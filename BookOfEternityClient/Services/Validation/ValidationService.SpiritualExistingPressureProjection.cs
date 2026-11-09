using System.Text;
using System.Text.Json.Nodes;
using BookOfEternityClient.Core;
using PressureGroup = BookOfEternityClient.Services.AcceptedMechanicsPlanner.ResourceExecutionSession.SpiritualMechanicsContext.ExistingPressureComparisonGroup;

namespace BookOfEternityClient.Services;

public partial class ValidationService
{
    internal sealed partial class SpiritualWoundSourceSession
    {
        // Only completed detached DATA crosses an admitted ordinal. No lease or context is retained there.
        private sealed class PressureProjectionData
        {
            private readonly JsonObject _ordinaryRoot;
            private readonly JsonObject _actualRoot;
            private readonly JsonObject _actualExchange;
            private readonly int _actualRank;
            private readonly bool _allowsZeroMaximum;
            private readonly ValidatedSpiritualWoundFailure? _guardianFailure;
            internal string AffectedSide { get; }
            internal int Ordinal { get; }
            internal string ConflictId { get; }
            internal string ExchangeId { get; }
            internal string RawFingerprint { get; }
            internal string PreimageJson { get; }
            internal SpiritualWoundCalculation OrdinaryCalculation { get; }
            internal IReadOnlyList<AfterlifeSpiritualConflictResourceOutcome.ExpectedTransition> ExpectedTransitions { get; }

