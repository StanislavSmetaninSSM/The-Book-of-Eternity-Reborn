using BookOfEternityClient.Core;
using BookOfEternityClient.Services;
using Microsoft.Extensions.Logging.Abstractions;

namespace BookOfEternityClient.Tests;

/// <summary>Test-only executable entry: abrupt process exit never runs lease/finally cleanup.</summary>
public static class TrustedLocalPublicationCrashFixture
{
    [System.Runtime.InteropServices.DllImport("libc", EntryPoint = "mkfifo", SetLastError = true)]
    private static extern int MakeOrdinaryReadFifo(string path, uint mode);

    public static async Task<int> RunAsync(string[] args)
    {
        if (args.Length != 4) return 64;
        var files = new FileSystemManager(args[0], NullLogger<FileSystemManager>.Instance);
        if (args[2].StartsWith("backup-", StringComparison.Ordinal))
        {
            try
            {
                if (args[2] is "backup-recover" or "backup-conflict")
                {
                    await using var recoveryLease = await files.AcquireCanonicalWriteLeaseAsync();
                    return args[2] == "backup-recover" ? 0 : 78;
                }
                files = new FileSystemManager(args[0], NullLogger<FileSystemManager>.Instance,
                    PhysicalLoadTransactionOperations.Instance, new FileSystemManagerHooks
                    {
                        LocalPublicationObserver = (phase, index) =>
                        {
                            if (args[3] == "cleanup-debt")
                            {
                                if (phase == TrustedLocalPublicationPhase.Committed) throw new BeginRecovery();
                                return;
                            }
                            var reached = args[3] switch
                            {
                                "first-member" => phase == TrustedLocalPublicationPhase.MemberPublished && index == 0,
                                "last-member" => phase == TrustedLocalPublicationPhase.MemberPublished && index == 1,
                                _ => phase.ToString() == args[3]
                            };
                            if (reached) Environment.Exit(73);
                        }
                    });
                const string source = "game_state/core/backup-source.bin";
                var backup = files.ResolvePath("game_state/core/arbitrary.test-backup");
                switch (args[2])
                {
                    case "backup-create": await files.CreateBackupAsync(source); break;
                    case "backup-restore": await files.RestoreBackupAsync(backup, source); break;
                    case "backup-cleanup": await files.CleanupBackupAsync(backup); break;
                    default: return 64;
                }
                if (args[3] == "cleanup-debt") Environment.Exit(73);
                return 78;
            }
            catch (InvalidDataException) when (args[2] == "backup-conflict" || args[3] == "reject") { return 0; }
            catch (Exception ex) { Console.Error.WriteLine(ex); return 1; }
        }
        if (args[2].StartsWith("directory-", StringComparison.Ordinal))
        {
            try
            {
                if (args[2] is "directory-recover" or "directory-conflict")
                {
                    await using var recoveryLease = await files.AcquireCanonicalWriteLeaseAsync();
                    return args[2] == "directory-recover" ? 0 : 78;
                }
                files = new FileSystemManager(args[0], NullLogger<FileSystemManager>.Instance,
                    PhysicalLoadTransactionOperations.Instance, new FileSystemManagerHooks
                    {
                        LocalPublicationObserver = (phase, index) =>
                        {
                            if (args[2] != "directory-cut") return;
                            if (args[3] == "cleanup-debt")
                            {
                                if (phase == TrustedLocalPublicationPhase.Committed) throw new BeginRecovery();
                                return;
                            }
                            var reached = args[3] switch
                            {
                                "first-member" => phase == TrustedLocalPublicationPhase.MemberPublished && index == 0,
                                "last-member" => phase == TrustedLocalPublicationPhase.MemberPublished && index == 2,
                                _ => phase.ToString() == args[3]
                            };
                            if (reached) Environment.Exit(73);
                        }
                    });
                await using var directoryLease = await files.AcquireCanonicalWriteLeaseAsync();
                files.DeleteDirectoryTree(directoryLease, "pending_turn_snapshot");
                if (args[2] == "directory-cut" && args[3] == "cleanup-debt")
                    Environment.Exit(73); // API returned a committed result while retaining cleanup debt.
                return 78;
            }
            catch (InvalidDataException) when (args[2] is "directory-conflict" or "directory-reject") { return 0; }
            catch (Exception ex) { Console.Error.WriteLine(ex); return 1; }
        }
        if (args[2].StartsWith("coordinated-", StringComparison.Ordinal))
        {
            try
            {
                if (args[2] is "coordinated-recover" or "coordinated-conflict")
                {
                    await using var recoveryLease = await files.AcquireCanonicalWriteLeaseAsync();
                    return args[2] == "coordinated-recover" ? 0 : 78;
                }
                if (args[2] != "coordinated-cut") return 64;
                files = new FileSystemManager(args[0], NullLogger<FileSystemManager>.Instance,
                    PhysicalLoadTransactionOperations.Instance, new FileSystemManagerHooks
                    {
                        LocalPublicationObserver = (phase, index) =>
                        {
                            var reached = args[3] switch
                            {
                                "first-member" => phase == TrustedLocalPublicationPhase.MemberPublished && index == 0,
                                "last-member" => phase == TrustedLocalPublicationPhase.MemberPublished && index == 2,
                                _ => phase.ToString() == args[3]
                            };
                            if (reached) Environment.Exit(73);
                        }
                    });
                // The caller seeds an admitted existing generation; this
                // invokes the real owning helper, not the publisher directly.
                await CoordinatedStateWriteHelper.TryCommitAsync(files,
                    new CoordinatedStateWriteHelper.PlannedWrite("game_state/meta/coordinated_replace.json", null, "{\"value\":\"accepted\"}", true,
                        ExactPrevious: new CanonicalBeforeImage(true, [0xEF, 0xBB, 0xBF, 0xFF, 0])),
                    new CoordinatedStateWriteHelper.PlannedWrite("game_state/meta/coordinated_create.json", null, "{\"value\":\"accepted\"}", true,
                        ExactPrevious: new CanonicalBeforeImage(false, null)),
                    new CoordinatedStateWriteHelper.PlannedWrite("game_state/meta/coordinated_delete.json", null, null, true,
                        ExactPrevious: new CanonicalBeforeImage(true, [])));
                return 66;
            }
            catch (InvalidDataException ex) when (args[2] == "coordinated-conflict")
            {
                Console.WriteLine(ex.Message);
                return 0;
            }
            catch (Exception ex) { Console.Error.WriteLine(ex); return 1; }
        }
        if (args[2] == "ordinary-read")
        {
            const string member = "game_state/core/ordinary-read.bin";
            if (args[3] == "async-boundary")
                files = new FileSystemManager(args[0], NullLogger<FileSystemManager>.Instance,
                    PhysicalLoadTransactionOperations.Instance, new FileSystemManagerHooks
                    {
                        BeforeCanonicalReadOpenAsync = relative =>
                        {
                            if (relative != member) return Task.CompletedTask;
                            var path = Path.Combine(args[0], "game_session", member);
                            File.Delete(path);
                            if (MakeOrdinaryReadFifo(path, Convert.ToUInt32("600", 8)) != 0)
                                throw new IOException("The owned FIFO boundary fixture could not be created.");
                            return Task.CompletedTask;
                        }
                    });
            try
            {
                switch (args[3])
                {
                    case "async":
                    case "async-boundary": await files.ReadFileBytesAsync(member); break;
                    case "sync": files.ReadFileBytesSync(member); break;
                    case "exists": files.FileExists(member); break;
                    default: return 64;
                }
                return 78;
            }
            catch (InvalidDataException) { return 0; }
            catch (Exception ex) { Console.Error.WriteLine(ex); return 1; }
        }
        if (args[2].StartsWith("replacement-", StringComparison.Ordinal))
        {
            try
            {
                if (args[2] == "replacement-recover")
                {
                    await using var replacementRecoveryLease = await files.AcquireCanonicalWriteLeaseAsync();
                    return 0;
                }
                var replacementGenerationBefore = File.ReadAllBytes(files.SessionGenerationPath);
                var replacementFiles = new FileSystemManager(args[0], NullLogger<FileSystemManager>.Instance,
                    PhysicalLoadTransactionOperations.Instance, new FileSystemManagerHooks
                    {
                        LocalPublicationObserver = (phase, index) =>
                        {
                            if (args[2] != "replacement-cut") return;
                            var cut = args[3] switch
                            {
                                "early-member" => phase == TrustedLocalPublicationPhase.MemberPublished && index == 0,
                                "after-generation" => phase == TrustedLocalPublicationPhase.MemberPublished &&
                                    !replacementGenerationBefore.AsSpan().SequenceEqual(File.ReadAllBytes(files.SessionGenerationPath)),
                                "committed" => phase == TrustedLocalPublicationPhase.Committed,
                                "cleanup" => phase == TrustedLocalPublicationPhase.CleanupMember,
                                _ => false
                            };
                            if (cut) Environment.Exit(73);
                        }
                    });
                await replacementFiles.ClearGameStateAsync();
                return args[2] == "replacement-success" ? 0 : 66;
            }
            catch (InvalidDataException) when (args[2] == "replacement-reject") { return 0; }
            catch (Exception ex) { Console.Error.WriteLine(ex); return 1; }
        }
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
