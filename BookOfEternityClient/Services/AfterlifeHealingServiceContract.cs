using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace BookOfEternityClient.Services;

/// <summary>One operation's immutable, visible public service DATA; never a capability.</summary>
internal sealed record AfterlifeHealingServiceProfile(WoundOwnerCoordinate Provider,
    string LocationRef, int HealingTier, int MultiplierPercent, string Fingerprint);

/// <summary>Publicly declared display DATA, never proven player access or a quote.</summary>
internal sealed record AfterlifeHealingServiceDisplayTerms(bool DeclaredAvailable,
    int? MinimumRelationship, int MultiplierPercent, IReadOnlyList<string> CompensationKinds,
    IReadOnlyList<string> AccessConditions);

/// <summary>Immutable treatment terms. Physical generation belongs to the enclosing live owner.</summary>
internal sealed record AfterlifeHealingServiceQuote(WoundOwnerCoordinate Provider,
    WoundOwnerCoordinate Patient, string WoundId, string WoundFingerprint, int SeverityRank,
    string LocationRef, int HealingTier, string ServiceFingerprint, string CheckFingerprint,
    int MultiplierPercent, int Amount, string Fingerprint)
{
    internal JsonObject ToJson() => AfterlifeHealingServiceContract.QuoteJson(this);
}

/// <summary>Public healing service DATA; never permission to charge or heal.</summary>
internal static class AfterlifeHealingServiceContract
{
    /// <summary>Returns a canonical whole-Feather quote, or null for unsupported inputs.</summary>
    internal static int? CalculatePrice(int severityRank, int multiplierPercent)
    {
        if (severityRank is < 1 or > 4 || multiplierPercent is < 50 or > 200) return null;
        var price = severityRank switch { 1 => 25, 2 => 50, 3 => 100, _ => 200 };
        return (price * multiplierPercent + 99) / 100;
    }

    internal static AfterlifeHealingServiceProfile? ReadPublicCurrencyProfile(
        WoundOwnerCoordinate provider, JsonObject actor) => ReadCurrencyProfile(provider, actor, requireAvailable: true);

    // The caller already validated this roster profile's materialization. Keep
    // restricted declarations separate from the unchanged currency-admission path.
    internal static AfterlifeHealingServiceDisplayTerms? ReadPublicDisplayTerms(
        WoundOwnerCoordinate provider, JsonObject actor)
    {
        if (provider is null || actor is null || provider.OwnerKind is not ("guardian" or "resident") ||
            provider.CarrierPath != WoundCarrierCatalog.AfterlifeProfilesPath ||
            !WoundEffectCarrierAdapter.TryCreateTargetKey(provider, out _) ||
            !WoundCarrierCollectionAuthority.AfterlifeProfileMatches(actor, provider) ||
            !AfterlifeEntityProfileState.TryNormalizeEffectRealm(Text(actor["realm"]), out var realm) || realm != provider.Realm ||
            !AfterlifeProfileVisibility.IsVisibleToPlayer(actor) || Text(actor["lifeState"]) != "alive" ||
            Text(actor["locationId"]) is not { } location || !ResourceMaterializationContract.IsExactIdentifier(location) ||
            !AfterlifeEntityProfileState.TryReadRequiredWoundArtTier(actor,
                AfterlifeSpiritualConflictState.SpiritualHealingArtId, out _, out var tier) || tier < 1 ||
            actor["healingServiceProfile"] is not JsonObject service ||
            !Closed(service, "providerId", "realm", "locationRef", "visibility", "availability", "minimumRelationship",
                "priceMultiplierPercent", "compensationKinds", "accessConditions") ||
            Text(service["providerId"]) != provider.OwnerId || Text(service["realm"]) != provider.Realm ||
            Text(service["locationRef"]) != location || Text(service["visibility"]) != "public" ||
            Text(service["availability"]) is not ("available" or "unavailable") ||
            service["minimumRelationship"] is not null && Number(service["minimumRelationship"]) is null ||
            Number(service["priceMultiplierPercent"]) is not int percent || percent is < 50 or > 200 ||
            service["compensationKinds"] is not JsonArray kinds || kinds.Count is < 1 or > 6 ||
            kinds.Any(k => Text(k) is not ("ink_feathers" or "favor" or "debt" or "quest" or "allegiance" or "free_aid")) ||
            kinds.Select(Text).Distinct(StringComparer.Ordinal).Count() != kinds.Count ||
            service["accessConditions"] is not JsonArray conditions ||
            conditions.Any(c => string.IsNullOrWhiteSpace(Text(c))))
            return null;

        return new(Text(service["availability"]) == "available", Number(service["minimumRelationship"]), percent,
            Array.AsReadOnly(kinds.Select(k => Text(k)!).ToArray()),
            Array.AsReadOnly(conditions.Select(c => Text(c)!).ToArray()));
    }

