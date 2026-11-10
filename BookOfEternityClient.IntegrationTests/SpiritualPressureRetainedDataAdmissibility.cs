using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using BookOfEternityClient.Services;

namespace BookOfEternityClient.Tests;

/// <summary>Names the first rejected DATA gate. No value grants live source, signing or publication authority.</summary>
internal enum RetainedPressureDataGate
{
    Catalog, Authority, EffectIdentityShape, EffectIndexAgreement, LedgerShape,
    HistoryAgreement, CanonicalBinding, RetainedJoins, Accepted
}

/// <summary>Detached diagnostics for one pure DATA check, not an authority or cached permission.</summary>
internal sealed record RetainedPressureDataCheck(
    RetainedPressureDataGate Gate, bool ReachedCanonicalBinding, IReadOnlyList<ValidationIssue> Issues)
{
    /// <summary>True only after every DATA gate and retained join returned without issues.</summary>
    internal bool IsValid => Gate == RetainedPressureDataGate.Accepted && Issues.Count == 0;
}

/// <summary>
/// Pure, operation-local pre-sign DATA check shared by cheap tests and the retained installer.
/// Existing typed validators own every canonical rule. The additional checks only join this
/// retained fixture's independent parent, effect, first-create History and receipt records.
/// It never writes, normalizes, repairs, reseals, signs or retains mutable input across calls.
/// </summary>
internal static class SpiritualPressureRetainedDataAdmissibility
{
    /// <summary>Checks the actual in-memory profile image before its first write or signature.</summary>
    internal static void Require(RetainedPressureGroupData data, JsonObject profiles,
        IReadOnlyDictionary<string, JsonNode?>? sourceRoots = null,
        EffectCarrierCatalogInput? carriers = null)
    {
        var result = Check(data, profiles, sourceRoots, carriers);
        if (!result.IsValid)
            throw new InvalidDataException($"Retained pressure DATA rejected at {result.Gate}:\n" +
                string.Join("\n", result.Issues.Select(issue => $"{issue.Code} at {issue.FilePath}: {issue.Message}")));
    }

    /// <summary>
    /// Uses complete canonical source composition, including built-ins, and exact caller roots.
    /// Defaults are only the minimal pure DATA contour, not a model of fresh live-world authority.
    /// All gates preceding CanonicalBinding must succeed before that production API is invoked.
    /// </summary>
    internal static RetainedPressureDataCheck Check(RetainedPressureGroupData data, JsonObject profiles,
        IReadOnlyDictionary<string, JsonNode?>? sourceRoots = null,
        EffectCarrierCatalogInput? carriers = null)
    {
        ArgumentNullException.ThrowIfNull(data);
        ArgumentNullException.ThrowIfNull(profiles);
        carriers ??= new(null, null, null, null, profiles, null);
        sourceRoots ??= CreateSourceRoots(data, profiles);
        var issues = new List<ValidationIssue>();
        var catalog = EffectCarrierCatalog.Build(carriers);
        var wounds = WoundCarrierCatalog.Build(new(
            ReadRoot(sourceRoots, WoundCarrierCatalog.PlayerPath),
            ReadRoot(sourceRoots, WoundCarrierCatalog.NpcPath),
            carriers.EnemyCombatants, carriers.AllyCombatants, profiles));
        issues.AddRange(catalog.Issues);
        issues.AddRange(wounds.Issues);
        if (issues.Count != 0) return Reject(RetainedPressureDataGate.Catalog, issues);

        var sources = EffectAcceptedTurnInputComposer.BuildCanonicalSourceAuthority(sourceRoots);
        var targets = EffectAcceptedTurnInputComposer.BuildCanonicalTargetAuthority(carriers, sourceRoots);
        issues.AddRange(sources.Issues);
        issues.AddRange(EffectAcceptedTurnInputComposer.ValidateRegisteredSourceOwners(sourceRoots));
        issues.AddRange(targets.Issues);
        issues.AddRange(targets.ValidateNamedCombatantBindings(carriers));
        if (issues.Count != 0) return Reject(RetainedPressureDataGate.Authority, issues);

        using var document = JsonDocument.Parse(data.EffectIdentities.ToJsonString());
        var effectIndex = EffectIdentityState.Parse(document.RootElement, EffectIdentityState.StatePath);
        issues.AddRange(effectIndex.Issues);
        if (effectIndex.State is null || issues.Count != 0)
            return Reject(RetainedPressureDataGate.EffectIdentityShape, issues);
        ValidationService.ValidateEffectCarrierIndexAgreement(catalog, effectIndex.State, issues);
        if (issues.Count != 0) return Reject(RetainedPressureDataGate.EffectIndexAgreement, issues);

        var identities = WoundIdentityState.Parse(data.WoundIdentities.ToJsonString(), WoundIdentityState.StatePath);
        var history = WoundHistoryState.Parse(data.History.ToJsonString(), WoundHistoryState.HistoryPath);
        var receipts = SpiritualWoundOpportunityReceiptState.Parse(data.Receipts.ToJsonString(),
            SpiritualWoundOpportunityReceiptState.StatePath);
        issues.AddRange(identities.Issues);
        issues.AddRange(history.Issues);
        issues.AddRange(receipts.Issues);
        if (!identities.IsValid || !history.IsValid || !receipts.IsValid)
            return Reject(RetainedPressureDataGate.LedgerShape, issues);
        issues.AddRange(history.State!.ValidateAgreement(identities.State!, wounds));
        Join(receipts.State!.HasExactStrainHistory(history.State), WoundHistoryState.HistoryPath,
            "complete exact strain History", issues);
        if (issues.Count != 0) return Reject(RetainedPressureDataGate.HistoryAgreement, issues);

        ValidationService.ValidateCanonicalEffectBindings(catalog, sources, targets, issues);
        if (issues.Count != 0) return Reject(RetainedPressureDataGate.CanonicalBinding, issues, reachedBinding: true);
        CheckRetainedJoins(data, profiles, carriers, sourceRoots, wounds, catalog, effectIndex.State,
            identities.State!, history.State, receipts.State, issues);
        return issues.Count == 0
            ? new(RetainedPressureDataGate.Accepted, true, Array.Empty<ValidationIssue>())
            : Reject(RetainedPressureDataGate.RetainedJoins, issues, reachedBinding: true);
    }

