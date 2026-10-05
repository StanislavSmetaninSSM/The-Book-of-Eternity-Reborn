using BookOfEternityClient.Services.GmWorkers;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed class GmWorkerConsumerEvidenceTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void LegacySuccessShapedRecord_DoesNotAuthorizeEitherConsumer(bool repair)
    {
        var task = repair ? GmWorkerBridgeTestFixtures.ValidationRepairTask() : GmWorkerBridgeTestFixtures.AnalysisTask();
        var run = SuccessShapedRecord(task);
        Assert.False(Accept(repair, run, task));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void FailedButStoppedWithCopy_CannotBorrowCleanupEvidence(bool repair)
    {
        var task = repair ? GmWorkerBridgeTestFixtures.ValidationRepairTask() : GmWorkerBridgeTestFixtures.AnalysisTask();
        var identity = new GmWorkerExecutionIdentity("failed-run", GmWorkerBackend.NativeLineage,
            GmWorkerBackendSelector.NativeGuarantee);
        var authority = new GmWorkerExecutionAuthority(identity, task);
        Assert.True(authority.ObserveStop(new(identity.RunId, identity.Backend, identity.Guarantee,
            GmWorkerStopState.StoppedWithinScope, "reaped", true, false, 0)));
        authority.ObserveCompletion(1);
        var failed = SuccessShapedRecord(task) with { ExecutionAuthority = authority, ExitCode = 1, Proposal = null };
        var forged = failed with { ExitCode = 0, Proposal = SuccessShapedRecord(task).Proposal };
        Assert.False(Accept(repair, forged, task));
    }

    private static bool Accept(bool repair, GmWorkerTaskRunResult run, WorkerTaskPacket task) => repair
        ? GmWorkerValidationRepairDelegator.CanAcceptExecution(run, task)
        : GmWorkerProposalOnlyDispatchService.CanAcceptExecution(run, task);

    private static GmWorkerTaskRunResult SuccessShapedRecord(WorkerTaskPacket task) => new()
    {
        Status = new() { WorkerId = task.WorkerId, CurrentTaskId = task.TaskId, State = WorkerBridgeState.Stopped },
        ExitCode = 0,
        BoundTask = task,
        Proposal = new() { ProposalId = "synthetic-record", WorkerId = task.WorkerId, TaskId = task.TaskId }
    };
}
