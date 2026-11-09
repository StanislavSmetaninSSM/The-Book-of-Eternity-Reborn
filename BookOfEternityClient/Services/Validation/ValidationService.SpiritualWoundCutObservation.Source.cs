using BookOfEternityClient.Core;

namespace BookOfEternityClient.Services;

public partial class ValidationService
{
    /// <summary>Detached facts about a privately retained ordinary context; no live context or routing token escapes.</summary>
    /// <param name="MechanicsCurrent">Currentness of the exact previously warmed context.</param>
    /// <param name="ExactOwnersCurrent">Whether capture, source, admission and resource owners still match.</param>
    /// <param name="SameOrdinaryContext">Whether the source still retains the identical warmed context.</param>
    /// <param name="SameRoutingIdentity">Whether the actual routing epoch remains unchanged.</param>
    /// <param name="StaleReadReturnedNoData">Whether the production stale branch returned no wound view.</param>
    /// <param name="StaleReadCode">The sole stale diagnostic code, or null.</param>
    /// <param name="StaleReadIssueCount">Number of actual stale-branch diagnostics.</param>
    internal sealed record SpiritualPressureCutWitnessFacts(bool MechanicsCurrent, bool ExactOwnersCurrent,
        bool SameOrdinaryContext, bool SameRoutingIdentity, bool StaleReadReturnedNoData,
        string? StaleReadCode, int StaleReadIssueCount);

    internal sealed partial class SpiritualWoundSourceSession
    {
        // This is a bounded diagnostic witness, not a cache or new authority. The exact ordinary context stays private
        // to its existing source owner from pre-speculation warm completion until the owning operation's finally.
        /// <summary>Retains the source's actual warmed context only for one capture-held diagnostic operation.</summary>
        /// <param name="Capture">Exact requesting capture, never exported.</param>
        /// <param name="Lease">Exact active request/query lease, never exported.</param>
        /// <param name="Context">The actual existing ordinary context, never a newly captured proxy.</param>
        /// <param name="RoutingIdentity">Existing routing epoch retained only for reference comparison.</param>
        private sealed record PressureCutWitness(SpiritualOriginalTurnCapture Capture,
            FileSystemManager.CanonicalWriteLease Lease,
            AcceptedMechanicsPlanner.ResourceExecutionSession.SpiritualMechanicsContext Context, object RoutingIdentity);
        private PressureCutWitness? _pressureCutWitness;

        /// <summary>Retains the exact warmed ordinary context, never a newly captured proxy or a caller-supplied context.</summary>
        /// <param name="capture">Exact capture holding its exclusion gate and active one-shot request.</param>
        /// <param name="lease">Exact lease used by both the request and ordinary query.</param>
        /// <returns>True only when the actual warmed context and its unchanged source identity remain current.</returns>
        internal bool RetainPressureCutWitnessUnderGate(SpiritualOriginalTurnCapture capture,
            FileSystemManager.CanonicalWriteLease lease)
        {
            if (!_continuationGate.Wait(0)) return false;
            try
            {
                if (!capture.OwnsPressureCutWitnessUnderGate(this, lease) || !IsCurrentOwner ||
                    !ReferenceEquals(_ordinaryLease, lease) || _ordinarySourceRevision != _continuationRevision ||
                    _ordinaryContext is not { IsCurrent: true } context ||
                    capture.ReadMechanicsEpoch(this) is not { } routing || _pressureCutWitness != null) return false;
                _pressureCutWitness = new(capture, lease, context, routing);
                return true;
            }
            finally { _continuationGate.Release(); }
        }

        /// <summary>Reads only currentness and, when already stale, the production stale-read branch inside the real cut scope.</summary>
        /// <param name="capture">Exact capture holding the same active request.</param>
        /// <param name="lease">Exact retained request lease.</param>
        /// <param name="readStaleDiagnostic">True only for the immediate post-cut observation.</param>
        /// <returns>Immutable facts, or null when the exact diagnostic ownership no longer matches.</returns>
        internal SpiritualPressureCutWitnessFacts? ReadPressureCutWitnessUnderGate(SpiritualOriginalTurnCapture capture,
            FileSystemManager.CanonicalWriteLease lease, bool readStaleDiagnostic)
        {
            if (!_continuationGate.Wait(0)) return null;
            try
            {
                if (_pressureCutWitness is not { } witness || !ReferenceEquals(witness.Capture, capture) ||
                    !ReferenceEquals(witness.Lease, lease) || !capture.OwnsPressureCutWitnessUnderGate(this, lease)) return null;
                var current = witness.Context.IsCurrent;
                var sameContext = ReferenceEquals(witness.Context, _ordinaryContext);
                var sameRouting = ReferenceEquals(witness.RoutingIdentity, capture.ReadMechanicsEpoch(this));
                var ownersCurrent = IsCurrentOwner && capture.OwnsPressureCutWitnessUnderGate(this, lease);
                var noData = false;
                string? code = null;
                var issueCount = 0;
                // A current read could lazily initialize a view. Never invoke it here: only the real cheap stale branch is observed.
                if (readStaleDiagnostic && !current)
                {
                    var issues = new List<ValidationIssue>();
                    noData = witness.Context.ReadCurrentWoundView(issues) == null;
                    issueCount = issues.Count;
                    code = issues.Count == 1 ? issues[0].Code : null;
                }
                return new(current, ownersCurrent, sameContext, sameRouting, noData, code, issueCount);
            }
            finally { _continuationGate.Release(); }
        }

        /// <summary>Releases only this capture's private witness on every success or exception; no owner is disposed through it.</summary>
        /// <param name="capture">Exact capture whose bounded observation has finished.</param>
        internal void ClearPressureCutWitness(SpiritualOriginalTurnCapture capture)
        {
            if (ReferenceEquals(_pressureCutWitness?.Capture, capture)) _pressureCutWitness = null;
        }
    }
}
