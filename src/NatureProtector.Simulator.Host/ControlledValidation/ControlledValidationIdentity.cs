using System.Security.Cryptography;
using System.Text;

namespace NatureProtector.Simulator.Host.ControlledValidation;

public static class ControlledValidationIdentity
{
    public static string CreateCorrelationId(
        string runLabel,
        string faultCaseId,
        int sequence)
    {
        if (string.IsNullOrWhiteSpace(runLabel))
        {
            throw new ArgumentException("run_label is required.", nameof(runLabel));
        }

        if (string.IsNullOrWhiteSpace(faultCaseId))
        {
            throw new ArgumentException("fault_case_id is required.", nameof(faultCaseId));
        }

        if (sequence <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(sequence), sequence, "sequence must be greater than zero.");
        }

        return $"cv:{runLabel}:{faultCaseId}:{sequence:000}";
    }

    public static Guid CreateDeterministicGuid(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException("value is required.", nameof(value));
        }

        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(value));
        Span<byte> bytes = stackalloc byte[16];
        hash.AsSpan(0, 16).CopyTo(bytes);

        bytes[7] = (byte)((bytes[7] & 0x0F) | 0x50);
        bytes[8] = (byte)((bytes[8] & 0x3F) | 0x80);

        return new Guid(bytes);
    }

    public static string ComputeRawBodySha256(byte[] rawBody)
    {
        ArgumentNullException.ThrowIfNull(rawBody);

        var hash = SHA256.HashData(rawBody);
        return Convert.ToHexString(hash).ToLowerInvariant();
    }
}
