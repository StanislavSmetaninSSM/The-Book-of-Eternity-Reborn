using System.Globalization;
using System.Text.Json.Nodes;

namespace BookOfEternityClient.Services;

internal static partial class WoundAcceptedTurnPlanner
{
    /// <summary>
    /// Rederives the exact item consumption command from its sealed finalization, without item authority.
    /// </summary>
    internal static MortalItemConsumptionCommand CreateTreatmentItemConsumptionCommand(string acceptedFingerprint,
        string requestFingerprint, string terminalFingerprint, string finalizationFingerprint, int ordinal,
        MortalWoundTreatmentResourceConsumptionIntent consumption)
    {
        var fingerprint = WoundAcceptedTurnFingerprintWriter.Compute(new string?[]
        {
            "book_of_eternity.mortal_wound_treatment.item_consumption_command", "1", acceptedFingerprint,
            requestFingerprint, terminalFingerprint, finalizationFingerprint, ordinal.ToString("D4", CultureInfo.InvariantCulture),
            consumption.AuthorityRef, consumption.Quantity.ToString(CultureInfo.InvariantCulture),
            consumption.ClaimFingerprint, consumption.IntentFingerprint
        });
        return new(ordinal + 1, consumption.AuthorityRef, checked((int)consumption.Quantity), consumption.ClaimFingerprint,
            "mitrn_" + fingerprint["sha256:".Length..], "mortal_wound_treatment",
            "treatment_item_" + fingerprint["sha256:".Length..("sha256:".Length + 24)]);
    }

    /// <summary>
    /// Rederives the exact registered resource source from immutable terminal inputs and quantities.
    /// </summary>
    internal static string ComputeTreatmentResourceConsumptionSourceFingerprint(string acceptedFingerprint,
        string requestFingerprint, string terminalFingerprint, string finalizationFingerprint, string intentFingerprint,
        string eventRef, ResourceCoordinate coordinate, decimal before, decimal after) =>
        WoundAcceptedTurnFingerprintWriter.Compute(new string?[]
        {
            "book_of_eternity.mortal_wound_treatment.resource_publication_source", "1", acceptedFingerprint,
            requestFingerprint, terminalFingerprint, finalizationFingerprint, intentFingerprint, eventRef, Describe(coordinate),
            before.ToString(CultureInfo.InvariantCulture), after.ToString(CultureInfo.InvariantCulture)
        });

    // Detached calculations grant no continuation, hold, item-slot or publication
    // authority. The old homogeneous and new joint issuers authenticate separately.
    private sealed record TreatmentRegisteredResourceContribution(
        IReadOnlyList<ResourceMutationSourceExport> Sources,
        IReadOnlyList<ResourceMutationIntent> Mutations,
        IReadOnlyList<MortalWoundTreatmentResourcePublicationDraft.ExpectedTransition> ExpectedTransitions,
        IReadOnlyList<ValidationIssue> Issues);