    private static AfterlifeHealingServiceProfile? ReadCurrencyProfile(
        WoundOwnerCoordinate provider, JsonObject actor, bool requireAvailable)
    {
        if (provider is null || actor is null || provider.OwnerKind is not ("guardian" or "resident") ||
            provider.CarrierPath != WoundCarrierCatalog.AfterlifeProfilesPath ||
            !WoundEffectCarrierAdapter.TryCreateTargetKey(provider, out _) ||
            !WoundCarrierCollectionAuthority.AfterlifeProfileMatches(actor, provider) ||
            !AfterlifeEntityProfileState.TryNormalizeEffectRealm(Text(actor["realm"]), out var actorRealm) || actorRealm != provider.Realm ||
            requireAvailable && !AfterlifeProfileVisibility.IsVisibleToPlayer(actor) ||
            (requireAvailable ? Text(actor["lifeState"]) != "alive" : Text(actor["lifeState"]) is not ("alive" or "dead")) ||
            Text(actor["locationId"]) is not { } location || !ResourceMaterializationContract.IsExactIdentifier(location) ||
            actor["standardArts"] is not JsonObject arts || Number(arts["spiritual_healing"]) is not int tier || tier is < 1 or > 5 ||
            actor["healingServiceProfile"] is not JsonObject service ||
            !Closed(service, "providerId", "realm", "locationRef", "visibility", "availability", "minimumRelationship",
                "priceMultiplierPercent", "compensationKinds", "accessConditions") ||
            Text(service["providerId"]) != provider.OwnerId || Text(service["realm"]) != provider.Realm ||
            Text(service["locationRef"]) != location || Text(service["visibility"]) != "public" ||
            (requireAvailable ? Text(service["availability"]) != "available" : Text(service["availability"]) is not ("available" or "unavailable")) || service["minimumRelationship"] is not null ||
            service["accessConditions"] is not JsonArray { Count: 0 } ||
            Number(service["priceMultiplierPercent"]) is not int percent || percent is < 50 or > 200 ||
            service["compensationKinds"] is not JsonArray kinds || kinds.Count is < 1 or > 6 ||
            kinds.Any(k => Text(k) is not ("ink_feathers" or "favor" or "debt" or "quest" or "allegiance" or "free_aid")) ||
            kinds.Select(Text).Distinct(StringComparer.Ordinal).Count() != kinds.Count ||
            !kinds.Any(k => Text(k) == "ink_feathers") ||
            provider.OwnerId == "guard_system_elyara_001" && (tier != 5 || percent != 100))
            return null;
        var permission = new JsonObject
        {
            ["provider"] = SpiritualHealingRequestProjection.OwnerJson(provider), ["lifeState"] = Text(actor["lifeState"]),
            ["locationId"] = location, ["healingTier"] = tier, ["visible"] = true, ["service"] = service.DeepClone()
        };
        return new(provider, location, tier, percent, Fingerprint(permission, "public_profile"));
    }

    internal static bool IsElyaraProfile(JsonObject actor) =>
        string.Equals(Text(actor["actorId"]), AfterlifeWoundTreatmentAuthority.ElyaraId, StringComparison.OrdinalIgnoreCase) ||
        string.Equals(Text(actor["actorRef"]), AfterlifeWoundTreatmentAuthority.ElyaraId, StringComparison.OrdinalIgnoreCase);

    internal static void RequireElyaraInvariant(JsonObject actor)
    {
        if (!IsElyaraProfile(actor)) return;
        if (Text(actor["actorId"]) is { } id && id != AfterlifeWoundTreatmentAuthority.ElyaraId ||
            Text(actor["actorRef"]) is { } reference && reference != AfterlifeWoundTreatmentAuthority.ElyaraId ||
            !AfterlifeEntityProfileState.TryNormalizeEffectRealm(Text(actor["realm"]), out var realm) ||
            ReadCurrencyProfile(new(realm, "guardian", AfterlifeWoundTreatmentAuthority.ElyaraId,
                WoundCarrierCatalog.AfterlifeProfilesPath), actor, requireAvailable: false) is null)
            throw new InvalidDataException("Canonical Elyara requires exact identity, lifeState alive/dead, current service locality, HealingV and public100% terms.");
    }

    internal static void RequireElyaraTransition(JsonObject previous, JsonObject next)
    {
        if (!IsElyaraProfile(previous) && !IsElyaraProfile(next)) return;
        RequireElyaraInvariant(next);
        if (Text(previous["lifeState"]) == "dead" && Text(next["lifeState"]) != "dead")
            throw new InvalidDataException("An accepted dead Elyara cannot be resurrected by profile upsert.");
    }

