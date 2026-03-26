using System.Text.Json;
using System.Text.Json.Nodes;
using BookOfEternityClient.Core;
using Microsoft.Extensions.Logging;

namespace BookOfEternityClient.Services;
public partial class CanonicalStateNormalizer
{
    private static void CollectGuardianProjectEntries(JsonObject? root, string propName, List<JsonObject> target)
    {
        if (root?[propName] is not JsonArray arr)
            return;

        foreach (var item in arr.OfType<JsonObject>())
            UpsertGuardianProjectEntry(target, CloneObject(item));
    }

    private static JsonObject GetOrCreateGuardianProjectEntry(List<JsonObject> entries, string guardianId, string projectId)
    {
        var existing = entries.FirstOrDefault(item =>
            string.Equals(GuardianProjectState.GetGuardianId(item), guardianId, StringComparison.OrdinalIgnoreCase) &&
            string.Equals(GuardianProjectState.GetProjectId(item), projectId, StringComparison.OrdinalIgnoreCase));
        if (existing != null)
            return existing;

        var created = new JsonObject
        {
            ["guardianId"] = guardianId,
            ["project"] = new JsonObject
            {
                ["projectId"] = projectId
            }
        };
        entries.Add(created);
        return created;
    }

    private static void UpsertGuardianProjectEntry(List<JsonObject> entries, JsonObject candidate)
    {
        var guardianId = GuardianProjectState.GetGuardianId(candidate);
        var projectId = GuardianProjectState.GetProjectId(candidate);
        var existing = entries.FirstOrDefault(item =>
            string.Equals(GuardianProjectState.GetGuardianId(item), guardianId, StringComparison.OrdinalIgnoreCase) &&
            string.Equals(GuardianProjectState.GetProjectId(item), projectId, StringComparison.OrdinalIgnoreCase));
        if (existing != null)
        {
            MergeObject(existing, candidate);
            return;
        }

        entries.Add(candidate.DeepClone()!.AsObject());
    }

    private static void ApplyGuardianProjectStartCommands(
        List<JsonObject> activeProjects,
        List<JsonObject> temporaryModifiers,
        JsonArray commands,
        List<JsonObject> journalEntries,
        int currentTurn)
    {
        foreach (var command in commands.OfType<JsonObject>())
        {
            var guardianId = GetNodeString(command["guardianId"]);
            if (string.IsNullOrWhiteSpace(guardianId) || command["project"] is not JsonObject project)
                continue;

            var normalizedProject = CloneObject(project);
            ApplyGuardianProjectStartModifiers(temporaryModifiers, guardianId!, normalizedProject);
            NormalizeGuardianProjectObject(normalizedProject, active: true);
            var projectId = GetNodeString(normalizedProject["projectId"]);
            if (string.IsNullOrWhiteSpace(projectId))
                continue;

            activeProjects.RemoveAll(item => string.Equals(GuardianProjectState.GetGuardianId(item), guardianId, StringComparison.OrdinalIgnoreCase));
            var entry = new JsonObject
            {
                ["guardianId"] = guardianId,
                ["project"] = normalizedProject
            };
            UpsertGuardianProjectEntry(activeProjects, entry);
            journalEntries.Add(BuildGuardianProjectJournalEntry(
                currentTurn,
                guardianId,
                projectId,
                "started",
                normalizedProject,
                "Проект Хранителя начат",
                "Хранитель запустил новый проект в своей Обители.",
                previousProject: null));
        }
    }

    private static void ApplyGuardianProjectUpdateCommands(
        List<JsonObject> activeProjects,
        JsonArray commands,
        List<JsonObject> journalEntries,
        List<JsonObject> powerEvents,
        int currentTurn)
    {
        foreach (var command in commands.OfType<JsonObject>())
        {
            var guardianId = GetNodeString(command["guardianId"]);
            var projectId = GetNodeString(command["projectId"]);
            if (string.IsNullOrWhiteSpace(guardianId) || string.IsNullOrWhiteSpace(projectId))
                continue;

            var entry = GetOrCreateGuardianProjectEntry(activeProjects, guardianId!, projectId!);
            var project = entry["project"] as JsonObject ?? new JsonObject();
            var previousProject = CloneObject(project);

            if (!string.IsNullOrWhiteSpace(GetNodeString(command["activeState"])))
                project["activeState"] = GetNodeString(command["activeState"]);
            if (command["workDone"] != null)
                project["workDone"] = command["workDone"]?.DeepClone();
            if (command["currentStage"] != null)
                project["currentStage"] = command["currentStage"]?.DeepClone();
            if (command["pressure"] != null)
                project["pressure"] = command["pressure"]?.DeepClone();
            if (command["stability"] != null)
                project["stability"] = command["stability"]?.DeepClone();
            if (command["pressureAudit"] != null)
                project["pressureAudit"] = command["pressureAudit"]?.DeepClone();
            if (command["stabilityAudit"] != null)
                project["stabilityAudit"] = command["stabilityAudit"]?.DeepClone();
            if (command["workAudit"] != null)
                project["workAudit"] = command["workAudit"]?.DeepClone();

            NormalizeGuardianProjectObject(project, active: true);
            entry["guardianId"] = guardianId;
            entry["project"] = project;

            foreach (var powerEvent in BuildGuardianProjectUpdatePowerEvents(guardianId!, projectId!, project, command, currentTurn))
                powerEvents.Add(powerEvent);

            if (HasGuardianProjectVisibleChange(previousProject, project))
            {
                journalEntries.Add(BuildGuardianProjectJournalEntry(
                    currentTurn,
                    guardianId,
                    projectId,
                    ResolveGuardianProjectUpdateEventType(previousProject, project),
                    project,
                    BuildGuardianProjectUpdateTitle(previousProject, project),
                    BuildGuardianProjectUpdateSummary(previousProject, project),
                    previousProject));
            }
        }
    }

