using System.Text.Json;
using System.Text.Json.Nodes;

namespace BookOfEternityClient.Services;

internal enum ElyaraVisitKind { Unavailable, Blocked, CoLocated, ChaosTravel }
internal sealed record ElyaraVisitIntent(string Generation, string SourceFingerprint,
    string SourceAbodeId, string SourceGuardianId, string TargetAbodeId, string TargetGuardianId);
internal sealed record ElyaraVisitCommandState(ElyaraVisitIntent Intent);
internal sealed record ElyaraVisitPlan(ElyaraVisitKind Kind, string Message, string LocationName,
    ElyaraVisitCommandState? State = null);
internal sealed record ElyaraVisitInput(JsonObject? Soul, JsonObject? Profiles, JsonObject? Guardians,
    JsonObject? Shining, JsonObject? Conflict);

/// <summary>Current provider/ordinary-route DATA; never authority retained across user interaction.</summary>
internal static partial class ElyaraVisitService
{
    internal const string Command = "/элиара";
    internal const string Label = "Пойти к Элиаре за лечением";
    internal const string BrowserSource = "browser-elyara-visit";
    internal const string UnavailableMessage = "Сейчас нет подтверждённого доступного пути к Элиаре за лечением.";

    // A navigation entry reveals no authority or destination. The actual command
    // reads/validates its own current sources; no duplicate full materialization
    // serialization is needed merely to offer this link from a wound view.
    internal static bool CanShowEntry(JsonObject? profiles)
    {
        try
        {
            var actor = RequireCanonicalActor(profiles);
            return T(actor["lifeState"]) == "alive" && AfterlifeProfileVisibility.IsVisibleToPlayer(actor);
        }
        catch (Exception error) when (IsInputFailure(error)) { return false; }
    }

    internal static JsonObject RequireCanonicalActor(JsonObject? profiles)
    {
        // Resolve across realms first: a second copy in another realm is not a
        // valid way to recover the same canonical Guardian after relocation.
        var matches = (profiles?["profiles"] as JsonArray)?.OfType<JsonObject>()
            .Where(AfterlifeHealingServiceContract.IsElyaraProfile).ToArray();
        if (matches is not { Length: 1 } || T(matches[0]["actorType"]) != "guardian" ||
            T(matches[0]["actorId"]) != AfterlifeWoundTreatmentAuthority.ElyaraId ||
            matches[0].ContainsKey("actorRef") && T(matches[0]["actorRef"]) != AfterlifeWoundTreatmentAuthority.ElyaraId)
            throw new InvalidDataException("The current canonical Elyara must have one exact identity across all realms.");
        AfterlifeHealingServiceContract.RequireElyaraInvariant(matches[0]);
        var location = T(matches[0]["locationId"]);
        foreach (var key in new[] { "currentLocationId", "hallId", "currentHallId", "currentAbodeId" })
            if (matches[0].ContainsKey(key) && T(matches[0][key]) != location)
                throw new InvalidDataException("Conflicting explicit current provider locality.");
        return matches[0];
    }

    internal static void RequireRegistryElyara(JsonObject? guardians)
    {
        var entries = (guardians?["guardians"] as JsonArray)?.OfType<JsonObject>().Where(g =>
            string.Equals(T(g["guardianId"]), AfterlifeWoundTreatmentAuthority.ElyaraId, StringComparison.OrdinalIgnoreCase)).ToArray();
        if (entries is not { Length: 1 } || T(entries[0]["guardianId"]) != AfterlifeWoundTreatmentAuthority.ElyaraId)
            throw new InvalidDataException("Public service requires the unique exact canonical Elyara Guardian.");
    }

    internal static LocalInteractionScope RequirePlayerScope(string realm, JsonObject guardians, JsonObject? shining)
    {
        if (realm == "chaos_sea") return AfterlifeWoundTreatmentAuthority.RequireServicePlayerScope(guardians);
        RequireRegistryElyara(guardians);
        if (realm != "shining_abode") throw new InvalidDataException("Unknown afterlife locality.");
        return ShiningCurrentHallContract.RequireCurrentScope(shining);
    }

