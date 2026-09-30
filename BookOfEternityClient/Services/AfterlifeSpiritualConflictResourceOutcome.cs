using System.Collections.ObjectModel;
using System.Text.Json.Nodes;

namespace BookOfEternityClient.Services;

internal static partial class AfterlifeSpiritualConflictResourceOutcome
{
    private const string ResourceKey = "spiritual_action_points";
    private const string ConflictPath = AfterlifeSpiritualConflictState.StatePath;

    internal sealed record BuildResult(
        IResourceRegisteredSystemOutcomeDraft? Draft,
        IReadOnlyList<ValidationIssue> Issues)
    {
        internal bool IsValid => Issues.Count == 0;
        internal IReadOnlyList<ExchangeBatch> Exchanges { get; init; } = Array.Empty<ExchangeBatch>();
        internal IReadOnlyList<ContourRequirement> PendingRequirements { get; init; } = Array.Empty<ContourRequirement>();
    }

    /// <summary>
    /// Retains exact source-local resource evidence, with a relative Gain check only after a recovery wound payment.
    /// </summary>
    /// <param name="EventRef">
    /// Stable turn, exchange and side event identity.
    /// </param>
    /// <param name="SourceKind">
    /// Registered payment or outcome source family.
    /// </param>
    /// <param name="SourceId">
    /// Exact source exchange identity.
    /// </param>
    /// <param name="Coordinate">
    /// Exact resource owner and key whose transition must match.
    /// </param>
    /// <param name="Operation">
    /// Required resource operation.
    /// </param>
    /// <param name="Before">
    /// Action-only starting balance; for burdened recovery Gain this is the balance immediately after its payment.
    /// </param>
    /// <param name="After">
    /// Action-only final balance, excluding separate causal reactions.
    /// </param>
    /// <param name="Amount">
    /// Fixed requested amount; ordinary transitions must also apply exactly this amount.
    /// </param>
    /// <param name="RecoveryPayment">
    /// Positive preceding wound payment for reaction-relative recovery Gain; zero preserves absolute checks.
    /// </param>
    internal sealed record ExpectedTransition(
        string EventRef,
        string SourceKind,
        string SourceId,
        ResourceCoordinate Coordinate,
        ResourceTransitionOperation Operation,
        decimal Before,
        decimal After,
        decimal Amount,
        decimal RecoveryPayment = 0m);

    internal enum ContourRequirement
    {
        StartResourceInitialization,
        TerminalResourceClosure,
        ActiveConflictSourceBinding
    }

    internal enum SideEvaluation { MissingAudit, EvaluatedZero, EvaluatedMutation }

    // Resource-local data, not a B1 source capability or accepted wound proof.
    internal sealed partial class ExchangeBatch
    {
        private readonly ResourceMutationSourceExport[] _sources;
        private readonly ResourceMutationIntent[] _mutations;
        private readonly ExpectedTransition[] _expected;

        internal ExchangeBatch(
            string conflictId, string exchangeId, int ordinal,
            SideEvaluation player, SideEvaluation opposition,
            IEnumerable<ResourceMutationSourceExport> sources,
            IEnumerable<ResourceMutationIntent> mutations,
            IEnumerable<ExpectedTransition> expected)
        {
            ConflictId = conflictId;
            ExchangeId = exchangeId;
            Ordinal = ordinal;
            Player = player;
            Opposition = opposition;
            _sources = sources.ToArray();
            _mutations = mutations.Select(CloneMutation).ToArray();
            _expected = expected.ToArray();
        }

        internal string ConflictId { get; }
        internal string ExchangeId { get; }
        internal int Ordinal { get; }
        internal SideEvaluation Player { get; }
        internal SideEvaluation Opposition { get; }
        internal IReadOnlyList<ResourceMutationSourceExport> Sources =>
            Array.AsReadOnly(_sources.ToArray());
        internal IReadOnlyList<ResourceMutationIntent> Mutations =>
            Array.AsReadOnly(_mutations.Select(CloneMutation).ToArray());
        internal IReadOnlyList<string> PendingSides =>
            Array.AsReadOnly(new[] { ("player", Player), ("opposition", Opposition) }
                .Where(value => value.Item2 == SideEvaluation.MissingAudit)
                .Select(value => value.Item1).ToArray());

        internal IReadOnlyList<ValidationIssue> ValidateTransitions(
            IReadOnlyList<ResourceTransition> applied,
            IReadOnlyList<ResourceTransition> replay) =>
            ValidateExpectedTransitions(_expected, applied.Concat(replay).ToArray());