    private static IEnumerable<JsonObject> BuildGuardianProjectUpdatePowerEvents(
        string guardianId,
        string projectId,
        JsonObject project,
        JsonObject command,
        int currentTurn)
    {
        var projectName = GetNodeString(project["projectName"]) ?? GetNodeString(project["name"]) ?? projectId;

        if (command["assistAudit"] is JsonObject assistAudit)
        {
            var classification = GetNodeString(assistAudit["classification"]);
            var delta = AbodePowerRules.ResolveGuardianProjectAssistPowerDelta(classification);
            if (delta != 0)
            {
                var reasonType = ResolveGuardianProjectAssistReasonType(assistAudit);
                var audit = CloneObject(assistAudit);
                audit["projectId"] ??= projectId;
                audit["projectName"] ??= projectName;
                audit["projectType"] ??= GetNodeString(project["projectType"]);
                audit["projectTier"] ??= GetNodeString(project["projectTier"]);
                audit["turn"] ??= currentTurn;
                var title = reasonType == "rival_defense"
                    ? $"Проект «{projectName}» получил защитную подпитку"
                    : $"Проект «{projectName}» получил помощь";
                var summary = reasonType == "rival_defense"
                    ? $"Защитные действия вокруг проекта «{projectName}» изменили силу Обители на +{delta}."
                    : $"Помощь проекту «{projectName}» изменила силу Обители на +{delta}.";
                yield return GuardianPowerEventState.BuildEvent(
                    $"guardian_project_update_assist_{guardianId}_{projectId}_{currentTurn}_{Guid.NewGuid():N}",
                    guardianId,
                    delta,
                    reasonType,
                    "guardianProjectUpdates",
                    projectId,
                    title,
                    summary,
                    audit);
            }
        }

        if (command["sabotageAudit"] is JsonObject sabotageAudit)
        {
            var classification = GetNodeString(sabotageAudit["classification"]);
            var delta = AbodePowerRules.ResolveGuardianProjectSabotagePowerDelta(classification);
            if (delta != 0)
            {
                var audit = CloneObject(sabotageAudit);
                audit["projectId"] ??= projectId;
                audit["projectName"] ??= projectName;
                audit["projectType"] ??= GetNodeString(project["projectType"]);
                audit["projectTier"] ??= GetNodeString(project["projectTier"]);
                audit["turn"] ??= currentTurn;
                var relatedGuardianId = GetNodeString(command["relatedGuardianId"]);
                yield return GuardianPowerEventState.BuildEvent(
                    $"guardian_project_update_sabotage_{guardianId}_{projectId}_{currentTurn}_{Guid.NewGuid():N}",
                    guardianId,
                    delta,
                    "rival_strike",
                    "guardianProjectUpdates",
                    projectId,
                    $"Проект «{projectName}» подвергся саботажу",
                    $"Саботаж вокруг проекта «{projectName}» изменил силу Обители на {delta}.",
                    audit,
                    string.IsNullOrWhiteSpace(relatedGuardianId) ? null : relatedGuardianId);
            }
        }
    }

    private static string ResolveGuardianProjectAssistReasonType(JsonObject assistAudit)
    {
        var auditKind = GetNodeString(assistAudit["auditKind"]);
        if (string.Equals(auditKind, "defense", StringComparison.OrdinalIgnoreCase))
            return "rival_defense";

        var classification = GetNodeString(assistAudit["classification"]) ?? string.Empty;
        return classification.Contains("defensive", StringComparison.OrdinalIgnoreCase) ||
               classification.Contains("protection", StringComparison.OrdinalIgnoreCase)
            ? "rival_defense"
            : "project_assist";
    }

    private static void ApplyGuardianProjectCompletionCommands(
        List<JsonObject> activeProjects,
        List<JsonObject> completedProjects,
        List<JsonObject> temporaryModifiers,
        JsonArray commands,
        List<JsonObject> journalEntries,
        List<JsonObject> powerEvents,
        int currentTurn,
        int currentIncarnation,
        string? currentRealm,
        JsonObject? guardiansRoot,
        ref bool guardiansChanged)
    {
        foreach (var command in commands.OfType<JsonObject>())
        {
            var guardianId = GetNodeString(command["guardianId"]);
            var projectId = GetNodeString(command["projectId"]);
            if (string.IsNullOrWhiteSpace(guardianId) || string.IsNullOrWhiteSpace(projectId))
                continue;

            var existing = activeProjects.FirstOrDefault(item =>
                string.Equals(GuardianProjectState.GetGuardianId(item), guardianId, StringComparison.OrdinalIgnoreCase) &&
                string.Equals(GuardianProjectState.GetProjectId(item), projectId, StringComparison.OrdinalIgnoreCase));

            var completedEntry = existing != null ? CloneObject(existing) : new JsonObject
            {
                ["guardianId"] = guardianId,
                ["project"] = new JsonObject
                {
                    ["projectId"] = projectId
                }
            };

            var project = completedEntry["project"] as JsonObject ?? new JsonObject();
            var previousProject = CloneObject(project);
            var finalState = GetNodeString(command["finalState"]);
            project["projectId"] = projectId;
            if (!string.IsNullOrWhiteSpace(GetNodeString(command["projectName"])))
                project["projectName"] = GetNodeString(command["projectName"]);
            if (!string.IsNullOrWhiteSpace(GetNodeString(command["outcome"])))
                project["outcome"] = GetNodeString(command["outcome"]);
            if (!string.IsNullOrWhiteSpace(finalState))
                project["finalState"] = finalState;
            project["completionTurn"] = currentTurn;
            if (command["abodePowerDelta"] != null)
                project["abodePowerDelta"] = command["abodePowerDelta"]?.DeepClone();
            else
                project["abodePowerDelta"] = GuardianProjectState.GetDefaultTerminalAbodePowerDelta(GetNodeString(project["projectType"]), finalState, GetNodeString(project["projectTier"]));
            if (command["targetGuardianId"] != null)
                project["targetGuardianId"] = command["targetGuardianId"]?.DeepClone();
            if (command["offensiveImpactAudit"] != null)
                project["offensiveImpactAudit"] = command["offensiveImpactAudit"]?.DeepClone();
            var projectType = GetNodeString(project["projectType"]) ?? string.Empty;
            var projectTier = GetNodeString(project["projectTier"]) ?? string.Empty;
            var normalizedOutcomeAudit = BuildDefaultGuardianProjectOutcomeAudit(
                projectType,
                finalState,
                projectTier,
                command["projectOutcomeAudit"] as JsonObject);
            if (normalizedOutcomeAudit != null)
                project["projectOutcomeAudit"] = normalizedOutcomeAudit;
            GuardianProjectState.EnsureRecipeEffectState(project, currentIncarnation, currentRealm);
            if (command["pressureAudit"] != null)
                project["pressureAudit"] = command["pressureAudit"]?.DeepClone();
            if (command["stabilityAudit"] != null)
                project["stabilityAudit"] = command["stabilityAudit"]?.DeepClone();
            if (command["workAudit"] != null)
                project["workAudit"] = command["workAudit"]?.DeepClone();
            project.Remove("activeState");

            if (string.Equals(projectType, "offensive_intrigue", StringComparison.OrdinalIgnoreCase) &&
                string.Equals(finalState, "Completed", StringComparison.OrdinalIgnoreCase))
            {
                var politicalTrackerRoot = BuildGuardianProjectsTrackerRoot(activeProjects, completedProjects, temporaryModifiers);
                var normalizedOffensiveAudit = NormalizeOffensiveImpactAudit(
                    command["offensiveImpactAudit"] as JsonObject,
                    politicalTrackerRoot,
                    guardiansRoot,
                    guardianId!,
                    targetGuardianId: GetNodeString(command["targetGuardianId"]),
                    projectTier);
                if (normalizedOffensiveAudit != null)
                    project["offensiveImpactAudit"] = normalizedOffensiveAudit;
            }

            if (string.Equals(projectType, "counter_rival_operation", StringComparison.OrdinalIgnoreCase) &&
                string.Equals(finalState, "Completed", StringComparison.OrdinalIgnoreCase))
            {
                var counterAudit = NormalizeCounterOperationImpactAudit(
                    command["projectOutcomeAudit"] as JsonObject,
                    projectTier);
                project["projectOutcomeAudit"] = counterAudit;
            }

            var systemEffectAudit = string.Equals(projectType, "offensive_intrigue", StringComparison.OrdinalIgnoreCase)
                ? project["offensiveImpactAudit"] as JsonObject
                : project["projectOutcomeAudit"] as JsonObject;
            project["systemEffectSummary"] = GuardianProjectState.BuildSystemEffectSummary(
                projectType,
                finalState ?? string.Empty,
                projectTier,
                systemEffectAudit);

            NormalizeGuardianProjectObject(project, active: false);
            completedEntry["project"] = project;
            if (existing != null)
                activeProjects.Remove(existing);
            UpsertGuardianProjectEntry(completedProjects, completedEntry);

            var abodePowerDelta = GetNodeInt(project["abodePowerDelta"]);
            if (abodePowerDelta != 0)
            {
                powerEvents.Add(BuildGuardianProjectPowerEvent(
                    guardianId!,
                    projectId!,
                    project,
                    abodePowerDelta,
                    ResolveGuardianProjectPowerReasonType(GetNodeString(project["projectType"]), finalState, defensive: false)));
            }

            var targetGuardianId = GetNodeString(command["targetGuardianId"]);
            if (!string.IsNullOrWhiteSpace(targetGuardianId) &&
                project["offensiveImpactAudit"] is JsonObject offensiveAudit)
            {
                var targetLoss = GetNodeInt(offensiveAudit["targetLoss"]);
                if (targetLoss > 0)
                {
                    powerEvents.Add(BuildGuardianProjectPowerEvent(
                        targetGuardianId!,
                        projectId!,
                        project,
                        -targetLoss,
                        "rival_strike",
                        relatedGuardianId: guardianId!,
                        auditOverride: CloneObject(offensiveAudit)));
                }
            }

            ApplyPoliticalProjectSideEffects(
                activeProjects,
                journalEntries,
                currentTurn,
                guardianId!,
                projectId!,
                project,
                finalState,
                targetGuardianId);

            ApplyGuardianProjectTerminalModifiers(temporaryModifiers, guardianId!, projectId!, project, finalState);
            guardiansChanged = ApplyGuardianProjectRecipeSideEffects(guardiansRoot, guardianId!, projectId!, project) || guardiansChanged;

            journalEntries.Add(BuildGuardianProjectJournalEntry(
                currentTurn,
                guardianId,
                projectId,
                ResolveGuardianProjectCompletionEventType(finalState),
                project,
                BuildGuardianProjectCompletionTitle(project, finalState),
                    BuildGuardianProjectCompletionSummary(project, finalState, abodePowerDelta),
                    previousProject));
        }
    }

