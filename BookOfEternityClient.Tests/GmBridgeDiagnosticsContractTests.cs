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
    public void LauncherScript_ExposesDiagnosticsCommand()
    {
        var source = ReadRepoFile("BookOfEternityClient/Launcher/bookofeternity.ps1");

        Assert.Contains("\"diagnostics\"", source, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("command = \"diagnostics\"", source, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void BridgeHost_ResolvesRepoRootFromProcessContextInsteadOfSessionParent()
    {
        var source = ReadRepoFile("BookOfEternityGMBridge/Program.cs");

        Assert.Contains("ResolveRepoRoot", source, StringComparison.Ordinal);
        Assert.DoesNotContain("_repoRoot = Directory.GetParent(_clientRoot)?.FullName ?? _clientRoot;", source, StringComparison.Ordinal);
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
