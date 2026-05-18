using Microsoft.EntityFrameworkCore;
using NatureProtector.Infrastructure.Postgres.Control;
using NatureProtector.Infrastructure.Postgres.Pipeline;
using NatureProtector.Infrastructure.Postgres.Projection;
using NatureProtector.Infrastructure.Postgres.Schemas;

namespace NatureProtector.Infrastructure.Postgres.Persistence;

/*
 * Este DbContext modela o esquema relacional do control plane, da pipeline e
 * das projeções operacionais em PostgreSQL.
 *
 * Rationale:
 * - O projeto precisa de um único ponto explícito onde a estrutura persistente
 *   do sistema fique definida.
 * - Centralizar o mapeamento relacional facilita leitura, migrações e revisão
 *   da correspondência entre arquitetura e runtime.
 *
 * Design considerations:
 * - O contexto agrega três áreas de responsabilidade: configuração/control
 *   plane, inbox/pipeline e projeções operacionais.
 * - Timestamps com offset são normalizados para UTC por convenção global.
 * - A configuração é declarada explicitamente por entidade para tornar os nomes
 *   de tabelas, índices e restrições fáceis de localizar.
 */

public sealed class NatureProtectorControlDbContext : DbContext
{
    /// <summary>
    /// Inicializa o DbContext do control plane NatureProtector.
    /// </summary>
    public NatureProtectorControlDbContext(DbContextOptions<NatureProtectorControlDbContext> options)
        : base(options)
    {
    }

    public DbSet<ConfigurationVersionRecord> ConfigurationVersions => Set<ConfigurationVersionRecord>();
    public DbSet<AreaRecord> Areas => Set<AreaRecord>();
    public DbSet<AreaContextRecord> AreaContexts => Set<AreaContextRecord>();
    public DbSet<GridCellRecord> GridCells => Set<GridCellRecord>();
    public DbSet<SensorProfileRecord> SensorProfiles => Set<SensorProfileRecord>();
    public DbSet<SensorNetworkRecord> SensorNetworks => Set<SensorNetworkRecord>();
    public DbSet<SensorNodeRecord> SensorNodes => Set<SensorNodeRecord>();
    public DbSet<ScenarioDefinitionRecord> ScenarioDefinitions => Set<ScenarioDefinitionRecord>();
    public DbSet<SimulationRunRecord> SimulationRuns => Set<SimulationRunRecord>();
    public DbSet<RuleSetVersionRecord> RuleSetVersions => Set<RuleSetVersionRecord>();
    public DbSet<DatasetArtifactRecord> DatasetArtifacts => Set<DatasetArtifactRecord>();
    public DbSet<ScenarioDatasetBindingRecord> ScenarioDatasetBindings => Set<ScenarioDatasetBindingRecord>();
    public DbSet<InboxEventRecord> InboxEvents => Set<InboxEventRecord>();
    public DbSet<ProcessingAttemptRecord> ProcessingAttempts => Set<ProcessingAttemptRecord>();
    public DbSet<RejectedEventRecord> RejectedEvents => Set<RejectedEventRecord>();
    public DbSet<QuarantinedEventRecord> QuarantinedEvents => Set<QuarantinedEventRecord>();
    public DbSet<AcceptedReadingLogRecord> AcceptedReadingLogs => Set<AcceptedReadingLogRecord>();
    public DbSet<RiskAssessmentLogRecord> RiskAssessmentLogs => Set<RiskAssessmentLogRecord>();
    public DbSet<AreaRiskSnapshotLogRecord> AreaRiskSnapshotLogs => Set<AreaRiskSnapshotLogRecord>();
    public DbSet<DailyCellStateRecord> DailyCellStates => Set<DailyCellStateRecord>();
    public DbSet<CellOperationalStateRecord> CellOperationalStates => Set<CellOperationalStateRecord>();
    public DbSet<AreaOperationalStateRecord> AreaOperationalStates => Set<AreaOperationalStateRecord>();
    public DbSet<AlertStateRecord> AlertStates => Set<AlertStateRecord>();

