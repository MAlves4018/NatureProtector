using Microsoft.Extensions.Options;
using Microsoft.Extensions.Hosting;
using NatureProtector.Prevention.Host.Configuration;
using NatureProtector.Shared.Contracts.Readings;
using NatureProtector.Shared.Messaging;

namespace NatureProtector.Prevention.Host.Processing;

public sealed class ControlledValidationProcessingFaultInjector : IProcessingFaultInjector
{
    private const string TransientFaultKind = "transient_failure";
    private const string PermanentFaultKind = "permanent_failure";

    private const string P3RetryTransientThenSuccess = "P3_RETRY_TRANSIENT_THEN_SUCCESS";
    private const string P3RetryExhaustedToQuarantine = "P3_RETRY_EXHAUSTED_TO_QUARANTINE";
    private const string P3PermanentFailureToQuarantine = "P3_PERMANENT_FAILURE_TO_QUARANTINE";

    private readonly ControlledValidationProcessingFaultOptions _options;
    private readonly IHostEnvironment _environment;
    private readonly ILogger<ControlledValidationProcessingFaultInjector> _logger;

    public ControlledValidationProcessingFaultInjector(
        IOptions<ControlledValidationProcessingFaultOptions> options,
        IHostEnvironment environment,
        ILogger<ControlledValidationProcessingFaultInjector> logger)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(environment);
        ArgumentNullException.ThrowIfNull(logger);

        _options = options.Value;
        _environment = environment;
        _logger = logger;

