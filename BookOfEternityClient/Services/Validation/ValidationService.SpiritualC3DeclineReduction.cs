using System.Text.Json.Nodes;
using BookOfEternityClient.Core;

namespace BookOfEternityClient.Services;

public partial class ValidationService
{
    /// <summary>
    /// Holds a detached accepted spiritual decision receipt and ordinary reduction without publication authority.
    /// </summary>
    /// <param name="Reduction">
    /// Sealed ordinary mechanics with the receipt companion image, or <see langword="null"/>.
    /// </param>
    /// <param name="ReceiptAfterImage">
    /// Complete detached receipt image, or <see langword="null"/> on rejection.
    /// </param>
    /// <param name="Issues">
    /// Failure diagnostics; empty only for a complete unpublished reduction.
    /// </param>
    /// <param name="ReceiptProof">
    /// Owner-bound C3 receipt join proof, or <see langword="null"/> when no join is required.
    /// </param>
    internal sealed record SpiritualC3DecisionReductionResult(
        AcceptedMechanicsPlanner.CompletedOrdinaryMechanicsReduction? Reduction,
        JsonObject? ReceiptAfterImage, IReadOnlyList<ValidationIssue> Issues,
        SpiritualWoundDeclineReceiptReducer.ValidatedReceiptProof? ReceiptProof = null)
    {
        /// <summary>
        /// Gets whether one complete detached reduction is available.
        /// </summary>
        internal bool Success => Reduction is not null && ReceiptAfterImage is not null && Issues.Count == 0;
    }

    internal sealed partial class SpiritualC2PrivateSession
    {
        /// <summary>
        /// Reduces the completed owner-bound explicit declines without publishing canonical state.
        /// </summary>
        /// <param name="lease">
        /// Active canonical lease retaining the exact C2 pair through reduction.
        /// </param>
        /// <returns>
        /// Detached receipt and ordinary reduction, or fail-closed diagnostics.
        /// </returns>
        internal Task<SpiritualC3DecisionReductionResult> ReduceCompletedDeclinesAsync(
            FileSystemManager.CanonicalWriteLease lease) =>
            ReduceCompletedDecisionsCoreAsync(lease, requireDeclines: true);

        /// <summary>
        /// Reduces every staged decision against its exact owner-sealed insertion or satisfaction.
        /// </summary>
        /// <param name="lease">
        /// Active canonical lease retaining the exact C2 pair through reduction.
        /// </param>
        /// <returns>
        /// Detached receipt and ordinary reduction, or fail-closed diagnostics.
        /// </returns>
        internal Task<SpiritualC3DecisionReductionResult> ReduceCompletedDecisionsAsync(
            FileSystemManager.CanonicalWriteLease lease) =>
            ReduceCompletedDecisionsCoreAsync(lease, requireDeclines: false);

        /// <summary>
        /// Serializes one terminal-session reduction against its retained capture.
        /// </summary>
        /// <param name="lease">
        /// Canonical lease used for fresh pair and owner checks.
        /// </param>
        /// <param name="requireDeclines">
        /// Whether to retain the older explicit-decline-only boundary.
        /// </param>
        /// <returns>
        /// Detached completed reduction or diagnostics.
        /// </returns>
        private async Task<SpiritualC3DecisionReductionResult> ReduceCompletedDecisionsCoreAsync(
            FileSystemManager.CanonicalWriteLease lease, bool requireDeclines)
        {
            await _submitGate.WaitAsync();
            try
            {
                if (_disposed || _used || Offer is not null)
                    return DeclineFailure("spiritual_c3_decline_session_unavailable");
                return await _capture.ReduceCompletedDecisionsCoreAsync(lease, requireDeclines);
            }
            finally { _submitGate.Release(); }
        }
    }

    internal sealed partial class SpiritualOriginalTurnCapture
    {
        private static readonly object C3PairIssuanceKey = new();
        private readonly object _satisfactionReceiptIdentity = new();

        /// <summary>
        /// Binds the exact completed packet to the capture that reauthenticated its physical C2 pair.
        /// </summary>
        internal sealed class AuthenticatedC3Pair
        {
            private readonly object _issuanceKey;
            private readonly SpiritualSignedC1Origin _signed;
            private readonly JsonObject _packet;
            private readonly string _packetFingerprint;
            private readonly IReadOnlyList<OriginalGuaranteeSatisfaction> _satisfactions;
            private readonly object _satisfactionReceiptIdentity;
            private readonly PreparedTerminal? _terminal;

