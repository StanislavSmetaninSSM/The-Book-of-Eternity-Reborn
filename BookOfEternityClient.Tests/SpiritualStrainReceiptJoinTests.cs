using System.Text.Json.Nodes;
using BookOfEternityClient.Services;
using Xunit;

namespace BookOfEternityClient.Tests;

/// <summary>Checks the retained comparison's parent decision, never original snapshot or live authority.</summary>
public sealed class SpiritualStrainReceiptJoinTests
{
    [Theory]
    [InlineData("foreign-source")]
    [InlineData("declined-parent")]
    [InlineData("different-wound")]
    [InlineData("different-transition")]
    [InlineData("different-rank")]
    public void ReceiptRefusesUnprovedParentAfterIntrinsicAndBaselinePass(string change)
    {
        var root = CreateLedger();
        var baseline = SpiritualWoundOpportunityReceiptState.Parse(root.ToJsonString(), "unit.strainLedger");
        Assert.True(baseline.IsValid, string.Join("\n", baseline.Issues));
        var parent = root["decisions"]![0]!.AsObject();
        switch (change)
        {
            case "foreign-source":
                var source = root["sources"]![1]!["witness"]!.AsObject();
                source["strainPreimage"]!["parentSourceId"] = "spiritual_source_" + new string('a', 64);
                SealSourceAndDecision(root, 1);
                break;
            case "declined-parent":
                parent["decision"] = "none";
                parent["selectedSeverityRank"] = null;
                parent["woundId"] = null;
                parent["transitionId"] = null;
                break;
            case "different-wound": parent["woundId"] = "another-wound"; break;
            case "different-transition": parent["transitionId"] = "another-transition"; break;
            case "different-rank": parent["selectedSeverityRank"] = 2; break;
            default: throw new ArgumentOutOfRangeException(nameof(change));
        }
        parent["decisionFingerprint"] = SpiritualWoundStateJson.Hash(parent, "decision", "decisionFingerprint");
        var before = root.ToJsonString();
        var result = SpiritualWoundOpportunityReceiptState.Parse(before, "unit.strainLedger");
        Assert.False(result.IsValid);
        Assert.Null(result.State);
        Assert.Equal(before, root.ToJsonString());
    }

    /// <summary>Builds a valid two-source packet ledger with an earlier genuine materialize-III decision.</summary>
    internal static JsonObject CreateLedger()
    {
        var (source, parent, wound, _) = SpiritualStrainPreimageDataTests.CreateData();
        var instance = new JsonObject
        {
            ["instanceId"] = "", ["ordinal"] = 1, ["displayConflictId"] = "conflict-a", ["realm"] = "chaos_sea",
            ["startSessionId"] = "session-a", ["startRequestId"] = "request-a", ["startSnapshotToken"] = "snapshot-a",
            ["startTurn"] = 42,
            ["baselineConflictFingerprint"] = "sha256:" + new string('a', 64), ["instanceFingerprint"] = ""
        };
        var hash = SpiritualWoundConflictInstanceState.ComputeRowFingerprint(instance, "instance");
        instance["instanceId"] = "spiritual_instance_" + hash[7..];
        instance["instanceFingerprint"] = hash;
        var root = new JsonObject
        {
            ["schemaVersion"] = 1, ["nextInstanceOrdinal"] = 2, ["nextClosureOrdinal"] = 1,
            ["nextSourceOrdinal"] = 3, ["nextDecisionOrdinal"] = 3,
            ["instances"] = new JsonArray(instance), ["closures"] = new JsonArray(),
            ["sources"] = new JsonArray(), ["decisions"] = new JsonArray()
        };
        var witnesses = new[] { parent, source };
        for (var index = 0; index < witnesses.Length; index++)
        {
            var witness = witnesses[index];
            root["sources"]!.AsArray().Add(new JsonObject
            {
                ["ordinal"] = index + 1, ["instanceId"] = instance["instanceId"]!.DeepClone(),
                ["instanceSourceOrdinal"] = index + 1, ["witness"] = witness
            });
            root["decisions"]!.AsArray().Add(new JsonObject
            {
                ["ordinal"] = index + 1, ["decisionId"] = "decision-" + index,
                ["instanceId"] = instance["instanceId"]!.DeepClone(),
                ["sourceId"] = witness["sourceId"]!.DeepClone(),
                ["opportunityRef"] = index == 0 ? wound["origin"]!["opportunityId"]!.DeepClone() : JsonValue.Create("opportunity-child"),
                ["sourceWitness"] = witness.DeepClone(),
                ["binding"] = new JsonObject
                {
                    ["sessionId"] = "session-a", ["requestId"] = "request-a", ["snapshotToken"] = "snapshot-a",
                    ["turn"] = 42, ["continuationGeneration"] = index + 1,
                    ["waveOrdinal"] = index, ["sourceOrdinal"] = index
                },
                ["decision"] = index == 0 ? "materialize" : "none",
                ["selectedSeverityRank"] = index == 0 ? JsonValue.Create(3) : null,
                ["woundId"] = index == 0 ? wound["woundId"]!.DeepClone() : null,
                ["transitionId"] = index == 0 ? wound["lastTransition"]!["transitionId"]!.DeepClone() : null,
                ["sourceFingerprint"] = witness["sourceFingerprint"]!.DeepClone(), ["decisionFingerprint"] = ""
            });
            SealSourceAndDecision(root, index);
        }
        return root;
    }

    private static void SealSourceAndDecision(JsonObject root, int index)
    {
        var witness = root["sources"]![index]!["witness"]!.AsObject();
        var hash = SpiritualWoundStateJson.Hash(witness, "source", "sourceId", "sourceFingerprint");
        witness["sourceId"] = "spiritual_source_" + hash[7..];
        witness["sourceFingerprint"] = hash;
        var decision = root["decisions"]![index]!.AsObject();
        decision["sourceId"] = witness["sourceId"]!.DeepClone();
        decision["sourceFingerprint"] = hash;
        decision["sourceWitness"] = witness.DeepClone();
        decision["decisionFingerprint"] = SpiritualWoundStateJson.Hash(decision, "decision", "decisionFingerprint");
    }
}
