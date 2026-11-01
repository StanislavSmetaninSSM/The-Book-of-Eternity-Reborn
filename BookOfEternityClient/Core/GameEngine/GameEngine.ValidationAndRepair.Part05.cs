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

    private List<ValidationIssue> CollectPlayerFacingOutputStaleAfterCanonicalRepairIssues(
        IReadOnlyCollection<ValidationIssue> repairedErrors,
        DateTime canonicalRepairBoundaryUtc)
    {
        var canonicalRepairCodes = repairedErrors
            .Where(issue => IsCanonicalStateRepairIssue(issue) &&
                            !IsPlayerFacingNeutralActorMemoryRepairIssue(issue))
            .Select(issue => string.IsNullOrWhiteSpace(issue.Code) ? issue.Category.ToString() : issue.Code!)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(code => code, StringComparer.OrdinalIgnoreCase)
            .ToArray();
        if (canonicalRepairCodes.Length == 0)
            return [];

        var issues = new List<ValidationIssue>();
        foreach (var outputPath in new[]
                 {
                     "output/narrative_response.json",
                     "output/interface_updates.json"
                 })
        {
            var outputFullPath = _fs.ResolvePath(outputPath);
            if (!File.Exists(outputFullPath))
                continue;

            var outputWrittenAtUtc = File.GetLastWriteTimeUtc(outputFullPath);
            if (outputWrittenAtUtc > canonicalRepairBoundaryUtc)
                continue;

            issues.Add(new ValidationIssue(
                outputPath,
                IssueSeverity.Error,
                $"{outputPath} был записан до canonical validation repair и может противоречить исправленному состоянию.",
                code: "accepted_turn_stale_player_facing_output_after_canonical_repair",
                section: "PlayerFacingOutput",
                expected: $"player-facing output rewritten after repaired canonical state was last written at {canonicalRepairBoundaryUtc:o}",
                actual: $"{outputPath} last write {outputWrittenAtUtc:o}; repaired canonical issues: {string.Join(", ", canonicalRepairCodes)}",
                repairHint: "Перепиши player-facing output под уже исправленное canonical state: обнови output/narrative_response.json.response и, если есть варианты выбора, output/interface_updates.json.dialogueOptions. Не меняй canonical state повторно, если validation_repair_request.json не перечисляет новые canonical ошибки."));
        }

        return issues;
    }

    private DateTime ResolveCanonicalRepairOutputFreshnessBoundaryUtc(
        IReadOnlyCollection<ValidationIssue> repairedErrors,
        DateTime repairStartedAtUtc,
        DateTime? unobservableFallbackUtc = null)
    {
        var canonicalTargetPaths = repairedErrors
            .Where(issue => IsCanonicalStateRepairIssue(issue) &&
                            !IsPlayerFacingNeutralActorMemoryRepairIssue(issue))
            .Select(issue => NormalizeCanonicalRepairTargetFilePath(issue.FilePath))
            .Where(PendingTurnSnapshotAuthority.IsSafeRelativePath)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        if (canonicalTargetPaths.Length == 0)
            return unobservableFallbackUtc ?? DateTime.UtcNow;

        var latestWriteUtc = DateTime.MinValue;
        var hasUnobservableTarget = false;
        foreach (var targetPath in canonicalTargetPaths)
        {
            try
            {
                var targetFullPath = _fs.ResolvePath(targetPath);
                if (!File.Exists(targetFullPath))
                {
                    hasUnobservableTarget = true;
                    continue;
                }

                var targetWriteUtc = File.GetLastWriteTimeUtc(targetFullPath);
                if (targetWriteUtc < repairStartedAtUtc)
                {
                    hasUnobservableTarget = true;
                    continue;
                }

                if (targetWriteUtc > latestWriteUtc)
                    latestWriteUtc = targetWriteUtc;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(
                    ex,
                    "Не удалось определить фактическую границу записи repaired canonical target {TargetPath}; freshness validation будет fail-closed.",
                    targetPath);
                hasUnobservableTarget = true;
            }
        }

        var fallbackUtc = unobservableFallbackUtc ?? DateTime.UtcNow;
        if (hasUnobservableTarget)
            return latestWriteUtc > fallbackUtc ? latestWriteUtc : fallbackUtc;
        return latestWriteUtc == DateTime.MinValue ? fallbackUtc : latestWriteUtc;
    }

    private static IReadOnlyCollection<ValidationIssue> MergeCanonicalRepairErrors(
        IReadOnlyCollection<ValidationIssue>? retainedErrors,
        IReadOnlyCollection<ValidationIssue> currentErrors)
    {
        return (retainedErrors ?? [])
            .Concat(currentErrors)
            .GroupBy(
                issue =>
                    $"{NormalizeCanonicalRepairTargetFilePath(issue.FilePath)}\u001f{issue.Code ?? issue.Category.ToString()}",
                StringComparer.OrdinalIgnoreCase)
            .Select(group => group.Last())
            .ToArray();
    }

    private static string NormalizeCanonicalRepairTargetFilePath(string path)
    {
        var normalized = NormalizeRepairTargetPath(path).Replace('\\', '/');
        var jsonExtensionIndex = normalized.IndexOf(".json", StringComparison.OrdinalIgnoreCase);
        return jsonExtensionIndex < 0
            ? normalized
            : normalized[..(jsonExtensionIndex + ".json".Length)];
    }

    private static bool IsCanonicalStateRepairIssue(ValidationIssue issue)
    {
        var path = NormalizeRepairTargetPath(issue.FilePath);
        return path.StartsWith("game_state/", StringComparison.OrdinalIgnoreCase) ||
               path.StartsWith("lore/", StringComparison.OrdinalIgnoreCase) ||
               IsAcceptedTurnMaterializationRepairIssue(issue);
    }

    private static bool IsAcceptedTurnMaterializationRepairIssue(ValidationIssue issue)
    {
        // Effect/resource validators report the exact raw command coordinate rather
        // than a canonical output path. A successful bounded repair still publishes
        // canonical after-images, so dependent player-facing output must be newer
        // than that repair just as it is for a direct game_state/* correction.
        return EffectRepairPacketBuilder.Build([issue], rollbackAvailable: true).Count > 0 ||
               ResourceRepairPacketBuilder.Build([issue]).Count > 0;
    }

    private static bool IsPlayerFacingNeutralActorMemoryRepairIssue(ValidationIssue issue)
    {
        if (IsGuardianThoughtJournalShapeRepairIssue(issue))
            return true;

        return (issue.Code ?? string.Empty).ToLowerInvariant() switch
        {
            "guardian_relevant_actor_missing_thought_journal_delta" => true,
            "mortal_npc_relevant_actor_missing_thought_journal_delta" => true,
            "afterlife_resident_relevant_actor_missing_thought_journal_delta" => true,
            "afterlife_entity_relevant_actor_missing_memory_ledger_delta" => true,
            "shining_faction_relevant_actor_missing_strategic_memory_delta" => true,
            "actor_thought_journal_not_first_person" => true,
            _ => false
        };
    }

    private async Task<bool> TryAutoRollbackRealmSegregationViolationsAsync(
        string source,
        IReadOnlyCollection<ValidationIssue> errors)
    {
        var realmSegregationErrors = errors
            .Where(error => string.Equals(error.Code, "realm_segregation_violation", StringComparison.OrdinalIgnoreCase))
            .ToList();
        if (realmSegregationErrors.Count == 0)
            return false;

        var forbiddenPaths = realmSegregationErrors
            .SelectMany(ExtractRealmSegregationPaths)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
        if (forbiddenPaths.Count == 0)
            return false;

        var sourceRealm = realmSegregationErrors
            .Select(ExtractRealmSegregationSourceRealm)
            .FirstOrDefault(realm => !string.IsNullOrWhiteSpace(realm));

        var rollbackService = new RealmSegregationAutoRollbackService(
            _fs,
            NullLogger<RealmSegregationAutoRollbackService>.Instance);
        var result = await rollbackService.TryRollbackForbiddenRealmMutationsAsync(
            sourceRealm,
            forbiddenPaths,
            source);

        if (!result.RolledBack)
            return false;

        _logger.LogWarning(
            "Client auto-rolled back {Count} forbidden realm mutations after {Source}; report: {ReportPath}",
            result.Actions.Count,
            source,
            result.ReportPath);
        return true;
    }

    private async Task<bool> TryAutoRepairStartupGuardianDirectMaterializationAsync(
        string source,
        IReadOnlyCollection<ValidationIssue> errors)
    {
        if (!errors.Any(IsGuardianPendingCreationMaterializationRepairIssue))
            return false;

        const string path = "game_state/meta/guardians.json";
        var json = await _fs.ReadFileAsync(path);
        if (string.IsNullOrWhiteSpace(json))
            return false;

        JsonObject root;
        try
        {
            if (JsonNode.Parse(json) is not JsonObject parsedRoot)
                return false;

            root = parsedRoot;
        }
        catch
        {
            return false;
        }

        if (root["UpdateGuardians"] is JsonArray existingUpdates && existingUpdates.Count > 0)
            return false;

        if (root["pendingGuardianCreation"] is not JsonObject pendingCreation ||
            !string.Equals(GetNodeString(pendingCreation["mode"]), "freeform", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (root["guardians"] is not JsonArray guardians)
            return false;

        var materializedGuardians = guardians.OfType<JsonObject>().ToList();
        if (materializedGuardians.Count != 1)
            return false;

        var guardian = materializedGuardians[0];
        if (!IsAutoRepairableStartupGuardianCreateCandidate(guardian))
            return false;

        var createData = CloneJsonObjectNode(guardian);
        NormalizeStartupGuardianCreateCandidate(createData);
        var guardianId = GetNodeString(createData["guardianId"]);
        root["UpdateGuardians"] = new JsonArray
        {
            new JsonObject
            {
                ["command"] = "create",
                ["data"] = createData.DeepClone()
            }
        };
        root["guardians"] = new JsonArray(createData.DeepClone());
        root["activeGuardian"] = createData.DeepClone();

        if (root["chaosSeaNavigation"] is JsonObject navigation)
        {
            navigation["currentGuardianId"] = guardianId;
            if (createData["abode"] is JsonObject abode)
            {
                var abodeId = GetNodeString(abode["abodeId"]);
                if (!string.IsNullOrWhiteSpace(abodeId))
                {
                    navigation["currentAbodeId"] = abodeId;
                    if (navigation["discoveredAbodes"] is not JsonArray discoveredAbodes ||
                        !discoveredAbodes.OfType<JsonValue>().Any(value =>
                            value.TryGetValue<string>(out var discovered) &&
                            string.Equals(discovered, abodeId, StringComparison.OrdinalIgnoreCase)))
                    {
                        navigation["discoveredAbodes"] = new JsonArray(abodeId);
                    }
                }
            }
        }

        root.Remove("pendingGuardianCreation");
        await _fs.WriteFileAtomicAsync(path, root.ToJsonString(JsonOpts));
        _logger.LogWarning(
            "Auto-repaired startup Guardian direct materialization after {Source}: synthesized UpdateGuardians.create for {GuardianId} and cleared pendingGuardianCreation.",
            source,
            guardianId);
        return true;
    }

    private static bool IsAutoRepairableStartupGuardianCreateCandidate(JsonObject guardian)
    {
        if (string.IsNullOrWhiteSpace(GetNodeString(guardian["guardianId"])) ||
            string.IsNullOrWhiteSpace(GetNodeString(guardian["canonicalName"])) ||
            guardian["manifestation"] is not JsonObject ||
            guardian["abode"] is not JsonObject ||
            guardian["personalityProfile"] is not JsonObject ||
            guardian["relationshipData"] is not JsonObject ||
            guardian["abodePower"] is not JsonObject ||
            guardian["questManagement"] is not JsonObject ||
            guardian["gachaSystem"] is not JsonObject ||
            guardian["mood"] is not JsonObject ||
            guardian["loreFragments"] is not JsonArray loreFragments ||
            loreFragments.Count < 7 ||
            guardian["musings"] is not JsonArray musings ||
            musings.Count == 0)
        {
            return false;
        }

        return true;
    }

    private static JsonObject CloneJsonObjectNode(JsonObject source)
    {
        return JsonNode.Parse(source.ToJsonString(JsonOpts))!.AsObject();
    }

    private static void NormalizeStartupGuardianCreateCandidate(JsonObject guardian)
    {
        if (guardian["relationshipData"] is JsonObject relationshipData)
        {
            if (relationshipData.TryGetPropertyValue("guardianRoleToPlayer", out var roleNode) &&
                !string.Equals(GetNodeString(roleNode), PlayerGuardianFoundationState.GuardianRoleFormerPatron, StringComparison.OrdinalIgnoreCase))
            {
                relationshipData.Remove("guardianRoleToPlayer");
            }

            var lastInteraction = GetNodeString(relationshipData["lastInteraction"]);
            if (!string.IsNullOrWhiteSpace(lastInteraction) &&
                !DateTimeOffset.TryParse(lastInteraction, out _))
            {
                relationshipData["lastInteraction"] = null;
            }
        }

        if (guardian["abodePower"] is JsonObject)
            AbodePowerRules.EnsureCanonicalState(guardian);
        GuardianGachaChargeRules.NormalizeGuardianGachaCompanionState(guardian);
        GuardianTradeRequestState.NormalizeGuardianTradeReceiptsShape(guardian);

        if (guardian["musings"] is JsonArray musings)
        {
            foreach (var musing in musings.OfType<JsonObject>())
            {
                var topic = GetNodeString(musing["topic"]);
                if (!IsAllowedStartupGuardianMusingTopic(topic))
                    musing["topic"] = "soul_assessment";

                var mood = GetNodeString(musing["mood"]);
                if (!IsAllowedStartupGuardianMusingMood(mood))
                    musing["mood"] = "contemplative";

                if (string.IsNullOrWhiteSpace(GetNodeString(musing["thought"])) &&
                    string.IsNullOrWhiteSpace(GetNodeString(musing["text"])))
                {
                    musing["thought"] = "Хранитель оценивает новую душу и условия первой встречи.";
                }
            }
        }
    }

    private static bool IsAllowedStartupGuardianMusingTopic(string topic)
    {
        return topic is "soul_assessment" or "domain_insight" or "guardian_politics" or "chaos_sea" or "personal_reflection" or "quest_planning";
    }

    private static bool IsAllowedStartupGuardianMusingMood(string mood)
    {
        return mood is "content" or "intrigued" or "concerned" or "amused" or "proud" or "disappointed" or "wary" or "nostalgic" or "determined" or "melancholic" or "excited" or "contemplative" or "irritated" or "hopeful";
    }

    private async Task<List<ValidationIssue>> FilterRestoredForbiddenRealmBaselineErrorsAsync(
        string source,
        List<ValidationIssue> errors)
    {
        if (errors.Count == 0)
            return errors;

        var rollbackService = new RealmSegregationAutoRollbackService(
            _fs,
            NullLogger<RealmSegregationAutoRollbackService>.Instance);
        var result = await rollbackService.FilterRestoredForbiddenBaselineIssuesAsync(
            _stateManager.CurrentState.CurrentRealm,
            errors);

        if (result.SuppressedIssues.Count == 0)
            return errors;

        _logger.LogWarning(
            "Suppressed {Count} restored forbidden-realm baseline validation errors after {Source}; these files match the validated pending-turn snapshot and remain outside GM repair scope for the current realm.",
            result.SuppressedIssues.Count,
            source);

        return PrioritizeValidationErrors(result.RemainingIssues).ToList();
    }

    private static IEnumerable<string> ExtractRealmSegregationPaths(ValidationIssue issue)
    {
        var actual = issue.Actual ?? string.Empty;
        if (string.IsNullOrWhiteSpace(actual))
            yield break;

        var pathList = actual.Split(" | surfaces:", 2, StringSplitOptions.None)[0];
        foreach (var candidate in pathList.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries))
        {
            var normalized = candidate.Replace('\\', '/');
            if (normalized.StartsWith("game_state/", StringComparison.OrdinalIgnoreCase))
                yield return normalized;
        }
    }

    private static string? ExtractRealmSegregationSourceRealm(ValidationIssue issue)
    {
        var match = Regex.Match(
            issue.Message,
            @"pre-turn realm '([^']+)'",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        return match.Success ? match.Groups[1].Value : null;
    }

    private static bool RequiresAcceptedTurnPayloadValidation(string source)
    {
        return source is "ответа GM" or "late response GM" or "обработки хода" or "оценки жизни";
    }

    private void ShowContractValidationErrors(string source, List<ValidationIssue> errors)
    {
        foreach (var issue in errors)
        {
            _logger.LogError(
                "Rejected state after {Source}: code={Code}; path={Path}; message={Message}; expected={Expected}; actual={Actual}; repairHint={RepairHint}",
                source,
                issue.Code,
                issue.FilePath,
                issue.Message,
                issue.Expected,
                issue.Actual,
                issue.RepairHint);
        }

        const string playerText =
            "Изменения мира не были приняты. Вернитесь к последнему устойчивому состоянию и повторите действие позже.";
        AnsiConsole.Write(new Panel(GameInterface.SafeMarkup(playerText))
        {
            Header = new PanelHeader(" Ход не принят ", Justify.Center),
            Border = BoxBorder.Double,
            BorderStyle = new Style(Color.Red),
            Padding = new Padding(2, 1),
            Expand = true
        });
        AnsiConsole.MarkupLine($"[grey]{_loc.T("press_any_key")}[/]");

        if (_inputSource is AgentConsoleLiveInputSource liveInput)
        {
            liveInput.PublishSnapshot(new AgentConsoleSnapshot
            {
                ScreenId = PlayerSafePausedScreenId,
                Mode = AgentConsoleMode.Error,
                Title = "Ход не принят",
                PlainText = playerText + Environment.NewLine + _loc.T("press_any_key"),
                AwaitingInput = true,
                InputKind = AgentConsoleInputKind.Key,
                Actions =
                [
                    new AgentConsoleAction
                    {
                        Id = "continue",
                        Label = "Продолжить",
                        Shortcut = "enter",
                        IsDefault = true
                    }
                ],
                RenderedAtUtc = DateTimeOffset.UtcNow,
                UpdatedAtUtc = DateTimeOffset.UtcNow,
                Diagnostics = []
            }, "Течение мира временно приостановлено.");
        }

        _inputSource.ReadKey(intercept: true);
    }

    internal static bool RequiresWoundRepairFailClosed(
        IReadOnlyList<ValidationIssue> errors,
        bool rollbackAvailable)
    {
        ArgumentNullException.ThrowIfNull(errors);
        var actionablePackets = WoundRepairPacketBuilder.Build(errors);
        return WoundRepairPacketBuilder.RequiresFailClosedRollback(errors) ||
               (actionablePackets.Count > 0 && !rollbackAvailable) ||
               errors.Any(static issue => string.Equals(
                   issue.Code,
                   "wound_repair_retry_authority_unavailable",
                   StringComparison.Ordinal));
    }

    private async Task<string> CaptureCurrentSessionGenerationAsync()
    {
        if (SessionOperationContext.TryGetExpectedGeneration(
                _fs.BasePath,
                out var boundSessionGeneration))
        {
            return boundSessionGeneration;
        }

        await using var writeLease = await _fs.AcquireCanonicalWriteLeaseAsync();
        return _fs.GetOrCreateSessionGeneration(writeLease);
    }
}
