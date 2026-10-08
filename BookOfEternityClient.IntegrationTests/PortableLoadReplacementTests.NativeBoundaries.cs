using System.Buffers.Binary;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text.Json;
using BookOfEternityClient.Core;
using BookOfEternityClient.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed partial class PortableLoadReplacementTests
{
    /// <summary>
    /// Loads an actual selected archive inside the opaque canonical library without replacing its source or siblings.
    /// </summary>
    /// <returns>
    /// Completion after committed bytes, exact selected source, complete library and generation assertions.
    /// </returns>
    [Fact]
    public async Task LibrarySelectedSourceCommitsWithoutChangingOpaqueLibrary()
    {
        var source = await PrepareCurrentArchiveAsync(selectedRelativePath: "saves/manual_saves/selected-source.zip");
        var protectedFiles = SnapshotLibraryAndSource(source);
        var libraryDirectories = Directory.EnumerateDirectories(_files.ResolvePath("saves"), "*", SearchOption.AllDirectories)
            .Order(StringComparer.Ordinal).ToArray();
        var generation = File.ReadAllBytes(_files.SessionGenerationPath);

        var result = await _service.LoadGameWithOutcomeAsync(source);

        Report(result);
        Assert.Equal(LoadReplacementDisposition.Committed, result.Disposition);
        Assert.False(result.ContinuationBlocked);
        Assert.False(result.NeedsFollowUp);
        Assert.Equal(source, result.SelectedSourcePath);
        Assert.Equal(_loadedMarker, File.ReadAllBytes(_files.ResolvePath(MarkerPath)));
        Assert.Equal(_loadedSoul, File.ReadAllBytes(_files.ResolvePath(SoulPath)));
        Assert.False(generation.SequenceEqual(File.ReadAllBytes(_files.SessionGenerationPath)));
        AssertPreserved(protectedFiles);
        Assert.Equal(protectedFiles.Keys.Order(StringComparer.Ordinal),
            Directory.EnumerateFiles(_files.ResolvePath("saves"), "*", SearchOption.AllDirectories).Order(StringComparer.Ordinal));
        Assert.Equal(libraryDirectories, Directory.EnumerateDirectories(_files.ResolvePath("saves"), "*", SearchOption.AllDirectories)
            .Order(StringComparer.Ordinal).ToArray());
        AssertOwnedScratchEmpty();
    }

    /// <summary>
    /// Replaces the old marker by name while a real native hard-link alias outside the session retains its exact bytes.
    /// </summary>
    /// <returns>
    /// Completion after native link creation, committed marker replacement and unchanged alias/source/library assertions.
    /// </returns>
    [Fact]
    public async Task NativeHardLinkedMarkerReplacementPreservesOutsideSessionAlias()
    {
        var source = await PrepareCurrentArchiveAsync();
        var marker = _files.ResolvePath(MarkerPath);
        var oldMarker = File.ReadAllBytes(marker);
        var outside = CreateLoadNativeOutsideDirectory("hard-link");
        var alias = Path.Combine(outside, "marker-alias.bin");
        CreateLoadNativeHardLink(alias, marker);
        Assert.Equal(oldMarker, File.ReadAllBytes(alias));
        var protectedFiles = SnapshotLibraryAndSource(source);

        var result = await _service.LoadGameWithOutcomeAsync(source);

        Report(result);
        Assert.Equal(LoadReplacementDisposition.Committed, result.Disposition);
        Assert.False(result.ContinuationBlocked);
        Assert.Equal(_loadedMarker, File.ReadAllBytes(marker));
        Assert.Equal(oldMarker, File.ReadAllBytes(alias));
        AssertPreserved(protectedFiles);
        AssertOwnedScratchEmpty();
    }

    /// <summary>
    /// Refuses an actual directory junction or symbolic link at each namespace admission boundary.
    /// </summary>
    /// <param name="position">
    /// Places the link at the marker target, marker ancestor, selected source ancestor or opaque library root.
    /// </param>
    /// <returns>
    /// Completion after admission refusal, exact outside bytes, original restored fixture namespace and no publication.
    /// </returns>
    [Theory]
    [InlineData("target")]
    [InlineData("ancestor")]
    [InlineData("source")]
    [InlineData("library")]
    public async Task NativeDirectoryLinkBoundaryRefusesBeforeSessionPublication(string position)
    {
        var source = await PrepareCurrentArchiveAsync(selectedRelativePath: "imports/source.zip");
        var marker = _files.ResolvePath(MarkerPath);
        var linkPath = position switch
        {
            "target" => marker,
            "ancestor" => Path.GetDirectoryName(marker)!,
            "source" => Path.GetDirectoryName(source)!,
            "library" => _files.ResolvePath("saves"),
            _ => throw new InvalidOperationException("Unknown owned native link position.")
        };
        var before = SnapshotCompleteNamespace();
        var generation = File.ReadAllBytes(_files.SessionGenerationPath);
        var outside = CreateLoadNativeOutsideDirectory(position);
        byte[] sentinel = [0xFF, 0, 71];
        File.WriteAllBytes(Path.Combine(outside, "sentinel.bin"), sentinel);
        File.WriteAllBytes(Path.Combine(outside, "load-marker.bin"), File.ReadAllBytes(marker));
        if (position == "source") File.Copy(source, Path.Combine(outside, "source.zip"));
        var outsideBefore = Snapshot(outside);
        var backup = Path.Combine(_root, "native-boundaries", position, "original-entry");
        AssertLoadNativeOwnedPath(linkPath);
        AssertLoadNativeOwnedPath(backup);
        var directory = Directory.Exists(linkPath);
        if (directory) Directory.Move(linkPath, backup);
        else File.Move(linkPath, backup);
        try
        {
            await CreateLoadNativeDirectoryLinkAsync(linkPath, outside);

            var result = await _service.LoadGameWithOutcomeAsync(source);

            Report(result);
            Assert.Equal(LoadReplacementDisposition.NotLoaded, result.Disposition);
            Assert.NotNull(result.Failure);
            Assert.Null(result.EstablishedGeneration);
            Assert.Empty(_phases);
            Assert.Equal(generation, File.ReadAllBytes(_files.SessionGenerationPath));
            Assert.Equal("Old live soul", _state.CurrentState.SoulName);
            AssertPreserved(outsideBefore);
            Assert.Equal(outsideBefore.Keys.Order(StringComparer.Ordinal), Snapshot(outside).Keys.Order(StringComparer.Ordinal));
        }
        finally
        {
            if (Directory.Exists(linkPath)) RemoveLoadNativeDirectoryLink(linkPath);
            if (directory) Directory.Move(backup, linkPath);
            else File.Move(backup, linkPath);
        }
        Assert.Equal(before.OrderBy(pair => pair.Key).ToArray(), SnapshotCompleteNamespace().OrderBy(pair => pair.Key).ToArray());
        AssertOwnedScratchEmpty();
    }

    /// <summary>
    /// Blocks publication and recovery when an exact transaction-owned file stage becomes a native directory link.
    /// </summary>
    /// <returns>
    /// Completion after uncertainty retains intent/before/outside evidence, followed by exact link removal and fresh rollback recovery.
    /// </returns>
    [Fact]
    public async Task NativeExactStageJunctionRetainsIntentUntilRemovedAndFreshlyRecovered()
    {
        var source = await PrepareCurrentArchiveAsync();
        var before = SnapshotCompleteNamespace();
        var beforeFiles = Snapshot(_files.GameSessionPath);
        var generation = File.ReadAllBytes(_files.SessionGenerationPath);
        var outside = CreateLoadNativeOutsideDirectory("exact-stage");
        File.WriteAllBytes(Path.Combine(outside, "sentinel.bin"), [0xEF, 0xBB, 0xBF, 0, 0xFF]);
        var outsideBefore = Snapshot(outside);
        var active = Path.Combine(_files.RuntimeRootPath, "trusted-local-publication-v1", "active.json");
        string? stage = null;
        string? evidenceHash = null;
        _fault = (phase, _) =>
        {
            if (phase != TrustedLocalPublicationPhase.IntentPublished || stage != null) return;
            stage = ReadLoadNativeMarkerStagePath();
            CreateLoadNativeDirectoryLinkAsync(stage, outside).GetAwaiter().GetResult();
            evidenceHash = Hash(active);
        };
        try
        {
            var result = await _service.LoadGameWithOutcomeAsync(source);

            Report(result);
            Assert.NotNull(stage);
            Assert.Equal(LoadReplacementDisposition.Uncertain, result.Disposition);
            Assert.True(result.ContinuationBlocked);
            Assert.True(result.NeedsFollowUp);
            Assert.Null(result.EstablishedGeneration);
            Assert.NotNull(result.Failure);
            Assert.True(File.Exists(active));
            Assert.Equal(evidenceHash, Hash(active));
            Assert.DoesNotContain(_phases, value => value.Phase == TrustedLocalPublicationPhase.MemberPublished);
            AssertPreserved(beforeFiles);
            Assert.Equal(generation, File.ReadAllBytes(_files.SessionGenerationPath));
            AssertPreserved(outsideBefore);
            await Assert.ThrowsAsync<InvalidDataException>(async () =>
            { await using var blocked = await FreshLoadNativeManager().AcquireCanonicalWriteLeaseAsync(); });
            Assert.Equal(evidenceHash, Hash(active));

            RemoveLoadNativeDirectoryLink(stage!);
            stage = null;
            _fault = null;
            await using (var recovered = await FreshLoadNativeManager().AcquireCanonicalWriteLeaseAsync()) { }

            Assert.False(File.Exists(active));
            Assert.Equal(generation, File.ReadAllBytes(_files.SessionGenerationPath));
            Assert.Equal(before.OrderBy(pair => pair.Key).ToArray(), SnapshotCompleteNamespace().OrderBy(pair => pair.Key).ToArray());
            AssertPreserved(outsideBefore);
            Assert.Empty(Directory.EnumerateFileSystemEntries(Path.GetDirectoryName(active)!));
            Assert.Empty(Directory.EnumerateFiles(_files.GameSessionPath, ".boe-local-*", SearchOption.AllDirectories));
            // Uncertain load deliberately retains private extraction evidence; fixture disposal owns that residue.
        }
        finally
        {
            _fault = null;
            if (stage != null && Directory.Exists(stage)) RemoveLoadNativeDirectoryLink(stage);
        }
    }

    /// <summary>
    /// Retains a committed decision when a late callback introduces an undeclared empty directory blocking cleanup.
    /// </summary>
    /// <param name="cut">
    /// Introduces the unknown empty child after commit or after one cleanup-member operation.
    /// </param>
    /// <returns>
    /// Completion after committed bytes/generation and blocking evidence survive, then fresh cleanup proceeds without rollback.
    /// </returns>
    [Theory]
    [InlineData("Committed")]
    [InlineData("CleanupMember")]
    public async Task CommittedUnknownEmptyDirectoryDebtPreservesDecisionUntilFreshCleanup(string cut)
    {
        var source = await PrepareCurrentArchiveAsync();
        var protectedFiles = SnapshotLibraryAndSource(source);
        var generation = File.ReadAllBytes(_files.SessionGenerationPath);
        var unknown = _files.ResolvePath("lore/native-unknown-empty");
        var active = Path.Combine(_files.RuntimeRootPath, "trusted-local-publication-v1", "active.json");
        var reached = false;
        _fault = (phase, _) =>
        {
            if (reached || phase.ToString() != cut) return;
            reached = true;
            Directory.CreateDirectory(unknown);
        };
        try
        {
            var result = await _service.LoadGameWithOutcomeAsync(source);

            Report(result);
            Assert.True(reached, $"The actual {cut} callback was not reached.");
            Assert.Equal(LoadReplacementDisposition.Committed, result.Disposition);
            Assert.True(result.NeedsFollowUp);
            Assert.True(result.ContinuationBlocked);
            Assert.NotNull(result.Failure);
            Assert.NotNull(result.EstablishedGeneration);
            Assert.Equal(_loadedMarker, File.ReadAllBytes(_files.ResolvePath(MarkerPath)));
            Assert.Equal(_loadedSoul, File.ReadAllBytes(_files.ResolvePath(SoulPath)));
            var committedGeneration = File.ReadAllBytes(_files.SessionGenerationPath);
            Assert.False(generation.SequenceEqual(committedGeneration));
            using (var header = ReadLoadNativeActiveHeader()) Assert.True(header.RootElement.GetProperty("Committed").GetBoolean());
            var evidenceHash = Hash(active);
            Assert.Empty(Directory.EnumerateFileSystemEntries(unknown));
            AssertPreserved(protectedFiles);
            await Assert.ThrowsAsync<InvalidDataException>(async () =>
            { await using var blocked = await FreshLoadNativeManager().AcquireCanonicalWriteLeaseAsync(); });
            Assert.Equal(evidenceHash, Hash(active));
            Assert.Equal(committedGeneration, File.ReadAllBytes(_files.SessionGenerationPath));

            Directory.Delete(unknown, recursive: false);
            _fault = null;
            await using (var recovered = await FreshLoadNativeManager().AcquireCanonicalWriteLeaseAsync()) { }

            Assert.Equal(committedGeneration, File.ReadAllBytes(_files.SessionGenerationPath));
            Assert.Equal(_loadedMarker, File.ReadAllBytes(_files.ResolvePath(MarkerPath)));
            Assert.Equal(_loadedSoul, File.ReadAllBytes(_files.ResolvePath(SoulPath)));
            Assert.False(File.Exists(_files.ResolvePath(OldOnlyPath)));
            AssertPreserved(protectedFiles);
            AssertOwnedScratchEmpty();
        }
        finally
        {
            _fault = null;
            if (Directory.Exists(unknown)) Directory.Delete(unknown, recursive: false);
        }
    }

    /// <summary>
    /// Preserves an established committed load when its plain commit callback throws before cleanup.
    /// </summary>
    /// <returns>
    /// Completion after committed files and generation survive follow-up debt and a fresh recovery never restores old bytes.
    /// </returns>
    [Fact]
    public async Task PlainCommittedCallbackFailureKeepsEstablishedLoadDecision()
    {
        var source = await PrepareCurrentArchiveAsync();
        var protectedFiles = SnapshotLibraryAndSource(source);
        var reached = false;
        _fault = (phase, _) =>
        {
            if (reached || phase != TrustedLocalPublicationPhase.Committed) return;
            reached = true;
            throw new InvalidOperationException("Owned committed callback debt");
        };

        var result = await _service.LoadGameWithOutcomeAsync(source);

        Report(result);
        Assert.True(reached);
        Assert.Equal(LoadReplacementDisposition.Committed, result.Disposition);
        Assert.True(result.NeedsFollowUp);
        Assert.NotNull(result.Failure);
        Assert.NotNull(result.EstablishedGeneration);
        Assert.Equal(_loadedMarker, File.ReadAllBytes(_files.ResolvePath(MarkerPath)));
        var committedGeneration = File.ReadAllBytes(_files.SessionGenerationPath);
        _fault = null;
        await using (var recovered = await FreshLoadNativeManager().AcquireCanonicalWriteLeaseAsync()) { }
        Assert.Equal(committedGeneration, File.ReadAllBytes(_files.SessionGenerationPath));
        Assert.Equal(_loadedMarker, File.ReadAllBytes(_files.ResolvePath(MarkerPath)));
        Assert.Equal(_loadedSoul, File.ReadAllBytes(_files.ResolvePath(SoulPath)));
        AssertPreserved(protectedFiles);
        AssertOwnedScratchEmpty();
    }

    /// <summary>
    /// Creates an ordinary owned sentinel directory outside GameSession without obtaining canonical mutation authority there.
    /// </summary>
    /// <param name="label">
    /// The fixed fixture label identifying this test's native boundary position.
    /// </param>
    /// <returns>
    /// The absent-then-created absolute directory inside this fixture root and outside the session.
    /// </returns>
    private string CreateLoadNativeOutsideDirectory(string label)
    {
        var path = Path.Combine(_root, "native-boundaries", label, "outside");
        AssertLoadNativeOwnedPath(path);
        Directory.CreateDirectory(path);
        Assert.False(path.StartsWith(_files.GameSessionPath + Path.DirectorySeparatorChar,
            OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal));
        return path;
    }

    /// <summary>
    /// Verifies native fixture paths before exact moves, link creation or nonrecursive link removal.
    /// </summary>
    /// <param name="path">
    /// The absolute source, target or backup path constrained to this fixture's owned root.
    /// </param>
    private void AssertLoadNativeOwnedPath(string path) => Assert.True(Path.GetFullPath(path).StartsWith(
        Path.GetFullPath(_root) + Path.DirectorySeparatorChar,
        OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal));

    /// <summary>
    /// Creates an actual Windows junction or Linux directory symbolic link using an independently owned native operation.
    /// </summary>
    /// <param name="path">
    /// The absent exact link name inside the fixture root.
    /// </param>
    /// <param name="target">
    /// The existing independently owned outside-session directory.
    /// </param>
    /// <returns>
    /// Completion after actual reparse-point creation and bounded owned-process cleanup.
    /// </returns>
    private async Task CreateLoadNativeDirectoryLinkAsync(string path, string target)
    {
        AssertLoadNativeOwnedPath(path);
        AssertLoadNativeOwnedPath(target);
        Assert.False(Directory.Exists(path));
        Assert.False(File.Exists(path));
        if (OperatingSystem.IsWindows())
        {
            var start = new ProcessStartInfo("cmd.exe", $"/d /c mklink /J \"{path}\" \"{target}\"")
            { UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true };
            using var process = Process.Start(start) ?? throw new InvalidOperationException("Owned native load junction process did not start.");
            var stdout = process.StandardOutput.ReadToEndAsync();
            var stderr = process.StandardError.ReadToEndAsync();
            using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(10));
            try { await process.WaitForExitAsync(deadline.Token).ConfigureAwait(false); }
            finally
            {
                if (!process.HasExited)
                {
                    process.Kill(entireProcessTree: true);
                    await process.WaitForExitAsync().ConfigureAwait(false);
                }
            }
            Assert.True(process.ExitCode == 0, await stdout.ConfigureAwait(false) + await stderr.ConfigureAwait(false));
            Assert.True(process.HasExited);
        }
        else
        {
            Assert.True(OperatingSystem.IsLinux(), "This category requires native Windows or Linux filesystem behavior.");
            Directory.CreateSymbolicLink(path, target);
        }
        Assert.True(Directory.Exists(path));
        Assert.True(File.GetAttributes(path).HasFlag(FileAttributes.ReparsePoint));
        _output.WriteLine("Actual native directory link created: OS={0}; name={1}; ownedTarget={2}",
            OperatingSystem.IsWindows() ? "Windows/junction" : "Linux/symlink", path, target);
    }

    /// <summary>
    /// Removes only one verified exact native link name without following or recursively deleting its target.
    /// </summary>
    /// <param name="path">
    /// The exact owned directory junction or symbolic-link name.
    /// </param>
    private void RemoveLoadNativeDirectoryLink(string path)
    {
        AssertLoadNativeOwnedPath(path);
        Assert.True(File.GetAttributes(path).HasFlag(FileAttributes.ReparsePoint));
        Directory.Delete(path, recursive: false);
        Assert.False(Directory.Exists(path));
    }

    /// <summary>
    /// Creates a real by-name file alias through the native hard-link primitive for the current test OS.
    /// </summary>
    /// <param name="created">
    /// The absent owned alias path outside the session.
    /// </param>
    /// <param name="existing">
    /// The existing old canonical marker file.
    /// </param>
    private void CreateLoadNativeHardLink(string created, string existing)
    {
        AssertLoadNativeOwnedPath(created);
        AssertLoadNativeOwnedPath(existing);
        if (OperatingSystem.IsWindows())
            Assert.True(NativeLoadCreateHardLink(created, existing, IntPtr.Zero), $"Native hard-link error={Marshal.GetLastPInvokeError()}");
        else
        {
            Assert.True(OperatingSystem.IsLinux(), "This category requires native Windows or Linux filesystem behavior.");
            Assert.Equal(0, NativeLoadLink(existing, created));
        }
        _output.WriteLine("Actual native hard link created: OS={0}; alias={1}; original={2}",
            OperatingSystem.IsWindows() ? "Windows" : "Linux", created, existing);
    }

    /// <summary>
    /// Opens an ordinary fresh manager without the publication fault hooks from the original load fixture.
    /// </summary>
    /// <returns>
    /// A fresh production manager for normal acquisition/recovery at the exact same owned root.
    /// </returns>
    private FileSystemManager FreshLoadNativeManager() => new(_root, NullLogger<FileSystemManager>.Instance);

    /// <summary>
    /// Reads only the actual independent v3 metadata region for stable transaction/member identity inspection.
    /// </summary>
    /// <returns>
    /// A JSON document owned by the caller, containing no frame payload bytes.
    /// </returns>
    private JsonDocument ReadLoadNativeActiveHeader()
    {
        var active = Path.Combine(_files.RuntimeRootPath, "trusted-local-publication-v1", "active.json");
        using var input = File.OpenRead(active);
        Span<byte> prefix = stackalloc byte[16];
        input.ReadExactly(prefix);
        Assert.Equal("BOELP3\r\n"u8.ToArray(), prefix[..8].ToArray());
        var length = BinaryPrimitives.ReadInt64LittleEndian(prefix[8..]);
        Assert.InRange(length, 1, input.Length - 16);
        var metadata = new byte[checked((int)length)];
        input.ReadExactly(metadata);
        return JsonDocument.Parse(metadata);
    }

    /// <summary>
    /// Derives the marker's exact forward stage from immutable published transaction identity and stable node order.
    /// </summary>
    /// <returns>
    /// The exact owned stage under the nearest parent declared Directory in both snapshots.
    /// </returns>
    private string ReadLoadNativeMarkerStagePath()
    {
        using var header = ReadLoadNativeActiveHeader();
        var transaction = header.RootElement.GetProperty("TransactionId").GetString();
        Assert.False(string.IsNullOrWhiteSpace(transaction));
        var members = header.RootElement.GetProperty("Members").EnumerateArray().ToArray();
        var marker = _files.ResolvePath(MarkerPath);
        var index = Array.FindIndex(members, member => member.GetProperty("Path").GetString() == marker);
        Assert.True(index >= 0);
        var parent = Path.GetDirectoryName(marker)!;
        while (true)
        {
            var anchor = members.SingleOrDefault(member => member.GetProperty("Path").GetString() == parent);
            if (anchor.ValueKind == JsonValueKind.Object &&
                anchor.GetProperty("Before").GetProperty("Kind").GetString() == "Directory" &&
                anchor.GetProperty("After").GetProperty("Kind").GetString() == "Directory") break;
            parent = Path.GetDirectoryName(parent) ?? throw new InvalidOperationException("The marker lacks a declared stable anchor.");
        }
        return Path.Combine(parent, $".boe-local-{transaction}-{index}.stage");
    }

    /// <summary>
    /// Creates one actual Windows hard link to an existing file by name.
    /// </summary>
    /// <param name="created">
    /// The absent absolute alias path.
    /// </param>
    /// <param name="existing">
    /// The existing absolute regular file path.
    /// </param>
    /// <param name="security">
    /// The optional security attributes pointer; this fixture passes zero for default attributes.
    /// </param>
    /// <returns>
    /// <see langword="true"/> on native creation success; otherwise <see langword="false"/> with the last native error preserved.
    /// </returns>
    [DllImport("kernel32.dll", EntryPoint = "CreateHardLinkW", CharSet = CharSet.Unicode, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool NativeLoadCreateHardLink(string created, string existing, IntPtr security);

    /// <summary>
    /// Creates one actual Linux hard link to an existing file by name.
    /// </summary>
    /// <param name="existing">
    /// The existing absolute regular file path.
    /// </param>
    /// <param name="created">
    /// The absent absolute alias path.
    /// </param>
    /// <returns>
    /// Zero on native creation success, or minus one with the last native error preserved.
    /// </returns>
    [DllImport("libc", EntryPoint = "link", SetLastError = true)]
    private static extern int NativeLoadLink(string existing, string created);
}
