using BookOfEternityClient.Core;
using BookOfEternityClient.Services.GmWorkers;
using System.Text;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed partial class GmWorkerApplyGateTests
{
    [Fact]
    public Task ApplyAsync_ValidatorThrowsRestoresWholeSetAndRethrowsOriginal() =>
        AssertValidatorExceptionDecisionAsync(conflict: false);

    [Fact]
    public Task ApplyAsync_ValidatorThrowsWithLateConflictPreservesBothCauses() =>
        AssertValidatorExceptionDecisionAsync(conflict: true);

    private static async Task AssertValidatorExceptionDecisionAsync(bool conflict)
    {
        var root = CreateTempRoot();
        try
        {
            const string replaced = "game_state/world/weather.json";
            const string added = "game_state/world/worker-added.json";
            const string deleted = "game_state/world/worker-z-deleted.json";
            const string history = "game_state/history/worker-exception-witness.bin";
            const string later = "game_state/world/worker-later.json";
            var fs = CreateFileSystem(root);
            var (profile, task, proposal) = await PrepareAllowedRepairAsync(fs);
            var before = new byte[] { 0xef, 0xbb, 0xbf }
                .Concat(Encoding.UTF8.GetBytes("{\"before\":true}\r\n")).ToArray();
            var after = File.ReadAllBytes(fs.ResolvePath(proposal.ChangedFiles[0].ContentRef!));
            var foreign = Encoding.UTF8.GetBytes("{\"foreign\":true}");
            var historyBytes = new byte[] { 44, 55, 0, 255 };
            File.WriteAllBytes(fs.ResolvePath(replaced), before);
            File.WriteAllBytes(fs.ResolvePath(deleted), []);
            Directory.CreateDirectory(Path.GetDirectoryName(fs.ResolvePath(history))!);
            File.WriteAllBytes(fs.ResolvePath(history), historyBytes);
            var addedContent = $"worker_proposals/{proposal.ProposalId}/{added}";
            Directory.CreateDirectory(Path.GetDirectoryName(fs.ResolvePath(addedContent))!);
            File.WriteAllBytes(fs.ResolvePath(addedContent), []);
            task = task with
            {
                AllowedProposalPaths = [replaced, added, deleted],
                ContextFiles =
                [
                    new WorkerFileReference { Path = replaced, Sha256 = ComputeSha256(before) },
                    new WorkerFileReference { Path = added, Sha256 = "missing" },
                    new WorkerFileReference { Path = deleted, Sha256 = ComputeSha256(Array.Empty<byte>()) }
                ]
            };
            proposal = proposal with
            {
                ChangedFiles =
                [
                    proposal.ChangedFiles[0] with { BeforeSha256 = ComputeSha256(before) },
                    new WorkerChangedFile
                    {
                        Path = added, ChangeKind = WorkerFileChangeKind.Add,
                        BeforeSha256 = "missing", AfterSha256 = ComputeSha256(Array.Empty<byte>()),
                        ContentRef = addedContent
                    },
                    new WorkerChangedFile
                    {
                        Path = deleted, ChangeKind = WorkerFileChangeKind.Delete,
                        BeforeSha256 = ComputeSha256(Array.Empty<byte>()), AfterSha256 = "missing"
                    }
                ]
            };
            await ReserveTaskAsync(fs, task);
            var generation = File.ReadAllBytes(fs.SessionGenerationPath);
            var reservationPath = fs.ResolvePath(GmWorkerBridgePool.GetTaskPacketPath(task.TaskId));
            var reservation = File.ReadAllBytes(reservationPath);
            var active = Path.Combine(fs.RuntimeRootPath, "trusted-local-publication-v1", "active.json");
            var primary = new InvalidOperationException("validator exception after complete pending set");
            var validatorCalls = 0;
            byte[]? pendingEvidence = null;
            var gate = new GmWorkerApplyGate(fs, async () =>
            {
                validatorCalls++;
                Assert.Equal(after, File.ReadAllBytes(fs.ResolvePath(replaced)));
                Assert.Empty(File.ReadAllBytes(fs.ResolvePath(added)));
                Assert.False(File.Exists(fs.ResolvePath(deleted)));
                Assert.Equal(generation, File.ReadAllBytes(fs.SessionGenerationPath));
                Assert.Equal(historyBytes, File.ReadAllBytes(fs.ResolvePath(history)));
                pendingEvidence = File.ReadAllBytes(active);
                Assert.False(File.Exists(fs.ActiveWorkerApplyTransactionJournalPath));
                if (conflict) File.WriteAllBytes(fs.ResolvePath(deleted), foreign);
                await Task.Yield();
                throw primary;
            });

            if (conflict)
            {
                var error = await Assert.ThrowsAsync<AggregateException>(() =>
                    gate.ApplyReservedAsync(proposal, profile, task.SessionGeneration));
                Assert.Equal(2, error.InnerExceptions.Count);
                Assert.Same(primary, error.InnerExceptions[0]);
                var recovery = Assert.IsType<InvalidDataException>(error.InnerExceptions[1]);
                Assert.Equal("A publication member contains unknown bytes; evidence retained.", recovery.Message);
                AssertConflictState();
                await Assert.ThrowsAnyAsync<InvalidDataException>(() =>
                    fs.WriteFileAtomicAsync(later, "{}"));
                Assert.False(File.Exists(fs.ResolvePath(later)));
                var fresh = new FileSystemManager(root, Microsoft.Extensions.Logging.Abstractions.NullLogger<FileSystemManager>.Instance);
                await Assert.ThrowsAnyAsync<InvalidDataException>(() => fresh.AcquireCanonicalWriteLeaseAsync());
                AssertConflictState();
            }
            else
            {
                var error = await Assert.ThrowsAsync<InvalidOperationException>(() =>
                    gate.ApplyReservedAsync(proposal, profile, task.SessionGeneration));
                Assert.Same(primary, error);
                Assert.Equal(before, File.ReadAllBytes(fs.ResolvePath(replaced)));
                Assert.False(File.Exists(fs.ResolvePath(added)));
                Assert.Empty(File.ReadAllBytes(fs.ResolvePath(deleted)));
                Assert.False(File.Exists(active));
                await fs.WriteFileAtomicAsync(later, "{}");
                Assert.Equal("{}", File.ReadAllText(fs.ResolvePath(later)));
            }
            Assert.Equal(1, validatorCalls);
            Assert.NotNull(pendingEvidence);
            Assert.Equal(generation, File.ReadAllBytes(fs.SessionGenerationPath));
            Assert.Equal(historyBytes, File.ReadAllBytes(fs.ResolvePath(history)));
            Assert.Equal(reservation, File.ReadAllBytes(reservationPath));
            Assert.False(File.Exists(fs.ActiveWorkerApplyTransactionJournalPath));

            void AssertConflictState()
            {
                // The last member blocks complete-set preflight before any earlier image is restored.
                Assert.Equal(after, File.ReadAllBytes(fs.ResolvePath(replaced)));
                Assert.Empty(File.ReadAllBytes(fs.ResolvePath(added)));
                Assert.Equal(foreign, File.ReadAllBytes(fs.ResolvePath(deleted)));
                Assert.Equal(pendingEvidence, File.ReadAllBytes(active));
            }
        }
        finally { CleanupTempRoot(root); }
    }
}
