using System.Reflection;
using System.Text.RegularExpressions;
using BookOfEternityClient.Configuration;
using BookOfEternityClient.Services;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed class AfterlifeDocumentationCoverageTests
{
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
        Assert.Contains("\"quotedCostFeathers\": 0", examples, StringComparison.Ordinal);
        Assert.Contains("\"quotedCostLightSparks\": 0", examples, StringComparison.Ordinal);

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

        Assert.DoesNotContain("returned_from_shining_abode", examples, StringComparison.Ordinal);
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
            "departed_to_neutral"
        })
        {
            Assert.Contains(requiredTerm, examples, StringComparison.Ordinal);
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

        Assert.Contains("{ \"requests\": [] }", matrix, StringComparison.Ordinal);
        Assert.Contains("{ \"requests\": [] }", apiSpec, StringComparison.Ordinal);
    }

    [Fact]
    public void AfterlifePromptDocsDescribeHandoffAsTriggerOnlyAndFileLevelForbiddenRule()
    {
        var matrix = ReadRepoFile("OtherGuides", "Afterlife_Contract_Matrix.md");
        var apiSpec = ReadRepoFile("CLI_API_Specification.md");
        var daemonSpec = ReadRepoFile("CLI_Agent_Daemon_Specification.md");
        var launchScript = ReadRepoFile("BookOfEternityClient", "Launcher", "CLI_Launch_Script.md");
        var launchGenerator = ReadRepoFile("BookOfEternityClient", "Launcher", "Generate_CLI_Launch_Script.ps1");
        var docs = new[] { matrix, apiSpec, daemonSpec, launchScript, launchGenerator };

        foreach (var doc in docs)
        {
            Assert.Contains("TriggerIncarnation", doc, StringComparison.Ordinal);
            Assert.Contains("game_state/world/*", doc, StringComparison.Ordinal);
            Assert.Contains("game_state/npcs/*", doc, StringComparison.Ordinal);
            Assert.Contains("game_state/factions/*", doc, StringComparison.Ordinal);
        }

        foreach (var doc in new[] { apiSpec, daemonSpec, launchScript, launchGenerator })
        {
            Assert.Contains("client", doc, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("Mortal bootstrap", doc, StringComparison.OrdinalIgnoreCase);
        }

        Assert.DoesNotContain("ONLY bootstrap/materialization", launchScript, StringComparison.Ordinal);
        Assert.DoesNotContain("ONLY bootstrap/materialization", launchGenerator, StringComparison.Ordinal);
        Assert.DoesNotContain("GM sends player to Mortal World", apiSpec, StringComparison.Ordinal);
        Assert.Contains("validation repair", daemonSpec, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("runtime normalization clears", daemonSpec, StringComparison.Ordinal);
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
        var docs = new[] { matrix, apiSpec, daemonSpec, taskGuide, examples };

        foreach (var doc in docs)
        {
            Assert.Contains("game_state/world/*", doc, StringComparison.Ordinal);
            Assert.Contains("game_state/npcs/*", doc, StringComparison.Ordinal);
            Assert.Contains("game_state/factions/*", doc, StringComparison.Ordinal);
            Assert.Contains("pending_npc_social_interactions.json", doc, StringComparison.Ordinal);
            Assert.Contains("MortalWorldProfile-only", doc, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("stale/repair", doc, StringComparison.OrdinalIgnoreCase);
        }

        Assert.NotEmpty(FileMapping.FieldToFile.Where(pair =>
            pair.Value.StartsWith("game_state/world/", StringComparison.OrdinalIgnoreCase) ||
            pair.Value.StartsWith("game_state/npcs/", StringComparison.OrdinalIgnoreCase) ||
            pair.Value.StartsWith("game_state/factions/", StringComparison.OrdinalIgnoreCase)));
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

        foreach (var doc in new[] { matrix, taskGuide, examples, apiSpec })
        {
            Assert.Contains("archive_candidate_manifest.json", doc, StringComparison.Ordinal);
            Assert.Contains("client-owned", doc, StringComparison.OrdinalIgnoreCase);
        }
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
        }

        foreach (var doc in new[] { matrix, apiSpec, examples })
        {
            Assert.Contains("availability", doc, StringComparison.Ordinal);
            Assert.Contains("active", doc, StringComparison.Ordinal);
            Assert.Contains("sealed_until_next_ascension", doc, StringComparison.Ordinal);
            Assert.Contains("preparedIncarnationPackage", doc, StringComparison.Ordinal);
            Assert.Contains("system_guardian_attraction.json", doc, StringComparison.Ordinal);
        }

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
    public void ChaosSeaGachaDocsMatchValidatedModifierAndCostContract()
    {
        var daemonSpec = ReadRepoFile("CLI_Agent_Daemon_Specification.md");
        var matrix = ReadRepoFile("OtherGuides", "Afterlife_Contract_Matrix.md");
        var examples = ReadRepoFile("Examples", "E_CLI_Afterlife_Turns.txt");
        var guardianRules = ReadRepoFile("Rules", "Block_32_Guardians.txt");
        var lifecyclePrompt = ReadRepoFile("BookOfEternityClient", "Core", "GameEngine", "GameEngine.TurnLifecycle.cs");

        foreach (var text in new[] { daemonSpec, matrix, examples, guardianRules, lifecyclePrompt })
        {
            Assert.Contains("[CHAOS_SEA_DIRECT_GACHA]", text, StringComparison.Ordinal);
            Assert.Contains("Чернильных Перьев", text, StringComparison.Ordinal);
            Assert.Contains("Ink Feathers", text, StringComparison.Ordinal);
            Assert.Contains("Abode Power", text, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("relic_forging", text, StringComparison.Ordinal);
            Assert.Contains("Guardian reputation", text, StringComparison.OrdinalIgnoreCase);
        }

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
        Assert.Contains("examples 14-23", daemonSpec, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("example 19", daemonScript, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("example 20", daemonScript, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("example 21", daemonScript, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("example 22", daemonScript, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("example 23", daemonScript, StringComparison.OrdinalIgnoreCase);
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

        var exampleNumbers = Regex.Matches(examples, @"(?m)^(\d+)\. VALID ")
            .Select(match => int.Parse(match.Groups[1].Value))
            .OrderBy(number => number)
            .ToArray();

        Assert.Equal(Enumerable.Range(1, 23).ToArray(), exampleNumbers);

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
