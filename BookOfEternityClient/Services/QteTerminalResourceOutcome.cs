using System.Collections.ObjectModel;
using System.Text.Json.Nodes;
using BookOfEternityClient.Core;

namespace BookOfEternityClient.Services;

internal sealed class QteTerminalResourceSelection
{
    private readonly JsonObject _responseFragment;

    internal QteTerminalResourceSelection(
        int sourceTurn,
        string qteId,
        string chapterId,
        string actionId,
        string grade,
        int outcomeOrdinal,
        string outcomeId,
        JsonObject? responseFragment)
    {
        if (sourceTurn <= 0)
            throw new ArgumentOutOfRangeException(nameof(sourceTurn));
        if (outcomeOrdinal <= 0)
            throw new ArgumentOutOfRangeException(nameof(outcomeOrdinal));
        SourceTurn = sourceTurn;
        QteId = qteId ?? throw new ArgumentNullException(nameof(qteId));
        ChapterId = chapterId ?? throw new ArgumentNullException(nameof(chapterId));
        ActionId = actionId ?? throw new ArgumentNullException(nameof(actionId));
        Grade = grade ?? throw new ArgumentNullException(nameof(grade));
        OutcomeOrdinal = outcomeOrdinal;
        OutcomeId = outcomeId ?? throw new ArgumentNullException(nameof(outcomeId));
        _responseFragment = responseFragment?.DeepClone().AsObject() ?? new JsonObject();
    }

    internal int SourceTurn { get; }
    internal string QteId { get; }
    internal string ChapterId { get; }
    internal string ActionId { get; }
    internal string Grade { get; }
    internal int OutcomeOrdinal { get; }
    internal string OutcomeId { get; }
    internal JsonObject ResponseFragment => _responseFragment.DeepClone().AsObject();
}

internal sealed record QteTerminalResourceWorkStatistics(
    long DefinitionAuthorityLookupCount,
    long DefinitionBindingLookupCount,
    long ExistingReplayTransitionVisitCount,
    long ExistingReplayIdentityLookupCount,
    long ProjectionTransitionVisitCount,
    long ProjectionIdentityLookupCount,
    long ProjectionCardinalityLookupCount)
{
    internal static QteTerminalResourceWorkStatistics Empty { get; } = new(
        DefinitionAuthorityLookupCount: 0,
        DefinitionBindingLookupCount: 0,
        ExistingReplayTransitionVisitCount: 0,
        ExistingReplayIdentityLookupCount: 0,
        ProjectionTransitionVisitCount: 0,
        ProjectionIdentityLookupCount: 0,
        ProjectionCardinalityLookupCount: 0);

    internal long TotalWorkUnits =>
        DefinitionAuthorityLookupCount +
        DefinitionBindingLookupCount +
        ExistingReplayTransitionVisitCount +
        ExistingReplayIdentityLookupCount +
        ProjectionTransitionVisitCount +
        ProjectionIdentityLookupCount +
        ProjectionCardinalityLookupCount;
}

internal sealed class QteTerminalResourceFilePlan
{
    private readonly CoordinatedStateWriteHelper.PlannedWrite[] _writes;
    private readonly ValidationIssue[] _issues;

    internal QteTerminalResourceFilePlan(
        bool hasResourceChanges,
        string? fingerprint,
        IReadOnlyList<CoordinatedStateWriteHelper.PlannedWrite> writes,
        IReadOnlyList<ValidationIssue> issues,
        QteTerminalResourceWorkStatistics? work = null)
    {
        HasResourceChanges = hasResourceChanges;
        Fingerprint = fingerprint;
        _writes = (writes ?? throw new ArgumentNullException(nameof(writes))).ToArray();
        _issues = (issues ?? throw new ArgumentNullException(nameof(issues))).ToArray();
        Work = work ?? QteTerminalResourceWorkStatistics.Empty;
    }

    internal bool HasResourceChanges { get; }
    internal string? Fingerprint { get; }
    internal IReadOnlyList<CoordinatedStateWriteHelper.PlannedWrite> Writes =>
        Array.AsReadOnly(_writes.ToArray());
    internal IReadOnlyList<ValidationIssue> Issues =>
        Array.AsReadOnly(_issues.ToArray());
    internal QteTerminalResourceWorkStatistics Work { get; }
    internal bool IsValid => _issues.Length == 0;
}

internal sealed class QteTerminalResourcePlanningDraft
{
    private readonly ValidationIssue[] _issues;

    internal QteTerminalResourcePlanningDraft(
        bool hasResourceChanges,
        ResourceMutationSourceCatalog? sources,
        IResourceRegisteredSystemOutcomeDraft? registeredOutcome,
        int executionSequenceOffset,
        IReadOnlyList<ValidationIssue> issues,
        QteTerminalResourceWorkStatistics work)
    {
        if (executionSequenceOffset < 0)
            throw new ArgumentOutOfRangeException(nameof(executionSequenceOffset));
        HasResourceChanges = hasResourceChanges;
        Sources = sources;
        RegisteredOutcome = registeredOutcome;
        ExecutionSequenceOffset = executionSequenceOffset;
        _issues = (issues ?? throw new ArgumentNullException(nameof(issues))).ToArray();
        Work = work ?? throw new ArgumentNullException(nameof(work));
    }

    internal bool HasResourceChanges { get; }
    internal ResourceMutationSourceCatalog? Sources { get; }
    internal IResourceRegisteredSystemOutcomeDraft? RegisteredOutcome { get; }
    internal int ExecutionSequenceOffset { get; }
    internal IReadOnlyList<ValidationIssue> Issues =>
        Array.AsReadOnly(_issues.ToArray());
    internal QteTerminalResourceWorkStatistics Work { get; }
    internal bool IsValid => _issues.Length == 0;
}

/// <summary>
/// Adapts one selected QTE terminal response into the common resource planner.
/// QTE never publishes transient resource commands and never owns a second reducer.
/// </summary>
internal static class QteTerminalResourceOutcome
{
    private const string Realm = "mortal_world";
    private const string PlayerId = "player_current";
    private const string SourceKind = "narrative_outcome";
    private const string QtePath = QteSceneService.QteOfferPath;

