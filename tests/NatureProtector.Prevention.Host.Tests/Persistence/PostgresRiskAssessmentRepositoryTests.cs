using NatureProtector.Core.Primitives;
using NatureProtector.Core.Risk;
using NatureProtector.Infrastructure.Postgres.Projection;
using NatureProtector.Prevention.Host.Persistence;
using NatureProtector.Prevention.Host.Tests.TestInfrastructure;
using Microsoft.Extensions.Logging.Abstractions;

namespace NatureProtector.Prevention.Host.Tests.Persistence;

public sealed class PostgresRiskAssessmentRepositoryTests
{
    [Fact]
    public async Task AddAsync_NewAssessment_PersistsAssessmentAndGridCell()
    {
        await using var scope = new SqliteControlDbContextScope();
        var seed = await ControlPlaneSeedData.SeedAreaWithSensorAsync(scope);
        var repository = CreateRepository(scope);
        var sourceEventId = Guid.NewGuid();
        var assessment = new RiskAssessment(
            Guid.Parse("30000000-0000-0000-0000-000000000001"),
            new DateTimeOffset(2026, 4, 10, 8, 0, 0, TimeSpan.Zero),
            0.74,
            "High risk");

        await repository.AddAsync(seed.AreaId, seed.SensorId, sourceEventId, assessment, CancellationToken.None);

        await using var dbContext = scope.CreateDbContext();
        var row = Assert.Single(dbContext.RiskAssessmentLogs);
        Assert.Equal(seed.AreaId, row.AreaId);
        Assert.Equal(seed.SensorId, row.SensorId);
        Assert.Equal(seed.GridCellId, row.GridCellId);
        Assert.Equal(sourceEventId, row.SourceEventId);
        Assert.Equal("VeryHigh", row.RiskLevel);
    }

    [Fact]
    public async Task AddAsync_DuplicateSourceEventId_IgnoresSecondWrite()
    {
        await using var scope = new SqliteControlDbContextScope();
        var seed = await ControlPlaneSeedData.SeedAreaWithSensorAsync(scope);
        var repository = CreateRepository(scope);
        var sourceEventId = Guid.NewGuid();

        await repository.AddAsync(
            seed.AreaId,
            seed.SensorId,
            sourceEventId,
            new RiskAssessment(Guid.NewGuid(), new DateTimeOffset(2026, 4, 10, 8, 0, 0, TimeSpan.Zero), 0.40, "First"),
            CancellationToken.None);

        await repository.AddAsync(
            seed.AreaId,
            seed.SensorId,
            sourceEventId,
            new RiskAssessment(Guid.NewGuid(), new DateTimeOffset(2026, 4, 10, 9, 0, 0, TimeSpan.Zero), 0.90, "Second"),
            CancellationToken.None);

        await using var dbContext = scope.CreateDbContext();
        var row = Assert.Single(dbContext.RiskAssessmentLogs);
        Assert.Equal(0.40, row.RiskScore);
    }

    [Fact]
    public async Task GetByAreaAsync_MultipleAssessments_ReturnsOrderedHistory()
    {
        await using var scope = new SqliteControlDbContextScope();
        var seed = await ControlPlaneSeedData.SeedAreaWithSensorAsync(scope);
        var repository = CreateRepository(scope);

        await repository.AddAsync(
            seed.AreaId,
            seed.SensorId,
            Guid.NewGuid(),
            new RiskAssessment(Guid.NewGuid(), new DateTimeOffset(2026, 4, 10, 9, 0, 0, TimeSpan.Zero), 0.65, "Second"),
            CancellationToken.None);

        await repository.AddAsync(
            seed.AreaId,
            seed.SensorId,
            Guid.NewGuid(),
            new RiskAssessment(Guid.NewGuid(), new DateTimeOffset(2026, 4, 10, 8, 0, 0, TimeSpan.Zero), 0.35, "First"),
            CancellationToken.None);

        var history = await repository.GetByAreaAsync(seed.AreaId, CancellationToken.None);

        Assert.Equal(
            [0.35, 0.65],
            history.Select(item => item.RiskScore));
    }

