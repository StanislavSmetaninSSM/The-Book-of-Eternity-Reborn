using Xunit;

namespace BookOfEternityClient.Tests;

public sealed class GmBridgeDiagnosticsContractTests
{
    [Fact]
    public void BridgeHost_ExposesBoundedDiagnosticsCommand()
    {
        var source = ReadRepoFile("BookOfEternityGMBridge/Program.cs");

        Assert.Contains("case \"diagnostics\":", source, StringComparison.Ordinal);
        Assert.Contains("BridgeDiagnostics", source, StringComparison.Ordinal);
        Assert.Contains("RecentOutputTail", source, StringComparison.Ordinal);
        Assert.Contains("ReadVisibleConsoleText()", source, StringComparison.Ordinal);
    }

    [Fact]
    public void BridgeHost_PromptVisibilityFailurePersistsLastError()
    {
        var source = ReadRepoFile("BookOfEternityGMBridge/Program.cs");

        Assert.Contains("FailWithLastError", source, StringComparison.Ordinal);
        Assert.Contains("Prompt text was pasted into the PTY", source, StringComparison.Ordinal);
    }

    [Fact]
    public void BridgeHost_PromptVisibilityFailureMarksBridgeNotReady()
    {
        var source = ReadRepoFile("BookOfEternityGMBridge/Program.cs");

        Assert.Contains("_status.Ready = false;", source, StringComparison.Ordinal);
        Assert.Contains("_status.State = \"DispatchFailed\";", source, StringComparison.Ordinal);
        Assert.Contains("!string.Equals(_status.State, \"DispatchFailed\", StringComparison.Ordinal)", source, StringComparison.Ordinal);
    }

    [Fact]
    public void BridgeHost_UsesConfiguredPromptVisibilityTimeout()
    {
        var source = ReadRepoFile("BookOfEternityGMBridge/Program.cs");

        Assert.Contains("GmBridgePromptVisibilityTimeoutSeconds", source, StringComparison.Ordinal);
        Assert.Contains("TimeSpan.FromSeconds(visibilitySettings.GmBridgePromptVisibilityTimeoutSeconds)", source, StringComparison.Ordinal);
    }

    [Fact]
    public void BridgeHost_DispatchRequiresFreshPasteAndSubmissionEvidence()
    {
        var source = ReadRepoFile("BookOfEternityGMBridge/BridgeHost.PromptDispatch.cs");
        Assert.Contains("_outputVersion > afterVersion", source, StringComparison.Ordinal);
        Assert.Contains("PromptDeliveryDisposition.SubmissionObserved", source, StringComparison.Ordinal);
        Assert.Contains("PromptDeliveryDisposition.UnknownOutcome", source, StringComparison.Ordinal);
        Assert.True(source.IndexOf("operation.Phase = PromptDeliveryPhase.SubmitStarted", StringComparison.Ordinal) < source.IndexOf("profile.SubmitSequence, false, token", StringComparison.Ordinal));
    }

