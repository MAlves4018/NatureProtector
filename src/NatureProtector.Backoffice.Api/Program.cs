using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;
using NatureProtector.Backoffice.Api.Bootstrap;
using NatureProtector.Backoffice.Api.Configuration;
using NatureProtector.Backoffice.Api.ControlPlane.DataExplorer.Services;
using NatureProtector.Backoffice.Api.ControlPlane.Services;
using NatureProtector.Backoffice.Api.Health;
using NatureProtector.Backoffice.Api.OpenApi;
using NatureProtector.Backoffice.Api.Operations.Authorization;
using NatureProtector.Backoffice.Api.Operations.Configuration;
using NatureProtector.Backoffice.Api.Operations.Services;
using NatureProtector.Backoffice.Api.RuntimeOrchestration;
using NatureProtector.Backoffice.Api.UserPlane.Services;
using NatureProtector.Infrastructure.Postgres.DependencyInjection;
using NatureProtector.Infrastructure.Postgres.Persistence;
using NatureProtector.Infrastructure.Postgres.Users;
using NatureProtector.Shared.Configuration;
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
AddInfluxEnvironmentAliases(builder.Configuration);
builder.Logging.AddNatureProtectorActivityTracking();
builder.Services.AddNatureProtectorOpenTelemetry(
    builder.Configuration,
    builder.Environment,
    BackofficeApiTelemetry.ServiceName,
    [BackofficeApiTelemetry.ServiceName],
    [BackofficeApiTelemetry.ServiceName],
    enableAspNetCoreInstrumentation: true);

builder.Services.AddControllers();
var healthChecks = builder.Services.AddHealthChecks();
builder.Services.AddHttpClient();
builder.Services.AddNatureProtectorRuntimeOrchestration(builder.Configuration, builder.Environment);
builder.Services.AddOpenApi(options =>
{
    options.AddDocumentTransformer<BackofficeOpenApiSecurityDocumentTransformer>();
    options.AddOperationTransformer<BackofficeOpenApiSecurityOperationTransformer>();
});
builder.Services.AddSingleton<IValidateOptions<BackofficeApiOptions>, BackofficeApiOptionsValidator>();
builder.Services.AddOptions<BackofficeApiOptions>()
    .Bind(builder.Configuration.GetSection(BackofficeApiOptions.SectionName))
    .ValidateOnStart();
builder.Services.AddSingleton<IValidateOptions<JwtAuthenticationOptions>, JwtAuthenticationOptionsValidator>();
builder.Services.AddOptions<JwtAuthenticationOptions>()
    .Bind(builder.Configuration.GetSection(JwtAuthenticationOptions.SectionName))
    .ValidateOnStart();
builder.Services.AddNatureProtectorApiRateLimiting(builder.Configuration);

var jwtOptions = builder.Configuration
    .GetSection(JwtAuthenticationOptions.SectionName)
    .Get<JwtAuthenticationOptions>() ?? new JwtAuthenticationOptions();
var backofficeOptions = builder.Configuration
    .GetSection(BackofficeApiOptions.SectionName)
    .Get<BackofficeApiOptions>() ?? new BackofficeApiOptions();

EnsureOptionsAreValid(
    new JwtAuthenticationOptionsValidator(builder.Environment),
    jwtOptions,
    JwtAuthenticationOptions.SectionName);
EnsureOptionsAreValid(
    new BackofficeApiOptionsValidator(builder.Environment),
    backofficeOptions,
    BackofficeApiOptions.SectionName);

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

builder.Services.AddSingleton<IAuthorizationHandler, OperationCapabilityAuthorizationHandler>();
builder.Services.AddAuthorization(OperationAuthorization.Configure);
builder.Services.AddOptions<OperationsOptions>()
    .Bind(builder.Configuration.GetSection(OperationsOptions.SectionName));
builder.Services.AddSingleton<IOperationCatalog, OperationCatalog>();
builder.Services.AddSingleton<IOperationStore, FileSystemOperationStore>();
builder.Services.AddSingleton<IAutomationDispatcher, SafeAutomationDispatcher>();
builder.Services.AddSingleton<IEngineeringOperationsService, EngineeringOperationsService>();
builder.Services.AddSingleton<ICloudEnvironmentCatalogService, CloudEnvironmentCatalogService>();

