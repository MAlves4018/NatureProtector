param(
    [string]$PostgresContainer = "np-postgres",
    [string]$PostgresUser = "np",
    [string]$Database = "natureprotector",
    [string]$OutputDir = "docs/evidence",
    [string]$ApiBaseUrl = "http://localhost:5254",
    [switch]$SkipApi
)

$ErrorActionPreference = "Continue"

$timestamp = Get-Date -Format "yyyy-MM-dd-HHmm"
$outputPath = Join-Path $OutputDir "v1-runtime-evidence-$timestamp.md"

New-Item -ItemType Directory -Force -Path $OutputDir | Out-Null

$script:Warnings = 0
$script:Errors = 0

function Add-Line {
    param([string]$Text = "")
    Add-Content -Path $outputPath -Value $Text -Encoding UTF8
}

function Add-Section {
    param([string]$Title)
    Add-Line ""
    Add-Line "## $Title"
    Add-Line ""
}

function Run-Command {
    param(
        [string]$Title,
        [scriptblock]$Command,
        [switch]$WarningOnly
    )

    Add-Section $Title
    Add-Line '```text'

    try {
        $result = & $Command 2>&1
        $exitCode = $LASTEXITCODE

        if ($null -ne $result) {
            Add-Line ($result | Out-String)
        }

        if ($exitCode -ne 0 -and $null -ne $exitCode) {
            if ($WarningOnly) {
                $script:Warnings++
                Add-Line ""
                Add-Line "WARNING: command exited with code $exitCode"
            } else {
                $script:Errors++
                Add-Line ""
                Add-Line "ERROR: command exited with code $exitCode"
            }
        }
    }
    catch {
        if ($WarningOnly) {
            $script:Warnings++
            Add-Line "WARNING: $($_.Exception.Message)"
        } else {
            $script:Errors++
            Add-Line "ERROR: $($_.Exception.Message)"
        }
    }

    Add-Line '```'
}

function Run-Sql {
    param(
        [string]$Title,
        [string]$Sql,
        [switch]$WarningOnly
    )

    Add-Section $Title
    Add-Line '```sql'
    Add-Line $Sql.Trim()
    Add-Line '```'
    Add-Line ""
    Add-Line '```text'

    try {
        $result = $Sql | docker exec -i $PostgresContainer psql `
            -U $PostgresUser `
            -d $Database `
            -X `
            -v ON_ERROR_STOP=0 `
            -P pager=off `
            -P null="∅" 2>&1

        $exitCode = $LASTEXITCODE

        if ($null -ne $result) {
            Add-Line ($result | Out-String)
        }

        if ($exitCode -ne 0 -and $null -ne $exitCode) {
            if ($WarningOnly) {
                $script:Warnings++
                Add-Line ""
                Add-Line "WARNING: SQL exited with code $exitCode"
            } else {
                $script:Errors++
                Add-Line ""
                Add-Line "ERROR: SQL exited with code $exitCode"
            }
        }
    }
    catch {
        if ($WarningOnly) {
            $script:Warnings++
            Add-Line "WARNING: $($_.Exception.Message)"
        } else {
            $script:Errors++
            Add-Line "ERROR: $($_.Exception.Message)"
        }
    }

    Add-Line '```'
}

Add-Line "# NatureProtector V1 Runtime Evidence"
Add-Line ""
Add-Line "- GeneratedAt: $(Get-Date -Format o)"
Add-Line "- PostgresContainer: $PostgresContainer"
Add-Line "- Database: $Database"
Add-Line "- ApiBaseUrl: $ApiBaseUrl"

Run-Command "Git branch" {
    git rev-parse --abbrev-ref HEAD
} -WarningOnly

Run-Command "Git commit" {
    git rev-parse HEAD
} -WarningOnly

Run-Command "Git status" {
    git status --short --branch
} -WarningOnly

Run-Command "Docker containers" {
    docker ps --format "table {{.Names}}\t{{.Image}}\t{{.Status}}\t{{.Ports}}"
} -WarningOnly

Run-Sql "Schemas and tables" @"
select table_schema, table_name
from information_schema.tables
where table_schema in ('control', 'pipeline', 'projection')
order by table_schema, table_name;
"@

