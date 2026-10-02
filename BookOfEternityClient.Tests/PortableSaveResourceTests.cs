using System.Diagnostics;
using System.Security.Cryptography;
using System.Text.Json;
using BookOfEternityClient.Core;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;
using Xunit.Abstractions;

namespace BookOfEternityClient.Tests;

public sealed class PortableSaveResourceTests(ITestOutputHelper output) : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "boe-save-resource-" + Guid.NewGuid().ToString("N"));
    private const long RssStop = 1024L * 1024 * 1024;
    private const long DiskStop = 3L * 1024 * 1024 * 1024;

    [Theory]
    [InlineData(64)]
    [InlineData(128)]
    [InlineData(512)]
    public async Task MeasureUnchangedProducerAndB1CandidateWithinDeclaredOwnedBounds(int mebibytes)
    {
        Assert.True(OperatingSystem.IsLinux(), "This qualification deliberately measures the unchanged Linux producer stop.");
        Directory.CreateDirectory(_root);
        var payloadBytes = (long)mebibytes * 1024 * 1024 - 64 * 1024; // Leave room for current required JSON/manifest metadata.
        var producer = await Run("producer", payloadBytes);
        Assert.True(producer.GetProperty("completed").GetBoolean(), producer.ToString());
        var files = new FileSystemManager(_root, NullLogger<FileSystemManager>.Instance);
        var generation = File.ReadAllBytes(files.SessionGenerationPath);
        var sentinels = Directory.GetFiles(files.ResolvePath("saves"), "existing.zip", SearchOption.AllDirectories)
            .ToDictionary(path => path, File.ReadAllBytes);
        Assert.Equal(3, sentinels.Count);
        var candidate = Path.Combine(_root, "candidate.zip");
        var candidateHash = Hash(candidate);
        var b1 = await Run("b1", payloadBytes);
        var committed = b1.GetProperty("completed").GetBoolean();
        // This is an experiment, not a success envelope test: a bounded rejection is evidence to review.
        if (!committed) Assert.True(b1.TryGetProperty("failure", out _), b1.ToString());
        var recovery = await Run("recover", payloadBytes);
        Assert.True(recovery.GetProperty("completed").GetBoolean(), recovery.ToString());
        var destination = files.ResolvePath("saves/manual_saves/resource-candidate.zip");
        Assert.Equal(committed, File.Exists(destination));
        if (committed) Assert.Equal(candidateHash, Hash(destination));
        Assert.Equal(candidateHash, Hash(candidate));
        Assert.Equal(generation, File.ReadAllBytes(files.SessionGenerationPath));
        foreach (var (path, bytes) in sentinels) Assert.Equal(bytes, File.ReadAllBytes(path));
        Assert.Empty(Directory.GetFiles(Path.Combine(files.RuntimeRootPath, "trusted-local-publication-v1")));
        Assert.Empty(Directory.GetFiles(files.GameSessionPath, ".boe-local-*", SearchOption.AllDirectories));
        Assert.False(Directory.Exists(Path.Combine(files.RuntimeRootPath, "save-staging")));
    }

    private async Task<JsonElement> Run(string mode, long bytes)
    {
        var assembly = typeof(PortableSaveResourceTests).Assembly.Location;
        var start = new ProcessStartInfo(Environment.GetEnvironmentVariable("DOTNET_HOST_PATH") ?? "dotnet")
        { UseShellExecute = false, RedirectStandardOutput = true, RedirectStandardError = true };
        foreach (var arg in new[] { "exec", "--runtimeconfig", Path.ChangeExtension(assembly, ".runtimeconfig.json"),
            "--depsfile", Path.ChangeExtension(assembly, ".deps.json"), typeof(PortableStorageCrashHost.Program).Assembly.Location,
            _root, bytes.ToString(), "save-resource", mode }) start.ArgumentList.Add(arg);
        // .NET environment numeric GC settings are hexadecimal. Applied ONLY to this owned probe.
        start.Environment["DOTNET_GCHeapHardLimit"] = "0x30000000"; // 768 MiB
        using var process = Process.Start(start) ?? throw new InvalidOperationException("Owned resource probe did not start.");
        var stdout = process.StandardOutput.ReadToEndAsync(); var stderr = process.StandardError.ReadToEndAsync();
        var timer = Stopwatch.StartNew(); long sampledPeak = 0;
        try
        {
            while (!process.HasExited)
            {
                process.Refresh(); sampledPeak = Math.Max(sampledPeak, process.WorkingSet64);
                if (sampledPeak > RssStop || timer.Elapsed > TimeSpan.FromSeconds(120) ||
                    Directory.GetFiles(_root, "*", SearchOption.AllDirectories).Sum(path => new FileInfo(path).Length) > DiskStop)
                    throw new InvalidOperationException($"Owned qualification safety stop: mode={mode}, RSS={sampledPeak}, elapsed={timer.Elapsed}.");
                await Task.Delay(25);
            }
            await process.WaitForExitAsync();
            Assert.True(process.ExitCode == 0, await stdout + await stderr);
            using var report = JsonDocument.Parse(File.ReadAllText(Path.Combine(_root, mode + "-report.json")));
            output.WriteLine("{0}; parentSampledPeakBytes={1}; parentMilliseconds={2}", report.RootElement.ToString(), sampledPeak, timer.Elapsed.TotalMilliseconds);
            return report.RootElement.Clone();
        }
        finally { if (!process.HasExited) { process.Kill(entireProcessTree: true); await process.WaitForExitAsync(); } }
    }
    private static string Hash(string path) { using var stream = File.OpenRead(path); return Convert.ToHexString(SHA256.HashData(stream)); }
    public void Dispose() { if (Directory.Exists(_root)) Directory.Delete(_root, recursive: true); }
}