    private sealed record ExpectedTransition(
        int Turn,
        int CommandOrdinal,
        string EventRef,
        ResourceCoordinate Coordinate,
        decimal Amount,
        ResourceAuthorizedSourceRoute Route);

    private sealed record ResolvedCommand(
        ResourceOrdinaryCommand Command,
        ResourceOwnerAuthorityEntry Owner,
        ResourceCoordinate Coordinate,
        ResourceDefinition Definition);

    private readonly record struct TransitionIdentity(
        string EventRef,
        string OriginKind,
        string OriginId,
        ResourceCoordinate Coordinate,
        ResourceTransitionOperation Operation);

    private readonly record struct IndexedTransition(
        ResourceTransition First,
        int Count);

    private sealed class WorkMeter
    {
        internal long DefinitionAuthorityLookupCount { get; set; }
        internal long DefinitionBindingLookupCount { get; set; }
        internal long ExistingReplayTransitionVisitCount { get; set; }
        internal long ExistingReplayIdentityLookupCount { get; set; }
        internal long ProjectionTransitionVisitCount { get; set; }
        internal long ProjectionIdentityLookupCount { get; set; }
        internal long ProjectionCardinalityLookupCount { get; set; }

        internal QteTerminalResourceWorkStatistics Snapshot() => new(
            DefinitionAuthorityLookupCount,
            DefinitionBindingLookupCount,
            ExistingReplayTransitionVisitCount,
            ExistingReplayIdentityLookupCount,
            ProjectionTransitionVisitCount,
            ProjectionIdentityLookupCount,
            ProjectionCardinalityLookupCount);
    }

    private sealed class Draft : IResourceRegisteredSystemOutcomeDraft
    {
        private readonly ResourceMutationSourceExport[] _sources;
        private readonly ResourceMutationIntent[] _mutations;
        private readonly ExpectedTransition[] _expected;
        private readonly WorkMeter _work;

        internal Draft(
            string fingerprint,
            IReadOnlyList<ResourceMutationSourceExport> sources,
            IReadOnlyList<ResourceMutationIntent> mutations,
            IReadOnlyList<ExpectedTransition> expected,
            WorkMeter work)
        {
            Fingerprint = fingerprint;
            _sources = sources.Select(CloneSource).ToArray();
            _mutations = mutations.Select(CloneMutation).ToArray();
            _expected = expected.ToArray();
            _work = work ?? throw new ArgumentNullException(nameof(work));
        }

        public string Fingerprint { get; }

        public IReadOnlyList<ResourceMutationSourceExport> SourceExports =>
            Array.AsReadOnly(_sources.Select(CloneSource).ToArray());

        public IReadOnlyList<ResourceMutationIntent> Mutations =>
            Array.AsReadOnly(_mutations.Select(CloneMutation).ToArray());

        public IReadOnlyDictionary<string, CanonicalBeforeImage> ExpectedBeforeImages =>
            new ReadOnlyDictionary<string, CanonicalBeforeImage>(
                new Dictionary<string, CanonicalBeforeImage>(StringComparer.Ordinal));

        public ResourceRegisteredSystemOutcomeProjectionResult Project(
            AcceptedMechanicsResourcePlanningResult resourceResult)
        {
            ArgumentNullException.ThrowIfNull(resourceResult);
            var issues = new List<ValidationIssue>();
            var transitions = resourceResult.AppliedTransitions
                .Concat(resourceResult.ReplayTransitions)
                .ToArray();
            var expectedRefs = _expected
                .Select(static expected => expected.EventRef)
                .ToHashSet(StringComparer.Ordinal);
            var transitionsByIdentity = new Dictionary<
                TransitionIdentity,
                IndexedTransition>();
            var ownedCountsByOriginId = new Dictionary<string, int>(
                StringComparer.Ordinal);
            foreach (var transition in transitions)
            {
                _work.ProjectionTransitionVisitCount++;
                if (string.Equals(
                        transition.OriginKind,
                        SourceKind,
                        StringComparison.Ordinal))
                {
                    AddIndexedTransition(transitionsByIdentity, transition);
                    if (expectedRefs.Contains(transition.OriginId))
                    {
                        ownedCountsByOriginId.TryGetValue(
                            transition.OriginId,
                            out var count);
                        ownedCountsByOriginId[transition.OriginId] = count + 1;
                    }
                }
            }

            foreach (var expected in _expected)
            {
                _work.ProjectionIdentityLookupCount++;
                transitionsByIdentity.TryGetValue(
                    ExpectedIdentity(expected),
                    out var indexed);
                if (indexed.Count != 1 ||
                    !TransitionSemanticsEqual(indexed.First, expected))
                {
                    Add(
                        issues,
                        ResourceMaterializationContract.HistoryPath,
                        "qte_terminal_resource_projection_mismatch",
                        "one exact applied-or-replay projection for every selected QTE damage command",
                        indexed.Count == 1
                            ? Describe(indexed.First)
                            : $"eventRef={expected.EventRef};matches={indexed.Count}");
                }
            }

            var ownedTransitions = 0;
            foreach (var expected in _expected)
            {
                _work.ProjectionCardinalityLookupCount++;
                if (ownedCountsByOriginId.TryGetValue(
                        expected.EventRef,
                        out var count))
                {
                    ownedTransitions += count;
                }
            }
            if (ownedTransitions != _expected.Length)
            {
                Add(
                    issues,
                    ResourceMaterializationContract.HistoryPath,
                    "qte_terminal_resource_projection_cardinality_mismatch",
                    $"exactly {_expected.Length} selected QTE resource projections",
                    ownedTransitions.ToString(System.Globalization.CultureInfo.InvariantCulture));
            }

            return new ResourceRegisteredSystemOutcomeProjectionResult(
                new Dictionary<string, JsonObject>(StringComparer.Ordinal),
                Array.Empty<AcceptedMechanicsOwnerTransition>(),
                issues);
        }

