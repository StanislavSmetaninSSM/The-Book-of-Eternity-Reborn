using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using BookOfEternityClient.Services;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed partial class PortableLoadReplacementTests
{
    /// <summary>
    /// Exercises the unedited current producer, typed replacement and second save with native Linux case pairs.
    /// </summary>
    /// <param name="upper">The first exact arbitrary payload path.</param>
    /// <param name="lower">The distinct second payload path differing in a leaf or directory component.</param>
    /// <returns>Completion after independent byte, manifest, fixed-authority and library checks.</returns>
    [Theory]
    [InlineData("lore/Pair.bin", "lore/pair.bin")]
    [InlineData("mods/Pair/member.bin", "mods/pair/member.bin")]
    public async Task CurrentProducerNativeNamesSurviveSaveTypedLoadSave(string upper, string lower)
    {
        Assert.True(OperatingSystem.IsLinux(), "This category qualifies actual case-distinct Linux files.");
        var expected = new Dictionary<string, byte[]>(StringComparer.Ordinal)
        {
            [upper] = [0xef, 0xbb, 0xbf, 0, 255, 1],
            [lower] = [],
            ["game_state/world/MyCustom.JSON"] = [254, 0, 13, 10, 2]
        };
        const string fixedAlias = "lore/CODEX_ENTRIES.JSON";
        const string fixedCanonical = "lore/codex_entries.json";
        var fixedBytes = Encoding.UTF8.GetBytes("{}\r\n");
        foreach (var (path, bytes) in expected) Put(path, bytes);
        Put(fixedAlias, fixedBytes);
        var generationBeforeSave = File.ReadAllBytes(_files.SessionGenerationPath);

        var first = await _service.CreateSaveAsync("native-first", "actual case-sensitive source");

        Assert.True(first.Committed, first.Failure?.ToString());
        Assert.False(first.NeedsFollowUp);
        Assert.False(first.ContinuationBlocked);
        Assert.Equal(generationBeforeSave, File.ReadAllBytes(_files.SessionGenerationPath));
        var source = _files.ResolvePath(first.DestinationRelativePath!);
        var original = AssertNativeArchiveManifest(source);
        foreach (var (path, bytes) in expected) Assert.Equal(bytes, original[path]);
        Assert.Equal(fixedBytes, original[fixedAlias]);
        Assert.DoesNotContain(fixedCanonical, original.Keys);
        foreach (var scope in new[] { "manual_saves", "autosaves", "checkpoint_saves" })
            Put($"saves/{scope}/native-sentinel.zip", [0xff, 0, 61]);
        var protectedFiles = SnapshotLibraryAndSource(source);
        File.Delete(_files.ResolvePath(upper));
        Put(lower, [73, 74]);
        Put("game_state/world/MyCustom.JSON", [91]);
        Put(OldOnlyPath, [0xfe, 7]);
        _observeLoad = true;

        var loaded = await _service.LoadGameWithOutcomeAsync(source);

        Report(loaded);
        Assert.Equal(LoadReplacementDisposition.Committed, loaded.Disposition);
        Assert.False(loaded.NeedsFollowUp);
        Assert.False(loaded.ContinuationBlocked);
        Assert.Equal(source, loaded.SelectedSourcePath);
        Assert.False(string.IsNullOrWhiteSpace(loaded.EstablishedGeneration));
        Assert.NotEqual(generationBeforeSave, File.ReadAllBytes(_files.SessionGenerationPath));
        Assert.Equal(1, _prepared);
        Assert.Single(_phases, phase => phase.Phase == Core.TrustedLocalPublicationPhase.Committed);
        foreach (var (path, bytes) in expected) Assert.Equal(bytes, File.ReadAllBytes(_files.ResolvePath(path)));
        Assert.Equal(fixedBytes, File.ReadAllBytes(_files.ResolvePath(fixedCanonical)));
        Assert.False(File.Exists(_files.ResolvePath(fixedAlias)));
        Assert.False(File.Exists(_files.ResolvePath(OldOnlyPath)));
        AssertPreserved(protectedFiles);
        var loadedGeneration = File.ReadAllBytes(_files.SessionGenerationPath);
        _observeLoad = false;

        var second = await _service.CreateSaveAsync("native-second", "current producer after typed replacement");

        Assert.True(second.Committed, second.Failure?.ToString());
        Assert.False(second.NeedsFollowUp);
        Assert.False(second.ContinuationBlocked);
        var roundTrip = AssertNativeArchiveManifest(_files.ResolvePath(second.DestinationRelativePath!));
        foreach (var (path, bytes) in expected) Assert.Equal(bytes, roundTrip[path]);
        Assert.Equal(fixedBytes, roundTrip[fixedCanonical]);
        Assert.DoesNotContain(fixedAlias, roundTrip.Keys);
        Assert.DoesNotContain(OldOnlyPath, roundTrip.Keys);
        Assert.Equal(loadedGeneration, File.ReadAllBytes(_files.SessionGenerationPath));
        AssertPreserved(protectedFiles);
        AssertOwnedScratchEmpty();
        Assert.Empty(Directory.EnumerateFileSystemEntries(Path.Combine(_files.RuntimeRootPath, "save-staging")));
    }

    /// <summary>
    /// Keeps fixed whole-file authority collisions rejected even though arbitrary Linux names may differ in case.
    /// </summary>
    /// <param name="canonical">A declared fixed consumer path.</param>
    /// <param name="alias">A conflicting physical file with a distinct Linux spelling.</param>
    /// <returns>Completion after refusal, exact live/generation/library preservation and owned cleanup.</returns>
    [Theory]
    [InlineData("lore/codex_entries.json", "lore/CODEX_ENTRIES.JSON")]
    [InlineData("game_state/world/world_time.json", "game_state/WORLD/WORLD_TIME.JSON")]
    public async Task CurrentProducerRejectsFixedAuthorityCaseCollision(string canonical, string alias)
    {
        Assert.True(OperatingSystem.IsLinux(), "This category requires two distinct native Linux source files.");
        Put(canonical, Encoding.UTF8.GetBytes("{}\n"));
        Put(alias, Encoding.UTF8.GetBytes("{}\r\n"));
        Put("saves/manual_saves/native-sentinel.zip", [0xfe, 0, 62]);
        var before = Snapshot(_files.GameSessionPath);
        var generation = File.ReadAllBytes(_files.SessionGenerationPath);
        _observeLoad = true;

        var result = await _service.CreateSaveAsync("native-conflict", "fixed authority collision");

        Assert.Equal(SaveCreationDisposition.NotCreated, result.Disposition);
        Assert.Contains("duplicate archive path", Assert.IsType<InvalidDataException>(result.Failure).Message);
        Assert.False(result.NeedsFollowUp);
        Assert.False(result.ContinuationBlocked);
        Assert.Equal(before.Keys.Order(), Snapshot(_files.GameSessionPath).Keys.Order());
        AssertPreserved(before);
        Assert.Equal(generation, File.ReadAllBytes(_files.SessionGenerationPath));
        Assert.Empty(_phases);
        AssertOwnedScratchEmpty();
        Assert.Empty(Directory.EnumerateFileSystemEntries(Path.Combine(_files.RuntimeRootPath, "save-staging")));
    }

    /// <summary>
    /// Verifies every original ZIP member against an independently read exact-name manifest and SHA-256 digest.
    /// </summary>
    /// <param name="path">An unedited archive emitted by the actual current producer.</param>
    /// <returns>The exact ordinal payload names and bytes, excluding only the manifest.</returns>
    private static Dictionary<string, byte[]> AssertNativeArchiveManifest(string path)
    {
        using var archive = ZipFile.OpenRead(path);
        var manifestEntry = Assert.Single(archive.Entries, entry => entry.FullName == "save_manifest.json");
        using var manifestInput = manifestEntry.Open();
        using var manifest = JsonDocument.Parse(manifestInput);
        Assert.Equal(1, manifest.RootElement.GetProperty("schemaVersion").GetInt32());
        Assert.Equal("SHA-256", manifest.RootElement.GetProperty("algorithm").GetString());
        var claims = manifest.RootElement.GetProperty("entries").EnumerateArray()
            .ToDictionary(entry => entry.GetProperty("path").GetString()!, StringComparer.Ordinal);
        var payloads = new Dictionary<string, byte[]>(StringComparer.Ordinal);
        foreach (var entry in archive.Entries.Where(entry => entry != manifestEntry))
        {
            Assert.NotEmpty(entry.Name);
            using var input = entry.Open();
            using var content = new MemoryStream();
            input.CopyTo(content);
            var bytes = content.ToArray();
            payloads.Add(entry.FullName, bytes);
            var claim = claims[entry.FullName];
            Assert.Equal(bytes.LongLength, claim.GetProperty("length").GetInt64());
            Assert.Equal(Convert.ToHexString(SHA256.HashData(bytes)), claim.GetProperty("sha256").GetString());
        }
        Assert.Equal(payloads.Keys.Order(StringComparer.Ordinal), claims.Keys.Order(StringComparer.Ordinal));
        return payloads;
    }
}
