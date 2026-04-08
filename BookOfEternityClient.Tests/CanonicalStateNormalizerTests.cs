using BookOfEternityClient.Core;
using BookOfEternityClient.Services;
using Microsoft.Extensions.Logging.Abstractions;
using System.Text.Json.Nodes;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed partial class CanonicalStateNormalizerTests : IDisposable
{
    private readonly string _rootPath;
    private readonly FileSystemManager _fs;

    public CanonicalStateNormalizerTests()
    {
        _rootPath = Path.Combine(Path.GetTempPath(), "boe-normalizer-tests-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_rootPath);

        _fs = new FileSystemManager(_rootPath, NullLogger<FileSystemManager>.Instance);
        _fs.EnsureDirectoryStructure();
    }

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(_rootPath))
                Directory.Delete(_rootPath, recursive: true);
        }
        catch
        {
            // Ignore temp cleanup failures.
        }
    }

    private async Task NormalizeAccumulatedStateWithTrackerBaselineAsync(
        CanonicalStateNormalizer normalizer,
        IReadOnlyDictionary<string, string>? additionalBackups = null)
    {
        var currentTrackerJson = await _fs.ReadFileAsync(GuardianProjectState.TrackerPath);
        if (string.IsNullOrWhiteSpace(currentTrackerJson))
        {
            await normalizer.NormalizeAccumulatedStateAsync(additionalBackups);
            return;
        }

        var currentTrackerRoot = JsonNode.Parse(currentTrackerJson) as JsonObject;
        if (currentTrackerRoot == null)
        {
            await normalizer.NormalizeAccumulatedStateAsync(additionalBackups);
            return;
        }

        var activeProjects = currentTrackerRoot["activeProjects"] as JsonArray;
        var completedProjects = currentTrackerRoot["completedProjects"] as JsonArray;
        var temporaryModifiers = currentTrackerRoot["temporaryProjectModifiers"] as JsonArray;
        var startCommands = currentTrackerRoot["startGuardianProjects"] as JsonArray;
        var updateCommands = currentTrackerRoot["guardianProjectUpdates"] as JsonArray;
        var completeCommands = currentTrackerRoot["completeGuardianProjects"] as JsonArray;

        if (activeProjects == null &&
            completedProjects == null &&
            temporaryModifiers == null &&
            startCommands == null &&
            updateCommands == null &&
            completeCommands == null)
        {
            await normalizer.NormalizeAccumulatedStateAsync(additionalBackups);
            return;
        }

        var baselineRoot = new JsonObject
        {
            ["activeProjects"] = activeProjects?.DeepClone() ?? new JsonArray(),
            ["completedProjects"] = completedProjects?.DeepClone() ?? new JsonArray(),
            ["temporaryProjectModifiers"] = temporaryModifiers?.DeepClone() ?? new JsonArray()
        };

        var commandRoot = new JsonObject();
        if (startCommands != null)
            commandRoot["startGuardianProjects"] = startCommands.DeepClone();
        if (updateCommands != null)
            commandRoot["guardianProjectUpdates"] = updateCommands.DeepClone();
        if (completeCommands != null)
            commandRoot["completeGuardianProjects"] = completeCommands.DeepClone();

        const string trackerBaselineBackupPath = "test_backups/normalizer_tracker_authority_baseline.json";
        await _fs.WriteFileAtomicAsync(trackerBaselineBackupPath, baselineRoot.ToJsonString());
        await _fs.WriteFileAtomicAsync(GuardianProjectState.TrackerPath, commandRoot.ToJsonString());

        var effectiveBackups = additionalBackups != null
            ? new Dictionary<string, string>(additionalBackups, StringComparer.OrdinalIgnoreCase)
            : new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        effectiveBackups[GuardianProjectState.TrackerPath] = trackerBaselineBackupPath;

        const string guardiansPath = "game_state/meta/guardians.json";
        if (!effectiveBackups.ContainsKey(guardiansPath))
        {
            var currentGuardiansJson = await _fs.ReadFileAsync(guardiansPath);
            if (!string.IsNullOrWhiteSpace(currentGuardiansJson) &&
                JsonNode.Parse(currentGuardiansJson) is JsonObject currentGuardiansRoot)
            {
                var guardiansBaselineRoot = BuildGuardianNormalizerBaselineRoot(currentGuardiansRoot, currentTrackerRoot);

                const string guardiansBaselineBackupPath = "test_backups/normalizer_guardians_authority_baseline.json";
                await _fs.WriteFileAtomicAsync(guardiansBaselineBackupPath, guardiansBaselineRoot.ToJsonString());
                effectiveBackups[guardiansPath] = guardiansBaselineBackupPath;
            }
        }

        await normalizer.NormalizeAccumulatedStateAsync(effectiveBackups);
    }

    private static JsonObject BuildGuardianNormalizerBaselineRoot(JsonObject currentGuardiansRoot, JsonObject currentTrackerRoot)
    {
        var baselineRoot = currentGuardiansRoot.DeepClone()!.AsObject();
        baselineRoot.Remove("UpdateGuardians");
        baselineRoot.Remove("guardianPowerEvents");
        baselineRoot.Remove(GuardianTradeRequestState.UpdateReceiptsProperty);

        var loreQuestSourceProjectIds = CollectCompletedGuardianProjectIds(
            currentTrackerRoot,
            static (projectType, projectOrigin) =>
                string.Equals(projectType, "lore_research", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(projectOrigin, "archive_consultation", StringComparison.OrdinalIgnoreCase));
        var relicForgingSourceProjectIds = CollectCompletedGuardianProjectIds(
            currentTrackerRoot,
            static (projectType, _) => string.Equals(projectType, "relic_forging", StringComparison.OrdinalIgnoreCase));

        if (baselineRoot["guardians"] is JsonArray guardians)
        {
            foreach (var guardian in guardians.OfType<JsonObject>())
                RewindGuardianProjectSideEffects(guardian, loreQuestSourceProjectIds, relicForgingSourceProjectIds);
        }

        if (baselineRoot["activeGuardian"] is JsonObject activeGuardian)
            RewindGuardianProjectSideEffects(activeGuardian, loreQuestSourceProjectIds, relicForgingSourceProjectIds);

        return baselineRoot;
    }

    private static HashSet<string> CollectCompletedGuardianProjectIds(
        JsonObject trackerRoot,
        Func<string?, string?, bool> matchesProject)
    {
        var ids = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (trackerRoot["completedProjects"] is not JsonArray completedProjects)
            return ids;

        foreach (var completedProject in completedProjects.OfType<JsonObject>())
        {
            var project = completedProject["project"] as JsonObject;
            var projectId = project?["projectId"]?.GetValue<string?>();
            if (string.IsNullOrWhiteSpace(projectId))
                continue;

            var projectType = project?["projectType"]?.GetValue<string?>();
            var projectOrigin = project?["projectOrigin"]?.GetValue<string?>();
            if (matchesProject(projectType, projectOrigin))
                ids.Add(projectId);
        }

        return ids;
    }

    private static void RewindGuardianProjectSideEffects(
        JsonObject guardian,
        IReadOnlySet<string> loreQuestSourceProjectIds,
        IReadOnlySet<string> relicForgingSourceProjectIds)
    {
        if (guardian["questManagement"] is JsonObject questManagement)
        {
            foreach (var arrayName in new[] { "availableQuests", "activeQuests", "completedQuests" })
            {
                if (questManagement[arrayName] is not JsonArray quests)
                    continue;

                for (var index = quests.Count - 1; index >= 0; index--)
                {
                    if (quests[index] is not JsonObject quest)
                        continue;

                    var sourceProjectId = quest["sourceProjectId"]?.GetValue<string?>();
                    var questOrigin = quest["questOrigin"]?.GetValue<string?>();
                    if (string.IsNullOrWhiteSpace(sourceProjectId) || !loreQuestSourceProjectIds.Contains(sourceProjectId))
                        continue;

                    if (!string.Equals(questOrigin, GuardianProjectState.LoreResearchHookOrigin, StringComparison.OrdinalIgnoreCase) &&
                        !string.Equals(questOrigin, GuardianProjectState.LoreResearchSpecialLineOrigin, StringComparison.OrdinalIgnoreCase) &&
                        !string.Equals(questOrigin, GuardianProjectState.ArchiveConsultationHookOrigin, StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }

                    quests.RemoveAt(index);
                }
            }
        }

        if (guardian["gachaSystem"] is JsonObject gachaSystem &&
            gachaSystem["gachaHistory"] is JsonArray gachaHistory)
        {
            for (var index = gachaHistory.Count - 1; index >= 0; index--)
            {
                if (gachaHistory[index] is not JsonObject historyEntry)
                    continue;

                var sourceProjectId = (historyEntry["gachaBonusAudit"] as JsonObject)?["sourceProjectId"]?.GetValue<string?>();
                if (!string.IsNullOrWhiteSpace(sourceProjectId) && relicForgingSourceProjectIds.Contains(sourceProjectId))
                    gachaHistory.RemoveAt(index);
            }
        }
    }

}