        private static void AddIndexedTransition(
            IDictionary<TransitionIdentity, IndexedTransition> index,
            ResourceTransition transition)
        {
            var identity = TransitionIdentityOf(transition);
            if (index.TryGetValue(identity, out var existing))
            {
                index[identity] = existing with { Count = existing.Count + 1 };
                return;
            }
            index.Add(identity, new IndexedTransition(transition, Count: 1));
        }

        private static bool TransitionSemanticsEqual(
            ResourceTransition transition,
            ExpectedTransition expected)
        {
            if (transition.Turn != expected.Turn ||
                transition.RequestedAmount != expected.Amount ||
                transition.AppliedAmount < 0m ||
                transition.AppliedAmount > expected.Amount ||
                transition.Phase != expected.Route.Phase ||
                transition.Priority != expected.Route.Priority ||
                transition.SourceEvidence != expected.Route.SourceEvidence ||
                !string.Equals(
                    transition.PolicyFingerprint,
                    expected.Route.PolicyBinding.AuthorityFingerprint,
                    StringComparison.Ordinal) ||
                transition.ReceiptId != null ||
                transition.BeforeState == null ||
                transition.AfterState == null ||
                transition.BeforeState.State != ResourceLifecycleState.Active ||
                transition.AfterState.State != ResourceLifecycleState.Active ||
                transition.BeforeState.Maximum != transition.AfterState.Maximum ||
                transition.BeforeState.CapacityBinding != transition.AfterState.CapacityBinding)
            {
                return false;
            }

            return ResourceMaterializationContract.TrySubtractExact(
                       transition.BeforeState.Current,
                       transition.AppliedAmount,
                       out var expectedAfter) &&
                   expectedAfter == transition.AfterState.Current;
        }

        private static string Describe(ResourceTransition transition) =>
            $"turn={transition.Turn};eventRef={transition.EventRef};" +
            $"origin={transition.OriginKind}/{transition.OriginId};" +
            $"operation={transition.Operation};requested={transition.RequestedAmount};" +
            $"applied={transition.AppliedAmount};phase={transition.Phase};" +
            $"priority={transition.Priority}";
    }

    internal static async Task<QteTerminalResourceFilePlan> BuildAsync(
        FileSystemManager fs,
        FileSystemManager.CanonicalWriteLease writeLease,
        QteTerminalResourceSelection selection)
    {
        ArgumentNullException.ThrowIfNull(fs);
        ArgumentNullException.ThrowIfNull(writeLease);
        ArgumentNullException.ThrowIfNull(selection);
        var work = new WorkMeter();
        var selectionIssues = ValidateSelection(selection, out var selectedCommands);
        if (selectionIssues.Count != 0)
        {
            return Failure(
                hasResourceChanges: selectedCommands != null,
                selectionIssues,
                work);
        }
        if (selectedCommands == null)
            return Empty(work);
        if (selectedCommands.ResourceChanges.Count == 0)
        {
            return new QteTerminalResourceFilePlan(
                hasResourceChanges: true,
                fingerprint: null,
                Array.Empty<CoordinatedStateWriteHelper.PlannedWrite>(),
                Array.Empty<ValidationIssue>(),
                work.Snapshot());
        }

        var beforeImages = new Dictionary<string, string?>(StringComparer.Ordinal);
        async Task<string?> CaptureAsync(string path)
        {
            if (!beforeImages.TryGetValue(path, out var json))
            {
                json = await fs.ReadFileAsync(writeLease, path);
                beforeImages.Add(path, json);
            }
            return json;
        }

        var definitionsResult = ResourceDefinitionCatalog.ParseCanonical(
            await CaptureAsync(ResourceMaterializationContract.DefinitionsPath),
            allowMissingPristine: false);
        if (!definitionsResult.IsValid || definitionsResult.Catalog == null)
            return Failure(hasResourceChanges: true, definitionsResult.Issues, work);
        var definitions = definitionsResult.Catalog;
        var stateResult = ResourceStateContract.ParseCanonical(
            await CaptureAsync(ResourceMaterializationContract.StatePath),
            definitions,
            allowMissingPristine: false);
        if (!stateResult.IsValid || stateResult.Ledger == null)
            return Failure(hasResourceChanges: true, stateResult.Issues, work);
        var state = stateResult.Ledger;
        var historyResult = ResourceHistoryState.ParseCanonical(
            await CaptureAsync(ResourceMaterializationContract.HistoryPath),
            definitions,
            allowMissingPristine: false);
        if (!historyResult.IsValid || historyResult.History == null)
            return Failure(hasResourceChanges: true, historyResult.Issues, work);
        var history = historyResult.History;
        var agreement = history.ValidateStateAgreement(state);
        if (agreement.Count != 0)
            return Failure(hasResourceChanges: true, agreement, work);

        var ownerResult = await CanonicalResourceOwnerAuthorityComposer.ComposeAsync(
            definitions,
            CaptureAsync,
            state,
            history,
            CanonicalResourceOwnerAuthorityPurpose.ExistingSessionValidation);
        if (!ownerResult.IsValid || ownerResult.Authority == null)
            return Failure(hasResourceChanges: true, ownerResult.Issues, work);
        var owners = ownerResult.Authority;

        var draftResult = BuildPlanningDraft(
            selection,
            definitions,
            state,
            history,
            owners,
            work);
        if (!draftResult.IsValid)
        {
            return Failure(
                draftResult.HasResourceChanges,
                draftResult.Issues,
                work);
        }
        if (!draftResult.HasResourceChanges)
            return Empty(work);
        if (draftResult.RegisteredOutcome == null ||
            draftResult.Sources == null)
        {
            return new QteTerminalResourceFilePlan(
                hasResourceChanges: true,
                fingerprint: null,
                Array.Empty<CoordinatedStateWriteHelper.PlannedWrite>(),
                Array.Empty<ValidationIssue>(),
                work.Snapshot());
        }

        var draft = draftResult.RegisteredOutcome;
        var catalog = draftResult.Sources;
        var planned = AcceptedMechanicsPlanner.BuildResources(
            new AcceptedMechanicsResourceInput(
                selection.SourceTurn,
                definitions,
                state,
                history,
                catalog,
                draft.Mutations,
                ExecutionSequenceOffset: draftResult.ExecutionSequenceOffset),
            new AcceptedMechanicsIdentityFactory());
        if (!planned.IsValid ||
            planned.StateAfterImage == null ||
            planned.HistoryAfterImage == null)
        {
            return Failure(hasResourceChanges: true, planned.Issues, work);
        }
        var projection = draft.Project(planned);
        if (!projection.IsValid)
            return Failure(hasResourceChanges: true, projection.Issues, work);
        var finalAgreement = planned.HistoryAfterImage.ValidateStateAgreement(
            planned.StateAfterImage);
        if (finalAgreement.Count != 0)
            return Failure(hasResourceChanges: true, finalAgreement, work);

        var writes = new List<CoordinatedStateWriteHelper.PlannedWrite>
        {
            CoordinatedStateWriteHelper.CreateGuardWrite(
                ResourceMaterializationContract.DefinitionsPath,
                beforeImages[ResourceMaterializationContract.DefinitionsPath]),
            new(
                ResourceMaterializationContract.StatePath,
                beforeImages[ResourceMaterializationContract.StatePath],
                planned.StateAfterImage.ToCanonicalJson(),
                RequireCurrentBaseline: true),
            new(
                ResourceMaterializationContract.HistoryPath,
                beforeImages[ResourceMaterializationContract.HistoryPath],
                planned.HistoryAfterImage.ToCanonicalJson(),
                RequireCurrentBaseline: true)
        };
        var quartet = await CanonicalResourceQuartetTransaction.ComposeExistingSessionAsync(
            definitions,
            state,
            history,
            planned.StateAfterImage,
            planned.HistoryAfterImage,
            CaptureAsync,
            beforeImages,
            new Dictionary<string, string>(StringComparer.Ordinal));
        if (quartet.Projection == null)
            return Failure(hasResourceChanges: true, quartet.Issues, work);
        CanonicalResourceQuartetTransaction.AddAuthorityWriteAndGlobalGuards(
            writes,
            quartet.Projection);

        return new QteTerminalResourceFilePlan(
            hasResourceChanges: true,
            draft.Fingerprint,
            writes,
            Array.Empty<ValidationIssue>(),
            work.Snapshot());
    }

