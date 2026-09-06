using System.Text.Json;
using System.Text.Json.Nodes;

namespace BookOfEternityClient.Services;

internal static class EffectAcceptedTurnInputComposer
{
    internal const string WorldTimePath = "game_state/world/world_time.json";

    internal const string PlayerWoundsPath = "game_state/player/wounds.json";

    internal static readonly string[] SameTurnOwnerAuthorityPaths =
    {
        "game_state/player/skills_active.json",
        "game_state/player/skills_passive.json",
        "game_state/quests/regular_quests.json",
        "game_state/quests/soul_quests.json",
        "game_state/world/world_events.json",
        "game_state/factions/faction_core.json",
        "game_state/npcs/npc_core.json",
        EffectCarrierCatalog.EnemiesPath,
        EffectCarrierCatalog.AlliesPath
    };

    internal static readonly string[] SourceAuthorityPaths =
    {
        "game_state/player/skills_active.json",
        "game_state/player/skills_passive.json",
        "game_state/inventory/items.json",
        PlayerWoundsPath,
        WoundCarrierCatalog.NpcPath,
        "game_state/quests/regular_quests.json",
        "game_state/quests/soul_quests.json",
        MortalLocationMaterializationContract.WorldMapPath,
        MortalLocationMaterializationContract.CurrentLocationPath,
        "game_state/world/world_events.json",
        "game_state/factions/faction_core.json",
        "game_state/npcs/npc_core.json",
        EffectCarrierCatalog.EnemiesPath,
        EffectCarrierCatalog.AlliesPath,
        EffectCarrierCatalog.AfterlifeProfilesPath
    };

    private static readonly SourceDescriptor[] SkillSourceDescriptors =
    {
        new("skill", "skillId")
    };

    private static readonly SourceDescriptor[] ItemSourceDescriptors =
    {
        new("item", "itemId")
    };

    private static readonly SourceDescriptor[] QuestSourceDescriptors =
    {
        new("quest", "questId")
    };

    private static readonly SourceDescriptor[] LocationSourceDescriptors =
    {
        new("location", "locationId")
    };

    private static readonly SourceDescriptor[] HazardSourceDescriptors =
    {
        new("hazard", "hazardId")
    };

    private static readonly SourceDescriptor[] FactionSourceDescriptors =
    {
        new("faction", "factionId")
    };

    private static readonly SourceDescriptor[] WorldEventSourceDescriptors =
    {
        new("world_event", "eventId")
    };

    private static readonly SourceDescriptor[] FateCardSourceDescriptors =
    {
        new("fate_card", "cardId")
    };

    private static readonly SourceDescriptor[] CombatSourceDescriptors =
    {
        new("combat_action", "combatActionId"),
        new("combat_action", "actionId")
    };

    private static readonly SourceDescriptor[] SpiritualArtSourceDescriptors =
    {
        new("spiritual_art", "artId")
    };

    private static readonly SourceDescriptor[] AfterlifeFateCardSourceDescriptors =
    {
        new("fate_card", "cardId")
    };

    private static readonly HashSet<string> TerminalSourceStatuses =
        new(StringComparer.OrdinalIgnoreCase)
        {
            "Completed", "Failed", "Abandoned", "cancelled", "resolved",
            "removed", "inactive", "disabled", "locked", "healed", "closed",
            "consumed", "destroyed"
        };

    internal static EffectAcceptedTurnInput Compose(
        string sessionId,
        string snapshotToken,
        int turn,
        JsonObject rawCommands,
        EffectCarrierCatalogInput preTurnCarriers,
        EffectCarrierCatalogInput acceptedCarriers,
        JsonObject? preTurnIdentityIndex,
        IReadOnlyDictionary<string, JsonNode?> preTurnSourceRoots,
        IReadOnlyList<EffectSourceExport>? acceptedPlanSourceExports = null,
        IReadOnlyList<EffectTargetExport>? acceptedPlanTargetExports = null,
        IReadOnlySet<EffectSourceOwnerKey>? replacedSourceOwners = null,
        IReadOnlySet<EffectTargetKey>? replacedTargets = null,
        long? currentWorldTime = null,
        EffectCarrierCatalogInput? publicationCarrierBaselines = null,
        CombatantIdentityState? preallocatedCombatantIdentities = null,
        string realm = "mortal_world",
        IReadOnlySet<string>? grantedBuiltInApplicationAuthorities = null,
        IReadOnlyList<JsonObject>? acceptedReportedLifecycleEvents = null,
        WoundPreparedAcceptedTurnPlan? preparedWoundPlan = null,
        IReadOnlyDictionary<string, JsonNode?>? acceptedSourceRoots = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sessionId);
        ArgumentException.ThrowIfNullOrWhiteSpace(snapshotToken);
        ArgumentNullException.ThrowIfNull(rawCommands);
        ArgumentNullException.ThrowIfNull(preTurnCarriers);
        ArgumentNullException.ThrowIfNull(acceptedCarriers);
        ArgumentNullException.ThrowIfNull(preTurnSourceRoots);

        var preparedWounds = ComposePreparedWoundSources(
            sessionId,
            snapshotToken,
            turn,
            realm,
            preparedWoundPlan,
            acceptedPlanSourceExports);
        var replacedSources = (replacedSourceOwners ??
                new HashSet<EffectSourceOwnerKey>())
            .Concat(preparedWounds.Groups.Select(static group =>
                new EffectSourceOwnerKey(
                    group.Key.Realm,
                    group.Key.Kind,
                    group.Key.SourceId)))
            .ToHashSet();
        var persistedWounds = CollectCanonicalWoundSources(
            preTurnSourceRoots,
            sameTurn: false);
        var preTurnSourceExports = CollectSources(preTurnSourceRoots, sameTurn: false)
            .Concat(persistedWounds.Exports)
            .Concat(EffectBuiltInSourceCatalog.CreateCanonicalExports())
            .Where(export => !replacedSources.Contains(new EffectSourceOwnerKey(
                export.Realm,
                export.Kind,
                export.SourceId)))
            .ToArray();
        var planSourceExports = (acceptedPlanSourceExports ??
                Array.Empty<EffectSourceExport>())
            .Where(static export => export is not null && !string.Equals(
                export.Kind,
                "wound",
                StringComparison.Ordinal))
            .Where(static export => !string.Equals(
                export.Kind,
                "wound_legacy",
                StringComparison.Ordinal))
            .Concat(preparedWounds.Exports)
            .ToArray();
        var woundGroups = persistedWounds.Groups
            .Where(group => !replacedSources.Contains(new EffectSourceOwnerKey(
                group.Key.Realm,
                group.Key.Kind,
                group.Key.SourceId)))
            .Concat(preparedWounds.Groups)
            .ToArray();
        var sourceAuthority = EffectSourceAuthority.Build(new EffectSourceAuthorityInput(
            preTurnSourceExports,
            planSourceExports,
            new HashSet<string>(StringComparer.Ordinal),
            grantedBuiltInApplicationAuthorities,
            woundGroups,
            persistedWounds.Issues.Concat(preparedWounds.Issues).ToArray()));
        var acceptedSpiritualTargets = new List<EffectTargetExport>();
        CollectSpiritualConflictTargets(
            acceptedCarriers.SpiritualConflict,
            acceptedSpiritualTargets);
        var preTurnTargets = CollectTargets(
            preTurnCarriers,
            preTurnSourceRoots,
            includeTemporaryRefs: false)
            .Where(static target => !string.Equals(
                target.Kind,
                "spiritual_conflict_side",
                StringComparison.Ordinal))
            .Concat(acceptedSpiritualTargets.Select(static target =>
                target with { SameTurn = false }))
            .Where(target => replacedTargets == null ||
                !replacedTargets.Contains(new EffectTargetKey(
                    target.Realm,
                    target.Kind,
                    target.TargetId)))
            .ToArray();
        var preTurnTargetKeys = preTurnTargets
            .Select(static target => (target.Realm, target.Kind, target.TargetId))
            .ToHashSet();
        var suppliedAcceptedTargets = acceptedPlanTargetExports ??
            Array.Empty<EffectTargetExport>();
        var suppliedAcceptedTargetKeys = suppliedAcceptedTargets
            .Select(static target => new EffectTargetKey(
                target.Realm,
                target.Kind,
                target.TargetId))
            .ToHashSet();
        var acceptedTargets = suppliedAcceptedTargets
            .Concat(preparedWounds.Groups
                .Select(static group => group.Target)
                .Distinct()
                .Where(target => !preTurnTargetKeys.Contains(
                                     (target.Realm, target.Kind, target.TargetId)) &&
                                 !suppliedAcceptedTargetKeys.Contains(target))
                .Select(static target => new EffectTargetExport(
                    target.Realm,
                    target.Kind,
                    target.TargetId,
                    SameTurn: false)))
            .ToArray();
        var acceptedStableTargets = acceptedTargets
            .Where(static target => target.TargetRef == null)
            .Select(static target => target with { SameTurn = false })
            .ToArray();
        var sameTurnTargets = acceptedTargets
            .Where(static target => target.TargetRef != null)
            .Where(target => !preTurnTargetKeys.Contains(
                (target.Realm, target.Kind, target.TargetId)))
            .Select(static target => target with { SameTurn = true })
            .ToArray();
        var targetAuthorityInput = new EffectTargetAuthorityInput(
            preTurnTargets.Concat(acceptedStableTargets).ToArray(),
            sameTurnTargets,
            new HashSet<string>(StringComparer.Ordinal),
            preallocatedCombatantIdentities);
        var targetAuthority = EffectTargetAuthority.Build(targetAuthorityInput);

