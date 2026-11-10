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

                /// <summary>Current complete guardian eligibility, never minted by selecting a player group member.</summary>
                internal sealed class GuardianActualBasis : ActualBasis
                {
                    private readonly ExistingPressureComparisonGroup _group;
                    internal override SpiritualMechanicsContext Context => _group.Context;
                    internal override string AffectedSide => "opposition";
                    /// <summary>Complete immutable parent facts in the authenticated group's canonical order.</summary>
                    internal IReadOnlyList<ActualParentData> Parents { get; }
                    internal string ActorId => _group._guardianActorId!;

                    private GuardianActualBasis(ExistingPressureComparisonGroup group)
                    {
                        _group = group;
                        Parents = Array.AsReadOnly(group._members.Select(member => new ActualParentData(
                            member.ParentJson, member.Contribution.EffectId, member.Contribution.ComponentId)).ToArray());
                    }

                    /// <summary>Rechecks every ordinal-zero guardian III/IV member before ordinary DATA reuse.</summary>
                    internal static GuardianActualBasis Capture(ExistingPressureComparisonGroup group)
                    {
                        if (!group.Context.IsCurrent || !group.IsGuardian || group.Context.Ordinal != 0 ||
                            group._members.Count == 0 || group._ordinaryRank is not (2 or 3 or 4) ||
                            group._members.Any(member => member.Rank is not (3 or 4) || member.Contribution.CurrentStacks != 1 ||
                                !member.Contribution.Magnitude.TryGetInt32(out var magnitude) || magnitude != 1) ||
                            Enumerable.Range(1, group._members.Count - 1).Any(index =>
                                StringComparer.Ordinal.Compare(group._members[index - 1].Contribution.WoundSource.SourceId,
                                    group._members[index].Contribution.WoundSource.SourceId) >= 0) ||
                            SpiritualWoundFailedAction.ReadGuardianDeclaration(group._exchange) != group._guardianActorId)
                            throw new InvalidOperationException("Actual guardian pressure requires its current complete declared III/IV ordinary-II/III/IV basis.");
                        if (HasAnotherGuardianPressureSource(group.Context.Contributions, group._guardianActorId!,
                                group._members.Select(member => member.Contribution).ToArray()))
                            throw new InvalidOperationException("Actual guardian pressure excludes another pressure wound source.");
                        return new(group);
                    }

                    internal override bool MatchesSelectedContribution(SpiritualMechanicsContext context,
                        SpiritualWoundConflictContribution contribution, JsonObject conflict) =>
                        ReferenceEquals(Context, context) && context.IsCurrent &&
                        _group._members.Any(member => ReferenceEquals(member.Contribution, contribution)) &&
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
                        row.Profile == "spiritual_strain_burden")
                        .OrderBy(row => row.WoundSource.Realm, StringComparer.Ordinal)
                        .ThenBy(row => row.WoundSource.Kind, StringComparer.Ordinal)
                        .ThenBy(row => row.WoundSource.SourceId, StringComparer.Ordinal)
                        .ThenBy(row => row.EffectId, StringComparer.Ordinal)
                        .ThenBy(row => row.ComponentId, StringComparer.Ordinal).ToArray();
                    if (matching.Length == 0) return null;
                    var sourceKeys = new HashSet<EffectIdentitySourceGroup>();
                    foreach (var selected in matching)
                        if (selected.CurrentStacks != 1 || selected.WoundSource.Kind != "wound" ||
                            selected.Actor.Realm != context._source.Realm || selected.Actor.Realm != selected.WoundSource.Realm ||
                            !selected.Magnitude.TryGetInt32(out var magnitude) || magnitude != 1 ||
                            !sourceKeys.Add(new(selected.WoundSource.Realm, selected.WoundSource.Kind, selected.WoundSource.SourceId))) return null;
                    if (HasAnotherGuardianPressureSource(context.Contributions, id, matching)) return null;
                    var view = context.ReadCurrentWoundView(issues);
                    var carriers = view?.WoundCarriers;
                    var identity = view?.WoundIdentity;
                    var history = view?.WoundHistory;
                    if (carriers == null || identity == null || history == null || issues.Count != 0) return null;
                    var ids = matching.Select(row => row.WoundSource.SourceId).ToArray();
                    var parents = SpiritualWoundCurrentData.ReadActiveBatch(carriers, identity.ToJsonString(), history.ToJsonString(),
                        context._source.Realm, ids, actor, context._source.OrdinaryCurrentViewDiagnostics);
                    var signed = context._source.ReadSignedGuardianStrainParents(context._capture, conflict, ids, actor);
                    var members = new List<Member>();
                    for (var index = 0; index < matching.Length; index++)
                    {
                        var selected = matching[index];
                        var parent = parents[index];
                        var parentJson = WoundMaterializationContract.SerializeCanonical(parent);
                        if (parent.Classification.Domain != "spiritual" || parent.Severity.Rank is not (3 or 4) ||
                            parentJson != WoundMaterializationContract.SerializeCanonical(signed[index]) ||
                            !context._owner._routing!.TryReadSpiritualWoundGroup(new(parent.Owner.Realm, "wound", parent.WoundId), out var group) ||
                            group.SameTurn || !MatchesSignedParentSourceGroup(parent, selected, group)) return null;
                        members.Add(new(selected, parentJson, group.GraphAuthorityFingerprint, parent.Severity.Rank));
                    }
                    return new(context, members,
                        conflict["conflictId"]!.GetValue<string>(), exchange,
                        SpiritualWoundOpportunityMath.StrainRank(after["oppositionSideStrain"]!.GetValue<string>()), id);
                }

                /// <summary>Bounds all matching wound sources, retaining legal siblings of every selected parent.</summary>
                /// <param name="contributions">Complete current pressure contribution DATA.</param>
                /// <param name="actorId">Exact declared guardian identifier.</param>
                /// <param name="selected">Complete distinct selected wound coordinates, not a saturation-limited subset.</param>
                /// <returns>True for an empty/duplicate set or a matching wound source outside that set.</returns>
                private static bool HasAnotherGuardianPressureSource(IReadOnlyList<SpiritualWoundConflictContribution> contributions,
                    string actorId, IReadOnlyList<SpiritualWoundConflictContribution> selected)
                {
                    var allowed = selected.Select(row => new EffectIdentitySourceGroup(
                        row.WoundSource.Realm, row.WoundSource.Kind, row.WoundSource.SourceId)).ToHashSet();
                    return selected.Count == 0 || allowed.Count != selected.Count || contributions.Any(row =>
                        row.ResolvedSide == "opposition" && row.Actor.Kind == "guardian" && row.Actor.TargetId == actorId &&
                        row.Operation == "pressure" && row.WoundSource.Kind == "wound" &&
                        !allowed.Contains(new(row.WoundSource.Realm, row.WoundSource.Kind, row.WoundSource.SourceId)));
                }

                /// <summary>Bounds the complete matching wound-source set, retaining siblings of the one selected parent.</summary>
                internal static bool HasAnotherGuardianPressureSource(IReadOnlyList<SpiritualWoundConflictContribution> contributions, string actorId,
                    SpiritualWoundConflictContribution selected) => HasAnotherGuardianPressureSource(contributions, actorId, [selected]);
            }
        }
    }
}
