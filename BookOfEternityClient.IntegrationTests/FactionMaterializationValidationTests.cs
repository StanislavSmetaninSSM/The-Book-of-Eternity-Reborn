using System.Text.Json.Nodes;
using System.Reflection;
using BookOfEternityClient.Core;
using BookOfEternityClient.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed class FactionMaterializationValidationTests : IDisposable
{
    private const string MortalPath = "game_state/factions/faction_core.json";
    private const string MortalStructurePath =
        "game_state/factions/faction_structure.json";
    private const string MortalResourcesPath =
        "game_state/factions/faction_resources.json";
    private const string MortalProjectsPath =
        "game_state/factions/faction_projects.json";
    private const string MortalCustomPath =
        "game_state/factions/faction_custom.json";
    private const string MortalChroniclesPath =
        "game_state/factions/faction_chronicles.json";
    private const string CurrentLocationPath =
        "game_state/world/current_location.json";
    private const string WorldMapPath =
        "game_state/world/world_map.json";
    private const string NpcCorePath =
        "game_state/npcs/npc_core.json";
    private const string ShiningPath = "game_state/meta/shining_abode_state.json";
    private const string ResidentPath = "game_state/meta/guardian_abode_residents.json";
    private const string SoulPath = "game_state/meta/soul_state.json";
    private const string AfterlifeProfilesPath = "game_state/meta/afterlife_entity_profiles.json";
    private const string GuardiansPath = "game_state/meta/guardians.json";
    private const string SarefStoryPath = "game_state/meta/main_story_saref_state.json";

    public static TheoryData<string, string> MissingMortalSemantics => new()
    {
        { "image_prompt", "faction_materialization_mortal_image_prompt_missing" },
        { "factionColor", "faction_materialization_mortal_color_missing" },
        { "purpose", "faction_materialization_mortal_purpose_missing" },
        { "currentAgenda", "faction_materialization_mortal_agenda_missing" },
        { "principles", "faction_materialization_mortal_principles_missing" },
        { "memory", "faction_materialization_mortal_memory_missing" },
        { "governance", "faction_materialization_mortal_governance_missing" },
        { "leadership", "faction_materialization_mortal_leadership_missing" },
        { "scribeChronicle", "faction_materialization_mortal_chronicle_missing" }
    };

    public static TheoryData<string, string> MissingShiningSemantics => new()
    {
        { "creationProvenance", "faction_materialization_shining_provenance_missing" },
        { "hallId", "faction_materialization_shining_hall_missing" },
        { "charter", "faction_materialization_shining_charter_missing" },
        { "currentAgenda", "faction_materialization_shining_agenda_missing" },
        { "factionLifecycle", "faction_materialization_shining_lifecycle_missing" },
        { "leadership", "faction_materialization_shining_leadership_missing" },
        { "strategicMemory", "faction_materialization_shining_memory_missing" },
        { "chronicle", "faction_materialization_shining_chronicle_missing" },
        { "visibility", "faction_materialization_shining_visibility_missing" },
        { "storyAuthority", "faction_materialization_shining_story_authority_missing" }
    };

    public static TheoryData<string, string> InvalidNativeDiscoveryRoutes => new()
    {
        { "duplicate_hall", "faction_materialization_shining_hall_reference_invalid" },
        { "extra_faction", "shining_discovery_unexpected_new_faction" },
        { "resident_count_low", "shining_discovery_invalid_new_resident_count" },
        { "resident_count_high", "shining_discovery_invalid_new_resident_count" },
        { "project_count", "shining_discovery_invalid_seeded_project_count" },
        { "project_not_completed", "shining_discovery_missing_seeded_project" },
        { "extra_unlisted_project", "faction_materialization_shining_route_project_set_invalid" },
        { "reuse_hall", "shining_discovery_reused_existing_hall_id" },
        { "reuse_faction", "shining_discovery_reused_existing_faction_id" },
        { "reuse_resident", "shining_discovery_invalid_new_resident_materialization" },
        { "reuse_project", "shining_discovery_reused_existing_project_id" },
        { "case_variant_faction_id", "faction_materialization_shining_route_identity_invalid" },
        { "case_variant_hall_id", "faction_materialization_shining_route_identity_invalid" },
        { "missing_actor_envelope", "actor_materialization_missing" },
        { "wrong_cost", "shining_discovery_light_sparks_cost_mismatch" },
        { "wrong_receipt", "shining_core_action_receipt_mismatch" },
        { "unrelated_resident_rewrite", "shining_discovery_existing_resident_changed" }
    };

    public static TheoryData<string, string> InvalidPlayerFoundingRoutes => new()
    {
        { "request_id", "faction_materialization_shining_route_provenance_invalid" },
        { "charter", "shining_founding_missing_faction_materialization" },
        { "supporters", "shining_founding_supporter_not_reassigned" },
        { "hall", "shining_founding_missing_hall_materialization" },
        { "player_soul", "shining_founding_missing_faction_materialization" },
        { "quoted_cost", "shining_founding_receipt_mismatch" },
        { "reserved_light_sparks", "shining_founding_reserved_light_sparks_rollback" },
        { "reserved_feathers", "shining_founding_reserved_ink_feathers_rollback" },
        { "missing_root_receipt", "shining_founding_missing_resolution" },
        { "missing_history", "faction_materialization_shining_route_history_invalid" },
        { "case_variant_faction_id", "faction_materialization_shining_route_identity_invalid" },
        { "case_variant_hall_id", "faction_materialization_shining_route_identity_invalid" },
        { "unrelated_resident_rewrite", "faction_materialization_shining_route_resident_affiliation_invalid" }
    };

    public static TheoryData<string> LegacyMortalExternalTouchChannels => new()
    {
        "factionRankChanges",
        "factionBonusChanges",
        "factionResourceChanges",
        "factionProjectUpdates",
        "completeFactionProjects",
        "factionCustomStateChanges",
        "factionChronicleUpdates",
        "current_location_factionControl",
        "world_map_factionControl",
        "npc_factionAffiliations"
    };

    public static TheoryData<string, string>
        RawFactionTouchProjectionCases => new()
        {
            {
                "mortal_npc_affiliation_upsert",
                "mortal_faction:faction_target"
            },
            {
                "shining_resident_update_move",
                "shining_faction:order_new,shining_faction:order_old"
            },
            {
                "shining_saref_update_move",
                "shining_faction:order_new,shining_faction:order_old"
            },
            {
                "shining_guardian_create",
                "shining_faction:order_guardian"
            },
            {
                "shining_political_current_move",
                "shining_faction:order_new,shining_faction:order_old"
            },
            { "shining_identity_upsert_omission", "" },
            {
                "shining_same_identity_last_write_exact_case",
                "shining_faction:ORDER_NEW,shining_faction:order_old"
            },
            { "shining_unrelated_guardian_mutation", "" },
            { "shining_nested_reorder", "" },
            { "mortal_structure_nested_reorder", "" },
            { "mortal_resources_nested_reorder", "" },
            { "mortal_custom_nested_reorder", "" },
            { "mortal_project_cost_nested_reorder", "" },
            {
                "mortal_nested_duplicate_addition",
                "mortal_faction:faction_target"
            },
            {
                "mortal_nested_duplicate_removal",
                "mortal_faction:faction_target"
            },
            {
                "mortal_same_identity_semantic_change",
                "mortal_faction:faction_target"
            },
            {
                "mortal_ordered_benefits_reorder",
                "mortal_faction:faction_target"
            },
            { "shining_long_receipt_history_reorder", "" }
        };

    public static TheoryData<string>
        MortalPromotionHistoricalMutationSurfaces => new()
        {
            "core_profile",
            "progression_power",
            "governance",
            "leadership",
            "ranks",
            "resources",
            "relations",
            "projects",
            "custom_state",
            "current_location_faction_control",
            "world_map_faction_control",
            "npc_affiliation"
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
    public async Task Validate_ShiningExactDerivedProjectionFieldsOnly_DoesNotPromoteLegacyFaction()
    {
        var preTurnFaction = LegacyShiningFaction("order_dawn", factionStrength: 30);
        var currentFaction = LegacyShiningFaction("order_dawn", factionStrength: 31);
        preTurnFaction["derivedTier"] = 1;
        currentFaction["derivedTier"] = 2;
        preTurnFaction["serviceMultiplier"] = 1.0;
        currentFaction["serviceMultiplier"] = 1.25;
        await WriteCurrentAndSnapshotAsync(
            (ShiningPath, ShiningRoot(currentFaction).ToJsonString()),
            (ShiningPath, ShiningRoot(preTurnFaction).ToJsonString()));

        var issues = await _validator.ValidateAcceptedTurnRawFactionMaterializationAsync();

        Assert.DoesNotContain(issues, issue =>
            issue.Actor == "shining_faction:order_dawn" &&
            issue.Code == "faction_materialization_missing");
    }

    [Fact]
    public async Task Validate_ShiningNestedServiceMultiplierSnapshot_IsGmAuthoredTouch()
    {
        var preTurnFaction = LegacyShiningFaction("order_dawn", factionStrength: 30);
        var currentFaction = LegacyShiningFaction("order_dawn", factionStrength: 30);
        preTurnFaction["tradeInventory"] = new JsonObject
        {
            ["serviceMultiplierSnapshot"] = 1.0
        };
        currentFaction["tradeInventory"] = new JsonObject
        {
            ["serviceMultiplierSnapshot"] = 1.25
        };
        await WriteCurrentAndSnapshotAsync(
            (ShiningPath, ShiningRoot(currentFaction).ToJsonString()),
            (ShiningPath, ShiningRoot(preTurnFaction).ToJsonString()));

        var issues = await _validator.ValidateAcceptedTurnRawFactionMaterializationAsync();

        Assert.Contains(issues, issue =>
            issue.Actor == "shining_faction:order_dawn" &&
            issue.Code == "faction_materialization_missing");
    }

    [Theory]
    [MemberData(nameof(LegacyMortalExternalTouchChannels))]
    public async Task Validate_LegacyMortalExternalTouchWithoutPromotion_ReportsPromotionRequired(
        string channel)
    {
        await WriteLegacyMortalExternalTouchAsync(channel);

        var issues = await _validator
            .ValidateAcceptedTurnRawFactionMaterializationAsync();

        Assert.Contains(issues, issue =>
            issue.Code == "faction_legacy_promotion_required" &&
            issue.Actor == "mortal_faction:faction_watch");
    }

    [Fact]
    public async Task Validate_LegacyShiningResidentRealignmentWithoutPromotion_ReportsPromotionRequired()
    {
        const string sourceFactionId = "order_twilight";
        const string targetFactionId = "order_dawn";
        const string residentId = "resident_liora";
        const string requestId = "realign_req_liora";
        const string historyEntryId = "history_realign_liora";

        var preTurnShining = ShiningRoot(
            LegacyShiningFaction(sourceFactionId, factionStrength: 30),
            LegacyShiningFaction(targetFactionId, factionStrength: 30));
        var currentShining = CloneJsonObject(preTurnShining);
        currentShining["factionRealignmentReceipts"] = new JsonArray(
            new JsonObject
            {
                ["requestId"] = requestId,
                ["residentId"] = residentId,
                ["residentName"] = "Liora",
                ["sourceFactionId"] = sourceFactionId,
                ["targetFactionId"] = targetFactionId,
                ["status"] = ShiningFactionRequestState.RequestStatusAccepted,
                ["realignmentMode"] =
                    ShiningFactionRequestState.RealignmentModeAcceptedTransfer,
                ["residentHistoryEntryId"] = historyEntryId,
                ["resolvedAtTurn"] = 12,
                ["resolvedAtUtc"] = "2026-08-03T12:00:00Z",
                ["reason"] = "accepted_by_target_faction"
            });

        var preTurnResidents = BuildRouteResidentRoot(
            BuildRouteResident(residentId, sourceFactionId));
        var currentResidents = BuildRouteResidentRoot(
            BuildRouteResident(residentId, targetFactionId));
        ((JsonArray)currentResidents[
            GuardianAbodeResidentState.HistoryLogProperty]!).Add(
            new JsonObject
            {
                ["entryId"] = historyEntryId,
                ["residentId"] = residentId,
                ["title"] = "A new radiant allegiance",
                ["summary"] = "Liora joined the Dawn order.",
                ["revealedAtTurn"] = 12,
                ["revealedAtUtc"] = "2026-08-03T12:00:00Z",
                ["tags"] = new JsonArray("faction", "realignment")
            });
        var pendingRealignment = new JsonObject
        {
            [ShiningFactionRequestState.RequestsProperty] = new JsonArray(
                new JsonObject
                {
                    ["requestId"] = requestId,
                    ["residentId"] = residentId,
                    ["residentName"] = "Liora",
                    ["sourceFactionId"] = sourceFactionId,
                    ["sourceFactionName"] = "Twilight Order",
                    ["targetFactionId"] = targetFactionId,
                    ["targetFactionName"] = "Dawn Order",
                    ["realignmentMode"] =
                        ShiningFactionRequestState.RealignmentModeAcceptedTransfer,
                    ["factionLoyaltyLevel"] = 14,
                    ["factionLoyaltyTier"] =
                        ShiningAbodeState.FactionLoyaltyTierAlienated,
                    ["factionRestlessness"] = 76,
                    ["factionRealignmentState"] =
                        ShiningAbodeState.FactionRealignmentStateReadyToRealign,
                    ["createdAtTurn"] = 12,
                    ["createdAtUtc"] = "2026-08-03T11:59:00Z"
                })
        };

        await WriteCurrentAndSnapshotAsync(
            (ShiningPath, currentShining.ToJsonString()),
            (ResidentPath, currentResidents.ToJsonString()),
            (ShiningFactionRequestState.PendingRealignmentsRequestPath,
                pendingRealignment.ToJsonString()),
            (ShiningPath, preTurnShining.ToJsonString()),
            (ResidentPath, preTurnResidents.ToJsonString()),
            (ShiningFactionRequestState.PendingRealignmentsRequestPath,
                pendingRealignment.ToJsonString()));

        var issues = (await _validator
                .ValidateAcceptedTurnRawFactionMaterializationAsync())
            .ToList();
        await InvokeRouteValidationAsync(
            "ValidatePendingShiningRealignmentResolutionAsync",
            issues);

        Assert.DoesNotContain(issues, issue =>
            issue.Code?.StartsWith(
                "shining_realignment_",
                StringComparison.Ordinal) == true);
        Assert.Contains(issues, issue =>
            issue.Code == "faction_legacy_promotion_required" &&
            issue.Actor == $"shining_faction:{sourceFactionId}");
        Assert.Contains(issues, issue =>
            issue.Code == "faction_legacy_promotion_required" &&
            issue.Actor == $"shining_faction:{targetFactionId}");
    }

    [Theory]
    [InlineData("untouched_mortal")]
    [InlineData("shining_derived_only")]
    public async Task Validate_LegacyExternalTouchControls_DoNotPromote(
        string control)
    {
        if (string.Equals(
                control,
                "untouched_mortal",
                StringComparison.Ordinal))
        {
            var faction = LegacyMortalFaction("faction_watch");
            await WriteCurrentAndSnapshotAsync(
                (MortalPath, MortalRoot(
                    CloneJsonObject(faction)).ToJsonString()),
                (MortalPath, MortalRoot(faction).ToJsonString()));
        }
        else
        {
            var preTurnFaction = LegacyShiningFaction(
                "order_dawn",
                factionStrength: 30);
            var currentFaction = LegacyShiningFaction(
                "order_dawn",
                factionStrength: 31);
            preTurnFaction["derivedTier"] = 1;
            currentFaction["derivedTier"] = 2;
            preTurnFaction["serviceMultiplier"] = 1.0;
            currentFaction["serviceMultiplier"] = 1.25;
            await WriteCurrentAndSnapshotAsync(
                (ShiningPath, ShiningRoot(currentFaction).ToJsonString()),
                (ShiningPath, ShiningRoot(preTurnFaction).ToJsonString()));
        }

        var issues = await _validator
            .ValidateAcceptedTurnRawFactionMaterializationAsync();

        Assert.DoesNotContain(issues, issue =>
            issue.Code == "faction_legacy_promotion_required");
    }

    [Theory]
    [MemberData(nameof(RawFactionTouchProjectionCases))]
    public async Task
        Validate_RawFactionTouchProjection_ClosesCarrierAndComparatorResiduals(
            string scenario,
            string expectedActorsCsv)
    {
        await WriteRawFactionTouchProjectionCaseAsync(scenario);

        var issues = await _validator
            .ValidateAcceptedTurnRawFactionMaterializationAsync();

        var expectedActors = string.IsNullOrEmpty(expectedActorsCsv)
            ? Array.Empty<string>()
            : expectedActorsCsv
                .Split(',', StringSplitOptions.RemoveEmptyEntries)
                .OrderBy(actor => actor, StringComparer.Ordinal)
                .ToArray();
        var actualActors = issues
            .Where(issue =>
                issue.Code == "faction_legacy_promotion_required")
            .Select(issue => issue.Actor)
            .Where(actor => actor != null)
            .Cast<string>()
            .OrderBy(actor => actor, StringComparer.Ordinal)
            .ToArray();

        Assert.Equal(expectedActors, actualActors);
    }

    [Theory]
    [MemberData(nameof(MissingShiningSemantics))]
    public async Task NewShiningFaction_MissingAuthoredSemantic_FailsBeforeNormalization(
        string field,
        string code)
    {
        var faction = BuildCompleteNativeShiningFaction();
        faction.Remove(field);
        await WriteNativeDiscoveryOutcomeAsync(faction);

        var issues = await _validator.ValidateAcceptedTurnRawFactionMaterializationAsync();

        Assert.Contains(issues, issue =>
            issue.Code == code &&
            issue.Actor == "shining_faction:shine_faction_dawn_archive");
    }

    [Fact]
    public async Task NewShiningFaction_AllSevenExactEmptySurfaces_PassesRaw()
    {
        await WriteNativeDiscoveryOutcomeAsync(
            BuildCompleteNativeShiningFaction());

        var issues = await _validator.ValidateAcceptedTurnRawFactionMaterializationAsync();

        Assert.DoesNotContain(issues, issue =>
            issue.Severity == IssueSeverity.Error &&
            issue.Actor == "shining_faction:shine_faction_dawn_archive");
    }

    [Theory]
    [InlineData(false, true, false)]
    [InlineData(false, false, true)]
    [InlineData(true, false, false)]
    [InlineData(true, true, true)]
    public async Task NewShiningFaction_TradeCapabilityMustMatchOperationalLeadershipEvidence(
        bool vacantLeadership,
        bool declaredCanTrade,
        bool expectsMismatch)
    {
        var faction = BuildCompleteNativeShiningFaction();
        var leadership = faction["leadership"]!.AsObject();
        if (vacantLeadership)
        {
            leadership["leadershipState"] = ShiningAbodeState.LeadershipStateVacant;
            leadership["headActorType"] = null;
            leadership["headActorId"] = null;
        }

        faction["materialization"]!["capabilities"]!["canTrade"] =
            declaredCanTrade;
        await WriteNativeDiscoveryOutcomeAsync(faction);

        var issues = await _validator.ValidateAcceptedTurnRawFactionMaterializationAsync();
        var hasMismatch = issues.Any(issue =>
            issue.Code == "faction_materialization_capability_mismatch" &&
            issue.Actor == "shining_faction:shine_faction_dawn_archive" &&
            issue.Section == "canTrade");

        Assert.Equal(expectsMismatch, hasMismatch);
    }

    [Theory]
    [InlineData(20, 30, true)]
    [InlineData(30, 20, false)]
    public async Task NewShiningFaction_ForgedStrengthCannotControlTradeCapability(
        int derivedBaseStrength,
        int submittedFactionStrength,
        bool declaredCanTrade)
    {
        var faction = BuildCompleteNativeShiningFaction();
        faction["baseStrength"] = derivedBaseStrength;
        faction["factionStrength"] = submittedFactionStrength;
        faction["materialization"]!["capabilities"]!["canTrade"] =
            declaredCanTrade;
        await WriteNativeDiscoveryOutcomeAsync(faction);

        var issues = await _validator.ValidateAcceptedTurnRawFactionMaterializationAsync();

        Assert.Contains(issues, issue =>
            issue.Code == "faction_materialization_capability_mismatch" &&
            issue.Actor == "shining_faction:shine_faction_dawn_archive" &&
            issue.Section == "canTrade");
    }

    [Theory]
    [InlineData(1, 20, 30, true)]
    [InlineData(3, -20, 10, false)]
    public async Task NewShiningFaction_ForgedProjectRewardCannotControlTradeCapability(
        int projectTier,
        int submittedProjectStrengthReward,
        int submittedFactionStrength,
        bool declaredCanTrade)
    {
        var faction = BuildCompleteNativeShiningFaction();
        faction["baseStrength"] = 10;
        faction["factionStrength"] = submittedFactionStrength;
        AddCompletedShiningProject(
            faction,
            projectTier,
            submittedProjectStrengthReward);
        faction["materialization"]!["capabilities"]!["canTrade"] =
            declaredCanTrade;
        await WriteNativeDiscoveryOutcomeAsync(faction);

        var issues = await _validator.ValidateAcceptedTurnRawFactionMaterializationAsync();

        Assert.Contains(issues, issue =>
            issue.Code == "faction_materialization_capability_mismatch" &&
            issue.Actor == "shining_faction:shine_faction_dawn_archive" &&
            issue.Section == "canTrade");
    }

    [Fact]
    public async Task NewShiningFaction_TradeContentCannotUseEmptyDisposition()
    {
        var faction = BuildCompleteNativeShiningFaction();
        faction["tradeInventory"] = new JsonObject
        {
            ["tradeCycleId"] = "shining_return_12"
        };
        await WriteNativeDiscoveryOutcomeAsync(faction);

        var issues = await _validator.ValidateAcceptedTurnRawFactionMaterializationAsync();

        Assert.Contains(issues, issue =>
            issue.Code == "faction_materialization_disposition_mismatch" &&
            issue.Actor == "shining_faction:shine_faction_dawn_archive" &&
            issue.Section == "trade");
    }

    [Theory]
    [InlineData("exact", false)]
    [InlineData("missing_inventory", true)]
    [InlineData("missing_receipts", true)]
    [InlineData("null_receipts", true)]
    [InlineData("non_empty_receipts", true)]
    public async Task NewShiningFaction_EmptyTradeDispositionRequiresExplicitNullAndEmptyReceipts(
        string surface,
        bool expectsMismatch)
    {
        var faction = BuildCompleteNativeShiningFaction();
        switch (surface)
        {
            case "missing_inventory":
                faction.Remove("tradeInventory");
                break;
            case "missing_receipts":
                faction.Remove("tradeInventoryReceipts");
                break;
            case "null_receipts":
                faction["tradeInventoryReceipts"] = null;
                break;
            case "non_empty_receipts":
                faction["tradeInventoryReceipts"] = new JsonArray(
                    new JsonObject
                    {
                        ["requestId"] = "trade_history"
                    });
                break;
        }

        await WriteNativeDiscoveryOutcomeAsync(faction);

        var issues = await _validator.ValidateAcceptedTurnRawFactionMaterializationAsync();
        var hasMismatch = issues.Any(issue =>
            issue.Code == "faction_materialization_disposition_mismatch" &&
            issue.Actor == "shining_faction:shine_faction_dawn_archive" &&
            issue.Section == "trade");

        Assert.Equal(expectsMismatch, hasMismatch);
    }

    [Fact]
    public async Task NewShiningFaction_UnknownExactHall_FailsRaw()
    {
        var faction = BuildCompleteNativeShiningFaction();
        faction["hallId"] = "hall_missing";
        await WriteNativeDiscoveryOutcomeAsync(faction);

        var issues = await _validator.ValidateAcceptedTurnRawFactionMaterializationAsync();

        Assert.Contains(issues, issue =>
            issue.Code == "faction_materialization_shining_hall_reference_invalid" &&
            issue.Actor == "shining_faction:shine_faction_dawn_archive");
    }

    [Theory]
    [InlineData("provenance_extra", "faction_materialization_shining_provenance_invalid")]
    [InlineData("route_invalid", "faction_materialization_shining_provenance_invalid")]
    [InlineData("non_story_authority", "faction_materialization_shining_story_authority_invalid")]
    [InlineData("story_authority_mismatch", "faction_materialization_shining_story_authority_invalid")]
    public async Task NewShiningFaction_ProvenanceAndStoryAuthorityUseClosedRouteShape(
        string mutation,
        string expectedCode)
    {
        var faction = BuildCompleteNativeShiningFaction();
        switch (mutation)
        {
            case "provenance_extra":
                faction["creationProvenance"]!["extra"] = "not_allowed";
                break;
            case "route_invalid":
                faction["creationProvenance"]!["route"] = "inferred";
                break;
            case "non_story_authority":
                faction["storyAuthority"] = new JsonObject
                {
                    ["authorityType"] = "saref_main_story",
                    ["authorityId"] = "shine_faction_dawn_archive",
                    ["factionRole"] = "wings_of_angels"
                };
                break;
            case "story_authority_mismatch":
                faction["originType"] = ShiningAbodeState.OriginTypeAscendedGuardian;
                faction["creationProvenance"]!["route"] = "story";
                faction["creationProvenance"]!["authorityType"] = "guardian_ascension";
                faction["creationProvenance"]!["authorityId"] = "guardian_dawn";
                faction["storyAuthority"] = new JsonObject
                {
                    ["authorityType"] = "guardian_ascension",
                    ["authorityId"] = "guardian_other",
                    ["factionRole"] = "patron_guardian"
                };
                break;
        }

        await WriteNativeDiscoveryOutcomeAsync(faction);

        var issues = await _validator.ValidateAcceptedTurnRawFactionMaterializationAsync();

        Assert.Contains(issues, issue =>
            issue.Code == expectedCode &&
            issue.Actor == "shining_faction:shine_faction_dawn_archive");
    }

    [Theory]
    [InlineData("hidden")]
    [InlineData("rumored")]
    [InlineData("revealed")]
    public async Task StoryFaction_ExactAuthorityAndVisibility_Passes(
        string visibility)
    {
        var faction = BuildCompleteSarefStoryFaction(visibility);
        var sarefRoot = BuildSarefStoryRoot(visibility);
        await WriteStoryFactionOutcomeAsync(
            faction,
            sarefRoot: sarefRoot);

        var issues = await _validator.ValidateAcceptedTurnRawFactionMaterializationAsync();

        Assert.DoesNotContain(issues, issue =>
            issue.Severity == IssueSeverity.Error &&
            issue.Actor == "shining_faction:shine_faction_wings");
    }

    [Fact]
    public async Task StoryFaction_SecretiveNameWithoutAuthority_Fails()
    {
        var faction = BuildCompleteSarefStoryFaction("hidden");
        faction["charter"]!["factionName"] = "The Hidden Wings";
        faction["charter"]!["summary"] =
            "A secret order concealed from all.";
        faction["storyAuthority"] = null;
        await WriteStoryFactionOutcomeAsync(
            faction,
            sarefRoot: BuildSarefStoryRoot("hidden"));

        var issues = await _validator.ValidateAcceptedTurnRawFactionMaterializationAsync();

        Assert.Contains(issues, issue =>
            issue.Code == "faction_materialization_shining_story_authority_invalid" &&
            issue.Actor == "shining_faction:shine_faction_wings");
    }

    [Theory]
    [InlineData("missing_authority_id")]
    [InlineData("wrong_authority_id")]
    [InlineData("wrong_role")]
    [InlineData("wrong_story_visibility")]
    [InlineData("wrong_faction_visibility")]
    [InlineData("wrong_legacy_visibility")]
    [InlineData("wrong_faction_role")]
    [InlineData("provenance_mismatch")]
    public async Task StoryFaction_MissingOrWrongCanonicalBinding_Fails(
        string mutation)
    {
        var faction = BuildCompleteSarefStoryFaction("hidden");
        var sarefRoot = BuildSarefStoryRoot("hidden");
        ApplySarefStoryMutation(faction, sarefRoot, mutation);
        await WriteStoryFactionOutcomeAsync(
            faction,
            sarefRoot: sarefRoot);

        var issues = await _validator.ValidateAcceptedTurnRawFactionMaterializationAsync();

        Assert.Contains(issues, issue =>
            issue.Actor == "shining_faction:shine_faction_wings" &&
            issue.Code is
                "faction_materialization_shining_story_authority_invalid" or
                "faction_materialization_shining_story_authority_reference_invalid");
    }

    [Fact]
    public async Task GuardianStoryFaction_ExactAuthorityLeaderAndProfile_Passes()
    {
        var faction = BuildCompleteGuardianStoryFaction();
        await WriteStoryFactionOutcomeAsync(
            faction,
            guardiansRoot: BuildGuardianStoryRoot(),
            profiles: new[]
            {
                BuildRouteAfterlifeProfile(
                    "guardian_dawn",
                    includeEnvelope: true,
                    actorType: ShiningAbodeState.HeadActorTypeGuardian,
                    canTrade: true)
            });

        var issues = await _validator.ValidateAcceptedTurnRawFactionMaterializationAsync();

        Assert.DoesNotContain(issues, issue =>
            issue.Severity == IssueSeverity.Error &&
            issue.Actor == "shining_faction:shine_faction_guardian_dawn");
    }

    [Theory]
    [InlineData("wrong_authority_id", "faction_materialization_shining_story_authority_reference_invalid")]
    [InlineData("wrong_role", "faction_materialization_shining_story_authority_reference_invalid")]
    [InlineData("wrong_visibility", "faction_materialization_shining_story_authority_reference_invalid")]
    [InlineData("wrong_head_type", "faction_materialization_shining_story_authority_reference_invalid")]
    [InlineData("wrong_head_id", "faction_materialization_shining_story_authority_reference_invalid")]
    [InlineData("missing_profile", "faction_materialization_shining_actor_profile_invalid")]
    [InlineData("incomplete_profile", "actor_materialization_missing")]
    [InlineData("duplicate_guardian", "faction_materialization_shining_story_authority_reference_invalid")]
    public async Task GuardianStoryFaction_InvalidAuthorityLeaderOrProfile_Fails(
        string mutation,
        string expectedCode)
    {
        var faction = BuildCompleteGuardianStoryFaction();
        var guardiansRoot = BuildGuardianStoryRoot(
            duplicateGuardian:
                string.Equals(
                    mutation,
                    "duplicate_guardian",
                    StringComparison.Ordinal));
        var profiles = new List<JsonObject>
        {
            BuildRouteAfterlifeProfile(
                "guardian_dawn",
                includeEnvelope:
                    !string.Equals(
                        mutation,
                        "incomplete_profile",
                        StringComparison.Ordinal),
                actorType: ShiningAbodeState.HeadActorTypeGuardian,
                canTrade: true)
        };
        ApplyGuardianStoryMutation(faction, mutation);
        if (string.Equals(
                mutation,
                "missing_profile",
                StringComparison.Ordinal))
        {
            profiles.Clear();
        }

        await WriteStoryFactionOutcomeAsync(
            faction,
            guardiansRoot: guardiansRoot,
            profiles: profiles);

        var issues = await _validator.ValidateAcceptedTurnRawFactionMaterializationAsync();

        Assert.Contains(issues, issue =>
            issue.Code == expectedCode);
    }

    [Theory]
    [InlineData("head")]
    [InlineData("resident")]
    [InlineData("political")]
    public async Task ShiningRequiredActor_CompleteEnvelope_Passes(
        string actorKind)
    {
        await WriteRequiredShiningActorOutcomeAsync(
            actorKind,
            includeProfile: true,
            includeEnvelope: true);

        var issues = await _validator.ValidateAcceptedTurnRawFactionMaterializationAsync();

        Assert.DoesNotContain(issues, issue =>
            issue.Code ==
                "faction_materialization_shining_actor_profile_invalid" ||
            issue.Code?.StartsWith(
                "actor_materialization_",
                StringComparison.Ordinal) == true);
    }

    [Fact]
    public async Task ShiningRequiredActor_ForgedStrengthCannotGrantTradeAuthority()
    {
        await WriteRequiredShiningActorOutcomeAsync(
            "head",
            includeProfile: true,
            includeEnvelope: true,
            derivedBaseStrength: 20,
            submittedFactionStrength: 30,
            factionCanTrade: false,
            actorCanTrade: true);

        var issues = await _validator.ValidateAcceptedTurnRawFactionMaterializationAsync();
        var factionHasMismatch = issues.Any(issue =>
            issue.Code == "faction_materialization_capability_mismatch" &&
            issue.Actor == "shining_faction:shine_faction_dawn_archive" &&
            issue.Section == "canTrade");
        var actorHasMismatch = issues.Any(issue =>
            issue.Code == "actor_materialization_capability_mismatch" &&
            issue.Actor == "resident:required_head" &&
            issue.Section == "canTrade");

        Assert.True(
            !factionHasMismatch && actorHasMismatch,
            $"Faction mismatch: {factionHasMismatch}; actor mismatch: {actorHasMismatch}.");
    }

    [Fact]
    public async Task ShiningRequiredActor_ForgedProjectRewardCannotGrantTradeAuthority()
    {
        await WriteRequiredShiningActorOutcomeAsync(
            "head",
            includeProfile: true,
            includeEnvelope: true,
            derivedBaseStrength: 10,
            submittedFactionStrength: 33,
            factionCanTrade: false,
            actorCanTrade: true,
            projectTier: 1,
            submittedProjectStrengthReward: 20);

        var issues = await _validator.ValidateAcceptedTurnRawFactionMaterializationAsync();
        var factionHasMismatch = issues.Any(issue =>
            issue.Code == "faction_materialization_capability_mismatch" &&
            issue.Actor == "shining_faction:shine_faction_dawn_archive" &&
            issue.Section == "canTrade");
        var actorHasMismatch = issues.Any(issue =>
            issue.Code == "actor_materialization_capability_mismatch" &&
            issue.Actor == "resident:required_head" &&
            issue.Section == "canTrade");

        Assert.True(
            !factionHasMismatch && actorHasMismatch,
            $"Faction mismatch: {factionHasMismatch}; actor mismatch: {actorHasMismatch}.");
    }

    [Theory]
    [InlineData("head", false, false, "faction_materialization_shining_actor_profile_invalid")]
    [InlineData("head", true, false, "actor_materialization_missing")]
    [InlineData("resident", false, false, "faction_materialization_shining_actor_profile_invalid")]
    [InlineData("resident", true, false, "actor_materialization_missing")]
    [InlineData("political", false, false, "faction_materialization_shining_actor_profile_invalid")]
    [InlineData("political", true, false, "actor_materialization_missing")]
    public async Task ShiningRequiredActor_MissingOrIncompleteEnvelope_Fails(
        string actorKind,
        bool includeProfile,
        bool includeEnvelope,
        string expectedCode)
    {
        await WriteRequiredShiningActorOutcomeAsync(
            actorKind,
            includeProfile,
            includeEnvelope);

        var issues = await _validator.ValidateAcceptedTurnRawFactionMaterializationAsync();

        Assert.Contains(issues, issue =>
            issue.Code == expectedCode);
    }

    [Theory]
    [InlineData("exact_plus_different_type", false)]
    [InlineData("wrong_type_only", true)]
    [InlineData("duplicate_exact_pair", true)]
    public async Task ShiningRequiredActor_ExactProfilePairControls(
        string profileShape,
        bool expectsProfileIssue)
    {
        await WriteRequiredShiningActorOutcomeAsync(
            "resident",
            includeProfile: true,
            includeEnvelope: true,
            profileShape: profileShape);

        var issues = await _validator.ValidateAcceptedTurnRawFactionMaterializationAsync();
        var factionMaterializationErrors = issues
            .Where(issue =>
                issue.Severity == IssueSeverity.Error &&
                issue.Actor ==
                    "shining_faction:shine_faction_dawn_archive" &&
                issue.Section == "FactionMaterialization")
            .ToArray();

        if (expectsProfileIssue)
        {
            var profileIssue =
                Assert.Single(factionMaterializationErrors);
            Assert.Equal(
                "faction_materialization_shining_actor_profile_invalid",
                profileIssue.Code);
            Assert.Equal(
                $"{ResidentPath}.entries[required_resident]",
                profileIssue.FilePath);
        }
        else
        {
            Assert.Empty(factionMaterializationErrors);
        }
        Assert.DoesNotContain(issues, issue =>
            issue.Code?.StartsWith(
                "actor_materialization_",
                StringComparison.Ordinal) == true);
    }

    [Theory]
    [InlineData("missing_resident_id")]
    [InlineData("duplicate_resident_id")]
    [InlineData("duplicate_political_identity")]
    [InlineData("duplicate_resident_id_across_factions")]
    [InlineData("duplicate_political_identity_across_factions")]
    public async Task ShiningRequiredActor_RawSourceIdentityMustBeUsableAndUnique(
        string mutation)
    {
        await WriteRequiredShiningActorSourceOutcomeAsync(mutation);

        var expectedPath = mutation switch
        {
            "missing_resident_id" =>
                $"{ResidentPath}.entries[0]",
            "duplicate_resident_id" =>
                $"{ResidentPath}.entries[1]",
            "duplicate_resident_id_across_factions" =>
                $"{ResidentPath}.entries[1]",
            "duplicate_political_identity" =>
                $"{ShiningPath}.shiningPoliticalActors[1]",
            "duplicate_political_identity_across_factions" =>
                $"{ShiningPath}.shiningPoliticalActors[1]",
            _ => throw new ArgumentOutOfRangeException(
                nameof(mutation),
                mutation,
                "Unsupported required actor source mutation.")
        };
        var issues = await _validator.ValidateAcceptedTurnRawFactionMaterializationAsync();
        var factionMaterializationErrors = issues
            .Where(issue =>
                issue.Severity == IssueSeverity.Error &&
                issue.Actor ==
                    "shining_faction:shine_faction_dawn_archive" &&
                issue.Section == "FactionMaterialization")
            .ToArray();

        var sourceIssue =
            Assert.Single(factionMaterializationErrors);
        Assert.Equal(
            "faction_materialization_shining_actor_profile_invalid",
            sourceIssue.Code);
        Assert.Equal(expectedPath, sourceIssue.FilePath);
        Assert.DoesNotContain(issues, issue =>
            issue.Code?.StartsWith(
                "actor_materialization_",
                StringComparison.Ordinal) == true);
    }

    [Theory]
    [InlineData("player_soul")]
    [InlineData("vacant")]
    public async Task ShiningHeadActor_ExactDocumentedException_Passes(
        string exceptionKind)
    {
        var faction = BuildCompleteNativeShiningFaction();
        var leadership = faction["leadership"]!.AsObject();
        if (string.Equals(
                exceptionKind,
                "vacant",
                StringComparison.Ordinal))
        {
            leadership["leadershipState"] =
                ShiningAbodeState.LeadershipStateVacant;
            leadership["headActorType"] = null;
            leadership["headActorId"] = null;
            faction["materialization"]!["capabilities"]!["canTrade"] =
                false;
        }

        await WriteNativeDiscoveryOutcomeAsync(faction);

        var issues = await _validator.ValidateAcceptedTurnRawFactionMaterializationAsync();

        Assert.DoesNotContain(issues, issue =>
            issue.Code ==
                "faction_materialization_shining_actor_profile_invalid" ||
            issue.Code ==
                "faction_materialization_shining_leadership_reference_invalid" ||
            issue.Code?.StartsWith(
                "actor_materialization_",
                StringComparison.Ordinal) == true);
    }

    [Fact]
    public async Task ShiningHeadActor_PlayerSoulExceptionRequiresExactPair()
    {
        var faction = BuildCompleteNativeShiningFaction();
        faction["leadership"]!["headActorId"] = "not_player_soul";
        await WriteNativeDiscoveryOutcomeAsync(faction);

        var issues = await _validator.ValidateAcceptedTurnRawFactionMaterializationAsync();

        Assert.Contains(issues, issue =>
            issue.Code ==
            "faction_materialization_shining_leadership_reference_invalid");
    }

    [Fact]
    public async Task ShiningHeadActor_VacantExceptionRequiresNullPair()
    {
        var faction = BuildCompleteNativeShiningFaction();
        faction["leadership"]!["leadershipState"] =
            ShiningAbodeState.LeadershipStateVacant;
        faction["leadership"]!["headActorType"] =
            ShiningAbodeState.HeadActorTypePlayerSoul;
        faction["leadership"]!["headActorId"] =
            ShiningAbodeState.HeadActorTypePlayerSoul;
        faction["materialization"]!["capabilities"]!["canTrade"] = false;
        await WriteNativeDiscoveryOutcomeAsync(faction);

        var issues = await _validator.ValidateAcceptedTurnRawFactionMaterializationAsync();

        Assert.Contains(issues, issue =>
            issue.Code ==
                "faction_materialization_shining_leadership_invalid" &&
            issue.Actor ==
                "shining_faction:shine_faction_dawn_archive");
    }

    [Fact]
    public async Task NativeDiscovery_CompleteMaterialization_Passes()
    {
        await WriteCompleteNativeDiscoveryAsync(
            residentCount: 2,
            completedProjectCount: 2);

        var issues = await ValidateNativeDiscoveryRouteAsync();

        Assert.DoesNotContain(issues, issue =>
            issue.Severity == IssueSeverity.Error &&
            issue.Actor == "shining_faction:shine_faction_native");
    }

    [Theory]
    [MemberData(nameof(InvalidNativeDiscoveryRoutes))]
    public async Task NativeDiscovery_InvalidRouteMutation_Fails(
        string mutation,
        string expectedCode)
    {
        await WriteCompleteNativeDiscoveryAsync(
            residentCount: 2,
            completedProjectCount: 2,
            mutation);

        var issues = await ValidateNativeDiscoveryRouteAsync();

        Assert.Contains(issues, issue =>
            string.Equals(issue.Code, expectedCode, StringComparison.Ordinal));
    }

    [Fact]
    public async Task PlayerFounding_CompleteMaterialization_Passes()
    {
        await WriteCompletePlayerFoundingAsync();

        var issues = await ValidatePlayerFoundingRouteAsync();

        Assert.DoesNotContain(issues, issue =>
            issue.Severity == IssueSeverity.Error &&
            issue.Actor == "shining_faction:shine_faction_player");
    }

    [Theory]
    [MemberData(nameof(InvalidPlayerFoundingRoutes))]
    public async Task PlayerFounding_InvalidRouteMutation_Fails(
        string mutation,
        string expectedCode)
    {
        await WriteCompletePlayerFoundingAsync(mutation);

        var issues = await ValidatePlayerFoundingRouteAsync();

        Assert.Contains(issues, issue =>
            string.Equals(issue.Code, expectedCode, StringComparison.Ordinal));
    }

    [Theory]
    [MemberData(nameof(MissingMortalSemantics))]
    public async Task NewMortalFaction_MissingSemanticField_FailsRaw(
        string propertyName,
        string expectedCode)
    {
        var faction = BuildCompleteMinimalMortalCreation();
        faction.Remove(propertyName);
        await WriteMortalCreationAsync(faction);

        var issues = await _validator.ValidateAcceptedTurnRawFactionMaterializationAsync();

        Assert.Contains(issues, issue =>
            issue.Code == expectedCode &&
            issue.Actor == "mortal_faction:temp-faction-watch");
    }

    [Fact]
    public async Task NewMortalFaction_CurrentChronicleCannotReplaceRawCreationChronicle()
    {
        var faction = BuildCompleteMinimalMortalCreation();
        faction.Remove("scribeChronicle");
        var current = new JsonObject
        {
            ["factionDataChanges"] = new JsonArray(faction)
        };
        var currentChronicles = new JsonObject
        {
            ["entries"] = new JsonArray(new JsonObject
            {
                ["factionId"] = "temp-faction-watch",
                ["entry"] = "#12 - The Wayfarer Watch took responsibility for the western road."
            })
        };
        var preTurnChronicles = new JsonObject
        {
            ["entries"] = new JsonArray()
        };
        await WriteCurrentAndSnapshotAsync(
            (MortalPath, current.ToJsonString()),
            ("game_state/factions/faction_chronicles.json", currentChronicles.ToJsonString()),
            (MortalPath, MortalRoot().ToJsonString()),
            ("game_state/factions/faction_chronicles.json", preTurnChronicles.ToJsonString()));

        var issues = await _validator.ValidateAcceptedTurnRawFactionMaterializationAsync();

        Assert.Contains(issues, issue =>
            issue.Code == "faction_materialization_mortal_chronicle_missing" &&
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
    [MemberData(nameof(MortalPromotionHistoricalMutationSurfaces))]
    public async Task Validate_MortalPromotion_UncommandedHistoricalMutation_ReportsPreservationFailure(
        string surface)
    {
        await WriteMortalPromotionHistoryFixtureAsync(surface);

        var issues = await _validator
            .ValidateAcceptedTurnRawFactionMaterializationAsync();

        Assert.Contains(issues, issue =>
            issue.Code ==
                "faction_materialization_promotion_history_changed" &&
            issue.Actor == "mortal_faction:faction_watch");
    }

    [Fact]
    public async Task Validate_MortalPromotion_AddsMissingSemanticsAndReceiptWithoutRewritingHistory()
    {
        await WriteMortalPromotionHistoryFixtureAsync();

        var issues = await _validator
            .ValidateAcceptedTurnRawFactionMaterializationAsync();

        Assert.Empty(issues);
    }

    [Fact]
    public async Task Validate_MortalPromotion_AddsPopulatedRowsWhereNoHistoricalValueExisted()
    {
        await WriteMortalPromotionHistoryFixtureAsync(
            includeHistoricalGovernedRows: false);

        var issues = await _validator
            .ValidateAcceptedTurnRawFactionMaterializationAsync();

        Assert.Empty(issues);
    }

    [Fact]
    public async Task Validate_MortalPromotion_ChronicleAdditionDoesNotReplaceHistoricalEntry()
    {
        await WriteMortalPromotionHistoryFixtureAsync("chronicle");

        var issues = await _validator
            .ValidateAcceptedTurnRawFactionMaterializationAsync();

        Assert.DoesNotContain(issues, issue =>
            issue.Code ==
                "faction_materialization_promotion_history_changed");
    }

    [Fact]
    public async Task Validate_MortalPromotion_OmittedExternalRowsRemainMergePreserved()
    {
        await WriteMortalPromotionHistoryFixtureAsync(
            "omit_external_history");

        var issues = await _validator
            .ValidateAcceptedTurnRawFactionMaterializationAsync();

        Assert.DoesNotContain(issues, issue =>
            issue.Code ==
                "faction_materialization_promotion_history_changed");
    }

    [Theory]
    [InlineData("omitted")]
    [InlineData("empty")]
    [InlineData("whitespace")]
    public async Task MortalPromotion_MissingOrBlankImagePrompt_FailsRaw(
        string variation)
    {
        var faction = BuildCompleteMortalPromotion();
        switch (variation)
        {
            case "omitted":
                faction.Remove("image_prompt");
                break;
            case "empty":
                faction["image_prompt"] = string.Empty;
                break;
            case "whitespace":
                faction["image_prompt"] = "   ";
                break;
            default:
                throw new ArgumentOutOfRangeException(
                    nameof(variation),
                    variation,
                    "Unsupported image prompt variation.");
        }
        await WriteMortalPromotionAsync(faction);

        var issues = await _validator.ValidateAcceptedTurnRawFactionMaterializationAsync();

        Assert.Contains(issues, issue =>
            issue.Code == "faction_materialization_mortal_image_prompt_missing" &&
            issue.Actor == "mortal_faction:faction_watch" &&
            issue.FilePath ==
                $"{MortalPath}.factionDataChanges[0].image_prompt");
    }

    [Theory]
    [InlineData(false, "non_english")]
    [InlineData(false, "overlong")]
    [InlineData(true, "non_english")]
    [InlineData(true, "overlong")]
    public async Task MortalMaterialization_InvalidImagePrompt_FailsRaw(
        bool promotion,
        string variation)
    {
        var faction = promotion
            ? BuildCompleteMortalPromotion()
            : BuildCompleteMinimalMortalCreation();
        faction["image_prompt"] = variation switch
        {
            "non_english" => "дозорные у старой башни",
            "overlong" => new string('a', 151),
            _ => throw new ArgumentOutOfRangeException(
                nameof(variation),
                variation,
                "Unsupported image prompt variation.")
        };
        if (promotion)
            await WriteMortalPromotionAsync(faction);
        else
            await WriteMortalCreationAsync(faction);

        var issues = await _validator.ValidateAcceptedTurnRawFactionMaterializationAsync();
        var expectedFactionId = promotion
            ? "faction_watch"
            : "temp-faction-watch";

        Assert.Contains(issues, issue =>
            issue.Code == "faction_materialization_mortal_image_prompt_invalid" &&
            issue.Actor == $"mortal_faction:{expectedFactionId}" &&
            issue.FilePath ==
                $"{MortalPath}.factionDataChanges[0].image_prompt");
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

    [Theory]
    [InlineData("omitted", "faction_materialization_mortal_image_prompt_missing")]
    [InlineData("non_english", "faction_materialization_mortal_image_prompt_invalid")]
    [InlineData("overlong", "faction_materialization_mortal_image_prompt_invalid")]
    public async Task CanonicalMaterializedMortal_MissingOrInvalidImagePrompt_FailsCompleteness(
        string variation,
        string expectedCode)
    {
        await WriteCanonicalMinimalMortalCreationAsync(
            imagePromptVariation: variation);

        var issues = await _validator.ValidateGameStateAsync(
            GameStateValidationPhase.AcceptedTurnFactionMaterializationCompleteness);

        Assert.Contains(issues, issue =>
            issue.Code == expectedCode &&
            issue.Actor == "mortal_faction:temp-faction-watch" &&
            issue.FilePath == $"{MortalPath}.factions[0].image_prompt");
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

    private async Task WriteMortalPromotionAsync(JsonObject faction)
    {
        var current = MortalRoot();
        current["factionDataChanges"] = new JsonArray(faction);
        var legacy = LegacyMortalFaction("faction_watch");
        legacy["name"] = faction["name"]?.DeepClone();
        await WriteCurrentAndSnapshotAsync(
            (MortalPath, current.ToJsonString()),
            (MortalPath, MortalRoot(
                legacy).ToJsonString()));
    }

    private async Task WriteMortalPromotionHistoryFixtureAsync(
        string? mutationSurface = null,
        bool includeHistoricalGovernedRows = true)
    {
        const string factionId = "faction_watch";
        var promotion = BuildCompleteMortalCreation();
        promotion["factionId"] = factionId;
        promotion.Remove("initialId");
        promotion.Remove("isNewFaction");
        promotion["relations"]![0]!["targetFactionId"] =
            "faction_allies";
        promotion["controlledTerritories"] = new JsonArray(
            new JsonObject
            {
                ["locationId"] = "location_watch_road",
                ["locationName"] = "Western Road"
            },
            new JsonObject
            {
                ["locationId"] = "location_watch_crossing",
                ["locationName"] = "Old Crossing"
            });
        promotion["scribeChronicle"] = new JsonArray();
        promotion["materialization"] =
            BuildPopulatedMortalEnvelope(
                factionId,
                "fmat_watch_promotion_history");

        var legacy = new JsonObject
        {
            ["factionId"] = factionId,
            ["name"] = promotion["name"]!.DeepClone(),
            ["description"] = promotion["description"]!.DeepClone(),
            ["purpose"] = promotion["purpose"]!.DeepClone(),
            ["level"] = promotion["level"]!.DeepClone(),
            ["experience"] = promotion["experience"]!.DeepClone(),
            ["experienceForNextLevel"] =
                promotion["experienceForNextLevel"]!.DeepClone(),
            ["developmentArchetype"] =
                promotion["developmentArchetype"]!.DeepClone(),
            ["powerProfile"] = promotion["powerProfile"]!.DeepClone()
        };
        if (includeHistoricalGovernedRows)
        {
            legacy["relations"] = promotion["relations"]!.DeepClone();
            legacy["controlledTerritories"] =
                promotion["controlledTerritories"]!.DeepClone();
        }
        var preTurnCore = MortalRoot(
            legacy,
            new JsonObject
            {
                ["factionId"] = "faction_allies",
                ["name"] = "Road Allies"
            });

        var preTurnStructure = new JsonObject
        {
            ["entries"] = new JsonArray(new JsonObject
            {
                ["factionId"] = factionId,
                ["factionName"] = promotion["name"]!.DeepClone(),
                ["name"] = promotion["name"]!.DeepClone(),
                ["governance"] = promotion["governance"]!.DeepClone(),
                ["leadership"] = promotion["leadership"]!.DeepClone()
            })
        };
        if (includeHistoricalGovernedRows)
        {
            preTurnStructure["entries"]![0]!["ranks"] =
                promotion["ranks"]!.DeepClone();
            preTurnStructure["entries"]![0]!["structuredBonuses"] =
                promotion["structuredBonuses"]!.DeepClone();
        }
        var promotionResources = promotion["resources"]!.AsObject();
        var preTurnResources = new JsonObject
        {
            ["entries"] = new JsonArray()
        };
        if (includeHistoricalGovernedRows)
        {
            preTurnResources["entries"]!.AsArray().Add(new JsonObject
            {
                ["factionId"] = factionId,
                ["factionName"] = promotion["name"]!.DeepClone(),
                ["name"] = promotion["name"]!.DeepClone(),
                ["metaResources"] =
                    promotionResources["metaResources"]!.DeepClone(),
                ["strategicGoods"] =
                    promotionResources["strategicGoods"]!.DeepClone()
            });
        }
        var historicalProject =
            promotion["activeProjects"]![0]!.DeepClone().AsObject();
        historicalProject["factionId"] = factionId;
        historicalProject["factionName"] =
            promotion["name"]!.DeepClone();
        var preTurnProjects = new JsonObject
        {
            ["activeProjects"] = includeHistoricalGovernedRows
                ? new JsonArray(historicalProject)
                : new JsonArray(),
            ["completedProjects"] = new JsonArray()
        };
        var preTurnCustom = new JsonObject
        {
            ["entries"] = new JsonArray()
        };
        if (includeHistoricalGovernedRows)
        {
            preTurnCustom["entries"]!.AsArray().Add(new JsonObject
            {
                ["factionId"] = factionId,
                ["factionName"] = promotion["name"]!.DeepClone(),
                ["name"] = promotion["name"]!.DeepClone(),
                ["customStates"] =
                    promotion["customStates"]!.DeepClone()
            });
        }
        var preTurnChronicles = new JsonObject
        {
            ["entries"] = new JsonArray(new JsonObject
            {
                ["factionId"] = factionId,
                ["factionName"] = promotion["name"]!.DeepClone(),
                ["entry"] =
                    "#7 - The old watch held the western road through winter."
            })
        };
        var preTurnCurrentLocation = new JsonObject
        {
            ["locationId"] = "location_watch_road",
            ["locationName"] = "Western Road",
            ["factionControl"] = includeHistoricalGovernedRows
                ? new JsonArray(new JsonObject
                {
                    ["factionId"] = factionId,
                    ["controlLevel"] = 35
                })
                : new JsonArray()
        };
        var preTurnWorldMap = new JsonObject
        {
            ["locations"] = new JsonArray(new JsonObject
            {
                ["locationId"] = "location_watch_crossing",
                ["locationName"] = "Old Crossing",
                ["factionControl"] = includeHistoricalGovernedRows
                    ? new JsonArray(new JsonObject
                    {
                        ["factionId"] = factionId,
                        ["controlLevel"] = 20
                    })
                    : new JsonArray()
            })
        };
        var preTurnNpcCore = new JsonObject
        {
            ["npcs"] = new JsonArray(new JsonObject
            {
                ["NPCId"] = "npc_watch_veteran",
                ["name"] = "Veteran Ilya",
                ["factionAffiliations"] = includeHistoricalGovernedRows
                    ? new JsonArray(new JsonObject
                    {
                        ["factionId"] = factionId,
                        ["role"] = "road_veteran"
                    })
                    : new JsonArray()
            })
        };

        var currentStructure = CloneJsonObject(preTurnStructure);
        var currentResources = CloneJsonObject(preTurnResources);
        var currentProjects = CloneJsonObject(preTurnProjects);
        var currentCustom = CloneJsonObject(preTurnCustom);
        var currentChronicles = CloneJsonObject(preTurnChronicles);
        var currentLocation = CloneJsonObject(preTurnCurrentLocation);
        var currentWorldMap = CloneJsonObject(preTurnWorldMap);
        var currentNpcCore = CloneJsonObject(preTurnNpcCore);
        if (!includeHistoricalGovernedRows)
        {
            currentLocation["factionControl"]!.AsArray().Add(
                new JsonObject
                {
                    ["factionId"] = factionId,
                    ["controlLevel"] = 35
                });
            currentWorldMap["locations"]![0]!["factionControl"]!
                .AsArray()
                .Add(new JsonObject
                {
                    ["factionId"] = factionId,
                    ["controlLevel"] = 20
                });
            currentNpcCore["npcs"]![0]!["factionAffiliations"]!
                .AsArray()
                .Add(new JsonObject
                {
                    ["factionId"] = factionId,
                    ["role"] = "road_veteran"
                });
        }

        switch (mutationSurface)
        {
            case null:
                break;
            case "core_profile":
                promotion["name"] = "Rewritten Wayfarer Watch";
                break;
            case "progression_power":
                promotion["powerProfile"]!["military"] = 9;
                break;
            case "governance":
                promotion["governance"]!["model"] =
                    "Unhistorical commandery";
                break;
            case "leadership":
                promotion["leadership"]!["summary"] =
                    "An unrelated captain now rules.";
                break;
            case "ranks":
                promotion["ranks"]!["branches"]![0]!["ranks"]![0]![
                    "name"] = "Rewritten Warden";
                break;
            case "resources":
                promotion["resources"]!["metaResources"]![0]!["amount"] =
                    99;
                break;
            case "relations":
                promotion["relations"]![0]!["status"] = "Hostile";
                break;
            case "projects":
                promotion["activeProjects"]![0]!["name"] =
                    "Replace the Historical Watchtower";
                break;
            case "custom_state":
                promotion["customStates"]![0]!["value"] = "discarded";
                break;
            case "chronicle":
                currentChronicles["entries"]![0]!["entry"] =
                    "#7 - Rewritten historical chronicle.";
                break;
            case "omit_external_history":
                currentStructure["entries"] = new JsonArray();
                currentResources["entries"] = new JsonArray();
                currentProjects["activeProjects"] = new JsonArray();
                currentProjects["completedProjects"] = new JsonArray();
                currentCustom["entries"] = new JsonArray();
                currentChronicles["entries"] = new JsonArray();
                currentLocation["factionControl"] = new JsonArray();
                currentWorldMap["locations"]![0]!["factionControl"] =
                    new JsonArray();
                currentNpcCore["npcs"]![0]!["factionAffiliations"] =
                    new JsonArray();
                break;
            case "current_location_faction_control":
                currentLocation["factionControl"]![0]!["controlLevel"] =
                    80;
                break;
            case "world_map_faction_control":
                currentWorldMap["locations"]![0]!["factionControl"]![0]![
                    "controlLevel"] = 75;
                break;
            case "npc_affiliation":
                currentNpcCore["npcs"]![0]!["factionAffiliations"]![0]![
                    "role"] = "rewritten_role";
                break;
            default:
                throw new ArgumentOutOfRangeException(
                    nameof(mutationSurface),
                    mutationSurface,
                    "Unsupported promotion-history mutation surface.");
        }

        var currentCore = MortalRoot();
        currentCore["factionDataChanges"] = new JsonArray(promotion);
        await WriteCurrentAndSnapshotAsync(
            (MortalPath, currentCore.ToJsonString()),
            (MortalStructurePath, currentStructure.ToJsonString()),
            (MortalResourcesPath, currentResources.ToJsonString()),
            (MortalProjectsPath, currentProjects.ToJsonString()),
            (MortalCustomPath, currentCustom.ToJsonString()),
            (MortalChroniclesPath, currentChronicles.ToJsonString()),
            (CurrentLocationPath, currentLocation.ToJsonString()),
            (WorldMapPath, currentWorldMap.ToJsonString()),
            (NpcCorePath, currentNpcCore.ToJsonString()),
            (MortalPath, preTurnCore.ToJsonString()),
            (MortalStructurePath, preTurnStructure.ToJsonString()),
            (MortalResourcesPath, preTurnResources.ToJsonString()),
            (MortalProjectsPath, preTurnProjects.ToJsonString()),
            (MortalCustomPath, preTurnCustom.ToJsonString()),
            (MortalChroniclesPath, preTurnChronicles.ToJsonString()),
            (CurrentLocationPath, preTurnCurrentLocation.ToJsonString()),
            (WorldMapPath, preTurnWorldMap.ToJsonString()),
            (NpcCorePath, preTurnNpcCore.ToJsonString()));
    }

    private async Task WriteLegacyMortalExternalTouchAsync(string channel)
    {
        const string factionId = "faction_watch";
        var preTurnCore = MortalRoot(LegacyMortalFaction(factionId));
        var currentCore = CloneJsonObject(preTurnCore);
        string path;
        JsonObject preTurnAuthority;
        JsonObject currentAuthority;

        switch (channel)
        {
            case "factionRankChanges":
                path = "game_state/factions/faction_structure.json";
                preTurnAuthority = new JsonObject
                {
                    ["entries"] = new JsonArray()
                };
                currentAuthority = CloneJsonObject(preTurnAuthority);
                currentAuthority[channel] = new JsonArray(new JsonObject
                {
                    ["factionId"] = factionId,
                    ["branchesToAdd"] = new JsonArray(new JsonObject
                    {
                        ["branchId"] = "road_scouts",
                        ["displayName"] = "Road Scouts",
                        ["ranks"] = new JsonArray()
                    })
                });
                break;
            case "factionBonusChanges":
                path = "game_state/factions/faction_structure.json";
                preTurnAuthority = new JsonObject
                {
                    ["entries"] = new JsonArray()
                };
                currentAuthority = CloneJsonObject(preTurnAuthority);
                currentAuthority[channel] = new JsonArray(new JsonObject
                {
                    ["factionId"] = factionId,
                    ["bonusesToAddOrUpdate"] = new JsonArray(new JsonObject
                    {
                        ["bonusId"] = null,
                        ["name"] = "Roadwise",
                        ["description"] = "Wardens read the old roads well."
                    })
                });
                break;
            case "factionResourceChanges":
                path = "game_state/factions/faction_resources.json";
                preTurnAuthority = new JsonObject
                {
                    ["entries"] = new JsonArray()
                };
                currentAuthority = CloneJsonObject(preTurnAuthority);
                currentAuthority[channel] = new JsonArray(new JsonObject
                {
                    ["factionId"] = factionId,
                    ["resourceChanges"] = new JsonArray(new JsonObject
                    {
                        ["resourceName"] = "timber",
                        ["changeAmount"] = 4
                    })
                });
                break;
            case "factionProjectUpdates":
                path = "game_state/factions/faction_projects.json";
                preTurnAuthority = new JsonObject
                {
                    ["activeProjects"] = new JsonArray(new JsonObject
                    {
                        ["factionId"] = factionId,
                        ["projectId"] = "project_watchtower",
                        ["projectName"] = "Raise the Watchtower",
                        ["activeState"] = "Active"
                    }),
                    ["completedProjects"] = new JsonArray()
                };
                currentAuthority = CloneJsonObject(preTurnAuthority);
                currentAuthority[channel] = new JsonArray(new JsonObject
                {
                    ["factionId"] = factionId,
                    ["projectUpdate"] = new JsonObject
                    {
                        ["projectId"] = "project_watchtower",
                        ["currentStep"] = 2
                    }
                });
                break;
            case "completeFactionProjects":
                path = "game_state/factions/faction_projects.json";
                preTurnAuthority = new JsonObject
                {
                    ["activeProjects"] = new JsonArray(new JsonObject
                    {
                        ["factionId"] = factionId,
                        ["projectId"] = "project_watchtower",
                        ["projectName"] = "Raise the Watchtower",
                        ["activeState"] = "Active"
                    }),
                    ["completedProjects"] = new JsonArray()
                };
                currentAuthority = CloneJsonObject(preTurnAuthority);
                currentAuthority[channel] = new JsonArray(new JsonObject
                {
                    ["factionId"] = factionId,
                    ["projectId"] = "project_watchtower",
                    ["projectName"] = "Raise the Watchtower",
                    ["finalState"] = "Completed"
                });
                break;
            case "factionCustomStateChanges":
                path = "game_state/factions/faction_custom.json";
                preTurnAuthority = new JsonObject
                {
                    ["entries"] = new JsonArray(new JsonObject
                    {
                        ["factionId"] = factionId,
                        ["customStates"] = new JsonArray(new JsonObject
                        {
                            ["stateId"] = "watch_priority",
                            ["name"] = "Watch Priority",
                            ["value"] = "bridge"
                        })
                    })
                };
                currentAuthority = CloneJsonObject(preTurnAuthority);
                currentAuthority[channel] = new JsonArray(new JsonObject
                {
                    ["factionId"] = factionId,
                    ["statesToRemove"] = new JsonArray("watch_priority")
                });
                break;
            case "factionChronicleUpdates":
                path = "game_state/factions/faction_chronicles.json";
                preTurnAuthority = new JsonObject
                {
                    ["entries"] = new JsonArray()
                };
                currentAuthority = CloneJsonObject(preTurnAuthority);
                currentAuthority[channel] = new JsonArray(new JsonObject
                {
                    ["factionId"] = factionId,
                    ["factionName"] = "Wayfarer Watch",
                    ["entryToAppend"] =
                        "#12 - The watch renewed its western-road patrol."
                });
                break;
            case "current_location_factionControl":
                path = "game_state/world/current_location.json";
                preTurnAuthority = new JsonObject
                {
                    ["locationId"] = "location_watch_road",
                    ["locationName"] = "Western Road",
                    ["factionControl"] = new JsonArray()
                };
                currentAuthority = CloneJsonObject(preTurnAuthority);
                ((JsonArray)currentAuthority["factionControl"]!).Add(
                    new JsonObject
                    {
                        ["factionId"] = factionId,
                        ["controlLevel"] = 35
                    });
                break;
            case "world_map_factionControl":
                path = "game_state/world/world_map.json";
                preTurnAuthority = new JsonObject
                {
                    ["locations"] = new JsonArray(new JsonObject
                    {
                        ["locationId"] = "location_watch_road",
                        ["locationName"] = "Western Road",
                        ["factionControl"] = new JsonArray()
                    })
                };
                currentAuthority = CloneJsonObject(preTurnAuthority);
                ((JsonArray)currentAuthority["locations"]![0]![
                    "factionControl"]!).Add(new JsonObject
                    {
                        ["factionId"] = factionId,
                        ["controlLevel"] = 35
                    });
                break;
            case "npc_factionAffiliations":
                path = "game_state/npcs/npc_core.json";
                preTurnAuthority = new JsonObject
                {
                    ["npcs"] = new JsonArray(new JsonObject
                    {
                        ["NPCId"] = "npc_watch_captain",
                        ["name"] = "Captain Mira",
                        ["factionAffiliations"] = new JsonArray()
                    })
                };
                currentAuthority = CloneJsonObject(preTurnAuthority);
                ((JsonArray)currentAuthority["npcs"]![0]![
                    "factionAffiliations"]!).Add(new JsonObject
                    {
                        ["factionId"] = factionId,
                        ["role"] = "captain"
                    });
                break;
            default:
                throw new ArgumentOutOfRangeException(
                    nameof(channel),
                    channel,
                    "Unsupported legacy Mortal external touch channel.");
        }

        await WriteCurrentAndSnapshotAsync(
            (MortalPath, currentCore.ToJsonString()),
            (path, currentAuthority.ToJsonString()),
            (MortalPath, preTurnCore.ToJsonString()),
            (path, preTurnAuthority.ToJsonString()));
    }

    private async Task WriteRawFactionTouchProjectionCaseAsync(
        string scenario)
    {
        const string targetMortalFactionId = "faction_target";
        const string unrelatedMortalFactionId = "faction_unrelated";
        const string oldShiningFactionId = "order_old";
        const string newShiningFactionId = "order_new";
        const string originShiningFactionId = "order_origin";
        const string unrelatedShiningFactionId = "order_unrelated";

        switch (scenario)
        {
            case "mortal_npc_affiliation_upsert":
            {
                var preTurnCore = BuildLegacyMortalTouchRoot(
                    targetMortalFactionId,
                    unrelatedMortalFactionId);
                var currentCore = CloneJsonObject(preTurnCore);
                var preTurnNpcs = new JsonObject
                {
                    [GuardianPolicyContracts
                        .NpcCoreUpdateSectionName] =
                        new JsonArray(new JsonObject
                        {
                            ["NPCId"] = "npc_affiliation_target",
                            ["name"] = "Affiliation Target",
                            ["factionAffiliations"] =
                                new JsonArray()
                        })
                };
                var currentNpcs = CloneJsonObject(preTurnNpcs);
                currentNpcs[NpcCoreChangesContract.PropertyName] =
                    new JsonArray(new JsonObject
                    {
                        ["NPCId"] = "npc_affiliation_target",
                        ["reason"] = "The target joined a permanent faction.",
                        ["factionAffiliationsToUpsert"] =
                            new JsonArray(new JsonObject
                            {
                                ["factionId"] = targetMortalFactionId,
                                ["factionName"] = targetMortalFactionId,
                                ["rank"] = "member",
                                ["branch"] = null,
                                ["membershipStatus"] = "Active"
                            })
                    });

                await WriteCurrentAndSnapshotAsync(
                    (MortalPath, currentCore.ToJsonString()),
                    (NpcCorePath, currentNpcs.ToJsonString()),
                    (MortalPath, preTurnCore.ToJsonString()),
                    (NpcCorePath, preTurnNpcs.ToJsonString()));
                return;
            }
            case "shining_resident_update_move":
            {
                var preTurnShining = BuildLegacyShiningTouchRoot(
                    oldShiningFactionId,
                    newShiningFactionId,
                    unrelatedShiningFactionId);
                var currentShining = CloneJsonObject(preTurnShining);
                var preTurnResidents = BuildRouteResidentRoot(
                    BuildRouteResident(
                        "resident_touch_target",
                        oldShiningFactionId));
                var currentResidents = new JsonObject
                {
                    [GuardianAbodeResidentState.UpdateProperty] =
                        new JsonArray(BuildRouteResident(
                            "resident_touch_target",
                            newShiningFactionId))
                };

                await WriteCurrentAndSnapshotAsync(
                    (ShiningPath, currentShining.ToJsonString()),
                    (ResidentPath, currentResidents.ToJsonString()),
                    (ShiningPath, preTurnShining.ToJsonString()),
                    (ResidentPath, preTurnResidents.ToJsonString()));
                return;
            }
            case "shining_saref_update_move":
            {
                var preTurnShining = BuildLegacyShiningTouchRoot(
                    oldShiningFactionId,
                    newShiningFactionId,
                    unrelatedShiningFactionId);
                var currentShining = CloneJsonObject(preTurnShining);
                var preTurnStory = SarefMainStoryState.CreateDefaultRoot();
                preTurnStory["factionLinks"]!["wingsFactionId"] =
                    oldShiningFactionId;
                var currentStory = new JsonObject
                {
                    [SarefMainStoryState.ResponseField] =
                        new JsonObject
                        {
                            ["mode"] =
                                SarefMainStoryState.WingsUpdateModeReveal,
                            ["requestId"] = "saref_touch_request",
                            ["resolvedAtTurn"] = 12,
                            ["factionLinks"] = new JsonObject
                            {
                                ["wingsFactionId"] =
                                    newShiningFactionId
                            }
                        }
                };

                await WriteCurrentAndSnapshotAsync(
                    (ShiningPath, currentShining.ToJsonString()),
                    (SarefStoryPath, currentStory.ToJsonString()),
                    (ShiningPath, preTurnShining.ToJsonString()),
                    (SarefStoryPath, preTurnStory.ToJsonString()));
                return;
            }
            case "shining_guardian_create":
            {
                const string guardianFactionId = "order_guardian";
                const string guardianId = "guardian_touch_target";
                var faction = LegacyShiningFaction(
                    guardianFactionId,
                    factionStrength: 30);
                faction["storyAuthority"] = new JsonObject
                {
                    ["authorityType"] = "guardian_ascension",
                    ["authorityId"] = guardianId
                };
                var preTurnShining = ShiningRoot(
                    faction,
                    LegacyShiningFaction(
                        unrelatedShiningFactionId,
                        factionStrength: 30));
                var currentShining = CloneJsonObject(preTurnShining);
                var preTurnGuardians = new JsonObject
                {
                    ["guardians"] = new JsonArray()
                };
                var currentGuardians = new JsonObject
                {
                    ["UpdateGuardians"] =
                        new JsonArray(new JsonObject
                        {
                            ["command"] = "create",
                            ["data"] = new JsonObject
                            {
                                ["guardianId"] = guardianId,
                                ["name"] = "Touch Guardian"
                            }
                        })
                };

                await WriteCurrentAndSnapshotAsync(
                    (ShiningPath, currentShining.ToJsonString()),
                    (GuardiansPath, currentGuardians.ToJsonString()),
                    (ShiningPath, preTurnShining.ToJsonString()),
                    (GuardiansPath, preTurnGuardians.ToJsonString()));
                return;
            }
            case "shining_political_current_move":
            {
                var preTurnShining = BuildLegacyShiningTouchRoot(
                    oldShiningFactionId,
                    newShiningFactionId,
                    originShiningFactionId,
                    unrelatedShiningFactionId);
                preTurnShining["shiningPoliticalActors"] =
                    new JsonArray(new JsonObject
                    {
                        ["actorId"] = "political_touch_target",
                        ["originFactionId"] = originShiningFactionId,
                        ["currentFactionId"] = oldShiningFactionId
                    });
                var currentShining = BuildLegacyShiningTouchRoot(
                    oldShiningFactionId,
                    newShiningFactionId,
                    originShiningFactionId,
                    unrelatedShiningFactionId);
                currentShining["shiningPoliticalActors"] =
                    new JsonArray(new JsonObject
                    {
                        ["actorId"] = "political_touch_target",
                        ["originFactionId"] = originShiningFactionId,
                        ["currentFactionId"] = newShiningFactionId
                    });

                await WriteCurrentAndSnapshotAsync(
                    (ShiningPath, currentShining.ToJsonString()),
                    (ShiningPath, preTurnShining.ToJsonString()));
                return;
            }
            case "shining_identity_upsert_omission":
            {
                var preTurnShining = BuildLegacyShiningTouchRoot(
                    oldShiningFactionId,
                    unrelatedShiningFactionId);
                preTurnShining["shiningPoliticalActors"] =
                    new JsonArray(new JsonObject
                    {
                        ["actorId"] = "omitted_actor",
                        ["originFactionId"] = oldShiningFactionId,
                        ["currentFactionId"] = oldShiningFactionId
                    });
                preTurnShining["coreActionReceipts"] =
                    new JsonArray(new JsonObject
                    {
                        ["requestId"] = "omitted_core_receipt",
                        ["factionId"] = oldShiningFactionId,
                        ["resolvedFactionId"] = oldShiningFactionId,
                        ["targetFactionId"] = oldShiningFactionId
                    });
                preTurnShining["factionFoundingReceipts"] =
                    new JsonArray(new JsonObject
                    {
                        ["requestId"] = "omitted_founding_receipt",
                        ["factionId"] = oldShiningFactionId,
                        ["proposedFactionId"] = oldShiningFactionId
                    });
                preTurnShining["factionRealignmentReceipts"] =
                    new JsonArray(new JsonObject
                    {
                        ["requestId"] = "omitted_realign_receipt",
                        ["sourceFactionId"] = oldShiningFactionId,
                        ["targetFactionId"] = oldShiningFactionId
                    });
                var currentShining = BuildLegacyShiningTouchRoot(
                    oldShiningFactionId,
                    unrelatedShiningFactionId);
                currentShining["shiningPoliticalActors"] =
                    new JsonArray(new JsonObject
                    {
                        ["actorId"] = "omitted_actor",
                        ["displayName"] = "Link Fields Omitted"
                    });
                currentShining["coreActionReceipts"] =
                    new JsonArray(new JsonObject
                    {
                        ["requestId"] = "omitted_core_receipt",
                        ["status"] = "acknowledged"
                    });
                currentShining["factionFoundingReceipts"] =
                    new JsonArray(new JsonObject
                    {
                        ["requestId"] = "omitted_founding_receipt",
                        ["status"] = "acknowledged"
                    });
                currentShining["factionRealignmentReceipts"] =
                    new JsonArray(new JsonObject
                    {
                        ["requestId"] = "omitted_realign_receipt",
                        ["status"] = "acknowledged"
                    });

                await WriteCurrentAndSnapshotAsync(
                    (ShiningPath, currentShining.ToJsonString()),
                    (ShiningPath, preTurnShining.ToJsonString()));
                return;
            }
            case "shining_same_identity_last_write_exact_case":
            {
                const string caseVariantFactionId = "ORDER_NEW";
                const string actorId = "folded_actor";
                var preTurnShining = BuildLegacyShiningTouchRoot(
                    oldShiningFactionId,
                    newShiningFactionId,
                    caseVariantFactionId,
                    unrelatedShiningFactionId);
                preTurnShining["shiningPoliticalActors"] =
                    new JsonArray(new JsonObject
                    {
                        ["actorId"] = actorId,
                        ["originFactionId"] = unrelatedShiningFactionId,
                        ["currentFactionId"] = oldShiningFactionId
                    });
                var currentShining = BuildLegacyShiningTouchRoot(
                    oldShiningFactionId,
                    newShiningFactionId,
                    caseVariantFactionId,
                    unrelatedShiningFactionId);
                currentShining["shiningPoliticalActors"] =
                    new JsonArray(
                        new JsonObject
                        {
                            ["actorId"] = actorId,
                            ["currentFactionId"] = newShiningFactionId
                        },
                        new JsonObject
                        {
                            ["actorId"] = actorId,
                            ["currentFactionId"] = caseVariantFactionId
                        });

                await WriteCurrentAndSnapshotAsync(
                    (ShiningPath, currentShining.ToJsonString()),
                    (ShiningPath, preTurnShining.ToJsonString()));
                return;
            }
            case "shining_unrelated_guardian_mutation":
            {
                const string guardianFactionId = "order_guardian";
                const string guardianId = "guardian_touch_target";
                var faction = LegacyShiningFaction(
                    guardianFactionId,
                    factionStrength: 30);
                faction["storyAuthority"] = new JsonObject
                {
                    ["authorityType"] = "guardian_ascension",
                    ["authorityId"] = guardianId
                };
                var preTurnShining = ShiningRoot(
                    faction,
                    LegacyShiningFaction(
                        unrelatedShiningFactionId,
                        factionStrength: 30));
                var currentShining = CloneJsonObject(preTurnShining);
                var preTurnGuardian = new JsonObject
                {
                    ["guardianId"] = guardianId,
                    ["name"] = "Touch Guardian",
                    ["mood"] = "calm",
                    ["memoryFragments"] =
                        new JsonArray("first", "second")
                };
                var currentGuardian =
                    CloneJsonObject(preTurnGuardian);
                currentGuardian["mood"] = "watchful";
                currentGuardian["memoryFragments"] =
                    new JsonArray("second", "first");
                var preTurnGuardians = new JsonObject
                {
                    ["guardians"] = new JsonArray(preTurnGuardian)
                };
                var currentGuardians = new JsonObject
                {
                    ["guardians"] = new JsonArray(currentGuardian)
                };

                await WriteCurrentAndSnapshotAsync(
                    (ShiningPath, currentShining.ToJsonString()),
                    (GuardiansPath, currentGuardians.ToJsonString()),
                    (ShiningPath, preTurnShining.ToJsonString()),
                    (GuardiansPath, preTurnGuardians.ToJsonString()));
                return;
            }
            case "shining_nested_reorder":
            {
                var preTurnShining = BuildLegacyShiningTouchRoot(
                    oldShiningFactionId,
                    newShiningFactionId,
                    unrelatedShiningFactionId);
                preTurnShining["shiningPoliticalActors"] =
                    new JsonArray(
                        BuildPoliticalTouchRow(
                            "actor_first",
                            oldShiningFactionId,
                            "alpha",
                            "beta"),
                        BuildPoliticalTouchRow(
                            "actor_second",
                            newShiningFactionId,
                            "gamma",
                            "delta"));
                preTurnShining["coreActionReceipts"] =
                    new JsonArray(
                        BuildReceiptTouchRow(
                            "receipt_first",
                            oldShiningFactionId,
                            "first",
                            "second"),
                        BuildReceiptTouchRow(
                            "receipt_second",
                            newShiningFactionId,
                            "third",
                            "fourth"));
                var currentShining = BuildLegacyShiningTouchRoot(
                    oldShiningFactionId,
                    newShiningFactionId,
                    unrelatedShiningFactionId);
                currentShining["shiningPoliticalActors"] =
                    new JsonArray(
                        BuildPoliticalTouchRow(
                            "actor_second",
                            newShiningFactionId,
                            "delta",
                            "gamma"),
                        BuildPoliticalTouchRow(
                            "actor_first",
                            oldShiningFactionId,
                            "beta",
                            "alpha"));
                currentShining["coreActionReceipts"] =
                    new JsonArray(
                        BuildReceiptTouchRow(
                            "receipt_second",
                            newShiningFactionId,
                            "fourth",
                            "third"),
                        BuildReceiptTouchRow(
                            "receipt_first",
                            oldShiningFactionId,
                            "second",
                            "first"));

                await WriteCurrentAndSnapshotAsync(
                    (ShiningPath, currentShining.ToJsonString()),
                    (ShiningPath, preTurnShining.ToJsonString()));
                return;
            }
            case "mortal_structure_nested_reorder":
            case "mortal_nested_duplicate_addition":
            case "mortal_nested_duplicate_removal":
            case "mortal_ordered_benefits_reorder":
            {
                var preTurnCore = BuildLegacyMortalTouchRoot(
                    targetMortalFactionId,
                    unrelatedMortalFactionId);
                var currentCore = CloneJsonObject(preTurnCore);
                var preTurnStructure =
                    BuildMortalStructureTouchRoot(
                        targetMortalFactionId);
                var currentStructure =
                    CloneJsonObject(preTurnStructure);
                var currentEntry =
                    currentStructure["entries"]![0]!.AsObject();

                if (scenario ==
                    "mortal_structure_nested_reorder")
                {
                    currentEntry["leadership"]![
                        "leaderNpcIds"] =
                        ReversedJsonArray(
                            currentEntry["leadership"]![
                                "leaderNpcIds"]!.AsArray());
                    currentEntry["ranks"]!["branches"] =
                        ReversedJsonArray(
                            currentEntry["ranks"]![
                                "branches"]!.AsArray());
                    foreach (var branch in currentEntry["ranks"]![
                                 "branches"]!.AsArray()
                                 .OfType<JsonObject>())
                    {
                        branch["ranks"] = ReversedJsonArray(
                            branch["ranks"]!.AsArray());
                        foreach (var rank in branch["ranks"]!
                                     .AsArray()
                                     .OfType<JsonObject>())
                        {
                            rank["availableBranches"] =
                                ReversedJsonArray(
                                    rank["availableBranches"]!
                                        .AsArray());
                        }
                    }

                    currentEntry["structuredBonuses"] =
                        ReversedJsonArray(
                            currentEntry["structuredBonuses"]!
                                .AsArray());
                }
                else if (scenario ==
                         "mortal_nested_duplicate_addition")
                {
                    var branches = currentEntry["ranks"]![
                        "branches"]!.AsArray();
                    branches.Add(branches[0]!.DeepClone());
                }
                else if (scenario ==
                         "mortal_nested_duplicate_removal")
                {
                    var preTurnBranches =
                        preTurnStructure["entries"]![0]!["ranks"]![
                            "branches"]!.AsArray();
                    preTurnBranches.Add(
                        preTurnBranches[0]!.DeepClone());
                }
                else
                {
                    var benefits = currentEntry["ranks"]![
                        "branches"]![0]!["ranks"]![0]![
                        "benefits"]!.AsArray();
                    currentEntry["ranks"]!["branches"]![0]![
                        "ranks"]![0]!["benefits"] =
                        ReversedJsonArray(benefits);
                }

                await WriteCurrentAndSnapshotAsync(
                    (MortalPath, currentCore.ToJsonString()),
                    (MortalStructurePath,
                        currentStructure.ToJsonString()),
                    (MortalPath, preTurnCore.ToJsonString()),
                    (MortalStructurePath,
                        preTurnStructure.ToJsonString()));
                return;
            }
            case "mortal_resources_nested_reorder":
            case "mortal_same_identity_semantic_change":
            {
                var preTurnCore = BuildLegacyMortalTouchRoot(
                    targetMortalFactionId,
                    unrelatedMortalFactionId);
                var currentCore = CloneJsonObject(preTurnCore);
                var preTurnResources =
                    BuildMortalResourcesTouchRoot(
                        targetMortalFactionId);
                var currentResources =
                    CloneJsonObject(preTurnResources);
                var currentEntry =
                    currentResources["entries"]![0]!.AsObject();
                if (scenario ==
                    "mortal_resources_nested_reorder")
                {
                    currentEntry["metaResources"] =
                        ReversedJsonArray(
                            currentEntry["metaResources"]!
                                .AsArray());
                    currentEntry["strategicGoods"] =
                        ReversedJsonArray(
                            currentEntry["strategicGoods"]!
                                .AsArray());
                }
                else
                {
                    currentEntry["metaResources"]![0]![
                        "currentStockpile"] = 99;
                }

                await WriteCurrentAndSnapshotAsync(
                    (MortalPath, currentCore.ToJsonString()),
                    (MortalResourcesPath,
                        currentResources.ToJsonString()),
                    (MortalPath, preTurnCore.ToJsonString()),
                    (MortalResourcesPath,
                        preTurnResources.ToJsonString()));
                return;
            }
            case "mortal_custom_nested_reorder":
            {
                var preTurnCore = BuildLegacyMortalTouchRoot(
                    targetMortalFactionId,
                    unrelatedMortalFactionId);
                var currentCore = CloneJsonObject(preTurnCore);
                var preTurnCustom =
                    BuildMortalCustomTouchRoot(
                        targetMortalFactionId);
                var currentCustom =
                    CloneJsonObject(preTurnCustom);
                var currentEntry =
                    currentCustom["entries"]![0]!.AsObject();
                currentEntry["customStates"] =
                    ReversedJsonArray(
                        currentEntry["customStates"]!.AsArray());

                await WriteCurrentAndSnapshotAsync(
                    (MortalPath, currentCore.ToJsonString()),
                    (MortalCustomPath,
                        currentCustom.ToJsonString()),
                    (MortalPath, preTurnCore.ToJsonString()),
                    (MortalCustomPath,
                        preTurnCustom.ToJsonString()));
                return;
            }
            case "mortal_project_cost_nested_reorder":
            {
                var preTurnCore = BuildLegacyMortalTouchRoot(
                    targetMortalFactionId,
                    unrelatedMortalFactionId);
                var currentCore = CloneJsonObject(preTurnCore);
                var preTurnProjects =
                    BuildMortalProjectsTouchRoot(
                        targetMortalFactionId);
                var currentProjects =
                    CloneJsonObject(preTurnProjects);
                var currentProject =
                    currentProjects["activeProjects"]![0]!
                        .AsObject();
                currentProject["totalResourceCost"] =
                    ReversedJsonArray(
                        currentProject["totalResourceCost"]!
                            .AsArray());
                currentProject["resourcesSpent"] =
                    ReversedJsonArray(
                        currentProject["resourcesSpent"]!
                            .AsArray());

                await WriteCurrentAndSnapshotAsync(
                    (MortalPath, currentCore.ToJsonString()),
                    (MortalProjectsPath,
                        currentProjects.ToJsonString()),
                    (MortalPath, preTurnCore.ToJsonString()),
                    (MortalProjectsPath,
                        preTurnProjects.ToJsonString()));
                return;
            }
            case "shining_long_receipt_history_reorder":
            {
                var preTurnShining = BuildLegacyShiningTouchRoot(
                    oldShiningFactionId,
                    unrelatedShiningFactionId);
                var receipts = new JsonArray();
                for (var index = 0; index < 1024; index++)
                {
                    receipts.Add(new JsonObject
                    {
                        ["requestId"] =
                            $"history_receipt_{index:D4}",
                        ["factionId"] = oldShiningFactionId,
                        ["auditTrail"] =
                            new JsonArray(
                                $"step_{index}_first",
                                $"step_{index}_second")
                    });
                }

                preTurnShining["coreActionReceipts"] = receipts;
                var currentShining =
                    BuildLegacyShiningTouchRoot(
                        oldShiningFactionId,
                        unrelatedShiningFactionId);
                currentShining["coreActionReceipts"] =
                    ReversedJsonArray(receipts);

                await WriteCurrentAndSnapshotAsync(
                    (ShiningPath, currentShining.ToJsonString()),
                    (ShiningPath, preTurnShining.ToJsonString()));
                return;
            }
            default:
                throw new ArgumentOutOfRangeException(
                    nameof(scenario),
                    scenario,
                    "Unsupported raw faction-touch projection scenario.");
        }
    }

    private static JsonObject BuildLegacyMortalTouchRoot(
        params string[] factionIds) =>
        MortalRoot(
            factionIds
                .Select(LegacyMortalFaction)
                .ToArray());

    private static JsonObject BuildLegacyShiningTouchRoot(
        params string[] factionIds) =>
        ShiningRoot(
            factionIds
                .Select(factionId =>
                    LegacyShiningFaction(
                        factionId,
                        factionStrength: 30))
                .ToArray());

    private static JsonObject BuildPoliticalTouchRow(
        string actorId,
        string factionId,
        string firstTag,
        string secondTag) =>
        new()
        {
            ["actorId"] = actorId,
            ["originFactionId"] = factionId,
            ["currentFactionId"] = factionId,
            ["tags"] = new JsonArray(firstTag, secondTag)
        };

    private static JsonObject BuildReceiptTouchRow(
        string requestId,
        string factionId,
        string firstAuditEntry,
        string secondAuditEntry) =>
        new()
        {
            ["requestId"] = requestId,
            ["factionId"] = factionId,
            ["auditTrail"] =
                new JsonArray(firstAuditEntry, secondAuditEntry)
        };

    private static JsonObject BuildMortalStructureTouchRoot(
        string factionId) =>
        new()
        {
            ["entries"] = new JsonArray(new JsonObject
            {
                ["factionId"] = factionId,
                ["leadership"] = new JsonObject
                {
                    ["leaderNpcIds"] =
                        new JsonArray("npc_first", "npc_second")
                },
                ["ranks"] = new JsonObject
                {
                    ["branches"] = new JsonArray(
                        BuildMortalBranchTouchRow(
                            "branch_first",
                            "branch_second"),
                        BuildMortalBranchTouchRow(
                            "branch_second",
                            "branch_first"))
                },
                ["structuredBonuses"] =
                    new JsonArray(
                        new JsonObject
                        {
                            ["bonusId"] = "bonus_first",
                            ["description"] = "First bonus",
                            ["bonusType"] = "reputation",
                            ["target"] = "roads"
                        },
                        new JsonObject
                        {
                            ["bonusId"] = "bonus_second",
                            ["description"] = "Second bonus",
                            ["bonusType"] = "reputation",
                            ["target"] = "bridges"
                        })
            })
        };

    private static JsonObject BuildMortalBranchTouchRow(
        string branchId,
        string otherBranchId) =>
        new()
        {
            ["branchId"] = branchId,
            ["displayName"] = branchId,
            ["ranks"] = new JsonArray(
                new JsonObject
                {
                    ["rankNameMale"] = $"{branchId}_senior",
                    ["rankNameFemale"] = $"{branchId}_senior_f",
                    ["name"] = $"{branchId}_senior",
                    ["benefits"] =
                        new JsonArray(
                            "first benefit",
                            "second benefit"),
                    ["availableBranches"] =
                        new JsonArray(branchId, otherBranchId)
                },
                new JsonObject
                {
                    ["rankNameMale"] = $"{branchId}_junior",
                    ["rankNameFemale"] = $"{branchId}_junior_f",
                    ["name"] = $"{branchId}_junior",
                    ["benefits"] =
                        new JsonArray(
                            "third benefit",
                            "fourth benefit"),
                    ["availableBranches"] =
                        new JsonArray(otherBranchId, branchId)
                })
        };

    private static JsonObject BuildMortalResourcesTouchRoot(
        string factionId) =>
        new()
        {
            ["entries"] = new JsonArray(new JsonObject
            {
                ["factionId"] = factionId,
                ["metaResources"] =
                    new JsonArray(
                        BuildMortalResourceTouchRow(
                            "Wealth",
                            includeUpkeep: true),
                        BuildMortalResourceTouchRow(
                            "Influence",
                            includeUpkeep: true)),
                ["strategicGoods"] =
                    new JsonArray(
                        BuildMortalResourceTouchRow(
                            "iron",
                            includeUpkeep: false),
                        BuildMortalResourceTouchRow(
                            "timber",
                            includeUpkeep: false))
            })
        };

    private static JsonObject BuildMortalResourceTouchRow(
        string resourceName,
        bool includeUpkeep)
    {
        var resource = new JsonObject
        {
            ["resourceName"] = resourceName,
            ["currentStockpile"] = 10,
            ["incomePerCycle"] = 2
        };
        if (includeUpkeep)
            resource["upkeepPerCycle"] = 1;
        return resource;
    }

    private static JsonObject BuildMortalCustomTouchRoot(
        string factionId) =>
        new()
        {
            ["entries"] = new JsonArray(new JsonObject
            {
                ["factionId"] = factionId,
                ["customStates"] =
                    new JsonArray(
                        BuildMortalCustomStateTouchRow(
                            "state_first"),
                        BuildMortalCustomStateTouchRow(
                            "state_second"))
            })
        };

    private static JsonObject BuildMortalCustomStateTouchRow(
        string stateId) =>
        new()
        {
            ["stateId"] = stateId,
            ["name"] = stateId,
            ["currentValue"] = 1,
            ["minValue"] = 0,
            ["maxValue"] = 10,
            ["description"] = $"State {stateId}",
            ["progressionRule"] = new JsonObject
            {
                ["changePerTurn"] = 1,
                ["description"] = "Changes once per turn."
            },
            ["thresholds"] = new JsonArray()
        };

    private static JsonObject BuildMortalProjectsTouchRoot(
        string factionId) =>
        new()
        {
            ["activeProjects"] = new JsonArray(new JsonObject
            {
                ["factionId"] = factionId,
                ["projectId"] = "project_touch",
                ["projectName"] = "Touch Project",
                ["activeState"] = "Active",
                ["description"] = "A stable project.",
                ["totalResourceCost"] =
                    new JsonArray(
                        new JsonObject
                        {
                            ["resourceName"] = "iron",
                            ["totalAmount"] = 10
                        },
                        new JsonObject
                        {
                            ["resourceName"] = "timber",
                            ["totalAmount"] = 20
                        }),
                ["resourcesSpent"] =
                    new JsonArray(
                        new JsonObject
                        {
                            ["resourceName"] = "iron",
                            ["amountSpent"] = 2
                        },
                        new JsonObject
                        {
                            ["resourceName"] = "timber",
                            ["amountSpent"] = 4
                        }),
                ["totalTimeCostMinutes"] = 120,
                ["timeSpentMinutes"] = 30,
                ["totalSteps"] = 4,
                ["currentStep"] = 1
            }),
            ["completedProjects"] = new JsonArray()
        };

    private static JsonArray ReversedJsonArray(JsonArray source) =>
        new(
            source
                .Reverse()
                .Select(node => node?.DeepClone())
                .ToArray());

    private async Task<IReadOnlyList<ValidationIssue>> ValidateNativeDiscoveryRouteAsync()
    {
        var issues = (await _validator
                .ValidateAcceptedTurnRawFactionMaterializationAsync())
            .ToList();
        await InvokeRouteValidationAsync(
            "ValidatePendingShiningCoreActionResolutionAsync",
            issues);
        return issues;
    }

    private async Task<IReadOnlyList<ValidationIssue>> ValidatePlayerFoundingRouteAsync()
    {
        var issues = (await _validator
                .ValidateAcceptedTurnRawFactionMaterializationAsync())
            .ToList();
        await InvokeRouteValidationAsync(
            "ValidatePendingShiningFoundingResolutionAsync",
            issues);
        return issues;
    }

    private async Task InvokeRouteValidationAsync(
        string methodName,
        List<ValidationIssue> issues)
    {
        var method = typeof(ValidationService).GetMethod(
            methodName,
            BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(method);
        var task = method!.Invoke(_validator, new object[] { issues }) as Task;
        Assert.NotNull(task);
        await task!;
    }

    private async Task WriteCompleteNativeDiscoveryAsync(
        int residentCount,
        int completedProjectCount,
        string? mutation = null)
    {
        const string requestId = "request_discover_native";
        const string nativeHallId = "hall_native";
        const string nativeFactionId = "shine_faction_native";
        const string existingHallId = "hall_existing";
        const string existingFactionId = "shine_faction_existing";
        const string existingResidentId = "resident_existing";
        const string existingProjectId = "project_existing";

        if (string.Equals(mutation, "resident_count_low", StringComparison.Ordinal))
            residentCount = 1;
        else if (string.Equals(mutation, "resident_count_high", StringComparison.Ordinal))
            residentCount = 5;

        if (string.Equals(mutation, "project_count", StringComparison.Ordinal))
            completedProjectCount = 1;

        var hallId = string.Equals(mutation, "reuse_hall", StringComparison.Ordinal)
            ? existingHallId
            : nativeHallId;
        var factionId = string.Equals(mutation, "reuse_faction", StringComparison.Ordinal)
            ? existingFactionId
            : nativeFactionId;
        var materializedHallId =
            string.Equals(
                mutation,
                "case_variant_hall_id",
                StringComparison.Ordinal)
                ? hallId.ToUpperInvariant()
                : hallId;
        var materializedFactionId =
            string.Equals(
                mutation,
                "case_variant_faction_id",
                StringComparison.Ordinal)
                ? factionId.ToUpperInvariant()
                : factionId;
        var residentIds = Enumerable.Range(1, residentCount)
            .Select(index => $"resident_native_{index}")
            .ToArray();
        if (string.Equals(mutation, "reuse_resident", StringComparison.Ordinal))
            residentIds[0] = existingResidentId;
        var projectIds = Enumerable.Range(1, completedProjectCount)
            .Select(index => $"project_native_{index}")
            .ToArray();
        if (string.Equals(mutation, "reuse_project", StringComparison.Ordinal))
            projectIds[0] = existingProjectId;

        var preTurnShining = BuildRouteShiningRoot();
        ((JsonArray)preTurnShining["halls"]!).Add(
            BuildShiningHall(existingHallId, "Existing Hall"));
        ((JsonArray)preTurnShining["factions"]!).Add(
            BuildLegacyRouteFaction(
                existingFactionId,
                existingHallId,
                existingProjectId));
        var currentShining = CloneJsonObject(preTurnShining);
        currentShining["radiance"]!["experience"] =
            preTurnShining["radiance"]!["experience"]!.GetValue<int>() + 20;
        currentShining["lightSparks"] =
            preTurnShining["lightSparks"]!.GetValue<int>() - 20;
        if (string.Equals(mutation, "wrong_cost", StringComparison.Ordinal))
            currentShining["lightSparks"] = preTurnShining["lightSparks"]!.GetValue<int>() - 19;

        if (!string.Equals(mutation, "reuse_hall", StringComparison.Ordinal))
        {
            ((JsonArray)currentShining["halls"]!).Add(
                BuildShiningHall(materializedHallId, "Native Hall"));
        }
        if (string.Equals(mutation, "duplicate_hall", StringComparison.Ordinal))
        {
            ((JsonArray)currentShining["halls"]!).Add(
                BuildShiningHall(nativeHallId, "Duplicate Native Hall"));
        }

        var projects = new JsonArray(
            projectIds.Select((projectId, index) =>
                (JsonNode?)BuildRouteProject(
                    projectId,
                    string.Equals(mutation, "project_not_completed", StringComparison.Ordinal) &&
                    index == 0
                        ? "active"
                        : ShiningAbodeState.ProjectStatusCompleted))
                .ToArray());
        if (string.Equals(
                mutation,
                "extra_unlisted_project",
                StringComparison.Ordinal))
        {
            projects.Add(
                BuildRouteProject(
                    "project_native_unlisted",
                    ShiningAbodeState.ProjectStatusCompleted));
        }
        var faction = BuildCompleteNativeRouteFaction(
            materializedFactionId,
            materializedHallId,
            requestId,
            residentIds[0],
            projects);
        var currentFactions = (JsonArray)currentShining["factions"]!;
        if (string.Equals(mutation, "reuse_faction", StringComparison.Ordinal))
            currentFactions.Clear();
        currentFactions.Add(faction);
        if (string.Equals(mutation, "extra_faction", StringComparison.Ordinal))
        {
            currentFactions.Add(new JsonObject
            {
                ["factionId"] = "shine_faction_unexpected",
                ["originType"] = ShiningAbodeState.OriginTypeNativeRadiant,
                ["hallId"] = materializedHallId
            });
        }

        var preTurnResidents = BuildRouteResidentRoot(
            BuildRouteResident(existingResidentId, existingFactionId));
        var currentResidents = CloneJsonObject(preTurnResidents);
        var currentResidentEntries = (JsonArray)currentResidents["entries"]!;
        foreach (var residentId in residentIds)
        {
            if (string.Equals(residentId, existingResidentId, StringComparison.Ordinal))
            {
                currentResidentEntries[0]!["shiningFactionId"] =
                    materializedFactionId;
                continue;
            }

            currentResidentEntries.Add(
                BuildRouteResident(
                    residentId,
                    materializedFactionId));
        }
        if (string.Equals(mutation, "unrelated_resident_rewrite", StringComparison.Ordinal))
            currentResidentEntries[0]!["displayName"] = "Rewritten Existing Resident";

        var preTurnSoul = BuildRouteSoulRoot(currentFeathers: 100);
        var currentSoul = CloneJsonObject(preTurnSoul);
        currentSoul["inkFeathers"]!["current"] = 75;
        var request = BuildNativeDiscoveryRequest(requestId);
        var receipt = BuildNativeDiscoveryReceipt(
            requestId,
            factionId,
            hallId,
            residentIds,
            projectIds);
        if (string.Equals(mutation, "wrong_receipt", StringComparison.Ordinal))
            receipt["actionType"] = ShiningCoreActionRequestState.ActionTypeInvestInFaction;
        currentShining["coreActionReceipts"] = new JsonArray(receipt);

        var profiles = new JsonArray(
            residentIds
                .Distinct(StringComparer.Ordinal)
                .Select(residentId => (JsonNode?)BuildRouteAfterlifeProfile(
                    residentId,
                    includeEnvelope:
                        !string.Equals(mutation, "missing_actor_envelope", StringComparison.Ordinal) ||
                        !string.Equals(residentId, residentIds[0], StringComparison.Ordinal),
                    canTrade:
                        string.Equals(
                            residentId,
                            residentIds[0],
                            StringComparison.Ordinal)))
                .ToArray());
        var profileRoot = new JsonObject
        {
            ["schemaVersion"] = 1,
            [AfterlifeEntityProfileState.ProfilesProperty] = profiles
        };
        var requestRoot = new JsonObject
        {
            [ShiningCoreActionRequestState.RequestsProperty] =
                new JsonArray(request)
        };

        await WriteCurrentAndSnapshotAsync(
            (ShiningPath, currentShining.ToJsonString()),
            (ResidentPath, currentResidents.ToJsonString()),
            (SoulPath, currentSoul.ToJsonString()),
            (ShiningCoreActionRequestState.PendingActionsRequestPath, requestRoot.ToJsonString()),
            (AfterlifeProfilesPath, profileRoot.ToJsonString()),
            (ShiningPath, preTurnShining.ToJsonString()),
            (ResidentPath, preTurnResidents.ToJsonString()),
            (SoulPath, preTurnSoul.ToJsonString()),
            (ShiningCoreActionRequestState.PendingActionsRequestPath, requestRoot.ToJsonString()));
    }

    private async Task WriteCompletePlayerFoundingAsync(string? mutation = null)
    {
        const string requestId = "request_found_player";
        const string hallId = "hall_player";
        const string factionId = "shine_faction_player";
        var supporterIds = new[] { "resident_supporter_1", "resident_supporter_2" };
        var materializedHallId =
            string.Equals(
                mutation,
                "case_variant_hall_id",
                StringComparison.Ordinal)
                ? hallId.ToUpperInvariant()
                : hallId;
        var materializedFactionId =
            string.Equals(
                mutation,
                "case_variant_faction_id",
                StringComparison.Ordinal)
                ? factionId.ToUpperInvariant()
                : factionId;

        var request = BuildPlayerFoundingRequest(
            requestId,
            factionId,
            hallId,
            supporterIds);
        var preTurnShining = BuildRouteShiningRoot();
        var currentShining = CloneJsonObject(preTurnShining);
        var hall = BuildPlayerFoundingHall(materializedHallId);
        var faction = BuildCompletePlayerFoundedRouteFaction(
            materializedFactionId,
            materializedHallId,
            requestId);
        if (string.Equals(mutation, "request_id", StringComparison.Ordinal))
            faction["creationProvenance"]!["authorityId"] = "request_other";
        else if (string.Equals(mutation, "charter", StringComparison.Ordinal))
            faction["charter"]!["summary"] = "Rewritten charter.";
        else if (string.Equals(mutation, "player_soul", StringComparison.Ordinal))
            faction["leadership"]!["headActorId"] = "resident_supporter_1";
        else if (string.Equals(mutation, "missing_history", StringComparison.Ordinal))
            faction["leadershipHistory"] = new JsonArray();
        if (string.Equals(mutation, "hall", StringComparison.Ordinal))
            hall["description"] = "Rewritten hall description.";

        ((JsonArray)currentShining["halls"]!).Add(hall);
        ((JsonArray)currentShining["factions"]!).Add(faction);
        var receipt = BuildPlayerFoundingReceipt(
            requestId,
            factionId,
            hallId,
            supporterIds);
        if (string.Equals(mutation, "quoted_cost", StringComparison.Ordinal))
            receipt["quotedCostFeathers"] = 999;
        if (!string.Equals(mutation, "missing_root_receipt", StringComparison.Ordinal))
            currentShining["factionFoundingReceipts"] = new JsonArray(receipt);
        if (string.Equals(mutation, "reserved_light_sparks", StringComparison.Ordinal))
            currentShining["lightSparks"] = preTurnShining["lightSparks"]!.GetValue<int>() + 1;

        var preTurnResidents = BuildRouteResidentRoot(
            BuildRouteResident(supporterIds[0], "shine_faction_old"),
            BuildRouteResident(supporterIds[1], "shine_faction_old"),
            BuildRouteResident("resident_unrelated", "shine_faction_old"));
        var currentResidents = CloneJsonObject(preTurnResidents);
        var currentEntries = (JsonArray)currentResidents["entries"]!;
        foreach (var resident in currentEntries.OfType<JsonObject>())
        {
            var residentId = resident["residentId"]!.GetValue<string>();
            if (supporterIds.Contains(residentId, StringComparer.Ordinal))
                resident["shiningFactionId"] = materializedFactionId;
        }
        if (string.Equals(mutation, "supporters", StringComparison.Ordinal))
            currentEntries[0]!["shiningFactionId"] = "shine_faction_old";
        else if (string.Equals(mutation, "unrelated_resident_rewrite", StringComparison.Ordinal))
            currentEntries[2]!["shiningFactionId"] =
                materializedFactionId;

        var preTurnSoul = BuildRouteSoulRoot(currentFeathers: 75);
        var currentSoul = CloneJsonObject(preTurnSoul);
        if (string.Equals(mutation, "reserved_feathers", StringComparison.Ordinal))
            currentSoul["inkFeathers"]!["current"] = 76;
        var requestRoot = new JsonObject
        {
            [ShiningFactionRequestState.RequestsProperty] =
                new JsonArray(request)
        };
        var supporterProfiles = BuildAfterlifeProfileRoot(
            supporterIds.Select(residentId =>
                BuildRouteAfterlifeProfile(
                    residentId,
                    includeEnvelope: true)));

        await WriteCurrentAndSnapshotAsync(
            (ShiningPath, currentShining.ToJsonString()),
            (ResidentPath, currentResidents.ToJsonString()),
            (SoulPath, currentSoul.ToJsonString()),
            (ShiningFactionRequestState.PendingFoundingsRequestPath, requestRoot.ToJsonString()),
            (AfterlifeProfilesPath, supporterProfiles.ToJsonString()),
            (ShiningPath, preTurnShining.ToJsonString()),
            (ResidentPath, preTurnResidents.ToJsonString()),
            (SoulPath, preTurnSoul.ToJsonString()),
            (ShiningFactionRequestState.PendingFoundingsRequestPath, requestRoot.ToJsonString()));
    }

    private async Task WriteNativeDiscoveryOutcomeAsync(JsonObject faction)
    {
        var current = ShiningRoot(faction);
        current["halls"] = new JsonArray(
            BuildShiningHall(
                "hall_dawn_archive",
                "Dawn Archive"));
        await WriteCurrentAndSnapshotAsync(
            (ShiningPath, current.ToJsonString()),
            (ShiningPath, ShiningRoot().ToJsonString()));
    }

    private async Task WriteStoryFactionOutcomeAsync(
        JsonObject faction,
        JsonObject? sarefRoot = null,
        JsonObject? guardiansRoot = null,
        IEnumerable<JsonObject>? profiles = null)
    {
        var current = ShiningRoot(faction);
        current["halls"] = new JsonArray(
            BuildShiningHall(
                faction["hallId"]!.GetValue<string>(),
                "Story Hall"));
        current["shiningPoliticalActors"] = new JsonArray();
        await WriteCurrentAndSnapshotAsync(
            (ShiningPath, current.ToJsonString()),
            (ResidentPath, BuildRouteResidentRoot().ToJsonString()),
            (AfterlifeProfilesPath,
                BuildAfterlifeProfileRoot(
                    profiles ?? Array.Empty<JsonObject>())
                    .ToJsonString()),
            (SarefStoryPath,
                (sarefRoot ?? new JsonObject()).ToJsonString()),
            (GuardiansPath,
                (guardiansRoot ?? BuildGuardianStoryRoot())
                    .ToJsonString()),
            (ShiningPath, ShiningRoot().ToJsonString()));
    }

    private async Task WriteRequiredShiningActorOutcomeAsync(
        string actorKind,
        bool includeProfile,
        bool includeEnvelope,
        string profileShape = "exact",
        int? derivedBaseStrength = null,
        int? submittedFactionStrength = null,
        bool? factionCanTrade = null,
        bool? actorCanTrade = null,
        int? projectTier = null,
        int? submittedProjectStrengthReward = null)
    {
        const string factionId = "shine_faction_dawn_archive";
        var actorId = $"required_{actorKind}";
        var faction = BuildCompleteNativeShiningFaction();
        if (derivedBaseStrength.HasValue)
            faction["baseStrength"] = derivedBaseStrength.Value;
        if (submittedFactionStrength.HasValue)
            faction["factionStrength"] = submittedFactionStrength.Value;
        if (factionCanTrade.HasValue)
        {
            faction["materialization"]!["capabilities"]!["canTrade"] =
                factionCanTrade.Value;
        }
        if (projectTier.HasValue != submittedProjectStrengthReward.HasValue)
        {
            throw new ArgumentException(
                "Project tier and submitted reward must be provided together.");
        }
        if (projectTier.HasValue)
        {
            AddCompletedShiningProject(
                faction,
                projectTier.Value,
                submittedProjectStrengthReward!.Value);
        }
        var residentRoot = BuildRouteResidentRoot();
        var current = ShiningRoot(faction);
        current["halls"] = new JsonArray(
            BuildShiningHall(
                "hall_dawn_archive",
                "Dawn Archive"));
        current["shiningPoliticalActors"] = new JsonArray();

        string actorType;
        var canTrade = false;
        switch (actorKind)
        {
            case "head":
                actorType = ShiningAbodeState.HeadActorTypeResident;
                canTrade = true;
                faction["leadership"] = new JsonObject
                {
                    ["leadershipState"] =
                        ShiningAbodeState.LeadershipStateSecure,
                    ["headActorType"] = actorType,
                    ["headActorId"] = actorId
                };
                ((JsonArray)residentRoot["entries"]!).Add(
                    BuildRouteResident(actorId, factionId));
                MarkResidentAffiliationsPopulated(faction);
                break;
            case "resident":
                actorType = ShiningAbodeState.HeadActorTypeResident;
                ((JsonArray)residentRoot["entries"]!).Add(
                    BuildRouteResident(actorId, factionId));
                MarkResidentAffiliationsPopulated(faction);
                break;
            case "political":
                actorType = ShiningAbodeState.HeadActorTypeRadiantActor;
                ((JsonArray)current["shiningPoliticalActors"]!).Add(
                    new JsonObject
                    {
                        ["actorId"] = actorId,
                        ["actorType"] = actorType,
                        ["currentFactionId"] = factionId
                    });
                break;
            default:
                throw new ArgumentOutOfRangeException(
                    nameof(actorKind),
                    actorKind,
                    "Unsupported required actor test kind.");
        }

        var profiles = new List<JsonObject>();
        if (includeProfile)
        {
            JsonObject ExactProfile() =>
                BuildRouteAfterlifeProfile(
                    actorId,
                    includeEnvelope,
                    actorType,
                    actorCanTrade ?? canTrade);
            JsonObject DifferentTypeProfile() =>
                BuildRouteAfterlifeProfile(
                    actorId,
                    includeEnvelope: true,
                    actorType:
                        ShiningAbodeState.HeadActorTypeGuardian,
                    canTrade: false);

            switch (profileShape)
            {
                case "exact":
                    profiles.Add(ExactProfile());
                    break;
                case "exact_plus_different_type":
                    profiles.Add(ExactProfile());
                    profiles.Add(DifferentTypeProfile());
                    break;
                case "wrong_type_only":
                    profiles.Add(DifferentTypeProfile());
                    break;
                case "duplicate_exact_pair":
                    profiles.Add(ExactProfile());
                    profiles.Add(ExactProfile());
                    break;
                default:
                    throw new ArgumentOutOfRangeException(
                        nameof(profileShape),
                        profileShape,
                        "Unsupported required actor profile shape.");
            }
        }

        await WriteCurrentAndSnapshotAsync(
            (ShiningPath, current.ToJsonString()),
            (ResidentPath, residentRoot.ToJsonString()),
            (AfterlifeProfilesPath,
                BuildAfterlifeProfileRoot(profiles).ToJsonString()),
            (ShiningPath, ShiningRoot().ToJsonString()));
    }

    private async Task WriteRequiredShiningActorSourceOutcomeAsync(
        string mutation)
    {
        const string factionId = "shine_faction_dawn_archive";
        var faction = BuildCompleteNativeShiningFaction();
        var residentRoot = BuildRouteResidentRoot();
        var current = ShiningRoot(faction);
        var preTurnShining = ShiningRoot();
        current["halls"] = new JsonArray(
            BuildShiningHall(
                "hall_dawn_archive",
                "Dawn Archive"));
        current["shiningPoliticalActors"] = new JsonArray();

        string actorId;
        string actorType;
        switch (mutation)
        {
            case "missing_resident_id":
                actorId = "required_resident";
                actorType =
                    ShiningAbodeState.HeadActorTypeResident;
                var missingIdResident =
                    BuildRouteResident(actorId, factionId);
                missingIdResident.Remove("residentId");
                ((JsonArray)residentRoot["entries"]!).Add(
                    missingIdResident);
                break;
            case "duplicate_resident_id":
                actorId = "required_resident";
                actorType =
                    ShiningAbodeState.HeadActorTypeResident;
                ((JsonArray)residentRoot["entries"]!).Add(
                    BuildRouteResident(actorId, factionId));
                ((JsonArray)residentRoot["entries"]!).Add(
                    BuildRouteResident(actorId, factionId));
                MarkResidentAffiliationsPopulated(faction);
                break;
            case "duplicate_resident_id_across_factions":
                actorId = "required_resident";
                actorType =
                    ShiningAbodeState.HeadActorTypeResident;
                ((JsonArray)residentRoot["entries"]!).Add(
                    BuildRouteResident(actorId, factionId));
                ((JsonArray)residentRoot["entries"]!).Add(
                    BuildRouteResident(
                        actorId,
                        "shine_faction_other"));
                MarkResidentAffiliationsPopulated(faction);
                break;
            case "duplicate_political_identity":
                actorId = "required_political";
                actorType =
                    ShiningAbodeState.HeadActorTypeRadiantActor;
                JsonObject PoliticalActor() =>
                    new()
                    {
                        ["actorId"] = actorId,
                        ["actorType"] = actorType,
                        ["currentFactionId"] = factionId
                    };
                ((JsonArray)current["shiningPoliticalActors"]!).Add(
                    PoliticalActor());
                ((JsonArray)current["shiningPoliticalActors"]!).Add(
                    PoliticalActor());
                break;
            case "duplicate_political_identity_across_factions":
                actorId = "required_political";
                actorType =
                    ShiningAbodeState.HeadActorTypeRadiantActor;
                ((JsonArray)current["shiningPoliticalActors"]!).Add(
                    new JsonObject
                    {
                        ["actorId"] = actorId,
                        ["actorType"] = actorType,
                        ["currentFactionId"] = factionId
                    });
                ((JsonArray)current["shiningPoliticalActors"]!).Add(
                    new JsonObject
                    {
                        ["actorId"] = actorId,
                        ["actorType"] = actorType,
                        ["currentFactionId"] =
                            "shine_faction_other"
                    });
                break;
            default:
                throw new ArgumentOutOfRangeException(
                    nameof(mutation),
                    mutation,
                    "Unsupported required actor source mutation.");
        }

        if (mutation is
            "duplicate_resident_id_across_factions" or
            "duplicate_political_identity_across_factions")
        {
            ((JsonArray)current["factions"]!).Add(
                LegacyShiningFaction(
                    "shine_faction_other",
                    factionStrength: 30));
            ((JsonArray)preTurnShining["factions"]!).Add(
                LegacyShiningFaction(
                    "shine_faction_other",
                    factionStrength: 30));
        }

        var profiles = new[]
        {
            BuildRouteAfterlifeProfile(
                actorId,
                includeEnvelope: true,
                actorType: actorType,
                canTrade: false)
        };
        await WriteCurrentAndSnapshotAsync(
            (ShiningPath, current.ToJsonString()),
            (ResidentPath, residentRoot.ToJsonString()),
            (AfterlifeProfilesPath,
                BuildAfterlifeProfileRoot(profiles).ToJsonString()),
            (ShiningPath, preTurnShining.ToJsonString()));
    }

    private async Task WriteCanonicalMinimalMortalCreationAsync(
        string? missingSidecar = null,
        string? imagePromptVariation = null)
    {
        var faction = BuildCompleteMinimalMortalCreation();
        faction["factionId"] = "temp-faction-watch";
        faction.Remove("initialId");
        faction.Remove("isNewFaction");
        switch (imagePromptVariation)
        {
            case null:
                break;
            case "omitted":
                faction.Remove("image_prompt");
                break;
            case "non_english":
                faction["image_prompt"] = "дозорные у старой башни";
                break;
            case "overlong":
                faction["image_prompt"] = new string('a', 151);
                break;
            default:
                throw new ArgumentOutOfRangeException(
                    nameof(imagePromptVariation),
                    imagePromptVariation,
                    "Unsupported image prompt variation.");
        }
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

    private static JsonObject BuildRouteShiningRoot()
    {
        var root = ShiningAbodeState.CreateDefaultState();
        root["availability"] = ShiningAbodeState.AvailabilityActive;
        root["radiance"] = new JsonObject
        {
            ["experience"] = 250,
            ["tier"] = 2
        };
        root["lightSparks"] = 80;
        root["halls"] = new JsonArray();
        root["factions"] = new JsonArray();
        root["shiningPoliticalActors"] = new JsonArray();
        root["coreActionReceipts"] = new JsonArray();
        root["factionFoundingReceipts"] = new JsonArray();
        root["pendingNativeFactionDiscovery"] = null;
        return root;
    }

    private static JsonObject BuildLegacyRouteFaction(
        string factionId,
        string hallId,
        string projectId) =>
        new()
        {
            ["factionId"] = factionId,
            ["originType"] = ShiningAbodeState.OriginTypeNativeRadiant,
            ["hallId"] = hallId,
            ["baseStrength"] = 30,
            ["factionStrength"] = 30,
            ["projects"] = new JsonArray(
                BuildRouteProject(
                    projectId,
                    ShiningAbodeState.ProjectStatusCompleted))
        };

    private static JsonObject BuildRouteProject(
        string projectId,
        string status) =>
        new()
        {
            ["projectId"] = projectId,
            ["projectName"] = $"Project {projectId}",
            ["status"] = status
        };

    private static JsonObject BuildCompleteNativeRouteFaction(
        string factionId,
        string hallId,
        string requestId,
        string headResidentId,
        JsonArray projects)
    {
        var faction = BuildCompleteNativeShiningFaction();
        faction["factionId"] = factionId;
        faction["hallId"] = hallId;
        faction["creationProvenance"] = new JsonObject
        {
            ["route"] = "native_discovery",
            ["authorityType"] = "shining_core_action_request",
            ["authorityId"] = requestId
        };
        faction["leadership"] = new JsonObject
        {
            ["leadershipState"] = ShiningAbodeState.LeadershipStateSecure,
            ["headActorType"] = ShiningAbodeState.HeadActorTypeResident,
            ["headActorId"] = headResidentId
        };
        faction["projects"] = projects;
        faction["materialization"] = BuildShiningRouteEnvelope(
            factionId,
            $"fmat_{factionId}_12",
            hasProjects: true,
            hasResidents: true,
            hasLeadershipHistory: false);
        return faction;
    }

    private static JsonObject BuildCompletePlayerFoundedRouteFaction(
        string factionId,
        string hallId,
        string requestId)
    {
        var faction = BuildCompleteNativeShiningFaction();
        faction["factionId"] = factionId;
        faction["originType"] = ShiningAbodeState.OriginTypePlayerFounded;
        faction["hallId"] = hallId;
        faction["creationProvenance"] = new JsonObject
        {
            ["route"] = "player_founding",
            ["authorityType"] = "shining_founding_request",
            ["authorityId"] = requestId
        };
        faction["charter"] = BuildPlayerFoundingCharter();
        faction["leadership"] = new JsonObject
        {
            ["leadershipState"] = ShiningAbodeState.LeadershipStateSecure,
            ["headActorType"] = ShiningAbodeState.HeadActorTypePlayerSoul,
            ["headActorId"] = ShiningAbodeState.HeadActorTypePlayerSoul
        };
        faction["baseStrength"] = 35;
        faction["factionStrength"] = 35;
        faction["projects"] = new JsonArray();
        faction["leadershipHistory"] = new JsonArray(
            new JsonObject
            {
                ["requestId"] = requestId,
                ["eventType"] = "founded",
                ["turnNumber"] = 12,
                ["headActorType"] = ShiningAbodeState.HeadActorTypePlayerSoul,
                ["headActorId"] = ShiningAbodeState.HeadActorTypePlayerSoul
            });
        faction["materialization"] = BuildShiningRouteEnvelope(
            factionId,
            $"fmat_{factionId}_12",
            hasProjects: false,
            hasResidents: true,
            hasLeadershipHistory: true);
        return faction;
    }

    private static JsonObject BuildShiningRouteEnvelope(
        string factionId,
        string materializationId,
        bool hasProjects,
        bool hasResidents,
        bool hasLeadershipHistory)
    {
        var envelope = BuildShiningEnvelope(
            factionId,
            materializationId,
            canTrade: true);
        var capabilities = envelope["capabilities"]!.AsObject();
        capabilities["runsProjects"] = hasProjects;
        capabilities["hasResidentAffiliations"] = hasResidents;
        capabilities["hasLeadershipHistory"] = hasLeadershipHistory;
        var sections = envelope["sections"]!.AsObject();
        sections["projects"] = hasProjects
            ? PopulatedDisposition()
            : EmptyDisposition("No projects exist yet.");
        sections["residentAffiliations"] = hasResidents
            ? PopulatedDisposition()
            : EmptyDisposition("No affiliations exist yet.");
        sections["leadershipHistory"] = hasLeadershipHistory
            ? PopulatedDisposition()
            : EmptyDisposition("No leadership history exists yet.");
        return envelope;
    }

    private static JsonObject BuildRouteResidentRoot(
        params JsonObject[] residents) =>
        new()
        {
            ["entries"] = new JsonArray(
                residents.Select(resident => (JsonNode?)resident).ToArray()),
            [GuardianAbodeResidentState.HistoryLogProperty] = new JsonArray()
        };

    private static JsonObject BuildRouteResident(
        string residentId,
        string factionId) =>
        new()
        {
            ["residentId"] = residentId,
            ["displayName"] = $"Resident {residentId}",
            ["ascensionState"] = ShiningAbodeState.AscensionStateAscended,
            ["shiningFactionId"] = factionId,
            ["factionLoyaltyLevel"] = 50,
            ["factionLoyaltyTier"] =
                ShiningAbodeState.ResolveFactionLoyaltyTier(50),
            ["factionRestlessness"] = 0,
            ["factionRealignmentState"] =
                ShiningAbodeState.FactionRealignmentStateSettled
        };

    private static JsonObject BuildRouteSoulRoot(int currentFeathers) =>
        new()
        {
            ["currentRealm"] = "Shining Abode",
            ["currentIncarnation"] = 2,
            ["soulName"] = "Route Test Soul",
            ["inkFeathers"] = new JsonObject
            {
                ["current"] = currentFeathers,
                ["total"] = 100
            }
        };

    private static JsonObject BuildNativeDiscoveryRequest(string requestId) =>
        new()
        {
            ["requestId"] = requestId,
            ["actionType"] =
                ShiningCoreActionRequestState.ActionTypeDiscoverNativeFaction,
            ["radianceTierAtRequest"] = 2,
            ["quotedCostFeathers"] = 25,
            ["quotedCostLightSparks"] = 20,
            ["sourceDraftVersion"] = 0,
            ["selectedCardIds"] = new JsonArray(),
            ["createdAtTurn"] = 12,
            ["createdAtUtc"] = "2026-08-03T00:00:00Z"
        };

    private static JsonObject BuildNativeDiscoveryReceipt(
        string requestId,
        string factionId,
        string hallId,
        IReadOnlyCollection<string> residentIds,
        IReadOnlyCollection<string> projectIds) =>
        new()
        {
            ["requestId"] = requestId,
            ["actionType"] =
                ShiningCoreActionRequestState.ActionTypeDiscoverNativeFaction,
            ["status"] = ShiningCoreActionRequestState.RequestStatusAccepted,
            ["selectedCardIds"] = new JsonArray(),
            ["newResidentIds"] = new JsonArray(
                residentIds.Select(id => (JsonNode?)id).ToArray()),
            ["seededProjectIds"] = new JsonArray(
                projectIds.Select(id => (JsonNode?)id).ToArray()),
            ["resolvedFactionId"] = factionId,
            ["hallId"] = hallId,
            ["quotedCostFeathers"] = 25,
            ["quotedCostLightSparks"] = 20,
            ["generatedDraftVersion"] = 0,
            ["resolvedAtTurn"] = 12,
            ["resolvedAtUtc"] = "2026-08-03T00:05:00Z",
            ["reason"] = "native_discovery_accepted"
        };

    private static JsonObject BuildPlayerFoundingRequest(
        string requestId,
        string factionId,
        string hallId,
        IReadOnlyCollection<string> supporterIds) =>
        new()
        {
            ["requestId"] = requestId,
            ["proposedFactionId"] = factionId,
            ["proposedHallId"] = hallId,
            ["proposedHallName"] = "Player Hall",
            ["proposedHallDescription"] =
                "A hall founded by the player soul.",
            ["proposedHallServiceTags"] = new JsonArray("social", "memory"),
            ["charter"] = BuildPlayerFoundingCharter(),
            ["supportingResidentIds"] = new JsonArray(
                supporterIds.Select(id => (JsonNode?)id).ToArray()),
            ["quotedCostFeathers"] =
                ShiningFactionRequestState.FactionFoundingCostFeathers,
            ["quotedCostLightSparks"] =
                ShiningFactionRequestState.FactionFoundingCostLightSparks,
            ["createdAtTurn"] = 12,
            ["createdAtUtc"] = "2026-08-03T00:00:00Z"
        };

    private static JsonObject BuildPlayerFoundingCharter() =>
        new()
        {
            ["factionName"] = "Player Covenant",
            ["favoredArchetype"] =
                ShiningAbodeState.ProjectArchetypeAccord,
            ["patronEffectFamily"] =
                ShiningAbodeState.EffectFamilySocial,
            ["summary"] = "The player soul and supporters found a covenant."
        };

    private static JsonObject BuildPlayerFoundingHall(string hallId) =>
        new()
        {
            ["hallId"] = hallId,
            ["hallName"] = "Player Hall",
            ["description"] = "A hall founded by the player soul.",
            ["serviceTags"] = new JsonArray("social", "memory")
        };

    private static JsonObject BuildPlayerFoundingReceipt(
        string requestId,
        string factionId,
        string hallId,
        IReadOnlyCollection<string> supporterIds) =>
        new()
        {
            ["requestId"] = requestId,
            ["proposedFactionId"] = factionId,
            ["proposedHallId"] = hallId,
            ["hallName"] = "Player Hall",
            ["factionId"] = factionId,
            ["hallId"] = hallId,
            ["status"] = ShiningFactionRequestState.RequestStatusAccepted,
            ["supportingResidentIds"] = new JsonArray(
                supporterIds.Select(id => (JsonNode?)id).ToArray()),
            ["quotedCostFeathers"] =
                ShiningFactionRequestState.FactionFoundingCostFeathers,
            ["quotedCostLightSparks"] =
                ShiningFactionRequestState.FactionFoundingCostLightSparks,
            ["resolvedAtTurn"] = 12,
            ["resolvedAtUtc"] = "2026-08-03T00:05:00Z",
            ["reason"] = "founding_accepted"
        };

    private static JsonObject BuildRouteAfterlifeProfile(
        string actorId,
        bool includeEnvelope,
        string actorType = ShiningAbodeState.HeadActorTypeResident,
        bool canTrade = false)
    {
        var profile = new JsonObject
        {
            ["actorType"] = actorType,
            ["actorId"] = actorId,
            ["displayName"] = $"Resident {actorId}",
            ["appearanceDescription"] =
                "A radiant resident with a fully authored spiritual form.",
            ["profileSummary"] =
                "A founding resident of the newly discovered faction.",
            ["personalityProfile"] = new JsonObject
            {
                ["archetype"] = "Radiant Founder",
                ["worldview"] =
                    "Shared memory gives a shining faction continuity."
            },
            ["motivation"] =
                "Build a durable home for newly ascended residents.",
            ["realm"] = "Shining Abode",
            ["locationId"] = "location_shining_abode_gate",
            ["locationName"] = "Shining Abode Gate",
            ["currencies"] = new JsonObject
            {
                ["inkFeathers"] = 0,
                ["lightSparks"] = 0
            },
            ["progression"] = new JsonObject
            {
                ["enlightenment"] = new JsonObject
                {
                    ["experience"] = 0,
                    ["tier"] = 0
                },
                ["radiance"] = new JsonObject
                {
                    ["experience"] = 0,
                    ["tier"] = 0
                }
            },
            ["standardArts"] = new JsonObject { ["guard"] = 1 },
            ["specialArts"] = new JsonArray(),
            ["customStates"] = new JsonArray(),
            ["fateCards"] = new JsonArray(),
            ["relationships"] = new JsonArray(),
            ["goals"] = new JsonObject
            {
                ["goalId"] = $"goal_{actorId}",
                ["shortTermGoal"] = "Open the new hall.",
                ["longTermGoal"] = "Preserve the faction memory.",
                ["plan"] = "Support the hall and its first projects.",
                ["gmThoughtsSummary"] =
                    "I must preserve the exact founding evidence.",
                ["updatedAtTurn"] = 12
            },
            ["personalQuests"] = new JsonArray(),
            ["currentActivity"] = null,
            ["completedActivities"] = new JsonArray(),
            ["soulDissipationTier"] = 0,
            ["progressionStrategy"] = new JsonObject
            {
                ["strategyId"] = $"strategy_{actorId}",
                ["summary"] = "Preserve the new faction.",
                ["priorityOrder"] = new JsonArray("guard")
            },
            ["ledger"] = new JsonArray(),
            ["progressionLedger"] = new JsonArray(),
            ["gmThoughtsSummary"] =
                "I remember why I accepted this founding role."
        };
        if (includeEnvelope)
        {
            profile[ActorMaterializationContract.PropertyName] =
                new JsonObject
                {
                    ["schemaVersion"] = 1,
                    ["materializationId"] = $"mat_resident_{actorId}_12",
                    ["actorType"] = actorType,
                    ["actorId"] = actorId,
                    ["materializedAtTurn"] = 12,
                    ["state"] = "complete",
                    ["capabilities"] = new JsonObject
                    {
                        ["canFight"] = true,
                        ["canTeach"] = false,
                        ["canTrade"] = canTrade
                    },
                    ["sections"] = new JsonObject
                    {
                        ["standardArts"] = PopulatedDisposition(),
                        ["specialArts"] =
                            EmptyDisposition("No special arts exist yet."),
                        ["customStates"] =
                            EmptyDisposition("No custom states exist yet."),
                        ["fateCards"] =
                            EmptyDisposition("No fate cards exist yet."),
                        ["relationships"] =
                            EmptyDisposition("No relationships exist yet."),
                        ["agency"] = PopulatedDisposition(),
                        ["progressionHistory"] =
                            EmptyDisposition("No progression history exists yet.")
                    }
                };
        }

        return profile;
    }

    private static JsonObject BuildAfterlifeProfileRoot(
        IEnumerable<JsonObject> profiles) =>
        new()
        {
            ["schemaVersion"] = 1,
            [AfterlifeEntityProfileState.ProfilesProperty] =
                new JsonArray(
                    profiles
                        .Select(profile => (JsonNode?)profile)
                        .ToArray())
        };

    private static JsonObject BuildCompleteSarefStoryFaction(
        string visibility)
    {
        const string factionId = "shine_faction_wings";
        var faction = BuildCompleteNativeShiningFaction();
        faction["factionId"] = factionId;
        faction["hallId"] = "hall_wings_beneath_abyss";
        faction["creationProvenance"] = new JsonObject
        {
            ["route"] = "story",
            ["authorityType"] = "saref_main_story",
            ["authorityId"] = factionId
        };
        faction["visibility"] = visibility;
        faction["storyAuthority"] = new JsonObject
        {
            ["authorityType"] = "saref_main_story",
            ["authorityId"] = factionId,
            ["factionRole"] = "wings_of_angels"
        };
        faction["sarefFactionRole"] = "wings_of_angels";
        faction["sarefVisibility"] = visibility;
        faction["materialization"] = BuildShiningEnvelope(
            factionId,
            "fmat_shine_faction_wings_12",
            canTrade: true);
        MarkStoryStatePopulated(faction);
        return faction;
    }

    private static JsonObject BuildSarefStoryRoot(
        string visibility) =>
        new()
        {
            ["factionLinks"] = new JsonObject
            {
                ["wingsFactionId"] = "shine_faction_wings",
                ["visibility"] = visibility
            },
            ["wingsInfiltration"] = null
        };

    private static void ApplySarefStoryMutation(
        JsonObject faction,
        JsonObject sarefRoot,
        string mutation)
    {
        var authority = faction["storyAuthority"]!.AsObject();
        var provenance = faction["creationProvenance"]!.AsObject();
        switch (mutation)
        {
            case "missing_authority_id":
                authority.Remove("authorityId");
                break;
            case "wrong_authority_id":
                authority["authorityId"] = "shine_faction_other";
                provenance["authorityId"] = "shine_faction_other";
                break;
            case "wrong_role":
                authority["factionRole"] = "hidden_order";
                faction["sarefFactionRole"] = "hidden_order";
                break;
            case "wrong_story_visibility":
                sarefRoot["factionLinks"]!["visibility"] = "rumored";
                break;
            case "wrong_faction_visibility":
                faction["visibility"] = "rumored";
                break;
            case "wrong_legacy_visibility":
                faction["sarefVisibility"] = "rumored";
                break;
            case "wrong_faction_role":
                faction["sarefFactionRole"] = "hidden_order";
                break;
            case "provenance_mismatch":
                provenance["authorityId"] = "shine_faction_other";
                break;
            default:
                throw new ArgumentOutOfRangeException(
                    nameof(mutation),
                    mutation,
                    "Unsupported Saref story mutation.");
        }
    }

    private static JsonObject BuildCompleteGuardianStoryFaction()
    {
        const string factionId =
            "shine_faction_guardian_dawn";
        const string guardianId = "guardian_dawn";
        var faction = BuildCompleteNativeShiningFaction();
        faction["factionId"] = factionId;
        faction["originType"] =
            ShiningAbodeState.OriginTypeAscendedGuardian;
        faction["hallId"] = "hall_guardian_dawn";
        faction["creationProvenance"] = new JsonObject
        {
            ["route"] = "story",
            ["authorityType"] = "guardian_ascension",
            ["authorityId"] = guardianId
        };
        faction["visibility"] = "revealed";
        faction["storyAuthority"] = new JsonObject
        {
            ["authorityType"] = "guardian_ascension",
            ["authorityId"] = guardianId,
            ["factionRole"] = "patron_guardian"
        };
        faction["leadership"] = new JsonObject
        {
            ["leadershipState"] =
                ShiningAbodeState.LeadershipStateSecure,
            ["headActorType"] =
                ShiningAbodeState.HeadActorTypeGuardian,
            ["headActorId"] = guardianId
        };
        faction["materialization"] = BuildShiningEnvelope(
            factionId,
            "fmat_shine_faction_guardian_dawn_12",
            canTrade: true);
        MarkStoryStatePopulated(faction);
        return faction;
    }

    private static JsonObject BuildGuardianStoryRoot(
        bool duplicateGuardian = false)
    {
        static JsonObject Guardian() =>
            new()
            {
                ["guardianId"] = "guardian_dawn",
                ["name"] = "Dawn Guardian"
            };

        var guardians = new JsonArray(Guardian());
        if (duplicateGuardian)
            guardians.Add(Guardian());

        return new JsonObject
        {
            ["activeGuardian"] = Guardian(),
            ["guardians"] = guardians
        };
    }

    private static void ApplyGuardianStoryMutation(
        JsonObject faction,
        string mutation)
    {
        var authority = faction["storyAuthority"]!.AsObject();
        var provenance = faction["creationProvenance"]!.AsObject();
        var leadership = faction["leadership"]!.AsObject();
        switch (mutation)
        {
            case "wrong_authority_id":
                authority["authorityId"] = "guardian_other";
                provenance["authorityId"] = "guardian_other";
                break;
            case "wrong_role":
                authority["factionRole"] = "story_patron";
                break;
            case "wrong_visibility":
                faction["visibility"] = "hidden";
                break;
            case "wrong_head_type":
                leadership["headActorType"] =
                    ShiningAbodeState.HeadActorTypeResident;
                break;
            case "wrong_head_id":
                leadership["headActorId"] = "guardian_other";
                break;
            case "missing_profile":
            case "incomplete_profile":
            case "duplicate_guardian":
                break;
            default:
                throw new ArgumentOutOfRangeException(
                    nameof(mutation),
                    mutation,
                    "Unsupported Guardian story mutation.");
        }
    }

    private static void MarkStoryStatePopulated(
        JsonObject faction)
    {
        faction["materialization"]!["capabilities"]![
            "usesStoryState"] = true;
        faction["materialization"]!["sections"]!["storyState"] =
            PopulatedDisposition();
    }

    private static void MarkResidentAffiliationsPopulated(
        JsonObject faction)
    {
        faction["materialization"]!["capabilities"]![
            "hasResidentAffiliations"] = true;
        faction["materialization"]!["sections"]![
            "residentAffiliations"] = PopulatedDisposition();
    }

    private static void AddCompletedShiningProject(
        JsonObject faction,
        int tier,
        int submittedStrengthReward)
    {
        faction["projects"] = new JsonArray(new JsonObject
        {
            ["projectId"] = "project_forged_reward",
            ["displayName"] = "Forged Reward Project",
            ["summary"] = "A completed project with raw derived evidence.",
            ["toneTags"] = new JsonArray(),
            ["targetFactionIds"] = new JsonArray(),
            ["projectArchetype"] =
                ShiningAbodeState.ProjectArchetypeRemembrance,
            ["outputEffectFamily"] =
                ShiningAbodeState.EffectFamilyMemory,
            ["tier"] = tier,
            ["status"] = ShiningAbodeState.ProjectStatusCompleted,
            ["isSupported"] = true,
            ["strengthReward"] = submittedStrengthReward,
            ["completedAtTurn"] = 12,
            ["completedAtUtc"] = "2026-08-03T00:05:00Z"
        });
        faction["materialization"]!["capabilities"]!["runsProjects"] =
            true;
        faction["materialization"]!["sections"]!["projects"] =
            PopulatedDisposition();
    }

    private static JsonObject CloneJsonObject(JsonObject source) =>
        JsonNode.Parse(source.ToJsonString())!.AsObject();

    private static JsonObject BuildCompleteNativeShiningFaction() =>
        new()
        {
            ["factionId"] = "shine_faction_dawn_archive",
            ["originType"] = ShiningAbodeState.OriginTypeNativeRadiant,
            ["hallId"] = "hall_dawn_archive",
            ["creationProvenance"] = new JsonObject
            {
                ["route"] = "native_discovery",
                ["authorityType"] = "shining_core_action_request",
                ["authorityId"] = "request_discover_dawn_archive"
            },
            ["charter"] = new JsonObject
            {
                ["factionName"] = "Dawn Archive",
                ["favoredArchetype"] = ShiningAbodeState.ProjectArchetypeRemembrance,
                ["patronEffectFamily"] = ShiningAbodeState.EffectFamilyMemory,
                ["summary"] = "Preserve the truths carried into light."
            },
            ["currentAgenda"] = "Recover the names erased from the western gallery.",
            ["visibility"] = "revealed",
            ["storyAuthority"] = null,
            ["factionLifecycle"] = new JsonObject
            {
                ["state"] = ShiningAbodeState.FactionLifecycleStateActive
            },
            ["leadership"] = new JsonObject
            {
                ["leadershipState"] = ShiningAbodeState.LeadershipStateSecure,
                ["headActorType"] = ShiningAbodeState.HeadActorTypePlayerSoul,
                ["headActorId"] = ShiningAbodeState.HeadActorTypePlayerSoul
            },
            ["strategicMemory"] = new JsonObject
            {
                ["summary"] = "The Archive remembers the first dimming.",
                ["lastUpdatedTurn"] = 12,
                ["recentCampaigns"] = new JsonArray(),
                ["losses"] = new JsonArray(),
                ["alliances"] = new JsonArray(),
                ["enemies"] = new JsonArray()
            },
            ["chronicle"] = new JsonArray(
                new JsonObject
                {
                    ["entryId"] = "shine_chronicle_dawn_archive_founding",
                    ["turnNumber"] = 12,
                    ["eventType"] = "faction_materialized",
                    ["summary"] = "The Dawn Archive opened its hall.",
                    ["visibility"] = "known",
                    ["consequences"] = new JsonArray()
                }),
            ["baseStrength"] = 30,
            ["factionStrength"] = 30,
            ["investCountThisAscension"] = 0,
            ["projectArchetypesCountedThisAscension"] = new JsonArray(),
            ["projects"] = new JsonArray(),
            ["territorialInfluence"] = new JsonArray(),
            ["resourceLedger"] = new JsonArray(),
            ["tradeInventory"] = null,
            ["tradeInventoryReceipts"] = new JsonArray(),
            ["leadershipReceipts"] = new JsonArray(),
            ["leadershipHistory"] = new JsonArray(),
            ["materialization"] = BuildShiningEnvelope(
                "shine_faction_dawn_archive",
                "fmat_dawn_archive_12",
                canTrade: true)
        };

    private static JsonObject BuildShiningHall(
        string hallId,
        string hallName) =>
        new()
        {
            ["hallId"] = hallId,
            ["hallName"] = hallName,
            ["description"] = $"{hallName} serves the faction.",
            ["serviceTags"] = new JsonArray("memory")
        };

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

    private static JsonObject BuildCompleteMortalPromotion()
    {
        var faction = BuildCompleteMinimalMortalCreation();
        faction["factionId"] = "faction_watch";
        faction.Remove("initialId");
        faction.Remove("isNewFaction");
        faction["materialization"] = BuildMortalEnvelope(
            "faction_watch",
            "fmat_watch_promotion");
        return faction;
    }

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
        string materializationId,
        bool canTrade = false) =>
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
                ["canTrade"] = canTrade,
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
