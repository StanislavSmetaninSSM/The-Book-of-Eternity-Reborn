using System.Text.Json;
using System.Text.Json.Nodes;
using BookOfEternityClient.Core;
using BookOfEternityClient.Models;

namespace BookOfEternityClient.Services;

/// <summary>
/// Proves current complete settlement from an independently authenticated browser
/// original and fresh canonical DATA. It never restores an owner or a reservation.
/// </summary>
internal static class MortalWoundDiagnosisCompletion
{
    private static readonly string[] ItemCompanions =
    ["game_state/inventory/item_bonds.json", "game_state/inventory/item_text_updates.json",
     "game_state/inventory/recipes.json", "game_state/npcs/item_journals.json", "game_state/quests/quest_history.json"];

    /// <summary>
    /// Reads each current dependency once under the caller's existing read/lease
    /// boundary and requires exact original, durable result and current settlement.
    /// </summary>
    internal static async Task<MortalWoundDiagnosisPersistedResult> RequirePublishedAsync(TurnRequest original,
        string snapshotToken, string generation, SpiritualSelfTreatmentSelection selection, JsonObject input,
        Func<string, Task<byte[]?>> read)
    {
        var control = original.ProgressionControl ?? throw Invalid("Missing original control.");
        var context = new SpiritualSelfTreatmentStageContext(original.SessionId, original.RequestId, original.TurnNumber, control);
        var selected = MortalWoundDiagnosisInputCodec.Read(input);
        if (selection.MortalDiagnosis is null || selected.Selection != selection || !MortalWoundDiagnosisInputCodec.IsBound(input, context))
            throw Invalid("The independent original selection/control changed.");
        var paths = WoundAcceptedTurnSnapshotContract.CarrierPaths
            .Concat(EffectAcceptedTurnInputComposer.SourceAuthorityPaths)
            .Concat(CanonicalResourceOwnerAuthorityComposer.SourceAuthorityPaths).Concat(ItemCompanions)
            .Concat(new[] { WoundHistoryState.HistoryPath, WoundIdentityState.StatePath, EffectCarrierCatalog.PlayerPath,
                EffectCarrierCatalog.NpcPath, EffectCarrierCatalog.SpiritualConflictPath, EffectAcceptedTurnPlan.IdentityIndexPath,
                MortalItemIdentityState.StatePath, ResourceMaterializationContract.DefinitionsPath,
                ResourceMaterializationContract.StatePath, ResourceMaterializationContract.HistoryPath,
                CanonicalResourceOwnerAuthorityComposer.AuthorityPath, EffectAcceptedTurnInputComposer.WorldTimePath,
                ProgressionScheduleService.SchedulePath, ProgressionScheduleService.ReportPath,
                AcceptedMechanicsPlan.WoundCommandPath, WoundAcceptedTurnSnapshotContract.PendingResolutionPath })
            .Distinct(StringComparer.Ordinal).ToArray();
        var bytes = new Dictionary<string, byte[]?>(StringComparer.Ordinal);
        var nodes = new Dictionary<string, JsonNode?>(StringComparer.Ordinal);
        foreach (var path in paths)
        {
            var current = await read(path); bytes.Add(path, current);
            nodes.Add(path, current is null ? null : StrictJsonAuthority.Deserialize<JsonNode>(
                CanonicalJsonUtf8.DecodeOneOptionalBom(current), new JsonSerializerOptions(), path));
        }
        string? Text(string path) => bytes[path] is { } value ? CanonicalJsonUtf8.DecodeOneOptionalBom(value) : null;
        JsonObject? Root(string path) => nodes.GetValueOrDefault(path) as JsonObject;
        var parsed = WoundHistoryState.Parse(Text(WoundHistoryState.HistoryPath), WoundHistoryState.HistoryPath);
        if (!parsed.IsValid) throw Invalid("Current wound History is invalid.");
        var history = parsed.State!;
        var matching = history.Transitions.Select(row => row.TransitionResult).OfType<MortalWoundDiagnosisPersistedResult>()
            .Where(value => value.Binding.SessionId == original.SessionId && value.Binding.RequestId == original.RequestId &&
                value.Binding.Turn == original.TurnNumber).ToArray();
        if (matching.Length != 1) throw Invalid("Expected exactly one complete diagnosis result for this original.");
        var result = matching[0]; var resultRoot = result.ToCanonicalJson();
        if (result.Binding.SnapshotToken != snapshotToken || result.Source.SessionGeneration != generation ||
            !JsonNode.DeepEquals(resultRoot["input"], input) || original.PreGeneratedDices1d20 is null ||
            !result.Source.AcceptedD20EventValues.SequenceEqual(original.PreGeneratedDices1d20))
            throw Invalid("The current result belongs to another original, token, generation or dice pool.");
        MortalWoundDiagnosisInputCodec.RequireRequestMatches(input, result.Request);
        var semantic = result.ReconstructPublicationSemantic(); // v1 is historical, never an implicit empty publication.
        var finalized = MortalWoundTreatmentResourceComposer.Finalize(result.Resolution);
        if (!finalized.IsValid || finalized.Finalization is null) throw Invalid("Invalid retained finalization.");
        var finalization = finalized.Finalization;
        var identitySeed = MortalWoundTreatmentResourcePublicationAuthority.ComputeIdentitySeed(result.Source.AcceptedStateFingerprint,
            result.Request.RequestFingerprint, result.Resolution.ResultFingerprint, finalization.FinalizationFingerprint, semantic);

        var carriers = WoundCarrierCatalog.Build(new(Root(WoundCarrierCatalog.PlayerPath), Root(WoundCarrierCatalog.NpcPath),
            Root(WoundCarrierCatalog.EnemiesPath), Root(WoundCarrierCatalog.AlliesPath), Root(WoundCarrierCatalog.AfterlifeProfilesPath)));
        var identities = WoundIdentityState.Parse(Text(WoundIdentityState.StatePath), WoundIdentityState.StatePath);
        if (carriers.Issues.Count != 0 || !identities.IsValid || history.ValidateAgreement(identities.State!, carriers).Count != 0)
            throw Invalid("Current wound carrier/identity/History agreement changed.");
        var cohort = result.Recoveries.Select(member => member.Original.WoundId).ToHashSet(StringComparer.Ordinal);
        foreach (var identity in identities.State!.Entries.Where(value => value.Realm == "mortal_world" && value.Domain == "physical" && value.Status == "active"))
            if (!cohort.Contains(identity.WoundId)) throw Invalid("Current diagnosis cohort acquired an unrelated active member.");
        foreach (var member in result.Recoveries)
        {
            var final = member.Stages.Length == 0 ? member.Working : member.Stages[^1].AfterWound;
            var occurrences = carriers.Occurrences.Where(value => value.WoundId == final.WoundId).ToArray();
            if (history.Transitions.Last(value => value.WoundId == final.WoundId).TransitionId != final.LastTransition.TransitionId ||
                (final.Lifecycle == "healed" ? occurrences.Length != 0 : occurrences.Length != 1 ||
                 WoundMaterializationContract.SerializeCanonical(occurrences[0].Wound) != WoundMaterializationContract.SerializeCanonical(final)))
                throw Invalid("The complete current cohort no longer equals the published diagnosis result.");
        }
        var clock = resultRoot["clock"]!.AsObject();
        if (!JsonNode.DeepEquals(nodes[EffectAcceptedTurnInputComposer.WorldTimePath], clock["after"]) ||
            !Equal(bytes[ProgressionScheduleService.SchedulePath], Decode(clock["scheduleAfterBase64"])) ||
            !Equal(bytes[ProgressionScheduleService.ReportPath], Decode(clock["reportAfterBase64"])) ||
            bytes[AcceptedMechanicsPlan.WoundCommandPath] is not null || bytes[WoundAcceptedTurnSnapshotContract.PendingResolutionPath] is not null)
            throw Invalid("Clock/schedule/report changed or consumed diagnosis work reappeared.");

        var definitions = ResourceDefinitionCatalog.ParseCanonical(Text(ResourceMaterializationContract.DefinitionsPath), false);
        if (!definitions.IsValid) throw Invalid("Current resource definitions are invalid.");
        var state = ResourceStateContract.ParseCanonical(Text(ResourceMaterializationContract.StatePath), definitions.Catalog!, false);
        var resources = ResourceHistoryState.ParseCanonical(Text(ResourceMaterializationContract.HistoryPath), definitions.Catalog!, false);
        if (!state.IsValid || !resources.IsValid || resources.History!.ValidateStateAgreement(state.Ledger!).Count != 0)
            throw Invalid("Current resource History/state agreement changed.");
        var owners = await CanonicalResourceOwnerAuthorityComposer.ComposeAsync(definitions.Catalog!,
            path => Task.FromResult(Text(path)), state.Ledger, resources.History,
            CanonicalResourceOwnerAuthorityPurpose.ExistingSessionValidation);
        if (!owners.IsValid) throw Invalid("Current resource owners are invalid.");
        RequireResourceConsumptions(result, finalization, semantic, identitySeed, definitions.Catalog!, resources.History!);

        var companions = ItemCompanions.Where(path => Root(path) is not null).ToDictionary(path => path, path => Root(path)!, StringComparer.Ordinal);
        var items = MortalItemCarrierCatalog.Build(new(Root(InventoryEquipmentService.ItemsPath), Root(NpcCoreChangesContract.NpcCorePath),
            Root("game_state/npcs/npc_inventory.json"), Root(StorageTransportMoveService.CurrentLocationPath),
            MortalItemProjectionRootParser.ToCarrierCatalogObject(nodes.GetValueOrDefault(StorageTransportMoveService.VehiclesPath), StorageTransportMoveService.VehiclesPath),
            companions, Root(MortalLocationStorageContentsState.StatePath)));
        var itemIndex = MortalItemIdentityState.Parse(nodes[MortalItemIdentityState.StatePath]);
        if (ValidationService.ValidateMortalDiagnosisCurrentItems(items, itemIndex).Count != 0)
            throw Invalid("Current item carrier/index/companion agreement changed.");
        RequireItemConsumptions(result, finalization, itemIndex);

        var effectInput = new EffectCarrierCatalogInput(Root(EffectCarrierCatalog.PlayerPath), Root(EffectCarrierCatalog.NpcPath),
            Root(EffectCarrierCatalog.EnemiesPath), Root(EffectCarrierCatalog.AlliesPath), Root(EffectCarrierCatalog.AfterlifeProfilesPath), Root(EffectCarrierCatalog.SpiritualConflictPath));
        var effects = EffectCarrierCatalog.Build(effectInput);
        var effectIndex = EffectIdentityState.Parse(JsonSerializer.SerializeToElement(nodes[EffectAcceptedTurnPlan.IdentityIndexPath]), EffectAcceptedTurnPlan.IdentityIndexPath);
        var issues = new List<ValidationIssue>();
        if (effects.Issues.Count != 0 || effectIndex.State is null || effectIndex.Issues.Count != 0)
            throw Invalid("Current effect catalog/index is invalid.");
        ValidationService.ValidateEffectCarrierIndexAgreement(effects, effectIndex.State, issues);
        var sourceAuthority = EffectAcceptedTurnInputComposer.BuildCanonicalSourceAuthority(nodes);
        var targetAuthority = EffectAcceptedTurnInputComposer.BuildCanonicalTargetAuthority(effectInput, nodes);
        issues.AddRange(sourceAuthority.Issues); issues.AddRange(targetAuthority.Issues);
        issues.AddRange(targetAuthority.ValidateNamedCombatantBindings(effectInput));
        ValidationService.ValidateCanonicalEffectBindings(effects, sourceAuthority, targetAuthority, issues);
        if (issues.Count != 0) throw Invalid("Current effect/source/target/index agreement changed.");
        return result;
    }

