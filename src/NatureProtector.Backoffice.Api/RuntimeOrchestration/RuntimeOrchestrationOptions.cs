namespace NatureProtector.Backoffice.Api.RuntimeOrchestration;

public sealed class RuntimeOrchestrationOptions
{
    public const string SectionName = "RuntimeOrchestration";

    public string Mode { get; set; } = RuntimeOrchestrationModes.Disabled;
    public bool AllowRemoteLaunch { get; set; }
    public string LaunchMode { get; set; } = RuntimeProcessLaunchModes.Project;
    public string EvidenceMode { get; set; } = RuntimeEvidenceModes.Disabled;
    public string ExecutablePath { get; set; } = "dotnet";
    public string SimulatorProjectPath { get; set; } = "src/NatureProtector.Simulator.Host";
    public string SimulatorAssemblyPath { get; set; } = "NatureProtector.Simulator.Host.dll";
    public string? WorkingDirectory { get; set; }
    public string EvidenceRoot { get; set; } = "artifacts/runtime-orchestration";
    public int MaximumTimeoutSeconds { get; set; } = 3600;
    public string CloudRunProjectId { get; set; } = string.Empty;
    public string CloudRunRegion { get; set; } = "europe-southwest1";
    public string CloudRunSimulatorJobName { get; set; } = "natureprotector-simulator";
    public string CloudRunSimulatorContainerName { get; set; } = "simulator";
    public int CloudRunLaunchLeaseSeconds { get; set; } = 120;
    public int CloudRunPollIntervalSeconds { get; set; } = 3;
}

public static class RuntimeOrchestrationModes
{
    public const string Disabled = "Disabled";
    public const string LocalProcess = "LocalProcess";
    public const string CloudRunJob = "CloudRunJob";
}

public static class RuntimeProcessLaunchModes
{
    public const string Project = "Project";
    public const string PublishedAssembly = "PublishedAssembly";
}

public static class RuntimeEvidenceModes
{
    public const string Disabled = "Disabled";
    public const string FileSystem = "FileSystem";
}
