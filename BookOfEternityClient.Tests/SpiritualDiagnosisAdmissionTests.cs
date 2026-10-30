using System.Text.Json;
using System.Text.Json.Nodes;
using BookOfEternityClient.CommandProtocol;
using BookOfEternityClient.Models;
using BookOfEternityClient.Services;
using BookOfEternityClient.UI;
using Xunit;

namespace BookOfEternityClient.Tests;

/// <summary>Detached menu/request DATA, never a signed execution or publication capability.</summary>
public sealed class SpiritualDiagnosisAdmissionTests
{
    [Theory]
    [InlineData("chaos_sea")][InlineData("shining_abode")]
    public void PresentZeroArtAdmitsExactPathWithoutHealingThresholdAndDetachesExports(string realm)
    {
        var context = Context(realm);
        var wound = Assert.Single(context.Catalog.Occurrences).Wound;
        var source = new SpiritualDiagnosisSourceData(context);
        var result = source.Admit(wound, "diagnose_tissue");
        var admitted = Assert.IsType<SpiritualDiagnosisAdmission>(result.Data);
        Assert.Null(result.IssueCode); Assert.Equal(0, admitted.HealingTier);
        Assert.Equal(2, admitted.Wound.Severity.Rank);
        Assert.NotNull(admitted.Selection.Diagnosis);
        var json = admitted.Selection.ToJson();
        Assert.Equal(9, json["schemaVersion"]!.GetValue<int>());
        Assert.Equal("diagnose", json["mode"]!.GetValue<string>());
        Assert.Equal(admitted.Selection, SpiritualSelfTreatmentSelection.Parse(json));
        json["diagnosisPathId"] = "changed_export";
        Assert.Equal("diagnose_tissue", admitted.Selection.Diagnosis!.DiagnosisPathId);
        var before = admitted.SourceFingerprint;
        context.Input.Carriers.AfterlifeProfiles!["profiles"]![0]!["standardArts"]!["spiritual_healing"] = 5;
        context.Soul["afterlifeCombatProfile"]!["artTiers"]!["spiritual_healing"] = 5;
        Assert.Equal(before, Assert.IsType<SpiritualDiagnosisAdmission>(source.Admit(wound, "diagnose_tissue").Data).SourceFingerprint);
        Assert.NotEqual(before, Assert.IsType<SpiritualDiagnosisAdmission>(new SpiritualDiagnosisSourceData(context).Admit(wound, "diagnose_tissue").Data).SourceFingerprint);
    }

    [Theory]
    [InlineData("missing")][InlineData("null")][InlineData("string")][InlineData("negative")]
    [InlineData("above_v")][InlineData("missing_soul")][InlineData("mirror")][InlineData("realm")]
    public void MissingOrMalformedSourceNeverBecomesZero(string fault)
    {
        var context = Context();
        var arts = context.Input.Carriers.AfterlifeProfiles!["profiles"]![0]!["standardArts"]!.AsObject();
        switch (fault)
        {
            case "missing": arts.Remove("spiritual_healing"); break;
            case "null": arts["spiritual_healing"] = null; break;
            case "string": arts["spiritual_healing"] = "0"; break;
            case "negative": arts["spiritual_healing"] = -1; break;
            case "above_v": arts["spiritual_healing"] = 6; break;
            case "missing_soul": context.Soul.Remove("afterlifeCombatProfile"); break;
            case "mirror": context.Soul["afterlifeCombatProfile"]!["artTiers"]!["spiritual_healing"] = 1; break;
            case "realm": context.Soul["currentRealm"] = "Mortal World"; break;
        }
        var result = new SpiritualDiagnosisSourceData(context).Admit(Assert.Single(context.Catalog.Occurrences).Wound, "diagnose_tissue");
        Assert.Null(result.Data); Assert.Equal("source_unavailable", result.IssueCode);
    }

