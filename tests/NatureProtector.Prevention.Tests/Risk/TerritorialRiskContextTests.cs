using FsCheck.Xunit;
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

    [Theory]
    [InlineData(42, 0.42)]
    [InlineData(-10, 0.0)]
    [InlineData(150, 1.0)]
    public void FromCellData_UsesTreeCoverDensityAsFallbackFuelWhenLabelsAreMissing(
        double treeCoverDensity,
        double expectedFuel)
    {
        var context = TerritorialRiskContext.FromCellData(
            Guid.NewGuid(),
            structuralHazard: "alta",
            landCoverClass: null,
            dominantForestType: null,
            dominantFuelModel: null,
            treeCoverDensity: treeCoverDensity,
            slopeDegrees: 12,
            aspectDegrees: 180,
            altitudeMeters: 400,
            source: "tree-cover-test");

        Assert.Equal(expectedFuel, context.FuelComponent, precision: 3);
        Assert.DoesNotContain("fuel_missing_candidate_default", context.Limitation ?? string.Empty);
    }

    [Theory]
    [InlineData(null, 180.0, 400.0, "slope_missing_candidate_default")]
    [InlineData(12.0, null, 400.0, "aspect_missing_candidate_default")]
    [InlineData(12.0, 180.0, null, "altitude_missing_candidate_default")]
    [InlineData(null, 180.0, null, "slope_missing_candidate_default")]
    [InlineData(12.0, null, null, "aspect_missing_candidate_default")]
    public void FromCellData_ReportsOnlyTheMissingGeomorphologyDimensions(
        double? slopeDegrees,
        double? aspectDegrees,
        double? altitudeMeters,
        string expectedLimitation)
    {
        var context = TerritorialRiskContext.FromCellData(
            Guid.NewGuid(),
            structuralHazard: "alta",
            landCoverClass: "Matos",
            dominantForestType: null,
            dominantFuelModel: null,
            treeCoverDensity: null,
            slopeDegrees: slopeDegrees,
            aspectDegrees: aspectDegrees,
            altitudeMeters: altitudeMeters,
            source: "geomorphology-partial-test");

        Assert.Contains(expectedLimitation, context.Limitation);
        Assert.DoesNotContain("geomorphology_missing_candidate_default", context.Limitation);
    }

    [Theory]
    [InlineData(89.999, 0.06)]
    [InlineData(90.0, 0.11)]
    [InlineData(134.999, 0.11)]
    [InlineData(135.0, 0.16)]
    [InlineData(270.0, 0.16)]
    [InlineData(270.001, 0.11)]
    [InlineData(315.0, 0.11)]
    [InlineData(315.001, 0.06)]
    [InlineData(-90.0, 0.16)]
    [InlineData(450.0, 0.11)]
    public void FromCellData_MapsAspectBoundaryBuckets(double aspectDegrees, double expectedGeomorphology)
    {
        var context = TerritorialRiskContext.FromCellData(
            Guid.NewGuid(),
            structuralHazard: "alta",
            landCoverClass: "Matos",
            dominantForestType: null,
            dominantFuelModel: null,
            treeCoverDensity: null,
            slopeDegrees: 0,
            aspectDegrees: aspectDegrees,
            altitudeMeters: 0,
            source: "aspect-boundary-test");

        Assert.Equal(expectedGeomorphology, context.GeomorphologyComponent, precision: 3);
    }

    [Theory]
    [InlineData("", "territorial_context")]
    [InlineData("  dataset-v1  ", "dataset-v1")]
    public void FromCellData_NormalizesSource(string source, string expectedSource)
    {
        var context = CreateContext(source: source);

        Assert.Equal(expectedSource, context.Source);
    }

    [Theory]
    [InlineData("muito_alta", 0.90)]
    [InlineData("MUITO-ALTA", 0.90)]
    [InlineData("Muito Alta", 0.90)]
    [InlineData("alta", 0.75)]
    [InlineData("media", 0.50)]
    [InlineData("média", 0.50)]
    [InlineData("baixa", 0.25)]
    [InlineData("muito_baixa", 0.10)]
    [InlineData("Muito-Baixa", 0.10)]
    public void FromCellData_MapsRealStructuralHazardVocabulary(string structuralHazard, double expectedHazard)
    {
        var context = CreateContext(structuralHazard: structuralHazard);

        Assert.Equal(expectedHazard, context.HazardComponent, precision: 3);
        Assert.DoesNotContain("hazard_unmapped_candidate_default", context.Limitation ?? string.Empty);
    }

    [Theory]
    [InlineData("Matos", 0.80)]
    [InlineData("Florestas de pinheiro bravo", 0.85)]
    [InlineData("Florestas de eucalipto", 0.85)]
    [InlineData("Florestas de outras folhosas", 0.75)]
    [InlineData("Culturas temporárias de sequeiro e regadio", 0.40)]
    [InlineData("Culturas temporárias e/ou pastagens melhoradas associadas a olival", 0.40)]
    [InlineData("Mosaicos culturais e parcelares complexos", 0.40)]
    [InlineData("Olivais", 0.40)]
    [InlineData("Albufeiras de barragens", 0.15)]
    [InlineData("corpos_de_agua", 0.15)]
    public void FromCellData_MapsRealLandCoverVocabulary(string landCoverClass, double expectedFuel)
    {
        var context = CreateContext(landCoverClass: landCoverClass);

        Assert.Equal(expectedFuel, context.FuelComponent, precision: 3);
        Assert.DoesNotContain("fuel_unmapped_candidate_default", context.Limitation ?? string.Empty);
    }

    [Fact]
    public void FromCellData_MapsEveryTerritorialCategoryPresentInBaselineDataset()
    {
        var rows = ReadBaselineCellsAttributes();
        var structuralHazards = DistinctDatasetValues(rows, "structural_hazard");
        var landCoverClasses = DistinctDatasetValues(rows, "land_cover_class");
        var dominantForestTypes = DistinctDatasetValues(rows, "dominant_forest_type");
        var dominantFuelModels = DistinctDatasetValues(rows, "dominant_fuel_model");

        Assert.NotEmpty(structuralHazards);
        Assert.NotEmpty(landCoverClasses);

        foreach (var structuralHazard in structuralHazards)
        {
            var context = CreateContext(structuralHazard: structuralHazard);

            Assert.DoesNotContain("hazard_unmapped_candidate_default", context.Limitation ?? string.Empty);
            Assert.DoesNotContain("hazard_missing_candidate_default", context.Limitation ?? string.Empty);
        }

        foreach (var landCoverClass in landCoverClasses)
        {
            var context = CreateContext(landCoverClass: landCoverClass);

            Assert.DoesNotContain("fuel_unmapped_candidate_default", context.Limitation ?? string.Empty);
            Assert.DoesNotContain("fuel_missing_candidate_default", context.Limitation ?? string.Empty);
        }

        foreach (var dominantForestType in dominantForestTypes)
        {
            var context = CreateContext(landCoverClass: null, dominantForestType: dominantForestType);

            Assert.DoesNotContain("fuel_unmapped_candidate_default", context.Limitation ?? string.Empty);
            Assert.DoesNotContain("fuel_missing_candidate_default", context.Limitation ?? string.Empty);
        }

        foreach (var dominantFuelModel in dominantFuelModels)
        {
            var context = CreateContext(landCoverClass: null, dominantFuelModel: dominantFuelModel);

            Assert.DoesNotContain("fuel_unmapped_candidate_default", context.Limitation ?? string.Empty);
            Assert.DoesNotContain("fuel_missing_candidate_default", context.Limitation ?? string.Empty);
        }
    }

    [Theory]
    [InlineData("highway")]
    [InlineData("muito_alta_experimental")]
    [InlineData("very high candidate")]
    public void FromCellData_DoesNotMapHazardFragmentsAsAliases(string structuralHazard)
    {
        var context = CreateContext(structuralHazard: structuralHazard);

        Assert.Equal(CandidateParameterSetV1.CandidateDefaultComponent, context.HazardComponent);
        Assert.Contains("hazard_unmapped_candidate_default", context.Limitation);
    }

    [Theory]
    [InlineData("pin")]
    [InlineData("oliv")]
    [InlineData("eucal")]
    [InlineData("artificialized")]
    public void FromCellData_DoesNotMapFuelFragmentsAsAliases(string landCoverClass)
    {
        var context = CreateContext(landCoverClass: landCoverClass);

        Assert.Equal(CandidateParameterSetV1.CandidateDefaultComponent, context.FuelComponent);
        Assert.Contains("fuel_unmapped_candidate_default", context.Limitation);
    }

    [Fact]
    public void FromCellData_ReportsUnknownValidLookingTextSeparatelyFromMissingText()
    {
        var unmapped = CreateContext(structuralHazard: "classe-territorial-experimental", landCoverClass: "cobertura-experimental");
        var missing = TerritorialRiskContext.FromCellData(
            Guid.NewGuid(),
            structuralHazard: null,
            landCoverClass: null,
            dominantForestType: null,
            dominantFuelModel: null,
            treeCoverDensity: null,
            slopeDegrees: 12,
            aspectDegrees: 180,
            altitudeMeters: 400,
            source: "dataset-vocabulary-test");

        Assert.Equal(CandidateParameterSetV1.CandidateDefaultComponent, unmapped.HazardComponent);
        Assert.Equal(CandidateParameterSetV1.CandidateDefaultComponent, unmapped.FuelComponent);
        Assert.Contains("hazard_unmapped_candidate_default", unmapped.Limitation);
        Assert.Contains("fuel_unmapped_candidate_default", unmapped.Limitation);
        Assert.Contains("hazard_missing_candidate_default", missing.Limitation);
        Assert.Contains("fuel_missing_candidate_default", missing.Limitation);
    }

    [Property(MaxTest = 100)]
    public bool FromCellData_KeepsAllComponentsNormalized(double slope, double aspect, double altitude, double treeCoverDensity)
    {
        var context = TerritorialRiskContext.FromCellData(
            Guid.NewGuid(),
            structuralHazard: "muito_alta",
            landCoverClass: null,
            dominantForestType: null,
            dominantFuelModel: null,
            treeCoverDensity: NormalizeFinite(treeCoverDensity, -50, 150),
            slopeDegrees: NormalizeFinite(slope, -720, 720),
            aspectDegrees: NormalizeFinite(aspect, -1080, 1080),
            altitudeMeters: NormalizeFinite(altitude, -500, 2500),
            source: "property-test");

        return IsNormalized(context.HazardComponent) &&
            IsNormalized(context.FuelComponent) &&
            IsNormalized(context.GeomorphologyComponent) &&
            IsNormalized(context.TerritoryComponent);
    }

    private static TerritorialRiskContext CreateContext(
        string structuralHazard = "alta",
        string? landCoverClass = "Matos",
        string? dominantForestType = null,
        string? dominantFuelModel = null,
        string source = "dataset-vocabulary-test")
    {
        return TerritorialRiskContext.FromCellData(
            Guid.NewGuid(),
            structuralHazard: structuralHazard,
            landCoverClass: landCoverClass,
            dominantForestType: dominantForestType,
            dominantFuelModel: dominantFuelModel,
            treeCoverDensity: null,
            slopeDegrees: 12,
            aspectDegrees: 180,
            altitudeMeters: 400,
            source: source);
    }

    private static double NormalizeFinite(double value, double min, double max)
    {
        return double.IsFinite(value)
            ? Math.Clamp(value, min, max)
            : 0;
    }

    private static bool IsNormalized(double value)
    {
        return value is >= 0.0 and <= 1.0;
    }

    private static IReadOnlyList<Dictionary<string, string>> ReadBaselineCellsAttributes()
    {
        var path = Path.Combine(
            FindRepositoryRoot(),
            "data",
            "baseline",
            "areas",
            "proenca-a-nova",
            "cells_attributes.csv");
        var lines = File.ReadAllLines(path);
        Assert.True(lines.Length > 1, "Baseline cells_attributes.csv must contain a header and data rows.");

        var headers = lines[0].Split(',');
        return lines
            .Skip(1)
            .Select(line =>
            {
                var values = line.Split(',');
                Assert.Equal(headers.Length, values.Length);

                return headers
                    .Select((header, index) => (header, value: values[index]))
                    .ToDictionary(
                        item => item.header,
                        item => item.value,
                        StringComparer.Ordinal);
            })
            .ToArray();
    }

    private static IReadOnlyList<string> DistinctDatasetValues(
        IReadOnlyList<Dictionary<string, string>> rows,
        string columnName)
    {
        return rows
            .Select(row => row[columnName])
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Select(value => value.Trim())
            .Distinct(StringComparer.Ordinal)
            .OrderBy(value => value, StringComparer.Ordinal)
            .ToArray();
    }

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);

        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "NatureProtector.sln")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException("Could not locate NatureProtector.sln from the test output directory.");
    }
}
