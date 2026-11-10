using System.Text.Json.Nodes;

namespace BookOfEternityClient.Services;

internal static partial class WoundAcceptedTurnPlannerCore
{
    /// <summary>
    /// Compares the complete independent legacy source set with the existing
    /// private treatment preparation. Unrelated, empty or foreign legacy owner
    /// exports cannot extend the ordinary same-turn wound source authority.
    /// </summary>
    private static bool TreatmentLegacySourcesAgree(WoundPreparedAcceptedTurnPlan prepared, EffectSourceAuthority authority)
    {
        var entries = authority.SnapshotSameTurnLegacyEntries();
        var owners = authority.SnapshotSameTurnLegacyOwners();
        var batches = prepared.EffectOperationBatches.Where(batch => batch.SourceExport.Kind == "wound_legacy").ToArray();
        if (!WoundAcceptedTurnPlanner.TryReadTreatmentLegacyPreparation(prepared, out var legacy))
            return batches.Length == 0 && entries.Count == 0 && owners.Count == 0;
        if (legacy is null) return false;
        try { MortalWoundHealLegacyPlanner.RequireExactBatches(legacy, batches); }
        catch (InvalidDataException) { return false; }
        if (owners.Count != batches.Length || entries.Count != batches.Sum(batch => batch.SourceExport.Definitions.Count))
            return false;
        foreach (var batch in batches)
        {
            var source = batch.SourceExport;
            var matchingOwners = owners.Where(owner => owner.Realm == source.Realm && owner.Kind == "wound_legacy" &&
                owner.SourceId == source.SourceId).ToArray();
            if (matchingOwners.Length != 1) return false;
            var owner = matchingOwners[0];
            if (owner.Materializable || !owner.Active || !owner.SameTurn || owner.SourceRef != source.SourceRef ||
                !JsonNode.DeepEquals(owner.Definitions, new JsonArray(source.Definitions.Select(row => (JsonNode)row.Definition).ToArray())))
                return false;
            foreach (var definition in source.Definitions)
            {
                var key = new EffectSourceKey(source.Realm, "wound_legacy", source.SourceId, definition.DefinitionKey);
                var matches = entries.Where(entry => entry.Key == key).ToArray();
                if (matches.Length != 1 || matches[0].Materializable || !matches[0].Active || !matches[0].SameTurn ||
                    matches[0].SourceRef != source.SourceRef || !JsonNode.DeepEquals(matches[0].Definition, definition.Definition))
                    return false;
            }
        }
        return true;
    }
}
