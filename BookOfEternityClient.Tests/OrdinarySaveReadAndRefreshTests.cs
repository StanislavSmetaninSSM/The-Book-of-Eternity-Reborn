using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using BookOfEternityClient.Configuration;
using BookOfEternityClient.Core;
using BookOfEternityClient.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace BookOfEternityClient.Tests;

/// <summary>
/// Detects missing ordinary stream validation or physical-route profile repairs in the immediate save callers.
/// </summary>
public sealed class OrdinarySaveReadAndRefreshTests : IDisposable
{
    private const string Archive = "saves/manual_saves/ordinary-stream.zip";
    private readonly string _root = Path.Combine(Path.GetTempPath(), "boe-save-read-refresh-" + Guid.NewGuid().ToString("N"));

    /// <summary>
    /// Resolves an isolated fixture manager with optional precise boundary hooks.
    /// </summary>
    /// <param name="hooks">
    /// The fixture hooks, or null for ordinary behavior.
    /// </param>
    /// <returns>
    /// A manager scoped to this test's mutable root.
    /// </returns>
    private FileSystemManager Create(FileSystemManagerHooks? hooks = null) =>
        new(_root, NullLogger<FileSystemManager>.Instance, PhysicalLoadTransactionOperations.Instance, hooks);

    /// <summary>
    /// Proves the ordinary reader exposes a full seekable file rather than allocating a whole-image buffer.
    /// </summary>
    /// <param name="hardLinked">
    /// Adds an outside alias when true, which the ordinary trusted-local contract must accept unchanged.
    /// </param>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task OrdinaryStreamReadsFullFileAndAcceptsHardLinkedInput(bool hardLinked)
    {
        var files = Create(); files.EnsureDirectoryStructure();
        var path = files.ResolvePath(Archive);
        using (var stream = File.Create(path)) { stream.SetLength(16L * 1024 * 1024); stream.Position = stream.Length - 1; stream.WriteByte(255); }
        var outside = Path.Combine(_root, "outside-archive.zip");
        if (hardLinked) MakeHardLink(outside, path);
        await using var lease = await files.AcquireCanonicalWriteLeaseAsync();
        await using var opened = await files.OpenOrdinaryReadFileAsync(lease, Archive);
        Assert.NotNull(opened);
        Assert.IsType<FileStream>(opened.Stream);
        Assert.Equal(16L * 1024 * 1024, opened.Length);
        Assert.True(opened.Stream.CanSeek); Assert.Equal(0, opened.Stream.Position);
        opened.Stream.Position = opened.Length - 1; Assert.Equal(255, opened.Stream.ReadByte());
        opened.Complete();
        Assert.True(lease.IsActive);
        if (hardLinked)
        {
            using var alias = File.OpenRead(outside); Assert.Equal(opened.Length, alias.Length);
            alias.Position = alias.Length - 1; Assert.Equal(255, alias.ReadByte());
        }
    }

    /// <summary>
    /// Detects completion that admits a linked or wrong-type replacement after the stream has been consumed.
    /// </summary>
    /// <param name="linked">
    /// Replaces the name with a directory link when true, or an ordinary directory otherwise.
    /// </param>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task OrdinaryStreamCompletionRejectsLinkedOrWrongTypeName(bool linked)
    {
        var files = Create(); files.EnsureDirectoryStructure(); var path = files.ResolvePath(Archive);
        File.WriteAllBytes(path, [1, 0, 255]);
        var outside = Path.Combine(_root, "outside-directory"); Directory.CreateDirectory(outside);
        File.WriteAllBytes(Path.Combine(outside, "sentinel.bin"), [7, 0, 255]);
        await using var lease = await files.AcquireCanonicalWriteLeaseAsync();
        await using var opened = await files.OpenOrdinaryReadFileAsync(lease, Archive);
        Assert.NotNull(opened); Assert.Equal(1, opened.Stream.ReadByte());
        File.Delete(path);
        if (linked) await CreateDirectoryLink(path, outside); else Directory.CreateDirectory(path);
        Assert.Throws<InvalidDataException>(() => opened.Complete());
        opened.Abandon();
        Assert.Equal(new byte[] { 7, 0, 255 }, File.ReadAllBytes(Path.Combine(outside, "sentinel.bin")));
        Directory.Delete(path); // Remove only the fixture-created empty directory or link name.
    }

