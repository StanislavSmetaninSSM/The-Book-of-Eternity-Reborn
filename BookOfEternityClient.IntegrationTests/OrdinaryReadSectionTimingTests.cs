using System.Collections.Concurrent;
using System.Text.Json;
using BookOfEternityClient.Core;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;
using Xunit.Abstractions;

namespace BookOfEternityClient.Tests;

/// <summary>
/// Checks nested ordinary-read diagnostics on independent files without constructing a game world.
/// </summary>
public sealed class OrdinaryReadSectionTimingTests
{
    private readonly ITestOutputHelper _output;

    /// <summary>
    /// Retains the receiver for complete timing and owned-fixture closing evidence.
    /// </summary>
    /// <param name="output">
    /// The current test's diagnostic receiver, without message or event quotas.
    /// </param>
    public OrdinaryReadSectionTimingTests(ITestOutputHelper output) => _output = output;

    /// <summary>
    /// Preserves content, absence and invalid-entry refusals with complete or failing passive observers.
    /// </summary>
    /// <param name="mode">
    /// Sync or async selects the actual typed ordinary text-read entrypoint.
    /// </param>
    /// <param name="entry">
    /// Regular, missing or directory selects the owned file-boundary fixture.
    /// </param>
    /// <param name="throwingObserver">
    /// Whether every observed boundary throws after its complete observation is retained.
    /// </param>
    [Theory]
    [InlineData("sync", "regular", false)]
    [InlineData("sync", "regular", true)]
    [InlineData("async", "regular", false)]
    [InlineData("async", "regular", true)]
    [InlineData("sync", "missing", false)]
    [InlineData("sync", "missing", true)]
    [InlineData("async", "missing", false)]
    [InlineData("async", "missing", true)]
    [InlineData("sync", "directory", false)]
    [InlineData("sync", "directory", true)]
    [InlineData("async", "directory", false)]
    [InlineData("async", "directory", true)]
    public async Task OrdinaryTextRead_PreservesOutcomeAndNamedSections(string mode, string entry, bool throwingObserver)
    {
        using var fixture = new Fixture(_output, entry);
        var trace = new Trace(throwingObserver);
        var files = fixture.Create(new FileSystemManagerHooks { OperationTimingObserver = trace.Observe });
        string? actual = null;
        try
        {
            trace.Phase = "read";
            var failure = await Record.ExceptionAsync(async () =>
            {
                actual = mode == "sync" ? files.ReadFileSync("subject.txt") : await files.ReadFileAsync("subject.txt");
            });
            if (entry == "directory")
            {
                Assert.IsType<InvalidDataException>(failure);
                Assert.DoesNotContain("passive timing cut", failure!.ToString(), StringComparison.Ordinal);
                Assert.True(Directory.Exists(fixture.FilePath));
            }
            else
            {
                Assert.Null(failure);
                Assert.Equal(entry == "regular" ? Fixture.Payload : null, actual);
                if (entry == "regular") Assert.Equal(Fixture.Payload, File.ReadAllText(fixture.FilePath));
                else Assert.False(File.Exists(fixture.FilePath));
            }
            var prefix = mode == "sync" ? "OrdinaryRead.Sync." : "OrdinaryRead.Async.";
            trace.RequireBalanced();
            trace.RequireNames(prefix + "ResolvePath", prefix + "CreateScope",
                prefix + "Admission.CanonicalSafety", prefix + "Admission.ValidateFile");
            if (entry != "directory") trace.RequireNames(prefix + "Admission.FileExists");
            if (entry == "regular") trace.RequireNames("OrdinaryRead.Open.ValidateFile", "OrdinaryRead.Open.FileStream",
                "OrdinaryRead.Complete.ValidateFile", "OrdinaryRead.Complete.CanonicalSafety");
        }
        finally { trace.Emit(_output); }
    }

    /// <summary>
    /// Retains the original missing-open revalidation after a file disappears at the existing async boundary.
    /// </summary>
    /// <param name="throwingObserver">
    /// Whether timing callbacks fail after recording their events.
    /// </param>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task MissingAtOpen_RevalidatesWithoutReplacingAbsence(bool throwingObserver)
    {
        using var fixture = new Fixture(_output, "regular");
        var trace = new Trace(throwingObserver);
        var removed = false;
        var files = fixture.Create(new FileSystemManagerHooks
        {
            OperationTimingObserver = trace.Observe,
            BeforeCanonicalReadOpenAsync = _ =>
            {
                if (!removed) { File.Delete(fixture.FilePath); removed = true; }
                return Task.CompletedTask;
            }
        });
        try
        {
            trace.Phase = "read";
            Assert.Null(await files.ReadFileAsync("subject.txt"));
            Assert.True(removed);
            Assert.False(File.Exists(fixture.FilePath));
            trace.RequireBalanced();
            trace.RequireNames("OrdinaryRead.Open.ValidateFile", "OrdinaryRead.Open.FileStream",
                "OrdinaryRead.Open.RevalidateMissing");
        }
        finally { trace.Emit(_output); }
    }

