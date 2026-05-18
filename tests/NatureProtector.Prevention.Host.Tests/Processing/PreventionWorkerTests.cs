using System.Reflection;
using System.Text;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NatureProtector.Prevention.Host.Configuration;
using NatureProtector.Prevention.Host.Persistence;
using NatureProtector.Prevention.Host.Projection;
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
        var inbox = new InMemoryReadingEventInbox();
        var worker = CreateWorker(CreatePipeline(
            acceptedReadingRepository,
            riskAssessmentRepository,
            areaRiskSnapshotRepository,
            influxWriteService),
            inbox);
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
        Assert.Single(recorder.Invocations, x => x.MethodName == "BasicAck");
        Assert.DoesNotContain(recorder.Invocations, x => x.MethodName == "BasicNack");
        Assert.Single(influxWriteService.AcceptedReadings);
        Assert.Single(inbox.Events);
        Assert.Equal(NatureProtector.Infrastructure.Postgres.Pipeline.InboxEventStatus.Processed, inbox.Events.Single().Status);
    }

    [Fact]
    public async Task HandleReceivedAsync_AcknowledgesNullEnvelopeBodies()
    {
        var inbox = new InMemoryReadingEventInbox();
        var worker = CreateWorker(CreatePipeline(
            new InMemoryAcceptedReadingRepository(),
            new InMemoryRiskAssessmentRepository(),
            new InMemoryAreaRiskSnapshotRepository(),
            new FakeInfluxWriteService()),
            inbox);
        var (channel, recorder) = RecordingDispatchProxy<IModel>.CreateProxy();

        await InvokeHandleReceivedAsync(
            worker,
            channel,
            CreateEventArgs(Encoding.UTF8.GetBytes("null"), 12),
            CancellationToken.None);

        Assert.Single(recorder.Invocations, x => x.MethodName == "BasicAck");
        Assert.DoesNotContain(recorder.Invocations, x => x.MethodName == "BasicNack");
        Assert.Single(inbox.Rejections);
        Assert.Equal("null_envelope", inbox.Rejections.Single().RejectionCode);
    }

    [Fact]
    public async Task HandleReceivedAsync_AcknowledgesNullPayload_AndStoresRejection()
    {
        var inbox = new InMemoryReadingEventInbox();
        var worker = CreateWorker(CreatePipeline(
            new InMemoryAcceptedReadingRepository(),
            new InMemoryRiskAssessmentRepository(),
            new InMemoryAreaRiskSnapshotRepository(),
            new FakeInfluxWriteService()),
            inbox);
        var (channel, recorder) = RecordingDispatchProxy<IModel>.CreateProxy();
        var envelope = EnvelopeFactory.Create();
        var body = Encoding.UTF8.GetBytes(
            $$"""{"schemaVersion":"1.0","eventId":"{{envelope.EventId}}","correlationId":"{{envelope.CorrelationId}}","producer":"{{envelope.Producer}}","eventType":"{{envelope.EventType}}","areaId":"{{envelope.AreaId}}","eventTime":"{{envelope.EventTime:O}}","payload":null}""");

        await InvokeHandleReceivedAsync(
            worker,
            channel,
            CreateEventArgs(body, 17),
            CancellationToken.None);

        Assert.Single(recorder.Invocations, x => x.MethodName == "BasicAck");
        Assert.DoesNotContain(recorder.Invocations, x => x.MethodName == "BasicNack");
        Assert.Empty(inbox.Events);
        Assert.Single(inbox.Rejections);
        Assert.Equal("missing_payload", inbox.Rejections.Single().RejectionCode);
    }

    [Fact]
    public async Task HandleReceivedAsync_AcknowledgesMissingPayload_AndStoresRejection()
    {
        var inbox = new InMemoryReadingEventInbox();
        var worker = CreateWorker(CreatePipeline(
            new InMemoryAcceptedReadingRepository(),
            new InMemoryRiskAssessmentRepository(),
            new InMemoryAreaRiskSnapshotRepository(),
            new FakeInfluxWriteService()),
            inbox);
        var (channel, recorder) = RecordingDispatchProxy<IModel>.CreateProxy();
        var envelope = EnvelopeFactory.Create();
        var body = Encoding.UTF8.GetBytes(
            $$"""{"schemaVersion":"1.0","eventId":"{{envelope.EventId}}","correlationId":"{{envelope.CorrelationId}}","producer":"{{envelope.Producer}}","eventType":"{{envelope.EventType}}","areaId":"{{envelope.AreaId}}","eventTime":"{{envelope.EventTime:O}}"}""");

        await InvokeHandleReceivedAsync(
            worker,
            channel,
            CreateEventArgs(body, 18),
            CancellationToken.None);

        Assert.Single(recorder.Invocations, x => x.MethodName == "BasicAck");
        Assert.DoesNotContain(recorder.Invocations, x => x.MethodName == "BasicNack");
        Assert.Empty(inbox.Events);
        Assert.Single(inbox.Rejections);
        Assert.Equal("missing_payload", inbox.Rejections.Single().RejectionCode);
    }

    [Fact]
    public async Task HandleReceivedAsync_AcknowledgesInvalidJson_AndStoresRejection()
    {
        var inbox = new InMemoryReadingEventInbox();
        var worker = CreateWorker(CreatePipeline(
            new InMemoryAcceptedReadingRepository(),
            new InMemoryRiskAssessmentRepository(),
            new InMemoryAreaRiskSnapshotRepository(),
            new FakeInfluxWriteService()),
            inbox);
        var (channel, recorder) = RecordingDispatchProxy<IModel>.CreateProxy();

        await InvokeHandleReceivedAsync(
            worker,
            channel,
            CreateEventArgs(Encoding.UTF8.GetBytes("{ invalid"), 13),
            CancellationToken.None);

        var ack = Assert.Single(recorder.Invocations, x => x.MethodName == "BasicAck");
        Assert.Equal(13UL, Assert.IsType<ulong>(ack.Arguments[0]));
        Assert.DoesNotContain(recorder.Invocations, x => x.MethodName == "BasicNack");
        Assert.Single(inbox.Rejections);
        Assert.Equal("invalid_json", inbox.Rejections.Single().RejectionCode);
    }

    [Fact]
    public async Task HandleReceivedAsync_AcknowledgesSemanticInvalidEnvelope_AndRejectsBeforeInbox()
    {
        var inbox = new InMemoryReadingEventInbox();
        var worker = CreateWorker(CreatePipeline(
            new InMemoryAcceptedReadingRepository(),
            new InMemoryRiskAssessmentRepository(),
            new InMemoryAreaRiskSnapshotRepository(),
            new FakeInfluxWriteService()),
            inbox);
        var (channel, recorder) = RecordingDispatchProxy<IModel>.CreateProxy();
        var envelope = EnvelopeFactory.Create(operationalState: SensorOperationalState.Invalid);

        await InvokeHandleReceivedAsync(
            worker,
            channel,
            CreateEventArgs(JsonEventSerializer.SerializeToUtf8Bytes(envelope), 15),
            CancellationToken.None);

        var ack = Assert.Single(recorder.Invocations, x => x.MethodName == "BasicAck");
        Assert.Equal(15UL, Assert.IsType<ulong>(ack.Arguments[0]));
        Assert.DoesNotContain(recorder.Invocations, x => x.MethodName == "BasicNack");
        Assert.Empty(inbox.Events);
        Assert.Single(inbox.Rejections);
        Assert.Equal("invalid_operational_state", inbox.Rejections.Single().RejectionCode);
    }

    [Fact]
    public async Task HandleReceivedAsync_AcknowledgesUndefinedMetricType_AndRejectsBeforeInbox()
    {
        var inbox = new InMemoryReadingEventInbox();
        var worker = CreateWorker(CreatePipeline(
            new InMemoryAcceptedReadingRepository(),
            new InMemoryRiskAssessmentRepository(),
            new InMemoryAreaRiskSnapshotRepository(),
            new FakeInfluxWriteService()),
            inbox);
        var (channel, recorder) = RecordingDispatchProxy<IModel>.CreateProxy();
        var envelope = EnvelopeFactory.Create(metricType: (SensorMetricType)999);

        await InvokeHandleReceivedAsync(
            worker,
            channel,
            CreateEventArgs(JsonEventSerializer.SerializeToUtf8Bytes(envelope), 19),
            CancellationToken.None);

        var ack = Assert.Single(recorder.Invocations, x => x.MethodName == "BasicAck");
        Assert.Equal(19UL, Assert.IsType<ulong>(ack.Arguments[0]));
        Assert.DoesNotContain(recorder.Invocations, x => x.MethodName == "BasicNack");
        Assert.Empty(inbox.Events);
        Assert.Single(inbox.Rejections);
        Assert.Equal("invalid_metric_type", inbox.Rejections.Single().RejectionCode);
    }

    [Fact]
    public async Task HandleReceivedAsync_AcknowledgesUndefinedMeasurementUnit_AndRejectsBeforeInbox()
    {
        var inbox = new InMemoryReadingEventInbox();
        var worker = CreateWorker(CreatePipeline(
            new InMemoryAcceptedReadingRepository(),
            new InMemoryRiskAssessmentRepository(),
            new InMemoryAreaRiskSnapshotRepository(),
            new FakeInfluxWriteService()),
            inbox);
        var (channel, recorder) = RecordingDispatchProxy<IModel>.CreateProxy();
        var envelope = EnvelopeFactory.Create(unit: (MeasurementUnit)999);

        await InvokeHandleReceivedAsync(
            worker,
            channel,
            CreateEventArgs(JsonEventSerializer.SerializeToUtf8Bytes(envelope), 20),
            CancellationToken.None);

        var ack = Assert.Single(recorder.Invocations, x => x.MethodName == "BasicAck");
        Assert.Equal(20UL, Assert.IsType<ulong>(ack.Arguments[0]));
        Assert.DoesNotContain(recorder.Invocations, x => x.MethodName == "BasicNack");
        Assert.Empty(inbox.Events);
        Assert.Single(inbox.Rejections);
        Assert.Equal("invalid_measurement_unit", inbox.Rejections.Single().RejectionCode);
    }

    [Fact]
    public async Task HandleReceivedAsync_AcknowledgesUnsupportedEventType_AndRejectsBeforeInbox()
    {
        var inbox = new InMemoryReadingEventInbox();
        var worker = CreateWorker(CreatePipeline(
            new InMemoryAcceptedReadingRepository(),
            new InMemoryRiskAssessmentRepository(),
            new InMemoryAreaRiskSnapshotRepository(),
            new FakeInfluxWriteService()),
            inbox);
        var (channel, recorder) = RecordingDispatchProxy<IModel>.CreateProxy();
        var envelope = EnvelopeFactory.Create() with { EventType = EventTypes.ReadingAccepted };

        await InvokeHandleReceivedAsync(
            worker,
            channel,
            CreateEventArgs(JsonEventSerializer.SerializeToUtf8Bytes(envelope), 16),
            CancellationToken.None);

        var ack = Assert.Single(recorder.Invocations, x => x.MethodName == "BasicAck");
        Assert.Equal(16UL, Assert.IsType<ulong>(ack.Arguments[0]));
        Assert.DoesNotContain(recorder.Invocations, x => x.MethodName == "BasicNack");
        Assert.Empty(inbox.Events);
        Assert.Single(inbox.Rejections);
        Assert.Equal("unsupported_event_type", inbox.Rejections.Single().RejectionCode);
    }

    [Fact]
    public async Task HandleReceivedAsync_AcknowledgesAndSchedulesRetry_WhenTransientFailureHappensAfterInboxCommit()
    {
        var inbox = new InMemoryReadingEventInbox();
        var worker = CreateWorker(CreatePipeline(
            new TimeoutThrowingAcceptedReadingRepository(),
            new InMemoryRiskAssessmentRepository(),
            new InMemoryAreaRiskSnapshotRepository(),
            new FakeInfluxWriteService()),
            inbox);
        var (channel, recorder) = RecordingDispatchProxy<IModel>.CreateProxy();
        var envelope = EnvelopeFactory.Create();

        await InvokeHandleReceivedAsync(
            worker,
            channel,
            CreateEventArgs(JsonEventSerializer.SerializeToUtf8Bytes(envelope), 14),
            CancellationToken.None);

        Assert.Single(recorder.Invocations, x => x.MethodName == "BasicAck");
        Assert.DoesNotContain(recorder.Invocations, x => x.MethodName == "BasicNack");
        var storedEvent = Assert.Single(inbox.Events);
        Assert.Equal(NatureProtector.Infrastructure.Postgres.Pipeline.InboxEventStatus.RetryPending, storedEvent.Status);
        Assert.Equal("timeout", storedEvent.LastErrorCode);
        Assert.Single(inbox.Attempts);
        Assert.Equal(
            NatureProtector.Infrastructure.Postgres.Pipeline.ProcessingAttemptOutcome.RetryScheduled,
            inbox.Attempts.Single().Outcome);
        Assert.Empty(inbox.Quarantines);
    }

    [Fact]
    public async Task HandleReceivedAsync_AcknowledgesDuplicateEvent_WithoutReprocessing()
    {
        var acceptedReadingRepository = new InMemoryAcceptedReadingRepository();
        var riskAssessmentRepository = new InMemoryRiskAssessmentRepository();
        var areaRiskSnapshotRepository = new InMemoryAreaRiskSnapshotRepository();
        var influxWriteService = new FakeInfluxWriteService();
        var inbox = new InMemoryReadingEventInbox();
        var worker = CreateWorker(CreatePipeline(
            acceptedReadingRepository,
            riskAssessmentRepository,
            areaRiskSnapshotRepository,
            influxWriteService),
            inbox);
        var (channel, recorder) = RecordingDispatchProxy<IModel>.CreateProxy();
        var eventId = Guid.NewGuid();
        var envelope = EnvelopeFactory.Create(eventId: eventId);
        var payload = JsonEventSerializer.SerializeToUtf8Bytes(envelope);

        await InvokeHandleReceivedAsync(worker, channel, CreateEventArgs(payload, 21), CancellationToken.None);
        await InvokeHandleReceivedAsync(worker, channel, CreateEventArgs(payload, 22), CancellationToken.None);

        Assert.Equal(2, recorder.Invocations.Count(x => x.MethodName == "BasicAck"));
        Assert.DoesNotContain(recorder.Invocations, x => x.MethodName == "BasicNack");
        Assert.Single(await acceptedReadingRepository.GetAllAsync(CancellationToken.None));
        Assert.Single(inbox.Events);
        Assert.Single(inbox.Attempts);
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

        var exchangeDeclare = Assert.Single(recorder.Invocations, x => x.MethodName == "ExchangeDeclare");
        Assert.Equal(NatureProtectorRabbitMqTopology.ExchangeName, Assert.IsType<string>(exchangeDeclare.Arguments[0]));
        Assert.Equal(NatureProtectorRabbitMqTopology.ExchangeType, Assert.IsType<string>(exchangeDeclare.Arguments[1]));

        var queueDeclares = recorder.Invocations.Where(x => x.MethodName == "QueueDeclare").ToList();
        Assert.Equal(2, queueDeclares.Count);
        Assert.Equal(NatureProtectorRabbitMqTopology.Bindings.Count(), recorder.Invocations.Count(x => x.MethodName == "QueueBind"));
    }

    private static PreventionWorker CreateWorker(ReadingRiskPipeline pipeline, IReadingEventInbox inbox)
    {
        var preventionOptions = Options.Create(new PreventionHostOptions
        {
            PipelinePersistenceEnabled = false,
            ConsumerPrefetchCount = 1,
            MaxProcessingAttempts = 3,
            RetryDelaySeconds = [0, 0],
            RetryPollingIntervalSeconds = 1
        });
        var processingService = new ReadingEventProcessingService(
            NullLogger<ReadingEventProcessingService>.Instance,
            preventionOptions,
            pipeline,
            inbox,
            new PassThroughReadingSemanticValidator(),
            new DefaultProcessingFailureClassifier());

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
            preventionOptions,
            inbox,
            processingService);
    }

    private static ReadingRiskPipeline CreatePipeline(
        IAcceptedReadingRepository acceptedReadingRepository,
        IRiskAssessmentRepository riskAssessmentRepository,
        IAreaRiskSnapshotRepository areaRiskSnapshotRepository,
        FakeInfluxWriteService influxWriteService)
    {
        return new ReadingRiskPipeline(
            acceptedReadingRepository,
            new RiskEligibilityService(),
            new InMemoryDailyCellStateRepository(),
            new SimpleRiskScoringService(),
            riskAssessmentRepository,
            new AreaRiskSnapshotService(),
            areaRiskSnapshotRepository,
            new InMemoryAreaOperationalProjectionStore(),
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

    private sealed class TimeoutThrowingAcceptedReadingRepository : IAcceptedReadingRepository
    {
        public Task AddAsync(
            EventEnvelope<SensorReadingProducedPayload> envelope,
            CancellationToken cancellationToken)
        {
            throw new TimeoutException("boom");
        }

        public Task<IReadOnlyCollection<EventEnvelope<SensorReadingProducedPayload>>> GetAllAsync(
            CancellationToken cancellationToken)
        {
            return Task.FromResult<IReadOnlyCollection<EventEnvelope<SensorReadingProducedPayload>>>([]);
        }
    }
}
