using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
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
    builder.Services.AddScoped<IControlPlaneService>(services =>
        new PostgresControlPlaneService(
            services.GetRequiredService<Microsoft.EntityFrameworkCore.IDbContextFactory<NatureProtector.Infrastructure.Postgres.Persistence.NatureProtectorControlDbContext>>(),
            builder.Environment.ContentRootPath,
            enableRuntimeProcessLaunch: true));
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

app.UseExceptionHandler(errorApp =>
{
    errorApp.Run(async context =>
    {
        var feature = context.Features.Get<IExceptionHandlerFeature>();
        var isPostgresUnavailable = IsPostgresConnectivityFailure(feature?.Error);

        context.Response.StatusCode = isPostgresUnavailable
            ? StatusCodes.Status503ServiceUnavailable
            : StatusCodes.Status500InternalServerError;
        context.Response.ContentType = "application/problem+json";

        var problem = new ProblemDetails
        {
            Status = context.Response.StatusCode,
            Title = isPostgresUnavailable
                ? "Control plane database unavailable"
                : "Backoffice API error",
            Detail = isPostgresUnavailable
                ? "Backoffice API could not reach PostgreSQL. Check POSTGRES_HOST, POSTGRES_PORT and the local runtime launcher configuration."
                : "An unexpected Backoffice API error occurred."
        };

        await context.Response.WriteAsJsonAsync(problem);
    });
});

app.UseAuthorization();
app.MapControllers();

app.Run();

static bool IsPostgresConnectivityFailure(Exception? exception)
{
    for (var current = exception; current is not null; current = current.InnerException)
    {
        var typeName = current.GetType().FullName ?? current.GetType().Name;
        if (typeName.Contains("Npgsql", StringComparison.OrdinalIgnoreCase) ||
            typeName.Contains("SocketException", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (current.Message.Contains("PostgreSQL", StringComparison.OrdinalIgnoreCase) &&
            current.Message.Contains("connect", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }
    }

    return false;
}
