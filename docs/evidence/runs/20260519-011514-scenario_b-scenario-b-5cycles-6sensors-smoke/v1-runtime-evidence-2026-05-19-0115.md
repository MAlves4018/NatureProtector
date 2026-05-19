# NatureProtector V1 Runtime Evidence

- GeneratedAt: 2026-05-19T01:15:41.3371430+02:00
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
?? docs/evidence/runs/20260519-011514-scenario_b-scenario-b-5cycles-6sensors-smoke/

```

## Docker containers

```text
NAMES         IMAGE                                      STATUS          PORTS
np-influxdb   influxdb:3.7.0-core                        Up 49 minutes   0.0.0.0:8181->8181/tcp
np-grafana    grafana/grafana-enterprise:13.0.1-ubuntu   Up 49 minutes   0.0.0.0:3000->3000/tcp
np-postgres   postgres:16                                Up 49 minutes   0.0.0.0:5433->5432/tcp
np-rabbitmq   rabbitmq:4.0.6-management                  Up 49 minutes   4369/tcp, 5671/tcp, 0.0.0.0:5672->5672/tcp, 15671/tcp, 15691-15692/tcp, 25672/tcp, 0.0.0.0:15672->15672/tcp

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
                      1 |     1 |        467 |           75 |               3 |                    3 |               3
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
 d8203d4b-1839-4908-87ef-05633c1f1ae5 | b3f4fb84-bf17-5522-a5f3-70fd1212f381 | 6034251c-b502-5f7b-a557-7187178cfb04 | c03be3d5-1f70-15cb-2fc0-3c86a204a644 | scenario_b   | Scenario B - High Risk Proenca-a-Nova         | 2026-05-18 23:15:19.164093+00 | 2026-05-18 23:15:19.38574+00  | 2026-05-18 23:15:39.73622+00  | 2020-09-13 10:00:00+00 |               5 |              5 |         12345 |      3 | {"sensor_count":6,"scenario_category":"HighRisk","orchestrator_correlation_id":"0e1b4017-40ba-45a4-9ae0-bb3b73ed4a36","run_overrides":{"requested":{"sensor_count":6,"number_of_cycles":5,"interval_seconds":5,"seed":12345,"degradation_profile":"none","orchestrator_correlation_id":"0e1b4017-40ba-45a4-9ae0-bb3b73ed4a36"},"resolved":{"sensor_count":6,"number_of_cycles":5,"interval_seconds":5,"seed":12345,"degradation_profile":"none","orchestrator_correlation_id":"0e1b4017-40ba-45a4-9ae0-bb3b73ed4a36","selected_sensor_names":["pilot-humidity-0001","pilot-humidity-0230","pilot-temperature-0001","pilot-temperature-0230","pilot-wind-0001","pilot-wind-0230"]}}}
 44327172-046b-4320-b2b0-1205b4f7827e | b3f4fb84-bf17-5522-a5f3-70fd1212f381 | 319bae31-9b7a-57cf-9b05-3eefdf320c95 | c03be3d5-1f70-15cb-2fc0-3c86a204a644 | scenario_c   | Scenario C - Degraded Pipeline Proenca-a-Nova | 2026-05-18 22:26:48.951863+00 | 2026-05-18 22:26:49.082674+00 | 2026-05-18 22:27:09.250524+00 | 2020-09-13 10:00:00+00 |               5 |              5 |         12345 |      3 | {"sensor_count":6,"scenario_category":"Failure","orchestrator_correlation_id":"83f91aeb-51c9-40a1-aae6-2d56b098ff1e","run_overrides":{"requested":{"sensor_count":6,"number_of_cycles":5,"interval_seconds":5,"seed":12345,"degradation_profile":"missing-readings","orchestrator_correlation_id":"83f91aeb-51c9-40a1-aae6-2d56b098ff1e"},"resolved":{"sensor_count":6,"number_of_cycles":5,"interval_seconds":5,"seed":12345,"degradation_profile":"missing-readings","orchestrator_correlation_id":"83f91aeb-51c9-40a1-aae6-2d56b098ff1e","selected_sensor_names":["pilot-humidity-0001","pilot-humidity-0230","pilot-temperature-0001","pilot-temperature-0230","pilot-wind-0001","pilot-wind-0230"]}}}
 33426c68-be78-46ae-8026-a03935b7daad | b3f4fb84-bf17-5522-a5f3-70fd1212f381 | 6034251c-b502-5f7b-a557-7187178cfb04 | c03be3d5-1f70-15cb-2fc0-3c86a204a644 | scenario_b   | Scenario B - High Risk Proenca-a-Nova         | 2026-05-18 17:55:08.665907+00 | 2026-05-18 17:55:08.810153+00 | 2026-05-18 17:55:29.069849+00 | 2020-09-13 10:00:00+00 |               5 |              5 |         12345 |      3 | {"sensor_count":6,"scenario_category":"HighRisk","orchestrator_correlation_id":"7895953d-9d50-4baa-83fb-4c47408c89bb","run_overrides":{"requested":{"sensor_count":6,"number_of_cycles":5,"interval_seconds":5,"seed":12345,"degradation_profile":"none","orchestrator_correlation_id":"7895953d-9d50-4baa-83fb-4c47408c89bb"},"resolved":{"sensor_count":6,"number_of_cycles":5,"interval_seconds":5,"seed":12345,"degradation_profile":"none","orchestrator_correlation_id":"7895953d-9d50-4baa-83fb-4c47408c89bb","selected_sensor_names":["pilot-humidity-0001","pilot-humidity-0230","pilot-temperature-0001","pilot-temperature-0230","pilot-wind-0001","pilot-wind-0230"]}}}
