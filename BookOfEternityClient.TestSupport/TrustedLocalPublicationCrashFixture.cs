using BookOfEternityClient.Core;
using Microsoft.Extensions.Logging.Abstractions;

namespace BookOfEternityClient.Tests;

/// <summary>Test-only executable entry: abrupt process exit never runs lease/finally cleanup.</summary>
public static class TrustedLocalPublicationCrashFixture
{
    public static async Task<int> RunAsync(string[] args)
    {
        if (args.Length != 4) return 64;
        var files = new FileSystemManager(args[0], NullLogger<FileSystemManager>.Instance);
        if (args[2] == "console-startup")
        {
            try
            {
                // The real app entrypoint runs in this separate owned process.
                // Catching reflection failures here keeps a failing RED startup
                // from generating an unhandled-process/core-dump fixture.
                var entrypoint = typeof(GameEngine).Assembly.EntryPoint!;
                var result = entrypoint.Invoke(null, [new[] { args[0], "--e2e-script", args[3],
                    "--e2e-artifacts", Path.Combine(args[0], "artifacts"), "--plain-output" }]);
                if (result is Task task) await task;
                return Environment.ExitCode;
            }
            catch (Exception ex) { Console.Error.WriteLine(ex); return 1; }
        }
        if (args[2] == "client-bootstrap")
        {
            try
            {
                var settings = new BookOfEternityClient.Configuration.GameSettings();
                var state = new StateManager(files, settings, NullLogger<StateManager>.Instance);
                var generation = await state.BootstrapLocalStorageAsync();
                return settings.Language == args[3] && (args[1].Length == 0 || generation == args[1]) ? 0 : 77;
            }
            catch (Exception ex) { Console.Error.WriteLine(ex); return 1; }
        }
        if (args[2] == "contend")
        {
            using var timeout = new CancellationTokenSource(TimeSpan.FromMilliseconds(350));
            try { await using var unexpected = await files.AcquireCanonicalWriteLeaseAsync(cancellationToken: timeout.Token); return 65; }
            catch (OperationCanceledException) { return 74; }
        }
        await using var lease = await files.AcquireCanonicalWriteLeaseAsync();
        var publisher = new TrustedLocalFilePublication(files, new TrustedLocalFileScope([args[0]]));
        if (args[2] == "recover") { publisher.Recover(lease); return 0; }
        var phase = Enum.Parse<TrustedLocalPublicationPhase>(args[3]);
        void ExitAtPhase(TrustedLocalPublicationPhase actual, int _) { if (phase == actual) Environment.Exit(73); }
        TrustedLocalFileChange[] members =
        [
            new(Path.Combine(args[0], "replace"), [0xEF, 0xBB, 0xBF, 0xFF], []),
            new(Path.Combine(args[0], "create"), null, [0xFE, 0]),
            new(Path.Combine(args[0], "delete"), [], null)
        ];
        if (phase == TrustedLocalPublicationPhase.RollbackStaged)
        {
            try
            {
                publisher.Publish(lease, TrustedLocalGeneration.Existing(args[1]), members,
                    (actual, index) => { if (actual == TrustedLocalPublicationPhase.MemberPublished && index == 2) throw new BeginRecovery(); });
            }
            catch (BeginRecovery) { publisher.Recover(lease, ExitAtPhase); }
        }
        else publisher.Publish(lease, TrustedLocalGeneration.Existing(args[1]), members, ExitAtPhase);
        return 66; // A requested crash phase was never reached.
    }

    private sealed class BeginRecovery : Exception { }
}
