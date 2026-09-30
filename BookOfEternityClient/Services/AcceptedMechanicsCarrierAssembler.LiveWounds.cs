using System.Text.Json;
using System.Text.Json.Nodes;

namespace BookOfEternityClient.Services;

internal static partial class AcceptedMechanicsCarrierAssembler
{
    /// <summary>
    /// Composes a completed spiritual wound chain over the exact final effect and owner roots.
    /// </summary>
    /// <param name="effectPlan">
    /// Final accepted effect plan from the same completed owner as <paramref name="liveWounds"/>.
    /// </param>
    /// <param name="ownerCompanionAfterImages">
    /// Other typed owner roots participating in the common reduction.
    /// </param>
    /// <param name="liveWounds">
    /// Ordered owner-sealed actual wound insertion outputs.
    /// </param>
    /// <param name="initialStages">
    /// Optional ordinary wound stage whose result preceded the live insertion chain.
    /// </param>
    /// <returns>
    /// One typed carrier composition and wound publication, or fail-closed diagnostics.
    /// </returns>
    internal static AcceptedMechanicsCarrierCompositionResult ComposeLive(
        EffectAcceptedTurnPlan? effectPlan,
        IReadOnlyDictionary<string, JsonObject> ownerCompanionAfterImages,
        SpiritualLiveWoundCompletion liveWounds,
        AcceptedMechanicsWoundStageBundle? initialStages)
    {
        ArgumentNullException.ThrowIfNull(liveWounds);
        if (effectPlan is null || !liveWounds.MatchesEffects(effectPlan) ||
            !effectPlan.IsAcceptedBoundaryComplete ||
            WoundAcceptedTurnFingerprints.ComputeAcceptedEffectPlanPayload(effectPlan) !=
                liveWounds.FinalEffectFingerprint ||
            effectPlan.AcceptedBoundaryBasePlanFingerprint !=
                liveWounds.BaseEffectFingerprint)
            return Failed("spiritual_live_wound_effect_completion_mismatch",
                "The live wound chain does not belong to the final completed effect plan.",
                "one exact completed effect owner", "missing or mismatched");
        var baseComposition = Compose(effectPlan, ownerCompanionAfterImages, null);
        if (!baseComposition.Success)
            return baseComposition;
        try
        {
            var insertions = liveWounds.Insertions;
            var firstBefore = insertions[0].OperationBefore;
            var final = liveWounds.FinalState;
            if (firstBefore.WoundCarriers is null ||
                firstBefore.WoundIdentity is null ||
                firstBefore.WoundHistory is null ||
                initialStages is not null &&
                (!JsonNode.DeepEquals(initialStages.FinalPlan.IdentityIndexAfterImage,
                    firstBefore.WoundIdentity) ||
                 !JsonNode.DeepEquals(initialStages.FinalPlan.HistoryAfterImage,
                    firstBefore.WoundHistory)))
                return Failed("spiritual_live_wound_seed_mismatch",
                    "The live chain does not begin at the accepted wound seed.",
                    "one exact prior wound state", "different seed");
            var effectRoots = baseComposition.EffectCarrierAfterImages.ToDictionary(
                static pair => pair.Key, static pair => pair.Value.DeepClone().AsObject(),
                StringComparer.Ordinal);
            var ownerRoots = baseComposition.OwnerCompanionAfterImages.ToDictionary(
                static pair => pair.Key, static pair => pair.Value.DeepClone().AsObject(),
                StringComparer.Ordinal);
            var woundRoots = new Dictionary<string, JsonObject>(StringComparer.Ordinal);
            var initialContributions = initialStages?.FinalPlan.CarrierContributions
                .ToDictionary(static contribution => contribution.Owner);
            var owners = insertions.Select(static insertion => insertion.Wound.Owner)
                .Concat(initialContributions is null
                    ? Enumerable.Empty<WoundOwnerCoordinate>()
                    : initialContributions.Keys)
                .Distinct()
                .ToArray();
            foreach (var owner in owners)
            {
                var path = owner.CarrierPath;
                var seedRoot = WoundCarrierCollectionAuthority.GetRoot(
                    firstBefore.WoundCarriers, path);
                var finalRoot = WoundCarrierCollectionAuthority.GetRoot(
                    final.Carriers, path);
                var selected = woundRoots.TryGetValue(path, out var existing)
                    ? existing
                    : effectRoots.TryGetValue(path, out var effectRoot)
                        ? effectRoot.DeepClone().AsObject()
                        : ownerRoots.TryGetValue(path, out var ownerRoot)
                            ? ownerRoot.DeepClone().AsObject()
                            : initialContributions?.ContainsKey(owner) == true
                                ? WoundCarrierCollectionAuthority.GetRoot(
                                    initialStages!.Input.PreTurnCarriers, path)?.DeepClone().AsObject()
                                : seedRoot?.DeepClone().AsObject();
                if (seedRoot is null || finalRoot is null || selected is null ||
                    !WoundCarrierCollectionAuthority.TryResolve(seedRoot, owner,
                        out var seedCollection, out _) ||
                    !WoundCarrierCollectionAuthority.TryResolve(finalRoot, owner,
                        out var finalCollection, out _) ||
                    !WoundCarrierCollectionAuthority.TryResolve(selected, owner,
                        out var selectedCollection, out _))
                    return Failed("spiritual_live_wound_carrier_baseline_mismatch",
                        "Another producer changed the selected wound collection before typed assembly.",
                        path, owner.OwnerId);
                if (initialContributions is not null &&
                    initialContributions.TryGetValue(owner, out var contribution))
                {
                    var originalRoot = WoundCarrierCollectionAuthority.GetRoot(
                        initialStages!.Input.PreTurnCarriers, path);
                    if (originalRoot is null ||
                        !WoundCarrierCollectionAuthority.TryResolve(originalRoot, owner,
                            out var originalCollection, out _) ||
                        !JsonNode.DeepEquals(originalCollection, selectedCollection) ||
                        WoundCarrierCollectionAuthority.ComputeFingerprint(owner, originalCollection) !=
                        contribution.ExpectedWoundCollectionFingerprint)
                        return Failed("spiritual_live_wound_initial_stage_baseline_mismatch",
                            "The initial wound stage does not match the selected original collection.",
                            path, owner.OwnerId);
                    foreach (var mutation in contribution.Mutations)
                    {
                        var issue = ApplyMutation(selectedCollection, owner, mutation);
                        if (issue is not null)
                            return Failed(issue);
                    }
                }
                if (!JsonNode.DeepEquals(seedCollection, selectedCollection))
                    return Failed("spiritual_live_wound_carrier_seed_mismatch",
                        "The initial wound stage does not produce the live chain's first collection.",
                        path, owner.OwnerId);
                selectedCollection.Clear();
                foreach (var wound in finalCollection)
                    selectedCollection.Add(wound?.DeepClone());
                woundRoots[path] = selected;
                effectRoots.Remove(path);
                ownerRoots.Remove(path);
            }
            var assembled = WoundAcceptedTurnData.CloneWoundCarriers(final.Carriers)!;
            foreach (var pair in effectRoots.Concat(ownerRoots).Concat(woundRoots)
                         .Where(pair => WoundCarrierCollectionAuthority.IsRegisteredPath(pair.Key)))
                assembled = WoundCarrierCollectionAuthority.WithRoot(
                    assembled, pair.Key, pair.Value);
            var catalog = WoundCarrierCatalog.Build(assembled);
            var identity = WoundIdentityState.Parse(final.Identity.ToJsonString(),
                WoundIdentityState.StatePath);
            var history = WoundHistoryState.Parse(final.History.ToJsonString(),
                WoundHistoryState.HistoryPath);
            var issues = catalog.Issues.Concat(identity.Issues).Concat(history.Issues).ToList();
            if (identity.State is not null && history.State is not null &&
                catalog.Issues.Count == 0)
                issues.AddRange(history.State.ValidateAgreement(identity.State, catalog));
            if (issues.Count != 0)
                return Failed("spiritual_live_wound_publication_agreement_invalid",
                    "The final composed wound carriers disagree with identity or history.",
                    "one complete canonical wound state",
                    string.Join(",", issues.Select(static issue => issue.Code ?? issue.FilePath)));
            var publication = AcceptedMechanicsWoundPublication.CreateValidatedLive(
                woundRoots, final.Identity, final.History,
                liveWounds.ProofFingerprint, liveWounds.FinalEffectFingerprint,
                PublicationProof);
            return AcceptedMechanicsCarrierCompositionResult.CreateValidated(
                effectRoots, ownerRoots, publication, [], PublicationProof);
        }
        catch (Exception error) when (error is ArgumentException or InvalidOperationException or
            JsonException or NullReferenceException)
        {
            return Failed("spiritual_live_wound_common_assembly_invalid",
                "The owner-sealed wound chain could not be assembled.",
                "one exact typed live wound composition", error.GetType().Name);
        }
    }
}