    private static MortalWoundTreatmentRequirementSuccessWitness QuantityWitness(MortalWoundDiagnosisPersistedResult result,
        MortalWoundTreatmentResourceConsumptionIntent consumption)
    {
        var claim = result.Request.ResourceAuthority.Claims.Single(value => value.ClaimFingerprint == consumption.ClaimFingerprint);
        var witness = result.Request.RequirementAuthority.Scopes.Single(value => value.Scope == consumption.Scope)
            .Bindings.Single(value => value.RequirementIndex == consumption.RequirementIndex).SuccessWitness;
        if (witness.Kind != consumption.Kind || witness.AuthorityRef != consumption.AuthorityRef || witness.Realm != consumption.Realm ||
            witness.OwnerKind != consumption.OwnerKind || witness.OwnerId != consumption.OwnerId || witness.WitnessFingerprint != claim.SuccessWitnessFingerprint)
            throw Invalid("Consumption lost its exact original quantity witness.");
        return witness;
    }

    private static void RequireResourceConsumptions(MortalWoundDiagnosisPersistedResult result,
        MortalWoundTreatmentResourceFinalization finalization, string semantic, string identitySeed,
        ResourceDefinitionCatalog definitions, ResourceHistoryState history)
    {
        var running = new Dictionary<ResourceCoordinate, decimal>(ResourceCoordinateComparer.Instance);
        var expectedIds = new List<string>();
        for (var ordinal = 0; ordinal < finalization.Consumptions.Count; ordinal++)
        {
            var consumption = finalization.Consumptions[ordinal]; if (consumption.Kind != "resource_quantity") continue;
            if (QuantityWitness(result, consumption).Evidence is not MortalWoundResourceQuantityRequirementEvidence evidence ||
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
            var sourceFingerprint = WoundAcceptedTurnPlanner.ComputeTreatmentResourceConsumptionSourceFingerprint(result.Source.AcceptedStateFingerprint,
                result.Request.RequestFingerprint, result.Resolution.ResultFingerprint, finalization.FinalizationFingerprint,
                consumption.IntentFingerprint, eventRef, coordinate, before, after);
            var sources = ResourceMutationSourceCatalog.Create(new[] { new ResourceMutationSourceExport("registered_system_outcome", eventRef,
                sourceFingerprint, ResourceMutationSourceState.Active, true) });
            var route = sources.Catalog!.Resolve(mutation.Source, definition!, coordinate);
            var rows = history.Transitions.Where(value => value.TransitionId == ids.TransitionId || value.OperationId == ids.OperationId || value.EventRef == eventRef).ToArray();
            if (!route.IsValid || rows.Length != 1) throw Invalid("Missing or duplicate exact diagnosis resource consumption.");
            var row = rows[0];
            if (row.TransitionId != ids.TransitionId || row.OperationId != ids.OperationId || row.EventRef != eventRef ||
                row.Turn != result.Binding.Turn || row.Coordinate != coordinate || row.OriginKind != "registered_system_outcome" || row.OriginId != eventRef ||
                row.Operation != ResourceTransitionOperation.Spend || row.Phase != route.Route!.Phase || row.Priority != route.Route.Priority ||
                row.RequestedAmount != consumption.Quantity || row.AppliedAmount != consumption.Quantity || row.Outcome != ResourceTransitionOutcome.Applied ||
                row.SourceEvidence != route.Route.SourceEvidence || row.PolicyFingerprint != route.Route.PolicyBinding.AuthorityFingerprint ||
                row.BeforeState is not { } initial || initial.Current != before || row.AfterState != (initial with { Current = after }) ||
                row.CapacityDisposition is not null || row.ReceiptId is not null)
                throw Invalid("Diagnosis resource consumption differs from its independently derived exact event.");
            running[coordinate] = after; expectedIds.Add(ids.TransitionId);
        }
        var prefix = "treatment_resource_";
        var actual = history.Transitions.Where(value => value.Turn == result.Binding.Turn && value.OriginKind == "registered_system_outcome" &&
            value.EventRef.StartsWith(prefix, StringComparison.Ordinal)).Select(value => value.TransitionId).ToArray();
        if (!actual.SequenceEqual(expectedIds, StringComparer.Ordinal)) throw Invalid("Diagnosis resource consumption set/order changed.");
    }

    private static void RequireItemConsumptions(MortalWoundDiagnosisPersistedResult result,
        MortalWoundTreatmentResourceFinalization finalization, MortalItemIdentityParseResult identity)
    {
        var expectedIds = new HashSet<string>(StringComparer.Ordinal); var counts = new Dictionary<string, int>(StringComparer.Ordinal);
        for (var ordinal = 0; ordinal < finalization.Consumptions.Count; ordinal++)
        {
            var consumption = finalization.Consumptions[ordinal]; if (consumption.Kind != "item_quantity") continue;
            if (QuantityWitness(result, consumption).Evidence is not MortalWoundItemQuantityRequirementEvidence evidence || evidence.AvailableCount < consumption.Quantity)
                throw Invalid("Missing original item quantity witness.");
            var command = WoundAcceptedTurnPlanner.CreateTreatmentItemConsumptionCommand(result.Source.AcceptedStateFingerprint,
                result.Request.RequestFingerprint, result.Resolution.ResultFingerprint, finalization.FinalizationFingerprint, ordinal, consumption);
            var matches = identity.EntriesByItemId.Values.SelectMany(entry => entry["transitions"]!.AsArray().OfType<JsonObject>())
                .Where(row => row["transitionId"]?.GetValue<string>() == command.TransitionId || row["authorityId"]?.GetValue<string>() == command.AuthorityId).ToArray();
            if (matches.Length != 1 || !identity.EntriesByItemId.TryGetValue(command.ItemId, out var item) ||
                !item["transitions"]!.AsArray().Contains(matches[0])) throw Invalid("Missing/foreign/duplicate exact item consumption.");
            var row = matches[0]; var before = counts.GetValueOrDefault(command.ItemId, evidence.Count); var after = checked(before - command.Quantity);
            if (row["transitionId"]?.GetValue<string>() != command.TransitionId || row["kind"]?.GetValue<string>() != "consume" ||
                row["authorityKind"]?.GetValue<string>() != command.AuthorityKind || row["authorityId"]?.GetValue<string>() != command.AuthorityId ||
                row["turn"]?.GetValue<int>() != result.Binding.Turn || row["quantityBefore"]?.GetValue<int>() != before || row["quantityAfter"]?.GetValue<int>() != after)
                throw Invalid("Diagnosis item consumption differs from its exact terminal intent.");
            counts[command.ItemId] = after; expectedIds.Add(command.TransitionId);
        }
        var actualIds = identity.EntriesByItemId.Values.SelectMany(entry => entry["transitions"]!.AsArray().OfType<JsonObject>())
            .Where(row => row["turn"]?.GetValue<int>() == result.Binding.Turn && row["authorityKind"]?.GetValue<string>() == "mortal_wound_treatment")
            .Select(row => row["transitionId"]!.GetValue<string>()).ToHashSet(StringComparer.Ordinal);
        if (!actualIds.SetEquals(expectedIds)) throw Invalid("Diagnosis item consumption set changed.");
    }

    private static byte[]? Decode(JsonNode? node) => node is null ? null : Convert.FromBase64String(node.GetValue<string>());
    private static bool Equal(byte[]? left, byte[]? right) => left is null ? right is null : right is not null && left.AsSpan().SequenceEqual(right);
    private static InvalidDataException Invalid(string reason) => new("Published Mortal diagnosis is incomplete: " + reason);
}
