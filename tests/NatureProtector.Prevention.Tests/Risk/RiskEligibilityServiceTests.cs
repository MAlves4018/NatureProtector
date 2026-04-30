using NatureProtector.Prevention.Readings;
using NatureProtector.Prevention.Risk;
using NatureProtector.Shared.Contracts.Readings;

namespace NatureProtector.Prevention.Tests.Risk;

public sealed class RiskEligibilityServiceTests
{
    [Fact]
    public async Task EvaluateAsync_ReturnsEligible_ForNormalizedReading()
    {
        var service = new RiskEligibilityService();
        var reading = CreateReading();

        var result = await service.EvaluateAsync(reading, CancellationToken.None);

        Assert.True(result.IsEligible);
        Assert.Equal(RiskEligibilityReason.Eligible, result.ReasonCode);
        Assert.Null(result.Message);
    }

    [Fact]
    public void EligibleSingleton_HasExpectedReasonCode()
    {
        var result = RiskEligibilityResult.Eligible;

        Assert.True(result.IsEligible);
        Assert.Equal(RiskEligibilityReason.Eligible, result.ReasonCode);
        Assert.Null(result.Message);
    }

    [Fact]
    public void NotEligibleFactory_CreatesExpectedIneligibleResult()
    {
        var result = RiskEligibilityResult.NotEligible(
            RiskEligibilityReason.UnsupportedMetric,
            "Metric not supported by the current risk model.");

        Assert.False(result.IsEligible);
        Assert.Equal(RiskEligibilityReason.UnsupportedMetric, result.ReasonCode);
        Assert.Equal("Metric not supported by the current risk model.", result.Message);
    }

    private static NormalizedReading CreateReading()
    {
        return new NormalizedReading(
            EventId: Guid.NewGuid(),
            CorrelationId: "corr-eligibility",
            AreaId: Guid.NewGuid(),
            SensorId: Guid.NewGuid(),
            SensorName: "Sensor-PT-03",
            MetricType: SensorMetricType.Temperature,
            Value: 28.4,
            Unit: MeasurementUnit.Celsius,
            Latitude: 39.78,
            Longitude: -7.88,
            OperationalState: SensorOperationalState.Nominal,
            EventTime: new DateTimeOffset(2026, 4, 30, 15, 0, 0, TimeSpan.Zero),
            IngestTime: new DateTimeOffset(2026, 4, 30, 15, 0, 3, TimeSpan.Zero));
    }
}
