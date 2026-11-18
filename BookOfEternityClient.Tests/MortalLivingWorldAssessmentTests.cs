using System.Text.Json.Nodes;
using BookOfEternityClient.Services;
using Xunit;

namespace BookOfEternityClient.Tests;

/// <summary>Tests admission DATA only, not authenticated source, event effects or publication.</summary>
public sealed class MortalLivingWorldAssessmentTests
{
    [Theory]
    [InlineData(0, 239, 0, 0)]
    [InlineData(1000, 1199, 960, 0)]
    public void ShortSceneWithoutDueCyclesDoesNotRequireReport(long original, long result, long world, long faction)
    {
        Assert.Empty(MortalLivingWorldAssessment.Validate(Context(original, result, world, faction), null));
    }

    [Theory]
    [InlineData(0, 240, 0, 0)]
    [InlineData(0, 1440, 0, 0)]
    [InlineData(240, 240, 0, 0)]
    [InlineData(1439, 1440, 1200, 0)]
    public void DueCycleCannotBeAcceptedWithoutAssessment(long original, long result, long world, long faction)
    {
        var issue = Assert.Single(MortalLivingWorldAssessment.Validate(Context(original, result, world, faction), null));
        Assert.Equal("mortal_living_world_report_required", issue.Code);
        Assert.Equal("game_state/control/mortal_living_world_report.json", issue.FilePath);
        Assert.Equal(IssueSeverity.Error, issue.Severity);
        Assert.False(string.IsNullOrWhiteSpace(issue.RepairHint));
    }

    [Fact]
    public void IndependentlyDueFactionCycleRequiresAssessment()
    {
        var context = Context(100, 120, 100, 0) with { WorldCycleMinutes = 1000, FactionCycleMinutes = 120 };
        Assert.Equal("mortal_living_world_report_required", Assert.Single(MortalLivingWorldAssessment.Validate(context, null)).Code);
    }

    [Theory]
    [InlineData(1000, 1200, 960, 0, 960)]
    [InlineData(1439, 1440, 1200, 0, 0)]
    [InlineData(240, 240, 0, 0, 0)]
    [InlineData(100, 120, 100, 0, 100)]
    public void AuditCoversEntireUnprocessedWindow(long original, long result, long world, long faction, long from)
    {
        var report = Report(from, result);
        Assert.Empty(MortalLivingWorldAssessment.Validate(Context(original, result, world, faction), report));
        report["fromMinute"] = from + 1;
        Assert.Equal("mortal_living_world_window_invalid", Assert.Single(MortalLivingWorldAssessment.Validate(Context(original, result, world, faction), report)).Code);
    }

    [Theory]
    [InlineData(-1, 240, 0, 0, 240, 1440)]
    [InlineData(10, 9, 0, 0, 240, 1440)]
    [InlineData(10, 20, -1, 0, 240, 1440)]
    [InlineData(10, 20, 0, -1, 240, 1440)]
    [InlineData(10, 20, 11, 0, 240, 1440)]
    [InlineData(10, 20, 0, 11, 240, 1440)]
    [InlineData(10, 20, 0, 0, 0, 1440)]
    [InlineData(10, 20, 0, 0, 240, -1)]
    public void InvalidClockContextFailsInsteadOfSkippingAssessment(long original, long result, long world, long faction, int worldCycle, int factionCycle)
    {
        var context = Context(original, result, world, faction) with { WorldCycleMinutes = worldCycle, FactionCycleMinutes = factionCycle };
        Assert.Equal("mortal_living_world_context_invalid", Assert.Single(MortalLivingWorldAssessment.Validate(context, null)).Code);
    }

    [Fact]
    public void MaximumClockValueDoesNotOverflowDueCalculation()
    {
        var context = Context(long.MaxValue - 1, long.MaxValue, long.MaxValue - 240, long.MaxValue - 10);
        Assert.Empty(MortalLivingWorldAssessment.Validate(context, Report(long.MaxValue - 240, long.MaxValue)));
    }

    [Theory]
    [InlineData("sessionId", "other")]
    [InlineData("sessionId", " session")]
    [InlineData("requestId", "request ")]
    [InlineData("requestId", "")]
    public void ReportSourceMustMatchExactly(string field, string value)
    {
        var report = Report(); report[field] = value;
        Assert.Equal("mortal_living_world_source_invalid", Assert.Single(MortalLivingWorldAssessment.Validate(Context(), report)).Code);
    }

    [Fact]
    public void StaleTurnIsRejectedEvenWhenNoCycleIsDue()
    {
        var report = Report(0, 10); report["turn"] = 6;
        Assert.Equal("mortal_living_world_source_invalid", Assert.Single(MortalLivingWorldAssessment.Validate(Context(result: 10), report)).Code);
    }

    [Fact]
    public void InvalidSourceContextCannotAuthorizeAReport()
    {
        Assert.Equal("mortal_living_world_context_invalid", Assert.Single(MortalLivingWorldAssessment.Validate(Context() with { SessionId = " " }, Report())).Code);
        Assert.Equal("mortal_living_world_context_invalid", Assert.Single(MortalLivingWorldAssessment.Validate(Context() with { Turn = -1 }, Report())).Code);
    }

