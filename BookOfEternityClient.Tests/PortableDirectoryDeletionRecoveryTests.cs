using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text.Json;
using BookOfEternityClient.Core;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed class PortableDirectoryDeletionRecoveryTests : IDisposable
{
    private const string Tree = "pending_turn_snapshot";
    private readonly string _root = Path.Combine(Path.GetTempPath(), "boe-tree-cold-" + Guid.NewGuid().ToString("N"));
    private readonly string _generation = Guid.NewGuid().ToString("N");
    private readonly FileSystemManager _files;
    private readonly Dictionary<string, byte[]> _images = new() { ["a"] = [0xFF, 0], ["nested/b"] = [0xEF, 0xBB, 0xBF, 1], ["nested/z"] = [] };
    private string Journal => Path.Combine(_files.RuntimeRootPath, "trusted-local-publication-v1");
    private string Active => Path.Combine(Journal, "active.json");
    public PortableDirectoryDeletionRecoveryTests()
    {
        _files = new(_root, NullLogger<FileSystemManager>.Instance);
        Directory.CreateDirectory(Path.GetDirectoryName(_files.SessionGenerationPath)!);
        File.WriteAllBytes(_files.SessionGenerationPath, Generation(_generation));
        foreach (var (name, bytes) in _images)
        {
            var path = _files.ResolvePath(Tree + "/" + name);
            Directory.CreateDirectory(Path.GetDirectoryName(path)!); File.WriteAllBytes(path, bytes);
        }
    }

    [Theory]
    [InlineData("IntentStaged", false)]
    [InlineData("IntentPublished", false)]
    [InlineData("first-member", false)]
    [InlineData("last-member", false)]
    [InlineData("CommitStaged", false)]
    [InlineData("Committed", true)]
    [InlineData("CleanupMember", true)]
    [InlineData("CleanupComplete", true)]
    [InlineData("cleanup-debt", true)]
    public async Task ActualDirectoryApiCrashRecoversExactWholeSetInSeparateProcess(string cut, bool committed)
    {
        Assert.Equal(73, await RunHost("directory-cut", cut));
        if (cut != "CleanupComplete")
        {
            var evidence = cut == "IntentStaged" ? Path.Combine(Journal, "intent.tmp") : Active;
            using var journal = JsonDocument.Parse(File.ReadAllBytes(evidence));
            Assert.Equal(3, journal.RootElement.GetProperty("Members").GetArrayLength());
            Assert.Equal(_generation, journal.RootElement.GetProperty("GenerationBefore").GetProperty("Id").GetString());
        }
        Assert.True(Directory.Exists(_files.ResolvePath(Tree + "/nested")));
        Assert.Equal(0, await RunHost("directory-recover", cut));
        AssertImages(!committed);
        Assert.Equal(Generation(_generation), File.ReadAllBytes(_files.SessionGenerationPath));
        Assert.Empty(Directory.EnumerateFileSystemEntries(Journal));
        Assert.Empty(Directory.EnumerateFiles(_root, ".boe-local-*", SearchOption.AllDirectories));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ColdUnknownBytesOrGenerationPreserveThePartialSetAndJournal(bool generationConflict)
    {
        Assert.Equal(73, await RunHost("directory-cut", "first-member"));
        Assert.False(File.Exists(_files.ResolvePath(Tree + "/a")));
        var changedPath = generationConflict ? _files.SessionGenerationPath : _files.ResolvePath(Tree + "/nested/z");
        var unknown = generationConflict ? Generation(Guid.NewGuid().ToString("N")) : new byte[] { 42 };
        File.WriteAllBytes(changedPath, unknown); var evidence = File.ReadAllBytes(Active);
        Assert.Equal(0, await RunHost("directory-conflict", "unused"));
        Assert.Equal(unknown, File.ReadAllBytes(changedPath)); Assert.Equal(evidence, File.ReadAllBytes(Active));
        Assert.False(File.Exists(_files.ResolvePath(Tree + "/a")));
        Assert.Equal(_images["nested/b"], File.ReadAllBytes(_files.ResolvePath(Tree + "/nested/b")));
    }

    [Theory]
    [InlineData("first-member", false)]
    [InlineData("cleanup-debt", true)]
    public async Task ColdHardLinkOutsideBytesSurviveRollbackAndCommittedCleanup(string cut, bool committed)
    {
        var source = _files.ResolvePath(Tree + "/a"); var outside = Path.Combine(_root, "outside.bin");
        if (OperatingSystem.IsWindows()) Assert.True(CreateHardLink(outside, source, IntPtr.Zero));
        else Assert.Equal(0, Link(source, outside));
        Assert.Equal(73, await RunHost("directory-cut", cut));
        Assert.Equal(_images["a"], File.ReadAllBytes(outside));
        Assert.Equal(0, await RunHost("directory-recover", "unused"));
        AssertImages(!committed); Assert.Equal(_images["a"], File.ReadAllBytes(outside));
    }

    [Fact]
    public async Task LinuxFifoIsRejectedBeforeAnyOpenOrMemberMutationInBoundedProcess()
    {
        Assert.True(OperatingSystem.IsLinux(), "This category requires an actual Linux FIFO fixture.");
        Assert.Equal(0, MkFifo(_files.ResolvePath(Tree + "/nested/zz-fifo"), Convert.ToUInt32("600", 8)));
        Assert.Equal(0, await RunHost("directory-reject", "unused"));
        AssertImages(true); Assert.False(File.Exists(Active));
    }

    private async Task<int> RunHost(string mode, string cut)
    {
        var assembly = typeof(PortableDirectoryDeletionRecoveryTests).Assembly.Location;
        var start = new ProcessStartInfo(Environment.GetEnvironmentVariable("DOTNET_HOST_PATH") ?? "dotnet")
        { UseShellExecute = false, RedirectStandardOutput = true, RedirectStandardError = true };
        foreach (var arg in new[] { "exec", "--runtimeconfig", Path.ChangeExtension(assembly, ".runtimeconfig.json"),
            "--depsfile", Path.ChangeExtension(assembly, ".deps.json"), typeof(PortableStorageCrashHost.Program).Assembly.Location,
            _root, _generation, mode, cut }) start.ArgumentList.Add(arg);
        using var process = Process.Start(start) ?? throw new InvalidOperationException("Owned directory crash host did not start.");
        var stdout = process.StandardOutput.ReadToEndAsync(); var stderr = process.StandardError.ReadToEndAsync();
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        try
        {
            await process.WaitForExitAsync(deadline.Token);
            var output = await stdout + await stderr; Assert.True(process.ExitCode is 0 or 73, output);
            return process.ExitCode;
        }
        finally
        {
            if (!process.HasExited) { process.Kill(entireProcessTree: true); await process.WaitForExitAsync(); }
        }
    }
    private void AssertImages(bool present)
    {
        foreach (var (name, bytes) in _images)
        {
            var path = _files.ResolvePath(Tree + "/" + name); Assert.Equal(present, File.Exists(path));
            if (present) Assert.Equal(bytes, File.ReadAllBytes(path));
        }
    }
    private static byte[] Generation(string id) => JsonSerializer.SerializeToUtf8Bytes(new { schemaVersion = 1, generationId = id });
    [DllImport("libc", EntryPoint = "mkfifo", SetLastError = true)] private static extern int MkFifo(string path, uint mode);
    [DllImport("libc", EntryPoint = "link", SetLastError = true)] private static extern int Link(string existing, string created);
    [DllImport("kernel32.dll", EntryPoint = "CreateHardLinkW", CharSet = CharSet.Unicode, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)] private static extern bool CreateHardLink(string created, string existing, IntPtr security);
    public void Dispose() { if (Directory.Exists(_root)) Directory.Delete(_root, recursive: true); }
}
