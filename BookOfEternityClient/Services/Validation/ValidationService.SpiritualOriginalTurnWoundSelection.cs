using System.Text.Json;
using BookOfEternityClient.Core;

namespace BookOfEternityClient.Services;

public partial class ValidationService
{
    internal sealed partial class SpiritualOriginalTurnCapture
    {
        private readonly Dictionary<WoundSourceAdmission, WoundSelection> _woundSelections = new();
        private readonly Dictionary<WoundSourceAdmission, DeclinedWoundDecision> _woundDeclines = new();

        /// <summary>
        /// Retains a validated decline with its signed binding and full composition for later common decision publication.
        /// It consumes the exact opportunity without creating a wound or initializing the effect draft.
        /// </summary>
        /// <param name="Fingerprint">
        /// Exact decision and scene fingerprint used only for retry comparison under the original owner.
        /// </param>
        /// <param name="Binding">
        /// Detached original event binding associated with the declined opportunity.
        /// </param>
        /// <param name="Composition">
        /// Validated immutable composition retaining the decision receipt and reconstructable command image.
        /// </param>
        /// <param name="Result">
        /// Retained empty-wound result returned for exact retries.
        /// </param>
        private sealed record DeclinedWoundDecision(string Fingerprint, WoundAcceptedTurnBinding Binding,
            WoundResponseInputCompositionResult Composition, OriginalWoundMaterializationResult Result);

        /// <summary>
        /// Retains a source-bound, validated wound decision without allocating an identity or advancing effects.
        /// Only the original response owner can create and register this selection.
        /// </summary>
        internal sealed partial class WoundSelection
        {
            private readonly SpiritualOriginalTurnCapture _owner;
            private readonly string _decisionFingerprint;
            private readonly string _proposalRawFingerprint;
            private readonly WoundAcceptedTurnInput _input;

            /// <summary>
            /// Freezes the already validated response input for registration by its original owner.
            /// </summary>
            /// <param name="owner">
            /// Capture that authenticated and validated the response.
            /// </param>
            /// <param name="admission">
            /// Actual source admission retained at the current exchange.
            /// </param>
            /// <param name="decisionFingerprint">
            /// Fingerprint used only to compare retries, never to recreate selection authority.
            /// </param>
            /// <param name="proposalRawFingerprint">
            /// Exact raw proposal fingerprint for joining the saved C2 decision.
            /// </param>
            /// <param name="input">
            /// Validated response and original snapshots copied into the selection.
            /// </param>
            /// <param name="locations">
            /// Immutable validated component diagnostic coordinates, or <see langword="null"/> when none apply.
            /// </param>
            private WoundSelection(SpiritualOriginalTurnCapture owner, WoundSourceAdmission admission,
                string decisionFingerprint, string proposalRawFingerprint,
                WoundAcceptedTurnInput input, EffectApplicationDiagnosticLocations? locations)
            {
                _owner = owner;
                Admission = admission;
                _decisionFingerprint = decisionFingerprint;
                _proposalRawFingerprint = proposalRawFingerprint;
                _input = WoundAcceptedTurnData.CloneInput(input)!;
                Locations = locations;
            }

            /// <summary>
            /// Gets the actual source admission governing this decision.
            /// </summary>
            internal WoundSourceAdmission Admission { get; }
            /// <summary>
            /// Gets immutable proposal component coordinates validated with the source decision.
            /// </summary>
            internal EffectApplicationDiagnosticLocations? Locations { get; }
            /// <summary>
            /// Gets the actual resource routing owner whose current epoch issued this selection's interval.
            /// </summary>
            internal EffectAcceptedTurnPlanner.BaseResourceRouting? Routing => _owner._resources?.Routing;

            /// <summary>
            /// Gets the detached original input; this image alone grants no live preparation authority.
            /// </summary>
            internal WoundAcceptedTurnInput Input => WoundAcceptedTurnData.CloneInput(_input)!;

            /// <summary>
            /// Gets the exact proposal bytes fingerprint read by this owner-bound selection.
            /// </summary>
            internal string ProposalRawFingerprint => _proposalRawFingerprint;

