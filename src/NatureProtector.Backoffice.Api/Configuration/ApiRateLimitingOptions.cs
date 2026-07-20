namespace NatureProtector.Backoffice.Api.Configuration;

public sealed class ApiRateLimitingOptions
{
    public const string SectionName = "RateLimiting";

    public bool Enabled { get; init; } = true;

    // Enable only when the external load balancer normalizes X-Forwarded-For
    // by replacing caller-supplied values with client and forwarding-rule IPs.
    public bool TrustNormalizedForwardedFor { get; init; }

    // Development-only partitioning for evidence campaigns. The header is
    // ignored outside Development and never disables rate limiting.
    public bool EvidenceSimulationLaunchPartitioningEnabled { get; init; }

    public string EvidenceRunIdHeaderName { get; init; } = "X-NP-Evidence-Run-Id";


    public RateLimitWindowOptions AnonymousRead { get; init; } = new(120, 60);

    public RateLimitWindowOptions AuthenticatedRead { get; init; } = new(600, 60);

    public RateLimitWindowOptions Authentication { get; init; } = new(10, 60);

    public RateLimitWindowOptions Mutation { get; init; } = new(120, 60);

    public RateLimitWindowOptions SimulationLaunch { get; init; } = new(6, 300);

    public RateLimitWindowOptions ExpensiveRead { get; init; } = new(30, 60);

    public RateLimitWindowOptions Administration { get; init; } = new(60, 60);

    public RateLimitWindowOptions Technical { get; init; } = new(600, 60);

    public RateLimitWindowOptions GetPolicy(string policyName) => policyName switch
    {
        ApiRateLimitPolicies.AnonymousRead => AnonymousRead,
        ApiRateLimitPolicies.AuthenticatedRead => AuthenticatedRead,
        ApiRateLimitPolicies.Authentication => Authentication,
        ApiRateLimitPolicies.Mutation => Mutation,
        ApiRateLimitPolicies.SimulationLaunch => SimulationLaunch,
        ApiRateLimitPolicies.ExpensiveRead => ExpensiveRead,
        ApiRateLimitPolicies.Administration => Administration,
        ApiRateLimitPolicies.Technical => Technical,
        _ => AuthenticatedRead
    };

    public bool IsValid() =>
        AnonymousRead.IsValid() &&
        AuthenticatedRead.IsValid() &&
        Authentication.IsValid() &&
        Mutation.IsValid() &&
        SimulationLaunch.IsValid() &&
        ExpensiveRead.IsValid() &&
        Administration.IsValid() &&
        Technical.IsValid();
}

public sealed record RateLimitWindowOptions(int PermitLimit, int WindowSeconds)
{
    public bool IsValid() => PermitLimit is >= 1 and <= 1_000_000 && WindowSeconds is >= 1 and <= 86_400;
}

public static class ApiRateLimitPolicies
{
    public const string AnonymousRead = "anonymous-read";
    public const string AuthenticatedRead = "authenticated-read";
    public const string Authentication = "authentication";
    public const string Mutation = "mutation";
    public const string SimulationLaunch = "simulation-launch";
    public const string ExpensiveRead = "expensive-read";
    public const string Administration = "administration";
    public const string Technical = "technical";
}
