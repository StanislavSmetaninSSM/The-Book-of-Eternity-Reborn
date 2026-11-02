using System.Text;
using System.Text.Json.Nodes;
using BookOfEternityClient.Services;
using Xunit;

namespace BookOfEternityClient.Tests;

/// <summary>Checks full immutable creation History and historical comparison DATA without issuing any live authority.</summary>
public sealed class SpiritualStrainHistoryDataTests
{
    [Fact]
    public void SignedSoulDisplayRealmJoinsOnlyItsOwnHistoricalInstance()
    {
        var root = SpiritualStrainReceiptJoinTests.CreateLedger();
        var ledger = Ledger(root);
        Assert.True(ledger.HasExactStrainHistory(History(CreateHistory(root))));
        var actual = JsonNode.Parse(Convert.FromBase64String(
            root["sources"]![1]!["witness"]!["exchangeJsonBase64"]!.GetValue<string>()))!.AsObject();
        foreach (var displayRealm in new[] { "Chaos Sea", "Море Хаоса" })
        {
            var soul = new JsonObject { ["currentRealm"] = displayRealm };
            var key = ValidationService.ReadSpiritualStrainHistoricalRealm(soul.ToJsonString());
            Assert.Equal("chaos_sea", key);
            Assert.NotNull(ledger.ReadRetainedStrainOrdinaryAfter(key!, "conflict-a", actual, false));
        }
        var foreignKey = ValidationService.ReadSpiritualStrainHistoricalRealm(
            new JsonObject { ["currentRealm"] = "Shining Abode" }.ToJsonString());
        Assert.Equal("shining_abode", foreignKey);
        Assert.Null(ledger.ReadRetainedStrainOrdinaryAfter(foreignKey!, "conflict-a", actual, false));
        foreach (var soul in new[] { null, "{", "{}", "{\"currentRealm\":7}",
                     "{\"currentRealm\":\"Mortal World\"}", "{\"currentRealm\":\"unknown\"}" })
            Assert.Null(ValidationService.ReadSpiritualStrainHistoricalRealm(soul));
    }

    [Fact]
    public void ExactFirstCreationMatchesFullParentEnvelope()
    {
        var root = SpiritualStrainReceiptJoinTests.CreateLedger();
        Assert.True(Ledger(root).HasExactStrainHistory(History(CreateHistory(root))));
    }

    [Theory]
    [InlineData("missing-create")]
    [InlineData("transition")]
    [InlineData("event")]
    [InlineData("after-fingerprint")]
    [InlineData("definition")]
    public void ChangedCreationOrRetainedDefinitionCannotBorrowTheSameWoundIdentity(string change)
    {
        var root = SpiritualStrainReceiptJoinTests.CreateLedger();
        var history = CreateHistory(root);
        Assert.True(Ledger(root).HasExactStrainHistory(History(history)));
        switch (change)
        {
            case "missing-create": history = WoundContractTestData.CreateHistory(); break;
            case "transition": history["transitions"]![0]!["transitionId"] = "foreign-transition"; break;
            case "event": history["transitions"]![0]!["eventRef"] = "foreign-event"; break;
            case "after-fingerprint": history["transitions"]![0]!["afterFingerprint"] = "sha256:" + new string('b', 64); break;
            case "definition":
                var proof = root["sources"]![1]!["witness"]!["strainPreimage"]!;
                var parent = Parent(root);
                parent["consequences"]!["ownedEffectSources"]!["definitions"]![0]!["display"]!["description"] =
                    "Иное описание сохранённого определения при тех же идентификаторах.";
                proof["parentWoundJsonBase64"] = Convert.ToBase64String(Encoding.UTF8.GetBytes(parent.ToJsonString()));
                SpiritualStrainReceiptJoinTests.SealSourceAndDecision(root, 1);
                break;
            default: throw new ArgumentOutOfRangeException(nameof(change));
        }
        Assert.False(Ledger(root).HasExactStrainHistory(History(history)));
    }

