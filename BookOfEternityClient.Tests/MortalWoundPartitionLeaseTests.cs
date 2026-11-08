using System.Diagnostics;
using System.Text.Json;
using BookOfEternityClient.Core;
using BookOfEternityClient.Services;
using BookOfEternityClient.Services.GmRuntime;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;
using Xunit.Abstractions;

namespace BookOfEternityClient.Tests;

/// <summary>
/// Checks the real current-partition reader with issued snapshot authority and independently owned leases.
/// </summary>
/// <param name="output">Receives phase durations, exact failures and owned cleanup.</param>
public sealed class MortalWoundPartitionLeaseTests(ITestOutputHelper output)
{
    /// <summary>
    /// Isolates normal async-returned lease ownership from caller-local ambient admission and present inputs.
    /// </summary>
    /// <param name="returnedLease">Whether acquisition completes inside an ordinary async helper.</param>
    /// <param name="present">Whether the signed optional states are present rather than genuinely absent.</param>
    [Theory]
    [InlineData(true, false)]
    [InlineData(false, false)]
    [InlineData(true, true)]
    public async Task CurrentPartitionUsesItsHeldLease(bool returnedLease, bool present)
    {
        using var fixture = new Fixture(output);
        await fixture.PrepareAsync(present);
        // Keep caller-local acquisition here: moving it into the helper would change the control.
        await using var lease = returnedLease
            ? await fixture.AcquireReturnedLeaseAsync()
            : await fixture.Files.AcquireCanonicalWriteLeaseAsync();
        var snapshot = fixture.ReadSnapshot(lease, present);
        var before = fixture.CaptureTree();
        var issues = new List<ValidationIssue>();
        MortalWoundOpportunityReceiptState? receipts = null;
        var success = false;
        var start = Stopwatch.GetTimestamp();
        var error = Record.Exception(() => success = MortalWoundOpportunityAdapter.TryReadPublishedReceiptPartition(
            fixture.Files, lease, snapshot, issues, out receipts));
        output.WriteLine(JsonSerializer.Serialize(new
        {
            Phase = "actual-current-partition", returnedLease, present,
            Seconds = Stopwatch.GetElapsedTime(start).TotalSeconds, Error = error?.ToString(), success
        }));
        fixture.Files.EnsureCanonicalWriteLeaseActive(lease);
        fixture.AssertTree(before);
        Assert.Null(error);
        Assert.True(success, Describe(issues));
        Assert.Empty(issues);
        Assert.Empty(Assert.IsType<MortalWoundOpportunityReceiptState>(receipts).Receipts);
    }

    /// <summary>
    /// Proves that this fixture's returned lease holds the guard and that only implicit absence reacquires it.
    /// </summary>
    [Fact]
    public async Task ReturnedLeaseSeparatesImplicitAbsenceFromExplicitRead()
    {
        using var fixture = new Fixture(output);
        fixture.WritePhysical("present.txt", "original");
        await using (var lease = await fixture.AcquireReturnedLeaseAsync())
        {
            fixture.Files.EnsureCanonicalWriteLeaseActive(lease);
            var before = fixture.CaptureTree();
            Assert.Throws<IOException>(() => fixture.ProbeExclusiveGuard());
            Assert.Equal("original", fixture.Files.ReadFileSync(lease, "present.txt"));
            Assert.Equal("original", fixture.Files.ReadFileSync("present.txt"));
            Assert.Null(fixture.Files.ReadFileSync(lease, "absent.txt"));
            var start = Stopwatch.GetTimestamp();
            var error = Assert.Throws<IOException>(() => fixture.Files.ReadFileSync("absent.txt"));
            output.WriteLine(JsonSerializer.Serialize(new
            {
                Phase = "implicit-absent-under-returned-lease",
                Seconds = Stopwatch.GetElapsedTime(start).TotalSeconds, Error = error.ToString()
            }));
            Assert.Contains("gm-main-owner.lock", error.Message);
            Assert.Contains("GmMainOwnerGuard.AcquireAsync", error.StackTrace);
            Assert.Contains("ReadObservedFileSyncCoreT607Original", error.StackTrace);
            fixture.Files.EnsureCanonicalWriteLeaseActive(lease);
            Assert.Null(fixture.Files.ReadFileSync(lease, "absent.txt"));
            fixture.AssertTree(before);
        }
        // No lock deletion or replacement: releasing our lease alone makes the exact guard available.
        fixture.ProbeExclusiveGuard();
    }

