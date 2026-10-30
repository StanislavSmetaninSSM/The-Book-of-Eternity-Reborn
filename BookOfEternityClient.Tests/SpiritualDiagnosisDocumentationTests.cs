using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using BookOfEternityClient.Models;
using BookOfEternityClient.Services;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed class SpiritualDiagnosisDocumentationTests
{
    private const string Marker = "spiritual_diagnosis_execution_v1";

    [Fact]
    public void EffectiveGuidanceAndBothDaemonDirectivesKeepDistinctGuaranteedDiagnosisAndRequiredNaturalSuffix()
    {
        foreach (var file in new[] { "OtherGuides/Wound_Materialization_Contract.md", "OtherGuides/Afterlife_Contract_Matrix.md",
            "CLI_API_Specification.md", "Rules/Block_CLI_Operations.txt", "Rules/Block_32_Guardians.txt", "TaskGuides/CLI_Step_Main.txt" })
        {
            var text = File.ReadAllText(Path.Combine(TestRepoPaths.RepoRoot, file));
            var start = text.IndexOf(Marker + " — guaranteed", StringComparison.Ordinal); Assert.True(start >= 0, file);
            foreach (var token in new[] { "spiritual_diagnosis_request", "schema9/mode=diagnose", "EVERY", "check is exactly {}",
                "REQUIRED complete nonempty", "zero", "No diagnostic roll", "naturalSuffix", "current target/locality",
                "residentThoughtJournalUpdates", "spiritualResidentThoughtJournalUpdates", "spiritualShiningPatientMemory", "Mortal diagnosis" })
                Assert.Contains(token, text[start..], StringComparison.OrdinalIgnoreCase);
        }
        var root = XDocument.Parse(File.ReadAllText(Path.Combine(TestRepoPaths.RepoRoot, "Rules/Block_32_Guardians.txt"))).Root!;
        Assert.Contains(Marker, root.Value);
        var daemon = File.ReadAllText(Path.Combine(TestRepoPaths.RepoRoot, "BookOfEternityClient/game_master_daemon.ps1"));
        var assignments = Regex.Matches(daemon, "\\$script:WoundMaterializationDirective = \\\"[^\\\"]*\\\"");
        Assert.Equal(2, assignments.Count);
        Assert.All(assignments.Cast<Match>(), a => { Assert.Contains(Marker, a.Value); Assert.Contains("REQUIRED complete nonempty", a.Value); });
        var entry = Assert.Single(ExampleValidationManifest.Load().EffectMaterializationCoverage, e => e.ContractId == Marker);
        var example = File.ReadAllText(Path.Combine(TestRepoPaths.RepoRoot, "Examples", entry.File));
        Assert.All(entry.RequiredText, token => Assert.Contains(token, example));
        Assert.Contains(Marker, File.ReadAllText(Path.Combine(TestRepoPaths.RepoRoot, "Examples/E_CLI_Wound_Materialization.txt")));
    }

    [Theory]
    [InlineData("chaos_sea")][InlineData("shining_abode")]
    public void WorkedPathPassesActualSourceRequestAndKnowledgeOnlyProgramWithoutInventedAuthority(string realm)
    {
        var entry = Assert.Single(ExampleValidationManifest.Load().EffectMaterializationCoverage, e => e.ContractId == Marker);
        var example = File.ReadAllText(Path.Combine(TestRepoPaths.RepoRoot, "Examples", entry.File));
        var start = example.IndexOf(Marker + " — worked", StringComparison.Ordinal);
        var end = example.IndexOf("end_" + Marker, start, StringComparison.Ordinal); Assert.True(start >= 0 && end > start);
        var match = Assert.Single(Regex.Matches(example[start..end], "```json\\s*([\\s\\S]*?)```", RegexOptions.CultureInvariant).Cast<Match>());
        var path = JsonNode.Parse(match.Groups[1].Value)!.AsObject();
        var prototype = SpiritualDiagnosisAdmissionTests.Context(realm);
        var node = JsonNode.Parse(WoundMaterializationContract.SerializeCanonical(Assert.Single(prototype.Catalog.Occurrences).Wound))!.AsObject();
        node["treatment"]!["diagnosisPaths"] = new JsonArray(path.DeepClone());
        var input = WoundPlayerProjectionTestData.InputInRealm(realm, node);
        input.Carriers.AfterlifeProfiles!["profiles"]![0]!["standardArts"] = new JsonObject { ["spiritual_healing"] = 0 };
        var catalog = WoundCarrierCatalog.Build(input.Carriers); Assert.Empty(catalog.Issues);
        Assert.Empty(input.History.ValidateAgreement(input.Identities, catalog));
        var context = new WoundPlayerReadContext(input, catalog, prototype.Soul.DeepClone().AsObject(), null, WoundPlayerProjection.Build(input, catalog));
        var original = Assert.Single(catalog.Occurrences).Wound;
        var frozen = input.Carriers.AfterlifeProfiles!.ToJsonString() + context.Soul.ToJsonString() + path.ToJsonString();
        var admission = Assert.IsType<SpiritualDiagnosisAdmission>(new SpiritualDiagnosisSourceData(context)
            .Admit(original, path["diagnosisPathId"]!.GetValue<string>()).Data);
        Assert.Equal(0, admission.HealingTier); Assert.Equal(3, admission.RequirementBindings.Count);
        var turn = new SpiritualSelfTreatmentStageContext("session_worked_diagnosis", "request_worked_diagnosis", 43,
            new ProgressionControl { CurrentRealm = context.Soul["currentRealm"]!.GetValue<string>(), NextChaosSeaTurnOrdinal = 1 });
        var request = SpiritualDiagnosisRequestCodec.Create(turn, admission.Selection, admission);
        var program = SpiritualDiagnosisProgram.Build(turn, request, admission, [new(original, 0)]);
        var knowledgeAfter = program.KnowledgeAfter;
        // Compare complete canonical component values, not detached collection identities.
        Assert.Equal(WoundMaterializationContract.SerializeCanonical(original),
            WoundMaterializationContract.SerializeCanonical(original with
            { Recovery = knowledgeAfter.Recovery, Care = knowledgeAfter.Care }));
        Assert.Equal("known_to_player", Assert.Single(knowledgeAfter.Complications).Visibility);
        Assert.Equal("recover", Assert.Single(program.NaturalStages).Kind);
        Assert.Equal(frozen, input.Carriers.AfterlifeProfiles!.ToJsonString() + context.Soul.ToJsonString() + path.ToJsonString());
        // Detached DATA proves the worked shape; native current-owner tests prove publication.
        Assert.Null(request["attempt"]); Assert.Null(request["payment"]);
    }
}
