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

    private static ValidationRepairHarnessPacket BuildActorMemoryPersistenceRepairPacket(
        IReadOnlyList<ValidationIssue> actorMemoryErrors,
        IReadOnlyCollection<string>? guardianActorNameHints = null)
    {
        var actorNames = CollectRepairActorNames(actorMemoryErrors, guardianActorNameHints)
            .OrderBy(actor => actor, StringComparer.OrdinalIgnoreCase)
            .ToList();
        var targetFiles = actorMemoryErrors
            .Select(issue => NormalizeRepairTargetPath(issue.FilePath))
            .Where(path => !string.IsNullOrWhiteSpace(path))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
            .ToList();

        var hasGuardianMemoryError = actorMemoryErrors.Any(issue =>
            string.Equals(issue.Code, "guardian_relevant_actor_missing_thought_journal_delta", StringComparison.OrdinalIgnoreCase) ||
            IsGuardianThoughtJournalShapeRepairIssue(issue));
        if (hasGuardianMemoryError)
        {
            targetFiles.RemoveAll(path =>
                string.Equals(path, "game_state/meta/guardians.json", StringComparison.OrdinalIgnoreCase) ||
                path.StartsWith(GuardianThoughtJournalState.StatePath, StringComparison.OrdinalIgnoreCase));
        }
        if (hasGuardianMemoryError &&
            !targetFiles.Contains(GuardianThoughtJournalState.StatePath, StringComparer.OrdinalIgnoreCase))
        {
            targetFiles.Add(GuardianThoughtJournalState.StatePath);
        }

        if (actorMemoryErrors.Any(issue =>
                string.Equals(issue.Code, "mortal_npc_relevant_actor_missing_thought_journal_delta", StringComparison.OrdinalIgnoreCase)) &&
            !targetFiles.Contains("game_state/npcs/npc_journals.json", StringComparer.OrdinalIgnoreCase))
        {
            targetFiles.Add("game_state/npcs/npc_journals.json");
        }

        if (actorMemoryErrors.Any(issue =>
                string.Equals(issue.Code, "afterlife_resident_relevant_actor_missing_thought_journal_delta", StringComparison.OrdinalIgnoreCase)) &&
            !targetFiles.Contains(GuardianAbodeResidentState.StatePath, StringComparer.OrdinalIgnoreCase))
        {
            targetFiles.Add(GuardianAbodeResidentState.StatePath);
        }

        if (!targetFiles.Contains("output/debug_logs.json", StringComparer.OrdinalIgnoreCase))
            targetFiles.Add("output/debug_logs.json");

        return new ValidationRepairHarnessPacket
        {
            Kind = "actor_memory_persistence_repair",
            Priority = "high",
            Title = "Relevant actor thought-journal persistence repair",
            TargetFiles = targetFiles,
            TemplateRefs = new List<string>
            {
                "OtherGuides/Actor_Brain_2_0.md",
                "Templates/MORTAL_NPC_UPDATE_TEMPLATE.md",
                "OtherGuides/Afterlife_Contract_Matrix.md",
                "Examples/E_CLI_Afterlife_Turns.txt"
            },
            CanonicalActorNames = actorNames,
            ExpectedShape = new List<string>
            {
                "A relevant actor that evaluated the player action and chose a strategy must gain a new canonical thought-journal entry in the same accepted turn.",
                $"Guardian memory repair writes canonical on-disk shape directly to {GuardianThoughtJournalState.StatePath}: {{ \"entries\": [ {{ \"entryId\": \"fresh-stable-id\", \"guardianId\": \"existing-guardian-id\", \"title\": \"short title\", \"summary\": \"Я ...\", \"turn\": <current turn>, \"timestamp\": \"ISO-8601 UTC\" }} ] }}. Preserve every old entry.",
                "guardianThoughtJournalUpdates is a first-pass response command surface, not the sole top-level canonical repair shape for a newly created journal file.",
                "Mortal NPC inner thoughts are appended to game_state/npcs/npc_journals.json NPCJournals[].journalEntries[] for the matching canonical NPC id/name.",
                $"Guardian Abode resident inner thoughts are appended through {GuardianAbodeResidentState.UpdateThoughtJournalProperty} and normalize into {GuardianAbodeResidentState.ThoughtJournalProperty}.",
                $"Other afterlife entity memory initializes gmThoughtsSummary when a profile is first materialized; every later decision appends ledger/progressionLedger evidence in {AfterlifeEntityProfileState.StatePath}, even when the current gmThoughtsSummary also changes.",
                $"A newly created Shining faction may initialize {ShiningAbodeState.FactionStrategicMemoryProperty}; every later decision appends {ShiningAbodeState.FactionChronicleProperty} through {ShiningAbodeState.FactionChronicleUpdatesProperty} in {ShiningAbodeState.StatePath}."
            },
            SafeCorrectionRules = new List<string>
            {
                "Add one concise first-person thought describing the actor's reaction, conclusion, fear, intent, or changed expectation; keep the Actor Brain strategy audit in debug logs and the actor's subjective memory in canonical state.",
                "Append a new entry with a fresh stable entryId/turn/timestamp where that journal contract supports them; do not mutate an old entry to imitate a new thought.",
                "Use the actor id and display name already present in canonical state; do not create a duplicate actor or rename the actor during memory repair.",
                "Patch only the listed actor journal surface and the matching Actor Brain state-change line in output/debug_logs.json; preserve the already accepted narrative, decision, relationships, inventory, currencies, and unrelated state."
            },
            Steps = new List<string>
            {
                "Open game_state/control/validation_repair_request.json and use errors[].actor plus errors[].code to choose the exact journal surface.",
                $"For Guardian thought-memory errors, append one valid first-person entry directly to the packet-listed {GuardianThoughtJournalState.StatePath} entries[] array. If the file is absent or malformed, create/repair the canonical root as {{ \"entries\": [...] }}; do not edit guardians.json unless a separate current repair error explicitly lists it.",
                "For mortal_npc_relevant_actor_missing_thought_journal_delta, append a new journalEntries[] object to the matching NPCJournals record; keep the thought in first person.",
                $"For afterlife_resident_relevant_actor_missing_thought_journal_delta, append a complete {GuardianAbodeResidentState.UpdateThoughtJournalProperty} entry for the matching residentId.",
                "For afterlife_entity_relevant_actor_missing_memory_ledger_delta, append actor-owned ledger/progressionLedger evidence to an existing profile; gmThoughtsSummary-only memory is valid only while first materializing a new profile.",
                $"For shining_faction_relevant_actor_missing_strategic_memory_delta, append one current-turn faction memory entry through {ShiningAbodeState.FactionChronicleUpdatesProperty}; preserve every previous chronicle entry. Do not treat a mutable strategicMemory rewrite as historical persistence for an existing faction.",
                "In output/debug_logs.json, update only the listed actor's - Изменения состояния: line so it names the exact journal command and canonical journal surface that this repair actually used.",
                "Do not rewrite output/narrative_response.json or re-resolve the actor decision; this repair persists the missing internal memory of the already authored response.",
                "After repairs are complete, call Complete-BoeValidationRepair as the last action, or create validation_repair_ready.json with exact metadata from the current validation_repair_request.json."
            },
            DoNotDo = new List<string>
            {
                "Do not replace the actor's first-person thought journal with an external chronicle, interaction log, or third-person scene summary.",
                "Do not persist schemaVersion or a guardianThoughtJournalUpdates-only root in game_state/meta/guardian_thought_journal.json; the repaired canonical file must contain entries[].",
                "Do not edit or delete previous journal entries; append a new entry for the current turn.",
                "Do not modify unrelated actors, prose, quests, factions, inventory, combat, currencies, or world state.",
                "Do not read implementation source to infer the repair; use this packet, its templateRefs, canonical state, and the validated repair request."
            }
        };
    }

    private static ValidationRepairHarnessPacket BuildFactionMaterializationRepairPacket(
        string coordinate,
        IReadOnlyList<ValidationIssue> factionIssues)
    {
        var isMortal = coordinate.StartsWith("mortal_faction:", StringComparison.Ordinal);
        var targetFiles = ResolveFactionMaterializationRepairTargetFiles(
            coordinate,
            factionIssues);
        var missingFields = factionIssues
            .Where(issue => HasExactMissingCodeComponent(issue.Code))
            .Select(issue => issue.FilePath)
            .Where(path => !string.IsNullOrEmpty(path))
            .Distinct(StringComparer.Ordinal)
            .OrderBy(path => path, StringComparer.Ordinal)
            .ToList();
        var expectedShape = new List<string>
        {
            "The exact faction coordinate has one immutable Faction Materialization v1 receipt; an accepted materializationId remains unchanged.",
            "Every governed materialization section is physically present in its exact state=populated or exact empty_by_design shape.",
            "Every faction-bound actor, location, and sidecar link resolves to the exact packet coordinate without changing another faction."
        };
        expectedShape.AddRange(
            factionIssues
                .Where(issue => issue.FactionRepairClassification.HasValue)
                .Select(issue => GetFactionMaterializationRepairClassification(
                    issue.FactionRepairClassification!.Value))
                .Distinct(StringComparer.Ordinal)
                .OrderBy(classification => classification, StringComparer.Ordinal));

        return new ValidationRepairHarnessPacket
        {
            Kind = "faction_materialization_repair",
            Priority = "critical",
            Title = $"Bounded Faction Materialization repair for {coordinate}",
            TargetFiles = targetFiles,
            TemplateRefs = isMortal
                ? new List<string> { "Templates/MORTAL_FACTION_UPDATE_TEMPLATE.md" }
                : new List<string>
                {
                    "OtherGuides/Afterlife_Contract_Matrix.md",
                    "Examples/E_CLI_Afterlife_Turns.txt"
                },
            CanonicalActorNames = new List<string> { coordinate },
            MissingFields = missingFields.Count == 0 ? null : missingFields,
            ExactFieldCorrections = factionIssues
                .Select(BuildExactFieldCorrection)
                .OrderBy(correction => correction.Path, StringComparer.Ordinal)
                .ThenBy(correction => correction.Code, StringComparer.Ordinal)
                .ToList(),
            ExpectedShape = expectedShape,
            SafeCorrectionRules = new List<string>
            {
                $"Change only the listed coordinate {coordinate} and the exact listed targetFiles; no other domain root is writable.",
                "Preserve all valid sections, accepted receipts, validated histories, chronicles, and every unrelated faction.",
                "Restore a missing bounded surface in place and keep every valid populated or exact empty governed section unchanged.",
                "Apply only corrections supported by the current validation issue and canonical evidence in the listed targets."
            },
            Steps = new List<string>
            {
                "Open game_state/control/validation_repair_request.json and only this packet's listed targetFiles and templateRefs.",
                $"Locate only the exact coordinate {coordinate}; do not resolve a faction from a display name, prose, file path, or another issue.",
                "Apply exactFieldCorrections one by one, changing only the named coordinate, path, and listed target root.",
                "Rerun raw Faction Materialization validation before normalization and do not accept a partially repaired bundle.",
                "After every exact repair passes, call Complete-BoeValidationRepair as the final action, or create validation_repair_ready.json with exact metadata from the current validation_repair_request.json."
            },
            DoNotDo = new List<string>
            {
                "Do not change another faction or broaden this repair to an unlisted target root.",
                "Do not change an accepted materializationId or replace an accepted historical receipt.",
                "Do not rewrite validated history, chronicles, or valid governed sections.",
                "Do not use whole-file replacement to repair one exact faction surface.",
                "Do not invent faction identity, links, sections, or content from names, tags, descriptions, or genre.",
                "Do not create a new turn or write ready/turn_complete.json during validation repair."
            }
        };
    }

    private static List<string> ResolveFactionMaterializationRepairTargetFiles(
        string coordinate,
        IReadOnlyList<ValidationIssue> factionIssues)
    {
        var targetFiles = new SortedSet<string>(StringComparer.Ordinal);
        string[] allowedTargets;
        if (coordinate.StartsWith("mortal_faction:", StringComparison.Ordinal))
        {
            targetFiles.Add("game_state/factions/faction_core.json");
            allowedTargets =
            [
                "game_state/factions/faction_core.json",
                "game_state/factions/faction_structure.json",
                "game_state/factions/faction_resources.json",
                "game_state/factions/faction_projects.json",
                "game_state/factions/faction_custom.json",
                "game_state/factions/faction_chronicles.json",
                "game_state/npcs/npc_core.json",
                "game_state/npcs/npc_journals.json",
                "game_state/npcs/npc_interaction_journal.json",
                "game_state/npcs/npc_masks.json",
                "game_state/npcs/npc_memory.json",
                "game_state/world/current_location.json",
                "game_state/world/world_map.json"
            ];
        }
        else
        {
            targetFiles.Add("game_state/meta/shining_abode_state.json");
            allowedTargets =
            [
                "game_state/meta/shining_abode_state.json",
                "game_state/meta/guardian_abode_residents.json",
                "game_state/meta/afterlife_entity_profiles.json",
                "game_state/meta/main_story_saref_state.json"
            ];
        }

        foreach (var issue in factionIssues)
        {
            foreach (var repairTarget in issue.RepairTargetFiles)
            {
                if (allowedTargets.Contains(
                        repairTarget,
                        StringComparer.Ordinal))
                {
                    targetFiles.Add(repairTarget);
                }
            }

            foreach (var allowedTarget in allowedTargets)
            {
                if (IssuePathNamesExactRepairRoot(issue.FilePath, allowedTarget))
                    targetFiles.Add(allowedTarget);
            }
        }

        return targetFiles.ToList();
    }

    private static bool IssuePathNamesExactRepairRoot(
        string? issuePath,
        string targetRoot)
    {
        if (issuePath == null ||
            !issuePath.StartsWith(targetRoot, StringComparison.Ordinal))
        {
            return false;
        }

        return issuePath.Length == targetRoot.Length ||
               issuePath[targetRoot.Length] is '.' or '[' or ':';
    }

    private static bool HasExactMissingCodeComponent(string? issueCode) =>
        (issueCode ?? string.Empty)
        .Split('_')
        .Contains("missing", StringComparer.Ordinal);

    private static string GetFactionMaterializationRepairClassification(
        FactionTouchKind classification) =>
        classification switch
        {
            FactionTouchKind.New => "new",
            FactionTouchKind.AlreadyMaterialized =>
                "already_materialized",
            FactionTouchKind.InvalidReceiptless =>
                "invalid_receiptless",
            _ => throw new ArgumentOutOfRangeException(
                nameof(classification),
                classification,
                "Unsupported faction repair classification.")
        };

    private static ValidationRepairHarnessPacket BuildFactionIdentityRepairPacket(
        IReadOnlyList<ValidationIssue> factionIdentityErrors)
    {
        var targetFiles = factionIdentityErrors
            .Select(issue => NormalizeRepairTargetPath(issue.FilePath))
            .Where(path => !string.IsNullOrWhiteSpace(path))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (!targetFiles.Contains("game_state/factions/faction_core.json", StringComparer.OrdinalIgnoreCase))
            targetFiles.Add("game_state/factions/faction_core.json");
        if (!targetFiles.Contains("game_state/control/pending_turn_snapshot/game_state/factions/faction_core.json", StringComparer.OrdinalIgnoreCase))
            targetFiles.Add("game_state/control/pending_turn_snapshot/game_state/factions/faction_core.json");

        return new ValidationRepairHarnessPacket
        {
            Kind = "faction_identity_repair",
            Priority = "high",
            Title = "Mortal faction identity repair",
            TargetFiles = targetFiles,
            TemplateRefs = new List<string> { "Templates/MORTAL_FACTION_UPDATE_TEMPLATE.md" },
            ExpectedShape = new List<string>
            {
                "game_state/factions/faction_core.json contains canonical factions[] with stable factionId, name/displayName, description, status, reputation/influence, ranks/rankBranches, relations, projects, chronicle, resources, and custom states preserved.",
                "Every faction sidecar entry references an existing canonical factionId from factions[] unless the sidecar entry is removed as invalid speculative data.",
                "Same-turn temporary faction identity remains factionId = null, initialId = <stable temporary id>, isNewFaction = true until the canonical promotion path creates a permanent faction."
            },
            SafeCorrectionRules = new List<string>
            {
                "If the bad id was meant to update an existing faction, replace it with the exact existing canonical factionId from game_state/factions/faction_core.json.",
                "If the story truly introduced a durable missing faction, create the missing faction as a complete factions[] object before any sidecar references it.",
                "If the sidecar is speculative, duplicate, or belongs to a faction that should not exist, remove the invalid sidecar entry or retarget it to an existing canonical factionId.",
                "Preserve unrelated faction ranks, rankBranches, chronicles, relations, projects, resources, reputation, and custom states while repairing identity."
            },
            Steps = new List<string>
            {
                "Open Templates/MORTAL_FACTION_UPDATE_TEMPLATE.md before editing game_state/factions/*.",
                "Use game_state/control/pending_turn_snapshot/game_state/factions/faction_core.json as the authority for whether the faction was already permanent before this turn or was still a same-turn temporary faction.",
                "If the matching faction in pending_turn_snapshot has factionId = null plus a non-empty initialId, restore the current object to the same temporary identity shape: factionId = null, the exact initialId, and isNewFaction = true.",
                "If the matching faction in pending_turn_snapshot has a non-empty factionId, use that exact permanent factionId and remove initialId/isNewFaction from the existing faction object.",
                "For unknown faction ids, choose exactly one correction path: reference an existing canonical factionId, create the missing faction as a complete factions[] object, or remove/retarget the invalid sidecar entry.",
                "After restoring the identity shape, preserve the intended turn content updates around the faction instead of replacing the whole file with a minimal skeleton.",
                "After file repairs are complete, call Complete-BoeValidationRepair as the last action, or create game_state/control/validation_repair_ready.json with exact sessionId/requestId/turnNumber from the current validation_repair_request.json."
            },
            DoNotDo = new List<string>
            {
                "Do not invent a permanent factionId from the faction name.",
                "Do not convert a same-turn temporary faction into an existing permanent faction just because validation mentions existing faction wording.",
                "Do not leave faction sidecar entries pointing at missing faction ids.",
                "Do not delete unrelated faction ranks, resources, projects, chronicles, or reputation details to silence identity validation.",
                "Do not read implementation code such as BookOfEternityClient/**/*.cs to infer repair rules; use this repair packet, the validation request, and session snapshot/control files."
            }
        };
    }

    private static ValidationRepairHarnessPacket BuildMortalFactionResourceRepairPacket(
        IReadOnlyList<ValidationIssue> factionResourceErrors)
    {
        var targetFiles = factionResourceErrors
            .Select(issue => NormalizeRepairTargetPath(issue.FilePath))
            .Where(path => !string.IsNullOrWhiteSpace(path))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (!targetFiles.Contains("game_state/factions/faction_resources.json", StringComparer.OrdinalIgnoreCase))
            targetFiles.Add("game_state/factions/faction_resources.json");
        if (!targetFiles.Contains("game_state/factions/faction_core.json", StringComparer.OrdinalIgnoreCase))
            targetFiles.Add("game_state/factions/faction_core.json");

        var missingFields = CollectRepairMissingFields(factionResourceErrors);
        var expectedShape = new List<string>
        {
            "canonical_faction_resource_entry_missing_required_fields means a canonical faction resource entry was written as a partial delta; repair it into a full resource object.",
            "metaResources entries require resourceName, currentStockpile, incomePerCycle, and upkeepPerCycle.",
            "strategicGoods entries require resourceName, currentStockpile, and incomePerCycle.",
            "Keep faction resources linked to the existing canonical faction and preserve unrelated ranks, branches, projects, chronicles, relations, and custom states."
        };

        if (missingFields.Count > 0)
            expectedShape.Add($"Validator reported these exact missing fields: {string.Join(", ", missingFields)}.");

        var steps = new List<string>
        {
            "Open Templates/MORTAL_FACTION_UPDATE_TEMPLATE.md before editing game_state/factions/*.",
            "Find the resource entry named by validation_repair_request.json.errors[].filePath and expand it to a full resource object instead of deleting the faction or replacing the file with a minimal skeleton.",
            "If the entry belongs to metaResources, include resourceName, currentStockpile, incomePerCycle, and upkeepPerCycle; if it belongs to strategicGoods, include resourceName, currentStockpile, and incomePerCycle.",
            "Use numeric values for currentStockpile/incomePerCycle/upkeepPerCycle and keep player-facing resource names meaningful in the current Mortal World scene.",
            "After file repairs are complete, call Complete-BoeValidationRepair as the last action, or create validation_repair_ready.json with exact metadata from the current validation_repair_request.json."
        };

        if (missingFields.Count > 0)
            steps.Insert(2, $"Patch the exact missing fields first: {string.Join(", ", missingFields)}.");

        return new ValidationRepairHarnessPacket
        {
            Kind = "mortal_faction_resource_repair",
            Priority = "high",
            Title = "Mortal faction resource entry repair",
            TargetFiles = targetFiles,
            TemplateRefs = new List<string> { "Templates/MORTAL_FACTION_UPDATE_TEMPLATE.md" },
            MissingFields = missingFields.Count == 0 ? null : missingFields,
            ExpectedShape = expectedShape,
            SafeCorrectionRules = new List<string>
            {
                "Complete the resource entry in place whenever it represents a real faction resource introduced or updated by the turn.",
                "Remove the entry only if it is speculative, duplicate, or not supported by the accepted scene.",
                "Preserve all unrelated faction state while repairing resource shape."
            },
            Steps = steps,
            DoNotDo = new List<string>
            {
                "Do not delete the whole faction to silence a resource-entry validation error.",
                "Do not leave resource entries as identity-only stubs or partial deltas.",
                "Do not read implementation code such as BookOfEternityClient/**/*.cs to infer resource rules; use this packet, the validation request, GM docs, and session files."
            }
        };
    }
}
