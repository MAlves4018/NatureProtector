using Microsoft.EntityFrameworkCore;
using NatureProtector.Backoffice.Api.ControlPlane.Contracts;
using NatureProtector.Backoffice.Api.RuntimeOrchestration;
using NatureProtector.Core.Scenarios;
using NatureProtector.Infrastructure.Postgres.Persistence;
using NatureProtector.Infrastructure.Postgres.Pipeline;
using NatureProtector.Infrastructure.Postgres.Projection;
using NatureProtector.Shared.Observability;
using System.Diagnostics;
using System.Diagnostics.Metrics;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Linq.Dynamic.Core;
using System.Reflection;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.AspNetCore.Mvc;

namespace NatureProtector.Backoffice.Api.ControlPlane.Services;

// Feature slice: RuntimeOperations. Public behavior remains exposed through IControlPlaneService.

public sealed partial class PostgresControlPlaneService : IControlPlaneService
{
    private const int SynchronousWaitMarginSeconds = 30;

    // <phase5-slice id="runtime-operations-api">
    public async Task<RuntimeRunStartResponse> StartRuntimeRunAsync(
        RuntimeRunStartRequest request,
        CancellationToken cancellationToken)
    {
        var requestedAtUtc = DateTimeOffset.UtcNow;
        var requestId = Guid.NewGuid();
        var orchestratorCorrelationId = Guid.NewGuid().ToString("D");
        Guid? operationId = null;
        RuntimeEvidenceReference? evidence = null;
        var warnings = new List<string>();
        var requestedDegradationProfiles = NormalizeDegradationProfiles(
            request.DegradationProfiles,
            request.DegradationProfile);
        var requestedDegradationProfile = ToLegacyDegradationProfile(requestedDegradationProfiles)
            ?? NormalizeLegacyDegradationProfile(request.DegradationProfile);

        await using var dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);

        if (string.IsNullOrWhiteSpace(request.AreaCode) || string.IsNullOrWhiteSpace(request.ScenarioCode))
        {
            return RuntimeRunResponse("Rejected", "areaCode and scenarioCode are required.", null);
        }

        if (request.SensorCount is <= 0 || request.NumberOfCycles is <= 0 || request.IntervalSeconds is <= 0)
        {
            return RuntimeRunResponse("Rejected", "sensorCount, numberOfCycles and intervalSeconds must be positive when provided.", null);
        }

        var area = await dbContext.Areas
            .AsNoTracking()
            .SingleOrDefaultAsync(entity => entity.Code == request.AreaCode, cancellationToken);
        if (area is null)
        {
            return RuntimeRunResponse("Rejected", $"Area '{request.AreaCode}' was not found.", null);
        }

        var scenarioExists = await dbContext.ScenarioDefinitions
            .AsNoTracking()
            .AnyAsync(entity => entity.AreaId == area.Id && entity.Code == request.ScenarioCode, cancellationToken);
        if (!scenarioExists)
        {
            return RuntimeRunResponse("Rejected", $"Scenario '{request.ScenarioCode}' was not found for area '{request.AreaCode}'.", null);
        }

        var activeRunCount = await dbContext.SimulationRuns
            .AsNoTracking()
            .CountAsync(entity => entity.EndedAt == null, cancellationToken);
        if (activeRunCount > 0)
        {
            return RuntimeRunResponse("Rejected", $"Parallel runs are blocked by default. Found {activeRunCount} active run(s).", null);
        }

        if (request.SensorCount.HasValue)
        {
            var activeSensorCount = await dbContext.SensorNodes
                .AsNoTracking()
                .CountAsync(entity => entity.AreaId == area.Id && entity.IsActive, cancellationToken);
            if (request.SensorCount.Value > activeSensorCount)
            {
                return RuntimeRunResponse("Rejected", $"sensorCount {request.SensorCount.Value} exceeds {activeSensorCount} active sensor(s) for area '{request.AreaCode}'.", null);
            }
        }

        if (string.Equals(request.ScenarioCode, "scenario_c", StringComparison.OrdinalIgnoreCase) &&
            IsNoneOrEmpty(requestedDegradationProfiles))
        {
            warnings.Add("scenario_c is intended for degraded/operational comparison. With degradationProfile=none it may behave like a clean scenario.");
            warnings.Add("No calibrated scientific degradation is inferred by the API; use a non-none degradationProfile only when simulator support is explicit.");
        }

        if (!_runtimeRunOrchestrator.IsAvailable)
        {
            warnings.Add(_runtimeRunOrchestrator.AvailabilityMessage);
            return RuntimeRunResponse(
                "Validated",
                "Run request is valid; the configured runtime orchestration provider is unavailable.",
                null);
        }

        if (request.WaitForCompletion && request.NumberOfCycles.HasValue && request.IntervalSeconds.HasValue)
        {
            var nominalDurationSeconds = checked(
                request.NumberOfCycles.Value * request.IntervalSeconds.Value);
            var minimumTimeoutSeconds = checked(
                nominalDurationSeconds + SynchronousWaitMarginSeconds);

            if (request.TimeoutSeconds < minimumTimeoutSeconds)
            {
                return RuntimeRunResponse(
                    "Rejected",
                    $"timeoutSeconds must be at least {minimumTimeoutSeconds} seconds because synchronous waiting may cancel the runtime process.",
                    null);
            }
        }

        if (request.CollectEvidence)
        {
            if (_runtimeEvidenceSink.IsAvailable)
            {
                evidence = await _runtimeEvidenceSink.CreateAsync(
                    "runtime-runs",
                    requestedAtUtc,
                    request.RunLabel ?? request.ScenarioCode,
                    cancellationToken);
            }
            else
            {
                warnings.Add($"Evidence collection was requested, but {_runtimeEvidenceSink.AvailabilityMessage}");
            }
        }

        var operation = await ReserveRuntimeOperationAsync(
            requestId,
            orchestratorCorrelationId,
            request,
            _runtimeRunOrchestrator.Provider,
            evidence,
            cancellationToken);
        if (operation is null)
        {
            return RuntimeRunResponse("Rejected", "Another operational runtime launch or settlement is active.", null);
        }

