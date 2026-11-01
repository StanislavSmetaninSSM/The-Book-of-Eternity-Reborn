using System.Diagnostics;
using System.Text;
using System.Text.RegularExpressions;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using BookOfEternityClient.AgentConsole;
using BookOfEternityClient.Configuration;
using BookOfEternityClient.Models;
using BookOfEternityClient.Services;
using BookOfEternityClient.Services.GmWorkers;
using BookOfEternityClient.UI;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Spectre.Console;

namespace BookOfEternityClient.Core;

public partial class GameEngine
{

    private static ValidationRepairHarnessPacket BuildMortalBootstrapMaterializationRepairPacket(
        IReadOnlyList<ValidationIssue> mortalBootstrapErrors)
    {
        var issueCodes = mortalBootstrapErrors
            .Select(issue => issue.Code ?? "validation_error")
            .Where(code => !string.IsNullOrWhiteSpace(code))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(code => code, StringComparer.OrdinalIgnoreCase)
            .ToList();
        var targetFiles = mortalBootstrapErrors
            .Select(issue => NormalizeMortalBootstrapRepairTargetPath(issue.FilePath))
            .Where(path => !string.IsNullOrWhiteSpace(path))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (!targetFiles.Contains("game_state/control/mortal_bootstrap_scaffold.json", StringComparer.OrdinalIgnoreCase))
            targetFiles.Insert(0, "game_state/control/mortal_bootstrap_scaffold.json");

        return new ValidationRepairHarnessPacket
        {
            Kind = "mortal_bootstrap_materialization_repair",
            Priority = "high",
            Title = "Mortal bootstrap materialization repair",
            TargetFiles = targetFiles,
            TemplateRefs = new List<string>
            {
                "Templates/MORTAL_LOCATION_TRANSITION_TEMPLATE.md",
                "Templates/MORTAL_FACTION_UPDATE_TEMPLATE.md",
                "Templates/MORTAL_NPC_UPDATE_TEMPLATE.md",
                "Templates/MORTAL_SKILL_PROGRESSION_TEMPLATE.md"
            },
            ExpectedShape = new List<string>
            {
                $"Repair these bootstrap issue codes first: {string.Join(", ", issueCodes)}.",
                "The first Mortal World turn must materialize only explicit GM-authored player-facing anchors from game_state/control/mortal_bootstrap_scaffold.json into canonical current-world files; the neutral baseline does not require factions, quests, or setting-owned location mechanics.",
                "Every structuredGmAuthority.playerProgression, carryingRules, or factionMechanics record must name the exact canonicalPath and contain a non-empty values object that repeats every authorized canonical value; faction records also identify factionId. Empty objects, reasons, unrelated paths, and prose do not grant authority.",
                "Current-world codex entries must describe this Mortal World, not afterlife lore or a previous world; sourceFile must start with current_world/ (for example current_world/world_setting.json), not lore/current_world/.",
                "Readable document/book inventory items need matching detail authority so /книги or document-reading surfaces can show contents.",
                "Starting items need canonical quality/rarity, durability when inspectable, and a valid equipment/accessory slot when item type implies one.",
                "Starting inventory items need the full canonical item shape: itemId/existedId, image_prompt, isConsumption, isContainer, requiresTwoHands, contentsPath, durability as a percentage string such as 100% (never bare number 100), and array-shaped text/bonus fields when present.",
                "Item journalEntries must be an array of non-empty string notes, not objects; do not write { text, turn, summary } objects into journalEntries[].",
                "equipmentSlot and accessoryForSlot must use canonical slot names, arrays of canonical slot names, or null; do not invent slots such as Pocket/Hands when the contract expects a fixed enum.",
                "Mortal World Relevant actors must be backed by a persistent NPC/faction/quest/inventory surface, or moved to Actors outside scope when they are only background scenery.",
            "The player character is not an NPC persistence target. If the current protagonist is named in Relevant actors, mark them as player character and do not create NPCsInScene/UpdateNPCs for them.",
            "NPCsInScene is only for actors physically present in currentLocationData. Offscreen voices, people behind a door, nearbyExitLocationId actors, and route pressure do not belong in NPCsInScene for the current room.",
            "Only an explicit structuredGmAuthority.actorCapabilities[] declaration with capability=canTeach and required=true requires a matching NPC with teacherProfile.canTeach=true and non-empty teacherProfile.skills[]. playerAuthoredStart prose alone never creates this requirement.",
            "Only an explicit structuredGmAuthority.actorCapabilities[] declaration with capability=canTrade and required=true requires a matching NPC with tradeState.canTrade=true, an explicit valid merchantProfile, relationshipLevel, and summary. Never infer merchantProfile from role, occupation, class, progression type, name, description, genre, or keywords. Leave tradeInventory absent until a trade vitrine is actually ready; if tradeInventory is present, it must be a full object, never a scalar/string/array placeholder.",
            "Every structuredGmAuthority.playerSkills[] entry is explicit GM authority. Preserve each matching active/passive skill and the active-skill mastery entry; do not infer any skill from playerAuthoredStart prose.",
            "worldEventRequirements requires the client-authored opening event to remain in game_state/world/world_events.json.worldEventsLog so /новости_мира is useful immediately after incarnation.",
            "Faction custom sidecars must carry full Custom State Objects: stateId/name, currentValue, minValue, maxValue, description, progressionRule { changePerTurn, description }, and thresholds[]; if you only need a narrative note, use faction_core chronicle instead.",
            "Active threats must be full objects, not strings: threatId/name/longTermGoal plus threatArchetype { motivation, method } and impactProfile { primaryTargetType, primaryTargetId, primaryTargetName, primaryImpact, baseImpactValue }. Use canonical enum values or keep activeThreats empty for vague pressure.",
            "current_location and world_map must agree on shared identity, coordinates, and navigation links. Location type, traversal, safety, biome, difficulty, faction control, factions, chronicles, and quests are setting-owned and remain absent or empty unless the GM explicitly materializes them with complete canonical data."
            },
            SafeCorrectionRules = new List<string>
            {
                "Use mortal_bootstrap_scaffold.json as the checklist and patch only targetFiles unless validation errors explicitly name another file.",
                "Preserve the accepted narrative and player-facing hooks; repair their canonical backing data instead of deleting the hooks.",
                "Prefer adding missing detail records, sidecar fields, codex links, and canonical enum values over replacing whole files with minimal skeletons."
            },
            Steps = new List<string>
            {
                "Open game_state/control/mortal_bootstrap_scaffold.json first and compare it with the listed targetFiles.",
                "Patch current-world lore/codex first: add at least one current-world codex entry with sourceFile starting current_world/ and remove stale reused-world references if validation names them.",
                "Patch readable document authority: every readable book/document item needs a concrete text/detail surface linked to that item.",
                "Patch inventory items: canonicalize quality/rarity, write durability as a percentage string such as 100% for intact inspectable items, and use valid equipment/accessory slots for wearable items.",
                "Patch complete item shape fields exactly where validation names them: image_prompt, existedId, isConsumption, isContainer, requiresTwoHands, contentsPath, durability, equipmentSlot, and accessoryForSlot.",
                "Patch item journalEntries as a JSON array of non-empty strings, not objects; preserve useful text by flattening each malformed object into one player-facing note string.",
                "Patch string-array fields as JSON arrays of strings, not scalar text or semicolon-delimited strings.",
                "Patch output/debug_logs.json Relevant actors: keep the current protagonist as player character, materialize real non-player Mortal actors through NPC/faction/quest/inventory surfaces, or move background objects to Actors outside scope with a clear reason.",
                "Patch requested training anchors only from structuredGmAuthority.actorCapabilities: for each required canTeach declaration, add the explicitly selected NPC to NPCsInScene or UpdateNPCs with teacherProfile.canTeach=true, relationshipLevel, summary, and skills[] entries containing skillId, skillName, displayName, skillKind, and masteryLevel.",
                "Patch requested trade anchors only from structuredGmAuthority.actorCapabilities: for each required canTrade declaration, add the explicitly selected NPC to NPCsInScene or UpdateNPCs with tradeState.canTrade=true, an explicit valid tradeState.merchantProfile, relationshipLevel, summary, and a player-facing role that makes /торговля_нпс discoverable. Never infer the profile from NPC prose or genre.",
                "Patch explicit GM-authored starter competencies: copy every structuredGmAuthority.playerSkills[] entry into the matching skills_active.json or skills_passive.json canonical collection and restore matching skill_mastery.json data for active skills.",
                "Patch opening world news: restore each worldEventRequirements.requiredEventIds entry in world_events.json.worldEventsLog and keep its title/description grounded in playerAuthoredStart.startingCircumstances.",
                "For a promised trader, use tradeBlockedReason only when canTrade=false, and keep it a string explaining the story gate. When canTrade=true, omit tradeBlockedReason or keep it as an empty string; never write null/object/array.",
                "For a promised trader, do not include inventory in UpdateNPCs when updating an existing NPC. Use inventory: [] only for a genuinely new full NPC object; use NPC inventory delta commands for existing NPC inventory changes.",
                "For a promised trader, do not fabricate a partial tradeInventory just to prove the NPC can trade. Leave tradeInventory absent so /торговля_нпс can request a vitrine, unless you are writing a complete canonical tradeInventory object with valid items and matching receipts.",
                "Patch NPCsInScene location scope: if an actor is behind a door, near nearbyExitLocationId, in another corridor, or only heard offscreen, remove them from NPCsInScene and represent them through narrative/location/quest/faction memory or UpdateNPCs at their actual location only when they are durable known actors.",
                "Patch factions only when the GM explicitly created them: complete faction custom/progression sidecar fields with full Custom State Objects, or move narrative-only pressure into an explicitly materialized faction chronicle. Keep neutral bootstrap faction collections empty.",
                "Patch active threats: either write complete Active Threat Objects with canonical enum values, or remove vague string-only threats and represent pressure through location events/faction chronicle.",
                "Patch location/map data: synchronize shared identity, coordinates, and navigation links. Do not invent location type, traversal, safety, biome, difficulty, faction control, faction chronicles, or quest mechanics merely to satisfy bootstrap.",
                "After repairs are complete, call Complete-BoeValidationRepair as the last action, or create validation_repair_ready.json with exact metadata from the current validation_repair_request.json."
            },
            DoNotDo = new List<string>
            {
                "Do not delete an explicitly GM-authored opening book, letter, NPC, faction, quest, map exit, or codex hook just to silence validation; neutral empty faction and quest collections do not require replacement entities.",
                "Do not copy afterlife lore or previous-world lore into current-world bootstrap files.",
                "Do not write item durability as bare numbers such as 100; use percentage strings such as 100%.",
                "Do not write item journalEntries as objects; journalEntries[] entries must be non-empty strings.",
                "Do not delete a structuredGmAuthority canTeach declaration just to avoid creating its complete teacherProfile.",
                "Do not delete a structuredGmAuthority canTrade declaration just to avoid creating its complete tradeState.",
                "Do not delete an explicit structuredGmAuthority player skill or the client-authored opening world event to reduce bootstrap scope.",
                "Do not write tradeInventory as a scalar, string, array, or empty placeholder; omit it until a complete trade vitrine exists.",
                "Do not read implementation code such as BookOfEternityClient/**/*.cs to infer bootstrap rules; use this packet, the scaffold, templates, and validation errors."
            }
        };
    }

