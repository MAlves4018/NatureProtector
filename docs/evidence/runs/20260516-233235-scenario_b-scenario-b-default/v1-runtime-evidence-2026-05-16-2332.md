# NatureProtector V1 Runtime Evidence

- GeneratedAt: 2026-05-16T23:32:42.8710087+02:00
- PostgresContainer: np-postgres
- Database: natureprotector
- ApiBaseUrl: http://localhost:5254

## Git branch

```text
master

```

## Git commit

```text
8f66b2474654bcc3c3267a345cd1ce59732a3d33

```

## Git status

```text
## master...origin/master
 M docs/NatureProtector-V1-overview.md
 M src/NatureProtector.Backoffice.Api/ControlPlane/Contracts/ControlPlaneResponses.cs
 M src/NatureProtector.Backoffice.Api/ControlPlane/Services/IControlPlaneService.cs
 M src/NatureProtector.Backoffice.Api/ControlPlane/Services/PostgresControlPlaneService.cs
 M src/NatureProtector.Backoffice.Api/ControlPlane/Services/UnavailableControlPlaneService.cs
 M src/NatureProtector.Backoffice.Api/NatureProtector.Backoffice.Api.http
 M src/NatureProtector.Backoffice.Api/README.md
 M tests/NatureProtector.Backoffice.Api.Tests/ControlPlaneApiTests.cs
 M tests/NatureProtector.Backoffice.Api.Tests/ControlPlaneApiWebApplicationFactory.cs
 M tests/NatureProtector.Backoffice.Api.Tests/UnavailableControlPlaneServiceTests.cs
 M webUI/docs/como_usar.md
 M webUI/src/app/components/views/Pipeline.tsx
 M webUI/src/app/services/api.ts
 M webUI/src/app/types/index.tsx
?? docs/evidence/runs/20260516-233235-scenario_b-scenario-b-default/
?? src/NatureProtector.Backoffice.Api/Controllers/ControlRuntimeController.cs
?? tests/NatureProtector.Backoffice.Api.Tests/RuntimeSummaryServiceTests.cs

```

## Docker containers

```text
NAMES         IMAGE                                      STATUS         PORTS
np-influxdb   influxdb:3.7.0-core                        Up 6 minutes   0.0.0.0:8181->8181/tcp
np-rabbitmq   rabbitmq:4.0.6-management                  Up 6 minutes   4369/tcp, 5671/tcp, 0.0.0.0:5672->5672/tcp, 15671/tcp, 15691-15692/tcp, 25672/tcp, 0.0.0.0:15672->15672/tcp
np-postgres   postgres:16                                Up 6 minutes   0.0.0.0:5433->5432/tcp
np-grafana    grafana/grafana-enterprise:13.0.1-ubuntu   Up 6 minutes   0.0.0.0:3000->3000/tcp

```

## Schemas and tables

```sql
select table_schema, table_name
from information_schema.tables
where table_schema in ('control', 'pipeline', 'projection')
order by table_schema, table_name;
```

```text
 table_schema |        table_name         
--------------+---------------------------
 control      | area_contexts
 control      | areas
 control      | configuration_versions
 control      | dataset_artifacts
 control      | grid_cells
 control      | rule_set_versions
 control      | scenario_dataset_bindings
 control      | scenario_definitions
 control      | sensor_networks
 control      | sensor_nodes
 control      | sensor_profiles
 control      | simulation_runs
 pipeline     | event_inbox
 pipeline     | processing_attempts
 pipeline     | quarantined_events
 pipeline     | rejected_events
 projection   | accepted_reading_log
 projection   | alert_state
 projection   | area_operational_state
 projection   | area_risk_snapshot_log
 projection   | cell_operational_state
 projection   | risk_assessment_log
(22 rows)


```

## Columns

```sql
select table_schema, table_name, column_name, data_type
from information_schema.columns
where table_schema in ('control', 'pipeline', 'projection')
order by table_schema, table_name, ordinal_position;
```

```text
 table_schema |        table_name         |          column_name           |        data_type         
--------------+---------------------------+--------------------------------+--------------------------
 control      | area_contexts             | Id                             | uuid
 control      | area_contexts             | AreaId                         | uuid
 control      | area_contexts             | VegetationType                 | character varying
 control      | area_contexts             | VegetationDensity              | double precision
 control      | area_contexts             | PopulationExposure             | double precision
 control      | area_contexts             | CriticalInfrastructureExposure | double precision
 control      | area_contexts             | Seasonality                    | character varying
 control      | areas                     | Id                             | uuid
 control      | areas                     | ConfigurationVersionId         | uuid
 control      | areas                     | Code                           | character varying
 control      | areas                     | Name                           | character varying
 control      | areas                     | CountryCode                    | character varying
 control      | areas                     | GeometryGeoJson                | text
 control      | areas                     | MetadataJson                   | text
 control      | configuration_versions    | Id                             | uuid
 control      | configuration_versions    | VersionNumber                  | integer
 control      | configuration_versions    | Description                    | character varying
 control      | configuration_versions    | IsActive                       | boolean
 control      | configuration_versions    | CreatedAt                      | timestamp with time zone
 control      | configuration_versions    | CreatedBy                      | character varying
 control      | dataset_artifacts         | Id                             | uuid
 control      | dataset_artifacts         | DatasetCode                    | character varying
 control      | dataset_artifacts         | DatasetType                    | character varying
 control      | dataset_artifacts         | SourceName                     | character varying
 control      | dataset_artifacts         | SourceUrl                      | character varying
 control      | dataset_artifacts         | AreaCode                       | character varying
 control      | dataset_artifacts         | Version                        | character varying
 control      | dataset_artifacts         | Format                         | character varying
 control      | dataset_artifacts         | RelativePath                   | character varying
 control      | dataset_artifacts         | Checksum                       | character varying
 control      | dataset_artifacts         | ValidFrom                      | date
 control      | dataset_artifacts         | ValidTo                        | date
 control      | dataset_artifacts         | MetadataJson                   | text
 control      | grid_cells                | Id                             | uuid
 control      | grid_cells                | AreaId                         | uuid
 control      | grid_cells                | ConfigurationVersionId         | uuid
 control      | grid_cells                | CellCode                       | character varying
 control      | grid_cells                | CentroidLatitude               | double precision
 control      | grid_cells                | CentroidLongitude              | double precision
 control      | grid_cells                | PolygonGeoJson                 | text
 control      | grid_cells                | AltitudeMeters                 | double precision
 control      | grid_cells                | SlopeDegrees                   | double precision
 control      | grid_cells                | AspectDegrees                  | double precision
 control      | grid_cells                | LandCoverClass                 | character varying
 control      | grid_cells                | DominantForestType             | character varying
 control      | grid_cells                | DominantFuelModel              | character varying
 control      | grid_cells                | TreeCoverDensity               | double precision
 control      | grid_cells                | StructuralHazard               | character varying
 control      | grid_cells                | ConjuncturalHazard             | character varying
 control      | rule_set_versions         | Id                             | uuid
 control      | rule_set_versions         | ConfigurationVersionId         | uuid
 control      | rule_set_versions         | Name                           | character varying
 control      | rule_set_versions         | Version                        | character varying
 control      | rule_set_versions         | Description                    | character varying
 control      | rule_set_versions         | ParametersJson                 | text
 control      | rule_set_versions         | IsActive                       | boolean
 control      | scenario_dataset_bindings | Id                             | uuid
 control      | scenario_dataset_bindings | ScenarioId                     | uuid
 control      | scenario_dataset_bindings | DatasetArtifactId              | uuid
 control      | scenario_dataset_bindings | BindingRole                    | character varying
 control      | scenario_dataset_bindings | Notes                          | character varying
 control      | scenario_definitions      | Id                             | uuid
 control      | scenario_definitions      | AreaId                         | uuid
 control      | scenario_definitions      | ConfigurationVersionId         | uuid
 control      | scenario_definitions      | BaseScenarioId                 | uuid
 control      | scenario_definitions      | Code                           | character varying
 control      | scenario_definitions      | Name                           | character varying
 control      | scenario_definitions      | ScenarioKind                   | integer
 control      | scenario_definitions      | Description                    | character varying
 control      | scenario_definitions      | ParametersJson                 | text
 control      | sensor_networks           | Id                             | uuid
 control      | sensor_networks           | ConfigurationVersionId         | uuid
 control      | sensor_networks           | Name                           | character varying
 control      | sensor_nodes              | Id                             | uuid
 control      | sensor_nodes              | AreaId                         | uuid
 control      | sensor_nodes              | GridCellId                     | uuid
 control      | sensor_nodes              | ProfileId                      | uuid
 control      | sensor_nodes              | ConfigurationVersionId         | uuid
 control      | sensor_nodes              | NetworkId                      | uuid
 control      | sensor_nodes              | Name                           | character varying
 control      | sensor_nodes              | Type                           | integer
 control      | sensor_nodes              | Latitude                       | double precision
 control      | sensor_nodes              | Longitude                      | double precision
 control      | sensor_nodes              | AltitudeMeters                 | double precision
 control      | sensor_nodes              | IsActive                       | boolean
 control      | sensor_nodes              | InstallationProfile            | character varying
 control      | sensor_profiles           | Id                             | uuid
 control      | sensor_profiles           | ConfigurationVersionId         | uuid
 control      | sensor_profiles           | Name                           | character varying
 control      | sensor_profiles           | SensorFamily                   | character varying
 control      | sensor_profiles           | AccuracyProfileJson            | text
 control      | sensor_profiles           | NoiseProfileJson               | text
 control      | sensor_profiles           | FaultProfileJson               | text
 control      | sensor_profiles           | PublicationPolicyJson          | text
 control      | simulation_runs           | Id                             | uuid
 control      | simulation_runs           | AreaId                         | uuid
 control      | simulation_runs           | ScenarioId                     | uuid
 control      | simulation_runs           | ConfigurationVersionId         | uuid
 control      | simulation_runs           | ScenarioCode                   | character varying
 control      | simulation_runs           | ScenarioName                   | character varying
 control      | simulation_runs           | CreatedAt                      | timestamp with time zone
 control      | simulation_runs           | StartedAt                      | timestamp with time zone
 control      | simulation_runs           | EndedAt                        | timestamp with time zone
 control      | simulation_runs           | LogicalStartTimestamp          | timestamp with time zone
 control      | simulation_runs           | IntervalSeconds                | integer
 control      | simulation_runs           | NumberOfCycles                 | integer
 control      | simulation_runs           | ExecutionSeed                  | integer
 control      | simulation_runs           | Status                         | integer
 control      | simulation_runs           | MetadataJson                   | text
 pipeline     | event_inbox               | Id                             | uuid
 pipeline     | event_inbox               | EventId                        | uuid
 pipeline     | event_inbox               | SchemaVersion                  | character varying
 pipeline     | event_inbox               | CorrelationId                  | character varying
 pipeline     | event_inbox               | Producer                       | character varying
 pipeline     | event_inbox               | EventType                      | character varying
 pipeline     | event_inbox               | AreaId                         | uuid
 pipeline     | event_inbox               | EventTime                      | timestamp with time zone
 pipeline     | event_inbox               | ReceivedAt                     | timestamp with time zone
 pipeline     | event_inbox               | IngestTime                     | timestamp with time zone
 pipeline     | event_inbox               | PayloadJson                    | text
 pipeline     | event_inbox               | EnvelopeJson                   | text
 pipeline     | event_inbox               | Status                         | integer
 pipeline     | event_inbox               | AttemptCount                   | integer
 pipeline     | event_inbox               | LastAttemptAt                  | timestamp with time zone
 pipeline     | event_inbox               | LastProcessedAt                | timestamp with time zone
 pipeline     | event_inbox               | LastErrorCode                  | character varying
 pipeline     | event_inbox               | LastErrorMessage               | character varying
 pipeline     | event_inbox               | NextAttemptNotBefore           | timestamp with time zone
 pipeline     | event_inbox               | QuarantinedAt                  | timestamp with time zone
 pipeline     | processing_attempts       | Id                             | uuid
 pipeline     | processing_attempts       | InboxEventId                   | uuid
 pipeline     | processing_attempts       | AttemptNumber                  | integer
 pipeline     | processing_attempts       | Stage                          | character varying
 pipeline     | processing_attempts       | StartedAt                      | timestamp with time zone
 pipeline     | processing_attempts       | FinishedAt                     | timestamp with time zone
 pipeline     | processing_attempts       | Outcome                        | integer
 pipeline     | processing_attempts       | ErrorCode                      | character varying
 pipeline     | processing_attempts       | ErrorMessage                   | character varying
 pipeline     | quarantined_events        | Id                             | uuid
 pipeline     | quarantined_events        | InboxEventId                   | uuid
 pipeline     | quarantined_events        | EventId                        | uuid
 pipeline     | quarantined_events        | FinalAttemptNumber             | integer
 pipeline     | quarantined_events        | QuarantineCode                 | character varying
 pipeline     | quarantined_events        | QuarantineReason               | character varying
 pipeline     | quarantined_events        | QuarantinedAt                  | timestamp with time zone
 pipeline     | quarantined_events        | MetadataJson                   | text
 pipeline     | rejected_events           | Id                             | uuid
 pipeline     | rejected_events           | InboxEventId                   | uuid
 pipeline     | rejected_events           | EventId                        | uuid
 pipeline     | rejected_events           | RejectionCode                  | character varying
 pipeline     | rejected_events           | RejectionReason                | character varying
 pipeline     | rejected_events           | RejectedAt                     | timestamp with time zone
 pipeline     | rejected_events           | RawBodyUtf8                    | text
 pipeline     | rejected_events           | MetadataJson                   | text
 projection   | accepted_reading_log      | Id                             | uuid
 projection   | accepted_reading_log      | EventId                        | uuid
 projection   | accepted_reading_log      | AreaId                         | uuid
 projection   | accepted_reading_log      | SensorId                       | uuid
 projection   | accepted_reading_log      | MetricType                     | character varying
 projection   | accepted_reading_log      | MeasurementUnit                | character varying
 projection   | accepted_reading_log      | OperationalState               | character varying
 projection   | accepted_reading_log      | Value                          | double precision
 projection   | accepted_reading_log      | EventTime                      | timestamp with time zone
 projection   | accepted_reading_log      | IngestTime                     | timestamp with time zone
 projection   | accepted_reading_log      | Producer                       | character varying
 projection   | accepted_reading_log      | CorrelationId                  | character varying
 projection   | accepted_reading_log      | PayloadJson                    | text
 projection   | accepted_reading_log      | EnvelopeJson                   | text
 projection   | accepted_reading_log      | CreatedAt                      | timestamp with time zone
 projection   | alert_state               | Id                             | uuid
 projection   | alert_state               | AreaId                         | uuid
 projection   | alert_state               | ConfigurationVersionId         | uuid
 projection   | alert_state               | AreaOperationalStateId         | uuid
 projection   | alert_state               | AlertCode                      | character varying
 projection   | alert_state               | Severity                       | character varying
 projection   | alert_state               | Status                         | character varying
 projection   | alert_state               | Message                        | character varying
 projection   | alert_state               | TriggeredAt                    | timestamp with time zone
 projection   | alert_state               | UpdatedAt                      | timestamp with time zone
 projection   | alert_state               | ResolvedAt                     | timestamp with time zone
 projection   | area_operational_state    | Id                             | uuid
 projection   | area_operational_state    | AreaId                         | uuid
 projection   | area_operational_state    | ConfigurationVersionId         | uuid
 projection   | area_operational_state    | SimulationRunId                | uuid
 projection   | area_operational_state    | SnapshotTimestamp              | timestamp with time zone
 projection   | area_operational_state    | AggregateRiskScore             | double precision
 projection   | area_operational_state    | AggregateRiskLevel             | character varying
 projection   | area_operational_state    | Severity                       | character varying
 projection   | area_operational_state    | Summary                        | character varying
 projection   | area_operational_state    | AssessmentCount                | integer
 projection   | area_operational_state    | UpdatedAt                      | timestamp with time zone
 projection   | area_risk_snapshot_log    | Id                             | uuid
 projection   | area_risk_snapshot_log    | AreaId                         | uuid
 projection   | area_risk_snapshot_log    | SimulationRunId                | uuid
 projection   | area_risk_snapshot_log    | SnapshotTimestamp              | timestamp with time zone
 projection   | area_risk_snapshot_log    | AggregateRiskScore             | double precision
 projection   | area_risk_snapshot_log    | AggregateRiskLevel             | character varying
 projection   | area_risk_snapshot_log    | Summary                        | character varying
 projection   | area_risk_snapshot_log    | AssessmentCount                | integer
 projection   | area_risk_snapshot_log    | CreatedAt                      | timestamp with time zone
 projection   | cell_operational_state    | Id                             | uuid
 projection   | cell_operational_state    | AreaId                         | uuid
 projection   | cell_operational_state    | GridCellId                     | uuid
 projection   | cell_operational_state    | SensorId                       | uuid
 projection   | cell_operational_state    | LatestAssessmentId             | uuid
 projection   | cell_operational_state    | SnapshotTimestamp              | timestamp with time zone
 projection   | cell_operational_state    | RiskScore                      | double precision
 projection   | cell_operational_state    | RiskLevel                      | character varying
 projection   | cell_operational_state    | Severity                       | character varying
 projection   | cell_operational_state    | Summary                        | character varying
 projection   | cell_operational_state    | UpdatedAt                      | timestamp with time zone
 projection   | risk_assessment_log       | Id                             | uuid
 projection   | risk_assessment_log       | AreaId                         | uuid
 projection   | risk_assessment_log       | SensorId                       | uuid
 projection   | risk_assessment_log       | GridCellId                     | uuid
 projection   | risk_assessment_log       | SourceEventId                  | uuid
 projection   | risk_assessment_log       | Timestamp                      | timestamp with time zone
 projection   | risk_assessment_log       | RiskScore                      | double precision
 projection   | risk_assessment_log       | RiskLevel                      | character varying
 projection   | risk_assessment_log       | ExplanationSummary             | character varying
 projection   | risk_assessment_log       | CreatedAt                      | timestamp with time zone
(221 rows)


```

## Control plane counts

```sql
select
  (select count(*) from control.configuration_versions) as configuration_versions,
  (select count(*) from control.areas) as areas,
  (select count(*) from control.grid_cells) as grid_cells,
  (select count(*) from control.sensor_nodes) as sensor_nodes,
  (select count(*) from control.sensor_profiles) as sensor_profiles,
  (select count(*) from control.scenario_definitions) as scenario_definitions,
  (select count(*) from control.simulation_runs) as simulation_runs;
```

```text
 configuration_versions | areas | grid_cells | sensor_nodes | sensor_profiles | scenario_definitions | simulation_runs 
------------------------+-------+------------+--------------+-----------------+----------------------+-----------------
                      1 |     1 |        467 |           75 |               3 |                    3 |              31
(1 row)


```

## Active configuration

```sql
select
  "Id",
  "VersionNumber",
  "IsActive",
  "Description",
  "CreatedAt",
  "CreatedBy"
from control.configuration_versions
order by "CreatedAt" desc;
```

```text
                  Id                  | VersionNumber | IsActive |                                 Description                                  |           CreatedAt           |     CreatedBy      
--------------------------------------+---------------+----------+------------------------------------------------------------------------------+-------------------------------+--------------------
 c03be3d5-1f70-15cb-2fc0-3c86a204a644 |             1 | t        | Bootstrap control-plane import for Proenca-a-Nova with pilot sensor network. | 2026-05-15 17:53:26.714942+00 | phase-04-bootstrap
(1 row)


```

## Areas

```sql
select
  "Id",
  "ConfigurationVersionId",
  "Code",
  "Name",
  "CountryCode"
from control.areas
order by "Code";
```

```text
                  Id                  |        ConfigurationVersionId        |      Code      |      Name      | CountryCode 
--------------------------------------+--------------------------------------+----------------+----------------+-------------
 b3f4fb84-bf17-5522-a5f3-70fd1212f381 | c03be3d5-1f70-15cb-2fc0-3c86a204a644 | proenca-a-nova | Proen├ºa-a-Nova | PT
(1 row)


```

## Sensor summary

```sql
select
  "IsActive",
  "Type",
  count(*) as count
from control.sensor_nodes
group by "IsActive", "Type"
order by "IsActive" desc, "Type";
```

```text
 IsActive | Type | count 
----------+------+-------
 t        |    0 |     2
 t        |    1 |     2
 t        |    2 |     2
 f        |    0 |    23
 f        |    1 |    23
 f        |    2 |    23
(6 rows)


```

## Latest simulation runs

```sql
select *
from control.simulation_runs
order by "CreatedAt" desc
limit 20;
```