    internal static QteTerminalResourcePlanningDraft BuildPlanningDraft(
        QteTerminalResourceSelection selection,
        ResourceDefinitionCatalog definitions,
        ResourceStateLedger state,
        ResourceHistoryState history,
        ResourceOwnerAuthority owners) =>
        BuildPlanningDraft(
            selection,
            definitions,
            state,
            history,
            owners,
            new WorkMeter());

    private static QteTerminalResourcePlanningDraft BuildPlanningDraft(
        QteTerminalResourceSelection selection,
        ResourceDefinitionCatalog definitions,
        ResourceStateLedger state,
        ResourceHistoryState history,
        ResourceOwnerAuthority owners,
        WorkMeter work)
    {
        ArgumentNullException.ThrowIfNull(selection);
        ArgumentNullException.ThrowIfNull(definitions);
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(history);
        ArgumentNullException.ThrowIfNull(owners);
        ArgumentNullException.ThrowIfNull(work);

        var issues = ValidateSelection(selection, out var commands);
        if (issues.Count != 0)
        {
            return PlanningFailure(
                commands != null,
                issues,
                work);
        }
        if (commands == null)
        {
            return new QteTerminalResourcePlanningDraft(
                hasResourceChanges: false,
                sources: null,
                registeredOutcome: null,
                executionSequenceOffset: 0,
                Array.Empty<ValidationIssue>(),
                work.Snapshot());
        }
        if (commands.ResourceChanges.Count == 0)
        {
            return new QteTerminalResourcePlanningDraft(
                hasResourceChanges: true,
                sources: null,
                registeredOutcome: null,
                executionSequenceOffset: 0,
                Array.Empty<ValidationIssue>(),
                work.Snapshot());
        }

        var resolvedCommands = new List<ResolvedCommand>(commands.ResourceChanges.Count);
        var definitionsByResourceKey = new Dictionary<
            string,
            ResourceDefinition?>(StringComparer.Ordinal);
        foreach (var command in commands.ResourceChanges)
        {
            var owner = owners.Resolve(new ResourceOwnerRequest(
                Realm,
                ResourceOwnerKind.Player,
                command.ResourceKey,
                PlayerId,
                OwnerRef: null));
            if (!owner.Success || owner.Entry == null)
            {
                issues.AddRange(owner.Issues);
                continue;
            }
            if (owner.Entry.Key.OwnerKind != ResourceOwnerKind.Player ||
                !string.Equals(owner.Entry.Key.Realm, Realm, StringComparison.Ordinal) ||
                !string.Equals(
                    owner.Entry.Key.ResourceOwnerId,
                    PlayerId,
                    StringComparison.Ordinal))
            {
                Add(
                    issues,
                    CommandPath(selection, command.CommandOrdinal) + ".target",
                    "qte_terminal_resource_owner_mismatch",
                    "exact active mortal player_current owner",
                    Describe(owner.Entry.Key));
                continue;
            }

            var coordinate = new ResourceCoordinate(
                owner.Entry.Key.Realm,
                owner.Entry.Key.OwnerKind,
                owner.Entry.Key.ResourceOwnerId,
                command.ResourceKey);
            if (!state.TryResolveExact(coordinate, out var entry) ||
                entry == null ||
                entry.State != ResourceLifecycleState.Active)
            {
                Add(
                    issues,
                    ResourceMaterializationContract.StatePath,
                    "qte_terminal_resource_coordinate_inactive",
                    "one exact active current-player resource coordinate",
                    Describe(coordinate));
                continue;
            }
            work.DefinitionBindingLookupCount++;
            if (!definitionsByResourceKey.TryGetValue(
                    command.ResourceKey,
                    out var definition))
            {
                work.DefinitionAuthorityLookupCount++;
                definitions.TryResolveExact(command.ResourceKey, out definition);
                definitionsByResourceKey.Add(command.ResourceKey, definition);
            }
            if (definition == null)
            {
                Add(
                    issues,
                    CommandPath(selection, command.CommandOrdinal) + ".resourceKey",
                    "qte_terminal_resource_definition_missing",
                    "one exact sealed resource definition",
                    command.ResourceKey);
                continue;
            }

            resolvedCommands.Add(new ResolvedCommand(
                command,
                owner.Entry,
                coordinate,
                definition));
        }
        if (issues.Count != 0)
            return PlanningFailure(hasResourceChanges: true, issues, work);

        var causalFingerprint = CreateCausalFingerprint(
            selection,
            owners,
            resolvedCommands);
        var sources = new List<ResourceMutationSourceExport>(resolvedCommands.Count);
        var mutations = new List<ResourceMutationIntent>(resolvedCommands.Count);
        ResourceOperationKey? dependency = null;
        foreach (var resolved in resolvedCommands)
        {
            var command = resolved.Command;
            var fingerprint = CreateSourceFingerprint(
                causalFingerprint,
                command,
                dependency);
            var source = new ResourceMutationSourceExport(
                SourceKind,
                command.EventRef,
                fingerprint,
                ResourceMutationSourceState.Active,
                SameTurn: true,
                resolved.Owner.Key);
            sources.Add(source);
            var mutation = new ResourceMutationIntent(
                command.EventRef,
                resolved.Coordinate,
                command.Amount,
                new ResourceMutationSourceRequest(
                    SourceKind,
                    command.EventRef,
                    ResourceOperation.Damage),
                dependency == null
                    ? Array.Empty<ResourceOperationKey>()
                    : new[] { dependency },
                Array.Empty<ResourceMutationEventRequirement>(),
                ReceiptId: null);
            mutations.Add(mutation);
            dependency = mutation.Key;
        }

        var sourceResult = ResourceMutationSourceCatalog.Create(sources);
        if (!sourceResult.IsValid || sourceResult.Catalog == null)
            return PlanningFailure(
                hasResourceChanges: true,
                sourceResult.Issues,
                work);
        var catalog = sourceResult.Catalog;
        var expected = new List<ExpectedTransition>(mutations.Count);
        for (var index = 0; index < mutations.Count; index++)
        {
            var resolved = resolvedCommands[index];
            var command = resolved.Command;
            var route = catalog.Resolve(
                mutations[index].Source,
                resolved.Definition,
                resolved.Coordinate);
            if (!route.IsValid || route.Route == null)
            {
                issues.AddRange(route.Issues);
                continue;
            }
            expected.Add(new ExpectedTransition(
                selection.SourceTurn,
                command.CommandOrdinal,
                command.EventRef,
                resolved.Coordinate,
                command.Amount,
                route.Route));
        }
        if (issues.Count != 0)
            return PlanningFailure(hasResourceChanges: true, issues, work);

        var executionOffset = ResolveExecutionSequenceOffset(
            selection,
            history,
            expected,
            issues,
            work);
        if (executionOffset == null || issues.Count != 0)
            return PlanningFailure(hasResourceChanges: true, issues, work);

        var draft = new Draft(
            CreateDraftFingerprint(selection, sources, mutations, expected),
            sources,
            mutations,
            expected,
            work);
        return new QteTerminalResourcePlanningDraft(
            hasResourceChanges: true,
            catalog,
            draft,
            executionOffset.Value,
            Array.Empty<ValidationIssue>(),
            work.Snapshot());
    }

