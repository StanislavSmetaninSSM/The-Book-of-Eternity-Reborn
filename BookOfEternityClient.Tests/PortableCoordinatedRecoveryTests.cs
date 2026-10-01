using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
using BookOfEternityClient.Core;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed class PortableCoordinatedRecoveryTests : IDisposable
{
    private const string ReplacePath = "game_state/meta/coordinated_replace.json";
    private const string CreatePath = "game_state/meta/coordinated_create.json";
    private const string DeletePath = "game_state/meta/coordinated_delete.json";
    private readonly string _root = Path.Combine(Path.GetTempPath(), "boe-coordinated-cold-" + Guid.NewGuid().ToString("N"));
    private readonly string _generation = Guid.NewGuid().ToString("N");
    private readonly FileSystemManager _files;
    private readonly byte[] _before = [0xEF, 0xBB, 0xBF, 0xFF, 0];
    private static byte[] After => [.. Encoding.UTF8.GetPreamble(), .. Encoding.UTF8.GetBytes("{\"value\":\"accepted\"}")];
    private string Journal => Path.Combine(_files.RuntimeRootPath, "trusted-local-publication-v1");
    private string Active => Path.Combine(Journal, "active.json");

    public PortableCoordinatedRecoveryTests()
    {
        _files = new FileSystemManager(_root, NullLogger<FileSystemManager>.Instance);
        _files.EnsureDirectoryStructure();
        Directory.CreateDirectory(Path.GetDirectoryName(_files.SessionGenerationPath)!);
        File.WriteAllBytes(_files.SessionGenerationPath, GenerationBytes(_generation));
        File.WriteAllBytes(_files.ResolvePath(ReplacePath), _before);
        File.WriteAllBytes(_files.ResolvePath(DeletePath), []);
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
    public async Task ActualHelperAbruptExitRecoversOneWholeSetInASeparateProcess(string cut, bool committed)
    {
        Assert.Equal(73, (await RunHost("coordinated-cut", cut)).Exit);
        if (cut != "CleanupComplete")
        {
            var evidence = cut == "IntentStaged" ? Path.Combine(Journal, "intent.tmp") : Active;
            using var journal = JsonDocument.Parse(File.ReadAllBytes(evidence));
            Assert.Equal(3, journal.RootElement.GetProperty("Members").GetArrayLength());
            Assert.Equal(_generation, journal.RootElement.GetProperty("GenerationBefore").GetProperty("Id").GetString());
        }

        Assert.Equal(0, (await RunHost("coordinated-recover", cut)).Exit);

        AssertImage(ReplacePath, committed ? After : _before);
        AssertImage(CreatePath, committed ? After : null);
        AssertImage(DeletePath, committed ? null : []);
        Assert.Equal(GenerationBytes(_generation), File.ReadAllBytes(_files.SessionGenerationPath));
        AssertClean();
    }

    [Theory]
    [InlineData("first-member", false)]
    [InlineData("Committed", true)]
    public async Task ActualHelperColdRecoveryPreservesTheOutsideHardLink(string cut, bool committed)
    {
        var outside = Path.Combine(_root, "outside-alias.bin");
        var source = _files.ResolvePath(ReplacePath);
        if (OperatingSystem.IsWindows()) Assert.True(CreateHardLink(outside, source, IntPtr.Zero));
        else Assert.Equal(0, Link(source, outside));
        Assert.Equal(_before, File.ReadAllBytes(outside));

        Assert.Equal(73, (await RunHost("coordinated-cut", cut)).Exit);
        Assert.Equal(_before, File.ReadAllBytes(outside));
        Assert.Equal(0, (await RunHost("coordinated-recover", cut)).Exit);

        AssertImage(ReplacePath, committed ? After : _before);
        AssertImage(CreatePath, committed ? After : null);
        AssertImage(DeletePath, committed ? null : []);
        Assert.Equal(_before, File.ReadAllBytes(outside));
        AssertClean();
    }

    [DllImport("libc", EntryPoint = "link", SetLastError = true)]
    private static extern int Link(string existing, string created);
    [DllImport("kernel32.dll", EntryPoint = "CreateHardLinkW", CharSet = CharSet.Unicode, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool CreateHardLink(string created, string existing, IntPtr security);

    [Fact]
    public async Task ColdRecoveryUnknownLaterMemberKeepsEarlierPublishedBytesAndEvidence()
    {
        Assert.Equal(73, (await RunHost("coordinated-cut", "first-member")).Exit);
        AssertImage(ReplacePath, After);
        byte[] unknown = [31, 32, 33];
        File.WriteAllBytes(_files.ResolvePath(CreatePath), unknown);
        var evidence = File.ReadAllBytes(Active);

        var rejected = await RunHost("coordinated-conflict", "unknown");

        Assert.Equal(0, rejected.Exit);
        Assert.Contains("unknown bytes", rejected.Output, StringComparison.Ordinal);
        Assert.Equal(evidence, File.ReadAllBytes(Active));
        AssertImage(ReplacePath, After);
        AssertImage(CreatePath, unknown);
        AssertImage(DeletePath, []);
    }

    [Fact]
    public async Task ColdRecoveryChangedGenerationKeepsCompleteEvidenceAndCurrentImages()
    {
        Assert.Equal(73, (await RunHost("coordinated-cut", "first-member")).Exit);
        var generationAfter = GenerationBytes(Guid.NewGuid().ToString("N"));
        File.WriteAllBytes(_files.SessionGenerationPath, generationAfter);
        var evidence = File.ReadAllBytes(Active);

        var rejected = await RunHost("coordinated-conflict", "generation");

        Assert.Equal(0, rejected.Exit);
        Assert.Contains("different session generation", rejected.Output, StringComparison.Ordinal);
        Assert.Equal(evidence, File.ReadAllBytes(Active));
        Assert.Equal(generationAfter, File.ReadAllBytes(_files.SessionGenerationPath));
        AssertImage(ReplacePath, After);
        AssertImage(CreatePath, null);
        AssertImage(DeletePath, []);
    }

    private async Task<(int Exit, string Output)> RunHost(string mode, string cut)
    {
        var assembly = typeof(PortableCoordinatedRecoveryTests).Assembly.Location;
        var start = new ProcessStartInfo(Environment.GetEnvironmentVariable("DOTNET_HOST_PATH") ?? "dotnet")
        {
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true
        };
        foreach (var argument in new[] { "exec", "--runtimeconfig", Path.ChangeExtension(assembly, ".runtimeconfig.json"),
                     "--depsfile", Path.ChangeExtension(assembly, ".deps.json"), typeof(PortableStorageCrashHost.Program).Assembly.Location,
                     _root, _generation, mode, cut }) start.ArgumentList.Add(argument);
        using var process = Process.Start(start) ?? throw new InvalidOperationException("The owned coordinated crash host did not start.");
        var stdout = process.StandardOutput.ReadToEndAsync();
        var stderr = process.StandardError.ReadToEndAsync();
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        try
        {
            await process.WaitForExitAsync(deadline.Token);
            var output = await stdout + await stderr;
            Assert.True(process.ExitCode is 0 or 73, output);
            return (process.ExitCode, output);
        }
        finally
        {
            if (!process.HasExited)
            {
                process.Kill(entireProcessTree: true);
                await process.WaitForExitAsync();
            }
        }
    }

    private static byte[] GenerationBytes(string id) => JsonSerializer.SerializeToUtf8Bytes(new { SchemaVersion = 1, GenerationId = id });
    private void AssertImage(string path, byte[]? expected)
    {
        var absolute = _files.ResolvePath(path);
        Assert.Equal(expected != null, File.Exists(absolute));
        if (expected != null) Assert.Equal(expected, File.ReadAllBytes(absolute));
    }
    private void AssertClean()
    {
        Assert.Empty(Directory.EnumerateFiles(_root, ".boe-local-*", SearchOption.AllDirectories));
        if (Directory.Exists(Journal)) Assert.Empty(Directory.EnumerateFileSystemEntries(Journal));
    }
    public void Dispose() { if (Directory.Exists(_root)) Directory.Delete(_root, recursive: true); }
}
