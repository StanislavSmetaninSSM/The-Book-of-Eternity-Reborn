using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using BookOfEternityClient.Core;
using BookOfEternityClient.Configuration;
using BookOfEternityClient.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;
using Xunit.Abstractions;

namespace BookOfEternityClient.Tests;

public sealed class WoundPlayerReadServiceTests(ITestOutputHelper output)
{
    [Fact]
    public async Task Load_VisibleCurrentWoundIsImmediateAndWholeOwnedTreeIsUnchanged()
    {
        await using var fixture = await WoundPlayerReadFixture.CreateAsync(output);
        var before = fixture.Snapshot();
        var projection = await WoundPlayerReadService.LoadAsync(fixture.Files);
        Assert.True(projection.IsAvailable);
        Assert.Equal("II", Assert.Single(projection.Active).Severity);
        Assert.Single(projection.Active[0].Methods);
        Assert.Equal(before, fixture.Snapshot());
    }

    [Fact]
    public async Task Load_EmptyCanonicalStateIsExplicitAndReadOnly()
    {
        await using var fixture = await WoundPlayerReadFixture.CreateAsync(output, empty: true);
        var before = fixture.Snapshot();
        var projection = await WoundPlayerReadService.LoadAsync(fixture.Files);
        Assert.True(projection.IsAvailable);
        Assert.Empty(projection.Active);
        Assert.Contains("нет видимых активных ран", projection.StatusMessage);
        Assert.Equal(before, fixture.Snapshot());
    }

    [Fact]
    public async Task Load_UsesOneExistingPublicationReadLeaseAndNoCanonicalMutation()
    {
        await using var fixture = await WoundPlayerReadFixture.CreateAsync(output);
        var before = fixture.Snapshot();
        var locks = fixture.LockOpens;
        var writes = fixture.MutationCalls;
        var result = await WoundPlayerReadService.LoadAsync(fixture.Files);
        Assert.True(result.IsAvailable);
        Assert.Equal(1, fixture.LockOpens - locks);
        Assert.Equal(writes, fixture.MutationCalls);
        Assert.Equal(before, fixture.Snapshot());
    }

    [Fact]
    public async Task Load_BorrowedLeaseRemainsActiveWithoutNestedLockOrMutation()
    {
        await using var fixture = await WoundPlayerReadFixture.CreateAsync(output);
        var before = fixture.Snapshot();
        await using (var lease = await fixture.Files.AcquireCanonicalWriteLeaseAsync(CanonicalWritePurpose.PublicationReadQuiescence))
        {
            var locks = fixture.LockOpens;
            var mutations = fixture.MutationCalls;
            var projection = await WoundPlayerReadService.LoadAsync(fixture.Files, lease);
            Assert.True(projection.IsAvailable);
            Assert.Single(projection.Active);
            Assert.True(lease.IsActive);
            Assert.Equal(locks, fixture.LockOpens);
            Assert.Equal(mutations, fixture.MutationCalls);
        }
        Assert.Equal(before, fixture.Snapshot());
    }