```text
                  Id                  |                AreaId                |              ScenarioId              |        ConfigurationVersionId        | ScenarioCode |             ScenarioName              |           CreatedAt           |           StartedAt           |            EndedAt            | LogicalStartTimestamp  | IntervalSeconds | NumberOfCycles | ExecutionSeed | Status |                                                                                                                                                                                                                                                                                                                                    MetadataJson                                                                                                                                                                                                                                                                                                                                     
--------------------------------------+--------------------------------------+--------------------------------------+--------------------------------------+--------------+---------------------------------------+-------------------------------+-------------------------------+-------------------------------+------------------------+-----------------+----------------+---------------+--------+-------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------
 28bdba51-2159-4aef-91ea-b746b0939f16 | b3f4fb84-bf17-5522-a5f3-70fd1212f381 | 6034251c-b502-5f7b-a557-7187178cfb04 | c03be3d5-1f70-15cb-2fc0-3c86a204a644 | scenario_b   | Scenario B - High Risk Proenca-a-Nova | 2026-05-15 21:44:35.582085+00 | 2026-05-15 21:44:35.707201+00 | 2026-05-15 21:44:55.998499+00 | 2020-09-13 10:00:00+00 |               5 |              5 |         12345 |      3 | {"sensor_count":6,"scenario_category":"HighRisk","orchestrator_correlation_id":"9de472e6-b16a-4470-8451-5ce136decb04","run_overrides":{"requested":{"sensor_count":6,"number_of_cycles":5,"interval_seconds":5,"seed":12345,"degradation_profile":"none","orchestrator_correlation_id":"9de472e6-b16a-4470-8451-5ce136decb04"},"resolved":{"sensor_count":6,"number_of_cycles":5,"interval_seconds":5,"seed":12345,"degradation_profile":"none","orchestrator_correlation_id":"9de472e6-b16a-4470-8451-5ce136decb04","selected_sensor_names":["pilot-humidity-0001","pilot-humidity-0230","pilot-temperature-0001","pilot-temperature-0230","pilot-wind-0001","pilot-wind-0230"]}}}
 0f7892f3-749f-4331-9eed-15962d697035 | b3f4fb84-bf17-5522-a5f3-70fd1212f381 | 6034251c-b502-5f7b-a557-7187178cfb04 | c03be3d5-1f70-15cb-2fc0-3c86a204a644 | scenario_b   | Scenario B - High Risk Proenca-a-Nova | 2026-05-15 20:01:16.119679+00 | 2026-05-15 20:01:16.241917+00 | 2026-05-15 20:01:36.557921+00 | 2020-09-13 10:00:00+00 |               5 |              5 |         12345 |      3 | {"sensor_count":6,"scenario_category":"HighRisk","orchestrator_correlation_id":"0e64a923-f788-4c29-9b87-38266b8035a5","run_overrides":{"requested":{"sensor_count":6,"number_of_cycles":5,"interval_seconds":5,"seed":12345,"degradation_profile":"none","orchestrator_correlation_id":"0e64a923-f788-4c29-9b87-38266b8035a5"},"resolved":{"sensor_count":6,"number_of_cycles":5,"interval_seconds":5,"seed":12345,"degradation_profile":"none","orchestrator_correlation_id":"0e64a923-f788-4c29-9b87-38266b8035a5","selected_sensor_names":["pilot-humidity-0001","pilot-humidity-0230","pilot-temperature-0001","pilot-temperature-0230","pilot-wind-0001","pilot-wind-0230"]}}}
 c8a664e0-8145-40f9-bb00-aaf5a51ebec0 | b3f4fb84-bf17-5522-a5f3-70fd1212f381 | 6034251c-b502-5f7b-a557-7187178cfb04 | c03be3d5-1f70-15cb-2fc0-3c86a204a644 | scenario_b   | Scenario B - High Risk Proenca-a-Nova | 2026-05-15 19:47:53.007711+00 | 2026-05-15 19:47:53.129802+00 | 2026-05-15 19:48:13.443998+00 | 2020-09-13 10:00:00+00 |               5 |              5 |         12345 |      3 | {"sensor_count":6,"scenario_category":"HighRisk","orchestrator_correlation_id":"f29484ef-51d2-48d9-bc78-1846cdacda52","run_overrides":{"requested":{"sensor_count":6,"number_of_cycles":5,"interval_seconds":5,"seed":12345,"degradation_profile":"none","orchestrator_correlation_id":"f29484ef-51d2-48d9-bc78-1846cdacda52"},"resolved":{"sensor_count":6,"number_of_cycles":5,"interval_seconds":5,"seed":12345,"degradation_profile":"none","orchestrator_correlation_id":"f29484ef-51d2-48d9-bc78-1846cdacda52","selected_sensor_names":["pilot-humidity-0001","pilot-humidity-0230","pilot-temperature-0001","pilot-temperature-0230","pilot-wind-0001","pilot-wind-0230"]}}}
 2f8bf6cc-4ffe-4f64-ada7-fe8e3380ae67 | b3f4fb84-bf17-5522-a5f3-70fd1212f381 | 6034251c-b502-5f7b-a557-7187178cfb04 | c03be3d5-1f70-15cb-2fc0-3c86a204a644 | scenario_b   | Scenario B - High Risk Proenca-a-Nova | 2026-05-15 19:33:35.450485+00 | 2026-05-15 19:33:35.582971+00 | 2026-05-15 19:33:55.897092+00 | 2020-09-13 10:00:00+00 |               5 |              5 |         12345 |      3 | {"sensor_count":6,"scenario_category":"HighRisk","orchestrator_correlation_id":"d0f2a63b-8fc6-4f08-ad21-5a403984f828","run_overrides":{"requested":{"sensor_count":6,"number_of_cycles":5,"interval_seconds":5,"seed":12345,"degradation_profile":"none","orchestrator_correlation_id":"d0f2a63b-8fc6-4f08-ad21-5a403984f828"},"resolved":{"sensor_count":6,"number_of_cycles":5,"interval_seconds":5,"seed":12345,"degradation_profile":"none","orchestrator_correlation_id":"d0f2a63b-8fc6-4f08-ad21-5a403984f828","selected_sensor_names":["pilot-humidity-0001","pilot-humidity-0230","pilot-temperature-0001","pilot-temperature-0230","pilot-wind-0001","pilot-wind-0230"]}}}
 9b937806-b2a2-46fb-9a55-a99f6d29235b | b3f4fb84-bf17-5522-a5f3-70fd1212f381 | 6034251c-b502-5f7b-a557-7187178cfb04 | c03be3d5-1f70-15cb-2fc0-3c86a204a644 | scenario_b   | Scenario B - High Risk Proenca-a-Nova | 2026-05-15 18:40:52.486417+00 | 2026-05-15 18:40:52.609334+00 | 2026-05-15 18:50:23.420853+00 | 2020-09-13 10:00:00+00 |              30 |             20 |         12345 |      3 | {"sensor_count":6,"scenario_category":"HighRisk"}
 a44215a7-14cf-48a9-9a88-5d0fb052fdfd | b3f4fb84-bf17-5522-a5f3-70fd1212f381 | 6034251c-b502-5f7b-a557-7187178cfb04 | c03be3d5-1f70-15cb-2fc0-3c86a204a644 | scenario_b   | Scenario B - High Risk Proenca-a-Nova | 2026-05-15 18:14:16.881783+00 | 2026-05-15 18:14:16.997118+00 | 2026-05-15 18:23:47.850931+00 | 2020-09-13 10:00:00+00 |              30 |             20 |         12345 |      3 | {"sensor_count":6,"scenario_category":"HighRisk"}
 890350a3-372a-4ad8-8b0e-4f232cf3d0a9 | b3f4fb84-bf17-5522-a5f3-70fd1212f381 | 6034251c-b502-5f7b-a557-7187178cfb04 | c03be3d5-1f70-15cb-2fc0-3c86a204a644 | scenario_b   | Scenario B - High Risk Proenca-a-Nova | 2026-05-15 17:53:35.889455+00 | 2026-05-15 17:53:36.005851+00 | 2026-05-15 18:03:07.099306+00 | 2020-09-13 10:00:00+00 |              30 |             20 |         12345 |      3 | {"sensor_count":6,"scenario_category":"HighRisk"}
 7afd89ce-98f1-4e9c-819f-a76a104a633e | b3f4fb84-bf17-5522-a5f3-70fd1212f381 | 6034251c-b502-5f7b-a557-7187178cfb04 | c03be3d5-1f70-15cb-2fc0-3c86a204a644 | scenario_b   | Scenario B - High Risk Proenca-a-Nova | 2026-05-13 21:48:33.22248+00  | 2026-05-13 21:48:33.330601+00 | 2026-05-15 18:34:48.203568+00 | 2020-09-13 10:00:00+00 |              30 |             20 |         12345 |      5 | {"sensor_count":6,"scenario_category":"HighRisk"}
 92b59da9-6c3d-4f4c-ba16-bd93656c0f91 | b3f4fb84-bf17-5522-a5f3-70fd1212f381 | 6034251c-b502-5f7b-a557-7187178cfb04 | c03be3d5-1f70-15cb-2fc0-3c86a204a644 | scenario_b   | Scenario B - High Risk Proenca-a-Nova | 2026-05-12 11:02:33.727336+00 | 2026-05-12 11:02:33.853471+00 | 2026-05-12 11:12:04.654885+00 | 2020-09-13 10:00:00+00 |              30 |             20 |         12345 |      3 | {"sensor_count":6,"scenario_category":"HighRisk"}
 d17c25bf-f7da-46a8-bf31-3bf6e33bd39e | b3f4fb84-bf17-5522-a5f3-70fd1212f381 | 6034251c-b502-5f7b-a557-7187178cfb04 | c03be3d5-1f70-15cb-2fc0-3c86a204a644 | scenario_b   | Scenario B - High Risk Proenca-a-Nova | 2026-04-29 18:46:51.676982+00 | 2026-04-29 18:46:51.79987+00  | 2026-04-29 18:50:09.475341+00 | 2020-09-13 10:00:00+00 |              30 |             20 |         12345 |      5 | {"sensor_count":6,"scenario_category":"HighRisk"}
 9699b9af-5fe9-4047-9ea1-6b5e3761b67a | b3f4fb84-bf17-5522-a5f3-70fd1212f381 | 6034251c-b502-5f7b-a557-7187178cfb04 | c03be3d5-1f70-15cb-2fc0-3c86a204a644 | scenario_b   | Scenario B - High Risk Proenca-a-Nova | 2026-04-29 17:25:14.744222+00 | 2026-04-29 17:25:14.869273+00 | 2026-04-29 17:26:05.773561+00 | 2020-09-13 10:00:00+00 |              30 |             20 |         12345 |      5 | {"sensor_count":6,"scenario_category":"HighRisk"}
 c07c5b32-58cc-466b-92ea-e6aa558322f7 | b3f4fb84-bf17-5522-a5f3-70fd1212f381 | 6034251c-b502-5f7b-a557-7187178cfb04 | c03be3d5-1f70-15cb-2fc0-3c86a204a644 | scenario_b   | Scenario B - High Risk Proenca-a-Nova | 2026-04-29 17:00:52.087097+00 | 2026-04-29 17:00:52.198391+00 | 2026-04-29 17:10:21.17336+00  | 2020-09-13 10:00:00+00 |              30 |             20 |         12345 |      3 | {"sensor_count":6,"scenario_category":"HighRisk"}
 9045bffa-3732-4066-9d47-12b90514204e | b3f4fb84-bf17-5522-a5f3-70fd1212f381 | 6034251c-b502-5f7b-a557-7187178cfb04 | c03be3d5-1f70-15cb-2fc0-3c86a204a644 | scenario_b   | Scenario B - High Risk Proenca-a-Nova | 2026-04-29 16:53:50.837761+00 | 2026-04-29 16:53:50.952449+00 | 2026-04-29 16:54:04.208752+00 | 2020-09-13 10:00:00+00 |              30 |             20 |         12345 |      5 | {"sensor_count":6,"scenario_category":"HighRisk"}
 b3e4d3e7-cb70-45ba-8407-1955a9771a8e | b3f4fb84-bf17-5522-a5f3-70fd1212f381 | 6034251c-b502-5f7b-a557-7187178cfb04 | c03be3d5-1f70-15cb-2fc0-3c86a204a644 | scenario_b   | Scenario B - High Risk Proenca-a-Nova | 2026-04-29 16:46:44.608164+00 | 2026-04-29 16:46:44.737889+00 | 2026-04-29 16:47:04.411921+00 | 2020-09-13 10:00:00+00 |              30 |             20 |         12345 |      5 | {"sensor_count":6,"scenario_category":"HighRisk"}
 8fb635f1-42b7-452f-a6dd-756ab27fab23 | b3f4fb84-bf17-5522-a5f3-70fd1212f381 | 6034251c-b502-5f7b-a557-7187178cfb04 | c03be3d5-1f70-15cb-2fc0-3c86a204a644 | scenario_b   | Scenario B - High Risk Proenca-a-Nova | 2026-04-21 13:10:09.716965+00 | 2026-04-21 13:10:09.830974+00 | 2026-04-21 13:19:40.183341+00 | 2020-09-13 10:00:00+00 |              30 |             20 |         12345 |      3 | {"sensor_count":6,"scenario_category":"HighRisk"}
 f6e629ed-5de5-4563-b48d-23062427c1e2 | b3f4fb84-bf17-5522-a5f3-70fd1212f381 | 6034251c-b502-5f7b-a557-7187178cfb04 | c03be3d5-1f70-15cb-2fc0-3c86a204a644 | scenario_b   | Scenario B - High Risk Proenca-a-Nova | 2026-04-18 14:54:32.692249+00 | 2026-04-18 14:54:32.804204+00 | 2026-04-18 15:04:03.060441+00 | 2020-09-13 10:00:00+00 |              30 |             20 |         12345 |      3 | {"sensor_count":6,"scenario_category":"HighRisk"}
 26f45445-7cb1-4790-8674-6d9c9d29a9a8 | b3f4fb84-bf17-5522-a5f3-70fd1212f381 | 6034251c-b502-5f7b-a557-7187178cfb04 | c03be3d5-1f70-15cb-2fc0-3c86a204a644 | scenario_b   | Scenario B - High Risk Proenca-a-Nova | 2026-04-18 12:31:27.187117+00 | 2026-04-18 12:31:27.311224+00 | 2026-04-18 12:40:57.761431+00 | 2020-09-13 10:00:00+00 |              30 |             20 |         12345 |      3 | {"sensor_count":6,"scenario_category":"HighRisk"}
 d8b5ea1c-7ee8-4c9d-a7ab-4ed3f7e04dfd | b3f4fb84-bf17-5522-a5f3-70fd1212f381 | 6034251c-b502-5f7b-a557-7187178cfb04 | c03be3d5-1f70-15cb-2fc0-3c86a204a644 | scenario_b   | Scenario B - High Risk Proenca-a-Nova | 2026-04-17 21:21:03.729291+00 | 2026-04-17 21:21:03.859747+00 | 2026-04-17 21:30:34.112524+00 | 2020-09-13 10:00:00+00 |              30 |             20 |         12345 |      3 | {"sensor_count":6,"scenario_category":"HighRisk"}
 db4ee3b3-d9b0-465b-b94a-c4a6dd5b98ed | b3f4fb84-bf17-5522-a5f3-70fd1212f381 | 6034251c-b502-5f7b-a557-7187178cfb04 | c03be3d5-1f70-15cb-2fc0-3c86a204a644 | scenario_b   | Scenario B - High Risk Proenca-a-Nova | 2026-04-17 19:48:09.757302+00 | 2026-04-17 19:48:09.891139+00 | 2026-04-17 19:56:03.578745+00 | 2020-09-13 10:00:00+00 |              30 |             20 |         12345 |      5 | {"sensor_count":6,"scenario_category":"HighRisk"}
 2a0547f8-04ca-4519-9cd0-84667a89270f | b3f4fb84-bf17-5522-a5f3-70fd1212f381 | 6034251c-b502-5f7b-a557-7187178cfb04 | c03be3d5-1f70-15cb-2fc0-3c86a204a644 | scenario_b   | Scenario B - High Risk Proenca-a-Nova | 2026-04-17 13:58:42.050864+00 | 2026-04-17 13:58:42.199056+00 | 2026-04-17 14:08:12.520451+00 | 2020-09-13 10:00:00+00 |              30 |             20 |         12345 |      3 | {"sensor_count":6,"scenario_category":"HighRisk"}
(20 rows)


```

## Pipeline totals

```sql
select
  (select count(*) from pipeline.event_inbox) as inbox_total,
  (select count(*) from pipeline.processing_attempts) as attempts_total,
  (select count(*) from pipeline.rejected_events) as rejected_total,
  (select count(*) from pipeline.quarantined_events) as quarantined_total;
```

```text
 inbox_total | attempts_total | rejected_total | quarantined_total 
-------------+----------------+----------------+-------------------
       87001 |          87266 |            148 |               100
(1 row)


```

## Inbox by status

```sql
select
  "Status",
  count(*) as count
from pipeline.event_inbox
group by "Status"
order by "Status";
```

```text
 Status | count 
--------+-------
      1 | 80304
      2 |  6597
      6 |   100
(3 rows)


```

## Inbox time range

```sql
select
  min("ReceivedAt") as first_inbox_received_at,
  max("ReceivedAt") as last_inbox_received_at,
  min("EventTime") as first_event_time,
  max("EventTime") as last_event_time
from pipeline.event_inbox;
```

```text
    first_inbox_received_at    |    last_inbox_received_at     |    first_event_time    |    last_event_time     
-------------------------------+-------------------------------+------------------------+------------------------
 2026-04-11 11:38:57.569963+00 | 2026-05-15 21:44:56.267822+00 | 2020-09-13 10:00:00+00 | 2020-09-13 10:23:55+00
(1 row)


```

## Latest inbox events

```sql
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
```

```text
                  Id                  |               EventId                |       EventType       |            Producer            | Status | AttemptCount |       EventTime        |          ReceivedAt           |         LastAttemptAt         |        LastProcessedAt        | LastErrorCode | LastErrorMessage | QuarantinedAt 
--------------------------------------+--------------------------------------+-----------------------+--------------------------------+--------+--------------+------------------------+-------------------------------+-------------------------------+-------------------------------+---------------+------------------+---------------
 69d867f3-7f94-412f-8b39-0ebcd2c184ae | 16cf048d-5d4a-47be-aae8-5f72e09531d3 | SensorReadingProduced | NatureProtector.Simulator.Host |      2 |            1 | 2020-09-13 10:00:20+00 | 2026-05-15 21:44:56.267822+00 | 2026-05-15 21:44:56.315433+00 | 2026-05-15 21:44:56.315433+00 | ├ó╦åÔÇª           | ├ó╦åÔÇª              | ├ó╦åÔÇª
 2be30eb5-53b9-4f8b-82fb-5effc2bb91ca | e6838c5e-fcf8-4673-848d-25ae334f7374 | SensorReadingProduced | NatureProtector.Simulator.Host |      2 |            1 | 2020-09-13 10:00:20+00 | 2026-05-15 21:44:56.215444+00 | 2026-05-15 21:44:56.260823+00 | 2026-05-15 21:44:56.260823+00 | ├ó╦åÔÇª           | ├ó╦åÔÇª              | ├ó╦åÔÇª
 bc01031f-22e6-4c60-80c5-9cbebd3308fa | b4c52802-1c73-4284-b225-58072d4fe93c | SensorReadingProduced | NatureProtector.Simulator.Host |      2 |            1 | 2020-09-13 10:00:20+00 | 2026-05-15 21:44:56.165839+00 | 2026-05-15 21:44:56.209338+00 | 2026-05-15 21:44:56.209338+00 | ├ó╦åÔÇª           | ├ó╦åÔÇª              | ├ó╦åÔÇª
 3da1801b-0cdd-40f4-9b41-cc117550f42f | a9a8f53e-f6b8-4070-8760-9d4918556456 | SensorReadingProduced | NatureProtector.Simulator.Host |      2 |            1 | 2020-09-13 10:00:20+00 | 2026-05-15 21:44:56.106618+00 | 2026-05-15 21:44:56.159421+00 | 2026-05-15 21:44:56.159421+00 | ├ó╦åÔÇª           | ├ó╦åÔÇª              | ├ó╦åÔÇª
 7a6e5a0a-f26b-4a8c-bdf8-4a494e276567 | 8da335a4-d553-4a46-8828-2c5cb1c55191 | SensorReadingProduced | NatureProtector.Simulator.Host |      2 |            1 | 2020-09-13 10:00:20+00 | 2026-05-15 21:44:56.050001+00 | 2026-05-15 21:44:56.099814+00 | 2026-05-15 21:44:56.099814+00 | ├ó╦åÔÇª           | ├ó╦åÔÇª              | ├ó╦åÔÇª
 f26170a5-bbc1-4c9a-83b9-670a0ddb4362 | a093e437-f178-4daa-9f21-645417fac748 | SensorReadingProduced | NatureProtector.Simulator.Host |      2 |            1 | 2020-09-13 10:00:20+00 | 2026-05-15 21:44:55.977781+00 | 2026-05-15 21:44:56.040834+00 | 2026-05-15 21:44:56.040834+00 | ├ó╦åÔÇª           | ├ó╦åÔÇª              | ├ó╦åÔÇª
 7189571e-f68c-438a-a711-33b13eea9138 | c7fcb2b5-e872-4dd8-ba66-3cabe970f7d0 | SensorReadingProduced | NatureProtector.Simulator.Host |      2 |            1 | 2020-09-13 10:00:15+00 | 2026-05-15 21:44:51.175315+00 | 2026-05-15 21:44:51.221096+00 | 2026-05-15 21:44:51.221096+00 | ├ó╦åÔÇª           | ├ó╦åÔÇª              | ├ó╦åÔÇª
 18e73495-be6d-465b-8702-3f6fd606408b | e401dbff-73c6-4cfa-baac-baa95daab3be | SensorReadingProduced | NatureProtector.Simulator.Host |      2 |            1 | 2020-09-13 10:00:15+00 | 2026-05-15 21:44:51.121257+00 | 2026-05-15 21:44:51.166625+00 | 2026-05-15 21:44:51.166625+00 | ├ó╦åÔÇª           | ├ó╦åÔÇª              | ├ó╦åÔÇª
 776f465c-42c2-49b6-b813-acc2fa5cf02c | 21b0a910-af9e-46e4-9884-925478ba3694 | SensorReadingProduced | NatureProtector.Simulator.Host |      2 |            1 | 2020-09-13 10:00:15+00 | 2026-05-15 21:44:51.066294+00 | 2026-05-15 21:44:51.114317+00 | 2026-05-15 21:44:51.114317+00 | ├ó╦åÔÇª           | ├ó╦åÔÇª              | ├ó╦åÔÇª
 cecacda3-adcc-41e4-a96d-ab5131972a6c | 99c5af0d-a4f5-4da0-a2ed-074dbdd40ea7 | SensorReadingProduced | NatureProtector.Simulator.Host |      2 |            1 | 2020-09-13 10:00:15+00 | 2026-05-15 21:44:51.008909+00 | 2026-05-15 21:44:51.057749+00 | 2026-05-15 21:44:51.057749+00 | ├ó╦åÔÇª           | ├ó╦åÔÇª              | ├ó╦åÔÇª
 87113195-c634-4c11-9986-95e43267f572 | 069c68a4-b515-4ae1-b79b-8258c595f080 | SensorReadingProduced | NatureProtector.Simulator.Host |      2 |            1 | 2020-09-13 10:00:15+00 | 2026-05-15 21:44:50.951215+00 | 2026-05-15 21:44:51.002647+00 | 2026-05-15 21:44:51.002647+00 | ├ó╦åÔÇª           | ├ó╦åÔÇª              | ├ó╦åÔÇª
 4535f50f-eb36-402a-99f2-daaa48c65b86 | b4a54df2-c2a1-4945-bc1c-929b4c453df1 | SensorReadingProduced | NatureProtector.Simulator.Host |      2 |            1 | 2020-09-13 10:00:10+00 | 2026-05-15 21:44:46.274742+00 | 2026-05-15 21:44:46.353746+00 | 2026-05-15 21:44:46.353746+00 | ├ó╦åÔÇª           | ├ó╦åÔÇª              | ├ó╦åÔÇª
 12b3826b-b58d-4941-b884-b5fcefc05435 | 088db1c0-c0bf-4f39-b714-eda0cfe9cc9e | SensorReadingProduced | NatureProtector.Simulator.Host |      2 |            1 | 2020-09-13 10:00:10+00 | 2026-05-15 21:44:46.195624+00 | 2026-05-15 21:44:46.261542+00 | 2026-05-15 21:44:46.261542+00 | ├ó╦åÔÇª           | ├ó╦åÔÇª              | ├ó╦åÔÇª
 047f3cca-4777-4e0c-9ebc-ebe996f8e5b7 | 4ffa59f9-2f84-4f60-afe0-5ee0dfdff43a | SensorReadingProduced | NatureProtector.Simulator.Host |      2 |            1 | 2020-09-13 10:00:10+00 | 2026-05-15 21:44:46.124664+00 | 2026-05-15 21:44:46.188218+00 | 2026-05-15 21:44:46.188218+00 | ├ó╦åÔÇª           | ├ó╦åÔÇª              | ├ó╦åÔÇª
 c0374dfd-974e-4a0e-b882-4f34afd17396 | 9f1e5e70-8b21-44d6-b3b7-8519b63998db | SensorReadingProduced | NatureProtector.Simulator.Host |      2 |            1 | 2020-09-13 10:00:10+00 | 2026-05-15 21:44:46.05223+00  | 2026-05-15 21:44:46.115219+00 | 2026-05-15 21:44:46.115219+00 | ├ó╦åÔÇª           | ├ó╦åÔÇª              | ├ó╦åÔÇª
 3ff9c392-4a44-47e7-b8fd-b3fdeb2e30ea | 30b5fae9-c208-4334-a7aa-d52a87284d74 | SensorReadingProduced | NatureProtector.Simulator.Host |      2 |            1 | 2020-09-13 10:00:10+00 | 2026-05-15 21:44:45.972856+00 | 2026-05-15 21:44:46.041988+00 | 2026-05-15 21:44:46.041988+00 | ├ó╦åÔÇª           | ├ó╦åÔÇª              | ├ó╦åÔÇª
 c816d398-f1dd-404f-b1ba-c71b6cfb0366 | 29c9199d-e22a-4613-83d8-83cdc575835f | SensorReadingProduced | NatureProtector.Simulator.Host |      2 |            1 | 2020-09-13 10:00:10+00 | 2026-05-15 21:44:45.909364+00 | 2026-05-15 21:44:45.963541+00 | 2026-05-15 21:44:45.963541+00 | ├ó╦åÔÇª           | ├ó╦åÔÇª              | ├ó╦åÔÇª
 d7d26655-50bf-42ed-a988-1f57a1873d31 | 305da371-35bd-438b-b040-efd77d4afe6b | SensorReadingProduced | NatureProtector.Simulator.Host |      2 |            1 | 2020-09-13 10:00:05+00 | 2026-05-15 21:44:41.203934+00 | 2026-05-15 21:44:41.260987+00 | 2026-05-15 21:44:41.260987+00 | ├ó╦åÔÇª           | ├ó╦åÔÇª              | ├ó╦åÔÇª
 6d266c31-44e8-48b1-8074-09145d821137 | 87cb0bc4-09f8-471e-8793-d43dac48eb09 | SensorReadingProduced | NatureProtector.Simulator.Host |      2 |            1 | 2020-09-13 10:00:05+00 | 2026-05-15 21:44:41.151187+00 | 2026-05-15 21:44:41.196513+00 | 2026-05-15 21:44:41.196513+00 | ├ó╦åÔÇª           | ├ó╦åÔÇª              | ├ó╦åÔÇª
 09c4b92c-994e-4ff0-8ff7-c660b07fde55 | 1dc90bcb-d2f8-4584-89a0-598208429c12 | SensorReadingProduced | NatureProtector.Simulator.Host |      2 |            1 | 2020-09-13 10:00:05+00 | 2026-05-15 21:44:41.090825+00 | 2026-05-15 21:44:41.143631+00 | 2026-05-15 21:44:41.143631+00 | ├ó╦åÔÇª           | ├ó╦åÔÇª              | ├ó╦åÔÇª
 c14c45e9-5a32-4383-a2e4-db85aa3808f9 | cb9e55ed-34b6-48d6-ab2b-1492bd5c9a19 | SensorReadingProduced | NatureProtector.Simulator.Host |      2 |            1 | 2020-09-13 10:00:05+00 | 2026-05-15 21:44:41.015748+00 | 2026-05-15 21:44:41.082392+00 | 2026-05-15 21:44:41.082392+00 | ├ó╦åÔÇª           | ├ó╦åÔÇª              | ├ó╦åÔÇª
 446efe6c-012c-4abb-a062-a91ed8eadd88 | 01c40599-e3a5-49dd-8615-102669f69eb8 | SensorReadingProduced | NatureProtector.Simulator.Host |      2 |            1 | 2020-09-13 10:00:05+00 | 2026-05-15 21:44:40.947605+00 | 2026-05-15 21:44:41.001417+00 | 2026-05-15 21:44:41.001417+00 | ├ó╦åÔÇª           | ├ó╦åÔÇª              | ├ó╦åÔÇª
 c4d26a75-77f6-43d0-bb93-50ffac117699 | 6bcee3ca-1425-4787-b9e1-8930d0219755 | SensorReadingProduced | NatureProtector.Simulator.Host |      2 |            1 | 2020-09-13 10:00:05+00 | 2026-05-15 21:44:40.871673+00 | 2026-05-15 21:44:40.935906+00 | 2026-05-15 21:44:40.935906+00 | ├ó╦åÔÇª           | ├ó╦åÔÇª              | ├ó╦åÔÇª
 23ff1b0f-1315-4940-83c0-8a45c2dbf041 | 8fb99fa7-f5d2-4ca2-bce1-66163bd55fb2 | SensorReadingProduced | NatureProtector.Simulator.Host |      2 |            1 | 2020-09-13 10:00:00+00 | 2026-05-15 21:44:36.142759+00 | 2026-05-15 21:44:36.196071+00 | 2026-05-15 21:44:36.196071+00 | ├ó╦åÔÇª           | ├ó╦åÔÇª              | ├ó╦åÔÇª
 2b332909-e67a-4563-b79a-5f91b03dea7b | fea86b3a-6ba7-4c0d-a303-2f70601e1bf7 | SensorReadingProduced | NatureProtector.Simulator.Host |      2 |            1 | 2020-09-13 10:00:00+00 | 2026-05-15 21:44:36.073928+00 | 2026-05-15 21:44:36.136038+00 | 2026-05-15 21:44:36.136038+00 | ├ó╦åÔÇª           | ├ó╦åÔÇª              | ├ó╦åÔÇª
(25 rows)


```

