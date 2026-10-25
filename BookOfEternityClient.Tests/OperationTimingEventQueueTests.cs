using System.Collections.Concurrent;
using System.Diagnostics;
using BookOfEternityClient.Core;
using Xunit;
using NamedEvent = BookOfEternityClient.Core.OperationTimingAccumulator.NamedEvent;

namespace BookOfEternityClient.Tests;

/// <summary>
/// Verifies original scalar event capture with independent memory-only windows.
/// </summary>
public sealed class OperationTimingEventQueueTests
{
    /// <summary>
    /// Preserves each original nested boundary, its clock, identity and inclusive duration.
    /// </summary>
    [Fact]
    public void NestedScopesRetainExactOriginalEventsAndElapsedTicks()
    {
        var recorder = new OperationTimingAccumulator();
        using var window = recorder.StartWindow(captureEvents: true);
        var before = Stopwatch.GetTimestamp();
        using (recorder.Measure("outer"))
            using (recorder.Measure("inner")) { }
        var after = Stopwatch.GetTimestamp();
        window.Dispose();

        var events = window.DrainEvents();
        Assert.Equal(new long[] { 1, 2, 3, 4 }, events.Select(item => item.Sequence));
        Assert.Equal(new[] { "outer", "inner", "inner", "outer" }, events.Select(item => item.Name));
        Assert.Equal(new[] { "begin", "begin", "end", "end" }, events.Select(item => item.State));
        Assert.True(events[0].ScopeId > 0);
        Assert.True(events[1].ScopeId > 0);
        Assert.NotEqual(events[0].ScopeId, events[1].ScopeId);
        Assert.Equal(events[0].ScopeId, events[3].ScopeId);
        Assert.Equal(events[1].ScopeId, events[2].ScopeId);
        Assert.All(events, item =>
        {
            Assert.InRange(item.TimestampTicks, before, after);
            Assert.False(item.Late);
        });
        Assert.Equal(0, events[0].ElapsedTicks);
        Assert.Equal(0, events[1].ElapsedTicks);
        Assert.Equal(events[2].TimestampTicks - events[1].TimestampTicks, events[2].ElapsedTicks);
        Assert.Equal(events[3].TimestampTicks - events[0].TimestampTicks, events[3].ElapsedTicks);
        var snapshot = window.Capture();
        Assert.True(snapshot.Complete);
        foreach (var end in events.Where(item => item.State == "end"))
            Assert.Equal(end.ElapsedTicks * 1000d / Stopwatch.Frequency,
                Assert.Single(snapshot.Rows, row => row.Name == end.Name).InclusiveMilliseconds);
        Assert.Equal(new OperationTimingAccumulator.EventStatus(true, 4, 4, 4, 0, 0),
            window.CaptureEventStatus());
        Assert.Empty(window.DrainEvents());
    }

    /// <summary>
    /// Keeps the original no-argument API and explicit disabled mode aggregate-only.
    /// </summary>
    /// <param name="explicitSelection">Whether to select disabled capture explicitly.</param>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void DefaultAndExplicitDisabledCaptureKeepAggregateBehavior(bool explicitSelection)
    {
        var recorder = new OperationTimingAccumulator();
        Assert.Null(recorder.Measure("outside"));
        using var window = explicitSelection ? recorder.StartWindow(captureEvents: false) : recorder.StartWindow();
        var scope = Assert.IsAssignableFrom<IDisposable>(recorder.Measure("aggregate"));
        scope.Dispose();
        scope.Dispose();
        window.Dispose();
        window.Dispose();
        Assert.Null(recorder.Measure("outside"));
        var snapshot = window.Capture();
        Assert.True(snapshot.Complete);
        var row = Assert.Single(snapshot.Rows);
        Assert.Equal("aggregate", row.Name);
        Assert.Equal(1, row.Started);
        Assert.Equal(1, row.Completed);
        Assert.Empty(window.DrainEvents());
        Assert.Equal(new OperationTimingAccumulator.EventStatus(false, 0, 0, 0, 0, 0),
            window.CaptureEventStatus());
    }

