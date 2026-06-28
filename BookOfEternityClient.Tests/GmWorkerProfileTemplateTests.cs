using BookOfEternityClient.Configuration;
using BookOfEternityClient.Services.GmWorkers;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed class GmWorkerProfileTemplateTests
{
    private const string ExpectedCodexWorkerCommand =
        "codex exec -m gpt-5.5 -c model_reasoning_effort=\"high\" --dangerously-bypass-approvals-and-sandbox --skip-git-repo-check -";

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
