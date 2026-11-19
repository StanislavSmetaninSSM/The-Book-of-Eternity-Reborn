using System.Reflection;
using System.Text.Json;
using System.Text.Json.Nodes;
using BookOfEternityClient.Core;
using BookOfEternityClient.Services;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed partial class ActorMaterializationValidationTests
{
    [Theory]
    [InlineData("unchanged")]
    [InlineData("item-count")]
    [InlineData("item-added")]
    [InlineData("item-removed")]
    [InlineData("other-row-field")]
    [InlineData("explicit-response")]
    [InlineData("missing-snapshot")]
    [InlineData("invalid-snapshot")]
    public async Task Mm25HistoricalCarrier_RealOriginalDistinguishesRetainedStateFromCommand(string scenario)
    {
        const string path = "game_state/npcs/npc_core.json";
        const string actorId = "mm25_retained_historical_actor";
        var original = BuildMortalActorStateJson(actorId, "UpdateNPCs", false, true, true);
        await _fs.WriteFileAtomicAsync(path, original);
        await _fs.WriteFileAtomicAsync("game_state/meta/soul_state.json",
            """{"currentRealm":"Mortal World","currentIncarnation":1}""");
        var prepared = await new LiveTurnPreparationService(_fs).PrepareAsync(new LiveTurnPreparationOptions
        {
            SessionId = "mm25-retained-source-test", RequestId = "mm25-" + Guid.NewGuid().ToString("N"),
            TurnNumber = 43, CurrentRealm = "Mortal World", PlayerAction = "Попросить разрешение; ничего не брать.",
            PreGeneratedDices1d20 = new[] { 10, 2, 14 }
        });
        var manifestBytes = File.ReadAllBytes(_fs.ResolvePath(prepared.ManifestPath));
        var authorityBytes = File.ReadAllBytes(_fs.ResolvePath(prepared.AuthorityPath));
        var manifest = JsonSerializer.Deserialize<LiveTurnPendingSnapshotManifest>(
            File.ReadAllText(_fs.ResolvePath(prepared.ManifestPath)),
            LiveTurnPreparationService.ManifestJsonOptions)!;
        Assert.Equal(prepared.RequestId, manifest.RequestId);
        Assert.Equal(43, manifest.TurnNumber);
        var snapshotPath = _fs.ResolvePath(manifest.Files[path]);
        Assert.Equal(File.ReadAllBytes(_fs.ResolvePath(path)), File.ReadAllBytes(snapshotPath));
        Assert.Equal(PendingTurnSnapshotAuthority.ComputeSha256(File.ReadAllBytes(snapshotPath)), manifest.SnapshotFileHashes[path]);

        var current = JsonNode.Parse(original)!.AsObject();
        var actor = current["UpdateNPCs"]![0]!.AsObject();
        switch (scenario)
        {
            case "item-count": actor["inventory"]![0]!["count"] = 2; break;
            case "item-added":
                var added = actor["inventory"]![0]!.DeepClone().AsObject();
                added["itemId"] = "item_mm25_extra"; added["existedId"] = "item_mm25_extra";
                actor["inventory"]!.AsArray().Add(added); break;
            case "item-removed": actor["inventory"]!.AsArray().Clear(); break;
            case "other-row-field": actor["plans"] = "A new unsupported full-row plan."; break;
            case "missing-snapshot":
                File.Delete(_fs.ResolvePath(prepared.ManifestPath));
                File.Delete(_fs.ResolvePath(prepared.AuthorityPath));
                Directory.Delete(_fs.ResolvePath(LiveTurnPreparationService.PendingTurnSnapshotDirectory), true);
                break;
            case "invalid-snapshot": File.WriteAllText(_fs.ResolvePath(prepared.AuthorityPath), "{}"); break;
        }
        if (scenario is "item-count" or "item-added" or "item-removed" or "other-row-field")
            await _fs.WriteFileAtomicAsync(path, current.ToJsonString());
        var lookupMethod = typeof(ValidationService).GetMethod("LoadValidatedPendingTurnSnapshotLookupAsync",
            BindingFlags.Instance | BindingFlags.NonPublic)!;
        Assert.NotNull(lookupMethod);
        var lookupTask = (Task)lookupMethod.Invoke(_validator,
            new object?[] { null, null, "output/narrative_response.json", true })!;
        await lookupTask;
        var lookup = lookupTask.GetType().GetProperty("Result")!.GetValue(lookupTask)!;
        var status = lookup.GetType().GetProperty("Status")!.GetValue(lookup)!.ToString();
        Assert.Equal(scenario == "missing-snapshot" ? "Missing" :
            scenario == "invalid-snapshot" ? "Unusable" : "Usable", status);
        var before = Directory.EnumerateFiles(_rootPath, "*", SearchOption.AllDirectories)
            .ToDictionary(x => x, File.ReadAllBytes);
        IReadOnlyList<ValidationIssue> issues;
        if (scenario == "explicit-response")
        {
            using var response = JsonDocument.Parse(original);
            issues = _validator.ValidateResponse(response.RootElement);
        }
        else
            issues = await _validator.ValidateGameStateAsync(IntegrationValidationProfiles.ActorMaterialization);
        Assert.Equal(before.Keys.Order(), Directory.EnumerateFiles(_rootPath, "*", SearchOption.AllDirectories).Order());
        foreach (var (file, bytes) in before) Assert.Equal(bytes, File.ReadAllBytes(file));
        if (scenario is not "missing-snapshot")
        {
            Assert.Equal(manifestBytes, File.ReadAllBytes(_fs.ResolvePath(prepared.ManifestPath)));
            if (scenario != "invalid-snapshot")
                Assert.Equal(authorityBytes, File.ReadAllBytes(_fs.ResolvePath(prepared.AuthorityPath)));
        }
        if (scenario == "unchanged")
            Assert.DoesNotContain(issues, x => x.Code == "npc_existing_inventory_resend_forbidden");
        else
            Assert.Contains(issues, x => x.Code == "npc_existing_inventory_resend_forbidden");
    }
}
