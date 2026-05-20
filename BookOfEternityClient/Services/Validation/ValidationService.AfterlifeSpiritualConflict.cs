using System.Text.Json;
using System.Text.Json.Nodes;

namespace BookOfEternityClient.Services;

public partial class ValidationService
{
    private static readonly HashSet<string> AfterlifeControlSourceOperations = new(StringComparer.OrdinalIgnoreCase)
    {
        "binding",
        "force_binding",
        "force_incarnation",
        "break_binding",
        "incarnation_resistance",
        "counter",
        "guard",
        "repair"
    };

    private static readonly IReadOnlyDictionary<string, ActionCostDefinition> AfterlifeActionCosts =
        new Dictionary<string, ActionCostDefinition>(StringComparer.OrdinalIgnoreCase)
        {
            ["pressure"] = new(3, 1),
            ["guard"] = new(2, 1),
            ["counter"] = new(4, 2),
            ["maneuver"] = new(3, 1),
            ["binding"] = new(4, 2),
            ["force_binding"] = new(5, 2),
            ["break_binding"] = new(3, 1),
            ["incarnation_resistance"] = new(3, 1),
            ["champion_coordination"] = new(2, 1),
            ["recover_spiritual_power"] = new(0, 0)
        };

    private sealed record ActionCostDefinition(int BaseCost, int MinCost);

    private async Task ValidateAfterlifeSpiritualConflictStateAsync(List<ValidationIssue> issues)
    {
        var json = await _fs.ReadFileAsync(AfterlifeSpiritualConflictState.StatePath);
        if (string.IsNullOrWhiteSpace(json))
        {
            if (_fs.FileExists(AfterlifeSpiritualConflictState.StatePath))
            {
                issues.Add(new ValidationIssue(
                    AfterlifeSpiritualConflictState.StatePath,
                    IssueSeverity.Error,
                    "afterlife_spiritual_conflict_state.json существует, но пуст.",
                    code: "afterlife_conflict_state_empty",
                    section: "AfterlifeSpiritualConflict",
                    expected: "JSON object with schemaVersion, activeConflict, recentConflicts",
                    actual: "empty/whitespace",
                    repairHint: "Восстанови canonical conflict root: { schemaVersion: 1, activeConflict: null, recentConflicts: [] }."));
            }

            await ValidateActiveConflictRemovalHasTerminalProofAsync(null, issues);
            return;
        }

        JsonObject root;
        try
        {
            root = JsonNode.Parse(json) as JsonObject
                   ?? throw new JsonException("Root is not object.");
        }
        catch
        {
            issues.Add(new ValidationIssue(
                AfterlifeSpiritualConflictState.StatePath,
                IssueSeverity.Error,
                "afterlife_spiritual_conflict_state.json должен быть валидным JSON object.",
                code: "afterlife_conflict_state_invalid_json",
                section: "AfterlifeSpiritualConflict",
                expected: "JSON object",
                actual: "unreadable/non-object"));
            await ValidateActiveConflictRemovalHasTerminalProofAsync(null, issues);
            return;
        }

        var gateContext = await ResolveAfterlifeSpiritualConflictGateContextAsync();
        var diceContext = await ResolveAfterlifeConflictDiceContextAsync(gateContext.Manifest);
        var actionCostAuthority = await ResolveAfterlifeActionCostAuthorityContextAsync(gateContext.Manifest);
        var rewardContext = await ResolveAfterlifeConflictRewardContextAsync(gateContext);
        var soulDissipationContext = await ResolveAfterlifeSoulDissipationContextAsync(gateContext.Manifest);
        await ValidateActiveConflictRemovalHasTerminalProofAsync(root, issues);
        ValidateAfterlifeSpiritualConflictRoot(root, AfterlifeSpiritualConflictState.StatePath, issues, diceContext, actionCostAuthority, rewardContext, soulDissipationContext);
        ValidateAfterlifeConflictRewardStateDeltas(rewardContext, issues);

        if (root["activeConflict"] is JsonObject activeConflict)
        {
            var gateRealmKey = AfterlifeSpiritualConflictState.NormalizeAfterlifeRealmKey(gateContext.Realm);
            if (gateRealmKey == null)
            {
                issues.Add(new ValidationIssue(
                    $"{AfterlifeSpiritualConflictState.StatePath}.activeConflict",
                    IssueSeverity.Error,
                    "Активный afterlife spiritual conflict допустим только в Chaos Sea или Shining Abode.",
                    code: "afterlife_conflict_active_wrong_realm",
                    section: "AfterlifeSpiritualConflict",
                    expected: gateContext.UsesValidatedSnapshot
                        ? "validated pre-turn soul_state.currentRealm = Chaos Sea or Shining Abode"
                        : "soul_state.currentRealm = Chaos Sea or Shining Abode",
                    actual: string.IsNullOrWhiteSpace(gateContext.Realm) ? "missing/empty" : gateContext.Realm,
                    repairHint: "Не переносите activeConflict в Mortal World. Сначала resolve/repair_cancel конфликт в afterlife или восстанови currentRealm."));
            }
            else
            {
                var activeRealm = AfterlifeSpiritualConflictState.GetNodeString(activeConflict["realm"]);
                var activeRealmKey = AfterlifeSpiritualConflictState.NormalizeAfterlifeRealmKey(activeRealm);
                if (activeRealmKey != null &&
                    !string.Equals(activeRealmKey, gateRealmKey, StringComparison.Ordinal))
                {
                    issues.Add(new ValidationIssue(
                        $"{AfterlifeSpiritualConflictState.StatePath}.activeConflict",
                        IssueSeverity.Error,
                        "activeConflict.realm должен совпадать с authority realm души.",
                        code: "afterlife_conflict_active_realm_mismatch",
                        section: "AfterlifeSpiritualConflict",
                        expected: gateContext.UsesValidatedSnapshot
                            ? $"activeConflict.realm normalized to validated pre-turn realm {gateRealmKey}"
                            : $"activeConflict.realm normalized to current realm {gateRealmKey}",
                        actual: string.IsNullOrWhiteSpace(activeRealm) ? "missing/empty" : activeRealm,
                        repairHint: "Не продвигайте конфликт из другого afterlife realm. Resolve/repair_cancel старый конфликт или восстанови authority realm/activeConflict.realm до одного realm."));
                }

                if (string.Equals(gateRealmKey, "shining_abode", StringComparison.Ordinal))
                {
                    var availability = await TryReadShiningAvailabilityForConflictGateAsync(gateContext);
                    if (!string.Equals(availability, ShiningAbodeState.AvailabilityActive, StringComparison.OrdinalIgnoreCase))
                    {
                        issues.Add(new ValidationIssue(
                            $"{AfterlifeSpiritualConflictState.StatePath}.activeConflict",
                            IssueSeverity.Error,
                            "Активный afterlife spiritual conflict допустим только в ordinary active Shining Abode.",
                            code: "afterlife_conflict_active_during_sealed_shining_abode",
                            section: "AfterlifeSpiritualConflict",
                            expected: gateContext.UsesValidatedSnapshot
                                ? "validated pre-turn shining_abode_state.availability = active"
                                : "shining_abode_state.availability = active",
                            actual: string.IsNullOrWhiteSpace(availability) ? "missing/empty" : availability,
                            repairHint: "Не запускай и не продвигай afterlife spiritual conflict, пока Сияющая Обитель sealed_until_next_ascension или иначе не active."));
                    }

                    var packageMode = await TryReadShiningPreparedPackageModeForConflictGateAsync(gateContext);
                    if (packageMode != ShiningAbodeState.PreparedIncarnationPackageMode.Absent)
                    {
                        issues.Add(new ValidationIssue(
                            $"{AfterlifeSpiritualConflictState.StatePath}.activeConflict",
                            IssueSeverity.Error,
                            "Активный afterlife spiritual conflict недопустим в Shining pending-bootstrap handoff или package-fault mode.",
                            code: "afterlife_conflict_active_during_shining_bootstrap",
                            section: "AfterlifeSpiritualConflict",
                            expected: gateContext.UsesValidatedSnapshot
                                ? "validated pre-turn ordinary active Shining Abode with preparedIncarnationPackage absent/null"
                                : "ordinary active Shining Abode with preparedIncarnationPackage absent/null",
                            actual: packageMode.ToString(),
                            repairHint: "В Shining pending-bootstrap handoff GM пишет только TriggerIncarnation и сохраняет preparedIncarnationPackage; не запускай и не продвигай afterlife spiritual conflict до завершения handoff/repair."));
                    }
                }
            }
        }
    }

    private sealed record AfterlifeSpiritualConflictGateContext(
        string? Realm,
        bool UsesValidatedSnapshot,
        ValidationPendingTurnSnapshotManifest? Manifest);

    private sealed record AfterlifeConflictDiceContext(
        int[]? AuthoritativeDice,
        int? LightIncarnateGrantTurn = null,
        IReadOnlyList<JsonObject>? PreTurnNoTurnDicePayloads = null,
        IReadOnlyList<JsonObject>? PreTurnConflictPayloads = null,
        string? PreTurnActiveConflictId = null,
        JsonNode? PreTurnActiveControlState = null,
        int? PreTurnPlayerActionCurrent = null,
        int? PreTurnOppositionActionCurrent = null,
        bool HasValidatedTurnBaseline = false,
        int SpiritFocusTier = 0,
        int SpiritFocusMaxActionPoints = 6,
        AfterlifeDifficultyDefinition? Difficulty = null)
    {
        public bool HasAuthoritativeDice => AuthoritativeDice is { Length: > 0 };
        public bool HasLightIncarnate => LightIncarnateGrantTurn is > 0;

        public bool IsPreTurnNoTurnDicePayload(JsonObject payload) =>
            PreTurnNoTurnDicePayloads?.Any(preTurnPayload => JsonNode.DeepEquals(preTurnPayload, payload)) == true;
    }

    private sealed record PreTurnActiveConflictControlContext(
        string? ConflictId,
        JsonNode? ControlState,
        int? PlayerActionCurrent,
        int? OppositionActionCurrent);

    private sealed record AfterlifeActionCostAuthorityContext(
        IReadOnlyDictionary<string, int> StandardArtTiers,
        IReadOnlyDictionary<string, JsonObject> PlayerSpecialArts,
        IReadOnlyDictionary<string, JsonObject> SpecialArtsByOwner,
        IReadOnlyDictionary<string, IReadOnlyDictionary<string, int>> EntityStandardArtTiers,
        IReadOnlyDictionary<string, IReadOnlyDictionary<string, int>> PreTurnConflictActorArtTierSnapshots);

    private sealed class PreTurnConflictPayloadTracker
    {
        private readonly IReadOnlyList<JsonObject> _payloads;
        private readonly bool[] _consumed;

        public PreTurnConflictPayloadTracker(IReadOnlyList<JsonObject>? payloads)
        {
            _payloads = payloads ?? Array.Empty<JsonObject>();
            _consumed = new bool[_payloads.Count];
        }

        public bool TryConsume(JsonObject payload)
        {
            for (var index = 0; index < _payloads.Count; index++)
            {
                if (_consumed[index])
                    continue;

                if (!JsonNode.DeepEquals(_payloads[index], payload))
                    continue;

                _consumed[index] = true;
                return true;
            }

            return false;
        }
    }

    private sealed class AfterlifeConflictRewardContext
    {
        public string? AuthorityRealmKey { get; init; }
        public bool UsesValidatedSnapshot { get; init; }
        public int? CurrentTurn { get; init; }
        public int? PreTurnInkFeathers { get; init; }
        public int? CurrentInkFeathers { get; init; }
        public int? PreTurnLightSparks { get; init; }
        public int? CurrentLightSparks { get; init; }
        public string? PreTurnActiveConflictId { get; init; }
        public string? PreTurnSideModel { get; init; }
        public string? PreTurnConflictPosition { get; init; }
        public int? PreTurnOpposingLeadStrength { get; init; }
        public AfterlifeDifficultyDefinition? Difficulty { get; init; }
        public int ExpectedCurrentTurnInkFeatherReward { get; set; }
        public int ExpectedCurrentTurnLightSparkReward { get; set; }
        public bool HasCurrentTurnInkFeatherRewardAudit { get; set; }
        public bool HasCurrentTurnLightSparkRewardAudit { get; set; }
    }

    private sealed record AfterlifeDifficultyDefinition(
        string Difficulty,
        string RussianLabel,
        int OppositionDiceModifier,
        int RewardMultiplierPercent);

    private sealed record AfterlifeSoulDissipationContext(
        JsonObject? CurrentSoulRoot,
        IReadOnlyDictionary<string, JsonObject> AuthorityProfiles);

    private async Task<AfterlifeSpiritualConflictGateContext> ResolveAfterlifeSpiritualConflictGateContextAsync()
    {
        var lookup = await LoadValidatedPendingTurnSnapshotLookupAsync();
        if (lookup.Status == ValidatedPendingTurnSnapshotStatus.Usable && lookup.Manifest != null)
        {
            return new AfterlifeSpiritualConflictGateContext(
                await TryReadValidatedPendingTurnSnapshotRealmAsync(lookup.Manifest),
                true,
                lookup.Manifest);
        }

        return new AfterlifeSpiritualConflictGateContext(
            await TryReadCurrentSoulRealmAsync(),
            false,
            null);
    }

    private async Task<AfterlifeConflictDiceContext> ResolveAfterlifeConflictDiceContextAsync(
        ValidationPendingTurnSnapshotManifest? manifest)
    {
        var lightIncarnateGrantTurn = await ResolveLightIncarnateGrantTurnAsync();
        var preTurnConflictPayloads = await ResolvePreTurnConflictPayloadsAsync(manifest);
        var preTurnActiveControl = await ResolvePreTurnActiveConflictControlContextAsync(manifest);
        var preTurnNoTurnDicePayloads = await ResolvePreTurnNoTurnConflictDicePayloadsAsync(manifest);
        var spiritFocusTier = await ResolveAfterlifeConflictSpiritFocusTierAsync(manifest);
        var spiritFocusMaxActionPoints = AfterlifeSpiritualConflictState.GetSpiritFocusMaxActionPoints(spiritFocusTier);
        var difficulty = await ResolveAfterlifeConflictDifficultyDefinitionAsync();

        if (manifest?.PreGeneratedDices1d20 is { Length: > 0 } manifestDice)
        {
            return new AfterlifeConflictDiceContext(
                manifestDice,
                lightIncarnateGrantTurn,
                preTurnNoTurnDicePayloads,
                preTurnConflictPayloads,
                preTurnActiveControl.ConflictId,
                preTurnActiveControl.ControlState,
                preTurnActiveControl.PlayerActionCurrent,
                preTurnActiveControl.OppositionActionCurrent,
                HasValidatedTurnBaseline: true,
                SpiritFocusTier: spiritFocusTier,
                SpiritFocusMaxActionPoints: spiritFocusMaxActionPoints,
                Difficulty: difficulty);
        }

        var liveRequestJson = await _fs.ReadFileAsync("input/turn_request.json");
        if (string.IsNullOrWhiteSpace(liveRequestJson))
        {
            return new AfterlifeConflictDiceContext(
                null,
                lightIncarnateGrantTurn,
                preTurnNoTurnDicePayloads,
                preTurnConflictPayloads,
                preTurnActiveControl.ConflictId,
                preTurnActiveControl.ControlState,
                preTurnActiveControl.PlayerActionCurrent,
                preTurnActiveControl.OppositionActionCurrent,
                HasValidatedTurnBaseline: manifest != null,
                SpiritFocusTier: spiritFocusTier,
                SpiritFocusMaxActionPoints: spiritFocusMaxActionPoints,
                Difficulty: difficulty);
        }

        try
        {
            if (JsonNode.Parse(liveRequestJson) is JsonObject root &&
                root["preGeneratedDices1d20"] is JsonArray diceArray)
            {
                var dice = new List<int>();
                foreach (var item in diceArray)
                {
                    if (TryGetJsonNodeInt(item, out var value))
                        dice.Add(value);
                }

                if (dice.Count > 0)
                {
                    return new AfterlifeConflictDiceContext(
                        dice.ToArray(),
                        lightIncarnateGrantTurn,
                        preTurnNoTurnDicePayloads,
                        preTurnConflictPayloads,
                        preTurnActiveControl.ConflictId,
                        preTurnActiveControl.ControlState,
                        preTurnActiveControl.PlayerActionCurrent,
                        preTurnActiveControl.OppositionActionCurrent,
                        HasValidatedTurnBaseline: manifest != null,
                        SpiritFocusTier: spiritFocusTier,
                        SpiritFocusMaxActionPoints: spiritFocusMaxActionPoints,
                        Difficulty: difficulty);
                }
            }
        }
        catch
        {
            // Other validators report malformed live turn requests; dice audit falls back to shape-only checks.
        }

        return new AfterlifeConflictDiceContext(
            null,
            lightIncarnateGrantTurn,
            preTurnNoTurnDicePayloads,
            preTurnConflictPayloads,
            preTurnActiveControl.ConflictId,
            preTurnActiveControl.ControlState,
            preTurnActiveControl.PlayerActionCurrent,
            preTurnActiveControl.OppositionActionCurrent,
            HasValidatedTurnBaseline: manifest != null,
            SpiritFocusTier: spiritFocusTier,
            SpiritFocusMaxActionPoints: spiritFocusMaxActionPoints,
            Difficulty: difficulty);
    }

    private async Task<int> ResolveAfterlifeConflictSpiritFocusTierAsync(ValidationPendingTurnSnapshotManifest? manifest)
    {
        const string soulStatePath = "game_state/meta/soul_state.json";
        var json = manifest == null
            ? await _fs.ReadFileAsync(soulStatePath)
            : await ReadValidatedPendingTurnSnapshotFileAsync(manifest, soulStatePath);

        if (string.IsNullOrWhiteSpace(json))
            return 0;

        try
        {
            return JsonNode.Parse(json) is JsonObject soulRoot
                ? AfterlifeSpiritualConflictState.ResolveSpiritFocusTier(soulRoot)
                : 0;
        }
        catch
        {
            return 0;
        }
    }

    private async Task<AfterlifeActionCostAuthorityContext> ResolveAfterlifeActionCostAuthorityContextAsync(
        ValidationPendingTurnSnapshotManifest? manifest)
    {
        const string soulStatePath = "game_state/meta/soul_state.json";
        var soulJson = manifest == null
            ? await _fs.ReadFileAsync(soulStatePath)
            : await ReadValidatedPendingTurnSnapshotFileAsync(manifest, soulStatePath);
        var profileJson = manifest == null
            ? await _fs.ReadFileAsync(AfterlifeEntityProfileState.StatePath)
            : await ReadValidatedPendingTurnSnapshotFileAsync(manifest, AfterlifeEntityProfileState.StatePath);
        var conflictJson = manifest == null
            ? await _fs.ReadFileAsync(AfterlifeSpiritualConflictState.StatePath)
            : await ReadValidatedPendingTurnSnapshotFileAsync(manifest, AfterlifeSpiritualConflictState.StatePath);

        var profilesRoot = TryParseJsonObject(profileJson);
        return new AfterlifeActionCostAuthorityContext(
            ReadAfterlifeCombatProfileArtTiers(TryParseJsonObject(soulJson)),
            ReadPlayerSpecialArts(profilesRoot),
            ReadSpecialArtsByOwner(profilesRoot),
            ReadEntityStandardArtTiers(profilesRoot),
            ReadConflictActorArtTierSnapshots(TryParseJsonObject(conflictJson)));
    }

    private static IReadOnlyDictionary<string, int> ReadAfterlifeCombatProfileArtTiers(JsonObject? soulRoot)
    {
        var result = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        if (soulRoot?[AfterlifeSpiritualConflictState.SoulStateProfileProperty] is not JsonObject profile ||
            profile["artTiers"] is not JsonObject artTiers)
        {
            return result;
        }

        foreach (var property in artTiers)
        {
            if (TryGetJsonNodeInt(property.Value, out var tier))
                result[property.Key] = Math.Clamp(tier, 0, 5);
        }

        return result;
    }

    private static IReadOnlyDictionary<string, JsonObject> ReadPlayerSpecialArts(JsonObject? profilesRoot)
    {
        var result = new Dictionary<string, JsonObject>(StringComparer.OrdinalIgnoreCase);
        if (profilesRoot?[AfterlifeEntityProfileState.ProfilesProperty] is not JsonArray profiles)
            return result;

        foreach (var profile in profiles.OfType<JsonObject>())
        {
            var actorType = AfterlifeSpiritualConflictState.GetNodeString(profile["actorType"]);
            var actorId = AfterlifeSpiritualConflictState.GetNodeString(profile["actorId"]);
            if (!ConflictTokenEquals(actorType, "player_soul", "player", "soul") &&
                !ConflictTokenEquals(actorId, "player_soul", "player", "soul"))
            {
                continue;
            }

            if (profile["specialArts"] is not JsonArray specialArts)
                continue;

            foreach (var specialArt in specialArts.OfType<JsonObject>())
            {
                var artId = AfterlifeSpiritualConflictState.GetNodeString(specialArt["artId"]);
                if (!string.IsNullOrWhiteSpace(artId))
                    result[artId] = specialArt;
            }
        }

        return result;
    }

    private static IReadOnlyDictionary<string, JsonObject> ReadSpecialArtsByOwner(JsonObject? profilesRoot)
    {
        var result = new Dictionary<string, JsonObject>(StringComparer.OrdinalIgnoreCase);
        if (profilesRoot?[AfterlifeEntityProfileState.ProfilesProperty] is not JsonArray profiles)
            return result;

        foreach (var profile in profiles.OfType<JsonObject>())
        {
            var profileActorType = AfterlifeSpiritualConflictState.GetNodeString(profile["actorType"]);
            var profileActorId = AfterlifeSpiritualConflictState.GetNodeString(profile["actorId"]) ??
                                 AfterlifeSpiritualConflictState.GetNodeString(profile["actorRef"]);
            if (profile["specialArts"] is not JsonArray specialArts)
                continue;

            foreach (var specialArt in specialArts.OfType<JsonObject>())
            {
                var artId = AfterlifeSpiritualConflictState.GetNodeString(specialArt["artId"]);
                var ownerActorType = AfterlifeSpiritualConflictState.GetNodeString(specialArt["ownerActorType"]) ??
                                     profileActorType;
                var ownerActorId = AfterlifeSpiritualConflictState.GetNodeString(specialArt["ownerActorId"]) ??
                                   profileActorId;
                var key = BuildSpecialArtAuthorityKey(ownerActorType, ownerActorId, artId);
                if (key != null)
                    result[key] = specialArt;
            }
        }

        return result;
    }

    private static IReadOnlyDictionary<string, IReadOnlyDictionary<string, int>> ReadEntityStandardArtTiers(JsonObject? profilesRoot)
    {
        var result = new Dictionary<string, IReadOnlyDictionary<string, int>>(StringComparer.OrdinalIgnoreCase);
        if (profilesRoot?[AfterlifeEntityProfileState.ProfilesProperty] is not JsonArray profiles)
            return result;

        foreach (var profile in profiles.OfType<JsonObject>())
        {
            var key = AfterlifeEntityProfileState.BuildIdentityKey(profile);
            if (string.IsNullOrWhiteSpace(key))
                continue;

            var tiers = ReadStandardArtTierSnapshot(profile["standardArts"] as JsonObject);
            if (tiers.Count > 0)
                result[key] = tiers;
        }

        return result;
    }

    private static IReadOnlyDictionary<string, IReadOnlyDictionary<string, int>> ReadConflictActorArtTierSnapshots(JsonObject? conflictRoot)
    {
        var result = new Dictionary<string, IReadOnlyDictionary<string, int>>(StringComparer.OrdinalIgnoreCase);
        var active = conflictRoot?["activeConflict"] as JsonObject;
        if (active == null)
            return result;

        TryAddConflictSideArtTierSnapshot(result, active["playerSide"] as JsonObject);
        TryAddConflictSideArtTierSnapshot(result, active["oppositionSide"] as JsonObject);
        return result;
    }

    private static void TryAddConflictSideArtTierSnapshot(
        Dictionary<string, IReadOnlyDictionary<string, int>> result,
        JsonObject? side)
    {
        if (side?["leadContestant"] is not JsonObject lead)
            return;

        var key = BuildActorAuthorityKey(
            AfterlifeSpiritualConflictState.GetNodeString(lead["actorType"]),
            AfterlifeSpiritualConflictState.GetNodeString(lead["actorId"]) ??
            AfterlifeSpiritualConflictState.GetNodeString(lead["actorRef"]) ??
            AfterlifeSpiritualConflictState.GetNodeString(lead["id"]));
        if (key == null)
            return;

        var tiers = ReadStandardArtTierSnapshot(lead["actorArtTierSnapshot"] as JsonObject);
        if (tiers.Count > 0)
            result[key] = tiers;
    }

    private static Dictionary<string, int> ReadStandardArtTierSnapshot(JsonObject? standardArts)
    {
        var tiers = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        if (standardArts == null)
            return tiers;

        foreach (var property in standardArts)
        {
            if (!AfterlifeEntityProfileState.StandardArtIds.Contains(property.Key))
                continue;

            if (TryGetJsonNodeInt(property.Value, out var tier))
                tiers[property.Key] = Math.Clamp(tier, 0, AfterlifeEntityProfileState.MaxProfileTier);
        }

        return tiers;
    }

    private async Task<AfterlifeConflictRewardContext> ResolveAfterlifeConflictRewardContextAsync(
        AfterlifeSpiritualConflictGateContext gateContext)
    {
        var currentSoulRoot = await ReadJsonObjectAsync("game_state/meta/soul_state.json");
        var preTurnSoulRoot = TryParseJsonObject(await ReadValidatedCurrentPreTurnTrackedFileAsync("game_state/meta/soul_state.json"));
        var currentShiningRoot = await ReadJsonObjectAsync(ShiningAbodeState.StatePath);
        var preTurnShiningRoot = TryParseJsonObject(await ReadValidatedCurrentPreTurnTrackedFileAsync(ShiningAbodeState.StatePath));
        var preTurnConflictRoot = TryParseJsonObject(await ReadValidatedCurrentPreTurnTrackedFileAsync(AfterlifeSpiritualConflictState.StatePath));
        var preTurnActiveConflict = preTurnConflictRoot?["activeConflict"] as JsonObject;

        return new AfterlifeConflictRewardContext
        {
            AuthorityRealmKey = AfterlifeSpiritualConflictState.NormalizeAfterlifeRealmKey(gateContext.Realm),
            UsesValidatedSnapshot = gateContext.UsesValidatedSnapshot,
            CurrentTurn = gateContext.Manifest?.TurnNumber > 0 ? gateContext.Manifest.TurnNumber : null,
            PreTurnInkFeathers = preTurnSoulRoot == null ? null : ShiningAbodeState.GetSoulSpendableInkFeathers(preTurnSoulRoot),
            CurrentInkFeathers = currentSoulRoot == null ? null : ShiningAbodeState.GetSoulSpendableInkFeathers(currentSoulRoot),
            PreTurnLightSparks = preTurnShiningRoot == null ? null : AfterlifeSpiritualConflictState.GetNodeInt(preTurnShiningRoot["lightSparks"]),
            CurrentLightSparks = currentShiningRoot == null ? null : AfterlifeSpiritualConflictState.GetNodeInt(currentShiningRoot["lightSparks"]),
            PreTurnActiveConflictId = preTurnActiveConflict == null ? null : TryReadConflictId(preTurnActiveConflict),
            PreTurnSideModel = AfterlifeSpiritualConflictState.GetNodeString(preTurnActiveConflict?["sideModel"]),
            PreTurnConflictPosition = AfterlifeSpiritualConflictState.GetNodeString(preTurnActiveConflict?["conflictPosition"]),
            PreTurnOpposingLeadStrength = ResolveRewardOpposingLeadStrength(preTurnActiveConflict),
            Difficulty = await ResolveAfterlifeConflictDifficultyDefinitionAsync()
        };
    }

    private static int? ResolveRewardOpposingLeadStrength(JsonObject? activeConflict)
    {
        if (activeConflict?["oppositionSide"] is not JsonObject oppositionSide ||
            oppositionSide["leadContestant"] is not JsonObject lead ||
            lead["actorArtTierSnapshot"] is not JsonObject snapshot)
        {
            return null;
        }

        var hasTier = false;
        var maxTier = 0;
        foreach (var property in snapshot)
        {
            if (!AfterlifeEntityProfileState.StandardArtIds.Contains(property.Key))
                continue;

            if (!TryGetJsonNodeInt(property.Value, out var tier))
                continue;

            hasTier = true;
            maxTier = Math.Max(maxTier, Math.Max(0, tier));
        }

        return hasTier ? maxTier + 1 : null;
    }

    private async Task<AfterlifeDifficultyDefinition?> ResolveAfterlifeConflictDifficultyDefinitionAsync()
    {
        var settingsJson = await _fs.ReadFileAsync(AfterlifeSpiritualConflictState.DifficultySettingsPath);
        if (string.IsNullOrWhiteSpace(settingsJson))
            return null;

        try
        {
            if (JsonNode.Parse(settingsJson) is not JsonObject settingsRoot)
                return null;

            var difficulty = AfterlifeSpiritualConflictState.GetNodeString(settingsRoot["difficulty"]);
            if (string.IsNullOrWhiteSpace(difficulty))
            {
                if (TryGetJsonNodeBool(settingsRoot["impossibleMode"], out var impossibleMode) && impossibleMode)
                    difficulty = "impossible";
                else if (TryGetJsonNodeBool(settingsRoot["hardMode"], out var hardMode) && hardMode)
                    difficulty = "hard";
                else
                    difficulty = "normal";
            }

            return ResolveAfterlifeDifficultyDefinition(difficulty);
        }
        catch
        {
            return null;
        }
    }

    private static AfterlifeDifficultyDefinition ResolveAfterlifeDifficultyDefinition(string? difficulty)
    {
        var normalized = difficulty?.Trim().ToLowerInvariant();
        if (string.IsNullOrWhiteSpace(normalized) ||
            !AfterlifeSpiritualConflictState.DifficultyDefinitions.TryGetValue(normalized, out var definition))
        {
            definition = AfterlifeSpiritualConflictState.DifficultyDefinitions["normal"];
        }

        return new AfterlifeDifficultyDefinition(
            definition.Difficulty,
            definition.RussianLabel,
            definition.OppositionDiceModifier,
            definition.RewardMultiplierPercent);
    }

    private async Task<AfterlifeSoulDissipationContext> ResolveAfterlifeSoulDissipationContextAsync(
        ValidationPendingTurnSnapshotManifest? manifest)
    {
        var soulRoot = await ReadJsonObjectAsync("game_state/meta/soul_state.json");
        var profileRoot = manifest == null
            ? await ReadJsonObjectAsync(AfterlifeEntityProfileState.StatePath)
            : TryParseJsonObject(await ReadValidatedPendingTurnSnapshotFileAsync(manifest, AfterlifeEntityProfileState.StatePath));
        var profiles = new Dictionary<string, JsonObject>(StringComparer.OrdinalIgnoreCase);

        if (profileRoot?[AfterlifeEntityProfileState.ProfilesProperty] is JsonArray profileArray)
        {
            foreach (var profile in profileArray.OfType<JsonObject>())
            {
                var key = AfterlifeEntityProfileState.BuildIdentityKey(profile);
                if (!string.IsNullOrWhiteSpace(key))
                    profiles[key] = profile;
            }
        }

        return new AfterlifeSoulDissipationContext(soulRoot, profiles);
    }

    private async Task<IReadOnlyList<JsonObject>> ResolvePreTurnNoTurnConflictDicePayloadsAsync(
        ValidationPendingTurnSnapshotManifest? manifest)
    {
        if (manifest == null)
            return Array.Empty<JsonObject>();

        var preTurnJson = await ReadValidatedCurrentPreTurnTrackedFileAsync(AfterlifeSpiritualConflictState.StatePath);
        if (string.IsNullOrWhiteSpace(preTurnJson))
            return Array.Empty<JsonObject>();

        try
        {
            if (JsonNode.Parse(preTurnJson) is not JsonObject root)
                return Array.Empty<JsonObject>();

            var payloads = new List<JsonObject>();
            if (root["recentConflicts"] is JsonArray recentConflicts)
            {
                foreach (var entry in recentConflicts.OfType<JsonObject>())
                    TryAddPreTurnNoTurnDicePayload(payloads, entry);
            }

            if (root["activeConflict"] is JsonObject activeConflict &&
                activeConflict["exchangeLog"] is JsonArray exchangeLog)
            {
                foreach (var entry in exchangeLog.OfType<JsonObject>())
                    TryAddPreTurnNoTurnDicePayload(payloads, entry);
            }

            return payloads;
        }
        catch
        {
            // Malformed conflict state is reported by the normal state validator.
            return Array.Empty<JsonObject>();
        }
    }

    private static void TryAddPreTurnNoTurnDicePayload(List<JsonObject> payloads, JsonObject payload)
    {
        if (payload["diceAudit"] is not JsonObject diceAudit)
            return;

        if (ResolveLightIncarnateAuditTurn(payload, diceAudit) is > 0)
            return;

        if (payload.DeepClone() is JsonObject clone)
            payloads.Add(clone);
    }

    private async Task<IReadOnlyList<JsonObject>> ResolvePreTurnConflictPayloadsAsync(
        ValidationPendingTurnSnapshotManifest? manifest)
    {
        if (manifest == null)
            return Array.Empty<JsonObject>();

        var preTurnJson = await ReadValidatedCurrentPreTurnTrackedFileAsync(AfterlifeSpiritualConflictState.StatePath);
        if (string.IsNullOrWhiteSpace(preTurnJson))
            return Array.Empty<JsonObject>();

        try
        {
            if (JsonNode.Parse(preTurnJson) is not JsonObject root)
                return Array.Empty<JsonObject>();

            var payloads = new List<JsonObject>();
            if (root["activeConflict"] is JsonObject activeConflict &&
                activeConflict["exchangeLog"] is JsonArray exchangeLog)
            {
                foreach (var entry in exchangeLog.OfType<JsonObject>())
                    TryAddPreTurnConflictPayload(payloads, entry);
            }

            return payloads;
        }
        catch
        {
            return Array.Empty<JsonObject>();
        }
    }

    private static void TryAddPreTurnConflictPayload(List<JsonObject> payloads, JsonObject payload)
    {
        if (payload.DeepClone() is JsonObject clone)
            payloads.Add(clone);
    }

    private async Task<PreTurnActiveConflictControlContext> ResolvePreTurnActiveConflictControlContextAsync(
        ValidationPendingTurnSnapshotManifest? manifest)
    {
        if (manifest == null)
            return new PreTurnActiveConflictControlContext(null, null, null, null);

        var preTurnJson = await ReadValidatedCurrentPreTurnTrackedFileAsync(AfterlifeSpiritualConflictState.StatePath);
        if (string.IsNullOrWhiteSpace(preTurnJson))
            return new PreTurnActiveConflictControlContext(null, null, null, null);

        try
        {
            if (JsonNode.Parse(preTurnJson) is JsonObject root &&
                root["activeConflict"] is JsonObject activeConflict)
            {
                var conflictId = TryReadConflictId(activeConflict);
                var controlState = activeConflict.ContainsKey("controlState")
                    ? activeConflict["controlState"]?.DeepClone()
                    : null;
                return new PreTurnActiveConflictControlContext(
                    conflictId,
                    controlState,
                    ReadActionEconomyCurrent(activeConflict["actionEconomy"] as JsonObject, "player"),
                    ReadActionEconomyCurrent(activeConflict["actionEconomy"] as JsonObject, "opposition"));
            }
        }
        catch
        {
            // Malformed conflict state is reported by the normal state validator.
        }

        return new PreTurnActiveConflictControlContext(null, null, null, null);
    }

    private static int? ReadActionEconomyCurrent(JsonObject? actionEconomy, string side)
    {
        if (actionEconomy?[side] is JsonObject pool &&
            TryGetJsonNodeInt(pool["current"], out var current))
        {
            return current;
        }

        return null;
    }

