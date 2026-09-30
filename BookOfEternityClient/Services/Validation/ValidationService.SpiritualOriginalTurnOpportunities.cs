using System.Text;
using System.Text.Json.Nodes;
using BookOfEternityClient.Core;

namespace BookOfEternityClient.Services;

public partial class ValidationService
{
    /// <summary>
    /// Carries a source-derived opportunity, a client-owned fulfilled guarantee, or composition issues.
    /// A zero severity ceiling without a guarantee produces no decision and no issues.
    /// </summary>
    /// <param name="Binding">
    /// Exact event binding for the opportunity, or <see langword="null"/> when none is produced.
    /// </param>
    /// <param name="Opportunity">
    /// Source-derived harmful opportunity, or <see langword="null"/> for a zero ceiling or failed validation.
    /// </param>
    /// <param name="Satisfaction">
    /// Exact no-transition satisfaction proof, or <see langword="null"/> for an ordinary offer.
    /// </param>
    /// <param name="Issues">
    /// Validation failures; empty when composition succeeds or the source cannot produce harm.
    /// </param>
    internal sealed record SpiritualWoundOpportunityResult(
        WoundAcceptedTurnBinding? Binding, WoundOpportunityAuthority? Opportunity,
        IReadOnlyList<ValidationIssue> Issues,
        SpiritualOriginalTurnCapture.OriginalGuaranteeSatisfaction? Satisfaction = null);

    internal sealed partial class SpiritualOriginalTurnCapture
    {
        private readonly Dictionary<WoundSourceAdmission, SpiritualWoundOpportunityResult> _woundOpportunities = new();

        /// <summary>
        /// Derives a wound opportunity from the current admitted source after revalidating signed and candidate inputs.
        /// Reading an opportunity does not insert effects, allocate wound identities or publish state.
        /// </summary>
        /// <param name="lease">
        /// Active canonical write lease used to validate retained inputs.
        /// </param>
        /// <param name="admission">
        /// Exact admission at the current exchange; stale or foreign admissions are rejected.
        /// </param>
        /// <returns>
        /// The retained source opportunity, no opportunity for a zero ceiling, or validation issues.
        /// </returns>
        internal async Task<SpiritualWoundOpportunityResult> ReadWoundOpportunityAsync(
            FileSystemManager.CanonicalWriteLease lease, WoundSourceAdmission admission)
        {
            ArgumentNullException.ThrowIfNull(admission);
            await _gate.WaitAsync();
            try
            {
                return await ReadWoundOpportunityCoreAsync(lease, admission);
            }
            finally { _gate.Release(); }
        }


