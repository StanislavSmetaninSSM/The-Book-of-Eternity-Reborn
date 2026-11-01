using System.Collections.Immutable;
using System.Globalization;
using System.Text.Json.Nodes;

namespace BookOfEternityClient.Services;

/// <summary>
/// Immutable preparation DATA for the legacies of one retained terminal treatment.
/// It neither allocates an effect ID nor grants execution/publication authority.
/// </summary>
internal sealed class MortalWoundHealLegacyPreparation
{
    private readonly WoundEffectOperationBatch[] _batches;
    private readonly WoundLegacyDeclaration[] _declarations;

    internal MortalWoundHealLegacyPreparation(string requestFingerprint, string intentFingerprint,
        IReadOnlyList<MortalWoundTreatmentLegacySeedBinding> bindings,
        IReadOnlyList<WoundLegacyDeclaration> declarations, IReadOnlyList<WoundEffectOperationBatch> batches)
    {
        RequestFingerprint = requestFingerprint;
        HealIntentFingerprint = intentFingerprint;
        LegacyDraftBindings = bindings.ToImmutableArray();
        _declarations = declarations.Select(row => row with { }).ToArray();
        _batches = batches.Select(WoundAcceptedTurnData.CloneOperationBatch).ToArray();
        PreparationFingerprint = WoundAcceptedTurnFingerprintWriter.Compute(new[]
        {
            "book_of_eternity.mortal_wound_treatment.legacy_preparation", "1",
            requestFingerprint, intentFingerprint
        }.Concat(bindings.Select(row => row.SeedFingerprint)).Concat(_batches.Select(row => row.SourceExportFingerprint)));
    }

    internal string RequestFingerprint { get; }
    internal string HealIntentFingerprint { get; }
    internal IReadOnlyList<MortalWoundTreatmentLegacySeedBinding> LegacyDraftBindings { get; }
    internal string PreparationFingerprint { get; }
    internal IReadOnlyList<WoundEffectOperationBatch> EffectOperationBatches =>
        _batches.Select(WoundAcceptedTurnData.CloneOperationBatch).ToArray();
    internal IReadOnlyList<WoundLegacyDeclaration> Declarations => _declarations.Select(row => row with { }).ToArray();
}

/// <summary>
/// Verified detached output of the existing effect allocator for one independent
/// mechanical legacy. The actual effect IDs are preserved, never derived here.
/// </summary>
internal sealed record MortalWoundHealLegacyApplicationGroup(int LegacyOrdinal, string LegacyId,
    string SourceExportFingerprint, ImmutableArray<EffectAcceptedApplicationResult> Results, string ResultGroupFingerprint);

