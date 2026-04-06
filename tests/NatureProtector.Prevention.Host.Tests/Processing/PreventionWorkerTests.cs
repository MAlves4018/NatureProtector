using System.Reflection;
using System.Text;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NatureProtector.Prevention.Host.Persistence;
using NatureProtector.Prevention.Host.Processing;
using NatureProtector.Prevention.Host.Tests.Fakes;
using NatureProtector.Prevention.Host.Tests.Helpers;
using NatureProtector.Prevention.Host.Tests.TestData;
using NatureProtector.Prevention.Persistence;
using NatureProtector.Prevention.Risk;
using NatureProtector.Shared.Configuration;
using NatureProtector.Shared.Contracts.Readings;
using NatureProtector.Shared.Messaging;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace NatureProtector.Prevention.Host.Tests.Processing;

public sealed class PreventionWorkerTests
{
    [Fact]
    public async Task HandleReceivedAsync_AcknowledgesProcessedEnvelope()
    {
        var acceptedReadingRepository = new InMemoryAcceptedReadingRepository();
        var riskAssessmentRepository = new InMemoryRiskAssessmentRepository();
        var areaRiskSnapshotRepository = new InMemoryAreaRiskSnapshotRepository();
        var influxWriteService = new FakeInfluxWriteService();
        var worker = CreateWorker(CreatePipeline(
            acceptedReadingRepository,
            riskAssessmentRepository,
            areaRiskSnapshotRepository,
            influxWriteService));
        var (channel, recorder) = RecordingDispatchProxy<IModel>.CreateProxy();
        var envelope = EnvelopeFactory.Create(
            metricType: SensorMetricType.Temperature,
            unit: MeasurementUnit.Celsius,
            value: 35.0);

        await InvokeHandleReceivedAsync(
            worker,
            channel,
            CreateEventArgs(JsonEventSerializer.SerializeToUtf8Bytes(envelope), 11),
            CancellationToken.None);

        Assert.Single(await acceptedReadingRepository.GetAllAsync(CancellationToken.None));
        Assert.Single(recorder.Invocations.Where(x => x.MethodName == "BasicAck"));
        Assert.DoesNotContain(recorder.Invocations, x => x.MethodName == "BasicNack");
        Assert.Single(influxWriteService.AcceptedReadings);
    }

    [Fact]
    public async Task HandleReceivedAsync_AcknowledgesNullEnvelopeBodies()
    {
        var worker = CreateWorker(CreatePipeline(
            new InMemoryAcceptedReadingRepository(),
            new InMemoryRiskAssessmentRepository(),
            new InMemoryAreaRiskSnapshotRepository(),
            new FakeInfluxWriteService()));
        var (channel, recorder) = RecordingDispatchProxy<IModel>.CreateProxy();

        await InvokeHandleReceivedAsync(
            worker,
            channel,
            CreateEventArgs(Encoding.UTF8.GetBytes("null"), 12),
            CancellationToken.None);

        Assert.Single(recorder.Invocations.Where(x => x.MethodName == "BasicAck"));
        Assert.DoesNotContain(recorder.Invocations, x => x.MethodName == "BasicNack");
    }

    [Fact]
    public async Task HandleReceivedAsync_NacksInvalidJson()
    {
        var worker = CreateWorker(CreatePipeline(
            new InMemoryAcceptedReadingRepository(),
            new InMemoryRiskAssessmentRepository(),
            new InMemoryAreaRiskSnapshotRepository(),
            new FakeInfluxWriteService()));
        var (channel, recorder) = RecordingDispatchProxy<IModel>.CreateProxy();

        await InvokeHandleReceivedAsync(
            worker,
            channel,
            CreateEventArgs(Encoding.UTF8.GetBytes("{ invalid"), 13),
            CancellationToken.None);

        var nack = Assert.Single(recorder.Invocations.Where(x => x.MethodName == "BasicNack"));
        Assert.Equal(13UL, Assert.IsType<ulong>(nack.Arguments[0]));
        Assert.False(Assert.IsType<bool>(nack.Arguments[1]));
        Assert.False(Assert.IsType<bool>(nack.Arguments[2]));
    }

