using System.Security.Cryptography;
using System.Text.Json;
using BookOfEternityClient.Core;
using BookOfEternityClient.Services;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed partial class PortableLoadReplacementTests
{
    private static Dictionary<string, byte[]> LiteralNamePayloads() => new(StringComparer.Ordinal)
    {
        [@"lore/literal\pair.bin"] = [0xef, 0xbb, 0xbf, 0, 255, 1],
        ["lore/literal/pair.bin"] = [2, 254, 0],
        [@"mods/part\leaf/member.bin"] = [3, 13, 10],
        [@"lore/trailing\"] = [4, 0, 253],
        ["lore/Case.bin"] = [5, 252],
        ["lore/case.bin"] = []
    };

    [Fact]
    public async Task HeldEnumerationPreservesLiteralAndHierarchicalNativeNames()
    {
        using var owned = new CleanupOwnedFixture(_root, _output.WriteLine);
        Assert.True(OperatingSystem.IsLinux(), "Literal backslash names require native Linux.");
        var expected = LiteralNamePayloads();
        foreach (var (path, bytes) in expected) Put(path, bytes);
        await using var lease = await _files.AcquireCanonicalWriteLeaseAsync();

        var names = _files.EnumerateFiles(lease, "*");

        Assert.Equal(names.Count, names.Distinct(StringComparer.Ordinal).Count());
        foreach (var (path, bytes) in expected)
        {
            Assert.Contains(path, names);
            Assert.Equal(bytes, await _files.ReadFileBytesAsync(lease, path));
        }
        _output.WriteLine(JsonSerializer.Serialize(new { kind = "f16-held-native-names", root = _root, names }));
    }

    [Fact]
    public async Task LiteralNativeNamesSurviveCurrentSaveTypedLoadSave()
    {
        using var owned = new CleanupOwnedFixture(_root, _output.WriteLine);
        Assert.True(OperatingSystem.IsLinux(), "Literal backslash names require native Linux.");
        var expected = LiteralNamePayloads();
        foreach (var (path, bytes) in expected) Put(path, bytes);
        var originalGeneration = File.ReadAllBytes(_files.SessionGenerationPath);

        var first = await _service.CreateSaveAsync("literal-first", "exact native payload cohort");

        Assert.True(first.Committed, first.Failure?.ToString());
        Assert.False(first.NeedsFollowUp);
        Assert.False(first.ContinuationBlocked);
        Assert.Equal(originalGeneration, File.ReadAllBytes(_files.SessionGenerationPath));
        var source = _files.ResolvePath(first.DestinationRelativePath!);
        var firstPayloads = AssertNativeArchiveManifest(source);
        foreach (var (path, bytes) in expected) Assert.Equal(bytes, firstPayloads[path]);
        foreach (var scope in new[] { "manual_saves", "autosaves", "checkpoint_saves" })
            Put($"saves/{scope}/literal-sentinel.zip", [0xff, 0, 73]);
        var protectedFiles = SnapshotLibraryAndSource(source);
        File.Delete(_files.ResolvePath(@"lore/literal\pair.bin"));
        Put("lore/literal/pair.bin", [88]);
        Put(OldOnlyPath, [89]);
        _observeLoad = true;

        var loaded = await _service.LoadGameWithOutcomeAsync(source);

        Report(loaded);
        Assert.Equal(LoadReplacementDisposition.Committed, loaded.Disposition);
        Assert.False(loaded.NeedsFollowUp);
        Assert.False(loaded.ContinuationBlocked);
        Assert.Equal(source, loaded.SelectedSourcePath);
        Assert.False(string.IsNullOrWhiteSpace(loaded.EstablishedGeneration));
        Assert.NotEqual(originalGeneration, File.ReadAllBytes(_files.SessionGenerationPath));
        Assert.Equal(1, _prepared);
        Assert.Single(_phases, phase => phase.Phase == TrustedLocalPublicationPhase.Committed);
        foreach (var (path, bytes) in expected) Assert.Equal(bytes, File.ReadAllBytes(_files.ResolvePath(path)));
        Assert.False(File.Exists(_files.ResolvePath(OldOnlyPath)));
        AssertPreserved(protectedFiles);
        var loadedGeneration = File.ReadAllBytes(_files.SessionGenerationPath);
        _observeLoad = false;

        var second = await _service.CreateSaveAsync("literal-second", "exact native payload roundtrip");

        Assert.True(second.Committed, second.Failure?.ToString());
        Assert.False(second.NeedsFollowUp);
        Assert.False(second.ContinuationBlocked);
        var secondPayloads = AssertNativeArchiveManifest(_files.ResolvePath(second.DestinationRelativePath!));
        foreach (var (path, bytes) in expected) Assert.Equal(bytes, secondPayloads[path]);
        Assert.DoesNotContain(OldOnlyPath, secondPayloads.Keys);
        Assert.Equal(loadedGeneration, File.ReadAllBytes(_files.SessionGenerationPath));
        AssertPreserved(protectedFiles);
        AssertOwnedScratchEmpty();
        Assert.Empty(Directory.EnumerateFileSystemEntries(Path.Combine(_files.RuntimeRootPath, "save-staging")));
        _output.WriteLine(JsonSerializer.Serialize(new
        {
            kind = "f16-native-save-load-save", root = _root,
            expected = expected.ToDictionary(pair => pair.Key, pair => Convert.ToHexString(SHA256.HashData(pair.Value)), StringComparer.Ordinal),
            firstNames = firstPayloads.Keys, secondNames = secondPayloads.Keys,
            originalGeneration, loadedGeneration
        }));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task IndependentlyManifestedLiteralNamesKeepExactLoadDecision(bool rollback)
    {
        using var owned = new CleanupOwnedFixture(_root, _output.WriteLine);
        Assert.True(OperatingSystem.IsLinux(), "Literal backslash names require native Linux.");
        var source = await PrepareCurrentArchiveAsync();
        var expected = LiteralNamePayloads();
        foreach (var (path, bytes) in expected) AppendManifestedPayload(source, path, bytes);
        var protectedFiles = SnapshotLibraryAndSource(source);
        Put(@"lore/literal\pair.bin", [91]);
        Put("lore/literal/pair.bin", [92]);
        var before = SnapshotCompleteNamespace();
        var generation = File.ReadAllBytes(_files.SessionGenerationPath);
        var cuts = 0;
        const string cutPath = @"lore/literal\pair.bin";
        if (rollback) _fault = (phase, index) =>
        {
            if (phase != TrustedLocalPublicationPhase.MemberPublished ||
                ReadNamespaceMemberRelativePath(index) != cutPath) return;
            Assert.Equal(expected[cutPath], File.ReadAllBytes(_files.ResolvePath(cutPath)));
            cuts++;
            throw new InvalidOperationException("actual literal-name member rollback cut");
        };

        var result = await _service.LoadGameWithOutcomeAsync(source);

        Report(result);
        Assert.Equal(rollback ? LoadReplacementDisposition.RolledBack : LoadReplacementDisposition.Committed, result.Disposition);
        Assert.Equal(1, _prepared);
        Assert.Equal(rollback, result.NeedsFollowUp);
        Assert.False(result.ContinuationBlocked);
        Assert.False(string.IsNullOrWhiteSpace(result.EstablishedGeneration));
        if (rollback)
        {
            Assert.Equal(1, cuts);
            Assert.Contains("actual literal-name member rollback cut", result.Failure!.ToString());
            Assert.Equal(before.OrderBy(pair => pair.Key).ToArray(), SnapshotCompleteNamespace().OrderBy(pair => pair.Key).ToArray());
            Assert.Equal(generation, File.ReadAllBytes(_files.SessionGenerationPath));
            Assert.DoesNotContain(_phases, phase => phase.Phase == TrustedLocalPublicationPhase.Committed);
        }
        else
        {
            Assert.Equal(0, cuts);
            foreach (var (path, bytes) in expected) Assert.Equal(bytes, File.ReadAllBytes(_files.ResolvePath(path)));
            Assert.False(File.Exists(_files.ResolvePath(OldOnlyPath)));
            Assert.NotEqual(generation, File.ReadAllBytes(_files.SessionGenerationPath));
            Assert.Single(_phases, phase => phase.Phase == TrustedLocalPublicationPhase.Committed);
        }
        AssertPreserved(protectedFiles);
        AssertOwnedScratchEmpty();
        _output.WriteLine(JsonSerializer.Serialize(new { kind = "f16-manifested-native-load", root = _root, rollback, cuts, paths = expected.Keys }));
    }
}
