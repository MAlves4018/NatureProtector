using Microsoft.EntityFrameworkCore;
using NatureProtector.Prevention.Host.Processing;
using Npgsql;

namespace NatureProtector.Prevention.Host.Tests.Processing;

public sealed class DefaultProcessingFailureClassifierTests
{
    private readonly DefaultProcessingFailureClassifier _classifier = new();

    [Fact]
    public void Classify_ReturnsPermanent_ForPostgresConstraintViolations()
    {
        var classification = _classifier.Classify(
            CreateDbUpdateException(PostgresErrorCodes.ForeignKeyViolation));

        Assert.Equal(ProcessingFailureKind.Permanent, classification.Kind);
        Assert.Equal("db_foreign_key_violation", classification.ErrorCode);
    }

    [Fact]
    public void Classify_ReturnsPermanent_ForPostgresDataExceptions()
    {
        var classification = _classifier.Classify(
            CreateDbUpdateException("22P02"));

        Assert.Equal(ProcessingFailureKind.Permanent, classification.Kind);
        Assert.Equal("db_data_exception", classification.ErrorCode);
    }

    [Fact]
    public void Classify_ReturnsTransient_ForPostgresDeadlocks()
    {
        var classification = _classifier.Classify(
            CreateDbUpdateException(PostgresErrorCodes.DeadlockDetected));

        Assert.Equal(ProcessingFailureKind.Transient, classification.Kind);
        Assert.Equal("db_deadlock_detected", classification.ErrorCode);
    }

    [Fact]
    public void Classify_FallsBackToTransient_WhenProviderSpecificDetailsAreUnavailable()
    {
        var classification = _classifier.Classify(
            new DbUpdateException("boom", new InvalidOperationException("missing provider details")));

        Assert.Equal(ProcessingFailureKind.Transient, classification.Kind);
        Assert.Equal("db_update_failed", classification.ErrorCode);
    }

    private static DbUpdateException CreateDbUpdateException(string sqlState)
    {
        return new DbUpdateException(
            "boom",
            new PostgresException("boom", "ERROR", "ERROR", sqlState));
    }
}
