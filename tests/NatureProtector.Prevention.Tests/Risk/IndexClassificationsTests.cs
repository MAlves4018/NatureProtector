using NatureProtector.Prevention.Risk;

namespace NatureProtector.Prevention.Tests.Risk;

public sealed class IndexClassificationsTests
{
    [Theory]
    [InlineData(8.19, "Low", "Baixo/Reduzido", "Moderate", 0.01)]
    [InlineData(8.2, "Moderate", "Moderado", "High", 9.0)]
    [InlineData(17.07, "Moderate", "Moderado", "High", 0.13)]
    [InlineData(17.2, "High", "Elevado", "VeryHigh", 7.4)]
    [InlineData(24.6, "VeryHigh", "Muito Elevado", "Maximum", 13.7)]
    [InlineData(38.3, "Maximum", "Maximo", "Extreme", 11.8)]
    [InlineData(50.1, "Extreme", "Extremo", "Exceptional", 13.9)]
    [InlineData(64.0, "Exceptional", "Excecional", null, null)]
    public void FireWeatherIndexClassification_CoversIpmaClassBoundaries(
        double rawValue,
        string expectedClass,
        string expectedLabel,
        string? expectedNextClass,
        double? expectedDistance)
    {
        var classification = FireWeatherIndexClassification.From(
            rawValue,
            CanadianFireWeatherIndexCalculator.NormalizeFireWeatherIndex(rawValue),
            FireWeatherIndexCalculationStatus.Complete);

        Assert.Equal(expectedClass, classification.IpmaClass);
        Assert.Equal(expectedLabel, classification.IpmaClassLabel);
        Assert.Equal(expectedNextClass, classification.NextIpmaClass);
        if (expectedDistance.HasValue)
        {
            Assert.Equal(expectedDistance.Value, classification.ThresholdDistanceToNextClass!.Value, precision: 2);
        }
        else
        {
            Assert.Null(classification.ThresholdDistanceToNextClass);
        }
        Assert.NotNull(classification.EffisClass);
        Assert.Empty(classification.Limitations);
    }

    [Fact]
    public void FireWeatherIndexClassification_ClassifiesIpmaThresholds()
    {
        var moderate = FireWeatherIndexClassification.From(
            17.07,
            0.213,
            FireWeatherIndexCalculationStatus.CompleteWithCandidateDefaults);
        var high = FireWeatherIndexClassification.From(
            17.2,
            0.215,
            FireWeatherIndexCalculationStatus.Complete);
        var veryHigh = FireWeatherIndexClassification.From(
            24.6,
            0.308,
            FireWeatherIndexCalculationStatus.Complete);

        Assert.Equal("Moderate", moderate.IpmaClass);
        Assert.Equal("Moderado", moderate.IpmaClassLabel);
        Assert.Equal("High", moderate.NextIpmaClass);
        Assert.Equal(0.13, moderate.ThresholdDistanceToNextClass!.Value, precision: 2);
        Assert.Equal("High", high.IpmaClass);
        Assert.Equal("VeryHigh", veryHigh.IpmaClass);
    }

    [Theory]
    [InlineData(0.0, "VeryLowDryness", "Secura muito baixa", "Complete")]
    [InlineData(199.99, "VeryLowDryness", "Secura muito baixa", "Complete")]
    [InlineData(200.0, "LowModerateDryness", "Secura baixa a moderada", "Complete")]
    [InlineData(400.0, "HighDryness", "Secura elevada", "Complete")]
    [InlineData(600.0, "SevereDryness", "Secura severa", "Complete")]
    [InlineData(700.0, "ExtremeDryness", "Secura extrema", "Complete")]
    [InlineData(800.0, "ExtremeDryness", "Secura extrema", "Complete")]
    public void KbdiClassification_CoversDrynessBoundaries(
        double rawValue,
        string expectedClass,
        string expectedLabel,
        string expectedHistory)
    {
        var classification = KbdiDrynessClassification.From(
            rawValue,
            rawValue / CandidateParameterSetV1.KeetchByramDroughtIndexMaximum,
            KbdiCalculationStatus.Complete,
            null);

        Assert.Equal(expectedClass, classification.DrynessClass);
        Assert.Equal(expectedLabel, classification.DrynessClassLabel);
        Assert.Equal(expectedHistory, classification.AntecedentHistoryQuality);
        Assert.Empty(classification.Limitations);
    }

