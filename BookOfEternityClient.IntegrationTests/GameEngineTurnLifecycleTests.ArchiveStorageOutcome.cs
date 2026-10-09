using System.Text.Json;
using System.Text.Json.Nodes;
using BookOfEternityClient.Core;
using BookOfEternityClient.Services;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed partial class GameEngineTurnLifecycleTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ArchiveUnknown_OriginalIdleNormalizationStopsAfterReservationOrRequestPublication(bool reservation)
    {
        using var owned = new CleanupOwnedFixture(_rootPath, line => _directGachaOutput?.WriteLine(line));
        Assert.True(OperatingSystem.IsLinux());
        using var probe = new ExplorerStorageProbe();
        var files = new FileSystemManager(_rootPath, Microsoft.Extensions.Logging.Abstractions.NullLogger<FileSystemManager>.Instance,
            PhysicalLoadTransactionOperations.Instance, probe.Hooks);
        probe.Attach(files);
        var soul = CreateLifecycleSoulState("Archive outcome", "Mortal World");
        soul["currentIncarnation"] = 2;
        var entry = new JsonObject
        {
            ["archiveId"] = "storage_archive", ["entryType"] = "lore_fragment", ["title"] = "Archive outcome",
            ["summary"] = "Bounded completed consultation", ["rarity"] = "Rare", ["sourceLife"] = 1,
            ["sourceKind"] = "codex", ["acquiredAtUtc"] = "2026-10-09T00:00:00Z"
        };
        if (reservation) entry["reservation"] = new JsonObject
        {
            ["reservationKind"] = "consultation", ["requestId"] = "storage_consultation",
            ["guardianId"] = "guardian_alpha", ["guardianName"] = "Азалия", ["createdAtTurn"] = 7,
            ["createdAtUtc"] = "2026-10-09T00:00:00Z"
        };
        soul["afterlifeArchive"] = new JsonObject
        {
            ["stored"] = new JsonArray(entry),
            ["actionReceipts"] = new JsonArray(new JsonObject
            {
                ["requestId"] = "storage_consultation", ["archiveId"] = "storage_archive", ["requestedMode"] = "consultation",
                ["status"] = "cancelled", ["resolvedAtTurn"] = 8, ["resolvedAtUtc"] = "2026-10-09T00:01:00Z"
            })
        };
        await SeedMortalLifeTransitionAuthorityAsync(soul);
        await files.WriteFileAtomicAsync(AfterlifeArchiveActionState.ConsultationRequestPath, JsonSerializer.Serialize(new
        {
            requestId = "storage_consultation", guardianId = "guardian_alpha", guardianName = "Азалия", archiveId = "storage_archive",
            archiveTitle = "Archive outcome", archiveEntryType = "lore_fragment", archiveRarity = "Rare", archiveSourceKind = "codex",
            targetIncarnation = 2, createdAtTurn = 7, createdAtUtc = "2026-10-09T00:00:00Z", requestedMode = "consultation"
        }));
        var input = new LoreRealmInput([], 0);
        var engine = CreateGameEngine(input, InertRealmSettings, fileSystem: files);
        await GetPrivateField<StateManager>(engine, "_stateManager").RefreshGameStateAsync();
        var requestBefore = File.ReadAllBytes(files.ResolvePath(AfterlifeArchiveActionState.ConsultationRequestPath));
        Dictionary<string, byte[]?>? priorAtCut = null;
        probe.Cut.Select = (path, member) => reservation
            ? path == files.ResolvePath("game_state/meta/soul_state.json") &&
              JsonNode.Parse(File.ReadAllText(path))?["afterlifeArchive"]?["stored"]?[0]?["reservation"] == null
            : path == files.ResolvePath(AfterlifeArchiveActionState.ConsultationRequestPath) && !member.GetProperty("After").GetProperty("Exists").GetBoolean();
        probe.Cut.BeforeCut = () =>
        {
            using var journal = CleanupPublicationCut.Metadata(File.ReadAllBytes(probe.Cut.JournalPath));
            var target = journal.RootElement.GetProperty("Members")[0].GetProperty("Path").GetString();
            var member = journal.RootElement.GetProperty("Members")[0];
            Assert.True(member.GetProperty("Before").GetProperty("Exists").GetBoolean());
            if (reservation)
            {
                var before = JsonNode.Parse(System.Text.Encoding.UTF8.GetString(member.GetProperty("Before").GetProperty("Bytes").GetBytesFromBase64()).TrimStart('\uFEFF'));
                Assert.Equal("storage_consultation", before!["afterlifeArchive"]!["stored"]![0]!["reservation"]!["requestId"]!.GetValue<string>());
            }
            priorAtCut = probe.Committed.Where(x => x.Key != target)
                .ToDictionary(x => x.Key, x => CleanupPublicationCut.ReadOptional(x.Key), StringComparer.Ordinal);
            foreach (var pair in priorAtCut) Assert.Equal(probe.Committed[pair.Key], pair.Value);
        };
        probe.Cut.Armed = true;
        var failure = await Record.ExceptionAsync(() => InvokePrivateTaskAsync(engine, "NormalizeRuntimeUiArtifactsAsync"));
        var requestAfter = CleanupPublicationCut.ReadOptional(files.ResolvePath(AfterlifeArchiveActionState.ConsultationRequestPath));
        var afterImages = priorAtCut?.ToDictionary(x => x.Key, x => CleanupPublicationCut.ReadOptional(x.Key), StringComparer.Ordinal);
        _directGachaOutput?.WriteLine(JsonSerializer.Serialize(new { reservation, Failure = failure?.ToString(), requestBefore, requestAfter,
            input.KeyReads, input.LineReads, probe.RequestAttempts, priorAtCut, afterImages, Cut = probe.Cut.Evidence() }));
        probe.Cut.AssertReachedAndStopped(); Assert.Same(probe.Cut.OriginalUncertainty, failure);
        input.AssertCompleted(); Assert.Equal(0, probe.RequestAttempts);
        if (reservation) Assert.Equal(requestBefore, requestAfter);
        Assert.NotNull(priorAtCut);
        foreach (var pair in priorAtCut!) Assert.Equal(pair.Value, afterImages![pair.Key]);
    }
}
