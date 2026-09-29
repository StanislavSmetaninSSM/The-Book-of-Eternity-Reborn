using System.Text.Json;
using BookOfEternityClient.Core;

namespace BookOfEternityClient.Services;

public partial class ValidationService
{
    internal sealed partial class SpiritualOriginalTurnCapture
    {
        private static readonly object C3SourceOnlyIssuanceKey = new();

        /// <summary>
        /// Retains the actual exhausted capture's origin and completed terminal owner without a C2 packet.
        /// </summary>
        internal sealed class AuthenticatedC3SourceOnly
        {
            private readonly object _issuanceKey;

            /// <summary>
            /// Binds the immutable original input and signed origin after source-only completion checks.
            /// </summary>
            /// <param name="issuanceKey">
            /// Private key belonging to the verifying capture.
            /// </param>
            /// <param name="signed">
            /// Reauthenticated original receipt and conflict images.
            /// </param>
            /// <param name="input">
            /// Exact input retained by the completed ordinary reduction.
            /// </param>
            /// <param name="realm">
            /// Realm of the original source owner.
            /// </param>
            /// <param name="terminal">
            /// Completed terminal owner, or <see langword="null"/> for a continuing conflict.
            /// </param>
            /// <param name="receiptIdentity">
            /// Exact terminal receipt identity, or <see langword="null"/> when no terminal join is required.
            /// </param>
            internal AuthenticatedC3SourceOnly(object issuanceKey, SpiritualSignedC1Origin signed,
                AcceptedMechanicsInput input, string realm, PreparedTerminal? terminal, object? receiptIdentity)
            {
                _issuanceKey = issuanceKey;
                Signed = signed;
                Input = input;
                Realm = realm;
                Terminal = terminal;
                ReceiptIdentity = receiptIdentity;
            }

            /// <summary>
            /// Gets whether this capability was issued by the verifying capture and its terminal is complete.
            /// </summary>
            internal bool IsAuthentic => ReferenceEquals(_issuanceKey, C3SourceOnlyIssuanceKey) &&
                (Terminal is null ? ReceiptIdentity is null : Terminal.IsComplete && ReceiptIdentity is not null);

            /// <summary>
            /// Gets the reauthenticated signed original images.
            /// </summary>
            internal SpiritualSignedC1Origin Signed { get; }

            /// <summary>
            /// Gets the exact input of the completed ordinary owner.
            /// </summary>
            internal AcceptedMechanicsInput Input { get; }

            /// <summary>
            /// Gets the original source realm.
            /// </summary>
            internal string Realm { get; }

            /// <summary>
            /// Gets the actual terminal owner when the conflict closes.
            /// </summary>
            internal PreparedTerminal? Terminal { get; }

            /// <summary>
            /// Gets the completed reduction's terminal receipt identity when a closure is required.
            /// </summary>
            internal object? ReceiptIdentity { get; }
        }

