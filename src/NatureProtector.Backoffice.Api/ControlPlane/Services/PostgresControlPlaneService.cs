using Microsoft.EntityFrameworkCore;
using NatureProtector.Backoffice.Api.ControlPlane.Contracts;
using NatureProtector.Backoffice.Api.RuntimeOrchestration;
using NatureProtector.Core.Scenarios;
using NatureProtector.Infrastructure.Postgres.Persistence;
using NatureProtector.Infrastructure.Postgres.Pipeline;
using NatureProtector.Infrastructure.Postgres.Projection;
using NatureProtector.Shared.Observability;
using System.Diagnostics;
using System.Diagnostics.Metrics;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace NatureProtector.Backoffice.Api.ControlPlane.Services;

/*
 * Este serviço projeta para a API os dados persistidos no control plane e nas
 * projeções operacionais.
 *
 * Rationale:
 * - Os controladores não devem conter queries nem detalhe relacional.
 * - A camada de serviço define a fronteira entre o esquema persistido e os
 *   contratos de resposta do backoffice.
 *
 * Design considerations:
 * - As queries usam projeções diretas para evitar transportar entidades
 *   completas para a API.
 * - Quando a versão de configuração não é indicada, usa-se a configuração ativa
 *   mais recente.
 * - A paginação aplica limites defensivos para proteger a API.
 */

public sealed partial class PostgresControlPlaneService : IControlPlaneService
{
    // <phase5-slice id="core-members">
    private const int DefaultTake = 100;
    private const int MaxTake = 500;
    private const int DefaultRecentMinutes = 30;
    private const int MinRecentMinutes = 1;
    private const int MaxRecentMinutes = 24 * 60;
    private const string ControlledValidationP3Phase = "P3NegativePipeline";
    private const string ControlledValidationP3AreaCode = "proenca-a-nova";
    private const string ControlledValidationP3ScenarioCode = "scenario_b";
    private const string ControlledValidationP3RunLabelPrefix = "controlled-validation-p3-negative-pipeline-";
    private const int ControlledValidationP3MessageCount = 11;
    private const int ControlledValidationP3ExecutableCases = 10;
    private const int ControlledValidationP3BlockedCases = 2;

    private readonly IDbContextFactory<NatureProtectorControlDbContext> _dbContextFactory;
    private readonly string _repositoryRoot;
    private readonly bool _enableRuntimeProcessLaunch;
    private readonly IRuntimeRunOrchestrator _runtimeRunOrchestrator;
    private readonly IRuntimeEvidenceSink _runtimeEvidenceSink;
    private readonly IRuntimeDataResetCoordinator _runtimeDataResetCoordinator;
    private readonly string _environmentName;
    private static readonly Regex ControlledValidationRunLabelRegex = new(
        "^[A-Za-z0-9][A-Za-z0-9._-]{0,119}$",
        RegexOptions.Compiled);

