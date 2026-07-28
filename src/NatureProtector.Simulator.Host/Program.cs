using Microsoft.Extensions.Options;
using NatureProtector.Infrastructure.Postgres.DependencyInjection;
using NatureProtector.Shared.Configuration;
using NatureProtector.Shared.Observability;
using NatureProtector.Simulator.Host.Configuration;
using NatureProtector.Simulator.Host.ControlledValidation;
using NatureProtector.Simulator.Host.Publishing;
using NatureProtector.Simulator.Host.Services;
using NatureProtector.Simulator.Host.TemporalLoad;

/*
 * Este ponto de entrada compõe o runtime do simulador.
 *
 * Rationale:
 * - O simulador precisa de orquestrar geração determinística, resolução de
 *   contexto e publicação sem misturar essas responsabilidades.
 * - A composição por DI permite alternar entre modo standalone e modo apoiado
 *   pelo control plane sem alterar a lógica de execução.
 *
 * Design considerations:
 * - A validação das opções corre no arranque para falhar cedo quando a
 *   configuração está incompleta.
 * - O ficheiro de definição gerado só é aplicado no modo standalone.
 * - A persistência de runs e a origem do contexto mudam conforme o modo de
 *   execução configurado.
 */

var builder = Host.CreateApplicationBuilder(args);
builder.Logging.AddNatureProtectorActivityTracking();
builder.Services.AddNatureProtectorOpenTelemetry(
    builder.Configuration,
    builder.Environment,
    SimulatorHostTelemetry.ServiceName,
    [SimulatorHostTelemetry.ServiceName],
    [SimulatorHostTelemetry.ServiceName]);

builder.Services.Configure<RabbitMqOptions>(
    builder.Configuration.GetSection(RabbitMqOptions.SectionName));
builder.Services.Configure<ControlledValidationOptions>(
    builder.Configuration.GetSection(ControlledValidationOptions.SectionName));
builder.Services.AddSingleton<IValidateOptions<SimulatorOptions>, SimulatorOptionsValidator>();
builder.Services.AddSingleton<IValidateOptions<TemporalLoadOptions>, SimulatorOptionsValidator>();
builder.Services.AddOptions<SimulatorOptions>()
    .Bind(builder.Configuration.GetSection(SimulatorOptions.SectionName))
    .ValidateOnStart();
builder.Services.AddOptions<TemporalLoadOptions>()
    .Bind(builder.Configuration.GetSection(TemporalLoadOptions.SectionName))
    .ValidateOnStart();

builder.Services.PostConfigure<SimulatorOptions>(
    options => GeneratedScenarioManifestLoader.ApplyIfConfigured(
        options,
        builder.Environment.ContentRootPath));

builder.Services.AddNatureProtectorControlPlanePostgres(builder.Environment.ContentRootPath);
builder.Services.AddSingleton<SeedProvider>();
builder.Services.AddSingleton<ScenarioContextFactory>();
builder.Services.AddSingleton<PostgresSimulationContextSource>();
builder.Services.AddSingleton<NoOpSimulationRunStore>();
builder.Services.AddSingleton<PostgresSimulationRunStore>();
builder.Services.AddSingleton<ReadingGenerationService>();
builder.Services.AddSingleton<ISimulatorProcessExitCode, EnvironmentSimulatorProcessExitCode>();
builder.Services.AddSingleton<ISimulationContextSource>(
    services =>
    {
        var options = services.GetRequiredService<IOptions<SimulatorOptions>>().Value;

        // O modo control plane usa PostgreSQL como fonte de contexto. No modo
        // standalone o contexto é montado diretamente a partir da configuração.
        return options.ControlPlaneEnabled
            ? services.GetRequiredService<PostgresSimulationContextSource>()
            : services.GetRequiredService<ScenarioContextFactory>();
    });
builder.Services.AddSingleton<ISimulationRunStore>(
    services =>
    {
        var options = services.GetRequiredService<IOptions<SimulatorOptions>>().Value;

        // Só faz sentido persistir runs quando existe uma configuração
        // materializada no control plane.
        return options.ControlPlaneEnabled
            ? services.GetRequiredService<PostgresSimulationRunStore>()
            : services.GetRequiredService<NoOpSimulationRunStore>();
    });

builder.Services.AddSingleton<IReadingPublisher, ConsoleReadingPublisher>();
builder.Services.AddSingleton<IReadingPublisher, RabbitMqReadingPublisher>();
builder.Services.AddSingleton<ControlledValidationManifestFactory>();
builder.Services.AddSingleton<ControlledValidationEvidenceWriter>();
builder.Services.AddSingleton<IControlledValidationMessagePublisher, RabbitMqControlledValidationMessagePublisher>();
builder.Services.AddSingleton<ControlledValidationOrchestrator>();

var controlledValidationEnabled = builder.Configuration
    .GetSection(ControlledValidationOptions.SectionName)
    .GetValue<bool>(nameof(ControlledValidationOptions.Enabled));
var temporalLoadEnabled = builder.Configuration
    .GetSection(TemporalLoadOptions.SectionName)
    .GetValue<bool>(nameof(TemporalLoadOptions.Enabled));

if (temporalLoadEnabled)
{
    builder.Services.AddHostedService<TemporalLoadRunner>();
}
else if (controlledValidationEnabled)
{
    builder.Services.AddHostedService<ControlledValidationRunner>();
}
else
{
    builder.Services.AddHostedService<SimulationRunner>();
}

var host = builder.Build();
host.Run();