        /// <summary>
        /// Revalidates and composes the retained opportunity while the caller holds the capture gate.
        /// </summary>
        /// <param name="lease">
        /// Active canonical write lease for the retained input checks.
        /// </param>
        /// <param name="admission">
        /// Exact admission whose current ownership is checked before returning cached or new claims.
        /// </param>
        /// <returns>
        /// The source opportunity or issues preventing its use at the current frontier.
        /// </returns>
        private async Task<SpiritualWoundOpportunityResult> ReadWoundOpportunityCoreAsync(
            FileSystemManager.CanonicalWriteLease lease, WoundSourceAdmission admission)
        {
            var inputIssues = await CheckWoundAdmissionInputsCoreAsync(lease, admission);
            if (inputIssues.Count != 0)
                return new(null, null, inputIssues);
            var creationReceiptIssues = ValidateSignedOriginalCreationReceipts(lease);
            if (creationReceiptIssues.Count != 0)
                return new(null, null, creationReceiptIssues);
            WoundMaterializationEnvelope? retraumaWound = null;
            WoundMaterializationEnvelope? sameConflictWound = null;
            if (admission.Source.RetraumaWoundId != null)
            {
                var targetIssues = new List<ValidationIssue>();
                var current = _effects!.ReadCurrentWoundView(this, _resources!, _source,
                    _effects.WoundReadVersion, targetIssues);
                if (current != null && targetIssues.Count == 0)
                    retraumaWound = _source.RevalidateRetraumaTarget(admission.Source, current, targetIssues);
                if (targetIssues.Count != 0 || retraumaWound == null)
                    return new(null, null, targetIssues);
                if (admission.Source.GuaranteedSeverityRank is int requiredRank &&
                    requiredRank <= retraumaWound.Severity.Rank)
                    return OpportunityFailure("spiritual_wound_guarantee_worsening_invalid",
                        "an original guaranteed severity strictly greater than the current wound severity");
            }
            else
            {
                var creations = _registeredWoundInsertions.Where(pair => pair.Value.IsCreation &&
                    pair.Key.Admission.Source.ConflictId == admission.Source.ConflictId &&
                    pair.Key.Admission.Source.AffectedSide == admission.Source.AffectedSide).ToArray();
                if (creations.Length > 1)
                    return OpportunityFailure("spiritual_wound_duplicate_conflict_side_wound",
                        "one actual newly created wound for this conflict side");
                if (creations.Length == 1)
                {
                    var targetIssues = new List<ValidationIssue>();
                    var current = _effects!.ReadCurrentWoundView(this, _resources!, _source,
                        _effects.WoundReadVersion, targetIssues);
                    if (current != null && targetIssues.Count == 0)
                        sameConflictWound = _source.RevalidateSameConflictTarget(admission.Source,
                            creations[0].Value.Wound.WoundId, current, targetIssues);
                    if (current == null && targetIssues.Count == 0)
                        return OpportunityFailure("spiritual_wound_same_conflict_target_stale",
                            "the current owned wound view after the earlier conflict insertion");
                    if (targetIssues.Count != 0 || sameConflictWound == null)
                        return new(null, null, targetIssues);
                }
                else
                {
                    var priorIssues = new List<ValidationIssue>();
                    sameConflictWound = ReadSignedPriorConflictWound(lease, admission, priorIssues);
                    if (priorIssues.Count != 0)
                        return new(null, null, priorIssues);
                }
            }
            var source = admission.Source;
            if (sameConflictWound != null && source.GuaranteedSeverityRank is int satisfiedRank &&
                sameConflictWound.Severity.Rank >= satisfiedRank)
            {
                var satisfaction = CreateGuaranteeSatisfaction(lease, admission, sameConflictWound);
                if (satisfaction == null)
                    return OpportunityFailure("spiritual_wound_guarantee_satisfaction_unproven",
                        "the signed conflict instance and exact current conflict wound");
                if (_woundOpportunities.TryGetValue(admission, out var priorSatisfaction))
                    return priorSatisfaction.Satisfaction?.SameAs(satisfaction) == true
                        ? priorSatisfaction
                        : OpportunityFailure("spiritual_wound_guarantee_satisfaction_changed",
                            "the same owned wound identity and severity at this source boundary");
                var satisfied = new SpiritualWoundOpportunityResult(null, null, [], satisfaction);
                _woundOpportunities.Add(admission, satisfied);
                return satisfied;
            }
            if (_woundOpportunities.TryGetValue(admission, out var retained))
            {
                if (sameConflictWound != null &&
                    (retained.Opportunity?.WorseningTarget?.Wound.WoundId != sameConflictWound.WoundId ||
                     WoundMaterializationContract.SerializeCanonical(
                         retained.Opportunity.WorseningTarget.Wound) !=
                     WoundMaterializationContract.SerializeCanonical(sameConflictWound)))
                    return OpportunityFailure("spiritual_wound_same_conflict_target_stale",
                        "the unchanged current conflict wound retained by this opportunity");
                return retained;
            }
            if (source.GuaranteedSeverityRank is int guaranteedRank &&
                guaranteedRank > source.Calculation.MaximumSeverityRank &&
                (sameConflictWound == null || sameConflictWound.Severity.Rank < guaranteedRank))
                return OpportunityFailure("spiritual_wound_guarantee_above_maximum",
                    "an exact existing conflict wound satisfying a guarantee above this source's hard maximum");
            if (source.Calculation.MaximumSeverityRank == 0)
                return new(null, null, Array.Empty<ValidationIssue>());

            var actor = source.AffectedActor.Split(':', 2);
            var kind = actor[0] switch
            {
                "player_soul" => "player_soul",
                "guardian" => "guardian",
                "resident" or "shining_resident" => "resident",
                "radiant_actor" => "radiant_actor",
                "shining_faction_head" or "saref_agent" or "system_actor" or "custom_afterlife_actor" => "afterlife_actor",
                _ => null
            };
            if (actor.Length != 2 || kind == null)
                return OpportunityFailure("spiritual_wound_owner_invalid", "one persistent affected afterlife actor");
            var owner = new WoundOwnerCoordinate(_source.Realm, kind, actor[1], WoundCarrierCatalog.AfterlifeProfilesPath);
            var key = WoundAcceptedTurnFingerprintWriter.Compute(new string?[]
            {
                "book_of_eternity.spiritual_wound_source_event", "1", _source.SessionId,
                _source.RequestId, _source.SnapshotToken, source.ConflictId, source.ExchangeId, source.AffectedSide
            }).Replace("sha256:", string.Empty, StringComparison.Ordinal);
            var eventRef = "spiritual_wound_event_" + key;
            var evidence = new WoundOpportunityEventEvidence("formal", "spiritual_exchange_side", key,
                "harmful", source.Calculation.MaximumSeverityRank, "Духовное воздействие в обмене конфликта.");
            var events = WoundAcceptedEventAuthorityComposer.Compose(new(
                _source.SessionId, _source.RequestId, _source.SnapshotToken, _source.TurnNumber,
                new[] { new WoundAcceptedResponseEventCoordinate(eventRef, evidence.AuthorityKind, evidence.AuthorityId) }),
                new[] { new WoundSelectedEventEvidence(0, evidence) });
            if (!events.Success)
                return new(null, null, events.Issues);
            var binding = new WoundAcceptedTurnBinding(_source.SessionId, _source.RequestId, _source.SnapshotToken,
                _source.Realm, _source.TurnNumber, events.Events, events.EventsFingerprint);
            var art = source.OriginalSpecialArtJson == null ? null : JsonNode.Parse(source.OriginalSpecialArtJson);
            var sourceKind = art == null ? "spiritual_standard_art" : "spiritual_art";
            // Scalar standard arts have no acquired artId. This coordinate names the
            // original actor's operation, without inventing an acquired object identity.
            var sourceId = art?["artId"]?.GetValue<string>() ?? source.ActingActor + ":" + source.Operation;
            var composed = WoundOpportunityAuthority.Compose(new(binding, "spiritual_wound_" + key,
                "spiritual_wound_" + key, eventRef, owner, "spiritual", "afterlife_strain_transition_v1",
                sourceKind, sourceId, "active", evidence, source.Calculation.MaximumSeverityRank, null,
                new WoundOpportunitySafeContext("Участник духовного конфликта", evidence.ReadableCause,
                    new[] { "anatomical", "systemic", "mental", "spiritual_axis", "other" }),
                WorseningTarget: retraumaWound != null ? new(retraumaWound, "retrauma") :
                    sameConflictWound != null ? new(sameConflictWound, "same_conflict") : null));
            if (!composed.Success)
                return new(null, null, composed.Issues);
            var result = new SpiritualWoundOpportunityResult(binding, composed.Opportunity, Array.Empty<ValidationIssue>());
            _woundOpportunities.Add(admission, result);
            return source.GuaranteedSeverityRank == null ? result : OriginalSourceGuarantee.SealRetained(this, admission);
        }

