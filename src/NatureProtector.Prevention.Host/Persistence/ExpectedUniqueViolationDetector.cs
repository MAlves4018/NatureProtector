using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace NatureProtector.Prevention.Host.Persistence;

internal static class ExpectedUniqueViolationDetector
{
    public static bool IsExpected(DbUpdateException exception, params ExpectedUniqueConstraint[] expectedConstraints)
    {
        ArgumentNullException.ThrowIfNull(exception);
        ArgumentNullException.ThrowIfNull(expectedConstraints);

        if (expectedConstraints.Length == 0)
        {
            return false;
        }

        if (exception.InnerException is PostgresException postgresException &&
            postgresException.SqlState == PostgresErrorCodes.UniqueViolation)
        {
            return expectedConstraints.Any(constraint =>
                string.Equals(
                    postgresException.ConstraintName,
                    constraint.PostgresConstraintName,
                    StringComparison.Ordinal));
        }

        if (exception.InnerException is Exception sqliteException &&
            string.Equals(
                sqliteException.GetType().FullName,
                "Microsoft.Data.Sqlite.SqliteException",
                StringComparison.Ordinal) &&
            TryGetSqliteErrorCode(sqliteException, out var sqliteErrorCode) &&
            sqliteErrorCode == 19)
        {
            return expectedConstraints.Any(constraint => constraint.MatchesSqlite(sqliteException.Message));
        }

        return false;
    }

    private static bool TryGetSqliteErrorCode(Exception exception, out int errorCode)
    {
        var property = exception.GetType().GetProperty("SqliteErrorCode");

        if (property?.GetValue(exception) is int resolvedErrorCode)
        {
            errorCode = resolvedErrorCode;
            return true;
        }

        errorCode = default;
        return false;
    }
}

internal sealed class ExpectedUniqueConstraint(
    string postgresConstraintName,
    string sqliteTableName,
    params string[] sqliteColumns)
{
    public string PostgresConstraintName { get; } = postgresConstraintName;
    public string SqliteTableName { get; } = sqliteTableName;
    public IReadOnlyList<string> SqliteColumns { get; } = sqliteColumns;

    public bool MatchesSqlite(string message)
    {
        if (!message.Contains(SqliteTableName, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        foreach (var column in SqliteColumns)
        {
            if (!message.Contains(column, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }
        }

        return true;
    }
}

internal static class NatureProtectorUniqueConstraints
{
    public static readonly ExpectedUniqueConstraint InboxEventId =
        new("IX_event_inbox_EventId", "event_inbox", "EventId");

    public static readonly ExpectedUniqueConstraint ProcessingAttemptNumber =
        new("IX_processing_attempts_InboxEventId_AttemptNumber", "processing_attempts", "InboxEventId", "AttemptNumber");

    public static readonly ExpectedUniqueConstraint AcceptedReadingEventId =
        new("IX_accepted_reading_log_EventId", "accepted_reading_log", "EventId");

    public static readonly ExpectedUniqueConstraint RiskAssessmentSourceEventId =
        new("IX_risk_assessment_log_SourceEventId", "risk_assessment_log", "SourceEventId");

    public static readonly ExpectedUniqueConstraint AreaRiskSnapshotId =
        new("PK_area_risk_snapshot_log", "area_risk_snapshot_log", "Id");

    public static readonly ExpectedUniqueConstraint CellOperationalStateGridCellId =
        new("IX_cell_operational_state_GridCellId", "cell_operational_state", "GridCellId");

    public static readonly ExpectedUniqueConstraint AreaOperationalStateAreaId =
        new("IX_area_operational_state_AreaId", "area_operational_state", "AreaId");
}
