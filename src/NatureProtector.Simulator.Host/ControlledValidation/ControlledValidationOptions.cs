namespace NatureProtector.Simulator.Host.ControlledValidation;

public sealed class ControlledValidationOptions
{
    public const string SectionName = "ControlledValidation";

    public bool Enabled { get; set; }

    public string Phase { get; set; } = ControlledValidationPhases.P0;

    public Guid ControlledValidationRunId { get; set; }

    public string? RunLabel { get; set; }

    public string? ScenarioCode { get; set; }

    public Guid AreaId { get; set; }

    public Guid SimulationRunId { get; set; }

    public Guid NominalSensorId { get; set; }

    public string? NominalSensorName { get; set; }

    public Guid SensorNotFoundId { get; set; }

    public DateTimeOffset? EventTime { get; set; }

    public bool WriteEvidenceSidecar { get; set; } = true;

    public string EvidenceOutputRoot { get; set; } = "";
}
