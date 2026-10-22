using System.Collections.Concurrent;
using System.Text.Json;
using BookOfEternityClient.Core;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;
using Xunit.Abstractions;

namespace BookOfEternityClient.Tests;

/// <summary>
/// Exercises bounded validation reads on tiny real files without preparing a game world.
/// </summary>
/// <param name="output">
/// Receiver for complete observations and owned fixture closing evidence.
/// </param>
public sealed class ReservedValidationReadSetTests(ITestOutputHelper output)
{
    /// <summary>
    /// Requires only first and exit source observations for repeated consumers of four distinct read identities.
    /// </summary>
    [Fact]
    public async Task RepeatedConsumersReadEachSourceOnlyAtEntryAndExit()
    {
        using var fixture = new Fixture(output);
        var files = fixture.Create();
        await using var lease = await files.AcquireCanonicalWriteLeaseAsync();
        files.GetOrCreateSessionGeneration(lease);
        fixture.Recording = true;
        for (var i = 0; i < 12; i++)
        {
            Assert.Equal("original", files.ReadFileSync(lease, "present.txt"));
            Assert.Null(files.ReadFileSync(lease, "missing.txt"));
            Assert.Equal("original", await files.ReadFileAsync(lease, "present.txt"));
            Assert.Null(await files.ReadFileAsync(lease, "missing.txt"));
        }
        Assert.Equal(8, fixture.PhysicalReads);
    }

    /// <summary>
    /// Owns an isolated marked root, exact timing observations and ordinary file payloads.
    /// </summary>
    /// <param name="output">
    /// Receiver for all observations, including unsuccessful cases.
    /// </param>
    private sealed class Fixture(ITestOutputHelper output) : IDisposable
    {
        private readonly string _nonce = Guid.NewGuid().ToString("N");
        private readonly string _parent = Path.GetFullPath("C:/Temp");
        private readonly ConcurrentQueue<(string Name, string Boundary, double Seconds)> _events = new();
        private string? _root;

        /// <summary>
        /// Gets or sets whether events belong to the measured consumer phase.
        /// </summary>
        internal bool Recording { get; set; }

        /// <summary>
        /// Gets the number of completed ordinary physical snapshot requests in the measured phase.
        /// </summary>
        internal int PhysicalReads => _events.Count(e => e.Boundary == "end" &&
            e.Name is "OrdinaryRead.Sync.Total" or "OrdinaryRead.Async.Total");

        /// <summary>
        /// Creates real ordinary files and a manager using passive timing observations only.
        /// </summary>
        /// <returns>
        /// The actual manager for this fixture's newly owned root.
        /// </returns>
        internal FileSystemManager Create()
        {
            _root = Path.Combine(_parent, "boe-reserved-read-" + _nonce);
            Directory.CreateDirectory(Path.Combine(_root, "game_session"));
            File.WriteAllText(Path.Combine(_root, ".fixture-owner"), _nonce);
            File.WriteAllText(Path.Combine(_root, "game_session", "present.txt"), "original");
            return new(_root, NullLogger<FileSystemManager>.Instance,
                PhysicalLoadTransactionOperations.Instance, new FileSystemManagerHooks
                {
                    OperationTimingObserver = (name, boundary, elapsed) =>
                    {
                        if (Recording) _events.Enqueue((name, boundary, elapsed.TotalSeconds));
                    }
                });
        }

        /// <summary>
        /// Writes every retained observation and removes only the exact marked ordinary fixture tree.
        /// </summary>
        public void Dispose()
        {
            output.WriteLine(JsonSerializer.Serialize(_events.Select(e => new { e.Name, e.Boundary, e.Seconds })));
            if (_root == null) return;
            var scope = new TrustedLocalFileScope([_parent]);
            if (Path.GetDirectoryName(_root) != _parent ||
                Path.GetFileName(_root) != "boe-reserved-read-" + _nonce ||
                File.ReadAllText(scope.ValidateFile(Path.Combine(_root, ".fixture-owner"), false)) != _nonce)
                throw new InvalidDataException("Fixture ownership changed; root retained.");
            scope.DeleteOwnedTree(_root);
            output.WriteLine(JsonSerializer.Serialize(new { Root = _root, Closing = !Directory.Exists(_root) }));
            Assert.False(Directory.Exists(_root));
        }
    }
}
