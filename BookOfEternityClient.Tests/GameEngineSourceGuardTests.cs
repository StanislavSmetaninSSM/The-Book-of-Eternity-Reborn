using Xunit;

namespace BookOfEternityClient.Tests;

public sealed class GameEngineSourceGuardTests
{
    private static string ReadGameEnginePartialSource(string fileName)
    {
        return File.ReadAllText(Path.Combine(
            TestRepoPaths.RepoRoot,
            "BookOfEternityClient",
            "Core",
            "GameEngine",
            fileName));
    }

    private static string ReadGameEngineSource()
    {
        var rootFile = Path.Combine(TestRepoPaths.RepoRoot, "BookOfEternityClient", "Core", "GameEngine.cs");
        var partialDir = Path.Combine(TestRepoPaths.RepoRoot, "BookOfEternityClient", "Core", "GameEngine");

        var files = new List<string> { rootFile };
        if (Directory.Exists(partialDir))
            files.AddRange(Directory.GetFiles(partialDir, "*.cs", SearchOption.TopDirectoryOnly).OrderBy(path => path, StringComparer.OrdinalIgnoreCase));

        return string.Join(Environment.NewLine + Environment.NewLine, files.Select(File.ReadAllText));
    }

    private static string ReadGameMasterDaemonSource()
    {
        return File.ReadAllText(Path.Combine(
            TestRepoPaths.RepoRoot,
            "BookOfEternityClient",
            "game_master_daemon.ps1"));
    }

    private static string ExtractMethodSource(string source, string methodHeader)
    {
        var start = source.IndexOf(methodHeader, StringComparison.Ordinal);
        Assert.True(start >= 0, $"Expected method header {methodHeader}.");

        var nextMethod = source.IndexOf("\n    private ", start + methodHeader.Length, StringComparison.Ordinal);
        Assert.True(nextMethod > start, $"Expected another private method after {methodHeader}.");
        return source[start..nextMethod];
    }

    private static string ExtractMethodSourceToEnd(string source, string methodHeader)
    {
        var start = source.IndexOf(methodHeader, StringComparison.Ordinal);
        Assert.True(start >= 0, $"Expected method header {methodHeader}.");
        return source[start..];
    }

    [Fact]
    public void NewGameFlow_MustCheckInitialWaitResultBeforeEnteringGameLoop()
    {
        var source = ReadGameEngineSource();

        Assert.Contains("if (!await WaitForGmResponse())", source, StringComparison.Ordinal);
    }

    [Fact]
    public void NewGameBootstrap_MustCreateGuardianProjectRollbackBaselineBeforeInitialDispatch()
    {
        var source = ReadGameEnginePartialSource("GameEngine.MainMenu.cs");
        var method = ExtractMethodSource(
            source,
            "private async Task InitializeChaosSea(");

        Assert.Contains("ChaosSeaBootstrapStateBuilder.BuildFreshNewGameFiles", method, StringComparison.Ordinal);
        Assert.Contains("\"lore/chaos_sea/player_chronicle.json\"", method, StringComparison.Ordinal);
        Assert.Contains("BuildAfterlifeEntityProfileRootForFreshNewGame", method, StringComparison.Ordinal);
        Assert.Contains("AfterlifeEntityProfileState.StatePath", method, StringComparison.Ordinal);
        Assert.Contains("WriteInitialGuardianProjectTrackerStateAsync", method, StringComparison.Ordinal);
        Assert.Contains("var rollbackBackups = await CreatePreTurnBackup(request.RequestId);", method, StringComparison.Ordinal);
        Assert.Contains("await CreateCanonicalBaselineSnapshotAsync(request, rollbackBackups, sourceLabel: \"первого описания Моря Хаоса\");", method, StringComparison.Ordinal);
        Assert.DoesNotContain("await CreateCanonicalBaselineSnapshotAsync(request, sourceLabel: \"первого описания Моря Хаоса\");", method, StringComparison.Ordinal);
    }

    [Fact]
    public void MortalBootstrap_MustWriteScaffoldBeforeInitialMortalDispatch()
    {
        var source = ReadGameEnginePartialSource("GameEngine.TurnLifecycle.cs");
        var method = ExtractMethodSource(
            source,
            "private async Task<bool> CheckGmIncarnationTrigger(");

        var snapshotIndex = method.IndexOf("await CreateCanonicalBaselineSnapshotAsync(request, rollbackBackups, \"GM-инициированного воплощения\");", StringComparison.Ordinal);
        var baselineIndex = method.IndexOf("await WriteMortalBootstrapBaselineAsync(", StringComparison.Ordinal);
        var scaffoldIndex = method.IndexOf("await WriteMortalBootstrapScaffoldAsync(", StringComparison.Ordinal);
        var requestWriteIndex = method.IndexOf("await _fs.WriteFileAtomicAsync(\"input/turn_request.json\"", StringComparison.Ordinal);

        Assert.True(baselineIndex >= 0, "Fresh Mortal bootstrap must materialize client-owned baseline files before the GM sees the first Mortal turn.");
        Assert.True(snapshotIndex >= 0, "Fresh Mortal bootstrap must create a rollback baseline before dispatch.");
        Assert.True(baselineIndex < snapshotIndex, "Fresh Mortal bootstrap baseline files must exist before pending-turn snapshot authority is captured.");
        Assert.True(scaffoldIndex > snapshotIndex, "Fresh Mortal bootstrap scaffold must be written after baseline authority exists.");
        Assert.True(scaffoldIndex < requestWriteIndex, "Fresh Mortal bootstrap scaffold must be available before GM sees input/turn_request.json.");
        Assert.Contains("MortalBootstrapStateBuilder.BuildFreshMortalBootstrapFiles", source, StringComparison.Ordinal);
        Assert.Contains("game_state/control/mortal_bootstrap_scaffold.json", source, StringComparison.Ordinal);
        Assert.Contains("MORTAL BOOTSTRAP BASELINE", source, StringComparison.Ordinal);
        Assert.Contains("baselineMaterializedBeforeDispatch", source, StringComparison.Ordinal);
    }

    [Fact]
    public void MortalBootstrapScaffold_MustContainCanonicalShapeRepairPreventionHints()
    {
        var source = ReadGameEnginePartialSource("GameEngine.TurnLifecycle.cs");
        var method = ExtractMethodSource(source, "private async Task WriteMortalBootstrapScaffoldAsync(");

        Assert.Contains("\"canonicalShapeHints\"", method, StringComparison.Ordinal);
        Assert.Contains("\"factionCoreMinimum\"", method, StringComparison.Ordinal);
        Assert.Contains("\"factionCustomStateMinimum\"", method, StringComparison.Ordinal);
        Assert.Contains("\"npcCoreMinimum\"", method, StringComparison.Ordinal);
        Assert.Contains("\"currentLocationMinimum\"", method, StringComparison.Ordinal);
        Assert.Contains("\"worldMapMinimum\"", method, StringComparison.Ordinal);
        Assert.Contains("\"activeThreatObjectMinimum\"", method, StringComparison.Ordinal);
        Assert.Contains("\"inventoryItemMinimum\"", method, StringComparison.Ordinal);
        Assert.Contains("\"codexEntryMinimum\"", method, StringComparison.Ordinal);
        Assert.Contains("\"sourceFilePrefixRequired\"", method, StringComparison.Ordinal);
        Assert.Contains("current_world/", method, StringComparison.Ordinal);
        Assert.Contains("\"canonicalFactionCustomStateRequiredFields\"", method, StringComparison.Ordinal);
        Assert.Contains("currentValue", method, StringComparison.Ordinal);
        Assert.Contains("progressionRule", method, StringComparison.Ordinal);
        Assert.Contains("thresholds", method, StringComparison.Ordinal);
        Assert.Contains("\"allowedThreatMotivations\"", method, StringComparison.Ordinal);
        Assert.Contains("Domination", method, StringComparison.Ordinal);
        Assert.Contains("Preservation", method, StringComparison.Ordinal);
        Assert.Contains("\"allowedThreatMethods\"", method, StringComparison.Ordinal);
        Assert.Contains("Systemic", method, StringComparison.Ordinal);
        Assert.Contains("\"allowedThreatPrimaryTargetTypes\"", method, StringComparison.Ordinal);
        Assert.Contains("\"allowedThreatPrimaryImpacts\"", method, StringComparison.Ordinal);
        Assert.Contains("\"game_state/inventory/items.json\"", method, StringComparison.Ordinal);
        Assert.Contains("\"allowedQualityValues\"", method, StringComparison.Ordinal);
        Assert.Contains("Trash", method, StringComparison.Ordinal);
        Assert.Contains("Uncommon", method, StringComparison.Ordinal);
        Assert.Contains("\"allowedEquipmentSlots\"", method, StringComparison.Ordinal);
        Assert.Contains("MainHand", method, StringComparison.Ordinal);
        Assert.Contains("Accessory1", method, StringComparison.Ordinal);
        Assert.Contains("\"allowedFactionControlTypes\"", method, StringComparison.Ordinal);
        Assert.Contains("Military", method, StringComparison.Ordinal);
        Assert.Contains("Covert", method, StringComparison.Ordinal);
        Assert.Contains("\"repairPreventionChecklist\"", method, StringComparison.Ordinal);
        Assert.Contains("bootstrap_codex_missing_current_world_entries", method, StringComparison.Ordinal);
        Assert.Contains("npc_contract_unknown_top_level_key", method, StringComparison.Ordinal);
        Assert.Contains("player character", method, StringComparison.Ordinal);
        Assert.Contains("world_map_link_preview_missing_difficulty_profile", method, StringComparison.Ordinal);
    }

