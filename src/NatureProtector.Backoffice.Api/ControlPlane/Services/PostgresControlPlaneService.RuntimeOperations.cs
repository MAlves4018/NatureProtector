using Microsoft.EntityFrameworkCore;
using NatureProtector.Backoffice.Api.ControlPlane.Contracts;
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

namespace NatureProtector.Backoffice.Api.ControlPlane.Services;

// Feature slice: RuntimeOperations. Public behavior remains exposed through IControlPlaneService.

public sealed partial class PostgresControlPlaneService : IControlPlaneService
{
    // <phase5-slice id="runtime-operations-api">
    public async Task<RuntimeRunStartResponse> StartRuntimeRunAsync(
        RuntimeRunStartRequest request,
        CancellationToken cancellationToken)
    {
        var requestedAtUtc = DateTimeOffset.UtcNow;
        var requestId = Guid.NewGuid();
        var orchestratorCorrelationId = Guid.NewGuid().ToString("D");
        var warnings = new List<string>();
        var requestedDegradationProfiles = NormalizeDegradationProfiles(
            request.DegradationProfiles,
            request.DegradationProfile);
        var requestedDegradationProfile = ToLegacyDegradationProfile(requestedDegradationProfiles)
            ?? NormalizeLegacyDegradationProfile(request.DegradationProfile);

        await using var dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);

        if (string.IsNullOrWhiteSpace(request.AreaCode) || string.IsNullOrWhiteSpace(request.ScenarioCode))
        {
            return RuntimeRunResponse("Rejected", "areaCode and scenarioCode are required.", null, null);
        }

        if (request.SensorCount is <= 0 || request.NumberOfCycles is <= 0 || request.IntervalSeconds is <= 0)
        {
            return RuntimeRunResponse("Rejected", "sensorCount, numberOfCycles and intervalSeconds must be positive when provided.", null, null);
        }

        var area = await dbContext.Areas
            .AsNoTracking()
            .SingleOrDefaultAsync(entity => entity.Code == request.AreaCode, cancellationToken);
        if (area is null)
        {
            return RuntimeRunResponse("Rejected", $"Area '{request.AreaCode}' was not found.", null, null);
        }

        var scenarioExists = await dbContext.ScenarioDefinitions
            .AsNoTracking()
            .AnyAsync(entity => entity.AreaId == area.Id && entity.Code == request.ScenarioCode, cancellationToken);
        if (!scenarioExists)
        {
            return RuntimeRunResponse("Rejected", $"Scenario '{request.ScenarioCode}' was not found for area '{request.AreaCode}'.", null, null);
        }

        if (!request.AllowParallelRun)
        {
            var activeRunCount = await dbContext.SimulationRuns
                .AsNoTracking()
                .CountAsync(entity => entity.EndedAt == null, cancellationToken);
            if (activeRunCount > 0)
            {
                return RuntimeRunResponse("Rejected", $"Parallel runs are blocked by default. Found {activeRunCount} active run(s).", null, null);
            }
        }

        if (request.SensorCount.HasValue)
        {
            var activeSensorCount = await dbContext.SensorNodes
                .AsNoTracking()
                .CountAsync(entity => entity.AreaId == area.Id && entity.IsActive, cancellationToken);
            if (request.SensorCount.Value > activeSensorCount)
            {
                return RuntimeRunResponse("Rejected", $"sensorCount {request.SensorCount.Value} exceeds {activeSensorCount} active sensor(s) for area '{request.AreaCode}'.", null, null);
            }
        }

        if (string.Equals(request.ScenarioCode, "scenario_c", StringComparison.OrdinalIgnoreCase) &&
            IsNoneOrEmpty(requestedDegradationProfiles))
        {
            warnings.Add("scenario_c is intended for degraded/operational comparison. With degradationProfile=none it may behave like a clean scenario.");
            warnings.Add("No calibrated scientific degradation is inferred by the API; use a non-none degradationProfile only when simulator support is explicit.");
        }