    [Theory]
    [InlineData("source_capability")][InlineData("skill_tier")][InlineData("provider")]
    public void SupportedCurrentRequirementMustActuallyMatchItsCanonicalSource(string kind)
    {
        var requirement = Requirement(kind);
        var context = Context(requirement: requirement);
        var wound = Assert.Single(context.Catalog.Occurrences).Wound;
        Assert.NotNull(new SpiritualDiagnosisSourceData(context).Admit(wound, "diagnose_tissue").Data);
        var changed = requirement.DeepClone().AsObject();
        changed[kind switch { "provider" => "providerRef", "location" => "locationRef", _ => "capabilityRef" }] = "missing_canonical_source";
        var denied = Context(requirement: changed);
        var result = new SpiritualDiagnosisSourceData(denied).Admit(Assert.Single(denied.Catalog.Occurrences).Wound, "diagnose_tissue");
        Assert.Null(result.Data); Assert.Equal("requirement_unavailable", result.IssueCode);
    }

    [Theory]
    [InlineData("item_quantity")][InlineData("resource_quantity")][InlineData("consent")]
    [InlineData("facility")][InlineData("location")][InlineData("quest_state")][InlineData("effect_state")][InlineData("environment")]
    public void RegisteredShapeWithoutItsCanonicalProofIsUnavailableRatherThanIgnored(string kind)
    {
        var context = Context(requirement: Requirement(kind));
        var result = new SpiritualDiagnosisSourceData(context).Admit(Assert.Single(context.Catalog.Occurrences).Wound, "diagnose_tissue");
        Assert.Null(result.Data); Assert.Equal("requirement_unavailable", result.IssueCode);
    }

    [Theory]
    [InlineData("unknown_kind")][InlineData("unknown_field")][InlineData("missing_field")][InlineData("wrong_role")]
    public void OpaqueRequirementIsClosedBeforeAnyAvailabilityClaim(string fault)
    {
        var requirement = Requirement("source_capability");
        switch (fault)
        {
            case "unknown_kind": requirement["kind"] = "invented_condition"; break;
            case "unknown_field": requirement["unownedExtra"] = true; break;
            case "missing_field": requirement.Remove("capabilityRef"); break;
            case "wrong_role": requirement["actorRole"] = "world"; break;
        }
        var context = Context(requirement: requirement);
        var result = new SpiritualDiagnosisSourceData(context).Admit(Assert.Single(context.Catalog.Occurrences).Wound, "diagnose_tissue");
        Assert.Null(result.Data); Assert.Equal("requirements_invalid", result.IssueCode);
    }

    [Theory]
    [InlineData("unknown")][InlineData("formula")]
    public void GuaranteedDiagnosisDoesNotIgnoreNonemptyCheck(string field)
    {
        var context = Context(check: new JsonObject { [field] = "unowned_check" });
        var result = new SpiritualDiagnosisSourceData(context).Admit(Assert.Single(context.Catalog.Occurrences).Wound, "diagnose_tissue");
        Assert.Null(result.Data); Assert.Equal("check_unsupported", result.IssueCode);
    }

    [Theory]
    [InlineData("gm_only")][InlineData("hidden")][InlineData("future_fact")][InlineData("combat")]
    public void CurrentKnowledgeAndSafeScopeCannotBeReplacedByFutureClosure(string fault)
    {
        var context = Context(pathFault: fault);
        var result = new SpiritualDiagnosisSourceData(context).Admit(Assert.Single(context.Catalog.Occurrences).Wound, "diagnose_tissue");
        Assert.Null(result.Data); Assert.Equal(fault == "combat" ? "conflict_active" : "path_unavailable", result.IssueCode);
    }

    [Theory]
    [InlineData("aid")][InlineData("combat")][InlineData("service")]
    public void IndependentlyClosedTreatmentVariantCannotMixWithDiagnosis(string family)
    {
        var context = Context(); var wound = Assert.Single(context.Catalog.Occurrences).Wound;
        var diagnosis = Assert.IsType<SpiritualDiagnosisAdmission>(new SpiritualDiagnosisSourceData(context).Admit(wound, "diagnose_tissue").Data).Selection;
        var treatment = diagnosis with { Diagnosis = null };
        if (family == "aid") treatment = treatment with { Aid = new("player_soul", "guardian_current", "sha256:" + new string('b', 64)) };
        else if (family == "combat") treatment = treatment with { Combat = new("conflict_current", "sha256:" + new string('c', 64), 1) };
        else
        {
            var quote = AfterlifeHealingServiceTestData.Quote();
            treatment = new(quote.WoundId, quote.WoundFingerprint) { Service = new(quote) };
        }
        Assert.Equal(treatment, SpiritualSelfTreatmentSelection.Parse(treatment.ToJson()));
        var mixed = treatment with { Diagnosis = diagnosis.Diagnosis };
        var error = Assert.Throws<InvalidDataException>(() => mixed.ToJson());
        Assert.Equal("Diagnosis cannot mix with treatment families.", error.Message);
    }

