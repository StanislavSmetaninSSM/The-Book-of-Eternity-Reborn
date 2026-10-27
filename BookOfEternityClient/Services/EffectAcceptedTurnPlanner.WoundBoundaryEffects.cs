using System.Collections.Immutable;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace BookOfEternityClient.Services;

internal static partial class EffectAcceptedTurnPlanner
{
    // Coordinates and facts are DATA. Only an authenticated domain executor calls this private method.
    private sealed record WoundBoundaryEffectCoordinates(string LogicalKey, int StageOrdinal,
        string EventRef, string OperationKey, string AuthorityFingerprint,
        string TransitionKind, string ReadableSummary, IReadOnlyList<WoundAcceptedEventAuthority> Events,
        bool IsMortalRecovery)
    {
        internal string Coordinate(string kind, string local) => IsMortalRecovery
            ? RecoveryCoordinate(kind, LogicalKey, StageOrdinal, local)
            : SpiritualWoundRecoveryPlanner.Coordinate(kind, LogicalKey, local, StageOrdinal);
    }
    private sealed record WoundBoundaryEffectFacts(IReadOnlyList<WoundComplication> Complications,
        WoundConsequences Consequences, IReadOnlyList<EffectAcceptedApplicationResult> Applications,
        IReadOnlyList<EffectAcceptedTerminationResult> Terminations);

