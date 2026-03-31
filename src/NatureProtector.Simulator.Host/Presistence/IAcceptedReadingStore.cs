/*
 * This interface defines the persistence contract for accepted readings.
 *
 * Rationale:
 * - The ingestion worker should not depend directly on file persistence details.
 * - This abstraction allows the Day 5 file-based persistence to be replaced later
 *   by a database-backed implementation without changing the consumer workflow.
 */

namespace NatureProtector.Prevention.Host.Persistence;

public interface IAcceptedReadingStore
{
    /// <summary>
    /// Persists one accepted reading.
    /// </summary>
    void Persist(AcceptedReadingRecord record);
}