using System.Text.Json.Nodes;

namespace BookOfEternityClient.Services;

internal sealed record SpiritualAidLocality(string GuardianId, string AbodeId);
internal sealed record SpiritualResidentAidParticipant(
    WoundOwnerCoordinate Healer, WoundOwnerCoordinate Target, SpiritualAidLocality Locality);

internal static partial class SpiritualTreatmentParticipation
{
    internal const string ResidentThoughtResponseProperty = "spiritualResidentThoughtJournalUpdates";
    internal static bool IsResidentConsent(JsonObject record) => Scalar<int>(record, "schemaVersion") == 3;
    internal static bool IsResidentProviderOffer(JsonObject record) => Scalar<int>(record, "schemaVersion") == 4;
    internal static bool IsResidentParticipation(JsonObject record) => Scalar<int>(record, "schemaVersion") is 3 or 4;
    internal static WoundOwnerCoordinate RecordHealer(JsonObject record) => IsResidentParticipation(record)
        ? ReadOwner(record["healer"])
        : new("chaos_sea", IsProviderOffer(record) ? "guardian" : "player_soul", Text(record, "healerId"), WoundCarrierCatalog.AfterlifeProfilesPath);
    internal static WoundOwnerCoordinate RecordTarget(JsonObject record) => IsResidentParticipation(record)
        ? ReadOwner(record["target"])
        : new("chaos_sea", IsProviderOffer(record) ? "player_soul" : "guardian", Text(record, "targetId"), WoundCarrierCatalog.AfterlifeProfilesPath);
    internal static WoundOwnerCoordinate RecordOwner(JsonObject record) => IsProviderOffer(record) ? RecordHealer(record) : RecordTarget(record);
    internal static SpiritualAidLocality RecordLocality(JsonObject record) => IsResidentParticipation(record)
        ? ReadLocality(record["locality"]) : new(RecordOwnerId(record), Text(record, "locationId"));

    internal static WoundOwnerCoordinate ReadOwner(JsonNode? value)
    {
        var root = value as JsonObject ?? throw new InvalidDataException("Participation requires a typed owner coordinate.");
        Closed(root, "realm", "ownerKind", "ownerId", "carrierPath");
        var owner = new WoundOwnerCoordinate(Text(root, "realm"), Text(root, "ownerKind"), Text(root, "ownerId"), Text(root, "carrierPath"));
        if (owner.Realm != "chaos_sea" || owner.OwnerKind is not ("player_soul" or "guardian" or "resident") ||
            !ResourceMaterializationContract.IsExactIdentifier(owner.OwnerId) || owner.CarrierPath != WoundCarrierCatalog.AfterlifeProfilesPath)
            throw new InvalidDataException("Participation owner coordinate is outside its exact Chaos profile family.");
        return owner;
    }

    internal static SpiritualAidLocality ReadLocality(JsonNode? value)
    {
        var root = value as JsonObject ?? throw new InvalidDataException("Participation requires exact locality.");
        Closed(root, "guardianId", "abodeId");
        var locality = new SpiritualAidLocality(Text(root, "guardianId"), Text(root, "abodeId"));
        if (!ResourceMaterializationContract.IsExactIdentifier(locality.GuardianId) ||
            !ResourceMaterializationContract.IsExactIdentifier(locality.AbodeId))
            throw new InvalidDataException("Participation locality requires exact Guardian and Abode identities.");
        return locality;
    }

    internal static JsonObject LocalityJson(SpiritualAidLocality locality) => new()
    { ["guardianId"] = locality.GuardianId, ["abodeId"] = locality.AbodeId };

    internal static SpiritualAidSelection Selection(JsonObject record)
    {
        var healer = RecordHealer(record); var target = RecordTarget(record);
        return new(healer.OwnerId, target.OwnerId, Fingerprint(record))
        {
            ReceiveAid = IsProviderOffer(record),
            Resident = IsResidentParticipation(record) ? new(healer, target, RecordLocality(record)) : null
        };
    }

    internal static bool MatchesSelection(JsonObject record, SpiritualAidSelection aid) =>
        ResultVersion(record) == aid.FamilyVersion && RecordHealer(record) == aid.Healer("chaos_sea") &&
        RecordTarget(record) == aid.Target("chaos_sea") &&
        (aid.Resident is null || RecordLocality(record) == aid.Resident.Locality);

    private static void ValidateResidentRecord(JsonObject record)
    {
        Closed(record, "schemaVersion", "kind", "status", "healer", "target", "woundId", "locality", "conscious", "capable",
            "readableEvidence", "binding", "acceptedCycleOrdinal", "eligibleCycleOrdinal", "primaryTransitionId");
        var healer = RecordHealer(record); var target = RecordTarget(record);
        _ = RecordLocality(record);
        if (Text(record, "kind") != "resident_target_consent" || Text(record, "status") is not ("granted" or "revoked") ||
            healer != new WoundOwnerCoordinate("chaos_sea", "player_soul", "player_soul", WoundCarrierCatalog.AfterlifeProfilesPath) ||
            target.OwnerKind != "resident")
            throw new InvalidDataException("Invalid canonical resident target-consent record.");
        var conscious = Scalar<bool>(record, "conscious"); var capable = Scalar<bool>(record, "capable");
        if (Text(record, "status") == "granted" && (!conscious || !capable))
            throw new InvalidDataException("A canonical resident grant requires conscious and capable participation.");
    }

