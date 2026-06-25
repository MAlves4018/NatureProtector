using System.Net;
using System.Security.Claims;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Options;

namespace NatureProtector.Backoffice.Api.Configuration;

public static class ApiRateLimitingExtensions
{
    public static IServiceCollection AddNatureProtectorApiRateLimiting(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddOptions<ApiRateLimitingOptions>()
            .Bind(configuration.GetSection(ApiRateLimitingOptions.SectionName))
            .Validate(options => options.IsValid(), "Every rate-limit policy must define a positive permit limit and window.")
            .ValidateOnStart();

        services.AddRateLimiter(options =>
        {
            options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
            options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(context =>
            {
                var configured = context.RequestServices
                    .GetRequiredService<IOptions<ApiRateLimitingOptions>>()
                    .Value;

                if (!configured.Enabled || IsUnrestrictedHealthEndpoint(context))
                {
                    return RateLimitPartition.GetNoLimiter("unrestricted-health");
                }

                var policyName = Classify(context);
                var clientKey = ResolveClientKey(context, configured.TrustNormalizedForwardedFor);
                var policy = configured.GetPolicy(policyName);

                return RateLimitPartition.GetFixedWindowLimiter(
                    $"{policyName}:{clientKey}",
                    _ => new FixedWindowRateLimiterOptions
                    {
                        PermitLimit = policy.PermitLimit,
                        Window = TimeSpan.FromSeconds(policy.WindowSeconds),
                        QueueLimit = 0,
                        QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                        AutoReplenishment = true
                    });
            });

            options.OnRejected = async (context, cancellationToken) =>
            {
                var policyName = Classify(context.HttpContext);
                context.HttpContext.Response.Headers["X-RateLimit-Policy"] = policyName;

                if (context.Lease.TryGetMetadata(MetadataName.RetryAfter, out var retryAfter))
                {
                    context.HttpContext.Response.Headers["Retry-After"] = Math.Max(1, (int)Math.Ceiling(retryAfter.TotalSeconds)).ToString();
                }

                var problem = new ProblemDetails
                {
                    Status = StatusCodes.Status429TooManyRequests,
                    Title = "Request rate limit exceeded",
                    Detail = "The request was rejected to protect service availability. Retry after the indicated interval.",
                    Type = "https://httpstatuses.com/429"
                };
                problem.Extensions["policy"] = policyName;

                context.HttpContext.Response.ContentType = "application/problem+json";
                await context.HttpContext.Response.WriteAsJsonAsync(problem, cancellationToken);
            };
        });

        return services;
    }

    public static string Classify(HttpContext context)
    {
        var path = context.Request.Path.Value ?? string.Empty;
        var method = context.Request.Method;

        if (path.StartsWith("/health", StringComparison.OrdinalIgnoreCase) ||
            path.StartsWith("/openapi", StringComparison.OrdinalIgnoreCase))
        {
            return ApiRateLimitPolicies.Technical;
        }

        if (path.Equals("/api/users-roles/login", StringComparison.OrdinalIgnoreCase))
        {
            return ApiRateLimitPolicies.Authentication;
        }

        if (HttpMethods.IsPost(method) &&
            (path.Equals("/api/control/runtime/runs", StringComparison.OrdinalIgnoreCase) ||
             path.EndsWith("/p3/run", StringComparison.OrdinalIgnoreCase)))
        {
            return ApiRateLimitPolicies.SimulationLaunch;
        }

        if (path.Contains("/diagnostics", StringComparison.OrdinalIgnoreCase) ||
            path.Contains("/observability/evidence", StringComparison.OrdinalIgnoreCase) ||
            path.EndsWith("/GeoJSON", StringComparison.OrdinalIgnoreCase))
        {
            return ApiRateLimitPolicies.ExpensiveRead;
        }

        if (path.StartsWith("/api/users-roles", StringComparison.OrdinalIgnoreCase) &&
            !HttpMethods.IsGet(method) &&
            !HttpMethods.IsHead(method))
        {
            return ApiRateLimitPolicies.Administration;
        }

        if (!HttpMethods.IsGet(method) && !HttpMethods.IsHead(method))
        {
            return ApiRateLimitPolicies.Mutation;
        }

        return context.User.Identity?.IsAuthenticated == true
            ? ApiRateLimitPolicies.AuthenticatedRead
            : ApiRateLimitPolicies.AnonymousRead;
    }


    private static bool IsUnrestrictedHealthEndpoint(HttpContext context)
        => context.Request.Path.StartsWithSegments("/health", StringComparison.OrdinalIgnoreCase);

    private static string ResolveClientKey(HttpContext context, bool trustNormalizedForwardedFor)
    {
        var subject = context.User.FindFirstValue(ClaimTypes.NameIdentifier) ??
                      context.User.FindFirstValue("sub");
        if (!string.IsNullOrWhiteSpace(subject))
        {
            return $"subject:{subject}";
        }

        if (trustNormalizedForwardedFor &&
            TryReadNormalizedForwardedAddress(context, out var forwardedAddress))
        {
            return $"ip:{forwardedAddress}";
        }

        var address = context.Connection.RemoteIpAddress?.ToString();
        return string.IsNullOrWhiteSpace(address) ? "anonymous:unknown" : $"ip:{address}";
    }

    private static bool TryReadNormalizedForwardedAddress(HttpContext context, out string address)
    {
        address = string.Empty;
        var header = context.Request.Headers["X-Forwarded-For"].ToString();
        if (string.IsNullOrWhiteSpace(header))
        {
            return false;
        }

        // The G8.1 load balancer backend overwrites this header as
        // <client-ip>,<forwarding-rule-ip>. Never enable the option when the
        // service can be reached through an untrusted proxy path.
        var first = header.Split(',', 2, StringSplitOptions.TrimEntries)[0];
        if (!IPAddress.TryParse(first, out var parsed))
        {
            return false;
        }

        address = parsed.ToString();
        return true;
    }
}