    private static WoundBoundaryEffectFacts? ExecuteWoundBoundaryEffects(EffectAcceptedTurnInput input,
        WoundMaterializationEnvelope before, WoundWorkingOwnedGraph graph, int slotBudget,
        WoundBoundaryEffectCoordinates coordinates, bool changedSeverity, bool terminalHeal,
        CarrierWorkspace workspace, EffectIdentityHistoryOwner identityRoot, EffectIdentityFactory identityFactory,
        int turn, List<string> effectIds, List<string> transitionIds, List<JsonObject> activeEffects,
        HashSet<string> processedEvents, List<EffectSourceAuthorityEntry> usedSources,
        List<EffectTargetKey> usedTargets, List<WoundApplicationRootEffectBinding> rootBindings,
        List<ValidationIssue> issues)
    {
        var complicationIds = graph.Complications.ToDictionary(row => row.Reference, row =>
            row.Reference.Origin == WoundWorkingReferenceOrigin.Existing ? row.Reference.Value :
                coordinates.Coordinate("wound_complication", row.Reference.Value));
        WoundRootOwnershipDomain Domain(WoundWorkingReference root)
        {
            var owner = graph.Complications.SingleOrDefault(row => row.OwnedRoots.Contains(root));
            return owner is null ? WoundRootOwnershipDomain.BaseWound :
                WoundRootOwnershipDomain.ForComplication(complicationIds[owner.Reference]);
        }
        if (!WoundEffectCarrierAdapter.TryCreateTargetKey(before.Owner, out var target))
            throw new InvalidOperationException("Recovery wound owner has no exact effect target.");
        var localRef = coordinates.Coordinate("wound_ref", before.WoundId);
        var definitions = graph.Definitions.Select(row =>
        {
            var body = JsonNode.Parse(row.Definition.GetRawText())!.AsObject();
            return new WoundEffectSourceDefinition(body["definitionKey"]!.GetValue<string>(), body);
        }).ToArray();
        var definitionMap = definitions.ToDictionary(row => row.DefinitionKey, StringComparer.Ordinal);
        var selectedRoots = terminalHeal ? Array.Empty<WoundWorkingRoot>() : graph.Roots
            .Where(row => changedSeverity || row.Reference.Origin != WoundWorkingReferenceOrigin.Existing).ToArray();
        var applications = new List<WoundRootEffectApplication>();
        var applicationReferences = new Dictionary<WoundWorkingReference, string>();
        foreach (var root in selectedRoots)
        {
            var definition = definitionMap[root.DefinitionKey].Definition;
            if (definition["components"] is not JsonArray components ||
                !WoundEffectCarrierAdapter.TryCreateCarrierCoordinate(before.Owner, target, definition, out var coordinate))
                throw new InvalidOperationException("Recovery root has no exact materialization coordinate.");
            var applicationRef = coordinates.Coordinate("wound_application", root.Reference.Value);
            applicationReferences.Add(root.Reference, applicationRef);
            var key = new EffectSourceKey(before.Owner.Realm, "wound", before.WoundId, root.DefinitionKey);
            var parameters = new JsonObject();
            applications.Add(new WoundRootEffectApplication(applicationRef, coordinates.StageOrdinal + 1, applications.Count + 1,
                "apply", coordinates.Coordinate("wound_operation", root.Reference.Value),
                root.DefinitionKey, new WoundEffectTargetSelector(target.Kind, target.TargetId, null), target,
                new WoundEffectSourceSelector(key.Realm, key.Kind, null, localRef, key.DefinitionKey), key, parameters,
                graph.Entries.Where(row => row.Root == root.Reference).Select(row =>
                    new WoundEffectSlotAgreement(row.Slot, row.ProfileKey, row.ReadableSummary)).ToArray(),
                components.Count, WoundEffectMaterializationFingerprint.Compute(key,
                    definition["schemaVersion"]!.GetValue<int>(), parameters, components),
                Domain(root.Reference), coordinates.EventRef, coordinate,
                root.Reference.Origin == WoundWorkingReferenceOrigin.Existing ? root.Reference.Value : null));
        }
        var acceptedEvent = coordinates.Events.Single(row => row.EventRef == coordinates.EventRef);
        var export = new WoundEffectSourceExport(1, "wound", before.WoundId, localRef, "active", false,
            before.Owner.Realm, before.Owner, coordinates.EventRef, acceptedEvent.SemanticFingerprint,
            coordinates.OperationKey, coordinates.AuthorityFingerprint, definitions);
        var currentIdentity = ParseIdentity(identityRoot.ReadSnapshot());
        issues.AddRange(currentIdentity.Issues);
        if (currentIdentity.State is null || issues.Count != 0) return null;
        var terminalRoots = changedSeverity || terminalHeal
            ? before.Consequences.OwnedEffectSources.RootBindings.Select(row => row.EffectId).ToArray()
            : Array.Empty<string>();
        var terminal = WoundEffectTerminalOperationPlanner.Plan(before, workspace.ToInput(), currentIdentity.State,
            terminalRoots, coordinates.EventRef, coordinates.StageOrdinal + 1, applications.Count, coordinates.OperationKey);
        issues.AddRange(terminal.Issues);
        if (issues.Count != 0) return null;
        var lineage = graph.Roots.Select(root => applicationReferences.TryGetValue(root.Reference, out var app)
            ? new WoundRootLineageAuthorityRow(app, null, root.DefinitionKey, Domain(root.Reference))
            : new WoundRootLineageAuthorityRow(null, root.Reference.Value, root.DefinitionKey, Domain(root.Reference))).ToArray();
        var beforeFingerprint = WoundIdentityState.ComputeSemanticFingerprint(before);
        var structural = WoundAcceptedTurnFingerprints.ComputeTransitionAuthority(coordinates.AuthorityFingerprint, localRef,
            before.WoundId, coordinates.OperationKey, coordinates.AuthorityFingerprint, coordinates.OperationKey,
            coordinates.ReadableSummary, before.Severity.Rank, coordinates.TransitionKind, null, beforeFingerprint);
        var transition = new WoundPreparedTransitionAuthority(coordinates.AuthorityFingerprint, coordinates.OperationKey,
            coordinates.AuthorityFingerprint, coordinates.OperationKey, coordinates.ReadableSummary, before.Severity.Rank,
            coordinates.TransitionKind, null, beforeFingerprint, structural);
        var provisional = new WoundEffectOperationBatch(localRef, before.WoundId, export, applications,
            terminal.Operations, lineage, string.Empty, transition);
        var batch = new WoundEffectOperationBatch(localRef, before.WoundId, export, applications,
            terminal.Operations, lineage, WoundAcceptedTurnFingerprints.ComputeSourceExport(provisional), transition);
        var sourceAuthority = EffectSourceAuthority.Build(new EffectSourceAuthorityInput([], [new EffectSourceExport(
            export.Realm, "wound", before.WoundId,
            new JsonArray(definitions.Select(row => (JsonNode)row.Definition).ToArray()),
            Materializable: false, Active: true, SameTurn: true, SourceRef: localRef,
            SatisfiedPredicates: new HashSet<string>(StringComparer.Ordinal) { "active" })],
            new HashSet<string>(StringComparer.Ordinal), WoundGroups: [new WoundSourceGroupAuthority(
                new EffectIdentitySourceGroup(export.Realm, "wound", before.WoundId), before.Owner, target, true,
                localRef, batch.SourceExportFingerprint, definitions,
                lineage.Where(row => row.ApplicationRef is not null).ToArray(),
                lineage.Where(row => row.EffectId is not null).ToArray())]));
        issues.AddRange(sourceAuthority.Issues);
        if (issues.Count != 0) return null;
        var stageInput = input with { SourceAuthority = sourceAuthority };
        var events = coordinates.Events.ToDictionary(row => row.EventRef, StringComparer.Ordinal);
        var requests = applications.Select(root => PrepareSelectedWoundApplication(stageInput, batch, export,
            definitionMap[root.DefinitionKey], root, events, "mortalRecovery.stageEffects", issues, null, ownedRecovery: true)).ToArray();
        var terminalRequests = terminal.Operations.Select(operation =>
            new WoundTerminalRequest(batch, before, operation, terminalRoots.ToImmutableArray())).ToArray();
        ValidateWoundTerminalRequests(terminalRequests, EffectCarrierCatalog.Build(workspace.ToInput()),
            currentIdentity.State, issues);
        if (issues.Count != 0) return null;
        var terminations = new List<EffectAcceptedTerminationResult>();
        foreach (var request in terminalRequests.OrderBy(row => row.Operation.OperationOrdinal))
        {
            ApplyWoundTerminalOperation(request, workspace, currentIdentity.State, identityRoot, identityFactory,
                turn, transitionIds, activeEffects, processedEvents, issues);
            if (issues.Count != 0) return null;
            var parsed = ParseIdentity(identityRoot.ReadSnapshot());
            issues.AddRange(parsed.Issues);
            if (parsed.State is null || !parsed.State.TryGetEntry(request.Operation.EffectId, out var retired))
                throw new InvalidOperationException("Recovery terminal identity disappeared.");
            var actual = retired.Transitions.Last();
            if (retired.State != "expired" || actual.Kind != "expire" || actual.EventRef != request.Operation.OperationRef)
                throw new InvalidOperationException("Recovery terminal write differs from its operation.");
            var operation = request.Operation;
            terminations.Add(new(operation.OperationRef, "expired", operation.EffectId, actual.TransitionId,
                actual.EventRef, operation.CausalEventRef, operation.ExpectedSourceKey,
                operation.ExpectedTargetKey, operation.ExpectedCarrierCoordinate));
        }
        var executed = new List<(WoundApplicationRequest Request, ApplicationExecutionFacts Facts)>();
        foreach (var request in requests)
        {
            if (request is null) throw new InvalidOperationException("Recovery application did not validate.");
            var facts = ApplyApplication(request.Application, input.Realm, input.EventInput, workspace, identityRoot,
                identityFactory, turn, effectIds, transitionIds, activeEffects, processedEvents, issues,
                request.Root.PriorRootEffectId is null ? ApplicationProvenance.Direct :
                    new ApplicationProvenance.SeverityGeneration(request.Root.PriorRootEffectId),
                skillScopeAuthority: input.SkillScopeAuthority);
            if (facts is null || issues.Count != 0) return null;
            ValidateWoundApplicationExecution(request, facts, issues);
            executed.Add((request, facts));
            usedSources.Add(request.Application.Source);
            usedTargets.Add(request.Application.Target);
            rootBindings.Add(new(request.Root.ApplicationRef, facts.EffectId));
        }
        ValidateAfterImages(workspace, identityRoot, activeEffects, issues);
        var resultingCatalog = EffectCarrierCatalog.Build(workspace.ToInput());
        var resultingIdentity = ParseIdentity(identityRoot.ReadSnapshot());
        issues.AddRange(resultingCatalog.Issues);
        issues.AddRange(resultingIdentity.Issues);
        if (issues.Count != 0 || resultingIdentity.State is null) return null;
        foreach (var (_, facts) in executed)
            if (!resultingCatalog.TryResolveOne(facts.EffectId, out var occurrence) ||
                !resultingIdentity.State.TryGetEntry(facts.EffectId, out var actualIdentity) ||
                !JsonNode.DeepEquals(occurrence.Effect, facts.CreatedEffect) ||
                !JsonNode.DeepEquals(actualIdentity.Raw, facts.CreatedIdentityEntry))
                throw new InvalidOperationException("Recovery created root did not survive its own boundary exactly.");
        var allocatedByRef = executed.ToDictionary(row => row.Request.Root.ApplicationRef, row => row.Facts.EffectId, StringComparer.Ordinal);
        string RootId(WoundWorkingReference reference) => applicationReferences.TryGetValue(reference, out var app)
            ? allocatedByRef[app] : reference.Value;
        var orderedEntries = coordinates.IsMortalRecovery || changedSeverity
            ? graph.Entries.OrderBy(row => RootId(row.Root), StringComparer.Ordinal).ThenBy(row => row.Slot)
            : graph.Entries.OrderBy(row => row.Slot);
        var entries = orderedEntries.Select((row, slot) =>
            new WoundConsequenceEntry(slot + 1, row.ProfileKey, RootId(row.Root), row.ReadableSummary)).ToArray();
        var applicationResults = executed.Select(row => new EffectAcceptedApplicationResult(row.Request.Root.ApplicationRef,
            row.Facts.Disposition, row.Facts.EffectId, row.Facts.TransitionId, row.Facts.CreatedEventRef!, row.Facts.CausalEventRef,
            row.Facts.Source, row.Facts.Target, row.Facts.CarrierCoordinate, new WoundEffectMaterializationAgreement(
                entries.Where(entry => entry.EffectId == row.Facts.EffectId).Select(entry =>
                    new WoundEffectSlotAgreement(entry.Slot, entry.ProfileKey, entry.ReadableSummary)).ToArray(),
                row.Request.Root.ExpectedComponentCount, row.Request.Root.ExpectedMaterializationFingerprint))).ToArray();
        var definitionBodies = graph.Definitions.Select(row => row.Definition.Clone()).ToArray();
        var complications = terminalHeal ? before.Complications : graph.Complications.Select(row =>
            new WoundComplication(complicationIds[row.Reference], row.Kind, row.State, row.DisplayName,
                row.TreatmentDifficultyModifier, row.OwnedRoots.Select(RootId).ToArray(), row.Visibility)).ToArray();
        var consequences = terminalHeal ? before.Consequences : new WoundConsequences(slotBudget, entries.Length, entries)
        {
            OwnedEffectSources = new WoundOwnedEffectSources(definitionBodies,
                graph.Roots.Select(row => new WoundRootEffectBinding(RootId(row.Reference), row.DefinitionKey)).ToArray())
            { DefinitionFacts = WoundMaterializationContract.BuildOwnedEffectDefinitionFacts(definitionBodies) }
        };
        return new(complications, consequences, applicationResults, terminations);
    }
}
