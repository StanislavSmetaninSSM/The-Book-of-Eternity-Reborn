using System.Text.Json.Nodes;
using BookOfEternityClient.Services;
using Xunit;

namespace BookOfEternityClient.Tests;

/// <summary>Authored example DATA and prompt delivery guards, not live publication qualification.</summary>
public sealed class MortalLivingWorldDocumentationTests
{
    private const string ExamplePath = "Examples/E_CLI_Mortal_Living_World.txt";
    private const string TemplateName = "MORTAL_LIVING_WORLD_TEMPLATE.md";

    [Fact]
    public void AuthoredAutonomousUpdateHasCompleteAssessmentAndActualCanonicalEffect()
    {
        var example = ReadExample("mortal_living_world_autonomous_update_v1");
        var report = example["report"]!.AsObject();
        var original = Documents(example, "original");
        var accepted = Documents(example, "accepted");
        var frozen = example.ToJsonString();
        Assert.Equal("changes", report["resolution"]!.GetValue<string>());
        Assert.Empty(MortalLivingWorldAssessment.Validate(Context(), report));
        Assert.Empty(MortalLivingWorldEvents.Validate(report, original, accepted));
        var effect = Assert.Single(report["events"]!.AsArray())!["effects"]![0]!;
        Assert.Equal("update", effect["operation"]!.GetValue<string>());
        Assert.Equal("worldFlag", effect["target"]!["kind"]!.GetValue<string>());
        Assert.False(effect["before"]!["value"]!.GetValue<bool>());
        Assert.True(effect["after"]!["value"]!.GetValue<bool>());
        Assert.Contains(MortalLivingWorldEvents.Validate(report, original, original),
            issue => issue.Code == "mortal_living_world_effect_mismatch");
        Assert.Equal(frozen, example.ToJsonString());
        var stale = report.DeepClone().AsObject(); stale["requestId"] = "foreign-request";
        Assert.Contains(MortalLivingWorldAssessment.Validate(Context(), stale),
            issue => issue.Code == "mortal_living_world_source_invalid");
    }

    [Fact]
    public void AuthoredNoChangeAllowsPlayerDrivenDeltaButNotMissingAuditOrInventedEvent()
    {
        var example = ReadExample("mortal_living_world_no_change_v1");
        var report = example["report"]!.AsObject();
        var original = Documents(example, "original");
        var accepted = Documents(example, "accepted");
        Assert.Equal("noSignificantChange", report["resolution"]!.GetValue<string>());
        Assert.Empty(report["events"]!.AsArray());
        Assert.False(JsonNode.DeepEquals(example["original"], example["accepted"]));
        Assert.Empty(MortalLivingWorldAssessment.Validate(Context(), report));
        Assert.Empty(MortalLivingWorldEvents.Validate(report, original, accepted));
        var missingAudit = report.DeepClone().AsObject(); missingAudit["audit"]!.AsArray().RemoveAt(0);
        Assert.Contains(MortalLivingWorldAssessment.Validate(Context(), missingAudit),
            issue => issue.Code == "mortal_living_world_audit_invalid");
        var invented = report.DeepClone().AsObject(); invented["events"]!.AsArray().Add(new JsonObject());
        Assert.Contains(MortalLivingWorldAssessment.Validate(Context(), invented),
            issue => issue.Code == "mortal_living_world_events_invalid");
    }

