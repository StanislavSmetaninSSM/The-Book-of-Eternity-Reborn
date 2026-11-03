using System.Text;
using System.Text.Json.Nodes;
using BookOfEternityClient.Services;
using Xunit;

namespace BookOfEternityClient.Tests;

/// <summary>Checks closed existing-parent comparison DATA, never current owner or publication authority.</summary>
public sealed class SpiritualExistingStrainPreimageTests
{
    /// <summary>Allows a signed-existing III or IV parent at the first source of a later request.</summary>
    [Theory]
    [InlineData(3)]
    [InlineData(4)]
    public void ExistingParentDataAcceptsClosedComparisonAtFirstSource(int rank)
    {
        var (source, parent, ordinary) = CreateData(rank);
        var parsed = WoundMaterializationContract.Parse(parent.ToJsonString(), "unit.existingParent");
        Assert.True(parsed.IsValid, string.Join("\n", parsed.Issues));
        var issues = new List<ValidationIssue>();
        ValidationService.ValidateRetainedSpiritualExchangeShape(ordinary, "unit.ordinary", issues);
        ValidationService.ValidateSpiritualWoundSourceActionShape(ordinary, "unit.ordinary", issues);
        ValidationService.ValidateRetainedSpiritualDiceAudit(ordinary, source["turnEvidence"]!.AsObject(), issues);
        ValidationService.ValidateRetainedSpiritualExchangeRules(ordinary, issues);
        Assert.DoesNotContain(issues, row => row.Severity == IssueSeverity.Error);
        var before = source.ToJsonString();
        Assert.Equal(2, SpiritualWoundSourceWitness.Validate(source).MaximumSeverityRank);
        var data = SpiritualStrainPreimageData.ReadIntrinsic(source, ReadActual(source));
        Assert.Equal(rank, data.ParentWound.Severity.Rank);
        Assert.Equal(WoundIdentityState.ComputeSemanticFingerprint(parsed.Wound!), data.ParentSemanticFingerprint);
        Assert.Equal("overwhelmed", data.ReadOrdinaryAfter(ReadActual(source))["playerSideStrain"]!.GetValue<string>());
        Assert.Equal(before, source.ToJsonString());
    }

    /// <summary>Retains every sibling and rejects incomplete, foreign or contradictory intrinsic comparisons.</summary>
    [Theory]
    [InlineData("extra-field")]
    [InlineData("parent-source-id")]
    [InlineData("v1-shape")]
    [InlineData("parent-owner")]
    [InlineData("parent-realm")]
    [InlineData("parent-rank")]
    [InlineData("parent-terminal")]
    [InlineData("effect")]
    [InlineData("component")]
    [InlineData("ordinary-opposition")]
    [InlineData("ordinary-position")]
    [InlineData("ordinary-dice")]
    [InlineData("wrong-step")]
    [InlineData("unchanged-ordinary")]
    public void ExistingParentDataRejectsRehashedContradiction(string change)
    {
        var (source, parent, ordinary) = CreateData(3);
        Assert.Equal(2, SpiritualWoundSourceWitness.Validate(source).MaximumSeverityRank);
        var proof = source["strainPreimage"]!.AsObject();
        switch (change)
        {
            case "extra-field": proof["unowned"] = true; break;
            case "parent-source-id": proof["parentSourceId"] = "spiritual_source_foreign"; break;
            case "v1-shape": proof["version"] = 1; break;
            case "parent-owner": parent["owner"]!["ownerId"] = "foreign-soul"; break;
            case "parent-realm": parent["owner"]!["realm"] = "shining_abode"; break;
            case "parent-rank": parent["severity"]!["rank"] = 2; parent["severity"]!["value"] = "II"; break;
            case "parent-terminal": parent["lifecycle"] = "healed"; break;
            case "effect": proof["effectId"] = "effect_foreign"; break;
            case "component": proof["componentId"] = "component_foreign"; break;
            case "ordinary-opposition": ordinary["after"]!["oppositionSideStrain"] = "strained"; break;
            case "ordinary-position": ordinary["after"]!["conflictPosition"] = "player_advantaged"; break;
            case "ordinary-dice": ordinary["diceAudit"]!["margin"] = 99; break;
            case "wrong-step":
                var wrong = ReadActual(source); wrong["after"]!["playerSideStrain"] = "overwhelmed";
                source["exchangeJsonBase64"] = Encode(wrong); break;
            case "unchanged-ordinary":
                ordinary["after"] = ordinary["before"]!.DeepClone();
                var unchanged = ordinary.DeepClone().AsObject(); unchanged["after"]!["playerSideStrain"] = "broken";
                source["exchangeJsonBase64"] = Encode(unchanged); break;
            default: throw new ArgumentOutOfRangeException(nameof(change));
        }
        proof["parentWoundJsonBase64"] = Encode(parent);
        proof["ordinaryExchangeJsonBase64"] = Encode(ordinary);
        Seal(source);
        var before = source.ToJsonString();
        Assert.Throws<FormatException>(() => SpiritualWoundSourceWitness.Validate(source));
        Assert.Equal(before, source.ToJsonString());
    }