    [Fact]
    public async Task HandleReceivedAsync_Nacks_WhenPipelineThrows()
    {
        var worker = CreateWorker(CreatePipeline(
            new ThrowingAcceptedReadingRepository(),
            new InMemoryRiskAssessmentRepository(),
            new InMemoryAreaRiskSnapshotRepository(),
            new FakeInfluxWriteService()));
        var (channel, recorder) = RecordingDispatchProxy<IModel>.CreateProxy();
        var envelope = EnvelopeFactory.Create();

        await InvokeHandleReceivedAsync(
            worker,
            channel,
            CreateEventArgs(JsonEventSerializer.SerializeToUtf8Bytes(envelope), 14),
            CancellationToken.None);

        Assert.Single(recorder.Invocations.Where(x => x.MethodName == "BasicNack"));
        Assert.DoesNotContain(recorder.Invocations, x => x.MethodName == "BasicAck");
    }

    [Fact]
    public void DeclareTopology_DeclaresExchangeQueuesAndBindings()
    {
        var (channel, recorder) = RecordingDispatchProxy<IModel>.CreateProxy();
        var method = typeof(PreventionWorker).GetMethod(
            "DeclareTopology",
            BindingFlags.Static | BindingFlags.NonPublic)
            ?? throw new InvalidOperationException("DeclareTopology method was not found.");

        method.Invoke(null, [channel]);

        var exchangeDeclare = Assert.Single(recorder.Invocations.Where(x => x.MethodName == "ExchangeDeclare"));
        Assert.Equal(NatureProtectorRabbitMqTopology.ExchangeName, Assert.IsType<string>(exchangeDeclare.Arguments[0]));
        Assert.Equal(NatureProtectorRabbitMqTopology.ExchangeType, Assert.IsType<string>(exchangeDeclare.Arguments[1]));

        var queueDeclares = recorder.Invocations.Where(x => x.MethodName == "QueueDeclare").ToList();
        Assert.Equal(2, queueDeclares.Count);
        Assert.Equal(NatureProtectorRabbitMqTopology.Bindings.Count(), recorder.Invocations.Count(x => x.MethodName == "QueueBind"));
    }

    private static PreventionWorker CreateWorker(ReadingRiskPipeline pipeline)
    {
        return new PreventionWorker(
            NullLogger<PreventionWorker>.Instance,
            Options.Create(new RabbitMqOptions
            {
                HostName = "localhost",
                Port = 5672,
                UserName = "np",
                Password = "pass",
                ExchangeName = NatureProtectorRabbitMqTopology.ExchangeName
            }),
            pipeline);
    }

    private static ReadingRiskPipeline CreatePipeline(
        IAcceptedReadingRepository acceptedReadingRepository,
        IRiskAssessmentRepository riskAssessmentRepository,
        IAreaRiskSnapshotRepository areaRiskSnapshotRepository,
        FakeInfluxWriteService influxWriteService)
    {
        return new ReadingRiskPipeline(
            acceptedReadingRepository,
            new SimpleRiskScoringService(),
            riskAssessmentRepository,
            new AreaRiskSnapshotService(),
            areaRiskSnapshotRepository,
            influxWriteService,
            NullLogger<ReadingRiskPipeline>.Instance);
    }

    private static async Task InvokeHandleReceivedAsync(
        PreventionWorker worker,
        IModel channel,
        BasicDeliverEventArgs eventArgs,
        CancellationToken cancellationToken)
    {
        var method = typeof(PreventionWorker).GetMethod(
            "HandleReceivedAsync",
            BindingFlags.Instance | BindingFlags.NonPublic)
            ?? throw new InvalidOperationException("HandleReceivedAsync method was not found.");

        var task = method.Invoke(worker, [channel, eventArgs, cancellationToken]) as Task
            ?? throw new InvalidOperationException("HandleReceivedAsync did not return a Task.");

        await task;
    }

    private static BasicDeliverEventArgs CreateEventArgs(byte[] body, ulong deliveryTag)
    {
        return new BasicDeliverEventArgs
        {
            DeliveryTag = deliveryTag,
            Body = new ReadOnlyMemory<byte>(body)
        };
    }

    private sealed class ThrowingAcceptedReadingRepository : IAcceptedReadingRepository
    {
        public Task AddAsync(
            EventEnvelope<SensorReadingProducedPayload> envelope,
            CancellationToken cancellationToken)
        {
            throw new InvalidOperationException("boom");
        }

        public Task<IReadOnlyCollection<EventEnvelope<SensorReadingProducedPayload>>> GetAllAsync(
            CancellationToken cancellationToken)
        {
            return Task.FromResult<IReadOnlyCollection<EventEnvelope<SensorReadingProducedPayload>>>([]);
        }
    }
}