        operationId = operation.OperationId;
        if (evidence is not null)
        {
            await _runtimeEvidenceSink.WriteJsonAsync(evidence, "request.json", request, cancellationToken);
            await WriteRuntimeSummaryEvidenceAsync(evidence.Location, "runtime-summary-before.json", request.AreaCode, cancellationToken);
        }

        var launchRequest = new RuntimeLaunchRequest(
            new RuntimeExecutionId(operation.OperationId),
            requestId,
            operation.IdempotencyKey,
            _environmentName,
            RuntimeLaunchProfile.Simulation,
            new RuntimeSimulationParameters(
                request.AreaCode,
                request.ScenarioCode,
                request.SensorCount,
                request.NumberOfCycles,
                request.IntervalSeconds,
                request.Seed,
                requestedDegradationProfile,
                requestedDegradationProfiles,
                orchestratorCorrelationId),
            null,
            request.CollectEvidence,
            request.WaitForCompletion,
            TimeSpan.FromSeconds(Math.Clamp(request.TimeoutSeconds, 5, MaximumRuntimeDeadlineSeconds)),
            evidence);

        RuntimeLaunchReceipt receipt;
        try
        {
            receipt = await _runtimeRunOrchestrator.StartAsync(launchRequest, cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            await UpdateRuntimeProviderStateAsync(
                operation.OperationId,
                RuntimeExecutionState.Running,
                providerReference: null,
                failureCode: RuntimeTerminationReason.RequestCancelled,
                failureMessage: "The HTTP request was cancelled after the runtime operation was reserved; provider observation continues.",
                CancellationToken.None);
            StartRuntimeExecutionObservation(operation.OperationId, request, requestedAtUtc, evidence, response: null);
            throw;
        }
        catch (Exception exception)
        {
            await UpdateRuntimeProviderStateAsync(
                operation.OperationId,
                RuntimeExecutionState.Failed,
                providerReference: null,
                failureCode: "provider_launch_exception",
                failureMessage: exception.Message,
                CancellationToken.None);
            var failed = RuntimeRunResponse(
                "Failed",
                "The configured runtime orchestration provider failed before launch acceptance.",
                null);
            if (evidence is not null)
            {
                await _runtimeEvidenceSink.WriteJsonAsync(evidence, "response.json", failed, CancellationToken.None);
                await _runtimeEvidenceSink.WriteTextAsync(evidence, "orchestrator-error.txt", exception.ToString(), CancellationToken.None);
            }
            return failed;
        }

        await ApplyRuntimeLaunchReceiptAsync(operation.OperationId, receipt, CancellationToken.None);

        var run = await FindRuntimeRunByCorrelationAsync(
            dbContext,
            request.AreaCode,
            request.ScenarioCode,
            requestedAtUtc.AddSeconds(-5),
            orchestratorCorrelationId,
            cancellationToken);
        if (run is not null)
        {
            await UpdateRuntimeOperationAsync(
                operation.OperationId,
                "RunObserved",
                receipt.State.ToString(),
                run.Status,
                "Pending",
                run.Id,
                null,
                null,
                null,
                cancellationToken);
        }

        var responseStatus = run is not null
            ? run.Status
            : receipt.State switch
            {
                RuntimeExecutionState.Rejected => "Rejected",
                RuntimeExecutionState.Failed => "Failed",
                RuntimeExecutionState.TimedOut => "TimedOut",
                RuntimeExecutionState.Cancelled => "Cancelled",
                RuntimeExecutionState.Succeeded => "ProducerCompleted",
                RuntimeExecutionState.Unknown => "LaunchAccepted",
                _ => "LaunchAccepted"
            };
        var responseMessage = receipt.State switch
        {
            RuntimeExecutionState.Rejected => receipt.Message ?? "The configured runtime orchestration provider rejected the request.",
            RuntimeExecutionState.Failed => receipt.Message ?? "The configured runtime orchestration provider failed to launch the run.",
            RuntimeExecutionState.TimedOut => receipt.Message ?? "The configured runtime orchestration provider timed out.",
            RuntimeExecutionState.Cancelled => receipt.Message ?? "The runtime execution was cancelled by the provider.",
            RuntimeExecutionState.Succeeded when run is null => "The producer completed; poll the persisted operation while the correlated run and pipeline settlement are reconciled.",
            _ when run is null => "The launch was accepted by the configured provider; poll the persisted operation identity for run observation and terminal outcome.",
            _ => "The provider accepted the launch and the correlated run was observed."
        };

        if (receipt.ReusedExistingExecution)
        {
            warnings.Add("The runtime provider reused the existing execution for this idempotency key.");
        }
        if (!string.IsNullOrWhiteSpace(receipt.RejectionCode))
        {
            warnings.Add($"Provider code: {receipt.RejectionCode}.");
        }

        var response = RuntimeRunResponse(responseStatus, responseMessage, ToRuntimeRun(run, warnings));
        if (evidence is not null)
        {
            await _runtimeEvidenceSink.WriteJsonAsync(evidence, "response.json", response, CancellationToken.None);
        }

        StartRuntimeExecutionObservation(operation.OperationId, request, requestedAtUtc, evidence, response);
        return response;

        RuntimeRunStartResponse RuntimeRunResponse(
            string status,
            string message,
            RuntimeRunSummaryResponse? run)
            => new(
                requestId,
                orchestratorCorrelationId,
                status,
                message,
                requestedAtUtc,
                new RuntimeRunOverrideValuesResponse(
                    request.SensorCount,
                    request.NumberOfCycles,
                    request.IntervalSeconds,
                    request.Seed,
                    requestedDegradationProfile,
                    orchestratorCorrelationId,
                    requestedDegradationProfiles),
                run,
                warnings.ToArray(),
                evidence?.Location,
                evidence?.Location,
                operationId);
    }

