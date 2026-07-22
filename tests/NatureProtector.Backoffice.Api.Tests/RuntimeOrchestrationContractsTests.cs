using NatureProtector.Backoffice.Api.RuntimeOrchestration;

namespace NatureProtector.Backoffice.Api.Tests;

public sealed class RuntimeOrchestrationContractsTests
{
    [Fact]
    public void RuntimeLaunchRequest_PreservesSimulationAndControlledValidationPayloads()
    {
        var executionId = new RuntimeExecutionId(Guid.Parse("11111111-1111-1111-1111-111111111111"));
        var evidence = new RuntimeEvidenceReference("evidence-1", "runtime/evidence-1");
        var simulation = new RuntimeSimulationParameters(
            "PT-11",
            "scenario_b",
            10,
            3,
            1,
            42,
            "legacy",
            ["missing", "duplicate"],
            "corr-123");
        var controlledValidation = new RuntimeControlledValidationParameters(
            "P3NegativePipeline",
            Guid.Parse("22222222-2222-2222-2222-222222222222"),
            "run-label",
            "scenario_c",
            Guid.Parse("33333333-3333-3333-3333-333333333333"),
            Guid.Parse("44444444-4444-4444-4444-444444444444"),
            Guid.Parse("55555555-5555-5555-5555-555555555555"),
            "Nominal Sensor",
            Guid.Parse("66666666-6666-6666-6666-666666666666"),
            new DateTimeOffset(2026, 7, 21, 12, 0, 0, TimeSpan.Zero),
            "evidence/p3");

        var request = new RuntimeLaunchRequest(
            executionId,
            Guid.Parse("77777777-7777-7777-7777-777777777777"),
            "idem-1",
            "Development",
            RuntimeLaunchProfile.ControlledValidationP3,
            simulation,
            controlledValidation,
            CollectEvidence: true,
            WaitForCompletion: false,
            TimeSpan.FromSeconds(30),
            evidence);

        Assert.Equal(RuntimeLaunchProfile.ControlledValidationP3, request.Profile);
        Assert.Equal(["missing", "duplicate"], request.Simulation.DegradationProfiles);
        Assert.Equal("corr-123", request.Simulation.OrchestratorCorrelationId);
        Assert.Equal("P3NegativePipeline", request.ControlledValidation?.Phase);
        Assert.Equal(Guid.Parse("22222222-2222-2222-2222-222222222222"), request.ControlledValidation?.ControlledValidationRunId);
        Assert.Equal("run-label", request.ControlledValidation?.RunLabel);
        Assert.Equal("scenario_c", request.ControlledValidation?.ScenarioCode);
        Assert.Equal(Guid.Parse("33333333-3333-3333-3333-333333333333"), request.ControlledValidation?.AreaId);
        Assert.Equal(Guid.Parse("44444444-4444-4444-4444-444444444444"), request.ControlledValidation?.SimulationRunId);
        Assert.Equal(Guid.Parse("55555555-5555-5555-5555-555555555555"), request.ControlledValidation?.NominalSensorId);
        Assert.Equal("Nominal Sensor", request.ControlledValidation?.NominalSensorName);
        Assert.Equal(Guid.Parse("66666666-6666-6666-6666-666666666666"), request.ControlledValidation?.SensorNotFoundId);
        Assert.Equal(new DateTimeOffset(2026, 7, 21, 12, 0, 0, TimeSpan.Zero), request.ControlledValidation?.EventTime);
        Assert.Equal("evidence/p3", request.ControlledValidation?.EvidenceOutputReference);
        Assert.Same(evidence, request.Evidence);
    }

    [Fact]
    public void RuntimeReceiptsAndSnapshots_PreserveTerminalProviderState()
    {
        var executionId = new RuntimeExecutionId(Guid.Parse("88888888-8888-8888-8888-888888888888"));
        var evidence = new RuntimeEvidenceReference("evidence-2", "runtime/evidence-2");
        var acceptedAt = new DateTimeOffset(2026, 7, 21, 12, 0, 0, TimeSpan.Zero);
        var startedAt = acceptedAt.AddSeconds(1);
        var finishedAt = acceptedAt.AddSeconds(5);

        var launch = new RuntimeLaunchReceipt(
            executionId,
            RuntimeExecutionState.Accepted,
            acceptedAt,
            "provider/run/1",
            "log-corr",
            ReusedExistingExecution: true,
            RejectionCode: null,
            Message: "accepted",
            evidence);
        var snapshot = new RuntimeExecutionSnapshot(
            executionId,
            RuntimeExecutionState.Failed,
            finishedAt,
            startedAt,
            finishedAt,
            17,
            "PROCESS_EXITED",
            "Process returned 17.",
            "log-corr",
            evidence);
        var stop = new RuntimeStopReceipt(
            executionId,
            RuntimeExecutionState.Cancelled,
            StopAccepted: false,
            "Execution already finished.");

        Assert.True(launch.ReusedExistingExecution);
        Assert.Equal("provider/run/1", launch.ProviderReference);
        Assert.Equal(17, snapshot.ExitCode);
        Assert.Equal("PROCESS_EXITED", snapshot.FailureCode);
        Assert.Equal(finishedAt, snapshot.FinishedAtUtc);
        Assert.False(stop.StopAccepted);
        Assert.Equal(RuntimeExecutionState.Cancelled, stop.State);
    }
}