        if (!_enableRuntimeProcessLaunch)
        {
            warnings.Add("Runtime process launch is disabled for this service instance; request was validated only.");
            return RuntimeRunResponse("Validated", "Run request is valid; process launch is disabled in this context.", null, null);
        }

        var logDirectory = PrepareApiRunLogDirectory(requestedAtUtc, request.RunLabel ?? request.ScenarioCode);
        var markerPath = Path.Combine(logDirectory, "request.json");
        await File.WriteAllTextAsync(
            markerPath,
            JsonSerializer.Serialize(request with { }, new JsonSerializerOptions { WriteIndented = true }),
            cancellationToken);
        if (request.CollectEvidence)
        {
            await WriteRuntimeSummaryEvidenceAsync(logDirectory, "runtime-summary-before.json", request.AreaCode, cancellationToken);
        }

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
        startInfo.Environment["DOTNET_ENVIRONMENT"] = "Development";
        startInfo.Environment["Simulator__ControlPlaneEnabled"] = "true";
        startInfo.Environment["Simulator__ControlPlaneAreaCode"] = request.AreaCode;
        startInfo.Environment["Simulator__ControlPlaneScenarioCode"] = request.ScenarioCode;
        startInfo.Environment["Simulator__RunOverrides__OrchestratorCorrelationId"] = orchestratorCorrelationId;
        SetProcessEnvironmentIfDefined(startInfo, "Simulator__RunOverrides__SensorCount", request.SensorCount);
        SetProcessEnvironmentIfDefined(startInfo, "Simulator__RunOverrides__NumberOfCycles", request.NumberOfCycles);
        SetProcessEnvironmentIfDefined(startInfo, "Simulator__RunOverrides__IntervalSeconds", request.IntervalSeconds);
        SetProcessEnvironmentIfDefined(startInfo, "Simulator__RunOverrides__Seed", request.Seed);
        SetProcessEnvironmentIfDefined(startInfo, "Simulator__RunOverrides__DegradationProfile", requestedDegradationProfile);
        for (var index = 0; index < requestedDegradationProfiles.Count; index++)
        {
            startInfo.Environment[$"Simulator__RunOverrides__DegradationProfiles__{index}"] = requestedDegradationProfiles[index];
        }

        var process = Process.Start(startInfo);
        if (process is null)
        {
            return RuntimeRunResponse("FailedToStart", "Simulator.Host process could not be started.", null, logDirectory);
        }

        Task<string>? stdoutTask = request.CollectEvidence ? process.StandardOutput.ReadToEndAsync(cancellationToken) : null;
        Task<string>? stderrTask = request.CollectEvidence ? process.StandardError.ReadToEndAsync(cancellationToken) : null;

        if (request.WaitForCompletion)
        {
            var timeout = TimeSpan.FromSeconds(Math.Clamp(request.TimeoutSeconds, 5, 3600));
            using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeoutCts.CancelAfter(timeout);
            try
            {
                await process.WaitForExitAsync(timeoutCts.Token);
            }
            catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
            {
                await TryTerminateProcessTreeAsync(
                    process,
                    timeout,
                    warning =>
                    {
                        warnings.Add(warning);
                        return Task.CompletedTask;
                    });
            }
        }

        var run = await FindRuntimeRunByCorrelationAsync(
            dbContext,
            request.AreaCode,
            request.ScenarioCode,
            requestedAtUtc.AddSeconds(-5),
            orchestratorCorrelationId,
            cancellationToken);

        var response = RuntimeRunResponse(
            run is null ? "Started" : run.Status,
            run is null ? "Simulator.Host was started; the run has not appeared in control.simulation_runs yet." : "Simulator.Host was started and the run was observed.",
            ToRuntimeRun(run, warnings),
            logDirectory);

