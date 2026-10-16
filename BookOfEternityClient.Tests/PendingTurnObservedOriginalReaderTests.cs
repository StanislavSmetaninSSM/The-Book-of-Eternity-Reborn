using System.Reflection;
using System.Text.Json;
using BookOfEternityClient.Core;
using BookOfEternityClient.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;
using Xunit.Abstractions;

namespace BookOfEternityClient.Tests;

public sealed class PendingTurnObservedOriginalReaderTests(ITestOutputHelper output)
{
    private const string RequiredPath =
        "game_state/control/pending_mortal_wound_occurrences.json";

    [Fact]
    public async Task ReadObservedCurrent_IssuesEvidenceForTheActualOriginalAndCurrentLease()
    {
        var root = Path.Combine(Path.GetTempPath(),
            "boe-observed-original-reader-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            var files = new FileSystemManager(root, NullLogger<FileSystemManager>.Instance);
            files.EnsureDirectoryStructure();
            await files.WriteFileAtomicAsync(RequiredPath, "{\"schemaVersion\":1,\"occurrences\":[]}");
            var originalBytes = File.ReadAllBytes(files.ResolvePath(RequiredPath));
            await new LiveTurnPreparationService(files).PrepareAsync(new LiveTurnPreparationOptions
            {
                SessionId = "observed-original-session-71",
                RequestId = "observed-original-request-71",
                TurnNumber = 71,
                CurrentRealm = "Mortal World",
                PlayerAction = "Проверить исходное состояние перед действием.",
                PreGeneratedDices1d20 = [3, 17]
            });
            await files.WriteFileAtomicAsync(RequiredPath,
                "{\"schemaVersion\":1,\"occurrences\":[{}]}");

            await using var lease = await files.AcquireCanonicalWriteLeaseAsync();
            var generation = files.ReadLocalGenerationSnapshot(lease);
            Assert.NotNull(generation.Binding.Id);
            Assert.NotNull(generation.Bytes);
            var before = SnapshotTree(root);
            var existing = PendingTurnSnapshotReader.ReadCurrent(files, lease, [RequiredPath]);
            Assert.True(existing.Success, JsonSerializer.Serialize(existing.Issues));
            Assert.Empty(existing.Issues);
            var original = Assert.IsType<PendingTurnSnapshotReadAuthority>(existing.Snapshot);
            Assert.Equal("observed-original-session-71", original.SessionId);
            Assert.Equal("observed-original-request-71", original.RequestId);
            Assert.Equal(71, original.TurnNumber);
            Assert.Equal("mortal_world", original.Realm);
            Assert.Equal(new[] { 3, 17 }, original.AcceptedD20EventValues);
            Assert.Equal(originalBytes, original.ReadRequiredBytes(RequiredPath));
            Assert.False(originalBytes.SequenceEqual(File.ReadAllBytes(files.ResolvePath(RequiredPath))));
            AssertTreeEqual(before, SnapshotTree(root));
            output.WriteLine(JsonSerializer.Serialize(new
            {
                GenuineOriginalControl = true,
                original.SessionId, original.RequestId, original.TurnNumber, original.SnapshotToken,
                Generation = generation.Binding.Id,
                OriginalBytes = Convert.ToBase64String(originalBytes),
                LiveBytes = Convert.ToBase64String(File.ReadAllBytes(files.ResolvePath(RequiredPath))),
                ReadOnlyTree = true
            }));

            // Deliberate causal RED: genuine original/current/G/default-reader
            // preconditions above must succeed before the missing API assertion.
            var observedMethod = typeof(PendingTurnSnapshotReader).GetMethod(
                "ReadObservedCurrent", BindingFlags.Static | BindingFlags.NonPublic);
            Assert.NotNull(observedMethod);
            var observed = observedMethod!.Invoke(null,
                [files, lease, new[] { RequiredPath }]);
            Assert.NotNull(observed);
            T Property<T>(string name) => Assert.IsAssignableFrom<T>(
                observed!.GetType().GetProperty(name,
                    BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)!.GetValue(observed));
            Assert.True(Property<bool>("Success"));
            var snapshot = Property<PendingTurnSnapshotReadAuthority>("Snapshot");
            Assert.NotSame(original, snapshot);
            Assert.Equal(original.SnapshotToken, snapshot.SnapshotToken);
            Assert.Equal(originalBytes, snapshot.ReadRequiredBytes(RequiredPath));
            var evidence = Property<object>("Evidence");
            var requireCurrent = evidence.GetType().GetMethod("RequireCurrent",
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            Assert.NotNull(requireCurrent);
            requireCurrent!.Invoke(evidence, [files, lease, snapshot]);
            AssertTreeEqual(before, SnapshotTree(root));
        }
        finally
        {
            Exception? failure = null;
            try { if (Directory.Exists(root)) Directory.Delete(root, recursive: true); }
            catch (Exception error) { failure = error; }
            output.WriteLine(JsonSerializer.Serialize(new
            {
                CleanupOwnedRoot = root,
                OwnedFixtureRemoved = !Directory.Exists(root),
                CleanupFailure = failure?.ToString()
            }));
            Assert.Null(failure);
            Assert.False(Directory.Exists(root));
        }
    }

    private static Dictionary<string, byte[]> SnapshotTree(string root) =>
        Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories)
            .ToDictionary(path => Path.GetRelativePath(root, path), File.ReadAllBytes,
                StringComparer.Ordinal);

    private static void AssertTreeEqual(
        IReadOnlyDictionary<string, byte[]> expected, IReadOnlyDictionary<string, byte[]> actual)
    {
        Assert.Equal(expected.Keys.OrderBy(path => path, StringComparer.Ordinal),
            actual.Keys.OrderBy(path => path, StringComparer.Ordinal));
        foreach (var pair in expected)
            Assert.Equal(pair.Value, actual[pair.Key]);
    }
}