    [Fact]
    public async Task StatusSnapshot_CapturesHeaderAndWoundsOnOneReadLeaseWithoutRepair()
    {
        await using var fixture = await WoundPlayerReadFixture.CreateAsync(output, realm: "chaos_sea");
        await using (var lease = await fixture.Files.AcquireCanonicalWriteLeaseAsync())
            await fixture.Files.WriteFileAtomicAsync(lease, "game_state/meta/soul_state.json",
                "{\"currentRealm\":\"Chaos Sea\",\"soulName\":\"Путник\",\"soulFormDescription\":\"Светлый узор\",\"currentIncarnation\":4}");
        var repairs = 0;
        var state = new StateManager(fixture.Files, new GameSettings(), NullLogger<StateManager>.Instance,
            new StateManagerHooks { AfterPlayerSoulProfileInputsReadAsync = () => { repairs++; return Task.CompletedTask; } });
        var before = fixture.Snapshot();
        var locks = fixture.LockOpens;
        var mutations = fixture.MutationCalls;
        var snapshot = await WoundPlayerStatusReadService.LoadAsync(state, fixture.Files);
        Assert.Equal("Chaos Sea", snapshot.State.CurrentRealm);
        Assert.Equal("Путник", snapshot.State.SoulName);
        Assert.Equal("Светлый узор", snapshot.State.SoulFormDescription);
        Assert.Equal(4, snapshot.State.Incarnation);
        Assert.Equal("Известная рана души", Assert.Single(snapshot.Wounds.Active).Name);
        Assert.Equal(locks + 1, fixture.LockOpens);
        Assert.Equal(mutations, fixture.MutationCalls);
        Assert.Equal(0, repairs);
        Assert.Equal(before, fixture.Snapshot());
        // A subsequent independent refresh must not replace the already-captured header.
        await using (var lease = await fixture.Files.AcquireCanonicalWriteLeaseAsync())
            await fixture.Files.WriteFileAtomicAsync(lease, "game_state/meta/soul_state.json", "{\"currentRealm\":\"Mortal\",\"soulName\":\"Иной путник\",\"soulFormDescription\":\"Иная форма\",\"currentIncarnation\":5}");
        var changed = fixture.Snapshot();
        var refreshMutations = fixture.MutationCalls;
        await using (var lease = await fixture.Files.AcquireCanonicalWriteLeaseAsync(CanonicalWritePurpose.PublicationReadQuiescence))
        {
            await state.RefreshGameStateReadOnlyAsync(lease);
            Assert.True(lease.IsActive);
            Assert.Equal(refreshMutations, fixture.MutationCalls);
        }
        Assert.Equal(changed, fixture.Snapshot());
        Assert.NotSame(snapshot.State, state.CurrentState);
        Assert.Equal("Mortal", state.CurrentState.CurrentRealm);
        Assert.Equal("Иной путник", state.CurrentState.SoulName);
        Assert.Equal("Иная форма", state.CurrentState.SoulFormDescription);
        Assert.Equal(5, state.CurrentState.Incarnation);
        Assert.Equal("Chaos Sea", snapshot.State.CurrentRealm);
        Assert.Equal("Путник", snapshot.State.SoulName);
        Assert.Equal("Светлый узор", snapshot.State.SoulFormDescription);
        Assert.Equal(4, snapshot.State.Incarnation);
        Assert.Equal(0, repairs);
    }

    [Theory]
    [InlineData("missing_player")]
    [InlineData("missing_npc")]
    [InlineData("missing_enemy")]
    [InlineData("missing_ally")]
    [InlineData("missing_profiles")]
    [InlineData("duplicate")]
    [InlineData("stale_identity")]
    [InlineData("legacy_shape")]
    [InlineData("invalid_utf8")]
    [InlineData("missing_identity")]
    [InlineData("missing_history")]
    [InlineData("missing_soul")]
    [InlineData("missing_resources")]
    [InlineData("foreign_owner")]
    [InlineData("stale_history")]
    [InlineData("duplicate_identity")]
    [InlineData("invalid_effect_utf8")]
    public async Task Load_IncompleteOrMalformedAuthorityIsUnavailableAndPreserved(string fault)
    {
        await using var fixture = await WoundPlayerReadFixture.CreateAsync(output);
        await fixture.DamageAsync(fault);
        var before = fixture.Snapshot();
        var projection = await WoundPlayerReadService.LoadAsync(fixture.Files);
        Assert.False(projection.IsAvailable);
        Assert.Equal(WoundPlayerProjection.UnavailableMessage, projection.StatusMessage);
        Assert.Empty(projection.Active);
        Assert.Empty(projection.History);
        Assert.Equal(before, fixture.Snapshot());
    }
    [Theory]
    [InlineData("chaos_sea")]
    [InlineData("shining_abode")]
    public async Task Load_PositiveAfterlifeUsesCanonicalSoulAndExactCurrentSelf(string realm)
    {
        await using var fixture = await WoundPlayerReadFixture.CreateAsync(output, realm: realm);
        var before = fixture.Snapshot();
        var result = await WoundPlayerReadService.LoadAsync(fixture.Files);
        Assert.True(result.IsAvailable);
        Assert.Equal("Известная рана души", Assert.Single(result.Active).Name);
        Assert.Equal(before, fixture.Snapshot());
    }

