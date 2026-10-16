using System.Reflection;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using BookOfEternityClient.Core;
using BookOfEternityClient.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;
using Xunit.Abstractions;

namespace BookOfEternityClient.Tests;

public sealed class PendingTurnObservedOriginalReaderConformanceTests(ITestOutputHelper output)
{
    private const string First = "game_state/control/pending_mortal_wound_occurrences.json";
    private const string Second = "game_state/core/observed_reader_guard.json";
    private const string Repair = "game_state/control/validation_repair_request.json";
    private const string Ready = "ready/turn_complete.json";

    [Fact]
    public async Task Evidence_RequiresExactOwnerLeaseSnapshotAndRetainsOnlyDetachedDiagnostics()
    {
        await using var fixture = new OriginalFixture(output);
        await fixture.PrepareAsync();
        var before = fixture.Tree();
        PendingTurnSnapshotReader.OriginalReadEvidence evidence;
        PendingTurnSnapshotReadAuthority snapshot;
        FileSystemManager.CanonicalWriteLease endedLease;
        string diagnostic;
        await using (var lease = await fixture.Files.AcquireCanonicalWriteLeaseAsync())
        {
            endedLease = lease;
            var result = fixture.ReadValid(lease);
            evidence = result.Evidence!;
            snapshot = result.Snapshot!;
            diagnostic = evidence.ExportDiagnosticData().ToJsonString();
            var exported = evidence.ExportDiagnosticData();
            exported["operations"]!.AsArray().Clear();
            exported["generation"] = "foreign";
            Assert.Equal(diagnostic, evidence.ExportDiagnosticData().ToJsonString());
            var bytes = snapshot.ReadRequiredBytes(First);
            bytes[0] ^= 0x01;
            Assert.Equal(fixture.OriginalFirst, snapshot.ReadRequiredBytes(First));

            var old = PendingTurnSnapshotReader.ReadCurrent(fixture.Files, lease, [First, Second]);
            Assert.True(old.Success);
            Assert.Throws<InvalidOperationException>(() =>
                evidence.RequireCurrent(fixture.Files, lease, old.Snapshot!));
            var foreign = new FileSystemManager(fixture.Root, NullLogger<FileSystemManager>.Instance);
            Assert.Throws<InvalidOperationException>(() => evidence.RequireCurrent(foreign, lease, snapshot));
            Assert.Throws<InvalidOperationException>(() =>
                PendingTurnSnapshotReader.ReadObservedCurrent(foreign, lease, [First]));
            var constructor = Assert.Single(typeof(PendingTurnSnapshotReader.OriginalReadEvidence)
                .GetConstructors(BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public));
            var invalidIssuer = Assert.Throws<TargetInvocationException>(() =>
                constructor.Invoke([new object(), new object(), snapshot]));
            Assert.IsType<InvalidOperationException>(invalidIssuer.InnerException);

            // These byte-only observations do not acquire a new mtime policy.
            File.SetLastWriteTimeUtc(fixture.Files.ResolvePath(fixture.Manifest.Files[First]),
                new DateTime(2001, 2, 3, 4, 5, 6, DateTimeKind.Utc));
            evidence.RequireCurrent(fixture.Files, lease, snapshot);
            Assert.Equal(diagnostic, evidence.ExportDiagnosticData().ToJsonString());
            var contexts = evidence.ExportDiagnosticData()["operations"]!.AsArray()
                .Where(row => row!["role"]!.GetValue<string>().StartsWith("Context", StringComparison.Ordinal))
                .Select(row => (row!["role"]!.GetValue<string>(), row["path"]!.GetValue<string>())).ToArray();
            Assert.Equal(new[] { ("ContextPresence", Repair), ("ContextPresence", LiveTurnPreparationService.TurnRequestPath),
                ("ContextText", LiveTurnPreparationService.TurnRequestPath), ("ContextPresence", Ready) }, contexts);
        }
        Assert.Throws<InvalidOperationException>(() =>
            evidence.RequireCurrent(fixture.Files, endedLease, snapshot));
        Assert.Equal(diagnostic, evidence.ExportDiagnosticData().ToJsonString());
        fixture.EqualTree(before);
    }

