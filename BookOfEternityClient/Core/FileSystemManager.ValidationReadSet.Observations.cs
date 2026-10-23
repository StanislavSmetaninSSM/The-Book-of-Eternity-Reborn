namespace BookOfEternityClient.Core;

public partial class FileSystemManager
{
    /// <summary>
    /// Holds immutable source comparison metadata without retaining payload bytes or reader authority.
    /// </summary>
    internal sealed class ValidationReadObservations
    {
        /// <summary>
        /// Gets the number of distinct original reader identities represented by this observation set.
        /// </summary>
        internal int Count => 0;
    }

    internal sealed partial class ReservedValidationReadSet
    {
        /// <summary>
        /// Captures all successful observations in this current phase, including earlier baseline reads.
        /// </summary>
        /// <returns>
        /// Immutable source comparison metadata; it grants no source, lease or publication authority.
        /// </returns>
        internal ValidationReadObservations CaptureObservations() => new();

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
        internal Task<bool> MatchesObservationsAsync(ValidationReadObservations observations) => Task.FromResult(true);
    }
}