    public async Task<ControlledValidationP3RunResponse> StartControlledValidationP3Async(
        ControlledValidationP3RunRequest request,
        string environmentName,
        CancellationToken cancellationToken)
    {
        var requestedAtUtc = DateTimeOffset.UtcNow;
        var requestId = Guid.NewGuid();
        var notes = new List<string>
        {
            "Dedicated P3 endpoint: no arbitrary payload, fault-case list, routing key, sensor or area input is accepted.",
            "Query pack 11 is not executed by this endpoint; post-run audit remains mandatory.",
            "sensor_inactive and sensor_area_mismatch remain blocked_needs_fixture in the current P3 manifest."
        };

        if (!IsControlledValidationEnvironmentAllowed(environmentName))
        {
            return P3Response(
                "Rejected",
                "Controlled validation P3 execution is only available in Development or Evidence.",
                NormalizeControlledValidationRunLabel(request.RunLabel, requestedAtUtc),
                null,
                null);
        }

        var runLabel = NormalizeControlledValidationRunLabel(request.RunLabel, requestedAtUtc);
        if (!ControlledValidationRunLabelRegex.IsMatch(runLabel) ||
            !runLabel.StartsWith(ControlledValidationP3RunLabelPrefix, StringComparison.Ordinal))
        {
            return P3Response(
                "Rejected",
                $"runLabel must start with '{ControlledValidationP3RunLabelPrefix}' and contain only letters, digits, '.', '_' or '-'.",
                runLabel,
                null,
                null);
        }

        if (request.RunAuditAfterCompletion)
        {
            notes.Add("runAuditAfterCompletion was requested, but no safe Backoffice query-pack executor exists yet; auditRequired remains true.");
        }

        await using var dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);

        var activeRunCount = await dbContext.SimulationRuns
            .AsNoTracking()
            .CountAsync(entity => entity.EndedAt == null, cancellationToken);
        if (activeRunCount > 0)
        {
            return P3Response(
                "Blocked",
                $"Controlled validation P3 is blocked while {activeRunCount} active runtime run(s) exist.",
                runLabel,
                null,
                null);
        }

        var duplicateRunLabel = await dbContext.SimulationRuns
            .AsNoTracking()
            .AnyAsync(entity => entity.MetadataJson != null && entity.MetadataJson.Contains(runLabel), cancellationToken);
        if (duplicateRunLabel)
        {
            return P3Response(
                "Rejected",
                "runLabel was already observed in control.simulation_runs metadata; choose a unique label.",
                runLabel,
                null,
                null);
        }

        var area = await dbContext.Areas
            .AsNoTracking()
            .SingleOrDefaultAsync(entity => entity.Code == ControlledValidationP3AreaCode, cancellationToken);
        if (area is null)
        {
            return P3Response(
                "Rejected",
                $"Required P3 area '{ControlledValidationP3AreaCode}' was not found.",
                runLabel,
                null,
                null);
        }

        var scenarioExists = await dbContext.ScenarioDefinitions
            .AsNoTracking()
            .AnyAsync(entity => entity.AreaId == area.Id && entity.Code == ControlledValidationP3ScenarioCode, cancellationToken);
        if (!scenarioExists)
        {
            return P3Response(
                "Rejected",
                $"Required P3 scenario '{ControlledValidationP3ScenarioCode}' was not found for area '{ControlledValidationP3AreaCode}'.",
                runLabel,
                null,
                null);
        }

        var nominalSensor = await dbContext.SensorNodes
            .AsNoTracking()
            .Where(entity => entity.AreaId == area.Id && entity.IsActive)
            .OrderBy(entity => entity.Name)
            .Select(entity => new { entity.Id, entity.Name })
            .FirstOrDefaultAsync(cancellationToken);
        if (nominalSensor is null)
        {
            return P3Response(
                "Rejected",
                $"Required active sensor was not found for area '{ControlledValidationP3AreaCode}'.",
                runLabel,
                null,
                null);
        }

        var controlledValidationRunId = Guid.NewGuid();
        var simulationRunId = Guid.NewGuid();
        var sensorNotFoundId = Guid.NewGuid();
        var evidenceRoot = Path.Combine(_repositoryRoot, "docs", "evidence", "controlled-validation", "p3");
        var evidencePath = BuildControlledValidationEvidencePath(evidenceRoot, requestedAtUtc, runLabel);

        if (!_enableRuntimeProcessLaunch)
        {
            notes.Add("Runtime process launch is disabled for this service instance; P3 request was validated only.");
            return P3Response(
                "Validated",
                "Controlled validation P3 request is valid; process launch is disabled in this context.",
                runLabel,
                evidencePath,
                null);
        }

        Directory.CreateDirectory(evidencePath);
        await WriteJsonEvidenceAsync(evidencePath, "backoffice-request.json", request, cancellationToken);

        var startInfo = new ProcessStartInfo
        {
            FileName = "dotnet",
            WorkingDirectory = _repositoryRoot,
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = request.CollectEvidence,
            RedirectStandardError = request.CollectEvidence
        };
        startInfo.ArgumentList.Add("run");
        startInfo.ArgumentList.Add("--no-restore");
        startInfo.ArgumentList.Add("--configfile");
        startInfo.ArgumentList.Add("NuGet.Config");
        startInfo.ArgumentList.Add("--project");
        startInfo.ArgumentList.Add("src/NatureProtector.Simulator.Host");
        startInfo.Environment["DOTNET_ENVIRONMENT"] = environmentName;
        startInfo.Environment["Simulator__ControlPlaneEnabled"] = "true";
        startInfo.Environment["Simulator__ControlPlaneAreaCode"] = ControlledValidationP3AreaCode;
        startInfo.Environment["Simulator__ControlPlaneScenarioCode"] = ControlledValidationP3ScenarioCode;
        startInfo.Environment["ControlledValidation__Enabled"] = "true";
        startInfo.Environment["ControlledValidation__Phase"] = ControlledValidationP3Phase;
        startInfo.Environment["ControlledValidation__ControlledValidationRunId"] = controlledValidationRunId.ToString("D");
        startInfo.Environment["ControlledValidation__RunLabel"] = runLabel;
        startInfo.Environment["ControlledValidation__ScenarioCode"] = ControlledValidationP3ScenarioCode;
        startInfo.Environment["ControlledValidation__AreaId"] = area.Id.ToString("D");
        startInfo.Environment["ControlledValidation__SimulationRunId"] = simulationRunId.ToString("D");
        startInfo.Environment["ControlledValidation__NominalSensorId"] = nominalSensor.Id.ToString("D");
        startInfo.Environment["ControlledValidation__NominalSensorName"] = nominalSensor.Name;
        startInfo.Environment["ControlledValidation__SensorNotFoundId"] = sensorNotFoundId.ToString("D");
        startInfo.Environment["ControlledValidation__EventTime"] = requestedAtUtc.ToString("o");
        startInfo.Environment["ControlledValidation__WriteEvidenceSidecar"] = "true";
        startInfo.Environment["ControlledValidation__EvidenceOutputRoot"] = evidenceRoot;

