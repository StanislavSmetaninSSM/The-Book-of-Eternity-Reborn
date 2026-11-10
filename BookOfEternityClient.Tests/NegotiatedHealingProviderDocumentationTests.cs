using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using BookOfEternityClient.Services;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed class NegotiatedHealingProviderDocumentationTests
{
    [Fact]
    public void WorkedBothProviderVariantsAndEffectiveGuidanceUseTheClosedExistingContract()
    {
        const string marker = "spiritual_negotiated_provider_v1";
        foreach (var file in new[] { "OtherGuides/Wound_Materialization_Contract.md", "OtherGuides/Afterlife_Contract_Matrix.md",
            "CLI_API_Specification.md", "Rules/Block_CLI_Operations.txt", "Rules/Block_32_Guardians.txt", "TaskGuides/CLI_Step_Main.txt",
            "BookOfEternityClient/system_guardians/built_in/elyara/dossier.md" })
            Assert.Contains(marker, File.ReadAllText(Path.Combine(TestRepoPaths.RepoRoot, file)));
        Assert.Contains(marker, XDocument.Parse(File.ReadAllText(Path.Combine(TestRepoPaths.RepoRoot,
            "Rules/Block_32_Guardians.txt"))).Root!.Value);
        var daemon = File.ReadAllText(Path.Combine(TestRepoPaths.RepoRoot, "BookOfEternityClient/game_master_daemon.ps1"));
        var directives = Regex.Matches(daemon, "\\$script:WoundMaterializationDirective = \\\"[^\\\"]*\\\"");
        Assert.Equal(2, directives.Count); Assert.All(directives.Cast<Match>(), d => Assert.Contains(marker, d.Value));
        var entry = Assert.Single(ExampleValidationManifest.Load().EffectMaterializationCoverage, e => e.ContractId == marker);
        var text = File.ReadAllText(Path.Combine(TestRepoPaths.RepoRoot, "Examples", entry.File));
        Assert.All(entry.RequiredText, token => Assert.Contains(token, text));
        var start = text.IndexOf(marker + " — worked", StringComparison.Ordinal);
        var end = text.IndexOf("end_" + marker, start, StringComparison.Ordinal);
        Assert.True(start >= 0 && end > start);
        var proposals = Regex.Matches(text[start..end], "```json\\s*([\\s\\S]*?)```", RegexOptions.CultureInvariant)
            .Cast<Match>().Select(m => JsonNode.Parse(m.Groups[1].Value)!.AsObject()).ToArray();
        Assert.Equal(2, proposals.Length);
        foreach (var proposal in proposals)
        {
            var resident = proposal["schemaVersion"]!.GetValue<int>() == 4;
            var owner = new WoundOwnerCoordinate("shining_abode", resident ? "resident" : "guardian",
                resident ? "resident_example" : AfterlifeWoundTreatmentAuthority.ElyaraId, WoundCarrierCatalog.AfterlifeProfilesPath);
            var previous = new JsonObject { ["profiles"] = new JsonArray(new JsonObject
                { ["actorType"] = owner.OwnerKind, ["actorId"] = owner.OwnerId, ["realm"] = "Shining Abode" }) };
            var response = previous.DeepClone().AsObject();
            response["profiles"]![0]![SpiritualTreatmentParticipation.ChangeProperty] = proposal.DeepClone();
            var change = Assert.Single(SpiritualTreatmentParticipation.ReadChanges(response, previous));
            var locality = SpiritualTreatmentParticipation.ReadLocality(proposal["locality"]);
            var binding = new JsonObject { ["sessionId"] = "session_example", ["requestId"] = "request_example",
                ["snapshotToken"] = "snapshot_example", ["realm"] = "shining_abode", ["turn"] = 43 };
            var record = SpiritualTreatmentParticipation.CreateOutcome(change, locality, binding, 1, "transition_example")["record"]!.AsObject();
            var selection = new SpiritualSelfTreatmentSelection("wound_player_example", "sha256:" + new string('a', 64))
                { Aid = SpiritualTreatmentParticipation.Selection(record) };
            Assert.Equal(selection, SpiritualSelfTreatmentSelection.Parse(selection.ToJson()));
            Assert.False(selection.Aid.PlayerConfirmed);
            Assert.Contains(proposal["compensation"]!["readableTerms"]!.GetValue<string>(), WoundApplicationService.ConfirmationFor(selection));
        }
    }
}
