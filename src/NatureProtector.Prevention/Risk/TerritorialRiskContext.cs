namespace NatureProtector.Prevention.Risk;

public sealed record TerritorialRiskContext(
    Guid? GridCellId,
    string Source,
    double StructuralHazardScore)
{
    public double HazardComponent { get; init; } = CandidateParameterSetV1.ClampNormalized(StructuralHazardScore);

    public double FuelComponent { get; init; } = CandidateParameterSetV1.CandidateDefaultComponent;

    public double GeomorphologyComponent { get; init; } = CandidateParameterSetV1.CandidateDefaultComponent;

    public double TerritoryComponent { get; init; } = CandidateParameterSetV1.ClampNormalized(StructuralHazardScore);

    public string? Limitation { get; init; } = "fuel_and_geomorphology_candidate_defaults";

    public static TerritorialRiskContext Unknown(Guid? gridCellId)
        => new(gridCellId, "unknown", CandidateParameterSetV1.CandidateDefaultComponent)
        {
            HazardComponent = CandidateParameterSetV1.CandidateDefaultComponent,
            FuelComponent = CandidateParameterSetV1.CandidateDefaultComponent,
            GeomorphologyComponent = CandidateParameterSetV1.CandidateDefaultComponent,
            TerritoryComponent = CandidateParameterSetV1.CandidateDefaultComponent,
            Limitation = "territorial_context_missing_candidate_defaults"
        };

    public static TerritorialRiskContext FromCellData(
        Guid gridCellId,
        string? structuralHazard,
        string? landCoverClass,
        string? dominantForestType,
        string? dominantFuelModel,
        double? treeCoverDensity,
        double? slopeDegrees,
        double? aspectDegrees,
        double? altitudeMeters,
        string source)
    {
        var hazard = ResolveHazardComponent(structuralHazard);
        var fuel = ResolveFuelComponent(landCoverClass, dominantForestType, dominantFuelModel, treeCoverDensity);
        var geomorphology = ResolveGeomorphologyComponent(slopeDegrees, aspectDegrees, altitudeMeters);
        var territory = CandidateParameterSetV1.ClampNormalized(
            (CandidateParameterSetV1.TerritoryHazardWeight * hazard.Score) +
            (CandidateParameterSetV1.TerritoryFuelWeight * fuel.Score) +
            (CandidateParameterSetV1.TerritoryGeomorphologyWeight * geomorphology.Score));

        var limitations = new[]
            {
                hazard.Limitation,
                fuel.Limitation,
                geomorphology.Limitation
            }
            .Where(item => !string.IsNullOrWhiteSpace(item))
            .Distinct(StringComparer.Ordinal)
            .ToArray();

        return new TerritorialRiskContext(gridCellId, NormalizeSource(source), territory)
        {
            HazardComponent = hazard.Score,
            FuelComponent = fuel.Score,
            GeomorphologyComponent = geomorphology.Score,
            TerritoryComponent = territory,
            Limitation = limitations.Length == 0 ? null : string.Join(";", limitations)
        };
    }

    private static ComponentScore ResolveHazardComponent(string? structuralHazard)
    {
        if (string.IsNullOrWhiteSpace(structuralHazard))
        {
            return ComponentScore.Default("hazard_missing_candidate_default");
        }

        var value = structuralHazard.Trim().ToLowerInvariant();
        var score = value switch
        {
            var item when item.Contains("muito alta", StringComparison.Ordinal) ||
                item.Contains("very high", StringComparison.Ordinal) ||
                item.Contains("extreme", StringComparison.Ordinal) => 0.90,
            var item when item.Contains("alta", StringComparison.Ordinal) ||
                item.Contains("high", StringComparison.Ordinal) => 0.75,
            var item when item.Contains("moderada", StringComparison.Ordinal) ||
                item.Contains("media", StringComparison.Ordinal) ||
                item.Contains("medium", StringComparison.Ordinal) => 0.50,
            var item when item.Contains("baixa", StringComparison.Ordinal) ||
                item.Contains("low", StringComparison.Ordinal) => 0.25,
            _ => CandidateParameterSetV1.CandidateDefaultComponent
        };
        var mapped = value.Contains("muito alta", StringComparison.Ordinal) ||
            value.Contains("very high", StringComparison.Ordinal) ||
            value.Contains("extreme", StringComparison.Ordinal) ||
            value.Contains("alta", StringComparison.Ordinal) ||
            value.Contains("high", StringComparison.Ordinal) ||
            value.Contains("moderada", StringComparison.Ordinal) ||
            value.Contains("media", StringComparison.Ordinal) ||
            value.Contains("medium", StringComparison.Ordinal) ||
            value.Contains("baixa", StringComparison.Ordinal) ||
            value.Contains("low", StringComparison.Ordinal);

        return new ComponentScore(score, mapped
            ? null
            : "hazard_unmapped_candidate_default");
    }

    private static ComponentScore ResolveFuelComponent(
        string? landCoverClass,
        string? dominantForestType,
        string? dominantFuelModel,
        double? treeCoverDensity)
    {
        var text = string.Join(
            " ",
            new[] { landCoverClass, dominantForestType, dominantFuelModel }
                .Where(item => !string.IsNullOrWhiteSpace(item)))
            .Trim()
            .ToLowerInvariant();

        if (!string.IsNullOrWhiteSpace(text))
        {
            var score = text switch
            {
                var item when item.Contains("eucal", StringComparison.Ordinal) ||
                    item.Contains("pin", StringComparison.Ordinal) ||
                    item.Contains("resin", StringComparison.Ordinal) => 0.85,
                var item when item.Contains("mato", StringComparison.Ordinal) ||
                    item.Contains("shrub", StringComparison.Ordinal) ||
                    item.Contains("scrub", StringComparison.Ordinal) => 0.80,
                var item when item.Contains("forest", StringComparison.Ordinal) ||
                    item.Contains("florest", StringComparison.Ordinal) ||
                    item.Contains("wood", StringComparison.Ordinal) => 0.75,
                var item when item.Contains("agric", StringComparison.Ordinal) ||
                    item.Contains("pasture", StringComparison.Ordinal) ||
                    item.Contains("herb", StringComparison.Ordinal) => 0.40,
                var item when item.Contains("urban", StringComparison.Ordinal) ||
                    item.Contains("water", StringComparison.Ordinal) ||
                    item.Contains("artificial", StringComparison.Ordinal) => 0.15,
                _ => CandidateParameterSetV1.CandidateDefaultComponent
            };
            var mapped = text.Contains("eucal", StringComparison.Ordinal) ||
                text.Contains("pin", StringComparison.Ordinal) ||
                text.Contains("resin", StringComparison.Ordinal) ||
                text.Contains("mato", StringComparison.Ordinal) ||
                text.Contains("shrub", StringComparison.Ordinal) ||
                text.Contains("scrub", StringComparison.Ordinal) ||
                text.Contains("forest", StringComparison.Ordinal) ||
                text.Contains("florest", StringComparison.Ordinal) ||
                text.Contains("wood", StringComparison.Ordinal) ||
                text.Contains("agric", StringComparison.Ordinal) ||
                text.Contains("pasture", StringComparison.Ordinal) ||
                text.Contains("herb", StringComparison.Ordinal) ||
                text.Contains("urban", StringComparison.Ordinal) ||
                text.Contains("water", StringComparison.Ordinal) ||
                text.Contains("artificial", StringComparison.Ordinal);

            return new ComponentScore(score, mapped
                ? null
                : "fuel_unmapped_candidate_default");
        }

        if (treeCoverDensity.HasValue)
        {
            return new ComponentScore(CandidateParameterSetV1.ClampNormalized(treeCoverDensity.Value / 100.0), null);
        }

        return ComponentScore.Default("fuel_missing_candidate_default");
    }

    private static ComponentScore ResolveGeomorphologyComponent(
        double? slopeDegrees,
        double? aspectDegrees,
        double? altitudeMeters)
    {
        if (!slopeDegrees.HasValue && !aspectDegrees.HasValue && !altitudeMeters.HasValue)
        {
            return ComponentScore.Default("geomorphology_missing_candidate_default");
        }

        var slope = slopeDegrees.HasValue
            ? CandidateParameterSetV1.ClampNormalized(slopeDegrees.Value / 35.0)
            : CandidateParameterSetV1.CandidateDefaultComponent;
        var aspect = aspectDegrees.HasValue
            ? ResolveAspectComponent(aspectDegrees.Value)
            : CandidateParameterSetV1.CandidateDefaultComponent;
        var altitude = altitudeMeters.HasValue
            ? CandidateParameterSetV1.ClampNormalized(altitudeMeters.Value / 1000.0)
            : CandidateParameterSetV1.CandidateDefaultComponent;

        var score = CandidateParameterSetV1.ClampNormalized((0.70 * slope) + (0.20 * aspect) + (0.10 * altitude));
        var limitation = new[]
            {
                slopeDegrees.HasValue ? null : "slope_missing_candidate_default",
                aspectDegrees.HasValue ? null : "aspect_missing_candidate_default",
                altitudeMeters.HasValue ? null : "altitude_missing_candidate_default"
            }
            .Where(item => item is not null)
            .ToArray();

        return new ComponentScore(score, limitation.Length == 0 ? null : string.Join(";", limitation));
    }

    private static double ResolveAspectComponent(double aspectDegrees)
    {
        var normalized = ((aspectDegrees % 360.0) + 360.0) % 360.0;

        return normalized switch
        {
            >= 135.0 and <= 270.0 => 0.80,
            >= 90.0 and < 135.0 => 0.55,
            > 270.0 and <= 315.0 => 0.55,
            _ => 0.30
        };
    }

    private static string NormalizeSource(string source)
    {
        return string.IsNullOrWhiteSpace(source)
            ? "territorial_context"
            : source.Trim();
    }

    private readonly record struct ComponentScore(double Score, string? Limitation)
    {
        public static ComponentScore Default(string limitation)
            => new(CandidateParameterSetV1.CandidateDefaultComponent, limitation);
    }
}
