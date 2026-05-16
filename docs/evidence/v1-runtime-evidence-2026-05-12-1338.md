# NatureProtector V1 Runtime Evidence

- GeneratedAt: 2026-05-12T13:38:02.7627522+02:00
- PostgresContainer: np-postgres
- Database: natureprotector
- ApiBaseUrl: http://localhost:5254

## Git branch

```text
docs/v1-implementation-plan

```

## Git commit

```text
6fa7853f04489ec437d46c600001fd03d9322989

```

## Git status

```text
## docs/v1-implementation-plan
 M src/NatureProtector.Backoffice.Api/ControlPlane/Contracts/ControlPlaneResponses.cs
 M src/NatureProtector.Backoffice.Api/ControlPlane/Services/PostgresControlPlaneService.cs
 M src/NatureProtector.Core/Risk/RiskAssessment.cs
 M src/NatureProtector.Prevention.Host/Processing/ReadingRiskPipeline.cs
 M src/NatureProtector.Prevention.Host/Projection/InMemoryAreaOperationalProjectionStore.cs
 M src/NatureProtector.Prevention.Host/Projection/PostgresAreaOperationalProjectionStore.cs
 M src/NatureProtector.Prevention/Readings/NormalizedReading.cs
 M src/NatureProtector.Prevention/Risk/RiskEligibilityReason.cs
 M src/NatureProtector.Prevention/Risk/RiskEligibilityResult.cs
 M src/NatureProtector.Prevention/Risk/RiskEligibilityService.cs
 M src/NatureProtector.Prevention/Risk/RiskInput.cs
 M src/NatureProtector.Prevention/Risk/SimpleRiskScoringService.cs
 M tests/NatureProtector.Backoffice.Api.Tests/ControlPlaneApiTests.cs
 M tests/NatureProtector.Backoffice.Api.Tests/ControlPlaneApiWebApplicationFactory.cs
 M tests/NatureProtector.Backoffice.Api.Tests/PostgresControlPlaneServiceTests.cs
 M tests/NatureProtector.Core.Tests/Risk/RiskAssessmentTests.cs
 M tests/NatureProtector.Prevention.Host.Tests/Processing/ReadingRiskPipelineTests.cs
 M tests/NatureProtector.Prevention.Host.Tests/Projection/PostgresAreaOperationalProjectionStoreTests.cs
 M tests/NatureProtector.Prevention.Tests/Readings/NormalizedReadingTests.cs
 M tests/NatureProtector.Prevention.Tests/Risk/RiskEligibilityServiceTests.cs
 M tests/NatureProtector.Prevention.Tests/Risk/RiskInputTests.cs
 M tests/NatureProtector.Prevention.Tests/Risk/SimpleRiskScoringServiceTests.cs
?? docs/contracts/
?? docs/evidence/
?? docs/implementation/
?? scripts/evidence/
?? src/NatureProtector.Prevention.Host/Projection/V1AlertPolicy.cs
?? src/NatureProtector.Prevention/Readings/OperationalEvent.cs
?? src/NatureProtector.Prevention/Risk/ClassifierResult.cs
?? src/NatureProtector.Prevention/Risk/ClassifierSeverity.cs
?? src/NatureProtector.Prevention/Risk/ClassifierStatus.cs
?? src/NatureProtector.Prevention/Risk/DailyCellState.cs
?? src/NatureProtector.Prevention/Risk/RiskInputStatus.cs
?? tests/NatureProtector.Prevention.Host.Tests/Projection/InMemoryAreaOperationalProjectionStoreAlertPolicyTests.cs
?? tests/NatureProtector.Prevention.Tests/Readings/OperationalEventTests.cs
?? tests/NatureProtector.Prevention.Tests/Risk/ClassifierResultTests.cs
?? tests/NatureProtector.Prevention.Tests/Risk/DailyCellStateTests.cs

```

## Docker containers

