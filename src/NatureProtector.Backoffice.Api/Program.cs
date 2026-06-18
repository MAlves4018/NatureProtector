using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using NatureProtector.Backoffice.Api.Bootstrap;
using NatureProtector.Backoffice.Api.Configuration;
using NatureProtector.Backoffice.Api.ControlPlane.Services;
using NatureProtector.Backoffice.Api.OpenApi;
using NatureProtector.Backoffice.Api.UserPlane.Services;
using NatureProtector.Infrastructure.Postgres.DependencyInjection;
using NatureProtector.Infrastructure.Postgres.Persistence;
using NatureProtector.Infrastructure.Postgres.Users;
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
builder.Services.AddHealthChecks();
builder.Services.AddHttpClient();
builder.Services.AddOpenApi(options =>
{
    options.AddDocumentTransformer<BackofficeOpenApiSecurityDocumentTransformer>();
    options.AddOperationTransformer<BackofficeOpenApiSecurityOperationTransformer>();
});
builder.Services.Configure<BackofficeApiOptions>(
    builder.Configuration.GetSection(BackofficeApiOptions.SectionName));
builder.Services.Configure<JwtAuthenticationOptions>(
    builder.Configuration.GetSection(JwtAuthenticationOptions.SectionName));

var jwtOptions = builder.Configuration
    .GetSection(JwtAuthenticationOptions.SectionName)
    .Get<JwtAuthenticationOptions>() ?? new JwtAuthenticationOptions();

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = jwtOptions.Issuer,
            ValidateAudience = true,
            ValidAudience = jwtOptions.Audience,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtOptions.SigningKey)),
            ValidateLifetime = true,
            ClockSkew = TimeSpan.FromMinutes(2)
        };
    });

builder.Services.AddAuthorization();

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
    builder.Services.AddScoped<IRuntimeObservabilityService, RuntimeObservabilityService>();
    builder.Services.AddSingleton<IPasswordHasher<UserRecord>, PasswordHasher<UserRecord>>();
    builder.Services.AddScoped<IUserRolePlaneService, PostgresUserRolePlaneService>();
}
else
{
    // Quando a feature está desligada, os controladores mantêm-se disponíveis
    // mas devolvem uma explicação clara de indisponibilidade.
    builder.Services.AddSingleton<IControlPlaneService>(
        _ => new UnavailableControlPlaneService(
            "The control-plane API is disabled. Set BackofficeApi:ControlPlaneEnabled=true to enable PostgreSQL-backed endpoints."));
    builder.Services.AddSingleton<IRuntimeObservabilityService>(
        _ => new UnavailableRuntimeObservabilityService(
            "Runtime observability is disabled because the control plane is not enabled."));
    builder.Services.AddSingleton<IUserRolePlaneService>(
        _ => new UnavailableUserRolePlaneService(
            "The user plane API is disabled because the control plane is not enabled."));
}

var app = builder.Build();

if (backofficeOptions.ControlPlaneEnabled)
{
    using var scope = app.Services.CreateScope();
    var dbContextFactory = scope.ServiceProvider
        .GetRequiredService<IDbContextFactory<NatureProtectorControlDbContext>>();
    var passwordHasher = scope.ServiceProvider.GetRequiredService<IPasswordHasher<UserRecord>>();
    var adminPassword = Environment.GetEnvironmentVariable("NP_BOOTSTRAP_ADMIN_PASSWORD");
    if (string.IsNullOrWhiteSpace(adminPassword) && app.Environment.IsDevelopment())
    {
        adminPassword = "admin123";
    }

    if (!string.IsNullOrWhiteSpace(adminPassword))
    {
        await using var dbContext = await dbContextFactory.CreateDbContextAsync();
        await BackofficeAdminBootstrapper.EnsureAdminUserAsync(
            dbContext,
            passwordHasher,
            adminPassword);
    }
}

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

app.UseAuthentication();
app.UseAuthorization();
app.MapHealthChecks("/health");
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
