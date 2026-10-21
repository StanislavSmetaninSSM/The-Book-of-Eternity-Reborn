using BookOfEternityClient.Core;

namespace BookOfEternityClient.Services;

/// <summary>
/// Reports presence of existing registry storage without exposing capabilities, receipts or mutable state.
/// </summary>
/// <param name="RootSlotPresent">
/// Whether the selected identity already has a registry slot.
/// </param>
/// <param name="AuthorityStatePresent">
/// Whether that existing slot contains an authority state.
/// </param>
/// <param name="TreatmentReservationPresent">
/// Whether that state retains a treatment publication reservation.
/// </param>
/// <param name="OpenJointRepairReceiptPresent">
/// Whether that state retains an open joint repair publication receipt.
/// </param>
internal readonly record struct ExistingJointPublicationObservation(
    bool RootSlotPresent,
    bool AuthorityStatePresent,
    bool TreatmentReservationPresent,
    bool OpenJointRepairReceiptPresent);

internal static partial class AcceptedTurnAuthorityRegistry
{
    /// <summary>
    /// Provides the neutral absence scaffold for a read-only existing-slot observation during causal RED verification.
    /// This scaffold is not used by runtime assertions and does not initialize or mutate authority.
    /// </summary>
    /// <param name="identity">
    /// The existing root identity to inspect; <see langword="null"/> is rejected.
    /// </param>
    /// <returns>
    /// Neutral absence until the positive existing-slot control has demonstrated its causal RED failure.
    /// </returns>
    internal static ExistingJointPublicationObservation ObserveExistingJointPublication(CanonicalRootIdentity identity)
    {
        ArgumentNullException.ThrowIfNull(identity);
        return default;
    }
}