        private static SpiritualWoundOpportunityResult OpportunityFailure(string code, string expected) =>
            new(null, null, new[] { SourceIssue(AfterlifeSpiritualConflictState.StatePath, code, expected) });

        /// <summary>
        /// Records a fulfilled source guarantee at its exact chronological wound read.
        /// Later legal worsening cannot rewrite this historical rank or create a transition here.
        /// </summary>
        internal sealed class OriginalGuaranteeSatisfaction
        {
            /// <summary>
            /// Retains the owned source, signed instance and exact current wound at satisfaction time.
            /// </summary>
            /// <param name="admission">
            /// Actual source admission at the current decision frontier.
            /// </param>
            /// <param name="instanceId">
            /// Conflict instance resolved from the signed original receipt and conflict image.
            /// </param>
            /// <param name="opportunityRef">
            /// Deterministic public source reference used by the receipt.
            /// </param>
            /// <param name="wound">
            /// Exact owner-validated current conflict wound.
            /// </param>
            /// <param name="readVersion">
            /// Wound draft version at this source boundary.
            /// </param>
            internal OriginalGuaranteeSatisfaction(WoundSourceAdmission admission,
                string instanceId, string opportunityRef, WoundMaterializationEnvelope wound,
                long readVersion)
            {
                Admission = admission;
                InstanceId = instanceId;
                OpportunityRef = opportunityRef;
                WoundId = wound.WoundId;
                SeverityRank = wound.Severity.Rank;
                WoundFingerprint = WoundAcceptedTurnFingerprintWriter.Compute(
                    [WoundMaterializationContract.SerializeCanonical(wound)]);
                WoundReadVersion = readVersion;
            }

