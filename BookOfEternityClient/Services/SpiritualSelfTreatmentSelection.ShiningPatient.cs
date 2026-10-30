using System.Text.Json.Nodes;

namespace BookOfEternityClient.Services;

internal sealed partial record SpiritualSelfTreatmentSelection
{
    private JsonObject ShiningPatientJson()
    {
        if (Service is not null || Combat is not null || Aid?.ShiningPatient is not { } patient ||
            !ResourceMaterializationContract.IsExactIdentifier(WoundId) ||
            !ResourceMaterializationContract.IsAuthorityFingerprint(ExpectedWoundFingerprint) ||
            !ResourceMaterializationContract.IsAuthorityFingerprint(Aid.ParticipationFingerprint))
            throw new InvalidDataException("Invalid exact Shining patient treatment selection.");
        _ = Aid.Healer("shining_abode"); _ = Aid.Target("shining_abode");
        return new()
        {
            ["schemaVersion"] = 8, ["mode"] = "shining_entity_aid", ["direction"] = "player_to_shining_entity",
            ["woundId"] = WoundId, ["expectedWoundFingerprint"] = ExpectedWoundFingerprint,
            ["healer"] = SpiritualHealingRequestProjection.OwnerJson(patient.Healer),
            ["target"] = SpiritualHealingRequestProjection.OwnerJson(patient.Target),
            ["locality"] = SpiritualTreatmentParticipation.LocalityJson(patient.Locality),
            ["participationFingerprint"] = Aid.ParticipationFingerprint
        };
    }

    private static SpiritualSelfTreatmentSelection ParseShiningPatient(SpiritualSelfTreatmentSelection selection, JsonObject root)
    {
        var fields = new[] { "schemaVersion", "mode", "direction", "woundId", "expectedWoundFingerprint", "healer", "target", "locality", "participationFingerprint" };
        if (root.Count != fields.Length || fields.Any(f => !root.ContainsKey(f)) ||
            root["mode"]?.GetValue<string>() != "shining_entity_aid" || root["direction"]?.GetValue<string>() != "player_to_shining_entity" ||
            !ResourceMaterializationContract.IsAuthorityFingerprint(root["participationFingerprint"]?.GetValue<string>()))
            throw new InvalidDataException("Invalid closed Shining patient selection.");
        var healer = SpiritualTreatmentParticipation.ReadShiningPatientOwner(root["healer"]);
        var target = SpiritualTreatmentParticipation.ReadShiningPatientOwner(root["target"]);
        var locality = SpiritualTreatmentParticipation.ReadLocality(root["locality"]);
        var selected = selection with { Aid = new(healer.OwnerId, target.OwnerId, root["participationFingerprint"]!.GetValue<string>())
            { ShiningPatient = new(healer, target, locality) } };
        if (!JsonNode.DeepEquals(selected.ToJson(), root))
            throw new InvalidDataException("Shining patient selection differs from its exact typed coordinate.");
        return selected;
    }
}
