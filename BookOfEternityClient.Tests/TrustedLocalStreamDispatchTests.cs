using System.Buffers.Binary;
using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using BookOfEternityClient.Core;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;
using Xunit.Abstractions;

namespace BookOfEternityClient.Tests;

public sealed class TrustedLocalStreamDispatchTests(ITestOutputHelper output) : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "boe-stream-dispatch-" + Guid.NewGuid().ToString("N"));
    private readonly string _generation = Guid.NewGuid().ToString("N");
    private const long HeapLimit = 64L * 1024 * 1024;
    private string Active => Path.Combine(_root, ".boe_runtime/trusted-local-publication-v1/active.json");
    private FileSystemManager Seed()
    {
        var files = new FileSystemManager(_root, NullLogger<FileSystemManager>.Instance); files.EnsureDirectoryStructure();
        Directory.CreateDirectory(Path.GetDirectoryName(files.SessionGenerationPath)!);
        File.WriteAllBytes(files.SessionGenerationPath, JsonSerializer.SerializeToUtf8Bytes(new { schemaVersion = 1, generationId = _generation }));
        Directory.CreateDirectory(Path.GetDirectoryName(Active)!);
        foreach (var directory in new[] { "manual_saves", "autosaves", "checkpoint_saves" })
            File.WriteAllBytes(files.ResolvePath($"saves/{directory}/existing.zip"), [7, 0, 255]);
        return files;
    }

    [Theory]
    [InlineData("unknown-version")]
    [InlineData("partial-magic")]
    [InlineData("json-looking-magic")]
    public async Task UnknownLargeFrameRejectsBeforeLegacyWholeFileAllocation(string corruption)
    {
        var files = Seed(); var generation = File.ReadAllBytes(files.SessionGenerationPath);
        const long regionLength = 128L * 1024 * 1024;
        var zeroChunk = new byte[64 * 1024];
        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        for (long bytes = 0; bytes < regionLength; bytes += zeroChunk.Length) hash.AppendData(zeroChunk);
        var header = JsonSerializer.SerializeToUtf8Bytes(new
        {
            Format = 2, TransactionId = "0123456789abcdef0123456789abcdef", Committed = false,
            GenerationBefore = new { Exists = true, Id = _generation }, GenerationAfter = new { Exists = true, Id = _generation },
            Members = new[] { new { Path = files.ResolvePath("game_state/core/create.bin"),
                Before = new { Exists = false, Length = 0L, Sha256 = (string?)null, Offset = (long?)null },
                After = new { Exists = true, Length = regionLength, Sha256 = (string?)Convert.ToHexString(hash.GetHashAndReset()), Offset = (long?)0 } } }
        });
        byte[] magic = corruption switch
        {
            "unknown-version" => Encoding.ASCII.GetBytes("BOELP3\r\n"),
            "partial-magic" => [(byte)'B', (byte)'O', (byte)'E', 0, 0, 0, 0, 0],
            _ => Encoding.ASCII.GetBytes("{OELP2\r\n")
        };
        using (var stream = File.Create(Active))
        {
            stream.Write(magic); var length = new byte[8]; BinaryPrimitives.WriteInt64LittleEndian(length, header.Length);
            stream.Write(length); stream.Write(header);
            stream.SetLength(16 + header.Length + regionLength); stream.Flush(flushToDisk: true);
        }
        var evidenceHash = Hash(Active);
        var result = await RecoverInOwnedProcess();
        Assert.Equal("CanonicalAcquisitionFailed", result.Report.GetProperty("Phase").GetString());
        Assert.Equal(HeapLimit, result.Report.GetProperty("HeapBytes").GetInt64());
        Assert.True(result.ExitCode == 78, result.Report.ToString()); // OOM is not a safe unknown-format rejection.
        Assert.Equal("InvalidDataException", result.Report.GetProperty("FailureType").GetString());
        Assert.Contains("publication", result.Report.GetProperty("Message").GetString(), StringComparison.OrdinalIgnoreCase);
        Assert.Contains("ReadJournal", result.Report.GetProperty("Stack").GetString(), StringComparison.Ordinal);
        Assert.Equal(evidenceHash, Hash(Active)); Assert.False(File.Exists(files.ResolvePath("game_state/core/create.bin")));
        Assert.Equal(generation, File.ReadAllBytes(files.SessionGenerationPath));
        foreach (var directory in new[] { "manual_saves", "autosaves", "checkpoint_saves" })
            Assert.Equal(new byte[] { 7, 0, 255 }, File.ReadAllBytes(files.ResolvePath($"saves/{directory}/existing.zip")));
    }

    [Fact]
    public async Task PlausibleV1PreservesReorderedPropertiesAndLongLeadingWhitespace()
    {
        var files = Seed(); var target = files.ResolvePath("game_state/core/replace.bin"); File.WriteAllBytes(target, [2]);
        var header = new Dictionary<string, object>
        {
            ["Members"] = new[] { new { Path = target,
                Before = new { Exists = true, Bytes = new byte[] { 1 }, Sha256 = Convert.ToHexString(SHA256.HashData(new byte[] { 1 })) },
                After = new { Exists = true, Bytes = new byte[] { 2 }, Sha256 = Convert.ToHexString(SHA256.HashData(new byte[] { 2 })) } } },
            ["GenerationBefore"] = new { Exists = true, Id = _generation }, ["GenerationAfter"] = new { Exists = true, Id = _generation },
            ["TransactionId"] = "0123456789abcdef0123456789abcdef", ["Committed"] = false, ["Format"] = 1
        };
        var json = JsonSerializer.Serialize(header); Assert.StartsWith("{\"Members\"", json);
        File.WriteAllText(Active, new string(' ', 256 * 1024) + json);
        var result = await RecoverInOwnedProcess(); Assert.True(result.ExitCode == 0, result.Report.ToString());
        Assert.Equal(new byte[] { 1 }, File.ReadAllBytes(target)); Assert.False(File.Exists(Active));
    }

    private async Task<(int ExitCode, JsonElement Report)> RecoverInOwnedProcess()
    {
        var assembly = typeof(TrustedLocalStreamDispatchTests).Assembly.Location;
        var start = new ProcessStartInfo(Environment.GetEnvironmentVariable("DOTNET_HOST_PATH") ?? "dotnet")
        { UseShellExecute = false, RedirectStandardOutput = true, RedirectStandardError = true };
        foreach (var argument in new[] { "exec", "--runtimeconfig", Path.ChangeExtension(assembly, ".runtimeconfig.json"),
            "--depsfile", Path.ChangeExtension(assembly, ".deps.json"), typeof(PortableStorageCrashHost.Program).Assembly.Location,
            _root, HeapLimit.ToString(), "stream-recover", "unused" }) start.ArgumentList.Add(argument);
        start.Environment["DOTNET_GCHeapHardLimit"] = "0x4000000";
        using var process = Process.Start(start) ?? throw new InvalidOperationException("Owned cold dispatch host did not start.");
        var stdout = process.StandardOutput.ReadToEndAsync(); var stderr = process.StandardError.ReadToEndAsync();
        var timer = Stopwatch.StartNew(); long peak = 0;
        try
        {
            while (!process.HasExited)
            {
                process.Refresh(); peak = Math.Max(peak, process.WorkingSet64);
                if (peak > 512L * 1024 * 1024 || timer.Elapsed > TimeSpan.FromSeconds(30))
                    throw new InvalidOperationException("Owned cold dispatch host exceeded its protective bound.");
                await Task.Delay(25);
            }
            await process.WaitForExitAsync(); var text = await stdout;
            Assert.True(process.ExitCode is 0 or 78 or 90, text + await stderr);
            using var report = JsonDocument.Parse(text);
            output.WriteLine("exit={0}; sampledPeakBytes={1}; {2}", process.ExitCode, peak, report.RootElement.ToString());
            return (process.ExitCode, report.RootElement.Clone());
        }
        finally { if (!process.HasExited) { process.Kill(entireProcessTree: true); await process.WaitForExitAsync(); } }
    }
    private static string Hash(string path) { using var stream = File.OpenRead(path); return Convert.ToHexString(SHA256.HashData(stream)); }
    public void Dispose() { if (Directory.Exists(_root)) Directory.Delete(_root, recursive: true); }
}
