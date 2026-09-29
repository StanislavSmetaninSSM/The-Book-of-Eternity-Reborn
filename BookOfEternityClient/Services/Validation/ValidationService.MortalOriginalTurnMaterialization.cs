using BookOfEternityClient.Core;

namespace BookOfEternityClient.Services;

public partial class ValidationService
{
    internal sealed partial class MortalOriginalTurnCapture
    {
        private AcceptedMechanicsPlanner.ResourceExecutionSession? _resources;
        private WoundSelection? _woundSelection;
        private EffectAcceptedTurnPlanningResult? _completedEffects;
        private readonly Dictionary<string, (WoundSelection Selection, OriginalWoundMaterializationResult Result)>
            _materializedWounds = new(StringComparer.Ordinal);

        /// <summary>
        /// Advances original fixed resource commands to their next actual closed boundary after fresh input validation.
        /// </summary>
        /// <param name="lease">
        /// Active canonical write lease protecting the original input checks and owned continuation.
        /// </param>
        /// <returns>
        /// Actual resource checkpoint or terminal result, or diagnostics preventing continuation.
        /// </returns>
        internal async Task<AcceptedMechanicsPlanner.ResourceContinuationResult> AdvanceNextResourceBoundaryAsync(
            FileSystemManager.CanonicalWriteLease lease)
        {
            await _gate.WaitAsync();
            var attempted = false;
            try
            {
                var issues = await CheckRetainedInputsAsync(lease);
                if (issues.Count != 0)
                    return new(null, issues);
                if (_effects.HasPendingWoundIntegration)
                    return new(null, new[] { WoundIssue(AcceptedMechanicsPlan.WoundCommandPath,
                        "mortal_wound_routing_required", "the inserted wound registered in its resource owner", "pending") });
                attempted = true;
                if (_resources == null)
                {
                    var created = AcceptedMechanicsPlanner.BeginOriginalMortalResourceExecution(_input);
                    if (created.Session == null)
                    {
                        RevokeUnderLease(lease);
                        return new(null, created.Issues);
                    }
                    _resources = created.Session;
                }
                var step = _resources.AdvanceToClosedBoundary();
                if (step.Result is { IsValid: false } failed)
                {
                    RevokeUnderLease(lease);
                    return new(null, failed.Issues);
                }
                return new(step, Array.Empty<ValidationIssue>());
            }
            catch
            {
                if (attempted)
                    RevokeUnderLease(lease);
                throw;
            }
            finally { _gate.Release(); }
        }

        /// <summary>
        /// Inserts the captured signed physical wound at the exact current ordinary resource checkpoint.
        /// </summary>
        /// <param name="lease">
        /// Active canonical lease used to authenticate retained original inputs again.
        /// </param>
        /// <param name="checkpoint">
        /// Exact checkpoint issued by this capture's retained executor; copies and stale checkpoints are rejected.
        /// </param>
        /// <returns>
        /// Detached inserted wound or validation diagnostics, without common publication authority.
        /// </returns>
        internal Task<OriginalWoundMaterializationResult> MaterializeWoundAsync(
            FileSystemManager.CanonicalWriteLease lease,
            AcceptedMechanicsPlanner.ResourceClosedBoundaryCheckpoint checkpoint) =>
            WoundSelection.MaterializeAsync(this, lease, checkpoint, null);

        /// <summary>
        /// Applies one explicitly selected original physical decision at the current owned resource checkpoint.
        /// </summary>
        /// <param name="lease">
        /// Active canonical lease protecting fresh original input validation.
        /// </param>
        /// <param name="checkpoint">
        /// Exact current boundary of this capture's executor.
        /// </param>
        /// <param name="opportunityRef">
        /// Exact public reference of a retained signed decision; unknown references are rejected.
        /// </param>
        /// <returns>
        /// Detached insertion result or diagnostics; no common publication authority is granted.
        /// </returns>
        internal Task<OriginalWoundMaterializationResult> MaterializeWoundAsync(
            FileSystemManager.CanonicalWriteLease lease,
            AcceptedMechanicsPlanner.ResourceClosedBoundaryCheckpoint checkpoint, string opportunityRef) =>
            WoundSelection.MaterializeAsync(this, lease, checkpoint, opportunityRef);

