using NatureProtector.Prevention.Host.Persistence;
using NatureProtector.Prevention.Host.Tests.TestData;

namespace NatureProtector.Prevention.Host.Tests.Persistence;

public sealed class InMemoryAcceptedReadingRepositoryTests
{
    [Fact]
    public async Task AddAsync_Throws_WhenEnvelopeIsNull()
    {
        var repository = new InMemoryAcceptedReadingRepository();

        var ex = await Assert.ThrowsAsync<ArgumentNullException>(() => repository.AddAsync(
            envelope: null!,
            cancellationToken: CancellationToken.None));

        Assert.Equal("envelope", ex.ParamName);
    }

    [Fact]
    public async Task GetAllAsync_ReturnsEmptyCollection_WhenNothingWasStored()
    {
        var repository = new InMemoryAcceptedReadingRepository();

        var result = await repository.GetAllAsync(CancellationToken.None);

        Assert.Empty(result);
    }

    [Fact]
    public async Task GetAllAsync_ReturnsStoredEnvelopes_InInsertionOrder()
    {
        var repository = new InMemoryAcceptedReadingRepository();
        var first = EnvelopeFactory.Create(sensorName: "Sensor-A");
        var second = EnvelopeFactory.Create(sensorName: "Sensor-B");

        await repository.AddAsync(first, CancellationToken.None);
        await repository.AddAsync(second, CancellationToken.None);

        var result = await repository.GetAllAsync(CancellationToken.None);

        Assert.Equal(new[] { first.EventId, second.EventId }, result.Select(x => x.EventId));
    }

    [Fact]
    public async Task AddAsync_IgnoresDuplicateEventIds()
    {
        var repository = new InMemoryAcceptedReadingRepository();
        var envelope = EnvelopeFactory.Create(eventId: Guid.NewGuid(), sensorName: "Sensor-A");

        await repository.AddAsync(envelope, CancellationToken.None);
        await repository.AddAsync(envelope, CancellationToken.None);

        var result = await repository.GetAllAsync(CancellationToken.None);

        Assert.Single(result);
    }
}
