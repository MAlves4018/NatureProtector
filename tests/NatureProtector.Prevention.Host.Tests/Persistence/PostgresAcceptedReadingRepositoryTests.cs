using Microsoft.Extensions.Logging.Abstractions;
using NatureProtector.Prevention.Host.Persistence;
using NatureProtector.Prevention.Host.Tests.TestData;
using NatureProtector.Prevention.Host.Tests.TestInfrastructure;

namespace NatureProtector.Prevention.Host.Tests.Persistence;

public sealed class PostgresAcceptedReadingRepositoryTests
{
    [Fact]
    public async Task AddAsync_NewEnvelope_PersistsExpectedReadingFields()
    {
        await using var scope = new SqliteControlDbContextScope();
        var seed = await ControlPlaneSeedData.SeedAreaWithSensorAsync(scope);
        var repository = CreateRepository(scope);
        var eventTime = new DateTimeOffset(2026, 4, 10, 8, 30, 0, TimeSpan.Zero);
        var envelope = EnvelopeFactory.Create(
            areaId: seed.AreaId,
            sensorId: seed.SensorId,
            sensorName: "Weather-01",
            value: 34.2,
            eventTime: eventTime);

        await repository.AddAsync(envelope, CancellationToken.None);

        await using var dbContext = scope.CreateDbContext();
        var row = Assert.Single(dbContext.AcceptedReadingLogs);
        Assert.Equal(envelope.EventId, row.EventId);
        Assert.Equal(seed.AreaId, row.AreaId);
        Assert.Equal(seed.SensorId, row.SensorId);
        Assert.Equal("Temperature", row.MetricType);
        Assert.Equal("Celsius", row.MeasurementUnit);
        Assert.Equal("Nominal", row.OperationalState);
        Assert.Equal(34.2, row.Value);
        Assert.Equal(eventTime, row.EventTime);
        Assert.Equal(envelope.CorrelationId, row.CorrelationId);
    }

    [Fact]
    public async Task AddAsync_DuplicateEventId_IgnoresSecondWrite()
    {
        await using var scope = new SqliteControlDbContextScope();
        var seed = await ControlPlaneSeedData.SeedAreaWithSensorAsync(scope);
        var repository = CreateRepository(scope);
        var eventId = Guid.NewGuid();

        await repository.AddAsync(
            EnvelopeFactory.Create(areaId: seed.AreaId, sensorId: seed.SensorId, eventId: eventId, value: 20.0),
            CancellationToken.None);

        await repository.AddAsync(
            EnvelopeFactory.Create(areaId: seed.AreaId, sensorId: seed.SensorId, eventId: eventId, value: 99.0),
            CancellationToken.None);

        await using var dbContext = scope.CreateDbContext();
        var row = Assert.Single(dbContext.AcceptedReadingLogs);
        Assert.Equal(20.0, row.Value);
    }

    [Fact]
    public async Task GetAllAsync_MultipleRows_ReturnsReadingsOrderedByEventTime()
    {
        await using var scope = new SqliteControlDbContextScope();
        var seed = await ControlPlaneSeedData.SeedAreaWithSensorAsync(scope);
        var repository = CreateRepository(scope);
        var first = EnvelopeFactory.Create(
            areaId: seed.AreaId,
            sensorId: seed.SensorId,
            eventId: Guid.Parse("10000000-0000-0000-0000-000000000001"),
            value: 22.0,
            eventTime: new DateTimeOffset(2026, 4, 10, 8, 0, 0, TimeSpan.Zero));
        var second = EnvelopeFactory.Create(
            areaId: seed.AreaId,
            sensorId: seed.SensorId,
            eventId: Guid.Parse("20000000-0000-0000-0000-000000000001"),
            value: 25.5,
            eventTime: new DateTimeOffset(2026, 4, 10, 9, 0, 0, TimeSpan.Zero));

        await repository.AddAsync(second, CancellationToken.None);
        await repository.AddAsync(first, CancellationToken.None);

        var all = await repository.GetAllAsync(CancellationToken.None);

        Assert.Equal(
            [first.EventId, second.EventId],
            all.Select(item => item.EventId));
        Assert.Equal(
            [22.0, 25.5],
            all.Select(item => item.Payload.Value));
    }

    private static PostgresAcceptedReadingRepository CreateRepository(SqliteControlDbContextScope scope)
    {
        return new PostgresAcceptedReadingRepository(
            scope.Factory,
            NullLogger<PostgresAcceptedReadingRepository>.Instance);
    }
}
