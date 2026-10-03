using System.Diagnostics;
using System.Text;
using System.Text.Json;
using BookOfEternityClient.Core;
using BookOfEternityClient.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;
using Xunit.Abstractions;

namespace BookOfEternityClient.Tests;

/// <summary>
/// Qualifies real load preparation, publication and journal-only recovery in separate bounded owned processes.
/// </summary>
/// <param name="output">
/// Receives actual seed/phase reports and independently sampled RSS, disk and elapsed metrics.
/// </param>
public sealed class PortableLoadResourceTests(ITestOutputHelper output) : IDisposable
{
    private const long MiB = 1024L * 1024;
    private const long HeapBytes = 768 * MiB;
    private const long RssStop = 1024 * MiB;
    private const long DiskStop = 5L * 1024 * MiB;
    private readonly string _root = Path.Combine(Path.GetTempPath(), "boe-load-resource-" + Guid.NewGuid().ToString("N"));

    /// <summary>
    /// Resolves the isolated owned root without preparing or extracting another archive.
    /// </summary>
    private FileSystemManager Files => new(_root, NullLogger<FileSystemManager>.Instance);

    /// <summary>
    /// Measures actual original-manifest preparation while deliberately stopping before lease/publication.
    /// </summary>
    /// <param name="workload">
    /// 64, 128, near-512 MiB expanded payloads or exact many-member/name input.
    /// </param>
    /// <returns>
    /// Completion after exact Before namespace/generation/source/library and cleanup verification.
    /// </returns>
    [Theory]
    [InlineData("64")]
    [InlineData("128")]
    [InlineData("512")]
    [InlineData("many")]
    public async Task ActualPreparationFitsDeclaredLoadResourceEnvelope(string workload)
    {
        var seed = await RunChildAsync("seed", workload);
        AssertSeed(seed, workload);
        var expected = PortableLoadFixture.Read(_root);
        var prepared = await RunChildAsync("preparation", workload);
        AssertPhase(prepared, "PreparedBoundaryReturned");
        Assert.Equal("NotLoaded", prepared.GetProperty("Disposition").GetString());
        Assert.False(prepared.GetProperty("NeedsFollowUp").GetBoolean());
        Assert.False(prepared.GetProperty("ContinuationBlocked").GetBoolean());
        Assert.Null(prepared.GetProperty("EstablishedGeneration").GetString());
        Assert.Equal(0, prepared.GetProperty("CommittedObserverCount").GetInt32());
        Assert.Equal(0, prepared.GetProperty("PublicationObserverCount").GetInt32());
        Assert.Equal(0, prepared.GetProperty("MemberCount").GetInt32());
        Assert.Equal(0L, prepared.GetProperty("MetadataLength").GetInt64());
        Assert.Equal("PreparationMeasured", prepared.GetProperty("DiagnosticType").GetString());
        Assert.True(prepared.GetProperty("PreparedBoundaryObserved").GetBoolean());
        AssertExplicitBytes(expected, prepared, after: false);
        AssertOwnedScratchEmpty();
    }

    /// <summary>
    /// Measures actual typed load from its prepared boundary through exactly one committed replacement decision.
    /// </summary>
    /// <param name="workload">
    /// The isolated legal bulk or many-member/name workload.
    /// </param>
    /// <returns>
    /// Completion after complete archive-derived After namespace, exact generation and protected bytes are verified.
    /// </returns>
    [Theory]
    [InlineData("64")]
    [InlineData("128")]
    [InlineData("512")]
    [InlineData("many")]
    public async Task ActualPublicationFitsDeclaredLoadResourceEnvelope(string workload)
    {
        var seed = await RunChildAsync("seed", workload);
        AssertSeed(seed, workload);
        var expected = PortableLoadFixture.Read(_root);
        var published = await RunChildAsync("publication", workload);
        AssertPhase(published, "TypedLoadCommitted");
        Assert.Equal("Committed", published.GetProperty("Disposition").GetString());
        Assert.Equal(1, published.GetProperty("CommittedObserverCount").GetInt32());
        Assert.True(published.GetProperty("PublicationObserverCount").GetInt32() > 1);
        Assert.True(published.GetProperty("PreparedBoundaryObserved").GetBoolean());
        Assert.False(published.GetProperty("NeedsFollowUp").GetBoolean());
        Assert.False(published.GetProperty("ContinuationBlocked").GetBoolean());
        Assert.False(string.IsNullOrEmpty(published.GetProperty("EstablishedGeneration").GetString()));
        AssertMetadata(published, workload);
        AssertExplicitBytes(expected, published, after: true);
        AssertOwnedScratchEmpty();
    }