            internal PressureProjectionData(OrdinaryPressureEvaluationInput input,
                SpiritualOrdinaryPressureComparisonData comparison,
                PressureGroup.ActualBasis isolation)
            {
                _ordinaryRoot = input.FrozenRaw.DeepClone().AsObject();
                var active = _ordinaryRoot["activeConflict"] as JsonObject;
                var log = active?["exchangeLog"] as JsonArray;
                var ordinary = comparison.ReadOrdinaryExchange();
                AffectedSide = isolation.AffectedSide;
                var strainField = AffectedSide + "SideStrain";
                if (active == null || log == null || log.Count == 0 || log[^1] is not JsonObject last ||
                    !JsonNode.DeepEquals(last, ordinary) ||
                    !JsonNode.DeepEquals(active[strainField], ordinary["after"]?[strainField]) ||
                    comparison.ExchangePointer != "/activeConflict/exchangeLog/" + (log.Count - 1) ||
                    comparison.ExchangePointer != input.Pointer || comparison.RawInputFingerprint != input.RawFingerprint)
                    throw new InvalidOperationException("The owned ordinary full root must end at its exact comparison exchange.");
                var singleton = isolation as PressureGroup.SingletonActualBasis;
                var multiParent = isolation as PressureGroup.MultiParentActualBasis;
                var guardian = isolation as PressureGroup.GuardianActualBasis;
                _allowsZeroMaximum = singleton is not null || multiParent is not null || guardian is not null;
                if (singleton is null && multiParent is null && guardian is null)
                    throw new InvalidOperationException("The pressure projection requires an owned singleton or multi-parent basis.");
                var ordinaryRank = comparison.OrdinaryCalculation.NewStrainRank;
                var rank = multiParent is not null ? (int)Math.Min(4L, (long)ordinaryRank + multiParent.Parents.Count) :
                    Math.Min(4, ordinaryRank + 1);
                if (comparison.OrdinaryCalculation.PreviousStrainRank >= ordinaryRank ||
                    singleton is not null && ordinaryRank is (< 1 or > 4) || multiParent is not null &&
                    (multiParent.Parents.Count < 2 || input.Ordinal != 0 || ordinaryRank is < 1 or > 4))
                    throw new InvalidOperationException("The pressure projection requires its exact increasing singleton or capped multi-parent contour.");
                if (guardian is not null)
                {
                    _guardianFailure = SpiritualWoundFailedAction.ReadValidated(active!, ordinary, input.Ordinal);
                    if (_guardianFailure.ActorId != guardian.ActorId || ordinaryRank is not (2 or 3 or 4) || rank is not (3 or 4) ||
                        comparison.OrdinaryCalculation.PreviousStrainRank != 1 ||
                        comparison.OrdinaryCalculation.MaximumSeverityRank < 0)
                        throw new InvalidOperationException("Actual guardian pressure requires increasing I-to-ordinary-II/III/IV-to-capped-actual-III/IV comparison DATA with nonnegative maximum.");
                }
                _actualRank = rank;
                var strain = new[] { "clear", "strained", "fractured", "overwhelmed", "broken" }[rank];
                _actualRoot = _ordinaryRoot.DeepClone().AsObject();
                _actualExchange = ordinary.DeepClone().AsObject();
                _actualExchange["after"]![strainField] = strain;
                _actualRoot["activeConflict"]!["exchangeLog"]![log.Count - 1] = _actualExchange.DeepClone();
                _actualRoot["activeConflict"]![strainField] = strain;
                Ordinal = input.Ordinal;
                ConflictId = active["conflictId"]!.GetValue<string>();
                ExchangeId = ordinary["exchangeId"]!.GetValue<string>();
                RawFingerprint = input.RawFingerprint;
                OrdinaryCalculation = comparison.OrdinaryCalculation;
                ExpectedTransitions = Array.AsReadOnly(comparison.ExpectedTransitions.ToArray());
                var preimage = new JsonObject
                {
                    ["version"] = guardian is not null ? 5 : singleton is not null ? 3 : multiParent!.Parents.Count == 2 ? 4 : 6,
                    ["inputRoleRevision"] = guardian is not null ? SpiritualWoundFailedAction.Role :
                        singleton is not null ? "existing_pressure_ordinary_v1" : multiParent!.Parents.Count == 2
                            ? "existing_pressure_ordinary_pair_v1" : "existing_pressure_ordinary_group_v1",
                    ["ordinaryExchangeJsonBase64"] = Convert.ToBase64String(Encoding.UTF8.GetBytes(SpiritualWoundStateJson.Canonical(ordinary))),
                    ["originalInput"] = new JsonObject { ["path"] = AfterlifeSpiritualConflictState.StatePath,
                        ["existed"] = true, ["contentFingerprint"] = RawFingerprint, ["exchangePointer"] = input.Pointer }
                };
                if (guardian is not null)
                {
                    preimage["parentWoundJsonBase64"] = Convert.ToBase64String(Encoding.UTF8.GetBytes(guardian.ParentJson));
                    preimage["effectId"] = guardian.EffectId;
                    preimage["componentId"] = guardian.ComponentId;
                }
                else if (singleton is not null)
                {
                    preimage["parentWoundJsonBase64"] = Convert.ToBase64String(Encoding.UTF8.GetBytes(singleton.ParentJson));
                    preimage["effectId"] = singleton.EffectId;
                    preimage["componentId"] = singleton.ComponentId;
                }
                else
                {
                    var parents = new JsonArray();
                    foreach (var parent in multiParent!.Parents)
                        parents.Add(new JsonObject
                        {
                            ["parentWoundJsonBase64"] = Convert.ToBase64String(Encoding.UTF8.GetBytes(parent.ParentJson)),
                            ["effectId"] = parent.EffectId, ["componentId"] = parent.ComponentId
                        });
                    preimage["parents"] = parents;
                }
                preimage["ordinaryCalculation"] = new JsonObject { ["traumaPressure"] = OrdinaryCalculation.TraumaPressure,
                    ["maximumSeverityRank"] = OrdinaryCalculation.MaximumSeverityRank };
                PreimageJson = preimage.ToJsonString();
            }

            internal JsonObject ApplyToOrdinaryRoot(JsonObject raw)
            {
                if (!JsonNode.DeepEquals(_ordinaryRoot, raw))
                    throw new InvalidOperationException("The actual pressure view requires the exact unchanged ordinary full root.");
                return _actualRoot.DeepClone().AsObject();
            }

