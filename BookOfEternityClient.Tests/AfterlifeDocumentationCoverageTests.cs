using System.Reflection;
using System.Text.RegularExpressions;
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
    public void AfterlifeArchiveUpdatesAndDerivedNotificationTriggersAreDocumented()
    {
        var matrix = ReadRepoFile("OtherGuides", "Afterlife_Contract_Matrix.md");
        var apiSpec = ReadRepoFile("CLI_API_Specification.md");
        var taskGuide = ReadRepoFile("TaskGuides", "CLI_Step_Main.txt");
        var examples = ReadRepoFile("Examples", "E_CLI_Afterlife_Turns.txt");

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
    public void ShiningQueueLimitsAvailabilityAndControlSurfacesAreDocumented()
    {
        var matrix = ReadRepoFile("OtherGuides", "Afterlife_Contract_Matrix.md");
        var apiSpec = ReadRepoFile("CLI_API_Specification.md");
        var daemonSpec = ReadRepoFile("CLI_Agent_Daemon_Specification.md");
        var examples = ReadRepoFile("Examples", "E_CLI_Afterlife_Turns.txt");

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
            Assert.Contains("currentLocationData", text, StringComparison.Ordinal);
            Assert.Contains("worldEventsLog", text, StringComparison.Ordinal);
        }

        Assert.Contains("[CHAOS_SEA_TRAVEL]", daemonSpec, StringComparison.Ordinal);
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
        Assert.Contains("examples 14-22", daemonSpec, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("example 19", daemonScript, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("example 20", daemonScript, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("example 21", daemonScript, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("example 22", daemonScript, StringComparison.OrdinalIgnoreCase);
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

        Assert.Equal(Enumerable.Range(1, 22).ToArray(), exampleNumbers);

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

    private static bool IsAfterlifePendingFile(string fileName) =>
        fileName.StartsWith("pending_shining_", StringComparison.Ordinal) ||
        fileName.StartsWith("pending_guardian_", StringComparison.Ordinal) ||
        fileName is
            "pending_abode_offering.json" or
            "pending_archive_consultation_request.json" or
            "pending_archive_project_fuel_request.json" or
            "pending_player_guardian_foundation.json" or
            "pending_resident_companion_manifestation_request.json";

    private static string ReadRepoFile(params string[] parts) =>
        File.ReadAllText(Path.Combine(new[] { TestRepoPaths.RepoRoot }.Concat(parts).ToArray()));

    private static string NormalizeSeparators(string text) =>
        text.Replace('\\', '/');
}
