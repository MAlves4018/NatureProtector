namespace NatureProtector.Prevention.Risk;

/// <summary>
/// Candidate parameter set for the V1 methodological baseline.
/// Values are engineering parameters for repeatable evaluation, not scientific
/// calibration.
/// </summary>
public static class CandidateParameterSetV1
{
    public const string Version = "Candidate Parameter Set V1.0";

    public const double MeteorologyWeight = 0.50;
    public const double DroughtWeight = 0.20;
    public const double TerritoryWeight = 0.30;

    public const double TerritoryHazardWeight = 0.50;
    public const double TerritoryFuelWeight = 0.30;
    public const double TerritoryGeomorphologyWeight = 0.20;
    public const double CandidateDefaultComponent = 0.50;

    public const double TemperatureMetricWeight = 0.40;
    public const double HumidityMetricWeight = 0.35;
    public const double WindMetricWeight = 0.25;

    public const double MetricFallbackDryness = 0.50;
    public const double DryAntecedentDryness = 0.70;
    public const double PrecipitationReductionReferenceMillimeters = 20.0;
    public const double MaximumPrecipitationDrynessReduction = 0.30;

    public const double FireWeatherIndexNormalizationReference = 80.0;
    public const double FireWeatherIndexMetricBlendWeight = 0.70;
    public const double FireWeatherIndexBlendWeight = 0.30;

    public const double KeetchByramDroughtIndexMaximum = 800.0;
    public const double CandidateMeanAnnualRainInches = 30.0;
    public const double MinimumMeanAnnualRainInches = 0.1;

    public const double WarningOpenThreshold = 0.60;
    public const double WarningCloseThreshold = 0.50;
    public const double AlarmOpenThreshold = 0.80;
    public const double AlarmCloseThreshold = 0.70;
    public const int AlertPersistenceCycles = 2;
    public const int AlertCooldownIntervalMultiplier = 3;
    public const int AlertCooldownMinimumSeconds = 180;

    public const int LatenessIntervalMultiplier = 2;
    public const int LatenessMinimumSeconds = 120;
    public const int ReorderIntervalMultiplier = 3;
    public const int ReorderMinimumSeconds = 180;
    public const int StaleIntervalMultiplier = 5;
    public const int StaleMinimumSeconds = 300;

    public const double HighConfidenceFactor = 1.00;
    public const double MediumConfidenceFactor = 0.97;
    public const double LowConfidenceFactor = 0.93;

    public const double IntactIntegrityFactor = 1.00;
    public const double DegradedIntegrityFactor = 0.90;
    public const double CompromisedIntegrityFactor = 0.80;

    public static double ClampNormalized(double value)
    {
        return Math.Clamp(value, 0.0, 1.0);
    }

    public static int ToScore100(double normalizedScore)
    {
        return (int)Math.Round(ClampNormalized(normalizedScore) * 100.0, MidpointRounding.AwayFromZero);
    }

    public static TimeSpan ResolveLatenessThreshold(TimeSpan interval)
    {
        return TimeSpan.FromSeconds(
            Math.Max(LatenessIntervalMultiplier * interval.TotalSeconds, LatenessMinimumSeconds));
    }

    public static TimeSpan ResolveReorderWindow(TimeSpan interval)
    {
        return TimeSpan.FromSeconds(
            Math.Max(ReorderIntervalMultiplier * interval.TotalSeconds, ReorderMinimumSeconds));
    }

    public static TimeSpan ResolveStaleThreshold(TimeSpan interval)
    {
        return TimeSpan.FromSeconds(
            Math.Max(StaleIntervalMultiplier * interval.TotalSeconds, StaleMinimumSeconds));
    }

    public static TimeSpan ResolveAlertCooldown(TimeSpan interval)
    {
        return TimeSpan.FromSeconds(
            Math.Max(AlertCooldownIntervalMultiplier * interval.TotalSeconds, AlertCooldownMinimumSeconds));
    }

    public static double ResolveConfidenceFactor(ObservationalConfidenceLevel confidence)
    {
        return confidence switch
        {
            ObservationalConfidenceLevel.High => HighConfidenceFactor,
            ObservationalConfidenceLevel.Medium => MediumConfidenceFactor,
            ObservationalConfidenceLevel.Low => LowConfidenceFactor,
            _ => HighConfidenceFactor
        };
    }

    public static double ResolveIntegrityFactor(OperationalIntegrityLevel integrity)
    {
        return integrity switch
        {
            OperationalIntegrityLevel.Intact => IntactIntegrityFactor,
            OperationalIntegrityLevel.Degraded => DegradedIntegrityFactor,
            OperationalIntegrityLevel.Compromised => CompromisedIntegrityFactor,
            _ => IntactIntegrityFactor
        };
    }
}