    private static List<ValidationIssue> ValidateSelection(
        QteTerminalResourceSelection selection,
        out ResourceCommandCompositionResult? commands)
    {
        commands = null;
        var issues = new List<ValidationIssue>();
        if (!ResourceMaterializationContract.IsExactIdentifier(selection.QteId) ||
            !ResourceMaterializationContract.IsExactIdentifier(selection.ChapterId) ||
            !ResourceMaterializationContract.IsExactIdentifier(selection.ActionId) ||
            !ResourceMaterializationContract.IsExactIdentifier(selection.OutcomeId) ||
            selection.Grade is not ("success" or "partial" or "fail"))
        {
            Add(
                issues,
                QtePath,
                "qte_terminal_resource_selection_invalid",
                "exact QTE/chapter/action/outcome identities and closed grade",
                $"qte={selection.QteId};chapter={selection.ChapterId};" +
                $"action={selection.ActionId};outcome={selection.OutcomeId};grade={selection.Grade}");
        }

        var fragment = selection.ResponseFragment;
        foreach (var forbidden in new[]
                 {
                     "resourceDefinitionCreations",
                     "resourceCapacityChanges"
                 })
        {
            if (!fragment.ContainsKey(forbidden))
                continue;
            Add(
                issues,
                OutcomePath(selection) + ".responseFragment." + forbidden,
                "qte_terminal_resource_surface_forbidden",
                "field omitted from closed QTE-v1 terminal resource authority",
                fragment[forbidden]?.GetValueKind().ToString() ?? "null");
        }
        if (!fragment.TryGetPropertyValue("resourceChanges", out var resourceNode))
            return issues;

        var root = new JsonObject
        {
            ["resourceChanges"] = resourceNode?.DeepClone()
        };
        commands = ResourceAcceptedTurnInputComposer.Parse(root.ToJsonString());
        foreach (var issue in commands.Issues)
        {
            issues.Add(new ValidationIssue(
                OutcomePath(selection) + ".responseFragment." + issue.FilePath,
                issue.Severity,
                issue.Message,
                issue.Code,
                issue.Actor,
                "QTE",
                issue.Expected,
                issue.Actual,
                issue.RepairHint));
        }
        if (resourceNode is not JsonArray rawCommands)
            return issues;

        for (var index = 0; index < rawCommands.Count; index++)
        {
            var path = CommandPath(selection, index + 1);
            if (rawCommands[index] is not JsonObject command)
                continue;
            var operationValid = ReadExactString(command["operation"]) == "damage";
            if (!operationValid)
            {
                Add(
                    issues,
                    path + ".operation",
                    "qte_terminal_resource_operation_forbidden",
                    "exact lowercase damage",
                    command["operation"]?.ToJsonString() ?? "missing");
            }
            var target = command["target"] as JsonObject;
            if (target == null ||
                target.Count != 2 ||
                ReadExactString(target["kind"]) != "player" ||
                ReadExactString(target["targetId"]) != PlayerId)
            {
                Add(
                    issues,
                    path + ".target",
                    "qte_terminal_resource_target_forbidden",
                    "closed {kind:player,targetId:player_current}",
                    target?.ToJsonString() ?? "missing");
            }
            var source = command["source"] as JsonObject;
            if (source == null ||
                source.Count != 1 ||
                ReadExactString(source["kind"]) != SourceKind)
            {
                Add(
                    issues,
                    path + ".source",
                    "qte_terminal_resource_source_forbidden",
                    "closed {kind:narrative_outcome} with client-derived source identity",
                    source?.ToJsonString() ?? "missing");
            }
            var expectedEventRef = FormattableString.Invariant(
                $"turn_{selection.SourceTurn}:qte_terminal:{selection.OutcomeOrdinal}:resource:{index + 1}");
            var submittedEventRef = ReadExactString(command["eventRef"]);
            if (!string.Equals(
                    expectedEventRef,
                    submittedEventRef,
                    StringComparison.Ordinal))
            {
                Add(
                    issues,
                    path + ".eventRef",
                    "qte_terminal_resource_event_ref_mismatch",
                    expectedEventRef,
                    submittedEventRef ?? "missing");
            }
        }

        if (commands.IsValid && commands.ResourceChanges.Count != rawCommands.Count)
        {
            Add(
                issues,
                OutcomePath(selection) + ".responseFragment.resourceChanges",
                "qte_terminal_resource_command_cardinality_mismatch",
                "every authored QTE resource command parsed exactly once",
                $"authored={rawCommands.Count};parsed={commands.ResourceChanges.Count}");
        }
        return issues;
    }

