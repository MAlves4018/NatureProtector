using Microsoft.EntityFrameworkCore;
using NatureProtector.Prevention.Host.Persistence;
using Npgsql;

namespace NatureProtector.Prevention.Host.Tests.Persistence;

public sealed class ExpectedUniqueViolationDetectorTests
{
    [Fact]
    public void IsExpected_NullException_ThrowsArgumentNullException()
    {
        var exception = Assert.Throws<ArgumentNullException>(() =>
            ExpectedUniqueViolationDetector.IsExpected(null!, NatureProtectorUniqueConstraints.InboxEventId));

        Assert.Equal("exception", exception.ParamName);
    }

    [Fact]
    public void IsExpected_NullConstraints_ThrowsArgumentNullException()
    {
        var exception = Assert.Throws<ArgumentNullException>(() =>
            ExpectedUniqueViolationDetector.IsExpected(new DbUpdateException("boom"), null!));

        Assert.Equal("expectedConstraints", exception.ParamName);
    }

    [Fact]
    public void IsExpected_EmptyConstraints_ReturnsFalse()
    {
        var result = ExpectedUniqueViolationDetector.IsExpected(new DbUpdateException("boom"));

        Assert.False(result);
    }

    [Fact]
    public void IsExpected_DbUpdateExceptionWithoutInnerException_ReturnsFalse()
    {
        var result = ExpectedUniqueViolationDetector.IsExpected(
            new DbUpdateException("boom"),
            NatureProtectorUniqueConstraints.InboxEventId);

        Assert.False(result);
    }

    [Fact]
    public void IsExpected_PostgresUniqueViolationWithKnownConstraint_ReturnsTrue()
    {
        var exception = CreateDbUpdateException(
            PostgresErrorCodes.UniqueViolation,
            NatureProtectorUniqueConstraints.InboxEventId.PostgresConstraintName);

        var result = ExpectedUniqueViolationDetector.IsExpected(
            exception,
            NatureProtectorUniqueConstraints.InboxEventId);

        Assert.True(result);
    }

    [Fact]
    public void IsExpected_PostgresUniqueViolationWithUnknownConstraint_ReturnsFalse()
    {
        var exception = CreateDbUpdateException(
            PostgresErrorCodes.UniqueViolation,
            "IX_unexpected_constraint");

        var result = ExpectedUniqueViolationDetector.IsExpected(
            exception,
            NatureProtectorUniqueConstraints.InboxEventId);

        Assert.False(result);
    }

    [Fact]
    public void IsExpected_PostgresNonUniqueViolationWithKnownConstraint_ReturnsFalse()
    {
        var exception = CreateDbUpdateException(
            PostgresErrorCodes.ForeignKeyViolation,
            NatureProtectorUniqueConstraints.InboxEventId.PostgresConstraintName);

        var result = ExpectedUniqueViolationDetector.IsExpected(
            exception,
            NatureProtectorUniqueConstraints.InboxEventId);

        Assert.False(result);
    }

    [Fact]
    public void MatchesSqlite_MessageContainsTableAndColumnsIgnoringCase_ReturnsTrue()
    {
        var constraint = new ExpectedUniqueConstraint(
            "IX_processing_attempts_InboxEventId_AttemptNumber",
            "processing_attempts",
            "InboxEventId",
            "AttemptNumber");

        var result = constraint.MatchesSqlite(
            "SQLite Error 19: UNIQUE constraint failed: PROCESSING_ATTEMPTS.AttemptNumber, processing_attempts.InboxEventId");

        Assert.True(result);
    }

    [Fact]
    public void MatchesSqlite_MessageContainsDifferentTable_ReturnsFalse()
    {
        var constraint = new ExpectedUniqueConstraint(
            "IX_event_inbox_EventId",
            "event_inbox",
            "EventId");

        var result = constraint.MatchesSqlite(
            "SQLite Error 19: UNIQUE constraint failed: processing_attempts.EventId");

        Assert.False(result);
    }

    [Fact]
    public void MatchesSqlite_MessageMissesExpectedColumn_ReturnsFalse()
    {
        var constraint = new ExpectedUniqueConstraint(
            "IX_processing_attempts_InboxEventId_AttemptNumber",
            "processing_attempts",
            "InboxEventId",
            "AttemptNumber");

        var result = constraint.MatchesSqlite(
            "SQLite Error 19: UNIQUE constraint failed: processing_attempts.InboxEventId");

        Assert.False(result);
    }

    private static DbUpdateException CreateDbUpdateException(string sqlState, string constraintName)
    {
        return new DbUpdateException(
            "boom",
            new PostgresException(
                messageText: "boom",
                severity: "ERROR",
                invariantSeverity: "ERROR",
                sqlState: sqlState,
                constraintName: constraintName));
    }
}
