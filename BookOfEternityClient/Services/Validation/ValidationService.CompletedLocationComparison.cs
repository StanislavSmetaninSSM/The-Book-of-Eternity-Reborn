using System.Security.Cryptography;
using System.Text.Json;
using BookOfEternityClient.Core;

namespace BookOfEternityClient.Services;

public partial class ValidationService
{
    /// <summary>Validates the actual completed refresh without treating a result DTO as authority.</summary>
    internal Task<List<ValidationIssue>> ValidateCompletedLocationStateAsync(
        AcceptedTurnCanonicalStateRefresh.Result published, GameStateValidationSelection selection)
    {
        ArgumentNullException.ThrowIfNull(published);
        if (published.Issues.Any(issue => issue.Severity == IssueSeverity.Error) || published.LocationComparison is null)
            throw new InvalidDataException("Completed location publication evidence is required.");
        return ValidateCompletedLocationStateAsync(published.LocationComparison, selection);
    }

    /// <summary>Starts one exclusive comparison; every repair iteration must call this afresh.</summary>
    internal Task<List<ValidationIssue>> ValidateCompletedLocationStateAsync(
        CompletedLocationComparison published, GameStateValidationSelection selection) =>
        StartAdmittedGameStateAsync(selection, false, completedLocation: published);

    /// <summary>Captures genuine original bindings only inside the ordinary publication owner.</summary>
    internal static async Task<LocationComparisonCapture?> CaptureLocationComparisonAsync(
        FileSystemManager files, FileSystemManager.CanonicalWriteLease lease,
        byte[]? rawNpcCore = null, byte[]? rawNpcActivities = null)
    {
        files.EnsureCanonicalWriteLeaseActive(lease);
        if (files.HasBrowserOriginalOperation)
        {
            // Only raw location birth candidates need a new location membership
            // comparison. This shape check selects an observer, not authority.
            var currentRaw = await files.ReadFileAsync(lease, MortalLocationMaterializationContract.CurrentLocationPath);
            if (!ContainsLocationBirth(currentRaw, current: true) &&
                !ContainsLocationBirth(await files.ReadFileAsync(lease, MortalLocationMaterializationContract.WorldMapPath), current: false))
                return null;
        }
        var manifest = await files.ReadFileBytesAsync(lease, LiveTurnPreparationService.PendingTurnSnapshotManifestPath);
        if (manifest is null) return null;
        var compareNpc = !files.HasBrowserOriginalOperation && NeedsOffscreenNpcProjection(rawNpcCore, rawNpcActivities);
        string[] originalPaths = compareNpc ? [.. LocationOriginalPaths, NpcCoreChangesContract.NpcCorePath] : LocationOriginalPaths;
        var read = PendingTurnSnapshotReader.ReadCurrent(files, lease, originalPaths);
        if (!read.Success && compareNpc)
        {
            // An optional NPC observer cannot change ordinary location-only admission.
            compareNpc = false;
            originalPaths = LocationOriginalPaths;
            read = PendingTurnSnapshotReader.ReadCurrent(files, lease, originalPaths);
        }
        if (!read.Success || read.Snapshot is null || read.Snapshot.Realm != "mortal_world") return null;
        var originalMap = read.Snapshot.ReadRequiredBytes(MortalLocationMaterializationContract.WorldMapPath);
        if (originalMap is null) return null;
        var generation = files.ReadExistingSessionGeneration(lease)
            ?? throw new InvalidDataException("Location comparison requires an original generation.");
        var bindings = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var path in LocationBindingPaths) bindings.Add(path, LocationHash(await files.ReadFileBytesAsync(lease, path)));
        var browser = files.CaptureBrowserLocationComparisonOwner(lease, read.Snapshot.SessionId,
            read.Snapshot.RequestId, read.Snapshot.TurnNumber, read.Snapshot.SnapshotToken);
        var npc = compareNpc ? BuildOffscreenNpcProjection(
            read.Snapshot.ReadRequiredBytes(NpcCoreChangesContract.NpcCorePath), rawNpcCore, rawNpcActivities,
            read.Snapshot.TurnNumber) : null;
        if (npc is null) originalPaths = LocationOriginalPaths;
        return new LocationComparisonCapture(LocationComparisonKey, files, generation, read.Snapshot, bindings, browser, originalPaths, npc);
    }

    private static bool ContainsLocationBirth(string? json, bool current)
    {
        if (string.IsNullOrWhiteSpace(json)) return false;
        try
        {
            using var document = JsonDocument.Parse(json);
            var root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object) return false;
            return current
                ? root.TryGetProperty("currentLocationData", out var location) &&
                  location.ValueKind == JsonValueKind.Object && location.TryGetProperty("locationId", out var id) &&
                  id.ValueKind == JsonValueKind.Null
                : root.TryGetProperty("worldMapUpdates", out var updates) && updates.ValueKind == JsonValueKind.Object &&
                  updates.TryGetProperty("newLocations", out var locations) && locations.ValueKind == JsonValueKind.Array &&
                  locations.GetArrayLength() != 0;
        }
        catch (JsonException) { return false; } // Existing raw validation rejects invalid declarations.
    }

    /// <summary>Exact previously published comparison lost its original or published binding; never repair by resealing.</summary>
    internal sealed class CompletedLocationComparisonRefusedException(string message) : IOException(message);

    private static readonly object LocationComparisonKey = new();
    private static readonly string[] LocationOriginalPaths = [MortalLocationMaterializationContract.WorldMapPath,
        MortalLocationIdentityState.StatePath, "game_state/meta/soul_state.json"];
    private static readonly string[] LocationBindingPaths = [LiveTurnPreparationService.TurnRequestPath,
        LiveTurnPreparationService.PendingTurnSnapshotManifestPath, PendingTurnSnapshotAuthority.AuthorityPath];

    /// <summary>Hashes required evidence; absence cannot become an empty successful proof.</summary>
    private static string LocationHash(byte[]? bytes) => bytes is null
        ? throw new InvalidDataException("Location comparison evidence is missing.")
        : Convert.ToHexString(SHA256.HashData(bytes));

    /// <summary>Private-origin original capture, never a publication lease or mutation permission.</summary>
    internal sealed class LocationComparisonCapture
    {
        private readonly FileSystemManager _files;
        private readonly string _generation;
        private readonly PendingTurnSnapshotReadAuthority _original;
        private readonly IReadOnlyDictionary<string, string> _bindings;
        private readonly FileSystemManager.BrowserOriginalOperationScope? _browserOwner;
        private readonly IReadOnlyCollection<string> _originalPaths;
        private readonly OffscreenNpcProjection? _offscreenNpc;
        private string? _publishedNpcHash, _publishedActivityHash;
        private int _completionTaken;
        private byte[]? _publishedMap;
        private string? _publishedIndexHash;
        internal LocationComparisonCapture(object key, FileSystemManager files, string generation,
            PendingTurnSnapshotReadAuthority original, IReadOnlyDictionary<string, string> bindings,
            FileSystemManager.BrowserOriginalOperationScope? browserOwner, IReadOnlyCollection<string> originalPaths,
            OffscreenNpcProjection? offscreenNpc)
        {
            if (!ReferenceEquals(key, LocationComparisonKey)) throw new InvalidOperationException("Original issuance required.");
            _files = files; _generation = generation; _original = original; _bindings = bindings; _browserOwner = browserOwner;
            _originalPaths = originalPaths; _offscreenNpc = offscreenNpc;
        }

        /// <summary>Reauthenticates the same original under the caller's genuine current lease.</summary>
        internal async Task RequireCurrentAsync(FileSystemManager files, FileSystemManager.CanonicalWriteLease lease)
        {
            using var fresh = FileSystemManager.SuppressValidationReuse();
            if (!ReferenceEquals(files, _files) || !files.IsCurrentSessionGeneration(lease, _generation))
                throw new CompletedLocationComparisonRefusedException("Completed location original owner changed.");
            if (!ReferenceEquals(_browserOwner, files.CaptureBrowserLocationComparisonOwner(lease,
                    _original.SessionId, _original.RequestId, _original.TurnNumber, _original.SnapshotToken)))
                throw new CompletedLocationComparisonRefusedException("Completed location browser scope changed.");
            var read = PendingTurnSnapshotReader.ReadCurrent(files, lease, _originalPaths);
            if (!read.Success || read.Snapshot is not { } current || current.SessionId != _original.SessionId ||
                current.RequestId != _original.RequestId || current.TurnNumber != _original.TurnNumber ||
                current.SnapshotToken != _original.SnapshotToken || current.Realm != _original.Realm)
                throw new CompletedLocationComparisonRefusedException("Completed location original binding changed.");
            foreach (var path in _originalPaths)
                if (!current.ReadRequiredBytes(path).AsSpan().SequenceEqual(_original.ReadRequiredBytes(path)))
                    throw new CompletedLocationComparisonRefusedException("Completed location original bytes changed.");
            foreach (var pair in _bindings)
            {
                var currentBytes = await files.ReadFileBytesAsync(lease, pair.Key);
                if (currentBytes is null || LocationHash(currentBytes) != pair.Value)
                    throw new CompletedLocationComparisonRefusedException("Completed location request binding changed.");
            }
        }

        /// <summary>Binds exact output immediately after actual normalization, before awaited post-validation.</summary>
        internal async Task BindPublishedImageAsync(FileSystemManager files,
            FileSystemManager.CanonicalWriteLease lease, object publicationIssuer)
        {
            if (!AcceptedTurnCanonicalStateRefresh.IsLocationPublicationIssuer(publicationIssuer))
                throw new InvalidOperationException("Only successful ordinary publication can bind location output.");
            if (_publishedMap is not null || Volatile.Read(ref _completionTaken) != 0)
                throw new InvalidOperationException("Location publication output was already bound.");
            await RequireCurrentAsync(files, lease);
            var map = await files.ReadFileBytesAsync(lease, MortalLocationMaterializationContract.WorldMapPath);
            var index = await files.ReadFileBytesAsync(lease, MortalLocationIdentityState.StatePath);
            _publishedIndexHash = LocationHash(index);
            _ = LocationHash(map);
            _publishedMap = map!.ToArray();
            if (_offscreenNpc is not null)
            {
                var npc = await files.ReadFileBytesAsync(lease, NpcCoreChangesContract.NpcCorePath);
                if (npc is not null && _offscreenNpc.MatchesCompleteRoot(npc))
                {
                    var activity = await files.ReadFileBytesAsync(lease, OffscreenNpcActivityPath);
                    if (!_offscreenNpc.MatchesActivityAfterImage(activity))
                        return; // Neither a pending command nor a later sidecar substitution is settled.
                    _publishedActivityHash = activity is null ? null : LocationHash(activity);
                    _publishedNpcHash = LocationHash(npc);
                }
            }
        }

        /// <summary>Seals only after the ordinary refresh has validated its actual published image.</summary>
        internal async Task<CompletedLocationComparison> CompleteAsync(FileSystemManager files,
            FileSystemManager.CanonicalWriteLease lease, object publicationIssuer)
        {
            if (!AcceptedTurnCanonicalStateRefresh.IsLocationPublicationIssuer(publicationIssuer))
                throw new InvalidOperationException("Only successful ordinary publication can issue location comparison.");
            if (Interlocked.Exchange(ref _completionTaken, 1) != 0)
                throw new InvalidOperationException("Location publication comparison was already completed.");
            await RequireCurrentAsync(files, lease);
            var map = await files.ReadFileBytesAsync(lease, MortalLocationMaterializationContract.WorldMapPath);
            var index = await files.ReadFileBytesAsync(lease, MortalLocationIdentityState.StatePath);
            if (_publishedMap is null || _publishedIndexHash is null ||
                !map.AsSpan().SequenceEqual(_publishedMap) || LocationHash(index) != _publishedIndexHash)
                throw new CompletedLocationComparisonRefusedException("Location publication output changed before completion.");
            using var doc = JsonDocument.Parse(CanonicalJsonUtf8.DecodeOneOptionalBom(_publishedMap));
            var ids = ReadExactCanonicalWorldMapLocationIds(doc.RootElement).ToHashSet(StringComparer.Ordinal);
            await RequirePublishedOffscreenNpcAsync(files, lease);
            return new CompletedLocationComparison(LocationComparisonKey, this, LocationHash(map), LocationHash(index), ids,
                _publishedNpcHash is null ? null : _offscreenNpc);
        }

        internal async Task RequirePublishedOffscreenNpcAsync(FileSystemManager files, FileSystemManager.CanonicalWriteLease lease)
        {
            if (_publishedNpcHash is null) return;
            var current = await files.ReadFileBytesAsync(lease, NpcCoreChangesContract.NpcCorePath);
            if (current is null || LocationHash(current) != _publishedNpcHash)
                throw new CompletedLocationComparisonRefusedException("Completed off-screen NPC published image changed.");
            var activities = await files.ReadFileBytesAsync(lease, OffscreenNpcActivityPath);
            if ((activities is null ? null : LocationHash(activities)) != _publishedActivityHash)
                throw new CompletedLocationComparisonRefusedException("Completed off-screen NPC activity carrier changed.");
        }
    }

    /// <summary>Immutable membership of one successfully published image; repairs may recheck, never reseal it.</summary>
    internal sealed class CompletedLocationComparison
    {
        private readonly LocationComparisonCapture _original;
        private readonly string _mapHash, _indexHash;
        private readonly HashSet<string> _ids;
        internal CompletedLocationComparison(object key, LocationComparisonCapture original, string mapHash,
            string indexHash, HashSet<string> ids, OffscreenNpcProjection? offscreenNpc)
        {
            if (!ReferenceEquals(key, LocationComparisonKey)) throw new InvalidOperationException("Completed publication required.");
            _original = original; _mapHash = mapHash; _indexHash = indexHash; _ids = ids;
            OffscreenNpcRows = offscreenNpc;
        }
        /// <summary>Checks exact published dependencies at both ends of each consuming invocation.</summary>
        internal async Task RequireCurrentAsync(FileSystemManager files, FileSystemManager.CanonicalWriteLease lease)
        {
            using var fresh = FileSystemManager.SuppressValidationReuse();
            await _original.RequireCurrentAsync(files, lease);
            var map = await files.ReadFileBytesAsync(lease, MortalLocationMaterializationContract.WorldMapPath);
            var index = await files.ReadFileBytesAsync(lease, MortalLocationIdentityState.StatePath);
            if (map is null || index is null || LocationHash(map) != _mapHash || LocationHash(index) != _indexHash)
                throw new CompletedLocationComparisonRefusedException("Completed location published image changed.");
            await _original.RequirePublishedOffscreenNpcAsync(files, lease);
        }
        /// <summary>Returns detached membership after fresh opening proof, not an authority token.</summary>
        internal HashSet<string> CopyIds() => new(_ids, StringComparer.Ordinal);
        internal OffscreenNpcProjection? OffscreenNpcRows { get; }
    }

    /// <summary>Only the admitted state invocation can consume its checked membership.</summary>
    private HashSet<string>? ReadCompletedLocationIds()
    {
        lock (_worldReadGate)
        {
            var invocation = _worldValidationInvocation.Value;
            if (invocation?.LocationIds is null) return null;
            if (invocation.Closed || !ReferenceEquals(_worldReservation, invocation))
                throw new InvalidOperationException("Completed location invocation is no longer current.");
            return new HashSet<string>(invocation.LocationIds, StringComparer.Ordinal);
        }
    }
}
