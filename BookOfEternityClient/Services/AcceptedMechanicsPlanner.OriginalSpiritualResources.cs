using System.Text.Json;

namespace BookOfEternityClient.Services;

internal static partial class AcceptedMechanicsPlanner
{
    internal sealed record OriginalSpiritualResourceExecutionResult(
        ResourceExecutionSession? Session, IReadOnlyList<ValidationIssue> Issues);

    /// <summary>
    /// Composes original resource mutations and retains routing for subsequent source-owned spiritual exchanges.
    /// The enclosing signed capture remains the source authority.
    /// </summary>
    /// <param name="input">
    /// Actual validated original accepted turn input before exchange continuation.
    /// </param>
    /// <param name="terminal">
    /// Capture-issued bounded terminal preparation, or <see langword="null"/> for continuing conflicts.
    /// </param>
    /// <returns>
    /// The unpublished live executor or preparation diagnostics.
    /// </returns>
    internal static OriginalSpiritualResourceExecutionResult BeginOriginalSpiritualResourceExecution(
        AcceptedMechanicsInput input, ValidationService.SpiritualOriginalTurnCapture.PreparedTerminal? terminal = null) =>
        BeginOriginalResourceExecution(input, live: true, terminal);

    /// <summary>
    /// Composes a fixed original Mortal resource executor with retained draft routing.
    /// </summary>
    /// <param name="input">
    /// Actual validated original input, before wound insertion.
    /// </param>
    /// <returns>
    /// The unpublished executor or preparation diagnostics.
    /// </returns>
    internal static OriginalSpiritualResourceExecutionResult BeginOriginalMortalResourceExecution(
        AcceptedMechanicsInput input) => BeginOriginalResourceExecution(input, live: false);

