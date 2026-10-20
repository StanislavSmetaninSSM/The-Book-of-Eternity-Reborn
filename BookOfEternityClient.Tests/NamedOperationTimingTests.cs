using BookOfEternityClient.Core;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace BookOfEternityClient.Tests;

/// <summary>
/// Verifies that optional named timers observe operations without changing their result or exception.
/// </summary>
public sealed class NamedOperationTimingTests
{
    /// <summary>
    /// Requires the disabled diagnostic to allocate no timing scope.
    /// </summary>
    [Fact]
    public void MissingObserverReturnsNoScope()
    {
        using var fixture = new Fixture(new FileSystemManagerHooks());
        Assert.Null(fixture.Files.MeasureOperation("Test.Disabled"));
    }

    /// <summary>
    /// Requires one named begin and one elapsed end even when disposal is repeated.
    /// </summary>
    [Fact]
    public void TimerReportsNamedBeginAndElapsedEndOnce()
    {
        var events = new List<(string Name, string Boundary, TimeSpan Elapsed)>();
        using var fixture = new Fixture(new FileSystemManagerHooks
        {
            OperationTimingObserver = (name, boundary, elapsed) => events.Add((name, boundary, elapsed))
        });
        var timer = Assert.IsAssignableFrom<IDisposable>(fixture.Files.MeasureOperation("Test.ActualOperation"));
        Assert.Single(events);
        Thread.Sleep(1);
        timer.Dispose();
        timer.Dispose();
        Assert.Equal(2, events.Count);
        Assert.Equal(("Test.ActualOperation", "begin", TimeSpan.Zero), events[0]);
        Assert.Equal("Test.ActualOperation", events[1].Name);
        Assert.Equal("end", events[1].Boundary);
        Assert.True(events[1].Elapsed > TimeSpan.Zero);
    }

    /// <summary>
    /// Requires observer exceptions at both boundaries to leave the measured operation unchanged.
    /// </summary>
    [Fact]
    public void ThrowingObserverCannotChangeOperationResult()
    {
        var calls = 0;
        using var fixture = new Fixture(new FileSystemManagerHooks
        {
            OperationTimingObserver = (_, _, _) => { calls++; throw new IOException("diagnostic only"); }
        });
        var result = 0;
        using (fixture.Files.MeasureOperation("Test.ObserverFailure")) result = 42;
        Assert.Equal(42, result);
        Assert.Equal(2, calls);
    }

    /// <summary>
    /// Requires exception unwinding to end the timer without replacing the original failure.
    /// </summary>
    [Fact]
    public void OperationExceptionRemainsPrimaryAndEndsTimer()
    {
        var ends = 0;
        using var fixture = new Fixture(new FileSystemManagerHooks
        {
            OperationTimingObserver = (_, boundary, _) => { if (boundary == "end") ends++; }
        });
        var expected = new InvalidDataException("original operation");
        var actual = Assert.Throws<InvalidDataException>((Action)(() =>
        {
            using var timer = fixture.Files.MeasureOperation("Test.FailedOperation");
            throw expected;
        }));
        Assert.Same(expected, actual);
        Assert.Equal(1, ends);
    }

    /// <summary>
    /// Owns only the temporary manager namespace needed to observe timer behavior.
    /// </summary>
    private sealed class Fixture : IDisposable
    {
        private readonly string _root;

        /// <summary>
        /// Creates an independent manager without a game, lease or worker process.
        /// </summary>
        /// <param name="hooks">
        /// Optional timing observations supplied by this test.
        /// </param>
        internal Fixture(FileSystemManagerHooks hooks)
        {
            _root = Path.Combine(Path.GetTempPath(), "boe-named-timer-" + Guid.NewGuid().ToString("N"));
            Files = new FileSystemManager(_root, NullLogger<FileSystemManager>.Instance,
                PhysicalLoadTransactionOperations.Instance, hooks);
        }

        /// <summary>
        /// Gets the independently owned manager under observation.
        /// </summary>
        internal FileSystemManager Files { get; }

        /// <summary>
        /// Removes only this fixture's explicit temporary descendant.
        /// </summary>
        public void Dispose()
        {
            var full = Path.GetFullPath(_root);
            var temporary = Path.GetFullPath(Path.GetTempPath()).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
            if (!full.StartsWith(temporary, StringComparison.OrdinalIgnoreCase) ||
                !Path.GetFileName(full).StartsWith("boe-named-timer-", StringComparison.Ordinal))
                throw new InvalidDataException("Timer fixture left its owned namespace.");
            if (Directory.Exists(full)) Directory.Delete(full, recursive: true);
        }
    }
}
