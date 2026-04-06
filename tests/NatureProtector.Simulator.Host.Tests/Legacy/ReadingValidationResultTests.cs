using NatureProtector.Prevention.Host.Validation;

namespace NatureProtector.Simulator.Host.Tests.Legacy;

public sealed class ReadingValidationResultTests
{
    [Fact]
    public void Accept_ReturnsAcceptedResultWithoutReason()
    {
        var result = ReadingValidationResult.Accept();

        Assert.True(result.IsAccepted);
        Assert.Null(result.RejectionReason);
    }

    [Fact]
    public void Reject_Throws_WhenReasonIsBlank()
    {
        var ex = Assert.Throws<ArgumentException>(() => ReadingValidationResult.Reject("   "));

        Assert.Equal("reason", ex.ParamName);
    }

    [Fact]
    public void Reject_TrimsReason_AndMarksResultAsRejected()
    {
        var result = ReadingValidationResult.Reject("  Invalid payload  ");

        Assert.False(result.IsAccepted);
        Assert.Equal("Invalid payload", result.RejectionReason);
    }
}
