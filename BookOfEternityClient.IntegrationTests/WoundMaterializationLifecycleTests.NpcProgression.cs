using System.Text.Json.Nodes;
using BookOfEternityClient.IO;
using BookOfEternityClient.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed partial class WoundMaterializationLifecycleTests
{
    [Fact]
    public async Task SignedPlayerWound_WithNpcProgressionAndNewSkill_PublishesOneCompleteTurn()
    {
        const string npcId = "npc_progression_wound_companion";
        const string skillName = "Archive";
        const string corePath = NpcCoreChangesContract.NpcCorePath;
        const string sidecarPath = MortalNpcAcceptedSkillInput.SidecarPath;
        await using var context = await CreatePlayerContextAsync();
        await context.WriteExactJsonAsync("game_state/meta/soul_state.json",
            """{"currentRealm":"Mortal World"}""");
        await context.WriteExactJsonAsync(MortalItemIdentityState.StatePath,
            MortalItemIdentityState.CreateEmptyRoot().ToJsonString());
        var original = MortalActorTestFixtures.CreateNpcCoreRoot(npcId);
        var originalActor = original["NPCsInScene"]![0]!;
        originalActor["level"] = 1;
        originalActor["experience"] = 99;
        originalActor["experienceForNextLevel"] = 100;
        originalActor["progressionProfile"] = JsonNode.Parse("""
            {"schemaVersion":1,"mode":"standard","consequences":{
              "skills":{"levelsPerAward":2,"allowedAwards":["newSkill","masteryIncrease"]},
              "characteristics":{"mode":"currentWorld"}}}
            """);
        await context.WriteExactJsonAsync(corePath, original.ToJsonString());
        await context.WriteExactJsonAsync(sidecarPath, "{}");
        var authority = await CreateSignedAuthorityAsync(context, maximumSeverityRank: 2,
            extraSnapshotPaths: new[] { corePath, sidecarPath });
        var manifest = Assert.IsType<JsonObject>(await context.ReadJsonAsync(
            "game_state/control/pending_turn_snapshot.json"));
        var backups = manifest["files"]!.AsObject().ToDictionary(pair => pair.Key,
            pair => pair.Value!.GetValue<string>(), StringComparer.Ordinal);
        var authenticatedPaths = new[]
        {
            "input/turn_request.json", "game_state/control/pending_turn_snapshot.json",
            backups[corePath], backups[sidecarPath]
        };
        var authenticatedBefore = await context.CaptureAsync(authenticatedPaths);
        var request = Assert.IsType<JsonObject>(await context.ReadJsonAsync("input/turn_request.json"));

        // Use the complete current Mortal route, as the real quest+wound publication
        // fixture does; the low-level physical proposal alone has no treatment route.
        var proposal = CreateRepairRoundtripProposal("treatment");
        proposal["severity"] = "I";
        var decision = Decision("materialize", proposal);
        decision["opportunityRef"] = authority.Opportunity.PublicRef;
        var response = Response(decision);
        response.NPCCoreChanges = new[] { ToElement(new JsonObject
        {
            ["NPCId"] = npcId, ["reason"] = "Completed the agreed practice objective",
            ["progression"] = new JsonObject
            {
                ["source"] = new JsonObject
                {
                    ["sessionId"] = request["sessionId"]!.DeepClone(),
                    ["requestId"] = request["requestId"]!.DeepClone(),
                    ["turnNumber"] = request["turnNumber"]!.DeepClone()
                },
                ["experienceDelta"] = 566, ["level"] = 3, ["experience"] = 0,
                ["experienceForNextLevel"] = 1558,
                ["skillAwards"] = new JsonArray(new JsonObject
                {
                    ["kind"] = "passive", ["skillName"] = skillName, ["award"] = "newSkill"
                })
            }
        }) };
        response.NPCPassiveSkillChanges = new[] { ToElement(new JsonObject
        {
            ["NPCId"] = npcId, ["skillChanges"] = new JsonArray(new JsonObject
            {
                ["skillName"] = skillName, ["skillDescription"] = "Archive knowledge", ["rarity"] = "Common",
                ["type"] = "KnowledgeBased", ["group"] = "Archives", ["masteryLevel"] = 1,
                ["maxMasteryLevel"] = 5, ["structuredBonuses"] = null, ["knowledgeDomain"] = "Archives",
                ["unlockedActiveSkillsCount"] = 0, ["maxUnlockableActiveSkills"] = 2
            })
        }) };
        var composed = WoundResponseInputComposer.Compose(authority.Binding,
            new[] { authority.Opportunity }, response.WoundDecisions, response.Response,
            Array.Empty<WoundOpportunityDecisionReceipt>());
        Assert.True(composed.Success, Describe(composed.Issues));
        await new StateDistributor(context.FileSystem, NullLogger<StateDistributor>.Instance)
            .DistributeAsync(response, composed);
        AssertNoQuestPublicationErrors(await context.Validator.ValidateNpcCoreChangesBeforeNormalizationAsync());
        AssertNoQuestPublicationErrors(await context.Validator.ValidateAcceptedTurnRawMortalItemMaterializationAsync());
        AssertNoQuestPublicationErrors(await context.Validator.ValidateAcceptedTurnRawResourceMaterializationAsync());
        await using (var lease = await context.FileSystem.AcquireCanonicalWriteLeaseAsync())
        {
            Assert.True(AcceptedMechanicsPlanAuthority.TryPeekValidated(context.FileSystem, lease,
                out _, out var admission));
            Assert.True(admission.Success, Describe(admission.Issues));
            Assert.NotNull(admission.Plan!.WoundStageBundle);
            Assert.NotNull(admission.Plan.EffectPlan!.NpcSkillInput);
            Assert.False(admission.Plan.HasWholeCarrierAfterImage(corePath));
            Assert.Equal(3, admission.Plan.EffectPlan.NpcSkillInput!.CandidateCore["NPCsInScene"]![0]!["level"]!.GetValue<int>());
        }

        var result = await AcceptedTurnCanonicalStateRefresh.NormalizeAndValidateWithPlanAsync(
            context.FileSystem, context.Normalizer, context.Validator, backups);

        AssertNoQuestPublicationErrors(result.Issues);
        Assert.Null(result.TreatmentResourcePublicationTransaction);
        Assert.NotNull(result.MechanicsPlan?.WoundStageBundle);
        var publishedCore = Assert.IsType<JsonObject>(await context.ReadJsonAsync(corePath));
        Assert.False(publishedCore.ContainsKey("NPCCoreChanges"));
        var actor = Assert.Single(publishedCore["NPCsInScene"]!.AsArray())!;
        Assert.Equal(3, actor["level"]!.GetValue<int>());
        Assert.Equal(0, actor["experience"]!.GetValue<int>());
        Assert.Equal(1558, actor["experienceForNextLevel"]!.GetValue<int>());
        Assert.Equal(skillName, Assert.Single(actor["passiveSkills"]!.AsArray())!["skillName"]!.GetValue<string>());
        Assert.Empty(Assert.IsType<JsonObject>(await context.ReadJsonAsync(sidecarPath)));
        var wounds = Assert.IsType<JsonObject>(await context.ReadJsonAsync(WoundCarrierCatalog.PlayerPath));
        var wound = Assert.Single(wounds["activeWounds"]!.AsArray())!;
        var woundId = wound["woundId"]!.GetValue<string>();
        var rootBinding = Assert.Single(wound["consequences"]!["ownedEffectSources"]!["rootBindings"]!.AsArray())!;
        var effects = Assert.IsType<JsonObject>(await context.ReadJsonAsync(EffectCarrierCatalog.PlayerPath));
        Assert.Contains(effects["activeEffects"]!.AsArray().OfType<JsonObject>(), effect =>
            effect["effectId"]!.GetValue<string>() == rootBinding["effectId"]!.GetValue<string>() &&
            effect["source"]!["sourceId"]!.GetValue<string>() == woundId);
        var index = Assert.IsType<JsonObject>(await context.ReadJsonAsync(WoundIdentityState.StatePath));
        Assert.Equal(woundId, Assert.Single(index["entries"]!.AsArray())!["woundId"]!.GetValue<string>());
        var history = Assert.IsType<JsonObject>(await context.ReadJsonAsync(WoundHistoryState.HistoryPath));
        Assert.Equal(woundId, Assert.Single(history["transitions"]!.AsArray())!["woundId"]!.GetValue<string>());
        Assert.Null(await context.ReadJsonAsync(AcceptedMechanicsPlan.WoundCommandPath));
        foreach (var (path, snapshot) in authenticatedBefore)
            Assert.Equal(snapshot.Bytes, await context.FileSystem.ReadFileBytesAsync(path));
    }
}