    [Fact]
    public void LaterTerminalHistoryDoesNotReplaceTheFirstCreateFingerprint()
    {
        var root = SpiritualStrainReceiptJoinTests.CreateLedger();
        var history = CreateHistory(root);
        var create = history["transitions"]![0]!.AsObject();
        var heal = create.DeepClone().AsObject();
        heal["transitionId"] = "later-heal";
        heal["operationKey"] = "later-heal-operation";
        heal["ordinal"] = 2;
        heal["woundTransitionOrdinal"] = 2;
        heal["kind"] = "heal";
        heal["turn"] = 43;
        heal["eventRef"] = "later-heal-event";
        heal["beforeFingerprint"] = create["afterFingerprint"]!.DeepClone();
        heal["afterFingerprint"] = "sha256:" + new string('c', 64);
        heal["terminal"] = true;
        history["transitions"]!.AsArray().Add(heal);
        history["nextOrdinal"] = 3;
        Assert.True(Ledger(root).HasExactStrainHistory(History(history)));
        create["afterFingerprint"] = "sha256:" + new string('d', 64);
        heal["beforeFingerprint"] = create["afterFingerprint"]!.DeepClone();
        Assert.False(Ledger(root).HasExactStrainHistory(History(history)));
    }

    [Fact]
    public void SummaryDriftRequiresExplicitHistoricalPolicyAndCannotHideMechanicsOrTurnChanges()
    {
        var root = SpiritualStrainReceiptJoinTests.CreateLedger();
        var ledger = Ledger(root);
        Assert.True(ledger.HasExactStrainHistory(History(CreateHistory(root))));
        var actual = JsonNode.Parse(Convert.FromBase64String(
            root["sources"]![1]!["witness"]!["exchangeJsonBase64"]!.GetValue<string>()))!.AsObject();
        Assert.Equal("overwhelmed", ledger.ReadRetainedStrainOrdinaryAfter("chaos_sea", "conflict-a", actual, false)!["playerSideStrain"]!.GetValue<string>());
        var summary = actual.DeepClone().AsObject();
        summary["summary"] = "Позднее читаемое описание того же события.";
        Assert.Null(ledger.ReadRetainedStrainOrdinaryAfter("chaos_sea", "conflict-a", summary, false));
        Assert.NotNull(ledger.ReadRetainedStrainOrdinaryAfter("chaos_sea", "conflict-a", summary, true));
        summary["turnNumber"] = 99;
        Assert.Null(ledger.ReadRetainedStrainOrdinaryAfter("chaos_sea", "conflict-a", summary, true));
        var mechanics = actual.DeepClone().AsObject();
        mechanics["after"]!["oppositionSideStrain"] = "broken";
        Assert.Null(ledger.ReadRetainedStrainOrdinaryAfter("chaos_sea", "conflict-a", mechanics, true));
        Assert.Null(ledger.ReadRetainedStrainOrdinaryAfter("shining_abode", "conflict-a", actual, true));
        Assert.Null(ledger.ReadRetainedStrainOrdinaryAfter("chaos_sea", "foreign-conflict", actual, true));
    }

    private static SpiritualWoundOpportunityReceiptState Ledger(JsonObject root)
    {
        var parsed = SpiritualWoundOpportunityReceiptState.Parse(root.ToJsonString(), "unit.strainLedger");
        Assert.True(parsed.IsValid, string.Join("\n", parsed.Issues));
        return parsed.State!;
    }

    private static WoundHistoryState History(JsonObject root)
    {
        var parsed = WoundHistoryState.Parse(root.ToJsonString(), WoundHistoryState.HistoryPath);
        Assert.True(parsed.IsValid, string.Join("\n", parsed.Issues));
        return parsed.State!;
    }

    private static JsonObject Parent(JsonObject root) => JsonNode.Parse(Convert.FromBase64String(
        root["sources"]![1]!["witness"]!["strainPreimage"]!["parentWoundJsonBase64"]!.GetValue<string>()))!.AsObject();

    private static JsonObject CreateHistory(JsonObject root)
    {
        var parsed = WoundMaterializationContract.Parse(Parent(root).ToJsonString(), "unit.parent");
        Assert.True(parsed.IsValid, string.Join("\n", parsed.Issues));
        var wound = parsed.Wound!;
        var create = WoundContractTestData.CreateTransition();
        create["transitionId"] = wound.LastTransition.TransitionId;
        create["woundId"] = wound.WoundId;
        create["turn"] = wound.LastTransition.Turn;
        create["eventRef"] = wound.Origin.EventRef;
        create["attemptId"] = null;
        create["beforeFingerprint"] = WoundHistoryState.ComputeNonexistentBeforeFingerprint(wound.WoundId);
        create["afterFingerprint"] = WoundIdentityState.ComputeSemanticFingerprint(wound);
        create["sourceFingerprint"] = "sha256:" + new string('a', 64);
        return WoundContractTestData.CreateHistory(create);
    }
}