    [Theory]
    [InlineData(KbdiCalculationStatus.CompleteWithCandidateDefaults, "CandidateDefaults")]
    [InlineData(KbdiCalculationStatus.CalculatedFromHistory, "CalculatedFromHistory")]
    [InlineData(KbdiCalculationStatus.ReferenceImported, "ReferenceImported")]
    public void KbdiClassification_ReportsAntecedentHistoryQuality(
        KbdiCalculationStatus status,
        string expectedHistory)
    {
        var classification = KbdiDrynessClassification.From(300.0, 0.375, status, "source_a; source_a; source_b");

        Assert.Equal(expectedHistory, classification.AntecedentHistoryQuality);
        Assert.Equal(["source_a", "source_b"], classification.Limitations);
    }

    [Fact]
    public void KbdiClassification_ClassifiesDrynessAndLimitedHistory()
    {
        var veryLow = KbdiDrynessClassification.From(
            16.56,
            0.0207,
            KbdiCalculationStatus.LimitedAntecedentHistory,
            "antecedent_kbdi_candidate_default");
        var lowModerate = KbdiDrynessClassification.From(
            200.0,
            0.25,
            KbdiCalculationStatus.Complete,
            null);
        var extreme = KbdiDrynessClassification.From(
            800.0,
            1.0,
            KbdiCalculationStatus.Complete,
            null);

        Assert.Equal("VeryLowDryness", veryLow.DrynessClass);
        Assert.Equal("LimitedAntecedentHistory", veryLow.AntecedentHistoryQuality);
        Assert.Equal("LowModerateDryness", lowModerate.DrynessClass);
        Assert.Equal("ExtremeDryness", extreme.DrynessClass);
    }

    [Theory]
    [InlineData(null, "Missing", null, null)]
    [InlineData(-0.2, "Complete", "VeryLow", "Muito baixo")]
    [InlineData(0.0, "Complete", "VeryLow", "Muito baixo")]
    [InlineData(0.2, "Complete", "Low", "Baixo")]
    [InlineData(0.4, "Complete", "Moderate", "Moderado")]
    [InlineData(0.6, "Complete", "High", "Elevado")]
    [InlineData(0.8, "Complete", "VeryHigh", "Muito elevado")]
    [InlineData(1.2, "Complete", "VeryHigh", "Muito elevado")]
    public void NatureProtectorClassification_CoversBoundariesAndMissing(
        double? score,
        string expectedStatus,
        string? expectedClass,
        string? expectedLabel)
    {
        var classification = NatureProtectorRiskClassification.From(score);

        Assert.Equal(expectedStatus, classification.Status);
        Assert.Equal(expectedClass, classification.RiskClass);
        Assert.Equal(expectedLabel, classification.RiskClassLabel);
        Assert.Equal(CandidateParameterSetV1.Version, classification.ParameterSetVersion);
        if (score is null)
        {
            Assert.Contains("np_score_missing", classification.Limitations);
        }
    }

    [Fact]
    public void MissingIndexes_DoNotProduceMisleadingClasses()
    {
        var fwi = FireWeatherIndexClassification.From(
            null,
            null,
            FireWeatherIndexCalculationStatus.Missing);
        var kbdi = KbdiDrynessClassification.From(
            null,
            null,
            KbdiCalculationStatus.Missing,
            "kbdi_missing");

        Assert.Null(fwi.IpmaClass);
        Assert.Contains("fwi_class_unavailable", fwi.Limitations);
        Assert.Null(kbdi.DrynessClass);
        Assert.Contains("kbdi_class_unavailable", kbdi.Limitations);
    }

    [Theory]
    [InlineData(0.0, "VeryLow")]
    [InlineData(0.2, "Low")]
    [InlineData(0.4, "Moderate")]
    [InlineData(0.6, "High")]
    [InlineData(0.8, "VeryHigh")]
    [InlineData(1.2, "VeryHigh")]
    public void PortugueseContextProxy_ClassifiesTerritoryBoundaries(double territory, string expectedClass)
    {
        Assert.Equal(expectedClass, PortugueseContextRiskProxy.ClassifyTerritory(territory));
    }

