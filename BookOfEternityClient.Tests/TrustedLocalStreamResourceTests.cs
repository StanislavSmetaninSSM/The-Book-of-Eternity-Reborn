using System.Buffers.Binary;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text.Json;
using BookOfEternityClient.Core;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;
using Xunit.Abstractions;

namespace BookOfEternityClient.Tests;

/// <summary>
/// Qualifies the declared archive workload through real image publication and cold recovery on the executing platform.
/// </summary>
/// <param name="output">
/// Receives child reports and independently sampled protective resource measurements.
/// </param>
public sealed class TrustedLocalStreamResourceTests(ITestOutputHelper output) : IDisposable
{
    private const long HeapLimit = 768L * 1024 * 1024;
    private const long RssStop = 1024L * 1024 * 1024;
    private const long DiskStop = 3L * 1024 * 1024 * 1024;
    private readonly string _root = Path.Combine(Path.GetTempPath(), "boe-stream-resource-" + Guid.NewGuid().ToString("N"));
    /// <summary>
    /// Resolves only this test's owned synthetic session.
    /// </summary>
    private FileSystemManager Files => new(_root, NullLogger<FileSystemManager>.Instance);
    /// <summary>
    /// Identifies the disposable closed producer archive.
    /// </summary>
    private string Candidate => Path.Combine(_root, "candidate.zip");
    /// <summary>
    /// Identifies the single declared archive publication member.
    /// </summary>
    private string Destination => Files.ResolvePath("saves/manual_saves/resource-candidate.zip");
    /// <summary>
    /// Identifies the common authoritative B1 journal.
    /// </summary>
    private string Active => Path.Combine(Files.RuntimeRootPath, "trusted-local-publication-v1/active.json");

