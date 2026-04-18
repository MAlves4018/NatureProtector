using Microsoft.Extensions.Options;

namespace NatureProtector.Prevention.Host.Configuration;

public sealed class PreventionHostOptionsValidator : IValidateOptions<PreventionHostOptions>
{
    public ValidateOptionsResult Validate(string? name, PreventionHostOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        var failures = new List<string>();

        if (options.MaxProcessingAttempts <= 0)
        {
            failures.Add("PreventionHost:MaxProcessingAttempts must be greater than zero.");
        }

        if (options.ConsumerPrefetchCount == 0)
        {
            failures.Add("PreventionHost:ConsumerPrefetchCount must be greater than zero.");
        }

        if (options.RetryDelaySeconds is null)
        {
            failures.Add("PreventionHost:RetryDelaySeconds must not be null.");
        }
        else if (options.RetryDelaySeconds.Any(seconds => seconds < 0))
        {
            failures.Add("PreventionHost:RetryDelaySeconds must contain only non-negative values.");
        }

        if (options.RetryPollingIntervalSeconds <= 0)
        {
            failures.Add("PreventionHost:RetryPollingIntervalSeconds must be greater than zero.");
        }

        return failures.Count == 0
            ? ValidateOptionsResult.Success
            : ValidateOptionsResult.Fail(failures);
    }
}
