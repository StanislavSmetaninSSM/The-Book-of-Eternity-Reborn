using System.Text.Json;
using System.Text.Json.Nodes;

namespace BookOfEternityClient.Services;

// One closed domain record on the existing persistent profile. Serialized DATA
// alone is never admission: staging must prove its actual accepted result.
internal static class SpiritualTreatmentParticipation
{
    internal const string ChangeProperty = "spiritualTreatmentParticipationChange";
    internal const string RecordProperty = "spiritualTreatmentParticipation";
    internal sealed record Change(string TargetId, JsonObject Proposal);

    internal static IReadOnlyList<Change> ReadChanges(JsonObject current, JsonObject previous)
    {
        var changes = new List<Change>();
        foreach (var property in new[] { AfterlifeEntityProfileState.ProfilesProperty,
                     AfterlifeEntityProfileState.ResponseProfilesProperty, AfterlifeEntityProfileState.UpdateProperty })
        {
            if (current[property] is not JsonArray rows) continue; // Ordinary shape validation owns non-arrays.
            foreach (var row in rows.OfType<JsonObject>())
            {
                var hasChange = row.ContainsKey(ChangeProperty);
                var hasRecord = row.ContainsKey(RecordProperty);
                if (!hasChange && !hasRecord) continue;
                var id = Text(row, "actorId");
                var kind = Text(row, "actorType");
                var original = (previous[AfterlifeEntityProfileState.ProfilesProperty] as JsonArray)?.OfType<JsonObject>()
                    .Where(p => p["actorId"]?.GetValue<string>() == id && p["actorType"]?.GetValue<string>() == kind).ToArray();
                if (original is not { Length: 1 }) throw new InvalidDataException("Participation requires an exact preexisting persistent profile.");
                if (hasRecord && !JsonNode.DeepEquals(row[RecordProperty], original[0][RecordProperty]))
                    throw new InvalidDataException("Raw profile data cannot mint or change a client-owned participation record.");
                if (hasRecord && row[RecordProperty] is not null)
                    ValidateRecord(row[RecordProperty]!.AsObject());
                if (!hasChange) continue;
                if (kind != "guardian" || !AfterlifeEntityProfileState.TryNormalizeEffectRealm(Text(row, "realm"), out var realm) || realm != "chaos_sea")
                    throw new InvalidDataException("Participation changes support only the current Chaos Sea Guardian.");
                var proposal = row[ChangeProperty] as JsonObject ?? throw new InvalidDataException("Participation change must be a closed object.");
                Closed(proposal, "schemaVersion", "operation", "healerId", "woundId", "conscious", "capable", "readableEvidence");
                if (proposal["schemaVersion"]!.GetValue<int>() != 1 || Text(proposal, "operation") is not ("grant" or "revoke") ||
                    Text(proposal, "healerId") != "player_soul" || !ResourceMaterializationContract.IsExactIdentifier(Text(proposal, "woundId")) ||
                    string.IsNullOrWhiteSpace(Text(proposal, "readableEvidence")))
                    throw new InvalidDataException("Invalid Guardian participation proposal.");
                var conscious = proposal["conscious"]!.GetValue<bool>();
                var capable = proposal["capable"]!.GetValue<bool>();
                if (Text(proposal, "operation") == "grant" && (!conscious || !capable))
                    throw new InvalidDataException("A grant requires conscious, capable and affirmative participation.");
                if (changes.Any(c => c.TargetId == id))
                    throw new InvalidDataException("A profile cannot propose participation through multiple response representations.");
                changes.Add(new(id, proposal.DeepClone().AsObject()));
            }
        }
        if (changes.Count > 1) throw new InvalidDataException("This aid slice accepts one current Guardian participation change per turn.");
        return changes;
    }

    internal static JsonObject CreateOutcome(Change change, string locationId, JsonObject binding,
        int cycleOrdinal, string primaryTransitionId)
    {
        var proposal = change.Proposal;
        var record = new JsonObject
        {
            ["schemaVersion"] = 1, ["status"] = Text(proposal, "operation") == "grant" ? "granted" : "revoked",
            ["healerId"] = "player_soul", ["targetKind"] = "guardian", ["targetId"] = change.TargetId,
            ["realm"] = "chaos_sea", ["woundId"] = Text(proposal, "woundId"), ["locationId"] = locationId,
            ["conscious"] = proposal["conscious"]!.GetValue<bool>(), ["capable"] = proposal["capable"]!.GetValue<bool>(),
            ["readableEvidence"] = Text(proposal, "readableEvidence"), ["binding"] = binding.DeepClone(),
            ["acceptedCycleOrdinal"] = cycleOrdinal, ["eligibleCycleOrdinal"] = checked(cycleOrdinal + 1),
            ["primaryTransitionId"] = primaryTransitionId
        };
        ValidateRecord(record);
        return new() { ["record"] = record, ["contentFingerprint"] = Fingerprint(record) };
    }