    private static void ValidateResidentProviderRecord(JsonObject record)
    {
        Closed(record, new[] { "schemaVersion", "kind", "status", "healer", "target", "woundId", "locality",
            "readableEvidence", "binding", "acceptedCycleOrdinal", "eligibleCycleOrdinal", "primaryTransitionId" }
            .Concat(OfferConditionFields).ToArray());
        var healer = RecordHealer(record); var target = RecordTarget(record);
        _ = RecordLocality(record);
        if (Text(record, "kind") != "resident_provider_offer" || Text(record, "status") is not ("offered" or "revoked") ||
            healer.OwnerKind != "resident" || target != new WoundOwnerCoordinate("chaos_sea", "player_soul", "player_soul", WoundCarrierCatalog.AfterlifeProfilesPath))
            throw new InvalidDataException("Invalid canonical resident provider-offer record.");
        foreach (var field in OfferConditionFields)
            if (!Scalar<bool>(record, field) && Text(record, "status") == "offered")
                throw new InvalidDataException("A canonical resident offer requires conscious and capable healer and target.");
    }

    internal static JsonObject CreateOutcome(Change change, SpiritualAidLocality locality, JsonObject binding,
        int cycleOrdinal, string primaryTransitionId)
    {
        if (change.Owner.OwnerKind != "resident")
            return CreateOutcome(change, locality.AbodeId, binding, cycleOrdinal, primaryTransitionId);
        ValidateProposal(change.Proposal);
        if (IsResidentProviderOffer(change.Proposal))
            return CreateResidentProviderOutcome(change, locality, binding, cycleOrdinal, primaryTransitionId);
        if (Scalar<int>(change.Proposal, "schemaVersion") != 3 || change.Owner.Realm != "chaos_sea" ||
            change.Owner.CarrierPath != WoundCarrierCatalog.AfterlifeProfilesPath)
            throw new InvalidDataException("Resident outcome lost its complete typed proposal owner.");
        var proposal = change.Proposal;
        var record = new JsonObject
        {
            ["schemaVersion"] = 3, ["kind"] = "resident_target_consent",
            ["status"] = Text(proposal, "operation") == "grant" ? "granted" : "revoked",
            ["healer"] = SpiritualHealingRequestProjection.OwnerJson(new("chaos_sea", "player_soul", "player_soul", WoundCarrierCatalog.AfterlifeProfilesPath)),
            ["target"] = SpiritualHealingRequestProjection.OwnerJson(change.Owner), ["woundId"] = Text(proposal, "woundId"),
            ["locality"] = LocalityJson(locality), ["conscious"] = proposal["conscious"]!.DeepClone(),
            ["capable"] = proposal["capable"]!.DeepClone(), ["readableEvidence"] = Text(proposal, "readableEvidence"),
            ["binding"] = binding.DeepClone(), ["acceptedCycleOrdinal"] = cycleOrdinal,
            ["eligibleCycleOrdinal"] = checked(cycleOrdinal + 1), ["primaryTransitionId"] = primaryTransitionId
        };
        ValidateRecord(record);
        return new() { ["record"] = record, ["contentFingerprint"] = Fingerprint(record) };
    }

    private static JsonObject CreateResidentProviderOutcome(Change change, SpiritualAidLocality locality,
        JsonObject binding, int cycleOrdinal, string primaryTransitionId)
    {
        if (change.Owner is not { Realm: "chaos_sea", OwnerKind: "resident", CarrierPath: WoundCarrierCatalog.AfterlifeProfilesPath })
            throw new InvalidDataException("Resident offer lost its complete typed healer.");
        var proposal = change.Proposal;
        var record = new JsonObject
        {
            ["schemaVersion"] = 4, ["kind"] = "resident_provider_offer",
            ["status"] = Text(proposal, "operation") == "offer" ? "offered" : "revoked",
            ["healer"] = SpiritualHealingRequestProjection.OwnerJson(change.Owner),
            ["target"] = SpiritualHealingRequestProjection.OwnerJson(new("chaos_sea", "player_soul", "player_soul", WoundCarrierCatalog.AfterlifeProfilesPath)),
            ["woundId"] = Text(proposal, "woundId"), ["locality"] = LocalityJson(locality),
            ["readableEvidence"] = Text(proposal, "readableEvidence"), ["binding"] = binding.DeepClone(),
            ["acceptedCycleOrdinal"] = cycleOrdinal, ["eligibleCycleOrdinal"] = checked(cycleOrdinal + 1),
            ["primaryTransitionId"] = primaryTransitionId
        };
        foreach (var field in OfferConditionFields) record[field] = proposal[field]!.DeepClone();
        ValidateRecord(record);
        return new() { ["record"] = record, ["contentFingerprint"] = Fingerprint(record) };
    }

