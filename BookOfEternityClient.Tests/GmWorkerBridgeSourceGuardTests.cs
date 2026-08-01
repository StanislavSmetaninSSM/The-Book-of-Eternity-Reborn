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

    [Fact]
    public void WorkerIsolationGuards_CannotRegressToUnsafePathOrCleanupSemantics()
    {
        var validator = ReadClientFile("Services/GmWorkers/GmWorkerContractValidator.cs");
        var workspace = ReadClientFile("Services/GmWorkers/GmWorkerExecutionWorkspace.cs");
        var bridgePool = ReadClientFile("Services/GmWorkers/GmWorkerBridgePool.cs");
        var reaper = ReadClientFile("Services/GmWorkers/GmWorkerQuarantineReaper.cs");
        var auditLog = ReadClientFile("Services/GmWorkers/GmWorkerAuditLog.cs");

        Assert.Contains("CanonicalPathComparer { get; } = StringComparer.OrdinalIgnoreCase", validator, StringComparison.Ordinal);
        Assert.Contains("must not contain wildcard patterns", validator, StringComparison.Ordinal);
        Assert.Contains("ValidatePath(path, \"afterlifeContract.allowedAfterlifeSurfaces\"", validator, StringComparison.Ordinal);
        Assert.DoesNotContain(
            "ValidatePathOrPattern(path, \"afterlifeContract.allowedAfterlifeSurfaces\"",
            validator,
            StringComparison.Ordinal);

        Assert.Contains("PhysicalFileAuthority.StableDirectory", workspace, StringComparison.Ordinal);
        Assert.Contains("OpenExistingStableDirectory", workspace, StringComparison.Ordinal);
        Assert.Contains("CreateNewWritableFile", workspace, StringComparison.Ordinal);
        Assert.Contains("TryDeleteDirectoryTree", workspace, StringComparison.Ordinal);
        Assert.DoesNotContain("File.WriteAllBytesAsync", workspace, StringComparison.Ordinal);
        Assert.DoesNotContain("new FileStream(", workspace, StringComparison.Ordinal);
        Assert.DoesNotContain("File.Delete(", workspace, StringComparison.Ordinal);
        Assert.DoesNotContain("requireSingleFileLinks: false", workspace, StringComparison.Ordinal);
        Assert.DoesNotContain("SearchOption.AllDirectories", workspace, StringComparison.Ordinal);
        Assert.DoesNotContain("Directory.Delete(workspaceRoot, recursive: true)", workspace, StringComparison.Ordinal);
        Assert.Contains("QuarantineAuditDirectoryName", workspace, StringComparison.Ordinal);
        Assert.Contains("RenameOpenedObjectRelative", workspace, StringComparison.Ordinal);
        Assert.Contains("requestDeleteAccess: true", workspace, StringComparison.Ordinal);

        Assert.Contains("GmWorkerQuarantineReaper", bridgePool, StringComparison.Ordinal);
        Assert.Contains("RecordRequiredTerminalEventOnceAsync", bridgePool, StringComparison.Ordinal);
        Assert.DoesNotContain("Retain the acquired semaphore and reference count permanently", bridgePool, StringComparison.Ordinal);
        Assert.Contains("AppendRequiredEventOnceIfCurrentSessionAsync", auditLog, StringComparison.Ordinal);
        Assert.Contains("suppressFailure: false", auditLog, StringComparison.Ordinal);
        Assert.Contains("DefaultCapacity", reaper, StringComparison.Ordinal);
        Assert.Contains("DefaultRetrySchedule", reaper, StringComparison.Ordinal);
        Assert.Contains("Interlocked.CompareExchange", reaper, StringComparison.Ordinal);
        Assert.Contains("ConfirmDeathAsync", reaper, StringComparison.Ordinal);
        Assert.Contains("DrainConfirmedAsync", reaper, StringComparison.Ordinal);
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
