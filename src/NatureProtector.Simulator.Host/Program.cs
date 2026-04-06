using NatureProtector.Shared.Configuration;
using NatureProtector.Simulator.Host.Configuration;
using NatureProtector.Simulator.Host.Publishing;
using NatureProtector.Simulator.Host.Services;

var builder = Host.CreateApplicationBuilder(args);

builder.Services.Configure<RabbitMqOptions>(
    builder.Configuration.GetSection(RabbitMqOptions.SectionName));

builder.Services.Configure<SimulatorOptions>(
    builder.Configuration.GetSection(SimulatorOptions.SectionName));

builder.Services.PostConfigure<SimulatorOptions>(
    options => GeneratedScenarioManifestLoader.ApplyIfConfigured(
        options,
        builder.Environment.ContentRootPath));

builder.Services.AddSingleton<SeedProvider>();
builder.Services.AddSingleton<ScenarioContextFactory>();
builder.Services.AddSingleton<ReadingGenerationService>();

builder.Services.AddSingleton<IReadingPublisher, ConsoleReadingPublisher>();
builder.Services.AddSingleton<IReadingPublisher, RabbitMqReadingPublisher>();

builder.Services.AddHostedService<SimulationRunner>();

var host = builder.Build();
host.Run();
