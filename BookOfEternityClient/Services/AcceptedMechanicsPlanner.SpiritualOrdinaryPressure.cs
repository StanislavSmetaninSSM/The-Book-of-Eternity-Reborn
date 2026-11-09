using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace BookOfEternityClient.Services;

internal static partial class AcceptedMechanicsPlanner
{
    internal sealed partial class ResourceExecutionSession
    {
        internal sealed partial class SpiritualMechanicsContext
        {
            /// <summary>Complete owner-issued comparison DATA and exact-row isolation for one ordinary probe.</summary>
            internal sealed partial class ExistingPressureComparisonGroup
            {
                private sealed record Member(SpiritualWoundConflictContribution Contribution,
                    string ParentJson, string GraphFingerprint, int Rank);
                private readonly IReadOnlyList<Member> _members;
                private readonly string _conflictId;
                private readonly JsonObject _exchange;
                private readonly int _ordinaryRank;
                private readonly string? _guardianActorId;
                /// <summary>Identifies the separately bounded guardian lane, never inferred from the player's result.</summary>
                internal bool IsGuardian => _guardianActorId is not null;
                /// <summary>Names the affected side of this complete authenticated group.</summary>
                internal string AffectedSide => IsGuardian ? "opposition" : "player";
                /// <summary>Separates guardian failure DATA from existing player ordinary-comparison roles.</summary>
                internal string InputRole => IsGuardian ? SpiritualWoundFailedAction.Role : "comparison_data/group_v1";
                /// <summary>Identifies the exact affected actor in the operation computation identity.</summary>
                internal string AffectedActorId => _guardianActorId ?? "player_soul";
                internal SpiritualMechanicsContext Context { get; }
                internal string DependencyData { get; }
                /// <summary>Reports the complete authenticated group cardinality for private actual-lane dispatch.</summary>
                internal int ParentCount => _members.Count;

                private ExistingPressureComparisonGroup(SpiritualMechanicsContext context,
                    IEnumerable<Member> members, string conflictId, JsonObject exchange, int ordinaryRank, string? guardianActorId = null)
                {
                    Context = context;
                    _members = Array.AsReadOnly(members.ToArray());
                    _conflictId = conflictId;
                    _exchange = exchange.DeepClone().AsObject();
                    _ordinaryRank = ordinaryRank;
                    _guardianActorId = guardianActorId;
                    var data = new JsonArray(new JsonObject { ["role"] = InputRole, ["revision"] = 3,
                        ["side"] = AffectedSide, ["guardianId"] = guardianActorId,
                        ["actionSlot"] = IsGuardian ? "/incomingAction" : null });
                    foreach (var member in _members)
                        data.Add(new JsonObject { ["parent"] = member.ParentJson,
                            ["graph"] = member.GraphFingerprint, ["effect"] = member.Contribution.EffectId,
                            ["component"] = member.Contribution.ComponentId });
                    DependencyData = data.ToJsonString();
                }

                /// <summary>Isolates all and only the exact selected strain rows in the frozen ordinary exchange.</summary>
                internal bool Isolates(SpiritualMechanicsContext context,
                    SpiritualWoundConflictContribution contribution, JsonObject conflict, JsonObject exchange) =>
                    ReferenceEquals(Context, context) && context.IsCurrent &&
                    _members.Any(member => ReferenceEquals(member.Contribution, contribution)) &&
                    conflict["conflictId"]?.GetValue<string>() == _conflictId &&
                    JsonNode.DeepEquals(_exchange, exchange);

                /// <summary>Private actual eligibility is rederived from complete group DATA before any core cache use.</summary>
                internal SingletonActualBasis CaptureSingletonActualBasis() => SingletonActualBasis.Capture(this);

                /// <summary>Mints complete multi-parent eligibility separately from the singleton lane and ordinary comparison DATA.</summary>
                internal MultiParentActualBasis CaptureMultiParentActualBasis() => MultiParentActualBasis.Capture(this);

