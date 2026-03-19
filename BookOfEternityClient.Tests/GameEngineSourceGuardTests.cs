using Xunit;

namespace BookOfEternityClient.Tests;

public sealed class GameEngineSourceGuardTests
{
    [Fact]
    public void NewGameFlow_MustCheckInitialWaitResultBeforeEnteringGameLoop()
    {
        var path = Path.Combine(TestRepoPaths.RepoRoot, "BookOfEternityClient", "Core", "GameEngine.cs");
        var source = File.ReadAllText(path);

        Assert.Contains("if (!await WaitForGmResponse())", source, StringComparison.Ordinal);
    }

    [Fact]
    public void CriticalAcceptedStateCorruption_MustNotBeRoutedAsTerminalProtocolFailure()
    {
        var path = Path.Combine(TestRepoPaths.RepoRoot, "BookOfEternityClient", "Core", "GameEngine.cs");
        var source = File.ReadAllText(path);

        Assert.DoesNotContain("critical state corruption after", source, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("после отклонения ready-сигнала")]
    [InlineData("после потери terminal outcome")]
    [InlineData("после конфликтующих terminal signals")]
    [InlineData("validation_repair_ready.json и переписал repair request")]
    public void PlayerFacingStatusMessages_MustNotExposeInternalProtocolTerms(string leakedPhrase)
    {
        var path = Path.Combine(TestRepoPaths.RepoRoot, "BookOfEternityClient", "Core", "GameEngine.cs");
        var source = File.ReadAllText(path);

        Assert.DoesNotContain(leakedPhrase, source, StringComparison.Ordinal);
    }

    [Fact]
    public void ManualRefreshAndResize_MustNormalizeStaleRepairArtifactsBeforeRevalidation()
    {
        var path = Path.Combine(TestRepoPaths.RepoRoot, "BookOfEternityClient", "Core", "GameEngine.cs");
        var source = File.ReadAllText(path);

        Assert.Contains("await NormalizeRuntimeUiArtifactsAsync();", source, StringComparison.Ordinal);
    }

    [Fact]
    public void RuntimeUiArtifactNormalizer_MustCleanOrphanTurnRequestAndReadySignalsWithoutManifest()
    {
        var path = Path.Combine(TestRepoPaths.RepoRoot, "BookOfEternityClient", "Core", "GameEngine.cs");
        var source = File.ReadAllText(path);

        Assert.Contains("Найдены ready-сигналы без pending snapshot manifest", source, StringComparison.Ordinal);
        Assert.Contains("Найден orphaned input/turn_request.json без pending snapshot manifest", source, StringComparison.Ordinal);
    }

    [Fact]
    public void ContinueFlow_MustNotDeleteAcceptedTurnOutputFilesJustBecauseThereIsNoPendingManifest()
    {
        var path = Path.Combine(TestRepoPaths.RepoRoot, "BookOfEternityClient", "Core", "GameEngine.cs");
        var source = File.ReadAllText(path);

        Assert.DoesNotContain("if (pendingManifest == null && !hasPendingTerminalSignal)\r\n            ClearTransientOutputFiles();", source, StringComparison.Ordinal);
    }

    [Fact]
    public void ManualRefreshAndLoadValidation_MustNotRequireAcceptedTurnPayloadArtifacts()
    {
        var path = Path.Combine(TestRepoPaths.RepoRoot, "BookOfEternityClient", "Core", "GameEngine.cs");
        var source = File.ReadAllText(path);

        Assert.Contains("RequiresAcceptedTurnPayloadValidation", source, StringComparison.Ordinal);
    }

    [Fact]
    public void SuccessfulAcceptedTurn_MustNotDeletePersistentLastResponseOutputs()
    {
        var path = Path.Combine(TestRepoPaths.RepoRoot, "BookOfEternityClient", "Core", "GameEngine.cs");
        var source = File.ReadAllText(path);

        Assert.DoesNotContain("_fs.DeleteFile(\"ready/turn_complete.json\");\r\n        ClearTransientOutputFiles();", source, StringComparison.Ordinal);
    }

    [Fact]
    public void ManualRefresh_MustMergeDiskResponseWithCurrentInMemoryLastResponse()
    {
        var path = Path.Combine(TestRepoPaths.RepoRoot, "BookOfEternityClient", "Core", "GameEngine.cs");
        var source = File.ReadAllText(path);

        Assert.Contains("var refreshedResponse = MergeWithLastResponse(await BuildGameResponseFromFiles());", source, StringComparison.Ordinal);
    }

    [Fact]
    public void PlayerInput_MustExposeClipboardPasteShortcut()
    {
        var path = Path.Combine(TestRepoPaths.RepoRoot, "BookOfEternityClient", "Core", "GameEngine.cs");
        var source = File.ReadAllText(path);

        Assert.Contains("\\p", source, StringComparison.Ordinal);
        Assert.Contains("ResolveClipboardPlayerInput()", source, StringComparison.Ordinal);
        Assert.Contains("TextComposer.Read", source, StringComparison.Ordinal);
    }

    [Fact]
    public void RuntimePrompt_MustRequireCurrentLocationInRelevantNpcReasoningBlocks()
    {
        var path = Path.Combine(TestRepoPaths.RepoRoot, "BookOfEternityClient", "Core", "GameEngine.cs");
        var source = File.ReadAllText(path);

        Assert.Contains("- Текущая локация / Current location", source, StringComparison.Ordinal);
        Assert.Contains("For EVERY relevant NPC block, the current-location line is mandatory", source, StringComparison.Ordinal);
    }

    [Fact]
    public void RuntimePrompt_AndSoulLifecycle_MustPreserveSoulRenameContinuity()
    {
        var path = Path.Combine(TestRepoPaths.RepoRoot, "BookOfEternityClient", "Core", "GameEngine.cs");
        var source = File.ReadAllText(path);

        Assert.Contains("previousSoulNames", source, StringComparison.Ordinal);
        Assert.Contains("If game_state/meta/soul_state.json contains previousSoulNames", source, StringComparison.Ordinal);
    }

    [Fact]
    public void RuntimePrompt_MustDescribeGuardianForcedIncarnationProtectionAndContract()
    {
        var path = Path.Combine(TestRepoPaths.RepoRoot, "BookOfEternityClient", "Core", "GameEngine.cs");
        var source = File.ReadAllText(path);

        Assert.Contains("afterlife_return_guard.json", source, StringComparison.Ordinal);
        Assert.Contains("Guardian-forced incarnation is legal only on an ordinary player-driven Chaos Sea turn", source, StringComparison.Ordinal);
        Assert.Contains("Do NOT immediately kick the soul back into a new life on that protected return turn", source, StringComparison.Ordinal);
        Assert.Contains("source = guardian_forced", source, StringComparison.Ordinal);
    }

    [Fact]
    public void GuardianForcedIncarnation_RuntimeGate_MustFailClosedOnInvalidReturnGuard()
    {
        var path = Path.Combine(TestRepoPaths.RepoRoot, "BookOfEternityClient", "Core", "GameEngine.cs");
        var source = File.ReadAllText(path);

        Assert.Contains("afterlife_return_guard is invalid. Failing closed", source, StringComparison.Ordinal);
    }

    [Fact]
    public void LifeEvaluationAndAcceptedAfterlifeTurns_MustActivateAndConsumeReturnProtection()
    {
        var path = Path.Combine(TestRepoPaths.RepoRoot, "BookOfEternityClient", "Core", "GameEngine.cs");
        var source = File.ReadAllText(path);

        Assert.Contains("ActivatePostLifeReturnAsync", source, StringComparison.Ordinal);
        Assert.Contains("ConsumeAfterlifeReturnProtectionIfNeededAsync", source, StringComparison.Ordinal);
        Assert.Contains("OrdinaryPlayerTurnSourceLabel", source, StringComparison.Ordinal);
    }

    [Fact]
    public void FreeformGameEngineTextInputs_MustNotUseTextPromptStringDirectly()
    {
        var path = Path.Combine(TestRepoPaths.RepoRoot, "BookOfEternityClient", "Core", "GameEngine.cs");
        var source = File.ReadAllText(path);

        Assert.DoesNotContain("TextPrompt<string>", source, StringComparison.Ordinal);
        Assert.Contains("PromptTextInput(", source, StringComparison.Ordinal);
    }

    [Fact]
    public void MultilinePlayerInput_MustUseUnifiedTextComposerInsteadOfLegacyPasteSentinel()
    {
        var path = Path.Combine(TestRepoPaths.RepoRoot, "BookOfEternityClient", "Core", "GameEngine.cs");
        var source = File.ReadAllText(path);

        Assert.DoesNotContain("::paste", source, StringComparison.Ordinal);
        Assert.Contains("Mode = TextComposerMode.MultilineEditor", source, StringComparison.Ordinal);
    }
}
