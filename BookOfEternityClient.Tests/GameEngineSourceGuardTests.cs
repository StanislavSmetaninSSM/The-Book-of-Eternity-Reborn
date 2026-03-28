using Xunit;

namespace BookOfEternityClient.Tests;

public sealed class GameEngineSourceGuardTests
{
    private static string ReadGameEngineSource()
    {
        var rootFile = Path.Combine(TestRepoPaths.RepoRoot, "BookOfEternityClient", "Core", "GameEngine.cs");
        var partialDir = Path.Combine(TestRepoPaths.RepoRoot, "BookOfEternityClient", "Core", "GameEngine");

        var files = new List<string> { rootFile };
        if (Directory.Exists(partialDir))
            files.AddRange(Directory.GetFiles(partialDir, "*.cs", SearchOption.TopDirectoryOnly).OrderBy(path => path, StringComparer.OrdinalIgnoreCase));

        return string.Join(Environment.NewLine + Environment.NewLine, files.Select(File.ReadAllText));
    }

    [Fact]
    public void NewGameFlow_MustCheckInitialWaitResultBeforeEnteringGameLoop()
    {
        var source = ReadGameEngineSource();

        Assert.Contains("if (!await WaitForGmResponse())", source, StringComparison.Ordinal);
    }

    [Fact]
    public void CriticalAcceptedStateCorruption_MustNotBeRoutedAsTerminalProtocolFailure()
    {
        var source = ReadGameEngineSource();

        Assert.DoesNotContain("critical state corruption after", source, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("после отклонения ready-сигнала")]
    [InlineData("после потери terminal outcome")]
    [InlineData("после конфликтующих terminal signals")]
    [InlineData("validation_repair_ready.json и переписал repair request")]
    public void PlayerFacingStatusMessages_MustNotExposeInternalProtocolTerms(string leakedPhrase)
    {
        var source = ReadGameEngineSource();

        Assert.DoesNotContain(leakedPhrase, source, StringComparison.Ordinal);
    }

    [Fact]
    public void ManualRefreshAndResize_MustNormalizeStaleRepairArtifactsBeforeRevalidation()
    {
        var source = ReadGameEngineSource();

        Assert.Contains("await NormalizeRuntimeUiArtifactsAsync();", source, StringComparison.Ordinal);
    }

    [Fact]
    public void RuntimeUiArtifactNormalizer_MustCleanOrphanTurnRequestAndReadySignalsWithoutManifest()
    {
        var source = ReadGameEngineSource();

        Assert.Contains("Найдены ready-сигналы без pending snapshot manifest", source, StringComparison.Ordinal);
        Assert.Contains("Найден orphaned input/turn_request.json без pending snapshot manifest", source, StringComparison.Ordinal);
    }

    [Fact]
    public void ContinueFlow_MustNotDeleteAcceptedTurnOutputFilesJustBecauseThereIsNoPendingManifest()
    {
        var source = ReadGameEngineSource();

        Assert.DoesNotContain("if (pendingManifest == null && !hasPendingTerminalSignal)\r\n            ClearTransientOutputFiles();", source, StringComparison.Ordinal);
    }

    [Fact]
    public void ManualRefreshAndLoadValidation_MustNotRequireAcceptedTurnPayloadArtifacts()
    {
        var source = ReadGameEngineSource();

        Assert.Contains("RequiresAcceptedTurnPayloadValidation", source, StringComparison.Ordinal);
    }

    [Fact]
    public void SuccessfulAcceptedTurn_MustNotDeletePersistentLastResponseOutputs()
    {
        var source = ReadGameEngineSource();

        Assert.DoesNotContain("_fs.DeleteFile(\"ready/turn_complete.json\");\r\n        ClearTransientOutputFiles();", source, StringComparison.Ordinal);
    }

    [Fact]
    public void ManualRefresh_MustMergeDiskResponseWithCurrentInMemoryLastResponse()
    {
        var source = ReadGameEngineSource();

        Assert.Contains("var refreshedResponse = MergeWithLastResponse(await BuildGameResponseFromFiles());", source, StringComparison.Ordinal);
    }

    [Fact]
    public void PlayerInput_MustExposeClipboardPasteShortcut()
    {
        var source = ReadGameEngineSource();

        Assert.Contains("\\p", source, StringComparison.Ordinal);
        Assert.Contains("ResolveClipboardPlayerInput()", source, StringComparison.Ordinal);
        Assert.Contains("TextComposer.Read", source, StringComparison.Ordinal);
    }

    [Fact]
    public void RuntimePrompt_MustRequireCurrentLocationInRelevantNpcReasoningBlocks()
    {
        var source = ReadGameEngineSource();

        Assert.Contains("- Текущая локация / Current location", source, StringComparison.Ordinal);
        Assert.Contains("For EVERY relevant NPC block, the current-location line is mandatory", source, StringComparison.Ordinal);
    }

    [Fact]
    public void RuntimePrompt_AndSoulLifecycle_MustPreserveSoulRenameContinuity()
    {
        var source = ReadGameEngineSource();

        Assert.Contains("previousSoulNames", source, StringComparison.Ordinal);
        Assert.Contains("If game_state/meta/soul_state.json contains previousSoulNames", source, StringComparison.Ordinal);
    }

    [Fact]
    public void RuntimePrompt_MustDescribeGuardianForcedIncarnationProtectionAndContract()
    {
        var source = ReadGameEngineSource();

        Assert.Contains("afterlife_return_guard.json", source, StringComparison.Ordinal);
        Assert.Contains("Guardian-forced incarnation is legal only on an ordinary player-driven Chaos Sea turn", source, StringComparison.Ordinal);
        Assert.Contains("Do NOT immediately kick the soul back into a new life on that protected return turn", source, StringComparison.Ordinal);
        Assert.Contains("source = guardian_forced", source, StringComparison.Ordinal);
    }

    [Fact]
    public void RuntimePrompt_MustDescribeEternalGuardianPresetContract()
    {
        var source = ReadGameEngineSource();

        Assert.Contains("ETERNAL GUARDIAN PRESETS:", source, StringComparison.Ordinal);
        Assert.Contains("guardian.sourcePreset", source, StringComparison.Ordinal);
        Assert.Contains("canonicalName", source, StringComparison.Ordinal);
        Assert.Contains("manifestationHistory", source, StringComparison.Ordinal);
    }

    [Fact]
    public void RuntimePrompt_MustDescribeDedicatedGuardianProjectTrackerSurface()
    {
        var source = ReadGameEngineSource();

        Assert.Contains("GUARDIAN PROJECT TRACKER — AFTERLIFE ONLY:", source, StringComparison.Ordinal);
        Assert.Contains("startGuardianProjects", source, StringComparison.Ordinal);
        Assert.Contains("guardianProjectUpdates", source, StringComparison.Ordinal);
        Assert.Contains("completeGuardianProjects", source, StringComparison.Ordinal);
        Assert.Contains("guardianPowerEvents", source, StringComparison.Ordinal);
        Assert.Contains("guardian_projects.json", source, StringComparison.Ordinal);
        Assert.Contains("abode_power_journal.json", source, StringComparison.Ordinal);
        Assert.Contains("no longer uses UpdateGuardians.updateProject", source, StringComparison.Ordinal);
    }

    [Fact]
    public void CliStepGuide_MustNotInstructDirectGuardianAbodePowerMutation()
    {
        var path = Path.Combine(TestRepoPaths.RepoRoot, "TaskGuides", "CLI_Step_Main.txt");
        var source = File.ReadAllText(path);

        Assert.DoesNotContain("If a guardian project changes Abode Power, update `guardian.abodePower` accordingly", source, StringComparison.Ordinal);
        Assert.Contains("Any Abode Power change must go through `guardianPowerEvents`", source, StringComparison.Ordinal);
    }

    [Fact]
    public void CliApiSpecification_MustDescribeGuardianPowerAndAfterlifeControlSurfaces()
    {
        var path = Path.Combine(TestRepoPaths.RepoRoot, "CLI_API_Specification.md");
        var source = File.ReadAllText(path);

        Assert.Contains("guardianPowerEvents", source, StringComparison.Ordinal);
        Assert.Contains("next_life_scenario_core.json", source, StringComparison.Ordinal);
        Assert.Contains("guardian_corrections.json", source, StringComparison.Ordinal);
        Assert.Contains("pending_abode_offering.json", source, StringComparison.Ordinal);
        Assert.Contains("archive_candidate_manifest.json", source, StringComparison.Ordinal);
        Assert.Contains("pending_archive_consultation_request.json", source, StringComparison.Ordinal);
        Assert.Contains("pending_archive_project_fuel_request.json", source, StringComparison.Ordinal);
        Assert.Contains("pending_guardian_trade_request.json", source, StringComparison.Ordinal);
        Assert.Contains("bonusClueRevealId", source, StringComparison.Ordinal);
    }

    [Fact]
    public void DomainDerivedGuardianMechanics_MustNotRemainInRuntimeContractsOrValidator()
    {
        var validationPath = Path.Combine(TestRepoPaths.RepoRoot, "BookOfEternityClient", "Services", "ValidationService.cs");
        var validationSource = File.ReadAllText(validationPath);
        var tradePath = Path.Combine(TestRepoPaths.RepoRoot, "BookOfEternityClient", "Services", "GuardianTradeService.cs");
        var tradeSource = File.ReadAllText(tradePath);

        Assert.DoesNotContain("AllowedGuardianDomains", validationSource, StringComparison.Ordinal);
        Assert.DoesNotContain("guardian_invalid_domain", validationSource, StringComparison.Ordinal);
        Assert.DoesNotContain("DomainProfiles", tradeSource, StringComparison.Ordinal);
        Assert.DoesNotContain("GetProfile(", tradeSource, StringComparison.Ordinal);
    }

    [Fact]
    public void RuntimePrompt_MustDescribeAbodeOfferingAndResonanceContracts()
    {
        var source = ReadGameEngineSource();

        Assert.Contains("pending_abode_offering.json", source, StringComparison.Ordinal);
        Assert.Contains("reasonType=offering", source, StringComparison.Ordinal);
        Assert.Contains("reasonType=resonance", source, StringComparison.Ordinal);
        Assert.Contains("GuardianAbodeOfferingState.EnsureHealthyAsync", source, StringComparison.Ordinal);
        Assert.Contains("afterlifeArchiveUpdates", source, StringComparison.Ordinal);
        Assert.Contains("archive_lore_fragment", source, StringComparison.Ordinal);
        Assert.Contains("archive_secret_record", source, StringComparison.Ordinal);
        Assert.Contains("archive_candidate_manifest.json", source, StringComparison.Ordinal);
    }

    [Fact]
    public void RuntimePrompt_MustDescribeScenarioCoreAndGuardianCorrectionsAsClientOwnedContracts()
    {
        var source = ReadGameEngineSource();

        Assert.Contains("NEXT-LIFE SCENARIO CORE / GUARDIAN CORRECTIONS:", source, StringComparison.Ordinal);
        Assert.Contains("game_state/control/next_life_scenario_core.json", source, StringComparison.Ordinal);
        Assert.Contains("game_state/world/guardian_corrections.json", source, StringComparison.Ordinal);
        Assert.Contains("not permission to negate, rewrite, or silently downgrade explicit player-authored start facts", source, StringComparison.Ordinal);
    }

    [Fact]
    public void RuntimePrompt_MustDescribeRivalSoulArcContract()
    {
        var source = ReadGameEngineSource();

        Assert.Contains("RIVAL SOUL ARCS — MORTAL WORLD ONLY:", source, StringComparison.Ordinal);
        Assert.Contains("Use UpdateRivalSoulArcs", source, StringComparison.Ordinal);
        Assert.Contains("Keep at most:", source, StringComparison.Ordinal);
        Assert.Contains("1 active major arc", source, StringComparison.Ordinal);
        Assert.Contains("relatedRivalArcId", source, StringComparison.Ordinal);
        Assert.Contains("If you surface a rival arc clue through worldEventsLog, mark that world event with relatedRivalArcId too", source, StringComparison.Ordinal);
        Assert.Contains("add turn/timestamp/date information to publicSignals or linked world events", source, StringComparison.Ordinal);
        Assert.Contains("consequences/impact/follow-up", source, StringComparison.Ordinal);
        Assert.Contains("bonusClueRevealId", source, StringComparison.Ordinal);
    }

    [Fact]
    public void AcceptedTurnRepairLoop_MustUsePreTurnSnapshotOnlyForInitialCanonicalMaterialization()
    {
        var source = ReadGameEngineSource();

        Assert.Contains("RefreshAcceptedTurnCanonicalStateForValidationAsync", source, StringComparison.Ordinal);
        Assert.Equal(1, source.Split("RefreshCanonicalStateAsync(snapshot)", StringSplitOptions.None).Length - 1);
        Assert.Contains("await RefreshCanonicalStateAsync();", source, StringComparison.Ordinal);
    }

    [Fact]
    public void RepairLoop_MustHealthCheckClientOwnedGuardianAndQteControlFilesBeforeRevalidation()
    {
        var source = ReadGameEngineSource();

        Assert.Contains("_afterlifeReturnGuardService.EnsureHealthyAsync", source, StringComparison.Ordinal);
        Assert.Contains("_systemGuardianLibraryService.EnsureAttractionRequestHealthyAsync", source, StringComparison.Ordinal);
        Assert.Contains("_qteSceneService.EnsureRuntimeStateHealthyAsync", source, StringComparison.Ordinal);
    }

    [Fact]
    public void TurnReminder_MustPassCurrentTurnNumberToRivalSoulArcReminderService()
    {
        var source = ReadGameEngineSource();

        Assert.Contains("BuildSystemReminderFragmentAsync(_stateManager.CurrentState.CurrentRealm, _gameLoop.TurnNumber)", source, StringComparison.Ordinal);
        Assert.Contains("_actorMemoryService.BuildSystemReminderFragmentAsync(_stateManager.CurrentState.CurrentRealm, _gameLoop.TurnNumber)", source, StringComparison.Ordinal);
    }

    [Fact]
    public void GuardianForcedIncarnation_RuntimeGate_MustFailClosedOnInvalidReturnGuard()
    {
        var source = ReadGameEngineSource();

        Assert.Contains("afterlife_return_guard is invalid. Failing closed", source, StringComparison.Ordinal);
    }

    [Fact]
    public void LifeEvaluationAndAcceptedAfterlifeTurns_MustActivateAndConsumeReturnProtection()
    {
        var source = ReadGameEngineSource();

        Assert.Contains("ActivatePostLifeReturnAsync", source, StringComparison.Ordinal);
        Assert.Contains("ConsumeAfterlifeReturnProtectionIfNeededAsync", source, StringComparison.Ordinal);
        Assert.Contains("OrdinaryPlayerTurnSourceLabel", source, StringComparison.Ordinal);
    }

    [Fact]
    public void LifeTransitions_AndNewIncarnation_MustResetLifeScopedRivalSoulArcs()
    {
        var source = ReadGameEngineSource();

        Assert.Contains("_rivalSoulArcService.ResetForNewLifeAsync();", source, StringComparison.Ordinal);
    }

    [Fact]
    public void FreeformGameEngineTextInputs_MustNotUseTextPromptStringDirectly()
    {
        var source = ReadGameEngineSource();

        Assert.DoesNotContain("TextPrompt<string>", source, StringComparison.Ordinal);
        Assert.Contains("PromptTextInput(", source, StringComparison.Ordinal);
    }

    [Fact]
    public void NewGameFlow_MustNotUseLegacyHardcodedGuardianTypeList()
    {
        var source = ReadGameEngineSource();

        Assert.DoesNotContain("Хранитель Магии — мудрый маг", source, StringComparison.Ordinal);
        Assert.DoesNotContain("Хранитель Битвы — закалённый воин", source, StringComparison.Ordinal);
        Assert.Contains("PromptSystemGuardianPresetSelectionAsync", source, StringComparison.Ordinal);
    }

    [Fact]
    public void MultilinePlayerInput_MustUseUnifiedTextComposerInsteadOfLegacyPasteSentinel()
    {
        var source = ReadGameEngineSource();

        Assert.DoesNotContain("::paste", source, StringComparison.Ordinal);
        Assert.Contains("Mode = TextComposerMode.MultilineEditor", source, StringComparison.Ordinal);
    }
}
