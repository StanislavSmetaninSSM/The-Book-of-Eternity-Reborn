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
    }

    [Fact]
    public void NewGamePlus_MustBackupAndRestoreGameSessionBeforeDestructiveReset()
    {
        var source = ReadGameEnginePartialSource("GameEngine.MainMenu.cs");

        Assert.Contains("CreateGameSessionSafetyBackup(\"new-game-plus\")", source, StringComparison.Ordinal);
        Assert.Contains("RestoreGameSessionSafetyBackup(backupPath)", source, StringComparison.Ordinal);
        Assert.Contains("CleanupGameSessionSafetyBackup(backupPath)", source, StringComparison.Ordinal);
        Assert.Contains("_fs.ClearGameState();", source, StringComparison.Ordinal);
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

        var lifeTransitionIndex = source.IndexOf("await CheckLifeTransitions();", acceptedTurnAnchor, StringComparison.Ordinal);
        var cleanupIndex = source.IndexOf("await CleanupPendingTurnSnapshotAsync();", acceptedTurnAnchor, StringComparison.Ordinal);

        Assert.True(lifeTransitionIndex >= 0);
        Assert.True(cleanupIndex >= 0);
        Assert.True(lifeTransitionIndex < cleanupIndex, "accepted-turn pending snapshot cleanup must happen after CheckLifeTransitions()");
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
        Assert.Contains("currentSoulStateRoot);", source, StringComparison.Ordinal);
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
    public void AcceptedTurnCanonicalBaselineRefresh_MustRequireFullCanonicalCoverage()
    {
        var source = ReadGameEngineSource();

        Assert.Contains("PendingTurnSnapshotAuthority.HasValidatedSnapshotCoverage(", source, StringComparison.Ordinal);
        Assert.DoesNotContain("if (!rollbackBaselineFiles.Contains(relativePath))", source, StringComparison.Ordinal);
        Assert.Contains("return snapshot.Count == canonicalFiles.Count ? snapshot : null;", source, StringComparison.Ordinal);
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
    public void RepairAndTerminalRequestWriters_MustEmitStructuredDiagnosticOnlyMetadataFlag()
    {
        var source = ReadGameEngineSource();

        Assert.Contains("public bool MetadataDiagnosticOnly { get; set; }", source, StringComparison.Ordinal);
        Assert.Contains("var metadataDiagnosticOnly = BuildProtocolRequestMetadataDiagnosticOnly(pendingSnapshot);", source, StringComparison.Ordinal);
        Assert.Contains("MetadataDiagnosticOnly = metadataDiagnosticOnly,", source, StringComparison.Ordinal);
        Assert.Contains("return pendingSnapshot.Status is PendingTurnSnapshotResolutionStatus.Missing or PendingTurnSnapshotResolutionStatus.Unusable;", source, StringComparison.Ordinal);
    }

    [Fact]
    public void ContinueFlow_MustNotHydrateFromRawPendingManifestPresence()
    {
        var source = ReadGameEnginePartialSource("GameEngine.MainMenu.cs");

        Assert.Contains("var pendingSnapshot = await ResolveActivePendingTurnSnapshotContextAsync();", source, StringComparison.Ordinal);
        Assert.Contains("pendingSnapshot.Status == PendingTurnSnapshotResolutionStatus.Usable || hasPendingTerminalSignal", source, StringComparison.Ordinal);
        Assert.DoesNotContain("pendingManifest != null || _fs.FileExists(\"ready/turn_complete.json\")", source, StringComparison.Ordinal);
    }

    [Fact]
    public void OrdinaryReturnToChaosSea_MustNotResetEnlightenment()
    {
        var source = ReadGameEnginePartialSource("GameEngine.MainMenu.cs");

        Assert.Contains("soulRoot[\"currentRealm\"] = \"Chaos Sea\";", source, StringComparison.Ordinal);
        Assert.DoesNotContain("var enlightenment = soulRoot[\"enlightenment\"] as JsonObject ?? new JsonObject();", source, StringComparison.Ordinal);
        Assert.DoesNotContain("enlightenment[\"currentTier\"] = \"Новичок\";", source, StringComparison.Ordinal);
        Assert.DoesNotContain("enlightenment[\"experience\"] = 0;", source, StringComparison.Ordinal);
        Assert.DoesNotContain("enlightenment[\"level\"] = 0;", source, StringComparison.Ordinal);
        Assert.DoesNotContain("enlightenment[\"progressPercent\"] = 0;", source, StringComparison.Ordinal);
    }

    [Fact]
    public void OrdinaryReturnToChaosSea_MustPurgePendingShiningRequests()
    {
        var source = ReadGameEnginePartialSource("GameEngine.MainMenu.cs");

        Assert.Contains("ShiningCoreActionRequestState.ClearRequests(_fs);", source, StringComparison.Ordinal);
        Assert.Contains("ShiningTradeRequestState.ClearRequests(_fs);", source, StringComparison.Ordinal);
        Assert.Contains("ShiningFactionRequestState.ClearAllRequests(_fs);", source, StringComparison.Ordinal);
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
