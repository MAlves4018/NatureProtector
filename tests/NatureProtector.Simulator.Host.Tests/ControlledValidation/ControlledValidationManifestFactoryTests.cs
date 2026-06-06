using Microsoft.Extensions.Options;
using NatureProtector.Core.Sensors;
using NatureProtector.Simulator.Host.Configuration;
using NatureProtector.Simulator.Host.ControlledValidation;

namespace NatureProtector.Simulator.Host.Tests.ControlledValidation;

public sealed class ControlledValidationManifestFactoryTests
{
    [Fact]
    public void Create_Throws_WhenControlledValidationIsDisabled()
    {
        var factory = new ControlledValidationManifestFactory(
            Options.Create(new ControlledValidationOptions { Enabled = false }),
            Options.Create(CreateSimulatorOptions()));

        var ex = Assert.Throws<InvalidOperationException>(factory.Create);

        Assert.Equal("ControlledValidation:Enabled must be true to create a controlled validation manifest.", ex.Message);
    }

    [Fact]
    public void Create_Throws_WhenRunLabelIsMissing()
    {
        var factory = new ControlledValidationManifestFactory(
            Options.Create(new ControlledValidationOptions { Enabled = true }),
            Options.Create(CreateSimulatorOptions()));

        var ex = Assert.Throws<InvalidOperationException>(factory.Create);

        Assert.Equal("ControlledValidation:RunLabel is required when controlled validation is enabled.", ex.Message);
    }

    [Fact]
    public void Create_UsesConfiguredValuesAndSimulatorSensorFallback()
    {
        var runId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
        var simulationRunId = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");
        var areaId = Guid.Parse("cccccccc-cccc-cccc-cccc-cccccccccccc");
        var eventTime = new DateTimeOffset(2026, 6, 4, 12, 0, 0, TimeSpan.Zero);
        var factory = new ControlledValidationManifestFactory(
            Options.Create(new ControlledValidationOptions
            {
                Enabled = true,
                ControlledValidationRunId = runId,
                RunLabel = "p0-smoke",
                ScenarioCode = "scenario_b",
                AreaId = areaId,
                SimulationRunId = simulationRunId,
                EventTime = eventTime
            }),
            Options.Create(CreateSimulatorOptions()));

        var manifest = factory.Create();

        Assert.Equal(runId, manifest.ControlledValidationRunId);
        Assert.Equal("p0-smoke", manifest.RunLabel);
        Assert.Equal("scenario_b", manifest.ScenarioCode);
        Assert.Equal(areaId, manifest.AreaId);
        Assert.Equal(simulationRunId, manifest.SimulationRunId);
        Assert.Equal(eventTime, manifest.EventTime);
        Assert.Equal(Guid.Parse("dddddddd-dddd-dddd-dddd-dddddddddddd"), manifest.NominalSensorId);
        Assert.Equal("sensor-p0-001", manifest.NominalSensorName);
        Assert.NotEqual(Guid.Empty, manifest.SensorNotFoundId);
        Assert.NotEqual(manifest.NominalSensorId, manifest.SensorNotFoundId);
    }

    [Fact]
    public void Create_UsesP1FaultCases_WhenPhaseIsP1()
    {
        var factory = new ControlledValidationManifestFactory(
            Options.Create(new ControlledValidationOptions
            {
                Enabled = true,
                Phase = ControlledValidationPhases.P1,
                RunLabel = "p1-smoke"
            }),
            Options.Create(CreateSimulatorOptions()));

        var manifest = factory.Create();

        Assert.Equal(ControlledValidationPhases.P1, manifest.Phase);
        Assert.Contains(manifest.FaultCases, faultCase =>
            faultCase.FaultCaseId == ControlledValidationFaultCaseIds.N5TransientFailure &&
            faultCase.ExpectedReasonCode == "transient_failure");
        Assert.Contains(manifest.FaultCases, faultCase =>
            faultCase.FaultCaseId == ControlledValidationFaultCaseIds.N6PermanentFailure &&
            faultCase.ExpectedReasonCode == "permanent_failure");
        Assert.DoesNotContain(manifest.FaultCases, faultCase =>
            faultCase.FaultCaseId == ControlledValidationFaultCaseIds.N1InvalidJson);
    }