    private static TransitionIdentity ExpectedIdentity(
        ExpectedTransition expected) => new(
        expected.EventRef,
        SourceKind,
        expected.EventRef,
        expected.Coordinate,
        ResourceTransitionOperation.Damage);

    private static TransitionIdentity TransitionIdentityOf(
        ResourceTransition transition) => new(
        transition.EventRef,
        transition.OriginKind,
        transition.OriginId,
        transition.Coordinate,
        transition.Operation);

    private static int? ResolveExecutionSequenceOffset(
        QteTerminalResourceSelection selection,
        ResourceHistoryState history,
        IReadOnlyList<ExpectedTransition> expected,
        List<ValidationIssue> issues,
        WorkMeter work)
    {
        var transitionsByIdentity = new Dictionary<
            TransitionIdentity,
            IndexedTransition>();
        int? maximumSameTurnSequence = null;
        foreach (var transition in history.Transitions)
        {
            work.ExistingReplayTransitionVisitCount++;
            if (transition.Turn == selection.SourceTurn &&
                (maximumSameTurnSequence == null ||
                 transition.ExecutionSequence > maximumSameTurnSequence.Value))
            {
                maximumSameTurnSequence = transition.ExecutionSequence;
            }
            if (!string.Equals(
                    transition.OriginKind,
                    SourceKind,
                    StringComparison.Ordinal))
            {
                continue;
            }

            var identity = TransitionIdentityOf(transition);
            if (transitionsByIdentity.TryGetValue(identity, out var indexed))
            {
                transitionsByIdentity[identity] = indexed with
                {
                    Count = indexed.Count + 1
                };
            }
            else
            {
                transitionsByIdentity.Add(
                    identity,
                    new IndexedTransition(transition, Count: 1));
            }
        }

        var existing = new ResourceTransition?[expected.Count];
        for (var index = 0; index < expected.Count; index++)
        {
            work.ExistingReplayIdentityLookupCount++;
            if (!transitionsByIdentity.TryGetValue(
                    ExpectedIdentity(expected[index]),
                    out var indexed))
            {
                continue;
            }
            if (indexed.Count != 1)
            {
                Add(
                    issues,
                    ResourceMaterializationContract.HistoryPath,
                    "qte_terminal_resource_replay_identity_conflict",
                    "at most one exact persisted transition for each selected QTE damage identity",
                    $"eventRef={expected[index].EventRef};matches={indexed.Count}");
                continue;
            }
            existing[index] = indexed.First;
        }
        if (issues.Count != 0)
            return null;

        var replayCount = existing.Count(static transition => transition != null);
        if (replayCount != 0 && replayCount != expected.Count)
        {
            Add(
                issues,
                ResourceMaterializationContract.HistoryPath,
                "qte_terminal_resource_partial_replay",
                "all selected QTE resource transitions present or all absent",
                $"expected={expected.Count};present={replayCount}");
            return null;
        }
        if (replayCount == expected.Count)
        {
            var offset = existing[0]!.ExecutionSequence;
            for (var index = 0; index < existing.Length; index++)
            {
                if (offset > int.MaxValue - index)
                {
                    Add(
                        issues,
                        ResourceMaterializationContract.HistoryPath,
                        "qte_terminal_resource_replay_sequence_mismatch",
                        "contiguous exact replay execution sequence at the accepted source turn",
                        $"eventRef={expected[index].EventRef};sequence overflow");
                    continue;
                }
                if (existing[index]!.Turn == selection.SourceTurn &&
                    existing[index]!.ExecutionSequence == offset + index)
                {
                    continue;
                }
                Add(
                    issues,
                    ResourceMaterializationContract.HistoryPath,
                    "qte_terminal_resource_replay_sequence_mismatch",
                    "contiguous exact replay execution sequence at the accepted source turn",
                    $"eventRef={expected[index].EventRef};turn={existing[index]!.Turn};" +
                    $"sequence={existing[index]!.ExecutionSequence}");
            }
            return issues.Count == 0 ? offset : null;
        }

        if (maximumSameTurnSequence == null)
            return 0;
        var maximum = maximumSameTurnSequence.Value;
        if (maximum == int.MaxValue)
        {
            Add(
                issues,
                ResourceMaterializationContract.HistoryPath,
                "qte_terminal_resource_execution_sequence_exhausted",
                "available execution sequence at the accepted source turn",
                maximum.ToString(System.Globalization.CultureInfo.InvariantCulture));
            return null;
        }
        return maximum + 1;
    }

