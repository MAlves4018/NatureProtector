using NatureProtector.Simulator.Host.Services;

namespace NatureProtector.Simulator.Host.Tests.Services;

public sealed class SeedProviderTests
{
    private readonly SeedProvider _provider = new();

    [Fact]
    public void ResolveSeed_ReturnsConfiguredSeed_WhenProvided()
    {
        var seed = _provider.ResolveSeed(4242);

        Assert.Equal(4242, seed);
    }

    [Fact]
    public void ResolveSeed_GeneratesPositiveSeed_WhenNotProvided()
    {
        var seed = _provider.ResolveSeed(null);

        Assert.InRange(seed, 1, int.MaxValue - 1);
    }

    [Fact]
    public void CreateRandom_UsesSeedDeterministically()
    {
        var first = _provider.CreateRandom(2026);
        var second = _provider.CreateRandom(2026);

        var firstSequence = Enumerable.Range(0, 5).Select(_ => first.Next()).ToArray();
        var secondSequence = Enumerable.Range(0, 5).Select(_ => second.Next()).ToArray();

        Assert.Equal(firstSequence, secondSequence);
    }
}
