using System.Collections.Concurrent;
using System.Text.Json;
using BookOfEternityClient.Core;
using BookOfEternityClient.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;
using Xunit.Abstractions;

namespace BookOfEternityClient.Tests;

/// <summary>
/// Exercises selected GameState source lifetimes using tiny independent real file fixtures.
/// </summary>
/// <param name="output">
/// Receives every collected timing record and the verified owned cleanup result.
/// </param>
public sealed class GameStateDataReuseTests(ITestOutputHelper output)
{
    /// <summary>
    /// Coalesces eight original implicit requests into one initial source and one fresh final proof.
    /// </summary>
    /// <param name="synchronous">
    /// Whether to exercise the original synchronous text reader.
    /// </param>
    /// <returns>
    /// Completion after the real wrapper returns all original text and opens only two sources.
    /// </returns>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task EightImplicitReadsUseOneInitialAndOneFinalSource(bool synchronous)
    {
        using var fixture = new Fixture(output);
        await fixture.InitializeGenerationAsync();
        await SessionOperationContext.RunParticipatingCurrentSessionAsync(fixture.Files, () =>
            fixture.Files.ReadGameStateDataAsync(async () =>
            {
                fixture.Recording = true;
                for (var index = 0; index < 8; index++)
                    Assert.Equal("original", synchronous
                        ? fixture.Files.ReadFileSync("present.txt")
                        : await fixture.Files.ReadFileAsync("present.txt"));
                return true;
            }));
        Assert.Equal(2, fixture.Reads("present.txt"));
    }

    /// <summary>
    /// Owns only a nonce-marked root, original filesystem manager and uncapped passive observations.
    /// </summary>
    private sealed class Fixture : IDisposable
    {
        private readonly ITestOutputHelper _output;
        private readonly string _nonce = Guid.NewGuid().ToString("N");
        private readonly ConcurrentDictionary<string, int> _reads = new(StringComparer.Ordinal);
        private readonly ConcurrentQueue<(string Name, string Boundary, double Milliseconds)> _timings = new();

        /// <summary>
        /// Creates one ordinary source without constructing a world or game engine.
        /// </summary>
        /// <param name="output">
        /// Receives all passive observations after exact owned cleanup.
        /// </param>
        internal Fixture(ITestOutputHelper output)
        {
            _output = output;
            Root = Path.Combine(Path.GetTempPath(), "boe-game-state-data-" + _nonce);
            Directory.CreateDirectory(Path.Combine(Root, "game_session"));
            File.WriteAllText(Path.Combine(Root, ".fixture-owner"), _nonce);
            File.WriteAllText(Path.Combine(Root, "game_session", "present.txt"), "original");
            Files = new FileSystemManager(Root, NullLogger<FileSystemManager>.Instance,
                PhysicalLoadTransactionOperations.Instance, new FileSystemManagerHooks
                {
                    CanonicalReadActivityObserver = path =>
                    {
                        if (Recording) _reads.AddOrUpdate(path, 1, (_, count) => count + 1);
                    },
                    OperationTimingObserver = (name, boundary, elapsed) =>
                        _timings.Enqueue((name, boundary, elapsed.TotalMilliseconds))
                });
        }

        /// <summary>
        /// Gets the verified exclusively owned physical root.
        /// </summary>
        internal string Root { get; }

        /// <summary>
        /// Gets the actual filesystem manager used for every admission and read.
        /// </summary>
        internal FileSystemManager Files { get; }

        /// <summary>
        /// Gets or sets whether actual source observations contribute to the counters.
        /// </summary>
        internal bool Recording { get; set; }

        /// <summary>
        /// Counts actual observed canonical source openings for an exact original relative spelling.
        /// </summary>
        /// <param name="path">
        /// The original relative spelling whose recorded observations are counted.
        /// </param>
        /// <returns>
        /// The complete number of observed reads, including zero for an unobserved path.
        /// </returns>
        internal int Reads(string path) => _reads.GetValueOrDefault(path);

        /// <summary>
        /// Establishes a genuine existing generation before the read-only wrapper is entered.
        /// </summary>
        /// <returns>
        /// Completion after the original generation publication and owned lease release.
        /// </returns>
        internal async Task InitializeGenerationAsync()
        {
            await using var lease = await Files.AcquireCanonicalWriteLeaseAsync();
            Files.GetOrCreateSessionGeneration(lease);
        }

        /// <summary>
        /// Deletes only the verified nonce-owned tree and writes every collected diagnostic record.
        /// </summary>
        public void Dispose()
        {
            try
            {
                Assert.Equal("boe-game-state-data-" + _nonce, Path.GetFileName(Root));
                Assert.Equal(_nonce, File.ReadAllText(Path.Combine(Root, ".fixture-owner")));
                new TrustedLocalFileScope([Path.GetTempPath()]).DeleteOwnedTree(Root);
                Assert.False(Directory.Exists(Root));
            }
            finally
            {
                foreach (var row in _timings)
                    _output.WriteLine(JsonSerializer.Serialize(new { row.Name, row.Boundary, row.Milliseconds }));
                foreach (var row in _reads)
                    _output.WriteLine(JsonSerializer.Serialize(new { Path = row.Key, Sources = row.Value }));
                _output.WriteLine(JsonSerializer.Serialize(new { Root, Removed = !Directory.Exists(Root) }));
            }
        }
    }
}