                /// <summary>Only the three sealed group issuers feed the private actual projector; DATA cannot supply this basis.</summary>
                internal abstract class ActualBasis
                {
                    private protected ActualBasis() { }
                    /// <summary>Identifies the live mechanics context that issued this separate actual basis.</summary>
                    internal abstract SpiritualMechanicsContext Context { get; }
                    /// <summary>Identifies the exact projected side without broadening the player-only basis.</summary>
                    internal virtual string AffectedSide => "player";
                    /// <summary>Matches only freshly owned selected strain rows in the exact conflict.</summary>
                    internal abstract bool MatchesSelectedContribution(SpiritualMechanicsContext context,
                        SpiritualWoundConflictContribution contribution, JsonObject conflict);
                }

                /// <summary>Singular actual permission cannot be supplied by comparison DATA or by choosing a group member.</summary>
                internal sealed class SingletonActualBasis : ActualBasis
                {
                    private readonly ExistingPressureComparisonGroup _group;
                    private readonly Member _member;
                    /// <inheritdoc />
                    internal override SpiritualMechanicsContext Context => _group.Context;
                    internal string ParentJson => _member.ParentJson;
                    internal string EffectId => _member.Contribution.EffectId;
                    internal string ComponentId => _member.Contribution.ComponentId;

                    private SingletonActualBasis(ExistingPressureComparisonGroup group, Member member)
                    { _group = group; _member = member; }

                    /// <summary>Mints a fresh actual-only basis after every unchanged singleton-specific early guard.</summary>
                    internal static SingletonActualBasis Capture(ExistingPressureComparisonGroup group)
                    {
                        if (!group.Context.IsCurrent)
                            throw new InvalidOperationException("The comparison group must still have its current owners.");
                        if (group.IsGuardian) throw new InvalidOperationException("Guardian attribution requires its separate actual basis.");
                        if (group._members.Count != 1)
                            throw new InvalidOperationException("actual existing pressure requires exactly one comparison parent");
                        if (group._ordinaryRank is < 1 or > 4)
                            throw new InvalidOperationException("actual existing pressure requires an increasing ordinary result at most broken");
                        var member = group._members.Single();
                        if (member.Rank is not (3 or 4))
                            throw new InvalidOperationException("actual existing pressure requires a rank-III/IV parent");
                        if (group.Context.Contributions.Any(row => row.ResolvedSide == "player" && row.Actor.Kind == "player" &&
                            row.Actor.TargetId == "player_soul" && row.Operation == "pressure" && row.WoundSource.Kind == "wound" &&
                            row.WoundSource.SourceId != member.Contribution.WoundSource.SourceId))
                            throw new InvalidOperationException("actual existing pressure excludes another pressure wound source");
                        return new(group, member);
                    }

                    /// <summary>Matches only this freshly checked singleton's exact row in its live mechanics context.</summary>
                    internal override bool MatchesSelectedContribution(SpiritualMechanicsContext context,
                        SpiritualWoundConflictContribution contribution, JsonObject conflict) =>
                        ReferenceEquals(Context, context) && context.IsCurrent &&
                        ReferenceEquals(_member.Contribution, contribution) &&
                        conflict["conflictId"]?.GetValue<string>() == _group._conflictId;
                }

                /// <summary>Immutable serialized parent facts do not grant current applicability or publication authority.</summary>
                internal sealed record ActualParentData(string ParentJson, string EffectId, string ComponentId);

                /// <summary>Fresh complete count-at-least-two permission is issued only from the complete authenticated current group.</summary>
                internal sealed class MultiParentActualBasis : ActualBasis
                {
                    private readonly ExistingPressureComparisonGroup _group;
                    /// <inheritdoc />
                    internal override SpiritualMechanicsContext Context => _group.Context;
                    /// <summary>Exposes immutable complete parent facts in the authenticated group's canonical order.</summary>
                    internal IReadOnlyList<ActualParentData> Parents { get; }

