namespace BookOfEternityClient.Services;

/// <summary>
/// Holds detached outputs of the exact ordered spiritual wound insertions sealed by a completed owner.
/// This evidence alone grants no accepted-plan or canonical publication authority.
/// </summary>
internal sealed class SpiritualLiveWoundCompletion
{
    private readonly Insertion[] _insertions;
    private readonly EffectAcceptedTurnPlan _completedEffects;
    private readonly AcceptedMechanicsResourcePlanningResult _completedResources;

    /// <summary>
    /// Detaches the ordered insertion evidence after its live owner has checked completion and registration.
    /// </summary>
    /// <param name="insertions">
    /// Complete registered insertion chain in actual draft version order.
    /// </param>
    /// <param name="completedEffects">
    /// Exact effect result issued by the validated draft.
    /// </param>
    /// <param name="completedResources">
    /// Exact resource result whose owner registered the insertion chain.
    /// </param>
    /// <param name="baseEffectFingerprint">
    /// Fingerprint of the immutable accepted effect base owned by the draft.
    /// </param>
    /// <param name="finalEffectFingerprint">
    /// Fingerprint of the exact effect result issued at completion.
    /// </param>
    private SpiritualLiveWoundCompletion(IReadOnlyList<Insertion> insertions,
        EffectAcceptedTurnPlan completedEffects,
        AcceptedMechanicsResourcePlanningResult completedResources,
        string baseEffectFingerprint, string finalEffectFingerprint)
    {
        if (insertions.Count == 0)
            throw new ArgumentException("A live wound completion requires an insertion.", nameof(insertions));
        _insertions = insertions.ToArray();
        _completedEffects = completedEffects;
        _completedResources = completedResources;
        BaseEffectFingerprint = baseEffectFingerprint;
        FinalEffectFingerprint = finalEffectFingerprint;
        var fields = new List<string?>
        {
            "book_of_eternity.spiritual_live_wound_completion", "1",
            BaseEffectFingerprint, FinalEffectFingerprint
        };
        foreach (var insertion in _insertions)
        {
            var before = insertion.OperationBefore;
            var reduced = insertion.ReducedState;
            fields.Add(insertion.SourceCoordinate);
            fields.Add(insertion.ProposalRawFingerprint);
            fields.Add(insertion.ExchangeOrdinal.ToString(System.Globalization.CultureInfo.InvariantCulture));
            fields.Add(insertion.VersionAfter.ToString(System.Globalization.CultureInfo.InvariantCulture));
            fields.Add(WoundAcceptedTurnFingerprints.ComputeInput(insertion.Input));
            fields.Add(insertion.Wound.WoundId);
            fields.Add(insertion.Wound.LastTransition.TransitionId);
            AddCarrierImage(fields, before.WoundCarriers!);
            fields.Add(WoundAcceptedTurnFingerprintWriter.CanonicalJson(before.WoundIdentity));
            fields.Add(WoundAcceptedTurnFingerprintWriter.CanonicalJson(before.WoundHistory));
            AddCarrierImage(fields, reduced.Carriers);
            fields.Add(WoundAcceptedTurnFingerprintWriter.CanonicalJson(reduced.Identity));
            fields.Add(WoundAcceptedTurnFingerprintWriter.CanonicalJson(reduced.History));
        }
        ProofFingerprint = WoundAcceptedTurnFingerprintWriter.Compute(fields);
    }

    /// <summary>
    /// Adds every registered wound carrier root to an ordered proof image.
    /// </summary>
    /// <param name="fields">
    /// Fingerprint fields to extend in canonical carrier-path order.
    /// </param>
    /// <param name="carriers">
    /// Exact detached wound carriers from one operation boundary.
    /// </param>
    private static void AddCarrierImage(List<string?> fields, WoundCarrierCatalogInput carriers)
    {
        fields.Add(WoundAcceptedTurnFingerprintWriter.CanonicalJson(carriers.PlayerWounds));
        fields.Add(WoundAcceptedTurnFingerprintWriter.CanonicalJson(carriers.NpcWounds));
        fields.Add(WoundAcceptedTurnFingerprintWriter.CanonicalJson(carriers.EnemyCombatants));
        fields.Add(WoundAcceptedTurnFingerprintWriter.CanonicalJson(carriers.AllyCombatants));
        fields.Add(WoundAcceptedTurnFingerprintWriter.CanonicalJson(carriers.AfterlifeProfiles));
    }

