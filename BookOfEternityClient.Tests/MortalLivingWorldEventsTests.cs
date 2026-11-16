using System.Text.Json.Nodes;
using BookOfEternityClient.Services;
using Xunit;

namespace BookOfEternityClient.Tests;

/// <summary>Detached event/effect DATA checks, not domain validity, source authentication or publication.</summary>
public sealed class MortalLivingWorldEventsTests
{
    [Fact]
    public void ActualFlagChangeProvidesEffectEvidence()
    {
        Assert.Empty(MortalLivingWorldEvents.Validate(Report(), Documents(false), Documents(true)));
    }

    [Fact]
    public void ClaimedAfterValueCannotReplaceActualCanonicalEffect()
    {
        var issues = MortalLivingWorldEvents.Validate(Report(), Documents(false), Documents(false));
        Assert.Contains(issues, issue => issue.Code == "mortal_living_world_effect_mismatch");
        Assert.All(issues, issue => Assert.False(string.IsNullOrWhiteSpace(issue.RepairHint)));
    }

    [Fact]
    public void NoChangeAssessmentAllowsUnrelatedPlayerDrivenChanges()
    {
        var report = Report(); report["resolution"] = "noSignificantChange"; report["events"] = new JsonArray();
        Assert.Empty(MortalLivingWorldEvents.Validate(report, Documents(false), Documents(true)));
    }

    [Fact]
    public void UndeclaredTargetCannotBorrowAnotherFlagsDelta()
    {
        var report = Report(); Effect(report)["target"]!["id"] = "other_flag";
        Assert.NotEmpty(MortalLivingWorldEvents.Validate(report, Documents(false), Documents(true)));
    }

    [Fact]
    public void SameCanonicalDeltaCannotProveTwoEvents()
    {
        var report = Report();
        var second = Event(report).DeepClone().AsObject(); second["eventId"] = "second_event";
        report["events"]!.AsArray().Add(second);
        Assert.Contains(MortalLivingWorldEvents.Validate(report, Documents(false), Documents(true)),
            issue => issue.Code == "mortal_living_world_effect_reused");
    }

    [Fact]
    public void HiddenEventCannotDeclarePublicNews()
    {
        var report = Report(); Event(report)["knowledge"]!["newsReferences"]!.AsArray().Add("news");
        Assert.NotEmpty(MortalLivingWorldEvents.Validate(report, Documents(false), Documents(true)));
    }

    [Fact]
    public void KnownEventRequiresExactExistingNewsIdentity()
    {
        var report = Report(); Event(report)["knowledge"]!["player"] = "known";
        Event(report)["knowledge"]!["newsReferences"]!.AsArray().Add("news");
        Assert.NotEmpty(MortalLivingWorldEvents.Validate(report, Documents(false), Documents(true)));
        var current = Documents(true);
        current["game_state/world/world_events.json"] = JsonNode.Parse("""{"worldEventsLog":[{"eventId":"news","description":"Public result"}]}""")!.AsObject();
        Assert.Empty(MortalLivingWorldEvents.Validate(report, Documents(false), current));
    }

    [Theory]
    [InlineData("fromMinute", -1)]
    [InlineData("toMinute", 241)]
    public void EventWindowMustStayInsideAssessment(string field, int value)
    {
        var report = Report(); Event(report)[field] = value;
        Assert.NotEmpty(MortalLivingWorldEvents.Validate(report, Documents(false), Documents(true)));
    }

    [Fact]
    public void CosmeticNestedEditsAndArrayReorderingDoNotProveConsequences()
    {
        var report = Report();
        foreach (var reference in new[] { Event(report)["actors"]![0], Event(report)["targets"]![0],
                     Event(report)["cause"]!["references"]![0], Effect(report)["target"] })
        { reference!["kind"] = "faction"; reference["id"] = "guild"; }
        Effect(report)["field"] = "relations";
        var before = JsonNode.Parse("""[{"factionId":"a","value":1,"description":"old"},{"factionId":"b","value":2}]""")!;
        var after = JsonNode.Parse("""[{"factionId":"b","value":2},{"factionId":"a","value":1,"description":"new"}]""")!;
        Effect(report)["before"]!["value"] = before.DeepClone(); Effect(report)["after"]!["value"] = after.DeepClone();
        Dictionary<string, JsonObject> Factions(JsonNode value) => new()
        { ["game_state/factions/faction_core.json"] = new JsonObject { ["factions"] = new JsonArray(new JsonObject { ["factionId"] = "guild", ["relations"] = value.DeepClone() }) } };
        Assert.Contains(MortalLivingWorldEvents.Validate(report, Factions(before), Factions(after)),
            issue => issue.Code == "mortal_living_world_effect_not_meaningful");
    }

    [Fact]
    public void UnknownNestedPropertiesFailClosed()
    {
        var report = Report(); Effect(report)["before"]!["authority"] = true;
        Assert.NotEmpty(MortalLivingWorldEvents.Validate(report, Documents(false), Documents(true)));
    }

    [Fact]
    public void ConsequenceMustCoverEachEffect()
    {
        var report = Report(); Event(report)["consequences"]![0]!["effectIds"] = new JsonArray("unknown");
        Assert.NotEmpty(MortalLivingWorldEvents.Validate(report, Documents(false), Documents(true)));
    }

    private static JsonObject Event(JsonObject report) => report["events"]![0]!.AsObject();
    private static JsonObject Effect(JsonObject report) => Event(report)["effects"]![0]!.AsObject();
    private static Dictionary<string, JsonObject> Documents(bool value) => new()
    {
        ["game_state/world/world_flags.json"] = new JsonObject
        { ["worldStateFlags"] = new JsonArray(new JsonObject { ["flagId"] = "bridge_open", ["value"] = value }) }
    };
    private static JsonObject Report() => JsonNode.Parse("""
        {"schemaVersion":1,"sessionId":"session","requestId":"request","turn":7,
         "fromMinute":0,"toMinute":240,"resolution":"changes","audit":[],"events":[
          {"eventId":"bridge_completed","fromMinute":0,"toMinute":240,
           "actors":[{"kind":"system","id":"worldCycle"}],
           "targets":[{"kind":"worldFlag","id":"bridge_open"}],
           "cause":{"summary":"Work finished","references":[{"kind":"worldFlag","id":"bridge_open"}]},
           "effects":[{"effectId":"open_bridge","target":{"kind":"worldFlag","id":"bridge_open"},
             "operation":"update","field":"value","before":{"present":true,"value":false},"after":{"present":true,"value":true}}],
           "consequences":[{"summary":"The crossing opens","effectIds":["open_bridge"]}],
           "knowledge":{"player":"hidden","newsReferences":[]}}]}
        """)!.AsObject();
}
