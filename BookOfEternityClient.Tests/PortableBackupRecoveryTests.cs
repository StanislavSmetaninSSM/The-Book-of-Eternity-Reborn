using System.Diagnostics;
using System.IO.Compression;
using System.Runtime.InteropServices;
using System.Text.Json;
using BookOfEternityClient.Core;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed class PortableBackupRecoveryTests : IDisposable
{
    private const string Source = "game_state/core/backup-source.bin";
    private const string Backup = "game_state/core/arbitrary.test-backup";
    private static readonly byte[] Exact = [0xEF, 0xBB, 0xBF, 0xFF, 0];
    private readonly string _root = Path.Combine(Path.GetTempPath(), "boe-backup-cold-" + Guid.NewGuid().ToString("N"));
    private readonly string _generation = Guid.NewGuid().ToString("N");
    private readonly FileSystemManager _files;
    private readonly Dictionary<string, byte[]> _sentinels = new();
    private string JournalRoot => Path.Combine(_files.RuntimeRootPath, "trusted-local-publication-v1");
    private string Active => Path.Combine(JournalRoot, "active.json");
    public PortableBackupRecoveryTests()
    {
        _files = new(_root, NullLogger<FileSystemManager>.Instance);
        Directory.CreateDirectory(Path.GetDirectoryName(_files.SessionGenerationPath)!);
        File.WriteAllBytes(_files.SessionGenerationPath, Generation(_generation));
        Directory.CreateDirectory(Path.GetDirectoryName(_files.ResolvePath(Source))!);
        File.WriteAllBytes(_files.ResolvePath(Source), [42]);
        foreach (var name in new[] { "manual", "autosave", "checkpoint" })
        {
            var path = Path.Combine(_root, name + ".zip");
            using (var zip = ZipFile.Open(path, ZipArchiveMode.Create))
            using (var entry = zip.CreateEntry("synthetic.bin").Open()) entry.Write(Exact);
            _sentinels.Add(path, File.ReadAllBytes(path));
        }
    }

    [Theory]
    [InlineData("create", "IntentStaged", false)]
    [InlineData("create", "MemberStaged", false)]
    [InlineData("create", "MemberPublished", false)]
    [InlineData("create", "Committed", true)]
    [InlineData("restore", "IntentPublished", false)]
    [InlineData("restore", "first-member", false)]
    [InlineData("restore", "last-member", false)]
    [InlineData("restore", "CommitStaged", false)]
    [InlineData("restore", "Committed", true)]
    [InlineData("restore", "CleanupMember", true)]
    [InlineData("restore", "CleanupComplete", true)]
    [InlineData("cleanup", "MemberPublished", false)]
    [InlineData("cleanup", "Committed", true)]
    [InlineData("cleanup", "cleanup-debt", true)]
    public async Task ActualBackupApiCrashRecoversCompleteDecisionInSeparateProcess(string operation, string cut, bool committed)
    {
        if (operation != "create") File.WriteAllBytes(_files.ResolvePath(Backup), Exact);
        Assert.Equal(73, await RunHost("backup-" + operation, cut));
        if (cut != "CleanupComplete")
        {
            using var doc = JsonDocument.Parse(File.ReadAllBytes(cut == "IntentStaged" ? Path.Combine(JournalRoot, "intent.tmp") : Active));
            Assert.Equal(operation == "restore" ? 2 : 1, doc.RootElement.GetProperty("Members").GetArrayLength());
            Assert.Equal(_generation, doc.RootElement.GetProperty("GenerationBefore").GetProperty("Id").GetString());
        }
        Assert.Equal(0, await RunHost("backup-recover", "unused"));
        AssertImages(operation, committed);
        Assert.Equal(0, await RunHost("backup-recover", "unused")); // Cold idempotent recovery, no replay.
        AssertImages(operation, committed);
        Assert.Empty(Directory.EnumerateFileSystemEntries(JournalRoot));
        Assert.Empty(Directory.GetFiles(_root, ".boe-local-*", SearchOption.AllDirectories));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ColdUnknownLaterBackupOrGenerationRetainsEarlierTargetAndEvidence(bool generation)
    {
        File.WriteAllBytes(_files.ResolvePath(Backup), Exact);
        Assert.Equal(73, await RunHost("backup-restore", "first-member"));
        var unknownPath = generation ? _files.SessionGenerationPath : _files.ResolvePath(Backup);
        var unknown = generation ? Generation(Guid.NewGuid().ToString("N")) : new byte[] { 99 };
        File.WriteAllBytes(unknownPath, unknown); var evidence = File.ReadAllBytes(Active);
        Assert.Equal(0, await RunHost("backup-conflict", "unused"));
        Assert.Equal(Exact, File.ReadAllBytes(_files.ResolvePath(Source)));
        Assert.Equal(unknown, File.ReadAllBytes(unknownPath)); Assert.Equal(evidence, File.ReadAllBytes(Active));
        AssertSentinels();
    }

    [Theory]
    [InlineData("last-member", false)]
    [InlineData("cleanup-debt", true)]
    public async Task ColdRestoreAndCleanupPreserveExternalHardLinkNames(string cut, bool committed)
    {
        File.WriteAllBytes(_files.ResolvePath(Backup), Exact);
        var targetAlias = Path.Combine(_root, "target-outside"); var backupAlias = Path.Combine(_root, "backup-outside");
        HardLink(_files.ResolvePath(Source), targetAlias); HardLink(_files.ResolvePath(Backup), backupAlias);
        Assert.Equal(73, await RunHost("backup-restore", cut));
        Assert.Equal(0, await RunHost("backup-recover", "unused"));
        AssertImages("restore", committed);
        Assert.Equal(new byte[] { 42 }, File.ReadAllBytes(targetAlias)); Assert.Equal(Exact, File.ReadAllBytes(backupAlias));
    }

    [Theory]
    [InlineData("create", false)]
    [InlineData("restore", false)]
    [InlineData("restore", true)]
    [InlineData("cleanup", true)]
    public async Task LinuxFifoIsRejectedInBoundedActualApiProcess(string operation, bool backup)
    {
        Assert.True(OperatingSystem.IsLinux(), "This owner requires actual Linux FIFO execution.");
        File.WriteAllBytes(_files.ResolvePath(Backup), Exact);
        var path = _files.ResolvePath(backup ? Backup : Source); File.Delete(path);
        Assert.Equal(0, MkFifo(path, Convert.ToUInt32("600", 8)));
        Assert.Equal(0, await RunHost("backup-" + operation, "reject"));
        Assert.False(File.Exists(Active)); AssertSentinels();
    }

    private void AssertImages(string operation, bool committed)
    {
        Assert.Equal(operation == "restore" && committed ? Exact : new byte[] { 42 }, File.ReadAllBytes(_files.ResolvePath(Source)));
        if (operation == "create")
        {
            var names = Directory.GetFiles(Path.GetDirectoryName(_files.ResolvePath(Source))!, "backup-source.bin.backup.*");
            if (committed) Assert.Equal(new byte[] { 42 }, File.ReadAllBytes(Assert.Single(names))); else Assert.Empty(names);
        }
        else
        {
            Assert.Equal(!committed, File.Exists(_files.ResolvePath(Backup)));
            if (!committed) Assert.Equal(Exact, File.ReadAllBytes(_files.ResolvePath(Backup)));
        }
        Assert.Equal(Generation(_generation), File.ReadAllBytes(_files.SessionGenerationPath)); AssertSentinels();
    }
    private void AssertSentinels() { foreach (var (path, bytes) in _sentinels) Assert.Equal(bytes, File.ReadAllBytes(path)); }
    private async Task<int> RunHost(string mode, string cut)
    {
        var assembly = typeof(PortableBackupRecoveryTests).Assembly.Location;
        var start = new ProcessStartInfo(Environment.GetEnvironmentVariable("DOTNET_HOST_PATH") ?? "dotnet")
        { UseShellExecute = false, RedirectStandardOutput = true, RedirectStandardError = true };
        foreach (var arg in new[] { "exec", "--runtimeconfig", Path.ChangeExtension(assembly, ".runtimeconfig.json"),
            "--depsfile", Path.ChangeExtension(assembly, ".deps.json"), typeof(PortableStorageCrashHost.Program).Assembly.Location,
            _root, _generation, mode, cut }) start.ArgumentList.Add(arg);
        using var process = Process.Start(start) ?? throw new InvalidOperationException("Owned backup crash host did not start.");
        var stdout = process.StandardOutput.ReadToEndAsync(); var stderr = process.StandardError.ReadToEndAsync();
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        try
        {
            await process.WaitForExitAsync(deadline.Token);
            var output = await stdout + await stderr; Assert.True(process.ExitCode is 0 or 73, output);
            return process.ExitCode;
        }
        finally { if (!process.HasExited) { process.Kill(entireProcessTree: true); await process.WaitForExitAsync(); } }
    }
    private static byte[] Generation(string id) => JsonSerializer.SerializeToUtf8Bytes(new { schemaVersion = 1, generationId = id });
    private static void HardLink(string existing, string created)
    { if (OperatingSystem.IsWindows()) Assert.True(CreateHardLink(created, existing, IntPtr.Zero)); else Assert.Equal(0, Link(existing, created)); }
    [DllImport("libc", EntryPoint = "mkfifo", SetLastError = true)] private static extern int MkFifo(string path, uint mode);
    [DllImport("libc", EntryPoint = "link", SetLastError = true)] private static extern int Link(string existing, string created);
    [DllImport("kernel32.dll", EntryPoint = "CreateHardLinkW", CharSet = CharSet.Unicode, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)] private static extern bool CreateHardLink(string created, string existing, IntPtr security);
    public void Dispose() { if (Directory.Exists(_root)) Directory.Delete(_root, true); }
}
