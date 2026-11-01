using System.Text.Json;
using System.Text.Json.Nodes;
using BookOfEternityClient.Services;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed partial class MortalWoundTreatmentResolverTests
{
    private static void PrepareTerminalSettlementWound(JsonObject wound, string variant)
    {
        if (variant == "remove")
        {
            PrepareTerminalPhysicalWound(wound, "guaranteed", removeThenHeal: true);
            return;
        }
        Assert.Equal("reduce_legacy", variant);
        wound["severity"]!["value"] = "IV"; wound["severity"]!["rank"] = 4;
        wound["severity"]!["maximumAtCreation"] = "IV";
        wound["consequences"]!["slotBudget"] = 4;
        var legacies = new JsonArray(CreateCosmeticLegacyDraft(),
            CreateMechanicalLegacyDraft("terminal_left", "shared_application"),
            CreateMechanicalLegacyDraft("terminal_right", "shared_application"));
        // The same local spelling is legal in two independent legacy namespaces.
        foreach (var legacy in legacies.OfType<JsonObject>().Where(row => row["kind"]!.GetValue<string>() == "mechanical_effect"))
        {
            legacy["effectDraft"]!["definitions"]![0]!["definitionRef"] = "shared_definition";
            legacy["effectDraft"]!["applications"]![0]!["definitionRef"] = "shared_definition";
        }
        AddTerminalLegacyReaction(legacies[1]!.AsObject());
        wound["treatment"]!["routes"]![0]!["outcomes"]![0]!["result"] = new JsonArray(
            new JsonObject { ["kind"] = "reduce_severity", ["steps"] = 1 },
            new JsonObject { ["kind"] = "reduce_severity", ["steps"] = 2 },
            new JsonObject { ["kind"] = "heal", ["legacies"] = legacies });
    }

    private static void AddTerminalLegacyReaction(JsonObject legacy)
    {
        var draft = legacy["effectDraft"]!.AsObject();
        const string parentKey = "terminal_parent_reaction";
        const string childKey = "terminal_child_restore";
        var parent = EffectMaterializationTestFixture.CreateDefinition("event_reaction");
        parent["definitionKey"] = parentKey;
        parent["lifetime"] = CreateSourceBoundActionControlDefinition("unused")["lifetime"]!.DeepClone();
        parent["links"] = new JsonArray();
        var payload = parent["components"]![0]!["payload"]!.AsObject();
        payload["eventType"] = "owner_turn_end"; payload["resultKind"] = "apply_definition";
        payload["definitionKey"] = childKey; payload["parameters"] = new JsonObject { ["amount"] = 7 };
        payload["maxExpansion"] = 1;
        parent["triggers"]![0]!["eventType"] = "owner_turn_end";
        var child = EffectMaterializationTestFixture.CreateDefinition("periodic_restore");
        child["definitionKey"] = childKey;
        child["lifetime"] = parent["lifetime"]!.DeepClone(); child["triggers"] = new JsonArray(); child["links"] = new JsonArray();
        draft["definitions"]![0]!["definition"] = parent;
        draft["definitions"]!.AsArray().Add(new JsonObject { ["definitionRef"] = "child_definition", ["definition"] = child });
    }

    private static void AssertTerminalPublicationStages(AcceptedMechanicsPlan plan, string variant)
    {
        var bundle = Assert.IsType<AcceptedMechanicsWoundStageBundle>(plan.WoundStageBundle);
        var input = bundle.Input; var prepared = bundle.PreparedPlan;
        var accepted = bundle.EffectBatchPlan; var finalPlan = bundle.FinalPlan;
        var frame = input.Binding; var source = accepted.EffectPlan; var effectInput = accepted.EffectInput;
        var terminations = accepted.TerminationResults; var preparedBatches = prepared.EffectOperationBatches;
        var contributions = finalPlan.CarrierContributions; var afterImages = source.CarrierAfterImages;
        Assert.True(WoundAcceptedTurnPlanner.TryReadTerminalTreatment(prepared, out var terminal));
        Assert.True(EffectAcceptedTurnPlanner.TryReadMortalTreatmentExecution(prepared,
            source, out var stages));
        Assert.Equal(2, stages.Count);
        Assert.Equal(variant == "reduce_legacy" ? 4 : 1, stages[0].Before.Severity.Rank);
        Assert.Equal(1, stages[0].After.Severity.Rank);
        Assert.Equal("healed", stages[1].After.Lifecycle);
        Assert.Equal(stages[0].After.LastTransition.Ordinal + 1, stages[1].After.LastTransition.Ordinal);
        Assert.Equal(WoundMaterializationContract.SerializeCanonical(stages[0].After),
            WoundMaterializationContract.SerializeCanonical(stages[1].Before));
        var originalRoot = input.PreTurnHistory;
        var finalRoot = finalPlan.HistoryAfterImage;
        var original = WoundHistoryState.Parse(originalRoot.ToJsonString(), WoundHistoryState.HistoryPath);
        Assert.True(original.IsValid, DescribeIssues(original.Issues));
        var history = WoundHistoryState.Parse(finalRoot.ToJsonString(), WoundHistoryState.HistoryPath);
        Assert.True(history.IsValid, DescribeIssues(history.Issues));
        var originalRows = originalRoot["transitions"]!.AsArray();
        var finalRows = finalRoot["transitions"]!.AsArray();
        Assert.Equal(originalRows.Count + 2, finalRows.Count);
        for (var index = 0; index < originalRows.Count; index++)
            Assert.True(JsonNode.DeepEquals(originalRows[index], finalRows[index]), "original History prefix row " + index);
        var rows = history.State!.Transitions.Skip(originalRows.Count).ToArray();
        Assert.Equal(new[] { "treat", "heal" }, rows.Select(row => row.Kind));
        Assert.Equal(original.State!.NextOrdinal + 2, history.State.NextOrdinal);
        var healIntent = Assert.IsType<MortalWoundHealOutcomeIntent>(terminal.Resolution.OutcomeIntents[^1]);
        for (var index = 0; index < rows.Length; index++)
        {
            var row = rows[index]; var stage = stages[index];
            Assert.Equal(original.State.NextOrdinal + index, row.Ordinal);
            Assert.Equal(terminal.Outcome.Before.LastTransition.Ordinal + index + 1, row.WoundTransitionOrdinal);
            Assert.Equal(frame.Turn, row.Turn); Assert.Equal(terminal.Request.Coordinates.WoundId, row.WoundId);
            Assert.Equal(stage.After.LastTransition.TransitionId, row.TransitionId);
            Assert.Equal(stage.OperationKey, row.OperationKey); Assert.Equal(stage.EventRef, row.EventRef);
            Assert.Equal(WoundIdentityState.ComputeSemanticFingerprint(stage.Before), row.BeforeFingerprint);
            Assert.Equal(WoundIdentityState.ComputeSemanticFingerprint(stage.After), row.AfterFingerprint);
            Assert.Equal(terminal.Request.RequestFingerprint, row.SourceFingerprint); Assert.Equal(index == 1, row.Terminal);
        }
        Assert.Equal(terminal.Request.Coordinates.OperationKey, rows[0].OperationKey);
        Assert.Equal(terminal.Request.Coordinates.EventRef, rows[0].EventRef);
        Assert.Equal(healIntent.HealChildCoordinates.OperationKey, rows[1].OperationKey);
        Assert.Equal(healIntent.HealChildCoordinates.EventRef, rows[1].EventRef);
        Assert.Equal(rows[0].AfterFingerprint, rows[1].BeforeFingerprint);
        var finalCarriers = input.PreTurnCarriers;
        foreach (var contribution in contributions)
        {
            var collection = WoundCarrierCollectionAuthority.Resolve(finalCarriers, contribution.Owner);
            foreach (var mutation in contribution.Mutations)
                Assert.Null(AcceptedMechanicsCarrierAssembler.ApplyMutation(collection, contribution.Owner, mutation));
        }
        var finalCatalog = WoundCarrierCatalog.Build(finalCarriers); Assert.Empty(finalCatalog.Issues);
        var finalIdentity = WoundIdentityState.Parse(finalPlan.IdentityIndexAfterImage.ToJsonString(), WoundIdentityState.StatePath);
        Assert.True(finalIdentity.IsValid, DescribeIssues(finalIdentity.Issues));
        Assert.Empty(history.State.ValidateAgreement(finalIdentity.State!, finalCatalog));
        Assert.True(finalIdentity.State!.TryGetEntry(terminal.Request.Coordinates.WoundId, out var healed));
        Assert.Equal("healed", healed.Status); Assert.Equal(rows[1].TransitionId, healed.TerminalTransitionId);
        Assert.Single(contributions.SelectMany(row => row.Mutations), row =>
            row.Operation == "delete" && row.WoundId == terminal.Request.Coordinates.WoundId && row.AfterWound is null);
        var legacyBatches = terminal.Legacies.EffectOperationBatches;
        var declaredHeal = Assert.IsType<MortalWoundHealOperation>(terminal.Resolution.DeclaredResult[^1]);
        var mechanicalDrafts = declaredHeal.Legacies.OfType<MortalWoundMechanicalEffectLegacyDraft>().ToArray();
        Assert.Equal(variant == "reduce_legacy" ? 2 : variant == "remove" ? 1 : 0, mechanicalDrafts.Length);
        Assert.Equal(mechanicalDrafts.Length, legacyBatches.Count);
        var groups = MortalWoundHealLegacyPlanner.Finalize(terminal.Legacies, prepared, accepted);
        Assert.Equal(legacyBatches.Select(row => row.PreparedWoundId), groups.Select(row => row.LegacyId));
        var admittedSources = effectInput.SourceAuthority;
        var wound = terminal.Outcome.Before;
        Assert.Single(admittedSources.SnapshotWoundGroupAuthorities(), row =>
            row.Key.Realm == wound.Owner.Realm && row.Key.Kind == "wound" && row.Key.SourceId == wound.WoundId);
        Assert.Equal(admittedSources.CanonicalFingerprint, source.SourceAuthorityFingerprint);
        var retiredSources = admittedSources.WithoutHealedWoundSource(wound.Owner.Realm, wound.WoundId);
        Assert.Empty(retiredSources.Issues);
        Assert.Equal(retiredSources.CanonicalFingerprint, source.SourceAuthority.CanonicalFingerprint);
        Assert.NotEqual(source.SourceAuthorityFingerprint, source.SourceAuthority.CanonicalFingerprint);
        Assert.DoesNotContain(source.SourceAuthority.SnapshotWoundGroupAuthorities(), row =>
            row.Key.Realm == wound.Owner.Realm && row.Key.Kind == "wound" && row.Key.SourceId == wound.WoundId);
        var honestCopy = CopyTerminalEffectPayload(source, afterImages);
        Assert.True(EffectAcceptedTurnPlanner.TryReadMortalTreatmentExecution(prepared, honestCopy, out _));
        var beforeRoleFailure = Assert.Throws<InvalidDataException>(() => CopyTerminalEffectPayload(source,
            afterImages, sourceFingerprint: source.SourceAuthority.CanonicalFingerprint));
        Assert.Equal("Lifecycle evidence belongs to a different effect result.", beforeRoleFailure.Message);
        var finalRoleFailure = Assert.Throws<InvalidDataException>(() => CopyTerminalEffectPayload(source,
            afterImages, finalSources: admittedSources));
        Assert.Equal("Lifecycle evidence belongs to a different effect result.", finalRoleFailure.Message);
        var surviving = EffectCarrierCatalog.Build(source.ResourceTriggerCarriers);
        Assert.Empty(surviving.Issues);
        var legacySources = source.SourceAuthority.SnapshotSameTurnLegacyEntries();
        var actualApplications = accepted.ApplicationResults;
        Assert.Equal(legacyBatches.Sum(batch => batch.SourceExport.Definitions.Count),
            legacySources.Count);
        foreach (var batch in legacyBatches)
        foreach (var root in batch.RootApplications)
        {
            var allocation = Assert.Single(actualApplications, row => row.ApplicationRef == root.ApplicationRef);
            Assert.True(surviving.TryResolveOne(allocation.EffectId, out var occurrence));
            Assert.Equal(root.ExpectedCarrierCoordinate, occurrence.Coordinate);
            Assert.Equal(root.ExpectedMaterializationFingerprint, WoundEffectMaterializationFingerprint.Compute(
                root.ExpectedSourceKey, EffectMaterializationContract.SchemaVersion, root.Parameters,
                occurrence.Effect["components"]!.AsArray()));
            var definition = Assert.Single(batch.SourceExport.Definitions, row => row.DefinitionKey == root.DefinitionKey);
            var binding = Assert.Single(legacySources, row => row.Key == root.ExpectedSourceKey);
            Assert.True(JsonNode.DeepEquals(definition.Definition, binding.Definition));
        }
        if (variant == "reduce_legacy")
        {
            Assert.Contains(stages[0].Applications, row => row.SourceKey.Kind == "wound");
            var identity = EffectIdentityState.Parse(JsonSerializer.SerializeToElement(source.IdentityIndexAfterImage),
                EffectIdentityState.StatePath);
            Assert.Empty(identity.Issues);
            foreach (var application in stages[0].Applications.Where(row => row.SourceKey.Kind == "wound"))
            {
                Assert.True(identity.State!.TryGetEntry(application.EffectId, out var entry));
                Assert.Equal("expired", entry.State);
                Assert.Equal(new[] { "create", "expire" }, entry.Transitions.Select(row => row.Kind));
            }
        }
        Assert.All(legacyBatches, batch =>
        {
            Assert.Equal("wound_legacy", batch.SourceExport.Kind);
            Assert.False(batch.SourceExport.Materializable);
            Assert.Single(batch.RootApplications);
        });
        if (legacyBatches.Count == 0) return;
        var changedImages = afterImages.ToDictionary(pair => pair.Key, pair => pair.Value.DeepClone().AsObject(), StringComparer.Ordinal);
        var effects = changedImages[EffectCarrierCatalog.PlayerPath]["activeEffects"]!.AsArray();
        Assert.NotEmpty(effects);
        effects[0]!["display"]!["description"] = "changed actual effect payload";
        Assert.Throws<InvalidDataException>(() => CopyTerminalEffectPayload(source, changedImages));
        Assert.NotEmpty(actualApplications);
        var mapFaults = new List<(string Label, IReadOnlyList<EffectAcceptedApplicationResult> Applications)>
        {
            ("omitted actual application", actualApplications.Skip(1).ToArray()),
            ("foreign allocation", actualApplications.Select((row, index) => index == 0
                ? row with { EffectId = "foreign_effect" } : row).ToArray())
        };
        if (actualApplications.Count > 1)
            mapFaults.Add(("reordered actual applications", actualApplications.Reverse().ToArray()));
        foreach (var (label, applications) in mapFaults)
        {
            var resealed = WoundEffectBatchAcceptedPlan.Create(prepared, effectInput, source,
                applications, terminations);
            var refused = WoundAcceptedTurnPlannerCore.Finalize(prepared, new(resealed, Array.Empty<ValidationIssue>()));
            Assert.False(refused.Success, label); Assert.NotEmpty(refused.Issues);
        }
        var prefix = preparedBatches.Take(preparedBatches.Count - legacyBatches.Count).ToArray();
        var batchFaults = new List<(string Label, IReadOnlyList<WoundEffectOperationBatch> Batches)>
        {
            ("omitted", prefix.Concat(legacyBatches.Skip(1)).ToArray()),
            ("foreign", prefix.Append(ResealRemovalBatch(legacyBatches[0], export:
                ForeignTerminalLegacySource(legacyBatches[0].SourceExport))).Concat(legacyBatches.Skip(1)).ToArray())
        };
        if (variant == "reduce_legacy")
        {
            Assert.Equal(2, legacyBatches.Count);
            Assert.NotEqual(legacyBatches[0].PreparedWoundId, legacyBatches[1].PreparedWoundId);
            var merged = ResealRemovalBatch(legacyBatches[0], applications:
                legacyBatches.SelectMany(row => row.RootApplications).ToArray());
            batchFaults.Add(("reordered", prefix.Concat(legacyBatches.Reverse()).ToArray()));
            batchFaults.Add(("merged", prefix.Append(merged).ToArray()));
            batchFaults.Add(("split", prefix.Concat(legacyBatches).Append(legacyBatches[0]).ToArray()));
        }
        else Assert.Single(legacyBatches);
        foreach (var (label, batches) in batchFaults)
        {
            var changed = ResealRemovalPrepared(prepared, batches, prepared.TreatmentContinuationAuthority!);
            var refused = WoundAcceptedTurnPlanCache.ValidatePreparedResult(input,
                WoundAcceptedTurnFingerprints.ComputeInput(input), new(changed, Array.Empty<ValidationIssue>()));
            Assert.False(refused.Success, label); Assert.NotEmpty(refused.Issues);
        }
        var originalDefinition = legacyBatches[0].SourceExport.Definitions[0].Definition.ToJsonString();
        var detachedDefinition = legacyBatches[0].SourceExport.Definitions[0].Definition;
        detachedDefinition["display"]!["description"] = "detached mutation";
        Assert.Equal(originalDefinition, terminal.Legacies.EffectOperationBatches[0].SourceExport.Definitions[0].Definition.ToJsonString());
    }

    private static EffectAcceptedTurnPlan CopyTerminalEffectPayload(EffectAcceptedTurnPlan source,
        IReadOnlyDictionary<string, JsonObject> images, string? sourceFingerprint = null,
        EffectSourceAuthority? finalSources = null) => new(source.InputFingerprint,
        source.CarrierAuthorityFingerprint, sourceFingerprint ?? source.SourceAuthorityFingerprint, source.TargetAuthorityFingerprint,
        source.AllocatedCombatantIds, source.AllocatedEffectIds, source.AllocatedTransitionIds, source.Sources,
        source.Targets, source.SourceBindings, source.DeferredReactions, source.ReactionExpansionCount,
        source.ReactionExpansionUsage, source.ActiveEffects, source.ResourceTriggerCarriers, finalSources ?? source.SourceAuthority,
        source.TargetAuthority, source.EventInput, source.CarrierBeforeImages, images, source.IdentityIndexBeforeImage,
        source.IdentityIndexAfterImage, source.TouchedPaths, source.DeletedPaths, source.AcceptedCarrierBaselines,
        woundApplicationRootEffectBindings: source.WoundApplicationRootEffectBindings,
        skillScopeAuthority: source.SkillScopeAuthority, mortalRecoveryExecutionAuthority: source.MortalRecoveryExecutionAuthority,
        lifecycleExecutionLedger: source.LifecycleExecutionLedger,
        spiritualHealingExecutionAuthority: source.SpiritualHealingExecutionAuthority,
        mortalDiagnosisExecutionAuthority: source.MortalDiagnosisExecutionAuthority,
        mortalTreatmentExecutionAuthority: source.MortalTreatmentExecutionAuthority);

    private static WoundEffectSourceExport ForeignTerminalLegacySource(WoundEffectSourceExport source) =>
        new(source.SchemaVersion, source.Kind, "foreign_legacy", source.SourceRef, source.State, source.Materializable,
            source.Realm, source.Owner, source.CausalEventRef, source.EventSemanticFingerprint, source.OpportunityId,
            source.OpportunityAuthorityFingerprint, source.Definitions);

    private static async Task AssertTerminalSettlementFaultsAsync(Dictionary<string, byte[]?> current,
        MortalWoundTreatmentPersistedResult result,
        Func<string, Action<Dictionary<string, byte[]?>>, Task> refuse,
        Func<Dictionary<string, byte[]?>, Task> accept)
    {
        await refuse("omitted heal stage", map => Change(map, WoundHistoryState.HistoryPath,
            root => root["transitions"]!.AsArray().RemoveAt(root["transitions"]!.AsArray().Count - 1)));
        await refuse("reordered linked stages", map => Change(map, WoundHistoryState.HistoryPath, root =>
        {
            var rows = root["transitions"]!.AsArray(); var first = rows[^2]!.DeepClone(); var last = rows[^1]!.DeepClone();
            rows[^2] = last; rows[^1] = first;
        }));
        await refuse("foreign terminal event", map => Change(map, WoundHistoryState.HistoryPath,
            root => root["transitions"]!.AsArray()[^1]!["eventRef"] = "foreign_terminal_event"));
        await refuse("healed identity drift", map => Change(map, WoundIdentityState.StatePath, root =>
            root["entries"]!.AsArray().OfType<JsonObject>().Single(row =>
                row["woundId"]!.GetValue<string>() == result.Request.Coordinates.WoundId)["terminalTransitionId"] = "foreign_terminal"));
        var index = JsonNode.Parse(CanonicalJsonUtf8.DecodeOneOptionalBom(current[EffectIdentityState.StatePath]!))!.AsObject();
        var retired = index["entries"]!.AsArray().OfType<JsonObject>().Where(row =>
            row["source"]?["kind"]?.GetValue<string>() == "wound" &&
            row["source"]?["sourceId"]?.GetValue<string>() == result.Request.Coordinates.WoundId).ToArray();
        Assert.NotEmpty(retired); Assert.All(retired, row => Assert.Equal("expired", row["state"]!.GetValue<string>()));
        var changedRoot = retired.OrderByDescending(row => row["createdAtTurn"]!.GetValue<int>()).First();
        foreach (var fault in new[] { "source", "target", "owner", "stack", "resurrection", "order", "parent" })
            await refuse("retired mapping " + fault, map => Change(map, EffectIdentityState.StatePath, root =>
            {
                var row = root["entries"]!.AsArray().OfType<JsonObject>().Single(entry =>
                    entry["effectId"]!.GetValue<string>() == changedRoot["effectId"]!.GetValue<string>());
                if (fault == "source")
                {
                    row["source"]!["sourceId"] = "foreign_wound";
                    row["stackCoordinate"]!["sourceId"] = "foreign_wound";
                }
                if (fault == "target")
                {
                    row["target"]!["targetId"] = "foreign_target"; row["stackCoordinate"]!["targetId"] = "foreign_target";
                    row["owner"]!["ownerId"] = "foreign_target";
                }
                if (fault == "owner")
                {
                    row["owner"]!["kind"] = "npc"; row["owner"]!["ownerId"] = "field_medic_01";
                    row["owner"]!["carrierPath"] = EffectCarrierCatalog.NpcPath;
                    row["target"]!["kind"] = "npc"; row["target"]!["targetId"] = "field_medic_01";
                    row["stackCoordinate"]!["targetKind"] = "npc"; row["stackCoordinate"]!["targetId"] = "field_medic_01";
                }
                if (fault == "stack") row["stackCoordinate"]!["stackKey"] = "foreign_stack";
                if (fault == "resurrection") row["state"] = "active";
                var transitions = row["transitions"]!.AsArray();
                if (fault == "order")
                {
                    Assert.True(transitions.Count >= 2); var first = transitions[0]!.DeepClone(); var last = transitions[^1]!.DeepClone();
                    transitions[0] = last; transitions[^1] = first;
                }
                if (fault == "parent") transitions[0]!["sourceEffectIds"] = new JsonArray("foreign_parent");
                if (fault is "source" or "target" or "owner" or "stack")
                    Assert.Empty(EffectIdentityState.Parse(JsonSerializer.SerializeToElement(root), EffectIdentityState.StatePath).Issues);
            }));
        // The complete current reader already checks surviving sources/materialization;
        // mutate a real emitted independent primary, never invent a current effect.
        foreach (var legacy in index["entries"]!.AsArray().OfType<JsonObject>().Where(row =>
            row["source"]?["kind"]?.GetValue<string>() == "wound_legacy" &&
            row["transitions"]![0]!["sourceEffectIds"]!.AsArray().Count == 0))
        {
            var id = legacy["effectId"]!.GetValue<string>();
            await refuse("primary legacy materialization drift " + id, map => Change(map, EffectCarrierCatalog.PlayerPath, root =>
                root["activeEffects"]!.AsArray().OfType<JsonObject>().Single(row => row["effectId"]!.GetValue<string>() == id)
                    ["components"]![0]!["priority"] = 99));
            await refuse("foreign legacy owner " + id, map => Change(map, EffectIdentityState.StatePath, root =>
                root["entries"]!.AsArray().OfType<JsonObject>().Single(row => row["effectId"]!.GetValue<string>() == id)
                    ["source"]!["sourceId"] = "foreign_legacy"));
        }
        var descendants = index["entries"]!.AsArray().OfType<JsonObject>().Where(row =>
            row["source"]?["kind"]?.GetValue<string>() == "wound_legacy" &&
            row["transitions"]![0]!["sourceEffectIds"]!.AsArray().Count == 1).ToArray();
        if (result.Request.RouteSourceWound.Severity.Rank == 4)
        {
            var descendant = Assert.Single(descendants);
            var childId = descendant["effectId"]!.GetValue<string>();
            var parentId = descendant["transitions"]![0]!["sourceEffectIds"]![0]!.GetValue<string>();
            var parent = index["entries"]!.AsArray().OfType<JsonObject>().Single(row => row["effectId"]!.GetValue<string>() == parentId);
            Assert.NotEqual(parent["source"]!["definitionKey"]!.GetValue<string>(), descendant["source"]!["definitionKey"]!.GetValue<string>());
            void ChildAmount(Dictionary<string, byte[]?> map, int amount) => Change(map, EffectCarrierCatalog.PlayerPath, root =>
                root["activeEffects"]!.AsArray().OfType<JsonObject>().Single(row => row["effectId"]!.GetValue<string>() == childId)
                    ["components"]![0]!["payload"]!["amount"] = amount);
            var lawful = current.ToDictionary(pair => pair.Key, pair => pair.Value?.ToArray(), StringComparer.Ordinal);
            ChildAmount(lawful, 8);
            await accept(lawful); // Current bound validity, not historical child-parameter reproduction.
            await refuse("out-of-bounds ordinary child", map => ChildAmount(map, 11));
            foreach (var fault in new[] { "forbidden_edge", "foreign_source", "foreign_owner", "ambiguous_parent", "unexplained_member" })
            {
                void Fault(Dictionary<string, byte[]?> map)
                {
                    Change(map, EffectIdentityState.StatePath, root =>
                    {
                        var entry = root["entries"]!.AsArray().OfType<JsonObject>().Single(row => row["effectId"]!.GetValue<string>() == childId);
                        if (fault == "forbidden_edge") entry["source"]!["definitionKey"] = parent["source"]!["definitionKey"]!.DeepClone();
                        if (fault == "foreign_source")
                        {
                            entry["source"]!["sourceId"] = "foreign_legacy"; entry["stackCoordinate"]!["sourceId"] = "foreign_legacy";
                        }
                        if (fault == "foreign_owner")
                        {
                            entry["owner"]!["ownerId"] = "foreign_owner"; entry["target"]!["targetId"] = "foreign_owner";
                            entry["stackCoordinate"]!["targetId"] = "foreign_owner";
                        }
                        if (fault == "ambiguous_parent") entry["transitions"]![0]!["sourceEffectIds"]!.AsArray().Add(retired[0]["effectId"]!.DeepClone());
                        if (fault == "unexplained_member") entry["transitions"]![0]!["sourceEffectIds"] = new JsonArray();
                        Assert.Empty(EffectIdentityState.Parse(JsonSerializer.SerializeToElement(root), EffectIdentityState.StatePath).Issues);
                    });
                    if (fault == "forbidden_edge") Change(map, EffectCarrierCatalog.PlayerPath, root =>
                    {
                        var effects = root["activeEffects"]!.AsArray();
                        var parentEffect = effects.OfType<JsonObject>().Single(row => row["effectId"]!.GetValue<string>() == parentId);
                        var position = effects.Select((row, ordinal) => (row, ordinal)).Single(pair =>
                            pair.row!["effectId"]!.GetValue<string>() == childId).ordinal;
                        var chronologicalChild = effects[position]!["chronology"]!.DeepClone();
                        var changed = parentEffect.DeepClone().AsObject(); changed["effectId"] = childId;
                        changed["chronology"] = chronologicalChild; effects[position] = changed;
                    });
                }
                if (fault is "forbidden_edge" or "ambiguous_parent" or "unexplained_member")
                {
                    var canonical = current.ToDictionary(pair => pair.Key, pair => pair.Value?.ToArray(), StringComparer.Ordinal);
                    Fault(canonical);
                    await AssertCanonicalTerminalMapAsync(canonical, result);
                }
                await refuse("ordinary legacy descendant " + fault, Fault);
            }

        }
    }
    private static async Task AssertCanonicalTerminalMapAsync(Dictionary<string, byte[]?> map, MortalWoundTreatmentPersistedResult result)
    {
        var exact = result.ReconstructGuidedPublication();
        var read = await MortalWoundTreatmentCompletion.ReadCurrentAsync(path => Task.FromResult(map[path]));
        _ = await MortalWoundTreatmentCompletion.RequireCanonicalSettlementAsync(read.Bytes, read.Nodes, result.Request,
            exact.Resolution.ResultFingerprint, result.Request.Coordinates.Turn, exact.Finalization, exact.SemanticFingerprint);
    }

}
