using NatureProtector.Core.Primitives;
using Xunit;

namespace NatureProtector.Core.Tests.Primitives;

/// <summary>
/// Unit tests for the Location value object.
/// </summary>
public class LocationTests
{
    [Fact]
    public void Ctor_AssignsProperties_WhenValid()
    {
        var location = new Location(38.7167, -9.1333, 20.0);

        Assert.Equal(38.7167, location.Latitude);
        Assert.Equal(-9.1333, location.Longitude);
        Assert.Equal(20.0, location.Altitude);
    }

    [Theory]
    [InlineData(-91.0, 0.0, "latitude")]
    [InlineData(91.0, 0.0, "latitude")]
    [InlineData(0.0, -181.0, "longitude")]
    [InlineData(0.0, 181.0, "longitude")]
    public void Ctor_Throws_WhenCoordinatesAreInvalid(double latitude, double longitude, string paramName)
    {
        var ex = Assert.Throws<ArgumentOutOfRangeException>(() => new Location(latitude, longitude));

        Assert.Equal(paramName, ex.ParamName);
    }

    [Fact]
    public void DistanceTo_Throws_WhenOtherIsNull()
    {
        var location = new Location(0.0, 0.0);

        var ex = Assert.Throws<ArgumentNullException>(() => location.DistanceTo(null!));

        Assert.Equal("other", ex.ParamName);
    }

    [Fact]
    public void DistanceTo_ReturnsZero_ForSamePoint()
    {
        var location = new Location(0.0, 0.0);

        var distance = location.DistanceTo(new Location(0.0, 0.0));

        Assert.Equal(0.0, distance, 6);
    }

    [Fact]
    public void DistanceTo_IsSymmetric_AndPositive()
    {
        var a = new Location(38.7167, -9.1333);
        var b = new Location(38.7369, -9.1427);

        var ab = a.DistanceTo(b);
        var ba = b.DistanceTo(a);

        Assert.True(ab > 0.0);
        Assert.Equal(ab, ba, 6);
    }
}