    private static JsonObject BuildGuardianProjectsTrackerRoot(
        IReadOnlyList<JsonObject> activeProjects,
        IReadOnlyList<JsonObject> completedProjects,
        IReadOnlyList<JsonObject> temporaryModifiers)
    {
        var root = new JsonObject
        {
            ["activeProjects"] = new JsonArray(),
            ["completedProjects"] = new JsonArray(),
            ["temporaryProjectModifiers"] = new JsonArray()
        };

        var activeArray = (JsonArray)root["activeProjects"]!;
        foreach (var entry in activeProjects)
            activeArray.Add(entry.DeepClone());

        var completedArray = (JsonArray)root["completedProjects"]!;
        foreach (var entry in completedProjects)
            completedArray.Add(entry.DeepClone());

        var modifierArray = (JsonArray)root["temporaryProjectModifiers"]!;
        foreach (var modifier in temporaryModifiers)
            modifierArray.Add(modifier.DeepClone());

        return root;
    }

    private static JsonObject? NormalizeOffensiveImpactAudit(
        JsonObject? rawAudit,
        JsonObject trackerRoot,
        JsonObject? guardiansRoot,
        string attackerGuardianId,
        string? targetGuardianId,
        string projectTier)
    {
        if (guardiansRoot == null || string.IsNullOrWhiteSpace(targetGuardianId))
            return rawAudit == null ? null : CloneObject(rawAudit);

        var attackerPower = TryReadGuardianCurrentPower(guardiansRoot, attackerGuardianId);
        var targetPower = TryReadGuardianCurrentPower(guardiansRoot, targetGuardianId!);
        if (!attackerPower.HasValue || !targetPower.HasValue)
            return rawAudit == null ? null : CloneObject(rawAudit);

        var playerDefenseBonus = Math.Clamp(GetNodeInt(rawAudit?["playerDefenseBonus"]), 0, 2);
        var result = GuardianProjectState.ResolveOffensiveImpact(
            trackerRoot,
            attackerGuardianId,
            targetGuardianId!,
            projectTier,
            attackerPower.Value,
            targetPower.Value,
            playerDefenseBonus);

        var audit = rawAudit != null ? CloneObject(rawAudit) : new JsonObject();
        audit["attackerCurrentPower"] = attackerPower.Value;
        audit["targetCurrentPower"] = targetPower.Value;
        audit["baseLoss"] = result.BaseLoss;
        audit["attackerBonus"] = result.AttackerBonus;
        audit["baseTargetShield"] = result.BaseTargetShield;
        audit["fortificationBonus"] = result.FortificationBonus;
        audit["counterOperationBonus"] = result.CounterOperationBonus;
        audit["playerDefenseBonus"] = result.PlayerDefenseBonus;
        audit["targetShield"] = result.TargetShield;
        audit["targetLoss"] = result.TargetLoss;
        audit["pressureDelta"] = result.PressureDelta;
        audit["stabilityDamage"] = result.StabilityDamage;
        return audit;
    }

