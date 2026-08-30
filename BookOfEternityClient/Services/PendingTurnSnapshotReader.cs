using System.Collections;
using System.Collections.ObjectModel;
using System.Text.Json;
using BookOfEternityClient.Core;

namespace BookOfEternityClient.Services;

/// <summary>
/// Reads immutable bytes from the current detached-authority pending-turn snapshot.
/// It never treats the live canonical root as the accepted before-image.
/// </summary>
internal static class PendingTurnSnapshotReader
{
    private const int MaximumRequiredPaths = 64;
    private static readonly PendingTurnSnapshotContextSource[] CurrentContextSources =
    {
        new("game_state/control/validation_repair_request.json", IsRepairRequest: true),
        new(LiveTurnPreparationService.TurnRequestPath, IsRepairRequest: false),
        new("ready/turn_complete.json", IsRepairRequest: false)
    };

    internal static PendingTurnSnapshotReadResult ReadCurrent(
        FileSystemManager fs,
        FileSystemManager.CanonicalWriteLease lease,
        IReadOnlyCollection<string> requiredLogicalPaths)
    {
        ArgumentNullException.ThrowIfNull(fs);
        ArgumentNullException.ThrowIfNull(lease);
        ArgumentNullException.ThrowIfNull(requiredLogicalPaths);
        fs.EnsureCanonicalWriteLeaseActive(lease);

        var issues = new List<ValidationIssue>();
        var selection = ValidatePathSelection(requiredLogicalPaths, issues);
        if (issues.Count != 0)
            return Failure(issues);

        LiveTurnPendingSnapshotManifest? manifest;
        try
        {
            var manifestJson = fs.ReadFileSync(
                LiveTurnPreparationService.PendingTurnSnapshotManifestPath);
            if (string.IsNullOrWhiteSpace(manifestJson) || HasDuplicateJsonProperties(manifestJson))
            {
                manifest = null;
            }
            else
            {
                manifest = JsonSerializer.Deserialize<LiveTurnPendingSnapshotManifest>(
                    manifestJson,
                    LiveTurnPreparationService.ManifestJsonOptions);
            }
        }
        catch (JsonException)
        {
            manifest = null;
        }

        if (manifest is null || manifest.Files is null || manifest.SnapshotFileHashes is null)
        {
            Add(
                issues,
                LiveTurnPreparationService.PendingTurnSnapshotManifestPath,
                "pending_turn_snapshot_reader_manifest_invalid",
                "missing or malformed pending-turn manifest");
            return Failure(issues);
        }

        ValidateNoCaseConfusableCoverage(selection, manifest, issues);
        if (issues.Count != 0)
            return Failure(issues);

        var authorityJson = fs.ReadFileSync(PendingTurnSnapshotAuthority.AuthorityPath);
        if (!PendingTurnSnapshotAuthority.TryValidateManifestForReaderAuthority(
                manifest,
                authorityJson,
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
                fs.ReadFileBytesSync,
                out var payload,
                out var authorityFailure) ||
            payload is null)
        {
            Add(
                issues,
                PendingTurnSnapshotAuthority.AuthorityPath,
                "pending_turn_snapshot_reader_authority_invalid",
                authorityFailure);
            return Failure(issues);
        }

        if (!string.Equals(
                payload.SnapshotHashMode,
                PendingTurnSnapshotAuthority.ExactSnapshotHashMode,
                StringComparison.Ordinal))
        {
            Add(
                issues,
                PendingTurnSnapshotAuthority.AuthorityPath,
                "pending_turn_snapshot_reader_hash_mode_invalid",
                payload.SnapshotHashMode ?? "legacy text hash mode");
            return Failure(issues);
        }

        var contextReads = CurrentContextSources
            .Select(source => ReadCurrentContext(fs, source))
            .ToArray();
        var invalidContext = contextReads.FirstOrDefault(
            read => read.Status == PendingTurnSnapshotContextStatus.Invalid);
        if (invalidContext is not null)
        {
            Add(
                issues,
                invalidContext.Path,
                "pending_turn_snapshot_reader_context_invalid",
                "an existing lifecycle context is malformed or incomplete");
            return Failure(issues);
        }

        var usableContexts = contextReads
            .Where(read => read.Status == PendingTurnSnapshotContextStatus.Usable)
            .ToArray();
        if (usableContexts.Length == 0)
        {
            Add(
                issues,
                LiveTurnPreparationService.TurnRequestPath,
                "pending_turn_snapshot_reader_context_stale",
                "no authoritative current request context exists");
            return Failure(issues);
        }

        var firstContext = usableContexts[0].Context!;
        if (usableContexts.Skip(1).Any(read =>
                !ContextsMatch(firstContext, read.Context!)))
        {
            Add(
                issues,
                LiveTurnPreparationService.TurnRequestPath,
                "pending_turn_snapshot_reader_context_conflict",
                string.Join(
                    "; ",
                    usableContexts.Select(read =>
                        $"{read.Path}={DescribeContext(read.Context!)}")));
            return Failure(issues);
        }

        if (!usableContexts.All(read => ContextMatches(manifest, read.Context!)))
        {
            Add(
                issues,
                LiveTurnPreparationService.TurnRequestPath,
                "pending_turn_snapshot_reader_context_stale",
                "the authoritative current request context does not match the signed manifest");
            return Failure(issues);
        }

        var realm = NormalizeRealm(manifest.ProgressionControl?.CurrentRealm);
        if (realm.Length == 0)
        {
            Add(
                issues,
                LiveTurnPreparationService.PendingTurnSnapshotManifestPath,
                "pending_turn_snapshot_reader_realm_invalid",
                manifest.ProgressionControl?.CurrentRealm ?? "null");
            return Failure(issues);
        }

        var selectedPaths = ResolveSelectedPaths(selection, manifest, issues);
        if (issues.Count != 0)
            return Failure(issues);

        var bytesByPath = new Dictionary<string, byte[]>(StringComparer.Ordinal);
        foreach (var logicalPath in selectedPaths)
        {
            var fileRows = manifest.Files
                .Where(pair => string.Equals(pair.Key, logicalPath, StringComparison.Ordinal))
                .ToArray();
            var hashRows = manifest.SnapshotFileHashes
                .Where(pair => string.Equals(pair.Key, logicalPath, StringComparison.Ordinal))
                .ToArray();
            if (fileRows.Length != 1 || hashRows.Length != 1)
            {
                Add(
                    issues,
                    logicalPath,
                    "pending_turn_snapshot_reader_coverage_missing",
                    "required logical path is absent or ambiguous in signed snapshot coverage");
                continue;
            }

            var snapshotPath = fileRows[0].Value;
            if (!PendingTurnSnapshotAuthority.IsSafeRelativePath(snapshotPath) ||
                !snapshotPath.StartsWith(
                    LiveTurnPreparationService.PendingTurnSnapshotDirectory + "/",
                    StringComparison.OrdinalIgnoreCase))
            {
                Add(
                    issues,
                    logicalPath,
                    "pending_turn_snapshot_reader_snapshot_path_invalid",
                    snapshotPath);
                continue;
            }

            var bytes = fs.ReadFileBytesSync(snapshotPath);
            if (bytes is null ||
                !string.Equals(
                    PendingTurnSnapshotAuthority.ComputeSnapshotFileHash(payload, bytes),
                    hashRows[0].Value,
                    StringComparison.OrdinalIgnoreCase))
            {
                Add(
                    issues,
                    logicalPath,
                    "pending_turn_snapshot_reader_bytes_mismatch",
                    bytes is null ? "snapshot bytes are missing" : "snapshot bytes do not match their signed hash");
                continue;
            }

            bytesByPath[logicalPath] = bytes.ToArray();
        }

        if (issues.Count != 0)
            return Failure(issues);

        return new PendingTurnSnapshotReadResult(
            true,
            new PendingTurnSnapshotReadAuthority(
                manifest.SessionId,
                manifest.RequestId,
                manifest.ManifestPayloadHash,
                manifest.TurnNumber,
                realm,
                manifest.PreGeneratedDices1d20,
                bytesByPath),
            Array.Empty<ValidationIssue>());
    }

