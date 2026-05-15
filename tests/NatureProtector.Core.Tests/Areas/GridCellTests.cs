using NatureProtector.Core.Areas;
using NatureProtector.Core.Primitives;
using NatureProtector.Core.Risk;

namespace NatureProtector.Core.Tests.Areas;

public class GridCellTests
{
    [Fact]
    public void Ctor_AssignsProperties_WhenValid()
    {
        var id = Guid.NewGuid();
        var areaId = Guid.NewGuid();
        var centroid = new Location(39.746, -7.925, altitude: 450.0, cellId: "PT-PN-001");

        var cell = new GridCell(
            id: id,
            areaId: areaId,
            cellCode: "PT-PN-001",
            centroid: centroid,
            altitudeMeters: 450.0,
            slopeDegrees: 8.5,
            aspectDegrees: 180.0,
            landCoverClass: "florestas",
            dominantForestType: "Pinhal",
            dominantFuelModel: "Matos densos",
            treeCoverDensity: 64.0,
            structuralHazard: "alta",
            conjuncturalHazard: "media");

        Assert.Equal(id, cell.Id);
        Assert.Equal(areaId, cell.AreaId);
        Assert.Equal("PT-PN-001", cell.CellCode);
        Assert.Same(centroid, cell.Centroid);
        Assert.Equal(450.0, cell.AltitudeMeters);
        Assert.Equal(8.5, cell.SlopeDegrees);
        Assert.Equal(180.0, cell.AspectDegrees);
        Assert.Equal("florestas", cell.LandCoverClass);
        Assert.Equal("Pinhal", cell.DominantForestType);
        Assert.Equal("Matos densos", cell.DominantFuelModel);
        Assert.Equal(64.0, cell.TreeCoverDensity);
        Assert.Equal("alta", cell.StructuralHazard);
        Assert.Equal("media", cell.ConjuncturalHazard);
    }

    [Fact]
    public void Ctor_WhitespaceOptionalText_NormalizesToNullAndTrimsCode()
    {
        var cell = new GridCell(
            id: Guid.NewGuid(),
            areaId: Guid.NewGuid(),
            cellCode: "  PT-PN-001  ",
            centroid: new Location(39.746, -7.925),
            landCoverClass: " ",
            dominantForestType: "  Pinhal  ",
            dominantFuelModel: "",
            structuralHazard: "\t",
            conjuncturalHazard: " medium ");

        Assert.Equal("PT-PN-001", cell.CellCode);
        Assert.Null(cell.LandCoverClass);
        Assert.Equal("Pinhal", cell.DominantForestType);
        Assert.Null(cell.DominantFuelModel);
        Assert.Null(cell.StructuralHazard);
        Assert.Equal("medium", cell.ConjuncturalHazard);
    }

    [Fact]
    public void Ctor_ValidBoundaryTerrainValues_AssignsValues()
    {
        var cell = new GridCell(
            id: Guid.NewGuid(),
            areaId: Guid.NewGuid(),
            cellCode: "PT-PN-001",
            centroid: new Location(39.746, -7.925),
            slopeDegrees: 90.0,
            aspectDegrees: 360.0,
            treeCoverDensity: 100.0);

        Assert.Equal(90.0, cell.SlopeDegrees);
        Assert.Equal(360.0, cell.AspectDegrees);
        Assert.Equal(100.0, cell.TreeCoverDensity);
    }

    [Fact]
    public void Ctor_EmptyId_ThrowsArgumentException()
    {
        var exception = Assert.Throws<ArgumentException>(() => new GridCell(
            id: Guid.Empty,
            areaId: Guid.NewGuid(),
            cellCode: "PT-PN-001",
            centroid: new Location(39.746, -7.925)));

        Assert.Equal("id", exception.ParamName);
    }