    /// <summary>
    /// Transfers every same-name scope exactly once across concurrent producers and drainers.
    /// </summary>
    /// <returns>Completion after all producers and both independent drain consumers settle.</returns>
    [Fact]
    public async Task ConcurrentProducersAndDrainersLoseOrDuplicateNoEvents()
    {
        const int producerCount = 4, scopesPerProducer = 250;
        var recorder = new OperationTimingAccumulator();
        using var window = recorder.StartWindow(captureEvents: true);
        var batches = new ConcurrentQueue<NamedEvent[]>();
        var stop = 0;
        var readyCount = 0;
        var ready = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var drainers = Enumerable.Range(0, 2).Select(_ => Task.Run(async () =>
        {
            if (Interlocked.Increment(ref readyCount) == 2) ready.SetResult();
            while (Volatile.Read(ref stop) == 0)
            {
                var batch = window.DrainEvents();
                if (batch.Length != 0) batches.Enqueue(batch);
                await Task.Yield();
            }
        })).ToArray();

        await ready.Task;
        try
        {
            await Task.WhenAll(Enumerable.Range(0, producerCount).Select(_ => Task.Run(() =>
            {
                for (var index = 0; index < scopesPerProducer; index++)
                {
                    using var scope = recorder.Measure("shared-name");
                    if (index % 32 == 0) Thread.Yield();
                }
            })));
            window.Dispose();
        }
        finally
        {
            Volatile.Write(ref stop, 1);
            await Task.WhenAll(drainers);
        }
        var last = window.DrainEvents();
        if (last.Length != 0) batches.Enqueue(last);

        Assert.All(batches, batch =>
        {
            for (var index = 1; index < batch.Length; index++)
                Assert.Equal(batch[index - 1].Sequence + 1, batch[index].Sequence);
        });
        var events = batches.SelectMany(batch => batch).OrderBy(item => item.Sequence).ToArray();
        const int expected = producerCount * scopesPerProducer * 2;
        Assert.Equal(expected, events.Length);
        Assert.Equal(Enumerable.Range(1, expected).Select(index => (long)index), events.Select(item => item.Sequence));
        var pairs = events.GroupBy(item => item.ScopeId).ToArray();
        Assert.Equal(producerCount * scopesPerProducer, pairs.Length);
        Assert.All(pairs, pair =>
        {
            var boundaries = pair.ToArray();
            Assert.Equal(2, boundaries.Length);
            Assert.Equal("begin", boundaries[0].State);
            Assert.Equal("end", boundaries[1].State);
            Assert.Equal(boundaries[1].TimestampTicks - boundaries[0].TimestampTicks, boundaries[1].ElapsedTicks);
        });
        Assert.All(events, item => { Assert.Equal("shared-name", item.Name); Assert.False(item.Late); });
        Assert.Equal(new OperationTimingAccumulator.EventStatus(true, expected, expected, expected, 0, 0),
            window.CaptureEventStatus());
        Assert.True(window.Capture().Complete);
    }

    /// <summary>
    /// Keeps open and late scopes attached to their original window across independent captures.
    /// </summary>
    [Fact]
    public void OpenAndLateScopesStayInTheirOriginalWindow()
    {
        var recorder = new OperationTimingAccumulator();
        using var first = recorder.StartWindow(captureEvents: true);
        var late = Assert.IsAssignableFrom<IDisposable>(recorder.Measure("first"));
        first.Dispose();
        Assert.Equal(1, first.Capture().OpenScopes);
        Assert.False(first.Capture().Complete);
        var begin = Assert.Single(first.DrainEvents());
        Assert.Equal("begin", begin.State);
        Assert.False(begin.Late);

        using var second = recorder.StartWindow(captureEvents: true);
        using (recorder.Measure("second")) { }
        late.Dispose();
        late.Dispose();
        first.Dispose();
        second.Dispose();
        var end = Assert.Single(first.DrainEvents());
        Assert.Equal("first", end.Name);
        Assert.Equal("end", end.State);
        Assert.Equal(begin.ScopeId, end.ScopeId);
        Assert.Equal(2, end.Sequence);
        Assert.True(end.Late);
        Assert.Equal(0, first.Capture().OpenScopes);
        Assert.Equal(1, first.Capture().LateCompletions);
        Assert.False(first.Capture().Complete);
        var next = second.DrainEvents();
        Assert.Equal(2, next.Length);
        Assert.Equal(new long[] { 1, 2 }, next.Select(item => item.Sequence));
        Assert.All(next, item => { Assert.Equal("second", item.Name); Assert.False(item.Late); });
        Assert.True(second.Capture().Complete);
        Assert.Equal(new OperationTimingAccumulator.EventStatus(true, 2, 2, 2, 0, 0), first.CaptureEventStatus());
        Assert.Equal(new OperationTimingAccumulator.EventStatus(true, 2, 2, 2, 0, 0), second.CaptureEventStatus());
    }

    /// <summary>
    /// Detaches mutable result arrays and accounts for partial drains while a scope remains open.
    /// </summary>
    [Fact]
    public void DrainedBatchesAreDetachedAndPendingCountsRemainExact()
    {
        var recorder = new OperationTimingAccumulator();
        using var window = recorder.StartWindow(captureEvents: true);
        var scope = Assert.IsAssignableFrom<IDisposable>(recorder.Measure("original"));
        Assert.Equal(new OperationTimingAccumulator.EventStatus(true, 1, 1, 0, 1, 0), window.CaptureEventStatus());
        var first = window.DrainEvents();
        var begin = Assert.Single(first);
        first[0] = begin with { Name = "changed-copy", ScopeId = -1 };
        Assert.Empty(window.DrainEvents());
        scope.Dispose();
        Assert.Equal(new OperationTimingAccumulator.EventStatus(true, 2, 2, 1, 1, 0), window.CaptureEventStatus());
        var second = window.DrainEvents();
        var end = Assert.Single(second);
        Assert.Equal("original", end.Name);
        Assert.Equal(begin.ScopeId, end.ScopeId);
        Assert.Equal("end", end.State);
        Assert.Equal(2, end.Sequence);
        Assert.Equal("changed-copy", first[0].Name);
        window.Dispose();
        Assert.True(window.Capture().Complete);
        Assert.Equal(new OperationTimingAccumulator.EventStatus(true, 2, 2, 2, 0, 0), window.CaptureEventStatus());
    }

