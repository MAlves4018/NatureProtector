using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using NatureProtector.Infrastructure.Influx.DependencyInjection;
using NatureProtector.Infrastructure.Postgres.DependencyInjection;
using NatureProtector.Prevention.Host;
using NatureProtector.Prevention.Host.Configuration;
using NatureProtector.Prevention.Host.Health;
using NatureProtector.Prevention.Host.Persistence;
using NatureProtector.Prevention.Host.Processing;
using NatureProtector.Prevention.Host.Projection;
using NatureProtector.Prevention.Host.Runtime;
using NatureProtector.Prevention.Persistence;
using NatureProtector.Prevention.Risk;
using NatureProtector.Shared.Configuration;
using NatureProtector.Shared.Observability;

/*
 * Este ponto de entrada compõe o runtime do fluxo operacional de prevenção.
 *
 * Rationale:
 * - O fluxo operacional precisa de ligar consumo de eventos, classificação de falhas,
 *   persistência e projeções sem acoplar essas decisões à lógica de negócio.
 * - A composição permite alternar entre modo em memória e modo persistente em
 *   PostgreSQL conforme a fase do runtime.
 *
 * Design considerations:
 * - As opções são validadas no arranque para falhar cedo.
 * - O suporte a inbox persistente e projeções PostgreSQL é ativado apenas
 *   quando a persistência do fluxo operacional está ligada.
 * - A escrita em Influx é registada independentemente do modo de inbox.
 */

var builder = WebApplication.CreateBuilder(args);
builder.Logging.AddNatureProtectorActivityTracking();
builder.Services.AddNatureProtectorOpenTelemetry(
    builder.Configuration,
    builder.Environment,
    PreventionHostTelemetry.ServiceName,
    [PreventionHostTelemetry.ServiceName],
    [PreventionHostTelemetry.ServiceName]);

builder.Services.AddSingleton<IValidateOptions<PreventionHostOptions>, PreventionHostOptionsValidator>();
builder.Services.AddOptions<PreventionHostOptions>()
    .Bind(builder.Configuration.GetSection(PreventionHostOptions.SectionName))
    .ValidateOnStart();
builder.Services.AddOptions<ControlledValidationProcessingFaultOptions>()
    .Bind(builder.Configuration.GetSection(ControlledValidationProcessingFaultOptions.SectionName))
    .ValidateOnStart();
builder.Services.Configure<RabbitMqOptions>(
    builder.Configuration.GetSection(RabbitMqOptions.SectionName));

builder.Services.AddInfluxPersistence(builder.Configuration, builder.Environment.ContentRootPath);
builder.Services.AddSingleton<PreventionRuntimeState>();
var healthChecks = builder.Services.AddHealthChecks()
    .AddCheck<PreventionReadinessHealthCheck>(
        "prevention-ready",
        failureStatus: HealthStatus.Unhealthy,
        tags: ["ready"]);

var preventionHostOptions = builder.Configuration
    .GetSection(PreventionHostOptions.SectionName)
    .Get<PreventionHostOptions>() ?? new PreventionHostOptions();

if (preventionHostOptions.PipelinePersistenceEnabled)
{
    // Neste modo o fluxo operacional usa inbox durável, projeções persistidas
    // e um worker de novas tentativas apoiado por PostgreSQL.
    builder.Services.AddNatureProtectorControlPlanePostgres(builder.Environment.ContentRootPath);
    healthChecks.AddCheck<PreventionDatabaseHealthCheck>(
        "prevention-postgres",
        failureStatus: HealthStatus.Unhealthy,
        tags: ["ready"],
        timeout: TimeSpan.FromSeconds(5));
    builder.Services.AddSingleton<IReadingSemanticValidator, ReadingSemanticValidator>();
    builder.Services.AddSingleton<IReadingEventInbox, PostgresReadingEventInbox>();
    builder.Services.AddSingleton<IAreaOperationalProjectionStore, PostgresAreaOperationalProjectionStore>();
    builder.Services.AddSingleton<PostgresCycleProjectionCoordinator>();
    builder.Services.AddSingleton<ICycleProjectionCoordinator>(services =>
        services.GetRequiredService<PostgresCycleProjectionCoordinator>());
    builder.Services.AddHostedService<CycleSettlementWorker>();
    builder.Services.AddSingleton<IAcceptedReadingRepository, PostgresAcceptedReadingRepository>();
    builder.Services.AddSingleton<IDailyCellStateRepository, PostgresDailyCellStateRepository>();
    builder.Services.AddSingleton<IRiskAssessmentRepository, PostgresRiskAssessmentRepository>();
    builder.Services.AddSingleton<IAreaRiskSnapshotRepository, PostgresAreaRiskSnapshotRepository>();
    builder.Services.AddHostedService<InboxRetryWorker>();
}
else
{
    // O modo em memória mantém a demonstração funcional sem dependência do
    // control plane persistente.
    builder.Services.AddSingleton<IReadingSemanticValidator, PassThroughReadingSemanticValidator>();
    builder.Services.AddSingleton<IReadingEventInbox, InMemoryReadingEventInbox>();
    builder.Services.AddSingleton<IAreaOperationalProjectionStore, InMemoryAreaOperationalProjectionStore>();
    builder.Services.AddSingleton<IAcceptedReadingRepository, InMemoryAcceptedReadingRepository>();
    builder.Services.AddSingleton<IDailyCellStateRepository, InMemoryDailyCellStateRepository>();
    builder.Services.AddSingleton<IRiskAssessmentRepository, InMemoryRiskAssessmentRepository>();
    builder.Services.AddSingleton<IAreaRiskSnapshotRepository, InMemoryAreaRiskSnapshotRepository>();
}

builder.Services.AddSingleton<IProcessingFailureClassifier, DefaultProcessingFailureClassifier>();
var processingFaultOptions = builder.Configuration
    .GetSection(ControlledValidationProcessingFaultOptions.SectionName)
    .Get<ControlledValidationProcessingFaultOptions>() ?? new ControlledValidationProcessingFaultOptions();
if (processingFaultOptions.Enabled)
{
    builder.Services.AddSingleton<IProcessingFaultInjector, ControlledValidationProcessingFaultInjector>();
}
else
{
    builder.Services.AddSingleton<IProcessingFaultInjector, NoOpProcessingFaultInjector>();
}
builder.Services.AddSingleton<IRiskEligibilityService, RiskEligibilityService>();
builder.Services.AddSingleton<SimpleRiskScoringService>();
builder.Services.AddSingleton<IRiskScoringService>(sp => sp.GetRequiredService<SimpleRiskScoringService>());
builder.Services.AddSingleton<ISimpleRiskScoringService>(sp => sp.GetRequiredService<SimpleRiskScoringService>());
builder.Services.AddSingleton<IAreaRiskSnapshotService, AreaRiskSnapshotService>();

builder.Services.AddSingleton<ReadingRiskPipeline>();
builder.Services.AddSingleton<ReadingEventProcessingService>();

builder.Services.AddHostedService<PreventionWorker>();
builder.Services.AddHostedService<InboxRetryWorker>();

var app = builder.Build();
app.MapHealthChecks("/health/live", new HealthCheckOptions
{
    Predicate = _ => false
});
app.MapHealthChecks("/health/ready", new HealthCheckOptions
{
    Predicate = registration => registration.Tags.Contains("ready")
});
app.Run();