    /// <summary>
    /// Measures fresh ordinary acquisition after an actual load cut with private extraction removed.
    /// </summary>
    /// <param name="workload">
    /// Bulk cases recover pending complete publication; the many-member case cleans committed evidence.
    /// </param>
    /// <returns>
    /// Completion after exact large Before restoration or committed many-member After preservation and owned cleanup.
    /// </returns>
    [Theory]
    [InlineData("64")]
    [InlineData("128")]
    [InlineData("512")]
    [InlineData("many")]
    public async Task ActualColdRecoveryFitsDeclaredLoadResourceEnvelope(string workload)
    {
        var seed = await RunChildAsync("seed", workload);
        AssertSeed(seed, workload);
        var expected = PortableLoadFixture.Read(_root);
        var cut = await RunChildAsync("cut", workload, terminateAtCut: true);
        AssertPhase(cut, "CutReady");
        var committed = workload == "many";
        Assert.Equal(committed ? "Committed" : "MemberPublished", cut.GetProperty("Cut").GetString());
        Assert.Equal(committed, cut.GetProperty("CommittedInput").GetBoolean());
        Assert.Equal(committed ? 1 : 0, cut.GetProperty("CommittedObserverCount").GetInt32());
        if (committed) Assert.Equal(-1, cut.GetProperty("Index").GetInt32());
        else Assert.Equal("@generation", cut.GetProperty("MemberPath").GetString());
        AssertMetadata(cut, workload);
        AssertExplicitBytes(expected, cut, after: true);
        DeleteExactExtraction(cut);
        var recovered = await RunChildAsync("recovery", workload);
        AssertPhase(recovered, "CanonicalAcquisitionReturned");
        AssertMetadata(recovered, workload);
        Assert.Equal(cut.GetProperty("TransactionId").GetString(), recovered.GetProperty("TransactionId").GetString());
        Assert.Equal(committed, recovered.GetProperty("CommittedInput").GetBoolean());
        Assert.Equal(cut.GetProperty("GenerationAfter").GetString(), recovered.GetProperty("GenerationAfter").GetString());
        AssertExplicitBytes(expected, recovered, committed);
        AssertOwnedScratchEmpty();
    }

    /// <summary>
    /// Requires actual original archive counts and the declared old-live workload before accepting a phase measurement.
    /// </summary>
    /// <param name="report">
    /// The separately measured seed child report.
    /// </param>
    /// <param name="workload">
    /// The exact requested bulk or many-member scenario.
    /// </param>
    private static void AssertSeed(JsonElement report, string workload)
    {
        Assert.Equal("SeedCompleted", report.GetProperty("Phase").GetString());
        Assert.True(report.GetProperty("Completed").GetBoolean(), report.ToString());
        Assert.Equal(HeapBytes, report.GetProperty("HeapBytes").GetInt64());
        Assert.InRange(report.GetProperty("ArchiveEntries").GetInt32(), 1, 8192);
        Assert.InRange(report.GetProperty("ManifestBytes").GetInt64(), 1, 4 * MiB);
        Assert.InRange(report.GetProperty("ArchiveNameBytes").GetInt64(), 1, 2 * MiB);
        Assert.InRange(report.GetProperty("ExpandedBytes").GetInt64(), 1, 512 * MiB);
        Assert.Equal(report.GetProperty("ArchiveEntries").GetInt32() - 1, report.GetProperty("IncomingFileCount").GetInt32());
        Assert.True(report.GetProperty("PhaseAllocatedBytes").GetInt64() > 0);
        Assert.True(report.GetProperty("PhaseMilliseconds").GetDouble() > 0);
        Assert.InRange(report.GetProperty("ProcessPeakWorkingSetBytes").GetInt64(), 1, RssStop);
        Assert.InRange(report.GetProperty("PredictedPayloadFootprintBytes").GetInt64(), 1, DiskStop);
        if (workload == "many")
        {
            Assert.Equal(8192, report.GetProperty("ArchiveEntries").GetInt32());
            Assert.InRange(report.GetProperty("ArchiveNameBytes").GetInt64(), 2 * MiB - 4096, 2 * MiB);
            Assert.Equal(9216, report.GetProperty("OldAdditionalFileCount").GetInt32());
            Assert.Equal(9216L * 128, report.GetProperty("OldAdditionalBytes").GetInt64());
            Assert.Equal(8192, report.GetProperty("BaselineEntryCount").GetInt32() + report.GetProperty("AdditionalIncomingCount").GetInt32());
        }
        else
        {
            var requested = long.Parse(workload) * MiB - 64 * 1024;
            Assert.Equal(requested, report.GetProperty("RequestedBulkPayloadBytes").GetInt64());
            Assert.Equal(requested, report.GetProperty("OldAdditionalBytes").GetInt64());
            Assert.InRange(report.GetProperty("ExpandedBytes").GetInt64(), requested, long.Parse(workload) * MiB);
        }
    }