(3 rows)


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
          73 |             73 |              6 |                 0
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
      2 |    72
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
 2026-05-18 17:55:09.105675+00 | 2026-05-18 23:15:43.176846+00 | 2020-09-13 10:00:00+00 | 2020-09-13 10:00:20+00
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
 2008b49b-beea-448f-a4b3-a5db4afe96a8 | 26662362-72fb-48c2-b979-328038304e80 | SensorReadingProduced | NatureProtector.Simulator.Host |      1 |            1 | 2020-09-13 10:00:20+00 | 2026-05-18 23:15:44.177692+00 | 2026-05-18 23:15:44.177692+00 | âˆ…                           | âˆ…           | âˆ…              | âˆ…
 6a7016dd-0b4d-4610-abb8-273e32328fce | dfc70f64-afc9-4f2b-aa2c-70f890c4bffb | SensorReadingProduced | NatureProtector.Simulator.Host |      2 |            1 | 2020-09-13 10:00:20+00 | 2026-05-18 23:15:43.176846+00 | 2026-05-18 23:15:44.162029+00 | 2026-05-18 23:15:44.162029+00 | âˆ…           | âˆ…              | âˆ…
 2b976102-6805-4fc4-bcf9-c34dce9b4a82 | 224ec085-9323-474a-a340-9fc331b214af | SensorReadingProduced | NatureProtector.Simulator.Host |      2 |            1 | 2020-09-13 10:00:15+00 | 2026-05-18 23:15:42.17534+00  | 2026-05-18 23:15:43.162522+00 | 2026-05-18 23:15:43.162522+00 | âˆ…           | âˆ…              | âˆ…
 767a0ee8-154f-4070-8f64-4885e4bf3966 | a5374875-a4bf-465a-8020-373d7f17d84e | SensorReadingProduced | NatureProtector.Simulator.Host |      2 |            1 | 2020-09-13 10:00:15+00 | 2026-05-18 23:15:41.178012+00 | 2026-05-18 23:15:42.161102+00 | 2026-05-18 23:15:42.161102+00 | âˆ…           | âˆ…              | âˆ…
 87ef8698-3903-4127-8404-ac21f1e52450 | 27edc86e-c18e-4bde-b729-999334fab094 | SensorReadingProduced | NatureProtector.Simulator.Host |      2 |            1 | 2020-09-13 10:00:15+00 | 2026-05-18 23:15:40.178223+00 | 2026-05-18 23:15:41.163093+00 | 2026-05-18 23:15:41.163093+00 | âˆ…           | âˆ…              | âˆ…
 5c6fd9bf-2a6e-4c76-a3a9-b4b078b8f48f | e2867bab-e3a4-4645-a6a3-6b2e128fce5c | SensorReadingProduced | NatureProtector.Simulator.Host |      2 |            1 | 2020-09-13 10:00:15+00 | 2026-05-18 23:15:39.175821+00 | 2026-05-18 23:15:40.163875+00 | 2026-05-18 23:15:40.163875+00 | âˆ…           | âˆ…              | âˆ…
 86a1783a-e277-4a0b-b15e-604a1a668b4d | 615cd288-c0c4-452a-9a6b-8b8133e4f3a5 | SensorReadingProduced | NatureProtector.Simulator.Host |      2 |            1 | 2020-09-13 10:00:15+00 | 2026-05-18 23:15:38.174966+00 | 2026-05-18 23:15:39.161655+00 | 2026-05-18 23:15:39.161655+00 | âˆ…           | âˆ…              | âˆ…
 bc79f266-4b01-44f5-81f9-428aa4a9c078 | 5dc90148-d598-45e8-8ef8-de84814d1c5e | SensorReadingProduced | NatureProtector.Simulator.Host |      2 |            1 | 2020-09-13 10:00:15+00 | 2026-05-18 23:15:37.177657+00 | 2026-05-18 23:15:38.161113+00 | 2026-05-18 23:15:38.161113+00 | âˆ…           | âˆ…              | âˆ…
 e2368f8c-c01b-442d-9adc-2af3432b1489 | 282801d0-c227-43a4-afa3-6ec4b9e7e68c | SensorReadingProduced | NatureProtector.Simulator.Host |      2 |            1 | 2020-09-13 10:00:10+00 | 2026-05-18 23:15:36.175313+00 | 2026-05-18 23:15:37.162314+00 | 2026-05-18 23:15:37.162314+00 | âˆ…           | âˆ…              | âˆ…
 3c44ad72-36d7-4ead-b385-f605632acf99 | bf810f12-4a6d-4b20-bc5a-d9b9a16aa6c7 | SensorReadingProduced | NatureProtector.Simulator.Host |      2 |            1 | 2020-09-13 10:00:10+00 | 2026-05-18 23:15:35.180151+00 | 2026-05-18 23:15:36.161292+00 | 2026-05-18 23:15:36.161292+00 | âˆ…           | âˆ…              | âˆ…
 e366e9ba-c330-4a0e-aa82-1f9ceb3d2119 | 100ba339-1a88-4570-bd36-f036704d9f4f | SensorReadingProduced | NatureProtector.Simulator.Host |      2 |            1 | 2020-09-13 10:00:10+00 | 2026-05-18 23:15:34.176191+00 | 2026-05-18 23:15:35.163846+00 | 2026-05-18 23:15:35.163846+00 | âˆ…           | âˆ…              | âˆ…
 77dae637-c13c-4e36-899a-98d4e39b5dec | 5d755307-92c2-4ee9-af3d-144439ff9e41 | SensorReadingProduced | NatureProtector.Simulator.Host |      2 |            1 | 2020-09-13 10:00:10+00 | 2026-05-18 23:15:33.175951+00 | 2026-05-18 23:15:34.162403+00 | 2026-05-18 23:15:34.162403+00 | âˆ…           | âˆ…              | âˆ…
 b6b9c327-f613-48f7-8342-89fe470f14a1 | a4aba6c2-0dcf-4703-9837-d70da48c3342 | SensorReadingProduced | NatureProtector.Simulator.Host |      2 |            1 | 2020-09-13 10:00:10+00 | 2026-05-18 23:15:32.177535+00 | 2026-05-18 23:15:33.162029+00 | 2026-05-18 23:15:33.162029+00 | âˆ…           | âˆ…              | âˆ…
 efec6ecc-3960-4ffd-966a-504ea99342a6 | ec93dcb5-ec44-4bad-bba1-a3309dd7ae8e | SensorReadingProduced | NatureProtector.Simulator.Host |      2 |            1 | 2020-09-13 10:00:10+00 | 2026-05-18 23:15:31.180874+00 | 2026-05-18 23:15:32.163495+00 | 2026-05-18 23:15:32.163495+00 | âˆ…           | âˆ…              | âˆ…
 508a9db6-8516-4975-abd5-d3203c2fcefb | a367b6a7-b423-45b9-b4cb-f277c1079a50 | SensorReadingProduced | NatureProtector.Simulator.Host |      2 |            1 | 2020-09-13 10:00:05+00 | 2026-05-18 23:15:30.182635+00 | 2026-05-18 23:15:31.161752+00 | 2026-05-18 23:15:31.161752+00 | âˆ…           | âˆ…              | âˆ…
 c034f1e0-ceef-4556-bd1f-c5eeedd8c1a6 | 7c50a662-84b7-4e46-b000-99e6ed879ead | SensorReadingProduced | NatureProtector.Simulator.Host |      2 |            1 | 2020-09-13 10:00:05+00 | 2026-05-18 23:15:29.181593+00 | 2026-05-18 23:15:30.164607+00 | 2026-05-18 23:15:30.164607+00 | âˆ…           | âˆ…              | âˆ…
 d48e00e6-a4aa-4e5d-a0f7-603694fa3801 | 0e8321fe-240d-4163-9335-4172ca95ab22 | SensorReadingProduced | NatureProtector.Simulator.Host |      2 |            1 | 2020-09-13 10:00:05+00 | 2026-05-18 23:15:28.177272+00 | 2026-05-18 23:15:29.165419+00 | 2026-05-18 23:15:29.165419+00 | âˆ…           | âˆ…              | âˆ…
 43a9c3de-6ffe-420d-a35d-c1dd731c0542 | fa9d7653-aa54-4e14-9a9a-97f125c51438 | SensorReadingProduced | NatureProtector.Simulator.Host |      2 |            1 | 2020-09-13 10:00:05+00 | 2026-05-18 23:15:27.175761+00 | 2026-05-18 23:15:28.16302+00  | 2026-05-18 23:15:28.16302+00  | âˆ…           | âˆ…              | âˆ…
 0ba51e2e-51a8-492d-8259-12176c49fefd | bdf6a26e-cd79-4456-8871-b248a55c799c | SensorReadingProduced | NatureProtector.Simulator.Host |      2 |            1 | 2020-09-13 10:00:05+00 | 2026-05-18 23:15:26.175466+00 | 2026-05-18 23:15:27.161882+00 | 2026-05-18 23:15:27.161882+00 | âˆ…           | âˆ…              | âˆ…
 c7674273-013a-4c04-8985-1b1207bc6de4 | c12e9c66-bb41-4ea9-b980-3c2bd8773c6c | SensorReadingProduced | NatureProtector.Simulator.Host |      2 |            1 | 2020-09-13 10:00:05+00 | 2026-05-18 23:15:25.18281+00  | 2026-05-18 23:15:26.162323+00 | 2026-05-18 23:15:26.162323+00 | âˆ…           | âˆ…              | âˆ…
 766a7e42-4709-4904-b873-0ecdac795c70 | a08f63dc-85dd-4dfe-945f-6fc11ec9704c | SensorReadingProduced | NatureProtector.Simulator.Host |      2 |            1 | 2020-09-13 10:00:00+00 | 2026-05-18 23:15:24.181212+00 | 2026-05-18 23:15:25.163056+00 | 2026-05-18 23:15:25.163056+00 | âˆ…           | âˆ…              | âˆ…
 411224f6-bb3a-4a04-8a47-b790aecb0c17 | fad53420-d2ea-41c1-85d9-277e10c30dff | SensorReadingProduced | NatureProtector.Simulator.Host |      2 |            1 | 2020-09-13 10:00:00+00 | 2026-05-18 23:15:23.177388+00 | 2026-05-18 23:15:24.16482+00  | 2026-05-18 23:15:24.16482+00  | âˆ…           | âˆ…              | âˆ…
 8b0724f7-8b44-4c71-971c-bcda42cbda66 | af0e311b-0cf5-418c-982c-c9db45072de5 | SensorReadingProduced | NatureProtector.Simulator.Host |      2 |            1 | 2020-09-13 10:00:00+00 | 2026-05-18 23:15:22.181355+00 | 2026-05-18 23:15:23.163001+00 | 2026-05-18 23:15:23.163001+00 | âˆ…           | âˆ…              | âˆ…
 392741ba-932b-4ec1-8c32-7b54627b3188 | 1bb0e60b-5960-4b01-8f6e-6d167b74a761 | SensorReadingProduced | NatureProtector.Simulator.Host |      2 |            1 | 2020-09-13 10:00:00+00 | 2026-05-18 23:15:21.179171+00 | 2026-05-18 23:15:22.164526+00 | 2026-05-18 23:15:22.164526+00 | âˆ…           | âˆ…              | âˆ…
 7fb5222a-d0c9-40f3-a36c-2d995083657d | 68bcef91-4cdb-4ebc-ae7b-a3b6db1df0de | SensorReadingProduced | NatureProtector.Simulator.Host |      2 |            1 | 2020-09-13 10:00:00+00 | 2026-05-18 23:15:20.222957+00 | 2026-05-18 23:15:21.164646+00 | 2026-05-18 23:15:21.164646+00 | âˆ…           | âˆ…              | âˆ…
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
 a446c14b-dc2e-4fa6-a19c-2a3e8bd9246e | 2008b49b-beea-448f-a4b3-a5db4afe96a8 |             1 | reading_risk_pipeline | 2026-05-18 23:15:44.177692+00 | âˆ…                           |       0 | âˆ…       | âˆ…
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
 c3abe9a8-6fc5-4010-a35c-1a341bcb4776 | 8b0724f7-8b44-4c71-971c-bcda42cbda66 |             1 | reading_risk_pipeline | 2026-05-18 23:15:22.181355+00 | 2026-05-18 23:15:23.163001+00 |       1 | âˆ…       | âˆ…
 1d8f136a-ae46-47c1-bb52-55a6159becac | 392741ba-932b-4ec1-8c32-7b54627b3188 |             1 | reading_risk_pipeline | 2026-05-18 23:15:21.179171+00 | 2026-05-18 23:15:22.164526+00 |       1 | âˆ…       | âˆ…
 4ebcf829-fc30-4fca-9f70-2de57e4a6ad4 | 7fb5222a-d0c9-40f3-a36c-2d995083657d |             1 | reading_risk_pipeline | 2026-05-18 23:15:20.222957+00 | 2026-05-18 23:15:21.164646+00 |       1 | âˆ…       | âˆ…
 2a6ed3e9-1cf6-416f-8ae2-b440d8ba86bb | 76b46a2c-d2bf-4fa3-a996-4e96b7738193 |             1 | reading_risk_pipeline | 2026-05-18 23:15:19.610429+00 | 2026-05-18 23:15:20.208981+00 |       1 | âˆ…       | âˆ…
 45bf9ad2-0171-4d3f-9679-855a9b7c6a3c | dfc9167f-db0a-409b-8df7-90fa27ca1e6a |             1 | reading_risk_pipeline | 2026-05-18 22:27:13.293232+00 | 2026-05-18 22:27:14.280174+00 |       1 | âˆ…       | âˆ…
 501a452b-af3a-4cc1-afe5-4a1eeb704a01 | 1b6f1c0b-7c1d-43be-947e-e1a5e1cc844e |             1 | reading_risk_pipeline | 2026-05-18 22:27:12.292765+00 | 2026-05-18 22:27:13.280528+00 |       1 | âˆ…       | âˆ…
 0146ee07-1684-4454-b64b-0fa488490f6f | e7a34490-b757-4a51-b43e-bf936546febe |             1 | reading_risk_pipeline | 2026-05-18 22:27:11.294127+00 | 2026-05-18 22:27:12.28004+00  |       1 | âˆ…       | âˆ…
 bcf9de2e-2a87-4587-bd95-a72e68a3f00c | 6ad05a2a-2721-44b0-ab39-60a48e7624fe |             1 | reading_risk_pipeline | 2026-05-18 22:27:10.294661+00 | 2026-05-18 22:27:11.280456+00 |       1 | âˆ…       | âˆ…
 520919ff-4c9e-478c-a70b-8caf4c4cb581 | 866f9e0e-a5f5-44a2-af11-43ba9643cee1 |             1 | reading_risk_pipeline | 2026-05-18 22:27:09.253436+00 | 2026-05-18 22:27:10.281992+00 |       1 | âˆ…       | âˆ…
 2e914b89-3264-4c13-9c4f-d6de556f94cb | 671f09d7-4d90-4224-8a60-eabb0657fa9a |             1 | reading_risk_pipeline | 2026-05-18 22:27:07.293423+00 | 2026-05-18 22:27:08.281123+00 |       1 | âˆ…       | âˆ…
 f493f777-79a5-448d-acbf-c9c8b40a04ad | e4a1b2c1-f463-4a9f-b9df-0e2ab2b6d192 |             1 | reading_risk_pipeline | 2026-05-18 22:27:06.293409+00 | 2026-05-18 22:27:07.280527+00 |       1 | âˆ…       | âˆ…
 dc33df8c-f0e1-45e5-bc57-5fd9d2771956 | e0b8c2e3-3663-44db-a939-d6641fe66ffd |             1 | reading_risk_pipeline | 2026-05-18 22:27:05.29541+00  | 2026-05-18 22:27:06.28057+00  |       1 | âˆ…       | âˆ…
 5ff29eda-06a3-4092-96fc-eacec4b789c8 | 4ae4f327-2ce0-4496-8e56-b3a4326a36c9 |             1 | reading_risk_pipeline | 2026-05-18 22:27:04.293164+00 | 2026-05-18 22:27:05.282298+00 |       1 | âˆ…       | âˆ…
 83f919c7-982b-43a3-abe3-20b0bad33f96 | 3d86eea3-5582-4c55-b63a-14c0bfb8cca7 |             1 | reading_risk_pipeline | 2026-05-18 22:27:03.296216+00 | 2026-05-18 22:27:04.28029+00  |       1 | âˆ…       | âˆ…
 2b3b83af-2abc-48d5-ae7e-1f6aaadf10fb | 3cde06e2-ffa6-45c7-940d-77bfb85fe221 |             1 | reading_risk_pipeline | 2026-05-18 22:27:02.294276+00 | 2026-05-18 22:27:03.281204+00 |       1 | âˆ…       | âˆ…
 6dc37a7e-3e9f-4a9a-a6e4-3280eeaa9af6 | 02f2b1f2-466f-4712-92d1-8e678f166c20 |             1 | reading_risk_pipeline | 2026-05-18 22:27:01.29379+00  | 2026-05-18 22:27:02.281243+00 |       1 | âˆ…       | âˆ…
 b7dc9650-1666-4038-b1a7-a47928ec03b5 | 7dcd6bd9-9bd5-40bb-ac9a-580f4e1e93f4 |             1 | reading_risk_pipeline | 2026-05-18 22:27:00.295113+00 | 2026-05-18 22:27:01.28098+00  |       1 | âˆ…       | âˆ…
 21e4a410-ad23-42fc-aa1f-c8894c16faa7 | 5b19b743-9b88-4bab-9335-fc733988a5a1 |             1 | reading_risk_pipeline | 2026-05-18 22:26:59.294292+00 | 2026-05-18 22:27:00.281059+00 |       1 | âˆ…       | âˆ…
 152bf41e-beeb-41e0-887b-8be5b96bcdfe | 67bcdd86-dfde-4b71-882b-624982797d4f |             1 | reading_risk_pipeline | 2026-05-18 22:26:58.294805+00 | 2026-05-18 22:26:59.281126+00 |       1 | âˆ…       | âˆ…
 e073cf8e-d2e6-4c7a-9144-6c6e8d164218 | 87ad67af-9bc1-412a-9a02-eaad6c32ab83 |             1 | reading_risk_pipeline | 2026-05-18 22:26:57.296867+00 | 2026-05-18 22:26:58.281398+00 |       1 | âˆ…       | âˆ…
 c62bc366-2e94-46bd-b47f-527de2202265 | 06b167cf-3147-4a71-baf4-4158b00c7929 |             1 | reading_risk_pipeline | 2026-05-18 22:26:56.296523+00 | 2026-05-18 22:26:57.281437+00 |       1 | âˆ…       | âˆ…
 630274a8-6c97-458a-ab53-bbb5875ad149 | 3df6548e-fb99-4619-8110-bba03b247c43 |             1 | reading_risk_pipeline | 2026-05-18 22:26:55.298021+00 | 2026-05-18 22:26:56.281747+00 |       1 | âˆ…       | âˆ…
 de8a6ee3-555b-497a-ba12-bfcb5424236c | c64a7f57-7cb9-422d-9469-45809b297201 |             1 | reading_risk_pipeline | 2026-05-18 22:26:54.232405+00 | 2026-05-18 22:26:55.281811+00 |       1 | âˆ…       | âˆ…
 8b9d9c27-0384-4950-b252-084dc7047909 | 01b76792-b2b3-495c-bf4b-f969b6797887 |             1 | reading_risk_pipeline | 2026-05-18 22:26:50.329669+00 | 2026-05-18 22:26:51.283228+00 |       1 | âˆ…       | âˆ…
 6501a870-2ad1-49ed-ac3a-928ac36d30d1 | 4a40957d-af91-4c11-8777-ba4377ef2081 |             1 | reading_risk_pipeline | 2026-05-18 22:26:49.398587+00 | 2026-05-18 22:26:50.303315+00 |       1 | âˆ…       | âˆ…
 5e3349b2-9b11-4ac3-b705-072ba3e63992 | b77761f8-4251-46b3-ba89-ec3ea7568935 |             1 | reading_risk_pipeline | 2026-05-18 17:55:35.672882+00 | 2026-05-18 17:55:36.658111+00 |       1 | âˆ…       | âˆ…
 95bd2708-04fc-4aee-ad48-efcab779f529 | 77ef5555-82da-4188-a8b7-ef8e894d2436 |             1 | reading_risk_pipeline | 2026-05-18 17:55:34.671328+00 | 2026-05-18 17:55:35.6595+00   |       1 | âˆ…       | âˆ…
 117ccdad-b81e-4e3b-8a85-830c0c8af947 | 6a2cb237-27a0-4647-928f-3af4dbb9f011 |             1 | reading_risk_pipeline | 2026-05-18 17:55:33.674252+00 | 2026-05-18 17:55:34.657925+00 |       1 | âˆ…       | âˆ…
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
                75 |               75 |                  75 |                       2 |                       1 |            1
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
 2026-05-18 17:55:09.476337+00 | 2026-05-18 23:15:45.206262+00 | 2020-09-13 10:00:00+00        | 2020-09-13 10:00:20+00       | 2026-05-18 23:15:45.241431+00 | 2026-05-18 23:15:45.241431+00
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
               76 |          0.515 |            0.6 | 0.5380043859649122
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
 High      |    76 |     0.515 |       0.6
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
 71255f23-5485-4cc0-8b63-d0d28a5c3b53 | b3f4fb84-bf17-5522-a5f3-70fd1212f381 | 8e6a3d8a-7e65-4fa5-8c51-fa4597b203b8 | 8a65d4e7-92f5-1b6f-fdb0-d60ec5707d72 | 5e03377d-1337-4834-aa78-e045c33e3a6f | 2020-09-13 10:00:20+00 |               0.54 | High      | Area=b3f4fb84-bf17-5522-a5f3-70fd1212f381; Sensor=8e6a3d8a-7e65-4fa5-8c51-fa4597b203b8; Event=5e03377d-1337-4834-aa78-e045c33e3a6f; Metric=Temperature; Value=32,62; InputStatus=CompleteEligible; M=0,58; D=0,50; T=0,50; BaseRisk=0,54; AdjustedScore=0,54; C=1,00; I=1,00; FWI=absent; KBDI=absent; FireIndexProvenance=absent; ParameterSet=Candidate Parameter Set V1.0 (non-calibrated). | 2026-05-18 23:15:46.207066+00
 47bb44fc-70b9-4e8b-bf8a-ef9bd769dd35 | b3f4fb84-bf17-5522-a5f3-70fd1212f381 | 27bf9aad-094f-7dc6-45b2-129a8cc0522e | 33d52e78-7801-cf3b-bc8b-c798db0d4068 | d50f8788-b690-46a4-aea3-a652e722b439 | 2020-09-13 10:00:20+00 |               0.54 | High      | Area=b3f4fb84-bf17-5522-a5f3-70fd1212f381; Sensor=27bf9aad-094f-7dc6-45b2-129a8cc0522e; Event=d50f8788-b690-46a4-aea3-a652e722b439; Metric=Temperature; Value=32,21; InputStatus=CompleteEligible; M=0,58; D=0,50; T=0,50; BaseRisk=0,54; AdjustedScore=0,54; C=1,00; I=1,00; FWI=absent; KBDI=absent; FireIndexProvenance=absent; ParameterSet=Candidate Parameter Set V1.0 (non-calibrated). | 2026-05-18 23:15:45.206262+00
 9409e768-1a81-4c56-8871-71fcb12b326c | b3f4fb84-bf17-5522-a5f3-70fd1212f381 | 2f494582-4077-86f2-9707-780861949155 | 8a65d4e7-92f5-1b6f-fdb0-d60ec5707d72 | 26662362-72fb-48c2-b979-328038304e80 | 2020-09-13 10:00:20+00 |               0.54 | High      | Area=b3f4fb84-bf17-5522-a5f3-70fd1212f381; Sensor=2f494582-4077-86f2-9707-780861949155; Event=26662362-72fb-48c2-b979-328038304e80; Metric=Humidity; Value=20,88; InputStatus=CompleteEligible; M=0,58; D=0,50; T=0,50; BaseRisk=0,54; AdjustedScore=0,54; C=1,00; I=1,00; FWI=absent; KBDI=absent; FireIndexProvenance=absent; ParameterSet=Candidate Parameter Set V1.0 (non-calibrated).    | 2026-05-18 23:15:44.202426+00
 5cc1d1e6-24e5-4dad-8305-1096da6df9d0 | b3f4fb84-bf17-5522-a5f3-70fd1212f381 | 22702eee-75aa-3cf4-31ab-b689bfb9a8bc | 33d52e78-7801-cf3b-bc8b-c798db0d4068 | dfc70f64-afc9-4f2b-aa2c-70f890c4bffb | 2020-09-13 10:00:20+00 |               0.54 | High      | Area=b3f4fb84-bf17-5522-a5f3-70fd1212f381; Sensor=22702eee-75aa-3cf4-31ab-b689bfb9a8bc; Event=dfc70f64-afc9-4f2b-aa2c-70f890c4bffb; Metric=Humidity; Value=21,18; InputStatus=CompleteEligible; M=0,58; D=0,50; T=0,50; BaseRisk=0,54; AdjustedScore=0,54; C=1,00; I=1,00; FWI=absent; KBDI=absent; FireIndexProvenance=absent; ParameterSet=Candidate Parameter Set V1.0 (non-calibrated).    | 2026-05-18 23:15:43.203609+00
 d8ed6f54-5e15-407e-ad78-037de4bdfd8a | b3f4fb84-bf17-5522-a5f3-70fd1212f381 | 17badc17-7920-17ac-c213-db1c12bedfb2 | 8a65d4e7-92f5-1b6f-fdb0-d60ec5707d72 | 224ec085-9323-474a-a340-9fc331b214af | 2020-09-13 10:00:15+00 |               0.54 | High      | Area=b3f4fb84-bf17-5522-a5f3-70fd1212f381; Sensor=17badc17-7920-17ac-c213-db1c12bedfb2; Event=224ec085-9323-474a-a340-9fc331b214af; Metric=WindSpeed; Value=5,32; InputStatus=CompleteEligible; M=0,58; D=0,50; T=0,50; BaseRisk=0,54; AdjustedScore=0,54; C=1,00; I=1,00; FWI=absent; KBDI=absent; FireIndexProvenance=absent; ParameterSet=Candidate Parameter Set V1.0 (non-calibrated).    | 2026-05-18 23:15:42.198785+00
 0b1e7208-e0c5-4b42-b0a5-f6799b27d836 | b3f4fb84-bf17-5522-a5f3-70fd1212f381 | e7660606-cadd-4212-e67b-574c09ef789a | 33d52e78-7801-cf3b-bc8b-c798db0d4068 | a5374875-a4bf-465a-8020-373d7f17d84e | 2020-09-13 10:00:15+00 |               0.54 | High      | Area=b3f4fb84-bf17-5522-a5f3-70fd1212f381; Sensor=e7660606-cadd-4212-e67b-574c09ef789a; Event=a5374875-a4bf-465a-8020-373d7f17d84e; Metric=WindSpeed; Value=5,49; InputStatus=CompleteEligible; M=0,58; D=0,50; T=0,50; BaseRisk=0,54; AdjustedScore=0,54; C=1,00; I=1,00; FWI=absent; KBDI=absent; FireIndexProvenance=absent; ParameterSet=Candidate Parameter Set V1.0 (non-calibrated).    | 2026-05-18 23:15:41.20416+00
 7c587729-8dc5-4ff3-b790-fead42af5f2b | b3f4fb84-bf17-5522-a5f3-70fd1212f381 | 8e6a3d8a-7e65-4fa5-8c51-fa4597b203b8 | 8a65d4e7-92f5-1b6f-fdb0-d60ec5707d72 | 27edc86e-c18e-4bde-b729-999334fab094 | 2020-09-13 10:00:15+00 |              0.515 | High      | Area=b3f4fb84-bf17-5522-a5f3-70fd1212f381; Sensor=8e6a3d8a-7e65-4fa5-8c51-fa4597b203b8; Event=27edc86e-c18e-4bde-b729-999334fab094; Metric=Temperature; Value=31,91; InputStatus=CompleteEligible; M=0,53; D=0,50; T=0,50; BaseRisk=0,52; AdjustedScore=0,52; C=1,00; I=1,00; FWI=absent; KBDI=absent; FireIndexProvenance=absent; ParameterSet=Candidate Parameter Set V1.0 (non-calibrated). | 2026-05-18 23:15:40.202582+00
 d4ca2f6b-4917-4fc2-9bc3-4970c0ead0cc | b3f4fb84-bf17-5522-a5f3-70fd1212f381 | 27bf9aad-094f-7dc6-45b2-129a8cc0522e | 33d52e78-7801-cf3b-bc8b-c798db0d4068 | e2867bab-e3a4-4645-a6a3-6b2e128fce5c | 2020-09-13 10:00:15+00 |              0.515 | High      | Area=b3f4fb84-bf17-5522-a5f3-70fd1212f381; Sensor=27bf9aad-094f-7dc6-45b2-129a8cc0522e; Event=e2867bab-e3a4-4645-a6a3-6b2e128fce5c; Metric=Temperature; Value=32,05; InputStatus=CompleteEligible; M=0,53; D=0,50; T=0,50; BaseRisk=0,52; AdjustedScore=0,52; C=1,00; I=1,00; FWI=absent; KBDI=absent; FireIndexProvenance=absent; ParameterSet=Candidate Parameter Set V1.0 (non-calibrated). | 2026-05-18 23:15:39.210621+00
 a9bd5a55-0e47-4507-87a9-7b5b10e69c78 | b3f4fb84-bf17-5522-a5f3-70fd1212f381 | 2f494582-4077-86f2-9707-780861949155 | 8a65d4e7-92f5-1b6f-fdb0-d60ec5707d72 | 615cd288-c0c4-452a-9a6b-8b8133e4f3a5 | 2020-09-13 10:00:15+00 |              0.515 | High      | Area=b3f4fb84-bf17-5522-a5f3-70fd1212f381; Sensor=2f494582-4077-86f2-9707-780861949155; Event=615cd288-c0c4-452a-9a6b-8b8133e4f3a5; Metric=Humidity; Value=20,91; InputStatus=CompleteEligible; M=0,53; D=0,50; T=0,50; BaseRisk=0,52; AdjustedScore=0,52; C=1,00; I=1,00; FWI=absent; KBDI=absent; FireIndexProvenance=absent; ParameterSet=Candidate Parameter Set V1.0 (non-calibrated).    | 2026-05-18 23:15:38.199138+00
 66eb4089-3f59-4618-8030-559bf887abb3 | b3f4fb84-bf17-5522-a5f3-70fd1212f381 | 22702eee-75aa-3cf4-31ab-b689bfb9a8bc | 33d52e78-7801-cf3b-bc8b-c798db0d4068 | 5dc90148-d598-45e8-8ef8-de84814d1c5e | 2020-09-13 10:00:15+00 |              0.515 | High      | Area=b3f4fb84-bf17-5522-a5f3-70fd1212f381; Sensor=22702eee-75aa-3cf4-31ab-b689bfb9a8bc; Event=5dc90148-d598-45e8-8ef8-de84814d1c5e; Metric=Humidity; Value=21,35; InputStatus=CompleteEligible; M=0,53; D=0,50; T=0,50; BaseRisk=0,52; AdjustedScore=0,52; C=1,00; I=1,00; FWI=absent; KBDI=absent; FireIndexProvenance=absent; ParameterSet=Candidate Parameter Set V1.0 (non-calibrated).    | 2026-05-18 23:15:37.201051+00
 380ad466-55cd-4177-985b-767bb8360944 | b3f4fb84-bf17-5522-a5f3-70fd1212f381 | 17badc17-7920-17ac-c213-db1c12bedfb2 | 8a65d4e7-92f5-1b6f-fdb0-d60ec5707d72 | 282801d0-c227-43a4-afa3-6ec4b9e7e68c | 2020-09-13 10:00:10+00 |              0.515 | High      | Area=b3f4fb84-bf17-5522-a5f3-70fd1212f381; Sensor=17badc17-7920-17ac-c213-db1c12bedfb2; Event=282801d0-c227-43a4-afa3-6ec4b9e7e68c; Metric=WindSpeed; Value=4,93; InputStatus=CompleteEligible; M=0,53; D=0,50; T=0,50; BaseRisk=0,52; AdjustedScore=0,52; C=1,00; I=1,00; FWI=absent; KBDI=absent; FireIndexProvenance=absent; ParameterSet=Candidate Parameter Set V1.0 (non-calibrated).    | 2026-05-18 23:15:36.198918+00
 1ec58d3d-81fc-4b97-8a0f-0fd7868dd243 | b3f4fb84-bf17-5522-a5f3-70fd1212f381 | e7660606-cadd-4212-e67b-574c09ef789a | 33d52e78-7801-cf3b-bc8b-c798db0d4068 | bf810f12-4a6d-4b20-bc5a-d9b9a16aa6c7 | 2020-09-13 10:00:10+00 |              0.515 | High      | Area=b3f4fb84-bf17-5522-a5f3-70fd1212f381; Sensor=e7660606-cadd-4212-e67b-574c09ef789a; Event=bf810f12-4a6d-4b20-bc5a-d9b9a16aa6c7; Metric=WindSpeed; Value=4,84; InputStatus=CompleteEligible; M=0,53; D=0,50; T=0,50; BaseRisk=0,52; AdjustedScore=0,52; C=1,00; I=1,00; FWI=absent; KBDI=absent; FireIndexProvenance=absent; ParameterSet=Candidate Parameter Set V1.0 (non-calibrated).    | 2026-05-18 23:15:35.202591+00
 87ca6d14-ace7-4b77-a8d7-73dad8d8a1f8 | b3f4fb84-bf17-5522-a5f3-70fd1212f381 | 8e6a3d8a-7e65-4fa5-8c51-fa4597b203b8 | 8a65d4e7-92f5-1b6f-fdb0-d60ec5707d72 | 100ba339-1a88-4570-bd36-f036704d9f4f | 2020-09-13 10:00:10+00 |              0.515 | High      | Area=b3f4fb84-bf17-5522-a5f3-70fd1212f381; Sensor=8e6a3d8a-7e65-4fa5-8c51-fa4597b203b8; Event=100ba339-1a88-4570-bd36-f036704d9f4f; Metric=Temperature; Value=31,96; InputStatus=CompleteEligible; M=0,53; D=0,50; T=0,50; BaseRisk=0,52; AdjustedScore=0,52; C=1,00; I=1,00; FWI=absent; KBDI=absent; FireIndexProvenance=absent; ParameterSet=Candidate Parameter Set V1.0 (non-calibrated). | 2026-05-18 23:15:34.197606+00
 4433e6ad-01f7-44b6-9f6a-b8f45325c7a4 | b3f4fb84-bf17-5522-a5f3-70fd1212f381 | 27bf9aad-094f-7dc6-45b2-129a8cc0522e | 33d52e78-7801-cf3b-bc8b-c798db0d4068 | 5d755307-92c2-4ee9-af3d-144439ff9e41 | 2020-09-13 10:00:10+00 |              0.515 | High      | Area=b3f4fb84-bf17-5522-a5f3-70fd1212f381; Sensor=27bf9aad-094f-7dc6-45b2-129a8cc0522e; Event=5d755307-92c2-4ee9-af3d-144439ff9e41; Metric=Temperature; Value=31,88; InputStatus=CompleteEligible; M=0,53; D=0,50; T=0,50; BaseRisk=0,52; AdjustedScore=0,52; C=1,00; I=1,00; FWI=absent; KBDI=absent; FireIndexProvenance=absent; ParameterSet=Candidate Parameter Set V1.0 (non-calibrated). | 2026-05-18 23:15:33.200017+00
 da9c2532-3dd9-4fa4-86f7-33ef94e07e68 | b3f4fb84-bf17-5522-a5f3-70fd1212f381 | 2f494582-4077-86f2-9707-780861949155 | 8a65d4e7-92f5-1b6f-fdb0-d60ec5707d72 | a4aba6c2-0dcf-4703-9837-d70da48c3342 | 2020-09-13 10:00:10+00 |              0.515 | High      | Area=b3f4fb84-bf17-5522-a5f3-70fd1212f381; Sensor=2f494582-4077-86f2-9707-780861949155; Event=a4aba6c2-0dcf-4703-9837-d70da48c3342; Metric=Humidity; Value=22,47; InputStatus=CompleteEligible; M=0,53; D=0,50; T=0,50; BaseRisk=0,52; AdjustedScore=0,52; C=1,00; I=1,00; FWI=absent; KBDI=absent; FireIndexProvenance=absent; ParameterSet=Candidate Parameter Set V1.0 (non-calibrated).    | 2026-05-18 23:15:32.206855+00
 ff997e9c-ece7-4a7f-b5a8-1bc1ec1d0c79 | b3f4fb84-bf17-5522-a5f3-70fd1212f381 | 22702eee-75aa-3cf4-31ab-b689bfb9a8bc | 33d52e78-7801-cf3b-bc8b-c798db0d4068 | ec93dcb5-ec44-4bad-bba1-a3309dd7ae8e | 2020-09-13 10:00:10+00 |              0.515 | High      | Area=b3f4fb84-bf17-5522-a5f3-70fd1212f381; Sensor=22702eee-75aa-3cf4-31ab-b689bfb9a8bc; Event=ec93dcb5-ec44-4bad-bba1-a3309dd7ae8e; Metric=Humidity; Value=21,98; InputStatus=CompleteEligible; M=0,53; D=0,50; T=0,50; BaseRisk=0,52; AdjustedScore=0,52; C=1,00; I=1,00; FWI=absent; KBDI=absent; FireIndexProvenance=absent; ParameterSet=Candidate Parameter Set V1.0 (non-calibrated).    | 2026-05-18 23:15:31.204636+00
 a2bd408a-5d58-4d90-a01b-5fce266d9d60 | b3f4fb84-bf17-5522-a5f3-70fd1212f381 | 17badc17-7920-17ac-c213-db1c12bedfb2 | 8a65d4e7-92f5-1b6f-fdb0-d60ec5707d72 | a367b6a7-b423-45b9-b4cb-f277c1079a50 | 2020-09-13 10:00:05+00 |              0.515 | High      | Area=b3f4fb84-bf17-5522-a5f3-70fd1212f381; Sensor=17badc17-7920-17ac-c213-db1c12bedfb2; Event=a367b6a7-b423-45b9-b4cb-f277c1079a50; Metric=WindSpeed; Value=4,45; InputStatus=CompleteEligible; M=0,53; D=0,50; T=0,50; BaseRisk=0,52; AdjustedScore=0,52; C=1,00; I=1,00; FWI=absent; KBDI=absent; FireIndexProvenance=absent; ParameterSet=Candidate Parameter Set V1.0 (non-calibrated).    | 2026-05-18 23:15:30.205411+00
 aaca8890-6b14-417e-95a2-39bde22560b1 | b3f4fb84-bf17-5522-a5f3-70fd1212f381 | e7660606-cadd-4212-e67b-574c09ef789a | 33d52e78-7801-cf3b-bc8b-c798db0d4068 | 7c50a662-84b7-4e46-b000-99e6ed879ead | 2020-09-13 10:00:05+00 |              0.515 | High      | Area=b3f4fb84-bf17-5522-a5f3-70fd1212f381; Sensor=e7660606-cadd-4212-e67b-574c09ef789a; Event=7c50a662-84b7-4e46-b000-99e6ed879ead; Metric=WindSpeed; Value=4,43; InputStatus=CompleteEligible; M=0,53; D=0,50; T=0,50; BaseRisk=0,52; AdjustedScore=0,52; C=1,00; I=1,00; FWI=absent; KBDI=absent; FireIndexProvenance=absent; ParameterSet=Candidate Parameter Set V1.0 (non-calibrated).    | 2026-05-18 23:15:29.212257+00
 33fe861c-dccb-4598-97dc-305f7a0d15a7 | b3f4fb84-bf17-5522-a5f3-70fd1212f381 | 8e6a3d8a-7e65-4fa5-8c51-fa4597b203b8 | 8a65d4e7-92f5-1b6f-fdb0-d60ec5707d72 | 0e8321fe-240d-4163-9335-4172ca95ab22 | 2020-09-13 10:00:05+00 |              0.515 | High      | Area=b3f4fb84-bf17-5522-a5f3-70fd1212f381; Sensor=8e6a3d8a-7e65-4fa5-8c51-fa4597b203b8; Event=0e8321fe-240d-4163-9335-4172ca95ab22; Metric=Temperature; Value=31,51; InputStatus=CompleteEligible; M=0,53; D=0,50; T=0,50; BaseRisk=0,52; AdjustedScore=0,52; C=1,00; I=1,00; FWI=absent; KBDI=absent; FireIndexProvenance=absent; ParameterSet=Candidate Parameter Set V1.0 (non-calibrated). | 2026-05-18 23:15:28.200816+00
 1a8df32b-8c08-46c7-bf39-015ce3eddaf4 | b3f4fb84-bf17-5522-a5f3-70fd1212f381 | 27bf9aad-094f-7dc6-45b2-129a8cc0522e | 33d52e78-7801-cf3b-bc8b-c798db0d4068 | fa9d7653-aa54-4e14-9a9a-97f125c51438 | 2020-09-13 10:00:05+00 |              0.515 | High      | Area=b3f4fb84-bf17-5522-a5f3-70fd1212f381; Sensor=27bf9aad-094f-7dc6-45b2-129a8cc0522e; Event=fa9d7653-aa54-4e14-9a9a-97f125c51438; Metric=Temperature; Value=31,60; InputStatus=CompleteEligible; M=0,53; D=0,50; T=0,50; BaseRisk=0,52; AdjustedScore=0,52; C=1,00; I=1,00; FWI=absent; KBDI=absent; FireIndexProvenance=absent; ParameterSet=Candidate Parameter Set V1.0 (non-calibrated). | 2026-05-18 23:15:27.200436+00
 4310651f-ad92-42ed-b81c-07e4a409ffa7 | b3f4fb84-bf17-5522-a5f3-70fd1212f381 | 2f494582-4077-86f2-9707-780861949155 | 8a65d4e7-92f5-1b6f-fdb0-d60ec5707d72 | bdf6a26e-cd79-4456-8871-b248a55c799c | 2020-09-13 10:00:05+00 |              0.515 | High      | Area=b3f4fb84-bf17-5522-a5f3-70fd1212f381; Sensor=2f494582-4077-86f2-9707-780861949155; Event=bdf6a26e-cd79-4456-8871-b248a55c799c; Metric=Humidity; Value=23,91; InputStatus=CompleteEligible; M=0,53; D=0,50; T=0,50; BaseRisk=0,52; AdjustedScore=0,52; C=1,00; I=1,00; FWI=absent; KBDI=absent; FireIndexProvenance=absent; ParameterSet=Candidate Parameter Set V1.0 (non-calibrated).    | 2026-05-18 23:15:26.198713+00
 aa4a4455-a29f-4e3a-8a80-a747cfda6e9d | b3f4fb84-bf17-5522-a5f3-70fd1212f381 | 22702eee-75aa-3cf4-31ab-b689bfb9a8bc | 33d52e78-7801-cf3b-bc8b-c798db0d4068 | c12e9c66-bb41-4ea9-b980-3c2bd8773c6c | 2020-09-13 10:00:05+00 |              0.515 | High      | Area=b3f4fb84-bf17-5522-a5f3-70fd1212f381; Sensor=22702eee-75aa-3cf4-31ab-b689bfb9a8bc; Event=c12e9c66-bb41-4ea9-b980-3c2bd8773c6c; Metric=Humidity; Value=23,70; InputStatus=CompleteEligible; M=0,53; D=0,50; T=0,50; BaseRisk=0,52; AdjustedScore=0,52; C=1,00; I=1,00; FWI=absent; KBDI=absent; FireIndexProvenance=absent; ParameterSet=Candidate Parameter Set V1.0 (non-calibrated).    | 2026-05-18 23:15:25.204434+00
 adb23787-d0c4-4882-ac22-e11959b83cfb | b3f4fb84-bf17-5522-a5f3-70fd1212f381 | 17badc17-7920-17ac-c213-db1c12bedfb2 | 8a65d4e7-92f5-1b6f-fdb0-d60ec5707d72 | a08f63dc-85dd-4dfe-945f-6fc11ec9704c | 2020-09-13 10:00:00+00 |              0.515 | High      | Area=b3f4fb84-bf17-5522-a5f3-70fd1212f381; Sensor=17badc17-7920-17ac-c213-db1c12bedfb2; Event=a08f63dc-85dd-4dfe-945f-6fc11ec9704c; Metric=WindSpeed; Value=3,97; InputStatus=CompleteEligible; M=0,53; D=0,50; T=0,50; BaseRisk=0,52; AdjustedScore=0,52; C=1,00; I=1,00; FWI=absent; KBDI=absent; FireIndexProvenance=absent; ParameterSet=Candidate Parameter Set V1.0 (non-calibrated).    | 2026-05-18 23:15:24.204196+00
 2960f096-3b72-45a8-ab37-a422c4eff0b7 | b3f4fb84-bf17-5522-a5f3-70fd1212f381 | e7660606-cadd-4212-e67b-574c09ef789a | 33d52e78-7801-cf3b-bc8b-c798db0d4068 | fad53420-d2ea-41c1-85d9-277e10c30dff | 2020-09-13 10:00:00+00 |              0.515 | High      | Area=b3f4fb84-bf17-5522-a5f3-70fd1212f381; Sensor=e7660606-cadd-4212-e67b-574c09ef789a; Event=fad53420-d2ea-41c1-85d9-277e10c30dff; Metric=WindSpeed; Value=3,59; InputStatus=CompleteEligible; M=0,53; D=0,50; T=0,50; BaseRisk=0,52; AdjustedScore=0,52; C=1,00; I=1,00; FWI=absent; KBDI=absent; FireIndexProvenance=absent; ParameterSet=Candidate Parameter Set V1.0 (non-calibrated).    | 2026-05-18 23:15:23.200389+00
 2f1ea922-6cb6-449b-b4fe-e87c66ea4f8b | b3f4fb84-bf17-5522-a5f3-70fd1212f381 | 8e6a3d8a-7e65-4fa5-8c51-fa4597b203b8 | 8a65d4e7-92f5-1b6f-fdb0-d60ec5707d72 | af0e311b-0cf5-418c-982c-c9db45072de5 | 2020-09-13 10:00:00+00 | 0.5866666666666667 | High      | Area=b3f4fb84-bf17-5522-a5f3-70fd1212f381; Sensor=8e6a3d8a-7e65-4fa5-8c51-fa4597b203b8; Event=af0e311b-0cf5-418c-982c-c9db45072de5; Metric=Temperature; Value=30,97; InputStatus=CompleteEligible; M=0,67; D=0,50; T=0,50; BaseRisk=0,59; AdjustedScore=0,59; C=1,00; I=1,00; FWI=absent; KBDI=absent; FireIndexProvenance=absent; ParameterSet=Candidate Parameter Set V1.0 (non-calibrated). | 2026-05-18 23:15:22.208755+00
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
 ec827232-2c6b-474f-bb30-82a5eb564ca3 | b3f4fb84-bf17-5522-a5f3-70fd1212f381 | c03be3d5-1f70-15cb-2fc0-3c86a204a644 | d8203d4b-1839-4908-87ef-05633c1f1ae5 | 2020-09-13 10:00:20+00 |               0.54 | High               | High     | Aggregated from 6 assessments; 6 at High or above; AreaRisk=0.70*p80(0,54)+0.30*max(0,54). |               6 | 2026-05-18 23:15:47.229962+00
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
                  Id                  |                AreaId                |              GridCellId              |               SensorId               |          LatestAssessmentId          |   SnapshotTimestamp    | RiskScore | RiskLevel | Severity |                                                                                                                                                                                            Summary                                                                                                                                                                                             |           UpdatedAt           
