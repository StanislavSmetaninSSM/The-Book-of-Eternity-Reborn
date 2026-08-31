using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using BookOfEternityClient.Services;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed partial class PromptDocumentationCoverageTests
{
    [Fact]
    public void WoundMaterializationContract_DocumentsStrictConstructorAndAuthority()
    {
        var contract = ReadRepoFile(
            "OtherGuides",
            "Wound_Materialization_Contract.md");

        foreach (var marker in new[]
                 {
                     "wound_constructor_v1",
                     "wound_optional_creation_v1",
                     "wound_guaranteed_creation_v1",
                     "wound_acquisition_narration_v1",
                     "wound_effect_separation_v1",
                     "wound_spiritual_profiles_v1"
                 })
        {
            Assert.Contains(marker, contract, StringComparison.Ordinal);
        }

        foreach (var required in new[]
                 {
                     "woundDecisions",
                     "\"decision\": \"none\"",
                     "\"decision\": \"materialize\"",
                     "woundRef",
                     "acquisitionNarration",
                     "severity I-IV",
                     "maximumSeverity",
                     "guaranteed",
                     "client-owned",
                     "activeEffectDefinitions[]",
                     "effectChanges[]",
                     "Effect removal never heals or deletes the wound",
                     "Death and soul dissipation are separate outcomes",
                     "no migration"
                 })
        {
            Assert.Contains(required, contract, StringComparison.OrdinalIgnoreCase);
        }

        Assert.DoesNotContain("playerWoundChanges", contract, StringComparison.Ordinal);
        Assert.DoesNotContain("NPCWoundChanges", contract, StringComparison.Ordinal);
    }

    [Fact]
    public void WoundRulesAndWorkedExamples_UseOnlyStrictOptionalConstructor()
    {
        var rules = ReadRepoFile("Rules", "Block_5.txt");
        var examples = ReadRepoFile("Examples", "E_Block_5.txt");

        foreach (var required in new[]
                 {
                     "Wound Materialization v1",
                     "Wound_Materialization_Contract.md",
                     "woundDecisions",
                     "opportunityRef",
                     "acquisitionNarration",
                     "ordinary wound is optional",
                     "guaranteed result"
                 })
        {
            Assert.Contains(required, rules, StringComparison.OrdinalIgnoreCase);
        }

        foreach (var contractId in new[]
                 {
                     "wound_optional_creation_v1",
                     "wound_lower_severity_creation_v1",
                     "wound_guaranteed_creation_v1",
                     "wound_rejected_creation_v1"
                 })
        {
            Assert.Contains(contractId, examples, StringComparison.Ordinal);
        }

        foreach (var document in new[] { rules, examples })
        {
            Assert.DoesNotContain("playerWoundChanges", document, StringComparison.Ordinal);
            Assert.DoesNotContain("NPCWoundChanges", document, StringComparison.Ordinal);
        }
    }

    [Fact]
    public void MortalDeteriorationPolicyDocumentation_UsesTheClosedTypedContract()
    {
        var contract = ReadRepoFile(
            "OtherGuides",
            "Wound_Materialization_Contract.md");
        var rules = ReadRepoFile("Rules", "Block_5.txt");
        var examples = ReadRepoFile("Examples", "E_Block_5.txt");

        foreach (var document in new[] { contract, rules, examples })
        {
            Assert.Contains(
                "wound_mortal_deterioration_policy_v1",
                document,
                StringComparison.Ordinal);
            foreach (var required in new[]
                     {
                         "policyRef", "unmetConditions", "graceMinutes",
                         "cadenceMinutes", "increase_severity"
                     })
            {
                Assert.Contains(required, document, StringComparison.Ordinal);
            }
        }

        foreach (var required in new[]
                 {
                     "add_complication", "complicationDraft", "death_contour",
                     "no_change", "add_recovery", "severity IV",
                     "silently converted", "16 complications",
                     "four total consequence slots", "five total owned effect definitions"
                 })
        {
            Assert.Contains(required, contract, StringComparison.OrdinalIgnoreCase);
        }

        var workedMatch = Regex.Match(
            contract,
            @"wound_mortal_deterioration_policy_v1.*?```json\s*(?<json>.*?)```",
            RegexOptions.Singleline | RegexOptions.CultureInvariant);
        Assert.True(workedMatch.Success, "Missing worked Mortal deterioration policy JSON.");
        var policy = Assert.IsType<JsonObject>(
            JsonNode.Parse(workedMatch.Groups["json"].Value));
        var wound = WoundContractTestData.CreateActiveWound();
        wound["recovery"]!["deteriorationPolicy"] = policy.DeepClone();

        var parsed = WoundMaterializationContract.Parse(
            wound.ToJsonString(),
            "documentedWound");

        Assert.True(parsed.IsValid, string.Join(" | ", parsed.Issues.Select(issue =>
            $"{issue.Code}@{issue.FilePath}:{issue.Actual}")));
    }

    [Fact]
    public void GmDaemon_ForcesWoundContractForExposedOpportunities()
    {
        var daemon = ReadRepoFile(
            "BookOfEternityClient",
            "game_master_daemon.ps1");

        foreach (var required in new[]
                 {
                     "Wound_Materialization_Contract.md",
                     "WoundMaterializationContractPath",
                     "WoundMaterializationDirective",
                     "woundDecisions",
                     "opportunityRef",
                     "acquisitionNarration",
                     "ordinary wound is optional",
                     "guaranteed"
                 })
        {
            Assert.Contains(required, daemon, StringComparison.OrdinalIgnoreCase);
        }
    }

    [Fact]
    public void WoundTreatmentSceneAuthorityDocumentation_CoversClosedKindsAndAuthoringRoutes()
    {
        var contract = ReadRepoFile(
            "OtherGuides",
            "Wound_Materialization_Contract.md");
        var example = ReadRepoFile(
            "Examples",
            "E_CLI_Wound_Materialization.txt");
        var manifest = ReadRepoFile(
            "Examples",
            "example_validation_manifest.json");
        var daemon = ReadRepoFile(
            "BookOfEternityClient",
            "game_master_daemon.ps1");

        foreach (var document in new[] { contract, example })
        {
            foreach (var required in new[]
                     {
                         "mortal_wound_treatment_facility",
                         "mortal_wound_treatment_environment",
                         "mortal_wound_treatment_consent",
                         "worldMapUpdates.locationUpdates[]",
                         "complete replacement",
                         "preserve every unrelated",
                         "currentLocationData",
                         "worldMapUpdates.newLocations[]",
                         "link `customStates[]`",
                         "no universal catalog"
                     })
            {
                Assert.Contains(required, document, StringComparison.OrdinalIgnoreCase);
            }
        }

        foreach (var marker in new[]
                 {
                     "wound_treatment_scene_authority_v1",
                     "wound_treatment_existing_location_update_v1",
                     "wound_treatment_new_selected_location_v1",
                     "wound_treatment_new_remote_location_v1",
                     "wound_treatment_link_rejection_v1"
                 })
        {
            Assert.Contains(marker, example, StringComparison.Ordinal);
            Assert.Contains(marker, manifest, StringComparison.Ordinal);
        }

        static JsonNode ReadWorkedJson(string document, string marker)
        {
            var match = Regex.Match(
                document,
                Regex.Escape(marker) + @".*?```json\s*(?<json>.*?)```",
                RegexOptions.Singleline | RegexOptions.CultureInvariant);
            Assert.True(match.Success, $"Missing JSON block for {marker}.");
            return Assert.IsAssignableFrom<JsonNode>(
                JsonNode.Parse(match.Groups["json"].Value));
        }

        static void AssertValidLocationStates(JsonNode states, string marker)
        {
            using var parsed = JsonDocument.Parse(states.ToJsonString());
            Assert.Empty(MortalLocationCustomStateContract.ValidateLocation(
                parsed.RootElement,
                marker + ".customStates"));
        }

        AssertValidLocationStates(
            ReadWorkedJson(example, "wound_treatment_scene_authority_v1"),
            "wound_treatment_scene_authority_v1");
        AssertValidLocationStates(
            ReadWorkedJson(example, "wound_treatment_existing_location_update_v1")!
                ["worldMapUpdates"]!["locationUpdates"]![0]!["customStates"]!,
            "wound_treatment_existing_location_update_v1");
        AssertValidLocationStates(
            ReadWorkedJson(example, "wound_treatment_new_selected_location_v1")!
                ["currentLocationData"]!["customStates"]!,
            "wound_treatment_new_selected_location_v1");
        AssertValidLocationStates(
            ReadWorkedJson(example, "wound_treatment_new_remote_location_v1")!
                ["worldMapUpdates"]!["newLocations"]![0]!["customStates"]!,
            "wound_treatment_new_remote_location_v1");
        var invalidLinkStates = ReadWorkedJson(
            example,
            "wound_treatment_link_rejection_v1")!
            ["worldMapUpdates"]!["newLinks"]![0]!["customStates"]!;
        using (var parsed = JsonDocument.Parse(invalidLinkStates.ToJsonString()))
        {
            Assert.Contains(
                MortalLocationCustomStateContract.ValidateLink(
                    parsed.RootElement,
                    "wound_treatment_link_rejection_v1.customStates"),
                issue => issue.Code == "mortal_wound_treatment_scene_state_wrong_container");
        }

        foreach (var required in new[]
                 {
                     "WoundTreatmentExamplePath",
                     "Examples\\E_CLI_Wound_Materialization.txt",
                     "wound_treatment_scene_authority_v1",
                     "Wound_Materialization_Contract.md",
                     "E_CLI_Wound_Materialization.txt",
                     "CompactMortalLocationTemplatePath"
                 })
        {
            Assert.Contains(required, daemon, StringComparison.OrdinalIgnoreCase);
        }
    }

    [Fact]
    public void SpiritualWoundProfileDocumentation_MatchesExactRuntimeRegistry()
    {
        var expected = SpiritualWoundEffectProfileCatalog.RegisteredProfiles
            .OrderBy(static profile => profile, StringComparer.Ordinal)
            .ToArray();
        Assert.Equal(8, expected.Length);

        foreach (var document in new[]
                 {
                     ReadRepoFile("OtherGuides", "Wound_Materialization_Contract.md"),
                     ReadRepoFile("OtherGuides", "Effect_Materialization_Contract.md")
                 })
        {
            var section = ExtractWoundDocumentationSection(
                document,
                "wound_spiritual_profiles_v1");
            var documented = Regex.Matches(
                    section,
                    "`(spiritual_[a-z_]+)`",
                    RegexOptions.CultureInvariant)
                .Select(static match => match.Groups[1].Value)
                .Distinct(StringComparer.Ordinal)
                .OrderBy(static profile => profile, StringComparer.Ordinal)
                .ToArray();

            Assert.Equal(expected, documented);
            Assert.Contains("profile_specific", section, StringComparison.Ordinal);
            Assert.Contains("operation", section, StringComparison.Ordinal);
            Assert.Contains("axis", section, StringComparison.Ordinal);
            Assert.Contains("magnitude", section, StringComparison.Ordinal);
            Assert.Contains("afterlife_combat_condition", section, StringComparison.Ordinal);
            Assert.Contains("MUST NOT", section, StringComparison.OrdinalIgnoreCase);
        }
    }

    [Fact]
    public void WoundRepairReplayGuidance_DocumentsBoundedRetryRollbackAndReceipt()
    {
        var contract = ReadRepoFile(
            "OtherGuides",
            "Wound_Materialization_Contract.md");
        var rules = ReadRepoFile("Rules", "Block_12.txt");
        var examples = ReadRepoFile("Examples", "E_Block_12.txt");

        foreach (var document in new[] { contract, rules, examples })
        {
            foreach (var marker in new[]
                     {
                         "wound_repair_retry_v1",
                         "wound_stale_repair_packet_v1",
                         "wound_rollback_replay_v1"
                     })
            {
                Assert.Contains(marker, document, StringComparison.Ordinal);
            }

            foreach (var required in new[]
                     {
                         "wound_materialization_repair",
                         "complete corrected semantic turn",
                         "exact before-image",
                         "fresh authority",
                         "already-accepted receipt",
                         "every packet and every listed path in the current repair wave",
                         "requiredResponseShape.woundDecisions[0].proposal.correctOnly",
                         "internal history resolver guarantee",
                         "unknown operation key is no replay match",
                         "matched operation key"
                     })
            {
                Assert.Contains(required, document, StringComparison.OrdinalIgnoreCase);
            }
        }

        foreach (var required in new[]
                 {
                     "wound_severity_above_opportunity",
                     "maximumSeverity",
                     "woundDecisions",
                     "acquisitionNarration",
                     "no second charge",
                     "no second history transition"
                 })
        {
            Assert.Contains(required, examples, StringComparison.OrdinalIgnoreCase);
        }

        Assert.Contains(
            "rollback-tracked canonical and player-output",
            contract,
            StringComparison.OrdinalIgnoreCase);
        Assert.Contains(
            "opaque binding envelope",
            contract,
            StringComparison.OrdinalIgnoreCase);
        var staleMarker = contract.IndexOf(
            "wound_stale_repair_packet_v1",
            StringComparison.Ordinal);
        var replayMarker = contract.IndexOf(
            "wound_rollback_replay_v1",
            StringComparison.Ordinal);
        Assert.True(staleMarker >= 0 && replayMarker > staleMarker);
        Assert.DoesNotContain(
            "receipt",
            contract[staleMarker..replayMarker],
            StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(
            "receipt changed before correction",
            examples,
            StringComparison.OrdinalIgnoreCase);

        var exampleDocument = XDocument.Parse(examples, LoadOptions.PreserveWhitespace);
        var packetText = Assert.Single(exampleDocument
            .Descendants("clientrepairpacketexcerpt"))
            .Value;
        var packet = Assert.IsType<JsonObject>(JsonNode.Parse(packetText));
        Assert.Equal(
            new[]
            {
                "kind", "sessionId", "requestId", "snapshotToken", "candidateRef",
                "semanticFingerprint", "issues", "safeContext", "preservedProposal",
                "requiredResponseShape"
            },
            packet.Select(static pair => pair.Key));
        Assert.Equal(
            "wound_materialization_repair",
            packet["kind"]!.GetValue<string>());
        var preservedProposal = packet["preservedProposal"]!.AsObject();
        Assert.False(preservedProposal.ContainsKey("severity"));
        Assert.Equal(
            "proposal.severity",
            packet["requiredResponseShape"]!["woundDecisions"]![0]!["proposal"]!
                ["correctOnly"]![0]!.GetValue<string>());
        var serializedPacket = packet.ToJsonString();
        foreach (var sensitiveKey in new[]
                 {
                     "woundId", "effectId", "ownerId", "providerId", "resourceId",
                     "routeSeal", "resourceSeal", "providerSeal", "sourceSeal",
                     "carrierPath", "authorityFingerprint", "opportunityId", "eventRef",
                     "privateNpcData", "gmPrivateNotes"
                 })
        {
            Assert.DoesNotContain(
                $"\"{sensitiveKey}\"",
                serializedPacket,
                StringComparison.Ordinal);
        }
    }

    private static string ExtractWoundDocumentationSection(
        string document,
        string marker)
    {
        var markerIndex = document.IndexOf(marker, StringComparison.Ordinal);
        Assert.True(markerIndex >= 0, $"Missing documentation section '{marker}'.");
        var nextHeading = document.IndexOf(
            "\n## ",
            markerIndex + marker.Length,
            StringComparison.Ordinal);
        return nextHeading < 0
            ? document[markerIndex..]
            : document[markerIndex..nextHeading];
    }
}
