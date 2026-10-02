using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text.Json;
using BookOfEternityClient.Core;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;
using Xunit.Abstractions;

namespace BookOfEternityClient.Tests;

public sealed class TrustedLocalStreamRecoveryTests(ITestOutputHelper output) : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "boe-stream-recovery-" + Guid.NewGuid().ToString("N"));
    private const long HeapLimit = 768L * 1024 * 1024;
    private FileSystemManager Files => new(_root, NullLogger<FileSystemManager>.Instance);
    private string Active => Path.Combine(_root, ".boe_runtime/trusted-local-publication-v1/active.json");
    private string Target => Files.ResolvePath("game_state/core/stream-target.bin");
    private string Created => Files.ResolvePath("game_state/core/stream-created.bin");
    private string Deleted => Files.ResolvePath("game_state/core/stream-deleted.bin");
    private string Alias => Path.Combine(_root, "outside-alias.bin");
    private string _beforeHash = "", _afterHash = "";
    private byte[] _generation = [];

    private void Seed(bool large = false)
    {
        var files = Files; files.EnsureDirectoryStructure(); Directory.CreateDirectory(Path.Combine(_root, "inputs"));
        Directory.CreateDirectory(Path.GetDirectoryName(files.SessionGenerationPath)!);
        _generation = JsonSerializer.SerializeToUtf8Bytes(new { schemaVersion = 1, generationId = Guid.NewGuid().ToString("N"), extension = true });
        File.WriteAllBytes(files.SessionGenerationPath, _generation);
        Fill(Target, large ? 256L * 1024 * 1024 : 17, 23);
        Fill(Path.Combine(_root, "inputs/after.bin"), large ? 64L * 1024 * 1024 : 19, 42);
        File.WriteAllBytes(Deleted, [0xEF, 0xBB, 0xBF, 0xFF]);
        _beforeHash = Hash(Target); _afterHash = Hash(Path.Combine(_root, "inputs/after.bin"));
        if (OperatingSystem.IsWindows()) Assert.True(CreateHardLink(Alias, Target, IntPtr.Zero));
        else Assert.Equal(0, Link(Target, Alias));
        foreach (var dir in new[] { "manual_saves", "autosaves", "checkpoint_saves" })
            File.WriteAllBytes(files.ResolvePath($"saves/{dir}/sentinel.zip"), [9, 0, 255]);
    }

    [Theory]
    [InlineData("IntentStaged", false)]
    [InlineData("IntentPublished", false)]
    [InlineData("MemberStaged", false)]
    [InlineData("first-member", false)]
    [InlineData("last-member", false)]
    [InlineData("CommitStaged", false)]
    [InlineData("Committed", true)]
    [InlineData("CleanupMember", true)]
    [InlineData("CleanupComplete", true)]
    public async Task AbruptImagePublicationRecoversCompleteSetThroughSeparatePublicAcquisition(string cut, bool committed)
    {
        Seed(); Assert.Equal(73, await Run("stream-publish", cut));
        if (cut != "IntentStaged") Assert.False(Directory.Exists(Path.Combine(_root, "inputs")));
        Assert.Equal(0, await Run("stream-recover", "unused")); AssertDecision(committed);
        Assert.Equal(0, await Run("stream-recover", "unused")); AssertDecision(committed);
        Assert.Empty(Directory.EnumerateFileSystemEntries(Path.GetDirectoryName(Active)!));
        Assert.Empty(Directory.GetFiles(Files.GameSessionPath, ".boe-local-*", SearchOption.AllDirectories));
    }

    [Theory]
    [InlineData("RollbackStaged")]
    [InlineData("MemberRestored")]
    public async Task LargeBeforeRegionSurvivesInterruptedColdRollbackWithoutCallerSources(string recoveryCut)
    {
        Seed(large: true); Assert.Equal(73, await Run("stream-publish", "last-member"));
        Assert.False(Directory.Exists(Path.Combine(_root, "inputs"))); Assert.Equal(_afterHash, Hash(Target));
        Assert.Equal(73, await Run("stream-recover-cut", recoveryCut));
        Assert.True(File.Exists(Active));
        if (recoveryCut == "RollbackStaged")
        {
            var undo = Assert.Single(Directory.GetFiles(Path.GetDirectoryName(Target)!, ".boe-local-*.undo"));
            Assert.Equal(_beforeHash, Hash(undo)); Assert.Equal(_afterHash, Hash(Target));
        }
        else Assert.Equal(_beforeHash, Hash(Target));
        Assert.Equal(0, await Run("stream-recover", "unused")); AssertDecision(false);
        Assert.Empty(Directory.EnumerateFileSystemEntries(Path.GetDirectoryName(Active)!));
        Assert.Empty(Directory.GetFiles(Files.GameSessionPath, ".boe-local-*", SearchOption.AllDirectories));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ColdLaterMemberOrGenerationConflictRetainsEarlierImagesAndEvidence(bool generation)
    {
        Seed(); Assert.Equal(73, await Run("stream-publish", "last-member"));
        var path = generation ? Files.SessionGenerationPath : Created;
        var unknown = generation ? JsonSerializer.SerializeToUtf8Bytes(new { schemaVersion = 1, generationId = Guid.NewGuid().ToString("N") }) : new byte[] { 99 };
        File.WriteAllBytes(path, unknown); var evidence = Hash(Active);
        Assert.Equal(78, await Run("stream-recover", "unused"));
        Assert.Equal(_afterHash, Hash(Target)); Assert.False(File.Exists(Deleted));
        Assert.Equal(unknown, File.ReadAllBytes(path)); Assert.Equal(evidence, Hash(Active)); AssertSentinels();
    }

    [Fact]
    public async Task CommittedColdCleanupCanBeInterruptedWithoutRestoringOldImages()
    {
        Seed(); Assert.Equal(73, await Run("stream-publish", "Committed"));
        Assert.Equal(73, await Run("stream-recover-cut", "CleanupMember"));
        Assert.True(File.Exists(Active)); AssertDecision(true);
        Assert.Equal(0, await Run("stream-recover", "unused")); AssertDecision(true);
        Assert.Empty(Directory.EnumerateFileSystemEntries(Path.GetDirectoryName(Active)!));
    }

    private void AssertDecision(bool committed)
    {
        Assert.Equal(committed ? _afterHash : _beforeHash, Hash(Target));
        Assert.Equal(committed, File.Exists(Created));
        if (committed) Assert.Equal(new byte[] { 7, 8 }, File.ReadAllBytes(Created));
        Assert.Equal(!committed, File.Exists(Deleted));
        if (!committed) Assert.Equal(new byte[] { 0xEF, 0xBB, 0xBF, 0xFF }, File.ReadAllBytes(Deleted));
        Assert.Equal(_generation, File.ReadAllBytes(Files.SessionGenerationPath)); AssertSentinels();
    }
    private void AssertSentinels()
    {
        Assert.Equal(_beforeHash, Hash(Alias));
        foreach (var dir in new[] { "manual_saves", "autosaves", "checkpoint_saves" })
            Assert.Equal(new byte[] { 9, 0, 255 }, File.ReadAllBytes(Files.ResolvePath($"saves/{dir}/sentinel.zip")));
        Assert.Equal(new[] { "autosaves/sentinel.zip", "checkpoint_saves/sentinel.zip", "manual_saves/sentinel.zip" },
            Directory.GetFiles(Files.ResolvePath("saves"), "*", SearchOption.AllDirectories)
                .Select(path => Path.GetRelativePath(Files.ResolvePath("saves"), path).Replace('\\', '/'))
                .Order(StringComparer.Ordinal).ToArray());
    }
    private async Task<int> Run(string mode, string cut)
    {
        var assembly = typeof(TrustedLocalStreamRecoveryTests).Assembly.Location;
        var start = new ProcessStartInfo(Environment.GetEnvironmentVariable("DOTNET_HOST_PATH") ?? "dotnet")
        { UseShellExecute = false, RedirectStandardOutput = true, RedirectStandardError = true };
        foreach (var argument in new[] { "exec", "--runtimeconfig", Path.ChangeExtension(assembly, ".runtimeconfig.json"),
            "--depsfile", Path.ChangeExtension(assembly, ".deps.json"), typeof(PortableStorageCrashHost.Program).Assembly.Location,
            _root, HeapLimit.ToString(), mode, cut }) start.ArgumentList.Add(argument);
        start.Environment["DOTNET_GCHeapHardLimit"] = "0x30000000";
        using var process = Process.Start(start) ?? throw new InvalidOperationException("Owned stream host did not start.");
        var stdout = process.StandardOutput.ReadToEndAsync(); var stderr = process.StandardError.ReadToEndAsync();
        var timer = Stopwatch.StartNew(); long peak = 0;
        try
        {
            while (!process.HasExited)
            {
                process.Refresh(); peak = Math.Max(peak, process.WorkingSet64);
                if (peak > 1024L * 1024 * 1024 || timer.Elapsed > TimeSpan.FromSeconds(120))
                    throw new InvalidOperationException("Owned stream host exceeded its declared bounds.");
                await Task.Delay(25);
            }
            await process.WaitForExitAsync(); var text = await stdout;
            Assert.True(process.ExitCode is 0 or 73 or 78, text + await stderr);
            if (process.ExitCode == 73)
            {
                using var reached = JsonDocument.Parse(text);
                Assert.Equal("AbruptExit", reached.RootElement.GetProperty("Phase").GetString());
                Assert.Equal(cut is "first-member" or "last-member" ? "MemberPublished" : cut,
                    reached.RootElement.GetProperty("Cut").GetString());
                var expectedIndex = cut switch
                {
                    "last-member" => 2,
                    "first-member" or "MemberStaged" or "RollbackStaged" or "MemberRestored" or "CleanupMember" => 0,
                    _ => -1
                };
                Assert.Equal(expectedIndex, reached.RootElement.GetProperty("Index").GetInt32());
            }
            output.WriteLine("mode={0}; cut={1}; exit={2}; sampledPeakBytes={3}; {4}", mode, cut, process.ExitCode, peak, text);
            return process.ExitCode;
        }
        finally { if (!process.HasExited) { process.Kill(entireProcessTree: true); await process.WaitForExitAsync(); } }
    }
    private static void Fill(string path, long count, byte value)
    {
        var bytes = Enumerable.Repeat(value, 64 * 1024).ToArray(); using var stream = File.Create(path);
        for (long written = 0; written < count;) { var take = (int)Math.Min(bytes.Length, count - written); stream.Write(bytes, 0, take); written += take; }
    }
    private static string Hash(string path) { using var stream = File.OpenRead(path); return Convert.ToHexString(SHA256.HashData(stream)); }
    [DllImport("libc", EntryPoint = "link", SetLastError = true)] private static extern int Link(string existing, string created);
    [DllImport("kernel32.dll", EntryPoint = "CreateHardLinkW", CharSet = CharSet.Unicode, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)] private static extern bool CreateHardLink(string created, string existing, IntPtr security);
    public void Dispose() { if (Directory.Exists(_root)) Directory.Delete(_root, recursive: true); }
}