    [Theory]
    [InlineData(-1)][InlineData(6)]
    public void SpiritualStandardArtRequirementDoesNotChangeTheExistingIntegerGrammar(int minimumTier)
    {
        var requirement = Requirement("skill_tier"); requirement["minimumTier"] = minimumTier;
        var context = Context(requirement: requirement); var wound = Assert.Single(context.Catalog.Occurrences).Wound;
        Assert.True(MortalWoundTreatmentContract.ParseRequirementProjection(Assert.Single(wound.Treatment.DiagnosisPaths).Requirements, "existing.grammar").IsValid);
        var result = new SpiritualDiagnosisSourceData(context).Admit(wound, "diagnose_tissue");
        Assert.Null(result.Data); Assert.Equal("requirement_unavailable", result.IssueCode);
    }

    [Fact]
    public void TargetFilterDoesNotTreatEveryDiagnosisAsAnAidlessSelfChoice()
    {
        var context = Context(); var wound = Assert.Single(context.Catalog.Occurrences).Wound;
        var self = Assert.IsType<SpiritualDiagnosisAdmission>(new SpiritualDiagnosisSourceData(context).Admit(wound, "diagnose_tissue").Data).Selection;
        var otherOwner = new WoundOwnerCoordinate("chaos_sea", "resident", "resident_other", WoundCarrierCatalog.AfterlifeProfilesPath);
        var other = self with { Diagnosis = self.Diagnosis! with { Target = otherOwner } };
        // Closed selection/navigation DATA only; this does not admit the other actor.
        Assert.Equal(other, SpiritualSelfTreatmentSelection.Parse(other.ToJson()));
        var choices = new WoundCommandState("generation", [new("self", self, "Мой осмотр", "II", 0, true, ""),
            new("other", other, "Чужой осмотр", "II", 0, true, "")]);
        var selfTarget = new WoundTargetChoice("self_target", "Себя", "", [], wound.Owner, null, "sha256:" + new string('d', 64));
        var otherTarget = new WoundTargetChoice("other_target", "Другой", "", [], otherOwner, null, "sha256:" + new string('e', 64));
        Assert.Equal("self", Assert.Single(choices.ForTarget(selfTarget).Choices).Value);
        Assert.Equal("other", Assert.Single(choices.ForTarget(otherTarget).Choices).Value);
    }

    [Theory]
    [InlineData("unknown_field")][InlineData("wrong_version")][InlineData("wrong_mode")][InlineData("wrong_realm")]
    [InlineData("wrong_carrier")][InlineData("null_path")][InlineData("bad_path_fp")]
    public void SelectionAndRequestHaveClosedIndependentShapes(string fault)
    {
        var context = Context(); var wound = Assert.Single(context.Catalog.Occurrences).Wound;
        var admitted = Assert.IsType<SpiritualDiagnosisAdmission>(new SpiritualDiagnosisSourceData(context).Admit(wound, "diagnose_tissue").Data);
        var json = admitted.Selection.ToJson();
        switch (fault)
        {
            case "unknown_field": json["unownedExtra"] = true; break;
            case "wrong_version": json["schemaVersion"] = 8; break;
            case "wrong_mode": json["mode"] = "aid"; break;
            case "wrong_realm": json["target"]!["realm"] = "mortal_world"; break;
            case "wrong_carrier": json["target"]!["carrierPath"] = "game_state/player/wounds.json"; break;
            case "null_path": json["diagnosisPathId"] = null; break;
            case "bad_path_fp": json["expectedDiagnosisPathFingerprint"] = "not_a_fingerprint"; break;
        }
        Assert.Throws<InvalidDataException>(() => SpiritualSelfTreatmentSelection.Parse(json));
    }