/// <summary>
/// Converts existing immutable heal intents into independent legacy batches and
/// accepts only their complete ordered results from the actual sealed effect stage.
/// Live ownership remains with the original treatment continuation and publisher.
/// </summary>
internal static class MortalWoundHealLegacyPlanner
{
    /// <summary>
    /// Prepares one batch per mechanical legacy and no batch for cosmetic rows.
    /// All identities/reference namespaces come from the retained typed heal intent;
    /// no route JSON is reparsed and no effect ID is allocated.
    /// </summary>
    internal static MortalWoundHealLegacyPreparation Prepare(MortalWoundTreatmentAttemptRequest request,
        MortalWoundTreatmentResolution resolution, WoundAcceptedTurnBinding binding, string preparedInputFingerprint,
        long currentGameMinute, IReadOnlyList<WoundEffectOperationBatch> precedingBatches)
    {
        if (!MortalWoundTreatmentDetachedSealValidator.IsValid(request) ||
            resolution.RequestFingerprint != request.RequestFingerprint ||
            !MortalWoundTreatmentOutcomePublicationPlanner.DetachedSelectionAgrees(resolution, currentGameMinute) ||
            !WoundResponseInputComposer.MatchesAcceptedBinding(binding, request.Coordinates) ||
            resolution.OutcomeIntents.LastOrDefault() is not MortalWoundHealOutcomeIntent heal ||
            resolution.DeclaredResult.LastOrDefault() is not MortalWoundHealOperation declared ||
            resolution.OutcomeIntents.Count(intent => intent is MortalWoundHealOutcomeIntent) != 1 ||
            declared.Legacies.Length > MortalWoundTreatmentContract.MaxHealLegacies ||
            heal.LegacySeedBindings.Count != declared.Legacies.Length ||
            !ResourceMaterializationContract.IsAuthorityFingerprint(preparedInputFingerprint))
            throw Invalid("Missing or foreign retained terminal heal preparation.");
        var owner = request.RouteSourceWound.Owner;
        if (!WoundEffectCarrierAdapter.TryCreateTargetKey(owner, out var target))
            throw Invalid("Terminal legacy owner has no canonical effect target.");
        var accepted = binding.AcceptedEvents.Single(row => row.EventRef == request.Coordinates.EventRef);
        var aliases = new HashSet<string>(StringComparer.Ordinal);
        var firstMechanicsOrdinal = checked(precedingBatches.SelectMany(batch => batch.RootApplications)
            .Select(row => row.MechanicsOrdinal).DefaultIfEmpty(0).Max() + 1);
        var batches = new List<WoundEffectOperationBatch>();
        var declarations = MortalWoundTreatmentOutcomePublicationPlanner.CreateTerminalDeclarations(heal, declared, request.Coordinates.WoundId);
        for (var ordinal = 0; ordinal < declared.Legacies.Length; ordinal++)
        {
            var legacy = declared.Legacies[ordinal];
            var seed = heal.LegacySeedBindings[ordinal];
            if (seed.LegacyOrdinal != ordinal || seed.LocalLegacyRef != legacy.LocalLegacyRef || seed.Kind != legacy.Kind ||
                !aliases.Add(MortalLocationIdentityState.BuildConfusableKey(legacy.LocalLegacyRef)))
                throw Invalid("Legacy order or exact/confusable local namespace changed.");
            if (legacy is MortalWoundCosmeticLegacyDraft) continue;
            if (legacy is not MortalWoundMechanicalEffectLegacyDraft mechanical ||
                mechanical.EffectDraft.Definitions.Length is < 1 or > 5 ||
                mechanical.EffectDraft.Applications.Length is < 1 or > 5 ||
                seed.DefinitionReferenceBindings.Count != mechanical.EffectDraft.Definitions.Length ||
                seed.ApplicationReferenceBindings.Count != mechanical.EffectDraft.Applications.Length)
                throw Invalid("Mechanical legacy requires its own complete bounded definition/application batch.");
            var definitions = new List<WoundEffectSourceDefinition>();
            var definitionsByRef = new Dictionary<string, WoundEffectSourceDefinition>(StringComparer.Ordinal);
            for (var index = 0; index < mechanical.EffectDraft.Definitions.Length; index++)
            {
                var draft = mechanical.EffectDraft.Definitions[index];
                var reference = seed.DefinitionReferenceBindings[index];
                if (reference.LocalRef != draft.DefinitionRef || reference.NamespacedRef != legacy.LocalLegacyRef + "/" + draft.DefinitionRef)
                    throw Invalid("Mechanical legacy definition namespace changed.");
                var body = JsonNode.Parse(draft.Definition.GetRawText())!.AsObject();
                var definition = new WoundEffectSourceDefinition(body["definitionKey"]!.GetValue<string>(), body);
                if (!definitionsByRef.TryAdd(draft.DefinitionRef, definition)) throw Invalid("Duplicate legacy definition reference.");
                definitions.Add(definition);
            }
            var roots = new List<WoundRootEffectApplication>();
            for (var index = 0; index < mechanical.EffectDraft.Applications.Length; index++)
            {
                var application = mechanical.EffectDraft.Applications[index];
                var reference = seed.ApplicationReferenceBindings[index];
                if (reference.LocalRef != application.ApplicationRef ||
                    reference.NamespacedRef != legacy.LocalLegacyRef + "/" + application.ApplicationRef ||
                    !definitionsByRef.TryGetValue(application.DefinitionRef, out var definition))
                    throw Invalid("Mechanical legacy application namespace or definition changed.");
                var body = definition.Definition;
                var parameters = JsonNode.Parse(application.Parameters.GetRawText())!.AsObject();
                if (body["components"] is not JsonArray components ||
                    !WoundEffectCarrierAdapter.TryCreateCarrierCoordinate(owner, target, body, out var coordinate))
                    throw Invalid("Mechanical legacy has no canonical component/carrier mapping.");
                var key = new EffectSourceKey(owner.Realm, "wound_legacy", seed.LegacyId, definition.DefinitionKey);
                roots.Add(new(reference.NamespacedRef, checked(firstMechanicsOrdinal + ordinal), index + 1,
                    "apply", seed.LegacyId + "_application_" + index.ToString(CultureInfo.InvariantCulture), definition.DefinitionKey,
                    new(target.Kind, target.TargetId, null), target,
                    new(key.Realm, key.Kind, null, legacy.LocalLegacyRef, key.DefinitionKey), key, parameters,
                    Array.Empty<WoundEffectSlotAgreement>(), components.Count,
                    WoundEffectMaterializationFingerprint.Compute(key, body["schemaVersion"]!.GetValue<int>(), parameters, components),
                    WoundRootOwnershipDomain.BaseWound, request.Coordinates.EventRef, coordinate));
            }
            var export = new WoundEffectSourceExport(1, "wound_legacy", seed.LegacyId, legacy.LocalLegacyRef,
                "active", false, owner.Realm, owner, request.Coordinates.EventRef, accepted.SemanticFingerprint,
                heal.HealChildCoordinates.OperationKey, heal.IntentFingerprint, definitions);
            var beforeFingerprint = request.Coordinates.ExpectedBeforeFingerprint;
            var seal = WoundAcceptedTurnFingerprints.ComputeTransitionAuthority(preparedInputFingerprint,
                legacy.LocalLegacyRef, seed.LegacyId, heal.HealChildCoordinates.OperationKey, heal.IntentFingerprint,
                heal.HealChildCoordinates.OperationKey, legacy.ReadableSummary, 1, "legacy", null, beforeFingerprint);
            var transition = new WoundPreparedTransitionAuthority(preparedInputFingerprint, heal.HealChildCoordinates.OperationKey,
                heal.IntentFingerprint, heal.HealChildCoordinates.OperationKey, legacy.ReadableSummary, 1, "legacy", null,
                beforeFingerprint, seal);
            var provisional = new WoundEffectOperationBatch(legacy.LocalLegacyRef, seed.LegacyId, export, roots,
                Array.Empty<WoundTerminalEffectOperation>(), roots.Select(row =>
                    new WoundRootLineageAuthorityRow(row.ApplicationRef, null, row.DefinitionKey, row.OwnershipDomain)).ToArray(),
                string.Empty, transition);
            batches.Add(new(legacy.LocalLegacyRef, seed.LegacyId, export, roots, provisional.TerminalOperations,
                provisional.RootLineageAuthority, WoundAcceptedTurnFingerprints.ComputeSourceExport(provisional), transition));
        }
        RequireDisjointCoordinates(precedingBatches.Concat(batches).ToArray(), heal);
        return new(request.RequestFingerprint, heal.IntentFingerprint, heal.LegacySeedBindings, declarations, batches);
    }

