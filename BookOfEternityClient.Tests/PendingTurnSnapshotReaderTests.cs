using System.Reflection;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using BookOfEternityClient.Core;
using BookOfEternityClient.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed class PendingTurnSnapshotReaderTests : IDisposable
{
    private const string RequiredPath =
        "game_state/control/pending_mortal_wound_occurrences.json";

    private readonly string _root;
    private readonly FileSystemManager _fs;

    public PendingTurnSnapshotReaderTests()
    {
        _root = Path.Combine(
            Path.GetTempPath(),
            "boe-pending-snapshot-reader-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_root);
        _fs = new FileSystemManager(
            _root,
            NullLogger<FileSystemManager>.Instance);
        _fs.EnsureDirectoryStructure();
    }

    [Fact]
    public void Api_IsOneLeaseBoundReadSurfaceWithDetachedSnapshotBytes()
    {
        var reader = RequiredType("PendingTurnSnapshotReader");
        var method = Assert.Single(
            reader.GetMethods(BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic),
            candidate => candidate.Name == "ReadCurrent");
        Assert.Collection(
            method.GetParameters(),
            parameter => Assert.Equal(typeof(FileSystemManager), parameter.ParameterType),
            parameter => Assert.Equal(
                typeof(FileSystemManager.CanonicalWriteLease),
                parameter.ParameterType),
            parameter => Assert.Equal(
                typeof(IReadOnlyCollection<string>),
                parameter.ParameterType));
        Assert.Equal("PendingTurnSnapshotReadResult", method.ReturnType.Name);
        Assert.Equal(
            new[] { "Issues", "Snapshot", "Success" },
            method.ReturnType
                .GetProperties(BindingFlags.Public | BindingFlags.Instance)
                .Select(property => property.Name)
                .OrderBy(name => name));

        var snapshot = RequiredType("PendingTurnSnapshotReadAuthority");
        Assert.Equal(
            new[] { "Realm", "RequestId", "SessionId", "SnapshotToken", "TurnNumber" },
            snapshot.GetProperties(BindingFlags.Public | BindingFlags.Instance)
                .Select(property => property.Name)
                .OrderBy(name => name));
        var read = Assert.Single(
            snapshot.GetMethods(BindingFlags.Public | BindingFlags.Instance),
            candidate => candidate.Name == "ReadRequiredBytes");
        Assert.Equal(typeof(byte[]), read.ReturnType);
        Assert.Equal(typeof(string), Assert.Single(read.GetParameters()).ParameterType);

        Assert.DoesNotContain(
            reader.GetMethods(BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic),
            candidate => candidate.Name.Contains("Write", StringComparison.OrdinalIgnoreCase) ||
                         candidate.Name.Contains("Delete", StringComparison.OrdinalIgnoreCase) ||
                         candidate.Name.Contains("Publish", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task ReadCurrent_ReturnsTheExactSignedPreTurnBytesNotTheChangedLiveRoot()
    {
        const string signedJson = "{\"schemaVersion\":1,\"occurrences\":[]}";
        const string changedLiveJson = "{\"schemaVersion\":1,\"occurrences\":[{}]}";
        await _fs.WriteFileAtomicAsync(RequiredPath, signedJson);
        var signedBytes = File.ReadAllBytes(_fs.ResolvePath(RequiredPath));
        await PrepareAsync();
        await _fs.WriteFileAtomicAsync(RequiredPath, changedLiveJson);
        var before = SnapshotTree();

        PendingTurnSnapshotReadResult result;
        await using (var lease = await _fs.AcquireCanonicalWriteLeaseAsync())
            result = PendingTurnSnapshotReader.ReadCurrent(_fs, lease, new[] { RequiredPath });

        Assert.True(result.Success, Describe(result.Issues));
        Assert.Empty(result.Issues);
        var snapshot = Assert.IsType<PendingTurnSnapshotReadAuthority>(result.Snapshot);
        Assert.Equal("snapshot-session-71", snapshot.SessionId);
        Assert.Equal("snapshot-request-71", snapshot.RequestId);
        Assert.Equal(71, snapshot.TurnNumber);
        Assert.Equal("mortal_world", snapshot.Realm);
        Assert.Matches("^[0-9A-F]{64}$", snapshot.SnapshotToken);
        Assert.Equal(signedBytes, snapshot.ReadRequiredBytes(RequiredPath));
        AssertTreeEqual(before, SnapshotTree());
    }

    [Fact]
    public async Task ReadCurrent_ReturnsDetachedBytesOnEveryAccess()
    {
        const string signedJson = "{\"schemaVersion\":1,\"occurrences\":[]}";
        await _fs.WriteFileAtomicAsync(RequiredPath, signedJson);
        var signedBytes = File.ReadAllBytes(_fs.ResolvePath(RequiredPath));
        await PrepareAsync();

        PendingTurnSnapshotReadResult result;
        await using (var lease = await _fs.AcquireCanonicalWriteLeaseAsync())
            result = PendingTurnSnapshotReader.ReadCurrent(_fs, lease, new[] { RequiredPath });

        Assert.True(result.Success, Describe(result.Issues));
        var first = result.Snapshot!.ReadRequiredBytes(RequiredPath);
        first[0] = (byte)'!';
        var second = result.Snapshot.ReadRequiredBytes(RequiredPath);
        Assert.Equal(signedBytes, second);
        Assert.NotSame(first, second);
    }

    [Fact]
    public async Task ReadCurrent_ReturnsExactSignedOptionalSubsetAndDetachedAcceptedDice()
    {
        const string coveredOptionalPath = "game_state/world/current_location.json";
        const string absentOptionalPath = "game_state/quests/regular_quests.json";
        await _fs.WriteFileAtomicAsync(
            RequiredPath,
            "{\"schemaVersion\":1,\"occurrences\":[]}");
        await _fs.WriteFileAtomicAsync(
            coveredOptionalPath,
            "{\"locationId\":\"loc_reader_test\"}");
        await PrepareAsync();

        PendingTurnSnapshotReadResult result;
        await using (var lease = await _fs.AcquireCanonicalWriteLeaseAsync())
        {
            result = PendingTurnSnapshotReader.ReadCurrent(
                _fs,
                lease,
                new PendingTurnSnapshotPathSelection(
                    new[] { RequiredPath },
                    new[] { coveredOptionalPath, absentOptionalPath }));
        }

        Assert.True(result.Success, Describe(result.Issues));
        var snapshot = Assert.IsType<PendingTurnSnapshotReadAuthority>(result.Snapshot);
        Assert.Equal(
            new[] { RequiredPath, coveredOptionalPath }.OrderBy(
                static path => path,
                StringComparer.Ordinal),
            snapshot.CoveredLogicalPaths);
        Assert.Equal(new[] { 3, 17 }, snapshot.AcceptedD20EventValues);
        Assert.Throws<NotSupportedException>(() =>
            Assert.IsAssignableFrom<IList<int>>(snapshot.AcceptedD20EventValues)[0] = 20);
        Assert.Throws<KeyNotFoundException>(() => snapshot.ReadRequiredBytes(absentOptionalPath));
    }

    [Theory]
    [InlineData("missing_coverage")]
    [InlineData("stale_context")]
    [InlineData("tampered_snapshot")]
    [InlineData("tampered_manifest")]
    [InlineData("tampered_authority")]
    public async Task ReadCurrent_RejectsEveryUnsignedMissingOrStaleBoundaryWithoutWriting(
        string scenario)
    {
        await _fs.WriteFileAtomicAsync(
            RequiredPath,
            "{\"schemaVersion\":1,\"occurrences\":[]}");
        await PrepareAsync();
        var requestedPath = RequiredPath;

        switch (scenario)
        {
            case "missing_coverage":
                requestedPath = "game_state/control/not_in_snapshot.json";
                break;
            case "stale_context":
            {
                var request = ReadRoot(LiveTurnPreparationService.TurnRequestPath);
                request["requestId"] = "different-request";
                await _fs.WriteFileAtomicAsync(
                    LiveTurnPreparationService.TurnRequestPath,
                    request.ToJsonString());
                break;
            }
            case "tampered_snapshot":
            {
                var manifest = ReadRoot(
                    LiveTurnPreparationService.PendingTurnSnapshotManifestPath);
                var snapshotPath = manifest["files"]![RequiredPath]!.GetValue<string>();
                File.WriteAllText(
                    _fs.ResolvePath(snapshotPath),
                    "{\"schemaVersion\":1,\"occurrences\":[{}]}");
                break;
            }
            case "tampered_manifest":
            {
                var manifest = ReadRoot(
                    LiveTurnPreparationService.PendingTurnSnapshotManifestPath);
                manifest["turnNumber"] = 72;
                await _fs.WriteFileAtomicAsync(
                    LiveTurnPreparationService.PendingTurnSnapshotManifestPath,
                    manifest.ToJsonString());
                break;
            }
            case "tampered_authority":
                await _fs.WriteFileAtomicAsync(
                    PendingTurnSnapshotAuthority.AuthorityPath,
                    "{}");
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(scenario), scenario, null);
        }

        var before = SnapshotTree();
        PendingTurnSnapshotReadResult result;
        await using (var lease = await _fs.AcquireCanonicalWriteLeaseAsync())
            result = PendingTurnSnapshotReader.ReadCurrent(_fs, lease, new[] { requestedPath });

        Assert.False(result.Success);
        Assert.Null(result.Snapshot);
        Assert.NotEmpty(result.Issues);
        AssertTreeEqual(before, SnapshotTree());
    }

    [Fact]
    public async Task ReadCurrent_RejectsLegacyTextHashAuthorityWhenOnlySnapshotEncodingChanges()
    {
        const string signedJson = "{\"schemaVersion\":1,\"occurrences\":[]}";
        await _fs.WriteFileAtomicAsync(RequiredPath, signedJson);
        await PrepareAsync();
        var manifest = await RewriteAuthorityForLegacyTextHashesAsync();
        var snapshotPath = manifest.Files[RequiredPath];
        var snapshotBytes = File.ReadAllBytes(_fs.ResolvePath(snapshotPath));
        var preamble = Encoding.UTF8.GetPreamble();
        var encodingChangedBytes = snapshotBytes.AsSpan().StartsWith(preamble)
            ? snapshotBytes[preamble.Length..]
            : preamble.Concat(snapshotBytes).ToArray();
        await _fs.WriteFileAtomicBytesAsync(snapshotPath, encodingChangedBytes);
        var before = SnapshotTree();

        PendingTurnSnapshotReadResult result;
        await using (var lease = await _fs.AcquireCanonicalWriteLeaseAsync())
            result = PendingTurnSnapshotReader.ReadCurrent(_fs, lease, new[] { RequiredPath });

        Assert.False(result.Success);
        Assert.Null(result.Snapshot);
        Assert.Contains(
            result.Issues,
            issue => issue.Code == "pending_turn_snapshot_reader_hash_mode_invalid");
        AssertTreeEqual(before, SnapshotTree());
    }

    [Theory]
    [InlineData("ready/turn_complete.json")]
    [InlineData("game_state/control/validation_repair_request.json")]
    public async Task ReadCurrent_RejectsMatchingOlderSignalWhenCurrentTurnRequestDisagrees(
        string matchingSignalPath)
    {
        await _fs.WriteFileAtomicAsync(
            RequiredPath,
            "{\"schemaVersion\":1,\"occurrences\":[]}");
        await PrepareAsync();
        await WriteContextAsync(
            matchingSignalPath,
            "snapshot-session-71",
            "snapshot-request-71",
            71);
        await WriteContextAsync(
            LiveTurnPreparationService.TurnRequestPath,
            "snapshot-session-71",
            "newer-request-72",
            72);
        var before = SnapshotTree();

        PendingTurnSnapshotReadResult result;
        await using (var lease = await _fs.AcquireCanonicalWriteLeaseAsync())
            result = PendingTurnSnapshotReader.ReadCurrent(_fs, lease, new[] { RequiredPath });

        Assert.False(result.Success);
        Assert.Null(result.Snapshot);
        Assert.Contains(
            result.Issues,
            issue => issue.Code == "pending_turn_snapshot_reader_context_conflict");
        AssertTreeEqual(before, SnapshotTree());
    }

    [Fact]
    public async Task ReadCurrent_RejectsDiagnosticOnlyRepairAsTheSoleCurrentContext()
    {
        await _fs.WriteFileAtomicAsync(
            RequiredPath,
            "{\"schemaVersion\":1,\"occurrences\":[]}");
        await PrepareAsync();
        _fs.DeleteFile(LiveTurnPreparationService.TurnRequestPath);
        await WriteContextAsync(
            "game_state/control/validation_repair_request.json",
            "snapshot-session-71",
            "snapshot-request-71",
            71,
            metadataDiagnosticOnly: true);
        var before = SnapshotTree();

        PendingTurnSnapshotReadResult result;
        await using (var lease = await _fs.AcquireCanonicalWriteLeaseAsync())
            result = PendingTurnSnapshotReader.ReadCurrent(_fs, lease, new[] { RequiredPath });

        Assert.False(result.Success);
        Assert.Null(result.Snapshot);
        Assert.Contains(
            result.Issues,
            issue => issue.Code == "pending_turn_snapshot_reader_context_stale");
        AssertTreeEqual(before, SnapshotTree());
    }

    [Theory]
    [InlineData("")]
    [InlineData(" game_state/player/wounds.json")]
    [InlineData("game_state\\player\\wounds.json")]
    [InlineData("../outside.json")]
    public async Task ReadCurrent_RejectsNonCanonicalRequiredPaths(string path)
    {
        await _fs.WriteFileAtomicAsync(
            RequiredPath,
            "{\"schemaVersion\":1,\"occurrences\":[]}");
        await PrepareAsync();

        PendingTurnSnapshotReadResult result;
        await using (var lease = await _fs.AcquireCanonicalWriteLeaseAsync())
            result = PendingTurnSnapshotReader.ReadCurrent(_fs, lease, new[] { path });

        Assert.False(result.Success);
        Assert.Null(result.Snapshot);
        Assert.NotEmpty(result.Issues);
    }

    private Task<LiveTurnPreparationResult> PrepareAsync() =>
        new LiveTurnPreparationService(_fs).PrepareAsync(new LiveTurnPreparationOptions
        {
            SessionId = "snapshot-session-71",
            RequestId = "snapshot-request-71",
            TurnNumber = 71,
            CurrentRealm = "Mortal World",
            PlayerAction = "Проверить подписанный снимок состояния.",
            PreGeneratedDices1d20 = new[] { 3, 17 }
        });

    private async Task<LiveTurnPendingSnapshotManifest> RewriteAuthorityForLegacyTextHashesAsync()
    {
        var manifest = JsonSerializer.Deserialize<LiveTurnPendingSnapshotManifest>(
            File.ReadAllText(_fs.ResolvePath(
                LiveTurnPreparationService.PendingTurnSnapshotManifestPath)),
            LiveTurnPreparationService.ManifestJsonOptions)!;
        foreach (var (logicalPath, snapshotPath) in manifest.Files)
        {
            var decodedText = File.ReadAllText(_fs.ResolvePath(snapshotPath), Encoding.UTF8);
            manifest.SnapshotFileHashes[logicalPath] =
                PendingTurnSnapshotAuthority.ComputeSha256(decodedText);
        }

        manifest.ManifestPayloadHash = PendingTurnSnapshotAuthority.ComputeManifestPayloadHash(
            manifest,
            LiveTurnPreparationService.ManifestHashJsonOptions,
            static value => value.ManifestPayloadHash,
            static (value, hash) => value.ManifestPayloadHash = hash);
        var authorityJson = PendingTurnSnapshotAuthority.CreateDetachedAuthorityJson(
            manifest,
            LiveTurnPreparationService.ManifestHashJsonOptions,
            static value => value.ManifestPayloadHash,
            static (value, hash) => value.ManifestPayloadHash = hash,
            static value => value.SessionId,
            static value => value.RequestId,
            static value => value.TurnNumber,
            static value => value.Files,
            static value => value.SnapshotFileHashes,
            static value => value.ClientOwnedValidationHashes,
            static value => value.RollbackBaselineFiles,
            static value => value.SourceLabel,
            static value => value.RollbackBackups,
            _fs.ReadFileBytesSync,
            hashSnapshotBytesExactly: false);
        await _fs.WriteFileAtomicAsync(
            LiveTurnPreparationService.PendingTurnSnapshotManifestPath,
            JsonSerializer.Serialize(manifest, LiveTurnPreparationService.ManifestJsonOptions));
        await _fs.WriteFileAtomicAsync(
            PendingTurnSnapshotAuthority.AuthorityPath,
            authorityJson);
        return manifest;
    }

    private Task WriteContextAsync(
        string path,
        string sessionId,
        string requestId,
        int turnNumber,
        bool? metadataDiagnosticOnly = null)
    {
        var context = new JsonObject
        {
            ["sessionId"] = sessionId,
            ["requestId"] = requestId,
            ["turnNumber"] = turnNumber
        };
        if (metadataDiagnosticOnly.HasValue)
            context["metadataDiagnosticOnly"] = metadataDiagnosticOnly.Value;
        return _fs.WriteFileAtomicAsync(path, context.ToJsonString());
    }

    private JsonObject ReadRoot(string path) =>
        JsonNode.Parse(File.ReadAllText(_fs.ResolvePath(path)))!.AsObject();

    private Dictionary<string, byte[]> SnapshotTree() => Directory
        .EnumerateFiles(_root, "*", SearchOption.AllDirectories)
        .ToDictionary(
            path => Path.GetRelativePath(_root, path).Replace('\\', '/'),
            File.ReadAllBytes,
            StringComparer.OrdinalIgnoreCase);

    private static void AssertTreeEqual(
        IReadOnlyDictionary<string, byte[]> expected,
        IReadOnlyDictionary<string, byte[]> actual)
    {
        Assert.Equal(
            expected.Keys.OrderBy(path => path),
            actual.Keys.OrderBy(path => path));
        foreach (var (path, bytes) in expected)
        {
            Assert.True(actual.TryGetValue(path, out var actualBytes));
            Assert.True(bytes.AsSpan().SequenceEqual(actualBytes));
        }
    }

    private static string Describe(IEnumerable<ValidationIssue> issues) =>
        string.Join(Environment.NewLine, issues.Select(issue =>
            $"{issue.Code}: {issue.FilePath}: {issue.Actual}"));

    private static Type RequiredType(string name) =>
        typeof(WoundMaterializationContract).Assembly.GetType(
            "BookOfEternityClient.Services." + name,
            throwOnError: false,
            ignoreCase: false) ??
        throw new Xunit.Sdk.XunitException($"T064 requires {name}.");

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(_root))
                Directory.Delete(_root, recursive: true);
        }
        catch (IOException)
        {
            // A transient handle must not hide the reader contract result.
        }
    }
}
