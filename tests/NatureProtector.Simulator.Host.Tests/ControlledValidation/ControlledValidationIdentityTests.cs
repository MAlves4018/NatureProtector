using System.Text;
using NatureProtector.Simulator.Host.ControlledValidation;

namespace NatureProtector.Simulator.Host.Tests.ControlledValidation;

public sealed class ControlledValidationIdentityTests
{
    [Fact]
    public void CreateCorrelationId_IsDeterministic()
    {
        var first = ControlledValidationIdentity.CreateCorrelationId(
            "p0-smoke",
            ControlledValidationFaultCaseIds.N2InvalidOperationalState,
            3);
        var second = ControlledValidationIdentity.CreateCorrelationId(
            "p0-smoke",
            ControlledValidationFaultCaseIds.N2InvalidOperationalState,
            3);

        Assert.Equal("cv:p0-smoke:N2_INVALID_OPERATIONAL_STATE:003", first);
        Assert.Equal(first, second);
    }

    [Fact]
    public void CreateCorrelationId_Throws_WhenSequenceIsInvalid()
    {
        var ex = Assert.Throws<ArgumentOutOfRangeException>(() =>
            ControlledValidationIdentity.CreateCorrelationId(
                "p0-smoke",
                ControlledValidationFaultCaseIds.N1InvalidJson,
                0));

        Assert.Equal("sequence", ex.ParamName);
    }

    [Fact]
    public void CreateDeterministicGuid_IsStableForSameValue()
    {
        var first = ControlledValidationIdentity.CreateDeterministicGuid("controlled-validation:p0");
        var second = ControlledValidationIdentity.CreateDeterministicGuid("controlled-validation:p0");

        Assert.NotEqual(Guid.Empty, first);
        Assert.Equal(first, second);
    }

    [Fact]
    public void ComputeRawBodySha256_IsDeterministic()
    {
        var body = Encoding.UTF8.GetBytes("{ invalid");

        var first = ControlledValidationIdentity.ComputeRawBodySha256(body);
        var second = ControlledValidationIdentity.ComputeRawBodySha256(body);

        Assert.Equal(64, first.Length);
        Assert.Equal(first, second);
    }
}
