# NatureProtector V1 Runtime Evidence

- GeneratedAt: 2026-05-14T00:17:04.9777108+02:00
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
 M ../../src/NatureProtector.Backoffice.Api/ControlPlane/Contracts/ControlPlaneResponses.cs
 M ../../src/NatureProtector.Backoffice.Api/ControlPlane/Services/PostgresControlPlaneService.cs
 M ../../src/NatureProtector.Core/Risk/RiskAssessment.cs
 M ../../src/NatureProtector.Prevention.Host/Processing/ReadingRiskPipeline.cs
 M ../../src/NatureProtector.Prevention.Host/Projection/InMemoryAreaOperationalProjectionStore.cs
 M ../../src/NatureProtector.Prevention.Host/Projection/PostgresAreaOperationalProjectionStore.cs
 M ../../src/NatureProtector.Prevention/Readings/NormalizedReading.cs
 M ../../src/NatureProtector.Prevention/Risk/RiskEligibilityReason.cs
 M ../../src/NatureProtector.Prevention/Risk/RiskEligibilityResult.cs
 M ../../src/NatureProtector.Prevention/Risk/RiskEligibilityService.cs
 M ../../src/NatureProtector.Prevention/Risk/RiskInput.cs
 M ../../src/NatureProtector.Prevention/Risk/SimpleRiskScoringService.cs
 M ../../tests/NatureProtector.Backoffice.Api.Tests/ControlPlaneApiTests.cs
 M ../../tests/NatureProtector.Backoffice.Api.Tests/ControlPlaneApiWebApplicationFactory.cs
 M ../../tests/NatureProtector.Backoffice.Api.Tests/PostgresControlPlaneServiceTests.cs
 M ../../tests/NatureProtector.Core.Tests/Risk/RiskAssessmentTests.cs
 M ../../tests/NatureProtector.Prevention.Host.Tests/Processing/ReadingRiskPipelineTests.cs
 M ../../tests/NatureProtector.Prevention.Host.Tests/Projection/PostgresAreaOperationalProjectionStoreTests.cs
 M ../../tests/NatureProtector.Prevention.Tests/Readings/NormalizedReadingTests.cs
 M ../../tests/NatureProtector.Prevention.Tests/Risk/RiskEligibilityServiceTests.cs
 M ../../tests/NatureProtector.Prevention.Tests/Risk/RiskInputTests.cs
 M ../../tests/NatureProtector.Prevention.Tests/Risk/SimpleRiskScoringServiceTests.cs
?? ../../docs/contracts/
?? ../../docs/evidence/
?? ../../docs/implementation/
?? ./
?? ../../src/NatureProtector.Prevention.Host/Projection/V1AlertPolicy.cs
?? ../../src/NatureProtector.Prevention/Readings/OperationalEvent.cs
?? ../../src/NatureProtector.Prevention/Risk/ClassifierResult.cs
?? ../../src/NatureProtector.Prevention/Risk/ClassifierSeverity.cs
?? ../../src/NatureProtector.Prevention/Risk/ClassifierStatus.cs
?? ../../src/NatureProtector.Prevention/Risk/DailyCellState.cs
?? ../../src/NatureProtector.Prevention/Risk/RiskInputStatus.cs
?? ../../tests/NatureProtector.Prevention.Host.Tests/Projection/InMemoryAreaOperationalProjectionStoreAlertPolicyTests.cs
?? ../../tests/NatureProtector.Prevention.Tests/Readings/OperationalEventTests.cs
?? ../../tests/NatureProtector.Prevention.Tests/Risk/ClassifierResultTests.cs
?? ../../tests/NatureProtector.Prevention.Tests/Risk/DailyCellStateTests.cs