                    private MultiParentActualBasis(ExistingPressureComparisonGroup group)
                    {
                        _group = group;
                        Parents = Array.AsReadOnly(group._members.Select(member => new ActualParentData(
                            member.ParentJson, member.Contribution.EffectId, member.Contribution.ComponentId)).ToArray());
                    }

                    /// <summary>Rechecks bounded multi-parent eligibility before shared ordinary DATA can be reused.</summary>
                    internal static MultiParentActualBasis Capture(ExistingPressureComparisonGroup group)
                    {
                        if (!group.Context.IsCurrent)
                            throw new InvalidOperationException("The comparison group must still have its current owners.");
                        if (group.IsGuardian) throw new InvalidOperationException("Guardian attribution cannot use a player multi-parent basis.");
                        if (group.Context.Ordinal != 0)
                            throw new InvalidOperationException("actual existing pressure group requires ordinal zero");
                        if (group._members.Count < 2 || group._members.Any(member => member.Rank is not (3 or 4) ||
                                member.Contribution.CurrentStacks != 1 ||
                                !member.Contribution.Magnitude.TryGetInt32(out var magnitude) || magnitude != 1) ||
                            Enumerable.Range(1, group._members.Count - 1).Any(index =>
                                StringComparer.Ordinal.Compare(group._members[index - 1].Contribution.WoundSource.SourceId,
                                    group._members[index].Contribution.WoundSource.SourceId) >= 0))
                            throw new InvalidOperationException("actual existing pressure group requires complete distinct ordered III/IV parents");
                        if (group._ordinaryRank is < 1 or > 4)
                            throw new InvalidOperationException("actual existing pressure group requires increasing ordinary I through IV");
                        var allowed = group._members.Select(member => new EffectIdentitySourceGroup(
                            member.Contribution.WoundSource.Realm, member.Contribution.WoundSource.Kind,
                            member.Contribution.WoundSource.SourceId)).ToHashSet();
                        if (allowed.Count != group._members.Count || group.Context.Contributions.Any(row =>
                            row.ResolvedSide == "player" && row.Actor.Kind == "player" && row.Actor.TargetId == "player_soul" &&
                            row.Operation == "pressure" && row.WoundSource.Kind == "wound" &&
                            !allowed.Contains(new(row.WoundSource.Realm, row.WoundSource.Kind, row.WoundSource.SourceId))))
                            throw new InvalidOperationException("actual existing pressure excludes another pressure wound source");
                        return new(group);
                    }

                    /// <summary>Only the complete group’s selected strain objects in this still-current context match, never their sibling components.</summary>
                    internal override bool MatchesSelectedContribution(SpiritualMechanicsContext context,
                        SpiritualWoundConflictContribution contribution, JsonObject conflict) =>
                        ReferenceEquals(Context, context) && context.IsCurrent &&
                        _group._members.Any(member => ReferenceEquals(member.Contribution, contribution)) &&
                        conflict["conflictId"]?.GetValue<string>() == _group._conflictId;
                }

