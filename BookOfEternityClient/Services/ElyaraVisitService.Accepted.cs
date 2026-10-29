namespace BookOfEternityClient.Services;

internal static partial class ElyaraVisitService
{
    // Callers supply authenticated original DATA, never GM current images in
    // place of it. Current arrival is checked separately against that original.
    internal static void RequireAccepted(ElyaraVisitIntent intent, ElyaraVisitInput original, ElyaraVisitInput current)
    {
        var before = Project(original, intent.Generation, DataFingerprint(original, intent.Generation));
        if (before.Kind != ElyaraVisitKind.ChaosTravel || before.State?.Intent != intent)
            throw new InvalidDataException("The visit does not match its authenticated original provider and reachable destination.");
        var previousActor = RequireCanonicalActor(original.Profiles);
        var actor = RequireCanonicalActor(current.Profiles);
        var provider = new WoundOwnerCoordinate("chaos_sea", "guardian", AfterlifeWoundTreatmentAuthority.ElyaraId,
            WoundCarrierCatalog.AfterlifeProfilesPath);
        var previousService = AfterlifeHealingServiceContract.ReadPublicCurrencyProfile(provider, previousActor);
        var service = AfterlifeHealingServiceContract.ReadPublicCurrencyProfile(provider, actor);
        if (previousService is null || service != previousService || T(actor["locationId"]) != intent.TargetAbodeId ||
            !AfterlifeEntityProfileState.TryNormalizeEffectRealm(T(current.Soul?["currentRealm"]), out var realm) || realm != "chaos_sea" ||
            current.Guardians is null)
            throw new InvalidDataException("Elyara's current life, place, service or the player's realm changed during the visit.");
        var arrival = RequirePlayerScope(realm, current.Guardians, current.Shining);
        if (arrival.LocationId != intent.TargetAbodeId || arrival.CurrentGuardianId != intent.TargetGuardianId)
            throw new InvalidDataException("The actual arrival does not match the original ordinary destination.");
    }
}
