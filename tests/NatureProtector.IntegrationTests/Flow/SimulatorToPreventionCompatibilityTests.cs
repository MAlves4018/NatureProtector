using Microsoft.Extensions.Logging.Abstractions;
using NatureProtector.Prevention.Host.Persistence;
using NatureProtector.Prevention.Host.Processing;
using NatureProtector.Prevention.Host.Projection;
using NatureProtector.Prevention.Persistence;
using NatureProtector.Prevention.Risk;
using NatureProtector.IntegrationTests.TestData;
using NatureProtector.Simulator.Host.Services;
using NatureProtector.IntegrationTests.Fakes;

namespace NatureProtector.IntegrationTests.Flow;

public sealed class SimulatorToPreventionCompatibilityTests
{
    [Fact]
    public async Task SimulatorEnvelope_CanBeConsumedByPreventionPipeline_WithoutBroker()
    {
        var simulatorOptions = SimulatorOptionsFactory.CreateValid();
        simulatorOptions.Sensors =
        [
            SimulatorOptionsFactory.CreateSensor(
                id: Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa"),
                name: "Sensor-T",
                type: NatureProtector.Core.Sensors.SensorType.Temperature)
        ];

        var simulationContextFactory = new ScenarioContextFactory(Microsoft.Extensions.Options.Options.Create(simulatorOptions));
        var context = simulationContextFactory.Create();
        var readingGenerationService = new ReadingGenerationService();
        var simulationRunId = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");
        var envelope = readingGenerationService.GenerateBatch(
            context,
            simulationRunId,
            cycleIndex: 0,
            eventTime: context.StartTimestamp,
            random: new Random(simulatorOptions.Seed!.Value)).Single();

        var acceptedReadingRepository = new InMemoryAcceptedReadingRepository();
        var riskAssessmentRepository = new InMemoryRiskAssessmentRepository();
        var areaRiskSnapshotRepository = new InMemoryAreaRiskSnapshotRepository();
        var influxWriteService = new FakeInfluxWriteService();
        var pipeline = new ReadingRiskPipeline(
            acceptedReadingRepository,
            new SimpleRiskScoringService(),
            riskAssessmentRepository,
            new AreaRiskSnapshotService(),
            areaRiskSnapshotRepository,
            new InMemoryAreaOperationalProjectionStore(),
            influxWriteService,
            NullLogger<ReadingRiskPipeline>.Instance);

        await pipeline.ProcessAcceptedReadingAsync(envelope, CancellationToken.None);

        var accepted = await acceptedReadingRepository.GetAllAsync(CancellationToken.None);
        var assessments = await riskAssessmentRepository.GetByAreaAsync(envelope.AreaId, CancellationToken.None);
        var snapshot = await areaRiskSnapshotRepository.GetLatestAsync(envelope.AreaId, CancellationToken.None);

        Assert.Single(accepted);
        Assert.Single(assessments);
        Assert.NotNull(snapshot);
        Assert.Single(influxWriteService.AcceptedReadings);
        Assert.Single(influxWriteService.RiskAssessments);
        Assert.Single(influxWriteService.AreaSnapshots);

        Assert.Equal(envelope.EventId, accepted.Single().EventId);
        Assert.Equal(envelope.Payload.SensorId, influxWriteService.RiskAssessments.Single().SensorId);
        Assert.Equal(envelope.EventTime, snapshot!.Timestamp);
    }

    [Fact]
    public async Task MultipleSimulatorReadings_FromSameArea_ProduceAggregatedSnapshot()
    {
        var simulatorOptions = SimulatorOptionsFactory.CreateValid();
        simulatorOptions.NumberOfCycles = 2;
        simulatorOptions.Sensors =
        [
            SimulatorOptionsFactory.CreateSensor(
                id: Guid.Parse("11111111-aaaa-bbbb-cccc-111111111111"),
                name: "Sensor-T",
                type: NatureProtector.Core.Sensors.SensorType.Temperature),
            SimulatorOptionsFactory.CreateSensor(
                id: Guid.Parse("22222222-aaaa-bbbb-cccc-222222222222"),
                name: "Sensor-W",
                type: NatureProtector.Core.Sensors.SensorType.Wind)
        ];

        var simulationContextFactory = new ScenarioContextFactory(Microsoft.Extensions.Options.Options.Create(simulatorOptions));
        var context = simulationContextFactory.Create();
        var readingGenerationService = new ReadingGenerationService();
        var envelopes = readingGenerationService.GenerateBatch(
            context,
            simulationRunId: Guid.Parse("33333333-aaaa-bbbb-cccc-333333333333"),
            cycleIndex: 1,
            eventTime: context.StartTimestamp.AddSeconds(context.Interval.TotalSeconds),
            random: new Random(simulatorOptions.Seed!.Value));

        var acceptedReadingRepository = new InMemoryAcceptedReadingRepository();
        var riskAssessmentRepository = new InMemoryRiskAssessmentRepository();
        var areaRiskSnapshotRepository = new InMemoryAreaRiskSnapshotRepository();
        var influxWriteService = new FakeInfluxWriteService();
        var pipeline = new ReadingRiskPipeline(
            acceptedReadingRepository,
            new SimpleRiskScoringService(),
            riskAssessmentRepository,
            new AreaRiskSnapshotService(),
            areaRiskSnapshotRepository,
            new InMemoryAreaOperationalProjectionStore(),
            influxWriteService,
            NullLogger<ReadingRiskPipeline>.Instance);

        foreach (var envelope in envelopes)
        {
            await pipeline.ProcessAcceptedReadingAsync(envelope, CancellationToken.None);
        }

        var assessments = await riskAssessmentRepository.GetByAreaAsync(context.AreaId, CancellationToken.None);
        var snapshot = await areaRiskSnapshotRepository.GetLatestAsync(context.AreaId, CancellationToken.None);

        Assert.Equal(2, assessments.Count);
        Assert.NotNull(snapshot);
        Assert.Equal(2, influxWriteService.AreaSnapshots.Last().AssessmentCount);
        Assert.Equal(2, influxWriteService.AcceptedReadings.Count);
        Assert.Equal(2, influxWriteService.RiskAssessments.Count);
    }
}
