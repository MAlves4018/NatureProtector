using NatureProtector.Prevention.Readings;
using NatureProtector.Shared.Contracts.Readings;

namespace NatureProtector.Prevention.Risk;

/// <summary>
/// Estado diário interno por célula/sensor para suportar modelos com memória
/// temporal sem acoplar ao scoring atual.
/// </summary>
public sealed class DailyCellState
{
    public Guid AreaId { get; }

    public Guid SensorId { get; }

    public Guid? GridCellId { get; }

    public Guid? SimulationRunId { get; }

    public Guid? ConfigurationVersionId { get; }

    public DateTimeOffset Day { get; }

    public double? DailyPrecipitationMillimeters { get; }

    public double? MaxTemperatureCelsius { get; }

    public double? LatestHumidityPercent { get; }

    public double? LatestWindSpeedMetersPerSecond { get; }

    public string DroughtContext { get; }

    public double? FireWeatherIndex { get; }

    public double? KeetchByramDroughtIndex { get; }

    public double? PreviousKeetchByramDroughtIndex { get; }

    public double? NormalizedKeetchByramDroughtIndex { get; }

    public KbdiCalculationStatus KbdiCalculationStatus { get; }

    public string? KbdiLimitations { get; }

    public string FireIndexProvenance { get; }

    public double? FineFuelMoistureCode { get; }

    public double? DuffMoistureCode { get; }

    public double? DroughtCode { get; }

    public double? InitialSpreadIndex { get; }

    public double? BuildupIndex { get; }

    public double? NormalizedFireWeatherIndex { get; }

    public FireWeatherIndexCalculationStatus FireWeatherCalculationStatus { get; }

    public string? FireWeatherLimitations { get; }

    public string AntecedentState { get; }

    public string CandidateParameterSetVersion { get; }

    public string Provenance { get; }

    public Guid? LastSourceEventId { get; }

    public DateTimeOffset LastUpdatedAt { get; }

    public DailyCellState(
        Guid areaId,
        Guid sensorId,
        DateTimeOffset day,
        string antecedentState,
        string candidateParameterSetVersion,
        string provenance,
        DateTimeOffset lastUpdatedAt,
        double? dailyPrecipitationMillimeters = null,
        double? maxTemperatureCelsius = null,
        Guid? lastSourceEventId = null,
        Guid? gridCellId = null,
        Guid? simulationRunId = null,
        Guid? configurationVersionId = null,
        double? latestHumidityPercent = null,
        double? latestWindSpeedMetersPerSecond = null,
        string? droughtContext = null,
        double? fireWeatherIndex = null,
        double? keetchByramDroughtIndex = null,
        double? previousKeetchByramDroughtIndex = null,
        double? normalizedKeetchByramDroughtIndex = null,
        KbdiCalculationStatus? kbdiCalculationStatus = null,
        string? kbdiLimitations = null,
        string? fireIndexProvenance = null,
        double? fineFuelMoistureCode = null,
        double? duffMoistureCode = null,
        double? droughtCode = null,
        double? initialSpreadIndex = null,
        double? buildupIndex = null,
        double? normalizedFireWeatherIndex = null,
        FireWeatherIndexCalculationStatus? fireWeatherCalculationStatus = null,
        string? fireWeatherLimitations = null)
    {
        if (areaId == Guid.Empty)
        {
            throw new ArgumentException("AreaId must not be an empty GUID.", nameof(areaId));
        }

        if (sensorId == Guid.Empty)
        {
            throw new ArgumentException("SensorId must not be an empty GUID.", nameof(sensorId));
        }

        if (day == default)
        {
            throw new ArgumentException("Day must be a valid, non-default value.", nameof(day));
        }

        if (lastUpdatedAt == default)
        {
            throw new ArgumentException("LastUpdatedAt must be a valid, non-default value.", nameof(lastUpdatedAt));
        }

        if (lastSourceEventId == Guid.Empty)
        {
            throw new ArgumentException("LastSourceEventId must not be an empty GUID.", nameof(lastSourceEventId));
        }

        if (gridCellId == Guid.Empty)
        {
            throw new ArgumentException("GridCellId must not be an empty GUID.", nameof(gridCellId));
        }

        if (simulationRunId == Guid.Empty)
        {
            throw new ArgumentException("SimulationRunId must not be an empty GUID.", nameof(simulationRunId));
        }

        if (configurationVersionId == Guid.Empty)
        {
            throw new ArgumentException("ConfigurationVersionId must not be an empty GUID.", nameof(configurationVersionId));
        }

        if (string.IsNullOrWhiteSpace(antecedentState))
        {
            throw new ArgumentException("AntecedentState is required.", nameof(antecedentState));
        }

        if (string.IsNullOrWhiteSpace(candidateParameterSetVersion))
        {
            throw new ArgumentException("CandidateParameterSetVersion is required.", nameof(candidateParameterSetVersion));
        }

        if (string.IsNullOrWhiteSpace(provenance))
        {
            throw new ArgumentException("Provenance is required.", nameof(provenance));
        }

        ValidateOptionalNonNegativeFinite(dailyPrecipitationMillimeters, nameof(dailyPrecipitationMillimeters));
        ValidateOptionalFinite(maxTemperatureCelsius, nameof(maxTemperatureCelsius));
        ValidateOptionalFinite(latestHumidityPercent, nameof(latestHumidityPercent));
        ValidateOptionalFinite(latestWindSpeedMetersPerSecond, nameof(latestWindSpeedMetersPerSecond));
        ValidateOptionalNonNegativeFinite(fireWeatherIndex, nameof(fireWeatherIndex));
        ValidateOptionalNonNegativeFinite(keetchByramDroughtIndex, nameof(keetchByramDroughtIndex));
        ValidateOptionalNonNegativeFinite(previousKeetchByramDroughtIndex, nameof(previousKeetchByramDroughtIndex));
        ValidateOptionalFinite(normalizedKeetchByramDroughtIndex, nameof(normalizedKeetchByramDroughtIndex));
        ValidateOptionalNonNegativeFinite(fineFuelMoistureCode, nameof(fineFuelMoistureCode));
        ValidateOptionalNonNegativeFinite(duffMoistureCode, nameof(duffMoistureCode));
        ValidateOptionalNonNegativeFinite(droughtCode, nameof(droughtCode));
        ValidateOptionalNonNegativeFinite(initialSpreadIndex, nameof(initialSpreadIndex));
        ValidateOptionalNonNegativeFinite(buildupIndex, nameof(buildupIndex));
        ValidateOptionalFinite(normalizedFireWeatherIndex, nameof(normalizedFireWeatherIndex));

        if (normalizedFireWeatherIndex is < 0.0 or > 1.0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(normalizedFireWeatherIndex),
                normalizedFireWeatherIndex,
                "NormalizedFireWeatherIndex must be in the range [0, 1] when provided.");
        }

