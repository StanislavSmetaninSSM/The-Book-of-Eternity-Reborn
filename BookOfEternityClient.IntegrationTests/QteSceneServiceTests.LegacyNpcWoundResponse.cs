using System.Text.Json;
using System.Text.Json.Nodes;
using BookOfEternityClient.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed partial class QteSceneServiceTests
{
    [Fact]
    public async Task ValidateAcceptedTurnQteOfferAsync_RejectsLegacyNpcWoundsInGenuineOrdinaryOffer()
    {
        await _fs.WriteFileAtomicAsync("game_state/core/game_settings.json", "{\"qteEventsEnabled\":true}");
        await _fs.WriteFileAtomicAsync("game_state/meta/soul_state.json",
            "{\"currentRealm\":\"Mortal World\",\"currentIncarnation\":1}");
        await _fs.WriteFileAtomicAsync("game_state/npcs/npc_effects.json", "{\"schemaVersion\":1,\"entries\":[]}");
        await new LiveTurnPreparationService(_fs).PrepareAsync(new LiveTurnPreparationOptions
        {
            SessionId = "npc-wound-qte-session", RequestId = "npc-wound-qte-request", TurnNumber = 20,
            CurrentRealm = "Mortal World", PlayerAction = "Открыть ворота.", PreGeneratedDices1d20 = [8]
        });
        await BindOrdinaryQteSnapshotSourceAsync(_fs);
        var offer = BuildUnscoredBranchChoiceOffer();
        var validator = new ValidationService(_fs, NullLogger<ValidationService>.Instance);
        await _fs.WriteFileAtomicAsync(QteSceneService.QteOfferPath, JsonSerializer.Serialize(offer));
        Assert.Empty(await validator.ValidateAcceptedTurnQteOfferAsync());

        var fragment = Assert.IsType<JsonObject>(Assert.Single(offer.TerminalOutcomes).ResponseFragment);
        fragment["NPCWoundChanges"] = new JsonArray(new JsonObject { ["NPCId"] = "npc_guard", ["wounds"] = new JsonArray() });
        var rawOffer = JsonSerializer.Serialize(offer);
        await _fs.WriteFileAtomicAsync(QteSceneService.QteOfferPath, rawOffer);
        var protectedPaths = new[]
        {
            LiveTurnPreparationService.TurnRequestPath, LiveTurnPreparationService.PendingTurnSnapshotManifestPath,
            PendingTurnSnapshotAuthority.AuthorityPath, "game_state/meta/soul_state.json", "game_state/npcs/npc_effects.json"
        };
        var before = new List<(string Path, byte[]? Bytes)>();
        foreach (var path in protectedPaths) before.Add((path, await _fs.ReadFileBytesAsync(path)));

        var issues = await validator.ValidateAcceptedTurnQteOfferAsync();

        Assert.Equal(rawOffer, await _fs.ReadFileAsync(QteSceneService.QteOfferPath));
        Assert.Equal(rawOffer, JsonSerializer.Serialize(offer));
        foreach (var (path, bytes) in before) Assert.Equal(bytes, await _fs.ReadFileBytesAsync(path));
        var issue = Assert.Single(issues);
        Assert.Equal(QteSceneService.QteOfferPath + ".terminalOutcomes[0].responseFragment.response.NPCWoundChanges", issue.FilePath);
        Assert.Equal(IssueSeverity.Error, issue.Severity);
        Assert.Equal("wound_materialization_legacy_response_unsupported", issue.Code);
        Assert.DoesNotContain("effectChanges", issue.RepairHint ?? string.Empty, StringComparison.OrdinalIgnoreCase);
    }
}
