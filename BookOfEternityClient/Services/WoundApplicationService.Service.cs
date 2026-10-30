using System.Text.Json.Nodes;

namespace BookOfEternityClient.Services;

internal static partial class WoundApplicationService
{
    // Pure menu projection over the enclosing read operation's already parsed
    // profiles, mechanics, Soul and locality. No independent IO or retained cache.
    /// <summary>Builds detached choices from current parsed DATA without issuing treatment or payment authority.</summary>
    /// <param name="context">The enclosing read operation's wound, profile and mechanics DATA.</param>
    /// <param name="guardians">Current Guardian locality for Elyara, independent of faction service access.</param>
    /// <param name="existing">Existing self/aid choices to retain.</param>
    /// <param name="shining">Current Shining state when in that realm.</param>
    /// <param name="residents">One current parsed roster shared by all faction providers.</param>
    /// <returns>Detached choices; fresh confirmation/staging/publication remains mandatory.</returns>
    internal static IReadOnlyList<WoundTreatmentChoice> BuildPublicServiceChoices(WoundPlayerReadContext context,
        JsonObject? guardians, IReadOnlyList<WoundTreatmentChoice> existing, JsonObject? shining = null,
        JsonObject? residents = null)
    {
        var realm = context.Input.Realm;
        if (!context.Input.SelfVisible || realm is not ("chaos_sea" or "shining_abode") ||
            context.SpiritualConflict?["activeConflict"] is not null ||
            context.Input.Carriers.AfterlifeProfiles?["profiles"] is not JsonArray ||
            realm == "shining_abode" && ShiningAbodeState.GetPreparedIncarnationPackageMode(shining) !=
                ShiningAbodeState.PreparedIncarnationPackageMode.Absent ||
            SpiritualTreatmentParticipation.HasUnsettledChange(context.Input.Carriers.AfterlifeProfiles)) return existing;
        var providers = new List<(AfterlifeHealingServiceProfile Profile, string Name)>();
        // Elyara retains her existing registry and locality contract. A refusal
        // here cannot impersonate or hide an independent faction provider.
        try
        {
            if (guardians is not null)
            {
                var provider = new WoundOwnerCoordinate(realm, "guardian", AfterlifeWoundTreatmentAuthority.ElyaraId,
                    WoundCarrierCatalog.AfterlifeProfilesPath);
                var actor = ElyaraVisitService.RequireCanonicalActor(context.Input.Carriers.AfterlifeProfiles);
                if (WoundCarrierCollectionAuthority.AfterlifeProfileMatches(actor, provider) &&
                    AfterlifeHealingServiceContract.ReadPublicCurrencyProfile(provider, actor) is { } profile)
                {
                    var scope = ElyaraVisitService.RequirePlayerScope(realm, guardians, shining);
                    if (profile.LocationRef == scope.LocationId && (realm != "shining_abode" ||
                        LocalInteractionScopeService.IsAfterlifeActorLocal(scope, actor))) providers.Add((profile, "Элиара"));
                }
            }
        }
        catch (Exception failure) when (failure is System.Text.Json.JsonException or InvalidDataException or InvalidOperationException or ArgumentException or OverflowException)
        { /* This independent provider is unavailable; no access is inferred. */ }
        if (realm == "shining_abode" && shining is not null && residents is not null)
        {
            try
            {
                foreach (var service in ShiningFactionHealingSupport.ProjectCurrentCurrencyServices(
                    shining, residents, context.Input.Carriers.AfterlifeProfiles!))
                    providers.Add((service.Profile, WoundPlayerProjection.Text(service.Name)));
            }
            catch (Exception failure) when (failure is System.Text.Json.JsonException or InvalidDataException or InvalidOperationException or ArgumentException or OverflowException)
            { /* Malformed faction DATA grants no service; Elyara stays independent. */ }
        }
        var choices = existing.ToList();
        var wounds = context.Catalog.Occurrences.Select(o => o.Wound).Where(w => w.Lifecycle == "active" &&
            w.Owner == new WoundOwnerCoordinate(realm, "player_soul", "player_soul", WoundCarrierCatalog.AfterlifeProfilesPath) &&
            w.Classification.Domain == "spiritual" && WoundPlayerProjection.Visible(w.Display.Visibility)).ToArray();
        foreach (var (profile, name) in providers)
        {
            var actor = context.Input.Carriers.AfterlifeProfiles!["profiles"]!.AsArray().OfType<JsonObject>().Single(p =>
                WoundCarrierCollectionAuthority.AfterlifeProfileMatches(p, profile.Provider));
            foreach (var wound in wounds)
            {
                var check = SpiritualHealingResolver.PrepareSource(new(wound, profile.Provider, actor, context.Soul, context.Input.Effects)).Source;
                if (check is null || AfterlifeHealingServiceContract.CreateQuote(profile, wound, check, profile.LocationRef) is not { } quote) continue;
                var available = GuardianPolicyContracts.TryPrepareHealingFeatherWallet(context.Soul!, quote.Amount, out _);
                choices.Add(new("wound_" + choices.Count, new SpiritualSelfTreatmentSelection(wound.WoundId, quote.WoundFingerprint)
                    { Service = new(quote) },
                    "Себя: " + WoundPlayerProjection.Text(wound.Display.Name) + " — " + name + ", " + quote.Amount + " Чернильных Перьев",
                    wound.Severity.Value, check.HealingTier, available,
                    available ? "Публичное лечение: " + quote.Amount + " Чернильных Перьев за принятую попытку, включая неудачу. Один безопасный цикл мира; без затрат ОД. Отмена и откат не оплачиваются."
                        : "Недостаточно Чернильных Перьев для указанной цены. Чтение предложения ничего не расходует."));
            }
        }
        return choices.AsReadOnly();
    }

    internal static string ConfirmationFor(SpiritualSelfTreatmentSelection selection) => selection.Service is { } service
        ? "Подтвердить лечение " + (service.Quote.Provider.OwnerKind == "resident" ? "у выбранного целителя фракции" : "у Элиары") +
            " за " + service.Quote.Amount + " Чернильных Перьев за принятую попытку, включая неудачу, и один цикл мира?"
        : selection.Combat is not null ? "Подтвердить прерываемое лечение за ОД в этом обмене?"
        : "Подтвердить лечение без затрат ОД и один цикл мира?";
}
