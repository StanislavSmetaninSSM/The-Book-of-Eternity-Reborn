using System.Text.Json;
using BookOfEternityClient.Core;
using BookOfEternityClient.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed class PortableDirectoryDeletionClosingTests
{
    [Fact]
    public async Task ActualTreeDecisionSurvivesClosingGenerationReplacement()
    {
        var root = Path.Combine(Path.GetTempPath(), "boe-tree-closing-" + Guid.NewGuid().ToString("N"));
        const string tree = "game_state/control/tree_closing";
        var generation = Guid.NewGuid().ToString("N");
        var replacementGeneration = Guid.NewGuid().ToString("N");
        var reached = 0;
        var closing = 0;
        FileSystemManager? files = null;
        Exception? originalDecision = null;
        byte[]? retainedJournal = null;
        try
        {
            files = new(root, NullLogger<FileSystemManager>.Instance, PhysicalLoadTransactionOperations.Instance,
                new FileSystemManagerHooks
                {
                    LocalPublicationObserver = (phase, index) =>
                    {
                        if (phase != TrustedLocalPublicationPhase.MemberPublished || index != 0) return;
                        var journalPath = Path.Combine(files!.RuntimeRootPath, "trusted-local-publication-v1", "active.json");
                        using var journal = JsonDocument.Parse(File.ReadAllBytes(journalPath));
                        var member = journal.RootElement.GetProperty("Members")[index];
                        Assert.Equal(files.ResolvePath(tree + "/a.bin"), member.GetProperty("Path").GetString());
                        Assert.False(member.GetProperty("After").GetProperty("Exists").GetBoolean());
                        Assert.False(File.Exists(files.ResolvePath(tree + "/a.bin")));
                        reached++;
                        File.WriteAllBytes(files.ResolvePath(tree + "/z.bin"), [42]);
                        retainedJournal = File.ReadAllBytes(journalPath);
                        throw new InvalidOperationException("tree closing publication cut");
                    },
                    SessionOperationClosingAsync = () =>
                    {
                        closing++;
                        File.WriteAllBytes(files!.SessionGenerationPath,
                            JsonSerializer.SerializeToUtf8Bytes(new { schemaVersion = 1, generationId = replacementGeneration }));
                        return Task.CompletedTask;
                    }
                });
            files.EnsureDirectoryStructure();
            Directory.CreateDirectory(Path.GetDirectoryName(files.SessionGenerationPath)!);
            File.WriteAllBytes(files.SessionGenerationPath,
                JsonSerializer.SerializeToUtf8Bytes(new { schemaVersion = 1, generationId = generation }));
            Directory.CreateDirectory(files.ResolvePath(tree));
            File.WriteAllBytes(files.ResolvePath(tree + "/a.bin"), [1]);
            File.WriteAllBytes(files.ResolvePath(tree + "/z.bin"), [2]);
            var failure = await Record.ExceptionAsync(() => SessionOperationContext.RunBoundAsync(files, generation, async () =>
            {
                await using var lease = await files.AcquireCanonicalWriteLeaseAsync();
                try { files.DeleteDirectoryTree(lease, tree); }
                catch (Exception decision) { originalDecision = decision; throw; }
            }));
            Assert.Equal(1, reached);
            Assert.Equal(1, closing);
            Assert.NotNull(originalDecision);
            Assert.False(File.Exists(files.ResolvePath(tree + "/a.bin")));
            Assert.Equal(new byte[] { 42 }, File.ReadAllBytes(files.ResolvePath(tree + "/z.bin")));
            Assert.Equal(retainedJournal, File.ReadAllBytes(Path.Combine(files.RuntimeRootPath, "trusted-local-publication-v1", "active.json")));
            var replacement = Assert.IsType<SessionReplacedException>(failure);
            Assert.Equal(generation, replacement.ExpectedGeneration);
            Assert.Same(originalDecision, replacement.Data["SessionOperationFailure"]);
            Assert.IsType<CoordinatedStatePublicationUncertainException>(originalDecision);
        }
        finally { if (Directory.Exists(root)) Directory.Delete(root, recursive: true); }
    }
}