    [Theory]
    [InlineData("manifest")]
    [InlineData("authority")]
    [InlineData("request")]
    [InlineData("repair_presence")]
    [InlineData("ready_presence")]
    [InlineData("generation")]
    public async Task ReadObservedCurrent_RejectsActualLateSourceDriftBeforeExport(string role)
    {
        var armed = false;
        var injections = 0;
        string? trigger = null, fullTarget = null;
        byte[]? changed = null;
        var hooks = new FileSystemManagerHooks
        {
            AfterCanonicalReadInitialValidationAsync = path =>
            {
                if (armed && path == trigger && injections == 0)
                {
                    injections++;
                    File.WriteAllBytes(fullTarget!, changed!);
                }
                return Task.CompletedTask;
            }
        };
        await using var fixture = new OriginalFixture(output, hooks);
        await fixture.PrepareAsync();
        var target = role switch
        {
            "manifest" => LiveTurnPreparationService.PendingTurnSnapshotManifestPath,
            "authority" => PendingTurnSnapshotAuthority.AuthorityPath,
            "request" => LiveTurnPreparationService.TurnRequestPath,
            "repair_presence" => Repair,
            "ready_presence" => Ready,
            _ => fixture.Files.SessionGenerationPath
        };
        fullTarget = role == "generation" ? target : fixture.Files.ResolvePath(target);
        changed = File.Exists(fullTarget) ? File.ReadAllBytes(fullTarget).Concat(new byte[] { 0x20 }).ToArray()
            : Encoding.UTF8.GetBytes("{}");
        trigger = fixture.Manifest.Files[Second];
        var before = fixture.Tree();
        await using (var lease = await fixture.Files.AcquireCanonicalWriteLeaseAsync())
        {
            fixture.ReadValid(lease);
            armed = true;
            var result = PendingTurnSnapshotReader.ReadObservedCurrent(fixture.Files, lease, [First, Second]);
            armed = false;
            Assert.Equal(1, injections);
            Assert.False(result.Success);
            Assert.Null(result.Snapshot);
            Assert.Null(result.Evidence);
            var issue = Assert.Single(result.Issues);
            Assert.Equal(role == "generation" ? "pending_turn_snapshot_reader_source_generation_changed"
                : "pending_turn_snapshot_reader_source_changed", issue.Code);
            Assert.Equal(target, issue.FilePath);
        }
        before[Path.GetRelativePath(fixture.Root, fullTarget)] = changed;
        fixture.EqualTree(before);
    }

    [Theory]
    [InlineData("manifest")]
    [InlineData("selected")]
    [InlineData("recheck_manifest")]
    public async Task ReadObservedCurrent_PropagatesTheActualPhysicalReadFailure(string role)
    {
        var armed = false;
        string? target = null;
        var failure = new IOException("Observed-original physical read fault.");
        var faults = 0;
        var targetReads = 0;
        var hooks = new FileSystemManagerHooks
        {
            AfterCanonicalReadInitialValidationAsync = path =>
            {
                if (armed && path == target && (++targetReads > 1 || role != "recheck_manifest"))
                { faults++; throw failure; }
                return Task.CompletedTask;
            }
        };
        await using var fixture = new OriginalFixture(output, hooks);
        await fixture.PrepareAsync();
        target = role is "manifest" or "recheck_manifest" ? LiveTurnPreparationService.PendingTurnSnapshotManifestPath
            : fixture.Manifest.Files[Second];
        var before = fixture.Tree();
        await using (var lease = await fixture.Files.AcquireCanonicalWriteLeaseAsync())
        {
            fixture.ReadValid(lease);
            armed = true;
            Assert.Same(failure, Assert.Throws<IOException>(() =>
                PendingTurnSnapshotReader.ReadObservedCurrent(fixture.Files, lease, [First, Second])));
            Assert.Equal(1, faults);
            armed = false;
        }
        fixture.EqualTree(before);
    }