--------------------------------------+--------------------------------------+--------------------------------------+--------------------------------------+--------------------------------------+------------------------+-----------+-----------+----------+------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------+-------------------------------
 54e32b19-78da-4c47-b0f5-a04f40bf3809 | b3f4fb84-bf17-5522-a5f3-70fd1212f381 | 33d52e78-7801-cf3b-bc8b-c798db0d4068 | e7660606-cadd-4212-e67b-574c09ef789a | 25762c3e-0dcd-4d8c-8dae-d05c4d6bdbb7 | 2020-09-13 10:00:20+00 |      0.54 | High      | High     | Area=b3f4fb84-bf17-5522-a5f3-70fd1212f381; Sensor=e7660606-cadd-4212-e67b-574c09ef789a; Event=bbab86b1-45cd-4411-b82a-33566287e3d1; Metric=WindSpeed; Value=5,64; InputStatus=CompleteEligible; M=0,58; D=0,50; T=0,50; BaseRisk=0,54; AdjustedScore=0,54; C=1,00; I=1,00; FWI=absent; KBDI=absent; FireIndexProvenance=absent; ParameterSet=Candidate Parameter Set V1.0 (non-calibrated).    | 2026-05-18 23:15:47.20851+00
 559c1310-0fb5-44b0-ada6-5f1d4f52021f | b3f4fb84-bf17-5522-a5f3-70fd1212f381 | 8a65d4e7-92f5-1b6f-fdb0-d60ec5707d72 | 8e6a3d8a-7e65-4fa5-8c51-fa4597b203b8 | 71255f23-5485-4cc0-8b63-d0d28a5c3b53 | 2020-09-13 10:00:20+00 |      0.54 | High      | High     | Area=b3f4fb84-bf17-5522-a5f3-70fd1212f381; Sensor=8e6a3d8a-7e65-4fa5-8c51-fa4597b203b8; Event=5e03377d-1337-4834-aa78-e045c33e3a6f; Metric=Temperature; Value=32,62; InputStatus=CompleteEligible; M=0,58; D=0,50; T=0,50; BaseRisk=0,54; AdjustedScore=0,54; C=1,00; I=1,00; FWI=absent; KBDI=absent; FireIndexProvenance=absent; ParameterSet=Candidate Parameter Set V1.0 (non-calibrated). | 2026-05-18 23:15:46.215264+00
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
 de10567a-a4a0-4f49-aa91-646c91dcbdbe | b3f4fb84-bf17-5522-a5f3-70fd1212f381 | 33426c68-be78-46ae-8026-a03935b7daad | 2020-09-13 10:00:20+00 |               0.54 | High               | Aggregated from 6 assessments; 6 at High or above; AreaRisk=0.70*p80(0,54)+0.30*max(0,54). |               6 | 2026-05-18 17:55:33.713463+00
 2dcaa330-b7c0-4929-83a2-093d62b72079 | b3f4fb84-bf17-5522-a5f3-70fd1212f381 | 44327172-046b-4320-b2b0-1205b4f7827e | 2020-09-13 10:00:20+00 |              0.554 | High               | Aggregated from 6 assessments; 6 at High or above; AreaRisk=0.70*p80(0,54)+0.30*max(0,59). |               6 | 2026-05-18 22:27:09.292141+00
 d50f8788-b690-46a4-aea3-a652e722b439 | b3f4fb84-bf17-5522-a5f3-70fd1212f381 | d8203d4b-1839-4908-87ef-05633c1f1ae5 | 2020-09-13 10:00:20+00 |               0.54 | High               | Aggregated from 6 assessments; 6 at High or above; AreaRisk=0.70*p80(0,54)+0.30*max(0,54). |               6 | 2026-05-18 23:15:45.220306+00
 bbab86b1-45cd-4411-b82a-33566287e3d1 | b3f4fb84-bf17-5522-a5f3-70fd1212f381 | d8203d4b-1839-4908-87ef-05633c1f1ae5 | 2020-09-13 10:00:20+00 |               0.54 | High               | Aggregated from 6 assessments; 6 at High or above; AreaRisk=0.70*p80(0,54)+0.30*max(0,54). |               6 | 2026-05-18 23:15:47.217084+00
 ae065386-a789-4b38-af88-49c4e8f36f35 | b3f4fb84-bf17-5522-a5f3-70fd1212f381 | 33426c68-be78-46ae-8026-a03935b7daad | 2020-09-13 10:00:20+00 |               0.54 | High               | Aggregated from 6 assessments; 6 at High or above; AreaRisk=0.70*p80(0,54)+0.30*max(0,54). |               6 | 2026-05-18 17:55:30.727472+00
 dfc70f64-afc9-4f2b-aa2c-70f890c4bffb | b3f4fb84-bf17-5522-a5f3-70fd1212f381 | d8203d4b-1839-4908-87ef-05633c1f1ae5 | 2020-09-13 10:00:20+00 |               0.54 | High               | Aggregated from 6 assessments; 6 at High or above; AreaRisk=0.70*p80(0,54)+0.30*max(0,54). |               6 | 2026-05-18 23:15:43.218775+00
 fd0e7c90-5a6b-4592-b97d-ac8b7f48a267 | b3f4fb84-bf17-5522-a5f3-70fd1212f381 | 44327172-046b-4320-b2b0-1205b4f7827e | 2020-09-13 10:00:20+00 |               0.54 | High               | Aggregated from 6 assessments; 6 at High or above; AreaRisk=0.70*p80(0,54)+0.30*max(0,54). |               6 | 2026-05-18 22:27:13.325805+00
 26662362-72fb-48c2-b979-328038304e80 | b3f4fb84-bf17-5522-a5f3-70fd1212f381 | d8203d4b-1839-4908-87ef-05633c1f1ae5 | 2020-09-13 10:00:20+00 |               0.54 | High               | Aggregated from 6 assessments; 6 at High or above; AreaRisk=0.70*p80(0,54)+0.30*max(0,54). |               6 | 2026-05-18 23:15:44.220435+00
 5e03377d-1337-4834-aa78-e045c33e3a6f | b3f4fb84-bf17-5522-a5f3-70fd1212f381 | d8203d4b-1839-4908-87ef-05633c1f1ae5 | 2020-09-13 10:00:20+00 |               0.54 | High               | Aggregated from 6 assessments; 6 at High or above; AreaRisk=0.70*p80(0,54)+0.30*max(0,54). |               6 | 2026-05-18 23:15:46.222979+00
 df917fdb-1a51-488e-a88f-f8bb7a0d7c9f | b3f4fb84-bf17-5522-a5f3-70fd1212f381 | 44327172-046b-4320-b2b0-1205b4f7827e | 2020-09-13 10:00:20+00 |              0.554 | High               | Aggregated from 6 assessments; 6 at High or above; AreaRisk=0.70*p80(0,54)+0.30*max(0,59). |               6 | 2026-05-18 22:27:11.329234+00
 7a4daf87-e872-4946-9df7-81b70b57cbfa | b3f4fb84-bf17-5522-a5f3-70fd1212f381 | 33426c68-be78-46ae-8026-a03935b7daad | 2020-09-13 10:00:20+00 |               0.54 | High               | Aggregated from 6 assessments; 6 at High or above; AreaRisk=0.70*p80(0,54)+0.30*max(0,54). |               6 | 2026-05-18 17:55:32.713383+00
 8470e61e-d540-4d1f-acfa-7a9837bf0723 | b3f4fb84-bf17-5522-a5f3-70fd1212f381 | 33426c68-be78-46ae-8026-a03935b7daad | 2020-09-13 10:00:20+00 |               0.54 | High               | Aggregated from 6 assessments; 6 at High or above; AreaRisk=0.70*p80(0,54)+0.30*max(0,54). |               6 | 2026-05-18 17:55:31.708561+00
 9fbd09d9-dd88-4f38-8eaf-3db8a6996b4d | b3f4fb84-bf17-5522-a5f3-70fd1212f381 | 44327172-046b-4320-b2b0-1205b4f7827e | 2020-09-13 10:00:20+00 |               0.54 | High               | Aggregated from 6 assessments; 6 at High or above; AreaRisk=0.70*p80(0,54)+0.30*max(0,54). |               6 | 2026-05-18 22:27:12.325051+00
 39b816e1-2a0a-4e67-a49f-0c06c6ae154e | b3f4fb84-bf17-5522-a5f3-70fd1212f381 | 33426c68-be78-46ae-8026-a03935b7daad | 2020-09-13 10:00:20+00 |               0.54 | High               | Aggregated from 6 assessments; 6 at High or above; AreaRisk=0.70*p80(0,54)+0.30*max(0,54). |               6 | 2026-05-18 17:55:35.707623+00
 2992fcd1-ebba-47fa-9bb1-c4ebc6f3f94a | b3f4fb84-bf17-5522-a5f3-70fd1212f381 | 33426c68-be78-46ae-8026-a03935b7daad | 2020-09-13 10:00:20+00 |               0.54 | High               | Aggregated from 6 assessments; 6 at High or above; AreaRisk=0.70*p80(0,54)+0.30*max(0,54). |               6 | 2026-05-18 17:55:34.706348+00
 95437990-b986-42d1-a871-6436004628a3 | b3f4fb84-bf17-5522-a5f3-70fd1212f381 | 44327172-046b-4320-b2b0-1205b4f7827e | 2020-09-13 10:00:20+00 |              0.554 | High               | Aggregated from 6 assessments; 6 at High or above; AreaRisk=0.70*p80(0,54)+0.30*max(0,59). |               6 | 2026-05-18 22:27:10.325308+00
 615cd288-c0c4-452a-9a6b-8b8133e4f3a5 | b3f4fb84-bf17-5522-a5f3-70fd1212f381 | d8203d4b-1839-4908-87ef-05633c1f1ae5 | 2020-09-13 10:00:15+00 |              0.515 | High               | Aggregated from 6 assessments; 6 at High or above; AreaRisk=0.70*p80(0,52)+0.30*max(0,52). |               6 | 2026-05-18 23:15:38.212688+00
 83183d83-b47c-4c16-af53-b4aa894f71d3 | b3f4fb84-bf17-5522-a5f3-70fd1212f381 | 44327172-046b-4320-b2b0-1205b4f7827e | 2020-09-13 10:00:15+00 |             0.5365 | High               | Aggregated from 6 assessments; 6 at High or above; AreaRisk=0.70*p80(0,52)+0.30*max(0,59). |               6 | 2026-05-18 22:27:05.329225+00
 e2867bab-e3a4-4645-a6a3-6b2e128fce5c | b3f4fb84-bf17-5522-a5f3-70fd1212f381 | d8203d4b-1839-4908-87ef-05633c1f1ae5 | 2020-09-13 10:00:15+00 |              0.515 | High               | Aggregated from 6 assessments; 6 at High or above; AreaRisk=0.70*p80(0,52)+0.30*max(0,52). |               6 | 2026-05-18 23:15:39.230012+00
 c197c820-53c1-4ae6-9e81-d45489081950 | b3f4fb84-bf17-5522-a5f3-70fd1212f381 | 44327172-046b-4320-b2b0-1205b4f7827e | 2020-09-13 10:00:15+00 |             0.5365 | High               | Aggregated from 6 assessments; 6 at High or above; AreaRisk=0.70*p80(0,52)+0.30*max(0,59). |               6 | 2026-05-18 22:27:06.325527+00
 965e3cee-159a-46ee-a815-fad5ad4e6e6c | b3f4fb84-bf17-5522-a5f3-70fd1212f381 | 44327172-046b-4320-b2b0-1205b4f7827e | 2020-09-13 10:00:15+00 |             0.5365 | High               | Aggregated from 6 assessments; 6 at High or above; AreaRisk=0.70*p80(0,52)+0.30*max(0,59). |               6 | 2026-05-18 22:27:04.326059+00
 5dc90148-d598-45e8-8ef8-de84814d1c5e | b3f4fb84-bf17-5522-a5f3-70fd1212f381 | d8203d4b-1839-4908-87ef-05633c1f1ae5 | 2020-09-13 10:00:15+00 |              0.515 | High               | Aggregated from 6 assessments; 6 at High or above; AreaRisk=0.70*p80(0,52)+0.30*max(0,52). |               6 | 2026-05-18 23:15:37.215098+00
 27edc86e-c18e-4bde-b729-999334fab094 | b3f4fb84-bf17-5522-a5f3-70fd1212f381 | d8203d4b-1839-4908-87ef-05633c1f1ae5 | 2020-09-13 10:00:15+00 |              0.515 | High               | Aggregated from 6 assessments; 6 at High or above; AreaRisk=0.70*p80(0,52)+0.30*max(0,52). |               6 | 2026-05-18 23:15:40.217201+00
 4e120157-8b7c-4444-b8d9-479b6ba59785 | b3f4fb84-bf17-5522-a5f3-70fd1212f381 | 33426c68-be78-46ae-8026-a03935b7daad | 2020-09-13 10:00:15+00 |               0.54 | High               | Aggregated from 6 assessments; 6 at High or above; AreaRisk=0.70*p80(0,54)+0.30*max(0,54). |               6 | 2026-05-18 17:55:29.714397+00
 31acaed7-ddf3-4314-92c0-df67564eb44f | b3f4fb84-bf17-5522-a5f3-70fd1212f381 | 33426c68-be78-46ae-8026-a03935b7daad | 2020-09-13 10:00:15+00 |               0.54 | High               | Aggregated from 6 assessments; 6 at High or above; AreaRisk=0.70*p80(0,54)+0.30*max(0,54). |               6 | 2026-05-18 17:55:28.703937+00
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
 bb10b4ce-8f7b-4eee-b6b7-e43354989f51 | b3f4fb84-bf17-5522-a5f3-70fd1212f381 | c03be3d5-1f70-15cb-2fc0-3c86a204a644 | ec827232-2c6b-474f-bb30-82a5eb564ca3 | area-risk-high | High     | Open   | AlertState=Warning; Area risk is High with adjusted score 0,54. Candidate Parameter Set V1.0 (non-official). | 2020-09-13 10:00:00+00 | 2026-05-18 23:15:47.229962+00 | âˆ…
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
 area-risk-high | High     | Open   |     1 | 2020-09-13 10:00:00+00 | 2026-05-18 23:15:48.229572+00
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
 b3f4fb84-bf17-5522-a5f3-70fd1212f381 |               0.54 | High               | High          | 2020-09-13 10:00:20+00 | 2026-05-18 23:15:48.229572+00 | area-risk-high | High           | Open         | AlertState=Warning; Area risk is High with adjusted score 0,54. Candidate Parameter Set V1.0 (non-official). | 2020-09-13 10:00:00+00 | âˆ…
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
    "updatedAt":  "2026-05-18T23:15:48.229572+00:00",
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
                      "updatedAt":  "2026-05-18T23:15:48.229572+00:00",
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
