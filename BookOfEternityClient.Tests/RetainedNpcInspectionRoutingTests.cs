using Xunit;

namespace BookOfEternityClient.Tests;

public sealed class RetainedNpcInspectionRoutingTests
{
    [Fact]
    public void InspectionPreservesSelectionAndDoesNotReplaceWorkerValidation()
    {
        var selected = new BookOfEternityClient.Services.GameStateValidationSelection(
            BookOfEternityClient.Services.GameStateValidationPhase.NpcStateFiles,
            new[] { "game_state/npcs/npc_core.json" });
        var inspection = selected.ForReadOnlyInspection();
        Assert.False(selected.IsReadOnlyInspection);
        Assert.True(inspection.IsReadOnlyInspection);
        Assert.Equal(selected.Phases, inspection.Phases);
        Assert.True(inspection.IncludesStateFile("game_state/npcs/npc_core.json"));
        Assert.False(inspection.IncludesStateFile("game_state/world/world_map.json"));
        foreach (var path in new[] { "WebUi/BrowserLifecycleDashboardService.cs",
                     "UI/ExplorerLifecycleLocalTurnCommandResultBuilder.cs",
                     "UI/ExplorerMode/ExplorerMode.PrivateImplementation.cs" })
        {
            var source = File.ReadAllText(Path.Combine(TestRepoPaths.RepoRoot, "BookOfEternityClient", path));
            Assert.Contains(".InspectGameStateAsync()", source);
            Assert.DoesNotContain(".ValidateGameStateAsync()", source);
        }
        var worker = Directory.GetFiles(Path.Combine(TestRepoPaths.RepoRoot, "BookOfEternityClient"),
            "GmWorkerApplyGate.cs", SearchOption.AllDirectories).Single();
        Assert.Contains(".ValidateGameStateAsync(", File.ReadAllText(worker));
        Assert.DoesNotContain(".InspectGameStateAsync(", File.ReadAllText(worker));
    }
}