                /// <summary>Authenticates every matching row and complete current/signed parent before bounding the group.</summary>
                internal static ExistingPressureComparisonGroup? Capture(SpiritualMechanicsContext context,
                    JsonObject conflict, JsonObject exchange, List<ValidationIssue> issues)
                {
                    if (exchange.ContainsKey(SpiritualWoundFailedAction.Property))
                        return CaptureGuardian(context, conflict, exchange, issues);
                    if (!context.IsCurrent || context._source.Realm != "chaos_sea" ||
                        context._source.TurnNumber <= 0 ||
                        !IsToken(exchange["operationType"]?.GetValue<string>(), "pressure") ||
                        !IsToken(exchange["outcome"]?.GetValue<string>(), "setback") ||
                        exchange["incomingAction"] is not JsonObject incoming ||
                        !IsToken((incoming["finalOperationType"] ?? incoming["operationType"])?.GetValue<string>(), "pressure") ||
                        exchange["diceAudit"] is not JsonObject ||
                        exchange.ContainsKey("specialArtAudit") || exchange.ContainsKey("specialArtAudits") ||
                        exchange["actionCostAudit"]?["player"]?["specialArtId"] is not null ||
                        exchange["actionCostAudit"]?["opposition"]?["specialArtId"] is not null ||
                        exchange["spiritualWoundTarget"] is not null || incoming["spiritualWoundTarget"] is not null ||
                        exchange["before"] is not JsonObject before || exchange["after"] is not JsonObject after ||
                        !JsonNode.DeepEquals(before["oppositionSideStrain"], after["oppositionSideStrain"])) return null;
                    var previous = SpiritualWoundOpportunityMath.StrainRank(before["playerSideStrain"]?.GetValue<string>());
                    var ordinary = SpiritualWoundOpportunityMath.StrainRank(after["playerSideStrain"]?.GetValue<string>());
                    if (previous < 0 || previous >= ordinary || ordinary > 4) return null;
                    var actor = ValidationService.SpiritualWoundSourceSession.ActorKey(
                        conflict["playerSide"]?["leadContestant"] as JsonObject);
                    if (actor != "player_soul:player_soul" ||
                        !context._source.OwnsSignedStrainActor(context._capture, conflict, actor)) return null;
                    // Collect the whole available set before rank or cardinality selection. Never repair duplicates.
                    var matching = context.Contributions.Where(row => row.ResolvedSide == "player" &&
                        row.Actor.Kind == "player" && row.Actor.TargetId == "player_soul" &&
                        row.Operation == "pressure" && row.Profile == "spiritual_strain_burden")
                        .OrderBy(row => row.WoundSource.Realm, StringComparer.Ordinal)
                        .ThenBy(row => row.WoundSource.Kind, StringComparer.Ordinal)
                        .ThenBy(row => row.WoundSource.SourceId, StringComparer.Ordinal)
                        .ThenBy(row => row.EffectId, StringComparer.Ordinal)
                        .ThenBy(row => row.ComponentId, StringComparer.Ordinal).ToArray();
                    if (matching.Length == 0) return null;
                    var sourceKeys = new HashSet<EffectIdentitySourceGroup>();
                    foreach (var selected in matching)
                        if (selected.CurrentStacks != 1 || selected.WoundSource.Kind != "wound" ||
                            selected.Actor.Realm != context._source.Realm ||
                            selected.Actor.Realm != selected.WoundSource.Realm ||
                            !selected.Magnitude.TryGetInt32(out var magnitude) || magnitude != 1 ||
                            !sourceKeys.Add(new(selected.WoundSource.Realm, selected.WoundSource.Kind,
                                selected.WoundSource.SourceId))) return null;
                    var view = context.ReadCurrentWoundView(issues);
                    // Each getter detaches; take each snapshot exactly once for the complete batch.
                    var carriers = view?.WoundCarriers;
                    var identity = view?.WoundIdentity;
                    var history = view?.WoundHistory;
                    if (carriers == null || identity == null || history == null || issues.Count != 0) return null;
                    var ids = matching.Select(row => row.WoundSource.SourceId).ToArray();
                    var parents = SpiritualWoundCurrentData.ReadActiveBatch(carriers, identity.ToJsonString(), history.ToJsonString(),
                        context._source.Realm, ids, actor, context._source.OrdinaryCurrentViewDiagnostics);
                    var signed = context._source.ReadSignedStrainParents(context._capture, conflict, ids, actor);
                    var members = new List<Member>();
                    for (var index = 0; index < matching.Length; index++)
                    {
                        var selected = matching[index];
                        var parent = parents[index];
                        var parentJson = WoundMaterializationContract.SerializeCanonical(parent);
                        if (parent.Classification.Domain != "spiritual" ||
                            parentJson != WoundMaterializationContract.SerializeCanonical(signed[index]) ||
                            !context._owner._routing!.TryReadSpiritualWoundGroup(
                                new(parent.Owner.Realm, "wound", parent.WoundId), out var group) || group.SameTurn ||
                            !MatchesSignedParentSourceGroup(parent, selected, group)) return null;
                        members.Add(new(selected, parentJson, group.GraphAuthorityFingerprint, parent.Severity.Rank));
                    }
                    if (members.Count == 0 || members.Any(member => member.Rank is not (3 or 4))) return null;
                    return new(context, members, conflict["conflictId"]!.GetValue<string>(), exchange, ordinary);
                }
            }

