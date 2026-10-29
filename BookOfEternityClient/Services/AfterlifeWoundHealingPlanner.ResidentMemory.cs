using System.Globalization;
using System.Text.Json.Nodes;

namespace BookOfEternityClient.Services;

internal static partial class AfterlifeWoundHealingPlanner
{
    internal sealed partial class Continuation
    {
        private JsonArray? _residentThoughtCommands;
        private JsonArray? _residentThoughtAfterImage;

        private void AdmitResidentThoughtResponse(JsonObject? response)
        {
            if (response?.ContainsKey(SpiritualTreatmentParticipation.ResidentThoughtResponseProperty) != true) return;
            _residentThoughtCommands = response[SpiritualTreatmentParticipation.ResidentThoughtResponseProperty] is JsonArray rows
                ? rows.DeepClone().AsArray() : throw new InvalidDataException("Protected resident thoughts require an explicit array.");
        }

        private JsonObject? ResidentThoughtRecord => _data?.AidWitness?["record"]?.AsObject() ??
            (_participationOutcomes is { Count: 1 } ? _participationOutcomes[0]?["record"]?.AsObject() : null);

        private void RequireResidentThoughtFamily()
        {
            if (_residentThoughtCommands is not null && (ResidentThoughtRecord is not { } record ||
                !SpiritualTreatmentParticipation.IsResidentConsent(record)))
                throw new InvalidDataException("Protected resident thoughts require this actual resident grant or aid owner.");
        }

        internal JsonObject ProjectResidentThoughts(JsonObject current)
        {
            if (_residentThoughtCommands is null) return current;
            RequireExecutionCurrent();
            RequireResidentThoughtFamily();
            if (current.ContainsKey(GuardianAbodeResidentState.UpdateThoughtJournalProperty))
                throw new InvalidDataException("Protected resident thoughts cannot compete with raw roster journal commands.");
            if (_residentThoughtAfterImage is not null && JsonNode.DeepEquals(
                    current[GuardianAbodeResidentState.ThoughtJournalProperty], _residentThoughtAfterImage)) return current;
            var commands = SpiritualTreatmentParticipation.ValidateResidentThoughtAppend(_residentThoughtCommands,
                SpiritualTreatmentParticipation.RecordOwnerId(ResidentThoughtRecord!), _input.Binding.Turn, current);
            var expected = current.DeepClone().AsObject();
            GuardianAbodeResidentState.ApplyThoughtJournalUpdates(expected, commands);
            var afterImage = expected[GuardianAbodeResidentState.ThoughtJournalProperty]!.AsArray();
            if (_residentThoughtAfterImage is not null && !JsonNode.DeepEquals(_residentThoughtAfterImage, afterImage))
                throw new InvalidDataException("Resident memory projection changed within its actual owner.");
            _residentThoughtAfterImage = afterImage.DeepClone().AsArray();
            var projected = current.DeepClone().AsObject();
            projected[GuardianAbodeResidentState.UpdateThoughtJournalProperty] = commands;
            return projected;
        }

        private void RequireResidentThoughtAfterImage(JsonObject before, JsonObject after)
        {
            if (_residentThoughtCommands is null) return;
            RequireResidentThoughtFamily();
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
            if (old is not JsonObject row || !ResourceMaterializationContract.IsExactIdentifier(Text(row, "entryId")) || !ids.Add(Text(row, "entryId")))
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
