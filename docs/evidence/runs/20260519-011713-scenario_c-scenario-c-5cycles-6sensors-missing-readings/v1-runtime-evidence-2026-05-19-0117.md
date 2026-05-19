# NatureProtector V1 Runtime Evidence

- GeneratedAt: 2026-05-19T01:17:39.3051165+02:00
- PostgresContainer: np-postgres
- Database: natureprotector
- ApiBaseUrl: http://localhost:5254

## Git branch

```text
master

```

## Git commit

```text
8dac9da9408d99b14c26ea312da4d3c536eeafcb

```

## Git status

```text
## master...origin/master
 M docs/evidence/dev-runtime/20260518-194733/backoffice-api.log
 M docs/evidence/dev-runtime/20260518-194733/prevention-host.log
 M src/NatureProtector.Simulator.Host/Services/ReadingGenerationService.cs
 M tests/NatureProtector.Simulator.Host.Tests/Services/ReadingGenerationServiceTests.cs
 M tests/NatureProtector.Simulator.Host.Tests/Services/SimulationRunnerTests.cs
?? docs/evidence/dev-runtime/20260518-222347-scenario-c-from-ui/
?? docs/evidence/dev-runtime/20260518-222524-scenario-c-from-ui/
?? docs/evidence/dev-runtime/20260518-222645-scenario-c-from-ui/
?? docs/evidence/dev-runtime/20260519-002616/
?? docs/evidence/progress-2026-05-22/
?? docs/evidence/runs/20260519-011514-scenario_b-scenario-b-5cycles-6sensors-smoke/
?? docs/evidence/runs/20260519-011713-scenario_c-scenario-c-5cycles-6sensors-missing-readings/

```

## Docker containers