        private static ResourceMutationIntent CloneMutation(ResourceMutationIntent value) =>
            value with
            {
                Dependencies = value.Dependencies.ToArray(),
                EventRequirements = value.EventRequirements.ToArray()
            };
    }

    private static IReadOnlyList<ValidationIssue> ValidateExpectedTransitions(
        IReadOnlyList<ExpectedTransition> expectedTransitions,
        IReadOnlyList<ResourceTransition> transitions)
    {
        var issues = new List<ValidationIssue>();
        foreach (var expected in expectedTransitions)
        {
            var matches = transitions.Where(transition =>
                string.Equals(transition.EventRef, expected.EventRef, StringComparison.Ordinal) &&
                string.Equals(transition.OriginKind, expected.SourceKind, StringComparison.Ordinal) &&
                string.Equals(transition.OriginId, expected.SourceId, StringComparison.Ordinal) &&
                ResourceCoordinateComparer.Instance.Equals(transition.Coordinate, expected.Coordinate) &&
                transition.Operation == expected.Operation).ToArray();
            if (matches.Length != 1 ||
                matches[0].RequestedAmount != expected.Amount ||
                (expected.RecoveryPayment > 0m
                    ? !MatchesRecoveryAfterPayment(expected, matches[0], transitions)
                    : matches[0].BeforeState?.Current != expected.Before ||
                      matches[0].AfterState?.Current != expected.After ||
                      matches[0].AppliedAmount != expected.Amount))
            {
                Add(issues, ConflictPath + ".activeConflict.exchangeLog",
                    "afterlife_conflict_resource_transition_mismatch",
                    $"one exact {expected.Operation} transition {expected.Before}->{expected.After} amount {expected.Amount}",
                    matches.Length == 1
                        ? $"{matches[0].Operation} {matches[0].BeforeState?.Current}->{matches[0].AfterState?.Current} requested={matches[0].RequestedAmount} applied={matches[0].AppliedAmount}"
                        : $"matches={matches.Length}");
            }
        }
        return issues;
    }

    /// <summary>
    /// Checks a burdened recovery against the actual balance after payment-triggered effects,
    /// preserving the action's original requested gain and exact preceding payment.
    /// </summary>
    /// <param name="expected">
    /// The recovery gain with its positive wound payment and action-only post-payment balance.
    /// </param>
    /// <param name="gain">
    /// The uniquely matched gain transition from the resource owner.
    /// </param>
    /// <param name="transitions">
    /// Applied and replayed transitions containing the matching payment.
    /// </param>
    /// <returns>
    /// True when payment precedes the gain and the gain follows the ordinary maximum cap; otherwise false.
    /// </returns>
    private static bool MatchesRecoveryAfterPayment(
        ExpectedTransition expected,
        ResourceTransition gain,
        IReadOnlyList<ResourceTransition> transitions)
    {
        if (gain.Operation != ResourceTransitionOperation.Gain ||
            gain.BeforeState is not { } before || gain.AfterState is not { } after ||
            before.Current < 0m || before.Current > before.Maximum)
            return false;

        var payments = transitions.Where(value =>
            string.Equals(value.EventRef, expected.EventRef, StringComparison.Ordinal) &&
            string.Equals(value.OriginKind, "afterlife_cost", StringComparison.Ordinal) &&
            string.Equals(value.OriginId, expected.SourceId, StringComparison.Ordinal) &&
            ResourceCoordinateComparer.Instance.Equals(value.Coordinate, expected.Coordinate) &&
            value.Operation == ResourceTransitionOperation.Spend).ToArray();
        if (payments.Length != 1 || payments[0].Turn != gain.Turn ||
            payments[0].ExecutionSequence >= gain.ExecutionSequence ||
            payments[0].BeforeState?.Current != expected.Before + expected.RecoveryPayment ||
            payments[0].AfterState?.Current != expected.Before ||
            payments[0].RequestedAmount != expected.RecoveryPayment ||
            payments[0].AppliedAmount != expected.RecoveryPayment)
            return false;

        var applied = Math.Min(expected.Amount, before.Maximum - before.Current);
        return gain.AppliedAmount == applied &&
            after == before with { Current = before.Current + applied } &&
            gain.Outcome == (applied < expected.Amount
                ? ResourceTransitionOutcome.ClampedMaximum : ResourceTransitionOutcome.Applied);
    }