    /// <summary>
    /// Rejects guard stops, diagnostic setup failures, missing measurement or incomplete physical verification.
    /// </summary>
    /// <param name="report">
    /// The actual phase child report.
    /// </param>
    /// <param name="phase">
    /// The exact completed phase or actual waiting cut announcement.
    /// </param>
    private static void AssertPhase(JsonElement report, string phase)
    {
        Assert.Equal(phase, report.GetProperty("Phase").GetString());
        Assert.True(report.GetProperty("Completed").GetBoolean(), report.ToString());
        Assert.Equal(HeapBytes, report.GetProperty("HeapBytes").GetInt64());
        Assert.True(report.GetProperty("VerifiedNamespace").GetBoolean());
        Assert.True(report.GetProperty("VerifiedGeneration").GetBoolean());
        Assert.True(report.GetProperty("VerifiedProtected").GetBoolean());
        Assert.True(report.GetProperty("PhaseAllocatedBytes").GetInt64() > 0);
        Assert.True(report.GetProperty("PhaseMilliseconds").GetDouble() > 0);
        Assert.InRange(report.GetProperty("ProcessPeakWorkingSetBytes").GetInt64(), 1, RssStop);
        Assert.False(report.TryGetProperty("Failure", out _), report.ToString());
    }

    /// <summary>
    /// Requires actual unbounded-metadata transport and full member inventory for the many-member workload.
    /// </summary>
    /// <param name="report">
    /// The actual publication or recovery frame measurements.
    /// </param>
    /// <param name="workload">
    /// The requested legal bulk or many-member workload.
    /// </param>
    private static void AssertMetadata(JsonElement report, string workload)
    {
        Assert.True(report.GetProperty("MetadataLength").GetInt64() > 0);
        Assert.True(report.GetProperty("MemberCount").GetInt32() > 0);
        if (workload == "many")
        {
            Assert.True(report.GetProperty("MetadataLength").GetInt64() > MiB);
            Assert.True(report.GetProperty("MemberCount").GetInt32() > 8192 + 9216);
        }
    }

    /// <summary>
    /// Compares explicit source, config, history, soul and exact generation bytes to original fixture expectations.
    /// </summary>
    /// <param name="expected">
    /// The independent original archive/Before snapshots exported before load.
    /// </param>
    /// <param name="report">
    /// The child report after complete namespace hash verification.
    /// </param>
    /// <param name="after">
    /// Selects complete incoming payloads when <see langword="true"/> or complete old live bytes otherwise.
    /// </param>
    private void AssertExplicitBytes(PortableLoadFixtureState expected, JsonElement report, bool after)
    {
        var target = after ? expected.After : expected.Before;
        Assert.Equal(after, report.GetProperty("After").GetBoolean());
        Assert.Equal(target.Count(pair => pair.Value != "Directory"), report.GetProperty("VerifiedFiles").GetInt32());
        Assert.Equal(target.Count(pair => pair.Value == "Directory"), report.GetProperty("VerifiedDirectories").GetInt32());
        Assert.Equal(expected.Protected[PortableLoadFixture.SourceRelative], report.GetProperty("SourceSha256").GetString());
        Assert.Equal(target["config.json"], report.GetProperty("ConfigSha256").GetString());
        Assert.Equal(target[ResourceMaterializationContract.HistoryPath], report.GetProperty("HistorySha256").GetString());
        Assert.Equal(target["game_state/meta/soul_state.json"], report.GetProperty("SoulSha256").GetString());
        var generation = after ? report.GetProperty("GenerationAfter").GetString() : expected.GenerationBefore;
        Assert.Equal(generation, report.GetProperty("GenerationBytes").GetString());
        Assert.Equal(generation, Convert.ToBase64String(File.ReadAllBytes(Files.SessionGenerationPath)));
        if (report.TryGetProperty("GenerationBefore", out var before)) Assert.Equal(expected.GenerationBefore, before.GetString());
        if (after)
        {
            using var generated = JsonDocument.Parse(Convert.FromBase64String(generation!));
            Assert.Equal(report.GetProperty("GenerationAfterId").GetString(), generated.RootElement.GetProperty("GenerationId").GetString());
        }
        if (report.GetProperty("Phase").GetString() is "TypedLoadCommitted" or "CanonicalAcquisitionReturned")
            Assert.Equal(after ? report.GetProperty("GenerationAfterId").GetString() : IndependentBeforeGenerationId(expected),
                report.GetProperty("EstablishedGeneration").GetString());
    }

