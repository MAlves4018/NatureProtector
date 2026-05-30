namespace NatureProtector.Prevention.Risk;

public interface IKbdiCalculator
{
    KbdiResult Calculate(KbdiInput input);
}