    [Fact]
    public void GeneratedTemplateIsDeliveredToOrdinaryAndRepairConsumersWithPrivateOwnership()
    {
        var daemon = Read("BookOfEternityClient/game_master_daemon.ps1");
        var marker = "-RelativePath \"Templates\\" + TemplateName + "\"";
        var template = Between(daemon, marker, "\n'@");
        Assert.Contains("-Content @'", template, StringComparison.Ordinal);
        foreach (var token in new[] { "game_state/control/mortal_living_world_report.json", "noSignificantChange",
                     "game_state/world/mortal_living_world_history.json", "client-owned", "FilesModified", "newly due",
                     "updates-only", "not full feature acceptance", "For ordinary assessed Mortal turns",
                     "Diagnosis supports noSignificantChange only", "do not relabel real",
                     "do not recreate a consumed report", "Existing cycle backlog alone does not forbid",
                     "Local QTE cannot consume a living-world report" })
            Assert.Contains(token, template, StringComparison.Ordinal);
        Assert.Contains("$script:CompactMortalLivingWorldTemplatePath = Join-Path", daemon, StringComparison.Ordinal);
        Assert.Contains("$script:GmCompactTemplateDirective += $script:MortalLivingWorldDirective", daemon, StringComparison.Ordinal);
        var directive = Between(daemon, "$script:MortalLivingWorldDirective =", "\n");
        Assert.Contains("$($script:CompactMortalLivingWorldTemplatePath)", directive, StringComparison.Ordinal);
        Assert.Contains("Mortal", directive, StringComparison.Ordinal);
        foreach (var prefix in new[] { "$message = \"Process turn #", "$message = \"REPAIR MODE for rejected turn #" })
            Assert.Contains("$($script:GmCompactTemplateDirective)", Between(daemon, prefix, "\n"), StringComparison.Ordinal);
        var helperRule = daemon.Split('\n').Single(line => line.StartsWith("$script:GmTurnHelperDirective +=", StringComparison.Ordinal)
            && line.Contains("mortal_living_world_history.json", StringComparison.Ordinal));
        Assert.Contains("client-owned", helperRule, StringComparison.Ordinal);
        Assert.Contains("do not write", helperRule, StringComparison.Ordinal);
        Assert.Contains("FilesModified", helperRule, StringComparison.Ordinal);
    }

    [Fact]
    public void CurrentGuidesAndManifestIdentifyBothBoundedWorkedRoutes()
    {
        foreach (var path in new[] { "TaskGuides/CLI_Step_Main.txt", "OtherGuides/CLI_World_Logic_Guide.txt", "Examples/E_CLI_Step_Main.txt" })
        {
            var text = Read(path);
            Assert.Contains("mortal_living_world_report.json", text, StringComparison.Ordinal);
            Assert.Contains("noSignificantChange", text, StringComparison.Ordinal);
            Assert.Contains("mortal_living_world_history.json", text, StringComparison.Ordinal);
            Assert.Contains("E_CLI_Mortal_Living_World.txt", text, StringComparison.Ordinal);
        }
        var coverage = JsonNode.Parse(Read("Examples/example_validation_manifest.json"))!["effectMaterializationCoverage"]!.AsArray();
        foreach (var id in new[] { "mortal_living_world_autonomous_update_v1", "mortal_living_world_no_change_v1" })
        {
            var entry = Assert.Single(coverage, item => item?["contractId"]?.GetValue<string>() == id)!;
            Assert.Equal("E_CLI_Mortal_Living_World.txt", entry["file"]!.GetValue<string>());
            Assert.Equal("focused-fragment", entry["validationKind"]!.GetValue<string>());
            Assert.Contains("not full feature acceptance", entry["coverageLimit"]!.GetValue<string>(), StringComparison.Ordinal);
            Assert.Equal("Mortal World", Assert.Single(entry["realms"]!.AsArray())!.GetValue<string>());
        }
    }

    // Each test owns a fresh small document; reuse the parsed DATA within that test only.
    private static JsonObject ReadExample(string id)
    {
        var text = Read(ExamplePath);
        return JsonNode.Parse(Between(text, "<!-- " + id + " -->\n```json\n", "\n```"))!.AsObject();
    }
    private static Dictionary<string, JsonObject> Documents(JsonObject example, string section) =>
        example[section]!.AsObject().ToDictionary(pair => pair.Key, pair => pair.Value!.AsObject(), StringComparer.Ordinal);
    private static MortalLivingWorldAssessmentContext Context() => new("example-session", "example-request", 12, 0, 240, 0, 0, 240, 1440);
    private static string Read(string path) => File.ReadAllText(Path.Combine(TestRepoPaths.RepoRoot, path));
    private static string Between(string text, string start, string end)
    {
        var begin = text.IndexOf(start, StringComparison.Ordinal);
        Assert.True(begin >= 0, "Missing authored section: " + start);
        begin += start.Length;
        var finish = text.IndexOf(end, begin, StringComparison.Ordinal);
        Assert.True(finish > begin, "Missing authored terminator: " + end);
        return text[begin..finish];
    }
}
