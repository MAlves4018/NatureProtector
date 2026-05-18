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
}
