using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using NatureProtector.Infrastructure.Postgres.Persistence;
using NatureProtector.Infrastructure.Postgres.Projection;
using NatureProtector.Prevention.Persistence;
using NatureProtector.Prevention.Readings;
using NatureProtector.Prevention.Risk;

namespace NatureProtector.Prevention.Host.Persistence;

public sealed class PostgresDailyCellStateRepository(
    IDbContextFactory<NatureProtectorControlDbContext> dbContextFactory) : IDailyCellStateRepository
{
    private static readonly IFireWeatherIndexCalculator FireWeatherIndexCalculator =
        new CanadianFireWeatherIndexCalculator();
    private static readonly IKbdiCalculator KbdiCalculator = new CandidateKbdiCalculator();

    public async Task<DailyCellStateLookupResult> GetForReadingAsync(
        NormalizedReading reading,
        Guid? simulationRunId,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(reading);

        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var sensor = await dbContext.SensorNodes
            .AsNoTracking()
            .Include(entity => entity.GridCell)
            .SingleOrDefaultAsync(entity => entity.Id == reading.SensorId, cancellationToken);

        if (sensor is null)
        {
            return new DailyCellStateLookupResult(null, null, null, TerritorialRiskContext.Unknown(null));
        }

        var day = NormalizeDay(reading.EventTime);
        var state = await dbContext.DailyCellStates
            .AsNoTracking()
            .Where(entity =>
                entity.AreaId == reading.AreaId &&
                entity.GridCellId == sensor.GridCellId &&
                entity.LogicalDate == day &&
                entity.SimulationRunId == simulationRunId)
            .SingleOrDefaultAsync(cancellationToken);

        return new DailyCellStateLookupResult(
            state is null ? null : ToDomain(state),
            sensor.GridCellId,
            sensor.ConfigurationVersionId,
            sensor.GridCell is null
                ? TerritorialRiskContext.Unknown(sensor.GridCellId)
                : TerritorialRiskContext.FromCellData(
                    sensor.GridCell.Id,
                    sensor.GridCell.StructuralHazard,
                    sensor.GridCell.LandCoverClass,
                    sensor.GridCell.DominantForestType,
                    sensor.GridCell.DominantFuelModel,
                    sensor.GridCell.TreeCoverDensity,
                    sensor.GridCell.SlopeDegrees,
                    sensor.GridCell.AspectDegrees,
                    sensor.GridCell.AltitudeMeters,
                    "control_grid_cell"));
    }

    public async Task UpsertAsync(
        RiskInput input,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(input);

        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var sensor = await dbContext.SensorNodes
            .AsNoTracking()
            .SingleOrDefaultAsync(entity => entity.Id == input.SensorId, cancellationToken);

        var gridCellId = input.GridCellId ?? sensor?.GridCellId;
        if (!gridCellId.HasValue)
        {
            return;
        }

        var configurationVersionId = input.ConfigurationVersionId ?? sensor?.ConfigurationVersionId;
        var day = NormalizeDay(input.EventTime);
        var existing = await dbContext.DailyCellStates
            .SingleOrDefaultAsync(entity =>
                entity.AreaId == input.AreaId &&
                entity.GridCellId == gridCellId.Value &&
                entity.LogicalDate == day &&
                entity.SimulationRunId == input.SimulationRunId,
                cancellationToken);
        var scenarioDailyReference = await LoadScenarioDailyReferenceAsync(
            dbContext,
            input.SimulationRunId,
            cancellationToken);

        if (existing is null)
        {
            var domain = BuildDailyCellState(
                existingState: null,
                input,
                gridCellId.Value,
                configurationVersionId,
                scenarioDailyReference);
            dbContext.DailyCellStates.Add(ToRecord(domain));

            try
            {
                await dbContext.SaveChangesAsync(cancellationToken);
                return;
            }
            catch (DbUpdateException exception) when (ExpectedUniqueViolationDetector.IsExpected(
                exception,
                NatureProtectorUniqueConstraints.DailyCellStateCellDayRun))
            {
                dbContext.ChangeTracker.Clear();
                existing = await dbContext.DailyCellStates
                    .SingleOrDefaultAsync(entity =>
                        entity.AreaId == input.AreaId &&
                        entity.GridCellId == gridCellId.Value &&
                        entity.LogicalDate == day &&
                        entity.SimulationRunId == input.SimulationRunId,
                        cancellationToken);

                if (existing is null)
                {
                    throw;
                }
            }
        }

        var updated = BuildDailyCellState(
            existingState: ToDomain(existing),
            input,
            gridCellId.Value,
            configurationVersionId,
            scenarioDailyReference);
        Apply(existing, updated);

        await dbContext.SaveChangesAsync(cancellationToken);
    }

    private static DailyCellState BuildDailyCellState(
        DailyCellState? existingState,
        RiskInput input,
        Guid gridCellId,
        Guid? configurationVersionId,
        ScenarioDailyReference? scenarioDailyReference)
    {
        var resolvedInput = input with
        {
            GridCellId = gridCellId,
            ConfigurationVersionId = configurationVersionId
        };
        var state = existingState is null
            ? DailyCellState.FromRiskInput(
                resolvedInput,
                antecedentState: "runtime-observed",
                candidateParameterSetVersion: CandidateParameterSetV1.Version,
                provenance: "prevention_pipeline")
            : existingState.ApplyRiskInput(resolvedInput);

        state = ApplyScenarioDailyReference(state, scenarioDailyReference);
        state = ApplyFireWeatherIndex(ApplyKbdi(state));
        return existingState is null
            ? MarkFirstDailyKbdiAsLimited(state)
            : state;
    }

    private static DailyCellState ApplyScenarioDailyReference(
        DailyCellState state,
        ScenarioDailyReference? reference)
    {
        if (reference is null)
        {
            return state;
        }

        return state.WithScenarioDailyReference(
            reference.PrecipitationTotalMillimeters,
            reference.TemperatureMaxCelsius,
            reference.RelativeHumidityMinPercent,
            reference.WindSpeedMaxMetersPerSecond,
            reference.FireIndexReferenceKind);
    }

    private static async Task<ScenarioDailyReference?> LoadScenarioDailyReferenceAsync(
        NatureProtectorControlDbContext dbContext,
        Guid? simulationRunId,
        CancellationToken cancellationToken)
    {
        if (!simulationRunId.HasValue)
        {
            return null;
        }

        var run = await dbContext.SimulationRuns
            .AsNoTracking()
            .SingleOrDefaultAsync(entity => entity.Id == simulationRunId.Value, cancellationToken);

        if (run is null)
        {
            return null;
        }

        var scenario = await dbContext.ScenarioDefinitions
            .AsNoTracking()
            .SingleOrDefaultAsync(entity => entity.Id == run.ScenarioId, cancellationToken);

        if (scenario is null || string.IsNullOrWhiteSpace(scenario.ParametersJson))
        {
            return null;
        }

        using var document = JsonDocument.Parse(scenario.ParametersJson);
        if (!document.RootElement.TryGetProperty("daily_reference", out var dailyReference) ||
            dailyReference.ValueKind != JsonValueKind.Object)
        {
            return null;
        }

        return new ScenarioDailyReference(
            TemperatureMaxCelsius: GetOptionalDouble(dailyReference, "temperature_max_c"),
            TemperatureMeanCelsius: GetOptionalDouble(dailyReference, "temperature_mean_c"),
            TemperatureMinCelsius: GetOptionalDouble(dailyReference, "temperature_min_c"),
            RelativeHumidityMinPercent: GetOptionalDouble(dailyReference, "relative_humidity_min_pct"),
            PrecipitationTotalMillimeters: GetOptionalDouble(dailyReference, "precipitation_total_mm"),
            WindSpeedMaxMetersPerSecond: GetOptionalDouble(dailyReference, "wind_speed_max_ms"),
            FireWeatherIndexReference: GetOptionalDouble(dailyReference, "fwi_reference"),
            KeetchByramDroughtIndexReference: GetOptionalDouble(dailyReference, "kbdi_reference"),
            FireIndexReferenceKind: GetOptionalString(dailyReference, "fire_index_reference_kind"));
    }

    private static double? GetOptionalDouble(JsonElement element, string propertyName)
    {
        return element.TryGetProperty(propertyName, out var property) &&
            property.ValueKind == JsonValueKind.Number
                ? property.GetDouble()
                : null;
    }

    private static string? GetOptionalString(JsonElement element, string propertyName)
    {
        return element.TryGetProperty(propertyName, out var property) &&
            property.ValueKind == JsonValueKind.String
                ? property.GetString()
                : null;
    }

    private static DailyCellState ToDomain(DailyCellStateRecord record)
    {
        return new DailyCellState(
            areaId: record.AreaId,
            sensorId: record.SensorId ?? Guid.Empty,
            day: record.LogicalDate,
            antecedentState: record.AntecedentState,
            candidateParameterSetVersion: record.CandidateParameterSetVersion,
            provenance: record.Provenance,
            lastUpdatedAt: record.LastUpdatedAt,
            dailyPrecipitationMillimeters: record.DailyPrecipitationMillimeters,
            maxTemperatureCelsius: record.MaxTemperatureCelsius,
            lastSourceEventId: record.LastSourceEventId,
            gridCellId: record.GridCellId,
            simulationRunId: record.SimulationRunId,
            configurationVersionId: record.ConfigurationVersionId,
            latestHumidityPercent: record.LatestHumidityPercent,
            latestWindSpeedMetersPerSecond: record.LatestWindSpeedMetersPerSecond,
            droughtContext: record.DroughtContext,
            fireWeatherIndex: record.FireWeatherIndex,
            keetchByramDroughtIndex: record.KeetchByramDroughtIndex,
            previousKeetchByramDroughtIndex: record.PreviousKeetchByramDroughtIndex,
            normalizedKeetchByramDroughtIndex: record.NormalizedKeetchByramDroughtIndex,
            kbdiCalculationStatus: Enum.TryParse<KbdiCalculationStatus>(
                record.KbdiCalculationStatus,
                out var kbdiStatus)
                ? kbdiStatus
                : KbdiCalculationStatus.Missing,
            kbdiLimitations: record.KbdiLimitations,
            fireIndexProvenance: record.FireIndexProvenance,
            fineFuelMoistureCode: record.FineFuelMoistureCode,
            duffMoistureCode: record.DuffMoistureCode,
            droughtCode: record.DroughtCode,
            initialSpreadIndex: record.InitialSpreadIndex,
            buildupIndex: record.BuildupIndex,
            normalizedFireWeatherIndex: record.NormalizedFireWeatherIndex,
            fireWeatherCalculationStatus: Enum.TryParse<FireWeatherIndexCalculationStatus>(
                record.FireWeatherCalculationStatus,
                out var status)
                ? status
                : FireWeatherIndexCalculationStatus.Missing,
            fireWeatherLimitations: record.FireWeatherLimitations);
    }

    private static DailyCellStateRecord ToRecord(DailyCellState state)
    {
        var now = DateTimeOffset.UtcNow;
        return new DailyCellStateRecord
        {
            Id = Guid.NewGuid(),
            AreaId = state.AreaId,
            GridCellId = state.GridCellId ?? throw new InvalidOperationException("DailyCellState GridCellId is required for PostgreSQL persistence."),
            SensorId = state.SensorId,
            SimulationRunId = state.SimulationRunId,
            ConfigurationVersionId = state.ConfigurationVersionId,
            LogicalDate = state.Day,
            DailyPrecipitationMillimeters = state.DailyPrecipitationMillimeters,
            MaxTemperatureCelsius = state.MaxTemperatureCelsius,
            LatestHumidityPercent = state.LatestHumidityPercent,
            LatestWindSpeedMetersPerSecond = state.LatestWindSpeedMetersPerSecond,
            AntecedentState = state.AntecedentState,
            DroughtContext = state.DroughtContext,
            FireWeatherIndex = state.FireWeatherIndex,
            KeetchByramDroughtIndex = state.KeetchByramDroughtIndex,
            PreviousKeetchByramDroughtIndex = state.PreviousKeetchByramDroughtIndex,
            NormalizedKeetchByramDroughtIndex = state.NormalizedKeetchByramDroughtIndex,
            KbdiCalculationStatus = state.KbdiCalculationStatus.ToString(),
            KbdiLimitations = state.KbdiLimitations,
            FireIndexProvenance = state.FireIndexProvenance,
            FineFuelMoistureCode = state.FineFuelMoistureCode,
            DuffMoistureCode = state.DuffMoistureCode,
            DroughtCode = state.DroughtCode,
            InitialSpreadIndex = state.InitialSpreadIndex,
            BuildupIndex = state.BuildupIndex,
            NormalizedFireWeatherIndex = state.NormalizedFireWeatherIndex,
            FireWeatherCalculationStatus = state.FireWeatherCalculationStatus.ToString(),
            FireWeatherLimitations = state.FireWeatherLimitations,
            CandidateParameterSetVersion = state.CandidateParameterSetVersion,
            Provenance = state.Provenance,
            LastSourceEventId = state.LastSourceEventId,
            LastUpdatedAt = state.LastUpdatedAt,
            CreatedAt = now,
            UpdatedAt = now
        };
    }

    private static DailyCellState ApplyFireWeatherIndex(DailyCellState state)
    {
        if (state.FireWeatherIndex.HasValue &&
            (state.FireIndexProvenance.Contains("import", StringComparison.OrdinalIgnoreCase) ||
             state.FireIndexProvenance.Contains("reference", StringComparison.OrdinalIgnoreCase)))
        {
            return state;
        }

        var result = FireWeatherIndexCalculator.Calculate(new FireWeatherIndexInput(
            TemperatureCelsius: state.MaxTemperatureCelsius,
            RelativeHumidityPercent: state.LatestHumidityPercent,
            WindSpeedMetersPerSecond: state.LatestWindSpeedMetersPerSecond,
            Precipitation24hMillimeters: state.DailyPrecipitationMillimeters,
            Month: state.Day.Month,
            PreviousFineFuelMoistureCode: state.FineFuelMoistureCode,
            PreviousDuffMoistureCode: state.DuffMoistureCode,
            PreviousDroughtCode: state.DroughtCode));

        return result.Status == FireWeatherIndexCalculationStatus.Complete || !state.FireWeatherIndex.HasValue
            ? state.WithFireWeatherIndex(result)
            : state;
    }

    private static DailyCellState ApplyKbdi(DailyCellState state)
    {
        if (state.KeetchByramDroughtIndex.HasValue &&
            (state.FireIndexProvenance.Contains("import", StringComparison.OrdinalIgnoreCase) ||
             state.FireIndexProvenance.Contains("reference", StringComparison.OrdinalIgnoreCase)))
        {
            return state;
        }

        var result = KbdiCalculator.Calculate(new KbdiInput(
            MaxTemperatureCelsius: state.MaxTemperatureCelsius,
            Precipitation24hMillimeters: state.DailyPrecipitationMillimeters,
            PreviousKeetchByramDroughtIndex: state.PreviousKeetchByramDroughtIndex));
        result = MarkLimitedAntecedentHistoryForFirstDailyCalculation(state, result);

        return IsUsableKbdi(result.Status) && !state.KeetchByramDroughtIndex.HasValue
            ? state.WithKbdi(result)
            : state;
    }

    private static KbdiResult MarkLimitedAntecedentHistoryForFirstDailyCalculation(
        DailyCellState state,
        KbdiResult result)
    {
        if (state.KeetchByramDroughtIndex.HasValue ||
            result.KeetchByramDroughtIndex is null ||
            result.Status is KbdiCalculationStatus.Missing or KbdiCalculationStatus.Partial)
        {
            return result;
        }

        var limitations = result.Limitations
            .Append("limited_antecedent_history")
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        return new KbdiResult(
            KbdiCalculationStatus.LimitedAntecedentHistory,
            result.InputCompleteness,
            result.PreviousKeetchByramDroughtIndex,
            result.KeetchByramDroughtIndex,
            result.NormalizedKeetchByramDroughtIndex,
            result.Provenance,
            limitations);
    }

    private static bool IsUsableKbdi(KbdiCalculationStatus status)
    {
        return status is KbdiCalculationStatus.Complete or
            KbdiCalculationStatus.CompleteWithCandidateDefaults or
            KbdiCalculationStatus.LimitedAntecedentHistory or
            KbdiCalculationStatus.CalculatedFromHistory or
            KbdiCalculationStatus.ReferenceImported;
    }

    private static DailyCellState MarkFirstDailyKbdiAsLimited(DailyCellState state)
    {
        if (!state.KeetchByramDroughtIndex.HasValue ||
            state.KbdiCalculationStatus != KbdiCalculationStatus.Complete)
        {
            return state;
        }

        return state.WithKbdi(new KbdiResult(
            KbdiCalculationStatus.LimitedAntecedentHistory,
            1.0,
            state.PreviousKeetchByramDroughtIndex,
            state.KeetchByramDroughtIndex,
            state.NormalizedKeetchByramDroughtIndex,
            "candidate_kbdi_calculator",
            (state.KbdiLimitations ?? string.Empty)
                .Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Append("limited_antecedent_history")
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray()));
    }

    private static void Apply(DailyCellStateRecord record, DailyCellState state)
    {
        record.SensorId = state.SensorId;
        record.ConfigurationVersionId = state.ConfigurationVersionId;
        record.DailyPrecipitationMillimeters = state.DailyPrecipitationMillimeters;
        record.MaxTemperatureCelsius = state.MaxTemperatureCelsius;
        record.LatestHumidityPercent = state.LatestHumidityPercent;
        record.LatestWindSpeedMetersPerSecond = state.LatestWindSpeedMetersPerSecond;
        record.AntecedentState = state.AntecedentState;
        record.DroughtContext = state.DroughtContext;
        record.FireWeatherIndex = state.FireWeatherIndex;
        record.KeetchByramDroughtIndex = state.KeetchByramDroughtIndex;
        record.PreviousKeetchByramDroughtIndex = state.PreviousKeetchByramDroughtIndex;
        record.NormalizedKeetchByramDroughtIndex = state.NormalizedKeetchByramDroughtIndex;
        record.KbdiCalculationStatus = state.KbdiCalculationStatus.ToString();
        record.KbdiLimitations = state.KbdiLimitations;
        record.FireIndexProvenance = state.FireIndexProvenance;
        record.FineFuelMoistureCode = state.FineFuelMoistureCode;
        record.DuffMoistureCode = state.DuffMoistureCode;
        record.DroughtCode = state.DroughtCode;
        record.InitialSpreadIndex = state.InitialSpreadIndex;
        record.BuildupIndex = state.BuildupIndex;
        record.NormalizedFireWeatherIndex = state.NormalizedFireWeatherIndex;
        record.FireWeatherCalculationStatus = state.FireWeatherCalculationStatus.ToString();
        record.FireWeatherLimitations = state.FireWeatherLimitations;
        record.CandidateParameterSetVersion = state.CandidateParameterSetVersion;
        record.Provenance = state.Provenance;
        record.LastSourceEventId = state.LastSourceEventId;
        record.LastUpdatedAt = state.LastUpdatedAt;
        record.UpdatedAt = DateTimeOffset.UtcNow;
    }

    private static DateTimeOffset NormalizeDay(DateTimeOffset value)
    {
        var utc = value.ToUniversalTime();
        return new DateTimeOffset(utc.Year, utc.Month, utc.Day, 0, 0, 0, TimeSpan.Zero);
    }

    private sealed record ScenarioDailyReference(
        double? TemperatureMaxCelsius,
        double? TemperatureMeanCelsius,
        double? TemperatureMinCelsius,
        double? RelativeHumidityMinPercent,
        double? PrecipitationTotalMillimeters,
        double? WindSpeedMaxMetersPerSecond,
        double? FireWeatherIndexReference,
        double? KeetchByramDroughtIndexReference,
        string? FireIndexReferenceKind);
}