            /// <summary>
            /// Gets the original source admission that proved this result.
            /// </summary>
            internal WoundSourceAdmission Admission { get; }
            /// <summary>
            /// Gets the signed durable conflict instance.
            /// </summary>
            internal string InstanceId { get; }
            /// <summary>
            /// Gets the deterministic source opportunity reference.
            /// </summary>
            internal string OpportunityRef { get; }
            /// <summary>
            /// Gets the exact previously created conflict wound identity.
            /// </summary>
            internal string WoundId { get; }
            /// <summary>
            /// Gets wound severity when the guarantee was met.
            /// </summary>
            internal int SeverityRank { get; }
            /// <summary>
            /// Gets the semantic fingerprint of the wound at satisfaction time.
            /// </summary>
            internal string WoundFingerprint { get; }
            /// <summary>
            /// Gets the wound draft read version at satisfaction time.
            /// </summary>
            internal long WoundReadVersion { get; }

            /// <summary>
            /// Compares repeated derivations without adopting a new source or wound claim.
            /// </summary>
            /// <param name="other">
            /// Freshly derived satisfaction at the same frontier.
            /// </param>
            /// <returns>
            /// <see langword="true"/> only when every source, instance and wound claim agrees.
            /// </returns>
            internal bool SameAs(OriginalGuaranteeSatisfaction other) =>
                ReferenceEquals(Admission, other.Admission) &&
                InstanceId == other.InstanceId && OpportunityRef == other.OpportunityRef &&
                WoundId == other.WoundId && SeverityRank == other.SeverityRank &&
                WoundFingerprint == other.WoundFingerprint && WoundReadVersion == other.WoundReadVersion;
        }

        /// <summary>
        /// Binds a no-transition result to signed instance identity and the owned current wound.
        /// </summary>
        /// <param name="lease">
        /// Active canonical lease for the signed original origin.
        /// </param>
        /// <param name="admission">
        /// Current exact source admission.
        /// </param>
        /// <param name="wound">
        /// Current same-conflict wound already proven by the owner.
        /// </param>
        /// <returns>
        /// An immutable satisfaction record, or <see langword="null"/> when signed identity cannot be proven.
        /// </returns>
        private OriginalGuaranteeSatisfaction? CreateGuaranteeSatisfaction(
            FileSystemManager.CanonicalWriteLease lease, WoundSourceAdmission admission,
            WoundMaterializationEnvelope wound)
        {
            try
            {
                var signed = ReadSignedConflictSideOriginCore(lease);
                var resolved = SpiritualWoundFirstOfferInstanceResolver.Resolve(
                    signed.Receipt, signed.Conflict, _source.Realm, _source.SessionId,
                    _source.RequestId, _source.SnapshotToken, _source.TurnNumber);
                if (resolved.Issues.Count != 0 || resolved.InstanceId is null)
                    return null;
                var source = admission.Source;
                var key = WoundAcceptedTurnFingerprintWriter.Compute(new string?[]
                {
                    "book_of_eternity.spiritual_wound_source_event", "1", _source.SessionId,
                    _source.RequestId, _source.SnapshotToken, source.ConflictId,
                    source.ExchangeId, source.AffectedSide
                })[7..];
                return new OriginalGuaranteeSatisfaction(admission, resolved.InstanceId,
                    "spiritual_wound_" + key, wound, _effects!.WoundReadVersion);
            }
            catch (InvalidOperationException)
            {
                return null;
            }
        }