    /// <summary>
    /// Detects whole-image resource amplification or loss of either full archive decision during cold acquisition.
    /// </summary>
    /// <param name="mebibytes">
    /// The original experiment's filler workload size, with a 64 KiB reserve for required archive metadata.
    /// </param>
    [Theory]
    [InlineData(64)]
    [InlineData(128)]
    [InlineData(512)]
    public async Task FullArchiveImagesPublishAndRecoverWithinOriginalOwnedBounds(int mebibytes)
    {
        Assert.True(OperatingSystem.IsWindows() || OperatingSystem.IsLinux(), "The resource qualification supports Windows and Linux.");
        Directory.CreateDirectory(_root);
        var payloadBytes = (long)mebibytes * 1024 * 1024 - 64 * 1024;
        var producer = await Run("producer", payloadBytes, 0);
        Assert.True(producer.GetProperty("completed").GetBoolean(), producer.ToString());
        Assert.Contains("CompletedArchiveBoundary", producer.GetProperty("phases").EnumerateArray().Select(value => value.GetString()));
        Assert.True(producer.GetProperty("saveReturned").GetBoolean());
        Assert.Equal("ordinary-create-only-image-publication", producer.GetProperty("producerRoute").GetString());
        var archiveBytes = new FileInfo(Candidate).Length;
        var afterHash = Hash(Candidate);
        Assert.Equal(producer.GetProperty("archiveSha256").GetString(), afterHash);
        Assert.Equal(archiveBytes, producer.GetProperty("archiveBytes").GetInt64());
        Assert.InRange(producer.GetProperty("expandedBytes").GetInt64(), payloadBytes, 512L * 1024 * 1024);
        var generation = File.ReadAllBytes(Files.SessionGenerationPath);
        var library = SnapshotLibrary();
        Assert.Equal(new[] { "autosaves/existing.zip", "checkpoint_saves/existing.zip", "manual_saves/existing.zip" }, library.Keys.Order(StringComparer.Ordinal));
        var outside = Path.Combine(_root, "outside-library-alias.zip");
        if (OperatingSystem.IsWindows()) Assert.True(CreateHardLink(outside, Files.ResolvePath("saves/manual_saves/existing.zip"), IntPtr.Zero));
        else Assert.Equal(0, Link(Files.ResolvePath("saves/manual_saves/existing.zip"), outside));
        var outsideHash = Hash(outside);

        File.Copy(Candidate, Destination);
        AppendZipComment(Destination);
        TrustedLocalStreamResourceProbe.ValidateArchive(Destination);
        var beforeHash = Hash(Destination);
        Assert.NotEqual(afterHash, beforeHash);
        Assert.Equal(archiveBytes + 32, new FileInfo(Destination).Length);

        var pending = await Run("publish-pending", payloadBytes, 73);
        Assert.Equal("MemberPublished", pending.GetProperty("cut").GetString());
        Assert.Equal(0, pending.GetProperty("cutIndex").GetInt32());
        Assert.True(pending.GetProperty("beforeFileBacked").GetBoolean());
        Assert.True(pending.GetProperty("afterFileBacked").GetBoolean());
        Assert.True(pending.GetProperty("imageMatches").GetBoolean());
        Assert.Equal(archiveBytes + 32, pending.GetProperty("beforeBytes").GetInt64());
        Assert.Equal(archiveBytes, pending.GetProperty("afterBytes").GetInt64());
        Assert.Equal(beforeHash, pending.GetProperty("beforeSha256").GetString());
        Assert.Equal(afterHash, pending.GetProperty("afterSha256").GetString());
        Assert.Equal(archiveBytes * 2 + 32, pending.GetProperty("framedPayloadBytes").GetInt64());
        Assert.False(pending.GetProperty("journalCommitted").GetBoolean());
        Assert.Equal(afterHash, Hash(Destination));
        Assert.False(File.Exists(Candidate));
        Assert.True(File.Exists(Active));
        AssertPreserved(generation, library, outside, outsideHash, afterHash);

        var rolledBack = await Run("recover-pending", payloadBytes, 0);
        Assert.True(rolledBack.GetProperty("completed").GetBoolean(), rolledBack.ToString());
        Assert.False(rolledBack.GetProperty("journalCommitted").GetBoolean());
        Assert.Equal(beforeHash, rolledBack.GetProperty("recoveredSha256").GetString());
        Assert.Equal(archiveBytes + 32, rolledBack.GetProperty("recoveredBytes").GetInt64());
        AssertPreserved(generation, library, outside, outsideHash, beforeHash);
        AssertClean();

        // Only after cold rollback returns, the fixture derives another exact original candidate.
        CopyPrefix(Destination, Candidate, archiveBytes);
        Assert.Equal(afterHash, Hash(Candidate));
        var committed = await Run("publish-committed", payloadBytes, 0);
        Assert.True(committed.GetProperty("completed").GetBoolean(), committed.ToString());
        Assert.Equal("Committed", committed.GetProperty("disposition").GetString());
        Assert.Equal("RetainCommittedEvidence", committed.GetProperty("cleanupFailureType").GetString());
        Assert.True(committed.GetProperty("journalCommitted").GetBoolean());
        Assert.True(committed.GetProperty("imageMatches").GetBoolean());
        Assert.Equal(archiveBytes * 2 + 32, committed.GetProperty("framedPayloadBytes").GetInt64());
        Assert.False(File.Exists(Candidate));
        Assert.True(File.Exists(Active));
        AssertPreserved(generation, library, outside, outsideHash, afterHash);

        var cleaned = await Run("recover-committed", payloadBytes, 0);
        Assert.True(cleaned.GetProperty("completed").GetBoolean(), cleaned.ToString());
        Assert.True(cleaned.GetProperty("journalCommitted").GetBoolean());
        Assert.Equal(afterHash, cleaned.GetProperty("recoveredSha256").GetString());
        Assert.Equal(archiveBytes, cleaned.GetProperty("recoveredBytes").GetInt64());
        AssertPreserved(generation, library, outside, outsideHash, afterHash);
        AssertClean();
    }