    [Fact]
    public void Ctor_EmptyAreaId_ThrowsArgumentException()
    {
        var exception = Assert.Throws<ArgumentException>(() => new GridCell(
            id: Guid.NewGuid(),
            areaId: Guid.Empty,
            cellCode: "PT-PN-001",
            centroid: new Location(39.746, -7.925)));

        Assert.Equal("areaId", exception.ParamName);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Ctor_MissingCellCode_ThrowsArgumentException(string? cellCode)
    {
        var exception = Assert.Throws<ArgumentException>(() => new GridCell(
            id: Guid.NewGuid(),
            areaId: Guid.NewGuid(),
            cellCode: cellCode!,
            centroid: new Location(39.746, -7.925)));

        Assert.Equal("cellCode", exception.ParamName);
    }

    [Fact]
    public void Ctor_NullCentroid_ThrowsArgumentNullException()
    {
        var exception = Assert.Throws<ArgumentNullException>(() => new GridCell(
            id: Guid.NewGuid(),
            areaId: Guid.NewGuid(),
            cellCode: "PT-PN-001",
            centroid: null!));

        Assert.Equal("centroid", exception.ParamName);
    }

    [Theory]
    [InlineData(-0.1)]
    [InlineData(90.1)]
    public void Ctor_SlopeOutsideValidRange_ThrowsArgumentOutOfRangeException(double slope)
    {
        var exception = Assert.Throws<ArgumentOutOfRangeException>(() => new GridCell(
            id: Guid.NewGuid(),
            areaId: Guid.NewGuid(),
            cellCode: "PT-PN-001",
            centroid: new Location(39.746, -7.925),
            slopeDegrees: slope));

        Assert.Equal("slopeDegrees", exception.ParamName);
    }

    [Theory]
    [InlineData(-0.1)]
    [InlineData(360.1)]
    public void Ctor_AspectOutsideValidRange_ThrowsArgumentOutOfRangeException(double aspect)
    {
        var exception = Assert.Throws<ArgumentOutOfRangeException>(() => new GridCell(
            id: Guid.NewGuid(),
            areaId: Guid.NewGuid(),
            cellCode: "PT-PN-001",
            centroid: new Location(39.746, -7.925),
            aspectDegrees: aspect));

        Assert.Equal("aspectDegrees", exception.ParamName);
    }

    [Theory]
    [InlineData(-0.1)]
    [InlineData(100.1)]
    public void Ctor_TreeCoverDensityOutsideValidRange_ThrowsArgumentOutOfRangeException(double density)
    {
        var exception = Assert.Throws<ArgumentOutOfRangeException>(() => new GridCell(
            id: Guid.NewGuid(),
            areaId: Guid.NewGuid(),
            cellCode: "PT-PN-001",
            centroid: new Location(39.746, -7.925),
            treeCoverDensity: density));

        Assert.Equal("treeCoverDensity", exception.ParamName);
    }

    [Fact]
    public void ToRiskCell_UsesSameIdentityAndCentroid()
    {
        var areaId = Guid.NewGuid();
        var cell = new GridCell(
            id: Guid.NewGuid(),
            areaId: areaId,
            cellCode: "PT-PN-001",
            centroid: new Location(39.746, -7.925));

        var riskCell = cell.ToRiskCell(RiskLevel.High);

        Assert.Equal(cell.Id, riskCell.Id);
        Assert.Equal(areaId, riskCell.AreaId);
        Assert.Equal("PT-PN-001", riskCell.CellId);
        Assert.Equal(RiskLevel.High, riskCell.CurrentRiskLevel);
        Assert.Equal(cell.Centroid.Latitude, riskCell.Location.Latitude);
        Assert.Equal(cell.Centroid.Longitude, riskCell.Location.Longitude);
        Assert.Equal("PT-PN-001", riskCell.Location.CellId);
    }

    [Fact]
    public void WithTerrain_ValidTerrain_ReturnsCopyWithUpdatedTerrainAndPreservedMetadata()
    {
        var cell = new GridCell(
            id: Guid.NewGuid(),
            areaId: Guid.NewGuid(),
            cellCode: "PT-PN-001",
            centroid: new Location(39.746, -7.925),
            altitudeMeters: 450.0,
            slopeDegrees: 8.5,
            aspectDegrees: 180.0,
            landCoverClass: "forest",
            dominantForestType: "pine",
            dominantFuelModel: "shrubs",
            treeCoverDensity: 64.0,
            structuralHazard: "high",
            conjuncturalHazard: "medium");

        var updated = cell.WithTerrain(
            altitudeMeters: 500.0,
            slopeDegrees: 12.0,
            aspectDegrees: 220.0);

        Assert.NotSame(cell, updated);
        Assert.Equal(cell.Id, updated.Id);
        Assert.Equal(cell.AreaId, updated.AreaId);
        Assert.Equal(cell.CellCode, updated.CellCode);
        Assert.Same(cell.Centroid, updated.Centroid);
        Assert.Equal(500.0, updated.AltitudeMeters);
        Assert.Equal(12.0, updated.SlopeDegrees);
        Assert.Equal(220.0, updated.AspectDegrees);
        Assert.Equal(cell.LandCoverClass, updated.LandCoverClass);
        Assert.Equal(cell.TreeCoverDensity, updated.TreeCoverDensity);
        Assert.Equal(cell.StructuralHazard, updated.StructuralHazard);
    }
}
