using System.Collections.Concurrent;
using System.Text.Json;
using BookOfEternityClient.Core;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;
using Xunit.Abstractions;

namespace BookOfEternityClient.Tests;

/// <summary>
/// Checks passive synchronous timing scopes without constructing game state.
/// </summary>
public sealed partial class OperationTimingContextTests
{
    private readonly ITestOutputHelper _output;

    /// <summary>
    /// Retains the receiver for every observed event and fixture closing receipt.
    /// </summary>
    /// <param name="output">
    /// The current test output receiver, without diagnostic quotas.
    /// </param>
    public OperationTimingContextTests(ITestOutputHelper output) => _output = output;

    /// <summary>
    /// Leaves an unobserved operation unchanged and does not create a current timer.
    /// </summary>
    [Fact]
    public void DisabledScope_DoesNotObserveOrChangeBody()
    {
        using var fixture = new Fixture(_output);
        var files = fixture.Create(null);
        var calls = 0;
        using (files.EnterOperationTimingContext())
        {
            Assert.Null(FileSystemManager.MeasureCurrentOperation("disabled"));
            calls++;
        }
        Assert.Equal(1, calls);
        Assert.Null(FileSystemManager.MeasureCurrentOperation("after-disabled"));
    }

    /// <summary>
    /// Preserves the operation exception and restores observation even when the receiver throws.
    /// </summary>
    /// <param name="throwingObserver">
    /// Whether each boundary is retained and then rejects its passive notification.
    /// </param>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ObservedScope_PreservesFailureAndRestoresContext(bool throwingObserver)
    {
        using var fixture = new Fixture(_output);
        var trace = new Trace(_output, throwingObserver);
        var files = fixture.Create(trace.Observe);
        var expected = new InvalidDataException("operation failure sentinel");
        var actual = Assert.Throws<InvalidDataException>((Action)(() =>
        {
            using var context = files.EnterOperationTimingContext();
            using var operation = FileSystemManager.MeasureCurrentOperation("throwing-body");
            throw expected;
        }));
        Assert.Same(expected, actual);
        Assert.Null(FileSystemManager.MeasureCurrentOperation("after-failure"));
        AssertEvents(trace, ("throwing-body", "begin"), ("throwing-body", "end"));
    }

    /// <summary>
    /// Routes nested scopes to their own receivers and restores the preceding receiver.
    /// </summary>
    [Fact]
    public void NestedScopes_RestoreReceiverAndDisposeIdempotently()
    {
        using var fixture = new Fixture(_output);
        var outer = new Trace(_output);
        var inner = new Trace(_output);
        var outerFiles = fixture.Create(outer.Observe);
        var innerFiles = fixture.Create(inner.Observe);
        var context = outerFiles.EnterOperationTimingContext();
        try
        {
            using (FileSystemManager.MeasureCurrentOperation("outer-before")) { }
            using (innerFiles.EnterOperationTimingContext())
            using (FileSystemManager.MeasureCurrentOperation("inner")) { }
            using (FileSystemManager.MeasureCurrentOperation("outer-after")) { }
        }
        finally { context?.Dispose(); }
        context?.Dispose();
        Assert.Null(FileSystemManager.MeasureCurrentOperation("after-all"));
        AssertEvents(outer, ("outer-before", "begin"), ("outer-before", "end"),
            ("outer-after", "begin"), ("outer-after", "end"));
        AssertEvents(inner, ("inner", "begin"), ("inner", "end"));
    }

    /// <summary>
    /// Suppresses an enclosing receiver inside an explicitly unobserved nested manager.
    /// </summary>
    [Fact]
    public void UnobservedNestedScope_DoesNotBorrowOuterReceiver()
    {
        using var fixture = new Fixture(_output);
        var trace = new Trace(_output);
        var observed = fixture.Create(trace.Observe);
        var unobserved = fixture.Create(null);
        using (observed.EnterOperationTimingContext())
        {
            using (unobserved.EnterOperationTimingContext())
                Assert.Null(FileSystemManager.MeasureCurrentOperation("suppressed"));
            using (FileSystemManager.MeasureCurrentOperation("restored")) { }
        }
        AssertEvents(trace, ("restored", "begin"), ("restored", "end"));
    }

    /// <summary>
    /// Keeps synchronous observation on its owning thread while another receiver runs.
    /// </summary>
    [Fact]
    public void ConcurrentThreads_DoNotInheritOrReplaceEachOther()
    {
        using var fixture = new Fixture(_output);
        var parent = new Trace(_output);
        var child = new Trace(_output);
        var parentFiles = fixture.Create(parent.Observe);
        var childFiles = fixture.Create(child.Observe);
        Exception? childFailure = null;
        using (parentFiles.EnterOperationTimingContext())
        {
            var thread = new Thread(() =>
            {
                try
                {
                    Assert.Null(FileSystemManager.MeasureCurrentOperation("before-child-scope"));
                    using (childFiles.EnterOperationTimingContext())
                    using (FileSystemManager.MeasureCurrentOperation("child")) { }
                    Assert.Null(FileSystemManager.MeasureCurrentOperation("after-child-scope"));
                }
                catch (Exception failure) { childFailure = failure; }
            }) { IsBackground = true };
            thread.Start();
            if (!thread.Join(TimeSpan.FromSeconds(10)))
            {
                fixture.Retain();
                throw new TimeoutException("Original diagnostic worker did not join; fixture retained.");
            }
            Assert.Null(childFailure);
            using (FileSystemManager.MeasureCurrentOperation("parent")) { }
        }
        AssertEvents(parent, ("parent", "begin"), ("parent", "end"));
        AssertEvents(child, ("child", "begin"), ("child", "end"));
    }

