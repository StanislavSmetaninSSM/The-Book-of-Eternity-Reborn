using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using BookOfEternityClient.Models;
using BookOfEternityClient.Services;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed class SpiritualResidentAidDocumentationTests
{
    [Fact]
    public void WorkedResidentConsentMemoryAndSelectionMatchTheClosedRuntimeAndGmEntryPoints()
    {
        const string marker = "spiritual_resident_aid_v5";
        foreach (var file in new[] { "OtherGuides/Wound_Materialization_Contract.md", "OtherGuides/Afterlife_Contract_Matrix.md",
                     "CLI_API_Specification.md", "Rules/Block_CLI_Operations.txt", "TaskGuides/CLI_Step_Main.txt", "BookOfEternityClient/game_master_daemon.ps1" })
            Assert.Contains(marker, File.ReadAllText(Path.Combine(TestRepoPaths.RepoRoot, file)));
        var daemon = File.ReadAllText(Path.Combine(TestRepoPaths.RepoRoot, "BookOfEternityClient/game_master_daemon.ps1"));
        var directives = Regex.Matches(daemon, "\\$script:WoundMaterializationDirective = \\\"[^\\\"]*\\\"");
        Assert.NotEmpty(directives.Cast<Match>());
        Assert.All(directives.Cast<Match>(), assignment => Assert.Contains(marker, assignment.Value));
        var entry = Assert.Single(ExampleValidationManifest.Load().EffectMaterializationCoverage, e => e.ContractId == marker);
        var text = File.ReadAllText(Path.Combine(TestRepoPaths.RepoRoot, "Examples", entry.File));
        Assert.All(entry.RequiredText, token => Assert.Contains(token, text));
        var start = text.IndexOf(marker + " — worked", StringComparison.Ordinal);
        Assert.True(start >= 0);
        var fragments = Regex.Matches(text[start..], "```json\\s*([\\s\\S]*?)```", RegexOptions.CultureInvariant)
            .Cast<Match>().Take(3).Select(m => JsonNode.Parse(m.Groups[1].Value)!.AsObject()).ToArray();
        Assert.Equal(3, fragments.Length);
        var previous = new JsonObject { ["profiles"] = new JsonArray(new JsonObject
        { ["actorType"] = "resident", ["actorId"] = "resident_example", ["realm"] = "Chaos Sea", ["locationId"] = "abode_example" }) };
        var response = previous.DeepClone().AsObject();
        response["profiles"]![0]![SpiritualTreatmentParticipation.ChangeProperty] = fragments[0].DeepClone();
        var change = Assert.Single(SpiritualTreatmentParticipation.ReadChanges(response, previous));
        Assert.Equal("resident", change.Owner.OwnerKind);
        var selection = SpiritualSelfTreatmentSelection.Parse(fragments[2]);
        Assert.Equal(change.Owner, selection.Aid!.Target("chaos_sea"));
        Assert.Equal(change.Proposal["woundId"]!.GetValue<string>(), selection.WoundId);
        Assert.Equal(WoundApplicationService.ResidentAidActionText, WoundApplicationService.ActionFor(selection));
        var memory = Assert.IsType<JsonArray>(fragments[1][SpiritualTreatmentParticipation.ResidentThoughtResponseProperty]);
        Assert.Single(SpiritualTreatmentParticipation.ValidateResidentThoughtAppend(memory, "resident_example", 43, new JsonObject()));
        var typed = JsonSerializer.Deserialize<GameResponse>(fragments[1].ToJsonString())!;
        Assert.Single(typed.SpiritualResidentThoughtJournalUpdates!);
        Assert.Contains(SpiritualTreatmentParticipation.ResidentThoughtResponseProperty, BookOfEternityClient.Configuration.FileMapping.OutputOnlyResponseFields);
        Assert.False(BookOfEternityClient.Configuration.FileMapping.FieldToFile.ContainsKey(SpiritualTreatmentParticipation.ResidentThoughtResponseProperty));
        Assert.Equal(GuardianAbodeResidentState.StatePath, BookOfEternityClient.Configuration.FileMapping.FieldToFile[GuardianAbodeResidentState.UpdateThoughtJournalProperty]);
    }
}