```text
NAMES         IMAGE                                      STATUS          PORTS
np-influxdb   influxdb:3.7.0-core                        Up 51 minutes   0.0.0.0:8181->8181/tcp
np-grafana    grafana/grafana-enterprise:13.0.1-ubuntu   Up 51 minutes   0.0.0.0:3000->3000/tcp
np-postgres   postgres:16                                Up 51 minutes   0.0.0.0:5433->5432/tcp
np-rabbitmq   rabbitmq:4.0.6-management                  Up 51 minutes   4369/tcp, 5671/tcp, 0.0.0.0:5672->5672/tcp, 15671/tcp, 15691-15692/tcp, 25672/tcp, 0.0.0.0:15672->15672/tcp

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
 projection   | daily_cell_state
 projection   | risk_assessment_log
(23 rows)


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
 projection   | area_operational_state    | AlertCooldownUntil             | timestamp with time zone
 projection   | area_operational_state    | PendingAlertCycles             | integer
 projection   | area_operational_state    | PendingAlertState              | character varying
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
 projection   | daily_cell_state          | Id                             | uuid
 projection   | daily_cell_state          | AreaId                         | uuid
 projection   | daily_cell_state          | GridCellId                     | uuid
 projection   | daily_cell_state          | SensorId                       | uuid
 projection   | daily_cell_state          | SimulationRunId                | uuid
 projection   | daily_cell_state          | ConfigurationVersionId         | uuid
 projection   | daily_cell_state          | LogicalDate                    | timestamp with time zone
 projection   | daily_cell_state          | DailyPrecipitationMillimeters  | double precision
 projection   | daily_cell_state          | MaxTemperatureCelsius          | double precision
 projection   | daily_cell_state          | LatestHumidityPercent          | double precision
 projection   | daily_cell_state          | LatestWindSpeedMetersPerSecond | double precision
 projection   | daily_cell_state          | AntecedentState                | character varying
 projection   | daily_cell_state          | DroughtContext                 | character varying
 projection   | daily_cell_state          | CandidateParameterSetVersion   | character varying
 projection   | daily_cell_state          | Provenance                     | character varying
 projection   | daily_cell_state          | LastSourceEventId              | uuid
 projection   | daily_cell_state          | LastUpdatedAt                  | timestamp with time zone
 projection   | daily_cell_state          | CreatedAt                      | timestamp with time zone
 projection   | daily_cell_state          | UpdatedAt                      | timestamp with time zone
 projection   | daily_cell_state          | FireIndexProvenance            | character varying
 projection   | daily_cell_state          | FireWeatherIndex               | double precision
 projection   | daily_cell_state          | KeetchByramDroughtIndex        | double precision
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
 projection   | risk_assessment_log       | SimulationRunId                | uuid
(247 rows)


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
                      1 |     1 |        467 |           75 |               3 |                    3 |               4
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
 c03be3d5-1f70-15cb-2fc0-3c86a204a644 |             1 | t        | Bootstrap control-plane import for Proenca-a-Nova with pilot sensor network. | 2026-05-17 23:50:35.654527+00 | phase-04-bootstrap
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
 b3f4fb84-bf17-5522-a5f3-70fd1212f381 | c03be3d5-1f70-15cb-2fc0-3c86a204a644 | proenca-a-nova | Proença-a-Nova | PT
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
                  Id                  |                AreaId                |              ScenarioId              |        ConfigurationVersionId        | ScenarioCode |                 ScenarioName                  |           CreatedAt           |           StartedAt           |            EndedAt            | LogicalStartTimestamp  | IntervalSeconds | NumberOfCycles | ExecutionSeed | Status |                                                                                                                                                                                                                                                                                                                                                MetadataJson                                                                                                                                                                                                                                                                                                                                                
--------------------------------------+--------------------------------------+--------------------------------------+--------------------------------------+--------------+-----------------------------------------------+-------------------------------+-------------------------------+-------------------------------+------------------------+-----------------+----------------+---------------+--------+------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------
 36caca67-352c-41f1-80e3-8fe951a1582c | b3f4fb84-bf17-5522-a5f3-70fd1212f381 | 319bae31-9b7a-57cf-9b05-3eefdf320c95 | c03be3d5-1f70-15cb-2fc0-3c86a204a644 | scenario_c   | Scenario C - Degraded Pipeline Proenca-a-Nova | 2026-05-18 23:17:17.232634+00 | 2026-05-18 23:17:17.369821+00 | 2026-05-18 23:17:37.682816+00 | 2020-09-13 10:00:00+00 |               5 |              5 |         12345 |      3 | {"sensor_count":6,"scenario_category":"Failure","orchestrator_correlation_id":"1369b8e7-7b9a-4842-a32e-5beaab3ebc12","run_overrides":{"requested":{"sensor_count":6,"number_of_cycles":5,"interval_seconds":5,"seed":12345,"degradation_profile":"missing-readings","orchestrator_correlation_id":"1369b8e7-7b9a-4842-a32e-5beaab3ebc12"},"resolved":{"sensor_count":6,"number_of_cycles":5,"interval_seconds":5,"seed":12345,"degradation_profile":"missing-readings","orchestrator_correlation_id":"1369b8e7-7b9a-4842-a32e-5beaab3ebc12","selected_sensor_names":["pilot-humidity-0001","pilot-humidity-0230","pilot-temperature-0001","pilot-temperature-0230","pilot-wind-0001","pilot-wind-0230"]}}}
 d8203d4b-1839-4908-87ef-05633c1f1ae5 | b3f4fb84-bf17-5522-a5f3-70fd1212f381 | 6034251c-b502-5f7b-a557-7187178cfb04 | c03be3d5-1f70-15cb-2fc0-3c86a204a644 | scenario_b   | Scenario B - High Risk Proenca-a-Nova         | 2026-05-18 23:15:19.164093+00 | 2026-05-18 23:15:19.38574+00  | 2026-05-18 23:15:39.73622+00  | 2020-09-13 10:00:00+00 |               5 |              5 |         12345 |      3 | {"sensor_count":6,"scenario_category":"HighRisk","orchestrator_correlation_id":"0e1b4017-40ba-45a4-9ae0-bb3b73ed4a36","run_overrides":{"requested":{"sensor_count":6,"number_of_cycles":5,"interval_seconds":5,"seed":12345,"degradation_profile":"none","orchestrator_correlation_id":"0e1b4017-40ba-45a4-9ae0-bb3b73ed4a36"},"resolved":{"sensor_count":6,"number_of_cycles":5,"interval_seconds":5,"seed":12345,"degradation_profile":"none","orchestrator_correlation_id":"0e1b4017-40ba-45a4-9ae0-bb3b73ed4a36","selected_sensor_names":["pilot-humidity-0001","pilot-humidity-0230","pilot-temperature-0001","pilot-temperature-0230","pilot-wind-0001","pilot-wind-0230"]}}}
 44327172-046b-4320-b2b0-1205b4f7827e | b3f4fb84-bf17-5522-a5f3-70fd1212f381 | 319bae31-9b7a-57cf-9b05-3eefdf320c95 | c03be3d5-1f70-15cb-2fc0-3c86a204a644 | scenario_c   | Scenario C - Degraded Pipeline Proenca-a-Nova | 2026-05-18 22:26:48.951863+00 | 2026-05-18 22:26:49.082674+00 | 2026-05-18 22:27:09.250524+00 | 2020-09-13 10:00:00+00 |               5 |              5 |         12345 |      3 | {"sensor_count":6,"scenario_category":"Failure","orchestrator_correlation_id":"83f91aeb-51c9-40a1-aae6-2d56b098ff1e","run_overrides":{"requested":{"sensor_count":6,"number_of_cycles":5,"interval_seconds":5,"seed":12345,"degradation_profile":"missing-readings","orchestrator_correlation_id":"83f91aeb-51c9-40a1-aae6-2d56b098ff1e"},"resolved":{"sensor_count":6,"number_of_cycles":5,"interval_seconds":5,"seed":12345,"degradation_profile":"missing-readings","orchestrator_correlation_id":"83f91aeb-51c9-40a1-aae6-2d56b098ff1e","selected_sensor_names":["pilot-humidity-0001","pilot-humidity-0230","pilot-temperature-0001","pilot-temperature-0230","pilot-wind-0001","pilot-wind-0230"]}}}
 33426c68-be78-46ae-8026-a03935b7daad | b3f4fb84-bf17-5522-a5f3-70fd1212f381 | 6034251c-b502-5f7b-a557-7187178cfb04 | c03be3d5-1f70-15cb-2fc0-3c86a204a644 | scenario_b   | Scenario B - High Risk Proenca-a-Nova         | 2026-05-18 17:55:08.665907+00 | 2026-05-18 17:55:08.810153+00 | 2026-05-18 17:55:29.069849+00 | 2020-09-13 10:00:00+00 |               5 |              5 |         12345 |      3 | {"sensor_count":6,"scenario_category":"HighRisk","orchestrator_correlation_id":"7895953d-9d50-4baa-83fb-4c47408c89bb","run_overrides":{"requested":{"sensor_count":6,"number_of_cycles":5,"interval_seconds":5,"seed":12345,"degradation_profile":"none","orchestrator_correlation_id":"7895953d-9d50-4baa-83fb-4c47408c89bb"},"resolved":{"sensor_count":6,"number_of_cycles":5,"interval_seconds":5,"seed":12345,"degradation_profile":"none","orchestrator_correlation_id":"7895953d-9d50-4baa-83fb-4c47408c89bb","selected_sensor_names":["pilot-humidity-0001","pilot-humidity-0230","pilot-temperature-0001","pilot-temperature-0230","pilot-wind-0001","pilot-wind-0230"]}}}
(4 rows)


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
         101 |            101 |              6 |                 0
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
      1 |     1
      2 |   100
(2 rows)


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
 2026-05-18 17:55:09.105675+00 | 2026-05-18 23:17:41.172144+00 | 2020-09-13 10:00:00+00 | 2020-09-13 10:00:20+00
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
 6eb50f0a-aea5-4ff7-868b-59bdee3f7700 | f35ef8b9-2ea4-4455-aad3-9b88797cffb7 | SensorReadingProduced | NatureProtector.Simulator.Host |      1 |            1 | 2020-09-13 10:00:20+00 | 2026-05-18 23:17:41.172144+00 | 2026-05-18 23:17:41.172144+00 | âˆ…                           | âˆ…           | âˆ…              | âˆ…
 cb79ab0c-c720-4157-91e3-1d571efd715f | 7a9887f3-0931-4e72-b42c-0477c1467f59 | SensorReadingProduced | NatureProtector.Simulator.Host |      2 |            1 | 2020-09-13 10:00:20+00 | 2026-05-18 23:17:40.173248+00 | 2026-05-18 23:17:41.157688+00 | 2026-05-18 23:17:41.157688+00 | âˆ…           | âˆ…              | âˆ…
 c82a48d5-17c7-4b30-a8cb-055ce661e7ab | 6c56cfc8-ee22-4b53-94fa-da4a43c2167f | SensorReadingProduced | NatureProtector.Simulator.Host |      2 |            1 | 2020-09-13 10:00:20+00 | 2026-05-18 23:17:39.170512+00 | 2026-05-18 23:17:40.158412+00 | 2026-05-18 23:17:40.158412+00 | âˆ…           | âˆ…              | âˆ…
 9ecf3641-4f1a-4776-bcf4-086e0749df87 | be6f0960-04c5-402c-926e-931d1d4cb4af | SensorReadingProduced | NatureProtector.Simulator.Host |      2 |            1 | 2020-09-13 10:00:20+00 | 2026-05-18 23:17:38.170594+00 | 2026-05-18 23:17:39.157297+00 | 2026-05-18 23:17:39.157297+00 | âˆ…           | âˆ…              | âˆ…
 a215f404-2f8c-49bb-af6a-536b21c39496 | 72cf15a1-18af-41af-8fda-1566b41bc119 | SensorReadingProduced | NatureProtector.Simulator.Host |      2 |            1 | 2020-09-13 10:00:20+00 | 2026-05-18 23:17:37.661191+00 | 2026-05-18 23:17:38.156936+00 | 2026-05-18 23:17:38.156936+00 | âˆ…           | âˆ…              | âˆ…
 4dd53aa8-58ad-4a4c-99af-d669d69ef649 | d883efbf-a051-41b7-b45d-b89554d4a3ee | SensorReadingProduced | NatureProtector.Simulator.Host |      2 |            1 | 2020-09-13 10:00:15+00 | 2026-05-18 23:17:35.170869+00 | 2026-05-18 23:17:36.156613+00 | 2026-05-18 23:17:36.156613+00 | âˆ…           | âˆ…              | âˆ…
 0197f84c-0628-448c-9390-27dd6d69fb47 | 6a584250-bcf8-4e07-91b6-41023097adee | SensorReadingProduced | NatureProtector.Simulator.Host |      2 |            1 | 2020-09-13 10:00:15+00 | 2026-05-18 23:17:34.170102+00 | 2026-05-18 23:17:35.157642+00 | 2026-05-18 23:17:35.157642+00 | âˆ…           | âˆ…              | âˆ…
 4287fbbe-a3fc-4c8b-9c77-d2313aeec37b | 5b5b3055-4545-4962-b650-80488e8d4488 | SensorReadingProduced | NatureProtector.Simulator.Host |      2 |            1 | 2020-09-13 10:00:15+00 | 2026-05-18 23:17:33.169906+00 | 2026-05-18 23:17:34.157061+00 | 2026-05-18 23:17:34.157061+00 | âˆ…           | âˆ…              | âˆ…
 ce9b5785-1c97-4993-93a5-fd12244b6de2 | 89972a4e-ff2e-4bd4-b2ab-0fbaccd6b55f | SensorReadingProduced | NatureProtector.Simulator.Host |      2 |            1 | 2020-09-13 10:00:15+00 | 2026-05-18 23:17:32.635474+00 | 2026-05-18 23:17:33.156933+00 | 2026-05-18 23:17:33.156933+00 | âˆ…           | âˆ…              | âˆ…
 4f34769d-7075-44bf-93fc-6153f83942e7 | 4c640d15-14a4-458a-a685-bf2321a43199 | SensorReadingProduced | NatureProtector.Simulator.Host |      2 |            1 | 2020-09-13 10:00:10+00 | 2026-05-18 23:17:31.170115+00 | 2026-05-18 23:17:32.156554+00 | 2026-05-18 23:17:32.156554+00 | âˆ…           | âˆ…              | âˆ…
 61d7cd8a-0921-4d56-b0d1-abee1b53500e | 1b7d59c1-8883-4f34-83bf-66605a8ef080 | SensorReadingProduced | NatureProtector.Simulator.Host |      2 |            1 | 2020-09-13 10:00:10+00 | 2026-05-18 23:17:30.171905+00 | 2026-05-18 23:17:31.156461+00 | 2026-05-18 23:17:31.156461+00 | âˆ…           | âˆ…              | âˆ…
 7bcdc3b8-423a-4f65-9471-53b5e7046a26 | 1dbec683-04d3-4756-ae2a-bf05c95554e7 | SensorReadingProduced | NatureProtector.Simulator.Host |      2 |            1 | 2020-09-13 10:00:10+00 | 2026-05-18 23:17:29.17108+00  | 2026-05-18 23:17:30.158426+00 | 2026-05-18 23:17:30.158426+00 | âˆ…           | âˆ…              | âˆ…
 26babef4-b4d3-4040-8176-6dcc895a13cd | 6622f1ef-eecc-4a09-b6d5-fc017408e73e | SensorReadingProduced | NatureProtector.Simulator.Host |      2 |            1 | 2020-09-13 10:00:10+00 | 2026-05-18 23:17:28.170723+00 | 2026-05-18 23:17:29.157168+00 | 2026-05-18 23:17:29.157168+00 | âˆ…           | âˆ…              | âˆ…
 a9a5cbc9-b281-48b6-8112-a03080d23c82 | 761ca7f4-60ea-4d5c-817a-9acd0f040b10 | SensorReadingProduced | NatureProtector.Simulator.Host |      2 |            1 | 2020-09-13 10:00:10+00 | 2026-05-18 23:17:27.602517+00 | 2026-05-18 23:17:28.15708+00  | 2026-05-18 23:17:28.15708+00  | âˆ…           | âˆ…              | âˆ…
 09272d89-96b3-4a84-8b03-9911398cf61c | c6687802-7679-41d0-ae8e-f7d28ebfcb0f | SensorReadingProduced | NatureProtector.Simulator.Host |      2 |            1 | 2020-09-13 10:00:05+00 | 2026-05-18 23:17:26.169944+00 | 2026-05-18 23:17:27.157253+00 | 2026-05-18 23:17:27.157253+00 | âˆ…           | âˆ…              | âˆ…
 abc75398-e958-4206-ae23-3de3ec404110 | 05843117-15c4-440e-87d5-639b5f27b606 | SensorReadingProduced | NatureProtector.Simulator.Host |      2 |            1 | 2020-09-13 10:00:05+00 | 2026-05-18 23:17:25.169616+00 | 2026-05-18 23:17:26.156292+00 | 2026-05-18 23:17:26.156292+00 | âˆ…           | âˆ…              | âˆ…
 d5dc440e-9701-4c07-b958-af037c60e06b | a57dbd7d-b18f-48e4-910e-2ea9cfe33f8b | SensorReadingProduced | NatureProtector.Simulator.Host |      2 |            1 | 2020-09-13 10:00:05+00 | 2026-05-18 23:17:24.172566+00 | 2026-05-18 23:17:25.156259+00 | 2026-05-18 23:17:25.156259+00 | âˆ…           | âˆ…              | âˆ…
 4cd109fa-f364-48cd-97f5-d6c8a7193917 | 1f9121b7-0c12-4c08-972e-867ad36878f5 | SensorReadingProduced | NatureProtector.Simulator.Host |      2 |            1 | 2020-09-13 10:00:05+00 | 2026-05-18 23:17:23.170612+00 | 2026-05-18 23:17:24.159445+00 | 2026-05-18 23:17:24.159445+00 | âˆ…           | âˆ…              | âˆ…
 41cd837c-fff3-40eb-8ade-e05bafb15eb2 | 028114ee-9656-49fc-a584-6732994f43af | SensorReadingProduced | NatureProtector.Simulator.Host |      2 |            1 | 2020-09-13 10:00:05+00 | 2026-05-18 23:17:22.570103+00 | 2026-05-18 23:17:23.156787+00 | 2026-05-18 23:17:23.156787+00 | âˆ…           | âˆ…              | âˆ…
 4e560d9c-6320-4e46-a74c-aacb4d775fca | 20c0ca80-4d7a-4293-a0be-7ec0dcc36815 | SensorReadingProduced | NatureProtector.Simulator.Host |      2 |            1 | 2020-09-13 10:00:00+00 | 2026-05-18 23:17:20.170687+00 | 2026-05-18 23:17:21.158434+00 | 2026-05-18 23:17:21.158434+00 | âˆ…           | âˆ…              | âˆ…
 1f8bd5a8-7095-447a-93f6-1fd1a695ea49 | c0266f97-dfa8-4d66-8e06-fe39a8314e3b | SensorReadingProduced | NatureProtector.Simulator.Host |      2 |            1 | 2020-09-13 10:00:00+00 | 2026-05-18 23:17:19.17123+00  | 2026-05-18 23:17:20.157206+00 | 2026-05-18 23:17:20.157206+00 | âˆ…           | âˆ…              | âˆ…
 97b491ec-bf99-47c1-9682-3e5657f1f5c0 | 4c846051-0a23-4eed-ac2f-c65c23122f05 | SensorReadingProduced | NatureProtector.Simulator.Host |      2 |            1 | 2020-09-13 10:00:00+00 | 2026-05-18 23:17:18.17144+00  | 2026-05-18 23:17:19.15771+00  | 2026-05-18 23:17:19.15771+00  | âˆ…           | âˆ…              | âˆ…
 a97f6525-03a9-473e-b85b-d66b711bf715 | 4bf9ff40-9c55-4813-804f-93a24068530f | SensorReadingProduced | NatureProtector.Simulator.Host |      2 |            1 | 2020-09-13 10:00:00+00 | 2026-05-18 23:17:17.534775+00 | 2026-05-18 23:17:18.157294+00 | 2026-05-18 23:17:18.157294+00 | âˆ…           | âˆ…              | âˆ…
 ca6f4904-f6ca-403e-b55d-f0b0ae13dbfe | 7dc91903-2d96-4b65-b7fa-f5161599e1a1 | SensorReadingProduced | NatureProtector.Simulator.Host |      2 |            1 | 2020-09-13 10:00:20+00 | 2026-05-18 23:15:48.175533+00 | 2026-05-18 23:15:49.161391+00 | 2026-05-18 23:15:49.161391+00 | âˆ…           | âˆ…              | âˆ…
 c8364810-d17f-4572-950b-9f2e8e793612 | bbab86b1-45cd-4411-b82a-33566287e3d1 | SensorReadingProduced | NatureProtector.Simulator.Host |      2 |            1 | 2020-09-13 10:00:20+00 | 2026-05-18 23:15:47.177613+00 | 2026-05-18 23:15:48.161+00    | 2026-05-18 23:15:48.161+00    | âˆ…           | âˆ…              | âˆ…
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
 LastErrorCode | LastErrorMessage | count 
---------------+------------------+-------
(0 rows)


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
 ada54860-be17-404b-a8e6-b51e8d474ac1 | b293a4ff-c04a-4b91-bef6-ec9b5b77340a |             1 | reading_risk_pipeline | 2026-05-18 23:17:42.172248+00 | âˆ…                           |       0 | âˆ…       | âˆ…
 9e743ad3-fa43-4589-a527-979147dc0423 | 6eb50f0a-aea5-4ff7-868b-59bdee3f7700 |             1 | reading_risk_pipeline | 2026-05-18 23:17:41.172144+00 | 2026-05-18 23:17:42.157993+00 |       1 | âˆ…       | âˆ…
 62efd47b-1759-4f67-a895-80d55941edef | cb79ab0c-c720-4157-91e3-1d571efd715f |             1 | reading_risk_pipeline | 2026-05-18 23:17:40.173248+00 | 2026-05-18 23:17:41.157688+00 |       1 | âˆ…       | âˆ…
 fee994aa-3f19-4014-8bfe-8ece8293ef89 | c82a48d5-17c7-4b30-a8cb-055ce661e7ab |             1 | reading_risk_pipeline | 2026-05-18 23:17:39.170512+00 | 2026-05-18 23:17:40.158412+00 |       1 | âˆ…       | âˆ…
 d59990ac-f97d-4528-97a2-79e0d43ab248 | 9ecf3641-4f1a-4776-bcf4-086e0749df87 |             1 | reading_risk_pipeline | 2026-05-18 23:17:38.170594+00 | 2026-05-18 23:17:39.157297+00 |       1 | âˆ…       | âˆ…
 c066e485-947d-4378-8df3-ea570728625b | a215f404-2f8c-49bb-af6a-536b21c39496 |             1 | reading_risk_pipeline | 2026-05-18 23:17:37.661191+00 | 2026-05-18 23:17:38.156936+00 |       1 | âˆ…       | âˆ…
 c633ca12-5b5f-49e4-836b-1361a29c535e | 4dd53aa8-58ad-4a4c-99af-d669d69ef649 |             1 | reading_risk_pipeline | 2026-05-18 23:17:35.170869+00 | 2026-05-18 23:17:36.156613+00 |       1 | âˆ…       | âˆ…
 fc9af3b6-f61b-4646-ac41-b3bfd7eca53d | 0197f84c-0628-448c-9390-27dd6d69fb47 |             1 | reading_risk_pipeline | 2026-05-18 23:17:34.170102+00 | 2026-05-18 23:17:35.157642+00 |       1 | âˆ…       | âˆ…
 749b71d7-f90d-4897-8d1c-189a467c196b | 4287fbbe-a3fc-4c8b-9c77-d2313aeec37b |             1 | reading_risk_pipeline | 2026-05-18 23:17:33.169906+00 | 2026-05-18 23:17:34.157061+00 |       1 | âˆ…       | âˆ…
 e8f0db1c-d83a-4285-b2c8-a9975f541885 | ce9b5785-1c97-4993-93a5-fd12244b6de2 |             1 | reading_risk_pipeline | 2026-05-18 23:17:32.635474+00 | 2026-05-18 23:17:33.156933+00 |       1 | âˆ…       | âˆ…
 5f07a508-5980-4ab2-ba64-90938954a9bb | 4f34769d-7075-44bf-93fc-6153f83942e7 |             1 | reading_risk_pipeline | 2026-05-18 23:17:31.170115+00 | 2026-05-18 23:17:32.156554+00 |       1 | âˆ…       | âˆ…
 63765faa-eeeb-4935-b1dc-1a28a73b38dd | 61d7cd8a-0921-4d56-b0d1-abee1b53500e |             1 | reading_risk_pipeline | 2026-05-18 23:17:30.171905+00 | 2026-05-18 23:17:31.156461+00 |       1 | âˆ…       | âˆ…
 912f2b7b-c804-4528-bddf-138d91791e8c | 7bcdc3b8-423a-4f65-9471-53b5e7046a26 |             1 | reading_risk_pipeline | 2026-05-18 23:17:29.17108+00  | 2026-05-18 23:17:30.158426+00 |       1 | âˆ…       | âˆ…
 e3d32893-9b8f-49bb-9256-a5ab4438abd0 | 26babef4-b4d3-4040-8176-6dcc895a13cd |             1 | reading_risk_pipeline | 2026-05-18 23:17:28.170723+00 | 2026-05-18 23:17:29.157168+00 |       1 | âˆ…       | âˆ…
 88190ef4-49d4-4a18-84b6-d166a6675180 | a9a5cbc9-b281-48b6-8112-a03080d23c82 |             1 | reading_risk_pipeline | 2026-05-18 23:17:27.602517+00 | 2026-05-18 23:17:28.15708+00  |       1 | âˆ…       | âˆ…
 8c7da3e2-882c-4942-a0c4-60efdaa890ae | 09272d89-96b3-4a84-8b03-9911398cf61c |             1 | reading_risk_pipeline | 2026-05-18 23:17:26.169944+00 | 2026-05-18 23:17:27.157253+00 |       1 | âˆ…       | âˆ…
 7e77f6cf-757a-4fb8-914d-5fc5599636cc | abc75398-e958-4206-ae23-3de3ec404110 |             1 | reading_risk_pipeline | 2026-05-18 23:17:25.169616+00 | 2026-05-18 23:17:26.156292+00 |       1 | âˆ…       | âˆ…
 31bfeeb6-9507-4e8f-b1a2-46510fa43a59 | d5dc440e-9701-4c07-b958-af037c60e06b |             1 | reading_risk_pipeline | 2026-05-18 23:17:24.172566+00 | 2026-05-18 23:17:25.156259+00 |       1 | âˆ…       | âˆ…
 fe8c110c-a70e-4e5d-81ca-3ad9523fb001 | 4cd109fa-f364-48cd-97f5-d6c8a7193917 |             1 | reading_risk_pipeline | 2026-05-18 23:17:23.170612+00 | 2026-05-18 23:17:24.159445+00 |       1 | âˆ…       | âˆ…
 324c78d5-e18a-45e2-8e99-4d98bd9d176c | 41cd837c-fff3-40eb-8ade-e05bafb15eb2 |             1 | reading_risk_pipeline | 2026-05-18 23:17:22.570103+00 | 2026-05-18 23:17:23.156787+00 |       1 | âˆ…       | âˆ…
 b431509c-db54-4d5a-95ac-63b26a34b887 | 4e560d9c-6320-4e46-a74c-aacb4d775fca |             1 | reading_risk_pipeline | 2026-05-18 23:17:20.170687+00 | 2026-05-18 23:17:21.158434+00 |       1 | âˆ…       | âˆ…
 7f57bb3e-73ee-4017-a77c-ffc479a668f4 | 1f8bd5a8-7095-447a-93f6-1fd1a695ea49 |             1 | reading_risk_pipeline | 2026-05-18 23:17:19.17123+00  | 2026-05-18 23:17:20.157206+00 |       1 | âˆ…       | âˆ…
 0345cb4b-7ec5-4be5-95e3-100bc78bb52c | 97b491ec-bf99-47c1-9682-3e5657f1f5c0 |             1 | reading_risk_pipeline | 2026-05-18 23:17:18.17144+00  | 2026-05-18 23:17:19.15771+00  |       1 | âˆ…       | âˆ…
 22dc93db-392d-4730-974c-19a64690e13b | a97f6525-03a9-473e-b85b-d66b711bf715 |             1 | reading_risk_pipeline | 2026-05-18 23:17:17.534775+00 | 2026-05-18 23:17:18.157294+00 |       1 | âˆ…       | âˆ…
 d3cd9cc7-001d-4f8a-828d-18a3aafcb93b | ca6f4904-f6ca-403e-b55d-f0b0ae13dbfe |             1 | reading_risk_pipeline | 2026-05-18 23:15:48.175533+00 | 2026-05-18 23:15:49.161391+00 |       1 | âˆ…       | âˆ…
 cdf65319-1f4e-47ba-8049-bdf3d851e612 | c8364810-d17f-4572-950b-9f2e8e793612 |             1 | reading_risk_pipeline | 2026-05-18 23:15:47.177613+00 | 2026-05-18 23:15:48.161+00    |       1 | âˆ…       | âˆ…
 524c99c8-e568-4bd5-b2b4-f476e1b70396 | 7b600f31-318c-4dad-8802-1981f52509e2 |             1 | reading_risk_pipeline | 2026-05-18 23:15:46.177336+00 | 2026-05-18 23:15:47.162252+00 |       1 | âˆ…       | âˆ…
 b7919984-281d-408f-b334-6fcfe3a07c21 | 19462830-3328-47c4-b716-472957b905d8 |             1 | reading_risk_pipeline | 2026-05-18 23:15:45.179887+00 | 2026-05-18 23:15:46.161261+00 |       1 | âˆ…       | âˆ…
 a446c14b-dc2e-4fa6-a19c-2a3e8bd9246e | 2008b49b-beea-448f-a4b3-a5db4afe96a8 |             1 | reading_risk_pipeline | 2026-05-18 23:15:44.177692+00 | 2026-05-18 23:15:45.165655+00 |       1 | âˆ…       | âˆ…
 51800d26-cbb9-4374-a6ab-d652ce2c1b80 | 6a7016dd-0b4d-4610-abb8-273e32328fce |             1 | reading_risk_pipeline | 2026-05-18 23:15:43.176846+00 | 2026-05-18 23:15:44.162029+00 |       1 | âˆ…       | âˆ…
 5ee19ead-c253-41db-bd0a-a20a124ac7b6 | 2b976102-6805-4fc4-bcf9-c34dce9b4a82 |             1 | reading_risk_pipeline | 2026-05-18 23:15:42.17534+00  | 2026-05-18 23:15:43.162522+00 |       1 | âˆ…       | âˆ…
 82119ed1-9dfa-402e-8a88-d0ae3329c64a | 767a0ee8-154f-4070-8f64-4885e4bf3966 |             1 | reading_risk_pipeline | 2026-05-18 23:15:41.178012+00 | 2026-05-18 23:15:42.161102+00 |       1 | âˆ…       | âˆ…
 cf828257-3eb4-4905-b239-b51938fe15b9 | 87ef8698-3903-4127-8404-ac21f1e52450 |             1 | reading_risk_pipeline | 2026-05-18 23:15:40.178223+00 | 2026-05-18 23:15:41.163093+00 |       1 | âˆ…       | âˆ…
 9441c522-dea9-4c1a-88ae-12881e45b9e9 | 5c6fd9bf-2a6e-4c76-a3a9-b4b078b8f48f |             1 | reading_risk_pipeline | 2026-05-18 23:15:39.175821+00 | 2026-05-18 23:15:40.163875+00 |       1 | âˆ…       | âˆ…
 c4ecf77b-a1fb-49e1-a5a7-904ed5256080 | 86a1783a-e277-4a0b-b15e-604a1a668b4d |             1 | reading_risk_pipeline | 2026-05-18 23:15:38.174966+00 | 2026-05-18 23:15:39.161655+00 |       1 | âˆ…       | âˆ…
 83626741-2afc-4de8-bbd3-840951b937bd | bc79f266-4b01-44f5-81f9-428aa4a9c078 |             1 | reading_risk_pipeline | 2026-05-18 23:15:37.177657+00 | 2026-05-18 23:15:38.161113+00 |       1 | âˆ…       | âˆ…
 23b0f56b-04d6-4445-b897-a290b68f38c0 | e2368f8c-c01b-442d-9adc-2af3432b1489 |             1 | reading_risk_pipeline | 2026-05-18 23:15:36.175313+00 | 2026-05-18 23:15:37.162314+00 |       1 | âˆ…       | âˆ…
 6ac5bff3-786d-4a2a-b60a-ad52e6bebb2a | 3c44ad72-36d7-4ead-b385-f605632acf99 |             1 | reading_risk_pipeline | 2026-05-18 23:15:35.180151+00 | 2026-05-18 23:15:36.161292+00 |       1 | âˆ…       | âˆ…
 5468b5c6-7977-4b33-9b15-ff62f48629fa | e366e9ba-c330-4a0e-aa82-1f9ceb3d2119 |             1 | reading_risk_pipeline | 2026-05-18 23:15:34.176191+00 | 2026-05-18 23:15:35.163846+00 |       1 | âˆ…       | âˆ…
 99019a3c-7cc8-40d8-8b8c-220e0aa9df6f | 77dae637-c13c-4e36-899a-98d4e39b5dec |             1 | reading_risk_pipeline | 2026-05-18 23:15:33.175951+00 | 2026-05-18 23:15:34.162403+00 |       1 | âˆ…       | âˆ…
 725b4c44-787e-4303-8c8a-17300e8923ce | b6b9c327-f613-48f7-8342-89fe470f14a1 |             1 | reading_risk_pipeline | 2026-05-18 23:15:32.177535+00 | 2026-05-18 23:15:33.162029+00 |       1 | âˆ…       | âˆ…
 20aaf98a-3373-4849-80ca-2242eaa7a9f4 | efec6ecc-3960-4ffd-966a-504ea99342a6 |             1 | reading_risk_pipeline | 2026-05-18 23:15:31.180874+00 | 2026-05-18 23:15:32.163495+00 |       1 | âˆ…       | âˆ…
 8edf289e-727c-4a56-a3b9-1bd0490f05ff | 508a9db6-8516-4975-abd5-d3203c2fcefb |             1 | reading_risk_pipeline | 2026-05-18 23:15:30.182635+00 | 2026-05-18 23:15:31.161752+00 |       1 | âˆ…       | âˆ…
 c2747eec-42f2-4b00-ad92-144b1aa2189f | c034f1e0-ceef-4556-bd1f-c5eeedd8c1a6 |             1 | reading_risk_pipeline | 2026-05-18 23:15:29.181593+00 | 2026-05-18 23:15:30.164607+00 |       1 | âˆ…       | âˆ…
 8c717c39-e92e-4c3f-824c-64bf0f25d91d | d48e00e6-a4aa-4e5d-a0f7-603694fa3801 |             1 | reading_risk_pipeline | 2026-05-18 23:15:28.177272+00 | 2026-05-18 23:15:29.165419+00 |       1 | âˆ…       | âˆ…
 c73b1d28-9db6-42b3-a1e9-807140263fbb | 43a9c3de-6ffe-420d-a35d-c1dd731c0542 |             1 | reading_risk_pipeline | 2026-05-18 23:15:27.175761+00 | 2026-05-18 23:15:28.16302+00  |       1 | âˆ…       | âˆ…
 21b1b53b-f37b-467b-8dae-550e7f544e61 | 0ba51e2e-51a8-492d-8259-12176c49fefd |             1 | reading_risk_pipeline | 2026-05-18 23:15:26.175466+00 | 2026-05-18 23:15:27.161882+00 |       1 | âˆ…       | âˆ…
 210468cf-7f99-4cd1-b74c-0e89c74e6779 | c7674273-013a-4c04-8985-1b1207bc6de4 |             1 | reading_risk_pipeline | 2026-05-18 23:15:25.18281+00  | 2026-05-18 23:15:26.162323+00 |       1 | âˆ…       | âˆ…
 9e71f17e-44de-47c1-b27a-61a63642553d | 766a7e42-4709-4904-b873-0ecdac795c70 |             1 | reading_risk_pipeline | 2026-05-18 23:15:24.181212+00 | 2026-05-18 23:15:25.163056+00 |       1 | âˆ…       | âˆ…
 c9ba9ed9-367c-47cf-bd05-5308e9c70e59 | 411224f6-bb3a-4a04-8a47-b790aecb0c17 |             1 | reading_risk_pipeline | 2026-05-18 23:15:23.177388+00 | 2026-05-18 23:15:24.16482+00  |       1 | âˆ…       | âˆ…
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
 Stage | Outcome | ErrorCode | ErrorMessage | count 
-------+---------+-----------+--------------+-------
(0 rows)


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
       RejectionCode       |                              RejectionReason                              | count |       first_rejected_at       |       last_rejected_at        
---------------------------+---------------------------------------------------------------------------+-------+-------------------------------+-------------------------------
 invalid_operational_state | OperationalState 'Invalid' is rejected before the accepted-risk pipeline. |     6 | 2026-05-18 17:55:13.677056+00 | 2026-05-18 22:27:14.294591+00
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
                  Id                  | InboxEventId | EventId |       RejectionCode       |                              RejectionReason                              |          RejectedAt           |                                                                                                                                                                                                                                                   raw_body_sample                                                                                                                                                                                                                                                    |                                                                                                                                                                                                                                        MetadataJson                                                                                                                                                                                                                                         
--------------------------------------+--------------+---------+---------------------------+---------------------------------------------------------------------------+-------------------------------+----------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------+---------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------
 b55afa8e-ccc8-496e-9a90-97c2e9b40d9a | âˆ…          | âˆ…     | invalid_operational_state | OperationalState 'Invalid' is rejected before the accepted-risk pipeline. | 2026-05-18 22:27:14.294591+00 | {"schemaVersion":"1.0","eventId":"0b32e21b-7be3-47b2-b624-b4fa76f1cec9","correlationId":"44327172046b4320b2b01205b4f7827e-0004-17badc17792017acc213db1c12bedfb2","producer":"NatureProtector.Simulator.Host","eventType":"SensorReadingProduced","areaId":"b3f4fb84-bf17-5522-a5f3-70fd1212f381","eventTime":"2020-09-13T12:00:20+02:00","payload":{"simulationRunId":"44327172-046b-4320-b2b0-1205b4f7827e","sensorId":"17badc17-7920-17ac-c213-db1c12bedfb2","sensorName":"pilot-wind-0230","metricType":"WindSpee | {"EventId":"0b32e21b-7be3-47b2-b624-b4fa76f1cec9","CorrelationId":"44327172046b4320b2b01205b4f7827e-0004-17badc17792017acc213db1c12bedfb2","Producer":"NatureProtector.Simulator.Host","EventType":"SensorReadingProduced","AreaId":"b3f4fb84-bf17-5522-a5f3-70fd1212f381","SchemaVersion":"1.0","SensorId":"17badc17-7920-17ac-c213-db1c12bedfb2","SensorName":"pilot-wind-0230","MetricType":"WindSpeed","OperationalState":"Invalid","Stage":"pre_inbox_validation","DeliveryTag":24}
 dcfc91fa-973f-4b53-a69b-de39e745d334 | âˆ…          | âˆ…     | invalid_operational_state | OperationalState 'Invalid' is rejected before the accepted-risk pipeline. | 2026-05-18 22:26:51.300423+00 | {"schemaVersion":"1.0","eventId":"c47ff323-ba8a-49a9-afa2-753751155722","correlationId":"44327172046b4320b2b01205b4f7827e-0000-17badc17792017acc213db1c12bedfb2","producer":"NatureProtector.Simulator.Host","eventType":"SensorReadingProduced","areaId":"b3f4fb84-bf17-5522-a5f3-70fd1212f381","eventTime":"2020-09-13T12:00:00+02:00","payload":{"simulationRunId":"44327172-046b-4320-b2b0-1205b4f7827e","sensorId":"17badc17-7920-17ac-c213-db1c12bedfb2","sensorName":"pilot-wind-0230","metricType":"WindSpee | {"EventId":"c47ff323-ba8a-49a9-afa2-753751155722","CorrelationId":"44327172046b4320b2b01205b4f7827e-0000-17badc17792017acc213db1c12bedfb2","Producer":"NatureProtector.Simulator.Host","EventType":"SensorReadingProduced","AreaId":"b3f4fb84-bf17-5522-a5f3-70fd1212f381","SchemaVersion":"1.0","SensorId":"17badc17-7920-17ac-c213-db1c12bedfb2","SensorName":"pilot-wind-0230","MetricType":"WindSpeed","OperationalState":"Invalid","Stage":"pre_inbox_validation","DeliveryTag":4}
 e4339ac0-bd6b-4d87-b8d3-3b1bf63d226a | âˆ…          | âˆ…     | invalid_operational_state | OperationalState 'Invalid' is rejected before the accepted-risk pipeline. | 2026-05-18 22:26:49.280521+00 | {"schemaVersion":"1.0","eventId":"c317b44e-f4a1-4e49-b235-ef9e2ef1ee7b","correlationId":"44327172046b4320b2b01205b4f7827e-0000-22702eee75aa3cf431abb689bfb9a8bc","producer":"NatureProtector.Simulator.Host","eventType":"SensorReadingProduced","areaId":"b3f4fb84-bf17-5522-a5f3-70fd1212f381","eventTime":"2020-09-13T12:00:00+02:00","payload":{"simulationRunId":"44327172-046b-4320-b2b0-1205b4f7827e","sensorId":"22702eee-75aa-3cf4-31ab-b689bfb9a8bc","sensorName":"pilot-humidity-0001","metricType":"Humi | {"EventId":"c317b44e-f4a1-4e49-b235-ef9e2ef1ee7b","CorrelationId":"44327172046b4320b2b01205b4f7827e-0000-22702eee75aa3cf431abb689bfb9a8bc","Producer":"NatureProtector.Simulator.Host","EventType":"SensorReadingProduced","AreaId":"b3f4fb84-bf17-5522-a5f3-70fd1212f381","SchemaVersion":"1.0","SensorId":"22702eee-75aa-3cf4-31ab-b689bfb9a8bc","SensorName":"pilot-humidity-0001","MetricType":"Humidity","OperationalState":"Invalid","Stage":"pre_inbox_validation","DeliveryTag":1}
 3b0b13f5-d678-45b7-a845-dcadf8a45273 | âˆ…          | âˆ…     | invalid_operational_state | OperationalState 'Invalid' is rejected before the accepted-risk pipeline. | 2026-05-18 17:55:29.673051+00 | {"schemaVersion":"1.0","eventId":"69962c74-bea7-412c-99f5-559c7cf01629","correlationId":"33426c68be7846ae8026a03935b7daad-0003-e7660606cadd4212e67b574c09ef789a","producer":"NatureProtector.Simulator.Host","eventType":"SensorReadingProduced","areaId":"b3f4fb84-bf17-5522-a5f3-70fd1212f381","eventTime":"2020-09-13T12:00:15+02:00","payload":{"simulationRunId":"33426c68-be78-46ae-8026-a03935b7daad","sensorId":"e7660606-cadd-4212-e67b-574c09ef789a","sensorName":"pilot-wind-0001","metricType":"WindSpee | {"EventId":"69962c74-bea7-412c-99f5-559c7cf01629","CorrelationId":"33426c68be7846ae8026a03935b7daad-0003-e7660606cadd4212e67b574c09ef789a","Producer":"NatureProtector.Simulator.Host","EventType":"SensorReadingProduced","AreaId":"b3f4fb84-bf17-5522-a5f3-70fd1212f381","SchemaVersion":"1.0","SensorId":"e7660606-cadd-4212-e67b-574c09ef789a","SensorName":"pilot-wind-0001","MetricType":"WindSpeed","OperationalState":"Invalid","Stage":"pre_inbox_validation","DeliveryTag":23}
 0b8bb3ca-162c-42bc-a18e-a1b5024764c6 | âˆ…          | âˆ…     | invalid_operational_state | OperationalState 'Invalid' is rejected before the accepted-risk pipeline. | 2026-05-18 17:55:20.674199+00 | {"schemaVersion":"1.0","eventId":"fa06d877-0e14-4859-865b-e8125256e53a","correlationId":"33426c68be7846ae8026a03935b7daad-0002-22702eee75aa3cf431abb689bfb9a8bc","producer":"NatureProtector.Simulator.Host","eventType":"SensorReadingProduced","areaId":"b3f4fb84-bf17-5522-a5f3-70fd1212f381","eventTime":"2020-09-13T12:00:10+02:00","payload":{"simulationRunId":"33426c68-be78-46ae-8026-a03935b7daad","sensorId":"22702eee-75aa-3cf4-31ab-b689bfb9a8bc","sensorName":"pilot-humidity-0001","metricType":"Humi | {"EventId":"fa06d877-0e14-4859-865b-e8125256e53a","CorrelationId":"33426c68be7846ae8026a03935b7daad-0002-22702eee75aa3cf431abb689bfb9a8bc","Producer":"NatureProtector.Simulator.Host","EventType":"SensorReadingProduced","AreaId":"b3f4fb84-bf17-5522-a5f3-70fd1212f381","SchemaVersion":"1.0","SensorId":"22702eee-75aa-3cf4-31ab-b689bfb9a8bc","SensorName":"pilot-humidity-0001","MetricType":"Humidity","OperationalState":"Invalid","Stage":"pre_inbox_validation","DeliveryTag":13}
 4a287a55-02ec-441c-930e-695091060d9a | âˆ…          | âˆ…     | invalid_operational_state | OperationalState 'Invalid' is rejected before the accepted-risk pipeline. | 2026-05-18 17:55:13.677056+00 | {"schemaVersion":"1.0","eventId":"8e0385b8-9b7d-485e-b2aa-a2f6cf622ab3","correlationId":"33426c68be7846ae8026a03935b7daad-0000-e7660606cadd4212e67b574c09ef789a","producer":"NatureProtector.Simulator.Host","eventType":"SensorReadingProduced","areaId":"b3f4fb84-bf17-5522-a5f3-70fd1212f381","eventTime":"2020-09-13T12:00:00+02:00","payload":{"simulationRunId":"33426c68-be78-46ae-8026-a03935b7daad","sensorId":"e7660606-cadd-4212-e67b-574c09ef789a","sensorName":"pilot-wind-0001","metricType":"WindSpee | {"EventId":"8e0385b8-9b7d-485e-b2aa-a2f6cf622ab3","CorrelationId":"33426c68be7846ae8026a03935b7daad-0000-e7660606cadd4212e67b574c09ef789a","Producer":"NatureProtector.Simulator.Host","EventType":"SensorReadingProduced","AreaId":"b3f4fb84-bf17-5522-a5f3-70fd1212f381","SchemaVersion":"1.0","SensorId":"e7660606-cadd-4212-e67b-574c09ef789a","SensorName":"pilot-wind-0001","MetricType":"WindSpeed","OperationalState":"Invalid","Stage":"pre_inbox_validation","DeliveryTag":5}
(6 rows)


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
 QuarantineCode | QuarantineReason | count | first_quarantined_at | last_quarantined_at 
----------------+------------------+-------+----------------------+---------------------
(0 rows)


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
 Id | InboxEventId | EventId | FinalAttemptNumber | QuarantineCode | QuarantineReason | QuarantinedAt | MetadataJson 
----+--------------+---------+--------------------+----------------+------------------+---------------+--------------
(0 rows)


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
               102 |              102 |                 102 |                       2 |                       1 |            1
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
     first_risk_created_at     |     last_risk_created_at      | first_area_snapshot_timestamp | last_area_snapshot_timestamp |  last_area_state_updated_at   |     last_alert_updated_at     
-------------------------------+-------------------------------+-------------------------------+------------------------------+-------------------------------+-------------------------------
 2026-05-18 17:55:09.476337+00 | 2026-05-18 23:17:42.201036+00 | 2020-09-13 10:00:00+00        | 2020-09-13 10:00:20+00       | 2026-05-18 23:17:42.227373+00 | 2026-05-18 23:17:42.227373+00
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
 SimulationRunId    | uuid
(11 rows)


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
              102 |          0.515 |            0.6 | 0.5384967320261438
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
 High      |   102 |     0.515 |       0.6
(1 row)


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
                  Id                  |                AreaId                |               SensorId               |              GridCellId              |            SourceEventId             |       Timestamp        |     RiskScore      | RiskLevel |                                                                                                                                                                                       ExplanationSummary                                                                                                                                                                                       |           CreatedAt           
--------------------------------------+--------------------------------------+--------------------------------------+--------------------------------------+--------------------------------------+------------------------+--------------------+-----------+------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------+-------------------------------
 04ab80e7-40ae-46f1-bf14-3f418d2ac0bb | b3f4fb84-bf17-5522-a5f3-70fd1212f381 | 17badc17-7920-17ac-c213-db1c12bedfb2 | 8a65d4e7-92f5-1b6f-fdb0-d60ec5707d72 | 7fc56741-210b-4087-b3a8-104c5b024ec5 | 2020-09-13 10:00:20+00 |               0.54 | High      | Area=b3f4fb84-bf17-5522-a5f3-70fd1212f381; Sensor=17badc17-7920-17ac-c213-db1c12bedfb2; Event=7fc56741-210b-4087-b3a8-104c5b024ec5; Metric=WindSpeed; Value=5,36; InputStatus=CompleteEligible; M=0,58; D=0,50; T=0,50; BaseRisk=0,54; AdjustedScore=0,54; C=1,00; I=1,00; FWI=absent; KBDI=absent; FireIndexProvenance=absent; ParameterSet=Candidate Parameter Set V1.0 (non-calibrated).    | 2026-05-18 23:17:42.201036+00
 f28b08cc-0474-4820-b360-3268fd9de962 | b3f4fb84-bf17-5522-a5f3-70fd1212f381 | e7660606-cadd-4212-e67b-574c09ef789a | 33d52e78-7801-cf3b-bc8b-c798db0d4068 | f35ef8b9-2ea4-4455-aad3-9b88797cffb7 | 2020-09-13 10:00:20+00 |               0.54 | High      | Area=b3f4fb84-bf17-5522-a5f3-70fd1212f381; Sensor=e7660606-cadd-4212-e67b-574c09ef789a; Event=f35ef8b9-2ea4-4455-aad3-9b88797cffb7; Metric=WindSpeed; Value=5,66; InputStatus=CompleteEligible; M=0,58; D=0,50; T=0,50; BaseRisk=0,54; AdjustedScore=0,54; C=1,00; I=1,00; FWI=absent; KBDI=absent; FireIndexProvenance=absent; ParameterSet=Candidate Parameter Set V1.0 (non-calibrated).    | 2026-05-18 23:17:41.196731+00
 76404df5-3d93-48b0-86f8-c7b94d1c7a92 | b3f4fb84-bf17-5522-a5f3-70fd1212f381 | 8e6a3d8a-7e65-4fa5-8c51-fa4597b203b8 | 8a65d4e7-92f5-1b6f-fdb0-d60ec5707d72 | 7a9887f3-0931-4e72-b42c-0477c1467f59 | 2020-09-13 10:00:20+00 |               0.54 | High      | Area=b3f4fb84-bf17-5522-a5f3-70fd1212f381; Sensor=8e6a3d8a-7e65-4fa5-8c51-fa4597b203b8; Event=7a9887f3-0931-4e72-b42c-0477c1467f59; Metric=Temperature; Value=32,72; InputStatus=CompleteEligible; M=0,58; D=0,50; T=0,50; BaseRisk=0,54; AdjustedScore=0,54; C=1,00; I=1,00; FWI=absent; KBDI=absent; FireIndexProvenance=absent; ParameterSet=Candidate Parameter Set V1.0 (non-calibrated). | 2026-05-18 23:17:40.199794+00
 4573e962-dd42-4131-91f7-2dc54f00fa4e | b3f4fb84-bf17-5522-a5f3-70fd1212f381 | 27bf9aad-094f-7dc6-45b2-129a8cc0522e | 33d52e78-7801-cf3b-bc8b-c798db0d4068 | 6c56cfc8-ee22-4b53-94fa-da4a43c2167f | 2020-09-13 10:00:20+00 |               0.54 | High      | Area=b3f4fb84-bf17-5522-a5f3-70fd1212f381; Sensor=27bf9aad-094f-7dc6-45b2-129a8cc0522e; Event=6c56cfc8-ee22-4b53-94fa-da4a43c2167f; Metric=Temperature; Value=32,10; InputStatus=CompleteEligible; M=0,58; D=0,50; T=0,50; BaseRisk=0,54; AdjustedScore=0,54; C=1,00; I=1,00; FWI=absent; KBDI=absent; FireIndexProvenance=absent; ParameterSet=Candidate Parameter Set V1.0 (non-calibrated). | 2026-05-18 23:17:39.196054+00
 a1ab9c02-3c23-4ca8-9a92-03fad0d9b3f6 | b3f4fb84-bf17-5522-a5f3-70fd1212f381 | 2f494582-4077-86f2-9707-780861949155 | 8a65d4e7-92f5-1b6f-fdb0-d60ec5707d72 | be6f0960-04c5-402c-926e-931d1d4cb4af | 2020-09-13 10:00:20+00 |               0.54 | High      | Area=b3f4fb84-bf17-5522-a5f3-70fd1212f381; Sensor=2f494582-4077-86f2-9707-780861949155; Event=be6f0960-04c5-402c-926e-931d1d4cb4af; Metric=Humidity; Value=20,97; InputStatus=CompleteEligible; M=0,58; D=0,50; T=0,50; BaseRisk=0,54; AdjustedScore=0,54; C=1,00; I=1,00; FWI=absent; KBDI=absent; FireIndexProvenance=absent; ParameterSet=Candidate Parameter Set V1.0 (non-calibrated).    | 2026-05-18 23:17:38.191312+00
 719d84c7-1266-48f0-91cb-b219bb5a27ad | b3f4fb84-bf17-5522-a5f3-70fd1212f381 | 22702eee-75aa-3cf4-31ab-b689bfb9a8bc | 33d52e78-7801-cf3b-bc8b-c798db0d4068 | 72cf15a1-18af-41af-8fda-1566b41bc119 | 2020-09-13 10:00:20+00 |               0.54 | High      | Area=b3f4fb84-bf17-5522-a5f3-70fd1212f381; Sensor=22702eee-75aa-3cf4-31ab-b689bfb9a8bc; Event=72cf15a1-18af-41af-8fda-1566b41bc119; Metric=Humidity; Value=21,15; InputStatus=CompleteEligible; M=0,58; D=0,50; T=0,50; BaseRisk=0,54; AdjustedScore=0,54; C=1,00; I=1,00; FWI=absent; KBDI=absent; FireIndexProvenance=absent; ParameterSet=Candidate Parameter Set V1.0 (non-calibrated).    | 2026-05-18 23:17:37.684421+00
 0319674e-7f2b-468b-9cd8-8410e98d3c11 | b3f4fb84-bf17-5522-a5f3-70fd1212f381 | 17badc17-7920-17ac-c213-db1c12bedfb2 | 8a65d4e7-92f5-1b6f-fdb0-d60ec5707d72 | d883efbf-a051-41b7-b45d-b89554d4a3ee | 2020-09-13 10:00:15+00 |               0.54 | High      | Area=b3f4fb84-bf17-5522-a5f3-70fd1212f381; Sensor=17badc17-7920-17ac-c213-db1c12bedfb2; Event=d883efbf-a051-41b7-b45d-b89554d4a3ee; Metric=WindSpeed; Value=5,29; InputStatus=CompleteEligible; M=0,58; D=0,50; T=0,50; BaseRisk=0,54; AdjustedScore=0,54; C=1,00; I=1,00; FWI=absent; KBDI=absent; FireIndexProvenance=absent; ParameterSet=Candidate Parameter Set V1.0 (non-calibrated).    | 2026-05-18 23:17:35.191634+00
 d355a3d7-96c7-405a-a4a0-7cbf71cd8bf4 | b3f4fb84-bf17-5522-a5f3-70fd1212f381 | e7660606-cadd-4212-e67b-574c09ef789a | 33d52e78-7801-cf3b-bc8b-c798db0d4068 | 6a584250-bcf8-4e07-91b6-41023097adee | 2020-09-13 10:00:15+00 |               0.54 | High      | Area=b3f4fb84-bf17-5522-a5f3-70fd1212f381; Sensor=e7660606-cadd-4212-e67b-574c09ef789a; Event=6a584250-bcf8-4e07-91b6-41023097adee; Metric=WindSpeed; Value=5,55; InputStatus=CompleteEligible; M=0,58; D=0,50; T=0,50; BaseRisk=0,54; AdjustedScore=0,54; C=1,00; I=1,00; FWI=absent; KBDI=absent; FireIndexProvenance=absent; ParameterSet=Candidate Parameter Set V1.0 (non-calibrated).    | 2026-05-18 23:17:34.190572+00
 7d1dd9ae-7b0a-451c-939e-ee586d95fd4c | b3f4fb84-bf17-5522-a5f3-70fd1212f381 | 27bf9aad-094f-7dc6-45b2-129a8cc0522e | 33d52e78-7801-cf3b-bc8b-c798db0d4068 | 5b5b3055-4545-4962-b650-80488e8d4488 | 2020-09-13 10:00:15+00 |              0.515 | High      | Area=b3f4fb84-bf17-5522-a5f3-70fd1212f381; Sensor=27bf9aad-094f-7dc6-45b2-129a8cc0522e; Event=5b5b3055-4545-4962-b650-80488e8d4488; Metric=Temperature; Value=31,95; InputStatus=CompleteEligible; M=0,53; D=0,50; T=0,50; BaseRisk=0,52; AdjustedScore=0,52; C=1,00; I=1,00; FWI=absent; KBDI=absent; FireIndexProvenance=absent; ParameterSet=Candidate Parameter Set V1.0 (non-calibrated). | 2026-05-18 23:17:33.190711+00
 651bc24a-1585-4f8b-8c72-d0647fb13f75 | b3f4fb84-bf17-5522-a5f3-70fd1212f381 | 2f494582-4077-86f2-9707-780861949155 | 8a65d4e7-92f5-1b6f-fdb0-d60ec5707d72 | 89972a4e-ff2e-4bd4-b2ab-0fbaccd6b55f | 2020-09-13 10:00:15+00 |              0.515 | High      | Area=b3f4fb84-bf17-5522-a5f3-70fd1212f381; Sensor=2f494582-4077-86f2-9707-780861949155; Event=89972a4e-ff2e-4bd4-b2ab-0fbaccd6b55f; Metric=Humidity; Value=20,65; InputStatus=CompleteEligible; M=0,53; D=0,50; T=0,50; BaseRisk=0,52; AdjustedScore=0,52; C=1,00; I=1,00; FWI=absent; KBDI=absent; FireIndexProvenance=absent; ParameterSet=Candidate Parameter Set V1.0 (non-calibrated).    | 2026-05-18 23:17:32.655599+00
 cf2b5f81-43c3-4f14-9790-7028f0c96f08 | b3f4fb84-bf17-5522-a5f3-70fd1212f381 | 17badc17-7920-17ac-c213-db1c12bedfb2 | 8a65d4e7-92f5-1b6f-fdb0-d60ec5707d72 | 4c640d15-14a4-458a-a685-bf2321a43199 | 2020-09-13 10:00:10+00 |              0.515 | High      | Area=b3f4fb84-bf17-5522-a5f3-70fd1212f381; Sensor=17badc17-7920-17ac-c213-db1c12bedfb2; Event=4c640d15-14a4-458a-a685-bf2321a43199; Metric=WindSpeed; Value=4,95; InputStatus=CompleteEligible; M=0,53; D=0,50; T=0,50; BaseRisk=0,52; AdjustedScore=0,52; C=1,00; I=1,00; FWI=absent; KBDI=absent; FireIndexProvenance=absent; ParameterSet=Candidate Parameter Set V1.0 (non-calibrated).    | 2026-05-18 23:17:31.191369+00
 20d30567-f68c-4da9-8360-216ae11525c5 | b3f4fb84-bf17-5522-a5f3-70fd1212f381 | e7660606-cadd-4212-e67b-574c09ef789a | 33d52e78-7801-cf3b-bc8b-c798db0d4068 | 1b7d59c1-8883-4f34-83bf-66605a8ef080 | 2020-09-13 10:00:10+00 |              0.515 | High      | Area=b3f4fb84-bf17-5522-a5f3-70fd1212f381; Sensor=e7660606-cadd-4212-e67b-574c09ef789a; Event=1b7d59c1-8883-4f34-83bf-66605a8ef080; Metric=WindSpeed; Value=4,78; InputStatus=CompleteEligible; M=0,53; D=0,50; T=0,50; BaseRisk=0,52; AdjustedScore=0,52; C=1,00; I=1,00; FWI=absent; KBDI=absent; FireIndexProvenance=absent; ParameterSet=Candidate Parameter Set V1.0 (non-calibrated).    | 2026-05-18 23:17:30.192856+00
 2a9f6967-d5e4-4c27-85dc-6de9d7cf3570 | b3f4fb84-bf17-5522-a5f3-70fd1212f381 | 8e6a3d8a-7e65-4fa5-8c51-fa4597b203b8 | 8a65d4e7-92f5-1b6f-fdb0-d60ec5707d72 | 1dbec683-04d3-4756-ae2a-bf05c95554e7 | 2020-09-13 10:00:10+00 |              0.515 | High      | Area=b3f4fb84-bf17-5522-a5f3-70fd1212f381; Sensor=8e6a3d8a-7e65-4fa5-8c51-fa4597b203b8; Event=1dbec683-04d3-4756-ae2a-bf05c95554e7; Metric=Temperature; Value=31,91; InputStatus=CompleteEligible; M=0,53; D=0,50; T=0,50; BaseRisk=0,52; AdjustedScore=0,52; C=1,00; I=1,00; FWI=absent; KBDI=absent; FireIndexProvenance=absent; ParameterSet=Candidate Parameter Set V1.0 (non-calibrated). | 2026-05-18 23:17:29.195392+00
 b15e2752-8fa3-48f1-8a1e-1ed9bebbddd4 | b3f4fb84-bf17-5522-a5f3-70fd1212f381 | 27bf9aad-094f-7dc6-45b2-129a8cc0522e | 33d52e78-7801-cf3b-bc8b-c798db0d4068 | 6622f1ef-eecc-4a09-b6d5-fc017408e73e | 2020-09-13 10:00:10+00 |              0.515 | High      | Area=b3f4fb84-bf17-5522-a5f3-70fd1212f381; Sensor=27bf9aad-094f-7dc6-45b2-129a8cc0522e; Event=6622f1ef-eecc-4a09-b6d5-fc017408e73e; Metric=Temperature; Value=31,82; InputStatus=CompleteEligible; M=0,53; D=0,50; T=0,50; BaseRisk=0,52; AdjustedScore=0,52; C=1,00; I=1,00; FWI=absent; KBDI=absent; FireIndexProvenance=absent; ParameterSet=Candidate Parameter Set V1.0 (non-calibrated). | 2026-05-18 23:17:28.192252+00
 941b43b0-7854-4dba-bf5d-3d405a0763a3 | b3f4fb84-bf17-5522-a5f3-70fd1212f381 | 22702eee-75aa-3cf4-31ab-b689bfb9a8bc | 33d52e78-7801-cf3b-bc8b-c798db0d4068 | 761ca7f4-60ea-4d5c-817a-9acd0f040b10 | 2020-09-13 10:00:10+00 |              0.515 | High      | Area=b3f4fb84-bf17-5522-a5f3-70fd1212f381; Sensor=22702eee-75aa-3cf4-31ab-b689bfb9a8bc; Event=761ca7f4-60ea-4d5c-817a-9acd0f040b10; Metric=Humidity; Value=21,68; InputStatus=CompleteEligible; M=0,53; D=0,50; T=0,50; BaseRisk=0,52; AdjustedScore=0,52; C=1,00; I=1,00; FWI=absent; KBDI=absent; FireIndexProvenance=absent; ParameterSet=Candidate Parameter Set V1.0 (non-calibrated).    | 2026-05-18 23:17:27.624085+00
 06932f7b-9378-426b-b718-e96ecb0c6309 | b3f4fb84-bf17-5522-a5f3-70fd1212f381 | e7660606-cadd-4212-e67b-574c09ef789a | 33d52e78-7801-cf3b-bc8b-c798db0d4068 | c6687802-7679-41d0-ae8e-f7d28ebfcb0f | 2020-09-13 10:00:05+00 |              0.515 | High      | Area=b3f4fb84-bf17-5522-a5f3-70fd1212f381; Sensor=e7660606-cadd-4212-e67b-574c09ef789a; Event=c6687802-7679-41d0-ae8e-f7d28ebfcb0f; Metric=WindSpeed; Value=4,46; InputStatus=CompleteEligible; M=0,53; D=0,50; T=0,50; BaseRisk=0,52; AdjustedScore=0,52; C=1,00; I=1,00; FWI=absent; KBDI=absent; FireIndexProvenance=absent; ParameterSet=Candidate Parameter Set V1.0 (non-calibrated).    | 2026-05-18 23:17:26.192118+00
 4a3c4c73-23cd-4267-82e3-84ea097d0e10 | b3f4fb84-bf17-5522-a5f3-70fd1212f381 | 8e6a3d8a-7e65-4fa5-8c51-fa4597b203b8 | 8a65d4e7-92f5-1b6f-fdb0-d60ec5707d72 | 05843117-15c4-440e-87d5-639b5f27b606 | 2020-09-13 10:00:05+00 |              0.515 | High      | Area=b3f4fb84-bf17-5522-a5f3-70fd1212f381; Sensor=8e6a3d8a-7e65-4fa5-8c51-fa4597b203b8; Event=05843117-15c4-440e-87d5-639b5f27b606; Metric=Temperature; Value=31,57; InputStatus=CompleteEligible; M=0,53; D=0,50; T=0,50; BaseRisk=0,52; AdjustedScore=0,52; C=1,00; I=1,00; FWI=absent; KBDI=absent; FireIndexProvenance=absent; ParameterSet=Candidate Parameter Set V1.0 (non-calibrated). | 2026-05-18 23:17:25.19327+00
 958f49ff-3c57-4edb-ade8-139927ec2b57 | b3f4fb84-bf17-5522-a5f3-70fd1212f381 | 27bf9aad-094f-7dc6-45b2-129a8cc0522e | 33d52e78-7801-cf3b-bc8b-c798db0d4068 | a57dbd7d-b18f-48e4-910e-2ea9cfe33f8b | 2020-09-13 10:00:05+00 | 0.5866666666666667 | High      | Area=b3f4fb84-bf17-5522-a5f3-70fd1212f381; Sensor=27bf9aad-094f-7dc6-45b2-129a8cc0522e; Event=a57dbd7d-b18f-48e4-910e-2ea9cfe33f8b; Metric=Temperature; Value=31,62; InputStatus=CompleteEligible; M=0,67; D=0,50; T=0,50; BaseRisk=0,59; AdjustedScore=0,59; C=1,00; I=1,00; FWI=absent; KBDI=absent; FireIndexProvenance=absent; ParameterSet=Candidate Parameter Set V1.0 (non-calibrated). | 2026-05-18 23:17:24.193576+00
 e25f4829-f554-42f2-b10c-eff7635ab5b9 | b3f4fb84-bf17-5522-a5f3-70fd1212f381 | 2f494582-4077-86f2-9707-780861949155 | 8a65d4e7-92f5-1b6f-fdb0-d60ec5707d72 | 1f9121b7-0c12-4c08-972e-867ad36878f5 | 2020-09-13 10:00:05+00 |              0.515 | High      | Area=b3f4fb84-bf17-5522-a5f3-70fd1212f381; Sensor=2f494582-4077-86f2-9707-780861949155; Event=1f9121b7-0c12-4c08-972e-867ad36878f5; Metric=Humidity; Value=23,93; InputStatus=CompleteEligible; M=0,53; D=0,50; T=0,50; BaseRisk=0,52; AdjustedScore=0,52; C=1,00; I=1,00; FWI=absent; KBDI=absent; FireIndexProvenance=absent; ParameterSet=Candidate Parameter Set V1.0 (non-calibrated).    | 2026-05-18 23:17:23.191182+00
 afb9a1fa-60ba-45fc-87a0-1ecbe0b6ecdc | b3f4fb84-bf17-5522-a5f3-70fd1212f381 | 22702eee-75aa-3cf4-31ab-b689bfb9a8bc | 33d52e78-7801-cf3b-bc8b-c798db0d4068 | 028114ee-9656-49fc-a584-6732994f43af | 2020-09-13 10:00:05+00 |                0.6 | High      | Area=b3f4fb84-bf17-5522-a5f3-70fd1212f381; Sensor=22702eee-75aa-3cf4-31ab-b689bfb9a8bc; Event=028114ee-9656-49fc-a584-6732994f43af; Metric=Humidity; Value=23,91; InputStatus=CompleteEligible; M=0,70; D=0,50; T=0,50; BaseRisk=0,60; AdjustedScore=0,60; C=1,00; I=1,00; FWI=absent; KBDI=absent; FireIndexProvenance=absent; ParameterSet=Candidate Parameter Set V1.0 (non-calibrated).    | 2026-05-18 23:17:22.592452+00
 3bb881cc-6274-458a-9edc-cc9ea420d5f4 | b3f4fb84-bf17-5522-a5f3-70fd1212f381 | 17badc17-7920-17ac-c213-db1c12bedfb2 | 8a65d4e7-92f5-1b6f-fdb0-d60ec5707d72 | 20c0ca80-4d7a-4293-a0be-7ec0dcc36815 | 2020-09-13 10:00:00+00 |              0.515 | High      | Area=b3f4fb84-bf17-5522-a5f3-70fd1212f381; Sensor=17badc17-7920-17ac-c213-db1c12bedfb2; Event=20c0ca80-4d7a-4293-a0be-7ec0dcc36815; Metric=WindSpeed; Value=4,01; InputStatus=CompleteEligible; M=0,53; D=0,50; T=0,50; BaseRisk=0,52; AdjustedScore=0,52; C=1,00; I=1,00; FWI=absent; KBDI=absent; FireIndexProvenance=absent; ParameterSet=Candidate Parameter Set V1.0 (non-calibrated).    | 2026-05-18 23:17:20.19711+00
 22726b34-9734-4ee8-9bbb-61ebfdce09aa | b3f4fb84-bf17-5522-a5f3-70fd1212f381 | 8e6a3d8a-7e65-4fa5-8c51-fa4597b203b8 | 8a65d4e7-92f5-1b6f-fdb0-d60ec5707d72 | c0266f97-dfa8-4d66-8e06-fe39a8314e3b | 2020-09-13 10:00:00+00 | 0.5866666666666667 | High      | Area=b3f4fb84-bf17-5522-a5f3-70fd1212f381; Sensor=8e6a3d8a-7e65-4fa5-8c51-fa4597b203b8; Event=c0266f97-dfa8-4d66-8e06-fe39a8314e3b; Metric=Temperature; Value=30,97; InputStatus=CompleteEligible; M=0,67; D=0,50; T=0,50; BaseRisk=0,59; AdjustedScore=0,59; C=1,00; I=1,00; FWI=absent; KBDI=absent; FireIndexProvenance=absent; ParameterSet=Candidate Parameter Set V1.0 (non-calibrated). | 2026-05-18 23:17:19.192886+00
 da38122d-82e9-4e72-8dea-6f6aeed9e4cd | b3f4fb84-bf17-5522-a5f3-70fd1212f381 | 2f494582-4077-86f2-9707-780861949155 | 8a65d4e7-92f5-1b6f-fdb0-d60ec5707d72 | 4c846051-0a23-4eed-ac2f-c65c23122f05 | 2020-09-13 10:00:00+00 |                0.6 | High      | Area=b3f4fb84-bf17-5522-a5f3-70fd1212f381; Sensor=2f494582-4077-86f2-9707-780861949155; Event=4c846051-0a23-4eed-ac2f-c65c23122f05; Metric=Humidity; Value=25,31; InputStatus=CompleteEligible; M=0,70; D=0,50; T=0,50; BaseRisk=0,60; AdjustedScore=0,60; C=1,00; I=1,00; FWI=absent; KBDI=absent; FireIndexProvenance=absent; ParameterSet=Candidate Parameter Set V1.0 (non-calibrated).    | 2026-05-18 23:17:18.200079+00
 415b92c0-c8f0-4236-8bdd-b6d9be9b58a4 | b3f4fb84-bf17-5522-a5f3-70fd1212f381 | 22702eee-75aa-3cf4-31ab-b689bfb9a8bc | 33d52e78-7801-cf3b-bc8b-c798db0d4068 | 4bf9ff40-9c55-4813-804f-93a24068530f | 2020-09-13 10:00:00+00 |                0.6 | High      | Area=b3f4fb84-bf17-5522-a5f3-70fd1212f381; Sensor=22702eee-75aa-3cf4-31ab-b689bfb9a8bc; Event=4bf9ff40-9c55-4813-804f-93a24068530f; Metric=Humidity; Value=24,55; InputStatus=CompleteEligible; M=0,70; D=0,50; T=0,50; BaseRisk=0,60; AdjustedScore=0,60; C=1,00; I=1,00; FWI=absent; KBDI=absent; FireIndexProvenance=absent; ParameterSet=Candidate Parameter Set V1.0 (non-calibrated).    | 2026-05-18 23:17:17.562735+00
 ce48af72-1127-400e-83bf-ea70406cba0e | b3f4fb84-bf17-5522-a5f3-70fd1212f381 | 17badc17-7920-17ac-c213-db1c12bedfb2 | 8a65d4e7-92f5-1b6f-fdb0-d60ec5707d72 | 7dc91903-2d96-4b65-b7fa-f5161599e1a1 | 2020-09-13 10:00:20+00 |               0.54 | High      | Area=b3f4fb84-bf17-5522-a5f3-70fd1212f381; Sensor=17badc17-7920-17ac-c213-db1c12bedfb2; Event=7dc91903-2d96-4b65-b7fa-f5161599e1a1; Metric=WindSpeed; Value=5,45; InputStatus=CompleteEligible; M=0,58; D=0,50; T=0,50; BaseRisk=0,54; AdjustedScore=0,54; C=1,00; I=1,00; FWI=absent; KBDI=absent; FireIndexProvenance=absent; ParameterSet=Candidate Parameter Set V1.0 (non-calibrated).    | 2026-05-18 23:15:48.202841+00
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
                  Id                  |                AreaId                |        ConfigurationVersionId        |           SimulationRunId            |   SnapshotTimestamp    | AggregateRiskScore | AggregateRiskLevel | Severity |                                          Summary                                           | AssessmentCount |           UpdatedAt           
--------------------------------------+--------------------------------------+--------------------------------------+--------------------------------------+------------------------+--------------------+--------------------+----------+--------------------------------------------------------------------------------------------+-----------------+-------------------------------
 ec827232-2c6b-474f-bb30-82a5eb564ca3 | b3f4fb84-bf17-5522-a5f3-70fd1212f381 | c03be3d5-1f70-15cb-2fc0-3c86a204a644 | 36caca67-352c-41f1-80e3-8fe951a1582c | 2020-09-13 10:00:20+00 |               0.54 | High               | High     | Aggregated from 6 assessments; 6 at High or above; AreaRisk=0.70*p80(0,54)+0.30*max(0,54). |               6 | 2026-05-18 23:17:42.227373+00
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
                  Id                  |                AreaId                |              GridCellId              |               SensorId               |          LatestAssessmentId          |   SnapshotTimestamp    | RiskScore | RiskLevel | Severity |                                                                                                                                                                                           Summary                                                                                                                                                                                           |           UpdatedAt           
--------------------------------------+--------------------------------------+--------------------------------------+--------------------------------------+--------------------------------------+------------------------+-----------+-----------+----------+---------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------+-------------------------------
 559c1310-0fb5-44b0-ada6-5f1d4f52021f | b3f4fb84-bf17-5522-a5f3-70fd1212f381 | 8a65d4e7-92f5-1b6f-fdb0-d60ec5707d72 | 17badc17-7920-17ac-c213-db1c12bedfb2 | 04ab80e7-40ae-46f1-bf14-3f418d2ac0bb | 2020-09-13 10:00:20+00 |      0.54 | High      | High     | Area=b3f4fb84-bf17-5522-a5f3-70fd1212f381; Sensor=17badc17-7920-17ac-c213-db1c12bedfb2; Event=7fc56741-210b-4087-b3a8-104c5b024ec5; Metric=WindSpeed; Value=5,36; InputStatus=CompleteEligible; M=0,58; D=0,50; T=0,50; BaseRisk=0,54; AdjustedScore=0,54; C=1,00; I=1,00; FWI=absent; KBDI=absent; FireIndexProvenance=absent; ParameterSet=Candidate Parameter Set V1.0 (non-calibrated). | 2026-05-18 23:17:42.208155+00
 54e32b19-78da-4c47-b0f5-a04f40bf3809 | b3f4fb84-bf17-5522-a5f3-70fd1212f381 | 33d52e78-7801-cf3b-bc8b-c798db0d4068 | e7660606-cadd-4212-e67b-574c09ef789a | f28b08cc-0474-4820-b360-3268fd9de962 | 2020-09-13 10:00:20+00 |      0.54 | High      | High     | Area=b3f4fb84-bf17-5522-a5f3-70fd1212f381; Sensor=e7660606-cadd-4212-e67b-574c09ef789a; Event=f35ef8b9-2ea4-4455-aad3-9b88797cffb7; Metric=WindSpeed; Value=5,66; InputStatus=CompleteEligible; M=0,58; D=0,50; T=0,50; BaseRisk=0,54; AdjustedScore=0,54; C=1,00; I=1,00; FWI=absent; KBDI=absent; FireIndexProvenance=absent; ParameterSet=Candidate Parameter Set V1.0 (non-calibrated). | 2026-05-18 23:17:41.207067+00
(2 rows)


```

