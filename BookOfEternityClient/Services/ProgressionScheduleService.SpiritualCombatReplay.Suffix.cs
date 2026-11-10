using System.Text.Json.Nodes;

namespace BookOfEternityClient.Services;

public partial class ProgressionScheduleService
{
    /// <summary>
    /// Joins later explicit re-trauma to the exact resolved healing and current canonical wound.
    /// All inputs are DATA read under the caller's current lease; this creates no mutation authority.
    /// </summary>
    private static void RequireLaterCombatRetraumaHistory(
        SpiritualWoundOpportunityReceiptState ledger, WoundHistoryState history, WoundCarrierCatalog catalog,
        JsonObject entry, SpiritualCombatHealingPersistedResult.SourceData healing,
        WoundMaterializationEnvelope healed, string sessionId, string requestId, int turn)
    {
        if (healed.Lifecycle != "active" || !catalog.TryResolveOne(healed.WoundId, out var current) ||
            current.Wound.Owner != healed.Owner)
            throw new InvalidDataException("Later combat re-trauma requires the same active healed wound.");
        var suffix = history.Transitions.Where(row => row.WoundId == healed.WoundId &&
            row.WoundTransitionOrdinal > healed.LastTransition.Ordinal).ToArray();
        var (sourceRows, decisionRows) = ledger.ReadDetachedInjuryRows();
        var instanceId = entry["instanceId"]!.GetValue<string>();
        var snapshotToken = entry["witness"]!["snapshotToken"]!.GetValue<string>();
        bool SameOriginal(JsonNode binding) => binding["sessionId"]!.GetValue<string>() == sessionId &&
            binding["requestId"]!.GetValue<string>() == requestId &&
            binding["snapshotToken"]!.GetValue<string>() == snapshotToken && binding["turn"]!.GetValue<int>() == turn;
        var decisions = decisionRows.Where(row =>
            row["instanceId"]!.GetValue<string>() == instanceId && row["decision"]!.GetValue<string>() == "materialize" &&
            row["woundId"]?.GetValue<string>() == healed.WoundId && SameOriginal(row["binding"]!))
            .ToDictionary(row => row["transitionId"]!.GetValue<string>(), StringComparer.Ordinal);
        if (suffix.Length == 0 || suffix.Length > 4 - healed.Severity.Rank || decisions.Count != suffix.Length)
            throw new InvalidDataException("Later combat History lacks its exact receipted transition set.");
        var sources = sourceRows.ToDictionary(
            row => row["witness"]!["sourceId"]!.GetValue<string>(), StringComparer.Ordinal);
        var rows = new List<(JsonObject Decision, JsonObject Harm, WoundMaterializationEnvelope Before)>(suffix.Length);
        var lastExchange = entry["witness"]!["turnExchangeOrdinal"]!.GetValue<int>();
        var lastWave = entry["outcome"]!["waveOrdinal"]!.GetValue<int>();
        var lastGeneration = 1;
        foreach (var change in suffix)
        {
            if (!decisions.TryGetValue(change.TransitionId, out var decision) ||
                !sources.TryGetValue(decision["sourceId"]!.GetValue<string>(), out var row))
                throw new InvalidDataException("Later combat History lost its unique materialize/source join.");
            var harm = row["witness"]!.AsObject();
            var exchange = harm["exchangeOrdinal"]!.GetValue<int>();
            var wave = decision["binding"]!["waveOrdinal"]!.GetValue<int>();
            var generation = decision["binding"]!["continuationGeneration"]!.GetValue<int>();
            if (row["instanceId"]!.GetValue<string>() != instanceId || !SameOriginal(harm["turnEvidence"]!) ||
                harm["conflictId"]!.GetValue<string>() != healing.Intent.ConflictId ||
                harm["affectedSide"]!.GetValue<string>() != "player" ||
                harm["retraumaWoundId"]?.GetValue<string>() != healed.WoundId ||
                exchange <= lastExchange || wave <= lastWave || generation <= lastGeneration)
                throw new InvalidDataException("Later combat re-trauma changed its original, target or causal position.");
            // The containing ledger already validated the source and claims. Parse only the
            // selected retained wound once to compare its complete envelope with the chain.
            var before = WoundMaterializationContract.Parse(CanonicalJsonUtf8.DecodeOneOptionalBom(
                Convert.FromBase64String(harm["retraumaWoundJsonBase64"]!.GetValue<string>())), "combat.later.original");
            if (!before.IsValid || before.Wound is not { } retained || retained.Lifecycle != "active" ||
                retained.WoundId != healed.WoundId || retained.Owner != healed.Owner)
                throw new InvalidDataException("Later combat re-trauma lost its retained active wound.");
            rows.Add((decision, harm, retained));
            lastExchange = exchange; lastWave = wave; lastGeneration = generation;
        }
        if (WoundMaterializationContract.SerializeCanonical(rows[0].Before) != WoundMaterializationContract.SerializeCanonical(healed))
            throw new InvalidDataException("Later combat re-trauma did not start from the exact healing result.");
        // Transient indexes and selected preimages live only for this read. Compute each
        // state fingerprint once; no repeated source reads or persistent cache is introduced.
        var states = rows.Select(row => row.Before).Append(current.Wound).ToArray();
        var fingerprints = states.Select(WoundIdentityState.ComputeSemanticFingerprint).ToArray();
        for (var index = 0; index < suffix.Length; index++)
        {
            var change = suffix[index]; var before = states[index]; var after = states[index + 1];
            var (decision, harm, _) = rows[index];
            var (eventRef, authority) = ReadCombatRetraumaAuthority(harm, decision, before,
                sessionId, requestId, snapshotToken, healing.Intent.ConflictId, turn);
            if (change.Kind != "worsen" || change.Turn != turn || change.EventRef != eventRef ||
                change.WoundTransitionOrdinal != before.LastTransition.Ordinal + 1 ||
                change.BeforeFingerprint != fingerprints[index] || change.AfterFingerprint != fingerprints[index + 1] ||
                after.LastTransition.TransitionId != change.TransitionId || after.LastTransition.Kind != "worsen" ||
                after.LastTransition.Ordinal != change.WoundTransitionOrdinal || after.LastTransition.Turn != turn ||
                after.WoundId != healed.WoundId || after.Owner != healed.Owner || after.Lifecycle != "active" ||
                decision["selectedSeverityRank"]!.GetValue<int>() != after.Severity.Rank || after.Severity.Rank <= before.Severity.Rank ||
                decision["sourceFingerprint"]!.GetValue<string>() != harm["sourceFingerprint"]!.GetValue<string>() ||
                change.SourceFingerprint != authority || after.Recovery.CurrentStepProgress != 0 ||
                after.Recovery.LastTickKey != before.Recovery.LastTickKey)
                throw new InvalidDataException("Later combat History lacks its exact independently receipted re-trauma chain.");
        }
    }
}
