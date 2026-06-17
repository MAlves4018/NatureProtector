using System.Globalization;
using System.Text;

namespace NatureProtector.Prevention.Risk;

public sealed record TerritorialRiskContext(
    Guid? GridCellId,
    string Source,
    double StructuralHazardScore)
{
    private static readonly IReadOnlyDictionary<string, CanonicalComponentMapping> HazardAliases =
        new Dictionary<string, CanonicalComponentMapping>(StringComparer.Ordinal)
        {
            ["muito alta"] = new("very_high_hazard", 0.90, true, null),
            ["very high"] = new("very_high_hazard", 0.90, true, null),
            ["extreme"] = new("very_high_hazard", 0.90, true, null),
            ["alta"] = new("high_hazard", 0.75, true, null),
            ["high"] = new("high_hazard", 0.75, true, null),
            ["moderada"] = new("medium_hazard", 0.50, true, null),
            ["media"] = new("medium_hazard", 0.50, true, null),
            ["medium"] = new("medium_hazard", 0.50, true, null),
            ["baixa"] = new("low_hazard", 0.25, true, null),
            ["low"] = new("low_hazard", 0.25, true, null),
            ["muito baixa"] = new("very_low_hazard", 0.10, true, null),
            ["very low"] = new("very_low_hazard", 0.10, true, null)
        };

    private static readonly IReadOnlyDictionary<string, CanonicalComponentMapping> FuelAliases =
        new Dictionary<string, CanonicalComponentMapping>(StringComparer.Ordinal)
        {
            ["florestas de eucalipto"] = new("high_flammability_forest", 0.85, true, null),
            ["eucalipto"] = new("high_flammability_forest", 0.85, true, null),
            ["eucaliptal"] = new("high_flammability_forest", 0.85, true, null),
            ["florestas de pinheiro bravo"] = new("high_flammability_forest", 0.85, true, null),
            ["pinheiro bravo"] = new("high_flammability_forest", 0.85, true, null),
            ["pinhal"] = new("high_flammability_forest", 0.85, true, null),
            ["pine"] = new("high_flammability_forest", 0.85, true, null),
            ["florestas de resinosas"] = new("high_flammability_forest", 0.85, true, null),
            ["resinosas"] = new("high_flammability_forest", 0.85, true, null),
            ["matos"] = new("shrubland", 0.80, true, null),
            ["mato"] = new("shrubland", 0.80, true, null),
            ["mato denso"] = new("shrubland", 0.80, true, null),
            ["matos densos"] = new("shrubland", 0.80, true, null),
            ["shrub"] = new("shrubland", 0.80, true, null),
            ["shrubs"] = new("shrubland", 0.80, true, null),
            ["scrub"] = new("shrubland", 0.80, true, null),
            ["florestas de outras folhosas"] = new("broadleaf_or_mixed_forest", 0.75, true, null),
            ["outras folhosas"] = new("broadleaf_or_mixed_forest", 0.75, true, null),
            ["florestas"] = new("broadleaf_or_mixed_forest", 0.75, true, null),
            ["floresta"] = new("broadleaf_or_mixed_forest", 0.75, true, null),
            ["forest"] = new("broadleaf_or_mixed_forest", 0.75, true, null),
            ["wood"] = new("broadleaf_or_mixed_forest", 0.75, true, null),
            ["woodland"] = new("broadleaf_or_mixed_forest", 0.75, true, null),
            ["culturas temporarias de sequeiro e regadio"] = new("agriculture_or_pasture", 0.40, true, null),
            ["culturas temporarias e ou pastagens melhoradas associadas a olival"] = new("agriculture_or_pasture", 0.40, true, null),
            ["mosaicos culturais e parcelares complexos"] = new("agriculture_or_pasture", 0.40, true, null),
            ["olivais"] = new("agriculture_or_pasture", 0.40, true, null),
            ["olival"] = new("agriculture_or_pasture", 0.40, true, null),
            ["agricultura"] = new("agriculture_or_pasture", 0.40, true, null),
            ["agriculture"] = new("agriculture_or_pasture", 0.40, true, null),
            ["pastagem"] = new("agriculture_or_pasture", 0.40, true, null),
            ["pastagens"] = new("agriculture_or_pasture", 0.40, true, null),
            ["pasture"] = new("agriculture_or_pasture", 0.40, true, null),
            ["herbaceas"] = new("agriculture_or_pasture", 0.40, true, null),
            ["herbaceous"] = new("agriculture_or_pasture", 0.40, true, null),
            ["albufeiras de barragens"] = new("water_or_artificial", 0.15, true, null),
            ["corpos de agua"] = new("water_or_artificial", 0.15, true, null),
            ["agua"] = new("water_or_artificial", 0.15, true, null),
            ["water"] = new("water_or_artificial", 0.15, true, null),
            ["urbano"] = new("water_or_artificial", 0.15, true, null),
            ["urban"] = new("water_or_artificial", 0.15, true, null),
            ["artificial"] = new("water_or_artificial", 0.15, true, null),
            ["areas artificiais"] = new("water_or_artificial", 0.15, true, null)
        };

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

        var value = NormalizeClassifierText(structuralHazard);
        var mapping = ResolveAlias(
            value,
            HazardAliases,
            "unknown_hazard",
            "hazard_unmapped_candidate_default");

        return new ComponentScore(mapping.Score, mapping.IsMapped ? null : mapping.Reason);
    }

    private static ComponentScore ResolveFuelComponent(
        string? landCoverClass,
        string? dominantForestType,
        string? dominantFuelModel,
        double? treeCoverDensity)
    {
        var normalizedLabels = new[] { dominantForestType, dominantFuelModel, landCoverClass }
            .Select(NormalizeClassifierText)
            .Where(item => !string.IsNullOrWhiteSpace(item))
            .ToArray();

        if (normalizedLabels.Length > 0)
        {
            var bestMapping = normalizedLabels
                .Select(value => ResolveAlias(value, FuelAliases, "unknown_fuel", "fuel_unmapped_candidate_default"))
                .Where(mapping => mapping.IsMapped)
                .OrderByDescending(mapping => mapping.Score)
                .FirstOrDefault();

            return bestMapping.IsMapped
                ? new ComponentScore(bestMapping.Score, null)
                : ComponentScore.Default("fuel_unmapped_candidate_default");
        }

        if (treeCoverDensity.HasValue)
        {
            return new ComponentScore(CandidateParameterSetV1.ClampNormalized(treeCoverDensity.Value / 100.0), null);
        }

        return ComponentScore.Default("fuel_missing_candidate_default");
    }

    private static CanonicalComponentMapping ResolveAlias(
        string normalizedValue,
        IReadOnlyDictionary<string, CanonicalComponentMapping> aliases,
        string unmappedClass,
        string unmappedReason)
    {
        return aliases.TryGetValue(normalizedValue, out var mapping)
            ? mapping
            : new CanonicalComponentMapping(
                unmappedClass,
                CandidateParameterSetV1.CandidateDefaultComponent,
                false,
                unmappedReason);
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

    private static string NormalizeClassifierText(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        var normalized = value.Trim().Normalize(NormalizationForm.FormD);
        var builder = new StringBuilder(normalized.Length);
        var previousWasSeparator = true;

        foreach (var character in normalized)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(character) == UnicodeCategory.NonSpacingMark)
            {
                continue;
            }

            if (char.IsLetterOrDigit(character))
            {
                builder.Append(char.ToLowerInvariant(character));
                previousWasSeparator = false;
                continue;
            }

            if (!previousWasSeparator)
            {
                builder.Append(' ');
                previousWasSeparator = true;
            }
        }

        return builder.ToString().TrimEnd().Normalize(NormalizationForm.FormC);
    }

    private readonly record struct ComponentScore(double Score, string? Limitation)
    {
        public static ComponentScore Default(string limitation)
            => new(CandidateParameterSetV1.CandidateDefaultComponent, limitation);
    }

    private readonly record struct CanonicalComponentMapping(
        string CanonicalClass,
        double Score,
        bool IsMapped,
        string? Reason);
}
