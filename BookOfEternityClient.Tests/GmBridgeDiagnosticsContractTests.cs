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
    public void BridgeHost_TreatsCodexBootAndModelLoadingScreensAsNotReady()
    {
        var source = ReadRepoFile("BookOfEternityGMBridge/Program.cs");

        Assert.Contains("Booting MCP server", source, StringComparison.Ordinal);
        Assert.Contains("model:", source, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("loading", source, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("IsWorkspaceTrustPrompt(normalized) || IsCodexCliUpdatePrompt(normalized) || IsCodexCliWorkingScreen(normalized)", source, StringComparison.Ordinal);
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
        var current = AppContext.BaseDirectory;
        while (!string.IsNullOrWhiteSpace(current))
        {
            if (File.Exists(Path.Combine(current, "TheBookOfEternityReborn.sln")) ||
                File.Exists(Path.Combine(current, ".git")) ||
                Directory.Exists(Path.Combine(current, ".git")))
            {
                return current;
            }

            var parent = Directory.GetParent(current);
            if (parent == null)
                break;
            current = parent.FullName;
        }

        throw new DirectoryNotFoundException("Could not locate repository root.");
    }
}