    /// <summary>
    /// Reads the independent fixture's exact UTF16-BOM Before document without using production generation parsing.
    /// </summary>
    /// <param name="expected">
    /// Contains exact authored old generation bytes or explicit absence.
    /// </param>
    /// <returns>
    /// The original mixed-case document's generation ID, or <see langword="null"/> for explicit absence.
    /// </returns>
    private static string? IndependentBeforeGenerationId(PortableLoadFixtureState expected)
    {
        if (expected.GenerationBefore == null) return null;
        var bytes = Convert.FromBase64String(expected.GenerationBefore);
        Assert.True(bytes.AsSpan(0, 2).SequenceEqual(new byte[] { 0xFF, 0xFE }));
        using var document = JsonDocument.Parse(Encoding.Unicode.GetString(bytes, 2, bytes.Length - 2));
        return document.RootElement.EnumerateObject().Single(property =>
            property.Name.Equals("generationId", StringComparison.OrdinalIgnoreCase)).Value.GetString();
    }

    /// <summary>
    /// Removes only the exact reported extraction after its waiting child has been terminated and reparse checks pass.
    /// </summary>
    /// <param name="report">
    /// The actual terminated typed-load cut report.
    /// </param>
    private void DeleteExactExtraction(JsonElement report)
    {
        var path = report.GetProperty("ExtractionRoot").GetString()!;
        AssertOwnedPath(path);
        var staging = Path.GetFullPath(Path.Combine(Files.RuntimeRootPath, "load-staging")) + Path.DirectorySeparatorChar;
        Assert.StartsWith(staging, Path.GetFullPath(path),
            OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal);
        for (var current = path; Path.GetFullPath(current) != Path.GetFullPath(_root);
             current = Path.GetDirectoryName(current) ?? throw new InvalidDataException("Extraction lacks owned root ancestor."))
        {
            AssertOwnedPath(current);
            Assert.False(File.GetAttributes(current).HasFlag(FileAttributes.ReparsePoint));
        }
        // This cold source is private fixture-created ordinary extraction; inspect types without rehashing bulk payloads.
        foreach (var entry in Directory.EnumerateFileSystemEntries(path, "*", SearchOption.AllDirectories))
            Assert.False(File.GetAttributes(entry).HasFlag(FileAttributes.ReparsePoint));
        Directory.Delete(path, recursive: true);
        Assert.False(Directory.Exists(path));
        Assert.Empty(Directory.EnumerateFileSystemEntries(staging.TrimEnd(Path.DirectorySeparatorChar)));
    }

