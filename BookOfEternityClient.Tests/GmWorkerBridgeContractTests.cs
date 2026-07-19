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
        var soulProfile = GmWorkerBridgeTestFixtures.SoulContentCodexProfile();

        var json = GmWorkerJson.Serialize(profile);
        var guardianJson = GmWorkerJson.Serialize(guardianProfile);
        var soulJson = GmWorkerJson.Serialize(soulProfile);
        var roundTrip = GmWorkerJson.Deserialize<WorkerBridgeProfile>(json);

        Assert.Contains("\"launchVisibility\": \"hidden\"", json, StringComparison.Ordinal);
        Assert.Contains("\"role\": \"validation-repair\"", json, StringComparison.Ordinal);
        Assert.Contains("\"validation-repair\"", json, StringComparison.Ordinal);
        Assert.Contains("\"role\": \"guardian-abode-content\"", guardianJson, StringComparison.Ordinal);
        Assert.Contains("\"guardian-abode-content\"", guardianJson, StringComparison.Ordinal);
        Assert.Contains("\"role\": \"soul-content\"", soulJson, StringComparison.Ordinal);
        Assert.Contains("\"soul-content\"", soulJson, StringComparison.Ordinal);
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
    public void ValidationRepairProposal_RejectsDuplicateChangedFilePaths()
    {
        var profile = GmWorkerBridgeTestFixtures.ValidationRepairCodexProfile();
        var task = GmWorkerBridgeTestFixtures.ValidationRepairTask();
        var valid = GmWorkerBridgeTestFixtures.ValidationRepairProposal();
        var proposal = valid with
        {
            ChangedFiles = [valid.ChangedFiles[0], valid.ChangedFiles[0]]
        };

        var result = GmWorkerContractValidator.ValidateProposal(proposal, task, profile);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, error =>
            error.Contains("duplicate", StringComparison.OrdinalIgnoreCase) &&
            error.Contains(valid.ChangedFiles[0].Path, StringComparison.Ordinal));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("not-a-sha256")]
    public void ValidationRepairProposal_RejectsMissingOrMalformedAfterSha256(string? afterSha256)
    {
        var profile = GmWorkerBridgeTestFixtures.ValidationRepairCodexProfile();
        var task = GmWorkerBridgeTestFixtures.ValidationRepairTask();
        var valid = GmWorkerBridgeTestFixtures.ValidationRepairProposal();
        var proposal = valid with
        {
            ChangedFiles =
            [
                valid.ChangedFiles[0] with { AfterSha256 = afterSha256 }
            ]
        };

        var result = GmWorkerContractValidator.ValidateProposal(proposal, task, profile);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, error =>
            error.Contains("afterSha256", StringComparison.OrdinalIgnoreCase) &&
            error.Contains("64", StringComparison.Ordinal));
    }

    [Fact]
    public void ValidationRepairProposal_RejectsContentRefOutsideOwnProposalDirectory()
    {
        var profile = GmWorkerBridgeTestFixtures.ValidationRepairCodexProfile();
        var task = GmWorkerBridgeTestFixtures.ValidationRepairTask();
        var valid = GmWorkerBridgeTestFixtures.ValidationRepairProposal();
        var proposal = valid with
        {
            ChangedFiles =
            [
                valid.ChangedFiles[0] with
                {
                    ContentRef = "worker_proposals/another_proposal/game_state/world/weather.json"
                }
            ]
        };

        var result = GmWorkerContractValidator.ValidateProposal(proposal, task, profile);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, error =>
            error.Contains("contentRef", StringComparison.OrdinalIgnoreCase) &&
            error.Contains(proposal.ProposalId, StringComparison.Ordinal));
    }

    [Fact]
    public void ValidationRepairTask_RejectsMalformedContextHash()
    {
        var profile = GmWorkerBridgeTestFixtures.ValidationRepairCodexProfile();
        var valid = GmWorkerBridgeTestFixtures.ValidationRepairTask();
        var task = valid with
        {
            ContextFiles = [valid.ContextFiles[0] with { Sha256 = "sha256-placeholder" }]
        };

        var result = GmWorkerContractValidator.ValidateTaskPacket(task, profile);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, error =>
            error.Contains("contextFiles.sha256", StringComparison.OrdinalIgnoreCase) &&
            error.Contains("64", StringComparison.Ordinal));
    }

    [Fact]
    public void CompletedValidationRepairProposal_RejectsEmptyChangedFiles()
    {
        var profile = GmWorkerBridgeTestFixtures.ValidationRepairCodexProfile();
        var task = GmWorkerBridgeTestFixtures.ValidationRepairTask();
        var proposal = GmWorkerBridgeTestFixtures.ValidationRepairProposal() with
        {
            ChangedFiles = []
        };

        var result = GmWorkerContractValidator.ValidateProposal(proposal, task, profile);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, error =>
            error.Contains("validation-repair", StringComparison.OrdinalIgnoreCase) &&
            error.Contains("changedFiles", StringComparison.OrdinalIgnoreCase));
    }

    [Theory]
    [InlineData(WorkerProposalStatus.Failed)]
    [InlineData(WorkerProposalStatus.TimedOut)]
    [InlineData(WorkerProposalStatus.Rejected)]
    public void NonCompletedProposal_RejectsCanonicalChangedFiles(WorkerProposalStatus status)
    {
        var profile = GmWorkerBridgeTestFixtures.ValidationRepairCodexProfile();
        var task = GmWorkerBridgeTestFixtures.ValidationRepairTask();
        var proposal = GmWorkerBridgeTestFixtures.ValidationRepairProposal() with { Status = status };

        var result = GmWorkerContractValidator.ValidateProposal(proposal, task, profile);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, error =>
            error.Contains("non-completed", StringComparison.OrdinalIgnoreCase) &&
            error.Contains("changedFiles", StringComparison.OrdinalIgnoreCase));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("not-a-sha256")]
    public void ValidationRepairProposal_RejectsMissingMalformedOrUnpinnedBeforeSha256(string? beforeSha256)
    {
        var profile = GmWorkerBridgeTestFixtures.ValidationRepairCodexProfile();
        var task = GmWorkerBridgeTestFixtures.ValidationRepairTask();
        var valid = GmWorkerBridgeTestFixtures.ValidationRepairProposal();
        var proposal = valid with
        {
            ChangedFiles = [valid.ChangedFiles[0] with { BeforeSha256 = beforeSha256 }]
        };

        var result = GmWorkerContractValidator.ValidateProposal(proposal, task, profile);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, error =>
            error.Contains("beforeSha256", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void ValidationRepairProposal_RejectsBeforeSha256DifferentFromTaskContext()
    {
        var profile = GmWorkerBridgeTestFixtures.ValidationRepairCodexProfile();
        var task = GmWorkerBridgeTestFixtures.ValidationRepairTask();
        var valid = GmWorkerBridgeTestFixtures.ValidationRepairProposal();
        var proposal = valid with
        {
            ChangedFiles = [valid.ChangedFiles[0] with { BeforeSha256 = new string('c', 64) }]
        };

        var result = GmWorkerContractValidator.ValidateProposal(proposal, task, profile);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, error =>
            error.Contains("beforeSha256", StringComparison.OrdinalIgnoreCase) &&
            error.Contains("context", StringComparison.OrdinalIgnoreCase));
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
    public void SoulContentTaskAndProposal_RequireSoulTypedAfterlifeProposal()
    {
        var profile = GmWorkerBridgeTestFixtures.SoulContentCodexProfile();
        var task = GmWorkerBridgeTestFixtures.SoulContentTask();
        var proposal = GmWorkerBridgeTestFixtures.SoulContentProposal();

        var taskResult = GmWorkerContractValidator.ValidateTaskPacket(task, profile);
        var proposalResult = GmWorkerContractValidator.ValidateProposal(proposal, task, profile);

        Assert.True(taskResult.IsValid, string.Join(Environment.NewLine, taskResult.Errors));
        Assert.True(proposalResult.IsValid, string.Join(Environment.NewLine, proposalResult.Errors));
    }

    [Fact]
    public void SoulContentTask_RejectsMissingAfterlifeContract()
    {
        var profile = GmWorkerBridgeTestFixtures.SoulContentCodexProfile();
        var task = GmWorkerBridgeTestFixtures.SoulContentTask() with
        {
            AfterlifeContract = null
        };

        var result = GmWorkerContractValidator.ValidateTaskPacket(task, profile);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, error =>
            error.Contains("soul-content", StringComparison.OrdinalIgnoreCase) &&
            error.Contains("afterlifeContract", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void SoulContentProposal_RejectsPlayerOwnedIdentityMutation()
    {
        var profile = GmWorkerBridgeTestFixtures.SoulContentCodexProfile();
        var task = GmWorkerBridgeTestFixtures.SoulContentTask();
        var valid = GmWorkerBridgeTestFixtures.SoulContentProposal();
        var proposal = valid with
        {
            AuthoringProposal = valid.AuthoringProposal! with
            {
                CreatedEntities =
                [
                    valid.AuthoringProposal!.CreatedEntities[0] with
                    {
                        RequiredFields =
                        [
                            new WorkerAuthoredField
                            {
                                Name = "soulName",
                                Value = "Новое имя, выбранное воркером"
                            },
                            new WorkerAuthoredField
                            {
                                Name = "soulFormDescription",
                                Value = "Новая форма души, выбранная воркером"
                            }
                        ]
                    }
                ]
            },
            SoulContentProposal = valid.SoulContentProposal! with
            {
                ForbiddenReadonlyFields = ["soulName"]
            }
        };

        var result = GmWorkerContractValidator.ValidateProposal(proposal, task, profile);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, error =>
            error.Contains("soulName", StringComparison.OrdinalIgnoreCase) &&
            error.Contains("readonly", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(result.Errors, error =>
            error.Contains("soulFormDescription", StringComparison.OrdinalIgnoreCase) &&
            error.Contains("readonly", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void SoulContentProposal_RejectsOrdinaryCharacterInventoryAndMortalStateSubstitutes()
    {
        var profile = GmWorkerBridgeTestFixtures.SoulContentCodexProfile();
        var task = GmWorkerBridgeTestFixtures.SoulContentTask();
        var valid = GmWorkerBridgeTestFixtures.SoulContentProposal();
        var proposal = valid with
        {
            AuthoringProposal = valid.AuthoringProposal! with
            {
                CreatedEntities =
                [
                    valid.AuthoringProposal!.CreatedEntities[0] with
                    {
                        EntityType = "character",
                        RequiredFields =
                        [
                            new WorkerAuthoredField
                            {
                                Name = "inventory",
                                Value = "Use game_state/player/character.json and player inventory as a soul substitute."
                            }
                        ]
                    }
                ]
            },
            AfterlifeProposal = valid.AfterlifeProposal! with
            {
                TargetSurfaces =
                [
                    "game_state/meta/soul_state.json",
                    "game_state/player/character.json"
                ]
            }
        };

        var result = GmWorkerContractValidator.ValidateProposal(proposal, task, profile);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, error =>
            error.Contains("soul", StringComparison.OrdinalIgnoreCase) &&
            error.Contains("substitute", StringComparison.OrdinalIgnoreCase));
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
