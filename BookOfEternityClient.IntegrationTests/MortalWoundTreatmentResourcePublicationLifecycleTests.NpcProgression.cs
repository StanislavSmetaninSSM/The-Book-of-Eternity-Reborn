using System.Text.Json;
using System.Text.Json.Nodes;
using BookOfEternityClient.Core;
using BookOfEternityClient.IO;
using BookOfEternityClient.Models;
using BookOfEternityClient.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed partial class GameEngineTurnLifecycleTests
{
    [Theory]
    [InlineData(4)]
    [InlineData(5)]
    [InlineData(6)]
    [InlineData(7)]
    public Task GuaranteedItemConsumption_NpcProgressionAndSkillUsesNaturalOwnerIntake(int scenario) =>
        GuaranteedItemConsumption_NpcProgressionAndTreatmentSkillShareAcceptedRoot(scenario);

    [Theory]
    [InlineData(8)]
    [InlineData(9)]
    [InlineData(10)]
    [InlineData(11)]
    public Task GuaranteedItemConsumption_NaturalNpcProgressionRequiresActualCadenceMasteryAward(int scenario) =>
        GuaranteedItemConsumption_NpcProgressionAndTreatmentSkillShareAcceptedRoot(scenario);

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    public async Task GuaranteedItemConsumption_NpcProgressionAndTreatmentSkillShareAcceptedRoot(int scenario)
    {
        var lateCut = scenario is 1 or 11;
        var armed = false;
        var reached = false;
        FileSystemManager? observedFiles = null;
        JsonNode? expectedNpc = null;
        var expectedConsumeTransitions = 0;
        const string cutMarker = "NPC progression treatment completed publication cut";
        var hooks = lateCut ? new FileSystemManagerHooks
        {
            AfterCanonicalReadInitialValidationAsync = _ =>
            {
                if (!armed || reached || observedFiles is null || expectedNpc is null)
                    return Task.CompletedTask;
                var corePath = observedFiles.ResolvePath(NpcCoreChangesContract.NpcCorePath);
                var skillsPath = observedFiles.ResolvePath("game_state/npcs/npc_skills.json");
                if (!File.Exists(corePath) || !File.Exists(skillsPath)) return Task.CompletedTask;
                var core = JsonNode.Parse(File.ReadAllText(corePath));
                var skills = JsonNode.Parse(File.ReadAllText(skillsPath)) as JsonObject;
                if (!JsonNode.DeepEquals(expectedNpc, core) || skills is null ||
                    new[] { "NPCActiveSkillChanges", "NPCPassiveSkillChanges", "NPCSkillMasteryChanges", "NPCPassiveSkillMasteryChanges" }
                        .Any(skills.ContainsKey))
                    return Task.CompletedTask;
                var indexPath = observedFiles.ResolvePath(MortalItemIdentityState.StatePath);
                if (!File.Exists(indexPath)) return Task.CompletedTask;
                var index = MortalItemIdentityState.Parse(JsonNode.Parse(File.ReadAllText(indexPath))!);
                if (index.Issues.Count != 0 || !index.EntriesByItemId.TryGetValue(
                        HeldTreatmentPipelineContext.SelectedItemId, out var selected) ||
                    selected["transitions"]!.AsArray().OfType<JsonObject>().Count(row =>
                        row["kind"]?.GetValue<string>() == "consume") != expectedConsumeTransitions)
                    return Task.CompletedTask;
                reached = true;
                throw new InvalidOperationException(cutMarker);
            }
        } : null;
        await using var context = await CreateHeldTreatmentPipelineContextAsync(
            fault: null,
            itemScenario: HeldTreatmentItemScenario.SelectedStack(publishNpcSkillChange: true),
            composePublication: false,
            hooks: hooks,
            configureBeforePreparation: async files =>
            {
                var core = await ReadJsonObjectAsync(files, NpcCoreChangesContract.NpcCorePath);
                var provider = ReadProvider(core);
                var baselineSkill = Assert.Single(provider["activeSkills"]!.AsArray().OfType<JsonObject>());
                baselineSkill["currentMasteryProgress"] = 0;
                baselineSkill["masteryProgressNeeded"] = 100;
                if (scenario == 10)
                    provider["passiveSkills"] = new JsonArray(new JsonObject
                    {
                        ["skillId"] = "skill_care_knowledge_t070b3", ["skillName"] = "Care Knowledge",
                        ["skillDescription"] = "Knowledge of field treatment", ["rarity"] = "Common",
                        ["type"] = "KnowledgeBased", ["group"] = "Medicine", ["masteryLevel"] = 1,
                        ["structuredBonuses"] = null, ["maxMasteryLevel"] = 5, ["knowledgeDomain"] = "Medicine",
                        ["unlockedActiveSkillsCount"] = 0, ["maxUnlockableActiveSkills"] = 2
                    });
                provider["level"] = 1;
                provider["experience"] = 99;
                provider["experienceForNextLevel"] = 100;
                provider["progressionProfile"] = JsonNode.Parse("""
                    {"schemaVersion":1,"mode":"standard","consequences":{
                      "skills":{"levelsPerAward":2,"allowedAwards":["newSkill","masteryIncrease"]},
                      "characteristics":{"mode":"currentWorld"}}}
                    """);
                await files.WriteFileAtomicAsync(NpcCoreChangesContract.NpcCorePath, core.ToJsonString());
                // The canonical provider already has Guaranteed Care. Prepare a clean original
                // command sidecar before the real signed live-turn capture, never repair a snapshot.
                await files.WriteFileAtomicAsync("game_state/npcs/npc_skills.json", """
                    {"NPCActiveSkillChanges":[],"NPCPassiveSkillChanges":[],"NPCSkillMasteryChanges":[],"NPCPassiveSkillMasteryChanges":[]}
                    """);
            });

        await context.ReleaseLeaseAsync();
        var manifest = await ReadJsonObjectAsync(context.FileSystem, "game_state/control/pending_turn_snapshot.json");
        var originalPath = manifest["files"]![NpcCoreChangesContract.NpcCorePath]!.GetValue<string>();
        var original = await ReadJsonObjectAsync(context.FileSystem, originalPath);
        Assert.Equal(1, ReadProvider(original)["level"]!.GetValue<int>());
        Assert.Equal(99, ReadProvider(original)["experience"]!.GetValue<int>());
        Assert.False(original.ContainsKey("NPCCoreChanges"));
        var originalSkillsPath = manifest["files"]!["game_state/npcs/npc_skills.json"]!.GetValue<string>();
        var originalSkills = await ReadJsonObjectAsync(context.FileSystem, originalSkillsPath);
        Assert.All(originalSkills, pair => Assert.Empty(pair.Value!.AsArray()));

        // One gained level requires no cadence award: the existing treatment row changes
        // displayName only, and must not be misrepresented as a new skill/mastery reward.
        var command = JsonNode.Parse("""
            {"NPCId":"field_medic_01","reason":"Experience from the accepted treatment","progression":{
              "source":{"sessionId":"session_t070b3_game_engine","requestId":"request_t070b3_game_engine","turnNumber":42},
              "experienceDelta":2,"level":2,"experience":1,"experienceForNextLevel":565}}
            """)!;
        var changedSkill = Assert.Single(ReadProvider(original)["activeSkills"]!.AsArray()
            .OfType<JsonObject>()).DeepClone().AsObject();
        changedSkill["displayName"] = HeldTreatmentPipelineContext.ChangedSkillDisplayName;
        if (scenario is 8 or 9 or 10 or 11)
        {
            // Two gained levels require exactly one award under levelsPerAward:2.
            // The documented cadence route uses the existing active mastery delta array.
            Assert.Equal(3, changedSkill["currentMasteryLevel"]!.GetValue<int>());
            var progression = command["progression"]!.AsObject();
            progression["experienceDelta"] = 566;
            progression["level"] = 3;
            progression["experience"] = 0;
            progression["experienceForNextLevel"] = 1558;
            progression["skillAwards"] = new JsonArray(new JsonObject
            {
                ["kind"] = scenario == 10 ? "passive" : "active",
                ["skillName"] = scenario == 10 ? JsonValue.Create("Care Knowledge") : changedSkill["skillName"]!.DeepClone(),
                ["skillId"] = scenario == 10 ? JsonValue.Create("skill_care_knowledge_t070b3") : changedSkill["skillId"]!.DeepClone(),
                ["award"] = "masteryIncrease"
            });
        }
        var proposal = new GameResponse
        {
            NPCCoreChanges = new[] { JsonSerializer.SerializeToElement(command) },
            NPCActiveSkillChanges = new[] { JsonSerializer.SerializeToElement(new JsonObject
            {
                ["npcId"] = HeldTreatmentPipelineContext.ProviderId,
                ["skillChanges"] = new JsonArray(changedSkill)
            }) }
        };
        if (scenario is 8 or 9 or 10 or 11)
        {
            // The held fixture already distributed a display-only full profile. Null is
            // omission in StateDistributor, so explicitly replace that pending array
            // before exercising the documented delta-only route.
            proposal.NPCActiveSkillChanges = Array.Empty<JsonElement>();
            proposal.NPCSkillMasteryChanges = new[] { JsonSerializer.SerializeToElement(new JsonObject
            {
                ["NPCId"] = HeldTreatmentPipelineContext.ProviderId,
                ["skillName"] = changedSkill["skillName"]!.DeepClone(),
                ["skillId"] = changedSkill["skillId"]!.DeepClone(),
                ["newMasteryLevel"] = scenario == 9 ? 3 : 4,
                ["newCurrentMasteryProgress"] = 0,
                ["newMasteryProgressNeeded"] = 100,
                ["masteryLeveledUp"] = scenario != 9
            }) };
        }
        if (scenario == 10)
        {
            proposal.NPCSkillMasteryChanges = null;
            proposal.NPCPassiveSkillMasteryChanges = new[] { JsonSerializer.SerializeToElement(new JsonObject
            {
                ["NPCId"] = HeldTreatmentPipelineContext.ProviderId,
                ["skillName"] = "Care Knowledge", ["skillId"] = "skill_care_knowledge_t070b3",
                ["newMasteryLevel"] = 2, ["newMaxMasteryLevel"] = 5, ["masteryLeveledUp"] = true
            }) };
        }
        if (scenario == 7)
            proposal.NPCActiveSkillChanges = null;
        await new StateDistributor(context.FileSystem, NullLogger<StateDistributor>.Instance)
            .DistributeAsync(proposal);
        if (scenario is 8 or 9 or 10 or 11)
        {
            var deltaOnlySidecar = await ReadJsonObjectAsync(context.FileSystem, "game_state/npcs/npc_skills.json");
            Assert.Empty(deltaOnlySidecar["NPCActiveSkillChanges"]!.AsArray());
        }
        if (scenario >= 4 && scenario != 11)
        {
            if (scenario == 5)
            {
                var tamperedCore = await ReadJsonObjectAsync(context.FileSystem, NpcCoreChangesContract.NpcCorePath);
                ReadProvider(tamperedCore)["displayName"] = "Unplanned canonical provider change";
                await context.FileSystem.WriteFileAtomicAsync(NpcCoreChangesContract.NpcCorePath, tamperedCore.ToJsonString());
            }
            else if (scenario == 6)
            {
                var tamperedSkills = await ReadJsonObjectAsync(context.FileSystem, "game_state/npcs/npc_skills.json");
                tamperedSkills["unrelatedMechanicalMetadata"] = new JsonObject { ["grantAuthority"] = true };
                await context.FileSystem.WriteFileAtomicAsync("game_state/npcs/npc_skills.json", tamperedSkills.ToJsonString());
            }
            else if (scenario == 7)
                File.Delete(context.FileSystem.ResolvePath("game_state/npcs/npc_skills.json"));
            var untouchedRoots = new Dictionary<string, byte[]?>();
            foreach (var path in new[] { MortalItemIdentityState.StatePath, WoundCarrierCatalog.PlayerPath,
                         WoundIdentityState.StatePath, WoundHistoryState.HistoryPath,
                         ResourceMaterializationContract.StatePath, ResourceMaterializationContract.HistoryPath })
                untouchedRoots[path] = await context.ReadFileBytesAsync(path);
            // Natural intake: leave the distributed commands raw. The real engine must
            // construct and admit its own plan; do not restore or compose a held plan here.
            var naturalTransitionsBefore = await CountSelectedItemConsumeTransitionsAsync(context);
            var naturalLogger = new SpiritualLifecycleTestLogger();
            var naturalInput = new NpcTreatmentRepairDiagnosticInput(context.FileSystem);
            var naturalEngine = CreateGameEngine(
                naturalInput, fileSystem: context.FileSystem, logger: naturalLogger);
            var naturalDisposition = await naturalEngine.ValidatePreparedNpcProgressionOutcomeAsync(
                "NPC progression treatment natural intake oracle", HeldTreatmentPipelineContext.Turn);
            if (scenario is 5 or 6 or 7 or 9)
            {
                Assert.NotEqual(AcceptedTurnValidationDisposition.Accepted, naturalDisposition);
                var refusalDiagnostic = naturalInput.RepairBeforeEscape ?? string.Empty;
                if (scenario == 5)
                {
                    // The candidate's direct-mutation guard can reject before owner intake.
                    Assert.Contains("npc_existing_core_direct_mutation_forbidden", refusalDiagnostic, StringComparison.Ordinal);
                    Assert.Contains("npc_core.json", refusalDiagnostic, StringComparison.Ordinal);
                }
                else if (scenario == 9)
                {
                    Assert.Contains("npc_progression_skill_award_unfulfilled", refusalDiagnostic, StringComparison.Ordinal);
                    Assert.Contains("skillAwards[0]", refusalDiagnostic, StringComparison.Ordinal);
                }
                else if (scenario == 7)
                {
                    Assert.Contains("npc_skill_treatment_original_sidecar_deleted", refusalDiagnostic, StringComparison.Ordinal);
                    Assert.Contains("npc_skills.json", refusalDiagnostic, StringComparison.Ordinal);
                }
                else
                {
                    // Unknown metadata survives candidate parsing but is forbidden by the
                    // intake-only exact original metadata comparison, surfaced by its catch.
                    Assert.Contains("mortal_wound_treatment_ordinary_input_invalid", refusalDiagnostic, StringComparison.Ordinal);
                    Assert.Contains("Treatment NPC intake changes unrelated sidecar metadata.", refusalDiagnostic, StringComparison.Ordinal);
                }
                foreach (var (path, bytes) in untouchedRoots)
                    Assert.Equal(bytes, await context.ReadFileBytesAsync(path));
                Assert.Equal(naturalTransitionsBefore, await CountSelectedItemConsumeTransitionsAsync(context));
                var rejectedCore = await ReadJsonObjectAsync(context.FileSystem, NpcCoreChangesContract.NpcCorePath);
                var rejectedProvider = ReadProvider(rejectedCore);
                Assert.Equal(1, rejectedProvider["level"]!.GetValue<int>());
                Assert.Equal(99, rejectedProvider["experience"]!.GetValue<int>());
                Assert.Equal(100, rejectedProvider["experienceForNextLevel"]!.GetValue<int>());
                Assert.Equal(2, ReadNpcItemCount(rejectedCore, HeldTreatmentPipelineContext.SelectedItemId));
                Assert.True(JsonNode.DeepEquals(ReadProvider(original)["activeSkills"], rejectedProvider["activeSkills"]));
                return;
            }
            Assert.True(naturalDisposition == AcceptedTurnValidationDisposition.Accepted,
                $"Natural treatment/progression disposition: {naturalDisposition}\n{naturalLogger.Describe()}\nRepair request before Escape:\n{naturalInput.RepairBeforeEscape ?? "<absent>"}");
            var naturalCore = await ReadJsonObjectAsync(context.FileSystem, NpcCoreChangesContract.NpcCorePath);
            var naturalProvider = ReadProvider(naturalCore);
            Assert.Equal(scenario is 8 or 10 ? 3 : 2, naturalProvider["level"]!.GetValue<int>());
            Assert.Equal(scenario is 8 or 10 ? 0 : 1, naturalProvider["experience"]!.GetValue<int>());
            Assert.Equal(scenario is 8 or 10 ? 1558 : 565, naturalProvider["experienceForNextLevel"]!.GetValue<int>());
            if (scenario == 8)
            {
                var masteredSkill = Assert.Single(naturalProvider["activeSkills"]!.AsArray().OfType<JsonObject>());
                Assert.Equal(changedSkill["skillId"]!.GetValue<string>(), masteredSkill["skillId"]!.GetValue<string>());
                Assert.Equal(changedSkill["skillName"]!.GetValue<string>(), masteredSkill["skillName"]!.GetValue<string>());
                Assert.Equal(4, masteredSkill["currentMasteryLevel"]!.GetValue<int>());
                Assert.Equal(0, masteredSkill["currentMasteryProgress"]!.GetValue<int>());
                Assert.Equal(100, masteredSkill["masteryProgressNeeded"]!.GetValue<int>());
            }
            if (scenario == 10)
            {
                var masteredPassive = Assert.Single(naturalProvider["passiveSkills"]!.AsArray().OfType<JsonObject>());
                Assert.Equal("skill_care_knowledge_t070b3", masteredPassive["skillId"]!.GetValue<string>());
                Assert.Equal("Care Knowledge", masteredPassive["skillName"]!.GetValue<string>());
                Assert.Equal(2, masteredPassive["masteryLevel"]!.GetValue<int>());
                Assert.Equal(5, masteredPassive["maxMasteryLevel"]!.GetValue<int>());
                Assert.True(JsonNode.DeepEquals(ReadProvider(original)["activeSkills"], naturalProvider["activeSkills"]));
            }
            Assert.False(naturalCore.ContainsKey("NPCCoreChanges"));
            Assert.Equal(1, ReadNpcItemCount(naturalCore, HeldTreatmentPipelineContext.SelectedItemId));
            Assert.Contains(naturalProvider["activeSkills"]!.AsArray().OfType<JsonObject>(),
                skill => skill["displayName"]?.GetValue<string>() == (scenario is 8 or 10
                    ? "Guaranteed Care" : HeldTreatmentPipelineContext.ChangedSkillDisplayName));
            Assert.Equal(naturalTransitionsBefore + 1, await CountSelectedItemConsumeTransitionsAsync(context));
            var naturalSkills = await ReadJsonObjectAsync(context.FileSystem, "game_state/npcs/npc_skills.json");
            foreach (var property in new[] { "NPCActiveSkillChanges", "NPCPassiveSkillChanges", "NPCSkillMasteryChanges", "NPCPassiveSkillMasteryChanges" })
                Assert.False(naturalSkills.ContainsKey(property), "Natural intake must consume NPC skill commands: " + property);
            return;
        }
        await context.AcquireLeaseAsync();
        var authorities = RestoreHeldTreatmentAuthorities(context);
        // The specialized treatment owner accepts its skill/item envelope only.
        // NPCCoreChanges remains in the real distributed raw core and is composed by
        // the existing final item baseline, not granted authority by this projection.
        var treatmentProposal = new GameResponse
        {
            NPCActiveSkillChanges = proposal.NPCActiveSkillChanges,
            NPCSkillMasteryChanges = proposal.NPCSkillMasteryChanges,
            NPCPassiveSkillMasteryChanges = proposal.NPCPassiveSkillMasteryChanges
        };
        var publication = WoundAcceptedTurnPlanner.ComposeMortalWoundTreatmentPublication(
            context.FileSystem, context.Lease, treatmentProposal,
            authorities.AcceptedState, authorities.Request, authorities.Resolution);
        Assert.True(publication.IsValid, DescribeValidationIssues(publication.Issues));
        var plan = Assert.IsType<AcceptedMechanicsPlan>(publication.Plan);
        Assert.True(AcceptedMechanicsPlanAuthority.TryPeekValidated(
            context.FileSystem, context.Lease, out var binding, out var cached));
        Assert.True(cached.Success, DescribeValidationIssues(cached.Issues));
        Assert.Same(plan, cached.Plan);
        context.SetCurrentAuthorities(authorities.AcceptedState, authorities.Request,
            authorities.Resolution, plan, binding);

        var npcAfterImage = Assert.Single(context.Plan.OwnerCompanionAfterImages,
            pair => pair.Key == NpcCoreChangesContract.NpcCorePath).Value;
        Assert.Equal(scenario == 11 ? 3 : 2, ReadProvider(npcAfterImage)["level"]!.GetValue<int>());
        Assert.Equal(scenario == 11 ? 0 : 1, ReadProvider(npcAfterImage)["experience"]!.GetValue<int>());
        Assert.Equal(scenario == 11 ? 1558 : 565, ReadProvider(npcAfterImage)["experienceForNextLevel"]!.GetValue<int>());
        Assert.Equal(1, ReadNpcItemCount(npcAfterImage, HeldTreatmentPipelineContext.SelectedItemId));
        Assert.Contains(ReadProvider(npcAfterImage)["activeSkills"]!.AsArray().OfType<JsonObject>(),
            skill => skill["displayName"]?.GetValue<string>() == (scenario == 11
                ? "Guaranteed Care" : HeldTreatmentPipelineContext.ChangedSkillDisplayName));
        if (scenario == 11)
        {
            var finalMastery = Assert.Single(ReadProvider(npcAfterImage)["activeSkills"]!.AsArray().OfType<JsonObject>());
            Assert.Equal(4, finalMastery["currentMasteryLevel"]!.GetValue<int>());
            Assert.Equal(0, finalMastery["currentMasteryProgress"]!.GetValue<int>());
            Assert.Equal(100, finalMastery["masteryProgressNeeded"]!.GetValue<int>());
        }
        var itemTransitionsBefore = await CountSelectedItemConsumeTransitionsAsync(context);
        if (scenario is 2 or 3)
        {
            var before = await CaptureExactTreatmentTransactionBytesAsync(context);
            const string skillPath = "game_state/npcs/npc_skills.json";
            var rawSkills = Assert.IsType<byte[]>(await context.ReadFileBytesAsync(skillPath));
            var backups = manifest["files"]!.AsObject().ToDictionary(pair => pair.Key,
                pair => pair.Value!.GetValue<string>(), StringComparer.Ordinal);
            await context.ReleaseLeaseAsync();
            var refreshed = await AcceptedTurnCanonicalStateRefresh.NormalizeAndValidateWithPlanAsync(
                context.FileSystem,
                new CanonicalStateNormalizer(context.FileSystem, NullLogger<CanonicalStateNormalizer>.Instance),
                new ValidationService(context.FileSystem, NullLogger<ValidationService>.Instance), backups);
            Assert.DoesNotContain(refreshed.Issues, issue => issue.Severity == IssueSeverity.Error);
            var transaction = Assert.IsType<MortalWoundTreatmentResourcePublicationTransaction>(
                refreshed.TreatmentResourcePublicationTransaction);
            Assert.True(JsonNode.DeepEquals(npcAfterImage,
                await ReadJsonObjectAsync(context.FileSystem, NpcCoreChangesContract.NpcCorePath)));
            var consumedSkills = await ReadJsonObjectAsync(context.FileSystem, skillPath);
            Assert.False(consumedSkills.ContainsKey("NPCActiveSkillChanges"));
            Assert.Equal(itemTransitionsBefore + 1, await CountSelectedItemConsumeTransitionsAsync(context));
            // The real coordinator has released its publication lease. Drift a retained
            // publication dependency before actual final settlement, not before admission.
            var worldBefore = await context.ReadFileBytesAsync(MortalNpcWorldProgressionPolicy.Path);
            try
            {
                if (scenario == 2)
                    await context.FileSystem.WriteFileAtomicBytesAsync(skillPath, rawSkills);
                else
                {
                    const string changedPolicy = """
                        {"npcProgression":{"schemaVersion":1,"characteristics":{
                          "mode":"fixedIncrements","perLevel":{"strength":1}}}}
                        """;
                    Assert.True(MortalNpcWorldProgressionPolicy.Parse(changedPolicy).IsValid);
                    await context.FileSystem.WriteFileAtomicAsync(MortalNpcWorldProgressionPolicy.Path, changedPolicy);
                }
                var completion = await transaction.CompleteAsync(context.FileSystem);
                Assert.False(completion.IsValid);
                Assert.Equal(MortalWoundTreatmentPublicationTransactionOutcome.PublishedAgreementChanged, completion.Outcome);
            }
            finally
            {
                // World lore is an external dependency, not transaction rollback output.
                // Remove the injected drift ourselves before asking its real owner to compensate.
                if (scenario == 3)
                {
                    if (worldBefore is null)
                        File.Delete(context.FileSystem.ResolvePath(MortalNpcWorldProgressionPolicy.Path));
                    else
                        await context.FileSystem.WriteFileAtomicBytesAsync(MortalNpcWorldProgressionPolicy.Path, worldBefore);
                }
            }
            // Complete refuses but does not compensate automatically. Exercise its actual
            // existing compensation owner, rather than restoring bytes in the test.
            var compensation = await transaction.CompensateAsync(context.FileSystem);
            Assert.True(compensation.IsValid, DescribeValidationIssues(compensation.Issues));
            await AssertExactTreatmentTransactionBytesAsync(context, before);
            Assert.Equal(itemTransitionsBefore, await CountSelectedItemConsumeTransitionsAsync(context));
            return;
        }
        if (lateCut)
        {
            var before = await CaptureExactTreatmentTransactionBytesAsync(context);
            var backups = manifest["files"]!.AsObject().ToDictionary(pair => pair.Key,
                pair => pair.Value!.GetValue<string>(), StringComparer.Ordinal);
            var signedPaths = backups.Values.Append("game_state/control/pending_turn_snapshot.json")
                .Append(PendingTurnSnapshotAuthority.AuthorityPath).Append("input/turn_request.json")
                .Distinct(StringComparer.Ordinal).ToArray();
            var signedBefore = new Dictionary<string, byte[]?>();
            foreach (var path in signedPaths)
                signedBefore.Add(path, await context.ReadFileBytesAsync(path));
            observedFiles = context.FileSystem;
            expectedNpc = npcAfterImage.DeepClone();
            expectedConsumeTransitions = itemTransitionsBefore + 1;
            await context.ReleaseLeaseAsync();
            armed = true;
            var failure = await Record.ExceptionAsync(() =>
                AcceptedTurnCanonicalStateRefresh.NormalizeAndValidateWithPlanAsync(
                    context.FileSystem,
                    new CanonicalStateNormalizer(context.FileSystem, NullLogger<CanonicalStateNormalizer>.Instance),
                    new ValidationService(context.FileSystem, NullLogger<ValidationService>.Instance), backups));
            armed = false;
            Assert.True(reached, "The held transaction must publish its exact NPC item/skill/progression root and consume the skill sidecar before the cut.");
            Assert.NotNull(failure);
            Assert.Contains(cutMarker, failure!.ToString(), StringComparison.Ordinal);
            await AssertExactTreatmentTransactionBytesAsync(context, before);
            foreach (var (path, bytes) in signedBefore)
                Assert.Equal(bytes, await context.ReadFileBytesAsync(path));
            Assert.Equal(itemTransitionsBefore, await CountSelectedItemConsumeTransitionsAsync(context));
            return;
        }
        await context.ReleaseLeaseAsync();
        var logger = new SpiritualLifecycleTestLogger();
        var repairInput = new NpcTreatmentRepairDiagnosticInput(context.FileSystem);
        var engine = CreateGameEngine(
            repairInput, fileSystem: context.FileSystem, logger: logger);
        var disposition = await engine.ValidatePreparedNpcProgressionOutcomeAsync(
            "NPC progression plus treatment skill/item oracle", HeldTreatmentPipelineContext.Turn);
        if (disposition != AcceptedTurnValidationDisposition.Accepted)
        {
            var repair = await context.FileSystem.ReadFileAsync("game_state/control/validation_repair_request.json");
            var diagnostic = await context.FileSystem.ReadFileAsync("game_state/control/validation_diagnostic_failure_report.json");
            Assert.True(false, $"Treatment/progression disposition: {disposition}\n{logger.Describe()}\nRepair request before Escape:\n{repairInput.RepairBeforeEscape ?? "<absent>"}\nRetained repair request:\n{repair ?? "<absent>"}\nDiagnostic failure report:\n{diagnostic ?? "<absent>"}");
        }
        var persisted = await ReadJsonObjectAsync(context.FileSystem, NpcCoreChangesContract.NpcCorePath);
        Assert.True(JsonNode.DeepEquals(npcAfterImage, persisted));
        Assert.False(persisted.ContainsKey("NPCCoreChanges"));
        Assert.Equal(itemTransitionsBefore + 1, await CountSelectedItemConsumeTransitionsAsync(context));
        var consumed = await ReadJsonObjectAsync(context.FileSystem, "game_state/npcs/npc_skills.json");
        foreach (var property in new[] { "NPCActiveSkillChanges", "NPCPassiveSkillChanges", "NPCSkillMasteryChanges", "NPCPassiveSkillMasteryChanges" })
            Assert.False(consumed.ContainsKey(property), "Accepted treatment must not retain replayable NPC skill commands: " + property);
    }

    // Read the owner's already-authored diagnostic before Escape cleanup removes it.
    // This observes output only; it does not re-run validation or modify authority.
    private sealed class NpcTreatmentRepairDiagnosticInput(FileSystemManager files) : IConsoleInputSource
    {
        private bool _escapeRead;
        public string? RepairBeforeEscape { get; private set; }
        public bool IsScripted => true;
        public bool KeyAvailable => !_escapeRead;
        public ConsoleKeyInfo ReadKey(bool intercept = true)
        {
            var path = Path.Combine(files.GameSessionPath, "game_state", "control", "validation_repair_request.json");
            RepairBeforeEscape = File.Exists(path) ? File.ReadAllText(path) : null;
            _escapeRead = true;
            return Key(ConsoleKey.Escape);
        }
        public string? ReadLine() => string.Empty;
        public void AssertCompleted() { } // Escape is diagnostic-only; success reads no key.
    }

}
