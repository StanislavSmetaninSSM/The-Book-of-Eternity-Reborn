using BookOfEternityClient.Core;
using BookOfEternityClient.Services;
using BookOfEternityClient.Services.GmWorkers;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed class GmWorkerProposalOnlyTests
{
    [Fact]
    public void BuildNarrativeDraftTask_ProducesReadOnlyProposalOnlyPacket()
    {
        var profile = GmWorkerBridgeTestFixtures.NarrativeDraftCodexProfile();
        var task = GmWorkerTaskPacketBuilder.BuildNarrativeDraftTask(
            profile,
            "worker_task_narrative",
            new WorkerTurnReference
            {
                SessionId = "test-session",
                RequestId = "test-request",
                TurnNumber = 12
            },
            new WorkerDraftRequest
            {
                SceneGoal = "Draft an atmospheric corridor description.",
                Tone = "dark fantasy, concise Russian prose",
                ContinuityNotes = ["Do not resolve the player's action."],
                TargetLength = "120-180 words"
            },
            [new WorkerFileReference { Path = "game_state/world/current_location.json", Sha256 = "sha-location" }],
            "2026-06-20T00:05:00Z");

        Assert.Equal(WorkerTaskType.NarrativeDraft, task.TaskType);
        Assert.Equal(profile.Role, task.Role);
        Assert.Equal(profile.TimeoutSeconds, task.TimeoutSeconds);
        Assert.Empty(task.AllowedProposalPaths);
        Assert.NotNull(task.DraftRequest);
        Assert.Contains(task.AcceptanceCriteria, criterion =>
            criterion.Contains("draftText", StringComparison.Ordinal));
        Assert.Contains(task.ForbiddenActions, action =>
            action.Contains("canonical game_session files", StringComparison.OrdinalIgnoreCase));
        Assert.Contains("proposal-only", task.Instructions, StringComparison.OrdinalIgnoreCase);

        var result = GmWorkerContractValidator.ValidateTaskPacket(task, profile);
        Assert.True(result.IsValid, string.Join(Environment.NewLine, result.Errors));
    }

    [Fact]
    public void BuildAnalysisTask_ProducesReadOnlyProposalOnlyPacket()
    {
        var profile = GmWorkerBridgeTestFixtures.AnalysisCodexProfile();
        var task = GmWorkerTaskPacketBuilder.BuildAnalysisTask(
            profile,
            "worker_task_analysis",
            new WorkerTurnReference
            {
                SessionId = "test-session",
                RequestId = "test-request",
                TurnNumber = 13
            },
            "Review whether NPC quest details are sufficiently exposed to the player.",
            ["Which commands need additional detail menus?", "Which data should remain hidden from the player?"],
            [new WorkerFileReference { Path = "game_state/npcs/npc_journals.json", Sha256 = "sha-npc-journals" }],
            "2026-06-20T00:30:00Z");

        Assert.Equal(WorkerTaskType.Analysis, task.TaskType);
        Assert.Equal(profile.Role, task.Role);
        Assert.Equal(profile.TimeoutSeconds, task.TimeoutSeconds);
        Assert.Empty(task.AllowedProposalPaths);
        Assert.Null(task.DraftRequest);
        Assert.Contains(task.AcceptanceCriteria, criterion =>
            criterion.Contains("findings", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(task.ForbiddenActions, action =>
            action.Contains("changedFiles", StringComparison.Ordinal));
        Assert.Contains("proposal-only", task.Instructions, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("NPC quest details", task.Instructions, StringComparison.Ordinal);
        Assert.Contains("detail menus", task.Instructions, StringComparison.Ordinal);

        var result = GmWorkerContractValidator.ValidateTaskPacket(task, profile);
        Assert.True(result.IsValid, string.Join(Environment.NewLine, result.Errors));
    }

    [Fact]
    public void BuildAnalysisTask_WithAfterlifeContract_ProducesRealmAwarePacket()
    {
        var profile = GmWorkerBridgeTestFixtures.AnalysisCodexProfile() with
        {
            Permissions = GmWorkerBridgeTestFixtures.AnalysisCodexProfile().Permissions with
            {
                ReadPaths =
                [
                    "game_state/meta/**",
                    "game_state/control/**",
                    "OtherGuides/Afterlife_Contract_Matrix.md"
                ]
            }
        };
        var contract = GmWorkerBridgeTestFixtures.AfterlifeWorkerTask().AfterlifeContract!;

        var task = GmWorkerTaskPacketBuilder.BuildAnalysisTask(
            profile,
            "worker_task_afterlife_analysis",
            new WorkerTurnReference
            {
                SessionId = "test-session",
                RequestId = "test-request",
                TurnNumber = 15
            },
            "Review Chaos Sea guardian state without using Mortal World substitutes.",
            ["Which afterlife surfaces need updates?"],
            [
                new WorkerFileReference { Path = "game_state/meta/soul_state.json", Sha256 = "sha-soul" },
                new WorkerFileReference { Path = "OtherGuides/Afterlife_Contract_Matrix.md", Sha256 = "sha-matrix" }
            ],
            "2026-06-20T00:50:00Z",
            contract);

        Assert.Same(contract, task.AfterlifeContract);
        Assert.Contains(task.AcceptanceCriteria, criterion =>
            criterion.Contains("afterlifeProposal", StringComparison.Ordinal));
        Assert.Contains(task.ForbiddenActions, action =>
            action.Contains("worldStateFlags", StringComparison.Ordinal));
        Assert.Contains("Afterlife_Contract_Matrix.md", task.Instructions, StringComparison.Ordinal);
        Assert.Contains("game_state/meta/guardians.json", task.Instructions, StringComparison.Ordinal);

        var result = GmWorkerContractValidator.ValidateTaskPacket(task, profile);
        Assert.True(result.IsValid, string.Join(Environment.NewLine, result.Errors));
    }

    [Fact]
    public void BuildContentAuthoringTask_ProducesStructuredProposalOnlyPacket()
    {
        var profile = GmWorkerBridgeTestFixtures.InventoryContentCodexProfile();
        var task = GmWorkerTaskPacketBuilder.BuildContentAuthoringTask(
            profile,
            WorkerTaskType.InventoryContent,
            "worker_task_inventory_content",
            new WorkerTurnReference
            {
                SessionId = "test-session",
                RequestId = "test-request",
                TurnNumber = 14
            },
            new WorkerContentAuthoringRequest
            {
                Domain = WorkerAuthoringDomain.Inventory,
                Goal = "Prepare two stealth-themed inventory item proposals for the current manor scene.",
                EntityHints = ["lockpick", "dark cloak"],
                RequiredLinks = ["current location", "player inventory"],
                OutputNotes = ["Do not write canonical item JSON; return proposal details for main-GM review."]
            },
            [new WorkerFileReference { Path = "game_state/world/current_location.json", Sha256 = "sha-location" }],
            "2026-06-20T00:45:00Z");

        Assert.Equal(WorkerTaskType.InventoryContent, task.TaskType);
        Assert.Equal(profile.Role, task.Role);
        Assert.Equal(profile.TimeoutSeconds, task.TimeoutSeconds);
        Assert.Empty(task.AllowedProposalPaths);
        Assert.NotNull(task.AuthoringRequest);
        Assert.Equal(WorkerAuthoringDomain.Inventory, task.AuthoringRequest!.Domain);
        Assert.Contains(task.AcceptanceCriteria, criterion =>
            criterion.Contains("authoringProposal", StringComparison.Ordinal));
        Assert.Contains(task.ForbiddenActions, action =>
            action.Contains("changedFiles", StringComparison.Ordinal));
        Assert.Contains("proposal-only", task.Instructions, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("inventory", task.Instructions, StringComparison.OrdinalIgnoreCase);

        var result = GmWorkerContractValidator.ValidateTaskPacket(task, profile);
        Assert.True(result.IsValid, string.Join(Environment.NewLine, result.Errors));
    }

    [Fact]
    public void ValidateProposal_ContentAuthoringRequiresStructuredAuthoringProposal()
    {
        var profile = GmWorkerBridgeTestFixtures.InventoryContentCodexProfile();
        var task = GmWorkerBridgeTestFixtures.InventoryContentTask();

        var missingAuthoring = GmWorkerBridgeTestFixtures.InventoryContentProposal() with
        {
            AuthoringProposal = null
        };
        var missingResult = GmWorkerContractValidator.ValidateProposal(missingAuthoring, task, profile);

        Assert.False(missingResult.IsValid);
        Assert.Contains(missingResult.Errors, error =>
            error.Contains("authoringProposal", StringComparison.Ordinal));

        var valid = GmWorkerBridgeTestFixtures.InventoryContentProposal();
        var validResult = GmWorkerContractValidator.ValidateProposal(valid, task, profile);

        Assert.True(validResult.IsValid, string.Join(Environment.NewLine, validResult.Errors));
        Assert.Empty(valid.ChangedFiles);
        Assert.Equal(WorkerAuthoringDomain.Inventory, valid.AuthoringProposal!.Domain);
        Assert.NotEmpty(valid.AuthoringProposal.CreatedEntities);
        Assert.NotEmpty(valid.AuthoringProposal.RequiredLinks);
        Assert.NotEmpty(valid.AuthoringProposal.ValidatorRisks);
        Assert.NotEmpty(valid.AuthoringProposal.GmReviewNotes);
    }

    [Fact]
    public void ValidateProposal_InventoryContentRequiresPlayerFacingItemDetailsAndStorageLinks()
    {
        var profile = GmWorkerBridgeTestFixtures.InventoryContentCodexProfile();
        var task = GmWorkerBridgeTestFixtures.InventoryContentTask();
        var proposal = GmWorkerBridgeTestFixtures.InventoryContentProposal();
        var incompleteEntity = proposal.AuthoringProposal!.CreatedEntities[0] with
        {
            RequiredFields =
            [
                new WorkerAuthoredField
                {
                    Name = "slot",
                    Value = "hands"
                }
            ],
            Relationships = ["lockpicking QTE"]
        };
        var incompleteProposal = proposal with
        {
            AuthoringProposal = proposal.AuthoringProposal with
            {
                CreatedEntities = [incompleteEntity],
                RequiredLinks = []
            }
        };

        var result = GmWorkerContractValidator.ValidateProposal(incompleteProposal, task, profile);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, error =>
            error.Contains("inventory", StringComparison.OrdinalIgnoreCase) &&
            error.Contains("description", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(result.Errors, error =>
            error.Contains("inventory", StringComparison.OrdinalIgnoreCase) &&
            error.Contains("storage", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(result.Errors, error =>
            error.Contains("inventory", StringComparison.OrdinalIgnoreCase) &&
            error.Contains("balance", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void ValidateProposal_SkillContentRequiresDetailedExplanationsAndLocalizedScaling()
    {
        var profile = GmWorkerBridgeTestFixtures.SkillContentCodexProfile();
        var task = GmWorkerBridgeTestFixtures.SkillContentTask();
        var valid = GmWorkerBridgeTestFixtures.SkillContentProposal();

        var validResult = GmWorkerContractValidator.ValidateProposal(valid, task, profile);

        Assert.True(validResult.IsValid, string.Join(Environment.NewLine, validResult.Errors));

        var incompleteSkill = valid.AuthoringProposal!.CreatedEntities[0] with
        {
            Summary = "Скрытность +1.",
            RequiredFields =
            [
                new WorkerAuthoredField
                {
                    Name = "structuredBonus",
                    Value = "stealth +1"
                },
                new WorkerAuthoredField
                {
                    Name = "scalingAttribute",
                    Value = "dexterity"
                }
            ],
            Relationships = []
        };
        var incompleteProposal = valid with
        {
            AuthoringProposal = valid.AuthoringProposal with
            {
                CreatedEntities = [incompleteSkill],
                RequiredLinks = []
            }
        };

        var incompleteResult = GmWorkerContractValidator.ValidateProposal(incompleteProposal, task, profile);

        Assert.False(incompleteResult.IsValid);
        Assert.Contains(incompleteResult.Errors, error =>
            error.Contains("skill", StringComparison.OrdinalIgnoreCase) &&
            error.Contains("description", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(incompleteResult.Errors, error =>
            error.Contains("skill", StringComparison.OrdinalIgnoreCase) &&
            error.Contains("localized", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(incompleteResult.Errors, error =>
            error.Contains("skill", StringComparison.OrdinalIgnoreCase) &&
            error.Contains("bonus", StringComparison.OrdinalIgnoreCase) &&
            error.Contains("explanation", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(incompleteResult.Errors, error =>
            error.Contains("skill", StringComparison.OrdinalIgnoreCase) &&
            error.Contains("effect", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void ValidateProposal_NpcContentRequiresLinkedDossierSections()
    {
        var profile = GmWorkerBridgeTestFixtures.NpcContentCodexProfile();
        var task = GmWorkerBridgeTestFixtures.NpcContentTask();
        var valid = GmWorkerBridgeTestFixtures.NpcContentProposal();

        var validResult = GmWorkerContractValidator.ValidateProposal(valid, task, profile);

        Assert.True(validResult.IsValid, string.Join(Environment.NewLine, validResult.Errors));

        var thinNpc = valid.AuthoringProposal!.CreatedEntities[0] with
        {
            Summary = "Дворецкий.",
            RequiredFields =
            [
                new WorkerAuthoredField
                {
                    Name = "description",
                    Value = "Старый дворецкий."
                }
            ],
            Relationships = []
        };
        var thinProposal = valid with
        {
            AuthoringProposal = valid.AuthoringProposal with
            {
                CreatedEntities = [thinNpc],
                RequiredLinks = []
            }
        };

        var thinResult = GmWorkerContractValidator.ValidateProposal(thinProposal, task, profile);

        Assert.False(thinResult.IsValid);
        Assert.Contains(thinResult.Errors, error =>
            error.Contains("npc", StringComparison.OrdinalIgnoreCase) &&
            error.Contains("public", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(thinResult.Errors, error =>
            error.Contains("npc", StringComparison.OrdinalIgnoreCase) &&
            error.Contains("thought", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(thinResult.Errors, error =>
            error.Contains("npc", StringComparison.OrdinalIgnoreCase) &&
            error.Contains("quest", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(thinResult.Errors, error =>
            error.Contains("npc", StringComparison.OrdinalIgnoreCase) &&
            error.Contains("relationship", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(thinResult.Errors, error =>
            error.Contains("npc", StringComparison.OrdinalIgnoreCase) &&
            error.Contains("detail", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task ApplyAsync_RejectsProposalOnlyChangedFilesWithoutWritingCanonicalFile()
    {
        var root = CreateTempRoot();
        try
        {
            var fs = CreateFileSystem(root);
            await fs.WriteFileAtomicAsync("game_state/world/current_location.json", "{\"before\":true}");
            await fs.WriteFileAtomicAsync(
                "worker_proposals/worker_proposal_20260620_0002/game_state/world/current_location.json",
                "{\"after\":true}");
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
            var gate = new GmWorkerApplyGate(fs, () => Task.FromResult<IReadOnlyList<ValidationIssue>>([]));

            var decision = await gate.ApplyAsync(proposal, task, profile);

            Assert.Equal(ApplyGateResult.Rejected, decision.Result);
            Assert.Contains(decision.RejectionReasons, reason =>
                reason.Contains("proposal-only", StringComparison.OrdinalIgnoreCase));
            Assert.Equal("{\"before\":true}", await fs.ReadFileAsync("game_state/world/current_location.json"));
        }
        finally
        {
            CleanupTempRoot(root);
        }
    }

    private static FileSystemManager CreateFileSystem(string root)
    {
        var fs = new FileSystemManager(root, NullLogger<FileSystemManager>.Instance);
        fs.EnsureDirectoryStructure();
        return fs;
    }

    private static string CreateTempRoot()
    {
        var root = Path.Combine(Path.GetTempPath(), "boe-gm-worker-proposal-only-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        return root;
    }

    private static void CleanupTempRoot(string root)
    {
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