## Inbox errors

```sql
select
  "LastErrorCode",
  "LastErrorMessage",
  count(*) as count
from pipeline.event_inbox
where "LastErrorMessage" is not null
  and "LastErrorMessage" <> ''
group by "LastErrorCode", "LastErrorMessage"
order by count desc, "LastErrorCode";
```

```text
   LastErrorCode   |                             LastErrorMessage                             | count 
-------------------+--------------------------------------------------------------------------+-------
 processing_failed | The given key 'EmptyProjectionMember' was not present in the dictionary. |   102
(1 row)


```

## Latest processing attempts

```sql
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
```

```text
                  Id                  |             InboxEventId             | AttemptNumber |         Stage         |           StartedAt           |          FinishedAt           | Outcome | ErrorCode | ErrorMessage 
--------------------------------------+--------------------------------------+---------------+-----------------------+-------------------------------+-------------------------------+---------+-----------+--------------
 ffea45cb-ec51-49f9-8aea-307781821379 | 69d867f3-7f94-412f-8b39-0ebcd2c184ae |             1 | reading_risk_pipeline | 2026-05-15 21:44:56.267822+00 | 2026-05-15 21:44:56.315433+00 |       1 | ├ó╦åÔÇª       | ├ó╦åÔÇª
 2fa995ea-adb9-4cff-a164-f37b4bab6c6d | 2be30eb5-53b9-4f8b-82fb-5effc2bb91ca |             1 | reading_risk_pipeline | 2026-05-15 21:44:56.215444+00 | 2026-05-15 21:44:56.260823+00 |       1 | ├ó╦åÔÇª       | ├ó╦åÔÇª
 c1fd7643-732e-4639-ac2d-fca440116822 | bc01031f-22e6-4c60-80c5-9cbebd3308fa |             1 | reading_risk_pipeline | 2026-05-15 21:44:56.165839+00 | 2026-05-15 21:44:56.209338+00 |       1 | ├ó╦åÔÇª       | ├ó╦åÔÇª
 3af3ce55-7588-4313-8b3a-db8e1d38f987 | 3da1801b-0cdd-40f4-9b41-cc117550f42f |             1 | reading_risk_pipeline | 2026-05-15 21:44:56.106618+00 | 2026-05-15 21:44:56.159421+00 |       1 | ├ó╦åÔÇª       | ├ó╦åÔÇª
 49048163-0065-468b-82a5-d8d0773b47de | 7a6e5a0a-f26b-4a8c-bdf8-4a494e276567 |             1 | reading_risk_pipeline | 2026-05-15 21:44:56.050001+00 | 2026-05-15 21:44:56.099814+00 |       1 | ├ó╦åÔÇª       | ├ó╦åÔÇª
 408b0f12-7250-4df7-9ab1-34af7a6e01bc | f26170a5-bbc1-4c9a-83b9-670a0ddb4362 |             1 | reading_risk_pipeline | 2026-05-15 21:44:55.977781+00 | 2026-05-15 21:44:56.040834+00 |       1 | ├ó╦åÔÇª       | ├ó╦åÔÇª
 43944e50-bbec-4718-b8dc-0f7828b489c3 | 7189571e-f68c-438a-a711-33b13eea9138 |             1 | reading_risk_pipeline | 2026-05-15 21:44:51.175315+00 | 2026-05-15 21:44:51.221096+00 |       1 | ├ó╦åÔÇª       | ├ó╦åÔÇª
 f14318c2-2019-49f4-a112-9ee0e46d6d02 | 18e73495-be6d-465b-8702-3f6fd606408b |             1 | reading_risk_pipeline | 2026-05-15 21:44:51.121257+00 | 2026-05-15 21:44:51.166625+00 |       1 | ├ó╦åÔÇª       | ├ó╦åÔÇª
 62757566-5304-48a0-ab8d-4ad1b5071aaa | 776f465c-42c2-49b6-b813-acc2fa5cf02c |             1 | reading_risk_pipeline | 2026-05-15 21:44:51.066294+00 | 2026-05-15 21:44:51.114317+00 |       1 | ├ó╦åÔÇª       | ├ó╦åÔÇª
 3e5a6505-5216-4674-baf5-0907826a2ec0 | cecacda3-adcc-41e4-a96d-ab5131972a6c |             1 | reading_risk_pipeline | 2026-05-15 21:44:51.008909+00 | 2026-05-15 21:44:51.057749+00 |       1 | ├ó╦åÔÇª       | ├ó╦åÔÇª
 0c24b0d9-4340-482f-9d44-c01bd72b6b28 | 87113195-c634-4c11-9986-95e43267f572 |             1 | reading_risk_pipeline | 2026-05-15 21:44:50.951215+00 | 2026-05-15 21:44:51.002647+00 |       1 | ├ó╦åÔÇª       | ├ó╦åÔÇª
 251beba1-8ba6-4823-aae2-e9d767fab936 | 4535f50f-eb36-402a-99f2-daaa48c65b86 |             1 | reading_risk_pipeline | 2026-05-15 21:44:46.274742+00 | 2026-05-15 21:44:46.353746+00 |       1 | ├ó╦åÔÇª       | ├ó╦åÔÇª
 53fe3510-30b0-4101-bce0-8e713a252c7a | 12b3826b-b58d-4941-b884-b5fcefc05435 |             1 | reading_risk_pipeline | 2026-05-15 21:44:46.195624+00 | 2026-05-15 21:44:46.261542+00 |       1 | ├ó╦åÔÇª       | ├ó╦åÔÇª
 f56a4459-d1ff-45d9-ada0-4156d129e547 | 047f3cca-4777-4e0c-9ebc-ebe996f8e5b7 |             1 | reading_risk_pipeline | 2026-05-15 21:44:46.124664+00 | 2026-05-15 21:44:46.188218+00 |       1 | ├ó╦åÔÇª       | ├ó╦åÔÇª
 13729d74-4b9f-4172-a0cf-9203aa00b217 | c0374dfd-974e-4a0e-b882-4f34afd17396 |             1 | reading_risk_pipeline | 2026-05-15 21:44:46.05223+00  | 2026-05-15 21:44:46.115219+00 |       1 | ├ó╦åÔÇª       | ├ó╦åÔÇª
 12a5705d-f0ba-4bbd-ba28-2b4bed323837 | 3ff9c392-4a44-47e7-b8fd-b3fdeb2e30ea |             1 | reading_risk_pipeline | 2026-05-15 21:44:45.972856+00 | 2026-05-15 21:44:46.041988+00 |       1 | ├ó╦åÔÇª       | ├ó╦åÔÇª
 57d48557-b71e-4d98-b257-5da79f9277d7 | c816d398-f1dd-404f-b1ba-c71b6cfb0366 |             1 | reading_risk_pipeline | 2026-05-15 21:44:45.909364+00 | 2026-05-15 21:44:45.963541+00 |       1 | ├ó╦åÔÇª       | ├ó╦åÔÇª
 429399b3-0705-45cd-9a51-880db372608a | d7d26655-50bf-42ed-a988-1f57a1873d31 |             1 | reading_risk_pipeline | 2026-05-15 21:44:41.203934+00 | 2026-05-15 21:44:41.260987+00 |       1 | ├ó╦åÔÇª       | ├ó╦åÔÇª
 a92639d3-8885-4230-8402-57330c205b0d | 6d266c31-44e8-48b1-8074-09145d821137 |             1 | reading_risk_pipeline | 2026-05-15 21:44:41.151187+00 | 2026-05-15 21:44:41.196513+00 |       1 | ├ó╦åÔÇª       | ├ó╦åÔÇª
 566ba991-8685-4143-b995-25713438ef93 | 09c4b92c-994e-4ff0-8ff7-c660b07fde55 |             1 | reading_risk_pipeline | 2026-05-15 21:44:41.090825+00 | 2026-05-15 21:44:41.143631+00 |       1 | ├ó╦åÔÇª       | ├ó╦åÔÇª
 82ac7feb-cd12-4cf6-9a21-54ddec14d333 | c14c45e9-5a32-4383-a2e4-db85aa3808f9 |             1 | reading_risk_pipeline | 2026-05-15 21:44:41.015748+00 | 2026-05-15 21:44:41.082392+00 |       1 | ├ó╦åÔÇª       | ├ó╦åÔÇª
 34ed5e01-9a3e-42c0-8412-3cbe7648d468 | 446efe6c-012c-4abb-a062-a91ed8eadd88 |             1 | reading_risk_pipeline | 2026-05-15 21:44:40.947605+00 | 2026-05-15 21:44:41.001417+00 |       1 | ├ó╦åÔÇª       | ├ó╦åÔÇª
 80f6124b-476c-419b-b971-9d058057b680 | c4d26a75-77f6-43d0-bb93-50ffac117699 |             1 | reading_risk_pipeline | 2026-05-15 21:44:40.871673+00 | 2026-05-15 21:44:40.935906+00 |       1 | ├ó╦åÔÇª       | ├ó╦åÔÇª
 429db434-f1d7-429e-b061-2c3a6a4b6ed6 | 23ff1b0f-1315-4940-83c0-8a45c2dbf041 |             1 | reading_risk_pipeline | 2026-05-15 21:44:36.142759+00 | 2026-05-15 21:44:36.196071+00 |       1 | ├ó╦åÔÇª       | ├ó╦åÔÇª
 e52cf123-6cc8-4be8-8200-6e010fe61b84 | 2b332909-e67a-4563-b79a-5f91b03dea7b |             1 | reading_risk_pipeline | 2026-05-15 21:44:36.073928+00 | 2026-05-15 21:44:36.136038+00 |       1 | ├ó╦åÔÇª       | ├ó╦åÔÇª
 246efc4e-9efb-4adc-b4a0-2a01452d05c9 | 0e97a7d3-4c59-4b93-9dab-002b3f920d59 |             1 | reading_risk_pipeline | 2026-05-15 21:44:36.019372+00 | 2026-05-15 21:44:36.064135+00 |       1 | ├ó╦åÔÇª       | ├ó╦åÔÇª
 1a561e31-9718-4939-af16-f938827f1303 | 2261699f-2a67-4326-90bb-aa7ec97f7e1d |             1 | reading_risk_pipeline | 2026-05-15 21:44:35.967037+00 | 2026-05-15 21:44:36.013098+00 |       1 | ├ó╦åÔÇª       | ├ó╦åÔÇª
 2afae7a4-1274-4d42-a260-a97f5aefca42 | 871ca12b-3331-4d63-ac36-dab71676310b |             1 | reading_risk_pipeline | 2026-05-15 21:44:35.911416+00 | 2026-05-15 21:44:35.956894+00 |       1 | ├ó╦åÔÇª       | ├ó╦åÔÇª
 a1be8502-7e8f-4942-8e74-ace527086353 | 8abd936b-39eb-48a9-81cf-ed84f49b4050 |             1 | reading_risk_pipeline | 2026-05-15 21:44:35.844302+00 | 2026-05-15 21:44:35.904575+00 |       1 | ├ó╦åÔÇª       | ├ó╦åÔÇª
 d062b5ea-b9cf-418c-b647-05357c3e576c | 42be937f-4576-471f-a862-13f765aaeae6 |             1 | reading_risk_pipeline | 2026-05-15 20:01:36.876637+00 | 2026-05-15 20:01:36.932839+00 |       1 | ├ó╦åÔÇª       | ├ó╦åÔÇª
 fad8581a-075a-47cd-8bdc-76bcf980f12e | be0f32c6-6728-4536-9b84-b5fda2a93555 |             1 | reading_risk_pipeline | 2026-05-15 20:01:36.806858+00 | 2026-05-15 20:01:36.868694+00 |       1 | ├ó╦åÔÇª       | ├ó╦åÔÇª
 80ca3b11-2980-4762-a323-4f49d208c621 | 7d3879e0-16c9-4de2-9771-916041832ef4 |             1 | reading_risk_pipeline | 2026-05-15 20:01:36.738588+00 | 2026-05-15 20:01:36.795804+00 |       1 | ├ó╦åÔÇª       | ├ó╦åÔÇª
 2e27330c-8880-4d6a-b9db-0667870ff214 | 0535bb22-9c80-49a3-9bb2-5cc47e25e71a |             1 | reading_risk_pipeline | 2026-05-15 20:01:36.663905+00 | 2026-05-15 20:01:36.728216+00 |       1 | ├ó╦åÔÇª       | ├ó╦åÔÇª
 0271b390-635d-4e15-ac91-a975718738cb | f4e81f72-2dee-40e0-ac85-86027801f66e |             1 | reading_risk_pipeline | 2026-05-15 20:01:36.594135+00 | 2026-05-15 20:01:36.653865+00 |       1 | ├ó╦åÔÇª       | ├ó╦åÔÇª
 de74c7f6-4220-4005-877d-01144db5bbce | 04a34856-125b-46e1-8745-e431f8b40f78 |             1 | reading_risk_pipeline | 2026-05-15 20:01:36.534306+00 | 2026-05-15 20:01:36.587213+00 |       1 | ├ó╦åÔÇª       | ├ó╦åÔÇª
 cb726468-78bb-4965-828f-1d9a3c1b78ad | ca273c28-467c-4e7e-aea0-874fa130dfc8 |             1 | reading_risk_pipeline | 2026-05-15 20:01:31.801227+00 | 2026-05-15 20:01:31.85532+00  |       1 | ├ó╦åÔÇª       | ├ó╦åÔÇª
 f8ceb690-d2cb-44f2-a3b7-19477557a734 | 2b601e9f-36ab-4ec5-b379-8e7558f1ecda |             1 | reading_risk_pipeline | 2026-05-15 20:01:31.71253+00  | 2026-05-15 20:01:31.789087+00 |       1 | ├ó╦åÔÇª       | ├ó╦åÔÇª
 a48441ec-8c89-45ca-9ab2-765fd0806c0c | 07e2b624-25d2-4e44-b0c8-5e749f8497a0 |             1 | reading_risk_pipeline | 2026-05-15 20:01:31.650016+00 | 2026-05-15 20:01:31.703578+00 |       1 | ├ó╦åÔÇª       | ├ó╦åÔÇª
 5e4906f4-ed86-403a-9540-c96be2f67c90 | ea76f900-0d64-42c2-893a-9380f24db569 |             1 | reading_risk_pipeline | 2026-05-15 20:01:31.575925+00 | 2026-05-15 20:01:31.639623+00 |       1 | ├ó╦åÔÇª       | ├ó╦åÔÇª
 47b9539a-62ba-49d4-b238-54c528da9bd2 | ac5f71e3-d02d-41ea-a39d-6c4373f3196f |             1 | reading_risk_pipeline | 2026-05-15 20:01:31.498184+00 | 2026-05-15 20:01:31.568941+00 |       1 | ├ó╦åÔÇª       | ├ó╦åÔÇª
 5cee131a-cda3-41ef-8d12-84b5759f813d | 6efc4f6f-b8c9-47a7-b48a-68cd35eca18a |             1 | reading_risk_pipeline | 2026-05-15 20:01:26.7514+00   | 2026-05-15 20:01:26.797656+00 |       1 | ├ó╦åÔÇª       | ├ó╦åÔÇª
 d449ba8e-2774-4f65-bd8d-737f732098e3 | 2275dd46-458c-47f8-bc76-4ee6db70f91a |             1 | reading_risk_pipeline | 2026-05-15 20:01:26.699684+00 | 2026-05-15 20:01:26.744916+00 |       1 | ├ó╦åÔÇª       | ├ó╦åÔÇª
 3a1dee81-5ecc-414d-9fba-aeac8b1f4320 | a9415eb9-3e21-4133-9738-fff83f8dbd9a |             1 | reading_risk_pipeline | 2026-05-15 20:01:26.648427+00 | 2026-05-15 20:01:26.692761+00 |       1 | ├ó╦åÔÇª       | ├ó╦åÔÇª
 f8f7bf56-69d0-44de-be43-2efcec050435 | 1e1b2295-bcc7-46c8-a2c6-078174c410bd |             1 | reading_risk_pipeline | 2026-05-15 20:01:26.593141+00 | 2026-05-15 20:01:26.640342+00 |       1 | ├ó╦åÔÇª       | ├ó╦åÔÇª
 2dbabd95-7317-48ea-b1d3-970a96ff3807 | b082235f-de27-41af-a38c-2ae1554f13aa |             1 | reading_risk_pipeline | 2026-05-15 20:01:26.526458+00 | 2026-05-15 20:01:26.585987+00 |       1 | ├ó╦åÔÇª       | ├ó╦åÔÇª
 2d5dae03-508b-439c-aa1c-8b644baff579 | b6dfb5b9-6d32-4d29-be61-2aba25dd6516 |             1 | reading_risk_pipeline | 2026-05-15 20:01:26.458258+00 | 2026-05-15 20:01:26.520137+00 |       1 | ├ó╦åÔÇª       | ├ó╦åÔÇª
 c7fadd38-8c49-4883-8869-934ec4ba1792 | 8680838f-db68-48a2-8b01-b3a051e44c03 |             1 | reading_risk_pipeline | 2026-05-15 20:01:21.723503+00 | 2026-05-15 20:01:21.788897+00 |       1 | ├ó╦åÔÇª       | ├ó╦åÔÇª
 70e4b54c-ed75-4930-adea-b1f9067be98b | 5bde7e74-c830-45b9-971b-952da404d943 |             1 | reading_risk_pipeline | 2026-05-15 20:01:21.658722+00 | 2026-05-15 20:01:21.71366+00  |       1 | ├ó╦åÔÇª       | ├ó╦åÔÇª
 be766b3d-116a-483f-bb2f-86129b0fe726 | 4e8475d9-ecfd-4fe8-a9e1-5b0f8a6d4765 |             1 | reading_risk_pipeline | 2026-05-15 20:01:21.600765+00 | 2026-05-15 20:01:21.647489+00 |       1 | ├ó╦åÔÇª       | ├ó╦åÔÇª
 c2971c36-fc1e-4d29-b8a0-982d8e40ee74 | 52d1284c-1e6c-4d3e-a61a-5da9b2825cc2 |             1 | reading_risk_pipeline | 2026-05-15 20:01:21.542249+00 | 2026-05-15 20:01:21.593876+00 |       1 | ├ó╦åÔÇª       | ├ó╦åÔÇª
(50 rows)


```

## Processing attempt errors

```sql
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
```

```text
         Stage         | Outcome |     ErrorCode     |                               ErrorMessage                               | count 
-----------------------+---------+-------------------+--------------------------------------------------------------------------+-------
 reading_risk_pipeline |       3 | processing_failed | The given key 'EmptyProjectionMember' was not present in the dictionary. |   265
 reading_risk_pipeline |       4 | processing_failed | The given key 'EmptyProjectionMember' was not present in the dictionary. |   100
(2 rows)


```

## Rejected events summary

```sql
select
  "RejectionCode",
  "RejectionReason",
  count(*) as count,
  min("RejectedAt") as first_rejected_at,
  max("RejectedAt") as last_rejected_at
from pipeline.rejected_events
group by "RejectionCode", "RejectionReason"
order by max("RejectedAt") desc, count desc;
```

```text
       RejectionCode       |                              RejectionReason                              | count |      first_rejected_at       |       last_rejected_at        
---------------------------+---------------------------------------------------------------------------+-------+------------------------------+-------------------------------
 invalid_operational_state | OperationalState 'Invalid' is rejected before the accepted-risk pipeline. |   148 | 2026-04-17 12:07:44.04663+00 | 2026-05-15 21:44:50.946649+00
(1 row)


```

