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
}
