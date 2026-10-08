using System.Buffers.Binary;
using System.Diagnostics;
using System.IO.Compression;
using System.Text;
using System.Text.Json;
using BookOfEternityClient.Configuration;
using BookOfEternityClient.Core;
using BookOfEternityClient.Services;
using Microsoft.Extensions.Logging.Abstractions;

namespace BookOfEternityClient.Tests;

/// <summary>
/// Qualifies actual current-producer load phases in isolated bounded processes without changing archive or save policies.
/// </summary>
public static class PortableLoadResourceProbe
{
    private const long MiB = 1024L * 1024;
    private const int BufferBytes = 64 * 1024;
    private const string IncomingRoot = "lore/resource-incoming";
    private const string OldRoot = "lore/resource-old";

    /// <summary>
    /// Marks the real prepared/lease boundary without entering canonical publication.
    /// </summary>
    private sealed class PreparationMeasured : Exception { }

    /// <summary>
    /// Executes separately measured seed, preparation, publication, cut or journal-only recovery for a declared workload.
    /// </summary>
    /// <param name="args">
    /// The owned root, exact declared heap bytes, load-resource mode and operation|workload scenario.
    /// Workloads are 64, 128, 512 and many; operations are seed, preparation, publication, cut and recovery.
    /// </param>
    /// <returns>
    /// Zero only for a fully verified operation, 90 for a recorded failure or 64 for unsupported arguments.
    /// Cut announces an actual waiting child that the owning test must terminate.
    /// </returns>
    public static async Task<int> RunAsync(string[] args)
    {
        if (args.Length != 4 || args[2] != "load-resource") return 64;
        var parts = args[3].Split('|');
        if (parts.Length != 2 || parts[0] is not ("seed" or "preparation" or "publication" or "cut" or "recovery") ||
            parts[1] is not ("64" or "128" or "512" or "many")) return 64;
        var heap = GC.GetGCMemoryInfo().TotalAvailableMemoryBytes;
        var report = new Dictionary<string, object?>
        {
            ["Operation"] = parts[0], ["Workload"] = parts[1], ["HeapBytes"] = heap, ["Completed"] = false,
            ["Os"] = System.Runtime.InteropServices.RuntimeInformation.OSDescription
        };
        var timer = Stopwatch.StartNew();
        var progressTimer = Stopwatch.StartNew();
        var allocated = GC.GetTotalAllocatedBytes(true);
        var measured = false;
        try
        {
            if (heap != long.Parse(args[1]) || heap != 768 * MiB) throw new InvalidOperationException("Resource child heap mismatch.");
            if (parts[0] == "seed")
            {
                AnnouncePhaseStarted(parts[0], heap);
                allocated = GC.GetTotalAllocatedBytes(true); timer.Restart();
                await SeedAsync(args[0], parts[1], report);
                FinishMeasurement(report, timer, allocated);
                report["Phase"] = "SeedCompleted";
                report["Completed"] = true;
                File.WriteAllText(Path.Combine(args[0], "resource-seed.json"), JsonSerializer.Serialize(report));
                WriteReport(report);
                return 0;
            }
            using (var seed = JsonDocument.Parse(File.ReadAllText(Path.Combine(args[0], "resource-seed.json"))))
                report["Seed"] = seed.RootElement.Clone();
            var expected = PortableLoadFixture.Read(args[0]);
            FileSystemManager? files = null;
            var committed = 0;
            var observed = 0;
            var mutationBoundaries = 0;
            var prepared = false;
            string? extraction = null;
            Action<TrustedLocalPublicationPhase, int> observe = (phase, index) =>
            {
                observed++;
                if (!prepared) return;
                if (index < 0 || index % 1000 == 0) WriteProgress(report, phase.ToString(), index, progressTimer.Elapsed.TotalMilliseconds);
                if (phase == TrustedLocalPublicationPhase.IntentPublished) DescribeFrame(files!, report);
                if (phase == TrustedLocalPublicationPhase.Committed) committed++;
                var cut = parts[0] == "cut" && (parts[1] == "many"
                    ? phase == TrustedLocalPublicationPhase.Committed
                    : phase == TrustedLocalPublicationPhase.MemberPublished && MemberIsGeneration(files!, index));
                if (phase == TrustedLocalPublicationPhase.Committed || cut)
                {
                    FinishMeasurement(report, timer, allocated); measured = true;
                    DescribeFrame(files!, report);
                }
                if (!cut) return;
                Verify(files!, expected, report, after: true);
                report["Phase"] = "CutReady";
                report["Cut"] = phase.ToString(); report["Index"] = index;
                report["MemberPath"] = index < 0 ? null : "@generation";
                report["CommittedObserverCount"] = committed;
                report["PublicationObserverCount"] = observed;
                report["PreparedBoundaryObserved"] = prepared;
                report["ExtractionRoot"] = extraction;
                report["ActivePath"] = Path.Combine(files!.RuntimeRootPath, "trusted-local-publication-v1", "active.json");
                report["Completed"] = true;
                WriteReport(report);
                Thread.Sleep(Timeout.Infinite);
            };
            files = new FileSystemManager(args[0], NullLogger<FileSystemManager>.Instance,
                PhysicalLoadTransactionOperations.Instance, new FileSystemManagerHooks
                {
                    LoadOperationObserver = stage =>
                    {
                        if (prepared) WriteProgress(report, stage, 0, progressTimer.Elapsed.TotalMilliseconds);
                    },
                    LocalPublicationObserver = observe,
                    AfterCanonicalWriteLockOpenedAsync = () =>
                    {
                        if (prepared) WriteProgress(report, "CanonicalLockOpened", 0, progressTimer.Elapsed.TotalMilliseconds);
                        return Task.CompletedTask;
                    },
                    BeforeCanonicalMutationBoundaryAsync = _ =>
                    {
                        if (prepared && (++mutationBoundaries == 1 || mutationBoundaries % 1000 == 0))
                            WriteProgress(report, "MutationAdmission", mutationBoundaries, progressTimer.Elapsed.TotalMilliseconds);
                        return Task.CompletedTask;
                    }
                });
            if (parts[0] == "recovery")
            {
                var staging = Path.Combine(files.RuntimeRootPath, "load-staging");
                if (Directory.Exists(staging) && Directory.EnumerateFileSystemEntries(staging).Any())
                    throw new InvalidOperationException("Cold resource recovery still has extraction sources.");
                DescribeFrame(files, report);
                var after = (bool)report["CommittedInput"]!;
                AnnouncePhaseStarted(parts[0], heap);
                allocated = GC.GetTotalAllocatedBytes(true); timer.Restart();
                await using (var lease = await files.AcquireCanonicalWriteLeaseAsync())
                    report["EstablishedGeneration"] = files.ReadExistingSessionGeneration(lease);
                FinishMeasurement(report, timer, allocated); measured = true;
                Verify(files, expected, report, after);
                VerifyCleanup(files);
                report["Phase"] = "CanonicalAcquisitionReturned"; report["Completed"] = true;
                WriteReport(report);
                return 0;
            }
            var state = new StateManager(files, new GameSettings(), NullLogger<StateManager>.Instance);
            var service = new SaveLoadService(files, state, NullLogger<SaveLoadService>.Instance, new SaveLoadServiceHooks
            {
                AfterLoadArchiveExtractedAsync = path => { extraction = path; return Task.CompletedTask; },
                BeforeLoadLeaseAcquisitionAsync = () =>
                {
                    prepared = true;
                    if (parts[0] == "preparation")
                    {
                        FinishMeasurement(report, timer, allocated); measured = true;
                        throw new PreparationMeasured();
                    }
                    AnnouncePhaseStarted(parts[0], heap);
                    allocated = GC.GetTotalAllocatedBytes(true); timer.Restart();
                    return Task.CompletedTask;
                }
            });
            // Source-implemented on both native OS; actual Linux evidence remains the owning runner's responsibility.
            if (parts[0] == "preparation")
            {
                AnnouncePhaseStarted(parts[0], heap);
                allocated = GC.GetTotalAllocatedBytes(true); timer.Restart();
            }
            var outcome = await service.LoadGameWithOutcomeAsync(files.ResolvePath(PortableLoadFixture.SourceRelative));
            report["Disposition"] = outcome.Disposition.ToString();
            report["NeedsFollowUp"] = outcome.NeedsFollowUp; report["ContinuationBlocked"] = outcome.ContinuationBlocked;
            report["EstablishedGeneration"] = outcome.EstablishedGeneration;
            report["CommittedObserverCount"] = committed;
            report["PublicationObserverCount"] = observed;
            report["PreparedBoundaryObserved"] = prepared;
            if (!prepared || !measured) throw new InvalidOperationException("The actual requested load boundary was not measured.");
            if (parts[0] == "preparation")
            {
                if (outcome.Disposition != LoadReplacementDisposition.NotLoaded || outcome.Failure is not PreparationMeasured ||
                    outcome.NeedsFollowUp || outcome.ContinuationBlocked || observed != 0)
                    throw new InvalidOperationException("Preparation did not stop cleanly before publication.", outcome.Failure);
                report["MetadataLength"] = 0L; report["MemberCount"] = 0;
                report["DiagnosticType"] = nameof(PreparationMeasured);
                Verify(files, expected, report, after: false);
                report["Phase"] = "PreparedBoundaryReturned";
            }
            else
            {
                if (parts[0] != "publication" || outcome.Disposition != LoadReplacementDisposition.Committed ||
                    outcome.Failure != null || outcome.NeedsFollowUp || outcome.ContinuationBlocked || committed != 1)
                    throw new InvalidOperationException("Actual typed publication did not commit exactly once.", outcome.Failure);
                Verify(files, expected, report, after: true);
                report["Phase"] = "TypedLoadCommitted";
            }
            VerifyCleanup(files);
            report["Completed"] = true;
            WriteReport(report);
            return 0;
        }
        catch (Exception failure)
        {
            if (!measured) FinishMeasurement(report, timer, allocated);
            report["Phase"] = "ResourceScenarioFailed"; report["Completed"] = false;
            report["FailureType"] = failure.GetType().Name; report["Failure"] = failure.ToString();
            WriteReport(report);
            return 90;
        }
    }