        if (normalizedKeetchByramDroughtIndex is < 0.0 or > 1.0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(normalizedKeetchByramDroughtIndex),
                normalizedKeetchByramDroughtIndex,
                "NormalizedKeetchByramDroughtIndex must be in the range [0, 1] when provided.");
        }

        AreaId = areaId;
        SensorId = sensorId;
        GridCellId = gridCellId;
        SimulationRunId = simulationRunId;
        ConfigurationVersionId = configurationVersionId;
        Day = NormalizeDay(day);
        DailyPrecipitationMillimeters = dailyPrecipitationMillimeters;
        MaxTemperatureCelsius = maxTemperatureCelsius;
        LatestHumidityPercent = latestHumidityPercent;
        LatestWindSpeedMetersPerSecond = latestWindSpeedMetersPerSecond;
        DroughtContext = string.IsNullOrWhiteSpace(droughtContext) ? "unknown" : droughtContext.Trim();
        FireWeatherIndex = fireWeatherIndex;
        KeetchByramDroughtIndex = keetchByramDroughtIndex;
        PreviousKeetchByramDroughtIndex = previousKeetchByramDroughtIndex;
        NormalizedKeetchByramDroughtIndex = normalizedKeetchByramDroughtIndex ??
            (keetchByramDroughtIndex.HasValue
                ? CandidateKbdiCalculator.NormalizeKbdi(keetchByramDroughtIndex.Value)
                : null);
        KbdiCalculationStatus = kbdiCalculationStatus ?? (keetchByramDroughtIndex.HasValue
            ? NatureProtector.Prevention.Risk.KbdiCalculationStatus.Complete
            : NatureProtector.Prevention.Risk.KbdiCalculationStatus.Missing);
        KbdiLimitations = string.IsNullOrWhiteSpace(kbdiLimitations) ? null : kbdiLimitations.Trim();
        FireIndexProvenance = string.IsNullOrWhiteSpace(fireIndexProvenance)
            ? (fireWeatherIndex.HasValue || keetchByramDroughtIndex.HasValue ? "unknown" : "absent")
            : fireIndexProvenance.Trim();
        FineFuelMoistureCode = fineFuelMoistureCode;
        DuffMoistureCode = duffMoistureCode;
        DroughtCode = droughtCode;
        InitialSpreadIndex = initialSpreadIndex;
        BuildupIndex = buildupIndex;
        NormalizedFireWeatherIndex = normalizedFireWeatherIndex ??
            (fireWeatherIndex.HasValue
                ? CanadianFireWeatherIndexCalculator.NormalizeFireWeatherIndex(fireWeatherIndex.Value)
                : null);
        FireWeatherCalculationStatus = fireWeatherCalculationStatus ?? (fireWeatherIndex.HasValue
            ? FireWeatherIndexCalculationStatus.Complete
            : FireWeatherIndexCalculationStatus.Missing);
        FireWeatherLimitations = string.IsNullOrWhiteSpace(fireWeatherLimitations)
            ? null
            : fireWeatherLimitations.Trim();
        AntecedentState = antecedentState.Trim();
        CandidateParameterSetVersion = candidateParameterSetVersion.Trim();
        Provenance = provenance.Trim();
        LastSourceEventId = lastSourceEventId;
        LastUpdatedAt = lastUpdatedAt;
    }

    public static DailyCellState FromRiskInput(
        RiskInput input,
        string antecedentState,
        string candidateParameterSetVersion,
        string provenance,
        double? dailyPrecipitationMillimeters = null)
    {
        ArgumentNullException.ThrowIfNull(input);

        return new DailyCellState(
            areaId: input.AreaId,
            sensorId: input.SensorId,
            day: input.EventTime,
            antecedentState: antecedentState,
            candidateParameterSetVersion: candidateParameterSetVersion,
            provenance: provenance,
            lastUpdatedAt: input.EventTime,
            dailyPrecipitationMillimeters: dailyPrecipitationMillimeters,
            maxTemperatureCelsius: ResolveTemperatureIfAvailable(input.MetricType, input.Unit, input.Value),
            lastSourceEventId: input.SourceEventId,
            gridCellId: input.GridCellId,
            simulationRunId: input.SimulationRunId,
            configurationVersionId: input.ConfigurationVersionId,
            latestHumidityPercent: ResolveHumidityIfAvailable(input.MetricType, input.Unit, input.Value),
            latestWindSpeedMetersPerSecond: ResolveWindSpeedIfAvailable(input.MetricType, input.Unit, input.Value),
            droughtContext: input.DailyCellState?.DroughtContext,
            fireWeatherIndex: input.FireWeatherIndexContext.FireWeatherIndex,
            keetchByramDroughtIndex: input.FireWeatherIndexContext.KeetchByramDroughtIndex,
            previousKeetchByramDroughtIndex: input.FireWeatherIndexContext.PreviousKeetchByramDroughtIndex,
            normalizedKeetchByramDroughtIndex: input.FireWeatherIndexContext.NormalizedKeetchByramDroughtIndex,
            kbdiCalculationStatus: input.FireWeatherIndexContext.KbdiCalculationStatus,
            kbdiLimitations: input.FireWeatherIndexContext.Limitations,
            fireIndexProvenance: input.FireWeatherIndexContext.Provenance,
            fineFuelMoistureCode: input.FireWeatherIndexContext.FineFuelMoistureCode,
            duffMoistureCode: input.FireWeatherIndexContext.DuffMoistureCode,
            droughtCode: input.FireWeatherIndexContext.DroughtCode,
            initialSpreadIndex: input.FireWeatherIndexContext.InitialSpreadIndex,
            buildupIndex: input.FireWeatherIndexContext.BuildupIndex,
            normalizedFireWeatherIndex: input.FireWeatherIndexContext.NormalizedFireWeatherIndex,
            fireWeatherCalculationStatus: input.FireWeatherIndexContext.CalculationStatus,
            fireWeatherLimitations: input.FireWeatherIndexContext.Limitations);
    }

    public static DailyCellState FromNormalizedReading(
        NormalizedReading reading,
        string antecedentState,
        string candidateParameterSetVersion,
        string provenance,
        double? dailyPrecipitationMillimeters = null)
    {
        ArgumentNullException.ThrowIfNull(reading);

        return new DailyCellState(
            areaId: reading.AreaId,
            sensorId: reading.SensorId,
            day: reading.EventTime,
            antecedentState: antecedentState,
            candidateParameterSetVersion: candidateParameterSetVersion,
            provenance: provenance,
            lastUpdatedAt: reading.EventTime,
            dailyPrecipitationMillimeters: dailyPrecipitationMillimeters,
            maxTemperatureCelsius: ResolveTemperatureIfAvailable(reading.MetricType, reading.Unit, reading.Value),
            lastSourceEventId: reading.EventId);
    }

    public DailyCellState ApplyRiskInput(RiskInput input)
    {
        ArgumentNullException.ThrowIfNull(input);

        if (input.AreaId != AreaId)
        {
            throw new InvalidOperationException("RiskInput AreaId does not match DailyCellState AreaId.");
        }

        if (input.GridCellId.HasValue &&
            GridCellId.HasValue &&
            input.GridCellId.Value != GridCellId.Value)
        {
            throw new InvalidOperationException("RiskInput GridCellId does not match DailyCellState GridCellId.");
        }

        if (input.SimulationRunId.HasValue &&
            SimulationRunId.HasValue &&
            input.SimulationRunId.Value != SimulationRunId.Value)
        {
            throw new InvalidOperationException("RiskInput SimulationRunId does not match DailyCellState SimulationRunId.");
        }

        if (input.ConfigurationVersionId.HasValue &&
            ConfigurationVersionId.HasValue &&
            input.ConfigurationVersionId.Value != ConfigurationVersionId.Value)
        {
            throw new InvalidOperationException("RiskInput ConfigurationVersionId does not match DailyCellState ConfigurationVersionId.");
        }

        if (NormalizeDay(input.EventTime) != Day)
        {
            throw new InvalidOperationException("RiskInput EventTime is outside DailyCellState day.");
        }

        var incomingTemperature = ResolveTemperatureIfAvailable(input.MetricType, input.Unit, input.Value);
        var nextMaxTemperature = incomingTemperature.HasValue
            ? (MaxTemperatureCelsius.HasValue
                ? Math.Max(MaxTemperatureCelsius.Value, incomingTemperature.Value)
                : incomingTemperature.Value)
            : MaxTemperatureCelsius;
        var incomingHumidity = ResolveHumidityIfAvailable(input.MetricType, input.Unit, input.Value);
        var incomingWindSpeed = ResolveWindSpeedIfAvailable(input.MetricType, input.Unit, input.Value);

        return new DailyCellState(
            areaId: AreaId,
            sensorId: input.SensorId,
            day: Day,
            antecedentState: AntecedentState,
            candidateParameterSetVersion: CandidateParameterSetVersion,
            provenance: Provenance,
            lastUpdatedAt: input.EventTime,
            dailyPrecipitationMillimeters: DailyPrecipitationMillimeters,
            maxTemperatureCelsius: nextMaxTemperature,
            lastSourceEventId: input.SourceEventId,
            gridCellId: GridCellId ?? input.GridCellId,
            simulationRunId: SimulationRunId ?? input.SimulationRunId,
            configurationVersionId: ConfigurationVersionId ?? input.ConfigurationVersionId,
            latestHumidityPercent: incomingHumidity ?? LatestHumidityPercent,
            latestWindSpeedMetersPerSecond: incomingWindSpeed ?? LatestWindSpeedMetersPerSecond,
            droughtContext: DroughtContext,
            fireWeatherIndex: FireWeatherIndex,
            keetchByramDroughtIndex: KeetchByramDroughtIndex,
            previousKeetchByramDroughtIndex: PreviousKeetchByramDroughtIndex,
            normalizedKeetchByramDroughtIndex: NormalizedKeetchByramDroughtIndex,
            kbdiCalculationStatus: KbdiCalculationStatus,
            kbdiLimitations: KbdiLimitations,
            fireIndexProvenance: FireIndexProvenance,
            fineFuelMoistureCode: FineFuelMoistureCode,
            duffMoistureCode: DuffMoistureCode,
            droughtCode: DroughtCode,
            initialSpreadIndex: InitialSpreadIndex,
            buildupIndex: BuildupIndex,
            normalizedFireWeatherIndex: NormalizedFireWeatherIndex,
            fireWeatherCalculationStatus: FireWeatherCalculationStatus,
            fireWeatherLimitations: FireWeatherLimitations);
    }

    public DailyCellState WithScenarioDailyReference(
        double? dailyPrecipitationMillimeters,
        double? maxTemperatureCelsius,
        double? relativeHumidityMinPercent,
        double? windSpeedMaxMetersPerSecond,
        string? referenceKind)
    {
        var nextMaxTemperature = MaxTemperatureCelsius;
        if (maxTemperatureCelsius.HasValue)
        {
            nextMaxTemperature = nextMaxTemperature.HasValue
                ? Math.Max(nextMaxTemperature.Value, maxTemperatureCelsius.Value)
                : maxTemperatureCelsius.Value;
        }

        var nextProvenance = Provenance.Contains("scenario_daily_reference", StringComparison.OrdinalIgnoreCase)
            ? Provenance
            : $"{Provenance};scenario_daily_reference";

        var nextFireIndexProvenance =
            FireIndexProvenance.Contains("scenario_daily_reference", StringComparison.OrdinalIgnoreCase)
                ? FireIndexProvenance
                : FireIndexProvenance == "absent"
                    ? "scenario_daily_reference"
                    : $"{FireIndexProvenance};scenario_daily_reference";

        return new DailyCellState(
            areaId: AreaId,
            sensorId: SensorId,
            day: Day,
            antecedentState: AntecedentState,
            candidateParameterSetVersion: CandidateParameterSetVersion,
            provenance: nextProvenance,
            lastUpdatedAt: LastUpdatedAt,
            dailyPrecipitationMillimeters: dailyPrecipitationMillimeters ?? DailyPrecipitationMillimeters,
            maxTemperatureCelsius: nextMaxTemperature,
            lastSourceEventId: LastSourceEventId,
            gridCellId: GridCellId,
            simulationRunId: SimulationRunId,
            configurationVersionId: ConfigurationVersionId,
            latestHumidityPercent: LatestHumidityPercent ?? relativeHumidityMinPercent,
            latestWindSpeedMetersPerSecond: LatestWindSpeedMetersPerSecond ?? windSpeedMaxMetersPerSecond,
            droughtContext: string.IsNullOrWhiteSpace(referenceKind) ? DroughtContext : referenceKind.Trim(),
            fireWeatherIndex: FireWeatherIndex,
            keetchByramDroughtIndex: KeetchByramDroughtIndex,
            previousKeetchByramDroughtIndex: PreviousKeetchByramDroughtIndex,
            normalizedKeetchByramDroughtIndex: NormalizedKeetchByramDroughtIndex,
            kbdiCalculationStatus: KbdiCalculationStatus,
            kbdiLimitations: KbdiLimitations,
            fireIndexProvenance: nextFireIndexProvenance,
            fineFuelMoistureCode: FineFuelMoistureCode,
            duffMoistureCode: DuffMoistureCode,
            droughtCode: DroughtCode,
            initialSpreadIndex: InitialSpreadIndex,
            buildupIndex: BuildupIndex,
            normalizedFireWeatherIndex: NormalizedFireWeatherIndex,
            fireWeatherCalculationStatus: FireWeatherCalculationStatus,
            fireWeatherLimitations: FireWeatherLimitations);
    }

    public DailyCellState WithKbdi(KbdiResult result)
    {
        ArgumentNullException.ThrowIfNull(result);

        return new DailyCellState(
            areaId: AreaId,
            sensorId: SensorId,
            day: Day,
            antecedentState: AntecedentState,
            candidateParameterSetVersion: CandidateParameterSetVersion,
            provenance: Provenance,
            lastUpdatedAt: LastUpdatedAt,
            dailyPrecipitationMillimeters: DailyPrecipitationMillimeters,
            maxTemperatureCelsius: MaxTemperatureCelsius,
            lastSourceEventId: LastSourceEventId,
            gridCellId: GridCellId,
            simulationRunId: SimulationRunId,
            configurationVersionId: ConfigurationVersionId,
            latestHumidityPercent: LatestHumidityPercent,
            latestWindSpeedMetersPerSecond: LatestWindSpeedMetersPerSecond,
            droughtContext: DroughtContext,
            fireWeatherIndex: FireWeatherIndex,
            keetchByramDroughtIndex: result.KeetchByramDroughtIndex,
            previousKeetchByramDroughtIndex: result.PreviousKeetchByramDroughtIndex,
            normalizedKeetchByramDroughtIndex: result.NormalizedKeetchByramDroughtIndex,
            kbdiCalculationStatus: result.Status,
            kbdiLimitations: result.Limitations.Count == 0 ? null : string.Join(";", result.Limitations),
            fireIndexProvenance: FireIndexProvenance == "absent"
                ? result.Provenance
                : $"{FireIndexProvenance};{result.Provenance}",
            fineFuelMoistureCode: FineFuelMoistureCode,
            duffMoistureCode: DuffMoistureCode,
            droughtCode: DroughtCode,
            initialSpreadIndex: InitialSpreadIndex,
            buildupIndex: BuildupIndex,
            normalizedFireWeatherIndex: NormalizedFireWeatherIndex,
            fireWeatherCalculationStatus: FireWeatherCalculationStatus,
            fireWeatherLimitations: FireWeatherLimitations);
    }

    public DailyCellState WithFireWeatherIndex(FireWeatherIndexResult result)
    {
        ArgumentNullException.ThrowIfNull(result);

        return new DailyCellState(
            areaId: AreaId,
            sensorId: SensorId,
            day: Day,
            antecedentState: AntecedentState,
            candidateParameterSetVersion: CandidateParameterSetVersion,
            provenance: Provenance,
            lastUpdatedAt: LastUpdatedAt,
            dailyPrecipitationMillimeters: DailyPrecipitationMillimeters,
            maxTemperatureCelsius: MaxTemperatureCelsius,
            lastSourceEventId: LastSourceEventId,
            gridCellId: GridCellId,
            simulationRunId: SimulationRunId,
            configurationVersionId: ConfigurationVersionId,
            latestHumidityPercent: LatestHumidityPercent,
            latestWindSpeedMetersPerSecond: LatestWindSpeedMetersPerSecond,
            droughtContext: DroughtContext,
            fireWeatherIndex: result.FireWeatherIndex,
            keetchByramDroughtIndex: KeetchByramDroughtIndex,
            fireIndexProvenance: FireIndexProvenance == "absent"
                ? result.Provenance
                : $"{FireIndexProvenance};{result.Provenance}",
            fineFuelMoistureCode: result.FineFuelMoistureCode,
            duffMoistureCode: result.DuffMoistureCode,
            droughtCode: result.DroughtCode,
            initialSpreadIndex: result.InitialSpreadIndex,
            buildupIndex: result.BuildupIndex,
            normalizedFireWeatherIndex: result.NormalizedFireWeatherIndex,
            fireWeatherCalculationStatus: result.Status,
            fireWeatherLimitations: result.Limitations.Count == 0
                ? null
                : string.Join(";", result.Limitations));
    }

    private static DateTimeOffset NormalizeDay(DateTimeOffset value)
    {
        var utc = value.ToUniversalTime();
        return new DateTimeOffset(
            utc.Year,
            utc.Month,
            utc.Day,
            0,
            0,
            0,
            TimeSpan.Zero);
    }

    private static double? ResolveTemperatureIfAvailable(
        SensorMetricType metricType,
        MeasurementUnit unit,
        double value)
    {
        if (metricType == SensorMetricType.Temperature &&
            unit == MeasurementUnit.Celsius)
        {
            ValidateOptionalFinite(value, nameof(value));
            return value;
        }

        return null;
    }

    private static double? ResolveHumidityIfAvailable(
        SensorMetricType metricType,
        MeasurementUnit unit,
        double value)
    {
        if (metricType == SensorMetricType.Humidity &&
            unit == MeasurementUnit.Percent)
        {
            ValidateOptionalFinite(value, nameof(value));
            return value;
        }

        return null;
    }

    private static double? ResolveWindSpeedIfAvailable(
        SensorMetricType metricType,
        MeasurementUnit unit,
        double value)
    {
        if (metricType == SensorMetricType.WindSpeed &&
            unit == MeasurementUnit.MetersPerSecond)
        {
            ValidateOptionalFinite(value, nameof(value));
            return value;
        }

        return null;
    }

    private static void ValidateOptionalFinite(double? value, string paramName)
    {
        if (!value.HasValue)
        {
            return;
        }

        if (double.IsNaN(value.Value) || double.IsInfinity(value.Value))
        {
            throw new ArgumentOutOfRangeException(
                paramName,
                value,
                "Value must be a finite number or null.");
        }
    }

    private static void ValidateOptionalNonNegativeFinite(double? value, string paramName)
    {
        ValidateOptionalFinite(value, paramName);

        if (value.HasValue && value.Value < 0.0)
        {
            throw new ArgumentOutOfRangeException(
                paramName,
                value,
                "Value must be greater than or equal to zero.");
        }
    }
}