        /// <summary>
        /// Reconstructs an earlier-turn conflict wound from signed receipts and the current owned wound view.
        /// A detached receipt claim alone never grants a worsening target.
        /// </summary>
        /// <param name="lease">
        /// Active canonical lease used to reauthenticate signed original images.
        /// </param>
        /// <param name="admission">
        /// Current exact source admission whose conflict side is being resolved.
        /// </param>
        /// <param name="issues">
        /// Receives signed-history, current-view or creation-transition failures.
        /// </param>
        /// <returns>
        /// The current active conflict wound, or <see langword="null"/> when no prior creation exists or proof fails.
        /// </returns>
        private WoundMaterializationEnvelope? ReadSignedPriorConflictWound(
            FileSystemManager.CanonicalWriteLease lease, WoundSourceAdmission admission,
            List<ValidationIssue> issues)
        {
            SpiritualSignedC1Origin signed;
            try { signed = ReadSignedConflictSideOriginCore(lease); }
            catch (InvalidOperationException)
            {
                issues.Add(SourceIssue(SpiritualWoundOpportunityReceiptState.StatePath,
                    "spiritual_wound_conflict_side_signed_origin_invalid",
                    "the exact signed original receipt and conflict images"));
                return null;
            }
            var resolved = SpiritualWoundFirstOfferInstanceResolver.Resolve(
                signed.Receipt, signed.Conflict, _source.Realm, _source.SessionId,
                _source.RequestId, _source.SnapshotToken, _source.TurnNumber);
            issues.AddRange(resolved.Issues);
            if (issues.Count != 0 || resolved.InstanceId == null || !signed.Receipt.Existed)
                return null;
            var parsed = SpiritualWoundOpportunityReceiptState.Parse(
                new UTF8Encoding(false, true).GetString(signed.Receipt.Bytes!),
                SpiritualWoundOpportunityReceiptState.StatePath);
            issues.AddRange(parsed.Issues);
            if (issues.Count != 0 || parsed.State == null)
                return null;
            var recorded = parsed.State.FindRecordedConflictSideWound(resolved.InstanceId,
                admission.Source.AffectedSide);
            issues.AddRange(recorded.Issues);
            if (issues.Count != 0 || recorded.Wound == null)
                return null;
            var current = _effects!.ReadCurrentWoundView(this, _resources!, _source,
                _effects.WoundReadVersion, issues);
            if (current == null || issues.Count != 0)
            {
                if (issues.Count == 0)
                    issues.Add(SourceIssue(AfterlifeSpiritualConflictState.StatePath,
                        "spiritual_wound_same_conflict_target_stale",
                        "the current owned wound view for the recorded conflict wound"));
                return null;
            }
            var wound = _source.RevalidateSameConflictTarget(admission.Source,
                recorded.Wound.WoundId, current, issues);
            if (wound == null || issues.Count != 0)
                return null;
            var history = WoundHistoryState.Parse(current.WoundHistory!.ToJsonString(),
                WoundHistoryState.HistoryPath);
            if (!history.IsValid || history.State == null ||
                !history.State.TryResolveExactTransition(recorded.Wound.CreateTransitionId,
                    out var creation) || creation is null || creation.Kind != "create" ||
                creation.WoundId != wound.WoundId || creation.WoundTransitionOrdinal != 1)
            {
                issues.Add(SourceIssue(WoundHistoryState.HistoryPath,
                    "spiritual_wound_conflict_side_origin_invalid",
                    "the signed first decision's actual creation transition in current wound history"));
                return null;
            }
            return wound;
        }