    /// <summary>Authors no game world: one complete player profile containing detached retained DATA.</summary>
    internal static JsonObject CreateProfiles(RetainedPressureGroupData data)
    {
        var player = AfterlifeActorMaterializationTestFixture.CreateCompleteProfile(
            "player_soul", "player_soul", "Chaos Sea", materializedAtTurn: 40);
        player.Remove(ActorMaterializationContract.PropertyName);
        player["activeWounds"] = new JsonArray(data.Parents.Select(row => row.DeepClone()).ToArray());
        player["activeEffects"] = new JsonArray(data.Effects.Select(row => row.DeepClone()).ToArray());
        return new JsonObject { ["schemaVersion"] = AfterlifeEntityProfileState.SchemaVersion,
            [AfterlifeEntityProfileState.ProfilesProperty] = new JsonArray(player) };
    }

    /// <summary>Provides the exact profile and ledger roots for one pure retained DATA check.</summary>
    internal static IReadOnlyDictionary<string, JsonNode?> CreateSourceRoots(
        RetainedPressureGroupData data, JsonObject profiles) => new Dictionary<string, JsonNode?>(StringComparer.Ordinal)
        {
            [AfterlifeEntityProfileState.StatePath] = profiles,
            [WoundIdentityState.StatePath] = data.WoundIdentities,
            [WoundHistoryState.HistoryPath] = data.History,
            [EffectIdentityState.StatePath] = data.EffectIdentities,
            [SpiritualWoundOpportunityReceiptState.StatePath] = data.Receipts
        };

