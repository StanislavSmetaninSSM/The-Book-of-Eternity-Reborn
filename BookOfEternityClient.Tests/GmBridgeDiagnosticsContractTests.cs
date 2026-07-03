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
    public void BridgeHost_ConfirmsPromptLeavesInputAfterAppendEnter()
    {
        var source = ReadRepoFile("BookOfEternityGMBridge/Program.cs");

        Assert.Contains("WaitForPromptSubmittedAfterEnterAsync", source, StringComparison.Ordinal);
        Assert.Contains("Prompt was visible and Enter was sent, but the CLI did not transition away from the pasted prompt marker", source, StringComparison.Ordinal);
        Assert.True(
            source.IndexOf("await WriteToPtyAsync(string.Empty, appendEnter: true);", StringComparison.Ordinal) <
            source.IndexOf("WaitForPromptSubmittedAfterEnterAsync", StringComparison.Ordinal),
            "The bridge must press Enter before waiting for the submitted/working screen.");
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
    public void BridgeHost_ClearsPendingCliInputBeforeDispatchPrompt()
    {
        var source = ReadRepoFile("BookOfEternityGMBridge/Program.cs");

        Assert.Contains("ClearPendingInputBeforePromptDispatchAsync", source, StringComparison.Ordinal);
        Assert.Contains("\"\\u0015\"", source, StringComparison.Ordinal);
        Assert.Contains("await ClearPendingInputBeforePromptDispatchAsync();", source, StringComparison.Ordinal);
        Assert.True(
            source.IndexOf("await ClearPendingInputBeforePromptDispatchAsync();", StringComparison.Ordinal) <
            source.IndexOf("var payload = BuildBracketedPastePayload", StringComparison.Ordinal),
            "Pending CLI drafts must be cleared before the bridge pastes a GM prompt.");
    }

    [Fact]
    public void BridgeHost_RefusesPromptDispatchWhileCodexCliIsWorking()
    {
        var source = ReadRepoFile("BookOfEternityGMBridge/Program.cs");

        Assert.Contains("ProbeCliPromptReadinessForDispatch", source, StringComparison.Ordinal);
        Assert.Contains("GM CLI is not ready for a new prompt", source, StringComparison.Ordinal);
        Assert.Contains("esc to interrupt", source, StringComparison.Ordinal);
        Assert.True(
            source.IndexOf("await ClearPendingInputBeforePromptDispatchAsync();", StringComparison.Ordinal) <
            source.IndexOf("var readiness = ProbeCliPromptReadinessForDispatch();", StringComparison.Ordinal),
            "The bridge should clear idle-line drafts before probing visible CLI readiness.");
        Assert.True(
            source.IndexOf("var readiness = ProbeCliPromptReadinessForDispatch();", StringComparison.Ordinal) <
            source.IndexOf("var payload = BuildBracketedPastePayload", StringComparison.Ordinal),
            "The bridge must verify that Codex is idle before pasting the GM prompt.");
    }

    [Fact]
    public void BridgeHost_ManualReadyMustProbeCodexCliReadiness()
    {
        var source = ReadRepoFile("BookOfEternityGMBridge/Program.cs");

        var setReadyMethod = source.IndexOf("private BridgeResponse SetReady(bool ready)", StringComparison.Ordinal);
        Assert.True(setReadyMethod >= 0, "Manual setReady should return a response so it can fail closed.");

        var readinessProbe = source.IndexOf("var readiness = ProbeCliPromptReadinessForDispatch();", setReadyMethod, StringComparison.Ordinal);
        var readyAssignment = source.IndexOf("_status.Ready = ready;", setReadyMethod, StringComparison.Ordinal);

        Assert.True(readinessProbe > setReadyMethod, "Manual ready must probe the visible Codex CLI state before accepting Ready=true.");
        Assert.True(readyAssignment > readinessProbe, "Manual ready must not set Ready=true before the Codex readiness probe.");
        Assert.Contains("Cannot mark bridge ready", source, StringComparison.Ordinal);
    }

    [Fact]
    public void BridgeHost_MarksNotReadyWhileDispatchedCodexPromptIsRunning()
    {
        var source = ReadRepoFile("BookOfEternityGMBridge/Program.cs");

        var dispatchStart = source.IndexOf("case \"dispatchprompt\":", StringComparison.Ordinal);
        var busyState = source.IndexOf("_status.State = \"Busy\";", dispatchStart, StringComparison.Ordinal);
        var readyReset = source.IndexOf("_status.Ready = false;", dispatchStart, StringComparison.Ordinal);
        var promptSubmitted = source.IndexOf("WaitForPromptSubmittedAfterEnterAsync", dispatchStart, StringComparison.Ordinal);

        Assert.True(dispatchStart >= 0, "dispatchprompt handler must exist.");
        Assert.True(readyReset > dispatchStart, "Dispatch must clear Ready before Codex starts processing.");
        Assert.True(busyState > readyReset, "Ready must be cleared before the bridge enters Busy state.");
        Assert.True(promptSubmitted > busyState, "Busy/not-ready state must be written before prompt submission.");
        Assert.Contains("RefreshDispatchFailureRecoveryIfCliPromptReady", source, StringComparison.Ordinal);
        Assert.Contains("AutoMarkReadyIfCliPromptReady", source, StringComparison.Ordinal);
    }

    [Fact]
    public void BridgeHost_RecoversDispatchFailedStatusWhenCodexPromptReturns()
    {
        var source = ReadRepoFile("BookOfEternityGMBridge/Program.cs");

        Assert.Contains("RefreshDispatchFailureRecoveryIfCliPromptReady", source, StringComparison.Ordinal);
        Assert.Contains("string.Equals(_status.State, \"DispatchFailed\", StringComparison.Ordinal)", source, StringComparison.Ordinal);
        Assert.Contains("_status.Ready = true;", source, StringComparison.Ordinal);
        Assert.Contains("_status.State = \"Ready\";", source, StringComparison.Ordinal);
        Assert.Contains("_status.LastError = null;", source, StringComparison.Ordinal);
        Assert.True(
            source.IndexOf("await RefreshBridgeAutomationStateAsync();", StringComparison.Ordinal) <
            source.IndexOf("case \"dispatchprompt\":", StringComparison.Ordinal),
            "Status and dispatch requests must recover a stale DispatchFailed state before rejecting the next prompt.");
    }

    [Fact]
    public void BridgeHost_AutoAcceptsTrustPromptOnlyForTrustedSessionDirectories()
    {
        var source = ReadRepoFile("BookOfEternityGMBridge/Program.cs");

        Assert.Contains("AutoAcceptTrustedCodexWorkingDirectoryTrustPromptAsync", source, StringComparison.Ordinal);
        Assert.Contains("IsWorkspaceTrustPrompt", source, StringComparison.Ordinal);
        Assert.Contains("IsTrustedCodexWorkingDirectory", source, StringComparison.Ordinal);
        Assert.Contains("var sessionRootPath = Path.GetFullPath(_sessionPath);", source, StringComparison.Ordinal);
        Assert.Contains("game_state", source, StringComparison.Ordinal);
        Assert.Contains("gm_context_pack", source, StringComparison.Ordinal);
        Assert.Contains("_lastAutoTrustOutputVersion", source, StringComparison.Ordinal);
    }

    [Fact]
    public void BridgeHost_AutoSkipsCodexUpdatePromptWithoutMarkingReady()
    {
        var source = ReadRepoFile("BookOfEternityGMBridge/Program.cs");

        Assert.Contains("AutoSkipCodexUpdatePromptAsync", source, StringComparison.Ordinal);
        Assert.Contains("IsCodexCliUpdatePrompt", source, StringComparison.Ordinal);
        Assert.Contains("Update available!", source, StringComparison.Ordinal);
        Assert.Contains("Skip until next version", source, StringComparison.Ordinal);
        Assert.Contains("await WriteToPtyAsync(\"3\", appendEnter: true);", source, StringComparison.Ordinal);
        Assert.Contains("Codex CLI is waiting at an update prompt.", source, StringComparison.Ordinal);
    }

    [Fact]
    public void BridgeHost_AutoMarksReadyOnlyAtIdleCodexPrompt()
    {
        var source = ReadRepoFile("BookOfEternityGMBridge/Program.cs");

        Assert.Contains("AutoMarkReadyIfCliPromptReady", source, StringComparison.Ordinal);
        Assert.Contains("IsCodexCliIdlePrompt", source, StringComparison.Ordinal);
        Assert.Contains("OpenAI Codex", source, StringComparison.Ordinal);
        Assert.Contains("Starting MCP server", source, StringComparison.Ordinal);
        Assert.Contains("Codex CLI is not at an idle input prompt", source, StringComparison.Ordinal);
    }

    [Fact]
    public void BridgeHost_AutoClearsReadyWhenCodexCliIsWorking()
    {
        var source = ReadRepoFile("BookOfEternityGMBridge/Program.cs");

        Assert.Contains("AutoMarkNotReadyIfCliWorking", source, StringComparison.Ordinal);
        Assert.Contains("Codex CLI is working; bridge is not ready for a new prompt.", source, StringComparison.Ordinal);

        var refreshMethod = source.IndexOf("private async Task RefreshBridgeAutomationStateAsync()", StringComparison.Ordinal);
        var notReadyCall = source.IndexOf("AutoMarkNotReadyIfCliWorking();", refreshMethod, StringComparison.Ordinal);
        var readyCall = source.IndexOf("AutoMarkReadyIfCliPromptReady();", refreshMethod, StringComparison.Ordinal);

        Assert.True(refreshMethod >= 0, "Bridge automation refresh must exist.");
        Assert.True(notReadyCall > refreshMethod, "Bridge should clear stale ready state during refresh.");
        Assert.True(readyCall > notReadyCall, "Bridge should clear working state before considering auto-ready.");
    }

    [Fact]
    public void BridgeHost_AutoReadyOnlyBlocksActiveDispatchNotEveryBusyState()
    {
        var source = ReadRepoFile("BookOfEternityGMBridge/Program.cs");

        Assert.Contains("string.Equals(_status.LastPromptDispatchState, \"Dispatching\", StringComparison.Ordinal)", source, StringComparison.Ordinal);
        Assert.DoesNotContain("string.Equals(_status.State, \"Busy\", StringComparison.Ordinal) ||", source, StringComparison.Ordinal);
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
    public void DaemonBridgeDispatch_RefreshesDiagnosticsBeforeNotReadyFallback()
    {
        var source = ReadRepoFile("BookOfEternityClient/game_master_daemon.ps1");

        Assert.Contains("function Refresh-GmBridgeReadiness", source, StringComparison.Ordinal);
        Assert.Contains("diagnostics -SessionPath $GameSessionPath", source, StringComparison.Ordinal);
        Assert.Contains("GM bridge readiness refresh via diagnostics", source, StringComparison.Ordinal);

        var notReadyCheck = source.IndexOf("if (-not $status.ready -and -not $AllowNotReady)", StringComparison.Ordinal);
        var refresh = source.IndexOf("Refresh-GmBridgeReadiness", notReadyCheck, StringComparison.Ordinal);
        var fallback = source.IndexOf("return \"bridge-not-ready\"", notReadyCheck, StringComparison.Ordinal);

        Assert.True(notReadyCheck >= 0, "Daemon must still guard against dispatch while bridge is not ready.");
        Assert.True(refresh > notReadyCheck, "Daemon should ask the bridge to refresh diagnostics before waiting.");
        Assert.True(fallback > refresh, "Daemon should only return bridge-not-ready after diagnostics refresh cannot prove readiness.");
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
