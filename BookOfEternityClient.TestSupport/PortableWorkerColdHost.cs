using BookOfEternityClient.Core;
using Microsoft.Extensions.Logging.Abstractions;

namespace BookOfEternityClient.Tests;

/// <summary>Actual worker decision crash host; deliberate exit bypasses every finally and lease disposal.</summary>
public static class PortableWorkerColdHost
{
    /// <summary>Runs one owned worker publication/recovery cut supplied by its test.</summary>
    public static async Task<int> RunAsync(string[] args)
    {
        if (args.Length != 4) return 64;
        try
        {
            var cut = args[3];
            void Observe(TrustedLocalPublicationPhase phase, int index)
            {
                if (phase.ToString() == cut || cut == "first-member" && phase == TrustedLocalPublicationPhase.MemberPublished && index == 0)
                    Environment.Exit(73);
            }
            var files = new FileSystemManager(args[0], NullLogger<FileSystemManager>.Instance,
                PhysicalLoadTransactionOperations.Instance, new FileSystemManagerHooks
                {
                    LocalPublicationObserver = args[2] == "worker-publish" ? Observe : null,
                    LocalPublicationRecoveryObserver = args[2] == "worker-recover-cut" ? Observe : null
                });
            await using var lease = await files.AcquireCanonicalWriteLeaseAsync();
            if (args[2] == "worker-recover") return 0;
            if (args[2] is "worker-conflict" or "worker-recover-cut") return 78;
            var transaction = await files.BeginWorkerApplyTransactionAsync(lease,
                [new("game_state/world/worker-a.bin", [0xef, 0xbb, 0xbf, 0xff, 0], [0xfe, 0, 7]),
                 new("game_state/world/worker-b.bin", null, []), new("game_state/world/worker-c.bin", [], null)]);
            if (cut == "PendingValidation") Environment.Exit(73);
            files.CommitWorkerApplyTransaction(lease, transaction);
            return 78;
        }
        catch (InvalidDataException) when (args[2] == "worker-conflict") { return 0; }
        catch (Exception failure) { Console.Error.WriteLine(failure); return 1; }
    }
}