## Latest area risk snapshots

```sql
select *
from projection.area_risk_snapshot_log
order by "SnapshotTimestamp" desc
limit 25;
```

```text
                  Id                  |                AreaId                |           SimulationRunId            |   SnapshotTimestamp    | AggregateRiskScore | AggregateRiskLevel |                                          Summary                                           | AssessmentCount |           CreatedAt           
--------------------------------------+--------------------------------------+--------------------------------------+------------------------+--------------------+--------------------+--------------------------------------------------------------------------------------------+-----------------+-------------------------------
 7dc91903-2d96-4b65-b7fa-f5161599e1a1 | b3f4fb84-bf17-5522-a5f3-70fd1212f381 | d8203d4b-1839-4908-87ef-05633c1f1ae5 | 2020-09-13 10:00:20+00 |               0.54 | High               | Aggregated from 6 assessments; 6 at High or above; AreaRisk=0.70*p80(0,54)+0.30*max(0,54). |               6 | 2026-05-18 23:15:48.217199+00
 de10567a-a4a0-4f49-aa91-646c91dcbdbe | b3f4fb84-bf17-5522-a5f3-70fd1212f381 | 33426c68-be78-46ae-8026-a03935b7daad | 2020-09-13 10:00:20+00 |               0.54 | High               | Aggregated from 6 assessments; 6 at High or above; AreaRisk=0.70*p80(0,54)+0.30*max(0,54). |               6 | 2026-05-18 17:55:33.713463+00
 d50f8788-b690-46a4-aea3-a652e722b439 | b3f4fb84-bf17-5522-a5f3-70fd1212f381 | d8203d4b-1839-4908-87ef-05633c1f1ae5 | 2020-09-13 10:00:20+00 |               0.54 | High               | Aggregated from 6 assessments; 6 at High or above; AreaRisk=0.70*p80(0,54)+0.30*max(0,54). |               6 | 2026-05-18 23:15:45.220306+00
 bbab86b1-45cd-4411-b82a-33566287e3d1 | b3f4fb84-bf17-5522-a5f3-70fd1212f381 | d8203d4b-1839-4908-87ef-05633c1f1ae5 | 2020-09-13 10:00:20+00 |               0.54 | High               | Aggregated from 6 assessments; 6 at High or above; AreaRisk=0.70*p80(0,54)+0.30*max(0,54). |               6 | 2026-05-18 23:15:47.217084+00
 6c56cfc8-ee22-4b53-94fa-da4a43c2167f | b3f4fb84-bf17-5522-a5f3-70fd1212f381 | 36caca67-352c-41f1-80e3-8fe951a1582c | 2020-09-13 10:00:20+00 |               0.54 | High               | Aggregated from 6 assessments; 6 at High or above; AreaRisk=0.70*p80(0,54)+0.30*max(0,54). |               6 | 2026-05-18 23:17:39.21105+00
 7fc56741-210b-4087-b3a8-104c5b024ec5 | b3f4fb84-bf17-5522-a5f3-70fd1212f381 | 36caca67-352c-41f1-80e3-8fe951a1582c | 2020-09-13 10:00:20+00 |               0.54 | High               | Aggregated from 6 assessments; 6 at High or above; AreaRisk=0.70*p80(0,54)+0.30*max(0,54). |               6 | 2026-05-18 23:17:42.216002+00
 fd0e7c90-5a6b-4592-b97d-ac8b7f48a267 | b3f4fb84-bf17-5522-a5f3-70fd1212f381 | 44327172-046b-4320-b2b0-1205b4f7827e | 2020-09-13 10:00:20+00 |               0.54 | High               | Aggregated from 6 assessments; 6 at High or above; AreaRisk=0.70*p80(0,54)+0.30*max(0,54). |               6 | 2026-05-18 22:27:13.325805+00
 26662362-72fb-48c2-b979-328038304e80 | b3f4fb84-bf17-5522-a5f3-70fd1212f381 | d8203d4b-1839-4908-87ef-05633c1f1ae5 | 2020-09-13 10:00:20+00 |               0.54 | High               | Aggregated from 6 assessments; 6 at High or above; AreaRisk=0.70*p80(0,54)+0.30*max(0,54). |               6 | 2026-05-18 23:15:44.220435+00
 5e03377d-1337-4834-aa78-e045c33e3a6f | b3f4fb84-bf17-5522-a5f3-70fd1212f381 | d8203d4b-1839-4908-87ef-05633c1f1ae5 | 2020-09-13 10:00:20+00 |               0.54 | High               | Aggregated from 6 assessments; 6 at High or above; AreaRisk=0.70*p80(0,54)+0.30*max(0,54). |               6 | 2026-05-18 23:15:46.222979+00
 df917fdb-1a51-488e-a88f-f8bb7a0d7c9f | b3f4fb84-bf17-5522-a5f3-70fd1212f381 | 44327172-046b-4320-b2b0-1205b4f7827e | 2020-09-13 10:00:20+00 |              0.554 | High               | Aggregated from 6 assessments; 6 at High or above; AreaRisk=0.70*p80(0,54)+0.30*max(0,59). |               6 | 2026-05-18 22:27:11.329234+00
 72cf15a1-18af-41af-8fda-1566b41bc119 | b3f4fb84-bf17-5522-a5f3-70fd1212f381 | 36caca67-352c-41f1-80e3-8fe951a1582c | 2020-09-13 10:00:20+00 |               0.54 | High               | Aggregated from 6 assessments; 6 at High or above; AreaRisk=0.70*p80(0,54)+0.30*max(0,54). |               6 | 2026-05-18 23:17:37.699652+00
 be6f0960-04c5-402c-926e-931d1d4cb4af | b3f4fb84-bf17-5522-a5f3-70fd1212f381 | 36caca67-352c-41f1-80e3-8fe951a1582c | 2020-09-13 10:00:20+00 |               0.54 | High               | Aggregated from 6 assessments; 6 at High or above; AreaRisk=0.70*p80(0,54)+0.30*max(0,54). |               6 | 2026-05-18 23:17:38.20408+00
 7a9887f3-0931-4e72-b42c-0477c1467f59 | b3f4fb84-bf17-5522-a5f3-70fd1212f381 | 36caca67-352c-41f1-80e3-8fe951a1582c | 2020-09-13 10:00:20+00 |               0.54 | High               | Aggregated from 6 assessments; 6 at High or above; AreaRisk=0.70*p80(0,54)+0.30*max(0,54). |               6 | 2026-05-18 23:17:40.215452+00
 f35ef8b9-2ea4-4455-aad3-9b88797cffb7 | b3f4fb84-bf17-5522-a5f3-70fd1212f381 | 36caca67-352c-41f1-80e3-8fe951a1582c | 2020-09-13 10:00:20+00 |               0.54 | High               | Aggregated from 6 assessments; 6 at High or above; AreaRisk=0.70*p80(0,54)+0.30*max(0,54). |               6 | 2026-05-18 23:17:41.213789+00
 2992fcd1-ebba-47fa-9bb1-c4ebc6f3f94a | b3f4fb84-bf17-5522-a5f3-70fd1212f381 | 33426c68-be78-46ae-8026-a03935b7daad | 2020-09-13 10:00:20+00 |               0.54 | High               | Aggregated from 6 assessments; 6 at High or above; AreaRisk=0.70*p80(0,54)+0.30*max(0,54). |               6 | 2026-05-18 17:55:34.706348+00
 95437990-b986-42d1-a871-6436004628a3 | b3f4fb84-bf17-5522-a5f3-70fd1212f381 | 44327172-046b-4320-b2b0-1205b4f7827e | 2020-09-13 10:00:20+00 |              0.554 | High               | Aggregated from 6 assessments; 6 at High or above; AreaRisk=0.70*p80(0,54)+0.30*max(0,59). |               6 | 2026-05-18 22:27:10.325308+00
 2dcaa330-b7c0-4929-83a2-093d62b72079 | b3f4fb84-bf17-5522-a5f3-70fd1212f381 | 44327172-046b-4320-b2b0-1205b4f7827e | 2020-09-13 10:00:20+00 |              0.554 | High               | Aggregated from 6 assessments; 6 at High or above; AreaRisk=0.70*p80(0,54)+0.30*max(0,59). |               6 | 2026-05-18 22:27:09.292141+00
 dfc70f64-afc9-4f2b-aa2c-70f890c4bffb | b3f4fb84-bf17-5522-a5f3-70fd1212f381 | d8203d4b-1839-4908-87ef-05633c1f1ae5 | 2020-09-13 10:00:20+00 |               0.54 | High               | Aggregated from 6 assessments; 6 at High or above; AreaRisk=0.70*p80(0,54)+0.30*max(0,54). |               6 | 2026-05-18 23:15:43.218775+00
 39b816e1-2a0a-4e67-a49f-0c06c6ae154e | b3f4fb84-bf17-5522-a5f3-70fd1212f381 | 33426c68-be78-46ae-8026-a03935b7daad | 2020-09-13 10:00:20+00 |               0.54 | High               | Aggregated from 6 assessments; 6 at High or above; AreaRisk=0.70*p80(0,54)+0.30*max(0,54). |               6 | 2026-05-18 17:55:35.707623+00
 9fbd09d9-dd88-4f38-8eaf-3db8a6996b4d | b3f4fb84-bf17-5522-a5f3-70fd1212f381 | 44327172-046b-4320-b2b0-1205b4f7827e | 2020-09-13 10:00:20+00 |               0.54 | High               | Aggregated from 6 assessments; 6 at High or above; AreaRisk=0.70*p80(0,54)+0.30*max(0,54). |               6 | 2026-05-18 22:27:12.325051+00
 ae065386-a789-4b38-af88-49c4e8f36f35 | b3f4fb84-bf17-5522-a5f3-70fd1212f381 | 33426c68-be78-46ae-8026-a03935b7daad | 2020-09-13 10:00:20+00 |               0.54 | High               | Aggregated from 6 assessments; 6 at High or above; AreaRisk=0.70*p80(0,54)+0.30*max(0,54). |               6 | 2026-05-18 17:55:30.727472+00
 8470e61e-d540-4d1f-acfa-7a9837bf0723 | b3f4fb84-bf17-5522-a5f3-70fd1212f381 | 33426c68-be78-46ae-8026-a03935b7daad | 2020-09-13 10:00:20+00 |               0.54 | High               | Aggregated from 6 assessments; 6 at High or above; AreaRisk=0.70*p80(0,54)+0.30*max(0,54). |               6 | 2026-05-18 17:55:31.708561+00
 7a4daf87-e872-4946-9df7-81b70b57cbfa | b3f4fb84-bf17-5522-a5f3-70fd1212f381 | 33426c68-be78-46ae-8026-a03935b7daad | 2020-09-13 10:00:20+00 |               0.54 | High               | Aggregated from 6 assessments; 6 at High or above; AreaRisk=0.70*p80(0,54)+0.30*max(0,54). |               6 | 2026-05-18 17:55:32.713383+00
 615cd288-c0c4-452a-9a6b-8b8133e4f3a5 | b3f4fb84-bf17-5522-a5f3-70fd1212f381 | d8203d4b-1839-4908-87ef-05633c1f1ae5 | 2020-09-13 10:00:15+00 |              0.515 | High               | Aggregated from 6 assessments; 6 at High or above; AreaRisk=0.70*p80(0,52)+0.30*max(0,52). |               6 | 2026-05-18 23:15:38.212688+00
 83183d83-b47c-4c16-af53-b4aa894f71d3 | b3f4fb84-bf17-5522-a5f3-70fd1212f381 | 44327172-046b-4320-b2b0-1205b4f7827e | 2020-09-13 10:00:15+00 |             0.5365 | High               | Aggregated from 6 assessments; 6 at High or above; AreaRisk=0.70*p80(0,52)+0.30*max(0,59). |               6 | 2026-05-18 22:27:05.329225+00
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
                  Id                  |                AreaId                |        ConfigurationVersionId        |        AreaOperationalStateId        |   AlertCode    | Severity | Status |                                                   Message                                                    |      TriggeredAt       |           UpdatedAt           | ResolvedAt 
--------------------------------------+--------------------------------------+--------------------------------------+--------------------------------------+----------------+----------+--------+--------------------------------------------------------------------------------------------------------------+------------------------+-------------------------------+------------
 bb10b4ce-8f7b-4eee-b6b7-e43354989f51 | b3f4fb84-bf17-5522-a5f3-70fd1212f381 | c03be3d5-1f70-15cb-2fc0-3c86a204a644 | ec827232-2c6b-474f-bb30-82a5eb564ca3 | area-risk-high | High     | Open   | AlertState=Warning; Area risk is High with adjusted score 0,54. Candidate Parameter Set V1.0 (non-official). | 2020-09-13 10:00:00+00 | 2026-05-18 23:17:42.227373+00 | âˆ…
(1 row)


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
   AlertCode    | Severity | Status | count |   first_triggered_at   |        last_updated_at        
----------------+----------+--------+-------+------------------------+-------------------------------
 area-risk-high | High     | Open   |     1 | 2020-09-13 10:00:00+00 | 2026-05-18 23:17:42.227373+00
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
                AreaId                | AggregateRiskScore | AggregateRiskLevel | area_severity |   SnapshotTimestamp    |        area_updated_at        |   AlertCode    | alert_severity | alert_status |                                                alert_message                                                 |      TriggeredAt       | ResolvedAt 
--------------------------------------+--------------------+--------------------+---------------+------------------------+-------------------------------+----------------+----------------+--------------+--------------------------------------------------------------------------------------------------------------+------------------------+------------
 b3f4fb84-bf17-5522-a5f3-70fd1212f381 |               0.54 | High               | High          | 2020-09-13 10:00:20+00 | 2026-05-18 23:17:42.227373+00 | area-risk-high | High           | Open         | AlertState=Warning; Area risk is High with adjusted score 0,54. Candidate Parameter Set V1.0 (non-official). | 2020-09-13 10:00:00+00 | âˆ…
(1 row)


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
    "aggregateRiskScore":  0.54,
    "aggregateRiskLevel":  "High",
    "severity":  "High",
    "summary":  "Aggregated from 6 assessments; 6 at High or above; AreaRisk=0.70*p80(0,54)+0.30*max(0,54).",
    "assessmentCount":  6,
    "updatedAt":  "2026-05-18T23:17:42.227373+00:00",
    "alertState":  "Warning"
}

```

## API active alerts

```text
{
    "value":  [
                  {
                      "id":  "bb10b4ce-8f7b-4eee-b6b7-e43354989f51",
                      "areaCode":  "proenca-a-nova",
                      "configurationVersionNumber":  1,
                      "alertCode":  "area-risk-high",
                      "severity":  "High",
                      "status":  "Open",
                      "message":  "AlertState=Warning; Area risk is High with adjusted score 0,54. Candidate Parameter Set V1.0 (non-official).",
                      "triggeredAt":  "2020-09-13T10:00:00+00:00",
                      "updatedAt":  "2026-05-18T23:17:42.227373+00:00",
                      "resolvedAt":  null,
                      "alertState":  "Warning"
                  }
              ],
    "Count":  1
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