    [Fact]
    public async Task GetLatestByAreaAsync_MultipleSensors_ReturnsLatestPerSensor()
    {
        await using var scope = new SqliteControlDbContextScope();
        var seedA = await ControlPlaneSeedData.SeedAreaWithSensorAsync(scope);
        var sensorBId = Guid.NewGuid();
        var gridCellBId = Guid.NewGuid();
        await scope.SeedAsync(dbContext =>
        {
            dbContext.GridCells.Add(new Infrastructure.Postgres.Control.GridCellRecord
            {
                Id = gridCellBId,
                AreaId = seedA.AreaId,
                ConfigurationVersionId = seedA.ConfigurationVersionId,
                CellCode = "CELL-002",
                CentroidLatitude = 39.76,
                CentroidLongitude = -7.91
            });

            dbContext.SensorNodes.Add(new Infrastructure.Postgres.Control.SensorNodeRecord
            {
                Id = sensorBId,
                AreaId = seedA.AreaId,
                GridCellId = gridCellBId,
                ProfileId = seedA.SensorProfileId,
                ConfigurationVersionId = seedA.ConfigurationVersionId,
                Name = "Sensor-02",
                Type = Core.Sensors.SensorType.WeatherStation,
                Latitude = 39.76,
                Longitude = -7.91,
                IsActive = true
            });

            return Task.CompletedTask;
        });
        var repository = CreateRepository(scope);

        await repository.AddAsync(
            seedA.AreaId,
            seedA.SensorId,
            Guid.NewGuid(),
            new RiskAssessment(Guid.NewGuid(), new DateTimeOffset(2026, 4, 10, 8, 0, 0, TimeSpan.Zero), 0.20, "Old A"),
            CancellationToken.None);
        await repository.AddAsync(
            seedA.AreaId,
            seedA.SensorId,
            Guid.NewGuid(),
            new RiskAssessment(Guid.NewGuid(), new DateTimeOffset(2026, 4, 10, 9, 0, 0, TimeSpan.Zero), 0.55, "Latest A"),
            CancellationToken.None);
        await repository.AddAsync(
            seedA.AreaId,
            sensorBId,
            Guid.NewGuid(),
            new RiskAssessment(Guid.NewGuid(), new DateTimeOffset(2026, 4, 10, 8, 30, 0, TimeSpan.Zero), 0.85, "Latest B"),
            CancellationToken.None);

        var latest = await repository.GetLatestByAreaAsync(seedA.AreaId, CancellationToken.None);

        Assert.Equal(2, latest.Count);
        Assert.Equal(
            [0.85, 0.55],
            latest.Select(item => item.RiskScore));
    }

    [Fact]
    public void SelectLatestAssessments_Throws_WhenRowsIsNull()
    {
        var ex = Assert.Throws<ArgumentNullException>(() =>
            PostgresRiskAssessmentRepository.SelectLatestAssessments(null!));

        Assert.Equal("rows", ex.ParamName);
    }

    [Fact]
    public void SelectLatestAssessments_ReturnsLatestPerSensor_UsingTimestampThenCreatedAt()
    {
        var sensorA = Guid.Parse("00000000-0000-0000-0000-000000000001");
        var sensorB = Guid.Parse("00000000-0000-0000-0000-000000000002");
        var chosenForSensorA = CreateRow(
            id: Guid.Parse("10000000-0000-0000-0000-000000000002"),
            sensorId: sensorA,
            timestamp: new DateTimeOffset(2026, 4, 11, 11, 0, 0, TimeSpan.Zero),
            createdAt: new DateTimeOffset(2026, 4, 11, 11, 0, 10, TimeSpan.Zero),
            score: 0.75);
        var result = PostgresRiskAssessmentRepository.SelectLatestAssessments([
            CreateRow(
                id: Guid.Parse("10000000-0000-0000-0000-000000000001"),
                sensorId: sensorA,
                timestamp: new DateTimeOffset(2026, 4, 11, 11, 0, 0, TimeSpan.Zero),
                createdAt: new DateTimeOffset(2026, 4, 11, 11, 0, 5, TimeSpan.Zero),
                score: 0.25),
            CreateRow(
                id: Guid.Parse("20000000-0000-0000-0000-000000000001"),
                sensorId: sensorB,
                timestamp: new DateTimeOffset(2026, 4, 11, 10, 59, 0, TimeSpan.Zero),
                createdAt: new DateTimeOffset(2026, 4, 11, 10, 59, 2, TimeSpan.Zero),
                score: 0.60),
            chosenForSensorA
        ]);

        Assert.Equal(2, result.Count);
        Assert.Equal(
            [
                Guid.Parse("20000000-0000-0000-0000-000000000001"),
                chosenForSensorA.Id
            ],
            result.Select(assessment => assessment.Id));

        var latestForSensorA = Assert.Single(result, assessment => assessment.Id == chosenForSensorA.Id);
        Assert.Equal(RiskLevel.VeryHigh, latestForSensorA.RiskLevel);
    }

    private static PostgresRiskAssessmentRepository CreateRepository(SqliteControlDbContextScope scope)
    {
        return new PostgresRiskAssessmentRepository(
            scope.Factory,
            NullLogger<PostgresRiskAssessmentRepository>.Instance);
    }

    private static RiskAssessmentLogRecord CreateRow(
        Guid id,
        Guid sensorId,
        DateTimeOffset timestamp,
        DateTimeOffset createdAt,
        double score)
    {
        return new RiskAssessmentLogRecord
        {
            Id = id,
            AreaId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa"),
            SensorId = sensorId,
            SourceEventId = Guid.NewGuid(),
            Timestamp = timestamp,
            RiskScore = score,
            RiskLevel = string.Empty,
            CreatedAt = createdAt
        };
    }
}
