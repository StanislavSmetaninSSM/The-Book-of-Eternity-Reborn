using System.Security.Cryptography;

namespace BookOfEternityClient.Core;

public partial class FileSystemManager
{
    /// <summary>
    /// Records a successful original presence result only in the current functional validation phase.
    /// </summary>
    /// <param name="path">
    /// Raw path supplied to the original presence reader.
    /// </param>
    /// <param name="lease">
    /// Explicit original lease, or <see langword="null"/> for ambient reader semantics.
    /// </param>
    /// <param name="exists">
    /// Actual original result; recording never substitutes a cached presence result.
    /// </param>
    private void RememberValidationPresence(string path, CanonicalWriteLease? lease, bool exists)
    {
        if (ValidationReuseSuppression.Value == 0)
            _validationReadSet.Value?.RecordPresence(path, lease, exists);
    }

    /// <summary>
    /// Holds immutable source comparison metadata without retaining payload bytes or reader authority.
    /// </summary>
    internal sealed class ValidationReadObservations
    {
        /// <summary>
        /// Copies comparison metadata into an immutable container without retaining source buffers.
        /// </summary>
        /// <param name="rows">
        /// Ordered original identities with presence, timestamp and exact content digest.
        /// </param>
        internal ValidationReadObservations((string Path, ValidationSnapshotFamily? Family, bool Explicit,
            bool Exists, DateTime Timestamp, string? Digest)[] rows) => Rows = Array.AsReadOnly(rows.ToArray());

        /// <summary>
        /// Gets immutable original comparison rows; no row contains payload bytes or authority.
        /// </summary>
        internal IReadOnlyList<(string Path, ValidationSnapshotFamily? Family, bool Explicit,
            bool Exists, DateTime Timestamp, string? Digest)> Rows { get; }

        /// <summary>
        /// Gets the number of distinct original reader identities represented by this observation set.
        /// </summary>
        internal int Count => Rows.Count;
    }

    internal sealed partial class ReservedValidationReadSet
    {
        private readonly Dictionary<(string Path, bool Explicit), bool> _presence = new();
        private readonly List<(string Path, bool Explicit)> _presenceOrder = [];

        /// <summary>
        /// Retains an actual successful presence observation without skipping its original reader.
        /// </summary>
        /// <param name="path">
        /// Raw path observed by the original FileExists operation.
        /// </param>
        /// <param name="declared">
        /// Exact explicit lease, or <see langword="null"/> for ambient semantics.
        /// </param>
        /// <param name="exists">
        /// Actual original presence result.
        /// </param>
        internal void RecordPresence(string path, CanonicalWriteLease? declared, bool exists)
        {
            Enter();
            try
            {
                RequireDeclaredLease(declared);
                RequireRetainedFlow();
                RememberPresence(path, declared != null, exists);
                lock (_state) RequireHealthy();
            }
            catch { Fail(); throw; }
            finally { Leave(); }
        }

        /// <summary>
        /// Registers one exact presence identity and refuses contradictory observations within a reserved phase.
        /// </summary>
        /// <param name="path">
        /// Original raw path.
        /// </param>
        /// <param name="explicitLease">
        /// Whether the original caller supplied its lease explicitly.
        /// </param>
        /// <param name="exists">
        /// Current successfully observed presence.
        /// </param>
        private void RememberPresence(string path, bool explicitLease, bool exists)
        {
            var key = (path, explicitLease);
            if (_presence.TryGetValue(key, out var prior))
            {
                if (prior != exists) throw new InvalidDataException("Reserved validation presence changed: " + path);
            }
            else { _presence.Add(key, exists); _presenceOrder.Add(key); }
        }

