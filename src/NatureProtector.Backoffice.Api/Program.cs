using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using NatureProtector.Backoffice.Api.Configuration;
using NatureProtector.Backoffice.Api.ControlPlane.Services;
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
builder.Services.AddOpenApi();
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
        var adminUser = await dbContext.Users
            .SingleOrDefaultAsync(
                entity => entity.Username == UserRecord.AdminUsername ||
                          entity.Email == UserRecord.AdminEmail);

        if (adminUser is null)
        {
            adminUser = new UserRecord
            {
                Id = Guid.Parse(UserRecord.AdminIdString),
                Username = UserRecord.AdminUsername,
                Email = UserRecord.AdminEmail,
                Organization = UserRecord.AdminOrganization,
                CreatedAt = DateTimeOffset.UtcNow
            };

            dbContext.Users.Add(adminUser);
        }

        adminUser.PasswordHash = passwordHasher.HashPassword(adminUser, adminPassword);

        var hasAdminRole = await dbContext.Set<UserRoleRecord>()
            .AnyAsync(entity => entity.UserId == adminUser.Id && entity.RoleId == RoleRecord.AdminId);
        if (!hasAdminRole)
        {
            dbContext.Set<UserRoleRecord>().Add(new UserRoleRecord
            {
                UserId = adminUser.Id,
                RoleId = RoleRecord.AdminId
            });
        }

        await dbContext.SaveChangesAsync();
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