    /// <summary>
    /// Issues a wound proof only after the completed draft and resource owner validate the whole chain.
    /// </summary>
    /// <param name="owner">
    /// Actual completed effect draft retaining every insertion.
    /// </param>
    /// <param name="completedPlan">
    /// Exact effect result issued by <paramref name="owner"/>.
    /// </param>
    /// <param name="routing">
    /// Installed resource routing from the successful completion.
    /// </param>
    /// <param name="transcript">
    /// Final accepted effect transcript consumed by <paramref name="owner"/>.
    /// </param>
    /// <param name="resources">
    /// Completed resource owner that registered every insertion.
    /// </param>
    /// <param name="selected">
    /// Capture-retained source selections paired with their real insertions in causal order.
    /// </param>
    /// <returns>
    /// The owner-sealed detached proof, or <see langword="null"/> on a mismatched chain.
    /// </returns>
    internal static SpiritualLiveWoundCompletion? Seal(
        EffectAcceptedTurnPlanner.EffectAcceptedDraft owner,
        EffectAcceptedTurnPlan completedPlan,
        EffectAcceptedTurnPlanner.BaseResourceRouting routing,
        AcceptedEffectBoundaryTranscript transcript,
        AcceptedMechanicsPlanner.ResourceExecutionSession resources,
        IReadOnlyList<(ValidationService.SpiritualOriginalTurnCapture.WoundSelection Selection,
            EffectAcceptedTurnPlanner.EffectAcceptedDraft.EffectDraftWoundInsertion Insertion)> selected)
    {
        ArgumentNullException.ThrowIfNull(owner);
        ArgumentNullException.ThrowIfNull(selected);
        // Both the owner check and detached proof must consume one immutable pairing image.
        var frozen = selected.ToArray();
        if (!owner.AuthenticatesSpiritualLiveWoundCompletion(
                completedPlan, routing, transcript, resources, frozen) ||
            resources.Result is not { IsValid: true } completedResources ||
            string.IsNullOrEmpty(completedPlan.AcceptedBoundaryBasePlanFingerprint))
            return null;
        return new SpiritualLiveWoundCompletion(
            frozen.Select(pair => new Insertion(pair.Selection, pair.Insertion)).ToArray(),
            completedPlan, completedResources,
            completedPlan.AcceptedBoundaryBasePlanFingerprint,
            WoundAcceptedTurnFingerprints.ComputeAcceptedEffectPlanPayload(completedPlan));
    }

    /// <summary>
    /// Checks exact completed owner-result identity before binding this proof to a reduction.
    /// </summary>
    /// <param name="effects">
    /// Effect result retained by the candidate ordinary reduction.
    /// </param>
    /// <param name="resources">
    /// Resource result retained by the candidate ordinary reduction.
    /// </param>
    /// <returns>
    /// <see langword="true"/> only for the two exact result objects sealed with this proof.
    /// </returns>
    internal bool Matches(EffectAcceptedTurnPlan? effects,
        AcceptedMechanicsResourcePlanningResult resources) =>
        ReferenceEquals(_completedEffects, effects) &&
        ReferenceEquals(_completedResources, resources);

    /// <summary>
    /// Checks the exact effect result retained by the completed wound owner.
    /// </summary>
    /// <param name="effects">
    /// Candidate final accepted effect result.
    /// </param>
    /// <returns>
    /// <see langword="true"/> only for the original result object sealed by the owner.
    /// </returns>
    internal bool MatchesEffects(EffectAcceptedTurnPlan? effects) =>
        ReferenceEquals(_completedEffects, effects);

    /// <summary>
    /// Gets the immutable chronological insertion evidence.
    /// </summary>
    internal IReadOnlyList<Insertion> Insertions => Array.AsReadOnly(_insertions);
    /// <summary>
    /// Gets the original accepted effect base fingerprint.
    /// </summary>
    internal string BaseEffectFingerprint { get; }
    /// <summary>
    /// Gets the completed effect plan fingerprint.
    /// </summary>
    internal string FinalEffectFingerprint { get; }
    /// <summary>
    /// Gets the deterministic fingerprint of this owner-sealed ordered insertion chain.
    /// </summary>
    internal string ProofFingerprint { get; }
    /// <summary>
    /// Gets the final detached wound state produced by the last real insertion.
    /// </summary>
    internal WoundStateReduction FinalState => _insertions[^1].ReducedState;

    /// <summary>
    /// Holds one source-bound insertion's actual preparation, reduction and effect writer evidence.
    /// </summary>
    internal sealed class Insertion
    {
        private readonly WoundAcceptedTurnInput _input;
        private readonly WoundOperationBeforeData _before;
        private readonly WoundPreparedAcceptedTurnPlan _prepared;
        private readonly WoundStateReduction _reduced;
        private readonly WoundMaterializationEnvelope _wound;
        private readonly EffectAcceptedApplicationResult[] _applications;

