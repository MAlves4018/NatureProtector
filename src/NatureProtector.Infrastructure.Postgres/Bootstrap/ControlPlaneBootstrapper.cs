using System.Globalization;
using System.Diagnostics;
using System.Diagnostics.Metrics;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using NatureProtector.Core.Scenarios;
using NatureProtector.Core.Sensors;
using NatureProtector.Infrastructure.Postgres.Control;
using NatureProtector.Infrastructure.Postgres.Persistence;
using NatureProtector.Infrastructure.Postgres.Users;
using NatureProtector.Shared.Observability;

namespace NatureProtector.Infrastructure.Postgres.Bootstrap;

/*
 * Este bootstrapper materializa no control plane PostgreSQL a baseline usada
 * pelo projeto para a área piloto.
 *
 * Rationale:
 * - O control plane precisa de ser povoado a partir dos artefactos versionados
 *   no repositório para que a runtime seja reproduzível.
 * - A lógica de bootstrap deve ficar separada do `Program` para manter o fluxo
 *   legível e testável.
 *
 * Design considerations:
 * - A operação é maioritariamente idempotente e usa GUIDs determinísticos para
 *   evitar duplicações de entidades estáveis.
 * - O bootstrap percorre uma cadeia completa: configuração, datasets, área,
 *   grelha, perfis, sensores, cenários e bindings.
 * - O código assume a convenção de caminhos do repositório e falha cedo quando
 *   um artefacto obrigatório não existe.
 */