    internal static ElyaraVisitPlan Project(ElyaraVisitInput input, string generation, string fingerprint)
    {
        try
        {
            var actor = RequireCanonicalActor(input.Profiles);
            if (!AfterlifeProfileVisibility.IsVisibleToPlayer(actor) || T(actor["lifeState"]) != "alive")
                return Unavailable();
            if (!AfterlifeEntityProfileState.TryNormalizeEffectRealm(T(input.Soul?["currentRealm"]), out var realm) ||
                realm is not ("chaos_sea" or "shining_abode") || input.Guardians is null ||
                string.IsNullOrWhiteSpace(generation) || !ResourceMaterializationContract.IsAuthorityFingerprint(fingerprint))
                return Unavailable();
            if (input.Conflict?["activeConflict"] is not null ||
                SpiritualTreatmentParticipation.HasUnsettledChange(input.Profiles) ||
                realm == "shining_abode" && ShiningAbodeState.GetPreparedIncarnationPackageMode(input.Shining) !=
                    ShiningAbodeState.PreparedIncarnationPackageMode.Absent)
                return Blocked("Сначала завершите текущий конфликт или подготовку другого действия.");
            if (ActorMaterializationContract.ValidateAfterlifeProfile(JsonSerializer.SerializeToElement(actor),
                AfterlifeEntityProfileState.StatePath, requireEnvelope: true, canTradeEvidence: null).Count != 0)
                return Unavailable();
            var scope = RequirePlayerScope(realm, input.Guardians, input.Shining);
            if (!AfterlifeEntityProfileState.TryNormalizeEffectRealm(T(actor["realm"]), out var actorRealm)) return Unavailable();
            if (T(actor["healingServiceProfile"]?["availability"]) != "available")
                return Blocked("Элиара сейчас не объявила доступный приём. Дорога, лечение и оплата не начинаются.");
            if (actorRealm != realm)
                return Blocked("Элиара сейчас в другом посмертном мире. Сначала нужен обычный доступный путь; команда лечения не меняет realm.");
            var location = T(actor["locationId"])!;
            if (location == scope.LocationId)
            {
                if (realm == "shining_abode" && !LocalInteractionScopeService.IsAfterlifeActorLocal(scope, actor))
                    return Blocked("Присутствие Элиары в текущем зале не подтверждено.");
                return new(ElyaraVisitKind.CoLocated,
                    "Элиара рядом. Откройте лечение и отдельно подтвердите актуальную цену; просмотр ничего не расходует.", scope.LocationName);
            }
            if (realm == "shining_abode")
                return Blocked("Элиара находится в другом зале. Подтверждённого обычного перехода к ней сейчас нет; телепортация не выполняется.");
            var hosts = (input.Guardians["guardians"] as JsonArray)?.OfType<JsonObject>()
                .Where(g => string.Equals(T(g["abode"]?["abodeId"]), location, StringComparison.OrdinalIgnoreCase)).ToArray();
            if (hosts is not { Length: 1 } || T(hosts[0]["abode"]?["abodeId"]) != location ||
                T(hosts[0]["guardianId"]) is not { } host || !ResourceMaterializationContract.IsExactIdentifier(host) ||
                hosts[0]["abode"]?["isDiscovered"] is not JsonValue discovered || !discovered.TryGetValue<bool>(out var known) || !known ||
                input.Guardians["chaosSeaNavigation"]?["discoveredAbodes"] is not JsonArray discoveredAbodes ||
                !discoveredAbodes.Any(id => T(id) == location))
                return Blocked("Текущая Обитель Элиары не подтверждена как открытая и доступная для обычного перехода.");
            var intent = new ElyaraVisitIntent(generation, fingerprint, scope.LocationId, scope.CurrentGuardianId, location, host);
            return new(ElyaraVisitKind.ChaosTravel,
                "Переход к Элиаре займёт один обычный цикл мира. Лечение и оплата не начинаются; после прибытия нужна новая котировка и подтверждение.",
                T(hosts[0]["abode"]?["name"]) ?? "Открытая Обитель", new(intent));
        }
        catch (Exception error) when (IsInputFailure(error)) { return Unavailable(); }
    }

    private static ElyaraVisitPlan Unavailable() => new(ElyaraVisitKind.Unavailable, UnavailableMessage, string.Empty);
    private static ElyaraVisitPlan Blocked(string message) => new(ElyaraVisitKind.Blocked, message, string.Empty);
    private static string? T(JsonNode? node) => AfterlifeHealingServiceContract.Text(node);
    private static bool IsInputFailure(Exception error) => error is JsonException or InvalidDataException or
        InvalidOperationException or ArgumentException or OverflowException;
}
