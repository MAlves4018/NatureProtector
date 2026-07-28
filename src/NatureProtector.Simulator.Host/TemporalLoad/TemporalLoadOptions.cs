namespace NatureProtector.Simulator.Host.TemporalLoad;

public sealed class TemporalLoadOptions
{
    public const string SectionName = "TemporalLoad";

    public bool Enabled { get; set; }

    public string? WorkloadPath { get; set; }

    public string? WorkloadId { get; set; }

    public string OutputRoot { get; set; } = "artifacts/scalability-temporal-comparison/raw";

    public string RunLabel { get; set; } = "temporal-load";

    public string Topology { get; set; } = "fixed-one";

    public int Repetition { get; set; } = 1;

    public int? Seed { get; set; }

    public int MaxCatchUpBurst { get; set; } = 5;

    public bool RequireNominalEvents { get; set; } = true;

    public int MaxNominalGenerationAttempts { get; set; } = 100;

    public int PublisherTimeoutSeconds { get; set; } = 900;
}

public sealed class TemporalWorkloadCatalog
{
    public int SchemaVersion { get; set; } = 1;

    public List<TemporalWorkloadDefinition> Workloads { get; set; } = [];
}

public sealed class TemporalWorkloadDefinition
{
    public string Id { get; set; } = string.Empty;

    public string Description { get; set; } = string.Empty;

    public int WarmUpSeconds { get; set; }

    public int CooldownSeconds { get; set; }

    public int DrainTimeoutSeconds { get; set; } = 120;

    public int? Seed { get; set; }

    public List<TemporalWorkloadSegment> Segments { get; set; } = [];
}

public sealed class TemporalWorkloadSegment
{
    public string Id { get; set; } = string.Empty;

    public string Kind { get; set; } = "constant";

    public double DurationSeconds { get; set; }

    public double? RequestedRate { get; set; }

    public double? StartRate { get; set; }

    public double? EndRate { get; set; }

    public int? BurstCount { get; set; }
}