        /// <summary>
        /// Joins an instance-only receipt to an exhausted original capture without inventing a wound decision.
        /// </summary>
        /// <param name="lease">
        /// Active canonical lease for original input, empty private-control and completion checks.
        /// </param>
        /// <returns>
        /// The completed reduction with its receipt, or diagnostics without a publication capability.
        /// </returns>
        private async Task<SpiritualC3DecisionReductionResult> ReduceCompletedSourceOnlyAsync(
            FileSystemManager.CanonicalWriteLease lease)
        {
            await _gate.WaitAsync();
            try
            {
                EnsureCurrent(lease);
                if (_matchedC2Pair is not null || _usesColdOriginalInputs || _originalOutputProjection is null ||
                    _completedOrdinaryReduction?.Reduction is not { } completed || _resources is null || _effects is null ||
                    _woundSelections.Count != 0 || _woundDeclines.Count != 0 || _woundOpportunities.Count != 0 ||
                    completed.HasLiveWoundWork || completed.LiveWoundCompletion is not null ||
                    _closedExchangeEvidence.Count == 0 || _closedExchangeEvidence.Count != _nextResourceOrdinal ||
                    !_source.TryReadInitialActiveExchangeInventory(out var conflictId, out var exchangeIds) ||
                    _closedExchangeEvidence.Count != exchangeIds.Length ||
                    _closedExchangeEvidence.Where((entry, index) => entry.Interval.Ordinal != index ||
                        entry.Interval.ConflictId != conflictId || entry.Interval.ExchangeId != exchangeIds[index] ||
                        !_resources.Owns(entry.Interval) || entry.Sources.Any(source => !_source.Owns(source) ||
                            source.Calculation.MaximumSeverityRank != 0 || source.GuaranteedSeverityRank is not null)).Any())
                    return DeclineFailure("spiritual_c3_source_only_completion_required");
                var issues = await CheckRetainedInputsAsync(lease);
                if (issues.Count != 0) return new(null, null, issues);
                issues = await _source.CheckCompletionInputsAsync(lease, _nextResourceOrdinal, _resources, _effects);
                if (issues.Count != 0) return new(null, null, issues);
                var inventory = ReadC1ImageInventoryCore();
                if (!_physicalWitnesses.TryGetValue(SpiritualWoundCaptureCheckpointState.StatePath, out var checkpoint) ||
                    !_physicalWitnesses.TryGetValue(SpiritualWoundDecisionPendingState.StatePath, out var pending) ||
                    !IsEmptyCheckpointBefore(checkpoint) || !IsEmptyPendingBefore(pending, inventory.RegisteredPaths))
                    return DeclineFailure("spiritual_c3_source_only_private_control_active");
                foreach (var path in _draftInputs.PathInventory)
                {
                    var bytes = await _validator._fs.ReadFileBytesAsync(lease, path);
                    if (!SameExactImage(_draftInputs.ReadImage(path), new CanonicalBeforeImage(bytes is not null, bytes)))
                        return DeclineFailure("spiritual_c3_source_only_input_changed");
                }
                var signed = ReadVerifiedSignedC1OriginCore(lease);
                issues = ValidateSignedOriginalCreationReceipts(lease, signed.Receipt);
                if (issues.Count != 0) return new(null, null, issues);
                var physicalReceipt = await _validator._fs.ReadFileBytesAsync(lease,
                    SpiritualWoundOpportunityReceiptState.StatePath);
                if (!SameExactImage(signed.Receipt, new CanonicalBeforeImage(physicalReceipt is not null, physicalReceipt)))
                    return DeclineFailure("spiritual_c3_decline_receipt_changed");
                var owner = new AuthenticatedC3SourceOnly(C3SourceOnlyIssuanceKey, signed, completed.Input,
                    _source.Realm, _terminal, _terminal is null ? null : _satisfactionReceiptIdentity);
                var receipt = SpiritualWoundDeclineReceiptReducer.ReduceSourceOnly(owner);
                if (receipt.AfterImage is null) return new(null, null, receipt.Issues);
                foreach (var path in new[] { SpiritualWoundCaptureCheckpointState.StatePath,
                             SpiritualWoundDecisionPendingState.StatePath, AcceptedMechanicsPlan.WoundCommandPath })
                    if (!completed.Input.BeforeImages.ContainsKey(path))
                        completed = completed.WithBeforeImage(path, _signedServiceRollbackImages.TryGetValue(path, out var original)
                            ? original : _draftInputs.ReadImage(path));
                completed = completed.WithOwnerCompanionAfterImage(SpiritualWoundOpportunityReceiptState.StatePath,
                    signed.Receipt, receipt.AfterImage, receipt.Proof);
                return new(completed, receipt.AfterImage.DeepClone().AsObject(), [], receipt.Proof);
            }
            catch (Exception error) when (error is IOException or UnauthorizedAccessException or
                InvalidOperationException or FormatException or JsonException or OverflowException)
            {
                return DeclineFailure("spiritual_c3_source_only_reduction_invalid");
            }
            finally { _gate.Release(); }
        }
    }
}
