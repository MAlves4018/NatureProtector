using NatureProtector.Prevention.Risk;

namespace NatureProtector.Prevention.Tests.Risk;

public sealed class CandidateKbdiCalculatorTests
{
    private readonly CandidateKbdiCalculator _calculator = new();

    [Fact]
    public void Calculate_WithCompleteInputs_ReturnsBoundedCandidateKbdi()
    {
        var result = _calculator.Calculate(new KbdiInput(
            MaxTemperatureCelsius: 35.0,
            Precipitation24hMillimeters: 0.0));

        Assert.Equal(KbdiCalculationStatus.LimitedAntecedentHistory, result.Status);
        Assert.Equal(1.0, result.InputCompleteness, precision: 6);
        Assert.Equal(0.0, result.PreviousKeetchByramDroughtIndex!.Value, precision: 3);
        Assert.Equal(18.401, result.KeetchByramDroughtIndex!.Value, precision: 3);
        Assert.Equal(0.023001, result.NormalizedKeetchByramDroughtIndex!.Value, precision: 6);
        Assert.Contains("antecedent_kbdi_candidate_default", result.Limitations);
        Assert.Contains("limited_antecedent_history", result.Limitations);
        Assert.Contains("mean_annual_rain_candidate_default", result.Limitations);
    }

    [Fact]
    public void MissingFactory_ReturnsAbsentResultWithLimitations()
    {
        var result = KbdiResult.Missing("daily_reference_missing", "temperature_max_missing");

        Assert.Equal(KbdiCalculationStatus.Missing, result.Status);
        Assert.Equal(0.0, result.InputCompleteness);
        Assert.Null(result.KeetchByramDroughtIndex);
        Assert.Null(result.NormalizedKeetchByramDroughtIndex);
        Assert.Equal("absent", result.Provenance);
        Assert.Equal(["daily_reference_missing", "temperature_max_missing"], result.Limitations);
    }

    [Fact]
    public void Calculate_WithVeryLowAnnualRain_StillReturnsBoundedResult()
    {
        var result = _calculator.Calculate(new KbdiInput(
            MaxTemperatureCelsius: 31.0,
            Precipitation24hMillimeters: 0.0,
            PreviousKeetchByramDroughtIndex: 20.0,
            MeanAnnualRainInches: 0.0));

        Assert.Equal(KbdiCalculationStatus.Complete, result.Status);
        Assert.Empty(result.Limitations);
        Assert.InRange(result.NormalizedKeetchByramDroughtIndex!.Value, 0.0, 1.0);
    }

    [Fact]
    public void Calculate_WithHeavyRain_ReducesPreviousKbdiBeforeDrying()
    {
        var result = _calculator.Calculate(new KbdiInput(
            MaxTemperatureCelsius: 25.0,
            Precipitation24hMillimeters: 25.0,
            PreviousKeetchByramDroughtIndex: 400.0,
            MeanAnnualRainInches: 30.0));

        Assert.Equal(KbdiCalculationStatus.Complete, result.Status);
        Assert.InRange(result.KeetchByramDroughtIndex!.Value, 300.0, 400.0);
    }

    [Fact]
    public void Calculate_WithoutPrecipitation_MarksPartialAndDoesNotInventKbdi()
    {
        var result = _calculator.Calculate(new KbdiInput(
            MaxTemperatureCelsius: 35.0,
            Precipitation24hMillimeters: null));

        Assert.Equal(KbdiCalculationStatus.Partial, result.Status);
        Assert.Null(result.KeetchByramDroughtIndex);
        Assert.Contains("precipitation_24h_missing", result.Limitations);
    }

    [Fact]
    public void Calculate_WithAllInputs_ReturnsCompleteAndNormalizesByEightHundred()
    {
        var result = _calculator.Calculate(new KbdiInput(
            MaxTemperatureCelsius: 35.0,
            Precipitation24hMillimeters: 0.0,
            PreviousKeetchByramDroughtIndex: 100.0,
            MeanAnnualRainInches: 30.0));

        Assert.Equal(KbdiCalculationStatus.Complete, result.Status);
        Assert.NotNull(result.KeetchByramDroughtIndex);
        Assert.Equal(
            result.KeetchByramDroughtIndex!.Value / CandidateParameterSetV1.KeetchByramDroughtIndexMaximum,
            result.NormalizedKeetchByramDroughtIndex!.Value,
            precision: 6);
        Assert.InRange(result.KeetchByramDroughtIndex.Value, 0.0, 800.0);
        Assert.InRange(result.NormalizedKeetchByramDroughtIndex.Value, 0.0, 1.0);
    }

    [Theory]
    [InlineData(null, 0.0, 1, "max_temperature_missing")]
    [InlineData(35.0, null, 1, "precipitation_24h_missing")]
    [InlineData(null, null, 0, "max_temperature_missing")]
    public void Calculate_MissingInputs_MarksPartialOrMissing(
        double? maxTemperature,
        double? precipitation,
        int providedInputs,
        string expectedLimitation)
    {
        var result = _calculator.Calculate(new KbdiInput(
            MaxTemperatureCelsius: maxTemperature,
            Precipitation24hMillimeters: precipitation));

        Assert.Equal(providedInputs == 0 ? KbdiCalculationStatus.Missing : KbdiCalculationStatus.Partial, result.Status);
        Assert.Null(result.KeetchByramDroughtIndex);
        Assert.Contains(expectedLimitation, result.Limitations);
    }
}
