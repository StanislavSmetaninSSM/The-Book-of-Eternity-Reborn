using BookOfEternityClient.Core;
using BookOfEternityClient.Services.GmRuntime;

namespace BookOfEternityClient.Tests;
internal sealed partial class GmLocalRemoteConsumerScenario
{
    private static async Task<IReadOnlyList<object>> RunRefusalControlsAsync(FileSystemManager files)
    {
        var results = new List<object>();
        Exception? bareFailure = null;
        try { await using var bare = await files.AcquireCanonicalWriteLeaseAsync(); }
        catch (Exception failure) when (failure is IOException or InvalidDataException or InvalidOperationException) { bareFailure = failure; }
        if (bareFailure == null) throw new InvalidOperationException("Unbound retired client acquired a remote writer.");
        results.Add(new { mode = "unbound", failure = bareFailure.Message });
        MainOperationClose? refusedClose = null;
        bool observed = false, remote = false;
        var mismatchedExpectedGeneration = Guid.NewGuid().ToString("N");
        SessionReplacedException? generationFailure = null;
        try
        {
            await SessionOperationContext.RunParticipatingExpectedSessionAsync(files, mismatchedExpectedGeneration,
                () => Task.FromResult(true), observeOriginalClose: (close, seen, wasRemote) =>
                { refusedClose = close; observed = seen; remote = wasRemote; });
        }
        catch (SessionReplacedException failure) { generationFailure = failure; }
        if (generationFailure == null || generationFailure.ExpectedGeneration != mismatchedExpectedGeneration ||
            !observed || !remote || refusedClose == null || refusedClose.Outcome != MainOperationOutcome.Failed)
            throw new InvalidOperationException("Wrong generation did not refuse and close its original operation.");
        // This is a mismatched expected binding, not a changed durable generation.
        results.Add(new { mode = "wrong-expected-generation", expectedGeneration = mismatchedExpectedGeneration,
            failure = generationFailure.Message, close = refusedClose });
        await RequireFollowingOrdinaryAsync(refusedClose);
        foreach (var mode in new[] { "replacement", "late-ordinary" })
        {
            var expected = await RunOriginalControlAsync(mode);
            await RequireFollowingOrdinaryAsync(expected);
        }
        return results;

        async Task<MainOperationClose> RunOriginalControlAsync(string mode)
        {
            // Isolated async flow owns this ambient frame, like the production
            // SessionOperationContext wrapper; no closed frame leaks to the next operation.
            await using var main = files.BeginParticipatingMainAdmission();
            await main.AcquireAsync();
            var expected = main.DescribeClose(MainOperationOutcome.Failed, false)
                ?? throw new InvalidOperationException("Missing original remote control identity.");
            Exception? refusal = null;
            try
            {
                if (mode == "replacement")
                {
                    await using var lifecycle = await files.AcquireSessionLifecycleLeaseAsync();
                    await using var replacement = await files.AcquireSessionReplacementWriteLeaseAsync(lifecycle);
                }
                else
                {
                    main.BeginClosing();
                    await using var ordinary = await files.AcquireCanonicalWriteLeaseAsync();
                }
            }
            catch (Exception failure) when (failure is IOException or InvalidDataException or InvalidOperationException) { refusal = failure; }
            if (refusal == null) throw new InvalidOperationException("Forbidden original remote purpose succeeded: " + mode);
            await main.CompleteAsync(MainOperationOutcome.Failed, false);
            if (!main.CloseObserved || main.TerminalClose != expected)
                throw new InvalidOperationException("Refusal did not close the same original operation: " + mode);
            results.Add(new { mode, failure = refusal.Message, close = main.TerminalClose });
            return expected;
        }

        async Task RequireFollowingOrdinaryAsync(MainOperationClose previous)
        {
            MainOperationClose? next = null; bool seen = false, wasRemote = false;
            await SessionOperationContext.RunParticipatingCurrentSessionAsync(files, () => Task.FromResult(true),
                observeOriginalClose: (close, observed, remote) => { next = close; seen = observed; wasRemote = remote; });
            if (!seen || !wasRemote || next == null || next.OperationId == previous.OperationId || next.Identity != previous.Identity)
                throw new InvalidOperationException("Subsequent ordinary original admission did not independently complete.");
            results.Add(new { mode = "ordinary-after-refusal", close = next });
        }
    }
}