            internal bool MatchesActual(JsonObject exchange) => JsonNode.DeepEquals(_actualExchange, exchange);
            internal bool MatchesActualSource(PreparedSpiritualSource source) =>
                source.ConflictId == ConflictId && source.ExchangeId == ExchangeId && source.AffectedSide == AffectedSide &&
                (_guardianFailure is null || source.AffectedActor == "guardian:" + _guardianFailure.ActorId && source.ActingActor == "player_soul:player_soul") &&
                source.Operation == "pressure" && source.OriginalSpecialArtJson == null && source.GuaranteedSeverityRank == null &&
                source.RetraumaWoundId == null && source.Calculation.HasIncreasingStrain &&
                (source.Calculation.MaximumSeverityRank > 0 || _allowsZeroMaximum && source.Calculation.MaximumSeverityRank == 0) &&
                source.Calculation.NewStrainRank == _actualRank &&
                (source.Calculation.Input with { NewStrain = OrdinaryCalculation.Input.NewStrain }) == OrdinaryCalculation.Input;
        }

        private sealed class LivePressureProjection
        {
            internal readonly FileSystemManager.CanonicalWriteLease Lease;
            internal readonly long SourceRevision;
            internal readonly AcceptedMechanicsPlanner.ResourceExecutionSession.SpiritualMechanicsContext Context;
            internal readonly PressureGroup.ActualBasis Isolation;
            internal readonly OperationStructuralSnapshot Dependencies;
            internal readonly PressureProjectionData Data;
            internal bool Prepared;

            internal LivePressureProjection(FileSystemManager.CanonicalWriteLease lease, long revision,
                AcceptedMechanicsPlanner.ResourceExecutionSession.SpiritualMechanicsContext context,
                PressureGroup.ActualBasis isolation,
                OperationStructuralSnapshot dependencies, PressureProjectionData data)
            {
                Lease = lease; SourceRevision = revision; Context = context; Isolation = isolation;
                Dependencies = dependencies; Data = data;
            }

            internal bool IsCurrent(SpiritualWoundSourceSession owner, SpiritualOriginalTurnCapture capture,
                FileSystemManager.CanonicalWriteLease lease)
            {
                owner._ordinaryFreshDependencyChecks++;
                return ReferenceEquals(owner._pendingPressureProjection, this) && ReferenceEquals(Lease, lease) &&
                    owner._continuationRevision == SourceRevision && Context.OwnsExistingPressureOrdinary(capture, owner) &&
                    Context.ReadOrdinaryPressureDependencySnapshot() is { } current && Dependencies.Matches(current);
            }
        }

        private LivePressureProjection? _pendingPressureProjection;
        private LivePressureProjection? _livePressureProjection;
        private readonly List<PressureProjectionData> _admittedPressureProjections = [];
        internal bool HasAdmittedPressureProjection => _admittedPressureProjections.Count != 0;

        /// <summary>Discards only tentative live projection permission; admitted immutable source DATA remains separate.</summary>
        internal void DiscardPendingPressureProjection() => _pendingPressureProjection = null;

