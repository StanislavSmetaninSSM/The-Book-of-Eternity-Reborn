using System.Text;
using System.Text.Json.Nodes;
using BookOfEternityClient.Services;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed class MortalLivingWorldQteBoundaryTests
{
    [Theory]
    [InlineData(0, 239, 0, 0, 240, 1440, false)]
    [InlineData(0, 240, 0, 0, 240, 1440, true)]
    [InlineData(239, 1, 0, 0, 240, 1440, true)]
    [InlineData(239, 0, 0, 0, 240, 1440, false)]
    [InlineData(1000, 1, 0, 0, 240, 1440, false)]
    [InlineData(1000, 199, 0, 0, 240, 1440, false)]
    [InlineData(1000, 200, 0, 0, 240, 1440, true)]
    [InlineData(1439, 1, 1439, 0, 240, 1440, true)]
    [InlineData(100, 9, 100, 100, 10, 60, false)]
    [InlineData(100, 10, 100, 100, 10, 60, true)]
    public void ClockBoundary_RejectsOnlyAdditionalElapsedCycles(int minute, int delta, int worldMarker,
        int factionMarker, int worldCycle, int factionCycle, bool rejected)
    {
        var issues = MortalLivingWorldQteBoundary.Validate(Clock(minute),
            Schedule(minute, worldMarker, factionMarker, worldCycle, factionCycle),
            new JsonObject { ["timeChange"] = delta });
        if (!rejected) Assert.Empty(issues);
        else
        {
            var issue = Assert.Single(issues);
            Assert.Equal("mortal_living_world_qte_time_unsupported", issue.Code);
            Assert.Contains("ordinary", issue.RepairHint ?? string.Empty, StringComparison.OrdinalIgnoreCase);
        }
    }

    [Theory]
    [InlineData("{\"response\":\"A short scene.\"}")]
    [InlineData("{\"timeChange\":null,\"setWorldTime\":null}")]
    [InlineData("{\"timeChange\":0}")]
    public void ClockBoundary_NoElapsedTimeNeedsNoClockFixture(string json) =>
        Assert.Empty(MortalLivingWorldQteBoundary.Validate(null, null, JsonNode.Parse(json)!.AsObject()));

    [Theory]
    [InlineData("{\"timeChange\":-1}")]
    [InlineData("{\"timeChange\":\"5\"}")]
    [InlineData("{\"timeChange\":1.5}")]
    [InlineData("{\"timeChange\":true}")]
    [InlineData("{\"timeChange\":1,\"setWorldTime\":{\"currentTimeInMinutes\":2}}")]
    [InlineData("{\"setWorldTime\":{\"year\":1,\"monthName\":\"Spring\",\"dayOfMonth\":1,\"timeOfDay\":\"08:00\"}}")]
    [InlineData("{\"setWorldTime\":{\"currentTimeInMinutes\":-1}}")]
    [InlineData("{\"setWorldTime\":[]}")]
    public void ClockBoundary_MalformedOrUnresolvedCommandCannotBypassAdmission(string json)
    {
        var issue = Assert.Single(MortalLivingWorldQteBoundary.Validate(Clock(100), Schedule(100), JsonNode.Parse(json)!.AsObject()));
        Assert.Equal("mortal_living_world_qte_clock_invalid", issue.Code);
    }

    [Theory]
    [InlineData(239, 240, true)]
    [InlineData(1000, 1001, false)]
    [InlineData(1000, 1200, true)]
    public void ClockBoundary_NumericAbsoluteTimeUsesSameAdditionalCycleRule(int minute, int absolute, bool rejected)
    {
        var issues = MortalLivingWorldQteBoundary.Validate(Clock(minute), Schedule(minute),
            new JsonObject { ["setWorldTime"] = new JsonObject { ["currentTimeInMinutes"] = absolute } });
        Assert.Equal(rejected, issues.Any());
    }

    [Fact]
    public void ClockBoundary_ClockAndCurrentScheduleMustAgree()
    {
        var issue = Assert.Single(MortalLivingWorldQteBoundary.Validate(Clock(100), Schedule(99),
            new JsonObject { ["timeChange"] = 1 }));
        Assert.Equal("mortal_living_world_qte_clock_invalid", issue.Code);
    }

    [Fact]
    public void ClockBoundary_RetainedCanonicalMinuteCannotHideDelta()
    {
        var issue = Assert.Single(MortalLivingWorldQteBoundary.Validate(Clock(239), Schedule(239),
            new JsonObject { ["timeChange"] = 1 }));
        Assert.Equal("mortal_living_world_qte_time_unsupported", issue.Code);
    }

    [Fact]
    public void ClockBoundary_IntegerOverflowCannotWrapIntoShortScene()
    {
        var issue = Assert.Single(MortalLivingWorldQteBoundary.Validate(Clock(int.MaxValue), Schedule(int.MaxValue),
            new JsonObject { ["timeChange"] = 1 }));
        Assert.Equal("mortal_living_world_qte_clock_invalid", issue.Code);
    }

    [Fact]
    public async Task ClockBoundary_CurrentReaderChecksLaterCandidateAndReadsEachSourceOnce()
    {
        var requests = new Dictionary<string, int>(StringComparer.Ordinal);
        var sources = new Dictionary<string, byte[]?>(StringComparer.Ordinal)
        {
            [MortalLivingWorldSource.ReportPath] = null,
            ["game_state/meta/soul_state.json"] = Encoding.UTF8.GetBytes("{\"currentRealm\":\"Mortal World\"}"),
            [MortalLivingWorldSource.WorldTimePath] = Encoding.UTF8.GetBytes(Clock(0).ToJsonString()),
            [ProgressionScheduleService.SchedulePath] = Encoding.UTF8.GetBytes(Schedule(0).ToJsonString())
        };
        Task<byte[]?> Read(string path)
        {
            requests[path] = requests.GetValueOrDefault(path) + 1;
            return Task.FromResult(sources[path]);
        }

        var issues = await MortalLivingWorldQteBoundary.ValidateCurrentAsync(Read,
            [new JsonObject { ["timeChange"] = 1 }, new JsonObject { ["timeChange"] = 240 }]);

        Assert.Equal("mortal_living_world_qte_time_unsupported", Assert.Single(issues).Code);
        Assert.Equal(sources.Keys.OrderBy(path => path, StringComparer.Ordinal),
            requests.Keys.OrderBy(path => path, StringComparer.Ordinal));
        Assert.All(requests.Values, count => Assert.Equal(1, count));
    }

    [Fact]
    public async Task ClockBoundary_CurrentReaderReturnsIssueForDeferredDuplicateFragmentProperties()
    {
        var requests = new List<string>();
        Task<byte[]?> Read(string path)
        {
            requests.Add(path);
            Assert.Equal(MortalLivingWorldSource.ReportPath, path);
            return Task.FromResult<byte[]?>(null);
        }
        var fragment = JsonNode.Parse("{\"timeChange\":1,\"timeChange\":240}")!.AsObject();

        var issues = await MortalLivingWorldQteBoundary.ValidateCurrentAsync(Read, [fragment]);

        Assert.Equal("mortal_living_world_qte_clock_invalid", Assert.Single(issues).Code);
        Assert.Equal(MortalLivingWorldSource.ReportPath, Assert.Single(requests));
    }

    private static JsonObject Clock(int minute) => new() { ["currentTimeInMinutes"] = minute };
    private static JsonObject Schedule(int minute, int worldMarker = 0, int factionMarker = 0,
        int worldCycle = 240, int factionCycle = 1440) => new()
    {
        ["currentRealm"] = "Mortal World", ["currentWorldTimeInMinutes"] = minute,
        ["lastWorldSimulationTimeInMinutes"] = worldMarker, ["lastFactionSimulationTimeInMinutes"] = factionMarker,
        ["worldCycleMinutes"] = worldCycle, ["factionCycleMinutes"] = factionCycle
    };
}