    private static PendingTurnSnapshotValidatedPathSelection ValidatePathSelection(
        IReadOnlyCollection<string> paths,
        ICollection<ValidationIssue> issues)
    {
        var result = new List<string>();
        var optional = new HashSet<string>(StringComparer.Ordinal);
        if (paths is PendingTurnSnapshotPathSelection pathSelection)
        {
            foreach (var path in pathSelection.OptionalLogicalPaths)
                optional.Add(path);
        }
        var exact = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var yieldedCount = 0;
        foreach (var path in paths)
        {
            yieldedCount++;
            if (yieldedCount > MaximumRequiredPaths)
            {
                Add(
                    issues,
                    LiveTurnPreparationService.PendingTurnSnapshotManifestPath,
                    "pending_turn_snapshot_reader_required_paths_invalid",
                    $"count>{MaximumRequiredPaths}");
                return PendingTurnSnapshotValidatedPathSelection.Empty;
            }
            if (string.IsNullOrWhiteSpace(path) ||
                !string.Equals(path, path.Trim(), StringComparison.Ordinal) ||
                path.Contains('\\') ||
                !PendingTurnSnapshotAuthority.IsSafeRelativePath(path) ||
                !exact.Add(path))
            {
                Add(
                    issues,
                    LiveTurnPreparationService.PendingTurnSnapshotManifestPath,
                    "pending_turn_snapshot_reader_required_paths_invalid",
                    path ?? "null");
                continue;
            }
            result.Add(path);
        }
        if (result.Count == 0)
        {
            Add(
                issues,
                LiveTurnPreparationService.PendingTurnSnapshotManifestPath,
                "pending_turn_snapshot_reader_required_paths_invalid",
                "count=0");
            return PendingTurnSnapshotValidatedPathSelection.Empty;
        }
        return new PendingTurnSnapshotValidatedPathSelection(
            Array.AsReadOnly(result.Where(path => !optional.Contains(path)).ToArray()),
            Array.AsReadOnly(result.Where(optional.Contains).ToArray()));
    }

