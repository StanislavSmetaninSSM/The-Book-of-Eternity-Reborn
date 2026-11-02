using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using BookOfEternityClient.Services;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed class PhysicalGuidedDocumentationTests
{
    [Fact]
    public void OrdinaryCourseWaitDocumentsClientPublicationAndRecoveryBoundary()
    {
        foreach (var path in new[] { "OtherGuides/Wound_Materialization_Contract.md", "Examples/E_CLI_Wound_Materialization.txt" })
        {
            var text = File.ReadAllText(Path.Combine(TestRepoPaths.RepoRoot, path));
            foreach (var term in new[] { "Ordinary course-wait clock publication", "{\"timeChange\":480}",
                         "game_state/world/world_time.json", "ready.filesModified", "numeric1260", "numeric1740",
                         "calendar", "interface_updates", "overflow", "foreign", "recovery", "deferred captures" })
                Assert.Contains(term, text, StringComparison.Ordinal);
        }
    }

    [Fact]
    public void WorkedSelectionsAndGmSurfacesKeepAllThreeModeOwnersAndSeparateCourseAttempts()
    {
        const string marker = "mortal_guided_treatment_execution_v1";
        foreach (var path in new[] { "CLI_API_Specification.md", "TaskGuides/CLI_Step_Main.txt", "Rules/Block_CLI_Operations.txt",
                     "Rules/Block_32_Guardians.txt", "OtherGuides/Wound_Materialization_Contract.md", "OtherGuides/Afterlife_Contract_Matrix.md" })
        {
            var text = File.ReadAllText(Path.Combine(TestRepoPaths.RepoRoot, path));
            var start = text.IndexOf(marker + " — existing", StringComparison.Ordinal); Assert.True(start >= 0, path);
            foreach (var term in new[] { "procedure", "course", "guaranteed", "mortal_guided_treatment_input", "mortalTreatment",
                         "guidedTreatmentRequest", "TooEarly", "Unsatisfied", "InvalidAuthority", "separate confirmation",
                         "genuine nonempty repair wave", "signed selection is immutable", "v1 receipts", "publication v2 witness",
                         "mortal_guided_terminal_heal_v1", "linked treat→heal", "one separate ordered batch" })
                Assert.Contains(term, text[start..], StringComparison.Ordinal);
        }
        var xml = XDocument.Parse(File.ReadAllText(Path.Combine(TestRepoPaths.RepoRoot, "Rules/Block_32_Guardians.txt")));
        Assert.Contains(marker, xml.Root!.Value);
        var daemon = File.ReadAllText(Path.Combine(TestRepoPaths.RepoRoot, "BookOfEternityClient/game_master_daemon.ps1"));
        var assignments = Regex.Matches(daemon, "\\$script:WoundMaterializationDirective = \"[^\"]*\"");
        Assert.Equal(2, assignments.Count); Assert.All(assignments.Cast<Match>(), assignment =>
        {
            Assert.Contains(marker, assignment.Value); Assert.Contains("mortal_guided_terminal_heal_v1", assignment.Value);
        });
        var entry = Assert.Single(ExampleValidationManifest.Load().EffectMaterializationCoverage, value => value.ContractId == marker);
        var example = File.ReadAllText(Path.Combine(TestRepoPaths.RepoRoot, "Examples", entry.File));
        Assert.All(entry.RequiredText, term => Assert.Contains(term, example));
        var first = example.IndexOf(marker + " — worked", StringComparison.Ordinal);
        var last = example.IndexOf("end_" + marker, first, StringComparison.Ordinal);
        Assert.True(first >= 0 && last > first);
        var blocks = Regex.Matches(example[first..last], "```json\\s*([\\s\\S]*?)```", RegexOptions.CultureInvariant).Cast<Match>().ToArray();
        Assert.Equal(2, blocks.Length);
        var json = blocks[0];
        var terminal = MortalWoundTreatmentContract.ParseRouteShape(JsonSerializer.SerializeToElement(
            JsonNode.Parse(blocks[1].Groups[1].Value)), "workedTerminal");
        Assert.True(terminal.IsValid, JsonSerializer.Serialize(terminal.Issues));
        var guaranteed = Assert.IsType<MortalWoundGuaranteedRouteDefinition>(terminal.Route);
        Assert.Equal(new[] { 1, 2 }, guaranteed.Outcome.DeclaredResult.OfType<MortalWoundReduceSeverityOperation>().Select(operation => operation.Steps));
        Assert.IsType<MortalWoundHealOperation>(guaranteed.Outcome.DeclaredResult[^1]);
        var selections = JsonNode.Parse(json.Groups[1].Value)!.AsArray(); Assert.Equal(3, selections.Count);
        var frozen = selections.ToJsonString();
        foreach (var (node, mode) in selections.Zip(new[] { "procedure", "course", "guaranteed" }))
        {
            var selection = SpiritualSelfTreatmentSelection.Parse(node);
            Assert.Equal(mode, selection.MortalTreatment!.Mode);
            Assert.Null(selection.MortalDiagnosis); Assert.Null(selection.Diagnosis); Assert.Null(selection.Combat);
            Assert.Equal(mode == "course" ? 1 : (int?)null, selection.MortalTreatment.MilestoneOrdinal);
            Assert.True(JsonNode.DeepEquals(node, selection.ToJson()));
            var changed = node!.DeepClone().AsObject(); changed["diagnosisPathId"] = "foreign_purpose";
            Assert.Throws<InvalidDataException>(() => SpiritualSelfTreatmentSelection.Parse(changed));
        }
        Assert.Equal(frozen, selections.ToJsonString());

        Assert.DoesNotContain("Direct treatment death, heal and legacy publication remain unfinished.",
            example, StringComparison.Ordinal);
        Assert.DoesNotContain("The direct treatment `heal`, explicit death and legacy producers remain pending implementation",
            example, StringComparison.Ordinal);

        // FR-086: the worked route must exercise the contracted mechanical branch,
        // rather than mention it only in prose after a cosmetic-only example.
        var heal = Assert.IsType<MortalWoundHealOperation>(guaranteed.Outcome.DeclaredResult[^1]);
        var cosmetic = Assert.Single(heal.Legacies.OfType<MortalWoundCosmeticLegacyDraft>());
        Assert.Equal("pale_scar", cosmetic.LocalLegacyRef);
        var mechanical = Assert.Single(heal.Legacies.OfType<MortalWoundMechanicalEffectLegacyDraft>());
        Assert.Equal("residual_tremor", mechanical.LocalLegacyRef);
        Assert.Equal(1, mechanical.EffectDraft.SchemaVersion);
        var definition = Assert.Single(mechanical.EffectDraft.Definitions);
        Assert.Equal("tremor_definition_local", definition.DefinitionRef);
        Assert.Equal("residual_hand_tremor", definition.Definition.GetProperty("definitionKey").GetString());
        Assert.Empty(definition.Definition.GetProperty("links").EnumerateArray());
        var application = Assert.Single(mechanical.EffectDraft.Applications);
        Assert.Equal("tremor_application_local", application.ApplicationRef);
        Assert.Equal("tremor_definition_local", application.DefinitionRef);
        Assert.Empty(application.Parameters.EnumerateObject());

        var workedRoute = JsonNode.Parse(blocks[1].Groups[1].Value)!.AsObject();
        var originalRoute = workedRoute.ToJsonString();
        Refuse(legacy => legacy["legacyId"] = "copied_client_id", ".legacyId");
        Refuse(legacy => legacy.Remove("effectDraft"), ".effectDraft");
        Refuse(legacy => legacy["effectDraft"]!["applications"]![0]!["definitionRef"] = "foreign_definition",
            ".effectDraft.applications[0].definitionRef");
        Assert.Equal(originalRoute, workedRoute.ToJsonString());

        void Refuse(Action<JsonObject> mutateLegacy, string suffix)
        {
            var changed = workedRoute.DeepClone().AsObject();
            var legacy = changed["outcomes"]![0]!["result"]![2]!["legacies"]![1]!.AsObject();
            mutateLegacy(legacy);
            var refused = MortalWoundTreatmentContract.ParseRouteShape(
                JsonSerializer.SerializeToElement(changed), "workedTerminal");
            Assert.False(refused.IsValid);
            Assert.Contains(refused.Issues, issue => issue.FilePath ==
                "workedTerminal.outcomes[0].result[2].legacies[1]" + suffix);
        }
    }
}
