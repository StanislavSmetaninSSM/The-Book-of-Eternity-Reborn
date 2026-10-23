using System.Collections.Concurrent;
using System.Text;
using System.Text.Json;
using BookOfEternityClient.Core;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;
using Xunit.Abstractions;

namespace BookOfEternityClient.Tests;

/// <summary>
/// Exercises completed-proof DATA inside the real trusted-local prepared publication callback.
/// </summary>
/// <param name="output">
/// Receives every passive timing record and the verified nonce-owned cleanup result.
/// </param>
public sealed class CleanupProofDataReuseTests(ITestOutputHelper output)
{
    /// <summary>
    /// Coalesces eight same-family proof reads after actual recovery without changing publication images.
    /// </summary>
    /// <param name="synchronous">
    /// Whether the callback uses the original synchronous byte reader rather than its asynchronous family.
    /// </param>
    /// <returns>
    /// Completion after committed target bytes, retained bytes and two actual proof source reads are verified.
    /// </returns>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task PreparedProofUsesOneInitialAndOneFreshCompletionSource(bool synchronous)
    {
        using var fixture = new Fixture(output);
        var files = fixture.Files;
        await using var lease = await files.AcquireCanonicalWriteLeaseAsync();
        files.GetOrCreateSessionGeneration(lease);
        var callbacks = 0;
        var outcome = await files.PublishLocalFilesAsync(lease,
            [new CanonicalLocalFileChange("present.txt", Encoding.UTF8.GetBytes("original"), Encoding.UTF8.GetBytes("published"))],
            validatePreparedNamespace: async () =>
            {
                callbacks++;
                files.ResolveBackupPublicationRecovery(lease);
                fixture.Recording = true;
                try
                {
                    var result = await files.ReadCleanupCompletedProofDataAsync(lease, async () =>
                    {
                        for (var index = 0; index < 8; index++)
                        {
                            var bytes = synchronous ? files.ReadFileBytesSync(lease, "present.txt")
                                : await files.ReadFileBytesAsync(lease, "present.txt");
                            Assert.NotNull(bytes);
                            Assert.Equal("original", Encoding.UTF8.GetString(bytes));
                            bytes[0] = 0;
                        }
                        return 17;
                    });
                    Assert.Equal(17, result);
                }
                finally { fixture.Recording = false; }
            },
            reusePreparedProofData: true);

        Assert.Equal(TrustedLocalPublicationDisposition.Committed, outcome.Disposition);
        Assert.Null(outcome.Failure);
        Assert.Equal(1, callbacks);
        Assert.True(lease.IsActive);
        Assert.Equal(Encoding.UTF8.GetBytes("published"), File.ReadAllBytes(fixture.PresentPath));
        Assert.Equal(Encoding.UTF8.GetBytes("retained"), File.ReadAllBytes(fixture.RetainedPath));
        Assert.Equal(Encoding.UTF8.GetBytes("published"), await files.ReadFileBytesAsync(lease, "present.txt"));
        Assert.Equal(Encoding.UTF8.GetBytes("retained"), await files.ReadFileBytesAsync(lease, "retained.txt"));
        Assert.Equal(2, fixture.ProofSources);
    }

    /// <summary>
    /// Owns independent ordinary files and records actual uncached source boundaries without functional hooks.
    /// </summary>
    private sealed class Fixture : IDisposable
    {
        private readonly ITestOutputHelper _output;
        private readonly string _nonce = Guid.NewGuid().ToString("N");
        private readonly ConcurrentQueue<(string Name, string Boundary, double Milliseconds)> _timings = new();
        private int _proofSources;

        /// <summary>
        /// Creates one publication member and one retained image on an exclusively owned root.
        /// </summary>
        /// <param name="output">
        /// Receives all observations after exact owned cleanup, without a diagnostic quota.
        /// </param>
        internal Fixture(ITestOutputHelper output)
        {
            _output = output;
            Root = Path.Combine(Path.GetTempPath(), "boe-cleanup-proof-data-" + _nonce);
            Directory.CreateDirectory(Path.Combine(Root, "game_session"));
            File.WriteAllText(Path.Combine(Root, ".fixture-owner"), _nonce);
            File.WriteAllBytes(PresentPath, Encoding.UTF8.GetBytes("original"));
            File.WriteAllBytes(RetainedPath, Encoding.UTF8.GetBytes("retained"));
            Files = new FileSystemManager(Root, NullLogger<FileSystemManager>.Instance,
                PhysicalLoadTransactionOperations.Instance, new FileSystemManagerHooks
                {
                    OperationTimingObserver = (name, boundary, elapsed) =>
                    {
                        _timings.Enqueue((name, boundary, elapsed.TotalMilliseconds));
                        if (Recording && boundary == "begin" &&
                            name is "OrdinaryRead.Sync.Total" or "OrdinaryRead.Async.Total")
                            Interlocked.Increment(ref _proofSources);
                    }
                });
        }

        /// <summary>
        /// Gets the exclusively owned nonce-marked root.
        /// </summary>
        internal string Root { get; }

        /// <summary>
        /// Gets the actual declared ordinary publication member.
        /// </summary>
        internal string PresentPath => Path.Combine(Root, "game_session", "present.txt");

        /// <summary>
        /// Gets the independent file whose exact retained bytes must survive publication.
        /// </summary>
        internal string RetainedPath => Path.Combine(Root, "game_session", "retained.txt");

        /// <summary>
        /// Gets the production manager issuing the original lease, recovery and publication.
        /// </summary>
        internal FileSystemManager Files { get; }

        /// <summary>
        /// Gets or sets whether ordinary physical-source timers belong to the selected proof window.
        /// </summary>
        internal bool Recording { get; set; }

        /// <summary>
        /// Gets the complete number of actual ordinary source reads during the selected proof helper.
        /// </summary>
        internal int ProofSources => Volatile.Read(ref _proofSources);

        /// <summary>
        /// Deletes only the exact owned root and emits every collected timing record and source count.
        /// </summary>
        public void Dispose()
        {
            try
            {
                Assert.Equal("boe-cleanup-proof-data-" + _nonce, Path.GetFileName(Root));
                Assert.Equal(_nonce, File.ReadAllText(Path.Combine(Root, ".fixture-owner")));
                new TrustedLocalFileScope([Path.GetTempPath()]).DeleteOwnedTree(Root);
                Assert.False(Directory.Exists(Root));
            }
            finally
            {
                foreach (var row in _timings)
                    _output.WriteLine(JsonSerializer.Serialize(new { row.Name, row.Boundary, row.Milliseconds }));
                _output.WriteLine(JsonSerializer.Serialize(new { Root, ProofSources, Removed = !Directory.Exists(Root) }));
            }
        }
    }
}