    /// <summary>
    /// Streams declared incoming and old-live workloads around the real producer, retaining the original manifested source.
    /// </summary>
    /// <param name="root">
    /// The absent isolated test-owned root.
    /// </param>
    /// <param name="workload">
    /// 64, 128 or 512 MiB minus mandatory reserve, or the exact many-member/name workload.
    /// </param>
    /// <param name="report">
    /// Receives actual archive/member/name/payload counts and declared old-live footprint.
    /// </param>
    /// <returns>
    /// Completion after exact fixture expectations and actual original archive budgets are verified.
    /// </returns>
    private static async Task SeedAsync(string root, string workload, Dictionary<string, object?> report)
    {
        var incomingPaths = new List<string>();
        var many = workload == "many";
        var payload = many ? 0 : long.Parse(workload) * MiB - BufferBytes;
        var oldBytes = 0L;
        var oldCount = 0;
        await PortableLoadFixture.CreateAsync(root, beforeProducer: async (files, producer) =>
        {
            if (many)
            {
                if (!await producer.SaveGameAsync("resource-count-base", "owned actual baseline inventory"))
                    throw new InvalidOperationException("The baseline current producer failed.");
                var baseline = Directory.GetFiles(files.ResolvePath("saves/manual_saves"), "*.zip").Single();
                var stats = ReadArchiveStats(baseline);
                // Delete only this newly produced baseline; final source will not have a second retained ZIP.
                AssertOwned(root, baseline); File.Delete(baseline);
                var needed = 8192 - stats.Entries;
                var nameBudget = 2L * MiB - 1024 - stats.NameBytes;
                if (needed <= 0 || nameBudget <= 0) throw new InvalidDataException("Current baseline does not fit many-member input.");
                var random = new Random(1553001); var buffer = new byte[BufferBytes];
                for (var index = 0; index < needed; index++)
                {
                    var fullNameBytes = checked((int)(nameBudget / needed + (index < nameBudget % needed ? 1 : 0)));
                    var name = ManyName(index, fullNameBytes);
                    WriteRandomFile(files, name, 128, random, buffer); incomingPaths.Add(files.ResolvePath(name));
                }
                report["BaselineEntryCount"] = stats.Entries; report["AdditionalIncomingCount"] = needed;
            }
            else
            {
                var random = new Random(1553002); var buffer = new byte[BufferBytes];
                for (long remaining = payload, index = 0; remaining > 0; index++)
                {
                    var count = Math.Min(remaining, 64 * MiB);
                    var name = $"{IncomingRoot}/bulk-{index:D2}.bin";
                    WriteRandomFile(files, name, count, random, buffer); incomingPaths.Add(files.ResolvePath(name));
                    remaining -= count;
                }
            }
        }, afterArchiveClosed: files =>
        {
            foreach (var path in incomingPaths) { AssertOwned(root, path); File.Delete(path); }
            var random = new Random(1553099); var buffer = new byte[BufferBytes];
            if (many)
            {
                for (var index = 0; index < 9216; index++)
                {
                    WriteRandomFile(files, $"{OldRoot}/old-{index:D5}.bin", 128, random, buffer);
                    oldBytes += 128; oldCount++;
                }
            }
            else
            {
                for (long remaining = payload, index = 0; remaining > 0; index++)
                {
                    var count = Math.Min(remaining, 64 * MiB);
                    WriteRandomFile(files, $"{OldRoot}/bulk-old-{index:D2}.bin", count, random, buffer);
                    oldBytes += count; oldCount++; remaining -= count;
                }
            }
            return Task.CompletedTask;
        }, moveProducedToSource: true);
        var manager = new FileSystemManager(root, NullLogger<FileSystemManager>.Instance);
        var source = manager.ResolvePath(PortableLoadFixture.SourceRelative);
        var archive = ReadArchiveStats(source);
        report["ArchiveEntries"] = archive.Entries; report["ArchiveNameBytes"] = archive.NameBytes;
        report["ExpandedBytes"] = archive.ExpandedBytes; report["ManifestBytes"] = archive.ManifestBytes;
        report["ArchiveBytes"] = new FileInfo(source).Length;
        report["IncomingFileCount"] = archive.IncomingFiles;
        report["RequestedBulkPayloadBytes"] = payload;
        report["OldAdditionalFileCount"] = oldCount; report["OldAdditionalBytes"] = oldBytes;
        report["PredictedPayloadFootprintBytes"] = checked(new FileInfo(source).Length + oldBytes + archive.ExpandedBytes +
            2 * (oldBytes + archive.ExpandedBytes) + 64 * MiB);
        if (many)
        {
            if (archive.Entries != 8192 || archive.NameBytes < 2 * MiB - 4096 || archive.NameBytes > 2 * MiB || oldCount != 9216)
                throw new InvalidDataException("Actual many-member counts or UTF8 names do not reach the declared workload.");
        }
        else if (archive.ExpandedBytes < payload || archive.ExpandedBytes > long.Parse(workload) * MiB || oldBytes != payload)
            throw new InvalidDataException("Actual bulk archive or old-live bytes do not reach the declared workload.");
        if (Directory.GetFiles(manager.ResolvePath("saves/manual_saves"), "*source*.zip").Length != 1)
            throw new InvalidDataException("Resource fixture retained another newly produced source archive.");
    }

