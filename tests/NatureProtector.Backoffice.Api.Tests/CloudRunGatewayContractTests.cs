using System.Net;
using NatureProtector.Backoffice.Api.RuntimeOrchestration;

namespace NatureProtector.Backoffice.Api.Tests;

public sealed class CloudRunGatewayContractTests
{
    private static readonly RuntimeOrchestrationOptions Options = new()
    {
        CloudRunProjectId = "project-a",
        CloudRunRegion = "europe-west1",
        CloudRunSimulatorJobName = "simulator"
    };

    [Fact]
    public void ResourcePolicy_AcceptsConfiguredBoundary()
    {
        var policy = new CloudRunResourceNamePolicy(Options);

        Assert.Equal(
            "projects/project-a/locations/europe-west1/operations/op-1",
            policy.ValidateOperationName("projects/project-a/locations/europe-west1/operations/op-1"));
        Assert.Equal(
            "projects/project-a/locations/europe-west1/jobs/simulator/executions/ex-1",
            policy.ValidateExecutionName("projects/project-a/locations/europe-west1/jobs/simulator/executions/ex-1"));
    }

    [Theory]
    [InlineData("https://attacker.invalid/operations/op-1")]
    [InlineData("projects/other/locations/europe-west1/operations/op-1")]
    [InlineData("projects/project-a/locations/europe-west1/operations/../jobs/simulator")]
    [InlineData("projects/project-a/locations/europe-west1/operations/op-1/extra")]
    public void ResourcePolicy_RejectsReferencesOutsideConfiguredBoundary(string value)
    {
        var policy = new CloudRunResourceNamePolicy(Options);

        var error = Assert.Throws<CloudRunGatewayException>(() => policy.ValidateOperationName(value));

        Assert.Equal(CloudRunGatewayOperation.ValidateProviderReference, error.Operation);
        Assert.False(error.IsRetryable);
    }

    [Theory]
    [InlineData(HttpStatusCode.RequestTimeout, true)]
    [InlineData(HttpStatusCode.TooManyRequests, true)]
    [InlineData(HttpStatusCode.ServiceUnavailable, true)]
    [InlineData(HttpStatusCode.InternalServerError, true)]
    [InlineData(HttpStatusCode.BadRequest, false)]
    [InlineData(HttpStatusCode.Forbidden, false)]
    [InlineData(HttpStatusCode.NotFound, false)]
    public void ErrorPolicy_ClassifiesRetryability(HttpStatusCode status, bool expected)
        => Assert.Equal(expected, CloudRunGatewayErrorPolicy.IsRetryable(status));

    [Fact]
    public void ErrorPolicy_BuildsHttpFailureWithSanitizedProviderSummary()
    {
        var body = """
            {
              "error": {
                "code": 503,
                "status": "UNAVAILABLE",
                "message": "provider temporarily unavailable password=secret"
              }
            }
            """;

        var error = CloudRunGatewayErrorPolicy.FromHttpFailure(
            CloudRunGatewayOperation.StartJob,
            HttpStatusCode.ServiceUnavailable,
            body);

        Assert.Equal(CloudRunGatewayOperation.StartJob, error.Operation);
        Assert.Equal("cloud_run_http_503", error.Code);
        Assert.Equal(HttpStatusCode.ServiceUnavailable, error.StatusCode);
        Assert.True(error.IsRetryable);
        Assert.Contains("status=UNAVAILABLE", error.Message, StringComparison.Ordinal);
        Assert.Contains("password=[REDACTED]", error.Message, StringComparison.Ordinal);
        Assert.DoesNotContain("password=secret", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void ErrorPolicy_RedactsAndBoundsProviderBody()
    {
        var body = "Bearer secret-token password=hunter2 " + new string('x', 900);

        var summary = CloudRunGatewayErrorPolicy.ExtractSafeProviderSummary(body);

        Assert.DoesNotContain("secret-token", summary, StringComparison.Ordinal);
        Assert.DoesNotContain("hunter2", summary, StringComparison.Ordinal);
        Assert.True(summary.Length <= CloudRunGatewayErrorPolicy.MaximumSafeProviderMessageCharacters + 3);
    }

    [Theory]
    [InlineData("", "<empty>")]
    [InlineData("   ", "<empty>")]
    [InlineData("malformed token=abc123 more", "malformed token=[REDACTED] more")]
    public void ErrorPolicy_SummarizesEmptyAndMalformedProviderBodies(string body, string expected)
        => Assert.Equal(expected, CloudRunGatewayErrorPolicy.ExtractSafeProviderSummary(body));
}