        /// <summary>Prepares the bounded actual view from this owner's own successful ordinary proof before actual allocation.</summary>
        internal async Task<IReadOnlyList<ValidationIssue>> PrepareExistingPressureProjectionAsync(
            FileSystemManager.CanonicalWriteLease lease, SpiritualOriginalTurnCapture capture)
        {
            _validator._fs.EnsureCanonicalWriteLeaseActive(lease);
            await _continuationGate.WaitAsync();
            try
            {
                _pendingPressureProjection = null;
                if (!IsCurrentOwner || !ReferenceEquals(_prefixCapture, capture) ||
                    !capture.TryReadNextSourceFrontier(this, out var limit))
                    return OrdinaryPressureFailure("spiritual_source_frontier_owner_mismatch", "the exact current pressure preparation owners");
                // Other operations and already exhausted input use their unchanged normal source route.
                if (_initialFullCandidateConflict["activeConflict"]?["exchangeLog"] is not JsonArray log ||
                    _originalConflict["activeConflict"]?["exchangeLog"] is not JsonArray history ||
                    history.Count + limit - 1 >= log.Count || log[history.Count + limit - 1] is not JsonObject next ||
                    !ConflictTokenEquals(ExactString(next["operationType"]), "pressure") ||
                    !(ConflictTokenEquals(ExactString(next["outcome"]), "setback") ||
                      ConflictTokenEquals(ExactString(next["outcome"]), "success") && next.ContainsKey(SpiritualWoundFailedAction.Property)))
                    return [];
                var issues = new List<ValidationIssue>();
                if (!PrepareExistingPressureOrdinaryContextUnderGate(lease, capture, issues))
                    return issues.Count != 0 ? issues : OrdinaryPressureFailure("spiritual_wound_mechanics_stale", "current exact source/resource mechanics");
                var currentContext = _ordinaryContext!;
                var guardianLane = next.ContainsKey(SpiritualWoundFailedAction.Property);
                var declaredGuardian = guardianLane ? SpiritualWoundFailedAction.ReadGuardianDeclaration(next) : null;
                if (!currentContext.Contributions.Any(row => row.Profile == "spiritual_strain_burden" && row.Operation == "pressure" &&
                    (guardianLane ? row.ResolvedSide == "opposition" && row.Actor.Kind == "guardian" && row.Actor.TargetId == declaredGuardian :
                        row.ResolvedSide == "player" && row.Actor.Kind == "player" && row.Actor.TargetId == "player_soul")))
                    return guardianLane ? OrdinaryPressureFailure("spiritual_pressure_ordinary_contour_unsupported", "one applicable signed guardian strain parent") : [];
                // Existing same-turn C2 corrections retain their established authoring and freshness policy.
                // A same-turn source group is never evidence of a signed-existing parent.
                if (_originalConflict["activeConflict"] is not JsonObject signedConflict ||
                    !guardianLane && (currentContext.ReadOwnedStrainCorrection(signedConflict, next) != null ||
                    currentContext.HasSameTurnPressureStrainGroup()))
                    return [];
                var fresh = await capture.CheckExistingPressureOrdinaryInputsAsync(lease);
                if (fresh.Count != 0) return fresh;
                if (capture.HasCombatHealingOrigin || _admittedPressureProjections.Count != 0)
                    return OrdinaryPressureFailure("spiritual_pressure_ordinary_contour_unsupported", "one existing pressure projection without mixed healing or a prior pressure mapping");
                var (comparison, actualBasis) = await ReadOwnedPressureOrdinaryForProjectionUnderGateAsync(lease, capture);
                if (_ordinaryContext == null || _ordinaryEvaluationInput == null ||
                    !ReferenceEquals(actualBasis.Context, _ordinaryContext) ||
                    _ordinaryContext.ReadOrdinaryPressureDependencySnapshot() is not { } dependencies)
                    return OrdinaryPressureFailure("spiritual_wound_mechanics_stale", "the same successful ordinary comparison owners");
                if (_ordinaryContext.HasPressureConflictCarrierCollision())
                    return OrdinaryPressureFailure("spiritual_pressure_effect_carrier_collision", "no original conflict effect-carrier participation in the bounded pressure projection");
                var data = new PressureProjectionData(_ordinaryEvaluationInput, comparison, actualBasis);
                _pendingPressureProjection = new(lease, _continuationRevision, _ordinaryContext,
                    actualBasis, dependencies, data);
                return [];
            }
            catch (OrdinaryPressureRefusal refused)
            {
                _pendingPressureProjection = null;
                InvalidateExistingPressureOrdinary();
                return refused.Issues;
            }
            catch (Exception error) when (error is not ObjectDisposedException &&
                error is InvalidOperationException or ArgumentException or FormatException or System.Text.Json.JsonException or OverflowException)
            {
                _pendingPressureProjection = null;
                InvalidateExistingPressureOrdinary();
                return OrdinaryPressureFailure("spiritual_pressure_ordinary_contour_unsupported", error.Message);
            }
            catch
            {
                _pendingPressureProjection = null;
                InvalidateExistingPressureOrdinary();
                throw;
            }
            finally
            {
                if (_pendingPressureProjection == null) InvalidateExistingPressureOrdinary();
                _continuationGate.Release();
            }
        }

