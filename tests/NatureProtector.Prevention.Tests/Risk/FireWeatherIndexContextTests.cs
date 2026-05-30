using NatureProtector.Prevention.Risk;

namespace NatureProtector.Prevention.Tests.Risk;

public sealed class FireWeatherIndexContextTests
{
    [Fact]
    public void Constructor_WithImportedFwi_MarksCompleteAndNormalizes()
    {
        var context = new FireWeatherIndexContext(
            FireWeatherIndex: 65.377,
            KeetchByramDroughtIndex: null,
            Provenance: "imported_reference");

        Assert.Equal(FireWeatherIndexCalculationStatus.Complete, context.CalculationStatus);
        Assert.Equal(65.377 / 80.0, context.NormalizedFireWeatherIndex!.Value, precision: 6);
        Assert.Equal(KbdiCalculationStatus.Missing, context.KbdiCalculationStatus);
        Assert.True(context.IsImported);
    }

    [Fact]
    public void FromResult_PreservesSubcomponentsAndLimitations()
    {
        var result = new FireWeatherIndexResult(
            FireWeatherIndexCalculationStatus.Complete,
            1.0,
            90,
            10,
            20,
            5,
            8,
            12,
            0.15,
            "candidate_fwi_calculator",
            ["antecedent_fwi_codes_candidate_defaults"]);

        var context = FireWeatherIndexContext.FromResult(result);

        Assert.Equal(90, context.FineFuelMoistureCode);
        Assert.Equal(10, context.DuffMoistureCode);
        Assert.Equal(20, context.DroughtCode);
        Assert.Equal(5, context.InitialSpreadIndex);
        Assert.Equal(8, context.BuildupIndex);
        Assert.Equal("antecedent_fwi_codes_candidate_defaults", context.Limitations);
    }

    [Fact]
    public void WithKbdi_PreservesNormalizedKbdiAndStatus()
    {
        var context = FireWeatherIndexContext.Absent.WithKbdi(new KbdiResult(
            KbdiCalculationStatus.Complete,
            1.0,
            10,
            20,
            0.025,
            "candidate_kbdi_calculator",
            ["mean_annual_rain_candidate_default"]));

        Assert.Equal(20, context.KeetchByramDroughtIndex);
        Assert.Equal(0.025, context.NormalizedKeetchByramDroughtIndex);
        Assert.Equal(KbdiCalculationStatus.Complete, context.KbdiCalculationStatus);
        Assert.Equal("mean_annual_rain_candidate_default", context.Limitations);
    }
}
