using System.Text.Json.Nodes;
using BookOfEternityClient.Core;

namespace BookOfEternityClient.Services;

public partial class ValidationService
{
    internal sealed partial class SpiritualOriginalTurnCapture
    {
        // These are the actual consumed capabilities, not a reconstructed alias
        // dictionary or a claim of current-generation wound admission. E needs
        // their staged candidate images and ordering to reconstruct this history.
        private sealed record ConsumedSpiritualContinuation(
            SpiritualWoundSourceSession.PreparedContinuation SourceTicket,
            long SourceRevisionBefore,
            long SourceRevisionAfter,
            AcceptedMechanicsPlanner.ResourceExecutionStep PreviousStep,
            AcceptedMechanicsPlanner.ResourceExecutionStep AcceptedStep,
            AcceptedMechanicsPlanner.ResourceExecutionSession.MissingSidePreparation? MissingSide,
            JsonArray? Receipts);

        private readonly List<ConsumedSpiritualContinuation> _consumedContinuations = new();

        internal JsonObject ReadPendingResourceRequest(FileSystemManager.CanonicalWriteLease lease,
            AcceptedMechanicsPlanner.PendingSpiritualResourceExchange expected)
        {
            ArgumentNullException.ThrowIfNull(expected);
            // A synchronous reader cannot race a continuation using this capture.
            if (!_gate.Wait(0))
                throw new InvalidOperationException("The original capture is continuing.");
            try
            {
                EnsureCurrent(lease);
                if (_resources == null || !ReferenceEquals(_lastResourceStep?.PendingResource, expected))
                    throw new InvalidOperationException("Read the exact retained resource wait.");
                return _resources.ReadPendingResourceRequest(expected);
            }
            finally { _gate.Release(); }
        }

        // Receipts are commands from the actual safe packet. The retained typed
        // resolver validates them; editing the canonical command file is still
        // forbidden by CheckRetainedInputsAsync. Canonical intake belongs to E.
        internal async Task<AcceptedMechanicsPlanner.ResourceContinuationResult> ResumePendingResourceAsync(
            FileSystemManager.CanonicalWriteLease lease,
            AcceptedMechanicsPlanner.PendingSpiritualResourceExchange expected,
            JsonArray receipts)
        {
            ArgumentNullException.ThrowIfNull(expected);
            ArgumentNullException.ThrowIfNull(receipts);
            // Freeze caller commands before the first await. This is data, not
            // validity: ResumePendingResource must still check every receipt.
            var capturedReceipts = receipts.DeepClone().AsArray();
            await _gate.WaitAsync();
            var attemptedResourceResume = false;
            try
            {
                EnsureCurrent(lease);
                if (_resources == null || !ReferenceEquals(_lastResourceStep?.PendingResource, expected))
                    return ResourceFailure("spiritual_resource_wait_mismatch", "the exact retained resource wait");
                var issues = await CheckRetainedInputsAsync(lease);
                if (issues.Count != 0)
                    return new(null, issues);
                if (_usesOriginalPrefix && _originalPrefix == null)
                {
                    using var prefixAllocations = _allocations.Clock.BeginSpeculation();
                    attemptedResourceResume = true;
                    var prefixResult = _resources.ResumePendingResource(expected, capturedReceipts);
                    var acceptedPrefix = prefixResult.Step == null ? prefixResult :
                        await AcceptOriginalPrefixStepAsync(lease, prefixResult.Step, capturedReceipts);
                    if (acceptedPrefix.Step != null) prefixAllocations.Commit();
                    return acceptedPrefix;
                }
                using var allocationScope = _allocations.Clock.BeginSpeculation();
                var prepared = await _source.PrepareContinuationAsync(lease);
                if (prepared.Ticket == null)
                    return new(null, prepared.Issues);
                EnsureCurrent(lease);
                attemptedResourceResume = true;
                var result = _resources.ResumePendingResource(expected, capturedReceipts);
                // A rejected receipt has no Step and has changed neither resource
                // nor source prefix. Discard this source ticket and retry later.
                var accepted = AcceptResumedResourceStep(lease, prepared.Ticket, result, null, capturedReceipts);
                if (accepted.Step != null) allocationScope.Commit();
                return accepted;
            }
            catch
            {
                if (attemptedResourceResume)
                    Dispose();
                throw;
            }
            finally { _gate.Release(); }
        }