    /// <summary>
    /// Preserves the existing direct manager timing entry outside any current context.
    /// </summary>
    [Fact]
    public void DirectTiming_DoesNotRequireCurrentContext()
    {
        using var fixture = new Fixture(_output);
        var trace = new Trace(_output);
        var files = fixture.Create(trace.Observe);
        using (files.MeasureOperation("direct")) { }
        AssertEvents(trace, ("direct", "begin"), ("direct", "end"));
    }

    /// <summary>
    /// Compares exact boundaries and finite durations after every event was emitted by its receiver.
    /// </summary>
    /// <param name="trace">
    /// The receiver whose entire retained event sequence is checked.
    /// </param>
    /// <param name="expected">
    /// The exact operation names and boundary order expected for this receiver.
    /// </param>
    private void AssertEvents(Trace trace, params (string Name, string Boundary)[] expected)
    {
        var events = trace.Events.ToArray();
        Assert.Equal(expected, events.Select(row => (row.Name, row.Boundary)).ToArray());
        Assert.All(events, row => Assert.True(double.IsFinite(row.Milliseconds) && row.Milliseconds >= 0));
    }

    /// <summary>
    /// Retains and emits every passive boundary, optionally throwing after capture.
    /// </summary>
    private sealed class Trace
    {
        private readonly bool _throwing;
        private readonly ITestOutputHelper _output;

        /// <summary>
        /// Gets the complete thread-safe event sequence without a retention limit.
        /// </summary>
        internal ConcurrentQueue<(string Name, string Boundary, double Milliseconds, int Thread)> Events { get; } = new();

        /// <summary>
        /// Selects the complete event receiver and optional callback failure.
        /// </summary>
        /// <param name="output">
        /// The receiver to which every boundary is emitted immediately.
        /// </param>
        /// <param name="throwing">
        /// Whether every callback throws an observation-only failure.
        /// </param>
        internal Trace(ITestOutputHelper output, bool throwing = false)
        {
            _output = output;
            _throwing = throwing;
        }

        /// <summary>
        /// Captures the actual operation boundary and current thread before any optional failure.
        /// </summary>
        /// <param name="name">
        /// The exact observed operation name.
        /// </param>
        /// <param name="boundary">
        /// The supplied begin or end boundary.
        /// </param>
        /// <param name="elapsed">
        /// The supplied inclusive elapsed duration.
        /// </param>
        internal void Observe(string name, string boundary, TimeSpan elapsed)
        {
            var thread = Environment.CurrentManagedThreadId;
            Events.Enqueue((name, boundary, elapsed.TotalMilliseconds, thread));
            _output.WriteLine(JsonSerializer.Serialize(new { Name = name, Boundary = boundary, Milliseconds = elapsed.TotalMilliseconds, Thread = thread }));
            if (_throwing) throw new InvalidOperationException("passive receiver failure sentinel");
        }
    }

    /// <summary>
    /// Owns a fresh marked filesystem namespace used only to construct diagnostic managers.
    /// </summary>
    private sealed class Fixture : IDisposable
    {
        private readonly string _parent;
        private readonly string _root;
        private readonly string _nonce = Guid.NewGuid().ToString("N");
        private readonly ITestOutputHelper _output;
        private bool _retained;

        /// <summary>
        /// Creates only a marked empty root, without game objects or canonical writes.
        /// </summary>
        /// <param name="output">
        /// The receiver for exact owned-root closing or retention evidence.
        /// </param>
        internal Fixture(ITestOutputHelper output)
        {
            _output = output;
            _parent = Path.GetFullPath(Path.Combine(TestRepoPaths.RepoRoot, "TestResults", "operation-timing-context"));
            Directory.CreateDirectory(_parent);
            _root = Path.Combine(_parent, _nonce);
            if (Directory.Exists(_root) || File.Exists(_root)) throw new InvalidDataException("Fixture destination exists.");
            Directory.CreateDirectory(_root);
            File.WriteAllText(Path.Combine(_root, ".fixture-owner"), _nonce);
        }

        /// <summary>
        /// Creates a real manager using only the selected passive receiver.
        /// </summary>
        /// <param name="observer">
        /// The passive receiver, or <see langword="null"/> to disable observation.
        /// </param>
        /// <returns>
        /// A manager rooted in this fixture, without a canonical operation or game setup.
        /// </returns>
        internal FileSystemManager Create(Action<string, string, TimeSpan>? observer) =>
            new(_root, NullLogger<FileSystemManager>.Instance, PhysicalLoadTransactionOperations.Instance,
                new FileSystemManagerHooks { OperationTimingObserver = observer });

        /// <summary>
        /// Retains the exact root when a worker's closing cannot be confirmed.
        /// </summary>
        internal void Retain() => _retained = true;

        /// <summary>
        /// Verifies the marker and removes only this owned ordinary fixture when closing is known.
        /// </summary>
        public void Dispose()
        {
            if (!_retained)
            {
                var scope = new TrustedLocalFileScope([_parent]);
                var marker = scope.ValidateFile(Path.Combine(_root, ".fixture-owner"), allowMissing: false);
                if (Path.GetDirectoryName(Path.GetFullPath(_root)) != _parent || Path.GetFileName(_root) != _nonce ||
                    File.ReadAllText(marker) != _nonce)
                    throw new InvalidDataException("Fixture closing ownership changed; root retained.");
                scope.DeleteOwnedTree(_root);
            }
            _output.WriteLine(JsonSerializer.Serialize(new { Root = _root, Retained = _retained, Closing = !Directory.Exists(_root) }));
            if (!_retained) Assert.False(Directory.Exists(_root));
        }
    }
}