        if (request.CollectEvidence)
        {
            await WriteJsonEvidenceAsync(logDirectory, "response.json", response, cancellationToken);
            _ = Task.Run(() => CompleteRunEvidenceBundleAsync(
                logDirectory,
                request,
                response,
                process,
                stdoutTask,
                stderrTask,
                CancellationToken.None), CancellationToken.None);
        }

        return response;

        RuntimeRunStartResponse RuntimeRunResponse(
            string status,
            string message,
            RuntimeRunSummaryResponse? run,
            string? directory)
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
                directory,
                request.CollectEvidence ? directory : null);
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

        Task<string>? stdoutTask = request.CollectEvidence ? process.StandardOutput.ReadToEndAsync(cancellationToken) : null;
        Task<string>? stderrTask = request.CollectEvidence ? process.StandardError.ReadToEndAsync(cancellationToken) : null;

        if (request.WaitForCompletion)
        {
            var timeout = TimeSpan.FromSeconds(Math.Clamp(request.TimeoutSeconds, 5, 3600));
            using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeoutCts.CancelAfter(timeout);
            try
            {
                await process.WaitForExitAsync(timeoutCts.Token);
            }
            catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
            {
                await TryTerminateProcessTreeAsync(
                    process,
                    timeout,
                    warning =>
                    {
                        notes.Add(warning);
                        return Task.CompletedTask;
                    });
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
            await WriteJsonEvidenceAsync(evidencePath, "backoffice-response.json", response, cancellationToken);
            _ = Task.Run(() => CompleteControlledValidationP3EvidenceBundleAsync(
                evidencePath,
                process,
                stdoutTask,
                stderrTask,
                CancellationToken.None), CancellationToken.None);
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
        await using var dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
        var before = await BuildRuntimeTableCountsAsync(dbContext, cancellationToken);

        if (!string.Equals(request.Scope, "runtime-only", StringComparison.Ordinal))
        {
            return new RuntimeResetResponse(DateTimeOffset.UtcNow, request.DryRun, "Rejected", "scope must be 'runtime-only'.", before, before);
        }

        if (!request.DryRun && !string.Equals(request.Confirm, "RESET_RUNTIME_STATE", StringComparison.Ordinal))
        {
            return new RuntimeResetResponse(DateTimeOffset.UtcNow, false, "Rejected", "Reset requires exact confirmation text RESET_RUNTIME_STATE.", before, before);
        }

        var activeRuns = await dbContext.SimulationRuns
            .AsNoTracking()
            .CountAsync(entity => entity.EndedAt == null, cancellationToken);
        if (activeRuns > 0)
        {
            return new RuntimeResetResponse(DateTimeOffset.UtcNow, request.DryRun, "Rejected", $"Reset is blocked while {activeRuns} active run(s) exist.", before, before);
        }

        if (request.DryRun)
        {
            return new RuntimeResetResponse(DateTimeOffset.UtcNow, true, "DryRun", "No data was changed.", before, before);
        }

        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);

        dbContext.ProcessingAttempts.RemoveRange(await dbContext.ProcessingAttempts.ToListAsync(cancellationToken));
        dbContext.RejectedEvents.RemoveRange(await dbContext.RejectedEvents.ToListAsync(cancellationToken));
        dbContext.QuarantinedEvents.RemoveRange(await dbContext.QuarantinedEvents.ToListAsync(cancellationToken));
        dbContext.InboxEvents.RemoveRange(await dbContext.InboxEvents.ToListAsync(cancellationToken));
        dbContext.AcceptedReadingLogs.RemoveRange(await dbContext.AcceptedReadingLogs.ToListAsync(cancellationToken));
        dbContext.RiskAssessmentLogs.RemoveRange(await dbContext.RiskAssessmentLogs.ToListAsync(cancellationToken));
        dbContext.AlertStates.RemoveRange(await dbContext.AlertStates.ToListAsync(cancellationToken));
        dbContext.AreaOperationalStates.RemoveRange(await dbContext.AreaOperationalStates.ToListAsync(cancellationToken));
        dbContext.CellOperationalStates.RemoveRange(await dbContext.CellOperationalStates.ToListAsync(cancellationToken));
        dbContext.AreaRiskSnapshotLogs.RemoveRange(await dbContext.AreaRiskSnapshotLogs.ToListAsync(cancellationToken));
        dbContext.SimulationRuns.RemoveRange(await dbContext.SimulationRuns.ToListAsync(cancellationToken));

        await dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        var after = await BuildRuntimeTableCountsAsync(dbContext, cancellationToken);
        return new RuntimeResetResponse(DateTimeOffset.UtcNow, false, "Completed", "Runtime state was reset. Control plane tables were not cleared.", before, after);
    }


    public async Task<IEnumerable<string?>> GetDBTablesList(
        CancellationToken cancellationToken
    )
    {
        await using var dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
        return dbContext.Model.GetEntityTypes().Select(t => t.GetTableName()).Where(name => name != null).ToList();
    }

    public async Task<ROQueryResponse> QueryDBAsync(
        ROQueryRequest request,
        CancellationToken cancellationToken
    )
    {
        await using var dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
        
        await using var connection = dbContext.Database.GetDbConnection();
        await connection.OpenAsync(cancellationToken);

        await using var command = connection.CreateCommand();
        command.CommandText = request.Query;

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
        /*
        var chosenEntityType = dbContext.Model.GetEntityTypes().FirstOrDefault(t => t.GetTableName() == request.Table);
        var chosenTable = chosenEntityType?.ClrType;


        if (chosenTable == null || chosenEntityType == null)
        {
            return new ROQueryResponse(
                new List<string>(),
                new List<Dictionary<string, string?>>(),
                new List<string> { $"Table '{request.Table}' was not found in the database." }
            );
        }

        IQueryable query = Set(dbContext, chosenTable);

        if (request.Offset.HasValue && request.Offset.Value > 0)
        {
            query = DynamicQueryableExtensions.Skip(query, request.Offset.Value);
        }

        if (request.Limit.HasValue && request.Limit.Value > 0)
        {
            query = DynamicQueryableExtensions.Take(query, request.Limit.Value);
        }

        var dynamicList = await query.ToDynamicListAsync(cancellationToken);

        var columns = new List<string>();
        var rows = new List<Dictionary<string, string?>>();
        var limitations = new List<string>();

        if (dynamicList.Count > 0)
        {
            object firstRow = (object)dynamicList[0];

            var columnProperties = chosenEntityType.GetProperties()
                .Where(p => !p.IsShadowProperty())
                .ToList();

            columns.AddRange(columnProperties.Select(p => p.Name));

            foreach (object item in dynamicList)
            {
                var rowDict = new Dictionary<string, string?>();
                foreach (var col in columns)
                {
                    var value = item.GetType().GetProperty(col)?.GetValue(item, null);
                    rowDict[col] = value?.ToString();
                }
                rows.Add(rowDict);
            }
        }

        if (request.Limit.HasValue) limitations.Add($"Max results limited to {request.Limit.Value}");
        if (request.Offset.HasValue) limitations.Add($"Skipped the first {request.Offset.Value} rows");
        return new ROQueryResponse(columns, rows, limitations);
        */

        return new ROQueryResponse(columns, rows, []);
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

    private async Task CompleteRunEvidenceBundleAsync(
        string logDirectory,
        RuntimeRunStartRequest request,
        RuntimeRunStartResponse response,
        Process process,
        Task<string>? stdoutTask,
        Task<string>? stderrTask,
        CancellationToken cancellationToken)
    {
        try
        {
            var timeout = TimeSpan.FromSeconds(Math.Clamp(request.TimeoutSeconds, 5, 3600));
            using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeoutCts.CancelAfter(timeout);

            try
            {
                await process.WaitForExitAsync(timeoutCts.Token);
            }
            catch (OperationCanceledException)
            {
                var evidenceWarnings = new List<string>();
                await TryTerminateProcessTreeAsync(
                    process,
                    timeout,
                    warning =>
                    {
                        evidenceWarnings.Add(warning);
                        return Task.CompletedTask;
                    });

                if (evidenceWarnings.Count > 0)
                {
                    await WriteTextEvidenceAsync(
                        logDirectory,
                        "evidence-warning.txt",
                        string.Join(Environment.NewLine, evidenceWarnings));
                }
            }

            if (stdoutTask is not null)
            {
                await WriteTextEvidenceAsync(logDirectory, "simulator-host.stdout.log", await stdoutTask);
            }

            if (stderrTask is not null)
            {
                await WriteTextEvidenceAsync(logDirectory, "simulator-host.stderr.log", await stderrTask);
            }

            await WriteRuntimeSummaryEvidenceAsync(logDirectory, "runtime-summary-after.json", request.AreaCode, cancellationToken);
            await WriteDiagnosticEvidenceAsync(logDirectory, "runtime-table-counts", request.AreaCode, cancellationToken);
            await WriteDiagnosticEvidenceAsync(logDirectory, "latest-runs", request.AreaCode, cancellationToken);
            await WriteDiagnosticEvidenceAsync(logDirectory, "latest-run-expected-vs-observed", request.AreaCode, cancellationToken);
            await WriteDiagnosticEvidenceAsync(logDirectory, "latest-run-events-by-cycle", request.AreaCode, cancellationToken);
            await WriteDiagnosticEvidenceAsync(logDirectory, "latest-run-risk-by-metric", request.AreaCode, cancellationToken);
            await WriteDiagnosticEvidenceAsync(logDirectory, "latest-run-np-vs-fwi-kbdi", request.AreaCode, cancellationToken);
            await WriteDiagnosticEvidenceAsync(logDirectory, "latest-run-components", request.AreaCode, cancellationToken);
            await WriteDiagnosticEvidenceAsync(logDirectory, "latest-run-quality-by-profile", request.AreaCode, cancellationToken);
            await WriteDiagnosticEvidenceAsync(logDirectory, "latest-run-degradation-effects", request.AreaCode, cancellationToken);
            await WriteDiagnosticEvidenceAsync(logDirectory, "latest-run-cell-context", request.AreaCode, cancellationToken);
            await WriteDiagnosticEvidenceAsync(logDirectory, "latest-run-fwi-input-completeness", request.AreaCode, cancellationToken);
            await WriteDiagnosticEvidenceAsync(logDirectory, "latest-run-kbdi-input-completeness", request.AreaCode, cancellationToken);
            await WriteDiagnosticEvidenceAsync(logDirectory, "latest-run-coverage-freshness", request.AreaCode, cancellationToken);
            await WriteDiagnosticEvidenceAsync(logDirectory, "area-operational-state", request.AreaCode, cancellationToken);
            await WriteDiagnosticEvidenceAsync(logDirectory, "cell-operational-states", request.AreaCode, cancellationToken);
            await WriteDiagnosticEvidenceAsync(logDirectory, "active-alerts", request.AreaCode, cancellationToken);
            await WriteDiagnosticEvidenceAsync(logDirectory, "recent-alert-transitions", request.AreaCode, cancellationToken);
            await WriteDiagnosticEvidenceAsync(logDirectory, "compare-latest-b-vs-c", request.AreaCode, cancellationToken);

            await WriteRunEvidenceSummaryAsync(logDirectory, request, response, cancellationToken);
            await WritePostRunReportAsync(logDirectory, request, response, cancellationToken);
        }
        catch (Exception exception)
        {
            await WriteTextEvidenceAsync(logDirectory, "evidence-error.txt", exception.ToString());
        }
        finally
        {
            process.Dispose();
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
