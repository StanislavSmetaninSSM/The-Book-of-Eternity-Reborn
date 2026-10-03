using System.Text.Json;
using BookOfEternityClient.Configuration;
using BookOfEternityClient.Core;
using BookOfEternityClient.Services;
using Microsoft.Extensions.Logging.Abstractions;

namespace BookOfEternityClient.Tests;

/// <summary>
/// Runs actual typed load or journal-only canonical acquisition in an owned process.
/// </summary>
public static class PortableLoadColdHost
{
    /// <summary>
    /// Announces an exact actual cut and waits for parent termination, or reports normal cold acquisition.
    /// </summary>
    /// <param name="args">
    /// The owned root, declared heap bytes, load-publish/load-recover/load-recover-cut mode and Phase|member selector.
    /// </param>
    /// <returns>
    /// Zero for normal acquisition, 90 for a recorded failure or 64 for unsupported arguments; cut children never return.
    /// </returns>
    public static async Task<int> RunAsync(string[] args)
    {
        if (args.Length != 4 || args[2] is not ("load-publish" or "load-recover" or "load-recover-cut")) return 64;
        var heap = GC.GetGCMemoryInfo().TotalAvailableMemoryBytes;
        if (heap != long.Parse(args[1])) throw new InvalidOperationException("Cold child did not inherit its declared heap.");
        FileSystemManager? files = null;
        string? extraction = null;
        var armed = args[2] != "load-publish";
        try
        {
            Action<TrustedLocalPublicationPhase, int> observe = (phase, index) =>
            {
                if (!armed) return;
                var request = args[3].Split('|');
                if (request.Length != 2 || phase.ToString() != request[0]) return;
                var journalRoot = Path.Combine(files!.RuntimeRootPath, "trusted-local-publication-v1");
                var active = Path.Combine(journalRoot, "active.json");
                var frame = phase == TrustedLocalPublicationPhase.IntentStaged ? Path.Combine(journalRoot, "intent.tmp") : active;
                using var header = PortableLoadFixture.ReadFrame(frame);
                var members = header.RootElement.GetProperty("Members").EnumerateArray().ToArray();
                var relative = index < 0 ? null : Relative(files, members[index].GetProperty("Path").GetString()!);
                var fileIndices = members.Select((member, ordinal) => (member, ordinal))
                    .Where(value => value.member.GetProperty("After").GetProperty("Kind").GetString() == "File" &&
                        value.member.GetProperty("Path").GetString() != files.SessionGenerationPath).Select(value => value.ordinal).ToArray();
                var matches = request[1] switch
                {
                    "@none" => index == -1,
                    "@first" => index == fileIndices.First(),
                    "@late" => index == fileIndices.Last(),
                    _ => relative == request[1]
                };
                if (!matches) return;
                var before = PortableLoadFixture.GenerationBefore(frame, files.SessionGenerationPath);
                Console.WriteLine(JsonSerializer.Serialize(new
                {
                    Phase = "CutReady", HeapBytes = heap, Mode = args[2], Cut = phase.ToString(), Index = index,
                    MemberPath = relative, AbsoluteMemberPath = index < 0 ? null : members[index].GetProperty("Path").GetString(),
                    ActivePath = active, ActiveExists = File.Exists(active), FramePath = frame,
                    FrameSha256 = PortableLoadFixture.Hash(frame),
                    TransactionId = header.RootElement.GetProperty("TransactionId").GetString(),
                    Committed = header.RootElement.GetProperty("Committed").GetBoolean(),
                    GenerationBefore = before == null ? null : Convert.ToBase64String(before),
                    GenerationBeforeId = header.RootElement.GetProperty("GenerationBefore").GetProperty("Id").GetString(),
                    GenerationAfter = Convert.ToBase64String(PortableLoadFixture.GenerationAfter(frame, files.SessionGenerationPath)),
                    GenerationAfterId = header.RootElement.GetProperty("GenerationAfter").GetProperty("Id").GetString(),
                    FirstFileIndex = fileIndices.First(), LateFileIndex = fileIndices.Last(), ExtractionRoot = extraction,
                    Os = System.Runtime.InteropServices.RuntimeInformation.OSDescription
                }));
                Console.Out.Flush();
                Thread.Sleep(Timeout.Infinite); // Parent kills only this announced, owned process.
            };
            files = new FileSystemManager(args[0], NullLogger<FileSystemManager>.Instance,
                PhysicalLoadTransactionOperations.Instance, new FileSystemManagerHooks
                {
                    LocalPublicationObserver = observe,
                    LocalPublicationRecoveryObserver = args[2] == "load-recover-cut" ? observe : null
                });
            if (args[2] != "load-publish")
            {
                await using var lease = await files.AcquireCanonicalWriteLeaseAsync();
                Console.WriteLine(JsonSerializer.Serialize(new
                {
                    Phase = "CanonicalAcquisitionReturned", HeapBytes = heap,
                    GenerationId = files.ReadExistingSessionGeneration(lease),
                    GenerationBytes = File.Exists(files.SessionGenerationPath) ? Convert.ToBase64String(File.ReadAllBytes(files.SessionGenerationPath)) : null
                }));
                return 0;
            }
            var state = new StateManager(files, new GameSettings(), NullLogger<StateManager>.Instance);
            var service = new SaveLoadService(files, state, NullLogger<SaveLoadService>.Instance,
                new SaveLoadServiceHooks
                {
                    AfterLoadArchiveExtractedAsync = path => { extraction = path; armed = true; return Task.CompletedTask; }
                });
            var outcome = await service.LoadGameWithOutcomeAsync(files.ResolvePath(PortableLoadFixture.SourceRelative));
            Console.WriteLine(JsonSerializer.Serialize(new
            {
                Phase = "TypedLoadReturnedWithoutCut", HeapBytes = heap, Disposition = outcome.Disposition.ToString(),
                outcome.ContinuationBlocked, outcome.NeedsFollowUp, outcome.EstablishedGeneration,
                FailureType = outcome.Failure?.GetType().Name, Failure = outcome.Failure?.ToString()
            }));
            return 90;
        }
        catch (Exception failure)
        {
            Console.WriteLine(JsonSerializer.Serialize(new
            { Phase = "ColdScenarioFailed", HeapBytes = heap, FailureType = failure.GetType().Name, failure.Message, Stack = failure.StackTrace }));
            return 90;
        }
    }

    /// <summary>
    /// Labels the exact runtime-generation member separately from session-relative members.
    /// </summary>
    /// <param name="files">
    /// The owned production manager resolving both authorities.
    /// </param>
    /// <param name="path">
    /// An absolute path from actual v3 evidence.
    /// </param>
    /// <returns>
    /// The session-relative slash-separated name, or @generation for the exact generation member.
    /// </returns>
    private static string Relative(FileSystemManager files, string path) => path == files.SessionGenerationPath
        ? "@generation" : Path.GetRelativePath(files.GameSessionPath, path).Replace('\\', '/');
}