    /// <summary>The bounded existing-parent lane must not authorize another incoming operation.</summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ExistingParentDataRequiresEffectiveIncomingGuard(bool finalOverride)
    {
        var (source, _, _) = CreateData(3);
        Assert.Equal(2, SpiritualWoundSourceWitness.Validate(source).MaximumSeverityRank);
        var actual = ReadActual(source);
        actual["incomingAction"]![finalOverride ? "finalOperationType" : "operationType"] = "pressure";
        var ordinary = JsonNode.Parse(Convert.FromBase64String(
            source["strainPreimage"]!["ordinaryExchangeJsonBase64"]!.GetValue<string>()))!.AsObject();
        ordinary["incomingAction"]![finalOverride ? "finalOperationType" : "operationType"] = "pressure";
        source["strainPreimage"]!["ordinaryExchangeJsonBase64"] = Encode(ordinary);
        var error = Assert.Throws<FormatException>(() => SpiritualStrainPreimageData.ReadIntrinsic(source, actual));
        Assert.Contains("incoming guard", error.Message);
    }

    /// <summary>Exposes independent DATA copies and refuses another actual exchange after a valid read.</summary>
    [Fact]
    public void ExistingParentDataReturnsDetachedOrdinaryWithoutPermission()
    {
        var (source, _, ordinary) = CreateData(3);
        Assert.Equal(2, SpiritualWoundSourceWitness.Validate(source).MaximumSeverityRank);
        var actual = ReadActual(source);
        var retained = SpiritualStrainPreimageData.ReadIntrinsic(source, actual);
        var first = retained.ReadOrdinaryAfter(actual);
        first["playerSideStrain"] = "clear";
        Assert.True(JsonNode.DeepEquals(ordinary["after"], retained.ReadOrdinaryAfter(actual)));
        var changed = actual.DeepClone().AsObject(); changed["after"]!["oppositionSideStrain"] = "broken";
        Assert.Throws<FormatException>(() => retained.ReadOrdinaryAfter(changed));
    }