        /// <summary>
        /// Reads signed original receipt and conflict images for C1 or a direct unpublished capture.
        /// The direct path must match this source owner's exact signed request identity.
        /// </summary>
        /// <param name="lease">
        /// Active canonical lease for reading the selected signed snapshot.
        /// </param>
        /// <returns>
        /// Original signed images; neither candidate bytes nor a reconstructed receipt grants authority.
        /// </returns>
        private SpiritualSignedC1Origin ReadSignedConflictSideOriginCore(
            FileSystemManager.CanonicalWriteLease lease)
        {
            if (_signedC1OriginSnapshot != null)
                return ReadVerifiedSignedC1OriginCore(lease);
            EnsureCurrent(lease);
            var read = PendingTurnSnapshotReader.ReadCurrent(_validator._fs, lease,
                PendingTurnSnapshotPathSelection.CreateWithObservedOptionalPaths(
                    [SpiritualWoundSourceSession.SoulPath],
                    [SpiritualWoundOpportunityReceiptState.StatePath,
                     AfterlifeSpiritualConflictState.StatePath]));
            if (!read.Success || read.Snapshot is not { } signed ||
                signed.SessionId != _source.SessionId ||
                signed.RequestId != _source.RequestId ||
                signed.SnapshotToken != _source.SnapshotToken ||
                signed.TurnNumber != _source.TurnNumber ||
                signed.Realm != _source.Realm || !_source.IsCurrentOwner)
                throw new InvalidOperationException("The source's signed original snapshot changed.");
            return new(ReadSelectedImage(signed, SpiritualWoundOpportunityReceiptState.StatePath),
                ReadSelectedImage(signed, AfterlifeSpiritualConflictState.StatePath),
                "sha256:" + signed.SnapshotToken.ToLowerInvariant());
        }

        /// <summary>
        /// Revalidates admission ownership and file-backed inputs without requiring a historical target to remain unchanged.
        /// </summary>
        /// <param name="lease">
        /// Active canonical write lease under the capture gate.
        /// </param>
        /// <param name="admission">
        /// Exact retained source admission whose current frontier must still be owned.
        /// </param>
        /// <returns>
        /// Validation issues, or an empty collection when the admission and original inputs remain current.
        /// </returns>
        private async Task<IReadOnlyList<ValidationIssue>> CheckWoundAdmissionInputsCoreAsync(
            FileSystemManager.CanonicalWriteLease lease, WoundSourceAdmission admission)
        {
            EnsureCurrent(lease);
            if (!OwnsWoundAdmission(admission))
                return OpportunityFailure("spiritual_wound_frontier_mismatch", "the exact current wound admission").Issues;
            var inputIssues = await CheckRetainedInputsAsync(lease);
            if (inputIssues.Count != 0)
                return inputIssues;
            var sourceIssues = await _source.CheckContinuationInputsAsync(lease);
            if (sourceIssues.Count != 0)
                return sourceIssues;
            EnsureCurrent(lease);
            return OwnsWoundAdmission(admission) ? Array.Empty<ValidationIssue>() :
                OpportunityFailure("spiritual_wound_frontier_mismatch", "the unchanged current wound admission").Issues;
        }

        /// <summary>
        /// Proves that a retained original source declared a guaranteed result before this exchange.
        /// It carries no claim about the source's creation turn and cannot be restored from serialized hashes.
        /// </summary>
        internal sealed class OriginalSourceGuarantee
        {
            private readonly SpiritualOriginalTurnCapture _owner;
            private readonly PreparedSpiritualSource _source;
            private readonly WoundSourceAdmission _admission;
            private readonly string _claimsFingerprint;

            private OriginalSourceGuarantee(SpiritualOriginalTurnCapture owner, WoundSourceAdmission admission,
                WoundOpportunityAuthority opportunity)
            {
                _owner = owner;
                _admission = admission;
                var source = admission.Source;
                _source = source;
                RequiredSeverityRank = source.GuaranteedSeverityRank!.Value;
                RequestTurn = owner._source.TurnNumber;
                TriggerId = opportunity.OpportunityId + "_original_guarantee";
                _claimsFingerprint = WoundOpportunityAuthority.RecomputeAuthorityFingerprint(opportunity);
                SourceContractFingerprint = WoundAcceptedTurnFingerprintWriter.Compute(new string?[]
                {
                    "book_of_eternity.spiritual_original_guarantee_source", "1", source.ActingActor,
                    source.Operation, source.OriginalSpecialArtJson
                });
                AuthorityFingerprint = WoundAcceptedTurnFingerprintWriter.Compute(new string?[]
                {
                    "book_of_eternity.spiritual_original_guarantee", "1", _claimsFingerprint,
                    SourceContractFingerprint, source.Coordinate, source.ExchangeJson,
                    RequestTurn.ToString(System.Globalization.CultureInfo.InvariantCulture), TriggerId
                });
            }

