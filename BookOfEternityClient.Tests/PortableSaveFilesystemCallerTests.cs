using System.Diagnostics;
using System.IO.Compression;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text.Json;
using BookOfEternityClient.Core;
using BookOfEternityClient.Models;
using BookOfEternityClient.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace BookOfEternityClient.Tests;

/// <summary>
/// Exercises pre-existing link admission through ordinary save, retention and listing callers.
/// Each test owns its complete mutable session and outside sentinels; no concurrent namespace swap is required.
/// </summary>
public sealed class PortableSaveFilesystemCallerTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "boe-save-filesystem-" + Guid.NewGuid().ToString("N"));

    /// <summary>Rejects a linked manual-save destination without publishing through it or losing the library.</summary>
    [Fact]
    public async Task PreexistingLinkedManualDestinationRejectsSaveWithoutOutsidePublication()
    {
        var files = Files();
        var state = PortableSaveFixture.Seed(files);
        SeedLibrary(files);
        var before = Snapshot(files.ResolvePath("saves"));
        var generation = File.ReadAllBytes(files.SessionGenerationPath);
        var destination = files.ResolvePath("saves/manual_saves");
        var displaced = Path.Combine(_root, "owned-manual-library");
        var outside = Outside();
        var outsideBefore = Snapshot(outside);
        Directory.Move(destination, displaced);
        await CreateDirectoryLink(destination, outside);
        try
        {
            var service = new SaveLoadService(files, state, NullLogger<SaveLoadService>.Instance);
            Assert.False(await service.SaveGameAsync("linked-destination", "static destination admission"));
            Assert.True(FileSystemManager.IsReparsePoint(destination));
            AssertSnapshot(outside, outsideBefore);
            Assert.Empty(Directory.GetFiles(outside, "*.zip"));
            Assert.Equal(generation, File.ReadAllBytes(files.SessionGenerationPath));
            AssertNoSaveScratch(files);
        }
        finally
        {
            Directory.Delete(destination);
            Directory.Move(displaced, destination);
        }
        AssertSnapshot(files.ResolvePath("saves"), before);
    }

    /// <summary>Proves the selected expired link rejects retention while the new archive remains committed.</summary>
    [Fact]
    public async Task PreexistingLinkedExpiredAutosavePreservesCommittedSaveAndOutsideTarget()
    {
        var files = Files();
        var state = PortableSaveFixture.Seed(files);
        SeedLibrary(files);
        state.Settings.MaxAutosaves = 2;
        var before = Snapshot(files.ResolvePath("saves"));
        var generation = File.ReadAllBytes(files.SessionGenerationPath);
        var outside = Outside();
        var outsideBefore = Snapshot(outside);
        var target = Path.Combine(outside, "external-payload.json");
        var expired = files.ResolvePath("saves/autosaves/expired-link.zip");
        File.CreateSymbolicLink(expired, target);
        Assert.True(FileSystemManager.IsReparsePoint(expired));
        // The production selector reads file creation time. Establish and verify an old link
        // before saving, rather than relying on enumeration order or a same-clock-tick tie.
        var old = new DateTime(2000, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        File.SetCreationTimeUtc(expired, old);
        File.SetLastWriteTimeUtc(expired, old);
        Assert.True(File.GetCreationTimeUtc(expired) < File.GetCreationTimeUtc(files.ResolvePath("saves/autosaves/existing.zip")));
        var deletionBoundary = 0;
        var service = new SaveLoadService(files, state, NullLogger<SaveLoadService>.Instance, new SaveLoadServiceHooks
        {
            BeforeAutosaveDeletionAsync = () =>
            {
                var selected = Directory.GetFiles(files.ResolvePath("saves/autosaves"), "*.zip")
                    .OrderByDescending(File.GetCreationTime).Skip(state.Settings.MaxAutosaves).ToArray();
                Assert.Equal(expired, Assert.Single(selected));
                deletionBoundary++;
                return Task.CompletedTask;
            }
        });

        var result = await service.CreateAutosaveAsync(73);

        Assert.Equal(1, deletionBoundary);
        Assert.True(result.Committed);
        Assert.True(result.NeedsFollowUp);
        Assert.False(result.ContinuationBlocked);
        Assert.IsType<InvalidDataException>(result.Failure);
        Assert.NotNull(result.DestinationRelativePath);
        var created = files.ResolvePath(result.DestinationRelativePath);
        Assert.True(FileSystemManager.IsReparsePoint(expired));
        Assert.Equal(target, new FileInfo(expired).LinkTarget);
        AssertSnapshot(outside, outsideBefore);
        Assert.Equal(generation, File.ReadAllBytes(files.SessionGenerationPath));
        AssertLibraryWithAdditions(files, before, created, expired);
        AssertValidArchive(created);
        AssertNoSaveScratch(files);
        Assert.False(File.Exists(Path.Combine(files.RuntimeRootPath, "trusted-local-publication-v1", "active.json")));
    }

    /// <summary>Excludes a linked source descendant from a successful, fully manifested archive.</summary>
    [Fact]
    public async Task LinkedSourceDescendantIsExcludedFromSuccessfulSave()
    {
        var files = Files();
        var state = PortableSaveFixture.Seed(files);
        SeedLibrary(files);
        var before = Snapshot(files.ResolvePath("saves"));
        var generation = File.ReadAllBytes(files.SessionGenerationPath);
        var outside = Outside();
        var outsideBefore = Snapshot(outside);
        var linked = files.ResolvePath("game_state/world/external-link");
        await CreateDirectoryLink(linked, outside);
        var service = new SaveLoadService(files, state, NullLogger<SaveLoadService>.Instance);

        Assert.True(await service.SaveGameAsync("no-linked-descendant", "ordinary source traversal"));

        var created = Assert.Single(Directory.GetFiles(files.ResolvePath("saves/manual_saves"), "*.zip")
            .Where(path => Path.GetFileName(path) != "existing.zip"));
        AssertValidArchive(created);
        using var archive = ZipFile.OpenRead(created);
        Assert.DoesNotContain(archive.Entries, entry => entry.FullName.Contains("external", StringComparison.OrdinalIgnoreCase));
        Assert.True(FileSystemManager.IsReparsePoint(linked));
        AssertSnapshot(outside, outsideBefore);
        Assert.Equal(generation, File.ReadAllBytes(files.SessionGenerationPath));
        AssertLibraryWithAdditions(files, before, created);
        AssertNoSaveScratch(files);
    }

    /// <summary>Preserves the established uncertainty outcome for a linked mandatory source root.</summary>
    [Fact]
    public async Task LinkedMandatorySourceRootRejectsSaveWithoutArchiveOrOutsideChanges()
    {
        var files = Files();
        var state = PortableSaveFixture.Seed(files);
        SeedLibrary(files);
        var before = Snapshot(files.ResolvePath("saves"));
        var generation = File.ReadAllBytes(files.SessionGenerationPath);
        var outside = Outside();
        var outsideBefore = Snapshot(outside);
        var source = files.ResolvePath("game_state");
        var displaced = Path.Combine(_root, "owned-game-state");
        Directory.Move(source, displaced);
        await CreateDirectoryLink(source, outside);
        try
        {
            var service = new SaveLoadService(files, state, NullLogger<SaveLoadService>.Instance);
            await Assert.ThrowsAsync<CoordinatedStatePublicationUncertainException>(() =>
                service.SaveGameAsync("linked-mandatory-root", "static source admission"));
            Assert.True(FileSystemManager.IsReparsePoint(source));
            AssertSnapshot(outside, outsideBefore);
            AssertSnapshot(files.ResolvePath("saves"), before);
            Assert.Equal(generation, File.ReadAllBytes(files.SessionGenerationPath));
            AssertNoSaveScratch(files);
        }
        finally
        {
            Directory.Delete(source);
            Directory.Move(displaced, source);
        }
    }

    /// <summary>Lists an already hard-linked archive once, preserving exact metadata, length and both names' bytes.</summary>
    [Fact]
    public async Task PublicListingAcceptsPreexistingHardLinkedArchiveWithOneOpen()
    {
        var opened = 0;
        string? archiveRelative = null;
        var files = Files(new FileSystemManagerHooks
        {
            AfterCanonicalReadInitialValidationAsync = path =>
            {
                if (path.Replace('\\', '/') == archiveRelative) opened++;
                return Task.CompletedTask;
            }
        });
        var state = PortableSaveFixture.Seed(files);
        var service = new SaveLoadService(files, state, NullLogger<SaveLoadService>.Instance);
        Assert.True(await service.SaveGameAsync("hard-linked-listing", "exact ordinary metadata", turnNumber: 12));
        var archivePath = Assert.Single(Directory.GetFiles(files.ResolvePath("saves/manual_saves"), "*.zip"));
        archiveRelative = Path.GetRelativePath(files.GameSessionPath, archivePath).Replace('\\', '/');
        AssertValidArchive(archivePath);
        var alias = Path.Combine(_root, "outside-archive-alias.zip");
        MakeHardLink(alias, archivePath);
        var before = File.ReadAllBytes(archivePath);
        var library = Snapshot(files.ResolvePath("saves"));
        var generation = File.ReadAllBytes(files.SessionGenerationPath);
        SaveMetadata expected;
        using (var archive = ZipFile.OpenRead(archivePath))
        using (var stream = archive.GetEntry("save_metadata.json")!.Open())
            expected = JsonSerializer.Deserialize<SaveMetadata>(stream)!;

        var save = Assert.Single(await service.GetAvailableSavesAsync());

        Assert.Equal(archivePath, save.FileName);
        Assert.NotNull(save.Metadata);
        Assert.Equal(JsonSerializer.Serialize(expected), JsonSerializer.Serialize(save.Metadata));
        Assert.Equal("hard-linked-listing", save.Metadata.SaveName);
        Assert.Equal("exact ordinary metadata", save.Metadata.Description);
        Assert.Equal(12, save.Metadata.TurnNumber);
        Assert.Equal(before.LongLength, save.FileSize);
        Assert.Equal(1, opened);
        Assert.Equal(before, File.ReadAllBytes(archivePath));
        Assert.Equal(before, File.ReadAllBytes(alias));
        Assert.Equal(generation, File.ReadAllBytes(files.SessionGenerationPath));
        AssertSnapshot(files.ResolvePath("saves"), library);
    }

    private FileSystemManager Files(FileSystemManagerHooks? hooks = null) =>
        new(_root, NullLogger<FileSystemManager>.Instance, PhysicalLoadTransactionOperations.Instance, hooks);

    private string Outside()
    {
        var outside = Path.Combine(_root, "outside");
        Directory.CreateDirectory(outside);
        File.WriteAllText(Path.Combine(outside, "external-payload.json"), "{\"mustNotArchive\":true}");
        File.WriteAllBytes(Path.Combine(outside, "sentinel.bin"), [71, 0, 255]);
        return outside;
    }

    private static void SeedLibrary(FileSystemManager files)
    {
        foreach (var directory in new[] { "manual_saves", "autosaves", "checkpoint_saves" })
            File.WriteAllBytes(files.ResolvePath($"saves/{directory}/existing.zip"), [19, 0, 254]);
    }

    private static Dictionary<string, byte[]> Snapshot(string root) => Directory.GetFiles(root, "*", SearchOption.AllDirectories)
        .ToDictionary(path => Path.GetRelativePath(root, path), File.ReadAllBytes, StringComparer.Ordinal);

    private static void AssertSnapshot(string root, Dictionary<string, byte[]> expected)
    {
        var actual = Snapshot(root);
        Assert.Equal(expected.Keys.Order(StringComparer.Ordinal), actual.Keys.Order(StringComparer.Ordinal));
        foreach (var (path, bytes) in expected) Assert.Equal(bytes, actual[path]);
    }

    private static void AssertLibraryWithAdditions(FileSystemManager files, Dictionary<string, byte[]> before, params string[] additions)
    {
        var root = files.ResolvePath("saves");
        var expectedNames = before.Keys.Concat(additions.Select(path => Path.GetRelativePath(root, path)));
        Assert.Equal(expectedNames.Order(StringComparer.Ordinal), Directory.GetFiles(root, "*", SearchOption.AllDirectories)
            .Select(path => Path.GetRelativePath(root, path)).Order(StringComparer.Ordinal));
        foreach (var (path, bytes) in before) Assert.Equal(bytes, File.ReadAllBytes(Path.Combine(root, path)));
    }

    private static void AssertNoSaveScratch(FileSystemManager files)
    {
        var scratch = Path.Combine(files.RuntimeRootPath, "save-staging");
        if (Directory.Exists(scratch)) Assert.Empty(Directory.EnumerateFileSystemEntries(scratch));
    }

    private static void AssertValidArchive(string path)
    {
        TrustedLocalStreamResourceProbe.ValidateArchive(path);
        using var archive = ZipFile.OpenRead(path);
        using var manifestStream = archive.GetEntry("save_manifest.json")!.Open();
        using var manifest = JsonDocument.Parse(manifestStream);
        Assert.Equal(1, manifest.RootElement.GetProperty("schemaVersion").GetInt32());
        Assert.Equal("SHA-256", manifest.RootElement.GetProperty("algorithm").GetString());
        var entries = manifest.RootElement.GetProperty("entries").EnumerateArray()
            .ToDictionary(entry => entry.GetProperty("path").GetString()!, StringComparer.Ordinal);
        var payload = archive.Entries.Where(entry => entry.Name.Length > 0 && entry.FullName != "save_manifest.json").ToArray();
        Assert.Equal(payload.Length, entries.Count);
        Assert.Contains("game_state/meta/soul_state.json", entries.Keys);
        Assert.Contains(CanonicalResourceOwnerAuthorityComposer.AuthorityPath, entries.Keys);
        foreach (var entry in payload)
        {
            using var stream = entry.Open();
            Assert.Equal(entry.Length, entries[entry.FullName].GetProperty("length").GetInt64());
            Assert.Equal(entries[entry.FullName].GetProperty("sha256").GetString(), Convert.ToHexString(SHA256.HashData(stream)), ignoreCase: true);
        }
    }

    private static async Task CreateDirectoryLink(string path, string target)
    {
        if (!OperatingSystem.IsWindows()) { Directory.CreateSymbolicLink(path, target); return; }
        var start = new ProcessStartInfo("cmd.exe", $"/c mklink /J \"{path}\" \"{target}\"")
        { UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true };
        using var process = Process.Start(start) ?? throw new InvalidOperationException("Owned junction fixture did not start.");
        var stdout = process.StandardOutput.ReadToEndAsync();
        var stderr = process.StandardError.ReadToEndAsync();
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        try { await process.WaitForExitAsync(deadline.Token); }
        finally { if (!process.HasExited) { process.Kill(entireProcessTree: true); await process.WaitForExitAsync(); } }
        Assert.True(process.ExitCode == 0, await stdout + await stderr);
    }

    private static void MakeHardLink(string created, string existing)
    {
        if (OperatingSystem.IsWindows()) Assert.True(CreateHardLink(created, existing, IntPtr.Zero));
        else Assert.Equal(0, Link(existing, created));
    }

    [DllImport("kernel32.dll", EntryPoint = "CreateHardLinkW", CharSet = CharSet.Unicode, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool CreateHardLink(string created, string existing, IntPtr security);

    [DllImport("libc", EntryPoint = "link", SetLastError = true)]
    private static extern int Link(string existing, string created);

    /// <summary>Removes only this test's owned synthetic state after all streams and leases have closed.</summary>
    public void Dispose() { if (Directory.Exists(_root)) Directory.Delete(_root, recursive: true); }
}
