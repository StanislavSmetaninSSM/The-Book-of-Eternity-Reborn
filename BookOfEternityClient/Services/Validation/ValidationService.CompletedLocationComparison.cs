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
        FileSystemManager files, FileSystemManager.CanonicalWriteLease lease)
    {
        files.EnsureCanonicalWriteLeaseActive(lease);
        if (files.HasBrowserOriginalOperation) return null;
        var manifest = await files.ReadFileBytesAsync(lease, LiveTurnPreparationService.PendingTurnSnapshotManifestPath);
        if (manifest is null) return null;
        var read = PendingTurnSnapshotReader.ReadCurrent(files, lease, LocationOriginalPaths);
        if (!read.Success || read.Snapshot is null || read.Snapshot.Realm != "mortal_world") return null;
        var originalMap = read.Snapshot.ReadRequiredBytes(MortalLocationMaterializationContract.WorldMapPath);
        if (originalMap is null) return null;
        var generation = files.ReadExistingSessionGeneration(lease)
            ?? throw new InvalidDataException("Location comparison requires an original generation.");
        var bindings = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var path in LocationBindingPaths) bindings.Add(path, LocationHash(await files.ReadFileBytesAsync(lease, path)));
        return new LocationComparisonCapture(LocationComparisonKey, files, generation, read.Snapshot, bindings);
    }

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
        internal LocationComparisonCapture(object key, FileSystemManager files, string generation,
            PendingTurnSnapshotReadAuthority original, IReadOnlyDictionary<string, string> bindings)
        {
            if (!ReferenceEquals(key, LocationComparisonKey)) throw new InvalidOperationException("Original issuance required.");
            _files = files; _generation = generation; _original = original; _bindings = bindings;
        }

        /// <summary>Reauthenticates the same original under the caller's genuine current lease.</summary>
        internal async Task RequireCurrentAsync(FileSystemManager files, FileSystemManager.CanonicalWriteLease lease)
        {
            using var fresh = FileSystemManager.SuppressValidationReuse();
            if (!ReferenceEquals(files, _files) || !files.IsCurrentSessionGeneration(lease, _generation))
                throw new InvalidDataException("Completed location original owner changed.");
            var read = PendingTurnSnapshotReader.ReadCurrent(files, lease, LocationOriginalPaths);
            if (!read.Success || read.Snapshot is not { } current || current.SessionId != _original.SessionId ||
                current.RequestId != _original.RequestId || current.TurnNumber != _original.TurnNumber ||
                current.SnapshotToken != _original.SnapshotToken || current.Realm != _original.Realm)
                throw new InvalidDataException("Completed location original binding changed.");
            foreach (var path in LocationOriginalPaths)
                if (!current.ReadRequiredBytes(path).AsSpan().SequenceEqual(_original.ReadRequiredBytes(path)))
                    throw new InvalidDataException("Completed location original bytes changed.");
            foreach (var pair in _bindings)
                if (LocationHash(await files.ReadFileBytesAsync(lease, pair.Key)) != pair.Value)
                    throw new InvalidDataException("Completed location request binding changed.");
        }

        /// <summary>Seals only after the ordinary refresh has validated its actual published image.</summary>
        internal async Task<CompletedLocationComparison> CompleteAsync(FileSystemManager files,
            FileSystemManager.CanonicalWriteLease lease)
        {
            await RequireCurrentAsync(files, lease);
            var map = await files.ReadFileBytesAsync(lease, MortalLocationMaterializationContract.WorldMapPath);
            var index = await files.ReadFileBytesAsync(lease, MortalLocationIdentityState.StatePath);
            using var doc = JsonDocument.Parse(map!);
            var ids = ReadExactCanonicalWorldMapLocationIds(doc.RootElement).ToHashSet(StringComparer.Ordinal);
            return new CompletedLocationComparison(LocationComparisonKey, this, LocationHash(map), LocationHash(index), ids);
        }
    }

    /// <summary>Immutable membership of one successfully published image; repairs may recheck, never reseal it.</summary>
    internal sealed class CompletedLocationComparison
    {
        private readonly LocationComparisonCapture _original;
        private readonly string _mapHash, _indexHash;
        private readonly HashSet<string> _ids;
        internal CompletedLocationComparison(object key, LocationComparisonCapture original, string mapHash,
            string indexHash, HashSet<string> ids)
        {
            if (!ReferenceEquals(key, LocationComparisonKey)) throw new InvalidOperationException("Completed publication required.");
            _original = original; _mapHash = mapHash; _indexHash = indexHash; _ids = ids;
        }
        /// <summary>Checks exact published dependencies at both ends of each consuming invocation.</summary>
        internal async Task RequireCurrentAsync(FileSystemManager files, FileSystemManager.CanonicalWriteLease lease)
        {
            using var fresh = FileSystemManager.SuppressValidationReuse();
            await _original.RequireCurrentAsync(files, lease);
            if (LocationHash(await files.ReadFileBytesAsync(lease, MortalLocationMaterializationContract.WorldMapPath)) != _mapHash ||
                LocationHash(await files.ReadFileBytesAsync(lease, MortalLocationIdentityState.StatePath)) != _indexHash)
                throw new InvalidDataException("Completed location published image changed.");
        }
        /// <summary>Returns detached membership after fresh opening proof, not an authority token.</summary>
        internal HashSet<string> CopyIds() => new(_ids, StringComparer.Ordinal);
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
