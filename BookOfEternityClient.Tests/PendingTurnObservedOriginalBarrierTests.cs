using System.Reflection;
using System.Runtime.ExceptionServices;
using System.Text.Json;
using BookOfEternityClient.Core;
using BookOfEternityClient.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;
using Xunit.Abstractions;

namespace BookOfEternityClient.Tests;

public sealed class PendingTurnObservedOriginalBarrierTests(ITestOutputHelper output)
{
    private const string FirstPath = "game_state/control/pending_mortal_wound_occurrences.json";
    private const string SecondPath = "game_state/core/observed_original_control.json";
    private static string[] RequiredPaths => [FirstPath, SecondPath];

    [Fact]
    public async Task ReadCurrent_CharacterizesCapturedOriginalBytesAfterLaterSourceDrift()
    {
        var observation = await ObserveLateDriftAsync(useObservedApi: false);
        Assert.True(observation.Result.Success);
        Assert.Equal("ReadCurrent", observation.Result.InvokedApi);
        Assert.Equal(observation.OriginalFirst,
            Assert.IsType<PendingTurnSnapshotReadAuthority>(observation.Result.Snapshot)
                .ReadRequiredBytes(FirstPath));
    }

    [Fact]
    public async Task ReadObservedCurrent_MustRejectAlreadyReadOriginalDriftBeforeIssuingAuthority()
    {
        var observation = await ObserveLateDriftAsync(useObservedApi: true);
        var result = observation.Result;
        // On absent-feature source this deliberately runs the real old behavior.
        // Its Success=true, after the proven late mutation, is the causal RED.
        Assert.False(result.Success,
            "A completed original read must not issue authority after B's post-open hook changed already-read A.");
        Assert.Null(result.Snapshot);
        Assert.Null(result.Evidence);
        Assert.Contains(result.Issues, issue =>
            issue.Code == "pending_turn_snapshot_reader_source_changed" && issue.FilePath == FirstPath);
        // A legacy fallback can NEVER qualify GREEN, even if default semantics
        // were accidentally strengthened. The characterization above protects them.
        Assert.True(result.ApiPresent, "GREEN requires the genuine observed-reader API.");
        Assert.Equal("ReadObservedCurrent", result.InvokedApi);
    }

