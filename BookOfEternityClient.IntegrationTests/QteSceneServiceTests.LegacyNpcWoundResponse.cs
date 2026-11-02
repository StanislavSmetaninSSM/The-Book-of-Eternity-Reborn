using System.Text.Json;
using System.Text.Json.Nodes;
using BookOfEternityClient.Configuration;
using BookOfEternityClient.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed partial class QteSceneServiceTests
{
    [Fact]
    public async Task ValidateAcceptedTurnQteOfferAsync_RejectsLegacyNpcWoundsInGenuineOrdinaryOffer()
    {
        await AssertLegacyWoundOfferRefusedAsync("NPCWoundChanges", new JsonArray(
            new JsonObject { ["NPCId"] = "npc_guard", ["wounds"] = new JsonArray() }));
    }

    /// <summary>The existing genuine ordinary offer must also refuse the retired player wound command.</summary>
    [Fact]
    public async Task ValidateAcceptedTurnQteOfferAsync_RejectsLegacyPlayerWoundsInGenuineOrdinaryOffer()
    {
        await AssertLegacyWoundOfferRefusedAsync("playerWoundChanges", new JsonArray(
            new JsonObject { ["woundName"] = "Старая рана" }));
    }

    /// <summary>Reuses the same preparation with independently owned per-test state and a single exact wound-field mutation.</summary>
    private async Task AssertLegacyWoundOfferRefusedAsync(string propertyName, JsonNode value)
    {
        await _fs.WriteFileAtomicAsync("game_state/core/game_settings.json", "{\"qteEventsEnabled\":true}");
        await _fs.WriteFileAtomicAsync("game_state/meta/soul_state.json",
            "{\"currentRealm\":\"Mortal World\",\"currentIncarnation\":1}");
        await _fs.WriteFileAtomicAsync("game_state/npcs/npc_effects.json", "{\"schemaVersion\":1,\"entries\":[]}");
        await _fs.WriteFileAtomicAsync(WoundCarrierCatalog.PlayerPath,
            WoundContractTestData.CreatePlayerCarrier().ToJsonString());
        await new LiveTurnPreparationService(_fs).PrepareAsync(new LiveTurnPreparationOptions
        {
            SessionId = "npc-wound-qte-session", RequestId = "npc-wound-qte-request", TurnNumber = 20,
            CurrentRealm = "Mortal World", PlayerAction = "Открыть ворота.", PreGeneratedDices1d20 = [8]
        });
        await BindOrdinaryQteSnapshotSourceAsync(_fs);
        var offer = BuildUnscoredBranchChoiceOffer();
        var validator = new ValidationService(_fs, NullLogger<ValidationService>.Instance);
        await _fs.WriteFileAtomicAsync(QteSceneService.QteOfferPath, JsonSerializer.Serialize(offer, SharedJsonOptions.PrettyCamelCaseUnsafeRelaxed));
        Assert.Empty(await validator.ValidateAcceptedTurnQteOfferAsync());

        var fragment = Assert.IsType<JsonObject>(Assert.Single(offer.TerminalOutcomes).ResponseFragment);
        fragment[propertyName] = value;
        var rawOffer = JsonSerializer.Serialize(offer, SharedJsonOptions.PrettyCamelCaseUnsafeRelaxed);
        await _fs.WriteFileAtomicAsync(QteSceneService.QteOfferPath, rawOffer);
        var protectedPaths = new[]
        {
            LiveTurnPreparationService.TurnRequestPath, LiveTurnPreparationService.PendingTurnSnapshotManifestPath,
            PendingTurnSnapshotAuthority.AuthorityPath, "game_state/meta/soul_state.json", "game_state/npcs/npc_effects.json",
            WoundCarrierCatalog.PlayerPath
        };
        var before = new List<(string Path, byte[]? Bytes)>();
        foreach (var path in protectedPaths) before.Add((path, await _fs.ReadFileBytesAsync(path)));

        var issues = await validator.ValidateAcceptedTurnQteOfferAsync();

        Assert.Equal(rawOffer, await _fs.ReadFileAsync(QteSceneService.QteOfferPath));
        Assert.Equal(rawOffer, JsonSerializer.Serialize(offer, SharedJsonOptions.PrettyCamelCaseUnsafeRelaxed));
        foreach (var (path, bytes) in before) Assert.Equal(bytes, await _fs.ReadFileBytesAsync(path));
        var issue = Assert.Single(issues);
        Assert.Equal(QteSceneService.QteOfferPath + ".terminalOutcomes[0].responseFragment.response." + propertyName, issue.FilePath);
        Assert.Equal(IssueSeverity.Error, issue.Severity);
        Assert.Equal("wound_materialization_legacy_response_unsupported", issue.Code);
        Assert.DoesNotContain("effectChanges", issue.RepairHint ?? string.Empty, StringComparison.OrdinalIgnoreCase);
    }
}