Run-Sql "Columns" @"
select table_schema, table_name, column_name, data_type
from information_schema.columns
where table_schema in ('control', 'pipeline', 'projection')
order by table_schema, table_name, ordinal_position;
"@

Run-Sql "Control plane counts" @"
select
  (select count(*) from control.configuration_versions) as configuration_versions,
  (select count(*) from control.areas) as areas,
  (select count(*) from control.grid_cells) as grid_cells,
  (select count(*) from control.sensor_nodes) as sensor_nodes,
  (select count(*) from control.sensor_profiles) as sensor_profiles,
  (select count(*) from control.scenario_definitions) as scenario_definitions,
  (select count(*) from control.simulation_runs) as simulation_runs;
"@

Run-Sql "Active configuration" @"
select
  "Id",
  "VersionNumber",
  "IsActive",
  "Description",
  "CreatedAt",
  "CreatedBy"
from control.configuration_versions
order by "CreatedAt" desc;
"@

Run-Sql "Areas" @"
select
  "Id",
  "ConfigurationVersionId",
  "Code",
  "Name",
  "CountryCode"
from control.areas
order by "Code";
"@

Run-Sql "Sensor summary" @"
select
  "IsActive",
  "Type",
  count(*) as count
from control.sensor_nodes
group by "IsActive", "Type"
order by "IsActive" desc, "Type";
"@

Run-Sql "Latest simulation runs" @"
select *
from control.simulation_runs
order by "CreatedAt" desc
limit 20;
"@

Run-Sql "Pipeline totals" @"
select
  (select count(*) from pipeline.event_inbox) as inbox_total,
  (select count(*) from pipeline.processing_attempts) as attempts_total,
  (select count(*) from pipeline.rejected_events) as rejected_total,
  (select count(*) from pipeline.quarantined_events) as quarantined_total;
"@

Run-Sql "Inbox by status" @"
select
  "Status",
  count(*) as count
from pipeline.event_inbox
group by "Status"
order by "Status";
"@

Run-Sql "Inbox time range" @"
select
  min("ReceivedAt") as first_inbox_received_at,
  max("ReceivedAt") as last_inbox_received_at,
  min("EventTime") as first_event_time,
  max("EventTime") as last_event_time
from pipeline.event_inbox;
"@

Run-Sql "Latest inbox events" @"
select
  "Id",
  "EventId",
  "EventType",
  "Producer",
  "Status",
  "AttemptCount",
  "EventTime",
  "ReceivedAt",
  "LastAttemptAt",
  "LastProcessedAt",
  "LastErrorCode",
  "LastErrorMessage",
  "QuarantinedAt"
from pipeline.event_inbox
order by "ReceivedAt" desc
limit 25;
"@

Run-Sql "Inbox errors" @"
select
  "LastErrorCode",
  "LastErrorMessage",
  count(*) as count
from pipeline.event_inbox
where "LastErrorMessage" is not null
  and "LastErrorMessage" <> ''
group by "LastErrorCode", "LastErrorMessage"
order by count desc, "LastErrorCode";
"@

Run-Sql "Latest processing attempts" @"
select
  "Id",
  "InboxEventId",
  "AttemptNumber",
  "Stage",
  "StartedAt",
  "FinishedAt",
  "Outcome",
  "ErrorCode",
  "ErrorMessage"
from pipeline.processing_attempts
order by "StartedAt" desc
limit 50;
"@

Run-Sql "Processing attempt errors" @"
select
  "Stage",
  "Outcome",
  "ErrorCode",
  "ErrorMessage",
  count(*) as count
from pipeline.processing_attempts
where "ErrorMessage" is not null
  and "ErrorMessage" <> ''
group by "Stage", "Outcome", "ErrorCode", "ErrorMessage"
order by count desc;
"@

Run-Sql "Rejected events summary" @"
select
  "RejectionCode",
  "RejectionReason",
  count(*) as count,
  min("RejectedAt") as first_rejected_at,
  max("RejectedAt") as last_rejected_at
from pipeline.rejected_events
group by "RejectionCode", "RejectionReason"
order by max("RejectedAt") desc, count desc;
"@ -WarningOnly