    internal static bool IsConflictOutcome(IResourceRegisteredSystemOutcomeDraft outcome) =>
        outcome is Draft;

    private sealed class Draft : IResourceRegisteredSystemOutcomeDraft
    {
        private readonly ResourceMutationSourceExport[] _sources;
        private readonly ResourceMutationIntent[] _mutations;
        private readonly ExpectedTransition[] _expectedTransitions;

        internal Draft(
            string fingerprint,
            IReadOnlyList<ResourceMutationSourceExport> sources,
            IReadOnlyList<ResourceMutationIntent> mutations,
            IReadOnlyList<ExpectedTransition> expectedTransitions)
        {
            Fingerprint = fingerprint;
            _sources = sources.ToArray();
            _mutations = mutations.ToArray();
            _expectedTransitions = expectedTransitions.ToArray();
        }

        public string Fingerprint { get; }

        public IReadOnlyList<ResourceMutationSourceExport> SourceExports =>
            Array.AsReadOnly(_sources.ToArray());

        public IReadOnlyList<ResourceMutationIntent> Mutations =>
            Array.AsReadOnly(_mutations.ToArray());

        public IReadOnlyDictionary<string, CanonicalBeforeImage> ExpectedBeforeImages =>
            new ReadOnlyDictionary<string, CanonicalBeforeImage>(
                new Dictionary<string, CanonicalBeforeImage>(StringComparer.Ordinal));

        public ResourceRegisteredSystemOutcomeProjectionResult Project(
            AcceptedMechanicsResourcePlanningResult resourceResult)
        {
            ArgumentNullException.ThrowIfNull(resourceResult);
            var issues = ValidateExpectedTransitions(_expectedTransitions,
                resourceResult.AppliedTransitions.Concat(resourceResult.ReplayTransitions).ToArray());

            return new ResourceRegisteredSystemOutcomeProjectionResult(
                new Dictionary<string, JsonObject>(StringComparer.Ordinal),
                Array.Empty<AcceptedMechanicsOwnerTransition>(),
                issues);
        }
    }

