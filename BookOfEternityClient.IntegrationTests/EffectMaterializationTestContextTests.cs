using System.Text.Json.Nodes;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed class EffectMaterializationTestContextTests
{
    [Fact]
    public void OwnedPaths_CoverEveryPlannedCarrierAndControlRoot()
    {
        Assert.Equal(
            new[]
            {
                "game_state/player/effects.json",
                "game_state/npcs/npc_effects.json",
                "game_state/combat/enemies.json",
                "game_state/combat/allies.json",
                "game_state/meta/afterlife_entity_profiles.json",
                "game_state/meta/afterlife_spiritual_conflict_state.json",
                "game_state/effects/effect_identity_index.json",
                "game_state/effects/effect_commands.json",
                "game_state/control/pending_effect_resolutions.json"
            },
            EffectMaterializationTestContext.OwnedPaths);
        Assert.Equal(
            EffectMaterializationTestContext.OwnedPaths.Length,
            EffectMaterializationTestContext.OwnedPaths.Distinct(StringComparer.Ordinal).Count());
    }

    [Fact]
    public async Task WriteReadAndCaptureBytes_PreservePresentAndMissingEvidence()
    {
        await using var context = await EffectMaterializationTestContext.CreateAsync();
        var value = new JsonObject
        {
            ["schemaVersion"] = 1,
            ["activeEffects"] = new JsonArray()
        };

        await context.WriteJsonAsync(EffectMaterializationTestContext.PlayerEffectsPath, value);

        var read = await context.ReadJsonAsync(EffectMaterializationTestContext.PlayerEffectsPath);
        var bytes = await context.CaptureBytesAsync(
            EffectMaterializationTestContext.PlayerEffectsPath,
            EffectMaterializationTestContext.IdentityIndexPath);

        Assert.True(JsonNode.DeepEquals(value, read));
        Assert.NotNull(bytes[EffectMaterializationTestContext.PlayerEffectsPath]);
        Assert.Null(bytes[EffectMaterializationTestContext.IdentityIndexPath]);
        Assert.NotNull(context.Validator);
        Assert.NotNull(context.Normalizer);
    }

    [Fact]
    public async Task CaptureValidatedPendingSnapshotAsync_BindsExistingOwnedFiles()
    {
        await using var context = await EffectMaterializationTestContext.CreateAsync();
        await context.WriteJsonAsync(
            EffectMaterializationTestContext.PlayerEffectsPath,
            new JsonObject
            {
                ["schemaVersion"] = 1,
                ["activeEffects"] = new JsonArray()
            });

        await context.CaptureValidatedPendingSnapshotAsync(turn: 42);

        var manifest = (await context.ReadJsonAsync(
            "game_state/control/pending_turn_snapshot.json"))!.AsObject();
        Assert.Equal("session_effect_materialization", manifest["sessionId"]!.GetValue<string>());
        Assert.Equal(42, manifest["turnNumber"]!.GetValue<int>());
        Assert.True(manifest["files"]!.AsObject().ContainsKey(
            EffectMaterializationTestContext.PlayerEffectsPath));
        Assert.False(manifest["files"]!.AsObject().ContainsKey(
            EffectMaterializationTestContext.IdentityIndexPath));
        Assert.False(string.IsNullOrWhiteSpace(
            manifest["manifestPayloadHash"]!.GetValue<string>()));
    }

    [Fact]
    public async Task DisposeAsync_RemovesOnlyItsOwnedTemporaryRoot()
    {
        var context = await EffectMaterializationTestContext.CreateAsync();
        var rootPath = context.RootPath;
        Assert.True(Directory.Exists(rootPath));

        await context.DisposeAsync();

        Assert.False(Directory.Exists(rootPath));
    }
}
