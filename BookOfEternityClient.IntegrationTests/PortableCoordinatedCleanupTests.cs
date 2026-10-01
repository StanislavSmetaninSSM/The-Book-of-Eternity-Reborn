using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using BookOfEternityClient.Core;
using BookOfEternityClient.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed class PortableCoordinatedCleanupTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "boe-coordinated-cleanup-" + Guid.NewGuid().ToString("N"));
    private readonly FileSystemManager _files;
    private Action<TrustedLocalPublicationPhase, int>? _observer;
    private Func<string, Task>? _afterRead;
    private Func<Task>? _beforeLease;
    private string Journal => Path.Combine(_files.RuntimeRootPath, "trusted-local-publication-v1", "active.json");

    public PortableCoordinatedCleanupTests()
    {
        _files = new FileSystemManager(_root, NullLogger<FileSystemManager>.Instance,
            PhysicalLoadTransactionOperations.Instance, new FileSystemManagerHooks
            {
                LocalPublicationObserver = (phase, index) => _observer?.Invoke(phase, index),
                AfterCanonicalReadAttemptAsync = path => _afterRead?.Invoke(path) ?? Task.CompletedTask,
                BeforeCanonicalWriteLockOpenAsync = () => _beforeLease?.Invoke() ?? Task.CompletedTask
            });
        _files.EnsureDirectoryStructure();
        Directory.CreateDirectory(Path.GetDirectoryName(_files.SessionGenerationPath)!);
        File.WriteAllBytes(_files.SessionGenerationPath,
            JsonSerializer.SerializeToUtf8Bytes(new { SchemaVersion = 1, GenerationId = Guid.NewGuid().ToString("N") }));
    }

    [Theory]
    [InlineData("npc")]
    [InlineData("npc-leased")]
    [InlineData("guardian")]
    [InlineData("shining")]
    public Task ActualCleanupConsumerReachesOneCommittedMemberSet(string consumer) => AssertCommittedCleanup(consumer);

    private async Task AssertCommittedCleanup(string consumer)
    {
        var target = await Seed(consumer);
        string[]? members = null;
        var commits = 0;
        var targetPublication = false;
        _observer = (phase, _) =>
        {
            if (phase == TrustedLocalPublicationPhase.IntentPublished)
            {
                var current = ReadMembers();
                targetPublication = current.Contains(_files.ResolvePath(target), StringComparer.Ordinal);
                if (targetPublication) members = current;
            }
            if (targetPublication && phase == TrustedLocalPublicationPhase.Committed) commits++;
        };

        await Invoke(consumer);

        Assert.Equal(1, commits);
        Assert.Contains(_files.ResolvePath(target), members!);
        Assert.Equal(consumer == "resident" ? 2 : 1, members!.Length);
        Assert.Equal(consumer == "resident", File.Exists(_files.ResolvePath(target)));
        Assert.False(File.Exists(Journal));
    }

    [Theory]
    [InlineData("npc")]
    [InlineData("npc-leased")]
    [InlineData("guardian")]
    [InlineData("shining")]
    public Task ActualCleanupConsumerDoesNotSwallowUncertainPublication(string consumer) => AssertUncertainCleanup(consumer);

    private async Task AssertUncertainCleanup(string consumer)
    {
        var target = await Seed(consumer);
        var hits = 0;
        byte[]? evidence = null;
        Dictionary<string, byte[]?>? images = null;
        _observer = (phase, index) =>
        {
            if (phase != TrustedLocalPublicationPhase.MemberPublished || index != 0) return;
            var members = ReadMembers();
            if (!members.Contains(_files.ResolvePath(target), StringComparer.Ordinal)) return;
            hits++;
            File.WriteAllText(_files.ResolvePath(target), "{\"unknownAuthority\":true}", new UTF8Encoding(false));
            evidence = File.ReadAllBytes(Journal);
            images = members.ToDictionary(path => path, path => File.Exists(path) ? File.ReadAllBytes(path) : null);
            throw new IOException("Injected reached cleanup publication cut.");
        };

        var error = await Record.ExceptionAsync(() => Invoke(consumer));

        Assert.Equal(1, hits);
        Assert.Equal(evidence, File.ReadAllBytes(Journal));
        foreach (var pair in images!) Assert.Equal(pair.Value, File.Exists(pair.Key) ? File.ReadAllBytes(pair.Key) : null);
        Assert.IsType<CoordinatedStatePublicationUncertainException>(error);
    }

    [Theory]
    [InlineData("npc")]
    [InlineData("npc-leased")]
    [InlineData("guardian")]
    [InlineData("shining")]
    public Task MalformedPlanningInputRemainsAnOrdinaryKnownAbort(string consumer) => AssertMalformedPlanning(consumer);

    private async Task AssertMalformedPlanning(string consumer)
    {
        var target = await Seed(consumer);
        var before = File.ReadAllBytes(_files.ResolvePath(target));
        var input = consumer switch
        {
            "guardian" => "game_state/meta/guardians.json",
            "shining" => ShiningAbodeState.StatePath,
            _ => "game_state/npcs/npc_core.json"
        };
        File.WriteAllText(_files.ResolvePath(input), "{ malformed");
        var intents = 0;
        _observer = (phase, _) =>
        {
            if (phase == TrustedLocalPublicationPhase.IntentPublished &&
                ReadMembers().Contains(_files.ResolvePath(target), StringComparer.Ordinal)) intents++;
        };

        await Invoke(consumer);

        Assert.Equal(0, intents);
        Assert.Equal(before, File.ReadAllBytes(_files.ResolvePath(target)));
        Assert.False(File.Exists(Journal));
    }

    [Theory]
    [InlineData("npc", false)]
    public Task RetainedEvidenceAdmissionPropagatesBeforeAnyNewPublication(string consumer, bool duringRead) =>
        AssertRetainedAdmission(consumer, duringRead);

    private async Task AssertRetainedAdmission(string consumer, bool duringRead)
    {
        var target = await Seed(consumer);
        var before = File.ReadAllBytes(_files.ResolvePath(target));
        byte[] invalidEvidence = Encoding.UTF8.GetBytes("{\"SchemaVersion\":999}");
        var hits = 0;
        var armed = false;
        var intents = 0;
        void RetainEvidence()
        {
            if (hits != 0) return;
            hits++;
            Directory.CreateDirectory(Path.GetDirectoryName(Journal)!);
            File.WriteAllBytes(Journal, invalidEvidence);
        }
        _afterRead = path =>
        {
            if (duringRead && path == "game_state/control/life_transitions.json") RetainEvidence();
            if (!duringRead && path == "game_state/npcs/npc_core.json") armed = true;
            return Task.CompletedTask;
        };
        _beforeLease = () => { if (armed) RetainEvidence(); return Task.CompletedTask; };
        // Admission must block every new publication after evidence appears,
        // independently of which files the earlier public cleanup touched.
        _observer = (phase, _) =>
        {
            if (hits != 0 && phase == TrustedLocalPublicationPhase.IntentPublished) intents++;
        };

        var error = await Record.ExceptionAsync(() => Invoke(consumer));

        Assert.Equal(1, hits);
        Assert.Equal(0, intents);
        Assert.Equal(before, File.ReadAllBytes(_files.ResolvePath(target)));
        Assert.Equal(invalidEvidence, File.ReadAllBytes(Journal));
        Assert.IsType<InvalidDataException>(error);
    }

    [Fact]
    public Task ResidentTargetPublicationFollowsItsIndependentCleanupPreamble() => AssertCommittedCleanup("resident");

    [Fact]
    public Task ResidentTargetUncertaintyCannotBecomeSuccessfulCleanup() => AssertUncertainCleanup("resident");

    [Fact]
    public Task ResidentMalformedNpcPlanningRetainsTheManifestationBoundary() => AssertMalformedPlanning("resident");

    [Fact]
    public Task ResidentRetainedEvidenceDuringInterveningReadMustPropagate() => AssertRetainedAdmission("resident", duringRead: true);

    private async Task Invoke(string consumer)
    {
        switch (consumer)
        {
            case "npc": await NpcTradeRequestState.EnsureHealthyAsync(_files, "Mortal World"); break;
            case "npc-leased":
                await using (var lease = await _files.AcquireCanonicalWriteLeaseAsync())
                    await NpcTradeRequestState.EnsureHealthyAsync(_files, lease, "Mortal World");
                break;
            case "guardian": await GuardianTradeRequestState.EnsureHealthyAsync(_files, "Chaos Sea"); break;
            case "shining": await ShiningTradeRequestState.EnsureHealthyAsync(_files, "Shining Abode"); break;
            case "resident": await GuardianAbodeResidentRequestState.EnsureHealthyAsync(_files, "Mortal World"); break;
            default: throw new ArgumentOutOfRangeException(nameof(consumer));
        }
    }

    private async Task<string> Seed(string consumer)
    {
        if (consumer.StartsWith("npc", StringComparison.Ordinal))
        {
            Write(NpcTradeRequestState.PendingRequestPath, "{\"requests\":[{\"requestId\":\"invalid_cleanup\"}]}");
            Write("game_state/npcs/npc_core.json", "{}");
            return NpcTradeRequestState.PendingRequestPath;
        }
        if (consumer == "guardian")
        {
            var request = new GuardianTradeRequestState.PendingGuardianTradeRequest
            {
                RequestId = "guardian_cleanup", GuardianId = "guardian", GuardianName = "Хранитель",
                AbodeId = "abode", ReturnCycleId = "cycle", CurrentReputation = 10, DerivedTradeSlotCount = 1
            };
            var inventory = new JsonObject
            {
                ["tradeCycleId"] = "cycle", ["generatedAtUtc"] = "2026-10-01T00:00:00Z",
                ["generationReputationTier"] = "Neutral", ["pricingReputationTier"] = "Neutral",
                ["effectiveRarityCeilingBonusSteps"] = 0, ["projectBonusSignature"] = request.ProjectBonusSignature,
                ["items"] = new JsonArray(new JsonObject { ["slotId"] = "slot" })
            };
            var receipt = new JsonObject
            {
                ["requestId"] = request.RequestId, ["guardianId"] = request.GuardianId,
                ["abodeId"] = request.AbodeId, ["tradeCycleId"] = request.ReturnCycleId,
                ["status"] = "ready", ["itemCount"] = 1, ["resolvedAtTurn"] = 1,
                ["resolvedAtUtc"] = "2026-10-01T00:00:00Z"
            };
            Assert.True(GuardianTradeRequestState.InventoryMatchesRequestContract(inventory, request));
            Assert.True(GuardianTradeRequestState.ReceiptMatchesRequestContract(receipt, request, inventory));
            Write(GuardianTradeRequestState.PendingRequestPath, JsonSerializer.Serialize(request));
            Write("game_state/meta/guardians.json", new JsonObject
            {
                ["guardians"] = new JsonArray(new JsonObject
                {
                    ["guardianId"] = request.GuardianId, ["tradeInventory"] = inventory,
                    ["tradeInventoryReceipts"] = new JsonArray(receipt)
                })
            }.ToJsonString());
            return GuardianTradeRequestState.PendingRequestPath;
        }
        if (consumer == "shining")
        {
            await ShiningTradeRequestStateTests.WriteMinimalShiningTradeStateAsync(_files, 62, withReadyInventory: true);
            await ShiningTradeRequestState.WriteRequestAsync(_files, new()
            {
                RequestId = "shining_trade_existing", FactionId = "faction_old", FactionName = "Старый Дом",
                TradeCycleId = "shining_return_2", DerivedTradeTier = 2, DerivedTradeSlotCount = 6,
                DerivedRarityCeiling = "rare", DerivedServiceMultiplier = 1.25,
                MerchantProfile = ShiningTradeRequestState.MerchantProfileShiningFaction, CreatedAtTurn = 10
            });
            return ShiningTradeRequestState.PendingRequestsPath;
        }
        Write(GuardianAbodeResidentRequestState.PendingManifestationRequestPath,
            "{\"requests\":[{\"requestId\":\"resident_cleanup\",\"relicId\":\"relic\",\"targetIncarnation\":2}]}");
        Write("game_state/meta/soul_state.json",
            "{\"currentRealm\":\"Mortal World\",\"currentIncarnation\":2,\"soulRelics\":{\"equipped\":[],\"stored\":[]}}");
        Write("game_state/control/life_transitions.json", "{}");
        Write("game_state/npcs/npc_core.json", "{}");
        return GuardianAbodeResidentRequestState.PendingManifestationRequestPath;
    }

    private void Write(string path, string json) => File.WriteAllText(_files.ResolvePath(path), json, new UTF8Encoding(false));
    private string[] ReadMembers()
    {
        using var journal = JsonDocument.Parse(File.ReadAllBytes(Journal));
        return journal.RootElement.GetProperty("Members").EnumerateArray()
            .Select(member => member.GetProperty("Path").GetString()!).ToArray();
    }
    public void Dispose() { if (Directory.Exists(_root)) Directory.Delete(_root, recursive: true); }
}
