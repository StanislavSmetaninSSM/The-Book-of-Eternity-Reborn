using System.Security.Cryptography;

namespace BookOfEternityClient.Core;

public partial class FileSystemManager
{
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
        internal ValidationReadObservations((string Path, ValidationSnapshotFamily Family, bool Explicit,
            bool Exists, DateTime Timestamp, string? Digest)[] rows) => Rows = Array.AsReadOnly(rows.ToArray());

        /// <summary>
        /// Gets immutable original comparison rows; no row contains payload bytes or authority.
        /// </summary>
        internal IReadOnlyList<(string Path, ValidationSnapshotFamily Family, bool Explicit,
            bool Exists, DateTime Timestamp, string? Digest)> Rows { get; }

        /// <summary>
        /// Gets the number of distinct original reader identities represented by this observation set.
        /// </summary>
        internal int Count => Rows.Count;
    }

    internal sealed partial class ReservedValidationReadSet
    {
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
                    return (key.Path, key.Family, key.Explicit, snapshot != null,
                        snapshot?.LastWriteTimeUtc ?? default, snapshot == null ? null :
                            Convert.ToHexString(SHA256.HashData(snapshot.Content)));
                }).ToArray();
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
        /// True when all current bytes, absence and timestamps agree; false selects the original calculation.
        /// Reader failures propagate rather than becoming a match or calculation permission.
        /// </returns>
        internal async Task<bool> MatchesObservationsAsync(ValidationReadObservations observations)
        {
            ArgumentNullException.ThrowIfNull(observations);
            using var timing = _files.MeasureOperation("ValidationReadSet.ReplayObservations");
            foreach (var row in observations.Rows)
            {
                var declared = row.Explicit ? _lease : null;
                var snapshot = row.Family is ValidationSnapshotFamily.OrdinaryAsync or ValidationSnapshotFamily.ProtectedAsync
                    ? await ReadAsync(row.Path, row.Family, declared, CancellationToken.None)
                    : Read(row.Path, row.Family, declared);
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