    /// <summary>
    /// Prepares original resource mutations once and retains the same validated effect routing.
    /// </summary>
    /// <param name="input">
    /// Validated original accepted turn input.
    /// </param>
    /// <param name="live">
    /// True for subsequent spiritual exchange batches; false for fixed Mortal commands.
    /// </param>
    /// <param name="terminal">
    /// Exact capture preparation retaining only the terminal opposition owner during execution;
    /// <see langword="null"/> preserves ordinary owner authority.
    /// </param>
    /// <returns>
    /// An executor owning the prepared input or validation diagnostics.
    /// </returns>
    private static OriginalSpiritualResourceExecutionResult BeginOriginalResourceExecution(
        AcceptedMechanicsInput input, bool live,
        ValidationService.SpiritualOriginalTurnCapture.PreparedTerminal? terminal = null)
    {
        var context = input.PlanningContext;
        if (terminal != null && !ReferenceEquals(input, terminal.OriginalInput))
            return new(null, Issue("spiritual_terminal_input_mismatch",
                "the exact original input retained by the terminal capture", "foreign input"));
        if (context?.EffectPlan == null)
            return new(null, Issue("spiritual_original_effect_context_missing",
                "the validated original effect base, including an empty base", "missing"));
        if (!live && context.PendingResolutionState is { Requests.Count: > 0 })
            return new(null, Issue("mortal_original_pending_replay_unsupported",
                "an original fixed turn without retained pending requests or receipts", "pending state requires its replay owner"));
        var issues = new List<ValidationIssue>();
        if (context.WoundStageBundle is { } woundStages)
            issues.AddRange(AcceptedMechanicsCarrierAssembler.ValidateInitialEffectPlan(
                context.EffectPlan, woundStages));
        if (issues.Count != 0)
            return new(null, issues);

        var definitions = context.Definitions;
        var sameTurnDefinitions = new Dictionary<string, ResourceDefinition>(StringComparer.Ordinal);
        if (context.Commands.DefinitionCreations.Count != 0)
        {
            var batch = ResourceDefinitionCatalog.BeginMaterializationBatch(definitions);
            var definitionIdentityFactory = context.ResourceIdentityFactory
                ?? new AcceptedMechanicsIdentityFactory();
            foreach (var creation in context.Commands.DefinitionCreations.OrderBy(value => value.CommandOrdinal))
            {
                using var proposal = JsonDocument.Parse(creation.Definition.ToJsonString());
                var materialized = batch.MaterializeProposal(proposal.RootElement,
                    input.Turn, creation.EventRef,
                    () => new ResourceDefinitionIdentity(
                        definitionIdentityFactory.CreateDefinitionId(creation, input.Turn),
                        definitionIdentityFactory.CreateDefinitionSeal(creation, input.Turn)));
                issues.AddRange(materialized.Issues);
                if (materialized.IsValid && materialized.Definition != null)
                    sameTurnDefinitions.Add(creation.DefinitionRef, materialized.Definition);
            }
            definitions = batch.Freeze();
        }
        var capacity = ComposeCapacityTransitions(input, context, definitions, sameTurnDefinitions, issues);
        var ordinary = ComposeOrdinaryMutations(input, context, definitions, issues)
            .Concat(context.RegisteredSystemOutcomes
                .Where(outcome => !AfterlifeSpiritualConflictResourceOutcome.IsConflictOutcome(outcome))
                .SelectMany(outcome => outcome.Mutations)).ToArray();
        // Exchange exports arrive with their owned batch. Eagerly seeding every
        // observed exchange would retain a partial leaf's policy even if that
        // leaf is completed before its first resource execution.
        var exchangeSources = context.RegisteredSystemOutcomes
            .Where(AfterlifeSpiritualConflictResourceOutcome.IsConflictOutcome)
            .SelectMany(outcome => outcome.SourceExports)
            .Select(source => (source.SourceKind, source.SourceId)).ToHashSet();
        var baselineSources = ResourceMutationSourceCatalog.Create(context.Sources.Exports
            .Where(source => !exchangeSources.Contains((source.SourceKind, source.SourceId))));
        issues.AddRange(baselineSources.Issues);
        if (issues.Count != 0)
            return new(null, issues);

        // Same-turn definitions are a real output of the preparation above.
        // Rebind only the typed context to that exact catalog; original accepted
        // input data/fingerprints and before-images remain untouched.
        var effectiveContext = new AcceptedMechanicsPlanningContext(
            context.DefinitionRoot, definitions, context.State, context.History,
            terminal?.ExecutionOwners ?? context.Owners, context.Sources, context.Commands, context.EffectIdentityRoot,
            context.EffectPlan, context.CapacityTransitions, context.OwnerCapacityDrafts,
            context.TerminalOwners, context.OwnerCompanionAfterImages, context.OwnerTransitions,
            registeredSystemOutcomes: context.RegisteredSystemOutcomes,
            pendingResolutionState: context.PendingResolutionState,
            resourceIdentityFactory: context.ResourceIdentityFactory,
            effectIdentityFactory: context.EffectIdentityFactory,
            executionSequenceOffset: context.ExecutionSequenceOffset,
            woundStageBundle: context.WoundStageBundle,
            woundAnchorPlan: context.WoundAnchorPlan,
            directWoundPublicationAuthority: context.DirectWoundPublicationAuthority,
            treatmentResourcePublicationAuthority: context.TreatmentResourcePublicationAuthority);
        var pendingInput = input.WithPlanningContext(effectiveContext);
        var routing = EffectAcceptedTurnPlanner.BaseResourceRouting.Capture(
            context.EffectPlan, effectiveContext.Owners, definitions);
        var session = new ResourceExecutionSession(new AcceptedMechanicsResourceInput(
            input.Turn, definitions, context.State, context.History, baselineSources.Catalog!,
            ordinary, capacity, context.ExecutionSequenceOffset),
            context.ResourceIdentityFactory ?? new AcceptedMechanicsIdentityFactory(), live, routing, terminal);
        try
        {
            if (live)
                session.BindOriginalPendingContext(pendingInput);
            return new(session, Array.Empty<ValidationIssue>());
        }
        catch
        {
            session.Dispose();
            throw;
        }
    }
}
