using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Nodes;
using BookOfEternityClient.Core;

namespace BookOfEternityClient.Services;

public partial class ValidationService
{
    private readonly AsyncLocal<GuardianMusingsValidationScope?> _guardianMusingsPublication = new();
    private static readonly object GuardianMusingsIssuanceKey = new();
    private const string GuardianMusingsPath = "game_state/meta/guardians.json";
    private static readonly string[] GuardianMusingsSnapshotPaths =
        [GuardianMusingsPath, "game_state/meta/soul_state.json"];
    private static readonly string[] GuardianMusingsBindingPaths =
        [LiveTurnPreparationService.TurnRequestPath, LiveTurnPreparationService.PendingTurnSnapshotManifestPath,
            PendingTurnSnapshotAuthority.AuthorityPath];

    /// <summary>Captures only originally authorized addMusings while the publication lease is held.</summary>
    internal async Task<GuardianMusingsPublicationCapture?> CaptureGuardianMusingsPublicationAsync(
        FileSystemManager.CanonicalWriteLease lease)
    {
        _fs.EnsureCanonicalWriteLeaseActive(lease);
        var json = await _fs.ReadFileAsync(lease, GuardianMusingsPath);
        if (string.IsNullOrWhiteSpace(json)) return null;
        using var doc = JsonDocument.Parse(json);
        if (HasDuplicateGuardianMusingsProperties(doc.RootElement))
            throw new InvalidDataException("Guardian publication has duplicate JSON properties.");
        if (!doc.RootElement.TryGetProperty("UpdateGuardians", out var updates) ||
            updates.ValueKind != JsonValueKind.Array ||
            !updates.EnumerateArray().Any(row => row.ValueKind == JsonValueKind.Object &&
                row.TryGetProperty("command", out var name) && name.ValueKind == JsonValueKind.String && name.GetString() == "addMusings"))
            return null;

        var read = PendingTurnSnapshotReader.ReadCurrent(_fs, lease, GuardianMusingsSnapshotPaths);
        if (!read.Success || read.Snapshot is null)
            throw new InvalidDataException("addMusings requires the exact signed original pending snapshot.");
        var original = await ResolveValidatedGuardianTrackedSnapshotFileAsync(GuardianMusingsPath);
        var context = BuildGuardianPolicyContext(json, original);
        var issues = new List<ValidationIssue>();
        var authorized = AuthorizeGuardianCommandsForPolicy(doc.RootElement, GuardianMusingsPath, issues, context);
        var commands = authorized.AuthorizedCommands.Where(command =>
            command["command"]?.GetValue<string>() == "addMusings").ToArray();
        var count = updates.EnumerateArray().Count(row => row.ValueKind == JsonValueKind.Object &&
            row.TryGetProperty("command", out var name) && name.ValueKind == JsonValueKind.String && name.GetString() == "addMusings");
        if (!context.HasUsableValidatedPreTurnGuardiansSnapshot || !context.HasPreTurnRoot ||
            issues.Any(issue => issue.Severity == IssueSeverity.Error) || commands.Length != count ||
            commands.GroupBy(command => command["guardianId"]!.GetValue<string>(), StringComparer.OrdinalIgnoreCase)
                .Any(group => group.Sum(command => command["musings"]!.AsArray().Count) is < 1 or > 2))
            throw new InvalidDataException("Original addMusings authorization failed; no canonical authority is inferred.");
        var generation = _fs.ReadExistingSessionGeneration(lease)
            ?? throw new InvalidDataException("Original addMusings generation is unavailable.");
        var hashes = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var path in GuardianMusingsBindingPaths)
            hashes.Add(path, HashRequired(await _fs.ReadFileBytesAsync(lease, path)));
        return new GuardianMusingsPublicationCapture(GuardianMusingsIssuanceKey, _fs, read.Snapshot,
            generation, new JsonArray(commands.Select(command => (JsonNode)command.DeepClone()).ToArray()).ToJsonString(),
            context.PreTurnRoot.GetRawText(), hashes);
    }

    /// <summary>Detached comparison evidence, never a filesystem admission or mutation owner.</summary>
    internal sealed class GuardianMusingsPublicationCapture
    {
        private readonly FileSystemManager _files;
        private readonly PendingTurnSnapshotReadAuthority _snapshot;
        private readonly string _generation, _commandsJson, _preTurnJson;
        private readonly IReadOnlyDictionary<string, string> _bindingHashes;

        internal GuardianMusingsPublicationCapture(object key, FileSystemManager files,
            PendingTurnSnapshotReadAuthority snapshot, string generation, string commandsJson, string preTurnJson,
            IReadOnlyDictionary<string, string> bindingHashes)
        {
            if (!ReferenceEquals(key, GuardianMusingsIssuanceKey)) throw new InvalidOperationException("Original capture required.");
            _files = files; _snapshot = snapshot; _generation = generation; _commandsJson = commandsJson;
            _preTurnJson = preTurnJson; _bindingHashes = new Dictionary<string, string>(bindingHashes, StringComparer.Ordinal);
        }

        private JsonObject[] ReadCommands() => JsonNode.Parse(_commandsJson)!.AsArray().OfType<JsonObject>().ToArray();

        private async Task<bool> MatchesAsync(FileSystemManager files, FileSystemManager.CanonicalWriteLease lease)
        {
            if (!ReferenceEquals(files, _files) || !files.IsCurrentSessionGeneration(lease, _generation)) return false;
            var read = PendingTurnSnapshotReader.ReadCurrent(files, lease, GuardianMusingsSnapshotPaths);
            if (!read.Success || read.Snapshot is not { } current || current.SessionId != _snapshot.SessionId ||
                current.RequestId != _snapshot.RequestId || current.TurnNumber != _snapshot.TurnNumber ||
                current.SnapshotToken != _snapshot.SnapshotToken || current.Realm != _snapshot.Realm) return false;
            foreach (var path in GuardianMusingsSnapshotPaths)
                if (!current.ReadRequiredBytes(path).AsSpan().SequenceEqual(_snapshot.ReadRequiredBytes(path))) return false;
            foreach (var pair in _bindingHashes)
                if (HashRequired(await files.ReadFileBytesAsync(lease, pair.Key)) != pair.Value) return false;
            return true;
        }

        /// <summary>Seals exact published musings, original prefix and active mirror before physical close.</summary>
        internal async Task<GuardianMusingsCompletedValidation> CompleteAsync(FileSystemManager files,
            FileSystemManager.CanonicalWriteLease lease)
        {
            if (!await MatchesAsync(files, lease)) throw new InvalidDataException("Original addMusings binding changed before publication close.");
            var bytes = await files.ReadFileBytesAsync(lease, GuardianMusingsPath);
            var published = JsonNode.Parse(bytes!)!.AsObject();
            var commands = ReadCommands();
            if (published["UpdateGuardians"] is JsonArray updates && updates.OfType<JsonObject>()
                .Any(command => command["command"]?.GetValue<string>() == "addMusings"))
                throw new InvalidDataException("Published addMusings must be consumed exactly once.");
            var expected = CanonicalStateNormalizer.BuildGuardianAuthorityRootForValidation(
                JsonNode.Parse(_preTurnJson)!.AsObject(), published, commands, null, null, _snapshot.TurnNumber);
            foreach (var group in commands.GroupBy(command => command["guardianId"]!.GetValue<string>(), StringComparer.OrdinalIgnoreCase))
            {
                JsonObject Guardian(JsonObject root) => root["guardians"]!.AsArray().OfType<JsonObject>()
                    .Single(guardian => string.Equals(guardian["guardianId"]?.GetValue<string>(), group.Key, StringComparison.OrdinalIgnoreCase));
                var before = Guardian(JsonNode.Parse(_preTurnJson)!.AsObject())["musings"] as JsonArray;
                var target = Guardian(published);
                var after = target["musings"] as JsonArray;
                if (after is null || after.Count != (before?.Count ?? 0) + group.Sum(command => command["musings"]!.AsArray().Count) ||
                    !JsonNode.DeepEquals(after, Guardian(expected)["musings"]) ||
                    (published["activeGuardian"] is JsonObject active &&
                        string.Equals(active["guardianId"]?.GetValue<string>(), group.Key, StringComparison.OrdinalIgnoreCase) &&
                        !JsonNode.DeepEquals(active["musings"], after)))
                    throw new InvalidDataException("Published addMusings changed its original prefix, delta, count or active mirror.");
            }
            return new GuardianMusingsCompletedValidation(GuardianMusingsIssuanceKey, this, HashRequired(bytes));
        }

        /// <summary>Immutable comparison handoff; private capture is the sole issuer.</summary>
        internal sealed class GuardianMusingsCompletedValidation
        {
            private readonly GuardianMusingsPublicationCapture _capture;
            private readonly string _publishedHash;
            internal GuardianMusingsCompletedValidation(object key, GuardianMusingsPublicationCapture capture, string publishedHash)
            {
                if (!ReferenceEquals(key, GuardianMusingsIssuanceKey)) throw new InvalidOperationException("Completed original capture required.");
                _capture = capture; _publishedHash = publishedHash;
            }
            internal async Task<JsonObject[]?> ReadForComparisonAsync(FileSystemManager files,
                FileSystemManager.CanonicalWriteLease lease, JsonElement currentRoot)
            {
                if (!await _capture.MatchesAsync(files, lease)) return null;
                var bytes = await files.ReadFileBytesAsync(lease, GuardianMusingsPath);
                if (HashRequired(bytes) != _publishedHash ||
                    !JsonNode.DeepEquals(JsonNode.Parse(bytes!), JsonNode.Parse(currentRoot.GetRawText()))) return null;
                return _capture.ReadCommands();
            }
        }
    }

    /// <summary>Logical scope restores nested context and can be invalidated before repair mutation.</summary>
    internal sealed class GuardianMusingsValidationScope : IDisposable
    {
        private readonly ValidationService _validator;
        private readonly GuardianMusingsValidationScope? _previous;
        internal readonly GuardianMusingsPublicationCapture.GuardianMusingsCompletedValidation? Completion;
        internal bool Enabled { get; private set; } = true;
        internal GuardianMusingsValidationScope(ValidationService validator,
            GuardianMusingsPublicationCapture.GuardianMusingsCompletedValidation? completion)
        {
            _validator = validator; Completion = completion; _previous = validator._guardianMusingsPublication.Value;
        }
        internal void Invalidate() => Enabled = false;
        public void Dispose() { _validator._guardianMusingsPublication.Value = _previous; Enabled = false; }
    }

    internal GuardianMusingsValidationScope UseCompletedGuardianMusingsValidationScope(
        GuardianMusingsPublicationCapture.GuardianMusingsCompletedValidation? completion)
    {
        var scope = new GuardianMusingsValidationScope(this, completion);
        _guardianMusingsPublication.Value = scope;
        return scope;
    }

    private JsonObject[] ReadCompletedGuardianMusingsForComparison(JsonElement currentRoot)
    {
        if (_guardianMusingsPublication.Value is not { Enabled: true, Completion: { } completion }) return [];
        var lease = _fs.AcquireCanonicalWriteLeaseAsync().GetAwaiter().GetResult();
        Exception? failure = null;
        try { return completion.ReadForComparisonAsync(_fs, lease, currentRoot).GetAwaiter().GetResult() ?? []; }
        catch (Exception caught) { failure = caught; throw; }
        finally { CoordinatedStateWriteHelper.ReleaseOwnedLeaseAsync(_fs, lease, false, failure).GetAwaiter().GetResult(); }
    }

    private static bool HasDuplicateGuardianMusingsProperties(JsonElement root)
    {
        if (root.ValueKind == JsonValueKind.Object)
        {
            var names = new HashSet<string>(StringComparer.Ordinal);
            return root.EnumerateObject().Any(property => !names.Add(property.Name) || HasDuplicateGuardianMusingsProperties(property.Value));
        }
        return root.ValueKind == JsonValueKind.Array && root.EnumerateArray().Any(HasDuplicateGuardianMusingsProperties);
    }

    private static string HashRequired(byte[]? bytes) => bytes is null
        ? throw new InvalidDataException("Original addMusings comparison evidence is missing.")
        : Convert.ToHexString(SHA256.HashData(bytes));
}
