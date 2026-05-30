using NatureProtector.Prevention.Risk;

namespace NatureProtector.Prevention.Tests.Risk;

public sealed class QualityFlagCatalogTests
{
    [Fact]
    public void ParseMany_MapsKnownWireFlagsToTypedCatalog()
    {
        var flags = QualityFlagCatalog.ParseMany(
            ["Delayed", "Stale", "OutOfOrder", "UnsupportedMetric", "InvalidUnit", "Dropped", "Duplicate", RiskInput.MissingDailyCellStateFlag]);

        Assert.Contains(QualityFlag.Delayed, flags);
        Assert.Contains(QualityFlag.Stale, flags);
        Assert.Contains(QualityFlag.OutOfOrder, flags);
        Assert.Contains(QualityFlag.UnsupportedMetric, flags);
        Assert.Contains(QualityFlag.InvalidUnit, flags);
        Assert.Contains(QualityFlag.Dropped, flags);
        Assert.Contains(QualityFlag.Duplicate, flags);
        Assert.Contains(QualityFlag.DailyCellStateMissing, flags);
    }

    [Fact]
    public void ToWireName_PreservesCompatibilityNames()
    {
        Assert.Equal("Delayed", QualityFlag.Delayed.ToWireName());
        Assert.Equal(RiskInput.MissingDailyCellStateFlag, QualityFlag.DailyCellStateMissing.ToWireName());
    }

    [Theory]
    [InlineData(QualityFlag.StuckFlatline, "stuck_flatline")]
    [InlineData(QualityFlag.RangeClipping, "range_clipping")]
    [InlineData(QualityFlag.DegradedSensor, "degraded_sensor")]
    [InlineData(QualityFlag.LowCoverage, "low_coverage")]
    [InlineData(QualityFlag.Outlier, "Outlier")]
    public void ToWireName_UsesStableWireNames(QualityFlag flag, string expectedWireName)
    {
        Assert.Equal(expectedWireName, flag.ToWireName());
    }

    [Theory]
    [InlineData("stuck-flatline", QualityFlag.StuckFlatline)]
    [InlineData("stuck_flatline", QualityFlag.StuckFlatline)]
    [InlineData("range_clipping", QualityFlag.RangeClipping)]
    [InlineData("degraded_sensor", QualityFlag.DegradedSensor)]
    [InlineData("low_coverage", QualityFlag.LowCoverage)]
    [InlineData(" DailyCellStateMissing ", QualityFlag.DailyCellStateMissing)]
    [InlineData("unsupportedmetric", QualityFlag.UnsupportedMetric)]
    public void TryParse_AcceptsCanonicalAndLegacyWireNames(string wireName, QualityFlag expectedFlag)
    {
        Assert.True(QualityFlagCatalog.TryParse(wireName, out var flag));
        Assert.Equal(expectedFlag, flag);
    }

    [Fact]
    public void ParseMany_DeduplicatesAndIgnoresUnknownValues()
    {
        var flags = QualityFlagCatalog.ParseMany(
            ["low_coverage", "LowCoverage", "unknown_flag", "", "range-clipping", "range_clipping"]);

        Assert.Equal(2, flags.Count);
        Assert.Contains(QualityFlag.LowCoverage, flags);
        Assert.Contains(QualityFlag.RangeClipping, flags);
    }

    [Fact]
    public void ParseMany_NullInput_ReturnsEmptyCollection()
    {
        Assert.Empty(QualityFlagCatalog.ParseMany(null));
    }
}
