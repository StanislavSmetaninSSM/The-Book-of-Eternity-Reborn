using System.Security.Cryptography;
using System.Text;

namespace BookOfEternityClient.Services.GmWorkers;

public sealed record GmWorkerExecutionIdentity(string RunId, GmWorkerBackend Backend, string Guarantee);

internal sealed record GmWorkerCleanupEvidence(bool NoLaunch, GmWorkerStopEvidence? Stop);

// Cleanup evidence and an accepted publication are distinct. Only this retained
// per-execution state can issue a success permit; public result records cannot.
internal sealed class GmWorkerExecutionAuthority
{
    private readonly object _sync = new();
    private readonly string _reservedBytesDigest, _taskModelDigest, _workerId, _taskId, _generation;
    private readonly bool _noLaunch;
    private string? _uncertainty;
    private GmWorkerStopEvidence? _observedStop, _validatedStop;
    private GmWorkerOwnedOutputs? _outputs;
    private int? _completion;
    private Publication? _publication;

    internal GmWorkerExecutionAuthority(GmWorkerExecutionIdentity identity, WorkerTaskPacket task, byte[]? reservedBytes = null)
        : this(identity, task, reservedBytes, noLaunch: false) { }

    private GmWorkerExecutionAuthority(GmWorkerExecutionIdentity? identity, WorkerTaskPacket task, byte[]? reservedBytes, bool noLaunch)
    {
        Identity = identity; _noLaunch = noLaunch;
        _workerId = task.WorkerId; _taskId = task.TaskId; _generation = task.SessionGeneration;
        var bytes = Encoding.UTF8.GetBytes(GmWorkerJson.Serialize(task));
        _reservedBytesDigest = Digest(reservedBytes ?? bytes);
        _taskModelDigest = Digest(bytes);
        if (reservedBytes != null)
        {
            try
            {
                var text = Encoding.UTF8.GetString(reservedBytes);
                var reserved = GmWorkerJson.Deserialize<WorkerTaskPacket>(text.TrimStart('\uFEFF'));
                if (reserved == null || ModelDigest(reserved) != _taskModelDigest)
                    _uncertainty = "Execution authority does not match the exact reserved task bytes.";
            }
            catch { _uncertainty = "Execution task binding could not be read."; }
        }
    }

    internal static GmWorkerExecutionAuthority NoLaunch(WorkerTaskPacket task, byte[]? reservedBytes = null) =>
        new(null, task, reservedBytes, noLaunch: true);
    internal GmWorkerExecutionIdentity? Identity { get; }
    internal bool OutputsSettled { get { lock (_sync) return _outputs != null && _uncertainty == null; } }
    internal bool IsUncertain { get { lock (_sync) return _uncertainty != null; } }
    internal string? UncertaintyReason { get { lock (_sync) return _uncertainty; } }
    internal GmWorkerOwnedOutputs? Outputs { get { lock (_sync) return _outputs; } }
    internal GmWorkerStopEvidence? StopEvidence
    {
        get
        {
            lock (_sync)
            {
                if (_uncertainty == null) return _observedStop;
                return _observedStop is { } observed
                    ? observed with { State = GmWorkerStopState.Uncertain, Reason = _uncertainty }
                    : Identity == null ? null : new(Identity.RunId, Identity.Backend, Identity.Guarantee,
                        GmWorkerStopState.Uncertain, _uncertainty, false, false, null);
            }
        }
    }

    internal bool ObserveStop(GmWorkerStopEvidence evidence)
    {
        lock (_sync)
        {
            _observedStop = evidence;
            if (_uncertainty != null) return false;
            if (Identity == null || string.IsNullOrWhiteSpace(Identity.RunId) ||
                (Identity.Backend != GmWorkerBackend.NativeLineage && Identity.Backend != GmWorkerBackend.WindowsJob) ||
                Identity.Guarantee != (Identity.Backend == GmWorkerBackend.NativeLineage ? GmWorkerBackendSelector.NativeGuarantee : "windows-job") ||
                evidence.RunId != Identity.RunId || evidence.Backend != Identity.Backend || evidence.Guarantee != Identity.Guarantee ||
                evidence.State != GmWorkerStopState.StoppedWithinScope || !evidence.CleanupComplete || evidence.AuthorityRetained)
            {
                _uncertainty = "Stop evidence is uncertain, malformed or does not match the original execution.";
                return false;
            }
            _validatedStop = evidence;
            return true;
        }
    }

