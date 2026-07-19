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
            path,
            contentRef,
            "actor_materialization_invalid_envelope",
            $"{path}.NPCsInScene[0].materialization.state");
    }

    private static (WorkerBridgeProfile Profile, WorkerTaskPacket Task, WorkerProposal Proposal)
        BuildActorRepairPacket(
            string path,
            string contentRef,
            string code,
            string issuePath,
            string actor = "mortal_npc:npc_repair_target")
    {
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
            ContextFiles = [new WorkerFileReference { Path = path, Sha256 = "baseline" }],
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
