using Xunit;

namespace BookOfEternityClient.Tests;

public sealed class GuardianFixtureIsolationTests
{
    [Fact]
    public void PreparedFixtureSnapshot_MaterializesIndependentRootsWithoutMutatingRepositoryBaseline()
    {
        var baselineSoulStatePath = Path.Combine(
            TestRepoPaths.BaseSessionRoot,
            "game_state",
            "meta",
            "soul_state.json");
        var baselineBytes = File.ReadAllBytes(baselineSoulStatePath);

        using var first = new GuardianSystemRegressionTests();
        using var second = new GuardianSystemRegressionTests();

        Assert.NotEqual(first.FixtureRootPath, second.FixtureRootPath);
        Assert.Equal(1, GuardianSystemRegressionTests.FixtureSnapshotBuildCount);

        var firstSoulStatePath = Path.Combine(
            first.FixtureRootPath,
            "game_state",
            "meta",
            "soul_state.json");
        var secondSoulStatePath = Path.Combine(
            second.FixtureRootPath,
            "game_state",
            "meta",
            "soul_state.json");

        File.WriteAllText(firstSoulStatePath, """{"soulName":"first-only"}""");

        Assert.DoesNotContain(
            "first-only",
            File.ReadAllText(secondSoulStatePath),
            StringComparison.Ordinal);
        Assert.Equal(baselineBytes, File.ReadAllBytes(baselineSoulStatePath));
    }
}