    private static ValidationRepairHarnessPacket BuildMortalNpcLocationRepairPacket(
        IReadOnlyList<ValidationIssue> mortalNpcLocationErrors)
    {
        var actorNames = CollectRepairActorNames(mortalNpcLocationErrors)
            .OrderBy(actor => actor, StringComparer.OrdinalIgnoreCase)
            .ToList();
        var targetFiles = BuildMortalNpcTargetFiles(mortalNpcLocationErrors, includeNpcCoreWhenMissing: false);
        var touchesNpcCore = targetFiles.Contains("game_state/npcs/npc_core.json", StringComparer.OrdinalIgnoreCase);
        var touchesDebugLog = targetFiles.Contains("output/debug_logs.json", StringComparer.OrdinalIgnoreCase);
        var knownSceneLocationErrors = mortalNpcLocationErrors
            .Where(issue =>
                string.Equals(issue.Code, "npc_scene_missing_current_location_id", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(issue.Code, "npc_scene_location_mismatch", StringComparison.OrdinalIgnoreCase))
            .ToList();
        var sameTurnNewLocationErrors = mortalNpcLocationErrors
            .Where(issue =>
                string.Equals(issue.Code, "current_location_new_scene_missing_initial_id_for_npc_scene", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(issue.Code, "npc_scene_missing_initial_location_id", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(issue.Code, "npc_scene_initial_location_mismatch", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(issue.Code, "npc_initial_location_same_turn_target_unknown", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(issue.Code, "npc_same_turn_initial_location_requires_null_current_location", StringComparison.OrdinalIgnoreCase))
            .ToList();
        var steps = new List<string>
        {
            "Open Templates/MORTAL_NPC_UPDATE_TEMPLATE.md before repairing NPC location validation errors."
        };
        if (touchesNpcCore)
        {
            if (knownSceneLocationErrors.Count > 0)
            {
                var expectedRules = knownSceneLocationErrors
                    .Select(issue => string.IsNullOrWhiteSpace(issue.Expected) ? "currentLocationId = currentLocationData.locationId" : issue.Expected.Trim())
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .OrderBy(rule => rule, StringComparer.OrdinalIgnoreCase)
                    .ToList();
                steps.Add($"For NPCsInScene entries in a known current scene location, set {string.Join("; ", expectedRules)} and keep initialLocationId as JSON null unless validation separately reports a same-turn new-location initialId for that entry.");
            }
            if (sameTurnNewLocationErrors.Count > 0)
            {
                steps.Add("For NPCsInScene entries in a same-turn new location, set initialLocationId to currentLocationData.initialId or the matching newLocations.initialId, set currentLocationId to JSON null, and keep currentLocationName as the visible location name.");
            }
            steps.Add("If a persistent NPC location is missing in game_state/npcs/npc_core.json, set currentLocationId/currentLocationName from the intended scene or move command; do not leave both location ids empty.");
            steps.Add("Keep NPCsInScene and NPCs entries as full canonical objects; do not replace them with short display-only rows.");
        }
        if (touchesDebugLog)
        {
            steps.Add("In output/debug_logs.json.gm_thoughts_markdown, add a current location / Текущая локация subpoint inside each listed NPC reasoning block.");
            steps.Add("The current-location subpoint must say where the NPC is now and whether the NPC stays there or moves during this turn.");
        }
        steps.Add("After repairs are complete, call Complete-BoeValidationRepair as the last action, or create validation_repair_ready.json with exact metadata from the current validation_repair_request.json.");

        return new ValidationRepairHarnessPacket
        {
            Kind = "mortal_npc_location_repair",
            Priority = "high",
            Title = "Mortal NPC same-turn location repair",
            TargetFiles = targetFiles,
            TemplateRefs = new List<string> { "Templates/MORTAL_NPC_UPDATE_TEMPLATE.md" },
            CanonicalActorNames = actorNames,
            ExpectedShape = new List<string>
            {
                "Same-turn scene NPCs live in game_state/npcs/npc_core.json under NPCsInScene and/or NPCs as full objects, not as partial name-only stubs.",
                "NPCsInScene is only for actors physically present in currentLocationData; actors heard behind a door, placed near a nearbyExitLocationId, or waiting in another corridor are offscreen for the current scene.",
                "For a known current location: NPCsInScene.currentLocationId = currentLocationData.locationId, initialLocationId = JSON null, currentLocationName = visible current location name.",
                "For a same-turn new location: NPCsInScene.initialLocationId = currentLocationData.initialId or matching newLocations.initialId, currentLocationId = JSON null, currentLocationName = visible current location name.",
                "For an already persistent NPC moved by the turn: keep permanent npcId, set currentLocationId/currentLocationName to the destination, and do not invent a same-turn initialId."
            },
            SafeCorrectionRules = new List<string>
            {
                "Patch only game_state/npcs/npc_core.json unless another file is explicitly listed by validation_repair_request.json.",
                "Use the current turn/request location authority already present in session state or pending snapshot; do not invent a location id from prose.",
                "Preserve intended NPC personality, goals, relationshipLock, journals, and unrelated NPC entries while repairing only the location fields."
            },
            Steps = steps,
            DoNotDo = new List<string>
            {
                "Do not read implementation code such as BookOfEternityClient/**/*.cs to infer NPC location rules.",
                "Do not silence the issue by deleting a meaningful NPC from Relevant actors or NPCsInScene.",
                "Do not keep an offscreen voice, nearbyExitLocationId actor, or corridor/door pressure in NPCsInScene for the current room.",
                "Do not set currentLocationId to JSON null for NPCsInScene when validation expects currentLocationId for a known current location.",
                "Do not set currentLocationId to a non-null value for a same-turn new location NPC that validator says must use initialLocationId."
            }
        };
    }

    private static ValidationRepairHarnessPacket BuildMortalNpcScopeRepairPacket(
        IReadOnlyList<ValidationIssue> mortalNpcScopeErrors)
    {
        var actorNames = CollectRepairActorNames(mortalNpcScopeErrors)
            .OrderBy(actor => actor, StringComparer.OrdinalIgnoreCase)
            .ToList();
        var targetFiles = BuildMortalNpcTargetFiles(mortalNpcScopeErrors, includeNpcCoreWhenMissing: true);
        if (!targetFiles.Contains("output/debug_logs.json", StringComparer.OrdinalIgnoreCase))
            targetFiles.Add("output/debug_logs.json");

        return new ValidationRepairHarnessPacket
        {
            Kind = "mortal_npc_scope_repair",
            Priority = "high",
            Title = "Mortal NPC relevant-actor scope repair",
            TargetFiles = targetFiles,
            TemplateRefs = new List<string> { "Templates/MORTAL_NPC_UPDATE_TEMPLATE.md" },
            CanonicalActorNames = actorNames,
            ExpectedShape = new List<string>
            {
                "Every structured Mortal NPC update must be covered by output/debug_logs.json NPC scope: the actor name appears in Relevant actors and has a reasoning block.",
                "The reasoning block must include current location, situation, thoughts, and actions for that actor in the accepted turn.",
                "Every non-player Mortal relevant actor must have a persistent NPC/faction/quest/inventory surface, or be moved to Actors outside scope when it is only background context.",
                "When a direct-speaking or directly addressed actor is an NPC, game_state/npcs/npc_core.json must keep or restore a full canonical NPC object for that actor.",
                "If the NPC is only offscreen continuity, route color, background pressure, or unchanged existing state, do not emit UpdateNPCs/NPCsInScene/other structured NPC updates for that actor this turn.",
                "Actors outside scope is for named actors not structurally changed this turn; it is not enough when UpdateNPCs changes that actor."
            },
            SafeCorrectionRules = new List<string>
            {
                "Add the actor to Relevant actors and a full reasoning block only when the accepted player action truly changes, addresses, observes, or depends on that actor this turn.",
                "Materialize or restore a missing direct NPC as a full canonical NPC object instead of deleting the actor from the scene to silence validation.",
                "Remove the structured NPC update when the actor is merely offscreen, unchanged, or mentioned only as context; preserve the information in narrative, quest log, location summary, or Actors outside scope instead.",
                "Keep canonical NPC names identical across Relevant actors, reasoning headings, and game_state/npcs/npc_core.json.",
                "Preserve unrelated NPC state while repairing only the out-of-scope update or its missing reasoning coverage."
            },
            Steps = new List<string>
            {
                "Open output/debug_logs.json and game_state/npcs/npc_core.json before repairing structured_npc_update_out_of_scope.",
                "For each named actor, decide whether the accepted turn really changed or directly used that NPC.",
                "For mortal_relevant_actor_missing_persistence, either restore/materialize the missing persistent NPC/faction/quest/inventory surface, or move the actor to Actors outside scope if the actor was only background context.",
                "If the actor is a direct-speaking or directly addressed NPC, keep a full persistent NPC object in game_state/npcs/npc_core.json and preserve existing canonical fields from the pre-turn state when available.",
                "If yes, add the exact canonical name to Relevant actors and add a matching reasoning block with current location, situation, thoughts, and actions.",
                "If no, remove that actor's structured NPC update from this turn and keep any useful clue in non-NPC structured surfaces such as quest log or current_location summary.",
                "After repairs are complete, call Complete-BoeValidationRepair as the last action, or create validation_repair_ready.json with exact metadata from the current validation_repair_request.json."
            },
            DoNotDo = new List<string>
            {
                "Do not silence the error by deleting the NPC permanently when only the current turn update is out of scope.",
                "Do not add an actor to Relevant actors without a matching reasoning block.",
                "Do not keep a structured NPC update for an offscreen unchanged actor just because the actor exists in npc_core.json.",
                "Do not read implementation code such as BookOfEternityClient/**/*.cs to infer scope rules; use this packet, the validation request, and GM docs."
            }
        };
    }
}