    /// <summary>Joins the exact latest III/IV parent anchor and preserves it after later lawful healing.</summary>
    [Theory]
    [InlineData(3)]
    [InlineData(4)]
    public void ExistingParentHistoryRetainsLatestAnchorAfterLaterHealing(int rank)
    {
        var (root, parent) = CreateLedger(rank);
        var ledger = ReadLedger(root);
        var history = CreateHistory(parent, rank);
        Assert.True(ledger.HasExactStrainHistory(ReadHistory(history)));
        var last = history["transitions"]!.AsArray().Last()!.AsObject();
        var heal = last.DeepClone().AsObject();
        heal["transitionId"] = "transition_later_heal"; heal["operationKey"] = "operation_later_heal";
        heal["ordinal"] = rank == 3 ? 2 : 3; heal["woundTransitionOrdinal"] = rank == 3 ? 2 : 3;
        heal["kind"] = "heal"; heal["turn"] = 44; heal["eventRef"] = "event_later_heal";
        heal["beforeFingerprint"] = last["afterFingerprint"]!.DeepClone();
        heal["afterFingerprint"] = "sha256:" + new string('c', 64); heal["terminal"] = true;
        history["transitions"]!.AsArray().Add(heal); history["nextOrdinal"] = rank == 3 ? 3 : 4;
        Assert.True(ledger.HasExactStrainHistory(ReadHistory(history)));
        var actual = ReadActual(root["sources"]![1]!["witness"]!.AsObject());
        Assert.NotNull(ledger.ReadRetainedStrainOrdinaryAfter("chaos_sea", "conflict-a", actual, false));
        actual["after"]!["oppositionSideStrain"] = "broken";
        Assert.Null(ledger.ReadRetainedStrainOrdinaryAfter("chaos_sea", "conflict-a", actual, true));
    }

    /// <summary>Rejects a missing or changed exact latest transition, even with an otherwise valid parsed ledger.</summary>
    [Theory]
    [InlineData("missing")]
    [InlineData("identity")]
    [InlineData("kind")]
    [InlineData("turn")]
    [InlineData("fingerprint")]
    public void ExistingParentHistoryRefusesChangedLatestAnchor(string change)
    {
        var (root, parent) = CreateLedger(4);
        var ledger = ReadLedger(root);
        var history = CreateHistory(parent, 4);
        Assert.True(ledger.HasExactStrainHistory(ReadHistory(history)));
        var latest = history["transitions"]![1]!.AsObject();
        switch (change)
        {
            case "missing": history["transitions"]!.AsArray().RemoveAt(1); history["nextOrdinal"] = 2; break;
            case "identity": latest["transitionId"] = "foreign_transition"; break;
            case "kind": latest["kind"] = "complicate"; break;
            case "turn": latest["turn"] = 43; break;
            case "fingerprint": latest["afterFingerprint"] = "sha256:" + new string('d', 64); break;
        }
        Assert.False(ledger.HasExactStrainHistory(ReadHistory(history)));
    }

    /// <summary>Decline-only v2 still requires the actual owner-reconstructed C3 pair, never an arbitrary internal instance.</summary>
    [Fact]
    public void ExistingParentDeclineRefusesMissingOrForgedOwnerPair()
    {
        var (root, _) = CreateLedger(3);
        _ = ReadLedger(root);
        var source = root["sources"]![1]!["witness"]!.DeepClone();
        Assert.Equal(2, SpiritualWoundSourceWitness.Validate(source.AsObject()).MaximumSeverityRank);
        var staged = new JsonObject { ["opportunityRef"] = "opportunity_existing", ["sourceId"] = source["sourceId"]!.DeepClone(),
            ["decisionFingerprint"] = "", ["sourceOrdinal"] = 0, ["waveOrdinal"] = 0, ["decision"] = "none",
            ["selectedSeverityRank"] = null, ["woundDraftBase64"] = null, ["woundDraftFingerprint"] = null };
        staged["decisionFingerprint"] = SpiritualWoundStateJson.Hash(staged, "staged_decision", "decisionFingerprint");
        var packet = new JsonObject { ["sources"] = new JsonArray(source), ["stagedDecisions"] = new JsonArray(staged),
            ["cursor"] = new JsonObject { ["nextSourceOrdinal"] = 1 }, ["realm"] = "chaos_sea", ["sessionId"] = "session-a",
            ["requestId"] = "request-existing43", ["snapshotToken"] = "snapshot-existing43", ["turn"] = 43,
            ["originalSnapshotFingerprint"] = source["turnEvidence"]!["originalSnapshotFingerprint"]!.DeepClone(),
            ["conflictInstanceRef"] = root["instances"]![0]!["instanceId"]!.DeepClone() };
        var signed = new ValidationService.SpiritualSignedC1Origin(new CanonicalBeforeImage(false, null),
            new CanonicalBeforeImage(true, Encoding.UTF8.GetBytes("{\"schemaVersion\":1,\"activeConflict\":{\"conflictId\":\"conflict-a\"}}")),
            source["turnEvidence"]!["originalSnapshotFingerprint"]!.GetValue<string>());
        foreach (var pair in new ValidationService.SpiritualOriginalTurnCapture.AuthenticatedC3Pair?[]
                 { null, new(new object(), signed, packet, null, new object()) })
        {
            var result = SpiritualWoundDeclineReceiptReducer.Reduce(signed, packet, authenticatedPair: pair);
            Assert.Null(result.AfterImage);
            Assert.Contains(result.Issues, issue => issue.Code == "spiritual_c3_materialization_owner_pair_missing");
        }
    }

