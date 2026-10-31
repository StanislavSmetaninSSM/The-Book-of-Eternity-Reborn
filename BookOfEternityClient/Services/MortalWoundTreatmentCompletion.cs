using System.Text.Json;
using System.Text.Json.Nodes;
using BookOfEternityClient.Core;

namespace BookOfEternityClient.Services;

/// <summary>
/// Shared fresh canonical settlement comparison for guided Mortal procedures.
/// Its copied DATA lasts one caller-owned read operation and grants no permission.
/// </summary>
internal static class MortalWoundTreatmentCompletion
{
    private static readonly string[] ItemCompanions =
    ["game_state/inventory/item_bonds.json", "game_state/inventory/item_text_updates.json",
     "game_state/inventory/recipes.json", "game_state/npcs/item_journals.json", "game_state/quests/quest_history.json"];

    /// <summary>
    /// Reads each closed canonical dependency once through the existing caller's
    /// authenticated read boundary; no new filesystem owner or cache is created.
    /// </summary>
    internal static async Task<(Dictionary<string, byte[]?> Bytes, Dictionary<string, JsonNode?> Nodes)> ReadCurrentAsync(
        Func<string, Task<byte[]?>> read)
    {
        var paths = WoundAcceptedTurnSnapshotContract.CarrierPaths
            .Concat(EffectAcceptedTurnInputComposer.SourceAuthorityPaths)
            .Concat(CanonicalResourceOwnerAuthorityComposer.SourceAuthorityPaths).Concat(ItemCompanions)
            .Concat(new[] { WoundHistoryState.HistoryPath, WoundIdentityState.StatePath, EffectCarrierCatalog.PlayerPath,
                EffectCarrierCatalog.NpcPath, EffectCarrierCatalog.SpiritualConflictPath, EffectAcceptedTurnPlan.IdentityIndexPath,
                MortalItemIdentityState.StatePath, ResourceMaterializationContract.DefinitionsPath,
                ResourceMaterializationContract.StatePath, ResourceMaterializationContract.HistoryPath,
                CanonicalResourceOwnerAuthorityComposer.AuthorityPath, EffectAcceptedTurnInputComposer.WorldTimePath,
                ProgressionScheduleService.SchedulePath, ProgressionScheduleService.ReportPath,
                AcceptedMechanicsPlan.WoundCommandPath, WoundAcceptedTurnSnapshotContract.PendingResolutionPath,
                ResourceMaterializationContract.CommandPath, EffectAcceptedTurnPlan.CommandPath })
            .Distinct(StringComparer.Ordinal).ToArray();
        var bytes = new Dictionary<string, byte[]?>(StringComparer.Ordinal);
        var nodes = new Dictionary<string, JsonNode?>(StringComparer.Ordinal);
        foreach (var path in paths)
        {
            var current = await read(path); bytes.Add(path, current);
            nodes.Add(path, current is null ? null : StrictJsonAuthority.Deserialize<JsonNode>(
                CanonicalJsonUtf8.DecodeOneOptionalBom(current), new JsonSerializerOptions(), path));
        }
        return (bytes, nodes);
    }