    private static IReadOnlyList<string> ResolveSelectedPaths(
        PendingTurnSnapshotValidatedPathSelection selection,
        LiveTurnPendingSnapshotManifest manifest,
        ICollection<ValidationIssue> issues)
    {
        var result = new List<string>(
            selection.RequiredLogicalPaths.Count + selection.OptionalLogicalPaths.Count);
        foreach (var path in selection.RequiredLogicalPaths)
        {
            if (!TryResolveExactCoverage(path, manifest, optional: false, issues, out var selected))
            {
                continue;
            }
            if (selected)
                result.Add(path);
        }
        foreach (var path in selection.OptionalLogicalPaths)
        {
            if (!TryResolveExactCoverage(path, manifest, optional: true, issues, out var selected))
                continue;
            if (selected)
                result.Add(path);
        }
        return Array.AsReadOnly(result.ToArray());
    }

    private static void ValidateNoCaseConfusableCoverage(
        PendingTurnSnapshotValidatedPathSelection selection,
        LiveTurnPendingSnapshotManifest manifest,
        ICollection<ValidationIssue> issues)
    {
        ValidateNoCaseConfusableCoverage(manifest.Files.Keys, issues);
        ValidateNoCaseConfusableCoverage(manifest.SnapshotFileHashes.Keys, issues);
        var fileKeys = manifest.Files.Keys.ToHashSet(StringComparer.Ordinal);
        var hashKeys = manifest.SnapshotFileHashes.Keys.ToHashSet(StringComparer.Ordinal);
        if (!fileKeys.SetEquals(hashKeys))
        {
            foreach (var path in fileKeys.Except(hashKeys, StringComparer.Ordinal)
                         .Concat(hashKeys.Except(fileKeys, StringComparer.Ordinal)))
            {
                Add(
                    issues,
                    path,
                    "pending_turn_snapshot_reader_coverage_case_mismatch",
                    "signed snapshot file and hash coverage keys do not agree exactly");
            }
        }
        if (issues.Count != 0)
            return;

        foreach (var path in selection.RequiredLogicalPaths.Concat(selection.OptionalLogicalPaths))
        {
            if (manifest.Files.Keys.Any(key =>
                    string.Equals(key, path, StringComparison.OrdinalIgnoreCase) &&
                    !string.Equals(key, path, StringComparison.Ordinal)) ||
                manifest.SnapshotFileHashes.Keys.Any(key =>
                    string.Equals(key, path, StringComparison.OrdinalIgnoreCase) &&
                    !string.Equals(key, path, StringComparison.Ordinal)))
            {
                Add(
                    issues,
                    path,
                    "pending_turn_snapshot_reader_coverage_case_mismatch",
                    "signed snapshot coverage contains a confusable path key");
            }
        }
    }