    [Theory]
    [InlineData("manifest")]
    [InlineData("authority")]
    [InlineData("request")]
    [InlineData("repair_presence")]
    [InlineData("ready_presence")]
    [InlineData("selected")]
    [InlineData("generation")]
    public async Task Evidence_RejectsChangedFrozenSourcesWithoutRebinding(string role)
    {
        await using var fixture = new OriginalFixture(output);
        await fixture.PrepareAsync();
        string target;
        byte[] changed;
        var before = fixture.Tree();
        await using (var lease = await fixture.Files.AcquireCanonicalWriteLeaseAsync())
        {
            var result = fixture.ReadValid(lease);
            var diagnostic = result.Evidence!.ExportDiagnosticData().ToJsonString();
            target = role switch
            {
                "manifest" => fixture.Files.ResolvePath(LiveTurnPreparationService.PendingTurnSnapshotManifestPath),
                "authority" => fixture.Files.ResolvePath(PendingTurnSnapshotAuthority.AuthorityPath),
                "request" => fixture.Files.ResolvePath(LiveTurnPreparationService.TurnRequestPath),
                "repair_presence" => fixture.Files.ResolvePath(Repair),
                "ready_presence" => fixture.Files.ResolvePath(Ready),
                "selected" => fixture.Files.ResolvePath(fixture.Manifest.Files[First]),
                _ => fixture.Files.SessionGenerationPath
            };
            changed = File.Exists(target) ? File.ReadAllBytes(target).Concat(new byte[] { 0x20 }).ToArray()
                : Encoding.UTF8.GetBytes("{}");
            File.WriteAllBytes(target, changed);
            Assert.Throws<InvalidDataException>(() => result.Evidence.RequireCurrent(fixture.Files, lease, result.Snapshot!));
            Assert.Equal(diagnostic, result.Evidence.ExportDiagnosticData().ToJsonString());
        }
        before[Path.GetRelativePath(fixture.Root, target)] = changed;
        fixture.EqualTree(before);
    }