    /// <summary>
    /// Requires exact owned consumptions and complete current canonical agreement,
    /// allowing only valid ordinary successor chains, without restoring authority.
    /// </summary>
    internal static async Task RequireCanonicalSettlementAsync(Dictionary<string, byte[]?> bytes,
        Dictionary<string, JsonNode?> nodes, MortalWoundTreatmentAttemptRequest request, string terminalResultFingerprint,
        int turn, MortalWoundTreatmentResourceFinalization finalization, string semantic)
    {
        string? Text(string path) => bytes[path] is { } value ? CanonicalJsonUtf8.DecodeOneOptionalBom(value) : null;
        JsonObject? Root(string path) => nodes.GetValueOrDefault(path) as JsonObject;
        var identitySeed = MortalWoundTreatmentResourcePublicationAuthority.ComputeIdentitySeed(
            request.Coordinates.AcceptedStateFingerprint, request.RequestFingerprint, terminalResultFingerprint,
            finalization.FinalizationFingerprint, semantic);
        var definitions = ResourceDefinitionCatalog.ParseCanonical(Text(ResourceMaterializationContract.DefinitionsPath), false);
        if (!definitions.IsValid) throw Invalid("Current resource definitions are invalid.");
        var state = ResourceStateContract.ParseCanonical(Text(ResourceMaterializationContract.StatePath), definitions.Catalog!, false);
        var resources = ResourceHistoryState.ParseCanonical(Text(ResourceMaterializationContract.HistoryPath), definitions.Catalog!, false);
        if (!state.IsValid || !resources.IsValid || resources.History!.ValidateStateAgreement(state.Ledger!).Count != 0)
            throw Invalid("Current resource History/state agreement changed.");
        var owners = await CanonicalResourceOwnerAuthorityComposer.ComposeAsync(definitions.Catalog!,
            path => Task.FromResult(Text(path)), state.Ledger, resources.History,
            CanonicalResourceOwnerAuthorityPurpose.ExistingSessionValidation);
        if (!owners.IsValid || owners.Authority!.ValidateCanonicalAgreement(state.Ledger!, resources.History!).Count != 0)
            throw Invalid("Current resource owners do not agree with the live ledger.");
        RequireResourceConsumptions(request, terminalResultFingerprint, turn, finalization, semantic, identitySeed, definitions.Catalog!, resources.History!);

        var companions = ItemCompanions.Where(path => Root(path) is not null).ToDictionary(path => path, path => Root(path)!, StringComparer.Ordinal);
        var items = MortalItemCarrierCatalog.Build(new(Root(InventoryEquipmentService.ItemsPath), Root(NpcCoreChangesContract.NpcCorePath),
            null, Root(StorageTransportMoveService.CurrentLocationPath),
            MortalItemProjectionRootParser.ToCarrierCatalogObject(nodes.GetValueOrDefault(StorageTransportMoveService.VehiclesPath), StorageTransportMoveService.VehiclesPath),
            companions, Root(MortalLocationStorageContentsState.StatePath)));
        var itemIndex = MortalItemIdentityState.Parse(nodes[MortalItemIdentityState.StatePath]);
        if (ValidationService.ValidateMortalDiagnosisCurrentItems(items, itemIndex).Count != 0)
            throw Invalid("Current item carrier/index/companion agreement changed.");
        RequireItemConsumptions(request, terminalResultFingerprint, turn, finalization, itemIndex);

        var effectInput = new EffectCarrierCatalogInput(Root(EffectCarrierCatalog.PlayerPath), Root(EffectCarrierCatalog.NpcPath),
            Root(EffectCarrierCatalog.EnemiesPath), Root(EffectCarrierCatalog.AlliesPath), Root(EffectCarrierCatalog.AfterlifeProfilesPath), Root(EffectCarrierCatalog.SpiritualConflictPath));
        var effects = EffectCarrierCatalog.Build(effectInput);
        var effectIndex = EffectIdentityState.Parse(JsonSerializer.SerializeToElement(nodes[EffectAcceptedTurnPlan.IdentityIndexPath]), EffectAcceptedTurnPlan.IdentityIndexPath);
        var issues = new List<ValidationIssue>();
        if (effects.Issues.Count != 0 || effectIndex.State is null || effectIndex.Issues.Count != 0)
            throw Invalid("Current effect catalog/index is invalid.");
        ValidationService.ValidateEffectCarrierIndexAgreement(effects, effectIndex.State, issues);
        var sourceRoots = EffectAcceptedTurnInputComposer.SourceAuthorityPaths.ToDictionary(path => path,
            path => nodes.GetValueOrDefault(path), StringComparer.Ordinal);
        issues.AddRange(ValidationService.ValidateMortalDiagnosisEffectSources(sourceRoots));
        var sourceAuthority = EffectAcceptedTurnInputComposer.BuildCanonicalSourceAuthority(sourceRoots);
        var targetAuthority = EffectAcceptedTurnInputComposer.BuildCanonicalTargetAuthority(effectInput, sourceRoots);
        issues.AddRange(sourceAuthority.Issues); issues.AddRange(targetAuthority.Issues);
        issues.AddRange(targetAuthority.ValidateNamedCombatantBindings(effectInput));
        ValidationService.ValidateCanonicalEffectBindings(effects, sourceAuthority, targetAuthority, issues);
        if (issues.Count != 0) throw Invalid("Current effect/source/target/index agreement changed.");
    }

