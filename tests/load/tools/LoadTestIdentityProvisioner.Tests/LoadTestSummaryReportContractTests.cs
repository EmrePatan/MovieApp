using System.Text.Json;

namespace MovieApp.LoadTestIdentityProvisioner.Tests;

public sealed class LoadTestSummaryReportContractTests
{
    [Fact]
    public void EnhancedReportV2_HasExpectedTopLevelSections()
    {
        const string json = """
            {
              "metadata": { "reportSchemaVersion": 2, "scenario": "user-concurrency" },
              "application": { "http_reqs": { "count": 10, "rate": 1.0 } },
              "iterations": { "completed": { "count": 5, "rate": 0.5 } },
              "outcomes": {
                "success2xx": { "count": 8 },
                "transportTimeout": { "count": 1 },
                "_meta": { "totalRequests": 10, "totalOutcomeEvents": 9 }
              },
              "metrics": { "http_req_duration": { "med": 100, "p(95)": 200 } },
              "groups": {
                "home": {
                  "http_reqs": { "count": 3, "rate": 0.3 },
                  "latency": { "med": 150, "p(95)": 250 }
                }
              }
            }
            """;

        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;
        Assert.Equal(2, root.GetProperty("metadata").GetProperty("reportSchemaVersion").GetInt32());
        Assert.True(root.TryGetProperty("application", out _));
        Assert.True(root.TryGetProperty("outcomes", out _));
        Assert.True(root.TryGetProperty("groups", out var groups));
        Assert.True(groups.TryGetProperty("home", out _));
    }
}
