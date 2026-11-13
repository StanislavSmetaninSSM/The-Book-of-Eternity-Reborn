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
    public async Task GuaranteedItemConsumption_NpcProgressionAndTreatmentSkillShareAcceptedRoot()
    {
        await using var context = await CreateHeldTreatmentPipelineContextAsync(
            fault: null,
            itemScenario: HeldTreatmentItemScenario.SelectedStack(publishNpcSkillChange: true),
            composePublication: false,
            configureBeforePreparation: async files =>
            {
                var core = await ReadJsonObjectAsync(files, NpcCoreChangesContract.NpcCorePath);
                var provider = ReadProvider(core);
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
        await new StateDistributor(context.FileSystem, NullLogger<StateDistributor>.Instance)
            .DistributeAsync(new GameResponse { NPCCoreChanges = new[] { JsonSerializer.SerializeToElement(command) } });
        await context.AcquireLeaseAsync();
        await RestoreHeldTreatmentAndComposeSameSemanticPlanAsync(context);

        var npcAfterImage = Assert.Single(context.Plan.OwnerCompanionAfterImages,
            pair => pair.Key == NpcCoreChangesContract.NpcCorePath).Value;
        Assert.Equal(2, ReadProvider(npcAfterImage)["level"]!.GetValue<int>());
        Assert.Equal(1, ReadProvider(npcAfterImage)["experience"]!.GetValue<int>());
        Assert.Equal(565, ReadProvider(npcAfterImage)["experienceForNextLevel"]!.GetValue<int>());
        Assert.Equal(1, ReadNpcItemCount(npcAfterImage, HeldTreatmentPipelineContext.SelectedItemId));
        Assert.Contains(ReadProvider(npcAfterImage)["activeSkills"]!.AsArray().OfType<JsonObject>(),
            skill => skill["displayName"]?.GetValue<string>() == HeldTreatmentPipelineContext.ChangedSkillDisplayName);
        var itemTransitionsBefore = await CountSelectedItemConsumeTransitionsAsync(context);
        await context.ReleaseLeaseAsync();
        var logger = new SpiritualLifecycleTestLogger();
        var engine = CreateGameEngine(
            new QueuedConsoleInputSource(new[] { Key(ConsoleKey.Escape) }), fileSystem: context.FileSystem, logger: logger);
        var disposition = await engine.ValidatePreparedNpcProgressionOutcomeAsync(
            "NPC progression plus treatment skill/item oracle", HeldTreatmentPipelineContext.Turn);
        if (disposition != AcceptedTurnValidationDisposition.Accepted)
        {
            var repair = await context.FileSystem.ReadFileAsync("game_state/control/validation_repair_request.json");
            Assert.True(false, $"Treatment/progression disposition: {disposition}\n{logger.Describe()}\nRetained repair request:\n{repair ?? "<absent>"}");
        }
        var persisted = await ReadJsonObjectAsync(context.FileSystem, NpcCoreChangesContract.NpcCorePath);
        Assert.True(JsonNode.DeepEquals(npcAfterImage, persisted));
        Assert.False(persisted.ContainsKey("NPCCoreChanges"));
        Assert.Equal(itemTransitionsBefore + 1, await CountSelectedItemConsumeTransitionsAsync(context));
        var consumed = await ReadJsonObjectAsync(context.FileSystem, "game_state/npcs/npc_skills.json");
        foreach (var property in new[] { "NPCActiveSkillChanges", "NPCPassiveSkillChanges", "NPCSkillMasteryChanges", "NPCPassiveSkillMasteryChanges" })
            Assert.False(consumed.ContainsKey(property), "Accepted treatment must not retain replayable NPC skill commands: " + property);
    }
}
