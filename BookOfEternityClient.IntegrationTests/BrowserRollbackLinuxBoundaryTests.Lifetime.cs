using System.Text.Json;
using BookOfEternityClient.Core;
using BookOfEternityClient.Services;
using BookOfEternityClient.Services.GmRuntime;
using BookOfEternityClient.WebUi;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed partial class BrowserRollbackLinuxBoundaryTests
{
    [Fact]
    public async Task RevokedOriginalGenerationAfterPublication_RetainsUncertainAndEvidence()
    {
        var (files, coordinator) = await CreateAsync();
        var result = await coordinator.ExecuteAtomicAsync(Request, [Member], async lease =>
        {
            await files.WriteFileAtomicBytesAsync(lease, Member, After);
            File.WriteAllBytes(files.SessionGenerationPath, JsonSerializer.SerializeToUtf8Bytes(new
                { schemaVersion = 1, generationId = Guid.NewGuid().ToString("N") })); // Controlled authority-loss cut, not player protection.
        });
        Assert.Equal(After, File.ReadAllBytes(files.ResolvePath(Member)));
        Assert.Equal(BrowserPreparedWriteDisposition.Uncertain, result.Disposition);
        Assert.True(result.ContinuationBlocked); Assert.True(result.NeedsFollowUp); Assert.False(result.Success);
        Assert.True(Directory.EnumerateFiles(files.ResolvePath(ExplorerLocalTurnRollbackArtifacts.Root), "browser_write_manifest.json", SearchOption.AllDirectories).Any());
    }

    private sealed record DecisionProjection(BrowserLocalWriteResult Result);
    [Theory]
    [InlineData("commit")]
    [InlineData("rollback")]
    [InlineData("uncertain")]
    public async Task GenericConsumerClosingCarrier_PreservesAtomicResultAndMappedMainOutcome(string decision)
    {
        var (files, coordinator) = await CreateAsync();
        _closing = () => Task.FromException(new IOException("generic consumer closing cut"));
        var failure = await Assert.ThrowsAsync<MainOperationContinuationException<DecisionProjection>>(() => coordinator.RunBoundTransactionAsync(async lease =>
        {
            var result = await coordinator.ExecuteAtomicWithinTransactionAsync(lease, Request, [Member], async original =>
            {
                await files.WriteFileAtomicBytesAsync(original, Member, After);
                if (decision == "uncertain") File.WriteAllBytes(files.ResolvePath(Member), [111]);
                if (decision != "commit") throw new InvalidOperationException("generic consumer callback cut");
            });
            return new DecisionProjection(result);
        }));
        Assert.Equal(decision == "commit" ? MainOperationOutcome.Committed : decision == "rollback" ? MainOperationOutcome.RolledBack
            : MainOperationOutcome.Uncertain, failure.EstablishedOutcome);
        Assert.Equal(decision == "commit" ? BrowserPreparedWriteDisposition.Committed : decision == "rollback"
            ? BrowserPreparedWriteDisposition.RolledBack : BrowserPreparedWriteDisposition.Uncertain, failure.EstablishedResult.Result.Disposition);
        Assert.Equal(decision == "commit" ? After : decision == "rollback" ? Before : new byte[] { 111 }, File.ReadAllBytes(files.ResolvePath(Member)));
    }

    [Fact]
    public async Task HeldTransaction_ReplacementWaitsForCompleteSettlement()
    {
        var (files, coordinator) = await CreateAsync();
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var contended = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        _contended = () => { contended.TrySetResult(); return Task.CompletedTask; };
        var generation = File.ReadAllBytes(files.SessionGenerationPath);
        var browser = coordinator.ExecuteAtomicAsync(Request, [Member], async lease =>
        {
            await files.WriteFileAtomicBytesAsync(lease, Member, After);
            entered.TrySetResult(); await release.Task.WaitAsync(TimeSpan.FromSeconds(5));
        });
        Task replacement;
        try
        {
            await entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
            using (ExecutionContext.SuppressFlow()) replacement = Task.Run(() => files.ClearGameStateAsync());
            await contended.Task.WaitAsync(TimeSpan.FromSeconds(5));
            Assert.False(replacement.IsCompleted);
            Assert.Equal(generation, File.ReadAllBytes(files.SessionGenerationPath));
        }
        finally { release.TrySetResult(); }
        var result = await browser.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.True(result.Success, result.Message);
        await replacement.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.False(generation.SequenceEqual(File.ReadAllBytes(files.SessionGenerationPath)));
        AssertNoBrowserEvidence(files);
    }
}
