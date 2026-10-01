using System.Text.Json;
using BookOfEternityClient.Core;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed class PortableLegacyGenerationTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "boe-legacy-generation-" + Guid.NewGuid().ToString("N"));

    [Fact]
    public async Task CommitLoadTransaction_UsesOriginalGenerationReadBoundaryBeforeCommittingJournal()
    {
        Directory.CreateDirectory(_root);
        var generationPath = Path.Combine(_root, ".boe_runtime", "session-generation", "current.json");
        var armed = false;
        var generationReads = 0;
        var files = new FileSystemManager(_root, NullLogger<FileSystemManager>.Instance,
            PhysicalLoadTransactionOperations.Instance, new FileSystemManagerHooks
            {
                BeforeRuntimeFileReadOpenAsync = path =>
                {
                    if (armed && path == generationPath)
                    {
                        generationReads++;
                        throw new IOException("Owned original generation read fault.");
                    }
                    return Task.CompletedTask;
                }
            });
        files.EnsureDirectoryStructure();
        string generation;
        await using (var setupLease = await files.AcquireCanonicalWriteLeaseAsync())
            generation = files.GetOrCreateSessionGeneration(setupLease);
        var generationBytes = File.ReadAllBytes(generationPath);

        await using var lifecycleLease = await files.AcquireSessionLifecycleLeaseAsync();
        await using var replacementLease = await files.AcquireSessionReplacementWriteLeaseAsync(lifecycleLease);
        var transactionId = Guid.NewGuid().ToString("N");
        var journalBytes = JsonSerializer.SerializeToUtf8Bytes(new
        {
            SchemaVersion = 2, TransactionId = transactionId, Committed = false,
            PreviousGenerationId = generation, ReplacementGenerationId = generation
        });
        // Seed current-schema evidence only after admission under this owned
        // lease. We test the read gate before any legacy replacement publication.
        Directory.CreateDirectory(Path.GetDirectoryName(files.ActiveLoadTransactionJournalPath)!);
        File.WriteAllBytes(files.ActiveLoadTransactionJournalPath, journalBytes);
        armed = true;

        var failure = Record.Exception(() => files.CommitLoadTransaction(replacementLease, transactionId));
        Assert.True(generationReads == 1, "The original generation read boundary was bypassed. Observed: " + failure);
        Assert.Equal("Owned original generation read fault.", Assert.IsType<IOException>(failure).Message);
        Assert.Equal(journalBytes, File.ReadAllBytes(files.ActiveLoadTransactionJournalPath));
        Assert.Equal(generationBytes, File.ReadAllBytes(generationPath));
    }

    public void Dispose() { if (Directory.Exists(_root)) Directory.Delete(_root, true); }
}
