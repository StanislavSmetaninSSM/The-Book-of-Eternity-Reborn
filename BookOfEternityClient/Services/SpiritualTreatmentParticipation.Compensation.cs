using System.Text.Json.Nodes;

namespace BookOfEternityClient.Services;

/// <summary>Explicit consideration DATA; only an actually confirmed accepted treatment binds its terms.</summary>
internal sealed record SpiritualTreatmentCompensation(string Kind, string AgreementRef, string ReadableTerms);

internal static partial class SpiritualTreatmentParticipation
{
    internal static SpiritualTreatmentCompensation? ReadCompensation(JsonObject source)
    {
        if (!source.ContainsKey("compensation")) return null;
        var value = source["compensation"] as JsonObject
            ?? throw new InvalidDataException("Compensation must be a closed nonnull object.");
        Closed(value, "kind", "agreementRef", "readableTerms");
        var result = new SpiritualTreatmentCompensation(Text(value, "kind"), Text(value, "agreementRef"), Text(value, "readableTerms"));
        ValidateCompensation(result);
        return result;
    }

    internal static JsonObject CompensationJson(SpiritualTreatmentCompensation value)
    {
        ValidateCompensation(value);
        return new() { ["kind"] = value.Kind, ["agreementRef"] = value.AgreementRef, ["readableTerms"] = value.ReadableTerms };
    }

    private static void ValidateCompensation(SpiritualTreatmentCompensation value)
    {
        if (value.Kind is not ("favor" or "debt" or "quest" or "allegiance" or "free_aid") ||
            !ResourceMaterializationContract.IsExactIdentifier(value.AgreementRef) || string.IsNullOrWhiteSpace(value.ReadableTerms))
            throw new InvalidDataException("Compensation requires an exact noncurrency kind, agreement reference and readable terms.");
    }

    private static string[] ProviderFields(JsonObject value, IEnumerable<string> required)
    {
        var fields = required.ToList();
        foreach (var optional in new[] { "compensation", "locality" })
            if (value.ContainsKey(optional) && !fields.Contains(optional, StringComparer.Ordinal)) fields.Add(optional);
        return fields.ToArray();
    }

    private static void CopyCompensation(JsonObject source, JsonObject target)
    {
        if (ReadCompensation(source) is { } value) target["compensation"] = CompensationJson(value);
    }

    private static void RequireProposedProviderLocality(Change change, SpiritualAidLocality locality)
    {
        RequireProviderLocality(change.Owner, locality);
        if (change.Owner.Realm == "shining_abode")
        {
            if (ReadLocality(change.Proposal["locality"]) != locality)
                throw new InvalidDataException("Provider proposal locality differs from independently resolved current sources.");
        }
        else if (change.Proposal.ContainsKey("locality"))
            throw new InvalidDataException("A Chaos proposal cannot add a Shining locality alias.");
    }

    internal static void RequireProviderLocality(WoundOwnerCoordinate healer, SpiritualAidLocality locality)
    {
        if (healer.CarrierPath != WoundCarrierCatalog.AfterlifeProfilesPath || healer.Realm != locality.Realm ||
            ((healer.OwnerKind, locality.Value) switch
            {
                ("guardian", SpiritualAidLocality.ChaosAbode chaos) => chaos.GuardianId != healer.OwnerId,
                ("resident", SpiritualAidLocality.ChaosAbode) => false,
                ("resident", SpiritualAidLocality.ShiningFactionHall) => false,
                ("guardian", SpiritualAidLocality.ElyaraShiningHall) => healer.OwnerId != AfterlifeWoundTreatmentAuthority.ElyaraId,
                _ => true
            }))
            throw new InvalidDataException("Provider owner and exact locality variant differ.");
    }
}
