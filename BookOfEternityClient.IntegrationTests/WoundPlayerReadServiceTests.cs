using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using BookOfEternityClient.Core;
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
}

internal sealed class WoundPlayerReadFixture(ITestOutputHelper output) : IAsyncDisposable
{
    internal string Root { get; } = Path.Combine(Path.GetTempPath(), "boe-wound-read-" + Guid.NewGuid().ToString("N"));
    internal FileSystemManager Files { get; private set; } = null!;

    internal static async Task<WoundPlayerReadFixture> CreateAsync(ITestOutputHelper output, bool empty = false)
    {
        var fixture = new WoundPlayerReadFixture(output);
        try
        {
            Directory.CreateDirectory(fixture.Root);
            fixture.Files = new(fixture.Root, NullLogger<FileSystemManager>.Instance);
            fixture.Files.EnsureDirectoryStructure();
            var input = empty ? WoundPlayerProjectionTestData.Input() :
                WoundPlayerProjectionTestData.Input(WoundContractTestData.CreateActiveWound());
            await using var lease = await fixture.Files.AcquireCanonicalWriteLeaseAsync();
            foreach (var (path, root) in new (string, JsonObject)[]
            {
                (WoundCarrierCatalog.PlayerPath, input.Carriers.PlayerWounds!),
                (WoundCarrierCatalog.NpcPath, input.Carriers.NpcWounds!),
                (WoundCarrierCatalog.EnemiesPath, input.Carriers.EnemyCombatants!),
                (WoundCarrierCatalog.AlliesPath, input.Carriers.AllyCombatants!),
                (WoundCarrierCatalog.AfterlifeProfilesPath, input.Carriers.AfterlifeProfiles!),
                ("game_state/meta/soul_state.json", new() { ["currentRealm"] = "Mortal", ["soulName"] = "Путник" }),
                (ResourceMaterializationContract.DefinitionsPath, input.Resources.ToCanonicalRoot())
            })
                await fixture.Files.WriteFileAsync(lease, path, root.ToJsonString());
            await fixture.Files.WriteFileAsync(lease, WoundIdentityState.StatePath, WoundIdentityState.SerializeCanonical(input.Identities));
            await fixture.Files.WriteFileAsync(lease, WoundHistoryState.HistoryPath, WoundHistoryState.SerializeCanonical(input.History));
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
            "missing_profiles" => WoundCarrierCatalog.AfterlifeProfilesPath, _ => null
        };
        if (missing is not null) { Files.DeleteFile(lease, missing); return; }
        if (fault == "invalid_utf8")
        {
            await Files.WriteFileAtomicBytesAsync(lease, WoundCarrierCatalog.PlayerPath, [0xff]); return;
        }
        var path = fault == "stale_identity" ? WoundIdentityState.StatePath : WoundCarrierCatalog.PlayerPath;
        var raw = (await Files.ReadFileAsync(lease, path))!;
        var changed = fault switch
        {
            "duplicate" => raw.Replace("\"schemaVersion\":1", "\"schemaVersion\":1,\"schemaVersion\":1", StringComparison.Ordinal),
            "stale_identity" => raw.Replace("\"lastTransitionOrdinal\":1", "\"lastTransitionOrdinal\":2", StringComparison.Ordinal),
            "legacy_shape" => "{\"wounds\":[{\"woundName\":\"Нельзя читать старую форму\",\"severity\":\"moderate\"}]}",
            _ => throw new ArgumentOutOfRangeException(nameof(fault))
        };
        Assert.NotEqual(raw, changed);
        await Files.WriteFileAsync(lease, path, changed);
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
