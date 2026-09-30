using System.Text.Json;
using BookOfEternityClient.Core;

namespace BookOfEternityClient.Services;

public partial class ValidationService
{
    /// <summary>
    /// Returns a detached unpublished wound or validation issues from the original turn's insertion operation.
    /// It is not a completed turn or publication authority.
    /// </summary>
    internal sealed class OriginalWoundMaterializationResult
    {
        private readonly WoundMaterializationEnvelope? _wound;

        /// <summary>
        /// Retains detached result data from a completed insertion attempt.
        /// </summary>
        /// <param name="wound">
        /// Actual inserted wound, or <see langword="null"/> on decline or rejection.
        /// </param>
        /// <param name="issues">
        /// Validation failures; empty on successful insertion or a valid decline.
        /// </param>
        internal OriginalWoundMaterializationResult(WoundMaterializationEnvelope? wound,
            IReadOnlyList<ValidationIssue> issues)
        {
            _wound = WoundAcceptedTurnData.CloneWound(wound);
            Issues = Array.AsReadOnly(issues.ToArray());
        }

        /// <summary>
        /// Gets a detached wound with the actual allocated root identities, or <see langword="null"/>.
        /// </summary>
        public WoundMaterializationEnvelope? Wound => WoundAcceptedTurnData.CloneWound(_wound);

        /// <summary>
        /// Gets the validation issues recorded by the insertion attempt.
        /// </summary>
        public IReadOnlyList<ValidationIssue> Issues { get; }
    }

    internal sealed partial class SpiritualOriginalTurnCapture
    {
        private readonly Dictionary<WoundSelection, OriginalWoundMaterializationResult> _materializedWounds = new();
        private readonly Dictionary<WoundSelection,
            EffectAcceptedTurnPlanner.EffectAcceptedDraft.EffectDraftWoundInsertion>
            _registeredWoundInsertions = new();

        /// <summary>
        /// Validates, prepares, applies and registers one wound creation or worsening under the original capture's exclusive gate.
        /// Installs current routing for subsequent exchanges; common publication remains separately guarded.
        /// </summary>
        /// <param name="lease">
        /// Active canonical write lease used to revalidate signed and candidate inputs.
        /// </param>
        /// <param name="admission">
        /// Exact current source admission; copied, stale or foreign admissions are rejected.
        /// </param>
        /// <param name="decision">
        /// Source-bound GM decision, frozen before awaiting the capture gate.
        /// </param>
        /// <param name="finalScene">
        /// Scene text containing the required materialization notification, or <see langword="null"/>.
        /// </param>
        /// <returns>
        /// The retained exact-retry result, a valid decline, or issues preventing insertion.
        /// </returns>
        internal Task<OriginalWoundMaterializationResult> MaterializeWoundAsync(
            FileSystemManager.CanonicalWriteLease lease, WoundSourceAdmission admission,
            JsonElement decision, string? finalScene) =>
            WoundSelection.MaterializeAsync(this, lease, admission, decision.Clone(), finalScene);

        internal sealed partial class WoundSelection
        {
            /// <summary>
            /// Executes the original capture's insertion transaction using its private selection validator.
            /// Failures after draft mutation revoke the unpublished capture instead of permitting a partial retry.
            /// </summary>
            /// <param name="owner">
            /// Original turn capture owning source, effect draft and resource executor.
            /// </param>
            /// <param name="lease">
            /// Active canonical write lease for fresh ownership checks.
            /// </param>
            /// <param name="admission">
            /// Exact admission to validate before any retained-result lookup.
            /// </param>
            /// <param name="decision">
            /// Detached decision supplied by the capture entrypoint.
            /// </param>
            /// <param name="finalScene">
            /// Scene text included in decision retry validation.
            /// </param>
            /// <returns>
            /// Detached insertion data and issues; no turn completion authority is produced.
            /// </returns>
            internal static async Task<OriginalWoundMaterializationResult> MaterializeAsync(
                SpiritualOriginalTurnCapture owner, FileSystemManager.CanonicalWriteLease lease,
                WoundSourceAdmission admission, JsonElement decision, string? finalScene)
            {
                ArgumentNullException.ThrowIfNull(admission);
                await owner._gate.WaitAsync();
                try
                {
                    return await MaterializeCoreAsync(owner, lease, admission, decision, finalScene);
                }
                finally { owner._gate.Release(); }
            }