        if (_options.Enabled && !IsAllowedEnvironment(_environment.EnvironmentName))
        {
            throw new InvalidOperationException(
                $"ControlledValidation:ProcessingFaults is enabled for environment '{_environment.EnvironmentName}', but only Development/Evidence-style environments are allowed.");
        }
    }

    public ValueTask InjectAsync(
        EventEnvelope<SensorReadingProducedPayload> envelope,
        InboxProcessingLease lease,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(envelope);
        ArgumentNullException.ThrowIfNull(lease);
        cancellationToken.ThrowIfCancellationRequested();

        if (!_options.Enabled)
        {
            return ValueTask.CompletedTask;
        }

        var identity = ControlledValidationCorrelationIdentity.Parse(envelope.CorrelationId);

        foreach (var faultCase in _options.Cases)
        {
            if (!Matches(faultCase, envelope, identity))
            {
                continue;
            }

            InjectIfAttemptShouldFail(
                envelope,
                lease,
                faultCase.RunLabel ?? identity?.RunLabel ?? "<unknown>",
                faultCase.FaultCaseId ?? identity?.FaultCaseId ?? "<unknown>",
                NormalizeFaultKind(faultCase.FaultKind),
                faultCase.FailAttempts);

            return ValueTask.CompletedTask;
        }

        if (TryResolveBuiltInP3FaultCase(identity, out var builtInFaultCase))
        {
            InjectIfAttemptShouldFail(
                envelope,
                lease,
                builtInFaultCase.RunLabel,
                builtInFaultCase.FaultCaseId,
                builtInFaultCase.FaultKind,
                builtInFaultCase.FailAttempts);
        }

        return ValueTask.CompletedTask;
    }

    private bool IsAllowedEnvironment(string environmentName)
    {
        var allowed = _options.AllowedEnvironments is { Length: > 0 }
            ? _options.AllowedEnvironments
            : ["Development", "Evidence"];

        return allowed.Any(value =>
            string.Equals(value, environmentName, StringComparison.OrdinalIgnoreCase));
    }

    private void InjectIfAttemptShouldFail(
        EventEnvelope<SensorReadingProducedPayload> envelope,
        InboxProcessingLease lease,
        string runLabel,
        string faultCaseId,
        string faultKind,
        int failAttempts)
    {
        var maxFailedAttempts = Math.Max(1, failAttempts);
        if (lease.AttemptNumber > maxFailedAttempts)
        {
            return;
        }

        var normalizedFaultKind = NormalizeFaultKind(faultKind);
        var failureKind = normalizedFaultKind == PermanentFaultKind
            ? ProcessingFailureKind.Permanent
            : ProcessingFailureKind.Transient;

        _logger.LogWarning(
            "Injecting controlled validation processing fault | RunLabel={RunLabel} | FaultCaseId={FaultCaseId} | FaultKind={FaultKind} | EventId={EventId} | CorrelationId={CorrelationId} | Attempt={AttemptNumber}",
            runLabel,
            faultCaseId,
            normalizedFaultKind,
            envelope.EventId,
            envelope.CorrelationId,
            lease.AttemptNumber);

        throw new ControlledValidationProcessingFaultException(
            failureKind,
            normalizedFaultKind,
            $"Controlled validation processing fault '{normalizedFaultKind}' for '{faultCaseId}'.");
    }

    private bool TryResolveBuiltInP3FaultCase(
        ControlledValidationCorrelationIdentity? identity,
        out ResolvedProcessingFaultCase faultCase)
    {
        faultCase = default!;

        if (!_options.EnableBuiltInP3Cases || identity is null)
        {
            return false;
        }

        if (!IsAllowedBuiltInRunLabel(identity.RunLabel))
        {
            return false;
        }

        faultCase = identity.FaultCaseId switch
        {
            P3RetryTransientThenSuccess => new ResolvedProcessingFaultCase(
                identity.RunLabel,
                identity.FaultCaseId,
                TransientFaultKind,
                FailAttempts: 1),

            P3RetryExhaustedToQuarantine => new ResolvedProcessingFaultCase(
                identity.RunLabel,
                identity.FaultCaseId,
                TransientFaultKind,
                FailAttempts: 3),

            P3PermanentFailureToQuarantine => new ResolvedProcessingFaultCase(
                identity.RunLabel,
                identity.FaultCaseId,
                PermanentFaultKind,
                FailAttempts: 1),

            _ => default!
        };

        return faultCase is not null;
    }

    private bool IsAllowedBuiltInRunLabel(string runLabel)
    {
        if (string.IsNullOrWhiteSpace(runLabel))
        {
            return false;
        }

        var prefixes = _options.AllowedRunLabelPrefixes;
        if (prefixes is not { Length: > 0 })
        {
            return false;
        }

        return prefixes.Any(prefix =>
            !string.IsNullOrWhiteSpace(prefix) &&
            runLabel.StartsWith(prefix, StringComparison.Ordinal));
    }

    private static bool Matches(
        ProcessingFaultCaseOptions faultCase,
        EventEnvelope<SensorReadingProducedPayload> envelope,
        ControlledValidationCorrelationIdentity? identity)
    {
        if (string.IsNullOrWhiteSpace(faultCase.FaultCaseId) ||
            string.IsNullOrWhiteSpace(faultCase.RunLabel))
        {
            return false;
        }

        var hasEventIdAllowlist = faultCase.EventId is not null;
        if (faultCase.EventId is { } eventId && eventId != envelope.EventId)
        {
            return false;
        }

        var hasCorrelationAllowlist = !string.IsNullOrWhiteSpace(faultCase.CorrelationId);
        if (hasCorrelationAllowlist &&
            !string.Equals(faultCase.CorrelationId, envelope.CorrelationId, StringComparison.Ordinal))
        {
            return false;
        }

        if (identity is not null)
        {
            return string.Equals(faultCase.RunLabel, identity.RunLabel, StringComparison.Ordinal) &&
                   string.Equals(faultCase.FaultCaseId, identity.FaultCaseId, StringComparison.Ordinal);
        }

        return hasCorrelationAllowlist || hasEventIdAllowlist;
    }

    private static string NormalizeFaultKind(string? faultKind)
    {
        if (string.Equals(faultKind, PermanentFaultKind, StringComparison.OrdinalIgnoreCase))
        {
            return PermanentFaultKind;
        }

        if (string.Equals(faultKind, TransientFaultKind, StringComparison.OrdinalIgnoreCase))
        {
            return TransientFaultKind;
        }

        return TransientFaultKind;
    }

    private sealed record ResolvedProcessingFaultCase(
        string RunLabel,
        string FaultCaseId,
        string FaultKind,
        int FailAttempts);

    private sealed record ControlledValidationCorrelationIdentity(
        string RunLabel,
        string FaultCaseId)
    {
        public static ControlledValidationCorrelationIdentity? Parse(string? correlationId)
        {
            if (string.IsNullOrWhiteSpace(correlationId) ||
                !correlationId.StartsWith("cv:", StringComparison.Ordinal))
            {
                return null;
            }

            var parts = correlationId.Split(':');
            return parts.Length >= 4 &&
                !string.IsNullOrWhiteSpace(parts[1]) &&
                !string.IsNullOrWhiteSpace(parts[2])
                ? new ControlledValidationCorrelationIdentity(parts[1], parts[2])
                : null;
        }
    }
}
