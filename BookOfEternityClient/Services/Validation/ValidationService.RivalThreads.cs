using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using BookOfEternityClient.Core;
using BookOfEternityClient.Configuration;
using BookOfEternityClient.Models;
using Microsoft.Extensions.Logging;

namespace BookOfEternityClient.Services;
public partial class ValidationService
{
    private void ValidateRivalSoulArcArray(JsonElement root, string contextPrefix, List<ValidationIssue> issues, string propName)
    {
        if (!TryGetArray(root, propName, $"{contextPrefix}.{propName}", issues, out var arr))
            return;

        var activeMajorCount = 0;
        var activeMinorCount = 0;
        var index = 0;
        foreach (var item in arr.EnumerateArray())
        {
            var itemContext = $"{contextPrefix}.{propName}[{index++}]";
            if (!RequireObject(item, itemContext, issues))
                continue;

            ValidateRivalSoulArcObject(item, itemContext, issues);

            var scope = GetFirstNonEmptyString(item, "scope");
            var status = GetFirstNonEmptyString(item, "status");
            var isActive = !string.Equals(status, "resolved", StringComparison.OrdinalIgnoreCase) &&
                           !string.Equals(status, "failed", StringComparison.OrdinalIgnoreCase);

            if (isActive && string.Equals(scope, "major", StringComparison.OrdinalIgnoreCase))
                activeMajorCount++;
            if (isActive && string.Equals(scope, "minor", StringComparison.OrdinalIgnoreCase))
                activeMinorCount++;
        }

        if (activeMajorCount > 1)
        {
            issues.Add(new ValidationIssue(
                $"{contextPrefix}.{propName}",
                IssueSeverity.Error,
                "В одной смертной жизни нельзя держать более одной активной major rival soul arc",
                code: "rival_arc_major_cap_exceeded",
                section: "RivalSoulArcs",
                expected: "<= 1 active major arc",
                actual: activeMajorCount.ToString(),
                repairHint: "Оставь только одну active/non-terminal major arc. Остальные переведи в failed/resolved или убери."));
        }

        if (activeMinorCount > 1)
        {
            issues.Add(new ValidationIssue(
                $"{contextPrefix}.{propName}",
                IssueSeverity.Error,
                "В одной смертной жизни нельзя держать более одной активной minor rival soul arc",
                code: "rival_arc_minor_cap_exceeded",
                section: "RivalSoulArcs",
                expected: "<= 1 active minor arc",
                actual: activeMinorCount.ToString(),
                repairHint: "Оставь только одну active/non-terminal minor arc. Остальные переведи в failed/resolved или убери."));
        }
    }