    /// <summary>
    /// Composes resource-local exchange batches while preserving original producer provenance.
    /// </summary>
    /// <param name="turn">
    /// Positive current turn number.
    /// </param>
    /// <param name="preTurnRoot">
    /// Unchanged original conflict root.
    /// </param>
    /// <param name="acceptedRoot">
    /// Checked current candidate preserving its original exchange prefix.
    /// </param>
    /// <param name="owners">
    /// Registered resource-owner authority used for exact coordinates.
    /// </param>
    /// <param name="state">
    /// Original accepted resource-prefix ledger; never replaced by an exchange-local image.
    /// </param>
    /// <param name="ownedFrontier">
    /// Actual exchange-start context for the retained path; null preserves strict whole-log composition.
    /// </param>
    /// <param name="retainedPrefix">
    /// Actual complete producer batches preceding the owned ordinal; required only with an owned frontier.
    /// </param>
    /// <param name="terminal">
    /// Capture-issued direct terminal preparation, or <see langword="null"/> for the ordinary active contour.
    /// </param>
    /// <returns>
    /// Prepared batches, pending contour requirements or diagnostics; no resource state is mutated.
    /// </returns>
    internal static BuildResult TryCreate(
        int turn,
        JsonObject preTurnRoot,
        JsonObject acceptedRoot,
        ResourceOwnerAuthority owners,
        ResourceStateLedger state,
        AcceptedMechanicsPlanner.ResourceExecutionSession.SpiritualMechanicsContext? ownedFrontier = null,
        IReadOnlyList<ExchangeBatch>? retainedPrefix = null,
        ValidationService.SpiritualOriginalTurnCapture.PreparedTerminal? terminal = null)
    {
        ArgumentNullException.ThrowIfNull(preTurnRoot);
        ArgumentNullException.ThrowIfNull(acceptedRoot);
        ArgumentNullException.ThrowIfNull(owners);
        ArgumentNullException.ThrowIfNull(state);
        var issues = new List<ValidationIssue>();
        if (turn <= 0)
        {
            Add(
                issues,
                ConflictPath,
                "afterlife_conflict_resource_turn_invalid",
                "positive accepted turn",
                turn.ToString());
            return new BuildResult(null, issues);
        }

        var preTurnConflict = preTurnRoot["activeConflict"] as JsonObject;
        var acceptedConflict = acceptedRoot["activeConflict"] as JsonObject;
        JsonObject? terminalResolution = null;
        if (terminal != null)
        {
            if (!terminal.Matches(preTurnRoot, acceptedRoot) || !ReferenceEquals(owners, terminal.ExecutionOwners) ||
                !ValidationService.SpiritualWoundSourceSession.TryReadSingleTerminal(preTurnRoot, acceptedRoot,
                    out terminalResolution))
                throw new InvalidOperationException("Exact signed terminal preparation and execution authority required.");
            // Owner metadata is signed A; exchange data remains the real terminal B witness.
            acceptedConflict = preTurnConflict;
        }
        if (preTurnConflict == null || acceptedConflict == null)
        {
            var requirement = preTurnRoot["activeConflict"] is not JsonObject &&
                              acceptedRoot["activeConflict"] is JsonObject
                ? ContourRequirement.StartResourceInitialization
                : preTurnRoot["activeConflict"] is JsonObject &&
                  acceptedRoot["activeConflict"] is not JsonObject
                    ? ContourRequirement.TerminalResourceClosure
                    : ContourRequirement.ActiveConflictSourceBinding;
            return new BuildResult(null, Array.Empty<ValidationIssue>())
            {
                PendingRequirements = Array.AsReadOnly(new[] { requirement })
            };
        }

        var conflictId = ReadExact(acceptedConflict["conflictId"]);
        var preTurnConflictId = ReadExact(preTurnConflict["conflictId"]);
        var realm = ReadRealm(acceptedConflict["realm"]);
        if (conflictId == null || preTurnConflictId == null || realm == null ||
            !string.Equals(conflictId, preTurnConflictId, StringComparison.Ordinal))
        {
            Add(
                issues,
                ConflictPath + ".activeConflict",
                "afterlife_conflict_resource_identity_mismatch",
                "one exact continuing conflict identity and realm",
                $"before={preTurnConflictId ?? "missing"};after={conflictId ?? "missing"};realm={realm ?? "missing"}");
            return new BuildResult(null, issues);
        }

        var beforeLog = preTurnConflict["exchangeLog"] as JsonArray ?? new JsonArray();
        var afterLog = terminalResolution == null
            ? acceptedConflict["exchangeLog"] as JsonArray ?? new JsonArray()
            : new JsonArray(beforeLog.Select(row => row?.DeepClone())
                .Append(terminalResolution["terminalExchange"]!.DeepClone()).ToArray());
        if (afterLog.Count < beforeLog.Count)
        {
            Add(
                issues,
                ConflictPath + ".activeConflict.exchangeLog",
                "afterlife_conflict_resource_log_truncated",
                "accepted exchange log preserving the validated pre-turn prefix",
                $"before={beforeLog.Count};after={afterLog.Count}");
            return new BuildResult(null, issues);
        }
        for (var index = 0; index < beforeLog.Count; index++)
        {
            if (JsonNode.DeepEquals(beforeLog[index], afterLog[index]))
                continue;
            Add(
                issues,
                $"{ConflictPath}.activeConflict.exchangeLog[{index}]",
                "afterlife_conflict_resource_log_prefix_mutated",
                "byte-semantic validated pre-turn exchange entry",
                afterLog[index]?.ToJsonString() ?? "null");
        }
        if (issues.Count != 0)
            return new BuildResult(null, issues);

        if (!TryResolveCoordinate(
                owners,
                state,
                new ResourceOwnerKey(realm, ResourceOwnerKind.AfterlifeActor, "player_soul"),
                out var playerCoordinate,
                out var playerCurrent,
                out var playerMaximum,
                issues))
        {
            return new BuildResult(null, issues);
        }
        var oppositionOwnerId = ReadExact(
            acceptedConflict[AfterlifeEntityProfileState.ResourceOwnerBindingsProperty]?
                ["opposition"]?["resourceOwnerId"]);
        if (oppositionOwnerId == null ||
            !TryResolveCoordinate(
                owners,
                state,
                new ResourceOwnerKey(
                    realm,
                    ResourceOwnerKind.AfterlifeConflictSide,
                    oppositionOwnerId ?? string.Empty),
                out var oppositionCoordinate,
                out var oppositionCurrent,
                out var oppositionMaximum,
                issues))
        {
            if (oppositionOwnerId == null)
            {
                Add(
                    issues,
                    ConflictPath + ".activeConflict.resourceOwnerBindings.opposition",
                    "afterlife_conflict_resource_opposition_owner_missing",
                    "exact client-owned opposition resource owner binding",
                    "missing");
            }
            return new BuildResult(null, issues);
        }

        var firstIndex = beforeLog.Count;
        var lastIndex = afterLog.Count;
        ResourceOperationKey? retainedPlayer = null;
        ResourceOperationKey? retainedOpposition = null;
        if (ownedFrontier != null)
        {
            var frontier = ownedFrontier.ReadActionPointBefore(conflictId, ownedFrontier.Ordinal);
            if (frontier.Player.Coordinate != playerCoordinate || frontier.Opposition.Coordinate != oppositionCoordinate ||
                frontier.Player.Maximum != playerMaximum || frontier.Opposition.Maximum != oppositionMaximum ||
                retainedPrefix == null || retainedPrefix.Count != ownedFrontier.Ordinal)
                throw new InvalidOperationException("Exact owned resource frontier and complete retained producer prefix required.");
            for (var ordinal = 0; ordinal < retainedPrefix.Count; ordinal++)
            {
                var retained = retainedPrefix[ordinal];
                if (retained.Ordinal != ordinal || retained.ConflictId != conflictId || retained.PendingSides.Count != 0 ||
                    !ProducerImages.TryGetValue(retained, out var image) || image.Index != beforeLog.Count + ordinal ||
                    image.Context.Frontier == null || image.Context.Frontier.Ordinal != ordinal ||
                    !ownedFrontier.HasSameOwner(image.Context.Frontier) ||
                    image.Context.Turn != turn || image.Context.Owners != owners.Fingerprint || image.Context.State != state.Fingerprint ||
                    !JsonNode.DeepEquals(image.Context.Before, preTurnRoot) || image.Index >= afterLog.Count ||
                    !JsonNode.DeepEquals(image.Context.Candidate["activeConflict"]!["exchangeLog"]![image.Index], afterLog[image.Index]))
                    throw new InvalidOperationException("Unchanged actual producer batches must precede the owned resource frontier.");
                foreach (var mutation in retained.Mutations)
                {
                    if (mutation.Coordinate == playerCoordinate)
                        retainedPlayer = mutation.Key;
                    else if (mutation.Coordinate == oppositionCoordinate)
                        retainedOpposition = mutation.Key;
                }
            }
            playerCurrent = frontier.Player.Current;
            oppositionCurrent = frontier.Opposition.Current;
            firstIndex += ownedFrontier.Ordinal;
            lastIndex = Math.Min(afterLog.Count, firstIndex + 1);
        }
        var producerContext = new ProducerContext(turn, preTurnRoot.DeepClone().AsObject(),
            acceptedRoot.DeepClone().AsObject(), owners.Fingerprint, state.Fingerprint, ownedFrontier);
        var batches = new List<ExchangeBatch>();
        var sourceExports = new List<ResourceMutationSourceExport>();
        var mutations = new List<ResourceMutationIntent>();
        var expected = new List<ExpectedTransition>();
        var sourceKeys = new HashSet<string>(StringComparer.Ordinal);
        var exchangeIds = new HashSet<string>(StringComparer.Ordinal);
        var exchangeAliases = new HashSet<string>(StringComparer.Ordinal);
        if (ownedFrontier != null)
            foreach (var retained in retainedPrefix!)
            {
                if (!exchangeIds.Add(retained.ExchangeId) ||
                    !exchangeAliases.Add(ResourceMaterializationContract.BuildConfusableKey(retained.ExchangeId)))
                    throw new InvalidOperationException("Retained producer prefix has ambiguous exchange identities.");
            }
        ResourceOperationKey? priorPlayer = retainedPlayer;
        ResourceOperationKey? priorOpposition = retainedOpposition;

        for (var index = firstIndex; index < lastIndex; index++)
        {
            var path = $"{ConflictPath}.activeConflict.exchangeLog[{index}]";
            if (afterLog[index] is not JsonObject exchange ||
                ReadExact(exchange["exchangeId"]) is not { } exchangeId)
            {
                Add(
                    issues,
                    path + ".exchangeId",
                    "afterlife_conflict_resource_exchange_identity_invalid",
                    "exact exchange identity",
                    afterLog[index]?.ToJsonString() ?? "null");
                continue;
            }
            if (!exchangeIds.Add(exchangeId) ||
                !exchangeAliases.Add(ResourceMaterializationContract.BuildConfusableKey(exchangeId)))
            {
                Add(
                    issues,
                    path + ".exchangeId",
                    "afterlife_conflict_resource_exchange_identity_ambiguous",
                    "exact/confusable-unique accepted exchange identity",
                    exchangeId);
                continue;
            }
            var audit = exchange["actionCostAudit"] as JsonObject;
            if (exchange.ContainsKey("actionCostAudit") && audit == null)
            {
                Add(issues, path + ".actionCostAudit", "afterlife_conflict_resource_audit_invalid",
                    "an audit object or a genuinely absent free-action audit", exchange["actionCostAudit"]?.ToJsonString() ?? "null");
                continue;
            }

            var sourceStart = sourceExports.Count;
            var mutationStart = mutations.Count;
            var expectedStart = expected.Count;

            using var sourceFingerprintBuilder = new ResourceFingerprintBuilder(
                "afterlife-conflict-resource-outcome-v1");
            sourceFingerprintBuilder.Append(turn);
            sourceFingerprintBuilder.Append(conflictId);
            sourceFingerprintBuilder.Append(realm);
            sourceFingerprintBuilder.Append(exchangeId);
            sourceFingerprintBuilder.Append(exchange.ToJsonString());
            sourceFingerprintBuilder.Append(owners.Fingerprint);
            var sourceFingerprint = sourceFingerprintBuilder.Build();

            var playerEvaluation = ComposeSide(
                "player",
                audit?["player"],
                exchangeId,
                path,
                playerCoordinate!,
                playerMaximum,
                ref playerCurrent,
                ref priorPlayer,
                sourceFingerprint,
                sourceKeys,
                sourceExports,
                mutations,
                expected,
                issues,
                turn,
                audit?.ContainsKey("player") != true && ValidationService.IsOwnedUnburdenedForceAction(
                    ownedFrontier, acceptedConflict, exchange, "player"));
            var oppositionEvaluation = ComposeSide(
                "opposition",
                audit?["opposition"],
                exchangeId,
                path,
                oppositionCoordinate!,
                oppositionMaximum,
                ref oppositionCurrent,
                ref priorOpposition,
                sourceFingerprint,
                sourceKeys,
                sourceExports,
                mutations,
                expected,
                issues,
                turn,
                audit?.ContainsKey("opposition") != true && ValidationService.IsOwnedUnburdenedForceAction(
                    ownedFrontier, acceptedConflict, exchange, "opposition"));
            var producedBatch = new ExchangeBatch(conflictId, exchangeId, index - beforeLog.Count,
                playerEvaluation, oppositionEvaluation, sourceExports.Skip(sourceStart),
                mutations.Skip(mutationStart), expected.Skip(expectedStart));
            ProducerImages.Add(producedBatch, new ProducerImage(producerContext, index));
            batches.Add(producedBatch);
        }

        if (issues.Count != 0 || mutations.Count == 0)
        {
            return new BuildResult(
                null,
                issues.Count == 0 ? Array.Empty<ValidationIssue>() : issues)
            {
                Exchanges = issues.Count == 0 ? Array.AsReadOnly(batches.ToArray()) : Array.Empty<ExchangeBatch>()
            };
        }

        using var draftFingerprint = new ResourceFingerprintBuilder(
            "afterlife-conflict-resource-draft-v1");
        draftFingerprint.Append(turn);
        draftFingerprint.Append(conflictId);
        draftFingerprint.Append(realm);
        draftFingerprint.Append(state.Fingerprint);
        foreach (var source in sourceExports
                     .OrderBy(static value => value.SourceKind, StringComparer.Ordinal)
                     .ThenBy(static value => value.SourceId, StringComparer.Ordinal))
        {
            draftFingerprint.Append(source.SourceKind);
            draftFingerprint.Append(source.SourceId);
            draftFingerprint.Append(source.AuthorityFingerprint);
        }
        return new BuildResult(
            new Draft(
                draftFingerprint.Build(),
                sourceExports,
                mutations,
                expected),
            Array.Empty<ValidationIssue>()) { Exchanges = Array.AsReadOnly(batches.ToArray()) };
    }

