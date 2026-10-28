using BookOfEternityClient.Core;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace BookOfEternityClient.Services;

public partial class ValidationService
{
    internal sealed partial class SpiritualOriginalTurnCapture
    {
        private CombatHealingSelection? _combatHealingSelection;

        // A third concrete domain selection for the existing wound draft. Public
        // comparison DATA cannot call this private constructor or register itself.
        internal sealed class CombatHealingSelection
        {
            private readonly SpiritualOriginalTurnCapture _owner;
            private readonly CombatHealingExchangeData _source;
            private readonly WoundAcceptedTurnInput _input;
            private readonly SpiritualWoundStage[] _stages;
            private readonly long _sourceRevision;
            private readonly string _interfaceJson;
            private readonly string _sourceFingerprint;
            private WoundWorkingOwnedGraph? _destinationGraph;
            private EffectAcceptedTurnPlanner.EffectAcceptedDraft.EffectDraftWoundBeforeAuthority? _before;

            private CombatHealingSelection(SpiritualOriginalTurnCapture owner, CombatHealingExchangeData source,
                AcceptedMechanicsPlanner.SpiritualExchangeInterval interval, WoundOperationBeforeData initial)
            {
                _owner = owner; _source = source; Interval = interval; _sourceRevision = owner._source.ContinuationRevision;
                var origin = owner._combatHealingOrigin ?? throw new InvalidOperationException("Actual combat origin required.");
                var accepted = WoundAcceptedEventAuthorityComposer.ComposeDefaultAcceptedTurn(owner._source.SessionId,
                    owner._source.RequestId, owner._source.SnapshotToken, owner._source.TurnNumber);
                if (!accepted.Success || accepted.Events.Single().EventRef != source.EventRef)
                    throw new InvalidOperationException("Combat selection has no exact actual event binding.");
                var binding = new WoundAcceptedTurnBinding(owner._source.SessionId, owner._source.RequestId,
                    owner._source.SnapshotToken, "chaos_sea", owner._source.TurnNumber, accepted.Events,
                    WoundAcceptedEventSetFingerprint.Compute(accepted.Events));
                _input = new(binding, Array.Empty<WoundOpportunityAuthority>(), Array.Empty<WoundAcceptedTransitionDraft>(),
                    initial.WoundCarriers!, initial.WoundIdentity!, initial.WoundHistory!, initial.EffectCarriers, initial.EffectIdentity);
                _stages = SpiritualWoundRecoveryPlanner.BuildActiveOnly(origin.Source.Wound,
                    source.SourceId + "_check", source.SourceId, source.Resolution.Calculation).ToArray();
                _sourceFingerprint = JsonNode.Parse(source.WitnessJson)!["sourceFingerprint"]!.GetValue<string>();
                var response = owner._draftInputs.ReadImage("output/interface_updates.json");
                _interfaceJson = response.Bytes is { } bytes ? CanonicalJsonUtf8.DecodeOneOptionalBom(bytes) : "{}";
                GraphResponseFingerprint = response.Fingerprint;
                Fingerprint = WoundAcceptedTurnFingerprintWriter.Compute(new[]
                    { "book_of_eternity.spiritual_combat_healing.selection", "1", SourceFingerprint, GraphResponseFingerprint });
            }

            internal string Fingerprint { get; }
            internal string SourceFingerprint => _sourceFingerprint;
            internal string GraphResponseFingerprint { get; }
            internal string EventRef => _source.EventRef;
            internal string SourceId => _source.SourceId;
            internal string ProgramReference => _owner._combatHealingOrigin!.Intent.Fingerprint;
            internal JsonObject Witness => JsonNode.Parse(_source.WitnessJson)!.AsObject();
            internal WoundMaterializationEnvelope Target => WoundAcceptedTurnData.CloneWound(_owner._combatHealingOrigin!.Source.Wound)!;
            internal WoundAcceptedTurnInput Input => WoundAcceptedTurnData.CloneInput(_input)!;
            internal AcceptedMechanicsPlanner.SpiritualExchangeInterval Interval { get; }
            internal EffectAcceptedTurnPlanner.BaseResourceRouting? Routing => _owner._resources?.Routing;
            internal IReadOnlyList<SpiritualWoundStage> Stages => _stages.ToArray();
            internal ResourceTransition ReadActualSpend() => ReadCombatSpend(Interval, JsonNode.Parse(_source.ExchangeJson)!.AsObject());

            internal static ResourceTransition ReadCombatSpend(AcceptedMechanicsPlanner.SpiritualExchangeInterval interval, JsonObject exchange)
            {
                var rows = interval.AppliedTransitions.Concat(interval.ReplayTransitions).Where(t =>
                    t.OriginKind == "afterlife_cost" && t.OriginId == interval.ExchangeId &&
                    t.Coordinate == new ResourceCoordinate("chaos_sea", ResourceOwnerKind.AfterlifeActor, "player_soul", "spiritual_action_points") &&
                    t.Operation == ResourceTransitionOperation.Spend).ToArray();
                if (rows.Length != 1 || exchange["actionCostAudit"]?["player"] is not JsonObject audit ||
                    audit["effectiveCost"] is not JsonValue cost || !cost.TryGetValue<int>(out var amount) || amount < 2 ||
                    rows[0].RequestedAmount != amount || rows[0].AppliedAmount != amount ||
                    rows[0].BeforeState?.Current != audit["before"]!.GetValue<int>() ||
                    rows[0].AfterState?.Current != audit["after"]!.GetValue<int>())
                    throw new InvalidDataException("Combat healing requires exactly its actual closed typed OD spend.");
                return rows[0];
            }

