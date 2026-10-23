using System.Collections.Concurrent;
using System.Text;
using System.Text.Json;
using BookOfEternityClient.Core;
using BookOfEternityClient.Services.GmRuntime;
using BookOfEternityClient.Services.GmWorkers;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;
using Xunit.Abstractions;

namespace BookOfEternityClient.Tests;

/// <summary>
/// Exercises provisional cached DATA separately from fresh authority and effect fences on tiny real files.
/// </summary>
/// <param name="output">
/// Receives complete diagnostic file references and verified owned cleanup results.
/// </param>
public sealed class LeaseDataAdmissionTests(ITestOutputHelper output)
{
    /// <summary>
    /// Supplies every normal declared reader and both present and absent DATA.
    /// </summary>
    /// <returns>
    /// Ten independent reader and source combinations.
    /// </returns>
    public static IEnumerable<object[]> Readers()
    {
        foreach (var reader in new[] { "async-text", "async-bytes", "async-snapshot", "sync-text", "sync-bytes" })
        foreach (var present in new[] { true, false }) yield return [reader, present];
    }

    /// <summary>
    /// Avoids all filesystem admission work for eight genuine hits and freshly verifies before completion.
    /// </summary>
    /// <param name="reader">
    /// Normal declared API exercised without changing its reader policy.
    /// </param>
    /// <param name="present">
    /// Whether the original DATA is present rather than absent.
    /// </param>
    /// <returns>
    /// Completion after source copies, zero repeated admission IO and a fresh owner fence are verified.
    /// </returns>
    [Theory]
    [MemberData(nameof(Readers))]
    public async Task EightWarmedReadsAvoidFullAdmission(string reader, bool present)
    {
        using var fixture = new Fixture(output);
        var files = fixture.Files;
        await using var lease = await files.AcquireCanonicalWriteLeaseAsync();
        files.GetOrCreateSessionGeneration(lease);
        using var operation = files.BeginSourceDataOperationIfNeeded();
        Assert.NotNull(operation);
        var path = present ? "present.txt" : "absent.txt";
        var expected = present ? "original" : null;
        Assert.Equal(expected, await Read(files, lease, path, reader));
        fixture.Counts.Clear();
        for (var i = 0; i < 8; i++) Assert.Equal(expected, await Read(files, lease, path, reader));
        Assert.Equal(0, fixture.Count("Lease.Validate.Total"));
        Assert.Equal(0, fixture.Count("CanonicalPath.Resolve"));
        Assert.Equal(8, fixture.Count("LeaseDataHit.Reuse"));
        await operation.CompleteAsync();
        Assert.True(fixture.Count("Lease.Validate.Total") > 0);
        Assert.True(fixture.Count("OperationFileData.FreshSource") > 0);
    }

    /// <summary>
    /// Supplies raw main and worker changes at each original escape boundary.
    /// </summary>
    /// <returns>
    /// Independent drift and effect combinations without game or world construction.
    /// </returns>
    public static IEnumerable<object[]> DriftCases()
    {
        foreach (var drift in new[] { "main-running", "main-malformed", "worker-directory", "worker-file", "namespace" })
        foreach (var boundary in new[] { "callback", "completion", "write", "explicit-admission", "miss" })
            yield return [drift, boundary];
    }

    /// <summary>
    /// Allows provisional DATA but refuses raw authority drift before callback, effects or success.
    /// </summary>
    /// <param name="drift">
    /// Untracked filesystem edit introduced after the first fully admitted read.
    /// </param>
    /// <param name="boundary">
    /// Original independently fresh boundary that must reject the changed authority.
    /// </param>
    /// <returns>
    /// Completion after the actual boundary refuses and no callback or file effect occurs.
    /// </returns>
    [Theory]
    [MemberData(nameof(DriftCases))]
    public async Task CachedDataDoesNotAuthorizeAnEscape(string drift, string boundary)
    {
        using var fixture = new Fixture(output);
        var files = fixture.Files;
        await using var lease = await files.AcquireCanonicalWriteLeaseAsync();
        files.GetOrCreateSessionGeneration(lease);
        using var operation = files.BeginSourceDataOperationIfNeeded();
        Assert.NotNull(operation);
        Assert.Equal("original", await files.ReadFileAsync(lease, "present.txt"));
        fixture.Drift(drift);
        var called = false;
        try
        {
            Assert.Equal("original", await files.ReadFileAsync(lease, "present.txt"));
            var failure = await Record.ExceptionAsync(async () =>
            {
                switch (boundary)
                {
                    case "callback": await files.InvokeRecoveryValidationCallbackAsync(() => { called = true; return Task.CompletedTask; }); break;
                    case "completion": await operation.CompleteAsync(); break;
                    case "write": await files.VerifySourceDataAsync(lease); await files.WriteFileAtomicAsync(lease, "effect.txt", "forbidden"); break;
                    case "explicit-admission": files.EnsureCanonicalWriteLeaseActive(lease); break;
                    case "miss": await files.ReadFileAsync(lease, "new-miss.txt"); break;
                    default: throw new ArgumentOutOfRangeException(nameof(boundary));
                }
            });
            Assert.NotNull(failure);
            Assert.False(called);
            Assert.False(File.Exists(Path.Combine(fixture.Root, "game_session", "effect.txt")));
        }
        finally { operation.Fail(); }
    }

