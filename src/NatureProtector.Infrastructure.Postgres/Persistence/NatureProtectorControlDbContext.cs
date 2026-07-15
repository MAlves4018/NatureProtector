using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using NatureProtector.Infrastructure.Postgres.Control;
using NatureProtector.Infrastructure.Postgres.Pipeline;
using NatureProtector.Infrastructure.Postgres.Projection;
using NatureProtector.Infrastructure.Postgres.Schemas;
using NatureProtector.Infrastructure.Postgres.Users;

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

    public DbSet<UserRecord> Users => Set<UserRecord>();

    public DbSet<RoleRecord> Roles => Set<RoleRecord>();
    public DbSet<UserRoleRecord> UserRoles => Set<UserRoleRecord>();
    public DbSet<AreaRecord> Areas => Set<AreaRecord>();
    public DbSet<AreaContextRecord> AreaContexts => Set<AreaContextRecord>();
    public DbSet<GridCellRecord> GridCells => Set<GridCellRecord>();
    public DbSet<SensorProfileRecord> SensorProfiles => Set<SensorProfileRecord>();
    public DbSet<SensorNetworkRecord> SensorNetworks => Set<SensorNetworkRecord>();
    public DbSet<SensorNodeRecord> SensorNodes => Set<SensorNodeRecord>();
    public DbSet<ScenarioDefinitionRecord> ScenarioDefinitions => Set<ScenarioDefinitionRecord>();
    public DbSet<SimulationRunRecord> SimulationRuns => Set<SimulationRunRecord>();
    public DbSet<RuntimeOperationRecord> RuntimeOperations => Set<RuntimeOperationRecord>();
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
    public DbSet<CycleSettlementRecord> CycleSettlements => Set<CycleSettlementRecord>();
    public DbSet<CycleObservationRecord> CycleObservations => Set<CycleObservationRecord>();
    public DbSet<CellCycleSnapshotRecord> CellCycleSnapshots => Set<CellCycleSnapshotRecord>();
    public DbSet<AreaCycleSnapshotRecord> AreaCycleSnapshots => Set<AreaCycleSnapshotRecord>();

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
        ConfigureUsers(modelBuilder);
        ConfigureRoles(modelBuilder);
        ConfigureUserRoles(modelBuilder);
        ConfigureAreas(modelBuilder);
        ConfigureAreaContexts(modelBuilder);
        ConfigureGridCells(modelBuilder);
        ConfigureSensorProfiles(modelBuilder);
        ConfigureSensorNetworks(modelBuilder);
        ConfigureSensorNodes(modelBuilder);
        ConfigureScenarioDefinitions(modelBuilder);
        ConfigureSimulationRuns(modelBuilder);
        ConfigureRuntimeOperations(modelBuilder);
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
        ConfigureCycleSettlements(modelBuilder);
        ConfigureCycleObservations(modelBuilder);
        ConfigureCellCycleSnapshots(modelBuilder);
        ConfigureAreaCycleSnapshots(modelBuilder);
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

    private static void ConfigureUsers(ModelBuilder modelBuilder)
    {
        var builder = modelBuilder.Entity<UserRecord>();

        builder.ToTable("users", PostgresSchemaNames.UserBase);
        builder.HasKey(entity => entity.Id);
        builder.Property(entity => entity.Username).HasMaxLength(100).IsRequired();
        builder.Property(entity => entity.Email).HasMaxLength(200).IsRequired();
        builder.Property(entity => entity.PasswordHash).HasMaxLength(500).IsRequired();
        builder.HasIndex(entity => entity.Email).IsUnique();
    }

    private static void ConfigureRoles(ModelBuilder modelBuilder)
    {
        var builder = modelBuilder.Entity<RoleRecord>();

        builder.ToTable("roles", PostgresSchemaNames.UserBase);
        builder.HasKey(entity => entity.Id);
        builder.Property(entity => entity.Name).HasMaxLength(100).IsRequired();
        builder.HasIndex(entity => entity.Name).IsUnique();
        builder.HasData(
            new RoleRecord { Id = RoleRecord.AdminId, Name = RoleRecord.Admin },
            new RoleRecord { Id = RoleRecord.SimId, Name = RoleRecord.Sim },
            new RoleRecord { Id = RoleRecord.PipelineId, Name = RoleRecord.Pipeline }
        );
    }

    private static void ConfigureUserRoles(ModelBuilder modelBuilder)
    {
        var builder = modelBuilder.Entity<UserRoleRecord>();

        builder.ToTable("user_roles", PostgresSchemaNames.UserBase);
        builder.HasKey(entity => new { entity.UserId, entity.RoleId });
        builder.HasOne<UserRecord>().WithMany().HasForeignKey(entity => entity.UserId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne<RoleRecord>().WithMany().HasForeignKey(entity => entity.RoleId).OnDelete(DeleteBehavior.Cascade);
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
        builder.Property(entity => entity.OrchestratorCorrelationId).HasColumnName("orchestrator_correlation_id").HasMaxLength(250);
        builder.HasIndex(entity => entity.OrchestratorCorrelationId).IsUnique();
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

    private static void ConfigureRuntimeOperations(ModelBuilder modelBuilder)
    {
        var builder = modelBuilder.Entity<RuntimeOperationRecord>();
        builder.ToTable("runtime_orchestrator_executions", PostgresSchemaNames.Control);
        builder.HasKey(entity => entity.OperationId);
        builder.Property(entity => entity.OperationId).HasColumnName("execution_id");
        builder.Property(entity => entity.RequestId).HasColumnName("request_id");
        builder.Property(entity => entity.IdempotencyKey).HasColumnName("idempotency_key").HasMaxLength(250).IsRequired();
        builder.Property(entity => entity.Provider).HasColumnName("provider").HasMaxLength(50).IsRequired();
        builder.Property(entity => entity.ProviderOperationName).HasColumnName("provider_operation_name").HasMaxLength(500);
        builder.Property(entity => entity.ProviderExecutionName).HasColumnName("provider_execution_name").HasMaxLength(500);
        builder.Property(entity => entity.SimulationRunId).HasColumnName("simulation_run_id");
        builder.Property(entity => entity.CorrelationId).HasColumnName("log_correlation").HasMaxLength(250).IsRequired();
        builder.Property(entity => entity.RequestedState).HasColumnName("requested_state").HasMaxLength(50).IsRequired();
        builder.Property(entity => entity.ProviderState).HasColumnName("provider_state").HasMaxLength(50).IsRequired();
        builder.Property(entity => entity.RunState).HasColumnName("run_state").HasMaxLength(50).IsRequired();
        builder.Property(entity => entity.ProcessingState).HasColumnName("processing_state").HasMaxLength(50).IsRequired();
        builder.Property(entity => entity.State).HasColumnName("state").HasMaxLength(50).IsRequired();
        builder.Property(entity => entity.TerminalOutcome).HasColumnName("terminal_outcome").HasMaxLength(50);
        builder.Property(entity => entity.IsOperational).HasColumnName("is_operational");
        builder.Property(entity => entity.AcceptedAt).HasColumnName("accepted_at");
        builder.Property(entity => entity.UpdatedAt).HasColumnName("updated_at");
        builder.Property(entity => entity.DeadlineAt).HasColumnName("deadline_at");
        builder.Property(entity => entity.StartedAt).HasColumnName("started_at");
        builder.Property(entity => entity.ProducerCompletedAt).HasColumnName("producer_completed_at");
        builder.Property(entity => entity.SystemCompletedAt).HasColumnName("system_completed_at");
        builder.Property(entity => entity.FinishedAt).HasColumnName("finished_at");
        builder.Property(entity => entity.FailureCode).HasColumnName("failure_code").HasMaxLength(150);
        builder.Property(entity => entity.FailureMessage).HasColumnName("failure_message").HasMaxLength(4000);
        builder.Property(entity => entity.EvidenceId).HasColumnName("evidence_id").HasMaxLength(250);
        builder.Property(entity => entity.EvidenceLocation).HasColumnName("evidence_location").HasMaxLength(1000);
        builder.Property(entity => entity.LaunchLeaseToken).HasColumnName("launch_lease_token");
        builder.Property(entity => entity.LaunchLeaseUntil).HasColumnName("launch_lease_until");
        builder.HasIndex(entity => entity.RequestId).IsUnique();
        builder.HasIndex(entity => entity.IdempotencyKey).IsUnique();
        builder.HasIndex(entity => entity.CorrelationId).IsUnique();
        builder.HasIndex(entity => entity.SimulationRunId).IsUnique();
        builder.HasIndex(entity => entity.IsOperational)
            .HasFilter("is_operational = TRUE AND terminal_outcome IS NULL")
            .IsUnique();
        builder.HasOne(entity => entity.SimulationRun)
            .WithOne(entity => entity.RuntimeOperation)
            .HasForeignKey<RuntimeOperationRecord>(entity => entity.SimulationRunId)
            .OnDelete(DeleteBehavior.SetNull);
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
        builder.HasIndex(entity => new { entity.SimulationRunId, entity.Status });
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
        builder.Property(entity => entity.DominantDriver).HasMaxLength(50).HasDefaultValue("Mixed").IsRequired();
        builder.Property(entity => entity.ParameterSetVersion).HasMaxLength(100).HasDefaultValue("Unknown").IsRequired();
        builder.Property(entity => entity.CalculationStatus).HasMaxLength(50).HasDefaultValue("CandidateFallback").IsRequired();
        builder.Property(entity => entity.ConfidenceFactor).HasDefaultValue(1.0);
        builder.Property(entity => entity.IntegrityFactor).HasDefaultValue(1.0);
        builder.Property(entity => entity.Limitations).HasMaxLength(1000);
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
        builder.Property(entity => entity.AntecedentState).HasColumnType("text").IsRequired();
        builder.Property(entity => entity.DroughtContext).HasColumnType("text").IsRequired();
        builder.Property(entity => entity.KbdiCalculationStatus).HasMaxLength(50).HasDefaultValue("Missing").IsRequired();
        builder.Property(entity => entity.KbdiLimitations).HasColumnType("text");
        builder.Property(entity => entity.FireIndexProvenance).HasColumnType("text").IsRequired();
        builder.Property(entity => entity.FireWeatherCalculationStatus).HasMaxLength(50).HasDefaultValue("Missing").IsRequired();
        builder.Property(entity => entity.FireWeatherLimitations).HasColumnType("text");
        builder.Property(entity => entity.CandidateParameterSetVersion).HasMaxLength(100).IsRequired();
        builder.Property(entity => entity.Provenance).HasColumnType("text").IsRequired();
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
        builder.Property(entity => entity.CoverageStatus).HasMaxLength(50).HasDefaultValue("Complete").IsRequired();
        builder.Property(entity => entity.FreshnessStatus).HasMaxLength(50).HasDefaultValue("Fresh").IsRequired();
        builder.Property(entity => entity.CarryForwardStatus).HasMaxLength(50).HasDefaultValue("Current").IsRequired();
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
        builder.Property(entity => entity.CoverageStatus).HasMaxLength(50).HasDefaultValue("Complete").IsRequired();
        builder.Property(entity => entity.FreshnessStatus).HasMaxLength(50).HasDefaultValue("Fresh").IsRequired();
        builder.Property(entity => entity.CarryForwardStatus).HasMaxLength(50).HasDefaultValue("Current").IsRequired();
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

    private static void ConfigureCycleSettlements(ModelBuilder modelBuilder)
    {
        var builder = modelBuilder.Entity<CycleSettlementRecord>();
        builder.ToTable("cycle_settlement", PostgresSchemaNames.Projection);
        builder.HasKey(entity => entity.Id);
        builder.Property(entity => entity.Status).HasMaxLength(50).IsRequired();
        builder.Property(entity => entity.FinalizationReason).HasMaxLength(50);
        builder.HasIndex(entity => new { entity.SimulationRunId, entity.CycleIndex }).IsUnique();
        builder.HasIndex(entity => new { entity.AreaId, entity.Status });
    }

    private static void ConfigureCycleObservations(ModelBuilder modelBuilder)
    {
        var builder = modelBuilder.Entity<CycleObservationRecord>();
        builder.ToTable("cycle_observation", PostgresSchemaNames.Projection);
        builder.HasKey(entity => entity.Id);
        builder.Property(entity => entity.MetricOrigin).HasMaxLength(50).IsRequired();
        builder.Property(entity => entity.Outcome).HasMaxLength(50).IsRequired();
        builder.Property(entity => entity.RiskLevel).HasMaxLength(50);
        builder.HasIndex(entity => new { entity.SimulationRunId, entity.CycleIndex, entity.SensorId }).IsUnique();
        builder.HasIndex(entity => entity.EventId).IsUnique();
    }

    private static void ConfigureCellCycleSnapshots(ModelBuilder modelBuilder)
    {
        var builder = modelBuilder.Entity<CellCycleSnapshotRecord>();
        builder.ToTable("cell_cycle_snapshot", PostgresSchemaNames.Projection);
        builder.HasKey(entity => entity.Id);
        builder.Property(entity => entity.AggregateRiskLevel).HasMaxLength(50).IsRequired();
        builder.HasIndex(entity => new { entity.SimulationRunId, entity.CycleIndex, entity.GridCellId }).IsUnique();
    }

    private static void ConfigureAreaCycleSnapshots(ModelBuilder modelBuilder)
    {
        var builder = modelBuilder.Entity<AreaCycleSnapshotRecord>();
        builder.ToTable("area_cycle_snapshot", PostgresSchemaNames.Projection);
        builder.HasKey(entity => entity.Id);
        builder.Property(entity => entity.AggregateRiskLevel).HasMaxLength(50).IsRequired();
        builder.Property(entity => entity.AlertOutcome).HasMaxLength(50).IsRequired();
        builder.HasIndex(entity => new { entity.SimulationRunId, entity.CycleIndex, entity.AreaId }).IsUnique();
        builder.HasIndex(entity => new { entity.AreaId, entity.CycleIndex });
    }
}