        /// <summary>
        /// Drains the retained ordinary resource owner and completes its shared effect draft without publishing canonical files.
        /// </summary>
        /// <param name="lease">
        /// Active canonical lease used to reauthenticate every original input before completion or retry.
        /// </param>
        /// <returns>
        /// The exact retained completed effect result, or diagnostics after revoking a failed mutation attempt.
        /// </returns>
        internal async Task<EffectAcceptedTurnPlanningResult> CompleteEffectsAsync(
            FileSystemManager.CanonicalWriteLease lease)
        {
            await _gate.WaitAsync();
            var attempted = false;
            try
            {
                var issues = await CheckRetainedInputsAsync(lease);
                if (issues.Count != 0)
                    return new(null, issues);
                if (_completedEffects != null)
                    return _completedEffects;
                if (_effects.HasPendingWoundIntegration)
                    return new(null, new[] { WoundIssue(AcceptedMechanicsPlan.WoundCommandPath,
                        "mortal_wound_routing_required", "every prepared wound installed in its resource owner", "pending") });
                if (_materializedWounds.Count != _originalCommands.Commands.Count)
                    return new(null, new[] { WoundIssue(AcceptedMechanicsPlan.WoundCommandPath,
                        "mortal_wound_materialization_required", "every retained original wound decision materialized", "incomplete") });
                attempted = true;
                if (_resources == null)
                {
                    var created = AcceptedMechanicsPlanner.BeginOriginalMortalResourceExecution(_input);
                    if (created.Session == null)
                    {
                        RevokeUnderLease(lease);
                        return new(null, created.Issues);
                    }
                    _resources = created.Session;
                }
                var resources = _resources.Result ?? _resources.Drain();
                if (!resources.IsValid)
                {
                    RevokeUnderLease(lease);
                    return new(null, resources.Issues);
                }
                var routing = _resources.Routing;
                if (routing == null)
                {
                    RevokeUnderLease(lease);
                    return new(null, new[] { WoundIssue(AcceptedMechanicsPlan.WoundCommandPath,
                        "mortal_wound_routing_required", "the retained original resource routing owner", "missing") });
                }
                var completed = _effects.CompleteWithWoundRouting(resources.EffectBoundaryTranscript!, routing);
                if (!completed.Success)
                {
                    RevokeUnderLease(lease);
                    return completed;
                }
                _completedEffects = completed;
                return completed;
            }
            catch
            {
                if (attempted)
                    RevokeUnderLease(lease);
                throw;
            }
            finally { _gate.Release(); }
        }

        /// <summary>
        /// Revokes this unpublished capture and only its own retained original effect handoff.
        /// </summary>
        /// <param name="lease">
        /// Active canonical lease protecting effect-cache invalidation.
        /// </param>
        private void RevokeUnderLease(FileSystemManager.CanonicalWriteLease lease)
        {
            if (EffectAcceptedTurnPlanAuthority.TryPeekValidated(_validator._fs, lease, out var retained) &&
                ReferenceEquals(retained.Plan, _input.PlanningContext!.EffectPlan))
                EffectAcceptedTurnPlanAuthority.InvalidateValidated(_validator._fs, lease);
            Dispose();
        }

        /// <summary>
        /// Retains a real signed Mortal selection at an owned ordinary checkpoint; only its capture can register it.
        /// </summary>
        internal sealed partial class WoundSelection
        {
            private readonly MortalOriginalTurnCapture _owner;
            private readonly WoundAcceptedTurnInput _input;
            private readonly EffectApplicationDiagnosticLocations? _locations;