    [Fact]
    public void DiagnosisRequestRetainsExactBindingWithoutRollCostOrSnapshotToken()
    {
        var context = Context(); var wound = Assert.Single(context.Catalog.Occurrences).Wound;
        var admitted = Assert.IsType<SpiritualDiagnosisAdmission>(new SpiritualDiagnosisSourceData(context).Admit(wound, "diagnose_tissue").Data);
        var turn = new SpiritualSelfTreatmentStageContext("session_diagnosis", "request_diagnosis", 43,
            new ProgressionControl { Type = "afterlife", CurrentRealm = "Chaos Sea", NextChaosSeaTurnOrdinal = 1 });
        var root = SpiritualDiagnosisRequestCodec.Create(turn, admitted.Selection, admitted);
        Assert.Equal(admitted.Selection, SpiritualDiagnosisRequestCodec.ReadSelection(root));
        Assert.True(SpiritualDiagnosisRequestCodec.IsExact(root, turn, admitted.Selection, admitted));
        foreach (var forbidden in new[] { "roll", "payment", "cost", "snapshotToken", "playerConfirmation" }) Assert.False(root.ContainsKey(forbidden));
        var changed = root.DeepClone().AsObject(); changed["diagnosis"]!["roll"] = new JsonArray(20);
        Assert.Throws<InvalidDataException>(() => SpiritualDiagnosisRequestCodec.ReadSelection(changed));
        Assert.False(SpiritualDiagnosisRequestCodec.IsExact(root, turn with { RequestId = "other_request" }, admitted.Selection, admitted));
        Assert.Throws<InvalidDataException>(() => SpiritualSelfTreatmentRequestCodec.Create(turn, admitted.Selection, wound.Owner.Realm, 1,
            admitted.SourceFingerprint, "normal", [14]));
        Assert.False(SpiritualSelfTreatmentRequestCodec.IsBound(root, turn, admitted.Selection, wound.Owner.Realm, 1));
        Assert.False(SpiritualSelfTreatmentRequestCodec.IsBoundActive(root, turn, admitted.Selection, wound.Owner.Realm, 1));
    }

    [Fact]
    public void RequestBindsCompleteControlEvenWhenRealmTypeAndOrdinalStayEqual()
    {
        var context = Context(); var wound = Assert.Single(context.Catalog.Occurrences).Wound;
        var data = Assert.IsType<SpiritualDiagnosisAdmission>(new SpiritualDiagnosisSourceData(context).Admit(wound, "diagnose_tissue").Data);
        var control = new ProgressionControl { Type = "afterlife", CurrentRealm = "Chaos Sea", NextChaosSeaTurnOrdinal = 1 };
        var turn = new SpiritualSelfTreatmentStageContext("session_control", "request_control", 43, control);
        var original = SpiritualDiagnosisRequestCodec.Create(turn, data.Selection, data);
        Assert.True(SpiritualDiagnosisRequestCodec.IsExact(original, turn, data.Selection, data));
        Assert.True(JsonNode.DeepEquals(JsonSerializer.SerializeToNode(control), original["control"]));
        var changed = JsonSerializer.Deserialize<ProgressionControl>(JsonSerializer.Serialize(control))!;
        changed.PendingChaosSeaCycles = 1;
        Assert.Equal(control.Type, changed.Type); Assert.Equal(control.CurrentRealm, changed.CurrentRealm);
        Assert.Equal(control.NextChaosSeaTurnOrdinal, changed.NextChaosSeaTurnOrdinal);
        var otherTurn = turn with { Progression = changed };
        var other = SpiritualDiagnosisRequestCodec.Create(otherTurn, data.Selection, data);
        Assert.Equal(data.Selection, SpiritualDiagnosisRequestCodec.ReadSelection(other));
        Assert.False(SpiritualDiagnosisRequestCodec.IsExact(original, otherTurn, data.Selection, data));
        Assert.False(SpiritualDiagnosisRequestCodec.IsExact(other, turn, data.Selection, data));
        var extra = original.DeepClone().AsObject(); extra["control"]!["unownedExtra"] = true;
        var payload = extra.DeepClone().AsObject(); payload.Remove("requestFingerprint");
        extra["requestFingerprint"] = WoundAcceptedTurnFingerprintWriter.Compute(new[]
        {
            "spiritual_diagnosis_request", "1", WoundAcceptedTurnFingerprintWriter.CanonicalJson(payload)
        });
        var refusal = Assert.Throws<InvalidDataException>(() => SpiritualDiagnosisRequestCodec.ReadSelection(extra));
        Assert.Equal("Diagnosis request control must have its complete exact closed shape.", refusal.Message);
    }

