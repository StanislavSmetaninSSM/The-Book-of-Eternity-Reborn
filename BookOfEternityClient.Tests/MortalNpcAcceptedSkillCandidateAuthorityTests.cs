using System.Text.Json.Nodes;
using BookOfEternityClient.Services;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed class MortalNpcAcceptedSkillCandidateAuthorityTests
{
    [Theory]
    [InlineData("training", true)]
    [InlineData("trade", true)]
    [InlineData("training", false)]
    [InlineData("trade", false)]
    public void Compose_DedicatedRawPatch_RequiresOriginalRequestAuthority(string kind, bool authorize)
    {
        var original = Core();
        var (current, tradePending, trainingPending) = DedicatedPatch(original, kind);
        // These detached pending strings stand for authenticated ORIGINAL request DATA.
        // Construct authority from raw current carriers, never the final skill candidate.
        var authority = MortalActorAcceptedTurnAuthority.Create(current,
            authorize ? tradePending : null, authorize ? trainingPending : null);
        var rawBytes = current.ToJsonString();
        var beforeBytes = original.ToJsonString();
        var existingEvaluation = NpcCoreChangesContract.Evaluate(current, original, CoreAuthority(),
            ValidationService.ValidateNpcCoreFateCardsAgainstProductionContract, true, authority);
        Assert.Equal(authorize, existingEvaluation.Issues.Count == 0);

        var result = MortalNpcAcceptedSkillCandidate.Compose(original, new(), current, new(), CoreAuthority(), authority);

        Assert.Equal(authorize, result.IsValid);
        Assert.Equal(rawBytes, current.ToJsonString());
        Assert.Equal(beforeBytes, original.ToJsonString());
        if (authorize)
        {
            Assert.True(JsonNode.DeepEquals(current, result.Core));
            Assert.Empty(result.Sidecar!);
        }
        else
        {
            Assert.Null(result.Core);
            Assert.Null(result.Sidecar);
            Assert.Contains(result.Issues, issue => issue.Code == "npc_existing_core_direct_mutation_forbidden");
        }
    }

    [Theory]
    [InlineData("training")]
    [InlineData("trade")]
    public void Compose_DedicatedAuthority_DoesNotAuthorizeUnrelatedCoreSkillRewrite(string kind)
    {
        var original = Core();
        var (current, tradePending, trainingPending) = DedicatedPatch(original, kind);
        var authority = MortalActorAcceptedTurnAuthority.Create(current, tradePending, trainingPending);
        current["NPCsInScene"]![0]!["activeSkills"] = new JsonArray(new JsonObject { ["skillName"] = "Unapproved" });
        var result = MortalNpcAcceptedSkillCandidate.Compose(original, new(), current, new(), CoreAuthority(), authority);
        Assert.False(result.IsValid);
        Assert.Null(result.Core);
        Assert.Contains(result.Issues, issue => issue.Code == "npc_existing_core_direct_mutation_forbidden");
    }

    // Same request/receipt shapes as NpcCoreChangesTests' exact dedicated-authority fixtures;
    // no world, source authentication, materialization or publication is simulated here.
    private static (JsonObject Current, string? Trade, string? Training) DedicatedPatch(JsonObject original, string kind)
    {
        var current = original.DeepClone().AsObject();
        var actor = current["NPCsInScene"]![0]!.AsObject();
        if (kind == "training")
        {
            var hash = TrainingService.ComputeSourceSnapshotHash(actor);
            actor["trainingShowcase"] = new JsonObject
            {
                ["schemaVersion"] = 1, ["requestId"] = "training_request_bound",
                ["requestKind"] = "mortal_teacher_showcase", ["sourceActorId"] = "npc_mentor",
                ["sourceActorName"] = "Test mentor", ["sourceActorSnapshotHash"] = hash,
                ["preparedAtTurn"] = 42, ["offers"] = new JsonArray()
            };
            return (current, null, new JsonObject
            {
                ["requests"] = new JsonArray(new JsonObject
                {
                    ["requestId"] = "training_request_bound", ["requestKind"] = "mortal_teacher_showcase",
                    ["sourceActorId"] = "npc_mentor", ["sourceActorName"] = "Test mentor",
                    ["sourceActorKind"] = "mortal_npc", ["realm"] = "mortal_world",
                    ["createdAtTurn"] = 42, ["createdAtUtc"] = "2026-07-22T00:00:00Z",
                    ["sourceActorSnapshotHash"] = hash, ["reason"] = "Refresh exact teacher showcase"
                })
            }.ToJsonString());
        }
        actor["tradeInventory"] = new JsonObject
        {
            ["tradeCycleId"] = "cycle_request_bound", ["generatedAtWorldDate"] = 10,
            ["refreshAfterWorldDate"] = 20, ["generationTradeTier"] = "Good", ["pricingTradeTier"] = "Neutral",
            ["items"] = new JsonArray(new JsonObject { ["slotId"] = "slot_1", ["merchantProfile"] = "GeneralGoods" })
        };
        current["UpdateNpcTradeInventoryReceipts"] = new JsonArray(new JsonObject
        {
            ["requestId"] = "npc_trade_request_bound", ["npcId"] = "npc_mentor", ["npcName"] = "Test mentor",
            ["tradeCycleId"] = "cycle_request_bound", ["merchantProfile"] = "GeneralGoods",
            ["status"] = "ready", ["itemCount"] = 1, ["resolvedAtTurn"] = 42,
            ["resolvedAtUtc"] = "2026-07-22T00:01:00Z"
        });
        return (current, new JsonObject
        {
            ["requests"] = new JsonArray(new JsonObject
            {
                ["requestId"] = "npc_trade_request_bound", ["npcId"] = "npc_mentor", ["npcName"] = "Test mentor",
                ["merchantProfile"] = "GeneralGoods", ["tradeCycleId"] = "cycle_request_bound",
                ["derivedTradeSlotCount"] = 1, ["createdAtTurn"] = 42, ["createdAtUtc"] = "2026-07-22T00:00:00Z",
                ["createdAtWorldDate"] = 10, ["refreshAfterWorldDate"] = 20
            })
        }.ToJsonString(), null);
    }

    private static JsonObject Core() => new()
    {
        ["NPCsInScene"] = new JsonArray(new JsonObject
        {
            ["NPCId"] = "npc_mentor", ["name"] = "Test mentor",
            ["activeSkills"] = new JsonArray(), ["passiveSkills"] = new JsonArray()
        })
    };

    private static NpcCoreChangesContract.Authority CoreAuthority() =>
        new(new(StringComparer.Ordinal), new(StringComparer.Ordinal), new(StringComparer.Ordinal), new(StringComparer.Ordinal));
}