            /// <summary>
            /// Checks that this is the registered selection at the current source frontier of the given draft.
            /// </summary>
            /// <param name="draft">
            /// Actual effect draft that will advance the selection's dependency closure.
            /// </param>
            /// <returns>
            /// <see langword="true"/> for the exact owned selection and current admission; otherwise, <see langword="false"/>.
            /// </returns>
            internal bool IsCurrentFor(EffectAcceptedTurnPlanner.EffectAcceptedDraft draft) =>
                ReferenceEquals(Admission.Draft, draft) && _owner.OwnsWoundAdmission(Admission) &&
                _owner._woundSelections.TryGetValue(Admission, out var retained) && ReferenceEquals(retained, this);

            /// <summary>
            /// Reads the authenticated initial wound seed for the exact current effect draft.
            /// </summary>
            /// <param name="draft">
            /// Draft requesting its initial detached wound state.
            /// </param>
            /// <param name="issues">
            /// Receives stale selection or initial-state validation failures.
            /// </param>
            /// <returns>
            /// Detached initial state, or <see langword="null"/> when the selection or seed is invalid.
            /// </returns>
            internal WoundOperationBeforeData? ReadInitialState(
                EffectAcceptedTurnPlanner.EffectAcceptedDraft draft, List<ValidationIssue> issues)
            {
                if (!IsCurrentFor(draft))
                {
                    issues.Add(SourceIssue(AfterlifeSpiritualConflictState.StatePath,
                        "spiritual_wound_selection_stale", "the exact current source-bound wound selection"));
                    return null;
                }
                return _owner.ReadInitialWoundState(issues);
            }

            /// <summary>
            /// Validates a decision against the actual retained opportunity under the capture gate.
            /// </summary>
            /// <param name="owner">
            /// Current original response owner retaining the source and effect draft.
            /// </param>
            /// <param name="lease">
            /// Active canonical write lease for fresh source and candidate validation.
            /// </param>
            /// <param name="admission">
            /// Actual current admission to which the decision is bound.
            /// </param>
            /// <param name="decision">
            /// One GM-authored decision; its public opportunity reference must match the retained offer.
            /// </param>
            /// <param name="finalScene">
            /// Final scene text checked against the wound notification contract.
            /// </param>
            /// <param name="issues">
            /// Receives source, response, structural or conflicting-retry failures.
            /// </param>
            /// <returns>
            /// A retained materializing selection, or <see langword="null"/> for a decline or validation failure.
            /// </returns>
            internal static async Task<WoundSelection?> SelectAsync(SpiritualOriginalTurnCapture owner,
                FileSystemManager.CanonicalWriteLease lease, WoundSourceAdmission admission,
                JsonElement decision, string? finalScene, List<ValidationIssue> issues)
            {
                await owner._gate.WaitAsync();
                try
                {
                    return await SelectCoreAsync(owner, lease, admission, decision, finalScene, issues);
                }
                finally { owner._gate.Release(); }
            }

