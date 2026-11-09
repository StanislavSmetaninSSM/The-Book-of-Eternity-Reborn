using System.Text.Json.Nodes;

namespace BookOfEternityClient.Services;

internal static partial class AcceptedMechanicsPlanner
{
    internal sealed partial class ResourceExecutionSession
    {
        internal sealed partial class SpiritualMechanicsContext
        {
            internal sealed partial class ExistingPressureComparisonGroup
            {
                /// <summary>Issues the guardian-only actual basis independently of ordinary comparison DATA.</summary>
                internal GuardianActualBasis CaptureGuardianActualBasis() => GuardianActualBasis.Capture(this);

                /// <summary>Current singleton guardian eligibility, never minted by selecting a player group member.</summary>
                internal sealed class GuardianActualBasis : ActualBasis
                {
                    private readonly ExistingPressureComparisonGroup _group;
                    private readonly Member _member;
                    internal override SpiritualMechanicsContext Context => _group.Context;
                    internal override string AffectedSide => "opposition";
                    internal string ParentJson => _member.ParentJson;
                    internal string EffectId => _member.Contribution.EffectId;
                    internal string ComponentId => _member.Contribution.ComponentId;
                    internal string ActorId => _group._guardianActorId!;

                    private GuardianActualBasis(ExistingPressureComparisonGroup group, Member member)
                    { _group = group; _member = member; }

                    /// <summary>Rechecks exact ordinal-zero guardian singleton III/IV applicability before ordinary DATA reuse.</summary>
                    internal static GuardianActualBasis Capture(ExistingPressureComparisonGroup group)
                    {
                        if (!group.Context.IsCurrent || !group.IsGuardian || group.Context.Ordinal != 0 ||
                            group._members.Count != 1 || group._members[0].Rank is not (3 or 4) || group._ordinaryRank is not (2 or 3 or 4) ||
                            SpiritualWoundFailedAction.ReadGuardianDeclaration(group._exchange) != group._guardianActorId)
                            throw new InvalidOperationException("Actual guardian pressure requires its current declared singleton III/IV ordinary-II/III/IV basis.");
                        if (HasAnotherGuardianPressureSource(group.Context.Contributions, group._guardianActorId!,
                                group._members[0].Contribution))
                            throw new InvalidOperationException("Actual guardian pressure excludes another pressure wound source.");
                        return new(group, group._members[0]);
                    }

                    internal override bool MatchesSelectedContribution(SpiritualMechanicsContext context,
                        SpiritualWoundConflictContribution contribution, JsonObject conflict) =>
                        ReferenceEquals(Context, context) && context.IsCurrent && ReferenceEquals(_member.Contribution, contribution) &&
                        conflict["conflictId"]?.GetValue<string>() == _group._conflictId &&
                        ValidationService.SpiritualWoundSourceSession.ActorKey(conflict["oppositionSide"]?["leadContestant"] as JsonObject) == "guardian:" + ActorId;
                }

                /// <summary>Authenticates the whole declared guardian group without treating declaration shape as validated failure.</summary>
                private static ExistingPressureComparisonGroup? CaptureGuardian(SpiritualMechanicsContext context,
                    JsonObject conflict, JsonObject exchange, List<ValidationIssue> issues)
                {
                    if (!context.IsCurrent || context._source.Realm != "chaos_sea" || context._source.TurnNumber <= 0 ||
                        context.Ordinal != 0 || exchange["diceAudit"] is not JsonObject ||
                        exchange["before"] is not JsonObject before || exchange["after"] is not JsonObject after)
                        return null;
                    var id = SpiritualWoundFailedAction.ReadGuardianDeclaration(exchange);
                    var actor = "guardian:" + id;
                    if (!context._source.OwnsSignedGuardianStrainActor(context._capture, conflict, actor) ||
                        before["oppositionSideStrain"]?.GetValue<string>() != "strained" ||
                        after["oppositionSideStrain"]?.GetValue<string>() is not ("fractured" or "overwhelmed" or "broken")) return null;
                    foreach (var field in new[] { "playerSideStrain", "conflictPosition", "controlState", "tempoAdvantage" })
                        if (!JsonNode.DeepEquals(before[field], after[field])) return null;
                    var matching = context.Contributions.Where(row => row.ResolvedSide == "opposition" &&
                        row.Actor.Kind == "guardian" && row.Actor.TargetId == id && row.Operation == "pressure" &&
                        row.Profile == "spiritual_strain_burden").ToArray();
                    if (matching.Length != 1 || matching[0] is not { CurrentStacks: 1 } selected ||
                        selected.WoundSource.Kind != "wound" || selected.Actor.Realm != context._source.Realm ||
                        selected.Actor.Realm != selected.WoundSource.Realm ||
                        !selected.Magnitude.TryGetInt32(out var magnitude) || magnitude != 1) return null;
                    if (HasAnotherGuardianPressureSource(context.Contributions, id, selected)) return null;
                    var view = context.ReadCurrentWoundView(issues);
                    var carriers = view?.WoundCarriers;
                    var identity = view?.WoundIdentity;
                    var history = view?.WoundHistory;
                    if (carriers == null || identity == null || history == null || issues.Count != 0) return null;
                    var ids = new[] { selected.WoundSource.SourceId };
                    var parents = SpiritualWoundCurrentData.ReadActiveBatch(carriers, identity.ToJsonString(), history.ToJsonString(),
                        context._source.Realm, ids, actor, context._source.OrdinaryCurrentViewDiagnostics);
                    var signed = context._source.ReadSignedGuardianStrainParents(context._capture, conflict, ids, actor);
                    var parent = parents.Single();
                    var parentJson = WoundMaterializationContract.SerializeCanonical(parent);
                    if (parent.Classification.Domain != "spiritual" || parent.Severity.Rank is not (3 or 4) ||
                        parentJson != WoundMaterializationContract.SerializeCanonical(signed.Single()) ||
                        !context._owner._routing!.TryReadSpiritualWoundGroup(new(parent.Owner.Realm, "wound", parent.WoundId), out var group) ||
                        group.SameTurn || !MatchesSignedParentSourceGroup(parent, selected, group)) return null;
                    return new(context, [new(selected, parentJson, group.GraphAuthorityFingerprint, parent.Severity.Rank)],
                        conflict["conflictId"]!.GetValue<string>(), exchange,
                        SpiritualWoundOpportunityMath.StrainRank(after["oppositionSideStrain"]!.GetValue<string>()), id);
                }

                /// <summary>Bounds the complete matching wound-source set, retaining siblings of the one selected parent.</summary>
                internal static bool HasAnotherGuardianPressureSource(IReadOnlyList<SpiritualWoundConflictContribution> contributions, string actorId,
                    SpiritualWoundConflictContribution selected) => contributions.Any(row =>
                    row.ResolvedSide == "opposition" && row.Actor.Kind == "guardian" && row.Actor.TargetId == actorId &&
                    row.Operation == "pressure" && row.WoundSource.Kind == "wound" &&
                    (row.WoundSource.Realm != selected.WoundSource.Realm ||
                     row.WoundSource.SourceId != selected.WoundSource.SourceId));
            }
        }
    }
}
