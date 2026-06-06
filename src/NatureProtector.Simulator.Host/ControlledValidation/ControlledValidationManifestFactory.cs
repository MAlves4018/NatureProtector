using Microsoft.Extensions.Options;
using NatureProtector.Simulator.Host.Configuration;

namespace NatureProtector.Simulator.Host.ControlledValidation;

public sealed class ControlledValidationManifestFactory(
    IOptions<ControlledValidationOptions> controlledValidationOptions,
    IOptions<SimulatorOptions> simulatorOptions)
{
    public ControlledValidationScenarioManifest Create()
    {
        var options = controlledValidationOptions.Value;

        if (!options.Enabled)
        {
            throw new InvalidOperationException("ControlledValidation:Enabled must be true to create a controlled validation manifest.");
        }

        if (string.IsNullOrWhiteSpace(options.RunLabel))
        {
            throw new InvalidOperationException("ControlledValidation:RunLabel is required when controlled validation is enabled.");
        }

        var simulator = simulatorOptions.Value;
        var runId = options.ControlledValidationRunId != Guid.Empty
            ? options.ControlledValidationRunId
            : ControlledValidationIdentity.CreateDeterministicGuid($"controlled-validation-run:{options.RunLabel}");
        var scenarioCode = !string.IsNullOrWhiteSpace(options.ScenarioCode)
            ? options.ScenarioCode
            : !string.IsNullOrWhiteSpace(simulator.ControlPlaneScenarioCode)
                ? simulator.ControlPlaneScenarioCode
                : $"controlled-validation-{NormalizePhase(options.Phase).ToLowerInvariant()}";
        var areaId = options.AreaId != Guid.Empty ? options.AreaId : simulator.AreaId;
        var simulationRunId = options.SimulationRunId != Guid.Empty ? options.SimulationRunId : runId;
        var nominalSensorId = ResolveNominalSensorId(options, simulator);
        var nominalSensorName = ResolveNominalSensorName(options, simulator);
        var sensorNotFoundId = options.SensorNotFoundId != Guid.Empty
            ? options.SensorNotFoundId
            : ControlledValidationIdentity.CreateDeterministicGuid($"{runId:N}:sensor-not-found");

        return new ControlledValidationScenarioManifest(
            controlledValidationRunId: runId,
            runLabel: options.RunLabel,
            scenarioCode: scenarioCode,
            areaId: areaId,
            simulationRunId: simulationRunId,
            eventTime: options.EventTime ?? DateTimeOffset.UtcNow,
            nominalSensorId: nominalSensorId,
            nominalSensorName: nominalSensorName,
            sensorNotFoundId: sensorNotFoundId,
            faultCases: CreateFaultCases(options.Phase),
            phase: NormalizePhase(options.Phase));
    }

    private static IReadOnlyList<ValidationFaultCase> CreateFaultCases(string? phase)
    {
        return NormalizePhase(phase) switch
        {
            ControlledValidationPhases.P3NegativePipeline => ControlledValidationScenarioManifest.CreateDefaultP3NegativePipelineFaultCases(),
            ControlledValidationPhases.P2Extended => ControlledValidationScenarioManifest.CreateDefaultP2FaultCases(),
            ControlledValidationPhases.P2 => ControlledValidationScenarioManifest.CreateDefaultP2FaultCases(),
            ControlledValidationPhases.P1 => ControlledValidationScenarioManifest.CreateDefaultP1FaultCases(),
            _ => ControlledValidationScenarioManifest.CreateDefaultP0FaultCases()
        };
    }

    private static string NormalizePhase(string? phase)
    {
        if (string.Equals(phase, ControlledValidationPhases.P3NegativePipeline, StringComparison.OrdinalIgnoreCase))
        {
            return ControlledValidationPhases.P3NegativePipeline;
        }

        if (string.Equals(phase, ControlledValidationPhases.P2Extended, StringComparison.OrdinalIgnoreCase))
        {
            return ControlledValidationPhases.P2Extended;
        }

        if (string.Equals(phase, ControlledValidationPhases.P2, StringComparison.OrdinalIgnoreCase))
        {
            return ControlledValidationPhases.P2;
        }

        return string.Equals(phase, ControlledValidationPhases.P1, StringComparison.OrdinalIgnoreCase)
            ? ControlledValidationPhases.P1
            : ControlledValidationPhases.P0;
    }

    private static Guid ResolveNominalSensorId(
        ControlledValidationOptions options,
        SimulatorOptions simulator)
    {
        if (options.NominalSensorId != Guid.Empty)
        {
            return options.NominalSensorId;
        }

        var configuredSensorId = simulator.Sensors.FirstOrDefault(sensor => sensor.Id.HasValue)?.Id;

        if (configuredSensorId is { } sensorId && sensorId != Guid.Empty)
        {
            return sensorId;
        }

        throw new InvalidOperationException(
            "ControlledValidation:NominalSensorId is required when no simulator sensor id is configured.");
    }

    private static string ResolveNominalSensorName(
        ControlledValidationOptions options,
        SimulatorOptions simulator)
    {
        if (!string.IsNullOrWhiteSpace(options.NominalSensorName))
        {
            return options.NominalSensorName;
        }

        var configuredSensorName = simulator.Sensors.FirstOrDefault(sensor => sensor.Id.HasValue)?.Name;

        return !string.IsNullOrWhiteSpace(configuredSensorName)
            ? configuredSensorName
            : "controlled-validation-nominal-sensor";
    }
}