    /// <summary>
    /// Rejects exact or confusable operation/application/event collisions across
    /// every supplied prefix or joint batch and the terminal child before publication.
    /// Legacy mechanics ordinals follow the actual preceding batch inventory.
    /// </summary>
    internal static void RequireDisjointCoordinates(IReadOnlyList<WoundEffectOperationBatch> batches,
        MortalWoundHealOutcomeIntent? heal)
    {
        var applications = new HashSet<string>(StringComparer.Ordinal);
        var operations = new HashSet<string>(StringComparer.Ordinal);
        var events = new HashSet<string>(StringComparer.Ordinal);
        static void Add(HashSet<string> set, string value)
        {
            if (!ResourceMaterializationContract.IsExactIdentifier(value) ||
                !set.Add(MortalLocationIdentityState.BuildConfusableKey(value)))
                throw Invalid("Colliding terminal/prefix/legacy operation coordinates.");
        }
        if (heal is not null)
        {
            Add(operations, heal.HealChildCoordinates.OperationKey);
            Add(events, heal.HealChildCoordinates.EventRef);
            Add(events, heal.HealChildCoordinates.CausalEventRef);
        }
        foreach (var batch in batches)
        {
            foreach (var root in batch.RootApplications)
            {
                Add(applications, root.ApplicationRef); Add(operations, root.OperationKey);
                Add(events, WoundEffectOperationEventRef.Create(root.CausalEventRef,
                    root.MechanicsOrdinal, root.OperationOrdinal, root.OperationKind));
            }
            foreach (var terminal in batch.TerminalOperations)
            {
                Add(operations, terminal.OperationKey);
                Add(events, WoundEffectOperationEventRef.Create(terminal.CausalEventRef,
                    terminal.MechanicsOrdinal, terminal.OperationOrdinal, terminal.OperationKind));
            }
        }
    }