    /// <summary>
    /// Keeps nullable data failures distinct from physical path refusals under a real owned lease.
    /// </summary>
    /// <param name="kind">
    /// Data-io injects a data-hook failure; path-refusal replaces only the owned leaf with a directory.
    /// </param>
    /// <param name="throwingObserver">
    /// Whether passive timing callbacks also fail after retaining their observations.
    /// </param>
    [Theory]
    [InlineData("data-io", false)]
    [InlineData("data-io", true)]
    [InlineData("path-refusal", false)]
    [InlineData("path-refusal", true)]
    public async Task NullableRead_RetainsDataFailureAndAuthorityDistinction(string kind, bool throwingObserver)
    {
        using var fixture = new Fixture(_output, "regular");
        var trace = new Trace(throwingObserver);
        var dataFailure = new IOException("owned nullable data cut");
        var files = fixture.Create(new FileSystemManagerHooks
        {
            OperationTimingObserver = trace.Observe,
            AfterCanonicalReadInitialValidationAsync = _ =>
            {
                if (kind == "data-io") return Task.FromException(dataFailure);
                File.Delete(fixture.FilePath);
                Directory.CreateDirectory(fixture.FilePath);
                return Task.CompletedTask;
            }
        });
        try
        {
            await using var lease = await files.AcquireCanonicalWriteLeaseAsync();
            trace.Phase = "read";
            if (kind == "data-io")
            {
                var result = files.ReadWorldValidationNullableBytesSync(lease, "subject.txt", notifyAttempt: false);
                Assert.Null(result.Bytes);
                Assert.Same(dataFailure, result.DataFailure?.SourceException);
                Assert.Equal(Fixture.Payload, File.ReadAllText(fixture.FilePath));
            }
            else
            {
                var refusal = Assert.Throws<InvalidDataException>(() =>
                    files.ReadWorldValidationNullableBytesSync(lease, "subject.txt", notifyAttempt: false));
                Assert.DoesNotContain("passive timing cut", refusal.ToString(), StringComparison.Ordinal);
                Assert.True(Directory.Exists(fixture.FilePath));
                trace.RequireNames("OrdinaryRead.Complete.ValidateFile");
            }
            trace.RequireBalanced();
            trace.RequireNames("OrdinaryRead.Sync.ResolvePath", "OrdinaryRead.Sync.CreateScope",
                "OrdinaryRead.Open.ValidateFile", "OrdinaryRead.Open.FileStream");
            trace.Phase = "closing";
        }
        finally { trace.Emit(_output); }
    }

    /// <summary>
    /// Retains every callback, including setup and closing, independently of assertion outcomes.
    /// </summary>
    /// <param name="throwingObserver">
    /// Whether each callback throws after recording its complete observation.
    /// </param>
    private sealed class Trace(bool throwingObserver)
    {
        private readonly ConcurrentQueue<(string Phase, string Name, string Boundary, TimeSpan Elapsed)> _events = new();

        /// <summary>
        /// Gets or sets the descriptive phase attached to subsequent events; setup is the initial value.
        /// </summary>
        internal string Phase { get; set; } = "setup";

        /// <summary>
        /// Records the exact timing callback before optionally simulating a passive observer failure.
        /// </summary>
        /// <param name="name">
        /// The actual named section emitted by the filesystem manager.
        /// </param>
        /// <param name="boundary">
        /// The actual begin or end boundary.
        /// </param>
        /// <param name="elapsed">
        /// The manager's zero-at-begin or inclusive-at-end duration.
        /// </param>
        internal void Observe(string name, string boundary, TimeSpan elapsed)
        {
            _events.Enqueue((Phase, name, boundary, elapsed));
            if (throwingObserver) throw new IOException("passive timing cut");
        }

