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
        wound["treatment"]!["routes"]![0]!["outcomes"]![0]!["result"] = new JsonArray(
            new JsonObject { ["kind"] = "reduce_severity", ["steps"] = 1 },
            new JsonObject { ["kind"] = "reduce_severity", ["steps"] = 2 },
            new JsonObject { ["kind"] = "heal", ["legacies"] = legacies });
    }

    private static void AssertTerminalPublicationStages(AcceptedMechanicsPlan plan, string variant)
    {
        var bundle = Assert.IsType<AcceptedMechanicsWoundStageBundle>(plan.WoundStageBundle);
        Assert.True(WoundAcceptedTurnPlanner.TryReadTerminalTreatment(bundle.PreparedPlan, out var terminal));
        Assert.True(EffectAcceptedTurnPlanner.TryReadMortalTreatmentExecution(bundle.PreparedPlan,
            bundle.EffectBatchPlan.EffectPlan, out var stages));
        Assert.Equal(2, stages.Count);
        Assert.Equal(variant == "reduce_legacy" ? 4 : 1, stages[0].Before.Severity.Rank);
        Assert.Equal(1, stages[0].After.Severity.Rank);
        Assert.Equal("healed", stages[1].After.Lifecycle);
        Assert.Equal(stages[0].After.LastTransition.Ordinal + 1, stages[1].After.LastTransition.Ordinal);
        Assert.Equal(WoundMaterializationContract.SerializeCanonical(stages[0].After),
            WoundMaterializationContract.SerializeCanonical(stages[1].Before));
        var history = WoundHistoryState.Parse(bundle.FinalPlan.HistoryAfterImage.ToJsonString(), WoundHistoryState.HistoryPath);
        Assert.True(history.IsValid, DescribeIssues(history.Issues));
        var rows = history.State!.Transitions.Where(row => row.Turn == bundle.Input.Binding.Turn).ToArray();
        Assert.Equal(new[] { "treat", "heal" }, rows.Select(row => row.Kind));
        Assert.Equal(rows[0].AfterFingerprint, rows[1].BeforeFingerprint);
        Assert.Single(bundle.FinalPlan.CarrierContributions.SelectMany(row => row.Mutations), row =>
            row.Operation == "delete" && row.WoundId == terminal.Request.Coordinates.WoundId && row.AfterWound is null);
        var legacyBatches = terminal.Legacies.EffectOperationBatches;
        Assert.Equal(variant == "reduce_legacy" ? 2 : 0, legacyBatches.Count);
        var groups = MortalWoundHealLegacyPlanner.Finalize(terminal.Legacies, bundle.PreparedPlan, bundle.EffectBatchPlan);
        Assert.Equal(legacyBatches.Select(row => row.PreparedWoundId), groups.Select(row => row.LegacyId));
        if (variant == "reduce_legacy")
        {
            Assert.NotEmpty(stages[0].Applications.Where(row => row.SourceKey.Kind == "wound"));
            var identity = EffectIdentityState.Parse(JsonSerializer.SerializeToElement(bundle.EffectBatchPlan.EffectPlan.IdentityIndexAfterImage),
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
        var accepted = bundle.EffectBatchPlan;
        var actualApplications = accepted.ApplicationResults;
        Assert.True(actualApplications.Count >= 2);
        foreach (var (label, applications) in new (string, IReadOnlyList<EffectAcceptedApplicationResult>)[]
        {
            ("omitted actual application", actualApplications.Skip(1).ToArray()),
            ("reordered actual applications", actualApplications.Reverse().ToArray()),
            ("foreign allocation", actualApplications.Select((row, index) => index == 0
                ? row with { EffectId = "foreign_effect" } : row).ToArray())
        })
        {
            var resealed = WoundEffectBatchAcceptedPlan.Create(bundle.PreparedPlan, accepted.EffectInput, accepted.EffectPlan,
                applications, accepted.TerminationResults);
            var refused = WoundAcceptedTurnPlannerCore.Finalize(bundle.PreparedPlan, new(resealed, Array.Empty<ValidationIssue>()));
            Assert.False(refused.Success, label); Assert.NotEmpty(refused.Issues);
        }
        var prepared = bundle.PreparedPlan;
        var prefix = prepared.EffectOperationBatches.Take(prepared.EffectOperationBatches.Count - legacyBatches.Count).ToArray();
        var merged = ResealRemovalBatch(legacyBatches[0], applications:
            legacyBatches.SelectMany(row => row.RootApplications).ToArray());
        foreach (var (label, batches) in new (string, IReadOnlyList<WoundEffectOperationBatch>)[]
        {
            ("omitted", prefix.Concat(legacyBatches.Take(1)).ToArray()),
            ("reordered", prefix.Concat(legacyBatches.Reverse()).ToArray()),
            ("merged", prefix.Append(merged).ToArray()),
            ("split", prefix.Concat(legacyBatches).Append(legacyBatches[0]).ToArray()),
            ("foreign", prefix.Append(ResealRemovalBatch(legacyBatches[0], export:
                ForeignTerminalLegacySource(legacyBatches[0].SourceExport))).Append(legacyBatches[1]).ToArray())
        })
        {
            var changed = ResealRemovalPrepared(prepared, batches, prepared.TreatmentContinuationAuthority!);
            var refused = WoundAcceptedTurnPlanCache.ValidatePreparedResult(bundle.Input,
                WoundAcceptedTurnFingerprints.ComputeInput(bundle.Input), new(changed, Array.Empty<ValidationIssue>()));
            Assert.False(refused.Success, label); Assert.NotEmpty(refused.Issues);
        }
        var originalDefinition = legacyBatches[0].SourceExport.Definitions[0].Definition.ToJsonString();
        var detachedDefinition = legacyBatches[0].SourceExport.Definitions[0].Definition;
        detachedDefinition["display"]!["description"] = "detached mutation";
        Assert.Equal(originalDefinition, terminal.Legacies.EffectOperationBatches[0].SourceExport.Definitions[0].Definition.ToJsonString());
    }

    private static WoundEffectSourceExport ForeignTerminalLegacySource(WoundEffectSourceExport source) =>
        new(source.SchemaVersion, source.Kind, "foreign_legacy", source.SourceRef, source.State, source.Materializable,
            source.Realm, source.Owner, source.CausalEventRef, source.EventSemanticFingerprint, source.OpportunityId,
            source.OpportunityAuthorityFingerprint, source.Definitions);

    private static async Task AssertTerminalSettlementFaultsAsync(Dictionary<string, byte[]?> current,
        MortalWoundTreatmentPersistedResult result,
        Func<string, Action<Dictionary<string, byte[]?>>, Task> refuse)
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
        foreach (var fault in new[] { "source", "target", "owner", "resurrection", "order", "parent" })
            await refuse("retired mapping " + fault, map => Change(map, EffectIdentityState.StatePath, root =>
            {
                var row = root["entries"]!.AsArray().OfType<JsonObject>().Single(entry =>
                    entry["effectId"]!.GetValue<string>() == changedRoot["effectId"]!.GetValue<string>());
                if (fault == "source")
                {
                    row["source"]!["sourceId"] = "foreign_wound";
                    row["stackCoordinate"]!["sourceId"] = "foreign_wound";
                }
                if (fault == "target") row["target"]!["targetId"] = "foreign_target";
                if (fault == "owner") row["owner"]!["ownerId"] = "foreign_owner";
                if (fault == "resurrection") row["state"] = "active";
                var transitions = row["transitions"]!.AsArray();
                if (fault == "order")
                {
                    Assert.True(transitions.Count >= 2); var first = transitions[0]!.DeepClone(); var last = transitions[^1]!.DeepClone();
                    transitions[0] = last; transitions[^1] = first;
                }
                if (fault == "parent") transitions[0]!["sourceEffectIds"] = new JsonArray("foreign_parent");
                if (fault is "source" or "target" or "owner")
                    Assert.Empty(EffectIdentityState.Parse(JsonSerializer.SerializeToElement(root), EffectIdentityState.StatePath).Issues);
            }));
        // The complete current reader already checks surviving sources/materialization;
        // mutate a real emitted independent primary, never invent a current effect.
        foreach (var legacy in index["entries"]!.AsArray().OfType<JsonObject>().Where(row =>
            row["source"]?["kind"]?.GetValue<string>() == "wound_legacy"))
        {
            var id = legacy["effectId"]!.GetValue<string>();
            await refuse("primary legacy materialization drift " + id, map => Change(map, EffectCarrierCatalog.PlayerPath, root =>
                root["activeEffects"]!.AsArray().OfType<JsonObject>().Single(row => row["effectId"]!.GetValue<string>() == id)
                    ["components"]![0]!["payload"]!["action"] = "attack"));
            await refuse("foreign legacy owner " + id, map => Change(map, EffectIdentityState.StatePath, root =>
                root["entries"]!.AsArray().OfType<JsonObject>().Single(row => row["effectId"]!.GetValue<string>() == id)
                    ["source"]!["sourceId"] = "foreign_legacy"));
        }
    }
}