    // Exact typed join over already parsed operation DATA. Generic locality sets
    // deliberately allow aliases/defaults and cannot establish this authority.
    internal static LocalInteractionScope RequireResidentScope(JsonObject guardians, JsonObject? residents,
        JsonObject profiles, WoundOwnerCoordinate target, WoundMaterializationEnvelope wound)
    {
        if (target is not { Realm: "chaos_sea", OwnerKind: "resident", CarrierPath: WoundCarrierCatalog.AfterlifeProfilesPath } ||
            wound.Owner != target || wound.Classification.Domain != "spiritual" || wound.Lifecycle != "active" || wound.Display.Visibility is not ("public" or "known_to_player"))
            throw new InvalidDataException("Resident participation requires the exact known active wound owner.");
        return RequireResidentParticipantScope(guardians, residents, profiles, target);
    }

    internal static LocalInteractionScope RequireResidentParticipantScope(JsonObject guardians, JsonObject? residents,
        JsonObject profiles, WoundOwnerCoordinate target)
    {
        if (target is not { Realm: "chaos_sea", OwnerKind: "resident", CarrierPath: WoundCarrierCatalog.AfterlifeProfilesPath })
            throw new InvalidDataException("Resident participation requires its exact typed participant.");
        var navigation = guardians["chaosSeaNavigation"] as JsonObject
            ?? throw new InvalidDataException("Resident treatment requires current Guardian navigation.");
        var guardianId = Text(navigation, "currentGuardianId");
        var scope = RequireCurrentGuardianScope(guardians, residents, guardianId);
        var roster = (residents?[GuardianAbodeResidentState.EntriesProperty] as JsonArray)?.OfType<JsonObject>()
            .Where(r => string.Equals(r["residentId"]?.GetValue<string>(), target.OwnerId, StringComparison.OrdinalIgnoreCase)).ToArray();
        var owners = (profiles[AfterlifeEntityProfileState.ProfilesProperty] as JsonArray)?.OfType<JsonObject>()
            .Where(p => p["actorType"]?.GetValue<string>() == "resident" &&
                string.Equals(ProfileId(p), target.OwnerId, StringComparison.OrdinalIgnoreCase)).ToArray();
        if (roster is not { Length: 1 } || owners is not { Length: 1 } ||
            Text(roster[0], "residentId") != target.OwnerId || Text(roster[0], "guardianId") != guardianId ||
            Text(roster[0], "abodeId") != scope.LocationId || !Scalar<bool>(roster[0], "isPresent") ||
            ProfileId(owners[0]) != target.OwnerId || !WoundCarrierCollectionAuthority.AfterlifeProfileMatches(owners[0], target) ||
            Text(owners[0], "locationId") != scope.LocationId || !AfterlifeProfileVisibility.IsVisibleToPlayer(owners[0]))
            throw new InvalidDataException("Resident participation lost its exact current roster/profile/Guardian/Abode join.");
        foreach (var alias in new[] { "actorId", "actorRef" })
            if (roster[0].ContainsKey(alias) && Text(roster[0], alias) != target.OwnerId)
                throw new InvalidDataException("Resident roster has conflicting typed identity aliases.");
        return scope;
    }

    internal static LocalInteractionScope RequireResidentProviderScope(JsonObject guardians, JsonObject? residents,
        JsonObject profiles, WoundOwnerCoordinate healer, WoundMaterializationEnvelope wound)
    {
        if (wound.Owner != new WoundOwnerCoordinate("chaos_sea", "player_soul", "player_soul", WoundCarrierCatalog.AfterlifeProfilesPath) ||
            wound.Classification.Domain != "spiritual" || wound.Lifecycle != "active" || wound.Display.Visibility is not ("public" or "known_to_player"))
            throw new InvalidDataException("Resident provider requires the exact known active player wound.");
        return RequireResidentParticipantScope(guardians, residents, profiles, healer);
    }

    internal static LocalInteractionScope RequireAidScope(JsonObject guardians, JsonObject? residents,
        JsonObject profiles, SpiritualAidSelection aid, WoundMaterializationEnvelope wound)
    {
        if (aid.Resident is not { } resident) return RequireCurrentGuardianScope(guardians, residents, aid.GuardianId);
        var scope = aid.ReceiveAid
            ? RequireResidentProviderScope(guardians, residents, profiles, resident.Healer, wound)
            : RequireResidentScope(guardians, residents, profiles, resident.Target, wound);
        if (wound.Owner != resident.Target)
            throw new InvalidDataException("Resident aid patient differs from the exact wound owner.");
        if (scope.CurrentGuardianId != resident.Locality.GuardianId || scope.LocationId != resident.Locality.AbodeId)
            throw new InvalidDataException("Resident aid selection belongs to a different exact locality.");
        return scope;
    }
}