    /// <summary>
    /// Detects profile repair using physical receipts or reacquiring the caller's already-held canonical lease.
    /// </summary>
    [Fact]
    public async Task ProfileRepairPublishesOneCommonDecisionOnTheSuppliedLease()
    {
        var commits = 0; var physical = 0; var leases = 0;
        var files = Create(new FileSystemManagerHooks
        {
            BeforeCanonicalWriteLockOpenAsync = () => { leases++; return Task.CompletedTask; },
            AfterPhysicalFilePublishedAsync = _ => { physical++; return Task.CompletedTask; },
            LocalPublicationObserver = (phase, _) => { if (phase == TrustedLocalPublicationPhase.Committed) commits++; }
        });
        SeedProfile(files);
        var generation = File.ReadAllBytes(files.SessionGenerationPath);
        var manager = new StateManager(files, new GameSettings(), NullLogger<StateManager>.Instance);
        await using var lease = await files.AcquireCanonicalWriteLeaseAsync();
        await manager.RefreshGameStateAsync(lease);
        Assert.Equal(1, commits); Assert.Equal(0, physical); Assert.Equal(1, leases);
        using var profile = JsonDocument.Parse(File.ReadAllText(files.ResolvePath(AfterlifeEntityProfileState.StatePath)));
        var player = profile.RootElement.GetProperty("profiles")[0];
        Assert.Equal(17, player.GetProperty("currencies").GetProperty("inkFeathers").GetInt32());
        Assert.Equal(0, player.GetProperty("standardArts").GetProperty("guard").GetInt32());
        Assert.Equal("preserved", player.GetProperty("gmRevision").GetString());
        Assert.Equal(generation, File.ReadAllBytes(files.SessionGenerationPath));
        Assert.Empty(Directory.EnumerateFileSystemEntries(Path.Combine(files.RuntimeRootPath, "trusted-local-publication-v1")));
    }

    /// <summary>
    /// Detects an unnecessary publication or encoding rewrite when the pure player projection is already current.
    /// </summary>
    [Fact]
    public async Task MatchingProfileRefreshPreservesExactBomBytesWithoutPublication()
    {
        var commits = 0;
        var files = Create(new FileSystemManagerHooks { LocalPublicationObserver = (phase, _) =>
            { if (phase == TrustedLocalPublicationPhase.Committed) commits++; } });
        SeedProfile(files);
        var profilePath = files.ResolvePath(AfterlifeEntityProfileState.StatePath);
        var root = JsonNode.Parse(File.ReadAllText(profilePath))!.AsObject();
        var soul = JsonNode.Parse(File.ReadAllText(files.ResolvePath("game_state/meta/soul_state.json")))!.AsObject();
        AfterlifeEntityProfileState.ApplyPlayerSoulProfileClientAuthority(root, soul, null);
        var text = " \n" + root.ToJsonString() + "\n ";
        var bytes = Encoding.Unicode.GetPreamble().Concat(Encoding.Unicode.GetBytes(text)).ToArray();
        File.WriteAllBytes(profilePath, bytes);
        var manager = new StateManager(files, new GameSettings(), NullLogger<StateManager>.Instance);
        await manager.RefreshGameStateAsync();
        Assert.Equal(0, commits); Assert.Equal(bytes, File.ReadAllBytes(profilePath));
    }