    private static TreatmentRegisteredResourceContribution CalculateTreatmentRegisteredResources(
        string acceptedStateFingerprint, string requestFingerprint, string resultFingerprint, int turn,
        string semanticFingerprint, MortalWoundTreatmentResourceFinalization finalization,
        IReadOnlyDictionary<string, MortalWoundTreatmentResourceClaim> claims,
        ResourceDefinitionCatalog definitions, ResourceStateLedger state, ResourceOwnerAuthority owners, string identitySeed)
    {
        var sources = new List<ResourceMutationSourceExport>();
        var mutations = new List<ResourceMutationIntent>();
        var expected = new List<MortalWoundTreatmentResourcePublicationDraft.ExpectedTransition>();
        var issues = new List<ValidationIssue>();
        var running = new Dictionary<ResourceCoordinate, decimal>(
            ResourceCoordinateComparer.Instance);
        var priorByCoordinate = new Dictionary<ResourceCoordinate, ResourceOperationKey>(
            ResourceCoordinateComparer.Instance);
        for (var ordinal = 0; ordinal < finalization.Consumptions.Count; ordinal++)
        {
            var consumption = finalization.Consumptions[ordinal];
            if (!string.Equals(
                    consumption.Kind,
                    "resource_quantity",
                    StringComparison.Ordinal))
                continue;
            if (!claims.TryGetValue(consumption.ClaimFingerprint, out var claim) ||
                !ConsumptionAgrees(consumption, claim) ||
                !ResourceDefinitionCatalog.TryParseOwnerKind(
                    consumption.OwnerKind,
                    out var ownerKind))
            {
                issues.Add(PublicationIssue(
                    "mortal_wound_treatment_publication_resource_finalization_mismatch",
                    "one exact selected resource claim and typed owner coordinate",
                    consumption.ClaimFingerprint));
                continue;
            }
            var owner = owners.Resolve(new ResourceOwnerRequest(
                consumption.Realm,
                ownerKind,
                consumption.AuthorityRef,
                consumption.OwnerId,
                OwnerRef: null));
            var coordinate = owner.Entry is null
                ? new ResourceCoordinate(
                    consumption.Realm,
                    ownerKind,
                    consumption.OwnerId,
                    consumption.AuthorityRef)
                : new ResourceCoordinate(
                    owner.Entry.Key.Realm,
                    owner.Entry.Key.OwnerKind,
                    owner.Entry.Key.ResourceOwnerId,
                    consumption.AuthorityRef);
            var definitionResolved = definitions.TryResolveExact(
                consumption.AuthorityRef,
                out var definition);
            var liveResolved = state.TryResolveExact(coordinate, out var live);
            if (!owner.Success || owner.Entry is null ||
                !definitionResolved ||
                definition is null ||
                definition.NumericKind != ResourceNumericKind.Integer ||
                definition.Quantum != 1m ||
                !definition.AllowedOwnerKinds.Contains(ownerKind) ||
                !definition.AllowedOperations.Contains(ResourceOperation.Spend) ||
                !liveResolved ||
                live is null ||
                live.State != ResourceLifecycleState.Active ||
                live.Current != claim.AvailableQuantity ||
                decimal.Truncate(live.Current) != live.Current)
            {
                issues.Add(PublicationIssue(
                    "mortal_wound_treatment_publication_resource_state_changed",
                    "one exact active integer resource coordinate equal to the sealed held quantity witness",
                    DescribeResourceStateMismatch(
                        coordinate,
                        owner,
                        definition,
                        live,
                        claim.AvailableQuantity)));
                continue;
            }
            if (!running.TryGetValue(coordinate, out var before))
                before = live.Current;
            decimal after;
            try
            {
                after = checked(before - consumption.Quantity);
            }
            catch (OverflowException)
            {
                issues.Add(PublicationIssue(
                    "mortal_wound_treatment_publication_resource_state_changed",
                    "checked full-quantity resource spend",
                    Describe(coordinate)));
                continue;
            }
            if (after < definition.MinimumPolicy.Value)
            {
                issues.Add(PublicationIssue(
                    "mortal_wound_treatment_publication_resource_state_changed",
                    "sufficient current quantity for the full selected spend",
                    Describe(coordinate)));
                continue;
            }

            var eventRef = CreateEventRef(
                semanticFingerprint,
                finalization.FinalizationFingerprint,
                ordinal);
            var sourceFingerprint = ComputeTreatmentResourceConsumptionSourceFingerprint(acceptedStateFingerprint,
                requestFingerprint, resultFingerprint, finalization.FinalizationFingerprint, consumption.IntentFingerprint,
                eventRef, coordinate, before, after);
            var source = new ResourceMutationSourceExport(
                "registered_system_outcome",
                eventRef,
                sourceFingerprint,
                ResourceMutationSourceState.Active,
                SameTurn: true);
            sources.Add(source);
            var dependencies = priorByCoordinate.TryGetValue(
                    coordinate,
                    out var prior)
                ? new[] { prior }
                : Array.Empty<ResourceOperationKey>();
            var mutation = new ResourceMutationIntent(
                eventRef,
                coordinate,
                consumption.Quantity,
                new ResourceMutationSourceRequest(
                    "registered_system_outcome",
                    eventRef,
                    ResourceOperation.Spend),
                dependencies,
                Array.Empty<ResourceMutationEventRequirement>(),
                ReceiptId: null);
            mutations.Add(mutation);
            priorByCoordinate[coordinate] = mutation.Key;
            running[coordinate] = after;

            var sourceCatalog = ResourceMutationSourceCatalog.Create(new[] { source });
            var route = sourceCatalog.Catalog?.Resolve(
                mutation.Source,
                definition,
                coordinate);
            if (route is not { IsValid: true, Route: not null })
            {
                issues.AddRange(sourceCatalog.Issues);
                if (route is not null)
                    issues.AddRange(route.Issues);
                continue;
            }
            var identities = MortalWoundTreatmentResourcePublicationAuthority
                .CreateMutationIdentities(identitySeed, mutation);
            expected.Add(new MortalWoundTreatmentResourcePublicationDraft.ExpectedTransition(
                ordinal,
                turn,
                eventRef,
                eventRef,
                identities.OperationId,
                identities.TransitionId,
                coordinate,
                consumption.Quantity,
                before,
                after,
                route.Route.Priority,
                sourceFingerprint,
                route.Route.PolicyBinding.AuthorityFingerprint,
                EmitsDepleted: before > definition.MinimumPolicy.Value &&
                               after == definition.MinimumPolicy.Value));
        }
        return new(sources, mutations, expected, issues);
    }

