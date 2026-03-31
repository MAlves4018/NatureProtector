using NatureProtector.Core.Risk;

namespace NatureProtector.Prevention.Risk;

public interface IAreaRiskSnapshotService
{
    AreaRiskSnapshot BuildSnapshot(
        IEnumerable<RiskAssessment> assessments,
        DateTimeOffset snapshotTime);
}