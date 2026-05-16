using NatureProtector.Core.Primitives;
using NatureProtector.Core.Risk;
using Xunit;
using GridCell = NatureProtector.Core.Areas.GridCell;

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
    public void Ctor_NullInitialGridCells_InitializesEmptyTerritorialGrid()
    {
        var area = new NatureProtector.Core.Areas.Area(
            id: Guid.NewGuid(),
            name: "Area A",
            boundaries: CreateBoundaries(),
            gridCells: null);

        Assert.Empty(area.GridCells);
    }

    [Fact]
    public void Ctor_Throws_WhenInitialGridCellsBelongToDifferentArea()
    {
        var areaId = Guid.NewGuid();

        var exception = Assert.Throws<ArgumentException>(() =>
            new NatureProtector.Core.Areas.Area(
                id: areaId,
                name: "Area A",
                boundaries: CreateBoundaries(),
                gridCells:
                [
                    CreateGridCell(areaId, "CELL-01"),
                    CreateGridCell(Guid.NewGuid(), "CELL-02")
                ]));

        Assert.Equal("gridCells", exception.ParamName);
        Assert.Contains("All grid cells must belong to the same AreaId as the Area.", exception.Message);
    }

    [Fact]
    public void Ctor_Throws_WhenInitialGridCellsContainDuplicateIds()
    {
        var areaId = Guid.NewGuid();
        var duplicatedId = Guid.NewGuid();

        var exception = Assert.Throws<ArgumentException>(() =>
            new NatureProtector.Core.Areas.Area(
                id: areaId,
                name: "Area A",
                boundaries: CreateBoundaries(),
                gridCells:
                [
                    CreateGridCell(areaId, "CELL-01", duplicatedId),
                    CreateGridCell(areaId, "CELL-02", duplicatedId)
                ]));

        Assert.Equal("gridCells", exception.ParamName);
        Assert.Contains("Grid cells must not contain duplicate identifiers.", exception.Message);
    }

    [Fact]
    public void Ctor_Throws_WhenInitialGridCellsContainDuplicateCodesIgnoringCase()
    {
        var areaId = Guid.NewGuid();

        var exception = Assert.Throws<ArgumentException>(() =>
            new NatureProtector.Core.Areas.Area(
                id: areaId,
                name: "Area A",
                boundaries: CreateBoundaries(),
                gridCells:
                [
                    CreateGridCell(areaId, "CELL-01"),
                    CreateGridCell(areaId, "cell-01")
                ]));

        Assert.Equal("gridCells", exception.ParamName);
        Assert.Contains("Grid cells must not contain duplicate cell codes.", exception.Message);
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
    public void Ctor_Throws_WhenInitialRiskCellsContainDuplicateIds()
    {
        var areaId = Guid.NewGuid();
        var duplicatedCellId = Guid.NewGuid();

        var ex = Assert.Throws<ArgumentException>(
            () => new NatureProtector.Core.Areas.Area(
                id: areaId,
                name: "Area A",
                boundaries: CreateBoundaries(),
                riskCells: new[]
                {
                    new RiskCell(
                        id: duplicatedCellId,
                        areaId: areaId,
                        location: new Location(5.0, 5.0),
                        initialRiskLevel: RiskLevel.Low,
                        initialTimestamp: DateTimeOffset.UtcNow.AddMinutes(-1)),
                    new RiskCell(
                        id: duplicatedCellId,
                        areaId: areaId,
                        location: new Location(5.0001, 5.0001),
                        initialRiskLevel: RiskLevel.Moderate,
                        initialTimestamp: DateTimeOffset.UtcNow)
                }));

        Assert.Equal("riskCells", ex.ParamName);
        Assert.Contains("duplicate identifiers", ex.Message);
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
    public void AddGridCell_Throws_WhenGridCellIsNull()
    {
        var area = CreateArea();

        var exception = Assert.Throws<ArgumentNullException>(() => area.AddGridCell(null!));

        Assert.Equal("gridCell", exception.ParamName);
    }

    [Fact]
    public void AddGridCell_Throws_WhenGridCellBelongsToAnotherArea()
    {
        var area = CreateArea();
        var cell = CreateGridCell(Guid.NewGuid(), "CELL-01");

        var exception = Assert.Throws<InvalidOperationException>(() => area.AddGridCell(cell));

        Assert.Contains("does not belong to area", exception.Message);
    }

    [Fact]
    public void AddGridCell_AddsCellAndIgnoresDuplicateById()
    {
        var area = CreateArea();
        var cell = CreateGridCell(area.Id, "CELL-01");

        area.AddGridCell(cell);
        area.AddGridCell(cell);

        var stored = Assert.Single(area.GridCells);
        Assert.Same(cell, stored);
    }

    [Fact]
    public void AddGridCells_Throws_WhenCollectionIsNull()
    {
        var area = CreateArea();

        var exception = Assert.Throws<ArgumentNullException>(() => area.AddGridCells(null!));

        Assert.Equal("cells", exception.ParamName);
    }

    [Fact]
    public void AddGridCells_AddsAllUniqueCells()
    {
        var area = CreateArea();
        var cell1 = CreateGridCell(area.Id, "CELL-01");
        var cell2 = CreateGridCell(area.Id, "CELL-02");

        area.AddGridCells([cell1, cell2, cell1]);

        Assert.Equal(2, area.GridCells.Count);
        Assert.Contains(area.GridCells, cell => cell.CellCode == "CELL-01");
        Assert.Contains(area.GridCells, cell => cell.CellCode == "CELL-02");
    }

    [Fact]
    public void RemoveGridCell_MissingCell_ReturnsFalse()
    {
        var area = CreateArea();

        var removed = area.RemoveGridCell(Guid.NewGuid());

        Assert.False(removed);
    }

    [Fact]
    public void RemoveGridCell_ExistingCell_RemovesAndReturnsTrue()
    {
        var area = CreateArea();
        var cell = CreateGridCell(area.Id, "CELL-01");
        area.AddGridCell(cell);

        var removed = area.RemoveGridCell(cell.Id);

        Assert.True(removed);
        Assert.Empty(area.GridCells);
    }

    [Fact]
    public void GetGridCellById_ExistingCell_ReturnsCell()
    {
        var area = CreateArea();
        var cell = CreateGridCell(area.Id, "CELL-01");
        area.AddGridCell(cell);

        var found = area.GetGridCellById(cell.Id);

        Assert.Same(cell, found);
    }

    [Fact]
    public void GetGridCellById_MissingCell_ThrowsKeyNotFoundException()
    {
        var area = CreateArea();

        var exception = Assert.Throws<KeyNotFoundException>(() => area.GetGridCellById(Guid.NewGuid()));

        Assert.Contains("was not found in area", exception.Message);
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
            location: new Location(5.0, 5.0),
            initialRiskLevel: RiskLevel.Moderate,
            cellId: "CELL-01");

        area.AddRiskCell(cell);

        var result = area.TryGetRiskCellForLocation(
            new Location(8.0, 8.0, cellId: "cell-01"),
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
    public void GetRiskCellForLocation_ReturnsCell_WhenLocationResolves()
    {
        var area = CreateArea();
        var cell = new RiskCell(
            id: Guid.NewGuid(),
            areaId: area.Id,
            location: new Location(5.0, 5.0),
            initialRiskLevel: RiskLevel.Moderate,
            cellId: "CELL-02");

        area.AddRiskCell(cell);

        var found = area.GetRiskCellForLocation(new Location(9.0, 9.0, cellId: "cell-02"));

        Assert.Same(cell, found);
    }

    [Fact]
    public void GetRiskCellForLocation_ReturnsNull_WhenNoMatchExists()
    {
        var area = CreateArea();
        area.AddRiskCell(CreateRiskCell(area.Id));

        var found = area.GetRiskCellForLocation(new Location(9.0, 9.0));

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
    public void WithUpdatedGridCells_Throws_WhenCollectionIsNull()
    {
        var area = CreateArea();

        var exception = Assert.Throws<ArgumentNullException>(() => area.WithUpdatedGridCells(null!));

        Assert.Equal("newGridCells", exception.ParamName);
    }

    [Fact]
    public void WithUpdatedGridCells_Throws_WhenCellsBelongToDifferentArea()
    {
        var area = CreateArea();

        var exception = Assert.Throws<ArgumentException>(() => area.WithUpdatedGridCells(
        [
            CreateGridCell(area.Id, "CELL-01"),
            CreateGridCell(Guid.NewGuid(), "CELL-02")
        ]));

        Assert.Equal("newGridCells", exception.ParamName);
        Assert.Contains("All grid cells must belong to this area.", exception.Message);
    }

    [Fact]
    public void WithUpdatedGridCells_ReturnsNewAreaWithReplacedGridCells()
    {
        var area = CreateArea();
        var original = CreateGridCell(area.Id, "CELL-00");
        area.AddGridCell(original);
        var replacement1 = CreateGridCell(area.Id, "CELL-01");
        var replacement2 = CreateGridCell(area.Id, "CELL-02");

        var updated = area.WithUpdatedGridCells([replacement1, replacement2]);

        Assert.NotSame(area, updated);
        Assert.Equal(area.Id, updated.Id);
        Assert.Single(area.GridCells);
        Assert.Equal(2, updated.GridCells.Count);
        Assert.DoesNotContain(updated.GridCells, cell => cell.CellCode == "CELL-00");
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

    private static GridCell CreateGridCell(Guid areaId, string cellCode, Guid? id = null) =>
        new(
            id: id ?? Guid.NewGuid(),
            areaId: areaId,
            cellCode: cellCode,
            centroid: new Location(5.0, 5.0));

    private static NatureProtector.Core.Areas.Area CreateArea() =>
        new(
            id: Guid.NewGuid(),
            name: "Area A",
            boundaries: CreateBoundaries());
}