    private static JsonObject NormalizeCounterOperationImpactAudit(JsonObject? rawAudit, string projectTier)
    {
        var audit = rawAudit != null ? CloneObject(rawAudit) : new JsonObject();
        audit["pressureRelief"] = GuardianProjectState.GetCounterOperationPressureRelief(projectTier);
        audit["stabilityRelief"] = GuardianProjectState.GetCounterOperationStabilityRelief(projectTier);
        audit["abodePowerGain"] = GuardianProjectState.GetCounterOperationAbodePowerGain(projectTier);
        return audit;
    }

    private static int? TryReadGuardianCurrentPower(JsonObject guardiansRoot, string guardianId)
    {
        if (guardiansRoot["guardians"] is not JsonArray guardians)
            return null;

        var guardian = guardians
            .OfType<JsonObject>()
            .FirstOrDefault(item => string.Equals(GetNodeString(item["guardianId"]), guardianId, StringComparison.OrdinalIgnoreCase));
        return guardian == null ? null : AbodePowerRules.GetCurrentPower(guardian);
    }

    private static void ApplyPoliticalProjectSideEffects(
        List<JsonObject> activeProjects,
        List<JsonObject> journalEntries,
        int currentTurn,
        string sourceGuardianId,
        string sourceProjectId,
        JsonObject sourceProject,
        string? finalState,
        string? targetGuardianId)
    {
        if (string.IsNullOrWhiteSpace(targetGuardianId))
            return;

        var projectType = GetNodeString(sourceProject["projectType"]);
        if (string.Equals(projectType, "offensive_intrigue", StringComparison.OrdinalIgnoreCase) &&
            string.Equals(finalState, "Completed", StringComparison.OrdinalIgnoreCase) &&
            sourceProject["offensiveImpactAudit"] is JsonObject offensiveAudit)
        {
            ApplyOffensiveProjectImpactToTargetActiveProject(
                activeProjects,
                journalEntries,
                currentTurn,
                sourceGuardianId,
                sourceProjectId,
                sourceProject,
                targetGuardianId!,
                offensiveAudit);
            return;
        }

        if (string.Equals(projectType, "counter_rival_operation", StringComparison.OrdinalIgnoreCase) &&
            string.Equals(finalState, "Completed", StringComparison.OrdinalIgnoreCase) &&
            sourceProject["projectOutcomeAudit"] is JsonObject counterAudit)
        {
            ApplyCounterOperationReliefToTargetActiveProject(
                activeProjects,
                journalEntries,
                currentTurn,
                sourceGuardianId,
                sourceProjectId,
                sourceProject,
                targetGuardianId!,
                counterAudit);
        }
    }

    private static void ApplyOffensiveProjectImpactToTargetActiveProject(
        List<JsonObject> activeProjects,
        List<JsonObject> journalEntries,
        int currentTurn,
        string sourceGuardianId,
        string sourceProjectId,
        JsonObject sourceProject,
        string targetGuardianId,
        JsonObject offensiveAudit)
    {
        var targetEntry = activeProjects.FirstOrDefault(item =>
            string.Equals(GuardianProjectState.GetGuardianId(item), targetGuardianId, StringComparison.OrdinalIgnoreCase));
        if (targetEntry?["project"] is not JsonObject targetProject)
            return;

        var previousProject = CloneObject(targetProject);
        var pressureDelta = Math.Max(0, GetNodeInt(offensiveAudit["pressureDelta"]));
        var stabilityDamage = Math.Max(0, GetNodeInt(offensiveAudit["stabilityDamage"]));
        if (pressureDelta <= 0 && stabilityDamage <= 0)
            return;

        targetProject["pressure"] = Math.Max(0, GetNodeInt(targetProject["pressure"]) + pressureDelta);
        targetProject["stability"] = Math.Max(0, GetNodeInt(targetProject["stability"]) - stabilityDamage);
        targetProject["pressureAudit"] = new JsonObject
        {
            ["reason"] = "completed_offensive_intrigue",
            ["sourceGuardianId"] = sourceGuardianId,
            ["sourceProjectId"] = sourceProjectId,
            ["appliedDelta"] = pressureDelta
        };
        targetProject["stabilityAudit"] = new JsonObject
        {
            ["reason"] = "completed_offensive_intrigue",
            ["sourceGuardianId"] = sourceGuardianId,
            ["sourceProjectId"] = sourceProjectId,
            ["appliedDelta"] = -stabilityDamage
        };

        var targetProjectId = GetNodeString(targetProject["projectId"]) ?? string.Empty;
        journalEntries.Add(BuildGuardianProjectJournalEntry(
            currentTurn,
            targetGuardianId,
            targetProjectId,
            "pressured",
            targetProject,
            $"На проект «{GetNodeString(targetProject["projectName"])}» обрушилась rival-интрига",
            $"Completed offensive_intrigue добавил Pressure +{pressureDelta} и Stability -{stabilityDamage}.",
            previousProject));
    }

    private static void ApplyCounterOperationReliefToTargetActiveProject(
        List<JsonObject> activeProjects,
        List<JsonObject> journalEntries,
        int currentTurn,
        string sourceGuardianId,
        string sourceProjectId,
        JsonObject sourceProject,
        string targetGuardianId,
        JsonObject counterAudit)
    {
        var targetEntry = activeProjects.FirstOrDefault(item =>
            string.Equals(GuardianProjectState.GetGuardianId(item), targetGuardianId, StringComparison.OrdinalIgnoreCase));
        if (targetEntry?["project"] is not JsonObject targetProject)
            return;

        var previousProject = CloneObject(targetProject);
        var pressureRelief = Math.Max(0, GetNodeInt(counterAudit["pressureRelief"]));
        var stabilityRelief = Math.Max(0, GetNodeInt(counterAudit["stabilityRelief"]));
        if (pressureRelief <= 0 && stabilityRelief <= 0)
            return;

        var currentPressure = GetNodeInt(targetProject["pressure"]);
        var currentStability = GetNodeInt(targetProject["stability"]);
        var appliedPressureRelief = Math.Min(currentPressure, pressureRelief);
        var appliedStabilityRelief = stabilityRelief;

        targetProject["pressure"] = Math.Max(0, currentPressure - appliedPressureRelief);
        targetProject["stability"] = Math.Min(100, currentStability + appliedStabilityRelief);
        targetProject["pressureAudit"] = new JsonObject
        {
            ["reason"] = "completed_counter_rival_operation",
            ["sourceGuardianId"] = sourceGuardianId,
            ["sourceProjectId"] = sourceProjectId,
            ["appliedDelta"] = -appliedPressureRelief
        };
        targetProject["stabilityAudit"] = new JsonObject
        {
            ["reason"] = "completed_counter_rival_operation",
            ["sourceGuardianId"] = sourceGuardianId,
            ["sourceProjectId"] = sourceProjectId,
            ["appliedDelta"] = appliedStabilityRelief
        };

        var targetProjectId = GetNodeString(targetProject["projectId"]) ?? string.Empty;
        journalEntries.Add(BuildGuardianProjectJournalEntry(
            currentTurn,
            targetGuardianId,
            targetProjectId,
            "stabilized",
            targetProject,
            $"Rival-проект «{GetNodeString(targetProject["projectName"])}» получил контр-удар",
            $"Completed counter_rival_operation снял с rival-проекта Pressure {appliedPressureRelief} и изменил Stability на +{appliedStabilityRelief}.",
            previousProject));
    }