    internal static void ValidateRecord(JsonObject record)
    {
        Closed(record, "schemaVersion", "status", "healerId", "targetKind", "targetId", "realm", "woundId", "locationId",
            "conscious", "capable", "readableEvidence", "binding", "acceptedCycleOrdinal", "eligibleCycleOrdinal", "primaryTransitionId");
        if (record["schemaVersion"]!.GetValue<int>() != 1 || Text(record, "status") is not ("granted" or "revoked") ||
            Text(record, "healerId") != "player_soul" || Text(record, "targetKind") != "guardian" || Text(record, "realm") != "chaos_sea" ||
            string.IsNullOrWhiteSpace(Text(record, "readableEvidence")))
            throw new InvalidDataException("Invalid canonical Guardian participation record.");
        foreach (var field in new[] { "targetId", "woundId", "locationId", "primaryTransitionId" })
            if (!ResourceMaterializationContract.IsExactIdentifier(Text(record, field)))
                throw new InvalidDataException("Participation identities must be exact identifiers.");
        var conscious = record["conscious"]!.GetValue<bool>(); var capable = record["capable"]!.GetValue<bool>();
        if (Text(record, "status") == "granted" && (!conscious || !capable))
            throw new InvalidDataException("A canonical grant requires conscious and capable participation.");
        var cycle = record["acceptedCycleOrdinal"]!.GetValue<int>();
        if (cycle < 1 || record["eligibleCycleOrdinal"]!.GetValue<int>() != checked(cycle + 1))
            throw new InvalidDataException("Participation authorizes exactly the next accepted safe cycle.");
        var binding = record["binding"]!.AsObject();
        Closed(binding, "sessionId", "requestId", "snapshotToken", "realm", "turn");
        foreach (var field in new[] { "sessionId", "requestId", "snapshotToken" })
            if (!ResourceMaterializationContract.IsExactIdentifier(Text(binding, field)))
                throw new InvalidDataException("Participation lost its actual accepted binding.");
        if (Text(binding, "realm") != "chaos_sea" || binding["turn"]!.GetValue<int>() < 1)
            throw new InvalidDataException("Participation binding realm/turn differs.");
    }

    internal static void ValidateOutcomes(JsonArray outcomes, JsonObject binding, int ordinal,
        IReadOnlyList<SpiritualHealingPublishedStage> stages)
    {
        if (outcomes.Count != 1) throw new InvalidDataException("Participation natural result requires one nonempty bounded outcome.");
        foreach (var item in outcomes)
        {
            var outcome = item!.AsObject(); Closed(outcome, "record", "contentFingerprint");
            var record = outcome["record"]!.AsObject(); ValidateRecord(record);
            if (Text(outcome, "contentFingerprint") != Fingerprint(record) || !JsonNode.DeepEquals(record["binding"], binding) ||
                record["acceptedCycleOrdinal"]!.GetValue<int>() != ordinal ||
                Text(record, "primaryTransitionId") != stages[0].TransitionId || !stages.Any(s =>
                    s.Before.WoundId == Text(record, "woundId") && s.Before.Owner.OwnerKind == "guardian" &&
                    s.Before.Owner.OwnerId == Text(record, "targetId") && s.Before.Owner.Realm == "chaos_sea"))
                throw new InvalidDataException("Participation outcome differs from its complete accepted natural result.");
        }
    }

    internal static void RequirePublishedRecords(JsonArray outcomes, JsonObject? profiles)
    {
        foreach (var outcome in outcomes.OfType<JsonObject>())
        {
            var record = outcome["record"]!.AsObject();
            var current = (profiles?[AfterlifeEntityProfileState.ProfilesProperty] as JsonArray)?.OfType<JsonObject>()
                .Where(p => p["actorType"]?.GetValue<string>() == "guardian" && p["actorId"]?.GetValue<string>() == Text(record, "targetId")).ToArray();
            if (current is not { Length: 1 } || !JsonNode.DeepEquals(current[0][RecordProperty], record) || current[0].ContainsKey(ChangeProperty))
                throw new InvalidDataException("Published participation no longer matches its exact accepted profile content.");
        }
    }

    internal static string Fingerprint(JsonObject record) => WoundAcceptedTurnFingerprintWriter.Compute(new[]
        { "book_of_eternity.spiritual_treatment_participation", "1", WoundAcceptedTurnFingerprintWriter.CanonicalJson(record) });
    private static string Text(JsonObject root, string key) => root[key]?.GetValue<string>() ?? throw new InvalidDataException("Missing participation " + key);
    private static void Closed(JsonObject root, params string[] fields)
    {
        if (root.Count != fields.Length || fields.Any(f => !root.ContainsKey(f)))
            throw new InvalidDataException("Participation object has missing or unknown fields.");
    }
}
