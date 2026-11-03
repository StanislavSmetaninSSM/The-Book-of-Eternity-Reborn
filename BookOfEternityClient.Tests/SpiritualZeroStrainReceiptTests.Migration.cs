using System.Text.Json;
using System.Text.Json.Nodes;
using BookOfEternityClient.Models;
using BookOfEternityClient.Services;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed partial class SpiritualZeroStrainReceiptTests
{
    /// <summary>Appends v3 comparison DATA to independently valid nonempty v2 blocked-healing DATA, without treatment or authority.</summary>
    [Fact]
    public void ZeroComparisonPreservesExistingV2HealingFamily()
    {
        var (baseline, next) = CreateLedger("prior-injury");
        var sourceWound = WoundMaterializationContract.Parse(WoundContractTestData.CreateSpiritualActiveWound(
            "wound_migration_healing", "chaos_sea", "player_soul", "player_soul", WoundCarrierCatalog.AfterlifeProfilesPath)
            .ToJsonString(), "unit.healingBefore");
        Assert.True(sourceWound.IsValid); var wound = sourceWound.Wound!;
        const string request = "request-healing42", snapshot = "snapshot-healing42", exchangeId = "exchange_healing42";
        var fingerprint = "sha256:" + new string('a', 64);
        var selection = new SpiritualSelfTreatmentSelection(wound.WoundId, WoundIdentityState.ComputeSemanticFingerprint(wound))
            { Combat = new("conflict-a", fingerprint, 0) };
        var files = AfterlifeWoundTreatmentAuthority.CombatSourcePaths.Select(path =>
            new AfterlifeWoundTreatmentAuthority.SourceFile(path, new(false, null), null)).ToArray();
        var intent = SpiritualCombatHealingIntent.Create("session-a", request, 42, selection, 3,
            JsonSerializer.SerializeToNode(new ProgressionControl())!.AsObject(), files);
        var source = new SpiritualHealingPreparedSource(wound.WoundId, wound.Owner, wound.Owner, wound.Severity.Rank,
            3, wound.Complications.Where(row => row.State == "active").Sum(row => (long)row.TreatmentDifficultyModifier), "normal", []);
        var resolution = SpiritualHealingResolver.ResolvePrepared(source, "normal", [8]);
        Assert.True(resolution.IsValid); Assert.NotNull(resolution.Resolution);
        var exchange = new JsonObject { ["exchangeId"] = exchangeId, ["operationType"] = "heal_spiritual_wound", ["outcome"] = "blocked",
            ["diceAudit"] = new JsonObject { ["diceUsed"] = new JsonArray(
                new JsonObject { ["side"] = "player", ["sourceIndex"] = 0, ["value"] = 8 },
                new JsonObject { ["side"] = "opposition", ["sourceIndex"] = 1, ["value"] = 12 }) },
            ["actionCostAudit"] = new JsonObject { ["player"] = new JsonObject { ["baseCost"] = 5, ["minCost"] = 2,
                ["artTier"] = 3, ["effectiveCost"] = 2, ["before"] = 6, ["after"] = 4 } } };
        var coordinate = WoundAcceptedTurnFingerprintWriter.Compute(new[]
            { "book_of_eternity.spiritual_combat_healing.source", "1", snapshot, intent.Fingerprint, exchangeId })[7..];
        var accepted = WoundAcceptedEventAuthorityComposer.ComposeDefaultAcceptedTurn("session-a", request, snapshot, 42);
        Assert.True(accepted.Success);
        var witness = new JsonObject { ["schemaVersion"] = 1, ["kind"] = "spiritual_combat_healing_source",
            ["sourceId"] = "spiritual_combat_healing_source_" + coordinate, ["eventRef"] = accepted.Events.Single().EventRef,
            ["sessionId"] = "session-a", ["requestId"] = request, ["snapshotToken"] = snapshot, ["turn"] = 42,
            ["realm"] = "chaos_sea", ["conflictId"] = "conflict-a", ["exchangeId"] = exchangeId, ["exchangeOrdinal"] = 0,
            ["turnExchangeOrdinal"] = 0, ["intent"] = intent.Root, ["beforeWound"] = JsonNode.Parse(WoundMaterializationContract.SerializeCanonical(wound)),
            ["healingSource"] = JsonSerializer.SerializeToNode(source), ["rollMode"] = "normal",
            ["diceClaim"] = new JsonArray(new JsonObject { ["sourceIndex"] = 0, ["value"] = 8, ["selection"] = null }),
            ["calculation"] = JsonSerializer.SerializeToNode(resolution.Resolution!.Calculation), ["disposition"] = "blocked",
            ["exchange"] = exchange, ["exchangeFingerprint"] = WoundAcceptedTurnFingerprintWriter.Compute(new[]
                { WoundAcceptedTurnFingerprintWriter.CanonicalJson(exchange) }) };
        witness["sourceFingerprint"] = WoundAcceptedTurnFingerprintWriter.Compute(new[]
            { "book_of_eternity.spiritual_combat_healing.witness", "1", WoundAcceptedTurnFingerprintWriter.CanonicalJson(witness) });
        Assert.Equal("blocked", SpiritualCombatHealingPersistedResult.ReadSource(witness).Disposition);
        var binding = new ResourceCapacityBinding(ResourceCapacityKind.InstanceFixed, "capacity_healing_data", fingerprint);
        var spend = new ResourceTransition("transition_healing_data", "operation_healing_data", accepted.Events.Single().EventRef,
            "afterlife_cost", exchangeId, ResourceMutationPhase.DirectCost, 0, 0,
            new("chaos_sea", ResourceOwnerKind.AfterlifeActor, "player_soul", "spiritual_action_points"), ResourceTransitionOperation.Spend,
            2, 2, ResourceTransitionOutcome.Applied, null, new(6, 6, binding, ResourceLifecycleState.Active),
            new(4, 6, binding, ResourceLifecycleState.Active), new("afterlife_cost", exchangeId, fingerprint), fingerprint, null, 42);
        var outcome = new JsonObject { ["sourceId"] = witness["sourceId"]!.DeepClone(), ["sourceOrdinal"] = 0, ["waveOrdinal"] = 0,
            ["disposition"] = "blocked", ["resourceTransition"] = JsonSerializer.SerializeToNode(spend), ["result"] = null, ["decisionFingerprint"] = "" };
        outcome["decisionFingerprint"] = SpiritualWoundStateJson.Hash(outcome, "staged_combat_healing", "decisionFingerprint");
        var evidence = baseline["sources"]![0]!["witness"]!["turnEvidence"]!.DeepClone();
        evidence["requestId"] = request; evidence["snapshotToken"] = snapshot; evidence["acceptedD20Values"] = new JsonArray(8, 12);
        evidence["bounds"]!["exchangeCount"] = 1; evidence["bounds"]!["sourceSlotCount"] = 2; evidence["bounds"]!["diceCount"] = 2;
        var claims = new JsonArray();
        foreach (var (index, value) in new[] { (0, 8), (1, 12) })
        {
            var claim = new JsonObject { ["sourceIndex"] = index, ["value"] = value, ["exchangeOrdinal"] = 0, ["claimFingerprint"] = "" };
            claim["claimFingerprint"] = SpiritualWoundTurnEvidence.ComputeClaimFingerprint(claim); claims.Add(claim);
        }
        evidence["diceClaims"] = claims;
        SpiritualWoundDecisionPendingState.ValidateHealingDecision(outcome,
            new JsonObject { ["sourceOrdinal"] = 0, ["turnEvidence"] = evidence.DeepClone(), ["witness"] = witness.DeepClone() });
        baseline["schemaVersion"] = 2; baseline["nextHealingSourceOrdinal"] = 2; baseline["nextHealingDecisionOrdinal"] = 2;
        var instance = baseline["instances"]![0]!["instanceId"]!;
        baseline["healingSources"] = new JsonArray(new JsonObject { ["ordinal"] = 1, ["instanceId"] = instance.DeepClone(),
            ["turnEvidence"] = evidence, ["witness"] = witness });
        baseline["healingDecisions"] = new JsonArray(new JsonObject { ["ordinal"] = 1, ["instanceId"] = instance.DeepClone(), ["outcome"] = outcome });
        var v2 = ParseLedger(baseline); Assert.True(v2.HasCombatHealing);
        foreach (var field in new[] { "healingSources", "healingDecisions", "nextHealingSourceOrdinal", "nextHealingDecisionOrdinal" })
            next[field] = baseline[field]!.DeepClone();
        var v3 = ParseLedger(next); Assert.True(v3.HasCombatHealing);
        Assert.Equal("appended", SpiritualWoundOpportunityReceiptState.PlanAppend(v2, v3).Disposition);
        Assert.Equal("exact_replay", SpiritualWoundOpportunityReceiptState.PlanAppend(v3, v3).Disposition);
        next["healingSources"]![0]!["turnEvidence"]!["originalSnapshotFingerprint"] = "sha256:" + new string('c', 64);
        Assert.Equal("conflict", SpiritualWoundOpportunityReceiptState.PlanAppend(v2, ParseLedger(next)).Disposition);
    }
}