    private static void NormalizeGuardianProjectObject(JsonObject project, bool active)
    {
        if (string.IsNullOrWhiteSpace(GetNodeString(project["projectName"])) &&
            !string.IsNullOrWhiteSpace(GetNodeString(project["name"])))
        {
            project["projectName"] = GetNodeString(project["name"]);
        }

        if (string.IsNullOrWhiteSpace(GetNodeString(project["name"])) &&
            !string.IsNullOrWhiteSpace(GetNodeString(project["projectName"])))
        {
            project["name"] = GetNodeString(project["projectName"]);
        }

        if (active)
        {
            project["totalWork"] ??= 0;
            project["workDone"] ??= 0;
            project["totalStages"] ??= 1;
            project["currentStage"] ??= 0;
            project["pressure"] ??= 0;
            project["stability"] ??= 100;
        }
    }

    private static void ApplyGuardianProjectStartModifiers(List<JsonObject> temporaryModifiers, string guardianId, JsonObject project)
    {
        var projectMode = GetNodeString(project["projectMode"]);
        if (!string.Equals(projectMode, "internal", StringComparison.OrdinalIgnoreCase))
            return;

        var appliedModifiers = new List<JsonObject>();
        foreach (var modifier in temporaryModifiers.ToList())
        {
            if (!string.Equals(GetNodeString(modifier["guardianId"]), guardianId, StringComparison.OrdinalIgnoreCase) ||
                !string.Equals(GetNodeString(modifier["modifierType"]), "next_internal_project_starting_pressure", StringComparison.OrdinalIgnoreCase) ||
                GetNodeInt(modifier["remainingApplications"]) <= 0)
            {
                continue;
            }

            project["pressure"] = GetNodeInt(project["pressure"]) + GetNodeInt(modifier["value"]);
            project["pressureAudit"] = new JsonObject
            {
                ["modifierId"] = GetNodeString(modifier["modifierId"]),
                ["reason"] = "temporary_project_modifier",
                ["appliedDelta"] = GetNodeInt(modifier["value"]),
                ["sourceProjectId"] = GetNodeString(modifier["sourceProjectId"])
            };
            modifier["remainingApplications"] = Math.Max(0, GetNodeInt(modifier["remainingApplications"]) - 1);
            appliedModifiers.Add(modifier);
        }

        foreach (var applied in appliedModifiers)
        {
            if (GetNodeInt(applied["remainingApplications"]) <= 0)
                temporaryModifiers.Remove(applied);
        }
    }

