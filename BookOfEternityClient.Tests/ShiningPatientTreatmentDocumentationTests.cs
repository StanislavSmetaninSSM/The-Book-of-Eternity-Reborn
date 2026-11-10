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
            foreach (var token in new[] { "shining_target_consent", "player_to_shining_entity", "spiritualResidentThoughtJournalUpdates", "gmThoughtsSummary" })
                Assert.Contains(token, text[start..]);
            Assert.Contains("diagnosis", text[start..], StringComparison.OrdinalIgnoreCase);
            foreach (var token in new[] { "spiritual_shining_patient_memory_v8", "residentThoughtJournalUpdates", "spiritualShiningPatientMemory", "turnNumber", "original + ordinary + protected" })
                Assert.Contains(token, text);
        }
        XDocument.Parse(File.ReadAllText(Path.Combine(TestRepoPaths.RepoRoot, "Rules/Block_32_Guardians.txt")));
        var brainGuide = File.ReadAllText(Path.Combine(TestRepoPaths.RepoRoot, "OtherGuides/Actor_Brain_2_0.md"));
        foreach (var token in new[] { "spiritual_shining_patient_memory_v8", "residentThoughtJournalUpdates", "spiritualShiningPatientMemory", "turnNumber" })
            Assert.Contains(token, brainGuide);
        var repairGuidance = File.ReadAllText(Path.Combine(TestRepoPaths.RepoRoot, "BookOfEternityClient/Core/GameEngine/GameEngine.ValidationAndRepair.cs"));
        Assert.Contains("For actual schema5 Shining patient natural8/active8", repairGuidance);
        Assert.Contains("already published response/program and its complete accepted afterimage are immutable", repairGuidance);
        var daemon = File.ReadAllText(Path.Combine(TestRepoPaths.RepoRoot, "BookOfEternityClient/game_master_daemon.ps1"));
        var assignments = Regex.Matches(daemon, "\\$script:WoundMaterializationDirective = \\\"[^\\\"]*\\\"");
        Assert.Equal(2, assignments.Count); Assert.All(assignments.Cast<Match>(), a =>
        { Assert.Contains(Marker, a.Value); Assert.Contains("spiritual_shining_patient_memory_v8", a.Value); });
        var entry = Assert.Single(ExampleValidationManifest.Load().EffectMaterializationCoverage, e => e.ContractId == Marker);
        var example = File.ReadAllText(Path.Combine(TestRepoPaths.RepoRoot, "Examples", entry.File));
        Assert.All(entry.RequiredText, token => Assert.Contains(token, example));
    }

    [Fact]
    public void WorkedMemoryProgramsUseTheActualDetachedComposerForBothPatientKinds()
    {
        const string marker = "spiritual_shining_patient_memory_v8";
        var entry = Assert.Single(ExampleValidationManifest.Load().EffectMaterializationCoverage, e => e.ContractId == marker);
        var example = File.ReadAllText(Path.Combine(TestRepoPaths.RepoRoot, "Examples", entry.File));
        Assert.All(entry.RequiredText, token => Assert.Contains(token, example));
        var start = example.IndexOf(marker + " — worked", StringComparison.Ordinal);
        var end = example.IndexOf("end_" + marker, start, StringComparison.Ordinal); Assert.True(start >= 0 && end > start);
        var rows = Regex.Matches(example[start..end], "```json\\s*([\\s\\S]*?)```", RegexOptions.CultureInvariant)
            .Cast<Match>().Select(m => JsonNode.Parse(m.Groups[1].Value)!.AsObject()).ToArray();
        Assert.Equal(2, rows.Length);
        foreach (var kind in new[] { "resident", "radiant_actor" })
        {
            var (input, _, locality) = ShiningPatientTreatmentAdmissionTests.Inputs(kind);
            var profile = AfterlifeActorMaterializationTestFixture.CreateCompleteProfile(kind, "shining_patient", "Shining Abode", 42);
            profile["lifeState"] = "alive"; profile["visibility"] = "public";
            profile["locationId"] = ShiningFactionHealingServiceTestData.HallId; profile["locationName"] = "Зал исцеления";
            profile["standardArts"]!["spiritual_healing"] = 0; input.Profiles["profiles"] = new JsonArray(profile);
            if (kind == "resident") input.Residents["entries"]![0]!["residentId"] = "shining_patient";
            else
            {
                input.Shining["factions"]![0]!["leadership"]!["headActorId"] = "shining_patient";
                input.Shining["shiningPoliticalActors"]![0]!["actorId"] = "shining_patient";
            }
            ShiningPatientMemoryProgramTests.AddMemorySupport(input, "support_memory");
            var row = rows[kind == "resident" ? 0 : 1]; var frozen = row.ToJsonString() + input.Profiles.ToJsonString() + input.Residents.ToJsonString();
            var program = SpiritualTreatmentParticipation.PrepareShiningPatientMemory(
                new("shining_abode", kind, "shining_patient", WoundCarrierCatalog.AfterlifeProfilesPath), locality,
                input.Profiles, input.Residents, input.Shining, 43, row[GuardianAbodeResidentState.UpdateThoughtJournalProperty]!.AsArray(),
                row[SpiritualTreatmentParticipation.ResidentThoughtResponseProperty] as JsonArray,
                row[SpiritualTreatmentParticipation.ShiningPatientMemoryResponseProperty] as JsonObject);
            var residents = program.ProjectResidents(input.Residents);
            GuardianAbodeResidentState.ApplyThoughtJournalUpdates(residents, residents[GuardianAbodeResidentState.UpdateThoughtJournalProperty]!.AsArray());
            residents.Remove(GuardianAbodeResidentState.UpdateThoughtJournalProperty);
            program.RequireResidentAfterImage(input.Residents, residents);
            var profiles = input.Profiles.DeepClone().AsObject(); program.ApplyProfiles(profiles); program.RequireProfileAfterImage(profiles);
            Assert.Equal(frozen, row.ToJsonString() + input.Profiles.ToJsonString() + input.Residents.ToJsonString());
        }
        var block = XDocument.Parse(File.ReadAllText(Path.Combine(TestRepoPaths.RepoRoot, "Rules/Block_32_Guardians.txt")));
        Assert.Contains(marker, block.Root!.Value);
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
