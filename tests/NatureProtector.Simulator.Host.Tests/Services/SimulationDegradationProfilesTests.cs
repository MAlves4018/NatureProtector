using NatureProtector.Simulator.Host.Services;

namespace NatureProtector.Simulator.Host.Tests.Services;

public sealed class SimulationDegradationProfilesTests
{
    [Fact]
    public void Normalize_SplitsAliasesAndRemovesNoneWhenCombined()
    {
        var profiles = SimulationDegradationProfiles.Normalize(
            ["none", "missing;noisy-readings|stuck+range"],
            "delay,duplicate-events,out-of-order-events");

        Assert.Equal(
            [
                SimulationDegradationProfiles.MissingReadings,
                SimulationDegradationProfiles.Noise,
                SimulationDegradationProfiles.StuckValue,
                SimulationDegradationProfiles.ClippingRange,
                SimulationDegradationProfiles.LagDelay,
                SimulationDegradationProfiles.Duplicate,
                SimulationDegradationProfiles.OutOfOrder
            ],
            profiles);
    }

    [Fact]
    public void Normalize_EmptyInputReturnsEmptyAndNoneRemainsWhenAlone()
    {
        Assert.Empty(SimulationDegradationProfiles.Normalize(null, null));
        Assert.Equal([SimulationDegradationProfiles.None], SimulationDegradationProfiles.Normalize(null, "none"));
    }

    [Fact]
    public void Resolve_PrefersRequestedProfilesOverScenarioDefaults()
    {
        var resolved = SimulationDegradationProfiles.Resolve(
            requestedProfiles: [SimulationDegradationProfiles.Noise],
            requestedLegacyProfile: null,
            scenarioProfiles: [SimulationDegradationProfiles.MissingReadings],
            scenarioLegacyProfile: SimulationDegradationProfiles.LagDelay);

        Assert.Equal([SimulationDegradationProfiles.Noise], resolved);
    }

    [Fact]
    public void Resolve_FallsBackToScenarioProfiles()
    {
        var resolved = SimulationDegradationProfiles.Resolve(
            requestedProfiles: null,
            requestedLegacyProfile: null,
            scenarioProfiles: null,
            scenarioLegacyProfile: "deterministic-missing-readings+late");

        Assert.Equal(
            [SimulationDegradationProfiles.MissingReadings, SimulationDegradationProfiles.LagDelay],
            resolved);
    }

    [Fact]
    public void LegacyAndContainsHelpersExposeCompatibilitySemantics()
    {
        var profiles = new[]
        {
            SimulationDegradationProfiles.MissingReadings,
            SimulationDegradationProfiles.Noise
        };

        Assert.Null(SimulationDegradationProfiles.ToLegacyProfile([]));
        Assert.Equal(SimulationDegradationProfiles.Noise, SimulationDegradationProfiles.ToLegacyProfile([SimulationDegradationProfiles.Noise]));
        Assert.Equal("missing-readings+noise", SimulationDegradationProfiles.ToLegacyProfile(profiles));
        Assert.True(SimulationDegradationProfiles.Contains(profiles, "NOISE"));
        Assert.False(SimulationDegradationProfiles.IsNoneOrEmpty(profiles));
        Assert.True(SimulationDegradationProfiles.IsNoneOrEmpty([]));
        Assert.True(SimulationDegradationProfiles.IsNoneOrEmpty([SimulationDegradationProfiles.None]));
    }
}
