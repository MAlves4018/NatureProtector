using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;

namespace NatureProtector.Backoffice.Api.Tests;

public sealed class OpenApiSemanticTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;

    public OpenApiSemanticTests(WebApplicationFactory<Program> factory)
    {
        _factory = factory.WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment("Development");
            builder.UseSetting("BackofficeApi:ControlPlaneEnabled", "false");
            builder.ConfigureAppConfiguration((_, configurationBuilder) =>
            {
                configurationBuilder.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["BackofficeApi:ControlPlaneEnabled"] = "false"
                });
            });
        });
    }

    [Fact]
    public async Task OpenApiDocument_DescribesJwtSecurityRequirements()
    {
        using var document = await GetOpenApiDocumentAsync();
        var root = document.RootElement;

        var bearer = root
            .GetProperty("components")
            .GetProperty("securitySchemes")
            .GetProperty("Bearer");
        Assert.Equal("http", bearer.GetProperty("type").GetString());
        Assert.Equal("bearer", bearer.GetProperty("scheme").GetString());
        Assert.Equal("JWT", bearer.GetProperty("bearerFormat").GetString());

        var runtimeSummary = GetOperation(root, "/api/control/runtime/summary", "get");
        AssertBearerSecurity(runtimeSummary);
        AssertResponseCodes(runtimeSummary, "200", "401", "403", "503");

        var startRun = GetOperation(root, "/api/control/runtime/runs", "post");
        AssertBearerSecurity(startRun);
        AssertResponseCodes(startRun, "200", "400", "401", "403", "503");

        var publicAreas = GetOperation(root, "/api/control/areas", "get");
        Assert.False(publicAreas.TryGetProperty("security", out _));
    }

    [Fact]
    public async Task OpenApiDocument_DescribesRuntimeSummarySchema()
    {
        using var document = await GetOpenApiDocumentAsync();
        var root = document.RootElement;
        var schemas = root.GetProperty("components").GetProperty("schemas");
        var summary = schemas.GetProperty("RuntimeSummaryResponse");

        AssertRequiredContains(
            summary,
            "generatedAtUtc",
            "recentWindowMinutes",
            "areaCode",
            "currentRun",
            "pipeline",
            "risk",
            "limitations",
            "warnings");
        AssertSchemaProperty(summary, "generatedAtUtc", "string", "date-time", nullable: false);
        AssertSchemaProperty(summary, "recentWindowMinutes", "integer", "int32", nullable: false);
        AssertSchemaProperty(summary, "areaCode", "string", null, nullable: true);
        AssertSchemaReference(summary, "currentRun", "RuntimeRunSummaryResponse");
        AssertSchemaReference(summary, "pipeline", "RuntimePipelineSummaryResponse");

        var operation = GetOperation(root, "/api/control/runtime/summary", "get");
        var schemaReference = operation
            .GetProperty("responses")
            .GetProperty("200")
            .GetProperty("content")
            .GetProperty("application/json")
            .GetProperty("schema")
            .GetProperty("$ref")
            .GetString();
        Assert.Equal("#/components/schemas/RuntimeSummaryResponse", schemaReference);
    }

    [Fact]
    public async Task OpenApiDocument_DescribesRuntimeRunRequestAndContentTypes()
    {
        using var document = await GetOpenApiDocumentAsync();
        var root = document.RootElement;
        var schemas = root.GetProperty("components").GetProperty("schemas");
        var request = schemas.GetProperty("RuntimeRunStartRequest");

        AssertRequiredContains(
            request,
            "areaCode",
            "scenarioCode",
            "sensorCount",
            "numberOfCycles",
            "intervalSeconds",
            "seed",
            "degradationProfile");
        AssertSchemaProperty(request, "areaCode", "string", null, nullable: false);
        AssertSchemaProperty(request, "sensorCount", "integer", "int32", nullable: true);
        AssertSchemaProperty(request, "collectEvidence", "boolean", null, nullable: false);
        AssertSchemaProperty(request, "degradationProfiles", "array", null, nullable: true);

        var operation = GetOperation(root, "/api/control/runtime/runs", "post");
        var requestBody = operation.GetProperty("requestBody");
        Assert.True(requestBody.GetProperty("required").GetBoolean());
        var requestContent = requestBody.GetProperty("content");
        Assert.True(requestContent.TryGetProperty("application/json", out var jsonContent));
        Assert.Equal(
            "#/components/schemas/RuntimeRunStartRequest",
            jsonContent.GetProperty("schema").GetProperty("$ref").GetString());

        var responseContent = operation
            .GetProperty("responses")
            .GetProperty("200")
            .GetProperty("content");
        Assert.True(responseContent.TryGetProperty("application/json", out var responseJson));
        Assert.Equal(
            "#/components/schemas/RuntimeRunStartResponse",
            responseJson.GetProperty("schema").GetProperty("$ref").GetString());
    }

    [Fact]
    public async Task OpenApiDocument_DescribesObservabilityTypesAndNullability()
    {
        using var document = await GetOpenApiDocumentAsync();
        var schemas = document.RootElement.GetProperty("components").GetProperty("schemas");
        var healthComponent = schemas.GetProperty("RuntimeOperationalHealthComponentResponse");
        var queueMetric = schemas.GetProperty("RabbitMqQueueMetricResponse");

        AssertSchemaProperty(healthComponent, "status", "string", null, nullable: false);
        AssertSchemaProperty(healthComponent, "lastSuccessAt", "string", "date-time", nullable: true);
        AssertSchemaProperty(healthComponent, "ageSeconds", "number", "double", nullable: true);

        AssertSchemaProperty(queueMetric, "queueName", "string", null, nullable: false);
        AssertSchemaProperty(queueMetric, "messagesReady", "integer", "int32", nullable: true);
        AssertSchemaProperty(queueMetric, "messagesUnacknowledged", "integer", "int32", nullable: true);
        AssertSchemaProperty(queueMetric, "consumers", "integer", "int32", nullable: true);
    }

    private async Task<JsonDocument> GetOpenApiDocumentAsync()
    {
        using var client = _factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false
        });
        using var response = await client.GetAsync("/openapi/v1.json");
        response.EnsureSuccessStatusCode();
        return JsonDocument.Parse(await response.Content.ReadAsStringAsync());
    }

    private static JsonElement GetOperation(JsonElement root, string path, string method)
        => root.GetProperty("paths").GetProperty(path).GetProperty(method);

    private static void AssertBearerSecurity(JsonElement operation)
    {
        Assert.True(operation.TryGetProperty("security", out var security));
        var requirement = Assert.Single(security.EnumerateArray());
        Assert.True(requirement.TryGetProperty("Bearer", out var scopes));
        Assert.Empty(scopes.EnumerateArray());
    }

    private static void AssertResponseCodes(JsonElement operation, params string[] codes)
    {
        var responses = operation.GetProperty("responses");
        foreach (var code in codes)
        {
            Assert.True(responses.TryGetProperty(code, out _), $"Missing OpenAPI response code {code}.");
        }
    }

    private static void AssertRequiredContains(JsonElement schema, params string[] expected)
    {
        var required = schema
            .GetProperty("required")
            .EnumerateArray()
            .Select(item => item.GetString())
            .ToHashSet(StringComparer.Ordinal);

        foreach (var property in expected)
        {
            Assert.Contains(property, required);
        }
    }

    private static void AssertSchemaProperty(
        JsonElement schema,
        string propertyName,
        string expectedType,
        string? expectedFormat,
        bool nullable)
    {
        var property = schema.GetProperty("properties").GetProperty(propertyName);
        Assert.Equal(expectedType, property.GetProperty("type").GetString());
        if (expectedFormat is not null)
        {
            Assert.Equal(expectedFormat, property.GetProperty("format").GetString());
        }

        var isNullable = property.TryGetProperty("nullable", out var nullableElement) &&
                         nullableElement.GetBoolean();
        Assert.Equal(nullable, isNullable);
    }

    private static void AssertSchemaReference(JsonElement schema, string propertyName, string expectedSchema)
    {
        var reference = schema
            .GetProperty("properties")
            .GetProperty(propertyName)
            .GetProperty("$ref")
            .GetString();
        Assert.Equal($"#/components/schemas/{expectedSchema}", reference);
    }
}
