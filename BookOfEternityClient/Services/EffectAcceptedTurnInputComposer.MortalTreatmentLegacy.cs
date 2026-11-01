using System.Text.Json.Nodes;

namespace BookOfEternityClient.Services;

internal static partial class EffectAcceptedTurnInputComposer
{
    /// <summary>
    /// Reconstructs only non-public independent legacy definitions from complete
    /// canonical terminal History. Removing the wound never erases these typed
    /// declarations; historical DATA does not grant new application authority.
    /// </summary>
    private static CanonicalWoundSourceComposition CollectCanonicalTreatmentLegacySources(
        IReadOnlyDictionary<string, JsonNode?> roots, WoundCarrierCatalog catalog, bool sameTurn)
    {
        var exports = new List<EffectSourceExport>();
        var issues = new List<ValidationIssue>();
        if (!roots.TryGetValue(WoundHistoryState.HistoryPath, out var historyNode) || historyNode is null)
            return CanonicalWoundSourceComposition.Empty;
        var history = WoundHistoryState.Parse(historyNode.ToJsonString(), WoundHistoryState.HistoryPath);
        if (!history.IsValid)
            return new(Array.Empty<EffectSourceExport>(), Array.Empty<WoundSourceGroupAuthority>(), history.Issues);
        var selected = history.State!.Transitions.Where(row => row.TreatmentResult is { } result &&
            result.Receipt.DeclaredResult.LastOrDefault() is MortalWoundHealOperation { Legacies.Length: > 0 }).ToArray();
        if (selected.Length == 0) return CanonicalWoundSourceComposition.Empty;
        var identity = WoundIdentityState.Parse(roots.GetValueOrDefault(WoundIdentityState.StatePath)?.ToJsonString(), WoundIdentityState.StatePath);
        if (!identity.IsValid || catalog.Issues.Count != 0 || history.State.ValidateAgreement(identity.State!, catalog).Count != 0)
        {
            issues.Add(WoundCompositionIssue("effect_source_wound_legacy_history_invalid",
                "Legacy definitions require complete current wound History/identity/carrier agreement.",
                "one canonical terminal provenance chain", "missing or changed terminal owner evidence"));
            return new(exports, Array.Empty<WoundSourceGroupAuthority>(), issues);
        }
        var ids = new HashSet<string>(StringComparer.Ordinal);
        foreach (var primary in selected)
        {
            var result = primary.TreatmentResult!;
            var request = result.Request;
            var heal = (MortalWoundHealOperation)result.Receipt.DeclaredResult[^1];
            var operationOrdinal = result.Receipt.DeclaredResult.Count - 1;
            var child = MortalWoundHealChildCoordinates.Create(request.RequestFingerprint, operationOrdinal);
            var terminal = history.State.Transitions.Where(row => row.TransitionId == child.TransitionId).ToArray();
            if (terminal.Length != 1 || terminal[0].WoundId != primary.WoundId || terminal[0].Kind != "heal" || !terminal[0].Terminal ||
                terminal[0].Ordinal != checked(primary.Ordinal + 1) || terminal[0].WoundTransitionOrdinal != checked(primary.WoundTransitionOrdinal + 1) ||
                terminal[0].Turn != primary.Turn || terminal[0].OperationKey != child.OperationKey || terminal[0].EventRef != child.EventRef ||
                terminal[0].BeforeFingerprint != primary.AfterFingerprint || terminal[0].SourceFingerprint != request.RequestFingerprint ||
                terminal[0].AttemptId is not null || terminal[0].TransitionResult is not null)
            {
                issues.Add(WoundCompositionIssue("effect_source_wound_legacy_terminal_link_invalid",
                    "Independent legacy sources require the exact executed child heal, never a declared-only primary row.",
                    child.TransitionId, primary.TransitionId));
                continue;
            }
            for (var ordinal = 0; ordinal < heal.Legacies.Length; ordinal++)
            {
                var draft = heal.Legacies[ordinal];
                var seed = MortalWoundTreatmentLegacySeedBinding.Create(request.RequestFingerprint, operationOrdinal, ordinal, draft);
                if (!ids.Add(MortalLocationIdentityState.BuildConfusableKey(seed.LegacyId)))
                {
                    issues.Add(WoundCompositionIssue("effect_source_wound_legacy_duplicate", "Legacy provenance IDs must be unique.",
                        "one exact independent legacy owner", seed.LegacyId));
                    continue;
                }
                if (draft is not MortalWoundMechanicalEffectLegacyDraft mechanical) continue;
                // Complete definitions are retained by the strictly parsed request;
                // no current candidate effect or unsealed after-image supplies them.
                var definitions = new JsonArray(mechanical.EffectDraft.Definitions.Select(definition =>
                    JsonNode.Parse(definition.Definition.GetRawText())!).ToArray());
                exports.Add(new(request.RouteSourceWound.Owner.Realm, "wound_legacy", seed.LegacyId, definitions,
                    Materializable: false, Active: true, SameTurn: sameTurn,
                    SatisfiedPredicates: new HashSet<string>(StringComparer.Ordinal) { "active" }));
            }
        }
        return new(exports, Array.Empty<WoundSourceGroupAuthority>(), issues);
    }
}
