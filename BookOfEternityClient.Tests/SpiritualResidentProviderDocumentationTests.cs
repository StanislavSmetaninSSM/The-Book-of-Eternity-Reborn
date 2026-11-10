using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using BookOfEternityClient.Models;
using BookOfEternityClient.Services;
using Xunit;

namespace BookOfEternityClient.Tests;

/// <summary>The worked provider example and effective GM entrypoints share the current closed contract.</summary>
public sealed class SpiritualResidentProviderDocumentationTests
{
    /// <summary>GM proposal/memory and client selection distinguish resident healer from player patient.</summary>
    [Fact]
    public void WorkedResidentOfferHealerMemoryAndConfirmedSelectionMatchRuntimeAndGmEntryPoints()
    {
        const string marker = "spiritual_resident_help_v6";
        foreach (var file in new[] { "OtherGuides/Wound_Materialization_Contract.md", "OtherGuides/Afterlife_Contract_Matrix.md",
                     "CLI_API_Specification.md", "Rules/Block_CLI_Operations.txt", "TaskGuides/CLI_Step_Main.txt", "BookOfEternityClient/game_master_daemon.ps1" })
            Assert.Contains(marker, File.ReadAllText(Path.Combine(TestRepoPaths.RepoRoot, file)));
        var daemon = File.ReadAllText(Path.Combine(TestRepoPaths.RepoRoot, "BookOfEternityClient/game_master_daemon.ps1"));
        var directives = Regex.Matches(daemon, "\\$script:WoundMaterializationDirective = \\\"[^\\\"]*\\\"");
        Assert.Equal(2, directives.Count);
        Assert.All(directives.Cast<Match>(), assignment => Assert.Contains(marker, assignment.Value));
        var entry = Assert.Single(ExampleValidationManifest.Load().EffectMaterializationCoverage, e => e.ContractId == marker);
        var text = File.ReadAllText(Path.Combine(TestRepoPaths.RepoRoot, "Examples", entry.File));
        Assert.All(entry.RequiredText, token => Assert.Contains(token, text));
        var start = text.IndexOf(marker + " — worked", StringComparison.Ordinal); Assert.True(start >= 0);
        var fragments = Regex.Matches(text[start..], "```json\\s*([\\s\\S]*?)```", RegexOptions.CultureInvariant)
            .Cast<Match>().Take(3).Select(m => JsonNode.Parse(m.Groups[1].Value)!.AsObject()).ToArray();
        Assert.Equal(3, fragments.Length);
        var previous = new JsonObject { ["profiles"] = new JsonArray(new JsonObject
        { ["actorType"] = "resident", ["actorId"] = "resident_example", ["realm"] = "Chaos Sea", ["locationId"] = "abode_example" }) };
        var response = previous.DeepClone().AsObject();
        response["profiles"]![0]![SpiritualTreatmentParticipation.ChangeProperty] = fragments[0].DeepClone();
        var change = Assert.Single(SpiritualTreatmentParticipation.ReadChanges(response, previous));
        var selection = SpiritualSelfTreatmentSelection.Parse(fragments[2]);
        Assert.Equal(change.Owner, selection.Aid!.Healer("chaos_sea"));
        Assert.Equal(new WoundOwnerCoordinate("chaos_sea", "player_soul", "player_soul", WoundCarrierCatalog.AfterlifeProfilesPath), selection.Aid.Target("chaos_sea"));
        Assert.Equal(6, selection.Aid.FamilyVersion); Assert.True(selection.Aid.ReceiveAid); Assert.False(selection.Aid.PlayerConfirmed);
        Assert.Equal(change.Proposal["woundId"]!.GetValue<string>(), selection.WoundId);
        Assert.Equal(WoundApplicationService.ResidentReceiveAidActionText, WoundApplicationService.ActionFor(selection));
        var confirmed = selection with { Aid = selection.Aid with { PlayerConfirmed = true } };
        Assert.Equal("player_target_confirmed", confirmed.ToJson()["confirmation"]!.GetValue<string>());
        var memory = Assert.IsType<JsonArray>(fragments[1][SpiritualTreatmentParticipation.ResidentThoughtResponseProperty]);
        Assert.Single(SpiritualTreatmentParticipation.ValidateResidentThoughtAppend(memory, change.Owner.OwnerId, 43, new JsonObject()));
        Assert.Throws<InvalidDataException>(() => SpiritualTreatmentParticipation.ValidateResidentThoughtAppend(memory, "player_soul", 43, new JsonObject()));
        Assert.Single(JsonSerializer.Deserialize<GameResponse>(fragments[1].ToJsonString())!.SpiritualResidentThoughtJournalUpdates!);
        Assert.Contains(SpiritualTreatmentParticipation.ResidentThoughtResponseProperty, BookOfEternityClient.Configuration.FileMapping.OutputOnlyResponseFields);
        Assert.False(BookOfEternityClient.Configuration.FileMapping.FieldToFile.ContainsKey(SpiritualTreatmentParticipation.ResidentThoughtResponseProperty));
    }
}
