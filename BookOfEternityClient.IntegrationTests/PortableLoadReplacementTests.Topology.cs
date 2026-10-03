using System.IO.Compression;
using BookOfEternityClient.Core;
using BookOfEternityClient.Services;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed partial class PortableLoadReplacementTests
{
    /// <summary>
    /// Replaces a complete session across file and directory shapes using the real ordinary-load entry.
    /// </summary>
    /// <param name="shape">
    /// Selects a file-to-directory conversion, empty or nonempty directory-to-file conversion, or harmless old-only empty directory preservation.
    /// </param>
    /// <returns>
    /// Completion after one committed decision, exact imported bytes, declared namespace reconciliation and protected source/library preservation.
    /// </returns>
    [Theory]
    [InlineData("file-directory")]
    [InlineData("empty-directory-file")]
    [InlineData("nonempty-directory-file")]
    [InlineData("preserve-empty-directory")]
    public async Task CurrentProducerLoadReconcilesCompleteNamespace(string shape)
    {
        var source = await PrepareCurrentArchiveAsync();
        const string relative = "lore/namespace-switch";
        var destination = _files.ResolvePath(relative);
        byte[] incoming = [0xEF, 0xBB, 0xBF, 0xFF, 0, 41];
        if (shape == "file-directory")
            Put(relative, [0xFE, 1, 2]);
        else
        {
            Directory.CreateDirectory(destination);
            if (shape == "nonempty-directory-file")
            {
                Put(relative + "/old.bin", [0xFF, 42]);
                Directory.CreateDirectory(Path.Combine(destination, "old-empty"));
            }
        }
        using (var archive = ZipFile.Open(source, ZipArchiveMode.Update))
        {
            archive.GetEntry("save_manifest.json")!.Delete();
            if (shape != "preserve-empty-directory")
            {
                var entryPath = shape == "file-directory" ? relative + "/new.bin" : relative;
                using var payload = archive.CreateEntry(entryPath, CompressionLevel.NoCompression).Open();
                payload.Write(incoming);
            }
        }
        var protectedFiles = SnapshotLibraryAndSource(source);
        var generation = File.ReadAllBytes(_files.SessionGenerationPath);

        var result = await _service.LoadGameWithOutcomeAsync(source);

        Report(result);
        Assert.Equal(LoadReplacementDisposition.Committed, result.Disposition);
        Assert.False(result.ContinuationBlocked);
        Assert.False(result.NeedsFollowUp);
        Assert.Equal(source, result.SelectedSourcePath);
        Assert.Equal(1, _prepared);
        Assert.Single(_phases.Where(value => value.Phase == TrustedLocalPublicationPhase.Committed));
        Assert.False(generation.SequenceEqual(File.ReadAllBytes(_files.SessionGenerationPath)));
        Assert.False(string.IsNullOrWhiteSpace(result.EstablishedGeneration));
        if (shape == "file-directory")
        {
            Assert.True(Directory.Exists(destination));
            Assert.Equal(incoming, File.ReadAllBytes(Path.Combine(destination, "new.bin")));
            Assert.Single(Directory.EnumerateFileSystemEntries(destination));
        }
        else if (shape == "preserve-empty-directory")
        {
            Assert.False(File.Exists(destination));
            Assert.True(Directory.Exists(destination));
            Assert.Empty(Directory.EnumerateFileSystemEntries(destination));
        }
        else
        {
            Assert.False(Directory.Exists(destination));
            Assert.Equal(incoming, File.ReadAllBytes(destination));
        }
        Assert.False(File.Exists(_files.ResolvePath(OldOnlyPath)));
        Assert.Equal(_loadedSoul, File.ReadAllBytes(_files.ResolvePath(SoulPath)));
        Assert.Equal("Loaded soul", _state.CurrentState.SoulName);
        AssertPreserved(protectedFiles);
        AssertOwnedScratchEmpty();
    }
}
