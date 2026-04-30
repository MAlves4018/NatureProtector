using NatureProtector.Prevention.Risk;
using NatureProtector.Shared.Contracts.Readings;

namespace NatureProtector.Prevention.Tests.Risk;

public sealed class SimpleRiskScoringServiceTests
{
    private readonly SimpleRiskScoringService _service = new();

    public static TheoryData<double, double> TemperatureCases => new()
    {
        { 19.9, 0.10 },
        { 20.0, 0.20 },
        { 25.0, 0.40 },
        { 30.0, 0.65 },
        { 35.0, 0.85 },
        { 40.0, 1.00 }
    };

    public static TheoryData<double, double> HumidityCases => new()
    {
        { 70.0, 0.05 },
        { 50.0, 0.20 },
        { 35.0, 0.40 },
        { 20.0, 0.70 },
        { 19.9, 0.95 }
    };

    public static TheoryData<double, double> WindCases => new()
    {
        { 4.9, 0.10 },
        { 5.0, 0.30 },
        { 10.0, 0.55 },
        { 15.0, 0.75 },
        { 20.0, 0.95 }
    };

    [Theory]
    [MemberData(nameof(TemperatureCases))]
    public void CreateAssessment_MapsTemperatureThresholds(double value, double expectedScore)
    {
        var assessment = _service.CreateAssessment(CreateRiskInput(
            metricType: SensorMetricType.Temperature,
            value: value));

        Assert.Equal(expectedScore, assessment.RiskScore, precision: 3);
    }

    [Theory]
    [MemberData(nameof(HumidityCases))]
    public void CreateAssessment_MapsHumidityThresholds(double value, double expectedScore)
    {
        var assessment = _service.CreateAssessment(CreateRiskInput(
            metricType: SensorMetricType.Humidity,
            value: value));

        Assert.Equal(expectedScore, assessment.RiskScore, precision: 3);
    }

    [Theory]
    [MemberData(nameof(WindCases))]
    public void CreateAssessment_MapsWindSpeedThresholds(double value, double expectedScore)
    {
        var assessment = _service.CreateAssessment(CreateRiskInput(
            metricType: SensorMetricType.WindSpeed,
            value: value));

        Assert.Equal(expectedScore, assessment.RiskScore, precision: 3);
    }

    [Fact]
    public void CreateAssessment_UsesFallbackScore_ForUnsupportedMetric()
    {
        var assessment = _service.CreateAssessment(CreateRiskInput(
            metricType: SensorMetricType.WindDirection,
            value: 180.0,
            unit: MeasurementUnit.Degrees));

        Assert.Equal(0.20, assessment.RiskScore, precision: 3);
    }

    [Fact]
    public void CreateAssessment_CreatesAssessmentWithExpectedMetadata()
    {
        var areaId = Guid.NewGuid();
        var sensorId = Guid.NewGuid();
        var eventId = Guid.NewGuid();
        var assessedAt = DateTimeOffset.UtcNow;

        var assessment = _service.CreateAssessment(new RiskInput(
            AreaId: areaId,
            SensorId: sensorId,
            SourceEventId: eventId,
            MetricType: SensorMetricType.Temperature,
            Value: 32.4,
            Unit: MeasurementUnit.Celsius,
            EventTime: assessedAt));

        Assert.NotEqual(Guid.Empty, assessment.Id);
        Assert.Equal(assessedAt, assessment.Timestamp);
        Assert.Contains(areaId.ToString(), assessment.ExplanationSummary);
        Assert.Contains(sensorId.ToString(), assessment.ExplanationSummary);
        Assert.Contains(eventId.ToString(), assessment.ExplanationSummary);
        Assert.Contains(nameof(SensorMetricType.Temperature), assessment.ExplanationSummary);
    }

    private static RiskInput CreateRiskInput(
        SensorMetricType metricType,
        double value,
        MeasurementUnit unit = MeasurementUnit.Celsius)
    {
        return new RiskInput(
            AreaId: Guid.NewGuid(),
            SensorId: Guid.NewGuid(),
            SourceEventId: Guid.NewGuid(),
            MetricType: metricType,
            Value: value,
            Unit: unit,
            EventTime: DateTimeOffset.UtcNow);
    }
}