    [Fact]
    public void IdenticalMethodsRetainWoundContextAndAccurateUnavailablePresentation()
    {
        var prototype = Context();
        var template = JsonNode.Parse(WoundMaterializationContract.SerializeCanonical(Assert.Single(prototype.Catalog.Occurrences).Wound))!.AsObject();
        JsonObject Wound(string id, string locus)
        {
            var node = WoundContractTestData.CreateSpiritualActiveWound(woundId: id, ownerId: "player_soul",
                carrierPath: WoundCarrierCatalog.AfterlifeProfilesPath);
            node["complications"] = template["complications"]!.DeepClone();
            node["treatment"]!["diagnosisPaths"] = template["treatment"]!["diagnosisPaths"]!.DeepClone();
            node["classification"]!["locationProfile"]!["readableLocus"] = locus;
            // The factory already binds the source woundId. Rekey only its known
            // owned identifiers and every matching reference for this second vector.
            var text = node.ToJsonString();
            foreach (var owned in new[] { "effect_spiritual_roll", "effect_spiritual_cost", "definition_spiritual_roll", "definition_spiritual_cost",
                "component_spiritual_roll", "component_spiritual_cost", "stack_definition_spiritual_roll", "stack_definition_spiritual_cost" })
                text = text.Replace("\"" + owned + "\"", "\"" + owned + "_" + id + "\"", StringComparison.Ordinal);
            return JsonNode.Parse(text)!.AsObject();
        }
        var input = WoundPlayerProjectionTestData.InputInRealm("chaos_sea", Wound("wound_twin_a", "воля"), Wound("wound_twin_b", "память"));
        input.Carriers.AfterlifeProfiles!["profiles"]![0]!["standardArts"] = new JsonObject { ["spiritual_healing"] = 0 };
        var catalog = WoundCarrierCatalog.Build(input.Carriers); Assert.Empty(catalog.Issues);
        Assert.Empty(input.History.ValidateAgreement(input.Identities, catalog));
        var context = new WoundPlayerReadContext(input, catalog, prototype.Soul.DeepClone().AsObject(), null, WoundPlayerProjection.Build(input, catalog));
        var state = WoundApplicationService.CreateState("two_wounds", context);
        var choices = state.Choices.Where(choice => choice.Available && choice.Selection.Diagnosis is not null).ToArray();
        Assert.Equal(2, choices.Length); Assert.NotEqual(choices[0].Name, choices[1].Name);
        Assert.All(choices, choice =>
        {
            Assert.Contains("Осмотреть духовную ткань", choice.Name);
            Assert.Contains("Трещина духовной целостности", choice.Name);
            Assert.DoesNotContain(choice.Selection.WoundId, choice.Name);
        });
        Assert.Contains("воля", choices[0].Name); Assert.Contains("память", choices[1].Name);
        var command = ExplorerWoundCommandResultBuilder.Build("/лечить", context.Projection, state, true);
        var prompt = Assert.IsType<UiSelectionPrompt>(Assert.Single(command.Prompts, candidate => candidate.Id == "wound_selection"));
        Assert.Equal(choices.Select(choice => choice.Name + " — " + choice.Severity), prompt.Options.Where(option => !option.Disabled).Select(option => option.Label));
        Assert.Equal("wound_twin_b", state.Resolve(choices[1].Value).Selection.WoundId);
        var unsupported = Context(check: new JsonObject { ["formula"] = "not_supported" });
        var disabled = WoundApplicationService.CreateState("disabled_diagnosis", unsupported);
        var panels = ExplorerWoundCommandResultBuilder.Build("/лечить", unsupported.Projection, disabled, true).Blocks.OfType<UiPanelBlock>().ToArray();
        Assert.Contains(panels, panel => panel.Title.StartsWith("Духовный осмотр", StringComparison.Ordinal));
        Assert.DoesNotContain(panels, panel => panel.Title.StartsWith("Самостоятельное лечение", StringComparison.Ordinal) && panel.Title.Contains("Осмотреть духовную ткань", StringComparison.Ordinal));
    }