public sealed class ControlPlaneBootstrapper
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private readonly NatureProtectorControlDbContext _dbContext;
    private readonly string _repoRoot;
    private readonly bool _skipSchemaMigration;

    public ControlPlaneBootstrapper(
        NatureProtectorControlDbContext dbContext,
        string repoRoot,
        bool skipSchemaMigration = false)
    {
        _dbContext = dbContext;
        _repoRoot = repoRoot;
        _skipSchemaMigration = skipSchemaMigration;
    }

    /// <summary>
    /// Materializa a baseline da área piloto e devolve um resumo do que foi
    /// importado.
    /// </summary>
    public async Task<ControlPlaneBootstrapSummary> BootstrapPilotAreaAsync(CancellationToken cancellationToken = default)
    {
        using var activity = PostgresBootstrapTelemetry.ActivitySource.StartActivity("natureprotector.bootstrap.run");
        var stopwatch = Stopwatch.StartNew();
        PostgresBootstrapTelemetry.BootstrapRuns.Add(1);

        if (!_skipSchemaMigration)
        {
            await ExecuteStepAsync("ensure_schema", _ => EnsureSchemaAsync(cancellationToken), cancellationToken);
        }

        var configuration = await ExecuteStepAsync("upsert_configuration_version", _ => UpsertConfigurationVersionAsync(cancellationToken), cancellationToken);
        await _dbContext.SaveChangesAsync(cancellationToken);

        var areaId = ResolveAreaGuid();
        var artifactsByPath = await ExecuteStepAsync("upsert_dataset_artifacts", _ => UpsertDatasetArtifactsAsync(cancellationToken), cancellationToken, artifactsByPath => artifactsByPath.Count);
        await _dbContext.SaveChangesAsync(cancellationToken);

        var area = await ExecuteStepAsync("upsert_pilot_area", _ => UpsertPilotAreaAsync(configuration.Id, areaId, cancellationToken), cancellationToken);
        await _dbContext.SaveChangesAsync(cancellationToken);

        var gridCellCount = await ExecuteStepAsync("upsert_grid_cells", _ => UpsertGridCellsAsync(configuration.Id, area.Id, cancellationToken), cancellationToken, count => count);
        await _dbContext.SaveChangesAsync(cancellationToken);

        var sensorProfileCount = await ExecuteStepAsync("upsert_sensor_profiles", _ => UpsertSensorProfilesAsync(configuration.Id, cancellationToken), cancellationToken, count => count);
        await _dbContext.SaveChangesAsync(cancellationToken);

        var sensorNodeCount = await ExecuteStepAsync("upsert_sensor_network_and_nodes", _ => UpsertSensorNetworkAndNodesAsync(configuration.Id, area.Id, cancellationToken), cancellationToken, count => count);
        await _dbContext.SaveChangesAsync(cancellationToken);

        var scenarioCount = await ExecuteStepAsync("upsert_scenarios", _ => UpsertScenariosAsync(configuration.Id, area.Id, artifactsByPath, cancellationToken), cancellationToken, count => count);
        await _dbContext.SaveChangesAsync(cancellationToken);

        var bindingCount = await _dbContext.ScenarioDatasetBindings.CountAsync(cancellationToken);
        var datasetArtifactCount = await _dbContext.DatasetArtifacts.CountAsync(cancellationToken);
        activity?.SetTag(TelemetryTags.ConfigurationVersion, configuration.VersionNumber);
        activity?.SetTag(TelemetryTags.AreaId, area.Id);
        activity?.SetTag(TelemetryTags.Outcome, "completed");
        PostgresBootstrapTelemetry.UpsertRows.Record(bindingCount, new TagList
        {
            { TelemetryTags.Host, PostgresBootstrapTelemetry.ServiceName },
            { TelemetryTags.Operation, "count_scenario_dataset_bindings" }
        });
        PostgresBootstrapTelemetry.UpsertRows.Record(datasetArtifactCount, new TagList
        {
            { TelemetryTags.Host, PostgresBootstrapTelemetry.ServiceName },
            { TelemetryTags.Operation, "count_dataset_artifacts" }
        });
        stopwatch.Stop();
        PostgresBootstrapTelemetry.BootstrapDurationMs.Record(stopwatch.Elapsed.TotalMilliseconds, new TagList
        {
            { TelemetryTags.Host, PostgresBootstrapTelemetry.ServiceName },
            { TelemetryTags.Outcome, "completed" }
        });

        return new ControlPlaneBootstrapSummary(
            configuration.VersionNumber,
            area.Code,
            area.Name,
            gridCellCount,
            sensorProfileCount,
            sensorNodeCount,
            scenarioCount,
            datasetArtifactCount,
            bindingCount);
    }

    private async Task ExecuteStepAsync(string operationName, Func<Activity?, Task> action, CancellationToken cancellationToken)
    {
        using var activity = PostgresBootstrapTelemetry.ActivitySource.StartActivity($"natureprotector.bootstrap.{operationName}");
        var stopwatch = Stopwatch.StartNew();
        activity?.SetTag(TelemetryTags.Operation, operationName);

        await action(activity);

        stopwatch.Stop();
        PostgresBootstrapTelemetry.UpsertOperations.Add(1, new TagList
        {
            { TelemetryTags.Host, PostgresBootstrapTelemetry.ServiceName },
            { TelemetryTags.Operation, operationName }
        });
        PostgresBootstrapTelemetry.UpsertDurationMs.Record(stopwatch.Elapsed.TotalMilliseconds, new TagList
        {
            { TelemetryTags.Host, PostgresBootstrapTelemetry.ServiceName },
            { TelemetryTags.Operation, operationName },
            { TelemetryTags.Outcome, "completed" }
        });
    }

    private async Task<T> ExecuteStepAsync<T>(
        string operationName,
        Func<Activity?, Task<T>> action,
        CancellationToken cancellationToken,
        Func<T, long>? rowCountSelector = null)
    {
        using var activity = PostgresBootstrapTelemetry.ActivitySource.StartActivity($"natureprotector.bootstrap.{operationName}");
        var stopwatch = Stopwatch.StartNew();
        activity?.SetTag(TelemetryTags.Operation, operationName);

        var result = await action(activity);

        stopwatch.Stop();
        PostgresBootstrapTelemetry.UpsertOperations.Add(1, new TagList
        {
            { TelemetryTags.Host, PostgresBootstrapTelemetry.ServiceName },
            { TelemetryTags.Operation, operationName }
        });
        PostgresBootstrapTelemetry.UpsertDurationMs.Record(stopwatch.Elapsed.TotalMilliseconds, new TagList
        {
            { TelemetryTags.Host, PostgresBootstrapTelemetry.ServiceName },
            { TelemetryTags.Operation, operationName },
            { TelemetryTags.Outcome, "completed" }
        });

        if (rowCountSelector is not null)
        {
            PostgresBootstrapTelemetry.UpsertRows.Record(rowCountSelector(result), new TagList
            {
                { TelemetryTags.Host, PostgresBootstrapTelemetry.ServiceName },
                { TelemetryTags.Operation, operationName }
            });
        }

        return result;
    }

    /// <summary>
    /// Garante que o esquema PostgreSQL existe antes do bootstrap de dados.
    /// </summary>
    private async Task EnsureSchemaAsync(CancellationToken cancellationToken)
    {
        var migrations = _dbContext.Database.GetMigrations();

        if (migrations.Any())
        {
            await _dbContext.Database.MigrateAsync(cancellationToken);
            return;
        }

        await _dbContext.Database.EnsureCreatedAsync(cancellationToken);
    }

    /// <summary>
    /// Cria ou atualiza a versão de configuração usada pela baseline.
    /// </summary>
    private async Task<ConfigurationVersionRecord> UpsertConfigurationVersionAsync(CancellationToken cancellationToken)
    {
        var versionNumber = 1;
        var existing = await _dbContext.ConfigurationVersions
            .SingleOrDefaultAsync(entity => entity.VersionNumber == versionNumber, cancellationToken);

        if (existing is null)
        {
            existing = new ConfigurationVersionRecord
            {
                Id = DeterministicGuid.FromString("configuration-version", "control:v1"),
                VersionNumber = versionNumber
            };

            _dbContext.ConfigurationVersions.Add(existing);
        }

        existing.Description = "Bootstrap control-plane import for Proenca-a-Nova with pilot sensor network.";
        existing.IsActive = true;
        existing.CreatedAt = DateTimeOffset.UtcNow;
        existing.CreatedBy = "phase-04-bootstrap";

        return existing;
    }

    /// <summary>
    /// Indexa os artefactos de dados declarados no manifesto de datasets.
    /// </summary>
    private async Task<Dictionary<string, DatasetArtifactRecord>> UpsertDatasetArtifactsAsync(CancellationToken cancellationToken)
    {
        var manifestPath = GetRequiredFilePath("data/manifests/datasets/proenca-a-nova-dataset-plan.json");
        using var document = JsonDocument.Parse(await File.ReadAllTextAsync(manifestPath, Encoding.UTF8, cancellationToken));

        var root = document.RootElement;
        var areaCode = root.GetProperty("area_id").GetString() ?? "proenca-a-nova";
        var version = root.GetProperty("version").GetString() ?? "0.1.0";

        var existing = await _dbContext.DatasetArtifacts
            .Where(entity => entity.AreaCode == areaCode && entity.Version == version)
            .ToDictionaryAsync(entity => NormalizePath(entity.RelativePath), cancellationToken);

        foreach (var dataset in root.GetProperty("datasets").EnumerateArray())
        {
            var datasetId = dataset.GetProperty("id").GetString() ?? "unknown";
            var sourceName = dataset.GetProperty("source_name").GetString() ?? datasetId;
            var sourceUrl = dataset.TryGetProperty("source_url", out var sourceUrlElement)
                ? sourceUrlElement.GetString()
                : null;
            var status = dataset.TryGetProperty("status", out var statusElement) ? statusElement.GetString() : null;
            var notes = dataset.TryGetProperty("notes", out var notesElement) ? notesElement.GetString() : null;
            var rawTarget = dataset.TryGetProperty("raw_target", out var rawTargetElement) ? rawTargetElement.GetString() : null;

            foreach (var output in dataset.GetProperty("curated_outputs").EnumerateArray())
            {
                var relativePath = NormalizePath(output.GetString() ?? string.Empty);
                if (string.IsNullOrWhiteSpace(relativePath))
                {
                    continue;
                }

                if (!existing.TryGetValue(relativePath, out var artifact))
                {
                    artifact = new DatasetArtifactRecord
                    {
                        Id = DeterministicGuid.FromString("dataset-artifact", relativePath)
                    };

                    _dbContext.DatasetArtifacts.Add(artifact);
                    existing[relativePath] = artifact;
                }

                artifact.DatasetCode = BuildDatasetCode(datasetId, relativePath);
                artifact.DatasetType = InferDatasetType(relativePath);
                artifact.SourceName = sourceName;
                artifact.SourceUrl = sourceUrl;
                artifact.AreaCode = areaCode;
                artifact.Version = version;
                artifact.Format = Path.GetExtension(relativePath).TrimStart('.').ToLowerInvariant();
                artifact.RelativePath = relativePath;
                artifact.MetadataJson = JsonSerializer.Serialize(new
                {
                    status,
                    notes,
                    raw_target = rawTarget
                }, JsonOptions);
            }
        }

        return existing;
    }

    /// <summary>
    /// Cria ou atualiza a área piloto a partir da baseline geoespacial.
    /// </summary>
    private async Task<AreaRecord> UpsertPilotAreaAsync(
        Guid configurationVersionId,
        Guid areaId,
        CancellationToken cancellationToken)
    {
        var areaPath = GetRequiredFilePath("data/baseline/areas/proenca-a-nova/area.geojson");
        var baselineManifestPath = GetRequiredFilePath("data/baseline/areas/proenca-a-nova/manifest.json");

        using var areaDocument = JsonDocument.Parse(await File.ReadAllTextAsync(areaPath, Encoding.UTF8, cancellationToken));
        using var baselineDocument = JsonDocument.Parse(await File.ReadAllTextAsync(baselineManifestPath, Encoding.UTF8, cancellationToken));

        var feature = areaDocument.RootElement.GetProperty("features")[0];
        var properties = feature.GetProperty("properties");
        var areaCode = properties.GetProperty("area_id").GetString() ?? "proenca-a-nova";
        var name = properties.GetProperty("municipio").GetString() ?? areaCode;

        var area = await _dbContext.Areas.SingleOrDefaultAsync(entity => entity.Id == areaId, cancellationToken)
            ?? await _dbContext.Areas.SingleOrDefaultAsync(
                entity => entity.ConfigurationVersionId == configurationVersionId && entity.Code == areaCode,
                cancellationToken);

        if (area is null)
        {
            area = new AreaRecord
            {
                Id = areaId
            };

            _dbContext.Areas.Add(area);
        }

        area.ConfigurationVersionId = configurationVersionId;
        area.Code = areaCode;
        area.Name = name;
        area.CountryCode = "PT";
        area.GeometryGeoJson = feature.GetProperty("geometry").GetRawText();
        area.MetadataJson = JsonSerializer.Serialize(new
        {
            properties = JsonSerializer.Deserialize<object>(properties.GetRawText(), JsonOptions),
            baseline_manifest = JsonSerializer.Deserialize<object>(baselineDocument.RootElement.GetRawText(), JsonOptions)
        }, JsonOptions);

        return area;
    }

    /// <summary>
    /// Cria ou atualiza as células da grelha base da área piloto.
    /// </summary>
    private async Task<int> UpsertGridCellsAsync(
        Guid configurationVersionId,
        Guid areaId,
        CancellationToken cancellationToken)
    {
        var gridGeometryByCellId = await LoadGridCellGeometriesAsync(cancellationToken);
        var rows = await LoadCsvRowsAsync("data/baseline/areas/proenca-a-nova/cells_attributes.csv", cancellationToken);
        var existing = await _dbContext.GridCells
            .Where(entity => entity.AreaId == areaId && entity.ConfigurationVersionId == configurationVersionId)
            .ToDictionaryAsync(entity => entity.CellCode, cancellationToken);

        foreach (var row in rows)
        {
            var cellCode = row["cell_id"];

            if (!existing.TryGetValue(cellCode, out var cell))
            {
                cell = new GridCellRecord
                {
                    Id = DeterministicGuid.FromString("grid-cell", $"{row["area_id"]}:{cellCode}")
                };

                _dbContext.GridCells.Add(cell);
                existing[cellCode] = cell;
            }

            cell.AreaId = areaId;
            cell.ConfigurationVersionId = configurationVersionId;
            cell.CellCode = cellCode;
            cell.CentroidLatitude = ParseDouble(row["centroid_lat"]) ?? 0d;
            cell.CentroidLongitude = ParseDouble(row["centroid_lon"]) ?? 0d;
            cell.PolygonGeoJson = gridGeometryByCellId.TryGetValue(cellCode, out var geometry) ? geometry : null;
            cell.AltitudeMeters = ParseDouble(row["altitude_m"]);
            cell.SlopeDegrees = ParseDouble(row["slope_deg"]);
            cell.AspectDegrees = ParseDouble(row["aspect_deg"]);
            cell.LandCoverClass = NullIfEmpty(row["land_cover_class"]);
            cell.DominantForestType = NullIfEmpty(row["dominant_forest_type"]);
            cell.DominantFuelModel = NullIfEmpty(row["dominant_fuel_model"]);
            cell.TreeCoverDensity = ParseDouble(row["tree_cover_density"]);
            cell.StructuralHazard = NullIfEmpty(row["structural_hazard"]);
            cell.ConjuncturalHazard = NullIfEmpty(row["conjunctural_hazard"]);
        }

        return rows.Count;
    }

    /// <summary>
    /// Regista os perfis de sensores usados pela rede piloto.
    /// </summary>
    private async Task<int> UpsertSensorProfilesAsync(
        Guid configurationVersionId,
        CancellationToken cancellationToken)
    {
        var definitions = new[]
        {
            new PilotSensorProfileDefinition(
                DeterministicGuid.FromString("sensor-profile", "pilot:temperature"),
                "pilot-temperature-default",
                "temperature-field",
                """{"bias_celsius":0.3,"resolution":0.1}""",
                """{"noise_level":0.08}""",
                """{"latency_profile":"Low latency","failure_profile":"Rare failures"}""",
                """{"sampling_interval_seconds":5,"communication_mode":"RabbitMq"}"""),
            new PilotSensorProfileDefinition(
                DeterministicGuid.FromString("sensor-profile", "pilot:humidity"),
                "pilot-humidity-default",
                "humidity-field",
                """{"bias_percent":1.5,"resolution":0.1}""",
                """{"noise_level":0.09}""",
                """{"latency_profile":"Low latency","failure_profile":"Rare failures"}""",
                """{"sampling_interval_seconds":5,"communication_mode":"RabbitMq"}"""),
            new PilotSensorProfileDefinition(
                DeterministicGuid.FromString("sensor-profile", "pilot:wind"),
                "pilot-wind-default",
                "wind-field",
                """{"bias_ms":0.2,"resolution":0.1}""",
                """{"noise_level":0.10}""",
                """{"latency_profile":"Moderate latency","failure_profile":"Occasional faults"}""",
                """{"sampling_interval_seconds":5,"communication_mode":"RabbitMq"}""")
        };

        var existing = await _dbContext.SensorProfiles
            .Where(entity => entity.ConfigurationVersionId == configurationVersionId)
            .ToDictionaryAsync(entity => entity.Name, cancellationToken);

        foreach (var definition in definitions)
        {
            if (!existing.TryGetValue(definition.Name, out var profile))
            {
                profile = new SensorProfileRecord
                {
                    Id = definition.Id
                };

                _dbContext.SensorProfiles.Add(profile);
                existing[definition.Name] = profile;
            }

            profile.ConfigurationVersionId = configurationVersionId;
            profile.Name = definition.Name;
            profile.SensorFamily = definition.SensorFamily;
            profile.AccuracyProfileJson = definition.AccuracyJson;
            profile.NoiseProfileJson = definition.NoiseJson;
            profile.FaultProfileJson = definition.FaultsJson;
            profile.PublicationPolicyJson = definition.PublicationJson;
        }

        return definitions.Length;
    }

    /// <summary>
    /// Cria a rede piloto e os sensores ativos associados às células escolhidas.
    /// </summary>
    private async Task<int> UpsertSensorNetworkAndNodesAsync(
        Guid configurationVersionId,
        Guid areaId,
        CancellationToken cancellationToken)
    {
        var networkName = "proenca-a-nova-pilot-network";
        var network = await _dbContext.SensorNetworks
            .SingleOrDefaultAsync(
                entity => entity.ConfigurationVersionId == configurationVersionId && entity.Name == networkName,
                cancellationToken);

        if (network is null)
        {
            network = new SensorNetworkRecord
            {
                Id = DeterministicGuid.FromString("sensor-network", networkName)
            };

            _dbContext.SensorNetworks.Add(network);
        }

        network.ConfigurationVersionId = configurationVersionId;
        network.Name = networkName;

        await _dbContext.SaveChangesAsync(cancellationToken);

        var profileIds = await _dbContext.SensorProfiles
            .Where(entity => entity.ConfigurationVersionId == configurationVersionId)
            .ToDictionaryAsync(entity => entity.Name, entity => entity.Id, cancellationToken);

        var selectedCells = await SelectPilotSensorCellsAsync(areaId, cancellationToken);
        var existingNodes = await _dbContext.SensorNodes
            .Where(entity => entity.AreaId == areaId && entity.ConfigurationVersionId == configurationVersionId)
            .ToDictionaryAsync(entity => entity.Name, cancellationToken);
        
        var desiredSensorNames = selectedCells
            .SelectMany(GetPilotSensorTemplates)
            .Select(template => template.Name)
            .ToHashSet(StringComparer.Ordinal);
        
        foreach (var existingNode in existingNodes.Values.Where(existingNode => !desiredSensorNames.Contains(existingNode.Name)))
        {
            existingNode.IsActive = false;
        }
        
        foreach (var cell in selectedCells)
        {
            foreach (var sensorTemplate in GetPilotSensorTemplates(cell))
            {
                if (!existingNodes.TryGetValue(sensorTemplate.Name, out var node))
                {
                    node = new SensorNodeRecord
                    {
                        Id = DeterministicGuid.FromString("sensor-node", sensorTemplate.Name)
                    };

                    _dbContext.SensorNodes.Add(node);
                    existingNodes[sensorTemplate.Name] = node;
                }

                node.AreaId = areaId;
                node.GridCellId = cell.Id;
                node.ProfileId = profileIds[sensorTemplate.ProfileName];
                node.ConfigurationVersionId = configurationVersionId;
                node.NetworkId = network.Id;
                node.Name = sensorTemplate.Name;
                node.Type = sensorTemplate.Type;
                node.Latitude = cell.CentroidLatitude;
                node.Longitude = cell.CentroidLongitude;
                node.AltitudeMeters = cell.AltitudeMeters;
                node.IsActive = true;
                node.InstallationProfile = sensorTemplate.InstallationProfile;
            }
        }

        return selectedCells.Count * 3;
    }

    /// <summary>
    /// Materializa os cenários gerados e as suas ligações aos datasets.
    /// </summary>
    private async Task<int> UpsertScenariosAsync(
        Guid configurationVersionId,
        Guid areaId,
        IReadOnlyDictionary<string, DatasetArtifactRecord> artifactsByPath,
        CancellationToken cancellationToken)
    {
        var catalogPath = GetRequiredFilePath("data/manifests/scenarios/proenca-a-nova-scenarios.generated.json");
        using var catalogDocument = JsonDocument.Parse(await File.ReadAllTextAsync(catalogPath, Encoding.UTF8, cancellationToken));

        var scenarios = catalogDocument.RootElement.GetProperty("scenarios").EnumerateArray().ToList();
        var scenarioIds = scenarios
            .Select(scenario => Guid.Parse(scenario.GetProperty("scenario_id").GetString()!))
            .ToArray();

        var existing = await _dbContext.ScenarioDefinitions
            .Where(entity => entity.AreaId == areaId)
            .ToDictionaryAsync(entity => entity.Code, cancellationToken);

        foreach (var scenario in scenarios)
        {
            var code = scenario.GetProperty("scenario_key").GetString() ?? throw new InvalidOperationException("Scenario key is required.");

            if (!existing.TryGetValue(code, out var definition))
            {
                definition = new ScenarioDefinitionRecord
                {
                    Id = Guid.Parse(scenario.GetProperty("scenario_id").GetString()!)
                };

                _dbContext.ScenarioDefinitions.Add(definition);
                existing[code] = definition;
            }

            definition.AreaId = areaId;
            definition.ConfigurationVersionId = configurationVersionId;
            definition.Code = code;
            definition.Name = scenario.GetProperty("simulator_options").GetProperty("ScenarioName").GetString() ?? code;
            definition.Description = scenario.GetProperty("simulator_options").GetProperty("ScenarioDescription").GetString();
            definition.ScenarioKind = ParseScenarioCategory(scenario.GetProperty("scenario_category").GetString());
            definition.BaseScenarioId = scenario.TryGetProperty("base_scenario_id", out var baseScenario)
                ? Guid.Parse(baseScenario.GetString()!)
                : null;
            definition.ParametersJson = scenario.GetRawText();
        }

        await UpsertScenarioBindingsAsync(scenarios, artifactsByPath, cancellationToken);
        return scenarioIds.Length;
    }

    private async Task UpsertScenarioBindingsAsync(
        IReadOnlyList<JsonElement> scenarios,
        IReadOnlyDictionary<string, DatasetArtifactRecord> artifactsByPath,
        CancellationToken cancellationToken)
    {
        var existing = await _dbContext.ScenarioDatasetBindings
            .ToDictionaryAsync(
                entity => BuildBindingKey(entity.ScenarioId, entity.DatasetArtifactId, entity.BindingRole),
                cancellationToken);

        var catalogPath = NormalizePath("data/manifests/scenarios/proenca-a-nova-scenarios.generated.json");

        foreach (var scenario in scenarios)
        {
            var scenarioId = Guid.Parse(scenario.GetProperty("scenario_id").GetString()!);
            var scenarioKey = scenario.GetProperty("scenario_key").GetString() ?? throw new InvalidOperationException("Scenario key is required.");

            AddBinding(existing, artifactsByPath, scenarioId, catalogPath, "scenario_catalog", "Generated A/B/C catalog.");

            var manifestPath = scenarioKey switch
            {
                "scenario_a" => "data/manifests/scenarios/proenca-a-nova/scenario_a.base.json",
                "scenario_b" => "data/manifests/scenarios/proenca-a-nova/scenario_b.high-risk.json",
                "scenario_c" => "data/manifests/scenarios/proenca-a-nova/scenario_c.degraded-pipeline.json",
                _ => throw new InvalidOperationException($"Unsupported scenario key '{scenarioKey}'.")
            };

            AddBinding(existing, artifactsByPath, scenarioId, manifestPath, "scenario_manifest", "Scenario manifest used by Simulator.Host.");

            var sourceDataset = scenario.GetProperty("source_context").GetProperty("source_dataset").GetString();
            var selectionReferencePath = sourceDataset switch
            {
                "open_meteo_historical_api" => "data/baseline/areas/proenca-a-nova/weather_daily_reference.parquet",
                "PT-FireSprd_v2.0" => "data/baseline/areas/proenca-a-nova/scenario_candidates.parquet",
                _ => null
            };

            if (selectionReferencePath is not null)
            {
                AddBinding(existing, artifactsByPath, scenarioId, selectionReferencePath, "selection_reference", $"Primary dataset used to select {scenarioKey}.");
            }
        }
    }

    private async Task<Dictionary<string, string>> LoadGridCellGeometriesAsync(CancellationToken cancellationToken)
    {
        var gridPath = GetRequiredFilePath("data/baseline/areas/proenca-a-nova/grid_1km.geojson");
        using var document = JsonDocument.Parse(await File.ReadAllTextAsync(gridPath, Encoding.UTF8, cancellationToken));
        var dictionary = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        foreach (var feature in document.RootElement.GetProperty("features").EnumerateArray())
        {
            var cellCode = feature.GetProperty("properties").GetProperty("cell_id").GetString();

            if (!string.IsNullOrWhiteSpace(cellCode))
            {
                dictionary[cellCode] = feature.GetProperty("geometry").GetRawText();
            }
        }

        return dictionary;
    }

    private async Task<List<Dictionary<string, string>>> LoadCsvRowsAsync(string relativePath, CancellationToken cancellationToken)
    {
        var filePath = GetRequiredFilePath(relativePath);
        var lines = await File.ReadAllLinesAsync(filePath, Encoding.UTF8, cancellationToken);

        if (lines.Length == 0)
        {
            return [];
        }

        var headers = ParseCsvLine(lines[0]);
        var rows = new List<Dictionary<string, string>>();

        foreach (var line in lines.Skip(1))
        {
            if (string.IsNullOrWhiteSpace(line))
            {
                continue;
            }

            var values = ParseCsvLine(line);
            var row = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            for (var index = 0; index < headers.Count; index++)
            {
                row[headers[index]] = index < values.Count ? values[index] : string.Empty;
            }

            rows.Add(row);
        }

        return rows;
    }

    private static List<string> ParseCsvLine(string line)
    {
        var values = new List<string>();
        var builder = new StringBuilder();
        var insideQuotes = false;

        foreach (var character in line)
        {
            switch (character)
            {
                case '"':
                    insideQuotes = !insideQuotes;
                    break;
                case ',' when !insideQuotes:
                    values.Add(builder.ToString());
                    builder.Clear();
                    break;
                default:
                    builder.Append(character);
                    break;
            }
        }

        values.Add(builder.ToString());
        return values;
    }

    private void AddBinding(
        IDictionary<string, ScenarioDatasetBindingRecord> existing,
        IReadOnlyDictionary<string, DatasetArtifactRecord> artifactsByPath,
        Guid scenarioId,
        string relativePath,
        string role,
        string notes)
    {
        var normalizedPath = NormalizePath(relativePath);

        if (!artifactsByPath.TryGetValue(normalizedPath, out var artifact))
        {
            return;
        }

        var key = BuildBindingKey(scenarioId, artifact.Id, role);

        if (!existing.TryGetValue(key, out var binding))
        {
            binding = new ScenarioDatasetBindingRecord
            {
                Id = DeterministicGuid.FromString("scenario-dataset-binding", key)
            };

            _dbContext.ScenarioDatasetBindings.Add(binding);
            existing[key] = binding;
        }

        binding.ScenarioId = scenarioId;
        binding.DatasetArtifactId = artifact.Id;
        binding.BindingRole = role;
        binding.Notes = notes;
    }

    private Guid ResolveAreaGuid()
    {
        var catalogPath = GetRequiredFilePath("data/manifests/scenarios/proenca-a-nova-scenarios.generated.json");
        using var document = JsonDocument.Parse(File.ReadAllText(catalogPath, Encoding.UTF8));

        foreach (var scenario in document.RootElement.GetProperty("scenarios").EnumerateArray())
        {
            if (scenario.TryGetProperty("simulator_options", out var simulatorOptions)
                && simulatorOptions.TryGetProperty("AreaId", out var areaIdElement)
                && Guid.TryParse(areaIdElement.GetString(), out var areaId))
            {
                return areaId;
            }
        }

        return DeterministicGuid.FromString("area", "proenca-a-nova");
    }

    private string GetRequiredFilePath(string relativePath)
    {
        var fullPath = Path.Combine(_repoRoot, relativePath.Replace('/', Path.DirectorySeparatorChar));

        if (!File.Exists(fullPath))
        {
            throw new FileNotFoundException($"Required bootstrap input file was not found: {relativePath}", fullPath);
        }

        return fullPath;
    }

    private static string NormalizePath(string path)
    {
        return path.Replace('\\', '/').Trim();
    }

    private static string BuildDatasetCode(string datasetId, string relativePath)
    {
        var fileName = Path.GetFileName(relativePath)
            .Trim()
            .ToLowerInvariant()
            .Replace('.', '-');

        return $"{datasetId}:{fileName}";
    }

    private static string BuildBindingKey(Guid scenarioId, Guid datasetArtifactId, string role)
    {
        return $"{scenarioId:D}:{datasetArtifactId:D}:{role}";
    }

    private static string InferDatasetType(string relativePath)
    {
        var fileName = Path.GetFileNameWithoutExtension(relativePath).ToLowerInvariant();

        if (fileName.Contains("scenario"))
        {
            return "scenario";
        }

        if (fileName.Contains("weather"))
        {
            return "weather";
        }

        if (fileName.Contains("fire_history"))
        {
            return "fire_history";
        }

        if (fileName.Contains("cells_attributes") || fileName.Contains("grid"))
        {
            return "spatial_attributes";
        }

        if (fileName.Contains("area"))
        {
            return "area_boundary";
        }

        return "artifact";
    }

    private static ScenarioCategory ParseScenarioCategory(string? scenarioCategory)
    {
        return scenarioCategory?.Trim().ToLowerInvariant() switch
        {
            "base" => ScenarioCategory.Base,
            "highrisk" => ScenarioCategory.HighRisk,
            "failure" => ScenarioCategory.Failure,
            "exercise" => ScenarioCategory.Exercise,
            _ => ScenarioCategory.Base
        };
    }

    private static double? ParseDouble(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        return double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed)
            ? parsed
            : null;
    }

    private static string? NullIfEmpty(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }

    private async Task<List<GridCellRecord>> SelectPilotSensorCellsAsync(Guid areaId, CancellationToken cancellationToken)
    {
        var cells = await _dbContext.GridCells
            .Where(entity => entity.AreaId == areaId)
            .OrderBy(entity => entity.CellCode)
            .ToListAsync(cancellationToken);

        const int targetStationCount = 2;
        if (cells.Count <= targetStationCount)
        {
            return cells;
        }

        var prioritized = cells
            .OrderByDescending(entity => StructuralHazardRank(entity.StructuralHazard))
            .ThenBy(entity => entity.CellCode)
            .ToList();

        var selected = new List<GridCellRecord>();
        var step = Math.Max(1, prioritized.Count / targetStationCount);

        for (var index = 0; index < prioritized.Count && selected.Count < targetStationCount; index += step)
        {
            selected.Add(prioritized[index]);
        }

        foreach (var remaining in prioritized.Where(entity => selected.All(selectedCell => selectedCell.Id != entity.Id)))
        {
            if (selected.Count >= targetStationCount)
            {
                break;
            }

            selected.Add(remaining);
        }

        return selected
            .OrderBy(entity => entity.CellCode)
            .ToList();
    }

    private static IEnumerable<PilotSensorTemplate> GetPilotSensorTemplates(GridCellRecord cell)
    {
        var suffix = cell.CellCode.Split('-').Last();
        var installationProfile = StructuralHazardRank(cell.StructuralHazard) >= 4 ? "reinforced" : "standard";

        yield return new PilotSensorTemplate(
            $"pilot-temperature-{suffix}",
            "pilot-temperature-default",
            SensorType.Temperature,
            installationProfile);

        yield return new PilotSensorTemplate(
            $"pilot-humidity-{suffix}",
            "pilot-humidity-default",
            SensorType.Humidity,
            installationProfile);

        yield return new PilotSensorTemplate(
            $"pilot-wind-{suffix}",
            "pilot-wind-default",
            SensorType.Wind,
            installationProfile);
    }

    private static int StructuralHazardRank(string? hazard)
    {
        return hazard?.Trim().ToLowerInvariant() switch
        {
            "muito_alta" => 5,
            "alta" => 4,
            "media" => 3,
            "baixa" => 2,
            "muito_baixa" => 1,
            _ => 0
        };
    }

    private sealed record PilotSensorTemplate(
        string Name,
        string ProfileName,
        SensorType Type,
        string InstallationProfile);

    private sealed record PilotSensorProfileDefinition(
        Guid Id,
        string Name,
        string SensorFamily,
        string AccuracyJson,
        string NoiseJson,
        string FaultsJson,
        string PublicationJson);
}