    /// <summary>
    /// Preserves all original names and events without an invocation or record quota.
    /// </summary>
    [Fact]
    public void ManyDistinctNamesRetainEveryOriginalEvent()
    {
        const int scopeCount = 10000;
        var recorder = new OperationTimingAccumulator();
        using var window = recorder.StartWindow(captureEvents: true);
        for (var index = 0; index < scopeCount; index++)
            using (recorder.Measure("section-" + index)) { }
        window.Dispose();
        var events = window.DrainEvents();
        Assert.Equal(scopeCount * 2, events.Length);
        for (var index = 0; index < scopeCount; index++)
        {
            var begin = events[index * 2];
            var end = events[index * 2 + 1];
            Assert.Equal("section-" + index, begin.Name);
            Assert.Equal(begin.Name, end.Name);
            Assert.Equal("begin", begin.State);
            Assert.Equal("end", end.State);
            Assert.Equal(begin.ScopeId, end.ScopeId);
            Assert.Equal(index * 2L + 1, begin.Sequence);
            Assert.Equal(index * 2L + 2, end.Sequence);
        }
        Assert.Equal(scopeCount, window.Capture().Rows.Length);
        Assert.True(window.Capture().Complete);
        Assert.Equal(new OperationTimingAccumulator.EventStatus(true, scopeCount * 2, scopeCount * 2,
            scopeCount * 2, 0, 0), window.CaptureEventStatus());
    }

    /// <summary>
    /// Retains exact boundaries while overlap refusal, invalid registration and body failure stay explicit.
    /// </summary>
    [Fact]
    public void OriginalBodyFailureAndRegistrationRefusalsPreserveCapture()
    {
        var recorder = new OperationTimingAccumulator();
        using var window = recorder.StartWindow(captureEvents: true);
        Assert.Throws<InvalidOperationException>(() => recorder.StartWindow(captureEvents: true));
        Assert.Throws<ArgumentException>(() => recorder.Measure(string.Empty));
        Assert.Throws<ArgumentNullException>(() => recorder.Measure(null!));
        var failure = new InvalidDataException("original operation failure");
        Action operation = () =>
        {
            using var scope = recorder.Measure("throwing-operation");
            throw failure;
        };
        Assert.Same(failure, Record.Exception(operation));
        window.Dispose();
        var events = window.DrainEvents();
        Assert.Equal(2, events.Length);
        Assert.Equal(new[] { "begin", "end" }, events.Select(item => item.State));
        Assert.All(events, item => Assert.Equal("throwing-operation", item.Name));
        Assert.True(window.Capture().Complete);
        Assert.Equal(new OperationTimingAccumulator.EventStatus(true, 2, 2, 2, 0, 0), window.CaptureEventStatus());
    }

    /// <summary>
    /// Records each racing completion once and exposes exactly those registered after close as late.
    /// </summary>
    /// <returns>Completion after all scopes racing window closure have settled.</returns>
    [Fact]
    public async Task ConcurrentCloseAndCompletionsRetainExactLateAccounting()
    {
        const int scopeCount = 128;
        var recorder = new OperationTimingAccumulator();
        using var window = recorder.StartWindow(captureEvents: true);
        var scopes = Enumerable.Range(0, scopeCount)
            .Select(_ => Assert.IsAssignableFrom<IDisposable>(recorder.Measure("closing"))).ToArray();
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var workers = scopes.Chunk(16).Select(chunk => Task.Run(async () =>
        {
            await release.Task;
            foreach (var scope in chunk) { scope.Dispose(); scope.Dispose(); }
        })).ToArray();
        release.SetResult();
        window.Dispose();
        await Task.WhenAll(workers);
        Assert.Null(recorder.Measure("after-close"));
        var events = window.DrainEvents();
        Assert.Equal(scopeCount * 2, events.Length);
        Assert.Equal(scopeCount, events.Count(item => item.State == "begin"));
        Assert.Equal(scopeCount, events.Count(item => item.State == "end"));
        Assert.Equal(scopeCount, events.Select(item => item.ScopeId).Distinct().Count());
        Assert.All(events.Where(item => item.State == "begin"), item => Assert.False(item.Late));
        var snapshot = window.Capture();
        Assert.Equal(0, snapshot.OpenScopes);
        Assert.Equal(events.LongCount(item => item.State == "end" && item.Late), snapshot.LateCompletions);
        Assert.Equal(snapshot.LateCompletions == 0, snapshot.Complete);
        Assert.Equal(new OperationTimingAccumulator.EventStatus(true, scopeCount * 2, scopeCount * 2,
            scopeCount * 2, 0, 0), window.CaptureEventStatus());
    }
}
