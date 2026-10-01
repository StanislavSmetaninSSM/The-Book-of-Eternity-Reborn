using System.Text;
using System.Text.Json;
using BookOfEternityClient.Configuration;
using BookOfEternityClient.Core;
using BookOfEternityClient.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed class PortableConsoleSettingsPreparationTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "boe-settings-prepare-" + Guid.NewGuid().ToString("N"));
    private readonly FileSystemManager _files;
    private readonly GameSettings _live = new() { MusicEnabled = false, SoundEnabled = false };
    private readonly StateManager _state;
    private readonly LocalSettingsPreparation _prepare;
    private const string Projection = LocalSettingsPreparation.ProjectionPath;
    private const string Manifest = SystemModService.ManifestPath;
    private static readonly byte[] OldProjection = [0xFE, 0, 0xFF];
    private static readonly byte[] OldManifest = [0xFF, 0xFE];

    public PortableConsoleSettingsPreparationTests()
    {
        Directory.CreateDirectory(_root);
        _files = new FileSystemManager(_root, NullLogger<FileSystemManager>.Instance);
        _state = new StateManager(_files, _live, NullLogger<StateManager>.Instance);
        _prepare = new LocalSettingsPreparation(_files, _state,
            new SystemModService(_files, _live, NullLogger<SystemModService>.Instance));
    }

    private async Task<LocalSettingsBaseline> Initialize(bool withMod = true)
    {
        var generation = await _state.BootstrapLocalStorageAsync();
        var config = EncodeUtf16("{\"language\":\"ru\",\"difficulty\":\"normal\",\"musicEnabled\":false,\"soundEnabled\":false}");
        File.WriteAllBytes(_files.ResolvePath("config.json"), config);
        File.WriteAllBytes(_files.ResolvePath(Projection), OldProjection);
        File.WriteAllBytes(_files.ResolvePath(Manifest), OldManifest);
        if (withMod) File.WriteAllText(_files.ResolvePath("mods/weather.json"),
            "{\"modId\":\"weather\",\"name\":\"Weather rules\",\"description\":\"Local test mod\"}");
        return new(generation, config);
    }

    private static GameSettings Draft() => new()
    {
        Language = "en", Difficulty = "hard", EnableQteEvents = false,
        MusicEnabled = false, SoundEnabled = false,
        EnabledSystemMods = ["weather.json", "WEATHER.JSON", "missing.md"]
    };
    private static byte[] EncodeUtf16(string text) => Encoding.Unicode.GetPreamble().Concat(Encoding.Unicode.GetBytes(text)).ToArray();
    private static JsonDocument Parse(byte[] bytes)
    {
        using var reader = new StreamReader(new MemoryStream(bytes), Encoding.UTF8, true);
        return JsonDocument.Parse(reader.ReadToEnd());
    }
    private void AssertUnchanged(LocalSettingsBaseline baseline)
    {
        Assert.Equal(baseline.ConfigBytes, File.ReadAllBytes(_files.ResolvePath("config.json")));
        Assert.Equal(OldProjection, File.ReadAllBytes(_files.ResolvePath(Projection)));
        Assert.Equal(OldManifest, File.ReadAllBytes(_files.ResolvePath(Manifest)));
        Assert.Equal("ru", _live.Language);
        Assert.Empty(_live.EnabledSystemMods);
        Assert.False(File.Exists(Path.Combine(_files.RuntimeRootPath, "trusted-local-publication-v1", "active.json")));
    }

    [Fact]
    public async Task PreparationDeclaresThreeExactMembersWithoutFilesOrRuntimeMutation()
    {
        var baseline = await Initialize();
        await using var lease = await _files.AcquireCanonicalWriteLeaseAsync();
        var result = await _prepare.PrepareAsync(lease, baseline, Draft());
        Assert.Equal(new[] { "config.json", Projection, Manifest }, result.Changes.Select(change => change.RelativePath));
        Assert.Equal(baseline.ConfigBytes, result.Changes[0].Before);
        Assert.Equal(OldProjection, result.Changes[1].Before);
        Assert.Equal(OldManifest, result.Changes[2].Before);
        Assert.All(result.Changes, change => Assert.NotNull(change.After));
        AssertUnchanged(baseline);
    }

    [Fact]
    public async Task OneModSnapshotNormalizesDetachedCandidateAndProjectionWithoutMutatingDraft()
    {
        var baseline = await Initialize(); var requested = Draft();
        await using var lease = await _files.AcquireCanonicalWriteLeaseAsync();
        var result = await _prepare.PrepareAsync(lease, baseline, requested);
        Assert.NotSame(requested, result.Settings); Assert.NotSame(_live, result.Settings);
        Assert.Equal(new[] { "weather.json" }, result.Settings.EnabledSystemMods);
        Assert.Equal(3, requested.EnabledSystemMods.Count);
        using var projection = Parse(result.Changes[1].After!);
        Assert.Equal("hard", projection.RootElement.GetProperty("difficulty").GetString());
        Assert.False(projection.RootElement.GetProperty("qteEventsEnabled").GetBoolean());
        var mod = Assert.Single(projection.RootElement.GetProperty("enabledSystemMods").EnumerateArray());
        Assert.Equal("weather", mod.GetProperty("modId").GetString());
        Assert.Equal("Weather rules", mod.GetProperty("name").GetString());
        using var manifest = Parse(result.Changes[2].After!);
        Assert.Equal(1, manifest.RootElement.GetProperty("activeCount").GetInt32());
        AssertUnchanged(baseline);
    }

    [Fact]
    public async Task PreparedSetPublishesThroughTheExistingCommonJournal()
    {
        var baseline = await Initialize();
        await using var lease = await _files.AcquireCanonicalWriteLeaseAsync();
        var prepared = await _prepare.PrepareAsync(lease, baseline, Draft());
        var outcome = await _files.PublishLocalFilesAsync(lease, prepared.Changes);
        Assert.Equal(TrustedLocalPublicationDisposition.Committed, outcome.Disposition);
        foreach (var member in prepared.Changes) Assert.Equal(member.After, File.ReadAllBytes(_files.ResolvePath(member.RelativePath)));
        Assert.Equal("ru", _live.Language); // Runtime application belongs to the later outcome consumer.
    }

    [Fact]
    public async Task EquivalentProjectionAndManifestRetainExactUtf16BytesAndWhitespace()
    {
        var baseline = await Initialize();
        await using var lease = await _files.AcquireCanonicalWriteLeaseAsync();
        var first = await _prepare.PrepareAsync(lease, baseline, Draft());
        using var projectionDocument = Parse(first.Changes[1].After!);
        using var manifestDocument = Parse(first.Changes[2].After!);
        var projection = EncodeUtf16("  \n" + projectionDocument.RootElement.GetRawText() + "\n ");
        var manifest = EncodeUtf16("\n " + manifestDocument.RootElement.GetRawText() + "\n");
        File.WriteAllBytes(_files.ResolvePath(Projection), projection); File.WriteAllBytes(_files.ResolvePath(Manifest), manifest);
        var second = await _prepare.PrepareAsync(lease, baseline, Draft());
        Assert.Equal(projection, second.Changes[1].Before); Assert.Equal(projection, second.Changes[1].After);
        Assert.Equal(manifest, second.Changes[2].Before); Assert.Equal(manifest, second.Changes[2].After);
    }

    [Fact]
    public async Task ChangedConfigBaselineIsRejectedBeforePreparation()
    {
        var baseline = await Initialize(); var changed = EncodeUtf16("{\"language\":\"en\"}");
        File.WriteAllBytes(_files.ResolvePath("config.json"), changed);
        await using var lease = await _files.AcquireCanonicalWriteLeaseAsync();
        await Assert.ThrowsAsync<LocalSettingsBaselineChangedException>(() => _prepare.PrepareAsync(lease, baseline, Draft()));
        Assert.Equal(changed, File.ReadAllBytes(_files.ResolvePath("config.json")));
        Assert.Equal(OldManifest, File.ReadAllBytes(_files.ResolvePath(Manifest)));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ChangedOrAbsentGenerationIsRejectedWithoutBootstrap(bool absent)
    {
        var baseline = await Initialize();
        if (absent) File.Delete(_files.SessionGenerationPath);
        else File.WriteAllBytes(_files.SessionGenerationPath, JsonSerializer.SerializeToUtf8Bytes(new { SchemaVersion = 1, GenerationId = Guid.NewGuid().ToString("N") }));
        var generationBefore = absent ? null : File.ReadAllBytes(_files.SessionGenerationPath);
        await using var lease = await _files.AcquireCanonicalWriteLeaseAsync();
        await Assert.ThrowsAsync<SessionReplacedException>(() => _prepare.PrepareAsync(lease, baseline, Draft()));
        Assert.Equal(generationBefore, File.Exists(_files.SessionGenerationPath) ? File.ReadAllBytes(_files.SessionGenerationPath) : null);
        AssertUnchanged(baseline);
    }

    [Fact]
    public async Task ReleasedLeaseCannotPrepareMemberImages()
    {
        var baseline = await Initialize(); var lease = await _files.AcquireCanonicalWriteLeaseAsync();
        await lease.DisposeAsync();
        await Assert.ThrowsAsync<InvalidOperationException>(() => _prepare.PrepareAsync(lease, baseline, Draft()));
        AssertUnchanged(baseline);
    }

    [Fact]
    public async Task MissingModsDirectoryIsNotCreatedDuringPreparation()
    {
        var baseline = await Initialize(withMod: false); var modsPath = _files.ResolvePath("mods");
        Directory.Delete(modsPath, recursive: true);
        await using var lease = await _files.AcquireCanonicalWriteLeaseAsync();
        var result = await _prepare.PrepareAsync(lease, baseline, Draft());
        Assert.Empty(result.Settings.EnabledSystemMods); Assert.False(Directory.Exists(modsPath));
        AssertUnchanged(baseline);
    }

    [Fact]
    public async Task LinkedModInputIsRejectedInsteadOfFollowingItsTarget()
    {
        var baseline = await Initialize(withMod: false); var outside = Path.Combine(_root, "outside.txt");
        File.WriteAllText(outside, "outside unchanged");
        File.CreateSymbolicLink(_files.ResolvePath("mods/weather.json"), outside);
        await using var lease = await _files.AcquireCanonicalWriteLeaseAsync();
        await Assert.ThrowsAsync<InvalidDataException>(() => _prepare.PrepareAsync(lease, baseline, Draft()));
        Assert.Equal("outside unchanged", File.ReadAllText(outside)); AssertUnchanged(baseline);
    }

    public void Dispose()
    {
        if (Directory.Exists(_root)) Directory.Delete(_root, recursive: true);
    }
}