    internal static WoundPlayerReadContext Context(string realm = "chaos_sea", JsonObject? requirement = null,
        JsonObject? check = null, string? pathFault = null)
    {
        var node = WoundContractTestData.CreateSpiritualActiveWound(realm: realm, ownerId: "player_soul", carrierPath: WoundCarrierCatalog.AfterlifeProfilesPath);
        node["complications"] = new JsonArray(new JsonObject { ["complicationId"] = "hidden_instability", ["kind"] = "spiritual_instability",
            ["state"] = "active", ["displayName"] = "Скрытая неустойчивость", ["treatmentDifficultyModifier"] = 0,
            ["ownedEffectIds"] = new JsonArray(), ["visibility"] = "hidden" });
        node["treatment"]!["diagnosisPaths"] = new JsonArray(new JsonObject { ["diagnosisPathId"] = "diagnose_tissue", ["displayName"] = "Осмотреть духовную ткань",
            ["visibility"] = pathFault is "gm_only" or "hidden" ? pathFault : "public",
            ["requiresKnownFacts"] = pathFault == "future_fact" ? new JsonArray("complication:hidden_instability") : new JsonArray(),
            ["requirements"] = new JsonArray((requirement ?? Requirement("source_capability")).DeepClone()), ["check"] = check?.DeepClone() ?? new JsonObject(),
            ["reveals"] = new JsonArray("complication:hidden_instability"), ["failurePolicy"] = "no_reveal" });
        var input = WoundPlayerProjectionTestData.InputInRealm(realm, node);
        var profile = input.Carriers.AfterlifeProfiles!["profiles"]![0]!.AsObject();
        profile["standardArts"] = new JsonObject { ["spiritual_healing"] = 0 }; profile["locationId"] = "hall_current";
        var soul = new JsonObject { ["currentRealm"] = realm == "chaos_sea" ? "Chaos Sea" : "Shining Abode",
            ["afterlifeCombatProfile"] = new JsonObject { ["artTiers"] = new JsonObject { ["spiritual_healing"] = 0 } } };
        var catalog = WoundCarrierCatalog.Build(input.Carriers); Assert.Empty(catalog.Issues);
        return new(input, catalog, soul, pathFault == "combat" ? new JsonObject { ["activeConflict"] = new JsonObject() } : null,
            WoundPlayerProjection.Build(input, catalog));
    }

    private static JsonObject Requirement(string kind) => kind switch
    {
        "source_capability" => new() { ["kind"] = kind, ["capabilityRef"] = "spiritual_healing", ["actorRole"] = "provider" },
        "skill_tier" => new() { ["kind"] = kind, ["capabilityRef"] = "spiritual_healing", ["minimumTier"] = 0, ["actorRole"] = "provider" },
        "provider" => new() { ["kind"] = kind, ["providerRef"] = "player_soul" },
        "location" => new() { ["kind"] = kind, ["locationRef"] = "hall_current", ["targetRole"] = "target" },
        "item_quantity" => new() { ["kind"] = kind, ["itemRef"] = "item_current", ["quantity"] = 1, ["ownerRole"] = "provider" },
        "resource_quantity" => new() { ["kind"] = kind, ["resourceRef"] = "energy", ["quantity"] = 1, ["ownerRole"] = "provider" },
        "consent" => new() { ["kind"] = kind, ["consentRef"] = "consent_current", ["providerRef"] = "player_soul", ["targetRef"] = "player_soul" },
        "facility" => new() { ["kind"] = kind, ["facilityRef"] = "facility_current" },
        "quest_state" => new() { ["kind"] = kind, ["questRef"] = "quest_current", ["requiredState"] = "active" },
        "effect_state" => new() { ["kind"] = kind, ["effectRef"] = "effect_current", ["requiredState"] = "active", ["targetRole"] = "target" },
        "environment" => new() { ["kind"] = kind, ["environmentRef"] = "environment_current", ["requiredState"] = "safe" },
        _ => throw new ArgumentOutOfRangeException(nameof(kind))
    };
}
