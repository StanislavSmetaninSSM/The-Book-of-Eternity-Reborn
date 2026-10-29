using System.Text.Json.Nodes;

namespace BookOfEternityClient.Services;

internal static partial class WoundApplicationService
{
    // Pure menu projection over the enclosing read operation's already parsed
    // profiles, mechanics, Soul and locality. No independent IO or retained cache.
    internal static IReadOnlyList<WoundTreatmentChoice> BuildPublicServiceChoices(WoundPlayerReadContext context,
        JsonObject? guardians, IReadOnlyList<WoundTreatmentChoice> existing, JsonObject? shining = null)
    {
        var realm = context.Input.Realm;
        if (!context.Input.SelfVisible || realm is not ("chaos_sea" or "shining_abode") || guardians is null ||
            context.SpiritualConflict?["activeConflict"] is not null ||
            context.Input.Carriers.AfterlifeProfiles?["profiles"] is not JsonArray ||
            realm == "shining_abode" && ShiningAbodeState.GetPreparedIncarnationPackageMode(shining) !=
                ShiningAbodeState.PreparedIncarnationPackageMode.Absent ||
            SpiritualTreatmentParticipation.HasUnsettledChange(context.Input.Carriers.AfterlifeProfiles)) return existing;
        try
        {
            var provider = new WoundOwnerCoordinate(realm, "guardian", AfterlifeWoundTreatmentAuthority.ElyaraId,
                WoundCarrierCatalog.AfterlifeProfilesPath);
            var actor = ElyaraVisitService.RequireCanonicalActor(context.Input.Carriers.AfterlifeProfiles);
            if (!WoundCarrierCollectionAuthority.AfterlifeProfileMatches(actor, provider) ||
                AfterlifeHealingServiceContract.ReadPublicCurrencyProfile(provider, actor) is not { } profile)
                return existing;
            var scope = ElyaraVisitService.RequirePlayerScope(realm, guardians, shining);
            if (profile.LocationRef != scope.LocationId ||
                realm == "shining_abode" && !LocalInteractionScopeService.IsAfterlifeActorLocal(scope, actor)) return existing;
            var choices = existing.ToList();
            foreach (var wound in context.Catalog.Occurrences.Select(o => o.Wound).Where(w => w.Lifecycle == "active" &&
                w.Owner == new WoundOwnerCoordinate(realm, "player_soul", "player_soul", WoundCarrierCatalog.AfterlifeProfilesPath) && w.Classification.Domain == "spiritual" &&
                WoundPlayerProjection.Visible(w.Display.Visibility)))
            {
                var check = SpiritualHealingResolver.PrepareSource(new(wound, provider, actor, context.Soul, context.Input.Effects)).Source;
                if (check is null || AfterlifeHealingServiceContract.CreateQuote(profile, wound, check, scope.LocationId) is not { } quote) continue;
                var available = GuardianPolicyContracts.TryPrepareHealingFeatherWallet(context.Soul!, quote.Amount, out _);
                choices.Add(new("wound_" + choices.Count, new SpiritualSelfTreatmentSelection(wound.WoundId, quote.WoundFingerprint)
                    { Service = new(quote) },
                    "Себя: " + WoundPlayerProjection.Text(wound.Display.Name) + " — Элиара, " + quote.Amount + " Чернильных Перьев",
                    wound.Severity.Value, check.HealingTier, available,
                    available ? "Публичное лечение: " + quote.Amount + " Чернильных Перьев за принятую попытку, включая неудачу. Один безопасный цикл мира; без затрат ОД. Отмена и откат не оплачиваются."
                        : "Недостаточно Чернильных Перьев для указанной цены. Чтение предложения ничего не расходует."));
            }
            return choices;
        }
        catch (Exception failure) when (failure is System.Text.Json.JsonException or InvalidDataException or InvalidOperationException or ArgumentException or OverflowException)
        { return existing; }
    }

    internal static string ConfirmationFor(SpiritualSelfTreatmentSelection selection) => selection.Service is { } service
        ? "Подтвердить лечение у Элиары за " + service.Quote.Amount + " Чернильных Перьев за принятую попытку, включая неудачу, и один цикл мира?"
        : selection.Combat is not null ? "Подтвердить прерываемое лечение за ОД в этом обмене?"
        : "Подтвердить лечение без затрат ОД и один цикл мира?";
}