    /// <summary>
    /// Starts one owned child with the original heap, sampled RSS, disk and elapsed-time bounds.
    /// </summary>
    /// <param name="mode">
    /// Selects producer, publication or ordinary canonical recovery behavior.
    /// </param>
    /// <param name="payloadBytes">
    /// Specifies the filler bytes used by the producer.
    /// </param>
    /// <param name="expectedExit">
    /// The expected ordinary return or deliberate abrupt publication exit code.
    /// </param>
    /// <returns>
    /// The completed child's report; a safety stop or unexpected exit fails the test.
    /// </returns>
    private async Task<JsonElement> Run(string mode, long payloadBytes, int expectedExit)
    {
        var assembly = typeof(TrustedLocalStreamResourceTests).Assembly.Location;
        var start = new ProcessStartInfo(Environment.GetEnvironmentVariable("DOTNET_HOST_PATH") ?? "dotnet")
        { UseShellExecute = false, RedirectStandardOutput = true, RedirectStandardError = true };
        foreach (var argument in new[] { "exec", "--runtimeconfig", Path.ChangeExtension(assembly, ".runtimeconfig.json"),
            "--depsfile", Path.ChangeExtension(assembly, ".deps.json"), typeof(PortableStorageCrashHost.Program).Assembly.Location,
            _root, payloadBytes.ToString(), "stream-resource", mode }) start.ArgumentList.Add(argument);
        start.Environment["DOTNET_GCHeapHardLimit"] = "0x30000000";
        using var process = Process.Start(start) ?? throw new InvalidOperationException("Owned image resource child did not start.");
        var stdout = process.StandardOutput.ReadToEndAsync();
        var stderr = process.StandardError.ReadToEndAsync();
        var timer = Stopwatch.StartNew();
        long peakRss = 0, peakDisk = 0;
        try
        {
            while (!process.HasExited)
            {
                try
                {
                    process.Refresh();
                    peakRss = Math.Max(peakRss, process.WorkingSet64);
                }
                catch (InvalidOperationException) when (process.HasExited)
                {
                    // The owned child can exit between the loop guard and the RSS query.
                    // Its exact exit code and final resource report are still required below.
                    break;
                }
                peakDisk = Math.Max(peakDisk, OwnedDiskBytes());
                if (peakRss > RssStop || peakDisk > DiskStop || timer.Elapsed > TimeSpan.FromSeconds(120))
                    throw new InvalidOperationException($"Owned image resource safety stop: mode={mode}, RSS={peakRss}, disk={peakDisk}, elapsed={timer.Elapsed}.");
                await Task.Delay(25);
            }
            await process.WaitForExitAsync();
            var childOutput = await stdout;
            Assert.True(process.ExitCode == expectedExit, childOutput + await stderr);
            peakDisk = Math.Max(peakDisk, OwnedDiskBytes());
            Assert.InRange(peakDisk, 0, DiskStop);
            using var report = JsonDocument.Parse(File.ReadAllText(Path.Combine(_root, mode + "-report.json")));
            Assert.Equal(OperatingSystem.IsWindows() ? "Windows" : "Linux", report.RootElement.GetProperty("platform").GetString());
            Assert.Equal(HeapLimit, report.RootElement.GetProperty("gcAvailableBytes").GetInt64());
            Assert.InRange(report.RootElement.GetProperty("peakWorkingSetBytes").GetInt64(), 0, RssStop);
            output.WriteLine("{0}; parentSampledPeakBytes={1}; parentSampledDiskBytes={2}; parentMilliseconds={3}",
                report.RootElement.ToString(), peakRss, peakDisk, timer.Elapsed.TotalMilliseconds);
            return report.RootElement.Clone();
        }
        finally
        {
            if (!process.HasExited) { process.Kill(entireProcessTree: true); await process.WaitForExitAsync(); }
        }
    }

    /// <summary>
    /// Counts current file lengths in the owned fixture, tolerating files removed by its child between samples.
    /// </summary>
    /// <returns>
    /// The recursive logical byte total, conservatively counting each hard-link name.
    /// </returns>
    private long OwnedDiskBytes()
    {
        long total = 0;
        var remaining = new Stack<string>(); remaining.Push(_root);
        while (remaining.TryPop(out var directory))
        {
            string[] children, files;
            try { children = Directory.GetDirectories(directory); files = Directory.GetFiles(directory); }
            catch (DirectoryNotFoundException) { continue; }
            foreach (var child in children) remaining.Push(child);
            foreach (var path in files)
            {
                try { total = checked(total + new FileInfo(path).Length); }
                catch (FileNotFoundException) { }
                catch (DirectoryNotFoundException) { }
            }
        }
        return total;
    }

    /// <summary>
    /// Captures every save-library member by relative name and complete streamed hash.
    /// </summary>
    /// <returns>
    /// The exact current membership and hashes.
    /// </returns>
    private Dictionary<string, string> SnapshotLibrary() => Directory.GetFiles(Files.ResolvePath("saves"), "*", SearchOption.AllDirectories)
        .ToDictionary(path => Path.GetRelativePath(Files.ResolvePath("saves"), path).Replace('\\', '/'), Hash, StringComparer.Ordinal);

    /// <summary>
    /// Verifies the complete library decision, unchanged generation bytes and outside alias.
    /// </summary>
    /// <param name="generation">
    /// The original complete generation document.
    /// </param>
    /// <param name="library">
    /// The preexisting library members and hashes.
    /// </param>
    /// <param name="outside">
    /// The outside hard-link sentinel path.
    /// </param>
    /// <param name="outsideHash">
    /// The sentinel's original complete hash.
    /// </param>
    /// <param name="targetHash">
    /// The exact expected archive decision hash.
    /// </param>
    private void AssertPreserved(byte[] generation, Dictionary<string, string> library, string outside, string outsideHash, string targetHash)
    {
        Assert.Equal(generation, File.ReadAllBytes(Files.SessionGenerationPath));
        Assert.Equal(outsideHash, Hash(outside));
        var current = SnapshotLibrary();
        Assert.Equal(library.Keys.Append("manual_saves/resource-candidate.zip").Order(StringComparer.Ordinal), current.Keys.Order(StringComparer.Ordinal));
        foreach (var member in library) Assert.Equal(member.Value, current[member.Key]);
        Assert.Equal(targetHash, current["manual_saves/resource-candidate.zip"]);
    }

