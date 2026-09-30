using BookOfEternityClient.Core;

namespace BookOfEternityClient.Services;

public partial class ValidationService
{
    /// <summary>
    /// Reports an owned source admission or the validation issues preventing it.
    /// </summary>
    /// <param name="Admission">
    /// Retained admission, or <see langword="null"/> when source or frontier validation fails.
    /// </param>
    /// <param name="Issues">
    /// Issues produced by source and frontier validation; empty for a successful admission.
    /// </param>
    internal sealed record SpiritualWoundAdmissionResult(
        SpiritualOriginalTurnCapture.WoundSourceAdmission? Admission,
        IReadOnlyList<ValidationIssue> Issues);

    internal sealed partial class SpiritualOriginalTurnCapture
    {
        private readonly Dictionary<PreparedSpiritualSource, WoundSourceAdmission> _woundAdmissions =
            new(ReferenceEqualityComparer.Instance);

        /// <summary>
        /// Admits a retained signed source at its actual closed resource exchange without materializing or publishing a wound.
        /// </summary>
        /// <param name="lease">
        /// Active canonical lease used to revalidate the original inputs and candidate source evidence.
        /// </param>
        /// <param name="interval">
        /// Exact most recently closed interval produced by this capture's resource executor.
        /// </param>
        /// <param name="source">
        /// Exact retained source object for a side of that exchange; a reconstructed copy is rejected.
        /// </param>
        /// <returns>
        /// The retained admission or validation issues. Repeating an unchanged admission returns the same object.
        /// </returns>
        internal Task<SpiritualWoundAdmissionResult> AdmitWoundSourceAsync(
            FileSystemManager.CanonicalWriteLease lease,
            AcceptedMechanicsPlanner.SpiritualExchangeInterval interval,
            PreparedSpiritualSource source) => WoundSourceAdmission.AdmitAsync(this, lease, interval, source);

        /// <summary>
        /// Checks retained object ownership and frontier freshness without rereading files.
        /// </summary>
        /// <param name="admission">
        /// Admission to check; a <see langword="null"/> or foreign admission is not owned.
        /// </param>
        /// <returns>
        /// <see langword="true"/> only for the registered admission at the current closed exchange and source revision;
        /// otherwise, <see langword="false"/>.
        /// File-backed consumers must additionally revalidate under a canonical lease.
        /// </returns>
        internal bool OwnsWoundAdmission(WoundSourceAdmission? admission) =>
            admission != null && IsCurrentOwner &&
            _source.ContinuationRevision == admission.SourceRevision &&
            ReferenceEquals(_effects, admission.Draft) &&
            OwnsWoundFrontier(admission.Interval, admission.Source) &&
            _woundAdmissions.TryGetValue(admission.Source, out var retained) && ReferenceEquals(retained, admission);

        private bool OwnsWoundFrontier(AcceptedMechanicsPlanner.SpiritualExchangeInterval interval,
            PreparedSpiritualSource source) =>
            IsCurrentOwner && _resources is { Result: null } && _effects != null &&
            ReferenceEquals(_lastResourceStep?.Interval, interval) && _resources.Owns(interval) &&
            interval.Ordinal == _nextResourceOrdinal - 1 && _source.Owns(source) &&
            string.Equals(interval.ConflictId, source.ConflictId, StringComparison.Ordinal) &&
            string.Equals(interval.ExchangeId, source.ExchangeId, StringComparison.Ordinal) &&
            source.AffectedSide is "player" or "opposition";

        /// <summary>
        /// Retains the exact signed source and closed exchange owned by one live capture.
        /// This is not a completed wound, effect plan or publication authority.
        /// </summary>
        internal sealed class WoundSourceAdmission
        {
            private WoundSourceAdmission(SpiritualOriginalTurnCapture owner,
                AcceptedMechanicsPlanner.SpiritualExchangeInterval interval, PreparedSpiritualSource source)
            {
                Interval = interval;
                Source = source;
                SourceRevision = owner._source.ContinuationRevision;
                Draft = owner._effects!;
            }

