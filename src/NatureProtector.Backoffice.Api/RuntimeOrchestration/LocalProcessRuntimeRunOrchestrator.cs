using System.Collections.Concurrent;
using System.Diagnostics;
using Microsoft.Extensions.Options;

namespace NatureProtector.Backoffice.Api.RuntimeOrchestration;

public sealed class LocalProcessRuntimeRunOrchestrator : IRuntimeRunOrchestrator, IDisposable
{
    private readonly RuntimeOrchestrationOptions _options;
    private readonly IRuntimeEvidenceSink _evidenceSink;
    private readonly IHostEnvironment _environment;
    private readonly ILogger<LocalProcessRuntimeRunOrchestrator> _logger;
    private readonly ConcurrentDictionary<RuntimeExecutionId, ExecutionHandle> _executions = new();
    private readonly ConcurrentDictionary<string, RuntimeExecutionId> _idempotency = new(StringComparer.Ordinal);
    private readonly SemaphoreSlim _launchGate = new(1, 1);
    private bool _disposed;

    public LocalProcessRuntimeRunOrchestrator(
        IOptions<RuntimeOrchestrationOptions> options,
        IRuntimeEvidenceSink evidenceSink,
        IHostEnvironment environment,
        ILogger<LocalProcessRuntimeRunOrchestrator> logger)
    {
        _options = options.Value;
        _evidenceSink = evidenceSink;
        _environment = environment;
        _logger = logger;
    }

    public string Provider => "local-process";

    public bool IsAvailable => true;

    public string AvailabilityMessage =>
        $"Local process runtime orchestration is enabled with launch mode '{_options.LaunchMode}'.";

