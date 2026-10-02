using System.Buffers.Binary;
using System.Diagnostics;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text.Json;
using BookOfEternityClient.Core;
using BookOfEternityClient.Services;
using Microsoft.Extensions.Logging.Abstractions;

namespace BookOfEternityClient.Tests;

/// <summary>
/// Runs isolated producer, image-publication and recovery resource probes with the ordinary save caller.
/// </summary>
public static class TrustedLocalStreamResourceProbe
{
    /// <summary>
    /// Deliberately retains committed cleanup debt for a separate ordinary canonical acquisition.
    /// </summary>
    private sealed class RetainCommittedEvidence : Exception { }

    /// <summary>
    /// Executes exactly one bounded producer, publication or cold recovery scenario.
    /// </summary>
    /// <param name="args">
    /// The owned root, filler byte count, stream-resource dispatch name and scenario mode.
    /// </param>
    /// <returns>
    /// Zero on ordinary success, 73 at the deliberate pending publication cut, or 90 on a recorded failure.
    /// </returns>
    public static async Task<int> RunAsync(string[] args)
    {
        if (args.Length != 4 || args[2] != "stream-resource" || !long.TryParse(args[1], out var bytes) ||
            bytes is <= 0 or > 512L * 1024 * 1024) return 64;
        var available = GC.GetGCMemoryInfo().TotalAvailableMemoryBytes;
        if (available != 768L * 1024 * 1024)
            throw new InvalidOperationException("The resource child did not inherit the exact original managed heap bound.");
        var root = args[0];
        var mode = args[3];
        var report = new Dictionary<string, object?>
        {
            ["mode"] = mode, ["platform"] = OperatingSystem.IsWindows() ? "Windows" : OperatingSystem.IsLinux() ? "Linux" : "Unsupported",
            ["requestedPayloadBytes"] = bytes, ["gcAvailableBytes"] = available,
            ["phases"] = new List<string>(), ["completed"] = false
        };
        var phases = (List<string>)report["phases"]!;
        var files = new FileSystemManager(root, NullLogger<FileSystemManager>.Instance);
        var candidate = Path.Combine(root, "candidate.zip");
        var destination = files.ResolvePath("saves/manual_saves/resource-candidate.zip");
        var active = Path.Combine(files.RuntimeRootPath, "trusted-local-publication-v1/active.json");
        var timer = Stopwatch.StartNew();
        var allocated = GC.GetTotalAllocatedBytes(true);
        var exit = 0;
        try
        {
            if (!OperatingSystem.IsWindows() && !OperatingSystem.IsLinux())
                throw new PlatformNotSupportedException("The resource probe supports only the declared Windows and Linux paths.");
            if (mode == "producer")
            {
                var state = PortableSaveFixture.Seed(files);
                WritePayloads(files, bytes);
                foreach (var dir in new[] { "manual_saves", "autosaves", "checkpoint_saves" })
                    File.WriteAllBytes(files.ResolvePath($"saves/{dir}/existing.zip"), [7, 0, 255]);
                var libraryBefore = Directory.GetFiles(files.ResolvePath("saves"), "*", SearchOption.AllDirectories).ToHashSet(StringComparer.Ordinal);
                GC.Collect(); GC.WaitForPendingFinalizers(); GC.Collect();
                allocated = GC.GetTotalAllocatedBytes(true); timer.Restart();
                var logger = new PortableSaveFixture.CaptureLogger();
                var service = new SaveLoadService(files, state, logger, new SaveLoadServiceHooks
                {
                    BeforeSaveCommitAsync = () =>
                    {
                        phases.Add("CompletedArchiveBoundary");
                        report["producerBoundaryMilliseconds"] = timer.Elapsed.TotalMilliseconds;
                        report["producerBoundaryAllocatedBytes"] = GC.GetTotalAllocatedBytes(true) - allocated;
                        return Task.CompletedTask;
                    }
                });
                var saved = await service.SaveGameAsync("stream-resource-producer", "bounded actual archive producer");
                report["saveReturned"] = saved;
                report["producerAllocatedBytes"] = GC.GetTotalAllocatedBytes(true) - allocated;
                report["producerMilliseconds"] = timer.Elapsed.TotalMilliseconds;
                report["producerErrors"] = logger.Errors.Select(failure => failure.GetType().FullName).ToArray();
                report["producerRoute"] = "ordinary-create-only-image-publication";
                if (!phases.Contains("CompletedArchiveBoundary") || !saved)
                    throw new InvalidOperationException("The actual producer did not reach its declared platform boundary.");
                // Both platforms execute the real public save path. Native Linux execution remains pending.
                // Only this newly created fixture member moves; the established library stays intact.
                var produced = Directory.GetFiles(files.ResolvePath("saves"), "*", SearchOption.AllDirectories)
                    .Where(path => !libraryBefore.Contains(path)).Single();
                File.Move(produced, candidate);
                foreach (var path in Directory.GetFiles(files.ResolvePath("lore/current_world"), "resource-*.bin")) File.Delete(path);
                var saveStaging = Path.Combine(files.RuntimeRootPath, "save-staging");
                if (Directory.Exists(saveStaging))
                {
                    if (Directory.EnumerateFileSystemEntries(saveStaging).Any())
                        throw new InvalidDataException("Ordinary save left private staging evidence after success.");
                    Directory.Delete(saveStaging, recursive: false);
                }
                var inspectionStart = GC.GetTotalAllocatedBytes(true);
                var descriptors = ValidateArchive(candidate);
                report["archiveInspectionAllocatedBytes"] = GC.GetTotalAllocatedBytes(true) - inspectionStart;
                report["archiveBytes"] = new FileInfo(candidate).Length;
                report["archiveSha256"] = Hash(candidate);
                report["archiveEntryCount"] = descriptors.Count;
                report["expandedBytes"] = descriptors.Sum(entry => entry.Length);
                report["generationDocumentBytes"] = new FileInfo(files.SessionGenerationPath).Length;
                report["completed"] = true;
            }
            else if (mode is "publish-pending" or "publish-committed")
            {
                var publicationAllocated = 0L;
                var publicationTimer = new Stopwatch();
                files = new FileSystemManager(root, NullLogger<FileSystemManager>.Instance,
                    PhysicalLoadTransactionOperations.Instance, new FileSystemManagerHooks
                    {
                        LocalPublicationObserver = (phase, index) =>
                        {
                            phases.Add(phase.ToString());
                            if (phase == TrustedLocalPublicationPhase.IntentPublished) File.Delete(candidate);
                            if (mode == "publish-pending" && phase == TrustedLocalPublicationPhase.MemberPublished && index == 0)
                            {
                                report["cut"] = phase.ToString(); report["cutIndex"] = index;
                                report["publicationAllocatedBytes"] = GC.GetTotalAllocatedBytes(true) - publicationAllocated;
                                report["publicationMilliseconds"] = publicationTimer.Elapsed.TotalMilliseconds;
                                DescribeFrame(active, report);
                                WriteReport(root, mode, report, timer, allocated);
                                Console.Out.Flush(); Environment.Exit(73);
                            }
                            if (mode == "publish-committed" && phase == TrustedLocalPublicationPhase.Committed)
                                throw new RetainCommittedEvidence();
                        }
                    });
                await using var lease = await files.AcquireCanonicalWriteLeaseAsync();
                var generation = Measure(report, "generationSnapshot", () => files.ReadLocalGenerationSnapshot(lease));
                report["generationDocumentBytes"] = generation.Bytes?.LongLength ?? 0;
                report["generationId"] = generation.Binding.Id;
                var scope = new TrustedLocalFileScope([root]);
                var before = Measure(report, "beforeCapture", () => TrustedLocalFileImage.CaptureFile(scope, destination));
                var after = Measure(report, "afterCapture", () => TrustedLocalFileImage.CaptureFile(scope, candidate));
                report["beforeFileBacked"] = before.IsFileBacked; report["afterFileBacked"] = after.IsFileBacked;
                report["beforeBytes"] = before.Length; report["afterBytes"] = after.Length;
                report["beforeSha256"] = before.Sha256; report["afterSha256"] = after.Sha256;
                Measure(report, "imageCopyHash", () => { before.CopyTo(Stream.Null); after.CopyTo(Stream.Null); return true; });
                var matches = Measure(report, "imageMatchesHash", () => before.MatchesFile(scope, destination) && after.MatchesFile(scope, candidate));
                if (!matches) throw new InvalidDataException("Captured full archive images did not match their source files.");
                report["imageMatches"] = matches;
                publicationAllocated = GC.GetTotalAllocatedBytes(true); publicationTimer.Start();
                var outcome = await files.PublishLocalImageFilesAsync(lease,
                    [new CanonicalLocalImageChange("saves/manual_saves/resource-candidate.zip", before, after)]);
                report["publicationAllocatedBytes"] = GC.GetTotalAllocatedBytes(true) - publicationAllocated;
                report["publicationMilliseconds"] = publicationTimer.Elapsed.TotalMilliseconds;
                report["disposition"] = outcome.Disposition.ToString();
                report["cleanupFailureType"] = outcome.Failure?.GetType().Name;
                report["failure"] = outcome.Failure?.ToString();
                if (mode != "publish-committed" || outcome.Disposition != TrustedLocalPublicationDisposition.Committed ||
                    outcome.Failure is not RetainCommittedEvidence)
                    throw new InvalidOperationException("The image adapter did not retain the deliberate committed decision.");
                DescribeFrame(active, report);
                report["completed"] = true;
            }
            else if (mode is "recover-pending" or "recover-committed")
            {
                DescribeFrame(active, report);
                var coldAllocated = GC.GetTotalAllocatedBytes(true);
                var coldTimer = Stopwatch.StartNew();
                phases.Add("BeforeColdCanonicalAcquisition");
                await using var lease = await files.AcquireCanonicalWriteLeaseAsync();
                report["coldAcquisitionAllocatedBytes"] = GC.GetTotalAllocatedBytes(true) - coldAllocated;
                report["coldAcquisitionMilliseconds"] = coldTimer.Elapsed.TotalMilliseconds;
                phases.Add("ColdCanonicalAcquisitionReturned");
                var generation = Measure(report, "generationSnapshot", () => files.ReadLocalGenerationSnapshot(lease));
                report["generationDocumentBytes"] = generation.Bytes?.LongLength ?? 0;
                report["generationId"] = generation.Binding.Id;
                var scope = new TrustedLocalFileScope([root]);
                var recovered = Measure(report, "recoveredCapture", () => TrustedLocalFileImage.CaptureFile(scope, destination));
                report["recoveredBytes"] = recovered.Length; report["recoveredSha256"] = recovered.Sha256;
                if (!Measure(report, "recoveredMatchesHash", () => recovered.MatchesFile(scope, destination)))
                    throw new InvalidDataException("The recovered complete archive did not match its captured image.");
                report["completed"] = true;
            }
            else return 64;
        }
        catch (Exception failure)
        {
            report["failure"] = failure.ToString(); report["failureType"] = failure.GetType().Name; exit = 90;
        }
        WriteReport(root, mode, report, timer, allocated);
        return exit;
    }