    private void ValidateRivalSoulArcObject(JsonElement item, string itemContext, List<ValidationIssue> issues)
    {
        RequireString(item, itemContext, issues, "arcId");
        var scope = RequireString(item, itemContext, issues, "scope");
        var arcType = RequireString(item, itemContext, issues, "arcType");
        var status = RequireString(item, itemContext, issues, "status");
        RequireString(item, itemContext, issues, "objective");

        if (!string.IsNullOrWhiteSpace(scope) && !AllowedRivalSoulArcScopes.Contains(scope))
        {
            issues.Add(new ValidationIssue(
                $"{itemContext}.scope",
                IssueSeverity.Error,
                "rival soul arc scope должен быть одним из canonical enum значений",
                code: "rival_arc_invalid_scope",
                section: "RivalSoulArcs",
                expected: string.Join(" | ", AllowedRivalSoulArcScopes),
                actual: scope,
                repairHint: "Используй для scope только major или minor."));
        }

        if (!string.IsNullOrWhiteSpace(arcType) && !AllowedRivalSoulArcTypes.Contains(arcType))
        {
            issues.Add(new ValidationIssue(
                $"{itemContext}.arcType",
                IssueSeverity.Error,
                "rival soul arc type должен быть одним из canonical enum значений",
                code: "rival_arc_invalid_type",
                section: "RivalSoulArcs",
                expected: string.Join(" | ", AllowedRivalSoulArcTypes),
                actual: arcType,
                repairHint: "Используй только поддерживаемые arcType значения либо custom."));
        }

        if (!string.IsNullOrWhiteSpace(status) && !AllowedRivalSoulArcStatuses.Contains(status))
        {
            issues.Add(new ValidationIssue(
                $"{itemContext}.status",
                IssueSeverity.Error,
                "rival soul arc status должен быть одним из canonical enum значений",
                code: "rival_arc_invalid_status",
                section: "RivalSoulArcs",
                expected: string.Join(" | ", AllowedRivalSoulArcStatuses),
                actual: status,
                repairHint: "Используй для status только latent, rising, intersecting, resolved или failed."));
        }

        ValidateRivalSoulArcSponsorRef(item, itemContext, issues);
        ValidateRivalSoulArcRivalSoul(item, itemContext, issues);
        ValidateRivalSoulArcPlayerIntersection(item, itemContext, issues);

        if (!item.TryGetProperty("milestones", out var milestones) ||
            !TryGetArray(item, "milestones", $"{itemContext}.milestones", issues, out milestones))
        {
            return;
        }

        ValidateNonNegativeIntegerField(item, itemContext, issues, "currentStage", "RivalSoulArcs");
        ValidateRivalSoulArcMilestones(milestones, $"{itemContext}.milestones", issues);
        ValidateRivalSoulArcSignals(item, itemContext, issues);
        ValidateRivalSoulArcResolution(item, itemContext, issues);

        if (item.TryGetProperty("currentStage", out var currentStageNode) &&
            currentStageNode.ValueKind == JsonValueKind.Number &&
            currentStageNode.TryGetInt32(out var currentStage) &&
            milestones.ValueKind == JsonValueKind.Array)
        {
            var maxStage = -1;
            foreach (var milestone in milestones.EnumerateArray())
            {
                if (milestone.ValueKind == JsonValueKind.Object &&
                    milestone.TryGetProperty("stage", out var stageNode) &&
                    stageNode.ValueKind == JsonValueKind.Number &&
                    stageNode.TryGetInt32(out var stage))
                {
                    maxStage = Math.Max(maxStage, stage);
                }
            }

            if (maxStage >= 0 && currentStage > maxStage)
            {
                issues.Add(new ValidationIssue(
                    $"{itemContext}.currentStage",
                    IssueSeverity.Error,
                    "currentStage rival soul arc не может выходить за пределы описанных milestones",
                    code: "rival_arc_stage_out_of_range",
                    section: "RivalSoulArcs",
                    expected: $"<= {maxStage}",
                    actual: currentStage.ToString(),
                    repairHint: "Увеличь milestones или уменьшай currentStage так, чтобы он попадал в описанный milestone range."));
            }
        }

    }


    private void ValidateRivalSoulArcSponsorRef(JsonElement item, string itemContext, List<ValidationIssue> issues)
    {
        if (!item.TryGetProperty("sponsorGuardianRef", out var sponsorRef) ||
            !RequireObject(sponsorRef, $"{itemContext}.sponsorGuardianRef", issues))
        {
            issues.Add(new ValidationIssue(
                $"{itemContext}.sponsorGuardianRef",
                IssueSeverity.Error,
                "rival soul arc должен содержать sponsorGuardianRef object",
                code: "rival_arc_missing_sponsor_ref",
                section: "RivalSoulArcs",
                expected: "sponsorGuardianRef object",
                actual: !item.TryGetProperty("sponsorGuardianRef", out var actualNode) ? "missing" : actualNode.ValueKind.ToString(),
                repairHint: "Добавь sponsorGuardianRef с mode + displayName и guardianId/presetId в зависимости от режима."));
            return;
        }

        var sponsorContext = $"{itemContext}.sponsorGuardianRef";
        var mode = RequireString(sponsorRef, sponsorContext, issues, "mode");
        RequireString(sponsorRef, sponsorContext, issues, "displayName");

        if (!string.IsNullOrWhiteSpace(mode) && !AllowedRivalSoulArcSponsorModes.Contains(mode))
        {
            issues.Add(new ValidationIssue(
                $"{sponsorContext}.mode",
                IssueSeverity.Error,
                "sponsorGuardianRef.mode должен быть одним из canonical enum значений",
                code: "rival_arc_invalid_sponsor_mode",
                section: "RivalSoulArcs",
                expected: string.Join(" | ", AllowedRivalSoulArcSponsorModes),
                actual: mode,
                repairHint: "Используй sponsorGuardianRef.mode = guardianId или eternalPreset."));
        }

        if (string.Equals(mode, "guardianId", StringComparison.OrdinalIgnoreCase))
        {
            RequireString(sponsorRef, sponsorContext, issues, "guardianId");
            ValidateOptionalString(sponsorRef, sponsorContext, issues, "presetId");
        }
        else if (string.Equals(mode, "eternalPreset", StringComparison.OrdinalIgnoreCase))
        {
            RequireString(sponsorRef, sponsorContext, issues, "presetId");
            ValidateOptionalString(sponsorRef, sponsorContext, issues, "guardianId");
        }
    }


