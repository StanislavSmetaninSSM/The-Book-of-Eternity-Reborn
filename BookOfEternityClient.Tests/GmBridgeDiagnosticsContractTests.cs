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
    public void BridgeHost_RecoversDispatchFailedStatusWhenCodexPromptReturns()
    {
        var source = ReadRepoFile("BookOfEternityGMBridge/Program.cs");

        Assert.Contains("RefreshDispatchFailureRecoveryIfCliPromptReady", source, StringComparison.Ordinal);
        Assert.Contains("string.Equals(_status.State, \"DispatchFailed\", StringComparison.Ordinal)", source, StringComparison.Ordinal);
        Assert.Contains("_status.Ready = true;", source, StringComparison.Ordinal);
        Assert.Contains("_status.State = \"Ready\";", source, StringComparison.Ordinal);
        Assert.Contains("_status.LastError = null;", source, StringComparison.Ordinal);
        Assert.True(
            source.IndexOf("RefreshDispatchFailureRecoveryIfCliPromptReady();", StringComparison.Ordinal) <
            source.IndexOf("case \"dispatchprompt\":", StringComparison.Ordinal),
            "Status and dispatch requests must recover a stale DispatchFailed state before rejecting the next prompt.");
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
