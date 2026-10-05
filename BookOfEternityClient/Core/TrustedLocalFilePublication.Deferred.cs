namespace BookOfEternityClient.Core;

internal sealed partial class TrustedLocalFilePublication
{
    /// <summary>Begins one complete file set whose existing canonical lease remains held through validation.</summary>
    internal DeferredDecision BeginDeferred(FileSystemManager.CanonicalWriteLease lease,
        TrustedLocalGeneration generation, IReadOnlyList<TrustedLocalFileChange> changes,
        Action<TrustedLocalPublicationPhase, int>? observer = null) =>
        DeferredDecision.Begin(this, lease, generation, changes, observer);

    /// <summary>Opaque, exact-lease-bound pending decision. It cannot finalize another journal or transfer authority.</summary>
    internal sealed class DeferredDecision
    {
        private readonly TrustedLocalFilePublication _owner;
        private readonly FileSystemManager.CanonicalWriteLease _lease;
        private readonly Action<TrustedLocalPublicationPhase, int>? _observer;
        private readonly PublicationAttempt _attempt = new();
        // Retained immutable byte images survive journal cleanup for exact rollback confirmation.
        private readonly Member[] _originalMembers;
        private readonly TrustedLocalGeneration _generation;
        private Journal? _journal;
        private bool _rolledBack;
        private bool _committed;
        internal Exception? Failure { get; private set; }
        internal string TransactionId => _journal?.TransactionId ?? throw new InvalidOperationException("Worker publication did not begin.");

        private DeferredDecision(TrustedLocalFilePublication owner, FileSystemManager.CanonicalWriteLease lease,
            Action<TrustedLocalPublicationPhase, int>? observer, Member[] members, TrustedLocalGeneration generation)
        { _owner = owner; _lease = lease; _observer = observer; _originalMembers = members; _generation = generation; }

        internal static DeferredDecision Begin(TrustedLocalFilePublication owner,
            FileSystemManager.CanonicalWriteLease lease, TrustedLocalGeneration generation,
            IReadOnlyList<TrustedLocalFileChange> changes, Action<TrustedLocalPublicationPhase, int>? observer)
        {
            lease.EnsureNoPendingLocalDecision();
            owner.BeginPublication(lease, generation, changes.Count);
            var members = changes.Select(change => new Member
            {
                Path = owner.ValidateMemberPath(change.Path),
                Before = TrustedLocalFileImage.FromBytes(change.Before), After = TrustedLocalFileImage.FromBytes(change.After)
            }).ToArray();
            var decision = new DeferredDecision(owner, lease, observer, members, generation);
            lease.PendingLocalDecision = decision; // Before intent/member callbacks, never after they return.
            try
            {
                decision._journal = owner.ApplyMembers(lease, generation, members, 2, observer, decision._attempt);
                owner.Preflight(lease, decision._journal, requireAfter: true);
                return decision;
            }
            catch (Exception failure)
            {
                try { decision.RecoverOwned(); decision.Complete(rolledBack: true); }
                catch (Exception recovery)
                {
                    var combined = new AggregateException(failure, recovery);
                    decision.Failure = combined;
                    throw combined;
                }
                throw;
            }
        }

        internal TrustedLocalPublicationOutcome Commit(FileSystemManager.CanonicalWriteLease lease)
        {
            ValidateLease(lease);
            var journal = ReadOwnedPending(); // Identity failure must not trigger recovery of somebody else's evidence.
            try
            {
                var result = _owner.CommitMembers(lease, journal, _observer, _attempt);
                Complete(rolledBack: false);
                return new(TrustedLocalPublicationDisposition.Committed, result, null);
            }
            catch (Exception failure)
            {
                if (_attempt.Committed != null)
                {
                    Complete(rolledBack: false); Failure = failure;
                    return new(TrustedLocalPublicationDisposition.Committed, _attempt.Committed, failure);
                }
                try
                {
                    RecoverOwned(); Complete(rolledBack: true); Failure = failure;
                    return new(TrustedLocalPublicationDisposition.RolledBack, null, failure);
                }
                catch (Exception recovery)
                {
                    Failure = new AggregateException(failure, recovery);
                    return new(TrustedLocalPublicationDisposition.Uncertain, null, Failure);
                }
            }
        }

        internal void Rollback(FileSystemManager.CanonicalWriteLease lease,
            Action<TrustedLocalPublicationPhase, int>? observer = null)
        {
            // A failed commit may already have completed rollback before the caller's catch.
            // This no-op is valid only on its original lease with no newer decision.
            _owner._files.EnsureCanonicalWriteLeaseActive(lease);
            if (ReferenceEquals(lease, _lease) && _rolledBack && lease.PendingLocalDecision == null) return;
            ValidateLease(lease);
            try
            {
                _ = ReadOwnedPending();
                RecoverOwned(observer); Complete(rolledBack: true); Failure = null;
            }
            catch (Exception failure) { Failure = failure; throw; }
        }

        private void ValidateLease(FileSystemManager.CanonicalWriteLease lease)
        {
            _owner._files.EnsureCanonicalWriteLeaseActive(lease);
            if (!ReferenceEquals(lease, _lease) || !ReferenceEquals(lease.PendingLocalDecision, this) || _committed || _rolledBack)
                throw new InvalidOperationException("Worker decision is not active on this exact canonical lease.");
        }

        private Journal ReadOwnedPending()
        {
            var expected = _journal ?? _attempt.Prepared ?? throw new InvalidOperationException("Worker intent is not prepared.");
            var current = _owner.ReadJournal(_owner.Active);
            if (current.Committed || current.TransactionId != expected.TransactionId ||
                current.GenerationBefore != expected.GenerationBefore || current.GenerationAfter != expected.GenerationAfter ||
                current.Members.Length != expected.Members.Length || !current.Members.Zip(expected.Members).All(pair =>
                    MemberComparer.Equals(pair.First.Path, pair.Second.Path) &&
                    pair.First.Before.Exists == pair.Second.Before.Exists && pair.First.Before.Sha256 == pair.Second.Before.Sha256 &&
                    pair.First.After.Exists == pair.Second.After.Exists && pair.First.After.Sha256 == pair.Second.After.Sha256))
                throw Conflict("Worker pending journal identity or declared member authority changed; evidence retained.");
            return current;
        }

        private void RecoverOwned(Action<TrustedLocalPublicationPhase, int>? observer = null)
        {
            ValidateLease(_lease);
            if (File.Exists(_owner._journalScope.ValidateFile(_owner.Active))) _ = ReadOwnedPending();
            _owner.RecoverCore(_lease, observer); // Private owner path; the lease guard is never temporarily removed.
            // A missing active journal alone is not evidence that a published set was restored.
            // Pre-intent refusal is different: none of this decision's members could have changed.
            if (_attempt.IntentPublished && (_owner.ReadGeneration(_lease) != _generation ||
                !_originalMembers.All(member => _owner.Matches(member.Path, member.Before))))
                throw Conflict("Worker rollback could not confirm every original image and generation; continuation remains blocked.");
        }

        private void Complete(bool rolledBack)
        {
            _rolledBack = rolledBack; _committed = !rolledBack;
            _lease.PendingLocalDecision = null;
        }
    }
}
