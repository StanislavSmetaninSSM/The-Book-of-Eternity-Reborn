using System.Diagnostics;
using BookOfEternityClient.Core;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed partial class PortableWorkerTransactionTests
{
    [Theory]
    [InlineData("IntentStaged", false)]
    [InlineData("IntentPublished", false)]
    [InlineData("first-member", false)]
    [InlineData("PendingValidation", false)]
    [InlineData("CommitStaged", false)]
    [InlineData("Committed", true)]
    [InlineData("CleanupMember", true)]
    [InlineData("CleanupComplete", true)]
    public async Task ActualWorkerDecisionRecoversInIndependentProcess(string cut, bool committed)
    {
        Assert.Equal(73, await RunWorkerHost("worker-publish", cut));
        if (cut == "PendingValidation") AssertState(after: true);
        Assert.Equal(0, await RunWorkerHost("worker-recover", "none"));
        AssertState(after: committed); Assert.False(File.Exists(Active));
        Assert.Equal(0, await RunWorkerHost("worker-recover", "none"));
        AssertState(after: committed);
        Assert.Empty(Directory.GetFiles(_root, ".boe-local-*", SearchOption.AllDirectories));
        Assert.False(Directory.Exists(Path.GetDirectoryName(_files.ActiveWorkerApplyTransactionJournalPath)!));
    }

    [Theory]
    [InlineData("RollbackStaged")]
    [InlineData("MemberRestored")]
    public async Task ColdRollbackCanResumeAfterItsOwnInterruption(string cut)
    {
        Assert.Equal(73, await RunWorkerHost("worker-publish", "PendingValidation"));
        Assert.Equal(73, await RunWorkerHost("worker-recover-cut", cut));
        Assert.Equal(0, await RunWorkerHost("worker-recover", "none"));
        AssertState(after: false); Assert.False(File.Exists(Active));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ColdUnknownMemberOrGenerationBlocksBeforeAnyRestoration(bool generation)
    {
        Assert.Equal(73, await RunWorkerHost("worker-publish", "PendingValidation"));
        var unknown = generation ? Generation(Guid.NewGuid().ToString("N")) : new byte[] { 99 };
        var path = generation ? _files.SessionGenerationPath : _files.ResolvePath(C);
        File.WriteAllBytes(path, unknown); var evidence = File.ReadAllBytes(Active);
        Assert.Equal(0, await RunWorkerHost("worker-conflict", "none"));
        Assert.Equal(After, File.ReadAllBytes(_files.ResolvePath(A)));
        Assert.Empty(File.ReadAllBytes(_files.ResolvePath(B)));
        Assert.Equal(unknown, File.ReadAllBytes(path)); Assert.Equal(evidence, File.ReadAllBytes(Active));
    }

    private async Task<int> RunWorkerHost(string mode, string cut)
    {
        var assembly = typeof(PortableWorkerTransactionTests).Assembly.Location;
        var start = new ProcessStartInfo(Environment.GetEnvironmentVariable("DOTNET_HOST_PATH") ?? "dotnet")
        { UseShellExecute = false, RedirectStandardOutput = true, RedirectStandardError = true };
        foreach (var arg in new[] { "exec", "--runtimeconfig", Path.ChangeExtension(assembly, ".runtimeconfig.json"),
            "--depsfile", Path.ChangeExtension(assembly, ".deps.json"), typeof(PortableStorageCrashHost.Program).Assembly.Location,
            _root, _generation, mode, cut }) start.ArgumentList.Add(arg);
        using var process = Process.Start(start) ?? throw new InvalidOperationException("Owned worker crash host did not start.");
        var stdout = process.StandardOutput.ReadToEndAsync(); var stderr = process.StandardError.ReadToEndAsync();
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        try
        {
            await process.WaitForExitAsync(deadline.Token);
            var output = await stdout + await stderr;
            Assert.True(process.ExitCode is 0 or 73, $"Worker host {mode}/{cut} exited {process.ExitCode}: {output}");
            return process.ExitCode;
        }
        finally
        {
            if (!process.HasExited) { process.Kill(entireProcessTree: true); await process.WaitForExitAsync(); }
        }
    }
}
