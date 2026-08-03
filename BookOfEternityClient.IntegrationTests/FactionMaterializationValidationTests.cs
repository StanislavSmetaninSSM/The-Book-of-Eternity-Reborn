using System.Text.Json.Nodes;
using BookOfEternityClient.Core;
using BookOfEternityClient.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed class FactionMaterializationValidationTests : IDisposable
{
    private const string MortalPath = "game_state/factions/faction_core.json";
    private const string ShiningPath = "game_state/meta/shining_abode_state.json";

    public static TheoryData<string, string> MissingMortalSemantics => new()
    {
        { "factionColor", "faction_materialization_mortal_color_missing" },
        { "purpose", "faction_materialization_mortal_purpose_missing" },
        { "currentAgenda", "faction_materialization_mortal_agenda_missing" },
        { "principles", "faction_materialization_mortal_principles_missing" },
        { "memory", "faction_materialization_mortal_memory_missing" },
        { "governance", "faction_materialization_mortal_governance_missing" },
        { "leadership", "faction_materialization_mortal_leadership_missing" },
        { "scribeChronicle", "faction_materialization_mortal_chronicle_missing" }
    };

    private readonly string _rootPath;
    private readonly FileSystemManager _fs;
    private readonly ValidationService _validator;

    public FactionMaterializationValidationTests()
    {
        _rootPath = Path.Combine(
            Path.GetTempPath(),
            "boe-faction-materialization-validation-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_rootPath);

        _fs = new FileSystemManager(
            _rootPath,
            NullLogger<FileSystemManager>.Instance);
        _fs.EnsureDirectoryStructure();
        _validator = new ValidationService(
            _fs,
            NullLogger<ValidationService>.Instance);
    }

    [Theory]
    [InlineData(false, false, true, false, (int)FactionTouchKind.New)]
    [InlineData(true, false, true, false, (int)FactionTouchKind.LegacyPromotion)]
    [InlineData(true, true, true, false, (int)FactionTouchKind.AlreadyMaterialized)]
    [InlineData(true, true, false, false, (int)FactionTouchKind.AlreadyMaterialized)]
    [InlineData(true, false, false, false, (int)FactionTouchKind.UntouchedLegacy)]
    [InlineData(true, true, false, true, (int)FactionTouchKind.ClientDerivedOnly)]
    public void Classify_ReturnsExpectedTouchKind(
        bool existedPreTurn,
        bool hadReceiptPreTurn,
        bool gmAuthoredTouch,
        bool derivedOnly,
        int expected)
    {
        Assert.Equal(
            (FactionTouchKind)expected,
            FactionTouchClassifier.Classify(
                existedPreTurn,
                hadReceiptPreTurn,
                gmAuthoredTouch,
                derivedOnly));
    }

    [Fact]
    public async Task Validate_ChangedHistoricalEnvelope_ReportsImmutableFailure()
    {
        await WriteValidatedMortalFactionAsync(
            preTurnMaterializationId: "fmat_original",
            currentMaterializationId: "fmat_changed");

        var issues = await _validator.ValidateAcceptedTurnRawFactionMaterializationAsync();

        Assert.Contains(issues, issue =>
            issue.Code == "faction_materialization_immutable_receipt_changed" &&
            issue.Actor == "mortal_faction:faction_watch");
    }

    [Fact]
    public async Task Validate_DuplicateMaterializationIdAcrossFamilies_ReportsDuplicate()
    {
        await WriteMortalAndShiningCreationsAsync(
            mortalMaterializationId: "fmat_shared",
            shiningMaterializationId: "fmat_shared");

        var issues = await _validator.ValidateAcceptedTurnRawFactionMaterializationAsync();

        Assert.Contains(issues, issue =>
            issue.Code == "faction_materialization_duplicate_id" &&
            issue.Actor == "shining_faction:order_dawn");
    }

    [Fact]
    public async Task Validate_DuplicatePreTurnFactionIdentity_FailsClosed()
    {
        var preTurn = MortalRoot(
            LegacyMortalFaction("faction_watch"),
            LegacyMortalFaction("faction_watch"));
        var current = MortalRoot(
            MaterializedMortalFaction("faction_watch", "fmat_watch"));
        await WriteCurrentAndSnapshotAsync(
            (MortalPath, current.ToJsonString()),
            (MortalPath, preTurn.ToJsonString()));

        var issues = await _validator.ValidateAcceptedTurnRawFactionMaterializationAsync();

        Assert.Contains(issues, issue =>
            issue.Code == "faction_materialization_pre_turn_authority_unusable" &&
            issue.FilePath == MortalPath);
    }

    [Fact]
    public async Task Validate_DuplicateIdentityAcrossCanonicalAndFullCarriers_ReportsDuplicate()
    {
        var current = MortalRoot(
            MaterializedMortalFaction("faction_watch", "fmat_canonical"));
        current["factionDataChanges"] = new JsonArray(
            MaterializedMortalFaction("faction_watch", "fmat_full"));
        await WriteCurrentAndSnapshotAsync(
            (MortalPath, current.ToJsonString()),
            (MortalPath, MortalRoot().ToJsonString()));

        var issues = await _validator.ValidateAcceptedTurnRawFactionMaterializationAsync();

        Assert.Contains(issues, issue =>
            issue.Code == "faction_materialization_duplicate_effective_identity" &&
            issue.Actor == "mortal_faction:faction_watch");
    }

    [Fact]
    public async Task Validate_CaseVariantFactionIds_AreDistinctExactIdentities()
    {
        var preTurn = MortalRoot(
            MaterializedMortalFaction(
                "Faction_Watch",
                "fmat_historical_case_variant"));
        var current = MortalRoot(
            MaterializedMortalFaction(
                "faction_watch",
                "fmat_current_exact_identity"));
        await WriteCurrentAndSnapshotAsync(
            (MortalPath, current.ToJsonString()),
            (MortalPath, preTurn.ToJsonString()));

        var issues = await _validator.ValidateAcceptedTurnRawFactionMaterializationAsync();

        Assert.DoesNotContain(issues, issue =>
            issue.Actor == "mortal_faction:faction_watch" &&
            issue.Code is
                "faction_materialization_missing" or
                "faction_materialization_immutable_receipt_changed" or
                "faction_materialization_pre_turn_authority_unusable");
    }

    [Theory]
    [InlineData(false, null)]
    [InlineData(false, "")]
    [InlineData(false, "{")]
    [InlineData(false, "[]")]
    [InlineData(true, null)]
    [InlineData(true, "")]
    [InlineData(true, "{")]
    [InlineData(true, "[]")]
    public async Task Validate_UnusableCurrentAuthorityWithHistoricalFaction_FailsClosed(
        bool shining,
        string? currentJson)
    {
        var path = shining ? ShiningPath : MortalPath;
        var preTurn = shining
            ? ShiningRoot(
                MaterializedShiningFaction("order_dawn", "fmat_historical"))
            : MortalRoot(
                MaterializedMortalFaction("faction_watch", "fmat_historical"));
        if (currentJson != null)
            await _fs.WriteFileAtomicAsync(path, currentJson);
        await WriteValidatedSnapshotManifestAsync((path, preTurn.ToJsonString()));

        var issues = await _validator.ValidateAcceptedTurnRawFactionMaterializationAsync();

        Assert.Contains(issues, issue =>
            issue.Code == "faction_materialization_current_authority_unusable" &&
            issue.FilePath == path);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Validate_AbsentCurrentAuthorityWithoutHistoricalFactions_RemainsOptional(
        bool shining)
    {
        var path = shining ? ShiningPath : MortalPath;
        var preTurn = shining ? ShiningRoot() : MortalRoot();
        await WriteValidatedSnapshotManifestAsync((path, preTurn.ToJsonString()));

        var issues = await _validator.ValidateAcceptedTurnRawFactionMaterializationAsync();

        Assert.DoesNotContain(issues, issue =>
            issue.Code == "faction_materialization_current_authority_unusable" &&
            issue.FilePath == path);
    }

    [Fact]
    public async Task Validate_AbsentCurrentAuthorityWithDuplicatePreTurnFactionMembers_FailsClosed()
    {
        var historicalFaction =
            MaterializedMortalFaction("faction_watch", "fmat_historical");
        var preTurnJson = $$"""
        {
          "factions": [{{historicalFaction.ToJsonString()}}],
          "factions": []
        }
        """;
        await WriteValidatedSnapshotManifestAsync((MortalPath, preTurnJson));

        var issues = await _validator.ValidateAcceptedTurnRawFactionMaterializationAsync();

        Assert.Contains(issues, issue =>
            issue.Code == "faction_materialization_pre_turn_authority_unusable" &&
            issue.FilePath.StartsWith(MortalPath, StringComparison.Ordinal));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Validate_RawCanonicalLegacyWithoutSnapshot_FailsClosed(
        bool shining)
    {
        var path = shining ? ShiningPath : MortalPath;
        var current = shining
            ? ShiningRoot(LegacyShiningFaction("order_dawn", factionStrength: 30))
            : MortalRoot(LegacyMortalFaction("faction_watch"));
        await _fs.WriteFileAtomicAsync(path, current.ToJsonString());

        var issues = await _validator.ValidateAcceptedTurnRawFactionMaterializationAsync();

        Assert.Contains(issues, issue =>
            issue.Code == "faction_materialization_pre_turn_authority_unusable" &&
            issue.FilePath == path);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Validate_PostCanonicalLegacyWithoutSnapshot_RemainsCompatible(
        bool shining)
    {
        var path = shining ? ShiningPath : MortalPath;
        var current = shining
            ? ShiningRoot(LegacyShiningFaction("order_dawn", factionStrength: 30))
            : MortalRoot(LegacyMortalFaction("faction_watch"));
        await _fs.WriteFileAtomicAsync(path, current.ToJsonString());

        var issues = await _validator.ValidateGameStateAsync(
            GameStateValidationPhase.AcceptedTurnFactionMaterializationCompleteness);

        Assert.DoesNotContain(issues, issue =>
            issue.Code is
                "faction_materialization_pre_turn_authority_unusable" or
                "faction_materialization_missing");
    }

    [Fact]
    public async Task Validate_MortalMutationWithoutUsableSnapshot_FailsClosed()
    {
        var current = new JsonObject
        {
            ["factionDataChanges"] = new JsonArray(
                MaterializedMortalFaction("faction_watch", "fmat_watch"))
        };
        await _fs.WriteFileAtomicAsync(MortalPath, current.ToJsonString());

        var issues = await _validator.ValidateAcceptedTurnRawFactionMaterializationAsync();

        Assert.Contains(issues, issue =>
            issue.Code == "faction_materialization_pre_turn_authority_unusable" &&
            issue.FilePath == MortalPath);
    }

    [Fact]
    public async Task Validate_UntouchedLegacyFaction_DoesNotRequireReceipt()
    {
        var preTurn = MortalRoot(LegacyMortalFaction("faction_watch"));
        var current = MortalRoot(LegacyMortalFaction("faction_watch"));
        await WriteCurrentAndSnapshotAsync(
            (MortalPath, current.ToJsonString()),
            (MortalPath, preTurn.ToJsonString()));

        var issues = await _validator.ValidateAcceptedTurnRawFactionMaterializationAsync();

        Assert.DoesNotContain(issues, issue =>
            issue.Actor == "mortal_faction:faction_watch" &&
            issue.Code == "faction_materialization_missing");
    }

    [Fact]
    public async Task Validate_ShiningDerivedStrengthOnly_DoesNotPromoteLegacyFaction()
    {
        var preTurnFaction = LegacyShiningFaction("order_dawn", factionStrength: 30);
        var currentFaction = LegacyShiningFaction("order_dawn", factionStrength: 31);
        await WriteCurrentAndSnapshotAsync(
            (ShiningPath, ShiningRoot(currentFaction).ToJsonString()),
            (ShiningPath, ShiningRoot(preTurnFaction).ToJsonString()));

        var issues = await _validator.ValidateAcceptedTurnRawFactionMaterializationAsync();

        Assert.DoesNotContain(issues, issue =>
            issue.Actor == "shining_faction:order_dawn" &&
            issue.Code == "faction_materialization_missing");
    }

    [Theory]
    [MemberData(nameof(MissingMortalSemantics))]
    public async Task NewMortalFaction_MissingSemanticField_FailsRaw(
        string propertyName,
        string expectedCode)
    {
        var faction = BuildCompleteMortalCreation();
        faction.Remove(propertyName);
        await WriteMortalCreationAsync(faction);

        var issues = await _validator.ValidateAcceptedTurnRawFactionMaterializationAsync();

        Assert.Contains(issues, issue =>
            issue.Code == expectedCode &&
            issue.Actor == "mortal_faction:temp-faction-watch");
    }

    [Fact]
    public async Task NewMortalFaction_AllSevenExactEmptySurfaces_Passes()
    {
        await WriteMortalCreationAsync(BuildCompleteMinimalMortalCreation());

        var issues = await _validator.ValidateAcceptedTurnRawFactionMaterializationAsync();

        Assert.DoesNotContain(issues, issue =>
            issue.Severity == IssueSeverity.Error &&
            issue.Actor == "mortal_faction:temp-faction-watch");
    }

    [Fact]
    public async Task NewMortalFaction_AllSevenPopulatedSurfaces_PassesRaw()
    {
        await WriteMortalCreationAsync(BuildCompleteMortalCreation());
        await WritePopulatedMortalLocationAuthorityAsync();

        var issues = await _validator.ValidateAcceptedTurnRawFactionMaterializationAsync();

        Assert.DoesNotContain(issues, issue =>
            issue.Severity == IssueSeverity.Error &&
            issue.Actor == "mortal_faction:temp-faction-watch");
    }

    [Fact]
    public async Task NewMortalFaction_InvalidColor_FailsRaw()
    {
        var faction = BuildCompleteMortalCreation();
        faction["factionColor"] = "watch-blue";
        await WriteMortalCreationAsync(faction);

        var issues = await _validator.ValidateAcceptedTurnRawFactionMaterializationAsync();

        Assert.Contains(issues, issue =>
            issue.Code == "faction_materialization_mortal_color_invalid" &&
            issue.Actor == "mortal_faction:temp-faction-watch");
    }

    [Theory]
    [InlineData("structure", "faction_materialization_mortal_structure_missing")]
    [InlineData("resources", "faction_materialization_mortal_resources_missing")]
    [InlineData("custom", "faction_materialization_mortal_custom_missing")]
    public async Task NewMortalFaction_MissingCanonicalSidecarTarget_Fails(
        string sidecar,
        string expectedCode)
    {
        await WriteCanonicalMinimalMortalCreationAsync(missingSidecar: sidecar);

        var issues = await _validator.ValidateGameStateAsync(
            GameStateValidationPhase.AcceptedTurnFactionMaterializationCompleteness);

        Assert.Contains(issues, issue =>
            issue.Code == expectedCode &&
            issue.Actor == "mortal_faction:temp-faction-watch");
    }

    [Fact]
    public async Task NewMortalFaction_CompleteCanonicalMinimalBundle_Passes()
    {
        await WriteCanonicalMinimalMortalCreationAsync();

        var issues = await _validator.ValidateGameStateAsync(
            GameStateValidationPhase.AcceptedTurnFactionMaterializationCompleteness);

        Assert.DoesNotContain(issues, issue =>
            issue.Severity == IssueSeverity.Error &&
            issue.Actor == "mortal_faction:temp-faction-watch");
    }

    [Fact]
    public async Task NewMortalFaction_CanonicalUnknownStructureLeader_Fails()
    {
        await WriteCanonicalMinimalMortalCreationAsync();
        await _fs.WriteFileAtomicAsync(
            "game_state/factions/faction_structure.json",
            new JsonObject
            {
                ["entries"] = new JsonArray(new JsonObject
                {
                    ["factionId"] = "temp-faction-watch",
                    ["factionName"] = "Wayfarer Watch",
                    ["governance"] = new JsonObject
                    {
                        ["model"] = "Open moot",
                        ["decisionProcess"] = "Active wardens decide by simple majority."
                    },
                    ["leadership"] = new JsonObject
                    {
                        ["leadershipState"] = "headed",
                        ["summary"] = "A named captain commands the watch.",
                        ["leaderNpcIds"] = new JsonArray("npc_missing")
                    },
                    ["ranks"] = new JsonObject
                    {
                        ["branches"] = new JsonArray()
                    },
                    ["structuredBonuses"] = new JsonArray()
                })
            }.ToJsonString());

        var issues = await _validator.ValidateGameStateAsync(
            GameStateValidationPhase.AcceptedTurnFactionMaterializationCompleteness);

        Assert.Contains(issues, issue =>
            issue.Code == "faction_materialization_mortal_leader_unknown_npc" &&
            issue.Actor == "mortal_faction:temp-faction-watch");
    }

    [Fact]
    public async Task NewMortalFaction_CanonicalOrphanChronicleEntry_Fails()
    {
        await WriteCanonicalMinimalMortalCreationAsync();
        await _fs.WriteFileAtomicAsync(
            "game_state/factions/faction_chronicles.json",
            new JsonObject
            {
                ["entries"] = new JsonArray(
                    new JsonObject
                    {
                        ["factionId"] = "temp-faction-watch",
                        ["entry"] = "#12 - The Wayfarer Watch took responsibility for the western road."
                    },
                    new JsonObject
                    {
                        ["factionId"] = "faction_missing",
                        ["entry"] = "#12 - An unknown faction claimed the eastern road."
                    })
            }.ToJsonString());

        var issues = await _validator.ValidateGameStateAsync(
            GameStateValidationPhase.AcceptedTurnFactionMaterializationCompleteness);

        Assert.Contains(issues, issue =>
            issue.Code == "faction_materialization_mortal_orphaned_chronicle" &&
            issue.Actor == "mortal_faction:faction_missing");
    }

    [Fact]
    public async Task NewMortalFaction_ProjectRowContradictsEmptyDisposition_FailsRaw()
    {
        var faction = BuildCompleteMinimalMortalCreation();
        faction["activeProjects"] = new JsonArray(new JsonObject
        {
            ["projectId"] = "project_watchtower",
            ["name"] = "Raise the Watchtower"
        });
        await WriteMortalCreationAsync(faction);

        var issues = await _validator.ValidateAcceptedTurnRawFactionMaterializationAsync();

        Assert.Contains(issues, issue =>
            issue.Code == "faction_materialization_disposition_mismatch" &&
            issue.Actor == "mortal_faction:temp-faction-watch" &&
            issue.Section == "projects");
    }

    [Theory]
    [InlineData("relation", "faction_materialization_mortal_relation_unknown_target")]
    [InlineData("location", "faction_materialization_mortal_territory_unknown_location")]
    [InlineData("leader", "faction_materialization_mortal_leader_unknown_npc")]
    public async Task NewMortalFaction_UnknownCrossReference_FailsRaw(
        string referenceKind,
        string expectedCode)
    {
        var faction = BuildCompleteMortalCreation();
        switch (referenceKind)
        {
            case "relation":
                faction["relations"] = new JsonArray(new JsonObject
                {
                    ["targetFactionId"] = "faction_missing",
                    ["status"] = "Neutral",
                    ["description"] = "No trusted envoy has confirmed this faction."
                });
                break;
            case "location":
                faction["controlledTerritories"] = new JsonArray(new JsonObject
                {
                    ["locationId"] = "location_missing",
                    ["locationName"] = "Missing Hold"
                });
                break;
            case "leader":
                faction["leadership"] = new JsonObject
                {
                    ["leadershipState"] = "headed",
                    ["summary"] = "A named captain commands the watch.",
                    ["leaderNpcIds"] = new JsonArray("npc_missing")
                };
                break;
        }

        await WriteMortalCreationAsync(faction);

        var issues = await _validator.ValidateAcceptedTurnRawFactionMaterializationAsync();

        Assert.Contains(issues, issue =>
            issue.Code == expectedCode &&
            issue.Actor == "mortal_faction:temp-faction-watch");
    }

    [Fact]
    public async Task NewMortalFaction_InitialIdCollidesWithPreTurnFaction_FailsRaw()
    {
        var faction = BuildCompleteMortalCreation();
        faction["initialId"] = "faction_watch";
        faction["materialization"] = BuildMortalEnvelope(
            "faction_watch",
            "fmat_watch_creation");
        var current = new JsonObject
        {
            ["factionDataChanges"] = new JsonArray(faction)
        };
        await WriteCurrentAndSnapshotAsync(
            (MortalPath, current.ToJsonString()),
            (MortalPath, MortalRoot(LegacyMortalFaction("faction_watch")).ToJsonString()));

        var issues = await _validator.ValidateAcceptedTurnRawFactionMaterializationAsync();

        Assert.Contains(issues, issue =>
            issue.Code == "faction_materialization_mortal_initial_id_collision" &&
            issue.Actor == "mortal_faction:faction_watch");
    }

    [Fact]
    public async Task NewMortalFaction_SameTurnNpcInitialIdSatisfiesHeadedLeadership()
    {
        var faction = BuildCompleteMinimalMortalCreation();
        faction["leadership"] = new JsonObject
        {
            ["leadershipState"] = "headed",
            ["summary"] = "Captain Mira commands the watch.",
            ["leaderNpcIds"] = new JsonArray("temp-npc-watch-captain")
        };
        await WriteMortalCreationAsync(faction);
        await _fs.WriteFileAtomicAsync(
            "game_state/npcs/npc_core.json",
            new JsonObject
            {
                ["UpdateNPCs"] = new JsonArray(new JsonObject
                {
                    ["NPCId"] = null,
                    ["initialId"] = "temp-npc-watch-captain",
                    ["name"] = "Captain Mira"
                })
            }.ToJsonString());

        var issues = await _validator.ValidateAcceptedTurnRawFactionMaterializationAsync();

        Assert.DoesNotContain(issues, issue =>
            issue.Severity == IssueSeverity.Error &&
            issue.Actor == "mortal_faction:temp-faction-watch");
    }

    [Fact]
    public async Task NewMortalFaction_VacantLeadershipWithLeaderIds_FailsRaw()
    {
        var faction = BuildCompleteMinimalMortalCreation();
        faction["leadership"]!["leaderNpcIds"] = new JsonArray("npc_watch_captain");
        await WriteMortalCreationAsync(faction);

        var issues = await _validator.ValidateAcceptedTurnRawFactionMaterializationAsync();

        Assert.Contains(issues, issue =>
            issue.Code == "faction_materialization_mortal_leadership_invalid" &&
            issue.Actor == "mortal_faction:temp-faction-watch");
    }

    [Fact]
    public async Task NewMortalFaction_OmitsExactPlayerNonMemberValue_FailsRaw()
    {
        var faction = BuildCompleteMinimalMortalCreation();
        faction.Remove("reputationDescription");
        await WriteMortalCreationAsync(faction);

        var issues = await _validator.ValidateAcceptedTurnRawFactionMaterializationAsync();

        Assert.Contains(issues, issue =>
            issue.Code == "faction_materialization_mortal_player_membership_incomplete" &&
            issue.Actor == "mortal_faction:temp-faction-watch");
    }

    [Fact]
    public async Task NewMortalFaction_NpcAffiliationTargetsUnknownFaction_FailsRaw()
    {
        await WriteMortalCreationAsync(BuildCompleteMinimalMortalCreation());
        await _fs.WriteFileAtomicAsync(
            "game_state/npcs/npc_core.json",
            new JsonObject
            {
                ["UpdateNPCs"] = new JsonArray(new JsonObject
                {
                    ["NPCId"] = "npc_watch_captain",
                    ["name"] = "Captain Mira",
                    ["factionAffiliations"] = new JsonArray(new JsonObject
                    {
                        ["factionId"] = "faction_missing",
                        ["factionName"] = "Missing Faction",
                        ["rank"] = "Captain",
                        ["branch"] = null,
                        ["membershipStatus"] = "Active"
                    })
                })
            }.ToJsonString());

        var issues = await _validator.ValidateAcceptedTurnRawFactionMaterializationAsync();

        Assert.Contains(issues, issue =>
            issue.Code == "faction_materialization_mortal_npc_affiliation_unknown_faction");
    }

    public void Dispose()
    {
        if (Directory.Exists(_rootPath))
            Directory.Delete(_rootPath, recursive: true);
    }

    private async Task WriteValidatedMortalFactionAsync(
        string preTurnMaterializationId,
        string currentMaterializationId)
    {
        var preTurn = MortalRoot(
            MaterializedMortalFaction("faction_watch", preTurnMaterializationId));
        var current = MortalRoot(
            MaterializedMortalFaction("faction_watch", currentMaterializationId));
        await WriteCurrentAndSnapshotAsync(
            (MortalPath, current.ToJsonString()),
            (MortalPath, preTurn.ToJsonString()));
    }

    private async Task WriteMortalCreationAsync(JsonObject faction)
    {
        var current = new JsonObject
        {
            ["factionDataChanges"] = new JsonArray(faction)
        };
        await WriteCurrentAndSnapshotAsync(
            (MortalPath, current.ToJsonString()),
            (MortalPath, MortalRoot().ToJsonString()));
    }

    private async Task WriteCanonicalMinimalMortalCreationAsync(string? missingSidecar = null)
    {
        var faction = BuildCompleteMinimalMortalCreation();
        faction["factionId"] = "temp-faction-watch";
        faction.Remove("initialId");
        faction.Remove("isNewFaction");
        faction.Remove("governance");
        faction.Remove("leadership");
        faction.Remove("ranks");
        faction.Remove("structuredBonuses");
        faction.Remove("resources");
        faction.Remove("activeProjects");
        faction.Remove("completedProjects");
        faction.Remove("customStates");
        faction.Remove("scribeChronicle");

        await WriteCurrentAndSnapshotAsync(
            (MortalPath, MortalRoot(faction).ToJsonString()),
            (MortalPath, MortalRoot().ToJsonString()));

        if (!string.Equals(missingSidecar, "structure", StringComparison.Ordinal))
        {
            await _fs.WriteFileAtomicAsync(
                "game_state/factions/faction_structure.json",
                new JsonObject
                {
                    ["entries"] = new JsonArray(new JsonObject
                    {
                        ["factionId"] = "temp-faction-watch",
                        ["factionName"] = "Wayfarer Watch",
                        ["governance"] = new JsonObject
                        {
                            ["model"] = "Open moot",
                            ["decisionProcess"] = "Active wardens decide by simple majority."
                        },
                        ["leadership"] = new JsonObject
                        {
                            ["leadershipState"] = "vacant",
                            ["summary"] = "No successor has been chosen.",
                            ["leaderNpcIds"] = new JsonArray()
                        },
                        ["ranks"] = new JsonObject
                        {
                            ["branches"] = new JsonArray()
                        },
                        ["structuredBonuses"] = new JsonArray()
                    })
                }.ToJsonString());
        }

        if (!string.Equals(missingSidecar, "resources", StringComparison.Ordinal))
        {
            await _fs.WriteFileAtomicAsync(
                "game_state/factions/faction_resources.json",
                new JsonObject
                {
                    ["entries"] = new JsonArray(new JsonObject
                    {
                        ["factionId"] = "temp-faction-watch",
                        ["factionName"] = "Wayfarer Watch",
                        ["metaResources"] = new JsonArray(),
                        ["strategicGoods"] = new JsonArray()
                    })
                }.ToJsonString());
        }

        await _fs.WriteFileAtomicAsync(
            "game_state/factions/faction_projects.json",
            new JsonObject
            {
                ["activeProjects"] = new JsonArray(),
                ["completedProjects"] = new JsonArray()
            }.ToJsonString());

        if (!string.Equals(missingSidecar, "custom", StringComparison.Ordinal))
        {
            await _fs.WriteFileAtomicAsync(
                "game_state/factions/faction_custom.json",
                new JsonObject
                {
                    ["entries"] = new JsonArray(new JsonObject
                    {
                        ["factionId"] = "temp-faction-watch",
                        ["factionName"] = "Wayfarer Watch",
                        ["customStates"] = new JsonArray()
                    })
                }.ToJsonString());
        }

        await _fs.WriteFileAtomicAsync(
            "game_state/factions/faction_chronicles.json",
            new JsonObject
            {
                ["entries"] = new JsonArray(new JsonObject
                {
                    ["factionId"] = "temp-faction-watch",
                    ["entry"] = "#12 - The Wayfarer Watch took responsibility for the western road."
                })
            }.ToJsonString());
    }

    private async Task WritePopulatedMortalLocationAuthorityAsync()
    {
        await _fs.WriteFileAtomicAsync(
            "game_state/world/current_location.json",
            new JsonObject
            {
                ["locationId"] = "location_watch_road",
                ["locationName"] = "Western Road",
                ["factionControl"] = new JsonArray(new JsonObject
                {
                    ["factionId"] = "temp-faction-watch"
                })
            }.ToJsonString());
    }

    private async Task WriteMortalAndShiningCreationsAsync(
        string mortalMaterializationId,
        string shiningMaterializationId)
    {
        var currentMortal = MortalRoot(
            MaterializedMortalFaction("faction_watch", mortalMaterializationId));
        var currentShining = ShiningRoot(
            MaterializedShiningFaction("order_dawn", shiningMaterializationId));
        var preTurnMortal = MortalRoot();
        var preTurnShining = ShiningRoot();

        await WriteCurrentAndSnapshotAsync(
            (MortalPath, currentMortal.ToJsonString()),
            (ShiningPath, currentShining.ToJsonString()),
            (MortalPath, preTurnMortal.ToJsonString()),
            (ShiningPath, preTurnShining.ToJsonString()));
    }

    private async Task WriteCurrentAndSnapshotAsync(
        params (string Path, string Json)[] files)
    {
        var currentByPath = new Dictionary<string, string>(StringComparer.Ordinal);
        var snapshotByPath = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var file in files)
        {
            if (!currentByPath.TryAdd(file.Path, file.Json))
                snapshotByPath[file.Path] = file.Json;
        }

        foreach (var (path, json) in currentByPath)
            await _fs.WriteFileAtomicAsync(path, json);

        await WriteValidatedSnapshotManifestAsync(
            snapshotByPath.Select(entry => (entry.Key, entry.Value)).ToArray());
    }

    private async Task WriteValidatedSnapshotManifestAsync(
        params (string Path, string Json)[] snapshotFiles)
    {
        const string sessionId = "session_faction_materialization_validation";
        const string requestId = "request_faction_materialization_validation";
        const int turnNumber = 12;
        const string playerAction = "Validate faction materialization continuity.";

        await _fs.WriteFileAtomicAsync("input/turn_request.json", $$"""
        {
          "sessionId": "{{sessionId}}",
          "requestId": "{{requestId}}",
          "turnNumber": {{turnNumber}},
          "playerAction": "{{playerAction}}"
        }
        """);

        var files = new JsonObject();
        var snapshotFileHashes = new JsonObject();
        var rollbackBaselineFiles = new JsonArray();
        foreach (var (path, json) in snapshotFiles)
        {
            var snapshotPath = $"game_state/control/pending_turn_snapshot/{path}";
            await _fs.WriteFileAtomicAsync(snapshotPath, json);
            files[path] = snapshotPath;
            snapshotFileHashes[path] = PendingTurnSnapshotAuthority.ComputeSha256(json);
            rollbackBaselineFiles.Add(path);
        }

        var manifest = new JsonObject
        {
            ["sessionId"] = sessionId,
            ["requestId"] = requestId,
            ["turnNumber"] = turnNumber,
            ["requestTimestamp"] = "2026-08-03T00:00:00Z",
            ["playerAction"] = playerAction,
            ["files"] = files,
            ["snapshotFileHashes"] = snapshotFileHashes,
            ["clientOwnedValidationHashes"] = new JsonObject(),
            ["rollbackBackups"] = new JsonObject(),
            ["rollbackBaselineFiles"] = rollbackBaselineFiles,
            ["sourceLabel"] = "accepted faction turn",
            ["manifestPayloadHash"] = string.Empty
        };
        manifest["manifestPayloadHash"] =
            PendingTurnSnapshotTestAuthority.ComputeManifestPayloadHash(manifest);

        await _fs.WriteFileAtomicAsync(
            "game_state/control/pending_turn_snapshot.json",
            manifest.ToJsonString());
        await PendingTurnSnapshotTestAuthority.SyncAuthorityForCurrentManifestAsync(_fs);
    }

    private static JsonObject MortalRoot(params JsonObject[] factions) =>
        new()
        {
            ["factions"] = new JsonArray(
                factions.Select(faction => (JsonNode?)faction).ToArray())
        };

    private static JsonObject ShiningRoot(params JsonObject[] factions) =>
        new()
        {
            ["factions"] = new JsonArray(
                factions.Select(faction => (JsonNode?)faction).ToArray())
        };

    private static JsonObject LegacyMortalFaction(string factionId) =>
        new()
        {
            ["factionId"] = factionId,
            ["name"] = factionId
        };

    private static JsonObject LegacyShiningFaction(
        string factionId,
        int factionStrength) =>
        new()
        {
            ["factionId"] = factionId,
            ["baseStrength"] = 30,
            ["factionStrength"] = factionStrength
        };

    private static JsonObject MaterializedMortalFaction(
        string factionId,
        string materializationId)
    {
        var faction = LegacyMortalFaction(factionId);
        faction["materialization"] = BuildMortalEnvelope(factionId, materializationId);
        return faction;
    }

    private static JsonObject MaterializedShiningFaction(
        string factionId,
        string materializationId)
    {
        var faction = LegacyShiningFaction(factionId, factionStrength: 30);
        faction["materialization"] = BuildShiningEnvelope(factionId, materializationId);
        return faction;
    }

    private static JsonObject BuildCompleteMortalCreation()
    {
        var faction = BuildCompleteMinimalMortalCreation();
        faction["leadership"] = new JsonObject
        {
            ["leadershipState"] = "collective",
            ["summary"] = "The road wardens govern as a collective.",
            ["leaderNpcIds"] = new JsonArray()
        };
        faction["ranks"] = new JsonObject
        {
            ["branches"] = new JsonArray(new JsonObject
            {
                ["branchId"] = "road_wardens",
                ["name"] = "Road Wardens",
                ["ranks"] = new JsonArray(new JsonObject
                {
                    ["rankId"] = "warden",
                    ["name"] = "Warden"
                })
            })
        };
        faction["structuredBonuses"] = new JsonArray(new JsonObject
        {
            ["bonusId"] = "safe_passage",
            ["description"] = "Wardens provide safer passage on watched roads."
        });
        faction["resources"] = new JsonObject
        {
            ["metaResources"] = new JsonArray(new JsonObject
            {
                ["resourceId"] = "warden_trust",
                ["name"] = "Warden Trust",
                ["amount"] = 8
            }),
            ["strategicGoods"] = new JsonArray(new JsonObject
            {
                ["goodId"] = "bridge_timbers",
                ["name"] = "Bridge Timbers",
                ["amount"] = 12
            })
        };
        faction["relations"] = new JsonArray(new JsonObject
        {
            ["targetFactionId"] = "temp-faction-watch",
            ["status"] = "Allied",
            ["description"] = "The wardens share authority across their patrols."
        });
        faction["activeProjects"] = new JsonArray(new JsonObject
        {
            ["projectId"] = "project_watchtower",
            ["name"] = "Raise the Watchtower"
        });
        faction["controlledTerritories"] = new JsonArray(new JsonObject
        {
            ["locationId"] = "location_watch_road",
            ["locationName"] = "Western Road"
        });
        faction["customStates"] = new JsonArray(new JsonObject
        {
            ["stateId"] = "bridge_repair_priority",
            ["value"] = "urgent"
        });
        faction["isPlayerMember"] = true;
        faction["playerRank"] = "Warden";
        faction["playerBranch"] = "Road Wardens";
        faction["playerStrategyDirective"] = "Keep the western road open.";
        faction["reputation"] = 10;
        faction["reputationDescription"] = "Trusted road warden";
        faction["materialization"] = BuildPopulatedMortalEnvelope(
            "temp-faction-watch",
            "fmat_watch_creation");
        return faction;
    }

    private static JsonObject BuildCompleteMinimalMortalCreation() =>
        new()
        {
            ["factionId"] = null,
            ["initialId"] = "temp-faction-watch",
            ["isNewFaction"] = true,
            ["name"] = "Wayfarer Watch",
            ["description"] = "A small watch formed to keep one road safe.",
            ["image_prompt"] = "weathered road wardens beneath a wooden watchtower",
            ["factionColor"] = "#7B6852",
            ["purpose"] = "Keep the old western road open.",
            ["currentAgenda"] = "Repair the bridge before the spring thaw.",
            ["principles"] = new JsonArray(
                "Every traveler receives warning before judgment."),
            ["memory"] = new JsonObject
            {
                ["summary"] = "The watch formed after the bridge massacre.",
                ["lastUpdatedTurn"] = 12,
                ["enduringFacts"] = new JsonArray(
                    "The first wardens were caravan survivors."),
                ["openThreads"] = new JsonArray(
                    "The bridge attackers were never identified.")
            },
            ["governance"] = new JsonObject
            {
                ["model"] = "Open moot",
                ["decisionProcess"] = "Active wardens decide by simple majority."
            },
            ["leadership"] = new JsonObject
            {
                ["leadershipState"] = "vacant",
                ["summary"] = "The founder died and no successor has been chosen.",
                ["leaderNpcIds"] = new JsonArray()
            },
            ["powerProfile"] = new JsonObject
            {
                ["military"] = 0,
                ["economic"] = 0,
                ["social"] = 0,
                ["covert"] = 0,
                ["logistics"] = 0,
                ["stability"] = 0,
                ["arcane_tech"] = 0,
                ["exploration"] = 0
            },
            ["ranks"] = new JsonObject
            {
                ["branches"] = new JsonArray()
            },
            ["structuredBonuses"] = new JsonArray(),
            ["resources"] = new JsonObject
            {
                ["metaResources"] = new JsonArray(),
                ["strategicGoods"] = new JsonArray()
            },
            ["relations"] = new JsonArray(),
            ["activeProjects"] = new JsonArray(),
            ["completedProjects"] = new JsonArray(),
            ["controlledTerritories"] = new JsonArray(),
            ["customStates"] = new JsonArray(),
            ["scribeChronicle"] = new JsonArray(
                "#12 - The Wayfarer Watch took responsibility for the western road."),
            ["isPlayerFaction"] = false,
            ["isPlayerMember"] = false,
            ["playerRank"] = null,
            ["playerBranch"] = null,
            ["playerStrategyDirective"] = null,
            ["reputation"] = 0,
            ["reputationDescription"] = null,
            ["level"] = 1,
            ["experience"] = 0,
            ["experienceForNextLevel"] = 100,
            ["developmentArchetype"] = "Custodian",
            ["materialization"] = BuildMortalEnvelope(
                "temp-faction-watch",
                "fmat_watch_creation")
        };

    private static JsonObject BuildMortalEnvelope(
        string factionId,
        string materializationId) =>
        new()
        {
            ["schemaVersion"] = 1,
            ["materializationId"] = materializationId,
            ["factionType"] = "mortal_faction",
            ["factionId"] = factionId,
            ["materializedAtTurn"] = 12,
            ["state"] = "complete",
            ["capabilities"] = new JsonObject
            {
                ["hasFormalHierarchy"] = false,
                ["usesFactionResources"] = false,
                ["maintainsRelations"] = false,
                ["runsProjects"] = false,
                ["holdsTerritoryOrInfluence"] = false,
                ["supportsPlayerMembership"] = false,
                ["usesCustomMechanics"] = false
            },
            ["sections"] = new JsonObject
            {
                ["hierarchy"] = EmptyDisposition("No ranks exist yet."),
                ["resources"] = EmptyDisposition("No formal resources exist yet."),
                ["relations"] = EmptyDisposition("No formal relations exist yet."),
                ["projects"] = EmptyDisposition("No projects exist yet."),
                ["territoryAndInfluence"] = EmptyDisposition("No territory is claimed."),
                ["playerMembership"] = EmptyDisposition("The player is not a member."),
                ["customStates"] = EmptyDisposition("No custom state exists.")
            }
        };

    private static JsonObject BuildPopulatedMortalEnvelope(
        string factionId,
        string materializationId) =>
        new()
        {
            ["schemaVersion"] = 1,
            ["materializationId"] = materializationId,
            ["factionType"] = "mortal_faction",
            ["factionId"] = factionId,
            ["materializedAtTurn"] = 12,
            ["state"] = "complete",
            ["capabilities"] = new JsonObject
            {
                ["hasFormalHierarchy"] = true,
                ["usesFactionResources"] = true,
                ["maintainsRelations"] = true,
                ["runsProjects"] = true,
                ["holdsTerritoryOrInfluence"] = true,
                ["supportsPlayerMembership"] = true,
                ["usesCustomMechanics"] = true
            },
            ["sections"] = new JsonObject
            {
                ["hierarchy"] = PopulatedDisposition(),
                ["resources"] = PopulatedDisposition(),
                ["relations"] = PopulatedDisposition(),
                ["projects"] = PopulatedDisposition(),
                ["territoryAndInfluence"] = PopulatedDisposition(),
                ["playerMembership"] = PopulatedDisposition(),
                ["customStates"] = PopulatedDisposition()
            }
        };

    private static JsonObject BuildShiningEnvelope(
        string factionId,
        string materializationId) =>
        new()
        {
            ["schemaVersion"] = 1,
            ["materializationId"] = materializationId,
            ["factionType"] = "shining_faction",
            ["factionId"] = factionId,
            ["materializedAtTurn"] = 12,
            ["state"] = "complete",
            ["capabilities"] = new JsonObject
            {
                ["runsProjects"] = false,
                ["holdsTerritorialInfluence"] = false,
                ["usesResourceLedger"] = false,
                ["hasResidentAffiliations"] = false,
                ["canTrade"] = false,
                ["hasLeadershipHistory"] = false,
                ["usesStoryState"] = false
            },
            ["sections"] = new JsonObject
            {
                ["projects"] = EmptyDisposition("No projects exist yet."),
                ["territorialInfluence"] = EmptyDisposition("No influence exists yet."),
                ["resourceLedger"] = EmptyDisposition("No resource ledger exists yet."),
                ["residentAffiliations"] = EmptyDisposition("No affiliations exist yet."),
                ["trade"] = EmptyDisposition("No trade authority exists yet."),
                ["leadershipHistory"] = EmptyDisposition("No leadership history exists yet."),
                ["storyState"] = EmptyDisposition("No story state exists yet.")
            }
        };

    private static JsonObject EmptyDisposition(string reason) =>
        new()
        {
            ["state"] = "empty_by_design",
            ["reason"] = reason
        };

    private static JsonObject PopulatedDisposition() =>
        new()
        {
            ["state"] = "populated"
        };
}
