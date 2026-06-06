namespace NatureProtector.Simulator.Host.Tests.ControlledValidation;

public sealed class ControlledValidationP3QueryPackTests
{
    [Fact]
    public void QueryPack11_ExistsWithExpectedOutputsAndNoBom()
    {
        var path = FindRepoFile("tools/data-audit/postgres/11_controlled_validation_p3_negative_pipeline.sql");
        var bytes = File.ReadAllBytes(path);
        var sql = File.ReadAllText(path);

        Assert.False(
            bytes.Length >= 3 && bytes[0] == 0xEF && bytes[1] == 0xBB && bytes[2] == 0xBF,
            "11_controlled_validation_p3_negative_pipeline.sql must not start with UTF-8 BOM.");
        Assert.Contains("p3_expected_vs_observed.csv", sql, StringComparison.Ordinal);
        Assert.Contains("p3_rejected_by_fault_case.csv", sql, StringComparison.Ordinal);
        Assert.Contains("p3_quarantined_by_fault_case.csv", sql, StringComparison.Ordinal);
        Assert.Contains("p3_retry_paths_by_fault_case.csv", sql, StringComparison.Ordinal);
        Assert.Contains("p3_processing_attempts_by_fault_case.csv", sql, StringComparison.Ordinal);
        Assert.Contains("p3_m3_label_support.csv", sql, StringComparison.Ordinal);
        Assert.Contains("p3_negative_m5_traceability.csv", sql, StringComparison.Ordinal);
        Assert.Contains("p3_unexpected_accepted_or_risk.csv", sql, StringComparison.Ordinal);
        Assert.Contains("p3_blocked_or_skipped_cases.csv", sql, StringComparison.Ordinal);
        Assert.Contains("P3_REJECT_INVALID_JSON", sql, StringComparison.Ordinal);
        Assert.Contains("P3_REJECT_UNSUPPORTED_EVENT_TYPE", sql, StringComparison.Ordinal);
        Assert.Contains("P3_RETRY_TRANSIENT_THEN_SUCCESS", sql, StringComparison.Ordinal);
        Assert.Contains("P3_RETRY_EXHAUSTED_TO_QUARANTINE", sql, StringComparison.Ordinal);
        Assert.Contains("P3_QUARANTINE_SENSOR_INACTIVE", sql, StringComparison.Ordinal);
        Assert.Contains("P3_QUARANTINE_SENSOR_AREA_MISMATCH", sql, StringComparison.Ordinal);
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