    private static readonly IReadOnlyList<RuntimeDiagnosticDefinitionResponse> RuntimeDiagnostics =
    [
        new("runtime-table-counts", "Runtime table counts", "Counts rows in runtime/control, pipeline and projection tables."),
        new("active-runs", "Active runs", "Lists simulation runs with no EndedAt timestamp."),
        new("latest-runs", "Latest runs", "Lists the latest 10 simulation runs."),
        new("inbox-by-status", "Inbox by status", "Groups pipeline.event_inbox by Status."),
        new("attempts-by-outcome", "Attempts by outcome", "Groups pipeline.processing_attempts by Outcome."),
        new("failed-attempts-by-error", "Failed attempts by error", "Groups recent failed/retry/quarantined attempts by outcome and error."),
        new("latest-rejected-events", "Latest rejected events", "Lists latest rejected events."),
        new("latest-quarantined-events", "Latest quarantined events", "Lists latest quarantined events."),
        new("latest-run-expected-vs-observed", "Latest run expected vs observed", "Compares latest run expected event count with accepted readings observed for that run."),
        new("latest-run-events-by-cycle", "Latest run events by cycle", "Groups latest run accepted readings by logical cycle."),
        new("latest-run-risk-by-metric", "Latest run risk by metric", "Groups latest run persisted risk assessments by metric type."),
        new("latest-run-np-vs-fwi-kbdi", "Latest run NP vs FWI/KBDI", "Shows persisted NatureProtector score components beside FWI/KBDI context for the latest run."),
        new("latest-run-np-vs-fwi", "Latest run NP vs FWI/KBDI (legacy id)", "Compatibility alias for latest-run-np-vs-fwi-kbdi."),
        new("latest-run-portuguese-context-proxy", "Latest run Portuguese context proxy", "Shows the candidate, non-official Portuguese context proxy derived from FWI IPMA class and territory."),
        new("latest-run-kbdi-series-context", "Latest run KBDI series context", "Shows KBDI antecedent/status signals and explicitly reports limited history."),
        new("latest-run-components", "Latest run score components", "Lists persisted M/D/T, H/F/G and C/I components for latest run assessments."),
        new("latest-run-quality-by-profile", "Latest run quality by profile", "Summarizes latest run degradation profiles, accepted readings and persisted eligibility/quality signals."),
        new("latest-run-degradation-effects", "Latest run degradation effects", "Summarizes observable effects for missing, noise and lag/delay profiles."),
        new("latest-run-cell-context", "Latest run cell context", "Shows DailyCellState and territorial context fields for latest run cells."),
        new("latest-run-fwi-input-completeness", "Latest run FWI input completeness", "Shows FWI/KBDI input/status completeness from persisted DailyCellState rows."),
        new("latest-run-kbdi-input-completeness", "Latest run KBDI input completeness", "Compatibility diagnostic focused on KBDI status from persisted DailyCellState rows."),
        new("latest-run-coverage-freshness", "Latest run coverage freshness", "Shows current coverage, freshness and carry-forward projection statuses."),
        new("area-operational-state", "Area operational state", "Shows the current persisted area operational projection."),
        new("cell-operational-states", "Cell operational states", "Shows recent persisted cell operational projections."),
        new("active-alerts", "Active alerts", "Lists open alerts."),
        new("recent-alert-transitions", "Recent alert transitions", "Lists recent alert rows including resolved alerts."),
        new("scenario-definition-details", "Scenario definition details", "Shows persisted scenario parameters and simulator options for the selected area."),
        new("compare-latest-b-vs-c", "Compare latest B vs C", "Compares latest scenario_b and scenario_c runs for the selected area using persisted runtime data.")
    ];

    public PostgresControlPlaneService(
        IDbContextFactory<NatureProtectorControlDbContext> dbContextFactory,
        string? contentRootPath = null,
        bool enableRuntimeProcessLaunch = false,
        IRuntimeRunOrchestrator? runtimeRunOrchestrator = null,
        IRuntimeEvidenceSink? runtimeEvidenceSink = null,
        IRuntimeDataResetCoordinator? runtimeDataResetCoordinator = null,
        string environmentName = "Development")
    {
        _dbContextFactory = dbContextFactory;
        _repositoryRoot = ResolveRepositoryRoot(contentRootPath ?? AppContext.BaseDirectory);
        _enableRuntimeProcessLaunch = enableRuntimeProcessLaunch;
        _runtimeRunOrchestrator = runtimeRunOrchestrator ?? DisabledRuntimeRunOrchestrator.Instance;
        _runtimeEvidenceSink = runtimeEvidenceSink ?? NullRuntimeEvidenceSink.Instance;
        _runtimeDataResetCoordinator = runtimeDataResetCoordinator ?? DatabaseOnlyRuntimeDataResetCoordinator.Instance;
        _environmentName = string.IsNullOrWhiteSpace(environmentName) ? "Production" : environmentName;
    }
    // </phase5-slice>

    // <phase5-slice id="availability">
    /// <summary>
    /// Indica que a implementação PostgreSQL do control plane está disponível.
    /// </summary>
    public bool IsAvailable => true;

    /// <summary>
    /// Mensagem curta de disponibilidade exposta pelos endpoints da API.
    /// </summary>
    public string AvailabilityMessage => "PostgreSQL-backed control plane is available.";
    // </phase5-slice>

}
