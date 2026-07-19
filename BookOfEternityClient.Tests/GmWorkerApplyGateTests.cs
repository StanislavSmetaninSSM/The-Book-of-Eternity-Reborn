using BookOfEternityClient.Core;
using BookOfEternityClient.Services;
using BookOfEternityClient.Services.GmWorkers;
using Microsoft.Extensions.Logging.Abstractions;
using System.Text.Json.Nodes;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed class GmWorkerApplyGateTests
{
    [Fact]
    public async Task ApplyAsync_AcceptsAllowedProposalAndWritesCanonicalFile()
    {
        var root = CreateTempRoot();
        try
        {
            var fs = CreateFileSystem(root);
            var (profile, task, proposal) = await PrepareAllowedRepairAsync(fs);
            var gate = new GmWorkerApplyGate(fs, () => Task.FromResult<IReadOnlyList<ValidationIssue>>([]));

            var decision = await gate.ApplyAsync(proposal, task, profile);

            Assert.Equal(ApplyGateResult.Accepted, decision.Result);
            Assert.True(decision.ScopeCheck.Passed);
            Assert.True(decision.ValidationCheck.Passed);
            Assert.Contains("game_state/world/weather.json", decision.AppliedFiles);
            Assert.Equal("{\"after\":true}", await fs.ReadFileAsync("game_state/world/weather.json"));
        }
        finally
        {
            CleanupTempRoot(root);
        }
    }

    [Fact]
    public async Task ApplyAsync_RejectsProposalOutsideTaskAllowedPathsWithoutWritingCanonicalFile()
    {
        var root = CreateTempRoot();
        try
        {
            var fs = CreateFileSystem(root);
            var (profile, task, proposal) = await PrepareAllowedRepairAsync(fs);
            proposal = proposal with
            {
                ChangedFiles =
                [
                    new WorkerChangedFile
                    {
                        Path = "game_state/player/transformation.json",
                        ChangeKind = WorkerFileChangeKind.Replace,
                        ContentRef = "worker_proposals/worker_proposal_20260620_0001/game_state/player/transformation.json"
                    }
                ]
            };
            await fs.WriteFileAtomicAsync(
                "worker_proposals/worker_proposal_20260620_0001/game_state/player/transformation.json",
                "{\"bad\":true}");
            var gate = new GmWorkerApplyGate(fs, () => Task.FromResult<IReadOnlyList<ValidationIssue>>([]));

            var decision = await gate.ApplyAsync(proposal, task, profile);

            Assert.Equal(ApplyGateResult.Rejected, decision.Result);
            Assert.False(decision.ScopeCheck.Passed);
            Assert.Contains(decision.RejectionReasons, reason =>
                reason.Contains("outside task allowedProposalPaths", StringComparison.OrdinalIgnoreCase));
            Assert.Equal("{\"before\":true}", await fs.ReadFileAsync("game_state/world/weather.json"));
            Assert.False(fs.FileExists("game_state/player/transformation.json"));
        }
        finally
        {
            CleanupTempRoot(root);
        }
    }

    [Fact]
    public async Task ApplyAsync_RollsBackAllowedProposalWhenValidationFails()
    {
        var root = CreateTempRoot();
        try
        {
            var fs = CreateFileSystem(root);
            var (profile, task, proposal) = await PrepareAllowedRepairAsync(fs);
            var validationIssue = new ValidationIssue(
                "game_state/world/weather.json",
                IssueSeverity.Error,
                "Weather is still invalid.",
                code: "weather_still_invalid");
            var gate = new GmWorkerApplyGate(
                fs,
                () => Task.FromResult<IReadOnlyList<ValidationIssue>>([validationIssue]));

            var decision = await gate.ApplyAsync(proposal, task, profile);

            Assert.Equal(ApplyGateResult.ValidationFailed, decision.Result);
            Assert.True(decision.ScopeCheck.Passed);
            Assert.True(decision.ValidationCheck.Required);
            Assert.False(decision.ValidationCheck.Passed);
            Assert.Equal(1, decision.ValidationCheck.IssueCount);
            Assert.Equal("{\"before\":true}", await fs.ReadFileAsync("game_state/world/weather.json"));
        }
        finally
        {
            CleanupTempRoot(root);
        }
    }

    [Fact]
    public async Task ApplyAsync_WhenAuditLogProvided_RecordsApplyDecision()
    {
        var root = CreateTempRoot();
        try
        {
            var fs = CreateFileSystem(root);
            var (profile, task, proposal) = await PrepareAllowedRepairAsync(fs);
            var audit = new GmWorkerAuditLog(fs);
            var gate = new GmWorkerApplyGate(
                fs,
                () => Task.FromResult<IReadOnlyList<ValidationIssue>>([]),
                audit);

            var decision = await gate.ApplyAsync(proposal, task, profile);
            var events = await audit.ReadEventsAsync();

            Assert.Equal(ApplyGateResult.Accepted, decision.Result);
            var applyEvent = Assert.Single(events);
            Assert.Equal("proposal-applied", applyEvent.EventType);
            Assert.Equal(proposal.ProposalId, applyEvent.ProposalId);
        }
        finally
        {
            CleanupTempRoot(root);
        }
    }

    [Fact]
    public async Task ApplyAsync_ActorMaterializationRepairChangingProtectedActorData_IsRejected()
    {
        var root = CreateTempRoot();
        try
        {
            var fs = CreateFileSystem(root);
            var (profile, task, proposal) = await PrepareActorMaterializationRepairAsync(
                fs,
                changeProtectedData: true);
            var gate = new GmWorkerApplyGate(fs, () => Task.FromResult<IReadOnlyList<ValidationIssue>>([]));

            var decision = await gate.ApplyAsync(proposal, task, profile);

            Assert.Equal(ApplyGateResult.Rejected, decision.Result);
            Assert.Contains(decision.RejectionReasons, reason =>
                reason.Contains("protected actor data", StringComparison.OrdinalIgnoreCase));
            var current = JsonNode.Parse((await fs.ReadFileAsync("game_state/npcs/npc_core.json"))!)!.AsObject();
            Assert.Equal(
                "Сдержанная и наблюдательная.",
                current["NPCsInScene"]![0]!["personality"]!["summary"]!.GetValue<string>());
        }
        finally
        {
            CleanupTempRoot(root);
        }
    }

    [Fact]
    public async Task ApplyAsync_ActorMaterializationRepairChangingOnlyNamedSection_IsAccepted()
    {
        var root = CreateTempRoot();
        try
        {
            var fs = CreateFileSystem(root);
            var (profile, task, proposal) = await PrepareActorMaterializationRepairAsync(
                fs,
                changeProtectedData: false);
            var gate = new GmWorkerApplyGate(fs, () => Task.FromResult<IReadOnlyList<ValidationIssue>>([]));

            var decision = await gate.ApplyAsync(proposal, task, profile);

            Assert.Equal(ApplyGateResult.Accepted, decision.Result);
            var current = JsonNode.Parse((await fs.ReadFileAsync("game_state/npcs/npc_core.json"))!)!.AsObject();
            Assert.Equal(
                "empty_by_design",
                current["NPCsInScene"]![0]!["materialization"]!["sections"]!["inventory"]!["state"]!
                    .GetValue<string>());
            Assert.Equal(
                "Сдержанная и наблюдательная.",
                current["NPCsInScene"]![0]!["personality"]!["summary"]!.GetValue<string>());
        }
        finally
        {
            CleanupTempRoot(root);
        }
    }

    private static async Task<(WorkerBridgeProfile Profile, WorkerTaskPacket Task, WorkerProposal Proposal)> PrepareAllowedRepairAsync(
        FileSystemManager fs)
    {
        var profile = GmWorkerBridgeTestFixtures.ValidationRepairCodexProfile();
        var task = GmWorkerBridgeTestFixtures.ValidationRepairTask();
        var proposal = GmWorkerBridgeTestFixtures.ValidationRepairProposal() with
        {
            ChangedFiles =
            [
                new WorkerChangedFile
                {
                    Path = "game_state/world/weather.json",
                    ChangeKind = WorkerFileChangeKind.Replace,
                    BeforeSha256 = "example",
                    AfterSha256 = "example-after",
                    ContentRef = "worker_proposals/worker_proposal_20260620_0001/game_state/world/weather.json"
                }
            ]
        };

        await fs.WriteFileAtomicAsync("game_state/world/weather.json", "{\"before\":true}");
        await fs.WriteFileAtomicAsync(
            "worker_proposals/worker_proposal_20260620_0001/game_state/world/weather.json",
            "{\"after\":true}");

        return (profile, task, proposal);
    }

    private static async Task<(WorkerBridgeProfile Profile, WorkerTaskPacket Task, WorkerProposal Proposal)>
        PrepareActorMaterializationRepairAsync(
            FileSystemManager fs,
            bool changeProtectedData)
    {
        const string path = "game_state/npcs/npc_core.json";
        const string contentRef =
            "worker_proposals/worker_proposal_actor_materialization/game_state/npcs/npc_core.json";
        var baseline = new JsonObject
        {
            ["UpdateNPCs"] = new JsonArray(),
            ["NPCsInScene"] = new JsonArray(new JsonObject
            {
                ["NPCId"] = "npc_repair_target",
                ["name"] = "Ирен Соль",
                ["personality"] = new JsonObject
                {
                    ["summary"] = "Сдержанная и наблюдательная."
                },
                ["inventory"] = new JsonArray(),
                ["materialization"] = new JsonObject
                {
                    ["schemaVersion"] = 1,
                    ["materializationId"] = "mat_npc_repair_target_turn_12",
                    ["actorType"] = "mortal_npc",
                    ["actorId"] = "npc_repair_target",
                    ["materializedAtTurn"] = 12,
                    ["state"] = "complete",
                    ["capabilities"] = new JsonObject
                    {
                        ["canFight"] = false,
                        ["canTeach"] = false,
                        ["canTrade"] = false,
                        ["ownsItems"] = false
                    },
                    ["sections"] = new JsonObject
                    {
                        ["skills"] = EmptySection("Боевых навыков пока нет."),
                        ["fateCards"] = EmptySection("Карты Судьбы пока не открыты."),
                        ["personalQuests"] = EmptySection("Личных просьб пока нет."),
                        ["relationships"] = EmptySection("Устойчивых отношений пока нет.")
                    }
                }
            })
        };
        var proposed = baseline.DeepClone().AsObject();
        proposed["NPCsInScene"]![0]!["materialization"]!["sections"]!["inventory"] =
            EmptySection("У персонажа пока нет вещей.");
        if (changeProtectedData)
        {
            proposed["NPCsInScene"]![0]!["personality"]!["summary"] =
                "Полностью переписанная личность.";
        }

        await fs.WriteFileAtomicAsync(path, baseline.ToJsonString());
        await fs.WriteFileAtomicAsync(contentRef, proposed.ToJsonString());

        var profile = GmWorkerBridgeTestFixtures.ValidationRepairCodexProfile();
        var task = GmWorkerBridgeTestFixtures.ValidationRepairTask() with
        {
            ValidationIssues =
            [
                new WorkerValidationIssue
                {
                    Code = "actor_materialization_section_missing",
                    Path = $"{path}.NPCsInScene[0].materialization.sections.inventory",
                    Message = "Первичная материализация не объясняет секцию inventory.",
                    Actor = "mortal_npc:npc_repair_target",
                    Section = "inventory",
                    Expected = "populated or empty_by_design with reason",
                    Actual = "missing"
                }
            ],
            ContextFiles = [new WorkerFileReference { Path = path, Sha256 = "baseline" }],
            AllowedProposalPaths = [path]
        };
        var proposal = GmWorkerBridgeTestFixtures.ValidationRepairProposal() with
        {
            ChangedFiles =
            [
                new WorkerChangedFile
                {
                    Path = path,
                    ChangeKind = WorkerFileChangeKind.Replace,
                    BeforeSha256 = "baseline",
                    AfterSha256 = "proposal",
                    ContentRef = contentRef
                }
            ]
        };
        return (profile, task, proposal);

        static JsonObject EmptySection(string reason) => new()
        {
            ["state"] = "empty_by_design",
            ["reason"] = reason
        };
    }

    private static FileSystemManager CreateFileSystem(string root)
    {
        var fs = new FileSystemManager(root, NullLogger<FileSystemManager>.Instance);
        fs.EnsureDirectoryStructure();
        return fs;
    }

    private static string CreateTempRoot()
    {
        var root = Path.Combine(Path.GetTempPath(), "boe-gm-worker-gate-" + Guid.NewGuid().ToString("N"));
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