```text
NAMES         IMAGE                                      STATUS          PORTS
np-influxdb   influxdb:3.7.0-core                        Up 45 minutes   0.0.0.0:8181->8181/tcp
np-rabbitmq   rabbitmq:4.0.6-management                  Up 45 minutes   4369/tcp, 5671/tcp, 0.0.0.0:5672->5672/tcp, 15671/tcp, 15691-15692/tcp, 25672/tcp, 0.0.0.0:15672->15672/tcp
np-postgres   postgres:16                                Up 45 minutes   0.0.0.0:5433->5432/tcp
np-grafana    grafana/grafana-enterprise:13.0.1-ubuntu   Up 45 minutes   0.0.0.0:3000->3000/tcp

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
                      1 |     1 |        467 |           75 |               3 |                    3 |              23
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
                  Id                  | VersionNumber | IsActive |                                 Description                                  |          CreatedAt           |     CreatedBy      
--------------------------------------+---------------+----------+------------------------------------------------------------------------------+------------------------------+--------------------
 c03be3d5-1f70-15cb-2fc0-3c86a204a644 |             1 | t        | Bootstrap control-plane import for Proenca-a-Nova with pilot sensor network. | 2026-05-12 10:25:36.50686+00 | phase-04-bootstrap
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
                  Id                  |                AreaId                |              ScenarioId              |        ConfigurationVersionId        | ScenarioCode |             ScenarioName              |           CreatedAt           |           StartedAt           |            EndedAt            | LogicalStartTimestamp  | IntervalSeconds | NumberOfCycles | ExecutionSeed | Status |                    MetadataJson                    
--------------------------------------+--------------------------------------+--------------------------------------+--------------------------------------+--------------+---------------------------------------+-------------------------------+-------------------------------+-------------------------------+------------------------+-----------------+----------------+---------------+--------+----------------------------------------------------
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
 f28f8c86-2426-4f19-9b74-87d344c8ebc7 | b3f4fb84-bf17-5522-a5f3-70fd1212f381 | 6034251c-b502-5f7b-a557-7187178cfb04 | c03be3d5-1f70-15cb-2fc0-3c86a204a644 | scenario_b   | Scenario B - High Risk Proenca-a-Nova | 2026-04-17 13:57:37.013678+00 | 2026-04-17 13:57:37.141557+00 | 2026-04-17 13:57:38.779815+00 | 2020-09-13 10:00:00+00 |              30 |             20 |         12345 |      5 | {"sensor_count":72,"scenario_category":"HighRisk"}
 fef1f63f-40a0-46e3-bd0a-0d94c3296dc5 | b3f4fb84-bf17-5522-a5f3-70fd1212f381 | 6034251c-b502-5f7b-a557-7187178cfb04 | c03be3d5-1f70-15cb-2fc0-3c86a204a644 | scenario_b   | Scenario B - High Risk Proenca-a-Nova | 2026-04-17 12:06:50.27294+00  | 2026-04-17 12:06:50.389607+00 | 2026-04-17 12:30:47.714518+00 | 2020-09-13 10:00:00+00 |               5 |            288 |         12345 |      3 | {"sensor_count":72,"scenario_category":"HighRisk"}
 e73b99cd-f2e2-422a-ae4f-514241b3e045 | b3f4fb84-bf17-5522-a5f3-70fd1212f381 | 6034251c-b502-5f7b-a557-7187178cfb04 | c03be3d5-1f70-15cb-2fc0-3c86a204a644 | scenario_b   | Scenario B - High Risk Proenca-a-Nova | 2026-04-17 09:56:29.567806+00 | 2026-04-17 09:56:29.678355+00 | 2026-04-17 10:20:27.202166+00 | 2020-09-13 10:00:00+00 |               5 |            288 |         12345 |      3 | {"sensor_count":72,"scenario_category":"HighRisk"}
 584b2497-293c-4710-bef2-a0d492a1cdef | b3f4fb84-bf17-5522-a5f3-70fd1212f381 | 6034251c-b502-5f7b-a557-7187178cfb04 | c03be3d5-1f70-15cb-2fc0-3c86a204a644 | scenario_b   | Scenario B - High Risk Proenca-a-Nova | 2026-04-16 21:14:43.739185+00 | 2026-04-16 21:14:43.853718+00 | 2026-04-16 21:38:41.518989+00 | 2020-09-13 10:00:00+00 |               5 |            288 |         12345 |      3 | {"sensor_count":72,"scenario_category":"HighRisk"}
 b74c6398-0225-47e9-814a-3d5bc8834f47 | b3f4fb84-bf17-5522-a5f3-70fd1212f381 | 6034251c-b502-5f7b-a557-7187178cfb04 | c03be3d5-1f70-15cb-2fc0-3c86a204a644 | scenario_b   | Scenario B - High Risk Proenca-a-Nova | 2026-04-12 10:28:12.836484+00 | 2026-04-12 10:28:12.949652+00 | 2026-04-12 10:52:10.561298+00 | 2020-09-13 10:00:00+00 |               5 |            288 |         12345 |      3 | {"sensor_count":72,"scenario_category":"HighRisk"}
 57331d67-6a6e-42ef-8ccc-d8bdff3e1b98 | b3f4fb84-bf17-5522-a5f3-70fd1212f381 | 6034251c-b502-5f7b-a557-7187178cfb04 | c03be3d5-1f70-15cb-2fc0-3c86a204a644 | scenario_b   | Scenario B - High Risk Proenca-a-Nova | 2026-04-11 16:39:50.654842+00 | 2026-04-11 16:39:50.768528+00 | 2026-04-11 17:03:48.22324+00  | 2020-09-13 10:00:00+00 |               5 |            288 |         12345 |      3 | {"sensor_count":72,"scenario_category":"HighRisk"}
 58ad336b-ac4a-4d31-a7a1-dc7842072fd9 | b3f4fb84-bf17-5522-a5f3-70fd1212f381 | 6034251c-b502-5f7b-a557-7187178cfb04 | c03be3d5-1f70-15cb-2fc0-3c86a204a644 | scenario_b   | Scenario B - High Risk Proenca-a-Nova | 2026-04-11 16:18:31.485807+00 | 2026-04-11 16:18:31.599283+00 | 2026-04-11 16:39:45.055306+00 | 2020-09-13 10:00:00+00 |               5 |            288 |         12345 |      5 | {"sensor_count":72,"scenario_category":"HighRisk"}
 209d1ffc-1101-4a47-b6d3-0440e237e6a7 | b3f4fb84-bf17-5522-a5f3-70fd1212f381 | 6034251c-b502-5f7b-a557-7187178cfb04 | c03be3d5-1f70-15cb-2fc0-3c86a204a644 | scenario_b   | Scenario B - High Risk Proenca-a-Nova | 2026-04-11 13:41:15.951518+00 | 2026-04-11 13:41:16.062632+00 | 2026-04-11 13:45:33.772909+00 | 2020-09-13 10:00:00+00 |               5 |            288 |         12345 |      5 | {"sensor_count":72,"scenario_category":"HighRisk"}
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
       86514 |          86779 |            131 |               100
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
      2 |  6110
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
 2026-04-11 11:38:57.569963+00 | 2026-05-12 11:12:04.923186+00 | 2020-09-13 10:00:00+00 | 2020-09-13 10:23:55+00
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
 5bb132cb-6d0e-4310-8e3d-44d1af4569ca | 1477c43e-314b-4f27-bdc0-45659fd0fa80 | SensorReadingProduced | NatureProtector.Simulator.Host |      2 |            1 | 2020-09-13 10:09:30+00 | 2026-05-12 11:12:04.923186+00 | 2026-05-12 11:12:04.978769+00 | 2026-05-12 11:12:04.978769+00 | ├ó╦åÔÇª           | ├ó╦åÔÇª              | ├ó╦åÔÇª
 054327fa-37d9-4840-84eb-38d89e5855cf | 60c1756b-4d53-49e9-a9de-6866a60fe34b | SensorReadingProduced | NatureProtector.Simulator.Host |      2 |            1 | 2020-09-13 10:09:30+00 | 2026-05-12 11:12:04.870537+00 | 2026-05-12 11:12:04.916432+00 | 2026-05-12 11:12:04.916432+00 | ├ó╦åÔÇª           | ├ó╦åÔÇª              | ├ó╦åÔÇª
 84abc113-953a-4d4c-9cdf-d9241d617e4b | dd022ec4-4fd0-4a09-a293-e806a535627d | SensorReadingProduced | NatureProtector.Simulator.Host |      2 |            1 | 2020-09-13 10:09:30+00 | 2026-05-12 11:12:04.815758+00 | 2026-05-12 11:12:04.862559+00 | 2026-05-12 11:12:04.862559+00 | ├ó╦åÔÇª           | ├ó╦åÔÇª              | ├ó╦åÔÇª
 a7b0e7b5-d161-4fb2-91e5-1dd9f78da276 | 02b394ff-440d-4674-b210-48fc0634dc02 | SensorReadingProduced | NatureProtector.Simulator.Host |      2 |            1 | 2020-09-13 10:09:30+00 | 2026-05-12 11:12:04.765522+00 | 2026-05-12 11:12:04.809406+00 | 2026-05-12 11:12:04.809406+00 | ├ó╦åÔÇª           | ├ó╦åÔÇª              | ├ó╦åÔÇª
 e3fda4d8-df24-4237-97a4-6580e46b0ba2 | 6673e384-f20c-43ea-ac8d-257bdd347017 | SensorReadingProduced | NatureProtector.Simulator.Host |      2 |            1 | 2020-09-13 10:09:30+00 | 2026-05-12 11:12:04.704713+00 | 2026-05-12 11:12:04.75959+00  | 2026-05-12 11:12:04.75959+00  | ├ó╦åÔÇª           | ├ó╦åÔÇª              | ├ó╦åÔÇª
 7e46597f-8c02-48a8-9294-2f9d2620dcc9 | 6e2fcc92-a101-463b-8e28-84621f604a33 | SensorReadingProduced | NatureProtector.Simulator.Host |      2 |            1 | 2020-09-13 10:09:30+00 | 2026-05-12 11:12:04.652724+00 | 2026-05-12 11:12:04.699007+00 | 2026-05-12 11:12:04.699007+00 | ├ó╦åÔÇª           | ├ó╦åÔÇª              | ├ó╦åÔÇª
 6e70e108-7687-4a85-bb69-8a6808dbfa29 | a8647ed4-9b2d-4a77-a657-8f3d5206646e | SensorReadingProduced | NatureProtector.Simulator.Host |      2 |            1 | 2020-09-13 10:09:00+00 | 2026-05-12 11:11:34.931006+00 | 2026-05-12 11:11:34.981761+00 | 2026-05-12 11:11:34.981761+00 | ├ó╦åÔÇª           | ├ó╦åÔÇª              | ├ó╦åÔÇª
 26025ea0-9cd0-4865-a0a6-0b00f60aecc3 | d71223d1-7215-44aa-aaf3-17af6446f715 | SensorReadingProduced | NatureProtector.Simulator.Host |      2 |            1 | 2020-09-13 10:09:00+00 | 2026-05-12 11:11:34.867426+00 | 2026-05-12 11:11:34.923293+00 | 2026-05-12 11:11:34.923293+00 | ├ó╦åÔÇª           | ├ó╦åÔÇª              | ├ó╦åÔÇª
 d07a221f-5d37-4222-91d4-3b05ce3816a6 | 7f27d6b3-bc99-4127-b9f8-59954e111cec | SensorReadingProduced | NatureProtector.Simulator.Host |      2 |            1 | 2020-09-13 10:09:00+00 | 2026-05-12 11:11:34.795404+00 | 2026-05-12 11:11:34.859309+00 | 2026-05-12 11:11:34.859309+00 | ├ó╦åÔÇª           | ├ó╦åÔÇª              | ├ó╦åÔÇª
 80035bc7-8f58-455a-baab-0fbf445bdcc0 | 7f943a4f-6211-40db-94dd-5baf73a959e1 | SensorReadingProduced | NatureProtector.Simulator.Host |      2 |            1 | 2020-09-13 10:09:00+00 | 2026-05-12 11:11:34.740167+00 | 2026-05-12 11:11:34.788575+00 | 2026-05-12 11:11:34.788575+00 | ├ó╦åÔÇª           | ├ó╦åÔÇª              | ├ó╦åÔÇª
 ec84b3a7-9be2-494f-8c81-f6a1662dd562 | c85f71e5-2001-4178-83c0-e32ffb5f0380 | SensorReadingProduced | NatureProtector.Simulator.Host |      2 |            1 | 2020-09-13 10:09:00+00 | 2026-05-12 11:11:34.692904+00 | 2026-05-12 11:11:34.733167+00 | 2026-05-12 11:11:34.733167+00 | ├ó╦åÔÇª           | ├ó╦åÔÇª              | ├ó╦åÔÇª
 b2a0c4b2-d342-49f0-abe6-dfd503f45161 | 95fabfdf-6c73-4685-a8d1-3f214edd8470 | SensorReadingProduced | NatureProtector.Simulator.Host |      2 |            1 | 2020-09-13 10:09:00+00 | 2026-05-12 11:11:34.640043+00 | 2026-05-12 11:11:34.686789+00 | 2026-05-12 11:11:34.686789+00 | ├ó╦åÔÇª           | ├ó╦åÔÇª              | ├ó╦åÔÇª
 f80cf3d6-5fba-4527-994d-68cce96db4a3 | ba311f72-cfa2-4dac-a909-f255f49a90d0 | SensorReadingProduced | NatureProtector.Simulator.Host |      2 |            1 | 2020-09-13 10:08:30+00 | 2026-05-12 11:11:04.83617+00  | 2026-05-12 11:11:04.886739+00 | 2026-05-12 11:11:04.886739+00 | ├ó╦åÔÇª           | ├ó╦åÔÇª              | ├ó╦åÔÇª
 eec01397-f633-41b4-a294-53bbe2f6ab98 | 79095d28-b3e0-43ff-a893-4ce4a1a23c2c | SensorReadingProduced | NatureProtector.Simulator.Host |      2 |            1 | 2020-09-13 10:08:30+00 | 2026-05-12 11:11:04.785675+00 | 2026-05-12 11:11:04.82998+00  | 2026-05-12 11:11:04.82998+00  | ├ó╦åÔÇª           | ├ó╦åÔÇª              | ├ó╦åÔÇª
 b7a7afa9-8b24-41b9-837c-33ed804c9e98 | 8e983901-1c8f-4f40-b227-a5a803a9706a | SensorReadingProduced | NatureProtector.Simulator.Host |      2 |            1 | 2020-09-13 10:08:30+00 | 2026-05-12 11:11:04.73372+00  | 2026-05-12 11:11:04.779106+00 | 2026-05-12 11:11:04.779106+00 | ├ó╦åÔÇª           | ├ó╦åÔÇª              | ├ó╦åÔÇª
 f9c824f4-15c4-4f27-8559-901263b8e607 | a8316e07-71e4-4605-98b5-85f9095c7e08 | SensorReadingProduced | NatureProtector.Simulator.Host |      2 |            1 | 2020-09-13 10:08:30+00 | 2026-05-12 11:11:04.681553+00 | 2026-05-12 11:11:04.727275+00 | 2026-05-12 11:11:04.727275+00 | ├ó╦åÔÇª           | ├ó╦åÔÇª              | ├ó╦åÔÇª
 48cfde1a-27f3-4948-8782-090f4cf3f7d5 | d0fdcafc-5838-4543-b139-352a4f478830 | SensorReadingProduced | NatureProtector.Simulator.Host |      2 |            1 | 2020-09-13 10:08:30+00 | 2026-05-12 11:11:04.62429+00  | 2026-05-12 11:11:04.671967+00 | 2026-05-12 11:11:04.671967+00 | ├ó╦åÔÇª           | ├ó╦åÔÇª              | ├ó╦åÔÇª
 a1e92362-90c9-4838-a55c-8232fa35dba1 | 7547dfcb-d49d-4ac0-a486-d73ef4cbaddc | SensorReadingProduced | NatureProtector.Simulator.Host |      2 |            1 | 2020-09-13 10:08:00+00 | 2026-05-12 11:10:34.91964+00  | 2026-05-12 11:10:34.97091+00  | 2026-05-12 11:10:34.97091+00  | ├ó╦åÔÇª           | ├ó╦åÔÇª              | ├ó╦åÔÇª
 b41f38ce-9946-445e-bc48-8a610fe23aae | 445b70da-746e-4f04-b83c-bb6aca3a9c43 | SensorReadingProduced | NatureProtector.Simulator.Host |      2 |            1 | 2020-09-13 10:08:00+00 | 2026-05-12 11:10:34.864406+00 | 2026-05-12 11:10:34.912283+00 | 2026-05-12 11:10:34.912283+00 | ├ó╦åÔÇª           | ├ó╦åÔÇª              | ├ó╦åÔÇª
 73670387-08fd-4032-9d12-00590ec01f84 | 4aee7c74-6e97-4771-829b-3f8e7ac4fd0c | SensorReadingProduced | NatureProtector.Simulator.Host |      2 |            1 | 2020-09-13 10:08:00+00 | 2026-05-12 11:10:34.811256+00 | 2026-05-12 11:10:34.858189+00 | 2026-05-12 11:10:34.858189+00 | ├ó╦åÔÇª           | ├ó╦åÔÇª              | ├ó╦åÔÇª
 ea74cb7a-dcde-484f-a274-dc82d4739110 | 6ae7eddb-696d-499e-9ce1-034d9ec7b7eb | SensorReadingProduced | NatureProtector.Simulator.Host |      2 |            1 | 2020-09-13 10:08:00+00 | 2026-05-12 11:10:34.756745+00 | 2026-05-12 11:10:34.804634+00 | 2026-05-12 11:10:34.804634+00 | ├ó╦åÔÇª           | ├ó╦åÔÇª              | ├ó╦åÔÇª
 122a5f55-5b5c-4bfa-9b34-396f8bff8d58 | 4eb2de39-6e23-4939-a947-619a3ee5fd15 | SensorReadingProduced | NatureProtector.Simulator.Host |      2 |            1 | 2020-09-13 10:08:00+00 | 2026-05-12 11:10:34.700728+00 | 2026-05-12 11:10:34.750533+00 | 2026-05-12 11:10:34.750533+00 | ├ó╦åÔÇª           | ├ó╦åÔÇª              | ├ó╦åÔÇª
 b03608de-fa14-41fc-b85f-1ca586e98fa8 | cf61c606-5b5d-43f1-b641-bf9ec07d2cda | SensorReadingProduced | NatureProtector.Simulator.Host |      2 |            1 | 2020-09-13 10:08:00+00 | 2026-05-12 11:10:34.607798+00 | 2026-05-12 11:10:34.691233+00 | 2026-05-12 11:10:34.691233+00 | ├ó╦åÔÇª           | ├ó╦åÔÇª              | ├ó╦åÔÇª
 a9a0f190-b822-4f5b-8e7e-c7f3fbdb042a | 431eb840-6cb1-4627-8db9-49083a68df0e | SensorReadingProduced | NatureProtector.Simulator.Host |      2 |            1 | 2020-09-13 10:07:30+00 | 2026-05-12 11:10:04.922376+00 | 2026-05-12 11:10:04.97471+00  | 2026-05-12 11:10:04.97471+00  | ├ó╦åÔÇª           | ├ó╦åÔÇª              | ├ó╦åÔÇª
 5a558199-333c-4731-b828-abd2d6e86fe2 | 8a8ff1ed-fb40-490a-b2a7-818efcebf868 | SensorReadingProduced | NatureProtector.Simulator.Host |      2 |            1 | 2020-09-13 10:07:30+00 | 2026-05-12 11:10:04.86187+00  | 2026-05-12 11:10:04.914269+00 | 2026-05-12 11:10:04.914269+00 | ├ó╦åÔÇª           | ├ó╦åÔÇª              | ├ó╦åÔÇª
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
 2d631a70-b217-4dcc-a711-0b401dd62512 | 5bb132cb-6d0e-4310-8e3d-44d1af4569ca |             1 | reading_risk_pipeline | 2026-05-12 11:12:04.923186+00 | 2026-05-12 11:12:04.978769+00 |       1 | ├ó╦åÔÇª       | ├ó╦åÔÇª
 8b3d2bee-8f27-43af-b658-d6e1bcae7769 | 054327fa-37d9-4840-84eb-38d89e5855cf |             1 | reading_risk_pipeline | 2026-05-12 11:12:04.870537+00 | 2026-05-12 11:12:04.916432+00 |       1 | ├ó╦åÔÇª       | ├ó╦åÔÇª
 58bf562d-f635-4a53-9819-6141f76e197c | 84abc113-953a-4d4c-9cdf-d9241d617e4b |             1 | reading_risk_pipeline | 2026-05-12 11:12:04.815758+00 | 2026-05-12 11:12:04.862559+00 |       1 | ├ó╦åÔÇª       | ├ó╦åÔÇª
 ca5ad818-997f-4c47-8909-0eff7dda9619 | a7b0e7b5-d161-4fb2-91e5-1dd9f78da276 |             1 | reading_risk_pipeline | 2026-05-12 11:12:04.765522+00 | 2026-05-12 11:12:04.809406+00 |       1 | ├ó╦åÔÇª       | ├ó╦åÔÇª
 f18d256c-b16a-4f66-b751-cb914a9e2cd8 | e3fda4d8-df24-4237-97a4-6580e46b0ba2 |             1 | reading_risk_pipeline | 2026-05-12 11:12:04.704713+00 | 2026-05-12 11:12:04.75959+00  |       1 | ├ó╦åÔÇª       | ├ó╦åÔÇª
 d800b76d-053a-4822-b027-6d38a8fcac5e | 7e46597f-8c02-48a8-9294-2f9d2620dcc9 |             1 | reading_risk_pipeline | 2026-05-12 11:12:04.652724+00 | 2026-05-12 11:12:04.699007+00 |       1 | ├ó╦åÔÇª       | ├ó╦åÔÇª
 32fe5268-0f07-45b5-a1f3-f5bb7a8a0558 | 6e70e108-7687-4a85-bb69-8a6808dbfa29 |             1 | reading_risk_pipeline | 2026-05-12 11:11:34.931006+00 | 2026-05-12 11:11:34.981761+00 |       1 | ├ó╦åÔÇª       | ├ó╦åÔÇª
 a44c2a08-77f5-4396-a197-edda6275f7e4 | 26025ea0-9cd0-4865-a0a6-0b00f60aecc3 |             1 | reading_risk_pipeline | 2026-05-12 11:11:34.867426+00 | 2026-05-12 11:11:34.923293+00 |       1 | ├ó╦åÔÇª       | ├ó╦åÔÇª
 17ca71d1-8b4f-4f06-a516-711fa3af87e5 | d07a221f-5d37-4222-91d4-3b05ce3816a6 |             1 | reading_risk_pipeline | 2026-05-12 11:11:34.795404+00 | 2026-05-12 11:11:34.859309+00 |       1 | ├ó╦åÔÇª       | ├ó╦åÔÇª
 f284f278-8a80-4e89-97eb-29e1c62f96c4 | 80035bc7-8f58-455a-baab-0fbf445bdcc0 |             1 | reading_risk_pipeline | 2026-05-12 11:11:34.740167+00 | 2026-05-12 11:11:34.788575+00 |       1 | ├ó╦åÔÇª       | ├ó╦åÔÇª
 0024f22d-2b8f-4dd7-878e-cfbca367365f | ec84b3a7-9be2-494f-8c81-f6a1662dd562 |             1 | reading_risk_pipeline | 2026-05-12 11:11:34.692904+00 | 2026-05-12 11:11:34.733167+00 |       1 | ├ó╦åÔÇª       | ├ó╦åÔÇª
 a392427d-b79b-4655-881e-02dca5eb64f4 | b2a0c4b2-d342-49f0-abe6-dfd503f45161 |             1 | reading_risk_pipeline | 2026-05-12 11:11:34.640043+00 | 2026-05-12 11:11:34.686789+00 |       1 | ├ó╦åÔÇª       | ├ó╦åÔÇª
 0e2fa98d-bdf0-44f3-b409-b388f49c41f0 | f80cf3d6-5fba-4527-994d-68cce96db4a3 |             1 | reading_risk_pipeline | 2026-05-12 11:11:04.83617+00  | 2026-05-12 11:11:04.886739+00 |       1 | ├ó╦åÔÇª       | ├ó╦åÔÇª
 bb66b7cb-811d-4824-bf94-1c71a9977b5c | eec01397-f633-41b4-a294-53bbe2f6ab98 |             1 | reading_risk_pipeline | 2026-05-12 11:11:04.785675+00 | 2026-05-12 11:11:04.82998+00  |       1 | ├ó╦åÔÇª       | ├ó╦åÔÇª
 a9d84e01-0884-486d-be3c-8f6aa473fd1f | b7a7afa9-8b24-41b9-837c-33ed804c9e98 |             1 | reading_risk_pipeline | 2026-05-12 11:11:04.73372+00  | 2026-05-12 11:11:04.779106+00 |       1 | ├ó╦åÔÇª       | ├ó╦åÔÇª
 1c5bc46e-ab58-43d5-b5c6-a263a725f7c2 | f9c824f4-15c4-4f27-8559-901263b8e607 |             1 | reading_risk_pipeline | 2026-05-12 11:11:04.681553+00 | 2026-05-12 11:11:04.727275+00 |       1 | ├ó╦åÔÇª       | ├ó╦åÔÇª
 2be18f2b-4dfc-4e5d-ae53-a43f76a6e22c | 48cfde1a-27f3-4948-8782-090f4cf3f7d5 |             1 | reading_risk_pipeline | 2026-05-12 11:11:04.62429+00  | 2026-05-12 11:11:04.671967+00 |       1 | ├ó╦åÔÇª       | ├ó╦åÔÇª
 95859e2e-aa59-4d01-a802-3994c295a76a | a1e92362-90c9-4838-a55c-8232fa35dba1 |             1 | reading_risk_pipeline | 2026-05-12 11:10:34.91964+00  | 2026-05-12 11:10:34.97091+00  |       1 | ├ó╦åÔÇª       | ├ó╦åÔÇª
 52dd3178-3a13-4396-8e81-e92c20e0ad2c | b41f38ce-9946-445e-bc48-8a610fe23aae |             1 | reading_risk_pipeline | 2026-05-12 11:10:34.864406+00 | 2026-05-12 11:10:34.912283+00 |       1 | ├ó╦åÔÇª       | ├ó╦åÔÇª
 04b23d4c-c7da-475c-84d2-b612b0b9fa51 | 73670387-08fd-4032-9d12-00590ec01f84 |             1 | reading_risk_pipeline | 2026-05-12 11:10:34.811256+00 | 2026-05-12 11:10:34.858189+00 |       1 | ├ó╦åÔÇª       | ├ó╦åÔÇª
 bbe659aa-b0d6-4b1f-afe6-18bcff75b300 | ea74cb7a-dcde-484f-a274-dc82d4739110 |             1 | reading_risk_pipeline | 2026-05-12 11:10:34.756745+00 | 2026-05-12 11:10:34.804634+00 |       1 | ├ó╦åÔÇª       | ├ó╦åÔÇª
 303fc209-fb75-47d8-9de7-6c9ad97b53a1 | 122a5f55-5b5c-4bfa-9b34-396f8bff8d58 |             1 | reading_risk_pipeline | 2026-05-12 11:10:34.700728+00 | 2026-05-12 11:10:34.750533+00 |       1 | ├ó╦åÔÇª       | ├ó╦åÔÇª
 d3ddd4e5-f48e-47c5-9fdb-909bb89777a6 | b03608de-fa14-41fc-b85f-1ca586e98fa8 |             1 | reading_risk_pipeline | 2026-05-12 11:10:34.607798+00 | 2026-05-12 11:10:34.691233+00 |       1 | ├ó╦åÔÇª       | ├ó╦åÔÇª
 69904db4-21e9-4ed9-a036-5abe7ed0165b | a9a0f190-b822-4f5b-8e7e-c7f3fbdb042a |             1 | reading_risk_pipeline | 2026-05-12 11:10:04.922376+00 | 2026-05-12 11:10:04.97471+00  |       1 | ├ó╦åÔÇª       | ├ó╦åÔÇª
 4cd944e3-f0ac-4112-8c94-c9881e46a7b7 | 5a558199-333c-4731-b828-abd2d6e86fe2 |             1 | reading_risk_pipeline | 2026-05-12 11:10:04.86187+00  | 2026-05-12 11:10:04.914269+00 |       1 | ├ó╦åÔÇª       | ├ó╦åÔÇª
 d8d7c50c-c668-400f-8212-1aeca6eab8e3 | 4a6740c5-9241-4b1f-bb41-6f5029a0f6e9 |             1 | reading_risk_pipeline | 2026-05-12 11:10:04.796995+00 | 2026-05-12 11:10:04.852461+00 |       1 | ├ó╦åÔÇª       | ├ó╦åÔÇª
 b2b32159-2889-46a8-bdc6-609900310652 | d8e9592a-68a8-4781-b3cf-7649177c94cd |             1 | reading_risk_pipeline | 2026-05-12 11:10:04.73073+00  | 2026-05-12 11:10:04.788248+00 |       1 | ├ó╦åÔÇª       | ├ó╦åÔÇª
 29e36208-ebaf-437a-94a6-a153bcd67c52 | 55834161-9107-4196-a0fa-1d7a3b22c123 |             1 | reading_risk_pipeline | 2026-05-12 11:10:04.664727+00 | 2026-05-12 11:10:04.723972+00 |       1 | ├ó╦åÔÇª       | ├ó╦åÔÇª
 6fc280f1-d289-4bbb-9433-f0eb59f3aee9 | 562b5664-5621-4f26-84aa-b501b77d77a6 |             1 | reading_risk_pipeline | 2026-05-12 11:10:04.600753+00 | 2026-05-12 11:10:04.657644+00 |       1 | ├ó╦åÔÇª       | ├ó╦åÔÇª
 6be162e4-ae65-448d-89b8-88f79c914564 | d3213bb5-2f31-4381-b3e1-1f475d53dc67 |             1 | reading_risk_pipeline | 2026-05-12 11:09:34.866775+00 | 2026-05-12 11:09:34.918126+00 |       1 | ├ó╦åÔÇª       | ├ó╦åÔÇª
 0c7dc036-cf99-4423-98d5-f1886adc4f09 | 0774beac-23e2-4ad9-8f65-bdb7d0b3df92 |             1 | reading_risk_pipeline | 2026-05-12 11:09:34.796313+00 | 2026-05-12 11:09:34.857688+00 |       1 | ├ó╦åÔÇª       | ├ó╦åÔÇª
 1f2c53cb-5ffe-4ada-a074-c27f2703afad | 35503c8b-8a42-4b4a-bb5c-67128588c2b0 |             1 | reading_risk_pipeline | 2026-05-12 11:09:34.725715+00 | 2026-05-12 11:09:34.78621+00  |       1 | ├ó╦åÔÇª       | ├ó╦åÔÇª
 81bbbf81-04bd-4f9d-bb9c-2a7978d8dabe | cea62889-86eb-4a84-a179-ddd5595b02c0 |             1 | reading_risk_pipeline | 2026-05-12 11:09:34.656813+00 | 2026-05-12 11:09:34.717955+00 |       1 | ├ó╦åÔÇª       | ├ó╦åÔÇª
 9001e44f-6737-4f2e-8c01-732fca4b077d | 254660ae-832c-43bd-aca0-826caf57eb11 |             1 | reading_risk_pipeline | 2026-05-12 11:09:34.596064+00 | 2026-05-12 11:09:34.647297+00 |       1 | ├ó╦åÔÇª       | ├ó╦åÔÇª
 45d145fe-012d-439a-a998-bf02bff17ed6 | 659a725e-5ea2-41d9-b505-acd08cf666a4 |             1 | reading_risk_pipeline | 2026-05-12 11:09:04.84655+00  | 2026-05-12 11:09:04.887898+00 |       1 | ├ó╦åÔÇª       | ├ó╦åÔÇª
 2bce83cd-e738-4c97-beff-ed2b81dfb756 | 01663102-5c8f-4d0a-9d75-ed4b104a14a2 |             1 | reading_risk_pipeline | 2026-05-12 11:09:04.792146+00 | 2026-05-12 11:09:04.840406+00 |       1 | ├ó╦åÔÇª       | ├ó╦åÔÇª
 ab8e12b9-f2fc-4347-b39c-e3b1c159008e | fc00f442-9194-4d19-b3d3-c94d5c93ac2e |             1 | reading_risk_pipeline | 2026-05-12 11:09:04.742556+00 | 2026-05-12 11:09:04.785874+00 |       1 | ├ó╦åÔÇª       | ├ó╦åÔÇª
 187556c3-43ac-4439-b1da-346e0d62ac11 | 89f15de3-a5eb-4870-a000-a1c11586d4f3 |             1 | reading_risk_pipeline | 2026-05-12 11:09:04.6786+00   | 2026-05-12 11:09:04.73625+00  |       1 | ├ó╦åÔÇª       | ├ó╦åÔÇª
 b907bef8-83ac-445e-a925-10207c0c1922 | dcb7e4cc-bf2d-403b-831b-4f8c700a7f59 |             1 | reading_risk_pipeline | 2026-05-12 11:09:04.630416+00 | 2026-05-12 11:09:04.672478+00 |       1 | ├ó╦åÔÇª       | ├ó╦åÔÇª
 a3e20cdc-47ec-4b7e-a089-b902146d60e2 | 4ff3cb5e-ee7f-496b-bc19-59beaaca0039 |             1 | reading_risk_pipeline | 2026-05-12 11:09:04.579735+00 | 2026-05-12 11:09:04.622216+00 |       1 | ├ó╦åÔÇª       | ├ó╦åÔÇª
 aa4872f8-0c1d-4981-82eb-32eb45f66ddf | 17ccb9cf-465e-41ce-b204-bfe9dca53bbf |             1 | reading_risk_pipeline | 2026-05-12 11:08:34.846639+00 | 2026-05-12 11:08:34.891703+00 |       1 | ├ó╦åÔÇª       | ├ó╦åÔÇª
 6eaef284-a751-441b-91e5-0b89b59497e7 | be34f3ce-ade2-4365-b43a-e48f95dac113 |             1 | reading_risk_pipeline | 2026-05-12 11:08:34.778235+00 | 2026-05-12 11:08:34.839608+00 |       1 | ├ó╦åÔÇª       | ├ó╦åÔÇª
 fc8fda81-6694-4bf9-8432-2014655d72b2 | eb028eda-75f2-4730-b193-916815902773 |             1 | reading_risk_pipeline | 2026-05-12 11:08:34.729747+00 | 2026-05-12 11:08:34.772115+00 |       1 | ├ó╦åÔÇª       | ├ó╦åÔÇª
 06693d74-3cbc-451c-b8e5-d0d11de4ebfc | 5ffe4600-c4d9-4856-b389-c19b4bbfe002 |             1 | reading_risk_pipeline | 2026-05-12 11:08:34.681459+00 | 2026-05-12 11:08:34.723319+00 |       1 | ├ó╦åÔÇª       | ├ó╦åÔÇª
 72945d2d-c73a-4d84-9658-90e3a56d4049 | 1966cd98-a0b2-4ce5-a919-87412ec94ccb |             1 | reading_risk_pipeline | 2026-05-12 11:08:34.630443+00 | 2026-05-12 11:08:34.674953+00 |       1 | ├ó╦åÔÇª       | ├ó╦åÔÇª
 e8e8057e-1398-46e7-aeda-ae5e7c3eff70 | 2195bcf4-4a86-408f-8690-12e5d8a4c54f |             1 | reading_risk_pipeline | 2026-05-12 11:08:34.566601+00 | 2026-05-12 11:08:34.623665+00 |       1 | ├ó╦åÔÇª       | ├ó╦åÔÇª
 625a5413-2b73-425f-b602-e2cef3345872 | ea6402fe-9db2-4e97-b70f-ee0c4395b308 |             1 | reading_risk_pipeline | 2026-05-12 11:08:04.812488+00 | 2026-05-12 11:08:04.858912+00 |       1 | ├ó╦åÔÇª       | ├ó╦åÔÇª
 a29287c0-f888-451b-a7c2-8e4f0ee35e2d | 971e54d6-cca8-4fa2-b97c-0afaba0ff3ea |             1 | reading_risk_pipeline | 2026-05-12 11:08:04.761165+00 | 2026-05-12 11:08:04.805463+00 |       1 | ├ó╦åÔÇª       | ├ó╦åÔÇª
 79f8287f-d682-4bbb-8504-ef98638225b9 | 25914e3c-3bee-42f1-b543-f184944ecdee |             1 | reading_risk_pipeline | 2026-05-12 11:08:04.708531+00 | 2026-05-12 11:08:04.754415+00 |       1 | ├ó╦åÔÇª       | ├ó╦åÔÇª
 39501365-0ca2-412c-912a-cb870840477b | 130d1f62-b1c7-431b-a247-9a8c75938059 |             1 | reading_risk_pipeline | 2026-05-12 11:08:04.654769+00 | 2026-05-12 11:08:04.702329+00 |       1 | ├ó╦åÔÇª       | ├ó╦åÔÇª
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
  "ErrorCode",
  "ErrorMessage",
  count(*) as count
from pipeline.rejected_events
group by "ErrorCode", "ErrorMessage"
order by count desc;
```

