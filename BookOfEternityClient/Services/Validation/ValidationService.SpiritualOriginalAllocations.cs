using System.Text.Json.Nodes;
using BookOfEternityClient.Core;

namespace BookOfEternityClient.Services;

public partial class ValidationService
{
    /// <summary>
    /// Owns fresh allocation adapters and one guarded journal for an original capture attempt.
    /// </summary>
    private sealed class SpiritualOriginalAllocationOwner
    {
        /// <summary>
        /// Creates fresh adapters before any original input composition can allocate identities or time.
        /// </summary>
        /// <param name="journalJson">
        /// Exact retained allocation array, or an empty array for the first attempt.
        /// </param>
        /// <param name="replay">
        /// Forbids new allocations beyond the retained stream when <see langword="true"/>.
        /// </param>
        /// <param name="includeUpstreamIntake">
        /// Creates scoped item and location adapters only for the named upstream-intake path.
        /// </param>
        internal SpiritualOriginalAllocationOwner(string journalJson, bool replay, bool includeUpstreamIntake = false)
        {
            Journal = replay ? SpiritualWoundReplayJournal.CreateReplay(journalJson, requireScope: true) :
                SpiritualWoundReplayJournal.CreateAppend(journalJson, requireScope: true);
            Clock = new SpiritualWoundProjectionClock(Journal, new AcceptedTurnProjectionClock());
            Resources = new SpiritualWoundResourceIdentityFactory(Journal, new AcceptedMechanicsIdentityFactory());
            Effects = new SpiritualWoundEffectIdentityFactory(Journal, new EffectIdentityFactory());
            Vehicles = new SpiritualWoundVehicleIdentityFactory(Journal, new VehicleIdentityFactory());
            if (includeUpstreamIntake)
            {
                Items = new SpiritualWoundItemIdentityFactory(Journal, new MortalItemIdentityFactory());
                Locations = new SpiritualWoundLocationIdentityFactory(Journal, new MortalLocationIdentityFactory());
            }
        }

        /// <summary>
        /// Gets the guarded allocation stream shared by this attempt's owners.
        /// </summary>
        internal SpiritualWoundReplayJournal Journal { get; }
        /// <summary>
        /// Gets the shared projection clock and combined transaction entry point.
        /// </summary>
        internal SpiritualWoundProjectionClock Clock { get; }
        /// <summary>
        /// Gets the stable resource allocation adapter for the entire attempt.
        /// </summary>
        internal SpiritualWoundResourceIdentityFactory Resources { get; }
        /// <summary>
        /// Gets the stable effect and combatant allocation adapter for the entire attempt.
        /// </summary>
        internal SpiritualWoundEffectIdentityFactory Effects { get; }
        /// <summary>
        /// Gets the vehicle allocation adapter used by initial owner composition.
        /// </summary>
        internal SpiritualWoundVehicleIdentityFactory Vehicles { get; }
        /// <summary>
        /// Gets the private item intake factory, or <see langword="null"/> when ordinary prevalidated admission is used.
        /// </summary>
        internal SpiritualWoundItemIdentityFactory? Items { get; }
        /// <summary>
        /// Gets the shared location intake factory, or <see langword="null"/> for ordinary location planning.
        /// </summary>
        internal SpiritualWoundLocationIdentityFactory? Locations { get; }
    }

    internal sealed partial class SpiritualOriginalTurnCapture
    {
        /// <summary>
        /// Reads committed allocation progress from the current capture without requiring
        /// consumption of future retained journal rows.
        /// </summary>
        /// <param name="lease">
        /// Active canonical write lease authenticating this current capture.
        /// </param>
        /// <returns>
        /// Exact committed allocation position, without checkpoint or gameplay authority.
        /// </returns>
        internal int ReadAllocationCursor(FileSystemManager.CanonicalWriteLease lease)
        {
            if (!_gate.Wait(0))
                throw new InvalidOperationException("The original capture is continuing.");
            try
            {
                EnsureCurrent(lease);
                return _allocations.Journal.ReadCursor();
            }
            finally { _gate.Release(); }
        }

        /// <summary>
        /// Reads detached allocation evidence after accepted operations have consumed the retained prefix.
        /// The result grants no checkpoint or gameplay authority.
        /// </summary>
        /// <param name="lease">
        /// Active canonical write lease authenticating this current capture.
        /// </param>
        /// <returns>
        /// Exact ordered allocation rows; incomplete replay and active operations are rejected.
        /// </returns>
        internal JsonArray ReadAllocationJournal(FileSystemManager.CanonicalWriteLease lease)
        {
            if (!_gate.Wait(0))
                throw new InvalidOperationException("The original capture is continuing.");
            try
            {
                EnsureCurrent(lease);
                return _allocations.Journal.Export();
            }
            finally { _gate.Release(); }
        }
    }
}
