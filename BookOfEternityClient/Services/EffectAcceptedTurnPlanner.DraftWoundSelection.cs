using System.Runtime.CompilerServices;

namespace BookOfEternityClient.Services;

internal static partial class EffectAcceptedTurnPlanner
{
    /// <summary>
    /// Routes only concrete validator-owned selections into the shared draft; detached data cannot create authority.
    /// </summary>
    internal sealed class EffectDraftWoundSelection
    {
        private static readonly ConditionalWeakTable<ValidationService.SpiritualOriginalTurnCapture.WoundSelection,
            EffectDraftWoundSelection> SpiritualSelections = new();
        private static readonly ConditionalWeakTable<ValidationService.MortalOriginalTurnCapture.WoundSelection,
            EffectDraftWoundSelection> MortalSelections = new();
        private readonly ValidationService.SpiritualOriginalTurnCapture.WoundSelection? _spiritual;
        private readonly ValidationService.MortalOriginalTurnCapture.WoundSelection? _mortal;

        /// <summary>
        /// Retains an actual spiritual selection without copying its ownership.
        /// </summary>
        /// <param name="selection">
        /// Selection issued and registered by its original validator capture.
        /// </param>
        private EffectDraftWoundSelection(ValidationService.SpiritualOriginalTurnCapture.WoundSelection selection) =>
            _spiritual = selection;

        /// <summary>
        /// Retains an actual Mortal selection without copying its ownership.
        /// </summary>
        /// <param name="selection">
        /// Selection issued and registered at the capture's current resource checkpoint.
        /// </param>
        private EffectDraftWoundSelection(ValidationService.MortalOriginalTurnCapture.WoundSelection selection) =>
            _mortal = selection;

        /// <summary>
        /// Gets the stable adapter for one concrete spiritual selection.
        /// </summary>
        /// <param name="selection">
        /// Actual selection; ownership is checked again whenever the adapter is consumed.
        /// </param>
        /// <returns>
        /// The retained adapter for exactly this selection object.
        /// </returns>
        internal static EffectDraftWoundSelection From(ValidationService.SpiritualOriginalTurnCapture.WoundSelection selection) =>
            SpiritualSelections.GetValue(selection, static value => new(value));

        /// <summary>
        /// Gets the stable adapter for one concrete Mortal selection.
        /// </summary>
        /// <param name="selection">
        /// Actual selection; ownership is checked again whenever the adapter is consumed.
        /// </param>
        /// <returns>
        /// The retained adapter for exactly this selection object.
        /// </returns>
        internal static EffectDraftWoundSelection From(ValidationService.MortalOriginalTurnCapture.WoundSelection selection) =>
            MortalSelections.GetValue(selection, static value => new(value));

        /// <summary>
        /// Gets detached validated wound input; the input itself grants no insertion authority.
        /// </summary>
        internal WoundAcceptedTurnInput Input => _spiritual?.Input ?? _mortal!.Input;
        /// <summary>
        /// Gets the validated proposal's optional diagnostic coordinates.
        /// </summary>
        internal EffectApplicationDiagnosticLocations? Locations => _spiritual != null ? _spiritual.Locations : _mortal!.Locations;
        /// <summary>
        /// Gets the actual closed spiritual interval, or null for an ordinary resource checkpoint.
        /// </summary>
        internal AcceptedMechanicsPlanner.SpiritualExchangeInterval? Interval => _spiritual?.Admission.Interval;
        /// <summary>
        /// Gets the actual ordinary checkpoint, or null for a spiritual interval.
        /// </summary>
        internal AcceptedMechanicsPlanner.ResourceClosedBoundaryCheckpoint? Checkpoint => _mortal?.Checkpoint;
        /// <summary>
        /// Gets the actual resource routing owner associated with this current selection.
        /// </summary>
        internal BaseResourceRouting? Routing => _spiritual?.Routing ?? _mortal?.Routing;
        /// <summary>
        /// Gets the accepted effect prefix from the actual owned boundary.
        /// </summary>
        internal AcceptedEffectBoundaryTranscript.ClosedPrefix Prefix => Interval?.EffectAfter ?? Checkpoint!.EffectPrefix;

        /// <summary>
        /// Rechecks the concrete selection's registration and current resource frontier.
        /// </summary>
        /// <param name="draft">
        /// Shared effect draft requesting insertion authority.
        /// </param>
        /// <returns>
        /// True only for the current registered selection of this draft; otherwise false.
        /// </returns>
        internal bool IsCurrentFor(EffectAcceptedDraft draft) =>
            _spiritual?.IsCurrentFor(draft) ?? _mortal!.IsCurrentFor(draft);

        /// <summary>
        /// Reads the authenticated original wound state through its actual selection owner.
        /// </summary>
        /// <param name="draft">
        /// Draft whose ownership must still agree with the selection.
        /// </param>
        /// <param name="issues">
        /// Receives stale ownership or invalid original state diagnostics.
        /// </param>
        /// <returns>
        /// Detached original state, or null on rejection.
        /// </returns>
        internal WoundOperationBeforeData? ReadInitialState(EffectAcceptedDraft draft, List<ValidationIssue> issues) =>
            _spiritual != null ? _spiritual.ReadInitialState(draft, issues) : _mortal!.ReadInitialState(draft, issues);
    }
}