    private void ValidateRivalSoulArcRivalSoul(JsonElement item, string itemContext, List<ValidationIssue> issues)
    {
        if (!item.TryGetProperty("rivalSoul", out var rivalSoul) ||
            !RequireObject(rivalSoul, $"{itemContext}.rivalSoul", issues))
        {
            issues.Add(new ValidationIssue(
                $"{itemContext}.rivalSoul",
                IssueSeverity.Error,
                "rival soul arc должен содержать rivalSoul object",
                code: "rival_arc_missing_rival_soul",
                section: "RivalSoulArcs",
                expected: "rivalSoul object",
                actual: !item.TryGetProperty("rivalSoul", out var actualNode) ? "missing" : actualNode.ValueKind.ToString(),
                repairHint: "Добавь rivalSoul с rivalSoulId, displayNameOrMoniker, roleSummary и isKnownToPlayer."));
            return;
        }

        var rivalContext = $"{itemContext}.rivalSoul";
        RequireString(rivalSoul, rivalContext, issues, "rivalSoulId");
        RequireString(rivalSoul, rivalContext, issues, "displayNameOrMoniker");
        RequireString(rivalSoul, rivalContext, issues, "roleSummary");
        RequireBooleanField(rivalSoul, rivalContext, issues, "isKnownToPlayer");
    }


    private void ValidateRivalSoulArcPlayerIntersection(JsonElement item, string itemContext, List<ValidationIssue> issues)
    {
        if (!item.TryGetProperty("playerIntersection", out var playerIntersection) ||
            !RequireObject(playerIntersection, $"{itemContext}.playerIntersection", issues))
        {
            issues.Add(new ValidationIssue(
                $"{itemContext}.playerIntersection",
                IssueSeverity.Error,
                "rival soul arc должен содержать playerIntersection object",
                code: "rival_arc_missing_player_intersection",
                section: "RivalSoulArcs",
                expected: "playerIntersection object",
                actual: !item.TryGetProperty("playerIntersection", out var actualNode) ? "missing" : actualNode.ValueKind.ToString(),
                repairHint: "Добавь playerIntersection с targetsPlayerDirectly, stakes, canBecomeSoulQuest и recommendedCounterQuestTone."));
            return;
        }

        var intersectionContext = $"{itemContext}.playerIntersection";
        RequireBooleanField(playerIntersection, intersectionContext, issues, "targetsPlayerDirectly");
        RequireString(playerIntersection, intersectionContext, issues, "stakes");
        RequireBooleanField(playerIntersection, intersectionContext, issues, "canBecomeSoulQuest");
        RequireString(playerIntersection, intersectionContext, issues, "recommendedCounterQuestTone");
    }


    private void ValidateRivalSoulArcMilestones(JsonElement milestones, string contextPrefix, List<ValidationIssue> issues)
    {
        var seenStages = new HashSet<int>();
        var index = 0;
        foreach (var milestone in milestones.EnumerateArray())
        {
            var milestoneContext = $"{contextPrefix}[{index++}]";
            if (!RequireObject(milestone, milestoneContext, issues))
                continue;

            if (milestone.TryGetProperty("stage", out var stageNode) &&
                stageNode.ValueKind == JsonValueKind.Number &&
                stageNode.TryGetInt32(out var stage))
            {
                if (!seenStages.Add(stage))
                {
                    issues.Add(new ValidationIssue(
                        $"{milestoneContext}.stage",
                        IssueSeverity.Error,
                        "milestones не должны содержать дублирующиеся stage",
                        code: "rival_arc_duplicate_milestone_stage",
                        section: "RivalSoulArcs",
                        actual: stage.ToString(),
                        repairHint: "Каждый milestone должен иметь уникальный stage внутри одного rival soul arc."));
                }
            }
            else
            {
                ValidateNonNegativeIntegerField(milestone, milestoneContext, issues, "stage", "RivalSoulArcs");
            }

            RequireString(milestone, milestoneContext, issues, "title");
            RequireString(milestone, milestoneContext, issues, "summary");
            RequireBooleanField(milestone, milestoneContext, issues, "visibleToPlayer");
        }
    }