    /// <summary>
    /// Starts one exact-heap child, samples its owned envelope, and treats every guard/setup/cleanup failure as a test failure.
    /// </summary>
    /// <param name="operation">
    /// Seed, preparation, publication, cut or journal-only recovery.
    /// </param>
    /// <param name="workload">
    /// The exact declared workload for this isolated root.
    /// </param>
    /// <param name="terminateAtCut">
    /// Requires an actual live CutReady announcement and kills only that owned process when <see langword="true"/>.
    /// </param>
    /// <returns>
    /// The verified child report after its process has exited, with independent parent measurements retained separately.
    /// </returns>
    private async Task<JsonElement> RunChildAsync(string operation, string workload, bool terminateAtCut = false)
    {
        var assembly = typeof(PortableLoadResourceTests).Assembly.Location;
        var start = new ProcessStartInfo(Environment.GetEnvironmentVariable("DOTNET_HOST_PATH") ?? "dotnet")
        { UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true, RedirectStandardInput = true };
        foreach (var argument in new[] { "exec", "--runtimeconfig", Path.ChangeExtension(assembly, ".runtimeconfig.json"),
            "--depsfile", Path.ChangeExtension(assembly, ".deps.json"), typeof(PortableStorageCrashHost.Program).Assembly.Location,
            _root, HeapBytes.ToString(), "load-resource", operation + "|" + workload }) start.ArgumentList.Add(argument);
        start.Environment["DOTNET_GCHeapHardLimit"] = "0x30000000";
        using var process = Process.Start(start) ?? throw new InvalidOperationException("Owned load resource child did not start.");
        var line = process.StandardOutput.ReadLineAsync();
        var stderr = process.StandardError.ReadToEndAsync();
        var timer = Stopwatch.StartNew();
        long rssPeak = 0, diskPeak = 0;
        long phaseRssPeak = 0, phaseDiskPeak = 0;
        var phaseStarted = false;
        var phaseStopped = false;
        var phaseSamples = 0;
        var samples = 0;
        try
        {
            JsonElement report;
            string? text;
            while (true)
            {
                while (!line.IsCompleted)
                {
                    SampleChild(process, timer, ref rssPeak, ref diskPeak, phaseStarted && !phaseStopped, ref phaseRssPeak, ref phaseDiskPeak, ref phaseSamples); samples++;
                    await Task.Delay(100);
                }
                text = await line;
                Assert.False(string.IsNullOrEmpty(text), "Owned resource child returned without its complete phase report.");
                using var document = JsonDocument.Parse(text!);
                Assert.Equal(HeapBytes, document.RootElement.GetProperty("HeapBytes").GetInt64());
                if (document.RootElement.GetProperty("Phase").GetString() == "MeasurementStarted")
                {
                    Assert.False(phaseStarted, "The child announced its measured starting boundary twice.");
                    Assert.Equal(operation, document.RootElement.GetProperty("Operation").GetString());
                    phaseStarted = true;
                    SampleChild(process, timer, ref rssPeak, ref diskPeak, measuring: true, ref phaseRssPeak, ref phaseDiskPeak, ref phaseSamples); samples++;
                    await process.StandardInput.WriteLineAsync("measurement-start");
                    await process.StandardInput.FlushAsync();
                    line = process.StandardOutput.ReadLineAsync();
                    continue;
                }
                if (document.RootElement.GetProperty("Phase").GetString() == "MeasurementStopped")
                {
                    Assert.True(phaseStarted && !phaseStopped, "Actual measurement stop has no unique matching start.");
                    Assert.Equal(operation, document.RootElement.GetProperty("Operation").GetString());
                    SampleChild(process, timer, ref rssPeak, ref diskPeak, measuring: true, ref phaseRssPeak, ref phaseDiskPeak, ref phaseSamples); samples++;
                    phaseStopped = true;
                    await process.StandardInput.WriteLineAsync("measurement-stop");
                    await process.StandardInput.FlushAsync();
                    line = process.StandardOutput.ReadLineAsync();
                    continue;
                }
                report = document.RootElement.Clone();
                break;
            }
            Assert.True(report.GetProperty("Completed").GetBoolean(), text);
            Assert.Equal(HeapBytes, report.GetProperty("HeapBytes").GetInt64());
            if (terminateAtCut)
            {
                Assert.Equal("CutReady", report.GetProperty("Phase").GetString());
                Assert.False(process.HasExited, "The actual cut must be waiting for parent termination.");
                SampleChild(process, timer, ref rssPeak, ref diskPeak, phaseStarted && !phaseStopped, ref phaseRssPeak, ref phaseDiskPeak, ref phaseSamples); samples++;
                process.Kill(entireProcessTree: true);
            }
            while (!process.HasExited)
            {
                SampleChild(process, timer, ref rssPeak, ref diskPeak, phaseStarted && !phaseStopped, ref phaseRssPeak, ref phaseDiskPeak, ref phaseSamples); samples++;
                await Task.Delay(100);
            }
            await process.WaitForExitAsync();
            var remainder = await process.StandardOutput.ReadToEndAsync();
            var error = await stderr;
            Assert.True(string.IsNullOrWhiteSpace(remainder), text + remainder + error);
            if (!terminateAtCut) Assert.True(process.ExitCode == 0, text + error);
            Assert.True(samples > 0 && rssPeak > 0, "Missing owned-process sampling is not qualification.");
            Assert.True(phaseStarted && phaseStopped && phaseSamples > 0 && phaseRssPeak > 0 && phaseDiskPeak > 0,
                "Missing actual phase sampling is not qualification.");
            diskPeak = Math.Max(diskPeak, MeasureOwnedDisk(allowPublicationRace: false));
            Assert.InRange(diskPeak, 1, DiskStop);
            Assert.InRange(rssPeak, 1, RssStop);
            Assert.True(timer.Elapsed <= TimeSpan.FromSeconds(180), "Child exceeded its fixed 180-second bound.");
            var parent = new
            {
                Operation = operation, Workload = workload, ParentTerminated = terminateAtCut,
                SampleCount = samples, SampledPeakWorkingSetBytes = rssPeak, OwnedDiskPeakBytes = diskPeak,
                PhaseSampleCount = phaseSamples, SampledPhasePeakWorkingSetBytes = phaseRssPeak, OwnedPhaseDiskPeakBytes = phaseDiskPeak,
                ParentMilliseconds = timer.Elapsed.TotalMilliseconds, OwnedProcessExited = process.HasExited
            };
            File.WriteAllText(Path.Combine(_root, $"resource-parent-{operation}-{workload}.json"), JsonSerializer.Serialize(parent));
            output.WriteLine("Child={0}; parent={1}; stderr={2}", text, JsonSerializer.Serialize(parent), error);
            return report;
        }
        finally
        {
            if (!process.HasExited) { process.Kill(entireProcessTree: true); await process.WaitForExitAsync(); }
        }
    }

