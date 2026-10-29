using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using BookOfEternityClient.Models;
using BookOfEternityClient.Services;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed class ElyaraCurrentLocationDocumentationTests
{
    private const string Marker = "elyara_current_location_v1";

    [Fact]
    public void EffectiveGuidanceAndWorkedManifestPreserveCurrentPlaceAndSeparateCycles()
    {
        foreach (var path in new[] { "OtherGuides/Wound_Materialization_Contract.md", "OtherGuides/Afterlife_Contract_Matrix.md",
            "CLI_API_Specification.md", "Rules/Block_CLI_Operations.txt", "Rules/Block_32_Guardians.txt", "TaskGuides/CLI_Step_Main.txt",
            "BookOfEternityClient/system_guardians/built_in/elyara/dossier.md" })
        {
            var text = File.ReadAllText(Path.Combine(TestRepoPaths.RepoRoot, path));
            var start = text.IndexOf(Marker + ":", StringComparison.Ordinal); Assert.True(start >= 0);
            foreach (var token in new[] { "HOME", "ONE ordinary travel cycle", "NEW /лечить quote", "ONE exact alive",
                "currentHallId", "Shining cycle", "ZERO active treatment", "Remote", "historical-faction" })
                Assert.Contains(token, text[start..]);
        }
        XDocument.Parse(File.ReadAllText(Path.Combine(TestRepoPaths.RepoRoot, "Rules/Block_32_Guardians.txt")));
        var daemon = File.ReadAllText(Path.Combine(TestRepoPaths.RepoRoot, "BookOfEternityClient/game_master_daemon.ps1"));
        var directives = Regex.Matches(daemon, "\\$script:WoundMaterializationDirective = \\\"[^\\\"]*\\\"");
        Assert.Equal(2, directives.Count);
        Assert.All(directives.Cast<Match>(), match => Assert.Contains(Marker, match.Value));
        var entry = Assert.Single(ExampleValidationManifest.Load().EffectMaterializationCoverage, e => e.ContractId == Marker);
        var example = File.ReadAllText(Path.Combine(TestRepoPaths.RepoRoot, "Examples", entry.File));
        Assert.All(entry.RequiredText, token => Assert.Contains(token, example));
    }

    [Fact]
    public void WorkedRelocatedHostAndShiningCompanionUseActualProjectionAndResponseContracts()
    {
        var text = File.ReadAllText(Path.Combine(TestRepoPaths.RepoRoot, "Examples/E_CLI_Afterlife_Turns.txt"));
        var start = text.IndexOf(Marker + " — worked", StringComparison.Ordinal);
        var end = text.IndexOf("end_" + Marker, start, StringComparison.Ordinal); Assert.True(end > start);
        var fragments = Regex.Matches(text[start..end], "```json\\s*([\\s\\S]*?)```", RegexOptions.CultureInvariant)
            .Cast<Match>().Select(m => JsonNode.Parse(m.Groups[1].Value)!.AsObject()).ToArray();
        Assert.Equal(3, fragments.Length);
        var guardianData = fragments[0]["guardians"]!.AsObject();
        for (var index = 0; index < 2; index++)
        {
            var excerpt = fragments[index];
            var actor = AfterlifeActorMaterializationTestFixture.CreateCompleteProfile("guardian", AfterlifeWoundTreatmentAuthority.ElyaraId,
                excerpt["realm"]!.GetValue<string>(), 42);
            actor["lifeState"] = "alive"; actor["locationId"] = excerpt["locationId"]!.DeepClone();
            actor["standardArts"]!["spiritual_healing"] = 5;
            actor["healingServiceProfile"] = AfterlifeHealingServiceTestData.Actor()["healingServiceProfile"]!.DeepClone();
            actor["healingServiceProfile"]!["realm"] = excerpt["serviceRealm"]!.DeepClone();
            actor["healingServiceProfile"]!["locationRef"] = excerpt["serviceLocationRef"]!.DeepClone();
            var input = new ElyaraVisitInput(excerpt["soul"]!.AsObject(), new() { ["profiles"] = new JsonArray(actor) },
                guardianData, excerpt["shining"] as JsonObject, null);
            var plan = ElyaraVisitService.Project(input, "generation_worked_elyara", ElyaraVisitService.DataFingerprint(input, "generation_worked_elyara"));
            Assert.Equal(index == 0 ? ElyaraVisitKind.ChaosTravel : ElyaraVisitKind.CoLocated, plan.Kind);
            if (index == 0)
            {
                Assert.Equal("guardian_hosted", plan.State!.Intent.TargetGuardianId);
                Assert.Equal("abode_hosted", plan.State.Intent.TargetAbodeId);
                Assert.Equal(plan.State.Intent, ElyaraVisitService.ParseAction(ElyaraVisitService.ActionFor(plan.State.Intent)));
            }
        }
        var response = JsonSerializer.Deserialize<GameResponse>(fragments[2].ToJsonString())!;
        Assert.NotNull(response.SpiritualHealingGraphs); Assert.Empty(response.SpiritualHealingGraphs!);
    }
}
