using System.Text.Json.Nodes;

namespace BookOfEternityClient.Services;

internal enum ShiningFactionNormalizationMode
{
    LegacyCompatibility,
    AuthoredMaterialization
}

internal static partial class ShiningAbodeState
{
    public enum PreparedIncarnationPackageMode
    {
        Absent,
        ValidHandoff,
        InvalidFault
    }

    public const string StatePath = "game_state/meta/shining_abode_state.json";

    public const string AvailabilityActive = "active";
    public const string AvailabilitySealedUntilNextAscension = "sealed_until_next_ascension";

    public const string LeadershipStateSecure = "secure";
    public const string LeadershipStateContested = "contested";
    public const string LeadershipStateVacant = "vacant";

    public const string FactionLifecycleStateActive = "active";
    public const string FactionLifecycleStateWeakened = "weakened";
    public const string FactionLifecycleStateLeaderless = "leaderless";
    public const string FactionLifecycleStateBroken = "broken";
    public const string FactionLifecycleStateDissolved = "dissolved";

    public const string FactionConflictCampaignsProperty = "factionConflictCampaigns";
    public const string FactionChronicleProperty = "chronicle";
    public const string FactionInfluenceProperty = "territorialInfluence";
    public const string FactionStrategicMemoryProperty = "strategicMemory";
    public const string FactionResourceLedgerProperty = "resourceLedger";
    public const string FactionChronicleUpdatesProperty = "shiningFactionChronicleUpdates";
    public const string FactionInfluenceUpdatesProperty = "shiningFactionInfluenceUpdates";
    public const string FactionStrategicMemoryUpdatesProperty = "shiningFactionStrategicMemoryUpdates";
    public const string FactionResourceLedgerUpdatesProperty = "shiningFactionResourceLedgerUpdates";
    public const string LastInvalidFactionPoliticalCommandProperty = "lastInvalidShiningFactionPoliticalCommand";
    public const string LastInvalidFactionPoliticalCommandReasonProperty = "lastInvalidShiningFactionPoliticalCommandReason";

    public const string FactionCampaignGoalWeaken = "weaken";
    public const string FactionCampaignGoalExpose = "expose";
    public const string FactionCampaignGoalDeposeLeader = "depose_leader";
    public const string FactionCampaignGoalBreak = "break";
    public const string FactionCampaignGoalDissolve = "dissolve";

    public const string FactionCampaignStatusActive = "active";
    public const string FactionCampaignStatusBreakthroughReady = "breakthrough_ready";
    public const string FactionCampaignStatusCompleted = "completed";
    public const string FactionCampaignStatusFailed = "failed";
    public const string FactionCampaignStatusAbandoned = "abandoned";

    public const string FactionCampaignBreakthroughExposure = "exposure";
    public const string FactionCampaignBreakthroughDuelVictory = "duel_victory";
    public const string FactionCampaignBreakthroughDefection = "defection";
    public const string FactionCampaignBreakthroughSabotage = "sabotage";
    public const string FactionCampaignBreakthroughResourceDisruption = "resource_disruption";
    public const string FactionCampaignBreakthroughOathBreak = "oath_break";
    public const string FactionCampaignBreakthroughTrial = "trial";
    public const string FactionCampaignBreakthroughSarefDirective = "saref_directive";

    public const string HeadActorTypeGuardian = "guardian";
    public const string HeadActorTypePlayerSoul = "player_soul";
    public const string HeadActorTypeResident = "resident";
    public const string HeadActorTypeRadiantActor = "radiant_actor";

    public const string OriginTypeAscendedGuardian = "ascended_guardian";
    public const string OriginTypeNativeRadiant = "native_radiant";
    public const string OriginTypePlayerFounded = "player_founded";

    public const string PoliticalStatusHead = "head";
    public const string PoliticalStatusFormerHead = "former_head";
    public const string PoliticalStatusClaimant = "claimant";
    public const string PoliticalStatusElder = "elder";
    public const string PoliticalStatusRetired = "retired";

    public const string AscensionStateAscended = "ascended";
    public const string AscensionStateRemainedInChaosSea = "remained_in_chaos_sea";

    public const string FactionRealignmentStateSettled = "settled";
    public const string FactionRealignmentStateWavering = "wavering";
    public const string FactionRealignmentStateRestless = "restless";
    public const string FactionRealignmentStateConsideringRealignment = "considering_realignment";
    public const string FactionRealignmentStateReadyToRealign = "ready_to_realign";

    public const string FactionLoyaltyTierAlienated = "alienated";
    public const string FactionLoyaltyTierUncertain = "uncertain";
    public const string FactionLoyaltyTierAttached = "attached";
    public const string FactionLoyaltyTierDevoted = "devoted";
    public const string FactionLoyaltyTierSteadfast = "steadfast";

    public const string ResidentRoleArchiveSupport = "archive_support";
    public const string ResidentRoleForgeSupport = "forge_support";
    public const string ResidentRoleSocialSupport = "social_support";
    public const string ResidentRoleResourceSupport = "resource_support";
    public const string ResidentRoleDescentSupport = "descent_support";

    public const string HallServiceTagSocial = "social";
    public const string HallServiceTagLore = "lore";
    public const string HallServiceTagResource = "resource";
    public const string HallServiceTagMemory = "memory";
    public const string HallServiceTagDescent = "descent";
    public const string HallServiceTagRelic = "relic";

    public const string ProjectStatusActive = "active";
    public const string ProjectStatusCompleted = "completed";
    public const string ProjectStatusRetired = "retired";

    public const string ProjectArchetypeRevelation = "revelation";
    public const string ProjectArchetypeAccord = "accord";
    public const string ProjectArchetypeProvision = "provision";
    public const string ProjectArchetypeRemembrance = "remembrance";
    public const string ProjectArchetypeRefinement = "refinement";
    public const string ProjectArchetypePassage = "passage";
    public const string ProjectArchetypeWarding = "warding";
    public const string ProjectArchetypeSubversion = "subversion";

    public const string EffectFamilyLore = "lore";
    public const string EffectFamilySocial = "social";
    public const string EffectFamilyResource = "resource";
    public const string EffectFamilyMemory = "memory";
    public const string EffectFamilyDescent = "descent";
    public const string EffectFamilySurvival = "survival";
    public const string EffectFamilyRelic = "relic";
    public const string EffectFamilyRoute = "route";

    public const string CardSourceTypeHead = "head";
    public const string CardSourceTypeProject = "project";
    public const string CardSourceTypeResidentDescent = "resident_descent";

    public const string RarityCommon = "common";
    public const string RarityUncommon = "uncommon";
    public const string RarityRare = "rare";
    public const string RarityEpic = "epic";
    public const string RarityLegendary = "legendary";
    public const string RarityRadiant = "radiant";

    private static readonly HashSet<string> AllowedAvailabilityValues = new(StringComparer.OrdinalIgnoreCase)
    {
        AvailabilityActive,
        AvailabilitySealedUntilNextAscension
    };

    private static readonly HashSet<string> AllowedLeadershipStates = new(StringComparer.OrdinalIgnoreCase)
    {
        LeadershipStateSecure,
        LeadershipStateContested,
        LeadershipStateVacant
    };

    private static readonly HashSet<string> AllowedFactionLifecycleStates = new(StringComparer.OrdinalIgnoreCase)
    {
        FactionLifecycleStateActive,
        FactionLifecycleStateWeakened,
        FactionLifecycleStateLeaderless,
        FactionLifecycleStateBroken,
        FactionLifecycleStateDissolved
    };

    private static readonly HashSet<string> AllowedFactionCampaignGoals = new(StringComparer.OrdinalIgnoreCase)
    {
        FactionCampaignGoalWeaken,
        FactionCampaignGoalExpose,
        FactionCampaignGoalDeposeLeader,
        FactionCampaignGoalBreak,
        FactionCampaignGoalDissolve
    };

    private static readonly HashSet<string> AllowedFactionCampaignStatuses = new(StringComparer.OrdinalIgnoreCase)
    {
        FactionCampaignStatusActive,
        FactionCampaignStatusBreakthroughReady,
        FactionCampaignStatusCompleted,
        FactionCampaignStatusFailed,
        FactionCampaignStatusAbandoned
    };