        internal async Task<AcceptedMechanicsPlanner.ResourceContinuationResult> ResumeMissingAuditSideAsync(
            FileSystemManager.CanonicalWriteLease lease,
            AcceptedMechanicsPlanner.PendingSpiritualExchange expected)
        {
            ArgumentNullException.ThrowIfNull(expected);
            await _gate.WaitAsync();
            var attemptedResourceCommit = false;
            try
            {
                EnsureCurrent(lease);
                if (_resources == null || !ReferenceEquals(_lastResourceStep?.PendingExchange, expected))
                    return ResourceFailure("spiritual_missing_side_wait_mismatch", "the exact retained missing-side wait");
                var issues = await CheckRetainedInputsAsync(lease);
                if (issues.Count != 0)
                    return new(null, issues);
                using var allocationScope = _allocations.Clock.BeginSpeculation();
                var preparedSource = await _source.PrepareContinuationAsync(lease);
                if (preparedSource.Ticket == null)
                    return new(null, preparedSource.Issues);
                var context = _input.PlanningContext!;
                var batches = _source.BuildPreparedResourceBatches(lease, preparedSource.Ticket,
                    _terminal?.ExecutionOwners ?? context.Owners, SourceResourceBaseline);
                if (!batches.IsValid)
                    return new(null, batches.Issues);
                var completion = batches.Exchanges.SingleOrDefault(batch => batch.Ordinal == expected.Ordinal);
                if (completion == null || completion.ConflictId != expected.ConflictId ||
                    completion.ExchangeId != expected.ExchangeId)
                    return ResourceFailure("spiritual_missing_side_candidate_missing", "the waiting exchange from the exact signed source ticket");
                EnsureCurrent(lease);
                var preparedResource = _resources.PrepareMissingAuditSide(expected, completion);
                if (preparedResource.Preparation == null)
                    return new(null, preparedResource.Issues);
                var owned = preparedResource.Preparation;
                // This very raw completion came from this source ticket. The
                // exact resource-owned preparation retains its canonical exports,
                // deterministic aliases, and original raw/execution prefix.
                if (!ReferenceEquals(owned.Owner, _resources) ||
                    !ReferenceEquals(owned.Expected, expected) ||
                    !ReferenceEquals(owned.CheckedCompletion, completion))
                    throw new InvalidOperationException("Missing-side preparation lost its exact producer relation.");
                attemptedResourceCommit = true;
                var result = _resources.CommitMissingAuditSide(owned);
                var accepted = AcceptResumedResourceStep(lease, preparedSource.Ticket, result, owned, null);
                if (accepted.Step != null) allocationScope.Commit();
                return accepted;
            }
            catch
            {
                if (attemptedResourceCommit)
                    Dispose();
                throw;
            }
            finally { _gate.Release(); }
        }

        private AcceptedMechanicsPlanner.ResourceContinuationResult AcceptResumedResourceStep(
            FileSystemManager.CanonicalWriteLease lease,
            SpiritualWoundSourceSession.PreparedContinuation sourceTicket,
            AcceptedMechanicsPlanner.ResourceContinuationResult result,
            AcceptedMechanicsPlanner.ResourceExecutionSession.MissingSidePreparation? missing,
            JsonArray? receipts)
        {
            if (result.Step == null)
                return result;
            if (result.Issues.Any(issue => issue.Severity == IssueSeverity.Error) ||
                result.Step.Result is { IsValid: false })
            {
                RevokeUnderLease(lease);
                return new(null, result.Issues);
            }
            EnsureCurrent(lease);
            var step = result.Step;
            // Validate actual returned interval ownership before accepting source
            // evidence. An intermediate wait is retained without closing ordinal.
            if (step.Interval is { } interval &&
                (!_resources!.Owns(interval) || interval.Ordinal != _nextResourceOrdinal))
                throw new InvalidOperationException("The resumed interval must belong to this executor and ordinal.");
            var before = _source.ContinuationRevision;
            var committed = _source.CommitPreparedContinuation(lease, sourceTicket);
            if (committed.Session == null)
            {
                RevokeUnderLease(lease);
                return new(null, committed.Issues);
            }
            _consumedContinuations.Add(new(sourceTicket, before, _source.ContinuationRevision,
                _lastResourceStep!, step, missing, receipts));
            _lastResourceStep = step;
            if (step.Interval != null)
            {
                RetainClosedExchangeEvidence(step.Interval);
                _nextResourceOrdinal++;
            }
            return new(step, result.Issues);
        }
    }
}