    private static MortalWoundTreatmentRequirementSuccessWitness QuantityWitness(MortalWoundTreatmentAttemptRequest request,
        MortalWoundTreatmentResourceConsumptionIntent consumption)
    {
        var claim = request.ResourceAuthority.Claims.Single(value => value.ClaimFingerprint == consumption.ClaimFingerprint);
        var witness = request.RequirementAuthority.Scopes.Single(value => value.Scope == consumption.Scope)
            .Bindings.Single(value => value.RequirementIndex == consumption.RequirementIndex).SuccessWitness;
        if (witness.Kind != consumption.Kind || witness.AuthorityRef != consumption.AuthorityRef || witness.Realm != consumption.Realm ||
            witness.OwnerKind != consumption.OwnerKind || witness.OwnerId != consumption.OwnerId || witness.WitnessFingerprint != claim.SuccessWitnessFingerprint)
            throw Invalid("Consumption lost its exact original quantity witness.");
        return witness;
    }

    private static void RequireResourceConsumptions(MortalWoundTreatmentAttemptRequest request, string terminalResultFingerprint, int turn,
        MortalWoundTreatmentResourceFinalization finalization, string semantic, string identitySeed,
        ResourceDefinitionCatalog definitions, ResourceHistoryState history)
    {
        var running = new Dictionary<ResourceCoordinate, decimal>(ResourceCoordinateComparer.Instance);
        var expectedIds = new List<string>();
        for (var ordinal = 0; ordinal < finalization.Consumptions.Count; ordinal++)
        {
            var consumption = finalization.Consumptions[ordinal]; if (consumption.Kind != "resource_quantity") continue;
            if (QuantityWitness(request, consumption).Evidence is not MortalWoundResourceQuantityRequirementEvidence evidence ||
                evidence.CurrentValue != evidence.AvailableValue || evidence.AvailableValue < consumption.Quantity ||
                !ResourceDefinitionCatalog.TryParseOwnerKind(consumption.OwnerKind, out var ownerKind) ||
                !definitions.TryResolveExact(consumption.AuthorityRef, out var definition))
                throw Invalid("Original resource witness cannot describe this exact publication.");
            var coordinate = new ResourceCoordinate(consumption.Realm, ownerKind, consumption.OwnerId, consumption.AuthorityRef);
            var before = running.GetValueOrDefault(coordinate, evidence.CurrentValue); var after = checked(before - consumption.Quantity);
            var eventRef = WoundAcceptedTurnPlanner.CreateEventRef(semantic, finalization.FinalizationFingerprint, ordinal);
            var mutation = new ResourceMutationIntent(eventRef, coordinate, consumption.Quantity,
                new("registered_system_outcome", eventRef, ResourceOperation.Spend), Array.Empty<ResourceOperationKey>(), Array.Empty<ResourceMutationEventRequirement>(), null);
            var ids = MortalWoundTreatmentResourcePublicationAuthority.CreateMutationIdentities(identitySeed, mutation);
            var sourceFingerprint = WoundAcceptedTurnPlanner.ComputeTreatmentResourceConsumptionSourceFingerprint(request.Coordinates.AcceptedStateFingerprint,
                request.RequestFingerprint, terminalResultFingerprint, finalization.FinalizationFingerprint,
                consumption.IntentFingerprint, eventRef, coordinate, before, after);
            var sources = ResourceMutationSourceCatalog.Create(new[] { new ResourceMutationSourceExport("registered_system_outcome", eventRef,
                sourceFingerprint, ResourceMutationSourceState.Active, true) });
            var route = sources.Catalog!.Resolve(mutation.Source, definition!, coordinate);
            var rows = history.Transitions.Where(value => value.TransitionId == ids.TransitionId || value.OperationId == ids.OperationId || value.EventRef == eventRef).ToArray();
            if (!route.IsValid || rows.Length != 1) throw Invalid("Missing or duplicate exact treatment resource consumption.");
            var row = rows[0];
            if (row.TransitionId != ids.TransitionId || row.OperationId != ids.OperationId || row.EventRef != eventRef ||
                row.Turn != turn || row.Coordinate != coordinate || row.OriginKind != "registered_system_outcome" || row.OriginId != eventRef ||
                row.Operation != ResourceTransitionOperation.Spend || row.Phase != route.Route!.Phase || row.Priority != route.Route.Priority ||
                row.RequestedAmount != consumption.Quantity || row.AppliedAmount != consumption.Quantity || row.Outcome != ResourceTransitionOutcome.Applied ||
                row.SourceEvidence != route.Route.SourceEvidence || row.PolicyFingerprint != route.Route.PolicyBinding.AuthorityFingerprint ||
                row.BeforeState is not { } initial || initial.Current != before || row.AfterState != (initial with { Current = after }) ||
                row.CapacityDisposition is not null || row.ReceiptId is not null)
                throw Invalid("Treatment resource consumption differs from its independently derived exact event.");
            running[coordinate] = after; expectedIds.Add(ids.TransitionId);
        }
        var prefix = "treatment_resource_";
        var actual = history.Transitions.Where(value => value.Turn == turn && value.OriginKind == "registered_system_outcome" &&
            value.EventRef.StartsWith(prefix, StringComparison.Ordinal)).Select(value => value.TransitionId).ToArray();
        if (!actual.SequenceEqual(expectedIds, StringComparer.Ordinal)) throw Invalid("Treatment resource consumption set/order changed.");
    }