    [Theory]
    [InlineData("accepted", true)]
    [InlineData("proposal", false)]
    [InlineData("bare", false)]
    [InlineData("contained", false)]
    [InlineData("hidden", false)]
    [InlineData("gm_only", false)]
    [InlineData("invalid_visibility", false)]
    [InlineData("duplicate", false)]
    [InlineData("unaccepted_duplicate", false)]
    public async Task Load_ItemLabelsRequireUniqueAcceptedVisibleTopLevelCurrentItems(string state, bool readable)
    {
        await using var fixture = await WoundPlayerReadFixture.CreateAsync(output, targetMaterials: true);
        var item = MortalItemTestFixture.CreateCanonicalRoot("sterile_thread");
        item["name"] = "Известная хирургическая нить";
        if (state == "contained") item["contentsPath"] = new JsonArray("private_container");
        if (state is "hidden" or "gm_only" or "invalid_visibility") item["visibility"] = state == "invalid_visibility" ? "unknown" : state;
        MortalItemTestFixture.ResealCanonical(item);
        Assert.True(MortalItemMaterializationContract.TryReadAcceptedIdentity(item, out _));
        var inventory = new JsonObject { ["items"] = new JsonArray(item) };
        if (state == "proposal")
        {
            inventory["items"] = new JsonArray();
            inventory["UpdateInventory"] = new JsonArray(new JsonObject
            {
                ["itemId"] = "sterile_thread", ["creationRef"] = "proposal_secret",
                ["name"] = "Известная хирургическая нить"
            });
        }
        if (state == "bare") inventory["items"] = new JsonArray(new JsonObject
            { ["itemId"] = "sterile_thread", ["name"] = "Известная хирургическая нить" });
        if (state is "duplicate" or "unaccepted_duplicate") inventory["items"]!.AsArray().Add(state == "duplicate" ? item.DeepClone() :
            new JsonObject { ["itemId"] = "sterile_thread", ["name"] = "Непринятый дубль" });
        await using (var lease = await fixture.Files.AcquireCanonicalWriteLeaseAsync())
            await fixture.Files.WriteFileAtomicAsync(lease, "game_state/inventory/items.json", inventory.ToJsonString());
        var before = fixture.Snapshot();
        var result = await WoundPlayerReadService.LoadAsync(fixture.Files);
        Assert.True(result.IsAvailable);
        var requirements = Assert.Single(Assert.Single(result.Active).Methods).Requirements;
        Assert.Equal(readable, requirements.Any(q => q.Contains("Известная хирургическая нить")));
        Assert.Equal(!readable, requirements.Any(q => q.StartsWith("Лечебные материалы")));
        Assert.DoesNotContain(requirements, q => q.Contains("proposal_secret") || q.Contains("sterile_thread") || q.Contains("Непринятый дубль"));
        Assert.Equal(before, fixture.Snapshot());
    }

    [Fact]
    public async Task Load_HiddenSoulProfileRestrictsOtherwiseKnownWound()
    {
        await using var fixture = await WoundPlayerReadFixture.CreateAsync(output, realm: "chaos_sea", hiddenSelf: true);
        var before = fixture.Snapshot();
        var result = await WoundPlayerReadService.LoadAsync(fixture.Files);
        Assert.True(result.IsAvailable);
        Assert.Empty(result.Active);
        Assert.Empty(result.History);
        Assert.Equal(before, fixture.Snapshot());
    }

    [Fact]
    public async Task Load_StoredOldProfileIsLegalInMortalAndCanonicalRealmSwitchChangesOnlyReadScope()
    {
        await using var fixture = await WoundPlayerReadFixture.CreateAsync(output, includeOldSoul: true);
        var before = fixture.Snapshot();
        var mortal = await WoundPlayerReadService.LoadAsync(fixture.Files);
        Assert.True(mortal.IsAvailable);
        Assert.Equal("Рваная рана левого бока", Assert.Single(mortal.Active).Name);
        Assert.Equal(before, fixture.Snapshot());
        await using (var lease = await fixture.Files.AcquireCanonicalWriteLeaseAsync())
            await fixture.Files.WriteFileAtomicAsync(lease, "game_state/meta/soul_state.json", "{\"currentRealm\":\"Chaos Sea\"}");
        var changed = fixture.Snapshot();
        var soul = await WoundPlayerReadService.LoadAsync(fixture.Files);
        Assert.True(soul.IsAvailable);
        Assert.Equal("Известная рана души", Assert.Single(soul.Active).Name);
        Assert.Equal(changed, fixture.Snapshot());
    }

}