    /// <summary>
    /// Keeps current malformed or unsigned absence refusals after genuinely signing present before-images.
    /// </summary>
    /// <param name="mutation">The sole live-state change made after snapshot issuance.</param>
    /// <param name="code">The original parser refusal expected from the actual current reader.</param>
    [Theory]
    [InlineData("malformed-receipt", "mortal_wound_opportunity_receipt_invalid_root")]
    [InlineData("missing-receipt", "mortal_wound_opportunity_receipt_invalid_root")]
    [InlineData("missing-history", "wound_history_invalid_root")]
    public async Task CurrentPartitionRejectsChangedSignedInputs(string mutation, string code)
    {
        using var fixture = new Fixture(output);
        await fixture.PrepareAsync(present: true);
        // These refusal controls deliberately retain ambient admission, independently of the causal RED.
        await using var lease = await fixture.Files.AcquireCanonicalWriteLeaseAsync();
        var snapshot = fixture.ReadSnapshot(lease, present: true);
        var path = mutation == "missing-history" ? WoundHistoryState.HistoryPath : MortalWoundOpportunityReceiptState.StatePath;
        if (mutation == "malformed-receipt") fixture.WritePhysical(path, "{");
        else File.Delete(fixture.Files.ResolvePath(path));
        var before = fixture.CaptureTree();
        var issues = new List<ValidationIssue>();
        Assert.False(MortalWoundOpportunityAdapter.TryReadPublishedReceiptPartition(
            fixture.Files, lease, snapshot, issues, out var receipts));
        Assert.Null(receipts);
        Assert.Contains(issues, issue => issue.Code == code);
        fixture.Files.EnsureCanonicalWriteLeaseActive(lease);
        fixture.AssertTree(before);
    }

    /// <summary>
    /// Keeps provisional current-reader DATA subordinate to the real owning source fence and its failed lifetime.
    /// </summary>
    [Fact]
    public async Task CurrentPartitionDataCannotEscapeAFailedSourceFence()
    {
        using var fixture = new Fixture(output);
        await fixture.PrepareAsync(present: true);
        await using var lease = await fixture.Files.AcquireCanonicalWriteLeaseAsync();
        fixture.Files.GetOrCreateSessionGeneration(lease);
        var snapshot = fixture.ReadSnapshot(lease, present: true);
        using var source = fixture.Files.BeginSourceDataOperationIfNeeded();
        Assert.NotNull(source);
        var issues = new List<ValidationIssue>();
        Assert.True(MortalWoundOpportunityAdapter.TryReadPublishedReceiptPartition(
            fixture.Files, lease, snapshot, issues, out _), Describe(issues));
        Assert.True(MortalWoundOpportunityAdapter.TryReadPublishedReceiptPartition(
            fixture.Files, lease, snapshot, issues, out _), Describe(issues));
        var path = fixture.Files.ResolvePath(MortalWoundOpportunityReceiptState.StatePath);
        var timestamp = File.GetLastWriteTimeUtc(path);
        fixture.WritePhysical(MortalWoundOpportunityReceiptState.StatePath, "{");
        File.SetLastWriteTimeUtc(path, timestamp);
        var before = fixture.CaptureTree();
        // Within this owner the previous calculation is provisional DATA, not permission to return success.
        Assert.True(MortalWoundOpportunityAdapter.TryReadPublishedReceiptPartition(
            fixture.Files, lease, snapshot, issues, out var provisional), Describe(issues));
        Assert.Empty(Assert.IsType<MortalWoundOpportunityReceiptState>(provisional).Receipts);
        var refusal = await Assert.ThrowsAsync<InvalidDataException>(() => source!.CompleteAsync());
        Assert.Equal("Operation source changed: " + MortalWoundOpportunityReceiptState.StatePath, refusal.Message);
        Assert.Throws<InvalidOperationException>(() => MortalWoundOpportunityAdapter.TryReadPublishedReceiptPartition(
            fixture.Files, lease, snapshot, issues, out _));
        Assert.True(lease.IsActive);
        fixture.AssertTree(before);
        output.WriteLine(JsonSerializer.Serialize(new { Phase = "source-closing-refusal", Error = refusal.ToString() }));
    }

