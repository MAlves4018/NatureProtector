using Microsoft.Extensions.Logging.Abstractions;
using NatureProtector.Prevention.Host.Processing;
using NatureProtector.Prevention.Host.Tests.TestData;
using NatureProtector.Prevention.Host.Tests.TestInfrastructure;

namespace NatureProtector.Prevention.Host.Tests.Processing;

public sealed class ReadingSemanticValidatorTests
{
    [Fact]
    public async Task ValidateAsync_ValidSensor_ReturnsValid()
    {
        await using var scope = new SqliteControlDbContextScope();
        var seed = await ControlPlaneSeedData.SeedAreaWithSensorAsync(scope);
        var validator = CreateValidator(scope);
        var envelope = EnvelopeFactory.Create(areaId: seed.AreaId, sensorId: seed.SensorId);

        var result = await validator.ValidateAsync(envelope, CancellationToken.None);

        Assert.True(result.IsValid);
        Assert.Equal(ReadingSemanticValidationReason.None, result.Reason);
    }

    [Fact]
    public async Task ValidateAsync_SensorNotFound_ReturnsInvalid()
    {
        await using var scope = new SqliteControlDbContextScope();
        var seed = await ControlPlaneSeedData.SeedAreaWithSensorAsync(scope);
        var validator = CreateValidator(scope);
        var envelope = EnvelopeFactory.Create(areaId: seed.AreaId, sensorId: Guid.NewGuid());

        var result = await validator.ValidateAsync(envelope, CancellationToken.None);

        Assert.False(result.IsValid);
        Assert.Equal(ReadingSemanticValidationReason.SensorNotFound, result.Reason);
        Assert.Equal("sensor_not_found", result.ReasonCode);
    }

    [Fact]
    public async Task ValidateAsync_SensorInactive_ReturnsInvalid()
    {
        await using var scope = new SqliteControlDbContextScope();
        var seed = await ControlPlaneSeedData.SeedAreaWithSensorAsync(scope, isActive: false);
        var validator = CreateValidator(scope);
        var envelope = EnvelopeFactory.Create(areaId: seed.AreaId, sensorId: seed.SensorId);

        var result = await validator.ValidateAsync(envelope, CancellationToken.None);

        Assert.False(result.IsValid);
        Assert.Equal(ReadingSemanticValidationReason.SensorInactive, result.Reason);
        Assert.Equal("sensor_inactive", result.ReasonCode);
    }

    [Fact]
    public async Task ValidateAsync_SensorAreaMismatch_ReturnsInvalid()
    {
        await using var scope = new SqliteControlDbContextScope();
        var areaA = await ControlPlaneSeedData.SeedAreaWithSensorAsync(scope);
        var areaB = await ControlPlaneSeedData.SeedAreaWithSensorAsync(scope);
        var validator = CreateValidator(scope);
        var envelope = EnvelopeFactory.Create(areaId: areaA.AreaId, sensorId: areaB.SensorId);

        var result = await validator.ValidateAsync(envelope, CancellationToken.None);

        Assert.False(result.IsValid);
        Assert.Equal(ReadingSemanticValidationReason.SensorAreaMismatch, result.Reason);
        Assert.Equal("sensor_area_mismatch", result.ReasonCode);
    }

    private static ReadingSemanticValidator CreateValidator(SqliteControlDbContextScope scope)
    {
        return new ReadingSemanticValidator(
            scope.Factory,
            NullLogger<ReadingSemanticValidator>.Instance);
    }
}
