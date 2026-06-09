using Xunit;

namespace BookOfEternityClient.Tests;

public sealed class PromptDocumentationCoverageTests
{
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
            "Browser interactive MashInput parity remains #918"
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
            "Browser interactive PatternMemory parity remains #918"
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
            "Browser interactive RhythmPulse parity remains #918"
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
            "Browser interactive PrecisionChoice parity remains #918"
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
            "Browser interactive StealthNoise parity remains #918"
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
            "\"dangerThreshold\": 70",
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
            "Browser interactive LockPinSet parity remains #918"
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

    private static string ReadRepoFile(params string[] parts) =>
        File.ReadAllText(Path.Combine(new[] { TestRepoPaths.RepoRoot }.Concat(parts).ToArray()));
}
