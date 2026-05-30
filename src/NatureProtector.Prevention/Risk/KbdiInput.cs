namespace NatureProtector.Prevention.Risk;

public sealed record KbdiInput(
    double? MaxTemperatureCelsius,
    double? Precipitation24hMillimeters,
    double? PreviousKeetchByramDroughtIndex = null,
    double? MeanAnnualRainInches = null);