    /// <summary>
    /// Samples actual RSS and owned disk, enforcing fixed process bounds without converting a stop into a pass.
    /// </summary>
    /// <param name="process">
    /// The exact child process started by this case.
    /// </param>
    /// <param name="timer">
    /// Time since this child started.
    /// </param>
    /// <param name="rssPeak">
    /// Maximum observed child RSS, updated in place.
    /// </param>
    /// <param name="diskPeak">
    /// Maximum observed owned-root file bytes, updated in place.
    /// </param>
    /// <param name="measuring">
    /// True only between actual matching start/stop announcements, excluding setup and post-phase verification.
    /// </param>
    /// <param name="phaseRssPeak">
    /// Maximum actual phase RSS, excluding earlier setup samples.
    /// </param>
    /// <param name="phaseDiskPeak">
    /// Maximum actual phase owned disk, excluding earlier setup samples.
    /// </param>
    /// <param name="phaseSamples">
    /// The actual phase's independent sampling count.
    /// </param>
    private void SampleChild(Process process, Stopwatch timer, ref long rssPeak, ref long diskPeak,
        bool measuring, ref long phaseRssPeak, ref long phaseDiskPeak, ref int phaseSamples)
    {
        long rss = 0;
        if (!process.HasExited)
        {
            try { process.Refresh(); if (!process.HasExited) rss = process.WorkingSet64; }
            catch (InvalidOperationException) when (process.HasExited) { }
        }
        var disk = MeasureOwnedDisk(allowPublicationRace: !process.HasExited);
        rssPeak = Math.Max(rssPeak, rss); diskPeak = Math.Max(diskPeak, disk);
        if (measuring)
        {
            phaseSamples++; phaseRssPeak = Math.Max(phaseRssPeak, rss); phaseDiskPeak = Math.Max(phaseDiskPeak, disk);
        }
        if (rssPeak > RssStop || diskPeak > DiskStop || timer.Elapsed > TimeSpan.FromSeconds(180))
            throw new InvalidOperationException($"Owned load-resource guard stop: RSS={rssPeak}; disk={diskPeak}; elapsed={timer.Elapsed}.");
    }