            internal bool IsCurrentFor(EffectAcceptedTurnPlanner.EffectAcceptedDraft draft) =>
                _owner.IsCurrentOwner && ReferenceEquals(_owner._effects, draft) && ReferenceEquals(_owner._combatHealingSelection, this) &&
                _owner._resources is { Result: null } resources && resources.Owns(Interval) &&
                ReferenceEquals(_owner._lastResourceStep?.Interval, Interval) && Interval.Ordinal == _owner._nextResourceOrdinal - 1 &&
                _owner._source.ContinuationRevision == _sourceRevision && _owner._source.CheckedExchanges.Count > Interval.Ordinal &&
                _owner._source.CheckedExchanges[Interval.Ordinal] == _source.ExchangeJson;

            internal WoundOperationBeforeData? ReadInitialState(EffectAcceptedTurnPlanner.EffectAcceptedDraft draft,
                List<ValidationIssue> issues)
            {
                if (IsCurrentFor(draft)) return _owner.ReadInitialWoundState(issues);
                issues.Add(SourceIssue(AcceptedMechanicsPlan.WoundCommandPath, "spiritual_combat_healing_selection_stale",
                    "the actual current capture, closed resource interval and combat selection"));
                return null;
            }

            internal WoundAcceptedTurnPreparationResult Prepare(
                EffectAcceptedTurnPlanner.EffectAcceptedDraft.EffectDraftWoundBeforeAuthority before)
            {
                if (!before.IsPreparing || !ReferenceEquals(before.Selection.Combat, this) ||
                    !IsCurrentFor(_owner._effects!) || _before is not null && !ReferenceEquals(_before, before))
                    throw new InvalidOperationException("Only this actual current combat draft-before may prepare healing.");
                var catalog = WoundCarrierCatalog.Build(before.Data.WoundCarriers!);
                if (catalog.Issues.Count != 0 || !catalog.TryResolveOne(Target.WoundId, out var occurrence) ||
                    WoundMaterializationContract.SerializeCanonical(occurrence.Wound) != WoundMaterializationContract.SerializeCanonical(Target))
                    throw new InvalidDataException("The combat treatment target changed before its actual execution boundary.");
                var profiles = (before.Data.WoundCarriers!.AfterlifeProfiles?["profiles"] as JsonArray)?.OfType<JsonObject>()
                    .Where(p => WoundCarrierCollectionAuthority.AfterlifeProfileMatches(p, Target.Owner)).ToArray();
                var mechanics = EffectMechanicsSnapshot.Build(new EffectMechanicsInput(before.Data.EffectCarriers!,
                    before.Data.EffectIdentity!, _owner._input.PlanningContext!.EffectPlan!.SkillScopeAuthority));
                var soul = JsonNode.Parse(_owner._source.ReadOriginal(SpiritualWoundSourceSession.SoulPath)!)!.AsObject();
                if (profiles is not { Length: 1 } || !mechanics.IsAccepted)
                    throw new InvalidDataException("Combat healing lost its actual current actor or effect source.");
                var preparedSource = SpiritualHealingResolver.PrepareSource(new(occurrence.Wound, Target.Owner, profiles[0], soul, mechanics));
                if (preparedSource.Source is not { } actualSource || !JsonNode.DeepEquals(JsonSerializer.SerializeToNode(actualSource),
                        JsonSerializer.SerializeToNode(_owner._combatHealingOrigin!.Source.Source)))
                    throw new InvalidDataException("Combat healing source changed after the original die selection.");
                _before = before;
                AdmitDestinationGraph();
                var input = Input;
                var fingerprint = WoundAcceptedTurnFingerprints.ComputeInput(input);
                var baseline = WoundAcceptedTurnPlannerCore.CreateBaselineAuthority(fingerprint, input);
                WoundPreparedAcceptedTurnPlan Build(string seal) => new(input.Binding,
                    WoundAcceptedTurnFingerprints.ComputeBinding(input.Binding), fingerprint, seal, new[] { Target.WoundId },
                    _stages.Select(s => s.TransitionId).ToArray(), new[] { Target }, Array.Empty<WoundEffectOperationBatch>(), baseline,
                    draftBefore: before, combatHealingAuthority: this);
                var provisional = Build(string.Empty);
                return new(Build(WoundAcceptedTurnFingerprints.ComputePreparation(provisional)), Array.Empty<ValidationIssue>());
            }

