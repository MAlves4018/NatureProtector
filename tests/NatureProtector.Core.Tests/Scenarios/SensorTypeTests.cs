using NatureProtector.Core.Sensors;
using Xunit;

namespace NatureProtector.Core.Tests.Sensors;

/// <summary>
/// Unit tests for SensorType.
/// These tests protect the current enum shape expected by the domain model.
/// </summary>
public class SensorTypeTests
{
    [Fact]
    public void Enum_DefinesExpectedValues()
    {
        Assert.True(Enum.IsDefined(typeof(SensorType), SensorType.Temperature));
        Assert.True(Enum.IsDefined(typeof(SensorType), SensorType.Humidity));
        Assert.True(Enum.IsDefined(typeof(SensorType), SensorType.Wind));
        Assert.True(Enum.IsDefined(typeof(SensorType), SensorType.WeatherStation));
        Assert.True(Enum.IsDefined(typeof(SensorType), SensorType.Composite));
    }

    [Fact]
    public void Enum_HasExpectedUnderlyingValues()
    {
        Assert.Equal(0, (int)SensorType.Temperature);
        Assert.Equal(1, (int)SensorType.Humidity);
        Assert.Equal(2, (int)SensorType.Wind);
        Assert.Equal(3, (int)SensorType.WeatherStation);
        Assert.Equal(4, (int)SensorType.Composite);
    }
}