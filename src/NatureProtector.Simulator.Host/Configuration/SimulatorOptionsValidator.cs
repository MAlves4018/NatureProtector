using Microsoft.Extensions.Options;
using NatureProtector.Core.Sensors;
using NatureProtector.Simulator.Host.TemporalLoad;

namespace NatureProtector.Simulator.Host.Configuration;

/*
 * Este validador garante que o simulador arranca com uma configuração coerente
 * para o modo de execução selecionado.
 *
 * Rationale:
 * - O host suporta dois modos distintos, standalone e control plane, com
 *   pré-condições diferentes.
 * - Falhar cedo no arranque é preferível a descobrir erros só durante a
 *   execução da simulação.
 *
 * Design considerations:
 * - No modo control plane exigem-se identificadores lógicos da área e do
 *   cenário a resolver em PostgreSQL.
 * - No modo standalone exigem-se os dados mínimos para construir o contexto em
 *   memória.
 * - Os tipos de sensor aceites refletem apenas os contratos de leitura hoje
 *   suportados.
 */

public sealed class SimulatorOptionsValidator : IValidateOptions<SimulatorOptions>, IValidateOptions<TemporalLoadOptions>
{
    /// <summary>
    /// Valida as opções do simulador antes do arranque do host.
    /// </summary>
    public ValidateOptionsResult Validate(string? name, SimulatorOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        var failures = new List<string>();

        if (options.ControlPlaneEnabled)
        {
            if (string.IsNullOrWhiteSpace(options.ControlPlaneAreaCode))
            {
                failures.Add("Simulator:ControlPlaneAreaCode is required when ControlPlaneEnabled=true.");
            }

            var hasScenarioId = options.ScenarioId != Guid.Empty;
            var hasScenarioCode = !string.IsNullOrWhiteSpace(options.ControlPlaneScenarioCode);

            if (!hasScenarioId && !hasScenarioCode)
            {
                failures.Add(
                    "Simulator:ScenarioId or Simulator:ControlPlaneScenarioCode is required when ControlPlaneEnabled=true.");
            }

            if (hasScenarioId && hasScenarioCode)
            {
                failures.Add(
                    "Simulator:ScenarioId and Simulator:ControlPlaneScenarioCode cannot both be configured when ControlPlaneEnabled=true. " +
                    "Configure exactly one scenario selector to avoid ambiguous control-plane scenario selection.");
            }

            if (!string.IsNullOrWhiteSpace(options.ScenarioManifestPath))
            {
                failures.Add("Simulator:ScenarioManifestPath is not supported when ControlPlaneEnabled=true.");
            }
        }
        else
        {
            if (options.AreaId == Guid.Empty)
            {
                failures.Add("Simulator:AreaId is required when ControlPlaneEnabled=false.");
            }

            if (options.ScenarioId == Guid.Empty)
            {
                failures.Add("Simulator:ScenarioId is required when ControlPlaneEnabled=false.");
            }

            if (string.IsNullOrWhiteSpace(options.ScenarioName))
            {
                failures.Add("Simulator:ScenarioName is required when ControlPlaneEnabled=false.");
            }

            if (options.Sensors is null || options.Sensors.Count == 0)
            {
                failures.Add("Simulator:Sensors must define at least one sensor when ControlPlaneEnabled=false.");
            }
            else
            {
                foreach (var sensor in options.Sensors)
                {
                    if (sensor.Type is SensorType.Temperature or SensorType.Humidity or SensorType.Wind)
                    {
                        continue;
                    }

                    failures.Add(
                        $"Simulator:Sensors contains unsupported standalone sensor type '{sensor.Type}'. " +
                        "Use Temperature, Humidity or Wind.");
                }
            }
        }

        if (options.RunOverrides.SensorCount.HasValue && options.RunOverrides.SensorCount.Value <= 0)
        {
            failures.Add("Simulator:RunOverrides:SensorCount must be greater than zero when provided.");
        }

        if (options.RunOverrides.NumberOfCycles.HasValue && options.RunOverrides.NumberOfCycles.Value <= 0)
        {
            failures.Add("Simulator:RunOverrides:NumberOfCycles must be greater than zero when provided.");
        }

        if (options.RunOverrides.IntervalSeconds.HasValue && options.RunOverrides.IntervalSeconds.Value <= 0)
        {
            failures.Add("Simulator:RunOverrides:IntervalSeconds must be greater than zero when provided.");
        }

        if (options.LagDelaySeconds is < 0 or > 3600)
        {
            failures.Add("Simulator:LagDelaySeconds must be between 0 and 3600.");
        }

        return failures.Count == 0
            ? ValidateOptionsResult.Success
            : ValidateOptionsResult.Fail(failures);
    }

    public ValidateOptionsResult Validate(string? name, TemporalLoadOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        if (!options.Enabled)
        {
            return ValidateOptionsResult.Success;
        }

        var failures = new List<string>();
        if (string.IsNullOrWhiteSpace(options.WorkloadPath))
        {
            failures.Add("TemporalLoad:WorkloadPath is required when TemporalLoad:Enabled=true.");
        }

        if (string.IsNullOrWhiteSpace(options.WorkloadId))
        {
            failures.Add("TemporalLoad:WorkloadId is required when TemporalLoad:Enabled=true.");
        }

        if (options.Repetition <= 0)
        {
            failures.Add("TemporalLoad:Repetition must be greater than zero.");
        }

        if (options.MaxCatchUpBurst <= 0)
        {
            failures.Add("TemporalLoad:MaxCatchUpBurst must be greater than zero.");
        }

        if (options.MaxNominalGenerationAttempts <= 0)
        {
            failures.Add("TemporalLoad:MaxNominalGenerationAttempts must be greater than zero.");
        }

        if (options.PublisherTimeoutSeconds <= 0)
        {
            failures.Add("TemporalLoad:PublisherTimeoutSeconds must be greater than zero.");
        }

        return failures.Count == 0
            ? ValidateOptionsResult.Success
            : ValidateOptionsResult.Fail(failures);
    }
}