    private sealed record TreatmentItemConsumptionContribution(
        MortalItemConsumptionPlanningInput Input, MortalItemConsumptionPlanningResult Consumption,
        IReadOnlyDictionary<string, JsonNode?> FinalRoots, IReadOnlyDictionary<string, JsonObject> PublicationAfterImages,
        ResourceOwnerAuthority FinalOwners);
    private sealed record TreatmentItemContributionBuild(TreatmentItemConsumptionContribution? Contribution,
        IReadOnlyList<ValidationIssue> Issues);

    private static TreatmentItemContributionBuild CalculateTreatmentItemConsumption(
        string acceptedStateFingerprint, string requestFingerprint, string resultFingerprint, int turn,
        MortalWoundTreatmentResourceFinalization finalization,
        IReadOnlyDictionary<string, MortalWoundTreatmentResourceClaim> claims,
        ResourceDefinitionCatalog definitions, ResourceStateLedger state, ResourceOwnerAuthority owners, string identitySeed,
        MortalItemPublicationBaselineResult baseline, IReadOnlyDictionary<string, JsonNode?> skillComposedRoots,
        IReadOnlyDictionary<string, JsonObject> skillAfterImages)
    {
        var itemRows = finalization.Consumptions
            .Select((consumption, index) => (Consumption: consumption, Index: index))
            .Where(static row => string.Equals(
                row.Consumption.Kind,
                "item_quantity",
                StringComparison.Ordinal))
            .ToArray();
        var commands = new List<MortalItemConsumptionCommand>(itemRows.Length);
        for (var index = 0; index < itemRows.Length; index++)
        {
            var row = itemRows[index];
            if (!claims.TryGetValue(
                    row.Consumption.ClaimFingerprint,
                    out var claim) ||
                !ConsumptionAgrees(row.Consumption, claim) ||
                row.Consumption.Quantity > int.MaxValue)
            {
                return ItemContributionUnsupported(
                    "item finalization does not match one exact held claim");
            }
            commands.Add(CreateTreatmentItemConsumptionCommand(acceptedStateFingerprint, requestFingerprint,
                resultFingerprint, finalization.FinalizationFingerprint, row.Index, row.Consumption));
        }

        var identity = MortalItemIdentityState.Parse(
            baseline.IdentityIndexAfterImage.DeepClone());
        if (identity.Issues.Count != 0)
            return new TreatmentItemContributionBuild(null, identity.Issues);
        var capacitySourceFingerprint = WoundAcceptedTurnFingerprintWriter.Compute(
            new string?[]
            {
                "book_of_eternity.mortal_wound_treatment.item_capacity_source",
                "1",
                identitySeed,
                baseline.Fingerprint
            });
        var capacityPolicyFingerprint = WoundAcceptedTurnFingerprintWriter.Compute(
            new string?[]
            {
                "book_of_eternity.mortal_wound_treatment.item_capacity_policy",
                "1",
                identitySeed,
                baseline.Fingerprint
            });
        var consumptionInput = new MortalItemConsumptionPlanningInput(
            turn,
            baseline.Fingerprint,
            CreateConsumptionCarrierRoots(skillComposedRoots),
            identity,
            commands,
            definitions,
            state,
            new ResourceSourceEvidence(
                "mortal_wound_treatment",
                "treatment_item_capacity_" +
                capacitySourceFingerprint[
                    "sha256:".Length..("sha256:".Length + 20)],
                capacitySourceFingerprint),
            capacityPolicyFingerprint);
        var consumption = MortalItemConsumptionPlanner.Plan(consumptionInput);
        if (!consumption.IsValid || consumption.IdentityIndexAfterImage is null)
            return new TreatmentItemContributionBuild(null, consumption.Issues);

        var finalRoots = MortalItemAcceptedTurnNormalizationSnapshot.CloneRoots(
            skillComposedRoots);
        foreach (var pair in consumption.CarrierAfterImages)
        {
            finalRoots[pair.Key] =
                MortalItemProjectionRootParser.RestorePublicationRoot(
                    skillComposedRoots.GetValueOrDefault(pair.Key),
                    pair.Value,
                    pair.Key);
        }
        finalRoots[MortalItemIdentityState.StatePath] =
            consumption.IdentityIndexAfterImage.DeepClone();

        var publicationAfterImages = new Dictionary<string, JsonObject>(
            StringComparer.Ordinal);
        if (skillAfterImages.TryGetValue(
                NpcCoreChangesContract.NpcCorePath,
                out var composedNpc))
        {
            publicationAfterImages[NpcCoreChangesContract.NpcCorePath] =
                composedNpc.DeepClone().AsObject();
        }
        foreach (var pair in consumption.CarrierAfterImages)
        {
            var publicationRoot = finalRoots[pair.Key];
            if (!JsonNode.DeepEquals(
                    skillComposedRoots.GetValueOrDefault(pair.Key),
                    publicationRoot))
            {
                publicationAfterImages[pair.Key] =
                    pair.Value.DeepClone().AsObject();
            }
        }
        if (!JsonNode.DeepEquals(
                baseline.IdentityIndexAfterImage,
                consumption.IdentityIndexAfterImage))
        {
            publicationAfterImages[MortalItemIdentityState.StatePath] =
                consumption.IdentityIndexAfterImage.DeepClone().AsObject();
        }

        var ownerResult = MortalWoundTreatmentItemOwnerSuccessor.Compose(
            owners,
            consumption.TerminalOwners);
        if (!ownerResult.IsValid || ownerResult.Authority is null)
            return new TreatmentItemContributionBuild(null, ownerResult.Issues);

        return new(new(consumptionInput, consumption, finalRoots, publicationAfterImages, ownerResult.Authority),
            Array.Empty<ValidationIssue>());
    }

    private static TreatmentItemContributionBuild ItemContributionUnsupported(string actual) => new(null,
        new[] { PublicationIssue("mortal_wound_treatment_publication_item_consumption_unsupported",
            "one genuine complete private item publication capability", actual) });
}
