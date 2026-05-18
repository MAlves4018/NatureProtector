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
    public async Task<DailyCellStateLookupResult> GetForReadingAsync(
        NormalizedReading reading,
        Guid? simulationRunId,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(reading);

        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var sensor = await dbContext.SensorNodes
            .AsNoTracking()
            .SingleOrDefaultAsync(entity => entity.Id == reading.SensorId, cancellationToken);

        if (sensor is null)
        {
            return new DailyCellStateLookupResult(null, null, null);
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
            sensor.ConfigurationVersionId);
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

        var domain = existing is null
            ? DailyCellState.FromRiskInput(
                input with
                {
                    GridCellId = gridCellId,
                    ConfigurationVersionId = configurationVersionId
                },
                antecedentState: "runtime-observed",
                candidateParameterSetVersion: "Candidate Parameter Set V1.0",
                provenance: "prevention_pipeline")
            : ToDomain(existing).ApplyRiskInput(input with
            {
                GridCellId = gridCellId,
                ConfigurationVersionId = configurationVersionId
            });

        if (existing is null)
        {
            dbContext.DailyCellStates.Add(ToRecord(domain));
        }
        else
        {
            Apply(existing, domain);
        }

        await dbContext.SaveChangesAsync(cancellationToken);
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
            fireIndexProvenance: record.FireIndexProvenance);
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
            FireIndexProvenance = state.FireIndexProvenance,
            CandidateParameterSetVersion = state.CandidateParameterSetVersion,
            Provenance = state.Provenance,
            LastSourceEventId = state.LastSourceEventId,
            LastUpdatedAt = state.LastUpdatedAt,
            CreatedAt = now,
            UpdatedAt = now
        };
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
        record.FireIndexProvenance = state.FireIndexProvenance;
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
}