if (backofficeOptions.ControlPlaneEnabled)
{
    // RabbitMQ Management is an observability surface, not a readiness
    // dependency. It nevertheless requires a dedicated, validated HTTP client
    // so cloud HTTPS/private-CA configuration is not silently downgraded.
    builder.Services.AddRabbitMqManagementHttpClient(builder.Configuration);

    // Quando o control plane está ativo, a API expõe dados reais persistidos em
    // PostgreSQL e só fica ready quando essa dependência obrigatória responde.
    builder.Services.AddNatureProtectorControlPlanePostgres(builder.Environment.ContentRootPath);
    builder.Services.AddSingleton<IRuntimeDataResetCoordinator, RuntimeDataResetCoordinator>();
    builder.Services.AddOptions<RuntimeOperationReconciliationOptions>()
        .Bind(builder.Configuration.GetSection(RuntimeOperationReconciliationOptions.SectionName));
    builder.Services.AddHostedService<RuntimeOperationReconciliationWorker>();
    healthChecks.AddCheck<ControlPlaneDatabaseHealthCheck>(
        "control-plane-postgres",
        failureStatus: HealthStatus.Unhealthy,
        tags: ["ready"],
        timeout: TimeSpan.FromSeconds(5));
    builder.Services.AddScoped<IControlPlaneService>(services =>
        new PostgresControlPlaneService(
            services.GetRequiredService<Microsoft.EntityFrameworkCore.IDbContextFactory<NatureProtector.Infrastructure.Postgres.Persistence.NatureProtectorControlDbContext>>(),
            builder.Environment.ContentRootPath,
            enableRuntimeProcessLaunch: backofficeOptions.LocalRuntimeProcessLaunchEnabled,
            runtimeRunOrchestrator: services.GetRequiredService<IRuntimeRunOrchestrator>(),
            runtimeEvidenceSink: services.GetRequiredService<IRuntimeEvidenceSink>(),
            runtimeDataResetCoordinator: services.GetRequiredService<IRuntimeDataResetCoordinator>(),
            environmentName: builder.Environment.EnvironmentName));
    builder.Services.AddScoped<IRuntimeObservabilityService, RuntimeObservabilityService>();
    builder.Services.AddSingleton<IPasswordHasher<UserRecord>, PasswordHasher<UserRecord>>();
    builder.Services.AddSingleton<IReadOnlyDataExplorerService, EfReadOnlyDataExplorerService>();
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
            "Runtime observability is disabled because the control plane is not enabled.",
            builder.Configuration
                .GetSection(RabbitMqOptions.SectionName)
                .Get<RabbitMqOptions>()));
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
            Detail = feature?.Error?.Message
                ?? (isPostgresUnavailable
                ? "Backoffice API could not reach PostgreSQL. Check POSTGRES_HOST, POSTGRES_PORT and the local runtime launcher configuration."
                : "An unexpected Backoffice API error occurred.")
        };

        await context.Response.WriteAsJsonAsync(problem);
    });
});

app.UseAuthentication();
app.UseRateLimiter();
app.UseAuthorization();
app.MapHealthChecks("/health");
app.MapHealthChecks("/health/live", new HealthCheckOptions
{
    // Liveness representa apenas o processo HTTP; dependências externas não
    // devem provocar reinícios contínuos do container.
    Predicate = _ => false
});
app.MapHealthChecks("/health/ready", new HealthCheckOptions
{
    Predicate = registration => registration.Tags.Contains("ready")
});
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

static void EnsureOptionsAreValid<TOptions>(
    IValidateOptions<TOptions> validator,
    TOptions options,
    string optionsName)
    where TOptions : class
{
    var result = validator.Validate(optionsName, options);
    if (result.Failed)
    {
        throw new OptionsValidationException(
            optionsName,
            typeof(TOptions),
            result.Failures ?? ["Unknown configuration validation failure."]);
    }
}

static void AddInfluxEnvironmentAliases(IConfigurationManager configuration)
{
    var aliases = new Dictionary<string, string?>();
    AddAliasIfMissing(aliases, configuration, "InfluxDb:Url", Environment.GetEnvironmentVariable("INFLUXDB_URL"));
    var port = Environment.GetEnvironmentVariable("INFLUXDB_PORT");
    if (string.IsNullOrWhiteSpace(configuration["InfluxDb:Url"]) &&
        string.IsNullOrWhiteSpace(aliases.GetValueOrDefault("InfluxDb:Url")) &&
        !string.IsNullOrWhiteSpace(port))
    {
        aliases["InfluxDb:Url"] = $"http://localhost:{port}";
    }

    AddAliasIfMissing(aliases, configuration, "InfluxDb:Token", Environment.GetEnvironmentVariable("INFLUXDB_TOKEN"));
    AddAliasIfMissing(aliases, configuration, "InfluxDb:Bucket", Environment.GetEnvironmentVariable("INFLUXDB_BUCKET"));
    AddAliasIfMissing(aliases, configuration, "InfluxDb:Bucket", Environment.GetEnvironmentVariable("INFLUXDB_DATABASE"));
    if (aliases.Count > 0)
    {
        configuration.AddInMemoryCollection(aliases);
    }
}

static void AddAliasIfMissing(
    IDictionary<string, string?> aliases,
    IConfiguration configuration,
    string target,
    string? value)
{
    if (!string.IsNullOrWhiteSpace(configuration[target]) ||
        aliases.ContainsKey(target) ||
        string.IsNullOrWhiteSpace(value))
    {
        return;
    }

    aliases[target] = value;
}
