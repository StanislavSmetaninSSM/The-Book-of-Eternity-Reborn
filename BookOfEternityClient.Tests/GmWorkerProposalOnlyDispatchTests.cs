using BookOfEternityClient.Core;
using BookOfEternityClient.Services;
using BookOfEternityClient.Services.GmWorkers;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed class GmWorkerProposalOnlyDispatchTests
{
    private const string LocationPath = "game_state/world/current_location.json";

    [Fact]
    public async Task DispatchAsync_FakeNarrativeWorker_StoresDraftProposalInInbox()
    {
        var root = CreateTempRoot();
        try
        {
            var fs = CreateFileSystem(root);
            await fs.WriteFileAtomicAsync(LocationPath, "{\"name\":\"Коридор\"}");
            var profile = BuildProfile(root, GmWorkerBridgeTestFixtures.NarrativeDraftCodexProfile(), "fake-narrative-dispatch.ps1", """
                $task = Get-Content -Raw -Path $env:BOE_WORKER_TASK_PATH | ConvertFrom-Json
                $proposal = [ordered]@{
                    schemaVersion = 1
                    proposalId = 'worker_proposal_dispatch_narrative'
                    taskId = $task.taskId
                    workerId = $task.workerId
                    status = 'completed'
                    summary = 'Narrative draft ready.'
                    changedFiles = @()
                    findings = @([ordered]@{
                        kind = 'tone'
                        message = 'Keeps the scene unresolved.'
                    })
                    draftText = 'Draft for main GM.'
                    selfCheck = [ordered]@{
                        scopeReviewed = $true
                        validationExpectedToPass = $true
                        notes = @('proposal-only')
                    }
                    createdAtUtc = '2026-06-20T02:00:00Z'
                }
                $proposal | ConvertTo-Json -Depth 20 | Set-Content -Path $env:BOE_WORKER_PROPOSAL_PATH -Encoding UTF8
                """);
            var service = CreateService(fs);

            var result = await service.DispatchAsync(
                [profile],
                GmWorkerProposalOnlyDispatchRequest.NarrativeDraft(
                    TurnReference(),
                    "Describe the corridor without resolving the action.",
                    "dark fantasy",
                    ["Do not create state changes."],
                    "100-140 words",
                    [LocationPath]));
            var inboxEntry = await new GmWorkerProposalInboxService(fs).ReadAsync("worker_proposal_dispatch_narrative");

            Assert.Equal(GmWorkerProposalOnlyDispatchOutcome.Completed, result.Outcome);
            Assert.Equal("worker_proposal_dispatch_narrative", result.ProposalId);
            Assert.NotNull(inboxEntry);
            Assert.Equal("review-only", inboxEntry!.ReviewMode);
            Assert.Equal("Draft for main GM.", inboxEntry.DraftText);
            Assert.Equal("{\"name\":\"Коридор\"}", await fs.ReadFileAsync(LocationPath));
        }
        finally
        {
            CleanupTempRoot(root);
        }
    }

    [Fact]
    public async Task DispatchAsync_FakeAnalysisWorker_StoresFindingsProposalInInbox()
    {
        var root = CreateTempRoot();
        try
        {
            var fs = CreateFileSystem(root);
            await fs.WriteFileAtomicAsync(LocationPath, "{\"name\":\"Коридор\"}");
            var profile = BuildProfile(root, GmWorkerBridgeTestFixtures.AnalysisCodexProfile(), "fake-analysis-dispatch.ps1", """
                $task = Get-Content -Raw -Path $env:BOE_WORKER_TASK_PATH | ConvertFrom-Json
                $proposal = [ordered]@{
                    schemaVersion = 1
                    proposalId = 'worker_proposal_dispatch_analysis'
                    taskId = $task.taskId
                    workerId = $task.workerId
                    status = 'completed'
                    summary = 'Analysis ready.'
                    changedFiles = @()
                    findings = @([ordered]@{
                        kind = 'risk'
                        message = 'No canonical state should be changed.'
                    })
                    draftText = $null
                    selfCheck = [ordered]@{
                        scopeReviewed = $true
                        validationExpectedToPass = $true
                        notes = @('analysis-only')
                    }
                    createdAtUtc = '2026-06-20T02:05:00Z'
                }
                $proposal | ConvertTo-Json -Depth 20 | Set-Content -Path $env:BOE_WORKER_PROPOSAL_PATH -Encoding UTF8
                """);
            var service = CreateService(fs);

            var result = await service.DispatchAsync(
                [profile],
                GmWorkerProposalOnlyDispatchRequest.Analysis(
                    TurnReference(),
                    "Review whether the scene draft creates hidden state.",
                    ["Does it mutate canonical state?"],
                    [LocationPath]));
            var inboxEntry = await new GmWorkerProposalInboxService(fs).ReadAsync("worker_proposal_dispatch_analysis");

            Assert.Equal(GmWorkerProposalOnlyDispatchOutcome.Completed, result.Outcome);
            Assert.NotNull(inboxEntry);
            Assert.Equal(WorkerTaskType.Analysis, inboxEntry!.TaskType);
            Assert.Equal("review-only", inboxEntry.ReviewMode);
            Assert.Equal(1, inboxEntry.FindingCount);
        }
        finally
        {
            CleanupTempRoot(root);
        }
    }

    [Fact]
    public async Task DispatchAsync_FakeInventoryContentWorker_StoresStructuredAuthoringProposalInInbox()
    {
        var root = CreateTempRoot();
        try
        {
            var fs = CreateFileSystem(root);
            await fs.WriteFileAtomicAsync(LocationPath, "{\"name\":\"Коридор\"}");
            var profile = BuildProfile(root, GmWorkerBridgeTestFixtures.InventoryContentCodexProfile(), "fake-inventory-content-dispatch.ps1", """
                $task = Get-Content -Raw -Path $env:BOE_WORKER_TASK_PATH | ConvertFrom-Json
                $proposal = [ordered]@{
                    schemaVersion = 1
                    proposalId = 'worker_proposal_dispatch_inventory_content'
                    taskId = $task.taskId
                    workerId = $task.workerId
                    status = 'completed'
                    summary = 'Inventory content proposals ready.'
                    changedFiles = @()
                    findings = @([ordered]@{
                        kind = 'validator-risk'
                        message = 'Items must be linked to inventory storage by the main GM.'
                    })
                    draftText = $null
                    authoringProposal = [ordered]@{
                        domain = 'inventory'
                        goal = $task.authoringRequest.goal
                        createdEntities = @([ordered]@{
                            entityType = 'item'
                            entityId = 'item_valmont_lockpick_set'
                            displayName = 'Valmont lockpick set'
                            summary = 'Compact set for quiet simple lock opening.'
                            requiredFields = @([ordered]@{
                                name = 'slot'
                                value = 'hands'
                            })
                            relationships = @('player inventory', 'lockpicking QTE')
                        })
                        updatedEntities = @()
                        requiredLinks = @([ordered]@{
                            source = 'item_valmont_lockpick_set'
                            target = 'player_inventory'
                            reason = 'Main GM must decide whether the item is discovered or already carried.'
                        })
                        validatorRisks = @([ordered]@{
                            code = 'inventory_storage_link_required'
                            message = 'Item proposal is useless unless linked to an inventory container.'
                            mitigation = 'Main GM should add the accepted item through the normal inventory state surface.'
                        })
                        gmReviewNotes = @('Review balance before adding bonuses.')
                    }
                    selfCheck = [ordered]@{
                        scopeReviewed = $true
                        validationExpectedToPass = $true
                        notes = @('proposal-only authoring')
                    }
                    createdAtUtc = '2026-06-20T02:15:00Z'
                }
                $proposal | ConvertTo-Json -Depth 30 | Set-Content -Path $env:BOE_WORKER_PROPOSAL_PATH -Encoding UTF8
                """);
            var service = CreateService(fs);

            var result = await service.DispatchAsync(
                [profile],
                GmWorkerProposalOnlyDispatchRequest.ContentAuthoring(
                    WorkerTaskType.InventoryContent,
                    TurnReference(),
                    new WorkerContentAuthoringRequest
                    {
                        Domain = WorkerAuthoringDomain.Inventory,
                        Goal = "Prepare stealth inventory item proposals.",
                        EntityHints = ["lockpick set"],
                        RequiredLinks = ["player inventory"],
                        OutputNotes = ["Return structured proposal only."]
                    },
                    [LocationPath]));
            var inboxEntry = await new GmWorkerProposalInboxService(fs).ReadAsync("worker_proposal_dispatch_inventory_content");

            Assert.Equal(GmWorkerProposalOnlyDispatchOutcome.Completed, result.Outcome);
            Assert.NotNull(inboxEntry);
            Assert.Equal(WorkerTaskType.InventoryContent, inboxEntry!.TaskType);
            Assert.Equal("review-only", inboxEntry.ReviewMode);
            Assert.Equal(WorkerAuthoringDomain.Inventory, inboxEntry.AuthoringDomain);
            Assert.Equal(1, inboxEntry.AuthoringCreatedEntityCount);
            Assert.Equal(1, inboxEntry.AuthoringRequiredLinkCount);
            Assert.Equal("{\"name\":\"Коридор\"}", await fs.ReadFileAsync(LocationPath));
        }
        finally
        {
            CleanupTempRoot(root);
        }
    }

    [Fact]
    public async Task DispatchAsync_NoMatchingWorker_ReturnsNoWorkerWithoutLaunchingTask()
    {
        var root = CreateTempRoot();
        try
        {
            var fs = CreateFileSystem(root);
            var service = CreateService(fs);

            var result = await service.DispatchAsync(
                [],
                GmWorkerProposalOnlyDispatchRequest.Analysis(TurnReference(), "Review", ["Question"], []));

            Assert.Equal(GmWorkerProposalOnlyDispatchOutcome.SkippedNoWorker, result.Outcome);
            Assert.False(Directory.Exists(fs.ResolvePath(GmWorkerBridgePool.TaskRoot)));
            Assert.False(Directory.Exists(fs.ResolvePath(GmWorkerBridgePool.ProposalInboxRoot)));
        }
        finally
        {
            CleanupTempRoot(root);
        }
    }

    [Fact]
    public async Task DispatchAsync_ProposalOnlyWorkerReturnsChangedFiles_RejectsWithoutCanonicalWrites()
    {
        var root = CreateTempRoot();
        try
        {
            var fs = CreateFileSystem(root);
            await fs.WriteFileAtomicAsync(LocationPath, "{\"name\":\"Коридор\"}");
            var profile = BuildProfile(root, GmWorkerBridgeTestFixtures.NarrativeDraftCodexProfile(), "fake-narrative-invalid-changes.ps1", """
                $task = Get-Content -Raw -Path $env:BOE_WORKER_TASK_PATH | ConvertFrom-Json
                $proposalId = 'worker_proposal_dispatch_invalid_changes'
                $contentRef = 'worker_proposals/' + $proposalId + '/game_state/world/current_location.json'
                $contentPath = Join-Path $env:BOE_WORKER_SESSION_PATH $contentRef
                New-Item -ItemType Directory -Path (Split-Path $contentPath) -Force | Out-Null
                Set-Content -Path $contentPath -Value '{"name":"Changed"}' -Encoding UTF8 -NoNewline
                $proposal = [ordered]@{
                    schemaVersion = 1
                    proposalId = $proposalId
                    taskId = $task.taskId
                    workerId = $task.workerId
                    status = 'completed'
                    summary = 'Invalid narrative changed files.'
                    changedFiles = @([ordered]@{
                        path = 'game_state/world/current_location.json'
                        changeKind = 'replace'
                        contentRef = $contentRef
                    })
                    findings = @()
                    draftText = 'Draft with invalid changed files.'
                    selfCheck = [ordered]@{
                        scopeReviewed = $false
                        validationExpectedToPass = $false
                        notes = @('invalid')
                    }
                    createdAtUtc = '2026-06-20T02:10:00Z'
                }
                $proposal | ConvertTo-Json -Depth 20 | Set-Content -Path $env:BOE_WORKER_PROPOSAL_PATH -Encoding UTF8
                """);
            var service = CreateService(fs);

            var result = await service.DispatchAsync(
                [profile],
                GmWorkerProposalOnlyDispatchRequest.NarrativeDraft(
                    TurnReference(),
                    "Draft",
                    "dark fantasy",
                    [],
                    "short",
                    [LocationPath]));

            Assert.Equal(GmWorkerProposalOnlyDispatchOutcome.ProposalRejected, result.Outcome);
            Assert.Contains("proposal-only", result.FallbackReason, StringComparison.OrdinalIgnoreCase);
            Assert.Equal("{\"name\":\"Коридор\"}", await fs.ReadFileAsync(LocationPath));
        }
        finally
        {
            CleanupTempRoot(root);
        }
    }

    private static GmWorkerProposalOnlyDispatchService CreateService(FileSystemManager fs)
    {
        var audit = new GmWorkerAuditLog(fs);
        return new GmWorkerProposalOnlyDispatchService(
            fs,
            new GmWorkerBridgePool(fs, new GmWorkerProposalStore(fs), audit),
            audit);
    }

    private static WorkerBridgeProfile BuildProfile(
        string root,
        WorkerBridgeProfile profile,
        string fileName,
        string script)
    {
        var scriptPath = Path.Combine(root, fileName);
        File.WriteAllText(scriptPath, script);
        return profile with
        {
            LaunchCommand = $"powershell.exe -NoLogo -NoProfile -ExecutionPolicy Bypass -File \"{scriptPath}\"",
            TimeoutSeconds = 10
        };
    }

    private static WorkerTurnReference TurnReference() => new()
    {
        SessionId = "test-session",
        RequestId = "manual-worker-dispatch",
        TurnNumber = 12
    };

    private static FileSystemManager CreateFileSystem(string root)
    {
        var fs = new FileSystemManager(root, NullLogger<FileSystemManager>.Instance);
        fs.EnsureDirectoryStructure();
        return fs;
    }

    private static string CreateTempRoot()
    {
        var root = Path.Combine(Path.GetTempPath(), "boe-gm-worker-dispatch-" + Guid.NewGuid().ToString("N"));
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