    internal static AfterlifeHealingServiceQuote? CreateQuote(AfterlifeHealingServiceProfile profile,
        WoundMaterializationEnvelope wound, SpiritualHealingPreparedSource check, string currentLocation)
    {
        if (profile is null || profile.Provider is null || wound is null || check is null || wound.Owner is null ||
            wound.Classification?.Domain != "spiritual" || wound.Lifecycle != "active" ||
            !IsSupportedQuoteRealm(wound.Owner.Realm, profile.Provider) || wound.Owner.OwnerKind != "player_soul" || wound.Owner.OwnerId != "player_soul" ||
            wound.Owner.CarrierPath != WoundCarrierCatalog.AfterlifeProfilesPath ||
            wound.Display?.Visibility is not ("public" or "known_to_player") ||
            !WoundEffectCarrierAdapter.TryCreateTargetKey(wound.Owner, out _) ||
            !ResourceMaterializationContract.IsExactIdentifier(wound.WoundId) ||
            profile.Provider.OwnerKind is not ("guardian" or "resident") ||
            profile.Provider.CarrierPath != WoundCarrierCatalog.AfterlifeProfilesPath ||
            !WoundEffectCarrierAdapter.TryCreateTargetKey(profile.Provider, out _) ||
            profile.Provider.Realm != wound.Owner.Realm || profile.LocationRef != currentLocation ||
            !ResourceMaterializationContract.IsExactIdentifier(currentLocation) || profile.HealingTier is < 1 or > 5 ||
            profile.Provider.OwnerId == "guard_system_elyara_001" && (profile.HealingTier != 5 || profile.MultiplierPercent != 100) ||
            !ResourceMaterializationContract.IsAuthorityFingerprint(profile.Fingerprint) ||
            check.Target != wound.Owner || check.Healer != profile.Provider || check.WoundId != wound.WoundId ||
            check.WoundSeverityRank != wound.Severity?.Rank || check.HealingTier != profile.HealingTier ||
            profile.HealingTier < check.WoundSeverityRank || check.RollMode is not ("normal" or "advantage" or "disadvantage") ||
            check.ComplicationModifier is < 0 or > 64 || check.RollContributions is null ||
            CalculatePrice(check.WoundSeverityRank, profile.MultiplierPercent) is not int amount)
            return null;
        var quote = new AfterlifeHealingServiceQuote(profile.Provider, wound.Owner, wound.WoundId,
            WoundIdentityState.ComputeSemanticFingerprint(wound), check.WoundSeverityRank, currentLocation,
            profile.HealingTier, profile.Fingerprint, HealingCheckFingerprint(check), profile.MultiplierPercent, amount, "");
        return quote with { Fingerprint = QuoteFingerprint(quote) };
    }

    internal static string HealingCheckFingerprint(SpiritualHealingPreparedSource source) =>
        WoundAcceptedTurnFingerprintWriter.Compute(new[]
        {
            "book_of_eternity.public_healing_check", "1", source.WoundId,
            WoundAcceptedTurnFingerprintWriter.CanonicalJson(SpiritualHealingRequestProjection.OwnerJson(source.Target)),
            WoundAcceptedTurnFingerprintWriter.CanonicalJson(SpiritualHealingRequestProjection.OwnerJson(source.Healer)),
            source.WoundSeverityRank.ToString(CultureInfo.InvariantCulture), source.HealingTier.ToString(CultureInfo.InvariantCulture),
            source.ComplicationModifier.ToString(CultureInfo.InvariantCulture), source.RollMode,
            WoundAcceptedTurnFingerprintWriter.CanonicalJson(JsonSerializer.SerializeToNode(source.RollContributions)!)
        });

    internal static AfterlifeHealingServiceQuote ParseQuote(JsonNode? node)
    {
        if (node is not JsonObject root || !Closed(root, "schemaVersion", "provider", "patient", "woundId", "woundFingerprint",
                "severityRank", "locationRef", "healingTier", "serviceFingerprint", "checkFingerprint", "multiplierPercent",
                "compensationKind", "amount", "quoteFingerprint") || Number(root["schemaVersion"]) != 1 ||
            Text(root["compensationKind"]) != "ink_feathers")
            throw new InvalidDataException("Invalid closed public healing quote.");
        var provider = ReadServiceOwner(root["provider"]);
        var patient = ReadServiceOwner(root["patient"]);
        var quote = new AfterlifeHealingServiceQuote(provider, patient, Text(root["woundId"]) ?? "",
            Text(root["woundFingerprint"]) ?? "", Number(root["severityRank"]) ?? 0, Text(root["locationRef"]) ?? "",
            Number(root["healingTier"]) ?? 0, Text(root["serviceFingerprint"]) ?? "", Text(root["checkFingerprint"]) ?? "",
            Number(root["multiplierPercent"]) ?? 0, Number(root["amount"]) ?? 0, Text(root["quoteFingerprint"]) ?? "");
        if (!IsSupportedQuoteRealm(patient.Realm, provider) || provider.OwnerKind is not ("guardian" or "resident") ||
            patient != new WoundOwnerCoordinate(provider.Realm, "player_soul", "player_soul", WoundCarrierCatalog.AfterlifeProfilesPath) ||
            !ResourceMaterializationContract.IsExactIdentifier(quote.WoundId) ||
            !ResourceMaterializationContract.IsExactIdentifier(quote.LocationRef) ||
            !ResourceMaterializationContract.IsAuthorityFingerprint(quote.WoundFingerprint) ||
            !ResourceMaterializationContract.IsAuthorityFingerprint(quote.ServiceFingerprint) ||
            !ResourceMaterializationContract.IsAuthorityFingerprint(quote.CheckFingerprint) ||
            quote.HealingTier is < 1 or > 5 || quote.HealingTier < quote.SeverityRank ||
            CalculatePrice(quote.SeverityRank, quote.MultiplierPercent) != quote.Amount ||
            provider.OwnerId == "guard_system_elyara_001" && (quote.HealingTier != 5 || quote.MultiplierPercent != 100) ||
            quote.Fingerprint != QuoteFingerprint(quote))
            throw new InvalidDataException("Public healing quote terms or fingerprint differ.");
        return quote;
    }

