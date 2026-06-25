using Microsoft.Extensions.Options;

namespace NatureProtector.Backoffice.Api.RuntimeOrchestration;

public static class RuntimeOrchestrationServiceCollectionExtensions
{
    public static IServiceCollection AddNatureProtectorRuntimeOrchestration(
        this IServiceCollection services,
        IConfiguration configuration,
        IHostEnvironment environment)
    {
        services.AddOptions<RuntimeOrchestrationOptions>()
            .Bind(configuration.GetSection(RuntimeOrchestrationOptions.SectionName))
            .Validate(options =>
                options.MaximumTimeoutSeconds is >= 5 and <= 24 * 60 * 60,
                "RuntimeOrchestration:MaximumTimeoutSeconds must be between 5 and 86400.")
            .Validate(options =>
                !string.IsNullOrWhiteSpace(options.EvidenceRoot),
                "RuntimeOrchestration:EvidenceRoot is required.")
            .Validate(options => options.CloudRunLaunchLeaseSeconds is >= 30 and <= 900,
                "RuntimeOrchestration:CloudRunLaunchLeaseSeconds must be between 30 and 900.")
            .Validate(options => options.CloudRunPollIntervalSeconds is >= 1 and <= 30,
                "RuntimeOrchestration:CloudRunPollIntervalSeconds must be between 1 and 30.")
            .Validate(options =>
                !options.AllowRemoteLaunch ||
                string.Equals(options.Mode, RuntimeOrchestrationModes.CloudRunJob, StringComparison.OrdinalIgnoreCase),
                "RuntimeOrchestration:AllowRemoteLaunch=true requires Mode=CloudRunJob.")
            .ValidateOnStart();

        var evidenceMode = configuration
            .GetSection(RuntimeOrchestrationOptions.SectionName)
            .GetValue<string>(nameof(RuntimeOrchestrationOptions.EvidenceMode))
            ?? RuntimeEvidenceModes.Disabled;

        if (string.Equals(evidenceMode, RuntimeEvidenceModes.FileSystem, StringComparison.OrdinalIgnoreCase))
        {
            if (!environment.IsDevelopment() &&
                !string.Equals(environment.EnvironmentName, "Evidence", StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException(
                    "RuntimeOrchestration:EvidenceMode=FileSystem is restricted to Development or Evidence environments.");
            }

            services.AddSingleton<IRuntimeEvidenceSink, FileSystemRuntimeEvidenceSink>();
        }
        else if (string.Equals(evidenceMode, RuntimeEvidenceModes.Disabled, StringComparison.OrdinalIgnoreCase))
        {
            services.AddSingleton<IRuntimeEvidenceSink>(_ => NullRuntimeEvidenceSink.Instance);
        }
        else
        {
            throw new InvalidOperationException(
                $"Unsupported RuntimeOrchestration:EvidenceMode '{evidenceMode}'. Expected Disabled or FileSystem.");
        }

        var mode = configuration
            .GetSection(RuntimeOrchestrationOptions.SectionName)
            .GetValue<string>(nameof(RuntimeOrchestrationOptions.Mode))
            ?? RuntimeOrchestrationModes.Disabled;

        if (string.Equals(mode, RuntimeOrchestrationModes.LocalProcess, StringComparison.OrdinalIgnoreCase))
        {
            if (!environment.IsDevelopment() &&
                !string.Equals(environment.EnvironmentName, "Evidence", StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException(
                    "RuntimeOrchestration:Mode=LocalProcess is restricted to Development or Evidence environments.");
            }

            services.AddSingleton<IRuntimeRunOrchestrator, LocalProcessRuntimeRunOrchestrator>();
        }
        else if (string.Equals(mode, RuntimeOrchestrationModes.CloudRunJob, StringComparison.OrdinalIgnoreCase))
        {
            var section = configuration.GetSection(RuntimeOrchestrationOptions.SectionName);
            var projectId = section.GetValue<string>(nameof(RuntimeOrchestrationOptions.CloudRunProjectId));
            var region = section.GetValue<string>(nameof(RuntimeOrchestrationOptions.CloudRunRegion));
            var jobName = section.GetValue<string>(nameof(RuntimeOrchestrationOptions.CloudRunSimulatorJobName));
            if (string.IsNullOrWhiteSpace(projectId) || string.IsNullOrWhiteSpace(region) || string.IsNullOrWhiteSpace(jobName))
            {
                throw new InvalidOperationException(
                    "CloudRunJob mode requires CloudRunProjectId, CloudRunRegion and CloudRunSimulatorJobName.");
            }

            services.AddHttpClient<IGoogleAccessTokenSource, MetadataGoogleAccessTokenSource>();
            services.AddHttpClient<ICloudRunJobsGateway, CloudRunJobsRestGateway>();
            services.AddSingleton<ICloudRunExecutionStore, PostgresCloudRunExecutionStore>();
            services.AddSingleton<IRuntimeRunOrchestrator, CloudRunJobRuntimeRunOrchestrator>();
        }
        else if (string.Equals(mode, RuntimeOrchestrationModes.Disabled, StringComparison.OrdinalIgnoreCase))
        {
            services.AddSingleton<IRuntimeRunOrchestrator>(_ => DisabledRuntimeRunOrchestrator.Instance);
        }
        else
        {
            throw new InvalidOperationException(
                $"Unsupported RuntimeOrchestration:Mode '{mode}'. Expected Disabled, LocalProcess or CloudRunJob.");
        }

        return services;
    }
}
