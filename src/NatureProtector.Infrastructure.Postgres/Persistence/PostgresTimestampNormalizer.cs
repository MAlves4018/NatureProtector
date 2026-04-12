using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace NatureProtector.Infrastructure.Postgres.Persistence;

public static class PostgresTimestampNormalizer
{
    public static DateTimeOffset Normalize(DateTimeOffset value)
    {
        return value.ToUniversalTime();
    }

    public static DateTimeOffset? Normalize(DateTimeOffset? value)
    {
        return value?.ToUniversalTime();
    }
}

internal sealed class UtcDateTimeOffsetConverter : ValueConverter<DateTimeOffset, DateTimeOffset>
{
    public UtcDateTimeOffsetConverter()
        : base(
            value => PostgresTimestampNormalizer.Normalize(value),
            value => PostgresTimestampNormalizer.Normalize(value))
    {
    }
}

internal sealed class NullableUtcDateTimeOffsetConverter : ValueConverter<DateTimeOffset?, DateTimeOffset?>
{
    public NullableUtcDateTimeOffsetConverter()
        : base(
            value => PostgresTimestampNormalizer.Normalize(value),
            value => PostgresTimestampNormalizer.Normalize(value))
    {
    }
}