    [Fact]
    public void MortalBootstrapScaffold_MustMakeRequestedTrainingMentorsActionable()
    {
        var source = ReadGameEnginePartialSource("GameEngine.TurnLifecycle.cs");
        var method = ExtractMethodSource(source, "private async Task WriteMortalBootstrapScaffoldAsync(");

        Assert.Contains("\"trainingAnchorRequirements\"", method, StringComparison.Ordinal);
        Assert.Contains("teacherProfile", method, StringComparison.Ordinal);
        Assert.Contains("canTeach", method, StringComparison.Ordinal);
        Assert.Contains("skills", method, StringComparison.Ordinal);
        Assert.Contains("/обучение", method, StringComparison.Ordinal);
        Assert.Contains("pending_training_showcase_requests.json", method, StringComparison.Ordinal);
        Assert.Contains("Do not advertise paid training only in prose", method, StringComparison.Ordinal);
        Assert.Contains("starterResourceGrant.CurrentLevelExperience", method, StringComparison.Ordinal);
        Assert.Contains("preMaterializedBaselineFiles.Add(\"game_state/npcs/npc_core.json\")", method, StringComparison.Ordinal);
        Assert.Contains("requiredMortalBootstrapFiles.Add(\"game_state/npcs/npc_core.json\")", method, StringComparison.Ordinal);
    }