    private static void ApplyGuardianProjectTerminalModifiers(
        List<JsonObject> temporaryModifiers,
        string guardianId,
        string projectId,
        JsonObject project,
        string? finalState)
    {
        if (!string.Equals(GetNodeString(project["projectType"]), "abode_fortification", StringComparison.OrdinalIgnoreCase) ||
            !string.Equals(finalState, "Sabotaged", StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        temporaryModifiers.Add(new JsonObject
        {
            ["guardianId"] = guardianId,
            ["modifierId"] = $"tmp_{guardianId}_{projectId}_next_internal_pressure",
            ["modifierType"] = "next_internal_project_starting_pressure",
            ["value"] = 10,
            ["remainingApplications"] = 1,
            ["sourceProjectId"] = projectId
        });
    }

    private static bool HasGuardianProjectVisibleChange(JsonObject previousProject, JsonObject currentProject)
    {
        return GetNodeString(previousProject["activeState"]) != GetNodeString(currentProject["activeState"]) ||
               GetNodeInt(previousProject["workDone"]) != GetNodeInt(currentProject["workDone"]) ||
               GetNodeInt(previousProject["currentStage"]) != GetNodeInt(currentProject["currentStage"]) ||
               GetNodeInt(previousProject["pressure"]) != GetNodeInt(currentProject["pressure"]) ||
               GetNodeInt(previousProject["stability"]) != GetNodeInt(currentProject["stability"]);
    }

    private static string ResolveGuardianProjectUpdateEventType(JsonObject previousProject, JsonObject currentProject)
    {
        if (GetNodeInt(currentProject["stability"]) > GetNodeInt(previousProject["stability"]))
            return "stabilized";
        if (GetNodeInt(currentProject["pressure"]) > GetNodeInt(previousProject["pressure"]) ||
            GetNodeInt(currentProject["stability"]) < GetNodeInt(previousProject["stability"]))
        {
            return "pressured";
        }

        return "progressed";
    }

    private static string ResolveGuardianProjectCompletionEventType(string? finalState) =>
        finalState switch
        {
            "Completed" => "completed",
            "Abandoned" => "abandoned",
            "Sabotaged" => "sabotaged",
            "Collapsed" => "collapsed",
            _ => "completed"
        };

    private static JsonObject BuildGuardianProjectJournalEntry(
        int currentTurn,
        string guardianId,
        string projectId,
        string eventType,
        JsonObject currentProject,
        string title,
        string summary,
        JsonObject? previousProject)
    {
        var details = new JsonArray();
        var currentState = GetNodeString(currentProject["activeState"]);
        var currentName = GetNodeString(currentProject["projectName"]) ?? projectId;
        details.Add($"Проект: {currentName}");
        if (!string.IsNullOrWhiteSpace(currentState))
            details.Add($"Состояние: {currentState}");
        AppendGuardianProjectDiffDetail(details, "Стадия", previousProject, currentProject, "currentStage");
        AppendGuardianProjectDiffDetail(details, "Работа", previousProject, currentProject, "workDone");
        AppendGuardianProjectDiffDetail(details, "Pressure", previousProject, currentProject, "pressure");
        AppendGuardianProjectDiffDetail(details, "Stability", previousProject, currentProject, "stability");
        if (currentProject["systemEffectSummary"] is JsonArray effectSummary)
        {
            foreach (var effect in effectSummary.OfType<JsonValue>())
            {
                if (effect.TryGetValue<string>(out var text) && !string.IsNullOrWhiteSpace(text))
                    details.Add($"Эффект: {text}");
            }
        }

        return new JsonObject
        {
            ["entryId"] = $"gpj_{guardianId}_{projectId}_{currentTurn}_{eventType}_{Guid.NewGuid():N}",
            ["turn"] = currentTurn,
            ["guardianId"] = guardianId,
            ["projectId"] = projectId,
            ["eventType"] = eventType,
            ["visibility"] = "player_known",
            ["title"] = title,
            ["summary"] = summary,
            ["details"] = details
        };
    }

    private static void AppendGuardianProjectDiffDetail(JsonArray details, string label, JsonObject? previousProject, JsonObject currentProject, string fieldName)
    {
        var currentValue = GetNodeInt(currentProject[fieldName]);
        if (previousProject == null)
        {
            details.Add($"{label}: {currentValue}");
            return;
        }

        var previousValue = GetNodeInt(previousProject[fieldName]);
        if (previousValue == currentValue)
            return;

        details.Add($"{label}: {previousValue} -> {currentValue}");
    }

    private static string BuildGuardianProjectUpdateTitle(JsonObject previousProject, JsonObject currentProject)
    {
        var projectName = GetNodeString(currentProject["projectName"]) ?? GetNodeString(currentProject["name"]) ?? "Проект";
        if (GetNodeInt(currentProject["stability"]) > GetNodeInt(previousProject["stability"]))
            return $"Проект «{projectName}» стабилизирован";
        if (GetNodeInt(currentProject["pressure"]) > GetNodeInt(previousProject["pressure"]) ||
            GetNodeInt(currentProject["stability"]) < GetNodeInt(previousProject["stability"]))
        {
            return $"Проект «{projectName}» испытывает давление";
        }

        return $"Проект «{projectName}» продвинулся";
    }

    private static string BuildGuardianProjectUpdateSummary(JsonObject previousProject, JsonObject currentProject)
    {
        if (GetNodeInt(currentProject["stability"]) > GetNodeInt(previousProject["stability"]))
            return "Устойчивость проекта выросла после защитных или стабилизирующих действий.";
        if (GetNodeInt(currentProject["pressure"]) > GetNodeInt(previousProject["pressure"]) ||
            GetNodeInt(currentProject["stability"]) < GetNodeInt(previousProject["stability"]))
        {
            return "На проект усилилось внешнее давление, и это отразилось на его устойчивости.";
        }

        return "Хранитель продвинул проект вперёд в текущем afterlife-цикле.";
    }

    private static string BuildGuardianProjectCompletionTitle(JsonObject project, string? finalState)
    {
        var projectName = GetNodeString(project["projectName"]) ?? GetNodeString(project["name"]) ?? "Проект";
        return finalState switch
        {
            "Completed" => $"Проект «{projectName}» завершён",
            "Abandoned" => $"Проект «{projectName}» оставлен",
            "Sabotaged" => $"Проект «{projectName}» сорван",
            "Collapsed" => $"Проект «{projectName}» рухнул",
            _ => $"Проект «{projectName}» завершил цикл"
        };
    }

    private static string BuildGuardianProjectCompletionSummary(JsonObject project, string? finalState, int abodePowerDelta)
    {
        var outcome = GetNodeString(project["outcome"]);
        var deltaTag = abodePowerDelta == 0
            ? ""
            : abodePowerDelta > 0
                ? $" Сила Обители: +{abodePowerDelta}."
                : $" Сила Обители: {abodePowerDelta}.";

        var prefix = finalState switch
        {
            "Completed" => "Проект доведён до конца.",
            "Abandoned" => "Хранитель отказался от проекта.",
            "Sabotaged" => "Проект был сорван внешним ударом или осознанным саботажем.",
            "Collapsed" => "Проект не выдержал накопленного давления и обрушился.",
            _ => "Проект перешёл в terminal state."
        };

        return string.IsNullOrWhiteSpace(outcome)
            ? $"{prefix}{deltaTag}".Trim()
            : $"{prefix} {outcome}{deltaTag}".Trim();
    }

    private static bool ApplyGuardianProjectRecipeSideEffects(JsonObject? guardiansRoot, string guardianId, string projectId, JsonObject project)
    {
        if (guardiansRoot == null)
            return false;

        var projectType = GetNodeString(project["projectType"]);
        var finalState = GetNodeString(project["finalState"]);
        if (!string.Equals(projectType, "lore_research", StringComparison.OrdinalIgnoreCase) ||
            !string.Equals(finalState, "Completed", StringComparison.OrdinalIgnoreCase) ||
            project["projectOutcomeAudit"] is not JsonObject projectOutcomeAudit ||
            projectOutcomeAudit["unlockedLoreFragments"] is not JsonArray unlockedLoreFragments ||
            guardiansRoot["guardians"] is not JsonArray guardians)
        {
            return false;
        }

        var guardian = guardians
            .OfType<JsonObject>()
            .FirstOrDefault(item => string.Equals(GetNodeString(item["guardianId"]), guardianId, StringComparison.OrdinalIgnoreCase));
        if (guardian == null)
            return false;

        var loreFragments = EnsureArray(guardian, "loreFragments");
        foreach (var fragment in unlockedLoreFragments.OfType<JsonObject>())
        {
            var clone = CloneObject(fragment);
            clone["isUnlocked"] = true;
            clone["unlockedByProjectId"] = projectId;
            clone["unlockSource"] = "lore_research";
            UpsertByIdentity(loreFragments, clone, "fragmentId", "title");
        }

        SyncActiveGuardian(guardiansRoot, guardianId, guardian);
        return true;
    }

    private static JsonObject? BuildDefaultGuardianProjectOutcomeAudit(string projectType, string? finalState, string projectTier, JsonObject? explicitAudit)
    {
        if (explicitAudit != null)
            return CloneObject(explicitAudit);

        if (string.Equals(projectType, "abode_fortification", StringComparison.OrdinalIgnoreCase) &&
            string.Equals(finalState, "Completed", StringComparison.OrdinalIgnoreCase))
        {
            return new JsonObject
            {
                ["safePressureBonus"] = GuardianProjectState.GetDefaultFortificationSafePressureBonus(projectTier),
                ["defenseRatingBonus"] = GuardianProjectState.GetDefaultFortificationDefenseRatingBonus(projectTier)
            };
        }

        if (string.Equals(projectType, "relic_forging", StringComparison.OrdinalIgnoreCase) &&
            string.Equals(finalState, "Completed", StringComparison.OrdinalIgnoreCase))
        {
            return new JsonObject
            {
                ["upgradedTradeSlots"] = GuardianProjectState.GetDefaultRelicForgingUpgradedTradeSlots(projectTier),
                ["elevatedTradeSlots"] = GuardianProjectState.GetDefaultRelicForgingElevatedTradeSlots(projectTier),
                ["guardianRarityCeilingBonusSteps"] = GuardianProjectState.GetDefaultRelicForgingRarityBonusSteps(projectTier)
            };
        }

        if (string.Equals(projectType, "soul_preparation", StringComparison.OrdinalIgnoreCase) &&
            string.Equals(finalState, "Completed", StringComparison.OrdinalIgnoreCase))
        {
            return new JsonObject
            {
                ["preparationBudgetPoints"] = GuardianProjectState.GetDefaultSoulPreparationBudgetPoints(projectTier),
                ["preparationClaimPriorityBonus"] = GuardianProjectState.GetDefaultSoulPreparationClaimPriorityBonus(projectTier)
            };
        }

        if (string.Equals(projectType, "soul_preparation", StringComparison.OrdinalIgnoreCase) &&
            string.Equals(finalState, "Sabotaged", StringComparison.OrdinalIgnoreCase))
        {
            return new JsonObject
            {
                ["hostilePriorityTokensGranted"] = GuardianProjectState.GetDefaultSoulPreparationHostilePriorityTokens(projectTier)
            };
        }

        if (string.Equals(projectType, "counter_rival_operation", StringComparison.OrdinalIgnoreCase) &&
            string.Equals(finalState, "Completed", StringComparison.OrdinalIgnoreCase))
        {
            return new JsonObject
            {
                ["pressureRelief"] = GuardianProjectState.GetCounterOperationPressureRelief(projectTier),
                ["stabilityRelief"] = GuardianProjectState.GetCounterOperationStabilityRelief(projectTier),
                ["abodePowerGain"] = GuardianProjectState.GetCounterOperationAbodePowerGain(projectTier)
            };
        }

        return null;
    }

    private static void ConsumeLoreResearchQuestTokens(
        List<JsonObject> completedProjects,
        JsonObject? previousGuardiansRoot,
        JsonObject currentGuardiansRoot,
        int currentIncarnation,
        List<JsonObject> journalEntries)
    {
        var previouslyKnownQuestIds = CollectGuardianQuestIds(previousGuardiansRoot);
        if (currentGuardiansRoot["guardians"] is not JsonArray guardians)
            return;

        foreach (var guardian in guardians.OfType<JsonObject>())
        {
            var guardianId = GetNodeString(guardian["guardianId"]);
            if (string.IsNullOrWhiteSpace(guardianId) ||
                guardian["questManagement"] is not JsonObject questManagement)
            {
                continue;
            }

            foreach (var arrayName in new[] { "availableQuests", "activeQuests", "completedQuests" })
            {
                if (questManagement[arrayName] is not JsonArray quests)
                    continue;

                foreach (var quest in quests.OfType<JsonObject>())
                {
                    var questId = GetNodeString(quest["questId"]);
                    if (string.IsNullOrWhiteSpace(questId) ||
                        previouslyKnownQuestIds.Contains(GuardianProjectState.BuildKey(guardianId!, questId!)))
                    {
                        continue;
                    }

                    var questOrigin = GetNodeString(quest["questOrigin"]);
                    var sourceProjectId = GetNodeString(quest["sourceProjectId"]);
                    if (!string.Equals(questOrigin, GuardianProjectState.LoreResearchHookOrigin, StringComparison.OrdinalIgnoreCase) &&
                        !string.Equals(questOrigin, GuardianProjectState.LoreResearchSpecialLineOrigin, StringComparison.OrdinalIgnoreCase) &&
                        !string.Equals(questOrigin, GuardianProjectState.ArchiveConsultationHookOrigin, StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }

                    if (string.IsNullOrWhiteSpace(sourceProjectId))
                        continue;

                    if (GuardianProjectState.TryConsumeLoreQuestToken(completedProjects, guardianId!, sourceProjectId!, questOrigin!, currentIncarnation))
                    {
                        var archiveConsultation = string.Equals(questOrigin, GuardianProjectState.ArchiveConsultationHookOrigin, StringComparison.OrdinalIgnoreCase);
                        journalEntries.Add(new JsonObject
                        {
                            ["entryId"] = $"gpj_{guardianId}_{sourceProjectId}_{questId}_token_{Guid.NewGuid():N}",
                            ["turn"] = currentIncarnation,
                            ["guardianId"] = guardianId,
                            ["projectId"] = sourceProjectId,
                            ["eventType"] = "assisted",
                            ["visibility"] = "player_known",
                            ["title"] = archiveConsultation ? "Реализована архивная гарантия квеста" : "Израсходован исследовательский token",
                            ["summary"] = archiveConsultation
                                ? "Архивная консультация гарантированно породила новый квест Хранителя."
                                : $"Проект lore_research открыл новый {(string.Equals(questOrigin, GuardianProjectState.LoreResearchSpecialLineOrigin, StringComparison.OrdinalIgnoreCase) ? "special-line" : "hook")} квест.",
                            ["details"] = new JsonArray
                            {
                                $"QuestId: {questId}",
                                $"Quest origin: {questOrigin}"
                            }
                        });
                    }
                }
            }
        }
    }

    private static void ConsumeRelicForgingGachaUses(
        List<JsonObject> completedProjects,
        JsonObject? previousGuardiansRoot,
        JsonObject currentGuardiansRoot,
        List<JsonObject> journalEntries)
    {
        var previousHistoryKeys = CollectGuardianGachaHistoryKeys(previousGuardiansRoot);
        if (currentGuardiansRoot["guardians"] is not JsonArray guardians)
            return;

        foreach (var guardian in guardians.OfType<JsonObject>())
        {
            var guardianId = GetNodeString(guardian["guardianId"]);
            if (string.IsNullOrWhiteSpace(guardianId) ||
                guardian["gachaSystem"] is not JsonObject gachaSystem ||
                gachaSystem["gachaHistory"] is not JsonArray history)
            {
                continue;
            }

            foreach (var historyEntry in history.OfType<JsonObject>())
            {
                var historyKey = GetNodeString(historyEntry["eventId"]);
                if (string.IsNullOrWhiteSpace(historyKey))
                    historyKey = $"{guardianId}::{GetNodeString(historyEntry["timestamp"])}::{GetNodeString(historyEntry["relicId"])}";
                if (previousHistoryKeys.Contains(historyKey))
                    continue;

                var gachaBonusAudit = historyEntry["gachaBonusAudit"] as JsonObject;
                var sourceProjectId = GetNodeString(gachaBonusAudit?["sourceProjectId"]);
                var forgingSteps = GetNodeInt(gachaBonusAudit?["relicForgingBonusSteps"]);
                if (forgingSteps <= 0)
                    continue;

                if (GuardianProjectState.TryConsumeRelicForgingGachaUse(completedProjects, guardianId!, sourceProjectId))
                {
                    journalEntries.Add(new JsonObject
                    {
                        ["entryId"] = $"gpj_{guardianId}_{sourceProjectId}_gacha_{Guid.NewGuid():N}",
                        ["turn"] = 0,
                        ["guardianId"] = guardianId,
                        ["projectId"] = sourceProjectId,
                        ["eventType"] = "completed",
                        ["visibility"] = "player_known",
                        ["title"] = "Израсходован forge gacha-bonus",
                        ["summary"] = $"Результат relic_forging усилил guardian-mediated гача на {forgingSteps} step(ов) редкости.",
                        ["details"] = new JsonArray
                        {
                            $"Rarity bonus from forge: +{forgingSteps}",
                            $"RelicId: {GetNodeString(historyEntry["relicId"])}"
                        }
                    });
                }
            }
        }
    }

    private static JsonObject BuildGuardianProjectPowerEvent(
        string guardianId,
        string projectId,
        JsonObject project,
        int delta,
        string reasonType,
        string? relatedGuardianId = null,
        JsonObject? auditOverride = null)
    {
        var projectName = GetNodeString(project["projectName"]) ?? GetNodeString(project["name"]) ?? projectId;
        var projectType = GetNodeString(project["projectType"]);
        var projectTier = GetNodeString(project["projectTier"]);
        var finalState = GetNodeString(project["finalState"]);
        var audit = auditOverride ?? new JsonObject
        {
            ["projectId"] = projectId,
            ["projectName"] = projectName,
            ["projectType"] = projectType,
            ["projectTier"] = projectTier,
            ["finalState"] = finalState
        };

        var title = delta >= 0
            ? $"Проект «{projectName}» изменил силу Обители"
            : $"Проект «{projectName}» ослабил силу Обители";
        var summary = delta >= 0
            ? $"Проект «{projectName}» завершил цикл с изменением силы Обители на +{delta}."
            : $"Проект «{projectName}» завершил цикл с изменением силы Обители на {delta}.";

        return GuardianPowerEventState.BuildEvent(
            $"gpe_{guardianId}_{projectId}_{reasonType}_{Guid.NewGuid():N}",
            guardianId,
            delta,
            reasonType,
            "completeGuardianProjects",
            projectId,
            title,
            summary,
            audit,
            relatedGuardianId);
    }

    private static string ResolveGuardianProjectPowerReasonType(string? projectType, string? finalState, bool defensive)
    {
        if (string.Equals(projectType, "counter_rival_operation", StringComparison.OrdinalIgnoreCase) || defensive)
            return "rival_defense";

        if (string.Equals(finalState, "Completed", StringComparison.OrdinalIgnoreCase))
            return "project_completion";

        return "project_failure";
    }

    private async Task AppendGuardianProjectJournalEntriesAsync(List<JsonObject> journalEntries)
    {
        var currentNode = await ReadNodeAsync(GuardianProjectState.JournalPath);
        var result = currentNode as JsonObject != null
            ? CloneObject((JsonObject)currentNode)
            : new JsonObject();
        var entries = EnsureArray(result, "entries");
        foreach (var entry in journalEntries)
            UpsertByIdentity(entries, entry, "entryId");

        if (currentNode is JsonObject currentObject)
            await WriteIfChangedAsync(GuardianProjectState.JournalPath, currentObject, result);
        else
            await _fs.WriteFileAtomicAsync(GuardianProjectState.JournalPath, result.ToJsonString(JsonOpts));
    }

    private async Task<int> TryReadCurrentTurnNumberAsync()
    {
        var raw = await _fs.ReadFileAsync("input/turn_request.json");
        if (string.IsNullOrWhiteSpace(raw))
            return 0;

        try
        {
            using var doc = JsonDocument.Parse(raw);
            if (doc.RootElement.ValueKind == JsonValueKind.Object &&
                doc.RootElement.TryGetProperty("turnNumber", out var turnNode) &&
                turnNode.ValueKind == JsonValueKind.Number &&
                turnNode.TryGetInt32(out var turn))
            {
                return turn;
            }
        }
        catch
        {
            // ignored
        }

        return 0;
    }

    private static HashSet<string> CollectGuardianQuestIds(JsonObject? guardiansRoot)
    {
        var result = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (guardiansRoot?["guardians"] is not JsonArray guardians)
            return result;

        foreach (var guardian in guardians.OfType<JsonObject>())
        {
            var guardianId = GetNodeString(guardian["guardianId"]);
            if (string.IsNullOrWhiteSpace(guardianId) ||
                guardian["questManagement"] is not JsonObject questManagement)
            {
                continue;
            }

            foreach (var arrayName in new[] { "availableQuests", "activeQuests", "completedQuests" })
            {
                if (questManagement[arrayName] is not JsonArray quests)
                    continue;

                foreach (var quest in quests.OfType<JsonObject>())
                {
                    var questId = GetNodeString(quest["questId"]);
                    if (!string.IsNullOrWhiteSpace(questId))
                        result.Add(GuardianProjectState.BuildKey(guardianId!, questId!));
                }
            }
        }

        return result;
    }

    private static HashSet<string> CollectGuardianGachaHistoryKeys(JsonObject? guardiansRoot)
    {
        var result = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (guardiansRoot?["guardians"] is not JsonArray guardians)
            return result;

        foreach (var guardian in guardians.OfType<JsonObject>())
        {
            var guardianId = GetNodeString(guardian["guardianId"]);
            if (string.IsNullOrWhiteSpace(guardianId) ||
                guardian["gachaSystem"] is not JsonObject gachaSystem ||
                gachaSystem["gachaHistory"] is not JsonArray history)
            {
                continue;
            }

            foreach (var entry in history.OfType<JsonObject>())
            {
                var key = GetNodeString(entry["eventId"]);
                if (string.IsNullOrWhiteSpace(key))
                    key = $"{guardianId}::{GetNodeString(entry["timestamp"])}::{GetNodeString(entry["relicId"])}";
                result.Add(key);
            }
        }

        return result;
    }
}