    private static void ValidateNoCaseConfusableCoverage(
        IEnumerable<string> paths,
        ICollection<ValidationIssue> issues)
    {
        var exact = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var path in paths)
        {
            if (exact.TryGetValue(path, out var previous) &&
                !string.Equals(previous, path, StringComparison.Ordinal))
            {
                Add(
                    issues,
                    path,
                    "pending_turn_snapshot_reader_coverage_case_mismatch",
                    $"signed snapshot coverage contains confusable keys '{previous}' and '{path}'");
                continue;
            }
            exact[path] = path;
        }
    }

    private static bool TryResolveExactCoverage(
        string path,
        LiveTurnPendingSnapshotManifest manifest,
        bool optional,
        ICollection<ValidationIssue> issues,
        out bool selected)
    {
        selected = false;
        var fileKeys = manifest.Files.Keys
            .Where(key => string.Equals(key, path, StringComparison.OrdinalIgnoreCase))
            .ToArray();
        var hashKeys = manifest.SnapshotFileHashes.Keys
            .Where(key => string.Equals(key, path, StringComparison.OrdinalIgnoreCase))
            .ToArray();
        var fileExact = fileKeys.Count(key => string.Equals(key, path, StringComparison.Ordinal));
        var hashExact = hashKeys.Count(key => string.Equals(key, path, StringComparison.Ordinal));

        if (fileKeys.Any(key => !string.Equals(key, path, StringComparison.Ordinal)) ||
            hashKeys.Any(key => !string.Equals(key, path, StringComparison.Ordinal)))
        {
            Add(
                issues,
                path,
                "pending_turn_snapshot_reader_coverage_case_mismatch",
                "signed snapshot coverage contains a confusable path key");
            return false;
        }

        if (fileExact == 1 && hashExact == 1)
        {
            selected = true;
            return true;
        }

        if (optional && fileExact == 0 && hashExact == 0)
            return true;

        Add(
            issues,
            path,
            "pending_turn_snapshot_reader_coverage_missing",
            optional
                ? "optional logical path has incomplete signed snapshot coverage"
                : "required logical path is absent or ambiguous in signed snapshot coverage");
        return false;
    }

    private static bool HasDuplicateJsonProperties(string json)
    {
        using var document = JsonDocument.Parse(json);
        return HasDuplicateJsonProperties(document.RootElement);
    }

    private static bool HasDuplicateJsonProperties(JsonElement element)
    {
        if (element.ValueKind == JsonValueKind.Object)
        {
            var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var property in element.EnumerateObject())
            {
                if (!names.Add(property.Name) || HasDuplicateJsonProperties(property.Value))
                    return true;
            }
        }
        else if (element.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in element.EnumerateArray())
            {
                if (HasDuplicateJsonProperties(item))
                    return true;
            }
        }

        return false;
    }

    private static PendingTurnSnapshotContextRead ReadCurrentContext(
        FileSystemManager fs,
        PendingTurnSnapshotContextSource source)
    {
        if (!fs.FileExists(source.Path))
            return new PendingTurnSnapshotContextRead(
                source.Path,
                PendingTurnSnapshotContextStatus.Missing,
                null);

        var json = fs.ReadFileSync(source.Path);
        if (string.IsNullOrWhiteSpace(json))
            return new PendingTurnSnapshotContextRead(
                source.Path,
                PendingTurnSnapshotContextStatus.Invalid,
                null);

        try
        {
            using var document = JsonDocument.Parse(json);
            var root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object ||
                HasAmbiguousContextAuthorityProperties(root, source.IsRepairRequest))
            {
                return new PendingTurnSnapshotContextRead(
                    source.Path,
                    PendingTurnSnapshotContextStatus.Invalid,
                    null);
            }

            if (source.IsRepairRequest &&
                root.TryGetProperty("metadataDiagnosticOnly", out var diagnosticNode))
            {
                if (diagnosticNode.ValueKind is not (JsonValueKind.True or JsonValueKind.False))
                {
                    return new PendingTurnSnapshotContextRead(
                        source.Path,
                        PendingTurnSnapshotContextStatus.Invalid,
                        null);
                }

                if (diagnosticNode.GetBoolean())
                {
                    return new PendingTurnSnapshotContextRead(
                        source.Path,
                        PendingTurnSnapshotContextStatus.DiagnosticOnly,
                        null);
                }
            }

            if (!root.TryGetProperty("sessionId", out var sessionNode) ||
                sessionNode.ValueKind != JsonValueKind.String ||
                !root.TryGetProperty("requestId", out var requestNode) ||
                requestNode.ValueKind != JsonValueKind.String ||
                !root.TryGetProperty("turnNumber", out var turnNode) ||
                turnNode.ValueKind != JsonValueKind.Number ||
                !turnNode.TryGetInt32(out var turn))
            {
                return new PendingTurnSnapshotContextRead(
                    source.Path,
                    PendingTurnSnapshotContextStatus.Invalid,
                    null);
            }

            var session = sessionNode.GetString() ?? string.Empty;
            var request = requestNode.GetString() ?? string.Empty;
            if (string.IsNullOrWhiteSpace(session) ||
                string.IsNullOrWhiteSpace(request) ||
                turn <= 0)
            {
                return new PendingTurnSnapshotContextRead(
                    source.Path,
                    PendingTurnSnapshotContextStatus.Invalid,
                    null);
            }

            return new PendingTurnSnapshotContextRead(
                source.Path,
                PendingTurnSnapshotContextStatus.Usable,
                new PendingTurnSnapshotRequestContext(session, request, turn));
        }
        catch (JsonException)
        {
            return new PendingTurnSnapshotContextRead(
                source.Path,
                PendingTurnSnapshotContextStatus.Invalid,
                null);
        }
    }

    private static bool HasAmbiguousContextAuthorityProperties(
        JsonElement root,
        bool isRepairRequest)
    {
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var property in root.EnumerateObject())
        {
            var canonicalName = ContextAuthorityPropertyName(
                property.Name,
                isRepairRequest);
            if (canonicalName is null)
                continue;
            if (!string.Equals(property.Name, canonicalName, StringComparison.Ordinal) ||
                !seen.Add(canonicalName))
            {
                return true;
            }
        }

        return false;
    }

    private static string? ContextAuthorityPropertyName(
        string propertyName,
        bool isRepairRequest)
    {
        if (string.Equals(propertyName, "sessionId", StringComparison.OrdinalIgnoreCase))
            return "sessionId";
        if (string.Equals(propertyName, "requestId", StringComparison.OrdinalIgnoreCase))
            return "requestId";
        if (string.Equals(propertyName, "turnNumber", StringComparison.OrdinalIgnoreCase))
            return "turnNumber";
        if (isRepairRequest && string.Equals(
                propertyName,
                "metadataDiagnosticOnly",
                StringComparison.OrdinalIgnoreCase))
        {
            return "metadataDiagnosticOnly";
        }

        return null;
    }

    private static bool ContextsMatch(
        PendingTurnSnapshotRequestContext left,
        PendingTurnSnapshotRequestContext right) =>
        left.TurnNumber == right.TurnNumber &&
        PendingTurnSnapshotAuthority.DoesPendingTurnContextIdMatch(
            left.SessionId,
            right.SessionId) &&
        PendingTurnSnapshotAuthority.DoesPendingTurnContextIdMatch(
            left.RequestId,
            right.RequestId);

    private static string DescribeContext(PendingTurnSnapshotRequestContext context) =>
        $"{context.SessionId}/{context.RequestId}/{context.TurnNumber}";

    private static bool ContextMatches(
        LiveTurnPendingSnapshotManifest manifest,
        PendingTurnSnapshotRequestContext context) =>
        manifest.TurnNumber == context.TurnNumber &&
        PendingTurnSnapshotAuthority.DoesPendingTurnContextIdMatch(
            manifest.SessionId,
            context.SessionId) &&
        PendingTurnSnapshotAuthority.DoesPendingTurnContextIdMatch(
            manifest.RequestId,
            context.RequestId);

    private static string NormalizeRealm(string? value) => value switch
    {
        "Mortal World" or "mortal_world" => "mortal_world",
        "Chaos Sea" or "chaos_sea" => "chaos_sea",
        "Shining Abode" or "shining_abode" => "shining_abode",
        _ => string.Empty
    };

    private static PendingTurnSnapshotReadResult Failure(
        IEnumerable<ValidationIssue> issues) =>
        new(false, null, Array.AsReadOnly(issues.ToArray()));

    private static void Add(
        ICollection<ValidationIssue> issues,
        string path,
        string code,
        string actual) => issues.Add(new ValidationIssue(
        path,
        IssueSeverity.Error,
        "The current pending-turn snapshot cannot provide immutable reader authority.",
        code: code,
        actor: "Client",
        section: "pending_turn_snapshot_reader",
        expected: "one current detached-authority manifest with exact signed required bytes",
        actual: actual,
        repairHint:
        "Recreate the active turn snapshot through the client; never substitute live or caller-authored bytes."));

    private sealed record PendingTurnSnapshotRequestContext(
        string SessionId,
        string RequestId,
        int TurnNumber);

    private sealed record PendingTurnSnapshotContextSource(
        string Path,
        bool IsRepairRequest);

    private sealed record PendingTurnSnapshotContextRead(
        string Path,
        PendingTurnSnapshotContextStatus Status,
        PendingTurnSnapshotRequestContext? Context);

    private sealed record PendingTurnSnapshotValidatedPathSelection(
        IReadOnlyList<string> RequiredLogicalPaths,
        IReadOnlyList<string> OptionalLogicalPaths)
    {
        internal static PendingTurnSnapshotValidatedPathSelection Empty { get; } = new(
            Array.Empty<string>(),
            Array.Empty<string>());
    }

    private enum PendingTurnSnapshotContextStatus
    {
        Missing,
        Invalid,
        DiagnosticOnly,
        Usable
    }
}

