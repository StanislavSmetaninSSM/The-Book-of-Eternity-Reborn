using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json.Nodes;
using BookOfEternityClient.CommandProtocol;
using BookOfEternityClient.Configuration;
using BookOfEternityClient.Core;
using BookOfEternityClient.Services;
using BookOfEternityClient.UI;
using BookOfEternityClient.WebUi;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed partial class PortableSessionReplacementTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Clear_LockDirectoryIsInvalidEvenWhenStructurallyEmpty(bool containsFile)
    {
        var selected = SeedSelected(); SeedAll(selected);
        var path = PathFor(LocalUiSessionLockService.LockPath);
        Directory.CreateDirectory(path);
        if (containsFile) File.WriteAllBytes(Path.Combine(path, "unknown.bin"), [42]);
        await Assert.ThrowsAsync<InvalidDataException>(() => _files.ClearGameStateAsync());
        AssertImages(selected, present: true);
        Assert.Equal(_generationBytes, File.ReadAllBytes(_files.SessionGenerationPath));
        Assert.True(Directory.Exists(path));
        if (containsFile) Assert.Equal(new byte[] { 42 }, File.ReadAllBytes(Path.Combine(path, "unknown.bin")));
    }

    [Fact]
    public async Task BrowserPendingProjection_EmptySnapshotAllowsFormAndActualFileBlocksIt()
    {
        var child = PathFor(BrowserPendingTurnInspector.PendingTurnSnapshotDirectory + "/game_state/core");
        Directory.CreateDirectory(child);
        var state = new StateManager(_files, new GameSettings(), NullLogger<StateManager>.Instance);
        var validation = new ValidationService(_files, NullLogger<ValidationService>.Instance);
        var empty = await ExplorerLifecycleLocalTurnCommandResultBuilder.TryBuildAsync("/distribute", state, _files, validation);
        Assert.NotNull(empty);
        Assert.Equal(CommandExecutionState.RequiresInput, empty.State);
        File.WriteAllBytes(Path.Combine(child, "empty.json"), []);
        var pending = await ExplorerLifecycleLocalTurnCommandResultBuilder.TryBuildAsync("/distribute", state, _files, validation);
        Assert.NotNull(pending);
        Assert.Equal(CommandExecutionState.Pending, pending.State);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void PendingInspector_RejectsWrongTypeRootOrLinkedDescendant(bool descendantLink)
    {
        var path = PathFor(BrowserPendingTurnInspector.PendingTurnSnapshotDirectory);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        if (descendantLink)
        {
            Directory.CreateDirectory(path);
            var outside = Path.Combine(_root, "outside-pending.bin"); File.WriteAllBytes(outside, [42]);
            File.CreateSymbolicLink(Path.Combine(path, "link"), outside);
        }
        else File.WriteAllBytes(path, [42]);
        Assert.Throws<InvalidDataException>(() => BrowserPendingTurnInspector.Build(_files));
    }

    [Fact]
    public async Task Clear_AbsentGenerationRollsBackAsAbsenceWithSelectedMembers()
    {
        var selected = SeedSelected(); SeedAll(selected);
        File.Delete(_files.SessionGenerationPath);
        var observed = false;
        var files = Manager(new FileSystemManagerHooks
        {
            LocalPublicationObserver = (phase, _) =>
            {
                if (phase != TrustedLocalPublicationPhase.IntentPublished) return;
                var document = JsonNode.Parse(File.ReadAllBytes(ActiveReplacementJournal))!;
                Assert.False(document["GenerationBefore"]!["Exists"]!.GetValue<bool>());
                Assert.Equal(selected.Count + 1, document["Members"]!.AsArray().Count);
                observed = true; throw new CutFailure();
            }
        });
        await Assert.ThrowsAsync<CutFailure>(() => files.ClearGameStateAsync());
        Assert.True(observed);
        Assert.False(File.Exists(_files.SessionGenerationPath));
        AssertImages(selected, present: true);
    }

    [Fact]
    public async Task Clear_HardLinkedGenerationAndMemberLeaveOutsideNamesUnchanged()
    {
        Seed("game_state/core/linked.bin", [0xFF, 0]);
        var outsideGeneration = Path.Combine(_root, "outside-generation.json");
        var outsideMember = Path.Combine(_root, "outside-member.bin");
        CreateOwnedHardLink(_files.SessionGenerationPath, outsideGeneration);
        CreateOwnedHardLink(PathFor("game_state/core/linked.bin"), outsideMember);
        await _files.ClearGameStateAsync();
        Assert.Equal(_generationBytes, File.ReadAllBytes(outsideGeneration));
        Assert.Equal(new byte[] { 0xFF, 0 }, File.ReadAllBytes(outsideMember));
        Assert.False(File.Exists(PathFor("game_state/core/linked.bin")));
        Assert.NotEqual(_generation, await ReadGeneration());
    }

    [Fact]
    public async Task Clear_UnknownLaterMemberBlocksCompleteRollbackAndRetainsEvidence()
    {
        var selected = SeedSelected(); SeedAll(selected);
        string? firstPath = null, changedPath = null;
        byte[]? journal = null;
        var files = Manager(new FileSystemManagerHooks
        {
            LocalPublicationObserver = (phase, index) =>
            {
                if (phase != TrustedLocalPublicationPhase.MemberPublished || index != 0) return;
                var members = JsonNode.Parse(File.ReadAllBytes(ActiveReplacementJournal))!["Members"]!.AsArray();
                firstPath = members[0]!["Path"]!.GetValue<string>();
                changedPath = members.Skip(1).First(member => !member!["After"]!["Exists"]!.GetValue<bool>())!["Path"]!.GetValue<string>();
                File.WriteAllBytes(changedPath, [99]);
                journal = File.ReadAllBytes(ActiveReplacementJournal);
                throw new CutFailure();
            }
        });
        await Assert.ThrowsAsync<InvalidDataException>(() => files.ClearGameStateAsync());
        Assert.NotNull(firstPath); Assert.NotNull(changedPath); Assert.NotNull(journal);
        Assert.False(File.Exists(firstPath));
        Assert.Equal(new byte[] { 99 }, File.ReadAllBytes(changedPath));
        Assert.Equal(journal, File.ReadAllBytes(ActiveReplacementJournal));
        await Assert.ThrowsAsync<InvalidDataException>(() => ReadGeneration());
        Assert.False(File.Exists(firstPath));
        Assert.Equal(new byte[] { 99 }, File.ReadAllBytes(changedPath));
        Assert.Equal(journal, File.ReadAllBytes(ActiveReplacementJournal));
        Assert.Equal(_generationBytes, File.ReadAllBytes(_files.SessionGenerationPath));
    }

    [Fact]
    public async Task Clear_ChangedGenerationRetainsPartialMembersAndJournal()
    {
        var selected = SeedSelected(); SeedAll(selected);
        string? firstPath = null; byte[]? journal = null;
        var changedGeneration = Encoding.UTF8.GetBytes("{\"SchemaVersion\":1,\"GenerationId\":\"" + Guid.NewGuid().ToString("N") + "\"}");
        var files = Manager(new FileSystemManagerHooks
        {
            LocalPublicationObserver = (phase, index) =>
            {
                if (phase != TrustedLocalPublicationPhase.MemberPublished || index != 0) return;
                firstPath = JsonNode.Parse(File.ReadAllBytes(ActiveReplacementJournal))!["Members"]![0]!["Path"]!.GetValue<string>();
                File.WriteAllBytes(_files.SessionGenerationPath, changedGeneration);
                journal = File.ReadAllBytes(ActiveReplacementJournal);
                throw new CutFailure();
            }
        });
        await Assert.ThrowsAsync<InvalidDataException>(() => files.ClearGameStateAsync());
        Assert.NotNull(firstPath); Assert.NotNull(journal);
        Assert.False(File.Exists(firstPath));
        await Assert.ThrowsAsync<InvalidDataException>(() => ReadGeneration());
        Assert.False(File.Exists(firstPath));
        Assert.Equal(changedGeneration, File.ReadAllBytes(_files.SessionGenerationPath));
        Assert.Equal(journal, File.ReadAllBytes(ActiveReplacementJournal));
    }

    [Theory]
    [InlineData("input/nonregular.json")]
    [InlineData("game_state/core/nonregular.bin")]
    [InlineData("worker_tasks/task/nonregular.bin")]
    public async Task Clear_SelectedFifoIsRejectedBeforeOpenInColdProcess(string relative)
    {
        if (!OperatingSystem.IsLinux()) return;
        var selected = SeedSelected(); SeedAll(selected);
        var path = PathFor(relative); Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        Assert.Equal(0, MakeReplacementFifo(path, Convert.ToUInt32("600", 8)));
        await RunReplacementChild("replacement-reject", "unused", 0);
        AssertImages(selected, present: true);
        Assert.Equal(_generationBytes, File.ReadAllBytes(_files.SessionGenerationPath));
        Assert.True(File.Exists(path));
    }

    [Fact]
    public async Task Clear_OpaqueContextPackFifoRemainsUnopenedInColdProcess()
    {
        if (!OperatingSystem.IsLinux()) return;
        var selected = SeedSelected(); SeedAll(selected);
        var path = PathFor("game_state/control/gm_context_pack/deep/nonregular.bin");
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        Assert.Equal(0, MakeReplacementFifo(path, Convert.ToUInt32("600", 8)));
        await RunReplacementChild("replacement-success", "unused", 0);
        AssertImages(selected, present: false);
        Assert.True(File.Exists(path));
        Assert.NotEqual(_generation, await ReadGeneration());
    }

    [Theory]
    [InlineData("early-member", false)]
    [InlineData("after-generation", false)]
    [InlineData("committed", true)]
    [InlineData("cleanup", true)]
    public async Task Clear_ColdProcessRecoveryIncludesGenerationWorkersAndEmptyResidue(string cut, bool committed)
    {
        var selected = SeedSelected();
        selected[BrowserPendingTurnInspector.PendingTurnSnapshotDirectory + "/game_state/core/copy.json"] = [42];
        SeedAll(selected);
        await RunReplacementChild("replacement-cut", cut, 73);
        await RunReplacementChild("replacement-recover", "unused", 0);
        AssertImages(selected, present: !committed);
        var generation = await ReadGeneration();
        if (committed) Assert.NotEqual(_generation, generation);
        else
        {
            Assert.Equal(_generation, generation);
            Assert.Equal(_generationBytes, File.ReadAllBytes(_files.SessionGenerationPath));
        }
        Assert.True(Directory.Exists(PathFor("worker_proposals/proposal")));
        Assert.Equal(!committed, BrowserPendingTurnInspector.Build(_files).HasActiveGmTurn);
        Assert.False(File.Exists(ActiveReplacementJournal));
    }

    [Theory]
    [InlineData(@"\\?\C:\session\game_state", @"C:\session\game_state\control\gm_context_pack\part.json")]
    [InlineData(@"C:\session\game_state", @"\\?\C:\session\game_state\control\gm_context_pack\part.json")]
    [InlineData(@"\\?\UNC\server\share\session\game_state", @"\\server\share\session\game_state\control\gm_context_pack\part.json")]
    [InlineData(@"\\server\share\session\game_state", @"\\?\UNC\server\share\session\game_state\control\gm_context_pack\part.json")]
    public void LocalRelativePath_CanonicalizesSupportedWindowsRootAndMemberSpellings(string root, string member)
    {
        Assert.Equal("control/gm_context_pack/part.json", FileSystemManager.GetLocalRelativePath(root, member, windows: true));
    }

    [Fact]
    public async Task Clear_ExtendedWindowsRootPreservesExclusionsAndCanonicalMutationBoundaries()
    {
        if (!OperatingSystem.IsWindows()) return;
        var selected = SeedSelected(); SeedAll(selected);
        const string keep = "game_state/control/gm_context_pack/deep/keep.bin";
        Seed(keep, [42]);
        var extended = _root.StartsWith(@"\\", StringComparison.Ordinal)
            ? @"\\?\UNC\" + _root[2..] : @"\\?\" + _root;
        var boundaries = new List<string>();
        var files = new FileSystemManager(extended, NullLogger<FileSystemManager>.Instance,
            PhysicalLoadTransactionOperations.Instance, new FileSystemManagerHooks
            {
                BeforeCanonicalMutationBoundaryAsync = relative =>
                {
                    boundaries.Add(relative.Replace('\\', '/'));
                    return Task.CompletedTask;
                }
            });
        await files.ClearGameStateAsync();
        AssertImages(selected, present: false);
        Assert.Equal(new byte[] { 42 }, File.ReadAllBytes(PathFor(keep)));
        Assert.NotEqual(_generation, await ReadGeneration());
        Assert.Equal(selected.Keys.OrderBy(path => path), boundaries.OrderBy(path => path));
    }

    private string ActiveReplacementJournal => Path.Combine(_files.RuntimeRootPath, "trusted-local-publication-v1", "active.json");
    private async Task RunReplacementChild(string mode, string cut, int expectedExit)
    {
        var assembly = typeof(PortableSessionReplacementTests).Assembly.Location;
        var start = new ProcessStartInfo(Environment.GetEnvironmentVariable("DOTNET_HOST_PATH") ?? "dotnet")
        { UseShellExecute = false, RedirectStandardOutput = true, RedirectStandardError = true };
        foreach (var argument in new[] { "exec", "--runtimeconfig", Path.ChangeExtension(assembly, ".runtimeconfig.json"),
                     "--depsfile", Path.ChangeExtension(assembly, ".deps.json"), typeof(PortableStorageCrashHost.Program).Assembly.Location,
                     _root, _generation, mode, cut }) start.ArgumentList.Add(argument);
        using var process = Process.Start(start) ?? throw new InvalidOperationException("Replacement child did not start.");
        var output = process.StandardOutput.ReadToEndAsync(); var error = process.StandardError.ReadToEndAsync();
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5)); var timedOut = false;
        try { await process.WaitForExitAsync(timeout.Token); }
        catch (OperationCanceledException) { timedOut = true; }
        finally { if (!process.HasExited) { process.Kill(entireProcessTree: true); await process.WaitForExitAsync(); } }
        var diagnostic = await output + await error;
        Assert.False(timedOut, "Replacement child exceeded its bound: " + mode + "/" + cut);
        Assert.True(process.ExitCode == expectedExit, diagnostic + " Exit: " + process.ExitCode);
    }

    private static void CreateOwnedHardLink(string source, string alias)
    {
        if (OperatingSystem.IsWindows()) Assert.True(CreateReplacementHardLink(alias, source, IntPtr.Zero));
        else Assert.Equal(0, ReplacementLink(source, alias));
    }
    [DllImport("libc", EntryPoint = "mkfifo", SetLastError = true)] private static extern int MakeReplacementFifo(string path, uint mode);
    [DllImport("libc", EntryPoint = "link", SetLastError = true)] private static extern int ReplacementLink(string existing, string created);
    [DllImport("kernel32.dll", EntryPoint = "CreateHardLinkW", CharSet = CharSet.Unicode, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)] private static extern bool CreateReplacementHardLink(string path, string existing, IntPtr security);
}
