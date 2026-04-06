using NatureProtector.Core.Primitives;
using NatureProtector.Core.Risk;
using Xunit;

namespace NatureProtector.Core.Tests.Risk;

/// <summary>
/// Unit tests for the RiskCell entity.
/// These tests cover construction invariants, containment behaviour,
/// risk updates and simple trend/history logic according to the current model.
/// </summary>
public class RiskCellTests
{
    [Fact]
    public void Ctor_AssignsProperties_WhenValid()
    {
        // Arrange
        var id = Guid.NewGuid();
        var areaId = Guid.NewGuid();
        var location = new Location(38.7167, -9.1333, 12.0);

        // Act
        var cell = new RiskCell(
            id: id,
            areaId: areaId,
            location: location,
            initialRiskLevel: RiskLevel.Low,
            cellId: " C-01 ");

        // Assert
        Assert.Equal(id, cell.Id);
        Assert.Equal(areaId, cell.AreaId);
        Assert.Same(location, cell.Location);
        Assert.Equal(RiskLevel.Low, cell.CurrentRiskLevel);
        Assert.Equal("C-01", cell.CellId);
        Assert.Null(cell.LastUpdatedAt);
        Assert.Empty(cell.History);
    }

    [Fact]
    public void Ctor_AssignsNullCellId_WhenNotProvided()
    {
        // Arrange
        var id = Guid.NewGuid();
        var areaId = Guid.NewGuid();
        var location = new Location(38.7167, -9.1333);

        // Act
        var cell = new RiskCell(
            id: id,
            areaId: areaId,
            location: location,
            initialRiskLevel: RiskLevel.Moderate);

        // Assert
        Assert.Equal(id, cell.Id);
        Assert.Equal(areaId, cell.AreaId);
        Assert.Same(location, cell.Location);
        Assert.Equal(RiskLevel.Moderate, cell.CurrentRiskLevel);
        Assert.Null(cell.CellId);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Ctor_NormalizesWhitespaceCellId_ToNull(string? rawCellId)
    {
        // Arrange
        var id = Guid.NewGuid();
        var areaId = Guid.NewGuid();
        var location = new Location(38.7167, -9.1333);

        // Act
        var cell = new RiskCell(
            id: id,
            areaId: areaId,
            location: location,
            initialRiskLevel: RiskLevel.Low,
            cellId: rawCellId);

        // Assert
        Assert.Null(cell.CellId);
    }

    [Fact]
    public void Ctor_StoresInitialTimestamp_InHistory()
    {
        // Arrange
        var timestamp = DateTimeOffset.UtcNow;
        var location = new Location(38.7167, -9.1333);

        // Act
        var cell = new RiskCell(
            id: Guid.NewGuid(),
            areaId: Guid.NewGuid(),
            location: location,
            initialRiskLevel: RiskLevel.High,
            initialTimestamp: timestamp);

        // Assert
        Assert.Equal(timestamp, cell.LastUpdatedAt);
        Assert.Single(cell.History);
        Assert.Equal(timestamp, cell.History[0].Timestamp);
        Assert.Equal(RiskLevel.High, cell.History[0].Level);
    }

    [Fact]
    public void Ctor_Throws_WhenIdIsEmpty()
    {
        // Arrange
        var location = new Location(38.7167, -9.1333);

        // Act
        var ex = Assert.Throws<ArgumentException>(
            () => new RiskCell(
                id: Guid.Empty,
                areaId: Guid.NewGuid(),
                location: location,
                initialRiskLevel: RiskLevel.Low));

        // Assert
        Assert.Equal("id", ex.ParamName);
        Assert.Contains("must not be an empty GUID", ex.Message);
    }

    [Fact]
    public void Ctor_Throws_WhenAreaIdIsEmpty()
    {
        // Arrange
        var location = new Location(38.7167, -9.1333);

        // Act
        var ex = Assert.Throws<ArgumentException>(
            () => new RiskCell(
                id: Guid.NewGuid(),
                areaId: Guid.Empty,
                location: location,
                initialRiskLevel: RiskLevel.Low));

        // Assert
        Assert.Equal("areaId", ex.ParamName);
        Assert.Contains("must not be an empty GUID", ex.Message);
    }

    [Fact]
    public void Ctor_Throws_WhenLocationIsNull()
    {
        // Act
        var ex = Assert.Throws<ArgumentNullException>(
            () => new RiskCell(
                id: Guid.NewGuid(),
                areaId: Guid.NewGuid(),
                location: null!,
                initialRiskLevel: RiskLevel.Low));

        // Assert
        Assert.Equal("location", ex.ParamName);
    }

    [Fact]
    public void Contains_Throws_WhenLocationIsNull()
    {
        // Arrange
        var cell = new RiskCell(
            id: Guid.NewGuid(),
            areaId: Guid.NewGuid(),
            location: new Location(38.7167, -9.1333),
            initialRiskLevel: RiskLevel.Low);

        // Act
        var ex = Assert.Throws<ArgumentNullException>(() => cell.Contains(null!));

        // Assert
        Assert.Equal("location", ex.ParamName);
    }

    [Fact]
    public void Contains_ReturnsTrue_ForSameLocation()
    {
        // Arrange
        var location = new Location(38.7167, -9.1333);
        var cell = new RiskCell(
            id: Guid.NewGuid(),
            areaId: Guid.NewGuid(),
            location: location,
            initialRiskLevel: RiskLevel.Low);

        // Act
        var result = cell.Contains(location);

        // Assert
        Assert.True(result);
    }

    [Fact]
    public void Contains_ReturnsTrue_ForMatchingCellId()
    {
        // Arrange
        var cell = new RiskCell(
            id: Guid.NewGuid(),
            areaId: Guid.NewGuid(),
            location: new Location(38.7167, -9.1333),
            initialRiskLevel: RiskLevel.Low,
            cellId: "CELL-01");

        var queryLocation = new Location(40.0000, -8.0000, cellId: "cell-01");

        // Act
        var result = cell.Contains(queryLocation);

        // Assert
        Assert.True(result);
    }

    [Fact]
    public void Contains_ReturnsTrue_ForNearbyLocation_WithinDefaultRadius()
    {
        // Arrange
        var anchor = new Location(38.7167, -9.1333);

        // ~11 meters north, inside the 25 meter radius.
        var nearby = new Location(38.7168, -9.1333);

        var cell = new RiskCell(
            id: Guid.NewGuid(),
            areaId: Guid.NewGuid(),
            location: anchor,
            initialRiskLevel: RiskLevel.Low);

        // Act
        var result = cell.Contains(nearby);

        // Assert
        Assert.True(result);
    }

    [Fact]
    public void Contains_ReturnsFalse_ForFarLocation_OutsideDefaultRadius()
    {
        // Arrange
        var anchor = new Location(38.7167, -9.1333);

        // ~110 meters north, outside the 25 meter radius.
        var far = new Location(38.7177, -9.1333);

        var cell = new RiskCell(
            id: Guid.NewGuid(),
            areaId: Guid.NewGuid(),
            location: anchor,
            initialRiskLevel: RiskLevel.Low);

        // Act
        var result = cell.Contains(far);

        // Assert
        Assert.False(result);
    }

    [Fact]
    public void UpdateRiskLevel_UpdatesCurrentState_AndAppendsHistory()
    {
        // Arrange
        var initialTimestamp = DateTimeOffset.UtcNow.AddMinutes(-10);
        var updatedAt = DateTimeOffset.UtcNow;

        var cell = new RiskCell(
            id: Guid.NewGuid(),
            areaId: Guid.NewGuid(),
            location: new Location(38.7167, -9.1333),
            initialRiskLevel: RiskLevel.Low,
            initialTimestamp: initialTimestamp);

        // Act
        cell.UpdateRiskLevel(RiskLevel.High, updatedAt);

        // Assert
        Assert.Equal(RiskLevel.High, cell.CurrentRiskLevel);
        Assert.Equal(updatedAt, cell.LastUpdatedAt);
        Assert.Equal(2, cell.History.Count);
        Assert.Equal(RiskLevel.High, cell.History[^1].Level);
        Assert.Equal(updatedAt, cell.History[^1].Timestamp);
    }

    [Fact]
    public void UpdateRiskLevel_Throws_WhenTimestampIsDefault()
    {
        // Arrange
        var cell = new RiskCell(
            id: Guid.NewGuid(),
            areaId: Guid.NewGuid(),
            location: new Location(38.7167, -9.1333),
            initialRiskLevel: RiskLevel.Low);

        // Act
        var ex = Assert.Throws<ArgumentException>(
            () => cell.UpdateRiskLevel(RiskLevel.High, default));

        // Assert
        Assert.Equal("updatedAt", ex.ParamName);
        Assert.Contains("non-default timestamp", ex.Message);
    }

    [Fact]
    public void UpdateRiskLevel_Throws_WhenTimestampGoesBackwards()
    {
        // Arrange
        var initialTimestamp = DateTimeOffset.UtcNow;
        var olderTimestamp = initialTimestamp.AddMinutes(-1);

        var cell = new RiskCell(
            id: Guid.NewGuid(),
            areaId: Guid.NewGuid(),
            location: new Location(38.7167, -9.1333),
            initialRiskLevel: RiskLevel.Low,
            initialTimestamp: initialTimestamp);

        // Act
        var ex = Assert.Throws<InvalidOperationException>(
            () => cell.UpdateRiskLevel(RiskLevel.High, olderTimestamp));

        // Assert
        Assert.Contains("cannot be earlier than last update", ex.Message);
    }

    [Fact]
    public void GetRiskTrendDescription_ReturnsUnknown_WhenHistoryIsInsufficient()
    {
        // Arrange
        var cell = new RiskCell(
            id: Guid.NewGuid(),
            areaId: Guid.NewGuid(),
            location: new Location(38.7167, -9.1333),
            initialRiskLevel: RiskLevel.Low);

        // Act
        var trend = cell.GetRiskTrendDescription();

        // Assert
        Assert.Equal("Unknown or insufficient data", trend);
    }

    [Fact]
    public void GetRiskTrendDescription_ReturnsIncreasing_WhenLastLevelIsHigherThanFirst()
    {
        // Arrange
        var t0 = DateTimeOffset.UtcNow.AddMinutes(-10);
        var t1 = DateTimeOffset.UtcNow;

        var cell = new RiskCell(
            id: Guid.NewGuid(),
            areaId: Guid.NewGuid(),
            location: new Location(38.7167, -9.1333),
            initialRiskLevel: RiskLevel.Low,
            initialTimestamp: t0);

        cell.UpdateRiskLevel(RiskLevel.High, t1);

        // Act
        var trend = cell.GetRiskTrendDescription();

        // Assert
        Assert.Equal("Increasing", trend);
    }

    [Fact]
    public void GetRiskTrendDescription_ReturnsDecreasing_WhenLastLevelIsLowerThanFirst()
    {
        // Arrange
        var t0 = DateTimeOffset.UtcNow.AddMinutes(-10);
        var t1 = DateTimeOffset.UtcNow;

        var cell = new RiskCell(
            id: Guid.NewGuid(),
            areaId: Guid.NewGuid(),
            location: new Location(38.7167, -9.1333),
            initialRiskLevel: RiskLevel.High,
            initialTimestamp: t0);

        cell.UpdateRiskLevel(RiskLevel.Low, t1);

        // Act
        var trend = cell.GetRiskTrendDescription();

        // Assert
        Assert.Equal("Decreasing", trend);
    }

    [Fact]
    public void GetRiskTrendDescription_ReturnsStable_WhenHistoryLevelsNeverChange()
    {
        // Arrange
        var t0 = DateTimeOffset.UtcNow.AddMinutes(-10);
        var t1 = DateTimeOffset.UtcNow.AddMinutes(-5);
        var t2 = DateTimeOffset.UtcNow;

        var cell = new RiskCell(
            id: Guid.NewGuid(),
            areaId: Guid.NewGuid(),
            location: new Location(38.7167, -9.1333),
            initialRiskLevel: RiskLevel.Moderate,
            initialTimestamp: t0);

        cell.UpdateRiskLevel(RiskLevel.Moderate, t1);
        cell.UpdateRiskLevel(RiskLevel.Moderate, t2);

        // Act
        var trend = cell.GetRiskTrendDescription();

        // Assert
        Assert.Equal("Stable", trend);
    }

    [Fact]
    public void GetRiskTrendDescription_ReturnsVariableButOverallStable_WhenLevelsVaryButFirstEqualsLast()
    {
        // Arrange
        var t0 = DateTimeOffset.UtcNow.AddMinutes(-10);
        var t1 = DateTimeOffset.UtcNow.AddMinutes(-5);
        var t2 = DateTimeOffset.UtcNow;

        var cell = new RiskCell(
            id: Guid.NewGuid(),
            areaId: Guid.NewGuid(),
            location: new Location(38.7167, -9.1333),
            initialRiskLevel: RiskLevel.Moderate,
            initialTimestamp: t0);

        cell.UpdateRiskLevel(RiskLevel.High, t1);
        cell.UpdateRiskLevel(RiskLevel.Moderate, t2);

        // Act
        var trend = cell.GetRiskTrendDescription();

        // Assert
        Assert.Equal("Variable but overall stable", trend);
    }

    [Fact]
    public void IsAtLeast_AndIsAbove_ReturnExpectedValues()
    {
        // Arrange
        var cell = new RiskCell(
            id: Guid.NewGuid(),
            areaId: Guid.NewGuid(),
            location: new Location(38.7167, -9.1333),
            initialRiskLevel: RiskLevel.High);

        // Act / Assert
        Assert.True(cell.IsAtLeast(RiskLevel.Moderate));
        Assert.True(cell.IsAtLeast(RiskLevel.High));
        Assert.False(cell.IsAtLeast(RiskLevel.VeryHigh));

        Assert.True(cell.IsAbove(RiskLevel.Moderate));
        Assert.False(cell.IsAbove(RiskLevel.High));
    }

    [Fact]
    public void HasBecomeSaferComparedTo_ReturnsTrue_WhenCurrentRiskIsLower()
    {
        // Arrange
        var id = Guid.NewGuid();
        var areaId = Guid.NewGuid();
        var location = new Location(38.7167, -9.1333);

        var previous = new RiskCell(
            id: id,
            areaId: areaId,
            location: location,
            initialRiskLevel: RiskLevel.High);

        var current = new RiskCell(
            id: id,
            areaId: areaId,
            location: location,
            initialRiskLevel: RiskLevel.Low);

        // Act / Assert
        Assert.True(current.HasBecomeSaferComparedTo(previous));
        Assert.False(previous.HasBecomeSaferComparedTo(current));
    }

    [Fact]
    public void HasBecomeSaferComparedTo_Throws_WhenCellsDoNotRepresentSameLogicalCell()
    {
        // Arrange
        var left = new RiskCell(
            id: Guid.NewGuid(),
            areaId: Guid.NewGuid(),
            location: new Location(38.7167, -9.1333),
            initialRiskLevel: RiskLevel.High);

        var right = new RiskCell(
            id: Guid.NewGuid(),
            areaId: Guid.NewGuid(),
            location: new Location(38.7167, -9.1333),
            initialRiskLevel: RiskLevel.Low);

        // Act
        var ex = Assert.Throws<InvalidOperationException>(
            () => right.HasBecomeSaferComparedTo(left));

        // Assert
        Assert.Contains("same risk cell", ex.Message);
    }
}
