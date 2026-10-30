using System.Text.Json.Nodes;

namespace BookOfEternityClient.Services;

/// <summary>Detached current public service DATA, never a retained access or publication grant.</summary>
internal sealed record ShiningFactionCurrencyService(AfterlifeHealingServiceProfile Profile,
    string Name, string FactionId, string HallId, string AdmissionFingerprint);

internal static partial class ShiningFactionHealingSupport
{
    /// <summary>Projects explicitly accessible currency services using one caller-owned parsed read set.</summary>
    /// <param name="shining">Current Shining state, including the exact current hall.</param>
    /// <param name="residentsRoot">Current complete resident roster; it is mandatory for this route.</param>
    /// <param name="profilesRoot">Current exact materialized actor profiles.</param>
    /// <returns>Detached quotes' provider DATA; confirmation and staging must read sources freshly.</returns>
    internal static IReadOnlyList<ShiningFactionCurrencyService> ProjectCurrentCurrencyServices(
        JsonObject shining, JsonObject residentsRoot, JsonObject profilesRoot)
    {
        var scope = ShiningCurrentHallContract.RequireCurrentScope(shining, residentsRoot);
        if (shining["factions"] is not JsonArray factions || residentsRoot["entries"] is not JsonArray residents ||
            profilesRoot["profiles"] is not JsonArray profiles)
            throw new InvalidDataException("Public faction service requires its actual faction, roster and profile roots.");
        var factionIndex = factions.OfType<JsonObject>().ToLookup(f => Text(f["factionId"]), StringComparer.Ordinal);
        var residentIndex = residents.OfType<JsonObject>().ToLookup(row => Text(row["residentId"]), StringComparer.Ordinal);
        var profileIndex = profiles.OfType<JsonObject>().ToLookup(row => (Text(row["actorType"]), Text(row["actorId"])));
        var result = new List<ShiningFactionCurrencyService>();
        foreach (var faction in factions.OfType<JsonObject>())
        {
            var factionId = Text(faction["factionId"]);
            if (!ResourceMaterializationContract.IsExactIdentifier(factionId) || factionIndex[factionId].Count() != 1 ||
                !SarefMainStoryState.IsPlayerVisibleShiningFaction(faction) || Text(faction["hallId"]) != scope.LocationId ||
                faction["factionLifecycle"] is not JsonObject lifecycle || Text(lifecycle["state"]) is not ("active" or "weakened" or "leaderless"))
                continue;
            foreach (var member in ProjectVisibleRoster(faction, residents, residentIndex, profileIndex))
            {
                if (member.HealingTier is null) continue;
                var resident = residentIndex[member.ResidentId].Single();
                var actor = profileIndex[("resident", member.ResidentId)].Single();
                var provider = new WoundOwnerCoordinate("shining_abode", "resident", member.ResidentId,
                    WoundCarrierCatalog.AfterlifeProfilesPath);
                if (AfterlifeHealingServiceContract.ReadPublicCurrencyProfile(provider, actor) is not { } profile ||
                    profile.LocationRef != scope.LocationId || !LocalInteractionScopeService.IsAfterlifeActorLocal(scope, actor))
                    continue;
                var admission = new JsonObject
                {
                    ["provider"] = SpiritualHealingRequestProjection.OwnerJson(provider), ["factionId"] = factionId,
                    ["hallId"] = scope.LocationId, ["lifecycle"] = lifecycle.DeepClone(),
                    ["factionVisibility"] = faction["visibility"]?.DeepClone(),
                    ["sarefVisibility"] = faction["sarefVisibility"]?.DeepClone(),
                    ["ascensionState"] = resident["ascensionState"]?.DeepClone(),
                    ["residentRole"] = resident["residentRole"]?.DeepClone(),
                    ["affiliation"] = resident["shiningFactionId"]?.DeepClone(),
                    ["profileVisibility"] = actor["visibility"]?.DeepClone(),
                    ["isPlayerVisible"] = actor["isPlayerVisible"]?.DeepClone(),
                    ["serviceFingerprint"] = profile.Fingerprint
                };
                result.Add(new(profile, member.Name, factionId, scope.LocationId,
                    AfterlifeHealingServiceContract.Fingerprint(admission, "faction_service_admission")));
            }
        }
        return result.AsReadOnly();
    }
}
