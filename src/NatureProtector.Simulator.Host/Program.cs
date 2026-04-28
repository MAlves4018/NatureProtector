using Microsoft.Extensions.Options;
using NatureProtector.Infrastructure.Postgres.DependencyInjection;
using NatureProtector.Shared.Configuration;
using NatureProtector.Shared.Observability;
using NatureProtector.Simulator.Host.Configuration;
using NatureProtector.Simulator.Host.Publishing;
using NatureProtector.Simulator.Host.Services;

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

builder.Services.AddSingleton<IValidateOptions<SimulatorOptions>, SimulatorOptionsValidator>();
builder.Services.AddOptions<SimulatorOptions>()
    .Bind(builder.Configuration.GetSection(SimulatorOptions.SectionName))
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

builder.Services.AddHostedService<SimulationRunner>();

var host = builder.Build();
host.Run();
