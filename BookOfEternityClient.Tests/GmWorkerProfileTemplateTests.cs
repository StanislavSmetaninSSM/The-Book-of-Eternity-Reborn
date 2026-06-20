using BookOfEternityClient.Configuration;
using BookOfEternityClient.Services.GmWorkers;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed class GmWorkerProfileTemplateTests
{
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
                "codex --dangerously-bypass-approvals-and-sandbox"),
            narrative => AssertTemplate(
                narrative,
                "narrative_draft_codex",
                WorkerRole.NarrativeDraft,
                WorkerTaskType.NarrativeDraft,
                "codex --dangerously-bypass-approvals-and-sandbox"),
            repair => AssertTemplate(
                repair,
                "validation_repair_codex",
                WorkerRole.ValidationRepair,
                WorkerTaskType.ValidationRepair,
                "codex --dangerously-bypass-approvals-and-sandbox"));
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
            "codex --dangerously-bypass-approvals-and-sandbox");
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
            ["analysis_codex", "narrative_draft_codex", "validation_repair_codex"],
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
        Assert.Contains(expectedAgentCommand, template.LaunchCommand, StringComparison.Ordinal);

        var validation = GmWorkerContractValidator.ValidateProfile(template);
        Assert.True(validation.IsValid, string.Join(Environment.NewLine, validation.Errors));
    }
}