            /// <summary>
            /// Retains the capture's private issuance key and immutable packet identity.
            /// </summary>
            /// <param name="issuanceKey">
            /// Private key held by the verifying capture.
            /// </param>
            /// <param name="signed">
            /// Exact signed origin verified for the current pair.
            /// </param>
            /// <param name="packet">
            /// Owner-reconstructed completed C2 packet.
            /// </param>
            /// <param name="satisfactions">
            /// Owner-issued historical no-transition results from chronological C2 replay;
            /// <see langword="null"/> means no such results were issued.
            /// </param>
            /// <param name="satisfactionReceiptIdentity">
            /// Capture-owned identity that binds a satisfaction-only receipt to its completed reduction.
            /// </param>
            /// <param name="terminal">
            /// Exact terminal preparation whose real completion must precede closure;
            /// <see langword="null"/> means this turn keeps its conflict active.
            /// </param>
            internal AuthenticatedC3Pair(object issuanceKey, SpiritualSignedC1Origin signed,
                JsonObject packet, IReadOnlyList<OriginalGuaranteeSatisfaction>? satisfactions,
                object satisfactionReceiptIdentity, PreparedTerminal? terminal = null)
            {
                _issuanceKey = issuanceKey;
                _signed = signed;
                _packet = packet;
                _packetFingerprint = SpiritualWoundStateJson.Hash(packet, "authenticated_c3_pair");
                _satisfactions = satisfactions?.ToArray() ?? [];
                _satisfactionReceiptIdentity = satisfactionReceiptIdentity;
                _terminal = terminal;
            }

            /// <summary>
            /// Checks that the reducer received the entire unmodified owner packet and signed origin.
            /// </summary>
            /// <param name="signed">
            /// Candidate signed origin.
            /// </param>
            /// <param name="packet">
            /// Candidate completed packet.
            /// </param>
            /// <returns>
            /// <see langword="true"/> only for this capture's exact packet.
            /// </returns>
            internal bool Matches(SpiritualSignedC1Origin signed, JsonObject packet) =>
                ReferenceEquals(_issuanceKey, C3PairIssuanceKey) &&
                ReferenceEquals(_signed, signed) && ReferenceEquals(_packet, packet) &&
                _packetFingerprint == SpiritualWoundStateJson.Hash(packet, "authenticated_c3_pair");

            /// <summary>
            /// Gets the number of owner-issued no-transition results that the reducer must consume.
            /// </summary>
            internal int SatisfactionCount => _satisfactions.Count;

            /// <summary>
            /// Gets the identity of the capture that issued these satisfaction results.
            /// </summary>
            internal object SatisfactionReceiptIdentity => _satisfactionReceiptIdentity;

            /// <summary>
            /// Gets the completed exact terminal owner, or <see langword="null"/> for a continuing conflict.
            /// </summary>
            internal PreparedTerminal? Terminal => _terminal?.IsComplete == true ? _terminal : null;

            /// <summary>
            /// Matches a staged no-transition decision to exactly one retained owner-issued result.
            /// </summary>
            /// <param name="signed">
            /// Signed original images authenticated with the completed packet.
            /// </param>
            /// <param name="packet">
            /// Exact completed packet bound by this pair.
            /// </param>
            /// <param name="witness">
            /// Owner-reconstructed source witness in causal order.
            /// </param>
            /// <param name="staged">
            /// Closed staged satisfaction row to verify.
            /// </param>
            /// <returns>
            /// <see langword="true"/> only for an exact source, instance, side, wound and historical rank.
            /// </returns>
            internal bool MatchesSatisfaction(SpiritualSignedC1Origin signed, JsonObject packet,
                JsonObject witness, JsonObject staged)
            {
                if (!Matches(signed, packet) ||
                    witness["retraumaWoundId"] is not null ||
                    witness["guaranteedSeverityRank"] is null)
                    return false;
                var matches = _satisfactions.Where(value =>
                    value.InstanceId == packet["conflictInstanceRef"]?.GetValue<string>() &&
                    value.Admission.Source.Coordinate == witness["coordinate"]?.GetValue<string>() &&
                    value.Admission.Source.ConflictId == witness["conflictId"]?.GetValue<string>() &&
                    value.Admission.Source.ExchangeId == witness["exchangeId"]?.GetValue<string>() &&
                    value.Admission.Source.AffectedSide == witness["affectedSide"]?.GetValue<string>() &&
                    value.Admission.Source.GuaranteedSeverityRank ==
                        witness["guaranteedSeverityRank"]?.GetValue<int>() &&
                    value.OpportunityRef == staged["opportunityRef"]?.GetValue<string>() &&
                    value.WoundId == staged["satisfiedWoundId"]?.GetValue<string>() &&
                    value.SeverityRank == staged["satisfiedSeverityRank"]?.GetValue<int>() &&
                    value.SeverityRank >= value.Admission.Source.GuaranteedSeverityRank).ToArray();
                return matches.Length == 1;
            }
        }