    /// <summary>
    /// Refuses actual foreign/closed leases and fresh main ownership or namespace drift without changing files.
    /// </summary>
    /// <param name="mutation">The exact authority boundary changed after valid snapshot issuance.</param>
    [Theory]
    [InlineData("foreign-manager")]
    [InlineData("disposed")]
    [InlineData("main-running")]
    [InlineData("main-namespace")]
    public async Task CurrentPartitionRejectsInvalidLeaseAuthority(string mutation)
    {
        using var fixture = new Fixture(output);
        await fixture.PrepareAsync(present: true);
        await using var lease = await fixture.AcquireReturnedLeaseAsync();
        var snapshot = fixture.ReadSnapshot(lease, present: true);
        var files = fixture.Files;
        if (mutation == "foreign-manager")
            files = new FileSystemManager(fixture.Root, NullLogger<FileSystemManager>.Instance);
        else if (mutation == "disposed") await lease.DisposeAsync();
        else fixture.ChangeMainAuthority(mutation);
        var before = fixture.CaptureTree();
        var issues = new List<ValidationIssue>();
        var error = Record.Exception(() => MortalWoundOpportunityAdapter.TryReadPublishedReceiptPartition(
            files, lease, snapshot, issues, out _));
        output.WriteLine(JsonSerializer.Serialize(new { Phase = "current-authority-refusal", mutation, Error = error?.ToString() }));
        Assert.Empty(issues);
        fixture.AssertTree(before);
        if (mutation is "foreign-manager" or "disposed") Assert.IsType<InvalidOperationException>(error);
        else if (mutation == "main-namespace") Assert.IsType<InvalidDataException>(error);
        else Assert.IsAssignableFrom<IOException>(error);
    }

    /// <summary>Renders original issue codes for a failed assertion.</summary>
    private static string Describe(IEnumerable<ValidationIssue> issues) => string.Join("; ", issues.Select(issue => issue.Code));

    /// <summary>Owns a minimal real canonical tree; no engine, observer, cache owner or authority fabrication.</summary>
    private sealed class Fixture : IDisposable
    {
        private const string EmptyOccurrences = "{\"schemaVersion\":1,\"occurrences\":[]}";
        private const string EmptyReceipts = "{\"schemaVersion\":1,\"nextOrdinal\":1,\"receipts\":[]}";
        private const string EmptyHistory = "{\"schemaVersion\":1,\"nextOrdinal\":1,\"transitions\":[]}";
        private readonly string _nonce = Guid.NewGuid().ToString("N");
        private readonly ITestOutputHelper _output;

        /// <summary>Creates only this case's nonce-owned root and real manager.</summary>
        internal Fixture(ITestOutputHelper output)
        {
            _output = output;
            Root = Path.Combine(Path.GetTempPath(), "boe-partition-lease-" + _nonce);
            Directory.CreateDirectory(Path.Combine(Root, "game_session"));
            File.WriteAllText(Path.Combine(Root, ".fixture-owner"), _nonce);
            Files = new FileSystemManager(Root, NullLogger<FileSystemManager>.Instance);
        }

        /// <summary>Gets the exact owned root.</summary>
        internal string Root { get; }

        /// <summary>Gets this case's real file manager.</summary>
        internal FileSystemManager Files { get; }

        /// <summary>Issues a genuine signed original using the supported preparation service.</summary>
        internal async Task PrepareAsync(bool present)
        {
            var start = Stopwatch.GetTimestamp();
            WritePhysical(MortalWoundOccurrenceState.StatePath, EmptyOccurrences);
            if (present)
            {
                WritePhysical(MortalWoundOpportunityReceiptState.StatePath, EmptyReceipts);
                WritePhysical(WoundHistoryState.HistoryPath, EmptyHistory);
            }
            await new LiveTurnPreparationService(Files).PrepareAsync(new LiveTurnPreparationOptions
            {
                SessionId = "partition-lease-session", RequestId = "partition-lease-request", TurnNumber = 71,
                CurrentRealm = "Mortal World", PlayerAction = "Inspect the signed wound partition.",
                PreGeneratedDices1d20 = [3, 17]
            });
            _output.WriteLine(JsonSerializer.Serialize(new { Phase = "prepare-issued-snapshot", present,
                Seconds = Stopwatch.GetElapsedTime(start).TotalSeconds }));
        }

        /// <summary>Returns a real lease across an ordinary async method boundary without flow suppression.</summary>
        internal async Task<FileSystemManager.CanonicalWriteLease> AcquireReturnedLeaseAsync() =>
            await Files.AcquireCanonicalWriteLeaseAsync();

