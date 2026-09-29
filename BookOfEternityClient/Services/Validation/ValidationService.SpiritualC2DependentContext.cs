using System.Text;
using System.Text.Json;
using BookOfEternityClient.Core;

namespace BookOfEternityClient.Services;

public partial class ValidationService
{
    /// <summary>
    /// Describes one saved selection's dependent correction without granting write authority.
    /// </summary>
    /// <param name="ContinuationId">
    /// Comparison token for the exact private pair, never an execution capability.
    /// </param>
    /// <param name="BaselineIssues">
    /// Diagnostics that define the saved selection's correction frontier.
    /// </param>
    /// <param name="CurrentIssues">
    /// Diagnostics remaining in the current physical draft; empty allows a resume attempt.
    /// </param>
    internal sealed record SpiritualC2DependentContext(string ContinuationId,
        IReadOnlyList<ValidationIssue> BaselineIssues, IReadOnlyList<ValidationIssue> CurrentIssues)
    {
        /// <summary>
        /// Gets the exact raw correction fields derived from the committed baseline.
        /// </summary>
        internal IReadOnlyList<SpiritualWoundContinuationField> DependentDraftFields { get; init; } = [];

        /// <summary>
        /// Retains the genuine raw comparison policy internally; it is never included in a transport request.
        /// </summary>
        internal SpiritualWoundDependentDraftPolicy? DraftPolicy { get; init; }
    }

    internal sealed partial class SpiritualC2PrivateSession
    {
        /// <summary>
        /// Reads the saved selection's dependent context under its retained owner.
        /// </summary>
        /// <param name="lease">
        /// Current canonical lease covering physical inputs and private controls.
        /// </param>
        /// <returns>
        /// Current context, or null when this session no longer owns a pending selection.
        /// </returns>
        internal async Task<SpiritualC2DependentContext?> ReadDependentContextAsync(
            FileSystemManager.CanonicalWriteLease lease)
        {
            await _submitGate.WaitAsync();
            try
            {
                if (_disposed || _used || Offer is not null || !_capture.HasC2PendingSubmission)
                    return null;
                if (_capture.NeedsSequentialDependentContext)
                    return await ReadSequentialDependentContextAsync(lease);
                return await _capture.ReadC2DependentContextAsync(lease);
            }
            finally { _submitGate.Release(); }
        }
    }

    internal sealed partial class SpiritualOriginalTurnCapture
    {
        /// <summary>
        /// Separately probes the committed baseline and current draft for the exact replayed selection.
        /// </summary>
        /// <param name="lease">
        /// Current canonical lease required by the owner diagnostics probe.
        /// </param>
        /// <returns>
        /// Comparison-only context, or null after ownership or immutable input failure.
        /// </returns>
        internal async Task<SpiritualC2DependentContext?> ReadC2DependentContextAsync(
            FileSystemManager.CanonicalWriteLease lease)
        {
            await _gate.WaitAsync();
            var succeeded = false;
            try
            {
                var checkpoint = await RequireC2PendingSubmissionCoreAsync(lease);
                var committed = ReadC2CommittedInputLayer(checkpoint);
                if (_coldCurrentInputs is null || _coldProposedInputs is not null ||
                    !SameExactImage(committed[AfterlifeSpiritualConflictState.StatePath],
                        _coldCurrentInputs.ReadImage(AfterlifeSpiritualConflictState.StatePath)))
                    return null;
                if (!_allocations.Journal.IsAppendMode) _allocations.Journal.EnableAppendAfterReplay();
                var baseline = await ProbeC2SelectedContinuationCoreAsync(lease, []);
                if (baseline.Count != 0 && !IsCorrectableDependentConflictFailure(baseline))
                    return null;
                var changes = await ReadC2SubmissionChangesCoreAsync(lease, checkpoint);
                var original = _source.ReadMechanicsConflict(this);
                AfterlifeConflictActionPointProjection? frontier = null;
                AcceptedMechanicsPlanner.ResourceExecutionSession.SpiritualMechanicsContext? mechanics = null;
                if (baseline.Count != 0)
                {
                    var mechanicsIssues = new List<ValidationIssue>();
                    mechanics = PrepareSourceMechanics(_source, mechanicsIssues);
                    if (mechanics is null || mechanicsIssues.Count != 0) return null;
                    var conflictId = original["activeConflict"]?["conflictId"]?.GetValue<string>();
                    if (conflictId is null) return null;
                    frontier = mechanics.ReadActionPointBefore(conflictId, mechanics.Ordinal);
                }
                var committedImage = committed[AfterlifeSpiritualConflictState.StatePath];
                var currentImage = changes.FirstOrDefault(change =>
                    change.Path == AfterlifeSpiritualConflictState.StatePath).Image ?? committedImage;
                if (committedImage.Bytes is null || currentImage.Bytes is null) return null;
                var policy = CreateC2DependentPolicy(original,
                    SpiritualWoundDependentDraftPolicy.ReadStrictRoot(SpiritualWoundStateJson.DecodeUtf8JsonText(committedImage.Bytes)),
                    baseline, frontier, mechanics);
                if (policy is null || !policy.Allows(SpiritualWoundDependentDraftPolicy.ReadStrictRoot(
                        SpiritualWoundStateJson.DecodeUtf8JsonText(currentImage.Bytes)))) return null;
                var current = await ProbeC2SelectedContinuationCoreAsync(lease, changes);
                if (current.Count != 0 && !IsCorrectableDependentConflictFailure(current))
                    return null;
                var repeated = await ReadC2SubmissionChangesCoreAsync(lease, checkpoint);
                if (changes.Count != repeated.Count || changes.Where((change, index) =>
                        change.Path != repeated[index].Path ||
                        !SameExactImage(change.Image, repeated[index].Image)).Any())
                    return null;
                await RequireC2PendingSubmissionCoreAsync(lease);
                var pair = _matchedC2Pair!;
                var id = DependentContextFingerprint(pair.CheckpointBytes, pair.PendingBytes, policy.Fields);
                succeeded = true;
                return new(id, baseline.ToArray(), current.ToArray())
                { DependentDraftFields = policy.Fields, DraftPolicy = policy };
            }
            catch (Exception error) when (error is IOException or UnauthorizedAccessException or
                InvalidOperationException or FormatException or JsonException or OverflowException or DecoderFallbackException)
            {
                return null;
            }
            finally
            {
                if (!succeeded && IsCurrentOwner) RevokeUnderLease(lease);
                _gate.Release();
            }
        }
    }
}
