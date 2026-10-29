using System.Text.Json.Nodes;

namespace BookOfEternityClient.Services;

internal static partial class WoundApplicationService
{
    // Pure menu projection over the enclosing read operation's already parsed
    // profiles, mechanics, Soul and locality. No independent IO or retained cache.
    internal static IReadOnlyList<WoundTreatmentChoice> BuildPublicServiceChoices(WoundPlayerReadContext context,
        JsonObject? guardians, IReadOnlyList<WoundTreatmentChoice> existing)
    {
        if (!context.Input.SelfVisible || context.Input.Realm != "chaos_sea" || guardians is null ||
            context.SpiritualConflict?["activeConflict"] is not null ||
            context.Input.Carriers.AfterlifeProfiles?["profiles"] is not JsonArray profiles ||
            SpiritualTreatmentParticipation.HasUnsettledChange(context.Input.Carriers.AfterlifeProfiles)) return existing;
        try
        {
            var provider = new WoundOwnerCoordinate("chaos_sea", "guardian", AfterlifeWoundTreatmentAuthority.ElyaraId,
                WoundCarrierCatalog.AfterlifeProfilesPath);
            var actors = profiles.OfType<JsonObject>().Where(p => WoundCarrierCollectionAuthority.AfterlifeProfileMatches(p, provider)).ToArray();
            if (actors is not { Length: 1 } || AfterlifeHealingServiceContract.ReadPublicCurrencyProfile(provider, actors[0]) is not { } profile)
                return existing;
            var scope = SpiritualTreatmentParticipation.RequireCurrentGuardianScope(guardians, null, provider.OwnerId);
            if (profile.LocationRef != scope.LocationId) return existing;
            var choices = existing.ToList();
            foreach (var wound in context.Catalog.Occurrences.Select(o => o.Wound).Where(w => w.Lifecycle == "active" &&
                w.Owner == new WoundOwnerCoordinate("chaos_sea", "player_soul", "player_soul", WoundCarrierCatalog.AfterlifeProfilesPath) && w.Classification.Domain == "spiritual" &&
                WoundPlayerProjection.Visible(w.Display.Visibility)))
            {
                var check = SpiritualHealingResolver.PrepareSource(new(wound, provider, actors[0], context.Soul, context.Input.Effects)).Source;
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
