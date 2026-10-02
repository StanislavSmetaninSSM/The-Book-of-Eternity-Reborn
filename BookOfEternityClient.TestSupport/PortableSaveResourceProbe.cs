using System.Diagnostics;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text.Json;
using BookOfEternityClient.Core;
using BookOfEternityClient.Services;
using Microsoft.Extensions.Logging.Abstractions;

namespace BookOfEternityClient.Tests;

/// <summary>Exploratory T032-A resource fixture, never a production save-size or RAM policy.</summary>
public static class PortableSaveResourceProbe
{
    private sealed class StopAfterArchive : Exception { }
    private sealed class RetainCommittedEvidence : Exception { }

    public static async Task<int> RunAsync(string[] args)
    {
        if (args.Length != 4 || args[2] != "save-resource") return 64;
        var root = args[0];
        var mode = args[3];
        var bytes = long.Parse(args[1]);
        if (bytes is <= 0 or > 512L * 1024 * 1024) return 64;
        if (GC.GetGCMemoryInfo().TotalAvailableMemoryBytes != 768L * 1024 * 1024)
            throw new InvalidOperationException("The owned probe did not inherit the exact declared managed-heap bound.");
        var report = new Dictionary<string, object?>
        {
            ["mode"] = mode, ["requestedPayloadBytes"] = bytes,
            ["gcAvailableBytes"] = GC.GetGCMemoryInfo().TotalAvailableMemoryBytes,
            ["phases"] = new List<string>(), ["completed"] = false
        };
        var phases = (List<string>)report["phases"]!;
        var files = new FileSystemManager(root, NullLogger<FileSystemManager>.Instance);
        var candidate = Path.Combine(root, "candidate.zip");
        var destination = Path.Combine(root, "game_session/saves/manual_saves/resource-candidate.zip");
        var timer = Stopwatch.StartNew();
        var allocated = GC.GetTotalAllocatedBytes(true);
        try
        {
            if (mode == "producer")
            {
                if (!OperatingSystem.IsLinux()) throw new InvalidOperationException("This unchanged-producer stop is qualified on Linux only.");
                var state = PortableSaveFixture.Seed(files);
                WritePayloads(files, bytes);
                // Seed three independent library members; this probe never interprets or deletes them.
                foreach (var dir in new[] { "manual_saves", "autosaves", "checkpoint_saves" })
                    File.WriteAllBytes(files.ResolvePath($"saves/{dir}/existing.zip"), [7, 0, 255]);
                GC.Collect(); GC.WaitForPendingFinalizers(); GC.Collect();
                allocated = GC.GetTotalAllocatedBytes(true); timer.Restart();
                var logger = new PortableSaveFixture.CaptureLogger();
                var service = new SaveLoadService(files, state, logger, new SaveLoadServiceHooks
                {
                    BeforeSaveCommitAsync = () => { phases.Add("CompletedArchiveBoundary"); throw new StopAfterArchive(); }
                });
                var saved = await service.SaveGameAsync("resource-producer", "bounded producer measurement");
                report["saveReturned"] = saved;
                report["producerMilliseconds"] = timer.Elapsed.TotalMilliseconds;
                report["producerAllocatedBytes"] = GC.GetTotalAllocatedBytes(true) - allocated;
                report["errors"] = logger.Errors.Select(ex => ex.GetType().FullName).ToArray();
                if (saved || !phases.Contains("CompletedArchiveBoundary"))
                    throw new InvalidOperationException("The unchanged producer did not reach the explicit final-publication cut.");
                // SaveGame finally closed the stream. The known Linux physical cleanup failure retains it.
                // This move belongs only to the isolated test fixture, never to product recovery authority.
                var staged = Directory.GetFiles(Path.Combine(files.RuntimeRootPath, "save-staging"), "save.zip", SearchOption.AllDirectories).Single();
                File.Move(staged, candidate);
                report["archiveBytes"] = new FileInfo(candidate).Length;
                report["archiveSha256"] = Hash(candidate);
                using (var archive = ZipFile.OpenRead(candidate))
                {
                    var descriptors = archive.Entries.Select(e => new SaveLoadService.SaveArchiveEntryDescriptor(
                        e.FullName, e.FullName.EndsWith('/'), e.Length, e.CompressedLength)).ToArray();
                    SaveLoadService.ValidateTrustedArchiveBudget(descriptors);
                    report["archiveEntryCount"] = descriptors.Length;
                    report["expandedBytes"] = descriptors.Sum(e => e.Length);
                }
                Directory.Delete(Path.Combine(files.RuntimeRootPath, "save-staging"), recursive: true);
                report["completed"] = true;
            }
            else if (mode == "b1")
            {
                files = new FileSystemManager(root, NullLogger<FileSystemManager>.Instance,
                    PhysicalLoadTransactionOperations.Instance, new FileSystemManagerHooks
                    {
                        LocalPublicationObserver = (phase, _) =>
                        {
                            phases.Add(phase.ToString());
                            if (phase == TrustedLocalPublicationPhase.Committed) throw new RetainCommittedEvidence();
                        }
                    });
                await using var lease = await files.AcquireCanonicalWriteLeaseAsync();
                phases.Add("BeforeWholeArchiveRead");
                var image = File.ReadAllBytes(candidate);
                phases.Add("WholeArchiveRead");
                var outcome = await files.PublishLocalFilesAsync(lease,
                    [new CanonicalLocalFileChange("saves/manual_saves/resource-candidate.zip", null, image)]);
                report["disposition"] = outcome.Disposition.ToString();
                report["failure"] = outcome.Failure?.ToString();
                report["completed"] = outcome.Disposition == TrustedLocalPublicationDisposition.Committed;
            }
            else if (mode == "recover")
            {
                phases.Add("BeforeColdCanonicalAcquisition");
                await using var lease = await files.AcquireCanonicalWriteLeaseAsync();
                phases.Add("ColdCanonicalAcquisitionReturned");
                report["completed"] = true;
            }
            else return 64;
        }
        catch (Exception ex)
        {
            report["failure"] = ex.ToString();
            report["failureType"] = ex.GetType().FullName;
        }
        report["milliseconds"] = timer.Elapsed.TotalMilliseconds;
        report["allocatedBytes"] = GC.GetTotalAllocatedBytes(true) - allocated;
        report["peakWorkingSetBytes"] = Process.GetCurrentProcess().PeakWorkingSet64;
        report["destinationExists"] = File.Exists(destination);
        report["destinationSha256"] = File.Exists(destination) ? Hash(destination) : null;
        report["journalFiles"] = Directory.Exists(Path.Combine(files.RuntimeRootPath, "trusted-local-publication-v1"))
            ? Directory.GetFiles(Path.Combine(files.RuntimeRootPath, "trusted-local-publication-v1")).Select(Path.GetFileName).Order().ToArray() : [];
        File.WriteAllText(Path.Combine(root, mode + "-report.json"), JsonSerializer.Serialize(report));
        return 0; // Measurement complete, including explicitly recorded bounded failures; not a product pass.
    }

    private static void WritePayloads(FileSystemManager files, long count)
    {
        var chunk = new byte[64 * 1024];
        var random = new Random(1553);
        for (var index = 0; count > 0; index++)
        {
            var size = Math.Min(count, 64L * 1024 * 1024);
            using var stream = File.Create(files.ResolvePath($"lore/current_world/resource-{index}.bin"));
            for (long written = 0; written < size;)
            {
                random.NextBytes(chunk);
                var take = (int)Math.Min(chunk.Length, size - written);
                stream.Write(chunk, 0, take); written += take;
            }
            count -= size;
        }
    }

    private static string Hash(string path)
    {
        using var stream = File.OpenRead(path);
        return Convert.ToHexString(SHA256.HashData(stream));
    }
}
