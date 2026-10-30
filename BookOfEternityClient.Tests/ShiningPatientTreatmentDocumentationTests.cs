using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using BookOfEternityClient.Services;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed class ShiningPatientTreatmentDocumentationTests
{
    private const string Marker = "spiritual_shining_patient_v8";
    [Fact]
    public void EffectiveGuidanceAndDaemonKeepDistinctPatientAuthorityAndMemoryOwners()
    {
        foreach (var file in new[] { "OtherGuides/Wound_Materialization_Contract.md", "OtherGuides/Afterlife_Contract_Matrix.md",
            "Rules/Block_32_Guardians.txt", "CLI_API_Specification.md", "TaskGuides/CLI_Step_Main.txt" })
        {
            var text = File.ReadAllText(Path.Combine(TestRepoPaths.RepoRoot, file));
            var start = text.IndexOf(Marker, StringComparison.Ordinal); Assert.True(start >= 0);
            foreach (var token in new[] { "shining_target_consent", "player_to_shining_entity", "spiritualResidentThoughtJournalUpdates", "gmThoughtsSummary", "diagnosis" })
                Assert.Contains(token, text[start..]);
        }
        XDocument.Parse(File.ReadAllText(Path.Combine(TestRepoPaths.RepoRoot, "Rules/Block_32_Guardians.txt")));
        var daemon = File.ReadAllText(Path.Combine(TestRepoPaths.RepoRoot, "BookOfEternityClient/game_master_daemon.ps1"));
        var assignments = Regex.Matches(daemon, "\\$script:WoundMaterializationDirective = \\\"[^\\\"]*\\\"");
        Assert.Equal(2, assignments.Count); Assert.All(assignments.Cast<Match>(), a => Assert.Contains(Marker, a.Value));
        var entry = Assert.Single(ExampleValidationManifest.Load().EffectMaterializationCoverage, e => e.ContractId == Marker);
        var example = File.ReadAllText(Path.Combine(TestRepoPaths.RepoRoot, "Examples", entry.File));
        Assert.All(entry.RequiredText, token => Assert.Contains(token, example));
    }

    [Fact]
    public void BothWorkedProposalsUseTheRealPreexistingTypedProfileReaderWithoutMintingRecords()
    {
        var example = File.ReadAllText(Path.Combine(TestRepoPaths.RepoRoot, "Examples/E_CLI_Afterlife_Turns.txt"));
        var start = example.IndexOf(Marker + " — worked", StringComparison.Ordinal); Assert.True(start >= 0);
        var end = example.IndexOf("end_" + Marker, start, StringComparison.Ordinal); Assert.True(end > start);
        var rows = Regex.Matches(example[start..end], "```json\\s*([\\s\\S]*?)```", RegexOptions.CultureInvariant)
            .Cast<Match>().Select(m => JsonNode.Parse(m.Groups[1].Value)!.AsObject()).ToArray();
        Assert.Equal(2, rows.Length);
        foreach (var row in rows)
        {
            var kind = row["actorType"]!.GetValue<string>();
            var profile = AfterlifeActorMaterializationTestFixture.CreateCompleteProfile(kind, "shining_patient", "Shining Abode", 42);
            profile["locationId"] = "hall_patient";
            var original = new JsonObject { ["profiles"] = new JsonArray(profile) };
            var response = new JsonObject { ["afterlifeEntityProfileUpdates"] = new JsonArray(row.DeepClone()) };
            var frozen = original.ToJsonString() + response.ToJsonString();
            var change = Assert.Single(SpiritualTreatmentParticipation.ReadChanges(response, original));
            Assert.Equal(kind, change.Owner.OwnerKind); Assert.Equal("shining_abode", change.Owner.Realm);
            Assert.Equal(9, change.Proposal.Count); Assert.False(profile.ContainsKey(SpiritualTreatmentParticipation.RecordProperty));
            Assert.Equal(frozen, original.ToJsonString() + response.ToJsonString());
        }
    }
}