## Latest rejected events

```sql
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
```

```text
                  Id                  | InboxEventId | EventId |       RejectionCode       |                              RejectionReason                              |          RejectedAt           |                                                                                                                                                                                                                                                   raw_body_sample                                                                                                                                                                                                                                                    |                                                                                                                                                                                                                                           MetadataJson                                                                                                                                                                                                                                            
--------------------------------------+--------------+---------+---------------------------+---------------------------------------------------------------------------+-------------------------------+----------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------+---------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------
 8f3d9c81-6218-400b-898b-77e9fad68342 | ├ó╦åÔÇª          | ├ó╦åÔÇª     | invalid_operational_state | OperationalState 'Invalid' is rejected before the accepted-risk pipeline. | 2026-05-15 21:44:50.946649+00 | {"schemaVersion":"1.0","eventId":"156803b6-c956-4df5-b78c-44073824d5df","correlationId":"28bdba5121594aef91eab746b0939f16-0003-22702eee75aa3cf431abb689bfb9a8bc","producer":"NatureProtector.Simulator.Host","eventType":"SensorReadingProduced","areaId":"b3f4fb84-bf17-5522-a5f3-70fd1212f381","eventTime":"2020-09-13T12:00:15+02:00","payload":{"simulationRunId":"28bdba51-2159-4aef-91ea-b746b0939f16","sensorId":"22702eee-75aa-3cf4-31ab-b689bfb9a8bc","sensorName":"pilot-humidity-0001","metricType":"Humi | {"EventId":"156803b6-c956-4df5-b78c-44073824d5df","CorrelationId":"28bdba5121594aef91eab746b0939f16-0003-22702eee75aa3cf431abb689bfb9a8bc","Producer":"NatureProtector.Simulator.Host","EventType":"SensorReadingProduced","AreaId":"b3f4fb84-bf17-5522-a5f3-70fd1212f381","SchemaVersion":"1.0","SensorId":"22702eee-75aa-3cf4-31ab-b689bfb9a8bc","SensorName":"pilot-humidity-0001","MetricType":"Humidity","OperationalState":"Invalid","Stage":"pre_inbox_validation","DeliveryTag":229}
 edae7a17-ff99-4284-b0c5-87c1f46c79fa | ├ó╦åÔÇª          | ├ó╦åÔÇª     | invalid_operational_state | OperationalState 'Invalid' is rejected before the accepted-risk pipeline. | 2026-05-15 20:01:31.492987+00 | {"schemaVersion":"1.0","eventId":"de62d7b5-acaf-4585-9a91-e8316fa6f266","correlationId":"0f7892f3749f43319eed15962d697035-0003-22702eee75aa3cf431abb689bfb9a8bc","producer":"NatureProtector.Simulator.Host","eventType":"SensorReadingProduced","areaId":"b3f4fb84-bf17-5522-a5f3-70fd1212f381","eventTime":"2020-09-13T12:00:15+02:00","payload":{"simulationRunId":"0f7892f3-749f-4331-9eed-15962d697035","sensorId":"22702eee-75aa-3cf4-31ab-b689bfb9a8bc","sensorName":"pilot-humidity-0001","metricType":"Humi | {"EventId":"de62d7b5-acaf-4585-9a91-e8316fa6f266","CorrelationId":"0f7892f3749f43319eed15962d697035-0003-22702eee75aa3cf431abb689bfb9a8bc","Producer":"NatureProtector.Simulator.Host","EventType":"SensorReadingProduced","AreaId":"b3f4fb84-bf17-5522-a5f3-70fd1212f381","SchemaVersion":"1.0","SensorId":"22702eee-75aa-3cf4-31ab-b689bfb9a8bc","SensorName":"pilot-humidity-0001","MetricType":"Humidity","OperationalState":"Invalid","Stage":"pre_inbox_validation","DeliveryTag":199}
 ebf3fde6-0ba5-4dd9-af6a-b972a73e2661 | ├ó╦åÔÇª          | ├ó╦åÔÇª     | invalid_operational_state | OperationalState 'Invalid' is rejected before the accepted-risk pipeline. | 2026-05-15 19:48:08.38325+00  | {"schemaVersion":"1.0","eventId":"1faf5a57-bd8a-45a5-a2ab-10fd40dee6fc","correlationId":"c8a664e0814540f9bb00aaf5a51ebec0-0003-22702eee75aa3cf431abb689bfb9a8bc","producer":"NatureProtector.Simulator.Host","eventType":"SensorReadingProduced","areaId":"b3f4fb84-bf17-5522-a5f3-70fd1212f381","eventTime":"2020-09-13T12:00:15+02:00","payload":{"simulationRunId":"c8a664e0-8145-40f9-bb00-aaf5a51ebec0","sensorId":"22702eee-75aa-3cf4-31ab-b689bfb9a8bc","sensorName":"pilot-humidity-0001","metricType":"Humi | {"EventId":"1faf5a57-bd8a-45a5-a2ab-10fd40dee6fc","CorrelationId":"c8a664e0814540f9bb00aaf5a51ebec0-0003-22702eee75aa3cf431abb689bfb9a8bc","Producer":"NatureProtector.Simulator.Host","EventType":"SensorReadingProduced","AreaId":"b3f4fb84-bf17-5522-a5f3-70fd1212f381","SchemaVersion":"1.0","SensorId":"22702eee-75aa-3cf4-31ab-b689bfb9a8bc","SensorName":"pilot-humidity-0001","MetricType":"Humidity","OperationalState":"Invalid","Stage":"pre_inbox_validation","DeliveryTag":169}
 21535286-5bd8-40f7-89ac-42ebe9e8083d | ├ó╦åÔÇª          | ├ó╦åÔÇª     | invalid_operational_state | OperationalState 'Invalid' is rejected before the accepted-risk pipeline. | 2026-05-15 19:33:50.833011+00 | {"schemaVersion":"1.0","eventId":"c239f959-f69c-4df6-abee-50f411c0b9a1","correlationId":"2f8bf6cc4ffe4f64ada7fe8e3380ae67-0003-22702eee75aa3cf431abb689bfb9a8bc","producer":"NatureProtector.Simulator.Host","eventType":"SensorReadingProduced","areaId":"b3f4fb84-bf17-5522-a5f3-70fd1212f381","eventTime":"2020-09-13T12:00:15+02:00","payload":{"simulationRunId":"2f8bf6cc-4ffe-4f64-ada7-fe8e3380ae67","sensorId":"22702eee-75aa-3cf4-31ab-b689bfb9a8bc","sensorName":"pilot-humidity-0001","metricType":"Humi | {"EventId":"c239f959-f69c-4df6-abee-50f411c0b9a1","CorrelationId":"2f8bf6cc4ffe4f64ada7fe8e3380ae67-0003-22702eee75aa3cf431abb689bfb9a8bc","Producer":"NatureProtector.Simulator.Host","EventType":"SensorReadingProduced","AreaId":"b3f4fb84-bf17-5522-a5f3-70fd1212f381","SchemaVersion":"1.0","SensorId":"22702eee-75aa-3cf4-31ab-b689bfb9a8bc","SensorName":"pilot-humidity-0001","MetricType":"Humidity","OperationalState":"Invalid","Stage":"pre_inbox_validation","DeliveryTag":139}
 6da88e9d-a99c-4333-8c68-3bdebf64a723 | ├ó╦åÔÇª          | ├ó╦åÔÇª     | invalid_operational_state | OperationalState 'Invalid' is rejected before the accepted-risk pipeline. | 2026-05-15 18:49:23.390119+00 | {"schemaVersion":"1.0","eventId":"10a7fb45-461c-4890-a06a-1cac66bff71d","correlationId":"9b937806b2a246fb9a55a99f6d29235b-0017-2f494582407786f29707780861949155","producer":"NatureProtector.Simulator.Host","eventType":"SensorReadingProduced","areaId":"b3f4fb84-bf17-5522-a5f3-70fd1212f381","eventTime":"2020-09-13T12:08:30+02:00","payload":{"simulationRunId":"9b937806-b2a2-46fb-9a55-a99f6d29235b","sensorId":"2f494582-4077-86f2-9707-780861949155","sensorName":"pilot-humidity-0230","metricType":"Humi | {"EventId":"10a7fb45-461c-4890-a06a-1cac66bff71d","CorrelationId":"9b937806b2a246fb9a55a99f6d29235b-0017-2f494582407786f29707780861949155","Producer":"NatureProtector.Simulator.Host","EventType":"SensorReadingProduced","AreaId":"b3f4fb84-bf17-5522-a5f3-70fd1212f381","SchemaVersion":"1.0","SensorId":"2f494582-4077-86f2-9707-780861949155","SensorName":"pilot-humidity-0230","MetricType":"Humidity","OperationalState":"Invalid","Stage":"pre_inbox_validation","DeliveryTag":104}
 7526946c-206e-4eca-9f68-02e5b526a9fc | ├ó╦åÔÇª          | ├ó╦åÔÇª     | invalid_operational_state | OperationalState 'Invalid' is rejected before the accepted-risk pipeline. | 2026-05-15 18:47:53.223969+00 | {"schemaVersion":"1.0","eventId":"20f351fd-b1c6-4379-8e35-ff3d68ddd757","correlationId":"9b937806b2a246fb9a55a99f6d29235b-0014-22702eee75aa3cf431abb689bfb9a8bc","producer":"NatureProtector.Simulator.Host","eventType":"SensorReadingProduced","areaId":"b3f4fb84-bf17-5522-a5f3-70fd1212f381","eventTime":"2020-09-13T12:07:00+02:00","payload":{"simulationRunId":"9b937806-b2a2-46fb-9a55-a99f6d29235b","sensorId":"22702eee-75aa-3cf4-31ab-b689bfb9a8bc","sensorName":"pilot-humidity-0001","metricType":"Humi | {"EventId":"20f351fd-b1c6-4379-8e35-ff3d68ddd757","CorrelationId":"9b937806b2a246fb9a55a99f6d29235b-0014-22702eee75aa3cf431abb689bfb9a8bc","Producer":"NatureProtector.Simulator.Host","EventType":"SensorReadingProduced","AreaId":"b3f4fb84-bf17-5522-a5f3-70fd1212f381","SchemaVersion":"1.0","SensorId":"22702eee-75aa-3cf4-31ab-b689bfb9a8bc","SensorName":"pilot-humidity-0001","MetricType":"Humidity","OperationalState":"Invalid","Stage":"pre_inbox_validation","DeliveryTag":85}
 e3dfb474-5836-483a-86e7-eea5b211928b | ├ó╦åÔÇª          | ├ó╦åÔÇª     | invalid_operational_state | OperationalState 'Invalid' is rejected before the accepted-risk pipeline. | 2026-05-15 18:43:23.038843+00 | {"schemaVersion":"1.0","eventId":"f556f64f-8a51-406b-9849-fc9b513e45dd","correlationId":"9b937806b2a246fb9a55a99f6d29235b-0005-27bf9aad094f7dc645b2129a8cc0522e","producer":"NatureProtector.Simulator.Host","eventType":"SensorReadingProduced","areaId":"b3f4fb84-bf17-5522-a5f3-70fd1212f381","eventTime":"2020-09-13T12:02:30+02:00","payload":{"simulationRunId":"9b937806-b2a2-46fb-9a55-a99f6d29235b","sensorId":"27bf9aad-094f-7dc6-45b2-129a8cc0522e","sensorName":"pilot-temperature-0001","metricType":"T | {"EventId":"f556f64f-8a51-406b-9849-fc9b513e45dd","CorrelationId":"9b937806b2a246fb9a55a99f6d29235b-0005-27bf9aad094f7dc645b2129a8cc0522e","Producer":"NatureProtector.Simulator.Host","EventType":"SensorReadingProduced","AreaId":"b3f4fb84-bf17-5522-a5f3-70fd1212f381","SchemaVersion":"1.0","SensorId":"27bf9aad-094f-7dc6-45b2-129a8cc0522e","SensorName":"pilot-temperature-0001","MetricType":"Temperature","OperationalState":"Invalid","Stage":"pre_inbox_validation","DeliveryTag":33}
 1332d977-4403-4318-a8d7-9b846010cdd9 | ├ó╦åÔÇª          | ├ó╦åÔÇª     | invalid_operational_state | OperationalState 'Invalid' is rejected before the accepted-risk pipeline. | 2026-05-15 18:42:22.846608+00 | {"schemaVersion":"1.0","eventId":"bf0ea8e4-e2f9-4184-84db-d1c1a21335d0","correlationId":"9b937806b2a246fb9a55a99f6d29235b-0003-22702eee75aa3cf431abb689bfb9a8bc","producer":"NatureProtector.Simulator.Host","eventType":"SensorReadingProduced","areaId":"b3f4fb84-bf17-5522-a5f3-70fd1212f381","eventTime":"2020-09-13T12:01:30+02:00","payload":{"simulationRunId":"9b937806-b2a2-46fb-9a55-a99f6d29235b","sensorId":"22702eee-75aa-3cf4-31ab-b689bfb9a8bc","sensorName":"pilot-humidity-0001","metricType":"Humi | {"EventId":"bf0ea8e4-e2f9-4184-84db-d1c1a21335d0","CorrelationId":"9b937806b2a246fb9a55a99f6d29235b-0003-22702eee75aa3cf431abb689bfb9a8bc","Producer":"NatureProtector.Simulator.Host","EventType":"SensorReadingProduced","AreaId":"b3f4fb84-bf17-5522-a5f3-70fd1212f381","SchemaVersion":"1.0","SensorId":"22702eee-75aa-3cf4-31ab-b689bfb9a8bc","SensorName":"pilot-humidity-0001","MetricType":"Humidity","OperationalState":"Invalid","Stage":"pre_inbox_validation","DeliveryTag":19}
 59319604-edd8-409f-90fa-5810a07beda9 | ├ó╦åÔÇª          | ├ó╦åÔÇª     | invalid_operational_state | OperationalState 'Invalid' is rejected before the accepted-risk pipeline. | 2026-05-15 18:22:47.802181+00 | {"schemaVersion":"1.0","eventId":"6057a3a0-8a3e-4b04-ba08-d1a22a8e5066","correlationId":"a44215a714cf48a99a885d0fb052fdfd-0017-2f494582407786f29707780861949155","producer":"NatureProtector.Simulator.Host","eventType":"SensorReadingProduced","areaId":"b3f4fb84-bf17-5522-a5f3-70fd1212f381","eventTime":"2020-09-13T12:08:30+02:00","payload":{"simulationRunId":"a44215a7-14cf-48a9-9a88-5d0fb052fdfd","sensorId":"2f494582-4077-86f2-9707-780861949155","sensorName":"pilot-humidity-0230","metricType":"Humi | {"EventId":"6057a3a0-8a3e-4b04-ba08-d1a22a8e5066","CorrelationId":"a44215a714cf48a99a885d0fb052fdfd-0017-2f494582407786f29707780861949155","Producer":"NatureProtector.Simulator.Host","EventType":"SensorReadingProduced","AreaId":"b3f4fb84-bf17-5522-a5f3-70fd1212f381","SchemaVersion":"1.0","SensorId":"2f494582-4077-86f2-9707-780861949155","SensorName":"pilot-humidity-0230","MetricType":"Humidity","OperationalState":"Invalid","Stage":"pre_inbox_validation","DeliveryTag":104}
 a885abfe-289b-48e5-ae6a-7f6353abe2cc | ├ó╦åÔÇª          | ├ó╦åÔÇª     | invalid_operational_state | OperationalState 'Invalid' is rejected before the accepted-risk pipeline. | 2026-05-15 18:21:17.64931+00  | {"schemaVersion":"1.0","eventId":"24f9b826-9930-41d7-9afc-e3066457713f","correlationId":"a44215a714cf48a99a885d0fb052fdfd-0014-22702eee75aa3cf431abb689bfb9a8bc","producer":"NatureProtector.Simulator.Host","eventType":"SensorReadingProduced","areaId":"b3f4fb84-bf17-5522-a5f3-70fd1212f381","eventTime":"2020-09-13T12:07:00+02:00","payload":{"simulationRunId":"a44215a7-14cf-48a9-9a88-5d0fb052fdfd","sensorId":"22702eee-75aa-3cf4-31ab-b689bfb9a8bc","sensorName":"pilot-humidity-0001","metricType":"Humi | {"EventId":"24f9b826-9930-41d7-9afc-e3066457713f","CorrelationId":"a44215a714cf48a99a885d0fb052fdfd-0014-22702eee75aa3cf431abb689bfb9a8bc","Producer":"NatureProtector.Simulator.Host","EventType":"SensorReadingProduced","AreaId":"b3f4fb84-bf17-5522-a5f3-70fd1212f381","SchemaVersion":"1.0","SensorId":"22702eee-75aa-3cf4-31ab-b689bfb9a8bc","SensorName":"pilot-humidity-0001","MetricType":"Humidity","OperationalState":"Invalid","Stage":"pre_inbox_validation","DeliveryTag":85}
 7ebd75f8-5912-4e5b-9b4c-53159d10cf10 | ├ó╦åÔÇª          | ├ó╦åÔÇª     | invalid_operational_state | OperationalState 'Invalid' is rejected before the accepted-risk pipeline. | 2026-05-15 18:16:47.451151+00 | {"schemaVersion":"1.0","eventId":"c01b352b-3ae1-44fd-977b-cb1bb885da78","correlationId":"a44215a714cf48a99a885d0fb052fdfd-0005-27bf9aad094f7dc645b2129a8cc0522e","producer":"NatureProtector.Simulator.Host","eventType":"SensorReadingProduced","areaId":"b3f4fb84-bf17-5522-a5f3-70fd1212f381","eventTime":"2020-09-13T12:02:30+02:00","payload":{"simulationRunId":"a44215a7-14cf-48a9-9a88-5d0fb052fdfd","sensorId":"27bf9aad-094f-7dc6-45b2-129a8cc0522e","sensorName":"pilot-temperature-0001","metricType":"T | {"EventId":"c01b352b-3ae1-44fd-977b-cb1bb885da78","CorrelationId":"a44215a714cf48a99a885d0fb052fdfd-0005-27bf9aad094f7dc645b2129a8cc0522e","Producer":"NatureProtector.Simulator.Host","EventType":"SensorReadingProduced","AreaId":"b3f4fb84-bf17-5522-a5f3-70fd1212f381","SchemaVersion":"1.0","SensorId":"27bf9aad-094f-7dc6-45b2-129a8cc0522e","SensorName":"pilot-temperature-0001","MetricType":"Temperature","OperationalState":"Invalid","Stage":"pre_inbox_validation","DeliveryTag":33}
 47f76b6c-4103-466b-85fe-e26a8d87dd61 | ├ó╦åÔÇª          | ├ó╦åÔÇª     | invalid_operational_state | OperationalState 'Invalid' is rejected before the accepted-risk pipeline. | 2026-05-15 18:15:47.268031+00 | {"schemaVersion":"1.0","eventId":"027d8bef-70dd-4fcd-a3f6-0174adfc0c41","correlationId":"a44215a714cf48a99a885d0fb052fdfd-0003-22702eee75aa3cf431abb689bfb9a8bc","producer":"NatureProtector.Simulator.Host","eventType":"SensorReadingProduced","areaId":"b3f4fb84-bf17-5522-a5f3-70fd1212f381","eventTime":"2020-09-13T12:01:30+02:00","payload":{"simulationRunId":"a44215a7-14cf-48a9-9a88-5d0fb052fdfd","sensorId":"22702eee-75aa-3cf4-31ab-b689bfb9a8bc","sensorName":"pilot-humidity-0001","metricType":"Humi | {"EventId":"027d8bef-70dd-4fcd-a3f6-0174adfc0c41","CorrelationId":"a44215a714cf48a99a885d0fb052fdfd-0003-22702eee75aa3cf431abb689bfb9a8bc","Producer":"NatureProtector.Simulator.Host","EventType":"SensorReadingProduced","AreaId":"b3f4fb84-bf17-5522-a5f3-70fd1212f381","SchemaVersion":"1.0","SensorId":"22702eee-75aa-3cf4-31ab-b689bfb9a8bc","SensorName":"pilot-humidity-0001","MetricType":"Humidity","OperationalState":"Invalid","Stage":"pre_inbox_validation","DeliveryTag":19}
 054caed3-3579-4b8b-81bd-26f354f42b33 | ├ó╦åÔÇª          | ├ó╦åÔÇª     | invalid_operational_state | OperationalState 'Invalid' is rejected before the accepted-risk pipeline. | 2026-05-15 18:02:07.116268+00 | {"schemaVersion":"1.0","eventId":"51add752-e38a-40cb-89d5-4a1a51b72ba8","correlationId":"890350a3372a4ad88b0e4f232cf3d0a9-0017-2f494582407786f29707780861949155","producer":"NatureProtector.Simulator.Host","eventType":"SensorReadingProduced","areaId":"b3f4fb84-bf17-5522-a5f3-70fd1212f381","eventTime":"2020-09-13T12:08:30+02:00","payload":{"simulationRunId":"890350a3-372a-4ad8-8b0e-4f232cf3d0a9","sensorId":"2f494582-4077-86f2-9707-780861949155","sensorName":"pilot-humidity-0230","metricType":"Humi | {"EventId":"51add752-e38a-40cb-89d5-4a1a51b72ba8","CorrelationId":"890350a3372a4ad88b0e4f232cf3d0a9-0017-2f494582407786f29707780861949155","Producer":"NatureProtector.Simulator.Host","EventType":"SensorReadingProduced","AreaId":"b3f4fb84-bf17-5522-a5f3-70fd1212f381","SchemaVersion":"1.0","SensorId":"2f494582-4077-86f2-9707-780861949155","SensorName":"pilot-humidity-0230","MetricType":"Humidity","OperationalState":"Invalid","Stage":"pre_inbox_validation","DeliveryTag":104}
 3820ff45-71ea-4ec8-a0ad-54ba339d1230 | ├ó╦åÔÇª          | ├ó╦åÔÇª     | invalid_operational_state | OperationalState 'Invalid' is rejected before the accepted-risk pipeline. | 2026-05-15 18:00:37.018789+00 | {"schemaVersion":"1.0","eventId":"cd3a206f-cea7-4a80-b896-fb10231720a8","correlationId":"890350a3372a4ad88b0e4f232cf3d0a9-0014-22702eee75aa3cf431abb689bfb9a8bc","producer":"NatureProtector.Simulator.Host","eventType":"SensorReadingProduced","areaId":"b3f4fb84-bf17-5522-a5f3-70fd1212f381","eventTime":"2020-09-13T12:07:00+02:00","payload":{"simulationRunId":"890350a3-372a-4ad8-8b0e-4f232cf3d0a9","sensorId":"22702eee-75aa-3cf4-31ab-b689bfb9a8bc","sensorName":"pilot-humidity-0001","metricType":"Humi | {"EventId":"cd3a206f-cea7-4a80-b896-fb10231720a8","CorrelationId":"890350a3372a4ad88b0e4f232cf3d0a9-0014-22702eee75aa3cf431abb689bfb9a8bc","Producer":"NatureProtector.Simulator.Host","EventType":"SensorReadingProduced","AreaId":"b3f4fb84-bf17-5522-a5f3-70fd1212f381","SchemaVersion":"1.0","SensorId":"22702eee-75aa-3cf4-31ab-b689bfb9a8bc","SensorName":"pilot-humidity-0001","MetricType":"Humidity","OperationalState":"Invalid","Stage":"pre_inbox_validation","DeliveryTag":85}
 1b371382-b7a7-4ba2-9648-d4a77e05fb5e | ├ó╦åÔÇª          | ├ó╦åÔÇª     | invalid_operational_state | OperationalState 'Invalid' is rejected before the accepted-risk pipeline. | 2026-05-15 17:56:07.026133+00 | {"schemaVersion":"1.0","eventId":"aa0a54f3-8db2-4fbb-a04d-52eb1b6d5079","correlationId":"890350a3372a4ad88b0e4f232cf3d0a9-0005-27bf9aad094f7dc645b2129a8cc0522e","producer":"NatureProtector.Simulator.Host","eventType":"SensorReadingProduced","areaId":"b3f4fb84-bf17-5522-a5f3-70fd1212f381","eventTime":"2020-09-13T12:02:30+02:00","payload":{"simulationRunId":"890350a3-372a-4ad8-8b0e-4f232cf3d0a9","sensorId":"27bf9aad-094f-7dc6-45b2-129a8cc0522e","sensorName":"pilot-temperature-0001","metricType":"T | {"EventId":"aa0a54f3-8db2-4fbb-a04d-52eb1b6d5079","CorrelationId":"890350a3372a4ad88b0e4f232cf3d0a9-0005-27bf9aad094f7dc645b2129a8cc0522e","Producer":"NatureProtector.Simulator.Host","EventType":"SensorReadingProduced","AreaId":"b3f4fb84-bf17-5522-a5f3-70fd1212f381","SchemaVersion":"1.0","SensorId":"27bf9aad-094f-7dc6-45b2-129a8cc0522e","SensorName":"pilot-temperature-0001","MetricType":"Temperature","OperationalState":"Invalid","Stage":"pre_inbox_validation","DeliveryTag":33}
 564476da-84b5-4228-aed8-71d31e0413d4 | ├ó╦åÔÇª          | ├ó╦åÔÇª     | invalid_operational_state | OperationalState 'Invalid' is rejected before the accepted-risk pipeline. | 2026-05-15 17:55:06.880885+00 | {"schemaVersion":"1.0","eventId":"406f94f5-ab07-4ff2-8d4d-5d24b6b7bac5","correlationId":"890350a3372a4ad88b0e4f232cf3d0a9-0003-22702eee75aa3cf431abb689bfb9a8bc","producer":"NatureProtector.Simulator.Host","eventType":"SensorReadingProduced","areaId":"b3f4fb84-bf17-5522-a5f3-70fd1212f381","eventTime":"2020-09-13T12:01:30+02:00","payload":{"simulationRunId":"890350a3-372a-4ad8-8b0e-4f232cf3d0a9","sensorId":"22702eee-75aa-3cf4-31ab-b689bfb9a8bc","sensorName":"pilot-humidity-0001","metricType":"Humi | {"EventId":"406f94f5-ab07-4ff2-8d4d-5d24b6b7bac5","CorrelationId":"890350a3372a4ad88b0e4f232cf3d0a9-0003-22702eee75aa3cf431abb689bfb9a8bc","Producer":"NatureProtector.Simulator.Host","EventType":"SensorReadingProduced","AreaId":"b3f4fb84-bf17-5522-a5f3-70fd1212f381","SchemaVersion":"1.0","SensorId":"22702eee-75aa-3cf4-31ab-b689bfb9a8bc","SensorName":"pilot-humidity-0001","MetricType":"Humidity","OperationalState":"Invalid","Stage":"pre_inbox_validation","DeliveryTag":19}
 028234e2-fe62-4d30-a35f-0397ed9a5d3b | ├ó╦åÔÇª          | ├ó╦åÔÇª     | invalid_operational_state | OperationalState 'Invalid' is rejected before the accepted-risk pipeline. | 2026-05-13 21:50:03.947645+00 | {"schemaVersion":"1.0","eventId":"aff0e8a4-1390-4959-9442-3a8289b381d7","correlationId":"7afd89ce98f14e9c819fa76a104a633e-0003-22702eee75aa3cf431abb689bfb9a8bc","producer":"NatureProtector.Simulator.Host","eventType":"SensorReadingProduced","areaId":"b3f4fb84-bf17-5522-a5f3-70fd1212f381","eventTime":"2020-09-13T12:01:30+02:00","payload":{"simulationRunId":"7afd89ce-98f1-4e9c-819f-a76a104a633e","sensorId":"22702eee-75aa-3cf4-31ab-b689bfb9a8bc","sensorName":"pilot-humidity-0001","metricType":"Humi | {"EventId":"aff0e8a4-1390-4959-9442-3a8289b381d7","CorrelationId":"7afd89ce98f14e9c819fa76a104a633e-0003-22702eee75aa3cf431abb689bfb9a8bc","Producer":"NatureProtector.Simulator.Host","EventType":"SensorReadingProduced","AreaId":"b3f4fb84-bf17-5522-a5f3-70fd1212f381","SchemaVersion":"1.0","SensorId":"22702eee-75aa-3cf4-31ab-b689bfb9a8bc","SensorName":"pilot-humidity-0001","MetricType":"Humidity","OperationalState":"Invalid","Stage":"pre_inbox_validation","DeliveryTag":19}
 62327365-58de-4a84-be2c-cd8812a4afa8 | ├ó╦åÔÇª          | ├ó╦åÔÇª     | invalid_operational_state | OperationalState 'Invalid' is rejected before the accepted-risk pipeline. | 2026-05-12 11:11:04.67822+00  | {"schemaVersion":"1.0","eventId":"2f6659f0-b221-4207-965c-760edb490d5b","correlationId":"92b59da96c3d4f4cba16bd93656c0f91-0017-2f494582407786f29707780861949155","producer":"NatureProtector.Simulator.Host","eventType":"SensorReadingProduced","areaId":"b3f4fb84-bf17-5522-a5f3-70fd1212f381","eventTime":"2020-09-13T12:08:30+02:00","payload":{"simulationRunId":"92b59da9-6c3d-4f4c-ba16-bd93656c0f91","sensorId":"2f494582-4077-86f2-9707-780861949155","sensorName":"pilot-humidity-0230","metricType":"Humi | {"EventId":"2f6659f0-b221-4207-965c-760edb490d5b","CorrelationId":"92b59da96c3d4f4cba16bd93656c0f91-0017-2f494582407786f29707780861949155","Producer":"NatureProtector.Simulator.Host","EventType":"SensorReadingProduced","AreaId":"b3f4fb84-bf17-5522-a5f3-70fd1212f381","SchemaVersion":"1.0","SensorId":"2f494582-4077-86f2-9707-780861949155","SensorName":"pilot-humidity-0230","MetricType":"Humidity","OperationalState":"Invalid","Stage":"pre_inbox_validation","DeliveryTag":104}
 7119c1fc-b998-4eda-a74f-7db9f4d0afa7 | ├ó╦åÔÇª          | ├ó╦åÔÇª     | invalid_operational_state | OperationalState 'Invalid' is rejected before the accepted-risk pipeline. | 2026-05-12 11:09:34.592094+00 | {"schemaVersion":"1.0","eventId":"7a6ee107-5d8a-4e3e-ad66-28fd7e7308f4","correlationId":"92b59da96c3d4f4cba16bd93656c0f91-0014-22702eee75aa3cf431abb689bfb9a8bc","producer":"NatureProtector.Simulator.Host","eventType":"SensorReadingProduced","areaId":"b3f4fb84-bf17-5522-a5f3-70fd1212f381","eventTime":"2020-09-13T12:07:00+02:00","payload":{"simulationRunId":"92b59da9-6c3d-4f4c-ba16-bd93656c0f91","sensorId":"22702eee-75aa-3cf4-31ab-b689bfb9a8bc","sensorName":"pilot-humidity-0001","metricType":"Humi | {"EventId":"7a6ee107-5d8a-4e3e-ad66-28fd7e7308f4","CorrelationId":"92b59da96c3d4f4cba16bd93656c0f91-0014-22702eee75aa3cf431abb689bfb9a8bc","Producer":"NatureProtector.Simulator.Host","EventType":"SensorReadingProduced","AreaId":"b3f4fb84-bf17-5522-a5f3-70fd1212f381","SchemaVersion":"1.0","SensorId":"22702eee-75aa-3cf4-31ab-b689bfb9a8bc","SensorName":"pilot-humidity-0001","MetricType":"Humidity","OperationalState":"Invalid","Stage":"pre_inbox_validation","DeliveryTag":85}
 24fa0c71-2c79-4b87-b505-940b60fd5815 | ├ó╦åÔÇª          | ├ó╦åÔÇª     | invalid_operational_state | OperationalState 'Invalid' is rejected before the accepted-risk pipeline. | 2026-05-12 11:05:04.603674+00 | {"schemaVersion":"1.0","eventId":"2f59dd5f-bedb-43b4-8337-458e6a585fb7","correlationId":"92b59da96c3d4f4cba16bd93656c0f91-0005-27bf9aad094f7dc645b2129a8cc0522e","producer":"NatureProtector.Simulator.Host","eventType":"SensorReadingProduced","areaId":"b3f4fb84-bf17-5522-a5f3-70fd1212f381","eventTime":"2020-09-13T12:02:30+02:00","payload":{"simulationRunId":"92b59da9-6c3d-4f4c-ba16-bd93656c0f91","sensorId":"27bf9aad-094f-7dc6-45b2-129a8cc0522e","sensorName":"pilot-temperature-0001","metricType":"T | {"EventId":"2f59dd5f-bedb-43b4-8337-458e6a585fb7","CorrelationId":"92b59da96c3d4f4cba16bd93656c0f91-0005-27bf9aad094f7dc645b2129a8cc0522e","Producer":"NatureProtector.Simulator.Host","EventType":"SensorReadingProduced","AreaId":"b3f4fb84-bf17-5522-a5f3-70fd1212f381","SchemaVersion":"1.0","SensorId":"27bf9aad-094f-7dc6-45b2-129a8cc0522e","SensorName":"pilot-temperature-0001","MetricType":"Temperature","OperationalState":"Invalid","Stage":"pre_inbox_validation","DeliveryTag":33}
 d9398c42-eb87-47b0-9f90-65c5a8245217 | ├ó╦åÔÇª          | ├ó╦åÔÇª     | invalid_operational_state | OperationalState 'Invalid' is rejected before the accepted-risk pipeline. | 2026-05-12 11:04:04.44+00     | {"schemaVersion":"1.0","eventId":"e0550aa9-4fc3-4931-9c98-2d67c511eeb3","correlationId":"92b59da96c3d4f4cba16bd93656c0f91-0003-22702eee75aa3cf431abb689bfb9a8bc","producer":"NatureProtector.Simulator.Host","eventType":"SensorReadingProduced","areaId":"b3f4fb84-bf17-5522-a5f3-70fd1212f381","eventTime":"2020-09-13T12:01:30+02:00","payload":{"simulationRunId":"92b59da9-6c3d-4f4c-ba16-bd93656c0f91","sensorId":"22702eee-75aa-3cf4-31ab-b689bfb9a8bc","sensorName":"pilot-humidity-0001","metricType":"Humi | {"EventId":"e0550aa9-4fc3-4931-9c98-2d67c511eeb3","CorrelationId":"92b59da96c3d4f4cba16bd93656c0f91-0003-22702eee75aa3cf431abb689bfb9a8bc","Producer":"NatureProtector.Simulator.Host","EventType":"SensorReadingProduced","AreaId":"b3f4fb84-bf17-5522-a5f3-70fd1212f381","SchemaVersion":"1.0","SensorId":"22702eee-75aa-3cf4-31ab-b689bfb9a8bc","SensorName":"pilot-humidity-0001","MetricType":"Humidity","OperationalState":"Invalid","Stage":"pre_inbox_validation","DeliveryTag":19}
 4960f659-608c-4a2d-8c68-219c39d5a47e | ├ó╦åÔÇª          | ├ó╦åÔÇª     | invalid_operational_state | OperationalState 'Invalid' is rejected before the accepted-risk pipeline. | 2026-04-29 18:49:22.42409+00  | {"schemaVersion":"1.0","eventId":"f512d6e5-a4a3-46d6-a561-6be303568a71","correlationId":"d17c25bff7da46a8bf313bf6e33bd39e-0005-27bf9aad094f7dc645b2129a8cc0522e","producer":"NatureProtector.Simulator.Host","eventType":"SensorReadingProduced","areaId":"b3f4fb84-bf17-5522-a5f3-70fd1212f381","eventTime":"2020-09-13T12:02:30+02:00","payload":{"simulationRunId":"d17c25bf-f7da-46a8-bf31-3bf6e33bd39e","sensorId":"27bf9aad-094f-7dc6-45b2-129a8cc0522e","sensorName":"pilot-temperature-0001","metricType":"T | {"EventId":"f512d6e5-a4a3-46d6-a561-6be303568a71","CorrelationId":"d17c25bff7da46a8bf313bf6e33bd39e-0005-27bf9aad094f7dc645b2129a8cc0522e","Producer":"NatureProtector.Simulator.Host","EventType":"SensorReadingProduced","AreaId":"b3f4fb84-bf17-5522-a5f3-70fd1212f381","SchemaVersion":"1.0","SensorId":"27bf9aad-094f-7dc6-45b2-129a8cc0522e","SensorName":"pilot-temperature-0001","MetricType":"Temperature","OperationalState":"Invalid","Stage":"pre_inbox_validation","DeliveryTag":33}
 15742978-9c98-4973-8a12-36107127f980 | ├ó╦åÔÇª          | ├ó╦åÔÇª     | invalid_operational_state | OperationalState 'Invalid' is rejected before the accepted-risk pipeline. | 2026-04-29 18:48:22.27846+00  | {"schemaVersion":"1.0","eventId":"4d1a8431-188a-4112-9408-5ef18e471903","correlationId":"d17c25bff7da46a8bf313bf6e33bd39e-0003-22702eee75aa3cf431abb689bfb9a8bc","producer":"NatureProtector.Simulator.Host","eventType":"SensorReadingProduced","areaId":"b3f4fb84-bf17-5522-a5f3-70fd1212f381","eventTime":"2020-09-13T12:01:30+02:00","payload":{"simulationRunId":"d17c25bf-f7da-46a8-bf31-3bf6e33bd39e","sensorId":"22702eee-75aa-3cf4-31ab-b689bfb9a8bc","sensorName":"pilot-humidity-0001","metricType":"Humi | {"EventId":"4d1a8431-188a-4112-9408-5ef18e471903","CorrelationId":"d17c25bff7da46a8bf313bf6e33bd39e-0003-22702eee75aa3cf431abb689bfb9a8bc","Producer":"NatureProtector.Simulator.Host","EventType":"SensorReadingProduced","AreaId":"b3f4fb84-bf17-5522-a5f3-70fd1212f381","SchemaVersion":"1.0","SensorId":"22702eee-75aa-3cf4-31ab-b689bfb9a8bc","SensorName":"pilot-humidity-0001","MetricType":"Humidity","OperationalState":"Invalid","Stage":"pre_inbox_validation","DeliveryTag":19}
 7a1a8e5b-f6b2-4aa4-acba-27a875f849b8 | ├ó╦åÔÇª          | ├ó╦åÔÇª     | invalid_operational_state | OperationalState 'Invalid' is rejected before the accepted-risk pipeline. | 2026-04-29 17:09:22.851761+00 | {"schemaVersion":"1.0","eventId":"8d42017c-c628-4cb2-8ac6-8cb7769789c0","correlationId":"c07c5b3258cc466b92eae6aa558322f7-0017-2f494582407786f29707780861949155","producer":"NatureProtector.Simulator.Host","eventType":"SensorReadingProduced","areaId":"b3f4fb84-bf17-5522-a5f3-70fd1212f381","eventTime":"2020-09-13T12:08:30+02:00","payload":{"simulationRunId":"c07c5b32-58cc-466b-92ea-e6aa558322f7","sensorId":"2f494582-4077-86f2-9707-780861949155","sensorName":"pilot-humidity-0230","metricType":"Humi | {"EventId":"8d42017c-c628-4cb2-8ac6-8cb7769789c0","CorrelationId":"c07c5b3258cc466b92eae6aa558322f7-0017-2f494582407786f29707780861949155","Producer":"NatureProtector.Simulator.Host","EventType":"SensorReadingProduced","AreaId":"b3f4fb84-bf17-5522-a5f3-70fd1212f381","SchemaVersion":"1.0","SensorId":"2f494582-4077-86f2-9707-780861949155","SensorName":"pilot-humidity-0230","MetricType":"Humidity","OperationalState":"Invalid","Stage":"pre_inbox_validation","DeliveryTag":104}
 b5fb5418-2791-4e5a-9461-a971ec9410bf | ├ó╦åÔÇª          | ├ó╦åÔÇª     | invalid_operational_state | OperationalState 'Invalid' is rejected before the accepted-risk pipeline. | 2026-04-29 17:07:51.103022+00 | {"schemaVersion":"1.0","eventId":"28816398-4349-42c3-99a3-ba0aea25d66c","correlationId":"c07c5b3258cc466b92eae6aa558322f7-0014-22702eee75aa3cf431abb689bfb9a8bc","producer":"NatureProtector.Simulator.Host","eventType":"SensorReadingProduced","areaId":"b3f4fb84-bf17-5522-a5f3-70fd1212f381","eventTime":"2020-09-13T12:07:00+02:00","payload":{"simulationRunId":"c07c5b32-58cc-466b-92ea-e6aa558322f7","sensorId":"22702eee-75aa-3cf4-31ab-b689bfb9a8bc","sensorName":"pilot-humidity-0001","metricType":"Humi | {"EventId":"28816398-4349-42c3-99a3-ba0aea25d66c","CorrelationId":"c07c5b3258cc466b92eae6aa558322f7-0014-22702eee75aa3cf431abb689bfb9a8bc","Producer":"NatureProtector.Simulator.Host","EventType":"SensorReadingProduced","AreaId":"b3f4fb84-bf17-5522-a5f3-70fd1212f381","SchemaVersion":"1.0","SensorId":"22702eee-75aa-3cf4-31ab-b689bfb9a8bc","SensorName":"pilot-humidity-0001","MetricType":"Humidity","OperationalState":"Invalid","Stage":"pre_inbox_validation","DeliveryTag":85}
(25 rows)


```