```

## Docker containers

```text
NAMES         IMAGE                                      STATUS          PORTS
np-influxdb   influxdb:3.7.0-core                        Up 29 minutes   0.0.0.0:8181->8181/tcp
np-rabbitmq   rabbitmq:4.0.6-management                  Up 29 minutes   4369/tcp, 5671/tcp, 0.0.0.0:5672->5672/tcp, 15671/tcp, 15691-15692/tcp, 25672/tcp, 0.0.0.0:15672->15672/tcp
np-postgres   postgres:16                                Up 29 minutes   0.0.0.0:5433->5432/tcp
np-grafana    grafana/grafana-enterprise:13.0.1-ubuntu   Up 29 minutes   0.0.0.0:3000->3000/tcp

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
                      1 |     1 |        467 |           75 |               3 |                    3 |              24
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
 c03be3d5-1f70-15cb-2fc0-3c86a204a644 |             1 | t        | Bootstrap control-plane import for Proenca-a-Nova with pilot sensor network. | 2026-05-13 21:48:13.989908+00 | phase-04-bootstrap
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
 7afd89ce-98f1-4e9c-819f-a76a104a633e | b3f4fb84-bf17-5522-a5f3-70fd1212f381 | 6034251c-b502-5f7b-a557-7187178cfb04 | c03be3d5-1f70-15cb-2fc0-3c86a204a644 | scenario_b   | Scenario B - High Risk Proenca-a-Nova | 2026-05-13 21:48:33.22248+00  | 2026-05-13 21:48:33.330601+00 | ├ó╦åÔÇª                           | 2020-09-13 10:00:00+00 |              30 |             20 |         12345 |      2 | {"sensor_count":6,"scenario_category":"HighRisk"}
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
       86537 |          86802 |            132 |               100
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
      2 |  6133
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
 2026-04-11 11:38:57.569963+00 | 2026-05-13 21:50:04.209062+00 | 2020-09-13 10:00:00+00 | 2020-09-13 10:23:55+00
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
 be8ca28e-ef2e-41dc-a5ce-79fea71baeeb | ca977823-c412-4a8f-80b5-885e50134693 | SensorReadingProduced | NatureProtector.Simulator.Host |      2 |            1 | 2020-09-13 10:01:30+00 | 2026-05-13 21:50:04.209062+00 | 2026-05-13 21:50:04.298938+00 | 2026-05-13 21:50:04.298938+00 | ├ó╦åÔÇª           | ├ó╦åÔÇª              | ├ó╦åÔÇª
 79863a31-e2b6-4540-ab08-fff9d0400aa5 | 2fbed137-c198-44b0-ba0c-392380230f54 | SensorReadingProduced | NatureProtector.Simulator.Host |      2 |            1 | 2020-09-13 10:01:30+00 | 2026-05-13 21:50:04.149866+00 | 2026-05-13 21:50:04.1999+00   | 2026-05-13 21:50:04.1999+00   | ├ó╦åÔÇª           | ├ó╦åÔÇª              | ├ó╦åÔÇª
 b0727ca2-f846-4466-a76d-a1461bcdd405 | 8be46566-bdd0-4960-9f46-eef3054dc328 | SensorReadingProduced | NatureProtector.Simulator.Host |      2 |            1 | 2020-09-13 10:01:30+00 | 2026-05-13 21:50:04.095686+00 | 2026-05-13 21:50:04.143399+00 | 2026-05-13 21:50:04.143399+00 | ├ó╦åÔÇª           | ├ó╦åÔÇª              | ├ó╦åÔÇª
 b8a65ba3-16b0-47e2-9000-b60bb84e3ebd | 4381a0b5-ae0a-470f-ac25-30dcccf3bfbd | SensorReadingProduced | NatureProtector.Simulator.Host |      2 |            1 | 2020-09-13 10:01:30+00 | 2026-05-13 21:50:04.039967+00 | 2026-05-13 21:50:04.088203+00 | 2026-05-13 21:50:04.088203+00 | ├ó╦åÔÇª           | ├ó╦åÔÇª              | ├ó╦åÔÇª
 3d5753f0-77c7-4a88-9c17-3c9e0ac2990d | ba5e8872-06f9-48b2-8b83-752ac5a59d82 | SensorReadingProduced | NatureProtector.Simulator.Host |      2 |            1 | 2020-09-13 10:01:30+00 | 2026-05-13 21:50:03.974767+00 | 2026-05-13 21:50:04.033473+00 | 2026-05-13 21:50:04.033473+00 | ├ó╦åÔÇª           | ├ó╦åÔÇª              | ├ó╦åÔÇª
 50b02aa6-0bbc-4a5f-a25a-d1532cd9d9f8 | 4e6970b1-4ea7-4347-a401-39341f13fbac | SensorReadingProduced | NatureProtector.Simulator.Host |      2 |            1 | 2020-09-13 10:01:00+00 | 2026-05-13 21:49:34.199642+00 | 2026-05-13 21:49:34.248807+00 | 2026-05-13 21:49:34.248807+00 | ├ó╦åÔÇª           | ├ó╦åÔÇª              | ├ó╦åÔÇª
 7d7102d6-747f-4fce-9ff7-b5e92475b11b | 9c71ccf1-338d-47d9-8680-ee6d7bc243e5 | SensorReadingProduced | NatureProtector.Simulator.Host |      2 |            1 | 2020-09-13 10:01:00+00 | 2026-05-13 21:49:34.148296+00 | 2026-05-13 21:49:34.19258+00  | 2026-05-13 21:49:34.19258+00  | ├ó╦åÔÇª           | ├ó╦åÔÇª              | ├ó╦åÔÇª
 8ce0c187-69c1-4f1b-9299-e54cf216cd98 | 3918e686-a4aa-4612-8e70-1e4d22c3cfdc | SensorReadingProduced | NatureProtector.Simulator.Host |      2 |            1 | 2020-09-13 10:01:00+00 | 2026-05-13 21:49:34.092674+00 | 2026-05-13 21:49:34.142013+00 | 2026-05-13 21:49:34.142013+00 | ├ó╦åÔÇª           | ├ó╦åÔÇª              | ├ó╦åÔÇª
 43c6077c-1982-4ad2-9d6f-47c1e5e29f8d | 682c7078-c088-40dc-8864-a90b0597ff1f | SensorReadingProduced | NatureProtector.Simulator.Host |      2 |            1 | 2020-09-13 10:01:00+00 | 2026-05-13 21:49:34.038129+00 | 2026-05-13 21:49:34.085041+00 | 2026-05-13 21:49:34.085041+00 | ├ó╦åÔÇª           | ├ó╦åÔÇª              | ├ó╦åÔÇª
 6cb85ec4-1cf0-4cb1-8a2c-899aed187c08 | 5161644d-6344-4f11-8518-3252cf5ee38d | SensorReadingProduced | NatureProtector.Simulator.Host |      2 |            1 | 2020-09-13 10:01:00+00 | 2026-05-13 21:49:33.989741+00 | 2026-05-13 21:49:34.032221+00 | 2026-05-13 21:49:34.032221+00 | ├ó╦åÔÇª           | ├ó╦åÔÇª              | ├ó╦åÔÇª
 f3f53d79-824d-4b67-844b-c6b657612660 | 88841ead-7e55-4c17-a5fb-59b9543fa0e0 | SensorReadingProduced | NatureProtector.Simulator.Host |      2 |            1 | 2020-09-13 10:01:00+00 | 2026-05-13 21:49:33.921928+00 | 2026-05-13 21:49:33.983112+00 | 2026-05-13 21:49:33.983112+00 | ├ó╦åÔÇª           | ├ó╦åÔÇª              | ├ó╦åÔÇª
 c1f236cf-f7ad-4d52-91b0-85b12e731894 | e28c3d42-0184-4670-a09a-56d30eafc042 | SensorReadingProduced | NatureProtector.Simulator.Host |      2 |            1 | 2020-09-13 10:00:30+00 | 2026-05-13 21:49:04.223514+00 | 2026-05-13 21:49:04.307483+00 | 2026-05-13 21:49:04.307483+00 | ├ó╦åÔÇª           | ├ó╦åÔÇª              | ├ó╦åÔÇª
 57e2a35b-f6b4-40f1-b952-d9ff68abd6e3 | 77fb1e04-c101-4985-9063-29d2436a93bb | SensorReadingProduced | NatureProtector.Simulator.Host |      2 |            1 | 2020-09-13 10:00:30+00 | 2026-05-13 21:49:04.129053+00 | 2026-05-13 21:49:04.214417+00 | 2026-05-13 21:49:04.214417+00 | ├ó╦åÔÇª           | ├ó╦åÔÇª              | ├ó╦åÔÇª
 272ac392-9f50-4e01-b35e-2d9acf1ab9a7 | 947370fa-7abe-4cf9-bee6-bb675a2f1fdb | SensorReadingProduced | NatureProtector.Simulator.Host |      2 |            1 | 2020-09-13 10:00:30+00 | 2026-05-13 21:49:04.034634+00 | 2026-05-13 21:49:04.121702+00 | 2026-05-13 21:49:04.121702+00 | ├ó╦åÔÇª           | ├ó╦åÔÇª              | ├ó╦åÔÇª
 8294c98a-dd78-40d9-a343-48c057bab722 | ac47b042-20ef-405a-a685-e32a9fb51929 | SensorReadingProduced | NatureProtector.Simulator.Host |      2 |            1 | 2020-09-13 10:00:30+00 | 2026-05-13 21:49:03.941757+00 | 2026-05-13 21:49:04.026659+00 | 2026-05-13 21:49:04.026659+00 | ├ó╦åÔÇª           | ├ó╦åÔÇª              | ├ó╦åÔÇª
 5302b820-b872-40d3-899c-3a4077276ba9 | 2c130b49-c710-4c06-973e-a43879bc6afc | SensorReadingProduced | NatureProtector.Simulator.Host |      2 |            1 | 2020-09-13 10:00:30+00 | 2026-05-13 21:49:03.843199+00 | 2026-05-13 21:49:03.933806+00 | 2026-05-13 21:49:03.933806+00 | ├ó╦åÔÇª           | ├ó╦åÔÇª              | ├ó╦åÔÇª
 6f036683-e2ba-43f3-904c-e81910187342 | d5a2fd2d-03b1-43bd-bbfa-c9f6ba6ab67e | SensorReadingProduced | NatureProtector.Simulator.Host |      2 |            1 | 2020-09-13 10:00:30+00 | 2026-05-13 21:49:03.734618+00 | 2026-05-13 21:49:03.827879+00 | 2026-05-13 21:49:03.827879+00 | ├ó╦åÔÇª           | ├ó╦åÔÇª              | ├ó╦åÔÇª
 ad7d0f8e-3716-46ba-ac26-d7a7354eef5b | 054b2f21-1196-4ee6-8ea7-c1cff4cf8dd9 | SensorReadingProduced | NatureProtector.Simulator.Host |      2 |            1 | 2020-09-13 10:00:00+00 | 2026-05-13 21:48:35.453487+00 | 2026-05-13 21:48:35.611163+00 | 2026-05-13 21:48:35.611163+00 | ├ó╦åÔÇª           | ├ó╦åÔÇª              | ├ó╦åÔÇª
 d6f4caef-b617-4657-8518-e4c366d44cda | 4095f571-1402-45bb-9f91-db2c28e46cae | SensorReadingProduced | NatureProtector.Simulator.Host |      2 |            1 | 2020-09-13 10:00:00+00 | 2026-05-13 21:48:35.061838+00 | 2026-05-13 21:48:35.327887+00 | 2026-05-13 21:48:35.327887+00 | ├ó╦åÔÇª           | ├ó╦åÔÇª              | ├ó╦åÔÇª
 1baf4175-8d21-4dfa-bb40-7d27b54346b2 | 0feaff80-0aaf-42a2-8daf-ccb77cf48d07 | SensorReadingProduced | NatureProtector.Simulator.Host |      2 |            1 | 2020-09-13 10:00:00+00 | 2026-05-13 21:48:34.86847+00  | 2026-05-13 21:48:35.015833+00 | 2026-05-13 21:48:35.015833+00 | ├ó╦åÔÇª           | ├ó╦åÔÇª              | ├ó╦åÔÇª
 7c9eea03-257d-4144-a129-86d8f3cd24a7 | 04256b27-656e-42cd-b218-71d21867b1b0 | SensorReadingProduced | NatureProtector.Simulator.Host |      2 |            1 | 2020-09-13 10:00:00+00 | 2026-05-13 21:48:34.6661+00   | 2026-05-13 21:48:34.819742+00 | 2026-05-13 21:48:34.819742+00 | ├ó╦åÔÇª           | ├ó╦åÔÇª              | ├ó╦åÔÇª
 56e6f7d6-604a-474e-9b81-515fb7bb685f | f69095ff-d642-455d-a443-e3e6aa4e9904 | SensorReadingProduced | NatureProtector.Simulator.Host |      2 |            1 | 2020-09-13 10:00:00+00 | 2026-05-13 21:48:34.42306+00  | 2026-05-13 21:48:34.609959+00 | 2026-05-13 21:48:34.609959+00 | ├ó╦åÔÇª           | ├ó╦åÔÇª              | ├ó╦åÔÇª
 e7576bc6-ee6f-464c-8a77-99e6fabc8bd2 | 6e0af212-52a8-4136-b41a-966b05e865ac | SensorReadingProduced | NatureProtector.Simulator.Host |      2 |            1 | 2020-09-13 10:00:00+00 | 2026-05-13 21:48:33.628665+00 | 2026-05-13 21:48:34.36158+00  | 2026-05-13 21:48:34.36158+00  | ├ó╦åÔÇª           | ├ó╦åÔÇª              | ├ó╦åÔÇª
 5bb132cb-6d0e-4310-8e3d-44d1af4569ca | 1477c43e-314b-4f27-bdc0-45659fd0fa80 | SensorReadingProduced | NatureProtector.Simulator.Host |      2 |            1 | 2020-09-13 10:09:30+00 | 2026-05-12 11:12:04.923186+00 | 2026-05-12 11:12:04.978769+00 | 2026-05-12 11:12:04.978769+00 | ├ó╦åÔÇª           | ├ó╦åÔÇª              | ├ó╦åÔÇª
 054327fa-37d9-4840-84eb-38d89e5855cf | 60c1756b-4d53-49e9-a9de-6866a60fe34b | SensorReadingProduced | NatureProtector.Simulator.Host |      2 |            1 | 2020-09-13 10:09:30+00 | 2026-05-12 11:12:04.870537+00 | 2026-05-12 11:12:04.916432+00 | 2026-05-12 11:12:04.916432+00 | ├ó╦åÔÇª           | ├ó╦åÔÇª              | ├ó╦åÔÇª
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
 e9f6e5f1-3fb3-4404-845c-3fc8f6d329b3 | be8ca28e-ef2e-41dc-a5ce-79fea71baeeb |             1 | reading_risk_pipeline | 2026-05-13 21:50:04.209062+00 | 2026-05-13 21:50:04.298938+00 |       1 | ├ó╦åÔÇª       | ├ó╦åÔÇª
 3af742dd-1792-445a-b3c5-ec27b17a1e8e | 79863a31-e2b6-4540-ab08-fff9d0400aa5 |             1 | reading_risk_pipeline | 2026-05-13 21:50:04.149866+00 | 2026-05-13 21:50:04.1999+00   |       1 | ├ó╦åÔÇª       | ├ó╦åÔÇª
 db66410f-24cb-4071-b39d-0b023e50e612 | b0727ca2-f846-4466-a76d-a1461bcdd405 |             1 | reading_risk_pipeline | 2026-05-13 21:50:04.095686+00 | 2026-05-13 21:50:04.143399+00 |       1 | ├ó╦åÔÇª       | ├ó╦åÔÇª
 97bcaea3-71d4-41d8-9b8a-36f0757e0419 | b8a65ba3-16b0-47e2-9000-b60bb84e3ebd |             1 | reading_risk_pipeline | 2026-05-13 21:50:04.039967+00 | 2026-05-13 21:50:04.088203+00 |       1 | ├ó╦åÔÇª       | ├ó╦åÔÇª
 e88a872b-cf3c-4850-a21c-d51783b98c28 | 3d5753f0-77c7-4a88-9c17-3c9e0ac2990d |             1 | reading_risk_pipeline | 2026-05-13 21:50:03.974767+00 | 2026-05-13 21:50:04.033473+00 |       1 | ├ó╦åÔÇª       | ├ó╦åÔÇª
 e2dcc550-ca5b-40e7-81df-6e244d72c16c | 50b02aa6-0bbc-4a5f-a25a-d1532cd9d9f8 |             1 | reading_risk_pipeline | 2026-05-13 21:49:34.199642+00 | 2026-05-13 21:49:34.248807+00 |       1 | ├ó╦åÔÇª       | ├ó╦åÔÇª
 6101cfd4-f2e9-461a-ad9c-9ade57e9e63b | 7d7102d6-747f-4fce-9ff7-b5e92475b11b |             1 | reading_risk_pipeline | 2026-05-13 21:49:34.148296+00 | 2026-05-13 21:49:34.19258+00  |       1 | ├ó╦åÔÇª       | ├ó╦åÔÇª
 532cc117-be96-410b-a6b8-fd7a5bcd81ba | 8ce0c187-69c1-4f1b-9299-e54cf216cd98 |             1 | reading_risk_pipeline | 2026-05-13 21:49:34.092674+00 | 2026-05-13 21:49:34.142013+00 |       1 | ├ó╦åÔÇª       | ├ó╦åÔÇª
 0810af85-48de-4e6e-b7f7-1afeef74cda1 | 43c6077c-1982-4ad2-9d6f-47c1e5e29f8d |             1 | reading_risk_pipeline | 2026-05-13 21:49:34.038129+00 | 2026-05-13 21:49:34.085041+00 |       1 | ├ó╦åÔÇª       | ├ó╦åÔÇª
 8c76b61a-3aff-4cc5-b346-27c9e21193f2 | 6cb85ec4-1cf0-4cb1-8a2c-899aed187c08 |             1 | reading_risk_pipeline | 2026-05-13 21:49:33.989741+00 | 2026-05-13 21:49:34.032221+00 |       1 | ├ó╦åÔÇª       | ├ó╦åÔÇª
 640f24fc-400e-4272-b853-4700809532e6 | f3f53d79-824d-4b67-844b-c6b657612660 |             1 | reading_risk_pipeline | 2026-05-13 21:49:33.921928+00 | 2026-05-13 21:49:33.983112+00 |       1 | ├ó╦åÔÇª       | ├ó╦åÔÇª
 c7e9a4f2-ee58-47be-873b-94d968c54606 | c1f236cf-f7ad-4d52-91b0-85b12e731894 |             1 | reading_risk_pipeline | 2026-05-13 21:49:04.223514+00 | 2026-05-13 21:49:04.307483+00 |       1 | ├ó╦åÔÇª       | ├ó╦åÔÇª
 81c7db21-7729-43b7-8f80-f4105a20abdf | 57e2a35b-f6b4-40f1-b952-d9ff68abd6e3 |             1 | reading_risk_pipeline | 2026-05-13 21:49:04.129053+00 | 2026-05-13 21:49:04.214417+00 |       1 | ├ó╦åÔÇª       | ├ó╦åÔÇª
 a355d780-675e-4bc1-9b06-f4f8f18933c1 | 272ac392-9f50-4e01-b35e-2d9acf1ab9a7 |             1 | reading_risk_pipeline | 2026-05-13 21:49:04.034634+00 | 2026-05-13 21:49:04.121702+00 |       1 | ├ó╦åÔÇª       | ├ó╦åÔÇª
 60011744-bbcc-4fb6-be05-b39c5f6945e4 | 8294c98a-dd78-40d9-a343-48c057bab722 |             1 | reading_risk_pipeline | 2026-05-13 21:49:03.941757+00 | 2026-05-13 21:49:04.026659+00 |       1 | ├ó╦åÔÇª       | ├ó╦åÔÇª
 ed55bdd9-af0e-423c-ba2c-bc0c8b1dddc1 | 5302b820-b872-40d3-899c-3a4077276ba9 |             1 | reading_risk_pipeline | 2026-05-13 21:49:03.843199+00 | 2026-05-13 21:49:03.933806+00 |       1 | ├ó╦åÔÇª       | ├ó╦åÔÇª
 47ccb185-3c85-45e5-80ca-bc0a55c05d17 | 6f036683-e2ba-43f3-904c-e81910187342 |             1 | reading_risk_pipeline | 2026-05-13 21:49:03.734618+00 | 2026-05-13 21:49:03.827879+00 |       1 | ├ó╦åÔÇª       | ├ó╦åÔÇª
 cc3a996e-8c60-4667-b93c-a975f483c861 | ad7d0f8e-3716-46ba-ac26-d7a7354eef5b |             1 | reading_risk_pipeline | 2026-05-13 21:48:35.453487+00 | 2026-05-13 21:48:35.611163+00 |       1 | ├ó╦åÔÇª       | ├ó╦åÔÇª
 807b1ba0-0d65-4a8d-95a9-cb76b68f9500 | d6f4caef-b617-4657-8518-e4c366d44cda |             1 | reading_risk_pipeline | 2026-05-13 21:48:35.061838+00 | 2026-05-13 21:48:35.327887+00 |       1 | ├ó╦åÔÇª       | ├ó╦åÔÇª
 6a8de654-f80e-4450-8db3-af4bc9f51dca | 1baf4175-8d21-4dfa-bb40-7d27b54346b2 |             1 | reading_risk_pipeline | 2026-05-13 21:48:34.86847+00  | 2026-05-13 21:48:35.015833+00 |       1 | ├ó╦åÔÇª       | ├ó╦åÔÇª
 50621d75-dfc8-4f24-aee5-cfd7480c590d | 7c9eea03-257d-4144-a129-86d8f3cd24a7 |             1 | reading_risk_pipeline | 2026-05-13 21:48:34.6661+00   | 2026-05-13 21:48:34.819742+00 |       1 | ├ó╦åÔÇª       | ├ó╦åÔÇª
 ca2ef4f1-1ef9-4f76-8b82-7d8517ab000c | 56e6f7d6-604a-474e-9b81-515fb7bb685f |             1 | reading_risk_pipeline | 2026-05-13 21:48:34.42306+00  | 2026-05-13 21:48:34.609959+00 |       1 | ├ó╦åÔÇª       | ├ó╦åÔÇª
 bc9ab7b0-70f5-4f89-9de0-60686a16c17f | e7576bc6-ee6f-464c-8a77-99e6fabc8bd2 |             1 | reading_risk_pipeline | 2026-05-13 21:48:33.628665+00 | 2026-05-13 21:48:34.36158+00  |       1 | ├ó╦åÔÇª       | ├ó╦åÔÇª
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
              6243 |             6242 |                6134 |                      25 |                       1 |            4
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
 2026-04-11 11:38:58.563527+00 | 2026-05-13 21:50:04.233324+00 | 2020-09-13 10:00:00+00        | 2020-09-13 10:09:30+00       | 2026-05-13 21:50:04.290312+00 | 2026-04-17 13:42:58.79077+00
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
             6242 |            0.1 |           0.95 | 0.5106616469080437
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
 Low       |  1036 |       0.1 |       0.1
 Moderate  |  1265 |       0.3 |       0.4
 High      |  1808 |      0.65 |      0.65
 VeryHigh  |  2088 |       0.7 |       0.7
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
 39c0e02c-be06-493e-b001-c288efb8168d | b3f4fb84-bf17-5522-a5f3-70fd1212f381 | 17badc17-7920-17ac-c213-db1c12bedfb2 | 8a65d4e7-92f5-1b6f-fdb0-d60ec5707d72 | ca977823-c412-4a8f-80b5-885e50134693 | 2020-09-13 10:01:30+00 |       0.3 | Moderate  | Area=b3f4fb84-bf17-5522-a5f3-70fd1212f381; Sensor=17badc17-7920-17ac-c213-db1c12bedfb2; Event=ca977823-c412-4a8f-80b5-885e50134693; Metric=WindSpeed; Value=5,22; BaseRisk=0,30; AdjustedScore=0,30; C=1,00; I=1,00; EligibilityFactor=1,00; ParameterSet=Candidate Parameter Set V1.0 (non-calibrated).    | 2026-05-13 21:50:04.233324+00
 99e9b459-0d87-482c-8ba8-dba306199f2b | b3f4fb84-bf17-5522-a5f3-70fd1212f381 | e7660606-cadd-4212-e67b-574c09ef789a | 33d52e78-7801-cf3b-bc8b-c798db0d4068 | 2fbed137-c198-44b0-ba0c-392380230f54 | 2020-09-13 10:01:30+00 |       0.3 | Moderate  | Area=b3f4fb84-bf17-5522-a5f3-70fd1212f381; Sensor=e7660606-cadd-4212-e67b-574c09ef789a; Event=2fbed137-c198-44b0-ba0c-392380230f54; Metric=WindSpeed; Value=5,38; BaseRisk=0,30; AdjustedScore=0,30; C=1,00; I=1,00; EligibilityFactor=1,00; ParameterSet=Candidate Parameter Set V1.0 (non-calibrated).    | 2026-05-13 21:50:04.163504+00
 0897ab60-659c-43a8-b263-4d2316d6dc23 | b3f4fb84-bf17-5522-a5f3-70fd1212f381 | 8e6a3d8a-7e65-4fa5-8c51-fa4597b203b8 | 8a65d4e7-92f5-1b6f-fdb0-d60ec5707d72 | 8be46566-bdd0-4960-9f46-eef3054dc328 | 2020-09-13 10:01:30+00 |      0.65 | High      | Area=b3f4fb84-bf17-5522-a5f3-70fd1212f381; Sensor=8e6a3d8a-7e65-4fa5-8c51-fa4597b203b8; Event=8be46566-bdd0-4960-9f46-eef3054dc328; Metric=Temperature; Value=32,10; BaseRisk=0,65; AdjustedScore=0,65; C=1,00; I=1,00; EligibilityFactor=1,00; ParameterSet=Candidate Parameter Set V1.0 (non-calibrated). | 2026-05-13 21:50:04.107453+00
 8b19e270-5325-4ecd-b67c-e3554bd03149 | b3f4fb84-bf17-5522-a5f3-70fd1212f381 | 27bf9aad-094f-7dc6-45b2-129a8cc0522e | 33d52e78-7801-cf3b-bc8b-c798db0d4068 | 4381a0b5-ae0a-470f-ac25-30dcccf3bfbd | 2020-09-13 10:01:30+00 |      0.65 | High      | Area=b3f4fb84-bf17-5522-a5f3-70fd1212f381; Sensor=27bf9aad-094f-7dc6-45b2-129a8cc0522e; Event=4381a0b5-ae0a-470f-ac25-30dcccf3bfbd; Metric=Temperature; Value=32,41; BaseRisk=0,65; AdjustedScore=0,65; C=1,00; I=1,00; EligibilityFactor=1,00; ParameterSet=Candidate Parameter Set V1.0 (non-calibrated). | 2026-05-13 21:50:04.053456+00
 3c857af0-a7a1-4145-b70a-0be81336af71 | b3f4fb84-bf17-5522-a5f3-70fd1212f381 | 2f494582-4077-86f2-9707-780861949155 | 8a65d4e7-92f5-1b6f-fdb0-d60ec5707d72 | ba5e8872-06f9-48b2-8b83-752ac5a59d82 | 2020-09-13 10:01:30+00 |       0.7 | VeryHigh  | Area=b3f4fb84-bf17-5522-a5f3-70fd1212f381; Sensor=2f494582-4077-86f2-9707-780861949155; Event=ba5e8872-06f9-48b2-8b83-752ac5a59d82; Metric=Humidity; Value=21,54; BaseRisk=0,70; AdjustedScore=0,70; C=1,00; I=1,00; EligibilityFactor=1,00; ParameterSet=Candidate Parameter Set V1.0 (non-calibrated).    | 2026-05-13 21:50:03.989812+00
 1d17f695-5c89-4851-8187-bd797e57183e | b3f4fb84-bf17-5522-a5f3-70fd1212f381 | 17badc17-7920-17ac-c213-db1c12bedfb2 | 8a65d4e7-92f5-1b6f-fdb0-d60ec5707d72 | 4e6970b1-4ea7-4347-a401-39341f13fbac | 2020-09-13 10:01:00+00 |       0.3 | Moderate  | Area=b3f4fb84-bf17-5522-a5f3-70fd1212f381; Sensor=17badc17-7920-17ac-c213-db1c12bedfb2; Event=4e6970b1-4ea7-4347-a401-39341f13fbac; Metric=WindSpeed; Value=5,06; BaseRisk=0,30; AdjustedScore=0,30; C=1,00; I=1,00; EligibilityFactor=1,00; ParameterSet=Candidate Parameter Set V1.0 (non-calibrated).    | 2026-05-13 21:49:34.21126+00
 73ffbc1e-cf58-409f-84d4-4e639b02aaf4 | b3f4fb84-bf17-5522-a5f3-70fd1212f381 | e7660606-cadd-4212-e67b-574c09ef789a | 33d52e78-7801-cf3b-bc8b-c798db0d4068 | 9c71ccf1-338d-47d9-8680-ee6d7bc243e5 | 2020-09-13 10:01:00+00 |       0.1 | Low       | Area=b3f4fb84-bf17-5522-a5f3-70fd1212f381; Sensor=e7660606-cadd-4212-e67b-574c09ef789a; Event=9c71ccf1-338d-47d9-8680-ee6d7bc243e5; Metric=WindSpeed; Value=4,88; BaseRisk=0,10; AdjustedScore=0,10; C=1,00; I=1,00; EligibilityFactor=1,00; ParameterSet=Candidate Parameter Set V1.0 (non-calibrated).    | 2026-05-13 21:49:34.160618+00
 d70ac0c8-cb7d-425e-98d7-57faa007fbd1 | b3f4fb84-bf17-5522-a5f3-70fd1212f381 | 8e6a3d8a-7e65-4fa5-8c51-fa4597b203b8 | 8a65d4e7-92f5-1b6f-fdb0-d60ec5707d72 | 3918e686-a4aa-4612-8e70-1e4d22c3cfdc | 2020-09-13 10:01:00+00 |      0.65 | High      | Area=b3f4fb84-bf17-5522-a5f3-70fd1212f381; Sensor=8e6a3d8a-7e65-4fa5-8c51-fa4597b203b8; Event=3918e686-a4aa-4612-8e70-1e4d22c3cfdc; Metric=Temperature; Value=31,72; BaseRisk=0,65; AdjustedScore=0,65; C=1,00; I=1,00; EligibilityFactor=1,00; ParameterSet=Candidate Parameter Set V1.0 (non-calibrated). | 2026-05-13 21:49:34.103968+00
 d573c79e-7f56-4ee0-b6d7-dba541e54184 | b3f4fb84-bf17-5522-a5f3-70fd1212f381 | 27bf9aad-094f-7dc6-45b2-129a8cc0522e | 33d52e78-7801-cf3b-bc8b-c798db0d4068 | 682c7078-c088-40dc-8864-a90b0597ff1f | 2020-09-13 10:01:00+00 |      0.65 | High      | Area=b3f4fb84-bf17-5522-a5f3-70fd1212f381; Sensor=27bf9aad-094f-7dc6-45b2-129a8cc0522e; Event=682c7078-c088-40dc-8864-a90b0597ff1f; Metric=Temperature; Value=31,74; BaseRisk=0,65; AdjustedScore=0,65; C=1,00; I=1,00; EligibilityFactor=1,00; ParameterSet=Candidate Parameter Set V1.0 (non-calibrated). | 2026-05-13 21:49:34.051139+00
 878def97-ec55-4403-bdb5-c15d4917d829 | b3f4fb84-bf17-5522-a5f3-70fd1212f381 | 2f494582-4077-86f2-9707-780861949155 | 8a65d4e7-92f5-1b6f-fdb0-d60ec5707d72 | 5161644d-6344-4f11-8518-3252cf5ee38d | 2020-09-13 10:01:00+00 |       0.7 | VeryHigh  | Area=b3f4fb84-bf17-5522-a5f3-70fd1212f381; Sensor=2f494582-4077-86f2-9707-780861949155; Event=5161644d-6344-4f11-8518-3252cf5ee38d; Metric=Humidity; Value=23,00; BaseRisk=0,70; AdjustedScore=0,70; C=1,00; I=1,00; EligibilityFactor=1,00; ParameterSet=Candidate Parameter Set V1.0 (non-calibrated).    | 2026-05-13 21:49:34.001053+00
 5a3595de-7b65-488c-b85b-387de102713b | b3f4fb84-bf17-5522-a5f3-70fd1212f381 | 22702eee-75aa-3cf4-31ab-b689bfb9a8bc | 33d52e78-7801-cf3b-bc8b-c798db0d4068 | 88841ead-7e55-4c17-a5fb-59b9543fa0e0 | 2020-09-13 10:01:00+00 |       0.7 | VeryHigh  | Area=b3f4fb84-bf17-5522-a5f3-70fd1212f381; Sensor=22702eee-75aa-3cf4-31ab-b689bfb9a8bc; Event=88841ead-7e55-4c17-a5fb-59b9543fa0e0; Metric=Humidity; Value=22,76; BaseRisk=0,70; AdjustedScore=0,70; C=1,00; I=1,00; EligibilityFactor=1,00; ParameterSet=Candidate Parameter Set V1.0 (non-calibrated).    | 2026-05-13 21:49:33.943817+00
 c94cc6fd-e948-40c8-abe3-70198191622f | b3f4fb84-bf17-5522-a5f3-70fd1212f381 | 17badc17-7920-17ac-c213-db1c12bedfb2 | 8a65d4e7-92f5-1b6f-fdb0-d60ec5707d72 | e28c3d42-0184-4670-a09a-56d30eafc042 | 2020-09-13 10:00:30+00 |       0.1 | Low       | Area=b3f4fb84-bf17-5522-a5f3-70fd1212f381; Sensor=17badc17-7920-17ac-c213-db1c12bedfb2; Event=e28c3d42-0184-4670-a09a-56d30eafc042; Metric=WindSpeed; Value=4,49; BaseRisk=0,10; AdjustedScore=0,10; C=1,00; I=1,00; EligibilityFactor=1,00; ParameterSet=Candidate Parameter Set V1.0 (non-calibrated).    | 2026-05-13 21:49:04.241052+00
 9dccd3ac-3af8-4f02-a5b5-246741f1a8f0 | b3f4fb84-bf17-5522-a5f3-70fd1212f381 | e7660606-cadd-4212-e67b-574c09ef789a | 33d52e78-7801-cf3b-bc8b-c798db0d4068 | 77fb1e04-c101-4985-9063-29d2436a93bb | 2020-09-13 10:00:30+00 |       0.1 | Low       | Area=b3f4fb84-bf17-5522-a5f3-70fd1212f381; Sensor=e7660606-cadd-4212-e67b-574c09ef789a; Event=77fb1e04-c101-4985-9063-29d2436a93bb; Metric=WindSpeed; Value=4,39; BaseRisk=0,10; AdjustedScore=0,10; C=1,00; I=1,00; EligibilityFactor=1,00; ParameterSet=Candidate Parameter Set V1.0 (non-calibrated).    | 2026-05-13 21:49:04.144275+00
 cfd06030-bcfb-4852-a80c-8dac7ca78913 | b3f4fb84-bf17-5522-a5f3-70fd1212f381 | 8e6a3d8a-7e65-4fa5-8c51-fa4597b203b8 | 8a65d4e7-92f5-1b6f-fdb0-d60ec5707d72 | 947370fa-7abe-4cf9-bee6-bb675a2f1fdb | 2020-09-13 10:00:30+00 |      0.65 | High      | Area=b3f4fb84-bf17-5522-a5f3-70fd1212f381; Sensor=8e6a3d8a-7e65-4fa5-8c51-fa4597b203b8; Event=947370fa-7abe-4cf9-bee6-bb675a2f1fdb; Metric=Temperature; Value=31,39; BaseRisk=0,65; AdjustedScore=0,65; C=1,00; I=1,00; EligibilityFactor=1,00; ParameterSet=Candidate Parameter Set V1.0 (non-calibrated). | 2026-05-13 21:49:04.052041+00
 acbd477f-37b5-42c6-a2da-1b69300d63d5 | b3f4fb84-bf17-5522-a5f3-70fd1212f381 | 27bf9aad-094f-7dc6-45b2-129a8cc0522e | 33d52e78-7801-cf3b-bc8b-c798db0d4068 | ac47b042-20ef-405a-a685-e32a9fb51929 | 2020-09-13 10:00:30+00 |      0.65 | High      | Area=b3f4fb84-bf17-5522-a5f3-70fd1212f381; Sensor=27bf9aad-094f-7dc6-45b2-129a8cc0522e; Event=ac47b042-20ef-405a-a685-e32a9fb51929; Metric=Temperature; Value=31,77; BaseRisk=0,65; AdjustedScore=0,65; C=1,00; I=1,00; EligibilityFactor=1,00; ParameterSet=Candidate Parameter Set V1.0 (non-calibrated). | 2026-05-13 21:49:03.957144+00
 4eff8364-4bbb-46d1-bf79-78de49f8db08 | b3f4fb84-bf17-5522-a5f3-70fd1212f381 | 2f494582-4077-86f2-9707-780861949155 | 8a65d4e7-92f5-1b6f-fdb0-d60ec5707d72 | 2c130b49-c710-4c06-973e-a43879bc6afc | 2020-09-13 10:00:30+00 |       0.7 | VeryHigh  | Area=b3f4fb84-bf17-5522-a5f3-70fd1212f381; Sensor=2f494582-4077-86f2-9707-780861949155; Event=2c130b49-c710-4c06-973e-a43879bc6afc; Metric=Humidity; Value=24,15; BaseRisk=0,70; AdjustedScore=0,70; C=1,00; I=1,00; EligibilityFactor=1,00; ParameterSet=Candidate Parameter Set V1.0 (non-calibrated).    | 2026-05-13 21:49:03.861025+00
 f4fda980-6c9c-44ef-9c53-87dad99d7976 | b3f4fb84-bf17-5522-a5f3-70fd1212f381 | 22702eee-75aa-3cf4-31ab-b689bfb9a8bc | 33d52e78-7801-cf3b-bc8b-c798db0d4068 | d5a2fd2d-03b1-43bd-bbfa-c9f6ba6ab67e | 2020-09-13 10:00:30+00 |       0.7 | VeryHigh  | Area=b3f4fb84-bf17-5522-a5f3-70fd1212f381; Sensor=22702eee-75aa-3cf4-31ab-b689bfb9a8bc; Event=d5a2fd2d-03b1-43bd-bbfa-c9f6ba6ab67e; Metric=Humidity; Value=22,78; BaseRisk=0,70; AdjustedScore=0,70; C=1,00; I=1,00; EligibilityFactor=1,00; ParameterSet=Candidate Parameter Set V1.0 (non-calibrated).    | 2026-05-13 21:49:03.754396+00
 ae330a12-5316-4b23-8447-2fcc1c000a63 | b3f4fb84-bf17-5522-a5f3-70fd1212f381 | 17badc17-7920-17ac-c213-db1c12bedfb2 | 8a65d4e7-92f5-1b6f-fdb0-d60ec5707d72 | 054b2f21-1196-4ee6-8ea7-c1cff4cf8dd9 | 2020-09-13 10:00:00+00 |       0.1 | Low       | Area=b3f4fb84-bf17-5522-a5f3-70fd1212f381; Sensor=17badc17-7920-17ac-c213-db1c12bedfb2; Event=054b2f21-1196-4ee6-8ea7-c1cff4cf8dd9; Metric=WindSpeed; Value=3,71; BaseRisk=0,10; AdjustedScore=0,10; C=1,00; I=1,00; EligibilityFactor=1,00; ParameterSet=Candidate Parameter Set V1.0 (non-calibrated).    | 2026-05-13 21:48:35.509492+00
 03cc428c-165e-403a-9b81-c881ffc0d659 | b3f4fb84-bf17-5522-a5f3-70fd1212f381 | e7660606-cadd-4212-e67b-574c09ef789a | 33d52e78-7801-cf3b-bc8b-c798db0d4068 | 4095f571-1402-45bb-9f91-db2c28e46cae | 2020-09-13 10:00:00+00 |       0.1 | Low       | Area=b3f4fb84-bf17-5522-a5f3-70fd1212f381; Sensor=e7660606-cadd-4212-e67b-574c09ef789a; Event=4095f571-1402-45bb-9f91-db2c28e46cae; Metric=WindSpeed; Value=3,78; BaseRisk=0,10; AdjustedScore=0,10; C=1,00; I=1,00; EligibilityFactor=1,00; ParameterSet=Candidate Parameter Set V1.0 (non-calibrated).    | 2026-05-13 21:48:35.101382+00
 72bf89d0-e5b5-4792-bb6c-32eb96c61d4c | b3f4fb84-bf17-5522-a5f3-70fd1212f381 | 8e6a3d8a-7e65-4fa5-8c51-fa4597b203b8 | 8a65d4e7-92f5-1b6f-fdb0-d60ec5707d72 | 0feaff80-0aaf-42a2-8daf-ccb77cf48d07 | 2020-09-13 10:00:00+00 |      0.65 | High      | Area=b3f4fb84-bf17-5522-a5f3-70fd1212f381; Sensor=8e6a3d8a-7e65-4fa5-8c51-fa4597b203b8; Event=0feaff80-0aaf-42a2-8daf-ccb77cf48d07; Metric=Temperature; Value=31,17; BaseRisk=0,65; AdjustedScore=0,65; C=1,00; I=1,00; EligibilityFactor=1,00; ParameterSet=Candidate Parameter Set V1.0 (non-calibrated). | 2026-05-13 21:48:34.906238+00
 46fed163-c02c-42b6-8b2c-8e4288f4c4da | b3f4fb84-bf17-5522-a5f3-70fd1212f381 | 27bf9aad-094f-7dc6-45b2-129a8cc0522e | 33d52e78-7801-cf3b-bc8b-c798db0d4068 | 04256b27-656e-42cd-b218-71d21867b1b0 | 2020-09-13 10:00:00+00 |      0.65 | High      | Area=b3f4fb84-bf17-5522-a5f3-70fd1212f381; Sensor=27bf9aad-094f-7dc6-45b2-129a8cc0522e; Event=04256b27-656e-42cd-b218-71d21867b1b0; Metric=Temperature; Value=31,24; BaseRisk=0,65; AdjustedScore=0,65; C=1,00; I=1,00; EligibilityFactor=1,00; ParameterSet=Candidate Parameter Set V1.0 (non-calibrated). | 2026-05-13 21:48:34.705794+00
 c051002c-5d1d-42ec-ad1d-cb55b233d4d4 | b3f4fb84-bf17-5522-a5f3-70fd1212f381 | 2f494582-4077-86f2-9707-780861949155 | 8a65d4e7-92f5-1b6f-fdb0-d60ec5707d72 | f69095ff-d642-455d-a443-e3e6aa4e9904 | 2020-09-13 10:00:00+00 |       0.7 | VeryHigh  | Area=b3f4fb84-bf17-5522-a5f3-70fd1212f381; Sensor=2f494582-4077-86f2-9707-780861949155; Event=f69095ff-d642-455d-a443-e3e6aa4e9904; Metric=Humidity; Value=25,02; BaseRisk=0,70; AdjustedScore=0,70; C=1,00; I=1,00; EligibilityFactor=1,00; ParameterSet=Candidate Parameter Set V1.0 (non-calibrated).    | 2026-05-13 21:48:34.466365+00
 916641ee-e28b-4797-bf14-aac11095fd5d | b3f4fb84-bf17-5522-a5f3-70fd1212f381 | 22702eee-75aa-3cf4-31ab-b689bfb9a8bc | 33d52e78-7801-cf3b-bc8b-c798db0d4068 | 6e0af212-52a8-4136-b41a-966b05e865ac | 2020-09-13 10:00:00+00 |       0.7 | VeryHigh  | Area=b3f4fb84-bf17-5522-a5f3-70fd1212f381; Sensor=22702eee-75aa-3cf4-31ab-b689bfb9a8bc; Event=6e0af212-52a8-4136-b41a-966b05e865ac; Metric=Humidity; Value=24,18; BaseRisk=0,70; AdjustedScore=0,70; C=1,00; I=1,00; EligibilityFactor=1,00; ParameterSet=Candidate Parameter Set V1.0 (non-calibrated).    | 2026-05-13 21:48:33.953477+00
 c017d28a-a39c-4e50-9549-e8113b6da835 | b3f4fb84-bf17-5522-a5f3-70fd1212f381 | 17badc17-7920-17ac-c213-db1c12bedfb2 | 8a65d4e7-92f5-1b6f-fdb0-d60ec5707d72 | 1477c43e-314b-4f27-bdc0-45659fd0fa80 | 2020-09-13 10:09:30+00 |       0.1 | Low       | Area=b3f4fb84-bf17-5522-a5f3-70fd1212f381; Sensor=17badc17-7920-17ac-c213-db1c12bedfb2; Event=1477c43e-314b-4f27-bdc0-45659fd0fa80; Metric=WindSpeed; Value=3,85; BaseRisk=0,10; AdjustedScore=0,10; C=1,00; I=1,00; EligibilityFactor=1,00; ParameterSet=Candidate Parameter Set V1.0 (non-calibrated).    | 2026-05-12 11:12:04.933814+00
 73029c1d-e68a-42f7-83d8-f8bf84d520bb | b3f4fb84-bf17-5522-a5f3-70fd1212f381 | e7660606-cadd-4212-e67b-574c09ef789a | 33d52e78-7801-cf3b-bc8b-c798db0d4068 | 60c1756b-4d53-49e9-a9de-6866a60fe34b | 2020-09-13 10:09:30+00 |       0.1 | Low       | Area=b3f4fb84-bf17-5522-a5f3-70fd1212f381; Sensor=e7660606-cadd-4212-e67b-574c09ef789a; Event=60c1756b-4d53-49e9-a9de-6866a60fe34b; Metric=WindSpeed; Value=3,84; BaseRisk=0,10; AdjustedScore=0,10; C=1,00; I=1,00; EligibilityFactor=1,00; ParameterSet=Candidate Parameter Set V1.0 (non-calibrated).    | 2026-05-12 11:12:04.882251+00
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
 5739af5e-1709-4d14-90fd-cefb9ce0ed1c | b3f4fb84-bf17-5522-a5f3-70fd1212f381 | c03be3d5-1f70-15cb-2fc0-3c86a204a644 | ├ó╦åÔÇª             | 2020-09-13 10:01:30+00 | 0.44799999999999973 | Moderate           | Medium   | Aggregated from 75 assessments; 37 at High or above. |              75 | 2026-05-13 21:50:04.290312+00
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
 35e298e3-043b-42f9-86bd-135becebce67 | b3f4fb84-bf17-5522-a5f3-70fd1212f381 | 8a65d4e7-92f5-1b6f-fdb0-d60ec5707d72 | 17badc17-7920-17ac-c213-db1c12bedfb2 | 39c0e02c-be06-493e-b001-c288efb8168d | 2020-09-13 10:01:30+00 |       0.3 | Moderate  | Medium   | Area=b3f4fb84-bf17-5522-a5f3-70fd1212f381; Sensor=17badc17-7920-17ac-c213-db1c12bedfb2; Event=ca977823-c412-4a8f-80b5-885e50134693; Metric=WindSpeed; Value=5,22; BaseRisk=0,30; AdjustedScore=0,30; C=1,00; I=1,00; EligibilityFactor=1,00; ParameterSet=Candidate Parameter Set V1.0 (non-calibrated). | 2026-05-13 21:50:04.244849+00
 98b72b45-ab6b-45ab-ba9b-8c7cebc198e7 | b3f4fb84-bf17-5522-a5f3-70fd1212f381 | 33d52e78-7801-cf3b-bc8b-c798db0d4068 | e7660606-cadd-4212-e67b-574c09ef789a | 99e9b459-0d87-482c-8ba8-dba306199f2b | 2020-09-13 10:01:30+00 |       0.3 | Moderate  | Medium   | Area=b3f4fb84-bf17-5522-a5f3-70fd1212f381; Sensor=e7660606-cadd-4212-e67b-574c09ef789a; Event=2fbed137-c198-44b0-ba0c-392380230f54; Metric=WindSpeed; Value=5,38; BaseRisk=0,30; AdjustedScore=0,30; C=1,00; I=1,00; EligibilityFactor=1,00; ParameterSet=Candidate Parameter Set V1.0 (non-calibrated). | 2026-05-13 21:50:04.16769+00
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
 b3f4fb84-bf17-5522-a5f3-70fd1212f381 | 0.44799999999999973 | Moderate           | Medium        | 2020-09-13 10:01:30+00 | 2026-05-13 21:50:04.290312+00 | area-risk-high | High           | Resolved     | Area risk is High with score 0,50. | 2020-09-13 10:01:45+00 | 2026-04-17 13:42:58.79077+00
 b3f4fb84-bf17-5522-a5f3-70fd1212f381 | 0.44799999999999973 | Moderate           | Medium        | 2020-09-13 10:01:30+00 | 2026-05-13 21:50:04.290312+00 | area-risk-high | High           | Resolved     | Area risk is High with score 0,50. | 2020-09-13 10:01:25+00 | 2026-04-17 13:08:10.878331+00
 b3f4fb84-bf17-5522-a5f3-70fd1212f381 | 0.44799999999999973 | Moderate           | Medium        | 2020-09-13 10:01:30+00 | 2026-05-13 21:50:04.290312+00 | area-risk-high | High           | Resolved     | Area risk is High with score 0,50. | 2020-09-13 10:00:10+00 | 2026-04-11 16:50:56.671844+00
 b3f4fb84-bf17-5522-a5f3-70fd1212f381 | 0.44799999999999973 | Moderate           | Medium        | 2020-09-13 10:01:30+00 | 2026-05-13 21:50:04.290312+00 | area-risk-high | High           | Resolved     | Area risk is High with score 0,50. | 2020-09-13 10:00:00+00 | 2026-04-11 13:44:42.698509+00
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
    "snapshotTimestamp":  "2020-09-13T10:01:30+00:00",
    "aggregateRiskScore":  0.44799999999999973,
    "aggregateRiskLevel":  "Moderate",
    "severity":  "Medium",
    "summary":  "Aggregated from 75 assessments; 37 at High or above.",
    "assessmentCount":  75,
    "updatedAt":  "2026-05-13T21:50:04.290312+00:00",
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
