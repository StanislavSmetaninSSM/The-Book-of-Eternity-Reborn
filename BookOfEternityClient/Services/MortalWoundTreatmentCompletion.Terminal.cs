using System.Text.Json.Nodes;

namespace BookOfEternityClient.Services;

internal static partial class MortalWoundTreatmentCompletion
{
    private static void RequireIdentityCoordinates(WoundOwnerCoordinate owner, string kind, string sourceId,
        string definitionKey, JsonObject definition, EffectIdentityEntry entry)
    {
        if (!WoundEffectCarrierAdapter.TryCreateTargetKey(owner, out var target) ||
            !WoundEffectCarrierAdapter.TryCreateCarrierCoordinate(owner, target, definition, out var carrier) ||
            !WoundEffectTerminalOperationPlanner.TryCreateExpectedIdentityOwner(target, carrier, out var expectedOwner) ||
            definition["stacking"]?["stackKey"]?.GetValue<string>() is not { } stackKey ||
            entry.Realm != owner.Realm || entry.Owner != expectedOwner ||
            entry.Source["kind"]?.GetValue<string>() != kind || entry.Source["sourceId"]?.GetValue<string>() != sourceId ||
            entry.Source["definitionKey"]?.GetValue<string>() != definitionKey ||
            entry.Target["kind"]?.GetValue<string>() != target.Kind || entry.Target["targetId"]?.GetValue<string>() != target.TargetId ||
            entry.StackCoordinate != new EffectStackCoordinate(owner.Realm, target.Kind, target.TargetId, kind, sourceId, stackKey))
            throw Invalid("A terminal identity changed its complete source/target/owner/carrier/stack coordinate.");
    }

    private static void RequireTerminalTreatmentSettlement(MortalWoundTreatmentPersistedResult.GuidedComparison comparison,
        WoundMaterializationEnvelope treated, WoundHistoryState history, WoundHistoryTransition primary,
        WoundIdentityState identities, WoundCarrierCatalog carriers, CurrentCanonicalAgreement current)
    {
        var resolution = comparison.Resolution;
        var request = resolution.RequestAuthority;
        var coordinates = request.Coordinates;
        var heal = comparison.Preparation.ReadTerminalHealIntent() ?? throw Invalid("Terminal current proof lost its typed heal intent.");
        var declared = resolution.DeclaredResult.LastOrDefault() as MortalWoundHealOperation ?? throw Invalid("Missing declared terminal heal.");
        var declarations = MortalWoundTreatmentOutcomePublicationPlanner.CreateTerminalDeclarations(heal, declared, coordinates.WoundId);
        var reduced = MortalWoundTreatmentOutcomePublicationPlanner.ReduceTerminalHeal(treated, heal, declarations, coordinates.Turn);
        if (!reduced.IsValid || reduced.ProposedAfter is not { } healed) throw Invalid("Terminal healed snapshot does not reconstruct.");
        var treatedFingerprint = WoundIdentityState.ComputeSemanticFingerprint(treated);
        var healedFingerprint = WoundIdentityState.ComputeSemanticFingerprint(healed);
        var children = history.Transitions.Where(row => row.TransitionId == heal.HealChildCoordinates.TransitionId).ToArray();
        if (primary.Kind != "treat" || primary.Terminal || primary.TransitionId != treated.LastTransition.TransitionId ||
            primary.WoundTransitionOrdinal != treated.LastTransition.Ordinal || primary.AfterFingerprint != treatedFingerprint ||
            primary.BeforeFingerprint != coordinates.ExpectedBeforeFingerprint || primary.SourceFingerprint != request.RequestFingerprint ||
            primary.Turn != coordinates.Turn || children.Length != 1)
            throw Invalid("Primary terminal treatment History differs from the exact reconstructed stage.");
        var child = children[0];
        if (child.Kind != "heal" || !child.Terminal || child.WoundId != coordinates.WoundId || child.Turn != coordinates.Turn ||
            child.Ordinal != checked(primary.Ordinal + 1) || child.WoundTransitionOrdinal != healed.LastTransition.Ordinal ||
            child.OperationKey != heal.HealChildCoordinates.OperationKey || child.EventRef != heal.HealChildCoordinates.EventRef ||
            child.BeforeFingerprint != treatedFingerprint || child.AfterFingerprint != healedFingerprint ||
            child.SourceFingerprint != request.RequestFingerprint || child.AttemptId is not null || child.CycleKey is not null ||
            child.TransitionResult is not null || history.Transitions.Last(row => row.WoundId == coordinates.WoundId) != child ||
            carriers.Occurrences.Any(row => row.WoundId == coordinates.WoundId) ||
            !identities.TryGetEntry(coordinates.WoundId, out var identity) || identity.Status != "healed" ||
            identity.TerminalTransitionId != child.TransitionId || identity.LastTransitionOrdinal != healed.LastTransition.Ordinal ||
            identity.SemanticFingerprint != healedFingerprint || identity.Realm != healed.Owner.Realm ||
            identity.OwnerKind != healed.Owner.OwnerKind || identity.OwnerId != healed.Owner.OwnerId || identity.CarrierPath != healed.Owner.CarrierPath)
            throw Invalid("The complete linked heal/terminal identity/carrier absence differs from actual settlement.");
        RequireTerminalWoundEffects(comparison, treated, current);
        RequireTerminalLegacyEffects(comparison, heal, declared, current);
    }