    /// <summary>
    /// Detects refresh swallowing an uncertain common repair result and presenting a newly aggregated healthy state.
    /// </summary>
    /// <param name="suppliedLease">
    /// Uses the caller's explicit lease when true, or the public owned-lease refresh otherwise.
    /// </param>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task UncertainMirrorRepairPropagatesWithoutRefreshingRuntime(bool suppliedLease)
    {
        FileSystemManager? files = null;
        var reached = 0; var unknown = Encoding.UTF8.GetBytes("{\"unknown\":\"retain\"}");
        files = Create(new FileSystemManagerHooks { LocalPublicationObserver = (phase, _) =>
        {
            if (phase != TrustedLocalPublicationPhase.IntentPublished) return;
            reached++; File.WriteAllBytes(files!.ResolvePath(AfterlifeEntityProfileState.StatePath), unknown);
            throw new IOException("Deliberate publication failure after unknown profile bytes.");
        } });
        SeedProfile(files);
        var manager = new StateManager(files, new GameSettings(), NullLogger<StateManager>.Instance);
        var runtime = manager.CurrentState;
        if (suppliedLease)
        {
            await using var lease = await files.AcquireCanonicalWriteLeaseAsync();
            await Assert.ThrowsAsync<CoordinatedStatePublicationUncertainException>(() => manager.RefreshGameStateAsync(lease));
        }
        else await Assert.ThrowsAsync<CoordinatedStatePublicationUncertainException>(() => manager.RefreshGameStateAsync());
        Assert.Equal(1, reached); Assert.Same(runtime, manager.CurrentState);
        Assert.Equal(unknown, File.ReadAllBytes(files.ResolvePath(AfterlifeEntityProfileState.StatePath)));
        Assert.True(File.Exists(Path.Combine(files.RuntimeRootPath, "trusted-local-publication-v1/active.json")));
    }

    /// <summary>
    /// Detects read boundaries bypassing same-lease committed debt before ordinary generation validation or profile reads.
    /// </summary>
    [Fact]
    public async Task RetainedDebtBlocksStreamAndRefreshBeforeAnyReadOnTheSameLease()
    {
        var reads = 0;
        var files = Create(new FileSystemManagerHooks
        {
            BeforeCanonicalReadOpenAsync = _ => { reads++; return Task.CompletedTask; },
            LocalPublicationObserver = (phase, _) => { if (phase == TrustedLocalPublicationPhase.Committed) throw new InvalidOperationException("Retained cleanup debt."); }
        });
        SeedProfile(files); File.WriteAllBytes(files.ResolvePath(Archive), [1]);
        var debt = files.ResolvePath("game_state/core/debt.bin"); File.WriteAllBytes(debt, [1]);
        await using var lease = await files.AcquireCanonicalWriteLeaseAsync();
        var outcome = await files.PublishLocalFilesAsync(lease, [new CanonicalLocalFileChange("game_state/core/debt.bin", [1], [2])]);
        Assert.Equal(TrustedLocalPublicationDisposition.Committed, outcome.Disposition);
        File.WriteAllBytes(debt, [99]); reads = 0;
        await Assert.ThrowsAsync<CoordinatedStatePublicationUncertainException>(() => files.OpenOrdinaryReadFileAsync(lease, Archive));
        var manager = new StateManager(files, new GameSettings(), NullLogger<StateManager>.Instance);
        await Assert.ThrowsAsync<CoordinatedStatePublicationUncertainException>(() => manager.RefreshGameStateAsync(lease));
        Assert.Equal(0, reads); Assert.Equal(new byte[] { 99 }, File.ReadAllBytes(debt));
        Assert.True(File.Exists(Path.Combine(files.RuntimeRootPath, "trusted-local-publication-v1/active.json")));
    }

