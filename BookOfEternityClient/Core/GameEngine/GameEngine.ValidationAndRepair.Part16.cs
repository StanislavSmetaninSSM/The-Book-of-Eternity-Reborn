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

    private static string DescribeAfterlifeEntityProfileScaffoldRepairIssue(ValidationIssue issue)
    {
        var normalizedPath = (issue.FilePath ?? string.Empty).Replace('\\', '/');
        var match = Regex.Match(
            normalizedPath,
            @"(?:profiles\[(?<profileIndex>\d+)\]\.(?<profileField>[A-Za-z0-9_.\[\]]+)|profileCommands\.specialArtLearningReceipts\[(?<receiptIndex>\d+)\](?:\.(?<receiptField>[A-Za-z0-9_]+))?)",
            RegexOptions.IgnoreCase);
        string location;
        if (match.Success && !string.IsNullOrWhiteSpace(match.Groups["profileIndex"].Value))
        {
            location = $"profiles[{match.Groups["profileIndex"].Value}].{match.Groups["profileField"].Value}";
        }
        else if (match.Success && !string.IsNullOrWhiteSpace(match.Groups["receiptIndex"].Value))
        {
            location = string.IsNullOrWhiteSpace(match.Groups["receiptField"].Value)
                ? $"profileCommands.specialArtLearningReceipts[{match.Groups["receiptIndex"].Value}]"
                : $"profileCommands.specialArtLearningReceipts[{match.Groups["receiptIndex"].Value}].{match.Groups["receiptField"].Value}";
        }
        else
        {
            location = NormalizeRepairTargetPath(normalizedPath);
        }

        var expected = string.IsNullOrWhiteSpace(issue.Expected) ? "see error.expected" : issue.Expected.Trim();
        var actual = string.IsNullOrWhiteSpace(issue.Actual) ? "see error.actual" : issue.Actual.Trim();
        var code = string.IsNullOrWhiteSpace(issue.Code) ? "validation_error" : issue.Code.Trim();

        return $"{location}: expected {expected}, actual {actual} ({code})";
    }

    private static ValidationRepairExactFieldCorrection BuildExactFieldCorrection(ValidationIssue issue)
    {
        return new ValidationRepairExactFieldCorrection
        {
            Path = issue.FilePath?.Trim() ?? "",
            Expected = string.IsNullOrWhiteSpace(issue.Expected) ? "see error.expected" : issue.Expected.Trim(),
            Actual = string.IsNullOrWhiteSpace(issue.Actual) ? "see error.actual" : issue.Actual.Trim(),
            Code = string.IsNullOrWhiteSpace(issue.Code) ? "validation_error" : issue.Code.Trim()
        };
    }

    private static string DescribeAfterlifeActionCostRepairIssue(ValidationIssue issue)
    {
        var normalizedPath = (issue.FilePath ?? string.Empty).Replace('\\', '/');
        var match = Regex.Match(
            normalizedPath,
            @"exchangeLog\[(?<index>\d+)\]\.actionCostAudit\.(?<side>player|opposition)\.(?<field>[A-Za-z0-9_]+)",
            RegexOptions.IgnoreCase);
        var location = match.Success
            ? $"exchangeLog[{match.Groups["index"].Value}] {match.Groups["side"].Value}.{match.Groups["field"].Value}"
            : NormalizeRepairTargetPath(normalizedPath);
        var expected = string.IsNullOrWhiteSpace(issue.Expected) ? "see error.expected" : issue.Expected.Trim();
        var actual = string.IsNullOrWhiteSpace(issue.Actual) ? "see error.actual" : issue.Actual.Trim();
        var code = string.IsNullOrWhiteSpace(issue.Code) ? "validation_error" : issue.Code.Trim();

        return $"{location}: expected {expected}, actual {actual} ({code})";
    }

    private static List<string> BuildMortalNpcTargetFiles(
        IReadOnlyList<ValidationIssue> errors,
        bool includeNpcCoreWhenMissing)
    {
        var targetFiles = errors
            .Select(issue => NormalizeRepairTargetPath(issue.FilePath))
            .Where(path => !string.IsNullOrWhiteSpace(path))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (includeNpcCoreWhenMissing &&
            !targetFiles.Contains("game_state/npcs/npc_core.json", StringComparer.OrdinalIgnoreCase))
        {
            targetFiles.Add("game_state/npcs/npc_core.json");
        }

        return targetFiles;
    }

    private static string NormalizeTrainingShowcaseRepairTargetPath(string path)
    {
        var normalized = path.Replace('\\', '/');
        if (normalized.StartsWith("game_state/npcs/npc_core.json", StringComparison.OrdinalIgnoreCase))
            return "game_state/npcs/npc_core.json";
        if (normalized.StartsWith("game_state/meta/afterlife_entity_profiles.json", StringComparison.OrdinalIgnoreCase))
            return "game_state/meta/afterlife_entity_profiles.json";
        if (normalized.StartsWith("game_state/control/pending_training_showcase_requests.json", StringComparison.OrdinalIgnoreCase))
            return "game_state/control/pending_training_showcase_requests.json";

        return NormalizeRepairTargetPath(path);
    }

    private static string NormalizeMortalSkillProgressionRepairTargetPath(string path)
    {
        var normalized = path.Replace('\\', '/');
        if (normalized.StartsWith("game_state/player/skills_active.json", StringComparison.OrdinalIgnoreCase))
            return "game_state/player/skills_active.json";
        if (normalized.StartsWith("game_state/player/skills_passive.json", StringComparison.OrdinalIgnoreCase))
            return "game_state/player/skills_passive.json";
        if (normalized.StartsWith("game_state/player/skill_mastery.json", StringComparison.OrdinalIgnoreCase))
            return "game_state/player/skill_mastery.json";
        if (normalized.StartsWith("game_state/control/pending_training_showcase_requests.json", StringComparison.OrdinalIgnoreCase))
            return "game_state/control/pending_training_showcase_requests.json";

        return NormalizeRepairTargetPath(path);
    }

    private async Task<IReadOnlyCollection<string>> ReadCurrentGuardianRepairActorNameHintsAsync()
    {
        const string path = "game_state/meta/guardians.json";
        var json = await _fs.ReadFileAsync(path);
        if (string.IsNullOrWhiteSpace(json))
            return Array.Empty<string>();

        try
        {
            if (JsonNode.Parse(json) is not JsonObject root)
                return Array.Empty<string>();

            var names = new List<string>();
            AddGuardianRepairActorNameHint(names, root["activeGuardian"]);
            if (root["guardians"] is JsonArray guardians)
            {
                foreach (var guardian in guardians)
                    AddGuardianRepairActorNameHint(names, guardian);
            }

            return names
                .Select(NormalizeRepairActorName)
                .Where(name => !string.IsNullOrWhiteSpace(name))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(name => name, StringComparer.OrdinalIgnoreCase)
                .ToArray();
        }
        catch
        {
            return Array.Empty<string>();
        }
    }

    private static HashSet<string> CollectRepairActorNames(
        IReadOnlyList<ValidationIssue> errors,
        IReadOnlyCollection<string>? fallbackHints = null)
    {
        var names = errors
            .Select(issue => NormalizeRepairActorName(issue.Actor))
            .Where(actor => !string.IsNullOrWhiteSpace(actor))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        if (names.Count == 0 && fallbackHints is { Count: > 0 })
        {
            foreach (var actor in fallbackHints.Select(NormalizeRepairActorName).Where(actor => !string.IsNullOrWhiteSpace(actor)))
                names.Add(actor);
        }

        return names;
    }

    private static void AddGuardianRepairActorNameHint(List<string> names, JsonNode? node)
    {
        if (node is not JsonObject guardian)
            return;

        foreach (var key in new[] { "displayName", "name", "guardianName" })
        {
            var value = guardian[key]?.GetValue<string>();
            if (!string.IsNullOrWhiteSpace(value))
            {
                names.Add(value);
                return;
            }
        }
    }

    private static bool IsGuardianReasoningSection(string? section)
    {
        return string.Equals(section, "npc_scope", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(section, "npc_reasoning", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(section, "Guardians", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(section, "UpdateGuardians", StringComparison.OrdinalIgnoreCase);
    }

    private static string NormalizeRepairActorName(string? actor)
    {
        if (string.IsNullOrWhiteSpace(actor))
            return string.Empty;

        return actor.Trim().TrimEnd('.', ',', ';', ':', '!', '?').Trim();
    }

    private static string NormalizeMortalBootstrapRepairTargetPath(string path)
    {
        var normalized = path.Replace('\\', '/');
        foreach (var bootstrapFile in new[]
                 {
                     "game_state/control/mortal_bootstrap_scaffold.json",
                     "lore/codex_entries.json",
                     "game_state/inventory/items.json",
                     "game_state/world/current_location.json",
                     "game_state/world/world_map.json",
                     "game_state/world/world_events.json",
                     "game_state/player/skills_active.json",
                     "game_state/player/skills_passive.json",
                     "game_state/player/skill_mastery.json"
                 })
        {
            if (normalized.StartsWith(bootstrapFile, StringComparison.OrdinalIgnoreCase))
                return bootstrapFile;
        }

        if (normalized.StartsWith("lore/current_world/", StringComparison.OrdinalIgnoreCase))
        {
            var jsonIndex = normalized.IndexOf(".json", StringComparison.OrdinalIgnoreCase);
            return jsonIndex >= 0 ? normalized[..(jsonIndex + ".json".Length)] : normalized;
        }

        return NormalizeRepairTargetPath(path);
    }

    private static string NormalizeRepairTargetPath(string path)
    {
        var normalized = path.Replace('\\', '/');
        if (normalized.StartsWith("game_state/meta/guardians.json", StringComparison.OrdinalIgnoreCase))
            return "game_state/meta/guardians.json";
        if (normalized.StartsWith("game_state/meta/guardian_projects.json", StringComparison.OrdinalIgnoreCase))
            return "game_state/meta/guardian_projects.json";
        if (normalized.StartsWith("game_state/meta/afterlife_chronicles.json", StringComparison.OrdinalIgnoreCase))
            return "game_state/meta/afterlife_chronicles.json";
        if (normalized.StartsWith("game_state/meta/afterlife_spiritual_conflict_state.json", StringComparison.OrdinalIgnoreCase))
            return "game_state/meta/afterlife_spiritual_conflict_state.json";
        if (normalized.StartsWith(GuardianAbodeResidentState.StatePath, StringComparison.OrdinalIgnoreCase))
            return GuardianAbodeResidentState.StatePath;
        if (normalized.StartsWith("game_state/world/current_location.json", StringComparison.OrdinalIgnoreCase))
            return "game_state/world/current_location.json";
        if (normalized.StartsWith("game_state/world/world_map.json", StringComparison.OrdinalIgnoreCase))
            return "game_state/world/world_map.json";
        if (normalized.StartsWith("output/narrative_response.json", StringComparison.OrdinalIgnoreCase))
            return "output/narrative_response.json";
        if (normalized.StartsWith("output/debug_logs.json", StringComparison.OrdinalIgnoreCase))
            return "output/debug_logs.json";
        foreach (var npcFile in new[]
                 {
                     "game_state/npcs/npc_core.json",
                     "game_state/npcs/npc_journals.json",
                     "game_state/npcs/npc_interaction_journal.json",
                     "game_state/npcs/npc_masks.json",
                     "game_state/npcs/npc_memory.json"
                 })
        {
            if (normalized.StartsWith(npcFile, StringComparison.OrdinalIgnoreCase))
                return npcFile;
        }
        foreach (var factionFile in new[]
                 {
                     "game_state/factions/faction_core.json",
                     "game_state/factions/faction_structure.json",
                     "game_state/factions/faction_resources.json",
                     "game_state/factions/faction_projects.json",
                     "game_state/factions/faction_custom.json",
                     "game_state/factions/faction_chronicles.json"
                 })
        {
            if (normalized.StartsWith(factionFile, StringComparison.OrdinalIgnoreCase))
                return factionFile;
        }
        return normalized;
    }

    private static string BuildGuardianScopeDebugLogTemplate(string actorList, IReadOnlyList<string> actorNames)
    {
        var builder = new StringBuilder();
        builder.AppendLine("## Охват NPC-анализа");
        builder.AppendLine("Режим: Guardian-centric");
        builder.AppendLine($"Релевантные акторы: {actorList}");
        builder.AppendLine("Почему они релевантны: ход меняет или использует активного Хранителя, поэтому его нужно явно покрыть reasoning scope.");
        builder.AppendLine("Акторы вне охвата: нет");
        builder.AppendLine("Почему они вне охвата: все измененные Guardian-сущности перечислены выше.");
        builder.AppendLine();
        builder.AppendLine("## Guardian Thoughts");
        foreach (var actor in actorNames)
        {
            builder.AppendLine($"### {actor}");
            builder.AppendLine("- Текущая локация: кратко укажи, где актор находится и остаётся ли он там или перемещается.");
            builder.AppendLine("- Ситуация: кратко опиши текущую ситуацию Хранителя в этом ходе.");
            AppendFullActorBrainDecisionTemplate(builder);
            builder.AppendLine();
        }

        return builder.ToString().TrimEnd();
    }

    private static string BuildAcceptedTurnOutputArtifactDebugLogTemplate()
    {
        var builder = new StringBuilder();
        builder.AppendLine("## Охват NPC-анализа");
        builder.AppendLine("Режим: None | Scene-local | World-progression | Guardian-centric | Mixed");
        builder.AppendLine("Релевантные акторы: <точные имена из уже принятого хода или нет только для действительно actorless scope>");
        builder.AppendLine("Почему они релевантны: <сохрани уже принятое обоснование>");
        builder.AppendLine("Акторы вне охвата: <точные имена или нет>");
        builder.AppendLine("Почему они вне охвата: <сохрани уже принятое обоснование>");
        builder.AppendLine();
        builder.AppendLine("Если релевантных акторов нет, остановись после полного scope-блока и не выдумывай Actor Brain.");
        builder.AppendLine("Если релевантные акторы есть, сохрани отдельный полный блок для каждого:");
        builder.AppendLine();
        builder.AppendLine("## Actor Brain 2.0");
        builder.AppendLine("### <точное имя релевантного актора>");
        builder.AppendLine("- Текущая локация: сохрани принятую локацию и решение остаться/переместиться.");
        builder.AppendLine("- Ситуация: сохрани принятую ситуацию этого же хода.");
        AppendFullActorBrainDecisionTemplate(builder);

        return builder.ToString().TrimEnd();
    }

    private static string BuildActorReasoningSubpointDebugLogTemplate(IReadOnlyList<string> actorNames)
    {
        var builder = new StringBuilder();
        builder.AppendLine("## Размышления акторов");
        foreach (var actor in actorNames)
        {
            builder.AppendLine($"### {actor}");
            builder.AppendLine("- Текущая локация: кратко укажи, где NPC находится сейчас и остаётся ли он там или перемещается.");
            builder.AppendLine("- Ситуация: кратко опиши, в каком положении актор находится в этом ходе.");
            AppendFullActorBrainDecisionTemplate(builder);
            builder.AppendLine();
        }

        return builder.ToString().TrimEnd();
    }

    private static string BuildNpcScopeDeclarationDebugLogTemplate(IReadOnlyList<string> actorNames)
    {
        var requiredActors = actorNames.Count > 0
            ? actorNames
            : new[] { "<actor name>" };
        var actorList = actorNames.Count > 0
            ? string.Join(", ", actorNames)
            : "<actor name 1>, <actor name 2> OR нет only for a truly actorless Scene-local turn";
        var builder = new StringBuilder();
        builder.AppendLine("## NPC Scope");
        builder.AppendLine("- Mode: Scene-local | World-progression | Guardian-centric | Mixed");
        builder.AppendLine($"- Relevant actors: {actorList}");
        builder.AppendLine("- Why relevant: <why these actors directly act, react, anchor the scene, or receive structured state>");
        builder.AppendLine("- Actors outside scope: <names or нет>");
        builder.AppendLine("- Why outside scope: <why other mentioned actors do not receive structured updates>");
        builder.AppendLine();
        builder.AppendLine("## Actor Brain 2.0");
        foreach (var actor in requiredActors)
        {
            builder.AppendLine($"### {actor}");
            builder.AppendLine("- Текущая локация: укажи, где актор находится и остаётся ли он там или перемещается.");
            builder.AppendLine("- Ситуация: опиши, что актор воспринимает и решает в этом ходу.");
            AppendFullActorBrainDecisionTemplate(builder);
            builder.AppendLine();
        }

        return builder.ToString().TrimEnd();
    }

    private static void AppendFullActorBrainDecisionTemplate(StringBuilder builder)
    {
        builder.AppendLine("- Данные профиля: перечисли только релевантные черты, отношения, память, роль, домен и текущее состояние.");
        builder.AppendLine("- Мотивация: чего актор хочет добиться именно сейчас и почему.");
        builder.AppendLine("- Ограничения: чего актор не знает, не может или не станет делать.");
        builder.AppendLine("- Мысли: кратко опиши внутреннюю оценку ситуации от лица актора.");
        builder.AppendLine("- Варианты стратегий:");
        builder.AppendLine("  1. <реально отличимая стратегия>. Выгода: <что получает актор>. Риск: <чем он рискует>.");
        builder.AppendLine("  2. <реально отличимая стратегия>. Выгода: <что получает актор>. Риск: <чем он рискует>.");
        builder.AppendLine("- Выбранная стратегия: назови итоговую линию поведения.");
        builder.AppendLine("- Почему альтернативы отвергнуты: объясни, почему остальные стратегии хуже в текущем контексте.");
        builder.AppendLine("- Действия: кратко опиши, что актор делает, решает или меняет.");
        builder.AppendLine("- Изменения состояния: перечисли точные canonical surfaces, включая собственный журнал мыслей, или явно обоснуй отсутствие изменения.");
    }

    /// <summary>
    /// Dispatches ordinary repair or an explicit spiritual continuation through the configured worker bridge.
    /// </summary>
    /// <param name="prioritizedErrors">
    /// Actual validation diagnostics; an owner-issued continuation may supply an empty list.
    /// </param>
    /// <param name="requestMetadata">
    /// Exact original session, request and turn identity.
    /// </param>
    /// <param name="createdAtUtc">
    /// UTC creation timestamp of the request already published for this dispatch.
    /// </param>
    /// <param name="attempt">
    /// Positive request attempt number used for worker task correlation.
    /// </param>
    /// <param name="expectedSessionGeneration">
    /// Original session generation that must remain current.
    /// </param>
    /// <param name="spiritualWoundContinuation">
    /// Owner-derived bounded continuation, or <see langword="null"/> for ordinary repair.
    /// </param>
    /// <returns>
    /// Worker dispatch and Ready outcome; acceptance without Ready does not consume a continuation response.
    /// </returns>
    private async Task<GmWorkerValidationRepairDispatchResult> RunWorkerValidationRepairIfAvailableAsync(
        IReadOnlyList<ValidationIssue> prioritizedErrors,
        (string SessionId, string RequestId, int TurnNumber) requestMetadata,
        string createdAtUtc,
        int attempt,
        string expectedSessionGeneration,
        SpiritualWoundContinuationRequest? spiritualWoundContinuation = null)
    {
        try
        {
            var workerFileSystem = _validator.CanonicalFileSystem;
            var audit = new GmWorkerAuditLog(workerFileSystem);
            var delegator = new GmWorkerValidationRepairDelegator(
                workerFileSystem,
                new GmWorkerBridgePool(
                    workerFileSystem,
                    new GmWorkerProposalStore(workerFileSystem),
                    audit),
                new GmWorkerApplyGate(_validator, audit),
                audit);
            var result = await delegator.TryRunAsync(
                _stateManager.Settings.GmWorkerBridgeProfiles,
                prioritizedErrors,
                new WorkerTurnReference
                {
                    SessionId = requestMetadata.SessionId,
                    RequestId = requestMetadata.RequestId,
                    TurnNumber = requestMetadata.TurnNumber
                },
                createdAtUtc,
                attempt,
                expectedSessionGeneration,
                spiritualWoundContinuation: spiritualWoundContinuation);

            if (result.Outcome is not GmWorkerValidationRepairOutcome.SkippedNoWorker and
                not GmWorkerValidationRepairOutcome.Applied)
            {
                _logger.LogWarning(
                    "GM worker validation repair ended with {Outcome}: {Reason}. Legacy repair loop remains active.",
                    result.Outcome,
                    result.FallbackReason);
            }

            return result;
        }
        catch (SessionReplacedException ex)
        {
            return new GmWorkerValidationRepairDispatchResult
            {
                Outcome = GmWorkerValidationRepairOutcome.SessionReplaced,
                FallbackReason = ex.Message
            };
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to run GM worker validation repair. Legacy repair loop remains active.");
            return new GmWorkerValidationRepairDispatchResult
            {
                Outcome = GmWorkerValidationRepairOutcome.WorkerFailed,
                FallbackReason = ex.Message
            };
        }
    }

    private async Task WriteTerminalProtocolFailureRequestAsync(string source, List<ValidationIssue> errors)
    {
        var prioritizedErrors = PrioritizeValidationErrors(errors).ToList();
        var pendingSnapshot = await ResolveActivePendingTurnSnapshotContextAsync();
        var requestMetadata = BuildProtocolRequestMetadata(pendingSnapshot);
        var metadataDiagnosticOnly = BuildProtocolRequestMetadataDiagnosticOnly(pendingSnapshot);
        var degradedMetadataWarning = BuildProtocolRequestMetadataWarning(pendingSnapshot);
        var gmInstructions =
            "Клиент отклонил terminal ready signal как protocol failure. Это НЕ validation_repair_request.json и НЕ repair loop. " +
            "Не создавай validation_repair_ready.json и не пытайся продолжать этот уже закрытый wait cycle. " +
            "Прочитай TaskGuides/CLI_Step_Main.txt и Examples/E_CLI_Step_Main.txt, разберись с terminal protocol problem по списку ошибок ниже и исправь логику для следующего корректного хода.";
        if (!string.IsNullOrWhiteSpace(degradedMetadataWarning))
            gmInstructions += " " + degradedMetadataWarning;

        var request = new TerminalProtocolFailureRequest
        {
            SessionId = requestMetadata.SessionId,
            RequestId = requestMetadata.RequestId,
            TurnNumber = requestMetadata.TurnNumber,
            MetadataDiagnosticOnly = metadataDiagnosticOnly,
            Source = source,
            DetectedAtUtc = DateTime.UtcNow.ToString("o"),
            GmInstructions = gmInstructions,
            SummaryGroups = BuildValidationSummaryLines(prioritizedErrors, 6),
            Errors = prioritizedErrors.Select(e => new ValidationRepairIssue
            {
                Code = e.Code ?? "terminal_protocol_failure",
                FilePath = e.FilePath,
                Severity = e.Severity.ToString(),
                Category = e.Category.ToString(),
                Message = e.Message,
                Actor = e.Actor,
                Section = e.Section,
                Expected = e.Expected,
                Actual = e.Actual,
                RepairHint = e.RepairHint ?? "Исправь terminal completion protocol так, чтобы клиент получил ровно один корректный terminal signal."
            }).ToList()
        };

        await _fs.WriteFileAtomicAsync(TerminalProtocolFailureRequestPath, JsonSerializer.Serialize(request, JsonOpts));
    }
}