    // Service coordinates are not Chaos participation records. Keep that
    // separate contract unchanged; the quote below still restricts Shining to
    // the exact canonical Elyara and its own player patient.
    internal static WoundOwnerCoordinate ReadServiceOwner(JsonNode? node)
    {
        if (node is not JsonObject root || !Closed(root, "realm", "ownerKind", "ownerId", "carrierPath"))
            throw new InvalidDataException("Service requires a closed typed owner coordinate.");
        var owner = new WoundOwnerCoordinate(Text(root["realm"]) ?? "", Text(root["ownerKind"]) ?? "",
            Text(root["ownerId"]) ?? "", Text(root["carrierPath"]) ?? "");
        if (owner.Realm is not ("chaos_sea" or "shining_abode") ||
            owner.OwnerKind is not ("player_soul" or "guardian" or "resident") ||
            owner.Realm == "shining_abode" && owner.OwnerKind == "resident" ||
            !ResourceMaterializationContract.IsExactIdentifier(owner.OwnerId) ||
            owner.CarrierPath != WoundCarrierCatalog.AfterlifeProfilesPath ||
            !WoundEffectCarrierAdapter.TryCreateTargetKey(owner, out _))
            throw new InvalidDataException("Service owner coordinate is outside its exact afterlife profile family.");
        return owner;
    }

    // Existing Chaos quote DATA remains supported; Shining execution in this
    // slice is restricted to the same canonical Elyara, never arbitrary clinics.
    private static bool IsSupportedQuoteRealm(string realm, WoundOwnerCoordinate provider) =>
        provider.Realm == realm && (realm == "chaos_sea" || realm == "shining_abode" &&
            provider.OwnerKind == "guardian" && provider.OwnerId == AfterlifeWoundTreatmentAuthority.ElyaraId);

    internal static JsonObject QuoteJson(AfterlifeHealingServiceQuote quote) => new()
    {
        ["schemaVersion"] = 1, ["provider"] = SpiritualHealingRequestProjection.OwnerJson(quote.Provider),
        ["patient"] = SpiritualHealingRequestProjection.OwnerJson(quote.Patient), ["woundId"] = quote.WoundId,
        ["woundFingerprint"] = quote.WoundFingerprint, ["severityRank"] = quote.SeverityRank,
        ["locationRef"] = quote.LocationRef, ["healingTier"] = quote.HealingTier,
        ["serviceFingerprint"] = quote.ServiceFingerprint, ["checkFingerprint"] = quote.CheckFingerprint,
        ["multiplierPercent"] = quote.MultiplierPercent, ["compensationKind"] = "ink_feathers",
        ["amount"] = quote.Amount, ["quoteFingerprint"] = quote.Fingerprint
    };

    private static string QuoteFingerprint(AfterlifeHealingServiceQuote quote)
    {
        var root = QuoteJson(quote); root.Remove("quoteFingerprint");
        return Fingerprint(root, "quote");
    }
    internal static string Fingerprint(JsonObject root, string kind) => WoundAcceptedTurnFingerprintWriter.Compute(
        new[] { "book_of_eternity.public_healing_service", "1", kind, WoundAcceptedTurnFingerprintWriter.CanonicalJson(root) });
    internal static bool Closed(JsonObject root, params string[] fields) => root.Count == fields.Length && fields.All(root.ContainsKey);
    internal static string? Text(JsonNode? node) => node is JsonValue value && value.TryGetValue<string>(out var text) ? text : null;
    internal static int? Number(JsonNode? node) => node is JsonValue value && value.TryGetValue<int>(out var number) ? number : null;
}
