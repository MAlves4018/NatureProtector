using Microsoft.Extensions.Options;
using NatureProtector.Core.Sensors;

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

public sealed class SimulatorOptionsValidator : IValidateOptions<SimulatorOptions>
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

            if (string.IsNullOrWhiteSpace(options.ControlPlaneScenarioCode))
            {
                failures.Add("Simulator:ControlPlaneScenarioCode is required when ControlPlaneEnabled=true.");
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

        return failures.Count == 0
            ? ValidateOptionsResult.Success
            : ValidateOptionsResult.Fail(failures);
    }
}
