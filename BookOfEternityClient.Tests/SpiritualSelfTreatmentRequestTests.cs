using System.Text.Json.Nodes;
using BookOfEternityClient.Core;
using BookOfEternityClient.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed class SpiritualSelfTreatmentRequestTests
{
    [Fact]
    public async Task Stage_ValidSelfSourcePersistsSeparateDrawWithoutHealingOrAdvancingWorld()
    {
        await using var f = await Fixture.CreateAsync();
        await using var lease = await f.Files.AcquireCanonicalWriteLeaseAsync();
        var before = f.CanonicalBytes();
        var draws = 0;
        var result = await AfterlifeWoundTreatmentAuthority.StageSelfTreatmentAsync(f.Files, lease,
            f.Context, f.Selection, count => { draws++; Assert.Equal(1, count); return new[] { 14 }; });
        Assert.True(result.IsStaged, result.IssueCode);
        Assert.False(result.AlreadyStaged);
        Assert.Equal(1, draws);
        var root = await f.ReadRequestAsync(lease);
        Assert.Equal(2, root["schemaVersion"]!.GetValue<int>());
        Assert.Equal("spiritual_self_treatment_request", root["kind"]!.GetValue<string>());
        Assert.False(root.ContainsKey("snapshotToken"));
        Assert.False(root.ContainsKey("commands"));
        Assert.Equal(14, root["attempt"]!["roll"]!["values"]![0]!.GetValue<int>());
        Assert.Equal(before, f.CanonicalBytes());
    }

    [Fact]
    public async Task Stage_ExactPendingRetryReusesStoredDrawAndRequestBytes()
    {
        await using var f = await Fixture.CreateAsync();
        await using var lease = await f.Files.AcquireCanonicalWriteLeaseAsync();
        var draws = 0;
        int[] Draw(int count) { draws++; return new[] { 14 }; }
        var first = await AfterlifeWoundTreatmentAuthority.StageSelfTreatmentAsync(f.Files, lease, f.Context, f.Selection, Draw);
        Assert.True(first.IsStaged, first.IssueCode);
        var bytes = await f.Files.ReadFileBytesAsync(lease, AcceptedMechanicsPlan.WoundCommandPath);
        var again = await AfterlifeWoundTreatmentAuthority.StageSelfTreatmentAsync(f.Files, lease, f.Context, f.Selection, Draw);
        Assert.True(again.IsStaged, again.IssueCode);
        Assert.True(again.AlreadyStaged);
        Assert.Equal(first.RequestFingerprint, again.RequestFingerprint);
        Assert.Equal(1, draws);
        Assert.Equal(bytes, await f.Files.ReadFileBytesAsync(lease, AcceptedMechanicsPlan.WoundCommandPath));
    }

    [Fact]
    public async Task Stage_AnotherRequestCannotReplacePendingAttemptOrDraw()
    {
        await using var f = await Fixture.CreateAsync();
        await using var lease = await f.Files.AcquireCanonicalWriteLeaseAsync();
        var first = await AfterlifeWoundTreatmentAuthority.StageSelfTreatmentAsync(f.Files, lease,
            f.Context, f.Selection, _ => new[] { 14 });
        Assert.True(first.IsStaged, first.IssueCode);
        var bytes = await f.Files.ReadFileBytesAsync(lease, AcceptedMechanicsPlan.WoundCommandPath);
        var draws = 0;
        var other = await AfterlifeWoundTreatmentAuthority.StageSelfTreatmentAsync(f.Files, lease,
            f.Context with { RequestId = "other_request" }, f.Selection, _ => { draws++; return new[] { 20 }; });
        Assert.False(other.IsStaged);
        Assert.Equal("spiritual_self_treatment_pending_conflict", other.IssueCode);
        Assert.Equal(0, draws);
        Assert.Equal(bytes, await f.Files.ReadFileBytesAsync(lease, AcceptedMechanicsPlan.WoundCommandPath));
    }

    [Fact]
    public async Task Stage_InsufficientTierRefusesBeforeEntropyAndCanonicalWrites()
    {
        await using var f = await Fixture.CreateAsync(tier: 0);
        await using var lease = await f.Files.AcquireCanonicalWriteLeaseAsync();
        var before = f.CanonicalBytes();
        var draws = 0;
        var result = await AfterlifeWoundTreatmentAuthority.StageSelfTreatmentAsync(f.Files, lease,
            f.Context, f.Selection, _ => { draws++; return new[] { 20 }; });
        Assert.False(result.IsStaged);
        Assert.Equal("spiritual_self_treatment_tier_insufficient", result.IssueCode);
        Assert.Equal(0, draws);
        Assert.Null(await f.Files.ReadFileBytesAsync(lease, AcceptedMechanicsPlan.WoundCommandPath));
        Assert.Equal(before, f.CanonicalBytes());
    }

    [Fact]
    public async Task Stage_StaleWoundSelectionRefusesBeforeEntropy()
    {
        await using var f = await Fixture.CreateAsync();
        await using var lease = await f.Files.AcquireCanonicalWriteLeaseAsync();
        var draws = 0;
        var result = await AfterlifeWoundTreatmentAuthority.StageSelfTreatmentAsync(f.Files, lease, f.Context,
            f.Selection with { ExpectedWoundFingerprint = "sha256:" + new string('a', 64) },
            _ => { draws++; return new[] { 20 }; });
        Assert.False(result.IsStaged);
        Assert.Equal("spiritual_self_treatment_wound_changed", result.IssueCode);
        Assert.Equal(0, draws);
        Assert.Null(await f.Files.ReadFileBytesAsync(lease, AcceptedMechanicsPlan.WoundCommandPath));
    }

    [Fact]
    public async Task Stage_BackloggedCycleCannotBeCalledOneSafeSession()
    {
        await using var f = await Fixture.CreateAsync();
        await using var lease = await f.Files.AcquireCanonicalWriteLeaseAsync();
        f.Context.Progression.ChaosSeaCyclesExpectedThisTurn = 2;
        var draws = 0;
        var result = await AfterlifeWoundTreatmentAuthority.StageSelfTreatmentAsync(f.Files, lease,
            f.Context, f.Selection, _ => { draws++; return new[] { 20 }; });
        Assert.False(result.IsStaged);
        Assert.Equal("spiritual_self_treatment_cycle_invalid", result.IssueCode);
        Assert.Equal(0, draws);
        Assert.Null(await f.Files.ReadFileBytesAsync(lease, AcceptedMechanicsPlan.WoundCommandPath));
    }

    [Fact]
    public async Task Stage_ChangedSourceDuringEntropyCannotPublishStaleRequest()
    {
        await using var f = await Fixture.CreateAsync();
        await using var lease = await f.Files.AcquireCanonicalWriteLeaseAsync();
        var result = await AfterlifeWoundTreatmentAuthority.StageSelfTreatmentAsync(f.Files, lease,
            f.Context, f.Selection, _ =>
            {
                f.ChangeSoulTierDuringEntropy();
                return new[] { 14 };
            });
        Assert.False(result.IsStaged);
        Assert.Equal("spiritual_self_treatment_source_changed", result.IssueCode);
        Assert.Null(await f.Files.ReadFileBytesAsync(lease, AcceptedMechanicsPlan.WoundCommandPath));
    }

    [Fact]
    public async Task Stage_InvalidEntropyCannotBecomeRetainedDice()
    {
        await using var f = await Fixture.CreateAsync();
        await using var lease = await f.Files.AcquireCanonicalWriteLeaseAsync();
        var before = f.CanonicalBytes();
        var result = await AfterlifeWoundTreatmentAuthority.StageSelfTreatmentAsync(f.Files, lease,
            f.Context, f.Selection, _ => new[] { 21 });
        Assert.False(result.IsStaged);
        Assert.Equal("spiritual_self_treatment_draw_invalid", result.IssueCode);
        Assert.Null(await f.Files.ReadFileBytesAsync(lease, AcceptedMechanicsPlan.WoundCommandPath));
        Assert.Equal(before, f.CanonicalBytes());
    }

    private sealed class Fixture : IAsyncDisposable
    {
        private readonly string _root;
        private readonly string[] _paths;
        private Fixture(string root, FileSystemManager files, SpiritualSelfTreatmentStageContext context,
            SpiritualSelfTreatmentSelection selection, IEnumerable<string> paths)
        { _root = root; Files = files; Context = context; Selection = selection; _paths = paths.ToArray(); }
        internal FileSystemManager Files { get; }
        internal SpiritualSelfTreatmentStageContext Context { get; }
        internal SpiritualSelfTreatmentSelection Selection { get; }

        internal static async Task<Fixture> CreateAsync(int tier = 2)
        {
            var root = Path.Combine(Path.GetTempPath(), "boe-spiritual-self-request-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(root);
            var files = new FileSystemManager(root, NullLogger<FileSystemManager>.Instance);
            var wound = WoundContractTestData.CreateSpiritualActiveWound(ownerId: "player_soul",
                carrierPath: WoundCarrierCatalog.AfterlifeProfilesPath);
            var seed = WoundPlayerProjectionTestData.InputInRealm("chaos_sea", wound);
            var profiles = seed.Carriers.AfterlifeProfiles!;
            var profile = profiles["profiles"]![0]!.AsObject();
            var arts = AfterlifeSpiritualConflictState.CreateDefaultArtTiers();
            arts["spiritual_healing"] = tier;
            profile["standardArts"] = arts.DeepClone();
            var soulProfile = AfterlifeSpiritualConflictState.CreateDefaultCombatProfile();
            soulProfile["artTiers"] = arts.DeepClone();
            var soul = new JsonObject { ["currentRealm"] = "Chaos Sea", ["afterlifeCombatProfile"] = soulProfile };
            var parsed = WoundCarrierCatalog.Build(seed.Carriers);
            Assert.Empty(parsed.Issues);
            var current = Assert.Single(parsed.Occurrences).Wound;
            var effects = new List<JsonObject>();
            foreach (var binding in current.Consequences.OwnedEffectSources.RootBindings)
            {
                var definition = JsonNode.Parse(current.Consequences.OwnedEffectSources.Definitions.Single(d =>
                    d.GetProperty("definitionKey").GetString() == binding.DefinitionKey).GetRawText())!.AsObject();
                var key = current.Consequences.Entries.Single(e => e.EffectId == binding.EffectId).ProfileKey;
                var effect = EffectMaterializationTestFixture.CreateSpiritualWoundCanonicalEffect(key,
                    targetKind: "player", woundId: current.WoundId);
                effect["effectId"] = binding.EffectId;
                effect["target"]!["targetId"] = "player_soul";
                effect["source"]!["definitionKey"] = binding.DefinitionKey;
                effect["components"] = definition["components"]!.DeepClone();
                effect["triggers"] = definition["triggers"]!.DeepClone();
                effect["removal"] = definition["removal"]!.DeepClone();
                effect["stacking"]!["stackKey"] = definition["stacking"]!["stackKey"]!.DeepClone();
                effect["chronology"]!["lastTransitionId"] = "seed_effect_" + effects.Count;
                effects.Add(effect);
            }
            profile["activeEffects"] = new JsonArray(effects.Select(e => (JsonNode)e.DeepClone()).ToArray());
            var effectIndex = EffectMaterializationTestFixture.CreateIdentityIndex(effects.ToArray());
            foreach (var entry in effectIndex["entries"]!.AsArray().OfType<JsonObject>())
            {
                entry["owner"]!["kind"] = "afterlife_profile";
                entry["owner"]!["carrierPath"] = EffectCarrierCatalog.AfterlifeProfilesPath;
                entry["transitions"]![0]!["transitionId"] = effects.Single(e =>
                    e["effectId"]!.GetValue<string>() == entry["effectId"]!.GetValue<string>())["chronology"]!["lastTransitionId"]!.DeepClone();
            }
            var roots = new Dictionary<string, JsonObject>(StringComparer.Ordinal)
            {
                ["game_state/meta/soul_state.json"] = soul,
                [WoundCarrierCatalog.AfterlifeProfilesPath] = profiles,
                [WoundIdentityState.StatePath] = JsonNode.Parse(WoundIdentityState.SerializeCanonical(seed.Identities))!.AsObject(),
                [WoundHistoryState.HistoryPath] = JsonNode.Parse(WoundHistoryState.SerializeCanonical(seed.History))!.AsObject(),
                [EffectIdentityState.StatePath] = effectIndex
            };
            await using (var lease = await files.AcquireCanonicalWriteLeaseAsync())
                foreach (var pair in roots)
                    await files.WriteFileAtomicAsync(lease, pair.Key, pair.Value.ToJsonString());
            var mechanics = EffectMechanicsSnapshot.Build(new(new(null, null, null, null, profiles, null), effectIndex));
            Assert.True(mechanics.IsAccepted, string.Join(";", mechanics.Issues.Select(i => i.Code + ":" + i.Message)));
            Assert.Empty(seed.History.ValidateAgreement(seed.Identities, WoundCarrierCatalog.Build(seed.Carriers)));
            var schedule = new ProgressionScheduleService(files, NullLogger<ProgressionScheduleService>.Instance);
            var control = await schedule.BuildControlForNextTurnAsync();
            Assert.Equal(1, control.ChaosSeaCyclesExpectedThisTurn);
            var context = new SpiritualSelfTreatmentStageContext("session_spiritual_self", "request_spiritual_self", 43, control);
            return new(root, files, context, new(current.WoundId, WoundIdentityState.ComputeSemanticFingerprint(current)),
                roots.Keys.Append(ProgressionScheduleService.SchedulePath));
        }

        internal async Task<JsonObject> ReadRequestAsync(FileSystemManager.CanonicalWriteLease lease) =>
            JsonNode.Parse((await Files.ReadFileAsync(lease, AcceptedMechanicsPlan.WoundCommandPath))!)!.AsObject();

        internal string[] CanonicalBytes() => _paths.Order(StringComparer.Ordinal).Select(path =>
            Convert.ToBase64String(File.ReadAllBytes(Path.Combine(_root, path.Replace('/', Path.DirectorySeparatorChar))))).ToArray();

        internal void ChangeSoulTierDuringEntropy()
        {
            var path = Path.Combine(_root, "game_state", "meta", "soul_state.json");
            var root = JsonNode.Parse(File.ReadAllBytes(path))!.AsObject();
            root["afterlifeCombatProfile"]!["artTiers"]!["spiritual_healing"] = 3;
            File.WriteAllText(path, root.ToJsonString());
        }

        public ValueTask DisposeAsync()
        {
            if (!Path.GetFileName(_root).StartsWith("boe-spiritual-self-request-", StringComparison.Ordinal) ||
                Path.GetDirectoryName(_root) != Path.GetTempPath().TrimEnd(Path.DirectorySeparatorChar))
                throw new InvalidOperationException("Fixture cleanup refused a foreign root.");
            Directory.Delete(_root, recursive: true);
            return ValueTask.CompletedTask;
        }
    }
}