    private async Task<int?> ResolveLightIncarnateGrantTurnAsync()
    {
        var soulRoot = await ReadJsonObjectAsync("game_state/meta/soul_state.json");
        if (!SourceOfLightCapstoneState.HasLightIncarnate(soulRoot))
            return null;

        var shiningRoot = await ReadJsonObjectAsync(ShiningAbodeState.StatePath);
        return SourceOfLightCapstoneState.GetLightIncarnateGrantTurn(soulRoot, shiningRoot);
    }

    private async Task<string?> TryReadShiningAvailabilityForConflictGateAsync(AfterlifeSpiritualConflictGateContext gateContext)
    {
        if (gateContext.UsesValidatedSnapshot && gateContext.Manifest != null)
        {
            var root = await TryReadValidatedShiningRootAsync(gateContext.Manifest);
            return root == null ? null : AfterlifeSpiritualConflictState.GetNodeString(root["availability"]);
        }

        return await TryReadCurrentShiningAvailabilityAsync();
    }

    private async Task<ShiningAbodeState.PreparedIncarnationPackageMode> TryReadShiningPreparedPackageModeForConflictGateAsync(
        AfterlifeSpiritualConflictGateContext gateContext)
    {
        if (gateContext.UsesValidatedSnapshot && gateContext.Manifest != null)
        {
            var root = await TryReadValidatedShiningRootAsync(gateContext.Manifest);
            return root == null
                ? ShiningAbodeState.PreparedIncarnationPackageMode.Absent
                : ShiningAbodeState.GetPreparedIncarnationPackageMode(root);
        }

        return await TryReadCurrentShiningPreparedPackageModeAsync();
    }

    private async Task<JsonObject?> TryReadValidatedShiningRootAsync(ValidationPendingTurnSnapshotManifest manifest)
    {
        var snapshotJson = await ReadValidatedPendingTurnSnapshotFileAsync(manifest, ShiningAbodeState.StatePath);
        if (string.IsNullOrWhiteSpace(snapshotJson))
            return null;

        try
        {
            return JsonNode.Parse(snapshotJson) as JsonObject;
        }
        catch
        {
            return null;
        }
    }