            /// <summary>
            /// Validates and retains a selection while the original response owner already holds its capture gate.
            /// </summary>
            /// <param name="owner">
            /// Capture holding its exclusive gate across the larger materialization operation.
            /// </param>
            /// <param name="lease">
            /// Active canonical write lease for fresh input checks.
            /// </param>
            /// <param name="admission">
            /// Exact current source admission.
            /// </param>
            /// <param name="decision">
            /// One response decision to validate against the retained opportunity.
            /// </param>
            /// <param name="finalScene">
            /// Final scene text, including the required wound notification when materializing.
            /// </param>
            /// <param name="issues">
            /// Receives source, response or retry conflicts.
            /// </param>
            /// <returns>
            /// A retained materializing selection, or <see langword="null"/> for decline or failure.
            /// </returns>
            private static async Task<WoundSelection?> SelectCoreAsync(SpiritualOriginalTurnCapture owner,
                FileSystemManager.CanonicalWriteLease lease, WoundSourceAdmission admission,
                JsonElement decision, string? finalScene, List<ValidationIssue> issues)
            {
                var offer = await owner.ReadWoundOpportunityCoreAsync(lease, admission);
                issues.AddRange(offer.Issues);
                if (offer.Satisfaction is not null)
                {
                    issues.Add(SourceIssue(AfterlifeSpiritualConflictState.StatePath,
                        "spiritual_wound_client_owned_satisfaction",
                        "the guarantee is already satisfied and has no GM wound decision"));
                    return null;
                }
                if (issues.Count != 0 || offer.Opportunity == null || offer.Binding == null)
                    return null;
                var fingerprint = WoundAcceptedTurnFingerprintWriter.Compute(new string?[]
                {
                    "book_of_eternity.spiritual_wound_selection", "1", decision.GetRawText(), finalScene
                });
                if (owner._woundDeclines.TryGetValue(admission, out var declined))
                {
                    if (declined.Fingerprint != fingerprint)
                        issues.Add(SourceIssue(AfterlifeSpiritualConflictState.StatePath,
                            "spiritual_wound_selection_conflict", "the exact already retained wound decision"));
                    return null;
                }
                if (owner._woundSelections.TryGetValue(admission, out var retained))
                {
                    if (retained._decisionFingerprint == fingerprint)
                        return retained;
                    issues.Add(SourceIssue(AfterlifeSpiritualConflictState.StatePath,
                        "spiritual_wound_selection_conflict", "the exact already retained wound decision"));
                    return null;
                }
                var composed = WoundResponseInputComposer.Compose(offer.Binding, new[] { offer.Opportunity },
                    new[] { decision }, finalScene, Array.Empty<WoundOpportunityDecisionReceipt>());
                issues.AddRange(composed.Issues);
                if (!composed.Success)
                    return null;
                if (composed.Transitions.Count == 0)
                {
                    owner._woundDeclines.Add(admission, new DeclinedWoundDecision(fingerprint,
                        WoundAcceptedTurnData.CloneBinding(offer.Binding)!, composed,
                        new OriginalWoundMaterializationResult(null, Array.Empty<ValidationIssue>())));
                    return null;
                }
                var initial = owner.ReadInitialWoundState(issues);
                if (initial == null || issues.Count != 0)
                    return null;
                var plan = owner._input.PlanningContext!.EffectPlan!;
                issues.AddRange(WoundAcceptedTurnPlanner.BindAcceptedSourceTargets(
                    offer.Binding, composed.MaterializedOpportunities, plan.TargetAuthority).Issues);
                issues.AddRange(WoundResponseInputComposer.ValidateSkillScopes(offer.Binding,
                    new[] { new WoundResponseCommandDraft(offer.Opportunity, decision, finalScene) },
                    composed.Transitions, plan.SkillScopeAuthority, out var locations));
                if (issues.Count != 0)
                    return null;
                // Original input keeps original snapshots. The live draft supplies its
                // separate current operation-before proof when preparation is requested.
                var original = owner._input.PlanningContext?.WoundStageBundle?.Input ?? owner._input.WoundInput;
                var input = new WoundAcceptedTurnInput(offer.Binding, composed.MaterializedOpportunities,
                    composed.Transitions, original?.PreTurnCarriers ?? initial.WoundCarriers!,
                    original?.PreTurnIdentityIndex ?? initial.WoundIdentity!,
                    original?.PreTurnHistory ?? initial.WoundHistory!,
                    original?.PreTurnEffectCarriers, original?.PreTurnEffectIdentityIndex);
                var operationBefore = offer.Opportunity.WorseningTarget == null ? initial :
                    owner._effects!.ReadCurrentWoundView(owner, owner._resources!, owner._source,
                        owner._effects.WoundReadVersion, issues);
                if (operationBefore == null || issues.Count != 0)
                    return null;
                issues.AddRange(WoundAcceptedTurnPlannerCore.ValidateInput(input, operationBefore).Issues);
                if (issues.Count != 0)
                    return null;
                var proposalRawFingerprint = RawFingerprint(
                    System.Text.Encoding.UTF8.GetBytes(
                        decision.GetProperty("proposal").GetRawText()));
                var selection = new WoundSelection(owner, admission, fingerprint,
                    proposalRawFingerprint, input, locations);
                owner._woundSelections.Add(admission, selection);
                return selection;
            }

        }
    }
}