        var lifecycleCarriers = PreserveClosingSpiritualConflictCarrier(
            preTurnCarriers,
            acceptedCarriers);
        var eventInput = BuildAcceptedEventInput(
            turn,
            rawCommands,
            currentWorldTime,
            preTurnCarriers,
            acceptedCarriers,
            realm,
            acceptedReportedLifecycleEvents);
        if (preparedWoundPlan is not null &&
            preparedWounds.Issues.Count == 0 &&
            preparedWoundPlan.Binding.AcceptedEvents.Count != 0)
        {
            eventInput["events"] = new JsonArray(
                preparedWoundPlan.Binding.AcceptedEvents.Select(static value =>
                    (JsonNode)new JsonObject
                    {
                        ["eventRef"] = value.EventRef,
                        ["kind"] = value.Kind,
                        ["authorityId"] = value.AuthorityId
                    }).ToArray());
        }
        return new EffectAcceptedTurnInput(
            sessionId,
            snapshotToken,
            rawCommands.DeepClone().AsObject(),
            sourceAuthority,
            targetAuthority,
            eventInput,
            Realm: realm,
            PreTurnCarriers: CloneCarriers(lifecycleCarriers),
            PreTurnIdentityIndex: preTurnIdentityIndex?.DeepClone().AsObject(),
            TargetAuthorityInput: targetAuthorityInput,
            PublicationCarrierBaselines: CloneCarriers(
                publicationCarrierBaselines ?? acceptedCarriers),
            PreallocatedCombatantIdentities: preallocatedCombatantIdentities,
            AcceptedCarrierBaselines: CloneCarriers(acceptedCarriers),
            SkillScopeAuthority: ComposeSkillScopeAuthority(preTurnSourceRoots, acceptedSourceRoots));
    }

    private static EffectCarrierCatalogInput PreserveClosingSpiritualConflictCarrier(
        EffectCarrierCatalogInput preTurnCarriers,
        EffectCarrierCatalogInput acceptedCarriers)
    {
        if (acceptedCarriers.SpiritualConflict is { } acceptedConflictRoot &&
            acceptedConflictRoot["activeConflict"] == null &&
            preTurnCarriers.SpiritualConflict?["activeConflict"] is JsonObject)
        {
            return acceptedCarriers with
            {
                SpiritualConflict = preTurnCarriers.SpiritualConflict
                    .DeepClone()
                    .AsObject()
            };
        }

        return acceptedCarriers;
    }

    internal static JsonObject CreateEmptyCommandRoot() => new()
    {
        ["effectChanges"] = new JsonArray(),
        ["effectResolutionReceipts"] = new JsonArray(),
        [EffectAcceptedEventReportCatalog.ResponseField] = new JsonArray()
    };

    internal static bool HasPendingCombatantRefs(EffectCarrierCatalogInput carriers) =>
        ContainsCombatantRef(carriers.EnemyCombatants, "enemiesData") ||
        ContainsCombatantRef(carriers.AllyCombatants, "alliesData");

    private static bool ContainsCombatantRef(JsonObject? root, string collection) =>
        root?[collection] is JsonArray combatants &&
        combatants.OfType<JsonObject>().Any(static combatant =>
            combatant.ContainsKey("combatantRef") ||
            combatant["members"] is JsonArray members &&
            members.OfType<JsonObject>().Any(static member =>
                member.ContainsKey("memberRef")));

    internal static JsonObject BuildAcceptedEventInput(
        int turn,
        JsonObject rawCommands,
        long? currentWorldTime = null,
        EffectCarrierCatalogInput? preTurnCarriers = null,
        EffectCarrierCatalogInput? acceptedCarriers = null,
        string commandRealm = "mortal_world",
        IReadOnlyList<JsonObject>? acceptedReportedLifecycleEvents = null)
    {
        var changes = rawCommands["effectChanges"] as JsonArray;
        var operationCount = changes?.Count ?? 0;
        var acceptedExchanges = CollectNewAcceptedAfterlifeExchanges(
            preTurnCarriers?.SpiritualConflict,
            acceptedCarriers?.SpiritualConflict);
        var events = new JsonArray();
        for (var index = 0; index < Math.Max(1, operationCount); index++)
        {
            var ordinal = index + 1;
            if (changes != null &&
                index < changes.Count &&
                changes[index] is JsonObject change &&
                TryBuildAfterlifeExchangeEvent(
                    change,
                    acceptedExchanges,
                    out var exchangeEvent))
            {
                events.Add(exchangeEvent);
                continue;
            }
            events.Add(new JsonObject
            {
                ["kind"] = "accepted_turn",
                ["authorityId"] = ordinal == 1
                    ? $"turn_{turn}"
                    : $"turn_{turn}_effect_{ordinal}",
                ["eventRef"] = ordinal == 1
                    ? $"turn_{turn}:accepted_effect"
                    : $"turn_{turn}:accepted_effect:{ordinal}"
            });
        }
        var currentTargetRealms = acceptedCarriers == null
            ? new Dictionary<(string Kind, string TargetId), string>()
            : CollectAcceptedTargetCurrentRealms(acceptedCarriers);
        var lifecycleTargets = new HashSet<(
            string Realm,
            string CurrentRealm,
            string Kind,
            string TargetId)>();
        if (string.Equals(commandRealm, "mortal_world", StringComparison.Ordinal))
        {
            lifecycleTargets.Add((
                "mortal_world",
                "mortal_world",
                "player",
                "player_current"));
        }
        if (acceptedCarriers != null)
        {
            foreach (var occurrence in EffectCarrierCatalog.Build(acceptedCarriers).Occurrences)
            {
                if (occurrence.Effect["target"] is not JsonObject target ||
                    !TryReadExact(occurrence.Effect["realm"], out var realm) ||
                    !TryReadExact(target["kind"], out var kind) ||
                    !TryReadExact(target["targetId"], out var targetId))
                {
                    continue;
                }
                lifecycleTargets.Add((
                    realm,
                    currentTargetRealms.TryGetValue((kind, targetId), out var currentRealm)
                        ? currentRealm
                        : realm,
                    kind,
                    targetId));
            }
        }
        if (rawCommands["effectChanges"] is JsonArray effectChanges)
        {
            foreach (var change in effectChanges.OfType<JsonObject>())
            {
                if (change["target"] is JsonObject target &&
                    TryReadExact(target["kind"], out var kind) &&
                    TryReadExact(target["targetId"], out var targetId))
                {
                    lifecycleTargets.Add((commandRealm, commandRealm, kind, targetId));
                }
            }
        }
        var lifecycleEvents = new JsonArray(lifecycleTargets
            .OrderBy(static target => target.Realm, StringComparer.Ordinal)
            .ThenBy(static target => target.CurrentRealm, StringComparer.Ordinal)
            .ThenBy(static target => target.Kind, StringComparer.Ordinal)
            .ThenBy(static target => target.TargetId, StringComparer.Ordinal)
            .Select(target => (JsonNode)new JsonObject
            {
                ["eventRef"] = target == (
                        "mortal_world",
                        "mortal_world",
                        "player",
                        "player_current")
                    ? $"turn_{turn}:lifecycle:owner_turn_end:player_current"
                    : $"turn_{turn}:lifecycle:owner_turn_end:{target.Realm}:{target.Kind}:{target.TargetId}",
                ["turn"] = turn,
                ["phase"] = "owner_turn_end",
                ["realm"] = target.Realm,
                ["target"] = new JsonObject
                {
                    ["kind"] = target.Kind,
                    ["targetId"] = target.TargetId
                },
                ["triggerId"] = null,
                ["currentTime"] = currentWorldTime,
                ["currentSceneId"] = null,
                ["sceneClosed"] = false,
                ["sourceSatisfied"] = null,
                ["conditionSatisfied"] = null,
                ["targetSatisfied"] = null,
                ["currentRealm"] = target.CurrentRealm
            }).ToArray());
        foreach (var acceptedExchange in acceptedExchanges
                     .OrderBy(static pair => pair.Value, StringComparer.Ordinal)
                     .ThenBy(static pair => pair.Key, StringComparer.Ordinal))
        {
            if (!TryResolveAfterlifeConflictRealm(
                    acceptedCarriers?.SpiritualConflict ??
                    preTurnCarriers?.SpiritualConflict,
                    acceptedExchange.Value,
                    out var exchangeRealm))
            {
                continue;
            }

            foreach (var side in AfterlifeSpiritualConflictState
                         .CombatConditionTargetSides
                         .OrderBy(static value => value, StringComparer.Ordinal))
            {
                var targetId = $"{acceptedExchange.Value}:{side}";
                var causalEventRef =
                    $"afterlife_exchange:{acceptedExchange.Value}:{acceptedExchange.Key}";
                lifecycleEvents.Add(new JsonObject
                {
                    ["eventRef"] = $"{causalEventRef}:lifecycle:{side}",
                    ["causalEventRef"] = causalEventRef,
                    ["turn"] = turn,
                    ["phase"] = "afterlife_exchange_end",
                    ["realm"] = exchangeRealm,
                    ["target"] = new JsonObject
                    {
                        ["kind"] = "spiritual_conflict_side",
                        ["targetId"] = targetId
                    },
                    ["triggerId"] = null,
                    ["currentTime"] = currentWorldTime,
                    ["currentSceneId"] = acceptedExchange.Value,
                    ["sceneClosed"] = false,
                    ["sourceSatisfied"] = null,
                    ["conditionSatisfied"] = null,
                    ["targetSatisfied"] = null,
                    ["currentRealm"] = exchangeRealm
                });
            }
        }
        if (TryReadActiveConflictAuthority(
                preTurnCarriers?.SpiritualConflict,
                out var closedConflictId,
                out var closedConflictRealm) &&
            (!TryReadActiveConflictAuthority(
                    acceptedCarriers?.SpiritualConflict,
                    out var acceptedConflictId,
                    out _) ||
             !string.Equals(
                 acceptedConflictId,
                 closedConflictId,
                 StringComparison.Ordinal)))
        {
            foreach (var side in AfterlifeSpiritualConflictState
                         .CombatConditionTargetSides
                         .OrderBy(static value => value, StringComparer.Ordinal))
            {
                lifecycleEvents.Add(new JsonObject
                {
                    ["eventRef"] =
                        $"turn_{turn}:lifecycle:afterlife_scene_closed:{closedConflictId}:{side}",
                    ["causalEventRef"] = null,
                    ["turn"] = turn,
                    ["phase"] = "scene_ended",
                    ["realm"] = closedConflictRealm,
                    ["target"] = new JsonObject
                    {
                        ["kind"] = "spiritual_conflict_side",
                        ["targetId"] = $"{closedConflictId}:{side}"
                    },
                    ["triggerId"] = null,
                    ["currentTime"] = currentWorldTime,
                    ["currentSceneId"] = closedConflictId,
                    ["sceneClosed"] = true,
                    ["sourceSatisfied"] = null,
                    ["conditionSatisfied"] = null,
                    ["targetSatisfied"] = false,
                    ["currentRealm"] = closedConflictRealm
                });
            }
        }
        foreach (var reportedEvent in acceptedReportedLifecycleEvents ??
                     Array.Empty<JsonObject>())
        {
            ArgumentNullException.ThrowIfNull(reportedEvent);
            lifecycleEvents.Add(reportedEvent.DeepClone());
        }

        var result = new JsonObject
        {
            ["turn"] = turn,
            ["events"] = events,
            ["lifecycleEvents"] = lifecycleEvents
        };
        if (currentWorldTime.HasValue)
        {
            result["currentTime"] = currentWorldTime.Value;
            result["timeAuthority"] =
                EffectSourceDefinitionContract.CanonicalWorldTimeAuthority;
        }
        if (TryReadCurrentAfterlifeConflictId(
                acceptedCarriers?.SpiritualConflict ??
                preTurnCarriers?.SpiritualConflict,
                out var currentConflictId))
        {
            result["sceneId"] = currentConflictId;
        }
        return result;
    }

    private static Dictionary<(string Kind, string TargetId), string>
        CollectAcceptedTargetCurrentRealms(EffectCarrierCatalogInput carriers)
    {
        var candidates = new List<EffectTargetKey>();
        if (carriers.AfterlifeProfiles?[AfterlifeEntityProfileState.ProfilesProperty]
            is JsonArray profiles)
        {
            foreach (var profile in profiles.OfType<JsonObject>())
            {
                if (AfterlifeEntityProfileState.TryResolveEffectTarget(
                        profile,
                        out var target))
                {
                    candidates.Add(target);
                }
            }
        }

        return candidates
            .GroupBy(static target => (target.Kind, target.TargetId))
            .Where(static group => group.Count() == 1)
            .ToDictionary(
                static group => group.Key,
                static group => group.Single().Realm);
    }

    private static bool TryReadCurrentAfterlifeConflictId(
        JsonObject? root,
        out string conflictId)
    {
        conflictId = string.Empty;
        return root?["activeConflict"] is JsonObject conflict &&
            TryReadExact(conflict["conflictId"], out conflictId) &&
            TryReadExact(conflict["resolutionState"], out var resolutionState) &&
            string.Equals(resolutionState, "active", StringComparison.Ordinal);
    }

    private static bool TryReadActiveConflictAuthority(
        JsonObject? root,
        out string conflictId,
        out string realm)
    {
        conflictId = string.Empty;
        realm = string.Empty;
        return root?["activeConflict"] is JsonObject conflict &&
            TryReadExact(conflict["conflictId"], out conflictId) &&
            TryReadExact(conflict["realm"], out var rawRealm) &&
            AfterlifeEntityProfileState.TryNormalizeEffectRealm(rawRealm, out realm) &&
            TryReadExact(conflict["resolutionState"], out var resolutionState) &&
            string.Equals(resolutionState, "active", StringComparison.Ordinal);
    }

    private static bool TryResolveAfterlifeConflictRealm(
        JsonObject? root,
        string conflictId,
        out string realm)
    {
        realm = string.Empty;
        return root?["activeConflict"] is JsonObject conflict &&
            TryReadExact(conflict["conflictId"], out var candidateConflictId) &&
            string.Equals(candidateConflictId, conflictId, StringComparison.Ordinal) &&
            TryReadExact(conflict["realm"], out var rawRealm) &&
            AfterlifeEntityProfileState.TryNormalizeEffectRealm(rawRealm, out realm);
    }

    private static IReadOnlyDictionary<string, string>
        CollectNewAcceptedAfterlifeExchanges(
            JsonObject? preTurnRoot,
            JsonObject? acceptedRoot)
    {
        var preTurnAliases = EnumerateAfterlifeExchanges(preTurnRoot)
            .Select(static exchange => MortalLocationIdentityState.BuildConfusableKey(
                exchange.ExchangeId))
            .ToHashSet(StringComparer.Ordinal);
        var candidates = EnumerateAfterlifeExchanges(acceptedRoot)
            .Where(exchange => !preTurnAliases.Contains(
                MortalLocationIdentityState.BuildConfusableKey(exchange.ExchangeId)))
            .ToArray();
        var exactCounts = candidates
            .GroupBy(static exchange => exchange.ExchangeId, StringComparer.Ordinal)
            .ToDictionary(static group => group.Key, static group => group.Count(), StringComparer.Ordinal);
        var aliasCounts = candidates
            .GroupBy(
                static exchange => MortalLocationIdentityState.BuildConfusableKey(
                    exchange.ExchangeId),
                StringComparer.Ordinal)
            .ToDictionary(static group => group.Key, static group => group.Count(), StringComparer.Ordinal);
        return candidates
            .Where(exchange =>
                exactCounts[exchange.ExchangeId] == 1 &&
                aliasCounts[MortalLocationIdentityState.BuildConfusableKey(
                    exchange.ExchangeId)] == 1)
            .ToDictionary(
                static exchange => exchange.ExchangeId,
                static exchange => exchange.ConflictId,
                StringComparer.Ordinal);
    }

    private static IEnumerable<(string ConflictId, string ExchangeId)>
        EnumerateAfterlifeExchanges(JsonObject? root)
    {
        if (root?["activeConflict"] is not JsonObject conflict ||
            !TryReadExact(conflict["conflictId"], out var conflictId) ||
            conflict["exchangeLog"] is not JsonArray exchanges)
        {
            yield break;
        }

        foreach (var exchange in exchanges.OfType<JsonObject>())
        {
            if (TryReadExact(exchange["exchangeId"], out var exchangeId))
                yield return (conflictId, exchangeId);
        }
    }

    private static bool TryBuildAfterlifeExchangeEvent(
        JsonObject change,
        IReadOnlyDictionary<string, string> acceptedExchanges,
        out JsonObject acceptedEvent)
    {
        acceptedEvent = null!;
        if (change["eventRef"] is not JsonObject requestedEvent ||
            !TryReadExact(requestedEvent["kind"], out var kind) ||
            !string.Equals(kind, "afterlife_exchange", StringComparison.Ordinal) ||
            !TryReadExact(requestedEvent["authorityId"], out var exchangeId) ||
            !acceptedExchanges.TryGetValue(exchangeId, out var conflictId) ||
            change["target"] is not JsonObject target ||
            !TryReadExact(target["kind"], out var targetKind) ||
            !string.Equals(targetKind, "spiritual_conflict_side", StringComparison.Ordinal) ||
            !TryReadExact(target["targetId"], out var targetId) ||
            !(string.Equals(targetId, conflictId + ":player", StringComparison.Ordinal) ||
              string.Equals(targetId, conflictId + ":opposition", StringComparison.Ordinal)))
        {
            return false;
        }

        acceptedEvent = new JsonObject
        {
            ["kind"] = "afterlife_exchange",
            ["authorityId"] = exchangeId,
            ["eventRef"] = $"afterlife_exchange:{conflictId}:{exchangeId}"
        };
        return true;
    }

    internal static long? ReadCanonicalWorldTime(string? json)
    {
        if (json == null)
            return null;
        try
        {
            using var document = JsonDocument.Parse(json);
            var root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object)
                return null;
            if (root.TryGetProperty("setWorldTime", out var setWorldTime) &&
                setWorldTime.ValueKind != JsonValueKind.Null)
            {
                if (setWorldTime.ValueKind == JsonValueKind.Object &&
                    TryReadNonNegativeLong(
                        setWorldTime,
                        "currentTimeInMinutes",
                        out var acceptedAbsolute))
                {
                    return acceptedAbsolute;
                }
                return null;
            }
            if (TryReadNonNegativeLong(root, "currentTimeInMinutes", out var direct))
                return direct;
        }
        catch (JsonException)
        {
            return null;
        }
        return null;
    }

    private static bool TryReadNonNegativeLong(
        JsonElement root,
        string field,
        out long value)
    {
        value = -1;
        return root.TryGetProperty(field, out var node) &&
            node.ValueKind == JsonValueKind.Number &&
            node.TryGetInt64(out value) &&
            value >= 0;
    }

    internal static EffectAcceptedOwnerExports CollectValidatedSameTurnOwnerExports(
        IReadOnlyDictionary<string, JsonNode?> preTurnSourceRoots,
        IReadOnlyDictionary<string, JsonNode?> acceptedSourceRoots)
    {
        ArgumentNullException.ThrowIfNull(preTurnSourceRoots);
        ArgumentNullException.ThrowIfNull(acceptedSourceRoots);
        var allowedPaths = SameTurnOwnerAuthorityPaths.ToHashSet(StringComparer.Ordinal);
        var changedPaths = acceptedSourceRoots.Keys
            .Where(allowedPaths.Contains)
            .ToHashSet(StringComparer.Ordinal);
        var preTurnRoots = preTurnSourceRoots
            .Where(pair => changedPaths.Contains(pair.Key))
            .ToDictionary(
                static pair => pair.Key,
                static pair => pair.Value,
                StringComparer.Ordinal);
        var validatedRoots = acceptedSourceRoots
            .Where(pair => changedPaths.Contains(pair.Key))
            .ToDictionary(
                static pair => pair.Key,
                static pair => pair.Value,
                StringComparer.Ordinal);
        var beforeSources = CollectSources(preTurnRoots, sameTurn: false);
        var composedRoots = validatedRoots.ToDictionary(
            static pair => pair.Key,
            pair => ComposeAcceptedOwnerRoot(
                pair.Key,
                preTurnRoots.GetValueOrDefault(pair.Key),
                pair.Value),
            StringComparer.Ordinal);
        var afterSources = CollectSources(composedRoots, sameTurn: true);
        var beforeTargets = new List<EffectTargetExport>();
        var afterTargets = new List<EffectTargetExport>();
        CollectNpcTargets(preTurnRoots, includeTemporaryRefs: false, beforeTargets);
        CollectNpcTargets(composedRoots, includeTemporaryRefs: true, afterTargets);
        var acceptedStableTargetKeys = afterTargets
            .Where(static target => target.TargetRef == null)
            .Select(static target => new EffectTargetKey(
                target.Realm,
                target.Kind,
                target.TargetId))
            .ToHashSet();
        return new EffectAcceptedOwnerExports(
            afterSources,
            afterTargets
                .Where(static target => target.TargetRef != null)
                .Select(static target => target with { SameTurn = true })
                .ToArray(),
            beforeSources
                .Concat(afterSources)
                .Select(static source => new EffectSourceOwnerKey(
                    source.Realm,
                    source.Kind,
                    source.SourceId))
                .ToHashSet(),
            beforeTargets
                .Select(static target => new EffectTargetKey(
                    target.Realm,
                    target.Kind,
                    target.TargetId))
                .Where(target => !acceptedStableTargetKeys.Contains(target))
                .ToHashSet());
    }

    private static JsonNode? ComposeAcceptedOwnerRoot(
        string path,
        JsonNode? preTurnRoot,
        JsonNode? acceptedRoot) =>
        path switch
        {
            "game_state/player/skills_active.json" =>
                ComposeSkillAcceptedRoot(
                    preTurnRoot,
                    acceptedRoot,
                    "activeSkillChanges",
                    "removeActiveSkills"),
            "game_state/player/skills_passive.json" =>
                ComposeSkillAcceptedRoot(
                    preTurnRoot,
                    acceptedRoot,
                    "passiveSkillChanges",
                    "removePassiveSkills"),
            "game_state/quests/regular_quests.json" =>
                ComposeQuestRoot(preTurnRoot, acceptedRoot, "UpdateQuests"),
            "game_state/quests/soul_quests.json" =>
                ComposeQuestRoot(preTurnRoot, acceptedRoot, "UpdateSoulQuests"),
            _ => acceptedRoot?.DeepClone()
        };

    internal static EffectRollSkillScopeAuthority ComposeSkillScopeAuthority(
        IReadOnlyDictionary<string, JsonNode?> preTurnRoots,
        IReadOnlyDictionary<string, JsonNode?>? acceptedRoots = null)
    {
        var currentRoots = new Dictionary<string, JsonNode?>(StringComparer.Ordinal);
        foreach (var (path, changes, removals) in new[]
        {
            ("game_state/player/skills_active.json", "activeSkillChanges", "removeActiveSkills"),
            ("game_state/player/skills_passive.json", "passiveSkillChanges", "removePassiveSkills")
        })
        {
            preTurnRoots.TryGetValue(path, out var preTurnRoot);
            JsonNode? acceptedRoot = null;
            acceptedRoots?.TryGetValue(path, out acceptedRoot);
            currentRoots[path] = acceptedRoot is null || JsonNode.DeepEquals(preTurnRoot, acceptedRoot)
                ? preTurnRoot
                : ComposeSkillAcceptedRoot(preTurnRoot, acceptedRoot, changes, removals, path);
        }

        const string npcPath = "game_state/npcs/npc_core.json";
        currentRoots[npcPath] = acceptedRoots != null && acceptedRoots.TryGetValue(npcPath, out var acceptedNpcRoot)
            ? acceptedNpcRoot
            : preTurnRoots.GetValueOrDefault(npcPath);
        return EffectRollSkillScopeAuthority.Build(new EffectRollSkillScopeAuthorityInput(preTurnRoots, currentRoots));
    }

    internal static JsonObject ComposeSkillAcceptedRoot(
        JsonNode? preTurnRoot,
        JsonNode? acceptedRoot,
        string changeProperty,
        string removalProperty,
        string? path = null)
    {
        var issues = new List<ValidationIssue>();
        var result = ComposeSkillAcceptedRootCore(
            preTurnRoot,
            acceptedRoot,
            changeProperty,
            removalProperty,
            path ?? changeProperty,
            issues);
        if (issues.Count > 0)
        {
            throw new InvalidDataException(
                $"Skill composition is ambiguous at '{path ?? changeProperty}': " +
                issues[0].Code);
        }
        return result;
    }

    internal static IReadOnlyList<ValidationIssue> ValidateAcceptedSkillComposition(
        IReadOnlyDictionary<string, JsonNode?> preTurnRoots,
        IReadOnlyDictionary<string, JsonNode?> acceptedRoots)
    {
        ArgumentNullException.ThrowIfNull(preTurnRoots);
        ArgumentNullException.ThrowIfNull(acceptedRoots);
        var issues = new List<ValidationIssue>();
        foreach (var (path, changeProperty, removalProperty) in new[]
                 {
                     ("game_state/player/skills_active.json", "activeSkillChanges", "removeActiveSkills"),
                     ("game_state/player/skills_passive.json", "passiveSkillChanges", "removePassiveSkills")
                 })
        {
            var preTurnRoot = preTurnRoots.GetValueOrDefault(path);
            var acceptedRoot = acceptedRoots.GetValueOrDefault(path);
            if (acceptedRoot == null || JsonNode.DeepEquals(preTurnRoot, acceptedRoot))
                continue;
            _ = ComposeSkillAcceptedRootCore(
                preTurnRoot,
                acceptedRoot,
                changeProperty,
                removalProperty,
                path,
                issues);
        }
        return issues;
    }

    private static JsonObject ComposeSkillAcceptedRootCore(
        JsonNode? preTurnRoot,
        JsonNode? acceptedRoot,
        string changeProperty,
        string removalProperty,
        string path,
        List<ValidationIssue> issues)
    {
        var skills = EnumerateSkillEntries(preTurnRoot, changeProperty)
            .Select(static skill => skill.DeepClone().AsObject())
            .ToList();
        var updateIndex = 0;
        foreach (var skill in EnumerateSkillEntries(acceptedRoot, changeProperty))
        {
            ApplySkillUpdate(
                skills,
                skill,
                $"{path}.{changeProperty}[{updateIndex++}]",
                issues);
        }

        if (acceptedRoot is JsonObject acceptedObject &&
            acceptedObject[removalProperty] is JsonArray removals)
        {
            var removalIndex = 0;
            foreach (var removal in removals)
            {
                if (!TryReadExact(removal, out var selector))
                {
                    removalIndex++;
                    continue;
                }
                ApplySkillRemoval(
                    skills,
                    selector,
                    $"{path}.{removalProperty}[{removalIndex++}]",
                    issues);
            }
        }

        ValidateComposedSkillNames(skills, path, changeProperty, issues);
        var result = new JsonObject
        {
            [changeProperty] = new JsonArray(
                skills.Select(static skill => (JsonNode?)skill).ToArray())
        };
        CopySkillMetadata(preTurnRoot, result);
        CopySkillMetadata(acceptedRoot, result);
        return result;
    }

    private static void CopySkillMetadata(JsonNode? source, JsonObject target)
    {
        if (source is not JsonObject sourceObject)
            return;
        foreach (var property in sourceObject.Where(static property =>
                     property.Key.StartsWith("_", StringComparison.Ordinal)))
        {
            target[property.Key] = property.Value?.DeepClone();
        }
    }

    private static IEnumerable<JsonObject> EnumerateSkillEntries(
        JsonNode? root,
        string changeProperty)
    {
        if (root is JsonArray array)
        {
            foreach (var skill in array.OfType<JsonObject>())
                yield return skill.DeepClone().AsObject();
            yield break;
        }
        if (root is not JsonObject objectRoot)
            yield break;
        foreach (var property in new[] { changeProperty, "skills" })
        {
            if (objectRoot[property] is not JsonArray entries)
                continue;
            foreach (var skill in entries.OfType<JsonObject>())
                yield return skill.DeepClone().AsObject();
        }
    }

    private static void ApplySkillUpdate(
        List<JsonObject> skills,
        JsonObject candidate,
        string path,
        List<ValidationIssue> issues)
    {
        var candidateId = ReadFirstExact(candidate, "skillId");
        if (candidateId != null)
        {
            var exact = FindSkillIndexes(skills, "skillId", candidateId, confusable: false);
            var aliases = FindSkillIndexes(skills, "skillId", candidateId, confusable: true);
            if (exact.Count > 1 || aliases.Count > 1)
            {
                AddSkillCompositionIssue(issues, path + ".skillId",
                    "effect_skill_composition_update_ambiguous",
                    "one exact/confusable skillId update target", candidateId);
                return;
            }
            if (exact.Count == 0 && aliases.Count == 1)
            {
                AddSkillCompositionIssue(issues, path + ".skillId",
                    "effect_skill_composition_update_confusable",
                    "exact skillId or one new non-confusable skillId", candidateId);
                return;
            }
            if (exact.Count == 1)
                skills[exact[0]] = candidate.DeepClone().AsObject();
            else
                skills.Add(candidate.DeepClone().AsObject());
            return;
        }

        var candidateName = ReadFirstExact(candidate, "skillName", "name");
        if (candidateName == null)
        {
            skills.Add(candidate.DeepClone().AsObject());
            return;
        }
        var exactNames = FindSkillNameIndexes(skills, candidateName, confusable: false);
        var aliasNames = FindSkillNameIndexes(skills, candidateName, confusable: true);
        if (exactNames.Count > 1 || aliasNames.Count > 1)
        {
            AddSkillCompositionIssue(issues, path + ".skillName",
                "effect_skill_composition_update_ambiguous",
                "one exact/confusable skillName update target", candidateName);
            return;
        }
        if (exactNames.Count == 0 && aliasNames.Count == 1)
        {
            AddSkillCompositionIssue(issues, path + ".skillName",
                "effect_skill_composition_update_confusable",
                "exact skillName or one new non-confusable skillName", candidateName);
            return;
        }
        if (exactNames.Count == 1)
            skills[exactNames[0]] = candidate.DeepClone().AsObject();
        else
            skills.Add(candidate.DeepClone().AsObject());
    }

    private static void ApplySkillRemoval(
        List<JsonObject> skills,
        string selector,
        string path,
        List<ValidationIssue> issues)
    {
        var exact = FindSkillNameIndexes(skills, selector, confusable: false);
        var aliases = FindSkillNameIndexes(skills, selector, confusable: true);
        if (exact.Count == 1 && aliases.Count == 1)
        {
            skills.RemoveAt(exact[0]);
            return;
        }

        var code = exact.Count == 0 && aliases.Count == 0
            ? "effect_skill_composition_selector_unknown"
            : exact.Count == 0 && aliases.Count == 1
                ? "effect_skill_composition_selector_confusable"
                : "effect_skill_composition_selector_ambiguous";
        AddSkillCompositionIssue(issues, path, code,
            "one exact, non-confusable skillName removal target", selector);
    }

    private static void ValidateComposedSkillNames(
        IReadOnlyList<JsonObject> skills,
        string path,
        string changeProperty,
        List<ValidationIssue> issues)
    {
        var exactNames = new HashSet<string>(StringComparer.Ordinal);
        var aliasNames = new HashSet<string>(StringComparer.Ordinal);
        for (var index = 0; index < skills.Count; index++)
        {
            var name = ReadFirstExact(skills[index], "skillName", "name");
            if (name == null)
                continue;
            if (!exactNames.Add(name))
            {
                AddSkillCompositionIssue(issues,
                    $"{path}.{changeProperty}[{index}].skillName",
                    "effect_skill_composition_name_ambiguous",
                    "globally exact-unique composed skillName", name);
            }
            else if (!aliasNames.Add(MortalLocationIdentityState.BuildConfusableKey(name)))
            {
                AddSkillCompositionIssue(issues,
                    $"{path}.{changeProperty}[{index}].skillName",
                    "effect_skill_composition_name_confusable",
                    "globally exact/confusable-unique composed skillName", name);
            }
        }
    }

    private static List<int> FindSkillIndexes(
        IReadOnlyList<JsonObject> skills,
        string field,
        string selector,
        bool confusable)
    {
        var result = new List<int>();
        var key = confusable
            ? MortalLocationIdentityState.BuildConfusableKey(selector)
            : selector;
        for (var index = 0; index < skills.Count; index++)
        {
            if (!TryReadExact(skills[index][field], out var value))
                continue;
            var candidate = confusable
                ? MortalLocationIdentityState.BuildConfusableKey(value)
                : value;
            if (string.Equals(candidate, key, StringComparison.Ordinal))
                result.Add(index);
        }
        return result;
    }

    private static List<int> FindSkillNameIndexes(
        IReadOnlyList<JsonObject> skills,
        string selector,
        bool confusable)
    {
        var result = new List<int>();
        var key = confusable
            ? MortalLocationIdentityState.BuildConfusableKey(selector)
            : selector;
        for (var index = 0; index < skills.Count; index++)
        {
            var name = ReadFirstExact(skills[index], "skillName", "name");
            if (name == null)
                continue;
            var candidate = confusable
                ? MortalLocationIdentityState.BuildConfusableKey(name)
                : name;
            if (string.Equals(candidate, key, StringComparison.Ordinal))
                result.Add(index);
        }
        return result;
    }

    private static void AddSkillCompositionIssue(
        List<ValidationIssue> issues,
        string path,
        string code,
        string expected,
        string actual)
    {
        issues.Add(new ValidationIssue(
            path,
            IssueSeverity.Error,
            "Player skill composition requires one exact, non-confusable owner target.",
            code: code,
            section: "effect_materialization",
            expected: expected,
            actual: actual,
            repairHint: "Use complete skill objects with stable exact skillId values and exact unique skillName selectors for removals."));
    }

    private static JsonObject ComposeQuestRoot(
        JsonNode? preTurnRoot,
        JsonNode? acceptedRoot,
        string updateProperty)
    {
        var quests = new List<JsonObject>();
        foreach (var quest in EnumerateQuestEntries(preTurnRoot, updateProperty))
            UpsertQuest(quests, quest);
        foreach (var quest in EnumerateQuestEntries(acceptedRoot, updateProperty))
            UpsertQuest(quests, quest);
        return new JsonObject
        {
            ["quests"] = new JsonArray(
                quests.Select(static quest => (JsonNode?)quest).ToArray())
        };
    }

    private static IEnumerable<JsonObject> EnumerateQuestEntries(
        JsonNode? root,
        string updateProperty)
    {
        if (root is JsonArray array)
        {
            foreach (var quest in array.OfType<JsonObject>())
                yield return quest.DeepClone().AsObject();
            yield break;
        }
        if (root is not JsonObject objectRoot)
            yield break;
        foreach (var property in new[] { "quests", updateProperty })
        {
            if (objectRoot[property] is not JsonArray entries)
                continue;
            foreach (var quest in entries.OfType<JsonObject>())
            {
                var clone = quest.DeepClone().AsObject();
                clone.Remove("newDetailsLogEntry");
                yield return clone;
            }
        }
    }

    private static void UpsertQuest(
        List<JsonObject> quests,
        JsonObject candidate)
    {
        var existing = quests.FirstOrDefault(quest =>
            QuestIdentityMatches(quest, candidate));
        if (existing == null)
        {
            quests.Add(candidate.DeepClone().AsObject());
            return;
        }
        foreach (var property in candidate)
            existing[property.Key] = property.Value?.DeepClone();
    }

    private static bool QuestIdentityMatches(
        JsonObject left,
        JsonObject right)
    {
        var leftQuestId = ReadFirstExact(left, "questId");
        var rightQuestId = ReadFirstExact(right, "questId");
        if (leftQuestId != null || rightQuestId != null)
        {
            return leftQuestId != null &&
                   rightQuestId != null &&
                   string.Equals(leftQuestId, rightQuestId, StringComparison.Ordinal);
        }

        var leftInitialId = ReadFirstExact(left, "initialId");
        var rightInitialId = ReadFirstExact(right, "initialId");
        if (leftInitialId != null || rightInitialId != null)
        {
            return leftInitialId != null &&
                   rightInitialId != null &&
                   string.Equals(leftInitialId, rightInitialId, StringComparison.Ordinal);
        }

        foreach (var field in new[] { "questName", "title", "name" })
        {
            if (TryReadExact(left[field], out var leftValue) &&
                TryReadExact(right[field], out var rightValue))
            {
                return string.Equals(leftValue, rightValue, StringComparison.Ordinal);
            }
        }
        return false;
    }

    internal static EffectSourceAuthority BuildCanonicalSourceAuthority(
        IReadOnlyDictionary<string, JsonNode?> sourceRoots,
        WoundPreparedAcceptedTurnPlan? preparedWoundPlan = null)
    {
        ArgumentNullException.ThrowIfNull(sourceRoots);
        var persistedWounds = CollectCanonicalWoundSources(
            sourceRoots,
            sameTurn: false);
        var preparedWounds = preparedWoundPlan is null
            ? CanonicalWoundSourceComposition.Empty
            : ComposePreparedWoundSources(
                preparedWoundPlan.Binding.SessionId,
                preparedWoundPlan.Binding.SnapshotToken,
                preparedWoundPlan.Binding.Turn,
                preparedWoundPlan.Binding.Realm,
                preparedWoundPlan,
                externallySuppliedExports: null);
        var replacedWoundOwners = preparedWounds.Groups
            .Select(static group => new EffectSourceOwnerKey(
                group.Key.Realm,
                group.Key.Kind,
                group.Key.SourceId))
            .ToHashSet();
        return EffectSourceAuthority.Build(new EffectSourceAuthorityInput(
            CollectSources(sourceRoots, sameTurn: false)
                .Concat(persistedWounds.Exports)
                .Concat(EffectBuiltInSourceCatalog.CreateCanonicalExports())
                .Where(export => !replacedWoundOwners.Contains(
                    new EffectSourceOwnerKey(
                        export.Realm,
                        export.Kind,
                        export.SourceId)))
                .ToArray(),
            preparedWounds.Exports,
            new HashSet<string>(StringComparer.Ordinal),
            WoundGroups: persistedWounds.Groups
                .Where(group => !replacedWoundOwners.Contains(
                    new EffectSourceOwnerKey(
                        group.Key.Realm,
                        group.Key.Kind,
                        group.Key.SourceId)))
                .Concat(preparedWounds.Groups)
                .ToArray(),
            CompositionIssues: persistedWounds.Issues
                .Concat(preparedWounds.Issues)
                .ToArray()));
    }

    internal static IReadOnlyList<ValidationIssue> ValidateRegisteredSourceOwners(
        IReadOnlyDictionary<string, JsonNode?> sourceRoots)
    {
        ArgumentNullException.ThrowIfNull(sourceRoots);
        var issues = new List<ValidationIssue>();
        foreach (var root in sourceRoots.OrderBy(static pair => pair.Key, StringComparer.Ordinal))
        {
            foreach (var candidate in EnumerateRegisteredSourceObjects(
                         root.Value,
                         root.Key,
                         InferRealm(root.Key)))
            {
                if (candidate.Owner["activeEffectDefinitions"] is not JsonArray)
                    continue;
                if (TryResolveSourceDescriptor(candidate, out _, out _))
                    continue;
                issues.Add(new ValidationIssue(
                    root.Key + ".activeEffectDefinitions",
                    IssueSeverity.Error,
                    "Effect source owner requires one exact stable identity on its registered owning contour.",
                    code: "effect_source_authority_invalid_owner_identity",
                    section: "effect_materialization",
                    expected: "exact source-kind identity with no conflicting identity selectors",
                    actual: candidate.Owner.ToJsonString(),
                    repairHint: "Add the exact stable identity required by this source contour and remove identity fields belonging to other source kinds."));
            }
        }
        return issues;
    }

    internal static EffectTargetAuthority BuildCanonicalTargetAuthority(
        EffectCarrierCatalogInput carriers,
        IReadOnlyDictionary<string, JsonNode?> sourceRoots)
    {
        ArgumentNullException.ThrowIfNull(carriers);
        ArgumentNullException.ThrowIfNull(sourceRoots);
        return EffectTargetAuthority.Build(new EffectTargetAuthorityInput(
            CollectTargets(carriers, sourceRoots, includeTemporaryRefs: false),
            Array.Empty<EffectTargetExport>(),
            new HashSet<string>(StringComparer.Ordinal),
            null));
    }

    internal static IReadOnlyList<EffectTargetExport> CollectAfterlifePlanTargets(
        ResourceOwnerAuthority? ownerAuthority,
        JsonObject? acceptedProfilesRoot)
    {
        if (ownerAuthority == null ||
            acceptedProfilesRoot?[AfterlifeEntityProfileState.ProfilesProperty]
                is not JsonArray profiles)
        {
            return Array.Empty<EffectTargetExport>();
        }

        var profilesById = profiles
            .OfType<JsonObject>()
            .Where(static profile => TryReadExact(profile["actorId"], out _))
            .GroupBy(
                static profile => profile["actorId"]!.GetValue<string>(),
                StringComparer.Ordinal)
            .Where(static group => group.Count() == 1)
            .ToDictionary(
                static group => group.Key,
                static group => group.Single(),
                StringComparer.Ordinal);
        var exports = new List<EffectTargetExport>();
        foreach (var owner in ownerAuthority.ExportInput().SameTurnOwners
                     .Where(static owner =>
                         owner.Key.OwnerKind == ResourceOwnerKind.AfterlifeActor)
                     .OrderBy(static owner => owner.Key.Realm, StringComparer.Ordinal)
                     .ThenBy(
                         static owner => owner.Key.ResourceOwnerId,
                         StringComparer.Ordinal))
        {
            if (owner.OwnerRef == null ||
                !profilesById.TryGetValue(owner.Key.ResourceOwnerId, out var profile) ||
                !AfterlifeEntityProfileState.TryResolveEffectTarget(
                    profile,
                    out var target) ||
                !string.Equals(
                    target.Realm,
                    owner.Key.Realm,
                    StringComparison.Ordinal) ||
                !string.Equals(
                    target.TargetId,
                    owner.Key.ResourceOwnerId,
                    StringComparison.Ordinal))
            {
                continue;
            }

            exports.Add(new EffectTargetExport(
                owner.Key.Realm,
                target.Kind,
                owner.Key.ResourceOwnerId,
                SameTurn: true,
                TargetRef: owner.OwnerRef));
        }
        return exports;
    }

    internal static EffectAcceptedCombatMemberTargetExports CollectCombatMemberPlanTargets(
        EffectCarrierCatalogInput preTurnCarriers,
        EffectCarrierCatalogInput acceptedCarriers,
        CombatantIdentityState? combatantIdentities)
    {
        ArgumentNullException.ThrowIfNull(preTurnCarriers);
        ArgumentNullException.ThrowIfNull(acceptedCarriers);

        var before = CollectCombatMemberTargets(preTurnCarriers, null);
        var after = CollectCombatMemberTargets(
            acceptedCarriers,
            combatantIdentities);
        var beforeIds = before
            .Select(static target => target.TargetId)
            .ToHashSet(StringComparer.Ordinal);
        var afterIds = after
            .Select(static target => target.TargetId)
            .ToHashSet(StringComparer.Ordinal);
        return new EffectAcceptedCombatMemberTargetExports(
            after.Where(target => !beforeIds.Contains(target.TargetId)).ToArray(),
            beforeIds
                .Except(afterIds, StringComparer.Ordinal)
                .Select(static targetId => new EffectTargetKey(
                    "mortal_world",
                    "combatant",
                    targetId))
                .ToHashSet());
    }

    private static IReadOnlyList<EffectTargetExport> CollectCombatMemberTargets(
        EffectCarrierCatalogInput carriers,
        CombatantIdentityState? combatantIdentities)
    {
        var refsById = combatantIdentities?.MemberIdsByRef
            .ToDictionary(
                static pair => pair.Value,
                static pair => pair.Key,
                StringComparer.Ordinal) ??
            new Dictionary<string, string>(StringComparer.Ordinal);
        var result = new List<EffectTargetExport>();
        CollectCombatMemberTargets(
            carriers.EnemyCombatants,
            "enemiesData",
            refsById,
            result);
        CollectCombatMemberTargets(
            carriers.AllyCombatants,
            "alliesData",
            refsById,
            result);
        return result;
    }

    private static void CollectCombatMemberTargets(
        JsonObject? root,
        string collection,
        IReadOnlyDictionary<string, string> refsById,
        List<EffectTargetExport> result)
    {
        if (root?[collection] is not JsonArray combatants)
            return;
        foreach (var combatant in combatants.OfType<JsonObject>())
        {
            if (combatant["isGroup"] is JsonValue groupNode &&
                groupNode.TryGetValue<bool>(out var isGroup) &&
                isGroup)
            {
                foreach (var member in
                         (combatant["members"] as JsonArray)?.OfType<JsonObject>() ??
                         Enumerable.Empty<JsonObject>())
                {
                    AddCombatMemberTarget(member, refsById, result);
                }
                continue;
            }
            AddCombatMemberTarget(combatant, refsById, result);
        }
    }

    private static void AddCombatMemberTarget(
        JsonObject member,
        IReadOnlyDictionary<string, string> refsById,
        List<EffectTargetExport> result)
    {
        if (!TryReadExact(member["memberId"], out var memberId))
            return;
        refsById.TryGetValue(memberId, out var memberRef);
        result.Add(new EffectTargetExport(
            "mortal_world",
            "combatant",
            memberId,
            SameTurn: memberRef != null,
            TargetRef: memberRef,
            BoundResourceOwnerKind: ResourceOwnerKind.CombatGroupMember));
    }

    internal static IReadOnlyList<EffectSourceExport> CollectLocationPlanSources(
        MortalLocationAcceptedTurnPlan? plan)
    {
        if (plan == null || plan.LocationIdsByInitialId.Count == 0)
            return Array.Empty<EffectSourceExport>();

        var locationsById = (plan.FinalWorldMap["locations"] as JsonArray)?
            .OfType<JsonObject>()
            .Where(static location => TryReadExact(location["locationId"], out _))
            .ToDictionary(
                static location => location["locationId"]!.GetValue<string>(),
                static location => location,
                StringComparer.Ordinal) ?? new Dictionary<string, JsonObject>(StringComparer.Ordinal);
        var exports = new List<EffectSourceExport>();
        foreach (var mapping in plan.LocationIdsByInitialId.OrderBy(
                     static pair => pair.Key,
                     StringComparer.Ordinal))
        {
            if (!locationsById.TryGetValue(mapping.Value, out var location))
                continue;

            var definitions = location["activeEffectDefinitions"] as JsonArray;
            exports.Add(new EffectSourceExport(
                "mortal_world",
                "location",
                mapping.Value,
                definitions?.DeepClone().AsArray() ?? new JsonArray(),
                Materializable: true,
                Active: true,
                SameTurn: true,
                SourceRef: mapping.Key));

            foreach (var hazard in EnumerateArrayOwners(
                         location,
                         "hazards",
                         "activeHazards"))
            {
                if (!TryReadExact(hazard["hazardId"], out var hazardId))
                {
                    continue;
                }
                var hazardDefinitions = hazard["activeEffectDefinitions"] as JsonArray;

                var active = IsSourceCurrentlyActive(
                    MortalLocationMaterializationContract.WorldMapPath,
                    "hazard",
                    hazard);
                exports.Add(new EffectSourceExport(
                    "mortal_world",
                    "hazard",
                    hazardId,
                    hazardDefinitions?.DeepClone().AsArray() ?? new JsonArray(),
                    Materializable: true,
                    Active: active,
                    SameTurn: true,
                    SatisfiedPredicates: BuildSatisfiedPredicates(
                        MortalLocationMaterializationContract.WorldMapPath,
                        "hazard",
                        hazardId,
                        hazard,
                        plan.FinalWorldMap,
                        active)));
            }
        }
        return exports;
    }

    private static IReadOnlyList<EffectSourceExport> CollectSources(
        IReadOnlyDictionary<string, JsonNode?> roots,
        bool sameTurn)
    {
        roots.TryGetValue(
            MortalLocationMaterializationContract.CurrentLocationPath,
            out var currentLocationRoot);
        var currentLocation = currentLocationRoot is JsonObject currentRoot &&
                              currentRoot["currentLocationData"] is JsonObject wrappedCurrent
            ? wrappedCurrent
            : currentLocationRoot as JsonObject;
        var selectedLocationId = currentLocation == null
            ? null
            : ReadFirstExact(currentLocation, "locationId");
        var selectedLocationSources = roots.TryGetValue(
                MortalLocationMaterializationContract.CurrentLocationPath,
                out currentLocationRoot)
            ? EnumerateRegisteredSourceObjects(
                    currentLocationRoot,
                    MortalLocationMaterializationContract.CurrentLocationPath,
                    "mortal_world")
                .Select(candidate => TryResolveSourceDescriptor(
                        candidate,
                        out var descriptor,
                        out var sourceId)
                    ? new EffectSourceOwnerKey("mortal_world", descriptor.Kind, sourceId)
                    : null)
                .OfType<EffectSourceOwnerKey>()
                .ToHashSet()
            : new HashSet<EffectSourceOwnerKey>();
        var exports = new List<EffectSourceExport>();
        foreach (var root in roots.OrderBy(static pair => pair.Key, StringComparer.Ordinal))
        {
            foreach (var candidate in EnumerateRegisteredSourceObjects(
                         root.Value,
                         root.Key,
                         InferRealm(root.Key)))
            {
                var owner = candidate.Owner;
                if (!TryResolveSourceDescriptor(
                        candidate,
                        out var descriptor,
                        out var sourceId))
                {
                    continue;
                }
                if (string.Equals(
                        root.Key,
                        MortalLocationMaterializationContract.WorldMapPath,
                        StringComparison.Ordinal) &&
                    (descriptor.Kind is "location" or "hazard") &&
                    selectedLocationId != null &&
                    string.Equals(
                        candidate.OwningLocationId,
                        selectedLocationId,
                        StringComparison.Ordinal) &&
                    selectedLocationSources.Contains(new EffectSourceOwnerKey(
                        "mortal_world",
                        descriptor.Kind,
                        sourceId)))
                {
                    continue;
                }
                var definitions = owner["activeEffectDefinitions"] as JsonArray;

                var active = candidate.ActiveOverride ?? IsSourceCurrentlyActive(
                    root.Key,
                    descriptor.Kind,
                    owner);
                exports.Add(new EffectSourceExport(
                    candidate.Realm,
                    descriptor.Kind,
                    sourceId,
                    definitions?.DeepClone().AsArray() ?? new JsonArray(),
                    Materializable: true,
                    Active: active,
                    SameTurn: sameTurn,
                    SatisfiedPredicates: BuildSatisfiedPredicates(
                        root.Key,
                        descriptor.Kind,
                        sourceId,
                        owner,
                        root.Value,
                        active)));
            }
        }
        return exports;
    }

    private static CanonicalWoundSourceComposition CollectCanonicalWoundSources(
        IReadOnlyDictionary<string, JsonNode?> roots,
        bool sameTurn)
    {
        static JsonObject? ReadRoot(
            IReadOnlyDictionary<string, JsonNode?> sourceRoots,
            string path) =>
            sourceRoots.TryGetValue(path, out var node)
                ? node as JsonObject
                : null;

        if (!roots.ContainsKey(WoundCarrierCatalog.PlayerPath) &&
            !roots.ContainsKey(WoundCarrierCatalog.NpcPath) &&
            !roots.ContainsKey(WoundCarrierCatalog.EnemiesPath) &&
            !roots.ContainsKey(WoundCarrierCatalog.AlliesPath) &&
            !roots.ContainsKey(WoundCarrierCatalog.AfterlifeProfilesPath))
            return CanonicalWoundSourceComposition.Empty;

        var catalog = WoundCarrierCatalog.Build(new WoundCarrierCatalogInput(
            ReadRoot(roots, WoundCarrierCatalog.PlayerPath),
            ReadRoot(roots, WoundCarrierCatalog.NpcPath),
            ReadRoot(roots, WoundCarrierCatalog.EnemiesPath),
            ReadRoot(roots, WoundCarrierCatalog.AlliesPath),
            ReadRoot(roots, WoundCarrierCatalog.AfterlifeProfilesPath)));
        if (catalog.Issues.Count != 0)
        {
            return new CanonicalWoundSourceComposition(
                Array.Empty<EffectSourceExport>(),
                Array.Empty<WoundSourceGroupAuthority>(),
                catalog.Issues.ToArray());
        }

        var exports = new List<EffectSourceExport>();
        var groups = new List<WoundSourceGroupAuthority>();
        var issues = new List<ValidationIssue>();
        foreach (var occurrence in catalog.Occurrences
                     .OrderBy(static value => value.Wound.Owner.Realm, StringComparer.Ordinal)
                     .ThenBy(static value => value.WoundId, StringComparer.Ordinal))
        {
            if (!WoundEffectCarrierAdapter.TryCreateTargetKey(
                    occurrence.Wound.Owner,
                    out var target))
            {
                issues.Add(new ValidationIssue(
                    occurrence.JsonPath + ".owner",
                    IssueSeverity.Error,
                    "A canonical wound owner must map to one exact effect target.",
                    code: "effect_source_wound_owner_target_invalid",
                    section: "wound_materialization",
                    expected: "closed wound owner-to-effect-target mapping",
                    actual: occurrence.Wound.Owner.ToString(),
                    repairHint: "Restore the exact canonical wound owner coordinate before composing its effect source graph."));
                continue;
            }

            var definitions = occurrence.Wound.Consequences.OwnedEffectSources.Definitions
                .Select(static definition =>
                {
                    var node = JsonNode.Parse(definition.GetRawText())!.AsObject();
                    return new WoundEffectSourceDefinition(
                        node["definitionKey"]!.GetValue<string>(),
                        node);
                })
                .ToArray();
            var definitionNodes = new JsonArray(definitions
                .Select(static definition => (JsonNode)definition.Definition)
                .ToArray());
            var rootDomains = occurrence.Wound.Consequences.OwnedEffectSources
                .RootBindings.ToDictionary(
                    static binding => binding.EffectId,
                    static _ => WoundRootOwnershipDomain.BaseWound,
                    StringComparer.Ordinal);
            foreach (var complication in occurrence.Wound.Complications)
            {
                foreach (var effectId in complication.OwnedEffectIds)
                {
                    if (!rootDomains.TryGetValue(effectId, out var current) ||
                        current != WoundRootOwnershipDomain.BaseWound)
                    {
                        issues.Add(new ValidationIssue(
                            occurrence.JsonPath + ".complications",
                            IssueSeverity.Error,
                            "A persisted wound root must belong to one exact ownership domain.",
                            code: "effect_source_wound_root_domain_invalid",
                            section: "wound_materialization",
                            expected: "pairwise-disjoint existing root ownership",
                            actual: effectId,
                            repairHint: "Restore complication ownedEffectIds to a pairwise-disjoint subset of canonical wound root bindings."));
                        continue;
                    }
                    rootDomains[effectId] =
                        WoundRootOwnershipDomain.ForComplication(
                            complication.ComplicationId);
                }
            }
            var existingRoots = occurrence.Wound.Consequences.OwnedEffectSources
                .RootBindings
                .OrderBy(static binding => binding.EffectId, StringComparer.Ordinal)
                .ThenBy(static binding => binding.DefinitionKey, StringComparer.Ordinal)
                .Select(binding => new WoundRootLineageAuthorityRow(
                    null,
                    binding.EffectId,
                    binding.DefinitionKey,
                    rootDomains[binding.EffectId]))
                .ToArray();
            exports.Add(new EffectSourceExport(
                occurrence.Wound.Owner.Realm,
                "wound",
                occurrence.WoundId,
                definitionNodes,
                Materializable: false,
                Active: string.Equals(
                    occurrence.Wound.Lifecycle,
                    "active",
                    StringComparison.Ordinal),
                SameTurn: sameTurn));
            groups.Add(new WoundSourceGroupAuthority(
                new EffectIdentitySourceGroup(
                    occurrence.Wound.Owner.Realm,
                    "wound",
                    occurrence.WoundId),
                occurrence.Wound.Owner,
                target,
                sameTurn,
                sourceRef: null,
                preparedSourceExportFingerprint: null,
                definitions,
                Array.Empty<WoundRootLineageAuthorityRow>(),
                existingRoots));
        }

        if (issues.Count != 0)
        {
            return new CanonicalWoundSourceComposition(
                Array.Empty<EffectSourceExport>(),
                Array.Empty<WoundSourceGroupAuthority>(),
                issues);
        }
        return new CanonicalWoundSourceComposition(exports, groups, issues);
    }

    private static CanonicalWoundSourceComposition ComposePreparedWoundSources(
        string sessionId,
        string snapshotToken,
        int turn,
        string realm,
        WoundPreparedAcceptedTurnPlan? prepared,
        IReadOnlyList<EffectSourceExport>? externallySuppliedExports)
    {
        var issues = new List<ValidationIssue>();
        var injectedWounds = (externallySuppliedExports ??
                Array.Empty<EffectSourceExport>())
            .Where(static export => export is not null && string.Equals(
                export.Kind,
                "wound",
                StringComparison.Ordinal))
            .ToArray();
        if (injectedWounds.Length != 0)
        {
            issues.Add(WoundCompositionIssue(
                "effect_source_wound_external_export_forbidden",
                "Wound source exports must descend from the sealed prepared wound plan.",
                "no caller-supplied generic wound exports",
                injectedWounds.Length.ToString(
                    System.Globalization.CultureInfo.InvariantCulture)));
        }
        var injectedLegacies = (externallySuppliedExports ??
                Array.Empty<EffectSourceExport>())
            .Count(static export => export is not null && string.Equals(
                export.Kind,
                "wound_legacy",
                StringComparison.Ordinal));
        if (injectedLegacies != 0)
        {
            issues.Add(WoundCompositionIssue(
                "effect_source_wound_legacy_external_export_forbidden",
                "Wound legacy source exports require private approved legacy preparation.",
                "no caller-supplied generic wound legacy exports",
                injectedLegacies.ToString(
                    System.Globalization.CultureInfo.InvariantCulture)));
        }

        if (prepared is null)
        {
            return issues.Count == 0
                ? CanonicalWoundSourceComposition.Empty
                : new CanonicalWoundSourceComposition(
                    Array.Empty<EffectSourceExport>(),
                    Array.Empty<WoundSourceGroupAuthority>(),
                    issues);
        }

        issues.AddRange(WoundAcceptedTurnPlannerCore.ValidatePreparedAuthority(prepared));
        var binding = prepared.Binding;
        if (!string.Equals(binding.SessionId, sessionId, StringComparison.Ordinal) ||
            !string.Equals(binding.SnapshotToken, snapshotToken, StringComparison.Ordinal) ||
            binding.Turn != turn ||
            !string.Equals(binding.Realm, realm, StringComparison.Ordinal))
        {
            issues.Add(WoundCompositionIssue(
                "effect_source_wound_prepared_binding_mismatch",
                "The prepared wound authority does not belong to this effect input.",
                "exact session, snapshot, turn, and realm binding",
                $"{binding.SessionId}/{binding.SnapshotToken}/{binding.Turn}/{binding.Realm}"));
        }
        if (issues.Count != 0)
        {
            return new CanonicalWoundSourceComposition(
                Array.Empty<EffectSourceExport>(),
                Array.Empty<WoundSourceGroupAuthority>(),
                issues);
        }

        var exports = new List<EffectSourceExport>(
            prepared.EffectOperationBatches.Count);
        var groups = new List<WoundSourceGroupAuthority>(
            prepared.EffectOperationBatches.Count);
        foreach (var batch in prepared.EffectOperationBatches)
        {
            var source = batch.SourceExport;
            if (!WoundEffectCarrierAdapter.TryCreateTargetKey(
                    source.Owner,
                    out var target))
            {
                issues.Add(WoundCompositionIssue(
                    "effect_source_wound_owner_target_invalid",
                    "The prepared wound owner does not map to one closed effect target.",
                    "closed wound owner-to-effect-target mapping",
                    source.Owner.ToString()));
                continue;
            }
            var definitions = source.Definitions
                .Select(static definition => new WoundEffectSourceDefinition(
                    definition.DefinitionKey,
                    definition.Definition))
                .ToArray();
            var applicationRoots = batch.RootLineageAuthority
                .Where(static row => row.ApplicationRef is not null)
                .ToArray();
            var existingRoots = batch.RootLineageAuthority
                .Where(static row => row.EffectId is not null)
                .ToArray();
            exports.Add(new EffectSourceExport(
                source.Realm,
                source.Kind,
                source.SourceId,
                new JsonArray(definitions.Select(static definition =>
                    (JsonNode)definition.Definition).ToArray()),
                Materializable: false,
                Active: string.Equals(source.State, "active", StringComparison.Ordinal),
                SameTurn: true,
                SourceRef: source.SourceRef,
                SatisfiedPredicates: new HashSet<string>(StringComparer.Ordinal)
                {
                    "active"
                }));
            groups.Add(new WoundSourceGroupAuthority(
                new EffectIdentitySourceGroup(
                    source.Realm,
                    source.Kind,
                    source.SourceId),
                source.Owner,
                target,
                sameTurn: true,
                source.SourceRef,
                batch.SourceExportFingerprint,
                definitions,
                applicationRoots,
                existingRoots));
        }

        if (issues.Count != 0)
        {
            return new CanonicalWoundSourceComposition(
                Array.Empty<EffectSourceExport>(),
                Array.Empty<WoundSourceGroupAuthority>(),
                issues);
        }
        return new CanonicalWoundSourceComposition(exports, groups, issues);
    }

    private static ValidationIssue WoundCompositionIssue(
        string code,
        string message,
        string expected,
        string actual) =>
        new(
            "effectAcceptedTurn.woundSourceComposition",
            IssueSeverity.Error,
            message,
            code: code,
            section: "wound_materialization",
            expected: expected,
            actual: actual,
            repairHint: "Recompose effect authority from the exact sealed prepared wound plan and canonical pre-turn wound carriers.");

    private static IReadOnlySet<string> BuildSatisfiedPredicates(
        string path,
        string kind,
        string sourceId,
        JsonObject owner,
        JsonNode? root,
        bool active)
    {
        var predicates = new HashSet<string>(StringComparer.Ordinal);
        if (active)
            predicates.Add("active");

        switch (kind)
        {
            case "item":
                if (MortalItemLocalActionPolicy.IsCarriedByPlayer(owner))
                    predicates.Add("carried");
                if (string.Equals(
                        path,
                        InventoryEquipmentService.ItemsPath,
                        StringComparison.Ordinal) &&
                    root is JsonObject inventoryRoot &&
                    MortalItemEquipmentAuthority.TryRead(
                        inventoryRoot,
                        inventoryRoot["items"] as JsonArray,
                        path,
                        out var equipment,
                        out _) &&
                    equipment.EquippedItemIds().Contains(sourceId))
                {
                    predicates.Add("equipped");
                }
                break;
            case "skill":
            case "spiritual_art":
            case "fate_card":
            case "combat_action":
                if (active)
                    predicates.Add("unlocked");
                break;
        }

        return predicates;
    }

    private static bool IsSourceCurrentlyActive(
        string path,
        string kind,
        JsonObject owner)
    {
        if (TryReadBoolean(owner["isInactive"], out var inactive) && inactive)
            return false;

        var status = ReadFirstExact(owner, "status", "state", "availability");
        switch (kind)
        {
            case "quest":
                return StatusIs(
                    status,
                    "Active",
                    "Updated",
                    "ready_to_turn_in");
            case "world_event":
                return TryReadBoolean(owner["isActive"], out var eventActive)
                    ? eventActive
                    : StatusIs(status, "Active", "ongoing", "current");
            case "item":
                return string.Equals(
                           path,
                           "game_state/inventory/items.json",
                           StringComparison.Ordinal) &&
                       MortalItemLocalActionPolicy.IsCarriedByPlayer(owner);
            case "location":
            case "hazard":
                return (string.Equals(
                            path,
                            MortalLocationMaterializationContract.CurrentLocationPath,
                            StringComparison.Ordinal) ||
                        string.Equals(
                            path,
                            MortalLocationMaterializationContract.WorldMapPath,
                            StringComparison.Ordinal)) &&
                       !IsTerminalSourceStatus(status);
            case "wound":
                return (!TryReadBoolean(owner["isHealed"], out var healed) || !healed) &&
                       !IsTerminalSourceStatus(status);
            case "fate_card":
                if (string.Equals(
                        path,
                        EffectCarrierCatalog.AfterlifeProfilesPath,
                        StringComparison.Ordinal))
                {
                    return StatusIs(status, "unlocked", "active");
                }
                return TryReadBoolean(owner["isUnlocked"], out var unlocked)
                    ? unlocked
                    : StatusIs(status, "unlocked", "active");
            case "skill":
            case "spiritual_art":
            case "faction":
            case "combat_action":
                return !IsTerminalSourceStatus(status);
            default:
                return false;
        }
    }

    private static bool IsTerminalSourceStatus(string? status) =>
        status != null && TerminalSourceStatuses.Contains(status);

    private static bool StatusIs(string? status, params string[] expected) =>
        status != null && expected.Contains(status, StringComparer.OrdinalIgnoreCase);

    private static bool TryResolveSourceDescriptor(
        SourceObjectCandidate candidate,
        out SourceDescriptor descriptor,
        out string sourceId)
    {
        var matchingDescriptors = candidate.Descriptors
            .Where(allowed =>
                TryReadExact(candidate.Owner[allowed.IdentityField], out _))
            .ToArray();
        descriptor = matchingDescriptors.Length == 1
            ? matchingDescriptors[0]
            : null!;
        if (descriptor != null &&
            TryReadExact(candidate.Owner[descriptor.IdentityField], out sourceId))
        {
            return true;
        }

        sourceId = string.Empty;
        if (!TryReadExact(candidate.Owner["initialId"], out var initialId))
            return false;
        var temporaryDescriptor = candidate.Descriptors.FirstOrDefault(allowed =>
            allowed.Kind is "quest" or "faction");
        if (temporaryDescriptor != null)
        {
            descriptor = temporaryDescriptor;
            sourceId = initialId;
            return true;
        }
        return false;
    }

    private static IReadOnlyList<EffectTargetExport> CollectTargets(
        EffectCarrierCatalogInput carriers,
        IReadOnlyDictionary<string, JsonNode?> sourceRoots,
        bool includeTemporaryRefs)
    {
        var result = new List<EffectTargetExport>
        {
            new("mortal_world", "player", "player_current", SameTurn: false)
        };

        CollectCombatants(carriers.EnemyCombatants, "enemiesData", result);
        CollectCombatants(carriers.AllyCombatants, "alliesData", result);
        CollectNpcTargets(sourceRoots, includeTemporaryRefs, result);
        CollectAfterlifeTargets(carriers.AfterlifeProfiles, result);
        CollectSpiritualConflictTargets(carriers.SpiritualConflict, result);
        return result;
    }

    private static void CollectSpiritualConflictTargets(
        JsonObject? root,
        List<EffectTargetExport> targets)
    {
        foreach (var side in AfterlifeSpiritualConflictState.CombatConditionTargetSides)
        {
            if (!AfterlifeSpiritualConflictState.TryResolveEffectTarget(
                    root,
                    side,
                    out var target))
            {
                continue;
            }

            targets.Add(new EffectTargetExport(
                target.Realm,
                target.Kind,
                target.TargetId,
                SameTurn: false));
        }
    }

    private static void CollectAfterlifeTargets(
        JsonObject? root,
        List<EffectTargetExport> targets)
    {
        if (root?[AfterlifeEntityProfileState.ProfilesProperty] is not JsonArray profiles)
            return;
        foreach (var profile in profiles.OfType<JsonObject>())
        {
            if (!AfterlifeEntityProfileState.TryResolveEffectTarget(
                    profile,
                    out var target))
                continue;
            var bindings = EnumerateAfterlifeProfileRealmBindings(profile).ToArray();
            if (bindings.Length == 0)
            {
                targets.Add(new EffectTargetExport(
                    target.Realm,
                    target.Kind,
                    target.TargetId,
                    SameTurn: false));
                continue;
            }

            foreach (var binding in bindings)
            {
                targets.Add(new EffectTargetExport(
                    binding.Realm,
                    target.Kind,
                    target.TargetId,
                    SameTurn: false));
            }
        }
    }

    private static void CollectNpcTargets(
        IReadOnlyDictionary<string, JsonNode?> sourceRoots,
        bool includeTemporaryRefs,
        List<EffectTargetExport> targets)
    {
        if (!sourceRoots.TryGetValue("game_state/npcs/npc_core.json", out var root) ||
            root is not JsonObject)
            return;
        foreach (var npc in EnumerateCanonicalNpcActors(root))
        {
            var temporaryRef = ReadFirstExact(npc, "npcRef", "initialId");
            var targetId = ReadFirstExact(npc, "NPCId", "npcId") ?? temporaryRef;
            if (targetId == null)
                continue;
            targets.Add(new EffectTargetExport(
                "mortal_world",
                "npc",
                targetId,
                SameTurn: false,
                TargetRef: includeTemporaryRefs ? temporaryRef : null));
        }
    }

    private static void CollectCombatants(
        JsonObject? root,
        string collection,
        List<EffectTargetExport> targets)
    {
        if (root?[collection] is not JsonArray combatants)
            return;
        foreach (var combatant in combatants.OfType<JsonObject>())
        {
            if (combatant["isGroup"] is JsonValue groupNode &&
                groupNode.TryGetValue<bool>(out var isGroup) &&
                isGroup)
            {
                foreach (var member in
                         (combatant["members"] as JsonArray)?.OfType<JsonObject>() ??
                         Enumerable.Empty<JsonObject>())
                {
                    CollectCombatTarget(member, targets);
                }
                continue;
            }
            CollectCombatTarget(combatant, targets);
        }
    }

    private static void CollectCombatTarget(
        JsonObject owner,
        List<EffectTargetExport> targets)
    {
        var hasCombatantId = TryReadExact(
            owner["combatantId"],
            out var combatantId);
        var hasMemberId = TryReadExact(
            owner["memberId"],
            out var memberId);
        if (hasCombatantId == hasMemberId)
            return;

        var targetId = hasMemberId ? memberId : combatantId;
        var boundNpcId = hasMemberId
            ? null
            : ReadFirstExact(owner, "NPCId");
        targets.Add(new EffectTargetExport(
            "mortal_world",
            "combatant",
            targetId,
            SameTurn: false,
            BoundNpcId: boundNpcId,
            BoundResourceOwnerKind: hasMemberId
                ? ResourceOwnerKind.CombatGroupMember
                : null));
    }

    private static IEnumerable<SourceObjectCandidate>
        EnumerateRegisteredSourceObjects(
            JsonNode? root,
            string path,
            string inheritedRealm)
    {
        switch (path)
        {
            case "game_state/player/skills_active.json":
                foreach (var owner in EnumerateArrayOwners(
                             root,
                             "activeSkillChanges",
                             "skills"))
                    yield return Candidate(owner, inheritedRealm, SkillSourceDescriptors);
                yield break;
            case "game_state/player/skills_passive.json":
                foreach (var owner in EnumerateArrayOwners(
                             root,
                             "passiveSkillChanges",
                             "skills"))
                    yield return Candidate(owner, inheritedRealm, SkillSourceDescriptors);
                yield break;
            case "game_state/inventory/items.json":
                foreach (var owner in EnumerateArrayOwners(root, "items"))
                    yield return Candidate(owner, inheritedRealm, ItemSourceDescriptors);
                yield break;
            case "game_state/quests/regular_quests.json":
            case "game_state/quests/soul_quests.json":
                foreach (var owner in EnumerateArrayOwners(root, "quests"))
                    yield return Candidate(owner, inheritedRealm, QuestSourceDescriptors);
                yield break;
            case MortalLocationMaterializationContract.WorldMapPath:
                foreach (var location in EnumerateArrayOwners(root, "locations"))
                {
                    var locationId = ReadFirstExact(location, "locationId");
                    yield return Candidate(
                        location,
                        inheritedRealm,
                        LocationSourceDescriptors,
                        locationId);
                    foreach (var hazard in EnumerateArrayOwners(
                                 location,
                                 "hazards",
                                 "activeHazards"))
                    {
                        yield return Candidate(
                            hazard,
                            inheritedRealm,
                            HazardSourceDescriptors,
                            locationId);
                    }
                }
                yield break;
            case MortalLocationMaterializationContract.CurrentLocationPath:
                var current = root is JsonObject currentRoot &&
                              currentRoot["currentLocationData"] is JsonObject wrappedCurrent
                    ? wrappedCurrent
                    : root as JsonObject;
                if (current != null)
                {
                    var locationId = ReadFirstExact(current, "locationId");
                    yield return Candidate(
                        current,
                        inheritedRealm,
                        LocationSourceDescriptors,
                        locationId);
                    foreach (var hazard in EnumerateArrayOwners(
                                 current,
                                 "hazards",
                                 "activeHazards"))
                    {
                        yield return Candidate(
                            hazard,
                            inheritedRealm,
                            HazardSourceDescriptors,
                            locationId);
                    }
                }
                yield break;
            case "game_state/world/world_events.json":
                foreach (var owner in EnumerateArrayOwners(
                             root,
                             "worldEventsLog"))
                    yield return Candidate(owner, inheritedRealm, WorldEventSourceDescriptors);
                yield break;
            case "game_state/factions/faction_core.json":
                foreach (var owner in EnumerateArrayOwners(
                             root,
                             "factions",
                             "factionDataChanges"))
                    yield return Candidate(owner, inheritedRealm, FactionSourceDescriptors);
                yield break;
            case "game_state/npcs/npc_core.json":
                foreach (var actor in EnumerateCanonicalNpcActors(root))
                {
                    foreach (var skill in EnumerateArrayOwners(
                                 actor,
                                 "activeSkills",
                                 "passiveSkills"))
                        yield return Candidate(skill, inheritedRealm, SkillSourceDescriptors);
                    foreach (var card in EnumerateArrayOwners(actor, "fateCards"))
                        yield return Candidate(card, inheritedRealm, FateCardSourceDescriptors);
                    foreach (var action in EnumerateArrayOwners(
                                 actor,
                                 "actions",
                                 "combatActions"))
                        yield return Candidate(action, inheritedRealm, CombatSourceDescriptors);
                }
                yield break;
            case EffectCarrierCatalog.EnemiesPath:
            case EffectCarrierCatalog.AlliesPath:
                foreach (var combatant in EnumerateArrayOwners(
                             root,
                             "enemiesData",
                             "alliesData"))
                {
                    foreach (var action in EnumerateArrayOwners(
                                 combatant,
                                 "actions",
                                 "combatActions"))
                        yield return Candidate(action, inheritedRealm, CombatSourceDescriptors);
                }
                yield break;
            case EffectCarrierCatalog.AfterlifeProfilesPath:
                foreach (var profile in EnumerateArrayOwners(root, "profiles"))
                {
                    var bindings = EnumerateAfterlifeProfileRealmBindings(profile)
                        .ToArray();
                    if (bindings.Length == 0)
                    {
                        var realm = ResolveObjectRealm(path, profile, inheritedRealm);
                        foreach (var art in EnumerateArrayOwners(
                                     profile,
                                     "specialArts"))
                            yield return Candidate(
                                art,
                                realm,
                                SpiritualArtSourceDescriptors);
                        foreach (var card in EnumerateArrayOwners(profile, "fateCards"))
                            yield return Candidate(
                                card,
                                realm,
                                AfterlifeFateCardSourceDescriptors);
                        continue;
                    }

                    foreach (var binding in bindings)
                    {
                        foreach (var art in EnumerateArrayOwners(
                                     profile,
                                     "specialArts"))
                        {
                            yield return Candidate(
                                art,
                                binding.Realm,
                                SpiritualArtSourceDescriptors,
                                activeOverride: binding.Active);
                        }
                        foreach (var card in EnumerateArrayOwners(profile, "fateCards"))
                        {
                            yield return Candidate(
                                card,
                                binding.Realm,
                                AfterlifeFateCardSourceDescriptors,
                                activeOverride: binding.Active);
                        }
                    }
                }
                yield break;
        }
    }

    private static IEnumerable<AfterlifeProfileRealmBinding>
        EnumerateAfterlifeProfileRealmBindings(JsonObject profile)
    {
        if (!TryReadExact(profile["actorId"], out var actorId) ||
            profile[AfterlifeEntityProfileState.ResourceOwnerBindingsProperty]
                is not JsonArray bindings)
        {
            yield break;
        }

        var seenRealms = new HashSet<string>(StringComparer.Ordinal);
        foreach (var binding in bindings.OfType<JsonObject>())
        {
            if (!TryReadExact(binding["realm"], out var declaredRealm) ||
                !AfterlifeEntityProfileState.TryNormalizeEffectRealm(
                    declaredRealm,
                    out var realm) ||
                !TryReadExact(binding["resourceOwnerId"], out var resourceOwnerId) ||
                !string.Equals(resourceOwnerId, actorId, StringComparison.Ordinal) ||
                !TryReadExact(binding["state"], out var state) ||
                state is not ("active" or "suspended") ||
                !seenRealms.Add(realm))
            {
                continue;
            }

            yield return new AfterlifeProfileRealmBinding(
                realm,
                string.Equals(state, "active", StringComparison.Ordinal));
        }
    }

    private static SourceObjectCandidate Candidate(
        JsonObject owner,
        string realm,
        IReadOnlyList<SourceDescriptor> descriptors,
        string? owningLocationId = null,
        bool? activeOverride = null) =>
        new(owner, realm, descriptors, owningLocationId, activeOverride);

    private static IEnumerable<JsonObject> EnumerateArrayOwners(
        JsonNode? root,
        params string[] properties)
    {
        if (root is JsonArray arrayRoot)
        {
            foreach (var owner in arrayRoot.OfType<JsonObject>())
                yield return owner;
            yield break;
        }
        if (root is not JsonObject objectRoot)
            yield break;
        foreach (var property in properties)
        {
            if (objectRoot[property] is not JsonArray owners)
                continue;
            foreach (var owner in owners.OfType<JsonObject>())
                yield return owner;
        }
    }

    internal static IEnumerable<JsonObject> EnumerateCanonicalNpcActors(JsonNode? root)
    {
        if (root is not JsonObject objectRoot)
            yield break;

        var actorsFromPriorSections = new List<(string NpcId, JsonObject Actor)>();
        foreach (var section in GuardianPolicyContracts.NpcCoreCanonicalNpcObjectSections)
        {
            if (objectRoot[section] is not JsonArray rows)
                continue;

            var actorsInSection = rows.OfType<JsonObject>().ToArray();
            foreach (var actor in actorsInSection)
            {
                if (GuardianPolicyContracts.TryResolveStrictPermanentNpcId(
                        actor,
                        out var npcId))
                {
                    var priorMirrors = actorsFromPriorSections
                        .Where(candidate => string.Equals(
                            candidate.NpcId,
                            npcId,
                            StringComparison.Ordinal))
                        .ToArray();
                    if (priorMirrors.Length != 0 &&
                        priorMirrors.All(candidate => JsonNode.DeepEquals(
                            candidate.Actor,
                            actor)))
                    {
                        continue;
                    }
                }

                yield return actor;
            }

            foreach (var actor in actorsInSection)
            {
                if (GuardianPolicyContracts.TryResolveStrictPermanentNpcId(
                        actor,
                        out var npcId))
                {
                    actorsFromPriorSections.Add((npcId, actor));
                }
            }
        }
    }

    private static string ResolveObjectRealm(
        string path,
        JsonObject owner,
        string inheritedRealm)
    {
        if (string.Equals(
                path,
                EffectCarrierCatalog.AfterlifeProfilesPath,
                StringComparison.Ordinal))
        {
            return TryReadExact(owner["actorType"], out _) &&
                   TryReadExact(owner["actorId"], out _) &&
                   owner["realm"] is JsonValue profileRealm &&
                   profileRealm.TryGetValue<string>(out var declaredProfileRealm) &&
                   AfterlifeEntityProfileState.TryNormalizeEffectRealm(
                       declaredProfileRealm,
                       out var normalizedProfileRealm)
                ? normalizedProfileRealm
                : inheritedRealm;
        }

        return TryReadExact(owner["realm"], out var declaredRealm)
            ? declaredRealm
            : inheritedRealm;
    }

    private static EffectCarrierCatalogInput CloneCarriers(EffectCarrierCatalogInput input) =>
        new(
            input.PlayerEffects?.DeepClone().AsObject(),
            input.NpcEffects?.DeepClone().AsObject(),
            input.EnemyCombatants?.DeepClone().AsObject(),
            input.AllyCombatants?.DeepClone().AsObject(),
            input.AfterlifeProfiles?.DeepClone().AsObject(),
            input.SpiritualConflict?.DeepClone().AsObject());

    private static bool TryReadExact(JsonNode? node, out string value)
    {
        value = node is JsonValue jsonValue && jsonValue.TryGetValue<string>(out var text)
            ? text
            : string.Empty;
        return value.Length > 0 && string.Equals(value, value.Trim(), StringComparison.Ordinal);
    }

    private static bool TryReadBoolean(JsonNode? node, out bool value)
    {
        value = false;
        return node is JsonValue jsonValue && jsonValue.TryGetValue(out value);
    }

    private static string? ReadFirstExact(JsonObject root, params string[] fields)
    {
        foreach (var field in fields)
        {
            if (TryReadExact(root[field], out var value))
                return value;
        }
        return null;
    }

    private static string InferRealm(string path) =>
        path.Contains("afterlife", StringComparison.OrdinalIgnoreCase) ||
        path.Contains("shining", StringComparison.OrdinalIgnoreCase)
            ? "shining_abode"
            : "mortal_world";

    private sealed record SourceDescriptor(string Kind, string IdentityField);

    private sealed record SourceObjectCandidate(
        JsonObject Owner,
        string Realm,
        IReadOnlyList<SourceDescriptor> Descriptors,
        string? OwningLocationId,
        bool? ActiveOverride);

    private sealed record AfterlifeProfileRealmBinding(
        string Realm,
        bool Active);

    private sealed record CanonicalWoundSourceComposition(
        IReadOnlyList<EffectSourceExport> Exports,
        IReadOnlyList<WoundSourceGroupAuthority> Groups,
        IReadOnlyList<ValidationIssue> Issues)
    {
        internal static CanonicalWoundSourceComposition Empty { get; } = new(
            Array.Empty<EffectSourceExport>(),
            Array.Empty<WoundSourceGroupAuthority>(),
            Array.Empty<ValidationIssue>());
    }
}

internal sealed record EffectAcceptedOwnerExports(
    IReadOnlyList<EffectSourceExport> Sources,
    IReadOnlyList<EffectTargetExport> Targets,
    IReadOnlySet<EffectSourceOwnerKey> ReplacedSourceOwners,
    IReadOnlySet<EffectTargetKey> ReplacedTargets)
{
    internal static EffectAcceptedOwnerExports Empty { get; } =
        new(
            Array.Empty<EffectSourceExport>(),
            Array.Empty<EffectTargetExport>(),
            new HashSet<EffectSourceOwnerKey>(),
            new HashSet<EffectTargetKey>());
}

internal sealed record EffectAcceptedCombatMemberTargetExports(
    IReadOnlyList<EffectTargetExport> Targets,
    IReadOnlySet<EffectTargetKey> ReplacedTargets);

internal sealed record EffectSourceOwnerKey(
    string Realm,
    string Kind,
    string SourceId);