## Quarantined events summary

```sql
select
  "QuarantineCode",
  "QuarantineReason",
  count(*) as count,
  min("QuarantinedAt") as first_quarantined_at,
  max("QuarantinedAt") as last_quarantined_at
from pipeline.quarantined_events
group by "QuarantineCode", "QuarantineReason"
order by max("QuarantinedAt") desc, count desc;
```

```text
  QuarantineCode   |                           QuarantineReason                           | count |     first_quarantined_at     |      last_quarantined_at      
-------------------+----------------------------------------------------------------------+-------+------------------------------+-------------------------------
 retries_exhausted | The event exhausted the configured retry policy and was quarantined. |   100 | 2026-04-11 11:39:40.53029+00 | 2026-04-17 19:56:09.876076+00
(1 row)


```

## Latest quarantined events

```sql
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
```

```text
                  Id                  |             InboxEventId             |               EventId                | FinalAttemptNumber |  QuarantineCode   |                           QuarantineReason                           |         QuarantinedAt         |           MetadataJson            
--------------------------------------+--------------------------------------+--------------------------------------+--------------------+-------------------+----------------------------------------------------------------------+-------------------------------+-----------------------------------
 4cbdf165-89f2-4e08-b26d-2ce29528d301 | 9b165111-d2dd-4f6f-bb10-dcbdba810c02 | 5dfc59bf-911a-4322-ad84-03ac8584b7d7 |                  3 | retries_exhausted | The event exhausted the configured retry policy and was quarantined. | 2026-04-17 19:56:09.876076+00 | {"stage":"reading_risk_pipeline"}
 cf08ae61-e508-48f3-ae42-f2a028eb7fb0 | 822c5547-be56-47b0-a617-5ba9cbc460c9 | b12550f1-f8a1-44de-b41a-3947a16ff597 |                  3 | retries_exhausted | The event exhausted the configured retry policy and was quarantined. | 2026-04-17 19:56:03.876135+00 | {"stage":"reading_risk_pipeline"}
 794ba8a5-6e94-41a5-97dc-731ab3693582 | 79bfabc3-0392-44c0-9e0e-88430b0a6bce | 88e50a73-f10d-4899-8312-85dfc2c4abf2 |                  3 | retries_exhausted | The event exhausted the configured retry policy and was quarantined. | 2026-04-17 19:55:59.876069+00 | {"stage":"reading_risk_pipeline"}
 921a29f2-a388-46df-9164-ccf99f52d453 | 870b0dba-2438-4d86-8856-44d793c39815 | 02545771-d50a-4ad8-93ca-27972653414d |                  3 | retries_exhausted | The event exhausted the configured retry policy and was quarantined. | 2026-04-17 19:55:55.876668+00 | {"stage":"reading_risk_pipeline"}
 df9be50e-0161-41ca-b181-70a2821588bb | e1e2e913-fd74-4638-9925-67a233c373c3 | 8fb8cea3-d8a9-466b-ab91-e9486317a0dd |                  3 | retries_exhausted | The event exhausted the configured retry policy and was quarantined. | 2026-04-17 19:55:39.877314+00 | {"stage":"reading_risk_pipeline"}
 b6b92fc1-bb9c-471f-a2bd-af690f2c93a5 | 166c6346-02f2-4980-b8a1-5a79e6abfa15 | 83f05117-ba43-4ba3-8edb-f735b196f0ab |                  3 | retries_exhausted | The event exhausted the configured retry policy and was quarantined. | 2026-04-17 19:55:37.877253+00 | {"stage":"reading_risk_pipeline"}
 e2ecb1c7-825b-4d36-9101-b1e955bf5902 | df9a5601-c2cf-4939-9a7b-7b0185a68fdd | effe17aa-c297-41fc-9b01-3922a222a0dc |                  3 | retries_exhausted | The event exhausted the configured retry policy and was quarantined. | 2026-04-17 19:55:35.876655+00 | {"stage":"reading_risk_pipeline"}
 2e02eacc-cbea-40e5-a6b6-b20de6a7a9f4 | c352cd14-0666-45a1-b1f0-4fd49bd1f95d | fac21986-b3f8-4e7f-a62f-26e7bebc517a |                  3 | retries_exhausted | The event exhausted the configured retry policy and was quarantined. | 2026-04-17 19:55:33.877083+00 | {"stage":"reading_risk_pipeline"}
 c47d682d-72b5-489e-bc88-9ea7514ddfdc | 92441f56-d471-4797-9cce-bc1ce2103cb5 | 66d886a2-1d21-4e1c-bfea-69b70bcbaf8a |                  3 | retries_exhausted | The event exhausted the configured retry policy and was quarantined. | 2026-04-17 19:55:29.877641+00 | {"stage":"reading_risk_pipeline"}
 837c9781-e4fe-49ac-b2f4-b242712c1de1 | 90a0c3e1-20a3-4555-8b38-597709d009e5 | 0459a2ee-1127-4423-bdaa-c4dc655eaa50 |                  3 | retries_exhausted | The event exhausted the configured retry policy and was quarantined. | 2026-04-17 19:55:25.877581+00 | {"stage":"reading_risk_pipeline"}
 548d136a-28e3-4a8f-a374-dc73c63439fc | 903bb861-c2e9-4fa1-90bb-2d46792fbe4b | 3db1d645-0366-4dd4-a5a8-782f31aaa936 |                  3 | retries_exhausted | The event exhausted the configured retry policy and was quarantined. | 2026-04-17 19:55:12.878248+00 | {"stage":"reading_risk_pipeline"}
 83bcfb1a-8253-4499-bb33-c57570107a22 | 38152132-af5b-413c-aee4-0368c959f433 | aaef40ed-7145-4829-9b90-78f6e4612ed1 |                  3 | retries_exhausted | The event exhausted the configured retry policy and was quarantined. | 2026-04-17 19:55:10.87814+00  | {"stage":"reading_risk_pipeline"}
 7d05bf11-50c2-4871-9e78-b3cb768d76a4 | 3318bd58-b9a5-4ac1-bd83-270daaf81732 | d0ea0377-41ab-4641-9406-85965635c8f1 |                  3 | retries_exhausted | The event exhausted the configured retry policy and was quarantined. | 2026-04-17 19:55:08.878517+00 | {"stage":"reading_risk_pipeline"}
 473f4989-7659-4f37-ac7c-58cdd34d424f | 3657679a-1fc8-4aef-8a28-fead59e2c2fb | 88a62d87-ed26-4289-a705-5c3bb7ff26c4 |                  3 | retries_exhausted | The event exhausted the configured retry policy and was quarantined. | 2026-04-17 19:55:04.878537+00 | {"stage":"reading_risk_pipeline"}
 f4650d16-2fac-414b-8f69-64b04d9927a2 | 49125174-c948-4531-8500-66f5e021b090 | 741034be-f294-49ce-9333-71663da8b7b9 |                  3 | retries_exhausted | The event exhausted the configured retry policy and was quarantined. | 2026-04-17 19:55:00.878651+00 | {"stage":"reading_risk_pipeline"}
 90a423cc-cdd5-46c2-a3d6-f22ddeae0e0e | cbf53d3d-a08a-46f5-8984-bbf5eb4c4fc3 | cbdb8262-49c1-42dd-9599-f7ce54c8851e |                  3 | retries_exhausted | The event exhausted the configured retry policy and was quarantined. | 2026-04-17 19:54:56.878621+00 | {"stage":"reading_risk_pipeline"}
 62cbb14d-44f2-427f-a372-79cb6a03f65b | 50db7092-d38b-43f1-9681-5bc910cab965 | 5798b995-c306-498e-b691-fb4490cedb70 |                  3 | retries_exhausted | The event exhausted the configured retry policy and was quarantined. | 2026-04-17 19:54:43.87969+00  | {"stage":"reading_risk_pipeline"}
 3e2e84f4-080e-4c84-8f99-43a06c04b117 | ca96451e-762c-406e-8236-ab6989e6d3aa | 9e55b1ab-c054-4838-acae-d04c9638c1a1 |                  3 | retries_exhausted | The event exhausted the configured retry policy and was quarantined. | 2026-04-17 19:54:41.879967+00 | {"stage":"reading_risk_pipeline"}
 447aba14-a39a-4681-836d-43b9bfcdff52 | d8404ba2-7d10-4484-a9de-1d960dbca167 | 8d05fb6f-76a8-4f10-b746-6317cefa7010 |                  3 | retries_exhausted | The event exhausted the configured retry policy and was quarantined. | 2026-04-17 19:54:39.879069+00 | {"stage":"reading_risk_pipeline"}
 44f7896e-8d9a-49f0-8d39-f9478aaf5d69 | 9dea49cf-ecfe-430b-a71b-72d67a8029ed | af7fff20-0541-4807-98d7-ca50496639b0 |                  3 | retries_exhausted | The event exhausted the configured retry policy and was quarantined. | 2026-04-17 19:54:37.878962+00 | {"stage":"reading_risk_pipeline"}
 2dcab30a-f5db-41fc-ab90-c88e38e14c28 | 8e38ed50-6ff5-4aa0-8f5d-f7309d28bf4a | 295ea8d9-553a-4eee-955b-1d51df37eb8a |                  3 | retries_exhausted | The event exhausted the configured retry policy and was quarantined. | 2026-04-17 19:54:33.879301+00 | {"stage":"reading_risk_pipeline"}
 007c0e22-f210-4649-9687-61460f7bc321 | 06dc9b84-0014-4410-a935-64229770cff9 | 31016ed9-c24d-461d-97ac-2ad7af10eb97 |                  3 | retries_exhausted | The event exhausted the configured retry policy and was quarantined. | 2026-04-17 19:54:29.879467+00 | {"stage":"reading_risk_pipeline"}
 76f93632-c57c-4acc-9910-f88c5b2155b2 | a71bc8c6-1833-4c5f-ac90-ba688b9fab3b | 6ab2b2be-b394-476c-8423-70613284f780 |                  3 | retries_exhausted | The event exhausted the configured retry policy and was quarantined. | 2026-04-17 19:54:14.880778+00 | {"stage":"reading_risk_pipeline"}
 7fba5482-e7b9-46f1-a75e-2e5bf8922945 | d6aa01fa-136a-49d4-9a22-57b6a3040610 | 88508da5-eb87-47f1-b725-da5e3e940d13 |                  3 | retries_exhausted | The event exhausted the configured retry policy and was quarantined. | 2026-04-17 19:54:12.880439+00 | {"stage":"reading_risk_pipeline"}
 4cc75fac-72f3-4ce8-83c5-d459aace494b | 96edd85b-555a-41ac-b96b-ac5cdea6136f | fd2e3448-4bfe-410b-ac07-43bb029d14c8 |                  3 | retries_exhausted | The event exhausted the configured retry policy and was quarantined. | 2026-04-17 19:54:10.880599+00 | {"stage":"reading_risk_pipeline"}
(25 rows)


```