    [Fact]
    public void LauncherScript_ExposesDiagnosticsCommand()
    {
        var source = ReadRepoFile("BookOfEternityClient/Launcher/bookofeternity.ps1");

        Assert.Contains("\"diagnostics\"", source, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("command = \"diagnostics\"", source, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void LauncherScript_ThrowsWhenBridgeReturnsFailureResponse()
    {
        var source = ReadRepoFile("BookOfEternityClient/Launcher/bookofeternity.ps1");

        Assert.Contains("function Assert-BridgeResponseOk", source, StringComparison.Ordinal);
        Assert.Contains("if ($null -ne $Response.ok -and -not [bool]$Response.ok)", source, StringComparison.Ordinal);
        Assert.Contains("throw \"GM bridge request failed:", source, StringComparison.Ordinal);
        Assert.Contains("Assert-BridgeResponseOk -Response $response", source, StringComparison.Ordinal);
    }

    [Fact]
    public void BridgeHost_ResolvesRepoRootFromProcessContextInsteadOfSessionParent()
    {
        var source = ReadRepoFile("BookOfEternityGMBridge/Program.cs");

        Assert.Contains("ResolveRepoRoot", source, StringComparison.Ordinal);
        Assert.DoesNotContain("_repoRoot = Directory.GetParent(_clientRoot)?.FullName ?? _clientRoot;", source, StringComparison.Ordinal);
    }

    [Fact]
    public void BridgeHost_DefaultShellWorkingDirectoryUsesGameSessionIsolation()
    {
        var source = ReadRepoFile("BookOfEternityGMBridge/Program.cs");

        Assert.Contains("ResolveGmBridgeShellWorkingDirectory", source, StringComparison.Ordinal);
        Assert.Contains("config.GmBridgeShellWorkingDirectory", source, StringComparison.Ordinal);
        Assert.Contains("_status.ShellWorkingDirectory = workingDirectory;", source, StringComparison.Ordinal);
        Assert.Contains("public string ShellWorkingDirectory { get; set; } = string.Empty;", source, StringComparison.Ordinal);
        Assert.DoesNotContain("var workingDirectory = Directory.Exists(_repoRoot)", source, StringComparison.Ordinal);
    }

    [Fact]
    public void BridgeStatus_TracksLastPromptDispatchTiming()
    {
        var source = ReadRepoFile("BookOfEternityGMBridge/Program.cs");

        Assert.Contains("LastPromptDispatchState", source, StringComparison.Ordinal);
        Assert.Contains("LastPromptDispatchStartedAtUtc", source, StringComparison.Ordinal);
        Assert.Contains("LastPromptDispatchCompletedAtUtc", source, StringComparison.Ordinal);
        Assert.Contains("LastPromptDispatchElapsedMs", source, StringComparison.Ordinal);
        Assert.Contains("Stopwatch.StartNew()", source, StringComparison.Ordinal);
    }

    [Fact]
    public void BridgeStatus_ExposesConfiguredWorkerStatuses()
    {
        var source = ReadRepoFile("BookOfEternityGMBridge/Program.cs");

        Assert.Contains("WorkerStatuses", source, StringComparison.Ordinal);
        Assert.Contains("GmWorkerBridgePool.BuildInitialStatuses", source, StringComparison.Ordinal);
        Assert.Contains("GmWorkerBridgeProfiles", source, StringComparison.Ordinal);
    }

    [Fact]
    public void BridgeDiagnostics_ExposeWorkerProposalInbox()
    {
        var source = ReadRepoFile("BookOfEternityGMBridge/Program.cs");

        Assert.Contains("WorkerProposalInbox", source, StringComparison.Ordinal);
        Assert.Contains("GmWorkerProposalInboxService", source, StringComparison.Ordinal);
        Assert.Contains("ListAsync", source, StringComparison.Ordinal);
    }

    [Fact]
    public void BridgeHost_ExposesProposalOnlyWorkerDispatchCommand()
    {
        var source = ReadRepoFile("BookOfEternityGMBridge/Program.cs");

        Assert.Contains("case \"dispatchworkertask\":", source, StringComparison.Ordinal);
        Assert.Contains("GmWorkerProposalOnlyDispatchService", source, StringComparison.Ordinal);
        Assert.Contains("WorkerDispatch", source, StringComparison.Ordinal);
    }

    [Fact]
    public void BridgeHost_PreservesPendingCliDraftBeforeAutomaticDispatch()
    {
        var source = ReadRepoFile("BookOfEternityGMBridge/Program.cs");
        Assert.DoesNotContain("ClearPendingInputBeforePromptDispatchAsync", source, StringComparison.Ordinal);
        Assert.Contains("return await DispatchPromptAsync(request);", source, StringComparison.Ordinal);
    }

    [Fact]
    public void BridgeHost_DispatchUsesPositiveProfileAndEmptyComposer()
    {
        var source = ReadRepoFile("BookOfEternityGMBridge/BridgeHost.PromptDispatch.cs");
        Assert.Contains("IsEmptyIdleView(operation.Snapshot.Profile, _promptScreenReader())", source, StringComparison.Ordinal);
        Assert.True(source.IndexOf("IsEmptyIdleView(operation.Snapshot.Profile", StringComparison.Ordinal) < source.IndexOf("operation.Phase = PromptDeliveryPhase.PasteStarted", StringComparison.Ordinal));
    }

    [Fact]
    public void BridgeHost_ManualReadyCannotClearUncertainDelivery()
    {
        var source = ReadRepoFile("BookOfEternityGMBridge/Program.cs");
        var ready = source[source.IndexOf("private BridgeResponse SetReady", StringComparison.Ordinal)..source.IndexOf("private void EnsureShellAlive", StringComparison.Ordinal)];
        Assert.Contains("_automaticInputPaused", ready, StringComparison.Ordinal);
        Assert.Contains("IsEmptyIdleView", ready, StringComparison.Ordinal);
        Assert.DoesNotContain("_automaticInputPaused = false", ready, StringComparison.Ordinal);
    }

    [Fact]
    public void BridgeHost_ActiveOperationCannotBeMarkedReady()
    {
        var source = ReadRepoFile("BookOfEternityGMBridge/Program.cs");
        Assert.Contains("_admittedPrompts != 0", source, StringComparison.Ordinal);
        Assert.Contains("_admittedPrompts == 0", source, StringComparison.Ordinal);
    }

    [Fact]
    public void BridgeHost_IdleObservationCannotClearUncertainDelivery()
    {
        var source = ReadRepoFile("BookOfEternityGMBridge/Program.cs");
        var refresh = source[source.IndexOf("private Task RefreshBridgeAutomationStateAsync", StringComparison.Ordinal)..source.IndexOf("private CliPromptReadiness", StringComparison.Ordinal)];
        Assert.Contains("!_automaticInputPaused", refresh, StringComparison.Ordinal);
        Assert.DoesNotContain("RefreshDispatchFailureRecovery", refresh, StringComparison.Ordinal);
    }

    [Fact]
    public void BridgeHost_DoesNotAutoAcceptTrustPrompt()
    {
        var source = ReadRepoFile("BookOfEternityGMBridge/Program.cs");
        Assert.DoesNotContain("AutoAcceptTrustedCodexWorkingDirectoryTrustPromptAsync", source, StringComparison.Ordinal);
    }

    [Fact]
    public void BridgeHost_DoesNotAutoAnswerUpdatePrompt()
    {
        var source = ReadRepoFile("BookOfEternityGMBridge/Program.cs");
        Assert.DoesNotContain("AutoSkipCodexUpdatePromptAsync", source, StringComparison.Ordinal);
    }

    [Fact]
    public void BridgeHost_AutoReadyRequiresSupportedEmptyIdleView()
    {
        var source = ReadRepoFile("BookOfEternityGMBridge/Program.cs");
        var refresh = source[source.IndexOf("private Task RefreshBridgeAutomationStateAsync", StringComparison.Ordinal)..source.IndexOf("private CliPromptReadiness", StringComparison.Ordinal)];
        Assert.Contains("GmCliInputProfile.Snapshot()", refresh, StringComparison.Ordinal);
        Assert.Contains("IsEmptyIdleView", refresh, StringComparison.Ordinal);
    }

    [Fact]
    public void BridgeHost_ManualTakeoverPrecedesCompetingWrite()
    {
        var source = ReadRepoFile("BookOfEternityGMBridge/BridgeHost.PromptDispatch.cs");
        var manual = source[source.IndexOf("private async Task WriteManualInputAsync", StringComparison.Ordinal)..];
        Assert.True(manual.IndexOf("TakeManualInput(input)", StringComparison.Ordinal) < manual.IndexOf("await WriteExclusiveInputAsync", StringComparison.Ordinal));
    }

    [Fact]
    public void BridgeHost_StatusAndCancelReachRealPromptOperation()
    {
        var source = ReadRepoFile("BookOfEternityGMBridge/Program.cs");
        Assert.Contains("case \"promptstatus\":", source, StringComparison.Ordinal);
        Assert.Contains("case \"cancelprompt\":", source, StringComparison.Ordinal);
        Assert.Contains("return QueryPrompt(request, true);", source, StringComparison.Ordinal);
    }

    [Fact]
    public void BridgeHost_TreatsCompletedCodexTurnPromptAsIdleEvenWhenHeaderScrolledAway()
    {
        var source = ReadRepoFile("BookOfEternityGMBridge/Program.cs");

        Assert.Contains("IsCodexCliCompletedTurnIdlePrompt", source, StringComparison.Ordinal);
        Assert.Contains("Run /review on my current changes", source, StringComparison.Ordinal);
        Assert.Contains("Find and fix a bug in @filename", source, StringComparison.Ordinal);
        Assert.Contains("Worked for", source, StringComparison.Ordinal);
        Assert.Contains("gpt-", source, StringComparison.Ordinal);
    }

    [Fact]
    public void BridgeHost_TreatsLowerCodexPromptAsIdleWhenStaleWorkingTextRemainsAbove()
    {
        var source = ReadRepoFile("BookOfEternityGMBridge/Program.cs");

        Assert.Contains("HasCodexIdlePromptAfterLastWorkingMarker", source, StringComparison.Ordinal);
        Assert.Contains("if (HasCodexIdlePromptAfterLastWorkingMarker(normalized))", source, StringComparison.Ordinal);
        Assert.Contains("return false;", source[source.IndexOf("if (HasCodexIdlePromptAfterLastWorkingMarker(normalized))", StringComparison.Ordinal)..], StringComparison.Ordinal);
        Assert.Contains("return HasCodexIdlePromptAfterLastWorkingMarker(normalized) ||", source, StringComparison.Ordinal);
        Assert.Contains("normalized.LastIndexOf(\"›\", StringComparison.Ordinal)", source, StringComparison.Ordinal);
        Assert.Contains("normalized.LastIndexOf(\"Working\", StringComparison.OrdinalIgnoreCase)", source, StringComparison.Ordinal);
    }

    [Fact]
    public void BridgeHost_TreatsCodexBootAndModelLoadingScreensAsNotReady()
    {
        var source = ReadRepoFile("BookOfEternityGMBridge/Program.cs");

        Assert.Contains("Booting MCP server", source, StringComparison.Ordinal);
        Assert.Contains("model:", source, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("loading", source, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("if (IsCodexCliWorkingScreen(normalized))", source, StringComparison.Ordinal);
        Assert.Contains("if (IsWorkspaceTrustPrompt(normalized) || IsCodexCliUpdatePrompt(normalized))", source, StringComparison.Ordinal);
    }

    [Fact]
    public void BridgeHost_BootAndModelLoadingScreensOutrankStaleIdlePromptHeuristics()
    {
        var source = ReadRepoFile("BookOfEternityGMBridge/Program.cs");

        Assert.Contains("IsCodexCliBootOrModelLoadingScreen", source, StringComparison.Ordinal);

        var workingMethod = source.IndexOf("private static bool IsCodexCliWorkingScreen", StringComparison.Ordinal);
        Assert.True(workingMethod >= 0, "Bridge host must keep a Codex working-screen detector.");
        var bootProbe = source.IndexOf("IsCodexCliBootOrModelLoadingScreen(normalized)", workingMethod, StringComparison.Ordinal);
        var staleIdleOverride = source.IndexOf("HasCodexIdlePromptAfterLastWorkingMarker(normalized)", workingMethod, StringComparison.Ordinal);
        Assert.True(bootProbe > workingMethod, "Working-screen detection must inspect Codex boot/model-loading markers.");
        Assert.True(staleIdleOverride > bootProbe, "Codex boot/model-loading markers must block ready before stale idle-prompt heuristics run.");

        var idleMethod = source.IndexOf("private static bool IsCodexCliIdlePrompt", StringComparison.Ordinal);
        Assert.True(idleMethod >= 0, "Bridge host must keep a Codex idle-prompt detector.");
        var idleBootGuard = source.IndexOf("IsCodexCliBootOrModelLoadingScreen(normalized)", idleMethod, StringComparison.Ordinal);
        var idlePromptProbe = source.IndexOf("HasCodexIdlePromptAfterLastWorkingMarker(normalized)", idleMethod, StringComparison.Ordinal);
        Assert.True(idleBootGuard > idleMethod, "Idle-prompt detection must reject Codex boot/model-loading screens.");
        Assert.True(idlePromptProbe > idleBootGuard, "Idle-prompt heuristics must run only after the boot/model-loading guard.");
    }

    [Fact]
    public void LauncherScript_StartBridgeDefaultsHiddenAndAllowsVisibleFallback()
    {
        var source = ReadRepoFile("BookOfEternityClient/Launcher/bookofeternity.ps1");

        Assert.Contains("-WindowStyle $windowStyle", source, StringComparison.Ordinal);
        Assert.Contains("$visibleBridge", source, StringComparison.Ordinal);
        Assert.Contains("visible", source, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("GM bridge starting in a hidden console window", source, StringComparison.Ordinal);
    }

    [Fact]
    public void LauncherScript_StartBridgePrefersBuiltExecutableToAvoidStaleBuildLocks()
    {
        var source = ReadRepoFile("BookOfEternityClient/Launcher/bookofeternity.ps1");

        Assert.Contains("$bridgeExe", source, StringComparison.Ordinal);
        Assert.Contains("BookOfEternityGMBridge.exe", source, StringComparison.Ordinal);
        Assert.Contains("Test-Path $bridgeExe", source, StringComparison.Ordinal);
        Assert.Contains("& \"{1}\" --host --sessionPath \"{2}\" --pipeName \"{3}\"", source, StringComparison.Ordinal);
        Assert.Contains("dotnet run --project", source, StringComparison.Ordinal);
    }

    [Fact]
    public void BridgeHost_ShutdownWritesResponseBeforeCancellingServerLoop()
    {
        var source = ReadRepoFile("BookOfEternityGMBridge/Program.cs");

        Assert.Contains("ShutdownAfterResponse", source, StringComparison.Ordinal);
        Assert.Contains("BridgeResponse.Shutdown", source, StringComparison.Ordinal);

        var shutdownCase = source.IndexOf("case \"shutdown\":", StringComparison.Ordinal);
        Assert.True(shutdownCase >= 0, "Bridge host must expose a shutdown command.");

        var shutdownResponse = source.IndexOf("BridgeResponse.Shutdown", shutdownCase, StringComparison.Ordinal);
        var directCancel = source.IndexOf("_cts.Cancel();", shutdownCase, StringComparison.Ordinal);
        Assert.True(shutdownResponse > shutdownCase, "Shutdown command must return a response object.");
        Assert.True(
            directCancel < 0 || shutdownResponse < directCancel,
            "Shutdown must not cancel the bridge token before the pipe response is written.");
    }

    [Fact]
    public void BridgeStatus_IncludesSessionPathForSessionLocalShutdownReports()
    {
        var source = ReadRepoFile("BookOfEternityGMBridge/Program.cs");

        Assert.Contains("public string SessionPath", source, StringComparison.Ordinal);
        Assert.Contains("SessionPath = _sessionPath", source, StringComparison.Ordinal);
    }

    [Fact]
    public void LauncherScript_ShutdownBridgeUsesSessionLocalFallback()
    {
        var source = ReadRepoFile("BookOfEternityClient/Launcher/bookofeternity.ps1");

        Assert.Contains("function Invoke-BridgeShutdown", source, StringComparison.Ordinal);
        Assert.Contains("function Stop-SessionLocalBridgeProcesses", source, StringComparison.Ordinal);
        Assert.Contains("function Get-ProcessDescendantIds", source, StringComparison.Ordinal);
        Assert.Contains("already-stopped", source, StringComparison.Ordinal);
        Assert.Contains("-FallbackUsed $true", source, StringComparison.Ordinal);
        Assert.Contains("command = \"shutdown\"", source, StringComparison.Ordinal);

        var shutdownAction = source.IndexOf("\"shutdown-bridge\" {", StringComparison.Ordinal);
        var shutdownFunction = source.IndexOf("Invoke-BridgeShutdown", shutdownAction, StringComparison.Ordinal);
        var plainRequest = source.IndexOf("Invoke-BridgeRequestChecked -ResolvedSessionPath $resolvedSessionPath -Payload @{\r\n            command = \"shutdown\"", shutdownAction, StringComparison.Ordinal);
        Assert.True(shutdownFunction > shutdownAction, "shutdown-bridge must use the dedicated shutdown path.");
        Assert.True(plainRequest < 0 || shutdownFunction < plainRequest, "shutdown-bridge must not rely only on the generic checked request.");
    }

    [Fact]
    public void LauncherScript_HiddenBridgeHostDoesNotKeepNoExitShellAlive()
    {
        var source = ReadRepoFile("BookOfEternityClient/Launcher/bookofeternity.ps1");

        Assert.Contains("$bridgeHostArguments", source, StringComparison.Ordinal);
        Assert.Contains("if ($VisibleBridge)", source, StringComparison.Ordinal);
        Assert.Contains("$bridgeHostArguments += \"-NoExit\"", source, StringComparison.Ordinal);
        Assert.DoesNotContain("-ArgumentList @(\"-NoExit\", \"-ExecutionPolicy\", \"Bypass\", \"-EncodedCommand\", $encodedHostScript)", source, StringComparison.Ordinal);
    }

    [Fact]
    public void DaemonBridgeDispatch_UsesTypedOperationWithoutReadinessBypass()
    {
        var source = ReadRepoFile("BookOfEternityClient/game_master_daemon.ps1");
        Assert.Contains("dispatch-operation $json", source, StringComparison.Ordinal);
        Assert.Contains("Test-GmPromptDeliveryIdentity", source, StringComparison.Ordinal);
        Assert.DoesNotContain("& $BridgeControlScript addText $Message", source, StringComparison.Ordinal);
        Assert.DoesNotContain("& $BridgeControlScript sendEnter", source, StringComparison.Ordinal);
    }

    [Fact]
    public void ConsoleOptions_ExposeGmWorkerBridgeProfileDiagnostics()
    {
        var source = ReadRepoFile("BookOfEternityClient/Core/GameEngine/GameEngine.OptionsAndSettings.cs");

        Assert.Contains("gm_worker_profiles", source, StringComparison.Ordinal);
        Assert.Contains("ShowGmWorkerBridgeDiagnostics", source, StringComparison.Ordinal);
        Assert.Contains("GmWorkerBridgeProfiles", source, StringComparison.Ordinal);
        Assert.Contains("GmWorkerProposalInboxService", source, StringComparison.Ordinal);
        Assert.Contains("Proposal inbox", source, StringComparison.Ordinal);
    }

    [Fact]
    public void DaemonContextPack_ExposesSafeGmProbeSurfaceBeforeSourceFallback()
    {
        var source = ReadRepoFile("BookOfEternityClient/game_master_daemon.ps1");

        Assert.Contains("Probes\\GM_SAFE_PROBES.json", source, StringComparison.Ordinal);
        Assert.Contains("Probes\\GM_SAFE_PROBES.md", source, StringComparison.Ordinal);
        Assert.Contains("current_realm_mode_summary", source, StringComparison.Ordinal);
        Assert.Contains("active_pending_contracts", source, StringComparison.Ordinal);
        Assert.Contains("validation_issue_summary", source, StringComparison.Ordinal);
        Assert.Contains("allowed_output_templates", source, StringComparison.Ordinal);
        Assert.Contains("rollback_status", source, StringComparison.Ordinal);
        Assert.Contains("worker_role_summary", source, StringComparison.Ordinal);
        Assert.Contains("\"inventory-content\"", source, StringComparison.Ordinal);
        Assert.Contains("\"npc-content\"", source, StringComparison.Ordinal);
        Assert.Contains("\"faction-content\"", source, StringComparison.Ordinal);
        Assert.Contains("\"location-content\"", source, StringComparison.Ordinal);
        Assert.Contains("proposalOnlyTaskTypes", source, StringComparison.Ordinal);
        Assert.Contains("read-only", source, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("missing harness surface", source, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("$script:GmSafeProbeDirective", source, StringComparison.Ordinal);
        Assert.Contains("$($script:GmSafeProbeDirective)", source, StringComparison.Ordinal);
        Assert.Contains("$script:GmSourceFallbackDirective", source, StringComparison.Ordinal);
        Assert.Contains("$($script:GmSourceFallbackDirective)", source, StringComparison.Ordinal);
        Assert.Contains("Do not read implementation code", source, StringComparison.Ordinal);

        var turnPromptIndex = source.IndexOf("Process turn #$turnNumber", StringComparison.Ordinal);
        var safeProbeIndex = source.IndexOf("$($script:GmSafeProbeDirective)", turnPromptIndex, StringComparison.Ordinal);
        var sourceFallbackIndex = source.IndexOf("$($script:GmSourceFallbackDirective)", turnPromptIndex, StringComparison.Ordinal);
        Assert.True(safeProbeIndex > turnPromptIndex, "Turn prompt should include safe probe guidance.");
        Assert.True(sourceFallbackIndex > safeProbeIndex, "Safe probes should be presented before implementation-source avoidance/fallback language.");
    }

    private static string ReadRepoFile(string relativePath)
    {
        var root = LocateRepoRoot();
        return File.ReadAllText(Path.Combine(root, relativePath.Replace('/', Path.DirectorySeparatorChar)));
    }

    private static string LocateRepoRoot()
    {
        return TestRepoPaths.RepoRoot;
    }
}
