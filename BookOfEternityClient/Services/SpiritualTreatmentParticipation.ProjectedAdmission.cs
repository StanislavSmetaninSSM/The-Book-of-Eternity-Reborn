using System.Text.Json.Nodes;

namespace BookOfEternityClient.Services;

internal sealed record SpiritualProjectedShiningAidAdmission(WoundOwnerCoordinate Actor,
    SpiritualAidLocality Locality, bool IsPatient, string Fingerprint);

internal static partial class SpiritualTreatmentParticipation
{
    // Detached current projection DATA, not a grant or publication permission.
    // The existing owner supplies its retained record and parsed original roots.
    internal static SpiritualProjectedShiningAidAdmission RequireProjectedShiningAidAdmission(JsonObject record,
        JsonObject? guardians, JsonObject? residents, JsonObject profiles, JsonObject? shining)
    {
        ValidateRecord(record);
        if (RecordRealm(record) != "shining_abode")
            throw new InvalidDataException("Projected Shining aid requires its exact Shining participation family.");
        var aid = Selection(record);
        if (aid.ShiningPatient is { } patient)
        {
            var admission = RequireShiningPatientAdmission(shining, profiles, residents, patient.Target, patient.Locality);
            return new(patient.Target, patient.Locality, true, admission.Fingerprint);
        }
        if (!aid.ReceiveAid)
            throw new InvalidDataException("Projected Shining provider admission requires the receiving-aid direction.");
        var provider = aid.Healer("shining_abode");
        var locality = aid.ExplicitLocality ?? throw new InvalidDataException("The retained Shining provider has no exact locality.");
        var provided = RequireShiningProviderAdmission(guardians, residents, profiles, shining, provider, locality);
        return new(provider, locality, false, provided.Fingerprint);
    }

    internal static void RequireSameProjectedShiningPatientAdmission(SpiritualProjectedShiningAidAdmission? expected,
        SpiritualProjectedShiningAidAdmission actual)
    {
        if (expected is not { IsPatient: true } || !actual.IsPatient || expected != actual)
            throw new InvalidDataException("The actual Shining patient differs from its exact owned client projection.");
    }
}
