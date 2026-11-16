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
        current["game_state/world/world_events.json"] = JsonNode.Parse("""{"worldEventsLog":[{"eventId":"news","title":"Result","description":"Public result","visibility":"public"}]}""")!.AsObject();
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

    [Theory]
    [InlineData("currentActivity", "activityName", "Work", "Renamed", false)]
    [InlineData("completedActivities", "narrativeSummary", "Old", "Reworded", false)]
    [InlineData("currentActivity", "timeSpentMinutes", "1", "2", true)]
    [InlineData("completedActivities", "finalOutcome", "Failure", "Success", true)]
    public void ActivityEvidenceUsesSemanticFields(string field, string nested, string beforeText, string afterText, bool meaningful)
    {
        JsonNode Value(string text)
        {
            var obj = new JsonObject { [nested] = nested == "timeSpentMinutes" ? JsonValue.Create(int.Parse(text)) : JsonValue.Create(text) };
            return field == "completedActivities" ? new JsonArray(obj) : obj;
        }
        var before = Value(beforeText); var after = Value(afterText);
        var report = TargetReport("npcActivity", "npc_worker", field, before, after);
        Dictionary<string, JsonObject> Docs(JsonNode value) => new()
        { ["game_state/npcs/npc_core.json"] = new JsonObject { ["UpdateNPCs"] = new JsonArray(new JsonObject { ["NPCId"] = "npc_worker", [field] = value.DeepClone() }) } };
        var issues = MortalLivingWorldEvents.Validate(report, Docs(before), Docs(after));
        if (meaningful) Assert.Empty(issues);
        else Assert.Contains(issues, issue => issue.Code == "mortal_living_world_effect_not_meaningful");
    }

    [Fact]
    public void ActualLocationStorageAccessChangeIsEvidence()
    {
        var before = JsonNode.Parse("""[{"storageId":"store","hasFullAccess":false}]""")!;
        var after = JsonNode.Parse("""[{"storageId":"store","hasFullAccess":true}]""")!;
        var report = TargetReport("location", "town", "locationStorages", before, after);
        Assert.Empty(MortalLivingWorldEvents.Validate(report, Map("locations", "locationId", "town", "locationStorages", before),
            Map("locations", "locationId", "town", "locationStorages", after)));
    }

    [Fact]
    public void ActualLocationLinkAccessChangeIsEvidence()
    {
        var before = JsonNode.Parse("""{"state":"sealed","reason":"Closed","requirements":[]}""")!;
        var after = JsonNode.Parse("""{"state":"open","reason":null,"requirements":[]}""")!;
        var report = TargetReport("locationLink", "road", "access", before, after);
        Assert.Empty(MortalLivingWorldEvents.Validate(report, Map("links", "linkId", "road", "access", before),
            Map("links", "linkId", "road", "access", after)));
    }

    [Theory]
    [InlineData("visibility", "Secret")]
    [InlineData("cause", "Private cause")]
    [InlineData("provenance", "Private source")]
    public void KnownNewsRejectsNonPublicOrPrivateFields(string field, string value)
    {
        var (report, current, news) = KnownNews(); news[field] = value;
        Assert.NotEmpty(MortalLivingWorldEvents.Validate(report, Documents(false), current));
    }

    [Theory]
    [InlineData("hidden", false)]
    [InlineData("rumored", false)]
    [InlineData("discovered", true)]
    [InlineData("visited", true)]
    public void KnownNewsLocationMustBePlayerDiscovered(string tier, bool allowed)
    {
        var (report, current, news) = KnownNews(); news["locationId"] = "town";
        current["game_state/world/world_map.json"] = new JsonObject { ["locations"] = new JsonArray(new JsonObject
        { ["locationId"] = "town", ["discovery"] = new JsonObject { ["tier"] = tier, ["audience"] = allowed ? "player_known" : "gm_only" } }) };
        var issues = MortalLivingWorldEvents.Validate(report, Documents(false), current);
        if (allowed) Assert.Empty(issues); else Assert.NotEmpty(issues);
    }

    [Fact]
    public void KnownNewsMissingOrDuplicateLocationFailsClosed()
    {
        var (report, current, news) = KnownNews(); news["locationId"] = "town";
        Assert.NotEmpty(MortalLivingWorldEvents.Validate(report, Documents(false), current));
        var location = new JsonObject { ["locationId"] = "town", ["discovery"] = new JsonObject { ["tier"] = "discovered", ["audience"] = "player_known" } };
        current["game_state/world/world_map.json"] = new JsonObject { ["locations"] = new JsonArray(location, location.DeepClone()) };
        Assert.NotEmpty(MortalLivingWorldEvents.Validate(report, Documents(false), current));
    }

    [Fact]
    public void ValidationDoesNotMutateDataAndRechecksChangedInput()
    {
        var report = Report(); var original = Documents(false); var current = Documents(true);
        var reportBytes = report.ToJsonString(); var originalBytes = original.Values.Single().ToJsonString(); var currentBytes = current.Values.Single().ToJsonString();
        Assert.Empty(MortalLivingWorldEvents.Validate(report, original, current));
        Assert.Equal(reportBytes, report.ToJsonString()); Assert.Equal(originalBytes, original.Values.Single().ToJsonString()); Assert.Equal(currentBytes, current.Values.Single().ToJsonString());
        current.Values.Single()["worldStateFlags"]![0]!["value"] = false;
        Assert.NotEmpty(MortalLivingWorldEvents.Validate(report, original, current));
    }

    [Fact]
    public void DuplicateOrMalformedTypedReferencesFailClosed()
    {
        var report = Report(); Event(report)["targets"]!.AsArray().Add(Event(report)["targets"]![0]!.DeepClone());
        Assert.NotEmpty(MortalLivingWorldEvents.Validate(report, Documents(false), Documents(true)));
        report = Report(); Event(report)["targets"]![0]!["ownerId"] = 123;
        Assert.NotEmpty(MortalLivingWorldEvents.Validate(report, Documents(false), Documents(true)));
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    [InlineData(true, true)]
    public void ThreatActivityEvidenceRejectsRenameButAcceptsProgress(bool aggregate, bool progress)
    {
        JsonObject Activity(string label, int spent) => new()
        { ["activityName"] = label, ["description"] = "Existing work", ["totalTimeCostMinutes"] = 60,
          ["timeSpentMinutes"] = spent, ["currentStepNumber"] = 1, ["totalStepsInActivity"] = 3 };
        JsonObject Threat(JsonObject activity) => new()
        { ["threatId"] = "raiders", ["name"] = "Raiders", ["intensity"] = 2,
          ["longTermGoal"] = "Take the bridge", ["currentActivity"] = activity.DeepClone() };
        var beforeActivity = Activity("Watch bridge", 10);
        var afterActivity = Activity(progress ? "Watch bridge" : "Renamed watch", progress ? 20 : 10);
        var beforeThreats = new JsonArray(Threat(beforeActivity));
        var afterThreats = new JsonArray(Threat(afterActivity));
        var report = TargetReport(aggregate ? "location" : "threat", aggregate ? "town" : "raiders",
            aggregate ? "activeThreats" : "currentActivity", aggregate ? beforeThreats : beforeActivity, aggregate ? afterThreats : afterActivity);
        if (!aggregate)
            foreach (var reference in new[] { Event(report)["targets"]![0], Event(report)["cause"]!["references"]![0], Effect(report)["target"] })
                reference!["ownerId"] = "town";
        var issues = MortalLivingWorldEvents.Validate(report, Map("locations", "locationId", "town", "activeThreats", beforeThreats),
            Map("locations", "locationId", "town", "activeThreats", afterThreats));
        if (progress) Assert.Empty(issues);
        else Assert.Contains(issues, issue => issue.Code == "mortal_living_world_effect_not_meaningful");
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    [InlineData(true, true)]
    public void FactionLabelsAreNotAuthorityEvidence(bool location, bool authorityChange)
    {
        var field = location ? "factionControl" : "factionAffiliations";
        JsonArray Value(string name, string factionId) => new(location
            ? new JsonObject { ["factionId"] = factionId, ["factionName"] = name, ["controlType"] = "Military", ["controlLevel"] = 50 }
            : new JsonObject { ["factionId"] = factionId, ["factionName"] = name, ["rank"] = "Member", ["branch"] = null, ["membershipStatus"] = "Active" });
        var before = Value("Guild", "guild");
        var after = Value(authorityChange ? "Guild" : "New label", authorityChange ? "council" : "guild");
        var report = TargetReport(location ? "location" : "npc", "subject", field, before, after);
        Dictionary<string, JsonObject> Docs(JsonNode value) => location
            ? Map("locations", "locationId", "subject", field, value)
            : new() { ["game_state/npcs/npc_core.json"] = new JsonObject { ["UpdateNPCs"] = new JsonArray(new JsonObject { ["NPCId"] = "subject", [field] = value.DeepClone() }) } };
        var issues = MortalLivingWorldEvents.Validate(report, Docs(before), Docs(after));
        if (authorityChange) Assert.Empty(issues);
        else Assert.Contains(issues, issue => issue.Code == "mortal_living_world_effect_not_meaningful");
    }

    private static (JsonObject Report, Dictionary<string, JsonObject> Current, JsonObject News) KnownNews()
    {
        var report = Report(); Event(report)["knowledge"]!["player"] = "known";
        Event(report)["knowledge"]!["newsReferences"]!.AsArray().Add("news");
        var news = new JsonObject { ["eventId"] = "news", ["title"] = "Result", ["description"] = "Public result", ["visibility"] = "public" };
        var current = Documents(true); current["game_state/world/world_events.json"] = new JsonObject { ["worldEventsLog"] = new JsonArray(news) };
        return (report, current, news);
    }
    private static Dictionary<string, JsonObject> Map(string collection, string idKey, string id, string field, JsonNode value) => new()
    { ["game_state/world/world_map.json"] = new JsonObject { [collection] = new JsonArray(new JsonObject { [idKey] = id, [field] = value.DeepClone() }) } };
    private static JsonObject TargetReport(string kind, string id, string field, JsonNode before, JsonNode after)
    {
        var report = Report();
        foreach (var reference in new[] { Event(report)["targets"]![0], Event(report)["cause"]!["references"]![0], Effect(report)["target"] })
        { reference!["kind"] = kind; reference["id"] = id; }
        Effect(report)["field"] = field; Effect(report)["before"]!["value"] = before.DeepClone(); Effect(report)["after"]!["value"] = after.DeepClone();
        return report;
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