internal sealed class PendingTurnSnapshotReadAuthority
{
    private readonly ReadOnlyDictionary<string, byte[]> _bytesByPath;
    private readonly ReadOnlyCollection<int> _acceptedD20EventValues;
    private readonly ReadOnlyCollection<string> _coveredLogicalPaths;

    internal PendingTurnSnapshotReadAuthority(
        string sessionId,
        string requestId,
        string snapshotToken,
        int turnNumber,
        string realm,
        IReadOnlyList<int>? acceptedD20EventValues,
        IReadOnlyDictionary<string, byte[]> bytesByPath)
    {
        SessionId = sessionId;
        RequestId = requestId;
        SnapshotToken = snapshotToken;
        TurnNumber = turnNumber;
        Realm = realm;
        _acceptedD20EventValues = Array.AsReadOnly(
            acceptedD20EventValues?.ToArray() ?? Array.Empty<int>());
        _coveredLogicalPaths = Array.AsReadOnly(
            bytesByPath.Keys.OrderBy(static path => path, StringComparer.Ordinal).ToArray());
        _bytesByPath = new ReadOnlyDictionary<string, byte[]>(
            bytesByPath.ToDictionary(
                pair => pair.Key,
                pair => pair.Value.ToArray(),
                StringComparer.Ordinal));
    }