        /// <summary>Prepares the complete group once, then chooses only its supported actual issuer before any ordinary core reuse.</summary>
        private async Task<(SpiritualOrdinaryPressureComparisonData Comparison, PressureGroup.ActualBasis Basis)>
            ReadOwnedPressureOrdinaryForProjectionUnderGateAsync(
                FileSystemManager.CanonicalWriteLease lease, SpiritualOriginalTurnCapture capture)
        {
            await PrepareOwnedExistingPressureOrdinaryUnderGateAsync(lease, capture);
            var group = _ordinaryGroup!;
            if (group.IsGuardian)
            {
                var basis = group.CaptureGuardianActualBasis();
                var comparison = await ComputeOwnedExistingPressureOrdinaryUnderGateAsync(lease, capture);
                var actualRank = Math.Min(4, comparison.OrdinaryCalculation.NewStrainRank + 1);
                var actual = SpiritualWoundOpportunityMath.Calculate(comparison.OrdinaryCalculation.Input with { NewStrain = actualRank == 3 ? "overwhelmed" : "broken" });
                if (actual is not { HasIncreasingStrain: true, MaximumSeverityRank: >= 0 })
                    throw new InvalidOperationException("Actual guardian pressure requires an increasing actual calculation with nonnegative maximum.");
                return (comparison, basis);
            }
            if (group.ParentCount == 1)
            {
                var result = await ReadOwnedSingletonPressureOrdinaryForProjectionUnderGateAsync(lease, capture, group);
                return (result.Comparison, result.Basis);
            }
            if (group.ParentCount >= 2)
            {
                var result = await ReadOwnedMultiParentPressureOrdinaryForProjectionUnderGateAsync(lease, capture, group);
                return (result.Comparison, result.Basis);
            }
            throw new InvalidOperationException("actual existing pressure requires a complete nonempty comparison group");
        }

        /// <summary>Mints unchanged singleton eligibility from the just-prepared group before reading shared ordinary DATA.</summary>
        private async Task<(SpiritualOrdinaryPressureComparisonData Comparison,
            PressureGroup.SingletonActualBasis Basis)>
            ReadOwnedSingletonPressureOrdinaryForProjectionUnderGateAsync(
                FileSystemManager.CanonicalWriteLease lease, SpiritualOriginalTurnCapture capture, PressureGroup group)
        {
            var basis = group.CaptureSingletonActualBasis();
            var comparison = await ComputeOwnedExistingPressureOrdinaryUnderGateAsync(lease, capture);
            var ordinaryRank = comparison.OrdinaryCalculation.NewStrainRank;
            if (ordinaryRank is < 1 or > 4)
                throw new InvalidOperationException("actual existing pressure requires an increasing ordinary result at most broken");
            var actualRank = Math.Min(4, ordinaryRank + 1);
            var actualStrain = new[] { "clear", "strained", "fractured", "overwhelmed", "broken" }[actualRank];
            var actualCalculation = SpiritualWoundOpportunityMath.Calculate(
                comparison.OrdinaryCalculation.Input with { NewStrain = actualStrain });
            if (actualCalculation is not { HasIncreasingStrain: true, MaximumSeverityRank: >= 0 })
                throw new InvalidOperationException("actual existing singleton pressure requires an increasing hypothetical actual calculation");
            return (comparison, basis);
        }

        /// <summary>Mints fresh ordinal-zero complete multi-parent eligibility, then proves the one ordinary result and its increasing capped actual.</summary>
        private async Task<(SpiritualOrdinaryPressureComparisonData Comparison, PressureGroup.MultiParentActualBasis Basis)>
            ReadOwnedMultiParentPressureOrdinaryForProjectionUnderGateAsync(
                FileSystemManager.CanonicalWriteLease lease, SpiritualOriginalTurnCapture capture, PressureGroup group)
        {
            var basis = group.CaptureMultiParentActualBasis();
            var comparison = await ComputeOwnedExistingPressureOrdinaryUnderGateAsync(lease, capture);
            var actualRank = (int)Math.Min(4L, (long)comparison.OrdinaryCalculation.NewStrainRank + basis.Parents.Count);
            var actualStrain = new[] { "clear", "strained", "fractured", "overwhelmed", "broken" }[actualRank];
            var actualCalculation = SpiritualWoundOpportunityMath.Calculate(
                comparison.OrdinaryCalculation.Input with { NewStrain = actualStrain });
            if (actualCalculation is not { HasIncreasingStrain: true, MaximumSeverityRank: >= 0 })
                throw new InvalidOperationException("actual existing pressure requires an increasing hypothetical actual calculation");
            return (comparison, basis);
        }