    /// <summary>Checks exact group DATA, never a captured owner; mismatched source facts cannot share a parent.</summary>
    [Theory]
    [InlineData("valid")]
    [InlineData("owner")]
    [InlineData("target")]
    [InlineData("definition")]
    [InlineData("root")]
    [InlineData("component")]
    public void ExistingParentSourceGroupRequiresExactGraphData(string change)
    {
        var (_, body, _) = CreateData(3);
        var wound = WoundMaterializationContract.Parse(body.ToJsonString(), "unit.parent").Wound!;
        var owner = wound.Owner;
        var target = new EffectTargetKey(owner.Realm, "player", owner.OwnerId);
        var definitions = wound.Consequences.OwnedEffectSources.DefinitionFacts.Select(fact =>
            new WoundEffectSourceDefinition(fact.DefinitionKey, SpiritualWoundStateJson.Parse(fact.CanonicalJson))).ToArray();
        var roots = wound.Consequences.OwnedEffectSources.RootBindings.Select(root =>
            new WoundRootLineageAuthorityRow(null, root.EffectId, root.DefinitionKey, WoundRootOwnershipDomain.BaseWound)).ToArray();
        var group = new WoundSourceGroupAuthority(new(owner.Realm, "wound", wound.WoundId), owner, target, false,
            null, null, definitions, [], roots);
        using var payload = System.Text.Json.JsonDocument.Parse("{\"operation\":\"maneuver\",\"axis\":\"sideStrain\",\"magnitude\":1}");
        using var magnitude = System.Text.Json.JsonDocument.Parse("1");
        var contribution = new SpiritualWoundConflictContribution("effect_parent_strain", "component_001",
            new(owner.Realm, "wound", wound.WoundId, "definition_parent_strain"), target, "player",
            SpiritualWoundProjectionKind.StrainBurden, "spiritual_strain_burden", "maneuver", "sideStrain", "sideStrain",
            magnitude.RootElement.Clone(), 0, 1, payload.RootElement.Clone());
        Assert.True(AcceptedMechanicsPlanner.ResourceExecutionSession.SpiritualMechanicsContext.MatchesSignedParentSourceGroup(wound, contribution, group));
        if (change == "owner") owner = owner with { OwnerId = "foreign_soul" };
        if (change == "target") target = target with { TargetId = "foreign_soul" };
        if (change == "definition")
        {
            var changed = definitions[0].Definition; changed["display"]!["description"] = "Иное определение.";
            definitions[0] = new(definitions[0].DefinitionKey, changed);
        }
        if (change == "root") roots[0] = roots[0] with { EffectId = "foreign_effect" };
        if (change == "component") contribution = contribution with { ComponentId = "foreign_component" };
        group = new(new(wound.Owner.Realm, "wound", wound.WoundId), owner, target, false, null, null, definitions, [], roots);
        Assert.Equal(change == "valid", AcceptedMechanicsPlanner.ResourceExecutionSession.SpiritualMechanicsContext
            .MatchesSignedParentSourceGroup(wound, contribution, group));
    }