Run-Sql "Latest rejected events" @"
select
  "Id",
  "InboxEventId",
  "EventId",
  "RejectionCode",
  "RejectionReason",
  "RejectedAt",
  left("RawBodyUtf8", 500) as raw_body_sample,
  "MetadataJson"
from pipeline.rejected_events
order by "RejectedAt" desc
limit 25;
"@ -WarningOnly

Run-Sql "Quarantined events summary" @"
select
  "QuarantineCode",
  "QuarantineReason",
  count(*) as count,
  min("QuarantinedAt") as first_quarantined_at,
  max("QuarantinedAt") as last_quarantined_at
from pipeline.quarantined_events
group by "QuarantineCode", "QuarantineReason"
order by max("QuarantinedAt") desc, count desc;
"@ -WarningOnly

Run-Sql "Latest quarantined events" @"
select
  "Id",
  "InboxEventId",
  "EventId",
  "FinalAttemptNumber",
  "QuarantineCode",
  "QuarantineReason",
  "QuarantinedAt",
  "MetadataJson"
from pipeline.quarantined_events
order by "QuarantinedAt" desc
limit 25;
"@ -WarningOnly

Run-Sql "Projection totals" @"
select
  (select count(*) from projection.accepted_reading_log) as accepted_readings,
  (select count(*) from projection.risk_assessment_log) as risk_assessments,
  (select count(*) from projection.area_risk_snapshot_log) as area_risk_snapshots,
  (select count(*) from projection.cell_operational_state) as cell_operational_states,
  (select count(*) from projection.area_operational_state) as area_operational_states,
  (select count(*) from projection.alert_state) as alert_states;
"@

Run-Sql "Projection time ranges" @"
select
  (select min("CreatedAt") from projection.risk_assessment_log) as first_risk_created_at,
  (select max("CreatedAt") from projection.risk_assessment_log) as last_risk_created_at,
  (select min("SnapshotTimestamp") from projection.area_risk_snapshot_log) as first_area_snapshot_timestamp,
  (select max("SnapshotTimestamp") from projection.area_risk_snapshot_log) as last_area_snapshot_timestamp,
  (select max("UpdatedAt") from projection.area_operational_state) as last_area_state_updated_at,
  (select max("UpdatedAt") from projection.alert_state) as last_alert_updated_at;
"@

Run-Sql "Risk assessment columns" @"
select column_name, data_type
from information_schema.columns
where table_schema = 'projection'
  and table_name = 'risk_assessment_log'
order by ordinal_position;
"@

Run-Sql "Risk assessment score range" @"
select
  count(*) as risk_assessments,
  min("RiskScore") as min_risk_score,
  max("RiskScore") as max_risk_score,
  avg("RiskScore") as avg_risk_score
from projection.risk_assessment_log;
"@

Run-Sql "Risk assessment by level" @"
select
  "RiskLevel",
  count(*) as count,
  min("RiskScore") as min_score,
  max("RiskScore") as max_score
from projection.risk_assessment_log
group by "RiskLevel"
order by min_score;
"@

Run-Sql "Latest risk assessments" @"
select
  "Id",
  "AreaId",
  "SensorId",
  "GridCellId",
  "SourceEventId",
  "Timestamp",
  "RiskScore",
  "RiskLevel",
  "ExplanationSummary",
  "CreatedAt"
from projection.risk_assessment_log
order by "CreatedAt" desc
limit 25;
"@

Run-Sql "Latest area operational state" @"
select
  "Id",
  "AreaId",
  "ConfigurationVersionId",
  "SimulationRunId",
  "SnapshotTimestamp",
  "AggregateRiskScore",
  "AggregateRiskLevel",
  "Severity",
  "Summary",
  "AssessmentCount",
  "UpdatedAt"
from projection.area_operational_state
order by "UpdatedAt" desc
limit 10;
"@

Run-Sql "Latest cell operational states" @"
select
  "Id",
  "AreaId",
  "GridCellId",
  "SensorId",
  "LatestAssessmentId",
  "SnapshotTimestamp",
  "RiskScore",
  "RiskLevel",
  "Severity",
  "Summary",
  "UpdatedAt"
from projection.cell_operational_state
order by "UpdatedAt" desc
limit 25;
"@