    /// <summary>
    /// Requires exact batch topology before allocation; missing, extra, reordered,
    /// merged or split batches cannot be treated as the selected legacy program.
    /// </summary>
    internal static void RequireExactBatches(MortalWoundHealLegacyPreparation preparation,
        IReadOnlyList<WoundEffectOperationBatch> supplied)
    {
        var expected = preparation.EffectOperationBatches;
        if (supplied.Count != expected.Count) throw Invalid("Legacy batch count changed.");
        for (var index = 0; index < expected.Count; index++)
            if (supplied[index].LocalWoundRef != expected[index].LocalWoundRef ||
                supplied[index].PreparedWoundId != expected[index].PreparedWoundId ||
                supplied[index].SourceExportFingerprint != expected[index].SourceExportFingerprint ||
                WoundAcceptedTurnFingerprints.ComputeSourceExport(supplied[index]) != expected[index].SourceExportFingerprint)
                throw Invalid("Legacy batch order, source or complete topology changed.");
    }

    /// <summary>
    /// Verifies the real accepted effect-plan seal and exact ordered allocated
    /// legacy result map before any durable legacy declaration is published.
    /// </summary>
    internal static IReadOnlyList<MortalWoundHealLegacyApplicationGroup> Finalize(
        MortalWoundHealLegacyPreparation preparation, WoundPreparedAcceptedTurnPlan prepared,
        WoundEffectBatchAcceptedPlan accepted)
    {
        var plan = accepted.EffectPlan;
        if (accepted.WoundPreparationFingerprint != prepared.WoundPreparationFingerprint ||
            accepted.EffectInputFingerprint != plan.InputFingerprint ||
            accepted.EffectAcceptedTurnPlanFingerprint != WoundAcceptedTurnFingerprints.ComputeAcceptedEffectPlanPayload(plan))
            throw Invalid("Legacy finalization requires the actual matching sealed effect plan.");
        var expected = preparation.EffectOperationBatches;
        var actual = accepted.ApplicationResults.Where(row => row.SourceKey.Kind == "wound_legacy").ToArray();
        if (actual.Length != expected.Sum(batch => batch.RootApplications.Count))
            throw Invalid("Missing or extra allocated legacy application result.");
        var mapping = plan.WoundApplicationRootEffectBindings;
        var groups = new List<MortalWoundHealLegacyApplicationGroup>(); var cursor = 0;
        foreach (var batch in expected)
        {
            var results = ImmutableArray.CreateBuilder<EffectAcceptedApplicationResult>();
            foreach (var root in batch.RootApplications)
            {
                var row = actual[cursor++];
                if (row.ApplicationRef != root.ApplicationRef || row.SourceKey != root.ExpectedSourceKey ||
                    row.TargetKey != root.ExpectedTargetKey || row.CarrierCoordinate != root.ExpectedCarrierCoordinate ||
                    row.Disposition != "created_new_identity" || row.CausalEventRef != root.CausalEventRef ||
                    row.CreatedEventRef != WoundEffectOperationEventRef.Create(root.CausalEventRef,
                        root.MechanicsOrdinal, root.OperationOrdinal, root.OperationKind) ||
                    row.Materialization.ComponentCount != root.ExpectedComponentCount ||
                    row.Materialization.MaterializationFingerprint != root.ExpectedMaterializationFingerprint ||
                    mapping.Count(value => value.ApplicationRef == row.ApplicationRef && value.EffectId == row.EffectId) != 1 ||
                    !plan.AllocatedEffectIds.Contains(row.EffectId, StringComparer.Ordinal) ||
                    !plan.AllocatedTransitionIds.Contains(row.CreateTransitionId, StringComparer.Ordinal))
                    throw Invalid("Foreign, reordered or incomplete accepted legacy allocation.");
                results.Add(WoundAcceptedTurnData.CloneApplicationResult(row));
            }
            var seed = preparation.LegacyDraftBindings.Single(row => row.LegacyId == batch.PreparedWoundId);
            var fingerprint = WoundAcceptedTurnFingerprintWriter.Compute(new[] { preparation.PreparationFingerprint,
                batch.SourceExportFingerprint, accepted.EffectAcceptedTurnPlanFingerprint }.Concat(results.SelectMany(row =>
                    new[] { row.ApplicationRef, row.EffectId, row.CreateTransitionId, row.CreatedEventRef,
                        row.Materialization.MaterializationFingerprint })));
            groups.Add(new(seed.LegacyOrdinal, seed.LegacyId, batch.SourceExportFingerprint, results.ToImmutable(), fingerprint));
        }
        return groups;
    }

    private static InvalidDataException Invalid(string message) => new(message);
}