    private static readonly HashSet<string> AllowedFactionCampaignBreakthroughTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        FactionCampaignBreakthroughExposure,
        FactionCampaignBreakthroughDuelVictory,
        FactionCampaignBreakthroughDefection,
        FactionCampaignBreakthroughSabotage,
        FactionCampaignBreakthroughResourceDisruption,
        FactionCampaignBreakthroughOathBreak,
        FactionCampaignBreakthroughTrial,
        FactionCampaignBreakthroughSarefDirective
    };

    private static readonly HashSet<string> AllowedHeadActorTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        HeadActorTypeGuardian,
        HeadActorTypePlayerSoul,
        HeadActorTypeResident,
        HeadActorTypeRadiantActor
    };

    private static readonly HashSet<string> AllowedOriginTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        OriginTypeAscendedGuardian,
        OriginTypeNativeRadiant,
        OriginTypePlayerFounded
    };

    private static readonly HashSet<string> AllowedPoliticalStatuses = new(StringComparer.OrdinalIgnoreCase)
    {
        PoliticalStatusHead,
        PoliticalStatusFormerHead,
        PoliticalStatusClaimant,
        PoliticalStatusElder,
        PoliticalStatusRetired
    };

    private static readonly HashSet<string> AllowedResidentRoles = new(StringComparer.OrdinalIgnoreCase)
    {
        ResidentRoleArchiveSupport,
        ResidentRoleForgeSupport,
        ResidentRoleSocialSupport,
        ResidentRoleResourceSupport,
        ResidentRoleDescentSupport
    };

    private static readonly HashSet<string> AllowedHallServiceTags = new(StringComparer.OrdinalIgnoreCase)
    {
        HallServiceTagSocial,
        HallServiceTagLore,
        HallServiceTagResource,
        HallServiceTagMemory,
        HallServiceTagDescent,
        HallServiceTagRelic
    };

    private static readonly HashSet<string> AllowedProjectStatuses = new(StringComparer.OrdinalIgnoreCase)
    {
        ProjectStatusActive,
        ProjectStatusCompleted,
        ProjectStatusRetired
    };

    private static readonly HashSet<string> AllowedProjectArchetypes = new(StringComparer.OrdinalIgnoreCase)
    {
        ProjectArchetypeRevelation,
        ProjectArchetypeAccord,
        ProjectArchetypeProvision,
        ProjectArchetypeRemembrance,
        ProjectArchetypeRefinement,
        ProjectArchetypePassage,
        ProjectArchetypeWarding,
        ProjectArchetypeSubversion
    };

    private static readonly HashSet<string> AllowedEffectFamilies = new(StringComparer.OrdinalIgnoreCase)
    {
        EffectFamilyLore,
        EffectFamilySocial,
        EffectFamilyResource,
        EffectFamilyMemory,
        EffectFamilyDescent,
        EffectFamilySurvival,
        EffectFamilyRelic,
        EffectFamilyRoute
    };

    private static readonly HashSet<string> AllowedCardSourceTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        CardSourceTypeHead,
        CardSourceTypeProject,
        CardSourceTypeResidentDescent
    };

    private static readonly HashSet<string> AllowedRarities = new(StringComparer.OrdinalIgnoreCase)
    {
        RarityCommon,
        RarityUncommon,
        RarityRare,
        RarityEpic,
        RarityLegendary,
        RarityRadiant
    };

    private static readonly HashSet<string> AllowedFactionLoyaltyTiers = new(StringComparer.OrdinalIgnoreCase)
    {
        FactionLoyaltyTierAlienated,
        FactionLoyaltyTierUncertain,
        FactionLoyaltyTierAttached,
        FactionLoyaltyTierDevoted,
        FactionLoyaltyTierSteadfast
    };

    private static readonly HashSet<string> AllowedFactionRealignmentStates = new(StringComparer.OrdinalIgnoreCase)
    {
        FactionRealignmentStateSettled,
        FactionRealignmentStateWavering,
        FactionRealignmentStateRestless,
        FactionRealignmentStateConsideringRealignment,
        FactionRealignmentStateReadyToRealign
    };

    public static bool IsSupportedAvailability(string? value) => !string.IsNullOrWhiteSpace(value) && AllowedAvailabilityValues.Contains(value);
    public static bool IsSupportedLeadershipState(string? value) => !string.IsNullOrWhiteSpace(value) && AllowedLeadershipStates.Contains(value);
    public static bool IsSupportedFactionLifecycleState(string? value) => !string.IsNullOrWhiteSpace(value) && AllowedFactionLifecycleStates.Contains(value);
    public static bool IsSupportedFactionCampaignGoal(string? value) => !string.IsNullOrWhiteSpace(value) && AllowedFactionCampaignGoals.Contains(value);
    public static bool IsSupportedFactionCampaignStatus(string? value) => !string.IsNullOrWhiteSpace(value) && AllowedFactionCampaignStatuses.Contains(value);
    public static bool IsSupportedFactionCampaignBreakthroughType(string? value) => !string.IsNullOrWhiteSpace(value) && AllowedFactionCampaignBreakthroughTypes.Contains(value);
    public static bool IsSupportedHeadActorType(string? value) => !string.IsNullOrWhiteSpace(value) && AllowedHeadActorTypes.Contains(value);
    public static bool IsSupportedOriginType(string? value) => !string.IsNullOrWhiteSpace(value) && AllowedOriginTypes.Contains(value);
    public static bool IsSupportedPoliticalStatus(string? value) => !string.IsNullOrWhiteSpace(value) && AllowedPoliticalStatuses.Contains(value);
    public static bool IsSupportedResidentRole(string? value) => !string.IsNullOrWhiteSpace(value) && AllowedResidentRoles.Contains(value);
    public static bool IsSupportedHallServiceTag(string? value) => !string.IsNullOrWhiteSpace(value) && AllowedHallServiceTags.Contains(value);
    public static bool IsSupportedProjectStatus(string? value) => !string.IsNullOrWhiteSpace(value) && AllowedProjectStatuses.Contains(value);
    public static bool IsSupportedProjectArchetype(string? value) => !string.IsNullOrWhiteSpace(value) && AllowedProjectArchetypes.Contains(value);
    public static bool IsSupportedEffectFamily(string? value) => !string.IsNullOrWhiteSpace(value) && AllowedEffectFamilies.Contains(value);
    public static bool IsSupportedCardSourceType(string? value) => !string.IsNullOrWhiteSpace(value) && AllowedCardSourceTypes.Contains(value);
    public static bool IsSupportedRarity(string? value) => !string.IsNullOrWhiteSpace(value) && AllowedRarities.Contains(value);
    public static bool IsSupportedFactionLoyaltyTier(string? value) => !string.IsNullOrWhiteSpace(value) && AllowedFactionLoyaltyTiers.Contains(value);
    public static bool IsSupportedFactionRealignmentState(string? value) => !string.IsNullOrWhiteSpace(value) && AllowedFactionRealignmentStates.Contains(value);

    public static PreparedIncarnationPackageMode GetPreparedIncarnationPackageMode(JsonObject? root)
    {
        if (root == null ||
            !root.ContainsKey("preparedIncarnationPackage") ||
            root["preparedIncarnationPackage"] == null)
        {
            return PreparedIncarnationPackageMode.Absent;
        }

        if (root["preparedIncarnationPackage"] is not JsonObject preparedPackage)
            return PreparedIncarnationPackageMode.InvalidFault;

        return string.IsNullOrWhiteSpace(ValidatePreparedIncarnationPackageForBootstrap(preparedPackage))
            ? PreparedIncarnationPackageMode.ValidHandoff
            : PreparedIncarnationPackageMode.InvalidFault;
    }

    public static string? ValidateRawOwnerStateForActionableMode(JsonObject root)
    {
        var availability = GetNodeString(root["availability"]);
        if (!IsSupportedAvailability(availability))
            return "shining_abode_state.json использует неподдерживаемое availability и не может authorise actionable Shining mode.";
        if (root.ContainsKey("preparedIncarnationPackage") &&
            root["preparedIncarnationPackage"] != null &&
            root["preparedIncarnationPackage"] is not JsonObject)
        {
            return "preparedIncarnationPackage повреждён и не позволяет надёжно определить lifecycle handoff.";
        }

        var pendingDiscoveryIssue = ValidateLegacyPendingNativeFactionDiscoveryShape(root);
        if (!string.IsNullOrWhiteSpace(pendingDiscoveryIssue))
            return pendingDiscoveryIssue;

        var treasuryIssue = ValidateTreasuryShape(root);
        if (!string.IsNullOrWhiteSpace(treasuryIssue))
            return treasuryIssue;

        if (root["radiance"] is not JsonObject)
            return "radiance object повреждён или отсутствует.";
        if (root["gates"] is not JsonObject)
            return "gates object повреждён или отсутствует.";
        if (root.ContainsKey("gachaSystem") && root["gachaSystem"] is not JsonObject)
            return "gachaSystem object повреждён.";
        if (root.ContainsKey(SourceOfLightCapstoneState.ShiningStateProperty) &&
            root[SourceOfLightCapstoneState.ShiningStateProperty] is not null &&
            root[SourceOfLightCapstoneState.ShiningStateProperty] is not JsonObject)
            return "sourceOfLightCapstone повреждён и не позволяет надёжно определить capstone reward state.";

        var blessingCardIssue = ValidateRawBlessingCardContracts(root);
        if (!string.IsNullOrWhiteSpace(blessingCardIssue))
            return blessingCardIssue;

        var politicalIssue = ValidateRawPoliticalContracts(root);
        if (!string.IsNullOrWhiteSpace(politicalIssue))
            return politicalIssue;

        return null;
    }

    public static string? ValidateLegacyPendingNativeFactionDiscoveryShape(JsonObject root)
    {
        if (root.ContainsKey("pendingNativeFactionDiscovery") &&
            root["pendingNativeFactionDiscovery"] != null &&
            root["pendingNativeFactionDiscovery"] is not JsonObject)
        {
            return "pendingNativeFactionDiscovery повреждён; legacy discovery contract должен быть repaired или closed перед actionable Shining mode.";
        }

        return null;
    }

    public static JsonObject CreateDefaultState()
    {
        return new JsonObject
        {
            ["availability"] = AvailabilityActive,
            ["radiance"] = new JsonObject
            {
                ["experience"] = 0,
                ["tier"] = 0
            },
            ["lightSparks"] = 100,
            ["halls"] = new JsonArray(),
            ["factions"] = new JsonArray(),
            ["shiningPoliticalActors"] = new JsonArray(),
            ["pendingNativeFactionDiscovery"] = null,
            ["gates"] = BuildDefaultGatesObject(),
            ["preparedIncarnationPackage"] = null,
            ["gachaSystem"] = BuildDefaultGachaSystemObject(),
            ["treasury"] = BuildDefaultTreasuryObject(),
            [SourceOfLightCapstoneState.ShiningStateProperty] = null,
            ["coreActionReceipts"] = new JsonArray(),
            ["factionFoundingReceipts"] = new JsonArray(),
            ["factionRealignmentReceipts"] = new JsonArray()
        };
    }

    public static JsonObject BuildDefaultGatesObject()
    {
        return new JsonObject
        {
            ["draftVersion"] = 0,
            ["hasOpenDraft"] = false,
            ["isStale"] = false,
            ["allCandidateBlessingCards"] = new JsonArray(),
            ["availableBlessingCards"] = new JsonArray(),
            ["shownBlessingCardIds"] = new JsonArray(),
            ["selectedBlessingCardIds"] = new JsonArray(),
            ["nextCandidateCursor"] = 0,
            ["rerollsRemaining"] = 0
        };
    }

    public static JsonObject BuildDefaultGachaSystemObject()
    {
        return new JsonObject
        {
            ["chargesPerReturn"] = 1,
            ["chargesUsedThisReturn"] = 0,
            ["currentReturnCycleId"] = string.Empty,
            ["gachaHistory"] = new JsonArray()
        };
    }

    public static JsonObject ActivateForAscension(JsonObject? existingRoot, JsonObject? residentRoot)
    {
        var root = CloneObject(existingRoot ?? CreateDefaultState());
        NormalizeStateRoot(root, residentRoot);
        root["availability"] = AvailabilityActive;
        root["lightSparks"] = 100;
        root["pendingNativeFactionDiscovery"] = null;
        root["gates"] = BuildDefaultGatesObject();
        root["preparedIncarnationPackage"] = null;

        foreach (var faction in EnsureFactionsArray(root).OfType<JsonObject>())
        {
            faction["investCountThisAscension"] = 0;
            faction["projectArchetypesCountedThisAscension"] = new JsonArray();
        }

        NormalizeStateRoot(root, residentRoot);
        return root;
    }

    public static void SealForChaosSeaReturn(JsonObject root)
    {
        NormalizeStateRoot(root, residentRoot: null);
        root["availability"] = AvailabilitySealedUntilNextAscension;
        root["pendingNativeFactionDiscovery"] = null;
        root["gates"] = BuildDefaultGatesObject();
        root["preparedIncarnationPackage"] = null;
    }

    public static JsonArray EnsureHallsArray(JsonObject root) => EnsureArray(root, "halls");
    public static JsonArray EnsureFactionsArray(JsonObject root) => EnsureArray(root, "factions");
    public static JsonArray EnsurePoliticalActorsArray(JsonObject root) => EnsureArray(root, "shiningPoliticalActors");
    public static JsonArray EnsureFactionConflictCampaignsArray(JsonObject root) => EnsureArray(root, FactionConflictCampaignsProperty);
    public static JsonArray EnsureCoreActionReceiptsArray(JsonObject root) => EnsureArray(root, "coreActionReceipts");
    public static JsonArray EnsureFactionFoundingReceiptsArray(JsonObject root) => EnsureArray(root, "factionFoundingReceipts");
    public static JsonArray EnsureFactionRealignmentReceiptsArray(JsonObject root) => EnsureArray(root, "factionRealignmentReceipts");

    public static JsonObject? FindReceipt(JsonArray receipts, string requestId)
    {
        if (string.IsNullOrWhiteSpace(requestId))
            return null;

        JsonObject? match = null;
        foreach (var receipt in receipts.OfType<JsonObject>())
        {
            if (!string.Equals(GetNodeString(receipt["requestId"]), requestId, StringComparison.OrdinalIgnoreCase))
                continue;

            if (match != null)
                return null;

            match = receipt;
        }

        return match;
    }

    public static JsonObject? FindLeadershipHistoryEntry(JsonArray history, string requestId)
    {
        if (string.IsNullOrWhiteSpace(requestId))
            return null;

        JsonObject? match = null;
        foreach (var entry in history.OfType<JsonObject>())
        {
            if (!string.Equals(GetNodeString(entry["requestId"]), requestId, StringComparison.OrdinalIgnoreCase))
                continue;

            if (match != null)
                return null;

            match = entry;
        }

        return match;
    }

    public static void NormalizeStateRoot(
        JsonObject root,
        JsonObject? residentRoot) =>
        NormalizeStateRootWithFactionModes(
            root,
            residentRoot,
            normalizationModes: null);

    internal static void NormalizeStateRootWithFactionModes(
        JsonObject root,
        JsonObject? residentRoot,
        IReadOnlyDictionary<string, ShiningFactionNormalizationMode>?
            normalizationModes)
    {
        var availability = GetNodeString(root["availability"]);
        root["availability"] = IsSupportedAvailability(availability) ? availability : AvailabilityActive;

        if (root["radiance"] is not JsonObject radiance)
        {
            radiance = new JsonObject();
            root["radiance"] = radiance;
        }

        var radianceExperience = Math.Max(0, GetNodeInt(radiance["experience"], 0));
        var radianceTier = ResolveRadianceTier(radianceExperience);
        radiance["experience"] = radianceExperience;
        radiance["tier"] = radianceTier;

        root["lightSparks"] = Math.Clamp(GetNodeInt(root["lightSparks"], 100), 0, 100);

        foreach (var hall in EnsureHallsArray(root).OfType<JsonObject>())
            NormalizeHallObject(hall);
        foreach (var actor in EnsurePoliticalActorsArray(root).OfType<JsonObject>())
            NormalizePoliticalActorObject(actor);
        foreach (var faction in EnsureFactionsArray(root).OfType<JsonObject>())
        {
            var factionId = GetNodeString(faction["factionId"]);
            var mode = normalizationModes != null
                ? !string.IsNullOrWhiteSpace(factionId) &&
                  normalizationModes.TryGetValue(
                      factionId,
                      out var selectedMode)
                    ? selectedMode
                    : ShiningFactionNormalizationMode
                        .AuthoredMaterialization
                : faction.ContainsKey(
                    FactionMaterializationContract.PropertyName)
                    ? ShiningFactionNormalizationMode
                        .AuthoredMaterialization
                    : ShiningFactionNormalizationMode
                        .LegacyCompatibility;
            NormalizeFactionObject(
                faction,
                residentRoot,
                radianceTier,
                mode);
        }
        if (root[FactionConflictCampaignsProperty] is JsonArray campaigns)
        {
            foreach (var campaign in campaigns.OfType<JsonObject>())
                NormalizeFactionConflictCampaignObject(campaign);
        }

        if (root["pendingNativeFactionDiscovery"] is JsonObject pendingDiscovery)
            NormalizePendingNativeFactionDiscoveryObject(pendingDiscovery, radianceTier);
        else if (!root.ContainsKey("pendingNativeFactionDiscovery"))
            root["pendingNativeFactionDiscovery"] = null;

        if (root["gates"] is not JsonObject gates)
        {
            gates = BuildDefaultGatesObject();
            root["gates"] = gates;
        }

        NormalizeGatesObject(gates, radianceTier);

        if (root["preparedIncarnationPackage"] is JsonObject preparedPackage)
            NormalizePreparedIncarnationPackage(preparedPackage);
        else if (!root.ContainsKey("preparedIncarnationPackage"))
            root["preparedIncarnationPackage"] = null;

        if (root["gachaSystem"] is not JsonObject gachaSystem)
        {
            gachaSystem = BuildDefaultGachaSystemObject();
            root["gachaSystem"] = gachaSystem;
        }

        NormalizeGachaSystemObject(gachaSystem, radianceTier);
        NormalizeTreasuryObject(root);
        NormalizeSourceOfLightCapstoneObject(root);

        NormalizeReceiptArray(root, residentRoot, "coreActionReceipts");
        NormalizeReceiptArray(root, residentRoot, "factionFoundingReceipts");
        NormalizeReceiptArray(root, residentRoot, "factionRealignmentReceipts");
    }

    private static void NormalizeSourceOfLightCapstoneObject(JsonObject root)
    {
        if (!root.ContainsKey(SourceOfLightCapstoneState.ShiningStateProperty))
            return;

        if (root[SourceOfLightCapstoneState.ShiningStateProperty] is not JsonObject capstone)
        {
            root[SourceOfLightCapstoneState.ShiningStateProperty] = null;
            return;
        }

        capstone["completed"] = capstone["completed"] is JsonValue completed &&
                                 completed.TryGetValue<bool>(out var completedValue) &&
                                 completedValue;
    }

    public static int ResolveRadianceTier(int experience)
    {
        var xp = Math.Max(0, experience);
        return xp switch
        {
            <= 99 => 0,
            <= 219 => 1,
            <= 379 => 2,
            <= 579 => 3,
            _ => 4
        };
    }

    internal static int ResolveRadianceTierFromAuthoredState(
        JsonObject? root) =>
        ResolveRadianceTier(
            GetNodeInt(root?["radiance"]?["experience"], 0));

    public static int GetPickCap(int radianceTier) => Math.Clamp(radianceTier, 0, 4) switch
    {
        0 => 1,
        1 => 2,
        2 => 2,
        3 => 3,
        _ => 4
    };

    public static int GetDraftSize(int radianceTier) => Math.Clamp(radianceTier, 0, 4) switch
    {
        0 => 4,
        1 => 6,
        2 => 7,
        3 => 8,
        _ => 10
    };

    public static int GetSupportedProjectCap(int radianceTier) => Math.Clamp(radianceTier, 0, 4) switch
    {
        <= 1 => 1,
        <= 3 => 2,
        _ => 3
    };

    public static int GetFactionStrengthCap(int radianceTier) => Math.Clamp(radianceTier, 0, 4) switch
    {
        0 => 50,
        1 => 65,
        2 => 80,
        3 => 90,
        _ => 100
    };

    public static string GetRadianceRarityCeiling(int radianceTier) => Math.Clamp(radianceTier, 0, 4) switch
    {
        0 => RarityCommon,
        1 => RarityUncommon,
        2 => RarityRare,
        3 => RarityRare,
        _ => RarityRadiant
    };

    public static string GetFactionStrengthBand(int factionStrength) => Math.Clamp(factionStrength, 0, 100) switch
    {
        <= 24 => "Dormant",
        <= 49 => "Stable",
        <= 74 => "Strong",
        _ => "Radiant"
    };

    public static int GetTradeTier(int factionStrength) => Math.Clamp(factionStrength, 0, 100) switch
    {
        <= 24 => 0,
        <= 49 => 1,
        <= 74 => 2,
        _ => 3
    };

    internal static bool FactionHasAvailableTrade(
        JsonObject? faction,
        int factionStrength) =>
        faction != null &&
        IsFactionOperational(faction) &&
        faction["leadership"] is JsonObject leadership &&
        !string.Equals(
            GetNodeString(leadership["leadershipState"]),
            LeadershipStateVacant,
            StringComparison.OrdinalIgnoreCase) &&
        GetTradeTier(factionStrength) >= 1;

    public static double GetServiceMultiplier(int factionStrength) => Math.Clamp(factionStrength, 0, 100) switch
    {
        <= 24 => 0.75,
        <= 49 => 1.0,
        <= 74 => 1.25,
        _ => 1.5
    };

    public static string GetFactionRarityCeiling(int factionStrength) => Math.Clamp(factionStrength, 0, 100) switch
    {
        <= 24 => RarityCommon,
        <= 49 => RarityUncommon,
        <= 74 => RarityRare,
        _ => RarityRadiant
    };

    public static bool FactionExists(JsonObject? root, string? factionId) => FindFaction(root, factionId) != null;

    public static JsonObject? FindFaction(JsonObject? root, string? factionId)
    {
        if (root == null || string.IsNullOrWhiteSpace(factionId) || root["factions"] is not JsonArray factions)
            return null;

        return factions.OfType<JsonObject>().FirstOrDefault(faction =>
            string.Equals(GetNodeString(faction["factionId"]), factionId, StringComparison.OrdinalIgnoreCase));
    }

    public static string GetFactionLifecycleState(JsonObject? faction)
    {
        var state = GetNodeString(faction?["factionLifecycle"]?["state"]);
        return IsSupportedFactionLifecycleState(state) ? state! : FactionLifecycleStateActive;
    }

    public static bool IsFactionDefeated(JsonObject? faction)
    {
        var state = GetFactionLifecycleState(faction);
        return string.Equals(state, FactionLifecycleStateBroken, StringComparison.OrdinalIgnoreCase) ||
               string.Equals(state, FactionLifecycleStateDissolved, StringComparison.OrdinalIgnoreCase);
    }

    public static bool IsFactionOperational(JsonObject? faction)
    {
        var state = GetFactionLifecycleState(faction);
        return string.Equals(state, FactionLifecycleStateActive, StringComparison.OrdinalIgnoreCase) ||
               string.Equals(state, FactionLifecycleStateWeakened, StringComparison.OrdinalIgnoreCase);
    }

    public static bool IsFactionLeaderless(JsonObject? faction)
    {
        var state = GetFactionLifecycleState(faction);
        return string.Equals(state, FactionLifecycleStateLeaderless, StringComparison.OrdinalIgnoreCase);
    }

    public static int CountAscendedResidentsForFaction(JsonObject? residentRoot, string? factionId)
    {
        if (residentRoot == null || string.IsNullOrWhiteSpace(factionId) || residentRoot["entries"] is not JsonArray entries)
            return 0;

        var count = 0;
        foreach (var resident in entries.OfType<JsonObject>())
        {
            if (!string.Equals(NormalizeAscensionState(GetNodeString(resident["ascensionState"])), AscensionStateAscended, StringComparison.OrdinalIgnoreCase))
                continue;
            if (!string.Equals(GetNodeString(resident["shiningFactionId"]), factionId, StringComparison.OrdinalIgnoreCase))
                continue;
            count++;
        }

        return count;
    }

    public static void NormalizeResidentShiningFields(JsonObject resident, JsonObject? shiningRoot)
    {
        var ascensionState = NormalizeAscensionState(GetNodeString(resident["ascensionState"]));
        resident["ascensionState"] = ascensionState;

        var shiningFactionId = GetNodeString(resident["shiningFactionId"]);
        if (!string.Equals(ascensionState, AscensionStateAscended, StringComparison.OrdinalIgnoreCase))
        {
            resident["shiningFactionId"] = null;
            resident["residentRole"] = null;
            resident["factionLoyaltyLevel"] = 0;
            resident["factionLoyaltyTier"] = FactionLoyaltyTierAlienated;
            resident["factionRestlessness"] = 0;
            resident["factionRealignmentState"] = FactionRealignmentStateSettled;
            return;
        }

        if (shiningRoot != null &&
            !string.IsNullOrWhiteSpace(shiningFactionId) &&
            !FactionExists(shiningRoot, shiningFactionId))
        {
            shiningFactionId = string.Empty;
        }

        if (string.IsNullOrWhiteSpace(shiningFactionId))
        {
            resident["shiningFactionId"] = null;
            resident["residentRole"] = null;
            resident["factionLoyaltyLevel"] = 0;
            resident["factionLoyaltyTier"] = FactionLoyaltyTierAlienated;
            resident["factionRestlessness"] = 0;
            resident["factionRealignmentState"] = FactionRealignmentStateSettled;
            return;
        }

        resident["shiningFactionId"] = shiningFactionId;

        var residentRole = GetNodeString(resident["residentRole"]);
        resident["residentRole"] = IsSupportedResidentRole(residentRole) ? residentRole : DeriveDefaultResidentRole(resident);

        if (shiningRoot == null)
        {
            var preservedLoyalty = Math.Clamp(GetNodeInt(resident["factionLoyaltyLevel"], 50), 0, 100);
            var preservedRestlessness = Math.Clamp(GetNodeInt(resident["factionRestlessness"], 0), 0, 100);
            var preservedRealignment = GetNodeString(resident["factionRealignmentState"]);

            resident["factionLoyaltyLevel"] = preservedLoyalty;
            resident["factionLoyaltyTier"] = ResolveFactionLoyaltyTier(preservedLoyalty);
            resident["factionRestlessness"] = preservedRestlessness;
            resident["factionRealignmentState"] = IsSupportedFactionRealignmentState(preservedRealignment)
                ? preservedRealignment
                : ResolveFactionRealignmentState(preservedLoyalty, preservedRestlessness);
            return;
        }

        var faction = FindFaction(shiningRoot, shiningFactionId);
        var loyalty = DeriveFactionLoyaltyLevel(resident, faction);
        var restlessness = DeriveFactionRestlessness(resident, faction, loyalty);

        resident["factionLoyaltyLevel"] = loyalty;
        resident["factionLoyaltyTier"] = ResolveFactionLoyaltyTier(loyalty);
        resident["factionRestlessness"] = restlessness;
        resident["factionRealignmentState"] = ResolveFactionRealignmentState(loyalty, restlessness);
    }

    public static string ResolveFactionLoyaltyTier(int level) => Math.Clamp(level, 0, 100) switch
    {
        <= 19 => FactionLoyaltyTierAlienated,
        <= 39 => FactionLoyaltyTierUncertain,
        <= 59 => FactionLoyaltyTierAttached,
        <= 79 => FactionLoyaltyTierDevoted,
        _ => FactionLoyaltyTierSteadfast
    };

    public static string ResolveFactionRealignmentState(int loyaltyLevel, int restlessness)
    {
        var clampedLoyalty = Math.Clamp(loyaltyLevel, 0, 100);
        var clampedRestlessness = Math.Clamp(restlessness, 0, 100);
        if (clampedLoyalty <= 15 && clampedRestlessness >= 70)
            return FactionRealignmentStateReadyToRealign;
        if (clampedLoyalty <= 30 && clampedRestlessness >= 55)
            return FactionRealignmentStateConsideringRealignment;
        if (clampedLoyalty <= 45 || clampedRestlessness >= 45)
            return FactionRealignmentStateRestless;
        if (clampedLoyalty <= 60 || clampedRestlessness >= 30)
            return FactionRealignmentStateWavering;
        return FactionRealignmentStateSettled;
    }

    private static int DeriveFactionLoyaltyLevel(JsonObject resident, JsonObject? faction)
    {
        var abodeDevotion = Math.Clamp(GetNodeInt(resident["abodeDevotionLevel"], 50), 0, 100);
        var factionStrength = faction == null ? 35 : Math.Clamp(GetNodeInt(faction["factionStrength"], 35), 0, 100);
        var leadershipState = faction?["leadership"]?["leadershipState"]?.GetValue<string>() ?? LeadershipStateSecure;
        var supportedProjects = CountSupportedProjects(faction);

        var strengthModifier = factionStrength switch
        {
            <= 24 => -10,
            <= 49 => -4,
            <= 74 => 3,
            _ => 8
        };

        var leadershipModifier = string.Equals(leadershipState, LeadershipStateContested, StringComparison.OrdinalIgnoreCase)
            ? -10
            : string.Equals(leadershipState, LeadershipStateVacant, StringComparison.OrdinalIgnoreCase)
                ? -15
                : 4;

        var projectModifier = supportedProjects > 0 ? Math.Min(8, supportedProjects * 3) : -5;
        return Math.Clamp(abodeDevotion + strengthModifier + leadershipModifier + projectModifier, 0, 100);
    }

    private static int DeriveFactionRestlessness(JsonObject resident, JsonObject? faction, int normalizedLoyalty)
    {
        var abodeRestlessness = Math.Clamp(GetNodeInt(resident["restlessness"], 0), 0, 100);
        var factionStrength = faction == null ? 35 : Math.Clamp(GetNodeInt(faction["factionStrength"], 35), 0, 100);
        var leadershipState = faction?["leadership"]?["leadershipState"]?.GetValue<string>() ?? LeadershipStateSecure;

        var pressure = factionStrength switch
        {
            <= 24 => 12,
            <= 49 => 5,
            <= 74 => -4,
            _ => -8
        };

        if (string.Equals(leadershipState, LeadershipStateContested, StringComparison.OrdinalIgnoreCase))
            pressure += 12;
        else if (string.Equals(leadershipState, LeadershipStateVacant, StringComparison.OrdinalIgnoreCase))
            pressure += 18;

        if (normalizedLoyalty <= 30)
            pressure += 10;
        else if (normalizedLoyalty >= 80)
            pressure -= 6;

        return Math.Clamp(abodeRestlessness + pressure, 0, 100);
    }

    private static void NormalizeHallObject(JsonObject hall)
    {
        hall["hallId"] = GetNodeString(hall["hallId"]) ?? string.Empty;
        hall["hallName"] = GetNodeString(hall["hallName"]) ?? string.Empty;
        hall["description"] = GetNodeString(hall["description"]) ?? string.Empty;

        var serviceTags = EnsureArray(hall, "serviceTags");
        NormalizeStringArrayInPlace(serviceTags);
        RemoveUnsupportedItems(serviceTags, AllowedHallServiceTags);
    }

    private static void NormalizeFactionObject(
        JsonObject faction,
        JsonObject? residentRoot,
        int radianceTier,
        ShiningFactionNormalizationMode mode)
    {
        faction["factionId"] = GetNodeString(faction["factionId"]) ?? string.Empty;

        var legacyCompatibility =
            mode == ShiningFactionNormalizationMode.LegacyCompatibility;
        if (legacyCompatibility)
        {
            var originType = GetNodeString(faction["originType"]);
            faction["originType"] = string.IsNullOrWhiteSpace(originType)
                ? OriginTypeAscendedGuardian
                : originType;
            faction["hallId"] =
                GetNodeString(faction["hallId"]) ?? string.Empty;
            NormalizeFactionLifecycleObject(faction);
        }

        var charter = faction["charter"] as JsonObject;
        if (legacyCompatibility)
        {
            if (charter == null)
            {
                charter = new JsonObject();
                faction["charter"] = charter;
            }

            charter["factionName"] =
                GetNodeString(charter["factionName"]) ??
                GetNodeString(faction["factionName"]) ??
                string.Empty;
            var authoredFavoredArchetype =
                GetNodeString(charter["favoredArchetype"]) ??
                GetNodeString(faction["favoredArchetype"]);
            charter["favoredArchetype"] =
                string.IsNullOrWhiteSpace(authoredFavoredArchetype)
                    ? ProjectArchetypeAccord
                    : authoredFavoredArchetype;
            var patronEffectFamily =
                GetNodeString(charter["patronEffectFamily"]) ??
                GetNodeString(faction["patronEffectFamily"]);
            charter["patronEffectFamily"] =
                string.IsNullOrWhiteSpace(patronEffectFamily)
                    ? EffectFamilySocial
                    : patronEffectFamily;
            charter["summary"] =
                GetNodeString(charter["summary"]) ?? string.Empty;
        }

        if (legacyCompatibility)
        {
            if (faction["leadership"] is not JsonObject leadership)
            {
                leadership = new JsonObject();
                faction["leadership"] = leadership;
            }

            var leadershipState =
                GetNodeString(leadership["leadershipState"]) ??
                LeadershipStateSecure;
            leadership["leadershipState"] =
                IsSupportedLeadershipState(leadershipState)
                    ? leadershipState
                    : LeadershipStateSecure;
            if (IsFactionDefeated(faction) ||
                IsFactionLeaderless(faction))
            {
                leadership["leadershipState"] =
                    LeadershipStateVacant;
                leadership["headActorType"] = null;
                leadership["headActorId"] = null;
            }
            else if (string.Equals(
                         GetNodeString(
                             leadership["leadershipState"]),
                         LeadershipStateVacant,
                         StringComparison.OrdinalIgnoreCase))
            {
                leadership["headActorType"] = null;
                leadership["headActorId"] = null;
            }
            else
            {
                var headActorType =
                    GetNodeString(leadership["headActorType"]) ??
                    GetNodeString(faction["headActorType"]);
                leadership["headActorType"] =
                    IsSupportedHeadActorType(headActorType)
                        ? headActorType
                        : HeadActorTypeGuardian;
                var headActorId =
                    GetNodeString(leadership["headActorId"]) ??
                    GetNodeString(faction["headActorId"]) ??
                    string.Empty;
                if (string.Equals(
                        GetNodeString(
                            leadership["headActorType"]),
                        HeadActorTypePlayerSoul,
                        StringComparison.OrdinalIgnoreCase) &&
                    string.IsNullOrWhiteSpace(headActorId))
                {
                    headActorId = HeadActorTypePlayerSoul;
                }

                leadership["headActorId"] = headActorId;
            }
        }

        faction["baseStrength"] = ResolveCanonicalBaseStrength(faction, radianceTier);
        faction["investCountThisAscension"] = Math.Clamp(GetNodeInt(faction["investCountThisAscension"], 0), 0, 3);

        var countedArchetypes = legacyCompatibility
            ? EnsureArray(
                faction,
                "projectArchetypesCountedThisAscension")
            : faction["projectArchetypesCountedThisAscension"]
                as JsonArray;
        if (countedArchetypes != null)
        {
            NormalizeStringArrayInPlace(countedArchetypes);
            RemoveUnsupportedItems(
                countedArchetypes,
                AllowedProjectArchetypes);
        }

        var projects = legacyCompatibility
            ? EnsureArray(faction, "projects")
            : faction["projects"] as JsonArray;
        var favoredArchetype =
            GetNodeString(charter?["favoredArchetype"]) ??
            ProjectArchetypeAccord;
        if (projects != null)
        {
            foreach (var project in projects.OfType<JsonObject>())
            {
                NormalizeProjectObject(project, favoredArchetype);
                if (IsFactionDefeated(faction))
                    project["isSupported"] = false;
            }
        }

        if (IsFactionDefeated(faction) && faction.ContainsKey("tradeInventory"))
        {
            faction["tradeInventory"] = null;
        }
        else if (faction["tradeInventory"] is JsonObject tradeInventory)
        {
            NormalizeTradeInventoryObject(tradeInventory);
        }
        else if (faction.ContainsKey("tradeInventory") && faction["tradeInventory"] is not null)
        {
            faction.Remove("tradeInventory");
        }

        if (legacyCompatibility ||
            faction["tradeInventoryReceipts"] is JsonArray)
        {
            ShiningTradeRequestState
                .NormalizeTradeInventoryReceiptsShape(faction);
            HydrateTradeInventoryReceiptSnapshots(faction);
        }

        if (legacyCompatibility)
        {
            EnsureArray(faction, "leadershipReceipts");
            EnsureArray(faction, "leadershipHistory");
        }

        NormalizeFactionPoliticalMemory(faction, mode);

        faction["factionStrength"] = ComputeFactionStrength(faction, residentRoot, radianceTier);

        if (legacyCompatibility)
        {
            faction.Remove("factionName");
            faction.Remove("favoredArchetype");
            faction.Remove("patronEffectFamily");
            faction.Remove("headActorType");
            faction.Remove("headActorId");
        }
    }

    private static void NormalizeFactionLifecycleObject(JsonObject faction)
    {
        if (faction["factionLifecycle"] is not JsonObject lifecycle)
        {
            lifecycle = new JsonObject();
            faction["factionLifecycle"] = lifecycle;
        }

        var state = GetNodeString(lifecycle["state"]);
        lifecycle["state"] = IsSupportedFactionLifecycleState(state) ? state : FactionLifecycleStateActive;
        if (IsFactionDefeated(faction))
        {
            lifecycle["defeatedAtTurn"] = Math.Max(0, GetNodeInt(lifecycle["defeatedAtTurn"], 0));
            lifecycle["defeatedAtUtc"] = GetNodeString(lifecycle["defeatedAtUtc"]) ?? string.Empty;
            lifecycle["defeatReason"] = GetNodeString(lifecycle["defeatReason"]) ?? string.Empty;
            lifecycle["remnantsSummary"] = GetNodeString(lifecycle["remnantsSummary"]) ?? string.Empty;
        }
    }

    private static void NormalizeFactionPoliticalMemory(
        JsonObject faction,
        ShiningFactionNormalizationMode mode)
    {
        var legacyCompatibility =
            mode == ShiningFactionNormalizationMode.LegacyCompatibility;
        var chronicle = legacyCompatibility
            ? EnsureArray(faction, FactionChronicleProperty)
            : faction[FactionChronicleProperty] as JsonArray;
        foreach (var entry in
                 chronicle?.OfType<JsonObject>() ??
                 Enumerable.Empty<JsonObject>())
        {
            if (legacyCompatibility)
            {
                entry["entryId"] =
                    GetNodeString(entry["entryId"]) ?? string.Empty;
                entry["eventType"] =
                    GetNodeString(entry["eventType"]) ?? string.Empty;
                entry["summary"] =
                    GetNodeString(entry["summary"]) ?? string.Empty;
                entry["visibility"] =
                    GetNodeString(entry["visibility"]) ?? "known";
            }

            entry["turnNumber"] = Math.Max(0, GetNodeInt(entry["turnNumber"], 0));
            var consequences = legacyCompatibility
                ? EnsureArray(entry, "consequences")
                : entry["consequences"] as JsonArray;
            if (consequences != null)
                NormalizeStringArrayInPlace(consequences);
        }

        var influence = legacyCompatibility
            ? EnsureArray(faction, FactionInfluenceProperty)
            : faction[FactionInfluenceProperty] as JsonArray;
        foreach (var zone in
                 influence?.OfType<JsonObject>() ??
                 Enumerable.Empty<JsonObject>())
        {
            zone["zoneId"] = GetNodeString(zone["zoneId"]) ?? string.Empty;
            zone["scopeType"] = GetNodeString(zone["scopeType"]) ?? string.Empty;
            zone["scopeId"] = GetNodeString(zone["scopeId"]) ?? string.Empty;
            zone["displayName"] = GetNodeString(zone["displayName"]) ?? GetNodeString(zone["scopeId"]) ?? string.Empty;
            zone["controlLevel"] = Math.Clamp(GetNodeInt(zone["controlLevel"], 0), 0, 100);
            zone["influenceValue"] = Math.Clamp(GetNodeInt(zone["influenceValue"], GetNodeInt(zone["controlLevel"], 0)), 0, 100);
            zone["publicStatus"] = GetNodeString(zone["publicStatus"]) ?? "known";
            zone["updatedAtTurn"] = Math.Max(0, GetNodeInt(zone["updatedAtTurn"], 0));
            zone["sourceEntryId"] = GetNodeString(zone["sourceEntryId"]) ?? string.Empty;
        }

        var memory =
            faction[FactionStrategicMemoryProperty] as JsonObject;
        if (memory == null && legacyCompatibility)
        {
            memory = new JsonObject();
            faction[FactionStrategicMemoryProperty] = memory;
        }

        if (memory != null)
        {
            if (legacyCompatibility)
            {
                memory["summary"] =
                    GetNodeString(memory["summary"]) ?? string.Empty;
            }

            memory["lastUpdatedTurn"] = Math.Max(
                0,
                GetNodeInt(memory["lastUpdatedTurn"], 0));
            foreach (var propertyName in new[]
                     {
                         "recentCampaigns",
                         "losses",
                         "alliances",
                         "enemies"
                     })
            {
                var values = legacyCompatibility
                    ? EnsureArray(memory, propertyName)
                    : memory[propertyName] as JsonArray;
                if (values != null)
                    NormalizeStringArrayInPlace(values);
            }
        }

        var resourceLedger = legacyCompatibility
            ? EnsureArray(faction, FactionResourceLedgerProperty)
            : faction[FactionResourceLedgerProperty] as JsonArray;
        foreach (var entry in
                 resourceLedger?.OfType<JsonObject>() ??
                 Enumerable.Empty<JsonObject>())
        {
            entry["entryId"] = GetNodeString(entry["entryId"]) ?? string.Empty;
            entry["turnNumber"] = Math.Max(0, GetNodeInt(entry["turnNumber"], 0));
            entry["resourceType"] = GetNodeString(entry["resourceType"]) ?? string.Empty;
            entry["delta"] = GetNodeInt(entry["delta"], 0);
            entry["balanceAfter"] = Math.Max(0, GetNodeInt(entry["balanceAfter"], 0));
            entry["reason"] = GetNodeString(entry["reason"]) ?? string.Empty;
        }
    }

    private static void NormalizeFactionConflictCampaignObject(JsonObject campaign)
    {
        campaign["campaignId"] = GetNodeString(campaign["campaignId"]) ?? string.Empty;
        campaign["targetFactionId"] = GetNodeString(campaign["targetFactionId"]) ?? string.Empty;

        campaign["goal"] = GetNodeString(campaign["goal"]) ?? string.Empty;
        campaign["status"] = GetNodeString(campaign["status"]) ?? string.Empty;
        campaign["startedAtTurn"] = Math.Max(0, GetNodeInt(campaign["startedAtTurn"], 0));
        campaign["startedAtUtc"] = GetNodeString(campaign["startedAtUtc"]) ?? string.Empty;
        campaign["completedAtTurn"] = Math.Max(0, GetNodeInt(campaign["completedAtTurn"], 0));
        campaign["completedAtUtc"] = GetNodeString(campaign["completedAtUtc"]) ?? string.Empty;
        campaign["summary"] = GetNodeString(campaign["summary"]) ?? string.Empty;
        campaign["playerIntent"] = GetNodeString(campaign["playerIntent"]) ?? string.Empty;

        if (campaign["breakthroughLog"] is not JsonArray breakthroughs)
            return;

        foreach (var breakthrough in breakthroughs.OfType<JsonObject>())
        {
            breakthrough["breakthroughId"] = GetNodeString(breakthrough["breakthroughId"]) ?? string.Empty;
            breakthrough["type"] = GetNodeString(breakthrough["type"]) ?? string.Empty;
            breakthrough["resolvedAtTurn"] = Math.Max(0, GetNodeInt(breakthrough["resolvedAtTurn"], 0));
            breakthrough["resolvedAtUtc"] = GetNodeString(breakthrough["resolvedAtUtc"]) ?? string.Empty;
            breakthrough["summary"] = GetNodeString(breakthrough["summary"]) ?? string.Empty;
        }
    }

    private static void NormalizeProjectObject(JsonObject project, string favoredArchetype)
    {
        project["projectId"] = GetNodeString(project["projectId"]) ?? string.Empty;
        project["displayName"] = GetNodeString(project["displayName"]) ?? string.Empty;
        project["summary"] = GetNodeString(project["summary"]) ?? string.Empty;
        NormalizeStringArrayInPlace(EnsureArray(project, "toneTags"));
        NormalizeStringArrayInPlace(EnsureArray(project, "targetFactionIds"));

        var projectArchetype = GetNodeString(project["projectArchetype"]);
        project["projectArchetype"] = string.IsNullOrWhiteSpace(projectArchetype) ? favoredArchetype : projectArchetype;

        var outputEffectFamily = GetNodeString(project["outputEffectFamily"]);
        project["outputEffectFamily"] = IsSupportedEffectFamily(outputEffectFamily)
            ? outputEffectFamily
            : string.IsNullOrWhiteSpace(outputEffectFamily)
                ? ResolveDefaultOutputFamily(project["projectArchetype"]?.GetValue<string>())
                : outputEffectFamily;

        var tier = Math.Clamp(GetNodeInt(project["tier"], 1), 1, 3);
        project["tier"] = tier;

        var status = GetNodeString(project["status"]);
        project["status"] = string.IsNullOrWhiteSpace(status) ? ProjectStatusCompleted : status;

        var isSupported = GetNodeBool(project["isSupported"]);
        if (!string.Equals(project["status"]?.GetValue<string>(), ProjectStatusCompleted, StringComparison.OrdinalIgnoreCase))
            isSupported = false;
        project["isSupported"] = isSupported;

        project["strengthReward"] = ResolveProjectStrengthReward(tier);
        project["completedAtTurn"] = Math.Max(0, GetNodeInt(project["completedAtTurn"], 0));
        project["completedAtUtc"] = GetNodeString(project["completedAtUtc"]) ?? string.Empty;
    }

    private static void NormalizePoliticalActorObject(JsonObject actor)
    {
        actor["actorId"] = GetNodeString(actor["actorId"]) ?? string.Empty;
        actor["actorType"] = HeadActorTypeRadiantActor;
        actor["displayName"] = GetNodeString(actor["displayName"]) ?? string.Empty;
        actor["summary"] = GetNodeString(actor["summary"]) ?? string.Empty;
        actor["originFactionId"] = GetNodeString(actor["originFactionId"]) ?? string.Empty;
        actor["currentFactionId"] = GetNodeString(actor["currentFactionId"]);

        var status = GetNodeString(actor["politicalStatus"]);
        actor["politicalStatus"] = IsSupportedPoliticalStatus(status) ? status : PoliticalStatusElder;
    }

    private static void NormalizePendingNativeFactionDiscoveryObject(JsonObject pendingDiscovery, int radianceTier)
    {
        pendingDiscovery["requestId"] = GetNodeString(pendingDiscovery["requestId"]) ?? string.Empty;
        pendingDiscovery["createdAtTurn"] = Math.Max(0, GetNodeInt(pendingDiscovery["createdAtTurn"], 0));
        pendingDiscovery["createdAtUtc"] = GetNodeString(pendingDiscovery["createdAtUtc"]) ?? string.Empty;
        pendingDiscovery["radianceTierAtRequest"] = Math.Clamp(GetNodeInt(pendingDiscovery["radianceTierAtRequest"], radianceTier), 0, 4);
        pendingDiscovery["costFeathers"] = Math.Max(0, GetNodeInt(pendingDiscovery["costFeathers"], 25));
        pendingDiscovery["costLightSparks"] = Math.Max(0, GetNodeInt(pendingDiscovery["costLightSparks"], 20));
    }

    private static void NormalizeGatesObject(JsonObject gates, int radianceTier)
    {
        gates["draftVersion"] = Math.Max(0, GetNodeInt(gates["draftVersion"], 0));
        var hasOpenDraft = GetNodeBool(gates["hasOpenDraft"]);
        gates["hasOpenDraft"] = hasOpenDraft;
        gates["isStale"] = hasOpenDraft && GetNodeBool(gates["isStale"]);

        var allCards = EnsureArray(gates, "allCandidateBlessingCards");
        var availableCards = EnsureArray(gates, "availableBlessingCards");
        var shownCardIds = EnsureArray(gates, "shownBlessingCardIds");
        var selectedCardIds = EnsureArray(gates, "selectedBlessingCardIds");

        foreach (var card in allCards.OfType<JsonObject>())
            NormalizeBlessingCardObject(card);
        foreach (var card in availableCards.OfType<JsonObject>())
            NormalizeBlessingCardObject(card);

        NormalizeStringArrayInPlace(shownCardIds);
        NormalizeStringArrayInPlace(selectedCardIds);
        DeduplicateStringArrayInPlace(shownCardIds);
        DeduplicateStringArrayInPlace(selectedCardIds);

        var allCandidateIds = new HashSet<string>(
            allCards.OfType<JsonObject>()
                .Select(card => GetNodeString(card["cardId"]))
                .Where(cardId => !string.IsNullOrWhiteSpace(cardId))!,
            StringComparer.OrdinalIgnoreCase);
        var availableIds = new HashSet<string>(
            availableCards.OfType<JsonObject>()
                .Select(card => GetNodeString(card["cardId"]))
                .Where(cardId => !string.IsNullOrWhiteSpace(cardId))!,
            StringComparer.OrdinalIgnoreCase);

        TrimStringArrayToSet(shownCardIds, allCandidateIds);
        TrimStringArrayToSet(selectedCardIds, availableIds);

        gates["nextCandidateCursor"] = Math.Clamp(GetNodeInt(gates["nextCandidateCursor"], availableCards.Count), 0, allCards.Count);
        gates["rerollsRemaining"] = Math.Max(0, GetNodeInt(gates["rerollsRemaining"], 0));

        while (selectedCardIds.Count > GetPickCap(radianceTier))
            selectedCardIds.RemoveAt(selectedCardIds.Count - 1);

        if (!hasOpenDraft)
        {
            gates["isStale"] = false;
            gates["allCandidateBlessingCards"] = new JsonArray();
            gates["availableBlessingCards"] = new JsonArray();
            gates["shownBlessingCardIds"] = new JsonArray();
            gates["selectedBlessingCardIds"] = new JsonArray();
            gates["nextCandidateCursor"] = 0;
            gates["rerollsRemaining"] = 0;
        }
    }

    private static void NormalizeBlessingCardObject(JsonObject card)
    {
        NormalizeOptionalStringProperty(card, "cardId");
        NormalizeOptionalStringProperty(card, "dedupeKey");
        NormalizeCanonicalTokenIfSupported(card, "sourceType", IsSupportedCardSourceType);
        NormalizeOptionalStringProperty(card, "sourceFactionId");
        NormalizeOptionalStringProperty(card, "sourceFactionName");
        NormalizeOptionalStringProperty(card, "sourceActorId");
        NormalizeOptionalStringProperty(card, "sourceActorName");
        NormalizeCanonicalTokenIfSupported(card, "effectFamily", IsSupportedEffectFamily);
        NormalizeCanonicalTokenIfSupported(card, "rarity", IsSupportedRarity);
        NormalizeOptionalStringProperty(card, "displayName");
        NormalizeOptionalStringProperty(card, "displaySummary");
    }

    private static void NormalizePreparedIncarnationPackage(JsonObject preparedPackage)
    {
        var selectedIds = EnsureArray(preparedPackage, "selectedCardIds");
        var selectedCards = EnsureArray(preparedPackage, "selectedCards");
        NormalizeStringArrayInPlace(selectedIds);
        foreach (var card in selectedCards.OfType<JsonObject>())
            NormalizeBlessingCardObject(card);
        preparedPackage["generatedFromDraftVersion"] = Math.Max(0, GetNodeInt(preparedPackage["generatedFromDraftVersion"], 0));
        preparedPackage["preparedAtTurn"] = Math.Max(0, GetNodeInt(preparedPackage["preparedAtTurn"], 0));
        preparedPackage["preparedAtUtc"] = GetNodeString(preparedPackage["preparedAtUtc"]) ?? string.Empty;
    }

    private static string? ValidateRawBlessingCardContracts(JsonObject root)
    {
        if (root["gates"] is JsonObject gates)
        {
            var gatesIssue = ValidateRawBlessingCardArray(gates["allCandidateBlessingCards"], "gates.allCandidateBlessingCards") ??
                             ValidateRawBlessingCardArray(gates["availableBlessingCards"], "gates.availableBlessingCards") ??
                             ValidateRawBlessingIdArray(gates["shownBlessingCardIds"], "gates.shownBlessingCardIds") ??
                             ValidateRawBlessingIdArray(gates["selectedBlessingCardIds"], "gates.selectedBlessingCardIds");
            if (!string.IsNullOrWhiteSpace(gatesIssue))
                return gatesIssue;
        }

        if (root["preparedIncarnationPackage"] is JsonObject preparedPackage)
        {
            var packageIssue = ValidatePreparedIncarnationPackageForBootstrap(preparedPackage);
            if (!string.IsNullOrWhiteSpace(packageIssue))
                return packageIssue;
        }

        return null;
    }

    public static string? ValidatePreparedIncarnationPackageForBootstrap(JsonObject preparedPackage)
    {
        if (preparedPackage["selectedCardIds"] is not JsonArray selectedIds)
            return "preparedIncarnationPackage.selectedCardIds отсутствует или повреждён и не может authorise actionable Shining bootstrap.";

        if (preparedPackage["selectedCards"] is not JsonArray selectedCards)
            return "preparedIncarnationPackage.selectedCards отсутствует или повреждён и не может authorise actionable Shining bootstrap.";

        if (selectedIds.Count == 0 || selectedCards.Count == 0)
            return "preparedIncarnationPackage должен содержать хотя бы одну frozen blessing card для actionable Shining bootstrap.";

        return ValidateRawBlessingIdArray(selectedIds, "preparedIncarnationPackage.selectedCardIds") ??
               ValidateRawBlessingCardArray(selectedCards, "preparedIncarnationPackage.selectedCards") ??
               ValidatePreparedPackageUniqueCardIds(preparedPackage) ??
               ValidatePreparedPackageCardSnapshot(preparedPackage);
    }

    private static string? ValidateRawBlessingCardArray(JsonNode? node, string path)
    {
        if (node == null)
            return null;
        if (node is not JsonArray cards)
            return $"{path} повреждён и не может authorise actionable Shining mode.";

        foreach (var card in cards)
        {
            if (card is not JsonObject cardObject)
                return $"{path} содержит повреждённую blessing-card запись и не может authorise actionable Shining mode.";

            if (string.IsNullOrWhiteSpace(GetNodeString(cardObject["cardId"])) ||
                !IsSupportedCardSourceType(GetNodeString(cardObject["sourceType"])) ||
                !IsSupportedEffectFamily(GetNodeString(cardObject["effectFamily"])) ||
                !IsSupportedRarity(GetNodeString(cardObject["rarity"])) ||
                cardObject["effectPayload"] is not JsonObject)
            {
                return $"{path} содержит повреждённую blessing-card запись и не может authorise actionable Shining mode.";
            }
        }

        return null;
    }

    private static string? ValidateRawBlessingIdArray(JsonNode? node, string path)
    {
        if (node == null)
            return null;
        if (node is not JsonArray ids)
            return $"{path} повреждён и не может authorise actionable Shining mode.";

        foreach (var idNode in ids)
        {
            if (idNode is not JsonValue value ||
                !value.TryGetValue<string>(out var id) ||
                string.IsNullOrWhiteSpace(id))
            {
                return $"{path} содержит повреждённый blessing-card id snapshot и не может authorise actionable Shining mode.";
            }
        }

        return null;
    }

    private static string? ValidatePreparedPackageCardSnapshot(JsonObject preparedPackage)
    {
        if (preparedPackage["selectedCardIds"] is not JsonArray selectedIds ||
            preparedPackage["selectedCards"] is not JsonArray selectedCards)
        {
            return null;
        }

        var orderedIds = selectedIds.OfType<JsonValue>()
            .Select(node => node.TryGetValue<string>(out var value) ? value?.Trim() ?? string.Empty : string.Empty)
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .ToList();
        var cardSnapshotIds = selectedCards.OfType<JsonObject>()
            .Select(card => GetNodeString(card["cardId"]) ?? string.Empty)
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .ToList();

        return orderedIds.SequenceEqual(cardSnapshotIds, StringComparer.OrdinalIgnoreCase)
            ? null
            : "preparedIncarnationPackage содержит mismatched selectedCardIds/selectedCards snapshot и не может authorise actionable Shining mode.";
    }

    private static string? ValidatePreparedPackageUniqueCardIds(JsonObject preparedPackage)
    {
        if (preparedPackage["selectedCardIds"] is not JsonArray selectedIds ||
            preparedPackage["selectedCards"] is not JsonArray selectedCards)
        {
            return null;
        }

        var orderedIds = selectedIds.OfType<JsonValue>()
            .Select(node => node.TryGetValue<string>(out var value) ? value?.Trim() ?? string.Empty : string.Empty)
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .ToList();
        if (orderedIds.Count != orderedIds.Distinct(StringComparer.OrdinalIgnoreCase).Count())
            return "preparedIncarnationPackage.selectedCardIds содержит duplicate blessing card id и не может authorise actionable Shining bootstrap.";

        var cardIds = selectedCards.OfType<JsonObject>()
            .Select(card => GetNodeString(card["cardId"]) ?? string.Empty)
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .ToList();
        return cardIds.Count == cardIds.Distinct(StringComparer.OrdinalIgnoreCase).Count()
            ? null
            : "preparedIncarnationPackage.selectedCards содержит duplicate blessing card id и не может authorise actionable Shining bootstrap.";
    }

    private static string? ValidateRawPoliticalContracts(JsonObject root)
    {
        if (root["halls"] is not JsonArray halls)
            return "halls повреждён и не может authorise actionable Shining mode.";

        var hallIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var hallNode in halls)
        {
            if (hallNode is not JsonObject hall)
                return "halls содержит повреждённую запись и не может authorise actionable Shining mode.";

            var hallId = GetNodeString(hall["hallId"]);
            if (string.IsNullOrWhiteSpace(hallId) || !hallIds.Add(hallId))
                return "halls содержит missing или duplicate hallId и не может authorise actionable Shining mode.";
        }

        if (root["factions"] is not JsonArray factions)
            return "factions повреждён и не может authorise actionable Shining mode.";

        var factionIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var projectIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var supportedProjects = 0;
        var radianceTier = GetNodeInt(root["radiance"]?["tier"], 0);
        foreach (var factionNode in factions)
        {
            if (factionNode is not JsonObject faction)
                return "factions содержит повреждённую запись и не может authorise actionable Shining mode.";

            var factionId = GetNodeString(faction["factionId"]);
            if (string.IsNullOrWhiteSpace(factionId) || !factionIds.Add(factionId))
                return "factions содержит missing или duplicate factionId и не может authorise actionable Shining mode.";

            if (!IsSupportedOriginType(GetNodeString(faction["originType"])))
                return "factions содержит неподдерживаемый originType и не может authorise actionable Shining mode.";

            var lifecycleState = GetFactionLifecycleState(faction);
            if (faction["factionLifecycle"] is JsonObject lifecycle &&
                !IsSupportedFactionLifecycleState(GetNodeString(lifecycle["state"])))
            {
                return "factions содержит неподдерживаемый factionLifecycle.state и не может authorise actionable Shining mode.";
            }
            var isDefeatedFaction = string.Equals(lifecycleState, FactionLifecycleStateBroken, StringComparison.OrdinalIgnoreCase) ||
                                    string.Equals(lifecycleState, FactionLifecycleStateDissolved, StringComparison.OrdinalIgnoreCase);
            var isLeaderlessFaction = string.Equals(lifecycleState, FactionLifecycleStateLeaderless, StringComparison.OrdinalIgnoreCase);

            if (faction["charter"] is not JsonObject charter ||
                !IsSupportedProjectArchetype(GetNodeString(charter["favoredArchetype"])) ||
                !IsSupportedEffectFamily(GetNodeString(charter["patronEffectFamily"])))
            {
                return "factions содержит повреждённый charter и не может authorise actionable Shining mode.";
            }

            if (faction["leadership"] is not JsonObject leadership)
                return "factions содержит повреждённый leadership contract и не может authorise actionable Shining mode.";

            var leadershipState = GetNodeString(leadership["leadershipState"]);
            if ((isDefeatedFaction || isLeaderlessFaction) &&
                (!string.Equals(leadershipState, LeadershipStateVacant, StringComparison.OrdinalIgnoreCase) ||
                 !string.IsNullOrWhiteSpace(GetNodeString(leadership["headActorType"])) ||
                 !string.IsNullOrWhiteSpace(GetNodeString(leadership["headActorId"]))))
            {
                return "leaderless/broken/dissolved faction содержит active leadership и не может authorise actionable Shining mode.";
            }

            if (!IsSupportedLeadershipState(leadershipState))
                return "factions содержит неподдерживаемый leadershipState и не может authorise actionable Shining mode.";

            if (string.Equals(leadershipState, LeadershipStateVacant, StringComparison.OrdinalIgnoreCase))
            {
                if (!string.IsNullOrWhiteSpace(GetNodeString(leadership["headActorType"])) ||
                    !string.IsNullOrWhiteSpace(GetNodeString(leadership["headActorId"])))
                {
                    return "vacant leadership не может одновременно содержать head binding в actionable Shining mode.";
                }
            }
            else
            {
                if (!IsSupportedHeadActorType(GetNodeString(leadership["headActorType"])) ||
                    string.IsNullOrWhiteSpace(GetNodeString(leadership["headActorId"])))
                {
                    return "factions содержит повреждённый head binding и не может authorise actionable Shining mode.";
                }
            }

            if (faction["projects"] is not JsonArray projects)
                return "factions.projects повреждён и не может authorise actionable Shining mode.";

            foreach (var projectNode in projects)
            {
                if (projectNode is not JsonObject project ||
                    !IsSupportedProjectArchetype(GetNodeString(project["projectArchetype"])) ||
                    !IsSupportedEffectFamily(GetNodeString(project["outputEffectFamily"])) ||
                    !IsSupportedProjectStatus(GetNodeString(project["status"])))
                {
                    return "factions.projects содержит повреждённый project contract и не может authorise actionable Shining mode.";
                }

                var projectId = GetNodeString(project["projectId"]);
                if (string.IsNullOrWhiteSpace(projectId) || !projectIds.Add(projectId))
                    return "factions.projects содержит missing или duplicate projectId и не может authorise actionable Shining mode.";

                if (string.Equals(GetNodeString(project["status"]), ProjectStatusCompleted, StringComparison.OrdinalIgnoreCase) &&
                    GetNodeBool(project["isSupported"]))
                {
                    if (isDefeatedFaction)
                        return "broken/dissolved faction содержит supported project и не может authorise actionable Shining mode.";

                    supportedProjects++;
                }
            }

            if (isDefeatedFaction && faction["tradeInventory"] is JsonObject)
                return "broken/dissolved faction содержит active tradeInventory и не может authorise actionable Shining mode.";
        }

        var supportedProjectCap = GetSupportedProjectCap(radianceTier);
        if (supportedProjects > supportedProjectCap)
            return $"Количество supported completed Shining projects ({supportedProjects}) превышает Radiance cap ({supportedProjectCap}) и не может authorise actionable Shining mode.";

        if (root["shiningPoliticalActors"] is not JsonArray actors)
            return "shiningPoliticalActors повреждён и не может authorise actionable Shining mode.";

        var actorIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var actorNode in actors)
        {
            if (actorNode is not JsonObject actor)
                return "shiningPoliticalActors содержит повреждённую запись и не может authorise actionable Shining mode.";

            var actorId = GetNodeString(actor["actorId"]);
            if (string.IsNullOrWhiteSpace(actorId) || !actorIds.Add(actorId))
                return "shiningPoliticalActors содержит missing или duplicate actorId и не может authorise actionable Shining mode.";

            var actorType = GetNodeString(actor["actorType"]);
            if (!string.IsNullOrWhiteSpace(actorType) &&
                !string.Equals(actorType, HeadActorTypeRadiantActor, StringComparison.OrdinalIgnoreCase))
            {
                return "shiningPoliticalActors содержит неподдерживаемый actorType и не может authorise actionable Shining mode.";
            }

            if (!IsSupportedPoliticalStatus(GetNodeString(actor["politicalStatus"])))
                return "shiningPoliticalActors содержит неподдерживаемый politicalStatus и не может authorise actionable Shining mode.";
        }

        return null;
    }

    private static void NormalizeOptionalStringProperty(JsonObject obj, string propertyName)
    {
        if (obj[propertyName] is JsonValue value && value.TryGetValue<string>(out var text))
            obj[propertyName] = text?.Trim() ?? string.Empty;
    }

    private static void NormalizeCanonicalTokenIfSupported(JsonObject obj, string propertyName, Func<string?, bool> predicate)
    {
        if (obj[propertyName] is not JsonValue value || !value.TryGetValue<string>(out var text))
            return;

        var trimmed = text?.Trim();
        if (predicate(trimmed))
            obj[propertyName] = trimmed;
    }

    private static void NormalizeReceiptArray(JsonObject root, JsonObject? residentRoot, string propertyName)
    {
        var receipts = EnsureArray(root, propertyName);
        for (var i = receipts.Count - 1; i >= 0; i--)
        {
            if (receipts[i] is not JsonObject receipt)
            {
                receipts.RemoveAt(i);
                continue;
            }

            receipt["requestId"] = GetNodeString(receipt["requestId"]) ?? string.Empty;
            receipt["resolvedAtTurn"] = Math.Max(0, GetNodeInt(receipt["resolvedAtTurn"], 0));
            receipt["resolvedAtUtc"] = GetNodeString(receipt["resolvedAtUtc"]) ?? string.Empty;
            receipt["reason"] = GetNodeString(receipt["reason"]) ?? string.Empty;

            if (string.Equals(propertyName, "coreActionReceipts", StringComparison.OrdinalIgnoreCase))
            {
                TryHydratePreparedPackageReceiptSnapshot(root, receipt);
                TryHydrateCoreReceiptSnapshot(root, receipt);
            }
            else if (string.Equals(propertyName, "factionFoundingReceipts", StringComparison.OrdinalIgnoreCase))
            {
                TryHydrateFoundingReceiptSnapshot(root, receipt);
            }
            else if (string.Equals(propertyName, "factionRealignmentReceipts", StringComparison.OrdinalIgnoreCase))
            {
                TryHydrateRealignmentReceiptSnapshot(root, residentRoot, receipt);
            }
        }
    }

    private static void TryHydrateFoundingReceiptSnapshot(JsonObject root, JsonObject receipt)
    {
        _ = root;
        var hallId = GetNodeString(receipt["hallId"]) ?? GetNodeString(receipt["proposedHallId"]) ?? string.Empty;
        receipt["hallName"] = GetNodeString(receipt["hallName"]) ?? hallId;
        receipt["hallDescription"] = GetNodeString(receipt["hallDescription"]) ?? string.Empty;
        if (receipt["hallServiceTags"] is not JsonArray)
            receipt["hallServiceTags"] = new JsonArray();

        var factionId = GetNodeString(receipt["factionId"]) ?? GetNodeString(receipt["proposedFactionId"]) ?? string.Empty;
        receipt["factionName"] = GetNodeString(receipt["factionName"]) ?? factionId;
        receipt["charterSummary"] = GetNodeString(receipt["charterSummary"]) ?? string.Empty;
        receipt["favoredArchetype"] = GetNodeString(receipt["favoredArchetype"]) ?? string.Empty;
        receipt["patronEffectFamily"] = GetNodeString(receipt["patronEffectFamily"]) ?? string.Empty;
    }

    private static void TryHydrateCoreReceiptSnapshot(JsonObject root, JsonObject receipt)
    {
        _ = root;
        var actionType = GetNodeString(receipt["actionType"]);
        if (string.IsNullOrWhiteSpace(actionType))
            return;

        var factionId = GetNodeString(receipt["factionId"]) ?? GetNodeString(receipt["resolvedFactionId"]) ?? string.Empty;
        receipt["factionName"] = GetNodeString(receipt["factionName"]) ?? factionId;

        if (string.Equals(actionType, ShiningCoreActionRequestState.ActionTypeDiscoverNativeFaction, StringComparison.OrdinalIgnoreCase))
        {
            receipt["hallName"] = GetNodeString(receipt["hallName"]) ?? GetNodeString(receipt["hallId"]) ?? string.Empty;

            return;
        }

        if (!string.Equals(actionType, ShiningCoreActionRequestState.ActionTypeCompleteProject, StringComparison.OrdinalIgnoreCase) &&
            !string.Equals(actionType, ShiningCoreActionRequestState.ActionTypeSupportProject, StringComparison.OrdinalIgnoreCase) &&
            !string.Equals(actionType, ShiningCoreActionRequestState.ActionTypeUnsupportProject, StringComparison.OrdinalIgnoreCase) &&
            !string.Equals(actionType, ShiningCoreActionRequestState.ActionTypeRetireProject, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        var projectId = GetNodeString(receipt["projectId"]) ?? string.Empty;
        receipt["projectName"] = GetNodeString(receipt["projectName"]) ?? projectId;
    }

    private static void TryHydrateRealignmentReceiptSnapshot(JsonObject root, JsonObject? residentRoot, JsonObject receipt)
    {
        _ = root;
        var residentId = GetNodeString(receipt["residentId"]) ?? string.Empty;
        receipt["residentName"] = GetNodeString(receipt["residentName"]) ?? residentId;

        var sourceFactionId = GetNodeString(receipt["sourceFactionId"]) ?? string.Empty;
        receipt["sourceFactionName"] = GetNodeString(receipt["sourceFactionName"]) ?? sourceFactionId;

        var targetFactionId = GetNodeString(receipt["targetFactionId"]) ?? string.Empty;
        receipt["targetFactionName"] = GetNodeString(receipt["targetFactionName"]) ??
                                       (string.IsNullOrWhiteSpace(targetFactionId) ? string.Empty : targetFactionId);

        _ = residentRoot;
    }

    private static JsonObject? FindResidentHistoryEntry(JsonObject? residentRoot, string? historyEntryId)
    {
        if (residentRoot?["entries"] is not JsonArray residents || string.IsNullOrWhiteSpace(historyEntryId))
            return null;

        foreach (var resident in residents.OfType<JsonObject>())
        {
            if (resident["historyLog"] is not JsonArray historyLog)
                continue;

            var match = historyLog.OfType<JsonObject>()
                .FirstOrDefault(entry => string.Equals(GetNodeString(entry["entryId"]), historyEntryId, StringComparison.OrdinalIgnoreCase));
            if (match != null)
                return match;
        }

        return null;
    }

    private static void HydrateTradeInventoryReceiptSnapshots(JsonObject faction)
    {
        if (faction["tradeInventoryReceipts"] is not JsonArray receipts)
            return;

        foreach (var receipt in receipts.OfType<JsonObject>())
        {
            if (string.IsNullOrWhiteSpace(GetNodeString(receipt["factionName"])))
                receipt["factionName"] = GetNodeString(faction["charter"]?["factionName"]) ?? GetNodeString(faction["factionId"]) ?? string.Empty;

            if (!receipt.ContainsKey("soldOutCount"))
                receipt["soldOutCount"] = null;
        }
    }

    private static void HydrateLeadershipReceiptSnapshots(JsonObject root, JsonObject? residentRoot, JsonObject? guardiansRoot)
    {
        _ = residentRoot;
        _ = guardiansRoot;
        foreach (var faction in EnsureFactionsArray(root).OfType<JsonObject>())
        {
            var stableFactionId = GetNodeString(faction["factionId"]) ?? string.Empty;
            if (faction["leadershipReceipts"] is not JsonArray receipts)
                continue;

            foreach (var receipt in receipts.OfType<JsonObject>())
            {
                receipt["requestId"] = GetNodeString(receipt["requestId"]) ?? string.Empty;
                receipt["transitionMode"] = GetNodeString(receipt["transitionMode"]) ?? string.Empty;
                receipt["previousHeadActorType"] = GetNodeString(receipt["previousHeadActorType"]);
                receipt["previousHeadActorId"] = GetNodeString(receipt["previousHeadActorId"]);
                receipt["newHeadActorType"] = GetNodeString(receipt["newHeadActorType"]);
                receipt["newHeadActorId"] = GetNodeString(receipt["newHeadActorId"]);
                receipt["status"] = GetNodeString(receipt["status"]) ?? string.Empty;
                receipt["resolvedAtTurn"] = Math.Max(0, GetNodeInt(receipt["resolvedAtTurn"], 0));
                receipt["resolvedAtUtc"] = GetNodeString(receipt["resolvedAtUtc"]) ?? string.Empty;
                receipt["reason"] = GetNodeString(receipt["reason"]) ?? string.Empty;

                if (string.IsNullOrWhiteSpace(GetNodeString(receipt["factionName"])))
                    receipt["factionName"] = stableFactionId;

                if (string.IsNullOrWhiteSpace(GetNodeString(receipt["previousHeadLabel"])))
                {
                    receipt["previousHeadLabel"] = BuildHeadActorStableFallbackLabel(
                        GetNodeString(receipt["previousHeadActorType"]),
                        GetNodeString(receipt["previousHeadActorId"]));
                }

                if (string.IsNullOrWhiteSpace(GetNodeString(receipt["newHeadLabel"])))
                {
                    receipt["newHeadLabel"] = BuildHeadActorStableFallbackLabel(
                        GetNodeString(receipt["newHeadActorType"]),
                        GetNodeString(receipt["newHeadActorId"]));
                }
            }
        }
    }

    private static void TryHydratePreparedPackageReceiptSnapshot(JsonObject root, JsonObject receipt)
    {
        if (!string.Equals(GetNodeString(receipt["actionType"]), ShiningCoreActionRequestState.ActionTypePrepareIncarnationPackage, StringComparison.OrdinalIgnoreCase))
            return;

        if (receipt["selectedCards"] is JsonArray existingSelectedCards)
        {
            foreach (var card in existingSelectedCards.OfType<JsonObject>())
                NormalizeBlessingCardObject(card);
            return;
        }

        if (root["preparedIncarnationPackage"] is not JsonObject preparedPackage ||
            preparedPackage["selectedCards"] is not JsonArray preparedSelectedCards ||
            preparedSelectedCards.Count == 0 ||
            preparedPackage["selectedCardIds"] is not JsonArray preparedSelectedIds ||
            receipt["selectedCardIds"] is not JsonArray receiptSelectedIds)
        {
            return;
        }

        var receiptIds = receiptSelectedIds.OfType<JsonValue>()
            .Select(node => node.TryGetValue<string>(out var value) ? value?.Trim() ?? string.Empty : string.Empty)
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .ToList();
        var preparedIds = preparedSelectedIds.OfType<JsonValue>()
            .Select(node => node.TryGetValue<string>(out var value) ? value?.Trim() ?? string.Empty : string.Empty)
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .ToList();

        if (receiptIds.Count == 0 ||
            receiptIds.Count != preparedIds.Count ||
            !receiptIds.SequenceEqual(preparedIds, StringComparer.OrdinalIgnoreCase))
        {
            return;
        }

        var generatedDraftVersion = GetNodeInt(receipt["generatedDraftVersion"], -1);
        if (generatedDraftVersion >= 0 &&
            generatedDraftVersion != GetNodeInt(preparedPackage["generatedFromDraftVersion"], generatedDraftVersion))
        {
            return;
        }

        var snapshot = new JsonArray();
        foreach (var card in preparedSelectedCards.OfType<JsonObject>())
            snapshot.Add(CloneCardForPersistence(card));
        receipt["selectedCards"] = snapshot;
    }

    private static JsonObject? FindResident(JsonObject? residentRoot, string? residentId)
    {
        if (residentRoot?["entries"] is not JsonArray entries || string.IsNullOrWhiteSpace(residentId))
            return null;

        return entries.OfType<JsonObject>()
            .FirstOrDefault(entry => string.Equals(GetNodeString(entry["residentId"]), residentId, StringComparison.OrdinalIgnoreCase));
    }

    private static JsonObject? FindGuardian(JsonObject? guardiansRoot, string? guardianId)
    {
        if (guardiansRoot?["guardians"] is not JsonArray guardians || string.IsNullOrWhiteSpace(guardianId))
            return null;

        return guardians.OfType<JsonObject>()
            .FirstOrDefault(guardian => string.Equals(GetNodeString(guardian["guardianId"]), guardianId, StringComparison.OrdinalIgnoreCase));
    }

    private static string ResolveFactionSnapshotName(JsonObject root, string? factionId)
    {
        if (string.IsNullOrWhiteSpace(factionId))
            return string.Empty;

        var faction = FindFaction(root, factionId);
        return GetNodeString(faction?["charter"]?["factionName"]) ?? factionId;
    }

    private static string BuildHeadActorStableFallbackLabel(string? headActorType, string? headActorId)
    {
        if (string.IsNullOrWhiteSpace(headActorType) ||
            string.Equals(headActorType, "vacant", StringComparison.OrdinalIgnoreCase) ||
            string.IsNullOrWhiteSpace(headActorId) ||
            string.Equals(headActorId, "vacant", StringComparison.OrdinalIgnoreCase))
        {
            return "вакантно";
        }

        if (string.Equals(headActorType, HeadActorTypeResident, StringComparison.OrdinalIgnoreCase))
            return $"резидент {headActorId}";
        if (string.Equals(headActorType, HeadActorTypeGuardian, StringComparison.OrdinalIgnoreCase))
            return $"хранитель {headActorId}";
        if (string.Equals(headActorType, HeadActorTypePlayerSoul, StringComparison.OrdinalIgnoreCase))
            return "душа игрока";
        if (string.Equals(headActorType, HeadActorTypeRadiantActor, StringComparison.OrdinalIgnoreCase))
            return $"светозарный актор {headActorId}";

        return $"{headActorType}:{headActorId}";
    }

    private static string BuildHeadActorSnapshotLabel(
        JsonObject root,
        JsonObject? residentRoot,
        JsonObject? guardiansRoot,
        string? headActorType,
        string? headActorId)
    {
        if (string.IsNullOrWhiteSpace(headActorType) ||
            string.Equals(headActorType, "vacant", StringComparison.OrdinalIgnoreCase) ||
            string.IsNullOrWhiteSpace(headActorId) ||
            string.Equals(headActorId, "vacant", StringComparison.OrdinalIgnoreCase))
        {
            return "вакантно";
        }

        if (string.Equals(headActorType, HeadActorTypeResident, StringComparison.OrdinalIgnoreCase))
        {
            var resident = FindResident(residentRoot, headActorId);
            var residentName = GetNodeString(resident?["displayName"]) ?? GetNodeString(resident?["residentName"]) ?? headActorId;
            return $"резидент {residentName}";
        }

        if (string.Equals(headActorType, HeadActorTypeGuardian, StringComparison.OrdinalIgnoreCase))
        {
            var guardian = FindGuardian(guardiansRoot, headActorId);
            var guardianName = GetNodeString(guardian?["canonicalName"]) ??
                               GetNodeString(guardian?["manifestation"]?["currentDisplayName"]) ??
                               headActorId;
            var guardianLabel = string.Equals(GetNodeString(guardian?["originType"]), OriginTypePlayerFounded, StringComparison.OrdinalIgnoreCase)
                ? "основанный хранитель"
                : "хранитель";
            return $"{guardianLabel} {guardianName}";
        }

        if (string.Equals(headActorType, HeadActorTypePlayerSoul, StringComparison.OrdinalIgnoreCase))
            return "душа игрока";

        if (string.Equals(headActorType, HeadActorTypeRadiantActor, StringComparison.OrdinalIgnoreCase))
        {
            var actorName = EnsurePoliticalActorsArray(root).OfType<JsonObject>()
                .FirstOrDefault(actor => string.Equals(GetNodeString(actor["actorId"]), headActorId, StringComparison.OrdinalIgnoreCase))?["displayName"]?
                .GetValue<string>();
            return string.IsNullOrWhiteSpace(actorName)
                ? $"светозарный актор {headActorId}"
                : $"светозарный актор {actorName}";
        }

        return $"{headActorType}:{headActorId}";
    }

    private static void NormalizeTradeInventoryObject(JsonObject tradeInventory)
    {
        tradeInventory["tradeCycleId"] = GetNodeString(tradeInventory["tradeCycleId"]) ?? string.Empty;
        tradeInventory["generatedAtUtc"] = GetNodeString(tradeInventory["generatedAtUtc"]) ?? string.Empty;
        tradeInventory["generationTradeTier"] = Math.Max(0, GetNodeInt(tradeInventory["generationTradeTier"], 0));
        var rarityCeiling = GetNodeString(tradeInventory["generationRarityCeiling"]);
        tradeInventory["generationRarityCeiling"] = IsSupportedTradeInventoryRarityCeiling(rarityCeiling)
            ? rarityCeiling
            : "none";
        tradeInventory["serviceMultiplierSnapshot"] = TryReadDouble(tradeInventory["serviceMultiplierSnapshot"], out var multiplier)
            ? Math.Max(0.0, multiplier)
            : 0.0;
        tradeInventory["merchantProfile"] = GetNodeString(tradeInventory["merchantProfile"]) ?? ShiningTradeRequestState.MerchantProfileShiningFaction;

        var items = EnsureArray(tradeInventory, "items");
        for (var i = items.Count - 1; i >= 0; i--)
        {
            if (items[i] is not JsonObject item)
            {
                items.RemoveAt(i);
                continue;
            }

            item["slotId"] = GetNodeString(item["slotId"]) ?? string.Empty;
            item["priceInFeathers"] = Math.Max(0, GetNodeInt(item["priceInFeathers"], 0));
            item["soldOut"] = GetNodeBool(item["soldOut"]);
            if (item["relicData"] is not JsonObject)
            {
                items.RemoveAt(i);
                continue;
            }
        }
    }

    internal static int ComputeFactionStrength(
        JsonObject faction,
        JsonObject? residentRoot,
        int radianceTier)
    {
        if (IsFactionDefeated(faction))
            return 0;

        var baseStrength = ResolveCanonicalBaseStrength(faction, radianceTier);
        var residentCount = CountAscendedResidentsForFaction(residentRoot, GetNodeString(faction["factionId"]));
        var residentBonus = Math.Min(15, residentCount * 3);
        var completedProjectBonus = 0;
        if (faction["projects"] is JsonArray projects)
        {
            foreach (var project in projects.OfType<JsonObject>())
            {
                if (string.Equals(GetNodeString(project["status"]), ProjectStatusCompleted, StringComparison.OrdinalIgnoreCase))
                {
                    var tier = Math.Clamp(
                        GetNodeInt(project["tier"], 1),
                        1,
                        3);
                    completedProjectBonus +=
                        ResolveProjectStrengthReward(tier);
                }
            }
        }

        var investmentBonus = 8 * Math.Clamp(GetNodeInt(faction["investCountThisAscension"], 0), 0, 3);
        var lifecyclePenalty = IsFactionLeaderless(faction) ? 15 : 0;
        return Math.Clamp(baseStrength + residentBonus + completedProjectBonus + investmentBonus - lifecyclePenalty, 0, GetFactionStrengthCap(radianceTier));
    }

    private static int ResolveCanonicalBaseStrength(JsonObject faction, int radianceTier)
    {
        var existing = Math.Max(0, GetNodeInt(faction["baseStrength"], 0));
        if (existing > 0)
            return existing;

        return NormalizeOriginType(GetNodeString(faction["originType"])) switch
        {
            OriginTypeNativeRadiant => Math.Min(70, 55 + (5 * Math.Max(0, radianceTier - 1))),
            OriginTypePlayerFounded => 35,
            _ => 35
        };
    }

    private static int ResolveProjectStrengthReward(int tier)
    {
        return Math.Clamp(tier, 1, 3) switch
        {
            1 => 8,
            2 => 12,
            _ => 16
        };
    }

    private static int CountSupportedProjects(JsonObject? faction)
    {
        if (faction?["projects"] is not JsonArray projects)
            return 0;

        return projects.OfType<JsonObject>().Count(project =>
            string.Equals(GetNodeString(project["status"]), ProjectStatusCompleted, StringComparison.OrdinalIgnoreCase) &&
            GetNodeBool(project["isSupported"]));
    }

    private static string DeriveDefaultResidentRole(JsonObject resident)
    {
        var hints = new List<string>();
        if (resident["mortalWorldImprint"] is JsonObject imprint)
        {
            if (imprint["archetypeHints"] is JsonArray archetypeHints)
                hints.AddRange(archetypeHints.OfType<JsonValue>().Select(node => node.TryGetValue<string>(out var value) ? value ?? string.Empty : string.Empty));
            if (imprint["coreTraits"] is JsonArray coreTraits)
                hints.AddRange(coreTraits.OfType<JsonValue>().Select(node => node.TryGetValue<string>(out var value) ? value ?? string.Empty : string.Empty));
        }

        var normalizedHints = string.Join(" ", hints).ToLowerInvariant();
        if (normalizedHints.Contains("forge") || normalizedHints.Contains("smith"))
            return ResidentRoleForgeSupport;
        if (normalizedHints.Contains("memory") || normalizedHints.Contains("archive"))
            return ResidentRoleArchiveSupport;
        if (normalizedHints.Contains("resource") || normalizedHints.Contains("merchant"))
            return ResidentRoleResourceSupport;
        if (normalizedHints.Contains("road") || normalizedHints.Contains("passage"))
            return ResidentRoleDescentSupport;
        return ResidentRoleSocialSupport;
    }

    private static string ResolveDefaultOutputFamily(string? projectArchetype)
    {
        return NormalizeProjectArchetype(projectArchetype) switch
        {
            ProjectArchetypeRevelation => EffectFamilyLore,
            ProjectArchetypeAccord => EffectFamilySocial,
            ProjectArchetypeProvision => EffectFamilyResource,
            ProjectArchetypeRemembrance => EffectFamilyMemory,
            ProjectArchetypeRefinement => EffectFamilyRelic,
            ProjectArchetypePassage => EffectFamilyDescent,
            ProjectArchetypeWarding => EffectFamilySurvival,
            ProjectArchetypeSubversion => EffectFamilyMemory,
            _ => EffectFamilySocial
        };
    }

    private static string NormalizeAscensionState(string? value)
    {
        return string.Equals((value ?? string.Empty).Trim(), AscensionStateAscended, StringComparison.OrdinalIgnoreCase)
            ? AscensionStateAscended
            : AscensionStateRemainedInChaosSea;
    }

    private static string NormalizeOriginType(string? value)
    {
        var normalized = (value ?? string.Empty).Trim();
        return IsSupportedOriginType(normalized) ? normalized : OriginTypeAscendedGuardian;
    }

    private static string NormalizeProjectArchetype(string? value)
    {
        var normalized = (value ?? string.Empty).Trim();
        return IsSupportedProjectArchetype(normalized) ? normalized : ProjectArchetypeAccord;
    }

    private static JsonArray EnsureArray(JsonObject root, string propertyName)
    {
        if (root[propertyName] is JsonArray array)
            return array;

        array = new JsonArray();
        root[propertyName] = array;
        return array;
    }

    private static JsonObject CloneObject(JsonObject source) => JsonNode.Parse(source.ToJsonString())!.AsObject();

    private static string? GetNodeString(JsonNode? node)
    {
        if (node == null)
            return null;

        try
        {
            return node.GetValue<string>();
        }
        catch
        {
            return node.ToJsonString().Trim('"');
        }
    }

    private static int GetNodeInt(JsonNode? node, int defaultValue)
    {
        if (node == null)
            return defaultValue;

        try
        {
            return node.GetValue<int>();
        }
        catch
        {
            return defaultValue;
        }
    }

    private static bool GetNodeBool(JsonNode? node)
    {
        if (node == null)
            return false;

        try
        {
            return node.GetValue<bool>();
        }
        catch
        {
            return false;
        }
    }

    private static bool TryReadDouble(JsonNode? node, out double value)
    {
        value = 0;
        if (node == null)
            return false;

        try
        {
            value = node.GetValue<double>();
            return true;
        }
        catch
        {
            return double.TryParse(node.ToString(), out value);
        }
    }

    private static void NormalizeStringArrayInPlace(JsonArray array)
    {
        var values = array
            .OfType<JsonValue>()
            .Select(node => node.TryGetValue<string>(out var value) ? value?.Trim() ?? string.Empty : string.Empty)
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .ToList();

        array.Clear();
        foreach (var value in values)
            array.Add(value);
    }

    private static void DeduplicateStringArrayInPlace(JsonArray array)
    {
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        for (var i = array.Count - 1; i >= 0; i--)
        {
            if (array[i] is not JsonValue node || !node.TryGetValue<string>(out var value) || string.IsNullOrWhiteSpace(value) || !seen.Add(value))
                array.RemoveAt(i);
        }
    }

    private static void RemoveUnsupportedItems(JsonArray array, HashSet<string> allowList)
    {
        for (var i = array.Count - 1; i >= 0; i--)
        {
            if (array[i] is not JsonValue node || !node.TryGetValue<string>(out var value) || string.IsNullOrWhiteSpace(value) || !allowList.Contains(value))
                array.RemoveAt(i);
        }
    }

    private static void TrimStringArrayToSet(JsonArray array, HashSet<string> allowSet)
    {
        for (var i = array.Count - 1; i >= 0; i--)
        {
            if (array[i] is not JsonValue node || !node.TryGetValue<string>(out var value) || string.IsNullOrWhiteSpace(value) || !allowSet.Contains(value))
                array.RemoveAt(i);
        }
    }
}
