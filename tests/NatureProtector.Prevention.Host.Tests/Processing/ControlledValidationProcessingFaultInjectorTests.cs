using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NatureProtector.Prevention.Host.Configuration;
using NatureProtector.Prevention.Host.Processing;
using NatureProtector.Prevention.Host.Tests.TestData;

namespace NatureProtector.Prevention.Host.Tests.Processing;

public sealed class ControlledValidationProcessingFaultInjectorTests
{
    [Fact]
    public async Task NoOpProcessingFaultInjector_DoesNotThrow()
    {
        var injector = new NoOpProcessingFaultInjector();
        var envelope = EnvelopeFactory.Create();
        var lease = CreateLease(attemptNumber: 1);

        await injector.InjectAsync(envelope, lease, CancellationToken.None);
    }

    [Fact]
    public async Task InjectAsync_DoesNothing_WhenDisabled()
    {
        var injector = CreateInjector(new ControlledValidationProcessingFaultOptions
        {
            Enabled = false,
            Cases =
            [
                CreateCase(
                    runLabel: "p1-smoke",
                    faultCaseId: "N5_TRANSIENT_FAILURE",
                    faultKind: "transient_failure")
            ]
        });
        var envelope = CreateControlledEnvelope("p1-smoke", "N5_TRANSIENT_FAILURE", sequence: 1);

        await injector.InjectAsync(envelope, CreateLease(attemptNumber: 1), CancellationToken.None);
    }

    [Fact]
    public void Ctor_Throws_WhenEnabledOutsideAllowedEnvironment()
    {
        var ex = Assert.Throws<InvalidOperationException>(() => CreateInjector(
            new ControlledValidationProcessingFaultOptions
            {
                Enabled = true,
                AllowedEnvironments = ["Development", "Evidence"],
                Cases =
                [
                    CreateCase(
                        runLabel: "p1-smoke",
                        faultCaseId: "N5_TRANSIENT_FAILURE",
                        faultKind: "transient_failure")
                ]
            },
            environmentName: "Production"));

        Assert.Contains("ControlledValidation:ProcessingFaults is enabled", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task InjectAsync_ThrowsTransientFailureOnlyForConfiguredAttempts()
    {
        var injector = CreateInjector(new ControlledValidationProcessingFaultOptions
        {
            Enabled = true,
            Cases =
            [
                CreateCase(
                    runLabel: "p1-smoke",
                    faultCaseId: "N5_TRANSIENT_FAILURE",
                    faultKind: "transient_failure",
                    failAttempts: 1)
            ]
        });
        var envelope = CreateControlledEnvelope("p1-smoke", "N5_TRANSIENT_FAILURE", sequence: 1);

        var ex = await Assert.ThrowsAsync<ControlledValidationProcessingFaultException>(() =>
            injector.InjectAsync(envelope, CreateLease(attemptNumber: 1), CancellationToken.None).AsTask());

        Assert.Equal(ProcessingFailureKind.Transient, ex.Kind);
        Assert.Equal("transient_failure", ex.ErrorCode);

        await injector.InjectAsync(envelope, CreateLease(attemptNumber: 2), CancellationToken.None);
    }

    [Fact]
    public async Task InjectAsync_ThrowsPermanentFailure_ForConfiguredCase()
    {
        var injector = CreateInjector(new ControlledValidationProcessingFaultOptions
        {
            Enabled = true,
            Cases =
            [
                CreateCase(
                    runLabel: "p1-smoke",
                    faultCaseId: "N6_PERMANENT_FAILURE",
                    faultKind: "permanent_failure")
            ]
        });
        var envelope = CreateControlledEnvelope("p1-smoke", "N6_PERMANENT_FAILURE", sequence: 2);

        var ex = await Assert.ThrowsAsync<ControlledValidationProcessingFaultException>(() =>
            injector.InjectAsync(envelope, CreateLease(attemptNumber: 1), CancellationToken.None).AsTask());

        Assert.Equal(ProcessingFailureKind.Permanent, ex.Kind);
        Assert.Equal("permanent_failure", ex.ErrorCode);
    }

    [Fact]
    public async Task InjectAsync_IgnoresNonAllowlistedCorrelation()
    {
        var injector = CreateInjector(new ControlledValidationProcessingFaultOptions
        {
            Enabled = true,
            Cases =
            [
                CreateCase(
                    runLabel: "p1-smoke",
                    faultCaseId: "N5_TRANSIENT_FAILURE",
                    faultKind: "transient_failure")
            ]
        });
        var envelope = CreateControlledEnvelope("other-run", "N5_TRANSIENT_FAILURE", sequence: 1);

        await injector.InjectAsync(envelope, CreateLease(attemptNumber: 1), CancellationToken.None);
    }

    [Fact]
    public async Task InjectAsync_AllowsExactEventIdAllowlist_WhenCorrelationDoesNotUseCvConvention()
    {
        var envelope = EnvelopeFactory.Create() with
        {
            CorrelationId = "manual-correlation"
        };
        var injector = CreateInjector(new ControlledValidationProcessingFaultOptions
        {
            Enabled = true,
            Cases =
            [
                new ProcessingFaultCaseOptions
                {
                    RunLabel = "p1-smoke",
                    FaultCaseId = "N5_TRANSIENT_FAILURE",
                    FaultKind = "transient_failure",
                    EventId = envelope.EventId,
                    FailAttempts = 1
                }
            ]
        });

        var ex = await Assert.ThrowsAsync<ControlledValidationProcessingFaultException>(() =>
            injector.InjectAsync(envelope, CreateLease(attemptNumber: 1), CancellationToken.None).AsTask());

        Assert.Equal("transient_failure", ex.ErrorCode);
    }

    private static ControlledValidationProcessingFaultInjector CreateInjector(
        ControlledValidationProcessingFaultOptions options,
        string environmentName = "Evidence")
    {
        return new ControlledValidationProcessingFaultInjector(
            Options.Create(options),
            new TestHostEnvironment(environmentName),
            NullLogger<ControlledValidationProcessingFaultInjector>.Instance);
    }

    private static ProcessingFaultCaseOptions CreateCase(
        string runLabel,
        string faultCaseId,
        string faultKind,
        int failAttempts = 1)
    {
        return new ProcessingFaultCaseOptions
        {
            RunLabel = runLabel,
            FaultCaseId = faultCaseId,
            FaultKind = faultKind,
            FailAttempts = failAttempts
        };
    }

    private static NatureProtector.Shared.Messaging.EventEnvelope<NatureProtector.Shared.Contracts.Readings.SensorReadingProducedPayload> CreateControlledEnvelope(
        string runLabel,
        string faultCaseId,
        int sequence)
    {
        return EnvelopeFactory.Create() with
        {
            CorrelationId = $"cv:{runLabel}:{faultCaseId}:{sequence:000}"
        };
    }

    private static InboxProcessingLease CreateLease(int attemptNumber)
    {
        return new InboxProcessingLease(
            Guid.NewGuid(),
            Guid.NewGuid(),
            attemptNumber,
            "reading_risk_pipeline");
    }

    private sealed class TestHostEnvironment(string environmentName) : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = environmentName;

        public string ApplicationName { get; set; } = "NatureProtector.Prevention.Host.Tests";

        public string ContentRootPath { get; set; } = AppContext.BaseDirectory;

        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }
}
