using NatureProtector.Core.Primitives;
using NatureProtector.Core.Risk;
using NatureProtector.Infrastructure.Postgres.Projection;
using NatureProtector.Prevention.Host.Persistence;

namespace NatureProtector.Prevention.Host.Tests.Persistence;

public sealed class PostgresRiskAssessmentRepositoryTests
{
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