    public string SessionId { get; }
    public string RequestId { get; }
    public string SnapshotToken { get; }
    public int TurnNumber { get; }
    public string Realm { get; }

    internal IReadOnlyList<int> AcceptedD20EventValues => _acceptedD20EventValues;
    internal IReadOnlyList<string> CoveredLogicalPaths => _coveredLogicalPaths;

    public byte[] ReadRequiredBytes(string logicalPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(logicalPath);
        if (!_bytesByPath.TryGetValue(logicalPath, out var bytes))
            throw new KeyNotFoundException($"Required snapshot path '{logicalPath}' was not validated.");
        return bytes.ToArray();
    }
}

internal sealed record PendingTurnSnapshotReadResult(
    bool Success,
    PendingTurnSnapshotReadAuthority? Snapshot,
    IReadOnlyList<ValidationIssue> Issues);

/// <summary>
/// Internal path-selection envelope for callers that need all mandatory roots plus
/// the exact signed subset of optional canonical roots. It deliberately implements
/// the frozen reader's existing third-parameter contract.
/// </summary>
internal sealed class PendingTurnSnapshotPathSelection : IReadOnlyCollection<string>
{
    private readonly ReadOnlyCollection<string> _allLogicalPaths;

    internal PendingTurnSnapshotPathSelection(
        IEnumerable<string> requiredLogicalPaths,
        IEnumerable<string> optionalLogicalPaths)
    {
        ArgumentNullException.ThrowIfNull(requiredLogicalPaths);
        ArgumentNullException.ThrowIfNull(optionalLogicalPaths);
        RequiredLogicalPaths = Array.AsReadOnly(requiredLogicalPaths.ToArray());
        OptionalLogicalPaths = Array.AsReadOnly(optionalLogicalPaths.ToArray());
        _allLogicalPaths = Array.AsReadOnly(
            RequiredLogicalPaths.Concat(OptionalLogicalPaths).ToArray());
    }

    internal IReadOnlyList<string> RequiredLogicalPaths { get; }
    internal IReadOnlyList<string> OptionalLogicalPaths { get; }
    public int Count => _allLogicalPaths.Count;
    public IEnumerator<string> GetEnumerator() => _allLogicalPaths.GetEnumerator();
    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
}