            /// <summary>
            /// Binds the capture's already validated selection to its actual resource checkpoint.
            /// </summary>
            /// <param name="owner">
            /// Original validator capture authenticating the signed selection.
            /// </param>
            /// <param name="checkpoint">
            /// Exact current checkpoint of that capture's executor.
            /// </param>
            /// <param name="input">
            /// One recomposed decision preserving original baselines and binding its target to the owned current view.
            /// </param>
            /// <param name="locations">
            /// Validated diagnostic coordinates for this single decision, or null when absent.
            /// </param>
            private WoundSelection(MortalOriginalTurnCapture owner,
                AcceptedMechanicsPlanner.ResourceClosedBoundaryCheckpoint checkpoint,
                WoundAcceptedTurnInput input, EffectApplicationDiagnosticLocations? locations)
            {
                _owner = owner;
                Checkpoint = checkpoint;
                _input = WoundAcceptedTurnData.CloneInput(input)!;
                _locations = locations;
            }

            /// <summary>
            /// Gets the concrete ordinary boundary selected for this insertion.
            /// </summary>
            internal AcceptedMechanicsPlanner.ResourceClosedBoundaryCheckpoint Checkpoint { get; }
            /// <summary>
            /// Gets detached validated input with original signed baselines and the selected owned current target.
            /// </summary>
            internal WoundAcceptedTurnInput Input => WoundAcceptedTurnData.CloneInput(_input)!;
            /// <summary>
            /// Gets validated proposal diagnostics, or null when no component coordinates were supplied.
            /// </summary>
            internal EffectApplicationDiagnosticLocations? Locations => _locations;
            /// <summary>
            /// Gets the actual resource routing owner whose current epoch issued <see cref="Checkpoint"/>.
            /// </summary>
            internal EffectAcceptedTurnPlanner.BaseResourceRouting? Routing => _owner._resources?.Routing;

            /// <summary>
            /// Checks exact capture, selection, draft and current checkpoint ownership.
            /// </summary>
            /// <param name="draft">
            /// Draft requesting this selection's insertion authority.
            /// </param>
            /// <returns>
            /// True only for this capture's registered selection at its current checkpoint; otherwise false.
            /// </returns>
            internal bool IsCurrentFor(EffectAcceptedTurnPlanner.EffectAcceptedDraft draft) =>
                _owner.IsCurrentOwner && ReferenceEquals(_owner._effects, draft) &&
                ReferenceEquals(_owner._woundSelection, this) &&
                _owner._resources?.OwnsCurrentCheckpoint(Checkpoint) == true;

            /// <summary>
            /// Reads original wound baselines together with the exact accepted effect base needed for the first local cut.
            /// </summary>
            /// <param name="draft">
            /// Draft whose exact current selection must still agree.
            /// </param>
            /// <param name="issues">
            /// Receives an ownership diagnostic if this selection has become stale.
            /// </param>
            /// <returns>
            /// Detached original wound and accepted effect images, or null for stale ownership.
            /// </returns>
            internal WoundOperationBeforeData? ReadInitialState(EffectAcceptedTurnPlanner.EffectAcceptedDraft draft,
                List<ValidationIssue> issues)
            {
                if (!IsCurrentFor(draft))
                {
                    issues.Add(WoundIssue(AcceptedMechanicsPlan.WoundCommandPath,
                        "mortal_wound_selection_stale", "the exact current registered selection", "stale"));
                    return null;
                }
                var input = Input;
                var plan = _owner._input.PlanningContext!.EffectPlan!;
                return new(input.PreTurnCarriers, input.PreTurnIdentityIndex, input.PreTurnHistory,
                    plan.ResourceTriggerCarriers, plan.IdentityIndexAfterImage);
            }