```text
docker : ERROR:  column "ErrorCode" does not exist
At C:\Users\Miguel\UNI\6sem\PS\IMP\A\NatureProtector\scripts\evidence\collect-v1-runtime-evidence.ps1:90 char:26
+         $result = $Sql | docker exec -i $PostgresContainer psql `
+                          ~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~
    + CategoryInfo          : NotSpecified: (ERROR:  column ... does not exist:String) [], RemoteException
    + FullyQualifiedErrorId : NativeCommandError
 
LINE 2:   "ErrorCode",
          ^

```

## Latest rejected events

```sql
select *
from pipeline.rejected_events
order by "CreatedAt" desc
limit 25;
```

```text
docker : ERROR:  column "CreatedAt" does not exist
At C:\Users\Miguel\UNI\6sem\PS\IMP\A\NatureProtector\scripts\evidence\collect-v1-runtime-evidence.ps1:90 char:26
+         $result = $Sql | docker exec -i $PostgresContainer psql `
+                          ~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~
    + CategoryInfo          : NotSpecified: (ERROR:  column ... does not exist:String) [], RemoteException
    + FullyQualifiedErrorId : NativeCommandError
 
LINE 3: order by "CreatedAt" desc
                 ^

```

## Latest quarantined events

```sql
select *
from pipeline.quarantined_events
order by "CreatedAt" desc
limit 25;
```