            /// <summary>
            /// Gets the actual closed interval retained at admission.
            /// </summary>
            internal AcceptedMechanicsPlanner.SpiritualExchangeInterval Interval { get; }
            /// <summary>
            /// Gets the signed source object retained by the source session.
            /// </summary>
            internal PreparedSpiritualSource Source { get; }
            /// <summary>
            /// Gets the source continuation revision checked when admission was created.
            /// </summary>
            internal long SourceRevision { get; }
            /// <summary>
            /// Gets the capture's effect draft, which owns subsequent materialization.
            /// </summary>
            internal EffectAcceptedTurnPlanner.EffectAcceptedDraft Draft { get; }

            /// <summary>
            /// Revalidates and retains source admission under the original capture's exclusive gate.
            /// </summary>
            /// <param name="owner">
            /// Current capture retaining the signed source and resource executor.
            /// </param>
            /// <param name="lease">
            /// Active canonical lease used for signed and candidate input validation.
            /// </param>
            /// <param name="interval">
            /// Exact current closed exchange interval; <see langword="null"/> is rejected.
            /// </param>
            /// <param name="source">
            /// Exact retained source; <see langword="null"/> is rejected.
            /// </param>
            /// <returns>
            /// The registered admission when the source frontier remains valid, or validation issues.
            /// </returns>
            internal static async Task<SpiritualWoundAdmissionResult> AdmitAsync(
                SpiritualOriginalTurnCapture owner, FileSystemManager.CanonicalWriteLease lease,
                AcceptedMechanicsPlanner.SpiritualExchangeInterval interval, PreparedSpiritualSource source)
            {
                ArgumentNullException.ThrowIfNull(interval);
                ArgumentNullException.ThrowIfNull(source);
                await owner._gate.WaitAsync();
                try
                {
                    return await AdmitCoreAsync(owner, lease, interval, source);
                }
                finally { owner._gate.Release(); }
            }

            /// <summary>
            /// Admits the exact retained source while a larger capture operation already holds its gate.
            /// </summary>
            /// <param name="owner">
            /// Current capture whose source and interval must agree.
            /// </param>
            /// <param name="lease">
            /// Active canonical lease for source and physical-input checks.
            /// </param>
            /// <param name="interval">
            /// Current owned closed resource interval.
            /// </param>
            /// <param name="source">
            /// Actual retained source object, never a caller reconstruction.
            /// </param>
            /// <returns>
            /// Existing or newly retained admission, or validation issues.
            /// </returns>
            internal static async Task<SpiritualWoundAdmissionResult> AdmitCoreAsync(
                SpiritualOriginalTurnCapture owner, FileSystemManager.CanonicalWriteLease lease,
                AcceptedMechanicsPlanner.SpiritualExchangeInterval interval, PreparedSpiritualSource source)
            {
                owner.EnsureCurrent(lease);
                if (!owner.OwnsWoundFrontier(interval, source))
                    return Mismatch();
                var inputIssues = await owner.CheckRetainedInputsAsync(lease);
                if (inputIssues.Count != 0)
                    return new(null, inputIssues);
                // Revalidate signed origin and retained candidate evidence. Do not
                // commit prospective sources or charge this closed exchange again.
                var sourceIssues = await owner._source.CheckContinuationInputsAsync(lease);
                if (sourceIssues.Count != 0)
                    return new(null, sourceIssues);
                owner.EnsureCurrent(lease);
                if (!owner.OwnsWoundFrontier(interval, source))
                    return Mismatch();
                if (owner._woundAdmissions.TryGetValue(source, out var existing))
                    return owner.OwnsWoundAdmission(existing)
                        ? new(existing, Array.Empty<ValidationIssue>()) : Mismatch();
                var admission = new WoundSourceAdmission(owner, interval, source);
                owner._woundAdmissions.Add(source, admission);
                return new(admission, Array.Empty<ValidationIssue>());
            }

            private static SpiritualWoundAdmissionResult Mismatch() => new(null, new[]
            {
                SourceIssue(AfterlifeSpiritualConflictState.StatePath, "spiritual_wound_frontier_mismatch",
                    "the exact retained source and current closed exchange owned by this original turn")
            });
        }
    }
}