            /// <summary>
            /// Authenticates and applies one retained selection through the shared draft and resource owner transaction.
            /// </summary>
            /// <param name="owner">
            /// Original capture retaining signed inputs and both mutable executors.
            /// </param>
            /// <param name="lease">
            /// Active canonical lease used before any retry lookup or mutation.
            /// </param>
            /// <param name="checkpoint">
            /// Actual current resource checkpoint selected by the caller.
            /// </param>
            /// <param name="opportunityRef">
            /// Exact original decision reference; null is accepted only for a single-row capture.
            /// </param>
            /// <returns>
            /// The exact retained retry result or diagnostics; mutation failures revoke the entire capture.
            /// </returns>
            internal static async Task<OriginalWoundMaterializationResult> MaterializeAsync(
                MortalOriginalTurnCapture owner, FileSystemManager.CanonicalWriteLease lease,
                AcceptedMechanicsPlanner.ResourceClosedBoundaryCheckpoint checkpoint, string? opportunityRef)
            {
                ArgumentNullException.ThrowIfNull(checkpoint);
                await owner._gate.WaitAsync();
                var attempted = false;
                try
                {
                    var issues = (await owner.CheckRetainedInputsAsync(lease)).ToList();
                    if (issues.Count != 0)
                        return new(null, issues);
                    if (owner._resources?.OwnsCurrentCheckpoint(checkpoint) != true)
                        return new(null, new[] { WoundIssue(AcceptedMechanicsPlan.WoundCommandPath,
                            "mortal_wound_checkpoint_mismatch", "the exact current insertion checkpoint", "foreign or stale") });
                    var rows = owner._originalCommands.Commands;
                    if (opportunityRef == null && rows.Count != 1)
                        return new(null, new[] { WoundIssue(AcceptedMechanicsPlan.WoundCommandPath,
                            "mortal_wound_selection_required", "one explicit retained occurrence selection", "ambiguous batch") });
                    opportunityRef ??= rows[0].Opportunity.PublicRef;
                    var matches = rows.Where(value => value.Opportunity.PublicRef == opportunityRef).ToArray();
                    if (matches.Length != 1)
                        return new(null, new[] { WoundIssue(AcceptedMechanicsPlan.WoundCommandPath,
                            "mortal_wound_selection_unknown", "one exact retained signed decision", opportunityRef) });
                    if (owner._materializedWounds.TryGetValue(opportunityRef, out var retained))
                        return ReferenceEquals(retained.Selection.Checkpoint, checkpoint) ? retained.Result :
                            new(null, new[] { WoundIssue(AcceptedMechanicsPlan.WoundCommandPath,
                                "mortal_wound_selection_already_applied", "an unused original occurrence", opportunityRef) });
                    var selection = SelectCurrent(owner, checkpoint, matches[0], issues);
                    if (selection == null || issues.Count != 0)
                        return new(null, issues);
                    owner._woundSelection = selection;
                    attempted = true;
                    var before = owner._effects.AdvanceForWoundInsertion(owner._woundSelection, issues);
                    if (before == null || issues.Count != 0)
                        return Failed();
                    var prepared = before.Prepare();
                    issues.AddRange(prepared.Issues);
                    if (!prepared.Success || issues.Count != 0)
                        return Failed();
                    var insertion = before.Apply(issues);
                    if (insertion == null || issues.Count != 0)
                        return Failed();
                    if (owner._resources.RegisterWoundInstances(insertion, issues) == null || issues.Count != 0)
                        return Failed();
                    owner._effects.AcceptInstalledWoundRouting(insertion, owner._resources);
                    var result = new OriginalWoundMaterializationResult(insertion.Wound, issues);
                    owner._materializedWounds.Add(opportunityRef, (selection, result));
                    return result;

                    OriginalWoundMaterializationResult Failed()
                    {
                        owner.RevokeUnderLease(lease);
                        return new(null, issues);
                    }
                }
                catch
                {
                    if (attempted)
                        owner.RevokeUnderLease(lease);
                    throw;
                }
                finally { owner._gate.Release(); }
            }
        }
    }
}