    private static void CheckRetainedJoins(RetainedPressureGroupData data, JsonObject profiles,
        EffectCarrierCatalogInput carriers, IReadOnlyDictionary<string, JsonNode?> roots,
        WoundCarrierCatalog wounds, EffectCarrierCatalog effects, EffectIdentityState effectIndex,
        WoundIdentityState identities, WoundHistoryState history, SpiritualWoundOpportunityReceiptState receipts,
        List<ValidationIssue> issues)
    {
        var count = data.Parents.Count;
        Join(count > 0 && data.Effects.Count == count && wounds.Occurrences.Count == count &&
            effects.Occurrences.Count == count && identities.Entries.Count == count && effectIndex.Entries.Count == count,
            "retainedPressure.inventory", "one distinct active wound/root/identity per retained parent", issues);
        var authoredWoundIds = data.Parents.Select(parent => Text(parent["woundId"])).ToHashSet(StringComparer.Ordinal);
        var authoredEffectIds = data.Effects.Select(effect => Text(effect["effectId"])).ToHashSet(StringComparer.Ordinal);
        Join(authoredWoundIds.Count == data.Parents.Count &&
            authoredWoundIds.SetEquals(wounds.Occurrences.Select(occurrence => occurrence.WoundId)),
            "retainedPressure.inventory.parents", "distinct authored wound IDs biject with actual catalog occurrences", issues);
        Join(authoredEffectIds.Count == data.Effects.Count &&
            authoredEffectIds.SetEquals(effects.Occurrences.Select(occurrence => occurrence.EffectId)),
            "retainedPressure.inventory.effects", "distinct authored effect IDs biject with actual catalog occurrences", issues);
        Join(JsonNode.DeepEquals(profiles, carriers.AfterlifeProfiles) &&
            JsonNode.DeepEquals(profiles, ReadRoot(roots, AfterlifeEntityProfileState.StatePath)),
            AfterlifeEntityProfileState.StatePath, "same complete source and target carrier profile image", issues);
        foreach (var (path, expected) in new[]
        {
            (WoundIdentityState.StatePath, data.WoundIdentities), (WoundHistoryState.HistoryPath, data.History),
            (EffectIdentityState.StatePath, data.EffectIdentities),
            (SpiritualWoundOpportunityReceiptState.StatePath, data.Receipts)
        })
            Join(roots.TryGetValue(path, out var actual) && JsonNode.DeepEquals(actual, expected), path,
                "same exact independent retained ledger image", issues);
        foreach (var field in new[] { "instances", "closures", "sources", "decisions" })
            Join(data.Receipts[field] is JsonArray rows && rows.Count == count,
                SpiritualWoundOpportunityReceiptState.StatePath + "." + field,
                "one complete immutable recorded-provenance row per parent", issues);
        Join(!receipts.HasRetainedPressureComparisons, SpiritualWoundOpportunityReceiptState.StatePath,
            "first-create retained scaffold, no actual-pressure comparison already appended", issues);
        Join(data.History["transitions"] is JsonArray transitions && transitions.Count == count,
            WoundHistoryState.HistoryPath + ".transitions", "one first-create transition per parent", issues);

        foreach (var parent in data.Parents)
        {
            var id = Text(parent["woundId"]);
            if (!wounds.TryResolveOne(id, out var occurrence))
            {
                Join(false, "retainedPressure.parents." + id, "one exact canonical parent occurrence", issues);
                continue;
            }
            var wound = occurrence.Wound;
            var path = occurrence.JsonPath;
            Join(JsonNode.DeepEquals(parent, JsonNode.Parse(WoundMaterializationContract.SerializeCanonical(wound))),
                path, "unchanged complete canonical parent envelope", issues);
            Join(wound.Lifecycle == "active" && wound.Classification.Domain == "spiritual" &&
                wound.Owner.Realm == "chaos_sea" && wound.Owner.OwnerKind == "player_soul" &&
                wound.Owner.OwnerId == "player_soul" && wound.Owner.CarrierPath == WoundCarrierCatalog.AfterlifeProfilesPath,
                path + ".owner", "active spiritual player_soul on its exact Chaos Sea profile carrier", issues);
            var bindings = wound.Consequences.OwnedEffectSources.RootBindings;
            var definitions = wound.Consequences.OwnedEffectSources.Definitions;
            Join(bindings.Count == 1 && definitions.Count == 1, path + ".consequences.ownedEffectSources",
                "one complete definition and bound independent root", issues);
            if (bindings.Count != 1 || definitions.Count != 1) continue;
            var rootResolved = effects.TryResolveOne(bindings[0].EffectId, out var effect);
            Join(rootResolved, path + ".consequences.ownedEffectSources.rootBindings",
                "the exact declared root effect resolves once on its active carrier", issues);
            if (!rootResolved) continue;
            var authoredEffects = data.Effects.Where(row => Text(row["effectId"]) == effect.EffectId).ToArray();
            Join(authoredEffects.Length == 1 && JsonNode.DeepEquals(authoredEffects[0], effect.Effect),
                effect.JsonPath, "exact authored root, not a reconstructed effect oracle", issues);
            var lineage = WoundEffectLineagePlanner.Plan(wound, effectIndex);
            issues.AddRange(lineage.Issues);
            Join(lineage.Success, path + ".consequences.ownedEffectSources.rootBindings",
                "complete existing typed wound/effect lineage", issues);
            Join(Text(effect.Effect["source"]?["kind"]) == "wound" &&
                Text(effect.Effect["source"]?["sourceId"]) == id &&
                Text(effect.Effect["source"]?["definitionKey"]) == bindings[0].DefinitionKey,
                effect.JsonPath + ".source", "exact parent source and root definition key", issues);
            Join(effect.Effect["chronology"]?["createdAtTurn"]?.GetValue<int>() == wound.Origin.CreatedAtTurn &&
                Text(effect.Effect["chronology"]?["createdEventRef"]) == wound.Origin.EventRef &&
                effect.Effect["chronology"]?["lastTransitionTurn"]?.GetValue<int>() == wound.LastTransition.Turn,
                effect.JsonPath + ".chronology", "root creation and last transition match the retained parent", issues);

            var creations = history.ResolveWoundTransitions(id).ToArray();
            Join(creations.Length == 1, path + ".lastTransition", "one immutable first-create History row", issues);
            if (creations.Length != 1) continue;
            var creation = creations[0];
            Join(creation.Kind == "create" && creation.WoundTransitionOrdinal == 1 &&
                creation.Turn == wound.Origin.CreatedAtTurn && creation.EventRef == wound.Origin.EventRef &&
                creation.BeforeFingerprint == WoundHistoryState.ComputeNonexistentBeforeFingerprint(id) &&
                creation.AfterFingerprint == WoundIdentityState.ComputeSemanticFingerprint(wound) &&
                creation.OutputFingerprint == Fingerprint(parent) && creation.AttemptId is null,
                WoundHistoryState.HistoryPath + ".transitions[" + (creation.Ordinal - 1) + "]",
                "complete non-existent → exact canonical parent first-create evidence", issues);
            Join(receipts.CoversOriginalCreation(id, wound.Origin.OpportunityId, creation),
                path + ".origin.opportunityId", "one exact original-creation decision", issues);
            var decisions = data.Receipts["decisions"]!.AsArray().OfType<JsonObject>()
                .Where(row => Text(row["woundId"]) == id).ToArray();
            if (decisions.Length != 1)
            {
                Join(false, path + ".origin", "one recorded materialization decision for the parent", issues);
                continue;
            }
            var decision = decisions[0];
            var source = decision["sourceWitness"]!.AsObject();
            var calculation = SpiritualWoundSourceWitness.ValidateAndReadStrainData(source, out var strain);
            var originalSources = data.Receipts["sources"]!.AsArray().OfType<JsonObject>()
                .Where(row => Text(row["witness"]?["sourceId"]) == Text(source["sourceId"])).ToArray();
            Join(strain is null && calculation.MaximumSeverityRank == wound.Severity.Rank &&
                decision["selectedSeverityRank"]!.GetValue<int>() == wound.Severity.Rank &&
                creation.SourceFingerprint == Text(source["sourceFingerprint"]) &&
                wound.Origin.SourceId == Text(source["appliedArtId"]) &&
                Text(source["affectedSide"]) == "player" && originalSources.Length == 1 &&
                JsonNode.DeepEquals(originalSources[0]["witness"], source) &&
                Text(originalSources[0]["instanceId"]) == Text(decision["instanceId"]),
                SpiritualWoundOpportunityReceiptState.StatePath + ".decisions[" +
                (decision["ordinal"]!.GetValue<int>() - 1) + "]",
                "full independent source witness / rank / History / instance join", issues);
            var recorded = receipts.FindRecordedConflictSideWound(Text(decision["instanceId"]), "player");
            issues.AddRange(recorded.Issues);
            Join(recorded.Wound?.WoundId == id && recorded.Wound.CreateTransitionId == creation.TransitionId,
                path + ".origin", "exact recorded conflict-side wound and first-create transition", issues);
        }
    }

    private static RetainedPressureDataCheck Reject(RetainedPressureDataGate gate,
        List<ValidationIssue> issues, bool reachedBinding = false) => new(gate, reachedBinding, issues.ToArray());

    private static JsonObject? ReadRoot(IReadOnlyDictionary<string, JsonNode?> roots, string path) =>
        roots.TryGetValue(path, out var value) ? value as JsonObject : null;

    private static string Text(JsonNode? value) => value?.GetValue<string>() ?? "";

    private static string Fingerprint(JsonNode value) => new CanonicalBeforeImage(true,
        Encoding.UTF8.GetBytes(SpiritualWoundStateJson.Canonical(value))).Fingerprint;

    private static void Join(bool valid, string path, string expected, List<ValidationIssue> issues)
    {
        if (!valid) issues.Add(new ValidationIssue(path, IssueSeverity.Error,
            "Retained pressure DATA records do not join.", code: "retained_pressure_data_join_mismatch",
            section: "retained_pressure_test_data", expected: expected, actual: "mismatch"));
    }
}