    [Fact]
    public void MortalBootstrap_MustGrantStarterResourcesWhenPaidTrainingOrTradeIsRequested()
    {
        var source = ReadGameEnginePartialSource("GameEngine.TurnLifecycle.cs");
        var triggerMethod = ExtractMethodSource(source, "private async Task<bool> CheckGmIncarnationTrigger(");
        var scaffoldMethod = ExtractMethodSource(source, "private async Task WriteMortalBootstrapScaffoldAsync(");

        Assert.Contains("MortalBootstrapStateBuilder.InferStarterResourceGrant", triggerMethod, StringComparison.Ordinal);
        Assert.Contains("starterResourceGrant.Money", triggerMethod, StringComparison.Ordinal);
        Assert.DoesNotContain("money = 0", triggerMethod, StringComparison.Ordinal);
        Assert.Contains("\"starterResourceRequirements\"", scaffoldMethod, StringComparison.Ordinal);
        Assert.Contains("starter purse", scaffoldMethod, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("current-level XP", scaffoldMethod, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void MortalBootstrapScaffold_NpcCoreTopLevelCollectionsMustMatchValidatorFileContract()
    {
        var source = ReadGameEnginePartialSource("GameEngine.TurnLifecycle.cs");
        var method = ExtractMethodSource(source, "private async Task WriteMortalBootstrapScaffoldAsync(");
        var allowListStart = method.IndexOf("[\"allowedTopLevelCollections\"]", StringComparison.Ordinal);
        Assert.True(allowListStart >= 0, "Expected npc_core allowedTopLevelCollections guidance.");

        var allowListEnd = method.IndexOf("[\"forbiddenTopLevelKeysInNpcCore\"]", allowListStart, StringComparison.Ordinal);
        Assert.True(allowListEnd > allowListStart, "Expected forbiddenTopLevelKeysInNpcCore after allowedTopLevelCollections.");

        var allowList = method[allowListStart..allowListEnd];
        Assert.Contains("\"NPCsInScene\"", allowList, StringComparison.Ordinal);
        Assert.Contains("\"NPCsRenameData\"", allowList, StringComparison.Ordinal);
        Assert.Contains("\"UpdateNPCs\"", allowList, StringComparison.Ordinal);
        Assert.Contains("\"UpdateNpcTradeInventoryReceipts\"", allowList, StringComparison.Ordinal);
        Assert.Contains("\"trainingPurchaseReceipts\"", allowList, StringComparison.Ordinal);
        Assert.DoesNotContain("\"NPCJournals\"", allowList, StringComparison.Ordinal);
        Assert.DoesNotContain("\"NPCQuestUpdates\"", allowList, StringComparison.Ordinal);
        Assert.DoesNotContain("\"NPCRelationshipUpdates\"", allowList, StringComparison.Ordinal);
    }

    [Fact]
    public void AcceptedTurnCleanup_MustClearSatisfiedMortalTrainingSkillEvolutionRequests()
    {
        var source = ReadGameEnginePartialSource("GameEngine.SessionAndSnapshots.cs");
        var method = ExtractMethodSource(source, "private async Task CleanupAcceptedTurnTerminalArtifactsAsync()");

        Assert.Contains("CleanupSatisfiedMortalSkillEvolutionRequestsAsync", method, StringComparison.Ordinal);
    }

    [Fact]
    public void MortalBootstrapScaffold_MustWarnAgainstPuttingOffscreenExitActorsIntoNpcScene()
    {
        var source = ReadGameEnginePartialSource("GameEngine.TurnLifecycle.cs");
        var method = ExtractMethodSource(source, "private async Task WriteMortalBootstrapScaffoldAsync(");

        Assert.Contains("\"offscreenSceneActorRule\"", method, StringComparison.Ordinal);
        Assert.Contains("NPCsInScene is only for actors physically present in currentLocationData", method, StringComparison.Ordinal);
        Assert.Contains("voices behind a door", method, StringComparison.Ordinal);
        Assert.Contains("nearbyExitLocationId", method, StringComparison.Ordinal);
        Assert.Contains("Prevent npc_scene_location_mismatch", method, StringComparison.Ordinal);
    }

    [Fact]
    public void MortalBootstrapScaffold_ItemDurabilityRuleMustUsePercentageString()
    {
        var source = ReadGameEnginePartialSource("GameEngine.TurnLifecycle.cs");
        var method = ExtractMethodSource(source, "private async Task WriteMortalBootstrapScaffoldAsync(");

        Assert.Contains("\"durabilityRule\"", method, StringComparison.Ordinal);
        Assert.Contains("percentage string", method, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("100%", method, StringComparison.Ordinal);
        Assert.DoesNotContain("use 100 for intact", method, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void MortalBootstrapScaffold_MustPublishCanonicalCoordinateAuthority()
    {
        var source = ReadGameEnginePartialSource("GameEngine.TurnLifecycle.cs");
        var method = ExtractMethodSource(source, "private async Task WriteMortalBootstrapScaffoldAsync(");

        Assert.Contains("\"canonicalCoordinateAuthority\"", method, StringComparison.Ordinal);
        Assert.Contains("\"currentLocationCoordinates\"", method, StringComparison.Ordinal);
        Assert.Contains("\"nearbyExitCoordinates\"", method, StringComparison.Ordinal);
        Assert.Contains("current_location_coordinates_mismatch", method, StringComparison.Ordinal);
        Assert.Contains("Copy these exact coordinates", method, StringComparison.Ordinal);
    }

    [Fact]
    public void NewGameFlow_MustUseAgentConsoleCompatibleGuardianChoicePrompt()
    {
        var source = ReadGameEnginePartialSource("GameEngine.MainMenu.cs");
        var method = ExtractMethodSource(source, "private async Task NewGameFlow()");

        Assert.Contains("PromptGuardianCreationMode", method, StringComparison.Ordinal);
        Assert.Contains("PromptAgentConsoleTextInput", method, StringComparison.Ordinal);
        Assert.Contains("PromptAgentConsoleMenuSelection", source, StringComparison.Ordinal);
        Assert.DoesNotContain("new SelectionPrompt<string>()", method, StringComparison.Ordinal);
        Assert.DoesNotContain("var soulName = PromptTextInput(", method, StringComparison.Ordinal);
        Assert.DoesNotContain("var guardianDescription = PromptTextInput(", method, StringComparison.Ordinal);
    }

    [Fact]
    public void SystemGuardianPresetSelection_MustUseAgentConsoleCompatibleChoicePrompt()
    {
        var source = ReadGameEnginePartialSource("GameEngine.MainMenu.cs");
        var method = ExtractMethodSource(source, "private async Task<SystemGuardianLibraryService.SystemGuardianPresetDescriptor?> PromptSystemGuardianPresetSelectionAsync()");

        Assert.Contains("PromptGuardianPresetChoice", method, StringComparison.Ordinal);
        Assert.DoesNotContain("new SelectionPrompt<string>()", method, StringComparison.Ordinal);
    }

    [Fact]
    public void GuardianSetupChoicePrompts_MustPublishAgentConsoleSnapshots()
    {
        var source = ReadGameEnginePartialSource("GameEngine.MainMenu.cs");
        var modeMethod = ExtractMethodSource(source, "private string PromptGuardianCreationMode()");
        var presetMethod = ExtractMethodSource(source, "private int PromptGuardianPresetChoice(");

        Assert.Contains("PromptAgentConsoleMenuSelection", modeMethod, StringComparison.Ordinal);
        Assert.Contains("PromptAgentConsoleMenuSelection", presetMethod, StringComparison.Ordinal);
        Assert.DoesNotContain("PromptAgentConsoleTextInput", modeMethod, StringComparison.Ordinal);
        Assert.DoesNotContain("PromptAgentConsoleTextInput", presetMethod, StringComparison.Ordinal);
        Assert.DoesNotContain("var input = PromptTextInput(", modeMethod, StringComparison.Ordinal);
        Assert.DoesNotContain("var input = PromptTextInput(", presetMethod, StringComparison.Ordinal);
    }

    [Fact]
    public void IncarnationSetupTextPrompts_MustPublishAgentConsoleSnapshots()
    {
        var source = ReadGameEnginePartialSource("GameEngine.MainMenu.cs");
        var method = ExtractMethodSource(source, "private async Task HandleIncarnation()");

        Assert.Contains("PromptAgentConsoleTextInput", method, StringComparison.Ordinal);
        Assert.DoesNotContain("var charDesc = PromptTextInput(", method, StringComparison.Ordinal);
        Assert.DoesNotContain("var worldDesc = PromptTextInput(", method, StringComparison.Ordinal);
        Assert.DoesNotContain("var circumstances = PromptTextInput(", method, StringComparison.Ordinal);
    }

    [Fact]
    public void IncarnationTriggerTransition_MustPublishAgentConsoleKeyAndStatSnapshots()
    {
        var source = ReadGameEnginePartialSource("GameEngine.TurnLifecycle.cs");
        var triggerMethod = ExtractMethodSource(source, "private async Task<bool> CheckGmIncarnationTrigger(");
        var statsMethod = ExtractMethodSourceToEnd(source, "private async Task ShowStatDistribution(");

        Assert.Contains("ReadAgentConsoleKeyContinuation(", triggerMethod, StringComparison.Ordinal);
        Assert.DoesNotContain("_inputSource.ReadKey(intercept: true);", triggerMethod, StringComparison.Ordinal);
        Assert.Contains("PublishAgentConsoleStatAllocationSnapshot(", statsMethod, StringComparison.Ordinal);
        Assert.Contains("ReadAgentConsoleKeyContinuation(", statsMethod, StringComparison.Ordinal);
    }

    [Fact]
    public void RealmTransitionBanner_MustPublishAgentConsoleKeySnapshot()
    {
        var path = Path.Combine(
            TestRepoPaths.RepoRoot,
            "BookOfEternityClient",
            "UI",
            "GameInterface.cs");
        var source = File.ReadAllText(path);
        var method = ExtractMethodSource(source, "public static void RenderRealmTransition(");

        Assert.Contains("AgentConsoleLiveInputSource", method, StringComparison.Ordinal);
        Assert.Contains("PublishSnapshot", method, StringComparison.Ordinal);
        Assert.Contains("InputKind = AgentConsoleInputKind.Key", method, StringComparison.Ordinal);
    }

    [Fact]
    public void GmResponseWaits_MustPublishAgentConsoleLoadingSnapshotBeforeTerminalWait()
    {
        var turnLifecycleSource = ReadGameEnginePartialSource("GameEngine.TurnLifecycle.cs");
        var agentConsoleSource = ReadGameEnginePartialSource("GameEngine.AgentConsole.cs");

        var waitMethod = ExtractMethodSource(turnLifecycleSource, "private async Task<bool> WaitForGmResponse()");
        var rawWaitMethod = ExtractMethodSource(turnLifecycleSource, "private async Task<bool> WaitForGmResponseRaw()");

        var waitPublish = waitMethod.IndexOf("PublishAgentConsoleGmWaitingSnapshot(", StringComparison.Ordinal);
        var waitTerminal = waitMethod.IndexOf("WaitForTerminalSignalAsync()", StringComparison.Ordinal);
        var rawWaitPublish = rawWaitMethod.IndexOf("PublishAgentConsoleGmWaitingSnapshot(", StringComparison.Ordinal);
        var rawWaitTerminal = rawWaitMethod.IndexOf("WaitForTerminalSignalAsync()", StringComparison.Ordinal);

        Assert.True(waitPublish >= 0, "Ordinary GM waits must publish an Agent Console loading snapshot.");
        Assert.True(waitTerminal >= 0, "Ordinary GM waits must wait for terminal files.");
        Assert.True(waitPublish < waitTerminal,
            "Ordinary GM waits must publish an Agent Console loading snapshot before waiting for terminal files.");
        Assert.True(rawWaitPublish >= 0, "Raw transition GM waits must publish an Agent Console loading snapshot.");
        Assert.True(rawWaitTerminal >= 0, "Raw transition GM waits must wait for terminal files.");
        Assert.True(rawWaitPublish < rawWaitTerminal,
            "Raw transition GM waits must publish an Agent Console loading snapshot before waiting for terminal files.");

        var helper = ExtractMethodSource(agentConsoleSource, "private void PublishAgentConsoleGmWaitingSnapshot(");
        Assert.Contains("AgentConsoleMode.Loading", helper, StringComparison.Ordinal);
        Assert.Contains("AwaitingInput = false", helper, StringComparison.Ordinal);
        Assert.Contains("InputKind = AgentConsoleInputKind.None", helper, StringComparison.Ordinal);
        Assert.Contains("ScreenId = \"gm-waiting\"", helper, StringComparison.Ordinal);
    }

    [Fact]
    public void GmResponseWaits_MustFailFastThroughHarnessTerminalErrorWhenRuntimeIsDead()
    {
        var turnLifecycleSource = ReadGameEnginePartialSource("GameEngine.TurnLifecycle.cs");
        var waitMethod = ExtractMethodSource(turnLifecycleSource, "private async Task<TerminalSignalWaitOutcome> WaitForTerminalSignalAsync()");

        Assert.Contains("ResolveTerminalSignalTimeoutSecondsAsync()", waitMethod, StringComparison.Ordinal);
        Assert.DoesNotContain("Math.Max(15, _stateManager.Settings.GmTimeoutSeconds)", waitMethod, StringComparison.Ordinal);
        Assert.Contains("DetectUnavailableGmRuntimeAsync()", waitMethod, StringComparison.Ordinal);
        Assert.Contains("TryWriteHarnessTerminalErrorAsync(", waitMethod, StringComparison.Ordinal);
        Assert.Contains("TerminalSignalWaitOutcome.Completed", waitMethod, StringComparison.Ordinal);

        Assert.Contains("private async Task<int> ResolveTerminalSignalTimeoutSecondsAsync()", turnLifecycleSource, StringComparison.Ordinal);
        Assert.Contains("GmDaemonTerminalTimeoutGraceSeconds", turnLifecycleSource, StringComparison.Ordinal);
        Assert.Contains("_stateManager.Settings.GmTimeoutSeconds", turnLifecycleSource, StringComparison.Ordinal);
        Assert.Contains("\"turnTimeoutSeconds\"", turnLifecycleSource, StringComparison.Ordinal);
        Assert.Contains("activeDaemonTimeoutSeconds + GmDaemonTerminalTimeoutGraceSeconds", turnLifecycleSource, StringComparison.Ordinal);
        Assert.Contains("GmDisabledDaemonTimeoutClientFloorSeconds", turnLifecycleSource, StringComparison.Ordinal);
        Assert.Contains("activeDaemonDisabledTimeoutSeconds <= 0", turnLifecycleSource, StringComparison.Ordinal);
        Assert.Contains("private async Task<string?> DetectUnavailableGmRuntimeAsync()", turnLifecycleSource, StringComparison.Ordinal);
        Assert.Contains("\"game_state/control/gm_daemon_status.json\"", turnLifecycleSource, StringComparison.Ordinal);
        Assert.Contains("\"game_state/control/gm_bridge_status.json\"", turnLifecycleSource, StringComparison.Ordinal);
        Assert.Contains("Process.GetProcessById", turnLifecycleSource, StringComparison.Ordinal);
        Assert.Contains("private async Task<bool> TryWriteHarnessTerminalErrorAsync(", turnLifecycleSource, StringComparison.Ordinal);
        Assert.Contains("\"ready/turn_error.json\"", turnLifecycleSource, StringComparison.Ordinal);
        Assert.Contains("harnessSource", turnLifecycleSource, StringComparison.Ordinal);
        Assert.Contains("gm_runtime_unavailable", turnLifecycleSource, StringComparison.Ordinal);
        Assert.Contains("gm_terminal_wait_timeout", turnLifecycleSource, StringComparison.Ordinal);
    }

    [Fact]
    public void OrdinaryPlayerTurnWait_MustPublishAgentConsoleLoadingSnapshotBeforeTerminalWait()
    {
        var turnLifecycleSource = ReadGameEnginePartialSource("GameEngine.TurnLifecycle.cs");
        var method = ExtractMethodSource(turnLifecycleSource, "private async Task ProcessPlayerTurn(");

        var publishIndex = method.IndexOf("PublishAgentConsoleGmWaitingSnapshot(", StringComparison.Ordinal);
        var waitIndex = method.IndexOf("WaitForTerminalSignalAsync()", StringComparison.Ordinal);

        Assert.True(publishIndex >= 0, "Ordinary player turns must publish an Agent Console GM-waiting snapshot.");
        Assert.True(waitIndex >= 0, "Ordinary player turns must wait for terminal files.");
        Assert.True(publishIndex < waitIndex,
            "Ordinary player turns must publish an Agent Console GM-waiting snapshot before waiting for terminal files.");
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
    public void EndOfLifeCommand_MustRequireExplicitMortalRealm()
    {
        var source = ReadGameEnginePartialSource("GameEngine.TurnLifecycle.cs");

        Assert.Contains("RealmSemantics.IsMortalRealm(currentRealm)", source, StringComparison.Ordinal);
        Assert.DoesNotContain("!_stateManager.CurrentState.IsInAfterlifeRealm)", source, StringComparison.Ordinal);
    }

    [Fact]
    public void RealmSensitiveStartupCleanup_MustRefreshPersistedStateBeforeHygiene()
    {
        var sessionSnapshotSource = ReadGameEnginePartialSource("GameEngine.SessionAndSnapshots.cs");
        var validationSource = ReadGameEnginePartialSource("GameEngine.ValidationAndRepair.cs");

        Assert.Contains("await _stateManager.RefreshGameStateAsync();", sessionSnapshotSource, StringComparison.Ordinal);
        Assert.Contains("await _stateManager.RefreshGameStateAsync();", validationSource, StringComparison.Ordinal);
    }

    [Fact]
    public void RealmTransitions_MustUseCoordinatedWritesAndAbortWithoutSoulRealmAuthority()
    {
        var incarnationSource = ReadGameEnginePartialSource("GameEngine.IncarnationAndAfterlife.cs");
        var mainMenuSource = ReadGameEnginePartialSource("GameEngine.MainMenu.cs");
        var turnLifecycleSource = ReadGameEnginePartialSource("GameEngine.TurnLifecycle.cs");

        Assert.Contains("private async Task<bool> UpdateSoulStateRealm", incarnationSource, StringComparison.Ordinal);
        Assert.Contains("TryCommitCoordinatedGameStateWritesAsync(", incarnationSource, StringComparison.Ordinal);
        Assert.Contains("TryCommitCoordinatedGameStateWritesAsync(", mainMenuSource, StringComparison.Ordinal);
        Assert.Contains("TryCommitCoordinatedGameStateWritesAsync(", turnLifecycleSource, StringComparison.Ordinal);
        Assert.DoesNotContain("await UpdateSoulStateRealm(\"Shining Abode\");", mainMenuSource, StringComparison.Ordinal);
        Assert.DoesNotContain("await UpdateSoulStateRealm(\"Shining Abode\");", turnLifecycleSource, StringComparison.Ordinal);
        Assert.Contains("if (!await UpdateSoulStateRealm(\"Chaos Sea\", lifeSummary))", turnLifecycleSource, StringComparison.Ordinal);
        Assert.Contains("if (!await UpdateSoulStateRealm(\"Mortal World\", incrementIncarnation: true))", turnLifecycleSource, StringComparison.Ordinal);
        Assert.True(
            turnLifecycleSource.IndexOf("if (!await UpdateSoulStateRealm(\"Chaos Sea\", lifeSummary))", StringComparison.Ordinal) <
            turnLifecycleSource.IndexOf("_fs.ClearCurrentWorldLore();", StringComparison.Ordinal));
        Assert.True(
            turnLifecycleSource.IndexOf("if (!await UpdateSoulStateRealm(\"Mortal World\", incrementIncarnation: true))", StringComparison.Ordinal) <
            turnLifecycleSource.IndexOf("await _rivalSoulArcService.ResetForNewLifeAsync();", StringComparison.Ordinal));
        Assert.Contains("await RefreshRuntimeStateAsync();", turnLifecycleSource, StringComparison.Ordinal);
    }

    [Fact]
    public void NewGamePlus_MustRouteThroughSafeShiningNewCycleReturn()
    {
        var source = ReadGameEnginePartialSource("GameEngine.MainMenu.cs");

        Assert.Contains("private async Task HandleNewGamePlus()", source, StringComparison.Ordinal);
        Assert.Contains("await HandleReturnToChaosSeaFromShiningAbode();", source, StringComparison.Ordinal);
        Assert.DoesNotContain("ExecuteNewGamePlusResetAsync", source, StringComparison.Ordinal);
        Assert.DoesNotContain("Чернильные Перья будут сброшены", source, StringComparison.Ordinal);
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
    public void PlayerFacingDialogueOptionSurfaces_MustUseControlTagNormalizer()
    {
        var files = new[]
        {
            Path.Combine(TestRepoPaths.RepoRoot, "BookOfEternityClient", "Core", "GameEngine", "GameEngine.SessionAndSnapshots.cs"),
            Path.Combine(TestRepoPaths.RepoRoot, "BookOfEternityClient", "Core", "GameEngine", "GameEngine.AgentConsole.cs"),
            Path.Combine(TestRepoPaths.RepoRoot, "BookOfEternityClient", "Core", "GameEngine", "GameEngine.TurnLifecycle.cs"),
            Path.Combine(TestRepoPaths.RepoRoot, "BookOfEternityClient", "UI", "GameInterface.cs"),
            Path.Combine(TestRepoPaths.RepoRoot, "BookOfEternityClient", "WebUi", "BrowserGameScreenService.cs")
        };

        foreach (var file in files)
        {
            var source = File.ReadAllText(file);
            Assert.Contains("DialogueOptionControlTagNormalizer", source, StringComparison.Ordinal);
        }
    }

    [Fact]
    public void GameInterfaceTransitions_MustUseHeadlessSafeConsoleClear()
    {
        var source = File.ReadAllText(Path.Combine(
            TestRepoPaths.RepoRoot,
            "BookOfEternityClient",
            "UI",
            "GameInterface.cs"));

        Assert.DoesNotContain("AnsiConsole.Clear();", source, StringComparison.Ordinal);
        Assert.Contains("SpectreConsoleSafe.Clear();", source, StringComparison.Ordinal);
    }

    [Fact]
    public void LifeEvaluationRewardScreen_MustUseHeadlessSafeConsoleClear()
    {
        var source = ReadGameEnginePartialSource("GameEngine.TurnLifecycle.cs");
        var methodStart = source.IndexOf("private async Task ShowLifeEvaluationRewards(", StringComparison.Ordinal);
        Assert.True(methodStart >= 0, "ShowLifeEvaluationRewards must exist.");
        var nextMethodStart = source.IndexOf("    private async Task", methodStart + 1, StringComparison.Ordinal);
        Assert.True(nextMethodStart > methodStart, "ShowLifeEvaluationRewards method boundary must be discoverable.");
        var methodSource = source[methodStart..nextMethodStart];

        Assert.DoesNotContain("AnsiConsole.Clear();", methodSource, StringComparison.Ordinal);
        Assert.Contains("SpectreConsoleSafe.Clear();", methodSource, StringComparison.Ordinal);
    }

    [Fact]
    public void ClientRuntimeCode_MustRouteConsoleClearThroughHeadlessSafeWrapper()
    {
        var clientRoot = Path.Combine(TestRepoPaths.RepoRoot, "BookOfEternityClient");
        var offenders = Directory
            .EnumerateFiles(clientRoot, "*.cs", SearchOption.AllDirectories)
            .Where(path => !path.EndsWith(Path.Combine("UI", "SpectreConsoleSafe.cs"), StringComparison.OrdinalIgnoreCase))
            .Where(path => File.ReadAllText(path).Contains("AnsiConsole.Clear();", StringComparison.Ordinal))
            .Select(path => Path.GetRelativePath(TestRepoPaths.RepoRoot, path))
            .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        Assert.Empty(offenders);
    }

    [Fact]
    public void GameEngineRuntimeConfirmations_MustUseAgentConsoleObservableHelper()
    {
        var source = ReadGameEngineSource();

        Assert.Equal(1, source.Split("AnsiConsole.Confirm(", StringSplitOptions.None).Length - 1);
        Assert.Contains("return AnsiConsole.Confirm(promptMarkup, defaultValue);", source, StringComparison.Ordinal);
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
    public void RuntimePrompt_MustRequireSoldOutSnapshotInShiningTradeReceipts()
    {
        var source = ReadGameEngineSource();

        Assert.Contains("soldOutCount", source, StringComparison.Ordinal);
        Assert.Contains("faction.tradeInventoryReceipts[]", source, StringComparison.Ordinal);
    }

    [Fact]
    public void IncarnationFlow_MustBlockOnPendingArchiveActionsBeforeLeavingAfterlife()
    {
        var source = ReadGameEngineSource();

        Assert.Contains("AfterlifeArchiveActionState.ReadConsultationStateAsync(_fs)", source, StringComparison.Ordinal);
        Assert.Contains("AfterlifeArchiveActionState.ReadProjectFuelStateAsync(_fs)", source, StringComparison.Ordinal);
        Assert.Contains("Нельзя войти в новую смертную жизнь, пока остаются незакрытые загробные контракты", source, StringComparison.Ordinal);
        Assert.Contains("GuardianAbodeOfferingState.PendingRequestPath", source, StringComparison.Ordinal);
        Assert.Contains("GuardianTradeRequestState.ReadStateAsync(_fs)", source, StringComparison.Ordinal);
    }

    [Fact]
    public void RuntimePrompt_MustRequireResidentReasoningForResidentStateAndJournalUpdates()
    {
        var source = ReadGameEngineSource();

        Assert.Contains("The same scope discipline applies to afterlife residents", source, StringComparison.Ordinal);
        Assert.Contains("UpdateGuardianAbodeResidents", source, StringComparison.Ordinal);
        Assert.Contains("residentThoughtJournalUpdates", source, StringComparison.Ordinal);
        Assert.Contains("residentInteractionLogUpdates", source, StringComparison.Ordinal);
        Assert.Contains("UpdateGuardianAbodeResidentHistoryLog", source, StringComparison.Ordinal);
        Assert.Contains("If a turn changes a resident's abode devotion, restlessness, migration state", source, StringComparison.Ordinal);
    }

    [Fact]
    public void CheckLifeTransitions_MustUseSharedCanonicalTriggerLifeEndParser()
    {
        var source = ReadGameEngineSource();

        Assert.Contains("CanonicalStateNormalizer.TryReadCanonicalTriggerLifeEnd(root, out var reason, out var summary)", source, StringComparison.Ordinal);
        Assert.Contains("CanonicalStateNormalizer.ResolveLifecycleAuthorizedTriggerLifeEndFromPendingSnapshotAsync(", source, StringComparison.Ordinal);
        Assert.DoesNotContain("TryReadLifeTransitionPayload(root, out var reason, out var summary)", source, StringComparison.Ordinal);
        Assert.DoesNotContain("if (root.TryGetProperty(\"TriggerLifeEnd\", out var nested) && nested.ValueKind == JsonValueKind.Object)", source, StringComparison.Ordinal);
        Assert.DoesNotContain("TryReadPreTurnSoulStateRealmFromPendingSnapshotAsync()", source, StringComparison.Ordinal);
    }

    [Fact]
    public void AcceptedTurnHappyPath_MustKeepPendingSnapshotAliveUntilLifeTransitionChecksFinish()
    {
        var source = ReadGameEngineSource();
        var acceptedTurnAnchor = source.IndexOf("// Turn accepted — backup no longer needed", StringComparison.Ordinal);
        Assert.True(acceptedTurnAnchor >= 0);

        var lifeTransitionIndex = source.IndexOf("CheckLifeTransitions(activeSnapshotContext)", acceptedTurnAnchor, StringComparison.Ordinal);
        var cleanupIndex = source.IndexOf("await CleanupAcceptedTurnTerminalArtifactsAsync();", acceptedTurnAnchor, StringComparison.Ordinal);

        Assert.True(lifeTransitionIndex >= 0);
        Assert.True(cleanupIndex >= 0);
        Assert.True(lifeTransitionIndex < cleanupIndex, "accepted-turn pending snapshot cleanup must happen after CheckLifeTransitions()");
    }

    [Fact]
    public void AcceptedTurnHappyPath_MustKeepRollbackBackupAliveUntilLifeTransitionChecksFinish()
    {
        var source = ReadGameEnginePartialSource("GameEngine.TurnLifecycle.cs");
        var acceptedTurnAnchor = source.IndexOf("// Turn accepted — backup no longer needed", StringComparison.Ordinal);
        Assert.True(acceptedTurnAnchor >= 0);

        var lifeTransitionIndex = source.IndexOf("CheckLifeTransitions(activeSnapshotContext)", acceptedTurnAnchor, StringComparison.Ordinal);
        var cleanupBackupIndex = source.IndexOf("CleanupBackup(backedUpFiles);", acceptedTurnAnchor, StringComparison.Ordinal);

        Assert.True(lifeTransitionIndex >= 0);
        Assert.True(cleanupBackupIndex >= 0);
        Assert.True(lifeTransitionIndex < cleanupBackupIndex, "accepted-turn rollback backup files must remain readable until TriggerLifeEnd authority checks finish.");
    }

    [Fact]
    public void AcceptedTurnHappyPath_MustPassValidatedSnapshotContextToLifeTransitionChecks()
    {
        var source = ReadGameEnginePartialSource("GameEngine.TurnLifecycle.cs");
        var acceptedTurnAnchor = source.IndexOf("// Turn accepted — backup no longer needed", StringComparison.Ordinal);
        Assert.True(acceptedTurnAnchor >= 0);

        var lifeTransitionIndex = source.IndexOf("CheckLifeTransitions(activeSnapshotContext)", acceptedTurnAnchor, StringComparison.Ordinal);

        Assert.True(lifeTransitionIndex >= 0, "accepted-turn TriggerLifeEnd checks must use the already validated pending snapshot context because repair cleanup can remove active request marker files before lifecycle processing.");
    }

    [Fact]
    public void ProcessPlayerTurn_MustKeepValidatedPendingSnapshotContextAcrossLongGmWait()
    {
        var source = ReadGameEnginePartialSource("GameEngine.TurnLifecycle.cs");
        var methodStart = source.IndexOf("private async Task ProcessPlayerTurn(", StringComparison.Ordinal);
        Assert.True(methodStart >= 0, "ProcessPlayerTurn must exist.");
        var nextMethodStart = source.IndexOf("    internal static bool", methodStart, StringComparison.Ordinal);
        Assert.True(nextMethodStart > methodStart, "ProcessPlayerTurn method boundary must be discoverable.");
        var methodSource = source[methodStart..nextMethodStart];

        var writeRequestIndex = methodSource.IndexOf("await _fs.WriteFileAtomicAsync(\"input/turn_request.json\"", StringComparison.Ordinal);
        var snapshotContextIndex = methodSource.IndexOf("activeSnapshotContext = await LoadValidatedPendingTurnSnapshotContextAsync(", StringComparison.Ordinal);
        var waitIndex = methodSource.IndexOf("if (await WaitForTerminalSignalAsync()", StringComparison.Ordinal);

        Assert.True(writeRequestIndex >= 0, "ProcessPlayerTurn must write input/turn_request.json.");
        Assert.True(snapshotContextIndex > writeRequestIndex, "ProcessPlayerTurn must validate pending snapshot after writing the current request.");
        Assert.True(waitIndex > snapshotContextIndex, "ProcessPlayerTurn must keep the validated snapshot context in memory before the long GM wait starts.");
    }

    [Fact]
    public void AcceptedTurnValidation_MustUsePreWaitPendingSnapshotContextForCanonicalBaseline()
    {
        var turnSource = ReadGameEnginePartialSource("GameEngine.TurnLifecycle.cs");
        var validationSource = ReadGameEnginePartialSource("GameEngine.ValidationAndRepair.cs");

        Assert.Contains("ValidateAcceptedTurnOutcomeWithRepairLoopAsync(", turnSource, StringComparison.Ordinal);
        Assert.Contains("activeSnapshotContext,", turnSource, StringComparison.Ordinal);
        Assert.Contains("ValidatedPendingTurnSnapshotContext? activeSnapshotContext,", validationSource, StringComparison.Ordinal);
        Assert.Contains("RefreshAcceptedTurnCanonicalStateForValidationAsync(expectedTurn, activeSnapshotContext)", validationSource, StringComparison.Ordinal);
        Assert.DoesNotContain("var snapshot = await LoadCanonicalBaselineSnapshotAsync(expectedTurn);", validationSource, StringComparison.Ordinal);
    }

    [Fact]
    public void AcceptedTurnValidation_MustExposePreWaitPendingSnapshotContextToValidators()
    {
        var validationSource = ReadGameEnginePartialSource("GameEngine.ValidationAndRepair.cs");
        var serviceSource = File.ReadAllText(Path.Combine(
            TestRepoPaths.RepoRoot,
            "BookOfEternityClient",
            "Services",
            "Validation",
            "ValidationService.AcceptedTurnAndInkFeathers.cs"));

        Assert.Contains("using var pendingSnapshotScope = _validator.UsePrevalidatedPendingTurnSnapshotScope(activeSnapshotContext?.Manifest);", validationSource, StringComparison.Ordinal);
        Assert.Contains("_prevalidatedPendingTurnSnapshotOverride", serviceSource, StringComparison.Ordinal);
        Assert.Contains("if (_prevalidatedPendingTurnSnapshotOverride != null)", serviceSource, StringComparison.Ordinal);
        Assert.Contains("return new ValidatedPendingTurnSnapshotLookup(ValidatedPendingTurnSnapshotStatus.Usable, _prevalidatedPendingTurnSnapshotOverride);", serviceSource, StringComparison.Ordinal);
    }

    [Fact]
    public void CheckLifeTransitions_MustClearAcceptedTurnReadySignalBeforeDispatchingLifeEvaluation()
    {
        var source = ReadGameEnginePartialSource("GameEngine.TurnLifecycle.cs");
        var transitionAnchor = source.IndexOf("private async Task<bool> CheckLifeTransitions", StringComparison.Ordinal);
        Assert.True(transitionAnchor >= 0, "CheckLifeTransitions must exist.");

        var deleteLifeTransitionIndex = source.IndexOf("_fs.DeleteFile(\"game_state/control/life_transitions.json\");", transitionAnchor, StringComparison.Ordinal);
        var clearReadyIndex = source.IndexOf("ClearReadySignals();", transitionAnchor, StringComparison.Ordinal);
        var writeEvaluationRequestIndex = source.IndexOf("await _fs.WriteFileAtomicAsync(\"input/turn_request.json\"", transitionAnchor, StringComparison.Ordinal);

        Assert.True(deleteLifeTransitionIndex >= 0, "CheckLifeTransitions must delete consumed life_transitions.json before dispatching Life Evaluation.");
        Assert.True(clearReadyIndex >= 0, "CheckLifeTransitions must clear stale accepted-turn ready signals before dispatching Life Evaluation.");
        Assert.True(writeEvaluationRequestIndex >= 0, "CheckLifeTransitions must dispatch a Life Evaluation request.");
        Assert.True(
            deleteLifeTransitionIndex < clearReadyIndex && clearReadyIndex < writeEvaluationRequestIndex,
            "Life Evaluation dispatch must remove stale ready/turn_complete.json from the accepted TriggerLifeEnd turn before writing the new input/turn_request.json.");
    }

    [Fact]
    public void CheckLifeTransitions_DeathScreen_MustPublishAgentConsoleKeySnapshot()
    {
        var source = ReadGameEnginePartialSource("GameEngine.TurnLifecycle.cs");
        var transitionAnchor = source.IndexOf("private async Task<bool> CheckLifeTransitions", StringComparison.Ordinal);
        Assert.True(transitionAnchor >= 0, "CheckLifeTransitions must exist.");

        var preDeathCaptureIndex = source.IndexOf("var preDeathInkFeathers", transitionAnchor, StringComparison.Ordinal);
        Assert.True(preDeathCaptureIndex > transitionAnchor, "CheckLifeTransitions must capture pre-death reward state after the death screen.");

        var deathScreenSource = source[transitionAnchor..preDeathCaptureIndex];

        Assert.Contains("ReadAgentConsoleKeyContinuation(", deathScreenSource, StringComparison.Ordinal);
        Assert.DoesNotContain("_inputSource.ReadKey(intercept: true);", deathScreenSource, StringComparison.Ordinal);
    }

    [Fact]
    public void ShowLifeEvaluationRewards_MustPublishAgentConsoleKeySnapshot()
    {
        var source = ReadGameEnginePartialSource("GameEngine.TurnLifecycle.cs");
        var method = ExtractMethodSource(source, "private async Task ShowLifeEvaluationRewards(");

        Assert.Contains("ReadAgentConsoleKeyContinuation(", method, StringComparison.Ordinal);
        Assert.DoesNotContain("_inputSource.ReadKey(intercept: true);", method, StringComparison.Ordinal);
    }

    [Fact]
    public void WaitForGmResponse_MustReturnAfterHandledLifeTransitionBeforeOldResponsePipelineContinues()
    {
        var source = ReadGameEnginePartialSource("GameEngine.TurnLifecycle.cs");
        var waitMethod = ExtractMethodSource(source, "private async Task<bool> WaitForGmResponse()");

        var lifeTransitionIndex = waitMethod.IndexOf("if (await CheckLifeTransitions(snapshotContext))", StringComparison.Ordinal);
        var qteHandlingIndex = waitMethod.IndexOf("var qteHandling = await HandleAcceptedQteOfferAsync(response, snapshotContext);", StringComparison.Ordinal);
        var oldResponseRewriteIndex = waitMethod.IndexOf("_lastResponse = qteHandling.Response;", StringComparison.Ordinal);

        Assert.True(lifeTransitionIndex >= 0, "WaitForGmResponse must branch on handled life transitions.");
        Assert.True(qteHandlingIndex > lifeTransitionIndex, "Handled life transition must be checked before QTE handling of the old Mortal response.");
        Assert.True(oldResponseRewriteIndex > lifeTransitionIndex, "Handled life transition must be checked before _lastResponse can be overwritten by the old Mortal response.");
        Assert.Contains("return true;", waitMethod[lifeTransitionIndex..qteHandlingIndex], StringComparison.Ordinal);
    }

    [Fact]
    public void AcceptedTurnHappyPath_MustNormalizeResolvedRuntimeArtifactsBeforePendingSnapshotCleanup()
    {
        var source = ReadGameEnginePartialSource("GameEngine.TurnLifecycle.cs");
        var acceptedTurnAnchor = source.IndexOf("// Turn accepted — backup no longer needed", StringComparison.Ordinal);
        Assert.True(acceptedTurnAnchor >= 0);

        var normalizeIndex = source.IndexOf("await NormalizeRuntimeUiArtifactsAsync();", acceptedTurnAnchor, StringComparison.Ordinal);
        var cleanupIndex = source.IndexOf("await CleanupPendingTurnSnapshotAsync();", acceptedTurnAnchor, StringComparison.Ordinal);

        Assert.True(normalizeIndex >= 0);
        Assert.True(cleanupIndex >= 0);
        Assert.True(normalizeIndex < cleanupIndex, "resolved afterlife pending contracts must be cleaned while the accepted-turn snapshot is still available.");
    }

    [Fact]
    public void AcceptedTurnFlows_MustValidateMaterializedStateAfterRuntimeNormalization()
    {
        var source = ReadGameEnginePartialSource("GameEngine.TurnLifecycle.cs");

        Assert.Contains("PostAcceptedMaterializedStateValidationSource", source, StringComparison.Ordinal);

        var anchors = new[]
        {
            "private async Task<bool> WaitForGmResponse()",
            "var acceptedLateResponse = false;",
            "// Read and validate the response before accepting the turn"
        };

        foreach (var anchor in anchors)
        {
            var anchorIndex = source.IndexOf(anchor, StringComparison.Ordinal);
            Assert.True(anchorIndex >= 0, $"Accepted-turn flow anchor not found: {anchor}");

            var normalizeIndex = source.IndexOf("await NormalizeRuntimeUiArtifactsAsync();", anchorIndex, StringComparison.Ordinal);
            Assert.True(normalizeIndex >= 0, $"Accepted-turn flow must normalize runtime artifacts after anchor: {anchor}");

            var postValidationIndex = source.IndexOf(
                "await ValidatePostAcceptedMaterializedStateWithRepairLoopAsync(",
                normalizeIndex,
                StringComparison.Ordinal);
            Assert.True(
                postValidationIndex > normalizeIndex,
                $"Accepted-turn flow must validate the fully materialized state after NormalizeRuntimeUiArtifactsAsync before returning control: {anchor}");
        }
    }

    [Fact]
    public void RuntimeUiArtifactNormalizer_MustCoverAfterlifePendingContractFamilies()
    {
        var source = ReadGameEnginePartialSource("GameEngine.SessionAndSnapshots.cs");

        Assert.Contains("await GuardianTradeRequestState.EnsureHealthyAsync(_fs, _stateManager.CurrentState.CurrentRealm);", source, StringComparison.Ordinal);
        Assert.Contains("await AfterlifeArchiveActionState.EnsureHealthyAsync(_fs, _stateManager.CurrentState.CurrentRealm);", source, StringComparison.Ordinal);
        Assert.Contains("await GuardianAbodeResidentRequestState.EnsureHealthyAsync(_fs, _stateManager.CurrentState.CurrentRealm);", source, StringComparison.Ordinal);
        Assert.Contains("await ActorSocialInteractionRequestState.EnsureHealthyAsync(_fs, _stateManager.CurrentState.CurrentRealm);", source, StringComparison.Ordinal);
    }

    [Fact]
    public void CheckLifeTransitions_MustResolveCurrentRealmFromSoulStateFile_NotStaleRuntimeCache()
    {
        var source = ReadGameEngineSource();

        Assert.Contains("var currentSoulStateJson = await _fs.ReadFileAsync(\"game_state/meta/soul_state.json\");", source, StringComparison.Ordinal);
        Assert.Contains("currentSoulStateRoot)", source, StringComparison.Ordinal);
        Assert.Contains("CanonicalStateNormalizer.TryReadStrictCurrentRealm(currentSoulStateJson)", source, StringComparison.Ordinal);
        Assert.DoesNotContain("transJson,\r\n                _stateManager.CurrentState.CurrentRealm", source, StringComparison.Ordinal);
    }

    [Fact]
    public void AcceptedTurnCanonicalBaselineRefresh_MustUseValidatedManifestStructureAndSnapshotHashes()
    {
        var source = ReadGameEngineSource();

        Assert.Contains("PendingTurnSnapshotAuthority.TryValidateManifestForDestructiveAuthority(", source, StringComparison.Ordinal);
        Assert.Contains("await LoadValidatedCurrentPendingTurnSnapshotAuthorityPayloadAsync(manifest)", source, StringComparison.Ordinal);
        Assert.Contains("payload.RollbackBaselineFiles", source, StringComparison.Ordinal);
        Assert.Contains("payload.SnapshotFileHashes.TryGetValue(relativePath, out var expectedSnapshotHash)", source, StringComparison.Ordinal);
    }

    [Fact]
    public void AcceptedTurnCanonicalBaselineRefresh_MustRequireRollbackBaselineCanonicalCoverage()
    {
        var source = ReadGameEngineSource();

        Assert.Contains("PendingTurnSnapshotAuthority.HasValidatedSnapshotCoverage(", source, StringComparison.Ordinal);
        Assert.Contains("var baselineCanonicalFiles = payload.RollbackBaselineFiles", source, StringComparison.Ordinal);
        Assert.Contains("canonicalFiles.Contains(path)", source, StringComparison.Ordinal);
        Assert.Contains("requireRollbackBaselineRegistration: true", source, StringComparison.Ordinal);
        Assert.Contains("TryAddOptionalCanonicalBaselineSnapshotAsync(", source, StringComparison.Ordinal);
        Assert.DoesNotContain("return snapshot.Count >= canonicalFiles.Count ? snapshot : null;", source, StringComparison.Ordinal);
    }

    [Fact]
    public void PendingSnapshotCleanup_AndLateRollback_MustNotTrustRawManifestArtifactPaths()
    {
        var source = ReadGameEngineSource();

        Assert.DoesNotContain("foreach (var snapshotPath in manifest.Files.Values)", source, StringComparison.Ordinal);
        Assert.DoesNotContain("foreach (var rollbackPath in manifest.RollbackBackups.Values)", source, StringComparison.Ordinal);
        Assert.DoesNotContain("GetRollbackSnapshot(manifest)", source, StringComparison.Ordinal);
        Assert.Contains("BuildValidatedRollbackSnapshot(snapshotContext)", source, StringComparison.Ordinal);
        Assert.Contains("Directory.Delete(snapshotDirectoryPath, recursive: true);", source, StringComparison.Ordinal);
    }

    [Fact]
    public void AcceptedTurnRuntimeAuthorityBranches_MustUseValidatedSnapshotContextInsteadOfRawManifestFields()
    {
        var source = ReadGameEngineSource();

        Assert.Contains("LoadValidatedPendingTurnSnapshotContextAsync(manifest)", source, StringComparison.Ordinal);
        Assert.Contains("snapshotContext?.SourceLabel", source, StringComparison.Ordinal);
        Assert.Contains("snapshotContext?.PlayerAction", source, StringComparison.Ordinal);
        Assert.DoesNotContain("manifest?.SourceLabel", source, StringComparison.Ordinal);
        Assert.DoesNotContain("manifest?.PlayerAction", source, StringComparison.Ordinal);
    }

    [Fact]
    public void TransitionWaitForGmResponse_MustRunAcceptedTurnFinalizerSteps()
    {
        var source = ReadGameEnginePartialSource("GameEngine.TurnLifecycle.cs");
        var waitStart = source.IndexOf("private async Task<bool> WaitForGmResponse()", StringComparison.Ordinal);
        var rawWaitStart = source.IndexOf("private async Task<bool> WaitForGmResponseRaw()", StringComparison.Ordinal);
        Assert.True(waitStart >= 0);
        Assert.True(rawWaitStart > waitStart);
        var waitForGmResponseSource = source[waitStart..rawWaitStart];

        Assert.Contains("CleanupBackup(rollbackSnapshot!)", waitForGmResponseSource, StringComparison.Ordinal);
        Assert.Contains("CleanupAfterAcceptedChaosSeaMarkerTurn(snapshotContext?.PlayerAction)", waitForGmResponseSource, StringComparison.Ordinal);
        Assert.Contains("await _pendingTurnState.RotateAfterAcceptedTurnAsync()", waitForGmResponseSource, StringComparison.Ordinal);
        Assert.Contains("await NormalizeRuntimeUiArtifactsAsync()", waitForGmResponseSource, StringComparison.Ordinal);
        Assert.Contains("await _storyService.AppendTurnAsync(", waitForGmResponseSource, StringComparison.Ordinal);
        Assert.Contains("await ProcessMortalProgressionAfterAcceptedTurnAsync()", waitForGmResponseSource, StringComparison.Ordinal);
        Assert.Contains("await _saveLoad.AutosaveAsync(_gameLoop.TurnNumber)", waitForGmResponseSource, StringComparison.Ordinal);
    }

    [Fact]
    public void ValidationRepairFlow_MustUseValidatedPendingSnapshotContext_ForRepairCorrelationAndMetadata()
    {
        var source = ReadGameEnginePartialSource("GameEngine.ValidationAndRepair.cs");

        Assert.Contains("ResolveActivePendingTurnSnapshotContextAsync()", source, StringComparison.Ordinal);
        Assert.Contains("IsMatchingRepairReady(ready, pendingSnapshot.Context)", source, StringComparison.Ordinal);
        Assert.Contains("BuildProtocolRequestMetadata(pendingSnapshot)", source, StringComparison.Ordinal);
        Assert.Contains("BuildValidationRepairRequestInstructions(pendingSnapshot)", source, StringComparison.Ordinal);
        Assert.Contains("BuildProtocolRequestMetadataWarning(pendingSnapshot)", source, StringComparison.Ordinal);
        Assert.Contains("BuildInvalidRepairReadyRepairHint(pendingSnapshot)", source, StringComparison.Ordinal);
        Assert.Contains("BuildMismatchedRepairReadyRepairHint(pendingSnapshot)", source, StringComparison.Ordinal);
        Assert.DoesNotContain("IsMatchingRepairReady(ready, manifest)", source, StringComparison.Ordinal);
        Assert.DoesNotContain("manifest?.SessionId ?? existingRequest?.SessionId", source, StringComparison.Ordinal);
        Assert.DoesNotContain("manifest?.RequestId ?? existingRequest?.RequestId", source, StringComparison.Ordinal);
        Assert.DoesNotContain("manifest?.TurnNumber ?? existingRequest?.TurnNumber", source, StringComparison.Ordinal);
        Assert.DoesNotContain("snapshotContext?.SessionId ?? existingRequest?.SessionId", source, StringComparison.Ordinal);
        Assert.DoesNotContain("snapshotContext?.RequestId ?? existingRequest?.RequestId", source, StringComparison.Ordinal);
        Assert.DoesNotContain("snapshotContext?.TurnNumber ?? existingRequest?.TurnNumber", source, StringComparison.Ordinal);
        Assert.DoesNotContain("snapshotContext?.SessionId ?? _gameLoop.SessionId", source, StringComparison.Ordinal);
        Assert.DoesNotContain("snapshotContext?.TurnNumber ?? (_gameLoop.TurnNumber + 1)", source, StringComparison.Ordinal);
        Assert.Contains("PendingTurnSnapshotResolutionStatus.Missing =>", source, StringComparison.Ordinal);
        Assert.Contains("PendingTurnSnapshotResolutionStatus.Unusable =>", source, StringComparison.Ordinal);
        Assert.Contains("Не копируй sentinel metadata из текущего validation_repair_request.json.", source, StringComparison.Ordinal);
        Assert.Contains("Текущий validation_repair_request.json использует diagnostic-only sentinel metadata.", source, StringComparison.Ordinal);
        Assert.DoesNotContain(
            "\"Пересоздай validation_repair_ready.json и скопируй sessionId/requestId/turnNumber ровно из validation_repair_request.json.\");",
            source,
            StringComparison.Ordinal);
    }

    [Fact]
    public void DaemonRepairAndTerminalPrompts_MustDescribeDiagnosticOnlySentinelMetadata()
    {
        var source = ReadGameMasterDaemonSource();

        Assert.Contains("function Test-ProtocolRequestUsesDiagnosticOnlyMetadata", source, StringComparison.Ordinal);
        Assert.Contains("$RequestObject.metadataDiagnosticOnly", source, StringComparison.Ordinal);
        Assert.Contains("diagnostic-only sentinel values", source, StringComparison.Ordinal);
        Assert.Contains("Do NOT copy those sentinel metadata into", source, StringComparison.Ordinal);
        Assert.Contains("Do NOT treat them as authoritative correlation metadata", source, StringComparison.Ordinal);
        Assert.Contains("Legacy fallback for requests written before metadataDiagnosticOnly was added", source, StringComparison.Ordinal);
        Assert.DoesNotContain("$repair.gmInstructions -and $repair.gmInstructions.Contains(\"служат только для диагностики\")", source, StringComparison.Ordinal);
        Assert.DoesNotContain("$failure.gmInstructions -and $failure.gmInstructions.Contains(\"служат только для диагностики\")", source, StringComparison.Ordinal);
        Assert.DoesNotContain("with matching sessionId/requestId/turnNumber copied from the CURRENT repair request. If your ready file is malformed or mismatched", source, StringComparison.Ordinal);
    }

    [Fact]
    public void DaemonRepairPrompt_MustKeepCodexGmFocusedOnTerminalRepairMarker()
    {
        var source = ReadGameMasterDaemonSource();

        Assert.Contains("REPAIR MODE", source, StringComparison.Ordinal);
        Assert.Contains("Do NOT run unrelated git or repository tasks", source, StringComparison.Ordinal);
        Assert.Contains("Do NOT wait for another prompt after files are fixed", source, StringComparison.Ordinal);
        Assert.Contains("as the LAST action", source, StringComparison.Ordinal);
        Assert.Contains("If the files already satisfy the listed errors", source, StringComparison.Ordinal);
        Assert.Contains("weather_direct_state_missing_required_fields", source, StringComparison.Ordinal);
        Assert.Contains("never write ready/turn_complete.json for repair", source, StringComparison.Ordinal);
    }

    [Fact]
    public void DaemonInitialTurnPrompt_MustPreemptWeatherDirectRootRepairLoop()
    {
        var source = ReadGameMasterDaemonSource();

        Assert.Contains("Weather contract:", source, StringComparison.Ordinal);
        Assert.Contains("game_state/world/weather.json direct root", source, StringComparison.Ordinal);
        Assert.Contains("both non-empty description and canonical tendency", source, StringComparison.Ordinal);
        Assert.Contains("Do not wait for weather_direct_state_missing_required_fields repair", source, StringComparison.Ordinal);
    }

    [Fact]
    public void RepairAndTerminalRequestWriters_MustEmitStructuredDiagnosticOnlyMetadataFlag()
    {
        var source = ReadGameEngineSource();

        Assert.Contains("public bool MetadataDiagnosticOnly { get; set; }", source, StringComparison.Ordinal);
        Assert.Contains("var metadataDiagnosticOnly = BuildProtocolRequestMetadataDiagnosticOnly(pendingSnapshot);", source, StringComparison.Ordinal);
        Assert.Contains("MetadataDiagnosticOnly = metadataDiagnosticOnly,", source, StringComparison.Ordinal);
        Assert.Contains("return pendingSnapshot.Status is PendingTurnSnapshotResolutionStatus.Missing or PendingTurnSnapshotResolutionStatus.Unusable;", source, StringComparison.Ordinal);
    }

    [Fact]
    public void DiagnosticOnlyValidationRepair_MustFailClosedWithoutWaitingForGmReady()
    {
        var source = ReadGameEnginePartialSource("GameEngine.ValidationAndRepair.cs");
        var method = ExtractMethodSource(source, "private async Task<bool> WaitForContractRepairAsync(");

        var writeRequestIndex = method.IndexOf("await WriteValidationRepairRequestAsync(", StringComparison.Ordinal);
        var diagnosticGuardIndex = method.IndexOf("FailClosedDiagnosticOnlyValidationRepairAsync(", StringComparison.Ordinal);
        var waitLoopIndex = method.IndexOf("while (true)", StringComparison.Ordinal);

        Assert.True(writeRequestIndex >= 0, "WaitForContractRepairAsync must write the client-authored repair request first.");
        Assert.True(diagnosticGuardIndex > writeRequestIndex, "Diagnostic-only repair handling must inspect the freshly written request.");
        Assert.True(waitLoopIndex > diagnosticGuardIndex, "Diagnostic-only repair must fail closed before waiting for validation_repair_ready.json.");
        Assert.Contains("ValidationDiagnosticFailureReportPath", source, StringComparison.Ordinal);
        Assert.Contains("Diagnostic-only validation repair request cannot be completed by GM", source, StringComparison.Ordinal);
        Assert.Contains("await DeleteValidationRepairFilesAsync();", source, StringComparison.Ordinal);
    }

    [Fact]
    public void DiagnosticOnlyValidationRepair_MustPreserveFailureReportAfterRollback()
    {
        var source = ReadGameEnginePartialSource("GameEngine.ValidationAndRepair.cs");
        var method = ExtractMethodSource(source, "private async Task<bool> FailClosedDiagnosticOnlyValidationRepairAsync(");

        var rollbackIndex = method.IndexOf("await RestorePreTurnBackup(rollbackSnapshot!);", StringComparison.Ordinal);
        var reportWriteIndex = method.IndexOf("await _fs.WriteFileAtomicAsync(ValidationDiagnosticFailureReportPath", StringComparison.Ordinal);
        var cleanupIndex = method.IndexOf("await DeleteValidationRepairFilesAsync();", StringComparison.Ordinal);

        Assert.True(rollbackIndex >= 0, "Diagnostic-only fail-closed path should use rollback when available.");
        Assert.True(reportWriteIndex > rollbackIndex, "Diagnostic failure report must be written after rollback so the backup restore cannot erase it.");
        Assert.True(cleanupIndex > reportWriteIndex, "Repair cleanup must not run before the preserved diagnostic failure report is written.");
    }

    [Fact]
    public void ContractValidationErrorScreen_MustExposeAgentConsoleKeyContinuation()
    {
        var source = ReadGameEnginePartialSource("GameEngine.ValidationAndRepair.cs");
        var method = ExtractMethodSource(source, "private void ShowContractValidationErrors(");

        Assert.Contains("AgentConsoleLiveInputSource", method, StringComparison.Ordinal);
        Assert.Contains("Mode = AgentConsoleMode.Error", method, StringComparison.Ordinal);
        Assert.Contains("InputKind = AgentConsoleInputKind.Key", method, StringComparison.Ordinal);
        Assert.Contains("new AgentConsoleAction", method, StringComparison.Ordinal);
        Assert.Contains("Shortcut = \"enter\"", method, StringComparison.Ordinal);
    }

    [Fact]
    public void ContinueFlow_MustNotHydrateFromRawPendingManifestPresence()
    {
        var source = ReadGameEnginePartialSource("GameEngine.MainMenu.cs");

        var method = ExtractMethodSource(source, "private async Task ContinueCurrentSessionFlow()");

        Assert.Contains("var restoredResponse = await BuildGameResponseFromFiles();", method, StringComparison.Ordinal);
        Assert.Contains("_lastResponse = MergeWithLastResponse(restoredResponse);", method, StringComparison.Ordinal);
        Assert.DoesNotContain("pendingSnapshot.Status == PendingTurnSnapshotResolutionStatus.Usable || hasPendingTerminalSignal", method, StringComparison.Ordinal);
        Assert.DoesNotContain("pendingManifest != null || _fs.FileExists(\"ready/turn_complete.json\")", source, StringComparison.Ordinal);
    }

    [Fact]
    public void OrdinaryReturnToChaosSea_MustResetEnlightenmentAndPreserveInkFeathers()
    {
        var source = ReadGameEnginePartialSource("GameEngine.MainMenu.cs");

        Assert.Contains("soulRoot[\"currentRealm\"] = \"Chaos Sea\";", source, StringComparison.Ordinal);
        Assert.Contains("soulRoot[\"enlightenment\"] = CreateNewCycleEnlightenmentResetObject();", source, StringComparison.Ordinal);
        Assert.Contains("soulRoot[\"soulProgression\"] = CreateNewCycleSoulProgressionResetObject();", source, StringComparison.Ordinal);
        Assert.Contains("[\"inkFeathersPreserved\"] = true", source, StringComparison.Ordinal);
        Assert.DoesNotContain("[\"inkFeathers\"] = new { current = 0, total = 0 }", source, StringComparison.Ordinal);
    }

    [Fact]
    public void OrdinaryReturnToChaosSea_MustBlockInsteadOfPurgingPendingShiningRequests()
    {
        var source = ReadGameEnginePartialSource("GameEngine.MainMenu.cs");

        Assert.Contains("GetBlockingShiningPendingContractPathsAsync()", source, StringComparison.Ordinal);
        Assert.DoesNotContain("ShiningCoreActionRequestState.ClearRequests(_fs);\r\n        ShiningTradeRequestState.ClearRequests(_fs);\r\n        ShiningFactionRequestState.ClearAllRequests(_fs);", source, StringComparison.Ordinal);
    }

    [Fact]
    public void RuntimeUiArtifactNormalizer_MustEnsureShiningFactionRequestsHealthy()
    {
        var source = ReadGameEnginePartialSource("GameEngine.SessionAndSnapshots.cs");

        Assert.Contains("await ShiningFactionRequestState.EnsureHealthyAsync(_fs, _stateManager.CurrentState.CurrentRealm);", source, StringComparison.Ordinal);
    }

    [Fact]
    public void ShiningReturnCycleSync_MustPersistNormalizationChangesEvenWithoutCycleBump()
    {
        var source = ReadGameEnginePartialSource("GameEngine.MainMenu.cs");

        Assert.Contains("var preNormalizationShiningRoot = shiningRoot.DeepClone() as JsonObject;", source, StringComparison.Ordinal);
        Assert.Contains("var stateChanged = preNormalizationShiningRoot != null && !JsonNode.DeepEquals(preNormalizationShiningRoot, shiningRoot);", source, StringComparison.Ordinal);
    }

    [Fact]
    public void OrdinaryReturnToChaosSea_MustShowBeforeAfterPreviewBeforeLocalWrite()
    {
        var source = ReadGameEnginePartialSource("GameEngine.MainMenu.cs");

        Assert.Contains("ConfirmOrdinaryReturnToChaosSeaFromShiningAbodeAsync", source, StringComparison.Ordinal);
        Assert.Contains("\"return_to_chaos_sea\"", source, StringComparison.Ordinal);
        Assert.Contains("\"blockersChecked\"", source, StringComparison.Ordinal);
        Assert.Contains("\"affectedFiles\"", source, StringComparison.Ordinal);
        Assert.Contains("\"soulCurrentRealm\"", source, StringComparison.Ordinal);
        Assert.Contains("\"shiningAvailability\"", source, StringComparison.Ordinal);
        Assert.Contains("GetBlockingShiningPendingContractPathsCoreAsync(deleteEmptyFiles: false)", source, StringComparison.Ordinal);
        Assert.Contains("Подтвердить локальный выход в Море Хаоса", source, StringComparison.Ordinal);
    }

    [Fact]
    public void ReenterShiningAbode_MustPreviewReturnCycleAndAutoTradeRefreshSideEffects()
    {
        var source = ReadGameEnginePartialSource("GameEngine.MainMenu.cs");

        Assert.Contains("BuildShiningReentrySideEffectPreviewAsync", source, StringComparison.Ordinal);
        Assert.Contains("\"returnCycleSync\"", source, StringComparison.Ordinal);
        Assert.Contains("\"autoTradeRefresh\"", source, StringComparison.Ordinal);
        Assert.Contains("autoTradeCreatesPending", source, StringComparison.Ordinal);
        Assert.Contains("autoTradeChangesPendingFile", source, StringComparison.Ordinal);
        Assert.Contains("autoTradeCleanupOnly", source, StringComparison.Ordinal);
        Assert.Contains("\"pendingFileWouldChange\"", source, StringComparison.Ordinal);
        Assert.Contains("\"createsPendingGmContract\"", source, StringComparison.Ordinal);
        Assert.Contains("\"cleanupOnly\"", source, StringComparison.Ordinal);
        Assert.Contains("\"currentReturnCycleIdBefore\"", source, StringComparison.Ordinal);
        Assert.Contains("\"currentReturnCycleIdAfter\"", source, StringComparison.Ordinal);
        Assert.Contains("\"chargesUsedThisReturnBefore\"", source, StringComparison.Ordinal);
        Assert.Contains("\"chargesUsedThisReturnAfter\"", source, StringComparison.Ordinal);
        Assert.Contains("ShiningTradeRequestState.PendingRequestsPath", source, StringComparison.Ordinal);
        Assert.Contains("PreviewAutoRefreshRequestsForCurrentCycleAsync", source, StringComparison.Ordinal);
        Assert.Contains("client-owned auto refresh создаст/обновит", source, StringComparison.Ordinal);
        Assert.Contains("client-owned auto refresh только очистит/обновит", source, StringComparison.Ordinal);
        Assert.Contains("не создаст новый GM closure contract", source, StringComparison.Ordinal);
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
    public void AcceptedTurnRepairLoop_MustUseSnapshotBackedCanonicalRefresh_AndRuntimeViewRefresh()
    {
        var source = ReadGameEngineSource();

        Assert.Contains("RefreshAcceptedTurnCanonicalStateForValidationAsync", source, StringComparison.Ordinal);
        Assert.Equal(1, source.Split("RefreshCanonicalStateAsync(snapshot)", StringSplitOptions.None).Length - 1);
        Assert.DoesNotContain("await RefreshCanonicalStateAsync();", source, StringComparison.Ordinal);
        Assert.Contains("private async Task RefreshRuntimeStateAsync()", source, StringComparison.Ordinal);
        Assert.Contains("await RefreshRuntimeStateAsync();", source, StringComparison.Ordinal);
        Assert.Contains("RefreshCanonicalStateAsync(IReadOnlyDictionary<string, string> backups)", source, StringComparison.Ordinal);
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
    public void TurnReminder_MortalWorldMustSurfaceGuardianQuestProgressException()
    {
        var source = ReadGameEnginePartialSource("GameEngine.TurnLifecycle.cs");

        Assert.Contains("guardianQuestProgressUpdates", source, StringComparison.Ordinal);
        Assert.Contains("ready_to_turn_in", source, StringComparison.Ordinal);
        Assert.Contains("non-physical echo/memory/imprint/resonance", source, StringComparison.Ordinal);
        Assert.Contains("does NOT receive a physical mortal inventory item", source, StringComparison.Ordinal);
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
    public void LifeEvaluationRewardScreen_MustNotFallbackToPermissiveSoulRelicParsingAfterStrictDeltaFailure()
    {
        var source = ReadGameEngineSource();

        Assert.DoesNotContain("else if (soulJson != null)", source, StringComparison.Ordinal);
    }

    [Fact]
    public void NewGamePlus_MustNotReadOrRehydrateLegacyCrossIncarnationDataIntoCanonicalSoulState()
    {
        var source = ReadGameEngineSource();

        Assert.DoesNotContain("crossIncarnationData", source, StringComparison.Ordinal);
        Assert.DoesNotContain("resetSoulState[\"crossIncarnationData\"] = crossIncarnationData;", source, StringComparison.Ordinal);
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
