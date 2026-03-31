using NatureProtector.Core.Primitives;
using Xunit;

namespace NatureProtector.Core.Tests.Primitives;

/// <summary>
/// Unit tests for rectangular geographic boundaries.
/// </summary>
public class BoundariesTests
{
    [Fact]
    public void Ctor_AssignsProperties_WhenValid()
    {
        var boundaries = new Boundaries(1.0, 2.0, 3.0, 4.0);

        Assert.Equal(1.0, boundaries.MinLatitude);
        Assert.Equal(2.0, boundaries.MaxLatitude);
        Assert.Equal(3.0, boundaries.MinLongitude);
        Assert.Equal(4.0, boundaries.MaxLongitude);
    }

    [Theory]
    [InlineData(-91.0, 10.0, 0.0, 1.0, "minLatitude")]
    [InlineData(0.0, 91.0, 0.0, 1.0, "maxLatitude")]
    [InlineData(0.0, 1.0, -181.0, 1.0, "minLongitude")]
    [InlineData(0.0, 1.0, 0.0, 181.0, "maxLongitude")]
    public void Ctor_Throws_WhenCoordinateOutsideAllowedRange(
        double minLatitude,
        double maxLatitude,
        double minLongitude,
        double maxLongitude,
        string expectedParamName)
    {
        var ex = Assert.Throws<ArgumentOutOfRangeException>(
            () => new Boundaries(minLatitude, maxLatitude, minLongitude, maxLongitude));

        Assert.Equal(expectedParamName, ex.ParamName);
    }

    [Fact]
    public void Ctor_Throws_WhenLatitudeRangeIsInvalid()
    {
        var ex = Assert.Throws<ArgumentException>(() => new Boundaries(10.0, 10.0, 0.0, 1.0));

        Assert.Equal("minLatitude", ex.ParamName);
    }

    [Fact]
    public void Ctor_Throws_WhenLongitudeRangeIsInvalid()
    {
        var ex = Assert.Throws<ArgumentException>(() => new Boundaries(0.0, 1.0, 5.0, 5.0));

        Assert.Equal("minLongitude", ex.ParamName);
    }

    [Fact]
    public void Contains_Throws_WhenLocationIsNull()
    {
        var boundaries = new Boundaries(0.0, 10.0, 0.0, 10.0);

        var ex = Assert.Throws<ArgumentNullException>(() => boundaries.Contains(null!));

        Assert.Equal("location", ex.ParamName);
    }

    [Fact]
    public void Contains_ReturnsTrue_ForPointInsideOrOnBoundary()
    {
        var boundaries = new Boundaries(0.0, 10.0, 0.0, 10.0);

        Assert.True(boundaries.Contains(new Location(5.0, 5.0)));
        Assert.True(boundaries.Contains(new Location(0.0, 0.0)));
        Assert.True(boundaries.Contains(new Location(10.0, 10.0)));
    }

    [Fact]
    public void Contains_ReturnsFalse_ForPointOutside()
    {
        var boundaries = new Boundaries(0.0, 10.0, 0.0, 10.0);

        Assert.False(boundaries.Contains(new Location(-0.1, 5.0)));
        Assert.False(boundaries.Contains(new Location(5.0, 10.1)));
    }
}