    /// <summary>
    /// Composes one side's payment and recovery intents without mutating the resource ledger.
    /// </summary>
    /// <param name="side">
    /// Exact player or opposition side used in event identity and diagnostics.
    /// </param>
    /// <param name="auditNode">
    /// Validated action audit, or <see langword="null"/> for absent evidence.
    /// </param>
    /// <param name="exchangeId">
    /// Stable original source exchange identity.
    /// </param>
    /// <param name="exchangePath">
    /// Exchange diagnostic path.
    /// </param>
    /// <param name="coordinate">
    /// Authenticated resource owner and key.
    /// </param>
    /// <param name="maximum">
    /// Registered current action-point maximum.
    /// </param>
    /// <param name="current">
    /// Expected incoming balance, replaced by the action-only audit result after validation.
    /// </param>
    /// <param name="prior">
    /// Previous operation dependency, advanced only when a nonzero mutation is composed.
    /// </param>
    /// <param name="sourceFingerprint">
    /// Authority fingerprint of this original exchange and its resource owners.
    /// </param>
    /// <param name="sourceKeys">
    /// Existing source-family and exchange identities used to avoid duplicate exports.
    /// </param>
    /// <param name="sourceExports">
    /// Destination for newly required registered sources.
    /// </param>
    /// <param name="mutations">
    /// Destination for ordered Spend and Gain requests, excluding zero amounts.
    /// </param>
    /// <param name="expected">
    /// Destination for exact payment and ordinary or reaction-relative recovery expectations.
    /// </param>
    /// <param name="issues">
    /// Destination for invalid shape, balance or arithmetic diagnostics.
    /// </param>
    /// <param name="turn">
    /// Current accepted turn used in stable event identity.
    /// </param>
    /// <param name="ownedFreeForce">
    /// Whether the actual current owner proved this absent force audit has zero applicable wound burden.
    /// </param>
    /// <returns>
    /// Missing evidence, an owner-proven zero action, or a side with composed mutations.
    /// </returns>
    private static SideEvaluation ComposeSide(
        string side,
        JsonNode? auditNode,
        string exchangeId,
        string exchangePath,
        ResourceCoordinate coordinate,
        decimal maximum,
        ref decimal current,
        ref ResourceOperationKey? prior,
        string sourceFingerprint,
        HashSet<string> sourceKeys,
        List<ResourceMutationSourceExport> sourceExports,
        List<ResourceMutationIntent> mutations,
        List<ExpectedTransition> expected,
        List<ValidationIssue> issues,
        int turn,
        bool ownedFreeForce)
    {
        if (auditNode == null)
            return ownedFreeForce ? SideEvaluation.EvaluatedZero : SideEvaluation.MissingAudit;
        var path = exchangePath + ".actionCostAudit." + side;
        if (auditNode is not JsonObject audit ||
            ReadExact(audit["operationType"]) is not { } operationType ||
            !TryReadDecimal(audit["before"], out var before) ||
            !TryReadDecimal(audit["after"], out var after) ||
            !TryReadDecimal(audit["effectiveCost"], out var effectiveCost))
        {
            Add(
                issues,
                path,
                "afterlife_conflict_resource_audit_invalid",
                "exact operationType, effectiveCost, before, and after",
                auditNode.ToJsonString());
            return SideEvaluation.MissingAudit;
        }
        if (before != current || after < 0m || after > maximum)
        {
            Add(
                issues,
                path,
                "afterlife_conflict_resource_audit_state_mismatch",
                $"ledger sequence before={current} and after within 0..{maximum}",
                $"before={before};after={after}");
            return SideEvaluation.MissingAudit;
        }

        var recovery = string.Equals(
            operationType,
            "recover_spiritual_power",
            StringComparison.OrdinalIgnoreCase);
        if (effectiveCost < 0m || recovery && effectiveCost > before)
        {
            Add(issues, path, "afterlife_conflict_resource_audit_delta_mismatch",
                "non-negative affordable payment before recovery", $"effectiveCost={effectiveCost};before={before}");
            return SideEvaluation.MissingAudit;
        }
        var postPayment = before - effectiveCost;
        var amount = recovery ? after - postPayment : effectiveCost;
        if (amount < 0m || !recovery && after != postPayment)
        {
            Add(
                issues,
                path,
                "afterlife_conflict_resource_audit_delta_mismatch",
                recovery
                    ? "non-negative recovery with after = before - effectiveCost + recovered amount"
                    : "positive action cost with after = before - effectiveCost",
                $"effectiveCost={effectiveCost};before={before};after={after}");
            return SideEvaluation.MissingAudit;
        }
        current = after;
        var mutationStart = mutations.Count;
        var nextPrior = prior;
        var eventRef = $"turn_{turn}:afterlife_conflict:{exchangeId}:{side}";
        if (recovery)
        {
            AppendMutation(ResourceOperation.Spend, effectiveCost, before, postPayment);
            AppendMutation(ResourceOperation.Gain, amount, postPayment, after);
        }
        else
            AppendMutation(ResourceOperation.Spend, amount, before, after);
        prior = nextPrior;
        return mutations.Count == mutationStart ? SideEvaluation.EvaluatedZero : SideEvaluation.EvaluatedMutation;

        void AppendMutation(ResourceOperation operation, decimal quantity, decimal from, decimal to)
        {
            if (quantity == 0m)
                return;
            var sourceKind = operation == ResourceOperation.Gain ? "afterlife_outcome" : "afterlife_cost";
            if (sourceKeys.Add(sourceKind + "\0" + exchangeId))
                sourceExports.Add(new ResourceMutationSourceExport(sourceKind, exchangeId, sourceFingerprint,
                    ResourceMutationSourceState.Active, SameTurn: true));
            var intent = new ResourceMutationIntent(eventRef, coordinate, quantity,
                new ResourceMutationSourceRequest(sourceKind, exchangeId, operation),
                nextPrior == null ? Array.Empty<ResourceOperationKey>() : new[] { nextPrior },
                Array.Empty<ResourceMutationEventRequirement>(), ReceiptId: null);
            mutations.Add(intent);
            expected.Add(new ExpectedTransition(eventRef, sourceKind, exchangeId, coordinate,
                operation == ResourceOperation.Gain ? ResourceTransitionOperation.Gain : ResourceTransitionOperation.Spend,
                from, to, quantity,
                recovery && operation == ResourceOperation.Gain ? effectiveCost : 0m));
            nextPrior = intent.Key;
        }
    }