        /// <summary>
        /// Runs the original presence contract without recursively registering its result.
        /// </summary>
        /// <param name="path">
        /// Original raw path.
        /// </param>
        /// <param name="explicitLease">
        /// Whether the original explicit reader rather than ambient reader is required.
        /// </param>
        /// <returns>
        /// The freshly observed original presence result; original failures propagate.
        /// </returns>
        private bool ProbePresence(string path, bool explicitLease)
        {
            using var suppression = SuppressValidationReuse();
            return explicitLease ? _files.FileExists(_lease, path) : _files.FileExists(path);
        }

        /// <summary>
        /// Replays one presence source and enlists its actual result in this phase's completion proof.
        /// </summary>
        /// <param name="path">
        /// Raw source path.
        /// </param>
        /// <param name="explicitLease">
        /// Original explicit or ambient reader mode.
        /// </param>
        /// <returns>
        /// Fresh current presence, after retained-flow validation.
        /// </returns>
        private bool ReadPresence(string path, bool explicitLease)
        {
            Enter();
            try
            {
                RequireRetainedFlow();
                var exists = ProbePresence(path, explicitLease);
                RequireRetainedFlow();
                RememberPresence(path, explicitLease, exists);
                lock (_state) { RequireHealthy(); return exists; }
            }
            catch { Fail(); throw; }
            finally { Leave(); }
        }

        /// <summary>
        /// Captures all successful observations in this current phase, including earlier baseline reads.
        /// </summary>
        /// <returns>
        /// Immutable source comparison metadata; it grants no source, lease or publication authority.
        /// </returns>
        internal ValidationReadObservations CaptureObservations()
        {
            Enter();
            try
            {
                using var timing = _files.MeasureOperation("ValidationReadSet.CaptureObservations");
                RequireRetainedFlow();
                var rows = _order.Select(key =>
                {
                    var snapshot = _snapshots[key];
                    return (key.Path, (ValidationSnapshotFamily?)key.Family, key.Explicit, snapshot != null,
                        snapshot?.LastWriteTimeUtc ?? default, snapshot == null ? null :
                            Convert.ToHexString(SHA256.HashData(snapshot.Content)));
                }).Concat(_presenceOrder.Select(key => (key.Path, (ValidationSnapshotFamily?)null, key.Explicit,
                    _presence[key], default(DateTime), (string?)null))).ToArray();
                lock (_state) { RequireHealthy(); return new(rows); }
            }
            catch { Fail(); throw; }
            finally { Leave(); }
        }

        /// <summary>
        /// Replays retained source identities through this phase for comparison and fresh exit-proof participation.
        /// </summary>
        /// <param name="observations">
        /// Complete successful source metadata retained with detached calculation DATA.
        /// </param>
        /// <returns>
        /// <see langword="true"/> when all current bytes, absence and timestamps agree;
        /// <see langword="false"/> selects the original calculation.
        /// Reader failures propagate rather than becoming a match or calculation permission.
        /// </returns>
        internal async Task<bool> MatchesObservationsAsync(ValidationReadObservations observations)
        {
            ArgumentNullException.ThrowIfNull(observations);
            using var timing = _files.MeasureOperation("ValidationReadSet.ReplayObservations");
            foreach (var row in observations.Rows)
            {
                if (row.Family is not { } family)
                {
                    if (ReadPresence(row.Path, row.Explicit) != row.Exists) return false;
                    continue;
                }
                var declared = row.Explicit ? _lease : null;
                var snapshot = family is ValidationSnapshotFamily.OrdinaryAsync or ValidationSnapshotFamily.ProtectedAsync
                    ? await ReadAsync(row.Path, family, declared, CancellationToken.None)
                    : Read(row.Path, family, declared);
                if ((snapshot != null) != row.Exists || snapshot != null &&
                    (snapshot.LastWriteTimeUtc != row.Timestamp ||
                        Convert.ToHexString(SHA256.HashData(snapshot.Content)) != row.Digest)) return false;
            }
            // Empty observation sets also require a healthy retained flow.
            Enter();
            try { RequireRetainedFlow(); lock (_state) RequireHealthy(); }
            catch { Fail(); throw; }
            finally { Leave(); }
            return true;
        }
    }
}
