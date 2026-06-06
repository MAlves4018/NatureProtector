using Microsoft.EntityFrameworkCore;
using NatureProtector.Prevention.Host.Processing;
using Npgsql;

namespace NatureProtector.Prevention.Host.Tests.Processing;

public sealed class DefaultProcessingFailureClassifierTests
{
    private readonly DefaultProcessingFailureClassifier _classifier = new();

    [Theory]
    [MemberData(nameof(TopLevelExceptionCases))]
    public void Classify_KnownTopLevelException_ReturnsExpectedClassification(
        Exception exception,
        ProcessingFailureKind expectedKind,
        string expectedErrorCode,
        bool expectedRetryable)
    {
        var classification = _classifier.Classify(exception);

        Assert.Equal(expectedKind, classification.Kind);
        Assert.Equal(expectedErrorCode, classification.ErrorCode);
        Assert.Equal(expectedRetryable, classification.IsRetryable);
    }

    [Fact]
    public void Classify_NullException_ThrowsArgumentNullException()
    {
        var exception = Assert.Throws<ArgumentNullException>(() => _classifier.Classify(null!));

        Assert.Equal("exception", exception.ParamName);
    }

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

    [Theory]
    [InlineData(PostgresErrorCodes.UniqueViolation, ProcessingFailureKind.Permanent, "db_unique_violation", false)]
    [InlineData(PostgresErrorCodes.CheckViolation, ProcessingFailureKind.Permanent, "db_check_violation", false)]
    [InlineData(PostgresErrorCodes.NotNullViolation, ProcessingFailureKind.Permanent, "db_not_null_violation", false)]
    [InlineData(PostgresErrorCodes.SerializationFailure, ProcessingFailureKind.Transient, "db_serialization_failure", true)]
    [InlineData(PostgresErrorCodes.LockNotAvailable, ProcessingFailureKind.Transient, "db_lock_not_available", true)]
    [InlineData("08006", ProcessingFailureKind.Transient, "db_connection_failed", true)]
    [InlineData("23000", ProcessingFailureKind.Permanent, "db_integrity_constraint_violation", false)]
    [InlineData("99999", ProcessingFailureKind.Transient, "db_update_failed", true)]
    public void Classify_PostgresSqlState_ReturnsExpectedClassification(
        string sqlState,
        ProcessingFailureKind expectedKind,
        string expectedErrorCode,
        bool expectedRetryable)
    {
        var classification = _classifier.Classify(CreateDbUpdateException(sqlState));

        Assert.Equal(expectedKind, classification.Kind);
        Assert.Equal(expectedErrorCode, classification.ErrorCode);
        Assert.Equal(expectedRetryable, classification.IsRetryable);
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

    public static TheoryData<Exception, ProcessingFailureKind, string, bool> TopLevelExceptionCases()
    {
        return new TheoryData<Exception, ProcessingFailureKind, string, bool>
        {
            { new TimeoutException("timeout"), ProcessingFailureKind.Transient, "timeout", true },
            { new ControlledValidationProcessingFaultException(ProcessingFailureKind.Transient, "transient_failure", "controlled"), ProcessingFailureKind.Transient, "transient_failure", true },
            { new ControlledValidationProcessingFaultException(ProcessingFailureKind.Permanent, "permanent_failure", "controlled"), ProcessingFailureKind.Permanent, "permanent_failure", false },
            { new HttpRequestException("http failed"), ProcessingFailureKind.Transient, "http_request_failed", true },
            { new IOException("io failed"), ProcessingFailureKind.Transient, "io_failed", true },
            { new ArgumentException("bad argument"), ProcessingFailureKind.Permanent, "invalid_argument", false },
            { new FormatException("bad format"), ProcessingFailureKind.Permanent, "invalid_format", false },
            { new InvalidDataException("bad data"), ProcessingFailureKind.Permanent, "invalid_data", false },
            { new NotSupportedException("unsupported"), ProcessingFailureKind.Permanent, "not_supported", false },
            { new OperationCanceledException("cancelled"), ProcessingFailureKind.Transient, "operation_cancelled", true },
            { new InvalidOperationException("unknown"), ProcessingFailureKind.Unknown, "processing_failed", true }
        };
    }
}
