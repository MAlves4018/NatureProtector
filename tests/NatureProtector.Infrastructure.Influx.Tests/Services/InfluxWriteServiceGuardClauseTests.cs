using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NatureProtector.Core.Risk;
using NatureProtector.Infrastructure.Influx.Configuration;
using NatureProtector.Infrastructure.Influx.Services;

namespace NatureProtector.Infrastructure.Influx.Tests.Services;

public sealed class InfluxWriteServiceGuardClauseTests
{
    [Fact]
    public async Task WriteAcceptedReadingAsync_Throws_WhenEnvelopeIsNull()
    {
        using var service = CreateService();

        var ex = await Assert.ThrowsAsync<ArgumentNullException>(() => service.WriteAcceptedReadingAsync(
            envelope: null!,
            cancellationToken: CancellationToken.None));

        Assert.Equal("envelope", ex.ParamName);
    }

    [Fact]
    public async Task WriteRiskAssessmentAsync_Throws_WhenAssessmentIsNull()
    {
        using var service = CreateService();

        var ex = await Assert.ThrowsAsync<ArgumentNullException>(() => service.WriteRiskAssessmentAsync(
            areaId: Guid.NewGuid(),
            sensorId: Guid.NewGuid(),
            assessment: null!,
            cancellationToken: CancellationToken.None));

        Assert.Equal("assessment", ex.ParamName);
    }

    [Fact]
    public async Task WriteAreaRiskSnapshotAsync_Throws_WhenSnapshotIsNull()
    {
        using var service = CreateService();

        var ex = await Assert.ThrowsAsync<ArgumentNullException>(() => service.WriteAreaRiskSnapshotAsync(
            areaId: Guid.NewGuid(),
            assessmentCount: 1,
            snapshot: null!,
            cancellationToken: CancellationToken.None));

        Assert.Equal("snapshot", ex.ParamName);
    }

    [Fact]
    public void Dispose_CanBeCalledMultipleTimes()
    {
        using var service = CreateService();

        service.Dispose();
        service.Dispose();
    }

    private static InfluxWriteService CreateService()
    {
        return new InfluxWriteService(Options.Create(new InfluxDbOptions
        {
            Url = "http://localhost:8086",
            Token = "token",
            Organization = "org",
            Bucket = "bucket"
        }),
        NullLogger<InfluxWriteService>.Instance);
    }
}