        private JsonObject ProjectActualFullCandidateConflict()
        {
            var raw = ProjectFullCandidateConflict();
            if (_admittedPressureProjections.Count > 1)
                throw new InvalidOperationException("Only one bounded existing-pressure projection is supported.");
            foreach (var data in _admittedPressureProjections)
                raw = data.ApplyToOrdinaryRoot(raw);
            if (_livePressureProjection is { } live && !_admittedPressureProjections.Contains(live.Data))
                raw = live.Data.ApplyToOrdinaryRoot(raw);
            return raw;
        }

        /// <summary>Returns an authenticated admitted actual full companion only to the exact latest resource owner.</summary>
        internal JsonObject ReadActualResourceCompanion(AcceptedMechanicsPlanner.ResourceExecutionSession resources,
            AcceptedMechanicsPlanner.SpiritualExchangeInterval interval, JsonObject ordinaryCompanion)
        {
            if (!IsCurrentOwner || _prefixCapture == null || !_prefixCapture.OwnsMechanicsOwner(resources, this) ||
                !resources.Owns(interval) || interval.Ordinal != _checkedExchanges.Count - 1 ||
                !JsonNode.DeepEquals(_initialFullCandidateConflict, ordinaryCompanion))
                throw new InvalidOperationException("The resource companion must match its exact original source and ordinary full root.");
            return _admittedPressureProjections.Count == 0 ? ordinaryCompanion.DeepClone().AsObject() :
                ProjectActualFullCandidateConflict();
        }

        /// <summary>Rechecks the actual private ticket and complete raw/dependency basis immediately before resource staging.</summary>
        internal Task<IReadOnlyList<ValidationIssue>> CheckPressureBeforeStageAsync(
            FileSystemManager.CanonicalWriteLease lease, SpiritualOriginalTurnCapture capture, PreparedContinuation ticket) =>
            PreparedContinuation.CheckPressureBeforeStageAsync(this, lease, capture, ticket);

        internal sealed partial class PreparedContinuation
        {
            /// <summary>Runs pre-stage ownership checks under the existing source gate without exporting projection objects.</summary>
            internal static async Task<IReadOnlyList<ValidationIssue>> CheckPressureBeforeStageAsync(
                SpiritualWoundSourceSession owner, FileSystemManager.CanonicalWriteLease lease,
                SpiritualOriginalTurnCapture capture, PreparedContinuation ticket)
            {
                await owner._continuationGate.WaitAsync();
                try
                {
                    if (owner._pendingPressureProjection == null && ticket._prospective._livePressureProjection == null) return [];
                    var fresh = await capture.CheckExistingPressureOrdinaryInputsAsync(lease);
                    if (fresh.Count != 0) return fresh;
                    var pressure = owner._pendingPressureProjection;
                    if (pressure == null || !ReferenceEquals(ticket._prospective._livePressureProjection, pressure) ||
                        !pressure.Prepared || !pressure.IsCurrent(owner, capture, lease) ||
                        ticket.ValidateCommitContext(owner, lease) != null ||
                        !ticket._prospective._admittedPressureProjections.Contains(pressure.Data))
                        return OrdinaryPressureFailure("spiritual_wound_mechanics_stale", "the exact validated actual pressure ticket before staging");
                    return [];
                }
                finally { owner._continuationGate.Release(); }
            }
        }

    }

    internal sealed partial class SpiritualOriginalTurnCapture
    {
        /// <summary>Rechecks the original ordinary bytes before any admitted pressure view or cached completion is used.</summary>
        private Task<IReadOnlyList<ValidationIssue>> CheckAdmittedPressureInputsAsync(FileSystemManager.CanonicalWriteLease lease) =>
            _source.HasAdmittedPressureProjection ? CheckExistingPressureOrdinaryInputsAsync(lease) : CheckRetainedInputsAsync(lease);

        /// <summary>Checks final conflict co-ownership without rebasing any effect-owned carrier.</summary>
        private IReadOnlyList<ValidationIssue> CheckCompletedPressureEffectCollision(EffectAcceptedTurnPlan plan) =>
            _source.HasAdmittedPressureProjection && plan.HasPressureConflictCarrierCollision()
                ? OrdinaryPressureFailure("spiritual_pressure_effect_carrier_collision", "no completed effect-owned conflict write in the bounded pressure projection") : [];
    }
}
