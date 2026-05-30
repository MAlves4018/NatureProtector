using NatureProtector.Prevention.Risk;

namespace NatureProtector.Prevention.Tests.Risk;

public sealed class TerritorialRiskContextTests
{
    [Fact]
    public void FromCellData_CalculatesHfgAndTerritoryComponent()
    {
        var gridCellId = Guid.NewGuid();

        var context = TerritorialRiskContext.FromCellData(
            gridCellId,
            structuralHazard: "high",
            landCoverClass: "forest",
            dominantForestType: "pine",
            dominantFuelModel: "mato denso",
            treeCoverDensity: 80,
            slopeDegrees: 20,
            aspectDegrees: 180,
            altitudeMeters: 500,
            source: "test");

        var expectedG = (0.70 * (20.0 / 35.0)) + (0.20 * 0.80) + (0.10 * 0.50);
        var expectedT =
            (CandidateParameterSetV1.TerritoryHazardWeight * 0.75) +
            (CandidateParameterSetV1.TerritoryFuelWeight * 0.85) +
            (CandidateParameterSetV1.TerritoryGeomorphologyWeight * expectedG);

        Assert.Equal(gridCellId, context.GridCellId);
        Assert.Equal(0.75, context.HazardComponent, precision: 3);
        Assert.Equal(0.85, context.FuelComponent, precision: 3);
        Assert.Equal(expectedG, context.GeomorphologyComponent, precision: 3);
        Assert.Equal(expectedT, context.TerritoryComponent, precision: 3);
        Assert.Null(context.Limitation);
    }

    [Fact]
    public void FromCellData_UsesCandidateDefaults_WhenInputsAreMissing()
    {
        var context = TerritorialRiskContext.FromCellData(
            Guid.NewGuid(),
            structuralHazard: null,
            landCoverClass: null,
            dominantForestType: null,
            dominantFuelModel: null,
            treeCoverDensity: null,
            slopeDegrees: null,
            aspectDegrees: null,
            altitudeMeters: null,
            source: "test");

        Assert.Equal(CandidateParameterSetV1.CandidateDefaultComponent, context.HazardComponent);
        Assert.Equal(CandidateParameterSetV1.CandidateDefaultComponent, context.FuelComponent);
        Assert.Equal(CandidateParameterSetV1.CandidateDefaultComponent, context.GeomorphologyComponent);
        Assert.Equal(CandidateParameterSetV1.CandidateDefaultComponent, context.TerritoryComponent);
        Assert.Contains("hazard_missing_candidate_default", context.Limitation);
        Assert.Contains("fuel_missing_candidate_default", context.Limitation);
        Assert.Contains("geomorphology_missing_candidate_default", context.Limitation);
    }

    [Fact]
    public void TerritoryComponent_DifferentiatesCellsWithSameMeteorology()
    {
        var highRiskCell = TerritorialRiskContext.FromCellData(
            Guid.NewGuid(),
            structuralHazard: "high",
            landCoverClass: "forest",
            dominantForestType: null,
            dominantFuelModel: "pine",
            treeCoverDensity: null,
            slopeDegrees: 25,
            aspectDegrees: 180,
            altitudeMeters: 650,
            source: "test");
        var lowRiskCell = TerritorialRiskContext.FromCellData(
            Guid.NewGuid(),
            structuralHazard: "low",
            landCoverClass: "urban",
            dominantForestType: null,
            dominantFuelModel: null,
            treeCoverDensity: null,
            slopeDegrees: 2,
            aspectDegrees: 20,
            altitudeMeters: 100,
            source: "test");

        Assert.True(highRiskCell.TerritoryComponent > lowRiskCell.TerritoryComponent);
    }
}