    /// <summary>
    /// Freshly closes fast DATA before a callback and retires it across the callback's changes.
    /// </summary>
    /// <returns>
    /// Completion after the callback sees fresh admission and the next reader sees changed bytes.
    /// </returns>
    [Fact]
    public async Task CallbackVerifiesThenRetiresProvisionalData()
    {
        using var fixture = new Fixture(output);
        var files = fixture.Files;
        await using var lease = await files.AcquireCanonicalWriteLeaseAsync();
        files.GetOrCreateSessionGeneration(lease);
        using var operation = files.BeginSourceDataOperationIfNeeded();
        Assert.NotNull(operation);
        Assert.Equal("original", await files.ReadFileAsync(lease, "present.txt"));
        Assert.Equal("original", await files.ReadFileAsync(lease, "present.txt"));
        fixture.Counts.Clear();
        await files.InvokeRecoveryValidationCallbackAsync(() =>
        {
            Assert.True(fixture.Count("OperationFileData.FreshFence") > 0);
            File.WriteAllText(fixture.PresentPath, "changed");
            return Task.CompletedTask;
        });
        Assert.Equal("changed", await files.ReadFileAsync(lease, "present.txt"));
        await operation.CompleteAsync();
    }

    /// <summary>
    /// Calls the selected actual reader and mutates returned bytes to test detachment.
    /// </summary>
    /// <param name="files">
    /// Actual production manager owning the operation.
    /// </param>
    /// <param name="lease">
    /// Exact original current physical lease.
    /// </param>
    /// <param name="path">
    /// Original raw relative source spelling.
    /// </param>
    /// <param name="reader">
    /// One of the five declared source APIs.
    /// </param>
    /// <returns>
    /// Decoded original bytes or observed absence.
    /// </returns>
    private static async Task<string?> Read(FileSystemManager files, FileSystemManager.CanonicalWriteLease lease, string path, string reader)
    {
        if (reader == "async-text") return await files.ReadFileAsync(lease, path);
        if (reader == "sync-text") return files.ReadFileSync(lease, path);
        var bytes = reader switch
        {
            "async-bytes" => await files.ReadFileBytesAsync(lease, path),
            "async-snapshot" => (await files.ReadFileSnapshotAsync(lease, path))?.Content,
            "sync-bytes" => files.ReadFileBytesSync(lease, path),
            _ => throw new ArgumentOutOfRangeException(nameof(reader))
        };
        if (bytes == null) return null;
        var result = Encoding.UTF8.GetString(bytes);
        bytes[0] = 0;
        return result;
    }

    /// <summary>
    /// Owns an isolated tiny source and writes every timing row outside Git without quotas.
    /// </summary>
    private sealed class Fixture : IDisposable
    {
        private readonly ITestOutputHelper _output;
        private readonly string _nonce = Guid.NewGuid().ToString("N");
        private readonly StreamWriter _raw;
        private readonly string _rawPath;