    /// <summary>
    /// Builds a unique CJK/ASCII leaf at an exact requested UTF8 path length within native component limits.
    /// </summary>
    /// <param name="index">
    /// The stable zero-based additional payload ordinal.
    /// </param>
    /// <param name="fullNameBytes">
    /// The desired complete relative UTF8 path byte count.
    /// </param>
    /// <returns>
    /// A canonical slash-separated name with a leaf at most 255 UTF8 bytes and short Windows UTF16 spelling.
    /// </returns>
    private static string ManyName(int index, int fullNameBytes)
    {
        var prefix = IncomingRoot + "/";
        var suffix = $"-{index:D5}.bin";
        var filler = fullNameBytes - Encoding.UTF8.GetByteCount(prefix + suffix);
        if (filler < 0) throw new InvalidDataException("Many-member name budget is too small.");
        var leaf = new string('界', filler / 3) + new string('a', filler % 3) + suffix;
        if (Encoding.UTF8.GetByteCount(leaf) > 255) throw new InvalidDataException("Authored component exceeds native UTF8 limit.");
        return prefix + leaf;
    }

    /// <summary>
    /// Writes deterministic incompressible bytes through one reusable 64KiB buffer into an owned regular payload.
    /// </summary>
    /// <param name="files">
    /// Resolves this isolated session.
    /// </param>
    /// <param name="name">
    /// The canonical relative payload name.
    /// </param>
    /// <param name="count">
    /// Exact file bytes, at most 64MiB.
    /// </param>
    /// <param name="random">
    /// The deterministic workload generator, with distinct old and incoming seeds.
    /// </param>
    /// <param name="buffer">
    /// The reused 64KiB write buffer, never a whole payload array.
    /// </param>
    private static void WriteRandomFile(FileSystemManager files, string name, long count, Random random, byte[] buffer)
    {
        if (count < 0 || count > 64 * MiB || buffer.Length != BufferBytes) throw new InvalidDataException("Invalid streamed fixture member.");
        var path = files.ResolvePath(name); AssertOwned(files.GameSessionPath, path);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        using var output = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.None, BufferBytes);
        for (long written = 0; written < count;)
        {
            random.NextBytes(buffer); var take = (int)Math.Min(buffer.Length, count - written);
            output.Write(buffer, 0, take); written += take;
        }
    }

    /// <summary>
    /// Measures the actual original archive inventory and checks unchanged production budget rules.
    /// </summary>
    /// <param name="source">
    /// The original manifested current-producer ZIP.
    /// </param>
    /// <returns>
    /// Actual entry, UTF8 name, expanded and manifest byte counts.
    /// </returns>
    private static (int Entries, long NameBytes, long ExpandedBytes, long ManifestBytes, int IncomingFiles) ReadArchiveStats(string source)
    {
        using var archive = ZipFile.OpenRead(source);
        var manifest = archive.GetEntry("save_manifest.json") ?? throw new InvalidDataException("Original manifest is missing.");
        var entries = archive.Entries.Select(entry => new SaveLoadService.SaveArchiveEntryDescriptor(
            entry.FullName, entry.FullName.EndsWith('/'), entry.Length, entry.CompressedLength)).ToArray();
        SaveLoadService.ValidateTrustedArchiveBudget(entries);
        if (manifest.Length > 4 * MiB) throw new InvalidDataException("Original manifest exceeds its accepted ceiling.");
        return (entries.Length, entries.Sum(entry => (long)Encoding.UTF8.GetByteCount(entry.Path)),
            archive.Entries.Sum(entry => entry.Length), manifest.Length,
            archive.Entries.Count(entry => entry.Name.Length != 0 && entry.FullName != "save_manifest.json"));
    }

    /// <summary>
    /// Records actual v3 metadata/member inventory and exact generation regions while authoritative evidence exists.
    /// </summary>
    /// <param name="files">
    /// Resolves this owned runtime and generation path.
    /// </param>
    /// <param name="report">
    /// Receives frame length, count, decision, identity and exact generation bytes.
    /// </param>
    private static void DescribeFrame(FileSystemManager files, Dictionary<string, object?> report)
    {
        var active = Path.Combine(files.RuntimeRootPath, "trusted-local-publication-v1", "active.json");
        using var input = File.OpenRead(active);
        input.Position = 8; Span<byte> encodedLength = stackalloc byte[8]; input.ReadExactly(encodedLength);
        report["MetadataLength"] = BinaryPrimitives.ReadInt64LittleEndian(encodedLength);
        report["FrameBytes"] = input.Length;
        using var header = PortableLoadFixture.ReadFrame(active);
        report["MemberCount"] = header.RootElement.GetProperty("Members").GetArrayLength();
        report["TransactionId"] = header.RootElement.GetProperty("TransactionId").GetString();
        report["CommittedInput"] = header.RootElement.GetProperty("Committed").GetBoolean();
        var before = PortableLoadFixture.GenerationBefore(active, files.SessionGenerationPath);
        report["GenerationBefore"] = before == null ? null : Convert.ToBase64String(before);
        report["GenerationAfter"] = Convert.ToBase64String(PortableLoadFixture.GenerationAfter(active, files.SessionGenerationPath));
        report["GenerationAfterId"] = header.RootElement.GetProperty("GenerationAfter").GetProperty("Id").GetString();
    }

    /// <summary>
    /// Binds a real publication observer index to the sole exact runtime-generation member.
    /// </summary>
    /// <param name="files">
    /// Resolves actual active evidence and exact generation authority.
    /// </param>
    /// <param name="index">
    /// The actual operation's stable wire-member ordinal.
    /// </param>
    /// <returns>
    /// True only for the generation member; negative nonmember callbacks return <see langword="false"/>.
    /// </returns>
    private static bool MemberIsGeneration(FileSystemManager files, int index)
    {
        if (index < 0) return false;
        using var header = PortableLoadFixture.ReadFrame(Path.Combine(files.RuntimeRootPath, "trusted-local-publication-v1", "active.json"));
        return header.RootElement.GetProperty("Members")[index].GetProperty("Path").GetString() == files.SessionGenerationPath;
    }

    /// <summary>
    /// Checks every independently expected namespace hash and exact generation after the measured operation.
    /// </summary>
    /// <param name="files">
    /// The isolated physical target manager.
    /// </param>
    /// <param name="expected">
    /// Original archive-derived After and independently authored Before snapshots.
    /// </param>
    /// <param name="report">
    /// Receives complete verification counts and explicit source/config/history/soul hashes.
    /// </param>
    /// <param name="after">
    /// Selects complete After when <see langword="true"/>, or exact Before otherwise.
    /// </param>
    private static void Verify(FileSystemManager files, PortableLoadFixtureState expected, Dictionary<string, object?> report, bool after)
    {
        var actual = PortableLoadFixture.Snapshot(files.GameSessionPath);
        var target = after ? expected.After : expected.Before;
        if (actual.Count != target.Count || target.Any(pair => !actual.TryGetValue(pair.Key, out var value) || value != pair.Value))
            throw new InvalidDataException("Actual namespace differs from original ZIP/before expectations.");
        var generation = after ? (string?)report["GenerationAfter"] : expected.GenerationBefore;
        var actualGeneration = File.Exists(files.SessionGenerationPath) ? Convert.ToBase64String(File.ReadAllBytes(files.SessionGenerationPath)) : null;
        if (generation != actualGeneration) throw new InvalidDataException("Exact generation bytes differ.");
        if (report.TryGetValue("GenerationBefore", out var wireBefore) && (string?)wireBefore != expected.GenerationBefore)
            throw new InvalidDataException("Wire Before generation differs from independent fixture bytes.");
        if (expected.Protected.Any(pair => !actual.TryGetValue(pair.Key, out var value) || value != pair.Value))
            throw new InvalidDataException("Protected library/source changed.");
        report["VerifiedNamespace"] = true; report["VerifiedGeneration"] = true; report["VerifiedProtected"] = true;
        report["VerifiedFiles"] = actual.Count(pair => pair.Value != "Directory");
        report["VerifiedDirectories"] = actual.Count(pair => pair.Value == "Directory");
        report["GenerationBytes"] = actualGeneration;
        report["SourceSha256"] = actual[PortableLoadFixture.SourceRelative];
        report["ConfigSha256"] = actual["config.json"];
        report["HistorySha256"] = actual[ResourceMaterializationContract.HistoryPath];
        report["SoulSha256"] = actual["game_state/meta/soul_state.json"];
        report["After"] = after;
    }

    /// <summary>
    /// Requires exact owned journal and load-private cleanup after an ordinary operation completes.
    /// </summary>
    /// <param name="files">
    /// Resolves isolated runtime and session scratch.
    /// </param>
    private static void VerifyCleanup(FileSystemManager files)
    {
        foreach (var name in new[] { "trusted-local-publication-v1", "load-staging", "load-transactions", "save-staging" })
        {
            var path = Path.Combine(files.RuntimeRootPath, name);
            if (Directory.Exists(path) && Directory.EnumerateFileSystemEntries(path).Any())
                throw new InvalidDataException("Owned resource scratch cleanup is incomplete: " + name);
        }
        if (Directory.EnumerateFiles(files.GameSessionPath, ".boe-local-*", SearchOption.AllDirectories).Any() ||
            Directory.EnumerateFiles(Path.GetDirectoryName(files.SessionGenerationPath)!, ".boe-local-*").Any())
            throw new InvalidDataException("Owned resource member scratch cleanup is incomplete.");
    }

    /// <summary>
    /// Captures measured phase time and process-wide allocated bytes at the exact actual boundary.
    /// </summary>
    /// <param name="report">
    /// Receives elapsed, allocated and OS peak-RSS metrics.
    /// </param>
    /// <param name="timer">
    /// Time since the phase's actual starting boundary.
    /// </param>
    /// <param name="allocated">
    /// Process-wide allocated-byte count at that starting boundary.
    /// </param>
    private static void FinishMeasurement(Dictionary<string, object?> report, Stopwatch timer, long allocated)
    {
        timer.Stop();
        report["PhaseMilliseconds"] = timer.Elapsed.TotalMilliseconds;
        report["PhaseAllocatedBytes"] = GC.GetTotalAllocatedBytes(true) - allocated;
        report["ProcessPeakWorkingSetBytes"] = Process.GetCurrentProcess().PeakWorkingSet64;
        Console.WriteLine(JsonSerializer.Serialize(new
        {
            Phase = "MeasurementStopped", Operation = report["Operation"], HeapBytes = report["HeapBytes"],
            PhaseMilliseconds = report["PhaseMilliseconds"], PhaseAllocatedBytes = report["PhaseAllocatedBytes"]
        }));
        Console.Out.Flush();
        if (Console.ReadLine() != "measurement-stop") throw new InvalidOperationException("Missing owned parent stop-sample acknowledgement.");
    }

    /// <summary>
    /// Announces the actual starting boundary so parent phase RSS/disk sampling excludes earlier setup or preparation.
    /// </summary>
    /// <param name="operation">
    /// The seed, preparation, publication, cut or recovery phase entering measurement.
    /// </param>
    /// <param name="heap">
    /// The verified inherited managed heap available to this exact owned child.
    /// </param>
    private static void AnnouncePhaseStarted(string operation, long heap)
    {
        Console.WriteLine(JsonSerializer.Serialize(new { Phase = "MeasurementStarted", Operation = operation, HeapBytes = heap }));
        Console.Out.Flush();
        if (Console.ReadLine() != "measurement-start") throw new InvalidOperationException("Missing owned parent start-sample acknowledgement.");
    }

    /// <summary>
    /// Writes the single structured child report and flushes before ordinary exit or parent-owned termination.
    /// </summary>
    /// <param name="report">
    /// The actual metrics, verified results or explicit failure record.
    /// </param>
    private static void WriteReport(Dictionary<string, object?> report)
    {
        var peak = Process.GetCurrentProcess().PeakWorkingSet64;
        report["ProcessPeakWorkingSetBytes"] = peak;
        if (peak > 1024 * MiB && (bool)report["Completed"]!)
            throw new InvalidOperationException("Actual OS peak RSS exceeded the unchanged owned 1GiB stop.");
        Console.WriteLine(JsonSerializer.Serialize(report)); Console.Out.Flush();
    }

    /// <summary>
    /// Announces a load diagnostic boundary, holding closed durable evidence until its owned disk sample is acknowledged.
    /// </summary>
    /// <param name="report">
    /// Supplies the actual operation and inherited heap for the owning parent.
    /// </param>
    /// <param name="boundary">
    /// The observed read-only stage, canonical lock, mutation admission or publication callback.
    /// </param>
    /// <param name="index">
    /// The callback's actual member index or admission count; zero represents the lock boundary.
    /// </param>
    /// <param name="childMilliseconds">
    /// Monotonic child elapsed time since this operation began, independent of parent disk-scan and stdout latency.
    /// </param>
    private static void WriteProgress(Dictionary<string, object?> report, string boundary, int index, double childMilliseconds)
    {
        var requiresDiskSample = boundary is "IntentStaged" or "IntentPublished" or "CommitStaged" or "Committed" or "CleanupComplete";
        Console.WriteLine(JsonSerializer.Serialize(new
        {
            Phase = "OperationProgress", Operation = report["Operation"], HeapBytes = report["HeapBytes"],
            Boundary = boundary, Index = index, ChildMilliseconds = childMilliseconds, RequiresDiskSample = requiresDiskSample
        }));
        Console.Out.Flush();
        if (requiresDiskSample && Console.ReadLine() != "operation-sampled")
            throw new InvalidOperationException("The owning parent did not acknowledge the actual durable-boundary sample.");
    }

    /// <summary>
    /// Confirms exact fixture creation/deletion names remain under their intended owned root.
    /// </summary>
    /// <param name="root">
    /// The root whose ownership the caller already holds.
    /// </param>
    /// <param name="path">
    /// The exact authored regular path to create or remove.
    /// </param>
    private static void AssertOwned(string root, string path)
    {
        if (!Path.GetFullPath(path).StartsWith(Path.GetFullPath(root) + Path.DirectorySeparatorChar,
            OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal))
            throw new InvalidOperationException("Resource fixture path escaped its owned root.");
    }
}
