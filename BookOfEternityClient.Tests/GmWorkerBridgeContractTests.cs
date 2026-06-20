using BookOfEternityClient.Services.GmWorkers;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed class GmWorkerBridgeContractTests
{
    [Fact]
    public void ValidationRepairProfile_UsesHiddenLaunchAndValidatedWriteScope()
    {
        var profile = GmWorkerBridgeTestFixtures.ValidationRepairCodexProfile();

        Assert.Equal(WorkerLaunchVisibility.Hidden, profile.LaunchVisibility);
        Assert.Contains("gm_worker_cli_runner.ps1", profile.LaunchCommand, StringComparison.Ordinal);
        Assert.Contains("-AgentCommand", profile.LaunchCommand, StringComparison.Ordinal);
        Assert.Contains(WorkerTaskType.ValidationRepair, profile.Permissions.TaskTypes);
        Assert.False(profile.Permissions.ProposalOnly);
        Assert.True(profile.Permissions.RequiresValidation);

        var result = GmWorkerContractValidator.ValidateProfile(profile);

        Assert.True(result.IsValid, string.Join(Environment.NewLine, result.Errors));
    }

    [Fact]
    public void NarrativeDraftProfile_IsHiddenProposalOnlyAndReadOnly()
    {
        var profile = GmWorkerBridgeTestFixtures.NarrativeDraftCodexProfile();

        Assert.Equal(WorkerLaunchVisibility.Hidden, profile.LaunchVisibility);
        Assert.Contains("gm_worker_cli_runner.ps1", profile.LaunchCommand, StringComparison.Ordinal);
        Assert.Contains("-AgentCommand", profile.LaunchCommand, StringComparison.Ordinal);
        Assert.Contains(WorkerTaskType.NarrativeDraft, profile.Permissions.TaskTypes);
        Assert.True(profile.Permissions.ProposalOnly);
        Assert.False(profile.Permissions.RequiresValidation);
        Assert.Empty(profile.Permissions.ProposalWritePaths);

        var result = GmWorkerContractValidator.ValidateProfile(profile);

        Assert.True(result.IsValid, string.Join(Environment.NewLine, result.Errors));
    }

    [Fact]
    public void CreateWorkerStartInfo_DefaultRelativeRunner_ResolvesRunnerOutsideGameSession()
    {
        var root = Path.Combine(Path.GetTempPath(), "boe-worker-runner-resolution-" + Guid.NewGuid().ToString("N"));
        var runnerPath = Path.Combine(root, "BookOfEternityClient", "Launcher", "gm_worker_cli_runner.ps1");
        var gameSessionPath = Path.Combine(root, "BookOfEternityClient", "game_session");
        var originalCurrentDirectory = Environment.CurrentDirectory;
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(runnerPath)!);
            Directory.CreateDirectory(gameSessionPath);
            File.WriteAllText(runnerPath, "");
            Environment.CurrentDirectory = root;
            var profile = GmWorkerBridgeTestFixtures.NarrativeDraftCodexProfile();

            var startInfo = GmWorkerBridgePool.CreateWorkerStartInfo(profile, gameSessionPath);
            var fileArgumentIndex = startInfo.ArgumentList.IndexOf("-File");

            Assert.True(fileArgumentIndex >= 0);
            Assert.True(fileArgumentIndex + 1 < startInfo.ArgumentList.Count);
            Assert.Equal(Path.GetFullPath(runnerPath), startInfo.ArgumentList[fileArgumentIndex + 1]);
        }
        finally
        {
            Environment.CurrentDirectory = originalCurrentDirectory;
            try
            {
                if (Directory.Exists(root))
                    Directory.Delete(root, recursive: true);
            }
            catch
            {
                // ignored
            }
        }
    }

    [Fact]
    public void WorkerContracts_SerializeEnumsAsKebabCaseCamelCaseJson()
    {
        var profile = GmWorkerBridgeTestFixtures.ValidationRepairCodexProfile();

        var json = GmWorkerJson.Serialize(profile);
        var roundTrip = GmWorkerJson.Deserialize<WorkerBridgeProfile>(json);

        Assert.Contains("\"launchVisibility\": \"hidden\"", json, StringComparison.Ordinal);
        Assert.Contains("\"role\": \"validation-repair\"", json, StringComparison.Ordinal);
        Assert.Contains("\"validation-repair\"", json, StringComparison.Ordinal);
        Assert.NotNull(roundTrip);
        Assert.Equal(profile.WorkerId, roundTrip!.WorkerId);
        Assert.Equal(WorkerRole.ValidationRepair, roundTrip.Role);
        Assert.Equal(WorkerLaunchVisibility.Hidden, roundTrip.LaunchVisibility);
        Assert.Contains(WorkerTaskType.ValidationRepair, roundTrip.Permissions.TaskTypes);
    }

    [Fact]
    public void ValidationRepairTaskAndProposal_ValidateAgainstProfileScope()
    {
        var profile = GmWorkerBridgeTestFixtures.ValidationRepairCodexProfile();
        var task = GmWorkerBridgeTestFixtures.ValidationRepairTask();
        var proposal = GmWorkerBridgeTestFixtures.ValidationRepairProposal();

        var taskResult = GmWorkerContractValidator.ValidateTaskPacket(task, profile);
        var proposalResult = GmWorkerContractValidator.ValidateProposal(proposal, task, profile);

        Assert.True(taskResult.IsValid, string.Join(Environment.NewLine, taskResult.Errors));
        Assert.True(proposalResult.IsValid, string.Join(Environment.NewLine, proposalResult.Errors));
    }

    [Fact]
    public void NarrativeDraftProposalOnlyTask_RejectsChangedFiles()
    {
        var profile = GmWorkerBridgeTestFixtures.NarrativeDraftCodexProfile();
        var task = GmWorkerBridgeTestFixtures.NarrativeDraftTask();
        var proposal = GmWorkerBridgeTestFixtures.NarrativeDraftProposal() with
        {
            ChangedFiles =
            [
                new WorkerChangedFile
                {
                    Path = "game_state/world/current_location.json",
                    ChangeKind = WorkerFileChangeKind.Replace,
                    ContentRef = "worker_proposals/worker_proposal_20260620_0002/game_state/world/current_location.json"
                }
            ]
        };

        var result = GmWorkerContractValidator.ValidateProposal(proposal, task, profile);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, error => error.Contains("proposal-only", StringComparison.OrdinalIgnoreCase));
    }
}