    private void ValidateRivalSoulArcSignals(JsonElement item, string itemContext, List<ValidationIssue> issues)
    {
        if (!TryGetArray(item, "publicSignals", $"{itemContext}.publicSignals", issues, out var signals))
            return;

        var signalIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var index = 0;
        foreach (var signal in signals.EnumerateArray())
        {
            var signalContext = $"{itemContext}.publicSignals[{index++}]";
            if (!RequireObject(signal, signalContext, issues))
                continue;

            var signalId = RequireString(signal, signalContext, issues, "signalId");
            if (!string.IsNullOrWhiteSpace(signalId) && !signalIds.Add(signalId))
            {
                issues.Add(new ValidationIssue(
                    $"{signalContext}.signalId",
                    IssueSeverity.Error,
                    "publicSignals не должны содержать дублирующиеся signalId",
                    code: "rival_arc_duplicate_signal_id",
                    section: "RivalSoulArcs",
                    actual: signalId,
                    repairHint: "Для каждого public signal используй уникальный signalId внутри arc."));
            }

            ValidateNonNegativeIntegerField(signal, signalContext, issues, "stage", "RivalSoulArcs");
            RequireString(signal, signalContext, issues, "description");
            RequireString(signal, signalContext, issues, "source");
            RequireBooleanField(signal, signalContext, issues, "visibleToPlayer");
            ValidateOptionalString(signal, signalContext, issues, "bonusClueSourceProjectId");
            ValidateOptionalString(signal, signalContext, issues, "bonusClueRevealId");
            if (signal.TryGetProperty("bonusClueCost", out _))
                ValidateNonNegativeIntegerField(signal, signalContext, issues, "bonusClueCost", "RivalSoulArcs");

            var bonusClueSourceProjectId = GetFirstNonEmptyString(signal, "bonusClueSourceProjectId");
            if (!string.IsNullOrWhiteSpace(bonusClueSourceProjectId) &&
                string.IsNullOrWhiteSpace(GetFirstNonEmptyString(signal, "bonusClueRevealId")))
            {
                issues.Add(new ValidationIssue(
                    $"{signalContext}.bonusClueRevealId",
                    IssueSeverity.Error,
                    "player-visible lore_research bonus clue должен иметь bonusClueRevealId для cross-surface dedupe",
                    code: "rival_arc_bonus_clue_missing_reveal_id",
                    section: "RivalSoulArcs",
                    repairHint: "Для bonusClueSourceProjectId передавай стабильный bonusClueRevealId. Если тот же clue mirrored в world event, используй тот же reveal id на обеих поверхностях."));
            }
        }
    }


    private void ValidateRivalSoulArcResolution(JsonElement item, string itemContext, List<ValidationIssue> issues)
    {
        if (!item.TryGetProperty("resolution", out var resolution) ||
            !RequireObject(resolution, $"{itemContext}.resolution", issues))
        {
            issues.Add(new ValidationIssue(
                $"{itemContext}.resolution",
                IssueSeverity.Error,
                "rival soul arc должен содержать resolution object",
                code: "rival_arc_missing_resolution",
                section: "RivalSoulArcs",
                expected: "resolution object",
                actual: !item.TryGetProperty("resolution", out var actualNode) ? "missing" : actualNode.ValueKind.ToString(),
                repairHint: "Добавь resolution с outcome и notes. Для незавершённой линии используй outcome=ongoing."));
            return;
        }

        var resolutionContext = $"{itemContext}.resolution";
        var outcome = RequireString(resolution, resolutionContext, issues, "outcome");
        RequireString(resolution, resolutionContext, issues, "notes");

        if (!string.IsNullOrWhiteSpace(outcome) && !AllowedRivalSoulArcResolutionOutcomes.Contains(outcome))
        {
            issues.Add(new ValidationIssue(
                $"{resolutionContext}.outcome",
                IssueSeverity.Error,
                "resolution.outcome должен быть одним из canonical enum значений",
                code: "rival_arc_invalid_resolution_outcome",
                section: "RivalSoulArcs",
                expected: string.Join(" | ", AllowedRivalSoulArcResolutionOutcomes),
                actual: outcome,
                repairHint: "Используй resolution.outcome = ongoing, player_supported, player_opposed, self_resolved, collapsed или unknown."));
        }
    }
}
