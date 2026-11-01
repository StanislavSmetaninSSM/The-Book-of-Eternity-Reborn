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

    private static bool IsMortalNpcFullObjectRepairIssue(ValidationIssue issue)
    {
        if (!IsMortalNpcRepairPath(issue.FilePath))
            return false;

        if (IsMortalNpcLocationRepairIssue(issue) || IsMortalNpcInventoryUpdateRepairIssue(issue) || IsMortalNpcRelationshipEnumRepairIssue(issue))
            return false;

        if (string.Equals(issue.Code, "npc_full_object_missing_required_fields", StringComparison.OrdinalIgnoreCase))
            return true;

        if (string.Equals(issue.Code, "expected_object", StringComparison.OrdinalIgnoreCase) &&
            IsMortalNpcSkillObjectRepairPath(issue.FilePath))
            return true;

        var code = issue.Code ?? string.Empty;
        return code.StartsWith("npc_", StringComparison.OrdinalIgnoreCase) &&
               (code.Contains("object", StringComparison.OrdinalIgnoreCase) ||
                code.Contains("null", StringComparison.OrdinalIgnoreCase) ||
                code.Contains("shape", StringComparison.OrdinalIgnoreCase));
    }

    private static bool IsMortalNpcScopeRepairIssue(ValidationIssue issue)
    {
        return string.Equals(issue.Code, "structured_npc_update_out_of_scope", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(issue.Code, "mortal_relevant_actor_missing_persistence", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsNpcScopeDeclarationRepairIssue(ValidationIssue issue)
    {
        return (issue.Code ?? string.Empty).ToLowerInvariant() switch
        {
            "missing_scope_mode" => true,
            "invalid_scope_mode" => true,
            "missing_relevant_actors_field" => true,
            "empty_relevant_actors_for_mode" => true,
            "missing_scope_relevance_reason" => true,
            "missing_out_of_scope_actors_field" => true,
            "missing_scope_out_of_scope_reason" => true,
            "missing_actor_reasoning_section" => true,
            "directly_addressed_actor_missing_from_scope" => true,
            "structured_resident_update_out_of_scope" => true,
            "structured_shining_actor_update_out_of_scope" => true,
            "structured_shining_faction_update_out_of_scope" => true,
            "structured_afterlife_entity_update_out_of_scope" => true,
            _ => false
        };
    }

    private static bool IsMortalNpcInventoryUpdateRepairIssue(ValidationIssue issue)
    {
        return IsMortalNpcRepairPath(issue.FilePath) &&
               string.Equals(issue.Code, "npc_existing_inventory_resend_forbidden", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsMortalBootstrapMaterializationRepairIssue(ValidationIssue issue)
    {
        return (issue.Code ?? string.Empty).ToLowerInvariant() switch
        {
            "bootstrap_codex_missing_current_world_entries" => true,
            "mortal_bootstrap_reused_previous_world_lore" => true,
            "canonical_faction_custom_state_missing_required_fields" => true,
            "canonical_faction_custom_state_missing_progression_rule" => true,
            "readable_document_missing_detail_authority" => true,
            "item_invalid_quality" => true,
            "item_missing_accessory_for_slot" => true,
            "item_missing_equipment_slot" => true,
            "location_faction_control_invalid_type" => true,
            "world_map_active_threat_missing_archetype" => true,
            "codex_related_entry_unknown_target" => true,
            "npc_contract_unknown_top_level_key" => true,
            "flexible_state_unknown_top_level_key" => IsMortalBootstrapGenericShapeRepairIssue(issue),
            "mortal_bootstrap_requested_teacher_missing" => true,
            "mortal_bootstrap_requested_trade_missing" => true,
            "mortal_bootstrap_explicit_competency_missing" => true,
            "mortal_bootstrap_world_event_missing" => true,
            "missing_required_string" => IsMortalBootstrapGenericShapeRepairIssue(issue),
            "missing_required_boolean_field" => IsMortalBootstrapGenericShapeRepairIssue(issue),
            "expected_string_array" => IsMortalBootstrapGenericShapeRepairIssue(issue),
            "missing_required_string_array_field" => IsMortalBootstrapGenericShapeRepairIssue(issue),
            "item_invalid_accessory_slot" => IsMortalBootstrapItemRepairPath(issue.FilePath),
            "item_invalid_equipment_slot" => IsMortalBootstrapItemRepairPath(issue.FilePath),
            "validation_error" => IsMortalBootstrapGenericShapeRepairIssue(issue),
            _ => false
        };
    }

    private static bool IsMortalBootstrapGenericShapeRepairIssue(ValidationIssue issue)
    {
        return IsMortalBootstrapItemRepairPath(issue.FilePath) ||
               IsMortalBootstrapCodexRepairPath(issue.FilePath) ||
               IsMortalBootstrapCurrentWorldRepairPath(issue.FilePath);
    }

    private static bool IsMortalBootstrapItemRepairPath(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
            return false;

        return path.Replace('\\', '/').StartsWith("game_state/inventory/items.json", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsMortalBootstrapCodexRepairPath(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
            return false;

        return path.Replace('\\', '/').StartsWith("lore/codex_entries.json", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsMortalBootstrapCurrentWorldRepairPath(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
            return false;

        return path.Replace('\\', '/').StartsWith("lore/current_world/", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsMortalNpcRelationshipEnumRepairIssue(ValidationIssue issue)
    {
        return IsMortalNpcRepairPath(issue.FilePath) &&
               (string.Equals(issue.Code, "npc_attitude_relationship_tier_mismatch", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(issue.Code, "npc_invalid_cultural_stance", StringComparison.OrdinalIgnoreCase));
    }

    private static bool IsMortalNpcReferenceRepairIssue(ValidationIssue issue)
    {
        return string.Equals(issue.Code, "npc_journal_unknown_npc_reference", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsMortalCombatStateRepairIssue(ValidationIssue issue)
    {
        return string.Equals(issue.Code, "mortal_combat_state_missing", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsMortalTrainingSkillEvolutionRepairIssue(ValidationIssue issue)
    {
        return string.Equals(issue.Code, "skill_mastery_unknown_active_skill", StringComparison.OrdinalIgnoreCase) &&
               NormalizeRepairTargetPath(issue.FilePath).StartsWith("game_state/player/skill_mastery.json", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsMortalSkillProgressionShapeRepairIssue(ValidationIssue issue)
    {
        var code = issue.Code ?? string.Empty;
        if (!string.Equals(code, "expected_array", StringComparison.OrdinalIgnoreCase) &&
            !string.Equals(code, "expected_array_of_objects", StringComparison.OrdinalIgnoreCase) &&
            !string.Equals(code, "expected_string_array", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var path = (issue.FilePath ?? string.Empty).Replace('\\', '/');
        if (!path.StartsWith("game_state/player/skills_active.json", StringComparison.OrdinalIgnoreCase) &&
            !path.StartsWith("game_state/player/skills_passive.json", StringComparison.OrdinalIgnoreCase) &&
            !path.StartsWith("game_state/player/skill_mastery.json", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        return path.Contains("activeSkillChanges", StringComparison.OrdinalIgnoreCase) ||
               path.Contains("passiveSkillChanges", StringComparison.OrdinalIgnoreCase) ||
               path.Contains("skillMasteryChanges", StringComparison.OrdinalIgnoreCase) ||
               path.Contains("removeActiveSkills", StringComparison.OrdinalIgnoreCase) ||
               path.Contains("removePassiveSkills", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsTrainingShowcaseSnapshotRepairIssue(ValidationIssue issue)
    {
        return string.Equals(issue.Code, "training_showcase_stale_source_actor_snapshot", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsMortalNpcRepairPath(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
            return false;

        var normalized = path.Replace('\\', '/');
        return normalized.StartsWith("game_state/npcs/", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsMortalNpcSkillObjectRepairPath(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
            return false;

        var normalized = path.Replace('\\', '/');
        return normalized.StartsWith("game_state/npcs/npc_core.json", StringComparison.OrdinalIgnoreCase) &&
               (normalized.Contains(".activeSkills[", StringComparison.OrdinalIgnoreCase) ||
                normalized.Contains(".passiveSkills[", StringComparison.OrdinalIgnoreCase));
    }

    private static bool IsAfterlifeSpiritualConflictActionCostRepairIssue(ValidationIssue issue)
    {
        var code = issue.Code ?? string.Empty;
        if (!code.StartsWith("afterlife_conflict_", StringComparison.OrdinalIgnoreCase))
            return false;

        return code.Contains("action_cost", StringComparison.OrdinalIgnoreCase) ||
               code.Contains("action_economy", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(code, "afterlife_conflict_dice_value_not_authorized", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(code, "afterlife_conflict_maneuver_changes_strain", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsGuardianTradeInventoryResolutionRepairIssue(ValidationIssue issue)
    {
        return (issue.Code ?? string.Empty).ToLowerInvariant() switch
        {
            "guardian_trade_request_missing_guardian_resolution" => true,
            "guardian_trade_request_missing_inventory_resolution" => true,
            "guardian_trade_request_missing_receipt_resolution" => true,
            _ => false
        };
    }

    private static bool IsAfterlifeSpiritualConflictRewardRepairIssue(ValidationIssue issue)
    {
        var code = issue.Code ?? string.Empty;
        return code.StartsWith("afterlife_conflict_reward_", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsAfterlifeEntityProfileScaffoldRepairIssue(ValidationIssue issue)
    {
        var code = issue.Code ?? string.Empty;
        return string.Equals(code, "afterlife_relevant_actor_missing_canonical_memory_owner", StringComparison.OrdinalIgnoreCase) ||
               code.StartsWith("afterlife_entity_profile_", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(code, "afterlife_entity_profile_agency_goals_not_object", StringComparison.OrdinalIgnoreCase) ||
               code.StartsWith("afterlife_entity_profile_agency_goal_", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(code, "afterlife_entity_profile_missing_progression_strategy", StringComparison.OrdinalIgnoreCase) ||
               code.StartsWith("afterlife_entity_profile_strategy_", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(code, "afterlife_entity_profile_missing_ledger", StringComparison.OrdinalIgnoreCase) ||
               code.StartsWith("afterlife_entity_profile_ledger_", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(code, "special_art_learning_receipts_not_array", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(code, "special_art_learning_receipt_not_object", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(code, "incomplete_special_art_learning_receipt", StringComparison.OrdinalIgnoreCase) ||
               code.StartsWith("invalid_special_art_learning_", StringComparison.OrdinalIgnoreCase) ||
               code.StartsWith("special_art_learning_", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsAcceptedTurnOutputArtifactRepairIssue(ValidationIssue issue)
    {
        return (issue.Code ?? string.Empty).ToLowerInvariant() switch
        {
            "accepted_turn_missing_narrative_response" => true,
            "accepted_turn_empty_narrative_response" => true,
            "accepted_turn_stale_narrative_response" => true,
            "accepted_turn_stale_player_facing_output_after_canonical_repair" => true,
            "narrative_response_technical_repair_leak" => true,
            "accepted_turn_invalid_narrative_json_root" => true,
            "accepted_turn_invalid_narrative_json" => true,
            "narrative_response_unknown_field" => true,
            "narrative_response_missing_timestamp" => true,
            "narrative_response_invalid_timestamp" => true,
            "accepted_turn_empty_interface_updates" => true,
            "accepted_turn_stale_interface_updates" => true,
            "accepted_turn_invalid_interface_updates_root" => true,
            "accepted_turn_invalid_interface_updates_json" => true,
            "interface_updates_missing_timestamp" => true,
            "interface_updates_invalid_timestamp" => true,
            "interface_updates_missing_payload" => true,
            "interface_updates_unknown_field" => true,
            "missing_gm_thoughts" => true,
            "accepted_turn_stale_debug_logs" => true,
            "invalid_debug_logs_json_root" => true,
            "invalid_debug_logs_json" => true,
            "debug_logs_unknown_field" => true,
            "debug_logs_missing_timestamp" => true,
            "debug_logs_invalid_timestamp" => true,
            _ => false
        };
    }

    private static ValidationRepairHarnessPacket BuildAcceptedTurnOutputArtifactRepairPacket(
        IReadOnlyList<ValidationIssue> outputArtifactErrors)
    {
        var targetFiles = outputArtifactErrors
            .Select(ResolveAcceptedTurnOutputArtifactTargetPath)
            .Where(path => !string.IsNullOrWhiteSpace(path))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
            .ToList();

        var repairsNarrative = targetFiles.Contains("output/narrative_response.json", StringComparer.OrdinalIgnoreCase);
        var repairsInterface = targetFiles.Contains("output/interface_updates.json", StringComparer.OrdinalIgnoreCase);
        var repairsDebugLog = targetFiles.Contains("output/debug_logs.json", StringComparer.OrdinalIgnoreCase);

        var expectedShape = new List<string>();
        if (repairsNarrative)
        {
            expectedShape.Add(
                "output/narrative_response.json must be a fresh JSON object for the current accepted turn: { \"response\": \"player-facing narrative text\", \"timestamp\": \"ISO-8601 UTC timestamp\" }.");
        }
        if (repairsInterface)
        {
            expectedShape.Add(
                "output/interface_updates.json must be a fresh JSON object for the same accepted turn: { \"dialogueOptions\": [ { \"text\": \"visible option\", \"inputValue\": \"player input\" } ], \"timestamp\": \"ISO-8601 UTC timestamp\" }.");
        }
        if (repairsDebugLog)
        {
            expectedShape.Add(
                "output/debug_logs.json must be a fresh JSON object for the current accepted turn: { \"gm_thoughts_markdown\": \"## Охват NPC-анализа\\n...\", \"timestamp\": \"ISO-8601 UTC timestamp\" }.");
            expectedShape.Add(
                "gm_thoughts_markdown must contain a separate `## Охват NPC-анализа` / `## NPC Scope` section before full Actor Brain reasoning blocks for every relevant actor.");
            expectedShape.Add(
                "If no NPC, Guardian, faction, or other actor meaningfully acts or changes, explicitly preserve an actorless scope and explain why; do not invent an actor.");
        }

        var steps = new List<string>
        {
            "Open game_state/control/validation_repair_request.json first and repair only the listed accepted-turn output artifact errors and targetFiles."
        };
        if (repairsNarrative)
        {
            steps.Add(
                "Rewrite output/narrative_response.json with a fresh non-empty response for this same player action; preserve the already accepted narrative meaning instead of inventing a new turn.");
            steps.Add(
                "If validation_repair_request.json says player-facing output is stale after canonical state repair, base the rewritten narrative on the current canonical game_state files, not the pre-repair wording.");
            steps.Add(
                "If validation reports narrative_response_technical_repair_leak, rewrite response as an in-world scene only; keep validation/repair/JSON/storage details out of player-facing prose.");
        }
        if (repairsInterface)
        {
            steps.Add(
                "Rewrite output/interface_updates.json dialogueOptions/inputValue choices so they match the repaired canonical state and current player-facing narrative.");
        }
        if (repairsDebugLog)
        {
            steps.Add(
                "Rewrite output/debug_logs.json.gm_thoughts_markdown only because output/debug_logs.json is listed in targetFiles. Preserve every already valid full Actor Brain block and its exact `Изменения состояния` journal/ledger command or canonical surface; repair only the invalid or stale portions and refresh the file timestamp.");
            steps.Add(
                "For any relevant actor, keep the full Actor Brain fields from debugLogTemplate, including profile inputs, motivation, constraints, at least two strategies with benefit/risk, the chosen strategy, rejected alternatives, actions, and exact state changes.");
            steps.Add(
                "Preserve the exact actor-memory surface already used: Mortal NPC `NPCJournals[].journalEntries[]`; Guardian `guardianThoughtJournalUpdates` or `UpdateGuardians.addMusings`; resident `residentThoughtJournalUpdates`; existing afterlife entity actor-owned `ledger/progressionLedger`; existing Shining faction `shiningFactionChronicleUpdates`.");
            steps.Add(
                "If the accepted scope was genuinely actorless, preserve the actorless scope explanation and omit Actor Brain actor blocks; do not invent an actor for output repair.");
        }
        steps.Add("Do not touch canonical game_state files unless validation_repair_request.json lists a canonical state error as well.");
        steps.Add(
            "After all listed output artifacts are repaired, call Complete-BoeValidationRepair as the last action, or create game_state/control/validation_repair_ready.json with exact sessionId/requestId/turnNumber from validation_repair_request.json.");

        var doNotDo = new List<string>
        {
            "Do not write ready/turn_complete.json for validation repair.",
            "Do not create a new turn, reroll, advance time, or change player choice while repairing missing output artifacts.",
            "Do not mention JSON, validation, repair, canonical state, arrays, file paths, field names, or storage mechanics inside output/narrative_response.json.response.",
            "Do not read implementation code such as BookOfEternityClient/**/*.cs to infer repair rules; use this packet, validation_repair_request.json, GM docs, and session files."
        };
        if (repairsDebugLog)
        {
            doNotDo.Add(
                "Do not shorten a valid Actor Brain block or replace its exact journal/ledger surface with generic wording such as `state unchanged` or `the prior memory remains valid`.");
        }
        else
        {
            doNotDo.Add(
                "Do not rewrite output/debug_logs.json because it is not listed in targetFiles; preserve its valid Actor Brain and exact journal/ledger surface unchanged.");
        }

        return new ValidationRepairHarnessPacket
        {
            Kind = "accepted_turn_output_artifact_repair",
            Priority = "high",
            Title = "Accepted turn output artifact repair",
            TargetFiles = targetFiles,
            ExpectedShape = expectedShape,
            Steps = steps,
            DebugLogTemplate = repairsDebugLog
                ? BuildAcceptedTurnOutputArtifactDebugLogTemplate()
                : string.Empty,
            DoNotDo = doNotDo
        };
    }

    private static string ResolveAcceptedTurnOutputArtifactTargetPath(ValidationIssue issue)
    {
        var normalized = NormalizeRepairTargetPath(issue.FilePath);
        if (normalized.StartsWith("output/narrative_response.json", StringComparison.OrdinalIgnoreCase))
            return "output/narrative_response.json";
        if (normalized.StartsWith("output/interface_updates.json", StringComparison.OrdinalIgnoreCase))
            return "output/interface_updates.json";
        if (normalized.StartsWith("output/debug_logs.json", StringComparison.OrdinalIgnoreCase))
            return "output/debug_logs.json";

        return (issue.Code ?? string.Empty).ToLowerInvariant() switch
        {
            "accepted_turn_missing_narrative_response" or
            "accepted_turn_empty_narrative_response" or
            "accepted_turn_stale_narrative_response" or
            "narrative_response_technical_repair_leak" or
            "accepted_turn_invalid_narrative_json_root" or
            "accepted_turn_invalid_narrative_json" or
            "narrative_response_unknown_field" or
            "narrative_response_missing_timestamp" or
            "narrative_response_invalid_timestamp" => "output/narrative_response.json",

            "accepted_turn_empty_interface_updates" or
            "accepted_turn_stale_interface_updates" or
            "accepted_turn_invalid_interface_updates_root" or
            "accepted_turn_invalid_interface_updates_json" or
            "interface_updates_missing_timestamp" or
            "interface_updates_invalid_timestamp" or
            "interface_updates_missing_payload" or
            "interface_updates_unknown_field" => "output/interface_updates.json",

            "missing_gm_thoughts" or
            "accepted_turn_stale_debug_logs" or
            "invalid_debug_logs_json_root" or
            "invalid_debug_logs_json" or
            "debug_logs_unknown_field" or
            "debug_logs_missing_timestamp" or
            "debug_logs_invalid_timestamp" => "output/debug_logs.json",

            _ => string.Empty
        };
    }

    private static ValidationRepairHarnessPacket BuildNpcScopeDeclarationRepairPacket(
        IReadOnlyList<ValidationIssue> npcScopeErrors)
    {
        var actorNames = CollectRepairActorNames(npcScopeErrors)
            .OrderBy(actor => actor, StringComparer.OrdinalIgnoreCase)
            .ToList();
        var missingFields = npcScopeErrors
            .Select(issue => issue.Code ?? "npc_scope_validation_error")
            .Where(code => !string.IsNullOrWhiteSpace(code))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(code => code, StringComparer.OrdinalIgnoreCase)
            .ToList();

        var targetFiles = npcScopeErrors
            .Select(issue => NormalizeRepairTargetPath(issue.FilePath))
            .Where(path => !string.IsNullOrWhiteSpace(path))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (!targetFiles.Contains("output/debug_logs.json", StringComparer.OrdinalIgnoreCase))
            targetFiles.Add("output/debug_logs.json");

        return new ValidationRepairHarnessPacket
        {
            Kind = "npc_scope_declaration_repair",
            Priority = "high",
            Title = "NPC Scope declaration repair",
            TargetFiles = targetFiles,
            MissingFields = missingFields,
            ExpectedShape = new List<string>
            {
                "Repair only output/debug_logs.json.gm_thoughts_markdown unless validation_repair_request.json lists additional state errors.",
                "The NPC Scope block must declare Mode, Relevant actors, Why relevant, Actors outside scope, and Why outside scope.",
                "World-progression, Guardian-centric, and Mixed modes require at least one actor in Relevant actors.",
                "Scene-local may use Relevant actors: нет / none only when no NPC, Guardian, faction, resident, opponent, or other actor-specific structured state changed.",
                "If Relevant actors is not empty, add a reasoning section with an exact `### <actor name>` block and a full Actor Brain decision audit for every listed actor."
            },
            SafeCorrectionRules = new List<string>
            {
                "Use actor names that are visible in the current turn, current state, pending snapshot, or listed validation errors.",
                "For afterlife spiritual conflict turns, include the player soul, active Guardian or teacher, and current opponent/trial actor when they act, anchor, or receive structured state.",
                "Use Scene-local with empty actors only for purely player/internal output with no structured actor mutations.",
                "Keep the accepted player action and already written canonical state; repair the reasoning declaration instead of inventing a new event."
            },
            Steps = new List<string>
            {
                "Open game_state/control/validation_repair_request.json and output/debug_logs.json.",
                "Replace or patch only gm_thoughts_markdown so it starts with a complete `## NPC Scope` block.",
                "Choose the narrowest correct mode: Scene-local, World-progression, Guardian-centric, or Mixed.",
                "Fill Relevant actors as comma-separated canonical names, or `нет` only if Scene-local and no actor-specific state changed.",
                "Add one full Actor Brain block per relevant actor using every field in debugLogTemplate, including two distinct strategies with explicit benefit/risk and exact state changes.",
                "After the markdown is repaired, call Complete-BoeValidationRepair as the last action, or create validation_repair_ready.json with exact metadata from validation_repair_request.json."
            },
            CanonicalActorNames = actorNames,
            DebugLogTemplate = BuildNpcScopeDeclarationDebugLogTemplate(actorNames),
            DoNotDo = new List<string>
            {
                "Do not create a new turn, reroll dice, advance time, or change the player choice while repairing NPC Scope.",
                "Do not delete meaningful actor state just to make Scene-local with empty actors pass.",
                "Do not list an actor in Relevant actors without a matching `### <actor name>` reasoning block.",
                "Do not read implementation code such as BookOfEternityClient/**/*.cs to infer repair rules; use this packet, validation_repair_request.json, templates, and session files."
            }
        };
    }
}
