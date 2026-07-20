using Xunit;

namespace BookOfEternityClient.Tests;

public sealed class GmWorkerBridgeSourceGuardTests
{
    [Fact]
    public void WorkerBridgePool_LaunchesWorkersHiddenByDefault()
    {
        var source = ReadClientFile("Services/GmWorkers/GmWorkerBridgePool.cs");

        Assert.Contains("UseShellExecute = false", source, StringComparison.Ordinal);
        Assert.Contains("CreateNoWindow = true", source, StringComparison.Ordinal);
        Assert.Contains("ProcessWindowStyle.Hidden", source, StringComparison.Ordinal);
        Assert.DoesNotContain("ProcessWindowStyle.Normal", source, StringComparison.Ordinal);
        Assert.DoesNotContain("ProcessWindowStyle.Maximized", source, StringComparison.Ordinal);
        Assert.DoesNotContain("ProcessWindowStyle.Minimized", source, StringComparison.Ordinal);
    }

    [Fact]
    public void WorkerServices_OnlyApplyGateMayApplyCanonicalFileChanges()
    {
        var workerRoot = Path.Combine(TestRepoPaths.RepoRoot, "BookOfEternityClient", "Services", "GmWorkers");
        Assert.True(Directory.Exists(workerRoot), "Worker service directory must exist.");

        var offenders = Directory
            .EnumerateFiles(workerRoot, "*.cs", SearchOption.AllDirectories)
            .Where(path => !string.Equals(Path.GetFileName(path), "GmWorkerApplyGate.cs", StringComparison.Ordinal))
            .Select(path => new
            {
                Path = Path.GetRelativePath(TestRepoPaths.RepoRoot, path).Replace('\\', '/'),
                Source = File.ReadAllText(path)
            })
            .Where(file =>
                file.Source.Contains("ApplyCanonicalFileChanges", StringComparison.Ordinal) ||
                file.Source.Contains("ApplyChangedFiles", StringComparison.Ordinal) ||
                file.Source.Contains("WriteCanonical", StringComparison.Ordinal))
            .Select(file => file.Path)
            .ToArray();

        Assert.Empty(offenders);
    }

    [Fact]
    public void SharedAuditEventIdGenerator_IsTheOnlyWorkerAuditIdFormatter()
    {
        var workerRoot = Path.Combine(TestRepoPaths.RepoRoot, "BookOfEternityClient", "Services", "GmWorkers");
        Assert.True(Directory.Exists(workerRoot), "Worker service directory must exist.");

        var offenders = Directory
            .EnumerateFiles(workerRoot, "*.cs", SearchOption.AllDirectories)
            .Where(path => !string.Equals(
                Path.GetFileName(path),
                "GmWorkerAuditEventIdGenerator.cs",
                StringComparison.Ordinal))
            .Where(path => File.ReadAllText(path).Contains("worker_audit_", StringComparison.Ordinal))
            .Select(path => Path.GetRelativePath(TestRepoPaths.RepoRoot, path).Replace('\\', '/'))
            .ToArray();

        Assert.Empty(offenders);
    }

    private static string ReadClientFile(string relativePath)
    {
        var path = Path.Combine(
            TestRepoPaths.RepoRoot,
            "BookOfEternityClient",
            Path.Combine(relativePath.Split('/')));
        return File.ReadAllText(path);
    }
}