    private static void RequireTerminalWoundEffects(MortalWoundTreatmentPersistedResult.GuidedComparison comparison,
        WoundMaterializationEnvelope treated, CurrentCanonicalAgreement current)
    {
        var before = comparison.Preparation.Before;
        var coordinates = comparison.Resolution.Coordinates;
        if (!comparison.Preparation.TryRecomposeSelectedGraph(out var selected) || selected is null)
            throw Invalid("Terminal comparison lost its exact ordered graph.");
        var definitions = new Dictionary<string, JsonObject>(StringComparer.Ordinal);
        var domains = new Dictionary<string, WoundEffectIdentityLineageDefinition>(StringComparer.Ordinal);
        foreach (var wound in new[] { before, treated })
        {
            foreach (var row in WoundEffectLineagePlanner.DescribeDefinitionDomains(wound))
            {
                if (domains.TryGetValue(row.Key, out var prior) && prior.OwnershipDomain != row.Value.OwnershipDomain)
                    throw Invalid("Terminal definition ownership changed between actual stages.");
                domains[row.Key] = row.Value;
            }
            foreach (var row in wound.Consequences.OwnedEffectSources.DefinitionFacts)
            {
                var definition = JsonNode.Parse(row.CanonicalJson)!.AsObject();
                if (definitions.TryGetValue(row.DefinitionKey, out var prior) && !JsonNode.DeepEquals(prior, definition))
                    throw Invalid("Terminal definition body changed between actual stages.");
                definitions[row.DefinitionKey] = definition;
            }
        }
        var finalKeys = treated.Consequences.OwnedEffectSources.RootBindings.Select(row => row.DefinitionKey).ToHashSet(StringComparer.Ordinal);
        var roots = treated.Consequences.OwnedEffectSources.RootBindings.Concat(before.Consequences.OwnedEffectSources.RootBindings
                .Where(row => !finalKeys.Contains(row.DefinitionKey)))
            .Select(row => new WoundEffectIdentityLineageRoot(row.EffectId, row.DefinitionKey, domains[row.DefinitionKey].OwnershipDomain)).ToArray();
        var group = new EffectIdentitySourceGroup(before.Owner.Realm, "wound", before.WoundId);
        var members = current.EffectIndex.ResolveSourceGroup(group);
        var analysis = WoundEffectIdentityLineageAnalyzer.Analyze(group, current.EffectIndex, roots, domains,
            WoundEffectLineageDiagnosticProfile.AcceptedMechanics);
        if (analysis.Issues.Count != 0 || analysis.CurrentEntries.Count + analysis.RetiredEntries.Count != members.Count)
            throw Invalid("Terminal effect identities contain missing, extra, foreign or ambiguous lineage.");
        foreach (var entry in members)
        {
            var key = entry.Source["definitionKey"]!.GetValue<string>();
            if (!definitions.TryGetValue(key, out var definition) || entry.State is "active" or "suspended" ||
                current.Effects.Occurrences.Any(row => row.EffectId == entry.EffectId) || entry.Transitions.Count == 0 ||
                entry.Transitions.Any(row => row.Turn > coordinates.Turn))
                throw Invalid("A healed source has an unproven identity, future transition or resurrected carrier.");
            RequireIdentityCoordinates(before.Owner, "wound", before.WoundId, key, definition, entry);
        }
        var creationCount = selected.FinalRoots.Count(row => selected.FinalSeverityChanged || row.AdditionBinding is not null);
        var newIds = new HashSet<string>(StringComparer.Ordinal);
        for (var ordinal = 1; ordinal <= creationCount; ordinal++)
        {
            var eventRef = WoundEffectOperationEventRef.Create(coordinates.EventRef, 1, ordinal, "apply");
            var mapped = members.Where(entry => entry.Transitions[0].Kind == "create" && entry.Transitions[0].EventRef == eventRef).ToArray();
            if (mapped.Length != 1 || mapped[0].CreatedAtTurn != coordinates.Turn || mapped[0].Transitions.Count != 2 ||
                !treated.Consequences.OwnedEffectSources.RootBindings.Any(row => row.EffectId == mapped[0].EffectId) || !newIds.Add(mapped[0].EffectId))
                throw Invalid("The exact newly allocated-and-retired root mapping changed.");
        }
        var prefixEvents = Enumerable.Range(1, members.Count).Select(index =>
            WoundEffectOperationEventRef.Create(coordinates.EventRef, 1, checked(creationCount + index), "expire")).ToHashSet(StringComparer.Ordinal);
        var healEvents = Enumerable.Range(1, members.Count).Select(index =>
            WoundEffectOperationEventRef.Create(coordinates.EventRef, 2, index, "expire")).ToHashSet(StringComparer.Ordinal);
        var prefix = members.Where(entry => prefixEvents.Contains(entry.Transitions[^1].EventRef)).OrderBy(entry => entry.EffectId, StringComparer.Ordinal).ToArray();
        var terminal = members.Where(entry => healEvents.Contains(entry.Transitions[^1].EventRef)).OrderBy(entry => entry.EffectId, StringComparer.Ordinal).ToArray();
        var prefixRoots = selected.SelectedTerminalRootIds.ToHashSet(StringComparer.Ordinal);
        if (prefixRoots.Any(id => !prefix.Any(entry => entry.EffectId == id)) ||
            treated.Consequences.OwnedEffectSources.RootBindings.Any(root => !terminal.Any(entry => entry.EffectId == root.EffectId)) ||
            newIds.Any(id => !terminal.Any(entry => entry.EffectId == id)))
            throw Invalid("A selected prefix or heal root lacks its exact terminal operation.");
        bool DescendsFrom(string id, IReadOnlySet<string> allowed)
        {
            var seen = new HashSet<string>(StringComparer.Ordinal);
            while (seen.Add(id) && current.EffectIndex.TryGetEntry(id, out var entry))
            {
                if (allowed.Contains(id)) return true;
                var parents = entry.Transitions[0].SourceEffectIds;
                if (parents.Count != 1) return false;
                id = parents[0];
            }
            return false;
        }
        var finalRoots = treated.Consequences.OwnedEffectSources.RootBindings.Select(row => row.EffectId).ToHashSet(StringComparer.Ordinal);
        void RequireTerminations(IReadOnlyList<EffectIdentityEntry> entries, int mechanics, int offset, IReadOnlySet<string> allowed)
        {
            for (var index = 0; index < entries.Count; index++)
            {
                var entry = entries[index]; var last = entry.Transitions[^1];
                if (!DescendsFrom(entry.EffectId, allowed) || entry.State != "expired" || last.Kind != "expire" || last.Turn != coordinates.Turn ||
                    last.EventRef != WoundEffectOperationEventRef.Create(coordinates.EventRef, mechanics, checked(offset + index + 1), "expire") ||
                    !last.SourceEffectIds.SequenceEqual(new[] { entry.EffectId }, StringComparer.Ordinal) || last.ResultEffectIds.Count != 0 || last.ReceiptId is not null)
                    throw Invalid("Terminal event order, ownership, chronology or complete effect identity chain changed.");
            }
        }
        RequireTerminations(prefix, 1, creationCount, prefixRoots);
        RequireTerminations(terminal, 2, 0, finalRoots);
    }
}