    [Theory]
    [InlineData("schemaVersion")]
    [InlineData("sessionId")]
    [InlineData("requestId")]
    [InlineData("turn")]
    [InlineData("fromMinute")]
    [InlineData("toMinute")]
    [InlineData("resolution")]
    [InlineData("audit")]
    [InlineData("events")]
    public void ReportRequiresEveryDeclaredField(string field)
    {
        var report = Report(); report.Remove(field);
        Assert.Single(MortalLivingWorldAssessment.Validate(Context(), report));
    }

    [Theory]
    [InlineData("schemaVersion", "\"1\"")]
    [InlineData("schemaVersion", "2")]
    [InlineData("turn", "\"7\"")]
    [InlineData("fromMinute", "0.5")]
    [InlineData("toMinute", "\"240\"")]
    [InlineData("resolution", "\"unknown\"")]
    [InlineData("audit", "{}")]
    [InlineData("events", "null")]
    [InlineData("unexpected", "true")]
    public void ReportRejectsWrongTypesVersionsAndUnknownFields(string field, string json)
    {
        var report = Report(); report[field] = JsonNode.Parse(json);
        Assert.Single(MortalLivingWorldAssessment.Validate(Context(), report));
    }

    [Theory]
    [InlineData("missing")]
    [InlineData("duplicate")]
    [InlineData("unknownScope")]
    [InlineData("blankReason")]
    [InlineData("extraField")]
    [InlineData("scalarRow")]
    public void AuditRequiresEachExactScopeOnceWithReason(string defect)
    {
        var report = Report(); var audit = report["audit"]!.AsArray();
        switch (defect)
        {
            case "missing": audit.RemoveAt(0); break;
            case "duplicate": audit[0]!["scope"] = "factions"; break;
            case "unknownScope": audit[0]!["scope"] = "Npcs"; break;
            case "blankReason": audit[0]!["reason"] = "  "; break;
            case "extraField": audit[0]!["extra"] = true; break;
            case "scalarRow": audit[0] = "npcs"; break;
        }
        Assert.Equal("mortal_living_world_audit_invalid", Assert.Single(MortalLivingWorldAssessment.Validate(Context(), report)).Code);
    }

    [Fact]
    public void NoChangeDoesNotRequireInventedEventsOrMutateReport()
    {
        var report = Report(); var original = report.ToJsonString();
        Assert.Empty(MortalLivingWorldAssessment.Validate(Context(), report));
        Assert.Empty(report["events"]!.AsArray());
        Assert.Equal(original, report.ToJsonString());
    }

    [Fact]
    public void NoChangeCannotContainAnEvent()
    {
        var report = Report(); report["events"]!.AsArray().Add(new JsonObject());
        Assert.Equal("mortal_living_world_events_invalid", Assert.Single(MortalLivingWorldAssessment.Validate(Context(), report)).Code);
    }

    [Theory]
    [InlineData("[]")]
    [InlineData("[null]")]
    [InlineData("[\"event\"]")]
    public void ChangesNeedNonemptyObjectRows(string events)
    {
        var report = Report(); report["resolution"] = "changes"; report["events"] = JsonNode.Parse(events);
        Assert.Equal("mortal_living_world_events_invalid", Assert.Single(MortalLivingWorldAssessment.Validate(Context(), report)).Code);
    }

    [Fact]
    public void ObjectEventRowsPassAdmissionOnlyNotEffectValidation()
    {
        var report = Report(); report["resolution"] = "changes"; report["events"]!.AsArray().Add(new JsonObject { ["eventId"] = "event_1" });
        Assert.Empty(MortalLivingWorldAssessment.Validate(Context(), report));
    }

    [Fact]
    public void DuplicateRootPropertiesProduceBoundedIssue()
    {
        var report = JsonNode.Parse("{\"schemaVersion\":1,\"schemaVersion\":2}")!.AsObject();
        Assert.Equal("mortal_living_world_report_invalid", Assert.Single(MortalLivingWorldAssessment.Validate(Context(), report)).Code);
    }

    private static MortalLivingWorldAssessmentContext Context(long original = 0, long result = 240, long world = 0, long faction = 0) =>
        new("session", "request", 7, original, result, world, faction, 240, 1440);

    private static JsonObject Report(long from = 0, long to = 240) => new()
    {
        ["schemaVersion"] = 1, ["sessionId"] = "session", ["requestId"] = "request", ["turn"] = 7,
        ["fromMinute"] = from, ["toMinute"] = to, ["resolution"] = "noSignificantChange",
        ["audit"] = new JsonArray(new[] { "npcs", "factions", "locations", "projects", "world" }.Select(scope =>
            (JsonNode)new JsonObject { ["scope"] = scope, ["reason"] = "Reviewed the bounded current scope; no autonomous change is due." }).ToArray()),
        ["events"] = new JsonArray()
    };
}
