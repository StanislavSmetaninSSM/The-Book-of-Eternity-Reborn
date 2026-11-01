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

    private static ValidationRepairHarnessPacket BuildGuardianPendingCreationMaterializationRepairPacket(
        IReadOnlyList<ValidationIssue> creationErrors)
    {
        var targetFiles = creationErrors
            .Select(issue => NormalizeRepairTargetPath(issue.FilePath))
            .Where(path => !string.IsNullOrWhiteSpace(path))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (!targetFiles.Contains("game_state/meta/guardians.json", StringComparer.OrdinalIgnoreCase))
            targetFiles.Add("game_state/meta/guardians.json");
        if (!targetFiles.Contains("output/debug_logs.json", StringComparer.OrdinalIgnoreCase))
            targetFiles.Add("output/debug_logs.json");

        return new ValidationRepairHarnessPacket
        {
            Kind = "guardian_pending_creation_materialization_repair",
            Priority = "high",
            Title = "Startup pending Guardian creation materialization repair",
            TargetFiles = targetFiles,
            TemplateRefs = new List<string>
            {
                "TaskGuides/CLI_Step_Main.txt",
                "OtherGuides/Afterlife_Contract_Matrix.md",
                "Examples/E_CLI_Afterlife_Turns.txt"
            },
            CanonicalCreateSkeleton = BuildGuardianPendingCreationCanonicalCreateSkeleton(),
            AllowedEnums = BuildGuardianPendingCreationAllowedEnums(),
            ExpectedShape = new List<string>
            {
                "Read game_state/meta/guardians.json.pendingGuardianCreation as the startup request authority. For mode=freeform, use soulName and description; for mode=system_preset, use the exact requested preset identity.",
                "For a freeform New Game startup request, repair through the supported Guardian create surface `UpdateGuardians.create`: `UpdateGuardians`: [{ `command`: `create`, `data`: <full canonical Guardian> }]. The `data` object is the authority for the new Guardian identity.",
                "Use harnessRepairPackets[].canonicalCreateSkeleton as the bounded machine-readable starting point. Replace placeholder ids/names/prose from pendingGuardianCreation, but keep the same command/data shape and canonical field families.",
                "Keep scalar validation fields valid while replacing prose: relationshipData.lastInteraction must be null or ISO 8601, abodePower.tier must match abodePower.currentPower, timestamps must be ISO 8601, and abodePower.history may stay empty unless you can write canonical entries with timestamp/change/reason/source.",
                "Do not satisfy guardian_materialized_without_create_surface by editing only materialized mirrors. `guardians[]` and `activeGuardian` must match the create result, but the explicit `UpdateGuardians.create` command=create data=<full canonical Guardian> surface must be present in the repaired response.",
                "The accepted game_state/meta/guardians.json must contain the new Guardian in guardians[] with stable guardianId, displayName/canonicalName, title/domain/originType, abode identity, relationshipData/currentReputation, loreFragments, musings, trade/project/profile arrays or empty containers required by the canonical Guardian shape.",
                "Canonical Guardian create data must include at least 7 loreFragments. Use the allowed category and requiredReputation values from harnessRepairPackets[].allowedEnums.",
                "Canonical musings must include turn, topic, mood, and thought/text. Use only guardianMusingTopics and guardianMusingMoods from harnessRepairPackets[].allowedEnums.",
                "Do not invent relationshipData.guardianRoleToPlayer for normal startup Guardians. Omit it unless repairing an explicit foundation/former patron branch; in v1 the only allowed value is former_patron.",
                "activeGuardian must mirror the created Guardian, and chaosSeaNavigation.currentAbodeId must point to the created/discovered abode when the Guardian is now accessible.",
                "After guardians[] and activeGuardian are materialized, remove pendingGuardianCreation from guardians.json. Leaving pendingGuardianCreation unresolved or beside the materialized Guardian keeps /хранители empty or pending.",
                "output/debug_logs.json.gm_thoughts_markdown must be Guardian-centric or Mixed, list the Guardian by player-facing name in Relevant actors, and include situation/thoughts/actions explaining the materialization."
            },
            SafeCorrectionRules = new List<string>
            {
                "Preserve the player's requested soul name and freeform Guardian description as the source of the created Guardian's identity and domain details.",
                "If the already written Guardian has useful prose/domain details, copy/reshape them into UpdateGuardians[0].data as the full canonical Guardian object instead of leaving them only in guardians[] or activeGuardian.",
                "For New Game freeform startup repairs, do not keep the request as pending-only: the accepted repair must create the requested Guardian or remain rejected.",
                "For system_preset requests, do not substitute a similar Guardian; materialize the exact preset or route to the already materialized exact preset.",
                "Repair only the startup Guardian creation fields and matching debug-log actor coverage unless validation_repair_request.json lists additional errors."
            },
            Steps = new List<string>
            {
                "Open game_state/control/validation_repair_request.json first and repair only the listed startup Guardian creation errors in place.",
                "Use Read-BoeJson -RelativePath 'game_state/meta/guardians.json' and inspect pendingGuardianCreation before editing.",
                "Write or repair `UpdateGuardians.create` first: `UpdateGuardians`: [{ `command`: `create`, `data`: <full canonical Guardian> }]. This create command is the authority; do not repair only guardians[]/activeGuardian.",
                "Start from harnessRepairPackets[].canonicalCreateSkeleton. Replace placeholders with pendingGuardianCreation soulName/description, keep 7 loreFragments, keep mood/musings enum values from allowedEnums, and omit guardianRoleToPlayer unless the repair request explicitly names a former_patron foundation branch.",
                "Before writing, preserve canonical scalar constraints from the skeleton: lastInteraction stays null until a real ISO timestamp exists, abodePower.tier stays derived from currentPower, all timestamp fields stay ISO 8601, and abodePower.history stays empty unless every entry has timestamp/change/reason/source.",
                "For materialization, write a full canonical Guardian into guardians[], set activeGuardian to the matching mirror, update chaosSeaNavigation.currentAbodeId, and remove pendingGuardianCreation only after those roots are present.",
                "Repair output/debug_logs.json.gm_thoughts_markdown with Guardian-centric/Mixed scope, the Guardian's player-facing name in Relevant actors, and a matching Guardian Thoughts block.",
                "Write repaired files with Write-BoeJson, then call Complete-BoeValidationRepair as the last action, or create validation_repair_ready.json with exact metadata from validation_repair_request.json."
            },
            DebugLogTemplate = string.Join(
                Environment.NewLine,
                "## Охват NPC-анализа",
                "Режим: Guardian-centric",
                "Релевантные акторы: <имя создаваемого Хранителя>",
                "Почему они релевантны: pendingGuardianCreation просит материализовать этого Хранителя как доступного покровителя души.",
                "Акторы вне охвата: NPC смертного мира, другие Хранители",
                "Почему они вне охвата: repair закрывает только стартовое создание Хранителя.",
                "",
                "## Guardian Thoughts",
                "### <имя создаваемого Хранителя>",
                "- Ситуация: душа завершает стартовую встречу с Хранителем, описанным в pendingGuardianCreation.",
                "- Мысли: Хранитель оценивает форму души и решает, как открыть свою Обитель.",
                "- Действия: Хранитель материализуется с полной canonical identity, activeGuardian mirror, abode navigation и без stale pendingGuardianCreation."),
            DoNotDo = new List<string>
            {
                "Do not create a new turn or write ready/turn_complete.json during validation repair.",
                "Do not delete pendingGuardianCreation without materializing the requested Guardian into both guardians[] and activeGuardian.",
                "Do not keep a direct materialized Guardian object that lacks the supported UpdateGuardians.create semantics and full canonical Guardian shape.",
                "Do not repair only guardians[] or activeGuardian without UpdateGuardians command=create data=<full canonical Guardian>.",
                "Do not invent relationshipData.guardianRoleToPlayer such as mentor, teacher, patron, owner, bonded, or ally; omit the field unless the explicit valid value is former_patron.",
                "Do not use sentinel strings such as startup_turn for relationshipData.lastInteraction; use null or a real ISO 8601 timestamp.",
                "Do not hand-write abodePower.tier from prose; derive it from abodePower.currentPower.",
                "Do not add non-canonical abodePower.history entries such as delta/resultingPower-only audit notes; leave history empty instead.",
                "Do not use arbitrary musing topics/moods, arbitrary mood.current values, or fewer than 7 loreFragments.",
                "Do not leave the Guardian only in narrative prose while /хранители still has no canonical Guardian.",
                "Do not rewrite the requested freeform Guardian into an unrelated system preset or Mortal NPC.",
                "Do not read implementation code such as BookOfEternityClient/**/*.cs to infer startup Guardian rules; use this packet, validation_repair_request.json, afterlife docs/examples, and session state."
            }
        };
    }

    private static JsonObject BuildGuardianPendingCreationCanonicalCreateSkeleton()
    {
        const int startupAbodePower = 10;
        var startupTimestamp = DateTimeOffset.UtcNow.ToString("O");
        var guardianData = new JsonObject
        {
            ["guardianId"] = "guardian_<stable_slug_from_pending_guardian_creation>",
            ["canonicalName"] = "<guardian name from pendingGuardianCreation.description>",
            ["originType"] = "freeform",
            ["domain"] = "<short domain inferred from pendingGuardianCreation.description>",
            ["nameVariants"] = new JsonObject
            {
                ["default"] = "<guardian display name>",
                ["feminine"] = "<optional feminine form or same display name>",
                ["masculine"] = "<optional masculine form or same display name>",
                ["neutral"] = "<optional neutral form or same display name>"
            },
            ["manifestation"] = new JsonObject
            {
                ["currentDisplayName"] = "<guardian display name>",
                ["formFlexibility"] = "selective",
                ["currentPresentationStyle"] = "neutral",
                ["currentPronouns"] = "они/их",
                ["appearanceDescription"] = "<visual form from pendingGuardianCreation.description>"
            },
            ["manifestationHistory"] = new JsonArray(),
            ["abode"] = new JsonObject
            {
                ["abodeId"] = "abode_<stable_slug_from_guardian>",
                ["name"] = "<guardian abode name>",
                ["isDiscovered"] = true
            },
            ["personalityProfile"] = new JsonObject
            {
                ["archetype"] = "<short archetype>",
                ["speechPattern"] = "<how the guardian speaks>",
                ["coreValues"] = new JsonArray("memory", "discipline", "truth")
            },
            ["relationshipData"] = new JsonObject
            {
                ["currentReputation"] = 0,
                ["reputationHistory"] = new JsonArray(),
                ["lastInteraction"] = null
            },
            ["abodePower"] = new JsonObject
            {
                ["currentPower"] = startupAbodePower,
                ["tier"] = AbodePowerRules.GetTierLabel(startupAbodePower),
                ["lastUpdatedAt"] = startupTimestamp,
                ["history"] = new JsonArray()
            },
            ["guardianRelationships"] = new JsonArray(),
            ["questManagement"] = new JsonObject
            {
                ["availableQuests"] = new JsonArray(),
                ["activeQuests"] = new JsonArray(),
                ["completedQuests"] = new JsonArray()
            },
            ["gachaSystem"] = new JsonObject
            {
                ["currentReturnCycleId"] = string.Empty,
                ["gachaHistory"] = new JsonArray()
            },
            ["mood"] = new JsonObject
            {
                ["current"] = "focused",
                ["intensity"] = 40,
                ["reason"] = "startup Guardian materialization",
                ["since"] = 1
            },
            ["loreFragments"] = new JsonArray
            {
                BuildGuardianLoreFragmentSkeleton(1, "personal_history", 0),
                BuildGuardianLoreFragmentSkeleton(2, "domain_mastery", 0),
                BuildGuardianLoreFragmentSkeleton(3, "soul_mechanics", 50),
                BuildGuardianLoreFragmentSkeleton(4, "lost_world", 50),
                BuildGuardianLoreFragmentSkeleton(5, "other_guardians", 130),
                BuildGuardianLoreFragmentSkeleton(6, "cosmic_secret", 130),
                BuildGuardianLoreFragmentSkeleton(7, "personal_history", 230)
            },
            ["musings"] = new JsonArray
            {
                new JsonObject
                {
                    ["turn"] = 1,
                    ["topic"] = "soul_assessment",
                    ["mood"] = "contemplative",
                    ["thought"] = "<short private thought about the newly created soul>"
                }
            }
        };

        return new JsonObject
        {
            ["authoritySurface"] = "UpdateGuardians.create",
            ["UpdateGuardians"] = new JsonArray
            {
                new JsonObject
                {
                    ["command"] = "create",
                    ["data"] = guardianData
                }
            },
            ["materializedMirrorRules"] = new JsonObject
            {
                ["guardians[]"] = "copy the same created Guardian data into guardians[] after the create surface is present",
                ["activeGuardian"] = "mirror the same created Guardian",
                ["chaosSeaNavigation.currentAbodeId"] = "use data.abode.abodeId",
                ["pendingGuardianCreation"] = "remove only after create data, guardians[], activeGuardian, and chaosSeaNavigation are present"
            },
            ["forbiddenDirectMirrorRepair"] = new JsonArray(
                "do not repair only guardians[]",
                "do not repair only activeGuardian",
                "do not omit UpdateGuardians[0].command=create",
                "do not invent relationshipData.guardianRoleToPlayer for normal startup")
        };
    }

    private static JsonObject BuildGuardianLoreFragmentSkeleton(int index, string category, int requiredReputation) => new()
    {
        ["fragmentId"] = $"lore_<stable_slug>_{index}",
        ["category"] = category,
        ["title"] = $"<planned lore fragment {index}>",
        ["content"] = index <= 2 ? $"<visible starter lore fragment {index}>" : null,
        ["requiredReputation"] = requiredReputation
    };

    private static JsonObject BuildGuardianPendingCreationAllowedEnums() => new()
    {
        ["guardianMusingTopics"] = new JsonArray(
            "soul_assessment",
            "domain_insight",
            "guardian_politics",
            "chaos_sea",
            "personal_reflection",
            "quest_planning"),
        ["guardianMusingMoods"] = new JsonArray(
            "content",
            "intrigued",
            "concerned",
            "amused",
            "proud",
            "disappointed",
            "wary",
            "nostalgic",
            "determined",
            "melancholic",
            "excited",
            "contemplative",
            "irritated",
            "hopeful"),
        ["guardianMoodCurrent"] = new JsonArray(
            "welcoming",
            "contemplative",
            "energized",
            "melancholic",
            "irritated",
            "proud",
            "suspicious",
            "playful",
            "focused",
            "nostalgic"),
        ["guardianLoreFragmentCategories"] = new JsonArray(
            "personal_history",
            "cosmic_secret",
            "domain_mastery",
            "lost_world",
            "other_guardians",
            "soul_mechanics"),
        ["guardianLoreFragmentRequiredReputation"] = new JsonArray("0", "50", "130", "230"),
        ["guardianRoleToPlayerV1"] = new JsonArray("former_patron")
    };

    private static ValidationRepairHarnessPacket BuildGuardianScopeRepairPacket(
        IReadOnlyList<ValidationIssue> guardianScopeErrors,
        IReadOnlyCollection<string>? guardianActorNameHints = null)
    {
        var actorNames = CollectRepairActorNames(guardianScopeErrors, guardianActorNameHints)
            .OrderBy(actor => actor, StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (actorNames.Count == 0)
            actorNames.Add("активный Хранитель из game_state/meta/guardians.json");

        var targetFiles = guardianScopeErrors
            .Select(issue => NormalizeRepairTargetPath(issue.FilePath))
            .Where(path => !string.IsNullOrWhiteSpace(path))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (!targetFiles.Contains("output/debug_logs.json", StringComparer.OrdinalIgnoreCase))
            targetFiles.Add("output/debug_logs.json");
        if (!targetFiles.Contains("game_state/meta/guardians.json", StringComparer.OrdinalIgnoreCase))
            targetFiles.Add("game_state/meta/guardians.json");
        if (guardianScopeErrors.Any(issue => string.Equals(issue.Code, "guardian_project_materialized_state_outside_authority", StringComparison.OrdinalIgnoreCase)) &&
            !targetFiles.Contains("game_state/meta/guardian_projects.json", StringComparer.OrdinalIgnoreCase))
        {
            targetFiles.Add("game_state/meta/guardian_projects.json");
        }

        var actorList = string.Join(", ", actorNames);
        var headingExamples = string.Join(", ", actorNames.Select(actor => $"### {actor}"));
        var template = BuildGuardianScopeDebugLogTemplate(actorList, actorNames);

        return new ValidationRepairHarnessPacket
        {
            Kind = "guardian_scope_repair",
            Priority = "high",
            Title = "Guardian scope and materialized mirror repair",
            TargetFiles = targetFiles,
            CanonicalActorNames = actorNames,
            Steps = new List<string>
            {
                "In game_state/meta/guardians.json, do not treat current activeGuardian or current guardians[] as authority. Rewrite activeGuardian and guardians[] to the kernel-authoritative Guardian state reconstructed from the validated pre-turn snapshot plus authorized same-turn Guardian mutations.",
                "If game_state/meta/guardian_projects.json is listed, rewrite activeProjects to the kernel-authoritative project tracker state from the validated pre-turn snapshot plus authorized same-turn project commands; do not invent or start a project just because the current materialized file contains it.",
                $"In output/debug_logs.json.gm_thoughts_markdown, set NPC scope mode to Guardian-centric or Mixed and include exactly these Guardian names in Relevant actors: {actorList}.",
                $"Add a reasoning block for every listed Guardian. Minimal required heading shapes: {headingExamples}; use client-recognized bullet labels exactly like - Ситуация:, - Мысли:, and - Действия:.",
                "After file repairs are complete, call Complete-BoeValidationRepair as the last action, or create game_state/control/validation_repair_ready.json with exact sessionId/requestId/turnNumber from the current validation_repair_request.json."
            },
            DebugLogTemplate = template,
            DoNotDo = new List<string>
            {
                "Do not copy stale mirror-only activeGuardian aliases into Relevant actors.",
                "Do not use raw guardianId as the actor name in Relevant actors or reasoning headings.",
                "Do not remove the Guardian mutation from the turn just to silence scope validation unless the mutation was actually unintended.",
                "Do not read implementation code such as BookOfEternityClient/**/*.cs to infer repair rules; use this repair packet, the validation request, GM docs, and session snapshot/control files."
            }
        };
    }

    private static ValidationRepairHarnessPacket BuildActorReasoningSubpointRepairPacket(
        IReadOnlyList<ValidationIssue> actorReasoningErrors)
    {
        var actorNames = actorReasoningErrors
            .Select(issue => NormalizeRepairActorName(issue.Actor))
            .Where(actor => !string.IsNullOrWhiteSpace(actor))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(actor => actor, StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (actorNames.Count == 0)
            actorNames.Add("актор из ошибки validation_repair_request.json");

        var targetFiles = actorReasoningErrors
            .Select(issue => NormalizeRepairTargetPath(issue.FilePath))
            .Where(path => !string.IsNullOrWhiteSpace(path))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (!targetFiles.Contains("output/debug_logs.json", StringComparer.OrdinalIgnoreCase))
            targetFiles.Add("output/debug_logs.json");

        var actorList = string.Join(", ", actorNames);

        return new ValidationRepairHarnessPacket
        {
            Kind = "actor_reasoning_subpoint_repair",
            Priority = "high",
            Title = "Actor reasoning block/subpoint repair",
            TargetFiles = targetFiles,
            CanonicalActorNames = actorNames,
            Steps = new List<string>
            {
                $"In output/debug_logs.json.gm_thoughts_markdown, repair or create a missing reasoning block for exactly these actors: {actorList}.",
                "Inside every listed actor block, include separate bullet subpoints with these exact client-recognized labels: - Текущая локация:, - Ситуация:, - Данные профиля:, - Мотивация:, - Ограничения:, - Мысли:, - Варианты стратегий:, - Выбранная стратегия:, - Почему альтернативы отвергнуты:, - Действия:, and - Изменения состояния:.",
                "Use - Текущая локация: to state where the NPC is now, whether it remains there, or whether it moves to a known current/same-turn location.",
                "Under - Варианты стратегий: list at least two genuinely different numbered strategies; each strategy must explicitly contain Выгода: and Риск: on the same numbered line.",
                "Name the final choice under - Выбранная стратегия: and explain why the other options lost under - Почему альтернативы отвергнуты:.",
                "Under - Изменения состояния: name the exact canonical journal/state delta, or explicitly state that no state delta is justified; never hide an intended change only in prose.",
                "Keep the actor heading shape as ### <actor name>. Do not merge the required subpoints into one paragraph or one bullet.",
                "Preserve unrelated accepted debug log content and do not create a new turn.",
                "After file repairs are complete, call Complete-BoeValidationRepair as the last action, or create game_state/control/validation_repair_ready.json with exact sessionId/requestId/turnNumber from the current validation_repair_request.json."
            },
            DebugLogTemplate = BuildActorReasoningSubpointDebugLogTemplate(actorNames),
            DoNotDo = new List<string>
            {
                "Do not write ready/turn_complete.json for validation repair.",
                "Do not use slash-only labels or a single combined sentence when the validator requested separate subpoints.",
                "Do not read implementation code such as BookOfEternityClient/**/*.cs to infer repair rules; use this packet, the validation request, GM docs, and session files."
            }
        };
    }
}
