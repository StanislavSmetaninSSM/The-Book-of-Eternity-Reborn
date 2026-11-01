using System.Text.Json.Nodes;

namespace BookOfEternityClient.Services;

internal static partial class MortalWoundTreatmentCompletion
{
    private static void RequireTerminalLegacyEffects(MortalWoundTreatmentPersistedResult.GuidedComparison comparison,
        MortalWoundHealOutcomeIntent heal, MortalWoundHealOperation declared, CurrentCanonicalAgreement current)
    {
        var request = comparison.Resolution.RequestAuthority;
        var coordinates = request.Coordinates;
        if (!comparison.Preparation.TryRecomposeSelectedGraph(out var selected) || selected is null)
            throw Invalid("Legacy current proof lost its exact treatment prefix.");
        var prefixCreates = selected.FinalRoots.Count(row => selected.FinalSeverityChanged || row.AdditionBinding is not null);
        var firstMechanics = prefixCreates == 0 ? 1 : 2;
        var allocated = new HashSet<string>(StringComparer.Ordinal);
        for (var ordinal = 0; ordinal < declared.Legacies.Length; ordinal++)
        {
            var draft = declared.Legacies[ordinal];
            var seed = heal.LegacySeedBindings[ordinal];
            var group = new EffectIdentitySourceGroup(request.RouteSourceWound.Owner.Realm, "wound_legacy", seed.LegacyId);
            var members = current.EffectIndex.ResolveSourceGroup(group);
            if (draft is MortalWoundCosmeticLegacyDraft)
            {
                if (members.Count != 0) throw Invalid("A cosmetic legacy unexpectedly owns effect identities.");
                continue;
            }
            if (draft is not MortalWoundMechanicalEffectLegacyDraft mechanical) throw Invalid("Unknown terminal legacy kind.");
            var described = MortalWoundHealLegacyPlanner.DescribeMechanicalApplications(mechanical, seed,
                request.RouteSourceWound.Owner, coordinates.EventRef, checked(firstMechanics + ordinal));
            var definitions = described.Definitions.ToDictionary(row => row.DefinitionKey, row => row.Definition, StringComparer.Ordinal);
            var roots = new List<WoundEffectIdentityLineageRoot>();
            foreach (var expected in described.Roots)
            {
                var creationEvent = WoundEffectOperationEventRef.Create(expected.CausalEventRef,
                    expected.MechanicsOrdinal, expected.OperationOrdinal, expected.OperationKind);
                var candidates = current.EffectIndex.Entries.Where(entry => entry.Transitions.Count > 0 &&
                    entry.Transitions[0].Kind == "create" && entry.Transitions[0].EventRef == creationEvent).ToArray();
                if (candidates.Length != 1) throw Invalid("Primary legacy allocation is missing, duplicated or foreign.");
                var entry = candidates[0]; var creation = entry.Transitions[0];
                if (!allocated.Add(entry.EffectId) || entry.CreatedAtTurn != coordinates.Turn || creation.Turn != coordinates.Turn ||
                    creation.SourceEffectIds.Count != 0 || !creation.ResultEffectIds.SequenceEqual(new[] { entry.EffectId }, StringComparer.Ordinal) ||
                    creation.ReceiptId is not null || entry.Transitions.Any(row => row.Turn > coordinates.Turn))
                    throw Invalid("Primary legacy identity or complete chronology changed.");
                RequireIdentityCoordinates(request.RouteSourceWound.Owner, "wound_legacy", seed.LegacyId,
                    expected.DefinitionKey, definitions[expected.DefinitionKey], entry);
                roots.Add(new(entry.EffectId, expected.DefinitionKey, WoundRootOwnershipDomain.BaseWound));
                if (entry.State is "active" or "suspended")
                {
                    if (!current.Effects.TryResolveOne(entry.EffectId, out var occurrence) || occurrence.Coordinate != expected.ExpectedCarrierCoordinate ||
                        occurrence.Effect["components"] is not JsonArray actual || occurrence.Effect["chronology"] is not JsonObject chronology ||
                        chronology["createdEventRef"]?.GetValue<string>() != creationEvent ||
                        chronology["causalEventRef"]?.GetValue<string>() != coordinates.EventRef ||
                        chronology["createdAtTurn"]?.GetValue<int>() != coordinates.Turn ||
                        WoundEffectMaterializationFingerprint.Compute(expected.ExpectedSourceKey,
                            EffectMaterializationContract.SchemaVersion, expected.Parameters, actual) != expected.ExpectedMaterializationFingerprint)
                        throw Invalid("Surviving primary legacy materialization differs from its exact declared parameters.");
                }
                else if (current.Effects.Occurrences.Any(row => row.EffectId == entry.EffectId))
                    throw Invalid("A terminal primary legacy identity was resurrected in a carrier.");
            }
            var facts = WoundMaterializationContract.BuildOwnedEffectDefinitionFacts(definitions.Values.Select(value =>
                System.Text.Json.JsonSerializer.SerializeToElement(value)).ToArray());
            var domains = facts.ToDictionary(row => row.DefinitionKey, row => new WoundEffectIdentityLineageDefinition(row.DefinitionKey,
                row.ApplyDefinitionTargets.ToHashSet(StringComparer.Ordinal), WoundRootOwnershipDomain.BaseWound), StringComparer.Ordinal);
            var lineage = WoundEffectIdentityLineageAnalyzer.Analyze(group, current.EffectIndex, roots, domains,
                WoundEffectLineageDiagnosticProfile.AcceptedMechanics);
            if (lineage.Issues.Count != 0 || lineage.CurrentEntries.Count + lineage.RetiredEntries.Count != members.Count ||
                members.Any(entry => !definitions.ContainsKey(entry.Source["definitionKey"]!.GetValue<string>())))
                throw Invalid("Legacy source-group members have unrooted, foreign, ambiguous or cyclic lineage.");
            // All members, including ordinary descendants, already passed the
            // complete fresh canonical source-definition/parameter bounds,
            // target, owner, carrier and index agreement. Descendants are not
            // compared with a primary parameter envelope and no historical
            // child-parameter replay is inferred from current values.
        }
    }
}
