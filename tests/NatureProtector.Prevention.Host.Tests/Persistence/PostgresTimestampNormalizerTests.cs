using NatureProtector.Infrastructure.Postgres.Persistence;

namespace NatureProtector.Prevention.Host.Tests.Persistence;

public sealed class PostgresTimestampNormalizerTests
{
    [Fact]
    public void Normalize_ConvertsNonUtcOffsetToUtc()
    {
        var localTime = new DateTimeOffset(2020, 9, 13, 12, 0, 10, TimeSpan.FromHours(2));

        var normalized = PostgresTimestampNormalizer.Normalize(localTime);

        Assert.Equal(new DateTimeOffset(2020, 9, 13, 10, 0, 10, TimeSpan.Zero), normalized);
    }

    [Fact]
    public void Normalize_ReturnsNull_WhenNullableValueIsNull()
    {
        var normalized = PostgresTimestampNormalizer.Normalize((DateTimeOffset?)null);

        Assert.Null(normalized);
    }

    [Fact]
    public void Normalize_ConvertsNullableNonUtcOffsetToUtc()
    {
        DateTimeOffset? localTime = new DateTimeOffset(2020, 9, 13, 12, 0, 10, TimeSpan.FromHours(2));

        var normalized = PostgresTimestampNormalizer.Normalize(localTime);

        Assert.Equal(new DateTimeOffset(2020, 9, 13, 10, 0, 10, TimeSpan.Zero), normalized);
    }
}
