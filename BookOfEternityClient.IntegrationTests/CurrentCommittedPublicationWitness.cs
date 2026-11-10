using System.Security.Cryptography;
using System.Text.Json;
using BookOfEternityClient.Core;
using Xunit;

namespace BookOfEternityClient.Tests;

// Raw evidence from the real publisher; never creates a lease or changes publication state.
internal sealed class CurrentCommittedPublicationWitness
{
    private readonly FileSystemManager _files;
    private readonly Dictionary<string, string> _paths;
    private readonly List<Publication> _events = [];

    internal CurrentCommittedPublicationWitness(FileSystemManager files, params string[] paths)
    {
        _files = files;
        _paths = paths.ToDictionary(files.ResolvePath, path => path,
            OperatingSystem.IsWindows() ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal);
        GenerationBefore = CleanupPublicationCut.ReadOptional(files.SessionGenerationPath);
    }

    internal byte[]? GenerationBefore { get; }
    internal string JournalPath => Path.Combine(_files.RuntimeRootPath, "trusted-local-publication-v1", "active.json");
    internal bool Has(string path) => _events.Any(value => value.Path == path);

    internal void Observe(TrustedLocalPublicationPhase phase, int index)
    {
        if (phase != TrustedLocalPublicationPhase.Committed) return;
        var journal = File.ReadAllBytes(JournalPath);
        using var parsed = CleanupPublicationCut.Metadata(journal);
        var root = parsed.RootElement;
        Assert.True(root.GetProperty("Committed").GetBoolean());
        foreach (var member in root.GetProperty("Members").EnumerateArray())
        {
            var absolute = member.GetProperty("Path").GetString()!;
            if (!_paths.TryGetValue(absolute, out var relative)) continue;
            var bytes = CleanupPublicationCut.ReadOptional(absolute);
            var after = member.GetProperty("After");
            Assert.Equal(after.GetProperty("Exists").GetBoolean(), bytes != null);
            Assert.Equal(after.GetProperty("Sha256").GetString(),
                bytes == null ? null : Convert.ToHexString(SHA256.HashData(bytes)));
            _events.Add(new(relative, root.GetProperty("TransactionId").GetString()!, journal, bytes));
        }
    }

    internal byte[]? RequireSingleCurrent(string path)
    {
        var value = Assert.Single(_events, value => value.Path == path);
        Assert.Equal(value.Bytes, CleanupPublicationCut.ReadOptional(_files.ResolvePath(path)));
        return value.Bytes;
    }

    internal void AssertRestored(string kind, IReadOnlyDictionary<string, byte[]?> absoluteBefore,
        Exception? failure, Action<string> output)
    {
        var after = absoluteBefore.ToDictionary(pair => pair.Key,
            pair => CleanupPublicationCut.ReadOptional(pair.Key), StringComparer.Ordinal);
        var generationAfter = CleanupPublicationCut.ReadOptional(_files.SessionGenerationPath);
        var journalAfter = CleanupPublicationCut.ReadOptional(JournalPath);
        output(JsonSerializer.Serialize(new { kind, events = _events, before = absoluteBefore, after,
            GenerationBefore, generationAfter, journalAfter, failure = failure?.ToString() }));
        Assert.Equal(GenerationBefore, generationAfter);
        Assert.Null(journalAfter);
        foreach (var pair in absoluteBefore) Assert.Equal(pair.Value, after[pair.Key]);
    }

    private sealed record Publication(string Path, string TransactionId, byte[] Journal, byte[]? Bytes);
}

// Dispose the original fixture (including its current lease) before inspecting root cleanup.
internal sealed class OriginalFixtureCompletion(string root, Action dispose, Action<string> output) : IDisposable
{
    public void Dispose()
    {
        Exception? failure = null;
        try { dispose(); }
        catch (Exception error) { failure = error; }
        output(JsonSerializer.Serialize(new { CleanupOwnedRoot = root,
            OwnedFixtureRemoved = !Directory.Exists(root), CleanupFailure = failure?.ToString() }));
        Assert.Null(failure);
        Assert.False(Directory.Exists(root));
    }
}