    /// <summary>
    /// Verifies removal of journal debt, sibling staging/undo files and producer staging.
    /// </summary>
    private void AssertClean()
    {
        Assert.Empty(Directory.EnumerateFileSystemEntries(Path.GetDirectoryName(Active)!));
        Assert.Empty(Directory.GetFiles(Files.GameSessionPath, ".boe-local-*", SearchOption.AllDirectories));
        Assert.False(Directory.Exists(Path.Combine(Files.RuntimeRootPath, "save-staging")));
        Assert.False(File.Exists(Candidate));
    }

    /// <summary>
    /// Adds a valid ZIP comment to produce a distinct complete before-image without changing entry payloads.
    /// </summary>
    /// <param name="path">
    /// The fixture-owned copy of the producer's closed ZIP, which has no existing comment.
    /// </param>
    private static void AppendZipComment(string path)
    {
        using var stream = new FileStream(path, FileMode.Open, FileAccess.ReadWrite, FileShare.None);
        stream.Position = stream.Length - 22;
        var trailer = new byte[22];
        stream.ReadExactly(trailer);
        Assert.Equal(0x06054b50U, BinaryPrimitives.ReadUInt32LittleEndian(trailer));
        Assert.Equal(0, BinaryPrimitives.ReadUInt16LittleEndian(trailer.AsSpan(20)));
        stream.Position = stream.Length - 2;
        stream.Write(new byte[] { 32, 0 });
        stream.Write(Enumerable.Repeat((byte)0x42, 32).ToArray());
        stream.Flush(flushToDisk: true);
    }

    /// <summary>
    /// Recreates the original ZIP after rollback by copying its exact prefix in bounded chunks.
    /// </summary>
    /// <param name="source">
    /// The fully restored before-image containing the added comment.
    /// </param>
    /// <param name="destination">
    /// The new disposable candidate path.
    /// </param>
    /// <param name="length">
    /// The original producer ZIP length, excluding the comment.
    /// </param>
    private static void CopyPrefix(string source, string destination, long length)
    {
        using var input = File.OpenRead(source);
        using var output = File.Create(destination);
        var buffer = new byte[64 * 1024];
        for (long remaining = length; remaining > 0;)
        {
            var take = (int)Math.Min(buffer.Length, remaining);
            input.ReadExactly(buffer.AsSpan(0, take));
            output.Write(buffer, 0, take);
            remaining -= take;
        }
        // Undo the before-image's EOCD comment length as well as omitting its trailing comment.
        output.Position = output.Length - 2;
        output.Write(new byte[] { 0, 0 });
        output.Flush(flushToDisk: true);
    }

    /// <summary>
    /// Hashes the entire named file without loading it into memory.
    /// </summary>
    /// <param name="path">
    /// The existing file to hash.
    /// </param>
    /// <returns>
    /// The uppercase SHA-256 value.
    /// </returns>
    private static string Hash(string path) { using var stream = File.OpenRead(path); return Convert.ToHexString(SHA256.HashData(stream)); }

    /// <summary>
    /// Creates the native outside alias used to check preservation of another library name.
    /// </summary>
    /// <param name="created">
    /// The new outside name.
    /// </param>
    /// <param name="existing">
    /// The existing library sentinel name.
    /// </param>
    /// <param name="security">
    /// The unused native security argument, passed as zero.
    /// </param>
    /// <returns>
    /// <see langword="true"/> when Windows creates the hard link; otherwise, <see langword="false"/>.
    /// </returns>
    [DllImport("kernel32.dll", EntryPoint = "CreateHardLinkW", CharSet = CharSet.Unicode, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool CreateHardLink(string created, string existing, IntPtr security);

    /// <summary>
    /// Creates the Linux outside alias used to check preservation of another library name.
    /// </summary>
    /// <param name="existing">
    /// The existing library sentinel name.
    /// </param>
    /// <param name="created">
    /// The new outside name.
    /// </param>
    /// <returns>
    /// Zero on success, or the native failure status.
    /// </returns>
    [DllImport("libc", EntryPoint = "link", SetLastError = true)]
    private static extern int Link(string existing, string created);

    /// <summary>
    /// Deletes only this test's independently owned mutable root after its children have stopped.
    /// </summary>
    public void Dispose() { if (Directory.Exists(_root)) Directory.Delete(_root, recursive: true); }
}
