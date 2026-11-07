using System.Text;
using System.Text.Json.Nodes;
using BookOfEternityClient.Services;

namespace BookOfEternityClient.Tests;

/// <summary>Pure guardian fixture DATA audit. Typed production readers own canonical rules; this class joins independent records only.</summary>
internal static class SpiritualGuardianAttributionDataCheck
{
    /// <summary>Checks complete guardian envelopes, source/effect/identity/History/receipt joins and current-shaped participant roots before signing.</summary>
    /// <param name="data">Independent retained singleton DATA, never historical execution evidence.</param>
    /// <param name="profiles">Exact profile image to be installed.</param>
    /// <param name="conflict">Exact current-shaped conflict image to be installed.</param>
    /// <param name="expectedRank">Independent expected III/IV, never inferred from actual DATA.</param>
    /// <returns>All typed-reader and fixture-join diagnostics without issuing current authority.</returns>
    internal static IReadOnlyList<ValidationIssue> Check(RetainedPressureGroupData data, JsonObject profiles, JsonObject conflict, int expectedRank = 3)
    {
        if (expectedRank is not (3 or 4)) throw new ArgumentOutOfRangeException(nameof(expectedRank));
        var issues = new List<ValidationIssue>();
        var carriers = new EffectCarrierCatalogInput(null, null, null, null, profiles, conflict);
        var wounds = WoundCarrierCatalog.Build(new(null, null, null, null, profiles));
        var effects = EffectCarrierCatalog.Build(carriers);
        issues.AddRange(wounds.Issues); issues.AddRange(effects.Issues);
        var roots = Roots(data, profiles, conflict);
        var sources = EffectAcceptedTurnInputComposer.BuildCanonicalSourceAuthority(roots);
        var targets = EffectAcceptedTurnInputComposer.BuildCanonicalTargetAuthority(carriers, roots);
        issues.AddRange(sources.Issues); issues.AddRange(targets.Issues);
        issues.AddRange(EffectAcceptedTurnInputComposer.ValidateRegisteredSourceOwners(roots));
        issues.AddRange(targets.ValidateNamedCombatantBindings(carriers));
        var snapshot = EffectMechanicsSnapshot.Build(effects, data.EffectIdentities, null, out var effectIdentity);
        issues.AddRange(snapshot.Issues);
        var identity = WoundIdentityState.Parse(data.WoundIdentities.ToJsonString(), WoundIdentityState.StatePath);
        var history = WoundHistoryState.Parse(data.History.ToJsonString(), WoundHistoryState.HistoryPath);
        var receipts = SpiritualWoundOpportunityReceiptState.Parse(data.Receipts.ToJsonString(), SpiritualWoundOpportunityReceiptState.StatePath);
        issues.AddRange(identity.Issues); issues.AddRange(history.Issues); issues.AddRange(receipts.Issues);
        if (issues.Count != 0 || identity.State is null || history.State is null || receipts.State is null || effectIdentity is null)
            return issues;
        issues.AddRange(history.State.ValidateAgreement(identity.State, wounds));
        ValidationService.ValidateCanonicalEffectBindings(effects, sources, targets, issues);
        var projection = SpiritualWoundConflictContributionProjector.Project(snapshot, sources, targets, conflict);
        issues.AddRange(projection.Issues);
        Join(data.Parents.Count == 1 && data.Effects.Count == 1 && wounds.Occurrences.Count == 1 && effects.Occurrences.Count == 1 &&
            identity.State.Entries.Count == 1 && effectIdentity.Entries.Count == 1, "inventory", "singleton parent/root/identity bijection", issues);
        if (wounds.Occurrences.Count != 1 || effects.Occurrences.Count != 1 || data.Parents.Count != 1 || data.Effects.Count != 1) return issues;
        var wound = wounds.Occurrences.Single().Wound;
        var effect = effects.Occurrences.Single();
        Join(JsonNode.DeepEquals(data.Parents.Single(), JsonNode.Parse(WoundMaterializationContract.SerializeCanonical(wound))),
            "parent", "complete canonical envelope equality", issues);
        Join(JsonNode.DeepEquals(data.Effects.Single(), effect.Effect), "effect", "exact authored canonical root", issues);
        Join((wound.Owner.Realm, wound.Owner.OwnerKind, wound.Owner.OwnerId, wound.Owner.CarrierPath, wound.Lifecycle,
            wound.Classification.Domain, wound.Severity.Rank) == ("chaos_sea", "guardian", Text(conflict["activeConflict"]?["oppositionSide"]?["leadContestant"]?["actorId"]),
            WoundCarrierCatalog.AfterlifeProfilesPath, "active", "spiritual", expectedRank), "parent.owner", "active guardian-owned spiritual expected III/IV", issues);
        Join(wound.Consequences.OwnedEffectSources.RootBindings.Count == 1 && wound.Consequences.OwnedEffectSources.Definitions.Count == 1,
            "parent.sources", "one full shared root definition", issues);
        var root = wound.Consequences.OwnedEffectSources.RootBindings.Single();
        var definition = JsonNode.Parse(wound.Consequences.OwnedEffectSources.Definitions.Single().GetRawText())!.AsObject();
        var lineage = WoundEffectLineagePlanner.Plan(wound, effectIdentity); issues.AddRange(lineage.Issues);
        Join(lineage.Success && effects.TryResolveOne(root.EffectId, out _), "lineage", "one exact canonical wound/effect lineage", issues);
        Join(WoundEffectCarrierAdapter.TryCreateTargetKey(wound.Owner, out var target) &&
            WoundEffectCarrierAdapter.TryCreateCarrierCoordinate(wound.Owner, target, definition, out var carrier) &&
            WoundAcceptedTurnPlannerCore.PriorRootHasExactGenerationAuthority(root.EffectId,
                new(wound.Owner.Realm, "wound", wound.WoundId, root.DefinitionKey), target, carrier, definition, effects, effectIdentity),
            "effect.base", "complete source/definition/target/carrier/identity equality", issues);
        Join(Text(effect.Effect["source"]?["sourceId"]) == wound.WoundId && Text(effect.Effect["source"]?["definitionKey"]) == root.DefinitionKey &&
            Text(effect.Effect["target"]?["kind"]) == "guardian" && Text(effect.Effect["target"]?["targetId"]) == wound.Owner.OwnerId &&
            Text(effect.Effect["state"]) == "active" && effect.Effect["stacking"]?["currentStacks"]?.GetValue<int>() == 1 &&
            effect.Effect["stacking"]?["maxStacks"]?.GetValue<int>() == 1, "effect", "available magnitude-one stack-one guardian root", issues);
        Join(effect.Effect["chronology"]?["createdAtTurn"]?.GetValue<int>() == wound.Origin.CreatedAtTurn &&
            Text(effect.Effect["chronology"]?["createdEventRef"]) == wound.Origin.EventRef &&
            effect.Effect["chronology"]?["lastTransitionTurn"]?.GetValue<int>() == wound.LastTransition.Turn,
            "effect.chronology", "parent/root chronology agreement", issues);
        Join(projection.Contributions.Count == expectedRank && projection.Contributions.All(row =>
            (row.Actor.Kind, row.Actor.TargetId, row.ResolvedSide, row.CurrentStacks, row.EffectId, row.WoundSource.SourceId) ==
            ("guardian", wound.Owner.OwnerId, "opposition", 1, root.EffectId, wound.WoundId)),
            "contributions", "all expected components on the exact opposition guardian", issues);
        var selected = projection.Contributions.Where(row => row.Profile == "spiritual_strain_burden" && row.Operation == "pressure").ToArray();
        Join(selected.Length == 1 && selected[0].Magnitude.GetInt32() == 1 && selected[0].ResolvedAxis == "oppositionSideStrain",
            "contributions.selected", "one magnitude-one pressure strain component", issues);
        var parentJson = data.Parents.Single();
        var rankName = expectedRank == 4 ? "IV" : "III";
        Join(Text(parentJson["severity"]?["value"]) == rankName && Text(parentJson["severity"]?["maximumAtCreation"]) == rankName &&
            parentJson["recovery"]?["currentStepThreshold"]?.GetValue<int>() == 2 * expectedRank &&
            parentJson["consequences"]?["slotBudget"]?.GetValue<int>() == expectedRank &&
            parentJson["consequences"]?["slotsUsed"]?.GetValue<int>() == expectedRank &&
            parentJson["consequences"]?["entries"] is JsonArray entries && entries.Count == expectedRank &&
            definition["components"] is JsonArray components && components.Count == expectedRank &&
            definition["triggers"]?[0]?["componentIds"] is JsonArray triggered && triggered.Count == expectedRank &&
            triggered.Select(Text).SequenceEqual(Enumerable.Range(1, expectedRank).Select(i => "component_00" + i)),
            "parent.rankGraph", "exact severity/value/creation maximum/recovery/slots/components/triggers", issues);
        var siblingRows = projection.Contributions.Where(row => row.Profile != "spiritual_strain_burden").ToArray();
        Join(siblingRows.Length == expectedRank - 1 && siblingRows.Count(row => row.ComponentId == "component_002" &&
            row.Profile == "spiritual_roll_hindrance" && row.Operation == "counter" && row.Magnitude.GetString() == "disadvantage") == 1 &&
            siblingRows.Count(row => row.ComponentId == "component_003" && row.Profile == "spiritual_action_cost_burden" &&
                row.Operation == "guard" && row.Magnitude.GetInt32() == (expectedRank == 4 ? 3 : 2)) == 1 &&
            (expectedRank == 3 || siblingRows.Count(row => row.ComponentId == "component_004" &&
                row.Profile == "spiritual_action_cost_burden" && row.Operation == "counter" && row.Magnitude.GetInt32() == 3) == 1),
            "parent.siblings", "independent full severity-specific siblings remain installed", issues);
        var creations = history.State.ResolveWoundTransitions(wound.WoundId).ToArray();
        Join(creations.Length == 1 && receipts.State.HasExactStrainHistory(history.State), "History", "exact first-create History", issues);
        if (creations.Length != 1) return issues;
        var creation = creations.Single();
        Join(creation.Kind == "create" && creation.WoundTransitionOrdinal == 1 && creation.TransitionId == wound.LastTransition.TransitionId &&
            creation.Turn == wound.Origin.CreatedAtTurn && creation.EventRef == wound.Origin.EventRef && creation.AttemptId is null &&
            creation.BeforeFingerprint == WoundHistoryState.ComputeNonexistentBeforeFingerprint(wound.WoundId) &&
            creation.AfterFingerprint == WoundIdentityState.ComputeSemanticFingerprint(wound) && creation.OutputFingerprint == Fingerprint(data.Parents.Single()),
            "History.creation", "complete nonexistent-to-exact-parent creation join", issues);
        foreach (var field in new[] { "instances", "closures", "sources", "decisions" })
            Join(data.Receipts[field] is JsonArray { Count: 1 }, "receipt." + field, "one complete retained lifecycle row", issues);
        var decision = data.Receipts["decisions"]![0]!.AsObject();
        var source = decision["sourceWitness"]!.AsObject();
        var sourceRow = data.Receipts["sources"]![0]!;
        var instance = data.Receipts["instances"]![0]!;
        var calculation = SpiritualWoundSourceWitness.ValidateAndReadStrainData(source, out var strain);
        Join(strain is null && calculation.MaximumSeverityRank == expectedRank &&
            calculation.PreviousStrainRank == 0 && calculation.NewStrainRank == expectedRank &&
            calculation.TraumaPressure == (expectedRank == 4 ? 25L : 20L) && Text(source["affectedSide"]) == "opposition" &&
            Text(source["actingActor"]?["actorKind"]) == "player_soul" && Text(source["actingActor"]?["actorId"]) == "player_soul" &&
            Text(source["affectedActor"]?["actorKind"]) == "guardian" && Text(source["affectedActor"]?["actorId"]) == wound.Owner.OwnerId &&
            Text(source["appliedArtId"]) == wound.Origin.SourceId && creation.SourceFingerprint == Text(source["sourceFingerprint"]) &&
            JsonNode.DeepEquals(sourceRow["witness"], source) && Text(sourceRow["instanceId"]) == Text(decision["instanceId"]) &&
            Text(instance["instanceId"]) == Text(decision["instanceId"]) && Text(instance["displayConflictId"]) == Text(source["conflictId"]),
            "receipt.source", "player-harm/guardian-target/full witness/History/instance join", issues);
        foreach (var field in new[] { "sessionId", "requestId", "snapshotToken", "turn" })
            Join(JsonNode.DeepEquals(source["turnEvidence"]![field], decision["binding"]![field]), "receipt.binding." + field, "exact source turn binding", issues);
        var key = SpiritualWoundSourceWitness.HarmEventKey(Text(source["turnEvidence"]!["sessionId"]), Text(source["turnEvidence"]!["requestId"]),
            Text(source["turnEvidence"]!["snapshotToken"]), Text(source["conflictId"]), Text(source["exchangeId"]), "opposition");
        Join(wound.Origin.EventRef == "spiritual_wound_event_" + key && wound.Origin.OpportunityId == "spiritual_wound_" + key &&
            Text(decision["decision"]) == "materialize" && decision["selectedSeverityRank"]?.GetValue<int>() == expectedRank &&
            Text(decision["transitionId"]) == creation.TransitionId && receipts.State.CoversOriginalCreation(wound.WoundId, wound.Origin.OpportunityId, creation),
            "receipt.creation", "opposition event/opportunity/materialize/rank/create joins", issues);
        var recorded = receipts.State.FindRecordedConflictSideWound(Text(instance["instanceId"]), "opposition");
        issues.AddRange(recorded.Issues);
        Join(recorded.Wound?.WoundId == wound.WoundId && recorded.Wound.CreateTransitionId == creation.TransitionId,
            "receipt.recorded", "exact guardian conflict-side wound", issues);
        var active = conflict["activeConflict"]!;
        Join(Text(active["playerSide"]?["leadContestant"]?["actorType"]) == "player" &&
            Text(active["playerSide"]?["leadContestant"]?["actorId"]) == "player_soul" &&
            Text(active["oppositionSide"]?["leadContestant"]?["actorType"]) == "guardian" &&
            Text(active["oppositionSide"]?["leadContestant"]?["actorId"]) == wound.Owner.OwnerId,
            "participants", "exact current-shaped player and opposition guardian leads", issues);
        return issues;
    }

