using NatureProtector.Core.Scenarios;
using NatureProtector.Simulator.Host.Services;

namespace NatureProtector.Simulator.Host.ControlledValidation;

public sealed class ControlledValidationOrchestrator(
    ILogger<ControlledValidationOrchestrator> logger,
    IHostEnvironment environment,
    ControlledValidationManifestFactory manifestFactory,
    ControlledValidationEvidenceWriter evidenceWriter,
    ISimulationContextSource simulationContextSource,
    ISimulationRunStore simulationRunStore,
    IControlledValidationMessagePublisher publisher)
{
    public Task<ControlledValidationScenarioManifest> PublishP0Async(
        CancellationToken cancellationToken = default)
    {
        return PublishAsync(cancellationToken);
    }

    public async Task<ControlledValidationScenarioManifest> PublishAsync(
        CancellationToken cancellationToken = default)
    {
        ControlledValidationEnvironmentGuard.EnsureAllowed(environment.EnvironmentName);

        var manifest = manifestFactory.Create();
        var builder = new ControlledValidationMessageBuilder(manifest);
        var messages = builder.BuildMessages();
        var (context, run) = await EnsureSimulationRunAsync(manifest, cancellationToken);

        await evidenceWriter.WriteExpectedOutcomesAsync(
            manifest,
            messages,
            cancellationToken);

        logger.LogInformation(
            "Starting controlled validation publication | Phase={Phase} | RunLabel={RunLabel} | ControlledValidationRunId={RunId} | ScenarioCode={ScenarioCode} | MessageCount={MessageCount}",
            manifest.Phase,
            manifest.RunLabel,
            manifest.ControlledValidationRunId,
            manifest.ScenarioCode,
            messages.Count);

        foreach (var message in messages)
        {
            await publisher.PublishAsync(message, cancellationToken);
        }

        run.Complete(DateTimeOffset.UtcNow);
        await simulationRunStore.UpsertAsync(context, run, cancellationToken);

        logger.LogInformation(
            "Completed controlled validation publication | Phase={Phase} | RunLabel={RunLabel} | ControlledValidationRunId={RunId}",
            manifest.Phase,
            manifest.RunLabel,
            manifest.ControlledValidationRunId);

        return manifest;
    }

    private async Task<(SimulationContext Context, SimulationRun Run)> EnsureSimulationRunAsync(
        ControlledValidationScenarioManifest manifest,
        CancellationToken cancellationToken)
    {
        var context = await simulationContextSource.CreateAsync(cancellationToken);

        if (context.AreaId != manifest.AreaId)
        {
            throw new InvalidOperationException(
                $"Controlled validation AreaId '{manifest.AreaId}' does not match resolved simulation context AreaId '{context.AreaId}'.");
        }

        if (!string.IsNullOrWhiteSpace(context.ScenarioCode) &&
            !string.Equals(context.ScenarioCode, manifest.ScenarioCode, StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                $"Controlled validation ScenarioCode '{manifest.ScenarioCode}' does not match resolved simulation context ScenarioCode '{context.ScenarioCode}'.");
        }

        var contextWithRunMetadata = CreateContextWithRunMetadata(context, manifest);
        var run = new SimulationRun(manifest.SimulationRunId, contextWithRunMetadata.PreferredSeed);

        run.MarkReady();
        await simulationRunStore.UpsertAsync(contextWithRunMetadata, run, cancellationToken);

        run.Start(DateTimeOffset.UtcNow);
        await simulationRunStore.UpsertAsync(contextWithRunMetadata, run, cancellationToken);

        return (contextWithRunMetadata, run);
    }

    private static SimulationContext CreateContextWithRunMetadata(
        SimulationContext context,
        ControlledValidationScenarioManifest manifest)
    {
        var correlationId = $"controlled-validation:{manifest.RunLabel}";
        var requested = context.RunOverrides?.Requested;
        var resolved = context.RunOverrides?.Resolved;
        var requestedOverrides = new SimulationRunOverridesRequested(
            requested?.SensorCount,
            requested?.NumberOfCycles ?? context.NumberOfCycles,
            requested?.IntervalSeconds ?? (int)context.Interval.TotalSeconds,
            requested?.Seed ?? context.PreferredSeed,
            requested?.DegradationProfile,
            correlationId)
        {
            DegradationProfiles = requested?.DegradationProfiles ?? []
        };
        var resolvedOverrides = new SimulationRunOverridesResolved(
            resolved?.SensorCount ?? context.Sensors.Count,
            resolved?.NumberOfCycles ?? context.NumberOfCycles,
            resolved?.IntervalSeconds ?? (int)context.Interval.TotalSeconds,
            resolved?.PreferredSeed ?? context.PreferredSeed,
            resolved?.DegradationProfile,
            correlationId,
            resolved?.SelectedSensorNames ?? context.Sensors.Select(sensor => sensor.Name).ToArray())
        {
            DegradationProfiles = resolved?.DegradationProfiles ?? []
        };

        return new SimulationContext(
            areaId: context.AreaId,
            scenario: context.Scenario,
            sensors: context.Sensors,
            startTimestamp: context.StartTimestamp,
            interval: context.Interval,
            numberOfCycles: context.NumberOfCycles,
            configurationVersionId: context.ConfigurationVersionId,
            scenarioCode: context.ScenarioCode,
            preferredSeed: context.PreferredSeed,
            runOverrides: new SimulationRunOverridesSnapshot(requestedOverrides, resolvedOverrides));
    }
}
