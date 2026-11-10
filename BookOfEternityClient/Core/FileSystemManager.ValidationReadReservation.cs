namespace BookOfEternityClient.Core;

public partial class FileSystemManager
{
    /// <summary>
    /// Identifies a read reservation on an existing main access; it grants no ownership itself.
    /// </summary>
    internal abstract class ValidationReadReservation
    {
        /// <summary>
        /// Exact existing physical admission retained by this read, or <see langword="null"/> for owned acquisition.
        /// </summary>
        internal virtual MainAdmission? RetainedPhysicalAdmission => null;

        /// <summary>
        /// Whether the original acquisition may still perform its ordinary recovery.
        /// </summary>
        internal abstract bool Opening { get; }

        /// <summary>
        /// Whether the reservation has irreversibly stopped admitting readers.
        /// </summary>
        internal abstract bool Closed { get; }
    }
}