    internal void ObserveUncertainty(string reason) { lock (_sync) _uncertainty ??= reason; }
    internal void ObserveCompletion(int exitCode) { lock (_sync) _completion = exitCode; }

    internal async Task<GmWorkerCleanupEvidence> StopForCleanupAsync(GmWorkerOwnedLaunch? owner)
    {
        lock (_sync)
        {
            if (_noLaunch && _uncertainty == null) return new(true, null);
            if (_validatedStop != null && _uncertainty == null) return new(false, _validatedStop);
        }
        if (owner == null || owner.Identity != Identity)
        {
            ObserveUncertainty("Original launch owner is missing or mismatched.");
            throw new InvalidOperationException(UncertaintyReason);
        }
        GmWorkerStopEvidence evidence;
        try { evidence = await owner.StopAndObserveAsync(); }
        catch
        {
            // Only the same retained assigned Job can retry a pending observation.
            if (!owner.RetainsAssignedWindowsJob) ObserveUncertainty("Original stop observation failed.");
            throw;
        }
        if (!ObserveStop(evidence)) throw new InvalidOperationException(UncertaintyReason);
        return new(false, evidence);
    }

    internal async Task<GmWorkerOwnedOutputs> SettleOutputsAsync(GmWorkerOwnedLaunch owner)
    {
        lock (_sync)
        {
            RequireScopedStop();
            if (_outputs != null) return _outputs;
        }
        try
        {
            if (owner.Identity != Identity) throw new InvalidOperationException("Output owner does not match this execution.");
            var outputs = await owner.SettleOutputsAsync();
            lock (_sync) { RequireScopedStop(); _outputs = outputs; return outputs; }
        }
        catch { ObserveUncertainty("Owned output settlement failed or exceeded its observation bound."); throw; }
    }

    internal GmWorkerCleanupEvidence RequireCleanupEvidence()
    {
        lock (_sync)
        {
            if (_noLaunch && _uncertainty == null) return new(true, null);
            RequireScopedStop();
            if (_outputs == null) throw new InvalidOperationException("Cleanup retains unsettled owned outputs.");
            return new(false, _validatedStop);
        }
    }

    internal void RequirePublication()
    {
        lock (_sync)
        {
            RequireScopedStop();
            if (_outputs == null || _completion != 0)
                throw new InvalidOperationException("Publication requires correlated worker success and settled owned outputs.");
        }
    }

    internal void RecordPublication(WorkerProposal proposal, byte[] publishedBytes)
    {
        lock (_sync)
        {
            RequirePublication();
            if (proposal.WorkerId != _workerId || proposal.TaskId != _taskId || _publication != null)
                throw new InvalidOperationException("Publication does not match the original execution or was already recorded.");
            _publication = new(proposal.ProposalId, _reservedBytesDigest, Digest(publishedBytes), ModelDigest(proposal));
        }
    }

    internal bool Allows(GmWorkerTaskRunResult result, WorkerTaskPacket requested)
    {
        lock (_sync)
        {
            if (_uncertainty != null || _publication == null || _validatedStop == null || _outputs == null || _completion != 0 ||
                result.TimedOut || result.SessionReplaced || result.ExitCode != _completion || result.Status.State != WorkerBridgeState.Stopped ||
                result.Status.WorkerId != _workerId || result.Status.CurrentTaskId != _taskId || result.ExecutionIdentity != Identity ||
                result.StopEvidence != _validatedStop || !result.OutputsSettled || result.BoundTask == null || result.Proposal == null ||
                result.Proposal.ProposalId != _publication.ProposalId || _publication.ReservedBytesDigest != _reservedBytesDigest) return false;
            try
            {
                return requested.WorkerId == _workerId && requested.TaskId == _taskId && requested.SessionGeneration == _generation &&
                    ModelDigest(requested) == _taskModelDigest && ModelDigest(result.BoundTask) == _taskModelDigest &&
                    ModelDigest(result.Proposal) == _publication.ModelDigest;
            }
            catch { return false; }
        }
    }

    private void RequireScopedStop()
    {
        if (_noLaunch || _uncertainty != null || _validatedStop == null)
            throw new InvalidOperationException(_uncertainty ?? "Matching scoped stop evidence is required.");
    }
    private static string ModelDigest<T>(T value) => Digest(Encoding.UTF8.GetBytes(GmWorkerJson.Serialize(value)));
    private static string Digest(byte[] bytes) => Convert.ToHexString(SHA256.HashData(bytes));
    private sealed record Publication(string ProposalId, string ReservedBytesDigest, string PublishedBytesDigest, string ModelDigest);
}
