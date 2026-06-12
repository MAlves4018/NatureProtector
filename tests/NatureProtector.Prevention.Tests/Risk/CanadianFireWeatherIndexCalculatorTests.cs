using NatureProtector.Prevention.Risk;

namespace NatureProtector.Prevention.Tests.Risk;

public sealed class CanadianFireWeatherIndexCalculatorTests
{
    private readonly CanadianFireWeatherIndexCalculator _calculator = new();

    [Fact]
    public void Calculate_WithCompleteInputs_ReturnsExpectedCandidateSubcomponents()
    {
        var result = _calculator.Calculate(new FireWeatherIndexInput(
            TemperatureCelsius: 32.4,
            RelativeHumidityPercent: 25.0,
            WindSpeedMetersPerSecond: 5.0,
            Precipitation24hMillimeters: 0.0,
            Month: 9));

        Assert.Equal(FireWeatherIndexCalculationStatus.CompleteWithCandidateDefaults, result.Status);
        Assert.Equal(1.0, result.InputCompleteness, precision: 6);
        Assert.Equal(93.788, result.FineFuelMoistureCode!.Value, precision: 3);
        Assert.Equal(154.473, result.DuffMoistureCode!.Value, precision: 3);
        Assert.Equal(657.536, result.DroughtCode!.Value, precision: 3);
        Assert.Equal(18.142, result.InitialSpreadIndex!.Value, precision: 3);
        Assert.Equal(194.634, result.BuildupIndex!.Value, precision: 3);
        Assert.Equal(56.460, result.FireWeatherIndex!.Value, precision: 3);
        Assert.Equal(0.705752, result.NormalizedFireWeatherIndex!.Value, precision: 6);
        Assert.Contains("antecedent_fwi_codes_candidate_defaults", result.Limitations);
    }

    [Fact]
    public void Calculate_WithRainExercisesRainBranchesAndKeepsNormalizedFwiBounded()
    {
        var result = _calculator.Calculate(new FireWeatherIndexInput(
            TemperatureCelsius: 22.0,
            RelativeHumidityPercent: 70.0,
            WindSpeedMetersPerSecond: 2.0,
            Precipitation24hMillimeters: 12.0,
            Month: 4,
            PreviousFineFuelMoistureCode: 95.0,
            PreviousDuffMoistureCode: 40.0,
            PreviousDroughtCode: 80.0));

        Assert.Equal(FireWeatherIndexCalculationStatus.Complete, result.Status);
        Assert.InRange(result.FineFuelMoistureCode!.Value, 0.0, 101.0);
        Assert.InRange(result.DuffMoistureCode!.Value, 0.0, 40.0);
        Assert.InRange(result.DroughtCode!.Value, 0.0, 80.0);
        Assert.InRange(result.FireWeatherIndex!.Value, 0.0, 80.0);
        Assert.InRange(result.NormalizedFireWeatherIndex!.Value, 0.0, 1.0);
    }

    [Fact]
    public void MissingFactory_ReturnsAbsentResultWithLimitations()
    {
        var result = FireWeatherIndexResult.Missing("no_weather_daily_reference", "precipitation_24h_missing");

        Assert.Equal(FireWeatherIndexCalculationStatus.Missing, result.Status);
        Assert.Equal(0.0, result.InputCompleteness);
        Assert.Null(result.FireWeatherIndex);
        Assert.Null(result.NormalizedFireWeatherIndex);
        Assert.Equal("absent", result.Provenance);
        Assert.Equal(["no_weather_daily_reference", "precipitation_24h_missing"], result.Limitations);
    }

    [Fact]
    public void Calculate_WithoutPrecipitation_MarksPartialAndDoesNotInventFwi()
    {
        var result = _calculator.Calculate(new FireWeatherIndexInput(
            TemperatureCelsius: 32.4,
            RelativeHumidityPercent: 25.0,
            WindSpeedMetersPerSecond: 5.0,
            Precipitation24hMillimeters: null,
            Month: 9));

        Assert.Equal(FireWeatherIndexCalculationStatus.Partial, result.Status);
        Assert.Null(result.FireWeatherIndex);
        Assert.Contains("precipitation_24h_missing", result.Limitations);
    }

    [Theory]
    [InlineData(null, 25.0, 5.0, 0.0, "temperature_missing")]
    [InlineData(32.4, null, 5.0, 0.0, "relative_humidity_missing")]
    [InlineData(32.4, 25.0, null, 0.0, "wind_speed_missing")]
    public void Calculate_MissingRequiredWeatherInput_MarksPartial(
        double? temperature,
        double? humidity,
        double? windSpeed,
        double? precipitation,
        string expectedLimitation)
    {
        var result = _calculator.Calculate(new FireWeatherIndexInput(
            TemperatureCelsius: temperature,
            RelativeHumidityPercent: humidity,
            WindSpeedMetersPerSecond: windSpeed,
            Precipitation24hMillimeters: precipitation,
            Month: 9));

        Assert.Equal(FireWeatherIndexCalculationStatus.Partial, result.Status);
        Assert.Null(result.FireWeatherIndex);
        Assert.Contains(expectedLimitation, result.Limitations);
    }

    [Fact]
    public void Calculate_WithAntecedentCodes_ReturnsCompleteWithoutCandidateDefaultLimitation()
    {
        var result = _calculator.Calculate(new FireWeatherIndexInput(
            TemperatureCelsius: 32.4,
            RelativeHumidityPercent: 25.0,
            WindSpeedMetersPerSecond: 5.0,
            Precipitation24hMillimeters: 0.0,
            Month: 9,
            PreviousFineFuelMoistureCode: 85.0,
            PreviousDuffMoistureCode: 6.0,
            PreviousDroughtCode: 15.0));

        Assert.Equal(FireWeatherIndexCalculationStatus.Complete, result.Status);
        Assert.DoesNotContain("antecedent_fwi_codes_candidate_defaults", result.Limitations);
        Assert.InRange(result.NormalizedFireWeatherIndex!.Value, 0.0, 1.0);
    }

    [Fact]
    public void Calculate_WithoutAnyInputs_MarksMissing()
    {
        var result = _calculator.Calculate(new FireWeatherIndexInput(
            TemperatureCelsius: null,
            RelativeHumidityPercent: null,
            WindSpeedMetersPerSecond: null,
            Precipitation24hMillimeters: null,
            Month: 9));

        Assert.Equal(FireWeatherIndexCalculationStatus.Missing, result.Status);
        Assert.Equal(0.0, result.InputCompleteness, precision: 6);
    }
}