    /// <summary>
    /// Aplica conversores globais para garantir persistência consistente em UTC.
    /// </summary>
    protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
    {
        configurationBuilder.Properties<DateTimeOffset>()
            .HaveConversion<UtcDateTimeOffsetConverter>();

        configurationBuilder.Properties<DateTimeOffset?>()
            .HaveConversion<NullableUtcDateTimeOffsetConverter>();
    }

    /// <summary>
    /// Configura todas as entidades e respetivos mapeamentos do esquema.
    /// </summary>
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        ConfigureConfigurationVersions(modelBuilder);
        ConfigureAreas(modelBuilder);
        ConfigureAreaContexts(modelBuilder);
        ConfigureGridCells(modelBuilder);
        ConfigureSensorProfiles(modelBuilder);
        ConfigureSensorNetworks(modelBuilder);
        ConfigureSensorNodes(modelBuilder);
        ConfigureScenarioDefinitions(modelBuilder);
        ConfigureSimulationRuns(modelBuilder);
        ConfigureRuleSetVersions(modelBuilder);
        ConfigureDatasetArtifacts(modelBuilder);
        ConfigureScenarioDatasetBindings(modelBuilder);
        ConfigureInboxEvents(modelBuilder);
        ConfigureProcessingAttempts(modelBuilder);
        ConfigureRejectedEvents(modelBuilder);
        ConfigureQuarantinedEvents(modelBuilder);
        ConfigureAcceptedReadingLogs(modelBuilder);
        ConfigureRiskAssessmentLogs(modelBuilder);
        ConfigureAreaRiskSnapshotLogs(modelBuilder);
        ConfigureDailyCellStates(modelBuilder);
        ConfigureCellOperationalStates(modelBuilder);
        ConfigureAreaOperationalStates(modelBuilder);
        ConfigureAlertStates(modelBuilder);
    }

    private static void ConfigureConfigurationVersions(ModelBuilder modelBuilder)
    {
        var builder = modelBuilder.Entity<ConfigurationVersionRecord>();

        builder.ToTable("configuration_versions", PostgresSchemaNames.Control);
        builder.HasKey(entity => entity.Id);
        builder.Property(entity => entity.VersionNumber).IsRequired();
        builder.Property(entity => entity.CreatedAt).IsRequired();
        builder.Property(entity => entity.Description).HasMaxLength(500);
        builder.Property(entity => entity.CreatedBy).HasMaxLength(200);
        builder.HasIndex(entity => entity.VersionNumber).IsUnique();
    }

    private static void ConfigureAreas(ModelBuilder modelBuilder)
    {
        var builder = modelBuilder.Entity<AreaRecord>();

        builder.ToTable("areas", PostgresSchemaNames.Control);
        builder.HasKey(entity => entity.Id);
        builder.Property(entity => entity.Code).HasMaxLength(100).IsRequired();
        builder.Property(entity => entity.Name).HasMaxLength(200).IsRequired();
        builder.Property(entity => entity.CountryCode).HasMaxLength(10);
        builder.HasIndex(entity => new { entity.ConfigurationVersionId, entity.Code }).IsUnique();
        builder.HasOne(entity => entity.ConfigurationVersion)
            .WithMany(parent => parent.Areas)
            .HasForeignKey(entity => entity.ConfigurationVersionId)
            .OnDelete(DeleteBehavior.Restrict);
    }

    private static void ConfigureAreaContexts(ModelBuilder modelBuilder)
    {
        var builder = modelBuilder.Entity<AreaContextRecord>();

        builder.ToTable("area_contexts", PostgresSchemaNames.Control);
        builder.HasKey(entity => entity.Id);
        builder.Property(entity => entity.VegetationType).HasMaxLength(200).IsRequired();
        builder.Property(entity => entity.Seasonality).HasMaxLength(200).IsRequired();
        builder.HasIndex(entity => entity.AreaId).IsUnique();
        builder.HasOne(entity => entity.Area)
            .WithOne(parent => parent.Context)
            .HasForeignKey<AreaContextRecord>(entity => entity.AreaId)
            .OnDelete(DeleteBehavior.Cascade);
    }

    private static void ConfigureGridCells(ModelBuilder modelBuilder)
    {
        var builder = modelBuilder.Entity<GridCellRecord>();

        builder.ToTable("grid_cells", PostgresSchemaNames.Control);
        builder.HasKey(entity => entity.Id);
        builder.Property(entity => entity.CellCode).HasMaxLength(100).IsRequired();
        builder.Property(entity => entity.LandCoverClass).HasMaxLength(200);
        builder.Property(entity => entity.DominantForestType).HasMaxLength(200);
        builder.Property(entity => entity.DominantFuelModel).HasMaxLength(200);
        builder.Property(entity => entity.StructuralHazard).HasMaxLength(100);
        builder.Property(entity => entity.ConjuncturalHazard).HasMaxLength(100);
        builder.HasIndex(entity => new { entity.AreaId, entity.CellCode }).IsUnique();
        builder.HasOne(entity => entity.Area)
            .WithMany(parent => parent.GridCells)
            .HasForeignKey(entity => entity.AreaId)
            .OnDelete(DeleteBehavior.Cascade);
        builder.HasOne(entity => entity.ConfigurationVersion)
            .WithMany(parent => parent.GridCells)
            .HasForeignKey(entity => entity.ConfigurationVersionId)
            .OnDelete(DeleteBehavior.Restrict);
    }

    private static void ConfigureSensorProfiles(ModelBuilder modelBuilder)
    {
        var builder = modelBuilder.Entity<SensorProfileRecord>();

        builder.ToTable("sensor_profiles", PostgresSchemaNames.Control);
        builder.HasKey(entity => entity.Id);
        builder.Property(entity => entity.Name).HasMaxLength(200).IsRequired();
        builder.Property(entity => entity.SensorFamily).HasMaxLength(100);
        builder.HasIndex(entity => new { entity.ConfigurationVersionId, entity.Name }).IsUnique();
        builder.HasOne(entity => entity.ConfigurationVersion)
            .WithMany(parent => parent.SensorProfiles)
            .HasForeignKey(entity => entity.ConfigurationVersionId)
            .OnDelete(DeleteBehavior.Restrict);
    }

    private static void ConfigureSensorNetworks(ModelBuilder modelBuilder)
    {
        var builder = modelBuilder.Entity<SensorNetworkRecord>();

        builder.ToTable("sensor_networks", PostgresSchemaNames.Control);
        builder.HasKey(entity => entity.Id);
        builder.Property(entity => entity.Name).HasMaxLength(200).IsRequired();
        builder.HasIndex(entity => new { entity.ConfigurationVersionId, entity.Name }).IsUnique();
        builder.HasOne(entity => entity.ConfigurationVersion)
            .WithMany(parent => parent.SensorNetworks)
            .HasForeignKey(entity => entity.ConfigurationVersionId)
            .OnDelete(DeleteBehavior.Restrict);
    }

    private static void ConfigureSensorNodes(ModelBuilder modelBuilder)
    {
        var builder = modelBuilder.Entity<SensorNodeRecord>();

        builder.ToTable("sensor_nodes", PostgresSchemaNames.Control);
        builder.HasKey(entity => entity.Id);
        builder.Property(entity => entity.Name).HasMaxLength(200).IsRequired();
        builder.Property(entity => entity.InstallationProfile).HasMaxLength(100);
        builder.HasIndex(entity => new { entity.AreaId, entity.GridCellId, entity.Name }).IsUnique();
        builder.HasOne(entity => entity.Area)
            .WithMany(parent => parent.SensorNodes)
            .HasForeignKey(entity => entity.AreaId)
            .OnDelete(DeleteBehavior.Cascade);
        builder.HasOne(entity => entity.GridCell)
            .WithMany(parent => parent.SensorNodes)
            .HasForeignKey(entity => entity.GridCellId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(entity => entity.Profile)
            .WithMany(parent => parent.SensorNodes)
            .HasForeignKey(entity => entity.ProfileId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(entity => entity.ConfigurationVersion)
            .WithMany(parent => parent.SensorNodes)
            .HasForeignKey(entity => entity.ConfigurationVersionId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(entity => entity.Network)
            .WithMany(parent => parent.SensorNodes)
            .HasForeignKey(entity => entity.NetworkId)
            .OnDelete(DeleteBehavior.SetNull);
    }

    private static void ConfigureScenarioDefinitions(ModelBuilder modelBuilder)
    {
        var builder = modelBuilder.Entity<ScenarioDefinitionRecord>();

        builder.ToTable("scenario_definitions", PostgresSchemaNames.Control);
        builder.HasKey(entity => entity.Id);
        builder.Property(entity => entity.Code).HasMaxLength(100).IsRequired();
        builder.Property(entity => entity.Name).HasMaxLength(200).IsRequired();
        builder.Property(entity => entity.Description).HasMaxLength(1000);
        builder.Property(entity => entity.ParametersJson).IsRequired();
        builder.HasIndex(entity => new { entity.AreaId, entity.Code }).IsUnique();
        builder.HasOne(entity => entity.Area)
            .WithMany(parent => parent.Scenarios)
            .HasForeignKey(entity => entity.AreaId)
            .OnDelete(DeleteBehavior.Cascade);
        builder.HasOne(entity => entity.ConfigurationVersion)
            .WithMany(parent => parent.Scenarios)
            .HasForeignKey(entity => entity.ConfigurationVersionId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(entity => entity.BaseScenario)
            .WithMany(parent => parent.DerivedScenarios)
            .HasForeignKey(entity => entity.BaseScenarioId)
            .OnDelete(DeleteBehavior.Restrict);
    }

    private static void ConfigureRuleSetVersions(ModelBuilder modelBuilder)
    {
        var builder = modelBuilder.Entity<RuleSetVersionRecord>();

        builder.ToTable("rule_set_versions", PostgresSchemaNames.Control);
        builder.HasKey(entity => entity.Id);
        builder.Property(entity => entity.Name).HasMaxLength(200).IsRequired();
        builder.Property(entity => entity.Version).HasMaxLength(100).IsRequired();
        builder.Property(entity => entity.Description).HasMaxLength(1000);
        builder.Property(entity => entity.ParametersJson).IsRequired();
        builder.HasIndex(entity => new { entity.ConfigurationVersionId, entity.Name, entity.Version }).IsUnique();
        builder.HasOne(entity => entity.ConfigurationVersion)
            .WithMany(parent => parent.RuleSetVersions)
            .HasForeignKey(entity => entity.ConfigurationVersionId)
            .OnDelete(DeleteBehavior.Restrict);
    }

    private static void ConfigureSimulationRuns(ModelBuilder modelBuilder)
    {
        var builder = modelBuilder.Entity<SimulationRunRecord>();

        builder.ToTable("simulation_runs", PostgresSchemaNames.Control);
        builder.HasKey(entity => entity.Id);
        builder.Property(entity => entity.ScenarioCode).HasMaxLength(100).IsRequired();
        builder.Property(entity => entity.ScenarioName).HasMaxLength(200).IsRequired();
        builder.Property(entity => entity.CreatedAt).IsRequired();
        builder.Property(entity => entity.LogicalStartTimestamp).IsRequired();
        builder.Property(entity => entity.IntervalSeconds).IsRequired();
        builder.Property(entity => entity.NumberOfCycles).IsRequired();
        builder.Property(entity => entity.MetadataJson);
        builder.HasIndex(entity => new { entity.AreaId, entity.CreatedAt });
        builder.HasIndex(entity => new { entity.ScenarioId, entity.CreatedAt });
        builder.HasOne(entity => entity.Area)
            .WithMany(parent => parent.SimulationRuns)
            .HasForeignKey(entity => entity.AreaId)
            .OnDelete(DeleteBehavior.Cascade);
        builder.HasOne(entity => entity.Scenario)
            .WithMany(parent => parent.SimulationRuns)
            .HasForeignKey(entity => entity.ScenarioId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(entity => entity.ConfigurationVersion)
            .WithMany(parent => parent.SimulationRuns)
            .HasForeignKey(entity => entity.ConfigurationVersionId)
            .OnDelete(DeleteBehavior.Restrict);
    }

    private static void ConfigureDatasetArtifacts(ModelBuilder modelBuilder)
    {
        var builder = modelBuilder.Entity<DatasetArtifactRecord>();

        builder.ToTable("dataset_artifacts", PostgresSchemaNames.Control);
        builder.HasKey(entity => entity.Id);
        builder.Property(entity => entity.DatasetCode).HasMaxLength(150).IsRequired();
        builder.Property(entity => entity.DatasetType).HasMaxLength(100).IsRequired();
        builder.Property(entity => entity.SourceName).HasMaxLength(200).IsRequired();
        builder.Property(entity => entity.SourceUrl).HasMaxLength(1000);
        builder.Property(entity => entity.AreaCode).HasMaxLength(100).IsRequired();
        builder.Property(entity => entity.Version).HasMaxLength(100).IsRequired();
        builder.Property(entity => entity.Format).HasMaxLength(50).IsRequired();
        builder.Property(entity => entity.RelativePath).HasMaxLength(1000).IsRequired();
        builder.Property(entity => entity.Checksum).HasMaxLength(200);
        builder.HasIndex(entity => new { entity.DatasetCode, entity.AreaCode, entity.Version }).IsUnique();
    }

    private static void ConfigureScenarioDatasetBindings(ModelBuilder modelBuilder)
    {
        var builder = modelBuilder.Entity<ScenarioDatasetBindingRecord>();

        builder.ToTable("scenario_dataset_bindings", PostgresSchemaNames.Control);
        builder.HasKey(entity => entity.Id);
        builder.Property(entity => entity.BindingRole).HasMaxLength(100).IsRequired();
        builder.Property(entity => entity.Notes).HasMaxLength(1000);
        builder.HasIndex(entity => new { entity.ScenarioId, entity.DatasetArtifactId, entity.BindingRole }).IsUnique();
        builder.HasOne(entity => entity.Scenario)
            .WithMany(parent => parent.DatasetBindings)
            .HasForeignKey(entity => entity.ScenarioId)
            .OnDelete(DeleteBehavior.Cascade);
        builder.HasOne(entity => entity.DatasetArtifact)
            .WithMany(parent => parent.ScenarioBindings)
            .HasForeignKey(entity => entity.DatasetArtifactId)
            .OnDelete(DeleteBehavior.Cascade);
    }

    private static void ConfigureInboxEvents(ModelBuilder modelBuilder)
    {
        var builder = modelBuilder.Entity<InboxEventRecord>();

        builder.ToTable("event_inbox", PostgresSchemaNames.Pipeline);
        builder.HasKey(entity => entity.Id);
        builder.Property(entity => entity.SchemaVersion).HasMaxLength(50).IsRequired();
        builder.Property(entity => entity.CorrelationId).HasMaxLength(300).IsRequired();
        builder.Property(entity => entity.Producer).HasMaxLength(200).IsRequired();
        builder.Property(entity => entity.EventType).HasMaxLength(200).IsRequired();
        builder.Property(entity => entity.PayloadJson).IsRequired();
        builder.Property(entity => entity.EnvelopeJson).IsRequired();
        builder.Property(entity => entity.LastErrorCode).HasMaxLength(100);
        builder.Property(entity => entity.LastErrorMessage).HasMaxLength(2000);
        builder.HasIndex(entity => entity.EventId).IsUnique();
        builder.HasIndex(entity => new { entity.Status, entity.ReceivedAt });
        builder.HasIndex(entity => new { entity.Status, entity.NextAttemptNotBefore });
    }

    private static void ConfigureProcessingAttempts(ModelBuilder modelBuilder)
    {
        var builder = modelBuilder.Entity<ProcessingAttemptRecord>();

        builder.ToTable("processing_attempts", PostgresSchemaNames.Pipeline);
        builder.HasKey(entity => entity.Id);
        builder.Property(entity => entity.Stage).HasMaxLength(100).IsRequired();
        builder.Property(entity => entity.ErrorCode).HasMaxLength(100);
        builder.Property(entity => entity.ErrorMessage).HasMaxLength(2000);
        builder.HasIndex(entity => new { entity.InboxEventId, entity.AttemptNumber }).IsUnique();
        builder.HasOne(entity => entity.InboxEvent)
            .WithMany(parent => parent.Attempts)
            .HasForeignKey(entity => entity.InboxEventId)
            .OnDelete(DeleteBehavior.Cascade);
    }

    private static void ConfigureRejectedEvents(ModelBuilder modelBuilder)
    {
        var builder = modelBuilder.Entity<RejectedEventRecord>();

        builder.ToTable("rejected_events", PostgresSchemaNames.Pipeline);
        builder.HasKey(entity => entity.Id);
        builder.Property(entity => entity.RejectionCode).HasMaxLength(100).IsRequired();
        builder.Property(entity => entity.RejectionReason).HasMaxLength(2000).IsRequired();
        builder.Property(entity => entity.RawBodyUtf8).IsRequired();
        builder.Property(entity => entity.MetadataJson);
        builder.HasIndex(entity => entity.RejectedAt);
        builder.HasOne(entity => entity.InboxEvent)
            .WithMany(parent => parent.Rejections)
            .HasForeignKey(entity => entity.InboxEventId)
            .OnDelete(DeleteBehavior.SetNull);
    }

    private static void ConfigureQuarantinedEvents(ModelBuilder modelBuilder)
    {
        var builder = modelBuilder.Entity<QuarantinedEventRecord>();

        builder.ToTable("quarantined_events", PostgresSchemaNames.Pipeline);
        builder.HasKey(entity => entity.Id);
        builder.Property(entity => entity.QuarantineCode).HasMaxLength(100).IsRequired();
        builder.Property(entity => entity.QuarantineReason).HasMaxLength(2000).IsRequired();
        builder.Property(entity => entity.MetadataJson);
        builder.HasIndex(entity => entity.QuarantinedAt);
        builder.HasIndex(entity => entity.EventId);
        builder.HasOne(entity => entity.InboxEvent)
            .WithMany(parent => parent.Quarantines)
            .HasForeignKey(entity => entity.InboxEventId)
            .OnDelete(DeleteBehavior.Cascade);
    }

    private static void ConfigureAcceptedReadingLogs(ModelBuilder modelBuilder)
    {
        var builder = modelBuilder.Entity<AcceptedReadingLogRecord>();

        builder.ToTable("accepted_reading_log", PostgresSchemaNames.Projection);
        builder.HasKey(entity => entity.Id);
        builder.Property(entity => entity.MetricType).HasMaxLength(100).IsRequired();
        builder.Property(entity => entity.MeasurementUnit).HasMaxLength(100).IsRequired();
        builder.Property(entity => entity.OperationalState).HasMaxLength(100).IsRequired();
        builder.Property(entity => entity.Producer).HasMaxLength(200).IsRequired();
        builder.Property(entity => entity.CorrelationId).HasMaxLength(300).IsRequired();
        builder.Property(entity => entity.PayloadJson).IsRequired();
        builder.Property(entity => entity.EnvelopeJson).IsRequired();
        builder.HasIndex(entity => entity.EventId).IsUnique();
        builder.HasIndex(entity => new { entity.AreaId, entity.EventTime });
        builder.HasIndex(entity => new { entity.SensorId, entity.EventTime });
        builder.HasOne(entity => entity.Area)
            .WithMany()
            .HasForeignKey(entity => entity.AreaId)
            .OnDelete(DeleteBehavior.Cascade);
        builder.HasOne(entity => entity.SensorNode)
            .WithMany()
            .HasForeignKey(entity => entity.SensorId)
            .OnDelete(DeleteBehavior.Restrict);
    }

    private static void ConfigureRiskAssessmentLogs(ModelBuilder modelBuilder)
    {
        var builder = modelBuilder.Entity<RiskAssessmentLogRecord>();

        builder.ToTable("risk_assessment_log", PostgresSchemaNames.Projection);
        builder.HasKey(entity => entity.Id);
        builder.Property(entity => entity.RiskLevel).HasMaxLength(50).IsRequired();
        builder.Property(entity => entity.ExplanationSummary).HasMaxLength(2000);
        builder.HasIndex(entity => entity.SourceEventId).IsUnique();
        builder.HasIndex(entity => entity.SimulationRunId);
        builder.HasIndex(entity => new { entity.AreaId, entity.Timestamp });
        builder.HasIndex(entity => new { entity.AreaId, entity.SimulationRunId, entity.Timestamp });
        builder.HasIndex(entity => new { entity.GridCellId, entity.Timestamp });
        builder.HasOne(entity => entity.Area)
            .WithMany()
            .HasForeignKey(entity => entity.AreaId)
            .OnDelete(DeleteBehavior.Cascade);
        builder.HasOne(entity => entity.SimulationRun)
            .WithMany()
            .HasForeignKey(entity => entity.SimulationRunId)
            .OnDelete(DeleteBehavior.SetNull);
        builder.HasOne(entity => entity.SensorNode)
            .WithMany()
            .HasForeignKey(entity => entity.SensorId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(entity => entity.GridCell)
            .WithMany()
            .HasForeignKey(entity => entity.GridCellId)
            .OnDelete(DeleteBehavior.SetNull);
    }

    private static void ConfigureAreaRiskSnapshotLogs(ModelBuilder modelBuilder)
    {
        var builder = modelBuilder.Entity<AreaRiskSnapshotLogRecord>();

        builder.ToTable("area_risk_snapshot_log", PostgresSchemaNames.Projection);
        builder.HasKey(entity => entity.Id);
        builder.Property(entity => entity.AggregateRiskLevel).HasMaxLength(50).IsRequired();
        builder.Property(entity => entity.Summary).HasMaxLength(2000);
        builder.HasIndex(entity => new { entity.AreaId, entity.SnapshotTimestamp });
        builder.HasOne(entity => entity.Area)
            .WithMany()
            .HasForeignKey(entity => entity.AreaId)
            .OnDelete(DeleteBehavior.Cascade);
        builder.HasOne(entity => entity.SimulationRun)
            .WithMany()
            .HasForeignKey(entity => entity.SimulationRunId)
            .OnDelete(DeleteBehavior.SetNull);
    }

    private static void ConfigureDailyCellStates(ModelBuilder modelBuilder)
    {
        var builder = modelBuilder.Entity<DailyCellStateRecord>();

        builder.ToTable("daily_cell_state", PostgresSchemaNames.Projection);
        builder.HasKey(entity => entity.Id);
        builder.Property(entity => entity.AntecedentState).HasMaxLength(100).IsRequired();
        builder.Property(entity => entity.DroughtContext).HasMaxLength(100).IsRequired();
        builder.Property(entity => entity.FireIndexProvenance).HasMaxLength(100).IsRequired();
        builder.Property(entity => entity.CandidateParameterSetVersion).HasMaxLength(100).IsRequired();
        builder.Property(entity => entity.Provenance).HasMaxLength(200).IsRequired();
        builder.HasIndex(entity => new { entity.AreaId, entity.GridCellId, entity.LogicalDate, entity.SimulationRunId }).IsUnique();
        builder.HasIndex(entity => new { entity.SensorId, entity.LogicalDate });
        builder.HasOne(entity => entity.Area)
            .WithMany()
            .HasForeignKey(entity => entity.AreaId)
            .OnDelete(DeleteBehavior.Cascade);
        builder.HasOne(entity => entity.GridCell)
            .WithMany()
            .HasForeignKey(entity => entity.GridCellId)
            .OnDelete(DeleteBehavior.Cascade);
        builder.HasOne(entity => entity.SensorNode)
            .WithMany()
            .HasForeignKey(entity => entity.SensorId)
            .OnDelete(DeleteBehavior.SetNull);
        builder.HasOne(entity => entity.SimulationRun)
            .WithMany()
            .HasForeignKey(entity => entity.SimulationRunId)
            .OnDelete(DeleteBehavior.SetNull);
        builder.HasOne(entity => entity.ConfigurationVersion)
            .WithMany()
            .HasForeignKey(entity => entity.ConfigurationVersionId)
            .OnDelete(DeleteBehavior.SetNull);
    }

    private static void ConfigureCellOperationalStates(ModelBuilder modelBuilder)
    {
        var builder = modelBuilder.Entity<CellOperationalStateRecord>();

        builder.ToTable("cell_operational_state", PostgresSchemaNames.Projection);
        builder.HasKey(entity => entity.Id);
        builder.Property(entity => entity.RiskLevel).HasMaxLength(50).IsRequired();
        builder.Property(entity => entity.Severity).HasMaxLength(50).IsRequired();
        builder.Property(entity => entity.Summary).HasMaxLength(2000);
        builder.HasIndex(entity => entity.GridCellId).IsUnique();
        builder.HasIndex(entity => new { entity.AreaId, entity.SnapshotTimestamp });
        builder.HasOne(entity => entity.Area)
            .WithMany()
            .HasForeignKey(entity => entity.AreaId)
            .OnDelete(DeleteBehavior.Cascade);
        builder.HasOne(entity => entity.GridCell)
            .WithMany()
            .HasForeignKey(entity => entity.GridCellId)
            .OnDelete(DeleteBehavior.Cascade);
        builder.HasOne(entity => entity.SensorNode)
            .WithMany()
            .HasForeignKey(entity => entity.SensorId)
            .OnDelete(DeleteBehavior.SetNull);
    }

    private static void ConfigureAreaOperationalStates(ModelBuilder modelBuilder)
    {
        var builder = modelBuilder.Entity<AreaOperationalStateRecord>();

        builder.ToTable("area_operational_state", PostgresSchemaNames.Projection);
        builder.HasKey(entity => entity.Id);
        builder.Property(entity => entity.AggregateRiskLevel).HasMaxLength(50).IsRequired();
        builder.Property(entity => entity.Severity).HasMaxLength(50).IsRequired();
        builder.Property(entity => entity.PendingAlertState).HasMaxLength(50).HasDefaultValue("None").IsRequired();
        builder.Property(entity => entity.Summary).HasMaxLength(2000);
        builder.HasIndex(entity => entity.AreaId).IsUnique();
        builder.HasIndex(entity => entity.SnapshotTimestamp);
        builder.HasOne(entity => entity.Area)
            .WithMany()
            .HasForeignKey(entity => entity.AreaId)
            .OnDelete(DeleteBehavior.Cascade);
        builder.HasOne(entity => entity.ConfigurationVersion)
            .WithMany()
            .HasForeignKey(entity => entity.ConfigurationVersionId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(entity => entity.SimulationRun)
            .WithMany()
            .HasForeignKey(entity => entity.SimulationRunId)
            .OnDelete(DeleteBehavior.SetNull);
    }

    private static void ConfigureAlertStates(ModelBuilder modelBuilder)
    {
        var builder = modelBuilder.Entity<AlertStateRecord>();

        builder.ToTable("alert_state", PostgresSchemaNames.Projection);
        builder.HasKey(entity => entity.Id);
        builder.Property(entity => entity.AlertCode).HasMaxLength(100).IsRequired();
        builder.Property(entity => entity.Severity).HasMaxLength(50).IsRequired();
        builder.Property(entity => entity.Status).HasMaxLength(50).IsRequired();
        builder.Property(entity => entity.Message).HasMaxLength(2000).IsRequired();
        builder.HasIndex(entity => new { entity.AreaId, entity.AlertCode, entity.Status });
        builder.HasIndex(entity => entity.UpdatedAt);
        builder.HasOne(entity => entity.Area)
            .WithMany()
            .HasForeignKey(entity => entity.AreaId)
            .OnDelete(DeleteBehavior.Cascade);
        builder.HasOne(entity => entity.ConfigurationVersion)
            .WithMany()
            .HasForeignKey(entity => entity.ConfigurationVersionId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(entity => entity.AreaOperationalState)
            .WithMany(parent => parent.Alerts)
            .HasForeignKey(entity => entity.AreaOperationalStateId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