    private async Task ValidateActiveConflictRemovalHasTerminalProofAsync(JsonObject? currentRoot, List<ValidationIssue> issues)
    {
        var lookup = await LoadValidatedPendingTurnSnapshotLookupAsync();
        if (lookup.Status != ValidatedPendingTurnSnapshotStatus.Usable || lookup.Manifest == null)
            return;

        var preTurnJson = await ReadValidatedPendingTurnSnapshotFileAsync(lookup.Manifest, AfterlifeSpiritualConflictState.StatePath);
        if (string.IsNullOrWhiteSpace(preTurnJson))
            return;

        JsonObject? preTurnRoot;
        try
        {
            preTurnRoot = JsonNode.Parse(preTurnJson) as JsonObject;
        }
        catch
        {
            return;
        }

        if (preTurnRoot?["activeConflict"] is not JsonObject)
            return;

        var preTurnConflictId = TryReadActiveConflictId(preTurnRoot);
        if (string.IsNullOrWhiteSpace(preTurnConflictId))
            return;

        var currentConflictId = currentRoot == null ? null : TryReadActiveConflictId(currentRoot);
        if (!string.IsNullOrWhiteSpace(currentConflictId) &&
            string.Equals(currentConflictId, preTurnConflictId, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        if (HasTerminalProofForConflict(currentRoot, preTurnConflictId))
            return;

        issues.Add(new ValidationIssue(
            $"{AfterlifeSpiritualConflictState.StatePath}.activeConflict",
            IssueSeverity.Error,
            "Pre-turn active afterlife spiritual conflict был удалён или заменён без terminal proof.",
            code: "afterlife_conflict_active_removed_without_terminal_proof",
            section: "AfterlifeSpiritualConflict",
            expected: $"activeConflict.conflictId = {preTurnConflictId} или matching recentConflicts[] resolve/repair_cancel proof",
            actual: currentRoot == null
                ? "current conflict state missing/unreadable"
                : string.IsNullOrWhiteSpace(currentConflictId)
                    ? "activeConflict missing/null and no matching terminal proof"
                    : $"activeConflict.conflictId = {currentConflictId} without terminal proof for {preTurnConflictId}",
            repairHint: "Восстанови pre-turn activeConflict или закрой его через afterlifeSpiritualConflictUpdate.mode=resolve либо mode=repair_cancel, чтобы recentConflicts[] содержал matching terminal proof."));
    }

    private static string? TryReadActiveConflictId(JsonObject root)
    {
        return root["activeConflict"] is JsonObject activeConflict
            ? TryReadConflictId(activeConflict)
            : null;
    }

    private static string? TryReadConflictId(JsonObject conflict)
    {
        return AfterlifeSpiritualConflictState.GetNodeString(conflict["conflictId"]) ??
               AfterlifeSpiritualConflictState.GetNodeString(conflict["id"]);
    }

    private static bool HasTerminalProofForConflict(JsonObject? currentRoot, string conflictId)
    {
        if (currentRoot?["recentConflicts"] is not JsonArray recentConflicts)
            return false;

        return recentConflicts
            .OfType<JsonObject>()
            .Any(proof => IsTerminalRecentConflictProof(proof, conflictId));
    }

    private static bool IsTerminalRecentConflictProof(JsonObject proof, string conflictId)
    {
        var proofConflictId = AfterlifeSpiritualConflictState.GetNodeString(proof["conflictId"]) ??
                              AfterlifeSpiritualConflictState.GetNodeString(proof["id"]);
        if (!string.Equals(proofConflictId, conflictId, StringComparison.OrdinalIgnoreCase))
            return false;

        var resolutionState = AfterlifeSpiritualConflictState.GetNodeString(proof["resolutionState"]) ??
                              AfterlifeSpiritualConflictState.GetNodeString(proof["status"]);
        if (string.Equals(resolutionState, "repair_cancelled", StringComparison.OrdinalIgnoreCase))
            return true;

        if (!string.Equals(resolutionState, "resolved", StringComparison.OrdinalIgnoreCase))
            return false;

        var operationType = AfterlifeSpiritualConflictState.GetNodeString(proof["operationType"]);
        return AfterlifeSpiritualConflictState.GetNodeInt(proof["resolvedAtTurn"]) > 0 &&
               !string.IsNullOrWhiteSpace(operationType) &&
               AfterlifeSpiritualConflictState.OperationTypes.Contains(operationType) &&
               HasTerminalResolveOutcomeEvidence(proof);
    }

    private static bool HasTerminalResolveOutcomeEvidence(JsonObject proof)
    {
        if (!string.IsNullOrWhiteSpace(AfterlifeSpiritualConflictState.GetNodeString(proof["playerOutcome"])))
            return true;

        var resolutionKind = AfterlifeSpiritualConflictState.GetNodeString(proof["resolutionKind"]);
        return string.Equals(resolutionKind, "player_loss", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(resolutionKind, "player_surrender", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(resolutionKind, "player_concession", StringComparison.OrdinalIgnoreCase);
    }

    private async Task<string?> TryReadCurrentShiningAvailabilityAsync()
    {
        var shiningJson = await _fs.ReadFileAsync(ShiningAbodeState.StatePath);
        if (string.IsNullOrWhiteSpace(shiningJson))
            return null;

        try
        {
            return JsonNode.Parse(shiningJson) is JsonObject root
                ? AfterlifeSpiritualConflictState.GetNodeString(root["availability"])
                : null;
        }
        catch
        {
            return null;
        }
    }

    private async Task<string?> TryReadCurrentSoulRealmAsync()
    {
        var soulJson = await _fs.ReadFileAsync("game_state/meta/soul_state.json");
        if (string.IsNullOrWhiteSpace(soulJson))
            return null;

        try
        {
            using var doc = JsonDocument.Parse(soulJson);
            return doc.RootElement.ValueKind == JsonValueKind.Object &&
                   doc.RootElement.TryGetProperty("currentRealm", out var realm) &&
                   realm.ValueKind == JsonValueKind.String
                ? realm.GetString()
                : null;
        }
        catch
        {
            return null;
        }
    }

    private async Task<ShiningAbodeState.PreparedIncarnationPackageMode> TryReadCurrentShiningPreparedPackageModeAsync()
    {
        var shiningJson = await _fs.ReadFileAsync(ShiningAbodeState.StatePath);
        if (string.IsNullOrWhiteSpace(shiningJson))
            return ShiningAbodeState.PreparedIncarnationPackageMode.Absent;

        try
        {
            var root = JsonNode.Parse(shiningJson) as JsonObject;
            return root == null
                ? ShiningAbodeState.PreparedIncarnationPackageMode.Absent
                : ShiningAbodeState.GetPreparedIncarnationPackageMode(root);
        }
        catch
        {
            return ShiningAbodeState.PreparedIncarnationPackageMode.Absent;
        }
    }

    private void ValidateAfterlifeCombatProfile(JsonElement root, string contextPrefix, List<ValidationIssue> issues)
    {
        if (!root.TryGetProperty(AfterlifeSpiritualConflictState.SoulStateProfileProperty, out var profile))
            return;

        var context = $"{contextPrefix}.{AfterlifeSpiritualConflictState.SoulStateProfileProperty}";
        if (!RequireObject(profile, context, issues))
            return;

        ValidateNonNegativeIntegerField(profile, context, issues, "schemaVersion", "AfterlifeSpiritualConflict");
        ValidateNonNegativeIntegerField(profile, context, issues, "enlightenmentRank", "AfterlifeSpiritualConflict");
        ValidateNonNegativeIntegerField(profile, context, issues, "radianceRank", "AfterlifeSpiritualConflict");
        ValidateNonNegativeIntegerField(profile, context, issues, "retainedRadianceRank", "AfterlifeSpiritualConflict");
        ValidateNonNegativeIntegerField(profile, context, issues, "lastRecoveryTurn", "AfterlifeSpiritualConflict");
        if (profile.TryGetProperty(AfterlifeSpiritualConflictState.SpiritFocusTierProperty, out var spiritFocusTier) &&
            (spiritFocusTier.ValueKind != JsonValueKind.Number ||
             !spiritFocusTier.TryGetInt32(out var parsedSpiritFocusTier) ||
             parsedSpiritFocusTier < 0 ||
             parsedSpiritFocusTier > AfterlifeSpiritualConflictState.SpiritFocusMaxTier))
        {
            issues.Add(new ValidationIssue(
                $"{context}.{AfterlifeSpiritualConflictState.SpiritFocusTierProperty}",
                IssueSeverity.Error,
                "afterlifeCombatProfile.spiritFocusTier должен быть integer 0..5.",
                code: "afterlife_combat_profile_invalid_spirit_focus_tier",
                section: "AfterlifeSpiritualConflict",
                expected: "integer 0..5",
                actual: spiritFocusTier.ValueKind == JsonValueKind.Number ? spiritFocusTier.GetRawText() : spiritFocusTier.ValueKind.ToString()));
        }

        ValidateLightIncarnateCombatProfileCapstone(profile, context, issues);

        if (!profile.TryGetProperty("artTiers", out var artTiers))
            return;

        if (!RequireObject(artTiers, $"{context}.artTiers", issues))
            return;

        var allowedArtIds = AfterlifeSpiritualConflictState.SpiritualArts
            .Select(art => art.ArtId)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        foreach (var art in artTiers.EnumerateObject())
        {
            if (!allowedArtIds.Contains(art.Name))
            {
                issues.Add(new ValidationIssue(
                    $"{context}.artTiers.{art.Name}",
                    IssueSeverity.Error,
                    "afterlifeCombatProfile.artTiers содержит неизвестный spiritual art id.",
                    code: "afterlife_combat_profile_unknown_art",
                    section: "AfterlifeSpiritualConflict",
                    expected: string.Join("/", allowedArtIds.OrderBy(value => value, StringComparer.OrdinalIgnoreCase)),
                    actual: art.Name));
                continue;
            }

            if (art.Value.ValueKind != JsonValueKind.Number ||
                !art.Value.TryGetInt32(out var tier) ||
                tier < 0 ||
                tier > 5)
            {
                issues.Add(new ValidationIssue(
                    $"{context}.artTiers.{art.Name}",
                    IssueSeverity.Error,
                    "spiritual art tier должен быть integer 0..5.",
                    code: "afterlife_combat_profile_invalid_art_tier",
                    section: "AfterlifeSpiritualConflict",
                    expected: "integer 0..5",
                    actual: art.Value.ValueKind == JsonValueKind.Number ? art.Value.GetRawText() : art.Value.ValueKind.ToString()));
            }
        }
    }

    private void ValidateAfterlifeSpiritualConflictUpdateContract(JsonElement root, string contextPrefix, List<ValidationIssue> issues)
    {
        if (!root.TryGetProperty(AfterlifeSpiritualConflictState.ResponseField, out var update))
            return;

        var context = $"{contextPrefix}.{AfterlifeSpiritualConflictState.ResponseField}";
        if (!RequireObject(update, context, issues))
            return;

        var mode = TryGetString(update, "mode");
        if (string.IsNullOrWhiteSpace(mode) || !AfterlifeSpiritualConflictState.Modes.Contains(mode))
        {
            issues.Add(new ValidationIssue(
                $"{context}.mode",
                IssueSeverity.Error,
                "afterlifeSpiritualConflictUpdate.mode должен быть одним из supported lifecycle modes.",
                code: "afterlife_conflict_update_invalid_mode",
                section: "AfterlifeSpiritualConflict",
                expected: string.Join("/", AfterlifeSpiritualConflictState.Modes.OrderBy(value => value, StringComparer.OrdinalIgnoreCase)),
                actual: string.IsNullOrWhiteSpace(mode) ? "missing/empty" : mode));
            return;
        }

        if (ContainsProperty(update, "opponent") ||
            ContainsProperty(update, "playerStrain") ||
            ContainsProperty(update, "opponentStrain"))
        {
            issues.Add(new ValidationIssue(
                context,
                IssueSeverity.Error,
                "afterlife spiritual conflict использует side-vs-side schema; root opponent/playerStrain/opponentStrain запрещены.",
                code: "afterlife_conflict_update_legacy_fields",
                section: "AfterlifeSpiritualConflict",
                expected: "playerSide/oppositionSide plus playerSideStrain/oppositionSideStrain",
                actual: "legacy opponent/playerStrain/opponentStrain field present"));
        }

        if (string.Equals(mode, AfterlifeSpiritualConflictState.ModeStart, StringComparison.OrdinalIgnoreCase) &&
            !TryGetObject(update, "conflictState", out _) &&
            !TryGetObject(update, "activeConflict", out _) &&
            !TryGetObject(update, "conflictSeed", out _))
        {
            issues.Add(new ValidationIssue(
                context,
                IssueSeverity.Error,
                "start update должен содержать conflictSeed, conflictState или activeConflict object.",
                code: "afterlife_conflict_start_missing_conflict_state",
                section: "AfterlifeSpiritualConflict",
                expected: "conflictSeed/conflictState/activeConflict object",
                actual: "missing"));
        }

        if (string.Equals(mode, AfterlifeSpiritualConflictState.ModeExchange, StringComparison.OrdinalIgnoreCase) &&
            !TryGetObject(update, "exchange", out _))
        {
            issues.Add(new ValidationIssue(
                context,
                IssueSeverity.Error,
                "exchange update должен содержать полный exchange object.",
                code: "afterlife_conflict_exchange_missing_payload",
                section: "AfterlifeSpiritualConflict",
                expected: "exchange object with exchangeId, operationType, outcome, before, and after",
                actual: "missing"));
        }
    }

    private void ValidateAfterlifeSpiritualConflictRoot(
        JsonObject root,
        string context,
        List<ValidationIssue> issues,
        AfterlifeConflictDiceContext diceContext,
        AfterlifeActionCostAuthorityContext actionCostAuthority,
        AfterlifeConflictRewardContext rewardContext,
        AfterlifeSoulDissipationContext soulDissipationContext)
    {
        if (root.ContainsKey(AfterlifeSpiritualConflictState.ResponseField))
        {
            issues.Add(new ValidationIssue(
                $"{context}.{AfterlifeSpiritualConflictState.ResponseField}",
                IssueSeverity.Error,
                "afterlifeSpiritualConflictUpdate не должен храниться wrapper-полем в state file.",
                code: "afterlife_conflict_state_unprojected_update",
                section: "AfterlifeSpiritualConflict",
                expected: "activeConflict/recentConflicts canonical projection",
                actual: "raw response field in state file"));
        }

        if (root.ContainsKey("lastInvalidUpdate"))
        {
            issues.Add(new ValidationIssue(
                $"{context}.lastInvalidUpdate",
                IssueSeverity.Error,
                "Последний afterlifeSpiritualConflictUpdate не был применён из-за некорректной формы.",
                code: "afterlife_conflict_state_invalid_update",
                section: "AfterlifeSpiritualConflict",
                expected: "valid start/exchange/resolve/repair_cancel update",
                actual: AfterlifeSpiritualConflictState.GetNodeString(root["lastInvalidUpdateReason"]) ?? "invalid update",
                repairHint: "Исправь GM response surface afterlifeSpiritualConflictUpdate и повтори accepted-turn repair."));
        }

        if (root["schemaVersion"] is not JsonValue schema ||
            !schema.TryGetValue<int>(out var schemaVersion) ||
            schemaVersion <= 0)
        {
            issues.Add(new ValidationIssue(
                $"{context}.schemaVersion",
                IssueSeverity.Error,
                "afterlife spiritual conflict state должен иметь positive integer schemaVersion.",
                code: "afterlife_conflict_state_invalid_schema_version",
                section: "AfterlifeSpiritualConflict",
                expected: "positive integer",
                actual: root["schemaVersion"]?.ToJsonString() ?? "missing"));
        }

        if (root["recentConflicts"] is JsonArray recentConflicts)
        {
            var rewardConflictIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            for (var index = 0; index < recentConflicts.Count; index++)
            {
                if (recentConflicts[index] is JsonObject proof)
                    ValidateRecentConflictProof(proof, $"{context}.recentConflicts[{index}]", issues, diceContext, rewardContext, rewardConflictIds, soulDissipationContext);
            }
        }
        else
        {
            issues.Add(new ValidationIssue(
                $"{context}.recentConflicts",
                IssueSeverity.Error,
                "recentConflicts должен быть array.",
                code: "afterlife_conflict_state_invalid_recent_conflicts",
                section: "AfterlifeSpiritualConflict",
                expected: "array",
                actual: root["recentConflicts"]?.GetType().Name ?? "missing"));
        }

        ValidateTerminalGameOverHasSoulDissipationProof(root, issues, soulDissipationContext);

        if (root["activeConflict"] is JsonObject active)
            ValidateActiveAfterlifeConflict(active, $"{context}.activeConflict", issues, diceContext, actionCostAuthority);
        else if (root.ContainsKey("activeConflict") && root["activeConflict"] != null)
            issues.Add(new ValidationIssue(
                $"{context}.activeConflict",
                IssueSeverity.Error,
                "activeConflict должен быть object или null.",
                code: "afterlife_conflict_state_invalid_active_conflict",
                section: "AfterlifeSpiritualConflict",
                expected: "object|null",
                actual: root["activeConflict"]?.GetType().Name ?? "missing"));
    }

    private void ValidateActiveAfterlifeConflict(
        JsonObject conflict,
        string context,
        List<ValidationIssue> issues,
        AfterlifeConflictDiceContext diceContext,
        AfterlifeActionCostAuthorityContext actionCostAuthority)
    {
        RequireNodeString(conflict, context, issues, "conflictId");
        var realm = RequireNodeString(conflict, context, issues, "realm");
        if (!string.IsNullOrWhiteSpace(realm) && !AfterlifeSpiritualConflictState.IsAfterlifeRealm(realm))
        {
            issues.Add(new ValidationIssue(
                $"{context}.realm",
                IssueSeverity.Error,
                "afterlife conflict realm должен быть Chaos Sea или Shining Abode.",
                code: "afterlife_conflict_invalid_realm",
                section: "AfterlifeSpiritualConflict",
                expected: "Chaos Sea or Shining Abode",
                actual: realm));
        }

        var sideModel = RequireNodeString(conflict, context, issues, "sideModel");
        if (!string.IsNullOrWhiteSpace(sideModel) && !AfterlifeSpiritualConflictState.SideModels.Contains(sideModel))
        {
            issues.Add(new ValidationIssue(
                $"{context}.sideModel",
                IssueSeverity.Error,
                "sideModel должен быть supported afterlife conflict model.",
                code: "afterlife_conflict_invalid_side_model",
                section: "AfterlifeSpiritualConflict",
                expected: string.Join("/", AfterlifeSpiritualConflictState.SideModels.OrderBy(value => value, StringComparer.OrdinalIgnoreCase)),
                actual: sideModel));
        }

        ValidateSide(conflict["playerSide"] as JsonObject, $"{context}.playerSide", issues, allowPlayerLead: true);
        ValidateSide(conflict["oppositionSide"] as JsonObject, $"{context}.oppositionSide", issues, allowPlayerLead: false);
        ValidateEnumNode(conflict, context, issues, "playerSideStrain", AfterlifeSpiritualConflictState.StrainStates, "afterlife_conflict_invalid_player_side_strain");
        ValidateEnumNode(conflict, context, issues, "oppositionSideStrain", AfterlifeSpiritualConflictState.StrainStates, "afterlife_conflict_invalid_opposition_side_strain");
        ValidateEnumNode(conflict, context, issues, "conflictPosition", AfterlifeSpiritualConflictState.ConflictPositions, "afterlife_conflict_invalid_position");
        ValidateControlStateShape(conflict["controlState"], $"{context}.controlState", issues, required: false);
        var resolutionState = ValidateEnumNode(conflict, context, issues, "resolutionState", AfterlifeSpiritualConflictState.ResolutionStates, "afterlife_conflict_invalid_resolution_state");
        if (string.Equals(resolutionState, "resolved", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(resolutionState, "repair_cancelled", StringComparison.OrdinalIgnoreCase))
        {
            issues.Add(new ValidationIssue(
                $"{context}.resolutionState",
                IssueSeverity.Error,
                "Terminal resolutionState не может оставаться под activeConflict.",
                code: "afterlife_conflict_terminal_active_conflict",
                section: "AfterlifeSpiritualConflict",
                expected: "active/concession_pending/surrender_pending/retreat_pending/ready_to_resolve for activeConflict",
                actual: resolutionState,
                repairHint: "Закрывай terminal конфликт через afterlifeSpiritualConflictUpdate.mode=resolve или repair_cancel: activeConflict должен стать null, а resolved proof должен быть перенесён в recentConflicts[]."));
        }

        foreach (var legacy in new[] { "opponent", "playerStrain", "opponentStrain" })
        {
            if (conflict.ContainsKey(legacy))
            {
                issues.Add(new ValidationIssue(
                    $"{context}.{legacy}",
                    IssueSeverity.Error,
                    "Legacy one-vs-one conflict field запрещён; используйте side-vs-side schema.",
                    code: "afterlife_conflict_legacy_field",
                    section: "AfterlifeSpiritualConflict",
                    expected: "playerSide/oppositionSide and side strain fields",
                    actual: legacy));
            }
        }

        var priorControlState = ResolveScopedPreTurnActiveControlState(conflict, diceContext);
        var hasCurrentExchange = false;
        int? expectedNextPlayerActionCostBefore = ResolveScopedPreTurnActionEconomyCurrent(conflict, diceContext, "player");
        int? lastCurrentPlayerActionCostAfter = null;
        int? expectedNextOppositionActionCostBefore = ResolveScopedPreTurnActionEconomyCurrent(conflict, diceContext, "opposition");
        int? lastCurrentOppositionActionCostAfter = null;
        if (conflict["exchangeLog"] is JsonArray exchangeLog)
        {
            var preTurnExchangePayloads = new PreTurnConflictPayloadTracker(diceContext.PreTurnConflictPayloads);
            for (var index = 0; index < exchangeLog.Count; index++)
            {
                if (exchangeLog[index] is JsonObject exchange)
                {
                    var isPreTurnExchange = preTurnExchangePayloads.TryConsume(exchange);
                    var isCurrentExchange = diceContext.HasValidatedTurnBaseline && !isPreTurnExchange;
                    hasCurrentExchange = hasCurrentExchange || isCurrentExchange;
                    ValidateConflictExchange(
                        exchange,
                        priorControlState,
                        conflict,
                        conflict["actionEconomy"] as JsonObject,
                        $"{context}.exchangeLog[{index}]",
                        issues,
                        diceContext,
                        actionCostAuthority,
                        isPreTurnExchange);
                    if (isCurrentExchange)
                    {
                        if (ExchangeExpectsPlayerActionCostAudit(exchange))
                        {
                            ValidateCurrentActionCostSequence(
                                exchange,
                                "player",
                                expectedNextPlayerActionCostBefore,
                                $"{context}.exchangeLog[{index}]",
                                issues,
                                out var currentExchangeActionAfter);
                            if (currentExchangeActionAfter.HasValue)
                            {
                                expectedNextPlayerActionCostBefore = currentExchangeActionAfter.Value;
                                lastCurrentPlayerActionCostAfter = currentExchangeActionAfter.Value;
                            }
                        }

                        if (ExchangeExpectsOppositionActionCostAudit(exchange))
                        {
                            ValidateCurrentActionCostSequence(
                                exchange,
                                "opposition",
                                expectedNextOppositionActionCostBefore,
                                $"{context}.exchangeLog[{index}]",
                                issues,
                                out var currentOppositionActionAfter);
                            if (currentOppositionActionAfter.HasValue)
                            {
                                expectedNextOppositionActionCostBefore = currentOppositionActionAfter.Value;
                                lastCurrentOppositionActionCostAfter = currentOppositionActionAfter.Value;
                            }
                        }
                    }

                    if (!isPreTurnExchange)
                    {
                        priorControlState = ResolveNextPriorControlState(priorControlState, exchange);
                    }
                }
                else
                    issues.Add(new ValidationIssue(
                        $"{context}.exchangeLog[{index}]",
                        IssueSeverity.Error,
                        "exchangeLog[] item должен быть object.",
                        code: "afterlife_conflict_invalid_exchange_item",
                        section: "AfterlifeSpiritualConflict"));
            }

        }
        else if (conflict.ContainsKey("exchangeLog") && conflict["exchangeLog"] != null)
        {
            issues.Add(new ValidationIssue(
                $"{context}.exchangeLog",
                IssueSeverity.Error,
                "exchangeLog должен быть array.",
                code: "afterlife_conflict_invalid_exchange_log",
                section: "AfterlifeSpiritualConflict"));
        }

        ValidateActionEconomyShape(
            conflict["actionEconomy"],
            $"{context}.actionEconomy",
            issues,
            required: hasCurrentExchange);
        ValidateActionEconomyMatchesSpiritFocus(
            conflict["actionEconomy"] as JsonObject,
            diceContext,
            $"{context}.actionEconomy.player",
            issues);
        ValidateActionEconomyMatchesLastCurrentExchange(
            conflict["actionEconomy"] as JsonObject,
            "player",
            lastCurrentPlayerActionCostAfter,
            $"{context}.actionEconomy.player.current",
            issues);
        ValidateActionEconomyUnchangedWhenUnaudited(
            conflict["actionEconomy"] as JsonObject,
            "player",
            lastCurrentPlayerActionCostAfter,
            ResolveScopedPreTurnActionEconomyCurrent(conflict, diceContext, "player"),
            $"{context}.actionEconomy.player.current",
            issues);
        ValidateActionEconomyMatchesLastCurrentExchange(
            conflict["actionEconomy"] as JsonObject,
            "opposition",
            lastCurrentOppositionActionCostAfter,
            $"{context}.actionEconomy.opposition.current",
            issues);
        ValidateActionEconomyUnchangedWhenUnaudited(
            conflict["actionEconomy"] as JsonObject,
            "opposition",
            lastCurrentOppositionActionCostAfter,
            ResolveScopedPreTurnActionEconomyCurrent(conflict, diceContext, "opposition"),
            $"{context}.actionEconomy.opposition.current",
            issues);

        ValidateFinalActiveControlStateMatchesExchangeSnapshots(
            conflict,
            priorControlState,
            context,
            issues,
            diceContext.HasValidatedTurnBaseline);
    }

    private void ValidateSide(JsonObject? side, string context, List<ValidationIssue> issues, bool allowPlayerLead)
    {
        if (side == null)
        {
            issues.Add(new ValidationIssue(
                context,
                IssueSeverity.Error,
                "Conflict side должен быть object с leadContestant.",
                code: "afterlife_conflict_missing_side",
                section: "AfterlifeSpiritualConflict",
                expected: "side object with leadContestant",
                actual: "missing/non-object"));
            return;
        }

        if (side["leadContestant"] is not JsonObject lead)
        {
            issues.Add(new ValidationIssue(
                $"{context}.leadContestant",
                IssueSeverity.Error,
                "Каждая сторона должна иметь ровно одного leadContestant.",
                code: "afterlife_conflict_missing_lead_contestant",
                section: "AfterlifeSpiritualConflict",
                expected: "leadContestant object",
                actual: side["leadContestant"]?.GetType().Name ?? "missing"));
            return;
        }

        var actorType = RequireNodeString(lead, $"{context}.leadContestant", issues, "actorType");
        RequireNodeString(lead, $"{context}.leadContestant", issues, "actorId");
        RequireNodeString(lead, $"{context}.leadContestant", issues, "displayName");

        var isPlayerLead = string.Equals(actorType, "player", StringComparison.OrdinalIgnoreCase) ||
                           string.Equals(actorType, "soul", StringComparison.OrdinalIgnoreCase);
        if (isPlayerLead && !allowPlayerLead)
        {
            issues.Add(new ValidationIssue(
                $"{context}.leadContestant.actorType",
                IssueSeverity.Error,
                "oppositionSide не может иметь player/soul leadContestant.",
                code: "afterlife_conflict_opposition_player_lead",
                section: "AfterlifeSpiritualConflict",
                expected: "guardian/resident/radiant_actor/custom_afterlife_actor",
                actual: actorType));
        }

        if (!isPlayerLead)
        {
            if (lead["actorArtTierSnapshot"] is not JsonObject)
            {
                issues.Add(new ValidationIssue(
                    $"{context}.leadContestant.actorArtTierSnapshot",
                    IssueSeverity.Error,
                    "Non-player leadContestant должен иметь actorArtTierSnapshot.",
                    code: "afterlife_conflict_missing_actor_art_snapshot",
                    section: "AfterlifeSpiritualConflict",
                    expected: "object with resolved spiritual art tiers",
                    actual: lead["actorArtTierSnapshot"]?.GetType().Name ?? "missing"));
            }

            RequireNodeString(lead, $"{context}.leadContestant", issues, "artAuthoritySource");
        }

        if (side["supporters"] is JsonArray supporters)
        {
            for (var index = 0; index < supporters.Count; index++)
            {
                if (supporters[index] is not JsonObject supporter)
                {
                    issues.Add(new ValidationIssue(
                        $"{context}.supporters[{index}]",
                        IssueSeverity.Error,
                        "supporters[] item должен быть object.",
                        code: "afterlife_conflict_invalid_supporter_item",
                        section: "AfterlifeSpiritualConflict",
                        expected: "supporter object with actorType, actorId, supportRole",
                        actual: supporters[index]?.GetType().Name ?? "null"));
                    continue;
                }

                RequireNodeString(supporter, $"{context}.supporters[{index}]", issues, "actorType");
                RequireNodeString(supporter, $"{context}.supporters[{index}]", issues, "actorId");
                RequireNodeString(supporter, $"{context}.supporters[{index}]", issues, "supportRole");
            }
        }
        else if (side.ContainsKey("supporters") && side["supporters"] != null)
        {
            issues.Add(new ValidationIssue(
                $"{context}.supporters",
                IssueSeverity.Error,
                "supporters должен быть array.",
                code: "afterlife_conflict_invalid_supporters",
                section: "AfterlifeSpiritualConflict"));
        }
    }

    private void ValidateRecentConflictProof(
        JsonObject proof,
        string context,
        List<ValidationIssue> issues,
        AfterlifeConflictDiceContext diceContext,
        AfterlifeConflictRewardContext rewardContext,
        HashSet<string> rewardConflictIds,
        AfterlifeSoulDissipationContext soulDissipationContext)
    {
        var diceRequired = ResolveDiceAuditRequired(proof);
        if (diceRequired && proof["diceAudit"] is not JsonObject)
        {
            issues.Add(new ValidationIssue(
                $"{context}.diceAudit",
                IssueSeverity.Error,
                "Contested afterlife conflict resolution требует diceAudit.",
                code: "afterlife_conflict_resolution_missing_dice_audit",
                section: "AfterlifeSpiritualConflict",
                expected: "resolution.diceAudit with current turn preGeneratedDices1d20 source indices",
                actual: proof["diceAudit"]?.GetType().Name ?? "missing"));
            return;
        }

        if (proof["diceAudit"] is JsonObject diceAudit)
        {
            ValidateAfterlifeConflictDiceAudit(diceAudit, $"{context}.diceAudit", issues, diceContext);
            ValidateLightIncarnateDiceAuditModifier(proof, diceAudit, $"{context}.diceAudit", issues, diceContext);
        }

        ValidateConflictRewardAudit(proof, context, issues, rewardContext, rewardConflictIds);
        ValidateSoulDissipationProof(proof, context, issues, soulDissipationContext);
    }

    private void ValidateSoulDissipationProof(
        JsonObject conflictProof,
        string context,
        List<ValidationIssue> issues,
        AfterlifeSoulDissipationContext soulDissipationContext)
    {
        if (conflictProof[AfterlifeSpiritualConflictState.SoulDissipationProofProperty] is null)
            return;

        var proofContext = $"{context}.{AfterlifeSpiritualConflictState.SoulDissipationProofProperty}";
        if (conflictProof[AfterlifeSpiritualConflictState.SoulDissipationProofProperty] is not JsonObject proof)
        {
            AddSoulDissipationIssue(
                issues,
                proofContext,
                "soulDissipationProof должен быть object.",
                "afterlife_conflict_soul_dissipation_invalid_shape",
                "object",
                conflictProof[AfterlifeSpiritualConflictState.SoulDissipationProofProperty]?.GetType().Name ?? "null");
            return;
        }

        var proofId = RequireSoulDissipationString(proof, proofContext, issues, "proofId");
        var actorType = RequireSoulDissipationString(proof, proofContext, issues, "actorType");
        var actorId = RequireSoulDissipationString(proof, proofContext, issues, "actorId");
        var targetActorType = RequireSoulDissipationString(proof, proofContext, issues, "targetActorType");
        var targetActorId = RequireSoulDissipationString(proof, proofContext, issues, "targetActorId");
        RequireSoulDissipationString(proof, proofContext, issues, "outcome");

        if (string.IsNullOrWhiteSpace(GetFirstSoulDissipationString(proof, "gmMotivation", "motivation", "reason")))
        {
            AddSoulDissipationIssue(
                issues,
                $"{proofContext}.gmMotivation",
                "Развеивание души требует явного решения и мотива ГМа; возможность развеять не означает автоматическое развеивание.",
                "afterlife_conflict_soul_dissipation_missing_motivation",
                "non-empty gmMotivation/motivation/reason",
                "missing");
        }

        if (!TryGetJsonNodeInt(proof["resolvedAtTurn"], out var proofTurn) || proofTurn <= 0)
        {
            AddSoulDissipationIssue(
                issues,
                $"{proofContext}.resolvedAtTurn",
                "soulDissipationProof должен фиксировать resolvedAtTurn.",
                "afterlife_conflict_soul_dissipation_missing_turn",
                "positive integer resolvedAtTurn",
                proof["resolvedAtTurn"]?.ToJsonString() ?? "missing");
        }

        var actorProfile = ResolveAfterlifeSoulDissipationProfile(soulDissipationContext, actorType, actorId);
        var targetProfile = ResolveAfterlifeSoulDissipationProfile(soulDissipationContext, targetActorType, targetActorId);
        var actorTierFromProfile = actorProfile == null
            ? 0
            : AfterlifeEntityProfileState.GetNodeInt(actorProfile[AfterlifeEntityProfileState.SoulDissipationTierProperty]);
        var targetCoefficientFromProfile = AfterlifeEntityProfileState.ResolveSoulStabilityCoefficient(targetProfile);

        if (actorProfile == null)
        {
            AddSoulDissipationIssue(
                issues,
                proofContext,
                "Развеивание души требует профиль действующей сущности в afterlife_entity_profiles.json.",
                "afterlife_conflict_soul_dissipation_missing_actor_profile",
                "matching actorType/actorId profile",
                BuildActorDescription(actorType, actorId));
        }

        if (targetProfile == null)
        {
            AddSoulDissipationIssue(
                issues,
                proofContext,
                "Развеивание души требует профиль цели в afterlife_entity_profiles.json.",
                "afterlife_conflict_soul_dissipation_missing_target_profile",
                "matching targetActorType/targetActorId profile",
                BuildActorDescription(targetActorType, targetActorId));
        }

        if (!TryGetJsonNodeInt(proof["dissipationTier"], out var proofTier) || proofTier < 0)
        {
            AddSoulDissipationIssue(
                issues,
                $"{proofContext}.dissipationTier",
                "soulDissipationProof должен указывать неотрицательный tier Развеивания души действующей сущности.",
                "afterlife_conflict_soul_dissipation_invalid_tier",
                "non-negative integer dissipationTier",
                proof["dissipationTier"]?.ToJsonString() ?? "missing");
        }
        else if (actorProfile != null && proofTier != actorTierFromProfile)
        {
            AddSoulDissipationIssue(
                issues,
                $"{proofContext}.dissipationTier",
                "soulDissipationProof.dissipationTier должен совпадать с профилем действующей сущности.",
                "afterlife_conflict_soul_dissipation_tier_mismatch",
                actorTierFromProfile.ToString(),
                proofTier.ToString());
        }

        if (!TryGetJsonNodeInt(proof["targetStabilityCoefficient"], out var proofCoefficient) || proofCoefficient < 0)
        {
            AddSoulDissipationIssue(
                issues,
                $"{proofContext}.targetStabilityCoefficient",
                "soulDissipationProof должен указывать targetStabilityCoefficient цели.",
                "afterlife_conflict_soul_dissipation_invalid_target_coefficient",
                "non-negative integer targetStabilityCoefficient",
                proof["targetStabilityCoefficient"]?.ToJsonString() ?? "missing");
        }
        else if (targetProfile != null && proofCoefficient != targetCoefficientFromProfile)
        {
            AddSoulDissipationIssue(
                issues,
                $"{proofContext}.targetStabilityCoefficient",
                "targetStabilityCoefficient должен совпадать с коэффициентом устойчивости души из профиля цели.",
                "afterlife_conflict_soul_dissipation_target_coefficient_mismatch",
                targetCoefficientFromProfile.ToString(),
                proofCoefficient.ToString());
        }

        var effectiveTier = actorProfile == null ? proofTier : actorTierFromProfile;
        var effectiveCoefficient = targetProfile == null ? proofCoefficient : targetCoefficientFromProfile;
        if (effectiveTier <= effectiveCoefficient)
        {
            AddSoulDissipationIssue(
                issues,
                $"{proofContext}.dissipationTier",
                "Развеивание души возможно только если tier Развеивания строго выше коэффициента устойчивости цели.",
                "afterlife_conflict_soul_dissipation_tier_too_low",
                $"dissipationTier > targetStabilityCoefficient ({effectiveCoefficient})",
                effectiveTier.ToString());
        }

        var actorIsPlayer = IsPlayerSoulActor(actorType, actorId);
        var targetIsPlayer = IsPlayerSoulActor(targetActorType, targetActorId);
        if (actorIsPlayer && !PlayerWonConflictProof(conflictProof))
        {
            AddSoulDissipationIssue(
                issues,
                proofContext,
                "Игрок может развеять душу только после доказанной победы/успеха в этом spiritual conflict.",
                "afterlife_conflict_soul_dissipation_missing_victory_proof",
                "playerOutcome=won/victory/success or resolutionKind=player_victory/player_success",
                DescribeConflictOutcome(conflictProof));
        }
        else if (targetIsPlayer && !PlayerLostConflictProof(conflictProof))
        {
            AddSoulDissipationIssue(
                issues,
                proofContext,
                "Душу игрока можно развеять только после доказанного поражения/сдачи/уступки игрока.",
                "afterlife_conflict_soul_dissipation_missing_victory_proof",
                "playerOutcome=lost/surrendered/conceded or resolutionKind=player_loss/player_surrender/player_concession",
                DescribeConflictOutcome(conflictProof));
        }
        else if (!actorIsPlayer && !targetIsPlayer &&
                 string.IsNullOrWhiteSpace(GetFirstSoulDissipationString(proof, "victoryProof", "conflictVictoryProof")))
        {
            AddSoulDissipationIssue(
                issues,
                $"{proofContext}.victoryProof",
                "NPC-vs-NPC развеивание души требует явного victoryProof.",
                "afterlife_conflict_soul_dissipation_missing_victory_proof",
                "non-empty victoryProof/conflictVictoryProof",
                "missing");
        }

        if (targetIsPlayer)
            ValidatePlayerSoulDissipationTerminalGameOver(conflictProof, proof, proofId, proofContext, issues, soulDissipationContext);
    }

    private void ValidatePlayerSoulDissipationTerminalGameOver(
        JsonObject conflictProof,
        JsonObject proof,
        string? proofId,
        string proofContext,
        List<ValidationIssue> issues,
        AfterlifeSoulDissipationContext soulDissipationContext)
    {
        if (soulDissipationContext.CurrentSoulRoot?[AfterlifeSpiritualConflictState.TerminalGameOverProperty] is not JsonObject gameOver)
        {
            AddSoulDissipationIssue(
                issues,
                "game_state/meta/soul_state.json.terminalGameOver",
                "Развеивание души игрока должно материализовать terminalGameOver в soul_state.json.",
                "afterlife_conflict_player_soul_dissipation_missing_game_over",
                "terminalGameOver object linked to the soulDissipationProof",
                "missing");
            return;
        }

        var message = AfterlifeSpiritualConflictState.GetNodeString(gameOver["message"]);
        if (!string.Equals(message, AfterlifeSpiritualConflictState.TerminalSoulDissipationMessage, StringComparison.Ordinal))
        {
            AddSoulDissipationIssue(
                issues,
                "game_state/meta/soul_state.json.terminalGameOver.message",
                "terminalGameOver.message должен использовать точный текст окончательной смерти души.",
                "afterlife_conflict_player_soul_dissipation_game_over_message_mismatch",
                AfterlifeSpiritualConflictState.TerminalSoulDissipationMessage,
                message ?? "missing");
        }

        var state = AfterlifeSpiritualConflictState.GetNodeString(gameOver["state"]);
        if (!ConflictTokenEquals(state, AfterlifeSpiritualConflictState.TerminalSoulDissipationState))
        {
            AddSoulDissipationIssue(
                issues,
                "game_state/meta/soul_state.json.terminalGameOver.state",
                "terminalGameOver.state должен фиксировать soul_dispersed.",
                "afterlife_conflict_player_soul_dissipation_game_over_state_mismatch",
                AfterlifeSpiritualConflictState.TerminalSoulDissipationState,
                state ?? "missing");
        }

        var conflictId = AfterlifeSpiritualConflictState.GetNodeString(conflictProof["conflictId"]) ??
                         AfterlifeSpiritualConflictState.GetNodeString(conflictProof["id"]);
        var gameOverConflictId = AfterlifeSpiritualConflictState.GetNodeString(gameOver["conflictId"]);
        if (!string.IsNullOrWhiteSpace(conflictId) &&
            !string.Equals(gameOverConflictId, conflictId, StringComparison.OrdinalIgnoreCase))
        {
            AddSoulDissipationIssue(
                issues,
                "game_state/meta/soul_state.json.terminalGameOver.conflictId",
                "terminalGameOver.conflictId должен ссылаться на тот же conflictId.",
                "afterlife_conflict_player_soul_dissipation_game_over_conflict_mismatch",
                conflictId,
                gameOverConflictId ?? "missing");
        }

        var gameOverProofId = AfterlifeSpiritualConflictState.GetNodeString(gameOver["proofId"]);
        if (!string.IsNullOrWhiteSpace(proofId) &&
            !string.Equals(gameOverProofId, proofId, StringComparison.OrdinalIgnoreCase))
        {
            AddSoulDissipationIssue(
                issues,
                "game_state/meta/soul_state.json.terminalGameOver.proofId",
                "terminalGameOver.proofId должен ссылаться на soulDissipationProof.proofId.",
                "afterlife_conflict_player_soul_dissipation_game_over_proof_mismatch",
                proofId,
                gameOverProofId ?? "missing");
        }
    }

    private static void ValidateTerminalGameOverHasSoulDissipationProof(
        JsonObject conflictRoot,
        List<ValidationIssue> issues,
        AfterlifeSoulDissipationContext soulDissipationContext)
    {
        if (soulDissipationContext.CurrentSoulRoot?[AfterlifeSpiritualConflictState.TerminalGameOverProperty] is not JsonObject)
            return;

        if (HasPlayerSoulDissipationProof(conflictRoot))
            return;

        AddSoulDissipationIssue(
            issues,
            "game_state/meta/soul_state.json.terminalGameOver",
            "terminalGameOver нельзя записывать без связанного current recentConflicts[].soulDissipationProof по развеиванию души игрока.",
            "afterlife_conflict_player_soul_dissipation_unlinked_game_over",
            "recentConflicts[].soulDissipationProof with targetActorType=player_soul",
            "missing");
    }

    private static bool HasPlayerSoulDissipationProof(JsonObject conflictRoot)
    {
        if (conflictRoot["recentConflicts"] is not JsonArray recentConflicts)
            return false;

        return recentConflicts
            .OfType<JsonObject>()
            .Any(proof =>
            {
                if (proof[AfterlifeSpiritualConflictState.SoulDissipationProofProperty] is not JsonObject soulDissipationProof)
                    return false;

                var targetActorType = AfterlifeSpiritualConflictState.GetNodeString(soulDissipationProof["targetActorType"]);
                var targetActorId = AfterlifeSpiritualConflictState.GetNodeString(soulDissipationProof["targetActorId"]);
                return IsPlayerSoulActor(targetActorType, targetActorId);
            });
    }

    private static JsonObject? ResolveAfterlifeSoulDissipationProfile(
        AfterlifeSoulDissipationContext context,
        string? actorType,
        string? actorId)
    {
        var key = BuildAfterlifeSoulDissipationProfileKey(actorType, actorId);
        return key != null && context.AuthorityProfiles.TryGetValue(key, out var profile)
            ? profile
            : null;
    }

    private static string? BuildAfterlifeSoulDissipationProfileKey(string? actorType, string? actorId)
    {
        if (string.IsNullOrWhiteSpace(actorType) || string.IsNullOrWhiteSpace(actorId))
            return null;

        var normalizedActorType = IsPlayerSoulActor(actorType, actorId)
            ? "player_soul"
            : actorType.Trim();
        var normalizedActorId = IsPlayerSoulActor(actorType, actorId)
            ? "player_soul"
            : actorId.Trim();
        return $"{normalizedActorType}:{normalizedActorId}";
    }

    private static bool IsPlayerSoulActor(string? actorType, string? actorId)
    {
        if (ConflictTokenEquals(actorType, "player_soul", "player", "soul"))
            return true;

        return string.Equals(actorId, "player_soul", StringComparison.OrdinalIgnoreCase);
    }

    private static bool PlayerWonConflictProof(JsonObject proof)
    {
        var playerOutcome = AfterlifeSpiritualConflictState.GetNodeString(proof["playerOutcome"]);
        var resolutionKind = AfterlifeSpiritualConflictState.GetNodeString(proof["resolutionKind"]);
        return ConflictTokenEquals(playerOutcome, "won", "win", "victory", "success", "succeeded", "prevailed") ||
               ConflictTokenEquals(resolutionKind, "player_victory", "player_success", "player_win");
    }

    private static bool PlayerLostConflictProof(JsonObject proof)
    {
        var playerOutcome = AfterlifeSpiritualConflictState.GetNodeString(proof["playerOutcome"]);
        var resolutionKind = AfterlifeSpiritualConflictState.GetNodeString(proof["resolutionKind"]);
        return ConflictTokenEquals(playerOutcome, "lost", "loss", "defeat", "surrendered", "conceded") ||
               ConflictTokenEquals(resolutionKind, "player_loss", "player_surrender", "player_concession");
    }

    private static string? RequireSoulDissipationString(
        JsonObject root,
        string context,
        List<ValidationIssue> issues,
        string propertyName)
    {
        var value = AfterlifeSpiritualConflictState.GetNodeString(root[propertyName]);
        if (!string.IsNullOrWhiteSpace(value))
            return value;

        AddSoulDissipationIssue(
            issues,
            $"{context}.{propertyName}",
            $"soulDissipationProof.{propertyName} должен быть непустой строкой.",
            $"afterlife_conflict_soul_dissipation_missing_{propertyName}",
            "non-empty string",
            root.ContainsKey(propertyName) ? root[propertyName]?.ToJsonString() ?? "null" : "missing");
        return null;
    }

    private static string? GetFirstSoulDissipationString(JsonObject root, params string[] propertyNames)
    {
        foreach (var propertyName in propertyNames)
        {
            var value = AfterlifeSpiritualConflictState.GetNodeString(root[propertyName]);
            if (!string.IsNullOrWhiteSpace(value))
                return value;
        }

        return null;
    }

    private static string BuildActorDescription(string? actorType, string? actorId) =>
        $"{(string.IsNullOrWhiteSpace(actorType) ? "missing_actorType" : actorType)}/{(string.IsNullOrWhiteSpace(actorId) ? "missing_actorId" : actorId)}";

    private static string DescribeConflictOutcome(JsonObject proof) =>
        $"playerOutcome={AfterlifeSpiritualConflictState.GetNodeString(proof["playerOutcome"]) ?? "missing"}; resolutionKind={AfterlifeSpiritualConflictState.GetNodeString(proof["resolutionKind"]) ?? "missing"}";

    private static void AddSoulDissipationIssue(
        List<ValidationIssue> issues,
        string path,
        string message,
        string code,
        string? expected = null,
        string? actual = null)
    {
        issues.Add(new ValidationIssue(
            path,
            IssueSeverity.Error,
            message,
            code: code,
            section: "AfterlifeSpiritualConflict",
            expected: expected,
            actual: actual,
            repairHint: "Запиши Развеивание души только как финальный proof после победы/сдачи/разгрома, с профилями сущностей, достаточным tier и явным мотивом ГМа."));
    }

    private void ValidateConflictRewardAudit(
        JsonObject proof,
        string context,
        List<ValidationIssue> issues,
        AfterlifeConflictRewardContext rewardContext,
        HashSet<string> rewardConflictIds)
    {
        if (proof[AfterlifeSpiritualConflictState.RewardAuditProperty] is null)
        {
            if (ContainsRewardLikeFieldsWithoutAudit(proof))
            {
                issues.Add(new ValidationIssue(
                    context,
                    IssueSeverity.Error,
                    "Afterlife conflict reward должен быть записан только через rewardAudit.",
                    code: "afterlife_conflict_reward_missing_audit",
                    section: "AfterlifeSpiritualConflict",
                    expected: "rewardAudit object with realm/currency/baseAmount/challengeTier/multipliers/difficultyAudit/finalAmount/narrativeReason",
                    actual: "reward-like fields outside rewardAudit",
                    repairHint: "Перенеси награду в recentConflicts[].rewardAudit или убери reward-поля для no-reward closure."));
            }

            return;
        }

        if (proof[AfterlifeSpiritualConflictState.RewardAuditProperty] is not JsonObject rewardAudit)
        {
            issues.Add(new ValidationIssue(
                $"{context}.{AfterlifeSpiritualConflictState.RewardAuditProperty}",
                IssueSeverity.Error,
                "rewardAudit должен быть object.",
                code: "afterlife_conflict_reward_invalid_audit_shape",
                section: "AfterlifeSpiritualConflict",
                expected: "object",
                actual: proof[AfterlifeSpiritualConflictState.RewardAuditProperty]?.GetType().Name ?? "null"));
            return;
        }

        var rewardRealm = AfterlifeSpiritualConflictState.GetNodeString(rewardAudit["realm"]);
        var rewardRealmKey = AfterlifeSpiritualConflictState.NormalizeAfterlifeRealmKey(rewardRealm);
        if (rewardRealmKey == null)
        {
            AddRewardIssue(
                issues,
                $"{context}.rewardAudit.realm",
                "rewardAudit.realm должен быть Chaos Sea или Shining Abode.",
                "afterlife_conflict_reward_invalid_realm",
                "Chaos Sea or Shining Abode",
                string.IsNullOrWhiteSpace(rewardRealm) ? "missing/empty" : rewardRealm);
            return;
        }

        var proofRealm = AfterlifeSpiritualConflictState.GetNodeString(proof["realm"]);
        var proofRealmKey = AfterlifeSpiritualConflictState.NormalizeAfterlifeRealmKey(proofRealm);
        if (proofRealmKey != null && !string.Equals(proofRealmKey, rewardRealmKey, StringComparison.Ordinal))
        {
            AddRewardIssue(
                issues,
                $"{context}.rewardAudit.realm",
                "rewardAudit.realm должен совпадать с realm resolved conflict proof.",
                "afterlife_conflict_reward_realm_mismatch",
                proofRealm ?? "missing/empty",
                rewardRealm ?? "missing/empty");
        }

        var isCurrentTurnReward = IsCurrentTurnReward(proof, rewardAudit, rewardContext, context, issues);
        if (isCurrentTurnReward &&
            rewardContext.AuthorityRealmKey != null &&
            !string.Equals(rewardContext.AuthorityRealmKey, rewardRealmKey, StringComparison.Ordinal))
        {
            AddRewardIssue(
                issues,
                $"{context}.rewardAudit.realm",
                "Current-turn afterlife conflict reward должен совпадать с validated pre-turn authority realm.",
                "afterlife_conflict_reward_authority_realm_mismatch",
                rewardContext.AuthorityRealmKey,
                rewardRealmKey);
        }

        var expectedCurrency = ResolveRewardCurrencyForRealm(rewardRealmKey);
        var currency = AfterlifeSpiritualConflictState.GetNodeString(rewardAudit["currency"]);
        if (!string.Equals(currency, expectedCurrency, StringComparison.OrdinalIgnoreCase))
        {
            AddRewardIssue(
                issues,
                $"{context}.rewardAudit.currency",
                "rewardAudit.currency не соответствует realm награды.",
                "afterlife_conflict_reward_wrong_currency",
                expectedCurrency,
                string.IsNullOrWhiteSpace(currency) ? "missing/empty" : currency);
        }

        if (!RewardAllowedForConflictProof(proof, rewardAudit))
        {
            AddRewardIssue(
                issues,
                $"{context}.rewardAudit",
                "Этот terminal afterlife conflict outcome не может выдавать currency reward.",
                "afterlife_conflict_reward_not_allowed",
                "resolved contested player victory with diceAudit.outcomeBand=player_success|decisive_player_success",
                DescribeRewardOutcome(proof));
            return;
        }

        var conflictId = AfterlifeSpiritualConflictState.GetNodeString(proof["conflictId"]) ??
                         AfterlifeSpiritualConflictState.GetNodeString(proof["id"]);
        if (string.IsNullOrWhiteSpace(conflictId))
        {
            AddRewardIssue(
                issues,
                $"{context}.conflictId",
                "Reward-bearing recentConflicts[] proof должен иметь conflictId для anti-farm проверки.",
                "afterlife_conflict_reward_missing_conflict_id",
                "non-empty conflictId",
                "missing");
        }
        else if (!rewardConflictIds.Add(conflictId))
        {
            AddRewardIssue(
                issues,
                $"{context}.rewardAudit",
                "Один afterlife conflictId не может выдавать награду повторно в recentConflicts[].",
                "afterlife_conflict_reward_duplicate_conflict",
                "one rewardAudit per conflictId",
                conflictId);
        }

        var matchesPreTurnRewardConflict = isCurrentTurnReward &&
                                           !string.IsNullOrWhiteSpace(conflictId) &&
                                           !string.IsNullOrWhiteSpace(rewardContext.PreTurnActiveConflictId) &&
                                           string.Equals(conflictId, rewardContext.PreTurnActiveConflictId, StringComparison.OrdinalIgnoreCase);

        var expectedBaseAmount = ResolveRewardBaseAmount(rewardRealmKey);
        if (!TryGetJsonNodeInt(rewardAudit["baseAmount"], out var baseAmount) || baseAmount != expectedBaseAmount)
        {
            AddRewardIssue(
                issues,
                $"{context}.rewardAudit.baseAmount",
                "rewardAudit.baseAmount должен быть canonical base amount для realm/currency.",
                "afterlife_conflict_reward_base_amount_mismatch",
                expectedBaseAmount.ToString(),
                rewardAudit["baseAmount"]?.ToJsonString() ?? "missing");
        }

        var outcomeBand = AfterlifeSpiritualConflictState.GetNodeString((proof["diceAudit"] as JsonObject)?["outcomeBand"]);
        var expectedOutcomeMultiplier = ResolveRewardOutcomeMultiplierPercent(outcomeBand);
        if (!TryGetJsonNodeInt(rewardAudit["outcomeMultiplierPercent"], out var outcomeMultiplier) ||
            outcomeMultiplier != expectedOutcomeMultiplier)
        {
            AddRewardIssue(
                issues,
                $"{context}.rewardAudit.outcomeMultiplierPercent",
                "rewardAudit.outcomeMultiplierPercent должен соответствовать diceAudit.outcomeBand.",
                "afterlife_conflict_reward_outcome_multiplier_mismatch",
                expectedOutcomeMultiplier.ToString(),
                rewardAudit["outcomeMultiplierPercent"]?.ToJsonString() ?? "missing");
        }

        var auditSideModel = AfterlifeSpiritualConflictState.GetNodeString(rewardAudit["sideModel"]);
        var proofSideModel = AfterlifeSpiritualConflictState.GetNodeString(proof["sideModel"]);
        var sideModel = auditSideModel ?? proofSideModel;
        if (string.IsNullOrWhiteSpace(auditSideModel))
        {
            AddRewardIssue(
                issues,
                $"{context}.rewardAudit.sideModel",
                "rewardAudit.sideModel обязателен для deterministic challenge tier.",
                "afterlife_conflict_reward_missing_side_model",
                "direct_duel|assisted_duel|champion_duel",
                "missing/empty");
        }
        else if (!AfterlifeSpiritualConflictState.SideModels.Contains(auditSideModel))
        {
            AddRewardIssue(
                issues,
                $"{context}.rewardAudit.sideModel",
                "rewardAudit.sideModel должен быть supported side model.",
                "afterlife_conflict_reward_invalid_side_model",
                string.Join("/", AfterlifeSpiritualConflictState.SideModels.OrderBy(value => value, StringComparer.OrdinalIgnoreCase)),
                auditSideModel);
        }
        else if (!string.IsNullOrWhiteSpace(proofSideModel) &&
                 !string.Equals(auditSideModel, proofSideModel, StringComparison.OrdinalIgnoreCase))
        {
            AddRewardIssue(
                issues,
                $"{context}.rewardAudit.sideModel",
                "rewardAudit.sideModel должен совпадать с resolved conflict proof sideModel.",
                "afterlife_conflict_reward_side_model_mismatch",
                proofSideModel,
                auditSideModel);
        }

        if (matchesPreTurnRewardConflict &&
            !string.IsNullOrWhiteSpace(rewardContext.PreTurnSideModel) &&
            !string.IsNullOrWhiteSpace(auditSideModel) &&
            !string.Equals(auditSideModel, rewardContext.PreTurnSideModel, StringComparison.OrdinalIgnoreCase))
        {
            AddRewardIssue(
                issues,
                $"{context}.rewardAudit.sideModel",
                "Current-turn rewardAudit.sideModel должен совпадать с validated pre-turn activeConflict.sideModel.",
                "afterlife_conflict_reward_side_model_mismatch",
                rewardContext.PreTurnSideModel,
                auditSideModel);
        }

        var startingPosition = AfterlifeSpiritualConflictState.GetNodeString(rewardAudit["startingConflictPosition"]);
        if (string.IsNullOrWhiteSpace(startingPosition))
        {
            AddRewardIssue(
                issues,
                $"{context}.rewardAudit.startingConflictPosition",
                "rewardAudit.startingConflictPosition обязателен для risk multiplier.",
                "afterlife_conflict_reward_missing_starting_position",
                string.Join("/", AfterlifeSpiritualConflictState.ConflictPositions.OrderBy(value => value, StringComparer.OrdinalIgnoreCase)),
                "missing/empty");
        }
        else if (!AfterlifeSpiritualConflictState.ConflictPositions.Contains(startingPosition))
        {
            AddRewardIssue(
                issues,
                $"{context}.rewardAudit.startingConflictPosition",
                "rewardAudit.startingConflictPosition должен быть supported conflictPosition.",
                "afterlife_conflict_reward_invalid_starting_position",
                string.Join("/", AfterlifeSpiritualConflictState.ConflictPositions.OrderBy(value => value, StringComparer.OrdinalIgnoreCase)),
                startingPosition);
        }

        if (matchesPreTurnRewardConflict &&
            !string.IsNullOrWhiteSpace(rewardContext.PreTurnConflictPosition) &&
            !string.IsNullOrWhiteSpace(startingPosition) &&
            !string.Equals(startingPosition, rewardContext.PreTurnConflictPosition, StringComparison.OrdinalIgnoreCase))
        {
            AddRewardIssue(
                issues,
                $"{context}.rewardAudit.startingConflictPosition",
                "Current-turn rewardAudit.startingConflictPosition должен совпадать с validated pre-turn activeConflict.conflictPosition.",
                "afterlife_conflict_reward_starting_position_mismatch",
                rewardContext.PreTurnConflictPosition,
                startingPosition);
        }

        var hasOpposingLeadStrength = TryGetJsonNodeInt(rewardAudit["opposingLeadStrength"], out var opposingLeadStrength);
        if (!hasOpposingLeadStrength || opposingLeadStrength <= 0)
        {
            AddRewardIssue(
                issues,
                $"{context}.rewardAudit.opposingLeadStrength",
                "rewardAudit.opposingLeadStrength должен быть positive integer.",
                "afterlife_conflict_reward_missing_opposing_strength",
                "positive integer derived from opposition lead art/authority snapshot",
                rewardAudit["opposingLeadStrength"]?.ToJsonString() ?? "missing");
        }
        else if (matchesPreTurnRewardConflict &&
                 rewardContext.PreTurnOpposingLeadStrength is int expectedOpposingLeadStrength &&
                 opposingLeadStrength != expectedOpposingLeadStrength)
        {
            AddRewardIssue(
                issues,
                $"{context}.rewardAudit.opposingLeadStrength",
                "Current-turn rewardAudit.opposingLeadStrength должен совпадать с validated pre-turn opposition lead art snapshot.",
                "afterlife_conflict_reward_opposing_strength_mismatch",
                expectedOpposingLeadStrength.ToString(),
                opposingLeadStrength.ToString());
        }

        var expectedChallengeTier = ResolveRewardChallengeTier(opposingLeadStrength, sideModel, startingPosition);
        if (!TryGetJsonNodeInt(rewardAudit["challengeTier"], out var challengeTier) ||
            challengeTier != expectedChallengeTier)
        {
            AddRewardIssue(
                issues,
                $"{context}.rewardAudit.challengeTier",
                "rewardAudit.challengeTier должен быть deterministic tier из opposingLeadStrength + sideModel + startingConflictPosition.",
                "afterlife_conflict_reward_challenge_tier_mismatch",
                expectedChallengeTier.ToString(),
                rewardAudit["challengeTier"]?.ToJsonString() ?? "missing");
        }

        var expectedRiskMultiplier = ResolveRewardRiskMultiplierPercent(startingPosition);
        if (!TryGetJsonNodeInt(rewardAudit["riskMultiplierPercent"], out var riskMultiplier) ||
            riskMultiplier != expectedRiskMultiplier)
        {
            AddRewardIssue(
                issues,
                $"{context}.rewardAudit.riskMultiplierPercent",
                "rewardAudit.riskMultiplierPercent должен соответствовать startingConflictPosition.",
                "afterlife_conflict_reward_risk_multiplier_mismatch",
                expectedRiskMultiplier.ToString(),
                rewardAudit["riskMultiplierPercent"]?.ToJsonString() ?? "missing");
        }

        var riskReason = AfterlifeSpiritualConflictState.GetNodeString(rewardAudit["riskReason"]);
        if (string.IsNullOrWhiteSpace(riskReason))
        {
            AddRewardIssue(
                issues,
                $"{context}.rewardAudit.riskReason",
                "rewardAudit.riskReason должен объяснять стартовый риск/позицию.",
                "afterlife_conflict_reward_missing_risk_reason",
                "non-empty riskReason",
                "missing/empty");
        }

        var narrativeReason = AfterlifeSpiritualConflictState.GetNodeString(rewardAudit["narrativeReason"]) ??
                              AfterlifeSpiritualConflictState.GetNodeString(rewardAudit["reason"]);
        if (string.IsNullOrWhiteSpace(narrativeReason))
        {
            AddRewardIssue(
                issues,
                $"{context}.rewardAudit.narrativeReason",
                "rewardAudit.narrativeReason должен объяснять, за что получена награда.",
                "afterlife_conflict_reward_missing_narrative_reason",
                "non-empty narrativeReason",
                "missing/empty");
        }

        var expectedDifficultyMultiplier = ValidateAfterlifeConflictRewardDifficultyAudit(
            rewardAudit,
            context,
            issues,
            rewardContext);
        var expectedFinalAmount = ResolveRewardFinalAmount(
            expectedBaseAmount,
            expectedChallengeTier,
            expectedOutcomeMultiplier,
            expectedRiskMultiplier,
            expectedDifficultyMultiplier,
            rewardRealmKey);
        if (!TryGetJsonNodeInt(rewardAudit["finalAmount"], out var finalAmount))
        {
            AddRewardIssue(
                issues,
                $"{context}.rewardAudit.finalAmount",
                "rewardAudit.finalAmount должен быть integer.",
                "afterlife_conflict_reward_final_amount_mismatch",
                expectedFinalAmount.ToString(),
                rewardAudit["finalAmount"]?.ToJsonString() ?? "missing");
            return;
        }

        var cap = ResolveRewardMaxAmount(rewardRealmKey);
        if (finalAmount > cap)
        {
            AddRewardIssue(
                issues,
                $"{context}.rewardAudit.finalAmount",
                "rewardAudit.finalAmount превышает cap для realm/currency.",
                "afterlife_conflict_reward_amount_over_cap",
                $"<= {cap}",
                finalAmount.ToString());
            return;
        }

        if (finalAmount != expectedFinalAmount)
        {
            AddRewardIssue(
                issues,
                $"{context}.rewardAudit.finalAmount",
                "rewardAudit.finalAmount должен совпадать с deterministic reward formula.",
                "afterlife_conflict_reward_final_amount_mismatch",
                expectedFinalAmount.ToString(),
                finalAmount.ToString());
            return;
        }

        if (!isCurrentTurnReward)
            return;

        if (string.Equals(expectedCurrency, AfterlifeSpiritualConflictState.RewardCurrencyInkFeathers, StringComparison.OrdinalIgnoreCase))
        {
            rewardContext.HasCurrentTurnInkFeatherRewardAudit = true;
            rewardContext.ExpectedCurrentTurnInkFeatherReward += finalAmount;
        }
        else if (string.Equals(expectedCurrency, AfterlifeSpiritualConflictState.RewardCurrencyLightSparks, StringComparison.OrdinalIgnoreCase))
        {
            rewardContext.HasCurrentTurnLightSparkRewardAudit = true;
            rewardContext.ExpectedCurrentTurnLightSparkReward += finalAmount;
        }
    }

    private static bool ContainsRewardLikeFieldsWithoutAudit(JsonObject proof)
    {
        foreach (var fieldName in new[]
                 {
                     "reward", "rewardCurrency", "currencyReward", "rewardAmount",
                     "currencyAmount", "inkFeathersAwarded", "lightSparksAwarded"
                 })
        {
            if (proof.ContainsKey(fieldName))
                return true;
        }

        return false;
    }

    private static bool RewardAllowedForConflictProof(JsonObject proof, JsonObject rewardAudit)
    {
        var mode = AfterlifeSpiritualConflictState.GetNodeString(proof["mode"]);
        var resolutionState = AfterlifeSpiritualConflictState.GetNodeString(proof["resolutionState"]) ??
                              AfterlifeSpiritualConflictState.GetNodeString(proof["status"]);
        if (!ConflictTokenEquals(mode, AfterlifeSpiritualConflictState.ModeResolve) ||
            !ConflictTokenEquals(resolutionState, "resolved"))
        {
            return false;
        }

        if (IsExplicitVoluntaryNonContest(proof))
            return false;

        var operationType = AfterlifeSpiritualConflictState.GetNodeString(proof["operationType"]) ??
                            AfterlifeSpiritualConflictState.GetNodeString(proof["finalOperationType"]);
        if (ConflictTokenEquals(operationType, "withdraw", "surrender", "negotiate"))
            return false;

        var outcome = AfterlifeSpiritualConflictState.GetNodeString(proof["outcome"]) ??
                      AfterlifeSpiritualConflictState.GetNodeString(proof["result"]);
        if (ConflictTokenEquals(outcome, "no_effect", "blocked"))
            return false;

        var outcomeBand = AfterlifeSpiritualConflictState.GetNodeString((proof["diceAudit"] as JsonObject)?["outcomeBand"]);
        if (!ConflictTokenEquals(outcomeBand, "player_success", "decisive_player_success"))
            return false;

        var playerOutcome = AfterlifeSpiritualConflictState.GetNodeString(proof["playerOutcome"]);
        if (!string.IsNullOrWhiteSpace(playerOutcome) &&
            !ConflictTokenEquals(playerOutcome, "won", "win", "victory", "success", "prevailed"))
        {
            return false;
        }

        var farmRepeat = rewardAudit["farmRepeat"] as JsonValue;
        if (farmRepeat != null && farmRepeat.TryGetValue<bool>(out var isFarmRepeat) && isFarmRepeat)
            return false;

        return true;
    }

    private static bool IsCurrentTurnReward(
        JsonObject proof,
        JsonObject rewardAudit,
        AfterlifeConflictRewardContext rewardContext,
        string context,
        List<ValidationIssue> issues)
    {
        if (rewardContext.CurrentTurn is not > 0)
            return false;

        var proofTurn = AfterlifeSpiritualConflictState.GetNodeInt(
            proof["resolvedAtTurn"],
            AfterlifeSpiritualConflictState.GetNodeInt(proof["turnNumber"]));
        var hasRewardAuditTurn = TryGetJsonNodeInt(rewardAudit["resolvedAtTurn"], out var rewardAuditTurn);
        if (proofTurn > 0 &&
            hasRewardAuditTurn &&
            rewardAuditTurn != proofTurn)
        {
            AddRewardIssue(
                issues,
                $"{context}.rewardAudit.resolvedAtTurn",
                "rewardAudit.resolvedAtTurn должен совпадать с resolved conflict proof turn.",
                "afterlife_conflict_reward_turn_mismatch",
                proofTurn.ToString(),
                rewardAuditTurn.ToString());
        }

        var effectiveRewardTurn = proofTurn > 0
            ? proofTurn
            : hasRewardAuditTurn
                ? rewardAuditTurn
                : 0;
        return effectiveRewardTurn == rewardContext.CurrentTurn.Value;
    }

    private static string ResolveRewardCurrencyForRealm(string rewardRealmKey) =>
        string.Equals(rewardRealmKey, "shining_abode", StringComparison.Ordinal)
            ? AfterlifeSpiritualConflictState.RewardCurrencyLightSparks
            : AfterlifeSpiritualConflictState.RewardCurrencyInkFeathers;

    private static int ResolveRewardBaseAmount(string rewardRealmKey) =>
        string.Equals(rewardRealmKey, "shining_abode", StringComparison.Ordinal)
            ? AfterlifeSpiritualConflictState.ShiningConflictRewardBaseAmount
            : AfterlifeSpiritualConflictState.ChaosSeaConflictRewardBaseAmount;

    private static int ResolveRewardMaxAmount(string rewardRealmKey) =>
        string.Equals(rewardRealmKey, "shining_abode", StringComparison.Ordinal)
            ? AfterlifeSpiritualConflictState.ShiningConflictRewardMaxAmount
            : AfterlifeSpiritualConflictState.ChaosSeaConflictRewardMaxAmount;

    private static int ResolveRewardOutcomeMultiplierPercent(string? outcomeBand) =>
        ConflictTokenEquals(outcomeBand, "decisive_player_success") ? 150 :
        ConflictTokenEquals(outcomeBand, "player_success") ? 100 :
        0;

    private static int ResolveRewardRiskMultiplierPercent(string? startingPosition) =>
        startingPosition?.Trim().ToLowerInvariant() switch
        {
            "opposition_dominant" => 150,
            "opposition_advantaged" => 125,
            "contested" => 100,
            "player_advantaged" => 75,
            "player_dominant" => 50,
            _ => 100
        };

    private static int ResolveRewardChallengeTier(int opposingLeadStrength, string? sideModel, string? startingPosition)
    {
        var strengthTier = opposingLeadStrength switch
        {
            <= 0 => 1,
            <= 2 => 1,
            <= 5 => 2,
            <= 8 => 3,
            <= 11 => 4,
            _ => 5
        };
        var sideModelAdjustment = sideModel?.Trim().ToLowerInvariant() switch
        {
            "direct_duel" => 1,
            "assisted_duel" => 0,
            "champion_duel" => 0,
            _ => 0
        };
        var positionAdjustment = startingPosition?.Trim().ToLowerInvariant() switch
        {
            "opposition_dominant" => 2,
            "opposition_advantaged" => 1,
            "contested" => 0,
            "player_advantaged" => -1,
            "player_dominant" => -2,
            _ => 0
        };

        return Math.Clamp(
            strengthTier + sideModelAdjustment + positionAdjustment,
            1,
            AfterlifeSpiritualConflictState.ConflictRewardMaxChallengeTier);
    }

    private static int ResolveRewardFinalAmount(
        int baseAmount,
        int challengeTier,
        int outcomeMultiplierPercent,
        int riskMultiplierPercent,
        int difficultyRewardMultiplierPercent,
        string rewardRealmKey)
    {
        if (baseAmount <= 0 ||
            challengeTier <= 0 ||
            outcomeMultiplierPercent <= 0 ||
            riskMultiplierPercent <= 0 ||
            difficultyRewardMultiplierPercent <= 0)
        {
            return 0;
        }

        var raw = (long)baseAmount *
                  challengeTier *
                  outcomeMultiplierPercent *
                  riskMultiplierPercent *
                  difficultyRewardMultiplierPercent /
                  1_000_000L;
        return (int)Math.Clamp(raw, 0, ResolveRewardMaxAmount(rewardRealmKey));
    }

    private static int ValidateAfterlifeConflictRewardDifficultyAudit(
        JsonObject rewardAudit,
        string context,
        List<ValidationIssue> issues,
        AfterlifeConflictRewardContext rewardContext)
    {
        if (rewardContext.Difficulty == null)
        {
            if (rewardAudit.ContainsKey("difficultyAudit"))
            {
                AddRewardIssue(
                    issues,
                    $"{context}.rewardAudit.difficultyAudit",
                    "rewardAudit.difficultyAudit допустим только при readable game_settings difficulty.",
                    "afterlife_conflict_reward_difficulty_without_settings",
                    "readable game_state/core/game_settings.json.difficulty",
                    rewardAudit["difficultyAudit"]?.ToJsonString() ?? "missing");
            }

            return 100;
        }

        if (rewardAudit["difficultyAudit"] is not JsonObject difficultyAudit)
        {
            AddRewardIssue(
                issues,
                $"{context}.rewardAudit.difficultyAudit",
                "rewardAudit должен фиксировать difficultyAudit для множителя награды.",
                "afterlife_conflict_reward_difficulty_audit_missing",
                "difficultyAudit object",
                rewardAudit["difficultyAudit"]?.GetType().Name ?? "missing");
            return rewardContext.Difficulty.RewardMultiplierPercent;
        }

        var difficulty = AfterlifeSpiritualConflictState.GetNodeString(difficultyAudit["difficulty"]);
        if (!string.Equals(difficulty, rewardContext.Difficulty.Difficulty, StringComparison.OrdinalIgnoreCase))
        {
            AddRewardIssue(
                issues,
                $"{context}.rewardAudit.difficultyAudit.difficulty",
                "rewardAudit.difficultyAudit.difficulty должен совпадать с game_settings difficulty.",
                "afterlife_conflict_reward_difficulty_mismatch",
                rewardContext.Difficulty.Difficulty,
                string.IsNullOrWhiteSpace(difficulty) ? "missing/empty" : difficulty);
        }

        var source = AfterlifeSpiritualConflictState.GetNodeString(difficultyAudit["source"]);
        if (!string.Equals(source, $"{AfterlifeSpiritualConflictState.DifficultySettingsPath}.difficulty", StringComparison.Ordinal))
        {
            AddRewardIssue(
                issues,
                $"{context}.rewardAudit.difficultyAudit.source",
                "rewardAudit.difficultyAudit.source должен ссылаться на authoritative game_settings difficulty.",
                "afterlife_conflict_reward_difficulty_source_mismatch",
                $"{AfterlifeSpiritualConflictState.DifficultySettingsPath}.difficulty",
                string.IsNullOrWhiteSpace(source) ? "missing/empty" : source);
        }

        if (!TryGetJsonNodeInt(difficultyAudit["oppositionModifier"], out var oppositionModifier) ||
            oppositionModifier != rewardContext.Difficulty.OppositionDiceModifier)
        {
            AddRewardIssue(
                issues,
                $"{context}.rewardAudit.difficultyAudit.oppositionModifier",
                "rewardAudit.difficultyAudit.oppositionModifier должен совпадать с таблицей сложности.",
                "afterlife_conflict_reward_difficulty_opposition_modifier_mismatch",
                rewardContext.Difficulty.OppositionDiceModifier.ToString(),
                difficultyAudit["oppositionModifier"]?.ToJsonString() ?? "missing");
        }

        if (!TryGetJsonNodeInt(difficultyAudit["rewardMultiplierPercent"], out var rewardMultiplier) ||
            rewardMultiplier != rewardContext.Difficulty.RewardMultiplierPercent)
        {
            AddRewardIssue(
                issues,
                $"{context}.rewardAudit.difficultyAudit.rewardMultiplierPercent",
                "rewardAudit.difficultyAudit.rewardMultiplierPercent должен совпадать с таблицей сложности.",
                "afterlife_conflict_reward_difficulty_multiplier_mismatch",
                rewardContext.Difficulty.RewardMultiplierPercent.ToString(),
                difficultyAudit["rewardMultiplierPercent"]?.ToJsonString() ?? "missing");
        }

        return rewardContext.Difficulty.RewardMultiplierPercent;
    }

    private static string DescribeRewardOutcome(JsonObject proof)
    {
        var mode = AfterlifeSpiritualConflictState.GetNodeString(proof["mode"]) ?? "missing_mode";
        var operation = AfterlifeSpiritualConflictState.GetNodeString(proof["operationType"]) ??
                        AfterlifeSpiritualConflictState.GetNodeString(proof["finalOperationType"]) ??
                        "missing_operation";
        var outcomeBand = AfterlifeSpiritualConflictState.GetNodeString((proof["diceAudit"] as JsonObject)?["outcomeBand"]) ?? "missing_outcomeBand";
        var playerOutcome = AfterlifeSpiritualConflictState.GetNodeString(proof["playerOutcome"]) ?? "missing_playerOutcome";
        return $"mode={mode}; operationType={operation}; outcomeBand={outcomeBand}; playerOutcome={playerOutcome}";
    }

    private static void ValidateAfterlifeConflictRewardStateDeltas(
        AfterlifeConflictRewardContext rewardContext,
        List<ValidationIssue> issues)
    {
        if (rewardContext.HasCurrentTurnInkFeatherRewardAudit)
        {
            if (rewardContext.PreTurnInkFeathers == null || rewardContext.CurrentInkFeathers == null)
            {
                AddRewardIssue(
                    issues,
                    "game_state/meta/soul_state.json.inkFeathers",
                    "Current-turn afterlife conflict Ink Feather reward требует pre-turn/current soul_state baseline.",
                    "afterlife_conflict_reward_missing_currency_baseline",
                    "validated pre-turn and current soul_state.inkFeathers",
                    "missing/unreadable");
            }
            else
            {
                var actualDelta = rewardContext.CurrentInkFeathers.Value - rewardContext.PreTurnInkFeathers.Value;
                if (actualDelta != rewardContext.ExpectedCurrentTurnInkFeatherReward)
                {
                    AddRewardIssue(
                        issues,
                        "game_state/meta/soul_state.json.inkFeathers",
                        "Ink Feather rewardAudit.finalAmount должен совпадать с фактической дельтой валюты.",
                        "afterlife_conflict_reward_currency_delta_mismatch",
                        rewardContext.ExpectedCurrentTurnInkFeatherReward.ToString(),
                        actualDelta.ToString());
                }
            }
        }

        if (rewardContext.HasCurrentTurnLightSparkRewardAudit)
        {
            if (rewardContext.PreTurnLightSparks == null || rewardContext.CurrentLightSparks == null)
            {
                AddRewardIssue(
                    issues,
                    $"{ShiningAbodeState.StatePath}.lightSparks",
                    "Current-turn afterlife conflict Light Spark reward требует pre-turn/current shining_abode_state baseline.",
                    "afterlife_conflict_reward_missing_currency_baseline",
                    "validated pre-turn and current shining_abode_state.lightSparks",
                    "missing/unreadable");
            }
            else
            {
                var actualDelta = rewardContext.CurrentLightSparks.Value - rewardContext.PreTurnLightSparks.Value;
                if (actualDelta != rewardContext.ExpectedCurrentTurnLightSparkReward)
                {
                    AddRewardIssue(
                        issues,
                        $"{ShiningAbodeState.StatePath}.lightSparks",
                        "Light Spark rewardAudit.finalAmount должен совпадать с фактической дельтой валюты.",
                        "afterlife_conflict_reward_currency_delta_mismatch",
                        rewardContext.ExpectedCurrentTurnLightSparkReward.ToString(),
                        actualDelta.ToString());
                }
            }
        }
    }

    private static void AddRewardIssue(
        List<ValidationIssue> issues,
        string path,
        string message,
        string code,
        string expected,
        string actual)
    {
        issues.Add(new ValidationIssue(
            path,
            IssueSeverity.Error,
            message,
            code: code,
            section: "AfterlifeSpiritualConflict",
            expected: expected,
            actual: actual));
    }

    private void ValidateConflictExchange(
        JsonObject exchange,
        JsonNode? priorControlState,
        JsonObject activeConflict,
        JsonObject? activeActionEconomy,
        string context,
        List<ValidationIssue> issues,
        AfterlifeConflictDiceContext diceContext,
        AfterlifeActionCostAuthorityContext actionCostAuthority,
        bool isPreTurnExchange)
    {
        RequireNodeString(exchange, context, issues, "exchangeId");
        var operationType = ValidateEnumNode(exchange, context, issues, "operationType", AfterlifeSpiritualConflictState.OperationTypes, "afterlife_conflict_invalid_operation_type");
        var outcome = ValidateEnumNode(exchange, context, issues, "outcome", AfterlifeSpiritualConflictState.OperationOutcomes, "afterlife_conflict_invalid_operation_outcome");

        if (string.Equals(outcome, "blocked", StringComparison.OrdinalIgnoreCase) &&
            exchange["incomingAction"] is not JsonObject)
        {
            issues.Add(new ValidationIssue(
                $"{context}.incomingAction",
                IssueSeverity.Error,
                "blocked exchange должен явно описывать incomingAction, который был предотвращён.",
                code: "afterlife_conflict_blocked_missing_incoming_action",
                section: "AfterlifeSpiritualConflict",
                expected: "incomingAction object",
                actual: exchange["incomingAction"]?.GetType().Name ?? "missing"));
        }

        if (string.Equals(outcome, "countered", StringComparison.OrdinalIgnoreCase) &&
            exchange["incomingAction"] is not JsonObject)
        {
            issues.Add(new ValidationIssue(
                $"{context}.incomingAction",
                IssueSeverity.Error,
                "countered exchange должен явно описывать incomingAction, который был отражён.",
                code: "afterlife_conflict_countered_missing_incoming_action",
                section: "AfterlifeSpiritualConflict",
                expected: "incomingAction object",
                actual: exchange["incomingAction"]?.GetType().Name ?? "missing"));
        }

        var before = exchange["before"] as JsonObject;
        if (before == null)
        {
            issues.Add(new ValidationIssue(
                $"{context}.before",
                IssueSeverity.Error,
                "exchange.before должен быть audit snapshot object.",
                code: "afterlife_conflict_exchange_missing_before",
                section: "AfterlifeSpiritualConflict",
                expected: "before object",
                actual: exchange["before"]?.GetType().Name ?? "missing"));
        }

        var after = exchange["after"] as JsonObject;
        if (after == null)
        {
            issues.Add(new ValidationIssue(
                $"{context}.after",
                IssueSeverity.Error,
                "exchange.after должен быть audit snapshot object.",
                code: "afterlife_conflict_exchange_missing_after",
                section: "AfterlifeSpiritualConflict",
                expected: "after object",
                actual: exchange["after"]?.GetType().Name ?? "missing"));
        }

        if (before != null &&
            after != null &&
            string.Equals(outcome, "no_effect", StringComparison.OrdinalIgnoreCase) &&
            ExchangeSnapshotsChangedSemantically(before, after))
        {
            issues.Add(new ValidationIssue(
                $"{context}.after",
                IssueSeverity.Error,
                "no_effect exchange не должен менять before/after audit snapshot.",
                code: "afterlife_conflict_no_effect_has_state_delta",
                section: "AfterlifeSpiritualConflict",
                expected: "before == after for outcome=no_effect",
                actual: "before != after"));
        }

        if (!string.Equals(outcome, "blocked", StringComparison.OrdinalIgnoreCase) &&
            !string.Equals(outcome, "no_effect", StringComparison.OrdinalIgnoreCase) &&
            before != null &&
            after != null &&
            !ExchangeSnapshotsChangedSemantically(before, after))
        {
            issues.Add(new ValidationIssue(
                $"{context}.after",
                IssueSeverity.Error,
                "Non-blocked exchange должен иметь измеримое изменение или явно быть outcome=no_effect.",
                code: "afterlife_conflict_exchange_no_state_delta",
                section: "AfterlifeSpiritualConflict",
                expected: "changed before/after or outcome=blocked/no_effect",
                actual: "before == after"));
        }

        var diceRequired = ExchangeDiceAuditRequired(exchange, outcome);
        var isCurrentExchange = diceContext.HasValidatedTurnBaseline && !isPreTurnExchange;
        var requiresCurrentMatchupAudit =
            exchange["diceAudit"] is JsonObject &&
            isCurrentExchange;
        ValidateSpecialArtAudit(exchange, operationType, actionCostAuthority, context, issues);
        ValidateActionCostAudit(exchange, activeConflict, activeActionEconomy, operationType, outcome, context, issues, isCurrentExchange, actionCostAuthority);

        if (before != null && after != null)
        {
            ValidateControlStateShape(before["controlState"], $"{context}.before.controlState", issues, required: false);
            ValidateControlStateShape(after["controlState"], $"{context}.after.controlState", issues, required: false);
            ValidateCurrentExchangeControlSnapshotCompleteness(
                priorControlState,
                before,
                after,
                context,
                issues,
                isCurrentExchange);
            ValidateSpiritualArtOperationRules(
                exchange,
                before,
                after,
                operationType,
                outcome,
                context,
                issues,
                isCurrentExchange,
                requiresCurrentMatchupAudit);
        }

        if (diceRequired && exchange["diceAudit"] is not JsonObject)
        {
            issues.Add(new ValidationIssue(
                $"{context}.diceAudit",
                IssueSeverity.Error,
                "Contested afterlife conflict exchange требует diceAudit.",
                code: "afterlife_conflict_exchange_missing_dice_audit",
                section: "AfterlifeSpiritualConflict",
                expected: "exchange.diceAudit with current turn preGeneratedDices1d20 source indices",
                actual: exchange["diceAudit"]?.GetType().Name ?? "missing"));
        }

        if (exchange["diceAudit"] is JsonObject diceAudit)
        {
            ValidateAfterlifeConflictDiceAudit(diceAudit, $"{context}.diceAudit", issues, diceContext);
            if (before != null)
                ValidateConflictPositionDiceModifier(diceAudit, before, context, issues);
            ValidateLightIncarnateDiceAuditModifier(exchange, diceAudit, $"{context}.diceAudit", issues, diceContext);
        }
    }

    private static void ValidateActionEconomyShape(
        JsonNode? actionEconomy,
        string context,
        List<ValidationIssue> issues,
        bool required)
    {
        if (actionEconomy == null)
        {
            if (required)
            {
                issues.Add(new ValidationIssue(
                    context,
                    IssueSeverity.Error,
                    "Активный духовный конфликт с новым обменом должен содержать actionEconomy для ОД обеих сторон.",
                    code: "afterlife_conflict_action_economy_missing",
                    section: "AfterlifeSpiritualConflict",
                    expected: "actionEconomy object with player/opposition current/max",
                    actual: "missing/null"));
            }

            return;
        }

        if (actionEconomy is not JsonObject actionEconomyObject)
        {
            issues.Add(new ValidationIssue(
                context,
                IssueSeverity.Error,
                "actionEconomy должен быть object.",
                code: "afterlife_conflict_action_economy_invalid",
                section: "AfterlifeSpiritualConflict",
                expected: "object",
                actual: actionEconomy.GetType().Name));
            return;
        }

        ValidateActionPoolShape(actionEconomyObject["player"], $"{context}.player", issues);
        ValidateActionPoolShape(actionEconomyObject["opposition"], $"{context}.opposition", issues);
    }

    private static void ValidateActionPoolShape(JsonNode? pool, string context, List<ValidationIssue> issues)
    {
        if (pool is not JsonObject poolObject)
        {
            issues.Add(new ValidationIssue(
                context,
                IssueSeverity.Error,
                "Пул ОД должен быть object с current/max.",
                code: "afterlife_conflict_action_pool_invalid",
                section: "AfterlifeSpiritualConflict",
                expected: "object with current/max/source",
                actual: pool?.GetType().Name ?? "missing"));
            return;
        }

        var hasCurrent = TryGetJsonNodeInt(poolObject["current"], out var current);
        var hasMax = TryGetJsonNodeInt(poolObject["max"], out var max);
        if (!hasCurrent || !hasMax || current < 0 || max < 0 || current > max)
        {
            issues.Add(new ValidationIssue(
                context,
                IssueSeverity.Error,
                "Пул ОД должен иметь 0 <= current <= max.",
                code: "afterlife_conflict_action_pool_bounds_invalid",
                section: "AfterlifeSpiritualConflict",
                expected: "0 <= current <= max",
                actual: $"current={poolObject["current"]?.ToJsonString() ?? "missing"}, max={poolObject["max"]?.ToJsonString() ?? "missing"}"));
        }
    }

    private static bool ExchangeExpectsPlayerActionCostAudit(JsonObject exchange) =>
        OperationHasActionCost(AfterlifeSpiritualConflictState.GetNodeString(exchange["operationType"]));

    private static bool ExchangeExpectsOppositionActionCostAudit(JsonObject exchange) =>
        OperationHasActionCost(ResolveOppositionOperationForActionCost(exchange));

    private static bool HasActionCostAuditSide(JsonObject exchange, string side) =>
        exchange["actionCostAudit"] is JsonObject actionCostAudit &&
        actionCostAudit.ContainsKey(side);

    private static void ValidateActionCostAudit(
        JsonObject exchange,
        JsonObject activeConflict,
        JsonObject? activeActionEconomy,
        string? operationType,
        string? outcome,
        string context,
        List<ValidationIssue> issues,
        bool isCurrentExchange,
        AfterlifeActionCostAuthorityContext actionCostAuthority)
    {
        if (!isCurrentExchange)
        {
            return;
        }

        ValidateOppositionActionCostAudit(exchange, activeConflict, activeActionEconomy, context, issues, actionCostAuthority);

        if (!OperationHasActionCost(operationType))
        {
            if (HasActionCostAuditSide(exchange, "player"))
            {
                AddActionCostIssue(
                    issues,
                    $"{context}.actionCostAudit.player",
                    "Терминальные/бесплатные духовные действия не должны иметь actionCostAudit.player и не могут менять ОД через fake-аудит.",
                    "afterlife_conflict_action_cost_audit_unexpected",
                    "missing actionCostAudit.player for no-cost operation",
                    exchange["actionCostAudit"]?.ToJsonString() ?? "missing");
            }

            return;
        }

        var resolvedPlayerOperation = operationType!;
        if (!AfterlifeActionCosts.TryGetValue(resolvedPlayerOperation, out var costDefinition))
            return;

        if (exchange["actionCostAudit"] is not JsonObject actionCostAudit ||
            actionCostAudit["player"] is not JsonObject playerAudit)
        {
            issues.Add(new ValidationIssue(
                $"{context}.actionCostAudit.player",
                IssueSeverity.Error,
                "Новый обмен духовного боя должен иметь actionCostAudit.player для стоимости/восстановления ОД.",
                code: "afterlife_conflict_action_cost_audit_missing",
                section: "AfterlifeSpiritualConflict",
                expected: "actionCostAudit.player object",
                actual: exchange["actionCostAudit"]?.GetType().Name ?? "missing"));
            return;
        }

        var auditOperation = AfterlifeSpiritualConflictState.GetNodeString(playerAudit["operationType"]);
        if (!ConflictTokenEquals(auditOperation, resolvedPlayerOperation))
        {
            AddActionCostIssue(
                issues,
                $"{context}.actionCostAudit.player.operationType",
                "actionCostAudit.player.operationType должен совпадать с exchange.operationType.",
                "afterlife_conflict_action_cost_mismatch",
                resolvedPlayerOperation,
                string.IsNullOrWhiteSpace(auditOperation) ? "missing" : auditOperation);
        }

        var hasBaseCost = TryGetJsonNodeInt(playerAudit["baseCost"], out var baseCost);
        var hasMinCost = TryGetJsonNodeInt(playerAudit["minCost"], out var minCost);
        var hasArtTier = TryGetJsonNodeInt(playerAudit["artTier"], out var artTier);
        var hasEffectiveCost = TryGetJsonNodeInt(playerAudit["effectiveCost"], out var effectiveCost);
        var hasBefore = TryGetJsonNodeInt(playerAudit["before"], out var before);
        var hasAfter = TryGetJsonNodeInt(playerAudit["after"], out var after);
        if (!hasBaseCost || !hasMinCost || !hasArtTier || !hasEffectiveCost || !hasBefore || !hasAfter)
        {
            AddActionCostIssue(
                issues,
                $"{context}.actionCostAudit.player",
                "actionCostAudit.player должен содержать baseCost, minCost, artTier, effectiveCost, before и after.",
                "afterlife_conflict_action_cost_audit_invalid",
                "complete integer cost audit",
                playerAudit.ToJsonString());
            return;
        }

        ValidateSpecialArtCostBindingUniqueness(
            exchange,
            resolvedPlayerOperation,
            true,
            AfterlifeSpiritualConflictState.GetNodeString(playerAudit["specialArtId"]),
            $"{context}.actionCostAudit.player",
            issues);

        var playerSpecialArtAudit = ResolvePlayerSpecialArtAudit(exchange, resolvedPlayerOperation);
        var authorityArtTier = ResolveAuthoritativeActionCostArtTier(
            resolvedPlayerOperation,
            playerSpecialArtAudit,
            actionCostAuthority,
            context,
            issues);
        if (artTier != authorityArtTier)
        {
            AddActionCostIssue(
                issues,
                $"{context}.actionCostAudit.player.artTier",
                "actionCostAudit.player.artTier должен совпадать с pre-turn authority профилем, а не задаваться GM произвольно.",
                "afterlife_conflict_action_cost_art_tier_authority_mismatch",
                authorityArtTier.ToString(),
                artTier.ToString());
        }

        var standardEffectiveCost = Math.Max(costDefinition.MinCost, costDefinition.BaseCost - Math.Max(0, authorityArtTier));
        var expectedEffectiveCost = standardEffectiveCost;
        if (playerSpecialArtAudit != null &&
            TryGetJsonNodeInt(playerSpecialArtAudit["costMultiplierPercent"], out var specialMultiplier) &&
            specialMultiplier > 100)
        {
            expectedEffectiveCost = ComputeSpecialArtEffectiveCost(costDefinition.MinCost, standardEffectiveCost, specialMultiplier);
            var auditSpecialArtId = AfterlifeSpiritualConflictState.GetNodeString(playerAudit["specialArtId"]);
            var specialArtId = AfterlifeSpiritualConflictState.GetNodeString(playerSpecialArtAudit["artId"]);
            var hasAuditMultiplier = TryGetJsonNodeInt(playerAudit["specialCostMultiplierPercent"], out var auditMultiplier);
            var hasStandardEffectiveCost = TryGetJsonNodeInt(playerAudit["standardEffectiveCost"], out var auditStandardEffectiveCost);
            if (string.IsNullOrWhiteSpace(specialArtId) ||
                !ConflictTokenEquals(auditSpecialArtId, specialArtId) ||
                !hasAuditMultiplier ||
                auditMultiplier != specialMultiplier ||
                !hasStandardEffectiveCost ||
                auditStandardEffectiveCost != standardEffectiveCost)
            {
                AddActionCostIssue(
                    issues,
                    $"{context}.actionCostAudit.player",
                    "actionCostAudit.player для особого духовного искусства должен ссылаться на specialArtId, specialCostMultiplierPercent и standardEffectiveCost.",
                    "afterlife_conflict_special_art_cost_audit_incomplete",
                    $"specialArtId={specialArtId}, specialCostMultiplierPercent={specialMultiplier}, standardEffectiveCost={standardEffectiveCost}",
                    playerAudit.ToJsonString());
            }
        }

        if (baseCost != costDefinition.BaseCost ||
            minCost != costDefinition.MinCost ||
            effectiveCost != expectedEffectiveCost)
        {
            var mismatchCode = playerSpecialArtAudit == null
                ? "afterlife_conflict_action_cost_mismatch"
                : "afterlife_conflict_special_art_cost_mismatch";
            AddActionCostIssue(
                issues,
                $"{context}.actionCostAudit.player.effectiveCost",
                playerSpecialArtAudit == null
                    ? "Стоимость ОД должна соответствовать формуле effectiveCost = max(minCost, baseCost - artTier)."
                    : "Стоимость ОД особого духовного искусства должна применять повышающий specialCostMultiplierPercent к стандартной стоимости.",
                mismatchCode,
                $"baseCost={costDefinition.BaseCost}, minCost={costDefinition.MinCost}, effectiveCost={expectedEffectiveCost}",
                $"baseCost={baseCost}, minCost={minCost}, effectiveCost={effectiveCost}");
        }

        if (ConflictTokenEquals(resolvedPlayerOperation, "recover_spiritual_power"))
        {
            ValidateRecoveryActionCost(
                exchange,
                activeActionEconomy,
                playerAudit,
                outcome,
                context,
                issues,
                before,
                after,
                "player",
                ResolveMatchupOppositionOperation(exchange));
        }
        else
        {
            if (before < effectiveCost)
            {
                AddActionCostIssue(
                    issues,
                    $"{context}.actionCostAudit.player.before",
                    "Духовное действие не может потратить больше ОД, чем было доступно до обмена.",
                    "afterlife_conflict_action_points_insufficient",
                    $"before >= effectiveCost ({effectiveCost})",
                    before.ToString());
            }

            if (after != before - effectiveCost)
            {
                AddActionCostIssue(
                    issues,
                    $"{context}.actionCostAudit.player.after",
                    "actionCostAudit.player.after должен точно равняться before - effectiveCost.",
                    "afterlife_conflict_action_cost_delta_mismatch",
                    (before - effectiveCost).ToString(),
                    after.ToString());
            }
        }
    }

    private static void ValidateOppositionActionCostAudit(
        JsonObject exchange,
        JsonObject activeConflict,
        JsonObject? activeActionEconomy,
        string context,
        List<ValidationIssue> issues,
        AfterlifeActionCostAuthorityContext actionCostAuthority)
    {
        var oppositionOperation = ResolveOppositionOperationForActionCost(exchange);
        if (!OperationHasActionCost(oppositionOperation))
        {
            if (HasActionCostAuditSide(exchange, "opposition"))
            {
                AddActionCostIssue(
                    issues,
                    $"{context}.actionCostAudit.opposition",
                    "Если у exchange нет активного платного действия противника, actionCostAudit.opposition запрещён и не может менять ОД.",
                    "afterlife_conflict_opposition_action_cost_audit_unexpected",
                    "missing actionCostAudit.opposition unless incomingAction/finalOperationType resolves to a costed operation",
                    exchange["actionCostAudit"]?.ToJsonString() ?? "missing");
            }

            return;
        }

        var resolvedOppositionOperation = oppositionOperation!;
        if (!AfterlifeActionCosts.TryGetValue(resolvedOppositionOperation, out var costDefinition))
            return;

        if (exchange["actionCostAudit"] is not JsonObject actionCostAudit ||
            actionCostAudit["opposition"] is not JsonObject oppositionAudit)
        {
            issues.Add(new ValidationIssue(
                $"{context}.actionCostAudit.opposition",
                IssueSeverity.Error,
                "Новый обмен духовного боя с активным действием противника должен иметь actionCostAudit.opposition.",
                code: "afterlife_conflict_opposition_action_cost_audit_missing",
                section: "AfterlifeSpiritualConflict",
                expected: "actionCostAudit.opposition object",
                actual: exchange["actionCostAudit"]?.GetType().Name ?? "missing"));
            return;
        }

        var auditOperation = AfterlifeSpiritualConflictState.GetNodeString(oppositionAudit["operationType"]);
        if (!ConflictTokenEquals(auditOperation, resolvedOppositionOperation))
        {
            AddActionCostIssue(
                issues,
                $"{context}.actionCostAudit.opposition.operationType",
                "actionCostAudit.opposition.operationType должен совпадать с incomingAction/matchupAudit действием противника.",
                "afterlife_conflict_opposition_action_cost_mismatch",
                resolvedOppositionOperation,
                string.IsNullOrWhiteSpace(auditOperation) ? "missing" : auditOperation);
        }

        var hasBaseCost = TryGetJsonNodeInt(oppositionAudit["baseCost"], out var baseCost);
        var hasMinCost = TryGetJsonNodeInt(oppositionAudit["minCost"], out var minCost);
        var hasArtTier = TryGetJsonNodeInt(oppositionAudit["artTier"], out var artTier);
        var hasEffectiveCost = TryGetJsonNodeInt(oppositionAudit["effectiveCost"], out var effectiveCost);
        var hasBefore = TryGetJsonNodeInt(oppositionAudit["before"], out var before);
        var hasAfter = TryGetJsonNodeInt(oppositionAudit["after"], out var after);
        if (!hasBaseCost || !hasMinCost || !hasArtTier || !hasEffectiveCost || !hasBefore || !hasAfter)
        {
            AddActionCostIssue(
                issues,
                $"{context}.actionCostAudit.opposition",
                "actionCostAudit.opposition должен содержать baseCost, minCost, artTier, effectiveCost, before и after.",
                "afterlife_conflict_opposition_action_cost_audit_invalid",
                "complete integer cost audit",
                oppositionAudit.ToJsonString());
            return;
        }

        ValidateSpecialArtCostBindingUniqueness(
            exchange,
            resolvedOppositionOperation,
            false,
            AfterlifeSpiritualConflictState.GetNodeString(oppositionAudit["specialArtId"]),
            $"{context}.actionCostAudit.opposition",
            issues);

        var oppositionSpecialArtAudit = ResolveOppositionSpecialArtAudit(exchange, resolvedOppositionOperation);
        var oppositionActorKey = ResolveOppositionActorAuthorityKey(exchange, activeConflict);
        ValidateOppositionSpecialArtOwnerMatchesActor(oppositionSpecialArtAudit, oppositionActorKey, context, issues);

        var authorityArtTier = ResolveOppositionActionCostArtTier(
            exchange,
            activeConflict,
            resolvedOppositionOperation,
            actionCostAuthority,
            oppositionSpecialArtAudit,
            oppositionActorKey);
        if (artTier != authorityArtTier)
        {
            AddActionCostIssue(
                issues,
                $"{context}.actionCostAudit.opposition.artTier",
                "actionCostAudit.opposition.artTier должен совпадать с pre-turn authority профилем противника.",
                "afterlife_conflict_opposition_action_cost_art_tier_authority_mismatch",
                authorityArtTier.ToString(),
                artTier.ToString());
        }

        var standardEffectiveCost = Math.Max(costDefinition.MinCost, costDefinition.BaseCost - Math.Max(0, authorityArtTier));
        var expectedEffectiveCost = standardEffectiveCost;
        if (oppositionSpecialArtAudit != null &&
            TryGetJsonNodeInt(oppositionSpecialArtAudit["costMultiplierPercent"], out var specialMultiplier) &&
            specialMultiplier > 100)
        {
            expectedEffectiveCost = ComputeSpecialArtEffectiveCost(costDefinition.MinCost, standardEffectiveCost, specialMultiplier);
            var auditSpecialArtId = AfterlifeSpiritualConflictState.GetNodeString(oppositionAudit["specialArtId"]);
            var specialArtId = AfterlifeSpiritualConflictState.GetNodeString(oppositionSpecialArtAudit["artId"]);
            var hasAuditMultiplier = TryGetJsonNodeInt(oppositionAudit["specialCostMultiplierPercent"], out var auditMultiplier);
            var hasStandardEffectiveCost = TryGetJsonNodeInt(oppositionAudit["standardEffectiveCost"], out var auditStandardEffectiveCost);
            if (string.IsNullOrWhiteSpace(specialArtId) ||
                !ConflictTokenEquals(auditSpecialArtId, specialArtId) ||
                !hasAuditMultiplier ||
                auditMultiplier != specialMultiplier ||
                !hasStandardEffectiveCost ||
                auditStandardEffectiveCost != standardEffectiveCost)
            {
                AddActionCostIssue(
                    issues,
                    $"{context}.actionCostAudit.opposition",
                    "actionCostAudit.opposition для особого духовного искусства противника должен ссылаться на specialArtId, specialCostMultiplierPercent и standardEffectiveCost.",
                    "afterlife_conflict_opposition_special_art_cost_audit_incomplete",
                    $"specialArtId={specialArtId}, specialCostMultiplierPercent={specialMultiplier}, standardEffectiveCost={standardEffectiveCost}",
                    oppositionAudit.ToJsonString());
            }
        }

        if (baseCost != costDefinition.BaseCost ||
            minCost != costDefinition.MinCost ||
            effectiveCost != expectedEffectiveCost)
        {
            var mismatchCode = oppositionSpecialArtAudit == null
                ? "afterlife_conflict_opposition_action_cost_mismatch"
                : "afterlife_conflict_opposition_special_art_cost_mismatch";
            AddActionCostIssue(
                issues,
                $"{context}.actionCostAudit.opposition.effectiveCost",
                oppositionSpecialArtAudit == null
                    ? "Стоимость ОД противника должна соответствовать формуле effectiveCost = max(minCost, baseCost - artTier)."
                    : "Стоимость ОД особого духовного искусства противника должна применять повышающий specialCostMultiplierPercent к стандартной стоимости.",
                mismatchCode,
                $"baseCost={costDefinition.BaseCost}, minCost={costDefinition.MinCost}, effectiveCost={expectedEffectiveCost}",
                $"baseCost={baseCost}, minCost={minCost}, effectiveCost={effectiveCost}");
        }

        if (ConflictTokenEquals(resolvedOppositionOperation, "recover_spiritual_power"))
        {
            ValidateRecoveryActionCost(
                exchange,
                activeActionEconomy,
                oppositionAudit,
                "success",
                context,
                issues,
                before,
                after,
                "opposition",
                AfterlifeSpiritualConflictState.GetNodeString(exchange["operationType"]));
            return;
        }

        if (before < effectiveCost)
        {
            AddActionCostIssue(
                issues,
                $"{context}.actionCostAudit.opposition.before",
                "Духовное действие противника не может потратить больше ОД, чем было доступно до обмена.",
                "afterlife_conflict_opposition_action_points_insufficient",
                $"before >= effectiveCost ({effectiveCost})",
                before.ToString());
        }

        if (after != before - effectiveCost)
        {
            AddActionCostIssue(
                issues,
                $"{context}.actionCostAudit.opposition.after",
                "actionCostAudit.opposition.after должен точно равняться before - effectiveCost.",
                "afterlife_conflict_opposition_action_cost_delta_mismatch",
                (before - effectiveCost).ToString(),
                after.ToString());
        }
    }

    private static int ResolveAuthoritativeActionCostArtTier(
        string operationType,
        JsonObject? specialArtAudit,
        AfterlifeActionCostAuthorityContext actionCostAuthority,
        string context,
        List<ValidationIssue> issues)
    {
        if (specialArtAudit == null)
        {
            return actionCostAuthority.StandardArtTiers.TryGetValue(operationType, out var tier)
                ? Math.Clamp(tier, 0, 5)
                : 0;
        }

        if (!SpecialArtAuditOwnerIsPlayer(specialArtAudit))
        {
            return actionCostAuthority.StandardArtTiers.TryGetValue(operationType, out var tier)
                ? Math.Clamp(tier, 0, 5)
                : 0;
        }

        var specialArtId = AfterlifeSpiritualConflictState.GetNodeString(specialArtAudit["artId"]);
        if (string.IsNullOrWhiteSpace(specialArtId) ||
            !actionCostAuthority.PlayerSpecialArts.TryGetValue(specialArtId, out var learnedArt))
        {
            AddSpecialArtIssue(
                issues,
                $"{context}.specialArtAudit.artId",
                "Особое духовное искусство можно использовать только если оно уже есть в pre-turn профиле души игрока.",
                "afterlife_conflict_special_art_not_learned",
                "player_soul.specialArts[] contains artId",
                string.IsNullOrWhiteSpace(specialArtId) ? "missing" : specialArtId);
            return 0;
        }

        var learnedBaseOperation = AfterlifeSpiritualConflictState.GetNodeString(learnedArt["baseOperation"]);
        var auditBaseOperation = AfterlifeSpiritualConflictState.GetNodeString(specialArtAudit["baseOperation"]);
        var learnedTier = Math.Clamp(AfterlifeSpiritualConflictState.GetNodeInt(learnedArt["tier"]), 0, 5);
        var learnedMultiplier = AfterlifeSpiritualConflictState.GetNodeInt(learnedArt["costMultiplierPercent"]);
        var auditMultiplier = AfterlifeSpiritualConflictState.GetNodeInt(specialArtAudit["costMultiplierPercent"]);
        if (!ConflictTokenEquals(learnedBaseOperation, operationType) ||
            !ConflictTokenEquals(auditBaseOperation, operationType) ||
            learnedMultiplier != auditMultiplier)
        {
            AddSpecialArtIssue(
                issues,
                $"{context}.specialArtAudit",
                "specialArtAudit должен совпадать с pre-turn player_soul.specialArts[]: baseOperation и costMultiplierPercent являются authority-полями.",
                "afterlife_conflict_special_art_authority_mismatch",
                $"artId={specialArtId}, baseOperation={learnedBaseOperation}, costMultiplierPercent={learnedMultiplier}",
                specialArtAudit.ToJsonString());
        }

        return learnedTier;
    }

    private static string? ResolveOppositionOperationForActionCost(JsonObject exchange)
    {
        var finalOperation = ResolveIncomingActionFinalOperation(exchange);
        if (exchange["matchupAudit"] is JsonObject matchupAudit)
        {
            var matchupOperation = AfterlifeSpiritualConflictState.GetNodeString(matchupAudit["oppositionOperation"]);
            if (OperationHasActionCost(matchupOperation))
            {
                if (!string.IsNullOrWhiteSpace(finalOperation))
                {
                    if (ConflictTokenEquals(matchupOperation, finalOperation))
                        return matchupOperation;
                }
                else
                {
                    var incomingActionOperations = ResolveIncomingActionOperations(exchange);
                    if (incomingActionOperations.Count == 0 ||
                        incomingActionOperations.Any(incomingOperation => ConflictTokenEquals(matchupOperation, incomingOperation)))
                    {
                        return matchupOperation;
                    }
                }
            }
        }

        if (exchange["incomingAction"] is JsonObject incomingAction)
        {
            if (OperationHasActionCost(finalOperation))
                return finalOperation;

            if (!string.IsNullOrWhiteSpace(finalOperation))
                return null;

            var operation = AfterlifeSpiritualConflictState.GetNodeString(incomingAction["operationType"]);
            if (OperationHasActionCost(operation))
                return operation;
        }

        return null;
    }

    private static string? ResolveIncomingActionFinalOperation(JsonObject exchange) =>
        exchange["incomingAction"] is JsonObject incomingAction
            ? AfterlifeSpiritualConflictState.GetNodeString(incomingAction["finalOperationType"])
            : null;

    private static bool OperationHasActionCost(string? operationType) =>
        !string.IsNullOrWhiteSpace(operationType) &&
        !ConflictTokenEquals(operationType, "none", "passive") &&
        !IsTerminalNoCostOperation(operationType) &&
        AfterlifeActionCosts.ContainsKey(operationType);

    private static int ResolveOppositionActionCostArtTier(
        JsonObject exchange,
        JsonObject activeConflict,
        string operationType,
        AfterlifeActionCostAuthorityContext actionCostAuthority,
        JsonObject? oppositionSpecialArtAudit,
        string? actorKey)
    {
        if (oppositionSpecialArtAudit != null &&
            !string.IsNullOrWhiteSpace(actorKey) &&
            string.Equals(ResolveSpecialArtAuditOwnerKey(oppositionSpecialArtAudit), actorKey, StringComparison.OrdinalIgnoreCase) &&
            ResolveSpecialArtAuthority(oppositionSpecialArtAudit, actionCostAuthority) is JsonObject authoritySpecialArt)
        {
            return Math.Clamp(
                AfterlifeSpiritualConflictState.GetNodeInt(authoritySpecialArt["tier"]),
                0,
                AfterlifeEntityProfileState.MaxProfileTier);
        }

        actorKey ??= ResolveOppositionActorAuthorityKey(exchange, activeConflict);
        if (actorKey == null)
            return 0;

        if (actionCostAuthority.EntityStandardArtTiers.TryGetValue(actorKey, out var profileTiers) &&
            profileTiers.TryGetValue(operationType, out var profileTier))
        {
            return Math.Clamp(profileTier, 0, AfterlifeEntityProfileState.MaxProfileTier);
        }

        if (actionCostAuthority.PreTurnConflictActorArtTierSnapshots.TryGetValue(actorKey, out var snapshotTiers) &&
            snapshotTiers.TryGetValue(operationType, out var snapshotTier))
        {
            return Math.Clamp(snapshotTier, 0, AfterlifeEntityProfileState.MaxProfileTier);
        }

        return 0;
    }

    private static JsonObject? ResolveOppositionSpecialArtAudit(JsonObject exchange, string oppositionOperation)
    {
        return EnumerateSpecialArtAuditObjects(exchange)
            .FirstOrDefault(audit =>
                !SpecialArtAuditOwnerIsPlayer(audit) &&
                ConflictTokenEqualsSingle(AfterlifeSpiritualConflictState.GetNodeString(audit["baseOperation"]), oppositionOperation));
    }

    private static void ValidateOppositionSpecialArtOwnerMatchesActor(
        JsonObject? oppositionSpecialArtAudit,
        string? oppositionActorKey,
        string context,
        List<ValidationIssue> issues)
    {
        if (oppositionSpecialArtAudit == null ||
            string.IsNullOrWhiteSpace(oppositionActorKey))
        {
            return;
        }

        var auditOwnerKey = ResolveSpecialArtAuditOwnerKey(oppositionSpecialArtAudit);
        if (auditOwnerKey == null ||
            string.Equals(auditOwnerKey, oppositionActorKey, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        AddActionCostIssue(
            issues,
            $"{context}.specialArtAudit.ownerActorId",
            "Особое духовное искусство противника должно принадлежать текущему opposition actor из incomingAction или oppositionSide.leadContestant.",
            "afterlife_conflict_opposition_special_art_owner_mismatch",
            $"ownerActorKey={oppositionActorKey}",
            $"ownerActorKey={auditOwnerKey}");
    }

    private static JsonObject? ResolvePlayerSpecialArtAudit(JsonObject exchange, string operationType)
    {
        return EnumerateSpecialArtAuditObjects(exchange)
            .FirstOrDefault(audit =>
                SpecialArtAuditOwnerIsPlayer(audit) &&
                ConflictTokenEqualsSingle(AfterlifeSpiritualConflictState.GetNodeString(audit["baseOperation"]), operationType));
    }

    private static int ComputeSpecialArtEffectiveCost(int minCost, int standardEffectiveCost, int specialMultiplier)
    {
        var multiplied = ((long)Math.Max(0, standardEffectiveCost) * Math.Max(0, specialMultiplier) + 99) / 100;
        var capped = Math.Min(int.MaxValue, Math.Max(minCost, multiplied));
        return (int)capped;
    }

    private static void ValidateSpecialArtCostBindingUniqueness(
        JsonObject exchange,
        string operationType,
        bool playerSide,
        string? requestedSpecialArtId,
        string context,
        List<ValidationIssue> issues)
    {
        var matchingAudits = EnumerateSpecialArtAuditObjects(exchange)
            .Where(audit =>
                SpecialArtAuditOwnerIsPlayer(audit) == playerSide &&
                ConflictTokenEqualsSingle(AfterlifeSpiritualConflictState.GetNodeString(audit["baseOperation"]), operationType))
            .ToArray();
        if (!string.IsNullOrWhiteSpace(requestedSpecialArtId) &&
            matchingAudits.Length == 0)
        {
            var missingSideLabel = playerSide ? "player" : "opposition";
            AddActionCostIssue(
                issues,
                context,
                $"actionCostAudit.{missingSideLabel}.specialArtId требует ровно один specialArtAudit/specialArtAudits[] для той же стороны и операции, иначе особый эффект и effectNote не проверяются.",
                "afterlife_conflict_special_art_cost_audit_incomplete",
                $"one {missingSideLabel} special art audit for operationType={operationType}, specialArtId={requestedSpecialArtId}",
                "missing matching specialArtAudit");
            return;
        }

        if (matchingAudits.Length <= 1)
            return;

        var sideLabel = playerSide ? "player" : "opposition";
        var artIds = string.Join(
            ", ",
            matchingAudits
                .Select(audit => AfterlifeSpiritualConflictState.GetNodeString(audit["artId"]))
                .Where(artId => !string.IsNullOrWhiteSpace(artId)));
        AddActionCostIssue(
            issues,
            context,
            "specialArtAudits[] должен иметь ровно одно особое духовное искусство, которое умножает стоимость ОД выбранной стороны и операции.",
            "afterlife_conflict_special_art_cost_binding_ambiguous",
            $"exactly one {sideLabel} special art audit for operationType={operationType}",
            $"specialArtId={requestedSpecialArtId ?? "missing"}, matchingArtIds=[{artIds}]");
    }

    private static string? ResolveOppositionActorAuthorityKey(JsonObject exchange, JsonObject activeConflict)
    {
        if (exchange["incomingAction"] is JsonObject incomingAction)
        {
            var incomingKey = BuildActorAuthorityKey(
                AfterlifeSpiritualConflictState.GetNodeString(incomingAction["actorType"]) ??
                AfterlifeSpiritualConflictState.GetNodeString(incomingAction["ownerActorType"]),
                AfterlifeSpiritualConflictState.GetNodeString(incomingAction["actorId"]) ??
                AfterlifeSpiritualConflictState.GetNodeString(incomingAction["actorRef"]) ??
                AfterlifeSpiritualConflictState.GetNodeString(incomingAction["ownerActorId"]) ??
                AfterlifeSpiritualConflictState.GetNodeString(incomingAction["guardianId"]) ??
                AfterlifeSpiritualConflictState.GetNodeString(incomingAction["id"]));
            if (incomingKey != null)
                return incomingKey;
        }

        var lead = activeConflict["oppositionSide"] is JsonObject oppositionSide
            ? oppositionSide["leadContestant"] as JsonObject
            : null;
        return BuildActorAuthorityKey(
            AfterlifeSpiritualConflictState.GetNodeString(lead?["actorType"]),
            AfterlifeSpiritualConflictState.GetNodeString(lead?["actorId"]) ??
            AfterlifeSpiritualConflictState.GetNodeString(lead?["actorRef"]) ??
            AfterlifeSpiritualConflictState.GetNodeString(lead?["id"]));
    }

    private static void ValidateSpecialArtAudit(
        JsonObject exchange,
        string? operationType,
        AfterlifeActionCostAuthorityContext actionCostAuthority,
        string context,
        List<ValidationIssue> issues)
    {
        if (!exchange.ContainsKey("specialArtAudit") &&
            !exchange.ContainsKey("specialArtAudits"))
        {
            return;
        }

        if (exchange.ContainsKey("specialArtAudit") &&
            exchange.ContainsKey("specialArtAudits"))
        {
            AddSpecialArtIssue(
                issues,
                $"{context}.specialArtAudits",
                "Используйте либо specialArtAudit для одного особого искусства, либо specialArtAudits[] для нескольких сторон; смешивать оба поля в одном обмене нельзя.",
                "afterlife_conflict_special_art_audit_ambiguous",
                "specialArtAudit OR specialArtAudits[]",
                "both present");
        }

        foreach (var audit in ReadSpecialArtAuditsForValidation(exchange, context, issues))
        {
            ValidateSingleSpecialArtAudit(exchange, operationType, actionCostAuthority, context, issues, audit);
        }
    }

    private static void ValidateSingleSpecialArtAudit(
        JsonObject exchange,
        string? operationType,
        AfterlifeActionCostAuthorityContext actionCostAuthority,
        string context,
        List<ValidationIssue> issues,
        JsonObject audit)
    {
        var artId = AfterlifeSpiritualConflictState.GetNodeString(audit["artId"]);
        if (string.IsNullOrWhiteSpace(artId))
        {
            AddSpecialArtIssue(
                issues,
                $"{context}.specialArtAudit.artId",
                "specialArtAudit.artId обязателен.",
                "afterlife_conflict_special_art_missing_id",
                "non-empty artId",
                audit["artId"]?.ToJsonString() ?? "missing");
        }

        var ownerActorType = AfterlifeSpiritualConflictState.GetNodeString(audit["ownerActorType"]);
        if (string.IsNullOrWhiteSpace(ownerActorType))
        {
            AddSpecialArtIssue(
                issues,
                $"{context}.specialArtAudit.ownerActorType",
                "specialArtAudit.ownerActorType обязателен.",
                "afterlife_conflict_special_art_missing_owner",
                "non-empty ownerActorType",
                audit["ownerActorType"]?.ToJsonString() ?? "missing");
        }

        if (string.IsNullOrWhiteSpace(AfterlifeSpiritualConflictState.GetNodeString(audit["ownerActorId"])))
        {
            AddSpecialArtIssue(
                issues,
                $"{context}.specialArtAudit.ownerActorId",
                "specialArtAudit.ownerActorId обязателен.",
                "afterlife_conflict_special_art_missing_owner",
                "non-empty ownerActorId",
                audit["ownerActorId"]?.ToJsonString() ?? "missing");
        }

        var baseOperation = AfterlifeSpiritualConflictState.GetNodeString(audit["baseOperation"]);
        if (string.IsNullOrWhiteSpace(baseOperation) ||
            !AfterlifeEntityProfileState.SpecialArtBaseOperations.Contains(baseOperation))
        {
            AddSpecialArtIssue(
                issues,
                $"{context}.specialArtAudit.baseOperation",
                "specialArtAudit.baseOperation должен ссылаться на стандартное духовное действие.",
                "afterlife_conflict_special_art_invalid_base_operation",
                string.Join("/", AfterlifeEntityProfileState.SpecialArtBaseOperations.OrderBy(item => item, StringComparer.OrdinalIgnoreCase)),
                string.IsNullOrWhiteSpace(baseOperation) ? "missing" : baseOperation);
        }
        else if (!SpecialArtBaseOperationMatchesExchangeSide(exchange, operationType, audit, baseOperation))
        {
            var expectedOperation = SpecialArtAuditOwnerIsPlayer(audit)
                ? operationType ?? "exchange.operationType"
                : "incomingAction.operationType/finalOperationType or matchupAudit.oppositionOperation";
            AddSpecialArtIssue(
                issues,
                $"{context}.specialArtAudit.baseOperation",
                "specialArtAudit.baseOperation должен совпадать с действием стороны, чьё особое духовное искусство применено.",
                "afterlife_conflict_special_art_base_operation_mismatch",
                expectedOperation,
                baseOperation);
        }

        ValidateSpecialArtAuthority(audit, actionCostAuthority, context, issues);

        if (!TryGetJsonNodeInt(audit["costMultiplierPercent"], out var multiplier) || multiplier <= 100)
        {
            AddSpecialArtIssue(
                issues,
                $"{context}.specialArtAudit.costMultiplierPercent",
                "Особое духовное искусство должно иметь повышенный costMultiplierPercent > 100.",
                "afterlife_conflict_special_art_invalid_cost_multiplier",
                "integer > 100",
                audit["costMultiplierPercent"]?.ToJsonString() ?? "missing");
        }

        if (string.IsNullOrWhiteSpace(AfterlifeSpiritualConflictState.GetNodeString(audit["effectNote"])))
        {
            AddSpecialArtIssue(
                issues,
                $"{context}.specialArtAudit.effectNote",
                "Если в бою используется особое духовное искусство, ГМ обязан записать effectNote о влиянии особого эффекта.",
                "afterlife_conflict_special_art_missing_effect_note",
                "non-empty effectNote",
                audit["effectNote"]?.ToJsonString() ?? "missing");
        }
    }

    private static IEnumerable<JsonObject> ReadSpecialArtAuditsForValidation(
        JsonObject exchange,
        string context,
        List<ValidationIssue> issues)
    {
        if (exchange.ContainsKey("specialArtAudit"))
        {
            if (exchange["specialArtAudit"] is JsonObject audit)
            {
                yield return audit;
            }
            else
            {
                AddSpecialArtIssue(
                    issues,
                    $"{context}.specialArtAudit",
                    "specialArtAudit должен быть object, если обмен использует одно особое духовное искусство.",
                    "afterlife_conflict_special_art_audit_not_object",
                    "object",
                    exchange["specialArtAudit"]?.ToJsonString() ?? "null");
            }
        }

        if (!exchange.ContainsKey("specialArtAudits"))
            yield break;

        if (exchange["specialArtAudits"] is not JsonArray audits)
        {
            AddSpecialArtIssue(
                issues,
                $"{context}.specialArtAudits",
                "specialArtAudits должен быть array, если в одном обмене используются особые духовные искусства нескольких сторон.",
                "afterlife_conflict_special_art_audits_not_array",
                "array of objects",
                exchange["specialArtAudits"]?.ToJsonString() ?? "null");
            yield break;
        }

        if (audits.Count == 0)
        {
            AddSpecialArtIssue(
                issues,
                $"{context}.specialArtAudits",
                "specialArtAudits не должен быть пустым.",
                "afterlife_conflict_special_art_audits_empty",
                "one or more special art audit objects",
                "[]");
            yield break;
        }

        var index = 0;
        foreach (var node in audits)
        {
            if (node is JsonObject audit)
            {
                yield return audit;
            }
            else
            {
                AddSpecialArtIssue(
                    issues,
                    $"{context}.specialArtAudits[{index}]",
                    "Каждый specialArtAudits[] entry должен быть object.",
                    "afterlife_conflict_special_art_audit_not_object",
                    "object",
                    node?.ToJsonString() ?? "null");
            }

            index++;
        }
    }

    private static IEnumerable<JsonObject> EnumerateSpecialArtAuditObjects(JsonObject exchange)
    {
        if (exchange["specialArtAudit"] is JsonObject singular)
            yield return singular;

        if (exchange["specialArtAudits"] is not JsonArray audits)
            yield break;

        foreach (var audit in audits.OfType<JsonObject>())
            yield return audit;
    }

    private static void ValidateSpecialArtAuthority(
        JsonObject audit,
        AfterlifeActionCostAuthorityContext actionCostAuthority,
        string context,
        List<ValidationIssue> issues)
    {
        var artId = AfterlifeSpiritualConflictState.GetNodeString(audit["artId"]);
        var ownerActorType = AfterlifeSpiritualConflictState.GetNodeString(audit["ownerActorType"]);
        var ownerActorId = AfterlifeSpiritualConflictState.GetNodeString(audit["ownerActorId"]);
        if (string.IsNullOrWhiteSpace(artId) ||
            string.IsNullOrWhiteSpace(ownerActorType) ||
            string.IsNullOrWhiteSpace(ownerActorId))
        {
            return;
        }

        var authorityKey = BuildSpecialArtAuthorityKey(ownerActorType, ownerActorId, artId);
        if (authorityKey == null ||
            !actionCostAuthority.SpecialArtsByOwner.TryGetValue(authorityKey, out var authorityArt))
        {
            AddSpecialArtIssue(
                issues,
                $"{context}.specialArtAudit.artId",
                "Особое духовное искусство можно использовать только если оно уже есть в pre-turn профиле владельца.",
                "afterlife_conflict_special_art_not_in_owner_profile",
                "owner profile specialArts[] contains artId",
                $"{ownerActorType}:{ownerActorId}:{artId}");
            return;
        }

        var authorityBaseOperation = AfterlifeSpiritualConflictState.GetNodeString(authorityArt["baseOperation"]);
        var authorityMultiplier = AfterlifeSpiritualConflictState.GetNodeInt(authorityArt["costMultiplierPercent"]);
        var auditBaseOperation = AfterlifeSpiritualConflictState.GetNodeString(audit["baseOperation"]);
        var auditMultiplier = AfterlifeSpiritualConflictState.GetNodeInt(audit["costMultiplierPercent"]);
        if (!ConflictTokenEqualsSingle(authorityBaseOperation, auditBaseOperation) ||
            authorityMultiplier != auditMultiplier)
        {
            AddSpecialArtIssue(
                issues,
                $"{context}.specialArtAudit",
                "specialArtAudit должен совпадать с pre-turn профилем владельца: baseOperation и costMultiplierPercent являются authority-полями.",
                "afterlife_conflict_special_art_authority_mismatch",
                $"artId={artId}, baseOperation={authorityBaseOperation}, costMultiplierPercent={authorityMultiplier}",
                audit.ToJsonString());
        }
    }

    private static bool SpecialArtBaseOperationMatchesExchangeSide(
        JsonObject exchange,
        string? operationType,
        JsonObject audit,
        string baseOperation)
    {
        if (SpecialArtAuditOwnerIsPlayer(audit))
            return !string.IsNullOrWhiteSpace(operationType) && ConflictTokenEquals(baseOperation, operationType);

        var resolvedOppositionOperation = ResolveOppositionOperationForActionCost(exchange);
        return ConflictTokenEqualsSingle(baseOperation, resolvedOppositionOperation);
    }

    private static bool ConflictTokenEqualsSingle(string? value, string? acceptedToken) =>
        !string.IsNullOrWhiteSpace(acceptedToken) &&
        ConflictTokenEquals(value, acceptedToken!);

    private static bool SpecialArtAuditOwnerIsPlayer(JsonObject specialArtAudit)
    {
        var ownerActorType = AfterlifeSpiritualConflictState.GetNodeString(specialArtAudit["ownerActorType"]);
        var ownerActorId = AfterlifeSpiritualConflictState.GetNodeString(specialArtAudit["ownerActorId"]);
        return IsPlayerSoulActor(ownerActorType, ownerActorId);
    }

    private static JsonObject? ResolveSpecialArtAuthority(
        JsonObject specialArtAudit,
        AfterlifeActionCostAuthorityContext actionCostAuthority)
    {
        var artId = AfterlifeSpiritualConflictState.GetNodeString(specialArtAudit["artId"]);
        var ownerActorType = AfterlifeSpiritualConflictState.GetNodeString(specialArtAudit["ownerActorType"]);
        var ownerActorId = AfterlifeSpiritualConflictState.GetNodeString(specialArtAudit["ownerActorId"]);
        var authorityKey = BuildSpecialArtAuthorityKey(ownerActorType, ownerActorId, artId);
        return authorityKey != null &&
               actionCostAuthority.SpecialArtsByOwner.TryGetValue(authorityKey, out var authorityArt)
            ? authorityArt
            : null;
    }

    private static string? ResolveSpecialArtAuditOwnerKey(JsonObject specialArtAudit)
    {
        var ownerActorType = AfterlifeSpiritualConflictState.GetNodeString(specialArtAudit["ownerActorType"]);
        var ownerActorId = AfterlifeSpiritualConflictState.GetNodeString(specialArtAudit["ownerActorId"]);
        return BuildSpecialArtOwnerKey(ownerActorType, ownerActorId);
    }

    private static string? BuildSpecialArtAuthorityKey(string? ownerActorType, string? ownerActorId, string? artId)
    {
        if (string.IsNullOrWhiteSpace(artId))
            return null;

        var ownerKey = BuildSpecialArtOwnerKey(ownerActorType, ownerActorId);
        return ownerKey == null
            ? null
            : $"{ownerKey}:{artId.Trim()}";
    }

    private static string? BuildSpecialArtOwnerKey(string? ownerActorType, string? ownerActorId)
    {
        if (IsPlayerSoulActor(ownerActorType, ownerActorId))
            return "player_soul:player_soul";

        return BuildActorAuthorityKey(ownerActorType, ownerActorId);
    }

    private static string? BuildActorAuthorityKey(string? actorType, string? actorId)
    {
        if (IsPlayerSoulActor(actorType, actorId))
            return "player_soul:player_soul";

        return string.IsNullOrWhiteSpace(actorType) || string.IsNullOrWhiteSpace(actorId)
            ? null
            : $"{actorType.Trim()}:{actorId.Trim()}";
    }

    private static void ValidateCurrentActionCostSequence(
        JsonObject exchange,
        string side,
        int? expectedBefore,
        string context,
        List<ValidationIssue> issues,
        out int? currentExchangeActionAfter)
    {
        currentExchangeActionAfter = null;
        if (!TryGetActionCostBeforeAfter(exchange, side, out var before, out var after))
            return;

        if (expectedBefore.HasValue && before != expectedBefore.Value)
        {
            AddActionCostIssue(
                issues,
                $"{context}.actionCostAudit.{side}.before",
                "Последовательные текущие обмены должны расходовать/восстанавливать ОД от результата предыдущего обмена.",
                "afterlife_conflict_action_cost_sequence_mismatch",
                expectedBefore.Value.ToString(),
                before.ToString());
        }

        currentExchangeActionAfter = after;
    }

    private static void ValidateActionEconomyMatchesLastCurrentExchange(
        JsonObject? actionEconomy,
        string side,
        int? expectedCurrent,
        string context,
        List<ValidationIssue> issues)
    {
        if (!expectedCurrent.HasValue)
            return;

        if (actionEconomy?[side] is not JsonObject pool ||
            !TryGetJsonNodeInt(pool["current"], out var actualCurrent))
        {
            return;
        }

        if (actualCurrent != expectedCurrent.Value)
        {
            AddActionCostIssue(
                issues,
                context,
                $"Итоговый activeConflict.actionEconomy.{side}.current должен совпадать с последним текущим actionCostAudit.{side}.after.",
                string.Equals(side, "opposition", StringComparison.OrdinalIgnoreCase)
                    ? "afterlife_conflict_action_economy_opposition_delta_mismatch"
                    : "afterlife_conflict_action_economy_delta_mismatch",
                expectedCurrent.Value.ToString(),
                actualCurrent.ToString());
        }
    }

    private static void ValidateActionEconomyUnchangedWhenUnaudited(
        JsonObject? actionEconomy,
        string side,
        int? auditedExpectedCurrent,
        int? preTurnExpectedCurrent,
        string context,
        List<ValidationIssue> issues)
    {
        if (auditedExpectedCurrent.HasValue || !preTurnExpectedCurrent.HasValue)
            return;

        if (actionEconomy?[side] is not JsonObject pool ||
            !TryGetJsonNodeInt(pool["current"], out var actualCurrent))
        {
            return;
        }

        if (actualCurrent == preTurnExpectedCurrent.Value)
            return;

        AddActionCostIssue(
            issues,
            context,
            $"activeConflict.actionEconomy.{side}.current нельзя менять без текущего actionCostAudit.{side}; сторона без audit должна сохранить pre-turn ОД.",
            string.Equals(side, "opposition", StringComparison.OrdinalIgnoreCase)
                ? "afterlife_conflict_action_economy_opposition_unaudited_delta"
                : "afterlife_conflict_action_economy_unaudited_delta",
            preTurnExpectedCurrent.Value.ToString(),
            actualCurrent.ToString());
    }

    private static void ValidateActionEconomyMatchesSpiritFocus(
        JsonObject? actionEconomy,
        AfterlifeConflictDiceContext diceContext,
        string context,
        List<ValidationIssue> issues)
    {
        if (actionEconomy?["player"] is not JsonObject playerPool ||
            !TryGetJsonNodeInt(playerPool["max"], out var actualMax))
        {
            return;
        }

        if (actualMax == diceContext.SpiritFocusMaxActionPoints)
            return;

        AddActionCostIssue(
            issues,
            $"{context}.max",
            "Максимум ОД игрока в activeConflict должен соответствовать Средоточию Души из authority soul_state.",
            "afterlife_conflict_action_economy_spirit_focus_mismatch",
            $"{diceContext.SpiritFocusMaxActionPoints} ОД from spiritFocusTier={diceContext.SpiritFocusTier}",
            actualMax.ToString());
    }

    private static bool TryGetActionCostBeforeAfter(JsonObject exchange, string side, out int before, out int after)
    {
        before = 0;
        after = 0;
        return exchange["actionCostAudit"] is JsonObject actionCostAudit &&
               actionCostAudit[side] is JsonObject sideAudit &&
               TryGetJsonNodeInt(sideAudit["before"], out before) &&
               TryGetJsonNodeInt(sideAudit["after"], out after);
    }

    private static void ValidateRecoveryActionCost(
        JsonObject exchange,
        JsonObject? activeActionEconomy,
        JsonObject playerAudit,
        string? outcome,
        string context,
        List<ValidationIssue> issues,
        int before,
        int after,
        string side,
        string? punishingOperation)
    {
        var maxActionPoints = TryGetActionPoolMax(activeActionEconomy, side, out var activeMax)
            ? activeMax
            : TryGetJsonNodeInt(playerAudit["max"], out var auditMax)
                ? auditMax
                : 0;

        if (maxActionPoints <= 0)
        {
            AddActionCostIssue(
                issues,
                $"{context}.actionCostAudit.{side}.max",
                $"Для восстановления ОД нужен max из activeConflict.actionEconomy.{side}.max или actionCostAudit.{side}.max.",
                "afterlife_conflict_action_recovery_missing_max",
                "positive max action points",
                "missing");
            return;
        }

        if (after > maxActionPoints)
        {
            AddActionCostIssue(
                issues,
                $"{context}.actionCostAudit.{side}.after",
                "Собрать Средоточие не может восстановить ОД выше максимума.",
                "afterlife_conflict_action_recovery_exceeds_max",
                $"after <= max ({maxActionPoints})",
                after.ToString());
        }

        var delta = after - before;
        var punishedRecovery = ConflictTokenEquals(
            punishingOperation,
            "pressure",
            "maneuver",
            "binding",
            "force_binding",
            "force_incarnation");

        if (punishedRecovery)
        {
            if (delta < 0 || delta > 1)
            {
                AddActionCostIssue(
                    issues,
                    $"{context}.actionCostAudit.{side}.after",
                    "Собрать Средоточие под давлением/манёвром/оковами восстанавливает только 0..1 ОД.",
                    "afterlife_conflict_action_recovery_delta_mismatch",
                    "delta 0..1 against pressure/maneuver/control",
                    delta.ToString());
            }

            return;
        }

        var expectedDelta = ConflictTokenEquals(outcome, "success")
            ? 3
            : ConflictTokenEquals(outcome, "partial_success")
                ? 2
                : 0;
        var expectedAfter = Math.Min(maxActionPoints, before + expectedDelta);
        if (after != expectedAfter)
        {
            AddActionCostIssue(
                issues,
                $"{context}.actionCostAudit.{side}.after",
                "Собрать Средоточие должно восстановить ровно ожидаемое количество ОД с учётом максимума.",
                "afterlife_conflict_action_recovery_delta_mismatch",
                expectedAfter.ToString(),
                after.ToString());
        }
    }

    private static bool TryGetActionPoolMax(JsonObject? actionEconomy, string side, out int max)
    {
        max = 0;
        return actionEconomy?[side] is JsonObject pool &&
               TryGetJsonNodeInt(pool["max"], out max);
    }

    private static string? ResolveMatchupOppositionOperation(JsonObject exchange)
    {
        if (exchange["matchupAudit"] is JsonObject matchupAudit)
            return AfterlifeSpiritualConflictState.GetNodeString(matchupAudit["oppositionOperation"]);
        return null;
    }

    private static bool IsTerminalNoCostOperation(string operationType) =>
        ConflictTokenEquals(operationType, "withdraw", "surrender", "negotiate");

    private static void AddActionCostIssue(
        List<ValidationIssue> issues,
        string path,
        string message,
        string code,
        string expected,
        string actual)
    {
        issues.Add(new ValidationIssue(
            path,
            IssueSeverity.Error,
            message,
            code: code,
            section: "AfterlifeSpiritualConflict",
            expected: expected,
            actual: actual));
    }

    private static void AddSpecialArtIssue(
        List<ValidationIssue> issues,
        string path,
        string message,
        string code,
        string expected,
        string actual)
    {
        issues.Add(new ValidationIssue(
            path,
            IssueSeverity.Error,
            message,
            code: code,
            section: "AfterlifeSpiritualConflict",
            expected: expected,
            actual: actual));
    }

    private static void ValidateCurrentExchangeControlSnapshotCompleteness(
        JsonNode? priorControlState,
        JsonObject before,
        JsonObject after,
        string context,
        List<ValidationIssue> issues,
        bool requiredForCurrentExchange)
    {
        if (!requiredForCurrentExchange ||
            (!HasActiveControlState(priorControlState) &&
             !HasActiveControlState(before) &&
             !HasActiveControlState(after)))
        {
            return;
        }

        if (!before.ContainsKey("controlState"))
        {
            AddSpiritualArtRuleIssue(
                issues,
                $"{context}.before.controlState",
                "Текущий exchange в конфликте с active controlState должен явно фиксировать before.controlState.",
                "afterlife_conflict_control_snapshot_missing",
                "before.controlState present as object/null/{ level: none }",
                "missing");
        }
        else if (!ControlAuditSnapshotMatchesPrior(priorControlState, before["controlState"]))
        {
            AddSpiritualArtRuleIssue(
                issues,
                $"{context}.before.controlState",
                "before.controlState текущего exchange должен совпадать с controlState, активным перед этим exchange.",
                "afterlife_conflict_control_snapshot_mismatch",
                DescribeControlNode(priorControlState),
                DescribeControlNode(before["controlState"]));
        }

        if (!after.ContainsKey("controlState"))
        {
            AddSpiritualArtRuleIssue(
                issues,
                $"{context}.after.controlState",
                "Текущий exchange в конфликте с active controlState должен явно фиксировать after.controlState.",
                "afterlife_conflict_control_snapshot_missing",
                "after.controlState present as object/null/{ level: none }",
                "missing");
        }
    }

    private static JsonNode? ResolveScopedPreTurnActiveControlState(
        JsonObject conflict,
        AfterlifeConflictDiceContext diceContext)
    {
        if (!diceContext.HasValidatedTurnBaseline ||
            string.IsNullOrWhiteSpace(diceContext.PreTurnActiveConflictId))
        {
            return null;
        }

        var currentConflictId = TryReadConflictId(conflict);
        return string.Equals(currentConflictId, diceContext.PreTurnActiveConflictId, StringComparison.OrdinalIgnoreCase)
            ? diceContext.PreTurnActiveControlState?.DeepClone()
            : null;
    }

    private static int? ResolveScopedPreTurnActionEconomyCurrent(
        JsonObject conflict,
        AfterlifeConflictDiceContext diceContext,
        string side)
    {
        if (!diceContext.HasValidatedTurnBaseline ||
            string.IsNullOrWhiteSpace(diceContext.PreTurnActiveConflictId))
        {
            return null;
        }

        var currentConflictId = TryReadConflictId(conflict);
        if (!string.Equals(currentConflictId, diceContext.PreTurnActiveConflictId, StringComparison.OrdinalIgnoreCase))
            return null;

        return string.Equals(side, "opposition", StringComparison.OrdinalIgnoreCase)
            ? diceContext.PreTurnOppositionActionCurrent
            : diceContext.PreTurnPlayerActionCurrent;
    }

    private static JsonNode? ResolveNextPriorControlState(JsonNode? priorControlState, JsonObject exchange)
    {
        if (exchange["after"] is not JsonObject after ||
            !after.ContainsKey("controlState"))
        {
            return priorControlState?.DeepClone();
        }

        return after["controlState"]?.DeepClone();
    }

    private static void ValidateFinalActiveControlStateMatchesExchangeSnapshots(
        JsonObject conflict,
        JsonNode? auditedControlState,
        string context,
        List<ValidationIssue> issues,
        bool requiredForCurrentTurn)
    {
        if (!requiredForCurrentTurn)
            return;

        var finalControlState = conflict.ContainsKey("controlState")
            ? conflict["controlState"]
            : null;
        if (!ControlStateChangedSemantically(auditedControlState, finalControlState))
            return;

        AddSpiritualArtRuleIssue(
            issues,
            $"{context}.controlState",
            "Итоговый activeConflict.controlState должен совпадать с последним audited exchange.after.controlState текущего хода.",
            "afterlife_conflict_control_snapshot_missing",
            "activeConflict.controlState matches the control state derived from current exchange.after.controlState snapshots",
            "root controlState differs from audited exchange controlState");
    }

    private static void ValidateSpiritualArtOperationRules(
        JsonObject exchange,
        JsonObject before,
        JsonObject after,
        string? operationType,
        string? outcome,
        string context,
        List<ValidationIssue> issues,
        bool isCurrentExchange,
        bool requiresCurrentMatchupAudit)
    {
        if (string.IsNullOrWhiteSpace(operationType))
            return;

        ValidateMatchupAudit(exchange, operationType, outcome, context, issues, requiresCurrentMatchupAudit);
        ValidateOperationIsNotRestrictedByOppositionControl(operationType, outcome, before, context, issues);

        if (ConflictTokenEquals(operationType, "pressure"))
            ValidatePressureRule(exchange, before, after, outcome, context, issues);

        if (ConflictTokenEquals(operationType, "guard"))
            ValidateGuardRule(exchange, before, after, outcome, context, issues);

        if (ConflictTokenEquals(operationType, "counter") &&
            exchange["incomingAction"] is not JsonObject)
        {
            AddSpiritualArtRuleIssue(
                issues,
                $"{context}.incomingAction",
                "Контрприём (counter) является реакцией и должен указывать incomingAction.",
                "afterlife_conflict_counter_missing_incoming_action",
                "incomingAction object that names the operation being countered",
                exchange["incomingAction"]?.GetType().Name ?? "missing");
        }

        if (ConflictTokenEquals(operationType, "counter") &&
            IsCounterPayoffOutcome(outcome) &&
            !HasCounterPayoff(exchange, before, after))
        {
            AddSpiritualArtRuleIssue(
                issues,
                $"{context}.counterPayoff",
                "Успешный контрприём (counter) должен иметь измеримый payoff: сорвать входящее действие и получить встречный выигрыш.",
                "afterlife_conflict_counter_missing_payoff",
                "counterPayoff object, improved conflictPosition, or worsened oppositionSideStrain",
                "no counter payoff");
        }

        if (ConflictTokenEquals(operationType, "counter"))
            ValidateCounterMatchupRule(exchange, before, after, outcome, context, issues);

        if (ConflictTokenEquals(operationType, "maneuver"))
            ValidateManeuverRule(before, after, outcome, context, issues);

        if (ConflictTokenEquals(operationType, "binding", "force_binding") &&
            IsSuccessfulArtOutcome(outcome) &&
            !HasBindingLeverage(exchange, before))
        {
            AddSpiritualArtRuleIssue(
                issues,
                $"{context}.operationType",
                "Наложение оков (binding/force_binding) требует преимущества, setup или decisive_player_success.",
                "afterlife_conflict_binding_without_leverage",
                "before.conflictPosition=player_advantaged|player_dominant, setup=true, or diceAudit.outcomeBand=decisive_player_success",
                DescribeConflictPosition(before));
        }

        if (ConflictTokenEquals(operationType, "force_binding") &&
            IsSuccessfulArtOutcome(outcome) &&
            !HasStrongBindingLeverage(exchange, before))
        {
            AddSpiritualArtRuleIssue(
                issues,
                $"{context}.operationType",
                "Силовые оковы (force_binding) требуют доминирования, готовой подготовки или decisive_player_success.",
                "afterlife_conflict_force_binding_without_strong_leverage",
                "before.conflictPosition=player_dominant, setup/bindingSetup=ready, or diceAudit.outcomeBand=decisive_player_success",
                DescribeConflictPosition(before));
        }

        if (ConflictTokenEquals(operationType, "binding", "force_binding") &&
            IsSuccessfulArtOutcome(outcome))
        {
            if (HasActiveOppositionControl(before))
            {
                AddSpiritualArtRuleIssue(
                    issues,
                    $"{context}.before.controlState",
                    "Наложение оков (binding/force_binding) не может создавать контроль игрока поверх активного контроля противника.",
                    "afterlife_conflict_binding_under_opposition_control",
                    "first answer opposition control with break_binding, valid counter, or incarnation_resistance for force_incarnation control",
                    DescribeControlTransition(before, after));
            }
            else if (!TryGetPlayerControlProgression(before, after, out var beforePlayerControlRank, out var afterPlayerControlRank))
            {
                if (isCurrentExchange)
                {
                    AddSpiritualArtRuleIssue(
                        issues,
                        $"{context}.after.controlState",
                        "Успешное наложение оков (binding/force_binding) должно измеримо создать или усилить контроль игрока.",
                        "afterlife_conflict_binding_missing_control_delta",
                        "after.controlState level stronger than before and controllerSide=player",
                        DescribeControlTransition(before, after));
                }
            }
            else if (afterPlayerControlRank != beforePlayerControlRank + 1)
            {
                AddSpiritualArtRuleIssue(
                    issues,
                    $"{context}.after.controlState",
                    "Наложение оков (binding/force_binding) усиливает контроль только на один шаг: none -> hindered -> bound -> locked.",
                    "afterlife_conflict_binding_control_step_too_large",
                    "control rank increases by exactly one step",
                    $"{beforePlayerControlRank}->{afterPlayerControlRank}");
            }

            if (ConflictTokenEquals(operationType, "force_binding") &&
                after["controlState"] is JsonObject afterControl &&
                GetControlRestrictionSet(afterControl).Count < 2)
            {
                AddSpiritualArtRuleIssue(
                    issues,
                    $"{context}.after.controlState.restrictedOperations",
                    "Силовые оковы (force_binding) должны отличаться от обычных оков более широким payoff: минимум две ограниченные операции.",
                    "afterlife_conflict_force_binding_without_broad_control_payoff",
                    "restrictedOperations contains at least two distinct operation ids for force_binding",
                    afterControl["restrictedOperations"]?.ToJsonString() ?? "missing");
            }
        }
        else if (ConflictTokenEquals(operationType, "binding", "force_binding") &&
                 (HasPlayerControlDelta(before, after) || HasAntiControlDelta(before, after)))
        {
            AddSpiritualArtRuleIssue(
                issues,
                $"{context}.after.controlState",
                "Неуспешное наложение оков (binding/force_binding) не может менять controlState игрока или противника.",
                "afterlife_conflict_binding_control_delta_on_failed_outcome",
                "controlState unchanged on blocked/countered/setback binding outcomes",
                DescribeControlTransition(before, after));
        }

        if (ConflictTokenEquals(operationType, "break_binding") &&
            !HasBindingOrCoerciveContext(exchange, before, after))
        {
            AddSpiritualArtRuleIssue(
                issues,
                $"{context}.operationType",
                "Разрыв оков (break_binding) должен ссылаться на binding/coercive context.",
                "afterlife_conflict_break_binding_without_binding",
                "incomingAction or before/after snapshot with binding/forced handoff context",
                "missing binding/coercive context");
        }

        if (ConflictTokenEquals(operationType, "break_binding") &&
            IsSuccessfulArtOutcome(outcome) &&
            !HasAntiControlDelta(before, after))
        {
            AddSpiritualArtRuleIssue(
                issues,
                $"{context}.after.controlState",
                "Успешный разрыв оков (break_binding) должен измеримо ослабить, снять или развернуть контроль против игрока.",
                "afterlife_conflict_break_binding_missing_control_delta",
                "opposition control weakened/removed/reversed, or coercive handoff cleared",
                DescribeControlTransition(before, after));
        }
        else if (ConflictTokenEquals(operationType, "break_binding") &&
                 !IsSuccessfulArtOutcome(outcome) &&
                 HasAntiControlDelta(before, after))
        {
            AddSpiritualArtRuleIssue(
                issues,
                $"{context}.after.controlState",
                "Неуспешный разрыв оков (break_binding) не может ослаблять, снимать или разворачивать контроль против игрока.",
                "afterlife_conflict_break_binding_control_delta_on_failed_outcome",
                "opposition control unchanged on blocked/countered/setback break_binding outcomes",
                DescribeControlTransition(before, after));
        }

        if (ConflictTokenEquals(operationType, "incarnation_resistance") &&
            !HasForcedIncarnationContext(exchange, before, after))
        {
            AddSpiritualArtRuleIssue(
                issues,
                $"{context}.operationType",
                "Сопротивление воплощению (incarnation_resistance) применимо только против force_incarnation/guardian_forced.",
                "afterlife_conflict_incarnation_resistance_without_force",
                "force_incarnation incomingAction/resolution/source context",
                "missing forced-incarnation context");
        }

        if (ConflictTokenEquals(operationType, "incarnation_resistance") &&
            HasActiveControlState(before) &&
            ControlStateChangedSemantically(before["controlState"], after["controlState"]) &&
            !HasForcedIncarnationControlState(before))
        {
            AddSpiritualArtRuleIssue(
                issues,
                $"{context}.after.controlState",
                "Сопротивление воплощению (incarnation_resistance) может менять или снимать только controlState, созданный force_incarnation.",
                "afterlife_conflict_incarnation_resistance_clears_non_force_control",
                "before.controlState.sourceOperation=force_incarnation for control changes/removal",
                DescribeControlTransition(before, after));
        }

        if (ConflictTokenEquals(operationType, "incarnation_resistance") &&
            TryGetPlayerControlProgression(before, after, out _, out _))
        {
            AddSpiritualArtRuleIssue(
                issues,
                $"{context}.after.controlState",
                "Сопротивление воплощению (incarnation_resistance) не может создавать или усиливать контроль игрока; свежий контроль создается только binding/force_binding или валидным counter против существующего контроля.",
                "afterlife_conflict_incarnation_resistance_creates_fresh_control",
                "no fresh player control from incarnation_resistance",
                DescribeControlTransition(before, after));
        }

        if (ConflictTokenEquals(operationType, "incarnation_resistance") &&
            !IsSuccessfulArtOutcome(outcome) &&
            ControlStateChangedSemantically(before["controlState"], after["controlState"]))
        {
            AddSpiritualArtRuleIssue(
                issues,
                $"{context}.after.controlState",
                "Неуспешное сопротивление воплощению (incarnation_resistance) не может ослаблять, снимать или переписывать controlState.",
                "afterlife_conflict_incarnation_resistance_control_delta_on_failed_outcome",
                "controlState unchanged on blocked/countered/setback incarnation_resistance outcomes",
                DescribeControlTransition(before, after));
        }

        ValidateControlSourceOperationMatchesExchange(exchange, before, after, operationType, outcome, context, issues);

        if (ConflictTokenEquals(operationType, "champion_coordination") &&
            !HasChampionDuelContext(exchange, before, after))
        {
            AddSpiritualArtRuleIssue(
                issues,
                $"{context}.operationType",
                "Координация чемпиона (champion_coordination) применима только в champion_duel.",
                "afterlife_conflict_champion_coordination_without_champion",
                "sideModel/conflictMode=champion_duel",
                "missing champion_duel context");
        }
    }

    private static void ValidateMatchupAudit(
        JsonObject exchange,
        string operationType,
        string? outcome,
        string context,
        List<ValidationIssue> issues,
        bool requiresCurrentMatchupAudit)
    {
        if (!IsTacticalCombatOperation(operationType) &&
            !requiresCurrentMatchupAudit &&
            exchange["matchupAudit"] is not JsonObject)
        {
            return;
        }

        if (exchange["matchupAudit"] is not JsonObject matchupAudit)
        {
            if (requiresCurrentMatchupAudit)
            {
                AddSpiritualArtRuleIssue(
                    issues,
                    $"{context}.matchupAudit",
                    "Новый спорный обмен духовного боя должен иметь matchupAudit с приёмом, контрприёмом и профилем риска.",
                    "afterlife_conflict_matchup_audit_missing",
                    "matchupAudit object with playerOperation/oppositionOperation/primaryResolutionLane/matchupRationale/riskProfile",
                    exchange["matchupAudit"]?.GetType().Name ?? "missing");
            }

            return;
        }

        var playerOperation = RequireMatchupString(matchupAudit, $"{context}.matchupAudit", issues, "playerOperation");
        if (!string.IsNullOrWhiteSpace(playerOperation) &&
            !ConflictTokenEquals(playerOperation, operationType))
        {
            AddSpiritualArtRuleIssue(
                issues,
                $"{context}.matchupAudit.playerOperation",
                "matchupAudit.playerOperation должен совпадать с основным operationType обмена.",
                "afterlife_conflict_matchup_player_operation_mismatch",
                operationType,
                playerOperation);
        }

        var oppositionOperation = RequireMatchupString(matchupAudit, $"{context}.matchupAudit", issues, "oppositionOperation");
        if (!string.IsNullOrWhiteSpace(oppositionOperation) &&
            !IsSupportedMatchupOperation(oppositionOperation))
        {
            AddSpiritualArtRuleIssue(
                issues,
                $"{context}.matchupAudit.oppositionOperation",
                "matchupAudit.oppositionOperation должен быть supported combat operation или none/passive.",
                "afterlife_conflict_matchup_invalid_opposition_operation",
                string.Join("/", AfterlifeSpiritualConflictState.OperationTypes.OrderBy(value => value, StringComparer.OrdinalIgnoreCase)) + "/none/passive",
                oppositionOperation);
        }

        var hasIncomingAction = exchange["incomingAction"] is JsonObject;
        var incomingActionOperations = ResolveIncomingActionOperationsForMatchup(exchange);
        if (!string.IsNullOrWhiteSpace(oppositionOperation) &&
            IsSupportedMatchupOperation(oppositionOperation) &&
            hasIncomingAction &&
            (incomingActionOperations.Count == 0 ||
             !incomingActionOperations.Any(incomingOperation => ConflictTokenEquals(oppositionOperation, incomingOperation))))
        {
            AddSpiritualArtRuleIssue(
                issues,
                $"{context}.matchupAudit.oppositionOperation",
                "matchupAudit.oppositionOperation должен совпадать с incomingAction.finalOperationType, если он указан; иначе с incomingAction.operationType.",
                "afterlife_conflict_matchup_opposition_operation_mismatch",
                incomingActionOperations.Count == 0
                    ? "incomingAction.operationType or incomingAction.finalOperationType"
                    : string.Join("/", incomingActionOperations),
                oppositionOperation);
        }

        if (requiresCurrentMatchupAudit &&
            !string.IsNullOrWhiteSpace(oppositionOperation) &&
            IsSupportedMatchupOperation(oppositionOperation))
        {
            ValidateMatchupRelationship(operationType, oppositionOperation, outcome, context, issues);
        }

        var primaryResolutionLane = RequireMatchupString(matchupAudit, $"{context}.matchupAudit", issues, "primaryResolutionLane");
        if (!string.IsNullOrWhiteSpace(primaryResolutionLane) &&
            !ConflictTokenEquals(primaryResolutionLane, operationType))
        {
            AddSpiritualArtRuleIssue(
                issues,
                $"{context}.matchupAudit.primaryResolutionLane",
                "matchupAudit.primaryResolutionLane должен указывать основной приём, который задаёт allowed state delta.",
                "afterlife_conflict_matchup_primary_lane_mismatch",
                operationType,
                primaryResolutionLane);
        }

        var riskProfile = RequireMatchupString(matchupAudit, $"{context}.matchupAudit", issues, "riskProfile");
        var expectedRiskProfile = ExpectedRiskProfileForOperation(operationType);
        if (!string.IsNullOrWhiteSpace(riskProfile) &&
            !string.Equals(riskProfile, expectedRiskProfile, StringComparison.OrdinalIgnoreCase))
        {
            AddSpiritualArtRuleIssue(
                issues,
                $"{context}.matchupAudit.riskProfile",
                "matchupAudit.riskProfile должен фиксировать tactical tradeoff выбранного приёма.",
                "afterlife_conflict_matchup_invalid_risk_profile",
                expectedRiskProfile,
                riskProfile);
        }

        RequireMatchupString(matchupAudit, $"{context}.matchupAudit", issues, "matchupRationale");
    }

    private static void ValidateMatchupRelationship(
        string operationType,
        string oppositionOperation,
        string? outcome,
        string context,
        List<ValidationIssue> issues)
    {
        if (!IsSuccessfulArtOutcome(outcome) ||
            !IsMatrixCounterForSuccessfulOperation(operationType, oppositionOperation))
        {
            return;
        }

        AddSpiritualArtRuleIssue(
            issues,
            $"{context}.matchupAudit.oppositionOperation",
            "matchupAudit противоречит tactical matrix: успешный результат не может игнорировать прямо контрящий приём противника.",
            "afterlife_conflict_matchup_matrix_violation",
            "successful/partial_success only against an operation not listed as a direct counter",
            $"{operationType} vs {oppositionOperation}");
    }

    private static void ValidatePressureRule(
        JsonObject exchange,
        JsonObject before,
        JsonObject after,
        string? outcome,
        string context,
        List<ValidationIssue> issues)
    {
        if (IsSuccessfulArtOutcome(outcome))
        {
            var hasBeforeOppositionStrain = TryGetStrainRank(before["oppositionSideStrain"], out var beforeOppositionStrain);
            var hasAfterOppositionStrain = TryGetStrainRank(after["oppositionSideStrain"], out var afterOppositionStrain);
            if (!hasBeforeOppositionStrain ||
                !hasAfterOppositionStrain ||
                afterOppositionStrain <= beforeOppositionStrain)
            {
                var actual = !hasBeforeOppositionStrain
                    ? "before.oppositionSideStrain missing or invalid"
                    : !hasAfterOppositionStrain
                        ? "after.oppositionSideStrain missing or invalid"
                        : $"{beforeOppositionStrain}->{afterOppositionStrain}";
                AddSpiritualArtRuleIssue(
                    issues,
                    hasBeforeOppositionStrain
                        ? $"{context}.after.oppositionSideStrain"
                        : $"{context}.before.oppositionSideStrain",
                    "Успешное давление (pressure) должно измеримо ухудшать oppositionSideStrain.",
                    "afterlife_conflict_pressure_missing_opposition_strain_delta",
                    "oppositionSideStrain worsened on success/partial_success",
                    actual);
            }
        }

        if (TryGetPositionRank(before["conflictPosition"], out var beforePosition) &&
            TryGetPositionRank(after["conflictPosition"], out var afterPosition) &&
            afterPosition > beforePosition)
        {
            AddSpiritualArtRuleIssue(
                issues,
                $"{context}.after.conflictPosition",
                "Давление (pressure) не должно работать как бесплатный манёвр позиции.",
                "afterlife_conflict_pressure_changes_position",
                "use maneuver for conflictPosition improvement",
                $"{beforePosition}->{afterPosition}");
        }

        if (AddsBindingOrControlState(exchange, before, after))
        {
            AddSpiritualArtRuleIssue(
                issues,
                $"{context}.operationType",
                "Давление (pressure) не должно накладывать, менять или снимать оковы/контроль; для этого используй binding/force_binding, break_binding или counter.",
                "afterlife_conflict_pressure_adds_binding",
                "pressure changes oppositionSideStrain only",
                "binding/control state changed");
        }
    }

    private static void ValidateGuardRule(
        JsonObject exchange,
        JsonObject before,
        JsonObject after,
        string? outcome,
        string context,
        List<ValidationIssue> issues)
    {
        if (IsSuccessfulArtOutcome(outcome) &&
            TryGetStrainRank(before["playerSideStrain"], out var beforePlayerStrain) &&
            TryGetStrainRank(after["playerSideStrain"], out var afterPlayerStrain) &&
            afterPlayerStrain > beforePlayerStrain)
        {
            AddSpiritualArtRuleIssue(
                issues,
                $"{context}.after.playerSideStrain",
                "Успешная защита (guard) не должна ухудшать playerSideStrain.",
                "afterlife_conflict_guard_worsens_player_strain",
                "playerSideStrain unchanged or improved for successful guard",
                $"{beforePlayerStrain}->{afterPlayerStrain}");
        }

        if (ConflictTokenEquals(outcome, "setback") &&
            IncomingActionHasOperation(exchange, "pressure"))
        {
            var hasBeforeGuardStrain = TryGetStrainRank(before["playerSideStrain"], out var beforeGuardStrain);
            var hasAfterGuardStrain = TryGetStrainRank(after["playerSideStrain"], out var afterGuardStrain);
            if (!hasBeforeGuardStrain || !hasAfterGuardStrain || afterGuardStrain - beforeGuardStrain > 1)
            {
                var actual = !hasBeforeGuardStrain
                    ? "before.playerSideStrain missing or invalid"
                    : !hasAfterGuardStrain
                        ? "after.playerSideStrain missing or invalid"
                        : $"{beforeGuardStrain}->{afterGuardStrain}";
                AddSpiritualArtRuleIssue(
                    issues,
                    hasBeforeGuardStrain
                        ? $"{context}.after.playerSideStrain"
                        : $"{context}.before.playerSideStrain",
                    "Даже проваленная защита (guard) против давления должна смягчать удар: playerSideStrain не может ухудшиться больше чем на один уровень.",
                    "afterlife_conflict_guard_missing_mitigation_floor",
                    "playerSideStrain worsens by at most one rank on setback guard against pressure",
                    actual);
            }
        }

        if (TryGetStrainRank(before["oppositionSideStrain"], out var beforeOppositionStrain) &&
            TryGetStrainRank(after["oppositionSideStrain"], out var afterOppositionStrain) &&
            afterOppositionStrain > beforeOppositionStrain)
        {
            AddSpiritualArtRuleIssue(
                issues,
                $"{context}.after.oppositionSideStrain",
                "Защита (guard) не должна напрямую наносить strain противнику.",
                "afterlife_conflict_guard_deals_opposition_strain",
                "oppositionSideStrain unchanged for guard",
                $"{beforeOppositionStrain}->{afterOppositionStrain}");
        }

        if (TryGetPositionRank(before["conflictPosition"], out var beforePosition) &&
            TryGetPositionRank(after["conflictPosition"], out var afterPosition) &&
            afterPosition > beforePosition)
        {
            AddSpiritualArtRuleIssue(
                issues,
                $"{context}.after.conflictPosition",
                "Защита (guard) не должна напрямую улучшать conflictPosition; для этого используй maneuver или counter payoff.",
                "afterlife_conflict_guard_improves_position",
                "conflictPosition unchanged or preserved for guard",
                $"{beforePosition}->{afterPosition}");
        }

        if (ControlStateChangedSemantically(before["controlState"], after["controlState"]) &&
            !GuardSetbackRecordsIncomingControl(exchange, before, after, outcome))
        {
            AddSpiritualArtRuleIssue(
                issues,
                $"{context}.after.controlState",
                "Защита (guard) может предотвратить новый входящий контроль, но не создает, не снимает и не меняет действующий controlState.",
                "afterlife_conflict_guard_changes_control",
                "controlState semantically unchanged for guard",
                DescribeControlTransition(before, after));
        }
    }

    private static bool GuardSetbackRecordsIncomingControl(
        JsonObject exchange,
        JsonObject before,
        JsonObject after,
        string? outcome)
    {
        if (!ConflictTokenEquals(outcome, "setback") ||
            exchange["incomingAction"] is not JsonObject incomingAction ||
            !IncomingActionIsControlOperation(incomingAction) ||
            !TryGetControlSnapshot(after, out var afterControl) ||
            afterControl.Rank <= 0 ||
            !string.Equals(afterControl.ControllerSide, "opposition", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (TryGetControlSnapshot(before, out var beforeControl))
        {
            if (beforeControl.Rank > 0)
            {
                if (!string.Equals(beforeControl.ControllerSide, "opposition", StringComparison.OrdinalIgnoreCase))
                    return false;

                if (afterControl.Rank < beforeControl.Rank)
                    return false;
            }
        }

        var sourceOperation = AfterlifeSpiritualConflictState.GetNodeString((after["controlState"] as JsonObject)?["sourceOperation"]);
        var incomingOperations = ResolveIncomingActionOperations(exchange);
        return !string.IsNullOrWhiteSpace(sourceOperation) &&
               incomingOperations.Any(incomingOperation => ConflictTokenEquals(sourceOperation, incomingOperation));
    }

    private static void ValidateControlSourceOperationMatchesExchange(
        JsonObject exchange,
        JsonObject before,
        JsonObject after,
        string operationType,
        string? outcome,
        string context,
        List<ValidationIssue> issues)
    {
        if (!ControlStateChangedSemantically(before["controlState"], after["controlState"]) ||
            !TryGetControlSnapshot(after, out var afterControl) ||
            afterControl.Rank <= 0)
        {
            return;
        }

        if (GuardSetbackRecordsIncomingControl(exchange, before, after, outcome))
            return;

        if (IncarnationResistanceRetainsForcedControlSource(before, after, operationType))
            return;

        var sourceOperation = AfterlifeSpiritualConflictState.GetNodeString((after["controlState"] as JsonObject)?["sourceOperation"]);
        if (ConflictTokenEquals(sourceOperation, operationType))
            return;

        AddSpiritualArtRuleIssue(
            issues,
            $"{context}.after.controlState.sourceOperation",
            "sourceOperation активного controlState должен совпадать с operationType обмена, который создал или изменил контроль.",
            "afterlife_conflict_control_source_operation_mismatch",
            operationType,
            string.IsNullOrWhiteSpace(sourceOperation) ? "missing" : sourceOperation);
    }

    private static bool IncarnationResistanceRetainsForcedControlSource(
        JsonObject before,
        JsonObject after,
        string operationType)
    {
        if (!ConflictTokenEquals(operationType, "incarnation_resistance") ||
            !TryGetControlSnapshot(before, out var beforeControl) ||
            !TryGetControlSnapshot(after, out var afterControl) ||
            beforeControl.Rank <= 0 ||
            afterControl.Rank <= 0 ||
            afterControl.Rank >= beforeControl.Rank ||
            !string.Equals(beforeControl.ControllerSide, "opposition", StringComparison.OrdinalIgnoreCase) ||
            !string.Equals(afterControl.ControllerSide, "opposition", StringComparison.OrdinalIgnoreCase) ||
            before["controlState"] is not JsonObject beforeControlState ||
            after["controlState"] is not JsonObject afterControlState)
        {
            return false;
        }

        var beforeControlId = AfterlifeSpiritualConflictState.GetNodeString(beforeControlState["controlId"]);
        var afterControlId = AfterlifeSpiritualConflictState.GetNodeString(afterControlState["controlId"]);
        return !string.IsNullOrWhiteSpace(beforeControlId) &&
               string.Equals(beforeControlId, afterControlId, StringComparison.OrdinalIgnoreCase) &&
               ConflictNodeStringEquals(beforeControlState, "force_incarnation", "sourceOperation") &&
               ConflictNodeStringEquals(afterControlState, "force_incarnation", "sourceOperation");
    }

    private static void ValidateCounterMatchupRule(
        JsonObject exchange,
        JsonObject before,
        JsonObject after,
        string? outcome,
        string context,
        List<ValidationIssue> issues)
    {
        var incomingOperation = ResolveIncomingOperation(exchange);
        if (!IsAllowedCounterTargetOperation(incomingOperation))
        {
            AddSpiritualArtRuleIssue(
                issues,
                $"{context}.incomingAction.operationType",
                "Контрприём (counter) применим только против конкретного прямого давления, контроля или принуждения, а не против защиты, манёвра, пассивности, переговоров или выхода.",
                "afterlife_conflict_counter_invalid_target_operation",
                "pressure/binding/force_binding/force_incarnation/break_binding/incarnation_resistance",
                string.IsNullOrWhiteSpace(incomingOperation) ? "missing" : incomingOperation);
        }

        if (ConflictTokenEquals(outcome, "setback") &&
            !HasCounterFailureDownside(exchange, before, after))
        {
            AddSpiritualArtRuleIssue(
                issues,
                $"{context}.after",
                "Провал контрприёма (counter) должен быть рискованнее защиты: нужен worsened playerSideStrain, worsened conflictPosition или counterBackfire.",
                "afterlife_conflict_counter_setback_without_downside",
                "playerSideStrain worsened, conflictPosition worsened, or counterBackfire object",
                "no counter downside");
        }

        if (CounterAdvancesPlayerControl(before, after))
        {
            AddSpiritualArtRuleIssue(
                issues,
                $"{context}.after.controlState",
                "Контрприём (counter) не может создавать или усиливать контроль игрока; новый/усиленный controlState создаётся через binding/force_binding.",
                "afterlife_conflict_counter_creates_fresh_control",
                "counter may weaken/reverse existing opposition control, not create or strengthen player control",
                DescribeControlTransition(before, after));
        }
    }

    private static void ValidateManeuverRule(
        JsonObject before,
        JsonObject after,
        string? outcome,
        string context,
        List<ValidationIssue> issues)
    {
        if (TryGetStrainRank(before["playerSideStrain"], out var beforePlayerStrain) &&
            TryGetStrainRank(after["playerSideStrain"], out var afterPlayerStrain) &&
            beforePlayerStrain != afterPlayerStrain)
        {
            AddSpiritualArtRuleIssue(
                issues,
                $"{context}.after.playerSideStrain",
                "Манёвр (maneuver) меняет позицию, а не side strain.",
                "afterlife_conflict_maneuver_changes_strain",
                "playerSideStrain unchanged for maneuver",
                $"{beforePlayerStrain}->{afterPlayerStrain}");
        }

        if (TryGetStrainRank(before["oppositionSideStrain"], out var beforeOppositionStrain) &&
            TryGetStrainRank(after["oppositionSideStrain"], out var afterOppositionStrain) &&
            beforeOppositionStrain != afterOppositionStrain)
        {
            AddSpiritualArtRuleIssue(
                issues,
                $"{context}.after.oppositionSideStrain",
                "Манёвр (maneuver) меняет позицию, а не side strain.",
                "afterlife_conflict_maneuver_changes_strain",
                "oppositionSideStrain unchanged for maneuver",
                $"{beforeOppositionStrain}->{afterOppositionStrain}");
        }

        if (ControlStateChangedSemantically(before["controlState"], after["controlState"]))
        {
            AddSpiritualArtRuleIssue(
                issues,
                $"{context}.after.controlState",
                "Манёвр (maneuver) меняет позицию и не может создавать, снимать или ослаблять controlState.",
                "afterlife_conflict_maneuver_changes_control",
                "controlState unchanged for maneuver",
                DescribeControlTransition(before, after));
        }

        if (IsSuccessfulArtOutcome(outcome) &&
            TryGetPositionRank(before["conflictPosition"], out var beforePosition) &&
            TryGetPositionRank(after["conflictPosition"], out var afterPosition) &&
            beforePosition == afterPosition)
        {
            AddSpiritualArtRuleIssue(
                issues,
                $"{context}.after.conflictPosition",
                "Успешный манёвр (maneuver) должен измеримо менять conflictPosition.",
                "afterlife_conflict_maneuver_missing_position_shift",
                "conflictPosition changed on success/partial_success",
                "unchanged");
        }

        if (IsSuccessfulArtOutcome(outcome) &&
            HasActiveOppositionControl(before) &&
            TryGetPositionRank(before["conflictPosition"], out var beforePositionUnderControl) &&
            TryGetPositionRank(after["conflictPosition"], out var afterPositionUnderControl) &&
            afterPositionUnderControl > beforePositionUnderControl)
        {
            AddSpiritualArtRuleIssue(
                issues,
                $"{context}.after.conflictPosition",
                "Манёвр (maneuver) не может свободно улучшать позицию, пока игрок находится под активным контролем противника.",
                "afterlife_conflict_maneuver_blocked_by_control",
                "remove/weaken control first via break_binding, valid counter, incarnation_resistance, negotiate, or surrender",
                DescribeControlTransition(before, after));
        }
    }

    private static bool HasBindingLeverage(JsonObject exchange, JsonObject before)
    {
        if (TryGetPositionRank(before["conflictPosition"], out var position) && position >= 1)
            return true;

        if (TryGetJsonNodeBool(exchange["setup"], out var setup) && setup)
            return true;

        if (string.Equals(AfterlifeSpiritualConflictState.GetNodeString(exchange["setupState"]), "ready", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(AfterlifeSpiritualConflictState.GetNodeString(exchange["bindingSetup"]), "ready", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (exchange["diceAudit"] is JsonObject diceAudit &&
            string.Equals(AfterlifeSpiritualConflictState.GetNodeString(diceAudit["outcomeBand"]), "decisive_player_success", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return false;
    }

    private static bool HasStrongBindingLeverage(JsonObject exchange, JsonObject before)
    {
        if (TryGetPositionRank(before["conflictPosition"], out var position) && position >= 2)
            return true;

        if (string.Equals(AfterlifeSpiritualConflictState.GetNodeString(exchange["setupState"]), "ready", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(AfterlifeSpiritualConflictState.GetNodeString(exchange["bindingSetup"]), "ready", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (exchange["diceAudit"] is JsonObject diceAudit &&
            string.Equals(AfterlifeSpiritualConflictState.GetNodeString(diceAudit["outcomeBand"]), "decisive_player_success", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return false;
    }

    private static bool HasBindingOrCoerciveContext(params JsonObject[] roots) =>
        roots.Any(root =>
            ConflictNodeStringEquals(root, "binding", "operationType", "finalOperationType") ||
            ConflictNodeStringEquals(root, "force_binding", "operationType", "finalOperationType") ||
            ConflictNodeStringEquals(root, "force_incarnation", "operationType", "finalOperationType") ||
            ConflictNodeStringEquals(root, "guardian_forced", "source", "reason", "consequence") ||
            HasActiveControlState(root) ||
            root.ContainsKey("bindingState") ||
            root.ContainsKey("bindingId") ||
            root.ContainsKey("activeBinding") ||
            root.ContainsKey("forcedHandoff") ||
            root.ContainsKey("forceIncarnation") ||
            root.ContainsKey("forcedIncarnation") ||
            root["incomingAction"] is JsonObject incoming && HasBindingOrCoerciveContext(incoming));

    private static bool HasForcedIncarnationContext(params JsonObject[] roots) =>
        roots.Any(root =>
            ConflictNodeStringEquals(root, "force_incarnation", "operationType", "finalOperationType") ||
            ConflictNodeStringEquals(root, "guardian_forced", "source", "reason", "consequence") ||
            HasForcedIncarnationControlState(root) ||
            root.ContainsKey("forceIncarnation") ||
            root.ContainsKey("forcedIncarnation") ||
            root["incomingAction"] is JsonObject incoming && HasForcedIncarnationContext(incoming));

    private static bool HasForcedIncarnationControlState(JsonObject root)
    {
        if (!TryGetControlSnapshot(root, out var control) ||
            control.Rank <= 0 ||
            root["controlState"] is not JsonObject controlState)
        {
            return false;
        }

        return ConflictNodeStringEquals(controlState, "force_incarnation", "sourceOperation", "operationType", "finalOperationType");
    }

    private static bool HasChampionDuelContext(params JsonObject[] roots) =>
        roots.Any(root =>
            ConflictNodeStringEquals(root, "champion_duel", "sideModel", "conflictMode", "conflictModel", "duelType") ||
            root["playerSide"]?["leadContestant"] is JsonObject lead &&
            !ConflictTokenEquals(AfterlifeSpiritualConflictState.GetNodeString(lead["actorType"]), "player", "soul"));

    private static bool IsSuccessfulArtOutcome(string? outcome) =>
        ConflictTokenEquals(outcome, "success", "partial_success");

    private static bool IsCounterPayoffOutcome(string? outcome) =>
        ConflictTokenEquals(outcome, "success", "partial_success", "countered");

    private static bool HasCounterPayoff(JsonObject exchange, JsonObject before, JsonObject after)
    {
        if (exchange["counterPayoff"] is JsonObject counterPayoff &&
            HasMeaningfulCounterPayoff(counterPayoff))
        {
            return true;
        }

        if (TryGetPositionRank(before["conflictPosition"], out var beforePosition) &&
            TryGetPositionRank(after["conflictPosition"], out var afterPosition) &&
            afterPosition > beforePosition)
        {
            return true;
        }

        if (TryGetStrainRank(before["oppositionSideStrain"], out var beforeOppositionStrain) &&
            TryGetStrainRank(after["oppositionSideStrain"], out var afterOppositionStrain) &&
            afterOppositionStrain > beforeOppositionStrain)
        {
            return true;
        }

        if (HasControlCounterPayoff(before, after))
            return true;

        return false;
    }

    private static bool HasMeaningfulCounterPayoff(JsonObject counterPayoff) =>
        counterPayoff.Any(property => HasMeaningfulJsonValue(property.Value));

    private static bool HasMeaningfulJsonValue(JsonNode? node) =>
        node switch
        {
            null => false,
            JsonValue value => !string.IsNullOrWhiteSpace(AfterlifeSpiritualConflictState.GetNodeString(value)) ||
                value.TryGetValue<int>(out _) ||
                value.TryGetValue<long>(out _) ||
                value.TryGetValue<double>(out _) ||
                value.TryGetValue<bool>(out _),
            JsonObject obj => obj.Any(property => HasMeaningfulJsonValue(property.Value)),
            JsonArray array => array.Any(HasMeaningfulJsonValue),
            _ => true
        };

    private static string? RequireMatchupString(
        JsonObject matchupAudit,
        string context,
        List<ValidationIssue> issues,
        string fieldName)
    {
        var value = AfterlifeSpiritualConflictState.GetNodeString(matchupAudit[fieldName]);
        if (!string.IsNullOrWhiteSpace(value))
            return value;

        AddSpiritualArtRuleIssue(
            issues,
            $"{context}.{fieldName}",
            $"matchupAudit.{fieldName} должен быть непустой строкой.",
            "afterlife_conflict_matchup_missing_field",
            "non-empty string",
            matchupAudit[fieldName]?.GetType().Name ?? "missing");
        return value;
    }

    private static bool IsTacticalCombatOperation(string? operationType) =>
        !ConflictTokenEquals(operationType, "withdraw", "surrender", "negotiate");

    private static bool IsSupportedMatchupOperation(string operationType) =>
        AfterlifeSpiritualConflictState.OperationTypes.Contains(operationType) ||
        ConflictTokenEquals(operationType, "none", "passive");

    private static void ValidateOperationIsNotRestrictedByOppositionControl(
        string operationType,
        string? outcome,
        JsonObject before,
        string context,
        List<ValidationIssue> issues)
    {
        if (!IsSuccessfulArtOutcome(outcome) ||
            ConflictTokenEquals(operationType, "break_binding", "incarnation_resistance", "counter", "withdraw", "surrender", "negotiate") ||
            before["controlState"] is not JsonObject beforeControl ||
            !HasActiveOppositionControl(before))
        {
            return;
        }

        var restrictedOperations = GetControlRestrictionSet(beforeControl);
        if (!restrictedOperations.Contains(operationType))
            return;

        AddSpiritualArtRuleIssue(
            issues,
            $"{context}.before.controlState.restrictedOperations",
            "Активный контроль противника запрещает успешное применение указанного духовного искусства до разрыва/ослабления контроля.",
            "afterlife_conflict_operation_restricted_by_control",
            "answer the control first with break_binding, valid counter, incarnation_resistance for force_incarnation, negotiate, surrender, or fail/block the restricted action",
            operationType);
    }

    private static bool IsAllowedCounterTargetOperation(string? operationType) =>
        ConflictTokenEquals(
            operationType,
            "pressure",
            "binding",
            "force_binding",
            "force_incarnation",
            "break_binding",
            "incarnation_resistance");

    private static bool IncomingActionIsControlOperation(JsonObject incomingAction)
    {
        var operationType = AfterlifeSpiritualConflictState.GetNodeString(incomingAction["operationType"]);
        var finalOperationType = AfterlifeSpiritualConflictState.GetNodeString(incomingAction["finalOperationType"]);
        return ConflictTokenEquals(operationType, "binding", "force_binding", "force_incarnation") ||
               ConflictTokenEquals(finalOperationType, "binding", "force_binding", "force_incarnation");
    }

    private static bool IncomingActionHasOperation(JsonObject exchange, params string[] operationTypes)
    {
        var incomingOperations = ResolveIncomingActionOperations(exchange);
        return incomingOperations.Any(incomingOperation =>
            operationTypes.Any(operationType => ConflictTokenEquals(incomingOperation, operationType)));
    }

    private static bool IsMatrixCounterForSuccessfulOperation(string operationType, string oppositionOperation)
    {
        var operation = NormalizeConflictToken(operationType);
        var opposition = NormalizeConflictToken(oppositionOperation);
        return operation switch
        {
            "pressure" => ConflictTokenEquals(opposition, "guard", "counter"),
            "guard" => ConflictTokenEquals(opposition, "maneuver", "binding", "force_binding"),
            "maneuver" => ConflictTokenEquals(opposition, "pressure", "maneuver", "binding", "force_binding"),
            "binding" or "force_binding" => ConflictTokenEquals(opposition, "break_binding"),
            "recover_spiritual_power" => ConflictTokenEquals(opposition, "pressure", "maneuver", "binding", "force_binding", "force_incarnation"),
            _ => false
        };
    }

    private static string ExpectedRiskProfileForOperation(string operationType) =>
        NormalizeConflictToken(operationType) switch
        {
            "pressure" => "offensive_pressure",
            "guard" => "safe_defense",
            "counter" => "risky_reversal",
            "maneuver" => "position_play",
            "binding" or "force_binding" => "control_leverage",
            "break_binding" or "incarnation_resistance" => "anti_control",
            "champion_coordination" => "champion_support",
            "recover_spiritual_power" => "recovery_timing",
            _ => "terminal_choice"
        };

    private static string? ResolveIncomingOperation(JsonObject exchange)
    {
        var hasIncomingAction = exchange["incomingAction"] is JsonObject;
        var incomingOperations = ResolveIncomingActionOperations(exchange);
        if (incomingOperations.Count > 0 &&
            exchange["matchupAudit"] is JsonObject matchupAudit)
        {
            var oppositionOperation = AfterlifeSpiritualConflictState.GetNodeString(matchupAudit["oppositionOperation"]);
            if (!string.IsNullOrWhiteSpace(oppositionOperation) &&
                incomingOperations.Any(incomingOperation => ConflictTokenEquals(oppositionOperation, incomingOperation)))
            {
                return oppositionOperation;
            }
        }

        if (incomingOperations.Count > 0)
            return incomingOperations[0];

        if (hasIncomingAction)
            return null;

        if (exchange["matchupAudit"] is JsonObject fallbackMatchupAudit)
            return AfterlifeSpiritualConflictState.GetNodeString(fallbackMatchupAudit["oppositionOperation"]);

        return null;
    }

    private static IReadOnlyList<string> ResolveIncomingActionOperations(JsonObject exchange)
    {
        if (exchange["incomingAction"] is not JsonObject incomingAction)
            return Array.Empty<string>();

        var operations = new List<string>(capacity: 2);
        AddIncomingActionOperation(operations, incomingAction["operationType"]);
        AddIncomingActionOperation(operations, incomingAction["finalOperationType"]);
        return operations;
    }

    private static void AddIncomingActionOperation(List<string> operations, JsonNode? node)
    {
        var operation = AfterlifeSpiritualConflictState.GetNodeString(node);
        if (string.IsNullOrWhiteSpace(operation) ||
            operations.Any(existing => ConflictTokenEquals(existing, operation)))
        {
            return;
        }

        operations.Add(operation);
    }

    private static IReadOnlyList<string> ResolveIncomingActionOperationsForMatchup(JsonObject exchange)
    {
        var finalOperation = ResolveIncomingActionFinalOperation(exchange);
        if (!string.IsNullOrWhiteSpace(finalOperation))
            return new[] { finalOperation };

        return ResolveIncomingActionOperations(exchange);
    }

    private static bool HasCounterFailureDownside(JsonObject exchange, JsonObject before, JsonObject after)
    {
        if (exchange["counterBackfire"] is JsonObject backfire &&
            HasMeaningfulCounterPayoff(backfire))
        {
            return true;
        }

        if (TryGetStrainRank(before["playerSideStrain"], out var beforePlayerStrain) &&
            TryGetStrainRank(after["playerSideStrain"], out var afterPlayerStrain) &&
            afterPlayerStrain > beforePlayerStrain)
        {
            return true;
        }

        if (TryGetPositionRank(before["conflictPosition"], out var beforePosition) &&
            TryGetPositionRank(after["conflictPosition"], out var afterPosition) &&
            afterPosition < beforePosition)
        {
            return true;
        }

        return false;
    }

    private static bool AddsBindingOrControlState(JsonObject exchange, JsonObject before, JsonObject after)
    {
        foreach (var field in new[] { "controlState", "bindingState", "bindingId", "activeBinding", "forcedHandoff", "forceIncarnation", "forcedIncarnation" })
        {
            if (string.Equals(field, "controlState", StringComparison.OrdinalIgnoreCase))
            {
                if (ControlStateChangedSemantically(before[field], after[field]))
                    return true;

                continue;
            }

            if ((before.ContainsKey(field) || after.ContainsKey(field)) &&
                !JsonNode.DeepEquals(before[field], after[field]))
            {
                return true;
            }
        }

        return exchange.ContainsKey("controlState") ||
               exchange.ContainsKey("bindingState") ||
               exchange.ContainsKey("bindingId") ||
               exchange.ContainsKey("activeBinding") ||
               exchange.ContainsKey("forcedHandoff") ||
               exchange.ContainsKey("forceIncarnation") ||
               exchange.ContainsKey("forcedIncarnation");
    }

    private static bool ExchangeSnapshotsChangedSemantically(JsonObject before, JsonObject after)
    {
        if (JsonNode.DeepEquals(before, after))
            return false;

        var normalizedBefore = CloneSnapshotForSemanticDeltaComparison(before);
        var normalizedAfter = CloneSnapshotForSemanticDeltaComparison(after);
        return !JsonNode.DeepEquals(normalizedBefore, normalizedAfter);
    }

    private static JsonObject CloneSnapshotForSemanticDeltaComparison(JsonObject snapshot)
    {
        var clone = snapshot.DeepClone().AsObject();
        if (IsNoActiveControlSnapshot(clone["controlState"]))
            clone.Remove("controlState");

        return clone;
    }

    private static bool ControlStateChangedSemantically(JsonNode? before, JsonNode? after)
    {
        if (IsNoActiveControlSnapshot(before) && IsNoActiveControlSnapshot(after))
            return false;

        return !JsonNode.DeepEquals(before, after);
    }

    private static bool ControlAuditSnapshotMatchesPrior(JsonNode? priorControlState, JsonNode? beforeControlState)
    {
        if (IsNoActiveControlSnapshot(priorControlState) && IsNoActiveControlSnapshot(beforeControlState))
            return true;

        if (priorControlState is not JsonObject priorControl ||
            beforeControlState is not JsonObject beforeControl)
        {
            return false;
        }

        return JsonNode.DeepEquals(
            NormalizeControlAuditSnapshotForComparison(priorControl),
            NormalizeControlAuditSnapshotForComparison(beforeControl));
    }

    private static JsonNode? NormalizeControlAuditSnapshotForComparison(JsonNode? controlState)
    {
        return IsNoActiveControlSnapshot(controlState)
            ? null
            : controlState?.DeepClone();
    }

    private static string DescribeControlNode(JsonNode? node)
    {
        if (IsNoActiveControlSnapshot(node))
            return "missing/none";

        if (node is not JsonObject control)
            return node?.GetType().Name ?? "missing";

        var side = AfterlifeSpiritualConflictState.GetNodeString(control["controllerSide"]);
        var level = AfterlifeSpiritualConflictState.GetNodeString(control["level"]);
        var controlId = AfterlifeSpiritualConflictState.GetNodeString(control["controlId"]);
        var sourceOperation = AfterlifeSpiritualConflictState.GetNodeString(control["sourceOperation"]);
        return $"{side}:{level}:{controlId}:{sourceOperation}";
    }

    private static bool IsNoActiveControlSnapshot(JsonNode? node)
    {
        if (node == null)
            return true;

        if (node is not JsonObject control)
            return false;

        var level = AfterlifeSpiritualConflictState.GetNodeString(control["level"]);
        return string.Equals(level, "none", StringComparison.OrdinalIgnoreCase);
    }

    private static void ValidateControlStateShape(JsonNode? node, string context, List<ValidationIssue> issues, bool required)
    {
        if (node == null)
        {
            if (required)
            {
                AddControlStateIssue(
                    issues,
                    context,
                    "controlState обязателен для этого обмена духовного боя.",
                    "afterlife_conflict_control_state_missing",
                    "controlState object",
                    "missing");
            }

            return;
        }

        if (node is not JsonObject control)
        {
            AddControlStateIssue(
                issues,
                context,
                "controlState должен быть object или null.",
                "afterlife_conflict_invalid_control_state",
                "object/null",
                node.GetType().Name);
            return;
        }

        var level = AfterlifeSpiritualConflictState.GetNodeString(control["level"]);
        if (string.IsNullOrWhiteSpace(level))
        {
            AddControlStateIssue(
                issues,
                $"{context}.level",
                "controlState.level должен явно указывать уровень контроля.",
                "afterlife_conflict_control_state_missing_level",
                "none/hindered/bound/locked",
                "missing");
            return;
        }

        if (!AfterlifeSpiritualConflictState.ControlLevels.Contains(level))
        {
            AddControlStateIssue(
                issues,
                $"{context}.level",
                "controlState.level содержит неизвестный уровень контроля.",
                "afterlife_conflict_invalid_control_level",
                string.Join("/", AfterlifeSpiritualConflictState.ControlLevels.OrderBy(value => value, StringComparer.OrdinalIgnoreCase)),
                level);
            return;
        }

        if (!TryGetControlLevelRank(level, out var rank) || rank == 0)
            return;

        var controllerSide = AfterlifeSpiritualConflictState.GetNodeString(control["controllerSide"]);
        if (string.IsNullOrWhiteSpace(controllerSide) ||
            !AfterlifeSpiritualConflictState.ControlSides.Contains(controllerSide))
        {
            AddControlStateIssue(
                issues,
                $"{context}.controllerSide",
                "Активный контроль должен указывать controllerSide: player или opposition.",
                "afterlife_conflict_control_state_missing_controller",
                "player/opposition",
                string.IsNullOrWhiteSpace(controllerSide) ? "missing" : controllerSide);
        }

        var controlId = AfterlifeSpiritualConflictState.GetNodeString(control["controlId"]);
        if (string.IsNullOrWhiteSpace(controlId))
        {
            AddControlStateIssue(
                issues,
                $"{context}.controlId",
                "Активный контроль должен иметь controlId для аудита и последующего break_binding/counter.",
                "afterlife_conflict_control_state_missing_id",
                "non-empty controlId",
                "missing");
        }

        var sourceOperation = AfterlifeSpiritualConflictState.GetNodeString(control["sourceOperation"]);
        if (string.IsNullOrWhiteSpace(sourceOperation) ||
            !AfterlifeControlSourceOperations.Contains(sourceOperation))
        {
            AddControlStateIssue(
                issues,
                $"{context}.sourceOperation",
                "Активный контроль должен указывать sourceOperation из списка операций, которые действительно создают, меняют или восстанавливают контроль.",
                string.IsNullOrWhiteSpace(sourceOperation)
                    ? "afterlife_conflict_control_state_missing_source_operation"
                    : "afterlife_conflict_control_state_invalid_source_operation",
                string.Join("/", AfterlifeControlSourceOperations.OrderBy(value => value, StringComparer.OrdinalIgnoreCase)),
                string.IsNullOrWhiteSpace(sourceOperation) ? "missing" : sourceOperation);
        }

        if (control["restrictedOperations"] is not JsonArray restrictedOperations ||
            restrictedOperations.Count == 0 ||
            !restrictedOperations.Any(item => !string.IsNullOrWhiteSpace(AfterlifeSpiritualConflictState.GetNodeString(item))))
        {
            AddControlStateIssue(
                issues,
                $"{context}.restrictedOperations",
                "Активный контроль должен перечислять restrictedOperations, иначе его эффект не имеет механического смысла.",
                "afterlife_conflict_control_state_missing_restrictions",
                "non-empty array of operation ids",
                control["restrictedOperations"]?.GetType().Name ?? "missing");
        }
        else
        {
            for (var i = 0; i < restrictedOperations.Count; i++)
            {
                var restrictedOperation = AfterlifeSpiritualConflictState.GetNodeString(restrictedOperations[i]);
                if (string.IsNullOrWhiteSpace(restrictedOperation) ||
                    !AfterlifeSpiritualConflictState.OperationTypes.Contains(restrictedOperation))
                {
                    AddControlStateIssue(
                        issues,
                        $"{context}.restrictedOperations[{i}]",
                        "restrictedOperations должен содержать только поддерживаемые operation ids.",
                        "afterlife_conflict_control_state_invalid_restricted_operation",
                        string.Join("/", AfterlifeSpiritualConflictState.OperationTypes.OrderBy(value => value, StringComparer.OrdinalIgnoreCase)),
                        string.IsNullOrWhiteSpace(restrictedOperation) ? "missing" : restrictedOperation);
                }
            }
        }

        var summary = AfterlifeSpiritualConflictState.GetNodeString(control["summary"]);
        if (string.IsNullOrWhiteSpace(summary))
        {
            AddControlStateIssue(
                issues,
                $"{context}.summary",
                "Активный контроль должен иметь краткое summary для игрока и ГМ-а.",
                "afterlife_conflict_control_state_missing_summary",
                "non-empty summary",
                "missing");
        }
    }

    private static void AddControlStateIssue(
        List<ValidationIssue> issues,
        string path,
        string message,
        string code,
        string expected,
        string actual)
    {
        issues.Add(new ValidationIssue(
            path,
            IssueSeverity.Error,
            message,
            code: code,
            section: "AfterlifeSpiritualConflict",
            expected: expected,
            actual: actual));
    }

    private static bool TryGetPlayerControlProgression(
        JsonObject before,
        JsonObject after,
        out int beforePlayerRank,
        out int afterPlayerRank)
    {
        beforePlayerRank = TryGetControlSnapshot(before, out var beforeControl) &&
                           string.Equals(beforeControl.ControllerSide, "player", StringComparison.OrdinalIgnoreCase)
            ? beforeControl.Rank
            : 0;

        if (TryGetControlSnapshot(after, out var afterControl) &&
            string.Equals(afterControl.ControllerSide, "player", StringComparison.OrdinalIgnoreCase))
        {
            afterPlayerRank = afterControl.Rank;
            return afterPlayerRank > beforePlayerRank;
        }

        afterPlayerRank = 0;
        return false;
    }

    private static bool HasPlayerControlDelta(JsonObject before, JsonObject after)
    {
        var beforeHasPlayerControl = TryGetControlSnapshot(before, out var beforeControl) &&
                                     beforeControl.Rank > 0 &&
                                     string.Equals(beforeControl.ControllerSide, "player", StringComparison.OrdinalIgnoreCase);
        var afterHasPlayerControl = TryGetControlSnapshot(after, out var afterControl) &&
                                    afterControl.Rank > 0 &&
                                    string.Equals(afterControl.ControllerSide, "player", StringComparison.OrdinalIgnoreCase);
        return (beforeHasPlayerControl || afterHasPlayerControl) &&
               ControlStateChangedSemantically(before["controlState"], after["controlState"]);
    }

    private static bool HasControlCounterPayoff(JsonObject before, JsonObject after) =>
        HasAntiControlDelta(before, after);

    private static bool CounterAdvancesPlayerControl(JsonObject before, JsonObject after)
    {
        if (!TryGetControlSnapshot(after, out var afterControl) ||
            afterControl.Rank <= 0 ||
            !string.Equals(afterControl.ControllerSide, "player", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (!TryGetControlSnapshot(before, out var beforeControl) ||
            beforeControl.Rank <= 0)
        {
            return true;
        }

        if (string.Equals(beforeControl.ControllerSide, "opposition", StringComparison.OrdinalIgnoreCase))
            return false;

        return string.Equals(beforeControl.ControllerSide, "player", StringComparison.OrdinalIgnoreCase) &&
               afterControl.Rank > beforeControl.Rank;
    }

    private static bool HasAntiControlDelta(JsonObject before, JsonObject after)
    {
        if (TryGetControlSnapshot(before, out var beforeControl) &&
            beforeControl.Rank > 0 &&
            string.Equals(beforeControl.ControllerSide, "opposition", StringComparison.OrdinalIgnoreCase))
        {
            var beforeControlNode = (JsonObject?)before["controlState"];
            if (!TryGetControlSnapshot(after, out var afterControl) || afterControl.Rank == 0)
                return true;

            if (string.Equals(afterControl.ControllerSide, "opposition", StringComparison.OrdinalIgnoreCase) &&
                afterControl.Rank < beforeControl.Rank)
            {
                return true;
            }

            if (string.Equals(afterControl.ControllerSide, "opposition", StringComparison.OrdinalIgnoreCase) &&
                afterControl.Rank == beforeControl.Rank &&
                beforeControlNode is not null &&
                after["controlState"] is JsonObject afterControlNode &&
                ControlRestrictionsStrictlyReduced(beforeControlNode, afterControlNode))
            {
                return true;
            }

            if (string.Equals(afterControl.ControllerSide, "player", StringComparison.OrdinalIgnoreCase) &&
                afterControl.Rank > 0)
            {
                return true;
            }

            return false;
        }

        foreach (var field in new[] { "bindingState", "bindingId", "activeBinding", "forcedHandoff", "forceIncarnation", "forcedIncarnation" })
        {
            if (before.ContainsKey(field) && !JsonNode.DeepEquals(before[field], after[field]))
                return true;
        }

        return false;
    }

    private static bool ControlRestrictionsStrictlyReduced(JsonObject beforeControl, JsonObject afterControl)
    {
        var beforeRestrictions = GetControlRestrictionSet(beforeControl);
        var afterRestrictions = GetControlRestrictionSet(afterControl);
        if (beforeRestrictions.Count == 0 ||
            afterRestrictions.Count == 0 ||
            afterRestrictions.Count >= beforeRestrictions.Count)
        {
            return false;
        }

        return afterRestrictions.All(beforeRestrictions.Contains);
    }

    private static HashSet<string> GetControlRestrictionSet(JsonObject control)
    {
        var restrictions = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (control["restrictedOperations"] is not JsonArray restrictedOperations)
            return restrictions;

        foreach (var item in restrictedOperations)
        {
            var operation = AfterlifeSpiritualConflictState.GetNodeString(item);
            if (!string.IsNullOrWhiteSpace(operation))
                restrictions.Add(operation.Trim());
        }

        return restrictions;
    }

    private static bool HasActiveOppositionControl(JsonObject root) =>
        TryGetControlSnapshot(root, out var control) &&
        control.Rank > 0 &&
        string.Equals(control.ControllerSide, "opposition", StringComparison.OrdinalIgnoreCase);

    private static bool HasActiveControlState(JsonObject root) =>
        TryGetControlSnapshot(root, out var control) && control.Rank > 0;

    private static bool HasActiveControlState(JsonNode? node)
    {
        if (node is not JsonObject control)
            return false;

        var level = AfterlifeSpiritualConflictState.GetNodeString(control["level"]);
        return TryGetControlLevelRank(level, out var rank) && rank > 0;
    }

    private static bool TryGetControlSnapshot(JsonObject root, out ControlSnapshot snapshot)
    {
        snapshot = default;
        if (root["controlState"] is not JsonObject control)
            return false;

        var level = AfterlifeSpiritualConflictState.GetNodeString(control["level"]);
        if (!TryGetControlLevelRank(level, out var rank))
            return false;

        var side = AfterlifeSpiritualConflictState.GetNodeString(control["controllerSide"]);
        snapshot = new ControlSnapshot(rank, side);
        return true;
    }

    private static bool TryGetControlLevelRank(string? level, out int rank)
    {
        rank = 0;
        if (string.IsNullOrWhiteSpace(level))
            return false;

        rank = level.Trim().ToLowerInvariant() switch
        {
            "none" => 0,
            "hindered" => 1,
            "bound" => 2,
            "locked" => 3,
            _ => 0
        };
        return AfterlifeSpiritualConflictState.ControlLevels.Contains(level);
    }

    private static string DescribeControlTransition(JsonObject before, JsonObject after) =>
        $"{DescribeControlState(before)} -> {DescribeControlState(after)}";

    private static string DescribeControlState(JsonObject root)
    {
        if (!TryGetControlSnapshot(root, out var control))
            return "missing/none";

        var side = string.IsNullOrWhiteSpace(control.ControllerSide) ? "none" : control.ControllerSide;
        return $"{side}:{control.Rank}";
    }

    private readonly record struct ControlSnapshot(int Rank, string? ControllerSide);

    private static bool TryGetPositionRank(JsonNode? node, out int rank)
    {
        rank = 0;
        var value = AfterlifeSpiritualConflictState.GetNodeString(node);
        if (string.IsNullOrWhiteSpace(value))
            return false;

        rank = value.Trim().ToLowerInvariant() switch
        {
            "opposition_dominant" => -2,
            "opposition_advantaged" => -1,
            "contested" => 0,
            "player_advantaged" => 1,
            "player_dominant" => 2,
            _ => 0
        };
        return AfterlifeSpiritualConflictState.ConflictPositions.Contains(value);
    }

    private static bool TryGetStrainRank(JsonNode? node, out int rank)
    {
        rank = 0;
        var value = AfterlifeSpiritualConflictState.GetNodeString(node);
        if (string.IsNullOrWhiteSpace(value))
            return false;

        rank = value.Trim().ToLowerInvariant() switch
        {
            "clear" => 0,
            "strained" => 1,
            "fractured" => 2,
            "overwhelmed" => 3,
            "broken" => 4,
            _ => 0
        };
        return AfterlifeSpiritualConflictState.StrainStates.Contains(value);
    }

    private static void ValidateConflictPositionDiceModifier(
        JsonObject diceAudit,
        JsonObject before,
        string context,
        List<ValidationIssue> issues)
    {
        var positionNode = before["conflictPosition"];
        if (!TryGetPositionRank(positionNode, out var positionRank))
        {
            AddDiceAuditIssue(
                issues,
                $"{context}.before.conflictPosition",
                "exchange.before.conflictPosition обязателен для diceAudit, чтобы стартовая позиция не обходила позиционный модификатор.",
                "afterlife_conflict_exchange_missing_before_position",
                "supported conflictPosition snapshot value",
                positionNode?.ToJsonString() ?? "missing");
            return;
        }

        var positionModifiers = CollectConflictPositionModifiers(diceAudit);
        if (positionRank == 0)
        {
            if (positionModifiers.Count > 0)
            {
                AddDiceAuditIssue(
                    issues,
                    $"{context}.diceAudit.modifierBreakdown",
                    "diceAudit не должен содержать conflict_position modifiers, когда before.conflictPosition=contested.",
                    "afterlife_conflict_dice_unexpected_position_modifier_for_contested",
                    "no conflict_position modifiers for contested starting position",
                    DescribeConflictPositionModifiers(positionModifiers));
            }

            return;
        }

        var position = AfterlifeSpiritualConflictState.GetNodeString(positionNode) ?? "missing";
        var expectedSide = positionRank > 0 ? "player" : "opposition";
        var expectedValue = Math.Abs(positionRank) * 2;
        var expectedModifiers = positionModifiers
            .Where(modifier =>
                string.Equals(modifier.Side, expectedSide, StringComparison.Ordinal) &&
                string.Equals(modifier.Position, position, StringComparison.OrdinalIgnoreCase))
            .ToList();
        var unexpectedSide = string.Equals(expectedSide, "player", StringComparison.Ordinal) ? "opposition" : "player";
        var unexpectedSideModifiers = positionModifiers
            .Where(modifier =>
                string.Equals(modifier.Side, unexpectedSide, StringComparison.Ordinal) &&
                string.Equals(modifier.Position, position, StringComparison.OrdinalIgnoreCase))
            .ToList();
        if (unexpectedSideModifiers.Count > 0)
        {
            AddDiceAuditIssue(
                issues,
                $"{context}.diceAudit.modifierBreakdown.{unexpectedSide}",
                "diceAudit не должен учитывать стартовую conflictPosition на противоположной стороне.",
                "afterlife_conflict_dice_unexpected_position_modifier_side",
                $"no conflict_position modifiers for {position} on {unexpectedSide}",
                DescribeConflictPositionModifiers(unexpectedSideModifiers));
        }

        var unexpectedModifiers = positionModifiers
            .Where(modifier =>
                !string.Equals(modifier.Side, expectedSide, StringComparison.Ordinal) ||
                !string.Equals(modifier.Position, position, StringComparison.OrdinalIgnoreCase))
            .ToList();
        if (unexpectedModifiers.Count > 0)
        {
            AddDiceAuditIssue(
                issues,
                $"{context}.diceAudit.modifierBreakdown",
                "diceAudit должен содержать только один conflict_position modifier, точно совпадающий с before.conflictPosition.",
                "afterlife_conflict_dice_unexpected_position_modifier",
                $"only one conflict_position modifier for {position} on {expectedSide}",
                DescribeConflictPositionModifiers(unexpectedModifiers));
        }

        if (expectedModifiers.Count == 0)
        {
            AddDiceAuditIssue(
                issues,
                $"{context}.diceAudit.modifierBreakdown.{expectedSide}",
                "diceAudit должен явно учитывать non-contested conflictPosition как позиционный модификатор.",
                "afterlife_conflict_dice_missing_position_modifier",
                $"modifierBreakdown.{expectedSide}[] item {{ modifierType: \"conflict_position\", position: \"{position}\", value: {expectedValue} }}",
                "missing");
            return;
        }

        var expectedTotal = expectedModifiers.Sum(modifier => modifier.Value);
        if (expectedModifiers.Count != 1 || expectedTotal != expectedValue)
        {
            AddDiceAuditIssue(
                issues,
                $"{context}.diceAudit.modifierBreakdown.{expectedSide}",
                "diceAudit должен учитывать стартовую conflictPosition ровно одним позиционным модификатором без задвоения.",
                "afterlife_conflict_dice_invalid_position_modifier_total",
                $"exactly one conflict_position modifier for {position} with value {expectedValue}",
                $"{expectedModifiers.Count} entries, total {expectedTotal}");
        }
    }

    private static List<(string Side, string? Position, int Value)> CollectConflictPositionModifiers(JsonObject diceAudit)
    {
        var result = new List<(string Side, string? Position, int Value)>();
        if (diceAudit["modifierBreakdown"] is not JsonObject modifierBreakdown)
            return result;

        CollectConflictPositionModifiers(modifierBreakdown, "player", result);
        CollectConflictPositionModifiers(modifierBreakdown, "opposition", result);
        return result;
    }

    private static void CollectConflictPositionModifiers(
        JsonObject modifierBreakdown,
        string side,
        List<(string Side, string? Position, int Value)> result)
    {
        if (modifierBreakdown[side] is not JsonArray modifiers)
            return;

        foreach (var item in modifiers.OfType<JsonObject>())
        {
            var modifierType = AfterlifeSpiritualConflictState.GetNodeString(item["modifierType"]);
            var source = AfterlifeSpiritualConflictState.GetNodeString(item["source"]);
            var modifierPosition = AfterlifeSpiritualConflictState.GetNodeString(item["position"]);
            var hasPositionIdentity =
                string.Equals(modifierType, "conflict_position", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(source, "conflictPosition", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(source, "conflict_position", StringComparison.OrdinalIgnoreCase);

            if (!hasPositionIdentity)
                continue;

            result.Add((side, modifierPosition, TryGetJsonNodeInt(item["value"], out var value) ? value : 0));
        }
    }

    private static string DescribeConflictPositionModifiers(IReadOnlyCollection<(string Side, string? Position, int Value)> modifiers)
    {
        if (modifiers.Count == 0)
            return "none";

        return $"{modifiers.Count} entries, total {modifiers.Sum(modifier => modifier.Value)}";
    }

    private static string DescribeConflictPosition(JsonObject root) =>
        AfterlifeSpiritualConflictState.GetNodeString(root["conflictPosition"]) ?? "missing";

    private static void AddSpiritualArtRuleIssue(
        List<ValidationIssue> issues,
        string path,
        string message,
        string code,
        string expected,
        string actual)
    {
        issues.Add(new ValidationIssue(
            path,
            IssueSeverity.Error,
            message,
            code: code,
            section: "AfterlifeSpiritualConflict",
            expected: expected,
            actual: actual));
    }

    private static bool ExchangeDiceAuditRequired(JsonObject exchange, string? outcome)
    {
        if (string.Equals(outcome, "no_effect", StringComparison.OrdinalIgnoreCase))
            return false;

        if (IsExplicitVoluntaryNonContest(exchange))
            return false;

        return !string.IsNullOrWhiteSpace(outcome);
    }

    private static bool ResolveDiceAuditRequired(JsonObject proof)
    {
        if (IsExplicitVoluntaryNonContest(proof))
            return false;

        return ConflictNodeStringEquals(proof, "force_incarnation", "operationType", "finalOperationType");
    }

    private static bool IsExplicitVoluntaryNonContest(JsonObject root)
    {
        var operationType = AfterlifeSpiritualConflictState.GetNodeString(root["operationType"]) ??
                            AfterlifeSpiritualConflictState.GetNodeString(root["finalOperationType"]);
        var isVoluntaryOperation =
            ConflictTokenEquals(operationType, "surrender", "withdraw", "negotiate") ||
            ConflictNodeContainsAnyToken(root, new[] { "playerOutcome", "resolutionKind", "outcome", "result" },
                "voluntary_surrender",
                "voluntary_concession",
                "voluntary_withdrawal",
                "voluntary_withdraw",
                "consented");

        if (!isVoluntaryOperation)
            return false;

        return TryGetJsonNodeBool(root["voluntary"], out var voluntary) && voluntary ||
               TryGetJsonNodeBool(root["isVoluntary"], out var isVoluntary) && isVoluntary ||
               ConflictNodeStringEquals(root, "voluntary_player_choice", "resolutionSource", "source");
    }

    private static bool ValidateAfterlifeConflictDiceAudit(
        JsonObject audit,
        string context,
        List<ValidationIssue>? issues,
        AfterlifeConflictDiceContext diceContext)
    {
        var valid = true;

        var formulaVersion = AfterlifeSpiritualConflictState.GetNodeString(audit["formulaVersion"]);
        if (!string.Equals(formulaVersion, "afterlife_spiritual_conflict_v1", StringComparison.Ordinal))
        {
            AddDiceAuditIssue(
                issues,
                $"{context}.formulaVersion",
                "diceAudit.formulaVersion должен быть afterlife_spiritual_conflict_v1.",
                "afterlife_conflict_dice_invalid_formula_version",
                "afterlife_spiritual_conflict_v1",
                string.IsNullOrWhiteSpace(formulaVersion) ? "missing" : formulaVersion);
            valid = false;
        }

        var diceSource = AfterlifeSpiritualConflictState.GetNodeString(audit["diceSource"]);
        if (!string.Equals(diceSource, "input/turn_request.json.preGeneratedDices1d20", StringComparison.Ordinal))
        {
            AddDiceAuditIssue(
                issues,
                $"{context}.diceSource",
                "diceAudit должен использовать visible turn_request preGeneratedDices1d20.",
                "afterlife_conflict_dice_invalid_source",
                "input/turn_request.json.preGeneratedDices1d20",
                string.IsNullOrWhiteSpace(diceSource) ? "missing" : diceSource);
            valid = false;
        }

        if (audit["diceUsed"] is not JsonArray diceUsed || diceUsed.Count < 2)
        {
            AddDiceAuditIssue(
                issues,
                $"{context}.diceUsed",
                "diceAudit.diceUsed должен содержать player и opposition d20 entries.",
                "afterlife_conflict_dice_missing_used",
                "array with player/opposition dice entries",
                audit["diceUsed"]?.GetType().Name ?? "missing");
            return false;
        }

        var playerRolls = new List<DiceRollEntry>();
        var oppositionRolls = new List<DiceRollEntry>();
        var usedSourceIndices = new HashSet<int>();
        for (var index = 0; index < diceUsed.Count; index++)
        {
            if (diceUsed[index] is not JsonObject dieEntry)
            {
                AddDiceAuditIssue(
                    issues,
                    $"{context}.diceUsed[{index}]",
                    "diceUsed[] item должен быть object.",
                    "afterlife_conflict_dice_invalid_used_entry",
                    "object",
                    diceUsed[index]?.GetType().Name ?? "null");
                valid = false;
                continue;
            }

            var side = AfterlifeSpiritualConflictState.GetNodeString(dieEntry["side"]);
            var sourceIndex = -1;
            if (!TryGetJsonNodeInt(dieEntry["sourceIndex"], out sourceIndex) || sourceIndex < 0)
            {
                AddDiceAuditIssue(
                    issues,
                    $"{context}.diceUsed[{index}].sourceIndex",
                    "diceUsed.sourceIndex должен быть non-negative integer.",
                    "afterlife_conflict_dice_invalid_source_index",
                    "0..19",
                    dieEntry["sourceIndex"]?.ToJsonString() ?? "missing");
                valid = false;
            }
            else if (!usedSourceIndices.Add(sourceIndex))
            {
                AddDiceAuditIssue(
                    issues,
                    $"{context}.diceUsed[{index}].sourceIndex",
                    "diceUsed.sourceIndex не должен повторяться внутри одного diceAudit.",
                    "afterlife_conflict_dice_duplicate_source_index",
                    "unique sourceIndex values",
                    sourceIndex.ToString());
                valid = false;
            }

            var valueIsValid = true;
            if (!TryGetJsonNodeInt(dieEntry["sides"], out var sides) || sides != 20)
            {
                AddDiceAuditIssue(
                    issues,
                    $"{context}.diceUsed[{index}].sides",
                    "afterlife conflict diceAudit поддерживает только d20.",
                    "afterlife_conflict_dice_invalid_sides",
                    "20",
                    dieEntry["sides"]?.ToJsonString() ?? "missing");
                valid = false;
            }

            if (!TryGetJsonNodeInt(dieEntry["value"], out var value) || value < 1 || value > 20)
            {
                AddDiceAuditIssue(
                    issues,
                    $"{context}.diceUsed[{index}].value",
                    "diceUsed.value должен быть d20 value в диапазоне 1..20.",
                    "afterlife_conflict_dice_invalid_value",
                    "1..20",
                    dieEntry["value"]?.ToJsonString() ?? "missing");
                valid = false;
                valueIsValid = false;
            }

            if (diceContext.AuthoritativeDice is { Length: > 0 } authoritativeDice &&
                sourceIndex >= 0)
            {
                if (sourceIndex >= authoritativeDice.Length)
                {
                    AddDiceAuditIssue(
                        issues,
                        $"{context}.diceUsed[{index}].sourceIndex",
                        "diceUsed.sourceIndex отсутствует в authoritative preGeneratedDices1d20.",
                        "afterlife_conflict_dice_source_index_not_authorized",
                        $"0..{authoritativeDice.Length - 1}",
                        sourceIndex.ToString());
                    valid = false;
                }
                else if (authoritativeDice[sourceIndex] != value)
                {
                    AddDiceAuditIssue(
                        issues,
                        $"{context}.diceUsed[{index}].value",
                        "diceUsed.value должен совпадать с authoritative preGeneratedDices1d20[sourceIndex].",
                        "afterlife_conflict_dice_value_not_authorized",
                        authoritativeDice[sourceIndex].ToString(),
                        value.ToString());
                    valid = false;
                }
            }

            if (ConflictTokenEquals(side, "player", "playerSide", "soul"))
            {
                if (valueIsValid)
                    playerRolls.Add(new DiceRollEntry(index, sourceIndex, value, AfterlifeSpiritualConflictState.GetNodeString(dieEntry["selection"])));
            }
            else if (ConflictTokenEquals(side, "opposition", "oppositionSide", "guardian"))
            {
                if (valueIsValid)
                    oppositionRolls.Add(new DiceRollEntry(index, sourceIndex, value, AfterlifeSpiritualConflictState.GetNodeString(dieEntry["selection"])));
            }
            else
            {
                AddDiceAuditIssue(
                    issues,
                    $"{context}.diceUsed[{index}].side",
                    "diceUsed.side должен быть player или opposition.",
                    "afterlife_conflict_dice_invalid_side",
                    "player/opposition",
                    string.IsNullOrWhiteSpace(side) ? "missing" : side);
                valid = false;
            }
        }

        if (playerRolls.Count == 0)
        {
            AddDiceAuditIssue(
                issues,
                $"{context}.diceUsed",
                "diceAudit должен включать player-side die.",
                "afterlife_conflict_dice_missing_player_die",
                "diceUsed side=player",
                "missing");
            valid = false;
        }

        if (oppositionRolls.Count == 0)
        {
            AddDiceAuditIssue(
                issues,
                $"{context}.diceUsed",
                "diceAudit должен включать opposition-side die.",
                "afterlife_conflict_dice_missing_opposition_die",
                "diceUsed side=opposition",
                "missing");
            valid = false;
        }

        var playerDie = ValidateDiceRollSelection(audit, "player", playerRolls, context, issues, ref valid);
        var oppositionDie = ValidateDiceRollSelection(audit, "opposition", oppositionRolls, context, issues, ref valid);

        var playerModifier = SumDiceAuditModifiers(audit, "player", $"{context}.modifierBreakdown.player", issues, ref valid);
        var oppositionModifier = SumDiceAuditModifiers(audit, "opposition", $"{context}.modifierBreakdown.opposition", issues, ref valid);
        ValidateAfterlifeConflictDifficultyDiceAudit(audit, context, diceContext, issues, ref valid);

        if (!TryGetJsonNodeInt(audit["playerTotal"], out var playerTotal))
        {
            AddDiceAuditIssue(issues, $"{context}.playerTotal", "diceAudit.playerTotal должен быть integer.", "afterlife_conflict_dice_missing_player_total", "integer", audit["playerTotal"]?.ToJsonString() ?? "missing");
            valid = false;
        }
        else if (playerDie != null && playerTotal != playerDie.Value + playerModifier)
        {
            AddDiceAuditIssue(issues, $"{context}.playerTotal", "diceAudit.playerTotal должен равняться player die + modifierBreakdown.player.", "afterlife_conflict_dice_player_total_mismatch", (playerDie.Value + playerModifier).ToString(), playerTotal.ToString());
            valid = false;
        }

        if (!TryGetJsonNodeInt(audit["oppositionTotal"], out var oppositionTotal))
        {
            AddDiceAuditIssue(issues, $"{context}.oppositionTotal", "diceAudit.oppositionTotal должен быть integer.", "afterlife_conflict_dice_missing_opposition_total", "integer", audit["oppositionTotal"]?.ToJsonString() ?? "missing");
            valid = false;
        }
        else if (oppositionDie != null && oppositionTotal != oppositionDie.Value + oppositionModifier)
        {
            AddDiceAuditIssue(issues, $"{context}.oppositionTotal", "diceAudit.oppositionTotal должен равняться opposition die + modifierBreakdown.opposition.", "afterlife_conflict_dice_opposition_total_mismatch", (oppositionDie.Value + oppositionModifier).ToString(), oppositionTotal.ToString());
            valid = false;
        }

        if (!TryGetJsonNodeInt(audit["margin"], out var margin))
        {
            AddDiceAuditIssue(issues, $"{context}.margin", "diceAudit.margin должен быть integer.", "afterlife_conflict_dice_missing_margin", "playerTotal - oppositionTotal", audit["margin"]?.ToJsonString() ?? "missing");
            valid = false;
        }
        else if (TryGetJsonNodeInt(audit["playerTotal"], out playerTotal) &&
                 TryGetJsonNodeInt(audit["oppositionTotal"], out oppositionTotal) &&
                 margin != playerTotal - oppositionTotal)
        {
            AddDiceAuditIssue(issues, $"{context}.margin", "diceAudit.margin должен равняться playerTotal - oppositionTotal.", "afterlife_conflict_dice_margin_mismatch", (playerTotal - oppositionTotal).ToString(), margin.ToString());
            valid = false;
        }

        var outcomeBand = AfterlifeSpiritualConflictState.GetNodeString(audit["outcomeBand"]);
        if (TryGetJsonNodeInt(audit["margin"], out margin))
        {
            var marginBand = ExpectedAfterlifeConflictOutcomeBand(margin);
            var expectedBand = ExpectedAfterlifeConflictOutcomeBand(margin, playerDie, oppositionDie);
            if (!string.Equals(outcomeBand, expectedBand, StringComparison.Ordinal))
            {
                AddDiceAuditIssue(issues, $"{context}.outcomeBand", "diceAudit.outcomeBand должен соответствовать margin и natural critical rules.", "afterlife_conflict_dice_outcome_band_mismatch", expectedBand, string.IsNullOrWhiteSpace(outcomeBand) ? "missing" : outcomeBand);
                valid = false;
            }

            ValidateAfterlifeConflictCriticalResult(
                audit,
                context,
                playerDie,
                oppositionDie,
                marginBand,
                expectedBand,
                issues,
                ref valid);
        }

        return valid;
    }

    private static void ValidateAfterlifeConflictDifficultyDiceAudit(
        JsonObject audit,
        string context,
        AfterlifeConflictDiceContext diceContext,
        List<ValidationIssue>? issues,
        ref bool valid)
    {
        var playerDifficultyModifier = SumDifficultyModifiers(audit, "player");
        var oppositionDifficultyModifier = SumDifficultyModifiers(audit, "opposition");

        if (playerDifficultyModifier != 0)
        {
            AddDiceAuditIssue(
                issues,
                $"{context}.modifierBreakdown.player",
                "Сложность игры не должна давать dice modifier стороне игрока.",
                "afterlife_conflict_dice_difficulty_wrong_side",
                "no player-side game_difficulty modifier",
                playerDifficultyModifier.ToString());
            valid = false;
        }

        if (diceContext.Difficulty == null)
        {
            if (oppositionDifficultyModifier != 0 || audit.ContainsKey("difficultyAudit"))
            {
                AddDiceAuditIssue(
                    issues,
                    $"{context}.difficultyAudit",
                    "difficultyAudit допустим только при readable game_settings difficulty.",
                    "afterlife_conflict_dice_difficulty_without_settings",
                    "readable game_state/core/game_settings.json.difficulty",
                    audit["difficultyAudit"]?.ToJsonString() ?? oppositionDifficultyModifier.ToString());
                valid = false;
            }

            return;
        }

        if (audit["difficultyAudit"] is not JsonObject difficultyAudit)
        {
            AddDiceAuditIssue(
                issues,
                $"{context}.difficultyAudit",
                "diceAudit должен фиксировать difficultyAudit из game_settings для afterlife combat.",
                "afterlife_conflict_dice_difficulty_audit_missing",
                "difficultyAudit object",
                audit["difficultyAudit"]?.GetType().Name ?? "missing");
            valid = false;
            return;
        }

        ValidateDifficultyAuditCommonFields(
            difficultyAudit,
            $"{context}.difficultyAudit",
            diceContext.Difficulty,
            issues,
            ref valid,
            issuePrefix: "afterlife_conflict_dice");

        if (!TryGetJsonNodeInt(difficultyAudit["oppositionModifier"], out var auditOppositionModifier) ||
            auditOppositionModifier != diceContext.Difficulty.OppositionDiceModifier)
        {
            AddDiceAuditIssue(
                issues,
                $"{context}.difficultyAudit.oppositionModifier",
                "difficultyAudit.oppositionModifier должен совпадать с таблицей сложности.",
                "afterlife_conflict_dice_difficulty_opposition_modifier_mismatch",
                diceContext.Difficulty.OppositionDiceModifier.ToString(),
                difficultyAudit["oppositionModifier"]?.ToJsonString() ?? "missing");
            valid = false;
        }

        if (oppositionDifficultyModifier != diceContext.Difficulty.OppositionDiceModifier)
        {
            AddDiceAuditIssue(
                issues,
                $"{context}.modifierBreakdown.opposition",
                "modifierBreakdown.opposition должен содержать ровно canonical modifier сложности игры.",
                "afterlife_conflict_dice_difficulty_modifier_mismatch",
                diceContext.Difficulty.OppositionDiceModifier.ToString(),
                oppositionDifficultyModifier.ToString());
            valid = false;
        }
    }

    private static void ValidateDifficultyAuditCommonFields(
        JsonObject difficultyAudit,
        string context,
        AfterlifeDifficultyDefinition expectedDifficulty,
        List<ValidationIssue>? issues,
        ref bool valid,
        string issuePrefix)
    {
        var difficulty = AfterlifeSpiritualConflictState.GetNodeString(difficultyAudit["difficulty"]);
        if (!string.Equals(difficulty, expectedDifficulty.Difficulty, StringComparison.OrdinalIgnoreCase))
        {
            AddDiceAuditIssue(
                issues,
                $"{context}.difficulty",
                "difficultyAudit.difficulty должен совпадать с game_settings difficulty.",
                $"{issuePrefix}_difficulty_mismatch",
                expectedDifficulty.Difficulty,
                string.IsNullOrWhiteSpace(difficulty) ? "missing/empty" : difficulty);
            valid = false;
        }

        var source = AfterlifeSpiritualConflictState.GetNodeString(difficultyAudit["source"]);
        if (!string.Equals(source, $"{AfterlifeSpiritualConflictState.DifficultySettingsPath}.difficulty", StringComparison.Ordinal))
        {
            AddDiceAuditIssue(
                issues,
                $"{context}.source",
                "difficultyAudit.source должен ссылаться на authoritative game_settings difficulty.",
                $"{issuePrefix}_difficulty_source_mismatch",
                $"{AfterlifeSpiritualConflictState.DifficultySettingsPath}.difficulty",
                string.IsNullOrWhiteSpace(source) ? "missing/empty" : source);
            valid = false;
        }

        if (!TryGetJsonNodeInt(difficultyAudit["rewardMultiplierPercent"], out var rewardMultiplier) ||
            rewardMultiplier != expectedDifficulty.RewardMultiplierPercent)
        {
            AddDiceAuditIssue(
                issues,
                $"{context}.rewardMultiplierPercent",
                "difficultyAudit.rewardMultiplierPercent должен совпадать с таблицей сложности.",
                $"{issuePrefix}_difficulty_reward_multiplier_mismatch",
                expectedDifficulty.RewardMultiplierPercent.ToString(),
                difficultyAudit["rewardMultiplierPercent"]?.ToJsonString() ?? "missing");
            valid = false;
        }
    }

    private static int? ValidateDiceRollSelection(
        JsonObject audit,
        string side,
        IReadOnlyList<DiceRollEntry> rolls,
        string context,
        List<ValidationIssue>? issues,
        ref bool valid)
    {
        if (rolls.Count == 0)
            return null;

        var rollMode = ReadDiceRollMode(audit, side, context, issues, ref valid);
        var selectedRolls = rolls
            .Where(roll => string.Equals(roll.Selection, "selected", StringComparison.OrdinalIgnoreCase))
            .ToList();
        var hasExplicitSelection = rolls.Any(roll => !string.IsNullOrWhiteSpace(roll.Selection));

        if (rolls.Count == 1 && !hasExplicitSelection)
            selectedRolls.Add(rolls[0]);

        if (hasExplicitSelection)
        {
            foreach (var roll in rolls)
            {
                if (ConflictTokenEquals(roll.Selection, "selected", "discarded"))
                    continue;

                AddDiceAuditIssue(
                    issues,
                    $"{context}.diceUsed[{roll.DiceUsedIndex}].selection",
                    "diceUsed.selection должен быть selected или discarded, если audit использует Преимущество/Помеху.",
                    "afterlife_conflict_dice_invalid_selection",
                    "selected/discarded",
                    string.IsNullOrWhiteSpace(roll.Selection) ? "missing" : roll.Selection!);
                valid = false;
            }
        }

        if (selectedRolls.Count != 1)
        {
            AddDiceAuditIssue(
                issues,
                $"{context}.diceUsed",
                "diceAudit должен явно выбрать ровно один d20 для стороны.",
                "afterlife_conflict_dice_selected_die_count_mismatch",
                $"exactly one selected {side} die",
                selectedRolls.Count.ToString());
            valid = false;
            return rolls[^1].Value;
        }

        var selectedRoll = selectedRolls[0];
        var effectiveMode = rollMode.EffectiveMode;
        var expectedMode = rollMode.ExpectedMode;

        if (!string.Equals(effectiveMode, expectedMode, StringComparison.OrdinalIgnoreCase))
        {
            AddDiceAuditIssue(
                issues,
                $"{context}.rollMode.{side}.effectiveMode",
                "rollMode.effectiveMode должен учитывать гашение Преимущества и Помехи.",
                "afterlife_conflict_dice_effective_roll_mode_mismatch",
                expectedMode,
                string.IsNullOrWhiteSpace(effectiveMode) ? "missing" : effectiveMode!);
            valid = false;
        }

        if (string.Equals(expectedMode, "normal", StringComparison.OrdinalIgnoreCase))
        {
            if (rollMode.IsCancelled && rolls.Count > 1)
            {
                AddDiceAuditIssue(
                    issues,
                    $"{context}.diceUsed",
                    "Встречные Преимущество и Помеха гасятся: после гашения используется один обычный d20 без дополнительных кубов.",
                    "afterlife_conflict_dice_cancelled_roll_uses_extra_dice",
                    $"one {side} die after advantage/disadvantage cancellation",
                    rolls.Count.ToString());
                valid = false;
            }
            else if (!rollMode.IsCancelled && rolls.Count > 1)
            {
                AddDiceAuditIssue(
                    issues,
                    $"{context}.diceUsed",
                    "Обычный бросок без Преимущества/Помехи не должен использовать дополнительные d20.",
                    "afterlife_conflict_dice_normal_roll_uses_extra_dice",
                    $"one {side} die",
                    rolls.Count.ToString());
                valid = false;
            }

            return selectedRoll.Value;
        }

        if (rolls.Count < 2)
        {
            AddDiceAuditIssue(
                issues,
                $"{context}.diceUsed",
                "Преимущество/Помеха требуют минимум два d20 для стороны: выбранный и отброшенный.",
                "afterlife_conflict_dice_multi_roll_missing_extra_die",
                $"at least two {side} dice",
                rolls.Count.ToString());
            valid = false;
            return selectedRoll.Value;
        }

        var expectedValue = string.Equals(expectedMode, "advantage", StringComparison.OrdinalIgnoreCase)
            ? rolls.Max(roll => roll.Value)
            : rolls.Min(roll => roll.Value);
        if (selectedRoll.Value != expectedValue)
        {
            var code = string.Equals(expectedMode, "advantage", StringComparison.OrdinalIgnoreCase)
                ? "afterlife_conflict_dice_advantage_selected_die_mismatch"
                : "afterlife_conflict_dice_disadvantage_selected_die_mismatch";
            var label = string.Equals(expectedMode, "advantage", StringComparison.OrdinalIgnoreCase)
                ? "лучший"
                : "худший";
            AddDiceAuditIssue(
                issues,
                $"{context}.diceUsed[{selectedRoll.DiceUsedIndex}].selection",
                $"При {TranslateRollMode(expectedMode)} выбранным должен быть {label} d20.",
                code,
                expectedValue.ToString(),
                selectedRoll.Value.ToString());
            valid = false;
        }

        return selectedRoll.Value;
    }

    private static DiceRollModeAudit ReadDiceRollMode(
        JsonObject audit,
        string side,
        string context,
        List<ValidationIssue>? issues,
        ref bool valid)
    {
        if (audit["rollMode"] is not JsonObject rollModeRoot ||
            rollModeRoot[side] is not JsonObject sideMode)
        {
            return new DiceRollModeAudit("normal", "normal", false);
        }

        var advantageSources = ReadRollModeSources(sideMode, "advantageSources", $"{context}.rollMode.{side}.advantageSources", issues, ref valid);
        var disadvantageSources = ReadRollModeSources(sideMode, "disadvantageSources", $"{context}.rollMode.{side}.disadvantageSources", issues, ref valid);
        var expectedMode =
            advantageSources > 0 && disadvantageSources > 0 ? "normal" :
            advantageSources > 0 ? "advantage" :
            disadvantageSources > 0 ? "disadvantage" :
            "normal";
        var effectiveMode = AfterlifeSpiritualConflictState.GetNodeString(sideMode["effectiveMode"]) ?? "normal";
        if (!ConflictTokenEquals(effectiveMode, "normal", "advantage", "disadvantage"))
        {
            AddDiceAuditIssue(
                issues,
                $"{context}.rollMode.{side}.effectiveMode",
                "rollMode.effectiveMode должен быть normal, advantage или disadvantage.",
                "afterlife_conflict_dice_invalid_effective_roll_mode",
                "normal/advantage/disadvantage",
                string.IsNullOrWhiteSpace(effectiveMode) ? "missing" : effectiveMode);
            valid = false;
        }

        return new DiceRollModeAudit(effectiveMode, expectedMode, advantageSources > 0 && disadvantageSources > 0);
    }

    private static int ReadRollModeSources(
        JsonObject sideMode,
        string propertyName,
        string context,
        List<ValidationIssue>? issues,
        ref bool valid)
    {
        if (sideMode[propertyName] is not JsonArray sources)
        {
            AddDiceAuditIssue(
                issues,
                context,
                "rollMode должен явно перечислять источники Преимущества и Помехи.",
                "afterlife_conflict_dice_missing_roll_mode_sources",
                "array of non-empty source strings",
                sideMode[propertyName]?.GetType().Name ?? "missing");
            valid = false;
            return 0;
        }

        var count = 0;
        for (var index = 0; index < sources.Count; index++)
        {
            var source = AfterlifeSpiritualConflictState.GetNodeString(sources[index]);
            if (string.IsNullOrWhiteSpace(source))
            {
                AddDiceAuditIssue(
                    issues,
                    $"{context}[{index}]",
                    "Источник Преимущества/Помехи должен быть непустой строкой.",
                    "afterlife_conflict_dice_empty_roll_mode_source",
                    "non-empty source string",
                    sources[index]?.ToJsonString() ?? "null");
                valid = false;
                continue;
            }

            count++;
        }

        return count;
    }

    private static string TranslateRollMode(string mode) =>
        string.Equals(mode, "advantage", StringComparison.OrdinalIgnoreCase) ? "Преимуществе" :
        string.Equals(mode, "disadvantage", StringComparison.OrdinalIgnoreCase) ? "Помехе" :
        "обычном броске";

    private readonly record struct DiceRollEntry(int DiceUsedIndex, int SourceIndex, int Value, string? Selection);

    private readonly record struct DiceRollModeAudit(string? EffectiveMode, string ExpectedMode, bool IsCancelled);

    private static int SumDiceAuditModifiers(
        JsonObject audit,
        string side,
        string context,
        List<ValidationIssue>? issues,
        ref bool valid)
    {
        if (audit["modifierBreakdown"] is not JsonObject modifierBreakdown ||
            modifierBreakdown[side] is not JsonArray modifiers)
        {
            AddDiceAuditIssue(
                issues,
                context,
                "diceAudit.modifierBreakdown должен содержать player/opposition arrays.",
                "afterlife_conflict_dice_missing_modifier_breakdown",
                "modifierBreakdown.player[] and modifierBreakdown.opposition[]",
                audit["modifierBreakdown"]?.GetType().Name ?? "missing");
            valid = false;
            return 0;
        }

        var total = 0;
        for (var index = 0; index < modifiers.Count; index++)
        {
            if (modifiers[index] is not JsonObject modifier ||
                !TryGetJsonNodeInt(modifier["value"], out var value))
            {
                AddDiceAuditIssue(
                    issues,
                    $"{context}[{index}].value",
                    "modifierBreakdown item должен иметь integer value.",
                    "afterlife_conflict_dice_invalid_modifier",
                    "integer value",
                    modifiers[index]?.ToJsonString() ?? "missing");
                valid = false;
                continue;
            }

            total += value;
        }

        return total;
    }

    private static int SumDifficultyModifiers(JsonObject audit, string side)
    {
        if (audit["modifierBreakdown"] is not JsonObject modifierBreakdown ||
            modifierBreakdown[side] is not JsonArray modifiers)
        {
            return 0;
        }

        var total = 0;
        foreach (var item in modifiers.OfType<JsonObject>())
        {
            if (!TryGetJsonNodeInt(item["value"], out var value))
                continue;

            var modifierType = AfterlifeSpiritualConflictState.GetNodeString(item["modifierType"]);
            var source = AfterlifeSpiritualConflictState.GetNodeString(item["source"]);
            if (ConflictTokenEquals(modifierType, "game_difficulty", "difficulty") ||
                (!string.IsNullOrWhiteSpace(source) &&
                 source.Contains("difficulty", StringComparison.OrdinalIgnoreCase)))
            {
                total += value;
            }
        }

        return total;
    }

    private static string ExpectedAfterlifeConflictOutcomeBand(int margin) =>
        margin >= 8 ? "decisive_player_success" :
        margin >= 3 ? "player_success" :
        margin >= -2 ? "mixed_or_no_effect" :
        margin >= -7 ? "opposition_success" :
        "decisive_opposition_success";

    private static string ExpectedAfterlifeConflictOutcomeBand(int margin, int? playerDie, int? oppositionDie)
    {
        var marginBand = ExpectedAfterlifeConflictOutcomeBand(margin);
        if (playerDie == null || oppositionDie == null)
            return marginBand;

        var playerCriticalSuccess = (playerDie.Value == 20 ? 1 : 0) + (oppositionDie.Value == 1 ? 1 : 0);
        var playerCriticalFailure = (playerDie.Value == 1 ? 1 : 0) + (oppositionDie.Value == 20 ? 1 : 0);

        if (playerCriticalSuccess > playerCriticalFailure)
            return OutcomeBandRank(marginBand) < 1 ? "player_success" : marginBand;

        if (playerCriticalFailure > playerCriticalSuccess)
            return OutcomeBandRank(marginBand) > -1 ? "opposition_success" : marginBand;

        return marginBand;
    }

    private static int OutcomeBandRank(string? band) =>
        band?.Trim().ToLowerInvariant() switch
        {
            "decisive_player_success" => 2,
            "player_success" => 1,
            "mixed_or_no_effect" => 0,
            "opposition_success" => -1,
            "decisive_opposition_success" => -2,
            _ => 0
        };

    private static void ValidateAfterlifeConflictCriticalResult(
        JsonObject audit,
        string context,
        int? playerDie,
        int? oppositionDie,
        string marginBand,
        string expectedBand,
        List<ValidationIssue>? issues,
        ref bool valid)
    {
        if (playerDie == null || oppositionDie == null)
            return;

        var hasNaturalCritical =
            playerDie.Value is 1 or 20 ||
            oppositionDie.Value is 1 or 20;
        var criticalChangedOutcome = hasNaturalCritical &&
                                     !string.Equals(marginBand, expectedBand, StringComparison.Ordinal);
        if (!hasNaturalCritical)
        {
            if (audit.ContainsKey("criticalResult"))
            {
                AddDiceAuditIssue(
                    issues,
                    $"{context}.criticalResult",
                    "criticalResult допустим только если player/opposition d20 содержит natural 1 или 20.",
                    "afterlife_conflict_dice_critical_result_without_critical_roll",
                    "no criticalResult without natural 1/20",
                    audit["criticalResult"]?.ToJsonString() ?? "missing");
                valid = false;
            }

            return;
        }

        if (audit["criticalResult"] is not JsonObject criticalResult)
        {
            if (criticalChangedOutcome || audit.ContainsKey("criticalResult"))
            {
                AddDiceAuditIssue(
                    issues,
                    $"{context}.criticalResult",
                    criticalChangedOutcome
                        ? "Natural 1/20, изменивший outcomeBand относительно margin, требует criticalResult с нормализацией масштаба."
                        : "criticalResult должен быть object.",
                    criticalChangedOutcome
                        ? "afterlife_conflict_dice_missing_critical_result"
                        : "afterlife_conflict_dice_invalid_critical_result",
                    "criticalResult object with playerNaturalRoll, oppositionNaturalRoll, marginOutcomeBand, normalizedOutcomeBand, scaleLimit, narrativeConstraint",
                    audit["criticalResult"]?.GetType().Name ?? "missing");
                valid = false;
            }

            return;
        }

        ValidateCriticalResultInt(
            criticalResult,
            context,
            "playerNaturalRoll",
            playerDie.Value,
            issues,
            ref valid);
        ValidateCriticalResultInt(
            criticalResult,
            context,
            "oppositionNaturalRoll",
            oppositionDie.Value,
            issues,
            ref valid);
        ValidateCriticalResultString(
            criticalResult,
            context,
            "marginOutcomeBand",
            marginBand,
            "afterlife_conflict_dice_critical_margin_band_mismatch",
            issues,
            ref valid);
        ValidateCriticalResultString(
            criticalResult,
            context,
            "normalizedOutcomeBand",
            expectedBand,
            "afterlife_conflict_dice_critical_normalized_band_mismatch",
            issues,
            ref valid);

        foreach (var fieldName in new[] { "scaleLimit", "narrativeConstraint" })
        {
            if (string.IsNullOrWhiteSpace(AfterlifeSpiritualConflictState.GetNodeString(criticalResult[fieldName])))
            {
                AddDiceAuditIssue(
                    issues,
                    $"{context}.criticalResult.{fieldName}",
                    "criticalResult должен явно ограничивать художественный масштаб natural critical под текущую ситуацию и силы сторон.",
                    fieldName == "scaleLimit"
                        ? "afterlife_conflict_dice_critical_missing_scale_limit"
                        : "afterlife_conflict_dice_critical_missing_narrative_constraint",
                    "non-empty text",
                    criticalResult[fieldName]?.ToJsonString() ?? "missing");
                valid = false;
            }
        }
    }

    private static void ValidateCriticalResultInt(
        JsonObject criticalResult,
        string context,
        string fieldName,
        int expected,
        List<ValidationIssue>? issues,
        ref bool valid)
    {
        if (!TryGetJsonNodeInt(criticalResult[fieldName], out var actual) || actual != expected)
        {
            AddDiceAuditIssue(
                issues,
                $"{context}.criticalResult.{fieldName}",
                "criticalResult должен повторять фактические natural d20 rolls из diceUsed[].",
                "afterlife_conflict_dice_critical_roll_mismatch",
                expected.ToString(),
                criticalResult[fieldName]?.ToJsonString() ?? "missing");
            valid = false;
        }
    }

    private static void ValidateCriticalResultString(
        JsonObject criticalResult,
        string context,
        string fieldName,
        string expected,
        string code,
        List<ValidationIssue>? issues,
        ref bool valid)
    {
        var actual = AfterlifeSpiritualConflictState.GetNodeString(criticalResult[fieldName]);
        if (!string.Equals(actual, expected, StringComparison.Ordinal))
        {
            AddDiceAuditIssue(
                issues,
                $"{context}.criticalResult.{fieldName}",
                "criticalResult должен фиксировать margin outcome и normalized critical outcome.",
                code,
                expected,
                string.IsNullOrWhiteSpace(actual) ? "missing/empty" : actual);
            valid = false;
        }
    }

    private static void AddDiceAuditIssue(
        List<ValidationIssue>? issues,
        string path,
        string message,
        string code,
        string expected,
        string actual)
    {
        issues?.Add(new ValidationIssue(
            path,
            IssueSeverity.Error,
            message,
            code: code,
            section: "AfterlifeSpiritualConflict",
            expected: expected,
            actual: actual));
    }

    private static bool ConflictNodeStringEquals(JsonObject root, string expected, params string[] propertyNames)
    {
        foreach (var propertyName in propertyNames)
        {
            if (string.Equals(AfterlifeSpiritualConflictState.GetNodeString(root[propertyName]), expected, StringComparison.OrdinalIgnoreCase))
                return true;
        }

        return false;
    }

    private static bool ConflictNodeContainsAnyToken(JsonObject root, IEnumerable<string> propertyNames, params string[] acceptedTokens)
    {
        foreach (var propertyName in propertyNames)
        {
            var value = AfterlifeSpiritualConflictState.GetNodeString(root[propertyName]);
            if (ConflictTokenEquals(value, acceptedTokens))
                return true;
        }

        return false;
    }

    private static bool ConflictTokenEquals(string? value, params string[] acceptedTokens)
    {
        if (string.IsNullOrWhiteSpace(value))
            return false;

        return acceptedTokens.Any(token => string.Equals(value, token, StringComparison.OrdinalIgnoreCase));
    }

    private static string NormalizeConflictToken(string? value) =>
        string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim().ToLowerInvariant();

    private static bool TryGetJsonNodeInt(JsonNode? node, out int value)
    {
        value = 0;
        return node is JsonValue jsonValue && jsonValue.TryGetValue<int>(out value);
    }

    private static bool TryGetJsonNodeBool(JsonNode? node, out bool value)
    {
        value = false;
        return node is JsonValue jsonValue && jsonValue.TryGetValue<bool>(out value);
    }

    private string? ValidateEnumNode(
        JsonObject root,
        string context,
        List<ValidationIssue> issues,
        string propertyName,
        HashSet<string> allowedValues,
        string code)
    {
        var value = RequireNodeString(root, context, issues, propertyName);
        if (string.IsNullOrWhiteSpace(value))
            return value;

        if (!allowedValues.Contains(value))
        {
            issues.Add(new ValidationIssue(
                $"{context}.{propertyName}",
                IssueSeverity.Error,
                $"{propertyName} содержит unsupported afterlife spiritual conflict value.",
                code: code,
                section: "AfterlifeSpiritualConflict",
                expected: string.Join("/", allowedValues.OrderBy(item => item, StringComparer.OrdinalIgnoreCase)),
                actual: value));
        }

        return value;
    }

    private static string? RequireNodeString(JsonObject root, string context, List<ValidationIssue> issues, string propertyName)
    {
        var value = AfterlifeSpiritualConflictState.GetNodeString(root[propertyName]);
        if (!string.IsNullOrWhiteSpace(value))
            return value;

        issues.Add(new ValidationIssue(
            $"{context}.{propertyName}",
            IssueSeverity.Error,
            $"{propertyName} должен быть non-empty string.",
            code: "afterlife_conflict_missing_required_string",
            section: "AfterlifeSpiritualConflict",
            expected: "non-empty string",
            actual: root.ContainsKey(propertyName) ? root[propertyName]?.ToJsonString() ?? "null" : "missing"));
        return null;
    }

    private static bool ContainsProperty(JsonElement root, string propertyName) =>
        root.ValueKind == JsonValueKind.Object && root.TryGetProperty(propertyName, out _);

    private static bool TryGetObject(JsonElement root, string propertyName, out JsonElement value)
    {
        value = default;
        return root.ValueKind == JsonValueKind.Object &&
               root.TryGetProperty(propertyName, out value) &&
               value.ValueKind == JsonValueKind.Object;
    }

    private static string? TryGetString(JsonElement root, string propertyName)
    {
        return root.ValueKind == JsonValueKind.Object &&
               root.TryGetProperty(propertyName, out var value) &&
               value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;
    }
}
