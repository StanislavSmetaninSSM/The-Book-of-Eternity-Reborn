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
        Assert.Empty(task.AllowedProposalPaths);
        Assert.NotNull(task.DraftRequest);
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
        Assert.Empty(task.AllowedProposalPaths);
        Assert.Null(task.DraftRequest);
        Assert.Contains("proposal-only", task.Instructions, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("NPC quest details", task.Instructions, StringComparison.Ordinal);
        Assert.Contains("detail menus", task.Instructions, StringComparison.Ordinal);

        var result = GmWorkerContractValidator.ValidateTaskPacket(task, profile);
        Assert.True(result.IsValid, string.Join(Environment.NewLine, result.Errors));
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
