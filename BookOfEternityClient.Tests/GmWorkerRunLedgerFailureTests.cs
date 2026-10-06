using System.Text.Json.Nodes;
using BookOfEternityClient.Services.GmWorkers;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed class GmWorkerRunLedgerFailureTests
{
    public static IEnumerable<object[]> FaultCuts()
    {
        foreach (var operation in new[] { "initialize", "prepare", "launch", "uncertain", "abort" })
        foreach (var stage in new[] { WorkerLedgerIoStage.BeforeStateWrite, WorkerLedgerIoStage.StateWritten,
            WorkerLedgerIoStage.StateFlushed, WorkerLedgerIoStage.StateRenamed, WorkerLedgerIoStage.StateDirectorySynced })
            yield return [operation, stage.ToString()];
        foreach (var stage in new[] { WorkerLedgerIoStage.BeforeArchiveWrite, WorkerLedgerIoStage.ArchiveWritten,
            WorkerLedgerIoStage.ArchiveFlushed, WorkerLedgerIoStage.ArchiveDirectorySynced }) yield return ["abort", stage.ToString()];
    }

    [Theory, MemberData(nameof(FaultCuts))]
    public async Task AmbiguousWrite_RetainsOriginalOwnerAndRetriesFrozenTransaction(string operation, string cut)
    {
        using var fixture = new Fixture();
        var armed = false; var reached = false;
        await using var owner = await fixture.Open(stage =>
        {
            if (armed && stage.ToString() == cut) { reached = true; armed = false; throw new IOException("finite fixture fault"); }
        });
        WorkerRunEntryHandle? entry = null;
        if (operation != "initialize") Assert.Equal(WorkerLedgerMutationKind.Applied, await owner.InitializeAsync());
        if (operation is "launch" or "uncertain" or "abort") entry = await fixture.Prepare(owner);
        var beforeSequence = owner.Sequence;
        armed = true;
        var result = operation switch
        {
            "initialize" => await owner.InitializeAsync(),
            "prepare" => (await owner.PrepareAsync(fixture.Request(), owner.Sequence)).Kind,
            "launch" => await owner.PlanLaunchAsync(entry!, owner.Sequence),
            "uncertain" => await owner.MarkUncertainAsync(entry!, owner.Sequence),
            _ => await owner.AbortBeforeLaunchAsync(entry!, owner.Sequence)
        };
        Assert.True(reached, "The actual I/O boundary must be exercised.");
        Assert.Equal(WorkerLedgerMutationKind.CommitPending, result);
        Assert.Equal(beforeSequence, owner.Sequence); // Never a false live acknowledgement.
        await using (var contender = await GmWorkerRunLedger.OpenCoordinatorAsync(fixture.Target)) Assert.Null(contender);
        Assert.Equal(WorkerLedgerMutationKind.Blocked, (await owner.PrepareAsync(fixture.Request("other"), owner.Sequence)).Kind);
        if (entry is not null) Assert.Equal(WorkerLedgerMutationKind.Blocked, await owner.AbortBeforeLaunchAsync(entry, owner.Sequence));
        var retried = await owner.RetryPendingAsync();
        Assert.Equal(WorkerLedgerMutationKind.Applied, retried.Kind);
        Assert.Equal(beforeSequence + 1, owner.Sequence);
        Assert.Equal(operation == "prepare", retried.Entry is not null);
        var observed = await GmWorkerRunLedger.ObserveAsync(fixture.Target);
        if (operation is "initialize" or "abort") Assert.Equal(WorkerRunObservationKind.Quiescent, observed.Kind);
        else
        {
            Assert.Equal(WorkerRunObservationKind.Uncertain, observed.Kind);
            Assert.Equal(operation switch { "prepare" => WorkerRunPhase.Prepared, "launch" => WorkerRunPhase.LaunchIntent, _ => WorkerRunPhase.Uncertain }, Assert.Single(observed.Entries).Phase);
        }
        Assert.Empty(Directory.EnumerateFiles(fixture.Target.DirectoryPath, "*.tmp"));
        Assert.Equal(WorkerLedgerMutationKind.Blocked, (await owner.RetryPendingAsync()).Kind);
        if (operation == "abort") Assert.Equal(WorkerLedgerMutationKind.AlreadyExact, await owner.AbortBeforeLaunchAsync(entry!, beforeSequence));
        fixture.AssertSentinel();
    }

    [Theory]
    [InlineData("state")]
    [InlineData("temporary")]
    [InlineData("owner.lock")]
    [InlineData("journal.lock")]
    public async Task PendingConflict_BlocksWithoutOverwritingEvidence(string conflict)
    {
        using var fixture = new Fixture(); var armed = false;
        await using var owner = await fixture.Open(stage => { if (armed && stage == WorkerLedgerIoStage.StateFlushed) { armed = false; throw new IOException("cut"); } });
        Assert.Equal(WorkerLedgerMutationKind.Applied, await owner.InitializeAsync());
        armed = true;
        Assert.Equal(WorkerLedgerMutationKind.CommitPending, (await owner.PrepareAsync(fixture.Request(), owner.Sequence)).Kind);
        var path = conflict switch { "state" => fixture.State, "temporary" => Assert.Single(Directory.EnumerateFiles(fixture.Target.DirectoryPath, "*.tmp")), _ => Path.Combine(fixture.Target.DirectoryPath, conflict) };
        if (conflict.EndsWith(".lock")) File.Move(path, Path.Combine(fixture.Target.RootPath, conflict + ".old"));
        File.WriteAllText(path, "retain-conflicting-evidence");
        Assert.Equal(WorkerLedgerMutationKind.Blocked, (await owner.RetryPendingAsync()).Kind);
        Assert.Equal("retain-conflicting-evidence", File.ReadAllText(path));
        Assert.Equal(WorkerLedgerMutationKind.Blocked, (await owner.PrepareAsync(fixture.Request("other"), owner.Sequence)).Kind);
        fixture.AssertSentinel();
    }

    [Fact]
    public async Task CancellationAndDisposeDuringWrite_WaitForOriginalIoAndRetainLock()
    {
        using var fixture = new Fixture(); using var cancel = new CancellationTokenSource();
        using var release = new ManualResetEventSlim(); var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously); var armed = false;
        await using var owner = await fixture.Open(stage =>
        {
            if (armed && stage == WorkerLedgerIoStage.StateWritten)
            { entered.TrySetResult(); if (!release.Wait(TimeSpan.FromSeconds(10))) throw new IOException("fixture deadline"); }
        });
        Assert.Equal(WorkerLedgerMutationKind.Applied, await owner.InitializeAsync());
        armed = true;
        var writing = Task.Run(() => owner.PrepareAsync(fixture.Request(), owner.Sequence, cancel.Token));
        Task? disposing = null;
        try
        {
            await entered.Task.WaitAsync(TimeSpan.FromSeconds(3));
            cancel.Cancel(); disposing = owner.DisposeAsync().AsTask();
            Assert.False(writing.IsCompleted); Assert.False(disposing.IsCompleted);
            await using var contender = await GmWorkerRunLedger.OpenCoordinatorAsync(fixture.Target); Assert.Null(contender);
        }
        finally { release.Set(); await writing; if (disposing is not null) await disposing; }
        Assert.Equal(WorkerLedgerMutationKind.Applied, (await writing).Kind);
        Assert.Equal(WorkerRunObservationKind.Uncertain, (await GmWorkerRunLedger.ObserveAsync(fixture.Target)).Kind);
        await using var cold = await GmWorkerRunLedger.OpenCoordinatorAsync(fixture.Target); Assert.Null(cold);
    }

    [Fact]
    public async Task CapacityAndTaskIdentityBounds_RefuseBeforeMutation()
    {
        using var fixture = new Fixture(); await using var owner = await fixture.Open(); await owner.InitializeAsync();
        for (var i = 0; i < 32; i++) await fixture.Prepare(owner, "task-" + i);
        var before = File.ReadAllBytes(fixture.State);
        Assert.Equal(WorkerLedgerMutationKind.Blocked, (await owner.PrepareAsync(fixture.Request("extra"), owner.Sequence)).Kind);
        Assert.Equal(WorkerLedgerMutationKind.Blocked, (await owner.PrepareAsync(fixture.Request("task-0") with { TaskSha256 = new string('b', 64) }, owner.Sequence)).Kind);
        Assert.Equal(before, File.ReadAllBytes(fixture.State)); Assert.Equal(32, (await GmWorkerRunLedger.ObserveAsync(fixture.Target)).Entries.Count);
    }

    [Theory]
    [InlineData("generation")]
    [InlineData("digest")]
    [InlineData("backend-scope")]
    [InlineData("workspace")]
    public async Task InvalidPreparation_IsReadOnly(string invalid)
    {
        using var fixture = new Fixture(); await using var owner = await fixture.Open(); await owner.InitializeAsync();
        var request = fixture.Request(); request = invalid switch
        { "generation" => request with { GenerationId = "" }, "digest" => request with { TaskSha256 = "wrong" },
          "backend-scope" => request with { Scope = WorkerRunScope.WindowsJob }, _ => request with { WorkspacePath = "relative" } };
        var before = File.ReadAllBytes(fixture.State);
        Assert.Equal(WorkerLedgerMutationKind.Blocked, (await owner.PrepareAsync(request, owner.Sequence)).Kind);
        Assert.Equal(before, File.ReadAllBytes(fixture.State));
    }

    [Theory]
    [InlineData("missing")]
    [InlineData("corrupt")]
    [InlineData("unknown")]
    [InlineData("symlink")]
    [InlineData("hash")]
    [InlineData("epoch")]
    [InlineData("duplicate-reference")]
    public async Task InconsistentArchiveIndex_NeverLooksQuiescent(string corruption)
    {
        using var fixture = new Fixture(); string archive;
        await using (var owner = await fixture.Open())
        { await owner.InitializeAsync(); var entry = await fixture.Prepare(owner); await owner.AbortBeforeLaunchAsync(entry, owner.Sequence); archive = Path.Combine(fixture.Target.DirectoryPath, "retired", entry.Identity.RunId + ".json"); }
        switch (corruption)
        {
            case "missing": File.Delete(archive); break;
            case "corrupt": File.WriteAllText(archive, "bad"); break;
            case "unknown": File.WriteAllText(Path.Combine(Path.GetDirectoryName(archive)!, "unknown.json"), "bad"); break;
            case "symlink": File.Delete(archive); File.CreateSymbolicLink(archive, fixture.Sentinel); break;
            default:
                var state = JsonNode.Parse(File.ReadAllBytes(fixture.State))!; var references = state["Retired"]!.AsArray();
                if (corruption == "hash") references[0]!["RecordSha256"] = new string('0', 64);
                if (corruption == "epoch") references[0]!["Epoch"] = 2;
                if (corruption == "duplicate-reference") references.Add(references[0]!.DeepClone());
                File.WriteAllText(fixture.State, state.ToJsonString()); break;
        }
        var before = File.ReadAllBytes(fixture.State);
        Assert.Equal(WorkerRunObservationKind.Blocked, (await GmWorkerRunLedger.ObserveAsync(fixture.Target)).Kind);
        await using var cold = await GmWorkerRunLedger.OpenCoordinatorAsync(fixture.Target); Assert.Null(cold);
        Assert.Equal(before, File.ReadAllBytes(fixture.State)); fixture.AssertSentinel();
    }

    [Theory]
    [InlineData("root")]
    [InlineData("runtime")]
    [InlineData("namespace")]
    [InlineData("owner.lock")]
    [InlineData("journal.lock")]
    [InlineData("state.json")]
    [InlineData("retired")]
    public async Task NonordinaryLedgerPath_IsBlockedWithoutFollowingOutsideLink(string component)
    {
        using var fixture = new Fixture();
        await using (var owner = await fixture.Open()) await owner.InitializeAsync();
        var path = component switch { "root" => fixture.Target.RootPath, "runtime" => Path.GetDirectoryName(fixture.Target.DirectoryPath)!,
            "namespace" => fixture.Target.DirectoryPath, _ => Path.Combine(fixture.Target.DirectoryPath, component) };
        var destination = Path.Combine(fixture.Container, "retained-original");
        if (Directory.Exists(path)) { Directory.Move(path, destination); Directory.CreateSymbolicLink(path, fixture.Container); }
        else { File.Move(path, destination); File.CreateSymbolicLink(path, fixture.Sentinel); }
        Assert.Equal(WorkerRunObservationKind.Blocked, (await GmWorkerRunLedger.ObserveAsync(fixture.Target)).Kind);
        await using var cold = await GmWorkerRunLedger.OpenCoordinatorAsync(fixture.Target); Assert.Null(cold);
        fixture.AssertSentinel();
    }

    [Fact]
    public async Task SequenceOverflowAndLifetimeInventoryBound_RefuseNewAllocation()
    {
        using var fixture = new Fixture();
        await using (var owner = await fixture.Open())
        { await owner.InitializeAsync(); var entry = await fixture.Prepare(owner); await owner.AbortBeforeLaunchAsync(entry, owner.Sequence); }
        var json = JsonNode.Parse(File.ReadAllBytes(fixture.State))!; json["Sequence"] = long.MaxValue;
        File.WriteAllText(fixture.State, json.ToJsonString()); var before = File.ReadAllBytes(fixture.State);
        await using (var reopened = await fixture.Open())
            Assert.Equal(WorkerLedgerMutationKind.Blocked, (await reopened.PrepareAsync(fixture.Request("next"), reopened.Sequence)).Kind);
        Assert.Equal(before, File.ReadAllBytes(fixture.State));
        var references = Enumerable.Range(1, 4096).Select(i => new WorkerRunRetiredReference(i.ToString("x32"), i,
            i.ToString("x64"), new string('a', 64))).ToArray();
        var full = new WorkerLedgerState(1, fixture.Target.RootPath, 4097, 4096, [], references);
        var record = new WorkerRunRecord(1, GmWorkerRunRecordTests.Identity() with
            { RootKey = fixture.Target.RootPath, Epoch = 4097, RunId = 4097.ToString("x32") }, WorkerRunPhase.Prepared);
        Assert.Throws<InvalidDataException>(() => GmWorkerRunLedgerCodec.AddPrepared(full, record));
    }

    internal sealed class Fixture : IDisposable
    {
        internal string Container { get; } = Path.Combine(Path.GetTempPath(), "boe-ledger-fault-" + Guid.NewGuid().ToString("N"));
        internal WorkerLedgerTarget Target { get; }
        internal string State => Path.Combine(Target.DirectoryPath, "state.json");
        internal string Sentinel => Path.Combine(Container, "outside.txt");
        internal Fixture() { Target = new(Path.Combine(Container, "root")); Directory.CreateDirectory(Target.RootPath); File.WriteAllText(Sentinel, "outside"); }
        internal void AssertSentinel() => Assert.Equal("outside", File.ReadAllText(Sentinel));
        internal WorkerRunPreparation Request(string task = "task") => new("22222222222222222222222222222222", "worker", task, new string('a', 64), WorkerRunBackend.LinuxNativeLineage, WorkerRunScope.OrdinarySamePidNamespace, Path.Combine(Container, "workspace"));
        internal async Task<WorkerRunLedgerCoordinator> Open(Action<WorkerLedgerIoStage>? observe = null) { var owner = await GmWorkerRunLedger.OpenCoordinatorAsync(Target, observeIo: observe); Assert.NotNull(owner); return owner; }
        internal async Task<WorkerRunEntryHandle> Prepare(WorkerRunLedgerCoordinator owner, string task = "task") { var result = await owner.PrepareAsync(Request(task), owner.Sequence); Assert.Equal(WorkerLedgerMutationKind.Applied, result.Kind); Assert.NotNull(result.Entry); return result.Entry; }
        public void Dispose() => Directory.Delete(Container, true);
    }
}