    private async Task<DriftObservation> ObserveLateDriftAsync(bool useObservedApi)
    {
        var root = Path.Combine(Path.GetTempPath(),
            "boe-observed-original-barrier-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            string? physicalFirst = null, physicalSecond = null, fullFirst = null;
            byte[]? changedFirst = null;
            var armed = false;
            var injected = 0;
            var firstOpens = 0;
            var secondOpens = 0;
            var order = new List<string>();
            var hooks = new FileSystemManagerHooks
            {
                AfterCanonicalReadInitialValidationAsync = path =>
                {
                    if (!armed) return Task.CompletedTask;
                    if (path == physicalFirst) { firstOpens++; order.Add("A"); }
                    if (path == physicalSecond)
                    {
                        secondOpens++;
                        order.Add("B");
                        if (injected == 0)
                        {
                            Assert.True(firstOpens > 0, "The real reader must finish A before opening B.");
                            injected++;
                            // Existing physical fault injection only. An atomic
                            // writer here would attempt another canonical lease.
                            File.WriteAllBytes(fullFirst!, changedFirst!);
                        }
                    }
                    return Task.CompletedTask;
                }
            };
            var files = new FileSystemManager(root, NullLogger<FileSystemManager>.Instance,
                PhysicalLoadTransactionOperations.Instance, hooks);
            files.EnsureDirectoryStructure();
            await files.WriteFileAtomicAsync(FirstPath, "{\"schemaVersion\":1,\"occurrences\":[]}");
            await files.WriteFileAtomicAsync(SecondPath, "{\"value\":17}");
            var originalFirst = File.ReadAllBytes(files.ResolvePath(FirstPath));
            var originalSecond = File.ReadAllBytes(files.ResolvePath(SecondPath));
            await new LiveTurnPreparationService(files).PrepareAsync(new LiveTurnPreparationOptions
            {
                SessionId = "observed-barrier-session-72",
                RequestId = "observed-barrier-request-72",
                TurnNumber = 72,
                CurrentRealm = "Mortal World",
                PlayerAction = "Проверить неизменность исходного состояния.",
                PreGeneratedDices1d20 = [4, 16]
            });
            var manifest = JsonSerializer.Deserialize<LiveTurnPendingSnapshotManifest>(
                File.ReadAllText(files.ResolvePath(LiveTurnPreparationService.PendingTurnSnapshotManifestPath)),
                LiveTurnPreparationService.ManifestJsonOptions)!;
            physicalFirst = manifest.Files[FirstPath];
            physicalSecond = manifest.Files[SecondPath];
            fullFirst = files.ResolvePath(physicalFirst);
            Assert.NotEqual(physicalFirst, physicalSecond);
            changedFirst = originalFirst.Concat(new byte[] { 0x20 }).ToArray();

            var before = SnapshotTree(root);
            LocalSessionGenerationSnapshot generation;
            await using (var baselineLease = await files.AcquireCanonicalWriteLeaseAsync())
            {
                generation = files.ReadLocalGenerationSnapshot(baselineLease);
                Assert.NotNull(generation.Binding.Id);
                Assert.NotNull(generation.Bytes);
                var baseline = PendingTurnSnapshotReader.ReadCurrent(files, baselineLease, RequiredPaths);
                Assert.True(baseline.Success, JsonSerializer.Serialize(baseline.Issues));
                Assert.Empty(baseline.Issues);
                var snapshot = Assert.IsType<PendingTurnSnapshotReadAuthority>(baseline.Snapshot);
                Assert.Equal(manifest.ManifestPayloadHash, snapshot.SnapshotToken);
                Assert.Equal("observed-barrier-session-72", snapshot.SessionId);
                Assert.Equal("observed-barrier-request-72", snapshot.RequestId);
                Assert.Equal(72, snapshot.TurnNumber);
                Assert.Equal(originalFirst, snapshot.ReadRequiredBytes(FirstPath));
                Assert.Equal(originalSecond, snapshot.ReadRequiredBytes(SecondPath));
            }
            AssertTreeEqual(before, SnapshotTree(root));

            ReaderInvocation result;
            await using (var lease = await files.AcquireCanonicalWriteLeaseAsync())
            {
                AssertGeneration(files.ReadLocalGenerationSnapshot(lease));
                armed = true;
                result = InvokeReader(files, lease, useObservedApi);
                armed = false;
                AssertGeneration(files.ReadLocalGenerationSnapshot(lease));
            }
            Assert.Equal(1, injected);
            Assert.True(firstOpens > 0);
            Assert.True(secondOpens > 0);
            Assert.Equal(new[] { "A", "B" }, order.Take(2));
            Assert.Equal(changedFirst, File.ReadAllBytes(files.ResolvePath(physicalFirst)));
            Assert.Equal(originalSecond, File.ReadAllBytes(files.ResolvePath(physicalSecond)));
            var expected = before.ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.Ordinal);
            expected[Path.GetRelativePath(root, files.ResolvePath(physicalFirst))] = changedFirst;
            AssertTreeEqual(expected, SnapshotTree(root));
            output.WriteLine(JsonSerializer.Serialize(new
            {
                GenuineBarrierControl = true,
                result.ApiPresent, result.InvokedApi, result.Success,
                SnapshotReturned = result.Snapshot is not null,
                EvidenceReturned = result.Evidence is not null,
                FirstLogicalPath = FirstPath, SecondLogicalPath = SecondPath,
                FirstPhysicalPath = physicalFirst, SecondPhysicalPath = physicalSecond,
                OriginalFirst = Convert.ToBase64String(originalFirst),
                ChangedFirst = Convert.ToBase64String(changedFirst),
                OriginalSecond = Convert.ToBase64String(originalSecond),
                Generation = generation.Binding.Id, manifest.ManifestPayloadHash,
                InjectionCount = injected, ReadOrder = order,
                OnlyFirstSnapshotChanged = true, SameGeneration = true
            }));
            return new DriftObservation(result, originalFirst);

            void AssertGeneration(LocalSessionGenerationSnapshot current)
            {
                Assert.Equal(generation.Binding.Id, current.Binding.Id);
                Assert.Equal(generation.Bytes, current.Bytes);
            }
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

    private static ReaderInvocation InvokeReader(FileSystemManager files,
        FileSystemManager.CanonicalWriteLease lease, bool useObservedApi)
    {
        if (useObservedApi)
        {
            var named = typeof(PendingTurnSnapshotReader).GetMethods(
                    BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic)
                .Where(method => method.Name == "ReadObservedCurrent").ToArray();
            if (named.Length > 0)
            {
                var method = Assert.Single(named);
                Assert.Equal(new[] { typeof(FileSystemManager), typeof(FileSystemManager.CanonicalWriteLease),
                    typeof(IReadOnlyCollection<string>) }, method.GetParameters().Select(parameter => parameter.ParameterType));
                object value;
                try { value = method.Invoke(null, [files, lease, RequiredPaths])!; }
                catch (TargetInvocationException error) when (error.InnerException is not null)
                {
                    ExceptionDispatchInfo.Capture(error.InnerException).Throw();
                    throw;
                }
                Assert.NotNull(value);
                object? Property(string name) => value.GetType().GetProperty(name,
                    BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)!.GetValue(value);
                return new ReaderInvocation(true, method.Name, Assert.IsType<bool>(Property("Success")),
                    (PendingTurnSnapshotReadAuthority?)Property("Snapshot"), Property("Evidence"),
                    Assert.IsAssignableFrom<IReadOnlyList<ValidationIssue>>(Property("Issues")));
            }
        }
        // Missing-feature behavior only; never selected after an implementation
        // throws or exposes a malformed API. The barrier test cannot GREEN here.
        var legacy = PendingTurnSnapshotReader.ReadCurrent(files, lease, RequiredPaths);
        return new ReaderInvocation(false, "ReadCurrent", legacy.Success, legacy.Snapshot, null, legacy.Issues);
    }

    private sealed record ReaderInvocation(bool ApiPresent, string InvokedApi, bool Success,
        PendingTurnSnapshotReadAuthority? Snapshot, object? Evidence, IReadOnlyList<ValidationIssue> Issues);
    private sealed record DriftObservation(ReaderInvocation Result, byte[] OriginalFirst);

    private static Dictionary<string, byte[]> SnapshotTree(string root) =>
        Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories)
            .ToDictionary(path => Path.GetRelativePath(root, path), File.ReadAllBytes, StringComparer.Ordinal);

    private static void AssertTreeEqual(IReadOnlyDictionary<string, byte[]> expected,
        IReadOnlyDictionary<string, byte[]> actual)
    {
        Assert.Equal(expected.Keys.OrderBy(path => path, StringComparer.Ordinal),
            actual.Keys.OrderBy(path => path, StringComparer.Ordinal));
        foreach (var pair in expected) Assert.Equal(pair.Value, actual[pair.Key]);
    }
}