    [Fact]
    public void PortugueseContextProxy_UsesCandidateMatrixAndMarksLimitations()
    {
        var moderateFwi = FireWeatherIndexClassification.From(
            17.07,
            0.213,
            FireWeatherIndexCalculationStatus.Complete);
        var lowFwi = FireWeatherIndexClassification.From(
            4.0,
            0.05,
            FireWeatherIndexCalculationStatus.Complete);
        var veryHighFwi = FireWeatherIndexClassification.From(
            30.0,
            0.375,
            FireWeatherIndexCalculationStatus.Complete);

        var highProxy = PortugueseContextRiskProxy.From(moderateFwi, 0.7);
        var lowProxy = PortugueseContextRiskProxy.From(lowFwi, 0.3);
        var veryHighProxy = PortugueseContextRiskProxy.From(veryHighFwi, 0.7);
        var missingProxy = PortugueseContextRiskProxy.From(
            FireWeatherIndexClassification.From(null, null, FireWeatherIndexCalculationStatus.Missing),
            0.7);

        Assert.Equal("High", highProxy.ProxyClass);
        Assert.Equal("Low", lowProxy.ProxyClass);
        Assert.Equal("VeryHigh", veryHighProxy.ProxyClass);
        Assert.Equal("Missing", missingProxy.Status);
        Assert.Contains("not_official_rcm", highProxy.Limitations);
        Assert.Contains("does_not_use_official_icnf_rural_hazard", highProxy.Limitations);
    }

    [Theory]
    [InlineData(4.0, 0.1, "Low", "Baixo")]
    [InlineData(17.2, 0.5, "Moderate", "Moderado")]
    [InlineData(17.2, 0.6, "VeryHigh", "Muito elevado")]
    [InlineData(24.6, 0.7, "VeryHigh", "Muito elevado")]
    [InlineData(38.3, 0.7, "Extreme", "Extremo")]
    [InlineData(50.1, 0.6, "Extreme", "Extremo")]
    [InlineData(64.0, 0.6, "Extreme", "Extremo")]
    public void PortugueseContextProxy_CoversCandidateCombinationMatrix(
        double rawFwi,
        double territoryComponent,
        string expectedProxyClass,
        string expectedLabel)
    {
        var fwi = FireWeatherIndexClassification.From(
            rawFwi,
            CanadianFireWeatherIndexCalculator.NormalizeFireWeatherIndex(rawFwi),
            FireWeatherIndexCalculationStatus.Complete);

        var proxy = PortugueseContextRiskProxy.From(fwi, territoryComponent);

        Assert.Equal("Complete", proxy.Status);
        Assert.Equal(expectedProxyClass, proxy.ProxyClass);
        Assert.Equal(expectedLabel, proxy.ProxyClassLabel);
        Assert.Equal(CandidateParameterSetV1.Version, proxy.MatrixVersion);
        Assert.Equal("candidate_portuguese_context_proxy", proxy.Provenance);
    }

    [Fact]
    public void PortugueseContextProxy_UnknownFwiClass_ReturnsPartialWithoutOverclaiming()
    {
        var fwi = new FireWeatherIndexClassification(
            RawValue: 12.0,
            NormalizedValue: 0.15,
            Status: "Complete",
            IpmaClass: "UnknownExternalClass",
            IpmaClassLabel: "Unknown",
            EffisClass: null,
            ThresholdDistanceToNextClass: null,
            NextIpmaClass: null,
            Limitations: []);

        var proxy = PortugueseContextRiskProxy.From(fwi, 0.4);

        Assert.Equal("Complete", proxy.Status);
        Assert.Equal("Partial", proxy.ProxyClass);
        Assert.Equal("Partial", proxy.ProxyClassLabel);
        Assert.Contains("not_official_rcm", proxy.Limitations);
    }

    [Fact]
    public void PortugueseContextProxy_MissingTerritory_ReturnsExplicitMissing()
    {
        var fwi = FireWeatherIndexClassification.From(17.2, 0.215, FireWeatherIndexCalculationStatus.Complete);

        var proxy = PortugueseContextRiskProxy.From(fwi, null);

        Assert.Equal("Missing", proxy.Status);
        Assert.Null(proxy.ProxyClass);
        Assert.Contains("missing_fwi_or_territory", proxy.Limitations);
        Assert.Equal("candidate_portuguese_context_proxy", proxy.Provenance);
    }

    [Fact]
    public void LocalFwiPercentile_NotAvailable_IsExplicit()
    {
        var result = LocalFwiPercentileResult.NotAvailable();

        Assert.Equal("NotAvailable", result.Status);
        Assert.Null(result.Percentile);
        Assert.Equal("historical_local_fwi_distribution_not_materialized", result.Reason);
    }
}
