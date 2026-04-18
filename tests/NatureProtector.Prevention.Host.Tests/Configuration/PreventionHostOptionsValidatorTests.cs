using NatureProtector.Prevention.Host.Configuration;

namespace NatureProtector.Prevention.Host.Tests.Configuration;

public sealed class PreventionHostOptionsValidatorTests
{
    private readonly PreventionHostOptionsValidator _validator = new();

    [Fact]
    public void Validate_Succeeds_ForValidOptions()
    {
        var options = new PreventionHostOptions
        {
            PipelinePersistenceEnabled = true,
            ConsumerPrefetchCount = 1,
            MaxProcessingAttempts = 3,
            RetryDelaySeconds = [5, 30],
            RetryPollingIntervalSeconds = 5
        };

        var result = _validator.Validate(name: null, options);

        Assert.True(result.Succeeded);
    }

    [Fact]
    public void Validate_Fails_WhenAttemptsPollingOrDelaysAreInvalid()
    {
        var options = new PreventionHostOptions
        {
            ConsumerPrefetchCount = 0,
            MaxProcessingAttempts = 0,
            RetryDelaySeconds = [5, -1],
            RetryPollingIntervalSeconds = 0
        };

        var result = _validator.Validate(name: null, options);

        Assert.True(result.Failed);
        Assert.Contains(
            "PreventionHost:ConsumerPrefetchCount must be greater than zero.",
            result.Failures);
        Assert.Contains(
            "PreventionHost:MaxProcessingAttempts must be greater than zero.",
            result.Failures);
        Assert.Contains(
            "PreventionHost:RetryDelaySeconds must contain only non-negative values.",
            result.Failures);
        Assert.Contains(
            "PreventionHost:RetryPollingIntervalSeconds must be greater than zero.",
            result.Failures);
    }
}