        var process = Process.Start(startInfo);
        if (process is null)
        {
            return P3Response(
                "Failed",
                "Simulator.Host process could not be started for controlled validation P3.",
                runLabel,
                evidencePath,
                null);
        }

        Task<string>? stdoutTask = request.CollectEvidence ? process.StandardOutput.ReadToEndAsync(CancellationToken.None) : null;
        Task<string>? stderrTask = request.CollectEvidence ? process.StandardError.ReadToEndAsync(CancellationToken.None) : null;

        if (request.WaitForCompletion)
        {
            var timeout = TimeSpan.FromSeconds(Math.Clamp(request.TimeoutSeconds, 5, 3600));
            try
            {
                _ = await WaitForRuntimeProcessAsync(
                    process,
                    timeout,
                    terminateOnTimeout: true,
                    warning =>
                    {
                        notes.Add(warning);
                        return Task.CompletedTask;
                    },
                    cancellationToken);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                if (request.CollectEvidence)
                {
                    _ = Task.Run(() => CompleteControlledValidationP3EvidenceBundleAsync(
                        evidencePath,
                        process,
                        stdoutTask,
                        stderrTask,
                        CancellationToken.None), CancellationToken.None);
                }
                else
                {
                    StartDetachedProcessDisposal(process);
                }

                throw;
            }
        }

        var run = await FindRuntimeRunByCorrelationAsync(
            dbContext,
            ControlledValidationP3AreaCode,
            ControlledValidationP3ScenarioCode,
            requestedAtUtc.AddSeconds(-5),
            $"controlled-validation:{runLabel}",
            cancellationToken);

        var status = request.WaitForCompletion switch
        {
            true when process.HasExited && process.ExitCode != 0 => "Failed",
            true when run is not null => run.Status,
            true => "Completed",
            false => "Started"
        };
        var message = status switch
        {
            "Failed" => $"Controlled validation P3 process exited with code {process.ExitCode}.",
            "Started" => "Controlled validation P3 was started; query pack audit is still required.",
            _ when run is null => "Controlled validation P3 finished, but the persisted SimulationRun was not observed yet; query pack audit is still required.",
            _ => "Controlled validation P3 finished; query pack audit is still required."
        };
        var response = P3Response(
            status,
            message,
            runLabel,
            request.CollectEvidence ? evidencePath : null,
            ToRuntimeRun(run, notes));

        if (request.CollectEvidence)
        {
            await WriteJsonEvidenceAsync(evidencePath, "backoffice-response.json", response, CancellationToken.None);
            _ = Task.Run(() => CompleteControlledValidationP3EvidenceBundleAsync(
                evidencePath,
                process,
                stdoutTask,
                stderrTask,
                CancellationToken.None), CancellationToken.None);
        }
        else
        {
            StartDetachedProcessDisposal(process);
        }

        return response;

