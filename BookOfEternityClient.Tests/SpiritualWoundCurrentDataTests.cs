using System.Text.Json.Nodes;
using BookOfEternityClient.Services;
using Xunit;

namespace BookOfEternityClient.Tests;

/// <summary>Checks intrinsic current snapshot consistency; these fixtures do not issue live or historical source authority.</summary>
public sealed class SpiritualWoundCurrentDataTests
{
    [Theory]
    [InlineData(3, false, "player_soul")]
    [InlineData(4, false, "player_soul")]
    [InlineData(4, true, "player_soul")]
    [InlineData(3, false, "guardian")]
    [InlineData(3, false, "resident")]
    public void CurrentSnapshotReadsItsActualRankActorAndLatestTransition(int rank, bool worsened, string actorKind)
    {
        var data = CreateData(rank, worsened, actorKind);
        var before = data.Profiles.ToJsonString();
        var wound = Read(data, actorKind + ":" + data.OwnerId);
        Assert.Equal(rank, wound.Severity.Rank);
        Assert.Equal(actorKind, wound.Owner.OwnerKind);
        Assert.Equal(data.OwnerId, wound.Owner.OwnerId);
        Assert.Equal(worsened ? "worsen" : "create", wound.LastTransition.Kind);
        Assert.Equal(worsened ? 2 : 1, wound.LastTransition.Ordinal);
        Assert.Equal(before, data.Profiles.ToJsonString());
    }

    [Theory]
    [InlineData("history")]
    [InlineData("identity-owner")]
    [InlineData("latest-fingerprint")]
    [InlineData("graph")]
    [InlineData("actor")]
    [InlineData("realm")]
    [InlineData("wound-id")]
    public void CurrentSnapshotRefusesAnotherGraphActorOrHistoryAfterValidBaseline(string change)
    {
        var data = CreateData(4, worsened: true);
        Assert.Equal(4, Read(data, "player_soul:" + data.OwnerId).Severity.Rank);
        var actor = "player_soul:" + data.OwnerId;
        var realm = "chaos_sea";
        var woundId = data.WoundId;
        switch (change)
        {
            case "history": data = data with { History = WoundContractTestData.CreateHistory() }; break;
            case "identity-owner": data.Identity["entries"]![0]!["ownerId"] = "another_soul"; break;
            case "latest-fingerprint": data.History["transitions"]![1]!["afterFingerprint"] = "sha256:" + new string('c', 64); break;
            case "graph": data.Current["consequences"]!["ownedEffectSources"]!["definitions"]![0]!["display"]!["description"] = "Другой граф при прежней истории."; break;
            case "actor": actor = "guardian:" + data.OwnerId; break;
            case "realm": realm = "shining_abode"; break;
            case "wound-id": woundId = "wound_foreign"; break;
            default: throw new ArgumentOutOfRangeException(nameof(change));
        }
        var before = data.Profiles.ToJsonString();
        Assert.Throws<InvalidOperationException>(() => Read(data, actor, realm, woundId));
        Assert.Equal(before, data.Profiles.ToJsonString());
    }

    [Fact]
    public void ReadEnvelopeCannotBeChangedThroughCallerSnapshots()
    {
        var data = CreateData(3);
        var wound = Read(data, "player_soul:" + data.OwnerId);
        var fingerprint = WoundIdentityState.ComputeSemanticFingerprint(wound);
        data.Current["display"]!["name"] = "Изменённое исходное имя";
        data.Current["consequences"]!["ownedEffectSources"]!["definitions"]![0]!["display"]!["description"] = "Изменённый исходный граф";
        Assert.Equal(fingerprint, WoundIdentityState.ComputeSemanticFingerprint(wound));
        Assert.NotEqual(data.Current["display"]!["name"]!.GetValue<string>(), wound.Display.Name);
    }

    private static WoundMaterializationEnvelope Read(SnapshotData data, string actor,
        string realm = "chaos_sea", string? woundId = null) => SpiritualWoundCurrentData.ReadActive(
        new(null, null, null, null, data.Profiles), data.Identity.ToJsonString(), data.History.ToJsonString(),
        realm, woundId ?? data.WoundId, actor);

    private sealed record SnapshotData(JsonObject Profiles, JsonObject Identity, JsonObject History,
        JsonObject Current, string WoundId, string OwnerId);

