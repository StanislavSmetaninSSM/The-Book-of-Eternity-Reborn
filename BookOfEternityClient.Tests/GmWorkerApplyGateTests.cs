using BookOfEternityClient.Core;
using BookOfEternityClient.Services;
using BookOfEternityClient.Services.GmWorkers;
using Microsoft.Extensions.Logging.Abstractions;
using System.Security.Cryptography;
using System.Text;
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

    [Theory]
    [InlineData(WorkerProposalStatus.Failed)]
    [InlineData(WorkerProposalStatus.TimedOut)]
    [InlineData(WorkerProposalStatus.Rejected)]
    public async Task ApplyAsync_NonCompletedProposalIsNeverApplyable(WorkerProposalStatus status)
    {
        var root = CreateTempRoot();
        try
        {
            var fs = CreateFileSystem(root);
            var (profile, task, proposal) = await PrepareAllowedRepairAsync(fs);
            proposal = proposal with
            {
                Status = status,
                ChangedFiles = []
            };
            var gate = new GmWorkerApplyGate(fs, () => Task.FromResult<IReadOnlyList<ValidationIssue>>([]));

            var decision = await gate.ApplyAsync(proposal, task, profile);

            Assert.Equal(ApplyGateResult.Rejected, decision.Result);
            Assert.Contains(decision.RejectionReasons, reason =>
                reason.Contains("completed", StringComparison.OrdinalIgnoreCase));
            Assert.Equal("{\"before\":true}", await fs.ReadFileAsync("game_state/world/weather.json"));
        }
        finally
        {
            CleanupTempRoot(root);
        }
    }

    [Fact]
    public async Task ApplyAsync_StaleTaskContextWithBomOnlyByteDifference_IsRejectedWithoutChangingCanonicalFile()
    {
        var root = CreateTempRoot();
        try
        {
            const string path = "game_state/world/weather.json";
            const string baseline = "{\"before\":true}";
            const string proposedContent = "{\"after\":true}";
            var fs = CreateFileSystem(root);
            var (profile, task, proposal) = await PrepareAllowedRepairAsync(fs);
            var taskContextBytes = Encoding.UTF8.GetBytes(baseline);
            byte[] utf8Bom = [0xef, 0xbb, 0xbf];
            var staleCanonicalBytes = new byte[utf8Bom.Length + taskContextBytes.Length];
            utf8Bom.CopyTo(staleCanonicalBytes, 0);
            taskContextBytes.CopyTo(staleCanonicalBytes, utf8Bom.Length);
            await File.WriteAllBytesAsync(fs.ResolvePath(path), staleCanonicalBytes);

            var taskContextSha = ComputeSha256(taskContextBytes);
            task = task with
            {
                ContextFiles = [new WorkerFileReference { Path = path, Sha256 = taskContextSha }]
            };
            proposal = proposal with
            {
                ChangedFiles =
                [
                    proposal.ChangedFiles[0] with
                    {
                        BeforeSha256 = taskContextSha,
                        AfterSha256 = ComputeSha256(proposedContent)
                    }
                ]
            };
            var gate = new GmWorkerApplyGate(fs, () => Task.FromResult<IReadOnlyList<ValidationIssue>>([]));

            var decision = await gate.ApplyAsync(proposal, task, profile);
            var actualCanonicalBytes = await File.ReadAllBytesAsync(fs.ResolvePath(path));
            var canonicalBytesPreserved = staleCanonicalBytes.AsSpan().SequenceEqual(actualCanonicalBytes);

            Assert.True(
                decision.Result == ApplyGateResult.Rejected && canonicalBytesPreserved,
                $"Expected exact-byte context SHA mismatch to reject without changing canonical bytes; " +
                $"result={decision.Result}, canonicalBytesPreserved={canonicalBytesPreserved}.");
        }
        finally
        {
            CleanupTempRoot(root);
        }
    }

    [Fact]
    public async Task ApplyAsync_ContentRefHashChangedAfterProposalWasCreated_IsRejectedWithoutChangingCanonicalFile()
    {
        var root = CreateTempRoot();
        try
        {
            const string path = "game_state/world/weather.json";
            const string baseline = "{\"before\":true}";
            const string mutatedProposalContent = "{\"after\":\"mutated\"}";
            var fs = CreateFileSystem(root);
            var (profile, task, proposal) = await PrepareAllowedRepairAsync(fs);
            var canonicalSha = ComputeFileSha256(fs, path);
            var expectedProposalSha = ComputeFileSha256(fs, proposal.ChangedFiles[0].ContentRef!);
            task = task with
            {
                ContextFiles = [new WorkerFileReference { Path = path, Sha256 = canonicalSha }]
            };
            proposal = proposal with
            {
                ChangedFiles =
                [
                    proposal.ChangedFiles[0] with
                    {
                        BeforeSha256 = canonicalSha,
                        AfterSha256 = expectedProposalSha
                    }
                ]
            };
            await fs.WriteFileAtomicAsync(proposal.ChangedFiles[0].ContentRef!, mutatedProposalContent);
            var gate = new GmWorkerApplyGate(fs, () => Task.FromResult<IReadOnlyList<ValidationIssue>>([]));

            var decision = await gate.ApplyAsync(proposal, task, profile);
            var canonicalContent = await fs.ReadFileAsync(path);

            Assert.True(
                decision.Result == ApplyGateResult.Rejected && canonicalContent == baseline,
                $"Expected mutated contentRef hash to reject without changing canonical content; " +
                $"result={decision.Result}, canonicalContent={canonicalContent}.");
        }
        finally
        {
            CleanupTempRoot(root);
        }
    }

    [Fact]
    public async Task ApplyAsync_ValidationFailureDoesNotRollbackOverConcurrentCanonicalMutation()
    {
        var root = CreateTempRoot();
        try
        {
            const string path = "game_state/world/weather.json";
            const string concurrentMutation = "{\"concurrent\":true}";
            var fs = CreateFileSystem(root);
            var (profile, task, proposal) = await PrepareAllowedRepairAsync(fs);
            var canonicalSha = ComputeFileSha256(fs, path);
            var proposalSha = ComputeFileSha256(fs, proposal.ChangedFiles[0].ContentRef!);
            task = task with
            {
                ContextFiles = [new WorkerFileReference { Path = path, Sha256 = canonicalSha }]
            };
            proposal = proposal with
            {
                ChangedFiles =
                [
                    proposal.ChangedFiles[0] with
                    {
                        BeforeSha256 = canonicalSha,
                        AfterSha256 = proposalSha
                    }
                ]
            };
            var validationIssue = new ValidationIssue(
                path,
                IssueSeverity.Error,
                "Weather is still invalid.",
                code: "weather_still_invalid");
            var gate = new GmWorkerApplyGate(
                fs,
                async () =>
                {
                    await fs.WriteFileAtomicAsync(path, concurrentMutation);
                    IReadOnlyList<ValidationIssue> issues = [validationIssue];
                    return issues;
                });

            var decision = await gate.ApplyAsync(proposal, task, profile);

            Assert.Equal(ApplyGateResult.ValidationFailed, decision.Result);
            Assert.Equal(concurrentMutation, await fs.ReadFileAsync(path));
        }
        finally
        {
            CleanupTempRoot(root);
        }
    }

    [Fact]
    public async Task ApplyAsync_CanonicalMutationAfterContextReadWinsOverWorkerProposal()
    {
        var root = CreateTempRoot();
        try
        {
            const string path = "game_state/world/weather.json";
            var concurrentBytes = Encoding.UTF8.GetBytes("{\"concurrent\":true}");
            var fs = CreateFileSystem(root);
            var (profile, task, proposal) = await PrepareAllowedRepairAsync(fs);
            var lockPath = Path.Combine(fs.GameSessionPath, ".locks", "canonical-write.lock");
            Directory.CreateDirectory(Path.GetDirectoryName(lockPath)!);
            await using var canonicalLock = new FileStream(
                lockPath,
                FileMode.OpenOrCreate,
                FileAccess.ReadWrite,
                FileShare.None);
            var gate = new GmWorkerApplyGate(fs, () => Task.FromResult<IReadOnlyList<ValidationIssue>>([]));

            var applyTask = gate.ApplyAsync(proposal, task, profile);
            await Task.Delay(200);
            await File.WriteAllBytesAsync(fs.ResolvePath(path), concurrentBytes);
            await canonicalLock.DisposeAsync();
            var decision = await applyTask;

            Assert.Equal(ApplyGateResult.Rejected, decision.Result);
            Assert.Equal(concurrentBytes, await fs.ReadFileBytesAsync(path));
            Assert.Contains(decision.RejectionReasons, reason =>
                reason.Contains("changed concurrently", StringComparison.OrdinalIgnoreCase));
        }
        finally
        {
            CleanupTempRoot(root);
        }
    }

    [Fact]
    public async Task ApplyAsync_RollbackCasDoesNotOverwriteMutationAfterOwnershipRead()
    {
        var root = CreateTempRoot();
        try
        {
            const string path = "game_state/world/weather.json";
            var concurrentBytes = Encoding.UTF8.GetBytes("{\"concurrent\":\"during-rollback\"}");
            var fs = CreateFileSystem(root);
            var (profile, task, proposal) = await PrepareAllowedRepairAsync(fs);
            var validationIssue = new ValidationIssue(
                path,
                IssueSeverity.Error,
                "Weather is still invalid.",
                code: "weather_still_invalid");
            var lockPath = Path.Combine(fs.GameSessionPath, ".locks", "canonical-write.lock");
            FileStream? rollbackBarrier = null;
            Task? concurrentWrite = null;
            var gate = new GmWorkerApplyGate(
                fs,
                () =>
                {
                    rollbackBarrier = new FileStream(
                        lockPath,
                        FileMode.OpenOrCreate,
                        FileAccess.ReadWrite,
                        FileShare.None);
                    concurrentWrite = Task.Run(async () =>
                    {
                        await Task.Delay(200);
                        await File.WriteAllBytesAsync(fs.ResolvePath(path), concurrentBytes);
                        await rollbackBarrier.DisposeAsync();
                    });
                    return Task.FromResult<IReadOnlyList<ValidationIssue>>([validationIssue]);
                });

            var decision = await gate.ApplyAsync(proposal, task, profile);
            if (concurrentWrite != null)
                await concurrentWrite;

            Assert.Equal(ApplyGateResult.ValidationFailed, decision.Result);
            Assert.Equal(concurrentBytes, await fs.ReadFileBytesAsync(path));
            Assert.Contains(decision.RejectionReasons, reason =>
                reason.Contains("rollback conflict", StringComparison.OrdinalIgnoreCase));
        }
        finally
        {
            CleanupTempRoot(root);
        }
    }

    [Fact]
    public async Task ApplyAsync_UnscopedCurrentAuthorityIssueCannotAuthorizeUnrelatedActorChange()
    {
        var root = CreateTempRoot();
        try
        {
            const string path = "game_state/npcs/npc_core.json";
            const string contentRef =
                "worker_proposals/worker_proposal_unscoped_authority/game_state/npcs/npc_core.json";
            var fs = CreateFileSystem(root);
            var baselineRoot = new JsonObject
            {
                ["UpdateNPCs"] = new JsonArray(),
                ["NPCsInScene"] = new JsonArray(
                    new JsonObject
                    {
                        ["NPCId"] = "npc_authority_target",
                        ["id"] = "npc_conflicting_alias",
                        ["name"] = "Authority target"
                    },
                    new JsonObject
                    {
                        ["NPCId"] = "npc_unrelated",
                        ["name"] = "Unrelated actor",
                        ["personality"] = "unchanged"
                    })
            };
            var proposedRoot = baselineRoot.DeepClone().AsObject();
            proposedRoot["NPCsInScene"]![0]!.AsObject().Remove("id");
            proposedRoot["NPCsInScene"]![1]!["personality"] = "worker rewrite";
            var baseline = baselineRoot.ToJsonString();
            var proposedContent = proposedRoot.ToJsonString();
            await fs.WriteFileAtomicAsync(path, baseline);
            await fs.WriteFileAtomicAsync(contentRef, proposedContent);

            var canonicalSha = ComputeFileSha256(fs, path);
            var proposalSha = ComputeFileSha256(fs, contentRef);
            var profile = GmWorkerBridgeTestFixtures.ValidationRepairCodexProfile();
            var task = GmWorkerBridgeTestFixtures.ValidationRepairTask() with
            {
                ValidationIssues =
                [
                    new WorkerValidationIssue
                    {
                        Code = "actor_materialization_current_authority_unusable",
                        Path = path,
                        Message = "Current actor authority aliases conflict.",
                        Section = "ActorMaterialization"
                    }
                ],
                ContextFiles = [new WorkerFileReference { Path = path, Sha256 = canonicalSha }],
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
                        BeforeSha256 = canonicalSha,
                        AfterSha256 = proposalSha,
                        ContentRef = contentRef
                    }
                ]
            };
            var gate = new GmWorkerApplyGate(fs, () => Task.FromResult<IReadOnlyList<ValidationIssue>>([]));

            var decision = await gate.ApplyAsync(proposal, task, profile);
            var canonicalContent = await fs.ReadFileAsync(path);

            Assert.True(
                decision.Result == ApplyGateResult.Rejected && canonicalContent == baseline,
                $"Expected unscoped authority issue to reject unrelated actor changes without writing; " +
                $"result={decision.Result}, canonicalPreserved={canonicalContent == baseline}.");
        }
        finally
        {
            CleanupTempRoot(root);
        }
    }

    [Fact]
    public async Task ApplyAsync_UnscopedAfterlifeBindingAuthorityIssueCannotAuthorizeRootRewrite()
    {
        var root = CreateTempRoot();
        try
        {
            const string path = "game_state/meta/shining_abode_state.json";
            const string contentRef =
                "worker_proposals/worker_proposal_afterlife_unscoped_authority/game_state/meta/shining_abode_state.json";
            const string baseline = "{\"schemaVersion\":1,\"factions\":[{\"factionId\":\"kept\"}]}";
            const string proposedContent = "{\"schemaVersion\":1,\"factions\":[]}";
            var fs = CreateFileSystem(root);
            await fs.WriteFileAtomicAsync(path, baseline);
            await fs.WriteFileAtomicAsync(contentRef, proposedContent);
            var canonicalSha = ComputeFileSha256(fs, path);
            var proposalSha = ComputeFileSha256(fs, contentRef);
            var profile = GmWorkerBridgeTestFixtures.ValidationRepairCodexProfile();
            var task = GmWorkerBridgeTestFixtures.ValidationRepairTask() with
            {
                ValidationIssues =
                [
                    new WorkerValidationIssue
                    {
                        Code = "afterlife_actor_binding_current_authority_unusable",
                        Path = path,
                        Message = "Current afterlife actor authority is malformed.",
                        Section = "ActorMaterialization"
                    }
                ],
                ContextFiles = [new WorkerFileReference { Path = path, Sha256 = canonicalSha }],
                AfterlifeContract = new WorkerAfterlifeTaskContract
                {
                    RealmGate = WorkerAfterlifeRealmGate.ShiningAbode,
                    CurrentRealm = "Shining Abode",
                    AllowedAfterlifeSurfaces = [path],
                    RequiredReceipts = ["No receipt required."],
                    RequiredReports = ["Apply-gate decision."],
                    ForbiddenMortalSubstitutes = ["Mortal state"]
                },
                AllowedProposalPaths = [path]
            };
            var proposal = GmWorkerBridgeTestFixtures.ValidationRepairProposal() with
            {
                ProposalId = "worker_proposal_afterlife_unscoped_authority",
                ChangedFiles =
                [
                    new WorkerChangedFile
                    {
                        Path = path,
                        ChangeKind = WorkerFileChangeKind.Replace,
                        BeforeSha256 = canonicalSha,
                        AfterSha256 = proposalSha,
                        ContentRef = contentRef
                    }
                ]
            };
            var gate = new GmWorkerApplyGate(fs, () => Task.FromResult<IReadOnlyList<ValidationIssue>>([]));

            var decision = await gate.ApplyAsync(proposal, task, profile);

            Assert.Equal(ApplyGateResult.Rejected, decision.Result);
            Assert.Equal(baseline, await fs.ReadFileAsync(path));
            Assert.Contains(decision.RejectionReasons, reason =>
                reason.Contains("cannot safely scope", StringComparison.OrdinalIgnoreCase));
        }
        finally
        {
            CleanupTempRoot(root);
        }
    }

    [Fact]
    public async Task ApplyAsync_AfterlifeTaskCannotChangeFileOutsideAllowedAfterlifeSurfaces()
    {
        var root = CreateTempRoot();
        try
        {
            const string allowedAfterlifePath = "game_state/meta/afterlife_entity_profiles.json";
            const string outsidePath = "game_state/world/weather.json";
            const string contentRef =
                "worker_proposals/worker_proposal_afterlife_escape/game_state/world/weather.json";
            const string baseline = "{\"mortal\":\"unchanged\"}";
            const string proposedContent = "{\"mortal\":\"worker rewrite\"}";
            var fs = CreateFileSystem(root);
            await fs.WriteFileAtomicAsync(outsidePath, baseline);
            await fs.WriteFileAtomicAsync(contentRef, proposedContent);

            var canonicalSha = ComputeFileSha256(fs, outsidePath);
            var proposalSha = ComputeFileSha256(fs, contentRef);
            var profile = GmWorkerBridgeTestFixtures.ValidationRepairCodexProfile();
            var task = GmWorkerBridgeTestFixtures.ValidationRepairTask() with
            {
                ValidationIssues =
                [
                    new WorkerValidationIssue
                    {
                        Code = "afterlife_actor_materialization_profile_missing",
                        Path = "game_state/meta/guardian_abode_residents.json.entries[0]",
                        Message = "Resident profile is missing.",
                        Actor = "resident:resident_scope_target",
                        Section = "ActorMaterialization"
                    }
                ],
                ContextFiles = [new WorkerFileReference { Path = outsidePath, Sha256 = canonicalSha }],
                AfterlifeContract = new WorkerAfterlifeTaskContract
                {
                    RealmGate = WorkerAfterlifeRealmGate.ChaosSea,
                    CurrentRealm = "Chaos Sea",
                    AllowedAfterlifeSurfaces = [allowedAfterlifePath],
                    RequiredReceipts = ["No receipt is required for this bounded repair."],
                    RequiredReports = ["Apply-gate validation decision."],
                    ForbiddenMortalSubstitutes = ["Mortal World state"]
                },
                AllowedProposalPaths = [outsidePath]
            };
            var proposal = GmWorkerBridgeTestFixtures.ValidationRepairProposal() with
            {
                ChangedFiles =
                [
                    new WorkerChangedFile
                    {
                        Path = outsidePath,
                        ChangeKind = WorkerFileChangeKind.Replace,
                        BeforeSha256 = canonicalSha,
                        AfterSha256 = proposalSha,
                        ContentRef = contentRef
                    }
                ]
            };
            var gate = new GmWorkerApplyGate(fs, () => Task.FromResult<IReadOnlyList<ValidationIssue>>([]));

            var decision = await gate.ApplyAsync(proposal, task, profile);
            var canonicalContent = await fs.ReadFileAsync(outsidePath);

            Assert.True(
                decision.Result == ApplyGateResult.Rejected && canonicalContent == baseline,
                $"Expected afterlife task write outside allowedAfterlifeSurfaces to reject without writing; " +
                $"result={decision.Result}, canonicalPreserved={canonicalContent == baseline}.");
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
    public async Task ApplyAsync_AcceptedCanonicalCommitSurvivesAuditPublicationFailure()
    {
        var root = CreateTempRoot();
        try
        {
            var fs = CreateFileSystem(root);
            var (profile, task, proposal) = await PrepareAllowedRepairAsync(fs);
            Directory.CreateDirectory(fs.ResolvePath(GmWorkerAuditLog.AuditLogPath));
            var gate = new GmWorkerApplyGate(
                fs,
                () => Task.FromResult<IReadOnlyList<ValidationIssue>>([]),
                new GmWorkerAuditLog(fs));

            var decision = await gate.ApplyAsync(proposal, task, profile);

            Assert.Equal(ApplyGateResult.Accepted, decision.Result);
            Assert.Equal("{\"after\":true}", await fs.ReadFileAsync("game_state/world/weather.json"));
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

    [Fact]
    public async Task ApplyAsync_ActorMaterializationScalarRepairChangingSiblingEnvelopeData_IsRejected()
    {
        var root = CreateTempRoot();
        try
        {
            var fs = CreateFileSystem(root);
            var (profile, task, proposal) = await PrepareActorMaterializationScalarRepairAsync(
                fs,
                changeSiblingData: true);
            var gate = new GmWorkerApplyGate(fs, () => Task.FromResult<IReadOnlyList<ValidationIssue>>([]));

            var decision = await gate.ApplyAsync(proposal, task, profile);

            Assert.Equal(ApplyGateResult.Rejected, decision.Result);
            Assert.Contains(decision.RejectionReasons, reason =>
                reason.Contains("protected actor data", StringComparison.OrdinalIgnoreCase));
        }
        finally
        {
            CleanupTempRoot(root);
        }
    }

    [Fact]
    public async Task ApplyAsync_ActorMaterializationScalarRepairChangingOnlyNamedScalar_IsAccepted()
    {
        var root = CreateTempRoot();
        try
        {
            var fs = CreateFileSystem(root);
            var (profile, task, proposal) = await PrepareActorMaterializationScalarRepairAsync(
                fs,
                changeSiblingData: false);
            var gate = new GmWorkerApplyGate(fs, () => Task.FromResult<IReadOnlyList<ValidationIssue>>([]));

            var decision = await gate.ApplyAsync(proposal, task, profile);

            Assert.Equal(ApplyGateResult.Accepted, decision.Result);
        }
        finally
        {
            CleanupTempRoot(root);
        }
    }

    [Fact]
    public async Task ApplyAsync_DuplicateMaterializationProperty_IsRejectedWithoutThrowing()
    {
        var root = CreateTempRoot();
        try
        {
            const string path = "game_state/npcs/npc_core.json";
            const string contentRef =
                "worker_proposals/worker_proposal_actor_duplicate/game_state/npcs/npc_core.json";
            var fs = CreateFileSystem(root);
            var baselineActor = BuildRepairTargetActor("complete");
            var baseline = new JsonObject
            {
                ["UpdateNPCs"] = new JsonArray(),
                ["NPCsInScene"] = new JsonArray(baselineActor)
            }.ToJsonString();
            var duplicateEnvelope = baselineActor["materialization"]!.ToJsonString();
            var actorWithoutEnvelope = baselineActor.DeepClone().AsObject();
            actorWithoutEnvelope.Remove("materialization");
            var actorPrefix = actorWithoutEnvelope.ToJsonString()[..^1];
            var proposed =
                $"{{\"UpdateNPCs\":[],\"NPCsInScene\":[{actorPrefix},\"materialization\":{duplicateEnvelope},\"materialization\":{duplicateEnvelope}}}]}}";
            await fs.WriteFileAtomicAsync(path, baseline);
            await fs.WriteFileAtomicAsync(contentRef, proposed);
            var (profile, task, proposal) = BuildActorRepairPacket(
                fs,
                path,
                contentRef,
                "actor_materialization_duplicate_property",
                $"{path}.NPCsInScene[0].materialization");
            var gate = new GmWorkerApplyGate(fs, () => Task.FromResult<IReadOnlyList<ValidationIssue>>([]));

            var decision = await gate.ApplyAsync(proposal, task, profile);

            Assert.Equal(ApplyGateResult.Rejected, decision.Result);
            Assert.Contains(decision.RejectionReasons, reason =>
                reason.Contains("valid JSON", StringComparison.OrdinalIgnoreCase));
        }
        finally
        {
            CleanupTempRoot(root);
        }
    }

    [Theory]
    [InlineData(false, ApplyGateResult.Accepted)]
    [InlineData(true, ApplyGateResult.Rejected)]
    public async Task ApplyAsync_ResidentMemoryRepair_PreservesUnrelatedResidentData(
        bool changeResidentData,
        ApplyGateResult expectedResult)
    {
        var root = CreateTempRoot();
        try
        {
            const string path = "game_state/meta/guardian_abode_residents.json";
            const string actorId = "resident_memory_repair";
            const string contentRef =
                "worker_proposals/worker_proposal_resident_memory/game_state/meta/guardian_abode_residents.json";
            var fs = CreateFileSystem(root);
            var resident = new JsonObject
            {
                ["residentId"] = actorId,
                ["displayName"] = "Смотрительница записей"
            };
            var baseline = new JsonObject
            {
                ["schemaVersion"] = 1,
                ["entries"] = new JsonArray(resident),
                ["thoughtJournal"] = new JsonArray(new JsonObject
                {
                    ["entryId"] = "thought_resident_memory_existing",
                    ["residentId"] = actorId,
                    ["summary"] = "Я сохраняю прежнюю мысль без изменений."
                })
            };
            var proposed = baseline.DeepClone().AsObject();
            proposed["thoughtJournal"]!.AsArray().Add(new JsonObject
            {
                ["entryId"] = "thought_resident_memory_repair_12",
                ["residentId"] = actorId,
                ["title"] = "Смысл встречи",
                ["summary"] = "Я должна сохранить смысл этой встречи."
            });
            if (changeResidentData)
                proposed["entries"]![0]!["displayName"] = "Переписанное имя";
            await fs.WriteFileAtomicAsync(path, baseline.ToJsonString());
            await fs.WriteFileAtomicAsync(contentRef, proposed.ToJsonString());
            var (profile, task, proposal) = BuildActorRepairPacket(
                fs,
                path,
                contentRef,
                "afterlife_actor_materialization_memory_missing",
                $"{path}.entries[0]",
                $"resident:{actorId}");
            var gate = new GmWorkerApplyGate(fs, () => Task.FromResult<IReadOnlyList<ValidationIssue>>([]));

            var decision = await gate.ApplyAsync(proposal, task, profile);

            Assert.True(
                decision.Result == expectedResult,
                $"Expected {expectedResult}, got {decision.Result}: {string.Join(" | ", decision.RejectionReasons)}");
        }
        finally
        {
            CleanupTempRoot(root);
        }
    }

    [Fact]
    public async Task ApplyAsync_ResidentMemoryRepair_RewritingExistingThought_IsRejected()
    {
        var root = CreateTempRoot();
        try
        {
            const string path = "game_state/meta/guardian_abode_residents.json";
            const string actorId = "resident_memory_history";
            const string contentRef =
                "worker_proposals/worker_proposal_resident_memory_history/game_state/meta/guardian_abode_residents.json";
            var fs = CreateFileSystem(root);
            var baseline = new JsonObject
            {
                ["schemaVersion"] = 1,
                ["entries"] = new JsonArray(new JsonObject
                {
                    ["residentId"] = actorId,
                    ["displayName"] = "Хранительница свидетельств"
                }),
                ["thoughtJournal"] = new JsonArray(new JsonObject
                {
                    ["entryId"] = "thought_existing",
                    ["residentId"] = actorId,
                    ["summary"] = "Я сохраню прежнее свидетельство без правок."
                })
            };
            var proposed = baseline.DeepClone().AsObject();
            proposed["thoughtJournal"]![0]!["summary"] = "Переписанная старая мысль.";
            proposed["thoughtJournal"]!.AsArray().Add(new JsonObject
            {
                ["entryId"] = "thought_new",
                ["residentId"] = actorId,
                ["summary"] = "Я добавлю новый вывод отдельно."
            });
            await fs.WriteFileAtomicAsync(path, baseline.ToJsonString());
            await fs.WriteFileAtomicAsync(contentRef, proposed.ToJsonString());
            var (profile, task, proposal) = BuildActorRepairPacket(
                fs,
                path,
                contentRef,
                "afterlife_actor_materialization_memory_missing",
                $"{path}.entries[0]",
                $"resident:{actorId}");
            var gate = new GmWorkerApplyGate(fs, () => Task.FromResult<IReadOnlyList<ValidationIssue>>([]));

            var decision = await gate.ApplyAsync(proposal, task, profile);

            Assert.Equal(ApplyGateResult.Rejected, decision.Result);
            Assert.Contains(decision.RejectionReasons, reason =>
                reason.Contains("append", StringComparison.OrdinalIgnoreCase));
        }
        finally
        {
            CleanupTempRoot(root);
        }
    }

    [Fact]
    public async Task ApplyAsync_GuardianMemoryRepair_RewritingExistingMusing_IsRejected()
    {
        var root = CreateTempRoot();
        try
        {
            const string path = "game_state/meta/guardians.json";
            const string actorId = "guardian_memory_history";
            const string contentRef =
                "worker_proposals/worker_proposal_guardian_memory_history/game_state/meta/guardians.json";
            var fs = CreateFileSystem(root);
            var baseline = new JsonObject
            {
                ["guardians"] = new JsonArray(new JsonObject
                {
                    ["guardianId"] = actorId,
                    ["canonicalName"] = "Смотрительница памяти",
                    ["musings"] = new JsonArray(new JsonObject
                    {
                        ["turn"] = 4,
                        ["topic"] = "old_oath",
                        ["thought"] = "Старую клятву нельзя переписывать."
                    })
                })
            };
            var proposed = baseline.DeepClone().AsObject();
            var musings = proposed["guardians"]![0]!["musings"]!.AsArray();
            musings[0]!["thought"] = "Переписанная старая клятва.";
            musings.Add(new JsonObject
            {
                ["turn"] = 5,
                ["topic"] = "new_oath",
                ["thought"] = "Новая мысль остаётся отдельной записью."
            });
            await fs.WriteFileAtomicAsync(path, baseline.ToJsonString());
            await fs.WriteFileAtomicAsync(contentRef, proposed.ToJsonString());
            var (profile, task, proposal) = BuildActorRepairPacket(
                fs,
                path,
                contentRef,
                "afterlife_actor_materialization_memory_missing",
                $"{path}.guardians[0]",
                $"guardian:{actorId}");
            var gate = new GmWorkerApplyGate(fs, () => Task.FromResult<IReadOnlyList<ValidationIssue>>([]));

            var decision = await gate.ApplyAsync(proposal, task, profile);

            Assert.Equal(ApplyGateResult.Rejected, decision.Result);
            Assert.Contains(decision.RejectionReasons, reason =>
                reason.Contains("append", StringComparison.OrdinalIgnoreCase));
        }
        finally
        {
            CleanupTempRoot(root);
        }
    }

    [Fact]
    public async Task ApplyAsync_GuardianMemoryRepair_AppendingMusingPreservesHistory()
    {
        var root = CreateTempRoot();
        try
        {
            const string path = "game_state/meta/guardians.json";
            const string actorId = "guardian_memory_append";
            const string contentRef =
                "worker_proposals/worker_proposal_guardian_memory_append/game_state/meta/guardians.json";
            var fs = CreateFileSystem(root);
            var baseline = new JsonObject
            {
                ["guardians"] = new JsonArray(new JsonObject
                {
                    ["guardianId"] = actorId,
                    ["canonicalName"] = "Смотрительница памяти",
                    ["musings"] = new JsonArray(new JsonObject
                    {
                        ["turn"] = 4,
                        ["topic"] = "old_oath",
                        ["thought"] = "Старая мысль остаётся неизменной."
                    })
                })
            };
            var proposed = baseline.DeepClone().AsObject();
            proposed["guardians"]![0]!["musings"]!.AsArray().Add(new JsonObject
            {
                ["turn"] = 5,
                ["topic"] = "new_oath",
                ["thought"] = "Новая мысль добавляется отдельной записью."
            });
            await fs.WriteFileAtomicAsync(path, baseline.ToJsonString());
            await fs.WriteFileAtomicAsync(contentRef, proposed.ToJsonString());
            var (profile, task, proposal) = BuildActorRepairPacket(
                fs,
                path,
                contentRef,
                "afterlife_actor_materialization_memory_missing",
                $"{path}.guardians[0]",
                $"guardian:{actorId}");
            var gate = new GmWorkerApplyGate(fs, () => Task.FromResult<IReadOnlyList<ValidationIssue>>([]));

            var decision = await gate.ApplyAsync(proposal, task, profile);

            Assert.Equal(ApplyGateResult.Accepted, decision.Result);
        }
        finally
        {
            CleanupTempRoot(root);
        }
    }

    [Fact]
    public async Task ApplyAsync_GuardianThoughtJournalRepair_AppendsOneOwnedEntry()
    {
        var root = CreateTempRoot();
        try
        {
            const string path = "game_state/meta/guardian_thought_journal.json";
            const string actorId = "guardian_journal_append";
            const string proposalId = "worker_proposal_guardian_journal_append";
            const string contentRef =
                "worker_proposals/worker_proposal_guardian_journal_append/game_state/meta/guardian_thought_journal.json";
            var fs = CreateFileSystem(root);
            var baseline = new JsonObject
            {
                ["entries"] = new JsonArray(new JsonObject
                {
                    ["entryId"] = "thought_old",
                    ["guardianId"] = actorId,
                    ["summary"] = "Я помню прежнее решение."
                })
            };
            var proposed = baseline.DeepClone().AsObject();
            proposed["entries"]!.AsArray().Add(new JsonObject
            {
                ["entryId"] = "thought_new",
                ["guardianId"] = actorId,
                ["summary"] = "Я учту новый выбор души."
            });
            await fs.WriteFileAtomicAsync(path, baseline.ToJsonString());
            await fs.WriteFileAtomicAsync(contentRef, proposed.ToJsonString());
            var (profile, task, proposal) = BuildActorRepairPacket(
                fs,
                path,
                contentRef,
                "afterlife_actor_materialization_memory_missing",
                "game_state/meta/guardians.json.guardians[0]",
                $"guardian:{actorId}");
            proposal = proposal with { ProposalId = proposalId };
            var gate = new GmWorkerApplyGate(fs, () => Task.FromResult<IReadOnlyList<ValidationIssue>>([]));

            var decision = await gate.ApplyAsync(proposal, task, profile);

            Assert.Equal(ApplyGateResult.Accepted, decision.Result);
        }
        finally
        {
            CleanupTempRoot(root);
        }
    }

    [Fact]
    public async Task ApplyAsync_MissingGuardianThoughtJournalRepair_AddsOneOwnedEntry()
    {
        var root = CreateTempRoot();
        try
        {
            const string path = "game_state/meta/guardian_thought_journal.json";
            const string actorId = "guardian_journal_first_entry";
            const string contentRef =
                "worker_proposals/worker_proposal_guardian_journal_first_entry/game_state/meta/guardian_thought_journal.json";
            var fs = CreateFileSystem(root);
            var proposed = new JsonObject
            {
                ["entries"] = new JsonArray(new JsonObject
                {
                    ["entryId"] = "thought_first",
                    ["guardianId"] = actorId,
                    ["summary"] = "Я сохраню первое самостоятельное решение души."
                })
            };
            await fs.WriteFileAtomicAsync(contentRef, proposed.ToJsonString());
            var (profile, task, proposal) = BuildActorRepairPacket(
                fs,
                path,
                contentRef,
                "afterlife_actor_materialization_memory_missing",
                "game_state/meta/guardians.json.guardians[0]",
                $"guardian:{actorId}");
            var gate = new GmWorkerApplyGate(fs, () => Task.FromResult<IReadOnlyList<ValidationIssue>>([]));

            var decision = await gate.ApplyAsync(proposal, task, profile);

            Assert.Equal(ApplyGateResult.Accepted, decision.Result);
            var appliedJson = await fs.ReadFileAsync(path);
            Assert.NotNull(appliedJson);
            Assert.True(JsonNode.DeepEquals(
                proposed,
                JsonNode.Parse(appliedJson)));
        }
        finally
        {
            CleanupTempRoot(root);
        }
    }

    [Theory]
    [InlineData("wrong_owner")]
    [InlineData("extra_root_data")]
    public async Task ApplyAsync_MissingGuardianThoughtJournalRepair_RejectsUnsafeCreation(
        string mutation)
    {
        var root = CreateTempRoot();
        try
        {
            const string path = "game_state/meta/guardian_thought_journal.json";
            const string actorId = "guardian_journal_safe_owner";
            const string contentRef =
                "worker_proposals/worker_proposal_guardian_journal_unsafe/game_state/meta/guardian_thought_journal.json";
            var fs = CreateFileSystem(root);
            var proposed = new JsonObject
            {
                ["entries"] = new JsonArray(new JsonObject
                {
                    ["entryId"] = "thought_unsafe",
                    ["guardianId"] = mutation == "wrong_owner" ? "guardian_journal_other_owner" : actorId,
                    ["summary"] = "Эта запись должна пройти только при точной безопасной маршрутизации."
                })
            };
            if (mutation == "extra_root_data")
                proposed["unrelatedState"] = new JsonObject { ["changed"] = true };

            await fs.WriteFileAtomicAsync(contentRef, proposed.ToJsonString());
            var (profile, task, proposal) = BuildActorRepairPacket(
                fs,
                path,
                contentRef,
                "afterlife_actor_materialization_memory_missing",
                "game_state/meta/guardians.json.guardians[0]",
                $"guardian:{actorId}");
            var gate = new GmWorkerApplyGate(fs, () => Task.FromResult<IReadOnlyList<ValidationIssue>>([]));

            var decision = await gate.ApplyAsync(proposal, task, profile);

            Assert.Equal(ApplyGateResult.Rejected, decision.Result);
            Assert.False(File.Exists(fs.ResolvePath(path)));
        }
        finally
        {
            CleanupTempRoot(root);
        }
    }

    [Fact]
    public async Task ApplyAsync_GuardianThoughtJournalRepair_RewritingHistoryIsRejected()
    {
        var root = CreateTempRoot();
        try
        {
            const string path = "game_state/meta/guardian_thought_journal.json";
            const string actorId = "guardian_journal_history";
            const string proposalId = "worker_proposal_guardian_journal_history";
            const string contentRef =
                "worker_proposals/worker_proposal_guardian_journal_history/game_state/meta/guardian_thought_journal.json";
            var fs = CreateFileSystem(root);
            var baseline = new JsonObject
            {
                ["entries"] = new JsonArray(new JsonObject
                {
                    ["entryId"] = "thought_old",
                    ["guardianId"] = actorId,
                    ["summary"] = "Эта мысль уже стала частью памяти."
                })
            };
            var proposed = baseline.DeepClone().AsObject();
            proposed["entries"]![0]!["summary"] = "Переписанная память.";
            proposed["entries"]!.AsArray().Add(new JsonObject
            {
                ["entryId"] = "thought_new",
                ["guardianId"] = actorId,
                ["summary"] = "Я сохраню новую мысль отдельно."
            });
            await fs.WriteFileAtomicAsync(path, baseline.ToJsonString());
            await fs.WriteFileAtomicAsync(contentRef, proposed.ToJsonString());
            var (profile, task, proposal) = BuildActorRepairPacket(
                fs,
                path,
                contentRef,
                "afterlife_actor_materialization_memory_missing",
                "game_state/meta/guardians.json.guardians[0]",
                $"guardian:{actorId}");
            proposal = proposal with { ProposalId = proposalId };
            var gate = new GmWorkerApplyGate(fs, () => Task.FromResult<IReadOnlyList<ValidationIssue>>([]));

            var decision = await gate.ApplyAsync(proposal, task, profile);

            Assert.Equal(ApplyGateResult.Rejected, decision.Result);
            Assert.Contains(decision.RejectionReasons, reason =>
                reason.Contains("append", StringComparison.OrdinalIgnoreCase) ||
                reason.Contains("preserve", StringComparison.OrdinalIgnoreCase));
        }
        finally
        {
            CleanupTempRoot(root);
        }
    }

    [Theory]
    [InlineData(false, ApplyGateResult.Accepted)]
    [InlineData(true, ApplyGateResult.Rejected)]
    public async Task ApplyAsync_AmbiguousAfterlifeProfileRepair_OnlyRemovesUnchangedDuplicate(
        bool rewriteSurvivor,
        ApplyGateResult expectedResult)
    {
        var root = CreateTempRoot();
        try
        {
            const string path = "game_state/meta/afterlife_entity_profiles.json";
            const string actorId = "resident_ambiguous_repair";
            const string contentRef =
                "worker_proposals/worker_proposal_ambiguous_profile/game_state/meta/afterlife_entity_profiles.json";
            var fs = CreateFileSystem(root);
            var actor = new JsonObject
            {
                ["actorType"] = "resident",
                ["actorId"] = actorId,
                ["displayName"] = "Свидетельница двух записей"
            };
            var baseline = new JsonObject
            {
                ["schemaVersion"] = 1,
                ["profiles"] = new JsonArray(actor, actor.DeepClone())
            };
            var survivor = actor.DeepClone().AsObject();
            if (rewriteSurvivor)
                survivor["displayName"] = "Переписанная свидетельница";
            var proposed = new JsonObject
            {
                ["schemaVersion"] = 1,
                ["profiles"] = new JsonArray(survivor)
            };
            await fs.WriteFileAtomicAsync(path, baseline.ToJsonString());
            await fs.WriteFileAtomicAsync(contentRef, proposed.ToJsonString());
            var (profile, task, proposal) = BuildActorRepairPacket(
                fs,
                path,
                contentRef,
                "afterlife_actor_materialization_profile_ambiguous",
                "game_state/meta/guardian_abode_residents.json.entries[0]",
                $"resident:{actorId}");
            var gate = new GmWorkerApplyGate(fs, () => Task.FromResult<IReadOnlyList<ValidationIssue>>([]));

            var decision = await gate.ApplyAsync(proposal, task, profile);

            Assert.True(
                decision.Result == expectedResult,
                $"Expected {expectedResult}, got {decision.Result}: {string.Join(" | ", decision.RejectionReasons)}");
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

        var beforeSha256 = ComputeSha256(await File.ReadAllBytesAsync(
            fs.ResolvePath("game_state/world/weather.json")));
        var afterSha256 = ComputeSha256(await File.ReadAllBytesAsync(
            fs.ResolvePath("worker_proposals/worker_proposal_20260620_0001/game_state/world/weather.json")));
        task = task with
        {
            ContextFiles =
            [
                new WorkerFileReference
                {
                    Path = "game_state/world/weather.json",
                    Sha256 = beforeSha256
                }
            ]
        };
        proposal = proposal with
        {
            ChangedFiles =
            [
                proposal.ChangedFiles[0] with
                {
                    BeforeSha256 = beforeSha256,
                    AfterSha256 = afterSha256
                }
            ]
        };

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
        var baselineSha256 = ComputeSha256(await File.ReadAllBytesAsync(fs.ResolvePath(path)));
        var proposalSha256 = ComputeSha256(await File.ReadAllBytesAsync(fs.ResolvePath(contentRef)));

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
            ContextFiles = [new WorkerFileReference { Path = path, Sha256 = baselineSha256 }],
            AllowedProposalPaths = [path]
        };
        var proposal = GmWorkerBridgeTestFixtures.ValidationRepairProposal() with
        {
            ProposalId = contentRef.Split('/')[1],
            ChangedFiles =
            [
                new WorkerChangedFile
                {
                    Path = path,
                    ChangeKind = WorkerFileChangeKind.Replace,
                    BeforeSha256 = baselineSha256,
                    AfterSha256 = proposalSha256,
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

    private static async Task<(WorkerBridgeProfile Profile, WorkerTaskPacket Task, WorkerProposal Proposal)>
        PrepareActorMaterializationScalarRepairAsync(
            FileSystemManager fs,
            bool changeSiblingData)
    {
        const string path = "game_state/npcs/npc_core.json";
        const string contentRef =
            "worker_proposals/worker_proposal_actor_scalar/game_state/npcs/npc_core.json";
        var baselineActor = BuildRepairTargetActor("partial");
        var proposedActor = baselineActor.DeepClone().AsObject();
        proposedActor["materialization"]!["state"] = "complete";
        if (changeSiblingData)
            proposedActor["materialization"]!["materializationId"] = "mat_rewritten_by_worker";

        var baseline = new JsonObject
        {
            ["UpdateNPCs"] = new JsonArray(),
            ["NPCsInScene"] = new JsonArray(baselineActor)
        };
        var proposed = new JsonObject
        {
            ["UpdateNPCs"] = new JsonArray(),
            ["NPCsInScene"] = new JsonArray(proposedActor)
        };
        await fs.WriteFileAtomicAsync(path, baseline.ToJsonString());
        await fs.WriteFileAtomicAsync(contentRef, proposed.ToJsonString());
        return BuildActorRepairPacket(
            fs,
            path,
            contentRef,
            "actor_materialization_invalid_envelope",
            $"{path}.NPCsInScene[0].materialization.state");
    }

    private static (WorkerBridgeProfile Profile, WorkerTaskPacket Task, WorkerProposal Proposal)
        BuildActorRepairPacket(
            FileSystemManager fs,
            string path,
            string contentRef,
            string code,
            string issuePath,
            string actor = "mortal_npc:npc_repair_target")
    {
        var resolvedBaselinePath = fs.ResolvePath(path);
        var baselineExists = File.Exists(resolvedBaselinePath);
        var baselineSha256 = baselineExists
            ? ComputeSha256(File.ReadAllBytes(resolvedBaselinePath))
            : "missing";
        var proposalSha256 = ComputeSha256(File.ReadAllBytes(fs.ResolvePath(contentRef)));
        var profile = GmWorkerBridgeTestFixtures.ValidationRepairCodexProfile();
        var task = GmWorkerBridgeTestFixtures.ValidationRepairTask() with
        {
            ValidationIssues =
            [
                new WorkerValidationIssue
                {
                    Code = code,
                    Path = issuePath,
                    Message = "Actor materialization repair regression.",
                    Actor = actor,
                    Section = "ActorMaterialization"
                }
            ],
            ContextFiles = [new WorkerFileReference { Path = path, Sha256 = baselineSha256 }],
            AfterlifeContract = actor.StartsWith("mortal_npc:", StringComparison.Ordinal)
                ? null
                : new WorkerAfterlifeTaskContract
                {
                    RealmGate = WorkerAfterlifeRealmGate.ChaosSea,
                    CurrentRealm = "Chaos Sea",
                    AllowedAfterlifeSurfaces = [path],
                    RequiredReceipts = ["No new receipt is required for bounded repair."],
                    RequiredReports = ["Apply-gate validation decision."],
                    ForbiddenMortalSubstitutes = ["worldStateFlags"]
                },
            AllowedProposalPaths = [path]
        };
        var proposal = GmWorkerBridgeTestFixtures.ValidationRepairProposal() with
        {
            ProposalId = contentRef.Split('/')[1],
            ChangedFiles =
            [
                new WorkerChangedFile
                {
                    Path = path,
                    ChangeKind = baselineExists
                        ? WorkerFileChangeKind.Replace
                        : WorkerFileChangeKind.Add,
                    BeforeSha256 = baselineSha256,
                    AfterSha256 = proposalSha256,
                    ContentRef = contentRef
                }
            ]
        };
        return (profile, task, proposal);
    }

    private static JsonObject BuildRepairTargetActor(string materializationState) => new()
    {
        ["NPCId"] = "npc_repair_target",
        ["name"] = "Ирен Соль",
        ["personality"] = new JsonObject { ["summary"] = "Сдержанная и наблюдательная." },
        ["inventory"] = new JsonArray(),
        ["materialization"] = new JsonObject
        {
            ["schemaVersion"] = 1,
            ["materializationId"] = "mat_npc_repair_target_turn_12",
            ["actorType"] = "mortal_npc",
            ["actorId"] = "npc_repair_target",
            ["materializedAtTurn"] = 12,
            ["state"] = materializationState,
            ["capabilities"] = new JsonObject
            {
                ["canFight"] = false,
                ["canTeach"] = false,
                ["canTrade"] = false,
                ["ownsItems"] = false
            },
            ["sections"] = new JsonObject
            {
                ["inventory"] = new JsonObject
                {
                    ["state"] = "empty_by_design",
                    ["reason"] = "У персонажа пока нет вещей."
                }
            }
        }
    };

    private static string ComputeSha256(string content) =>
        ComputeSha256(Encoding.UTF8.GetBytes(content));

    private static string ComputeSha256(byte[] content) =>
        Convert.ToHexString(SHA256.HashData(content)).ToLowerInvariant();

    private static string ComputeFileSha256(FileSystemManager fs, string path) =>
        ComputeSha256(File.ReadAllBytes(fs.ResolvePath(path)));

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
