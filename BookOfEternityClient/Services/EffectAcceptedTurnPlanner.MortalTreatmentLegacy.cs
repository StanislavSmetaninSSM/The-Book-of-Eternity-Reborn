using System.Text.Json.Nodes;

namespace BookOfEternityClient.Services;

internal static partial class EffectAcceptedTurnPlanner
{
    /// <summary>
    /// Admits independent legacy applications only from the actual treatment's
    /// exact prepared batch. These requests enter the existing ApplyApplication
    /// path and its sole effect/transition identity allocator.
    /// </summary>
    private static void PrepareTreatmentLegacyApplications(EffectAcceptedTurnInput input,
        WoundPreparedAcceptedTurnPlan prepared, WoundEffectOperationBatch batch, string path,
        IReadOnlyDictionary<string, WoundAcceptedEventAuthority> acceptedEvents,
        HashSet<string> applicationRefs, HashSet<string> applicationAliases,
        HashSet<string> createdEvents, HashSet<string> createdEventAliases,
        List<WoundApplicationRequest> applications, List<ValidationIssue> issues)
    {
        var initialIssues = issues.Count;
        if (!WoundAcceptedTurnPlanner.TryReadTreatmentLegacyPreparation(prepared, out var preparation) || preparation is null ||
            !preparation.EffectOperationBatches.Any(expected => expected.SourceExportFingerprint == batch.SourceExportFingerprint &&
                expected.PreparedWoundId == batch.PreparedWoundId))
        {
            AddWoundBatchIssue(issues, path, "wound_legacy_private_preparation_missing",
                "one exact independent legacy batch from the real treatment continuation", batch.PreparedWoundId);
            return;
        }
        var source = batch.SourceExport;
        var sources = input.SourceAuthority.SnapshotSameTurnLegacyEntries();
        var definitions = source.Definitions;
        foreach (var root in batch.RootApplications)
        {
            var rootIssueCount = issues.Count;
            var expectedKey = new EffectSourceKey(source.Realm, "wound_legacy", source.SourceId, root.DefinitionKey);
            var matching = sources.Where(row => row.Key == expectedKey).ToArray();
            var matchingDefinitions = definitions.Where(row => row.DefinitionKey == root.DefinitionKey).ToArray();
            var selector = root.SourceSelector;
            if (source.Kind != "wound_legacy" || source.Materializable || source.State != "active" ||
                source.SourceId != batch.PreparedWoundId || source.SourceRef != batch.LocalWoundRef ||
                root.ExpectedSourceKey != expectedKey || selector.Realm != source.Realm || selector.Kind != "wound_legacy" ||
                selector.SourceRef != source.SourceRef || selector.SourceId is not null || selector.DefinitionKey != root.DefinitionKey ||
                matching.Length != 1 || matchingDefinitions.Length != 1 || matching[0].Materializable || !matching[0].Active ||
                !matching[0].SameTurn || matching[0].SourceRef != source.SourceRef ||
                !JsonNode.DeepEquals(matching[0].Definition, matchingDefinitions[0].Definition) ||
                !acceptedEvents.TryGetValue(root.CausalEventRef, out var causal) ||
                source.CausalEventRef != root.CausalEventRef || causal.SemanticFingerprint != source.EventSemanticFingerprint ||
                root.OperationKind != "apply" || root.MechanicsOrdinal <= 0 || root.OperationOrdinal <= 0 ||
                root.PriorRootEffectId is not null || root.SlotBindings.Count != 0)
            {
                AddWoundBatchIssue(issues, path, "wound_legacy_source_binding_invalid",
                    "exact private non-public independent source, definition, accepted cause and direct allocation", root.ApplicationRef);
                continue;
            }
            if (!WoundEffectCarrierAdapter.TryCreateTargetKey(source.Owner, out var target) || target != root.ExpectedTargetKey ||
                !TryCreateExpectedWoundCarrierCoordinate(source.Owner, target, matching[0].Definition, out var coordinate) ||
                coordinate != root.ExpectedCarrierCoordinate)
                AddWoundBatchIssue(issues, path, "wound_legacy_target_binding_invalid",
                    "the original exact owner/target/carrier", root.ApplicationRef);
            ValidateWoundTargetSelector(input.TargetAuthority, root.TargetSelector, root.ExpectedTargetKey, path + ".target", issues);
            var parameters = root.Parameters;
            issues.AddRange(input.SourceAuthority.ValidateCanonicalParameters(matching[0], parameters)
                .Select(issue => Prefix(issue, path + ".parameters")));
            ValidateWoundMaterializationExpectation(root, matching[0], parameters, path, issues);
            var eventRef = WoundEffectOperationEventRef.Create(root.CausalEventRef,
                root.MechanicsOrdinal, root.OperationOrdinal, root.OperationKind);
            if (!applicationRefs.Add(root.ApplicationRef) ||
                !applicationAliases.Add(MortalLocationIdentityState.BuildConfusableKey(root.ApplicationRef)) ||
                !createdEvents.Add(eventRef) ||
                !createdEventAliases.Add(MortalLocationIdentityState.BuildConfusableKey(eventRef)))
                AddWoundBatchIssue(issues, path, "wound_legacy_coordinate_collision",
                    "disjoint full prefix/joint/legacy application and event coordinates", root.ApplicationRef);
            if (issues.Count == rootIssueCount)
                applications.Add(new(batch, root, source, matchingDefinitions[0],
                    new Application(matching[0], root.ExpectedTargetKey, parameters, eventRef, root.CausalEventRef,
                        path + ".components", "wound_materialization", AcceptedContinuation: true), eventRef));
        }
        if (issues.Count != initialIssues) return;
        // No allocation or canonical write occurs here. BuildCore executes this
        // closed request vector through the unchanged ordinary effect allocator.
    }
}