    /// <summary>
    /// Checks the actual reader budget over a complete closed archive's entry descriptors.
    /// </summary>
    /// <param name="path">
    /// The existing closed producer ZIP or its valid-comment before-image.
    /// </param>
    /// <returns>
    /// All descriptors after the production budget validator accepts them.
    /// </returns>
    internal static IReadOnlyList<SaveLoadService.SaveArchiveEntryDescriptor> ValidateArchive(string path)
    {
        using var archive = ZipFile.OpenRead(path);
        var descriptors = archive.Entries.Select(entry => new SaveLoadService.SaveArchiveEntryDescriptor(
            entry.FullName, entry.FullName.EndsWith('/'), entry.Length, entry.CompressedLength)).ToArray();
        SaveLoadService.ValidateTrustedArchiveBudget(descriptors);
        return descriptors;
    }

    /// <summary>
    /// Records a bounded header-only diagnostic allocation separately from bulk publication and recovery.
    /// </summary>
    /// <param name="path">
    /// The current authoritative framed journal path.
    /// </param>
    /// <param name="report">
    /// Receives header length, payload length, committed decision and diagnostic allocation.
    /// </param>
    private static void DescribeFrame(string path, Dictionary<string, object?> report)
    {
        Measure(report, "framedMetadataDiagnostic", () =>
        {
            using var stream = File.OpenRead(path);
            var prefix = new byte[16]; stream.ReadExactly(prefix);
            if (!prefix.AsSpan(0, 8).SequenceEqual("BOELP2\r\n"u8)) throw new InvalidDataException("The resource workload did not use v2 framing.");
            var headerLength = BinaryPrimitives.ReadInt64LittleEndian(prefix.AsSpan(8));
            if (headerLength is <= 0 or > 1024 * 1024 || headerLength > stream.Length - 16)
                throw new InvalidDataException("The resource journal metadata length is invalid.");
            var header = new byte[(int)headerLength]; stream.ReadExactly(header);
            using var parsed = JsonDocument.Parse(header);
            report["framedMetadataBytes"] = headerLength;
            report["framedPayloadBytes"] = stream.Length - 16 - headerLength;
            report["journalBytes"] = stream.Length;
            report["journalCommitted"] = parsed.RootElement.GetProperty("Committed").GetBoolean();
            return true;
        });
    }

