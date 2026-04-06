using NatureProtector.Core.Communication;
using NatureProtector.Core.Primitives;

namespace NatureProtector.Core.Tests.Communication;

public sealed class AlertTests
{
    [Fact]
    public void Ctor_Throws_WhenIdIsEmpty()
    {
        var ex = Assert.Throws<ArgumentException>(() => new Alert(
            id: Guid.Empty,
            severity: Severity.High,
            message: "Alert",
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
        var ex = Assert.Throws<ArgumentException>(() => new Alert(
            id: Guid.NewGuid(),
            severity: Severity.High,
            message: message!,
            createdAt: DateTimeOffset.UtcNow));

        Assert.Equal("message", ex.ParamName);
        Assert.Contains("must not be null or whitespace", ex.Message);
    }

    [Fact]
    public void Ctor_Throws_WhenCreatedAtIsDefault()
    {
        var ex = Assert.Throws<ArgumentException>(() => new Alert(
            id: Guid.NewGuid(),
            severity: Severity.High,
            message: "Alert",
            createdAt: default));

        Assert.Equal("createdAt", ex.ParamName);
        Assert.Contains("must be a valid, non-default value", ex.Message);
    }

    [Fact]
    public void Ctor_TrimsMessage_AndStartsUnacknowledged()
    {
        var createdAt = DateTimeOffset.UtcNow;
        var alert = new Alert(
            id: Guid.NewGuid(),
            severity: Severity.Critical,
            message: "  Evacuate area  ",
            createdAt: createdAt);

        Assert.Equal(Severity.Critical, alert.Severity);
        Assert.Equal("Evacuate area", alert.Message);
        Assert.Equal(createdAt, alert.CreatedAt);
        Assert.False(alert.IsAcknowledged);
    }

    [Fact]
    public void Acknowledge_IsIdempotent()
    {
        var alert = new Alert(
            id: Guid.NewGuid(),
            severity: Severity.High,
            message: "Alert",
            createdAt: DateTimeOffset.UtcNow);

        alert.Acknowledge();
        alert.Acknowledge();

        Assert.True(alert.IsAcknowledged);
    }
}
