namespace NatureProtector.Prevention.Risk;

public interface IFireWeatherIndexCalculator
{
    FireWeatherIndexResult Calculate(FireWeatherIndexInput input);
}
