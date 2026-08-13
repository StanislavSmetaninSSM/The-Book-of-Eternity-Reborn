using System.Text.Json;
using System.Text.Json.Nodes;
using BookOfEternityClient.Core;
using BookOfEternityClient.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed class PromptDocumentationCoverageTests
{
    [Fact]
    public void MortalItemIdentityIndex_IsClientOwnedAcrossValidationAndRepairMappings()
    {
        var issueClassification = ReadRepoFile(
            "BookOfEternityClient",
            "Services",
            "Validation",
            "ValidationService.PrivateImplementation.cs");
        var validationSurfaceClassification = ReadRepoFile(
            "BookOfEternityClient",
            "Services",
            "Validation",
            "ValidationService.NpcWorldAndMeta.cs");
        var itemRepairBuilder = ReadRepoFile(
            "BookOfEternityClient",
            "Services",
            "MortalItemRepairPacketBuilder.cs");

        Assert.Contains(
            "MortalItemRepairPacketBuilder.IsProtectedClientOwnedTarget",
            issueClassification,
            StringComparison.Ordinal);
        Assert.Contains(
            "MortalItemRepairPacketBuilder.IsProtectedClientOwnedTarget",
            validationSurfaceClassification,
            StringComparison.Ordinal);
        Assert.Contains(
            "MortalItemIdentityState.StatePath",
            itemRepairBuilder,
            StringComparison.Ordinal);
        Assert.Contains(
            "item_identity_index.json",
            itemRepairBuilder,
            StringComparison.Ordinal);
        Assert.Contains(
            "never a GM repair target",
            itemRepairBuilder,
            StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void InventoryMechanicalBonusAuthorityContract_IsDocumentedForGm()
    {
        var block10 = ReadRepoFile("Rules", "Block_10.txt");
        var example = ReadRepoFile("Examples", "E_Block_10.txt");

        foreach (var requiredText in new[]
        {
            "mechanicalSummaryAuthority",
            "mechanicalSummaryUnresolvedReason",
            "NarrativeOnly",
            "Unresolved",
            "structuredBonuses",
            "combatEffect",
            "customProperties",
            "display summaries only",
            "matching structured authority",
            "description/display text alone does not authorize mechanics",
            "target/value metadata must match"
        })
        {
            Assert.Contains(requiredText, block10, StringComparison.Ordinal);
        }

        foreach (var requiredText in new[]
        {
            "StructuredInventoryBonusAuthority_Example",
            "Репутация среди аристократов +3",
            "matching structured authority",
            "\"mechanicalSummaryAuthority\": \"NarrativeOnly\"",
            "\"mechanicalSummaryAuthority\": \"Unresolved\"",
            "\"mechanicalSummaryUnresolvedReason\""
        })
        {
            Assert.Contains(requiredText, example, StringComparison.Ordinal);
        }
    }

    [Fact]
    public void DaemonSpecDocumentsQteOfferRuntimeContract()
    {
        var daemonSpec = ReadRepoFile("CLI_Agent_Daemon_Specification.md");
        var apiSpec = ReadRepoFile("CLI_API_Specification.md");
        var qteExample = ReadRepoFile("Examples", "E_CLI_QTE_Offer.txt");
        var lifecyclePrompt = ReadRepoFile("BookOfEternityClient", "Core", "GameEngine", "GameEngine.TurnLifecycle.cs");

        Assert.Contains("QTE OFFERS", lifecyclePrompt, StringComparison.Ordinal);
        Assert.Contains("output/qte_offer.json", lifecyclePrompt, StringComparison.Ordinal);
        Assert.Contains("qteEventsEnabled", lifecyclePrompt, StringComparison.Ordinal);

        foreach (var requiredText in new[]
        {
            "Examples/E_CLI_QTE_Offer.txt",
            "output/qte_offer.json",
            "qteEventsEnabled",
            "ordinary player-driven Mortal World turn",
            "QTE-offer turn не должен одновременно",
            "responseFragment"
        })
        {
            Assert.Contains(requiredText, daemonSpec, StringComparison.Ordinal);
        }

        Assert.Contains("output/qte_offer.json", apiSpec, StringComparison.Ordinal);
        Assert.Contains("output/qte_offer.json", qteExample, StringComparison.Ordinal);
    }

    [Fact]
    public void QteLayoutIndependentKeyboardContract_IsDocumentedForGmAndPlayers()
    {
        var qteRules = ReadRepoFile("Rules", "Block_CLI_QTE.txt");
        var qteExample = ReadRepoFile("Examples", "E_CLI_QTE_Offer.txt");
        var stepGuide = ReadRepoFile("TaskGuides", "CLI_Step_Main.txt");
        var apiSpec = ReadRepoFile("CLI_API_Specification.md");

        foreach (var requiredText in new[]
        {
            "physical QTE keys",
            "Q / Й",
            "W / Ц",
            "E / У",
            "A / Ф",
            "S / Ы",
            "D / В",
            "GM-authored QTE configs do not encode player keyboard layout",
            "normal text input"
        })
        {
            Assert.Contains(requiredText, qteRules, StringComparison.Ordinal);
            Assert.Contains(requiredText, apiSpec, StringComparison.Ordinal);
        }

        foreach (var requiredText in new[]
        {
            "client handles physical key/RU-EN normalization",
            "GM-authored QTE configs do not encode player keyboard layout",
            "Q / Й",
            "normal text input"
        })
        {
            Assert.Contains(requiredText, qteExample, StringComparison.Ordinal);
            Assert.Contains(requiredText, stepGuide, StringComparison.Ordinal);
        }
    }

    [Fact]
    public void MashInputQteContract_IsDocumentedForGmAndPlayers()
    {
        var qteRules = ReadRepoFile("Rules", "Block_CLI_QTE.txt");
        var qteExample = ReadRepoFile("Examples", "E_CLI_QTE_Offer.txt");
        var apiSpec = ReadRepoFile("CLI_API_Specification.md");

        foreach (var requiredText in new[]
        {
            "MashInput",
            "check.config.keys",
            "durationMs",
            "targetPresses",
            "partialThreshold",
            "Escape/cancel resolves as fail",
            "Browser clients support MashInput through #918 mini-games"
        })
        {
            Assert.Contains(requiredText, qteRules, StringComparison.Ordinal);
            Assert.Contains(requiredText, apiSpec, StringComparison.Ordinal);
        }

        foreach (var requiredText in new[]
        {
            "\"type\": \"MashInput\"",
            "\"keys\": [\"space\"]",
            "\"durationMs\": 2500",
            "\"targetPresses\": 12",
            "\"partialThreshold\": 0.5",
            "\"terminalOutcomeId\": \"door_open\"",
            "\"terminalOutcomeId\": \"door_stuck\"",
            "\"terminalOutcomeId\": \"caught_at_door\""
        })
        {
            Assert.Contains(requiredText, qteExample, StringComparison.Ordinal);
        }
    }

    [Fact]
    public void ScoredQteContract_IsDocumentedForGmAndPlayers()
    {
        var qteRules = ReadRepoFile("Rules", "Block_CLI_QTE.txt");
        var qteExample = ReadRepoFile("Examples", "E_CLI_QTE_Offer.txt");
        var apiSpec = ReadRepoFile("CLI_API_Specification.md");

        foreach (var requiredText in new[]
        {
            "scoreModel",
            "metrics",
            "ranks",
            "rankOrder",
            "thresholds",
            "visibility",
            "always",
            "final",
            "hidden",
            "scoreDeltas",
            "final score summary",
            "Browser clients render scored QTE state read-only"
        })
        {
            Assert.Contains(requiredText, qteRules, StringComparison.Ordinal);
            Assert.Contains(requiredText, apiSpec, StringComparison.Ordinal);
        }

        foreach (var requiredText in new[]
        {
            "\"scoreModel\"",
            "\"scoreDeltas\"",
            "\"visibility\": \"always\"",
            "\"visibility\": \"final\"",
            "\"rankOrder\"",
            "\"id\": \"silver\"",
            "\"metric\": \"momentum\""
        })
        {
            Assert.Contains(requiredText, qteExample, StringComparison.Ordinal);
        }
    }

    [Fact]
    public void QtePracticeMode_IsDocumentedAsClientOwnedNoRewardTraining()
    {
        var qteRules = ReadRepoFile("Rules", "Block_CLI_QTE.txt");
        var qteExample = ReadRepoFile("Examples", "E_CLI_QTE_Offer.txt");
        var stepGuide = ReadRepoFile("TaskGuides", "CLI_Step_Main.txt");
        var apiSpec = ReadRepoFile("CLI_API_Specification.md");

        foreach (var requiredText in new[]
        {
            "QTE Practice Mode",
            "client-owned practice",
            "no rewards",
            "no GM-authored practice scenes",
            "does not mutate campaign state",
            "Daren",
            "#919"
        })
        {
            Assert.Contains(requiredText, qteRules, StringComparison.Ordinal);
            Assert.Contains(requiredText, apiSpec, StringComparison.Ordinal);
        }

        foreach (var requiredText in new[]
        {
            "QTE Practice Mode",
            "client-owned practice",
            "no rewards",
            "no GM-authored practice scenes",
            "does not mutate campaign state"
        })
        {
            Assert.Contains(requiredText, qteExample, StringComparison.Ordinal);
            Assert.Contains(requiredText, stepGuide, StringComparison.Ordinal);
        }
    }

    [Fact]
    public void PatternMemoryQteContract_IsDocumentedForGmAndPlayers()
    {
        var qteRules = ReadRepoFile("Rules", "Block_CLI_QTE.txt");
        var qteExample = ReadRepoFile("Examples", "E_CLI_QTE_Offer.txt");
        var apiSpec = ReadRepoFile("CLI_API_Specification.md");

        foreach (var requiredText in new[]
        {
            "PatternMemory",
            "check.config.alphabet",
            "sequenceLength",
            "revealMs",
            "inputTimeoutMs",
            "allowedMistakes",
            "фаза показа",
            "фаза ввода",
            "Browser clients support PatternMemory through #918 mini-games"
        })
        {
            Assert.Contains(requiredText, qteRules, StringComparison.Ordinal);
            Assert.Contains(requiredText, apiSpec, StringComparison.Ordinal);
        }

        foreach (var requiredText in new[]
        {
            "\"type\": \"PatternMemory\"",
            "\"alphabet\": [\"q\", \"w\", \"e\", \"space\"]",
            "\"sequenceLength\": 4",
            "\"revealMs\": 2500",
            "\"inputTimeoutMs\": 6000",
            "\"allowedMistakes\": 1",
            "\"terminalOutcomeId\": \"seal_open\"",
            "\"terminalOutcomeId\": \"seal_flickers\"",
            "\"terminalOutcomeId\": \"rune_alarm\""
        })
        {
            Assert.Contains(requiredText, qteExample, StringComparison.Ordinal);
        }
    }

    [Fact]
    public void RhythmPulseQteContract_IsDocumentedForGmAndPlayers()
    {
        var qteRules = ReadRepoFile("Rules", "Block_CLI_QTE.txt");
        var qteExample = ReadRepoFile("Examples", "E_CLI_QTE_Offer.txt");
        var apiSpec = ReadRepoFile("CLI_API_Specification.md");

        foreach (var requiredText in new[]
        {
            "RhythmPulse",
            "check.config.pulseCount",
            "beatIntervalMs",
            "hitWindowMs",
            "allowedMisses",
            "patternVariation",
            "visual/textual pulse timing",
            "Browser clients support RhythmPulse through #918 mini-games"
        })
        {
            Assert.Contains(requiredText, qteRules, StringComparison.Ordinal);
            Assert.Contains(requiredText, apiSpec, StringComparison.Ordinal);
        }

        foreach (var requiredText in new[]
        {
            "\"type\": \"RhythmPulse\"",
            "\"pulseCount\": 4",
            "\"beatIntervalMs\": 650",
            "\"hitWindowMs\": 120",
            "\"allowedMisses\": 1",
            "\"patternVariation\": \"steady\"",
            "\"terminalOutcomeId\": \"resonance_matched\"",
            "\"terminalOutcomeId\": \"resonance_wavers\"",
            "\"terminalOutcomeId\": \"resonance_breaks\""
        })
        {
            Assert.Contains(requiredText, qteExample, StringComparison.Ordinal);
        }
    }

    [Fact]
    public void PrecisionChoiceQteContract_IsDocumentedForGmAndPlayers()
    {
        var qteRules = ReadRepoFile("Rules", "Block_CLI_QTE.txt");
        var qteExample = ReadRepoFile("Examples", "E_CLI_QTE_Offer.txt");
        var apiSpec = ReadRepoFile("CLI_API_Specification.md");

        foreach (var requiredText in new[]
        {
            "PrecisionChoice",
            "check.config.choices",
            "correctChoiceId",
            "timeoutMs",
            "timeoutGrade",
            "decoyHints",
            "stable numbered choices",
            "Timeout resolves as fail by default and may resolve partial",
            "Browser clients support PrecisionChoice through #918 mini-games"
        })
        {
            Assert.Contains(requiredText, qteRules, StringComparison.Ordinal);
            Assert.Contains(requiredText, apiSpec, StringComparison.Ordinal);
        }

        foreach (var requiredText in new[]
        {
            "\"type\": \"PrecisionChoice\"",
            "\"correctChoiceId\": \"salt_wind\"",
            "\"timeoutMs\": 6000",
            "\"timeoutGrade\": \"fail\"",
            "\"decoyHints\"",
            "\"grade\": \"success\"",
            "\"grade\": \"partial\"",
            "\"grade\": \"fail\"",
            "\"terminalOutcomeId\": \"chase_escape\"",
            "\"terminalOutcomeId\": \"chase_scraped\"",
            "\"terminalOutcomeId\": \"chase_caught\""
        })
        {
            Assert.Contains(requiredText, qteExample, StringComparison.Ordinal);
        }
    }

    [Fact]
    public void StealthNoiseQteContract_IsDocumentedForGmAndPlayers()
    {
        var qteRules = ReadRepoFile("Rules", "Block_CLI_QTE.txt");
        var qteExample = ReadRepoFile("Examples", "E_CLI_QTE_Offer.txt");
        var apiSpec = ReadRepoFile("CLI_API_Specification.md");

        foreach (var requiredText in new[]
        {
            "StealthNoise",
            "check.config.durationMs",
            "startingNoise",
            "dangerThreshold",
            "noiseDriftPerSecond",
            "recoveryPerInput",
            "allowedOverThresholdMs",
            "gradeThresholds",
            "current noise",
            "danger threshold",
            "Browser clients support StealthNoise through #918 mini-games"
        })
        {
            Assert.Contains(requiredText, qteRules, StringComparison.Ordinal);
            Assert.Contains(requiredText, apiSpec, StringComparison.Ordinal);
        }

        foreach (var requiredText in new[]
        {
            "\"type\": \"StealthNoise\"",
            "\"durationMs\": 8000",
            "\"startingNoise\": 18",
            "\"dangerThreshold\": 55",
            "\"noiseDriftPerSecond\": 9",
            "\"recoveryPerInput\": 12",
            "\"allowedOverThresholdMs\": 900",
            "\"gradeThresholds\"",
            "\"terminalOutcomeId\": \"silent_passage\"",
            "\"terminalOutcomeId\": \"guard_stirs\"",
            "\"terminalOutcomeId\": \"alarm_raised\""
        })
        {
            Assert.Contains(requiredText, qteExample, StringComparison.Ordinal);
        }
    }

    [Fact]
    public void LockPinSetQteContract_IsDocumentedForGmAndPlayers()
    {
        var qteRules = ReadRepoFile("Rules", "Block_CLI_QTE.txt");
        var qteExample = ReadRepoFile("Examples", "E_CLI_QTE_Offer.txt");
        var apiSpec = ReadRepoFile("CLI_API_Specification.md");

        foreach (var requiredText in new[]
        {
            "LockPinSet",
            "check.config.pinCount",
            "pinWindows",
            "timerMs",
            "pickDurability",
            "maxMistakes",
            "pinDriftPerSecond",
            "gradeThresholds",
            "each pin state",
            "Shift+",
            "must be distinct",
            "Browser clients support LockPinSet through #918 mini-games"
        })
        {
            Assert.Contains(requiredText, qteRules, StringComparison.Ordinal);
            Assert.Contains(requiredText, apiSpec, StringComparison.Ordinal);
        }

        foreach (var requiredText in new[]
        {
            "\"type\": \"LockPinSet\"",
            "\"pinCount\": 4",
            "\"pinWindows\"",
            "\"timerMs\": 14000",
            "\"pickDurability\": 5",
            "\"maxMistakes\": 2",
            "\"pinDriftPerSecond\": 3",
            "\"gradeThresholds\"",
            "\"terminalOutcomeId\": \"archive_open_silently\"",
            "\"terminalOutcomeId\": \"archive_open_noisy\"",
            "\"terminalOutcomeId\": \"lockpick_alarm\""
        })
        {
            Assert.Contains(requiredText, qteExample, StringComparison.Ordinal);
        }
    }

    [Fact]
    public void QuestRewardAuthorityContract_IsDocumentedForGm()
    {
        var stepGuide = ReadRepoFile("TaskGuides", "CLI_Step_Main.txt");
        var operations = ReadRepoFile("Rules", "Block_CLI_Operations.txt");
        var example = ReadRepoFile("Examples", "E_CLI_Quest_Reward_Authority.txt");

        foreach (var requiredText in new[]
        {
            "questRewards",
            "itemsReceived",
            "skillsUnlocked",
            "relationshipChanges",
            "authorityStatus",
            "HistoricalOnly",
            "Unavailable",
            "reason",
            "current inventory/skills/NPC relationship authority",
            "bare strings are allowed only when they resolve"
        })
        {
            Assert.Contains(requiredText, stepGuide, StringComparison.Ordinal);
            Assert.Contains(requiredText, operations, StringComparison.Ordinal);
        }

        foreach (var requiredText in new[]
        {
            "QuestRewardAuthority_Example",
            "\"questRewards\"",
            "\"itemId\": \"item_merchant_seal\"",
            "\"skillName\": \"Продвинутая торговля\"",
            "\"npcId\": \"npc_guild_master\"",
            "\"authorityStatus\": \"HistoricalOnly\"",
            "\"reason\": \"Перстень остался в прошлой инкарнации.\""
        })
        {
            Assert.Contains(requiredText, example, StringComparison.Ordinal);
        }
    }

    [Fact]
    public void AcceptedTurnProseStateDeltaContract_IsDocumentedForGmAndLiveTests()
    {
        var stepGuide = ReadRepoFile("TaskGuides", "CLI_Step_Main.txt");
        var launchScript = ReadRepoFile("BookOfEternityClient", "Launcher", "CLI_Launch_Script.md");
        var launchGenerator = ReadRepoFile("BookOfEternityClient", "Launcher", "Generate_CLI_Launch_Script.ps1");
        var example = ReadRepoFile("Examples", "E_CLI_Step_Main.txt");
        var liveRoutes = ReadRepoFile("docs", "e2e", "live-test-paths.md");

        foreach (var requiredText in new[]
        {
            "Prose State Delta Rationale",
            "accepted_turn_skill_claim_missing_state_delta",
            "accepted_turn_quest_clue_missing_state_delta",
            "skillMasteryChanges",
            "detailsLog",
            "no-progress rationale"
        })
        {
            Assert.Contains(requiredText, stepGuide, StringComparison.Ordinal);
            Assert.Contains(requiredText, launchScript, StringComparison.Ordinal);
            Assert.Contains(requiredText, launchGenerator, StringComparison.Ordinal);
            Assert.Contains(requiredText, example, StringComparison.Ordinal);
        }

        foreach (var requiredText in new[]
        {
            "trained skill use",
            "quest clue persistence",
            "accepted_turn_skill_claim_missing_state_delta",
            "accepted_turn_quest_clue_missing_state_delta"
        })
        {
            Assert.Contains(requiredText, liveRoutes, StringComparison.Ordinal);
        }
    }

    [Fact]
    public void MortalActorMaterializationContract_IsDocumentedForGm()
    {
        var daemon = ReadRepoFile("BookOfEternityClient", "game_master_daemon.ps1");
        var npcRules = ReadRepoFile("Rules", "Block_19.txt");
        var npcInventoryRules = ReadRepoFile("Rules", "Block_19.A.txt");
        var stepGuide = ReadRepoFile("TaskGuides", "CLI_Step_Main.txt");
        var example = ReadRepoFile("Examples", "E_CLI_Step_Main.txt");
        var manifest = ReadRepoFile("Examples", "example_validation_manifest.json");
        var npcValidation = ReadRepoFile(
            "BookOfEternityClient",
            "Services",
            "Validation",
            "ValidationService.NpcWorldAndMeta.cs");

        foreach (var text in new[] { daemon, example })
        {
            foreach (var requiredText in new[]
                     {
                         "Actor Materialization v1",
                         "materializationId",
                         "materializedAtTurn",
                         "actorType",
                         "actorId",
                         "capabilities",
                         "sections",
                         "populated",
                         "empty_by_design",
                         "in-world reason",
                         "physically present",
                         "exactly one non-empty location authority",
                         "currentLocationId",
                         "same-turn initialLocationId",
                         "display name, prose, occupation, or setting genre",
                         "archetype prose",
                         "item types",
                         "genre keywords",
                         "existing NPC",
                         "dedicated delta"
                     })
            {
                Assert.Contains(requiredText, text, StringComparison.OrdinalIgnoreCase);
            }
        }

        foreach (var text in new[] { daemon, example })
        {
            Assert.Contains("\"actorType\": \"mortal_npc\"", text, StringComparison.Ordinal);
            Assert.Contains("\"relationships\": { \"state\": \"populated\" }", text, StringComparison.Ordinal);
            Assert.Contains("\"inventory\": {", text, StringComparison.Ordinal);
            Assert.Contains("\"state\": \"empty_by_design\"", text, StringComparison.Ordinal);
            Assert.Contains("\"activeSkills\": []", text, StringComparison.Ordinal);
            Assert.Contains("\"passiveSkills\": []", text, StringComparison.Ordinal);
            Assert.Contains("\"inventory\": []", text, StringComparison.Ordinal);
            Assert.Contains("\"fateCards\": []", text, StringComparison.Ordinal);
            Assert.Contains("\"personalQuests\": []", text, StringComparison.Ordinal);
        }

        foreach (var requiredText in new[]
                 {
                     "Actor Materialization v1",
                     "materializationId",
                     "materializedAtTurn",
                     "\"actorType\": \"mortal_npc\"",
                     "\"state\": \"complete\"",
                     "canFight",
                     "canTeach",
                     "canTrade",
                     "ownsItems",
                     "skills",
                     "inventory",
                     "fateCards",
                     "personalQuests",
                     "relationships",
                     "core identity, personality, characteristics, goals, progression, location, and memory",
                     "at least one setting-defined numeric property",
                     "First materialization",
                     "Legacy promotion",
                     "Existing-actor update"
                 })
        {
            Assert.Contains(requiredText, npcRules, StringComparison.OrdinalIgnoreCase);
        }

        foreach (var requiredText in new[]
                 {
                     "Legacy promotion",
                     "unchanged inventory snapshot",
                     "semantically identical to the validated pre-turn inventory snapshot",
                     "NPCInventoryAdds",
                     "NPCInventoryUpdates",
                     "NPCInventoryRemovals"
                 })
        {
            Assert.Contains(requiredText, npcInventoryRules, StringComparison.OrdinalIgnoreCase);
        }

        foreach (var requiredText in new[]
                 {
                     "first-materialization envelope",
                     "legacy promotion",
                     "same-turn initialId must not collide with a validated pre-turn permanent NPCId",
                     "validated pre-turn inventory snapshot",
                     "NPCInventoryAdds",
                     "NPCInventoryUpdates",
                     "NPCInventoryRemovals"
                 })
        {
            Assert.Contains(requiredText, stepGuide, StringComparison.OrdinalIgnoreCase);
        }

        foreach (var text in new[] { daemon, npcRules, npcInventoryRules, example })
        {
            Assert.Contains(
                "same-turn initialId must not collide with a validated pre-turn permanent NPCId",
                text,
                StringComparison.OrdinalIgnoreCase);
        }

        Assert.Contains(
            "Restore the exact validated pre-turn inventory snapshot on this carrier",
            npcValidation,
            StringComparison.OrdinalIgnoreCase);
        Assert.Contains(
            "For an ordinary existing UpdateNPCs entry, remove the whole full-object resend",
            npcValidation,
            StringComparison.OrdinalIgnoreCase);
        Assert.Contains("validated pre-turn inventory snapshot", npcValidation, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("NPCInventoryAdds/Updates/Removals", npcValidation, StringComparison.Ordinal);

        Assert.Contains("mortal_actor_materialization_v1", manifest, StringComparison.Ordinal);
        Assert.Contains("E_CLI_Step_Main.txt", manifest, StringComparison.Ordinal);
        Assert.Contains("exactly one non-empty location authority", manifest, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("physically present", manifest, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("archetype prose", manifest, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("item types", manifest, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("genre keywords", manifest, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void MortalFactionMaterializationGuidance_CoversLifecycleCoreChangesAndDaemonRouting()
    {
        var factionRules = ReadRepoFile("Rules", "Block_21.txt");
        var factionExample = ReadRepoFile("Examples", "E_Block_21.txt");
        var stepGuide = ReadRepoFile("TaskGuides", "CLI_Step_Main.txt");
        var stepExample = ReadRepoFile("Examples", "E_CLI_Step_Main.txt");
        var apiSpec = ReadRepoFile("CLI_API_Specification.md");
        var daemonSpec = ReadRepoFile("CLI_Agent_Daemon_Specification.md");
        var operations = ReadRepoFile("Rules", "Block_CLI_Operations.txt");
        var daemon = ReadRepoFile("BookOfEternityClient", "game_master_daemon.ps1");

        foreach (var requiredText in new[]
                 {
                     "\"purpose\"",
                     "\"currentAgenda\"",
                     "\"principles\"",
                     "\"memory\"",
                     "\"governance\"",
                     "\"leadership\"",
                     "\"materialization\"",
                     "hasFormalHierarchy",
                     "usesFactionResources",
                     "maintainsRelations",
                     "runsProjects",
                     "holdsTerritoryOrInfluence",
                     "supportsPlayerMembership",
                     "usesCustomMechanics",
                     "hierarchy",
                     "resources",
                     "relations",
                     "projects",
                     "territoryAndInfluence",
                     "playerMembership",
                     "customStates",
                     "scribeChronicle",
                     "factionCoreChanges",
                     "purposeAndPrinciples",
                     "progressionAndPower",
                     "governanceAndLeadership"
                 })
        {
            Assert.Contains(requiredText, factionRules, StringComparison.Ordinal);
        }

        foreach (var example in new[] { factionExample, stepExample })
        {
            foreach (var contractId in new[]
                     {
                         "mortal_faction_materialization_populated_creation_v1",
                         "mortal_faction_materialization_seven_empty_creation_v1",
                         "mortal_faction_materialized_existing_state_v1",
                         "mortal_faction_core_changes_update_v1",
                         "mortal_faction_materialization_repair_v1"
                     })
            {
                Assert.Contains(contractId, example, StringComparison.Ordinal);
            }
        }

        foreach (var guidance in new[] { stepGuide, apiSpec, daemonSpec, operations })
        {
            Assert.Contains("Faction Materialization", guidance, StringComparison.Ordinal);
            Assert.Contains("factionCoreChanges", guidance, StringComparison.Ordinal);
            Assert.Contains("receipt-less", guidance, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("empty_by_design", guidance, StringComparison.Ordinal);
        }

        Assert.Contains(
            "faction creation or ordinary faction update",
            daemon,
            StringComparison.OrdinalIgnoreCase);
        Assert.Contains("$script:CompactMortalFactionTemplatePath", daemon, StringComparison.Ordinal);
        Assert.Contains("Faction Materialization", daemon, StringComparison.Ordinal);
        Assert.Contains(
            "use '$($script:ExampleMainPath)' only when compact templates do not cover a route-specific shape",
            daemon,
            StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void CompactMortalFactionTemplate_UsesExecutableMaterializationAndCoreChangeCarriers()
    {
        var daemon = ReadRepoFile("BookOfEternityClient", "game_master_daemon.ps1");
        const string templateMarker =
            "-RelativePath \"Templates\\MORTAL_FACTION_UPDATE_TEMPLATE.md\"";
        var markerIndex = daemon.IndexOf(templateMarker, StringComparison.Ordinal);
        Assert.True(markerIndex >= 0, "Compact Mortal faction template marker is missing.");
        var contentStart = daemon.IndexOf("-Content @'", markerIndex, StringComparison.Ordinal);
        Assert.True(contentStart >= 0, "Compact Mortal faction template content is missing.");
        contentStart += "-Content @'".Length;
        var contentEnd = daemon.IndexOf("\n'@", contentStart, StringComparison.Ordinal);
        Assert.True(contentEnd > contentStart, "Compact Mortal faction template terminator is missing.");
        var template = daemon[contentStart..contentEnd];

        foreach (var requiredText in new[]
                 {
                     "\"factionDataChanges\"",
                     "\"factionId\": null",
                     "\"initialId\"",
                     "\"isNewFaction\": true",
                     "\"purpose\"",
                     "\"currentAgenda\"",
                     "\"memory\"",
                     "\"governance\"",
                     "\"leadership\"",
                     "\"materialization\"",
                     "\"ranks\"",
                     "\"branches\"",
                     "\"metaResources\"",
                     "\"strategicGoods\"",
                     "\"activeProjects\"",
                     "\"completedProjects\"",
                     "\"controlledTerritories\"",
                     "\"isPlayerFaction\"",
                     "\"isPlayerMember\"",
                     "\"playerRank\"",
                     "\"playerBranch\"",
                     "\"playerStrategyDirective\"",
                     "\"factionCoreChanges\"",
                     "\"purposeAndPrinciples\"",
                     "\"progressionAndPower\"",
                     "\"governanceAndLeadership\"",
                     "\"factionRankChanges\"",
                     "\"factionResourceChanges\"",
                     "\"factionProjectUpdates\"",
                     "\"factionChronicleUpdates\"",
                     "targetFiles",
                     "game_state/world/current_location.json",
                     "game_state/world/world_map.json"
                 })
        {
            Assert.Contains(requiredText, template, StringComparison.Ordinal);
        }

        Assert.DoesNotContain("Minimal durable faction object", template, StringComparison.Ordinal);
        Assert.DoesNotContain("Add a complete `factions[]` object", template, StringComparison.Ordinal);
        Assert.DoesNotContain("\"rankBranches\"", template, StringComparison.Ordinal);
        Assert.DoesNotContain("\"projects\": []", template, StringComparison.Ordinal);
        Assert.DoesNotContain("\"wealth\"", template, StringComparison.Ordinal);
    }

    [Fact]
    public void MortalNpcCoreChangesContract_IsDocumentedAcrossGmAndCliSurfaces()
    {
        var daemon = ReadRepoFile("BookOfEternityClient", "game_master_daemon.ps1");
        var responseRules = ReadRepoFile("Rules", "Block_2.txt");
        var updateRules = ReadRepoFile("Rules", "Block_2.5.txt");
        var npcRules = ReadRepoFile("Rules", "Block_19.txt");
        var npcWorldRules = ReadRepoFile("Rules", "Block_19.D.txt");
        var stepGuide = ReadRepoFile("TaskGuides", "CLI_Step_Main.txt");
        var daemonSpec = ReadRepoFile("CLI_Agent_Daemon_Specification.md");
        var cliMapping = ReadRepoFile("Examples", "CLI_Translation_Guide.md");
        var example = ReadRepoFile("Examples", "E_CLI_Step_Main.txt");

        foreach (var text in new[] { daemon, responseRules, npcRules, example })
        {
            foreach (var requiredText in new[]
                     {
                         "NPCCoreChanges",
                         "NPCId",
                         "reason",
                         "profile",
                         "location",
                         "progression",
                         "characteristicValues",
                         "factionAffiliationsToUpsert",
                         "fateCardsToAdd",
                         "fateCardIdsToRemove"
                     })
            {
                Assert.Contains(requiredText, text, StringComparison.Ordinal);
            }
        }

        Assert.Contains("absolute resulting values", npcRules, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("non-carrier", responseRules, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("NPCCoreChanges", updateRules, StringComparison.Ordinal);
        Assert.Contains("NPCCoreChanges", npcWorldRules, StringComparison.Ordinal);
        Assert.Contains("NPCCoreChanges", stepGuide, StringComparison.Ordinal);
        Assert.Contains("NPCCoreChanges", daemonSpec, StringComparison.Ordinal);
        Assert.Contains("\"NPCCoreChanges\": [...]", cliMapping, StringComparison.Ordinal);
        Assert.Contains("game_state/npcs/npc_core.json", cliMapping, StringComparison.Ordinal);
        Assert.Contains("ordinary existing", example, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("retained scene state", example, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("every actor-owned field", npcRules, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("full production Fate Card", npcRules, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("skillId is not required", npcRules, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("every actor-owned field", cliMapping, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void MortalActorMaterializationWorkedExample_PassesProductionNpcContract()
    {
        var snippet = Assert.Single(ExampleSnippetExtractor.ExtractAll(), candidate =>
            string.Equals(candidate.File, "E_CLI_Step_Main.txt", StringComparison.OrdinalIgnoreCase) &&
            candidate.RawText.Contains("mat_npc_orbital_xenobiologist_turn_4", StringComparison.Ordinal));
        using var document = JsonDocument.Parse(snippet.RawText);
        var npc = document.RootElement.GetProperty("NPCsInScene")[0];
        var validationRoot = JsonNode.Parse(snippet.RawText)!.AsObject();
        validationRoot["response"] = "Лиана завершает осмотр образца и фиксирует наблюдения.";
        using var validationDocument = JsonDocument.Parse(validationRoot.ToJsonString());

        var tempRoot = Path.Combine(
            Path.GetTempPath(),
            "boe-mortal-example-validation-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempRoot);
        try
        {
            var fs = new FileSystemManager(tempRoot, NullLogger<FileSystemManager>.Instance);
            fs.EnsureDirectoryStructure();
            var validator = new ValidationService(fs, NullLogger<ValidationService>.Instance);
            var issues = validator.ValidateResponse(validationDocument.RootElement);

            Assert.Empty(issues);
        }
        finally
        {
            Directory.Delete(tempRoot, recursive: true);
        }

        var personalityTraits = npc.GetProperty("personalityTraits").EnumerateArray().ToArray();
        Assert.InRange(personalityTraits.Length, 3, 5);
        Assert.All(personalityTraits, trait =>
        {
            Assert.True(trait.TryGetProperty("value", out var value));
            Assert.Equal(JsonValueKind.Number, value.ValueKind);
            Assert.True(value.TryGetInt32(out var integerValue));
            Assert.InRange(integerValue, 1, 10);
        });

        var characteristicKeys = npc.GetProperty("characteristics")
            .EnumerateObject()
            .Select(property => property.Name)
            .ToArray();
        Assert.Contains("xenobiology", characteristicKeys);
        Assert.Contains("biosafety_discipline", characteristicKeys);
        foreach (var universalKey in new[]
                 {
                     "strength", "dexterity", "constitution", "intelligence", "wisdom", "faith",
                     "attractiveness", "trade", "persuasion", "perception", "luck", "speed"
                 })
        {
            Assert.DoesNotContain(universalKey, characteristicKeys);
        }
    }

    [Fact]
    public void MortalInventoryRepairGuidance_DistinguishesLifecycleCasesWithoutPromotionRemoval()
    {
        var packetSource = ReadRepoFile(
            "BookOfEternityClient",
            "Core",
            "GameEngine",
            "GameEngine.ValidationAndRepair.cs");
        var example = ReadRepoFile("Examples", "E_CLI_Step_Main.txt");

        foreach (var text in new[] { packetSource, example })
        {
            Assert.DoesNotContain(
                "preserve any other validated update fields",
                text,
                StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain(
                "remove only the forbidden inventory",
                text,
                StringComparison.OrdinalIgnoreCase);
            Assert.Contains("genuinely new NPC", text, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("ordinary existing NPC", text, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("true legacy promotion", text, StringComparison.OrdinalIgnoreCase);
            Assert.Contains(
                "remove the whole ordinary-existing full-object resend",
                text,
                StringComparison.OrdinalIgnoreCase);
            Assert.Contains(
                "skill, inventory, relationship, journal, activity, equipment/resource",
                text,
                StringComparison.OrdinalIgnoreCase);
            Assert.Contains("main-GM rollback/repair path", text, StringComparison.OrdinalIgnoreCase);
            Assert.Contains(
                "exact semantically unchanged validated pre-turn inventory snapshot",
                text,
                StringComparison.OrdinalIgnoreCase);
            Assert.Contains("NPCInventoryAdds", text, StringComparison.Ordinal);
            Assert.Contains("NPCInventoryUpdates", text, StringComparison.Ordinal);
            Assert.Contains("NPCInventoryRemovals", text, StringComparison.Ordinal);
        }

        Assert.DoesNotContain(
            "Remove inventory from UpdateNPCs for every existing NPC",
            packetSource,
            StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(
            "Do not keep inventory: []",
            packetSource,
            StringComparison.OrdinalIgnoreCase);
        Assert.Contains(
            "Do not remove the schema-required inventory from a full true legacy promotion",
            packetSource,
            StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void MortalItemMaterializationGuidance_CoversRoutesLifecycleAndClientOwnership()
    {
        var block2 = ReadRepoFile("Rules", "Block_2.txt");
        var block5 = ReadRepoFile("Rules", "Block_5.txt");
        var block9 = ReadRepoFile("Rules", "Block_9.txt");
        var block10 = ReadRepoFile("Rules", "Block_10.txt");
        var block11 = ReadRepoFile("Rules", "Block_11.txt");
        var block19A = ReadRepoFile("Rules", "Block_19.A.txt");
        var block20 = ReadRepoFile("Rules", "Block_20.txt");
        var block10Example = ReadRepoFile("Examples", "E_Block_10.txt");
        var operations = ReadRepoFile("Rules", "Block_CLI_Operations.txt");
        var api = ReadRepoFile("CLI_API_Specification.md");
        var daemonSpec = ReadRepoFile("CLI_Agent_Daemon_Specification.md");
        var stepGuide = ReadRepoFile("TaskGuides", "CLI_Step_Main.txt");
        var stepExample = ReadRepoFile("Examples", "E_CLI_Step_Main.txt");
        var workedExample = ReadRepoFile("Examples", "E_CLI_Mortal_Item_Materialization.txt");
        var daemon = ReadRepoFile("BookOfEternityClient", "game_master_daemon.ps1");

        var rulesCorpus = string.Join('\n',
            block2, block5, block9, block10, block11, block19A, block20);
        var gmLifecycleCorpus = string.Join('\n',
            operations, api, daemonSpec, stepGuide, stepExample, workedExample);

        foreach (var route in new[]
                 {
                     "player_acquisition",
                     "npc_acquisition",
                     "new_npc_inventory",
                     "loot_acquisition",
                     "craft_output",
                     "trade_output",
                     "quest_reward",
                     "storage_placement"
                 })
        {
            Assert.Contains(route, rulesCorpus, StringComparison.Ordinal);
            Assert.Contains(route, gmLifecycleCorpus, StringComparison.Ordinal);
        }

        foreach (var guidance in new[] { operations, api, daemonSpec, stepGuide })
        {
            Assert.Contains("Mortal Item Materialization v1", guidance, StringComparison.Ordinal);
            Assert.Contains("не создавай itemId", guidance, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("не создавай materializationReceipt", guidance, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("item_identity_index.json", guidance, StringComparison.Ordinal);
            Assert.Contains("receipt-less", guidance, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("игра ещё не вышла", guidance, StringComparison.OrdinalIgnoreCase);
        }

        foreach (var bootstrapGuidance in new[] { operations, api, daemonSpec, stepGuide, stepExample })
        {
            Assert.Contains(
                "Previous-life item sidecars are rollback-only and are not the current GM baseline.",
                bootstrapGuidance,
                StringComparison.Ordinal);
            Assert.Contains("game_state/inventory/item_resources.json", bootstrapGuidance, StringComparison.Ordinal);
            Assert.Contains("game_state/inventory/item_bonds.json", bootstrapGuidance, StringComparison.Ordinal);
            Assert.Contains("game_state/inventory/item_text_updates.json", bootstrapGuidance, StringComparison.Ordinal);
            Assert.Contains("game_state/npcs/item_journals.json", bootstrapGuidance, StringComparison.Ordinal);
        }

        foreach (var requiredText in new[]
                 {
                     "existedId = null",
                     "creationRef",
                     "materialization.sections",
                     "exact itemId",
                     "contentsPath",
                     "split",
                     "merge",
                     "destroyed"
                 })
        {
            Assert.Contains(requiredText, gmLifecycleCorpus, StringComparison.OrdinalIgnoreCase);
        }

        Assert.Contains("craft_output", block9, StringComparison.Ordinal);
        Assert.Contains("exact itemId", block11, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("permanent parent item IDs", block10, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("container names, exact from Context", block10, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(
            "\"contentsPath\": [\"First Aid Kit\"]",
            block10Example,
            StringComparison.Ordinal);
        foreach (var placementGuidance in new[] { block10, stepGuide, workedExample })
        {
            Assert.Contains("isCarried", placementGuidance, StringComparison.Ordinal);
            Assert.Contains("not placement authority", placementGuidance, StringComparison.OrdinalIgnoreCase);
        }
        Assert.Contains("npc_acquisition", block19A, StringComparison.Ordinal);
        Assert.Contains("trade_output", block19A, StringComparison.Ordinal);
        Assert.Contains("storage_placement", block20, StringComparison.Ordinal);
        Assert.Contains("loot_acquisition", block5, StringComparison.Ordinal);
        Assert.Contains("quest_reward", block2, StringComparison.Ordinal);

        foreach (var contractId in new[]
                 {
                     "mortal_item_player_acquisition_empty_sections_v1",
                     "mortal_item_mechanic_trade_output_v1",
                     "mortal_item_existing_transfer_v1",
                     "mortal_item_split_merge_lineage_v1",
                     "mortal_item_receiptless_rejection_v1"
                 })
        {
            Assert.Contains(contractId, workedExample, StringComparison.Ordinal);
        }

        Assert.DoesNotContain("legacy promotion", workedExample, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("$script:MortalItemMaterializationDirective", daemon, StringComparison.Ordinal);
        Assert.Contains("$script:CompactMortalItemTemplatePath", daemon, StringComparison.Ordinal);
        Assert.Contains("Mortal Item Materialization v1", daemon, StringComparison.Ordinal);
    }

    [Fact]
    public void MortalLocationMaterializationGuidance_CoversExactRoutesTopologyBootstrapAndRepair()
    {
        var responseTemplate = ReadRepoFile("Rules", "Block_2.txt");
        var rules = ReadRepoFile("Rules", "Block_20.txt");
        var examples = ReadRepoFile("Examples", "E_Block_20.txt");
        var corpus = rules + '\n' + examples;

        foreach (var requiredText in new[]
                 {
                     "Mortal Location Materialization v1",
                     "current_scene_creation",
                     "world_map_creation",
                     "world_map_link_creation",
                     "materialization.sections",
                     "locationId = null",
                     "linkId = null",
                     "exact permanent locationId",
                     "exact permanent linkId",
                     "locationDiscoveryTransitions",
                     "linkRemovals",
                     "storageUpdates",
                     "storagesToRemove",
                     "threatsToAdd",
                     "threatsToUpdate",
                     "threatsToRemove",
                     "completeThreatActivities",
                     "open directed link",
                     "Existing movement carries only exact selection and operational fields",
                     "never include locationStorages or contents",
                     "client preserves storage item contents",
                     "immutable creation evidence",
                     "knownExits and adjacencyMap are client-derived",
                     "location_identity_index.json",
                     "mortal_bootstrap_scaffold.json",
                     "narrative-only unresolved exit",
                     "mortal_location_materialization_repair",
                     "full-turn resubmission",
                     "never author materializationReceipt",
                     "never infer identity from a display name",
                     "case-sensitive and Unicode-exact"
                 })
        {
            Assert.Contains(requiredText, corpus, StringComparison.OrdinalIgnoreCase);
        }

        foreach (var contractId in new[]
                 {
                     "mortal_location_bootstrap_start_neighbor_link_v1",
                     "mortal_location_existing_movement_storage_continuity_v1",
                     "mortal_location_hidden_remote_reveal_v1",
                     "mortal_location_governed_storage_threat_lifecycle_v1",
                     "mortal_location_bounded_repair_v1"
                 })
        {
            Assert.Contains(contractId, examples, StringComparison.Ordinal);
        }

        const string movementExampleId =
            "## mortal_location_existing_movement_storage_continuity_v1";
        var movementStart = examples.IndexOf(movementExampleId, StringComparison.Ordinal);
        Assert.True(movementStart >= 0, "Existing-movement worked example is missing.");
        var movementEnd = examples.IndexOf("\n## ", movementStart + movementExampleId.Length,
            StringComparison.Ordinal);
        var movementExample = movementEnd < 0
            ? examples[movementStart..]
            : examples[movementStart..movementEnd];
        Assert.DoesNotContain("\"locationStorages\"", movementExample, StringComparison.Ordinal);
        Assert.DoesNotContain("\"contents\"", movementExample, StringComparison.Ordinal);
        Assert.DoesNotContain("location_storage_contents", corpus, StringComparison.OrdinalIgnoreCase);

        foreach (var retiredText in new[]
                 {
                     "linksToRemove",
                     "targetCoordinates",
                     "internalDifficultyProfile",
                     "externalDifficultyProfile",
                     "system_assigned_guid",
                     "Backward Compatibility for Legacy Coordinates"
                 })
        {
            Assert.DoesNotContain(retiredText, rules, StringComparison.OrdinalIgnoreCase);
        }

        foreach (var requiredTemplateText in new[]
                 {
                     "locationDiscoveryTransitions",
                     "linkRemovals",
                     "exact permanent linkId",
                     "initialTargetLocationId",
                     "newCapacity",
                     "newOwner",
                     "Never include locationStorages or contents in an existing-movement payload",
                     "currentChronology"
                 })
        {
            Assert.Contains(requiredTemplateText, responseTemplate, StringComparison.OrdinalIgnoreCase);
        }

        foreach (var retiredTemplateText in new[]
                 {
                     "linksToRemove",
                     "targetCoordinates",
                     "newInternalDifficultyProfile",
                     "newExternalDifficultyProfile",
                     "newLastEventsDescription",
                     "internalDifficultyProfile",
                     "externalDifficultyProfile",
                     "adjacencyMap"
                 })
        {
            Assert.DoesNotContain(retiredTemplateText, responseTemplate, StringComparison.OrdinalIgnoreCase);
        }
    }

    [Fact]
    public void MortalLocationCliAndDaemonGuidance_UsesCurrentSchemaAndFullTurnRepair()
    {
        var api = ReadRepoFile("CLI_API_Specification.md");
        var daemonSpecification = ReadRepoFile("CLI_Agent_Daemon_Specification.md");
        var taskGuide = ReadRepoFile("TaskGuides", "CLI_Step_Main.txt");
        var workedExample = ReadRepoFile("Examples", "E_CLI_Step_Main.txt");
        var cliOperations = ReadRepoFile("Rules", "Block_CLI_Operations.txt");
        var daemon = ReadRepoFile("BookOfEternityClient", "game_master_daemon.ps1");

        foreach (var document in new[]
                 {
                     api,
                     daemonSpecification,
                     taskGuide,
                     workedExample,
                     cliOperations
                 })
        {
            Assert.Contains(
                "Mortal Location Materialization v1",
                document,
                StringComparison.OrdinalIgnoreCase);
        }

        var documentationCorpus = string.Join(
            '\n',
            api,
            daemonSpecification,
            taskGuide,
            workedExample,
            cliOperations);
        foreach (var requiredText in new[]
                 {
                     "current_scene_creation",
                     "world_map_creation",
                     "world_map_link_creation",
                     "locationDiscoveryTransitions",
                     "linkRemovals",
                     "storageUpdates",
                     "storagesToRemove",
                     "threatsToAdd",
                     "threatsToUpdate",
                     "threatsToRemove",
                     "completeThreatActivities",
                     "open directed",
                     "creation evidence",
                     "exact permanent locationId",
                     "exact permanent linkId",
                     "location_identity_index.json",
                     "knownExits and adjacencyMap are client-derived",
                     "mortal_bootstrap_scaffold.json",
                     "mortal_location_materialization_repair",
                     "full-turn resubmission",
                     "validated pre-turn baseline"
                 })
        {
            Assert.Contains(requiredText, documentationCorpus, StringComparison.OrdinalIgnoreCase);
        }

        Assert.DoesNotContain(
            "known-location shorthand with locationId + coordinates",
            api,
            StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("linksToRemove", api, StringComparison.OrdinalIgnoreCase);

        const string templateMarker =
            "-RelativePath \"Templates\\MORTAL_LOCATION_TRANSITION_TEMPLATE.md\"";
        var markerIndex = daemon.IndexOf(templateMarker, StringComparison.Ordinal);
        Assert.True(markerIndex >= 0, "Compact Mortal location template marker is missing.");
        var contentStart = daemon.IndexOf("-Content @'", markerIndex, StringComparison.Ordinal);
        Assert.True(contentStart >= 0, "Compact Mortal location template content is missing.");
        contentStart += "-Content @'".Length;
        var contentEnd = daemon.IndexOf("\n'@", contentStart, StringComparison.Ordinal);
        Assert.True(contentEnd > contentStart, "Compact Mortal location template terminator is missing.");
        var template = daemon[contentStart..contentEnd];

        foreach (var requiredText in new[]
                 {
                     "Mortal Location Materialization v1",
                     "current_scene_creation",
                     "world_map_creation",
                     "world_map_link_creation",
                     "materialization.sections",
                     "locationId = null",
                     "linkId = null",
                     "exact permanent locationId",
                     "exact permanent linkId",
                     "locationDiscoveryTransitions",
                     "linkRemovals",
                     "storageUpdates",
                     "storagesToRemove",
                     "threatsToAdd",
                     "threatsToUpdate",
                     "threatsToRemove",
                     "completeThreatActivities",
                     "open directed",
                     "creation evidence",
                     "knownExits and adjacencyMap are client-derived",
                     "mortal_bootstrap_scaffold.json",
                     "full-turn resubmission"
                 })
        {
            Assert.Contains(requiredText, template, StringComparison.OrdinalIgnoreCase);
        }

        foreach (var retiredText in new[]
                 {
                     "targetCoordinates",
                     "internalDifficultyProfile",
                     "externalDifficultyProfile",
                     "estimatedInternalDifficultyProfile",
                     "estimatedExternalDifficultyProfile"
                 })
        {
            Assert.DoesNotContain(retiredText, template, StringComparison.OrdinalIgnoreCase);
        }

        Assert.Contains("$hasMortalLocationMaterializationRepair", daemon, StringComparison.Ordinal);
        Assert.Contains("$mortalLocationRepairDirective", daemon, StringComparison.Ordinal);
        Assert.Contains("validated pre-turn baseline", daemon, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("full-turn resubmission", daemon, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("storageUpdates", daemon, StringComparison.Ordinal);
        Assert.Contains("completeThreatActivities", daemon, StringComparison.Ordinal);
        Assert.Contains("open directed", daemon, StringComparison.OrdinalIgnoreCase);
        Assert.Contains(
            "Never echo `locationStorages` or `contents` during",
            api,
            StringComparison.OrdinalIgnoreCase);

        var npcRules = ReadRepoFile("Rules", "Block_19.txt");
        Assert.Contains("locref_turn_42_dwarven_forge", npcRules, StringComparison.Ordinal);
        Assert.DoesNotContain("temp-loc-[description]", npcRules, StringComparison.OrdinalIgnoreCase);

        var factionRules = ReadRepoFile("Rules", "Block_21.txt");
        Assert.Contains("current-schema `internalDifficulty`", factionRules, StringComparison.Ordinal);
        Assert.Contains("`externalDifficulty` object", factionRules, StringComparison.Ordinal);
        Assert.DoesNotContain("internalDifficultyProfile", factionRules, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("externalDifficultyProfile", factionRules, StringComparison.OrdinalIgnoreCase);

        var storageExamples = ReadRepoFile("Examples", "E_Block_11.B.txt");
        Assert.DoesNotContain(
            "\"locationId\": \"loc-squad-hq-01\",\n                            \"coordinates\"",
            storageExamples,
            StringComparison.Ordinal);

        var launcher = ReadRepoFile(
            "BookOfEternityClient",
            "Launcher",
            "CLI_Launch_Script.md");
        var launchGenerator = ReadRepoFile(
            "BookOfEternityClient",
            "Launcher",
            "Generate_CLI_Launch_Script.ps1");
        var translation = ReadRepoFile("Examples", "CLI_Translation_Guide.md");
        foreach (var surface in new[] { launcher, launchGenerator, translation })
        {
            Assert.Contains("Mortal Location Materialization v1", surface, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("full-turn resubmission", surface, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("storageUpdates", surface, StringComparison.Ordinal);
            Assert.Contains("completeThreatActivities", surface, StringComparison.Ordinal);
            Assert.Contains("open directed", surface, StringComparison.OrdinalIgnoreCase);
        }

        Assert.DoesNotContain(
            "by currentLocationId/initialLocationId or canonical name",
            launcher,
            StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(
            "by currentLocationId/initialLocationId or canonical name",
            launchGenerator,
            StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void CompactMortalItemTemplate_CoversCompleteCreationAndProtectedLifecycle()
    {
        var daemon = ReadRepoFile("BookOfEternityClient", "game_master_daemon.ps1");
        const string templateMarker =
            "-RelativePath \"Templates\\MORTAL_ITEM_MATERIALIZATION_TEMPLATE.md\"";
        var markerIndex = daemon.IndexOf(templateMarker, StringComparison.Ordinal);
        Assert.True(markerIndex >= 0, "Compact Mortal item template marker is missing.");
        var contentStart = daemon.IndexOf("-Content @'", markerIndex, StringComparison.Ordinal);
        Assert.True(contentStart >= 0, "Compact Mortal item template content is missing.");
        contentStart += "-Content @'".Length;
        var contentEnd = daemon.IndexOf("\n'@", contentStart, StringComparison.Ordinal);
        Assert.True(contentEnd > contentStart, "Compact Mortal item template terminator is missing.");
        var template = daemon[contentStart..contentEnd];

        foreach (var requiredText in new[]
                 {
                     "player_acquisition",
                     "npc_acquisition",
                     "new_npc_inventory",
                     "loot_acquisition",
                     "craft_output",
                     "trade_output",
                     "quest_reward",
                     "storage_placement",
                     "existedId = null",
                     "creationRef",
                     "materialization.sections",
                     "presentation",
                     "ownershipAndPlacement",
                     "structuredBonuses",
                     "contentsPath",
                     "isCarried",
                     "not placement authority",
                     "item_identity_index.json",
                     "exact ordinal `itemId`",
                     "split",
                     "merge",
                     "destroyed",
                     "mortal_item_materialization_repair"
                 })
        {
            Assert.Contains(requiredText, template, StringComparison.OrdinalIgnoreCase);
        }

        Assert.Contains(
            "Examples\\E_CLI_Mortal_Item_Materialization.txt",
            daemon,
            StringComparison.Ordinal);
        Assert.Contains("compact_mortal_item_materialization_template", daemon, StringComparison.Ordinal);
        Assert.Contains("$script:MortalItemMaterializationDirective", daemon, StringComparison.Ordinal);
    }

    private static string ReadRepoFile(params string[] parts) =>
        File.ReadAllText(Path.Combine(new[] { TestRepoPaths.RepoRoot }.Concat(parts).ToArray()));
}
