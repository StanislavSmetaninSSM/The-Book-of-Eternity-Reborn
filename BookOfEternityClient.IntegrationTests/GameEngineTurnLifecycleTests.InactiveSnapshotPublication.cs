using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using BookOfEternityClient.Core;
using BookOfEternityClient.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed partial class GameEngineTurnLifecycleTests
{
    [Theory]
    [InlineData("archive_rollback")]
    [InlineData("archive_race")]
    [InlineData("source_rollback")]
    [InlineData("source_unknown")]
    [InlineData("source_committed")]
    [InlineData("generation")]
    [InlineData("archive_drift")]
    [InlineData("source_drift")]
    [InlineData("source_added")]
    public async Task InactiveSnapshotEvidence_CurrentPublicationPreservesExactRemainingCohort(string boundary)
    {
        var fixture = await CreateInactiveSnapshotEvidenceFixtureAsync();
        await using var context = fixture.Context;
        var before = await ReadSpiritualEntryGuardFilesAsync(context);
        var sources = before.Keys.Where(IsInactiveSnapshotEvidenceSource).Order(StringComparer.Ordinal).ToArray();
        var payloads = sources.Where(p => p.StartsWith("game_state/control/pending_turn_snapshot/", StringComparison.Ordinal)).ToArray();
        Assert.True(payloads.Length >= 3, "Three distinct original payloads are required for earlier progress, selected deletion and remaining-source drift.");
        var first = payloads[0]; var second = payloads[1];
        const string manifest = "game_state/control/pending_turn_snapshot.json";
        var journalPath = Path.Combine(context.RootPath, ".boe_runtime/trusted-local-publication-v1/active.json");
        var foreign = Encoding.UTF8.GetBytes("actual-foreign-evidence-image");
        var sourcePublications = new List<string>();
        var cut = 0;
        string? changedPath = null; byte[]? pendingJournal = null;
        Dictionary<string, byte[]>? atLateBoundary = null;
        var generationBefore = File.ReadAllBytes(context.FileSystem.SessionGenerationPath);
        var hooks = new FileSystemManagerHooks {
            BeforeCanonicalMutationAsync = path => {
                if (boundary == "archive_race" && path.StartsWith("diagnostics/", StringComparison.Ordinal)) {
                    Assert.Equal(0, cut);
                    changedPath = path;
                    var fullPath = context.FileSystem.ResolvePath(path);
                    Assert.False(File.Exists(fullPath));
                    Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);
                    File.WriteAllBytes(fullPath, foreign);
                    cut++;
                }
                return Task.CompletedTask;
            },
            AfterCanonicalMutationBoundaryValidatedAsync = async path => {
                if (path != second || boundary is not ("generation" or "archive_drift" or "source_drift" or "source_added")) return;
                Assert.Equal(new[] { first }, sourcePublications);
                Assert.False(File.Exists(context.FileSystem.ResolvePath(first)));
                atLateBoundary = await ReadSpiritualEntryGuardFilesAsync(context);
                AssertInactiveSnapshotEvidenceArchive(before, atLateBoundary);
                cut++;
                if (boundary == "generation") {
                    File.WriteAllBytes(context.FileSystem.SessionGenerationPath, JsonSerializer.SerializeToUtf8Bytes(
                        new { SchemaVersion = 1, GenerationId = Guid.NewGuid().ToString("N") }));
                    return;
                }
                if (boundary == "archive_drift") {
                    var index = Assert.Single(atLateBoundary, p => p.Key.StartsWith("diagnostics/", StringComparison.Ordinal) && Path.GetFileName(p.Key) == "index.json");
                    var entries = JsonNode.Parse(index.Value)!["entries"]!.AsArray();
                    // Corrupt the archive of an ALREADY removed source, not merely
                    // a copy needed by the next/remaining deletion.
                    var entry = Assert.Single(entries, e => e!["sourcePath"]!.GetValue<string>() == first)!;
                    changedPath = Path.Combine(Path.GetDirectoryName(index.Key)!, entry["blob"]!.GetValue<string>());
                } else changedPath = boundary == "source_drift" ? payloads[^1] : "game_state/control/pending_turn_snapshot/late-foreign.bin";
                if (boundary == "source_added") Assert.False(File.Exists(context.FileSystem.ResolvePath(changedPath)));
                File.WriteAllBytes(context.FileSystem.ResolvePath(changedPath), foreign);
            },
            LocalPublicationObserver = (phase, index) => {
                if (phase is not (TrustedLocalPublicationPhase.MemberPublished or TrustedLocalPublicationPhase.Committed)) return;
                using var journal = JsonDocument.Parse(File.ReadAllBytes(journalPath));
                var members = journal.RootElement.GetProperty("Members");
                if (members.GetArrayLength() != 1) return;
                var path = members[0].GetProperty("Path").GetString()!;
                var relative = Path.GetRelativePath(context.FileSystem.GameSessionPath, path);
                if (phase == TrustedLocalPublicationPhase.MemberPublished && sources.Contains(relative, StringComparer.Ordinal)) {
                    Assert.Equal(0, index); Assert.False(File.Exists(path));
                    sourcePublications.Add(relative);
                }
                if (boundary == "archive_rollback" && phase == TrustedLocalPublicationPhase.MemberPublished && relative.StartsWith("diagnostics/", StringComparison.Ordinal)) {
                    Assert.Equal(0, index); Assert.True(File.Exists(path)); cut++;
                    throw new InvalidOperationException("Actual archive member publication rollback.");
                }
                if (boundary is "source_rollback" or "source_unknown" && phase == TrustedLocalPublicationPhase.MemberPublished && relative == second) {
                    Assert.Equal(new[] { first, second }, sourcePublications);
                    cut++; pendingJournal = File.ReadAllBytes(journalPath);
                    if (boundary == "source_unknown") File.WriteAllBytes(path, foreign);
                    throw new InvalidOperationException("Actual selected source deletion publication cut.");
                }
                if (boundary == "source_committed" && phase == TrustedLocalPublicationPhase.Committed && relative == manifest) {
                    Assert.True(journal.RootElement.GetProperty("Committed").GetBoolean());
                    Assert.False(File.Exists(path)); cut++; pendingJournal = File.ReadAllBytes(journalPath);
                    throw new InvalidOperationException("Actual last manifest committed cleanup debt.");
                }
            }
        };
        var files = new FileSystemManager(context.RootPath, NullLogger<FileSystemManager>.Instance, PhysicalLoadTransactionOperations.Instance, hooks);
        var engine = CreateGameEngine(new QueuedConsoleInputSource([]), fileSystem: files);
        GetPrivateField<GameLoop>(engine, "_gameLoop").SetSession("session_entry_guard", 42);
        Exception? failure = null; object? result = null;
        try { result = await InvokePrivateTaskResultAsync(engine, "ArchiveInactivePendingSnapshotEvidenceAsync"); }
        catch (Exception error) { failure = error; }
        Assert.True(cut == 1, $"Selected actual cut not reached exactly once: {cut}; failure={failure}");
        var after = await ReadSpiritualEntryGuardFilesAsync(context);
        if (boundary == "source_unknown") {
            Assert.Equal(new[] { first, second }, sourcePublications);
            Assert.False(after.ContainsKey(first)); Assert.Equal(foreign, after[second]);
            Assert.Equal(pendingJournal, File.ReadAllBytes(journalPath));
            foreach (var path in sources.Where(p => p != first && p != second)) Assert.Equal(before[path], after[path]);
            AssertInactiveSnapshotEvidenceArchive(before, after);
            Assert.IsType<CoordinatedStatePublicationUncertainException>(failure);
            var cold = new FileSystemManager(context.RootPath, NullLogger<FileSystemManager>.Instance);
            await Assert.ThrowsAsync<InvalidDataException>(async () => { await using var lease = await cold.AcquireCanonicalWriteLeaseAsync(); });
            Assert.Equal(pendingJournal, File.ReadAllBytes(journalPath));
            return;
        }
        if (boundary == "generation") {
            Assert.IsType<SessionReplacedException>(failure);
            Assert.NotEqual(generationBefore, File.ReadAllBytes(files.SessionGenerationPath));
            Assert.Equal(new[] { first }, sourcePublications);
            await AssertSpiritualEntryGuardFilesAsync(context, atLateBoundary!);
            Assert.False(File.Exists(journalPath));
            return;
        }
        Assert.Null(failure);
        Assert.Equal(boundary == "source_committed", Assert.IsType<bool>(result));
        if (boundary == "source_committed") {
            Assert.Equal(sources.Length, sourcePublications.Count);
            Assert.DoesNotContain(after.Keys, IsInactiveSnapshotEvidenceSource);
            AssertInactiveSnapshotEvidenceArchive(before, after);
            Assert.Equal(pendingJournal, File.ReadAllBytes(journalPath));
            var cold = new FileSystemManager(context.RootPath, NullLogger<FileSystemManager>.Instance);
            await using (var lease = await cold.AcquireCanonicalWriteLeaseAsync()) { }
            Assert.False(File.Exists(journalPath));
            await AssertSpiritualEntryGuardFilesAsync(context, after);
        } else if (boundary is "archive_drift" or "source_drift" or "source_added") {
            Assert.Equal(new[] { first }, sourcePublications);
            atLateBoundary![changedPath!] = foreign;
            await AssertSpiritualEntryGuardFilesAsync(context, atLateBoundary);
            Assert.False(File.Exists(journalPath));
        } else if (boundary == "source_rollback") {
            Assert.Equal(new[] { first, second }, sourcePublications);
            Assert.False(after.ContainsKey(first));
            foreach (var pair in before.Where(p => p.Key != first)) Assert.Equal(pair.Value, after[pair.Key]);
            AssertInactiveSnapshotEvidenceArchive(before, after); Assert.False(File.Exists(journalPath));
        } else {
            Assert.Empty(sourcePublications);
            foreach (var pair in before) Assert.Equal(pair.Value, after[pair.Key]);
            if (boundary == "archive_race") { Assert.Equal(foreign, after[changedPath!]); }
            else Assert.DoesNotContain(after.Keys, p => p.StartsWith("diagnostics/", StringComparison.Ordinal));
            Assert.False(File.Exists(journalPath));
        }
    }

    [Fact]
    public async Task InactiveSnapshotEvidence_OriginalRuntimeNormalizationReachesCurrentRetirement()
    {
        var fixture = await CreateInactiveSnapshotEvidenceFixtureAsync();
        await using var context = fixture.Context;
        var before = await ReadSpiritualEntryGuardFilesAsync(context);
        var sourcePaths = before.Keys.Where(IsInactiveSnapshotEvidenceSource).ToHashSet(StringComparer.Ordinal);
        var sourcePublications = 0;
        var journalPath = Path.Combine(context.RootPath, ".boe_runtime/trusted-local-publication-v1/active.json");
        var files = new FileSystemManager(context.RootPath, NullLogger<FileSystemManager>.Instance, PhysicalLoadTransactionOperations.Instance,
            new FileSystemManagerHooks { LocalPublicationObserver = (phase, index) => {
                if (phase != TrustedLocalPublicationPhase.MemberPublished) return;
                using var journal = JsonDocument.Parse(File.ReadAllBytes(journalPath));
                var members = journal.RootElement.GetProperty("Members");
                var path = members[index].GetProperty("Path").GetString()!;
                if (!sourcePaths.Contains(Path.GetRelativePath(context.FileSystem.GameSessionPath, path))) return;
                Assert.Equal(0, index); Assert.Single(members.EnumerateArray());
                Assert.False(File.Exists(path)); sourcePublications++;
            }});
        var engine = CreateGameEngine(new QueuedConsoleInputSource([]), fileSystem: files);
        GetPrivateField<GameLoop>(engine, "_gameLoop").SetSession("session_entry_guard", 42);
        await InvokePrivateTaskResultAsync(engine, "NormalizeRuntimeUiArtifactsAsync");
        Assert.Equal(sourcePaths.Count, sourcePublications);
        var after = await ReadSpiritualEntryGuardFilesAsync(context);
        AssertInactiveSnapshotEvidenceArchive(before, after);
        Assert.DoesNotContain(after.Keys, IsInactiveSnapshotEvidenceSource);
        Assert.False(File.Exists(journalPath));
    }
}
