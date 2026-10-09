using System.Security.Cryptography;
using System.Text.Json;
using BookOfEternityClient.Core;
using Xunit;

namespace BookOfEternityClient.Tests;

// Observe actual local journals without acquiring another lease or changing their outcome.
internal sealed class SpiritualLifecyclePublicationWitness(FileSystemManager files, Action<string> output,
    params string[] paths)
{
    private readonly Dictionary<string, string> _paths = paths.ToDictionary(files.ResolvePath, path => path,
        OperatingSystem.IsWindows() ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal);
    private readonly List<Publication> _events = [];
    private readonly byte[]? _generation = CleanupPublicationCut.ReadOptional(files.SessionGenerationPath);
    internal string JournalPath => Path.Combine(files.RuntimeRootPath, "trusted-local-publication-v1", "active.json");

    internal IReadOnlyList<Publication> Observe(TrustedLocalPublicationPhase phase, int index)
    {
        if (phase is not (TrustedLocalPublicationPhase.Committed or TrustedLocalPublicationPhase.MemberPublished))
            return [];
        var bytes = File.ReadAllBytes(JournalPath);
        using var parsed = CleanupPublicationCut.Metadata(bytes);
        var root = parsed.RootElement;
        if (phase == TrustedLocalPublicationPhase.Committed) Assert.True(root.GetProperty("Committed").GetBoolean());
        var members = root.GetProperty("Members").EnumerateArray().ToArray();
        var selected = phase == TrustedLocalPublicationPhase.MemberPublished ? new[] { members[index] } : members;
        var observed = new List<Publication>();
        foreach (var member in selected)
        {
            var absolute = member.GetProperty("Path").GetString()!;
            if (!_paths.TryGetValue(absolute, out var relative)) continue;
            var current = CleanupPublicationCut.ReadOptional(absolute);
            var after = member.GetProperty("After");
            Assert.Equal(after.GetProperty("Exists").GetBoolean(), current != null);
            Assert.Equal(after.GetProperty("Sha256").GetString(), current == null ? null :
                Convert.ToHexString(SHA256.HashData(current)));
            var value = new Publication(relative, phase.ToString(), root.GetProperty("TransactionId").GetString()!, bytes, current);
            _events.Add(value);
            observed.Add(value);
        }
        return observed;
    }

    internal byte[] RequireLatestCurrent(string path)
    {
        var latest = _events.LastOrDefault(value => value.Path == path && value.Phase == "Committed");
        Assert.NotNull(latest);
        Assert.Equal(latest.Bytes, CleanupPublicationCut.ReadOptional(files.ResolvePath(path)));
        return Assert.IsType<byte[]>(latest.Bytes);
    }

    internal void AssertSettled(string boundary)
    {
        var generation = CleanupPublicationCut.ReadOptional(files.SessionGenerationPath);
        var journal = CleanupPublicationCut.ReadOptional(JournalPath);
        output(JsonSerializer.Serialize(new { kind = "spiritual-lifecycle-publication", boundary,
            root = files.BasePath, beforeGeneration = _generation, generation, journal, events = _events }));
        Assert.Equal(_generation, generation);
        Assert.Null(journal);
    }

    internal sealed record Publication(string Path, string Phase, string TransactionId, byte[] Journal, byte[]? Bytes);
}