            /// <summary>Distinguishes current-turn wound routing from the separately supported signed-existing parent lane.</summary>
            internal bool HasSameTurnPressureStrainGroup()
            {
                if (!IsCurrent) throw new InvalidOperationException("Current wound routing is required.");
                return Contributions.Where(row => row.Profile == "spiritual_strain_burden" && row.Operation == "pressure" &&
                    row.ResolvedSide == "player" && row.Actor.Kind == "player" && row.Actor.TargetId == "player_soul")
                    .Any(row => _owner._routing!.TryReadSpiritualWoundGroup(
                        new(row.WoundSource.Realm, row.WoundSource.Kind, row.WoundSource.SourceId), out var group) && group.SameTurn);
            }

            /// <summary>Authenticates the exact capture/source/resource tuple before private ordinary evaluation.</summary>
            internal bool OwnsExistingPressureOrdinary(ValidationService.SpiritualOriginalTurnCapture capture,
                ValidationService.SpiritualWoundSourceSession source) =>
                ReferenceEquals(_capture, capture) && ReferenceEquals(_source, source) &&
                IsCurrent && capture.OwnsMechanicsOwner(_owner, source);

            private static bool IsToken(string? value, string token) =>
                string.Equals(value, token, StringComparison.OrdinalIgnoreCase);

            /// <summary>Captures complete in-memory epoch and contribution DATA without re-reading signed parents.</summary>
            internal OperationStructuralSnapshot? ReadOrdinaryPressureDependencySnapshot()
            {
                _owner.Enter();
                try
                {
                    _owner.EnsureUsable();
                    if (!IsCurrent || _owner._active != null || _owner._staged != null || _owner._pendingExchange)
                        return null;
                    var values = new List<string?> { "existing_pressure_ordinary_v1", "comparison_data/group_v1", "3",
                        Ordinal.ToString(CultureInfo.InvariantCulture), _woundVersion.ToString(CultureInfo.InvariantCulture),
                        _dependencyCutVersion.ToString(CultureInfo.InvariantCulture) };
                    if (_source.ReadMechanicsConflict(_capture)["activeConflict"] is not JsonObject conflict) return null;
                    values.Add(JsonSerializer.Serialize(_owner._state.ReadSpiritualActionPointBefore(conflict)));
                    // This local traversal set is not a retained cache: every row/availability is still captured,
                    // while a shared immutable parent graph is recomputed only once in this locked snapshot.
                    var sourceGroups = new HashSet<EffectIdentitySourceGroup>();
                    foreach (var row in _acceptedContributions)
                    {
                        values.Add(JsonSerializer.Serialize(row));
                        values.Add(_owner._state.IsMechanicallyAvailable(row.EffectId).ToString());
                        if (row.WoundSource.Kind != "wound") continue;
                        var key = new EffectIdentitySourceGroup(row.WoundSource.Realm, "wound", row.WoundSource.SourceId);
                        if (!sourceGroups.Add(key)) continue;
                        values.Add(JsonSerializer.Serialize(key));
                        if (_owner._routing!.TryReadSpiritualWoundGroup(key, out var group))
                        {
                            values.Add(group.GraphAuthorityFingerprint);
                            values.Add(group.RecomputeGraphAuthorityFingerprint());
                            values.Add(group.SameTurn.ToString());
                            values.Add(group.SourceRef);
                            values.Add(group.PreparedSourceExportFingerprint);
                        }
                        else values.Add(null);
                    }
                    return new(values, [_owner, _capture, _source, _epoch, _woundDraft]);
                }
                finally { _owner.Exit(); }
            }
        }
    }
}
