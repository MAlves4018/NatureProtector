using NatureProtector.Core.Primitives;
using NatureProtector.Core.Risk;
using Xunit;

namespace NatureProtector.Core.Tests.Areas;

/// <summary>
/// Unit tests for the Area aggregate.
/// These tests cover construction invariants, risk cell management,
/// location-based queries and risk grid replacement.
/// </summary>
public class AreaTests
{
    [Fact]
    public void Ctor_Throws_WhenIdIsEmpty()
    {
        var boundaries = CreateBoundaries();

        var ex = Assert.Throws<ArgumentException>(
            () => new NatureProtector.Core.Areas.Area(
                id: Guid.Empty,
                name: "Area A",
                boundaries: boundaries));

        Assert.Equal("id", ex.ParamName);
        Assert.Contains("must not be an empty GUID", ex.Message);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Ctor_Throws_WhenNameIsNullOrWhitespace(string? rawName)
    {
        var ex = Assert.Throws<ArgumentException>(
            () => new NatureProtector.Core.Areas.Area(
                id: Guid.NewGuid(),
                name: rawName!,
                boundaries: CreateBoundaries()));

        Assert.Equal("name", ex.ParamName);
        Assert.Contains("must not be null or whitespace", ex.Message);
    }

    [Fact]
    public void Ctor_Throws_WhenBoundariesIsNull()
    {
        var ex = Assert.Throws<ArgumentNullException>(
            () => new NatureProtector.Core.Areas.Area(
                id: Guid.NewGuid(),
                name: "Area A",
                boundaries: null!));

        Assert.Equal("boundaries", ex.ParamName);
    }

    [Fact]
    public void Ctor_TrimsName_AndInitializesEmptyRiskGrid()
    {
        var id = Guid.NewGuid();
        var boundaries = CreateBoundaries();

        var area = new NatureProtector.Core.Areas.Area(
            id: id,
            name: "  Serra A  ",
            boundaries: boundaries);

        Assert.Equal(id, area.Id);
        Assert.Equal("Serra A", area.Name);
        Assert.Same(boundaries, area.Boundaries);
        Assert.Empty(area.RiskCells);
    }

    [Fact]
    public void Ctor_Throws_WhenInitialRiskCellsBelongToDifferentArea()
    {
        var areaId = Guid.NewGuid();
        var wrongAreaId = Guid.NewGuid();

        var ex = Assert.Throws<ArgumentException>(
            () => new NatureProtector.Core.Areas.Area(
                id: areaId,
                name: "Area A",
                boundaries: CreateBoundaries(),
                riskCells: new[]
                {
                    CreateRiskCell(areaId),
                    CreateRiskCell(wrongAreaId)
                }));

        Assert.Equal("riskCells", ex.ParamName);
        Assert.Contains("must belong to the same AreaId", ex.Message);
    }

    [Fact]
    public void AddRiskCell_Throws_WhenRiskCellIsNull()
    {
        var area = CreateArea();

        var ex = Assert.Throws<ArgumentNullException>(() => area.AddRiskCell(null!));

        Assert.Equal("riskCell", ex.ParamName);
    }

    [Fact]
    public void AddRiskCell_Throws_WhenRiskCellBelongsToAnotherArea()
    {
        var area = CreateArea();
        var otherCell = CreateRiskCell(Guid.NewGuid());

        var ex = Assert.Throws<InvalidOperationException>(() => area.AddRiskCell(otherCell));

        Assert.Contains("does not belong to area", ex.Message);
    }

    [Fact]
    public void AddRiskCell_AddsCell_AndIgnoresDuplicateById()
    {
        var area = CreateArea();
        var cell = CreateRiskCell(area.Id);

        area.AddRiskCell(cell);
        area.AddRiskCell(cell);

        Assert.Single(area.RiskCells);
        Assert.Contains(area.RiskCells, c => c.Id == cell.Id);
    }

    [Fact]
    public void RemoveRiskCell_ReturnsFalse_WhenMissing()
    {
        var area = CreateArea();

        var removed = area.RemoveRiskCell(Guid.NewGuid());

        Assert.False(removed);
    }

    [Fact]
    public void RemoveRiskCell_RemovesExistingCell()
    {
        var area = CreateArea();
        var cell = CreateRiskCell(area.Id);
        area.AddRiskCell(cell);

        var removed = area.RemoveRiskCell(cell.Id);

        Assert.True(removed);
        Assert.Empty(area.RiskCells);
    }

    [Fact]
    public void AddRiskCells_Throws_WhenCollectionIsNull()
    {
        var area = CreateArea();

        var ex = Assert.Throws<ArgumentNullException>(() => area.AddRiskCells(null!));

        Assert.Equal("cells", ex.ParamName);
    }

    [Fact]
    public void AddRiskCells_AddsAllUniqueCells()
    {
        var area = CreateArea();
        var cell1 = CreateRiskCell(area.Id);
        var cell2 = CreateRiskCell(area.Id);

        area.AddRiskCells(new[] { cell1, cell2, cell1 });

        Assert.Equal(2, area.RiskCells.Count);
    }

    [Fact]
    public void GetRiskCellById_ReturnsCell_WhenFound()
    {
        var area = CreateArea();
        var cell = CreateRiskCell(area.Id);
        area.AddRiskCell(cell);

        var found = area.GetRiskCellById(cell.Id);

        Assert.Same(cell, found);
    }

    [Fact]
    public void GetRiskCellById_Throws_WhenMissing()
    {
        var area = CreateArea();

        var ex = Assert.Throws<KeyNotFoundException>(() => area.GetRiskCellById(Guid.NewGuid()));

        Assert.Contains("was not found in area", ex.Message);
    }

    [Fact]
    public void TryGetRiskCellForLocation_Throws_WhenLocationIsNull()
    {
        var area = CreateArea();

        var ex = Assert.Throws<ArgumentNullException>(() => area.TryGetRiskCellForLocation(null!, out _));

        Assert.Equal("location", ex.ParamName);
    }

    [Fact]
    public void TryGetRiskCellForLocation_ReturnsFalse_WhenLocationIsOutsideBoundaries()
    {
        var area = new NatureProtector.Core.Areas.Area(
            id: Guid.NewGuid(),
            name: "Area A",
            boundaries: new Boundaries(0.0, 10.0, 0.0, 10.0));

        var result = area.TryGetRiskCellForLocation(new Location(20.0, 20.0), out var found);

        Assert.False(result);
        Assert.Null(found);
    }

    [Fact]
    public void TryGetRiskCellForLocation_MatchesByCellId_WhenAvailable()
    {
        var area = CreateArea();
        var cell = new RiskCell(
            id: Guid.NewGuid(),
            areaId: area.Id,
            location: new Location(5.0, 5.0, cellId: "CELL-01"),
            initialRiskLevel: RiskLevel.Moderate);

        area.AddRiskCell(cell);

        var result = area.TryGetRiskCellForLocation(
            new Location(5.0, 5.0, cellId: "cell-01"),
            out var found);

        Assert.True(result);
        Assert.Same(cell, found);
    }

    [Fact]
    public void TryGetRiskCellForLocation_FallsBackToDistanceMatch()
    {
        var area = CreateArea();
        var cell = new RiskCell(
            id: Guid.NewGuid(),
            areaId: area.Id,
            location: new Location(5.0, 5.0),
            initialRiskLevel: RiskLevel.Moderate);

        area.AddRiskCell(cell);

        var result = area.TryGetRiskCellForLocation(
            new Location(5.00005, 5.0),
            out var found);

        Assert.True(result);
        Assert.Same(cell, found);
    }

    [Fact]
    public void TryGetRiskCellForLocation_ReturnsFalse_WhenNoMatchExists()
    {
        var area = CreateArea();
        var cell = CreateRiskCell(area.Id);
        area.AddRiskCell(cell);

        var result = area.TryGetRiskCellForLocation(new Location(9.0, 9.0), out var found);

        Assert.False(result);
        Assert.Null(found);
    }

    [Fact]
    public void WithUpdatedRiskGrid_Throws_WhenCollectionIsNull()
    {
        var area = CreateArea();

        var ex = Assert.Throws<ArgumentNullException>(() => area.WithUpdatedRiskGrid(null!));

        Assert.Equal("newRiskCells", ex.ParamName);
    }

    [Fact]
    public void WithUpdatedRiskGrid_Throws_WhenCellsBelongToDifferentArea()
    {
        var area = CreateArea();

        var ex = Assert.Throws<ArgumentException>(() => area.WithUpdatedRiskGrid(new[]
        {
            CreateRiskCell(area.Id),
            CreateRiskCell(Guid.NewGuid())
        }));

        Assert.Equal("newRiskCells", ex.ParamName);
        Assert.Contains("must belong to this area", ex.Message);
    }

    [Fact]
    public void WithUpdatedRiskGrid_ReturnsNewArea_WithSameIdentity()
    {
        var area = CreateArea();
        var newCell1 = CreateRiskCell(area.Id);
        var newCell2 = CreateRiskCell(area.Id);

        var updated = area.WithUpdatedRiskGrid(new[] { newCell1, newCell2 });

        Assert.NotSame(area, updated);
        Assert.Equal(area.Id, updated.Id);
        Assert.Equal(area.Name, updated.Name);
        Assert.Same(area.Boundaries, updated.Boundaries);
        Assert.Equal(2, updated.RiskCells.Count);
    }

    [Fact]
    public void ContainsLocation_Throws_WhenLocationIsNull()
    {
        var area = CreateArea();

        var ex = Assert.Throws<ArgumentNullException>(() => area.ContainsLocation(null!));

        Assert.Equal("location", ex.ParamName);
    }

    [Fact]
    public void ContainsLocation_DelegatesToBoundaries()
    {
        var area = new NatureProtector.Core.Areas.Area(
            id: Guid.NewGuid(),
            name: "Area A",
            boundaries: new Boundaries(0.0, 10.0, 0.0, 10.0));

        Assert.True(area.ContainsLocation(new Location(5.0, 5.0)));
        Assert.False(area.ContainsLocation(new Location(20.0, 20.0)));
    }

    private static Boundaries CreateBoundaries() => new(0.0, 10.0, 0.0, 10.0);

    private static RiskCell CreateRiskCell(Guid areaId) =>
        new(
            id: Guid.NewGuid(),
            areaId: areaId,
            location: new Location(5.0, 5.0),
            initialRiskLevel: RiskLevel.Low,
            initialTimestamp: DateTimeOffset.UtcNow);

    private static NatureProtector.Core.Areas.Area CreateArea() =>
        new(
            id: Guid.NewGuid(),
            name: "Area A",
            boundaries: CreateBoundaries());
}
