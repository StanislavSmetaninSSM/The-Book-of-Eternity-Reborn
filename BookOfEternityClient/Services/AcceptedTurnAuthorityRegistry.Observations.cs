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
    /// Observes one existing slot and its retained publication flags under the original slot and state locks.
    /// No slot, generation, state, receipt or capability is created, replaced, consumed or cleared.
    /// </summary>
    /// <param name="identity">
    /// The existing root identity to inspect; <see langword="null"/> is rejected.
    /// </param>
    /// <returns>
    /// Detached presence DATA. Absence is distinct from an existing state whose reservation and receipt are clear.
    /// This observation does not establish current-generation or publication authority.
    /// </returns>
    internal static ExistingJointPublicationObservation ObserveExistingJointPublication(CanonicalRootIdentity identity)
    {
        ArgumentNullException.ThrowIfNull(identity);
        return RootSlots.TryGetValue(identity, out var slot) ? slot.ObserveJointPublication() : default;
    }

    private sealed partial class RootAuthoritySlot
    {
        /// <summary>
        /// Reads the existing state under the slot lock without generation lookup or replacement.
        /// </summary>
        /// <returns>
        /// Presence DATA from the same retained state, or a present-slot/absent-state observation.
        /// </returns>
        internal ExistingJointPublicationObservation ObserveJointPublication()
        {
            lock (_gate)
                return _state?.ObserveJointPublication() ?? new(true, false, false, false);
        }
    }

    private sealed partial class AcceptedTurnAuthorityState
    {
        /// <summary>
        /// Reads reservation and open-receipt presence under the existing authority-state lock.
        /// </summary>
        /// <returns>
        /// Detached flags for this existing state without exposing the retained owner objects.
        /// </returns>
        internal ExistingJointPublicationObservation ObserveJointPublication()
        {
            lock (_gate)
                return new(true, true, _mortalWoundTreatmentPublicationReservation is not null,
                    _openJointRepairPublicationReceipt is not null);
        }
    }
}