            internal void AdmitDestinationGraph()
            {
                if (_destinationGraph is not null) return;
                var required = SpiritualHealingRequestProjection.RequiredGraphs(new[] { Target }, _stages).ToArray();
                var response = StrictJsonAuthority.Deserialize<JsonObject>(_interfaceJson, new JsonSerializerOptions(), "combat interface")
                    ?? throw new InvalidDataException("Combat interface must be an object.");
                var rows = response["spiritualHealingGraphs"] as JsonArray;
                if (required.Length == 0 && rows is null) return;
                if (rows is null || rows.Count != required.Length || required.Length > 1)
                    throw new InvalidDataException("Combat destination graphs are required: " + new JsonArray(required.Select(r => (JsonNode)new JsonObject
                    { ["stageRef"] = SpiritualHealingRequestProjection.GraphRef(ProgramReference, r.WoundId, r.Rank), ["severityRank"] = r.Rank,
                        ["woundName"] = Target.Display.Name, ["complications"] = SpiritualHealingRequestProjection.Complications(ProgramReference, Target) }).ToArray()).ToJsonString());
                if (required.Length == 0) return;
                var row = rows[0] as JsonObject ?? throw new InvalidDataException("Combat graph row must be an object.");
                var reference = SpiritualHealingRequestProjection.GraphRef(ProgramReference, Target.WoundId, required[0].Rank);
                if (row.Count != 2 || row["stageRef"]?.GetValue<string>() != reference || !row.ContainsKey("consequenceDefinitions"))
                    throw new InvalidDataException("Combat graph must bind exactly its client stageRef and authored consequenceDefinitions.");
                var complications = Target.Complications.ToDictionary(c => SpiritualHealingRequestProjection.ComplicationRef(ProgramReference, c.ComplicationId),
                    c => c.ComplicationId, StringComparer.Ordinal);
                _destinationGraph = WoundResponseInputComposer.ParseSpiritualHealingGraph(JsonSerializer.SerializeToElement(row["consequenceDefinitions"]),
                    Target, required[0].Rank, reference, complications);
            }

            internal WoundWorkingOwnedGraph ResolveGraph(WoundMaterializationEnvelope before, SpiritualWoundStage stage) =>
                stage.Kind == "heal" || stage.SeverityRank == before.Severity.Rank
                    ? WoundMaterializationContract.ImportOwnedGraph(before.Complications, before.Consequences)
                    : WoundResponseInputComposer.BindSpiritualHealingGraphPredecessors(_destinationGraph
                        ?? throw new InvalidDataException("Missing actual combat destination graph."), before);

            internal bool PreparedAgrees(WoundPreparedAcceptedTurnPlan prepared) =>
                _owner.IsCurrentOwner && _before is not null && ReferenceEquals(prepared.CombatHealingAuthority, this) &&
                ReferenceEquals(prepared.DraftBefore, _before) && prepared.InputFingerprint == WoundAcceptedTurnFingerprints.ComputeInput(_input) &&
                prepared.EffectOperationBatches.Count == 0 && prepared.AllocatedWoundIds.SequenceEqual(new[] { Target.WoundId }) &&
                prepared.AllocatedTransitionIds.SequenceEqual(_stages.Select(s => s.TransitionId)) &&
                prepared.PreparedWounds.Count == 1 && WoundMaterializationContract.SerializeCanonical(prepared.PreparedWounds[0]) ==
                    WoundMaterializationContract.SerializeCanonical(Target);

            internal static CombatHealingSelection? CaptureAtClosedInterval(SpiritualOriginalTurnCapture owner,
                AcceptedMechanicsPlanner.SpiritualExchangeInterval interval, List<ValidationIssue> issues)
            {
                if (owner._combatHealingOrigin is null) return null;
                if (!owner.IsCurrentOwner || owner._effects is null || owner._resources is not { Result: null } resources ||
                    !resources.Owns(interval) || !ReferenceEquals(owner._lastResourceStep?.Interval, interval) ||
                    interval.Ordinal != owner._nextResourceOrdinal - 1 || interval.Ordinal >= owner._source.CheckedExchanges.Count)
                    throw new InvalidOperationException("Actual closed combat resource interval required.");
                var exchange = JsonNode.Parse(owner._source.CheckedExchanges[interval.Ordinal])!.AsObject();
                var source = owner._closedExchangeEvidence.Single(e => ReferenceEquals(e.Interval, interval)).Healing
                    ?? throw new InvalidOperationException("Actual closed healing source required.");
                _ = ReadCombatSpend(interval, exchange);
                if (source.Disposition != "resolved") return null; // Blocked never gets a wound insertion.
                if (owner._combatHealingSelection is { } retained)
                {
                    if (ReferenceEquals(retained.Interval, interval) && retained._source.WitnessJson == source.WitnessJson) return retained;
                    throw new InvalidDataException("Combat healing cannot replace its retained source or interval.");
                }
                var initial = owner.ReadInitialWoundState(issues);
                if (initial is null || issues.Count != 0) return null;
                return owner._combatHealingSelection = new(owner, source, interval, initial);
            }
        }
    }
}
