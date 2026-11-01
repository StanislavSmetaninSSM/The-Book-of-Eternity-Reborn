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

    private static ValidationRepairHarnessPacket BuildAfterlifeSpiritualConflictRewardRepairPacket(
        IReadOnlyList<ValidationIssue> rewardErrors)
    {
        var targetFiles = rewardErrors
            .Select(issue => NormalizeRepairTargetPath(issue.FilePath))
            .Where(path => !string.IsNullOrWhiteSpace(path))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (!targetFiles.Contains("game_state/meta/afterlife_spiritual_conflict_state.json", StringComparer.OrdinalIgnoreCase))
            targetFiles.Add("game_state/meta/afterlife_spiritual_conflict_state.json");

        var issueDetails = rewardErrors
            .Select(DescribeAfterlifeRewardRepairIssue)
            .Where(detail => !string.IsNullOrWhiteSpace(detail))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(detail => detail, StringComparer.OrdinalIgnoreCase)
            .ToList();
        var issueSummary = issueDetails.Count == 0
            ? "see validation_repair_request.json.errors for exact rewardAudit paths and expected/actual values"
            : string.Join("; ", issueDetails);

        return new ValidationRepairHarnessPacket
        {
            Kind = "afterlife_spiritual_conflict_reward_repair",
            Priority = "high",
            Title = "Afterlife spiritual conflict reward eligibility repair",
            TargetFiles = targetFiles,
            TemplateRefs = new List<string>
            {
                "OtherGuides/Afterlife_Contract_Matrix.md",
                "Examples/E_CLI_Afterlife_Turns.txt"
            },
            ExpectedShape = new List<string>
            {
                "Currency rewardAudit is allowed only for a resolved contested player victory with diceAudit.outcomeBand = player_success or decisive_player_success.",
                "Negotiated, withdrawn, failed, training-only, stalemate, teaching, or non-contested spiritual conflict outcomes must not grant currency rewards.",
                "If the conflict did not qualify for a reward, preserve the narrative/training outcome and remove rewardAudit/currency deltas instead of upgrading the outcome.",
                "If a reward is legitimately allowed, rewardAudit.realm, currency, conflictId, anti-farm uniqueness, and authority realm must match the resolved conflict proof."
            },
            SafeCorrectionRules = new List<string>
            {
                "Use validation_repair_request.json.errors as the immediate checklist; expected/actual values are authoritative for reward fields.",
                "For afterlife_conflict_reward_not_allowed, remove the currency reward path and any matching currency delta; keep non-currency learning, chronicle, relationship, or narrative consequences if they are otherwise valid.",
                "For reward realm/currency/conflictId errors on a valid victory, patch only rewardAudit identity/currency fields and preserve the resolved exchange outcome.",
                "Do not convert negotiated training into a victory just to keep feathers or light-spark rewards."
            },
            Steps = new List<string>
            {
                "Open game_state/control/validation_repair_request.json first and repair only the listed afterlife conflict reward errors in place.",
                $"Patch these rewardAudit fields exactly: {issueSummary}.",
                "In game_state/meta/afterlife_spiritual_conflict_state.json, inspect the listed recentConflicts[] or activeConflict terminal proof and decide whether diceAudit.outcomeBand is player_success/decisive_player_success.",
                "If the outcome is negotiated/training/withdrawn/non-victory, remove rewardAudit and matching currency reward deltas; keep the conflict resolution, learning evidence, chronicle, and prose unchanged.",
                "If the outcome is a valid reward-bearing victory, patch rewardAudit.realm/currency/conflictId to match the conflict proof and realm authority without changing dice or outcome.",
                "After file repairs are complete, call Complete-BoeValidationRepair as the last action, or create game_state/control/validation_repair_ready.json with exact sessionId/requestId/turnNumber from the current validation_repair_request.json."
            },
            DoNotDo = new List<string>
            {
                "Do not create a new turn or write ready/turn_complete.json during validation repair.",
                "Do not upgrade a negotiated, training-only, withdrawn, or failed conflict into a victory to preserve a reward.",
                "Do not reroll dice, change exchangeLog outcomes, or invent a new conflictId just to satisfy reward validation.",
                "Do not delete valid non-reward consequences such as special-art learning evidence, relationship changes, or afterlife chronicles unless validation explicitly rejects them.",
                "Do not read implementation code such as BookOfEternityClient/**/*.cs to infer reward rules; use this packet, validation_repair_request.json, afterlife docs/examples, and session state."
            }
        };
    }

    private static ValidationRepairHarnessPacket BuildAfterlifeEntityProfileScaffoldRepairPacket(
        IReadOnlyList<ValidationIssue> profileErrors)
    {
        var targetFiles = profileErrors
            .Select(issue => NormalizeRepairTargetPath(issue.FilePath))
            .Where(path => !string.IsNullOrWhiteSpace(path))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (!targetFiles.Contains("game_state/meta/afterlife_entity_profiles.json", StringComparer.OrdinalIgnoreCase))
            targetFiles.Add("game_state/meta/afterlife_entity_profiles.json");

        var actorNames = CollectRepairActorNames(profileErrors)
            .OrderBy(actor => actor, StringComparer.OrdinalIgnoreCase)
            .ToList();
        if (actorNames.Count == 0)
            actorNames.Add("afterlife entity profile from validation_repair_request.json");

        var issueDetails = profileErrors
            .Select(DescribeAfterlifeEntityProfileScaffoldRepairIssue)
            .Where(detail => !string.IsNullOrWhiteSpace(detail))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(detail => detail, StringComparer.OrdinalIgnoreCase)
            .ToList();
        var issueSummary = issueDetails.Count == 0
            ? "see validation_repair_request.json.errors for exact profile fields and expected/actual values"
            : string.Join("; ", issueDetails);
        var hasMissingMemoryOwner = profileErrors.Any(issue => string.Equals(
            issue.Code,
            "afterlife_relevant_actor_missing_canonical_memory_owner",
            StringComparison.OrdinalIgnoreCase));

        return new ValidationRepairHarnessPacket
        {
            Kind = "afterlife_entity_profile_scaffold_repair",
            Priority = "high",
            Title = "Afterlife entity profile scaffold and special-art learning repair",
            TargetFiles = targetFiles,
            TemplateRefs = new List<string>
            {
                "OtherGuides/Afterlife_Contract_Matrix.md",
                "Examples/E_CLI_Afterlife_Turns.txt"
            },
            CanonicalActorNames = actorNames,
            ExpectedShape = new List<string>
            {
                "Every significant afterlife entity profile must keep goals as an object with goalId, shortTermGoal, longTermGoal, plan, gmThoughtsSummary, and updatedAtTurn.",
                "progressionStrategy is a required object with strategyId, summary, priorityOrder[], resourceReserve, allowedSpends[], forbiddenSpends[], and optional lastUpdatedAtTurn.",
                "ledger is a required array; use [] when no visible profile events exist yet. progressionLedger is optional but must be an array of complete entries if present.",
                "profileCommands.specialArtLearningReceipts[] entries require receiptId, artId, teacherActorType, teacherActorId, playerActorId, trainingConditionSatisfied=true, learnedAtTurn, roleplayEvidence, summary, and initialTier absent or 0.",
                "A special-art learning receipt must not grant a higher tier; make sure the source teacher art canTeachPlayer=true.",
                "Every afterlife name kept in relevant actors as an independent decision-maker resolves to a canonical Guardian, resident, afterlife entity profile, or Shining faction with its own memory surface."
            },
            SafeCorrectionRules = new List<string>
            {
                "Use validation_repair_request.json.errors as the immediate checklist; patch only the listed profile/receipt fields unless adjacent minimal scaffold is required to validate.",
                "Repair missing profile scaffold in place; do not delete the profile, teacher, learned art, or relationship evidence to silence shape errors.",
                "For a missing memory owner, either materialize the genuine independent actor with a stable id and complete afterlife profile, or remove a non-actor/invented label from relevant actors and its standalone Actor Brain block.",
                "Use player-facing Russian prose in summaries/goals while keeping canonical JSON keys and enum-like values valid.",
                "If special-art learning happened, preserve the learning proof and complete the receipt shape instead of converting it into unrelated narrative only."
            },
            Steps = new List<string>
            {
                "Open game_state/control/validation_repair_request.json and game_state/meta/afterlife_entity_profiles.json first.",
                hasMissingMemoryOwner
                    ? "For afterlife_relevant_actor_missing_canonical_memory_owner, decide from existing canonical state whether the named independent actor is genuine: materialize it as a complete supported afterlife profile, or remove it from output/debug_logs.json relevant actors and the standalone Actor Brain block. Do not invent a profile merely to silence validation."
                    : "Keep every already resolved canonical actor owner unchanged while repairing the listed profile fields.",
                $"Patch this minimum profile scaffold and receipt checklist: {issueSummary}.",
                "For every listed profile, make goals an object with goalId, shortTermGoal, longTermGoal, plan, gmThoughtsSummary, and updatedAtTurn; do not use an array for goals.",
                "Add or repair progressionStrategy with strategyId, summary, priorityOrder[], resourceReserve, allowedSpends[], forbiddenSpends[], and lastUpdatedAtTurn when known.",
                "Add ledger: [] when missing, or repair each ledger[] entry to an object with entryId, summary, and optional valid turnNumber.",
                "For profileCommands.specialArtLearningReceipts[], complete receiptId/artId/teacherActorType/teacherActorId/playerActorId/trainingConditionSatisfied/learnedAtTurn/roleplayEvidence/summary and keep initialTier absent or 0.",
                "After file repairs are complete, call Complete-BoeValidationRepair as the last action, or create game_state/control/validation_repair_ready.json with exact sessionId/requestId/turnNumber from the current validation_repair_request.json."
            },
            DoNotDo = new List<string>
            {
                "Do not move afterlife profile state into Mortal World NPC/player files.",
                "Do not delete learned special art data, teacher proof, goals, or relationship hooks just to avoid completing the scaffold.",
                "Do not grant initialTier > 0 through specialArtLearningReceipts; upgrades must happen through the documented progression/upgrade path.",
                "Do not collapse arrays such as priorityOrder, allowedSpends, forbiddenSpends, ledger, or progressionLedger into scalar strings.",
                "Do not read implementation code such as BookOfEternityClient/**/*.cs to infer profile scaffold rules; use this packet, validation_repair_request.json, afterlife docs/examples, and session state."
            }
        };
    }

    private static ValidationRepairHarnessPacket BuildAfterlifeChronicleStringArrayRepairPacket(
        IReadOnlyList<ValidationIssue> chronicleErrors)
    {
        var targetFiles = chronicleErrors
            .Select(issue => NormalizeRepairTargetPath(issue.FilePath))
            .Where(path => !string.IsNullOrWhiteSpace(path))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (!targetFiles.Contains("game_state/meta/afterlife_chronicles.json", StringComparer.OrdinalIgnoreCase))
            targetFiles.Add("game_state/meta/afterlife_chronicles.json");

        var issueDetails = chronicleErrors
            .Select(DescribeAfterlifeChronicleStringArrayRepairIssue)
            .Where(detail => !string.IsNullOrWhiteSpace(detail))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(detail => detail, StringComparer.OrdinalIgnoreCase)
            .ToList();
        var issueSummary = issueDetails.Count == 0
            ? "see validation_repair_request.json.errors for exact paths and expected/actual values"
            : string.Join("; ", issueDetails);

        return new ValidationRepairHarnessPacket
        {
            Kind = "afterlife_chronicle_string_array_repair",
            Priority = "high",
            Title = "Afterlife chronicle string-array shape repair",
            TargetFiles = targetFiles,
            TemplateRefs = new List<string>
            {
                "OtherGuides/Afterlife_Contract_Matrix.md",
                "Examples/E_CLI_Afterlife_Turns.txt"
            },
            ExpectedShape = new List<string>
            {
                "GM-authored changes use afterlifeChronicleUpdates[]; canonical game_state/meta/afterlife_chronicles.json stores chronicles[].",
                "persistentConsequences[] must be an array of non-empty strings. Each string is one durable consequence; do not use objects, bullets with nested fields, nulls, or empty strings.",
                "openThreads[] must be an array of non-empty strings. Each string is one unresolved hook; do not use objects, bullets with nested fields, nulls, or empty strings.",
                "eventDescriptions[] is canonical archive memory and must not be added to afterlifeChronicleUpdates[]. Keep lastEventsDescription as the current readable summary."
            },
            SafeCorrectionRules = new List<string>
            {
                "Use validation_repair_request.json.errors as the immediate checklist; its file paths and expected/actual values are authoritative.",
                "Convert each invalid persistentConsequences/openThreads element into one concise player-meaningful string, preserving the same narrative meaning.",
                "If the invalid value is an object, summarize its meaning into a single sentence string instead of keeping nested keys.",
                "Patch only afterlife chronicle fields named by the errors unless the same chronicle entry needs a minimal adjacent string-array correction to validate."
            },
            Steps = new List<string>
            {
                "Open game_state/control/validation_repair_request.json first and repair only the listed validation errors in place.",
                $"Patch these afterlife chronicle string-array fields exactly: {issueSummary}.",
                "In game_state/meta/afterlife_chronicles.json, inspect each listed chronicles[n].persistentConsequences/openThreads path and replace every invalid element with a non-empty string.",
                "Remove afterlifeChronicleUpdates from output/narrative_response.json if it is present there; output/narrative_response.json may contain only response and timestamp.",
                "Keep afterlifeChronicleUpdates only on the accepted afterlife chronicle update surface for game_state/meta/afterlife_chronicles.json, then repair the listed canonical chronicle fields.",
                "Preserve chronicleId, scopeType, scopeId, displayName, lastEventsDescription, and lastUpdatedTurn unless those exact fields are listed as validation errors.",
                "After file repairs are complete, call Complete-BoeValidationRepair as the last action, or create game_state/control/validation_repair_ready.json with exact sessionId/requestId/turnNumber from the current validation_repair_request.json."
            },
            DoNotDo = new List<string>
            {
                "Do not create a new turn or write ready/turn_complete.json during validation repair.",
                "Do not put afterlifeChronicleUpdates inside output/narrative_response.json; use the afterlife chronicle surface for game_state/meta/afterlife_chronicles.json.",
                "Do not put eventDescriptions[] inside afterlifeChronicleUpdates[]; archive eventDescriptions are read-only canonical memory.",
                "Do not replace the afterlife chronicle with Mortal worldEventsLog, NPC journals, faction chronicles, or location news.",
                "Do not read implementation code such as BookOfEternityClient/**/*.cs to infer repair rules; use this packet, validation_repair_request.json, afterlife docs/examples, and session control files."
            }
        };
    }

    private static ValidationRepairHarnessPacket BuildGuardianTradeInventoryResolutionRepairPacket(
        IReadOnlyList<ValidationIssue> tradeErrors)
    {
        var targetFiles = tradeErrors
            .Select(issue => NormalizeRepairTargetPath(issue.FilePath))
            .Where(path => !string.IsNullOrWhiteSpace(path))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (!targetFiles.Contains("game_state/meta/guardians.json", StringComparer.OrdinalIgnoreCase))
            targetFiles.Add("game_state/meta/guardians.json");
        if (!targetFiles.Contains(GuardianTradeRequestState.PendingRequestPath, StringComparer.OrdinalIgnoreCase))
            targetFiles.Add(GuardianTradeRequestState.PendingRequestPath);
        if (!targetFiles.Contains("output/debug_logs.json", StringComparer.OrdinalIgnoreCase))
            targetFiles.Add("output/debug_logs.json");

        var issueDetails = tradeErrors
            .Select(DescribeGuardianTradeInventoryResolutionRepairIssue)
            .Where(detail => !string.IsNullOrWhiteSpace(detail))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(detail => detail, StringComparer.OrdinalIgnoreCase)
            .ToList();
        var issueSummary = issueDetails.Count == 0
            ? "see validation_repair_request.json.errors for exact Guardian trade resolution errors"
            : string.Join("; ", issueDetails);

        return new ValidationRepairHarnessPacket
        {
            Kind = "guardian_trade_inventory_resolution_repair",
            Priority = "high",
            Title = "Guardian trade inventory request resolution repair",
            TargetFiles = targetFiles,
            TemplateRefs = new List<string>
            {
                "OtherGuides/Afterlife_Contract_Matrix.md",
                "Examples/E_CLI_Afterlife_Turns.txt"
            },
            ExpectedShape = new List<string>
            {
                "Read game_state/control/pending_guardian_trade_request.json as read-only authority. It supplies requestId, guardianId, guardianName, abodeId, returnCycleId, currentReputation, derivedTradeSlotCount, effectiveRarityCeilingBonusSteps, projectBonusSignature, and createdAtTurn.",
                "In game_state/meta/guardians.json, patch the matching guardian so guardian.tradeInventory.tradeCycleId equals request.returnCycleId and generatedAtUtc is a fresh ISO-8601 UTC timestamp.",
                "guardian.tradeInventory.generationReputationTier and pricingReputationTier must match the request currentReputation tier; effectiveRarityCeilingBonusSteps and projectBonusSignature must exactly match the request.",
                "guardian.tradeInventory.items must be an array with exactly request.derivedTradeSlotCount entries. Every item needs a unique non-empty slotId and player-facing item data suitable for the Guardian's domain.",
                $"Close the request with {GuardianTradeRequestState.UpdateReceiptsProperty} or guardians[].{GuardianTradeRequestState.ReceiptsProperty}: requestId, guardianId, abodeId, tradeCycleId, status=ready, itemCount matching tradeInventory.items, resolvedAtTurn > 0, and resolvedAtUtc ISO-8601 UTC timestamp."
            },
            SafeCorrectionRules = new List<string>
            {
                "Patch only the Guardian trade resolution fields named by validation plus adjacent receipt/inventory fields required for the same request to validate.",
                "If tradeInventory already contains useful items, preserve and reshape them to the request contract instead of deleting the vitrine.",
                "If tradeInventory is missing, create a compact valid vitrine with request.derivedTradeSlotCount domain-appropriate offers; use player-facing Russian names/descriptions and stable slotId values.",
                "Use the active/matching Guardian from guardians.json; do not create a new Guardian just to close the trade request.",
                "Keep pending_guardian_trade_request.json unchanged; it is client-owned authority for this repair."
            },
            Steps = new List<string>
            {
                "Open game_state/control/validation_repair_request.json first and repair only the listed Guardian trade errors in place.",
                $"Patch this Guardian trade checklist: {issueSummary}.",
                "Use Read-BoeJson -RelativePath 'game_state/control/pending_guardian_trade_request.json' to read the exact client-authored request; do not edit that file.",
                "Use Read-BoeJson -RelativePath 'game_state/meta/guardians.json' and find the guardian whose guardianId matches request.guardianId.",
                "Set that guardian.tradeInventory to a valid object matching request.returnCycleId, currentReputation tier, effectiveRarityCeilingBonusSteps, projectBonusSignature, and derivedTradeSlotCount.",
                $"Add a matching receipt through {GuardianTradeRequestState.UpdateReceiptsProperty} at the guardians root or through guardians[].{GuardianTradeRequestState.ReceiptsProperty}; include requestId, guardianId, guardianName, abodeId, tradeCycleId, status=ready, itemCount, resolvedAtTurn, and resolvedAtUtc.",
                "Repair output/debug_logs.json.gm_thoughts_markdown so the active Guardian is in Relevant actors and has situation/thoughts/actions explaining the vitrine.",
                "Write the repaired guardians.json with Write-BoeJson, then call Complete-BoeValidationRepair as the last action, or create validation_repair_ready.json with exact metadata from validation_repair_request.json."
            },
            DebugLogTemplate = string.Join(
                Environment.NewLine,
                "## Охват NPC-анализа",
                "Режим: Guardian-centric",
                "Релевантные акторы: <имя активного Хранителя>",
                "Почему они релевантны: Хранитель подготавливает торговую витрину по pending_guardian_trade_request.json.",
                "Акторы вне охвата: нет",
                "Почему они вне охвата: repair закрывает только торговую витрину активного Хранителя.",
                "",
                "## Guardian Thoughts",
                "### <имя активного Хранителя>",
                "- Ситуация: игрок запросил торговлю, и Хранитель подготавливает ограниченную витрину своего домена.",
                "- Мысли: кратко объясни, почему эти реликвии/услуги соответствуют домену и текущей репутации.",
                "- Действия: Хранитель materialize-ит tradeInventory и закрывает request receipt."),
            DoNotDo = new List<string>
            {
                "Do not create a new turn or write ready/turn_complete.json during validation repair.",
                "Do not rewrite pending_guardian_trade_request.json; it is the read-only client-authored contract for this repair.",
                "Do not delete the pending request from game_state/control; the client clears it after a valid receipt is accepted.",
                "Do not leave guardian.tradeInventory prose-only in narrative output; the vitrine must be materialized in game_state/meta/guardians.json.",
                "Do not change requestId, guardianId, returnCycleId/tradeCycleId, derivedTradeSlotCount, effectiveRarityCeilingBonusSteps, or projectBonusSignature to fit already written inventory.",
                "Do not read implementation code such as BookOfEternityClient/**/*.cs to infer trade rules; use this packet, validation_repair_request.json, afterlife docs/examples, and session state."
            }
        };
    }

    private static string DescribeAfterlifeChronicleStringArrayRepairIssue(ValidationIssue issue)
    {
        var normalizedPath = (issue.FilePath ?? string.Empty).Replace('\\', '/');
        var match = Regex.Match(
            normalizedPath,
            @"chronicles\[(?<index>\d+)\]\.(?<field>persistentConsequences|openThreads)(?:\[(?<entry>\d+)\])?",
            RegexOptions.IgnoreCase);
        var location = match.Success
            ? string.IsNullOrWhiteSpace(match.Groups["entry"].Value)
                ? $"chronicles[{match.Groups["index"].Value}].{match.Groups["field"].Value}"
                : $"chronicles[{match.Groups["index"].Value}].{match.Groups["field"].Value}[{match.Groups["entry"].Value}]"
            : NormalizeRepairTargetPath(normalizedPath);
        var expected = string.IsNullOrWhiteSpace(issue.Expected) ? "see error.expected" : issue.Expected.Trim();
        var actual = string.IsNullOrWhiteSpace(issue.Actual) ? "see error.actual" : issue.Actual.Trim();
        var code = string.IsNullOrWhiteSpace(issue.Code) ? "validation_error" : issue.Code.Trim();

        return $"{location}: expected {expected}, actual {actual} ({code})";
    }

    private static string DescribeGuardianTradeInventoryResolutionRepairIssue(ValidationIssue issue)
    {
        var location = NormalizeRepairTargetPath(issue.FilePath);
        var expected = string.IsNullOrWhiteSpace(issue.Expected) ? "matching Guardian tradeInventory/receipt for pending request" : issue.Expected.Trim();
        var actual = string.IsNullOrWhiteSpace(issue.Actual) ? "see error.actual" : issue.Actual.Trim();
        var code = string.IsNullOrWhiteSpace(issue.Code) ? "validation_error" : issue.Code.Trim();

        return $"{location}: expected {expected}, actual {actual} ({code})";
    }

    private static string DescribeAfterlifeRewardRepairIssue(ValidationIssue issue)
    {
        var normalizedPath = (issue.FilePath ?? string.Empty).Replace('\\', '/');
        var match = Regex.Match(
            normalizedPath,
            @"(?:recentConflicts|resolvedConflicts)\[(?<index>\d+)\](?:\.(?<field>rewardAudit(?:\.[A-Za-z0-9_]+)?|conflictId|diceAudit(?:\.[A-Za-z0-9_]+)?))?",
            RegexOptions.IgnoreCase);
        var location = match.Success
            ? string.IsNullOrWhiteSpace(match.Groups["field"].Value)
                ? $"recentConflicts[{match.Groups["index"].Value}]"
                : $"recentConflicts[{match.Groups["index"].Value}].{match.Groups["field"].Value}"
            : NormalizeRepairTargetPath(normalizedPath);
        var expected = string.IsNullOrWhiteSpace(issue.Expected) ? "see error.expected" : issue.Expected.Trim();
        var actual = string.IsNullOrWhiteSpace(issue.Actual) ? "see error.actual" : issue.Actual.Trim();
        var code = string.IsNullOrWhiteSpace(issue.Code) ? "validation_error" : issue.Code.Trim();

        return $"{location}: expected {expected}, actual {actual} ({code})";
    }
}
