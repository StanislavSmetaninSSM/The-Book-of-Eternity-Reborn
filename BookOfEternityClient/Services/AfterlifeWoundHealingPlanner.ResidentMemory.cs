using System.Globalization;
using System.Text.Json.Nodes;

namespace BookOfEternityClient.Services;

internal static partial class AfterlifeWoundHealingPlanner
{
    internal sealed partial class Continuation
    {
        private JsonArray? _residentThoughtCommands;
        private JsonArray? _residentThoughtBeforeImage;
        private JsonArray? _residentThoughtAfterImage;

        private void AdmitResidentThoughtResponse(JsonObject? response)
        {
            if (response?.ContainsKey(SpiritualTreatmentParticipation.ResidentThoughtResponseProperty) != true) return;
            _residentThoughtCommands = response[SpiritualTreatmentParticipation.ResidentThoughtResponseProperty] is JsonArray rows
                ? rows.DeepClone().AsArray() : throw new InvalidDataException("Protected resident thoughts require an explicit array.");
        }

        private JsonObject? ResidentThoughtRecord => _data?.AidWitness?["record"]?.AsObject() ??
            (_participationOutcomes is { Count: 1 } ? _participationOutcomes[0]?["record"]?.AsObject() : null);

        // An actual service continuation admits this exact quoted resident; a
        // mode flag or an arbitrary resident coordinate alone cannot enable memory.
        private string? ResidentThoughtOwnerId => ResidentThoughtRecord is { } record &&
            (SpiritualTreatmentParticipation.IsResidentParticipation(record) ||
                SpiritualTreatmentParticipation.IsShiningPatientParticipation(record) &&
                SpiritualTreatmentParticipation.RecordOwner(record).OwnerKind == "resident")
                ? SpiritualTreatmentParticipation.RecordOwnerId(record)
                : _data is { ServiceProviderAdmissionFingerprint: not null,
                    ServiceQuote.Provider: { Realm: "shining_abode", OwnerKind: "resident" } provider } ? provider.OwnerId : null;

        private void RequireResidentThoughtFamily()
        {
            if (_residentThoughtCommands is not null && ResidentThoughtOwnerId is null)
                throw new InvalidDataException("Protected resident thoughts require this actual resident participation or faction-service owner.");
        }

        private void BindResidentThoughts(JsonObject? roster)
        {
            if (_residentThoughtCommands is null) return;
            RequireResidentThoughtFamily();
            if (roster is null) throw new InvalidDataException("Resident memory requires its original roster.");
            var commands = SpiritualTreatmentParticipation.ValidateResidentThoughtAppend(_residentThoughtCommands,
                ResidentThoughtOwnerId!, _input.Binding.Turn, roster);
            var expected = roster.DeepClone().AsObject();
            GuardianAbodeResidentState.ApplyThoughtJournalUpdates(expected, commands);
            _residentThoughtBeforeImage = (roster[GuardianAbodeResidentState.ThoughtJournalProperty] as JsonArray)?.DeepClone().AsArray() ?? new JsonArray();
            _residentThoughtAfterImage = expected[GuardianAbodeResidentState.ThoughtJournalProperty]!.DeepClone().AsArray();
            _residentThoughtCommands = commands; // One detached normalized DATA program, not a new permission.
        }

        internal JsonObject ProjectResidentThoughts(JsonObject current)
        {
            if (_residentThoughtCommands is null) return current;
            // Pure composition, like the owned profile projector. The actual
            // writer retains its full fresh opening/closing source scans.
            if (_published) throw new InvalidDataException("A closed resident owner cannot compose memory.");
            RequireResidentThoughtFamily();
            if (_residentThoughtAfterImage is null || current.ContainsKey(GuardianAbodeResidentState.UpdateThoughtJournalProperty))
                throw new InvalidDataException("Protected resident thoughts lack their bound program or compete with raw commands.");
            var journal = current[GuardianAbodeResidentState.ThoughtJournalProperty] ?? new JsonArray();
            if (JsonNode.DeepEquals(journal, _residentThoughtAfterImage)) return current;
            if (!JsonNode.DeepEquals(journal, _residentThoughtBeforeImage))
                throw new InvalidDataException("Resident memory baseline differs from its owned DATA.");
            var projected = current.DeepClone().AsObject();
            projected[GuardianAbodeResidentState.UpdateThoughtJournalProperty] = _residentThoughtCommands.DeepClone();
            return projected;
        }

        private void RequireResidentThoughtAfterImage(JsonObject before, JsonObject after)
        {
            if (_residentThoughtCommands is null) return;
            _ = ProjectResidentThoughts(before);
            if (!JsonNode.DeepEquals(_residentThoughtAfterImage, after[GuardianAbodeResidentState.ThoughtJournalProperty]))
                throw new InvalidDataException("Owned resident normalization lost or replaced its exact admitted memory append.");
        }
    }
}

internal static partial class SpiritualTreatmentParticipation
{
    // The existing journal composer upserts; this narrow admission must prove an
    // append first. No file reads, publication permission or separate cache.
    internal static JsonArray ValidateResidentThoughtAppend(JsonArray commands, string residentId, int turn, JsonObject before)
    {
        if (commands.Count == 0) throw new InvalidDataException("Protected resident memory requires a nonempty append.");
        var mandatory = new[] { "entryId", "residentId", "turn", "timestamp", "title", "summary" };
        var optional = new[] { "eventType", "consequence", "attitude", "intent", "tags" };
        var prior = before[GuardianAbodeResidentState.ThoughtJournalProperty];
        if (prior is not null && prior is not JsonArray)
            throw new InvalidDataException("Resident journal baseline must be an array.");
        var ids = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var old in prior as JsonArray ?? new JsonArray())
            if (old is not JsonObject priorRow || !ResourceMaterializationContract.IsExactIdentifier(Text(priorRow, "entryId")) || !ids.Add(Text(priorRow, "entryId")))
                throw new InvalidDataException("Resident journal baseline has ambiguous entries.");
        var result = new JsonArray();
        foreach (var item in commands)
        {
            if (item is not JsonObject row || mandatory.Any(f => !row.ContainsKey(f)) ||
                row.Any(p => !mandatory.Contains(p.Key, StringComparer.Ordinal) && !optional.Contains(p.Key, StringComparer.Ordinal)) ||
                Text(row, "residentId") != residentId || Scalar<int>(row, "turn") != turn ||
                !ResourceMaterializationContract.IsExactIdentifier(Text(row, "entryId")) || !ids.Add(Text(row, "entryId")) ||
                !DateTimeOffset.TryParse(Text(row, "timestamp"), CultureInfo.InvariantCulture, DateTimeStyles.None, out var timestamp) ||
                timestamp.Offset != TimeSpan.Zero || string.IsNullOrWhiteSpace(Text(row, "title")) || string.IsNullOrWhiteSpace(Text(row, "summary")))
                throw new InvalidDataException("Protected resident memory must append unique exact current-turn entries for its participant.");
            foreach (var field in optional.Where(f => f != "tags" && row.ContainsKey(f))) _ = Text(row, field);
            if (row.ContainsKey("tags") && (row["tags"] is not JsonArray tags || tags.Any(t => t is not JsonValue value || !value.TryGetValue<string>(out _))))
                throw new InvalidDataException("Resident memory tags require strings.");
            var copy = row.DeepClone().AsObject();
            ActorJournalState.NormalizeEntryObject(copy, "residentId");
            result.Add(copy);
        }
        return result;
    }
}