    private static SnapshotData CreateData(int rank, bool worsened = false, string actorKind = "player_soul")
    {
        var (_, _, initial, _) = SpiritualStrainPreimageDataTests.CreateData();
        var woundId = initial["woundId"]!.GetValue<string>();
        var ownerId = actorKind == "player_soul" ? "player_soul" : actorKind + "_parent";
        initial["owner"]!["ownerKind"] = actorKind;
        initial["owner"]!["ownerId"] = ownerId;
        if (rank == 4) initial["severity"]!["maximumAtCreation"] = "IV";
        if (actorKind != "player_soul")
        {
            var definitions = initial["consequences"]!["ownedEffectSources"]!["definitions"]!.AsArray();
            for (var i = 0; i < definitions.Count; i++)
            {
                var old = definitions[i]!;
                var component = old["components"]![0]!;
                definitions[i] = EffectMaterializationTestFixture.CreateSpiritualWoundDefinition(
                    component["profile"]!.GetValue<string>(), woundId: woundId, realm: "chaos_sea",
                    definitionKey: old["definitionKey"]!.GetValue<string>(), targetKind: actorKind,
                    operation: component["payload"]!["operation"]!.GetValue<string>(),
                    magnitude: component["payload"]!["magnitude"]!.DeepClone());
            }
        }
        var current = initial.DeepClone().AsObject();
        if (rank == 4)
        {
            current["severity"]!["value"] = "IV";
            current["severity"]!["rank"] = 4;
            current["consequences"]!["slotBudget"] = 4;
            current["consequences"]!["slotsUsed"] = 4;
            var owned = current["consequences"]!["ownedEffectSources"]!;
            owned["definitions"]![2]!["components"]![0]!["payload"]!["magnitude"] = 3;
            owned["definitions"]!.AsArray().Add(EffectMaterializationTestFixture.CreateSpiritualWoundDefinition(
                "spiritual_roll_hindrance", woundId: woundId, realm: "chaos_sea", definitionKey: "definition_parent_fourth",
                targetKind: actorKind == "player_soul" ? "player" : actorKind, operation: "binding"));
            owned["rootBindings"]!.AsArray().Add(WoundContractTestData.CreateRootBinding("effect_parent_fourth", "definition_parent_fourth"));
            current["consequences"]!["entries"]!.AsArray().Add(new JsonObject
            {
                ["slot"] = 4, ["profileKey"] = "spiritual_roll_hindrance", ["effectId"] = "effect_parent_fourth",
                ["readableSummary"] = "Оковывать противника труднее."
            });
        }
        if (worsened)
        {
            current["lastTransition"]!["transitionId"] = "wound_transition_parent_002";
            current["lastTransition"]!["ordinal"] = 2;
            current["lastTransition"]!["kind"] = "worsen";
            current["lastTransition"]!["turn"] = 43;
            current["lastTransition"]!["eventRef"] = "turn_43:wound_worsened";
            current["severity"]!["lastChangeEventRef"] = "turn_43:wound_worsened";
        }
        var first = Parse(worsened ? initial : current);
        var actual = Parse(current);
        var create = Transition(first, "create", 1, WoundHistoryState.ComputeNonexistentBeforeFingerprint(woundId));
        var history = worsened ? WoundContractTestData.CreateHistory(create,
            Transition(actual, "worsen", 2, WoundIdentityState.ComputeSemanticFingerprint(first))) : WoundContractTestData.CreateHistory(create);
        var identity = WoundContractTestData.CreateIdentityIndex(WoundContractTestData.CreateIdentityEntry(
            woundId, "chaos_sea", actorKind, ownerId, WoundCarrierCatalog.AfterlifeProfilesPath, "spiritual",
            createdAtTurn: actual.Origin.CreatedAtTurn, createdEventRef: actual.Origin.EventRef,
            lastTransitionOrdinal: actual.LastTransition.Ordinal, semanticFingerprint: WoundIdentityState.ComputeSemanticFingerprint(actual)));
        var profiles = new JsonObject { ["schemaVersion"] = 1, ["profiles"] = new JsonArray(new JsonObject
        {
            ["actorType"] = actorKind, ["actorId"] = ownerId, ["realm"] = "Chaos Sea", ["displayName"] = "Духовный участник",
            ["activeWounds"] = new JsonArray(current)
        }) };
        return new(profiles, identity, history, current, woundId, ownerId);
    }

    private static WoundMaterializationEnvelope Parse(JsonObject root)
    {
        var parsed = WoundMaterializationContract.Parse(root.ToJsonString(), "unit.currentWound");
        Assert.True(parsed.IsValid, string.Join("\n", parsed.Issues));
        return parsed.Wound!;
    }

    private static JsonObject Transition(WoundMaterializationEnvelope wound, string kind, int ordinal, string before)
    {
        var row = WoundContractTestData.CreateTransition(kind, ordinal, ordinal);
        row["woundId"] = wound.WoundId;
        row["transitionId"] = wound.LastTransition.TransitionId;
        row["turn"] = wound.LastTransition.Turn;
        row["eventRef"] = wound.LastTransition.EventRef;
        row["operationKey"] = "operation_parent_" + ordinal;
        row["attemptId"] = null;
        row["beforeFingerprint"] = before;
        row["afterFingerprint"] = WoundIdentityState.ComputeSemanticFingerprint(wound);
        row["sourceFingerprint"] = "sha256:" + new string('b', 64);
        return row;
    }
}