## Projection totals

```sql
select
  (select count(*) from projection.accepted_reading_log) as accepted_readings,
  (select count(*) from projection.risk_assessment_log) as risk_assessments,
  (select count(*) from projection.area_risk_snapshot_log) as area_risk_snapshots,
  (select count(*) from projection.cell_operational_state) as cell_operational_states,
  (select count(*) from projection.area_operational_state) as area_operational_states,
  (select count(*) from projection.alert_state) as alert_states;
```

```text
 accepted_readings | risk_assessments | area_risk_snapshots | cell_operational_states | area_operational_states | alert_states 
-------------------+------------------+---------------------+-------------------------+-------------------------+--------------
              6707 |             6706 |                6598 |                      25 |                       1 |            4
(1 row)


```

## Projection time ranges

```sql
select
  (select min("CreatedAt") from projection.risk_assessment_log) as first_risk_created_at,
  (select max("CreatedAt") from projection.risk_assessment_log) as last_risk_created_at,
  (select min("SnapshotTimestamp") from projection.area_risk_snapshot_log) as first_area_snapshot_timestamp,
  (select max("SnapshotTimestamp") from projection.area_risk_snapshot_log) as last_area_snapshot_timestamp,
  (select max("UpdatedAt") from projection.area_operational_state) as last_area_state_updated_at,
  (select max("UpdatedAt") from projection.alert_state) as last_alert_updated_at;
```

```text
     first_risk_created_at     |     last_risk_created_at      | first_area_snapshot_timestamp | last_area_snapshot_timestamp |  last_area_state_updated_at   |    last_alert_updated_at     
-------------------------------+-------------------------------+-------------------------------+------------------------------+-------------------------------+------------------------------
 2026-04-11 11:38:58.563527+00 | 2026-05-15 21:44:56.282589+00 | 2020-09-13 10:00:00+00        | 2020-09-13 10:09:30+00       | 2026-05-15 21:44:56.311218+00 | 2026-04-17 13:42:58.79077+00
(1 row)


```

## Risk assessment columns

```sql
select column_name, data_type
from information_schema.columns
where table_schema = 'projection'
  and table_name = 'risk_assessment_log'
order by ordinal_position;
```

```text
    column_name     |        data_type         
--------------------+--------------------------
 Id                 | uuid
 AreaId             | uuid
 SensorId           | uuid
 GridCellId         | uuid
 SourceEventId      | uuid
 Timestamp          | timestamp with time zone
 RiskScore          | double precision
 RiskLevel          | character varying
 ExplanationSummary | character varying
 CreatedAt          | timestamp with time zone
(10 rows)


```

## Risk assessment score range

```sql
select
  count(*) as risk_assessments,
  min("RiskScore") as min_risk_score,
  max("RiskScore") as max_risk_score,
  avg("RiskScore") as avg_risk_score
from projection.risk_assessment_log;
```

```text
 risk_assessments | min_risk_score | max_risk_score |   avg_risk_score   
------------------+----------------+----------------+--------------------
             6706 |            0.1 |           0.95 | 0.5097450044736064
(1 row)


```

## Risk assessment by level

```sql
select
  "RiskLevel",
  count(*) as count,
  min("RiskScore") as min_score,
  max("RiskScore") as max_score
from projection.risk_assessment_log
group by "RiskLevel"
order by min_score;
```

```text
 RiskLevel | count | min_score | max_score 
-----------+-------+-----------+-----------
 Low       |  1113 |       0.1 |       0.1
 Moderate  |  1375 |       0.3 |       0.4
 High      |  1938 |      0.65 |      0.65
 VeryHigh  |  2235 |       0.7 |       0.7
 Extreme   |    45 |      0.95 |      0.95
(5 rows)


```

## Latest risk assessments

```sql
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
```

```text
                  Id                  |                AreaId                |               SensorId               |              GridCellId              |            SourceEventId             |       Timestamp        | RiskScore | RiskLevel |                                                                                                                                             ExplanationSummary                                                                                                                                              |           CreatedAt           
--------------------------------------+--------------------------------------+--------------------------------------+--------------------------------------+--------------------------------------+------------------------+-----------+-----------+-------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------+-------------------------------
 6bdac3fb-6c39-46b5-9ffd-f8a6e419ed09 | b3f4fb84-bf17-5522-a5f3-70fd1212f381 | 17badc17-7920-17ac-c213-db1c12bedfb2 | 8a65d4e7-92f5-1b6f-fdb0-d60ec5707d72 | 16cf048d-5d4a-47be-aae8-5f72e09531d3 | 2020-09-13 10:00:20+00 |       0.3 | Moderate  | Area=b3f4fb84-bf17-5522-a5f3-70fd1212f381; Sensor=17badc17-7920-17ac-c213-db1c12bedfb2; Event=16cf048d-5d4a-47be-aae8-5f72e09531d3; Metric=WindSpeed; Value=5,79; BaseRisk=0,30; AdjustedScore=0,30; C=1,00; I=1,00; EligibilityFactor=1,00; ParameterSet=Candidate Parameter Set V1.0 (non-calibrated).    | 2026-05-15 21:44:56.282589+00
 d5ff106d-550d-48b8-9945-2157ec6d2228 | b3f4fb84-bf17-5522-a5f3-70fd1212f381 | e7660606-cadd-4212-e67b-574c09ef789a | 33d52e78-7801-cf3b-bc8b-c798db0d4068 | e6838c5e-fcf8-4673-848d-25ae334f7374 | 2020-09-13 10:00:20+00 |       0.3 | Moderate  | Area=b3f4fb84-bf17-5522-a5f3-70fd1212f381; Sensor=e7660606-cadd-4212-e67b-574c09ef789a; Event=e6838c5e-fcf8-4673-848d-25ae334f7374; Metric=WindSpeed; Value=5,66; BaseRisk=0,30; AdjustedScore=0,30; C=1,00; I=1,00; EligibilityFactor=1,00; ParameterSet=Candidate Parameter Set V1.0 (non-calibrated).    | 2026-05-15 21:44:56.227723+00
 26e8bcf9-9205-455c-9571-3f9f47fcffc1 | b3f4fb84-bf17-5522-a5f3-70fd1212f381 | 8e6a3d8a-7e65-4fa5-8c51-fa4597b203b8 | 8a65d4e7-92f5-1b6f-fdb0-d60ec5707d72 | b4c52802-1c73-4284-b225-58072d4fe93c | 2020-09-13 10:00:20+00 |      0.65 | High      | Area=b3f4fb84-bf17-5522-a5f3-70fd1212f381; Sensor=8e6a3d8a-7e65-4fa5-8c51-fa4597b203b8; Event=b4c52802-1c73-4284-b225-58072d4fe93c; Metric=Temperature; Value=32,12; BaseRisk=0,65; AdjustedScore=0,65; C=1,00; I=1,00; EligibilityFactor=1,00; ParameterSet=Candidate Parameter Set V1.0 (non-calibrated). | 2026-05-15 21:44:56.177363+00
 a67c89a2-7293-4f10-8886-91d8677806d6 | b3f4fb84-bf17-5522-a5f3-70fd1212f381 | 27bf9aad-094f-7dc6-45b2-129a8cc0522e | 33d52e78-7801-cf3b-bc8b-c798db0d4068 | a9a8f53e-f6b8-4070-8760-9d4918556456 | 2020-09-13 10:00:20+00 |      0.65 | High      | Area=b3f4fb84-bf17-5522-a5f3-70fd1212f381; Sensor=27bf9aad-094f-7dc6-45b2-129a8cc0522e; Event=a9a8f53e-f6b8-4070-8760-9d4918556456; Metric=Temperature; Value=32,42; BaseRisk=0,65; AdjustedScore=0,65; C=1,00; I=1,00; EligibilityFactor=1,00; ParameterSet=Candidate Parameter Set V1.0 (non-calibrated). | 2026-05-15 21:44:56.119652+00
 6af8153e-b75b-41c8-bb87-b6dd0f34ce74 | b3f4fb84-bf17-5522-a5f3-70fd1212f381 | 2f494582-4077-86f2-9707-780861949155 | 8a65d4e7-92f5-1b6f-fdb0-d60ec5707d72 | 8da335a4-d553-4a46-8828-2c5cb1c55191 | 2020-09-13 10:00:20+00 |       0.7 | VeryHigh  | Area=b3f4fb84-bf17-5522-a5f3-70fd1212f381; Sensor=2f494582-4077-86f2-9707-780861949155; Event=8da335a4-d553-4a46-8828-2c5cb1c55191; Metric=Humidity; Value=21,39; BaseRisk=0,70; AdjustedScore=0,70; C=1,00; I=1,00; EligibilityFactor=1,00; ParameterSet=Candidate Parameter Set V1.0 (non-calibrated).    | 2026-05-15 21:44:56.062241+00
 411fd096-5c0d-4d35-a219-216e37335cfb | b3f4fb84-bf17-5522-a5f3-70fd1212f381 | 22702eee-75aa-3cf4-31ab-b689bfb9a8bc | 33d52e78-7801-cf3b-bc8b-c798db0d4068 | a093e437-f178-4daa-9f21-645417fac748 | 2020-09-13 10:00:20+00 |       0.7 | VeryHigh  | Area=b3f4fb84-bf17-5522-a5f3-70fd1212f381; Sensor=22702eee-75aa-3cf4-31ab-b689bfb9a8bc; Event=a093e437-f178-4daa-9f21-645417fac748; Metric=Humidity; Value=20,50; BaseRisk=0,70; AdjustedScore=0,70; C=1,00; I=1,00; EligibilityFactor=1,00; ParameterSet=Candidate Parameter Set V1.0 (non-calibrated).    | 2026-05-15 21:44:55.999891+00
 a36a2258-13cc-4766-bfdc-164aa32b62bd | b3f4fb84-bf17-5522-a5f3-70fd1212f381 | 17badc17-7920-17ac-c213-db1c12bedfb2 | 8a65d4e7-92f5-1b6f-fdb0-d60ec5707d72 | c7fcb2b5-e872-4dd8-ba66-3cabe970f7d0 | 2020-09-13 10:00:15+00 |       0.3 | Moderate  | Area=b3f4fb84-bf17-5522-a5f3-70fd1212f381; Sensor=17badc17-7920-17ac-c213-db1c12bedfb2; Event=c7fcb2b5-e872-4dd8-ba66-3cabe970f7d0; Metric=WindSpeed; Value=5,22; BaseRisk=0,30; AdjustedScore=0,30; C=1,00; I=1,00; EligibilityFactor=1,00; ParameterSet=Candidate Parameter Set V1.0 (non-calibrated).    | 2026-05-15 21:44:51.187125+00
 f36e0931-3508-47f5-9659-21f0e089197a | b3f4fb84-bf17-5522-a5f3-70fd1212f381 | e7660606-cadd-4212-e67b-574c09ef789a | 33d52e78-7801-cf3b-bc8b-c798db0d4068 | e401dbff-73c6-4cfa-baac-baa95daab3be | 2020-09-13 10:00:15+00 |       0.3 | Moderate  | Area=b3f4fb84-bf17-5522-a5f3-70fd1212f381; Sensor=e7660606-cadd-4212-e67b-574c09ef789a; Event=e401dbff-73c6-4cfa-baac-baa95daab3be; Metric=WindSpeed; Value=5,38; BaseRisk=0,30; AdjustedScore=0,30; C=1,00; I=1,00; EligibilityFactor=1,00; ParameterSet=Candidate Parameter Set V1.0 (non-calibrated).    | 2026-05-15 21:44:51.132409+00
 7fcf2add-8c5a-4c2a-baa7-a8ae2aa9a4c6 | b3f4fb84-bf17-5522-a5f3-70fd1212f381 | 8e6a3d8a-7e65-4fa5-8c51-fa4597b203b8 | 8a65d4e7-92f5-1b6f-fdb0-d60ec5707d72 | 21b0a910-af9e-46e4-9884-925478ba3694 | 2020-09-13 10:00:15+00 |      0.65 | High      | Area=b3f4fb84-bf17-5522-a5f3-70fd1212f381; Sensor=8e6a3d8a-7e65-4fa5-8c51-fa4597b203b8; Event=21b0a910-af9e-46e4-9884-925478ba3694; Metric=Temperature; Value=32,10; BaseRisk=0,65; AdjustedScore=0,65; C=1,00; I=1,00; EligibilityFactor=1,00; ParameterSet=Candidate Parameter Set V1.0 (non-calibrated). | 2026-05-15 21:44:51.080918+00
 84e9e491-8de7-4c87-8c78-ccdc4aeb054a | b3f4fb84-bf17-5522-a5f3-70fd1212f381 | 27bf9aad-094f-7dc6-45b2-129a8cc0522e | 33d52e78-7801-cf3b-bc8b-c798db0d4068 | 99c5af0d-a4f5-4da0-a2ed-074dbdd40ea7 | 2020-09-13 10:00:15+00 |      0.65 | High      | Area=b3f4fb84-bf17-5522-a5f3-70fd1212f381; Sensor=27bf9aad-094f-7dc6-45b2-129a8cc0522e; Event=99c5af0d-a4f5-4da0-a2ed-074dbdd40ea7; Metric=Temperature; Value=32,41; BaseRisk=0,65; AdjustedScore=0,65; C=1,00; I=1,00; EligibilityFactor=1,00; ParameterSet=Candidate Parameter Set V1.0 (non-calibrated). | 2026-05-15 21:44:51.020877+00
 f125f512-cdb5-4f02-8518-287168b43651 | b3f4fb84-bf17-5522-a5f3-70fd1212f381 | 2f494582-4077-86f2-9707-780861949155 | 8a65d4e7-92f5-1b6f-fdb0-d60ec5707d72 | 069c68a4-b515-4ae1-b79b-8258c595f080 | 2020-09-13 10:00:15+00 |       0.7 | VeryHigh  | Area=b3f4fb84-bf17-5522-a5f3-70fd1212f381; Sensor=2f494582-4077-86f2-9707-780861949155; Event=069c68a4-b515-4ae1-b79b-8258c595f080; Metric=Humidity; Value=21,54; BaseRisk=0,70; AdjustedScore=0,70; C=1,00; I=1,00; EligibilityFactor=1,00; ParameterSet=Candidate Parameter Set V1.0 (non-calibrated).    | 2026-05-15 21:44:50.964743+00
 1a32c709-2033-4067-999e-0bce929e612e | b3f4fb84-bf17-5522-a5f3-70fd1212f381 | 17badc17-7920-17ac-c213-db1c12bedfb2 | 8a65d4e7-92f5-1b6f-fdb0-d60ec5707d72 | b4a54df2-c2a1-4945-bc1c-929b4c453df1 | 2020-09-13 10:00:10+00 |       0.3 | Moderate  | Area=b3f4fb84-bf17-5522-a5f3-70fd1212f381; Sensor=17badc17-7920-17ac-c213-db1c12bedfb2; Event=b4a54df2-c2a1-4945-bc1c-929b4c453df1; Metric=WindSpeed; Value=5,06; BaseRisk=0,30; AdjustedScore=0,30; C=1,00; I=1,00; EligibilityFactor=1,00; ParameterSet=Candidate Parameter Set V1.0 (non-calibrated).    | 2026-05-15 21:44:46.293186+00
 24232509-fc81-4f04-8b54-c7c43e88922d | b3f4fb84-bf17-5522-a5f3-70fd1212f381 | e7660606-cadd-4212-e67b-574c09ef789a | 33d52e78-7801-cf3b-bc8b-c798db0d4068 | 088db1c0-c0bf-4f39-b714-eda0cfe9cc9e | 2020-09-13 10:00:10+00 |       0.1 | Low       | Area=b3f4fb84-bf17-5522-a5f3-70fd1212f381; Sensor=e7660606-cadd-4212-e67b-574c09ef789a; Event=088db1c0-c0bf-4f39-b714-eda0cfe9cc9e; Metric=WindSpeed; Value=4,88; BaseRisk=0,10; AdjustedScore=0,10; C=1,00; I=1,00; EligibilityFactor=1,00; ParameterSet=Candidate Parameter Set V1.0 (non-calibrated).    | 2026-05-15 21:44:46.212987+00
 97fe8207-2404-4004-9c20-c2bb514cb5bd | b3f4fb84-bf17-5522-a5f3-70fd1212f381 | 8e6a3d8a-7e65-4fa5-8c51-fa4597b203b8 | 8a65d4e7-92f5-1b6f-fdb0-d60ec5707d72 | 4ffa59f9-2f84-4f60-afe0-5ee0dfdff43a | 2020-09-13 10:00:10+00 |      0.65 | High      | Area=b3f4fb84-bf17-5522-a5f3-70fd1212f381; Sensor=8e6a3d8a-7e65-4fa5-8c51-fa4597b203b8; Event=4ffa59f9-2f84-4f60-afe0-5ee0dfdff43a; Metric=Temperature; Value=31,72; BaseRisk=0,65; AdjustedScore=0,65; C=1,00; I=1,00; EligibilityFactor=1,00; ParameterSet=Candidate Parameter Set V1.0 (non-calibrated). | 2026-05-15 21:44:46.141615+00
 348c6067-a131-4a94-ad52-90852912a2f5 | b3f4fb84-bf17-5522-a5f3-70fd1212f381 | 27bf9aad-094f-7dc6-45b2-129a8cc0522e | 33d52e78-7801-cf3b-bc8b-c798db0d4068 | 9f1e5e70-8b21-44d6-b3b7-8519b63998db | 2020-09-13 10:00:10+00 |      0.65 | High      | Area=b3f4fb84-bf17-5522-a5f3-70fd1212f381; Sensor=27bf9aad-094f-7dc6-45b2-129a8cc0522e; Event=9f1e5e70-8b21-44d6-b3b7-8519b63998db; Metric=Temperature; Value=31,74; BaseRisk=0,65; AdjustedScore=0,65; C=1,00; I=1,00; EligibilityFactor=1,00; ParameterSet=Candidate Parameter Set V1.0 (non-calibrated). | 2026-05-15 21:44:46.071092+00
 c5f83247-2f41-47fb-8d6f-109f428898dd | b3f4fb84-bf17-5522-a5f3-70fd1212f381 | 2f494582-4077-86f2-9707-780861949155 | 8a65d4e7-92f5-1b6f-fdb0-d60ec5707d72 | 30b5fae9-c208-4334-a7aa-d52a87284d74 | 2020-09-13 10:00:10+00 |       0.7 | VeryHigh  | Area=b3f4fb84-bf17-5522-a5f3-70fd1212f381; Sensor=2f494582-4077-86f2-9707-780861949155; Event=30b5fae9-c208-4334-a7aa-d52a87284d74; Metric=Humidity; Value=23,00; BaseRisk=0,70; AdjustedScore=0,70; C=1,00; I=1,00; EligibilityFactor=1,00; ParameterSet=Candidate Parameter Set V1.0 (non-calibrated).    | 2026-05-15 21:44:45.99591+00
 0196fdcd-921e-4cca-be72-2297df248230 | b3f4fb84-bf17-5522-a5f3-70fd1212f381 | 22702eee-75aa-3cf4-31ab-b689bfb9a8bc | 33d52e78-7801-cf3b-bc8b-c798db0d4068 | 29c9199d-e22a-4613-83d8-83cdc575835f | 2020-09-13 10:00:10+00 |       0.7 | VeryHigh  | Area=b3f4fb84-bf17-5522-a5f3-70fd1212f381; Sensor=22702eee-75aa-3cf4-31ab-b689bfb9a8bc; Event=29c9199d-e22a-4613-83d8-83cdc575835f; Metric=Humidity; Value=22,76; BaseRisk=0,70; AdjustedScore=0,70; C=1,00; I=1,00; EligibilityFactor=1,00; ParameterSet=Candidate Parameter Set V1.0 (non-calibrated).    | 2026-05-15 21:44:45.924532+00
 1ba31f3a-447a-408a-bf99-28b6c37cd3a6 | b3f4fb84-bf17-5522-a5f3-70fd1212f381 | 17badc17-7920-17ac-c213-db1c12bedfb2 | 8a65d4e7-92f5-1b6f-fdb0-d60ec5707d72 | 305da371-35bd-438b-b040-efd77d4afe6b | 2020-09-13 10:00:05+00 |       0.1 | Low       | Area=b3f4fb84-bf17-5522-a5f3-70fd1212f381; Sensor=17badc17-7920-17ac-c213-db1c12bedfb2; Event=305da371-35bd-438b-b040-efd77d4afe6b; Metric=WindSpeed; Value=4,49; BaseRisk=0,10; AdjustedScore=0,10; C=1,00; I=1,00; EligibilityFactor=1,00; ParameterSet=Candidate Parameter Set V1.0 (non-calibrated).    | 2026-05-15 21:44:41.219249+00
 2c160ba0-ddcc-4543-9328-d11ef98bf554 | b3f4fb84-bf17-5522-a5f3-70fd1212f381 | e7660606-cadd-4212-e67b-574c09ef789a | 33d52e78-7801-cf3b-bc8b-c798db0d4068 | 87cb0bc4-09f8-471e-8793-d43dac48eb09 | 2020-09-13 10:00:05+00 |       0.1 | Low       | Area=b3f4fb84-bf17-5522-a5f3-70fd1212f381; Sensor=e7660606-cadd-4212-e67b-574c09ef789a; Event=87cb0bc4-09f8-471e-8793-d43dac48eb09; Metric=WindSpeed; Value=4,39; BaseRisk=0,10; AdjustedScore=0,10; C=1,00; I=1,00; EligibilityFactor=1,00; ParameterSet=Candidate Parameter Set V1.0 (non-calibrated).    | 2026-05-15 21:44:41.162697+00
 df84b493-f71a-49f9-becb-39245956f9cd | b3f4fb84-bf17-5522-a5f3-70fd1212f381 | 8e6a3d8a-7e65-4fa5-8c51-fa4597b203b8 | 8a65d4e7-92f5-1b6f-fdb0-d60ec5707d72 | 1dc90bcb-d2f8-4584-89a0-598208429c12 | 2020-09-13 10:00:05+00 |      0.65 | High      | Area=b3f4fb84-bf17-5522-a5f3-70fd1212f381; Sensor=8e6a3d8a-7e65-4fa5-8c51-fa4597b203b8; Event=1dc90bcb-d2f8-4584-89a0-598208429c12; Metric=Temperature; Value=31,39; BaseRisk=0,65; AdjustedScore=0,65; C=1,00; I=1,00; EligibilityFactor=1,00; ParameterSet=Candidate Parameter Set V1.0 (non-calibrated). | 2026-05-15 21:44:41.107326+00
 a6da1ffd-f297-4342-b2bb-134b0b0d9270 | b3f4fb84-bf17-5522-a5f3-70fd1212f381 | 27bf9aad-094f-7dc6-45b2-129a8cc0522e | 33d52e78-7801-cf3b-bc8b-c798db0d4068 | cb9e55ed-34b6-48d6-ab2b-1492bd5c9a19 | 2020-09-13 10:00:05+00 |      0.65 | High      | Area=b3f4fb84-bf17-5522-a5f3-70fd1212f381; Sensor=27bf9aad-094f-7dc6-45b2-129a8cc0522e; Event=cb9e55ed-34b6-48d6-ab2b-1492bd5c9a19; Metric=Temperature; Value=31,77; BaseRisk=0,65; AdjustedScore=0,65; C=1,00; I=1,00; EligibilityFactor=1,00; ParameterSet=Candidate Parameter Set V1.0 (non-calibrated). | 2026-05-15 21:44:41.039989+00
 a356557c-5674-401c-9955-da5fc4c597a8 | b3f4fb84-bf17-5522-a5f3-70fd1212f381 | 2f494582-4077-86f2-9707-780861949155 | 8a65d4e7-92f5-1b6f-fdb0-d60ec5707d72 | 01c40599-e3a5-49dd-8615-102669f69eb8 | 2020-09-13 10:00:05+00 |       0.7 | VeryHigh  | Area=b3f4fb84-bf17-5522-a5f3-70fd1212f381; Sensor=2f494582-4077-86f2-9707-780861949155; Event=01c40599-e3a5-49dd-8615-102669f69eb8; Metric=Humidity; Value=24,15; BaseRisk=0,70; AdjustedScore=0,70; C=1,00; I=1,00; EligibilityFactor=1,00; ParameterSet=Candidate Parameter Set V1.0 (non-calibrated).    | 2026-05-15 21:44:40.960698+00
 acdf3c71-c2dc-4a46-a1a4-8a806bcec645 | b3f4fb84-bf17-5522-a5f3-70fd1212f381 | 22702eee-75aa-3cf4-31ab-b689bfb9a8bc | 33d52e78-7801-cf3b-bc8b-c798db0d4068 | 6bcee3ca-1425-4787-b9e1-8930d0219755 | 2020-09-13 10:00:05+00 |       0.7 | VeryHigh  | Area=b3f4fb84-bf17-5522-a5f3-70fd1212f381; Sensor=22702eee-75aa-3cf4-31ab-b689bfb9a8bc; Event=6bcee3ca-1425-4787-b9e1-8930d0219755; Metric=Humidity; Value=22,78; BaseRisk=0,70; AdjustedScore=0,70; C=1,00; I=1,00; EligibilityFactor=1,00; ParameterSet=Candidate Parameter Set V1.0 (non-calibrated).    | 2026-05-15 21:44:40.889549+00
 18de8d24-8197-4033-953c-fb4fb8f9c4d3 | b3f4fb84-bf17-5522-a5f3-70fd1212f381 | 17badc17-7920-17ac-c213-db1c12bedfb2 | 8a65d4e7-92f5-1b6f-fdb0-d60ec5707d72 | 8fb99fa7-f5d2-4ca2-bce1-66163bd55fb2 | 2020-09-13 10:00:00+00 |       0.1 | Low       | Area=b3f4fb84-bf17-5522-a5f3-70fd1212f381; Sensor=17badc17-7920-17ac-c213-db1c12bedfb2; Event=8fb99fa7-f5d2-4ca2-bce1-66163bd55fb2; Metric=WindSpeed; Value=3,71; BaseRisk=0,10; AdjustedScore=0,10; C=1,00; I=1,00; EligibilityFactor=1,00; ParameterSet=Candidate Parameter Set V1.0 (non-calibrated).    | 2026-05-15 21:44:36.157195+00
 1b4f3bcf-0d64-464b-aae6-a18a6e60312c | b3f4fb84-bf17-5522-a5f3-70fd1212f381 | e7660606-cadd-4212-e67b-574c09ef789a | 33d52e78-7801-cf3b-bc8b-c798db0d4068 | fea86b3a-6ba7-4c0d-a303-2f70601e1bf7 | 2020-09-13 10:00:00+00 |       0.1 | Low       | Area=b3f4fb84-bf17-5522-a5f3-70fd1212f381; Sensor=e7660606-cadd-4212-e67b-574c09ef789a; Event=fea86b3a-6ba7-4c0d-a303-2f70601e1bf7; Metric=WindSpeed; Value=3,78; BaseRisk=0,10; AdjustedScore=0,10; C=1,00; I=1,00; EligibilityFactor=1,00; ParameterSet=Candidate Parameter Set V1.0 (non-calibrated).    | 2026-05-15 21:44:36.086176+00
(25 rows)


```

