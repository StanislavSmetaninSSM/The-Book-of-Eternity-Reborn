using System.Text.Json.Nodes;

namespace BookOfEternityClient.Services;

internal static partial class SpiritualTreatmentParticipation
{
    internal const string ShiningPatientMemoryResponseProperty = "spiritualShiningPatientMemory";

    // Detached DATA only. The Continuation supplies the actual retained patient,
    // signed sources and turn; this object grants no publication permission.
    internal static ShiningPatientMemoryProgram PrepareShiningPatientMemory(WoundOwnerCoordinate owner,
        SpiritualAidLocality locality, JsonObject profiles, JsonObject? roster, JsonObject shining, int turn,
        JsonArray? ordinary, JsonArray? protectedRows, JsonObject? radiant)
    {
        _ = RequireShiningPatientAdmission(shining, profiles, roster, owner, locality);
        if (protectedRows is not null && owner.OwnerKind != "resident")
            throw new InvalidDataException("Protected resident thoughts require the exact resident patient.");
        if (radiant is not null && owner.OwnerKind != "radiant_actor")
            throw new InvalidDataException("Shining patient ledger memory requires the exact radiant patient.");
        var combined = new JsonArray();
        var localResidents = new Dictionary<string, string>(StringComparer.Ordinal);
        if (ordinary is not null)
        {
            if (ordinary.Count == 0 || roster is null)
                throw new InvalidDataException("Ordinary patient-turn memory requires a nonempty signed resident append.");
            foreach (var item in ordinary)
            {
                var row = item as JsonObject ?? throw new InvalidDataException("Resident memory requires object rows.");
                var id = Text(row, "residentId");
                if (id == owner.OwnerId)
                    throw new InvalidDataException("The selected patient cannot enter ordinary nonpatient memory.");
                if (!localResidents.ContainsKey(id))
                    localResidents.Add(id, RequireShiningPatientAdmission(shining, profiles, roster,
                        new("shining_abode", "resident", id, WoundCarrierCatalog.AfterlifeProfilesPath), locality).Fingerprint);
                combined.Add(row.DeepClone());
            }
        }
        if (protectedRows is not null)
        {
            if (protectedRows.Count == 0) throw new InvalidDataException("Protected resident memory requires a nonempty append.");
            foreach (var item in protectedRows)
            {
                if (item is not JsonObject row || Text(row, "residentId") != owner.OwnerId)
                    throw new InvalidDataException("Protected resident thoughts may name only the retained patient.");
                combined.Add(row.DeepClone());
            }
        }
        var beforeJournal = (roster?[GuardianAbodeResidentState.ThoughtJournalProperty] as JsonArray)?.DeepClone().AsArray() ?? new JsonArray();
        if (combined.Count > 0)
        {
            if (roster is null) throw new InvalidDataException("Resident memory lost its signed roster.");
            combined = ValidateResidentThoughtAppends(combined,
                id => localResidents.ContainsKey(id) || owner.OwnerKind == "resident" && id == owner.OwnerId, turn, roster);
        }
        var journalRoot = new JsonObject { [GuardianAbodeResidentState.ThoughtJournalProperty] = beforeJournal.DeepClone() };
        GuardianAbodeResidentState.ApplyThoughtJournalUpdates(journalRoot, combined);
        JsonArray? beforeLedger = null; JsonArray? afterLedger = null; JsonNode? beforeSummary = null; JsonNode? afterSummary = null;
        if (radiant is not null)
        {
            Closed(radiant, "entryId", "summary", "turnNumber", "gmThoughtsSummary");
            var id = Text(radiant, "entryId");
            if (!ResourceMaterializationContract.IsExactIdentifier(id) || Scalar<int>(radiant, "turnNumber") != turn ||
                string.IsNullOrWhiteSpace(Text(radiant, "summary")) || string.IsNullOrWhiteSpace(Text(radiant, "gmThoughtsSummary")))
                throw new InvalidDataException("Radiant memory requires exact current-turn text and a fresh ledger ID.");
            var profile = ExactMemoryProfile(profiles, owner);
            beforeLedger = profile["ledger"] is JsonArray ledger ? ledger.DeepClone().AsArray()
                : throw new InvalidDataException("Radiant memory requires its original ledger array.");
            var ids = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var old in beforeLedger)
                if (old is JsonObject row && row["entryId"] is JsonValue value && value.TryGetValue<string>(out var oldId) && !ids.Add(oldId))
                    throw new InvalidDataException("The original radiant ledger has ambiguous IDs.");
            if (!ids.Add(id)) throw new InvalidDataException("Radiant memory must append a fresh ledger ID.");
            afterLedger = beforeLedger.DeepClone().AsArray();
            afterLedger.Add(new JsonObject { ["entryId"] = id, ["summary"] = Text(radiant, "summary"), ["turnNumber"] = turn });
            beforeSummary = profile["gmThoughtsSummary"]?.DeepClone(); afterSummary = radiant["gmThoughtsSummary"]!.DeepClone();
        }
        return new(owner, locality, profiles, shining, localResidents, combined, beforeJournal,
            journalRoot[GuardianAbodeResidentState.ThoughtJournalProperty]!.AsArray(), beforeLedger, afterLedger, beforeSummary, afterSummary);
    }

    private static JsonObject ExactMemoryProfile(JsonObject profiles, WoundOwnerCoordinate owner)
    {
        var rows = (profiles["profiles"] as JsonArray)?.OfType<JsonObject>().Where(p =>
            WoundCarrierCollectionAuthority.AfterlifeProfileMatches(p, owner) && ProfileId(p) == owner.OwnerId).ToArray();
        return rows is { Length: 1 } ? rows[0] : throw new InvalidDataException("Patient memory lost its exact profile.");
    }

    internal sealed class ShiningPatientMemoryProgram
    {
        private readonly WoundOwnerCoordinate _owner;
        private readonly SpiritualAidLocality _locality;
        private readonly JsonObject _profiles;
        private readonly JsonObject _shining;
        private readonly IReadOnlyDictionary<string, string> _residents;
        private readonly JsonArray _commands, _beforeJournal, _afterJournal;
        private readonly JsonArray? _beforeLedger, _afterLedger;
        private readonly JsonNode? _beforeSummary, _afterSummary;

        internal ShiningPatientMemoryProgram(WoundOwnerCoordinate owner, SpiritualAidLocality locality, JsonObject profiles,
            JsonObject shining, IReadOnlyDictionary<string, string> residents, JsonArray commands, JsonArray beforeJournal,
            JsonArray afterJournal, JsonArray? beforeLedger, JsonArray? afterLedger, JsonNode? beforeSummary, JsonNode? afterSummary)
        {
            _owner = owner; _locality = locality; _profiles = profiles.DeepClone().AsObject(); _shining = shining.DeepClone().AsObject();
            _residents = residents.ToDictionary(p => p.Key, p => p.Value, StringComparer.Ordinal);
            _commands = commands.DeepClone().AsArray(); _beforeJournal = beforeJournal.DeepClone().AsArray(); _afterJournal = afterJournal.DeepClone().AsArray();
            _beforeLedger = beforeLedger?.DeepClone().AsArray(); _afterLedger = afterLedger?.DeepClone().AsArray();
            _beforeSummary = beforeSummary?.DeepClone(); _afterSummary = afterSummary?.DeepClone();
        }

        internal JsonObject ProjectResidents(JsonObject current)
        {
            if (_commands.Count == 0) return current;
            if (current.ContainsKey(GuardianAbodeResidentState.UpdateThoughtJournalProperty))
                throw new InvalidDataException("Patient memory cannot compete with raw resident commands.");
            var journal = current[GuardianAbodeResidentState.ThoughtJournalProperty] ?? new JsonArray();
            if (JsonNode.DeepEquals(journal, _afterJournal)) return current;
            if (!JsonNode.DeepEquals(journal, _beforeJournal))
                throw new InvalidDataException("Patient resident memory differs from its complete before/after image.");
            var projected = current.DeepClone().AsObject();
            projected[GuardianAbodeResidentState.UpdateThoughtJournalProperty] = _commands.DeepClone();
            return projected;
        }

        internal void RequireResidentAfterImage(JsonObject before, JsonObject after)
        {
            _ = ProjectResidents(before);
            if (!JsonNode.DeepEquals(after[GuardianAbodeResidentState.ThoughtJournalProperty], _afterJournal))
                throw new InvalidDataException("Patient resident publication lost its exact combined append.");
            foreach (var resident in _residents)
                if (RequireShiningPatientAdmission(_shining, _profiles, after,
                    new("shining_abode", "resident", resident.Key, WoundCarrierCatalog.AfterlifeProfilesPath), _locality).Fingerprint != resident.Value)
                    throw new InvalidDataException("Ordinary resident memory lost its exact original local actor.");
        }

        internal void ApplyProfiles(JsonObject profiles)
        {
            if (_afterLedger is null) return;
            var profile = ExactMemoryProfile(profiles, _owner);
            if (JsonNode.DeepEquals(profile["ledger"], _afterLedger) && JsonNode.DeepEquals(profile["gmThoughtsSummary"], _afterSummary)) return;
            if (!JsonNode.DeepEquals(profile["ledger"], _beforeLedger) || !JsonNode.DeepEquals(profile["gmThoughtsSummary"], _beforeSummary))
                throw new InvalidDataException("Radiant memory differs from its complete before/after image.");
            profile["ledger"] = _afterLedger.DeepClone(); profile["gmThoughtsSummary"] = _afterSummary!.DeepClone();
        }
    }
}