    /// <summary>
    /// Seeds a valid generation and a deliberately stale client-owned player mirror using only fixture-owned paths.
    /// </summary>
    /// <param name="files">
    /// The fixture manager whose root is initialized.
    /// </param>
    private static void SeedProfile(FileSystemManager files)
    {
        files.EnsureDirectoryStructure(); Directory.CreateDirectory(Path.GetDirectoryName(files.SessionGenerationPath)!);
        File.WriteAllText(files.SessionGenerationPath, "{\"schemaVersion\":1,\"generationId\":\"0123456789abcdef0123456789abcdef\"}");
        File.WriteAllText(files.ResolvePath("game_state/meta/soul_state.json"), """
            {"soulName":"Искра","currentRealm":"Chaos Sea","currentIncarnation":1,"inkFeathers":{"current":17,"total":17},
             "enlightenment":{"experience":0,"level":0},"afterlifeCombatProfile":{"spiritFocusTier":0,"artTiers":{"guard":0}}}
            """);
        File.WriteAllText(files.ResolvePath(AfterlifeEntityProfileState.StatePath), """
            {"schemaVersion":1,"profiles":[{"actorType":"player_soul","actorId":"player_soul","displayName":"Искра",
              "realm":"Chaos Sea","gmRevision":"preserved","currencies":{"inkFeathers":99,"lightSparks":0},
              "progression":{},"standardArts":{"guard":4},"progressionLedger":[]}]}
            """);
    }

    /// <summary>
    /// Creates a bounded native directory-link fixture using an unprivileged Windows junction or a Linux symbolic link.
    /// </summary>
    /// <param name="path">
    /// The absent owned link name.
    /// </param>
    /// <param name="target">
    /// The separately owned outside sentinel directory.
    /// </param>
    /// <returns>
    /// Completion after the native link was created successfully.
    /// </returns>
    private static async Task CreateDirectoryLink(string path, string target)
    {
        if (!OperatingSystem.IsWindows()) { Directory.CreateSymbolicLink(path, target); return; }
        var start = new ProcessStartInfo("cmd.exe") { UseShellExecute = false, CreateNoWindow = true,
            RedirectStandardOutput = true, RedirectStandardError = true };
        start.ArgumentList.Add("/c"); start.ArgumentList.Add($"mklink /J \"{path}\" \"{target}\"");
        using var process = Process.Start(start) ?? throw new InvalidOperationException("Owned junction fixture did not start.");
        var stdout = process.StandardOutput.ReadToEndAsync(); var stderr = process.StandardError.ReadToEndAsync();
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        try { await process.WaitForExitAsync(deadline.Token); }
        finally { if (!process.HasExited) { process.Kill(entireProcessTree: true); await process.WaitForExitAsync(); } }
        Assert.True(process.ExitCode == 0, await stdout + await stderr);
    }

    /// <summary>
    /// Creates an outside hard-link name for the ordinary input contract.
    /// </summary>
    /// <param name="created">
    /// The absent outside name.
    /// </param>
    /// <param name="existing">
    /// The existing owned archive file.
    /// </param>
    private static void MakeHardLink(string created, string existing)
    {
        if (OperatingSystem.IsWindows()) Assert.True(CreateHardLink(created, existing, IntPtr.Zero));
        else Assert.Equal(0, Link(existing, created));
    }

    /// <summary>
    /// Creates a native Windows hard link.
    /// </summary>
    /// <param name="created">
    /// The new link name.
    /// </param>
    /// <param name="existing">
    /// The existing file name.
    /// </param>
    /// <param name="security">
    /// The unused security argument, passed as zero.
    /// </param>
    /// <returns>
    /// True on creation, otherwise false.
    /// </returns>
    [DllImport("kernel32.dll", EntryPoint = "CreateHardLinkW", CharSet = CharSet.Unicode, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool CreateHardLink(string created, string existing, IntPtr security);

    /// <summary>
    /// Creates a native Linux hard link.
    /// </summary>
    /// <param name="existing">
    /// The existing file name.
    /// </param>
    /// <param name="created">
    /// The new link name.
    /// </param>
    /// <returns>
    /// Zero on success, or a native failure status.
    /// </returns>
    [DllImport("libc", EntryPoint = "link", SetLastError = true)]
    private static extern int Link(string existing, string created);

    /// <summary>
    /// Removes only this test's owned mutable root after its leases and streams have closed.
    /// </summary>
    public void Dispose() { if (Directory.Exists(_root)) Directory.Delete(_root, recursive: true); }
}
