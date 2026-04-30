using NatureProtector.Core.Risk;
namespace NatureProtector.Prevention.Risk;

/// <summary>
/// Contract for converting one accepted reading into one operational risk assessment.
/// </summary>
public interface IRiskScoringService
{
    RiskAssessment CreateAssessment(RiskInput input);
}
