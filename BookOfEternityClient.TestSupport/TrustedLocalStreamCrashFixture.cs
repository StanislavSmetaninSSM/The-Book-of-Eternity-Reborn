using System.Text.Json;
using BookOfEternityClient.Core;
using Microsoft.Extensions.Logging.Abstractions;

namespace BookOfEternityClient.Tests;

public static class TrustedLocalStreamCrashFixture
{
    public static async Task<int> RunAsync(string[] args)
    {
        if (args.Length != 4 || args[2] is not ("stream-recover" or "stream-publish" or "stream-recover-cut")) return 64;
        var available = GC.GetGCMemoryInfo().TotalAvailableMemoryBytes;
        if (available != long.Parse(args[1])) throw new InvalidOperationException("The owned cold host did not inherit its declared heap limit.");
        try
        {
            void Cut(TrustedLocalPublicationPhase phase, int index)
            {
                var reached = args[3] switch
                {
                    "first-member" => phase == TrustedLocalPublicationPhase.MemberPublished && index == 0,
                    "last-member" => phase == TrustedLocalPublicationPhase.MemberPublished && index == 2,
                    _ => args[3] == phase.ToString()
                };
                if (!reached) return;
                Console.WriteLine(JsonSerializer.Serialize(new { Phase = "AbruptExit", Cut = phase.ToString(), Index = index, HeapBytes = available }));
                Console.Out.Flush(); Environment.Exit(73);
            }
            var files = new FileSystemManager(args[0], NullLogger<FileSystemManager>.Instance,
                PhysicalLoadTransactionOperations.Instance, new FileSystemManagerHooks
                {
                    LocalPublicationObserver = (phase, index) =>
                    {
                        if (phase == TrustedLocalPublicationPhase.IntentPublished)
                            Directory.Delete(Path.Combine(args[0], "inputs"), recursive: true);
                        if (args[2] == "stream-publish") Cut(phase, index);
                    },
                    LocalPublicationRecoveryObserver = (phase, index) =>
                    { if (args[2] == "stream-recover-cut") Cut(phase, index); }
                });
            await using var lease = await files.AcquireCanonicalWriteLeaseAsync();
            if (args[2] == "stream-publish")
            {
                var allocated = GC.GetTotalAllocatedBytes(true);
                var scope = new TrustedLocalFileScope([args[0]]);
                var outcome = await files.PublishLocalImageFilesAsync(lease,
                [
                    new("game_state/core/stream-target.bin", TrustedLocalFileImage.CaptureFile(scope, files.ResolvePath("game_state/core/stream-target.bin")),
                        TrustedLocalFileImage.CaptureFile(scope, Path.Combine(args[0], "inputs/after.bin"))),
                    new("game_state/core/stream-created.bin", TrustedLocalFileImage.FromBytes(null), TrustedLocalFileImage.FromBytes([7, 8])),
                    new("game_state/core/stream-deleted.bin", TrustedLocalFileImage.CaptureFile(scope, files.ResolvePath("game_state/core/stream-deleted.bin")),
                        TrustedLocalFileImage.FromBytes(null))
                ]);
                Console.WriteLine(JsonSerializer.Serialize(new { Phase = "PublicationReturned", Disposition = outcome.Disposition.ToString(),
                    HeapBytes = available, AllocatedBytes = GC.GetTotalAllocatedBytes(true) - allocated }));
                return 0;
            }
            Console.WriteLine(JsonSerializer.Serialize(new { Phase = "CanonicalAcquisitionReturned", HeapBytes = available }));
            return 0;
        }
        catch (Exception failure)
        {
            Console.WriteLine(JsonSerializer.Serialize(new
            { Phase = "CanonicalAcquisitionFailed", HeapBytes = available, FailureType = failure.GetType().Name,
                failure.Message, Stack = failure.StackTrace }));
            return failure is InvalidDataException ? 78 : 90;
        }
    }
}