        /// <summary>
        /// Joins a decision receipt to the retained completed reduction without allocating new identities.
        /// </summary>
        /// <param name="lease">
        /// Active lease used to recheck the pair, signed images and retained ordinary reduction.
        /// </param>
        /// <param name="requireDeclines">
        /// Whether the older decline-only entrypoint must reject materialized wound work.
        /// </param>
        /// <returns>
        /// Detached unpublished reduction, or diagnostics without a partial result.
        /// </returns>
        internal async Task<SpiritualC3DecisionReductionResult> ReduceCompletedDecisionsCoreAsync(
            FileSystemManager.CanonicalWriteLease lease, bool requireDeclines)
        {
            JsonObject packet;
            SpiritualSignedC1Origin signed;
            AuthenticatedC3Pair authenticatedPair;
            await _gate.WaitAsync();
            try
            {
                var next = await ReadC2NextSourceCoreAsync(lease);
                if (next.Issues.Count != 1 ||
                    next.Issues[0].Code != "spiritual_c2_next_source_missing" ||
                    _matchedC2Pair is null)
                    return new(null, null, next.Issues.Count != 0
                        ? next.Issues : DeclineFailure("spiritual_c3_decline_pair_incomplete").Issues);
                signed = ReadVerifiedSignedC1OriginCore(lease);
                var physical = await _validator._fs.ReadFileBytesAsync(lease,
                    SpiritualWoundOpportunityReceiptState.StatePath);
                if (signed.Receipt.Existed != (physical is not null) ||
                    signed.Receipt.Existed &&
                    !signed.Receipt.Bytes!.AsSpan().SequenceEqual(physical))
                    return DeclineFailure("spiritual_c3_decline_receipt_changed");
                packet = SpiritualWoundStateJson.Parse(
                    SpiritualWoundDecisionPendingState.SerializeCanonical(_matchedC2Pair.Pending))
                    ["pending"]!.AsObject();
                authenticatedPair = new AuthenticatedC3Pair(C3PairIssuanceKey, signed, packet,
                    _woundOpportunities.Values.Where(value => value.Satisfaction is not null)
                        .Select(value => value.Satisfaction!).ToArray(),
                    _satisfactionReceiptIdentity, _terminal);
                if (_completedOrdinaryReduction?.Reduction is null)
                    return DeclineFailure("spiritual_c3_completed_reduction_missing");
            }
            catch (Exception error) when (error is IOException or UnauthorizedAccessException or
                InvalidOperationException or FormatException or System.Text.Json.JsonException)
            {
                return DeclineFailure("spiritual_c3_decline_origin_unavailable");
            }
            finally { _gate.Release(); }

            var ordinary = await CompleteOrdinaryReductionAsync(lease);
            if (!ordinary.Success || ordinary.Reduction is null)
                return new(null, null, ordinary.Issues);
            if (requireDeclines && ordinary.Reduction.HasLiveWoundWork)
                return DeclineFailure("spiritual_c3_decline_materialization_deferred");
            var receipt = SpiritualWoundDeclineReceiptReducer.Reduce(signed, packet,
                ordinary.Reduction.LiveWoundCompletion, authenticatedPair);
            if (receipt.AfterImage is null)
                return new(null, null, receipt.Issues);
            try
            {
                var completed = ordinary.Reduction;
                if (completed.LiveWoundCompletion is not null)
                {
                    var woundPaths = new[]
                    {
                        AcceptedMechanicsPlan.WoundCommandPath,
                        WoundIdentityState.StatePath,
                        WoundHistoryState.HistoryPath,
                        WoundCarrierCatalog.PlayerPath,
                        WoundCarrierCatalog.NpcPath,
                        WoundCarrierCatalog.EnemiesPath,
                        WoundCarrierCatalog.AlliesPath
                    };
                    foreach (var path in woundPaths)
                    {
                        if (!completed.Input.BeforeImages.ContainsKey(path))
                            completed = completed.WithBeforeImage(path, _draftInputs.ReadImage(path));
                    }
                }
                foreach (var path in new[] { SpiritualWoundCaptureCheckpointState.StatePath,
                             SpiritualWoundDecisionPendingState.StatePath, AcceptedMechanicsPlan.WoundCommandPath })
                {
                    if (!completed.Input.BeforeImages.ContainsKey(path))
                        completed = completed.WithBeforeImage(path,
                            _signedServiceRollbackImages.TryGetValue(path, out var original)
                                ? original : _draftInputs.ReadImage(path));
                }
                completed = completed.WithOwnerCompanionAfterImage(
                    SpiritualWoundOpportunityReceiptState.StatePath, signed.Receipt,
                    receipt.AfterImage, receipt.Proof);
                return new(completed, receipt.AfterImage.DeepClone().AsObject(), [], receipt.Proof);
            }
            catch (InvalidOperationException)
            {
                return DeclineFailure("spiritual_c3_decline_companion_conflict");
            }
        }
    }

    private static SpiritualC3DecisionReductionResult DeclineFailure(string code) =>
        new(null, null,
        [
            new ValidationIssue(SpiritualWoundOpportunityReceiptState.StatePath,
                IssueSeverity.Error, "The completed spiritual decline is unavailable.",
                code: code, section: "AcceptedTurnWoundMaterialization")
        ]);
}
