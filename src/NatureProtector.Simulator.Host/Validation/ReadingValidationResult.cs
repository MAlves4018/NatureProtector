/*
 * This record represents the outcome of a simple reading validation.
 *
 * Rationale:
 * - Validation should return an explicit result instead of throwing for normal
 *   business rejections.
 * - This makes the worker logic clearer: accepted readings are persisted,
 *   rejected readings are logged and acknowledged.
 */

namespace NatureProtector.Prevention.Host.Validation;

public sealed record ReadingValidationResult(bool IsAccepted, string? RejectionReason)
{
    public static ReadingValidationResult Accept() => new(true, null);

    public static ReadingValidationResult Reject(string reason)
    {
        if (string.IsNullOrWhiteSpace(reason))
        {
            throw new ArgumentException(
                "Rejection reason must not be null or whitespace.",
                nameof(reason));
        }

        return new ReadingValidationResult(false, reason.Trim());
    }
}