        /// <summary>
        /// Requires every read-phase operation boundary to close in its original nested order.
        /// </summary>
        internal void RequireBalanced()
        {
            var stack = new Stack<string>();
            foreach (var row in _events.Where(row => row.Phase == "read"))
            {
                Assert.True(row.Elapsed >= TimeSpan.Zero);
                if (row.Boundary == "begin") stack.Push(row.Name);
                else { Assert.Equal("end", row.Boundary); Assert.NotEmpty(stack); Assert.Equal(stack.Pop(), row.Name); }
            }
            Assert.Empty(stack);
        }

        /// <summary>
        /// Requires actual read-phase begin and end observations for each requested diagnostic section.
        /// </summary>
        /// <param name="names">
        /// The exact required section names; an empty array imposes no name requirement.
        /// </param>
        internal void RequireNames(params string[] names)
        {
            foreach (var name in names)
                foreach (var boundary in new[] { "begin", "end" })
                    Assert.Contains(_events, row => row.Phase == "read" && row.Name == name && row.Boundary == boundary);
        }

        /// <summary>
        /// Writes all retained events without reducing or truncating the diagnostic stream.
        /// </summary>
        /// <param name="output">
        /// The current test's complete diagnostic receiver.
        /// </param>
        internal void Emit(ITestOutputHelper output) => output.WriteLine(JsonSerializer.Serialize(_events.Select(row => new
        { row.Phase, row.Name, row.Boundary, Milliseconds = row.Elapsed.TotalMilliseconds })));
    }

    /// <summary>
    /// Owns one marked ordinary namespace and closes it through the existing non-following tree remover.
    /// </summary>
    private sealed class Fixture : IDisposable
    {
        /// <summary>
        /// The exact text checked before and after the ordinary read.
        /// </summary>
        internal const string Payload = "owned ordinary read payload";
        private readonly string _parent;
        private readonly string _root;
        private readonly string _nonce = Guid.NewGuid().ToString("N");
        private readonly ITestOutputHelper _output;

        /// <summary>
        /// Gets the exact leaf created, omitted or replaced by this independent case.
        /// </summary>
        internal string FilePath => Path.Combine(_root, "game_session", "subject.txt");

        /// <summary>
        /// Creates a fresh marked root with only the selected ordinary leaf state.
        /// </summary>
        /// <param name="output">
        /// Receiver for the exact owned closing result.
        /// </param>
        /// <param name="entry">
        /// Regular, missing or directory selects the initial file-boundary state.
        /// </param>
        internal Fixture(ITestOutputHelper output, string entry)
        {
            _output = output;
            _parent = Path.GetFullPath(Path.Combine(TestRepoPaths.RepoRoot, "TestResults", "ordinary-read-section-timing"));
            Directory.CreateDirectory(_parent);
            _root = Path.Combine(_parent, _nonce);
            if (Directory.Exists(_root) || File.Exists(_root)) throw new InvalidDataException("Fixture destination already exists.");
            Directory.CreateDirectory(Path.Combine(_root, "game_session"));
            File.WriteAllText(Path.Combine(_root, ".fixture-owner"), _nonce);
            if (entry == "regular") File.WriteAllText(FilePath, Payload);
            else if (entry == "directory") Directory.CreateDirectory(FilePath);
            else if (entry != "missing") throw new ArgumentOutOfRangeException(nameof(entry));
        }

        /// <summary>
        /// Creates the actual manager with typed hooks and no game objects or substituted authority.
        /// </summary>
        /// <param name="hooks">
        /// The diagnostic and scheduling callbacks for this case.
        /// </param>
        /// <returns>
        /// The filesystem manager rooted in this fixture's independent namespace.
        /// </returns>
        internal FileSystemManager Create(FileSystemManagerHooks hooks) =>
            new(_root, NullLogger<FileSystemManager>.Instance, PhysicalLoadTransactionOperations.Instance, hooks);

        /// <summary>
        /// Verifies exact root ownership and removes only its freshly owned ordinary tree without following links.
        /// </summary>
        public void Dispose()
        {
            var scope = new TrustedLocalFileScope([_parent]);
            var marker = scope.ValidateFile(Path.Combine(_root, ".fixture-owner"), allowMissing: false);
            if (Path.GetDirectoryName(Path.GetFullPath(_root)) != _parent || Path.GetFileName(_root) != _nonce ||
                File.ReadAllText(marker) != _nonce)
                throw new InvalidDataException("Fixture closing ownership changed; root retained.");
            scope.DeleteOwnedTree(_root);
            _output.WriteLine(JsonSerializer.Serialize(new { Root = _root, Closing = !Directory.Exists(_root) }));
            Assert.False(Directory.Exists(_root));
        }
    }
}