internal sealed class WoundPlayerReadFixture(ITestOutputHelper output) : IAsyncDisposable
{
    internal string Root { get; } = Path.Combine(Path.GetTempPath(), "boe-wound-read-" + Guid.NewGuid().ToString("N"));
    internal FileSystemManager Files { get; private set; } = null!;
    internal int LockOpens { get; private set; }
    internal int MutationCalls { get; private set; }

    internal static async Task<WoundPlayerReadFixture> CreateAsync(ITestOutputHelper output, bool empty = false, string realm = "mortal_world", bool hiddenSelf = false, bool includeOldSoul = false, bool targetMaterials = false, WoundPlayerProjectionInput? suppliedInput = null, CanonicalReadActivityAccumulator? reads = null)
    {
        var fixture = new WoundPlayerReadFixture(output);
        try
        {
            Directory.CreateDirectory(fixture.Root);
            fixture.Files = new(fixture.Root, NullLogger<FileSystemManager>.Instance, PhysicalLoadTransactionOperations.Instance,
                new FileSystemManagerHooks
                {
                    BeforeCanonicalWriteLockOpenAsync = () => { fixture.LockOpens++; return Task.CompletedTask; },
                    PassiveCanonicalReads = reads,
                    BeforeCanonicalMutationBoundaryAsync = _ => { fixture.MutationCalls++; return Task.CompletedTask; }
                });
            fixture.Files.EnsureDirectoryStructure();
            var soulWound = WoundContractTestData.CreateSpiritualActiveWound("wound_soul", realm == "mortal_world" ? "chaos_sea" : realm,
                ownerId: "player_soul", carrierPath: WoundCarrierCatalog.AfterlifeProfilesPath);
            soulWound["display"]!["name"] = "Известная рана души";
            var mortalWound = WoundContractTestData.CreateActiveWound();
            if (targetMaterials) mortalWound["treatment"]!["routes"]![0]!["requirements"]![0]!["ownerRole"] = "target";
            var input = suppliedInput ?? (empty ? WoundPlayerProjectionTestData.Input() : realm != "mortal_world"
                ? WoundPlayerProjectionTestData.InputInRealm(realm, soulWound)
                : includeOldSoul ? WoundPlayerProjectionTestData.Input(mortalWound, soulWound)
                : WoundPlayerProjectionTestData.Input(mortalWound));
            if (hiddenSelf) input.Carriers.AfterlifeProfiles!["profiles"]![0]!["visibility"] = "hidden";
            await using var lease = await fixture.Files.AcquireCanonicalWriteLeaseAsync();
            foreach (var (path, root) in new (string, JsonObject)[]
            {
                (WoundCarrierCatalog.PlayerPath, input.Carriers.PlayerWounds!),
                (WoundCarrierCatalog.NpcPath, input.Carriers.NpcWounds!),
                (WoundCarrierCatalog.EnemiesPath, input.Carriers.EnemyCombatants!),
                (WoundCarrierCatalog.AlliesPath, input.Carriers.AllyCombatants!),
                (WoundCarrierCatalog.AfterlifeProfilesPath, input.Carriers.AfterlifeProfiles!),
                ("game_state/meta/soul_state.json", new() { ["currentRealm"] = realm == "mortal_world" ? "Mortal" : realm == "chaos_sea" ? "Chaos Sea" : "Shining Abode", ["soulName"] = "Путник" }),
                (ResourceMaterializationContract.DefinitionsPath, input.Resources.ToCanonicalRoot())
            })
                await fixture.Files.WriteFileAtomicAsync(lease, path, root.ToJsonString());
            await fixture.Files.WriteFileAtomicAsync(lease, WoundIdentityState.StatePath, WoundIdentityState.SerializeCanonical(input.Identities));
            await fixture.Files.WriteFileAtomicAsync(lease, WoundHistoryState.HistoryPath, WoundHistoryState.SerializeCanonical(input.History));
            return fixture;
        }
        catch
        {
            await fixture.DisposeAsync();
            throw;
        }
    }