        ControlledValidationP3RunResponse P3Response(
            string status,
            string message,
            string label,
            string? evidenceDirectory,
            RuntimeRunSummaryResponse? run)
            => new(
                requestId,
                label,
                ControlledValidationP3Phase,
                status,
                environmentName,
                message,
                requestedAtUtc,
                ControlledValidationP3MessageCount,
                ControlledValidationP3ExecutableCases,
                ControlledValidationP3BlockedCases,
                evidenceDirectory,
                null,
                true,
                run,
                notes.ToArray());
    }

    public async Task<RuntimeResetResponse> ResetRuntimeStateAsync(
        RuntimeResetRequest request,
        CancellationToken cancellationToken)
    {
        var resetId = Guid.NewGuid();
        await using var dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
        await using var maintenanceLock = await RuntimeMaintenanceLock.AcquireAsync(dbContext, cancellationToken);

        var before = await BuildRuntimeTableCountsAsync(dbContext, cancellationToken);
        if (!string.Equals(request.Scope, "runtime-only", StringComparison.Ordinal))
        {
            return ResetResponse("Rejected", "scope must be 'runtime-only'.", before, before, [], 0);
        }

        if (!request.DryRun && !string.Equals(request.Confirm, "RESET_RUNTIME_STATE", StringComparison.Ordinal))
        {
            return ResetResponse("Rejected", "Reset requires exact confirmation text RESET_RUNTIME_STATE.", before, before, [], 0);
        }

        var reconciledOrphans = request.ReconcileTerminalOrphans
            ? await ReconcileTerminalRuntimeOrphansAsync(dbContext, cancellationToken)
            : 0;
        if (reconciledOrphans > 0)
        {
            before = await BuildRuntimeTableCountsAsync(dbContext, cancellationToken);
        }

        var activeRuns = await dbContext.SimulationRuns
            .AsNoTracking()
            .CountAsync(entity => entity.EndedAt == null, cancellationToken);
        if (activeRuns > 0)
        {
            return ResetResponse(
                "Rejected",
                $"Reset is blocked while {activeRuns} active run(s) exist after orphan reconciliation.",
                before,
                before,
                [],
                reconciledOrphans);
        }

        var activeOperations = await dbContext.RuntimeOperations.AsNoTracking()
            .CountAsync(entity => entity.TerminalOutcome == null, cancellationToken);
        var activePipeline = await dbContext.InboxEvents.AsNoTracking()
            .CountAsync(entity => entity.Status == InboxEventStatus.Pending ||
                entity.Status == InboxEventStatus.Processing || entity.Status == InboxEventStatus.RetryPending,
                cancellationToken);
        if (activeOperations > 0 || activePipeline > 0)
        {
            return ResetResponse(
                "Rejected",
                $"Reset requires quiescence; active operations={activeOperations}, pending/processing/retry inbox={activePipeline}.",
                before,
                before,
                [],
                reconciledOrphans);
        }

        IReadOnlyList<RuntimeResetStoreResponse> storeInspection;
        if (request.RequireExternalStores)
        {
            storeInspection = await _runtimeDataResetCoordinator.InspectAsync(cancellationToken);
            var unavailable = storeInspection
                .Where(store => !string.Equals(store.Status, "Ready", StringComparison.Ordinal))
                .ToArray();
            if (unavailable.Length > 0)
            {
                return ResetResponse(
                    "Rejected",
                    "Systemic reset requires quiescent and configured RabbitMQ and InfluxDB stores.",
                    before,
                    before,
                    storeInspection,
                    reconciledOrphans);
            }
        }
        else
        {
            storeInspection =
            [
                new RuntimeResetStoreResponse(
                    "ExternalStores",
                    "NotRequested",
                    null,
                    null,
                    "Caller explicitly requested a PostgreSQL-only reset; this is not evidence of a clean system reset.")
            ];
        }

        if (request.DryRun)
        {
            return ResetResponse(
                "DryRun",
                "No data was changed. The response records whether external stores are ready for a systemic reset.",
                before,
                before,
                storeInspection,
                reconciledOrphans);
        }

        IReadOnlyList<RuntimeResetStoreResponse> storeResults = storeInspection;
        if (request.RequireExternalStores)
        {
            storeResults = await _runtimeDataResetCoordinator.ResetAsync(resetId, cancellationToken);
            if (storeResults.Any(store => !string.Equals(store.Status, "Cleared", StringComparison.Ordinal)))
            {
                return ResetResponse(
                    "Failed",
                    "External-store cleanup did not complete, so PostgreSQL runtime state was preserved.",
                    before,
                    before,
                    storeResults,
                    reconciledOrphans);
            }
        }

        try
        {
            await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
            await DeleteRuntimeRowsAsync(dbContext, cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            var partialAfter = await BuildRuntimeTableCountsAsync(dbContext, CancellationToken.None);
            return ResetResponse(
                "PartialFailure",
                $"External stores may already be cleared, but PostgreSQL reset failed with {exception.GetType().Name}.",
                before,
                partialAfter,
                storeResults,
                reconciledOrphans);
        }

        var after = await BuildRuntimeTableCountsAsync(dbContext, cancellationToken);
        var remainingRuntimeRows = after.Sum(item => item.Count);
        var status = remainingRuntimeRows == 0 ? "Completed" : "PartialFailure";
        var message = remainingRuntimeRows == 0
            ? request.RequireExternalStores
                ? "Runtime state was cleared from PostgreSQL, RabbitMQ and InfluxDB; static configuration and user data were preserved."
                : "PostgreSQL runtime state was cleared, but external stores were not requested and system-wide cleanliness was not proved."
            : $"Reset completed with {remainingRuntimeRows} unexpected runtime row(s) still present.";
        return ResetResponse(status, message, before, after, storeResults, reconciledOrphans);

        RuntimeResetResponse ResetResponse(
            string status,
            string message,
            IReadOnlyList<RuntimeTableCountResponse> beforeCounts,
            IReadOnlyList<RuntimeTableCountResponse> afterCounts,
            IReadOnlyList<RuntimeResetStoreResponse> stores,
            int reconciled)
            => new(
                DateTimeOffset.UtcNow,
                request.DryRun,
                status,
                message,
                beforeCounts,
                afterCounts,
                resetId,
                stores,
                reconciled);
    }

    private static async Task<int> ReconcileTerminalRuntimeOrphansAsync(
        NatureProtectorControlDbContext dbContext,
        CancellationToken cancellationToken)
    {
        var now = DateTimeOffset.UtcNow;
        var pendingOperations = await dbContext.RuntimeOperations
            .Where(entity => entity.TerminalOutcome == null)
            .ToListAsync(cancellationToken);
        var expiredOperations = pendingOperations
            .Where(entity => entity.DeadlineAt <= now)
            .ToList();
        foreach (var operation in expiredOperations)
        {
            await ReconcileRuntimeOperationAsync(dbContext, operation, cancellationToken);
        }

        var activeRuns = await dbContext.SimulationRuns
            .Where(run => run.EndedAt == null)
            .ToListAsync(cancellationToken);
        if (activeRuns.Count == 0)
        {
            return 0;
        }

        var runIds = activeRuns.Select(run => run.Id).ToArray();
        var terminalOperations = await dbContext.RuntimeOperations
            .AsNoTracking()
            .Where(operation => operation.SimulationRunId != null &&
                runIds.Contains(operation.SimulationRunId.Value) &&
                operation.TerminalOutcome != null)
            .ToDictionaryAsync(operation => operation.SimulationRunId!.Value, cancellationToken);

        var reconciled = 0;
        foreach (var run in activeRuns)
        {
            if (!terminalOperations.TryGetValue(run.Id, out var operation))
            {
                continue;
            }

            run.EndedAt = operation.FinishedAt ?? now;
            run.Status = string.Equals(operation.TerminalOutcome, "Cancelled", StringComparison.OrdinalIgnoreCase)
                ? SimulationRunStatus.Cancelled
                : SimulationRunStatus.Failed;
            reconciled++;
        }

        if (reconciled > 0)
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }

        return reconciled;
    }

    private static async Task DeleteRuntimeRowsAsync(
        NatureProtectorControlDbContext dbContext,
        CancellationToken cancellationToken)
    {
        // Delete dependants before principals. ExecuteDelete keeps memory usage bounded even
        // after soak tests or incidents that created a large runtime history.
        await dbContext.ProcessingAttempts.ExecuteDeleteAsync(cancellationToken);
        await dbContext.RejectedEvents.ExecuteDeleteAsync(cancellationToken);
        await dbContext.QuarantinedEvents.ExecuteDeleteAsync(cancellationToken);
        await dbContext.AlertStates.ExecuteDeleteAsync(cancellationToken);
        await dbContext.CellCycleSnapshots.ExecuteDeleteAsync(cancellationToken);
        await dbContext.AreaCycleSnapshots.ExecuteDeleteAsync(cancellationToken);
        await dbContext.CycleObservations.ExecuteDeleteAsync(cancellationToken);
        await dbContext.CycleSettlements.ExecuteDeleteAsync(cancellationToken);
        await dbContext.DailyCellStates.ExecuteDeleteAsync(cancellationToken);
        await dbContext.AreaOperationalStates.ExecuteDeleteAsync(cancellationToken);
        await dbContext.CellOperationalStates.ExecuteDeleteAsync(cancellationToken);
        await dbContext.AreaRiskSnapshotLogs.ExecuteDeleteAsync(cancellationToken);
        await dbContext.RiskAssessmentLogs.ExecuteDeleteAsync(cancellationToken);
        await dbContext.AcceptedReadingLogs.ExecuteDeleteAsync(cancellationToken);
        await dbContext.InboxEvents.ExecuteDeleteAsync(cancellationToken);
        await dbContext.RuntimeOperations.ExecuteDeleteAsync(cancellationToken);
        await dbContext.SimulationRuns.ExecuteDeleteAsync(cancellationToken);
    }


    public async Task<IEnumerable<string?>> GetDBTablesList(
        CancellationToken cancellationToken
    )
    {
        await using var dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
        return dbContext.Model.GetEntityTypes()
            .Select(t =>
            {
                var schema = t.GetSchema();
                var table = t.GetTableName();
                return string.IsNullOrEmpty(schema) ? table : $"{schema}.{table}";
            })
            .Where(name => name != null)
            .ToList();
    }

    public async Task<IEnumerable<string?>> GetDBTableColumnsList(
        string tableName,
        CancellationToken cancellationToken
    )
    {
        await using var dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);

        var entityType = dbContext.Model.GetEntityTypes()
            .FirstOrDefault(t =>
            {
                var schema = t.GetSchema();
                var fullName = string.IsNullOrEmpty(schema) ? t.GetTableName() : $"{schema}.{t.GetTableName()}";
                return string.Equals(fullName, tableName, StringComparison.OrdinalIgnoreCase);
            });

        if (entityType is null)
        {
            return [];
        }

        return entityType.GetProperties().Select(p => p.GetColumnName()).Where(name => name != null).ToList();
    }

    public async Task<ROQueryResponse> QueryDBAsync(
        ROQueryRequest request,
        CancellationToken cancellationToken
    )
    {
        await using var dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);

        var entityTable = dbContext.Model.GetEntityTypes()
            .FirstOrDefault(t =>
            {
                var schema = t.GetSchema();
                var fullName = string.IsNullOrEmpty(schema) ? t.GetTableName() : $"{schema}.{t.GetTableName()}";
                return string.Equals(fullName, request.Table, StringComparison.OrdinalIgnoreCase);
            });

        if (string.IsNullOrWhiteSpace(request.Table) || entityTable is null)
        {
            return new ROQueryResponse([], [], [$"Table '{request.Table}' is not allowed."]);
        }

        var tableSchema = entityTable.GetSchema();
        var tableName = entityTable.GetTableName();
        var qualifiedTable = string.IsNullOrEmpty(tableSchema)
            ? $"\"{tableName}\""
            : $"\"{tableSchema}\".\"{tableName}\"";

        await using var connection = dbContext.Database.GetDbConnection();
        await connection.OpenAsync(cancellationToken);

        await using var command = connection.CreateCommand();

        var queryType = request.Type?.Trim().ToLowerInvariant() ?? "select";

        var tableColumns = entityTable.GetProperties().Select(p => p.GetColumnName()).Where(name => name != null).ToList();

        var limitations = new List<string>();
        if (queryType == "count")
        {
            command.CommandText = $"SELECT COUNT(*) AS count FROM {qualifiedTable}";
        }
        else
        {
            var limit = Math.Clamp(request.Limit ?? 100, 1, 1000);
            var offset = Math.Clamp(request.Offset ?? 0, 0, 10000);

            if (request.Limit > 1000)
                limitations.Add("Limit clamped to maximum allowed value (1000).");
            if (request.Offset > 10000)
                limitations.Add("Offset clamped to maximum allowed value (10000).");


            var validColumns = request.Columns?.Where(c => tableColumns.Contains(c)).ToArray() ?? [];
            var selectColumns = validColumns.Length > 0
                ? string.Join(", ", validColumns.Select(c => $"\"{c}\""))
                : "*";

            command.CommandText = $"SELECT {selectColumns} FROM {qualifiedTable} LIMIT @limit OFFSET @offset";

            var limitParam = command.CreateParameter();
            limitParam.ParameterName = "@limit";
            limitParam.Value = limit;
            command.Parameters.Add(limitParam);

            var offsetParam = command.CreateParameter();
            offsetParam.ParameterName = "@offset";
            offsetParam.Value = offset;
            command.Parameters.Add(offsetParam);
        }

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);

        var columns = Enumerable.Range(0, reader.FieldCount)
            .Select(i => reader.GetName(i))
            .ToList();

        var rows = new List<Dictionary<string, string?>>();
        while (await reader.ReadAsync(cancellationToken))
        {
            var row = new Dictionary<string, string?>();
            foreach (var col in columns)
            {
                var ordinal = reader.GetOrdinal(col);
                row[col] = reader.IsDBNull(ordinal) ? null : reader[ordinal]?.ToString();
            }
            rows.Add(row);
        }

        return new ROQueryResponse(columns, rows, limitations);
    }
    // </phase5-slice>

    // <phase5-slice id="runtime-operations-evidence">
    private static string NormalizeControlledValidationRunLabel(
        string? runLabel,
        DateTimeOffset requestedAtUtc)
        => string.IsNullOrWhiteSpace(runLabel)
            ? $"{ControlledValidationP3RunLabelPrefix}{requestedAtUtc:yyyyMMdd-HHmmss}-ui"
            : runLabel.Trim();

    private static string BuildControlledValidationEvidencePath(
        string evidenceRoot,
        DateTimeOffset requestedAtUtc,
        string runLabel)
        => Path.Combine(
            evidenceRoot,
            $"{requestedAtUtc:yyyyMMdd-HHmmss}-{SanitizePathSegment(runLabel)}");

    private static string SanitizePathSegment(string value)
    {
        var safeLabel = new string(value.Select(character =>
            char.IsLetterOrDigit(character) || character is '-' or '_' or '.' ? character : '-').ToArray());
        return string.IsNullOrWhiteSpace(safeLabel) ? "run" : safeLabel;
    }

    private string PrepareApiRunLogDirectory(DateTimeOffset requestedAtUtc, string label)
    {
        var safeLabel = SanitizePathSegment(label);

        var path = Path.Combine(
            _repositoryRoot,
            "docs",
            "evidence",
            "dev-runtime",
            $"{requestedAtUtc:yyyyMMdd-HHmmss}-{safeLabel}");
        Directory.CreateDirectory(path);
        return path;
    }

    private static async Task CompleteControlledValidationP3EvidenceBundleAsync(
        string evidenceDirectory,
        Process process,
        Task<string>? stdoutTask,
        Task<string>? stderrTask,
        CancellationToken cancellationToken)
    {
        try
        {
            try
            {
                await process.WaitForExitAsync(cancellationToken);
            }
            catch (OperationCanceledException)
            {
                // The endpoint has already returned or the request was cancelled; leave persisted evidence intact.
            }

            if (stdoutTask is not null)
            {
                await WriteTextEvidenceAsync(evidenceDirectory, "simulator-host.stdout.log", await stdoutTask);
            }

            if (stderrTask is not null)
            {
                await WriteTextEvidenceAsync(evidenceDirectory, "simulator-host.stderr.log", await stderrTask);
            }

            await WriteJsonEvidenceAsync(
                evidenceDirectory,
                "process-exit.json",
                new
                {
                    hasExited = process.HasExited,
                    exitCode = process.HasExited ? process.ExitCode : (int?)null,
                    completedAtUtc = DateTimeOffset.UtcNow
                },
                CancellationToken.None);
        }
        catch (Exception exception)
        {
            await WriteTextEvidenceAsync(evidenceDirectory, "evidence-error.txt", exception.ToString());
        }
        finally
        {
            process.Dispose();
        }
    }

    private static void StartDetachedProcessDisposal(Process process)
        => _ = Task.Run(async () =>
        {
            try
            {
                await WaitForRuntimeProcessAsync(
                    process,
                    timeout: null,
                    terminateOnTimeout: false,
                    _ => Task.CompletedTask,
                    CancellationToken.None);
            }
            catch
            {
                // Process ownership must still be released even when observation fails.
            }
            finally
            {
                process.Dispose();
            }
        }, CancellationToken.None);

    private void StartRuntimeExecutionObservation(
        Guid operationId,
        RuntimeRunStartRequest request,
        DateTimeOffset requestedAtUtc,
        RuntimeEvidenceReference? evidence,
        RuntimeRunStartResponse? response)
        => _ = Task.Run(
            () => ObserveRuntimeExecutionAsync(
                operationId,
                request,
                requestedAtUtc,
                evidence,
                response),
            CancellationToken.None);

    private async Task ObserveRuntimeExecutionAsync(
        Guid operationId,
        RuntimeRunStartRequest request,
        DateTimeOffset requestedAtUtc,
        RuntimeEvidenceReference? evidence,
        RuntimeRunStartResponse? response)
    {
        try
        {
            var executionId = new RuntimeExecutionId(operationId);
            var deadline = requestedAtUtc.Add(CalculateRuntimeOperationDeadline(request));
            while (DateTimeOffset.UtcNow < deadline)
            {
                var snapshot = await _runtimeRunOrchestrator.GetAsync(executionId, CancellationToken.None);
                if (snapshot is not null)
                {
                    await ApplyRuntimeExecutionSnapshotAsync(operationId, snapshot, CancellationToken.None);
                }

                var operation = await GetRuntimeOperationAsync(operationId, CancellationToken.None);
                if (operation?.TerminalOutcome is not null)
                {
                    break;
                }

                if (snapshot?.State is RuntimeExecutionState.Rejected
                    or RuntimeExecutionState.Failed
                    or RuntimeExecutionState.TimedOut
                    or RuntimeExecutionState.Cancelled)
                {
                    break;
                }

                await Task.Delay(TimeSpan.FromSeconds(1), CancellationToken.None);
            }

            _ = await GetRuntimeOperationAsync(operationId, CancellationToken.None);

            if (evidence is null)
            {
                return;
            }

            await EnsureRuntimeEvidenceAsync(operationId, CancellationToken.None);

            if (response is not null)
            {
                await WriteRunEvidenceSummaryAsync(evidence.Location, request, response, CancellationToken.None);
                await WritePostRunReportAsync(evidence.Location, request, response, CancellationToken.None);
            }
        }
        catch (Exception exception)
        {
            if (evidence is not null)
            {
                await _runtimeEvidenceSink.WriteTextAsync(
                    evidence,
                    "evidence-error.txt",
                    exception.ToString(),
                    CancellationToken.None);
            }
        }
    }

    private async Task WriteRuntimeSummaryEvidenceAsync(
        string logDirectory,
        string fileName,
        string areaCode,
        CancellationToken cancellationToken)
    {
        try
        {
            var summary = await GetRuntimeSummaryAsync(areaCode, DefaultRecentMinutes, cancellationToken);
            await WriteJsonEvidenceAsync(logDirectory, fileName, summary, cancellationToken);
        }
        catch (Exception exception)
        {
            await WriteJsonEvidenceAsync(logDirectory, fileName, new { error = exception.Message }, CancellationToken.None);
        }
    }

    private async Task WriteDiagnosticEvidenceAsync(
        string logDirectory,
        string diagnosticId,
        string areaCode,
        CancellationToken cancellationToken)
    {
        var fileName = $"diagnostics-{diagnosticId}.json";
        try
        {
            var result = await ExecuteRuntimeDiagnosticAsync(
                diagnosticId,
                new RuntimeDiagnosticRequest(areaCode, DefaultRecentMinutes),
                cancellationToken);
            await WriteJsonEvidenceAsync(logDirectory, fileName, result is null ? new { error = $"Unknown diagnostic '{diagnosticId}'." } : (object)result, cancellationToken);
        }
        catch (Exception exception)
        {
            await WriteJsonEvidenceAsync(logDirectory, fileName, new { error = exception.Message }, CancellationToken.None);
        }
    }

    private static async Task WriteJsonEvidenceAsync(
        string logDirectory,
        string fileName,
        object value,
        CancellationToken cancellationToken)
        => await File.WriteAllTextAsync(
            Path.Combine(logDirectory, fileName),
            JsonSerializer.Serialize(value, new JsonSerializerOptions { WriteIndented = true }),
            cancellationToken);

    private static async Task WriteTextEvidenceAsync(
        string logDirectory,
        string fileName,
        string value)
        => await File.WriteAllTextAsync(Path.Combine(logDirectory, fileName), value);

    private async Task WriteRunEvidenceSummaryAsync(
        string logDirectory,
        RuntimeRunStartRequest request,
        RuntimeRunStartResponse response,
        CancellationToken cancellationToken)
    {
        var expectedVsObserved = await ExecuteRuntimeDiagnosticAsync(
            "latest-run-expected-vs-observed",
            new RuntimeDiagnosticRequest(request.AreaCode, DefaultRecentMinutes),
            cancellationToken);
        var riskByMetric = await ExecuteRuntimeDiagnosticAsync(
            "latest-run-risk-by-metric",
            new RuntimeDiagnosticRequest(request.AreaCode, DefaultRecentMinutes),
            cancellationToken);
        var alertTransitions = await ExecuteRuntimeDiagnosticAsync(
            "recent-alert-transitions",
            new RuntimeDiagnosticRequest(request.AreaCode, DefaultRecentMinutes),
            cancellationToken);

        var lines = new List<string>
        {
            "# Runtime Run Evidence",
            string.Empty,
            $"- startedAt: {response.RequestedAtUtc:o}",
            $"- completedAt: {DateTimeOffset.UtcNow:o}",
            $"- runLabel: {request.RunLabel}",
            $"- areaCode: {request.AreaCode}",
            $"- scenarioCode: {request.ScenarioCode}",
            $"- simulationRunId: {response.Run?.Id}",
            $"- correlationId: {response.OrchestratorCorrelationId}",
            $"- evidenceDirectory: {response.EvidenceDirectory}",
            string.Empty,
            "## Requested Parameters",
            string.Empty,
            $"- sensorCount: {request.SensorCount}",
            $"- numberOfCycles: {request.NumberOfCycles}",
            $"- intervalSeconds: {request.IntervalSeconds}",
            $"- seed: {request.Seed}",
            $"- degradationProfile: {request.DegradationProfile}",
            $"- degradationProfiles: {string.Join(", ", NormalizeDegradationProfiles(request.DegradationProfiles, request.DegradationProfile))}",
            $"- collectEvidence: {request.CollectEvidence}",
            $"- waitForCompletion: {request.WaitForCompletion}",
            string.Empty,
            "## Resolved Parameters",
            string.Empty,
            $"- status: {response.Status}",
            $"- message: {response.Message}",
            $"- selectedSensors: {string.Join(", ", response.Run?.RunOverrides?.SelectedSensorNames ?? [])}",
            string.Empty,
            "## Expected Vs Observed",
            string.Empty
        };

        lines.AddRange((expectedVsObserved?.Rows ?? []).Select(row => $"- {row.GetValueOrDefault("metric")}: {row.GetValueOrDefault("value")}"));
        lines.Add(string.Empty);
        lines.Add("## Risk By Metric");
        lines.Add(string.Empty);
        lines.AddRange((riskByMetric?.Rows ?? []).Select(row => $"- {row.GetValueOrDefault("metricType")}: count={row.GetValueOrDefault("count")}; minScore={row.GetValueOrDefault("minScore")}; maxScore={row.GetValueOrDefault("maxScore")}; avgScore={row.GetValueOrDefault("avgScore")}"));
        lines.Add(string.Empty);
        lines.Add("## Alert Transitions");
        lines.Add(string.Empty);
        lines.AddRange((alertTransitions?.Rows ?? []).Select(row => $"- {row.GetValueOrDefault("status")} {row.GetValueOrDefault("alertState")} at {row.GetValueOrDefault("updatedAt")}"));
        lines.Add(string.Empty);
        lines.Add("## Limitations");
        lines.Add(string.Empty);
        lines.Add("- Evidence uses read-only runtime diagnostics and persisted projections; it does not recalculate risk or alert state.");

        await WriteTextEvidenceAsync(logDirectory, "summary.md", string.Join(Environment.NewLine, lines));
    }

    private async Task WritePostRunReportAsync(
        string logDirectory,
        RuntimeRunStartRequest request,
        RuntimeRunStartResponse response,
        CancellationToken cancellationToken)
    {
        var comparison = await ExecuteRuntimeDiagnosticAsync(
            "compare-latest-b-vs-c",
            new RuntimeDiagnosticRequest(request.AreaCode, DefaultRecentMinutes),
            cancellationToken);

        var lines = new List<string>
        {
            "# Post Run Report",
            string.Empty,
            $"Run `{request.ScenarioCode}` was submitted with correlation `{response.OrchestratorCorrelationId}`.",
            string.Empty,
            "## Final State",
            string.Empty,
            $"- status: {response.Status}",
            $"- simulationRunId: {response.Run?.Id}",
            $"- evidenceDirectory: {response.EvidenceDirectory}",
            string.Empty,
            "## Comparison",
            string.Empty
        };

        if (comparison is null || comparison.Rows.Count == 0)
        {
            lines.Add("No B/C comparison data was available.");
        }
        else
        {
            lines.AddRange(comparison.Rows.Select(row => $"- {row.GetValueOrDefault("scenario")} / {row.GetValueOrDefault("metric")}: {row.GetValueOrDefault("value")}"));
        }

        lines.Add(string.Empty);
        lines.Add("## Limitations");
        lines.Add(string.Empty);
        lines.Add("- This report is generated from persisted runtime diagnostics. It does not use screenshots and does not recompute risk.");

        await WriteTextEvidenceAsync(logDirectory, "post-run-report.md", string.Join(Environment.NewLine, lines));
    }

    private static void SetProcessEnvironmentIfDefined(ProcessStartInfo startInfo, string name, object? value)
    {
        if (value is null)
        {
            return;
        }

        startInfo.Environment[name] = Convert.ToString(value, System.Globalization.CultureInfo.InvariantCulture) ?? string.Empty;
    }

    private static string ResolveRepositoryRoot(string startPath)
    {
        var current = Directory.Exists(startPath)
            ? new DirectoryInfo(startPath)
            : new FileInfo(startPath).Directory;

        while (current is not null)
        {
            if (File.Exists(Path.Combine(current.FullName, "NatureProtector.sln")))
            {
                return current.FullName;
            }

            current = current.Parent;
        }

        return Directory.GetCurrentDirectory();
    }

    // </phase5-slice>

    private static IQueryable Set(DbContext context, Type entityType)
    {
        var method = typeof(DbContext).GetMethod("Set", Type.EmptyTypes)
            ?? throw new InvalidOperationException("Set method not found.");
        return (IQueryable)method.MakeGenericMethod(entityType).Invoke(context, null);
    }
}