    /// <summary>
    /// Measures cumulative allocations and elapsed time for one real synchronous operation.
    /// </summary>
    /// <typeparam name="T">
    /// The operation's result type.
    /// </typeparam>
    /// <param name="report">
    /// Receives phase-specific allocation and timing values.
    /// </param>
    /// <param name="phase">
    /// The unambiguous measurement field prefix.
    /// </param>
    /// <param name="operation">
    /// The actual operation to execute once.
    /// </param>
    /// <returns>
    /// The operation's unchanged result.
    /// </returns>
    private static T Measure<T>(Dictionary<string, object?> report, string phase, Func<T> operation)
    {
        var allocated = GC.GetTotalAllocatedBytes(true); var timer = Stopwatch.StartNew();
        var result = operation();
        var delta = GC.GetTotalAllocatedBytes(true) - allocated;
        report[phase + "AllocatedBytes"] = delta; report[phase + "Milliseconds"] = timer.Elapsed.TotalMilliseconds;
        return result;
    }

    /// <summary>
    /// Writes incompressible filler in the same 64 MiB entry shape as the original resource experiment.
    /// </summary>
    /// <param name="files">
    /// Resolves the independently owned synthetic session.
    /// </param>
    /// <param name="count">
    /// The total requested filler bytes.
    /// </param>
    private static void WritePayloads(FileSystemManager files, long count)
    {
        var chunk = new byte[64 * 1024]; var random = new Random(1553);
        for (var index = 0; count > 0; index++)
        {
            var size = Math.Min(count, 64L * 1024 * 1024);
            using var stream = File.Create(files.ResolvePath($"lore/current_world/resource-{index}.bin"));
            for (long written = 0; written < size;)
            {
                random.NextBytes(chunk); var take = (int)Math.Min(chunk.Length, size - written);
                stream.Write(chunk, 0, take); written += take;
            }
            count -= size;
        }
    }

