using System.Text;
using System.Text.Json.Nodes;
using BookOfEternityClient.Core;
using BookOfEternityClient.Services;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed partial class AfterlifeResourceCutoverTests
{
    /// <summary>Audits every authored parent/graph/identity/History/receipt join before real current owners acquire it.</summary>
    private static async Task<PendingTurnSnapshotReadAuthority> AssertPressureTransitionSignedInputsAsync(
        ResourceMaterializationTestContext context, FileSystemManager.CanonicalWriteLease lease,
        RetainedPressureGroupData data, SpiritualPressureThirdGuardTrigger trigger, string name)
    {
        var optional = new[] { WoundCarrierCatalog.PlayerPath, WoundCarrierCatalog.NpcPath,
            WoundCarrierCatalog.EnemiesPath, WoundCarrierCatalog.AlliesPath, EffectCarrierCatalog.PlayerPath, EffectCarrierCatalog.NpcPath };
        var required = PressureOrdinarySignedPaths.Append(ResourceMaterializationContract.DefinitionsPath)
            .Append(CanonicalResourceOwnerAuthorityComposer.AuthorityPath).ToArray();
        var read = PendingTurnSnapshotReader.ReadCurrent(context.FileSystem, lease,
            PendingTurnSnapshotPathSelection.CreateWithObservedOptionalPaths(required, optional));
        Assert.True(read.Success, string.Join("\n", read.Issues));
        var signed = Assert.IsType<PendingTurnSnapshotReadAuthority>(read.Snapshot);
        Assert.Equal((46, "chaos_sea", "request_" + name), (signed.TurnNumber, signed.Realm, signed.RequestId));
        Assert.Equal(new[] { 4, 15, 3, 15 }, signed.AcceptedD20EventValues);
        foreach (var path in required.Concat(optional))
        {
            var bytes = await context.FileSystem.ReadFileBytesAsync(lease, path);
            if (bytes is null) Assert.Contains(path, signed.AbsentLogicalPaths);
            else Assert.Equal(bytes, signed.ReadRequiredBytes(path));
        }
        var manifest = JsonNode.Parse((await context.FileSystem.ReadFileAsync(lease,
            LiveTurnPreparationService.PendingTurnSnapshotManifestPath))!)!;
        foreach (var pair in manifest["files"]!.AsObject())
            Assert.Equal(await context.FileSystem.ReadFileBytesAsync(lease, pair.Key),
                await context.FileSystem.ReadFileBytesAsync(lease, pair.Value!.GetValue<string>()));
        var parsed = ReadRetainedPressureParentData(signed, 3);
        Assert.False(parsed.Receipts.HasRetainedPressureComparisons);
        foreach (var field in new[] { "instances", "closures", "sources", "decisions" })
            Assert.Equal(3, parsed.ReceiptRoot[field]!.AsArray().Count);
        foreach (var field in new[] { "nextInstanceOrdinal", "nextClosureOrdinal", "nextSourceOrdinal", "nextDecisionOrdinal" })
            Assert.Equal(4, parsed.ReceiptRoot[field]!.GetValue<int>());
        var soul = JsonNode.Parse(signed.ReadRequiredBytes(ValidationService.SpiritualWoundSourceSession.SoulPath))!;
        Assert.All(soul["afterlifeCombatProfile"]!["artTiers"]!.AsObject(), pair => Assert.Equal(0, pair.Value!.GetValue<int>()));
        foreach (var profile in parsed.Profiles["profiles"]!.AsArray())
            Assert.All(profile!["standardArts"]!.AsObject(), pair => Assert.Equal(0, pair.Value!.GetValue<int>()));
        // Adding the third guard must preserve the established pair byte-for-byte, including their creation joins.
        // This cheap independent DATA copy is never a shared context, lease, cache entry or permission.
        var unchangedPair = SpiritualPressureGroupOwnerCaseData.Create(2);
        for (var index = 0; index < 2; index++)
        {
            Assert.Equal(unchangedPair.Parents[index].ToJsonString(), data.Parents[index].ToJsonString());
            Assert.Equal(unchangedPair.Effects[index].ToJsonString(), data.Effects[index].ToJsonString());
            foreach (var field in new[] { "instances", "closures", "sources", "decisions" })
                Assert.Equal(unchangedPair.Receipts[field]![index]!.ToJsonString(), data.Receipts[field]![index]!.ToJsonString());
            Assert.Equal(unchangedPair.History["transitions"]![index]!.ToJsonString(), data.History["transitions"]![index]!.ToJsonString());
            Assert.Equal(unchangedPair.WoundIdentities["entries"]![index]!.ToJsonString(), data.WoundIdentities["entries"]![index]!.ToJsonString());
            Assert.Equal(unchangedPair.EffectIdentities["entries"]![index]!.ToJsonString(), data.EffectIdentities["entries"]![index]!.ToJsonString());
        }
        foreach (var occurrence in parsed.Catalog.Occurrences)
        {
            var wound = occurrence.Wound;
            var index = int.Parse(wound.WoundId["wound_retained_pressure_owner_".Length..], System.Globalization.CultureInfo.InvariantCulture);
            var rank = index == 3 ? 1 : 3;
            Assert.Equal(("active", "spiritual", rank, 40 + index),
                (wound.Lifecycle, wound.Classification.Domain, wound.Severity.Rank, wound.Origin.CreatedAtTurn));
            Assert.Equal(("chaos_sea", "player_soul", "player_soul", WoundCarrierCatalog.AfterlifeProfilesPath),
                (occurrence.Coordinate.Realm, occurrence.Coordinate.OwnerKind, occurrence.Coordinate.OwnerId, occurrence.Coordinate.CarrierPath));
            Assert.True(JsonNode.DeepEquals(data.Parents[index - 1],
                JsonNode.Parse(WoundMaterializationContract.SerializeCanonical(wound))));
            var creation = Assert.Single(parsed.History.ResolveWoundTransitions(wound.WoundId));
            Assert.Equal((index, 1, "create", 40 + index, wound.Origin.EventRef),
                (creation.Ordinal, creation.WoundTransitionOrdinal, creation.Kind, creation.Turn, creation.EventRef));
            Assert.Equal(WoundHistoryState.ComputeNonexistentBeforeFingerprint(wound.WoundId), creation.BeforeFingerprint);
            Assert.Equal(WoundIdentityState.ComputeSemanticFingerprint(wound), creation.AfterFingerprint);
            Assert.Equal(new CanonicalBeforeImage(true, Encoding.UTF8.GetBytes(SpiritualWoundStateJson.Canonical(data.Parents[index - 1]))).Fingerprint,
                creation.OutputFingerprint);
            Assert.True(parsed.Receipts.CoversOriginalCreation(wound.WoundId, wound.Origin.OpportunityId, creation));
            var decision = Assert.Single(parsed.ReceiptRoot["decisions"]!.AsArray(), row => row!["woundId"]!.GetValue<string>() == wound.WoundId)!;
            Assert.Equal((index, rank, creation.TransitionId), (decision["ordinal"]!.GetValue<int>(),
                decision["selectedSeverityRank"]!.GetValue<int>(), decision["transitionId"]!.GetValue<string>()));
            var source = decision["sourceWitness"]!.AsObject();
            var calculation = SpiritualWoundSourceWitness.ValidateAndReadStrainData(source, out var strain);
            Assert.Null(strain);
            Assert.Equal((0, rank, index == 3 ? 10L : 20L, rank), (calculation.PreviousStrainRank,
                calculation.NewStrainRank, calculation.TraumaPressure, calculation.MaximumSeverityRank));
            Assert.Equal(creation.SourceFingerprint, source["sourceFingerprint"]!.GetValue<string>());
            var recordedSource = Assert.Single(parsed.ReceiptRoot["sources"]!.AsArray(), row =>
                row!["witness"]!["sourceId"]!.GetValue<string>() == source["sourceId"]!.GetValue<string>())!;
            Assert.Equal((index, 1), (recordedSource["ordinal"]!.GetValue<int>(), recordedSource["instanceSourceOrdinal"]!.GetValue<int>()));
            Assert.True(JsonNode.DeepEquals(source, recordedSource["witness"]));
            var recorded = parsed.Receipts.FindRecordedConflictSideWound(decision["instanceId"]!.GetValue<string>(), "player");
            Assert.Empty(recorded.Issues);
            Assert.Equal((wound.WoundId, creation.TransitionId), (recorded.Wound!.WoundId, recorded.Wound.CreateTransitionId));
        }
        await AssertPressureTransitionSignedGraphsAsync(context, lease, parsed, trigger);
        return signed;
    }

    private static async Task AssertPressureTransitionSignedGraphsAsync(ResourceMaterializationTestContext context,
        FileSystemManager.CanonicalWriteLease lease, RetainedPressureParentData parents, SpiritualPressureThirdGuardTrigger trigger)
    {
        var loaded = await EffectMechanicsSnapshot.LoadWithDataAsync(context.FileSystem, lease);
        Assert.True(loaded.Snapshot.IsAccepted, string.Join("\n", loaded.Snapshot.Issues));
        var identities = Assert.IsType<EffectIdentityState>(loaded.Identities);
        Assert.Equal(3, loaded.Catalog.Occurrences.Count);
        var conflict = JsonNode.Parse((await context.FileSystem.ReadFileAsync(lease, AfterlifeSpiritualConflictState.StatePath))!)!.AsObject();
        var sources = EffectAcceptedTurnInputComposer.BuildCanonicalWoundSourceAuthority(parents.Catalog);
        Assert.Empty(sources.Issues);
        var targets = EffectAcceptedTurnInputComposer.BuildCanonicalTargetAuthority(
            new(null, null, parents.Carriers.EnemyCombatants, parents.Carriers.AllyCombatants, parents.Profiles, conflict),
            new Dictionary<string, JsonNode?>());
        Assert.Empty(targets.Issues);
        var projection = SpiritualWoundConflictContributionProjector.Project(loaded.Snapshot, sources, targets, conflict);
        Assert.True(projection.IsAccepted, string.Join("\n", projection.Issues));
        Assert.Equal(7, projection.Contributions.Count);
        Assert.Equal(2, projection.Contributions.Count(row => row.Profile == "spiritual_strain_burden" && row.Operation == "pressure"));
        Assert.Equal(2, projection.Contributions.Count(row => row.Profile == "spiritual_roll_hindrance" && row.Operation == "counter"));
        Assert.Equal(3, projection.Contributions.Count(row => row.Profile == "spiritual_action_cost_burden" && row.Operation == "guard"));
        Assert.Equal(5, projection.Contributions.Where(row => row.Profile == "spiritual_action_cost_burden" && row.Operation == "guard")
            .Sum(row => row.Magnitude.GetInt32()));
        foreach (var occurrence in parents.Catalog.Occurrences)
        {
            var wound = occurrence.Wound;
            var third = wound.WoundId == PressureTransitionWoundId;
            var lineage = WoundEffectLineagePlanner.Plan(wound, identities);
            Assert.True(lineage.Success, string.Join("\n", lineage.Issues));
            var root = Assert.Single(wound.Consequences.OwnedEffectSources.RootBindings);
            var definition = JsonNode.Parse(Assert.Single(wound.Consequences.OwnedEffectSources.Definitions).GetRawText())!.AsObject();
            Assert.True(loaded.Catalog.TryResolveOne(root.EffectId, out var effect));
            Assert.Equal(("active", 1, 1), (effect.Effect["state"]!.GetValue<string>(),
                effect.Effect["stacking"]!["currentStacks"]!.GetValue<int>(), effect.Effect["stacking"]!["maxStacks"]!.GetValue<int>()));
            foreach (var field in new[] { "components", "triggers", "links" }) Assert.True(JsonNode.DeepEquals(definition[field], effect.Effect[field]));
            Assert.True(WoundEffectCarrierAdapter.TryCreateTargetKey(wound.Owner, out var target));
            Assert.True(WoundEffectCarrierAdapter.TryCreateCarrierCoordinate(wound.Owner, target, definition, out var carrier));
            Assert.True(WoundAcceptedTurnPlannerCore.PriorRootHasExactGenerationAuthority(root.EffectId,
                new(wound.Owner.Realm, "wound", wound.WoundId, root.DefinitionKey), target, carrier, definition, loaded.Catalog, identities));
            var rows = projection.Contributions.Where(row => row.WoundSource.SourceId == wound.WoundId).ToArray();
            Assert.Equal(third ? 1 : 3, rows.Length);
            Assert.All(rows, row => Assert.Equal(("chaos_sea", "player", "player_soul", "player", 1, root.EffectId),
                (row.Actor.Realm, row.Actor.Kind, row.Actor.TargetId, row.ResolvedSide, row.CurrentStacks, row.EffectId)));
            Assert.Equal(third ? 1 : 2, Assert.Single(rows, row => row.Profile == "spiritual_action_cost_burden" && row.Operation == "guard").Magnitude.GetInt32());
            if (!third)
            {
                Assert.Equal(1, Assert.Single(rows, row => row.Profile == "spiritual_strain_burden" && row.Operation == "pressure").Magnitude.GetInt32());
                Assert.Single(rows, row => row.Profile == "spiritual_roll_hindrance" && row.Operation == "counter");
                continue;
            }
            Assert.Equal(PressureTransitionEffectId, root.EffectId);
            Assert.Equal((1, 1), (wound.Consequences.SlotBudget, wound.Consequences.SlotsUsed));
            Assert.Equal(1, definition["stacking"]!["maxStacks"]!.GetValue<int>());
            Assert.Equal(("source_bound", "active", "expire"), (definition["lifetime"]!["mode"]!.GetValue<string>(),
                definition["lifetime"]!["activePredicate"]!.GetValue<string>(), definition["lifetime"]!["onSourceLoss"]!.GetValue<string>()));
            Assert.Null(effect.Effect["lifetime"]!["remainingUses"]);
            var selectedTrigger = Assert.Single(definition["triggers"]!.AsArray())!;
            var spent = trigger == SpiritualPressureThirdGuardTrigger.OwnApSpent;
            Assert.Equal((spent ? "own_ap_spent" : "on_owner_turn_end", spent ? "resource_spent" : "owner_turn_end", 100, false, "deterministic"),
                (selectedTrigger["triggerId"]!.GetValue<string>(), selectedTrigger["eventType"]!.GetValue<string>(), selectedTrigger["priority"]!.GetValue<int>(),
                    selectedTrigger["consumeUses"]!.GetValue<bool>(), selectedTrigger["resolutionMode"]!.GetValue<string>()));
            Assert.Equal("component_001", Assert.Single(selectedTrigger["componentIds"]!.AsArray())!.GetValue<string>());
            Assert.Single(definition["components"]!.AsArray());
        }
    }
}
