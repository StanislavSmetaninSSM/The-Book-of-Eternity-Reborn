using System.Text.Json.Nodes;
using BookOfEternityClient.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace BookOfEternityClient.Tests;

/// <summary>Canonical projection schema and fresh seed shape, not publication admission.</summary>
public sealed class MortalPublishedProjectionShapeTests
{
    [Theory]
    [InlineData("activeEffectDefinitions")]
    [InlineData("knownExits")]
    [InlineData("adjacencyMap")]
    [InlineData("inventedProjectionAuthority")]
    public async Task CanonicalProjection_RecognizesClientProjectionFields(string field)
    {
        await using var context = await EffectMaterializationTestContext.CreateAsync();
        var location = MortalLocationTestFixture.CreateCanonicalLocationWithIdentity("loc_projection_shape", "Courtyard");
        var current = MortalLocationTestFixture.CreateCurrentProjection(location);
        current[field] = new JsonArray();
        const string path = MortalLocationMaterializationContract.CurrentLocationPath;
        await context.WriteJsonAsync(path, current);
        var issues = await context.Validator.ValidateGameStateAsync(new GameStateValidationSelection(
            GameStateValidationPhase.WorldQuestCombatFactionStateFiles, [path]));
        var unknown = issues.Where(issue => issue.Code == "mortal_location_state_unknown_top_level_field" &&
            issue.FilePath == path + "." + field).ToArray();
        if (field == "inventedProjectionAuthority") Assert.Single(unknown);
        else Assert.Empty(unknown);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task FreshSeed_HistoricalActorAlreadyHasIdempotentCanonicalTradeReceiptShape(bool stateOnly)
    {
        var root = Path.Combine(TestDiagnosticPaths.Root, "mortal-seed-receipt-shape", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            var files = new FileSystemManager(root, NullLogger<FileSystemManager>.Instance);
            await MortalLiveSeedBuilder.PrepareAsync(files, TestRepoPaths.BaseSessionRoot,
                MortalLiveSeedBuilder.SeedActorsAsync, stateOnly);
            var core = JsonNode.Parse((await files.ReadFileAsync("game_state/npcs/npc_core.json"))!)!;
            var actor = Assert.Single(core["NPCsInScene"]!.AsArray())!.AsObject();
            var before = actor.DeepClone();
            NpcTradeRequestState.NormalizeNpcTradeReceiptsShape(actor);
            Assert.True(JsonNode.DeepEquals(before, actor),
                "A fresh historical actor must already have the canonical shape before its first original is signed.");
            Assert.Empty(actor[NpcTradeRequestState.ReceiptsProperty]!.AsArray());
        }
        finally { if (Directory.Exists(root)) Directory.Delete(root, recursive: true); }
    }
}
