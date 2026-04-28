using NatureProtector.Backoffice.Api.Configuration;
using NatureProtector.Backoffice.Api.ControlPlane.Services;
using NatureProtector.Infrastructure.Postgres.DependencyInjection;
using NatureProtector.Shared.Observability;

/*
 * Este ponto de entrada compõe a API de backoffice usada para consultar o
 * control plane e as projeções operacionais.
 *
 * Rationale:
 * - A API deve poder ser arrancada em modo ativo ou em modo indisponível sem
 *   reescrever controladores.
 * - A composição por DI torna explícita a dependência opcional do PostgreSQL.
 */

var builder = WebApplication.CreateBuilder(args);
builder.Logging.AddNatureProtectorActivityTracking();
builder.Services.AddNatureProtectorOpenTelemetry(
    builder.Configuration,
    builder.Environment,
    BackofficeApiTelemetry.ServiceName,
    [BackofficeApiTelemetry.ServiceName],
    [BackofficeApiTelemetry.ServiceName],
    enableAspNetCoreInstrumentation: true);

builder.Services.AddControllers();
builder.Services.AddOpenApi();
builder.Services.Configure<BackofficeApiOptions>(
    builder.Configuration.GetSection(BackofficeApiOptions.SectionName));

var backofficeOptions = builder.Configuration
    .GetSection(BackofficeApiOptions.SectionName)
    .Get<BackofficeApiOptions>() ?? new BackofficeApiOptions();

if (backofficeOptions.ControlPlaneEnabled)
{
    // Quando o control plane está ativo, a API expõe dados reais persistidos em
    // PostgreSQL.
    builder.Services.AddNatureProtectorControlPlanePostgres(builder.Environment.ContentRootPath);
    builder.Services.AddScoped<IControlPlaneService, PostgresControlPlaneService>();
}
else
{
    // Quando a feature está desligada, os controladores mantêm-se disponíveis
    // mas devolvem uma explicação clara de indisponibilidade.
    builder.Services.AddSingleton<IControlPlaneService>(
        _ => new UnavailableControlPlaneService(
            "The control-plane API is disabled. Set BackofficeApi:ControlPlaneEnabled=true to enable PostgreSQL-backed endpoints."));
}

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseAuthorization();
app.MapControllers();

app.Run();
