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
        var guardianProfile = GmWorkerBridgeTestFixtures.GuardianAbodeContentCodexProfile();

        var json = GmWorkerJson.Serialize(profile);
        var guardianJson = GmWorkerJson.Serialize(guardianProfile);
        var roundTrip = GmWorkerJson.Deserialize<WorkerBridgeProfile>(json);

        Assert.Contains("\"launchVisibility\": \"hidden\"", json, StringComparison.Ordinal);
        Assert.Contains("\"role\": \"validation-repair\"", json, StringComparison.Ordinal);
        Assert.Contains("\"validation-repair\"", json, StringComparison.Ordinal);
        Assert.Contains("\"role\": \"guardian-abode-content\"", guardianJson, StringComparison.Ordinal);
        Assert.Contains("\"guardian-abode-content\"", guardianJson, StringComparison.Ordinal);
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
    public void GuardianAbodeContentTaskAndProposal_RequireAfterlifeTypedProposal()
    {
        var profile = GmWorkerBridgeTestFixtures.GuardianAbodeContentCodexProfile();
        var task = GmWorkerBridgeTestFixtures.GuardianAbodeContentTask();
        var proposal = GmWorkerBridgeTestFixtures.GuardianAbodeContentProposal();

        var taskResult = GmWorkerContractValidator.ValidateTaskPacket(task, profile);
        var proposalResult = GmWorkerContractValidator.ValidateProposal(proposal, task, profile);

        Assert.True(taskResult.IsValid, string.Join(Environment.NewLine, taskResult.Errors));
        Assert.True(proposalResult.IsValid, string.Join(Environment.NewLine, proposalResult.Errors));
    }

    [Fact]
    public void GuardianAbodeContentTask_RejectsMissingAfterlifeContract()
    {
        var profile = GmWorkerBridgeTestFixtures.GuardianAbodeContentCodexProfile();
        var task = GmWorkerBridgeTestFixtures.GuardianAbodeContentTask() with
        {
            AfterlifeContract = null
        };

        var result = GmWorkerContractValidator.ValidateTaskPacket(task, profile);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, error =>
            error.Contains("afterlifeContract", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void GuardianAbodeContentProposal_RejectsMortalNpcAndFactionSubstitutes()
    {
        var profile = GmWorkerBridgeTestFixtures.GuardianAbodeContentCodexProfile();
        var task = GmWorkerBridgeTestFixtures.GuardianAbodeContentTask();
        var proposal = GmWorkerBridgeTestFixtures.GuardianAbodeContentProposal() with
        {
            AuthoringProposal = GmWorkerBridgeTestFixtures.GuardianAbodeContentProposal().AuthoringProposal! with
            {
                CreatedEntities =
                [
                    GmWorkerBridgeTestFixtures.GuardianAbodeContentProposal().AuthoringProposal!.CreatedEntities[0] with
                    {
                        EntityType = "npc",
                        RequiredFields =
                        [
                            new WorkerAuthoredField
                            {
                                Name = "substitute",
                                Value = "Use NPCRelationshipChanges and factionDataChanges instead."
                            }
                        ]
                    }
                ]
            }
        };

        var result = GmWorkerContractValidator.ValidateProposal(proposal, task, profile);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, error =>
            error.Contains("Guardian", StringComparison.OrdinalIgnoreCase) &&
            error.Contains("Mortal", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void GuardianAbodeContentProposal_RejectsHiddenDossierLeak()
    {
        var profile = GmWorkerBridgeTestFixtures.GuardianAbodeContentCodexProfile();
        var task = GmWorkerBridgeTestFixtures.GuardianAbodeContentTask();
        var valid = GmWorkerBridgeTestFixtures.GuardianAbodeContentProposal();
        var proposal = valid with
        {
            GuardianAbodeProposal = valid.GuardianAbodeProposal! with
            {
                PlayerVisibleSummary = "Азалия тайно проверяет долг души.",
                DossierNotes =
                [
                    valid.GuardianAbodeProposal!.DossierNotes[0] with
                    {
                        Visibility = "visible"
                    }
                ]
            }
        };

        var result = GmWorkerContractValidator.ValidateProposal(proposal, task, profile);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, error =>
            error.Contains("hidden", StringComparison.OrdinalIgnoreCase) ||
            error.Contains("GM-only", StringComparison.OrdinalIgnoreCase));
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

    [Fact]
    public void AfterlifeAuthoringTaskAndProposal_RequireRealmAwareWrapper()
    {
        var profile = GmWorkerBridgeTestFixtures.AnalysisCodexProfile() with
        {
            Role = WorkerRole.Analysis,
            Permissions = GmWorkerBridgeTestFixtures.AnalysisCodexProfile().Permissions with
            {
                TaskTypes = [WorkerTaskType.Analysis],
                ReadPaths =
                [
                    "game_state/meta/**",
                    "game_state/control/**",
                    "OtherGuides/Afterlife_Contract_Matrix.md"
                ]
            }
        };
        var task = GmWorkerBridgeTestFixtures.AfterlifeWorkerTask();
        var proposal = GmWorkerBridgeTestFixtures.AfterlifeWorkerProposal();

        var taskResult = GmWorkerContractValidator.ValidateTaskPacket(task, profile);
        var proposalResult = GmWorkerContractValidator.ValidateProposal(proposal, task, profile);

        Assert.True(taskResult.IsValid, string.Join(Environment.NewLine, taskResult.Errors));
        Assert.True(proposalResult.IsValid, string.Join(Environment.NewLine, proposalResult.Errors));
    }

    [Fact]
    public void AfterlifeAuthoringProposal_RejectsMortalSubstituteSurfaces()
    {
        var profile = GmWorkerBridgeTestFixtures.AnalysisCodexProfile() with
        {
            Permissions = GmWorkerBridgeTestFixtures.AnalysisCodexProfile().Permissions with
            {
                ReadPaths = ["game_state/meta/**", "game_state/control/**"]
            }
        };
        var task = GmWorkerBridgeTestFixtures.AfterlifeWorkerTask();
        var proposal = GmWorkerBridgeTestFixtures.AfterlifeWorkerProposal() with
        {
            AfterlifeProposal = GmWorkerBridgeTestFixtures.AfterlifeWorkerProposal().AfterlifeProposal! with
            {
                TargetSurfaces =
                [
                    "game_state/meta/guardians.json",
                    "game_state/world/world_events.json"
                ],
                GmReviewNotes =
                [
                    "Use worldEventsLog as a quick substitute for afterlife chronicles."
                ]
            }
        };

        var result = GmWorkerContractValidator.ValidateProposal(proposal, task, profile);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, error => error.Contains("Mortal World substitute", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void AfterlifeAuthoringTask_RejectsMissingWrapperFromAuthoringGoal()
    {
        var profile = GmWorkerBridgeTestFixtures.InventoryContentCodexProfile();
        var task = GmWorkerBridgeTestFixtures.InventoryContentTask() with
        {
            AuthoringRequest = GmWorkerBridgeTestFixtures.InventoryContentTask().AuthoringRequest! with
            {
                Goal = "Prepare an afterlife guardian relic proposal."
            },
            Instructions = "Return authoringProposal only."
        };

        var result = GmWorkerContractValidator.ValidateTaskPacket(task, profile);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, error =>
            error.Contains("afterlifeContract", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void AfterlifeDraftTask_RejectsMissingWrapperFromDraftRequest()
    {
        var profile = GmWorkerBridgeTestFixtures.NarrativeDraftCodexProfile();
        var task = GmWorkerBridgeTestFixtures.NarrativeDraftTask() with
        {
            DraftRequest = GmWorkerBridgeTestFixtures.NarrativeDraftTask().DraftRequest! with
            {
                SceneGoal = "Draft a Chaos Sea guardian encounter."
            },
            Instructions = "Return draftText only."
        };

        var result = GmWorkerContractValidator.ValidateTaskPacket(task, profile);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, error =>
            error.Contains("afterlifeContract", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void AfterlifeAuthoringProposal_RejectsRealmMismatch()
    {
        var profile = GmWorkerBridgeTestFixtures.AnalysisCodexProfile() with
        {
            Permissions = GmWorkerBridgeTestFixtures.AnalysisCodexProfile().Permissions with
            {
                ReadPaths = ["game_state/meta/**", "game_state/control/**"]
            }
        };
        var task = GmWorkerBridgeTestFixtures.AfterlifeWorkerTask();
        var proposal = GmWorkerBridgeTestFixtures.AfterlifeWorkerProposal() with
        {
            AfterlifeProposal = GmWorkerBridgeTestFixtures.AfterlifeWorkerProposal().AfterlifeProposal! with
            {
                RealmGate = WorkerAfterlifeRealmGate.ShiningAbode
            }
        };

        var result = GmWorkerContractValidator.ValidateProposal(proposal, task, profile);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, error => error.Contains("realm", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void AfterlifeAuthoringProposal_AllowsPlayerFacingMortalPastWithoutSubstituteSurface()
    {
        var profile = GmWorkerBridgeTestFixtures.AnalysisCodexProfile() with
        {
            Permissions = GmWorkerBridgeTestFixtures.AnalysisCodexProfile().Permissions with
            {
                ReadPaths = ["game_state/meta/**", "game_state/control/**"]
            }
        };
        var task = GmWorkerBridgeTestFixtures.AfterlifeWorkerTask();
        var proposal = GmWorkerBridgeTestFixtures.AfterlifeWorkerProposal() with
        {
            AfterlifeProposal = GmWorkerBridgeTestFixtures.AfterlifeWorkerProposal().AfterlifeProposal! with
            {
                PlayerVisibleSummary = "Душа вспоминает смертную жизнь, но хроника обновляется через поверхности посмертия."
            }
        };

        var result = GmWorkerContractValidator.ValidateProposal(proposal, task, profile);

        Assert.True(result.IsValid, string.Join(Environment.NewLine, result.Errors));
    }
}