            /// <summary>
            /// Gets the exact severity required by the original signed source.
            /// </summary>
            internal int RequiredSeverityRank { get; }
            /// <summary>
            /// Gets the pending request turn, not the source creation turn.
            /// </summary>
            internal int RequestTurn { get; }
            /// <summary>
            /// Gets the source-bound trigger identity written into the wound origin.
            /// </summary>
            internal string TriggerId { get; }
            /// <summary>
            /// Gets the fingerprint of the original art declaration and acting source coordinate.
            /// </summary>
            internal string SourceContractFingerprint { get; }
            /// <summary>
            /// Gets the fingerprint binding all opportunity claims to the original source and exchange.
            /// </summary>
            internal string AuthorityFingerprint { get; }

            /// <summary>
            /// Validates retained source ownership and every opportunity claim without rereading files.
            /// </summary>
            /// <param name="opportunity">
            /// Opportunity whose fields and companion are checked; altered claims are rejected even if resealed.
            /// </param>
            /// <returns>
            /// <see langword="true"/> for unchanged claims with this retained proof; otherwise, <see langword="false"/>.
            /// </returns>
            internal bool Matches(WoundOpportunityAuthority opportunity) =>
                _owner.IsCurrentOwner && _owner._source.Owns(_source) &&
                _owner._woundOpportunities.TryGetValue(_admission, out var retained) &&
                ReferenceEquals(retained.Opportunity?.OriginalSourceGuarantee, this) &&
                ReferenceEquals(opportunity.OriginalSourceGuarantee, this) &&
                opportunity.GuaranteedTrigger == null &&
                opportunity.MinimumSeverityRank == RequiredSeverityRank &&
                RequiredSeverityRank <= opportunity.MaximumSeverityRank &&
                string.Equals(_claimsFingerprint, WoundOpportunityAuthority.RecomputeAuthorityFingerprint(
                    opportunity with { OriginalSourceGuarantee = null }), StringComparison.Ordinal);

            /// <summary>
            /// Attaches a guarantee only to the original owner's already retained opportunity.
            /// </summary>
            /// <param name="owner">
            /// Capture retaining both the original source and its derived opportunity.
            /// </param>
            /// <param name="admission">
            /// Current source admission used to locate the retained composition; no caller opportunity is accepted.
            /// </param>
            /// <returns>
            /// The retained guaranteed composition or validation issues when ownership is unavailable.
            /// </returns>
            internal static SpiritualWoundOpportunityResult SealRetained(
                SpiritualOriginalTurnCapture owner, WoundSourceAdmission admission)
            {
                if (!owner.OwnsWoundAdmission(admission) ||
                    admission.Source.GuaranteedSeverityRank is not (>= 1 and <= 4) ||
                    admission.Source.OriginalSpecialArtJson == null ||
                    !owner._woundOpportunities.TryGetValue(admission, out var retained) || retained.Opportunity == null)
                    return OpportunityFailure("spiritual_wound_guarantee_unowned", "one original signed guaranteed source and retained opportunity");
                if (retained.Opportunity.OriginalSourceGuarantee != null)
                    return retained;
                var claims = retained.Opportunity with { MinimumSeverityRank = admission.Source.GuaranteedSeverityRank };
                var proof = new OriginalSourceGuarantee(owner, admission, claims);
                var sealedOpportunity = claims with { OriginalSourceGuarantee = proof };
                sealedOpportunity = sealedOpportunity with
                {
                    AuthorityFingerprint = WoundOpportunityAuthority.RecomputeAuthorityFingerprint(sealedOpportunity)
                };
                var result = retained with { Opportunity = sealedOpportunity };
                owner._woundOpportunities[admission] = result;
                return result;
            }
        }
    }
}
