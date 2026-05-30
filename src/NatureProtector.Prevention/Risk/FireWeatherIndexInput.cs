namespace NatureProtector.Prevention.Risk;

public sealed record FireWeatherIndexInput(
    double? TemperatureCelsius,
    double? RelativeHumidityPercent,
    double? WindSpeedMetersPerSecond,
    double? Precipitation24hMillimeters,
    int Month,
    double? PreviousFineFuelMoistureCode = null,
    double? PreviousDuffMoistureCode = null,
    double? PreviousDroughtCode = null);
