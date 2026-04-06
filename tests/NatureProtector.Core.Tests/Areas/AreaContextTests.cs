using NatureProtector.Core.Areas;

namespace NatureProtector.Core.Tests.Areas;

public sealed class AreaContextTests
{
    public static TheoryData<double> InvalidFactorValues => new()
    {
        -0.01,
        1.01,
        double.NaN,
        double.PositiveInfinity,
        double.NegativeInfinity
    };

    [Fact]
    public void Ctor_TrimsStrings_AndPreservesNormalizedFactors()
    {
        var context = new AreaContext(
            vegetationType: "  Pine Forest  ",
            vegetationDensity: 0.70,
            populationExposure: 0.35,
            criticalInfrastructureExposure: 0.15,
            seasonality: "  Dry Season  ");

        Assert.Equal("Pine Forest", context.VegetationType);
        Assert.Equal(0.70, context.VegetationDensity);
        Assert.Equal(0.35, context.PopulationExposure);
        Assert.Equal(0.15, context.CriticalInfrastructureExposure);
        Assert.Equal("Dry Season", context.Seasonality);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Ctor_Throws_WhenVegetationTypeIsNullOrWhitespace(string? vegetationType)
    {
        var ex = Assert.Throws<ArgumentException>(() => new AreaContext(
            vegetationType: vegetationType!,
            vegetationDensity: 0.50,
            populationExposure: 0.50,
            criticalInfrastructureExposure: 0.50,
            seasonality: "Summer"));

        Assert.Equal("vegetationType", ex.ParamName);
        Assert.Contains("must not be null or whitespace", ex.Message);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Ctor_Throws_WhenSeasonalityIsNullOrWhitespace(string? seasonality)
    {
        var ex = Assert.Throws<ArgumentException>(() => new AreaContext(
            vegetationType: "Shrubland",
            vegetationDensity: 0.50,
            populationExposure: 0.50,
            criticalInfrastructureExposure: 0.50,
            seasonality: seasonality!));

        Assert.Equal("seasonality", ex.ParamName);
        Assert.Contains("must not be null or whitespace", ex.Message);
    }

    [Theory]
    [MemberData(nameof(InvalidFactorValues))]
    public void Ctor_Throws_WhenVegetationDensityIsInvalid(double vegetationDensity)
    {
        var ex = Assert.Throws<ArgumentOutOfRangeException>(() => new AreaContext(
            vegetationType: "Shrubland",
            vegetationDensity: vegetationDensity,
            populationExposure: 0.50,
            criticalInfrastructureExposure: 0.50,
            seasonality: "Summer"));

        Assert.Equal("vegetationDensity", ex.ParamName);
        Assert.Contains("range [0, 1]", ex.Message);
    }

    [Theory]
    [MemberData(nameof(InvalidFactorValues))]
    public void Ctor_Throws_WhenPopulationExposureIsInvalid(double populationExposure)
    {
        var ex = Assert.Throws<ArgumentOutOfRangeException>(() => new AreaContext(
            vegetationType: "Shrubland",
            vegetationDensity: 0.50,
            populationExposure: populationExposure,
            criticalInfrastructureExposure: 0.50,
            seasonality: "Summer"));

        Assert.Equal("populationExposure", ex.ParamName);
        Assert.Contains("range [0, 1]", ex.Message);
    }

    [Theory]
    [MemberData(nameof(InvalidFactorValues))]
    public void Ctor_Throws_WhenCriticalInfrastructureExposureIsInvalid(double criticalInfrastructureExposure)
    {
        var ex = Assert.Throws<ArgumentOutOfRangeException>(() => new AreaContext(
            vegetationType: "Shrubland",
            vegetationDensity: 0.50,
            populationExposure: 0.50,
            criticalInfrastructureExposure: criticalInfrastructureExposure,
            seasonality: "Summer"));

        Assert.Equal("criticalInfrastructureExposure", ex.ParamName);
        Assert.Contains("range [0, 1]", ex.Message);
    }
}
