using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using System.Security.Cryptography;
using System.Runtime.Versioning;
using BookOfEternityClient.Core;
using BookOfEternityClient.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed class PendingTurnSnapshotAuthorityTests : IDisposable
{
    private sealed class TestManifest
    {
        public string SessionId { get; set; } = string.Empty;
        public string RequestId { get; set; } = string.Empty;
        public int TurnNumber { get; set; }
        public string RequestTimestamp { get; set; } = string.Empty;
        public string PlayerAction { get; set; } = string.Empty;
        public Dictionary<string, string> Files { get; set; } = new(StringComparer.OrdinalIgnoreCase);
        public Dictionary<string, string> SnapshotFileHashes { get; set; } = new(StringComparer.OrdinalIgnoreCase);
        public Dictionary<string, string> ClientOwnedValidationHashes { get; set; } = new(StringComparer.OrdinalIgnoreCase);
        public Dictionary<string, string> RollbackBackups { get; set; } = new(StringComparer.OrdinalIgnoreCase);
        public List<string> RollbackBaselineFiles { get; set; } = new();
        public string? SourceLabel { get; set; }
        public string ManifestPayloadHash { get; set; } = string.Empty;
    }

    private static readonly JsonSerializerOptions ManifestJsonOpts = new()
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };

    private readonly string _rootPath;
    private readonly FileSystemManager _fs;

    public PendingTurnSnapshotAuthorityTests()
    {
        _rootPath = Path.Combine(Path.GetTempPath(), "boe-pending-snapshot-authority-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_rootPath);
        _fs = new FileSystemManager(_rootPath, NullLogger<FileSystemManager>.Instance);
        _fs.EnsureDirectoryStructure();
    }

    [Fact]
    public async Task CreateDetachedAuthorityJson_WritesPortableIntegrityEnvelopeWithoutLegacySignature()
    {
        var manifest = await CreateManifestWithSnapshotAndRollbackAsync(turnNumber: 6);

        var authorityJson = CreateDetachedAuthorityJson(manifest);
        var envelope = JsonNode.Parse(authorityJson)?.AsObject();

        Assert.NotNull(envelope);
        Assert.Equal(4, envelope["formatVersion"]?.GetValue<int>());
        Assert.Equal("SHA256-PAYLOAD-JSON", envelope["integrityAlgorithm"]?.GetValue<string>());
        Assert.False(envelope.ContainsKey("payloadSignature"));
        Assert.False(string.IsNullOrWhiteSpace(envelope["payloadJsonBase64"]?.GetValue<string>()));
        Assert.False(string.IsNullOrWhiteSpace(envelope["payloadSha256"]?.GetValue<string>()));
    }

    [Fact]
    public async Task TryValidateManifestAgainstAuthority_PortableIntegrityEnvelope_ValidatesWithoutLegacySignature()
    {
        var manifest = await CreateManifestWithSnapshotAndRollbackAsync(turnNumber: 7);
        var authorityJson = CreatePortableAuthorityJson(manifest);

        var valid = TryValidateReaderAuthority(manifest, authorityJson, out var payload, out var failureCode);

        Assert.True(valid);
        Assert.NotNull(payload);
        Assert.Equal("authorized", failureCode);
        Assert.Equal(manifest.ManifestPayloadHash, payload.ManifestPayloadHash);
    }

    [Fact]
    public async Task TryValidateManifestAgainstAuthority_LegacyWindowsSignatureEnvelope_RemainsReadableOnWindows()
    {
        if (!OperatingSystem.IsWindows())
            return;

        var manifest = await CreateManifestWithSnapshotAndRollbackAsync(turnNumber: 11);
        var authorityJson = CreateLegacyWindowsAuthorityJson(manifest);

        var valid = TryValidateReaderAuthority(manifest, authorityJson, out var payload, out var failureCode);

        Assert.True(valid);
        Assert.NotNull(payload);
        Assert.Equal("authorized", failureCode);
        Assert.Equal(manifest.ManifestPayloadHash, payload.ManifestPayloadHash);
    }

    [Fact]
    public async Task TryValidateManifestAgainstAuthority_TamperedPortablePayloadJson_FailsClosed()
    {
        var manifest = await CreateManifestWithSnapshotAndRollbackAsync(turnNumber: 8);
        var authority = JsonNode.Parse(CreatePortableAuthorityJson(manifest))!.AsObject();
        var payloadJson = Encoding.UTF8.GetString(Convert.FromBase64String(authority["payloadJsonBase64"]!.GetValue<string>()));
        var payload = JsonNode.Parse(payloadJson)!.AsObject();
        payload["turnNumber"] = manifest.TurnNumber + 1;
        authority["payloadJsonBase64"] = Convert.ToBase64String(Encoding.UTF8.GetBytes(payload.ToJsonString(ManifestJsonOpts)));

        var valid = TryValidateReaderAuthority(
            manifest,
            authority.ToJsonString(ManifestJsonOpts),
            out _,
            out var failureCode);

        Assert.False(valid);
        Assert.Equal("invalid_detached_authority", failureCode);
    }

    [Fact]
    public async Task TryValidateManifestAgainstAuthority_TamperedSnapshotHash_FailsClosed()
    {
        var manifest = await CreateManifestWithSnapshotAndRollbackAsync(turnNumber: 9);
        var authorityJson = CreatePortableAuthorityJson(manifest);
        manifest.SnapshotFileHashes["game_state/meta/soul_state.json"] = ComputeSha256("""
        {
          "currentRealm": "Chaos Sea"
        }
        """);
        manifest.ManifestPayloadHash = ComputeManifestPayloadHash(manifest);

        var valid = TryValidateReaderAuthority(manifest, authorityJson, out _, out var failureCode);

        Assert.False(valid);
        Assert.Equal("detached_authority_mismatch", failureCode);
    }

    [Fact]
    public async Task TryValidateManifestAgainstAuthority_TamperedManifestPayload_FailsClosed()
    {
        var manifest = await CreateManifestWithSnapshotAndRollbackAsync(turnNumber: 12);
        var authorityJson = CreatePortableAuthorityJson(manifest);
        manifest.PlayerAction = "tampered-authority-test";

        var valid = TryValidateReaderAuthority(manifest, authorityJson, out _, out var failureCode);

        Assert.False(valid);
        Assert.Equal("manifest_payload_hash_mismatch", failureCode);
    }

    [Fact]
    public async Task TryValidateManifestForDestructiveAuthority_MissingAuthority_FailsClosed()
    {
        var manifest = await CreateManifestWithSnapshotAndRollbackAsync(turnNumber: 10);

        var valid = TryValidateDestructiveAuthority(manifest, authorityJson: null, out _, out var failureCode);

        Assert.False(valid);
        Assert.Equal("missing_detached_authority", failureCode);
    }

    [Fact]
    public async Task TryValidateManifestAgainstAuthority_TamperedRollbackBackup_FailsClosed()
    {
        const string logicalPath = "game_state/meta/soul_state.json";
        const string snapshotPath = "game_state/control/pending_turn_snapshot/game_state/meta/soul_state.json";
        const string rollbackPath = "game_state/meta/soul_state.json.rollback.test";

        await _fs.WriteFileAtomicAsync(snapshotPath, """
        {
          "currentRealm": "Mortal World"
        }
        """);
        await _fs.WriteFileAtomicAsync(rollbackPath, """
        {
          "currentRealm": "Mortal World"
        }
        """);

        var manifest = new TestManifest
        {
            SessionId = "session",
            RequestId = "request",
            TurnNumber = 7,
            RequestTimestamp = "2026-04-14T00:00:00Z",
            PlayerAction = "authority-test",
            Files = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                [logicalPath] = snapshotPath
            },
            SnapshotFileHashes = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                [logicalPath] = ComputeSha256(File.ReadAllText(_fs.ResolvePath(snapshotPath), Encoding.UTF8))
            },
            RollbackBackups = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                [logicalPath] = rollbackPath
            },
            RollbackBaselineFiles = new List<string> { logicalPath },
            SourceLabel = "authority-tests"
        };
        manifest.ManifestPayloadHash = ComputeManifestPayloadHash(manifest);

        var authorityJson = CreateDetachedAuthorityJson(manifest);

        await _fs.WriteFileAtomicAsync(rollbackPath, """
        {
          "currentRealm": "Chaos Sea"
        }
        """);

        var valid = TryValidateReaderAuthority(manifest, authorityJson, out _, out var failureCode);

        Assert.False(valid);
        Assert.Equal("detached_authority_mismatch", failureCode);
    }

    [Fact]
    public async Task TryValidateManifestAgainstAuthority_RollbackBackupBomChanged_FailsClosed()
    {
        const string logicalPath = "game_state/meta/soul_state.json";
        var manifest = await CreateManifestWithSnapshotAndRollbackAsync(turnNumber: 13);
        var authorityJson = CreateDetachedAuthorityJson(manifest);
        var rollbackPath = manifest.RollbackBackups[logicalPath];
        var originalBytes = await _fs.ReadFileBytesAsync(rollbackPath);

        Assert.NotNull(originalBytes);
        var preamble = Encoding.UTF8.GetPreamble();
        Assert.True(originalBytes.AsSpan().StartsWith(preamble));

        await _fs.WriteFileAtomicBytesAsync(rollbackPath, originalBytes[preamble.Length..]);

        var valid = TryValidateReaderAuthority(manifest, authorityJson, out _, out var failureCode);

        Assert.False(valid);
        Assert.Equal("detached_authority_mismatch", failureCode);
    }

    [Fact]
    public void TryValidateManifestAgainstAuthority_UnsafeSnapshotPath_FailsStructure()
    {
        var manifest = new TestManifest
        {
            SessionId = "session",
            RequestId = "request",
            TurnNumber = 8,
            RequestTimestamp = "2026-04-14T00:00:00Z",
            PlayerAction = "authority-test",
            Files = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["game_state/meta/soul_state.json"] = "../outside.json"
            },
            SnapshotFileHashes = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["game_state/meta/soul_state.json"] = "ABC123"
            },
            RollbackBaselineFiles = new List<string> { "game_state/meta/soul_state.json" },
            SourceLabel = "authority-tests"
        };
        manifest.ManifestPayloadHash = ComputeManifestPayloadHash(manifest);

        var authorityJson = CreateDetachedAuthorityJson(manifest);

        var valid = TryValidateReaderAuthority(manifest, authorityJson, out _, out var failureCode);

        Assert.False(valid);
        Assert.Equal("invalid_manifest_structure", failureCode);
    }

    [Fact]
    public void HasValidatedSnapshotCoverage_MissingRequiredRollbackBaselineRegistration_FailsClosed()
    {
        var manifest = new TestManifest
        {
            SessionId = "session",
            RequestId = "request",
            TurnNumber = 9,
            RequestTimestamp = "2026-04-14T00:00:00Z",
            PlayerAction = "authority-test",
            Files = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["game_state/meta/soul_state.json"] = "game_state/control/pending_turn_snapshot/game_state/meta/soul_state.json"
            },
            SnapshotFileHashes = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["game_state/meta/soul_state.json"] = "ABC123"
            },
            RollbackBaselineFiles = new List<string>(),
            SourceLabel = "authority-tests"
        };

        var covered = PendingTurnSnapshotAuthority.HasValidatedSnapshotCoverage(
            manifest,
            static snapshotManifest => snapshotManifest.Files,
            static snapshotManifest => snapshotManifest.SnapshotFileHashes,
            new[] { "game_state/meta/soul_state.json" },
            out var missingPath,
            static snapshotManifest => snapshotManifest.RollbackBaselineFiles,
            requireRollbackBaselineRegistration: true);

        Assert.False(covered);
        Assert.Equal("game_state/meta/soul_state.json", missingPath);
    }

    private string? ReadRelativeFile(string relativePath)
    {
        if (!PendingTurnSnapshotAuthority.IsSafeRelativePath(relativePath))
            return null;

        var fullPath = _fs.ResolvePath(relativePath);
        if (!File.Exists(fullPath))
            return null;

        return File.ReadAllText(fullPath, Encoding.UTF8);
    }

    private byte[]? ReadRelativeFileBytes(string relativePath)
    {
        if (!PendingTurnSnapshotAuthority.IsSafeRelativePath(relativePath))
            return null;

        var fullPath = _fs.ResolvePath(relativePath);
        return File.Exists(fullPath) ? File.ReadAllBytes(fullPath) : null;
    }

    private static string ComputeManifestPayloadHash(TestManifest manifest)
    {
        return PendingTurnSnapshotAuthority.ComputeManifestPayloadHash(
            manifest,
            ManifestJsonOpts,
            static snapshotManifest => snapshotManifest.ManifestPayloadHash,
            static (snapshotManifest, hash) => snapshotManifest.ManifestPayloadHash = hash);
    }

    private static string ComputeSha256(string content)
    {
        return PendingTurnSnapshotAuthority.ComputeSha256(content);
    }

    private static string ComputeSha256(byte[] content)
    {
        return Convert.ToHexString(SHA256.HashData(content)).ToLowerInvariant();
    }

    private async Task<TestManifest> CreateManifestWithSnapshotAndRollbackAsync(int turnNumber)
    {
        const string logicalPath = "game_state/meta/soul_state.json";
        const string snapshotPath = "game_state/control/pending_turn_snapshot/game_state/meta/soul_state.json";
        var rollbackPath = $"game_state/meta/soul_state.json.rollback.test-{turnNumber}";
        const string json = """
        {
          "currentRealm": "Mortal World"
        }
        """;

        await _fs.WriteFileAtomicAsync(snapshotPath, json);
        await _fs.WriteFileAtomicAsync(rollbackPath, json);
        var snapshotBytes = (await _fs.ReadFileBytesAsync(snapshotPath))!;

        var manifest = new TestManifest
        {
            SessionId = "session",
            RequestId = "request",
            TurnNumber = turnNumber,
            RequestTimestamp = "2026-04-14T00:00:00Z",
            PlayerAction = "authority-test",
            Files = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                [logicalPath] = snapshotPath
            },
            SnapshotFileHashes = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                [logicalPath] = PendingTurnSnapshotAuthority.ComputeSha256(snapshotBytes)
            },
            RollbackBackups = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                [logicalPath] = rollbackPath
            },
            RollbackBaselineFiles = new List<string> { logicalPath },
            SourceLabel = "authority-tests"
        };
        manifest.ManifestPayloadHash = ComputeManifestPayloadHash(manifest);
        return manifest;
    }

    private string CreateDetachedAuthorityJson(TestManifest manifest) =>
        PendingTurnSnapshotAuthority.CreateDetachedAuthorityJson(
            manifest,
            ManifestJsonOpts,
            static snapshotManifest => snapshotManifest.ManifestPayloadHash,
            static (snapshotManifest, hash) => snapshotManifest.ManifestPayloadHash = hash,
            static snapshotManifest => snapshotManifest.SessionId,
            static snapshotManifest => snapshotManifest.RequestId,
            static snapshotManifest => snapshotManifest.TurnNumber,
            static snapshotManifest => snapshotManifest.Files,
            static snapshotManifest => snapshotManifest.SnapshotFileHashes,
            static snapshotManifest => snapshotManifest.ClientOwnedValidationHashes,
            static snapshotManifest => snapshotManifest.RollbackBaselineFiles,
            static snapshotManifest => snapshotManifest.SourceLabel,
            static snapshotManifest => snapshotManifest.RollbackBackups,
            ReadRelativeFileBytes);

    private bool TryValidateReaderAuthority(
        TestManifest manifest,
        string? authorityJson,
        out PendingTurnSnapshotAuthority.PendingTurnSnapshotAuthorityPayload? payload,
        out string failureCode) =>
        PendingTurnSnapshotAuthority.TryValidateManifestForReaderAuthority(
            manifest,
            authorityJson,
            ManifestJsonOpts,
            static snapshotManifest => snapshotManifest.ManifestPayloadHash,
            static (snapshotManifest, hash) => snapshotManifest.ManifestPayloadHash = hash,
            static snapshotManifest => snapshotManifest.SessionId,
            static snapshotManifest => snapshotManifest.RequestId,
            static snapshotManifest => snapshotManifest.TurnNumber,
            static snapshotManifest => snapshotManifest.Files,
            static snapshotManifest => snapshotManifest.SnapshotFileHashes,
            static snapshotManifest => snapshotManifest.ClientOwnedValidationHashes,
            static snapshotManifest => snapshotManifest.RollbackBaselineFiles,
            static snapshotManifest => snapshotManifest.SourceLabel,
            static snapshotManifest => snapshotManifest.RollbackBackups,
            ReadRelativeFileBytes,
            out payload,
            out failureCode);

    private bool TryValidateDestructiveAuthority(
        TestManifest manifest,
        string? authorityJson,
        out PendingTurnSnapshotAuthority.PendingTurnSnapshotAuthorityPayload? payload,
        out string failureCode) =>
        PendingTurnSnapshotAuthority.TryValidateManifestForDestructiveAuthority(
            manifest,
            authorityJson,
            ManifestJsonOpts,
            static snapshotManifest => snapshotManifest.ManifestPayloadHash,
            static (snapshotManifest, hash) => snapshotManifest.ManifestPayloadHash = hash,
            static snapshotManifest => snapshotManifest.SessionId,
            static snapshotManifest => snapshotManifest.RequestId,
            static snapshotManifest => snapshotManifest.TurnNumber,
            static snapshotManifest => snapshotManifest.Files,
            static snapshotManifest => snapshotManifest.SnapshotFileHashes,
            static snapshotManifest => snapshotManifest.ClientOwnedValidationHashes,
            static snapshotManifest => snapshotManifest.RollbackBaselineFiles,
            static snapshotManifest => snapshotManifest.SourceLabel,
            static snapshotManifest => snapshotManifest.RollbackBackups,
            ReadRelativeFileBytes,
            out payload,
            out failureCode);

    private string CreatePortableAuthorityJson(TestManifest manifest)
    {
        var payload = CreateAuthorityPayload(manifest);
        var payloadJson = JsonSerializer.Serialize(payload, ManifestJsonOpts);
        var envelope = new JsonObject
        {
            ["formatVersion"] = 2,
            ["integrityAlgorithm"] = "SHA256-PAYLOAD-JSON",
            ["payloadJsonBase64"] = Convert.ToBase64String(Encoding.UTF8.GetBytes(payloadJson)),
            ["payloadSha256"] = ComputeSha256(payloadJson)
        };

        return envelope.ToJsonString(ManifestJsonOpts);
    }

    [SupportedOSPlatform("windows")]
    private string CreateLegacyWindowsAuthorityJson(TestManifest manifest)
    {
        var payload = CreateAuthorityPayload(manifest);
        var payloadBytes = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(payload, ManifestJsonOpts));
        using var signer = OpenOrCreateLegacyWindowsSigner();
        var envelope = new JsonObject
        {
            ["payloadJsonBase64"] = Convert.ToBase64String(payloadBytes),
            ["payloadSignature"] = Convert.ToBase64String(signer.SignData(payloadBytes, HashAlgorithmName.SHA256))
        };

        return envelope.ToJsonString(ManifestJsonOpts);
    }

    private PendingTurnSnapshotAuthority.PendingTurnSnapshotAuthorityPayload CreateAuthorityPayload(TestManifest manifest)
    {
        var rollbackBackups = CopyNormalizedFilesDictionary(manifest.RollbackBackups);
        return new PendingTurnSnapshotAuthority.PendingTurnSnapshotAuthorityPayload
        {
            SessionId = manifest.SessionId.Trim(),
            RequestId = manifest.RequestId.Trim(),
            TurnNumber = manifest.TurnNumber,
            ManifestPayloadHash = ComputeManifestPayloadHash(manifest),
            Files = CopyNormalizedFilesDictionary(manifest.Files),
            SnapshotFileHashes = CopyNormalizedHashDictionary(manifest.SnapshotFileHashes),
            ClientOwnedValidationHashes = CopyNormalizedHashDictionary(manifest.ClientOwnedValidationHashes),
            RollbackBackups = rollbackBackups,
            RollbackBackupHashes = ComputeFileHashes(rollbackBackups),
            RollbackBaselineFiles = manifest.RollbackBaselineFiles
                .Where(path => !string.IsNullOrWhiteSpace(path))
                .Select(NormalizePath)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
                .ToList(),
            SourceLabel = string.IsNullOrWhiteSpace(manifest.SourceLabel)
                ? null
                : manifest.SourceLabel.Trim()
        };
    }

    [SupportedOSPlatform("windows")]
    private static ECDsa OpenOrCreateLegacyWindowsSigner()
    {
        const string authorityKeyName = "BookOfEternityClient.PendingTurnSnapshotAuthority";
        if (CngKey.Exists(authorityKeyName, CngProvider.MicrosoftSoftwareKeyStorageProvider))
            return new ECDsaCng(CngKey.Open(authorityKeyName, CngProvider.MicrosoftSoftwareKeyStorageProvider));

        var creationParameters = new CngKeyCreationParameters
        {
            Provider = CngProvider.MicrosoftSoftwareKeyStorageProvider,
            KeyUsage = CngKeyUsages.Signing
        };

        return new ECDsaCng(CngKey.Create(CngAlgorithm.ECDsaP256, authorityKeyName, creationParameters));
    }

    private Dictionary<string, string> ComputeFileHashes(IDictionary<string, string> files)
    {
        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var (logicalPath, relativePath) in files)
        {
            var content = ReadRelativeFile(relativePath);
            Assert.NotNull(content);
            result[logicalPath] = ComputeSha256(content);
        }

        return result;
    }

    private static Dictionary<string, string> CopyNormalizedFilesDictionary(IDictionary<string, string> source) =>
        source
            .Where(pair => !string.IsNullOrWhiteSpace(pair.Key) && !string.IsNullOrWhiteSpace(pair.Value))
            .ToDictionary(
                pair => NormalizePath(pair.Key),
                pair => NormalizePath(pair.Value),
                StringComparer.OrdinalIgnoreCase);

    private static Dictionary<string, string> CopyNormalizedHashDictionary(IDictionary<string, string> source) =>
        source
            .Where(pair => !string.IsNullOrWhiteSpace(pair.Key) && !string.IsNullOrWhiteSpace(pair.Value))
            .ToDictionary(
                pair => NormalizePath(pair.Key),
                pair => pair.Value.Trim(),
                StringComparer.OrdinalIgnoreCase);

    private static string NormalizePath(string value) => value.Replace('\\', '/').Trim();

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(_rootPath))
                Directory.Delete(_rootPath, recursive: true);
        }
        catch
        {
            // best-effort cleanup
        }
    }
}