    /// <summary>Runs the same full DATA audit against independent expected III/IV before writing or signing retained roots.</summary>
    /// <param name="data">Independent retained singleton graph and ledgers.</param>
    /// <param name="profiles">Exact complete profile carrier image to install.</param>
    /// <param name="conflict">Exact current-shaped conflict and lead identities.</param>
    /// <param name="expectedRank">Independent expected III/IV fixed by the caller before signing.</param>
    internal static void Require(RetainedPressureGroupData data, JsonObject profiles, JsonObject conflict, int expectedRank = 3)
    {
        var issues = Check(data, profiles, conflict, expectedRank);
        if (issues.Count != 0) throw new InvalidDataException(string.Join("\n", issues));
    }

    /// <summary>Supplies full independent canonical roots to existing source and target composers.</summary>
    internal static IReadOnlyDictionary<string, JsonNode?> Roots(RetainedPressureGroupData data, JsonObject profiles, JsonObject conflict) =>
        new Dictionary<string, JsonNode?>(StringComparer.Ordinal)
        {
            [AfterlifeEntityProfileState.StatePath] = profiles, [AfterlifeSpiritualConflictState.StatePath] = conflict,
            [WoundIdentityState.StatePath] = data.WoundIdentities, [WoundHistoryState.HistoryPath] = data.History,
            [EffectIdentityState.StatePath] = data.EffectIdentities, [SpiritualWoundOpportunityReceiptState.StatePath] = data.Receipts
        };

    private static string Text(JsonNode? node) => node?.GetValue<string>() ?? "";
    private static string Fingerprint(JsonNode node) => new CanonicalBeforeImage(true, Encoding.UTF8.GetBytes(SpiritualWoundStateJson.Canonical(node))).Fingerprint;
    private static void Join(bool value, string path, string expected, List<ValidationIssue> issues)
    {
        if (!value) issues.Add(new ValidationIssue("guardianFixture." + path, IssueSeverity.Error, "Guardian fixture DATA do not join.",
            code: "guardian_fixture_join_mismatch", section: "guardian_attribution_test_data", expected: expected, actual: "mismatch"));
    }
}