    internal async Task DamageAsync(string fault)
    {
        await using var lease = await Files.AcquireCanonicalWriteLeaseAsync();
        var missing = fault switch
        {
            "missing_player" => WoundCarrierCatalog.PlayerPath, "missing_npc" => WoundCarrierCatalog.NpcPath,
            "missing_enemy" => WoundCarrierCatalog.EnemiesPath, "missing_ally" => WoundCarrierCatalog.AlliesPath,
            "missing_profiles" => WoundCarrierCatalog.AfterlifeProfilesPath,
            "missing_identity" => WoundIdentityState.StatePath, "missing_history" => WoundHistoryState.HistoryPath,
            "missing_soul" => "game_state/meta/soul_state.json", "missing_resources" => ResourceMaterializationContract.DefinitionsPath,
            _ => null
        };
        if (missing is not null) { Files.DeleteFile(lease, missing); return; }
        if (fault == "invalid_utf8")
        {
            await Files.WriteFileAtomicBytesAsync(lease, WoundCarrierCatalog.PlayerPath, [0xff]); return;
        }
        if (fault == "invalid_effect_utf8")
        {
            await Files.WriteFileAtomicBytesAsync(lease, EffectCarrierCatalog.PlayerPath,
                Encoding.UTF8.GetBytes("{\"schemaVersion\":1,\"activeEffects\":[],\"ignored\":\"").Concat(new byte[] { 0xff }).Concat(Encoding.UTF8.GetBytes("\"}")).ToArray());
            return;
        }
        var path = fault is "stale_identity" or "foreign_owner" or "duplicate_identity" ? WoundIdentityState.StatePath :
            fault == "stale_history" ? WoundHistoryState.HistoryPath : WoundCarrierCatalog.PlayerPath;
        var raw = (await Files.ReadFileAsync(lease, path))!;
        var changed = fault switch
        {
            "duplicate" => raw.Replace("\"schemaVersion\":1", "\"schemaVersion\":1,\"schemaVersion\":1", StringComparison.Ordinal),
            "duplicate_identity" => raw.Replace("\"schemaVersion\":1", "\"schemaVersion\":1,\"schemaVersion\":1", StringComparison.Ordinal),
            "foreign_owner" => raw.Replace("\"ownerId\":\"player_current\"", "\"ownerId\":\"player_other\"", StringComparison.Ordinal),
            "stale_history" => WithStaleHistoryFingerprint(raw),
            "stale_identity" => raw.Replace("\"lastTransitionOrdinal\":1", "\"lastTransitionOrdinal\":2", StringComparison.Ordinal),
            "legacy_shape" => "{\"wounds\":[{\"woundName\":\"Нельзя читать старую форму\",\"severity\":\"moderate\"}]}",
            _ => throw new ArgumentOutOfRangeException(nameof(fault))
        };
        Assert.NotEqual(raw, changed);
        await Files.WriteFileAtomicAsync(lease, path, changed);
    }

    private static string WithStaleHistoryFingerprint(string raw)
    {
        var root = JsonNode.Parse(raw)!.AsObject();
        root["transitions"]![0]!["afterFingerprint"] = "sha256:" + new string('f', 64);
        return root.ToJsonString();
    }

    internal string[] Snapshot() => Directory.EnumerateFileSystemEntries(Root, "*", SearchOption.AllDirectories)
        .OrderBy(p => p, StringComparer.Ordinal)
        .Select(p => Path.GetRelativePath(Root, p).Replace('\\', '/') + ":" +
            (Directory.Exists(p) ? "directory" : Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(p)))))
        .ToArray();

    public async ValueTask DisposeAsync()
    {
        Exception? failure = null;
        bool inactive = false;
        try
        {
            if (Files is not null)
            {
                var lease = await Files.AcquireCanonicalWriteLeaseAsync(CanonicalWritePurpose.PublicationReadQuiescence);
                await lease.DisposeAsync();
                inactive = !lease.IsActive;
                Assert.True(inactive);
            }
            else inactive = true;
            if (Directory.Exists(Root)) Directory.Delete(Root, recursive: true);
        }
        catch (Exception exception) { failure = exception; }
        output.WriteLine(JsonSerializer.Serialize(new { CleanupOwnedRoot = Root, OwnedFixtureRemoved = !Directory.Exists(Root),
            LeaseInactive = inactive, CleanupFailure = failure?.ToString() }));
        Assert.Null(failure);
        Assert.False(Directory.Exists(Root));
    }
}
