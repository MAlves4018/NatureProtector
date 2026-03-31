using NatureProtector.Core.Risk;

namespace NatureProtector.Prevention.Risk;

public sealed class AreaRiskSnapshotService : IAreaRiskSnapshotService
{
    public AreaRiskSnapshot BuildSnapshot(
        IEnumerable<RiskAssessment> assessments,
        DateTimeOffset snapshotTime)
    {
        ArgumentNullException.ThrowIfNull(assessments);

        var items = assessments.ToList();

        if (items.Count == 0)
        {
            return new AreaRiskSnapshot(
                id: Guid.NewGuid(),
                timestamp: snapshotTime,
                aggregateRiskScore: 0.0,
                summary: "No accepted assessments are available for this area.");
        }

        return AreaRiskSnapshot.CreateFromAssessments(
            id: Guid.NewGuid(),
            timestamp: snapshotTime,
            assessments: items);
    }
}