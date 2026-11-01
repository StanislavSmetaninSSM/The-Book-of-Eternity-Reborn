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

    private static ValidationRepairHarnessPacket BuildMortalCombatStateRepairPacket(
        IReadOnlyList<ValidationIssue> mortalCombatStateErrors)
    {
        var targetFiles = mortalCombatStateErrors
            .Select(issue => NormalizeRepairTargetPath(issue.FilePath))
            .Where(path => !string.IsNullOrWhiteSpace(path))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
            .ToList();

        foreach (var path in new[]
                 {
                     "game_state/combat/enemies.json",
                     "game_state/combat/allies.json",
                     "game_state/combat/combat_log.json"
                 })
        {
            if (!targetFiles.Contains(path, StringComparer.OrdinalIgnoreCase))
                targetFiles.Add(path);
        }

        return new ValidationRepairHarnessPacket
        {
            Kind = "mortal_combat_state_repair",
            Priority = "high",
            Title = "Mortal combat state materialization repair",
            TargetFiles = targetFiles,
            TemplateRefs = new List<string> { "Templates/MORTAL_COMBAT_STATE_TEMPLATE.md" },
            ExpectedShape = new List<string>
            {
                "A Mortal World turn that explicitly resolves open combat and updates XP, active skill mastery, or combat resources must leave a player-inspectable combat surface.",
                "At minimum, write game_state/combat/combat_log.json with a recent combat summary that /бой can show.",
                "If enemies or allies remain tactically relevant after the turn, also write game_state/combat/enemies.json and game_state/combat/allies.json.",
                "If the fight fully ended in the same turn, enemies may be absent or marked defeated, but combat_log.json must still explain what happened."
            },
            SafeCorrectionRules = new List<string>
            {
                "Repair the already accepted combat scene; do not invent a different enemy, reroll dice, or rewrite the player action.",
                "Preserve the XP, skill mastery, and resource changes already written unless validation explicitly rejects them.",
                "Use player-facing Russian summaries in combat_log.json, while keeping canonical JSON keys and enum values valid.",
                "If there is no ongoing combat after the exchange, make that explicit in the combat log instead of leaving /бой empty."
            },
            Steps = new List<string>
            {
                "Open Templates/MORTAL_COMBAT_STATE_TEMPLATE.md and validation_repair_request.json first.",
                "Write or patch game_state/combat/combat_log.json so it summarizes the same combat exchange described in output/narrative_response.json.",
                "If the enemy still exists or its defeated state matters, write game_state/combat/enemies.json with the opponent, health/poise state, actions, and defeated/retreated status.",
                "If named allies participated tactically, write game_state/combat/allies.json with their roles and current state.",
                "Include every touched combat file in the repair completion evidence.",
                "After repairs are complete, call Complete-BoeValidationRepair as the last action, or create validation_repair_ready.json with exact metadata from the current validation_repair_request.json."
            },
            DoNotDo = new List<string>
            {
                "Do not delete XP, skill mastery, or player_status changes just to silence this repair.",
                "Do not leave /бой empty after a player-facing open combat scene.",
                "Do not read implementation code such as BookOfEternityClient/**/*.cs to infer combat shape; use this packet, the compact template, and session files."
            }
        };
    }

    private static ValidationRepairHarnessPacket BuildMortalSkillProgressionShapeRepairPacket(
        IReadOnlyList<ValidationIssue> skillShapeErrors)
    {
        var targetFiles = skillShapeErrors
            .Select(issue => NormalizeMortalSkillProgressionRepairTargetPath(issue.FilePath))
            .Where(path => !string.IsNullOrWhiteSpace(path))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
            .ToList();

        foreach (var path in new[]
                 {
                     "game_state/control/pending_training_showcase_requests.json",
                     "game_state/player/skills_active.json",
                     "game_state/player/skills_passive.json",
                     "game_state/player/skill_mastery.json"
                 })
        {
            if (!targetFiles.Contains(path, StringComparer.OrdinalIgnoreCase))
                targetFiles.Add(path);
        }

        var actorNames = CollectRepairActorNames(skillShapeErrors)
            .OrderBy(actor => actor, StringComparer.OrdinalIgnoreCase)
            .ToList();
        var exactFieldCorrections = skillShapeErrors
            .Select(BuildExactFieldCorrection)
            .Where(correction => !string.IsNullOrWhiteSpace(correction.Path))
            .DistinctBy(correction => correction.Path, StringComparer.OrdinalIgnoreCase)
            .OrderBy(correction => correction.Path, StringComparer.OrdinalIgnoreCase)
            .ToList();

        return new ValidationRepairHarnessPacket
        {
            Kind = "mortal_skill_progression_shape_repair",
            Priority = "high",
            Title = "Mortal skill progression array-shape repair",
            TargetFiles = targetFiles,
            TemplateRefs = new List<string>
            {
                "Templates/MORTAL_SKILL_PROGRESSION_TEMPLATE.md",
                "Examples/E_CLI_Training_Showcases.txt"
            },
            CanonicalActorNames = actorNames,
            ExactFieldCorrections = exactFieldCorrections,
            ExpectedShape = new List<string>
            {
                "Mortal skill progression fields are always JSON arrays, even when the turn changes exactly one skill.",
                "game_state/player/skills_active.json.activeSkillChanges, removeActiveSkills, and related active skill change fields must be arrays, not a single object.",
                "game_state/player/skills_passive.json.passiveSkillChanges, removePassiveSkills, and related passive skill change fields must be arrays, not a single object.",
                "game_state/player/skill_mastery.json.skillMasteryChanges must be an array, not a single mastery object.",
                "If game_state/control/pending_training_showcase_requests.json contains requestKind = mortal_training_skill_evolution, use that pending paid request as authority for targetKind, targetName, currentLevel, nextLevel, and purchaseReceipt.",
                "For an active skill unlock from paid training, write activeSkillChanges as an array with the complete active skill object and skillMasteryChanges as an array with the matching mastery entry.",
                "For a passive skill unlock or passive mastery training, write passiveSkillChanges as an array and do not invent an active skill solely to satisfy skillMasteryChanges."
            },
            SafeCorrectionRules = new List<string>
            {
                "Do not charge money, experience, ink feathers, or any other currency again; a paid training showcase purchase already created the pending request and receipt.",
                "If a field contains a valid single object where an array is expected, wrap that single object in an array instead of rewriting unrelated skill data.",
                "Patch only the listed player skill files and the directly related pending training request evidence; do not rewrite unrelated NPCs, inventory, combat, map, or world state.",
                "Preserve localized names, descriptions, structuredBonuses, playerStatBonus, mastery progress, sourceCap, and teacher identity while correcting array shape.",
                "If details.targetKind in the pending request conflicts with the malformed file, follow details.targetKind and remove the malformed change from the wrong skill surface."
            },
            Steps = new List<string>
            {
                "Open game_state/control/validation_repair_request.json first and inspect harnessRepairPackets[].exactFieldCorrections for the malformed paths.",
                "Open game_state/control/pending_training_showcase_requests.json and check whether a requestKind = mortal_training_skill_evolution is still pending.",
                "Open the listed player skill files: game_state/player/skills_active.json, game_state/player/skills_passive.json, and game_state/player/skill_mastery.json.",
                "For each malformed activeSkillChanges, passiveSkillChanges, removeActiveSkills, removePassiveSkills, or skillMasteryChanges field, wrap a valid single object/value into an array or replace malformed scalar content with the correct empty array when validation expected an empty collection.",
                "When a paid training request is pending, make the repaired skill change match details.targetKind: active targets use activeSkillChanges plus skillMasteryChanges; passive targets use passiveSkillChanges and no invented active skill.",
                "Do not apply another purchase, refund, or new turn result; this repair only fixes the shape and placement of the already authored skill progression.",
                "After repairs are complete, call Complete-BoeValidationRepair as the last action, or create validation_repair_ready.json with exact metadata from the current validation_repair_request.json."
            },
            DoNotDo = new List<string>
            {
                "Do not leave activeSkillChanges, passiveSkillChanges, removeActiveSkills, removePassiveSkills, or skillMasteryChanges as a single object.",
                "Do not delete pending_training_showcase_requests.json to silence the error.",
                "Do not charge resources a second time or create a second training receipt.",
                "Do not read implementation code such as BookOfEternityClient/**/*.cs to infer skill progression shape; use this packet, MORTAL_SKILL_PROGRESSION_TEMPLATE.md, Examples/E_CLI_Training_Showcases.txt, and canonical session files."
            }
        };
    }

    private static ValidationRepairHarnessPacket BuildMortalTrainingSkillEvolutionRepairPacket(
        IReadOnlyList<ValidationIssue> skillEvolutionErrors)
    {
        var targetFiles = skillEvolutionErrors
            .Select(issue => NormalizeRepairTargetPath(issue.FilePath))
            .Where(path => !string.IsNullOrWhiteSpace(path))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
            .ToList();

        foreach (var path in new[]
                 {
                     "game_state/control/pending_training_showcase_requests.json",
                     "game_state/player/skills_active.json",
                     "game_state/player/skills_passive.json",
                     "game_state/player/skill_mastery.json",
                     "game_state/npcs/npc_core.json"
                 })
        {
            if (!targetFiles.Contains(path, StringComparer.OrdinalIgnoreCase))
                targetFiles.Add(path);
        }

        var actorNames = CollectRepairActorNames(skillEvolutionErrors)
            .OrderBy(actor => actor, StringComparer.OrdinalIgnoreCase)
            .ToList();
        var exactFieldCorrections = skillEvolutionErrors
            .Select(BuildExactFieldCorrection)
            .Where(correction => !string.IsNullOrWhiteSpace(correction.Path))
            .DistinctBy(correction => correction.Path, StringComparer.OrdinalIgnoreCase)
            .OrderBy(correction => correction.Path, StringComparer.OrdinalIgnoreCase)
            .ToList();

        return new ValidationRepairHarnessPacket
        {
            Kind = "mortal_training_skill_evolution_repair",
            Priority = "high",
            Title = "Mortal training skill evolution target-kind repair",
            TargetFiles = targetFiles,
            TemplateRefs = new List<string> { "Examples/E_CLI_Training_Showcases.txt" },
            CanonicalActorNames = actorNames,
            ExactFieldCorrections = exactFieldCorrections,
            ExpectedShape = new List<string>
            {
                "A paid mortal training request in game_state/control/pending_training_showcase_requests.json is authoritative for the trained target.",
                "Open the pending request with requestKind = mortal_training_skill_evolution and follow details.targetKind exactly.",
                "If details.targetKind is passive_skill_mastery or passive_skill_unlock, patch game_state/player/skills_passive.json through passiveSkillChanges, not skillMasteryChanges.",
                "If details.targetKind is active_skill_mastery or active_skill_unlock, patch game_state/player/skills_active.json and only reference skills that already exist in canonical active skills state unless the request is an unlock.",
                "When updating passive skills, preserve structuredBonuses, playerStatBonus, source tags, and player-facing Russian descriptions unless the pending request explicitly changes them."
            },
            SafeCorrectionRules = new List<string>
            {
                "Do not charge money, experience, ink feathers, or any other currency again; the local showcase purchase already created the paid pending request and receipt.",
                "Resolve only the pending training evolution described by the current validation request; do not rewrite unrelated skills, NPCs, inventory, or world state.",
                "If validation reports skill_mastery_unknown_active_skill for a passive skill, remove the invalid skillMasteryChanges active entry and express the same evolution through passiveSkillChanges.",
                "Preserve structuredBonuses, playerStatBonus, source labels, and existing localized descriptions when patching passive skill data.",
                "Use the teacher's canonical skill profile in game_state/npcs/npc_core.json only to confirm caps and story wording; pending details.targetKind remains the source of truth for active vs passive."
            },
            Steps = new List<string>
            {
                "Open validation_repair_request.json and game_state/control/pending_training_showcase_requests.json first.",
                "Find the pending request with requestKind = mortal_training_skill_evolution and read details.targetKind, details.targetName, details.currentLevel, details.nextLevel, and purchaseReceipt.",
                "If details.targetKind says passive_skill_mastery or passive_skill_unlock, remove the invalid active skillMasteryChanges entry and patch game_state/player/skills_passive.json with passiveSkillChanges for that same target.",
                "If details.targetKind says active_skill_mastery or active_skill_unlock, keep the change on the active skill surface and ensure the active skill name/id exists or is created by the same unlock request.",
                "Preserve structuredBonuses, playerStatBonus, mastery progress, sourceCap, teacher identity, and player-facing Russian skill prose when patching the skill object.",
                "Include the touched skill file and pending request in repair completion evidence.",
                "After repairs are complete, call Complete-BoeValidationRepair as the last action, or create validation_repair_ready.json with exact metadata from the current validation_repair_request.json."
            },
            DoNotDo = new List<string>
            {
                "Do not invent an active skill just because skillMasteryChanges rejected a passive skill name.",
                "Do not delete pending_training_showcase_requests.json to silence the error.",
                "Do not apply a second purchase cost or refund the existing purchase unless validation explicitly asks for a receipt rollback.",
                "Do not read implementation code such as BookOfEternityClient/**/*.cs to infer training rules; use this packet, Examples/E_CLI_Training_Showcases.txt, the pending request, and canonical session files."
            }
        };
    }

    private static ValidationRepairHarnessPacket BuildTrainingShowcaseSnapshotHashRepairPacket(
        IReadOnlyList<ValidationIssue> trainingShowcaseSnapshotErrors)
    {
        var targetFiles = trainingShowcaseSnapshotErrors
            .Select(issue => NormalizeTrainingShowcaseRepairTargetPath(issue.FilePath))
            .Where(path => !string.IsNullOrWhiteSpace(path))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (!targetFiles.Contains("game_state/control/pending_training_showcase_requests.json", StringComparer.OrdinalIgnoreCase))
            targetFiles.Add("game_state/control/pending_training_showcase_requests.json");

        var actorNames = CollectRepairActorNames(trainingShowcaseSnapshotErrors)
            .OrderBy(actor => actor, StringComparer.OrdinalIgnoreCase)
            .ToList();
        var exactFieldCorrections = trainingShowcaseSnapshotErrors
            .Select(BuildExactFieldCorrection)
            .Where(correction => !string.IsNullOrWhiteSpace(correction.Path))
            .DistinctBy(correction => correction.Path, StringComparer.OrdinalIgnoreCase)
            .OrderBy(correction => correction.Path, StringComparer.OrdinalIgnoreCase)
            .ToList();

        return new ValidationRepairHarnessPacket
        {
            Kind = "training_showcase_snapshot_hash_repair",
            Priority = "high",
            Title = "Training showcase source snapshot hash repair",
            TargetFiles = targetFiles,
            TemplateRefs = new List<string> { "Examples/E_CLI_Training_Showcases.txt" },
            CanonicalActorNames = actorNames,
            ExactFieldCorrections = exactFieldCorrections,
            ExpectedShape = new List<string>
            {
                "trainingShowcase/mentorTrainingShowcase must describe the current teacher or mentor profile and carry sourceActorSnapshotHash that matches that same current source actor.",
                "If the repair only refreshes the showcase, preserve the source actor's profile and set sourceActorSnapshotHash exactly from exactFieldCorrections[].",
                "If the repair also changes the teacher profile, finish those profile fields first, then use exactFieldCorrections[] from the current validation request as the authoritative hash for this repair attempt.",
                "The showcase must remain a data vitrines surface only: it lists offers, costs, source caps, and conditions; it does not spend player currency or grant skills directly."
            },
            SafeCorrectionRules = new List<string>
            {
                "Open game_state/control/pending_training_showcase_requests.json first and preserve the requested requestId/requestKind/sourceActorId/sourceActorName/realm.",
                "Apply exactFieldCorrections[] path -> expected replacements before trying to infer hashes from prose.",
                "Patch only the listed teacher/mentor showcase and directly related source actor fields required by validation; preserve unrelated NPCs, guardians, memories, relationships, and location state.",
                "Do not update an unrelated NPC teacher just because the file contains multiple actors."
            },
            Steps = new List<string>
            {
                "Open game_state/control/validation_repair_request.json and game_state/control/pending_training_showcase_requests.json.",
                "Open the target source actor file named in targetFiles, usually game_state/npcs/npc_core.json for Mortal teachers or game_state/meta/afterlife_entity_profiles.json for afterlife mentors.",
                "Find the teacher/mentor showcase field named by exactFieldCorrections[].path.",
                "Set each exactFieldCorrections[].path to exactFieldCorrections[].expected.",
                "Recheck that every offer still obeys sourceCap <= teacher skill/art level and that costs are present; do not apply the purchase locally.",
                "After repairs are complete, call Complete-BoeValidationRepair as the last action, or create validation_repair_ready.json with exact metadata from the current validation_repair_request.json."
            },
            DoNotDo = new List<string>
            {
                "Do not guess sourceActorSnapshotHash manually.",
                "Do not delete the training showcase just to silence stale-hash validation while a pending training showcase request exists.",
                "Do not spend player money, experience, ink feathers, or change skill mastery during showcase repair.",
                "Do not read implementation code such as BookOfEternityClient/**/*.cs to infer training rules; use this packet, Examples/E_CLI_Training_Showcases.txt, the validation request, and pending_training_showcase_requests.json."
            }
        };
    }

    private static ValidationRepairHarnessPacket BuildAfterlifeSpiritualConflictActionCostRepairPacket(
        IReadOnlyList<ValidationIssue> actionCostErrors)
    {
        var targetFiles = actionCostErrors
            .Select(issue => NormalizeRepairTargetPath(issue.FilePath))
            .Where(path => !string.IsNullOrWhiteSpace(path))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (!targetFiles.Contains("game_state/meta/afterlife_spiritual_conflict_state.json", StringComparer.OrdinalIgnoreCase))
            targetFiles.Add("game_state/meta/afterlife_spiritual_conflict_state.json");

        var issueDetails = actionCostErrors
            .Select(DescribeAfterlifeActionCostRepairIssue)
            .Where(detail => !string.IsNullOrWhiteSpace(detail))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(detail => detail, StringComparer.OrdinalIgnoreCase)
            .ToList();
        var issueSummary = issueDetails.Count == 0
            ? "see validation_repair_request.json.errors for exact paths and expected/actual values"
            : string.Join("; ", issueDetails);
        var exactFieldCorrections = actionCostErrors
            .Select(BuildExactFieldCorrection)
            .Where(correction => !string.IsNullOrWhiteSpace(correction.Path))
            .DistinctBy(correction => correction.Path, StringComparer.OrdinalIgnoreCase)
            .OrderBy(correction => correction.Path, StringComparer.OrdinalIgnoreCase)
            .ToList();

        return new ValidationRepairHarnessPacket
        {
            Kind = "afterlife_spiritual_conflict_action_cost_repair",
            Priority = "high",
            Title = "Afterlife spiritual conflict action-cost sequence repair",
            TargetFiles = targetFiles,
            TemplateRefs = new List<string>
            {
                "OtherGuides/Afterlife_Contract_Matrix.md",
                "Examples/E_CLI_Afterlife_Turns.txt"
            },
            ExactFieldCorrections = exactFieldCorrections,
            ExpectedShape = new List<string>
            {
                "For every current exchange, actionCostAudit.<side>.before must equal the previous current exchange's actionCostAudit.<side>.after; for the first current exchange, before must equal the read-only pre-turn projected spiritual_action_points current value for that side.",
                "For paid actions, actionCostAudit.<side>.after must equal before - effectiveCost; for recovery, after must follow the documented recovery formula and stay within max.",
                "The GM-authored conflict keeps only actionCostAudit evidence; spiritual_action_points state, immutable resource history, and resource-owner bindings are client-owned and are not mirrored into the conflict.",
                "Dice values, operationType/finalOperationType, incomingAction, maneuver outcome, specialArtAudit, and matchupAudit must remain authority-bound; repair arithmetic and audit fields without inventing a new exchange."
            },
            SafeCorrectionRules = new List<string>
            {
                "Use validation_repair_request.json.errors as the immediate repair checklist; its expected/actual values are authoritative for the listed fields.",
                "Recompute actionCostAudit sequentially across activeConflict.exchangeLog from the read-only pre-turn projected spiritual_action_points baseline and the previous exchange result; do not copy a later current value backward.",
                "When fixing a before value, recompute only the dependent actionCostAudit fields for the same side; leave client-owned resource state, resource history, and owner bindings untouched.",
                "If the request includes dice authorization errors, replace only the unauthorized dice/audit value with a pre-generated value from pending-turn authority; do not roll new dice manually.",
                "If the request includes maneuver strain errors, keep strain changes out of activeConflict unless the player action was the documented strain-conversion maneuver."
            },
            Steps = new List<string>
            {
                "Open game_state/control/validation_repair_request.json first and repair only the listed validation errors in place.",
                $"Patch these action-cost/audit fields exactly: {issueSummary}.",
                "Use exactFieldCorrections[] as the machine-readable checklist: set each listed actionCostAudit path to expected, then recompute only dependent audit values.",
                "In game_state/meta/afterlife_spiritual_conflict_state.json, inspect activeConflict.exchangeLog in order and recompute actionCostAudit.player/actionCostAudit.opposition before/after values from the previous current exchange.",
                "For every listed exchangeLog[n], patch actionCostAudit.<side>.before to the expected projected spiritual_action_points value, then recompute that side's after using effectiveCost or the recovery rule.",
                "Use pending_turn_snapshot and control authority only as read-only baselines for pre-turn projected spiritual_action_points, dice, and authorized operations.",
                "After file repairs are complete, call Complete-BoeValidationRepair as the last action, or create game_state/control/validation_repair_ready.json with exact sessionId/requestId/turnNumber from the current validation_repair_request.json."
            },
            DoNotDo = new List<string>
            {
                "Do not create a new turn or write ready/turn_complete.json during validation repair.",
                "Do not edit game_state/control/pending_turn_snapshot or other authority snapshot files; use pending_turn_snapshot only as a read-only baseline.",
                "Do not author activeConflict.actionEconomy or resourceOwnerBindings; both are forbidden GM mirrors of client-owned resource authority.",
                "Do not edit game_state/resources/resource_definitions.json, resource_state.json, resource_history.json, or resource_owner_authority.json during GM validation repair; they are one client-owned guarded quartet.",
                "Do not change player prose, operation choices, dice rolls, special art ids, or exchange outcomes just to silence arithmetic validation.",
                "Do not read implementation code such as BookOfEternityClient/**/*.cs to infer repair rules; use this packet, validation_repair_request.json, afterlife docs/examples, and session control files."
            }
        };
    }
}