            /// <summary>
            /// Applies one source-bound decision while the caller holds the capture gate.
            /// A failed post-mutation attempt revokes the complete capture.
            /// </summary>
            /// <param name="owner">
            /// Current original capture retaining the source and effect draft.
            /// </param>
            /// <param name="lease">
            /// Active canonical lease for current input checks.
            /// </param>
            /// <param name="admission">
            /// Exact retained source admission.
            /// </param>
            /// <param name="decision">
            /// Frozen GM decision to validate and execute.
            /// </param>
            /// <param name="finalScene">
            /// Frozen final scene text for the wound notification contract.
            /// </param>
            /// <returns>
            /// Detached wound or decline result, or validation issues.
            /// </returns>
            internal static async Task<OriginalWoundMaterializationResult> MaterializeCoreAsync(
                SpiritualOriginalTurnCapture owner, FileSystemManager.CanonicalWriteLease lease,
                WoundSourceAdmission admission, JsonElement decision, string? finalScene)
            {
                var attemptedDraftMutation = false;
                try
                {
                    var issues = new List<ValidationIssue>();
                    if (owner._woundSelections.TryGetValue(admission, out var appliedSelection) &&
                        owner._materializedWounds.TryGetValue(appliedSelection, out var appliedResult))
                    {
                        issues.AddRange(await owner.CheckWoundAdmissionInputsCoreAsync(lease, admission));
                        if (issues.Count != 0)
                            return new(null, issues);
                        var retryFingerprint = WoundAcceptedTurnFingerprintWriter.Compute(new string?[]
                        {
                            "book_of_eternity.spiritual_wound_selection", "1", decision.GetRawText(), finalScene
                        });
                        if (retryFingerprint != appliedSelection._decisionFingerprint)
                            return new(null, new[] { SourceIssue(AfterlifeSpiritualConflictState.StatePath,
                                "spiritual_wound_selection_conflict", "the exact already applied wound decision") });
                        return appliedResult;
                    }
                    var selection = await SelectCoreAsync(owner, lease, admission, decision, finalScene, issues);
                    if (issues.Count != 0)
                        return new(null, issues);
                    if (selection is null)
                        return owner._woundDeclines.TryGetValue(admission, out var declined)
                            ? declined.Result : new(null, issues);
                    if (owner._materializedWounds.TryGetValue(selection, out var retained))
                        return retained;
                    if (selection.Input.Transitions.Any(transition => transition.Kind == "create") &&
                        owner._registeredWoundInsertions.Any(pair => pair.Value.IsCreation &&
                            pair.Key.Admission.Source.ConflictId == admission.Source.ConflictId &&
                            pair.Key.Admission.Source.AffectedSide == admission.Source.AffectedSide))
                    {
                        issues.Add(SourceIssue(AfterlifeSpiritualConflictState.StatePath,
                            "spiritual_wound_duplicate_conflict_side_wound",
                            "at most one newly created wound for each side of a conflict"));
                        return new(null, issues);
                    }
                    if (owner._effects is null || owner._resources is null || owner._effects.HasPendingWoundIntegration)
                    {
                        issues.Add(SourceIssue(AfterlifeSpiritualConflictState.StatePath,
                            "spiritual_wound_routing_required", "an owned draft ready for a new insertion"));
                        return new(null, issues);
                    }
                    using var allocationScope = owner._allocations.Clock.BeginSpeculation();
                    attemptedDraftMutation = true;
                    var before = owner._effects.AdvanceForWoundInsertion(selection, issues);
                    if (before is null || issues.Count != 0)
                        return Failed();
                    var prepared = before.Prepare();
                    issues.AddRange(prepared.Issues);
                    if (!prepared.Success || issues.Count != 0)
                        return Failed();
                    var insertion = before.Apply(issues);
                    if (insertion is null || issues.Count != 0)
                        return Failed();
                    var registration = owner._resources.RegisterWoundInstances(insertion, issues);
                    if (registration is null || issues.Count != 0)
                        return Failed();
                    owner._effects.AcceptInstalledWoundRouting(insertion, owner._resources);
                    var result = new OriginalWoundMaterializationResult(insertion.Wound, issues);
                    owner._registeredWoundInsertions.Add(selection, insertion);
                    owner._materializedWounds.Add(selection, result);
                    allocationScope.Commit();
                    return result;

                    OriginalWoundMaterializationResult Failed()
                    {
                        owner.RevokeUnderLease(lease);
                        return new(null, issues);
                    }
                }
                catch
                {
                    if (attemptedDraftMutation)
                        owner.RevokeUnderLease(lease);
                    throw;
                }
            }
        }
    }
}
