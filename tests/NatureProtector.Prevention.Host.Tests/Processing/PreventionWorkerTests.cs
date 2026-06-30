using System.Reflection;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Text.Json.Nodes;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NatureProtector.Prevention.Host.Configuration;
using NatureProtector.Prevention.Host.Persistence;
using NatureProtector.Prevention.Host.Projection;
using NatureProtector.Prevention.Host.Processing;
using NatureProtector.Prevention.Host.Runtime;
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
    public async Task HandleReceivedAsync_AcknowledgesPreviousV1FixtureWithoutOptionalIngestTime()
    {
        var body = CreateContractFixtureBytes();

        var (inbox, recorder) = await InvokeWorkerWithBodyAsync(body, 23);

        AssertAckedWithoutNack(recorder, 23);
        Assert.Single(inbox.Events);
        Assert.Empty(inbox.Rejections);
    }

    [Fact]
    public async Task HandleReceivedAsync_AcknowledgesForwardCompatibleUnknownOptionalFields()
    {
        var body = CreateContractFixtureBytes(root =>
        {
            root["producerBuild"] = "future-field";
            GetPayloadObject(root)["futurePayloadField"] = "ignored";
        });

        var (inbox, recorder) = await InvokeWorkerWithBodyAsync(body, 24);

        AssertAckedWithoutNack(recorder, 24);
        Assert.Single(inbox.Events);
        Assert.Empty(inbox.Rejections);
    }

    [Theory]
    [InlineData(ContractFixtureMutation.UnsupportedSchemaVersion, "unsupported_schema_version")]
    [InlineData(ContractFixtureMutation.MissingEventId, "invalid_event_id")]
    [InlineData(ContractFixtureMutation.MissingCorrelationId, "missing_correlation_id")]
    [InlineData(ContractFixtureMutation.NullProducer, "missing_producer")]
    [InlineData(ContractFixtureMutation.UnknownEventType, "unsupported_event_type")]
    [InlineData(ContractFixtureMutation.MissingEventTime, "invalid_event_time")]
    [InlineData(ContractFixtureMutation.MissingSimulationRunId, "missing_simulation_run_id")]
    [InlineData(ContractFixtureMutation.MissingSensorId, "missing_sensor_id")]
    [InlineData(ContractFixtureMutation.NullSensorName, "missing_sensor_name")]
    [InlineData(ContractFixtureMutation.UnknownStringMetricType, "invalid_json")]
    public async Task HandleReceivedAsync_AcknowledgesInvalidVersionedContractFixtures_AndStoresRejection(
        ContractFixtureMutation mutation,
        string expectedRejectionCode)
    {
        var body = CreateContractFixtureBytes(root => ApplyContractMutation(root, mutation));

        var (inbox, recorder) = await InvokeWorkerWithBodyAsync(body, 25);

        AssertAckedWithoutNack(recorder, 25);
        Assert.Empty(inbox.Events);
        Assert.Single(inbox.Rejections);
        Assert.Equal(expectedRejectionCode, inbox.Rejections.Single().RejectionCode);
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
        var options = new RabbitMqOptions
        {
            ExchangeName = "np.it.events",
            IngestionReadingsQueueName = "np.it.ingestion",
            ObservabilityRawQueueName = "np.it.raw"
        };

        method.Invoke(null, [channel, options]);

        var exchangeDeclare = Assert.Single(recorder.Invocations, x => x.MethodName == "ExchangeDeclare");
        Assert.Equal("np.it.events", Assert.IsType<string>(exchangeDeclare.Arguments[0]));
        Assert.Equal(NatureProtectorRabbitMqTopology.ExchangeType, Assert.IsType<string>(exchangeDeclare.Arguments[1]));

        var queueDeclares = recorder.Invocations.Where(x => x.MethodName == "QueueDeclare").ToList();
        Assert.Equal(2, queueDeclares.Count);
        Assert.Contains(queueDeclares, x => Equals(x.Arguments[0], "np.it.ingestion"));
        Assert.Contains(queueDeclares, x => Equals(x.Arguments[0], "np.it.raw"));
        Assert.Equal(2, recorder.Invocations.Count(x => x.MethodName == "QueueBind"));
    }

    [Fact]
    public void CreateConnectionFactory_ConfiguresTlsWithPrivateCertificateAuthority()
    {
        using var root = CreateCertificateAuthority("NatureProtector RabbitMQ Test Root");
        var caPath = WriteCertificateAuthorityPem(root);
        try
        {
            var options = new RabbitMqOptions
            {
                HostName = "rabbitmq.staging.natureprotector.internal",
                Port = 5671,
                UserName = "np_app",
                Password = "not-a-real-secret",
                TlsEnabled = true,
                TlsServerName = "rabbitmq.staging.natureprotector.internal",
                TlsCertificateAuthorityPath = caPath
            };

            var factory = PreventionWorker.CreateConnectionFactory(options);

            Assert.True(factory.Ssl.Enabled);
            Assert.Equal("rabbitmq.staging.natureprotector.internal", factory.Ssl.ServerName);
            Assert.NotNull(factory.Ssl.CertificateValidationCallback);
            Assert.True(factory.DispatchConsumersAsync);
        }
        finally
        {
            File.Delete(caPath);
        }
    }

    [Theory]
    [InlineData(null, "ca.pem", "RabbitMQ TlsServerName is required when TLS is enabled.")]
    [InlineData("rabbitmq.staging.natureprotector.internal", null, "RabbitMQ TlsCertificateAuthorityPath is required when TLS is enabled.")]
    public void CreateConnectionFactory_RejectsIncompleteTlsConfiguration(
        string? tlsServerName,
        string? tlsCertificateAuthorityPath,
        string expectedMessage)
    {
        var options = new RabbitMqOptions
        {
            HostName = "rabbitmq.staging.natureprotector.internal",
            Port = 5671,
            UserName = "np_app",
            Password = "not-a-real-secret",
            TlsEnabled = true,
            TlsServerName = tlsServerName,
            TlsCertificateAuthorityPath = tlsCertificateAuthorityPath
        };

        var exception = Assert.Throws<InvalidOperationException>(() =>
            PreventionWorker.CreateConnectionFactory(options));

        Assert.Equal(expectedMessage, exception.Message);
    }

    private static async Task<(InMemoryReadingEventInbox Inbox, RecordingDispatchProxy<IModel> Recorder)> InvokeWorkerWithBodyAsync(
        byte[] body,
        ulong deliveryTag)
    {
        var inbox = new InMemoryReadingEventInbox();
        var worker = CreateWorker(inbox);
        var (channel, recorder) = RecordingDispatchProxy<IModel>.CreateProxy();

        await InvokeHandleReceivedAsync(
            worker,
            channel,
            CreateEventArgs(body, deliveryTag),
            CancellationToken.None);

        return (inbox, recorder);
    }

    private static void AssertAckedWithoutNack(RecordingDispatchProxy<IModel> recorder, ulong deliveryTag)
    {
        var ack = Assert.Single(recorder.Invocations, x => x.MethodName == "BasicAck");
        Assert.Equal(deliveryTag, Assert.IsType<ulong>(ack.Arguments[0]));
        Assert.DoesNotContain(recorder.Invocations, x => x.MethodName == "BasicNack");
    }

    private static byte[] CreateContractFixtureBytes(Action<JsonObject>? mutate = null)
    {
        var envelope = EnvelopeFactory.Create(
            areaId: Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb"),
            eventId: Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa"),
            simulationRunId: Guid.Parse("cccccccc-cccc-cccc-cccc-cccccccccccc"),
            sensorId: Guid.Parse("dddddddd-dddd-dddd-dddd-dddddddddddd"));
        var root = JsonNode.Parse(JsonEventSerializer.SerializeToString(envelope))?.AsObject()
            ?? throw new InvalidOperationException("Contract fixture JSON could not be parsed.");

        mutate?.Invoke(root);

        return Encoding.UTF8.GetBytes(root.ToJsonString());
    }

    private static X509Certificate2 CreateCertificateAuthority(string commonName)
    {
        using var rsa = RSA.Create(2048);
        var request = new CertificateRequest(
            $"CN={commonName}",
            rsa,
            HashAlgorithmName.SHA256,
            RSASignaturePadding.Pkcs1);
        request.CertificateExtensions.Add(
            new X509BasicConstraintsExtension(
                certificateAuthority: true,
                hasPathLengthConstraint: false,
                pathLengthConstraint: 0,
                critical: true));
        request.CertificateExtensions.Add(
            new X509KeyUsageExtension(X509KeyUsageFlags.KeyCertSign, critical: true));

        var certificate = request.CreateSelfSigned(
            DateTimeOffset.UtcNow.AddDays(-1),
            DateTimeOffset.UtcNow.AddDays(1));
        return new X509Certificate2(certificate.Export(X509ContentType.Pfx));
    }

    private static string WriteCertificateAuthorityPem(X509Certificate2 certificate)
    {
        var path = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}.crt");
        File.WriteAllText(
            path,
            new string(PemEncoding.Write("CERTIFICATE", certificate.RawData)),
            Encoding.ASCII);
        return path;
    }

    private static void ApplyContractMutation(JsonObject root, ContractFixtureMutation mutation)
    {
        var payload = GetPayloadObject(root);

        switch (mutation)
        {
            case ContractFixtureMutation.UnsupportedSchemaVersion:
                root["schemaVersion"] = "2.0";
                break;
            case ContractFixtureMutation.MissingEventId:
                root.Remove("eventId");
                break;
            case ContractFixtureMutation.MissingCorrelationId:
                root.Remove("correlationId");
                break;
            case ContractFixtureMutation.NullProducer:
                root["producer"] = null;
                break;
            case ContractFixtureMutation.UnknownEventType:
                root["eventType"] = "UnknownFutureEvent";
                break;
            case ContractFixtureMutation.MissingEventTime:
                root.Remove("eventTime");
                break;
            case ContractFixtureMutation.MissingSimulationRunId:
                payload.Remove("simulationRunId");
                break;
            case ContractFixtureMutation.MissingSensorId:
                payload.Remove("sensorId");
                break;
            case ContractFixtureMutation.NullSensorName:
                payload["sensorName"] = null;
                break;
            case ContractFixtureMutation.UnknownStringMetricType:
                payload["metricType"] = "UnknownFutureMetric";
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(mutation), mutation, null);
        }
    }

    private static JsonObject GetPayloadObject(JsonObject root)
    {
        return root["payload"]?.AsObject()
            ?? throw new InvalidOperationException("Contract fixture payload is missing.");
    }

    private static PreventionWorker CreateWorker(IReadingEventInbox inbox)
    {
        return CreateWorker(CreatePipeline(
            new InMemoryAcceptedReadingRepository(),
            new InMemoryRiskAssessmentRepository(),
            new InMemoryAreaRiskSnapshotRepository(),
            new FakeInfluxWriteService()),
            inbox);
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
            new NoOpProcessingFaultInjector(),
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
            processingService,
            new PreventionRuntimeState());
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

    public enum ContractFixtureMutation
    {
        UnsupportedSchemaVersion,
        MissingEventId,
        MissingCorrelationId,
        NullProducer,
        UnknownEventType,
        MissingEventTime,
        MissingSimulationRunId,
        MissingSensorId,
        NullSensorName,
        UnknownStringMetricType
    }
}