    private static string CreateCausalFingerprint(
        QteTerminalResourceSelection selection,
        ResourceOwnerAuthority owners,
        IReadOnlyList<ResolvedCommand> commands)
    {
        using var builder = new ResourceFingerprintBuilder(
            "qte-terminal-resource-causal-v1");
        AppendSelection(builder, selection);
        builder.Append(owners.Fingerprint);
        builder.Append(commands.Count);
        foreach (var resolved in commands)
        {
            AppendCommand(builder, resolved.Command);
            AppendOwner(builder, resolved.Owner);
            AppendDefinition(builder, resolved.Definition);
        }
        return builder.Build();
    }

    private static string CreateSourceFingerprint(
        string causalFingerprint,
        ResourceOrdinaryCommand command,
        ResourceOperationKey? dependency)
    {
        using var builder = new ResourceFingerprintBuilder(
            "qte-terminal-resource-source-v1");
        builder.Append(causalFingerprint);
        AppendCommand(builder, command);
        builder.Append(dependency != null);
        if (dependency != null)
            AppendOperationKey(builder, dependency);
        return builder.Build();
    }

    private static void AppendCommand(
        ResourceFingerprintBuilder builder,
        ResourceOrdinaryCommand command)
    {
        builder.Append(command.CommandOrdinal);
        builder.Append(command.EventRef);
        builder.Append(SourceKind);
        builder.Append((int)command.Operation);
        builder.Append((int)command.Target.OwnerKind);
        builder.Append(command.Target.TargetId != null);
        if (command.Target.TargetId != null)
            builder.Append(command.Target.TargetId);
        builder.Append(command.Target.TargetRef != null);
        if (command.Target.TargetRef != null)
            builder.Append(command.Target.TargetRef);
        builder.Append(command.ResourceKey);
        builder.Append(command.Amount);
        builder.Append(command.Reason);
    }

    private static string CreateDraftFingerprint(
        QteTerminalResourceSelection selection,
        IReadOnlyList<ResourceMutationSourceExport> sources,
        IReadOnlyList<ResourceMutationIntent> mutations,
        IReadOnlyList<ExpectedTransition> expected)
    {
        using var builder = new ResourceFingerprintBuilder(
            "qte-terminal-resource-draft-v1");
        AppendSelection(builder, selection);
        builder.Append(sources.Count);
        for (var index = 0; index < sources.Count; index++)
        {
            var source = sources[index];
            var mutation = mutations[index];
            var projection = expected[index];
            builder.Append(source.SourceKind);
            builder.Append(source.SourceId);
            builder.Append(source.AuthorityFingerprint);
            builder.Append(source.SameTurn);
            builder.Append(source.BoundOwner!.Realm);
            builder.Append((int)source.BoundOwner.OwnerKind);
            builder.Append(source.BoundOwner.ResourceOwnerId);
            builder.Append(mutation.EventRef);
            ResourceStateContract.AppendCoordinate(builder, mutation.Coordinate);
            builder.Append(mutation.Amount);
            builder.Append((int)mutation.Source.Operation);
            builder.Append(mutation.Dependencies.Count);
            foreach (var dependency in mutation.Dependencies)
                AppendOperationKey(builder, dependency);
            builder.Append(projection.CommandOrdinal);
            builder.Append(projection.Turn);
            builder.Append((int)projection.Route.Phase);
            builder.Append(projection.Route.Priority);
            builder.Append(projection.Route.PolicyBinding.AuthorityFingerprint);
            builder.Append(projection.Route.SourceEvidence.AuthorityFingerprint);
        }
        return builder.Build();
    }

    private static void AppendSelection(
        ResourceFingerprintBuilder builder,
        QteTerminalResourceSelection selection)
    {
        builder.Append(selection.SourceTurn);
        builder.Append(selection.QteId);
        builder.Append(selection.ChapterId);
        builder.Append(selection.ActionId);
        builder.Append(selection.Grade);
        builder.Append(selection.OutcomeOrdinal);
        builder.Append(selection.OutcomeId);
    }

    private static void AppendOwner(
        ResourceFingerprintBuilder builder,
        ResourceOwnerAuthorityEntry owner)
    {
        builder.Append(owner.Key.Realm);
        builder.Append((int)owner.Key.OwnerKind);
        builder.Append(owner.Key.ResourceOwnerId);
        builder.Append((int)owner.Lifecycle);
        builder.Append(owner.SameTurn);
        builder.Append(owner.SameTurnRef != null);
        if (owner.SameTurnRef != null)
            builder.Append(owner.SameTurnRef);
        builder.Append(owner.BoundNpcId != null);
        if (owner.BoundNpcId != null)
            builder.Append(owner.BoundNpcId);
        var capabilities = owner.ResourceCapabilities.OrderBy(
                     static value => value,
                     StringComparer.Ordinal).ToArray();
        builder.Append(capabilities.Length);
        foreach (var capability in capabilities)
        {
            builder.Append(capability);
        }
        var realmIndependentCapabilities = owner.RealmIndependentResourceCapabilities
            .OrderBy(static value => value, StringComparer.Ordinal)
            .ToArray();
        builder.Append(realmIndependentCapabilities.Length);
        foreach (var capability in realmIndependentCapabilities)
            builder.Append(capability);
        builder.Append(owner.AuthorityFingerprint);
    }