Run-Sql "Latest area risk snapshots" @"
select *
from projection.area_risk_snapshot_log
order by "SnapshotTimestamp" desc
limit 25;
"@

Run-Sql "Alert state columns" @"
select column_name, data_type
from information_schema.columns
where table_schema = 'projection'
  and table_name = 'alert_state'
order by ordinal_position;
"@

Run-Sql "Latest alert states" @"
select
  "Id",
  "AreaId",
  "ConfigurationVersionId",
  "AreaOperationalStateId",
  "AlertCode",
  "Severity",
  "Status",
  "Message",
  "TriggeredAt",
  "UpdatedAt",
  "ResolvedAt"
from projection.alert_state
order by "UpdatedAt" desc
limit 25;
"@

Run-Sql "Alert states by status" @"
select
  "AlertCode",
  "Severity",
  "Status",
  count(*) as count,
  min("TriggeredAt") as first_triggered_at,
  max("UpdatedAt") as last_updated_at
from projection.alert_state
group by "AlertCode", "Severity", "Status"
order by "AlertCode", "Severity", "Status";
"@

Run-Sql "Area operational state joined to alerts" @"
select
  aos."AreaId",
  aos."AggregateRiskScore",
  aos."AggregateRiskLevel",
  aos."Severity" as area_severity,
  aos."SnapshotTimestamp",
  aos."UpdatedAt" as area_updated_at,
  als."AlertCode",
  als."Severity" as alert_severity,
  als."Status" as alert_status,
  als."Message" as alert_message,
  als."TriggeredAt",
  als."ResolvedAt"
from projection.area_operational_state aos
left join projection.alert_state als
  on als."AreaOperationalStateId" = aos."Id"
order by aos."UpdatedAt" desc, als."UpdatedAt" desc
limit 25;
"@

Run-Sql "Blocked or zero risk probe" @"
select
  count(*) filter (where "RiskScore" = 0) as zero_risk_assessments,
  count(*) filter (where lower("ExplanationSummary") like '%blocked%') as explanations_containing_blocked,
  count(*) filter (where lower("ExplanationSummary") like '%partial%') as explanations_containing_partial
from projection.risk_assessment_log;
"@

if (-not $SkipApi) {
    Run-Command "API operational state" {
        Invoke-RestMethod "$ApiBaseUrl/api/control/areas/proenca-a-nova/operational-state" |
            ConvertTo-Json -Depth 20
    } -WarningOnly

    Run-Command "API active alerts" {
        Invoke-RestMethod "$ApiBaseUrl/api/control/areas/proenca-a-nova/alerts/active" |
            ConvertTo-Json -Depth 20
    } -WarningOnly
}

Add-Section "Technical verdict template"
Add-Line "```text"
Add-Line "Veredicto: preencher manualmente após leitura dos resultados"
Add-Line "Confiança: baixa / média / alta"
Add-Line ""
Add-Line "Checks:"
Add-Line "- Infra viva: verificar Docker containers."
Add-Line "- Control plane carregado: verificar configuração ativa, área, células, sensores, cenários."
Add-Line "- Pipeline processou eventos: verificar inbox_total, status e attempts."
Add-Line "- Erros explicados: verificar rejected/quarantined/errors."
Add-Line "- Projeções existem: verificar accepted_reading_log, risk_assessment_log, snapshots, states."
Add-Line "- API reflete DB: comparar operational-state com area_operational_state."
Add-Line "- Alert policy: verificar alert_state e/ou testes se não houver cenário acima de thresholds."
Add-Line "- Blocked != zero risk: verificar zero_risk_assessments e evidência de eligibility/testes."
Add-Line "```"

Add-Section "Collection summary"
Add-Line "```text"
Add-Line "Output: $outputPath"
Add-Line "Warnings: $script:Warnings"
Add-Line "Errors: $script:Errors"

if ($script:Errors -eq 0 -and $script:Warnings -eq 0) {
    Add-Line "Collection result: OK"
}
elseif ($script:Errors -eq 0) {
    Add-Line "Collection result: OK with warnings"
}
else {
    Add-Line "Collection result: completed with errors"
}

Add-Line "```"

Write-Host "Evidence written to $outputPath"
Write-Host "Warnings: $script:Warnings"
Write-Host "Errors: $script:Errors"