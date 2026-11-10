using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using BookOfEternityClient.Core;

namespace BookOfEternityClient.Services;

internal static partial class ElyaraVisitService
{
    internal const string Marker = "[ELYARA_VISIT_V1]";
    internal static bool HasMarker(string action) => action.Contains(Marker, StringComparison.Ordinal);

    internal static string ActionFor(ElyaraVisitIntent intent)
    {
        var payload = new JsonObject
        {
            ["schemaVersion"] = 1, ["providerId"] = AfterlifeWoundTreatmentAuthority.ElyaraId,
            ["realm"] = "chaos_sea", ["generation"] = intent.Generation, ["sourceFingerprint"] = intent.SourceFingerprint,
            ["sourceAbodeId"] = intent.SourceAbodeId, ["sourceGuardianId"] = intent.SourceGuardianId,
            ["targetAbodeId"] = intent.TargetAbodeId, ["targetGuardianId"] = intent.TargetGuardianId
        };
        var encoded = Convert.ToBase64String(Encoding.UTF8.GetBytes(payload.ToJsonString()));
        return Marker + " " + encoded + "\n[CHAOS_SEA_TRAVEL] targetAbodeId=" + intent.TargetAbodeId +
            ", targetGuardianId=" + intent.TargetGuardianId + ", previousAbodeId=" + intent.SourceAbodeId +
            ", previousActiveGuardianId=" + intent.SourceGuardianId + ", targetAlreadyDiscovered=true.\n" +
            "Пойти к той же Элиаре Последней Раны в её текущую открытую Обитель. Это только путь: без активного лечения, лечебного броска и платы за лечение. " +
            "Продолжить один обычный безопасный посмертный цикл, используя обычный CHAOS_SEA_TRAVEL. " +
            "Штатное естественное восстановление этого цикла не отключается; авторствуй только запрошенные клиентом графы. " +
            "Сохранить каноническое местонахождение и жизнь Элиары, установить текущую Обитель и activeGuardian на хозяина цели. " +
            "После прибытия игрок отдельно откроет /лечить и подтвердит новую котировку. Не использовать Mortal World travel, не менять realm.";
    }

    internal static ElyaraVisitIntent ParseAction(string action)
    {
        if (!action.StartsWith(Marker + " ", StringComparison.Ordinal)) throw new InvalidDataException("Missing exact Elyara visit marker.");
        var end = action.IndexOf('\n');
        if (end <= Marker.Length + 1) throw new InvalidDataException("Missing visit payload.");
        var encoded = action[(Marker.Length + 1)..end];
        byte[] bytes;
        try { bytes = Convert.FromBase64String(encoded); }
        catch (FormatException error) { throw new InvalidDataException("Malformed visit payload.", error); }
        if (Convert.ToBase64String(bytes) != encoded) throw new InvalidDataException("Noncanonical visit payload encoding.");
        var root = StrictJsonAuthority.Deserialize<JsonObject>(CanonicalJsonUtf8.DecodeOneOptionalBom(bytes), new JsonSerializerOptions(), "Elyara visit")
            ?? throw new InvalidDataException("Missing visit payload object.");
        if (!AfterlifeHealingServiceContract.Closed(root, "schemaVersion", "providerId", "realm", "generation", "sourceFingerprint",
            "sourceAbodeId", "sourceGuardianId", "targetAbodeId", "targetGuardianId") ||
            AfterlifeHealingServiceContract.Number(root["schemaVersion"]) != 1 ||
            T(root["providerId"]) != AfterlifeWoundTreatmentAuthority.ElyaraId || T(root["realm"]) != "chaos_sea" ||
            string.IsNullOrWhiteSpace(T(root["generation"])) || !ResourceMaterializationContract.IsAuthorityFingerprint(T(root["sourceFingerprint"])))
            throw new InvalidDataException("Invalid closed Elyara visit identity.");
        foreach (var key in new[] { "sourceAbodeId", "sourceGuardianId", "targetAbodeId", "targetGuardianId" })
            if (!ResourceMaterializationContract.IsExactIdentifier(T(root[key]))) throw new InvalidDataException("Invalid visit coordinate.");
        var intent = new ElyaraVisitIntent(T(root["generation"])!, T(root["sourceFingerprint"])!, T(root["sourceAbodeId"])!,
            T(root["sourceGuardianId"])!, T(root["targetAbodeId"])!, T(root["targetGuardianId"])!);
        if (intent.SourceAbodeId == intent.TargetAbodeId || ActionFor(intent) != action)
            throw new InvalidDataException("The visit action or ordinary destination was altered.");
        return intent;
    }
}
