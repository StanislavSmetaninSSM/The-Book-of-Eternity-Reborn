using System.Text.Json;
using System.Text.Json.Nodes;

namespace BookOfEternityClient.Services;

// One closed domain record on the existing persistent profile. Serialized DATA
// alone is never admission: staging must prove its actual accepted result.
internal static partial class SpiritualTreatmentParticipation
{
    internal const string ChangeProperty = "spiritualTreatmentParticipationChange";
    internal const string RecordProperty = "spiritualTreatmentParticipation";
    internal sealed record Change(WoundOwnerCoordinate Owner, JsonObject Proposal)
    {
        internal Change(string targetId, JsonObject proposal) : this(
            new WoundOwnerCoordinate("chaos_sea", "guardian", targetId, WoundCarrierCatalog.AfterlifeProfilesPath), proposal) { }
        internal string TargetId => Owner.OwnerId;
    }

    internal static bool HasUnsettledChange(JsonObject? root) => root is not null &&
        new[] { AfterlifeEntityProfileState.ProfilesProperty, AfterlifeEntityProfileState.ResponseProfilesProperty,
            AfterlifeEntityProfileState.UpdateProperty }.Any(property => root[property] is JsonArray rows &&
                rows.OfType<JsonObject>().Any(row => row.ContainsKey(ChangeProperty)));

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
                var id = ProfileId(row);
                var kind = Text(row, "actorType");
                var original = (previous[AfterlifeEntityProfileState.ProfilesProperty] as JsonArray)?.OfType<JsonObject>()
                    .Where(p => ProfileId(p) == id && p["actorType"]?.GetValue<string>() == kind).ToArray();
                if (original is not { Length: 1 }) throw new InvalidDataException("Participation requires an exact preexisting persistent profile.");
                if (hasRecord && !JsonNode.DeepEquals(row[RecordProperty], original[0][RecordProperty]))
                    throw new InvalidDataException("Raw profile data cannot mint or change a client-owned participation record.");
                if (hasRecord && row[RecordProperty] is not null)
                    ValidateRecord(row[RecordProperty]!.AsObject());
                if (!hasChange) continue;
                if (original[0].ContainsKey(ChangeProperty))
                    throw new InvalidDataException("Participation proposal must be a new response, not retained original input.");
                var proposal = row[ChangeProperty] as JsonObject ?? throw new InvalidDataException("Participation change must be a closed object.");
                var shiningPatient = IsShiningPatientParticipation(proposal);
                if ((shiningPatient ? kind is not ("resident" or "radiant_actor") : kind is not ("guardian" or "resident")) || !AfterlifeEntityProfileState.TryNormalizeEffectRealm(
                        row["realm"]?.GetValue<string>() ?? Text(original[0], "realm"), out var realm) || realm is not ("chaos_sea" or "shining_abode"))
                    throw new InvalidDataException("Participation changes require an exact current afterlife Guardian or resident.");
                ValidateProposal(proposal);
                if (!shiningPatient && (Scalar<int>(proposal, "schemaVersion") is 3 or 4) != (kind == "resident"))
                    throw new InvalidDataException("Participation proposal family differs from its enclosing typed owner.");
                var owner = new WoundOwnerCoordinate(realm, kind, id, WoundCarrierCatalog.AfterlifeProfilesPath);
                if (shiningPatient)
                {
                    if (!AfterlifeEntityProfileState.TryNormalizeEffectRealm(Text(original[0], "realm"), out var originalRealm) ||
                        originalRealm != "shining_abode")
                        throw new InvalidDataException("A Shining patient proposal cannot relocate its original owner between realms.");
                    RequireShiningPatientChangeOwner(new(owner, proposal), ReadLocality(proposal["locality"]));
                }
                else if (realm == "shining_abode")
                {
                    if (!IsProviderOffer(proposal))
                        throw new InvalidDataException("Target consent remains limited to the exact Chaos participant family.");
                    RequireProviderLocality(owner, ReadLocality(proposal["locality"]));
                }
                else if (proposal.ContainsKey("locality"))
                    throw new InvalidDataException("A Chaos proposal cannot carry a Shining locality or an owner alias.");
                var mirrored = changes.SingleOrDefault(c => c.Owner == owner);
                if (mirrored is not null)
                {
                    if (!JsonNode.DeepEquals(mirrored.Proposal, proposal))
                        throw new InvalidDataException("Mirrored profile representations contain conflicting participation changes.");
                    continue; // Same exact intent, not a second grant or a new capability.
                }
                changes.Add(new(owner, proposal.DeepClone().AsObject()));
            }
        }
        if (changes.Count > 1) throw new InvalidDataException("This aid slice accepts one distinct typed participation change per turn.");
        return changes;
    }

    private static void ValidateProposal(JsonObject proposal)
    {
        var version = Scalar<int>(proposal, "schemaVersion");
        if (version is 1 or 3)
        {
            var fields = new[] { "schemaVersion", "operation", "healerId", "woundId", "conscious", "capable", "readableEvidence" };
            Closed(proposal, version == 3 ? fields.Append("kind").ToArray() : fields);
            if (version == 3 && Text(proposal, "kind") != "resident_target_consent")
                throw new InvalidDataException("Invalid resident target-consent proposal.");
            if (Text(proposal, "operation") is not ("grant" or "revoke") || Text(proposal, "healerId") != "player_soul")
                throw new InvalidDataException("Invalid Guardian target-consent proposal.");
            var conscious = Scalar<bool>(proposal, "conscious");
            var capable = Scalar<bool>(proposal, "capable");
            if (Text(proposal, "operation") == "grant" && (!conscious || !capable))
                throw new InvalidDataException("A grant requires conscious, capable and affirmative participation.");
        }
        else if (version is 2 or 4)
        {
            Closed(proposal, ProviderFields(proposal, new[] { "schemaVersion", "kind", "operation", "targetId", "woundId", "providerConscious",
                "providerCapable", "targetConscious", "targetCapable", "readableEvidence" }));
            _ = ReadCompensation(proposal);
            if (proposal.ContainsKey("locality") && ReadLocality(proposal["locality"]).Realm != "shining_abode")
                throw new InvalidDataException("An explicit provider proposal locality must be a closed Shining variant.");
            if (Text(proposal, "kind") != (version == 4 ? "resident_provider_offer" : "provider_offer") || Text(proposal, "operation") is not ("offer" or "revoke") ||
                Text(proposal, "targetId") != "player_soul")
                throw new InvalidDataException("Invalid private Guardian provider-offer proposal.");
            foreach (var field in OfferConditionFields)
                if (!Scalar<bool>(proposal, field) && Text(proposal, "operation") == "offer")
                    throw new InvalidDataException("A provider offer requires a conscious and capable healer and target.");
        }
        else if (version == 5) ValidateShiningPatientProposal(proposal);
        else throw new InvalidDataException("Unknown participation proposal schema.");
        if (!ResourceMaterializationContract.IsExactIdentifier(Text(proposal, "woundId")) ||
            string.IsNullOrWhiteSpace(Text(proposal, "readableEvidence")))
            throw new InvalidDataException("Participation proposal requires exact wound and readable evidence.");
    }

    private static readonly string[] OfferConditionFields =
        ["providerConscious", "providerCapable", "targetConscious", "targetCapable"];
    internal static bool IsProviderOffer(JsonObject record) => Scalar<int>(record, "schemaVersion") is 2 or 4;
    internal static string RecordOwnerId(JsonObject record) => RecordOwner(record).OwnerId;
    internal static string TargetKind(JsonObject record) => RecordTarget(record).OwnerKind;
    internal static int ResultVersion(JsonObject record) => IsShiningPatientParticipation(record) ? 8 : IsResidentProviderOffer(record) ? 6 : IsResidentConsent(record) ? 5 : IsProviderOffer(record) ? 4 : 3;
    internal static bool IsAvailable(JsonObject record) => Text(record, "status") == (IsProviderOffer(record) ? "offered" : "granted");

    internal static JsonObject CreateOutcome(Change change, string locationId, JsonObject binding,
        int cycleOrdinal, string primaryTransitionId) => CreateGuardianOutcome(change,
            new SpiritualAidLocality(change.Owner.OwnerId, locationId), binding, cycleOrdinal, primaryTransitionId);

    private static JsonObject CreateGuardianOutcome(Change change, SpiritualAidLocality locality, JsonObject binding,
        int cycleOrdinal, string primaryTransitionId)
    {
        if (change.Owner.OwnerKind != "guardian")
            throw new InvalidDataException("Resident outcomes require their exact typed current locality.");
        var proposal = change.Proposal;
        ValidateProposal(proposal);
        if (Scalar<int>(proposal, "schemaVersion") is 3 or 4)
            throw new InvalidDataException("Guardian outcomes cannot contain resident proposals.");
        var offer = Scalar<int>(proposal, "schemaVersion") == 2;
        if (offer) RequireProposedProviderLocality(change, locality);
        else if (change.Owner.Realm != "chaos_sea" || locality.Value is not SpiritualAidLocality.ChaosAbode)
            throw new InvalidDataException("Target consent remains limited to its exact Chaos Abode.");
        var record = new JsonObject
        {
            ["schemaVersion"] = offer ? 2 : 1,
            ["status"] = Text(proposal, "operation") == "revoke" ? "revoked" : offer ? "offered" : "granted",
            ["healerId"] = offer ? change.TargetId : "player_soul",
            ["targetKind"] = offer ? "player_soul" : "guardian", ["targetId"] = offer ? "player_soul" : change.TargetId,
            ["realm"] = change.Owner.Realm, ["woundId"] = Text(proposal, "woundId"), ["locationId"] = locality.LocationId,
            ["readableEvidence"] = Text(proposal, "readableEvidence"), ["binding"] = binding.DeepClone(),
            ["acceptedCycleOrdinal"] = cycleOrdinal, ["eligibleCycleOrdinal"] = checked(cycleOrdinal + 1),
            ["primaryTransitionId"] = primaryTransitionId
        };
        if (offer)
        {
            record["kind"] = "provider_offer"; record["healerKind"] = "guardian";
            foreach (var field in OfferConditionFields) record[field] = proposal[field]!.DeepClone();
            if (locality.Realm == "shining_abode") record["locality"] = LocalityJson(locality);
            CopyCompensation(proposal, record);
        }
        else
        {
            record["conscious"] = proposal["conscious"]!.DeepClone();
            record["capable"] = proposal["capable"]!.DeepClone();
        }
        ValidateRecord(record);
        return new() { ["record"] = record, ["contentFingerprint"] = Fingerprint(record) };
    }

    internal static void ValidateRecord(JsonObject record)
    {
        var version = Scalar<int>(record, "schemaVersion");
        var common = new[] { "schemaVersion", "status", "healerId", "targetKind", "targetId", "realm", "woundId", "locationId",
            "readableEvidence", "binding", "acceptedCycleOrdinal", "eligibleCycleOrdinal", "primaryTransitionId" };
        if (version == 1)
        {
            Closed(record, common.Concat(new[] { "conscious", "capable" }).ToArray());
            if (Text(record, "status") is not ("granted" or "revoked") || Text(record, "healerId") != "player_soul" ||
                Text(record, "targetKind") != "guardian")
                throw new InvalidDataException("Invalid canonical Guardian target-consent record.");
            var conscious = Scalar<bool>(record, "conscious"); var capable = Scalar<bool>(record, "capable");
            if (Text(record, "status") == "granted" && (!conscious || !capable))
                throw new InvalidDataException("A canonical grant requires conscious and capable participation.");
        }
        else if (version == 2)
        {
            Closed(record, ProviderFields(record, common.Concat(new[] { "kind", "healerKind" }).Concat(OfferConditionFields)));
            _ = ReadCompensation(record);
            var locality = RecordLocality(record);
            RequireProviderLocality(RecordHealer(record), locality);
            if (Text(record, "realm") == "chaos_sea" && record.ContainsKey("locality") || locality.LocationId != Text(record, "locationId"))
                throw new InvalidDataException("Guardian provider locality differs from its exact record coordinate.");
            if (Text(record, "kind") != "provider_offer" || Text(record, "status") is not ("offered" or "revoked") ||
                Text(record, "healerKind") != "guardian" || Text(record, "healerId") == "player_soul" ||
                Text(record, "targetKind") != "player_soul" || Text(record, "targetId") != "player_soul")
                throw new InvalidDataException("Invalid canonical private Guardian offer record.");
            foreach (var field in OfferConditionFields)
                if (!Scalar<bool>(record, field) && Text(record, "status") == "offered")
                    throw new InvalidDataException("A canonical offer requires conscious and capable healer and target.");
        }
        else if (version == 3) ValidateResidentRecord(record);
        else if (version == 4) ValidateResidentProviderRecord(record);
        else if (version == 5) ValidateShiningPatientRecord(record);
        else throw new InvalidDataException("Unknown participation record schema.");
        var recordRealm = RecordRealm(record);
        if (recordRealm is not ("chaos_sea" or "shining_abode") ||
            !IsProviderOffer(record) && !IsShiningPatientParticipation(record) && recordRealm != "chaos_sea" || string.IsNullOrWhiteSpace(Text(record, "readableEvidence")))
            throw new InvalidDataException("Participation requires the exact supported realm and readable evidence.");
        foreach (var field in version is 3 or 4 or 5 ? new[] { "woundId", "primaryTransitionId" } : new[] { "healerId", "targetId", "woundId", "locationId", "primaryTransitionId" })
            if (!ResourceMaterializationContract.IsExactIdentifier(Text(record, field)))
                throw new InvalidDataException("Participation identities must be exact identifiers.");
        var cycle = Scalar<int>(record, "acceptedCycleOrdinal");
        if (cycle < 1 || Scalar<int>(record, "eligibleCycleOrdinal") != checked(cycle + 1))
            throw new InvalidDataException("Participation authorizes exactly the next accepted safe cycle.");
        var binding = record["binding"] as JsonObject ?? throw new InvalidDataException("Participation requires its actual accepted binding.");
        Closed(binding, "sessionId", "requestId", "snapshotToken", "realm", "turn");
        foreach (var field in new[] { "sessionId", "requestId", "snapshotToken" })
            if (!ResourceMaterializationContract.IsExactIdentifier(Text(binding, field)))
                throw new InvalidDataException("Participation lost its actual accepted binding.");
        if (Text(binding, "realm") != recordRealm || Scalar<int>(binding, "turn") < 1)
            throw new InvalidDataException("Participation binding realm/turn differs.");
    }

    internal static void ValidateOutcomes(JsonArray outcomes, JsonObject binding, int ordinal,
        IReadOnlyList<SpiritualHealingPublishedStage> stages, int resultVersion)
    {
        if (outcomes.Count != 1) throw new InvalidDataException("Participation natural result requires one nonempty bounded outcome.");
        foreach (var item in outcomes)
        {
            var outcome = item!.AsObject(); Closed(outcome, "record", "contentFingerprint");
            var record = outcome["record"]!.AsObject(); ValidateRecord(record);
            if (ResultVersion(record) != resultVersion || Text(outcome, "contentFingerprint") != Fingerprint(record) || !JsonNode.DeepEquals(record["binding"], binding) ||
                record["acceptedCycleOrdinal"]!.GetValue<int>() != ordinal ||
                Text(record, "primaryTransitionId") != stages[0].TransitionId || !stages.Any(s =>
                    s.Before.WoundId == Text(record, "woundId") && s.Before.Owner == RecordTarget(record)))
                throw new InvalidDataException("Participation outcome differs from its complete accepted natural result.");
        }
    }

    internal static void RequirePublishedRecords(JsonArray outcomes, JsonObject? profiles)
    {
        foreach (var outcome in outcomes.OfType<JsonObject>())
        {
            var record = outcome["record"]!.AsObject();
            var current = (profiles?[AfterlifeEntityProfileState.ProfilesProperty] as JsonArray)?.OfType<JsonObject>()
                .Where(p => p["actorType"]?.GetValue<string>() == RecordOwner(record).OwnerKind && ProfileId(p) == RecordOwnerId(record) &&
                    AfterlifeEntityProfileState.TryNormalizeEffectRealm(p["realm"]?.GetValue<string>(), out var realm) && realm == RecordOwner(record).Realm).ToArray();
            if (current is not { Length: 1 } || !JsonNode.DeepEquals(current[0][RecordProperty], record) || current[0].ContainsKey(ChangeProperty))
                throw new InvalidDataException("Published participation no longer matches its exact accepted profile content.");
        }
    }

    internal static string Fingerprint(JsonObject record) => WoundAcceptedTurnFingerprintWriter.Compute(new[]
        { "book_of_eternity.spiritual_treatment_participation", Scalar<int>(record, "schemaVersion").ToString(System.Globalization.CultureInfo.InvariantCulture), WoundAcceptedTurnFingerprintWriter.CanonicalJson(record) });
    internal static LocalInteractionScope RequireCurrentGuardianScope(JsonObject guardians, JsonObject? residents, string targetId)
    {
        var scope = LocalInteractionScopeService.ResolveChaosSeaScope(guardians, residents,
            Array.Empty<LocalInteractionAuthoritySnapshot>());
        var exact = (guardians["guardians"] as JsonArray)?.OfType<JsonObject>().Where(g =>
            string.Equals(g["guardianId"]?.GetValue<string>(), targetId, StringComparison.OrdinalIgnoreCase)).ToArray();
        var activeAbode = guardians["activeGuardian"]?["abode"]?["abodeId"]?.GetValue<string>();
        if (!scope.IsResolved || scope.CurrentGuardianId != targetId || exact is not { Length: 1 } ||
            exact[0]["guardianId"]?.GetValue<string>() != targetId || guardians["activeGuardian"]?["guardianId"]?.GetValue<string>() != targetId ||
            guardians["chaosSeaNavigation"]?["currentGuardianId"]?.GetValue<string>() != targetId ||
            guardians["chaosSeaNavigation"]?["currentAbodeId"]?.GetValue<string>() != scope.LocationId ||
            exact[0]["abode"]?["abodeId"]?.GetValue<string>() != scope.LocationId || activeAbode is not null && activeAbode != scope.LocationId)
            throw new InvalidDataException("Participation cannot use an alias, ambiguous Guardian or foreign Abode.");
        return scope;
    }
    internal static string ProfileId(JsonObject profile)
    {
        var id = profile["actorId"]?.GetValue<string>(); var reference = profile["actorRef"]?.GetValue<string>();
        if (id is not null && reference is not null && id != reference)
            throw new InvalidDataException("Participation profile has conflicting exact actor identity fields.");
        var result = id ?? reference;
        return result is not null && ResourceMaterializationContract.IsExactIdentifier(result)
            ? result : throw new InvalidDataException("Participation profile requires an exact actor identity.");
    }
    private static string Text(JsonObject root, string key) => root[key]?.GetValue<string>() ?? throw new InvalidDataException("Missing participation " + key);
    private static T Scalar<T>(JsonObject root, string key) => root[key] is JsonValue value && value.TryGetValue<T>(out var scalar)
        ? scalar : throw new InvalidDataException("Participation requires a typed nonnull " + key);
    private static void Closed(JsonObject root, params string[] fields)
    {
        if (root.Count != fields.Length || fields.Any(f => !root.ContainsKey(f)))
            throw new InvalidDataException("Participation object has missing or unknown fields.");
    }
}