        /// <summary>
        /// Creates a real manager, minimal source and complete passive timing receiver.
        /// </summary>
        /// <param name="output">
        /// Final diagnostic reference receiver.
        /// </param>
        internal Fixture(ITestOutputHelper output)
        {
            _output = output;
            Root = Path.Combine(Path.GetTempPath(), "boe-lease-data-" + _nonce);
            Directory.CreateDirectory(Path.Combine(Root, "game_session"));
            File.WriteAllText(Path.Combine(Root, ".fixture-owner"), _nonce);
            File.WriteAllText(PresentPath, "original");
            var diagnostics = Environment.GetEnvironmentVariable("BOE_TEST_DIAGNOSTIC_ROOT") ?? @"D:\BoeDiagnostics\lease-validation-r54-20261023";
            Directory.CreateDirectory(Path.Combine(diagnostics, "lease-data-unit"));
            _rawPath = Path.Combine(diagnostics, "lease-data-unit", _nonce + ".jsonl");
            _raw = new StreamWriter(new FileStream(_rawPath, FileMode.CreateNew, FileAccess.Write, FileShare.Read));
            Files = new FileSystemManager(Root, NullLogger<FileSystemManager>.Instance,
                PhysicalLoadTransactionOperations.Instance, new FileSystemManagerHooks
                {
                    OperationTimingObserver = (name, boundary, elapsed) =>
                    {
                        lock (_raw) _raw.WriteLine(JsonSerializer.Serialize(new { Name = name, Boundary = boundary, Milliseconds = elapsed.TotalMilliseconds }));
                        if (boundary == "end") Counts.AddOrUpdate(name, 1, (_, count) => count + 1);
                    }
                });
        }

        /// <summary>
        /// Gets the exact nonce-owned physical root.
        /// </summary>
        internal string Root { get; }

        /// <summary>
        /// Gets the real ordinary source path.
        /// </summary>
        internal string PresentPath => Path.Combine(Root, "game_session", "present.txt");

        /// <summary>
        /// Gets the actual manager used by all fixture calls.
        /// </summary>
        internal FileSystemManager Files { get; }

        /// <summary>
        /// Gets counters whose resets never remove raw diagnostic rows.
        /// </summary>
        internal ConcurrentDictionary<string, int> Counts { get; } = new();

        /// <summary>
        /// Reads the completed timing count without emitting filesystem observations.
        /// </summary>
        /// <param name="name">
        /// Exact named production timer.
        /// </param>
        /// <returns>
        /// Recorded completions, or zero when no completion occurred.
        /// </returns>
        internal int Count(string name) => Counts.GetValueOrDefault(name);

        /// <summary>
        /// Introduces real untracked authority drift without advancing the in-memory root epoch.
        /// </summary>
        /// <param name="kind">
        /// Main record, inventory or namespace mutation selected by the test.
        /// </param>
        internal void Drift(string kind)
        {
            var main = Path.Combine(Root, ".boe_runtime", "gm-runs", "main.json");
            var workers = new WorkerLedgerTarget(Root).DirectoryPath;
            switch (kind)
            {
                case "main-running":
                    var identity = new GmSessionRunIdentity(Root, Guid.NewGuid().ToString("N"), Guid.Empty.ToString("N"), 1,
                        GmSessionRunBackend.WindowsJob, Guid.NewGuid().ToString("N"), "fixture-native-observation");
                    Directory.CreateDirectory(Path.GetDirectoryName(main)!);
                    File.WriteAllBytes(main, GmSessionRunRecordCodec.Encode(new GmSessionRunRecord(1, identity, GmSessionRunDisposition.Running, null)));
                    break;
                case "main-malformed": Directory.CreateDirectory(Path.GetDirectoryName(main)!); File.WriteAllText(main, "malformed"); break;
                case "worker-directory": Directory.CreateDirectory(workers); break;
                case "worker-file": Directory.CreateDirectory(Path.GetDirectoryName(workers)!); File.WriteAllText(workers, "blocker"); break;
                case "namespace":
                    var directory = Path.GetDirectoryName(main)!;
                    Assert.False(Directory.Exists(directory));
                    Directory.CreateDirectory(Path.GetDirectoryName(directory)!);
                    File.WriteAllText(directory, "blocker");
                    break;
                default: throw new ArgumentOutOfRangeException(nameof(kind));
            }
        }

        /// <summary>
        /// Closes every diagnostic row and removes only the verified nonce-owned root.
        /// </summary>
        public void Dispose()
        {
            _raw.Dispose();
            Assert.Equal("boe-lease-data-" + _nonce, Path.GetFileName(Root));
            Assert.Equal(_nonce, File.ReadAllText(Path.Combine(Root, ".fixture-owner")));
            new TrustedLocalFileScope([Path.GetTempPath()]).DeleteOwnedTree(Root);
            using var stream = File.OpenRead(_rawPath);
            _output.WriteLine(JsonSerializer.Serialize(new { Boundary = "lease_data_diagnostics", Path = _rawPath, Bytes = stream.Length,
                SHA256 = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(stream)), Complete = true, Root, Removed = !Directory.Exists(Root) }));
            Assert.False(Directory.Exists(Root));
        }
    }
}
