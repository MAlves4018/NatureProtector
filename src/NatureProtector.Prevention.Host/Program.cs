using NatureProtector.Infrastructure.Influx.DependencyInjection;
using NatureProtector.Prevention.Host;
using NatureProtector.Prevention.Host.Persistence;
using NatureProtector.Prevention.Host.Processing;
using NatureProtector.Prevention.Persistence;
using NatureProtector.Prevention.Risk;
using NatureProtector.Shared.Configuration;

var builder = Host.CreateApplicationBuilder(args);

builder.Services.Configure<RabbitMqOptions>(
    builder.Configuration.GetSection(RabbitMqOptions.SectionName));

builder.Services.AddInfluxPersistence(builder.Configuration);

builder.Services.AddSingleton<IAcceptedReadingRepository, InMemoryAcceptedReadingRepository>();
builder.Services.AddSingleton<ISimpleRiskScoringService, SimpleRiskScoringService>();
builder.Services.AddSingleton<IRiskAssessmentRepository, InMemoryRiskAssessmentRepository>();
builder.Services.AddSingleton<IAreaRiskSnapshotService, AreaRiskSnapshotService>();
builder.Services.AddSingleton<IAreaRiskSnapshotRepository, InMemoryAreaRiskSnapshotRepository>();

builder.Services.AddSingleton<ReadingRiskPipeline>();

builder.Services.AddHostedService<PreventionWorker>();

var host = builder.Build();
host.Run();