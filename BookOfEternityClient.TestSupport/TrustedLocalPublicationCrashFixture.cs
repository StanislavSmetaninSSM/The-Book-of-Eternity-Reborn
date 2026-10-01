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
        if (args[2] == "generation-read")
        {
            var request = args[3].Split(':');
            if (request.Length != 2) return 64;
            var reject = request[1] == "reject";
            try
            {
                if (request[0] == "bound-write")
                    await SessionOperationContext.RunBoundAsync(files, args[1], async () =>
                    {
                        await using (var generationLease = await files.AcquireCanonicalWriteLeaseAsync())
                            if (files.ReadLocalGenerationSnapshot(generationLease).Binding.Id != args[1])
                                throw new InvalidOperationException("The snapshot returned a different generation.");
                        await files.WriteFileAtomicBytesAsync("game_state/core/read-proof.json", [1]);
                    });
                else
                {
                    await using var generationLease = await files.AcquireCanonicalWriteLeaseAsync();
                    var id = request[0] switch
                    {
                        "snapshot" => files.ReadLocalGenerationSnapshot(generationLease).Binding.Id,
                        "existing" => files.ReadExistingSessionGeneration(generationLease),
                        "current" => files.IsCurrentSessionGeneration(generationLease, args[1]) ? args[1] : null,
                        "get-or-create" => files.GetOrCreateSessionGeneration(generationLease),
                        _ => throw new ArgumentException("Unknown generation reader.")
                    };
                    if (id != args[1]) throw new InvalidOperationException("The reader returned a different generation.");
                }
                return reject ? 78 : 0;
            }
            catch (InvalidDataException) when (reject) { return 0; }
            catch (Exception ex) { Console.Error.WriteLine(ex); return 1; }
        }
        if (args[2] == "client-nonregular")
        {
            try
            {
                const string member = "game_state/core/nonregular.bin";
                switch (args[3])
                {
                    case "bootstrap":
                        await new StateManager(files, new BookOfEternityClient.Configuration.GameSettings(),
                            NullLogger<StateManager>.Instance).BootstrapLocalStorageAsync(); break;
                    case "write": await files.WriteFileAtomicBytesAsync(member, [2]); break;
                    case "append": await files.AppendFileAtomicAsync(member, "a"); break;
                    case "compare": await files.CompareExchangeFileBytesAsync(member, null, [2]); break;
                    case "delete": files.DeleteFile(member); break;
                    default: return 64;
                }
                return 78; // A nonregular canonical file was accepted.
            }
            catch (InvalidDataException) { return 0; }
            catch (Exception ex) { Console.Error.WriteLine(ex); return 1; }
        }
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
