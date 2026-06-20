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
