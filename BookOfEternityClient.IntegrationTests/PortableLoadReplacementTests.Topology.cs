using System.IO.Compression;
using System.Buffers.Binary;
using System.Text.Json;
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

    /// <summary>
    /// Restores the complete original namespace after a real structural, file or commit-stage interruption.
    /// </summary>
    /// <param name="shape">
    /// Chooses file-to-directory or nonempty directory-to-file replacement.
    /// </param>
    /// <param name="cut">
    /// The actual publication boundary that throws once after its operation.
    /// </param>
    /// <returns>
    /// Completion after exact rollback of files, empty directories, generation and protected archives.
    /// </returns>
    [Theory]
    [InlineData("file-directory", "DirectoryCreated")]
    [InlineData("nonempty-directory-file", "DirectoryRemoved")]
    [InlineData("file-directory", "MemberStaged")]
    [InlineData("nonempty-directory-file", "MemberStaged")]
    [InlineData("file-directory", "MemberPublished")]
    [InlineData("nonempty-directory-file", "MemberPublished")]
    [InlineData("file-directory", "CommitStaged")]
    [InlineData("nonempty-directory-file", "CommitStaged")]
    public async Task InterruptedTopologyRestoresExactBeforeNamespace(string shape, string cut)
    {
        var (source, destination, _) = await PrepareTopologyArchiveAsync(shape);
        var before = SnapshotCompleteNamespace();
        var generation = File.ReadAllBytes(_files.SessionGenerationPath);
        var observed = false;
        _fault = (phase, index) =>
        {
            if (observed || phase.ToString() != cut) return;
            if (index >= 0)
            {
                var relative = ReadNamespaceMemberRelativePath(index);
                var expected = cut.StartsWith("Directory", StringComparison.Ordinal)
                    ? "lore/namespace-switch" : MarkerPath;
                if (relative != expected) return;
            }
            observed = true;
            throw new IOException("Owned topology interruption");
        };

        var result = await _service.LoadGameWithOutcomeAsync(source);

        Report(result);
        Assert.True(observed, $"Actual {cut} at the intended namespace member was not reached.");
        Assert.Equal(LoadReplacementDisposition.RolledBack, result.Disposition);
        Assert.False(result.ContinuationBlocked);
        Assert.True(result.NeedsFollowUp);
        Assert.NotNull(result.Failure);
        Assert.Equal(generation, File.ReadAllBytes(_files.SessionGenerationPath));
        Assert.Equal(before.OrderBy(pair => pair.Key).ToArray(),
            SnapshotCompleteNamespace().OrderBy(pair => pair.Key).ToArray());
        Assert.Equal("Old live soul", _state.CurrentState.SoulName);
        if (shape == "nonempty-directory-file")
            Assert.True(Directory.Exists(Path.Combine(destination, "old-empty")));
        AssertOwnedScratchEmpty();
    }

    /// <summary>
    /// Produces a real current archive and a distinct old namespace requiring a structural conversion.
    /// </summary>
    /// <param name="shape">
    /// Chooses the old file or nonempty directory shape.
    /// </param>
    /// <returns>
    /// The selected archive, conversion destination and exact incoming binary payload.
    /// </returns>
    private async Task<(string Source, string Destination, byte[] Incoming)> PrepareTopologyArchiveAsync(string shape)
    {
        var source = await PrepareCurrentArchiveAsync();
        const string relative = "lore/namespace-switch";
        var destination = _files.ResolvePath(relative);
        byte[] incoming = [0xFF, 0, 73];
        if (shape == "file-directory") Put(relative, [0xFE, 1]);
        else
        {
            Put(relative + "/old.bin", [0xFE, 2]);
            Directory.CreateDirectory(Path.Combine(destination, "old-empty"));
        }
        using var archive = ZipFile.Open(source, ZipArchiveMode.Update);
        archive.GetEntry("save_manifest.json")!.Delete();
        var entryPath = shape == "file-directory" ? relative + "/new.bin" : relative;
        using var payload = archive.CreateEntry(entryPath, CompressionLevel.NoCompression).Open();
        payload.Write(incoming);
        return (source, destination, incoming);
    }

    /// <summary>
    /// Captures exact fixture files and all directories, including empty directories.
    /// </summary>
    /// <returns>
    /// A fresh immutable comparison map rooted at the fixture session.
    /// </returns>
    private Dictionary<string, string> SnapshotCompleteNamespace()
    {
        var result = Snapshot(_files.GameSessionPath);
        foreach (var directory in Directory.GetDirectories(_files.GameSessionPath, "*", SearchOption.AllDirectories))
            result.Add(directory, "Directory");
        return result;
    }

    /// <summary>
    /// Inspects the actual v3 active evidence to bind an observer index to the intended path.
    /// </summary>
    /// <param name="index">
    /// The nonnegative stable member index announced by publication.
    /// </param>
    /// <returns>
    /// The exact session-relative path encoded by that member.
    /// </returns>
    private string ReadNamespaceMemberRelativePath(int index)
    {
        var active = Path.Combine(_files.RuntimeRootPath, "trusted-local-publication-v1", "active.json");
        var frame = File.ReadAllBytes(active);
        Assert.Equal("BOELP3\r\n"u8.ToArray(), frame.AsSpan(0, 8).ToArray());
        var length = checked((int)BinaryPrimitives.ReadInt64LittleEndian(frame.AsSpan(8, 8)));
        using var header = JsonDocument.Parse(frame.AsMemory(16, length));
        var path = header.RootElement.GetProperty("Members")[index].GetProperty("Path").GetString()!;
        return FileSystemManager.GetLocalRelativePath(_files.GameSessionPath, path, OperatingSystem.IsWindows());
    }
}
