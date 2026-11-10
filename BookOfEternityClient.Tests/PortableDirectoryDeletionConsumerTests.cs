using System.Text.Json;
using BookOfEternityClient.Core;
using BookOfEternityClient.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed class PortableDirectoryDeletionConsumerTests
{
    [Fact]
    public async Task LivePreparationPropagatesUncertainTreeOutcomeAndStopsBeforeDiceCleanup()
    {
        var root = Path.Combine(Path.GetTempPath(), "boe-tree-live-outcome-" + Guid.NewGuid().ToString("N"));
        const string snapshot = LiveTurnPreparationService.PendingTurnSnapshotDirectory;
        FileSystemManager? files = null; var treeDecision = false; var reached = false;
        byte[]? retainedJournal = null;
        try
        {
            files = new(root, NullLogger<FileSystemManager>.Instance, PhysicalLoadTransactionOperations.Instance,
                new FileSystemManagerHooks
                {
                    LocalPublicationObserver = (phase, index) =>
                    {
                        var active = Path.Combine(files!.RuntimeRootPath, "trusted-local-publication-v1", "active.json");
                        if (phase == TrustedLocalPublicationPhase.IntentPublished)
                        {
                            using var journal = JsonDocument.Parse(File.ReadAllBytes(active));
                            treeDecision = journal.RootElement.GetProperty("Members").EnumerateArray().Any(member =>
                                member.GetProperty("Path").GetString()!.StartsWith(files.ResolvePath(snapshot) + Path.DirectorySeparatorChar, StringComparison.Ordinal));
                        }
                        if (!treeDecision || phase != TrustedLocalPublicationPhase.MemberPublished || index != 0) return;
                        File.WriteAllBytes(files.ResolvePath(snapshot + "/z.bin"), [42]);
                        retainedJournal = File.ReadAllBytes(active);
                        reached = true; throw new CutFailure();
                    }
                });
            files.EnsureDirectoryStructure();
            Directory.CreateDirectory(Path.GetDirectoryName(files.SessionGenerationPath)!);
            File.WriteAllBytes(files.SessionGenerationPath, JsonSerializer.SerializeToUtf8Bytes(new { schemaVersion = 1, generationId = Guid.NewGuid().ToString("N") }));
            Directory.CreateDirectory(files.ResolvePath(snapshot));
            File.WriteAllBytes(files.ResolvePath(snapshot + "/a.bin"), [1]);
            File.WriteAllBytes(files.ResolvePath(snapshot + "/z.bin"), [2]);
            byte[] dice = [0xEF, 0xBB, 0xBF, 42];
            File.WriteAllBytes(files.ResolvePath(PendingTurnStateService.PendingDiceStatePath), dice);

            var failure = await Record.ExceptionAsync(() =>
                new LiveTurnPreparationService(files).PrepareAsync(new LiveTurnPreparationOptions { PlayerAction = "Осмотреться" }));

            Assert.True(reached);
            Assert.Equal(dice, File.ReadAllBytes(files.ResolvePath(PendingTurnStateService.PendingDiceStatePath)));
            Assert.False(File.Exists(files.ResolvePath(snapshot + "/a.bin")));
            Assert.Equal(new byte[] { 42 }, File.ReadAllBytes(files.ResolvePath(snapshot + "/z.bin")));
            var activeJournal = Path.Combine(files.RuntimeRootPath, "trusted-local-publication-v1", "active.json");
            Assert.NotNull(retainedJournal);
            Assert.Equal(retainedJournal, File.ReadAllBytes(activeJournal));
            Assert.False(File.Exists(files.ResolvePath(LiveTurnPreparationService.TurnRequestPath)));
            var coldFailure = await Assert.ThrowsAsync<InvalidDataException>(async () =>
            {
                var recovered = new FileSystemManager(root, NullLogger<FileSystemManager>.Instance);
                await using var lease = await recovered.AcquireCanonicalWriteLeaseAsync();
            });
            Assert.Equal("A publication member contains unknown bytes; evidence retained.", coldFailure.Message);
            Assert.Equal(retainedJournal, File.ReadAllBytes(activeJournal));
            Assert.IsType<CoordinatedStatePublicationUncertainException>(failure);
        }
        finally { if (Directory.Exists(root)) Directory.Delete(root, recursive: true); }
    }
    private sealed class CutFailure : Exception { }
}
