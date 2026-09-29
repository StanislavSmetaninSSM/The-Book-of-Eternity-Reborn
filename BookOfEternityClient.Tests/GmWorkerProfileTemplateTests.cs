using BookOfEternityClient.Configuration;
using BookOfEternityClient.Services.GmWorkers;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed class GmWorkerProfileTemplateTests
{
    private const string ExpectedCodexWorkerCommand =
        "codex exec -m gpt-5.6-terra -c model_reasoning_effort=high --dangerously-bypass-approvals-and-sandbox --skip-git-repo-check -";

    [Fact]
    public void DefaultTemplates_AreDisabledRunnerBasedAndValid()
    {
        var templates = GmWorkerBridgeProfileTemplates.CreateDefaultTemplates();

        Assert.Collection(
            templates.OrderBy(template => template.WorkerId),
            analysis => AssertTemplate(
                analysis,
                "analysis_codex",
                WorkerRole.Analysis,
                WorkerTaskType.Analysis,
                ExpectedCodexWorkerCommand),
            guardianAbode => AssertTemplate(
                guardianAbode,
                "guardian_abode_content_codex",
                WorkerRole.GuardianAbodeContent,
                WorkerTaskType.GuardianAbodeContent,
                ExpectedCodexWorkerCommand),
            inventory => AssertTemplate(
                inventory,
                "inventory_content_codex",
                WorkerRole.InventoryContent,
                WorkerTaskType.InventoryContent,
                ExpectedCodexWorkerCommand),
            narrative => AssertTemplate(
                narrative,
                "narrative_draft_codex",
                WorkerRole.NarrativeDraft,
                WorkerTaskType.NarrativeDraft,
                ExpectedCodexWorkerCommand),
            npc => AssertTemplate(
                npc,
                "npc_content_codex",
                WorkerRole.NpcContent,
                WorkerTaskType.NpcContent,
                ExpectedCodexWorkerCommand),
            skill => AssertTemplate(
                skill,
                "skill_content_codex",
                WorkerRole.SkillContent,
                WorkerTaskType.SkillContent,
                ExpectedCodexWorkerCommand),
            soul => AssertTemplate(
                soul,
                "soul_content_codex",
                WorkerRole.SoulContent,
                WorkerTaskType.SoulContent,
                ExpectedCodexWorkerCommand),
            repair => AssertTemplate(
                repair,
                "validation_repair_codex",
                WorkerRole.ValidationRepair,
                WorkerTaskType.ValidationRepair,
                ExpectedCodexWorkerCommand));
    }

    [Fact]
    public void DisabledTemplates_DoNotRouteTasksUntilUserEnablesThem()
    {
        var templates = GmWorkerBridgeProfileTemplates.CreateDefaultTemplates();

        var result = GmWorkerBridgePool.SelectWorkerForTask(templates, WorkerTaskType.ValidationRepair);

        Assert.False(result.Found);
        Assert.Null(result.Profile);
    }

    [Fact]
    public void DefaultTemplates_DoNotAdvertiseDeprecatedGeminiCliProfiles()
    {
        var templates = GmWorkerBridgeProfileTemplates.CreateDefaultTemplates();

        Assert.DoesNotContain(templates, template =>
            template.WorkerId.Contains("gemini", StringComparison.OrdinalIgnoreCase) ||
            template.DisplayName.Contains("gemini", StringComparison.OrdinalIgnoreCase) ||
            template.LaunchCommand.Contains("gemini", StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// Makes the exact narrative repair path usable while keeping every other output outside the repair profile.
    /// </summary>
    /// <param name="fromDefaults">
    /// <see langword="true"/> selects the default-list repair profile; <see langword="false"/> calls the dedicated repair factory.
    /// </param>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ValidationRepairTemplates_AllowExactNarrativeWithoutOtherOutputAccess(bool fromDefaults)
    {
        const string narrativePath = "output/narrative_response.json";
        var profile = fromDefaults
            ? Assert.Single(GmWorkerBridgeProfileTemplates.CreateDefaultTemplates(),
                value => value.Permissions.TaskTypes.Contains(WorkerTaskType.ValidationRepair))
            : GmWorkerBridgeProfileTemplates.CreateValidationRepairCodexTemplate();
        var task = GmWorkerBridgeTestFixtures.ValidationRepairTask() with
        {
            AllowedProposalPaths = [narrativePath],
            ContextFiles = [new WorkerFileReference { Path = narrativePath, Sha256 = new string('a', 64) }],
            ValidationIssues =
            [
                new WorkerValidationIssue
                {
                    Code = "narrative_response_missing_timestamp",
                    Path = narrativePath,
                    Message = "The current narrative requires its ordinary timestamp."
                }
            ]
        };

        var validation = GmWorkerContractValidator.ValidateTaskPacket(task, profile);

        Assert.True(validation.IsValid, string.Join(Environment.NewLine, validation.Errors));
        Assert.Contains(profile.Permissions.ReadPaths,
            pattern => GmWorkerContractValidator.PathMatches(pattern, narrativePath));
        foreach (var forbidden in new[]
                 {
                     "output/interface_updates.json", "output/debug_logs.json", "output/another_response.json",
                     "output/narrative_response.json/another.json"
                 })
        {
            Assert.DoesNotContain(profile.Permissions.ReadPaths,
                pattern => GmWorkerContractValidator.PathMatches(pattern, forbidden));
            Assert.DoesNotContain(profile.Permissions.ProposalWritePaths,
                pattern => GmWorkerContractValidator.PathMatches(pattern, forbidden));
        }
        Assert.Equal(new[] { "game_state/**", "lore/**", "input/**", "ready/**", narrativePath },
            profile.Permissions.ReadPaths);
        Assert.Equal(new[] { "game_state/**", "lore/**", "ready/**", narrativePath },
            profile.Permissions.ProposalWritePaths);
    }

    /// <summary>
    /// Keeps a saved repair profile's existing permissions unchanged when settings are loaded.
    /// </summary>
    [Fact]
    public void SettingsWithExistingRepairProfile_DoesNotWidenSavedNarrativePermissions()
    {
        var template = GmWorkerBridgeProfileTemplates.CreateValidationRepairCodexTemplate();
        var saved = template with
        {
            Enabled = true,
            Permissions = template.Permissions with
            {
                ReadPaths = ["game_state/**", "lore/**", "input/**", "ready/**"],
                ProposalWritePaths = ["game_state/**", "lore/**", "ready/**"]
            }
        };
        var settings = new GameSettings();

        settings.ApplyLoadedValues(new GameSettings { GmWorkerBridgeProfiles = [saved] });

        var actual = Assert.Single(settings.GmWorkerBridgeProfiles);
        Assert.Equal(saved.Permissions.ReadPaths, actual.Permissions.ReadPaths);
        Assert.Equal(saved.Permissions.ProposalWritePaths, actual.Permissions.ProposalWritePaths);
        Assert.True(actual.Enabled);
    }

    [Fact]
    public void DefaultTemplates_IncludeCodexNarrativeDraftTemplate()
    {
        var templates = GmWorkerBridgeProfileTemplates.CreateDefaultTemplates();

        var template = Assert.Single(templates, template => template.WorkerId == "narrative_draft_codex");
        AssertTemplate(
            template,
            "narrative_draft_codex",
            WorkerRole.NarrativeDraft,
            WorkerTaskType.NarrativeDraft,
            ExpectedCodexWorkerCommand);
    }

    [Fact]
    public void DefaultTemplates_PreserveQuotedCodexConfigInRunnerAgentCommand()
    {
        var template = GmWorkerBridgeProfileTemplates.CreateNarrativeDraftCodexTemplate();

        var startInfo = GmWorkerBridgePool.CreateWorkerStartInfo(template, Environment.CurrentDirectory);
        var agentCommandIndex = startInfo.ArgumentList.IndexOf("-AgentCommand");

        Assert.True(agentCommandIndex >= 0);
        Assert.True(agentCommandIndex + 1 < startInfo.ArgumentList.Count);
        Assert.Equal(ExpectedCodexWorkerCommand, startInfo.ArgumentList[agentCommandIndex + 1]);
    }

    [Fact]
    public void SettingsWithNoWorkerProfiles_ReceivesDisabledTemplates()
    {
        var settings = new GameSettings();
        var loaded = new GameSettings
        {
            GmWorkerBridgeProfiles = []
        };

        settings.ApplyLoadedValues(loaded);

        Assert.Equal(
            ["analysis_codex", "guardian_abode_content_codex", "inventory_content_codex", "narrative_draft_codex", "npc_content_codex", "skill_content_codex", "soul_content_codex", "validation_repair_codex"],
            settings.GmWorkerBridgeProfiles.Select(profile => profile.WorkerId).OrderBy(id => id).ToArray());
        Assert.All(settings.GmWorkerBridgeProfiles, profile => Assert.False(profile.Enabled));
    }

    [Fact]
    public void SettingsWithRetiredBuiltInWorkerCommand_MigratesToCurrentTemplateCommand()
    {
        var currentTemplate = GmWorkerBridgeProfileTemplates.CreateNarrativeDraftCodexTemplate();
        var retiredModel = "gpt-5" + ".5";
        var retiredLaunchCommand = currentTemplate.LaunchCommand
            .Replace("gpt-5.6-terra", retiredModel, StringComparison.Ordinal)
            .Replace("model_reasoning_effort=high", "model_reasoning_effort=\\\"high\\\"", StringComparison.Ordinal);
        var settings = new GameSettings();
        var loaded = new GameSettings
        {
            GmWorkerBridgeProfiles = [currentTemplate with { LaunchCommand = retiredLaunchCommand }]
        };

        settings.ApplyLoadedValues(loaded);

        var migrated = Assert.Single(settings.GmWorkerBridgeProfiles);
        Assert.Equal(currentTemplate.LaunchCommand, migrated.LaunchCommand);
    }

    [Fact]
    public void SettingsWithCustomWorkerWrappingRetiredPayload_PreservesCustomCommand()
    {
        var currentTemplate = GmWorkerBridgeProfileTemplates.CreateNarrativeDraftCodexTemplate();
        var retiredModel = "gpt-5" + ".5";
        var retiredPayload = GmWorkerBridgeProfileTemplates.CodexWorkerExecCommand
            .Replace("gpt-5.6-terra", retiredModel, StringComparison.Ordinal);
        var customLaunchCommand = $"custom-prefix {retiredPayload} custom-suffix";
        var settings = new GameSettings();
        var loaded = new GameSettings
        {
            GmWorkerBridgeProfiles =
            [
                currentTemplate with
                {
                    WorkerId = "custom_worker",
                    LaunchCommand = customLaunchCommand
                }
            ]
        };

        settings.ApplyLoadedValues(loaded);

        var preserved = Assert.Single(settings.GmWorkerBridgeProfiles);
        Assert.Equal(customLaunchCommand, preserved.LaunchCommand);
    }

    [Fact]
    public void SettingsWithExistingWorkerProfiles_PreservesConfiguredProfilesWithoutAppendingTemplates()
    {
        var customProfile = GmWorkerBridgeTestFixtures.AnalysisCodexProfile() with
        {
            WorkerId = "custom_analysis_worker",
            Enabled = true
        };
        var settings = new GameSettings();
        var loaded = new GameSettings
        {
            GmWorkerBridgeProfiles = [customProfile]
        };

        settings.ApplyLoadedValues(loaded);

        var profile = Assert.Single(settings.GmWorkerBridgeProfiles);
        Assert.Equal("custom_analysis_worker", profile.WorkerId);
        Assert.True(profile.Enabled);
        Assert.Contains("gm_worker_cli_runner.ps1", profile.LaunchCommand, StringComparison.Ordinal);
    }

    private static void AssertTemplate(
        WorkerBridgeProfile template,
        string expectedWorkerId,
        WorkerRole expectedRole,
        WorkerTaskType expectedTaskType,
        string expectedAgentCommand)
    {
        Assert.Equal(expectedWorkerId, template.WorkerId);
        Assert.Equal(expectedRole, template.Role);
        Assert.False(template.Enabled);
        Assert.Equal(WorkerLaunchVisibility.Hidden, template.LaunchVisibility);
        Assert.Equal(1, template.MaxConcurrentTasks);
        Assert.Contains(expectedTaskType, template.Permissions.TaskTypes);
        Assert.Contains("BookOfEternityClient/Launcher/gm_worker_cli_runner.ps1", template.LaunchCommand, StringComparison.Ordinal);
        Assert.Contains("-AgentCommand", template.LaunchCommand, StringComparison.Ordinal);
        Assert.Contains(expectedAgentCommand.Replace("\"", "\\\"", StringComparison.Ordinal), template.LaunchCommand, StringComparison.Ordinal);

        var validation = GmWorkerContractValidator.ValidateProfile(template);
        Assert.True(validation.IsValid, string.Join(Environment.NewLine, validation.Errors));
    }
}