    private static bool TryResolveCoordinate(
        ResourceOwnerAuthority owners,
        ResourceStateLedger state,
        ResourceOwnerKey key,
        out ResourceCoordinate? coordinate,
        out decimal current,
        out decimal maximum,
        List<ValidationIssue> issues)
    {
        coordinate = null;
        current = 0m;
        maximum = 0m;
        if (!owners.Entries.TryGetValue(key, out var owner) ||
            owner.Lifecycle != ResourceOwnerLifecycle.Active ||
            !owner.ResourceCapabilities.Contains(ResourceKey))
        {
            Add(
                issues,
                ConflictPath + ".activeConflict",
                "afterlife_conflict_resource_owner_unresolved",
                $"active {key.OwnerKind} owner {key.ResourceOwnerId} with {ResourceKey}",
                "missing or unauthorized");
            return false;
        }
        coordinate = new ResourceCoordinate(
            key.Realm,
            key.OwnerKind,
            key.ResourceOwnerId,
            ResourceKey);
        if (!state.TryResolveExact(coordinate, out var entry) ||
            entry == null ||
            entry.State != ResourceLifecycleState.Active)
        {
            Add(
                issues,
                ResourceMaterializationContract.StatePath,
                "afterlife_conflict_resource_state_unresolved",
                $"active canonical resource coordinate {key.Realm}/{key.OwnerKind}/{key.ResourceOwnerId}/{ResourceKey}",
                "missing or inactive");
            return false;
        }
        current = entry.Current;
        maximum = entry.Maximum;
        return true;
    }

    private static string? ReadExact(JsonNode? node)
    {
        if (node is not JsonValue value ||
            !value.TryGetValue<string>(out var text) ||
            string.IsNullOrWhiteSpace(text))
        {
            return null;
        }
        var exact = text.Trim();
        return ResourceMaterializationContract.IsExactIdentifier(exact) ? exact : null;
    }

    private static string? ReadRealm(JsonNode? node)
    {
        var raw = node is JsonValue value && value.TryGetValue<string>(out var text)
            ? text
            : null;
        return AfterlifeEntityProfileState.TryNormalizeEffectRealm(raw, out var realm)
            ? realm
            : null;
    }

    private static bool TryReadDecimal(JsonNode? node, out decimal result)
    {
        result = 0m;
        return node is JsonValue value &&
               (value.TryGetValue<decimal>(out result) ||
                value.TryGetValue<int>(out var integer) && (result = integer) == integer);
    }

    private static void Add(
        List<ValidationIssue> issues,
        string path,
        string code,
        string expected,
        string actual) =>
        ResourceMaterializationContract.AddIssue(
            issues,
            path,
            code,
            expected,
            actual);
}
