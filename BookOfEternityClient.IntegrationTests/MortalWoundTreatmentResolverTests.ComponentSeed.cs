using System.Text.Json.Nodes;
using BookOfEternityClient.Core;
using BookOfEternityClient.Services;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed partial class MortalWoundTreatmentResolverTests
{
    /// <summary>
    /// Writes one player wound and a guaranteed-treatment provider into a caller-owned fixture before snapshot preparation.
    /// This data seeding issues no accepted-state, request, reservation or joint publication authority.
    /// </summary>
    /// <param name="fileSystem">
    /// The fresh fixture filesystem, prepared before its participating lease and runtime factories are entered.
    /// </param>
    internal static void SeedMinimalJointComponentData(FileSystemManager fileSystem)
    {
        ArgumentNullException.ThrowIfNull(fileSystem);
        var scenario = CreateScenario("guaranteed_current_capability_proof_stabilizes", "guaranteed");
        scenario = scenario with
        {
            History = CreateCurrentWoundHistory(scenario.Before),
            SeedCanonicalWoundEffects = true
        };
        var targetKind = scenario.AcceptedState["targetKind"]!.GetValue<string>();
        var targetId = scenario.AcceptedState["targetId"]!.GetValue<string>();
        var targetCarrierPath = WoundCarrierCatalog.PlayerPath;
        _ = AcceptedStateFixture.SeedCanonicalComponentData(
            fileSystem, scenario, targetKind, targetId, targetCarrierPath);
    }

    private sealed partial class AcceptedStateFixture
    {
        /// <summary>
        /// Seeds the existing typed treatment data graph without invoking any reflected resolver helper.
        /// </summary>
        /// <param name="fileSystem">
        /// The fixture filesystem whose initial canonical data is being written.
        /// </param>
        /// <param name="scenario">
        /// The data-only treatment scenario, including its requested wound and effect lineage.
        /// </param>
        /// <param name="targetKind">
        /// The canonical wound owner kind selected by <paramref name="scenario"/>.
        /// </param>
        /// <param name="targetId">
        /// The canonical owner identifier selected by <paramref name="scenario"/>.
        /// </param>
        /// <param name="targetCarrierPath">
        /// The canonical carrier path for the selected target kind.
        /// </param>
        /// <returns>
        /// The seeded effect identity data used by the original fixture's normal snapshot check.
        /// </returns>
        internal static JsonObject SeedCanonicalComponentData(
            FileSystemManager fileSystem,
            ResolverScenario scenario,
            string targetKind,
            string targetId,
            string targetCarrierPath)
        {
            foreach (var relativePath in new[]
                     {
                         "game_state/world/world_time.json",
                         "game_state/world/current_location.json",
                         "game_state/meta/soul_state.json",
                         "input/turn_request.json",
                         WoundIdentityState.StatePath,
                          WoundHistoryState.HistoryPath,
                          WoundCarrierCatalog.PlayerPath,
                          WoundCarrierCatalog.EnemiesPath,
                          WoundCarrierCatalog.AlliesPath,
                          EffectCarrierCatalog.PlayerPath,
                         EffectIdentityState.StatePath,
                         "game_state/inventory/items.json",
                         "game_state/inventory/item_identity_index.json"
                     })
            {
                Directory.CreateDirectory(Path.GetDirectoryName(
                    fileSystem.ResolvePath(relativePath))!);
            }
            WriteTargetWoundCarrier(
                fileSystem,
                targetKind,
                targetId,
                targetCarrierPath,
                scenario.Before);
            var parsedBefore = WoundMaterializationContract.Parse(
                scenario.Before.ToJsonString(),
                targetCarrierPath + ".activeWounds[0]");
            Assert.True(parsedBefore.IsValid, DescribeIssues(parsedBefore.Issues));
            var woundFingerprint = WoundIdentityState.ComputeSemanticFingerprint(
                Assert.IsType<WoundMaterializationEnvelope>(parsedBefore.Wound));
            File.WriteAllText(
                fileSystem.ResolvePath(WoundIdentityState.StatePath),
                WoundContractTestData.CreateIdentityIndex(
                    WoundContractTestData.CreateIdentityEntry(
                        woundId: scenario.Before["woundId"]!.GetValue<string>(),
                        ownerKind: targetKind,
                        ownerId: targetId,
                        carrierPath: targetCarrierPath,
                        semanticFingerprint: woundFingerprint)).ToJsonString());
            File.WriteAllText(
                fileSystem.ResolvePath(WoundHistoryState.HistoryPath),
                scenario.History.ToJsonString());
            var playerTreatmentSkill = CreateTreatmentSkill(
                "skill_field_medicine_01",
                "field_medicine");
            if (scenario.RollMode is "advantage" or "disadvantage")
            {
                var rollDefinition = EffectMaterializationTestFixture
                    .CreateDefinition("roll_modifier");
                rollDefinition["definitionKey"] =
                    "t061-treatment-roll-" + scenario.RollMode;
                rollDefinition["display"]!["name"] =
                    "T061 " + scenario.RollMode;
                rollDefinition["components"]![0]!["payload"] =
                    EffectMaterializationTestFixture.CreateBroadRollModifierPayload(
                        scenario.RollMode,
                        "skill_check");
                playerTreatmentSkill["activeEffectDefinitions"] =
                    new JsonArray(rollDefinition);
            }
            File.WriteAllText(
                fileSystem.ResolvePath("game_state/player/skills_active.json"),
                new JsonObject
                {
                    ["activeSkillChanges"] = new JsonArray(playerTreatmentSkill)
                }.ToJsonString());
            File.WriteAllText(
                fileSystem.ResolvePath("game_state/player/skills_passive.json"),
                new JsonObject
                {
                    ["passiveSkillChanges"] = new JsonArray(
                        CreateProductionPassiveSkill())
                }.ToJsonString());
            File.WriteAllText(
                fileSystem.ResolvePath("game_state/player/skill_mastery.json"),
                new JsonObject
                {
                    ["skillMasteryChanges"] = new JsonArray(new JsonObject
                    {
                        ["skillName"] = "Field Medicine",
                        ["newMasteryLevel"] = 3,
                        ["newCurrentMasteryProgress"] = 0,
                        ["newMasteryProgressNeeded"] = 100,
                        ["masteryLeveledUp"] = false
                    })
                }.ToJsonString());
            var sterileThread = CreateCanonicalStack(
                "sterile_thread",
                scenario.AcceptedState["sterileThreadCount"]?.GetValue<int>() ?? 2);
            var dose = scenario.RequirementsAvailable
                ? CreateCanonicalStack(
                    "antibiotic_dose",
                    scenario.AcceptedState["antibioticDoseCount"]?.GetValue<int>() ?? 8)
                : null;
            var reusableTool = scenario.AcceptedState["reusableToolCount"] is null
                ? null
                : CreateCanonicalStack(
                    "reusable_field_kit",
                    scenario.AcceptedState["reusableToolCount"]!.GetValue<int>());
            var npcCore = reusableTool is null
                ? NpcCoreRoot(sterileThread)
                : NpcCoreRoot(sterileThread, reusableTool);
            var medic = npcCore["NPCsInScene"]![0]!.AsObject();
            medic["displayName"] = "Field medic";
            medic["currentLocationId"] = "loc_field_clinic_001";
            medic["activeSkills"] = new JsonArray(
                CreateTreatmentSkill("skill_guaranteed_care_01", "exact_materialized_healing_source"),
                CreateTreatmentSkill("skill_field_medicine_npc_01", "field_medicine"));
            medic["passiveSkills"] = new JsonArray();
            File.WriteAllText(
                fileSystem.ResolvePath("game_state/npcs/npc_core.json"),
                npcCore.ToJsonString());
            if (reusableTool is not null)
            {
                File.WriteAllText(
                    fileSystem.ResolvePath("game_state/npcs/npc_inventory.json"),
                    new JsonObject
                    {
                        ["NPCInventoryAdds"] = new JsonArray()
                    }.ToJsonString());
            }
            File.WriteAllText(
                fileSystem.ResolvePath("game_state/world/world_time.json"),
                new JsonObject
                {
                    ["schemaVersion"] = 1,
                    ["currentTimeInMinutes"] = scenario.AcceptedState["worldMinute"]!.DeepClone()
                }.ToJsonString());
            var location = MortalLocationTestFixture.CreateCanonicalLocationWithIdentity(
                "loc_field_clinic_001",
                "T061 field clinic");
            location["customStates"] = new JsonArray(
                new JsonObject
                {
                    ["kind"] = "mortal_wound_treatment_facility",
                    ["schemaVersion"] = 1,
                    ["facilityId"] = "clean_work_surface",
                    ["displayName"] = "Clean work surface",
                    ["available"] = true
                },
                new JsonObject
                {
                    ["kind"] = "mortal_wound_treatment_environment",
                    ["schemaVersion"] = 1,
                    ["environmentId"] = "sterile_field",
                    ["displayName"] = "Sterile field",
                    ["state"] = "active"
                },
                new JsonObject
                {
                    ["kind"] = "mortal_wound_treatment_consent",
                    ["schemaVersion"] = 1,
                    ["consentRef"] = "consent_field_medic_player_01",
                    ["displayName"] = "Field treatment consent",
                    ["providerKind"] = "npc",
                    ["providerId"] = "field_medic_01",
                    ["targetKind"] = targetKind,
                    ["targetId"] = targetId,
                    ["status"] = "granted"
                });
            location["materialization"]!["sections"]!["customStates"] = new JsonObject
            {
                ["disposition"] = "populated",
                ["reason"] = null
            };
            MortalLocationTestFixture.ResealCanonicalLocation(location);
            File.WriteAllText(
                fileSystem.ResolvePath(MortalLocationMaterializationContract.WorldMapPath),
                MortalLocationTestFixture.CreateWorldMap(location).ToJsonString());
            File.WriteAllText(
                fileSystem.ResolvePath(MortalLocationMaterializationContract.CurrentLocationPath),
                MortalLocationTestFixture.CreateCurrentProjection(location).ToJsonString());
            File.WriteAllText(
                fileSystem.ResolvePath(MortalLocationIdentityState.StatePath),
                MortalLocationTestFixture.CreateIdentityIndex(location).ToJsonString());
            File.WriteAllText(
                fileSystem.ResolvePath("game_state/meta/soul_state.json"),
                new JsonObject { ["currentRealm"] = "Mortal World" }.ToJsonString());
            File.WriteAllText(
                fileSystem.ResolvePath("game_state/inventory/items.json"),
                PlayerInventoryRoot(dose).ToJsonString());
            File.WriteAllText(
                fileSystem.ResolvePath("game_state/inventory/item_identity_index.json"),
                CreateItemIdentityIndex(sterileThread, reusableTool, dose).ToJsonString());
            File.WriteAllText(
                fileSystem.ResolvePath("game_state/quests/regular_quests.json"),
                new JsonObject
                {
                    ["quests"] = new JsonArray(CreateProductionRegularQuest())
                }.ToJsonString());
            WriteCanonicalResourceAuthority(
                fileSystem,
                targetKind,
                targetId);
            var effectState = CreateCanonicalPlayerEffectState(scenario);
            RelocateCanonicalCombatEffects(
                fileSystem,
                targetKind,
                targetId,
                targetCarrierPath,
                effectState.Carrier,
                effectState.IdentityIndex);
            File.WriteAllText(
                fileSystem.ResolvePath(EffectCarrierCatalog.PlayerPath),
                effectState.Carrier.ToJsonString());
            File.WriteAllText(
                fileSystem.ResolvePath(EffectIdentityState.StatePath),
                effectState.IdentityIndex.ToJsonString());
            return effectState.IdentityIndex;
        }
    }
}
