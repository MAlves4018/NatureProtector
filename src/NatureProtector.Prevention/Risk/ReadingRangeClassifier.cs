using NatureProtector.Prevention.Readings;
using NatureProtector.Shared.Contracts.Readings;

namespace NatureProtector.Prevention.Risk;

public static class ReadingRangeClassifier
{
    public const string ClassifierName = "range_v1";
    public const string RuleSetVersion = "candidate-range-v1";

    public static IReadOnlyList<ClassifierResult> Classify(NormalizedReading reading)
    {
        ArgumentNullException.ThrowIfNull(reading);

        var flags = new List<QualityFlag>();
        var reasons = new List<string>();

        switch (reading.MetricType)
        {
            case SensorMetricType.Temperature when reading.Unit == MeasurementUnit.Celsius:
                AddRangeFlags(reading.Value, -50.0, 60.0, flags, reasons, "temperature_out_of_candidate_range");
                break;

            case SensorMetricType.Humidity when reading.Unit == MeasurementUnit.Percent:
                AddRangeFlags(reading.Value, 0.0, 100.0, flags, reasons, "humidity_out_of_candidate_range");
                break;

            case SensorMetricType.WindSpeed when reading.Unit == MeasurementUnit.MetersPerSecond:
                AddRangeFlags(reading.Value, 0.0, 75.0, flags, reasons, "wind_speed_out_of_candidate_range");
                break;
        }

        if (flags.Count == 0)
        {
            return Array.Empty<ClassifierResult>();
        }

        return
        [
            ClassifierResult.Create(
                ClassifierName,
                ClassifierStatus.Failed,
                ClassifierSeverity.High,
                flags.Select(flag => flag.ToWireName()).Distinct(StringComparer.Ordinal).ToArray(),
                reasons.Distinct(StringComparer.Ordinal).ToArray(),
                reading.IngestTime ?? DateTimeOffset.UtcNow,
                RuleSetVersion)
        ];
    }

    private static void AddRangeFlags(
        double value,
        double minimum,
        double maximum,
        List<QualityFlag> flags,
        List<string> reasons,
        string reason)
    {
        if (value < minimum || value > maximum)
        {
            flags.Add(QualityFlag.Outlier);
            flags.Add(QualityFlag.RangeClipping);
            reasons.Add(reason);
        }
    }
}