    [Fact]
    public void Create_UsesP2FaultCases_WhenPhaseIsP2()
    {
        var factory = new ControlledValidationManifestFactory(
            Options.Create(new ControlledValidationOptions
            {
                Enabled = true,
                Phase = ControlledValidationPhases.P2,
                RunLabel = "p2-smoke"
            }),
            Options.Create(CreateSimulatorOptions()));

        var manifest = factory.Create();

        Assert.Equal(ControlledValidationPhases.P2, manifest.Phase);
        Assert.Contains(manifest.FaultCases, faultCase =>
            faultCase.FaultCaseId == ControlledValidationFaultCaseIds.P2MissingReadings &&
            faultCase.ExpectedOutcome == ControlledValidationExpectedOutcome.CoverageGap);
        Assert.Contains(manifest.FaultCases, faultCase =>
            faultCase.FaultCaseId == ControlledValidationFaultCaseIds.P2DuplicatePayloadIdentical &&
            faultCase.ExpectedOutcome == ControlledValidationExpectedOutcome.IdempotentDuplicate);
        Assert.DoesNotContain(manifest.FaultCases, faultCase =>
            faultCase.FaultCaseId == ControlledValidationFaultCaseIds.N5TransientFailure);
    }

    [Fact]
    public void Create_UsesP2FaultCases_WhenPhaseIsP2Extended()
    {
        var factory = new ControlledValidationManifestFactory(
            Options.Create(new ControlledValidationOptions
            {
                Enabled = true,
                Phase = ControlledValidationPhases.P2Extended,
                RunLabel = "p2-extended-smoke"
            }),
            Options.Create(CreateSimulatorOptions()));

        var manifest = factory.Create();

        Assert.Equal(ControlledValidationPhases.P2Extended, manifest.Phase);
        Assert.Contains(manifest.FaultCases, faultCase =>
            faultCase.FaultCaseId == ControlledValidationFaultCaseIds.P2ValueOutlier);
        Assert.Contains(manifest.FaultCases, faultCase =>
            faultCase.FaultCaseId == ControlledValidationFaultCaseIds.P2BlockedRangeEligibility);
        Assert.Contains(manifest.FaultCases, faultCase =>
            faultCase.FaultCaseId == ControlledValidationFaultCaseIds.P2TemporalDelayed);
        Assert.DoesNotContain(manifest.FaultCases, faultCase =>
            faultCase.FaultCaseId == ControlledValidationFaultCaseIds.N1InvalidJson);
    }

    [Fact]
    public void Create_UsesP3NegativePipelineFaultCases_WhenPhaseIsP3NegativePipeline()
    {
        var factory = new ControlledValidationManifestFactory(
            Options.Create(new ControlledValidationOptions
            {
                Enabled = true,
                Phase = ControlledValidationPhases.P3NegativePipeline,
                RunLabel = "p3-smoke"
            }),
            Options.Create(CreateSimulatorOptions()));

        var manifest = factory.Create();

        Assert.Equal(ControlledValidationPhases.P3NegativePipeline, manifest.Phase);
        Assert.Contains(manifest.FaultCases, faultCase =>
            faultCase.FaultCaseId == ControlledValidationFaultCaseIds.P3RejectInvalidJson &&
            faultCase.ExpectedReasonCode == "invalid_json");
        Assert.Contains(manifest.FaultCases, faultCase =>
            faultCase.FaultCaseId == ControlledValidationFaultCaseIds.P3QuarantineSensorNotFound &&
            faultCase.ExpectedReasonCode == "sensor_not_found");
        Assert.Contains(manifest.FaultCases, faultCase =>
            faultCase.FaultCaseId == ControlledValidationFaultCaseIds.P3RetryExhaustedToQuarantine &&
            faultCase.ExpectedReasonCode == "retries_exhausted");
        Assert.DoesNotContain(manifest.FaultCases, faultCase =>
            faultCase.FaultCaseId == ControlledValidationFaultCaseIds.P2MissingReadings);
        Assert.DoesNotContain(manifest.FaultCases, faultCase =>
            faultCase.FaultCaseId == ControlledValidationFaultCaseIds.N5TransientFailure);
    }

    [Fact]
    public void Create_Throws_WhenNoNominalSensorCanBeResolved()
    {
        var simulatorOptions = CreateSimulatorOptions();
        simulatorOptions.Sensors.Clear();
        var factory = new ControlledValidationManifestFactory(
            Options.Create(new ControlledValidationOptions
            {
                Enabled = true,
                RunLabel = "p0-smoke"
            }),
            Options.Create(simulatorOptions));

        var ex = Assert.Throws<InvalidOperationException>(factory.Create);

        Assert.Equal(
            "ControlledValidation:NominalSensorId is required when no simulator sensor id is configured.",
            ex.Message);
    }

    internal static SimulatorOptions CreateSimulatorOptions()
    {
        return new SimulatorOptions
        {
            AreaId = Guid.Parse("11111111-1111-1111-1111-111111111111"),
            ControlPlaneScenarioCode = "scenario_b",
            Sensors =
            [
                new SensorDefinitionOptions
                {
                    Id = Guid.Parse("dddddddd-dddd-dddd-dddd-dddddddddddd"),
                    Name = "sensor-p0-001",
                    Type = SensorType.Temperature,
                    Latitude = 39.8,
                    Longitude = -7.9
                }
            ]
        };
    }
}