        /// <summary>
        /// Detaches one insertion registered at its exact spiritual source frontier.
        /// </summary>
        /// <param name="selection">
        /// Actual capture-retained selection associated with <paramref name="insertion"/>.
        /// </param>
        /// <param name="insertion">
        /// Real draft insertion retaining the prepared and reduced outputs.
        /// </param>
        internal Insertion(ValidationService.SpiritualOriginalTurnCapture.WoundSelection selection,
            EffectAcceptedTurnPlanner.EffectAcceptedDraft.EffectDraftWoundInsertion insertion)
        {
            SourceCoordinate = selection.Admission.Source.Coordinate;
            ProposalRawFingerprint = selection.ProposalRawFingerprint;
            ExchangeOrdinal = selection.Admission.Interval.Ordinal;
            VersionAfter = insertion.VersionAfter;
            _input = selection.Input;
            _before = insertion.OperationBefore;
            _prepared = insertion.Prepared;
            _reduced = insertion.ReducedState;
            _wound = insertion.Wound;
            _applications = insertion.ApplicationResults.Select(
                WoundAcceptedTurnData.CloneApplicationResult).ToArray();
            ApplicationReceipts = Array.AsReadOnly(insertion.Applications.ToArray());
            IdentityWrites = Array.AsReadOnly(insertion.IdentityWrites.ToArray());
            Allocations = Array.AsReadOnly(insertion.Allocations.ToArray());
            CarrierEdits = Array.AsReadOnly(insertion.CarrierEdits.ToArray());
        }

        /// <summary>
        /// Gets the original source coordinate associated with this insertion.
        /// </summary>
        internal string SourceCoordinate { get; }
        /// <summary>
        /// Gets the exact raw proposal fingerprint accepted by the source owner.
        /// </summary>
        internal string ProposalRawFingerprint { get; }
        /// <summary>
        /// Gets the actual closed exchange ordinal.
        /// </summary>
        internal long ExchangeOrdinal { get; }
        /// <summary>
        /// Gets the draft wound version after this insertion.
        /// </summary>
        internal long VersionAfter { get; }
        /// <summary>
        /// Gets the validated source-bound wound input.
        /// </summary>
        internal WoundAcceptedTurnInput Input => WoundAcceptedTurnData.CloneInput(_input)!;
        /// <summary>
        /// Gets the operation-before snapshots read by the actual draft.
        /// </summary>
        internal WoundOperationBeforeData OperationBefore => new(
            _before.WoundCarriers, _before.WoundIdentity, _before.WoundHistory,
            _before.EffectCarriers, _before.EffectIdentity);
        /// <summary>
        /// Gets the actual prepared wound stage.
        /// </summary>
        internal WoundPreparedAcceptedTurnPlan Prepared => _prepared.ClonePreservingCacheAuthority();
        /// <summary>
        /// Gets the actual detached wound reduction.
        /// </summary>
        internal WoundStateReduction ReducedState => new(_reduced.Carriers,
            _reduced.Identity, _reduced.History, _reduced.Contributions, _reduced.Intents);
        /// <summary>
        /// Gets the actual wound envelope, including its allocated transition identity.
        /// </summary>
        internal WoundMaterializationEnvelope Wound => WoundAcceptedTurnData.CloneWound(_wound)!;
        /// <summary>
        /// Gets the typed effect results supplied to the wound reducer.
        /// </summary>
        internal IReadOnlyList<EffectAcceptedApplicationResult> ApplicationResults =>
            Array.AsReadOnly(_applications.Select(WoundAcceptedTurnData.CloneApplicationResult).ToArray());
        /// <summary>
        /// Gets the real draft application receipts retained at insertion time.
        /// </summary>
        internal IReadOnlyList<EffectAcceptedTurnPlanner.EffectDraftApplicationReceipt> ApplicationReceipts { get; }
        /// <summary>
        /// Gets the real effect identity writer receipts.
        /// </summary>
        internal IReadOnlyList<EffectIdentityWriteReceipt> IdentityWrites { get; }
        /// <summary>
        /// Gets the real effect identity allocation receipts.
        /// </summary>
        internal IReadOnlyList<EffectIdentityAllocationReceipt> Allocations { get; }
        /// <summary>
        /// Gets the real effect carrier edit receipts.
        /// </summary>
        internal IReadOnlyList<EffectAcceptedTurnPlanner.EffectDraftCarrierEdit> CarrierEdits { get; }
    }
}
