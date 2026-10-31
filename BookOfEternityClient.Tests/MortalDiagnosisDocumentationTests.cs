using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using BookOfEternityClient.Services;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed class MortalDiagnosisDocumentationTests
{
    internal const string Marker = "mortal_diagnosis_execution_v1";
    [Fact]
    public void CurrentGuidanceManifestAndBothDaemonAssignmentsKeepExplicitMortalPurposeTimeAndRetry()
    {
        foreach (var file in new[] { "OtherGuides/Wound_Materialization_Contract.md", "OtherGuides/Afterlife_Contract_Matrix.md",
            "CLI_API_Specification.md", "Rules/Block_CLI_Operations.txt", "Rules/Block_32_Guardians.txt", "TaskGuides/CLI_Step_Main.txt" })
        {
            var text = File.ReadAllText(Path.Combine(TestRepoPaths.RepoRoot, file));
            var start = text.IndexOf(Marker + " — explicit", StringComparison.Ordinal); Assert.True(start >= 0, file);
            foreach (var token in new[] { "mortal_procedure_diagnosis", "mortal_diagnosis_input", "mortalDiagnosis", "pending_wound_resolutions",
                "T0", "T1", "same attempt", "no extra same-turn report", "original absence", "signed input", "empty spiritual check", "Игровое время" })
                Assert.Contains(token, text[start..], StringComparison.Ordinal);
        }
        Assert.Contains(Marker, XDocument.Parse(File.ReadAllText(Path.Combine(TestRepoPaths.RepoRoot, "Rules/Block_32_Guardians.txt"))).Root!.Value);
        var daemon = File.ReadAllText(Path.Combine(TestRepoPaths.RepoRoot, "BookOfEternityClient/game_master_daemon.ps1"));
        var assignments = Regex.Matches(daemon, "\\$script:WoundMaterializationDirective = \\\"[^\\\"]*\\\"");
        Assert.Equal(2, assignments.Count); Assert.All(assignments.Cast<Match>(), value => Assert.Contains(Marker, value.Value));
        var entry = Assert.Single(ExampleValidationManifest.Load().EffectMaterializationCoverage, value => value.ContractId == Marker);
        var example = File.ReadAllText(Path.Combine(TestRepoPaths.RepoRoot, "Examples", entry.File));
        Assert.All(entry.RequiredText, token => Assert.Contains(token, example));
    }

    [Fact]
    public void WorkedExplicitMethodPassesWholeCurrentMortalContractAndEmptyCheckRefuses()
    {
        var example = File.ReadAllText(Path.Combine(TestRepoPaths.RepoRoot, "Examples/E_CLI_Wound_Materialization.txt"));
        var start = example.IndexOf(Marker + " — worked", StringComparison.Ordinal);
        var end = example.IndexOf("end_" + Marker, start, StringComparison.Ordinal); Assert.True(start >= 0 && end > start);
        var block = Assert.Single(Regex.Matches(example[start..end], "```json\\s*([\\s\\S]*?)```", RegexOptions.CultureInvariant).Cast<Match>());
        var path = JsonNode.Parse(block.Groups[1].Value)!.AsObject(); var frozen = path.ToJsonString();
        var parsed = MortalWoundTreatmentContract.ParseDiagnosisPathShape(JsonSerializer.SerializeToElement(path), "worked.method");
        Assert.True(parsed.IsValid, JsonSerializer.Serialize(parsed.Issues));
        var typed = Assert.IsType<MortalWoundDiagnosisPathDefinition>(parsed.DiagnosisPath);
        Assert.Equal(30, typed.Check.DurationMinutes); Assert.Equal(15, typed.Check.Resolution.Difficulty);
        Assert.IsType<MortalWoundFixedZeroModifierSource>(typed.Check.Resolution.ModifierSource);
        Assert.Equal(new[] { "success", "failed_attempt" }, typed.Check.Bands.Select(value => value.Category));
        Assert.Empty(typed.Check.ResourcePolicy.Mutations);
        var wound = WoundContractTestData.CreateActiveWound();
        var hidden = wound["treatment"]!["routes"]![0]!.DeepClone().AsObject();
        hidden["routeId"] = "diagnostic_hidden_route"; hidden["visibility"] = "hidden";
        wound["treatment"]!["routes"]!.AsArray().Add(hidden);
        wound["treatment"]!["diagnosisPaths"] = new JsonArray(path.DeepClone());
        var baseline = WoundMaterializationContract.Parse(wound.ToJsonString(), "worked.wound");
        Assert.True(baseline.IsValid, JsonSerializer.Serialize(baseline.Issues));
        var canonical = WoundMaterializationContract.SerializeCanonical(baseline.Wound!);
        Assert.True(WoundMaterializationContract.Parse(canonical, "worked.roundtrip").IsValid);
        wound["treatment"]!["diagnosisPaths"]![0]!["check"] = new JsonObject();
        var refused = WoundMaterializationContract.Parse(wound.ToJsonString(), "worked.currentEmpty");
        Assert.False(refused.IsValid); Assert.Contains(refused.Issues, issue => issue.FilePath.Contains(".check", StringComparison.Ordinal));
        Assert.Equal(frozen, path.ToJsonString());
    }

    // Explicit fixture/example parameters, never a client default or pricing rule.
    internal static JsonObject CreateExplicitFreeSampleCheck(int duration) => new()
    {
        ["schemaVersion"] = 1, ["kind"] = "mortal_procedure_diagnosis",
        ["resolution"] = new JsonObject { ["formulaKey"] = "mortal_wound_procedure_v1", ["difficulty"] = 15,
            ["rollSource"] = "accepted_d20", ["criticalPolicy"] = "natural_20_first_natural_1_last",
            ["modifierSource"] = new JsonObject { ["kind"] = "fixed_zero" } },
        ["outcomes"] = new JsonArray(new JsonObject { ["bandId"] = "diagnostic_success", ["minimumMargin"] = 0, ["maximumMargin"] = null, ["category"] = "success" },
            new JsonObject { ["bandId"] = "diagnostic_failure", ["minimumMargin"] = null, ["maximumMargin"] = -1, ["category"] = "failed_attempt" }),
        ["durationMinutes"] = duration,
        ["resourcePolicy"] = new JsonObject { ["reserveBeforeResolution"] = true, ["consumeOn"] = new JsonArray("success", "failed_attempt"),
            ["refundOn"] = new JsonArray("cancelled", "validation_failed", "rolled_back"), ["mutations"] = new JsonArray() }
    };
}