    /// <summary>
    /// Measures only this root's ordinary file bytes, tolerating a vanished exact enumerated file only while its child mutates.
    /// </summary>
    /// <param name="allowPublicationRace">
    /// Permits FileNotFound for a just-enumerated owned ordinary file during active publication/recovery; <see langword="false"/> requires a stable root.
    /// </param>
    /// <returns>
    /// The sampled total; links and all other access or I/O failures propagate as non-passes.
    /// </returns>
    private long MeasureOwnedDisk(bool allowPublicationRace)
    {
        if (!Directory.Exists(_root)) return 0;
        long count = 0;
        var files = Files;
        var pending = new Stack<DirectoryInfo>(); pending.Push(new DirectoryInfo(_root));
        while (pending.TryPop(out var directory))
        {
            var transient = allowPublicationRace && IsExpectedDirectoryMutation(directory.FullName, files);
            if (transient && !Directory.Exists(directory.FullName)) continue;
            FileSystemInfo[] entries;
            try
            {
                entries = directory.EnumerateFileSystemInfos("*", SearchOption.TopDirectoryOnly).ToArray();
            }
            catch (DirectoryNotFoundException) when (transient && !Directory.Exists(directory.FullName))
            {
                // Only a declared convertible or owned private-staging directory disappeared or became a file.
                continue;
            }
            foreach (var entry in entries)
            {
                AssertOwnedPath(entry.FullName);
                try
                {
                    if (entry.Attributes.HasFlag(FileAttributes.ReparsePoint)) throw new InvalidDataException("Owned disk sampling encountered a link.");
                    if (entry is DirectoryInfo child) pending.Push(child);
                    else count = checked(count + ((FileInfo)entry).Length);
                }
                catch (FileNotFoundException) when (allowPublicationRace && !File.Exists(entry.FullName))
                {
                    // Only this exact already-enumerated fixture-owned regular file disappeared during its operation.
                }
            }
        }
        return count;
    }

    /// <summary>
    /// Restricts directory disappearance tolerance to the fixture's declared conversions and owned private staging.
    /// </summary>
    /// <param name="path">
    /// An exact ordinary directory already enumerated under the owned root.
    /// </param>
    /// <param name="files">
    /// Resolves the current fixture's canonical conversion and private-runtime paths.
    /// </param>
    /// <returns>
    /// True only at/below a declared conversion or owned load/save staging directory; all other paths return <see langword="false"/>.
    /// </returns>
    private bool IsExpectedDirectoryMutation(string path, FileSystemManager files)
    {
        AssertOwnedPath(path);
        var comparison = OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
        foreach (var root in new[] { files.ResolvePath(PortableLoadFixture.FileToDirectory), files.ResolvePath(PortableLoadFixture.DirectoryToFile),
                     Path.Combine(files.RuntimeRootPath, "load-staging"), Path.Combine(files.RuntimeRootPath, "save-staging") })
            if (path.Equals(root, comparison) || path.StartsWith(root + Path.DirectorySeparatorChar, comparison)) return true;
        return false;
    }

    /// <summary>
    /// Requires complete removal of authoritative evidence and all owned private/member staging after a phase completes.
    /// </summary>
    private void AssertOwnedScratchEmpty()
    {
        foreach (var name in new[] { "trusted-local-publication-v1", "load-staging", "load-transactions", "save-staging" })
        {
            var path = Path.Combine(Files.RuntimeRootPath, name);
            if (Directory.Exists(path)) Assert.Empty(Directory.EnumerateFileSystemEntries(path));
        }
        Assert.Empty(Directory.EnumerateFiles(Files.GameSessionPath, ".boe-local-*", SearchOption.AllDirectories));
        Assert.Empty(Directory.EnumerateFiles(Path.GetDirectoryName(Files.SessionGenerationPath)!, ".boe-local-*"));
    }

    /// <summary>
    /// Verifies resolved mutation or measurement paths are beneath this case's independently owned root.
    /// </summary>
    /// <param name="path">
    /// The exact absolute fixture or child-announced path.
    /// </param>
    private void AssertOwnedPath(string path) => Assert.StartsWith(
        Path.GetFullPath(_root) + Path.DirectorySeparatorChar, Path.GetFullPath(path),
        OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal);

    /// <summary>
    /// Deletes only this isolated owned root after all sequential child runners have stopped their exact processes.
    /// </summary>
    public void Dispose()
    {
        var resolved = Path.GetFullPath(_root);
        Assert.Equal(Path.GetFullPath(Path.Combine(Path.GetTempPath(), Path.GetFileName(_root))), resolved);
        Assert.StartsWith("boe-load-resource-", Path.GetFileName(resolved), StringComparison.Ordinal);
        Assert.True(Guid.TryParseExact(Path.GetFileName(resolved)["boe-load-resource-".Length..], "N", out _));
        if (Directory.Exists(resolved))
        {
            Assert.False(File.GetAttributes(resolved).HasFlag(FileAttributes.ReparsePoint));
            Directory.Delete(resolved, recursive: true);
        }
    }
}