## Latest area operational state

```sql
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
```

```text
                  Id                  |                AreaId                |        ConfigurationVersionId        | SimulationRunId |   SnapshotTimestamp    | AggregateRiskScore  | AggregateRiskLevel | Severity |                       Summary                        | AssessmentCount |           UpdatedAt           
--------------------------------------+--------------------------------------+--------------------------------------+-----------------+------------------------+---------------------+--------------------+----------+------------------------------------------------------+-----------------+-------------------------------
 5739af5e-1709-4d14-90fd-cefb9ce0ed1c | b3f4fb84-bf17-5522-a5f3-70fd1212f381 | c03be3d5-1f70-15cb-2fc0-3c86a204a644 | ├ó╦åÔÇª             | 2020-09-13 10:00:20+00 | 0.44799999999999973 | Moderate           | Medium   | Aggregated from 75 assessments; 37 at High or above. |              75 | 2026-05-15 21:44:56.311218+00
(1 row)


```

## Latest cell operational states

```sql
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
```

```text
                  Id                  |                AreaId                |              GridCellId              |               SensorId               |          LatestAssessmentId          |   SnapshotTimestamp    | RiskScore | RiskLevel | Severity |                                                                                                                                                 Summary                                                                                                                                                  |           UpdatedAt           
--------------------------------------+--------------------------------------+--------------------------------------+--------------------------------------+--------------------------------------+------------------------+-----------+-----------+----------+----------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------+-------------------------------
 35e298e3-043b-42f9-86bd-135becebce67 | b3f4fb84-bf17-5522-a5f3-70fd1212f381 | 8a65d4e7-92f5-1b6f-fdb0-d60ec5707d72 | 17badc17-7920-17ac-c213-db1c12bedfb2 | 6bdac3fb-6c39-46b5-9ffd-f8a6e419ed09 | 2020-09-13 10:00:20+00 |       0.3 | Moderate  | Medium   | Area=b3f4fb84-bf17-5522-a5f3-70fd1212f381; Sensor=17badc17-7920-17ac-c213-db1c12bedfb2; Event=16cf048d-5d4a-47be-aae8-5f72e09531d3; Metric=WindSpeed; Value=5,79; BaseRisk=0,30; AdjustedScore=0,30; C=1,00; I=1,00; EligibilityFactor=1,00; ParameterSet=Candidate Parameter Set V1.0 (non-calibrated). | 2026-05-15 21:44:56.28767+00
 98b72b45-ab6b-45ab-ba9b-8c7cebc198e7 | b3f4fb84-bf17-5522-a5f3-70fd1212f381 | 33d52e78-7801-cf3b-bc8b-c798db0d4068 | e7660606-cadd-4212-e67b-574c09ef789a | d5ff106d-550d-48b8-9945-2157ec6d2228 | 2020-09-13 10:00:20+00 |       0.3 | Moderate  | Medium   | Area=b3f4fb84-bf17-5522-a5f3-70fd1212f381; Sensor=e7660606-cadd-4212-e67b-574c09ef789a; Event=e6838c5e-fcf8-4673-848d-25ae334f7374; Metric=WindSpeed; Value=5,66; BaseRisk=0,30; AdjustedScore=0,30; C=1,00; I=1,00; EligibilityFactor=1,00; ParameterSet=Candidate Parameter Set V1.0 (non-calibrated). | 2026-05-15 21:44:56.232437+00
 a32cd3ba-64e1-43fa-957c-7f96b95285e0 | b3f4fb84-bf17-5522-a5f3-70fd1212f381 | eb91abd5-6e37-5b78-b4f4-af8bbcddae0c | 8248f6af-53af-421e-9572-c1016f7dcd3c | b939ca39-6da4-47f6-90ef-d9226294c3f3 | 2020-09-13 10:00:00+00 |       0.1 | Low       | Low      | Area=b3f4fb84-bf17-5522-a5f3-70fd1212f381; Sensor=8248f6af-53af-421e-9572-c1016f7dcd3c; Event=6e83a613-d88e-42be-b56e-9374f5213ed9; Metric=WindSpeed; Value=3,90; Score=0,10.                                                                                                                            | 2026-04-17 14:02:03.167207+00
 e70884d4-19f2-4fb7-8af3-2687bcb6b768 | b3f4fb84-bf17-5522-a5f3-70fd1212f381 | 781b9401-38fa-3838-7051-b636b2299c78 | 0a6058d0-93cf-82de-775b-173b77d1c6c8 | 59836f3e-11f2-467e-9bc6-4346d82450aa | 2020-09-13 10:00:00+00 |       0.1 | Low       | Low      | Area=b3f4fb84-bf17-5522-a5f3-70fd1212f381; Sensor=0a6058d0-93cf-82de-775b-173b77d1c6c8; Event=15ed72a4-6b25-4726-9095-0c53d867a5db; Metric=WindSpeed; Value=3,88; Score=0,10.                                                                                                                            | 2026-04-17 14:02:00.166968+00
 8382a93d-3e46-489f-aa54-879a0eae712b | b3f4fb84-bf17-5522-a5f3-70fd1212f381 | bce40292-f50d-899a-1694-fb6963013d1a | 5032b7d4-b5dd-37ef-8d0a-c39e8032d777 | 4f062947-a158-4a4f-8228-c309d7ff3b5b | 2020-09-13 10:00:00+00 |       0.1 | Low       | Low      | Area=b3f4fb84-bf17-5522-a5f3-70fd1212f381; Sensor=5032b7d4-b5dd-37ef-8d0a-c39e8032d777; Event=668b7bd8-8bba-472c-b1da-7b8e7ca31529; Metric=WindSpeed; Value=3,94; Score=0,10.                                                                                                                            | 2026-04-17 14:01:57.169861+00
 1d369392-73fc-4562-9152-63c9e19734fd | b3f4fb84-bf17-5522-a5f3-70fd1212f381 | dda0e964-245d-23a0-5c14-efcaacc4cbcd | 6bf3bd1a-1b14-14aa-4d60-aaa819a06d04 | 5b9f3867-3f71-4234-8996-b182d962dcf2 | 2020-09-13 10:00:00+00 |       0.1 | Low       | Low      | Area=b3f4fb84-bf17-5522-a5f3-70fd1212f381; Sensor=6bf3bd1a-1b14-14aa-4d60-aaa819a06d04; Event=d5c1ab77-bc5c-43ad-8ca5-582a85325aee; Metric=WindSpeed; Value=3,85; Score=0,10.                                                                                                                            | 2026-04-17 14:01:54.167216+00
 20cf0d18-c1ea-4da3-968d-0423b3407cea | b3f4fb84-bf17-5522-a5f3-70fd1212f381 | 6ca6cc94-3f78-8ad4-416d-a7118d258ca5 | 3a7d26e9-13ab-aaf5-2a75-396268182911 | 6ba5d206-adc0-4d1f-af49-95b170d9af2a | 2020-09-13 10:00:00+00 |       0.1 | Low       | Low      | Area=b3f4fb84-bf17-5522-a5f3-70fd1212f381; Sensor=3a7d26e9-13ab-aaf5-2a75-396268182911; Event=bdc99e9d-2613-404e-ab3f-40568d228e52; Metric=WindSpeed; Value=3,75; Score=0,10.                                                                                                                            | 2026-04-17 14:01:51.168914+00
 1d903f1c-36aa-4e97-9ac7-6dc17229e066 | b3f4fb84-bf17-5522-a5f3-70fd1212f381 | ac273f47-01a9-9aa5-af77-e5ee240d7a48 | 4204b71f-e9f7-982a-1a51-15d27be2560f | 1dd8598a-c7e5-40fa-b404-eeb3c89fe277 | 2020-09-13 10:00:00+00 |       0.1 | Low       | Low      | Area=b3f4fb84-bf17-5522-a5f3-70fd1212f381; Sensor=4204b71f-e9f7-982a-1a51-15d27be2560f; Event=8995a659-be8c-4d70-a7d9-dbdaf924ec0a; Metric=WindSpeed; Value=3,48; Score=0,10.                                                                                                                            | 2026-04-17 14:01:48.168675+00
 d7907d4d-1410-467c-85a1-4d0213d890c2 | b3f4fb84-bf17-5522-a5f3-70fd1212f381 | 8db66edb-a7b3-47b2-f926-742a8ef2a0c2 | 39372933-7a63-f6fb-a00e-3698528f7f5b | 8741a074-0039-44b0-bddb-644f60e5ac96 | 2020-09-13 10:00:00+00 |       0.1 | Low       | Low      | Area=b3f4fb84-bf17-5522-a5f3-70fd1212f381; Sensor=39372933-7a63-f6fb-a00e-3698528f7f5b; Event=71a60f60-005d-4382-8cb5-fc5dd5b11ab8; Metric=WindSpeed; Value=3,69; Score=0,10.                                                                                                                            | 2026-04-17 14:01:45.16813+00
 1e435d0d-9f9c-4c2e-8177-cc099206fe28 | b3f4fb84-bf17-5522-a5f3-70fd1212f381 | f6439c93-84c2-43e8-8e5e-3b70a63bfb5e | 2e3899fe-d6a0-2d49-5be5-8ce3817e3765 | 32af27c3-3767-4b0e-af35-b53f11b5920f | 2020-09-13 10:00:00+00 |       0.1 | Low       | Low      | Area=b3f4fb84-bf17-5522-a5f3-70fd1212f381; Sensor=2e3899fe-d6a0-2d49-5be5-8ce3817e3765; Event=1ff90ce1-3490-46be-b8c9-bf4cb93a38aa; Metric=WindSpeed; Value=3,58; Score=0,10.                                                                                                                            | 2026-04-17 14:01:42.168742+00
 7de06e1f-0cde-4317-bb36-3d441131ea84 | b3f4fb84-bf17-5522-a5f3-70fd1212f381 | a43595b8-bea5-1340-a544-40c633407b21 | 910f146a-44e4-2d67-f4d9-8a5f100649e3 | f6a32e45-6abb-468f-9667-3e2aee4b50a0 | 2020-09-13 10:00:00+00 |       0.1 | Low       | Low      | Area=b3f4fb84-bf17-5522-a5f3-70fd1212f381; Sensor=910f146a-44e4-2d67-f4d9-8a5f100649e3; Event=3414a3bc-6ab8-4de9-a140-24962b4e8cb3; Metric=WindSpeed; Value=3,96; Score=0,10.                                                                                                                            | 2026-04-17 14:01:39.169933+00
 fc4ce67b-0b68-4fe0-80a7-23e38ff84f7f | b3f4fb84-bf17-5522-a5f3-70fd1212f381 | 1eff3195-cc02-4428-b52c-ebfbe74a8f24 | 2c3676c2-18ed-1295-fdef-fe99fad17f01 | 1b123c04-4248-4d6b-8112-4599e463a524 | 2020-09-13 10:00:00+00 |       0.1 | Low       | Low      | Area=b3f4fb84-bf17-5522-a5f3-70fd1212f381; Sensor=2c3676c2-18ed-1295-fdef-fe99fad17f01; Event=a7e733d7-3db7-48de-938e-c014820a4fb4; Metric=WindSpeed; Value=3,51; Score=0,10.                                                                                                                            | 2026-04-17 14:01:36.168847+00
 b3a4be18-4c6e-41dd-8fb8-9c09a8e9565d | b3f4fb84-bf17-5522-a5f3-70fd1212f381 | 0af8a91e-e7fa-b19a-353c-77f7eced9c48 | aedf9241-fd06-ec4e-ccbc-524027232336 | 837deffb-f71d-49d2-bb9f-713670fbabec | 2020-09-13 10:00:00+00 |       0.1 | Low       | Low      | Area=b3f4fb84-bf17-5522-a5f3-70fd1212f381; Sensor=aedf9241-fd06-ec4e-ccbc-524027232336; Event=74c087bc-04de-45e3-b151-d9ebec41cf33; Metric=WindSpeed; Value=3,99; Score=0,10.                                                                                                                            | 2026-04-17 14:01:33.168479+00
 09e0d744-eb64-4c44-be10-7ad761fab6d1 | b3f4fb84-bf17-5522-a5f3-70fd1212f381 | bab19d68-cfdb-bb35-8913-48ac63468f2b | bf5a88f7-1e91-901a-87ed-5c3262d5727d | dfe995db-3f7f-48e4-9cb0-4188f8460e69 | 2020-09-13 10:00:00+00 |       0.1 | Low       | Low      | Area=b3f4fb84-bf17-5522-a5f3-70fd1212f381; Sensor=bf5a88f7-1e91-901a-87ed-5c3262d5727d; Event=ef02025d-df28-437d-b034-f1fa57df4320; Metric=WindSpeed; Value=3,93; Score=0,10.                                                                                                                            | 2026-04-17 14:01:30.169876+00
 d5d0b454-caeb-49a5-8072-5418bc45fe42 | b3f4fb84-bf17-5522-a5f3-70fd1212f381 | 4cd0048e-7381-5074-51a5-09fff2027a53 | 3b88b0b1-ee47-3e11-5ebd-4d380a4190d6 | 06f3655b-423e-40f2-bfa4-3ee50e2c3ed0 | 2020-09-13 10:00:00+00 |       0.1 | Low       | Low      | Area=b3f4fb84-bf17-5522-a5f3-70fd1212f381; Sensor=3b88b0b1-ee47-3e11-5ebd-4d380a4190d6; Event=b42ddfb4-a44c-40d4-8605-af17146992fb; Metric=WindSpeed; Value=3,96; Score=0,10.                                                                                                                            | 2026-04-17 14:01:27.172372+00
 e03d0c86-e83f-45f5-b1b5-b9095bc6bfa2 | b3f4fb84-bf17-5522-a5f3-70fd1212f381 | 03575729-eb8d-f5a2-a449-b1e1994b34b2 | dee2835f-d5f5-e8aa-f9f2-f26591e6bfba | bdf00240-0ae4-4e1b-a114-90975c31d401 | 2020-09-13 10:00:00+00 |       0.1 | Low       | Low      | Area=b3f4fb84-bf17-5522-a5f3-70fd1212f381; Sensor=dee2835f-d5f5-e8aa-f9f2-f26591e6bfba; Event=7f55311d-0344-43be-8590-7843ace5403d; Metric=WindSpeed; Value=3,61; Score=0,10.                                                                                                                            | 2026-04-17 14:01:24.169335+00
 9b4a0c74-9abf-4152-aed9-1cdde7328a31 | b3f4fb84-bf17-5522-a5f3-70fd1212f381 | 401a9f13-1182-c47a-0849-a8878333abae | f97b8269-69eb-2717-61a8-d3e1af29e544 | ce238c2b-1cbf-4ab8-a73b-4ee0a7e6b027 | 2020-09-13 10:00:00+00 |       0.1 | Low       | Low      | Area=b3f4fb84-bf17-5522-a5f3-70fd1212f381; Sensor=f97b8269-69eb-2717-61a8-d3e1af29e544; Event=15e2e9a8-8789-494e-a025-1f14aabe6120; Metric=WindSpeed; Value=3,87; Score=0,10.                                                                                                                            | 2026-04-17 14:01:21.168883+00
 3aeff471-d144-4984-9e37-5c69108bcc21 | b3f4fb84-bf17-5522-a5f3-70fd1212f381 | 13468497-90b4-d197-f193-a259c86faed7 | db307b2d-ef16-e758-f06b-b5d78484ae2c | e75d1b6c-a8cb-4e96-b7b9-0a29545b7928 | 2020-09-13 10:00:00+00 |       0.1 | Low       | Low      | Area=b3f4fb84-bf17-5522-a5f3-70fd1212f381; Sensor=db307b2d-ef16-e758-f06b-b5d78484ae2c; Event=6f930cca-4da4-44e1-9196-fb31b76e8b26; Metric=WindSpeed; Value=3,86; Score=0,10.                                                                                                                            | 2026-04-17 14:01:18.170736+00
 297701aa-34e1-414b-81e6-205f5b68df87 | b3f4fb84-bf17-5522-a5f3-70fd1212f381 | 8bc9fb3b-68d0-5909-04ca-ae0cde6bd2d5 | ad8deeb2-b971-0515-9874-cbbea072092d | caf2471c-a10c-4326-8b9e-f7ac2b1c7519 | 2020-09-13 10:00:00+00 |       0.1 | Low       | Low      | Area=b3f4fb84-bf17-5522-a5f3-70fd1212f381; Sensor=ad8deeb2-b971-0515-9874-cbbea072092d; Event=8c53f0a3-937a-4402-91c1-b593b9cd0104; Metric=WindSpeed; Value=3,71; Score=0,10.                                                                                                                            | 2026-04-17 14:01:15.171357+00
 fb077e11-dc28-41e9-910b-7f7a14b138ce | b3f4fb84-bf17-5522-a5f3-70fd1212f381 | 1b676ea0-7e54-ff5f-76c3-3c3a144df10f | 42ad3205-b963-b0f7-56b0-bf2b8c6f3706 | fc429968-dc9e-4722-8d7b-cbbb91ddd49b | 2020-09-13 10:00:00+00 |       0.1 | Low       | Low      | Area=b3f4fb84-bf17-5522-a5f3-70fd1212f381; Sensor=42ad3205-b963-b0f7-56b0-bf2b8c6f3706; Event=93a69062-24f3-4d2a-bd2e-be3eb756abe6; Metric=WindSpeed; Value=3,52; Score=0,10.                                                                                                                            | 2026-04-17 14:01:12.170803+00
 c4ea9b2d-b6b2-4a96-a397-8dc096a7e232 | b3f4fb84-bf17-5522-a5f3-70fd1212f381 | 8711c546-5be2-1bc0-74c3-8d263111b1d8 | 084a624d-d8d8-ba5d-bd5a-ec536ee71e7a | 8defff82-917c-403b-9245-5d4a79f20044 | 2020-09-13 10:00:00+00 |       0.1 | Low       | Low      | Area=b3f4fb84-bf17-5522-a5f3-70fd1212f381; Sensor=084a624d-d8d8-ba5d-bd5a-ec536ee71e7a; Event=2da983dc-bb56-4831-b11b-c2719317f3ed; Metric=WindSpeed; Value=4,04; Score=0,10.                                                                                                                            | 2026-04-17 14:01:09.169778+00
 8498a03a-d8c6-4e99-adf9-4674b042f0cc | b3f4fb84-bf17-5522-a5f3-70fd1212f381 | 73e4a40e-6f3c-62a9-f6e3-c430295ec952 | 3c95b2c4-c3c5-cfb5-34af-9141413a409f | 88dd2217-fe29-4ee5-997d-58aeb0f069ef | 2020-09-13 10:00:00+00 |       0.1 | Low       | Low      | Area=b3f4fb84-bf17-5522-a5f3-70fd1212f381; Sensor=3c95b2c4-c3c5-cfb5-34af-9141413a409f; Event=5d349920-5275-46af-8052-d5f9c8dc5389; Metric=WindSpeed; Value=3,97; Score=0,10.                                                                                                                            | 2026-04-17 14:01:06.170594+00
 31eee2db-25d0-451b-bf06-8c6d48852cef | b3f4fb84-bf17-5522-a5f3-70fd1212f381 | e60f3f49-915f-1917-4b33-cce98891ad6b | 1685a9d8-bced-5dac-2843-1e77bbff944d | 887f1aee-8d8a-4713-96b6-5fc9cfa299c5 | 2020-09-13 10:00:00+00 |       0.1 | Low       | Low      | Area=b3f4fb84-bf17-5522-a5f3-70fd1212f381; Sensor=1685a9d8-bced-5dac-2843-1e77bbff944d; Event=3d7dfa7b-e2ba-4d6a-b84f-814a14308cb9; Metric=WindSpeed; Value=3,86; Score=0,10.                                                                                                                            | 2026-04-17 14:01:03.171142+00
 9d55bcbf-69f4-48fa-97c9-c39c61704eb9 | b3f4fb84-bf17-5522-a5f3-70fd1212f381 | d7839103-0db1-1d14-da01-b8fb4aa22deb | c2ed094c-0367-b368-dcaa-90d33ed8e32e | c765acd7-8878-4f56-a20b-1071c6105db6 | 2020-09-13 10:00:00+00 |       0.1 | Low       | Low      | Area=b3f4fb84-bf17-5522-a5f3-70fd1212f381; Sensor=c2ed094c-0367-b368-dcaa-90d33ed8e32e; Event=7cb7f890-b484-4555-8498-cc664d3539ff; Metric=WindSpeed; Value=3,85; Score=0,10.                                                                                                                            | 2026-04-17 14:01:00.170724+00
 bca3094b-cf2b-4c1c-9bee-058be03058cf | b3f4fb84-bf17-5522-a5f3-70fd1212f381 | 9be807ba-daec-3119-70dd-0d1ffa3e8796 | 89ba4774-94bd-2ed3-00d3-bbfcd4d232a6 | de011084-95a5-4c24-96e7-2937175e5d23 | 2020-09-13 10:00:00+00 |       0.1 | Low       | Low      | Area=b3f4fb84-bf17-5522-a5f3-70fd1212f381; Sensor=89ba4774-94bd-2ed3-00d3-bbfcd4d232a6; Event=58c5fadb-bdd6-4bfa-b292-e13c18db9478; Metric=WindSpeed; Value=3,63; Score=0,10.                                                                                                                            | 2026-04-17 14:00:57.171851+00
(25 rows)


```