        /// <summary>Reads and checks the service-issued authority, including actual signed optional absence.</summary>
        internal PendingTurnSnapshotReadAuthority ReadSnapshot(FileSystemManager.CanonicalWriteLease lease, bool present)
        {
            var result = PendingTurnSnapshotReader.ReadCurrent(Files, lease,
                PendingTurnSnapshotPathSelection.CreateWithObservedOptionalPaths(
                    [MortalWoundOccurrenceState.StatePath],
                    [MortalWoundOpportunityReceiptState.StatePath, WoundHistoryState.HistoryPath]));
            Assert.True(result.Success, Describe(result.Issues));
            Assert.Empty(result.Issues);
            var snapshot = Assert.IsType<PendingTurnSnapshotReadAuthority>(result.Snapshot);
            Assert.Equal("partition-lease-session", snapshot.SessionId);
            foreach (var path in new[] { MortalWoundOpportunityReceiptState.StatePath, WoundHistoryState.HistoryPath })
                Assert.Equal(!present, snapshot.AbsentLogicalPaths.Contains(path, StringComparer.Ordinal));
            return snapshot;
        }

        /// <summary>Writes one owned fixture input or explicit external-change stimulus, never authority envelopes.</summary>
        internal void WritePhysical(string path, string text)
        {
            var fullPath = Files.ResolvePath(path);
            Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);
            File.WriteAllText(fullPath, text);
        }

        /// <summary>Probes the exact existing physical guard without creating, deleting or changing it.</summary>
        internal void ProbeExclusiveGuard()
        {
            using var probe = new FileStream(Path.Combine(Root, ".boe_runtime", "locks", "gm-main-owner.lock"),
                FileMode.Open, FileAccess.ReadWrite, FileShare.None);
        }

        /// <summary>Introduces a real current main-record or namespace refusal after authority issuance.</summary>
        internal void ChangeMainAuthority(string mutation)
        {
            var directory = Path.Combine(Root, ".boe_runtime", "gm-runs");
            Assert.False(Directory.Exists(directory));
            if (mutation == "main-namespace") File.WriteAllText(directory, "namespace blocker");
            else
            {
                Directory.CreateDirectory(directory);
                var identity = new GmSessionRunIdentity(Root, Guid.NewGuid().ToString("N"), Guid.Empty.ToString("N"), 1,
                    OperatingSystem.IsWindows() ? GmSessionRunBackend.WindowsJob : GmSessionRunBackend.LinuxSupervisor,
                    Guid.NewGuid().ToString("N"), "partition-lease-boot");
                File.WriteAllBytes(Path.Combine(directory, "main.json"), GmSessionRunRecordCodec.Encode(
                    new GmSessionRunRecord(1, identity, GmSessionRunDisposition.Running, null)));
            }
        }

        /// <summary>Captures every file name and non-lock bytes; held lock bytes cannot be reopened on Windows.</summary>
        internal Dictionary<string, byte[]> CaptureTree() => Directory.EnumerateFiles(Root, "*", SearchOption.AllDirectories)
            .ToDictionary(path => Path.GetRelativePath(Root, path),
                path => path.EndsWith(".lock", StringComparison.Ordinal) ? [] : File.ReadAllBytes(path), StringComparer.Ordinal);

        /// <summary>Verifies no current source or authority file changed during the reader.</summary>
        internal void AssertTree(Dictionary<string, byte[]> before)
        {
            var after = CaptureTree();
            Assert.Equal(before.Keys.OrderBy(path => path), after.Keys.OrderBy(path => path));
            foreach (var (path, bytes) in before) Assert.Equal(bytes, after[path]);
        }

        /// <summary>Deletes only the exact nonce-owned root after the case has disposed its leases.</summary>
        public void Dispose()
        {
            var start = Stopwatch.GetTimestamp();
            var scope = new TrustedLocalFileScope([Path.GetTempPath()]);
            if (Path.GetFileName(Root) != "boe-partition-lease-" + _nonce ||
                File.ReadAllText(scope.ValidateFile(Path.Combine(Root, ".fixture-owner"), false)) != _nonce)
                throw new InvalidDataException("Partition fixture ownership changed; root retained.");
            scope.DeleteOwnedTree(Root);
            Assert.False(Directory.Exists(Root));
            _output.WriteLine(JsonSerializer.Serialize(new { Phase = "owned-cleanup", Root, Removed = true,
                Seconds = Stopwatch.GetElapsedTime(start).TotalSeconds }));
        }
    }
}