    private static void AppendDefinition(
        ResourceFingerprintBuilder builder,
        ResourceDefinition definition)
    {
        builder.Append(definition.ResourceKey);
        builder.Append(definition.DefinitionVersion);
        builder.Append(definition.DisplayName);
        builder.Append((int)definition.NumericKind);
        builder.Append(definition.Unit);
        builder.Append(definition.Quantum);
        builder.Append((int)definition.MinimumPolicy.Kind);
        builder.Append(definition.MinimumPolicy.Value);
        builder.Append((int)definition.CapacityPolicy.Kind);
        builder.Append(definition.CapacityPolicy.Value.HasValue);
        if (definition.CapacityPolicy.Value.HasValue)
            builder.Append(definition.CapacityPolicy.Value.Value);
        builder.Append(definition.CapacityPolicy.FormulaKey != null);
        if (definition.CapacityPolicy.FormulaKey != null)
            builder.Append(definition.CapacityPolicy.FormulaKey);
        builder.Append((int)definition.InitializationPolicy.Kind);
        builder.Append(definition.InitializationPolicy.Value.HasValue);
        if (definition.InitializationPolicy.Value.HasValue)
            builder.Append(definition.InitializationPolicy.Value.Value);
        builder.Append(definition.InitializationPolicy.FormulaKey != null);
        if (definition.InitializationPolicy.FormulaKey != null)
            builder.Append(definition.InitializationPolicy.FormulaKey);
        var ownerKinds = definition.AllowedOwnerKinds.OrderBy(
            static value => value).ToArray();
        builder.Append(ownerKinds.Length);
        foreach (var ownerKind in ownerKinds)
        {
            builder.Append((int)ownerKind);
        }
        var operations = definition.AllowedOperations.OrderBy(
            static value => value).ToArray();
        builder.Append(operations.Length);
        foreach (var operation in operations)
        {
            builder.Append((int)operation);
        }
        builder.Append((int)definition.FloorPolicy);
        builder.Append((int)definition.CapPolicy);
        builder.Append((int)definition.Visibility);
        builder.Append(definition.Materialization.SchemaVersion);
        builder.Append(definition.Materialization.DefinitionId);
        builder.Append(definition.Materialization.Seal);
        builder.Append(definition.Materialization.CreatedAtTurn);
        builder.Append(definition.Materialization.CreatedEventRef);
    }

    private static void AppendOperationKey(
        ResourceFingerprintBuilder builder,
        ResourceOperationKey key)
    {
        builder.Append(key.EventRef);
        builder.Append(key.OriginKind);
        builder.Append(key.OriginId);
        ResourceStateContract.AppendCoordinate(builder, key.Coordinate);
        builder.Append((int)key.Operation);
    }

    private static ResourceMutationSourceExport CloneSource(
        ResourceMutationSourceExport source) => source with
    {
        BoundOwner = source.BoundOwner == null ? null : source.BoundOwner with { }
    };

    private static ResourceMutationIntent CloneMutation(
        ResourceMutationIntent mutation) => mutation with
    {
        Dependencies = mutation.Dependencies.Select(static dependency =>
            dependency with
            {
                Coordinate = dependency.Coordinate with { }
            }).ToArray(),
        EventRequirements = mutation.EventRequirements.Select(static requirement =>
            requirement with
            {
                Producer = requirement.Producer with
                {
                    Coordinate = requirement.Producer.Coordinate with { }
                }
            }).ToArray(),
        Coordinate = mutation.Coordinate with { },
        Source = mutation.Source with { }
    };

    private static string OutcomePath(QteTerminalResourceSelection selection) =>
        $"{QtePath}.terminalOutcomes[{selection.OutcomeOrdinal - 1}]";

    private static string CommandPath(
        QteTerminalResourceSelection selection,
        int commandOrdinal) =>
        OutcomePath(selection) +
        $".responseFragment.resourceChanges[{commandOrdinal - 1}]";

    private static string? ReadExactString(JsonNode? node) =>
        node is JsonValue value &&
        value.TryGetValue<string>(out var text) &&
        ResourceMaterializationContract.IsExactIdentifier(text)
            ? text
            : null;

    private static string Describe(ResourceOwnerKey key) =>
        $"{key.Realm}/{ResourceDefinitionCatalog.GetOwnerKindToken(key.OwnerKind)}/" +
        key.ResourceOwnerId;

    private static string Describe(ResourceCoordinate coordinate) =>
        Describe(new ResourceOwnerKey(
            coordinate.Realm,
            coordinate.OwnerKind,
            coordinate.ResourceOwnerId)) + "/" + coordinate.ResourceKey;

    private static QteTerminalResourceFilePlan Empty(WorkMeter? work = null) =>
        new(
            hasResourceChanges: false,
            fingerprint: null,
            Array.Empty<CoordinatedStateWriteHelper.PlannedWrite>(),
            Array.Empty<ValidationIssue>(),
            work?.Snapshot());

    private static QteTerminalResourceFilePlan Failure(
        bool hasResourceChanges,
        IReadOnlyList<ValidationIssue> issues,
        WorkMeter? work = null) =>
        new(
            hasResourceChanges,
            fingerprint: null,
            Array.Empty<CoordinatedStateWriteHelper.PlannedWrite>(),
            issues,
            work?.Snapshot());

    private static QteTerminalResourcePlanningDraft PlanningFailure(
        bool hasResourceChanges,
        IReadOnlyList<ValidationIssue> issues,
        WorkMeter work) =>
        new(
            hasResourceChanges,
            sources: null,
            registeredOutcome: null,
            executionSequenceOffset: 0,
            issues,
            work.Snapshot());

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
