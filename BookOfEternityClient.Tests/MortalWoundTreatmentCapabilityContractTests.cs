using System.Text.Json;
using System.Text.Json.Nodes;
using BookOfEternityClient.Services;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed class MortalWoundTreatmentCapabilityContractTests
{
    public sealed record CapabilityCatalogScenario(
        string Name,
        string OwnerKind,
        string OwnerId,
        JsonObject ActiveRoot,
        string ActiveSourcePath,
        JsonObject PassiveRoot,
        string PassiveSourcePath,
        bool ExpectedValid,
        string? ExpectedCode,
        int ExpectedSourceCount);

    public static IEnumerable<object[]> CapabilityCatalogRows()
    {
        yield return Row("player active capability", "player", "player", true, false, true, null, 1);
        yield return Row("player passive capability", "player", "player", false, true, true, null, 1);
        yield return Row("npc active capability", "npc", "npc_field_medic", true, false, true, null, 1,
            npc: true);
        yield return Row("npc passive capability", "npc", "npc_field_medic", false, true, true, null, 1,
            npc: true);
        yield return Row("optional extension absence", "player", "player", false, false, true, null, 0);
        yield return Row("extension requires skill id", "player", "player", true, false, false,
            "mortal_wound_treatment_capability_skill_id_invalid", 0, mutate: skill => skill.Remove("skillId"));
        yield return Row("schema is closed", "player", "player", true, false, false,
            "mortal_wound_treatment_capability_shape_invalid", 0,
            mutate: skill => Capability(skill)["callerMayOverride"] = true);
        yield return Row("physical domain required", "player", "player", true, false, false,
            "mortal_wound_treatment_capability_domain_invalid", 0,
            mutate: skill => Capability(skill)["woundDomain"] = "spiritual");
        yield return Row("severity envelope is inclusive I through IV", "player", "player", true, false, false,
            "mortal_wound_treatment_capability_severity_invalid", 0,
            mutate: skill => Capability(skill)["minimumSeverityRank"] = 0);
        yield return Row("operation limits are closed", "player", "player", true, false, false,
            "mortal_wound_treatment_capability_operation_limits_invalid", 0,
            mutate: skill => Limits(Capability(skill))["callerMayOverride"] = true);
        yield return Row("complication kinds are closed", "player", "player", true, false, false,
            "mortal_wound_treatment_capability_complication_kind_invalid", 0,
            mutate: skill => Limits(Capability(skill))["removableComplicationKinds"] = new JsonArray("unknown"));
        yield return Row("legacy aggregate cannot exceed eight", "player", "player", true, false, false,
            "mortal_wound_treatment_capability_legacy_limit_invalid", 0,
            mutate: skill =>
            {
                Limits(Capability(skill))["maximumCosmeticHealLegacies"] = 8;
                Limits(Capability(skill))["maximumMechanicalEffectHealLegacies"] = 1;
            });
        yield return Row("at least one operation is enabled", "player", "player", true, false, false,
            "mortal_wound_treatment_capability_empty", 0,
            mutate: skill => SetAllOperationLimitsToZero(Limits(Capability(skill))));
        yield return Row("cross active passive exact skill ids collide", "player", "player", true, true, false,
            "mortal_wound_treatment_capability_skill_id_confusable", 0,
            mutate: null, duplicateSkillId: true);
        yield return Row("cross active passive confusable capability refs collide", "player", "player", true, true, false,
            "mortal_wound_treatment_capability_ref_confusable", 0,
            mutate: null, duplicateCapabilityRef: true, confusable: true);
    }

    [Theory]
    [MemberData(nameof(CapabilityCatalogRows))]
    public void ParseActorCatalog_UsesOneClosedCrossSkillNamespace(CapabilityCatalogScenario row)
    {
        var result = MortalWoundTreatmentCapabilityContract.ParseActorCatalog(
            row.OwnerKind,
            row.OwnerId,
            row.ActiveRoot,
            row.ActiveSourcePath,
            row.PassiveRoot,
            row.PassiveSourcePath);

        Assert.Equal(row.ExpectedValid, result.IsValid);
        Assert.Equal(row.ExpectedCode, result.Issues.FirstOrDefault()?.Code);
        Assert.Equal(row.ExpectedSourceCount, result.Sources.Count);
    }

    [Fact]
    public void ParseActorCatalog_ReturnsDetachedValuesAndNeverUsesDisplayNameAsAuthority()
    {
        var active = Root("activeSkillChanges", Skill("skill_field_medicine_01", "Field Medicine", withCapability: true));
        var passive = Root("passiveSkillChanges", Skill("skill_field_medicine_02", "Not authority", withCapability: false));

        var result = MortalWoundTreatmentCapabilityContract.ParseActorCatalog(
            "player", "player", active, "game_state/player/skills_active.json",
            passive, "game_state/player/skills_passive.json");

        Assert.True(result.IsValid);
        active["activeSkillChanges"]!.AsArray()[0]!["skillId"] = "skill_changed_after_parse";
        Capability(active["activeSkillChanges"]!.AsArray()[0]!.AsObject())["capabilityRef"] = "changed_after_parse";
        Assert.Equal("skill_field_medicine_01", Assert.Single(result.Sources).SkillId);
        Assert.Equal("field_medicine_guaranteed_care", Assert.Single(Assert.Single(result.Sources).Capabilities).CapabilityRef);
        Assert.Equal("Field Medicine", Assert.Single(result.Sources).DisplayName);
    }

    [Fact]
    public void ParseActorCatalog_DerivesCurrentLifecycleFromCatalogMembershipAndIgnoresLookalikes()
    {
        var skill = Skill("skill_field_medicine_01", "Field Medicine", withCapability: true);
        skill["lifecycle"] = "retired";
        skill["active"] = false;
        var active = Root("activeSkillChanges", skill);

        var result = MortalWoundTreatmentCapabilityContract.ParseActorCatalog(
            "player", "player_current", active, "game_state/player/skills_active.json",
            new JsonObject { ["passiveSkillChanges"] = new JsonArray() },
            "game_state/player/skills_passive.json");

        Assert.True(result.IsValid);
        var source = Assert.Single(result.Sources);
        Assert.Equal("active", source.Lifecycle);
        Assert.True(source.Active);
    }

    [Fact]
    public void ParseActorCatalog_RejectsPresentNullExtensionWhileAllowingItsAbsence()
    {
        var active = Root("activeSkillChanges", Skill("skill_field_medicine_01", "Field Medicine", withCapability: false));
        var passive = Root("passiveSkillChanges", Skill("skill_observation_01", "Observation", withCapability: false));

        Assert.True(MortalWoundTreatmentCapabilityContract.ParseActorCatalog(
            "player", "player", active, "game_state/player/skills_active.json",
            passive, "game_state/player/skills_passive.json").IsValid);

        active["activeSkillChanges"]!.AsArray()[0]!.AsObject()["mortalWoundTreatmentCapabilities"] = null;
        var result = MortalWoundTreatmentCapabilityContract.ParseActorCatalog(
            "player", "player", active, "game_state/player/skills_active.json",
            passive, "game_state/player/skills_passive.json");

        Assert.False(result.IsValid);
        Assert.Contains(result.Issues, issue => issue.Code == "mortal_wound_treatment_capability_shape_invalid");
    }

    [Fact]
    public void ValidateSkillExtension_RequiresExactFieldSetsAndCheckedOperationLimits()
    {
        using var document = JsonDocument.Parse(Skill("skill_field_medicine_01", "Field Medicine", true).ToJsonString());
        var issues = new List<ValidationIssue>();

        MortalWoundTreatmentCapabilityContract.ValidateSkillExtension(
            document.RootElement,
            "skills.active[0]",
            issues);

        Assert.Empty(issues);
    }

    [Fact]
    public void ValidateComposedActorCatalog_RequiresExistingExtensionsToBePreservedAndForbidsSynthesis()
    {
        var currentActive = Root("activeSkillChanges", Skill("skill_field_medicine_01", "Field Medicine", true));
        var currentPassive = Root("passiveSkillChanges", Skill("skill_field_medicine_02", "Observation", false));
        var composedActive = currentActive.DeepClone().AsObject();
        var composedPassive = currentPassive.DeepClone().AsObject();

        Assert.Empty(MortalWoundTreatmentCapabilityContract.ValidateComposedActorCatalog(
            "player", "player", currentActive, "game_state/player/skills_active.json",
            currentPassive, "game_state/player/skills_passive.json", composedActive, composedPassive));

        Limits(Capability(composedActive["activeSkillChanges"]!.AsArray()[0]!.AsObject()))["maximumRecoveryPoints"] = 2;
        var changedIssues = MortalWoundTreatmentCapabilityContract.ValidateComposedActorCatalog(
            "player", "player", currentActive, "game_state/player/skills_active.json",
            currentPassive, "game_state/player/skills_passive.json", composedActive, composedPassive);
        Assert.Contains(changedIssues, issue => issue.Code == "mortal_wound_treatment_capability_preservation_invalid");

        composedActive = currentActive.DeepClone().AsObject();
        composedPassive["passiveSkillChanges"]!.AsArray()[0]!.AsObject()["mortalWoundTreatmentCapabilities"] =
            new JsonArray(Capability(currentActive["activeSkillChanges"]!.AsArray()[0]!.AsObject()).DeepClone());
        Capability(composedPassive["passiveSkillChanges"]!.AsArray()[0]!.AsObject())["capabilityRef"] = "observation_guaranteed_care";
        var synthesizedIssues = MortalWoundTreatmentCapabilityContract.ValidateComposedActorCatalog(
            "player", "player", currentActive, "game_state/player/skills_active.json",
            currentPassive, "game_state/player/skills_passive.json", composedActive, composedPassive);
        Assert.Contains(synthesizedIssues, issue => issue.Code == "mortal_wound_treatment_capability_synthesized");
    }

    [Fact]
    public void ValidateComposedActorCatalog_PreservesTheCompleteExtensionNodeIncludingOrderAndEmptyArrays()
    {
        var currentActiveSkill = Skill("skill_field_medicine_01", "Field Medicine", true);
        currentActiveSkill["mortalWoundTreatmentCapabilities"]!.AsArray().Add(CapabilityDefinition("second_guaranteed_care"));
        var currentActive = Root("activeSkillChanges", currentActiveSkill);
        var currentPassive = Root("passiveSkillChanges", Skill("skill_observation_01", "Observation", false));

        var reorderedActive = currentActive.DeepClone().AsObject();
        var reorderedCapabilities = reorderedActive["activeSkillChanges"]!.AsArray()[0]!["mortalWoundTreatmentCapabilities"]!.AsArray();
        var first = reorderedCapabilities[0]!.DeepClone();
        reorderedCapabilities[0] = reorderedCapabilities[1]!.DeepClone();
        reorderedCapabilities[1] = first;
        Assert.Contains(
            MortalWoundTreatmentCapabilityContract.ValidateComposedActorCatalog(
                "player", "player", currentActive, "game_state/player/skills_active.json",
                currentPassive, "game_state/player/skills_passive.json",
                reorderedActive, currentPassive.DeepClone().AsObject()),
            issue => issue.Code == "mortal_wound_treatment_capability_preservation_invalid");

        var composedWithEmptyExtension = currentActive.DeepClone().AsObject();
        var passiveWithEmptyExtension = currentPassive.DeepClone().AsObject();
        passiveWithEmptyExtension["passiveSkillChanges"]!.AsArray()[0]!.AsObject()["mortalWoundTreatmentCapabilities"] = new JsonArray();
        Assert.Contains(
            MortalWoundTreatmentCapabilityContract.ValidateComposedActorCatalog(
                "player", "player", currentActive, "game_state/player/skills_active.json",
                currentPassive, "game_state/player/skills_passive.json",
                composedWithEmptyExtension, passiveWithEmptyExtension),
            issue => issue.Code == "mortal_wound_treatment_capability_synthesized");

        var currentWithEmptyExtension = currentPassive.DeepClone().AsObject();
        currentWithEmptyExtension["passiveSkillChanges"]!.AsArray()[0]!.AsObject()["mortalWoundTreatmentCapabilities"] = new JsonArray();
        Assert.Contains(
            MortalWoundTreatmentCapabilityContract.ValidateComposedActorCatalog(
                "player", "player", currentActive, "game_state/player/skills_active.json",
                currentWithEmptyExtension, "game_state/player/skills_passive.json",
                currentActive.DeepClone().AsObject(), currentPassive.DeepClone().AsObject()),
            issue => issue.Code == "mortal_wound_treatment_capability_preservation_invalid");
    }

    [Fact]
    public void ValidateComposedActorCatalog_RejectsPresentNullExtensionsInsteadOfTreatingThemAsAbsent()
    {
        var currentActive = Root("activeSkillChanges", Skill("skill_field_medicine_01", "Field Medicine", withCapability: false));
        var currentPassive = Root("passiveSkillChanges", Skill("skill_observation_01", "Observation", withCapability: false));

        var composedOnlyNull = currentPassive.DeepClone().AsObject();
        composedOnlyNull["passiveSkillChanges"]!.AsArray()[0]!.AsObject()["mortalWoundTreatmentCapabilities"] = null;
        Assert.Contains(
            MortalWoundTreatmentCapabilityContract.ValidateComposedActorCatalog(
                "player", "player", currentActive, "game_state/player/skills_active.json",
                currentPassive, "game_state/player/skills_passive.json",
                currentActive.DeepClone().AsObject(), composedOnlyNull),
            issue => issue.Code == "mortal_wound_treatment_capability_shape_invalid");

        var currentNull = currentActive.DeepClone().AsObject();
        currentNull["activeSkillChanges"]!.AsArray()[0]!.AsObject()["mortalWoundTreatmentCapabilities"] = null;
        Assert.Contains(
            MortalWoundTreatmentCapabilityContract.ValidateComposedActorCatalog(
                "player", "player", currentNull, "game_state/player/skills_active.json",
                currentPassive, "game_state/player/skills_passive.json",
                currentActive.DeepClone().AsObject(), currentPassive.DeepClone().AsObject()),
            issue => issue.Code == "mortal_wound_treatment_capability_shape_invalid");
    }

    private static object[] Row(
        string name,
        string ownerKind,
        string ownerId,
        bool activeCapability,
        bool passiveCapability,
        bool expectedValid,
        string? expectedCode,
        int expectedSourceCount,
        Action<JsonObject>? mutate = null,
        bool duplicateSkillId = false,
        bool duplicateCapabilityRef = false,
        bool confusable = false,
        bool npc = false)
    {
        var activeProperty = npc ? "activeSkills" : "activeSkillChanges";
        var passiveProperty = npc ? "passiveSkills" : "passiveSkillChanges";
        var activeSkill = Skill("skill_field_medicine_01", "Field Medicine", activeCapability);
        var passiveSkill = Skill("skill_observation_01", "Observation", passiveCapability);
        if (duplicateSkillId)
            passiveSkill["skillId"] = activeSkill["skillId"]!.DeepClone();
        if (duplicateCapabilityRef)
        {
            passiveSkill["mortalWoundTreatmentCapabilities"] = new JsonArray(Capability(activeSkill).DeepClone());
            if (confusable)
                Capability(passiveSkill)["capabilityRef"] = "fіeld_medicine_guaranteed_care";
        }

        mutate?.Invoke(activeCapability ? activeSkill : passiveSkill);
        return new object[] { new CapabilityCatalogScenario(
            name,
            ownerKind,
            ownerId,
            Root(activeProperty, activeSkill),
            npc ? "game_state/npcs/npc_core.json.NPCs[0].activeSkills" : "game_state/player/skills_active.json",
            Root(passiveProperty, passiveSkill),
            npc ? "game_state/npcs/npc_core.json.NPCs[0].passiveSkills" : "game_state/player/skills_passive.json",
            expectedValid,
            expectedCode,
            expectedSourceCount) };
    }

    private static JsonObject Root(string property, JsonObject skill) => new()
    {
        [property] = new JsonArray(skill)
    };

    private static JsonObject Skill(string skillId, string displayName, bool withCapability)
    {
        var skill = new JsonObject
        {
            ["skillId"] = skillId,
            ["skillName"] = displayName,
            ["displayName"] = displayName,
            ["lifecycle"] = "active",
            ["active"] = true
        };
        if (withCapability)
        {
            skill["mortalWoundTreatmentCapabilities"] = new JsonArray(CapabilityDefinition("field_medicine_guaranteed_care"));
        }
        return skill;
    }

    private static JsonObject CapabilityDefinition(string capabilityRef) => new()
    {
        ["schemaVersion"] = 1,
        ["capabilityRef"] = capabilityRef,
        ["woundDomain"] = "physical",
        ["minimumSeverityRank"] = 1,
        ["maximumSeverityRank"] = 4,
        ["operationLimits"] = new JsonObject
        {
            ["mayStabilize"] = true,
            ["maximumRecoveryPoints"] = 1,
            ["maximumSeverityReductionSteps"] = 1,
            ["removableComplicationKinds"] = new JsonArray("bleeding", "infection"),
            ["mayHealAtSeverityI"] = true,
            ["maximumCosmeticHealLegacies"] = 1,
            ["maximumMechanicalEffectHealLegacies"] = 1
        }
    };

    private static JsonObject Capability(JsonObject skill) =>
        Assert.IsType<JsonObject>(Assert.Single(skill["mortalWoundTreatmentCapabilities"]!.AsArray()));

    private static JsonObject Limits(JsonObject capability) =>
        Assert.IsType<JsonObject>(capability["operationLimits"]);

    private static void SetAllOperationLimitsToZero(JsonObject limits)
    {
        limits["mayStabilize"] = false;
        limits["maximumRecoveryPoints"] = 0;
        limits["maximumSeverityReductionSteps"] = 0;
        limits["removableComplicationKinds"] = new JsonArray();
        limits["mayHealAtSeverityI"] = false;
        limits["maximumCosmeticHealLegacies"] = 0;
        limits["maximumMechanicalEffectHealLegacies"] = 0;
    }
}