    private static void RequireItemConsumptions(MortalWoundTreatmentAttemptRequest request, string terminalResultFingerprint, int turn,
        MortalWoundTreatmentResourceFinalization finalization, MortalItemIdentityParseResult identity)
    {
        var expectedIds = new HashSet<string>(StringComparer.Ordinal); var counts = new Dictionary<string, int>(StringComparer.Ordinal);
        for (var ordinal = 0; ordinal < finalization.Consumptions.Count; ordinal++)
        {
            var consumption = finalization.Consumptions[ordinal]; if (consumption.Kind != "item_quantity") continue;
            if (QuantityWitness(request, consumption).Evidence is not MortalWoundItemQuantityRequirementEvidence evidence || evidence.AvailableCount < consumption.Quantity)
                throw Invalid("Missing original item quantity witness.");
            var command = WoundAcceptedTurnPlanner.CreateTreatmentItemConsumptionCommand(request.Coordinates.AcceptedStateFingerprint,
                request.RequestFingerprint, terminalResultFingerprint, finalization.FinalizationFingerprint, ordinal, consumption);
            var matches = identity.EntriesByItemId.Values.SelectMany(entry => entry["transitions"]!.AsArray().OfType<JsonObject>())
                .Where(row => row["transitionId"]?.GetValue<string>() == command.TransitionId || row["authorityId"]?.GetValue<string>() == command.AuthorityId).ToArray();
            if (matches.Length != 1 || !identity.EntriesByItemId.TryGetValue(command.ItemId, out var item) ||
                !item["transitions"]!.AsArray().Contains(matches[0])) throw Invalid("Missing/foreign/duplicate exact item consumption.");
            var row = matches[0]; var before = counts.GetValueOrDefault(command.ItemId, evidence.Count); var after = checked(before - command.Quantity);
            if (row["transitionId"]?.GetValue<string>() != command.TransitionId || row["kind"]?.GetValue<string>() != "consume" ||
                row["authorityKind"]?.GetValue<string>() != command.AuthorityKind || row["authorityId"]?.GetValue<string>() != command.AuthorityId ||
                row["turn"]?.GetValue<int>() != turn || row["quantityBefore"]?.GetValue<int>() != before || row["quantityAfter"]?.GetValue<int>() != after)
                throw Invalid("Treatment item consumption differs from its exact terminal intent.");
            counts[command.ItemId] = after; expectedIds.Add(command.TransitionId);
        }
        var actualIds = identity.EntriesByItemId.Values.SelectMany(entry => entry["transitions"]!.AsArray().OfType<JsonObject>())
            .Where(row => row["turn"]?.GetValue<int>() == turn && row["authorityKind"]?.GetValue<string>() == "mortal_wound_treatment")
            .Select(row => row["transitionId"]!.GetValue<string>()).ToHashSet(StringComparer.Ordinal);
        if (!actualIds.SetEquals(expectedIds)) throw Invalid("Treatment item consumption set changed.");
    }

    private static InvalidDataException Invalid(string reason) => new("Published Mortal treatment is incomplete: " + reason);
}
