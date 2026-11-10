using System.Security.Cryptography;
using System.Text.Json;
using BookOfEternityClient.Core;
using Xunit;

namespace BookOfEternityClient.Tests;

// Observe the real current publisher after its selected member reached disk.
// Unlike uncertainty fixtures, do not introduce foreign bytes or replace its decision.
internal sealed class KnownRollbackPublicationCut(string selectedPath)
{
    private FileSystemManager? _files;
    internal InvalidOperationException Failure { get; } = new("actual known-rollback MemberPublished cut");
    internal int Cuts { get; private set; }
    internal int Index { get; private set; } = -1;
    internal byte[]? JournalAtCut { get; private set; }
    internal byte[]? PublishedBytes { get; private set; }
    internal string? TransactionId { get; private set; }
    internal string JournalPath => Path.Combine(_files!.RuntimeRootPath, "trusted-local-publication-v1", "active.json");
    internal FileSystemManagerHooks Hooks => new() { LocalPublicationObserver = Observe };
    internal void Attach(FileSystemManager files) => _files = files;

    private void Observe(TrustedLocalPublicationPhase phase, int index)
    {
        if (phase != TrustedLocalPublicationPhase.MemberPublished || Cuts != 0) return;
        var bytes = File.ReadAllBytes(JournalPath);
        using var parsed = CleanupPublicationCut.Metadata(bytes);
        var root = parsed.RootElement;
        var member = root.GetProperty("Members")[index];
        var target = member.GetProperty("Path").GetString()!;
        if (!string.Equals(target, selectedPath,
                OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal)) return;
        Assert.False(root.GetProperty("Committed").GetBoolean());
        JournalAtCut = bytes;
        Index = index;
        TransactionId = root.GetProperty("TransactionId").GetString();
        PublishedBytes = CleanupPublicationCut.ReadOptional(target);
        var after = member.GetProperty("After");
        Assert.Equal(after.GetProperty("Exists").GetBoolean(), PublishedBytes != null);
        Assert.Equal(after.GetProperty("Sha256").GetString(),
            PublishedBytes == null ? null : Convert.ToHexString(SHA256.HashData(PublishedBytes)));
        Cuts++;
        throw Failure;
    }

    internal void AssertRestored(IReadOnlyDictionary<string, byte[]?> before,
        byte[]? generationBefore, Exception? outwardFailure, Action<string> output)
    {
        // Read raw images before a facade call could trigger recovery and mask an unfinished rollback.
        var after = before.ToDictionary(pair => pair.Key,
            pair => CleanupPublicationCut.ReadOptional(_files!.ResolvePath(pair.Key)), StringComparer.Ordinal);
        var generationAfter = CleanupPublicationCut.ReadOptional(_files!.SessionGenerationPath);
        var journalAfter = CleanupPublicationCut.ReadOptional(JournalPath);
        output(JsonSerializer.Serialize(new
        {
            kind = "known-rollback-original-publication", selectedPath, Cuts, Index, TransactionId,
            JournalAtCut, JournalSha256 = JournalAtCut == null ? null : Convert.ToHexString(SHA256.HashData(JournalAtCut)),
            PublishedBytes, before, after, generationBefore, generationAfter, journalAfter,
            injectedFailure = Failure.ToString(), outwardFailure = outwardFailure?.ToString()
        }));
        Assert.Equal(1, Cuts);
        Assert.NotNull(TransactionId);
        Assert.NotNull(JournalAtCut);
        Assert.Null(journalAfter);
        Assert.Equal(generationBefore, generationAfter);
        foreach (var (path, expected) in before) Assert.Equal(expected, after[path]);
        var selectedBefore = before.Single(pair => string.Equals(_files.ResolvePath(pair.Key), selectedPath,
            OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal)).Value;
        Assert.False(selectedBefore == null ? PublishedBytes == null :
            PublishedBytes != null && selectedBefore.AsSpan().SequenceEqual(PublishedBytes),
            "The selected real publication must change its tracked before image.");
    }
}
