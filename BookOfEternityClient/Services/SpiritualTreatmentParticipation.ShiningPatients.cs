using System.Text.Json.Nodes;

namespace BookOfEternityClient.Services;

internal sealed record SpiritualShiningPatientParticipant(WoundOwnerCoordinate Healer,
    WoundOwnerCoordinate Target, SpiritualAidLocality Locality);

internal static partial class SpiritualTreatmentParticipation
{
    internal static bool IsShiningPatientParticipation(JsonObject record) => Scalar<int>(record, "schemaVersion") == 5;

    // A closed coordinate reader for the new family only; legacy ReadOwner stays strict.
    internal static WoundOwnerCoordinate ReadShiningPatientOwner(JsonNode? value)
    {
        var root = value as JsonObject ?? throw new InvalidDataException("Shining patient participation requires a typed owner.");
        Closed(root, "realm", "ownerKind", "ownerId", "carrierPath");
        var owner = new WoundOwnerCoordinate(Text(root, "realm"), Text(root, "ownerKind"), Text(root, "ownerId"), Text(root, "carrierPath"));
        if (owner.Realm != "shining_abode" || owner.OwnerKind is not ("player_soul" or "resident" or "radiant_actor") ||
            !ResourceMaterializationContract.IsExactIdentifier(owner.OwnerId) || owner.CarrierPath != WoundCarrierCatalog.AfterlifeProfilesPath)
            throw new InvalidDataException("Shining patient owner differs from its exact supported profile family.");
        return owner;
    }

    private static void ValidateShiningPatientProposal(JsonObject proposal)
    {
        Closed(proposal, "schemaVersion", "kind", "operation", "healerId", "woundId", "locality", "conscious", "capable", "readableEvidence");
        if (Text(proposal, "kind") != "shining_target_consent" || Text(proposal, "operation") is not ("grant" or "revoke") ||
            Text(proposal, "healerId") != "player_soul" || ReadLocality(proposal["locality"]).Value is not SpiritualAidLocality.ShiningFactionHall)
            throw new InvalidDataException("Invalid closed Shining patient consent proposal.");
        var conscious = Scalar<bool>(proposal, "conscious"); var capable = Scalar<bool>(proposal, "capable");
        if (Text(proposal, "operation") == "grant" && (!conscious || !capable))
            throw new InvalidDataException("A Shining patient grant requires conscious and capable participation.");
    }

    private static void RequireShiningPatientChangeOwner(Change change, SpiritualAidLocality locality)
    {
        if (change.Owner is not { Realm: "shining_abode", OwnerKind: "resident" or "radiant_actor", CarrierPath: WoundCarrierCatalog.AfterlifeProfilesPath } ||
            !ResourceMaterializationContract.IsExactIdentifier(change.Owner.OwnerId) ||
            locality.Value is not SpiritualAidLocality.ShiningFactionHall ||
            ReadLocality(change.Proposal["locality"]) != locality)
            throw new InvalidDataException("Shining patient change differs from its exact enclosing owner and faction hall.");
    }

    private static void ValidateShiningPatientRecord(JsonObject record)
    {
        Closed(record, "schemaVersion", "kind", "status", "healer", "target", "woundId", "locality", "conscious", "capable",
            "readableEvidence", "binding", "acceptedCycleOrdinal", "eligibleCycleOrdinal", "primaryTransitionId");
        var healer = ReadShiningPatientOwner(record["healer"]); var target = ReadShiningPatientOwner(record["target"]);
        if (Text(record, "kind") != "shining_target_consent" || Text(record, "status") is not ("granted" or "revoked") ||
            healer != new WoundOwnerCoordinate("shining_abode", "player_soul", "player_soul", WoundCarrierCatalog.AfterlifeProfilesPath) ||
            target.OwnerKind is not ("resident" or "radiant_actor") ||
            ReadLocality(record["locality"]).Value is not SpiritualAidLocality.ShiningFactionHall)
            throw new InvalidDataException("Invalid canonical Shining patient consent record.");
        var conscious = Scalar<bool>(record, "conscious"); var capable = Scalar<bool>(record, "capable");
        if (Text(record, "status") == "granted" && (!conscious || !capable))
            throw new InvalidDataException("A canonical Shining patient grant requires conscious and capable participation.");
    }

    private static JsonObject CreateShiningPatientOutcome(Change change, SpiritualAidLocality locality,
        JsonObject binding, int cycleOrdinal, string primaryTransitionId)
    {
        ValidateProposal(change.Proposal); RequireShiningPatientChangeOwner(change, locality);
        var proposal = change.Proposal;
        var record = new JsonObject
        {
            ["schemaVersion"] = 5, ["kind"] = "shining_target_consent",
            ["status"] = Text(proposal, "operation") == "grant" ? "granted" : "revoked",
            ["healer"] = SpiritualHealingRequestProjection.OwnerJson(new("shining_abode", "player_soul", "player_soul", WoundCarrierCatalog.AfterlifeProfilesPath)),
            ["target"] = SpiritualHealingRequestProjection.OwnerJson(change.Owner), ["woundId"] = Text(proposal, "woundId"),
            ["locality"] = LocalityJson(locality), ["conscious"] = proposal["conscious"]!.DeepClone(),
            ["capable"] = proposal["capable"]!.DeepClone(), ["readableEvidence"] = Text(proposal, "readableEvidence"),
            ["binding"] = binding.DeepClone(), ["acceptedCycleOrdinal"] = cycleOrdinal,
            ["eligibleCycleOrdinal"] = checked(cycleOrdinal + 1), ["primaryTransitionId"] = primaryTransitionId
        };
        ValidateRecord(record);
        return new() { ["record"] = record, ["contentFingerprint"] = Fingerprint(record) };
    }
}