    [Theory]
    [InlineData("manifest")]
    [InlineData("authority")]
    [InlineData("contexts")]
    [InlineData("stale_request")]
    public async Task ReadObservedCurrent_PreservesDefaultFailurePriorityAndEagerContexts(string failure)
    {
        var tracing = false;
        var reads = new List<string>();
        var hooks = new FileSystemManagerHooks
        {
            AfterCanonicalReadInitialValidationAsync = path =>
            { if (tracing) reads.Add(path); return Task.CompletedTask; }
        };
        await using var fixture = new OriginalFixture(output, hooks);
        await fixture.PrepareAsync();
        await using (var baseline = await fixture.Files.AcquireCanonicalWriteLeaseAsync()) fixture.ReadValid(baseline);
        if (failure is "manifest" or "authority")
            File.WriteAllText(fixture.Files.ResolvePath(failure == "manifest"
                ? LiveTurnPreparationService.PendingTurnSnapshotManifestPath : PendingTurnSnapshotAuthority.AuthorityPath), "{");
        else if (failure == "contexts")
        {
            // Secondary malformed inputs, never a manufactured primary context.
            File.WriteAllText(fixture.Files.ResolvePath(Repair), "{");
            File.WriteAllText(fixture.Files.ResolvePath(Ready), "{");
        }
        else
        {
            var request = JsonNode.Parse(File.ReadAllText(fixture.Files.ResolvePath(LiveTurnPreparationService.TurnRequestPath)))!;
            request["requestId"] = "stale-request";
            File.WriteAllText(fixture.Files.ResolvePath(LiveTurnPreparationService.TurnRequestPath), request.ToJsonString());
        }
        var before = fixture.Tree();
        await using (var lease = await fixture.Files.AcquireCanonicalWriteLeaseAsync())
        {
            var old = PendingTurnSnapshotReader.ReadCurrent(fixture.Files, lease, [First, Second]);
            Assert.False(old.Success);
            tracing = true;
            var observed = PendingTurnSnapshotReader.ReadObservedCurrent(fixture.Files, lease, [First, Second]);
            tracing = false;
            Assert.False(observed.Success);
            Assert.Null(observed.Snapshot);
            Assert.Null(observed.Evidence);
            Assert.Equal(JsonSerializer.Serialize(old.Issues), JsonSerializer.Serialize(observed.Issues));
            if (failure == "contexts")
                Assert.Equal(new[] { Repair, LiveTurnPreparationService.TurnRequestPath, Ready },
                    reads.Where(path => path == Repair || path == LiveTurnPreparationService.TurnRequestPath || path == Ready));
        }
        fixture.EqualTree(before);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ReadObservedCurrent_PreservesBomDecodingAndDistinctRawDigest(bool utf16)
    {
        await using var fixture = new OriginalFixture(output);
        await fixture.PrepareAsync();
        await using (var baseline = await fixture.Files.AcquireCanonicalWriteLeaseAsync()) fixture.ReadValid(baseline);
        var encoding = utf16 ? Encoding.Unicode : Encoding.UTF8;
        foreach (var path in new[] { LiveTurnPreparationService.PendingTurnSnapshotManifestPath,
                     PendingTurnSnapshotAuthority.AuthorityPath, LiveTurnPreparationService.TurnRequestPath })
        {
            var full = fixture.Files.ResolvePath(path);
            var text = File.ReadAllText(full);
            File.WriteAllBytes(full, encoding.GetPreamble().Concat(encoding.GetBytes(text)).ToArray());
        }
        var before = fixture.Tree();
        await using (var lease = await fixture.Files.AcquireCanonicalWriteLeaseAsync())
        {
            var observed = fixture.ReadValid(lease);
            Assert.Equal(fixture.Manifest.ManifestPayloadHash, observed.Snapshot!.SnapshotToken);
            var diagnostic = observed.Evidence!.ExportDiagnosticData();
            Assert.Equal(PendingTurnSnapshotAuthority.ComputeSha256(File.ReadAllBytes(fixture.Files.ResolvePath(
                LiveTurnPreparationService.PendingTurnSnapshotManifestPath))), diagnostic["manifestRawHash"]!.GetValue<string>());
        }
        fixture.EqualTree(before);
    }

    [Fact]
    public async Task ReadObservedCurrent_SelectionFailurePrecedesMissingGenerationWithoutCreatingIt()
    {
        await using var fixture = new OriginalFixture(output);
        await fixture.PrepareAsync();
        await using (var baseline = await fixture.Files.AcquireCanonicalWriteLeaseAsync()) fixture.ReadValid(baseline);
        File.Delete(fixture.Files.SessionGenerationPath);
        await using (var lease = await fixture.Files.AcquireCanonicalWriteLeaseAsync())
        {
            Assert.Null(fixture.Files.ReadLocalGenerationSnapshot(lease).Binding.Id);
            var selection = PendingTurnSnapshotReader.ReadObservedCurrent(fixture.Files, lease, []);
            Assert.False(selection.Success);
            Assert.Equal("pending_turn_snapshot_reader_required_paths_invalid", Assert.Single(selection.Issues).Code);
            var missing = PendingTurnSnapshotReader.ReadObservedCurrent(fixture.Files, lease, [First]);
            Assert.False(missing.Success);
            Assert.Null(missing.Snapshot);
            Assert.Null(missing.Evidence);
            Assert.Equal("pending_turn_snapshot_reader_source_generation_unavailable", Assert.Single(missing.Issues).Code);
            Assert.False(File.Exists(fixture.Files.SessionGenerationPath));
        }
    }

    [Fact]
    public async Task ReadObservedCurrent_KeepsSignedOptionalAbsenceSeparateFromCurrentFiles()
    {
        const string optional = "game_state/npcs/npc_wounds.json";
        await using var fixture = new OriginalFixture(output);
        await fixture.PrepareAsync();
        Assert.False(fixture.Manifest.OriginalPathPresenceV1![optional]);
        await fixture.Files.WriteFileAtomicAsync(optional, "{}");
        var before = fixture.Tree();
        await using (var lease = await fixture.Files.AcquireCanonicalWriteLeaseAsync())
        {
            var result = PendingTurnSnapshotReader.ReadObservedCurrent(fixture.Files, lease,
                PendingTurnSnapshotPathSelection.CreateWithObservedOptionalPaths([First], [optional]));
            Assert.True(result.Success, JsonSerializer.Serialize(result.Issues));
            Assert.Contains(optional, result.Snapshot!.AbsentLogicalPaths);
            Assert.Throws<KeyNotFoundException>(() => result.Snapshot.ReadRequiredBytes(optional));
            Assert.DoesNotContain(result.Evidence!.ExportDiagnosticData()["operations"]!.AsArray(),
                row => row!["path"]!.GetValue<string>() == optional);
            result.Evidence.RequireCurrent(fixture.Files, lease, result.Snapshot);
        }
        fixture.EqualTree(before);
    }

    [Fact]
    public async Task ReadObservedCurrent_UsesTheExplicitLeaseWithoutAmbientExecutionContext()
    {
        await using var fixture = new OriginalFixture(output);
        await fixture.PrepareAsync();
        var before = fixture.Tree();
        await using (var lease = await fixture.Files.AcquireCanonicalWriteLeaseAsync())
        {
            Task task;
            using (ExecutionContext.SuppressFlow())
                task = Task.Run(() => fixture.ReadValid(lease));
            await task;
        }
        fixture.EqualTree(before);
    }

    private sealed class OriginalFixture : IAsyncDisposable
    {
        private readonly ITestOutputHelper _output;
        internal string Root { get; } = Path.Combine(Path.GetTempPath(), "boe-observed-guards-" + Guid.NewGuid().ToString("N"));
        internal FileSystemManager Files { get; }
        internal LiveTurnPendingSnapshotManifest Manifest { get; private set; } = null!;
        internal byte[] OriginalFirst { get; private set; } = null!;
        internal OriginalFixture(ITestOutputHelper output, FileSystemManagerHooks? hooks = null)
        {
            _output = output;
            Directory.CreateDirectory(Root);
            Files = new FileSystemManager(Root, NullLogger<FileSystemManager>.Instance,
                PhysicalLoadTransactionOperations.Instance, hooks);
        }
        internal async Task PrepareAsync()
        {
            Files.EnsureDirectoryStructure();
            await Files.WriteFileAtomicAsync(First, "{\"schemaVersion\":1,\"occurrences\":[]}");
            await Files.WriteFileAtomicAsync(Second, "{\"value\":19}");
            OriginalFirst = File.ReadAllBytes(Files.ResolvePath(First));
            await new LiveTurnPreparationService(Files).PrepareAsync(new LiveTurnPreparationOptions
            {
                SessionId = "observed-guards-session-73", RequestId = "observed-guards-request-73",
                TurnNumber = 73, CurrentRealm = "Mortal World", PlayerAction = "Проверить исходное состояние.",
                PreGeneratedDices1d20 = [5, 15]
            });
            Manifest = JsonSerializer.Deserialize<LiveTurnPendingSnapshotManifest>(File.ReadAllText(Files.ResolvePath(
                LiveTurnPreparationService.PendingTurnSnapshotManifestPath)), LiveTurnPreparationService.ManifestJsonOptions)!;
        }
        internal PendingTurnSnapshotObservedReadResult ReadValid(FileSystemManager.CanonicalWriteLease lease)
        {
            var result = PendingTurnSnapshotReader.ReadObservedCurrent(Files, lease, [First, Second]);
            Assert.True(result.Success, JsonSerializer.Serialize(result.Issues));
            Assert.Empty(result.Issues);
            Assert.NotNull(result.Snapshot);
            Assert.NotNull(result.Evidence);
            Assert.Equal(OriginalFirst, result.Snapshot!.ReadRequiredBytes(First));
            result.Evidence!.RequireCurrent(Files, lease, result.Snapshot);
            return result;
        }
        internal Dictionary<string, byte[]> Tree() => Directory.EnumerateFiles(Root, "*", SearchOption.AllDirectories)
            .ToDictionary(path => Path.GetRelativePath(Root, path), File.ReadAllBytes, StringComparer.Ordinal);
        internal void EqualTree(IReadOnlyDictionary<string, byte[]> before)
        {
            var after = Tree();
            Assert.Equal(before.Keys.OrderBy(path => path, StringComparer.Ordinal), after.Keys.OrderBy(path => path, StringComparer.Ordinal));
            foreach (var pair in before) Assert.Equal(pair.Value, after[pair.Key]);
        }
        public ValueTask DisposeAsync()
        {
            Exception? failure = null;
            try { if (Directory.Exists(Root)) Directory.Delete(Root, recursive: true); }
            catch (Exception error) { failure = error; }
            _output.WriteLine(JsonSerializer.Serialize(new { CleanupOwnedRoot = Root,
                OwnedFixtureRemoved = !Directory.Exists(Root), CleanupFailure = failure?.ToString() }));
            Assert.Null(failure);
            Assert.False(Directory.Exists(Root));
            return ValueTask.CompletedTask;
        }
    }
}