    /// <summary>
    /// Saves the child's complete report before ordinary return or the deliberate abrupt cut.
    /// </summary>
    /// <param name="root">
    /// The independently owned resource root.
    /// </param>
    /// <param name="mode">
    /// The scenario name used in its report filename.
    /// </param>
    /// <param name="report">
    /// The measured scenario and invariant fields.
    /// </param>
    /// <param name="timer">
    /// Measures the reported operation interval.
    /// </param>
    /// <param name="allocated">
    /// The cumulative allocation counter at the beginning of that interval.
    /// </param>
    private static void WriteReport(string root, string mode, Dictionary<string, object?> report, Stopwatch timer, long allocated)
    {
        report["milliseconds"] = timer.Elapsed.TotalMilliseconds;
        report["allocatedBytes"] = GC.GetTotalAllocatedBytes(true) - allocated;
        report["peakWorkingSetBytes"] = Process.GetCurrentProcess().PeakWorkingSet64;
        File.WriteAllText(Path.Combine(root, mode + "-report.json"), JsonSerializer.Serialize(report));
        Console.WriteLine(JsonSerializer.Serialize(report));
    }

    /// <summary>
    /// Hashes the complete file through a sequential stream.
    /// </summary>
    /// <param name="path">
    /// The existing file to hash.
    /// </param>
    /// <returns>
    /// The uppercase SHA-256 value.
    /// </returns>
    private static string Hash(string path) { using var stream = File.OpenRead(path); return Convert.ToHexString(SHA256.HashData(stream)); }
}