    private static (JsonObject Root, JsonObject Parent) CreateLedger(int rank)
    {
        var root = SpiritualStrainReceiptJoinTests.CreateLedger();
        var (source, parent, _) = CreateData(rank);
        root["sources"]![1]!["witness"] = source;
        var decision = root["decisions"]![1]!;
        decision["binding"] = new JsonObject { ["sessionId"] = "session-a", ["requestId"] = "request-existing43",
            ["snapshotToken"] = "snapshot-existing43", ["turn"] = 43, ["continuationGeneration"] = 1,
            ["waveOrdinal"] = 0, ["sourceOrdinal"] = 0 };
        SpiritualStrainReceiptJoinTests.SealSourceAndDecision(root, 1);
        return (root, parent);
    }

    private static SpiritualWoundOpportunityReceiptState ReadLedger(JsonObject root)
    {
        var result = SpiritualWoundOpportunityReceiptState.Parse(root.ToJsonString(), "unit.existingLedger");
        Assert.True(result.IsValid, string.Join("\n", result.Issues));
        return result.State!;
    }

    private static WoundHistoryState ReadHistory(JsonObject root)
    {
        var result = WoundHistoryState.Parse(root.ToJsonString(), WoundHistoryState.HistoryPath);
        Assert.True(result.IsValid, string.Join("\n", result.Issues));
        return result.State!;
    }

    private static JsonObject CreateHistory(JsonObject parent, int rank)
    {
        var (_, creation, _) = CreateData(3);
        var initial = WoundMaterializationContract.Parse(creation.ToJsonString(), "unit.existingCreation").Wound!;
        var latest = WoundMaterializationContract.Parse(parent.ToJsonString(), "unit.existingLatest").Wound!;
        var create = WoundContractTestData.CreateTransition();
        create["transitionId"] = initial.LastTransition.TransitionId; create["woundId"] = initial.WoundId;
        create["turn"] = initial.LastTransition.Turn; create["eventRef"] = initial.Origin.EventRef;
        create["attemptId"] = null;
        create["beforeFingerprint"] = WoundHistoryState.ComputeNonexistentBeforeFingerprint(initial.WoundId);
        create["afterFingerprint"] = WoundIdentityState.ComputeSemanticFingerprint(initial);
        create["sourceFingerprint"] = "sha256:" + new string('a', 64);
        var history = WoundContractTestData.CreateHistory(create);
        if (rank == 4)
        {
            var worsen = create.DeepClone().AsObject();
            worsen["transitionId"] = latest.LastTransition.TransitionId; worsen["operationKey"] = "operation_existing_worsen";
            worsen["ordinal"] = 2; worsen["woundTransitionOrdinal"] = 2; worsen["kind"] = "worsen";
            worsen["eventRef"] = latest.Severity.LastChangeEventRef;
            worsen["beforeFingerprint"] = create["afterFingerprint"]!.DeepClone();
            worsen["afterFingerprint"] = WoundIdentityState.ComputeSemanticFingerprint(latest);
            history["transitions"]!.AsArray().Add(worsen); history["nextOrdinal"] = 3;
        }
        return history;
    }

