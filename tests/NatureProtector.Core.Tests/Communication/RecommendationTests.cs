using NatureProtector.Core.Communication;

namespace NatureProtector.Core.Tests.Communication;

public sealed class RecommendationTests
{
    [Fact]
    public void Ctor_Throws_WhenIdIsEmpty()
    {
        var ex = Assert.Throws<ArgumentException>(() => new Recommendation(
            id: Guid.Empty,
            message: "Recommendation",
            priority: 1,
            createdAt: DateTimeOffset.UtcNow));

        Assert.Equal("id", ex.ParamName);
        Assert.Contains("must not be an empty GUID", ex.Message);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Ctor_Throws_WhenMessageIsNullOrWhitespace(string? message)
    {
        var ex = Assert.Throws<ArgumentException>(() => new Recommendation(
            id: Guid.NewGuid(),
            message: message!,
            priority: 1,
            createdAt: DateTimeOffset.UtcNow));

        Assert.Equal("message", ex.ParamName);
        Assert.Contains("must not be null or whitespace", ex.Message);
    }

    [Fact]
    public void Ctor_Throws_WhenPriorityIsNegative()
    {
        var ex = Assert.Throws<ArgumentOutOfRangeException>(() => new Recommendation(
            id: Guid.NewGuid(),
            message: "Recommendation",
            priority: -1,
            createdAt: DateTimeOffset.UtcNow));

        Assert.Equal("priority", ex.ParamName);
        Assert.Contains("greater than or equal to zero", ex.Message);
    }

    [Fact]
    public void Ctor_Throws_WhenCreatedAtIsDefault()
    {
        var ex = Assert.Throws<ArgumentException>(() => new Recommendation(
            id: Guid.NewGuid(),
            message: "Recommendation",
            priority: 1,
            createdAt: default));

        Assert.Equal("createdAt", ex.ParamName);
        Assert.Contains("must be a valid, non-default value", ex.Message);
    }

    [Fact]
    public void Ctor_TrimsMessage_AndPreservesPriority()
    {
        var createdAt = DateTimeOffset.UtcNow;
        var recommendation = new Recommendation(
            id: Guid.NewGuid(),
            message: "  Inspect the northern cell  ",
            priority: 3,
            createdAt: createdAt);

        Assert.Equal("Inspect the northern cell", recommendation.Message);
        Assert.Equal(3, recommendation.Priority);
        Assert.Equal(createdAt, recommendation.CreatedAt);
    }
}
