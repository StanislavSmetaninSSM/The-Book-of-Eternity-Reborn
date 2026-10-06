using System.Text.Json;
using System.Text;
using System.Text.Json.Nodes;
using BookOfEternityClient.Services.GmWorkers;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed class GmWorkerRunLedgerTests
{
    [Fact]
    public async Task LaunchIntent_IsDurableAndPermanentlyDisallowsPrelaunchAbort()
    {
        using var fixture = new LedgerFixture();
        await using var owner = await fixture.OpenInitialized();
        var entry = await fixture.Prepare(owner);
        Assert.Equal(WorkerLedgerMutationKind.Applied, await owner.PlanLaunchAsync(entry, owner.Sequence));
        Assert.Equal(WorkerRunPhase.LaunchIntent, Assert.Single((await GmWorkerRunLedger.ObserveAsync(fixture.Target)).Entries).Phase);
        var bytes = File.ReadAllBytes(fixture.StatePath);
        Assert.Equal(WorkerLedgerMutationKind.Blocked, await owner.AbortBeforeLaunchAsync(entry, owner.Sequence));
        Assert.Equal(bytes, File.ReadAllBytes(fixture.StatePath));
        Assert.Empty(Directory.EnumerateFiles(Path.Combine(fixture.Target.DirectoryPath, "retired")));
    }

    [Fact]
    public async Task Uncertain_IsAbsorbingAndPreservesWorkspaceAndReservation()
    {
        using var fixture = new LedgerFixture();
        await using var owner = await fixture.OpenInitialized();
        var entry = await fixture.Prepare(owner);
        Directory.CreateDirectory(entry.Identity.WorkspacePath);
        var evidence = Path.Combine(entry.Identity.WorkspacePath, "retained.txt");
        File.WriteAllText(evidence, "original-evidence");
        Assert.Equal(WorkerLedgerMutationKind.Applied, await owner.MarkUncertainAsync(entry, owner.Sequence));
        Assert.Equal(WorkerRunPhase.Uncertain, Assert.Single((await GmWorkerRunLedger.ObserveAsync(fixture.Target)).Entries).Phase);
        var bytes = File.ReadAllBytes(fixture.StatePath);
        Assert.Equal(WorkerLedgerMutationKind.Blocked, await owner.PlanLaunchAsync(entry, owner.Sequence));
        Assert.Equal(WorkerLedgerMutationKind.Blocked, await owner.AbortBeforeLaunchAsync(entry, owner.Sequence));
        Assert.Equal(bytes, File.ReadAllBytes(fixture.StatePath));
        Assert.Equal("original-evidence", File.ReadAllText(evidence));
    }

    [Fact]
    public async Task PrelaunchAbort_CommitsExactArchiveBeforeRemovingReservationAndIsIdempotent()
    {
        using var fixture = new LedgerFixture();
        await using var owner = await fixture.OpenInitialized();
        var entry = await fixture.Prepare(owner);
        var expectedSequence = owner.Sequence;
        Assert.Equal(WorkerLedgerMutationKind.Applied, await owner.AbortBeforeLaunchAsync(entry, expectedSequence));
        var archive = Path.Combine(fixture.Target.DirectoryPath, "retired", entry.Identity.RunId + ".json");
        var bytes = File.ReadAllBytes(archive);
        Assert.Equal(new WorkerRunRecord(1, entry.Identity, WorkerRunPhase.AbortedBeforeLaunch), GmWorkerRunRecordCodec.Decode(bytes));
        var observed = await GmWorkerRunLedger.ObserveAsync(fixture.Target);
        Assert.Equal(WorkerRunObservationKind.Quiescent, observed.Kind);
        Assert.Equal(1, observed.EpochHighWater);
        Assert.Empty(observed.Entries);
        var stateBytes = File.ReadAllBytes(fixture.StatePath);
        Assert.Equal(WorkerLedgerMutationKind.AlreadyExact, await owner.AbortBeforeLaunchAsync(entry, expectedSequence));
        Assert.Equal(bytes, File.ReadAllBytes(archive));
        Assert.Equal(stateBytes, File.ReadAllBytes(fixture.StatePath));
        fixture.AssertSentinel();
    }

    [Fact]
    public async Task OriginalEntriesWithDifferentEpochs_RemainIndependentlyValid()
    {
        using var fixture = new LedgerFixture();
        await using var owner = await fixture.OpenInitialized();
        var first = await fixture.Prepare(owner, "first");
        var second = await fixture.Prepare(owner, "second");
        Assert.Equal(1, first.Identity.Epoch); Assert.Equal(2, second.Identity.Epoch);
        Assert.Equal(WorkerLedgerMutationKind.Applied, await owner.MarkUncertainAsync(first, owner.Sequence));
        Assert.Equal(WorkerLedgerMutationKind.Applied, await owner.AbortBeforeLaunchAsync(second, owner.Sequence));
        var observed = await GmWorkerRunLedger.ObserveAsync(fixture.Target);
        Assert.Equal(WorkerRunObservationKind.Uncertain, observed.Kind);
        Assert.Equal(first.Identity, Assert.Single(observed.Entries).Identity);
        Assert.Equal(WorkerRunPhase.Uncertain, observed.Entries[0].Phase);
        Assert.Equal(2, observed.EpochHighWater);
    }

    [Theory]
    [InlineData("forged")]
    [InlineData("foreign")]
    [InlineData("stale-sequence")]
    [InlineData("disposed")]
    public async Task UnboundHandlesAndStaleSequences_CannotMutate(string mode)
    {
        using var fixture = new LedgerFixture(); using var other = new LedgerFixture();
        await using var owner = await fixture.OpenInitialized();
        await using var otherOwner = await other.OpenInitialized();
        var original = await fixture.Prepare(owner);
        var entry = mode switch { "forged" => new WorkerRunEntryHandle(original.Identity), "foreign" => await other.Prepare(otherOwner), _ => original };
        var sequence = mode == "stale-sequence" ? owner.Sequence - 1 : owner.Sequence;
        if (mode == "disposed") await owner.DisposeAsync();
        var before = File.ReadAllBytes(fixture.StatePath);
        Assert.Equal(WorkerLedgerMutationKind.Blocked, await owner.PlanLaunchAsync(entry, sequence));
        Assert.Equal(WorkerLedgerMutationKind.Blocked, await owner.MarkUncertainAsync(entry, sequence));
        Assert.Equal(WorkerLedgerMutationKind.Blocked, await owner.AbortBeforeLaunchAsync(entry, sequence));
        Assert.Equal(before, File.ReadAllBytes(fixture.StatePath));
        Assert.Empty(Directory.EnumerateFiles(Path.Combine(fixture.Target.DirectoryPath, "retired")));
    }

    [Fact]
    public async Task StaleSequence_DoesNotConsumeOriginalNeverStartToken()
    {
        using var fixture = new LedgerFixture();
        await using var owner = await fixture.OpenInitialized();
        var entry = await fixture.Prepare(owner);
        Assert.Equal(WorkerLedgerMutationKind.Blocked, await owner.PlanLaunchAsync(entry, owner.Sequence - 1));
        Assert.Equal(WorkerLedgerMutationKind.Applied, await owner.AbortBeforeLaunchAsync(entry, owner.Sequence));
    }

    [Fact]
    public async Task CanceledMutation_PreservesBytesAndOriginalNeverStartToken()
    {
        using var fixture = new LedgerFixture();
        await using var owner = await fixture.OpenInitialized();
        var entry = await fixture.Prepare(owner);
        var before = File.ReadAllBytes(fixture.StatePath);
        using var canceled = new CancellationTokenSource(); canceled.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => owner.PlanLaunchAsync(entry, owner.Sequence, canceled.Token));
        Assert.Equal(before, File.ReadAllBytes(fixture.StatePath));
        Assert.Equal(WorkerLedgerMutationKind.Applied, await owner.AbortBeforeLaunchAsync(entry, owner.Sequence));
    }

    [Fact]
    public async Task Reopen_PreservesRetiredEpochAndRefusesExactTaskReuse()
    {
        using var fixture = new LedgerFixture();
        WorkerRunEntryHandle retired;
        await using (var owner = await fixture.OpenInitialized())
        {
            retired = await fixture.Prepare(owner);
            Assert.Equal(WorkerLedgerMutationKind.Applied, await owner.AbortBeforeLaunchAsync(retired, owner.Sequence));
        }
        await using var replacement = await GmWorkerRunLedger.OpenCoordinatorAsync(fixture.Target);
        Assert.NotNull(replacement);
        Assert.Equal(WorkerLedgerMutationKind.AlreadyExact, await replacement.InitializeAsync());
        Assert.Equal(WorkerLedgerMutationKind.Blocked, await replacement.AbortBeforeLaunchAsync(retired, replacement.Sequence));
        Assert.Equal(WorkerLedgerMutationKind.Blocked, (await replacement.PrepareAsync(fixture.Preparation() with { TaskSha256 = new string('b', 64) }, replacement.Sequence)).Kind);
        var fresh = await fixture.Prepare(replacement, "new-task");
        Assert.Equal(2, fresh.Identity.Epoch);
        Assert.NotEqual(retired.Identity.RunId, fresh.Identity.RunId);
        Assert.NotEqual(retired.Identity.HostInstanceId, fresh.Identity.HostInstanceId);
    }

    [Theory]
    [InlineData("state")]
    [InlineData("owner.lock")]
    [InlineData("journal.lock")]
    public async Task AlreadyInitialized_CannotHideChangedBytesOrLostLockBinding(string changed)
    {
        using var fixture = new LedgerFixture();
        await using var owner = await fixture.OpenInitialized();
        if (changed == "state") File.WriteAllText(fixture.StatePath, "different-state");
        else
        {
            var path = Path.Combine(fixture.Target.DirectoryPath, changed);
            File.Move(path, Path.Combine(fixture.Target.RootPath, changed + ".old")); File.WriteAllText(path, "replacement");
        }
        Assert.Equal(WorkerLedgerMutationKind.Blocked, await owner.InitializeAsync());
        Assert.Equal(WorkerLedgerMutationKind.Blocked, (await owner.PrepareAsync(fixture.Preparation(), owner.Sequence)).Kind);
        fixture.AssertSentinel();
    }

    [Fact]
    public async Task PreparedEntry_PersistsIdentityAndColdRestartCannotAdoptIt()
    {
        using var fixture = new LedgerFixture();
        WorkerRunIdentity identity;
        await using (var owner = await GmWorkerRunLedger.OpenCoordinatorAsync(fixture.Target))
        {
            Assert.NotNull(owner);
            Assert.Equal(WorkerLedgerMutationKind.Applied, await owner.InitializeAsync());
            var request = fixture.Preparation();
            var prepared = await owner.PrepareAsync(request, owner.Sequence);
            Assert.Equal(WorkerLedgerMutationKind.Applied, prepared.Kind);
            Assert.NotNull(prepared.Entry);
            identity = prepared.Entry.Identity;
            Assert.Equal(1, identity.Epoch);
            Assert.Equal(fixture.Target.RootPath, identity.RootKey);
            Assert.Equal(request.TaskSha256, identity.TaskSha256);
            Assert.Equal(request.WorkspacePath, identity.WorkspacePath);
            Assert.True(Guid.TryParseExact(identity.RunId, "N", out _));
            Assert.Equal(2, owner.Sequence);
        }
        var bytes = File.ReadAllBytes(fixture.StatePath);
        var observed = await GmWorkerRunLedger.ObserveAsync(fixture.Target);
        Assert.Equal(WorkerRunObservationKind.Uncertain, observed.Kind);
        Assert.Equal(2, observed.Sequence);
        Assert.Equal(1, observed.EpochHighWater);
        Assert.Equal(new WorkerRunRecord(1, identity, WorkerRunPhase.Prepared), Assert.Single(observed.Entries));
        await using var replacement = await GmWorkerRunLedger.OpenCoordinatorAsync(fixture.Target);
        Assert.Null(replacement);
        Assert.Equal(bytes, File.ReadAllBytes(fixture.StatePath));
        fixture.AssertSentinel();
    }

    [Theory]
    [InlineData("Prepared")]
    [InlineData("LaunchIntent")]
    [InlineData("ReleaseIntent")]
    [InlineData("Released")]
    [InlineData("StopValidated")]
    [InlineData("PublicationIntent")]
    [InlineData("Published")]
    [InlineData("CleanupPending")]
    [InlineData("Uncertain")]
    public async Task ColdActiveInventory_PreservesProgressWithoutReconstructingLiveOwner(string phase)
    {
        using var fixture = new LedgerFixture();
        await fixture.Initialize();
        var record = new WorkerRunRecord(1, GmWorkerRunRecordTests.Identity() with
        { RootKey = fixture.Target.RootPath, WorkspacePath = fixture.Preparation().WorkspacePath }, Enum.Parse<WorkerRunPhase>(phase));
        var state = JsonNode.Parse(File.ReadAllBytes(fixture.StatePath))!.AsObject();
        state["Sequence"] = 2; state["EpochHighWater"] = 1;
        state["Entries"] = new JsonArray(JsonNode.Parse(GmWorkerRunRecordTests.Bytes(record)));
        var bytes = Encoding.UTF8.GetBytes(state.ToJsonString());
        File.WriteAllBytes(fixture.StatePath, bytes); // Cold schema fixture, never a live permit.
        var observed = await GmWorkerRunLedger.ObserveAsync(fixture.Target);
        Assert.Equal(WorkerRunObservationKind.Uncertain, observed.Kind);
        Assert.Equal(record, Assert.Single(observed.Entries));
        await using var owner = await GmWorkerRunLedger.OpenCoordinatorAsync(fixture.Target);
        Assert.Null(owner);
        Assert.Equal(bytes, File.ReadAllBytes(fixture.StatePath));
        fixture.AssertSentinel();
    }

    [Theory]
    [InlineData("wrong-root")]
    [InlineData("schema")]
    [InlineData("unknown")]
    [InlineData("missing")]
    [InlineData("duplicate")]
    [InlineData("sequence-zero")]
    [InlineData("epoch-negative")]
    [InlineData("epoch-without-inventory")]
    [InlineData("epoch-overflow")]
    [InlineData("entries-wrong-type")]
    [InlineData("retired-wrong-type")]
    [InlineData("oversize")]
    [InlineData("invalid-utf8")]
    [InlineData("missing-state")]
    [InlineData("extra-temp")]
    public async Task InvalidInventory_IsBlockedAndNeverRepaired(string mutation)
    {
        using var fixture = new LedgerFixture();
        await fixture.Initialize();
        var state = JsonNode.Parse(File.ReadAllBytes(fixture.StatePath))!.AsObject();
        byte[]? bytes = null;
        switch (mutation)
        {
            case "wrong-root": state["RootKey"] = fixture.Target.RootPath + "-other"; break;
            case "schema": state["SchemaVersion"] = 2; break;
            case "unknown": state["unknown"] = "private-payload"; break;
            case "missing": state.Remove("Retired"); break;
            case "duplicate": bytes = Encoding.UTF8.GetBytes(state.ToJsonString().Replace("\"Sequence\":1", "\"Sequence\":1,\"Sequence\":1")); break;
            case "sequence-zero": state["Sequence"] = 0; break;
            case "epoch-negative": state["EpochHighWater"] = -1; break;
            case "epoch-without-inventory": state["EpochHighWater"] = 1; state["Sequence"] = 2; break;
            case "epoch-overflow": bytes = Encoding.UTF8.GetBytes(state.ToJsonString().Replace("\"EpochHighWater\":0", "\"EpochHighWater\":9223372036854775808")); break;
            case "entries-wrong-type": state["Entries"] = false; break;
            case "retired-wrong-type": state["Retired"] = false; break;
            case "oversize": bytes = new byte[4 * 1024 * 1024 + 1]; break;
            case "invalid-utf8": bytes = [0xff]; break;
            case "missing-state": break;
            case "extra-temp": File.WriteAllText(Path.Combine(fixture.Target.DirectoryPath, "old.tmp"), "retained-evidence"); break;
            default: throw new InvalidOperationException("Unknown mutation.");
        }
        bytes ??= Encoding.UTF8.GetBytes(state.ToJsonString());
        if (mutation == "missing-state") File.Delete(fixture.StatePath); else File.WriteAllBytes(fixture.StatePath, bytes);
        Assert.Equal(WorkerRunObservationKind.Blocked, (await GmWorkerRunLedger.ObserveAsync(fixture.Target)).Kind);
        await using var owner = await GmWorkerRunLedger.OpenCoordinatorAsync(fixture.Target);
        Assert.Null(owner);
        if (mutation == "missing-state") Assert.False(File.Exists(fixture.StatePath));
        else Assert.Equal(bytes, File.ReadAllBytes(fixture.StatePath));
        if (mutation == "extra-temp") Assert.Equal("retained-evidence", File.ReadAllText(Path.Combine(fixture.Target.DirectoryPath, "old.tmp")));
        fixture.AssertSentinel();
    }

    [Fact]
    public async Task Initialize_PersistsBoundRootStateAndStableLocks()
    {
        using var fixture = new LedgerFixture();
        await using var owner = await GmWorkerRunLedger.OpenCoordinatorAsync(fixture.Target);
        Assert.NotNull(owner);
        Assert.Equal(WorkerLedgerMutationKind.Applied, await owner.InitializeAsync());
        var state = Path.Combine(fixture.Target.DirectoryPath, "state.json");
        Assert.True(File.Exists(state), "Initialization must persist an actual bounded ledger before acknowledging.");
        using var json = JsonDocument.Parse(await File.ReadAllBytesAsync(state));
        Assert.Equal(1, json.RootElement.GetProperty("SchemaVersion").GetInt32());
        Assert.Equal(fixture.Target.RootPath, json.RootElement.GetProperty("RootKey").GetString());
        Assert.Equal(1, json.RootElement.GetProperty("Sequence").GetInt64());
        Assert.Equal(0, json.RootElement.GetProperty("EpochHighWater").GetInt64());
        Assert.Empty(json.RootElement.GetProperty("Entries").EnumerateArray());
        Assert.Empty(json.RootElement.GetProperty("Retired").EnumerateArray());
        Assert.True(File.Exists(Path.Combine(fixture.Target.DirectoryPath, "owner.lock")));
        Assert.True(File.Exists(Path.Combine(fixture.Target.DirectoryPath, "journal.lock")));
        Assert.True(Directory.Exists(Path.Combine(fixture.Target.DirectoryPath, "retired")));
        var observed = await GmWorkerRunLedger.ObserveAsync(fixture.Target);
        Assert.Equal(WorkerRunObservationKind.Quiescent, observed.Kind);
        Assert.Equal(1, observed.Sequence);
        fixture.AssertSentinel();
    }

    [Fact]
    public async Task ColdMissing_IsReadOnlyAndDoesNotCreateNamespace()
    {
        using var fixture = new LedgerFixture();
        Assert.Equal(WorkerRunObservationKind.Missing, (await GmWorkerRunLedger.ObserveAsync(fixture.Target)).Kind);
        Assert.Empty(Directory.EnumerateFileSystemEntries(fixture.Target.RootPath));
        fixture.AssertSentinel();
    }

    [Fact]
    public async Task ExistingEmptyNamespace_IsBlockedWithoutBootstrapRepair()
    {
        using var fixture = new LedgerFixture();
        Directory.CreateDirectory(fixture.Target.DirectoryPath);
        Assert.Equal(WorkerRunObservationKind.Blocked, (await GmWorkerRunLedger.ObserveAsync(fixture.Target)).Kind);
        await using var owner = await GmWorkerRunLedger.OpenCoordinatorAsync(fixture.Target);
        Assert.Null(owner);
        Assert.Empty(Directory.EnumerateFileSystemEntries(fixture.Target.DirectoryPath));
        fixture.AssertSentinel();
    }

    private sealed class LedgerFixture : IDisposable
    {
        private readonly string _container = Path.Combine(Path.GetTempPath(), "boe-ledger-" + Guid.NewGuid().ToString("N"));
        internal WorkerLedgerTarget Target { get; }
        internal string StatePath => Path.Combine(Target.DirectoryPath, "state.json");
        internal LedgerFixture()
        {
            Assert.True(OperatingSystem.IsLinux(), "Select this native persistence category only on Linux.");
            Target = new(Path.Combine(_container, "root"));
            Directory.CreateDirectory(Target.RootPath);
            File.WriteAllText(Path.Combine(_container, "outside.txt"), "outside-sentinel");
        }
        internal void AssertSentinel() => Assert.Equal("outside-sentinel", File.ReadAllText(Path.Combine(_container, "outside.txt")));
        internal WorkerRunPreparation Preparation(string task = "task") => new(
            "22222222222222222222222222222222", "worker", task, new string('a', 64),
            WorkerRunBackend.LinuxNativeLineage, WorkerRunScope.OrdinarySamePidNamespace,
            Path.Combine(_container, "workspace"));
        internal async Task Initialize()
        {
            await using var owner = await GmWorkerRunLedger.OpenCoordinatorAsync(Target);
            Assert.NotNull(owner);
            Assert.Equal(WorkerLedgerMutationKind.Applied, await owner.InitializeAsync());
        }
        internal async Task<WorkerRunLedgerCoordinator> OpenInitialized()
        {
            var owner = await GmWorkerRunLedger.OpenCoordinatorAsync(Target);
            Assert.NotNull(owner);
            Assert.Equal(WorkerLedgerMutationKind.Applied, await owner.InitializeAsync());
            return owner;
        }
        internal async Task<WorkerRunEntryHandle> Prepare(WorkerRunLedgerCoordinator owner, string task = "task")
        {
            var result = await owner.PrepareAsync(Preparation(task), owner.Sequence);
            Assert.Equal(WorkerLedgerMutationKind.Applied, result.Kind);
            Assert.NotNull(result.Entry);
            return result.Entry;
        }
        public void Dispose() => Directory.Delete(_container, recursive: true);
    }
}
