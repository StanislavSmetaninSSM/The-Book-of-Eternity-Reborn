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
    [Fact]
    public Task GuaranteedItemConsumption_NpcProgressionAndSkillUsesNaturalOwnerIntake() =>
        GuaranteedItemConsumption_NpcProgressionAndTreatmentSkillShareAcceptedRoot(4);

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    public async Task GuaranteedItemConsumption_NpcProgressionAndTreatmentSkillShareAcceptedRoot(int scenario)
    {
        var lateCut = scenario == 1;
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
        var proposal = new GameResponse
        {
            NPCCoreChanges = new[] { JsonSerializer.SerializeToElement(command) },
            NPCActiveSkillChanges = new[] { JsonSerializer.SerializeToElement(new JsonObject
            {
                ["npcId"] = HeldTreatmentPipelineContext.ProviderId,
                ["skillChanges"] = new JsonArray(changedSkill)
            }) }
        };
        await new StateDistributor(context.FileSystem, NullLogger<StateDistributor>.Instance)
            .DistributeAsync(proposal);
        if (scenario == 4)
        {
            // Natural intake: leave the distributed commands raw. The real engine must
            // construct and admit its own plan; do not restore or compose a held plan here.
            var naturalTransitionsBefore = await CountSelectedItemConsumeTransitionsAsync(context);
            var naturalLogger = new SpiritualLifecycleTestLogger();
            var naturalInput = new NpcTreatmentRepairDiagnosticInput(context.FileSystem);
            var naturalEngine = CreateGameEngine(
                naturalInput, fileSystem: context.FileSystem, logger: naturalLogger);
            var naturalDisposition = await naturalEngine.ValidatePreparedNpcProgressionOutcomeAsync(
                "NPC progression treatment natural intake oracle", HeldTreatmentPipelineContext.Turn);
            Assert.True(naturalDisposition == AcceptedTurnValidationDisposition.Accepted,
                $"Natural treatment/progression disposition: {naturalDisposition}\n{naturalLogger.Describe()}\nRepair request before Escape:\n{naturalInput.RepairBeforeEscape ?? "<absent>"}");
            var naturalCore = await ReadJsonObjectAsync(context.FileSystem, NpcCoreChangesContract.NpcCorePath);
            var naturalProvider = ReadProvider(naturalCore);
            Assert.Equal(2, naturalProvider["level"]!.GetValue<int>());
            Assert.Equal(1, naturalProvider["experience"]!.GetValue<int>());
            Assert.Equal(565, naturalProvider["experienceForNextLevel"]!.GetValue<int>());
            Assert.False(naturalCore.ContainsKey("NPCCoreChanges"));
            Assert.Equal(1, ReadNpcItemCount(naturalCore, HeldTreatmentPipelineContext.SelectedItemId));
            Assert.Contains(naturalProvider["activeSkills"]!.AsArray().OfType<JsonObject>(),
                skill => skill["displayName"]?.GetValue<string>() == HeldTreatmentPipelineContext.ChangedSkillDisplayName);
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
        var treatmentProposal = new GameResponse { NPCActiveSkillChanges = proposal.NPCActiveSkillChanges };
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
        Assert.Equal(2, ReadProvider(npcAfterImage)["level"]!.GetValue<int>());
        Assert.Equal(1, ReadProvider(npcAfterImage)["experience"]!.GetValue<int>());
        Assert.Equal(565, ReadProvider(npcAfterImage)["experienceForNextLevel"]!.GetValue<int>());
        Assert.Equal(1, ReadNpcItemCount(npcAfterImage, HeldTreatmentPipelineContext.SelectedItemId));
        Assert.Contains(ReadProvider(npcAfterImage)["activeSkills"]!.AsArray().OfType<JsonObject>(),
            skill => skill["displayName"]?.GetValue<string>() == HeldTreatmentPipelineContext.ChangedSkillDisplayName);
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
