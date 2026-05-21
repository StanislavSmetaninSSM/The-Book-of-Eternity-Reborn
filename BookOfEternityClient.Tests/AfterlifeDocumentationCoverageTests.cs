using System.Reflection;
using System.Text.Json;
using System.Text.RegularExpressions;
using BookOfEternityClient.Configuration;
using BookOfEternityClient.Services;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed class AfterlifeDocumentationCoverageTests
{
    [Fact]
    public void AfterlifePendingControlSurfaceInventoryIsMachineReadable()
    {
        var inventory = ReadRepoFile("OtherGuides", "Afterlife_Pending_Control_Surface_Inventory.json");
        using var document = JsonDocument.Parse(inventory);

        Assert.Equal(JsonValueKind.Object, document.RootElement.ValueKind);
        Assert.Equal("afterlife_pending_control_surface_inventory_v1", document.RootElement.GetProperty("schema").GetString());
        Assert.True(document.RootElement.TryGetProperty("surfaces", out var surfaces));
        Assert.Equal(JsonValueKind.Array, surfaces.ValueKind);
        var surfaceArray = surfaces.EnumerateArray().ToArray();
        Assert.NotEmpty(surfaceArray);

        var paths = surfaceArray
            .Select(surface => surface.GetProperty("path").GetString())
            .Where(path => !string.IsNullOrWhiteSpace(path))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var requiredPaths = new[]
        {
            AfterlifeSpiritualConflictState.StatePath,
            AfterlifeEntityProfileState.StatePath,
            SarefMainStoryState.StatePath,
            GuardianAbodeOfferingState.PendingRequestPath,
            GuardianTradeRequestState.PendingRequestPath,
            PlayerGuardianFoundationState.PendingRequestPath,
            NpcTradeRequestState.PendingRequestPath,
            AfterlifeArchiveActionState.ConsultationRequestPath,
            AfterlifeArchiveActionState.ProjectFuelRequestPath,
            GuardianAbodeResidentRequestState.PendingResidentsRequestPath,
            GuardianAbodeResidentRequestState.PendingInteractionsRequestPath,
            GuardianAbodeResidentRequestState.PendingTransfersRequestPath,
            GuardianAbodeResidentRequestState.PendingManifestationRequestPath,
            ActorSocialInteractionRequestState.PendingGuardianRequestPath,
            ActorSocialInteractionRequestState.PendingNpcRequestPath,
            SystemGuardianLibraryService.AttractionRequestPath,
            ShiningCoreActionRequestState.PendingActionsRequestPath,
            ShiningTradeRequestState.PendingRequestsPath,
            ShiningFactionRequestState.PendingFoundingsRequestPath,
            ShiningFactionRequestState.PendingRealignmentsRequestPath,
            ShiningFactionRequestState.PendingLeadershipTransitionsRequestPath,
            SourceOfLightCapstoneState.PendingRequestPath,
            AfterlifeReturnGuardService.GuardPath,
            ProgressionScheduleService.SchedulePath,
            ProgressionScheduleService.ReportPath,
            "game_state/control/life_transitions.json",
            "game_state/control/incarnation_trigger.json",
            "game_state/control/ascension.json",
            WorldDirectiveService.PendingSetupPath,
            ScenarioCoreService.ManifestPath,
            AfterlifeArchiveCandidateService.ManifestPath,
            AfterlifeNotificationState.NotificationsPath
        };

        foreach (var requiredPath in requiredPaths)
            Assert.Contains(requiredPath, paths);

        foreach (var surface in surfaceArray)
        {
            Assert.False(string.IsNullOrWhiteSpace(surface.GetProperty("path").GetString()));
            Assert.False(string.IsNullOrWhiteSpace(surface.GetProperty("owner").GetString()));
            Assert.False(string.IsNullOrWhiteSpace(surface.GetProperty("realm").GetString()));
            Assert.False(string.IsNullOrWhiteSpace(surface.GetProperty("authority").GetString()));
            Assert.Equal(JsonValueKind.Array, surface.GetProperty("docAnchors").ValueKind);
        }
    }

    [Fact]
    public void ShiningCoreActionCoverageIncludesEverySupportedActionType()
    {
        var matrix = ReadRepoFile("OtherGuides", "Afterlife_Contract_Matrix.md");
        var examples = ReadRepoFile("Examples", "E_CLI_Afterlife_Turns.txt");

        var actionTypes = typeof(ShiningCoreActionRequestState)
            .GetFields(BindingFlags.Public | BindingFlags.Static | BindingFlags.FlattenHierarchy)
            .Where(field => field is { IsLiteral: true, IsInitOnly: false } &&
                            field.FieldType == typeof(string) &&
                            field.Name.StartsWith("ActionType", StringComparison.Ordinal))
            .Select(field => (string)field.GetRawConstantValue()!)
            .OrderBy(value => value, StringComparer.Ordinal)
            .ToArray();

        Assert.NotEmpty(actionTypes);

        foreach (var actionType in actionTypes)
        {
            Assert.Contains($"`{actionType}`", matrix, StringComparison.Ordinal);
            Assert.Contains(actionType, examples, StringComparison.Ordinal);
        }
    }

    [Fact]
    public void ShiningCoreActionSchemaAndStatusesMatchRuntime()
    {
        var matrix = ReadRepoFile("OtherGuides", "Afterlife_Contract_Matrix.md");
        var examples = ReadRepoFile("Examples", "E_CLI_Afterlife_Turns.txt");
        var apiSpec = ReadRepoFile("CLI_API_Specification.md");
        var daemonSpec = ReadRepoFile("CLI_Agent_Daemon_Specification.md");
        var docs = new[] { matrix, examples, apiSpec, daemonSpec };

        var statuses = typeof(ShiningCoreActionRequestState)
            .GetFields(BindingFlags.Public | BindingFlags.Static | BindingFlags.FlattenHierarchy)
            .Where(field => field is { IsLiteral: true, IsInitOnly: false } &&
                            field.FieldType == typeof(string) &&
                            field.Name.StartsWith("RequestStatus", StringComparison.Ordinal))
            .Select(field => (string)field.GetRawConstantValue()!)
            .OrderBy(value => value, StringComparer.Ordinal)
            .ToArray();

        Assert.Equal(
            new[]
            {
                ShiningCoreActionRequestState.RequestStatusAccepted,
                ShiningCoreActionRequestState.RequestStatusRefused,
                ShiningCoreActionRequestState.RequestStatusWithdrawn
            }.OrderBy(value => value, StringComparer.Ordinal),
            statuses);

        foreach (var doc in docs)
        {
            Assert.Contains("pending_shining_abode_actions.json", doc, StringComparison.Ordinal);
            Assert.Contains("requests[]", doc, StringComparison.Ordinal);
            Assert.DoesNotContain("requests[0]", doc, StringComparison.Ordinal);

            foreach (var status in statuses)
                Assert.Contains(status, doc, StringComparison.Ordinal);
        }
    }

    [Fact]
    public void ShiningCoreRequestShapeAndZeroCostFieldsAreDocumented()
    {
        var matrix = ReadRepoFile("OtherGuides", "Afterlife_Contract_Matrix.md");
        var apiSpec = ReadRepoFile("CLI_API_Specification.md");
        var examples = ReadRepoFile("Examples", "E_CLI_Afterlife_Turns.txt");

        foreach (var doc in new[] { matrix, apiSpec, examples })
        {
            Assert.Contains("requests[]", doc, StringComparison.Ordinal);
            Assert.Contains("quotedCostFeathers", doc, StringComparison.Ordinal);
            Assert.Contains("quotedCostLightSparks", doc, StringComparison.Ordinal);
        }

        Assert.DoesNotContain("pending_shining_abode_actions.json.actionType", examples, StringComparison.Ordinal);
        Assert.Contains("quotedCostFeathers = 0", matrix, StringComparison.Ordinal);
        Assert.Contains("quotedCostLightSparks = 0", matrix, StringComparison.Ordinal);
        Assert.Contains("quotedCostFeathers = costFeathers", matrix, StringComparison.Ordinal);
        Assert.Contains("quotedCostLightSparks = 0", apiSpec, StringComparison.Ordinal);
        Assert.Contains("quotedCostLightSparks = 0", ExtractRequiredSection(
            examples,
            "Legacy `pendingNativeFactionDiscovery` closure:",
            "Correct `discover_native_faction` receipt fragment:"), StringComparison.Ordinal);
        Assert.Contains("\"quotedCostFeathers\": 0", examples, StringComparison.Ordinal);
        Assert.Contains("\"quotedCostLightSparks\": 0", examples, StringComparison.Ordinal);
        Assert.Contains("selectedCards[]", matrix, StringComparison.Ordinal);
        Assert.Contains("selectedCards[]", examples, StringComparison.Ordinal);
        Assert.Contains("residentHistoryEntryId", matrix, StringComparison.Ordinal);
        Assert.Contains("residentHistoryEntryId", examples, StringComparison.Ordinal);

        var commonReceiptSkeleton = ExtractRequiredSection(
            examples,
            "Common accepted receipt fields for every Shining core action:",
            "Important receipt rules:");
        Assert.Contains("\"quotedCostFeathers\": 0", commonReceiptSkeleton, StringComparison.Ordinal);
        Assert.Contains("\"quotedCostLightSparks\": 0", commonReceiptSkeleton, StringComparison.Ordinal);
    }

    [Fact]
    public void PlayerGuardianFoundationPreconditionsAreDocumentedForGm()
    {
        var matrix = ReadRepoFile("OtherGuides", "Afterlife_Contract_Matrix.md");
        var apiSpec = ReadRepoFile("CLI_API_Specification.md");
        var taskGuide = ReadRepoFile("TaskGuides", "CLI_Step_Main.txt");
        var operations = ReadRepoFile("Rules", "Block_CLI_Operations.txt");
        var examples = ReadRepoFile("Examples", "E_CLI_Afterlife_Turns.txt");

        foreach (var doc in new[] { matrix, apiSpec, taskGuide, operations, examples })
        {
            Assert.Contains("sealed_until_next_ascension", doc, StringComparison.Ordinal);
            Assert.Contains("preparedIncarnationPackage", doc, StringComparison.Ordinal);
            Assert.Contains("afterlife_return_guard.json", doc, StringComparison.Ordinal);
            Assert.Contains("playerFoundedGuardianId", doc, StringComparison.Ordinal);
            Assert.Contains("sourceShiningAvailability", doc, StringComparison.Ordinal);
        }

        foreach (var doc in new[] { matrix, apiSpec, taskGuide, operations, examples })
        {
            Assert.Contains("malformed", doc, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("wrong-reason", doc, StringComparison.OrdinalIgnoreCase);
        }

        Assert.DoesNotContain("returned_from_shining_abode", examples, StringComparison.Ordinal);
    }

    [Fact]
    public void ReenterShiningAbodeLocalRouteDocsCoverGuardAndSyncSideEffects()
    {
        var apiSpec = ReadRepoFile("CLI_API_Specification.md");
        var matrix = ReadRepoFile("OtherGuides", "Afterlife_Contract_Matrix.md");
        var daemonSpec = ReadRepoFile("CLI_Agent_Daemon_Specification.md");
        var combined = apiSpec + matrix + daemonSpec;

        Assert.Contains("reenter_shining_abode", combined, StringComparison.Ordinal);
        Assert.Contains("malformed", combined, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("wrong-reason", combined, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("gacha charges", combined, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("trade auto-refresh", combined, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("No GM-authored output", apiSpec, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ShiningCanonicalEnumVocabularyIsDocumentedForGm()
    {
        var matrix = ReadRepoFile("OtherGuides", "Afterlife_Contract_Matrix.md");
        var examples = ReadRepoFile("Examples", "E_CLI_Afterlife_Turns.txt");

        var values = ShiningConstantValues(
            "HeadActorType",
            "LeadershipState",
            "OriginType",
            "PoliticalStatus",
            "ProjectStatus",
            "FactionRealignmentState",
            "FactionLoyaltyTier",
            "ResidentRole",
            "ProjectArchetype");

        Assert.NotEmpty(values);
        foreach (var value in values)
        {
            Assert.Contains(value, matrix, StringComparison.Ordinal);
            Assert.Contains(value, examples, StringComparison.Ordinal);
        }
    }

    [Fact]
    public void ShiningFactionFoundingDocsCoverClientResourceReservationSnapshot()
    {
        var matrix = ReadRepoFile("OtherGuides", "Afterlife_Contract_Matrix.md");
        var apiSpec = ReadRepoFile("CLI_API_Specification.md");
        var taskGuide = ReadRepoFile("TaskGuides", "CLI_Step_Main.txt");
        var examples = ReadRepoFile("Examples", "E_CLI_Afterlife_Turns.txt");

        foreach (var doc in new[] { matrix, apiSpec, taskGuide, examples })
        {
            Assert.Contains("reservedInkFeathersBefore", doc, StringComparison.Ordinal);
            Assert.Contains("reservedLightSparksBefore", doc, StringComparison.Ordinal);
            Assert.Contains("quotedCostFeathers", doc, StringComparison.Ordinal);
            Assert.Contains("quotedCostLightSparks", doc, StringComparison.Ordinal);
        }
    }

    [Fact]
    public void GuardianProjectAssistAndSabotageScoringAreDocumentedForGm()
    {
        var matrix = ReadRepoFile("OtherGuides", "Afterlife_Contract_Matrix.md");
        var examples = ReadRepoFile("Examples", "E_CLI_Afterlife_Turns.txt");
        var manifest = ReadRepoFile("Examples", "example_validation_manifest.json");
        var combined = matrix + examples + manifest;

        foreach (var term in new[]
        {
            "DomainRelevance",
            "RiskOrCost",
            "ScarcityOrUniqueness",
            "DirectProjectImpact",
            "assistScore",
            "auditKind=defense",
            "rival_defense",
            "HostileReach",
            "ProjectExposure",
            "DamageIntent",
            "DamageAchieved",
            "PlayerComplicity",
            "sabotageSeverityScore",
            "grand strike",
            "guardian_project_update_sabotage_example_001",
            "afterlife_guardian_project_sabotage_power_response"
        })
        {
            Assert.Contains(term, combined, StringComparison.Ordinal);
        }
    }

    [Fact]
    public void NonFeatherAbodeOfferingsAreDocumentedWithoutInkFeatherReceipt()
    {
        var matrix = ReadRepoFile("OtherGuides", "Afterlife_Contract_Matrix.md");
        var examples = ReadRepoFile("Examples", "E_CLI_Afterlife_Turns.txt");
        var manifest = ReadRepoFile("Examples", "example_validation_manifest.json");
        var combined = matrix + examples + manifest;

        foreach (var term in new[]
        {
            "soul_relic",
            "archive_lore_fragment",
            "archive_secret_record",
            "guardianAbodeOffering",
            "guardianPowerEvents",
            "Do not write `output/ink_feather_action_result.json`",
            "afterlife_abode_offering_response",
            "afterlife_abode_offering_soul_relic_response",
            "afterlife_abode_offering_archive_response",
            "abode_offering_azalia_soul_relic_return_5",
            "abode_offering_azalia_archive_return_5"
        })
        {
            Assert.Contains(term, combined, StringComparison.Ordinal);
        }
    }

    [Fact]
    public void ShiningGachaAndProjectStrengthDocsMatchRuntimeContracts()
    {
        var matrix = ReadRepoFile("OtherGuides", "Afterlife_Contract_Matrix.md");
        var examples = ReadRepoFile("Examples", "E_CLI_Afterlife_Turns.txt");
        var combined = matrix + examples;

        Assert.Contains("baseRarity", combined, StringComparison.Ordinal);
        Assert.Contains("finalRarity", combined, StringComparison.Ordinal);
        Assert.Contains("projectedGachaBonusSteps", combined, StringComparison.Ordinal);
        Assert.Contains("tier 1 = 8, tier 2 = 12, tier 3 = 16", examples, StringComparison.Ordinal);
        Assert.DoesNotContain("\"strengthReward\": 6", examples, StringComparison.Ordinal);
    }

    [Fact]
    public void AbodeResidentDispositionDevotionMigrationVocabularyIsDocumentedForGm()
    {
        var matrix = ReadRepoFile("OtherGuides", "Afterlife_Contract_Matrix.md");
        var examples = ReadRepoFile("Examples", "E_CLI_Afterlife_Turns.txt");

        var values = ResidentConstantValues(
            "PowerSensitivity",
            "MigrationDisposition",
            "CommunalOrientation",
            "StabilityNeed",
            "AbodeDevotionTier",
            "MigrationState");

        Assert.NotEmpty(values);
        foreach (var value in values)
        {
            Assert.Contains(value, matrix, StringComparison.Ordinal);
            Assert.Contains(value, examples, StringComparison.Ordinal);
        }

        foreach (var doc in new[] { matrix, examples })
        {
            Assert.Contains("0..19", doc, StringComparison.Ordinal);
            Assert.Contains("ready_to_transfer", doc, StringComparison.Ordinal);
            Assert.Contains("guest", doc, StringComparison.Ordinal);
        }
    }

    [Fact]
    public void DirectResidentActionExamplesAreRuntimeValidated()
    {
        var examples = ReadRepoFile("Examples", "E_CLI_Afterlife_Turns.txt");
        var manifest = ReadRepoFile("Examples", "example_validation_manifest.json");
        var combined = examples + manifest;

        foreach (var term in new[]
        {
            "ABODE_RESIDENT_RELIC_GRANT accepted response",
            "ABODE_RESIDENT_QUEST_REQUEST accepted response",
            "afterlife_direct_resident_relic_grant_response",
            "afterlife_direct_resident_quest_request_response",
            "metaStateUpdates",
            "soulRelicOperations",
            "relatedAfterlifeResidentId",
            "residentInteractionLogUpdates"
        })
        {
            Assert.Contains(term, combined, StringComparison.Ordinal);
        }
    }

    [Fact]
    public void ShiningClosureCompositeDiffRulesAreDocumentedForGm()
    {
        var matrix = ReadRepoFile("OtherGuides", "Afterlife_Contract_Matrix.md");
        var examples = ReadRepoFile("Examples", "E_CLI_Afterlife_Turns.txt");
        var apiSpec = ReadRepoFile("CLI_API_Specification.md");
        var daemonSpec = ReadRepoFile("CLI_Agent_Daemon_Specification.md");
        var taskGuide = ReadRepoFile("TaskGuides", "CLI_Step_Main.txt");
        var operations = ReadRepoFile("Rules", "Block_CLI_Operations.txt");
        var manifest = ReadRepoFile("Examples", "example_validation_manifest.json");

        foreach (var doc in new[] { matrix, examples })
        {
            Assert.Contains("game_state/control/pending_turn_snapshot", doc, StringComparison.Ordinal);
            Assert.Contains("game_state/meta/shining_abode_state.json", doc, StringComparison.Ordinal);
            Assert.Contains("game_state/meta/guardian_abode_residents.json", doc, StringComparison.Ordinal);
            Assert.Contains("game_state/meta/soul_state.json", doc, StringComparison.Ordinal);
            Assert.Contains("shining_closure_missing_pre_turn_shining_state", doc, StringComparison.Ordinal);
            Assert.Contains("shining_closure_missing_pre_turn_resident_state", doc, StringComparison.Ordinal);
            Assert.Contains("shining_closure_missing_pre_turn_soul_state", doc, StringComparison.Ordinal);
            Assert.Contains("shining_closure_unexpected_shining_state_diff", doc, StringComparison.Ordinal);
            Assert.Contains("shining_closure_unexpected_resident_state_diff", doc, StringComparison.Ordinal);
            Assert.Contains("shining_closure_unexpected_soul_state_diff", doc, StringComparison.Ordinal);
            Assert.Contains("ABODE_OFFERING", doc, StringComparison.Ordinal);
        }

        foreach (var doc in new[] { matrix, examples, apiSpec, daemonSpec, taskGuide, operations })
        {
            Assert.Contains("progressionProcessingReport", doc, StringComparison.Ordinal);
            Assert.Contains("scheduler-owned", doc, StringComparison.Ordinal);
            Assert.Contains("coreActionReceipts[]", doc, StringComparison.Ordinal);
            Assert.Contains("gachaSystem.gachaHistory", doc, StringComparison.Ordinal);
            Assert.Contains("lightSparks", doc, StringComparison.Ordinal);
            Assert.Contains("treasury", doc, StringComparison.Ordinal);
            Assert.Contains("sourceOfLightCapstone", doc, StringComparison.Ordinal);
        }

        Assert.Contains("shining_closure composite diff rules", manifest, StringComparison.Ordinal);
    }

    [Fact]
    public void ShiningPoliticalActorsAreDocumentedForGm()
    {
        var matrix = ReadRepoFile("OtherGuides", "Afterlife_Contract_Matrix.md");
        var examples = ReadRepoFile("Examples", "E_CLI_Afterlife_Turns.txt");
        var taskGuide = ReadRepoFile("TaskGuides", "CLI_Step_Main.txt");
        var operations = ReadRepoFile("Rules", "Block_CLI_Operations.txt");
        var daemonSpec = ReadRepoFile("CLI_Agent_Daemon_Specification.md");
        var apiSpec = ReadRepoFile("CLI_API_Specification.md");

        foreach (var text in new[] { matrix, examples, taskGuide, operations, daemonSpec, apiSpec })
        {
            Assert.Contains("shiningPoliticalActors", text, StringComparison.Ordinal);
            Assert.Contains("radiant_actor", text, StringComparison.Ordinal);
            Assert.Contains("headActorId", text, StringComparison.Ordinal);
            Assert.Contains("actorId", text, StringComparison.Ordinal);
        }
    }

    [Fact]
    public void AfterlifeEntityProfilesAreDocumentedForGm()
    {
        var matrix = ReadRepoFile("OtherGuides", "Afterlife_Contract_Matrix.md");
        var examples = ReadRepoFile("Examples", "E_CLI_Afterlife_Turns.txt");
        var apiSpec = ReadRepoFile("CLI_API_Specification.md");
        var daemonSpec = ReadRepoFile("CLI_Agent_Daemon_Specification.md");
        var taskGuide = ReadRepoFile("TaskGuides", "CLI_Step_Main.txt");
        var glossary = ReadRepoFile("OtherGuides", "Afterlife_Combat_Terminology_Glossary.md");
        var manifest = ReadRepoFile("Examples", "example_validation_manifest.json");

        foreach (var text in new[] { matrix, examples, apiSpec, daemonSpec, taskGuide })
        {
            Assert.Contains("game_state/meta/afterlife_entity_profiles.json", text, StringComparison.Ordinal);
            Assert.Contains("afterlifeEntityProfileUpdates", text, StringComparison.Ordinal);
            Assert.Contains("Профили сущностей посмертия", text, StringComparison.Ordinal);
            Assert.Contains("actorType", text, StringComparison.Ordinal);
            Assert.Contains("actorId", text, StringComparison.Ordinal);
            Assert.Contains("actorType=player_soul", text, StringComparison.Ordinal);
            Assert.Contains("actorId=player_soul", text, StringComparison.Ordinal);
            Assert.Contains("standardArts", text, StringComparison.Ordinal);
            Assert.Contains("specialArts", text, StringComparison.Ordinal);
            Assert.Contains("soulDissipationTier", text, StringComparison.Ordinal);
            Assert.Contains("targetStabilityCoefficient", text, StringComparison.Ordinal);
            Assert.Contains("soulDissipationProof", text, StringComparison.Ordinal);
            Assert.Contains("progressionStrategy", text, StringComparison.Ordinal);
            Assert.Contains("customStates", text, StringComparison.Ordinal);
            Assert.Contains("afterlifeEntityCustomStateChanges", text, StringComparison.Ordinal);
            Assert.Contains("statesToRemove", text, StringComparison.Ordinal);
            Assert.Contains("afterlifeEntityProgressionOverrides", text, StringComparison.Ordinal);
            Assert.Contains("currencyDeltas", text, StringComparison.Ordinal);
            Assert.Contains("progressionExperienceDeltas", text, StringComparison.Ordinal);
            Assert.Contains("inkFeathers", text, StringComparison.Ordinal);
            Assert.Contains("lightSparks", text, StringComparison.Ordinal);
            Assert.Contains("Chaos Sea profiles must keep `currencies.lightSparks = 0`", text, StringComparison.Ordinal);
            Assert.Contains("enlightenment", text, StringComparison.Ordinal);
            Assert.Contains("radiance", text, StringComparison.Ordinal);
            Assert.Contains("specialArtTierDeltas", text, StringComparison.Ordinal);
            Assert.Contains("soulDissipationTierDelta", text, StringComparison.Ordinal);
            Assert.Contains("progressionLedger", text, StringComparison.Ordinal);
            Assert.Contains("lastAutoProgressionCycleKey", text, StringComparison.Ordinal);
            Assert.Contains("current-turn `progression_report.json`", text, StringComparison.Ordinal);
            Assert.Contains("soul_dissipation", text, StringComparison.Ordinal);
            Assert.Contains("afterlifeSpecialArtLearningReceipts", text, StringComparison.Ordinal);
            Assert.Contains("trainingConditions", text, StringComparison.Ordinal);
            Assert.Contains("ownerActorType/ownerActorId` must match", text, StringComparison.Ordinal);
            Assert.Contains("costMultiplierPercent", text, StringComparison.Ordinal);
            Assert.Contains("Spark-only", text, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("at least one positive", text, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("initialTier", text, StringComparison.Ordinal);
            Assert.Contains("repair-blocking", text, StringComparison.OrdinalIgnoreCase);
        }

        Assert.Contains("/afterlife_profiles", glossary, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("/профили_загробья", glossary, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("afterlife_entity_profiles_v1", examples, StringComparison.Ordinal);
        Assert.Contains("Вы мертвы. Ваша душа окончательно развеяна. Загрузите последнее сохранение и попробуйте снова", examples, StringComparison.Ordinal);
        Assert.Contains("afterlife_special_art_learning_v1", examples, StringComparison.Ordinal);
        Assert.Contains("actionCostAudit.player.artTier", matrix + examples + apiSpec + daemonSpec + taskGuide, StringComparison.Ordinal);
        Assert.Contains("pre-turn authority", matrix + examples + apiSpec + daemonSpec + taskGuide, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("learned special arts", matrix + examples + apiSpec + daemonSpec + taskGuide, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("/spiritual_arts", matrix + examples + apiSpec + daemonSpec + taskGuide, StringComparison.Ordinal);
        Assert.Contains("afterlife_entity_profiles_v1", manifest, StringComparison.Ordinal);
        Assert.Contains("afterlife_special_art_learning_v1", manifest, StringComparison.Ordinal);
    }

    [Fact]
    public void AfterlifeEntityProfileEntrypointsMentionRequiredContracts()
    {
        var launcherScript = ReadRepoFile("BookOfEternityClient", "Launcher", "CLI_Launch_Script.md");
        var launcherGenerator = ReadRepoFile("BookOfEternityClient", "Launcher", "Generate_CLI_Launch_Script.ps1");
        var daemonScript = ReadRepoFile("BookOfEternityClient", "game_master_daemon.ps1");

        foreach (var text in new[] { launcherScript, launcherGenerator, daemonScript })
        {
            Assert.Contains("example 26", text, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("game_state/meta/afterlife_entity_profiles.json", text, StringComparison.Ordinal);
            Assert.Contains("afterlifeEntityProfileUpdates", text, StringComparison.Ordinal);
            Assert.Contains("afterlifeEntityCustomStateChanges", text, StringComparison.Ordinal);
            Assert.Contains("afterlifeEntityProgressionOverrides", text, StringComparison.Ordinal);
            Assert.Contains("afterlifeSpecialArtLearningReceipts", text, StringComparison.Ordinal);
            Assert.Contains("specialArtAudits[]", text, StringComparison.Ordinal);
            Assert.Contains("soulDissipationProof", text, StringComparison.Ordinal);
            Assert.Contains("targetStabilityCoefficient", text, StringComparison.Ordinal);
            Assert.Contains("terminalGameOver", text, StringComparison.Ordinal);
        }
    }

    [Fact]
    public void AfterlifeLiveSystemReminderMentionsEntityProfileSurfaces()
    {
        var lifecyclePrompt = ReadRepoFile("BookOfEternityClient", "Core", "GameEngine", "GameEngine.TurnLifecycle.cs");

        Assert.Contains("afterlifeEntityProfileUpdates", lifecyclePrompt, StringComparison.Ordinal);
        Assert.Contains("afterlifeEntityCustomStateChanges", lifecyclePrompt, StringComparison.Ordinal);
        Assert.Contains("afterlifeEntityProgressionOverrides", lifecyclePrompt, StringComparison.Ordinal);
        Assert.Contains("afterlifeSpecialArtLearningReceipts", lifecyclePrompt, StringComparison.Ordinal);
        Assert.Contains("soulDissipationProof", lifecyclePrompt, StringComparison.Ordinal);
        Assert.Contains("targetStabilityCoefficient", lifecyclePrompt, StringComparison.Ordinal);
        Assert.Contains("terminalGameOver", lifecyclePrompt, StringComparison.Ordinal);
    }

    [Fact]
    public void AfterlifeEntityProfileOperationRulesMentionCommandSurfaces()
    {
        var operations = ReadRepoFile("Rules", "Block_CLI_Operations.txt");

        foreach (var token in new[]
        {
            "game_state/meta/afterlife_entity_profiles.json",
            "afterlifeEntityProfileUpdates",
            "afterlifeEntityCustomStateChanges",
            "afterlifeEntityProgressionOverrides",
            "afterlifeSpecialArtLearningReceipts",
            "soulDissipationProof",
            "targetStabilityCoefficient",
            "terminalGameOver"
        })
        {
            Assert.Contains(token, operations, StringComparison.Ordinal);
        }
    }

    [Fact]
    public void AfterlifeLiveSystemReminderMentionsCriticalMatrixSurfaces()
    {
        var lifecyclePrompt = ReadRepoFile("BookOfEternityClient", "Core", "GameEngine", "GameEngine.TurnLifecycle.cs");
        var liveReminder = ExtractRequiredSection(
            lifecyclePrompt,
            "private string BuildSystemReminder()",
            "\" + _storyService.BuildStoryContext();");

        var requiredTokens = new[]
        {
            "afterlifeSpiritualConflictUpdate",
            "afterlifeEntityProfileUpdates",
            "afterlifeEntityCustomStateChanges",
            "afterlifeEntityProgressionOverrides",
            "afterlifeSpecialArtLearningReceipts",
            "progressionProcessingReport",
            "pending_shining_abode_actions.json",
            "pending_shining_trade_inventory_requests.json",
            "pending_shining_faction_foundings.json",
            "pending_shining_faction_realignments.json",
            "pending_shining_faction_leadership_transitions.json",
            "pending_source_of_light_capstone.json",
            "pending_saref_wings_infiltration.json",
            "sarefMainStoryUpdate",
            "Source of Light",
            "active spiritual conflict"
        };

        foreach (var token in requiredTokens)
            Assert.Contains(token, liveReminder, StringComparison.Ordinal);
    }

    [Fact]
    public void ShiningLeadershipTransitionModesAndHistoryMappingsAreDocumented()
    {
        var matrix = ReadRepoFile("OtherGuides", "Afterlife_Contract_Matrix.md");
        var examples = ReadRepoFile("Examples", "E_CLI_Afterlife_Turns.txt");
        var docs = new[] { matrix, examples };

        var transitionModes = typeof(ShiningFactionRequestState)
            .GetFields(BindingFlags.Public | BindingFlags.Static | BindingFlags.FlattenHierarchy)
            .Where(field => field is { IsLiteral: true, IsInitOnly: false } &&
                            field.FieldType == typeof(string) &&
                            field.Name.StartsWith("TransitionMode", StringComparison.Ordinal))
            .Select(field => (string)field.GetRawConstantValue()!)
            .OrderBy(value => value, StringComparer.Ordinal)
            .ToArray();

        Assert.Equal(
            new[]
            {
                ShiningFactionRequestState.TransitionModeAbdication,
                ShiningFactionRequestState.TransitionModePeacefulSuccession,
                ShiningFactionRequestState.TransitionModeRevolt
            }.OrderBy(value => value, StringComparer.Ordinal),
            transitionModes);

        foreach (var doc in docs)
        {
            Assert.Contains("pending_shining_faction_leadership_transitions.json", doc, StringComparison.Ordinal);

            foreach (var transitionMode in transitionModes)
                Assert.Contains(transitionMode, doc, StringComparison.Ordinal);
        }

        foreach (var requiredTerm in new[]
        {
            "accepted",
            "refused",
            "withdrawn",
            "succeeded",
            "revolted",
            "abdicated",
            "vacated",
            "leadershipHistory.eventType",
            "departed_to_neutral",
            "active-guardian auto faction",
            "faction_{slug(activeGuardianId)}",
            "candidateHeadActorType=guardian",
            "activeGuardianId",
            "Vacant leadership rule",
            "leadershipState = \"vacant\"",
            "every `pending_shining_faction_leadership_transitions.json` request requires an actual current non-vacant incumbent",
            "do not create or close `abdication`, `peaceful_succession`, or `revolt`",
            "single-head invariant",
            "Revolt supporter rule"
        })
        {
            Assert.Contains(requiredTerm, examples, StringComparison.Ordinal);
        }

        foreach (var requiredTerm in new[]
        {
            "active-guardian auto faction",
            "faction_{slug(activeGuardianId)}",
            "candidateHeadActorType=guardian",
            "activeGuardianId",
            "leadershipState = vacant",
            "any leadership transition requires `leadershipState != vacant`",
            "future explicit vacancy-fill feature",
            "single-head invariant",
            "supportingResidentIds[] must never include the incumbent resident"
        })
        {
            Assert.Contains(requiredTerm, matrix, StringComparison.Ordinal);
        }
    }

    [Fact]
    public void LegacyShiningNativeFactionDiscoveryContractIsDocumentedForGm()
    {
        var matrix = ReadRepoFile("OtherGuides", "Afterlife_Contract_Matrix.md");
        var examples = ReadRepoFile("Examples", "E_CLI_Afterlife_Turns.txt");
        var taskGuide = ReadRepoFile("TaskGuides", "CLI_Step_Main.txt");
        var operations = ReadRepoFile("Rules", "Block_CLI_Operations.txt");
        var daemonSpec = ReadRepoFile("CLI_Agent_Daemon_Specification.md");
        var apiSpec = ReadRepoFile("CLI_API_Specification.md");

        foreach (var text in new[] { matrix, examples, taskGuide, operations, daemonSpec, apiSpec })
        {
            Assert.Contains("pendingNativeFactionDiscovery", text, StringComparison.Ordinal);
            Assert.Contains("legacy", text, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("discover_native_faction", text, StringComparison.Ordinal);
            Assert.Contains("coreActionReceipts", text, StringComparison.Ordinal);
        }

        foreach (var text in new[] { matrix, examples, taskGuide, operations, apiSpec })
        {
            Assert.Contains("costFeathers", text, StringComparison.Ordinal);
            Assert.Contains("costLightSparks", text, StringComparison.Ordinal);
            Assert.Contains("pending_shining_abode_actions.json", text, StringComparison.Ordinal);
        }
    }

    [Fact]
    public void AfterlifePendingFilesMentionedByRuntimeAreCoveredByMatrix()
    {
        var matrix = ReadRepoFile("OtherGuides", "Afterlife_Contract_Matrix.md");
        var pendingFiles = Directory
            .EnumerateFiles(Path.Combine(TestRepoPaths.RepoRoot, "BookOfEternityClient"), "*.cs", SearchOption.AllDirectories)
            .SelectMany(path => Regex.Matches(File.ReadAllText(path), @"pending_[A-Za-z0-9_]+\.json")
                .Select(match => match.Value))
            .Where(IsAfterlifePendingFile)
            .Distinct(StringComparer.Ordinal)
            .OrderBy(value => value, StringComparer.Ordinal)
            .ToArray();

        Assert.NotEmpty(pendingFiles);

        foreach (var pendingFile in pendingFiles)
        {
            Assert.Contains($"`{pendingFile}`", matrix, StringComparison.Ordinal);
        }
    }

    [Fact]
    public void AfterlifeClientOwnedControlFilesAreCoveredByMatrixAndExamples()
    {
        var matrix = ReadRepoFile("OtherGuides", "Afterlife_Contract_Matrix.md");
        var examples = ReadRepoFile("Examples", "E_CLI_Afterlife_Turns.txt");

        foreach (var fileName in new[]
        {
            "system_guardian_attraction.json",
            "afterlife_return_guard.json"
        })
        {
            Assert.Contains($"`{fileName}`", matrix, StringComparison.Ordinal);
            Assert.Contains(fileName, examples, StringComparison.Ordinal);
        }

        foreach (var requiredTerm in new[]
        {
            "pendingGuardianCreation",
            "system_preset",
            "sourcePreset",
            "guardian_forced",
            "fail-closed"
        })
        {
            Assert.Contains(requiredTerm, matrix, StringComparison.Ordinal);
            Assert.Contains(requiredTerm, examples, StringComparison.Ordinal);
        }

        Assert.Contains("[CHAOS_SEA_SYSTEM_GUARDIAN_ATTRACTION:", examples, StringComparison.Ordinal);
    }

    [Fact]
    public void FounderAttractionResidentRosterModeIsDocumentedForGm()
    {
        var matrix = ReadRepoFile("OtherGuides", "Afterlife_Contract_Matrix.md");
        var examples = ReadRepoFile("Examples", "E_CLI_Afterlife_Turns.txt");
        var manifest = ReadRepoFile("Examples", "example_validation_manifest.json");

        foreach (var doc in new[] { matrix, examples })
        {
            Assert.Contains(GuardianAbodeResidentRequestState.ResidentsRequestModeFounderAttraction, doc, StringComparison.Ordinal);
            Assert.Contains("founderFeatureTitle", doc, StringComparison.Ordinal);
            Assert.Contains("founderFeatureSummary", doc, StringComparison.Ordinal);
            Assert.Contains("old-patron residents", doc, StringComparison.OrdinalIgnoreCase);
        }

        Assert.Contains("afterlife_founder_attraction_resident_roster_response", manifest, StringComparison.Ordinal);
        Assert.Contains("abode_roster_founder_lumen_318", manifest, StringComparison.Ordinal);
    }

    [Fact]
    public void AfterlifePlayerActionRoutingTagsAreCoveredByPromptDocs()
    {
        var matrix = ReadRepoFile("OtherGuides", "Afterlife_Contract_Matrix.md");
        var apiSpec = ReadRepoFile("CLI_API_Specification.md");
        var taskGuide = ReadRepoFile("TaskGuides", "CLI_Step_Main.txt");
        var examples = ReadRepoFile("Examples", "E_CLI_Afterlife_Turns.txt");
        var docs = new[] { matrix, apiSpec, taskGuide, examples };

        var requiredTags = new[]
        {
            "[ABODE_RESIDENT_RELIC_GRANT]",
            "[ABODE_RESIDENT_QUEST_REQUEST]",
            $"[{GuardianTradeRequestState.ActionTag}]",
            $"[{AfterlifeArchiveActionState.ConsultationActionTag}]",
            $"[{AfterlifeArchiveActionState.ProjectFuelActionTag}]",
            "[ABODE_RESIDENT_ROSTER_REQUEST]",
            $"[{PlayerGuardianFoundationState.ActionTag}]"
        };

        foreach (var doc in docs)
        {
            foreach (var tag in requiredTags)
                Assert.Contains(tag, doc, StringComparison.Ordinal);
        }

        foreach (var requiredSurface in new[]
        {
            "metaStateUpdates.soulRelicOperations.addRelic",
            "relatedAfterlifeResidentId",
            "residentInteractionLogUpdates",
            "new current-turn",
            "already granted",
            "unchanged pre-existing",
            "UpdateGuardianTradeInventoryReceipts",
            "archiveActionResolutions",
            "UpdateGuardianAbodeResidentRosterReceipts",
            "playerGuardianFoundationHistory"
        })
        {
            Assert.Contains(requiredSurface, matrix, StringComparison.Ordinal);
            Assert.Contains(requiredSurface, examples, StringComparison.Ordinal);
        }

        Assert.Contains("no pending file", examples, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("no UpdateGuardianAbodeResidentInteractionReceipts", examples, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void AfterlifePreviewOwnershipRulesDistinguishGmContractsFromClientLocalMutations()
    {
        var matrix = ReadRepoFile("OtherGuides", "Afterlife_Contract_Matrix.md");
        var examples = ReadRepoFile("Examples", "E_CLI_Afterlife_Turns.txt");
        var actionPreviews = ReadRepoFile("BookOfEternityClient", "UI", "ExplorerMode", "ExplorerMode.Afterlife.ChaosSea.ActionPreviews.cs");
        var soulRelicPreview = ReadRepoFile("BookOfEternityClient", "UI", "ExplorerMode", "ExplorerMode.Afterlife.SoulRelicsArchiveInbox.cs");

        foreach (var text in new[] { matrix, examples })
        {
            Assert.Contains("client-local mutation", text, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("no GM turn", text, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("no pending/control file", text, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("Soul Relic equip/unequip", text, StringComparison.OrdinalIgnoreCase);
        }

        Assert.Contains("contract-backed preview", matrix, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("no receipt", examples, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("no progression_report.json", examples, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("no gm_thoughts_markdown", examples, StringComparison.OrdinalIgnoreCase);

        Assert.Contains("AppendChaosSeaLocalPreviewRules", actionPreviews, StringComparison.Ordinal);
        Assert.Contains("AppendChaosSeaLocalPreviewRules(equipLines)", soulRelicPreview, StringComparison.Ordinal);
        Assert.Contains("AppendChaosSeaLocalPreviewRules(unequipLines)", soulRelicPreview, StringComparison.Ordinal);
        Assert.DoesNotContain("AppendChaosSeaCommonContractRules(equipLines)", soulRelicPreview, StringComparison.Ordinal);
        Assert.DoesNotContain("AppendChaosSeaCommonContractRules(unequipLines)", soulRelicPreview, StringComparison.Ordinal);
    }

    [Fact]
    public void ChaosSeaHighCostPreviewAuditSurfacesAreDocumentedForGm()
    {
        var matrix = ReadRepoFile("OtherGuides", "Afterlife_Contract_Matrix.md");
        var examples = ReadRepoFile("Examples", "E_CLI_Afterlife_Turns.txt");
        var inkFeatherPreview = ReadRepoFile("BookOfEternityClient", "UI", "ExplorerMode", "ExplorerMode.Afterlife.InkFeathersAndOfferings.cs");
        var tradePreview = ReadRepoFile("BookOfEternityClient", "UI", "ExplorerMode", "ExplorerMode.Afterlife.GuardiansProjectsTrade.cs");
        var inboxPreview = ReadRepoFile("BookOfEternityClient", "UI", "ExplorerMode", "ExplorerMode.Afterlife.SoulRelicsArchiveInbox.cs");

        foreach (var text in new[] { matrix, examples })
        {
            Assert.Contains("Soul Imprint", text, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("stateEvidence.imprintId", text, StringComparison.Ordinal);
            Assert.Contains("baseDelta", text, StringComparison.Ordinal);
            Assert.Contains("finalDelta", text, StringComparison.Ordinal);
            Assert.Contains("buybackEntryId", text, StringComparison.Ordinal);
            Assert.Contains("projectedFeathers", text, StringComparison.Ordinal);
            Assert.Contains("historyEntryId", text, StringComparison.Ordinal);
            Assert.Contains("archiveActionResolutions", text, StringComparison.Ordinal);
        }

        Assert.Contains("BuildSoulImprintPreviewAuditLines", inkFeatherPreview, StringComparison.Ordinal);
        Assert.Contains("BuildAbodeOfferingPreviewAuditLines", inkFeatherPreview, StringComparison.Ordinal);
        Assert.Contains("BuildGuardianBuybackAuditNode", tradePreview, StringComparison.Ordinal);
        Assert.Contains("BuildResidentNotificationReceiptAuditLines", inboxPreview, StringComparison.Ordinal);
    }

    [Fact]
    public void AfterlifeSocialRoutingTagsResponseModesAndTransferMetadataAreDocumented()
    {
        var matrix = ReadRepoFile("OtherGuides", "Afterlife_Contract_Matrix.md");
        var apiSpec = ReadRepoFile("CLI_API_Specification.md");
        var taskGuide = ReadRepoFile("TaskGuides", "CLI_Step_Main.txt");
        var daemonSpec = ReadRepoFile("CLI_Agent_Daemon_Specification.md");
        var examples = ReadRepoFile("Examples", "E_CLI_Afterlife_Turns.txt");
        var docs = new[] { matrix, apiSpec, taskGuide, daemonSpec, examples };

        foreach (var tag in new[]
        {
            "[GUARDIAN_SOCIAL_TALK_REQUEST]",
            "[GUARDIAN_SOCIAL_LORE_REQUEST]",
            "[ABODE_RESIDENT_HISTORY_REQUEST]",
            "[ABODE_RESIDENT_TALK]",
            "[ABODE_RESIDENT_TRANSFER_REQUEST]"
        })
        {
            foreach (var doc in docs)
                Assert.Contains(tag, doc, StringComparison.Ordinal);
        }

        foreach (var responseMode in new[]
        {
            ActorSocialInteractionRequestState.ResponseModeTalkScene,
            ActorSocialInteractionRequestState.ResponseModeLoreRevealed,
            ActorSocialInteractionRequestState.ResponseModeLoreRefused,
            ActorSocialInteractionRequestState.ResponseModeWarning,
            ActorSocialInteractionRequestState.ResponseModeRefusal,
            ActorSocialInteractionRequestState.ResponseModeTrustShift,
            ActorSocialInteractionRequestState.ResponseModeAttitudeShift,
            GuardianAbodeResidentState.ResponseModeHistoryRevealed,
            GuardianAbodeResidentState.ResponseModeHistoryRefused,
            GuardianAbodeResidentState.ResponseModeHistoryPartial,
            GuardianAbodeResidentState.ResponseModeBondShiftOnly
        })
        {
            foreach (var doc in new[] { matrix, apiSpec, taskGuide, examples })
                Assert.Contains(responseMode, doc, StringComparison.Ordinal);
        }

        foreach (var transferTerm in new[]
        {
            GuardianAbodeResidentRequestState.TransferSelectionModeCompetitionRecommended,
            GuardianAbodeResidentRequestState.TransferSelectionModeManualOverride,
            GuardianAbodeResidentRequestState.TransferSelectionModeDepartureOnly,
            "competitionScore",
            "competitionLabel",
            "competitionReason"
        })
        {
            foreach (var doc in new[] { matrix, apiSpec, taskGuide, examples })
                Assert.Contains(transferTerm, doc, StringComparison.Ordinal);
        }
    }

    [Fact]
    public void ResidentTransferCompetitionLabelsAreDocumentedForGm()
    {
        var matrix = ReadRepoFile("OtherGuides", "Afterlife_Contract_Matrix.md");
        var taskGuide = ReadRepoFile("TaskGuides", "CLI_Step_Main.txt");
        var examples = ReadRepoFile("Examples", "E_CLI_Afterlife_Turns.txt");
        var docs = new[] { matrix, taskGuide, examples };

        var labels = typeof(GuardianAbodeResidentState)
            .GetFields(BindingFlags.Public | BindingFlags.Static | BindingFlags.FlattenHierarchy)
            .Where(field => field is { IsLiteral: true, IsInitOnly: false } &&
                            field.FieldType == typeof(string) &&
                            field.Name.StartsWith("TransferCompetitionLabel", StringComparison.Ordinal))
            .Select(field => (string)field.GetRawConstantValue()!)
            .OrderBy(value => value, StringComparer.Ordinal)
            .ToArray();

        Assert.Equal(
            new[]
            {
                GuardianAbodeResidentState.TransferCompetitionLabelPlausiblePull,
                GuardianAbodeResidentState.TransferCompetitionLabelStrongPull,
                GuardianAbodeResidentState.TransferCompetitionLabelWeakPull
            }.OrderBy(value => value, StringComparer.Ordinal),
            labels);

        foreach (var doc in docs)
        {
            Assert.Contains("competitionLabel", doc, StringComparison.Ordinal);
            foreach (var label in labels)
                Assert.Contains(label, doc, StringComparison.Ordinal);
        }
    }

    [Fact]
    public void AfterlifeArchiveUpdatesAndDerivedNotificationTriggersAreDocumented()
    {
        var matrix = ReadRepoFile("OtherGuides", "Afterlife_Contract_Matrix.md");
        var apiSpec = ReadRepoFile("CLI_API_Specification.md");
        var taskGuide = ReadRepoFile("TaskGuides", "CLI_Step_Main.txt");
        var examples = ReadRepoFile("Examples", "E_CLI_Afterlife_Turns.txt");

        var notificationTypes = typeof(AfterlifeNotificationState)
            .GetFields(BindingFlags.Public | BindingFlags.Static | BindingFlags.FlattenHierarchy)
            .Where(field => field is { IsLiteral: true, IsInitOnly: false } &&
                            field.FieldType == typeof(string) &&
                            field.Name.StartsWith("Type", StringComparison.Ordinal))
            .Select(field => (string)field.GetRawConstantValue()!)
            .OrderBy(value => value, StringComparer.Ordinal)
            .ToArray();

        Assert.NotEmpty(notificationTypes);

        foreach (var notificationType in notificationTypes)
            Assert.Contains(notificationType, matrix, StringComparison.Ordinal);

        foreach (var term in new[]
        {
            "afterlifeArchiveUpdates",
            "afterlife_notifications.json",
            "pendingShiningBlessingEffects"
        })
        {
            foreach (var doc in new[] { matrix, apiSpec, taskGuide, examples })
                Assert.Contains(term, doc, StringComparison.OrdinalIgnoreCase);
        }

        foreach (var term in new[]
        {
            "command",
            "add",
            "remove",
            "archiveId",
            "entryType",
            "sourceLife",
            "acquiredAtUtc"
        })
        {
            foreach (var doc in new[] { matrix, apiSpec, examples })
                Assert.Contains(term, doc, StringComparison.OrdinalIgnoreCase);
        }
    }

    [Fact]
    public void AfterlifeLocalLifecycleRoutesAreDocumented()
    {
        var matrix = ReadRepoFile("OtherGuides", "Afterlife_Contract_Matrix.md");
        var apiSpec = ReadRepoFile("CLI_API_Specification.md");
        var daemonSpec = ReadRepoFile("CLI_Agent_Daemon_Specification.md");
        var examples = ReadRepoFile("Examples", "E_CLI_Afterlife_Turns.txt");

        foreach (var routeName in new[] { "return_to_chaos_sea", "reenter_shining_abode" })
        {
            foreach (var doc in new[] { matrix, apiSpec, daemonSpec })
                Assert.Contains(routeName, doc, StringComparison.Ordinal);
        }

        foreach (var doc in new[] { matrix, apiSpec })
        {
            Assert.Contains("client-owned", doc, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("No GM-authored output", doc, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("requests", doc, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("malformed", doc, StringComparison.OrdinalIgnoreCase);
        }

        var daemonReturnSection = ExtractRequiredSection(
            daemonSpec,
            "- explicit client-owned local `return_to_chaos_sea`",
            "- For Shining pending files with a `requests[]` root");
        Assert.Contains("pending_source_of_light_capstone.json", daemonReturnSection, StringComparison.Ordinal);
        Assert.Contains("afterlife_spiritual_conflict_state.json.activeConflict", daemonReturnSection, StringComparison.Ordinal);
        var apiReturnSection = ExtractRequiredSection(
            apiSpec,
            "- Client-owned `return_to_chaos_sea`",
            "- `Shining Abode pending-bootstrap handoff mode`");
        Assert.Contains("pending_source_of_light_capstone.json", apiReturnSection, StringComparison.Ordinal);
        Assert.Contains("afterlife_spiritual_conflict_state.json.activeConflict", apiReturnSection, StringComparison.Ordinal);
        var matrixReturnSection = ExtractRequiredSection(
            matrix,
            "| Client-owned `return_to_chaos_sea`",
            "| Client-owned Shining Gates");
        Assert.Contains("pending_source_of_light_capstone.json", matrixReturnSection, StringComparison.Ordinal);
        Assert.Contains("afterlife_spiritual_conflict_state.json.activeConflict", matrixReturnSection, StringComparison.Ordinal);
        var examplesReturnSection = ExtractRequiredSection(
            examples,
            "- For `return_to_chaos_sea`",
            "- The response does not confuse");
        Assert.Contains("pending_source_of_light_capstone.json", examplesReturnSection, StringComparison.Ordinal);
        Assert.Contains("afterlife_spiritual_conflict_state.json.activeConflict", examplesReturnSection, StringComparison.Ordinal);

        Assert.Contains("{ \"requests\": [] }", matrix, StringComparison.Ordinal);
        Assert.Contains("{ \"requests\": [] }", apiSpec, StringComparison.Ordinal);
    }

    [Fact]
    public void AfterlifePromptDocsDescribeHandoffAsTriggerOnlyAndFileLevelForbiddenRule()
    {
        var matrix = ReadRepoFile("OtherGuides", "Afterlife_Contract_Matrix.md");
        var apiSpec = ReadRepoFile("CLI_API_Specification.md");
        var daemonSpec = ReadRepoFile("CLI_Agent_Daemon_Specification.md");
        var examples = ReadRepoFile("Examples", "E_CLI_Afterlife_Turns.txt");
        var launchScript = ReadRepoFile("BookOfEternityClient", "Launcher", "CLI_Launch_Script.md");
        var launchGenerator = ReadRepoFile("BookOfEternityClient", "Launcher", "Generate_CLI_Launch_Script.ps1");
        var taskGuide = ReadRepoFile("TaskGuides", "CLI_Step_Main.txt");
        var operations = ReadRepoFile("Rules", "Block_CLI_Operations.txt");
        var lifecyclePrompt = ReadRepoFile("BookOfEternityClient", "Core", "GameEngine", "GameEngine.TurnLifecycle.cs");
        var docs = new[] { matrix, apiSpec, daemonSpec, launchScript, launchGenerator };

        foreach (var doc in docs)
        {
            Assert.Contains("TriggerIncarnation", doc, StringComparison.Ordinal);
            Assert.Contains("game_state/world/*", doc, StringComparison.Ordinal);
            Assert.Contains("game_state/npcs/*", doc, StringComparison.Ordinal);
            Assert.Contains("game_state/factions/*", doc, StringComparison.Ordinal);
        }

        foreach (var doc in new[] { launchScript, launchGenerator })
        {
            foreach (var forbiddenSurface in new[]
            {
                "game_state/core/player_status.json",
                "game_state/player/*",
                "game_state/inventory/*",
                "game_state/world/*",
                "game_state/npcs/*",
                "game_state/combat/*",
                "game_state/factions/*",
                "lore/current_world/*",
                "game_state/quests/regular_quests.json",
                "game_state/quests/quest_history.json",
                "game_state/quests/plot_outline.json",
                "game_state/meta/characteristics.json",
                "game_state/meta/vehicles.json",
                "game_state/meta/storage_access.json",
                "game_state/meta/player_interactions.json"
            })
            {
                Assert.Contains(forbiddenSurface, doc, StringComparison.Ordinal);
            }

            Assert.Contains("no unresolved or malformed afterlife pending/control contracts", doc, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("legacy `pendingNativeFactionDiscovery`", doc, StringComparison.Ordinal);
        }

        foreach (var doc in new[] { apiSpec, daemonSpec, launchScript, launchGenerator })
        {
            Assert.Contains("client", doc, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("Mortal bootstrap", doc, StringComparison.OrdinalIgnoreCase);
        }

        foreach (var doc in new[] { matrix, apiSpec, daemonSpec })
        {
            Assert.Contains("TriggerIncarnation", doc, StringComparison.Ordinal);
            Assert.Contains("preserve", doc, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("preparedIncarnationPackage", doc, StringComparison.Ordinal);
        }

        foreach (var doc in new[] { matrix, examples })
        {
            Assert.Contains("validated accepted-turn authority", doc, StringComparison.Ordinal);
            Assert.Contains("game_state/control/pending_turn_snapshot", doc, StringComparison.Ordinal);
            Assert.Contains("game_state/control/pending_turn_snapshot.authority.json", doc, StringComparison.Ordinal);
            Assert.Contains("incarnation_trigger_invalid_validated_snapshot_context", doc, StringComparison.Ordinal);
            Assert.Contains("no unresolved afterlife pending/control contracts", doc, StringComparison.OrdinalIgnoreCase);
        }

        var matrixHandoffSection = ExtractRequiredSection(
            matrix,
            "| Shining pending-bootstrap `TriggerIncarnation`",
            "| Client-owned `return_to_chaos_sea`");
        Assert.Contains("pending_source_of_light_capstone.json", matrixHandoffSection, StringComparison.Ordinal);
        Assert.Contains("afterlife_spiritual_conflict_state.json.activeConflict", matrixHandoffSection, StringComparison.Ordinal);
        var apiHandoffSection = ExtractRequiredSection(
            apiSpec,
            "- In `Shining Abode pending-bootstrap handoff mode`",
            "- Client-owned `return_to_chaos_sea`");
        Assert.Contains("pending_source_of_light_capstone.json", apiHandoffSection, StringComparison.Ordinal);
        Assert.Contains("afterlife_spiritual_conflict_state.json.activeConflict", apiHandoffSection, StringComparison.Ordinal);
        var launchHandoffSection = ExtractRequiredSection(
            launchScript,
            "- If `currentRealm = \"Shining Abode\"",
            "- **null / empty / missing**");
        Assert.Contains("pending_source_of_light_capstone.json", launchHandoffSection, StringComparison.Ordinal);
        var launchGeneratorHandoffSection = ExtractRequiredSection(
            launchGenerator,
            "- If `currentRealm = \"Shining Abode\"",
            "- **null / empty / missing**");
        Assert.Contains("pending_source_of_light_capstone.json", launchGeneratorHandoffSection, StringComparison.Ordinal);
        var examplesHandoffSection = ExtractRequiredSection(
            examples,
            "- No unresolved afterlife pending/control contracts remain;",
            "- Ordinary Shining living-world progression must NOT run in this handoff turn.");
        Assert.Contains("pending_source_of_light_capstone.json", examplesHandoffSection, StringComparison.Ordinal);
        Assert.Contains("afterlife_spiritual_conflict_state.json.activeConflict", examplesHandoffSection, StringComparison.Ordinal);

        Assert.Contains("game_state/control/pending_turn_snapshot.authority.json", apiSpec, StringComparison.Ordinal);
        Assert.Contains("client-owned transient authority", apiSpec, StringComparison.Ordinal);

        Assert.DoesNotContain("ONLY bootstrap/materialization", launchScript, StringComparison.Ordinal);
        Assert.DoesNotContain("ONLY bootstrap/materialization", launchGenerator, StringComparison.Ordinal);
        foreach (var prompt in new[] { taskGuide, operations, lifecyclePrompt, daemonSpec })
            Assert.DoesNotContain("process bootstrap only", prompt, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("GM sends player to Mortal World", apiSpec, StringComparison.Ordinal);
        Assert.Contains("validation repair", daemonSpec, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("runtime normalization clears", daemonSpec, StringComparison.Ordinal);

        var foundationState = ReadRepoFile("BookOfEternityClient", "Services", "PlayerGuardianFoundationState.cs");
        Assert.DoesNotContain("world events, and afterlife notifications", foundationState, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("allowed afterlife surfaces", foundationState, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Do NOT write Mortal World events or client-derived afterlife_notifications", foundationState, StringComparison.Ordinal);
    }

    [Fact]
    public void ShiningResidentAscensionDocsMatchNormalizerBehavior()
    {
        var matrix = ReadRepoFile("OtherGuides", "Afterlife_Contract_Matrix.md");
        var apiSpec = ReadRepoFile("CLI_API_Specification.md");
        var daemonSpec = ReadRepoFile("CLI_Agent_Daemon_Specification.md");
        var taskGuide = ReadRepoFile("TaskGuides", "CLI_Step_Main.txt");
        var operations = ReadRepoFile("Rules", "Block_CLI_Operations.txt");
        var examples = ReadRepoFile("Examples", "E_CLI_Afterlife_Turns.txt");

        foreach (var doc in new[] { matrix, apiSpec, daemonSpec, taskGuide, operations, examples })
        {
            Assert.Contains("ascended but unaffiliated", doc, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("remained_in_chaos_sea", doc, StringComparison.Ordinal);
            Assert.Contains("shiningFactionId", doc, StringComparison.Ordinal);
            Assert.Contains("shiningAlignment", doc, StringComparison.Ordinal);
        }

        foreach (var doc in new[] { matrix, apiSpec, daemonSpec, examples })
            Assert.Contains("preserves", doc, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void NextLifeScenarioCoreIsDocumentedAsClientOwnedBootstrapInput()
    {
        var matrix = ReadRepoFile("OtherGuides", "Afterlife_Contract_Matrix.md");
        var apiSpec = ReadRepoFile("CLI_API_Specification.md");
        var daemonSpec = ReadRepoFile("CLI_Agent_Daemon_Specification.md");
        var taskGuide = ReadRepoFile("TaskGuides", "CLI_Step_Main.txt");
        var operations = ReadRepoFile("Rules", "Block_CLI_Operations.txt");
        var examples = ReadRepoFile("Examples", "E_CLI_Afterlife_Turns.txt");

        foreach (var doc in new[] { matrix, apiSpec, daemonSpec, taskGuide, operations, examples })
        {
            Assert.Contains("next_life_scenario_core.json", doc, StringComparison.Ordinal);
            Assert.Contains("client-owned", doc, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("scenarioCoreAssertions", doc, StringComparison.Ordinal);
            Assert.Contains("candidateAssertions", doc, StringComparison.Ordinal);
            Assert.Contains("edit", doc, StringComparison.OrdinalIgnoreCase);
        }
    }

    [Fact]
    public void LauncherRealmCheckNamesCanonicalSoulStateSource()
    {
        var launchScript = ReadRepoFile("BookOfEternityClient", "Launcher", "CLI_Launch_Script.md");
        var launchGenerator = ReadRepoFile("BookOfEternityClient", "Launcher", "Generate_CLI_Launch_Script.ps1");

        foreach (var doc in new[] { launchScript, launchGenerator })
        {
            Assert.Contains("game_state/meta/soul_state.json.currentRealm", doc, StringComparison.Ordinal);
            Assert.Contains("Context.worldState.currentRealm", doc, StringComparison.Ordinal);
            Assert.DoesNotContain("Read `worldState.currentRealm` from game state.", doc, StringComparison.Ordinal);
        }
    }

    [Fact]
    public void AfterlifePromptDocsDescribeGuardianProjectStartAndShiningFactionTradeSurfaces()
    {
        var matrix = ReadRepoFile("OtherGuides", "Afterlife_Contract_Matrix.md");
        var apiSpec = ReadRepoFile("CLI_API_Specification.md");
        var daemonSpec = ReadRepoFile("CLI_Agent_Daemon_Specification.md");
        var operations = ReadRepoFile("Rules", "Block_CLI_Operations.txt");
        var docs = new[] { matrix, apiSpec, daemonSpec, operations };

        foreach (var doc in docs)
        {
            Assert.Contains("startGuardianProjects", doc, StringComparison.Ordinal);
            Assert.Contains("faction `tradeInventory`", doc, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("tradeInventoryReceipts", doc, StringComparison.Ordinal);
        }

        Assert.Contains("do not use Guardian trade inventory", operations, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Do not use Guardian trade inventory", apiSpec, StringComparison.Ordinal);
    }

    [Fact]
    public void AfterlifePromptDocsDescribeReturnGuardAndRealmWrongPendingSemantics()
    {
        var matrix = ReadRepoFile("OtherGuides", "Afterlife_Contract_Matrix.md");
        var apiSpec = ReadRepoFile("CLI_API_Specification.md");
        var daemonSpec = ReadRepoFile("CLI_Agent_Daemon_Specification.md");
        var operations = ReadRepoFile("Rules", "Block_CLI_Operations.txt");
        var taskGuide = ReadRepoFile("TaskGuides", "CLI_Step_Main.txt");
        var examples = ReadRepoFile("Examples", "E_CLI_Afterlife_Turns.txt");
        var implementationPlan = ReadRepoFile("OtherGuides", "Shining_Abode_Implementation_Plan.md");
        var endgamePlan = ReadRepoFile("OtherGuides", "Shining_Abode_Endgame_Design_Plan.md");

        foreach (var doc in new[] { matrix, apiSpec, daemonSpec, examples, implementationPlan, endgamePlan })
        {
            Assert.Contains("afterlife_return_guard.json", doc, StringComparison.Ordinal);
            Assert.Contains("fail-closed", doc, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("explicit client/runtime clear", doc, StringComparison.OrdinalIgnoreCase);
        }

        foreach (var doc in new[] { matrix, apiSpec, daemonSpec, operations, taskGuide, examples })
        {
            Assert.Contains("pending_resident_companion_manifestation_request.json", doc, StringComparison.Ordinal);
            Assert.Contains("MortalWorldProfile-only", doc, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("malformed", doc, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("repair", doc, StringComparison.OrdinalIgnoreCase);
        }

        Assert.DoesNotContain("runtime normalization clears", implementationPlan, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("runtime health normalization clears", implementationPlan, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("runtime-normalization", endgamePlan, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void AfterlifePromptDocsCoverRealmSegregationAndMortalOnlyPendingFiles()
    {
        var matrix = ReadRepoFile("OtherGuides", "Afterlife_Contract_Matrix.md");
        var apiSpec = ReadRepoFile("CLI_API_Specification.md");
        var daemonSpec = ReadRepoFile("CLI_Agent_Daemon_Specification.md");
        var taskGuide = ReadRepoFile("TaskGuides", "CLI_Step_Main.txt");
        var examples = ReadRepoFile("Examples", "E_CLI_Afterlife_Turns.txt");
        var lifecyclePrompt = ReadRepoFile("BookOfEternityClient", "Core", "GameEngine", "GameEngine.TurnLifecycle.cs");
        var docs = new[] { matrix, apiSpec, daemonSpec, taskGuide, examples };

        foreach (var doc in docs)
        {
            Assert.Contains("game_state/world/*", doc, StringComparison.Ordinal);
            Assert.Contains("game_state/npcs/*", doc, StringComparison.Ordinal);
            Assert.Contains("game_state/factions/*", doc, StringComparison.Ordinal);
            Assert.Contains("pending_npc_social_interactions.json", doc, StringComparison.Ordinal);
            Assert.Contains("pending_npc_trade_inventory_requests.json", doc, StringComparison.Ordinal);
            Assert.Contains("[NPC_TRADE_REQUEST]", doc, StringComparison.Ordinal);
            Assert.Contains("MortalWorldProfile-only", doc, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("repair", doc, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("wrong-realm", doc, StringComparison.OrdinalIgnoreCase);
        }

        Assert.Contains("pending_npc_trade_inventory_requests.json", lifecyclePrompt, StringComparison.Ordinal);
        Assert.Contains("[NPC_TRADE_REQUEST]", lifecyclePrompt, StringComparison.Ordinal);
        Assert.Contains("MortalWorldProfile-only", lifecyclePrompt, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("repair", lifecyclePrompt, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("wrong-realm", lifecyclePrompt, StringComparison.OrdinalIgnoreCase);
        foreach (var afterlifeAuthorityPath in new[]
        {
            "game_state/meta/guardian_abode_residents.json",
            "game_state/meta/guardian_thought_journal.json",
            "game_state/meta/guardian_social_journal.json",
            "game_state/meta/guardian_projects.json",
            "game_state/meta/guardian_project_journal.json",
            "game_state/meta/abode_power_journal.json",
            "game_state/meta/shining_abode_state.json"
        })
        {
            Assert.Contains(afterlifeAuthorityPath, matrix, StringComparison.Ordinal);
            Assert.Contains(afterlifeAuthorityPath, apiSpec, StringComparison.Ordinal);
            Assert.Contains(afterlifeAuthorityPath, daemonSpec, StringComparison.Ordinal);
            Assert.Contains(afterlifeAuthorityPath, taskGuide, StringComparison.Ordinal);
            Assert.Contains(afterlifeAuthorityPath, lifecyclePrompt, StringComparison.Ordinal);
        }

        Assert.Contains(FileMapping.FieldToFile, pair =>
            pair.Value.StartsWith("game_state/world/", StringComparison.OrdinalIgnoreCase) ||
            pair.Value.StartsWith("game_state/npcs/", StringComparison.OrdinalIgnoreCase) ||
            pair.Value.StartsWith("game_state/factions/", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void AfterlifePromptDocsCoverFullRuntimeForbiddenFileGroups()
    {
        var matrix = ReadRepoFile("OtherGuides", "Afterlife_Contract_Matrix.md");
        var apiSpec = ReadRepoFile("CLI_API_Specification.md");
        var daemonSpec = ReadRepoFile("CLI_Agent_Daemon_Specification.md");
        var taskGuide = ReadRepoFile("TaskGuides", "CLI_Step_Main.txt");
        var examples = ReadRepoFile("Examples", "E_CLI_Afterlife_Turns.txt");

        foreach (var doc in new[] { matrix, apiSpec, examples })
        {
            foreach (var forbiddenGroup in new[]
            {
                "game_state/core/player_status.json",
                "game_state/player/*",
                "game_state/inventory/*",
                "game_state/world/*",
                "game_state/npcs/*",
                "game_state/combat/*",
                "game_state/factions/*",
                "lore/current_world/*",
                "game_state/quests/regular_quests.json",
                "quest_history.json",
                "plot_outline.json",
                "game_state/misc/characteristics.json",
                "vehicles.json",
                "storage_access.json",
                "player_interactions.json"
            })
            {
                Assert.Contains(forbiddenGroup, doc, StringComparison.Ordinal);
            }
        }

        foreach (var doc in new[] { daemonSpec, taskGuide })
        {
            Assert.Contains("game_state/core/player_status.json", doc, StringComparison.Ordinal);
            Assert.Contains("game_state/combat/*", doc, StringComparison.Ordinal);
            Assert.Contains("lore/current_world/*", doc, StringComparison.Ordinal);
            Assert.Contains("Mortal misc", doc, StringComparison.OrdinalIgnoreCase);
        }

        var forbiddenRuntimeTargets = FileMapping.FieldToFile.Values
            .Where(path =>
                path.Equals("game_state/core/player_status.json", StringComparison.OrdinalIgnoreCase) ||
                path.StartsWith("game_state/player/", StringComparison.OrdinalIgnoreCase) ||
                path.StartsWith("game_state/inventory/", StringComparison.OrdinalIgnoreCase) ||
                path.StartsWith("game_state/world/", StringComparison.OrdinalIgnoreCase) ||
                path.StartsWith("game_state/npcs/", StringComparison.OrdinalIgnoreCase) ||
                path.StartsWith("game_state/combat/", StringComparison.OrdinalIgnoreCase) ||
                path.StartsWith("game_state/factions/", StringComparison.OrdinalIgnoreCase))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        Assert.NotEmpty(forbiddenRuntimeTargets);
    }

    [Fact]
    public void AfterlifePromptDocsTreatProgressionScheduleAsClientOwned()
    {
        var matrix = ReadRepoFile("OtherGuides", "Afterlife_Contract_Matrix.md");
        var apiSpec = ReadRepoFile("CLI_API_Specification.md");
        var daemonSpec = ReadRepoFile("CLI_Agent_Daemon_Specification.md");
        var taskGuide = ReadRepoFile("TaskGuides", "CLI_Step_Main.txt");
        var examples = ReadRepoFile("Examples", "E_CLI_Afterlife_Turns.txt");
        var docs = new[] { matrix, apiSpec, daemonSpec, taskGuide, examples };

        foreach (var doc in docs)
        {
            Assert.Contains(ProgressionScheduleService.SchedulePath, doc, StringComparison.Ordinal);
            Assert.Contains(ProgressionScheduleService.ReportPath, doc, StringComparison.Ordinal);
            Assert.Contains("client-owned", doc, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("progressionControl", doc, StringComparison.Ordinal);
            Assert.Contains("progressionProcessingReport", doc, StringComparison.Ordinal);
        }

        foreach (var doc in new[] { matrix, examples })
        {
            Assert.Contains("never edit", doc, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("last*Ordinal", doc, StringComparison.Ordinal);
            Assert.Contains("pending cycle counts", doc, StringComparison.OrdinalIgnoreCase);
        }
    }

    [Fact]
    public void ShiningSchedulerAllowanceDocsStayNarrowAcrossGmEntrypoints()
    {
        var docs = new[]
        {
            ReadRepoFile("OtherGuides", "Afterlife_Contract_Matrix.md"),
            ReadRepoFile("CLI_API_Specification.md"),
            ReadRepoFile("CLI_Agent_Daemon_Specification.md"),
            ReadRepoFile("TaskGuides", "CLI_Step_Main.txt"),
            ReadRepoFile("Rules", "Block_CLI_Operations.txt"),
            ReadRepoFile("Examples", "E_CLI_Afterlife_Turns.txt"),
            ReadRepoFile("BookOfEternityClient", "Launcher", "CLI_Launch_Script.md"),
            ReadRepoFile("BookOfEternityClient", "Launcher", "Generate_CLI_Launch_Script.ps1"),
            ReadRepoFile("BookOfEternityClient", "game_master_daemon.ps1"),
            ReadRepoFile("BookOfEternityClient", "Core", "GameEngine", "GameEngine.TurnLifecycle.cs")
        };

        var requiredTokens = new[]
        {
            "scheduler-owned",
            "progressionProcessingReport",
            "availability",
            "coreActionReceipts",
            "gates",
            "gachaSystem.gachaHistory",
            "pendingNativeFactionDiscovery",
            "preparedIncarnationPackage",
            "lightSparks",
            "treasury",
            "sourceOfLightCapstone"
        };

        foreach (var doc in docs)
        foreach (var token in requiredTokens)
            Assert.Contains(token, doc, StringComparison.Ordinal);
    }

    [Fact]
    public void AfterlifePromptDocsCoverGuardianProvocationAndArchiveCandidateManifest()
    {
        var matrix = ReadRepoFile("OtherGuides", "Afterlife_Contract_Matrix.md");
        var apiSpec = ReadRepoFile("CLI_API_Specification.md");
        var daemonSpec = ReadRepoFile("CLI_Agent_Daemon_Specification.md");
        var taskGuide = ReadRepoFile("TaskGuides", "CLI_Step_Main.txt");
        var examples = ReadRepoFile("Examples", "E_CLI_Afterlife_Turns.txt");
        var docs = new[] { matrix, apiSpec, daemonSpec, taskGuide, examples };

        foreach (var doc in docs)
        {
            Assert.Contains("[GUARDIAN_PROVOCATION]", doc, StringComparison.Ordinal);
            Assert.Contains("[GUARDIAN_PROVOCATION: guardianId]", doc, StringComparison.Ordinal);
            Assert.Contains("guardianId", doc, StringComparison.Ordinal);
        }

        foreach (var doc in new[] { matrix, apiSpec, taskGuide, examples })
        {
            Assert.Contains("CurrentReputation", doc, StringComparison.Ordinal);
            Assert.Contains("-21", doc, StringComparison.Ordinal);
            Assert.Contains("-51", doc, StringComparison.Ordinal);
            Assert.Contains("severity", doc, StringComparison.OrdinalIgnoreCase);
        }

        foreach (var doc in new[] { matrix, taskGuide, examples, apiSpec })
        {
            Assert.Contains("archive_candidate_manifest.json", doc, StringComparison.Ordinal);
            Assert.Contains("client-owned", doc, StringComparison.OrdinalIgnoreCase);
        }
    }

    [Fact]
    public void AfterlifePromptDocsCoverAscensionClientExecutedHandoff()
    {
        var matrix = ReadRepoFile("OtherGuides", "Afterlife_Contract_Matrix.md");
        var apiSpec = ReadRepoFile("CLI_API_Specification.md");
        var taskGuide = ReadRepoFile("TaskGuides", "CLI_Step_Main.txt");
        var examples = ReadRepoFile("Examples", "E_CLI_Afterlife_Turns.txt");
        var manifest = ReadRepoFile("Examples", "example_validation_manifest.json");

        foreach (var doc in new[] { matrix, apiSpec, taskGuide, examples })
        {
            Assert.Contains("AscensionTrigger", doc, StringComparison.Ordinal);
            Assert.Contains("playerChoice", doc, StringComparison.Ordinal);
            Assert.Contains("Ascension", doc, StringComparison.Ordinal);
            Assert.Contains("TriggerLifeEnd", doc, StringComparison.Ordinal);
            Assert.Contains("game_state/control/ascension.json", doc, StringComparison.Ordinal);
        }

        Assert.Contains("6B. VALID — ASCENSION HANDOFF IS CLIENT-EXECUTED", examples, StringComparison.Ordinal);
        Assert.Contains("client performs the realm handoff", matrix, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("do not manually switch `soul_state.currentRealm`", examples, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("life_transitions.json", examples, StringComparison.Ordinal);
        Assert.Contains(">= 60", matrix + apiSpec + taskGuide + examples, StringComparison.Ordinal);
        Assert.Contains("experienceGain = costInFeathers * 4", matrix + apiSpec + taskGuide, StringComparison.Ordinal);
        Assert.Contains("afterlife_chaos_ascension_trigger_response", manifest, StringComparison.Ordinal);
    }

    [Fact]
    public void AfterlifePromptDocsCoverSoulPreparationAndGuardianCorrections()
    {
        var matrix = ReadRepoFile("OtherGuides", "Afterlife_Contract_Matrix.md");
        var apiSpec = ReadRepoFile("CLI_API_Specification.md");
        var daemonSpec = ReadRepoFile("CLI_Agent_Daemon_Specification.md");
        var taskGuide = ReadRepoFile("TaskGuides", "CLI_Step_Main.txt");
        var operations = ReadRepoFile("Rules", "Block_CLI_Operations.txt");
        var examples = ReadRepoFile("Examples", "E_CLI_Afterlife_Turns.txt");
        var manifest = ReadRepoFile("Examples", "example_validation_manifest.json");

        foreach (var doc in new[] { matrix, apiSpec, daemonSpec, taskGuide, operations, examples })
        {
            Assert.Contains("soul_preparation", doc, StringComparison.Ordinal);
            Assert.Contains("guardian_corrections.json", doc, StringComparison.Ordinal);
            Assert.Contains("correction_spend", doc, StringComparison.Ordinal);
            Assert.Contains("guardian_corrections", doc, StringComparison.Ordinal);
        }

        foreach (var term in new[]
        {
            "projectOutcomeAudit",
            "effectState",
            "preparationBudgetPoints",
            "preparationClaimPriorityBonus",
            "preparationBudgetPointsGranted",
            "preparationBudgetPointsSpent",
            "preparationClaimPriorityBonusGranted",
            "hostilePriorityTokensGranted",
            "hostilePriorityTokensSpent",
            "consumedAtLifeStart",
            "claimants[]",
            "contestedSlots[]",
            "resolutionOrder[]",
            "corrections[]",
            "sourceSurface=guardian_corrections",
            "afterlife_guardian_correction_spend_reference"
        })
        {
            Assert.Contains(term, matrix + examples + manifest, StringComparison.Ordinal);
        }

        Assert.Contains("\"projectId\": \"gproj_neris_soul_preparation_sabotage_002\"", examples, StringComparison.Ordinal);
        Assert.Contains("\"projectMode\": \"offensive\"", examples, StringComparison.Ordinal);
        Assert.DoesNotContain("\"projectMode\": \"rival\"", examples, StringComparison.Ordinal);
    }

    [Fact]
    public void AfterlifePromptDocsCoverGuardianProjectStartsAndShiningBlessingTerminalAudits()
    {
        var matrix = ReadRepoFile("OtherGuides", "Afterlife_Contract_Matrix.md");
        var apiSpec = ReadRepoFile("CLI_API_Specification.md");
        var daemonSpec = ReadRepoFile("CLI_Agent_Daemon_Specification.md");
        var taskGuide = ReadRepoFile("TaskGuides", "CLI_Step_Main.txt");
        var examples = ReadRepoFile("Examples", "E_CLI_Afterlife_Turns.txt");
        var docs = new[] { matrix, apiSpec, daemonSpec, taskGuide, examples };

        Assert.Contains("startGuardianProjects", matrix, StringComparison.Ordinal);
        foreach (var doc in docs)
        {
            Assert.Contains("pendingShiningBlessingEffects", doc, StringComparison.Ordinal);
            Assert.Contains(ShiningBlessingEffectState.GenericStatusConsumed, doc, StringComparison.Ordinal);
            Assert.Contains(ShiningBlessingEffectState.GenericStatusExpired, doc, StringComparison.Ordinal);
            Assert.Contains("consumedAtTurn", doc, StringComparison.Ordinal);
            Assert.Contains("consumedAtUtc", doc, StringComparison.Ordinal);
            Assert.Contains("expiredAtTurn", doc, StringComparison.Ordinal);
            Assert.Contains("expiredAtUtc", doc, StringComparison.Ordinal);
        }
    }

    [Fact]
    public void ShiningForgePromptDocsUseExactActionTypesWithoutWildcard()
    {
        var mandatoryDocs = new[]
        {
            ReadRepoFile("CLI_API_Specification.md"),
            ReadRepoFile("CLI_Agent_Daemon_Specification.md"),
            ReadRepoFile("TaskGuides", "CLI_Step_Main.txt"),
            ReadRepoFile("Rules", "Block_CLI_Operations.txt"),
            ReadRepoFile("OtherGuides", "Afterlife_Contract_Matrix.md"),
            ReadRepoFile("Examples", "E_CLI_Afterlife_Turns.txt")
        };

        var forgeActionTypes = typeof(ShiningCoreActionRequestState)
            .GetFields(BindingFlags.Public | BindingFlags.Static | BindingFlags.FlattenHierarchy)
            .Where(field => field is { IsLiteral: true, IsInitOnly: false } &&
                            field.FieldType == typeof(string))
            .Select(field => (string)field.GetRawConstantValue()!)
            .Where(value => value.StartsWith("forge_relic.", StringComparison.Ordinal))
            .OrderBy(value => value, StringComparer.Ordinal)
            .ToArray();

        Assert.NotEmpty(forgeActionTypes);

        foreach (var doc in mandatoryDocs)
        {
            Assert.DoesNotContain("forge_relic.*", doc, StringComparison.Ordinal);
            foreach (var actionType in forgeActionTypes)
                Assert.Contains(actionType, doc, StringComparison.Ordinal);
        }
    }

    [Fact]
    public void ShiningTradePromptDocsRequireUniqueNewRelicIdentities()
    {
        var mandatoryDocs = new[]
        {
            ReadRepoFile("OtherGuides", "Afterlife_Contract_Matrix.md"),
            ReadRepoFile("Examples", "E_CLI_Afterlife_Turns.txt")
        };

        foreach (var doc in mandatoryDocs)
        {
            Assert.Contains("relicData.relicId", doc, StringComparison.Ordinal);
            Assert.Contains("soulRelics.equipped", doc, StringComparison.Ordinal);
            Assert.Contains("soulRelics.stored", doc, StringComparison.Ordinal);
            Assert.Contains("unique", doc, StringComparison.OrdinalIgnoreCase);
        }
    }

    [Fact]
    public void ShiningQueueLimitsAvailabilityAndControlSurfacesAreDocumented()
    {
        var matrix = ReadRepoFile("OtherGuides", "Afterlife_Contract_Matrix.md");
        var apiSpec = ReadRepoFile("CLI_API_Specification.md");
        var daemonSpec = ReadRepoFile("CLI_Agent_Daemon_Specification.md");
        var examples = ReadRepoFile("Examples", "E_CLI_Afterlife_Turns.txt");
        var taskGuide = ReadRepoFile("TaskGuides", "CLI_Step_Main.txt");
        var launcherGenerator = ReadRepoFile("BookOfEternityClient", "Launcher", "Generate_CLI_Launch_Script.ps1");
        var launcherGenerated = ReadRepoFile("BookOfEternityClient", "Launcher", "CLI_Launch_Script.md");

        foreach (var doc in new[] { matrix, apiSpec, daemonSpec, examples })
        {
            Assert.Contains("pending_shining_abode_actions.json", doc, StringComparison.Ordinal);
            Assert.Contains("one active", doc, StringComparison.OrdinalIgnoreCase);
        }

        foreach (var doc in new[] { matrix, apiSpec, daemonSpec })
        {
            Assert.Contains("(factionId, tradeCycleId)", doc, StringComparison.Ordinal);
            Assert.Contains("pending_shining_trade_inventory_requests.json", doc, StringComparison.Ordinal);
            Assert.Contains("ordinary active Shining", doc, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("preparedIncarnationPackage", doc, StringComparison.Ordinal);
        }

        foreach (var doc in new[] { matrix, apiSpec, examples })
        {
            Assert.Contains("availability", doc, StringComparison.Ordinal);
            Assert.Contains("active", doc, StringComparison.Ordinal);
            Assert.Contains("sealed_until_next_ascension", doc, StringComparison.Ordinal);
            Assert.Contains("preparedIncarnationPackage", doc, StringComparison.Ordinal);
            Assert.Contains("system_guardian_attraction.json", doc, StringComparison.Ordinal);
        }
        Assert.Contains("Ordinary Chaos Sea only", matrix, StringComparison.Ordinal);
        Assert.Contains("do not create, close, or repair this from Shining Abode", matrix, StringComparison.Ordinal);

        foreach (var doc in new[] { matrix, apiSpec, examples, taskGuide, launcherGenerator, launcherGenerated })
        {
            Assert.Contains("availability = active", doc, StringComparison.Ordinal);
            Assert.Contains("preparedIncarnationPackage", doc, StringComparison.Ordinal);
        }

        Assert.DoesNotContain("availability = active` or another accepted value", examples, StringComparison.Ordinal);
        Assert.DoesNotContain("preparedIncarnationPackage = null` → Active Shining Abode", launcherGenerator, StringComparison.Ordinal);
        Assert.DoesNotContain("preparedIncarnationPackage = null` → Active Shining Abode", launcherGenerated, StringComparison.Ordinal);
    }

    [Fact]
    public void ShiningBlessingPostBootstrapEffectsAreDocumentedForGm()
    {
        var matrix = ReadRepoFile("OtherGuides", "Afterlife_Contract_Matrix.md");
        var apiSpec = ReadRepoFile("CLI_API_Specification.md");
        var daemonSpec = ReadRepoFile("CLI_Agent_Daemon_Specification.md");
        var taskGuide = ReadRepoFile("TaskGuides", "CLI_Step_Main.txt");
        var examples = ReadRepoFile("Examples", "E_CLI_Afterlife_Turns.txt");

        foreach (var term in new[]
        {
            ShiningBlessingEffectState.SoulStateProperty,
            ShiningBlessingEffectState.MemoryStatusPendingPreTurnOneSelection,
            ShiningBlessingEffectState.ResourceStatusAppliedAtBootstrap,
            ShiningBlessingEffectState.RelicStatusPendingEntitlement,
            ShiningBlessingEffectState.SocialStatusPendingFirstRelationCommit,
            ShiningBlessingEffectState.RouteStatusPendingEarlyRouteSeed,
            ShiningBlessingEffectState.LoreStatusPendingLoreInsertion,
            ShiningBlessingEffectState.SurvivalStatusPendingFirstRuinousFailure,
            ShiningBlessingEffectState.DescentStatusPendingResidentDescent
        })
        {
            foreach (var doc in new[] { matrix, apiSpec, taskGuide, examples })
                Assert.Contains(term, doc, StringComparison.Ordinal);
        }

        foreach (var doc in new[] { matrix, apiSpec, daemonSpec, taskGuide, examples })
        {
            Assert.Contains("relicRefinementEntitlements", doc, StringComparison.Ordinal);
            Assert.Contains("exception", doc, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("Shining forge", doc, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("rerolls/freeShape/freeRetune", doc, StringComparison.Ordinal);
            Assert.Contains("pending_shining_abode_actions.json", doc, StringComparison.Ordinal);
        }
    }

    [Fact]
    public void GuardianQuestOriginMetadataIsDocumentedForAfterlifeHooks()
    {
        var matrix = ReadRepoFile("OtherGuides", "Afterlife_Contract_Matrix.md");
        var apiSpec = ReadRepoFile("CLI_API_Specification.md");
        var taskGuide = ReadRepoFile("TaskGuides", "CLI_Step_Main.txt");
        var daemonSpec = ReadRepoFile("CLI_Agent_Daemon_Specification.md");
        var guardianRules = ReadRepoFile("Rules", "Block_32_Guardians.txt");
        var examples = ReadRepoFile("Examples", "E_CLI_Afterlife_Turns.txt");

        foreach (var doc in new[] { matrix, apiSpec, taskGuide, examples })
        {
            Assert.Contains("lore_research_hook", doc, StringComparison.Ordinal);
            Assert.Contains("lore_research_special_line", doc, StringComparison.Ordinal);
            Assert.Contains("archive_consultation_hook", doc, StringComparison.Ordinal);
            Assert.Contains("guardian_baseline_mortal_life_hook", doc, StringComparison.Ordinal);
            Assert.Contains("sourceProjectId", doc, StringComparison.Ordinal);
            Assert.Contains("доброволь", doc, StringComparison.OrdinalIgnoreCase);
        }

        foreach (var doc in new[] { matrix, apiSpec, taskGuide, daemonSpec, guardianRules, examples })
        {
            Assert.Contains("guardianQuestProgressUpdates", doc, StringComparison.Ordinal);
            Assert.Contains("ready_to_turn_in", doc, StringComparison.Ordinal);
            Assert.Contains("readyToTurnInEvidence", doc, StringComparison.Ordinal);
            Assert.Contains("itemEcho", doc, StringComparison.Ordinal);
            Assert.Contains("memory", doc, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("physical", doc, StringComparison.OrdinalIgnoreCase);
        }
    }

    [Fact]
    public void ShiningBlessingCardTokensAreDocumentedForGm()
    {
        var matrix = ReadRepoFile("OtherGuides", "Afterlife_Contract_Matrix.md");
        var examples = ReadRepoFile("Examples", "E_CLI_Afterlife_Turns.txt");
        var docs = new[] { matrix, examples };

        var sourceTypes = typeof(ShiningAbodeState)
            .GetFields(BindingFlags.Public | BindingFlags.Static | BindingFlags.FlattenHierarchy)
            .Where(field => field is { IsLiteral: true, IsInitOnly: false } &&
                            field.FieldType == typeof(string) &&
                            field.Name.StartsWith("CardSourceType", StringComparison.Ordinal))
            .Select(field => (string)field.GetRawConstantValue()!)
            .OrderBy(value => value, StringComparer.Ordinal)
            .ToArray();
        var effectFamilies = typeof(ShiningAbodeState)
            .GetFields(BindingFlags.Public | BindingFlags.Static | BindingFlags.FlattenHierarchy)
            .Where(field => field is { IsLiteral: true, IsInitOnly: false } &&
                            field.FieldType == typeof(string) &&
                            field.Name.StartsWith("EffectFamily", StringComparison.Ordinal))
            .Select(field => (string)field.GetRawConstantValue()!)
            .OrderBy(value => value, StringComparer.Ordinal)
            .ToArray();
        var rarities = typeof(ShiningAbodeState)
            .GetFields(BindingFlags.Public | BindingFlags.Static | BindingFlags.FlattenHierarchy)
            .Where(field => field is { IsLiteral: true, IsInitOnly: false } &&
                            field.FieldType == typeof(string) &&
                            field.Name.StartsWith("Rarity", StringComparison.Ordinal))
            .Select(field => (string)field.GetRawConstantValue()!)
            .OrderBy(value => value, StringComparer.Ordinal)
            .ToArray();

        Assert.NotEmpty(sourceTypes);
        Assert.NotEmpty(effectFamilies);
        Assert.NotEmpty(rarities);

        foreach (var doc in docs)
        {
            Assert.Contains("sourceType", doc, StringComparison.Ordinal);
            Assert.Contains("effectFamily", doc, StringComparison.Ordinal);
            Assert.Contains("rarity", doc, StringComparison.Ordinal);

            foreach (var value in sourceTypes.Concat(effectFamilies).Concat(rarities))
                Assert.Contains(value, doc, StringComparison.Ordinal);
        }
    }

    [Fact]
    public void ShiningGatesLocalMutationsAndResidentNormalizerAreDocumentedForGm()
    {
        var matrix = ReadRepoFile("OtherGuides", "Afterlife_Contract_Matrix.md");
        var apiSpec = ReadRepoFile("CLI_API_Specification.md");
        var daemonSpec = ReadRepoFile("CLI_Agent_Daemon_Specification.md");
        var operations = ReadRepoFile("Rules", "Block_CLI_Operations.txt");
        var taskGuide = ReadRepoFile("TaskGuides", "CLI_Step_Main.txt");
        var examples = ReadRepoFile("Examples", "E_CLI_Afterlife_Turns.txt");
        var docs = new[] { matrix, apiSpec, daemonSpec, operations, taskGuide, examples };

        foreach (var doc in docs)
        {
            Assert.Contains("selectedBlessingCardIds", doc, StringComparison.Ordinal);
            Assert.Contains("shownBlessingCardIds", doc, StringComparison.Ordinal);
            Assert.Contains("rerollsRemaining", doc, StringComparison.Ordinal);
            Assert.Contains("nextCandidateCursor", doc, StringComparison.Ordinal);
            Assert.Contains("GM turn", doc, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("coreActionReceipts[]", doc, StringComparison.Ordinal);
            Assert.Contains("remained_in_chaos_sea", doc, StringComparison.Ordinal);
            Assert.Contains("factionLoyaltyTier", doc, StringComparison.Ordinal);
            Assert.Contains("factionRestlessness", doc, StringComparison.Ordinal);
            Assert.Contains("factionRealignmentState", doc, StringComparison.Ordinal);
            Assert.Contains("shiningAlignment", doc, StringComparison.Ordinal);
        }

        foreach (var doc in new[] { matrix, examples })
        {
            Assert.Contains("Deterministic `open_gates` Projection", doc, StringComparison.Ordinal);
            Assert.Contains("projectedStateFragment.afterFullShiningRoot.gates", doc, StringComparison.Ordinal);
            Assert.Contains("draft size", doc, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("pick cap", doc, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("dedupeKey", doc, StringComparison.Ordinal);
            Assert.Contains("allCandidateBlessingCards", doc, StringComparison.Ordinal);
            Assert.Contains("availableBlessingCards", doc, StringComparison.Ordinal);
        }
    }

    [Fact]
    public void ShiningFactionLifecycleContractIsDocumentedForGm()
    {
        var matrix = ReadRepoFile("OtherGuides", "Afterlife_Contract_Matrix.md");
        var examples = ReadRepoFile("Examples", "E_CLI_Afterlife_Turns.txt");
        var apiSpec = ReadRepoFile("CLI_API_Specification.md");
        var daemonSpec = ReadRepoFile("CLI_Agent_Daemon_Specification.md");
        var taskGuide = ReadRepoFile("TaskGuides", "CLI_Step_Main.txt");
        var operations = ReadRepoFile("Rules", "Block_CLI_Operations.txt");
        var manifest = ReadRepoFile("Examples", "example_validation_manifest.json");
        var docs = new[] { matrix, examples, apiSpec, daemonSpec, taskGuide, operations };

        foreach (var doc in docs)
        {
            Assert.Contains("factionLifecycle", doc, StringComparison.Ordinal);
            Assert.Contains(ShiningAbodeState.FactionLifecycleStateActive, doc, StringComparison.Ordinal);
            Assert.Contains(ShiningAbodeState.FactionLifecycleStateWeakened, doc, StringComparison.Ordinal);
            Assert.Contains(ShiningAbodeState.FactionLifecycleStateLeaderless, doc, StringComparison.Ordinal);
            Assert.Contains(ShiningAbodeState.FactionLifecycleStateBroken, doc, StringComparison.Ordinal);
            Assert.Contains(ShiningAbodeState.FactionLifecycleStateDissolved, doc, StringComparison.Ordinal);
            Assert.Contains("leadershipState=vacant", doc, StringComparison.Ordinal);
            Assert.Contains("tradeInventory", doc, StringComparison.Ordinal);
            Assert.Contains("isSupported=false", doc, StringComparison.Ordinal);
            Assert.Contains("defeatedAtTurn", doc, StringComparison.Ordinal);
            Assert.Contains("remnantsSummary", doc, StringComparison.Ordinal);
            Assert.Contains("Do not delete", doc, StringComparison.OrdinalIgnoreCase);
        }

        Assert.Contains("shining_faction_lifecycle_v1", manifest, StringComparison.Ordinal);
        Assert.Contains("factions[].factionLifecycle", manifest, StringComparison.Ordinal);
        Assert.Contains("Крылья Ангелов", examples, StringComparison.Ordinal);
    }

    [Fact]
    public void ShiningFactionConflictCampaignContractIsDocumentedForGm()
    {
        var matrix = ReadRepoFile("OtherGuides", "Afterlife_Contract_Matrix.md");
        var examples = ReadRepoFile("Examples", "E_CLI_Afterlife_Turns.txt");
        var apiSpec = ReadRepoFile("CLI_API_Specification.md");
        var daemonSpec = ReadRepoFile("CLI_Agent_Daemon_Specification.md");
        var taskGuide = ReadRepoFile("TaskGuides", "CLI_Step_Main.txt");
        var operations = ReadRepoFile("Rules", "Block_CLI_Operations.txt");
        var manifest = ReadRepoFile("Examples", "example_validation_manifest.json");
        var docs = new[] { matrix, examples, apiSpec, daemonSpec, taskGuide, operations };

        foreach (var doc in docs)
        {
            Assert.Contains(ShiningAbodeState.FactionConflictCampaignsProperty, doc, StringComparison.Ordinal);
            Assert.Contains(ShiningAbodeState.FactionCampaignGoalWeaken, doc, StringComparison.Ordinal);
            Assert.Contains(ShiningAbodeState.FactionCampaignGoalExpose, doc, StringComparison.Ordinal);
            Assert.Contains(ShiningAbodeState.FactionCampaignGoalDeposeLeader, doc, StringComparison.Ordinal);
            Assert.Contains(ShiningAbodeState.FactionCampaignGoalBreak, doc, StringComparison.Ordinal);
            Assert.Contains(ShiningAbodeState.FactionCampaignGoalDissolve, doc, StringComparison.Ordinal);
            Assert.Contains(ShiningAbodeState.FactionCampaignStatusBreakthroughReady, doc, StringComparison.Ordinal);
            Assert.Contains(ShiningAbodeState.FactionCampaignBreakthroughDuelVictory, doc, StringComparison.Ordinal);
            Assert.Contains(ShiningAbodeState.FactionCampaignBreakthroughExposure, doc, StringComparison.Ordinal);
            Assert.Contains(ShiningAbodeState.FactionCampaignBreakthroughDefection, doc, StringComparison.Ordinal);
            Assert.Contains(ShiningAbodeState.FactionCampaignBreakthroughSabotage, doc, StringComparison.Ordinal);
            Assert.Contains(ShiningAbodeState.FactionCampaignBreakthroughResourceDisruption, doc, StringComparison.Ordinal);
            Assert.Contains(ShiningAbodeState.FactionCampaignBreakthroughOathBreak, doc, StringComparison.Ordinal);
            Assert.Contains(ShiningAbodeState.FactionCampaignBreakthroughTrial, doc, StringComparison.Ordinal);
            Assert.Contains(ShiningAbodeState.FactionCampaignBreakthroughSarefDirective, doc, StringComparison.Ordinal);
            Assert.Contains("factionDataChanges", doc, StringComparison.Ordinal);
        }

        Assert.Contains("shining_faction_conflict_campaigns_v1", manifest, StringComparison.Ordinal);
        Assert.Contains("factionConflictCampaigns[]", manifest, StringComparison.Ordinal);
        Assert.Contains("breakthroughLog", examples, StringComparison.Ordinal);
    }

    [Fact]
    public void AfterlifeEnumContractsAreDocumentedForGm()
    {
        var matrix = ReadRepoFile("OtherGuides", "Afterlife_Contract_Matrix.md");

        foreach (var value in new[]
        {
            ShiningFactionRequestState.RealignmentModeAcceptedTransfer,
            ShiningFactionRequestState.RealignmentModeRefusedTransfer,
            ShiningFactionRequestState.RealignmentModeDepartureToNeutral,
            ShiningFactionRequestState.RequestStatusAccepted,
            ShiningFactionRequestState.RequestStatusRefused,
            ShiningFactionRequestState.RequestStatusWithdrawn,
            ShiningFactionRequestState.RequestStatusDepartedToNeutral,
            AfterlifeArchiveState.EntryTypeLoreFragment,
            AfterlifeArchiveState.EntryTypeSecretRecord,
            AfterlifeArchiveState.SourceKindCodex,
            AfterlifeArchiveState.SourceKindSystem,
            AfterlifeArchiveState.ReservationKindConsultation,
            AfterlifeArchiveState.ReservationKindProjectFuel,
            AfterlifeArchiveActionState.ProjectFuelResultModeProjectWork,
            AfterlifeArchiveActionState.ProjectFuelResultModePressureRelief,
            AfterlifeArchiveActionState.ConsultationOutcomeGuaranteedArchiveQuestCount,
            AfterlifeArchiveActionState.ConsultationOutcomeQuestHookCount,
            AfterlifeArchiveActionState.ConsultationOutcomeSpecialQuestLineUnlocks,
            AfterlifeArchiveActionState.ConsultationOutcomeVisibleRivalClueBonus,
            AfterlifeArchiveActionState.ConsultationOutcomeArchiveWarningTierBonus
        })
        {
            Assert.Contains(value, matrix, StringComparison.Ordinal);
        }

        foreach (var field in new[]
        {
            "residentId",
            "residentName",
            "residentKind",
            "originType",
            "isPresent",
            "sourceGuardianId",
            "sourceAbodeId",
            "abodeDevotionLevel",
            "migrationState",
            "historyLog",
            "interactionLog"
        })
        {
            Assert.Contains(field, matrix, StringComparison.Ordinal);
        }
    }

    [Fact]
    public void ShiningTreasuryClientOwnedEconomyIsDocumented()
    {
        var matrix = ReadRepoFile("OtherGuides", "Afterlife_Contract_Matrix.md");
        var apiSpec = ReadRepoFile("CLI_API_Specification.md");
        var daemonSpec = ReadRepoFile("CLI_Agent_Daemon_Specification.md");
        var taskGuide = ReadRepoFile("TaskGuides", "CLI_Step_Main.txt");
        var helpSource = ReadRepoFile("BookOfEternityClient", "UI", "ExplorerMode", "ExplorerMode.MetaStoryAndStatus.cs");
        var docs = new[] { matrix, apiSpec, daemonSpec, taskGuide };

        foreach (var doc in docs)
        {
            Assert.Contains("shining_treasury", doc, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("казначейство", doc, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("Ink Feathers", doc, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("Light Spark", doc, StringComparison.OrdinalIgnoreCase);
        }

        Assert.Contains("shining_treasury", helpSource, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("казначейство", helpSource, StringComparison.OrdinalIgnoreCase);

        foreach (var doc in new[] { matrix, apiSpec, daemonSpec })
        {
            Assert.Contains("client-owned", doc, StringComparison.OrdinalIgnoreCase);
            Assert.Contains(ShiningAbodeState.TreasuryFeathersPerLightSpark.ToString(), doc, StringComparison.Ordinal);
            Assert.Contains(ShiningAbodeState.TreasuryMaxLightSparksExchangePerCycle.ToString(), doc, StringComparison.Ordinal);
            Assert.Contains("Light Sparks cannot be deposited", doc, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("GM", doc, StringComparison.OrdinalIgnoreCase);
        }
    }

    [Fact]
    public void ShiningProjectedStateFragmentPreviewIsDocumentedForGm()
    {
        var matrix = ReadRepoFile("OtherGuides", "Afterlife_Contract_Matrix.md");
        var examples = ReadRepoFile("Examples", "E_CLI_Afterlife_Turns.txt");

        foreach (var doc in new[] { matrix, examples })
        {
            Assert.Contains("projectedStateFragment", doc, StringComparison.Ordinal);
            Assert.Contains("beforeFullShiningRoot", doc, StringComparison.Ordinal);
            Assert.Contains("afterFullShiningRoot", doc, StringComparison.Ordinal);
            Assert.Contains("beforeFullSoulRoot", doc, StringComparison.Ordinal);
            Assert.Contains("afterFullSoulRoot", doc, StringComparison.Ordinal);
            Assert.Contains("gachaAccounting", doc, StringComparison.Ordinal);
            Assert.Contains("expectedGachaHistoryEntryShape", doc, StringComparison.Ordinal);
            Assert.Contains("oneNewRelicEvidence", doc, StringComparison.Ordinal);
            Assert.Contains("audit", doc, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("not", doc, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("output file", doc, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("coreActionReceipts[]", doc, StringComparison.Ordinal);
            Assert.Contains("effectPayload", doc, StringComparison.Ordinal);
        }
    }

    [Fact]
    public void ResidentCompanionManifestationHandoffIsDocumentedForAfterlifeOrigin()
    {
        var matrix = ReadRepoFile("OtherGuides", "Afterlife_Contract_Matrix.md");
        var apiSpec = ReadRepoFile("CLI_API_Specification.md");
        var taskGuide = ReadRepoFile("TaskGuides", "CLI_Step_Main.txt");
        var examples = ReadRepoFile("Examples", "E_CLI_Afterlife_Turns.txt");

        foreach (var term in new[]
        {
            "pending_resident_companion_manifestation_request.json",
            "MortalWorldProfile",
            "sourceResidentId",
            "sourceImprintId",
            "sourceGuardianId",
            "futureCompanionPrompt",
            "targetIncarnation"
        })
        {
            foreach (var doc in new[] { matrix, apiSpec, taskGuide, examples })
                Assert.Contains(term, doc, StringComparison.Ordinal);
        }
    }

    [Fact]
    public void ChaosSeaTravelContractIsDocumentedForGm()
    {
        var matrix = ReadRepoFile("OtherGuides", "Afterlife_Contract_Matrix.md");
        var examples = ReadRepoFile("Examples", "E_CLI_Afterlife_Turns.txt");
        var daemonSpec = ReadRepoFile("CLI_Agent_Daemon_Specification.md");

        foreach (var text in new[] { matrix, examples })
        {
            Assert.Contains("[CHAOS_SEA_TRAVEL]", text, StringComparison.Ordinal);
            Assert.Contains("activeGuardian", text, StringComparison.Ordinal);
            Assert.Contains("currentAbodeId", text, StringComparison.Ordinal);
            Assert.Contains("discoveredAbodes", text, StringComparison.Ordinal);
            Assert.Contains("pre-turn", text, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("currentLocationData", text, StringComparison.Ordinal);
            Assert.Contains("worldEventsLog", text, StringComparison.Ordinal);
        }

        Assert.Contains("[CHAOS_SEA_TRAVEL]", daemonSpec, StringComparison.Ordinal);
    }

    [Fact]
    public void FreeformChaosSeaAbodeSearchContractIsDocumentedForGm()
    {
        var matrix = ReadRepoFile("OtherGuides", "Afterlife_Contract_Matrix.md");
        var examples = ReadRepoFile("Examples", "E_CLI_Afterlife_Turns.txt");
        var daemonSpec = ReadRepoFile("CLI_Agent_Daemon_Specification.md");
        var taskGuide = ReadRepoFile("TaskGuides", "CLI_Step_Main.txt");
        var launcherGenerator = ReadRepoFile("BookOfEternityClient", "Launcher", "Generate_CLI_Launch_Script.ps1");
        var launcherScript = ReadRepoFile("BookOfEternityClient", "Launcher", "CLI_Launch_Script.md");
        var source = ReadRepoFile(
            "BookOfEternityClient",
            "UI",
            "ExplorerMode",
            "ExplorerMode.Afterlife.GuardiansProjectsTrade.cs");

        foreach (var text in new[] { matrix, examples, daemonSpec, taskGuide, launcherGenerator, launcherScript })
        {
            Assert.Contains("freeform", text, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("Abode search", text, StringComparison.OrdinalIgnoreCase);
        }

        foreach (var text in new[] { matrix, examples, daemonSpec, taskGuide, source })
        {
            Assert.Contains("chaos_sea_abode_search", text, StringComparison.Ordinal);
            Assert.Contains("guardianId", text, StringComparison.Ordinal);
            Assert.Contains("guardianName", text, StringComparison.Ordinal);
            Assert.Contains("abodeId", text, StringComparison.Ordinal);
        }

        foreach (var text in new[] { matrix, examples })
        {
            Assert.Contains("UpdateGuardians.create", text, StringComparison.Ordinal);
            Assert.Contains("activeGuardian", text, StringComparison.Ordinal);
            Assert.Contains("chaosSeaNavigation", text, StringComparison.Ordinal);
            Assert.Contains("currentAbodeId", text, StringComparison.Ordinal);
            Assert.Contains("currentLocationData", text, StringComparison.Ordinal);
            Assert.Contains("UpdateNPCs", text, StringComparison.Ordinal);
            Assert.Contains("worldEventsLog", text, StringComparison.Ordinal);
            Assert.Contains("example 23", text, StringComparison.OrdinalIgnoreCase);
        }
    }

    [Fact]
    public void AfterlifeSpiritualConflictContractIsDocumentedForGm()
    {
        var matrix = ReadRepoFile("OtherGuides", "Afterlife_Contract_Matrix.md");
        var examples = ReadRepoFile("Examples", "E_CLI_Afterlife_Turns.txt");
        var apiSpec = ReadRepoFile("CLI_API_Specification.md");
        var daemonSpec = ReadRepoFile("CLI_Agent_Daemon_Specification.md");
        var taskGuide = ReadRepoFile("TaskGuides", "CLI_Step_Main.txt");
        var glossary = ReadRepoFile("OtherGuides", "Afterlife_Combat_Terminology_Glossary.md");
        var uiHelp = ReadRepoFile("BookOfEternityClient", "UI", "ExplorerMode", "ExplorerMode.Afterlife.SpiritualConflict.cs");
        var manifest = ReadRepoFile("Examples", "example_validation_manifest.json");
        var docs = new[] { matrix, examples, apiSpec, daemonSpec, taskGuide };

        foreach (var text in docs)
        {
            Assert.Contains("afterlifeSpiritualConflictUpdate", text, StringComparison.Ordinal);
            Assert.Contains("game_state/meta/afterlife_spiritual_conflict_state.json", text, StringComparison.Ordinal);
            Assert.Contains("Mortal combat", text, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("guardian_forced", text, StringComparison.Ordinal);
            Assert.Contains("diceAudit", text, StringComparison.Ordinal);
            Assert.Contains("preGeneratedDices1d20", text, StringComparison.Ordinal);
            Assert.Contains("actionEconomy", text, StringComparison.Ordinal);
            Assert.Contains("actionCostAudit", text, StringComparison.Ordinal);
            Assert.Contains("actionCostAudit.opposition", text, StringComparison.Ordinal);
            Assert.Contains("recover_spiritual_power", text, StringComparison.Ordinal);
            Assert.Contains("spiritFocusTier", text, StringComparison.Ordinal);
            Assert.Contains("Средоточие Души", text, StringComparison.Ordinal);
        }

        foreach (var text in new[] { matrix, examples, daemonSpec })
        {
            Assert.Contains("AFTERLIFE_SPIRITUAL_ACTION", text, StringComparison.Ordinal);
            Assert.Contains("playerSideStrain", text, StringComparison.Ordinal);
            Assert.Contains("oppositionSideStrain", text, StringComparison.Ordinal);
            Assert.Contains("actorArtTierSnapshot", text, StringComparison.Ordinal);
            Assert.Contains("ordinary", text, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("prose", text, StringComparison.OrdinalIgnoreCase);
        }

        Assert.Contains("afterlife_spiritual_conflict_start_response", manifest, StringComparison.Ordinal);
        Assert.Contains("afterlife_conflict_liora_forced_incarnation_001", examples, StringComparison.Ordinal);
        Assert.Contains("afterlife_spiritual_conflict_v1", examples, StringComparison.Ordinal);
        Assert.Contains("sourceIndex", examples, StringComparison.Ordinal);
        Assert.Contains("outcomeBand", examples, StringComparison.Ordinal);
        Assert.Contains("sourceIndex/value", matrix, StringComparison.Ordinal);
        Assert.Contains("modifierBreakdown", matrix, StringComparison.Ordinal);
        Assert.Contains("spiritual_arts_local_upgrade", matrix, StringComparison.Ordinal);
        Assert.Contains("must not author upgrade receipts", matrix, StringComparison.Ordinal);
        Assert.Contains("resolvedAtTurn", examples, StringComparison.Ordinal);
        Assert.Contains("operationType", examples, StringComparison.Ordinal);
        Assert.Contains("playerOutcome", examples, StringComparison.Ordinal);
        Assert.Contains("rewardAudit", matrix + examples + apiSpec + daemonSpec + taskGuide + glossary, StringComparison.Ordinal);
        Assert.Contains("criticalResult", matrix + examples + apiSpec + daemonSpec + taskGuide + glossary, StringComparison.Ordinal);
        Assert.Contains("natural 20", matrix + examples + apiSpec + daemonSpec + taskGuide + glossary, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("bounded criticals are symmetric", matrix + examples + apiSpec + daemonSpec + glossary, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("scaleLimit", matrix + examples + apiSpec + daemonSpec + taskGuide + glossary, StringComparison.Ordinal);
        Assert.Contains("rollMode", matrix + examples + apiSpec + daemonSpec + taskGuide + glossary, StringComparison.Ordinal);
        Assert.Contains("selection", matrix + examples + apiSpec + daemonSpec + taskGuide + glossary, StringComparison.Ordinal);
        Assert.Contains("Преимущество", matrix + examples + apiSpec + daemonSpec + taskGuide + glossary + uiHelp, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Помеха", matrix + examples + apiSpec + daemonSpec + taskGuide + glossary + uiHelp, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("advantageSources", matrix + examples + apiSpec + daemonSpec + taskGuide + glossary, StringComparison.Ordinal);
        Assert.Contains("disadvantageSources", matrix + examples + apiSpec + daemonSpec + taskGuide + glossary, StringComparison.Ordinal);
        Assert.Contains("Встречные Преимущество и Помеха гасятся", matrix + examples + apiSpec + daemonSpec + taskGuide + glossary, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("/spiritual_combat_help", glossary, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("/духовный_бой", glossary, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("/spiritual_combat_log", glossary + examples + matrix, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("/журнал_духовного_боя", glossary + examples + matrix, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("counterPayoff", matrix + apiSpec + daemonSpec + taskGuide + glossary, StringComparison.Ordinal);
        Assert.Contains("success/partial_success/countered", daemonSpec, StringComparison.Ordinal);
        Assert.Contains("success/partial_success/countered", taskGuide, StringComparison.Ordinal);
        Assert.Contains("success/partial_success/countered", examples, StringComparison.Ordinal);
        Assert.Contains("success/partial_success/countered", glossary, StringComparison.Ordinal);
        Assert.Contains("setback needs downside (`playerSideStrain`, worse `conflictPosition`, or `counterBackfire`)", glossary, StringComparison.Ordinal);
        Assert.DoesNotContain("setback needs downside (`playerSideStrain`, worse `conflictPosition`, `counterBackfire`, or control reversal)", glossary, StringComparison.Ordinal);
        Assert.Contains("conflict_position", matrix + examples + apiSpec + daemonSpec + taskGuide + glossary, StringComparison.Ordinal);
        Assert.Contains("Do not split or duplicate", apiSpec + daemonSpec + matrix + examples, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("zero `conflict_position` entries", matrix + examples + apiSpec + daemonSpec + taskGuide + glossary, StringComparison.Ordinal);
        Assert.Contains("exact matching `position`", matrix + examples + apiSpec + daemonSpec + taskGuide + glossary, StringComparison.Ordinal);
        Assert.Contains("matchupAudit", matrix + examples + apiSpec + daemonSpec + taskGuide + glossary + uiHelp, StringComparison.Ordinal);
        Assert.Contains("same-level narrowing of opposition `restrictedOperations` counts as weakened `controlState`", matrix + examples + apiSpec + daemonSpec + taskGuide + glossary, StringComparison.Ordinal);
        Assert.Contains("equal/reordered sets do not count", matrix + examples + apiSpec + daemonSpec + taskGuide + glossary, StringComparison.Ordinal);
        Assert.Contains("at most one", matrix + examples + apiSpec + daemonSpec + taskGuide + glossary, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("at least two distinct", matrix + examples + apiSpec + daemonSpec + taskGuide + glossary, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("cannot succeed until", matrix + examples + apiSpec + daemonSpec + taskGuide + glossary, StringComparison.OrdinalIgnoreCase);
        foreach (var text in new[] { matrix, apiSpec, daemonSpec, taskGuide, glossary })
        {
            Assert.Contains("controlState", text, StringComparison.Ordinal);
            Assert.Contains("restrictedOperations", text, StringComparison.Ordinal);
            Assert.Contains("sourceOperation=binding|force_binding|force_incarnation|break_binding|incarnation_resistance|counter|guard|repair", text, StringComparison.Ordinal);
            Assert.Contains("hindered", text, StringComparison.Ordinal);
            Assert.Contains("bound", text, StringComparison.Ordinal);
            Assert.Contains("locked", text, StringComparison.Ordinal);
            Assert.Contains("failed binding/force_binding outcomes (`blocked`, `countered`, `setback`) leave `controlState` unchanged on both sides", text, StringComparison.Ordinal);
            Assert.Contains("failed incarnation_resistance outcomes leave forced-incarnation `controlState` unchanged", text, StringComparison.Ordinal);
            Assert.Contains("force_binding", text, StringComparison.Ordinal);
            Assert.Contains("at most one", text, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("at least two distinct", text, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("cannot succeed until", text, StringComparison.OrdinalIgnoreCase);
        }
        Assert.Contains("controlState", examples + uiHelp, StringComparison.Ordinal);
        Assert.Contains("restrictedOperations", examples + uiHelp, StringComparison.Ordinal);
        Assert.Contains("sourceOperation=binding|force_binding|force_incarnation|break_binding|incarnation_resistance|counter|guard|repair", examples, StringComparison.Ordinal);
        Assert.Contains("стесн", glossary + uiHelp, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("скован", glossary + uiHelp, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("запечатан", glossary + uiHelp, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("playerOperation", matrix + examples + apiSpec + daemonSpec + taskGuide + glossary, StringComparison.Ordinal);
        Assert.Contains("oppositionOperation", matrix + examples + apiSpec + daemonSpec + taskGuide + glossary, StringComparison.Ordinal);
        Assert.Contains("primaryResolutionLane", matrix + examples + apiSpec + daemonSpec + taskGuide + glossary, StringComparison.Ordinal);
        Assert.Contains("riskProfile", matrix + examples + apiSpec + daemonSpec + taskGuide + glossary, StringComparison.Ordinal);
        Assert.Contains("matchupRationale", matrix + examples + apiSpec + daemonSpec + taskGuide + glossary, StringComparison.Ordinal);
        Assert.Contains("safe_defense", matrix + glossary, StringComparison.Ordinal);
        Assert.Contains("risky_reversal", matrix + glossary, StringComparison.Ordinal);
        Assert.Contains("position_play", matrix + glossary, StringComparison.Ordinal);
        Assert.Contains("recovery_timing", matrix + examples + apiSpec + daemonSpec + taskGuide + glossary, StringComparison.Ordinal);
        Assert.Contains("Собрать Средоточие", matrix + examples + apiSpec + daemonSpec + taskGuide + glossary + uiHelp, StringComparison.Ordinal);
        Assert.Contains("effectiveCost = max(minCost, baseCost - artTier)", matrix + examples + apiSpec + daemonSpec + taskGuide + glossary, StringComparison.Ordinal);
        Assert.Contains("pressure 3/1", matrix + examples + apiSpec + daemonSpec + taskGuide + glossary, StringComparison.Ordinal);
        Assert.Contains("force_binding 5/2", matrix + examples + apiSpec + daemonSpec + taskGuide + glossary, StringComparison.Ordinal);
        Assert.Contains("recover_spiritual_power 0/0", matrix + examples + apiSpec + daemonSpec + taskGuide + glossary, StringComparison.Ordinal);
        Assert.Contains("6/7/8/10/12/15", matrix + examples + apiSpec + daemonSpec + taskGuide + glossary, StringComparison.Ordinal);
        Assert.Contains("spiritFocusTier", matrix + examples + apiSpec + daemonSpec + taskGuide + glossary + uiHelp, StringComparison.Ordinal);
        Assert.Contains("Средоточие Души tier N", examples + apiSpec, StringComparison.Ordinal);
        Assert.Contains("Ink Feathers = 100 + nextTier * 100", glossary, StringComparison.Ordinal);
        Assert.Contains("Light Sparks = 8 + nextTier * 4", glossary, StringComparison.Ordinal);
        Assert.Contains("artTiers` reduce action cost", matrix, StringComparison.Ordinal);
        Assert.Contains("spiritFocusTier` sets max ОД", matrix, StringComparison.Ordinal);
        Assert.Contains("+0..1", matrix + examples + apiSpec + daemonSpec + taskGuide + glossary + uiHelp, StringComparison.Ordinal);
        Assert.Contains("ОД и стоимость действий", uiHelp, StringComparison.Ordinal);
        Assert.Contains("actionCostAudit", uiHelp, StringComparison.Ordinal);
        Assert.Contains("actionCostAudit.opposition", uiHelp, StringComparison.Ordinal);
        Assert.Contains("no current `actionCostAudit.<side>`", matrix + examples + apiSpec + daemonSpec + taskGuide + glossary, StringComparison.Ordinal);
        Assert.Contains("validated pre-turn active conflict", matrix + examples + apiSpec + daemonSpec + taskGuide + glossary, StringComparison.Ordinal);
        Assert.Contains("specialArtAudit", matrix + examples + apiSpec + daemonSpec + taskGuide + glossary, StringComparison.Ordinal);
        Assert.Contains("specialArtAudits[]", matrix, StringComparison.Ordinal);
        Assert.Contains("specialArtAudits[]", examples, StringComparison.Ordinal);
        Assert.Contains("specialArtAudits[]", apiSpec, StringComparison.Ordinal);
        Assert.Contains("specialArtAudits[]", daemonSpec, StringComparison.Ordinal);
        Assert.Contains("specialArtAudits[]", taskGuide, StringComparison.Ordinal);
        Assert.Contains("specialArtAudits[]", glossary, StringComparison.Ordinal);
        Assert.Contains("never write both", matrix + examples + apiSpec + daemonSpec + taskGuide + glossary, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("effectNote", matrix + examples + apiSpec + daemonSpec + taskGuide + glossary, StringComparison.Ordinal);
        Assert.Contains("specialCostMultiplierPercent", matrix + examples + apiSpec + daemonSpec + taskGuide + glossary, StringComparison.Ordinal);
        Assert.Contains("Non-player or incoming-action special arts", matrix, StringComparison.Ordinal);
        Assert.Contains("non-player/incoming special arts", examples + daemonSpec, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("when they power the opposition operation", matrix + examples + apiSpec + daemonSpec + taskGuide, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("resolved opposition operation used for `actionCostAudit.opposition`", matrix + examples + apiSpec + daemonSpec + taskGuide, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("terminal/free", matrix + apiSpec + daemonSpec + taskGuide, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Terminal/free player operations (`withdraw`, `surrender`, `negotiate`) must not include `actionCostAudit.player`", matrix + examples + apiSpec + daemonSpec + taskGuide + glossary, StringComparison.Ordinal);
        Assert.Contains("`finalOperationType` is authoritative", matrix + apiSpec + daemonSpec + taskGuide + glossary, StringComparison.Ordinal);
        Assert.Contains("never fall back to stale `incomingAction.operationType` when `finalOperationType` exists", examples, StringComparison.Ordinal);
        Assert.Contains("Матрица приём-контрприём", uiHelp, StringComparison.Ordinal);
        Assert.Contains("Сильнее против", uiHelp, StringComparison.Ordinal);
        Assert.Contains("Контрится", uiHelp, StringComparison.Ordinal);
        Assert.Contains("ink_feathers", matrix + examples + apiSpec + daemonSpec + taskGuide + glossary, StringComparison.Ordinal);
        Assert.Contains("light_sparks", matrix + apiSpec + daemonSpec + taskGuide + glossary, StringComparison.Ordinal);
        Assert.Contains("outcomeMultiplierPercent", matrix + examples + apiSpec + daemonSpec, StringComparison.Ordinal);
        Assert.Contains("riskMultiplierPercent", matrix + examples + apiSpec + daemonSpec, StringComparison.Ordinal);
        Assert.Contains("difficultyAudit", matrix + examples + apiSpec + daemonSpec + taskGuide + glossary + uiHelp, StringComparison.Ordinal);
        Assert.Contains("game_state/core/game_settings.json.difficulty", matrix + examples + apiSpec + daemonSpec + taskGuide + glossary + uiHelp, StringComparison.Ordinal);
        Assert.Contains("game_difficulty", matrix + examples + apiSpec + daemonSpec + taskGuide + glossary, StringComparison.Ordinal);
        Assert.Contains("normal` / Нормальная", matrix + examples + apiSpec + daemonSpec + taskGuide, StringComparison.Ordinal);
        Assert.Contains("hard` / Тяжёлая", matrix + examples + apiSpec + daemonSpec + taskGuide, StringComparison.Ordinal);
        Assert.Contains("impossible` / Невозможная", matrix + examples + apiSpec + daemonSpec + taskGuide, StringComparison.Ordinal);
        Assert.Contains("rewardMultiplierPercent", matrix + examples + apiSpec + daemonSpec + taskGuide + glossary, StringComparison.Ordinal);
        Assert.Contains("oppositionModifier", matrix + examples + apiSpec + daemonSpec + taskGuide + glossary, StringComparison.Ordinal);
        Assert.Contains("position dominance `+4`", matrix + examples + apiSpec + daemonSpec + glossary, StringComparison.Ordinal);
        Assert.Contains("light_incarnate` lead `+8`", matrix + examples + apiSpec + daemonSpec + glossary, StringComparison.Ordinal);
        Assert.Contains("afterlife_spiritual_conflict_difficulty_audit_v1", manifest, StringComparison.Ordinal);
        Assert.Contains("metaStateUpdates.inkFeatherChanges.add", matrix + examples + apiSpec + daemonSpec + taskGuide + glossary, StringComparison.Ordinal);
        Assert.Contains("GM preference", examples + matrix + apiSpec + daemonSpec, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("example 24", matrix, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("see example 24", examples, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("see example 26", examples, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("examples 14-26", daemonSpec, StringComparison.OrdinalIgnoreCase);
        foreach (var text in new[] { matrix, examples, apiSpec, daemonSpec })
            Assert.Contains("OtherGuides/Afterlife_Combat_Terminology_Glossary.md", text, StringComparison.Ordinal);

        foreach (var text in new[] { matrix, examples, apiSpec, daemonSpec, taskGuide })
        {
            Assert.Contains("difficultyAudit", text, StringComparison.Ordinal);
            Assert.Contains("game_state/core/game_settings.json.difficulty", text, StringComparison.Ordinal);
            Assert.Contains("game_difficulty", text, StringComparison.Ordinal);
            Assert.Contains("oppositionModifier", text, StringComparison.Ordinal);
            Assert.Contains("rewardMultiplierPercent", text, StringComparison.Ordinal);
            Assert.Contains("normal` / Нормальная", text, StringComparison.Ordinal);
            Assert.Contains("hard` / Тяжёлая", text, StringComparison.Ordinal);
            Assert.Contains("impossible` / Невозможная", text, StringComparison.Ordinal);
        }

        foreach (var term in new[]
        {
            "духовный конфликт посмертия",
            "духовное действие посмертия",
            "духовные искусства",
            "обмен действиями",
            "журнал духовного боя",
            "состояние контроля",
            "аудит кубиков",
            "выигрыш контрприёма",
            "принудительное воплощение",
            "сохранённый ранг Сияния",
            "уровень искусства",
            "прямой поединок",
            "поединок чемпиона"
        })
        {
            Assert.Contains(term, glossary, StringComparison.OrdinalIgnoreCase);
        }
    }

    [Fact]
    public void SourceOfLightCapstoneContractIsDocumentedForGm()
    {
        var matrix = ReadRepoFile("OtherGuides", "Afterlife_Contract_Matrix.md");
        var examples = ReadRepoFile("Examples", "E_CLI_Afterlife_Turns.txt");
        var apiSpec = ReadRepoFile("CLI_API_Specification.md");
        var daemonSpec = ReadRepoFile("CLI_Agent_Daemon_Specification.md");
        var taskGuide = ReadRepoFile("TaskGuides", "CLI_Step_Main.txt");
        var daemonScript = ReadRepoFile("BookOfEternityClient", "game_master_daemon.ps1");
        var docs = new[] { matrix, examples, apiSpec, daemonSpec, taskGuide, daemonScript };
        const string pendingFileName = "pending_source_of_light_capstone.json";

        foreach (var doc in docs)
        {
            Assert.Contains(pendingFileName, doc, StringComparison.Ordinal);
            Assert.Contains(SourceOfLightCapstoneState.PassiveId, doc, StringComparison.Ordinal);
            Assert.Contains(SourceOfLightCapstoneState.RelicId, doc, StringComparison.Ordinal);
            Assert.Contains(SourceOfLightCapstoneState.ShiningStateProperty, doc, StringComparison.Ordinal);
        }

        foreach (var doc in new[] { matrix, examples, apiSpec, taskGuide })
        {
            Assert.Contains("/source_of_light", doc, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("/источник_света", doc, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("radiance.tier = 4", doc, StringComparison.Ordinal);
            Assert.Contains("radiance.experience >= 580", doc, StringComparison.Ordinal);
            Assert.Contains("not a Shining core action", doc, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("coreActionReceipts[]", doc, StringComparison.OrdinalIgnoreCase);
        }

        foreach (var doc in new[] { matrix, examples, apiSpec })
        {
            Assert.Contains("Воплощение Света", doc, StringComparison.Ordinal);
            Assert.Contains("Воплощенный Свет", doc, StringComparison.Ordinal);
            Assert.Contains("afterlife_spiritual_conflict_v1", doc, StringComparison.Ordinal);
            Assert.Contains("modifierBreakdown", doc, StringComparison.Ordinal);
            Assert.Contains("+8", doc, StringComparison.Ordinal);
            Assert.Contains("+4", doc, StringComparison.Ordinal);
            Assert.Contains("+25", doc, StringComparison.Ordinal);
        }

        foreach (var characteristic in Characteristics.All)
            Assert.Contains(characteristic, examples, StringComparison.Ordinal);

        Assert.Contains("example 25", daemonSpec + daemonScript + examples, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void AfterlifePlayerPreviewsExposeFullAuditJsonForTravelOfferingsResidentsAndShining()
    {
        var guardiansTrade = ReadRepoFile(
            "BookOfEternityClient",
            "UI",
            "ExplorerMode",
            "ExplorerMode.Afterlife.GuardiansProjectsTrade.cs");
        var offerings = ReadRepoFile(
            "BookOfEternityClient",
            "UI",
            "ExplorerMode",
            "ExplorerMode.Afterlife.InkFeathersAndOfferings.cs");
        var statusAudit = ReadRepoFile(
            "BookOfEternityClient",
            "UI",
            "ExplorerMode",
            "ExplorerMode.Afterlife.StatusAudit.cs");
        var shiningOverview = ReadRepoFile(
            "BookOfEternityClient",
            "UI",
            "ExplorerMode",
            "ExplorerMode.Afterlife.ShiningAbode.cs");
        var shiningTradeForge = ReadRepoFile(
            "BookOfEternityClient",
            "UI",
            "ExplorerMode",
            "ExplorerMode.Afterlife.ShiningAbode.TradeAndForge.cs");

        foreach (var term in new[] { "previousActiveGuardianFull", "targetGuardianFull", "Полный JSON текущего Chaos Sea state перед свободным поиском" })
            Assert.Contains(term, guardiansTrade, StringComparison.Ordinal);

        foreach (var term in new[] { "consumedObjectFullJson", "consumedCollectionPath", "Полный JSON подношения реликвии", "Полный JSON подношения Архива" })
            Assert.Contains(term, offerings, StringComparison.Ordinal);

        foreach (var term in new[] { "residentFullJsonBefore", "soulQuestsFullJsonBefore", "Полный JSON transferReceipts резидента" })
            Assert.Contains(term, guardiansTrade, StringComparison.Ordinal);

        foreach (var term in new[] { "Полный JSON game_state/meta/shining_abode_state.json", "Полный JSON Врат Сияющей Обители", "Полный JSON preparedIncarnationPackage" })
            Assert.Contains(term, statusAudit, StringComparison.Ordinal);

        Assert.Contains("JSON shining_abode_state.factions/projects для просмотра", shiningOverview, StringComparison.Ordinal);
        Assert.Contains("Полный JSON forge request payload preview", shiningTradeForge, StringComparison.Ordinal);
        foreach (var term in new[]
        {
            "Полный JSON progression_schedule.json",
            "Полный JSON input/turn_request.json.progressionControl",
            "Полный JSON progression_report.json",
            "Полный JSON progression_report.progressionProcessingReport"
        })
            Assert.Contains(term, statusAudit, StringComparison.Ordinal);

        foreach (var term in new[] { "targetGuardianReadable", "expectedReputationDelta", "expectedReputationAfter" })
            Assert.Contains(term, offerings, StringComparison.Ordinal);
    }

    [Fact]
    public void ShiningForgeExactMutationTableIsDocumentedForGm()
    {
        var matrix = ReadRepoFile("OtherGuides", "Afterlife_Contract_Matrix.md");
        var examples = ReadRepoFile("Examples", "E_CLI_Afterlife_Turns.txt");

        foreach (var doc in new[] { matrix, examples })
        {
            Assert.Contains("Exact Shining Forge Mutation Table", doc, StringComparison.Ordinal);
            Assert.Contains("forge_relic.reshape", doc, StringComparison.Ordinal);
            Assert.Contains("forge_relic.retune_property", doc, StringComparison.Ordinal);
            Assert.Contains("forge_relic.strengthen_band", doc, StringComparison.Ordinal);
            Assert.Contains("forge_relic.stabilize_echo", doc, StringComparison.Ordinal);
            Assert.Contains("forge_relic.uplift_rarity", doc, StringComparison.Ordinal);
            Assert.Contains("required Radiance tier", doc, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("service multiplier", doc, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("minimum property counts", doc, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("common -> uncommon -> rare -> epic -> legendary", doc, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("pendingShiningBlessingEffects", doc, StringComparison.Ordinal);
            Assert.Contains("consumed", doc, StringComparison.Ordinal);
            Assert.Contains("expired", doc, StringComparison.Ordinal);
        }
    }

    [Fact]
    public void ChaosSeaGachaDocsMatchValidatedModifierAndCostContract()
    {
        var daemonSpec = ReadRepoFile("CLI_Agent_Daemon_Specification.md");
        var matrix = ReadRepoFile("OtherGuides", "Afterlife_Contract_Matrix.md");
        var examples = ReadRepoFile("Examples", "E_CLI_Afterlife_Turns.txt");
        var guardianRules = ReadRepoFile("Rules", "Block_32_Guardians.txt");
        var lifecyclePrompt = ReadRepoFile("BookOfEternityClient", "Core", "GameEngine", "GameEngine.TurnLifecycle.cs");
        var inkFeatherPreview = ReadRepoFile("BookOfEternityClient", "UI", "ExplorerMode", "ExplorerMode.Afterlife.InkFeathersAndOfferings.cs");

        foreach (var text in new[] { daemonSpec, matrix, examples, guardianRules, lifecyclePrompt })
        {
            Assert.Contains("[CHAOS_SEA_DIRECT_GACHA]", text, StringComparison.Ordinal);
            Assert.Contains("Чернильных Перьев", text, StringComparison.Ordinal);
            Assert.Contains("Ink Feathers", text, StringComparison.Ordinal);
            Assert.Contains("Abode Power", text, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("relic_forging", text, StringComparison.Ordinal);
            Assert.Contains("Guardian reputation", text, StringComparison.OrdinalIgnoreCase);
        }

        Assert.Contains("diceUsed", matrix, StringComparison.Ordinal);
        Assert.Contains("diceUsed", examples, StringComparison.Ordinal);
        Assert.Contains("diceUsed", inkFeatherPreview, StringComparison.Ordinal);
        Assert.DoesNotContain("Improve Gacha rates by", guardianRules, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Maximum Gacha benefits", guardianRules, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Guardian reputation bonus (Block 32)", lifecyclePrompt, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Hard Mode: +1 tier", guardianRules, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Impossible Mode: +1 tier", guardianRules, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void MandatoryPromptEntrypointsPointToAfterlifeMatrixAndExamples()
    {
        var entrypointPaths = new[]
        {
            Path.Combine("CLI_Agent_Daemon_Specification.md"),
            Path.Combine("TaskGuides", "CLI_Step_Main.txt"),
            Path.Combine("BookOfEternityClient", "Launcher", "CLI_Launch_Script.md"),
            Path.Combine("BookOfEternityClient", "Launcher", "Generate_CLI_Launch_Script.ps1"),
            Path.Combine("BookOfEternityClient", "game_master_daemon.ps1")
        };

        foreach (var relativePath in entrypointPaths)
        {
            var text = File.ReadAllText(Path.Combine(TestRepoPaths.RepoRoot, relativePath));
            Assert.Contains("OtherGuides/Afterlife_Contract_Matrix.md", NormalizeSeparators(text), StringComparison.Ordinal);
            Assert.Contains("Examples/E_CLI_Afterlife_Turns.txt", NormalizeSeparators(text), StringComparison.Ordinal);
        }

        var taskGuide = ReadRepoFile("TaskGuides", "CLI_Step_Main.txt");
        var daemonSpec = ReadRepoFile("CLI_Agent_Daemon_Specification.md");
        var daemonScript = ReadRepoFile("BookOfEternityClient", "game_master_daemon.ps1");

        Assert.Contains("example 19", taskGuide, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("examples 14-26", daemonSpec, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("example 19", daemonScript, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("example 20", daemonScript, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("example 21", daemonScript, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("example 22", daemonScript, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("example 23", daemonScript, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("example 24", daemonScript, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("example 25", daemonScript, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void AfterlifeDocsExposeClientCodeFallbackWithoutReplacingPrompts()
    {
        var matrix = ReadRepoFile("OtherGuides", "Afterlife_Contract_Matrix.md");
        var taskGuide = ReadRepoFile("TaskGuides", "CLI_Step_Main.txt");
        var daemonSpec = ReadRepoFile("CLI_Agent_Daemon_Specification.md");

        foreach (var text in new[] { matrix, taskGuide, daemonSpec })
        {
            Assert.Contains("fallback", text, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("client code", text, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("canonical", text, StringComparison.OrdinalIgnoreCase);
        }

        Assert.DoesNotContain(
            "The GM does not need to read client code.",
            matrix,
            StringComparison.Ordinal);
        Assert.Contains("normally does not need to read client code", matrix, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("FileMapping.cs", daemonSpec, StringComparison.Ordinal);
        Assert.Contains("Validation/", daemonSpec, StringComparison.Ordinal);
        Assert.Contains("gm_thoughts_markdown", daemonSpec, StringComparison.Ordinal);
        Assert.Contains("pending file name", taskGuide, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("must not be used to invent new gameplay outcomes", matrix, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void RealmDocsDoNotTreatEmptyCurrentRealmAsChaosSea()
    {
        var daemonSpec = ReadRepoFile("CLI_Agent_Daemon_Specification.md");
        var matrix = ReadRepoFile("OtherGuides", "Afterlife_Contract_Matrix.md");
        var block0 = ReadRepoFile("Rules", "Block_0.txt");
        var launchScript = ReadRepoFile("BookOfEternityClient", "Launcher", "CLI_Launch_Script.md");
        var launchGenerator = ReadRepoFile("BookOfEternityClient", "Launcher", "Generate_CLI_Launch_Script.ps1");

        foreach (var text in new[] { daemonSpec, matrix, block0, launchScript, launchGenerator })
        {
            Assert.Contains("unresolved realm", text, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("Do not infer Chaos Sea", text, StringComparison.OrdinalIgnoreCase);
        }

        Assert.DoesNotContain("empty/`Chaos Sea`", matrix, StringComparison.Ordinal);
        Assert.DoesNotContain("\"Chaos Sea\" / null", launchScript, StringComparison.Ordinal);
        Assert.DoesNotContain("\"Chaos Sea\" / null", launchGenerator, StringComparison.Ordinal);
        Assert.DoesNotContain("\"Chaos Sea\" / `null` / пусто", daemonSpec, StringComparison.Ordinal);
    }

    [Fact]
    public void AfterlifeWorkedExamplesHaveRuntimeScenarioOrExplicitCoverageExemption()
    {
        var examples = ReadRepoFile("Examples", "E_CLI_Afterlife_Turns.txt");
        var manifest = ExampleValidationManifest.Load();
        var scenarioIds = manifest.RuntimeScenarios
            .Where(scenario => string.Equals(scenario.File, "E_CLI_Afterlife_Turns.txt", StringComparison.OrdinalIgnoreCase))
            .Select(scenario => scenario.Id)
            .ToHashSet(StringComparer.Ordinal);

        var exampleNumbers = Regex.Matches(examples, @"(?m)^(?:EXAMPLE\s+(\d+)\b|(\d+)\. VALID )")
            .Select(match => int.Parse(match.Groups[1].Success ? match.Groups[1].Value : match.Groups[2].Value))
            .Distinct()
            .OrderBy(number => number)
            .ToArray();

        Assert.Equal(Enumerable.Range(1, 27).ToArray(), exampleNumbers);

        var coverageByExample = manifest.AfterlifeExampleCoverage
            .GroupBy(entry => entry.ExampleNumber)
            .ToDictionary(group => group.Key, group => group.ToArray());
        var staleCoverageEntries = coverageByExample.Keys
            .Except(exampleNumbers)
            .OrderBy(number => number)
            .ToArray();

        Assert.Empty(staleCoverageEntries);

        foreach (var exampleNumber in exampleNumbers)
        {
            Assert.True(
                coverageByExample.TryGetValue(exampleNumber, out var entries),
                $"Afterlife example {exampleNumber} must have runtime coverage or an explicit coverage exemption.");

            Assert.Single(entries!);
            var entry = entries![0];
            if (entry.RuntimeScenarioIds.Length == 0)
            {
                Assert.False(
                    string.IsNullOrWhiteSpace(entry.ExemptionReason),
                    $"Afterlife example {exampleNumber} coverage exemption must explain why runtime validation is not practical.");
                continue;
            }

            foreach (var scenarioId in entry.RuntimeScenarioIds)
            {
                Assert.True(
                    scenarioIds.Contains(scenarioId),
                    $"Afterlife example {exampleNumber} references missing runtime scenario '{scenarioId}'.");
            }
        }
    }

    [Fact]
    public void SarefCharacterBibleCoversRequiredRoleplayContract()
    {
        var bible = ReadRepoFile("OtherGuides", "Saref_Character_Bible.md");

        foreach (var requiredText in new[]
                 {
                     "Крылья над Бездной",
                     "Крылья Ангелов",
                     "Сияющая Обитель",
                     "Море Хаоса",
                     "мужской облик",
                     "женский облик",
                     "публичная ложь",
                     "истинная цель",
                     "не карикатурный злодей",
                     "не навязывает романтику",
                     "не спойлерит Сарефа",
                     "сделка",
                     "конфликт",
                     "романтика",
                     "поражение",
                     "победа",
                     "стирание памяти",
                     "клятва",
                     "боевое поведение"
                 })
        {
            Assert.Contains(requiredText, bible, StringComparison.OrdinalIgnoreCase);
        }
    }

    [Fact]
    public void SarefCharacterBibleCoversExplicitRomanceProfile()
    {
        var bible = ReadRepoFile("OtherGuides", "Saref_Character_Bible.md");
        var romanceProfile = ExtractRequiredSection(
            bible,
            "## Романтический профиль",
            "## Боевое поведение");

        foreach (var requiredText in new[]
                 {
                     "мужской облик",
                     "женский облик",
                     "игрок сам",
                     "не навязывает",
                     "антагонист",
                     "искушение",
                     "не романтический",
                     "клятва",
                     "разрыв клятвы",
                     "трагический",
                     "фан-сервис"
                 })
        {
            Assert.Contains(requiredText, romanceProfile, StringComparison.OrdinalIgnoreCase);
        }
    }

    [Fact]
    public void TaskGuideRoutesSarefMainStoryTurnsToAfterlifeExample27()
    {
        var taskGuide = ReadRepoFile("TaskGuides", "CLI_Step_Main.txt");

        Assert.Contains("Examples/E_CLI_Afterlife_Turns.txt", taskGuide, StringComparison.Ordinal);
        Assert.Contains("examples 10-27", taskGuide, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("example 27", taskGuide, StringComparison.OrdinalIgnoreCase);
        Assert.Contains(SarefMainStoryState.StatePath, taskGuide, StringComparison.Ordinal);
        Assert.Contains(SarefMainStoryState.PendingWingsInfiltrationPath, taskGuide, StringComparison.Ordinal);
        Assert.Contains("sarefAdvantages", taskGuide, StringComparison.Ordinal);
        Assert.Contains("finalConfrontation", taskGuide, StringComparison.Ordinal);
        Assert.Contains("playerOathState", taskGuide, StringComparison.Ordinal);
        Assert.Contains("defeatOutcomes", taskGuide, StringComparison.Ordinal);
        Assert.Contains("Крылья над Бездной", taskGuide, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Сареф", taskGuide, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("не раскрывай", taskGuide, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void SarefMainStoryRuntimeContractIsDocumented()
    {
        var matrix = ReadRepoFile("OtherGuides", "Afterlife_Contract_Matrix.md");
        var examples = ReadRepoFile("Examples", "E_CLI_Afterlife_Turns.txt");
        var manifest = ReadRepoFile("Examples", "example_validation_manifest.json");
        var inventory = ReadRepoFile("OtherGuides", "Afterlife_Pending_Control_Surface_Inventory.json");
        var daemonSpec = ReadRepoFile("CLI_Agent_Daemon_Specification.md");
        var apiSpec = ReadRepoFile("CLI_API_Specification.md");
        var taskGuide = ReadRepoFile("TaskGuides", "CLI_Step_Main.txt");
        var operations = ReadRepoFile("Rules", "Block_CLI_Operations.txt");

        foreach (var text in new[] { matrix, examples, manifest, inventory, daemonSpec, apiSpec, taskGuide, operations })
        {
            Assert.Contains(SarefMainStoryState.StatePath, text, StringComparison.Ordinal);
            Assert.Contains("main_story_saref_state", text, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("Крылья над Бездной", text, StringComparison.OrdinalIgnoreCase);
        }

        Assert.Contains("saref_reveal_stage", manifest, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("sarefMainStoryState", examples, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("No-spoiler", matrix, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("wings_revealed", matrix, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("questStates", matrix, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("questOrdinal", examples, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("physical mortal item", examples, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("sarefAdvantageUses", matrix, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("sarefAdvantageUses", examples, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("sarefAdvantageUses", manifest, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("sarefAdvantageUses", inventory, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("applicableScenes", matrix, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("applicableScenes", examples, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("spentAudit", matrix, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("suppressed", matrix, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("knownAgents", matrix, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("knownAgents", examples, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("shadowTraces", matrix, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("supporterArchetype", matrix, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("deceived", matrix, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("oathbound", matrix, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("fanatic", matrix, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("opportunist", matrix, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("sarefFactionRole", matrix, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("sarefVisibility", matrix, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("normal faction UI", matrix, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("/сареф", examples, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("/крылья_над_бездной", examples, StringComparison.OrdinalIgnoreCase);

        foreach (var text in new[] { matrix, examples, manifest, inventory, daemonSpec, apiSpec, taskGuide, operations })
        {
            Assert.Contains(SarefMainStoryState.MemorySceneUpdateModeRecord, text, StringComparison.Ordinal);
            Assert.Contains("memoryScene", text, StringComparison.Ordinal);
            Assert.Contains("memorySceneProof", text, StringComparison.Ordinal);
            Assert.Contains(SarefMainStoryState.MemorySceneLayerName, text, StringComparison.Ordinal);
            Assert.Contains("role", text, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("boundaries", text, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("abilities", text, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("requiredStoryNodes", text, StringComparison.Ordinal);
            Assert.Contains("successCondition", text, StringComparison.Ordinal);
            Assert.Contains("closureTarget", text, StringComparison.Ordinal);
            Assert.Contains("Mortal World", text, StringComparison.Ordinal);
            Assert.Contains("Memory Gates", text, StringComparison.Ordinal);
        }

        foreach (var text in new[] { matrix, examples, manifest, inventory, daemonSpec, apiSpec, taskGuide, operations })
        {
            Assert.Contains(SarefMainStoryState.FinalUpdateModeRecord, text, StringComparison.Ordinal);
            Assert.Contains("finalConfrontation", text, StringComparison.Ordinal);
            Assert.Contains(SarefMainStoryState.FinalRouteCombat, text, StringComparison.Ordinal);
            Assert.Contains(SarefMainStoryState.FinalRoutePolitical, text, StringComparison.Ordinal);
            Assert.Contains(SarefMainStoryState.FinalRouteOathLaw, text, StringComparison.Ordinal);
            Assert.Contains(SarefMainStoryState.FinalRouteMetaphysical, text, StringComparison.Ordinal);
            Assert.Contains(SarefMainStoryState.FinalRouteHybrid, text, StringComparison.Ordinal);
            Assert.Contains(SarefMainStoryState.FinalRouteDeal, text, StringComparison.Ordinal);
            Assert.Contains(SarefMainStoryState.FinalVictoryPyrrhic, text, StringComparison.Ordinal);
            Assert.Contains(SarefMainStoryState.FinalVictoryClean, text, StringComparison.Ordinal);
            Assert.Contains(SarefMainStoryState.FinalVictoryDeep, text, StringComparison.Ordinal);
            Assert.Contains(SarefMainStoryState.FinalVictoryDeal, text, StringComparison.Ordinal);
            Assert.Contains("directScene", text, StringComparison.Ordinal);
            Assert.Contains("advantageUseIds", text, StringComparison.Ordinal);
            Assert.Contains("sarefOutcome", text, StringComparison.Ordinal);
            Assert.Contains("wingsFactionOutcome", text, StringComparison.Ordinal);
            Assert.Contains("allied", text, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("joined", text, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("rewardBundle", text, StringComparison.Ordinal);
            Assert.Contains("resourceReward", text, StringComparison.Ordinal);
            Assert.Contains("antiOathProtection", text, StringComparison.Ordinal);
            Assert.Contains("antiForeignProtection", text, StringComparison.Ordinal);
            Assert.Contains("guardianRelationshipEffects", text, StringComparison.Ordinal);
            Assert.Contains("deepWorldStateEffects", text, StringComparison.Ordinal);
            Assert.Contains("oathCost", text, StringComparison.Ordinal);
            Assert.Contains("factionLifecycle", text, StringComparison.Ordinal);
            Assert.Contains(SarefMainStoryState.DefeatUpdateModeRecord, text, StringComparison.Ordinal);
            Assert.Contains("defeatOutcomes", text, StringComparison.Ordinal);
            Assert.Contains(SarefMainStoryState.DefeatOutcomeForcedOath, text, StringComparison.Ordinal);
            Assert.Contains(SarefMainStoryState.DefeatOutcomeExileToChaosSea, text, StringComparison.Ordinal);
            Assert.Contains(SarefMainStoryState.DefeatOutcomeMemorySuppression, text, StringComparison.Ordinal);
            Assert.Contains(SarefMainStoryState.DefeatOutcomeSoulDissipation, text, StringComparison.Ordinal);
            Assert.Contains(SarefMainStoryState.DefeatOutcomePyrrhicEscape, text, StringComparison.Ordinal);
            Assert.Contains("playerOathState", text, StringComparison.Ordinal);
            Assert.Contains("exileAudit", text, StringComparison.Ordinal);
            Assert.Contains("memorySuppressionAudit", text, StringComparison.Ordinal);
            Assert.Contains("soulDissipationProofId", text, StringComparison.Ordinal);
            Assert.Contains("mitigation", text, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("postStoryAgenda", text, StringComparison.Ordinal);
            Assert.Contains(SarefMainStoryState.PostStoryUpdateModeRecordAgenda, text, StringComparison.Ordinal);
            Assert.Contains(SarefMainStoryState.PostStoryStateOathbound, text, StringComparison.Ordinal);
            Assert.Contains("assignments", text, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("dominationScene", text, StringComparison.Ordinal);
            Assert.Contains(ShiningAbodeState.FactionConflictCampaignsProperty, text, StringComparison.Ordinal);
            Assert.Contains(ShiningAbodeState.FactionCampaignBreakthroughSarefDirective, text, StringComparison.Ordinal);
            Assert.Contains(SarefMainStoryState.OathBreakUpdateModeRecord, text, StringComparison.Ordinal);
            Assert.Contains("oathBreakArc", text, StringComparison.Ordinal);
            Assert.Contains(SarefMainStoryState.OathBreakStateNotStarted, text, StringComparison.Ordinal);
            Assert.Contains(SarefMainStoryState.OathBreakStateActive, text, StringComparison.Ordinal);
            Assert.Contains(SarefMainStoryState.OathBreakStateFailed, text, StringComparison.Ordinal);
            Assert.Contains(SarefMainStoryState.OathBreakStateBroken, text, StringComparison.Ordinal);
            Assert.Contains(SarefMainStoryState.OathBreakRouteSeret, text, StringComparison.Ordinal);
            Assert.Contains(SarefMainStoryState.OathBreakRouteLucian, text, StringComparison.Ordinal);
            Assert.Contains(SarefMainStoryState.OathBreakRouteIlarion, text, StringComparison.Ordinal);
            Assert.Contains(SarefMainStoryState.OathBreakRouteVeyra, text, StringComparison.Ordinal);
            Assert.Contains(SarefMainStoryState.OathBreakRouteDeepStoryEvidence, text, StringComparison.Ordinal);
            Assert.Contains(SarefMainStoryState.OathBreakConsequenceRenegade, text, StringComparison.Ordinal);
            Assert.Contains(SarefMainStoryState.OathBreakConsequenceOathReversed, text, StringComparison.Ordinal);
            Assert.Contains(SarefMainStoryState.OathBreakConsequenceBelovedTraitor, text, StringComparison.Ordinal);
            Assert.Contains(SarefMainStoryState.OathBreakConsequenceSecondConfrontation, text, StringComparison.Ordinal);
            Assert.Contains(SarefMainStoryState.SceneOathBreak, text, StringComparison.Ordinal);
        }

        foreach (var text in new[] { matrix, examples, inventory, daemonSpec, apiSpec, taskGuide, operations })
        {
            Assert.Contains(SarefMainStoryState.PendingWingsInfiltrationPath, text, StringComparison.Ordinal);
            Assert.Contains(SarefMainStoryState.ResponseField, text, StringComparison.Ordinal);
            Assert.Contains(SarefMainStoryState.WingsUpdateModeReveal, text, StringComparison.Ordinal);
            Assert.Contains(SarefMainStoryState.WingsUpdateModeRefuse, text, StringComparison.Ordinal);
            Assert.Contains(SarefMainStoryState.WingsUpdateModeBlock, text, StringComparison.Ordinal);
            Assert.Contains(SarefMainStoryState.WingsRouteSafetySafe, text, StringComparison.Ordinal);
            Assert.Contains(SarefMainStoryState.WingsRouteSafetyRisky, text, StringComparison.Ordinal);
            Assert.Contains(SarefMainStoryState.WingsRouteSafetyDesperate, text, StringComparison.Ordinal);
            Assert.Contains("disadvantages", text, StringComparison.OrdinalIgnoreCase);
        }
    }

    [Fact]
    public void SarefAzaliaQuestlineBibleCoversFourDarkFantasyQuests()
    {
        AssertSarefGuardianQuestlineBible(
            "azalia.md",
            "Азалия",
            "faction",
            "Ложная преданность",
            "политичес",
            "преданность");
    }

    [Fact]
    public void SarefLissaraQuestlineBibleCoversFourDarkFantasyQuests()
    {
        AssertSarefGuardianQuestlineBible(
            "lissara.md",
            "Лиссара",
            "exile_survival",
            "Тропа изгнанника",
            "выжив",
            "изгнан");
    }

    [Fact]
    public void SarefMyrielQuestlineBibleCoversFourDarkFantasyQuests()
    {
        AssertSarefGuardianQuestlineBible(
            "myriel.md",
            "Мириэль",
            "identity",
            "Пепельная формула чужого мира",
            "иномиров",
            "знани");
    }

    [Fact]
    public void SarefSeretQuestlineBibleCoversFourDarkFantasyQuests()
    {
        AssertSarefGuardianQuestlineBible(
            "seret.md",
            "Серет",
            "oath_break",
            "Разомкнутый договор",
            "клятв",
            "долг");
    }

    [Fact]
    public void SarefVarakQuestlineBibleCoversFourDarkFantasyQuests()
    {
        AssertSarefGuardianQuestlineBible(
            "varak.md",
            "Варак",
            "war_doctrine",
            "Трещина в строю",
            "войн",
            "стро");
    }

    [Fact]
    public void SarefBrannQuestlineBibleCoversFourDarkFantasyQuests()
    {
        AssertSarefGuardianQuestlineBible(
            "brann.md",
            "Бранн",
            "structural_weakness",
            "Клеймо разлома",
            "ремес",
            "структур");
    }

    [Fact]
    public void SarefIlarionQuestlineBibleCoversFourDarkFantasyQuests()
    {
        AssertSarefGuardianQuestlineBible(
            "ilarion.md",
            "Иларион",
            "method",
            "Якорь памяти",
            "памят",
            "архив");
    }

    [Fact]
    public void SarefVeyraQuestlineBibleCoversFourDarkFantasyQuests()
    {
        AssertSarefGuardianQuestlineBible(
            "veyra.md",
            "Вейра",
            "path",
            "Маска среди Крыльев",
            "маск",
            "путь");
    }

    [Fact]
    public void SarefLucianQuestlineBibleCoversFourDarkFantasyQuests()
    {
        AssertSarefGuardianQuestlineBible(
            "lucian.md",
            "Люциан",
            "false_light_cut",
            "Лунный Разрез Клятвы",
            "клин",
            "ложн");
    }

    private static void AssertSarefGuardianQuestlineBible(
        string fileName,
        string guardianName,
        string revelationCategory,
        string advantageName,
        params string[] identityTerms)
    {
        var bible = ReadRepoFile("OtherGuides", "Saref_Guardian_Questlines", fileName);

        foreach (var requiredText in new[]
                 {
                     guardianName,
                     "Крылья над Бездной",
                     "Крылья Ангелов",
                     "Сареф",
                     revelationCategory,
                     advantageName,
                     "sarefRevelation",
                     "sarefAdvantage",
                     "духовный слепок",
                     "не переносятся физически",
                     "GM не должен"
                 })
        {
            Assert.Contains(requiredText, bible, StringComparison.OrdinalIgnoreCase);
        }

        foreach (var identityTerm in identityTerms)
        {
            Assert.Contains(identityTerm, bible, StringComparison.OrdinalIgnoreCase);
        }

        for (var questNumber = 1; questNumber <= 4; questNumber++)
        {
            Assert.Contains($"## Квест {questNumber}", bible, StringComparison.OrdinalIgnoreCase);
        }

        foreach (var requiredSection in new[]
                 {
                     "Тип",
                     "Смертная жизнь",
                     "Что делает игрок",
                     "Трагический конфликт",
                     "Ключевая сцена",
                     "Моральная цена",
                     "Что раскрывается о Хранителе"
                 })
        {
            Assert.Contains(requiredSection, bible, StringComparison.OrdinalIgnoreCase);
        }
    }

    private static string[] ShiningConstantValues(params string[] prefixes) =>
        typeof(ShiningAbodeState)
            .GetFields(BindingFlags.Public | BindingFlags.Static | BindingFlags.FlattenHierarchy)
            .Where(field => field is { IsLiteral: true, IsInitOnly: false } &&
                            field.FieldType == typeof(string) &&
                            prefixes.Any(prefix => field.Name.StartsWith(prefix, StringComparison.Ordinal)))
            .Select(field => (string)field.GetRawConstantValue()!)
            .Distinct(StringComparer.Ordinal)
            .OrderBy(value => value, StringComparer.Ordinal)
            .ToArray();

    private static string[] ResidentConstantValues(params string[] prefixes) =>
        typeof(GuardianAbodeResidentState)
            .GetFields(BindingFlags.Public | BindingFlags.Static | BindingFlags.FlattenHierarchy)
            .Where(field => field is { IsLiteral: true, IsInitOnly: false } &&
                            field.FieldType == typeof(string) &&
                            prefixes.Any(prefix => field.Name.StartsWith(prefix, StringComparison.Ordinal)))
            .Select(field => (string)field.GetRawConstantValue()!)
            .Distinct(StringComparer.Ordinal)
            .OrderBy(value => value, StringComparer.Ordinal)
            .ToArray();

    private static bool IsAfterlifePendingFile(string fileName) =>
        fileName.StartsWith("pending_shining_", StringComparison.Ordinal) ||
        fileName.StartsWith("pending_guardian_", StringComparison.Ordinal) ||
        fileName is
            "pending_abode_offering.json" or
            "pending_archive_consultation_request.json" or
            "pending_archive_project_fuel_request.json" or
            "pending_player_guardian_foundation.json" or
            "pending_source_of_light_capstone.json" or
            "pending_saref_wings_infiltration.json" or
            "pending_resident_companion_manifestation_request.json";

    private static string ExtractRequiredSection(string text, string startMarker, string endMarker)
    {
        var start = text.IndexOf(startMarker, StringComparison.Ordinal);
        Assert.True(start >= 0, $"Missing section start marker: {startMarker}");

        start += startMarker.Length;
        var end = text.IndexOf(endMarker, start, StringComparison.Ordinal);
        Assert.True(end >= 0, $"Missing section end marker: {endMarker}");

        return text[start..end];
    }

    private static string ReadRepoFile(params string[] parts) =>
        File.ReadAllText(Path.Combine(new[] { TestRepoPaths.RepoRoot }.Concat(parts).ToArray()));

    private static string NormalizeSeparators(string text) =>
        text.Replace('\\', '/');
}