    internal static (JsonObject Source, JsonObject Parent, JsonObject Ordinary) CreateData(int rank)
    {
        var (source, _, parent, ordinary) = SpiritualStrainPreimageDataTests.CreateData();
        Assert.True(rank is 3 or 4);
        if (rank == 4)
        {
            parent["severity"]!["rank"] = 4; parent["severity"]!["value"] = "IV";
            parent["lastTransition"]!["kind"] = "worsen";
            parent["lastTransition"]!["ordinal"] = 2;
            parent["lastTransition"]!["transitionId"] = "transition_existing_worsen_2";
            parent["severity"]!["lastChangeEventRef"] = "event_existing_worsen_42";
            parent["consequences"]!["slotBudget"] = 4; parent["consequences"]!["slotsUsed"] = 4;
            var graph = parent["consequences"]!["ownedEffectSources"]!;
            var cost = graph["definitions"]!.AsArray().Single(row => row!["definitionKey"]!.GetValue<string>() == "definition_parent_cost")!;
            cost["components"]![0]!["payload"]!["magnitude"] = 3;
            graph["definitions"]!.AsArray().Add(EffectMaterializationTestFixture.CreateSpiritualWoundDefinition(
                "spiritual_roll_hindrance", woundId: parent["woundId"]!.GetValue<string>(), realm: "chaos_sea",
                definitionKey: "definition_parent_extra", targetKind: "player", operation: "pressure"));
            graph["rootBindings"]!.AsArray().Add(WoundContractTestData.CreateRootBinding("effect_parent_extra", "definition_parent_extra"));
            parent["consequences"]!["entries"]!.AsArray().Add(new JsonObject
                { ["slot"] = 4, ["profileKey"] = "spiritual_roll_hindrance", ["effectId"] = "effect_parent_extra",
                    ["readableSummary"] = "Давление душе даётся труднее." });
        }
        ordinary["turnNumber"] = 43;
        ordinary["diceAudit"]!["diceUsed"]![0]!["sourceIndex"] = 0;
        ordinary["diceAudit"]!["diceUsed"]![1]!["sourceIndex"] = 1;
        var actual = ordinary.DeepClone().AsObject(); actual["after"]!["playerSideStrain"] = "broken";
        source["coordinate"] = "activeConflict.exchangeLog[0].player";
        source["sourceOrdinal"] = 0; source["exchangeOrdinal"] = 0;
        source["selectedDiceIndices"] = new JsonArray(0, 1);
        var evidence = source["turnEvidence"]!;
        evidence["requestId"] = "request-existing43"; evidence["snapshotToken"] = "snapshot-existing43"; evidence["turn"] = 43;
        evidence["bounds"]!["exchangeCount"] = 1; evidence["bounds"]!["sourceSlotCount"] = 2; evidence["bounds"]!["diceCount"] = 2;
        evidence["acceptedD20Values"] = new JsonArray(4, 12);
        var claims = new JsonArray();
        for (var i = 0; i < 2; i++)
        {
            var row = new JsonObject { ["sourceIndex"] = i, ["value"] = i == 0 ? 4 : 12,
                ["exchangeOrdinal"] = 0, ["claimFingerprint"] = "" };
            row["claimFingerprint"] = SpiritualWoundTurnEvidence.ComputeClaimFingerprint(row); claims.Add(row);
        }
        evidence["diceClaims"] = claims;
        source["exchangeJsonBase64"] = Encode(actual);
        source["strainPreimage"] = new JsonObject { ["version"] = 2,
            ["ordinaryExchangeJsonBase64"] = Encode(ordinary), ["parentWoundJsonBase64"] = Encode(parent),
            ["effectId"] = "effect_parent_strain", ["componentId"] = "component_001" };
        Seal(source);
        return (source, parent, ordinary);
    }

    private static JsonObject ReadActual(JsonObject source) => JsonNode.Parse(Convert.FromBase64String(
        source["exchangeJsonBase64"]!.GetValue<string>()))!.AsObject();
    private static string Encode(JsonNode value) => Convert.ToBase64String(Encoding.UTF8.GetBytes(SpiritualWoundStateJson.Canonical(value)));
    private static void Seal(JsonObject source)
    {
        var hash = SpiritualWoundStateJson.Hash(source, "source", "sourceId", "sourceFingerprint");
        source["sourceFingerprint"] = hash; source["sourceId"] = "spiritual_source_" + hash[7..];
    }
}
