using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace NatureProtector.Prevention.Host.Processing;

/*
 * Este classificador converte exceções técnicas em categorias operacionais de
 * falha para a política de novas tentativas do fluxo operacional.
 *
 * Rationale:
 * - O fluxo operacional precisa de distinguir falhas transitórias de falhas
 *   permanentes antes de decidir entre nova tentativa e quarentena.
 * - Esta decisão não deve ficar espalhada pelos consumidores e services.
 *
 * Design considerations:
 * - Exceções de base de dados são analisadas mais a fundo para aproveitar os
 *   códigos SQLSTATE do PostgreSQL.
 * - Falhas desconhecidas ficam numa categoria intermédia para permitir retries
 *   controlados sem as declarar erroneamente como permanentes.
 */

public sealed class DefaultProcessingFailureClassifier : IProcessingFailureClassifier
{
    /// <summary>
    /// Classifica a exceção recebida para efeitos de nova tentativa ou quarentena.
    /// </summary>
    public ProcessingFailureClassification Classify(Exception exception)
    {
        ArgumentNullException.ThrowIfNull(exception);

        return exception switch
        {
            TimeoutException => new ProcessingFailureClassification(
                ProcessingFailureKind.Transient,
                "timeout"),

            HttpRequestException => new ProcessingFailureClassification(
                ProcessingFailureKind.Transient,
                "http_request_failed"),

            IOException => new ProcessingFailureClassification(
                ProcessingFailureKind.Transient,
                "io_failed"),

            DbUpdateException dbUpdateException => ClassifyDbUpdateException(dbUpdateException),

            ArgumentException => new ProcessingFailureClassification(
                ProcessingFailureKind.Permanent,
                "invalid_argument"),

            FormatException => new ProcessingFailureClassification(
                ProcessingFailureKind.Permanent,
                "invalid_format"),

            InvalidDataException => new ProcessingFailureClassification(
                ProcessingFailureKind.Permanent,
                "invalid_data"),

            NotSupportedException => new ProcessingFailureClassification(
                ProcessingFailureKind.Permanent,
                "not_supported"),

            OperationCanceledException => new ProcessingFailureClassification(
                ProcessingFailureKind.Transient,
                "operation_cancelled"),

            _ => new ProcessingFailureClassification(
                ProcessingFailureKind.Unknown,
                "processing_failed")
        };
    }

    /// <summary>
    /// Refina a classificação de falhas originadas durante atualizações na base
    /// de dados.
    /// </summary>
    private static ProcessingFailureClassification ClassifyDbUpdateException(DbUpdateException exception)
    {
        var postgresException = FindPostgresException(exception);

        if (postgresException is null)
        {
            return new ProcessingFailureClassification(
                ProcessingFailureKind.Transient,
                "db_update_failed");
        }

        return postgresException.SqlState switch
        {
            PostgresErrorCodes.ForeignKeyViolation => new ProcessingFailureClassification(
                ProcessingFailureKind.Permanent,
                "db_foreign_key_violation"),

            PostgresErrorCodes.UniqueViolation => new ProcessingFailureClassification(
                ProcessingFailureKind.Permanent,
                "db_unique_violation"),

            PostgresErrorCodes.CheckViolation => new ProcessingFailureClassification(
                ProcessingFailureKind.Permanent,
                "db_check_violation"),

            PostgresErrorCodes.NotNullViolation => new ProcessingFailureClassification(
                ProcessingFailureKind.Permanent,
                "db_not_null_violation"),

            PostgresErrorCodes.SerializationFailure => new ProcessingFailureClassification(
                ProcessingFailureKind.Transient,
                "db_serialization_failure"),

            PostgresErrorCodes.DeadlockDetected => new ProcessingFailureClassification(
                ProcessingFailureKind.Transient,
                "db_deadlock_detected"),

            PostgresErrorCodes.LockNotAvailable => new ProcessingFailureClassification(
                ProcessingFailureKind.Transient,
                "db_lock_not_available"),

            var sqlState when sqlState.StartsWith("08", StringComparison.Ordinal) => new ProcessingFailureClassification(
                ProcessingFailureKind.Transient,
                "db_connection_failed"),

            var sqlState when sqlState.StartsWith("22", StringComparison.Ordinal) => new ProcessingFailureClassification(
                ProcessingFailureKind.Permanent,
                "db_data_exception"),

            var sqlState when sqlState.StartsWith("23", StringComparison.Ordinal) => new ProcessingFailureClassification(
                ProcessingFailureKind.Permanent,
                "db_integrity_constraint_violation"),

            _ => new ProcessingFailureClassification(
                ProcessingFailureKind.Transient,
                "db_update_failed")
        };
    }

    /// <summary>
    /// Procura a primeira <see cref="PostgresException" /> presente na cadeia de
    /// exceções.
    /// </summary>
    private static PostgresException? FindPostgresException(Exception exception)
    {
        for (var current = exception; current is not null; current = current.InnerException)
        {
            if (current is PostgresException postgresException)
            {
                return postgresException;
            }
        }

        return null;
    }
}