    public async Task<RuntimeLaunchReceipt> StartAsync(
        RuntimeLaunchRequest request,
        CancellationToken cancellationToken)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.IdempotencyKey);
        cancellationToken.ThrowIfCancellationRequested();

        await _launchGate.WaitAsync(cancellationToken);
        try
        {
            if (_idempotency.TryGetValue(request.IdempotencyKey, out var existingId))
            {
                if (_executions.TryGetValue(existingId, out var existing))
                {
                    return existing.ToReceipt(reusedExistingExecution: true);
                }

                _idempotency.TryRemove(request.IdempotencyKey, out _);
            }

            var timeout = request.WaitForCompletion ? ClampTimeout(request.Timeout) : (TimeSpan?)null;
            var executionId = request.ExecutionId;
            var startInfo = BuildStartInfo(request);
            var process = new Process
            {
                StartInfo = startInfo,
                EnableRaisingEvents = true
            };

            var handle = new ExecutionHandle(
                executionId,
                request.IdempotencyKey,
                request.Simulation.OrchestratorCorrelationId,
                request.Evidence,
                process,
                DateTimeOffset.UtcNow);

            if (!_executions.TryAdd(executionId, handle))
            {
                process.Dispose();
                throw new InvalidOperationException("Runtime execution registration failed.");
            }

            if (!_idempotency.TryAdd(request.IdempotencyKey, executionId))
            {
                _executions.TryRemove(executionId, out _);
                process.Dispose();
                throw new InvalidOperationException("Runtime execution idempotency registration failed.");
            }

            try
            {
                handle.MarkStarting();
                if (!process.Start())
                {
                    handle.MarkFailed("process_start_failed", "Simulator.Host process could not be started.");
                    handle.DisposeProcessWhenObserved();
                    return handle.ToReceipt(reusedExistingExecution: false);
                }

                handle.MarkRunning(process.Id);
                var stdoutTask = request.CollectEvidence
                    ? process.StandardOutput.ReadToEndAsync(CancellationToken.None)
                    : null;
                var stderrTask = request.CollectEvidence
                    ? process.StandardError.ReadToEndAsync(CancellationToken.None)
                    : null;

                var observationTask = ObserveProcessAsync(
                    handle,
                    stdoutTask,
                    stderrTask,
                    timeout,
                    CancellationToken.None);
                handle.SetObservationTask(observationTask);

                if (request.WaitForCompletion)
                {
                    await observationTask.WaitAsync(cancellationToken);
                }

                return handle.ToReceipt(reusedExistingExecution: false);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception exception)
            {
                handle.MarkFailed("process_launch_exception", exception.Message);
                await WriteEvidenceErrorAsync(request.Evidence, exception);
                handle.DisposeProcessWhenObserved();
                return handle.ToReceipt(reusedExistingExecution: false);
            }
        }
        finally
        {
            _launchGate.Release();
        }
    }

    public Task<RuntimeExecutionSnapshot?> GetAsync(
        RuntimeExecutionId executionId,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(
            _executions.TryGetValue(executionId, out var handle)
                ? handle.ToSnapshot()
                : null);
    }

    public async Task<RuntimeStopReceipt> StopAsync(
        RuntimeExecutionId executionId,
        RuntimeStopReason reason,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (!_executions.TryGetValue(executionId, out var handle))
        {
            return new RuntimeStopReceipt(
                executionId,
                RuntimeExecutionState.Unknown,
                false,
                "Runtime execution was not found in this local orchestrator instance.");
        }

        if (handle.IsTerminal)
        {
            return new RuntimeStopReceipt(
                executionId,
                handle.State,
                false,
                "Runtime execution is already terminal.");
        }

        try
        {
            if (!handle.Process.HasExited)
            {
                handle.Process.Kill(entireProcessTree: true);
                await handle.Process.WaitForExitAsync(cancellationToken);
            }

            handle.MarkCancelled($"Stopped by local orchestrator. Reason={reason}.");
            return new RuntimeStopReceipt(
                executionId,
                handle.State,
                true,
                $"Stop accepted. Reason={reason}.");
        }
        catch (Exception exception)
        {
            handle.MarkFailed("process_stop_failed", exception.Message);
            await WriteEvidenceErrorAsync(handle.Evidence, exception);
            return new RuntimeStopReceipt(
                executionId,
                handle.State,
                false,
                exception.Message);
        }
    }

    private ProcessStartInfo BuildStartInfo(RuntimeLaunchRequest request)
    {
        var workingDirectory = ResolveWorkingDirectory();
        var startInfo = new ProcessStartInfo
        {
            FileName = _options.ExecutablePath,
            WorkingDirectory = workingDirectory,
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = request.CollectEvidence,
            RedirectStandardError = request.CollectEvidence
        };

        if (string.Equals(
            _options.LaunchMode,
            RuntimeProcessLaunchModes.PublishedAssembly,
            StringComparison.OrdinalIgnoreCase))
        {
            startInfo.ArgumentList.Add(ResolvePath(workingDirectory, _options.SimulatorAssemblyPath));
        }
        else if (string.Equals(
            _options.LaunchMode,
            RuntimeProcessLaunchModes.Project,
            StringComparison.OrdinalIgnoreCase))
        {
            startInfo.ArgumentList.Add("run");
            startInfo.ArgumentList.Add("--no-restore");
            startInfo.ArgumentList.Add("--project");
            startInfo.ArgumentList.Add(ResolvePath(workingDirectory, _options.SimulatorProjectPath));
        }
        else
        {
            throw new InvalidOperationException(
                $"Unsupported RuntimeOrchestration:LaunchMode '{_options.LaunchMode}'.");
        }

        ApplyEnvironment(startInfo, request);
        return startInfo;
    }

    private static void ApplyEnvironment(ProcessStartInfo startInfo, RuntimeLaunchRequest request)
    {
        startInfo.Environment["DOTNET_ENVIRONMENT"] = request.Environment;
        startInfo.Environment["Simulator__ControlPlaneEnabled"] = "true";
        startInfo.Environment["Simulator__ControlPlaneAreaCode"] = request.Simulation.AreaCode;
        startInfo.Environment["Simulator__ControlPlaneScenarioCode"] = request.Simulation.ScenarioCode;
        startInfo.Environment["Simulator__RunOverrides__OrchestratorCorrelationId"] =
            request.Simulation.OrchestratorCorrelationId;

        SetIfDefined(startInfo, "Simulator__RunOverrides__SensorCount", request.Simulation.SensorCount);
        SetIfDefined(startInfo, "Simulator__RunOverrides__NumberOfCycles", request.Simulation.NumberOfCycles);
        SetIfDefined(startInfo, "Simulator__RunOverrides__IntervalSeconds", request.Simulation.IntervalSeconds);
        SetIfDefined(startInfo, "Simulator__RunOverrides__Seed", request.Simulation.Seed);
        SetIfDefined(
            startInfo,
            "Simulator__RunOverrides__DegradationProfile",
            request.Simulation.LegacyDegradationProfile);

        for (var index = 0; index < request.Simulation.DegradationProfiles.Count; index++)
        {
            startInfo.Environment[$"Simulator__RunOverrides__DegradationProfiles__{index}"] =
                request.Simulation.DegradationProfiles[index];
        }

        if (request.Profile != RuntimeLaunchProfile.ControlledValidationP3 ||
            request.ControlledValidation is null)
        {
            return;
        }

        var controlled = request.ControlledValidation;
        startInfo.Environment["ControlledValidation__Enabled"] = "true";
        startInfo.Environment["ControlledValidation__Phase"] = controlled.Phase;
        startInfo.Environment["ControlledValidation__ControlledValidationRunId"] =
            controlled.ControlledValidationRunId.ToString("D");
        startInfo.Environment["ControlledValidation__RunLabel"] = controlled.RunLabel;
        startInfo.Environment["ControlledValidation__ScenarioCode"] = controlled.ScenarioCode;
        startInfo.Environment["ControlledValidation__AreaId"] = controlled.AreaId.ToString("D");
        startInfo.Environment["ControlledValidation__SimulationRunId"] = controlled.SimulationRunId.ToString("D");
        startInfo.Environment["ControlledValidation__NominalSensorId"] = controlled.NominalSensorId.ToString("D");
        startInfo.Environment["ControlledValidation__NominalSensorName"] = controlled.NominalSensorName;
        startInfo.Environment["ControlledValidation__SensorNotFoundId"] = controlled.SensorNotFoundId.ToString("D");
        startInfo.Environment["ControlledValidation__EventTime"] = controlled.EventTime.ToString("o");
        startInfo.Environment["ControlledValidation__WriteEvidenceSidecar"] = "true";
        startInfo.Environment["ControlledValidation__EvidenceOutputRoot"] = controlled.EvidenceOutputReference;
    }

    private async Task ObserveProcessAsync(
        ExecutionHandle handle,
        Task<string>? stdoutTask,
        Task<string>? stderrTask,
        TimeSpan? timeout,
        CancellationToken cancellationToken)
    {
        try
        {
            if (timeout is null)
            {
                await handle.Process.WaitForExitAsync(cancellationToken);
            }
            else
            {
                using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                timeoutCts.CancelAfter(timeout.Value);
                try
                {
                    await handle.Process.WaitForExitAsync(timeoutCts.Token);
                }
                catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
                {
                    if (!handle.Process.HasExited)
                    {
                        handle.Process.Kill(entireProcessTree: true);
                        await handle.Process.WaitForExitAsync(CancellationToken.None);
                    }

                    handle.MarkTimedOut($"Simulator.Host exceeded timeout {timeout.Value.TotalSeconds:0} seconds.");
                }
            }

            if (!handle.IsTerminal)
            {
                handle.MarkExited(handle.Process.ExitCode);
            }

            if (handle.Evidence is not null)
            {
                if (stdoutTask is not null)
                {
                    await _evidenceSink.WriteTextAsync(
                        handle.Evidence,
                        "simulator-host.stdout.log",
                        await stdoutTask,
                        CancellationToken.None);
                }

                if (stderrTask is not null)
                {
                    await _evidenceSink.WriteTextAsync(
                        handle.Evidence,
                        "simulator-host.stderr.log",
                        await stderrTask,
                        CancellationToken.None);
                }

                await _evidenceSink.WriteJsonAsync(
                    handle.Evidence,
                    "process-exit.json",
                    handle.ToSnapshot(),
                    CancellationToken.None);
            }
        }
        catch (Exception exception)
        {
            handle.MarkFailed("process_observation_failed", exception.Message);
            await WriteEvidenceErrorAsync(handle.Evidence, exception);
            _logger.LogError(
                exception,
                "Local runtime execution observation failed. ExecutionId={ExecutionId}",
                handle.ExecutionId.Value);
        }
        finally
        {
            handle.DisposeProcessWhenObserved();
        }
    }

    private async Task WriteEvidenceErrorAsync(
        RuntimeEvidenceReference? evidence,
        Exception exception)
    {
        if (evidence is null || !_evidenceSink.IsAvailable)
        {
            return;
        }

        try
        {
            await _evidenceSink.WriteTextAsync(
                evidence,
                "orchestrator-error.txt",
                exception.ToString(),
                CancellationToken.None);
        }
        catch
        {
            // Evidence failure must not mask the orchestration failure.
        }
    }

    private TimeSpan ClampTimeout(TimeSpan requested)
    {
        var maximumSeconds = Math.Clamp(_options.MaximumTimeoutSeconds, 5, 24 * 60 * 60);
        var requestedSeconds = Math.Clamp((int)Math.Ceiling(requested.TotalSeconds), 5, maximumSeconds);
        return TimeSpan.FromSeconds(requestedSeconds);
    }

    private string ResolveWorkingDirectory()
    {
        var configured = _options.WorkingDirectory;
        if (!string.IsNullOrWhiteSpace(configured))
        {
            return Path.GetFullPath(
                Path.IsPathRooted(configured)
                    ? configured
                    : Path.Combine(_environment.ContentRootPath, configured));
        }

        if (string.Equals(
            _options.LaunchMode,
            RuntimeProcessLaunchModes.PublishedAssembly,
            StringComparison.OrdinalIgnoreCase))
        {
            return _environment.ContentRootPath;
        }

        var current = new DirectoryInfo(_environment.ContentRootPath);
        while (current is not null)
        {
            if (File.Exists(Path.Combine(current.FullName, "NatureProtector.sln")))
            {
                return current.FullName;
            }

            current = current.Parent;
        }

        throw new InvalidOperationException(
            "Local project launch requires RuntimeOrchestration:WorkingDirectory or a parent containing NatureProtector.sln.");
    }

    private static string ResolvePath(string workingDirectory, string path)
        => Path.IsPathRooted(path) ? path : Path.GetFullPath(Path.Combine(workingDirectory, path));

    private static void SetIfDefined(ProcessStartInfo startInfo, string name, object? value)
    {
        if (value is null)
        {
            return;
        }

        startInfo.Environment[name] =
            Convert.ToString(value, System.Globalization.CultureInfo.InvariantCulture) ?? string.Empty;
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        foreach (var handle in _executions.Values)
        {
            handle.Dispose();
        }

        _launchGate.Dispose();
    }

    private sealed class ExecutionHandle : IDisposable
    {
        private readonly object _syncRoot = new();
        private RuntimeExecutionState _state = RuntimeExecutionState.Accepted;
        private DateTimeOffset _updatedAtUtc;
        private DateTimeOffset? _startedAtUtc;
        private DateTimeOffset? _finishedAtUtc;
        private int? _exitCode;
        private string? _failureCode;
        private string? _failureMessage;
        private int? _processId;
        private Task? _observationTask;
        private bool _processDisposed;

        public ExecutionHandle(
            RuntimeExecutionId executionId,
            string idempotencyKey,
            string logCorrelation,
            RuntimeEvidenceReference? evidence,
            Process process,
            DateTimeOffset acceptedAtUtc)
        {
            ExecutionId = executionId;
            IdempotencyKey = idempotencyKey;
            LogCorrelation = logCorrelation;
            Evidence = evidence;
            Process = process;
            AcceptedAtUtc = acceptedAtUtc;
            _updatedAtUtc = acceptedAtUtc;
        }

        public RuntimeExecutionId ExecutionId { get; }
        public string IdempotencyKey { get; }
        public string LogCorrelation { get; }
        public RuntimeEvidenceReference? Evidence { get; }
        public Process Process { get; }
        public DateTimeOffset AcceptedAtUtc { get; }

        public RuntimeExecutionState State
        {
            get
            {
                lock (_syncRoot)
                {
                    return _state;
                }
            }
        }

        public bool IsTerminal
        {
            get
            {
                lock (_syncRoot)
                {
                    return IsTerminalState(_state);
                }
            }
        }

        public void MarkStarting() => Transition(RuntimeExecutionState.Starting);

        public void MarkRunning(int processId)
        {
            lock (_syncRoot)
            {
                _processId = processId;
                _startedAtUtc = DateTimeOffset.UtcNow;
                _state = RuntimeExecutionState.Running;
                _updatedAtUtc = _startedAtUtc.Value;
            }
        }

        public void MarkExited(int exitCode)
        {
            lock (_syncRoot)
            {
                _exitCode = exitCode;
                _finishedAtUtc = DateTimeOffset.UtcNow;
                _updatedAtUtc = _finishedAtUtc.Value;
                _state = exitCode == 0
                    ? RuntimeExecutionState.Succeeded
                    : RuntimeExecutionState.Failed;
                if (exitCode != 0)
                {
                    _failureCode = "process_exit_nonzero";
                    _failureMessage = $"Simulator.Host exited with code {exitCode}.";
                }
            }
        }

        public void MarkTimedOut(string message)
        {
            lock (_syncRoot)
            {
                _state = RuntimeExecutionState.TimedOut;
                _failureCode = "process_timeout";
                _failureMessage = message;
                _finishedAtUtc = DateTimeOffset.UtcNow;
                _updatedAtUtc = _finishedAtUtc.Value;
            }
        }

        public void MarkCancelled(string message)
        {
            lock (_syncRoot)
            {
                _state = RuntimeExecutionState.Cancelled;
                _failureCode = "process_cancelled";
                _failureMessage = message;
                _finishedAtUtc = DateTimeOffset.UtcNow;
                _updatedAtUtc = _finishedAtUtc.Value;
            }
        }

        public void MarkFailed(string code, string message)
        {
            lock (_syncRoot)
            {
                _state = RuntimeExecutionState.Failed;
                _failureCode = code;
                _failureMessage = message;
                _finishedAtUtc = DateTimeOffset.UtcNow;
                _updatedAtUtc = _finishedAtUtc.Value;
            }
        }

        public void SetObservationTask(Task observationTask)
        {
            lock (_syncRoot)
            {
                _observationTask = observationTask;
            }
        }

        public RuntimeLaunchReceipt ToReceipt(bool reusedExistingExecution)
        {
            lock (_syncRoot)
            {
                return new RuntimeLaunchReceipt(
                    ExecutionId,
                    _state,
                    AcceptedAtUtc,
                    _processId?.ToString(System.Globalization.CultureInfo.InvariantCulture),
                    LogCorrelation,
                    reusedExistingExecution,
                    _failureCode,
                    _failureMessage,
                    Evidence);
            }
        }

        public RuntimeExecutionSnapshot ToSnapshot()
        {
            lock (_syncRoot)
            {
                return new RuntimeExecutionSnapshot(
                    ExecutionId,
                    _state,
                    _updatedAtUtc,
                    _startedAtUtc,
                    _finishedAtUtc,
                    _exitCode,
                    _failureCode,
                    _failureMessage,
                    LogCorrelation,
                    Evidence);
            }
        }

        public void DisposeProcessWhenObserved()
        {
            lock (_syncRoot)
            {
                if (_processDisposed)
                {
                    return;
                }

                Process.Dispose();
                _processDisposed = true;
            }
        }

        public void Dispose()
        {
            Task? observationTask;
            lock (_syncRoot)
            {
                observationTask = _observationTask;
            }

            if (observationTask is { IsCompleted: false })
            {
                try
                {
                    if (!Process.HasExited)
                    {
                        Process.Kill(entireProcessTree: true);
                    }
                }
                catch
                {
                    // Best-effort cleanup during host shutdown.
                }
            }

            DisposeProcessWhenObserved();
        }

        private void Transition(RuntimeExecutionState state)
        {
            lock (_syncRoot)
            {
                _state = state;
                _updatedAtUtc = DateTimeOffset.UtcNow;
            }
        }

        private static bool IsTerminalState(RuntimeExecutionState state)
            => state is RuntimeExecutionState.Succeeded
                or RuntimeExecutionState.Failed
                or RuntimeExecutionState.TimedOut
                or RuntimeExecutionState.Cancelled
                or RuntimeExecutionState.Rejected;
    }
}
