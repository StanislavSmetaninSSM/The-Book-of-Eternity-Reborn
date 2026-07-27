using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace BookOfEternityClient.Services;

internal static class PendingTurnSnapshotAuthority
{
    internal const string AuthorityPath = "game_state/control/pending_turn_snapshot.authority.json";
    private const int LegacyPortableAuthorityFormatVersion = 2;
    private const int ExactRollbackPortableAuthorityFormatVersion = 3;
    private const int PortableAuthorityFormatVersion = 4;
    private const string PortableIntegrityAlgorithm = "SHA256-PAYLOAD-JSON";
    internal const string ExactRollbackHashMode = "bytes";
    internal const string ExactSnapshotHashMode = "bytes";

    private static readonly JsonSerializerOptions AuthorityJsonOpts = new()
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };

    internal sealed class PendingTurnSnapshotAuthorityEnvelope
    {
        public int? FormatVersion { get; set; }
        public string? IntegrityAlgorithm { get; set; }
        public string PayloadJsonBase64 { get; set; } = string.Empty;
        public string? PayloadSha256 { get; set; }
        public string? PayloadSignature { get; set; }
    }

    internal sealed class PendingTurnSnapshotAuthorityPayload
    {
        public string SessionId { get; set; } = string.Empty;
        public string RequestId { get; set; } = string.Empty;
        public int TurnNumber { get; set; }
        public string ManifestPayloadHash { get; set; } = string.Empty;
        public Dictionary<string, string> Files { get; set; } = new(StringComparer.OrdinalIgnoreCase);
        public Dictionary<string, string> SnapshotFileHashes { get; set; } = new(StringComparer.OrdinalIgnoreCase);
        public string? SnapshotHashMode { get; set; }
        public Dictionary<string, string> ClientOwnedValidationHashes { get; set; } = new(StringComparer.OrdinalIgnoreCase);
        public Dictionary<string, string> RollbackBackups { get; set; } = new(StringComparer.OrdinalIgnoreCase);
        public Dictionary<string, string> RollbackBackupHashes { get; set; } = new(StringComparer.OrdinalIgnoreCase);
        public string? RollbackHashMode { get; set; }
        public List<string> RollbackBaselineFiles { get; set; } = new();
        public string? SourceLabel { get; set; }
    }

    internal static string ComputeManifestPayloadHash<TManifest>(
        TManifest manifest,
        JsonSerializerOptions options,
        Func<TManifest, string> getHash,
        Action<TManifest, string> setHash)
    {
        var originalHash = getHash(manifest);
        setHash(manifest, string.Empty);
        try
        {
            var payload = JsonSerializer.Serialize(manifest, options);
            return ComputeSha256(payload);
        }
        finally
        {
            setHash(manifest, originalHash);
        }
    }

    internal static string ComputeSha256(string content)
    {
        using var sha = SHA256.Create();
        var bytes = sha.ComputeHash(Encoding.UTF8.GetBytes(content));
        return Convert.ToHexString(bytes);
    }

    internal static bool DoesPendingTurnContextIdMatch(string manifestId, string contextId)
    {
        var hasManifestId = !string.IsNullOrWhiteSpace(manifestId);
        var hasContextId = !string.IsNullOrWhiteSpace(contextId);
        if (!hasManifestId && !hasContextId)
            return true;
        if (!hasManifestId || !hasContextId)
            return false;

        return string.Equals(manifestId, contextId, StringComparison.OrdinalIgnoreCase);
    }

    internal static bool IsSafeRelativePath(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return false;

        var trimmed = value.Trim();
        if (Path.IsPathRooted(trimmed) ||
            trimmed.StartsWith('/') ||
            trimmed.StartsWith('\\') ||
            trimmed.Contains(':', StringComparison.Ordinal))
        {
            return false;
        }

        var normalized = trimmed.Replace('\\', '/');
        var segments = normalized.Split('/', StringSplitOptions.None);
        foreach (var segment in segments)
        {
            if (string.IsNullOrWhiteSpace(segment) || segment == "." || segment == "..")
                return false;
        }

        return true;
    }

    internal static bool HasUsableManifestStructure<TManifest>(
        TManifest manifest,
        Func<TManifest, IDictionary<string, string>?> getFiles,
        Func<TManifest, IDictionary<string, string>?> getSnapshotFileHashes,
        Func<TManifest, IDictionary<string, string>?>? getClientOwnedValidationHashes = null,
        Func<TManifest, IDictionary<string, string>?>? getRollbackBackups = null,
        Func<TManifest, string?>? getSourceLabel = null,
        Func<TManifest, IEnumerable<string>?>? getRollbackBaselineFiles = null,
        bool requireSnapshotEntriesForRollbackBaselineFiles = false,
        bool requireRollbackArtifactPaths = true)
        where TManifest : class
    {
        if (manifest == null)
            return false;

        var files = getFiles(manifest);
        if (files == null || files.Count == 0)
            return false;

        var snapshotFileHashes = getSnapshotFileHashes(manifest);
        if (snapshotFileHashes == null || snapshotFileHashes.Count == 0)
            return false;

        foreach (var (relativePath, snapshotPath) in files)
        {
            if (string.IsNullOrWhiteSpace(relativePath) ||
                string.IsNullOrWhiteSpace(snapshotPath) ||
                !IsSafeRelativePath(relativePath) ||
                !IsSafeRelativePath(snapshotPath) ||
                !snapshotFileHashes.TryGetValue(relativePath, out var expectedSnapshotHash) ||
                string.IsNullOrWhiteSpace(expectedSnapshotHash))
            {
                return false;
            }
        }

        foreach (var (relativePath, expectedSnapshotHash) in snapshotFileHashes)
        {
            if (!IsSafeRelativePath(relativePath) || string.IsNullOrWhiteSpace(expectedSnapshotHash))
                return false;
        }

        if (getClientOwnedValidationHashes != null)
        {
            var clientOwnedValidationHashes = getClientOwnedValidationHashes(manifest);
            if (clientOwnedValidationHashes == null)
                return false;

            foreach (var baselinePath in clientOwnedValidationHashes.Keys)
            {
                if (!IsSafeRelativePath(baselinePath))
                    return false;
            }
        }

        if (getRollbackBackups != null)
        {
            var rollbackBackups = getRollbackBackups(manifest);
            if (rollbackBackups == null)
                return false;

            foreach (var (originalPath, backupPath) in rollbackBackups)
            {
                if (!IsSafeRelativePath(originalPath) ||
                    !IsSafeRelativePath(backupPath) ||
                    (requireRollbackArtifactPaths &&
                     !backupPath.Contains(".rollback.", StringComparison.OrdinalIgnoreCase)))
                {
                    return false;
                }
            }
        }

        if (getRollbackBaselineFiles != null)
        {
            var rollbackBaselineFiles = getRollbackBaselineFiles(manifest);
            if (rollbackBaselineFiles == null)
                return false;

            foreach (var baselinePath in rollbackBaselineFiles)
            {
                if (!IsSafeRelativePath(baselinePath))
                    return false;

                if (!requireSnapshotEntriesForRollbackBaselineFiles)
                    continue;

                if (!files.TryGetValue(baselinePath, out var baselineSnapshotPath) ||
                    string.IsNullOrWhiteSpace(baselineSnapshotPath) ||
                    !snapshotFileHashes.TryGetValue(baselinePath, out var baselineSnapshotHash) ||
                    string.IsNullOrWhiteSpace(baselineSnapshotHash))
                {
                    return false;
                }
            }
        }

        if (getSourceLabel != null && string.IsNullOrWhiteSpace(getSourceLabel(manifest)))
            return false;

        return true;
    }

    internal static bool HasValidatedSnapshotCoverage<TManifest>(
        TManifest manifest,
        Func<TManifest, IDictionary<string, string>?> getFiles,
        Func<TManifest, IDictionary<string, string>?> getSnapshotFileHashes,
        IEnumerable<string> requiredPaths,
        out string? missingPath,
        Func<TManifest, IEnumerable<string>?>? getRollbackBaselineFiles = null,
        bool requireRollbackBaselineRegistration = false)
        where TManifest : class
    {
        missingPath = null;
        if (manifest == null)
            return false;

        var files = getFiles(manifest);
        var snapshotFileHashes = getSnapshotFileHashes(manifest);
        if (files == null || snapshotFileHashes == null)
            return false;

        HashSet<string>? rollbackBaselineSet = null;
        if (requireRollbackBaselineRegistration)
        {
            var rollbackBaselineFiles = getRollbackBaselineFiles?.Invoke(manifest);
            if (rollbackBaselineFiles == null)
                return false;

            rollbackBaselineSet = new HashSet<string>(
                rollbackBaselineFiles
                    .Where(path => !string.IsNullOrWhiteSpace(path))
                    .Select(NormalizePath),
                StringComparer.OrdinalIgnoreCase);
        }

        foreach (var path in requiredPaths)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                missingPath = path;
                return false;
            }

            var normalizedPath = NormalizePath(path);
            if (!IsSafeRelativePath(normalizedPath))
            {
                missingPath = normalizedPath;
                return false;
            }

            if (rollbackBaselineSet != null && !rollbackBaselineSet.Contains(normalizedPath))
            {
                missingPath = normalizedPath;
                return false;
            }

            if (!files.TryGetValue(normalizedPath, out var snapshotPath) ||
                string.IsNullOrWhiteSpace(snapshotPath) ||
                !IsSafeRelativePath(snapshotPath) ||
                !snapshotFileHashes.TryGetValue(normalizedPath, out var expectedSnapshotHash) ||
                string.IsNullOrWhiteSpace(expectedSnapshotHash))
            {
                missingPath = normalizedPath;
                return false;
            }
        }

        return true;
    }

    internal static string CreateDetachedAuthorityJson<TManifest>(
        TManifest manifest,
        JsonSerializerOptions manifestHashOptions,
        Func<TManifest, string> getHash,
        Action<TManifest, string> setHash,
        Func<TManifest, string> getSessionId,
        Func<TManifest, string> getRequestId,
        Func<TManifest, int> getTurnNumber,
        Func<TManifest, IDictionary<string, string>?> getFiles,
        Func<TManifest, IDictionary<string, string>?> getSnapshotFileHashes,
        Func<TManifest, IDictionary<string, string>?> getClientOwnedValidationHashes,
        Func<TManifest, IEnumerable<string>?> getRollbackBaselineFiles,
        Func<TManifest, string?> getSourceLabel,
        Func<TManifest, IDictionary<string, string>?>? getRollbackBackups = null,
        Func<string, byte[]?>? readRelativeFileBytes = null,
        bool hashSnapshotBytesExactly = true)
        where TManifest : class
    {
        var payload = BuildAuthorityPayload(
            manifest,
            manifestHashOptions,
            getHash,
            setHash,
            getSessionId,
            getRequestId,
            getTurnNumber,
            getFiles,
            getSnapshotFileHashes,
            getClientOwnedValidationHashes,
            getRollbackBaselineFiles,
            getSourceLabel,
            getRollbackBackups,
            readRelativeFileBytes,
            hashRollbackBytesExactly: true,
            hashSnapshotBytesExactly);

        var payloadBytes = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(payload, AuthorityJsonOpts));
        var envelope = new PendingTurnSnapshotAuthorityEnvelope
        {
            FormatVersion = hashSnapshotBytesExactly
                ? PortableAuthorityFormatVersion
                : ExactRollbackPortableAuthorityFormatVersion,
            IntegrityAlgorithm = PortableIntegrityAlgorithm,
            PayloadJsonBase64 = Convert.ToBase64String(payloadBytes),
            PayloadSha256 = ComputeSha256(payloadBytes)
        };

        return JsonSerializer.Serialize(envelope, AuthorityJsonOpts);
    }

    internal static bool TryValidateManifestForDestructiveAuthority<TManifest>(
        TManifest manifest,
        string? authorityJson,
        JsonSerializerOptions manifestHashOptions,
        Func<TManifest, string> getHash,
        Action<TManifest, string> setHash,
        Func<TManifest, string> getSessionId,
        Func<TManifest, string> getRequestId,
        Func<TManifest, int> getTurnNumber,
        Func<TManifest, IDictionary<string, string>?> getFiles,
        Func<TManifest, IDictionary<string, string>?> getSnapshotFileHashes,
        Func<TManifest, IDictionary<string, string>?> getClientOwnedValidationHashes,
        Func<TManifest, IEnumerable<string>?> getRollbackBaselineFiles,
        Func<TManifest, string?> getSourceLabel,
        Func<TManifest, IDictionary<string, string>?>? getRollbackBackups,
        Func<string, byte[]?>? readRelativeFileBytes,
        out PendingTurnSnapshotAuthorityPayload? payload,
        out string failureCode)
        where TManifest : class
    {
        return TryValidateManifestAgainstAuthority(
            manifest,
            authorityJson,
            manifestHashOptions,
            getHash,
            setHash,
            getSessionId,
            getRequestId,
            getTurnNumber,
            getFiles,
            getSnapshotFileHashes,
            getClientOwnedValidationHashes,
            getRollbackBaselineFiles,
            getSourceLabel,
            getRollbackBackups,
            readRelativeFileBytes,
            out payload,
            out failureCode,
            requireRollbackArtifactPaths: true);
    }

    internal static bool TryValidateManifestForReaderAuthority<TManifest>(
        TManifest manifest,
        string? authorityJson,
        JsonSerializerOptions manifestHashOptions,
        Func<TManifest, string> getHash,
        Action<TManifest, string> setHash,
        Func<TManifest, string> getSessionId,
        Func<TManifest, string> getRequestId,
        Func<TManifest, int> getTurnNumber,
        Func<TManifest, IDictionary<string, string>?> getFiles,
        Func<TManifest, IDictionary<string, string>?> getSnapshotFileHashes,
        Func<TManifest, IDictionary<string, string>?> getClientOwnedValidationHashes,
        Func<TManifest, IEnumerable<string>?> getRollbackBaselineFiles,
        Func<TManifest, string?> getSourceLabel,
        Func<TManifest, IDictionary<string, string>?> getRollbackBackups,
        Func<string, byte[]?> readRelativeFileBytes,
        out PendingTurnSnapshotAuthorityPayload? payload,
        out string failureCode)
        where TManifest : class
    {
        return TryValidateManifestAgainstAuthority(
            manifest,
            authorityJson,
            manifestHashOptions,
            getHash,
            setHash,
            getSessionId,
            getRequestId,
            getTurnNumber,
            getFiles,
            getSnapshotFileHashes,
            getClientOwnedValidationHashes,
            getRollbackBaselineFiles,
            getSourceLabel,
            getRollbackBackups,
            readRelativeFileBytes,
            out payload,
            out failureCode,
            requireRollbackArtifactPaths: false);
    }

    internal static bool TryValidateManifestAgainstAuthority<TManifest>(
        TManifest manifest,
        string? authorityJson,
        JsonSerializerOptions manifestHashOptions,
        Func<TManifest, string> getHash,
        Action<TManifest, string> setHash,
        Func<TManifest, string> getSessionId,
        Func<TManifest, string> getRequestId,
        Func<TManifest, int> getTurnNumber,
        Func<TManifest, IDictionary<string, string>?> getFiles,
        Func<TManifest, IDictionary<string, string>?> getSnapshotFileHashes,
        Func<TManifest, IDictionary<string, string>?> getClientOwnedValidationHashes,
        Func<TManifest, IEnumerable<string>?> getRollbackBaselineFiles,
        Func<TManifest, string?> getSourceLabel,
        Func<TManifest, IDictionary<string, string>?>? getRollbackBackups,
        Func<string, byte[]?>? readRelativeFileBytes,
        out PendingTurnSnapshotAuthorityPayload? payload,
        out string failureCode,
        bool requireRollbackArtifactPaths)
        where TManifest : class
    {
        payload = null;
        failureCode = "invalid_manifest";

        if (manifest == null || string.IsNullOrWhiteSpace(getHash(manifest)))
            return false;

        if (!HasUsableManifestStructure(
                manifest,
                getFiles,
                getSnapshotFileHashes,
                getClientOwnedValidationHashes,
                getRollbackBackups,
                getSourceLabel,
                getRollbackBaselineFiles,
                requireSnapshotEntriesForRollbackBaselineFiles: false,
                requireRollbackArtifactPaths: requireRollbackArtifactPaths))
        {
            failureCode = "invalid_manifest_structure";
            return false;
        }

        var actualManifestHash = ComputeManifestPayloadHash(
            manifest,
            manifestHashOptions,
            getHash,
            setHash);
        if (!string.Equals(actualManifestHash, getHash(manifest), StringComparison.OrdinalIgnoreCase))
        {
            failureCode = "manifest_payload_hash_mismatch";
            return false;
        }

        if (!TryReadAuthorityPayload(authorityJson, out var actualPayload))
        {
            failureCode = string.IsNullOrWhiteSpace(authorityJson)
                ? "missing_detached_authority"
                : "invalid_detached_authority";
            return false;
        }

        if (actualPayload == null)
        {
            failureCode = "invalid_detached_authority";
            return false;
        }

        PendingTurnSnapshotAuthorityPayload expectedPayload;
        try
        {
            expectedPayload = BuildAuthorityPayload(
                manifest,
                manifestHashOptions,
                getHash,
                setHash,
                getSessionId,
                getRequestId,
                getTurnNumber,
                getFiles,
                getSnapshotFileHashes,
                getClientOwnedValidationHashes,
                getRollbackBaselineFiles,
                getSourceLabel,
                getRollbackBackups,
                readRelativeFileBytes,
                hashRollbackBytesExactly: string.Equals(
                    actualPayload.RollbackHashMode,
                    ExactRollbackHashMode,
                    StringComparison.Ordinal),
                hashSnapshotBytesExactly: string.Equals(
                    actualPayload.SnapshotHashMode,
                    ExactSnapshotHashMode,
                    StringComparison.Ordinal));
        }
        catch (InvalidOperationException)
        {
            failureCode = "rollback_backup_unreadable";
            return false;
        }

        if (!AuthorityPayloadEquals(expectedPayload, actualPayload, compareRollbackBackups: getRollbackBackups != null))
        {
            failureCode = "detached_authority_mismatch";
            return false;
        }

        payload = actualPayload;
        failureCode = "authorized";
        return true;
    }

    internal static bool TryValidateManifestAgainstAuthority<TManifest>(
        TManifest manifest,
        string? authorityJson,
        JsonSerializerOptions manifestHashOptions,
        Func<TManifest, string> getHash,
        Action<TManifest, string> setHash,
        Func<TManifest, string> getSessionId,
        Func<TManifest, string> getRequestId,
        Func<TManifest, int> getTurnNumber,
        Func<TManifest, IDictionary<string, string>?> getFiles,
        Func<TManifest, IDictionary<string, string>?> getSnapshotFileHashes,
        Func<TManifest, IDictionary<string, string>?> getClientOwnedValidationHashes,
        Func<TManifest, IEnumerable<string>?> getRollbackBaselineFiles,
        Func<TManifest, string?> getSourceLabel,
        out PendingTurnSnapshotAuthorityPayload? payload,
        out string failureCode)
        where TManifest : class
    {
        return TryValidateManifestAgainstAuthority(
            manifest,
            authorityJson,
            manifestHashOptions,
            getHash,
            setHash,
            getSessionId,
            getRequestId,
            getTurnNumber,
            getFiles,
            getSnapshotFileHashes,
            getClientOwnedValidationHashes,
            getRollbackBaselineFiles,
            getSourceLabel,
            null,
            null,
            out payload,
            out failureCode,
            requireRollbackArtifactPaths: true);
    }

    internal static bool TryReadDetachedAuthorityPayload(
        string? authorityJson,
        out PendingTurnSnapshotAuthorityPayload? payload)
    {
        return TryReadAuthorityPayload(authorityJson, out payload);
    }

    private static bool TryReadAuthorityPayload(
        string? authorityJson,
        out PendingTurnSnapshotAuthorityPayload? payload)
    {
        payload = null;
        if (string.IsNullOrWhiteSpace(authorityJson))
            return false;

        PendingTurnSnapshotAuthorityEnvelope? envelope;
        try
        {
            envelope = JsonSerializer.Deserialize<PendingTurnSnapshotAuthorityEnvelope>(authorityJson, AuthorityJsonOpts);
        }
        catch
        {
            return false;
        }

        if (envelope == null ||
            string.IsNullOrWhiteSpace(envelope.PayloadJsonBase64))
        {
            return false;
        }

        try
        {
            var payloadBytes = Convert.FromBase64String(envelope.PayloadJsonBase64);
            var isPortable = IsPortableAuthorityEnvelope(envelope);
            if (isPortable)
            {
                if (!VerifyPortableAuthorityIntegrity(payloadBytes, envelope))
                    return false;
            }
            else
            {
                if (string.IsNullOrWhiteSpace(envelope.PayloadSignature) ||
                    !VerifyLegacyAuthoritySignature(payloadBytes, envelope.PayloadSignature))
                {
                    return false;
                }
            }

            payload = JsonSerializer.Deserialize<PendingTurnSnapshotAuthorityPayload>(
                Encoding.UTF8.GetString(payloadBytes),
                AuthorityJsonOpts);
            if (payload == null)
                return false;

            var hasExactRollbackMode = string.Equals(
                payload.RollbackHashMode,
                ExactRollbackHashMode,
                StringComparison.Ordinal);
            var hasExactSnapshotMode = string.Equals(
                payload.SnapshotHashMode,
                ExactSnapshotHashMode,
                StringComparison.Ordinal);
            if (isPortable && envelope.FormatVersion == PortableAuthorityFormatVersion)
                return hasExactRollbackMode && hasExactSnapshotMode;
            if (isPortable && envelope.FormatVersion == ExactRollbackPortableAuthorityFormatVersion)
                return hasExactRollbackMode && string.IsNullOrWhiteSpace(payload.SnapshotHashMode);

            return !hasExactRollbackMode &&
                   !hasExactSnapshotMode &&
                   string.IsNullOrWhiteSpace(payload.RollbackHashMode) &&
                   string.IsNullOrWhiteSpace(payload.SnapshotHashMode);
        }
        catch
        {
            payload = null;
            return false;
        }
    }

    private static PendingTurnSnapshotAuthorityPayload BuildAuthorityPayload<TManifest>(
        TManifest manifest,
        JsonSerializerOptions manifestHashOptions,
        Func<TManifest, string> getHash,
        Action<TManifest, string> setHash,
        Func<TManifest, string> getSessionId,
        Func<TManifest, string> getRequestId,
        Func<TManifest, int> getTurnNumber,
        Func<TManifest, IDictionary<string, string>?> getFiles,
        Func<TManifest, IDictionary<string, string>?> getSnapshotFileHashes,
        Func<TManifest, IDictionary<string, string>?> getClientOwnedValidationHashes,
        Func<TManifest, IEnumerable<string>?> getRollbackBaselineFiles,
        Func<TManifest, string?> getSourceLabel,
        Func<TManifest, IDictionary<string, string>?>? getRollbackBackups,
        Func<string, byte[]?>? readRelativeFileBytes,
        bool hashRollbackBytesExactly,
        bool hashSnapshotBytesExactly)
        where TManifest : class
    {
        var rollbackBackups = getRollbackBackups != null
            ? CopyNormalizedFilesDictionary(getRollbackBackups(manifest))
            : new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        return new PendingTurnSnapshotAuthorityPayload
        {
            SessionId = (getSessionId(manifest) ?? string.Empty).Trim(),
            RequestId = (getRequestId(manifest) ?? string.Empty).Trim(),
            TurnNumber = getTurnNumber(manifest),
            ManifestPayloadHash = ComputeManifestPayloadHash(
                manifest,
                manifestHashOptions,
                getHash,
                setHash),
            Files = CopyNormalizedFilesDictionary(getFiles(manifest)),
            SnapshotFileHashes = CopyNormalizedHashDictionary(getSnapshotFileHashes(manifest)),
            SnapshotHashMode = hashSnapshotBytesExactly ? ExactSnapshotHashMode : null,
            ClientOwnedValidationHashes = CopyNormalizedHashDictionary(getClientOwnedValidationHashes(manifest)),
            RollbackBackups = rollbackBackups,
            RollbackBackupHashes = rollbackBackups.Count == 0
                ? new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                : CopyNormalizedFileHashes(
                    rollbackBackups,
                    readRelativeFileBytes,
                    hashRollbackBytesExactly),
            RollbackHashMode = hashRollbackBytesExactly ? ExactRollbackHashMode : null,
            RollbackBaselineFiles = CopyNormalizedBaselineFiles(getRollbackBaselineFiles(manifest)),
            SourceLabel = string.IsNullOrWhiteSpace(getSourceLabel(manifest))
                ? null
                : getSourceLabel(manifest)!.Trim()
        };
    }

    private static Dictionary<string, string> CopyNormalizedFilesDictionary(IDictionary<string, string>? source)
    {
        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (source == null)
            return result;

        foreach (var (key, value) in source)
        {
            if (string.IsNullOrWhiteSpace(key) || string.IsNullOrWhiteSpace(value))
                continue;

            result[NormalizePath(key)] = NormalizePath(value);
        }

        return result;
    }

    private static Dictionary<string, string> CopyNormalizedHashDictionary(IDictionary<string, string>? source)
    {
        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (source == null)
            return result;

        foreach (var (key, value) in source)
        {
            if (string.IsNullOrWhiteSpace(key) || string.IsNullOrWhiteSpace(value))
                continue;

            result[NormalizePath(key)] = value.Trim();
        }

        return result;
    }

    private static List<string> CopyNormalizedBaselineFiles(IEnumerable<string>? source)
    {
        if (source == null)
            return new List<string>();

        return source
            .Where(path => !string.IsNullOrWhiteSpace(path))
            .Select(NormalizePath)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static Dictionary<string, string> CopyNormalizedFileHashes(
        IDictionary<string, string> fileMap,
        Func<string, byte[]?>? readRelativeFileBytes,
        bool hashBytesExactly)
    {
        if (readRelativeFileBytes == null)
            throw new InvalidOperationException("Validated pending snapshot authority requires a readable file delegate for rollback-backed authority.");

        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var (logicalPath, relativePath) in fileMap)
        {
            var content = readRelativeFileBytes(relativePath);
            if (content == null)
            {
                throw new InvalidOperationException(
                    $"Validated pending snapshot authority requires readable file '{relativePath}' for '{logicalPath}'.");
            }

            result[logicalPath] = hashBytesExactly
                ? ComputeSha256(content)
                : ComputeSha256(DecodeLegacyText(content));
        }

        return result;
    }

    private static bool AuthorityPayloadEquals(
        PendingTurnSnapshotAuthorityPayload expected,
        PendingTurnSnapshotAuthorityPayload actual,
        bool compareRollbackBackups)
    {
        return string.Equals(expected.SessionId, actual.SessionId, StringComparison.OrdinalIgnoreCase) &&
               string.Equals(expected.RequestId, actual.RequestId, StringComparison.OrdinalIgnoreCase) &&
               expected.TurnNumber == actual.TurnNumber &&
               string.Equals(expected.ManifestPayloadHash, actual.ManifestPayloadHash, StringComparison.OrdinalIgnoreCase) &&
               DictionaryEquals(expected.Files, actual.Files, compareValuesIgnoreCase: true) &&
               DictionaryEquals(expected.SnapshotFileHashes, actual.SnapshotFileHashes, compareValuesIgnoreCase: true) &&
               string.Equals(
                   expected.SnapshotHashMode ?? string.Empty,
                   actual.SnapshotHashMode ?? string.Empty,
                   StringComparison.Ordinal) &&
               DictionaryEquals(expected.ClientOwnedValidationHashes, actual.ClientOwnedValidationHashes, compareValuesIgnoreCase: true) &&
               (!compareRollbackBackups || DictionaryEquals(expected.RollbackBackups, actual.RollbackBackups, compareValuesIgnoreCase: true)) &&
               (!compareRollbackBackups || DictionaryEquals(expected.RollbackBackupHashes, actual.RollbackBackupHashes, compareValuesIgnoreCase: true)) &&
               (!compareRollbackBackups || string.Equals(
                   expected.RollbackHashMode ?? string.Empty,
                   actual.RollbackHashMode ?? string.Empty,
                   StringComparison.Ordinal)) &&
               BaselineFilesEqual(expected.RollbackBaselineFiles, actual.RollbackBaselineFiles) &&
               string.Equals(expected.SourceLabel ?? string.Empty, actual.SourceLabel ?? string.Empty, StringComparison.Ordinal);
    }

    private static bool DictionaryEquals(
        IDictionary<string, string> expected,
        IDictionary<string, string> actual,
        bool compareValuesIgnoreCase)
    {
        if (expected.Count != actual.Count)
            return false;

        foreach (var (key, expectedValue) in expected)
        {
            if (!actual.TryGetValue(key, out var actualValue))
                return false;

            if (!string.Equals(
                    expectedValue,
                    actualValue,
                    compareValuesIgnoreCase ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal))
            {
                return false;
            }
        }

        return true;
    }

    private static bool BaselineFilesEqual(
        IReadOnlyCollection<string> expected,
        IReadOnlyCollection<string> actual)
    {
        if (expected.Count != actual.Count)
            return false;

        var actualSet = new HashSet<string>(actual, StringComparer.OrdinalIgnoreCase);
        return expected.All(actualSet.Contains);
    }

    private static string NormalizePath(string value) => value.Replace('\\', '/').Trim();

    internal static string ComputeSha256(byte[] content)
    {
        using var sha = SHA256.Create();
        var bytes = sha.ComputeHash(content);
        return Convert.ToHexString(bytes);
    }

    private static bool IsPortableAuthorityEnvelope(PendingTurnSnapshotAuthorityEnvelope envelope) =>
        envelope.FormatVersion.HasValue ||
        !string.IsNullOrWhiteSpace(envelope.IntegrityAlgorithm) ||
        !string.IsNullOrWhiteSpace(envelope.PayloadSha256);

    private static bool VerifyPortableAuthorityIntegrity(
        byte[] payloadBytes,
        PendingTurnSnapshotAuthorityEnvelope envelope)
    {
        if (envelope.FormatVersion is not (
                LegacyPortableAuthorityFormatVersion or
                ExactRollbackPortableAuthorityFormatVersion or
                PortableAuthorityFormatVersion) ||
            !string.Equals(envelope.IntegrityAlgorithm, PortableIntegrityAlgorithm, StringComparison.Ordinal) ||
            string.IsNullOrWhiteSpace(envelope.PayloadSha256))
        {
            return false;
        }

        // Portable envelopes are local lifecycle integrity/tamper evidence only. They bind this detached
        // payload to the manifest/snapshot/rollback hashes so accidental or GM-side edits fail closed, but
        // they are not a strong security boundary against a same-user process that can rewrite both payload
        // and hash. Legacy Windows CNG envelopes are read below only for compatibility with existing saves.
        return string.Equals(
            ComputeSha256(payloadBytes),
            envelope.PayloadSha256.Trim(),
            StringComparison.OrdinalIgnoreCase);
    }

    private static bool VerifyLegacyAuthoritySignature(byte[] payloadBytes, string signature)
    {
        try
        {
            var signatureBytes = Convert.FromBase64String(signature);
            using var verifier = OpenExistingAuthoritySigner();
            return verifier != null && verifier.VerifyData(payloadBytes, signatureBytes, HashAlgorithmName.SHA256);
        }
        catch
        {
            return false;
        }
    }

    private static ECDsa? OpenExistingAuthoritySigner()
    {
        if (!OperatingSystem.IsWindows())
            return null;

        if (!CngKey.Exists(AuthorityKeyName, CngProvider.MicrosoftSoftwareKeyStorageProvider))
            return null;

        return new ECDsaCng(CngKey.Open(AuthorityKeyName, CngProvider.MicrosoftSoftwareKeyStorageProvider));
    }

    private const string AuthorityKeyName = "BookOfEternityClient.PendingTurnSnapshotAuthority";

    internal static string ComputeSnapshotFileHash(
        PendingTurnSnapshotAuthorityPayload payload,
        byte[] content) =>
        ComputeSnapshotFileHash(payload.SnapshotHashMode, content);

    internal static string ComputeSnapshotFileHash(
        string? snapshotHashMode,
        byte[] content)
    {
        return string.Equals(
            snapshotHashMode,
            ExactSnapshotHashMode,
            StringComparison.Ordinal)
            ? ComputeSha256(content)
            : ComputeSha256(DecodeLegacyText(content));
    }

    private static string DecodeLegacyText(byte[] content)
    {
        using var stream = new MemoryStream(content, writable: false);
        using var reader = new StreamReader(
            stream,
            Encoding.UTF8,
            detectEncodingFromByteOrderMarks: true);
        return reader.ReadToEnd();
    }
}
