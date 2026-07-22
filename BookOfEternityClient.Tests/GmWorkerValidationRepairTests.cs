using BookOfEternityClient.Services;
using BookOfEternityClient.Services.GmWorkers;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed class GmWorkerValidationRepairTests
{
    [Fact]
    public void BuildValidationRepairTask_PackagesValidationIssuesIntoScopedWorkerPacket()
    {
        var profile = GmWorkerBridgeTestFixtures.ValidationRepairCodexProfile();
        var sourceTurn = new WorkerTurnReference
        {
            SessionId = "test-session",
            RequestId = "test-request",
            TurnNumber = 12
        };
        var issues = new[]
        {
            new ValidationIssue(
                "game_state/world/weather.json",
                IssueSeverity.Error,
                "normalizedWeatherState.description is required.",
                code: "normalized_weather_missing_description",
                repairHint: "Add a player-facing weather description.")
        };
        var contextHashes = new Dictionary<string, string>
        {
            ["game_state/world/weather.json"] = new string('a', 64)
        };

        var task = GmWorkerTaskPacketBuilder.BuildValidationRepairTask(
            profile,
            "worker_task_test",
            sourceTurn,
            issues,
            contextHashes,
            "2026-06-20T00:00:00Z");

        Assert.Equal("worker_task_test", task.TaskId);
        Assert.Equal(profile.WorkerId, task.WorkerId);
        Assert.Equal(profile.Role, task.Role);
        Assert.Equal(WorkerTaskType.ValidationRepair, task.TaskType);
        Assert.Equal(profile.TimeoutSeconds, task.TimeoutSeconds);
        Assert.Contains(task.AcceptanceCriteria, criterion =>
            criterion.Contains("worker-proposal-v1", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(task.AcceptanceCriteria, criterion =>
            criterion.Contains("validation", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(task.ForbiddenActions, action =>
            action.Contains("canonical game_session files", StringComparison.OrdinalIgnoreCase));
        Assert.Contains("game_state/world/weather.json", task.AllowedProposalPaths);
        Assert.Contains(task.ValidationIssues, issue =>
            issue.Code == "normalized_weather_missing_description" &&
            issue.Path == "game_state/world/weather.json");
        Assert.Contains(task.ContextFiles, file =>
            file.Path == "game_state/world/weather.json" &&
            file.Sha256 == new string('a', 64));

        var result = GmWorkerContractValidator.ValidateTaskPacket(task, profile);
        Assert.True(result.IsValid, string.Join(Environment.NewLine, result.Errors));
    }

    [Fact]
    public void BuildValidationRepairTask_PreservesActorCoordinatesAndTargetsCanonicalFile()
    {
        var profile = GmWorkerBridgeTestFixtures.ValidationRepairCodexProfile();
        var sourceTurn = new WorkerTurnReference
        {
            SessionId = "test-session",
            RequestId = "test-request",
            TurnNumber = 12
        };
        var issues = new[]
        {
            new ValidationIssue(
                "game_state/npcs/npc_core.json.NPCsInScene[0].materialization.sections.inventory",
                IssueSeverity.Error,
                "Первичная материализация не объясняет секцию inventory.",
                code: "actor_materialization_section_missing",
                actor: "mortal_npc:npc_repair_target",
                section: "inventory",
                expected: "populated or empty_by_design with reason",
                actual: "missing")
        };
        var contextHashes = new Dictionary<string, string>
        {
            ["game_state/npcs/npc_core.json"] = new string('b', 64)
        };

        var task = GmWorkerTaskPacketBuilder.BuildValidationRepairTask(
            profile,
            "worker_task_actor_materialization",
            sourceTurn,
            issues,
            contextHashes,
            "2026-06-20T00:00:00Z");

        Assert.Equal(new[] { "game_state/npcs/npc_core.json" }, task.AllowedProposalPaths);
        var packagedIssue = Assert.Single(task.ValidationIssues);
        Assert.Equal("mortal_npc:npc_repair_target", packagedIssue.Actor);
        Assert.Equal("inventory", packagedIssue.Section);
        Assert.Equal("populated or empty_by_design with reason", packagedIssue.Expected);
        Assert.Equal("missing", packagedIssue.Actual);
        Assert.Equal(
            "game_state/npcs/npc_core.json.NPCsInScene[0].materialization.sections.inventory",
            packagedIssue.Path);
        Assert.Equal(
            "game_state/npcs/npc_core.json",
            Assert.Single(task.ContextFiles).Path);
        Assert.Contains(task.AcceptanceCriteria, criterion =>
            criterion.Contains("protected actor data", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(task.AcceptanceCriteria, criterion =>
            criterion.Contains("finite JSON number", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(task.ForbiddenActions, action =>
            action.Contains("untargeted actor", StringComparison.OrdinalIgnoreCase));
    }

    [Theory]
    [InlineData("npc_initial_id_collides_with_existing_permanent_id", "NPCIdentity", null, false)]
    [InlineData("npc_existing_inventory_resend_forbidden", "NPCInventory", "Use dedicated inventory commands", false)]
    [InlineData("npc_existing_inventory_resend_forbidden", "NPCInventory", "[]", true)]
    [InlineData("npc_characteristics_empty", "NPCCharacteristics", "at least one setting-defined numeric characteristic", true)]
    public void BuildValidationRepairTask_MortalContinuityIssues_UseExplicitDispatchPolicy(
        string code,
        string section,
        string? expected,
        bool shouldDispatch)
    {
        const string path = "game_state/npcs/npc_core.json";
        var profile = GmWorkerBridgeTestFixtures.ValidationRepairCodexProfile();
        var issue = new ValidationIssue(
            $"{path}.UpdateNPCs[0].{(section == "NPCIdentity" ? "initialId" : section == "NPCInventory" ? "inventory" : "characteristics")}",
            IssueSeverity.Error,
            "Mortal continuity repair regression.",
            code: code,
            actor: "mortal_npc:npc_policy_target",
            section: section,
            expected: expected);

        WorkerTaskPacket Build()
        {
            var contextHashes = new Dictionary<string, string>
            {
                [path] = new string('f', 64)
            };
            if (code == "npc_characteristics_empty")
                contextHashes["game_state/misc/characteristics.json"] = new string('e', 64);

            return GmWorkerTaskPacketBuilder.BuildValidationRepairTask(
                profile,
                "worker_task_mortal_continuity_policy",
                new WorkerTurnReference
                {
                    SessionId = "test-session",
                    RequestId = "test-request",
                    TurnNumber = 12
                },
                [issue],
                contextHashes,
                "2026-06-20T00:00:00Z");
        }

        if (!shouldDispatch)
        {
            var exception = Assert.Throws<ArgumentException>(Build);
            Assert.Contains("main GM", exception.Message, StringComparison.OrdinalIgnoreCase);
            return;
        }

        var task = Build();
        Assert.Equal(new[] { path }, task.AllowedProposalPaths);
        var packagedIssue = Assert.Single(task.ValidationIssues);
        Assert.Equal("mortal_npc:npc_policy_target", packagedIssue.Actor);
        Assert.Equal(section, packagedIssue.Section);
        Assert.Equal(expected, packagedIssue.Expected);
    }

    [Fact]
    public void BuildValidationRepairTask_CharacteristicsRepairRequiresHashPinnedReadOnlyAuthority()
    {
        const string npcPath = "game_state/npcs/npc_core.json";
        const string authorityPath = "game_state/misc/characteristics.json";
        var profile = GmWorkerBridgeTestFixtures.ValidationRepairCodexProfile();
        var issue = new ValidationIssue(
            $"{npcPath}.UpdateNPCs[0].characteristics",
            IssueSeverity.Error,
            "Mortal characteristics are empty.",
            code: "npc_characteristics_empty",
            actor: "mortal_npc:npc_authority_target",
            section: "NPCCharacteristics",
            expected: "at least one setting-defined numeric characteristic");
        var sourceTurn = new WorkerTurnReference
        {
            SessionId = "test-session",
            RequestId = "test-request",
            TurnNumber = 12
        };

        var missingAuthority = Assert.Throws<ArgumentException>(() =>
            GmWorkerTaskPacketBuilder.BuildValidationRepairTask(
                profile,
                "worker_task_characteristics_missing_authority",
                sourceTurn,
                [issue],
                new Dictionary<string, string> { [npcPath] = new string('a', 64) },
                "2026-06-20T00:00:00Z"));
        Assert.Contains(authorityPath, missingAuthority.Message, StringComparison.Ordinal);

        var task = GmWorkerTaskPacketBuilder.BuildValidationRepairTask(
            profile,
            "worker_task_characteristics_with_authority",
            sourceTurn,
            [issue],
            new Dictionary<string, string>
            {
                [npcPath] = new string('a', 64),
                [authorityPath] = new string('b', 64)
            },
            "2026-06-20T00:00:00Z");

        Assert.Equal([npcPath], task.AllowedProposalPaths);
        Assert.Contains(task.ContextFiles, file =>
            file.Path == authorityPath && file.Sha256 == new string('b', 64));
    }

    [Theory]
    [InlineData("npc_existing_inventory_resend_forbidden", "NPCInventory", "[]", "inventory")]
    [InlineData("npc_characteristics_empty", "NPCCharacteristics", "at least one setting-defined numeric characteristic", "characteristics")]
    public void BuildValidationRepairTask_MortalContinuityIssueWithoutExactActor_ForcesMainGm(
        string code,
        string section,
        string expected,
        string property)
    {
        var profile = GmWorkerBridgeTestFixtures.ValidationRepairCodexProfile();
        var issue = new ValidationIssue(
            $"response.UpdateNPCs[0].{property}",
            IssueSeverity.Error,
            "Mortal continuity repair missing actor metadata.",
            code: code,
            actor: null,
            section: section,
            expected: expected);

        var exception = Assert.Throws<ArgumentException>(() =>
            GmWorkerTaskPacketBuilder.BuildValidationRepairTask(
                profile,
                "worker_task_mortal_continuity_missing_actor",
                new WorkerTurnReference
                {
                    SessionId = "test-session",
                    RequestId = "test-request",
                    TurnNumber = 12
                },
                [issue],
                new Dictionary<string, string>(),
                "2026-06-20T00:00:00Z"));

        Assert.Contains("main GM", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData(
        "game_state/meta/guardians.json.guardians[0]",
        "game_state/meta/guardian_thought_journal.json",
        "guardian:guardian_memory_target")]
    [InlineData(
        "game_state/meta/guardian_abode_residents.json.entries[0]",
        "game_state/meta/guardian_abode_residents.json",
        "resident:resident_memory_target")]
    [InlineData(
        "game_state/meta/shining_abode.json.radiantActors[0]",
        "game_state/meta/afterlife_entity_profiles.json",
        "radiant_actor:radiant_memory_target")]
    [InlineData(
        "game_state/meta/saref_main_story_state.json.agents[0]",
        "game_state/meta/afterlife_entity_profiles.json",
        "saref_agent:saref_memory_target")]
    public void BuildValidationRepairTask_AfterlifeMemoryTargetsExactDedicatedAuthority(
        string issuePath,
        string expectedTargetPath,
        string actor)
    {
        var profile = GmWorkerBridgeTestFixtures.ValidationRepairCodexProfile();
        var issue = new ValidationIssue(
            issuePath,
            IssueSeverity.Error,
            "Первичная материализация не инициализировала actor-owned memory.",
            code: "afterlife_actor_materialization_memory_missing",
            actor: actor,
            section: "ActorMemory");
        var hashes = new Dictionary<string, string>
        {
            [expectedTargetPath] = new string('c', 64),
            ["game_state/meta/soul_state.json"] = new string('d', 64)
        };

        var task = GmWorkerTaskPacketBuilder.BuildValidationRepairTask(
            profile,
            "worker_task_afterlife_memory",
            new WorkerTurnReference
            {
                SessionId = "test-session",
                RequestId = "test-request",
                TurnNumber = 12
            },
            [issue],
            hashes,
            "2026-06-20T00:00:00Z",
            BuildAfterlifeRepairContract(expectedTargetPath));

        Assert.Equal(new[] { expectedTargetPath }, task.AllowedProposalPaths);
        Assert.Contains(task.ContextFiles, context =>
            context.Path == expectedTargetPath && context.Sha256 == new string('c', 64));
        Assert.Contains(task.ContextFiles, context =>
            context.Path == "game_state/meta/soul_state.json" && context.Sha256 == new string('d', 64));
        Assert.DoesNotContain("game_state/meta/soul_state.json", task.AllowedProposalPaths);
        Assert.Contains("afterlifeProposal is optional", task.Instructions, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Return afterlifeProposal when this contract is present", task.Instructions, StringComparison.Ordinal);
    }

    [Fact]
    public void BuildValidationRepairTask_AfterlifeRepairRequiresHashPinnedRealmAuthority()
    {
        const string targetPath = "game_state/meta/guardian_thought_journal.json";
        var profile = GmWorkerBridgeTestFixtures.ValidationRepairCodexProfile();
        var issue = new ValidationIssue(
            "game_state/meta/guardians.json.guardians[0]",
            IssueSeverity.Error,
            "Guardian memory is missing.",
            code: "afterlife_actor_materialization_memory_missing",
            actor: "guardian:guardian_realm_authority",
            section: "ActorMemory");

        var exception = Assert.Throws<ArgumentException>(() =>
            GmWorkerTaskPacketBuilder.BuildValidationRepairTask(
                profile,
                "worker_task_afterlife_missing_realm_authority",
                new WorkerTurnReference
                {
                    SessionId = "test-session",
                    RequestId = "test-request",
                    TurnNumber = 12
                },
                [issue],
                new Dictionary<string, string> { [targetPath] = new string('c', 64) },
                "2026-06-20T00:00:00Z",
                BuildAfterlifeRepairContract(targetPath)));

        Assert.Contains("soul_state.json", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ValidateTaskPacket_AfterlifeRepairWithoutPinnedRealmAuthorityIsRejected()
    {
        const string targetPath = "game_state/meta/guardian_thought_journal.json";
        var profile = GmWorkerBridgeTestFixtures.ValidationRepairCodexProfile();
        var task = GmWorkerBridgeTestFixtures.ValidationRepairTask() with
        {
            ValidationIssues =
            [
                new WorkerValidationIssue
                {
                    Code = "afterlife_actor_materialization_memory_missing",
                    Path = "game_state/meta/guardians.json.guardians[0]",
                    Message = "Guardian memory is missing.",
                    Actor = "guardian:guardian_realm_authority",
                    Section = "ActorMemory"
                }
            ],
            ContextFiles = [new WorkerFileReference { Path = targetPath, Sha256 = new string('c', 64) }],
            AfterlifeContract = BuildAfterlifeRepairContract(targetPath),
            AllowedProposalPaths = [targetPath]
        };

        var result = GmWorkerContractValidator.ValidateTaskPacket(task, profile);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, error =>
            error.Contains("soul_state.json", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void BuildValidationRepairTask_MixedMortalAndAfterlifeMaterializationIssues_FailsClosed()
    {
        var profile = GmWorkerBridgeTestFixtures.ValidationRepairCodexProfile();
        var issues = new[]
        {
            new ValidationIssue(
                "game_state/npcs/npc_core.json.NPCsInScene[0].materialization",
                IssueSeverity.Error,
                "Mortal actor materialization is incomplete.",
                code: "actor_materialization_missing",
                actor: "mortal_npc:npc_mixed_target"),
            new ValidationIssue(
                "game_state/meta/afterlife_entity_profiles.json.profiles[0]",
                IssueSeverity.Error,
                "Afterlife actor profile is missing.",
                code: "afterlife_actor_materialization_profile_missing",
                actor: "guardian:guardian_mixed_target")
        };
        var hashes = new Dictionary<string, string>
        {
            ["game_state/npcs/npc_core.json"] = new string('d', 64),
            [AfterlifeEntityProfileState.StatePath] = new string('e', 64),
            ["game_state/meta/soul_state.json"] = new string('f', 64)
        };

        var exception = Assert.Throws<InvalidOperationException>(() =>
            GmWorkerTaskPacketBuilder.BuildValidationRepairTask(
                profile,
                "worker_task_mixed_repair",
                new WorkerTurnReference
                {
                    SessionId = "test-session",
                    RequestId = "test-request",
                    TurnNumber = 12
                },
                issues,
                hashes,
                "2026-06-20T00:00:00Z",
                BuildAfterlifeRepairContract(AfterlifeEntityProfileState.StatePath)));

        Assert.Contains("allowedAfterlifeSurfaces", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    private static WorkerAfterlifeTaskContract BuildAfterlifeRepairContract(string targetPath) => new()
    {
        RealmGate = WorkerAfterlifeRealmGate.ChaosSea,
        CurrentRealm = "Chaos Sea",
        AllowedAfterlifeSurfaces = [targetPath],
        RequiredReceipts = ["No new receipt is required for bounded repair."],
        RequiredReports = ["Apply-gate validation decision."],
        ForbiddenMortalSubstitutes = ["worldStateFlags"]
    };
}
