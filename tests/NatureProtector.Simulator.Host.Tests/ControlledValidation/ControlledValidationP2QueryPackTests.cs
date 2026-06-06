namespace NatureProtector.Simulator.Host.Tests.ControlledValidation;

public sealed class ControlledValidationP2QueryPackTests
{
    [Fact]
    public void QueryPack10_ExistsWithExpectedOutputsAndNoBom()
    {
        var path = FindRepoFile("tools/data-audit/postgres/10_controlled_validation_p2.sql");
        var bytes = File.ReadAllBytes(path);
        var sql = File.ReadAllText(path);

        Assert.False(
            bytes.Length >= 3 && bytes[0] == 0xEF && bytes[1] == 0xBB && bytes[2] == 0xBF,
            "10_controlled_validation_p2.sql must not start with UTF-8 BOM.");
        Assert.Contains("coverage_gap_summary.csv", sql, StringComparison.Ordinal);
        Assert.Contains("missing_readings_expected_vs_observed.csv", sql, StringComparison.Ordinal);
        Assert.Contains("idempotent_duplicate_summary.csv", sql, StringComparison.Ordinal);
        Assert.Contains("value_degradation_by_profile.csv", sql, StringComparison.Ordinal);
        Assert.Contains("value_degradation_extended_summary.csv", sql, StringComparison.Ordinal);
        Assert.Contains("temporal_quality_summary.csv", sql, StringComparison.Ordinal);
        Assert.Contains("p2_expected_vs_observed.csv", sql, StringComparison.Ordinal);
        Assert.Contains("p2_m5_coverage_traceability.csv", sql, StringComparison.Ordinal);
        Assert.Contains("p2_extended_expected_vs_observed.csv", sql, StringComparison.Ordinal);
        Assert.Contains("p2_extended_m5_traceability.csv", sql, StringComparison.Ordinal);
        Assert.Contains("P2_TEMPORAL_OUT_OF_ORDER", sql, StringComparison.Ordinal);
        Assert.DoesNotContain("insert into", sql, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("update ", sql, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("delete from", sql, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("truncate", sql, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("drop table", sql, StringComparison.OrdinalIgnoreCase);
    }

    private static string FindRepoFile(string relativePath)
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            var candidate = Path.Combine(directory.FullName, relativePath);
            if (File.Exists(candidate))
            {
                return candidate;
            }

            directory = directory.Parent;
        }

        throw new FileNotFoundException($"Could not find repository file '{relativePath}'.");
    }
}