## Latest area risk snapshots

```sql
select *
from projection.area_risk_snapshot_log
order by "SnapshotTimestamp" desc
limit 25;
```

```text
                  Id                  |                AreaId                | SimulationRunId |   SnapshotTimestamp    | AggregateRiskScore  | AggregateRiskLevel |                       Summary                        | AssessmentCount |           CreatedAt           
--------------------------------------+--------------------------------------+-----------------+------------------------+---------------------+--------------------+------------------------------------------------------+-----------------+-------------------------------
 c624f900-cdeb-4afb-a0be-ec79e73ed2b5 | b3f4fb84-bf17-5522-a5f3-70fd1212f381 | ├ó╦åÔÇª             | 2020-09-13 10:09:30+00 | 0.44799999999999973 | Moderate           | Aggregated from 75 assessments; 37 at High or above. |              75 | 2026-04-18 15:04:05.075559+00
 91c62055-7df9-4773-a1d0-e839ca7defa1 | b3f4fb84-bf17-5522-a5f3-70fd1212f381 | ├ó╦åÔÇª             | 2020-09-13 10:09:30+00 | 0.44799999999999973 | Moderate           | Aggregated from 75 assessments; 37 at High or above. |              75 | 2026-04-18 15:04:20.076794+00
 555df351-a0d1-425d-b71d-d81cd5d83b03 | b3f4fb84-bf17-5522-a5f3-70fd1212f381 | ├ó╦åÔÇª             | 2020-09-13 10:09:30+00 | 0.44799999999999973 | Moderate           | Aggregated from 75 assessments; 37 at High or above. |              75 | 2026-04-18 12:41:02.716481+00
 34a79ebb-3984-49b1-a9eb-b52a58cf3823 | b3f4fb84-bf17-5522-a5f3-70fd1212f381 | ├ó╦åÔÇª             | 2020-09-13 10:09:30+00 | 0.44799999999999973 | Moderate           | Aggregated from 75 assessments; 37 at High or above. |              75 | 2026-04-18 12:41:11.723216+00
 2e93149a-55b2-47d3-a677-fbcd10a7afae | b3f4fb84-bf17-5522-a5f3-70fd1212f381 | ├ó╦åÔÇª             | 2020-09-13 10:09:30+00 | 0.44799999999999973 | Moderate           | Aggregated from 75 assessments; 37 at High or above. |              75 | 2026-04-18 15:04:14.077524+00
 9575944e-f560-4c71-bfba-f2804e5b6b86 | b3f4fb84-bf17-5522-a5f3-70fd1212f381 | ├ó╦åÔÇª             | 2020-09-13 10:09:30+00 | 0.44799999999999973 | Moderate           | Aggregated from 75 assessments; 37 at High or above. |              75 | 2026-04-18 15:04:17.075331+00
 40d92d11-57d8-445c-993f-fe9bc31a7749 | b3f4fb84-bf17-5522-a5f3-70fd1212f381 | ├ó╦åÔÇª             | 2020-09-13 10:09:30+00 | 0.44799999999999973 | Moderate           | Aggregated from 75 assessments; 37 at High or above. |              75 | 2026-04-17 21:30:44.72407+00
 e9b665dc-91cf-4efc-aef2-0861b5911d83 | b3f4fb84-bf17-5522-a5f3-70fd1212f381 | ├ó╦åÔÇª             | 2020-09-13 10:09:30+00 | 0.44799999999999973 | Moderate           | Aggregated from 75 assessments; 37 at High or above. |              75 | 2026-04-18 12:40:59.713628+00
 c30cb069-ee81-4aee-9ac5-bbf8f7106ea3 | b3f4fb84-bf17-5522-a5f3-70fd1212f381 | ├ó╦åÔÇª             | 2020-09-13 10:09:30+00 | 0.44799999999999973 | Moderate           | Aggregated from 75 assessments; 37 at High or above. |              75 | 2026-04-17 21:30:41.725903+00
 e165b056-5a7a-41d9-b449-77bae7e81a1a | b3f4fb84-bf17-5522-a5f3-70fd1212f381 | ├ó╦åÔÇª             | 2020-09-13 10:09:30+00 | 0.44799999999999973 | Moderate           | Aggregated from 75 assessments; 37 at High or above. |              75 | 2026-04-18 12:41:08.713355+00
 db5e2bf0-1d68-4487-bbf3-59a90602fa5c | b3f4fb84-bf17-5522-a5f3-70fd1212f381 | ├ó╦åÔÇª             | 2020-09-13 10:09:30+00 | 0.44799999999999973 | Moderate           | Aggregated from 75 assessments; 37 at High or above. |              75 | 2026-04-18 15:04:11.076281+00
 648e88c0-e45d-4190-b1d9-41d6de9f613e | b3f4fb84-bf17-5522-a5f3-70fd1212f381 | ├ó╦åÔÇª             | 2020-09-13 10:09:30+00 | 0.44799999999999973 | Moderate           | Aggregated from 75 assessments; 37 at High or above. |              75 | 2026-04-17 21:30:50.72292+00
 b43a43da-9e4a-4c94-a097-83de4bf0358b | b3f4fb84-bf17-5522-a5f3-70fd1212f381 | ├ó╦åÔÇª             | 2020-09-13 10:09:30+00 | 0.44799999999999973 | Moderate           | Aggregated from 75 assessments; 37 at High or above. |              75 | 2026-04-17 21:30:38.723614+00
 05fbb420-90cc-474a-a276-da53614a7036 | b3f4fb84-bf17-5522-a5f3-70fd1212f381 | ├ó╦åÔÇª             | 2020-09-13 10:09:30+00 | 0.44799999999999973 | Moderate           | Aggregated from 75 assessments; 37 at High or above. |              75 | 2026-04-17 21:30:35.724935+00
 416201dd-99ae-4270-aae6-0ee0ba54abfa | b3f4fb84-bf17-5522-a5f3-70fd1212f381 | ├ó╦åÔÇª             | 2020-09-13 10:09:30+00 | 0.44799999999999973 | Moderate           | Aggregated from 75 assessments; 37 at High or above. |              75 | 2026-04-17 14:08:14.156345+00
 c7853617-377c-41be-8aff-efce89d06466 | b3f4fb84-bf17-5522-a5f3-70fd1212f381 | ├ó╦åÔÇª             | 2020-09-13 10:09:30+00 | 0.44799999999999973 | Moderate           | Aggregated from 75 assessments; 37 at High or above. |              75 | 2026-04-17 14:08:17.16361+00
 5a688735-14e7-445b-9dbe-b04834b482b0 | b3f4fb84-bf17-5522-a5f3-70fd1212f381 | ├ó╦åÔÇª             | 2020-09-13 10:09:30+00 | 0.44799999999999973 | Moderate           | Aggregated from 75 assessments; 37 at High or above. |              75 | 2026-04-18 12:41:05.714916+00
 326986ea-1895-4f23-a562-f19f299fabf1 | b3f4fb84-bf17-5522-a5f3-70fd1212f381 | ├ó╦åÔÇª             | 2020-09-13 10:09:30+00 | 0.44799999999999973 | Moderate           | Aggregated from 75 assessments; 37 at High or above. |              75 | 2026-04-18 12:41:14.715913+00
 a9a9b1c3-14a4-428f-97c6-9a8f046cfb8b | b3f4fb84-bf17-5522-a5f3-70fd1212f381 | ├ó╦åÔÇª             | 2020-09-13 10:09:30+00 | 0.44799999999999973 | Moderate           | Aggregated from 75 assessments; 37 at High or above. |              75 | 2026-04-17 14:08:20.159944+00
 dbc2d2f0-a841-4470-a4d8-241a9b84db66 | b3f4fb84-bf17-5522-a5f3-70fd1212f381 | ├ó╦åÔÇª             | 2020-09-13 10:09:30+00 | 0.44799999999999973 | Moderate           | Aggregated from 75 assessments; 37 at High or above. |              75 | 2026-04-17 14:08:29.160001+00
 1ce4dfd7-63e4-421f-9945-7f0fe275fba1 | b3f4fb84-bf17-5522-a5f3-70fd1212f381 | ├ó╦åÔÇª             | 2020-09-13 10:09:30+00 | 0.44799999999999973 | Moderate           | Aggregated from 75 assessments; 37 at High or above. |              75 | 2026-04-17 14:08:26.161119+00
 37f5787a-5a68-491f-a757-a7e88e159b0c | b3f4fb84-bf17-5522-a5f3-70fd1212f381 | ├ó╦åÔÇª             | 2020-09-13 10:09:30+00 | 0.44799999999999973 | Moderate           | Aggregated from 75 assessments; 37 at High or above. |              75 | 2026-04-17 14:08:23.155307+00
 3a5f6d08-c924-4a6c-99f1-97b3ee440fc2 | b3f4fb84-bf17-5522-a5f3-70fd1212f381 | ├ó╦åÔÇª             | 2020-09-13 10:09:30+00 | 0.44799999999999973 | Moderate           | Aggregated from 75 assessments; 37 at High or above. |              75 | 2026-04-17 21:30:47.721628+00
 c7ce4140-020a-42ce-ab06-fbd2a33e2f90 | b3f4fb84-bf17-5522-a5f3-70fd1212f381 | ├ó╦åÔÇª             | 2020-09-13 10:09:30+00 | 0.44799999999999973 | Moderate           | Aggregated from 75 assessments; 37 at High or above. |              75 | 2026-04-18 15:04:08.083984+00
 dc0e8a9c-9894-46ba-911e-c3ed6aa7409c | b3f4fb84-bf17-5522-a5f3-70fd1212f381 | ├ó╦åÔÇª             | 2020-09-13 10:09:30+00 | 0.44799999999999973 | Moderate           | Aggregated from 75 assessments; 37 at High or above. |              75 | 2026-04-21 13:19:41.832732+00
(25 rows)


```

## Alert state columns

```sql
select column_name, data_type
from information_schema.columns
where table_schema = 'projection'
  and table_name = 'alert_state'
order by ordinal_position;
```

```text
      column_name       |        data_type         
------------------------+--------------------------
 Id                     | uuid
 AreaId                 | uuid
 ConfigurationVersionId | uuid
 AreaOperationalStateId | uuid
 AlertCode              | character varying
 Severity               | character varying
 Status                 | character varying
 Message                | character varying
 TriggeredAt            | timestamp with time zone
 UpdatedAt              | timestamp with time zone
 ResolvedAt             | timestamp with time zone
(11 rows)


```

## Latest alert states

```sql
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
```

```text
                  Id                  |                AreaId                |        ConfigurationVersionId        |        AreaOperationalStateId        |   AlertCode    | Severity |  Status  |              Message               |      TriggeredAt       |           UpdatedAt           |          ResolvedAt           
--------------------------------------+--------------------------------------+--------------------------------------+--------------------------------------+----------------+----------+----------+------------------------------------+------------------------+-------------------------------+-------------------------------
 e3b43c2d-1761-4411-9519-3ec689e38e5d | b3f4fb84-bf17-5522-a5f3-70fd1212f381 | c03be3d5-1f70-15cb-2fc0-3c86a204a644 | 5739af5e-1709-4d14-90fd-cefb9ce0ed1c | area-risk-high | High     | Resolved | Area risk is High with score 0,50. | 2020-09-13 10:01:45+00 | 2026-04-17 13:42:58.79077+00  | 2026-04-17 13:42:58.79077+00
 b2c35736-3227-4990-b240-b3701b5a5691 | b3f4fb84-bf17-5522-a5f3-70fd1212f381 | c03be3d5-1f70-15cb-2fc0-3c86a204a644 | 5739af5e-1709-4d14-90fd-cefb9ce0ed1c | area-risk-high | High     | Resolved | Area risk is High with score 0,50. | 2020-09-13 10:01:25+00 | 2026-04-17 13:08:10.878331+00 | 2026-04-17 13:08:10.878331+00
 29a98399-c523-4f0c-856f-f4044568813f | b3f4fb84-bf17-5522-a5f3-70fd1212f381 | c03be3d5-1f70-15cb-2fc0-3c86a204a644 | 5739af5e-1709-4d14-90fd-cefb9ce0ed1c | area-risk-high | High     | Resolved | Area risk is High with score 0,50. | 2020-09-13 10:00:10+00 | 2026-04-11 16:50:56.671844+00 | 2026-04-11 16:50:56.671844+00
 e1a51d5b-f8a9-4377-be32-62d5be4e310c | b3f4fb84-bf17-5522-a5f3-70fd1212f381 | c03be3d5-1f70-15cb-2fc0-3c86a204a644 | 5739af5e-1709-4d14-90fd-cefb9ce0ed1c | area-risk-high | High     | Resolved | Area risk is High with score 0,50. | 2020-09-13 10:00:00+00 | 2026-04-11 13:44:42.698509+00 | 2026-04-11 13:44:42.698509+00
(4 rows)


```

## Alert states by status

```sql
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
```

```text
   AlertCode    | Severity |  Status  | count |   first_triggered_at   |       last_updated_at        
----------------+----------+----------+-------+------------------------+------------------------------
 area-risk-high | High     | Resolved |     4 | 2020-09-13 10:00:00+00 | 2026-04-17 13:42:58.79077+00
(1 row)


```

## Area operational state joined to alerts

```sql
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
```

```text
                AreaId                | AggregateRiskScore  | AggregateRiskLevel | area_severity |   SnapshotTimestamp    |        area_updated_at        |   AlertCode    | alert_severity | alert_status |           alert_message            |      TriggeredAt       |          ResolvedAt           
--------------------------------------+---------------------+--------------------+---------------+------------------------+-------------------------------+----------------+----------------+--------------+------------------------------------+------------------------+-------------------------------
 b3f4fb84-bf17-5522-a5f3-70fd1212f381 | 0.44799999999999973 | Moderate           | Medium        | 2020-09-13 10:00:20+00 | 2026-05-15 21:44:56.311218+00 | area-risk-high | High           | Resolved     | Area risk is High with score 0,50. | 2020-09-13 10:01:45+00 | 2026-04-17 13:42:58.79077+00
 b3f4fb84-bf17-5522-a5f3-70fd1212f381 | 0.44799999999999973 | Moderate           | Medium        | 2020-09-13 10:00:20+00 | 2026-05-15 21:44:56.311218+00 | area-risk-high | High           | Resolved     | Area risk is High with score 0,50. | 2020-09-13 10:01:25+00 | 2026-04-17 13:08:10.878331+00
 b3f4fb84-bf17-5522-a5f3-70fd1212f381 | 0.44799999999999973 | Moderate           | Medium        | 2020-09-13 10:00:20+00 | 2026-05-15 21:44:56.311218+00 | area-risk-high | High           | Resolved     | Area risk is High with score 0,50. | 2020-09-13 10:00:10+00 | 2026-04-11 16:50:56.671844+00
 b3f4fb84-bf17-5522-a5f3-70fd1212f381 | 0.44799999999999973 | Moderate           | Medium        | 2020-09-13 10:00:20+00 | 2026-05-15 21:44:56.311218+00 | area-risk-high | High           | Resolved     | Area risk is High with score 0,50. | 2020-09-13 10:00:00+00 | 2026-04-11 13:44:42.698509+00
(4 rows)


```

## Blocked or zero risk probe

```sql
select
  count(*) filter (where "RiskScore" = 0) as zero_risk_assessments,
  count(*) filter (where lower("ExplanationSummary") like '%blocked%') as explanations_containing_blocked,
  count(*) filter (where lower("ExplanationSummary") like '%partial%') as explanations_containing_partial
from projection.risk_assessment_log;
```

```text
 zero_risk_assessments | explanations_containing_blocked | explanations_containing_partial 
-----------------------+---------------------------------+---------------------------------
                     0 |                               0 |                               0
(1 row)


```

## API operational state

```text
{
    "areaCode":  "proenca-a-nova",
    "configurationVersionNumber":  1,
    "snapshotTimestamp":  "2020-09-13T10:00:20+00:00",
    "aggregateRiskScore":  0.44799999999999973,
    "aggregateRiskLevel":  "Moderate",
    "severity":  "Medium",
    "summary":  "Aggregated from 75 assessments; 37 at High or above.",
    "assessmentCount":  75,
    "updatedAt":  "2026-05-15T21:44:56.311218+00:00",
    "alertState":  null
}

```

## API active alerts

```text
{
    "value":  [

              ],
    "Count":  0
}

```

## Technical verdict template

`	ext
Veredicto: preencher manualmente apÃ³s leitura dos resultados
ConfianÃ§a: baixa / mÃ©dia / alta

Checks:
- Infra viva: verificar Docker containers.
- Control plane carregado: verificar configuraÃ§Ã£o ativa, Ã¡rea, cÃ©lulas, sensores, cenÃ¡rios.
- Pipeline processou eventos: verificar inbox_total, status e attempts.
- Erros explicados: verificar rejected/quarantined/errors.
- ProjeÃ§Ãµes existem: verificar accepted_reading_log, risk_assessment_log, snapshots, states.
- API reflete DB: comparar operational-state com area_operational_state.
- Alert policy: verificar alert_state e/ou testes se nÃ£o houver cenÃ¡rio acima de thresholds.
- Blocked != zero risk: verificar zero_risk_assessments e evidÃªncia de eligibility/testes.
`"

Add-Section 
