using System.Text.Json;
using BookOfEternityClient.Services;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed class FactionMaterializationContractTests
{
    [Fact]
    public void Validate_MortalMissingRequiredSection_ReportsStableIssue()
    {
        using var document = JsonDocument.Parse(BuildMortalFactionJson(
            sectionsOverride: """
            {
              "hierarchy": { "state": "empty_by_design", "reason": "No ranks exist yet." },
              "resources": { "state": "empty_by_design", "reason": "Members contribute personally." },
              "relations": { "state": "empty_by_design", "reason": "No formal relations exist." },
              "projects": { "state": "empty_by_design", "reason": "No chartered projects exist." },
              "territoryAndInfluence": { "state": "empty_by_design", "reason": "No territory is claimed." },
              "playerMembership": { "state": "empty_by_design", "reason": "The player is not a member." }
            }
            """));

        var evidence = EmptyMortalEvidence("faction_watch");
        var issues = FactionMaterializationContract.Validate(
            document.RootElement,
            "faction_core.factions[0]",
            FactionMaterializationFamily.Mortal,
            evidence,
            requireEnvelope: true);

        var issue = Assert.Single(issues, item =>
            item.Code == "faction_materialization_section_missing");
        Assert.Equal("mortal_faction:faction_watch", issue.Actor);
        Assert.Contains("customStates", issue.FilePath, StringComparison.Ordinal);
    }

    [Fact]
    public void Validate_EmptyByDesignWithContent_ReportsDispositionMismatch()
    {
        using var document = JsonDocument.Parse(BuildMortalFactionJson());
        var evidence = EmptyMortalEvidence("faction_watch") with
        {
            SectionHasContent = new Dictionary<string, bool>(StringComparer.Ordinal)
            {
                ["hierarchy"] = true,
                ["resources"] = false,
                ["relations"] = false,
                ["projects"] = false,
                ["territoryAndInfluence"] = false,
                ["playerMembership"] = false,
                ["customStates"] = false
            }
        };

        var issues = FactionMaterializationContract.Validate(
            document.RootElement,
            "faction_core.factions[0]",
            FactionMaterializationFamily.Mortal,
            evidence,
            requireEnvelope: true);

        Assert.Contains(issues, item =>
            item.Code == "faction_materialization_disposition_mismatch" &&
            item.Actor == "mortal_faction:faction_watch");
    }

    [Theory]
    [InlineData("mortal")]
    [InlineData("shining")]
    public void Validate_CompleteFamilyEnvelope_ReturnsNoIssues(string familyName)
    {
        var family = familyName == "mortal"
            ? FactionMaterializationFamily.Mortal
            : FactionMaterializationFamily.Shining;
        using var document = JsonDocument.Parse(
            family == FactionMaterializationFamily.Mortal ? BuildMortalFactionJson() : BuildShiningFactionJson());

        Assert.Empty(Validate(document.RootElement, family));
    }

    [Theory]
    [InlineData("\"materialization\": {", "\"materialization\": { \"schemaVersion\": 1 }, \"materialization\": {")]
    [InlineData("\"runsProjects\": false", "\"runsProjects\": false, \"runsProjects\": false")]
    [InlineData("\"state\": \"empty_by_design\"", "\"state\": \"empty_by_design\", \"state\": \"empty_by_design\"")]
    public void Validate_DuplicateEnvelopeOrNestedMember_ReportsStableDuplicateIssue(string find, string replace)
    {
        using var document = JsonDocument.Parse(BuildMortalFactionJson().Replace(find, replace, StringComparison.Ordinal));

        Assert.Contains(Validate(document.RootElement, FactionMaterializationFamily.Mortal), issue =>
            issue.Code == "faction_materialization_duplicate_property");
    }

    [Theory]
    [InlineData("\"state\": \"complete\",", "\"state\": \"complete\", \"futureField\": true,")]
    [InlineData("\"hierarchy\": {", "\"unknownSection\": { \"state\": \"populated\" }, \"hierarchy\": {")]
    public void Validate_UnknownMember_ReportsInvalidEnvelope(string find, string replace)
    {
        using var document = JsonDocument.Parse(BuildMortalFactionJson().Replace(find, replace, StringComparison.Ordinal));

        Assert.Contains(Validate(document.RootElement, FactionMaterializationFamily.Mortal), issue =>
            issue.Code == "faction_materialization_invalid");
    }

    [Theory]
    [InlineData("\"schemaVersion\": 1", "\"schemaVersion\": 2")]
    [InlineData("\"materializationId\": \"fmat_watch\"", "\"materializationId\": \" \"")]
    [InlineData("\"materializedAtTurn\": 12", "\"materializedAtTurn\": -1")]
    [InlineData("\"state\": \"complete\"", "\"state\": \"partial\"")]
    [InlineData("\"runsProjects\": false", "\"runsProjects\": \"false\"")]
    public void Validate_InvalidScalar_ReportsInvalidEnvelope(string find, string replace)
    {
        using var document = JsonDocument.Parse(BuildMortalFactionJson().Replace(find, replace, StringComparison.Ordinal));

        Assert.Contains(Validate(document.RootElement, FactionMaterializationFamily.Mortal), issue =>
            issue.Code == "faction_materialization_invalid");
    }

    [Fact]
    public void Validate_PopulatedDispositionWithReason_ReportsInvalidEnvelope()
    {
        using var document = JsonDocument.Parse(BuildMortalFactionJson().Replace(
            "\"hierarchy\": { \"state\": \"empty_by_design\", \"reason\": \"No ranks exist yet.\" }",
            "\"hierarchy\": { \"state\": \"populated\", \"reason\": \"Cannot explain populated.\" }",
            StringComparison.Ordinal));

        Assert.Contains(Validate(document.RootElement, FactionMaterializationFamily.Mortal), issue =>
            issue.Code == "faction_materialization_invalid");
    }

    [Fact]
    public void Validate_WhitespaceEmptyReason_ReportsInvalidEnvelope()
    {
        using var document = JsonDocument.Parse(BuildMortalFactionJson().Replace(
            "\"reason\": \"No ranks exist yet.\"", "\"reason\": \"   \"", StringComparison.Ordinal));

        Assert.Contains(Validate(document.RootElement, FactionMaterializationFamily.Mortal), issue =>
            issue.Code == "faction_materialization_invalid");
    }

    [Fact]
    public void Validate_EmptyByDesignWithoutCanonicalEmptySurface_ReportsDispositionMismatch()
    {
        var evidence = EmptyMortalEvidence("faction_watch") with
        {
            SectionHasCanonicalEmptySurface = new Dictionary<string, bool>(MortalEvidence(true), StringComparer.Ordinal)
            {
                ["resources"] = false
            }
        };
        using var document = JsonDocument.Parse(BuildMortalFactionJson());

        Assert.Contains(Validate(document.RootElement, FactionMaterializationFamily.Mortal, evidence), issue =>
            issue.Code == "faction_materialization_disposition_mismatch" && issue.Section == "resources");
    }

    [Fact]
    public void Validate_CapabilityContradictsEvidence_ReportsCapabilityMismatch()
    {
        var evidence = EmptyMortalEvidence("faction_watch") with
        {
            CapabilityEvidence = new Dictionary<string, bool>(EmptyMortalEvidence("faction_watch").CapabilityEvidence, StringComparer.Ordinal)
            {
                ["runsProjects"] = true
            }
        };
        using var document = JsonDocument.Parse(BuildMortalFactionJson());

        Assert.Contains(Validate(document.RootElement, FactionMaterializationFamily.Mortal, evidence), issue =>
            issue.Code == "faction_materialization_capability_mismatch" && issue.Section == "runsProjects");
    }

    [Theory]
    [InlineData("\"factionType\": \"mortal_faction\"", "\"factionType\": \"shining_faction\"")]
    [InlineData("\"factionId\": \"faction_watch\"", "\"factionId\": \"faction_elsewhere\"")]
    public void Validate_EnvelopeIdentityDoesNotMatchFaction_ReportsIdentityMismatch(string find, string replace)
    {
        using var document = JsonDocument.Parse(BuildMortalFactionJson().Replace(find, replace, StringComparison.Ordinal));

        Assert.Contains(Validate(document.RootElement, FactionMaterializationFamily.Mortal), issue =>
            issue.Code == "faction_materialization_identity_mismatch");
    }

    [Fact]
    public void Validate_ShiningCanTradeComparesCapabilityAndTradeDispositionIndependently()
    {
        using var document = JsonDocument.Parse(BuildShiningFactionJson().Replace(
            "\"canTrade\": false", "\"canTrade\": true", StringComparison.Ordinal));
        var evidence = EmptyShiningEvidence("shining_observatory") with
        {
            CapabilityEvidence = new Dictionary<string, bool>(EmptyShiningEvidence("shining_observatory").CapabilityEvidence, StringComparer.Ordinal)
            {
                ["canTrade"] = false
            },
            SectionHasContent = new Dictionary<string, bool>(EmptyShiningEvidence("shining_observatory").SectionHasContent, StringComparer.Ordinal)
            {
                ["trade"] = false
            }
        };

        var issues = Validate(document.RootElement, FactionMaterializationFamily.Shining, evidence);

        Assert.Contains(issues, issue => issue.Code == "faction_materialization_capability_mismatch" && issue.Section == "canTrade");
        Assert.DoesNotContain(issues, issue => issue.Code == "faction_materialization_disposition_mismatch" && issue.Section == "trade");
    }

    [Fact]
    public void Validate_UnsupportedFactionType_ReportsInvalidEnvelope()
    {
        using var document = JsonDocument.Parse(BuildMortalFactionJson().Replace(
            "\"factionType\": \"mortal_faction\"", "\"factionType\": \"unsupported_faction\"", StringComparison.Ordinal));
        var evidence = EmptyMortalEvidence("faction_watch") with { FactionType = "unsupported_faction" };

        Assert.Contains(Validate(document.RootElement, FactionMaterializationFamily.Mortal, evidence), issue =>
            issue.Code == "faction_materialization_invalid");
    }

    [Fact]
    public void Validate_UnsupportedFamily_ReportsInvalidEnvelope()
    {
        using var document = JsonDocument.Parse(BuildShiningFactionJson());

        Assert.Contains(Validate(document.RootElement, (FactionMaterializationFamily)42), issue =>
            issue.Code == "faction_materialization_invalid");
    }

    [Fact]
    public void Validate_DirectCapabilityRequiresItsMappedSectionToBePopulated()
    {
        using var document = JsonDocument.Parse(BuildMortalFactionJson().Replace(
            "\"runsProjects\": false", "\"runsProjects\": true", StringComparison.Ordinal));
        var evidence = EmptyMortalEvidence("faction_watch") with
        {
            CapabilityEvidence = new Dictionary<string, bool>(EmptyMortalEvidence("faction_watch").CapabilityEvidence, StringComparer.Ordinal)
            {
                ["runsProjects"] = true
            }
        };

        Assert.Contains(Validate(document.RootElement, FactionMaterializationFamily.Mortal, evidence), issue =>
            issue.Code == "faction_materialization_capability_mismatch" && issue.Section == "projects");
    }

    [Theory]
    [InlineData("\"usesCustomMechanics\": false", "\"usesCustomMechanics\": false, \"unknownCapability\": false")]
    [InlineData("\"hierarchy\": { \"state\": \"empty_by_design\", \"reason\": \"No ranks exist yet.\" }", "\"hierarchy\": { \"state\": \"empty_by_design\", \"reason\": \"No ranks exist yet.\", \"unknownDisposition\": false }")]
    public void Validate_UnknownCapabilityOrDispositionMember_ReportsInvalidEnvelope(string find, string replace)
    {
        using var document = JsonDocument.Parse(BuildMortalFactionJson().Replace(find, replace, StringComparison.Ordinal));

        Assert.Contains(Validate(document.RootElement, FactionMaterializationFamily.Mortal), issue =>
            issue.Code == "faction_materialization_invalid");
    }

    [Fact]
    public void ValidateUniqueMaterializationIds_DuplicateIdsReturnIssue()
    {
        using var first = JsonDocument.Parse(BuildMortalFactionJson());
        using var second = JsonDocument.Parse(BuildShiningFactionJson().Replace(
            "\"materializationId\": \"fmat_observatory\"", "\"materializationId\": \"fmat_watch\"", StringComparison.Ordinal));

        var issues = FactionMaterializationContract.ValidateUniqueMaterializationIds(new[]
        {
            (first.RootElement, "mortal[0]", "mortal_faction", "faction_watch"),
            (second.RootElement, "shining[0]", "shining_faction", "shining_observatory")
        });

        Assert.Contains(issues, issue => issue.Code == "faction_materialization_duplicate_id" && issue.Actor == "shining_faction:shining_observatory");
    }

    private static IReadOnlyList<ValidationIssue> Validate(
        JsonElement faction,
        FactionMaterializationFamily family,
        FactionMaterializationEvidence? evidence = null) =>
        FactionMaterializationContract.Validate(
            faction,
            "faction_core.factions[0]",
            family,
            evidence ?? (family == FactionMaterializationFamily.Mortal
                ? EmptyMortalEvidence("faction_watch")
                : EmptyShiningEvidence("shining_observatory")),
            requireEnvelope: true);

    private static FactionMaterializationEvidence EmptyMortalEvidence(string factionId) =>
        new(
            "mortal_faction",
            factionId,
            MortalEvidence(false),
            new Dictionary<string, bool>(StringComparer.Ordinal)
            {
                ["hasFormalHierarchy"] = false,
                ["usesFactionResources"] = false,
                ["maintainsRelations"] = false,
                ["runsProjects"] = false,
                ["holdsTerritoryOrInfluence"] = false,
                ["supportsPlayerMembership"] = false,
                ["usesCustomMechanics"] = false
            },
            MortalEvidence(true));

    private static Dictionary<string, bool> MortalEvidence(bool value) =>
        new(StringComparer.Ordinal)
        {
            ["hierarchy"] = value,
            ["resources"] = value,
            ["relations"] = value,
            ["projects"] = value,
            ["territoryAndInfluence"] = value,
            ["playerMembership"] = value,
            ["customStates"] = value
        };

    private static FactionMaterializationEvidence EmptyShiningEvidence(string factionId) =>
        new(
            "shining_faction",
            factionId,
            ShiningEvidence(false),
            new Dictionary<string, bool>(StringComparer.Ordinal)
            {
                ["runsProjects"] = false,
                ["holdsTerritorialInfluence"] = false,
                ["usesResourceLedger"] = false,
                ["hasResidentAffiliations"] = false,
                ["canTrade"] = false,
                ["hasLeadershipHistory"] = false,
                ["usesStoryState"] = false
            },
            ShiningEvidence(true));

    private static Dictionary<string, bool> ShiningEvidence(bool value) =>
        new(StringComparer.Ordinal)
        {
            ["projects"] = value,
            ["territorialInfluence"] = value,
            ["resourceLedger"] = value,
            ["residentAffiliations"] = value,
            ["trade"] = value,
            ["leadershipHistory"] = value,
            ["storyState"] = value
        };

    private static string BuildMortalFactionJson(string? sectionsOverride = null) => $$"""
    {
      "factionId": "faction_watch",
      "materialization": {
        "schemaVersion": 1,
        "materializationId": "fmat_watch",
        "factionType": "mortal_faction",
        "factionId": "faction_watch",
        "materializedAtTurn": 12,
        "state": "complete",
        "capabilities": {
          "hasFormalHierarchy": false,
          "usesFactionResources": false,
          "maintainsRelations": false,
          "runsProjects": false,
          "holdsTerritoryOrInfluence": false,
          "supportsPlayerMembership": false,
          "usesCustomMechanics": false
        },
        "sections": {{sectionsOverride ?? """
        {
          "hierarchy": { "state": "empty_by_design", "reason": "No ranks exist yet." },
          "resources": { "state": "empty_by_design", "reason": "Members contribute personally." },
          "relations": { "state": "empty_by_design", "reason": "No formal relations exist." },
          "projects": { "state": "empty_by_design", "reason": "No chartered projects exist." },
          "territoryAndInfluence": { "state": "empty_by_design", "reason": "No territory is claimed." },
          "playerMembership": { "state": "empty_by_design", "reason": "The player is not a member." },
          "customStates": { "state": "empty_by_design", "reason": "No custom mechanic exists." }
        }
        """}}
      }
    }
    """;

    private static string BuildShiningFactionJson() => """
    {
      "factionId": "shining_observatory",
      "materialization": {
        "schemaVersion": 1,
        "materializationId": "fmat_observatory",
        "factionType": "shining_faction",
        "factionId": "shining_observatory",
        "materializedAtTurn": 12,
        "state": "complete",
        "capabilities": {
          "runsProjects": false,
          "holdsTerritorialInfluence": false,
          "usesResourceLedger": false,
          "hasResidentAffiliations": false,
          "canTrade": false,
          "hasLeadershipHistory": false,
          "usesStoryState": false
        },
        "sections": {
          "projects": { "state": "empty_by_design", "reason": "No project is chartered." },
          "territorialInfluence": { "state": "empty_by_design", "reason": "No influence is claimed." },
          "resourceLedger": { "state": "empty_by_design", "reason": "No faction ledger is used." },
          "residentAffiliations": { "state": "empty_by_design", "reason": "No resident affiliation exists." },
          "trade": { "state": "empty_by_design", "reason": "No current trade record exists." },
          "leadershipHistory": { "state": "empty_by_design", "reason": "No leadership history exists." },
          "storyState": { "state": "empty_by_design", "reason": "No story state exists." }
        }
      }
    }
    """;
}