```text
docker : ERROR:  column "CreatedAt" does not exist
At C:\Users\Miguel\UNI\6sem\PS\IMP\A\NatureProtector\scripts\evidence\collect-v1-runtime-evidence.ps1:90 char:26
+         $result = $Sql | docker exec -i $PostgresContainer psql `
+                          ~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~
    + CategoryInfo          : NotSpecified: (ERROR:  column ... does not exist:String) [], RemoteException
    + FullyQualifiedErrorId : NativeCommandError
 
LINE 3: order by "CreatedAt" desc
                 ^

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
              6220 |             6219 |                6111 |                      25 |                       1 |            4
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
 2026-04-11 11:38:58.563527+00 | 2026-05-12 11:12:04.933814+00 | 2020-09-13 10:00:00+00        | 2020-09-13 10:09:30+00       | 2026-05-12 11:12:04.974282+00 | 2026-04-17 13:42:58.79077+00
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
             6219 |            0.1 |           0.95 | 0.5107010773436259
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
 Low       |  1031 |       0.1 |       0.1
 Moderate  |  1262 |       0.3 |       0.4
 High      |  1800 |      0.65 |      0.65
 VeryHigh  |  2081 |       0.7 |       0.7
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
 c017d28a-a39c-4e50-9549-e8113b6da835 | b3f4fb84-bf17-5522-a5f3-70fd1212f381 | 17badc17-7920-17ac-c213-db1c12bedfb2 | 8a65d4e7-92f5-1b6f-fdb0-d60ec5707d72 | 1477c43e-314b-4f27-bdc0-45659fd0fa80 | 2020-09-13 10:09:30+00 |       0.1 | Low       | Area=b3f4fb84-bf17-5522-a5f3-70fd1212f381; Sensor=17badc17-7920-17ac-c213-db1c12bedfb2; Event=1477c43e-314b-4f27-bdc0-45659fd0fa80; Metric=WindSpeed; Value=3,85; BaseRisk=0,10; AdjustedScore=0,10; C=1,00; I=1,00; EligibilityFactor=1,00; ParameterSet=Candidate Parameter Set V1.0 (non-calibrated).    | 2026-05-12 11:12:04.933814+00
 73029c1d-e68a-42f7-83d8-f8bf84d520bb | b3f4fb84-bf17-5522-a5f3-70fd1212f381 | e7660606-cadd-4212-e67b-574c09ef789a | 33d52e78-7801-cf3b-bc8b-c798db0d4068 | 60c1756b-4d53-49e9-a9de-6866a60fe34b | 2020-09-13 10:09:30+00 |       0.1 | Low       | Area=b3f4fb84-bf17-5522-a5f3-70fd1212f381; Sensor=e7660606-cadd-4212-e67b-574c09ef789a; Event=60c1756b-4d53-49e9-a9de-6866a60fe34b; Metric=WindSpeed; Value=3,84; BaseRisk=0,10; AdjustedScore=0,10; C=1,00; I=1,00; EligibilityFactor=1,00; ParameterSet=Candidate Parameter Set V1.0 (non-calibrated).    | 2026-05-12 11:12:04.882251+00
 732d6d27-9bdb-4493-be60-0f18a008b94e | b3f4fb84-bf17-5522-a5f3-70fd1212f381 | 8e6a3d8a-7e65-4fa5-8c51-fa4597b203b8 | 8a65d4e7-92f5-1b6f-fdb0-d60ec5707d72 | dd022ec4-4fd0-4a09-a293-e806a535627d | 2020-09-13 10:09:30+00 |      0.65 | High      | Area=b3f4fb84-bf17-5522-a5f3-70fd1212f381; Sensor=8e6a3d8a-7e65-4fa5-8c51-fa4597b203b8; Event=dd022ec4-4fd0-4a09-a293-e806a535627d; Metric=Temperature; Value=30,72; BaseRisk=0,65; AdjustedScore=0,65; C=1,00; I=1,00; EligibilityFactor=1,00; ParameterSet=Candidate Parameter Set V1.0 (non-calibrated). | 2026-05-12 11:12:04.828071+00
 f1864b11-9f7b-45d9-a584-53c31a4dff72 | b3f4fb84-bf17-5522-a5f3-70fd1212f381 | 27bf9aad-094f-7dc6-45b2-129a8cc0522e | 33d52e78-7801-cf3b-bc8b-c798db0d4068 | 02b394ff-440d-4674-b210-48fc0634dc02 | 2020-09-13 10:09:30+00 |      0.65 | High      | Area=b3f4fb84-bf17-5522-a5f3-70fd1212f381; Sensor=27bf9aad-094f-7dc6-45b2-129a8cc0522e; Event=02b394ff-440d-4674-b210-48fc0634dc02; Metric=Temperature; Value=31,41; BaseRisk=0,65; AdjustedScore=0,65; C=1,00; I=1,00; EligibilityFactor=1,00; ParameterSet=Candidate Parameter Set V1.0 (non-calibrated). | 2026-05-12 11:12:04.77683+00
 288c6bc1-e0fc-43b0-b0f2-132bd8d90a84 | b3f4fb84-bf17-5522-a5f3-70fd1212f381 | 2f494582-4077-86f2-9707-780861949155 | 8a65d4e7-92f5-1b6f-fdb0-d60ec5707d72 | 6673e384-f20c-43ea-ac8d-257bdd347017 | 2020-09-13 10:09:30+00 |       0.7 | VeryHigh  | Area=b3f4fb84-bf17-5522-a5f3-70fd1212f381; Sensor=2f494582-4077-86f2-9707-780861949155; Event=6673e384-f20c-43ea-ac8d-257bdd347017; Metric=Humidity; Value=24,32; BaseRisk=0,70; AdjustedScore=0,70; C=1,00; I=1,00; EligibilityFactor=1,00; ParameterSet=Candidate Parameter Set V1.0 (non-calibrated).    | 2026-05-12 11:12:04.717701+00
 60caa0a3-9542-4a07-b4bb-c9afa160c1d2 | b3f4fb84-bf17-5522-a5f3-70fd1212f381 | 22702eee-75aa-3cf4-31ab-b689bfb9a8bc | 33d52e78-7801-cf3b-bc8b-c798db0d4068 | 6e2fcc92-a101-463b-8e28-84621f604a33 | 2020-09-13 10:09:30+00 |       0.7 | VeryHigh  | Area=b3f4fb84-bf17-5522-a5f3-70fd1212f381; Sensor=22702eee-75aa-3cf4-31ab-b689bfb9a8bc; Event=6e2fcc92-a101-463b-8e28-84621f604a33; Metric=Humidity; Value=25,10; BaseRisk=0,70; AdjustedScore=0,70; C=1,00; I=1,00; EligibilityFactor=1,00; ParameterSet=Candidate Parameter Set V1.0 (non-calibrated).    | 2026-05-12 11:12:04.668974+00
 ac870e77-4e2e-411a-89ca-a47fd3adac3b | b3f4fb84-bf17-5522-a5f3-70fd1212f381 | 17badc17-7920-17ac-c213-db1c12bedfb2 | 8a65d4e7-92f5-1b6f-fdb0-d60ec5707d72 | a8647ed4-9b2d-4a77-a657-8f3d5206646e | 2020-09-13 10:09:00+00 |       0.1 | Low       | Area=b3f4fb84-bf17-5522-a5f3-70fd1212f381; Sensor=17badc17-7920-17ac-c213-db1c12bedfb2; Event=a8647ed4-9b2d-4a77-a657-8f3d5206646e; Metric=WindSpeed; Value=4,28; BaseRisk=0,10; AdjustedScore=0,10; C=1,00; I=1,00; EligibilityFactor=1,00; ParameterSet=Candidate Parameter Set V1.0 (non-calibrated).    | 2026-05-12 11:11:34.943926+00
 ee92de2c-4473-460c-9491-c07a7f5df81e | b3f4fb84-bf17-5522-a5f3-70fd1212f381 | e7660606-cadd-4212-e67b-574c09ef789a | 33d52e78-7801-cf3b-bc8b-c798db0d4068 | d71223d1-7215-44aa-aaf3-17af6446f715 | 2020-09-13 10:09:00+00 |       0.1 | Low       | Area=b3f4fb84-bf17-5522-a5f3-70fd1212f381; Sensor=e7660606-cadd-4212-e67b-574c09ef789a; Event=d71223d1-7215-44aa-aaf3-17af6446f715; Metric=WindSpeed; Value=4,06; BaseRisk=0,10; AdjustedScore=0,10; C=1,00; I=1,00; EligibilityFactor=1,00; ParameterSet=Candidate Parameter Set V1.0 (non-calibrated).    | 2026-05-12 11:11:34.882197+00
 1ba37cb3-0fc2-441c-89a0-02e849791f15 | b3f4fb84-bf17-5522-a5f3-70fd1212f381 | 8e6a3d8a-7e65-4fa5-8c51-fa4597b203b8 | 8a65d4e7-92f5-1b6f-fdb0-d60ec5707d72 | 7f27d6b3-bc99-4127-b9f8-59954e111cec | 2020-09-13 10:09:00+00 |      0.65 | High      | Area=b3f4fb84-bf17-5522-a5f3-70fd1212f381; Sensor=8e6a3d8a-7e65-4fa5-8c51-fa4597b203b8; Event=7f27d6b3-bc99-4127-b9f8-59954e111cec; Metric=Temperature; Value=30,66; BaseRisk=0,65; AdjustedScore=0,65; C=1,00; I=1,00; EligibilityFactor=1,00; ParameterSet=Candidate Parameter Set V1.0 (non-calibrated). | 2026-05-12 11:11:34.807238+00
 2230d0f7-6e00-4b87-b3f5-e95e05337b9a | b3f4fb84-bf17-5522-a5f3-70fd1212f381 | 27bf9aad-094f-7dc6-45b2-129a8cc0522e | 33d52e78-7801-cf3b-bc8b-c798db0d4068 | 7f943a4f-6211-40db-94dd-5baf73a959e1 | 2020-09-13 10:09:00+00 |      0.65 | High      | Area=b3f4fb84-bf17-5522-a5f3-70fd1212f381; Sensor=27bf9aad-094f-7dc6-45b2-129a8cc0522e; Event=7f943a4f-6211-40db-94dd-5baf73a959e1; Metric=Temperature; Value=30,42; BaseRisk=0,65; AdjustedScore=0,65; C=1,00; I=1,00; EligibilityFactor=1,00; ParameterSet=Candidate Parameter Set V1.0 (non-calibrated). | 2026-05-12 11:11:34.755209+00
 648ef3f2-0682-42e2-8f31-7ed6e881bfbd | b3f4fb84-bf17-5522-a5f3-70fd1212f381 | 2f494582-4077-86f2-9707-780861949155 | 8a65d4e7-92f5-1b6f-fdb0-d60ec5707d72 | c85f71e5-2001-4178-83c0-e32ffb5f0380 | 2020-09-13 10:09:00+00 |       0.7 | VeryHigh  | Area=b3f4fb84-bf17-5522-a5f3-70fd1212f381; Sensor=2f494582-4077-86f2-9707-780861949155; Event=c85f71e5-2001-4178-83c0-e32ffb5f0380; Metric=Humidity; Value=26,17; BaseRisk=0,70; AdjustedScore=0,70; C=1,00; I=1,00; EligibilityFactor=1,00; ParameterSet=Candidate Parameter Set V1.0 (non-calibrated).    | 2026-05-12 11:11:34.703577+00
 672f7340-314a-4dd8-9b76-2849f2124945 | b3f4fb84-bf17-5522-a5f3-70fd1212f381 | 22702eee-75aa-3cf4-31ab-b689bfb9a8bc | 33d52e78-7801-cf3b-bc8b-c798db0d4068 | 95fabfdf-6c73-4685-a8d1-3f214edd8470 | 2020-09-13 10:09:00+00 |       0.7 | VeryHigh  | Area=b3f4fb84-bf17-5522-a5f3-70fd1212f381; Sensor=22702eee-75aa-3cf4-31ab-b689bfb9a8bc; Event=95fabfdf-6c73-4685-a8d1-3f214edd8470; Metric=Humidity; Value=26,36; BaseRisk=0,70; AdjustedScore=0,70; C=1,00; I=1,00; EligibilityFactor=1,00; ParameterSet=Candidate Parameter Set V1.0 (non-calibrated).    | 2026-05-12 11:11:34.650543+00
 7c24ad71-3a83-421d-9fbb-7ad189360b0d | b3f4fb84-bf17-5522-a5f3-70fd1212f381 | 17badc17-7920-17ac-c213-db1c12bedfb2 | 8a65d4e7-92f5-1b6f-fdb0-d60ec5707d72 | ba311f72-cfa2-4dac-a909-f255f49a90d0 | 2020-09-13 10:08:30+00 |       0.1 | Low       | Area=b3f4fb84-bf17-5522-a5f3-70fd1212f381; Sensor=17badc17-7920-17ac-c213-db1c12bedfb2; Event=ba311f72-cfa2-4dac-a909-f255f49a90d0; Metric=WindSpeed; Value=4,62; BaseRisk=0,10; AdjustedScore=0,10; C=1,00; I=1,00; EligibilityFactor=1,00; ParameterSet=Candidate Parameter Set V1.0 (non-calibrated).    | 2026-05-12 11:11:04.850151+00
 f9373d83-e996-4f6b-8422-e10d153385dd | b3f4fb84-bf17-5522-a5f3-70fd1212f381 | e7660606-cadd-4212-e67b-574c09ef789a | 33d52e78-7801-cf3b-bc8b-c798db0d4068 | 79095d28-b3e0-43ff-a893-4ce4a1a23c2c | 2020-09-13 10:08:30+00 |       0.3 | Moderate  | Area=b3f4fb84-bf17-5522-a5f3-70fd1212f381; Sensor=e7660606-cadd-4212-e67b-574c09ef789a; Event=79095d28-b3e0-43ff-a893-4ce4a1a23c2c; Metric=WindSpeed; Value=5,01; BaseRisk=0,30; AdjustedScore=0,30; C=1,00; I=1,00; EligibilityFactor=1,00; ParameterSet=Candidate Parameter Set V1.0 (non-calibrated).    | 2026-05-12 11:11:04.797783+00
 9cbc050a-8c29-4f4b-95d4-e5d7cecfb9d8 | b3f4fb84-bf17-5522-a5f3-70fd1212f381 | 8e6a3d8a-7e65-4fa5-8c51-fa4597b203b8 | 8a65d4e7-92f5-1b6f-fdb0-d60ec5707d72 | 8e983901-1c8f-4f40-b227-a5a803a9706a | 2020-09-13 10:08:30+00 |       0.4 | Moderate  | Area=b3f4fb84-bf17-5522-a5f3-70fd1212f381; Sensor=8e6a3d8a-7e65-4fa5-8c51-fa4597b203b8; Event=8e983901-1c8f-4f40-b227-a5a803a9706a; Metric=Temperature; Value=29,84; BaseRisk=0,40; AdjustedScore=0,40; C=1,00; I=1,00; EligibilityFactor=1,00; ParameterSet=Candidate Parameter Set V1.0 (non-calibrated). | 2026-05-12 11:11:04.744335+00
 06e4f872-84e8-4285-bf57-cad9a459fb90 | b3f4fb84-bf17-5522-a5f3-70fd1212f381 | 27bf9aad-094f-7dc6-45b2-129a8cc0522e | 33d52e78-7801-cf3b-bc8b-c798db0d4068 | a8316e07-71e4-4605-98b5-85f9095c7e08 | 2020-09-13 10:08:30+00 |      0.65 | High      | Area=b3f4fb84-bf17-5522-a5f3-70fd1212f381; Sensor=27bf9aad-094f-7dc6-45b2-129a8cc0522e; Event=a8316e07-71e4-4605-98b5-85f9095c7e08; Metric=Temperature; Value=30,03; BaseRisk=0,65; AdjustedScore=0,65; C=1,00; I=1,00; EligibilityFactor=1,00; ParameterSet=Candidate Parameter Set V1.0 (non-calibrated). | 2026-05-12 11:11:04.69476+00
 7bec71be-cd39-4d1c-b402-dd25aaad7c23 | b3f4fb84-bf17-5522-a5f3-70fd1212f381 | 22702eee-75aa-3cf4-31ab-b689bfb9a8bc | 33d52e78-7801-cf3b-bc8b-c798db0d4068 | d0fdcafc-5838-4543-b139-352a4f478830 | 2020-09-13 10:08:30+00 |       0.7 | VeryHigh  | Area=b3f4fb84-bf17-5522-a5f3-70fd1212f381; Sensor=22702eee-75aa-3cf4-31ab-b689bfb9a8bc; Event=d0fdcafc-5838-4543-b139-352a4f478830; Metric=Humidity; Value=26,60; BaseRisk=0,70; AdjustedScore=0,70; C=1,00; I=1,00; EligibilityFactor=1,00; ParameterSet=Candidate Parameter Set V1.0 (non-calibrated).    | 2026-05-12 11:11:04.634908+00
 fd905efa-bcfa-4282-aeee-9b79232aba36 | b3f4fb84-bf17-5522-a5f3-70fd1212f381 | 17badc17-7920-17ac-c213-db1c12bedfb2 | 8a65d4e7-92f5-1b6f-fdb0-d60ec5707d72 | 7547dfcb-d49d-4ac0-a486-d73ef4cbaddc | 2020-09-13 10:08:00+00 |       0.3 | Moderate  | Area=b3f4fb84-bf17-5522-a5f3-70fd1212f381; Sensor=17badc17-7920-17ac-c213-db1c12bedfb2; Event=7547dfcb-d49d-4ac0-a486-d73ef4cbaddc; Metric=WindSpeed; Value=5,18; BaseRisk=0,30; AdjustedScore=0,30; C=1,00; I=1,00; EligibilityFactor=1,00; ParameterSet=Candidate Parameter Set V1.0 (non-calibrated).    | 2026-05-12 11:10:34.933687+00
 3f007dea-1092-426f-86ac-6853f5c22430 | b3f4fb84-bf17-5522-a5f3-70fd1212f381 | e7660606-cadd-4212-e67b-574c09ef789a | 33d52e78-7801-cf3b-bc8b-c798db0d4068 | 445b70da-746e-4f04-b83c-bb6aca3a9c43 | 2020-09-13 10:08:00+00 |       0.3 | Moderate  | Area=b3f4fb84-bf17-5522-a5f3-70fd1212f381; Sensor=e7660606-cadd-4212-e67b-574c09ef789a; Event=445b70da-746e-4f04-b83c-bb6aca3a9c43; Metric=WindSpeed; Value=5,09; BaseRisk=0,30; AdjustedScore=0,30; C=1,00; I=1,00; EligibilityFactor=1,00; ParameterSet=Candidate Parameter Set V1.0 (non-calibrated).    | 2026-05-12 11:10:34.877008+00
 19d91473-63a3-461c-81c5-6735deca0e21 | b3f4fb84-bf17-5522-a5f3-70fd1212f381 | 8e6a3d8a-7e65-4fa5-8c51-fa4597b203b8 | 8a65d4e7-92f5-1b6f-fdb0-d60ec5707d72 | 4aee7c74-6e97-4771-829b-3f8e7ac4fd0c | 2020-09-13 10:08:00+00 |       0.4 | Moderate  | Area=b3f4fb84-bf17-5522-a5f3-70fd1212f381; Sensor=8e6a3d8a-7e65-4fa5-8c51-fa4597b203b8; Event=4aee7c74-6e97-4771-829b-3f8e7ac4fd0c; Metric=Temperature; Value=29,87; BaseRisk=0,40; AdjustedScore=0,40; C=1,00; I=1,00; EligibilityFactor=1,00; ParameterSet=Candidate Parameter Set V1.0 (non-calibrated). | 2026-05-12 11:10:34.826391+00
 f4dc43ee-49ac-45e5-be79-a6032621d2a1 | b3f4fb84-bf17-5522-a5f3-70fd1212f381 | 27bf9aad-094f-7dc6-45b2-129a8cc0522e | 33d52e78-7801-cf3b-bc8b-c798db0d4068 | 6ae7eddb-696d-499e-9ce1-034d9ec7b7eb | 2020-09-13 10:08:00+00 |      0.65 | High      | Area=b3f4fb84-bf17-5522-a5f3-70fd1212f381; Sensor=27bf9aad-094f-7dc6-45b2-129a8cc0522e; Event=6ae7eddb-696d-499e-9ce1-034d9ec7b7eb; Metric=Temperature; Value=30,06; BaseRisk=0,65; AdjustedScore=0,65; C=1,00; I=1,00; EligibilityFactor=1,00; ParameterSet=Candidate Parameter Set V1.0 (non-calibrated). | 2026-05-12 11:10:34.770571+00
 49b0c5e5-5d88-4ccf-82e9-0e86e7cbd862 | b3f4fb84-bf17-5522-a5f3-70fd1212f381 | 2f494582-4077-86f2-9707-780861949155 | 8a65d4e7-92f5-1b6f-fdb0-d60ec5707d72 | 4eb2de39-6e23-4939-a947-619a3ee5fd15 | 2020-09-13 10:08:00+00 |       0.7 | VeryHigh  | Area=b3f4fb84-bf17-5522-a5f3-70fd1212f381; Sensor=2f494582-4077-86f2-9707-780861949155; Event=4eb2de39-6e23-4939-a947-619a3ee5fd15; Metric=Humidity; Value=27,79; BaseRisk=0,70; AdjustedScore=0,70; C=1,00; I=1,00; EligibilityFactor=1,00; ParameterSet=Candidate Parameter Set V1.0 (non-calibrated).    | 2026-05-12 11:10:34.712312+00
 5af89871-c064-4e50-ac0d-52840b3c4829 | b3f4fb84-bf17-5522-a5f3-70fd1212f381 | 22702eee-75aa-3cf4-31ab-b689bfb9a8bc | 33d52e78-7801-cf3b-bc8b-c798db0d4068 | cf61c606-5b5d-43f1-b641-bf9ec07d2cda | 2020-09-13 10:08:00+00 |       0.7 | VeryHigh  | Area=b3f4fb84-bf17-5522-a5f3-70fd1212f381; Sensor=22702eee-75aa-3cf4-31ab-b689bfb9a8bc; Event=cf61c606-5b5d-43f1-b641-bf9ec07d2cda; Metric=Humidity; Value=27,78; BaseRisk=0,70; AdjustedScore=0,70; C=1,00; I=1,00; EligibilityFactor=1,00; ParameterSet=Candidate Parameter Set V1.0 (non-calibrated).    | 2026-05-12 11:10:34.619233+00
 d114db26-61c3-41fe-940d-461db732725d | b3f4fb84-bf17-5522-a5f3-70fd1212f381 | 17badc17-7920-17ac-c213-db1c12bedfb2 | 8a65d4e7-92f5-1b6f-fdb0-d60ec5707d72 | 431eb840-6cb1-4627-8db9-49083a68df0e | 2020-09-13 10:07:30+00 |       0.3 | Moderate  | Area=b3f4fb84-bf17-5522-a5f3-70fd1212f381; Sensor=17badc17-7920-17ac-c213-db1c12bedfb2; Event=431eb840-6cb1-4627-8db9-49083a68df0e; Metric=WindSpeed; Value=5,26; BaseRisk=0,30; AdjustedScore=0,30; C=1,00; I=1,00; EligibilityFactor=1,00; ParameterSet=Candidate Parameter Set V1.0 (non-calibrated).    | 2026-05-12 11:10:04.936352+00
 5927bb3e-0049-4d98-942a-775634855138 | b3f4fb84-bf17-5522-a5f3-70fd1212f381 | e7660606-cadd-4212-e67b-574c09ef789a | 33d52e78-7801-cf3b-bc8b-c798db0d4068 | 8a8ff1ed-fb40-490a-b2a7-818efcebf868 | 2020-09-13 10:07:30+00 |       0.3 | Moderate  | Area=b3f4fb84-bf17-5522-a5f3-70fd1212f381; Sensor=e7660606-cadd-4212-e67b-574c09ef789a; Event=8a8ff1ed-fb40-490a-b2a7-818efcebf868; Metric=WindSpeed; Value=5,30; BaseRisk=0,30; AdjustedScore=0,30; C=1,00; I=1,00; EligibilityFactor=1,00; ParameterSet=Candidate Parameter Set V1.0 (non-calibrated).    | 2026-05-12 11:10:04.878103+00
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
 5739af5e-1709-4d14-90fd-cefb9ce0ed1c | b3f4fb84-bf17-5522-a5f3-70fd1212f381 | c03be3d5-1f70-15cb-2fc0-3c86a204a644 | ├ó╦åÔÇª             | 2020-09-13 10:09:30+00 | 0.44799999999999973 | Moderate           | Medium   | Aggregated from 75 assessments; 37 at High or above. |              75 | 2026-05-12 11:12:04.974282+00
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
 35e298e3-043b-42f9-86bd-135becebce67 | b3f4fb84-bf17-5522-a5f3-70fd1212f381 | 8a65d4e7-92f5-1b6f-fdb0-d60ec5707d72 | 17badc17-7920-17ac-c213-db1c12bedfb2 | c017d28a-a39c-4e50-9549-e8113b6da835 | 2020-09-13 10:09:30+00 |       0.1 | Low       | Low      | Area=b3f4fb84-bf17-5522-a5f3-70fd1212f381; Sensor=17badc17-7920-17ac-c213-db1c12bedfb2; Event=1477c43e-314b-4f27-bdc0-45659fd0fa80; Metric=WindSpeed; Value=3,85; BaseRisk=0,10; AdjustedScore=0,10; C=1,00; I=1,00; EligibilityFactor=1,00; ParameterSet=Candidate Parameter Set V1.0 (non-calibrated). | 2026-05-12 11:12:04.938268+00
 98b72b45-ab6b-45ab-ba9b-8c7cebc198e7 | b3f4fb84-bf17-5522-a5f3-70fd1212f381 | 33d52e78-7801-cf3b-bc8b-c798db0d4068 | e7660606-cadd-4212-e67b-574c09ef789a | 73029c1d-e68a-42f7-83d8-f8bf84d520bb | 2020-09-13 10:09:30+00 |       0.1 | Low       | Low      | Area=b3f4fb84-bf17-5522-a5f3-70fd1212f381; Sensor=e7660606-cadd-4212-e67b-574c09ef789a; Event=60c1756b-4d53-49e9-a9de-6866a60fe34b; Metric=WindSpeed; Value=3,84; BaseRisk=0,10; AdjustedScore=0,10; C=1,00; I=1,00; EligibilityFactor=1,00; ParameterSet=Candidate Parameter Set V1.0 (non-calibrated). | 2026-05-12 11:12:04.887448+00
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
 b3f4fb84-bf17-5522-a5f3-70fd1212f381 | 0.44799999999999973 | Moderate           | Medium        | 2020-09-13 10:09:30+00 | 2026-05-12 11:12:04.974282+00 | area-risk-high | High           | Resolved     | Area risk is High with score 0,50. | 2020-09-13 10:01:45+00 | 2026-04-17 13:42:58.79077+00
 b3f4fb84-bf17-5522-a5f3-70fd1212f381 | 0.44799999999999973 | Moderate           | Medium        | 2020-09-13 10:09:30+00 | 2026-05-12 11:12:04.974282+00 | area-risk-high | High           | Resolved     | Area risk is High with score 0,50. | 2020-09-13 10:01:25+00 | 2026-04-17 13:08:10.878331+00
 b3f4fb84-bf17-5522-a5f3-70fd1212f381 | 0.44799999999999973 | Moderate           | Medium        | 2020-09-13 10:09:30+00 | 2026-05-12 11:12:04.974282+00 | area-risk-high | High           | Resolved     | Area risk is High with score 0,50. | 2020-09-13 10:00:10+00 | 2026-04-11 16:50:56.671844+00
 b3f4fb84-bf17-5522-a5f3-70fd1212f381 | 0.44799999999999973 | Moderate           | Medium        | 2020-09-13 10:09:30+00 | 2026-05-12 11:12:04.974282+00 | area-risk-high | High           | Resolved     | Area risk is High with score 0,50. | 2020-09-13 10:00:00+00 | 2026-04-11 13:44:42.698509+00
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
    "snapshotTimestamp":  "2020-09-13T10:09:30+00:00",
    "aggregateRiskScore":  0.44799999999999973,
    "aggregateRiskLevel":  "Moderate",
    "severity":  "Medium",
    "summary":  "Aggregated from 75 assessments; 37 at High or above.",
    "assessmentCount":  75,
    "updatedAt":  "2026-05-12T11:12:04.974282+00:00",
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
