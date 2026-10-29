using System.Text.Json.Nodes;
using System.Text.Json.Serialization;

namespace BookOfEternityClient.Services;

internal sealed record WoundTargetWoundSummary(string Name, string Severity, string Description,
    [property: JsonIgnore] string WoundId, [property: JsonIgnore] string Fingerprint);

internal sealed record WoundTargetChoice(string Value, string Name, string Context,
    IReadOnlyList<WoundTargetWoundSummary> Wounds,
    [property: JsonIgnore] WoundOwnerCoordinate Owner,
    [property: JsonIgnore] SpiritualAidLocality? Locality,
    [property: JsonIgnore] string BindingFingerprint,
    bool Available = true, string Reason = "")
{
    public string Label { get; init; } = Name;
}

/// <summary>Detached current-target navigation; visibility never grants treatment permission.</summary>
internal static class WoundTargetSelectionService
{
    internal static IReadOnlyList<WoundTargetChoice> Build(WoundPlayerReadContext context,
        JsonObject? guardians, JsonObject? residents)
    {
        if (!context.Input.SelfVisible) return Array.Empty<WoundTargetChoice>();
        var realm = context.Input.Realm;
        var selfOwner = new WoundOwnerCoordinate(realm, realm == "mortal_world" ? "player" : "player_soul",
            realm == "mortal_world" ? "player_current" : "player_soul",
            realm == "mortal_world" ? WoundCarrierCatalog.PlayerPath : WoundCarrierCatalog.AfterlifeProfilesPath);
        // One index over the caller's prepared catalog; no rebuild, filesystem or retained cache.
        var wounds = context.Catalog.Occurrences.Select(o => o.Wound).Where(w => w.Lifecycle == "active" &&
            w.Owner.Realm == realm && WoundPlayerProjection.Visible(w.Display.Visibility)).ToLookup(w => w.Owner);
        var targets = new List<WoundTargetChoice>();
        SpiritualAidLocality? locality = null;
        LocalInteractionScope? scope = null;
        var profiles = (context.Input.Carriers.AfterlifeProfiles?["profiles"] as JsonArray)?.OfType<JsonObject>().ToArray() ?? [];
        if (realm == "chaos_sea" && context.SpiritualConflict?["activeConflict"] is null && guardians is not null)
        {
            try
            {
                scope = SpiritualTreatmentParticipation.RequireCurrentGuardianScope(guardians, residents,
                    guardians["chaosSeaNavigation"]?["currentGuardianId"]?.GetValue<string>() ?? "");
                locality = new(scope.CurrentGuardianId!, scope.LocationId!);
            }
            catch (Exception failure) when (Malformed(failure)) { }
        }
        targets.Add(Create(selfOwner, "Себя", "Ваш текущий персонаж", locality));
        if (scope is null || locality is null) return targets.AsReadOnly();
        var abodeName = Read(guardians!["activeGuardian"]?["abode"]?["name"]);
        if (abodeName.Length == 0) abodeName = "Текущая Обитель";
        var nearby = new List<WoundTargetChoice>();
        try
        {
            var guardianOwner = new WoundOwnerCoordinate("chaos_sea", "guardian", locality.GuardianId, WoundCarrierCatalog.AfterlifeProfilesPath);
            var matching = profiles.Where(p => p["actorType"]?.GetValue<string>() == "guardian" &&
                string.Equals(SpiritualTreatmentParticipation.ProfileId(p), guardianOwner.OwnerId, StringComparison.OrdinalIgnoreCase)).ToArray();
            if (matching is { Length: 1 } && SpiritualTreatmentParticipation.ProfileId(matching[0]) == guardianOwner.OwnerId &&
                WoundCarrierCollectionAuthority.AfterlifeProfileMatches(matching[0], guardianOwner) &&
                matching[0]["locationId"]?.GetValue<string>() == scope.LocationId && AfterlifeProfileVisibility.IsVisibleToPlayer(matching[0]))
                nearby.Add(Create(guardianOwner, Name(matching[0], Read(guardians["activeGuardian"]?["canonicalName"]), "Хранитель"),
                    "Хранитель · " + abodeName, locality));
        }
        catch (Exception failure) when (Malformed(failure)) { }
        foreach (var profile in profiles.Where(p => p["actorType"]?.GetValue<string>() == "resident"))
        {
            try
            {
                var owner = new WoundOwnerCoordinate("chaos_sea", "resident", SpiritualTreatmentParticipation.ProfileId(profile), WoundCarrierCatalog.AfterlifeProfilesPath);
                _ = SpiritualTreatmentParticipation.RequireResidentParticipantScope(guardians!, residents,
                    context.Input.Carriers.AfterlifeProfiles!, owner);
                var row = residents!["entries"]!.AsArray().OfType<JsonObject>().Single(r => r["residentId"]?.GetValue<string>() == owner.OwnerId);
                var publicContext = string.Join(" · ", new[] { "Житель Обители", abodeName, Read(row["roleLabel"]), Read(row["summary"]) }.Where(v => v.Length > 0));
                nearby.Add(Create(owner, Name(profile, Read(row["displayName"]), "Житель Обители"), publicContext, locality));
            }
            catch (Exception failure) when (Malformed(failure)) { }
        }
        nearby = nearby.OrderBy(t => t.Owner.OwnerKind == "guardian" ? 0 : 1)
            .ThenBy(t => t.Name, StringComparer.OrdinalIgnoreCase).ToList();
        var ambiguous = nearby.GroupBy(t => (t.Name + "\n" + t.Context).Normalize(System.Text.NormalizationForm.FormKC), StringComparer.OrdinalIgnoreCase)
            .Where(g => g.Count() > 1).SelectMany(g => g).Select(t => t.Owner).ToHashSet();
        var repeatedNames = nearby.Append(targets[0]).GroupBy(t => t.Name.Normalize(System.Text.NormalizationForm.FormKC), StringComparer.OrdinalIgnoreCase)
            .Where(g => g.Count() > 1).Select(g => g.Key).ToHashSet(StringComparer.OrdinalIgnoreCase);
        foreach (var target in nearby)
            targets.Add(target with { Value = "target_" + targets.Count,
                Label = target.Name + (repeatedNames.Contains(target.Name.Normalize(System.Text.NormalizationForm.FormKC)) ? " — " + target.Context : ""),
                Available = !ambiguous.Contains(target.Owner), Reason = ambiguous.Contains(target.Owner)
                    ? "Сейчас не удаётся различить одноимённых обитателей по известным сведениям." : "" });
        return targets.AsReadOnly();

        WoundTargetChoice Create(WoundOwnerCoordinate owner, string name, string description, SpiritualAidLocality? currentLocality)
        {
            var summaries = wounds[owner].OrderBy(w => w.Origin.CreatedAtTurn).ThenBy(w => w.WoundId, StringComparer.Ordinal)
                .Select(w => new WoundTargetWoundSummary(Read(w.Display.Name), w.Severity.Value, Read(w.Display.Description),
                    w.WoundId, WoundIdentityState.ComputeSemanticFingerprint(w))).ToArray();
            var fingerprint = WoundAcceptedTurnFingerprintWriter.Compute(new[]
            {
                "wound_target_navigation", "1", WoundAcceptedTurnFingerprintWriter.CanonicalJson(SpiritualHealingRequestProjection.OwnerJson(owner)),
                currentLocality is null ? "" : WoundAcceptedTurnFingerprintWriter.CanonicalJson(SpiritualTreatmentParticipation.LocalityJson(currentLocality)), name, description
            }.Concat(summaries.SelectMany(w => new[] { w.WoundId, w.Fingerprint, w.Name, w.Severity, w.Description })));
            return new("target_0", name, description, Array.AsReadOnly(summaries), owner, currentLocality, fingerprint);
        }
    }

    internal static WoundTargetChoice RequireCurrent(WoundTargetChoice previous, IReadOnlyList<WoundTargetChoice> current) =>
        current.SingleOrDefault(t => t.Owner == previous.Owner && t.Available &&
            t.BindingFingerprint == previous.BindingFingerprint)
        ?? throw new InvalidDataException("Цель или её известные раны изменились; откройте /лечить заново.");

    private static string Name(JsonObject profile, string fallback, string generic) =>
        new[] { Read(profile["name"]), Read(profile["displayName"]), fallback, generic }.First(v => v.Length > 0);
    private static string Read(JsonNode? node) => node is JsonValue value && value.TryGetValue<string>(out var text) ? Read(text) : "";
    private static string Read(string? text) => string.Join(' ', WoundPlayerProjection.Text(text).Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
    private static bool Malformed(Exception failure) => failure is System.Text.Json.JsonException or InvalidDataException or InvalidOperationException or ArgumentException or OverflowException;
}
