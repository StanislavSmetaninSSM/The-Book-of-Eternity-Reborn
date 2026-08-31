using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;
using BookOfEternityClient.Core;

namespace BookOfEternityClient.Services;

internal sealed record MortalWoundTreatmentAcceptedCanonicalProjectionResult(
    IReadOnlyList<MortalWoundTreatmentAuthority.Item> Items,
    IReadOnlyList<MortalWoundTreatmentAuthority.Resource> Resources,
    IReadOnlyList<MortalWoundTreatmentAuthority.Actor> Actors,
    IReadOnlyList<MortalWoundTreatmentAuthority.Facility> Facilities,
    IReadOnlyList<MortalWoundTreatmentAuthority.Location> Locations,
    IReadOnlyList<MortalWoundTreatmentAuthority.Quest> Quests,
    IReadOnlyList<MortalWoundTreatmentAuthority.EnvironmentState> Environments,
    IReadOnlyList<ValidationIssue> Issues);

internal static class MortalWoundTreatmentAcceptedCanonicalProjection
{
    private const string CurrentLocationPath = "game_state/world/current_location.json";
    private const string PlayerInventoryPath = "game_state/inventory/items.json";
    private const string ItemIdentityPath = "game_state/inventory/item_identity_index.json";
    private const string NpcCorePath = "game_state/npcs/npc_core.json";
    private const string PlayerActiveSkillsPath = "game_state/player/skills_active.json";
    private const string PlayerPassiveSkillsPath = "game_state/player/skills_passive.json";
    private const string PlayerSkillMasteryPath = "game_state/player/skill_mastery.json";
    private const string RegularQuestsPath = "game_state/quests/regular_quests.json";

    private static readonly string[] ItemCompanionPaths =
    {
        "game_state/inventory/item_bonds.json",
        "game_state/inventory/item_text_updates.json",
        "game_state/inventory/recipes.json",
        "game_state/npcs/item_journals.json",
        "game_state/quests/quest_history.json"
    };

    internal static MortalWoundTreatmentAcceptedCanonicalProjectionResult Compose(
        MortalWoundTreatmentAuthority.Context context,
        PendingTurnSnapshotReadAuthority signed,
        IReadOnlyDictionary<string, JsonObject> roots,
        IReadOnlyList<MortalWoundTreatmentCapabilitySkillSource> playerCapabilities,
        IReadOnlyList<MortalWoundTreatmentCapabilitySkillSource> npcCapabilities)
    {
        var issues = new List<ValidationIssue>();
        var resources = ComposeResources(signed, issues);
        var items = ComposeItems(roots, signed, issues);
        var location = ValidateLocation(context, roots, issues);
        var actors = ComposeActors(
            context,
            roots,
            location,
            playerCapabilities,
            npcCapabilities,
            issues);
        var scene = ParseScene(location, issues);
        AttachConsents(scene, actors, location, issues);
        var locations = ComposeLocations(location, actors);
        ValidateSelectedCoordinates(context, actors, locations, issues);
        var quests = ComposeQuests(roots, issues);
        var facilities = scene?.Facilities.Select(row =>
                new MortalWoundTreatmentAuthority.Facility(
                    row.FacilityId,
                    row.DisplayName,
                    "mortal_world",
                    context.CurrentLocationId,
                    "active",
                    true,
                    row.Available))
            .ToArray() ?? Array.Empty<MortalWoundTreatmentAuthority.Facility>();
        var environments = scene?.Environments.Select(row =>
                new MortalWoundTreatmentAuthority.EnvironmentState(
                    row.EnvironmentId,
                    row.DisplayName,
                    "mortal_world",
                    context.CurrentLocationId,
                    row.State,
                    "active",
                    true))
            .ToArray() ?? Array.Empty<MortalWoundTreatmentAuthority.EnvironmentState>();
        return new(
            items,
            resources,
            actors,
            facilities,
            locations,
            quests,
            environments,
            Array.AsReadOnly(issues.ToArray()));
    }

    private static IReadOnlyList<MortalWoundTreatmentAuthority.Resource> ComposeResources(
        PendingTurnSnapshotReadAuthority signed,
        ICollection<ValidationIssue> issues)
    {
        var definitionsResult = ResourceDefinitionCatalog.ParseCanonical(
            Read(signed, ResourceMaterializationContract.DefinitionsPath),
            allowMissingPristine: false);
        Add(issues, definitionsResult.Issues);
        if (definitionsResult.Catalog is not { } definitions)
            return Array.Empty<MortalWoundTreatmentAuthority.Resource>();
        var stateResult = ResourceStateContract.ParseCanonical(
            Read(signed, ResourceMaterializationContract.StatePath),
            definitions,
            allowMissingPristine: false);
        var historyResult = ResourceHistoryState.ParseCanonical(
            Read(signed, ResourceMaterializationContract.HistoryPath),
            definitions,
            allowMissingPristine: false);
        Add(issues, stateResult.Issues);
        Add(issues, historyResult.Issues);
        if (stateResult.Ledger is not { } state || historyResult.History is not { } history)
            return Array.Empty<MortalWoundTreatmentAuthority.Resource>();
        Add(issues, history.ValidateStateAgreement(state));
        var ownerResult = CanonicalResourceOwnerAuthorityComposer.ComposeAsync(
                definitions,
                path => Task.FromResult(ReadOptional(signed, path)),
                state,
                history,
                CanonicalResourceOwnerAuthorityPurpose.ExistingSessionValidation)
            .GetAwaiter()
            .GetResult();
        Add(issues, ownerResult.Issues);
        if (!ownerResult.IsValid || ownerResult.Authority is null)
            return Array.Empty<MortalWoundTreatmentAuthority.Resource>();
        Add(issues, ownerResult.Authority.ValidateCanonicalAgreement(state, history));

        return ProjectResources(definitions, state);
    }

    internal static IReadOnlyList<MortalWoundTreatmentAuthority.Resource> ProjectResources(
        ResourceDefinitionCatalog definitions,
        ResourceStateLedger state)
    {
        ArgumentNullException.ThrowIfNull(definitions);
        ArgumentNullException.ThrowIfNull(state);

        var result = new List<MortalWoundTreatmentAuthority.Resource>();
        foreach (var entry in state.Entries)
        {
            if (!string.Equals(entry.Coordinate.Realm, "mortal_world", StringComparison.Ordinal) ||
                !definitions.TryResolveExact(entry.Coordinate.ResourceKey, out var definition) ||
                definition is null || definition.NumericKind != ResourceNumericKind.Integer ||
                entry.Current != decimal.Truncate(entry.Current) ||
                entry.Current < 0 || entry.Current > int.MaxValue)
                continue;
            var ownerKind = entry.Coordinate.OwnerKind switch
            {
                ResourceOwnerKind.Player => "player",
                ResourceOwnerKind.Npc => "npc",
                ResourceOwnerKind.Combatant => "combatant",
                ResourceOwnerKind.CombatGroupMember => "combatant_member",
                _ => null
            };
            if (ownerKind is null)
                continue;
            var current = decimal.ToInt32(entry.Current);
            var active = entry.State == ResourceLifecycleState.Active;
            result.Add(new MortalWoundTreatmentAuthority.Resource(
                entry.Coordinate.ResourceKey,
                definition.DisplayName,
                "mortal_world",
                ownerKind,
                entry.Coordinate.ResourceOwnerId,
                current,
                active ? current : 0,
                "available",
                "active",
                active));
        }
        return result;
    }

    private static IReadOnlyList<MortalWoundTreatmentAuthority.Item> ComposeItems(
        IReadOnlyDictionary<string, JsonObject> roots,
        PendingTurnSnapshotReadAuthority signed,
        ICollection<ValidationIssue> issues)
    {
        var companions = ItemCompanionPaths
            .Where(roots.ContainsKey)
            .ToDictionary(path => path, path => roots[path], StringComparer.Ordinal);
        var catalog = MortalItemCarrierCatalog.Build(new MortalItemCarrierCatalogInput(
            Get(roots, PlayerInventoryPath),
            Get(roots, NpcCorePath),
            NpcInventoryCommands: null,
            Get(roots, CurrentLocationPath),
            Get(roots, StorageTransportMoveService.VehiclesPath),
            companions,
            Get(roots, MortalLocationStorageContentsState.StatePath)));
        foreach (var issue in catalog.Issues)
            issues.Add(Issue(issue.Path, issue.Code, "one exact full Mortal item carrier catalog", issue.Message));

        var identity = MortalItemIdentityState.Parse(Read(signed, ItemIdentityPath));
        Add(issues, identity.Issues);
        ValidateItemAgreement(catalog, identity, issues);
        if (issues.Count != 0)
            return Array.Empty<MortalWoundTreatmentAuthority.Item>();

        var result = new List<MortalWoundTreatmentAuthority.Item>();
        foreach (var occurrence in catalog.Occurrences)
        {
            if (occurrence.ItemId is not { } itemId ||
                occurrence.Carrier.Kind is not ("player_inventory" or "npc_inventory"))
                continue;
            if (!TryInt(occurrence.Item["count"], out var count) || count <= 0)
                continue;
            var player = occurrence.Carrier.Kind == "player_inventory";
            result.Add(new MortalWoundTreatmentAuthority.Item(
                itemId,
                Text(occurrence.Item, "displayName") ?? Text(occurrence.Item, "name") ?? itemId,
                "mortal_world",
                player ? "player" : "npc",
                player ? "player_current" : occurrence.Carrier.OwnerId,
                count,
                count,
                "available",
                "active",
                true));
        }
        return result;
    }

    private static void ValidateItemAgreement(
        MortalItemCarrierCatalog catalog,
        MortalItemIdentityParseResult identity,
        ICollection<ValidationIssue> issues)
    {
        foreach (var occurrence in catalog.Occurrences)
        {
            using (var itemDocument = JsonDocument.Parse(occurrence.Item.ToJsonString()))
            {
                Add(issues, MortalItemMaterializationContract.Validate(
                    itemDocument.RootElement,
                    occurrence.JsonPath,
                    MortalItemMaterializationPhase.CanonicalPostSeal));
            }
            if (occurrence.ItemId is not { } itemId ||
                !identity.EntriesByItemId.TryGetValue(itemId, out var entry) ||
                !string.Equals(Text(entry, "state"), "active", StringComparison.Ordinal) ||
                !CarrierAgrees(entry["currentCarrier"] as JsonObject, occurrence.Carrier) ||
                !string.Equals(Text(entry, "receiptId"), occurrence.ReceiptId, StringComparison.Ordinal) ||
                (occurrence.MaterializationId is not null &&
                 !ContainsExact(entry["originMaterializationIds"], occurrence.MaterializationId)) ||
                (occurrence.CreationRef is not null &&
                 !ContainsExact(entry["originCreationRefs"], occurrence.CreationRef)) ||
                !TryInt(occurrence.Item["count"], out var count) || count <= 0 ||
                entry["transitions"] is not JsonArray { Count: > 0 } transitions ||
                transitions[^1] is not JsonObject last ||
                !TryInt(last["quantityAfter"], out var recorded) || recorded != count)
            {
                issues.Add(Issue(
                    occurrence.JsonPath,
                    "mortal_wound_treatment_accepted_state_item_identity_mismatch",
                    "exact active identity/current-carrier/latest-quantity agreement",
                    occurrence.ItemId ?? "missing"));
            }
        }
        foreach (var pair in identity.EntriesByItemId)
        {
            var state = Text(pair.Value, "state");
            var occurrenceCount = catalog.Occurrences.Count(row =>
                string.Equals(row.ItemId, pair.Key, StringComparison.Ordinal));
            if ((string.Equals(state, "active", StringComparison.Ordinal) && occurrenceCount != 1) ||
                (!string.Equals(state, "active", StringComparison.Ordinal) && occurrenceCount != 0))
            {
                issues.Add(Issue(
                    ItemIdentityPath,
                    "mortal_wound_treatment_accepted_state_item_identity_mismatch",
                    string.Equals(state, "active", StringComparison.Ordinal)
                        ? "one exact current occurrence for every active identity"
                        : "zero current occurrences for every retired identity",
                    $"{pair.Key}: state={state ?? "missing"}; occurrences={occurrenceCount}"));
            }
        }
    }

    private static JsonObject? ValidateLocation(
        MortalWoundTreatmentAuthority.Context context,
        IReadOnlyDictionary<string, JsonObject> roots,
        ICollection<ValidationIssue> issues)
    {
        var current = Get(roots, CurrentLocationPath);
        var map = Get(roots, MortalLocationMaterializationContract.WorldMapPath);
        var indexRoot = Get(roots, MortalLocationIdentityState.StatePath);
        if (current is null || map is null || indexRoot is null)
        {
            issues.Add(Issue(CurrentLocationPath, "mortal_wound_treatment_accepted_state_location_invalid", "complete current/map/index roots", "missing"));
            return null;
        }
        using (var currentDocument = JsonDocument.Parse(current.ToJsonString()))
            Add(issues, MortalLocationMaterializationContract.ValidateCanonicalCurrentLocation(currentDocument.RootElement, CurrentLocationPath));
        var identity = MortalLocationIdentityState.Parse(indexRoot);
        Add(issues, identity.Issues);
        Add(issues, identity.ValidateCanonicalState(map));
        var catalog = MortalLocationPlayerProjection.Create(map, current, indexRoot);
        if (!string.Equals(catalog.CurrentLocationId, context.CurrentLocationId, StringComparison.Ordinal))
        {
            issues.Add(Issue(
                CurrentLocationPath,
                "mortal_wound_treatment_accepted_state_location_invalid",
                "exact current location agreed with world_map and location identity",
                catalog.CurrentLocationId ?? "unresolved"));
        }
        return current;
    }

    private static List<MortalWoundTreatmentAuthority.Actor> ComposeActors(
        MortalWoundTreatmentAuthority.Context context,
        IReadOnlyDictionary<string, JsonObject> roots,
        JsonObject? currentLocation,
        IReadOnlyList<MortalWoundTreatmentCapabilitySkillSource> playerCapabilities,
        IReadOnlyList<MortalWoundTreatmentCapabilitySkillSource> npcCapabilities,
        ICollection<ValidationIssue> issues)
    {
        var actors = new List<MortalWoundTreatmentAuthority.Actor>();
        actors.Add(new MortalWoundTreatmentAuthority.Actor(
            "player", "player_current", "Player", "mortal_world", context.CurrentLocationId,
            "active", true, true,
            ComposePlayerSkills(roots, playerCapabilities, issues),
            Capabilities(playerCapabilities),
            Array.Empty<MortalWoundTreatmentAuthority.Consent>()));

        var npcRoot = Get(roots, NpcCorePath);
        if (npcRoot is null)
        {
            issues.Add(Issue(NpcCorePath, "mortal_wound_treatment_accepted_state_actor_invalid", "canonical NPC root", "missing"));
        }
        else
        {
            var rowsByNpc = new Dictionary<string, List<(string Section, int Index, JsonObject Npc, string Location)>>(StringComparer.Ordinal);
            var confusableIds = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (var section in GuardianPolicyContracts.NpcCoreCanonicalNpcObjectSections)
            {
                if (npcRoot[section] is null)
                    continue;
                if (npcRoot[section] is not JsonArray rows)
                {
                    issues.Add(Issue(NpcCorePath + "." + section, "mortal_wound_treatment_accepted_state_collection_invalid", "canonical NPC array", Describe(npcRoot[section])));
                    continue;
                }
                for (var index = 0; index < rows.Count; index++)
                {
                    if (rows[index] is not JsonObject npc ||
                        !GuardianPolicyContracts.TryResolveStrictPermanentNpcId(npc, out var npcId) ||
                        !TryIdentifier(npc, "currentLocationId", out var npcLocation))
                    {
                        issues.Add(Issue(NpcCorePath + $".{section}[{index}]", "mortal_wound_treatment_accepted_state_actor_invalid", "one exact/confusable-unique located NPC", Describe(rows[index])));
                        continue;
                    }
                    var confusable = MortalLocationIdentityState.BuildConfusableKey(npcId);
                    if (confusableIds.TryGetValue(confusable, out var previousId) &&
                        !string.Equals(previousId, npcId, StringComparison.Ordinal))
                    {
                        issues.Add(Issue(NpcCorePath + $".{section}[{index}]", "mortal_wound_treatment_accepted_state_actor_invalid", "confusable-unique permanent NPC identity", npcId));
                        continue;
                    }
                    confusableIds[confusable] = npcId;
                    if (!rowsByNpc.TryGetValue(npcId, out var copies))
                        rowsByNpc[npcId] = copies = new();
                    copies.Add((section, index, npc, npcLocation));
                }
            }
            foreach (var (npcId, copies) in rowsByNpc)
            {
                if (copies.GroupBy(copy => copy.Section, StringComparer.Ordinal).Any(group => group.Count() != 1) ||
                    copies.Skip(1).Any(copy => !JsonNode.DeepEquals(copies[0].Npc, copy.Npc)))
                {
                    issues.Add(Issue(NpcCorePath, "mortal_wound_treatment_accepted_state_actor_invalid", "at most one exact row per carrier and semantically identical cross-carrier mirrors", npcId));
                    continue;
                }
                var selected = copies.FirstOrDefault(copy =>
                    string.Equals(copy.Section, GuardianPolicyContracts.NpcCoreSceneSectionName, StringComparison.Ordinal));
                if (selected.Npc is null)
                    selected = copies[0];
                var inScene = copies.Any(copy =>
                    string.Equals(copy.Section, GuardianPolicyContracts.NpcCoreSceneSectionName, StringComparison.Ordinal));
                if (inScene && !string.Equals(selected.Location, context.CurrentLocationId, StringComparison.Ordinal))
                {
                    issues.Add(Issue(NpcCorePath, "mortal_wound_treatment_accepted_state_presence_invalid", context.CurrentLocationId, selected.Location));
                    continue;
                }
                var sources = npcCapabilities.Where(source =>
                    string.Equals(source.OwnerId, npcId, StringComparison.Ordinal)).ToArray();
                actors.Add(new MortalWoundTreatmentAuthority.Actor(
                    "npc", npcId, Text(selected.Npc, "displayName") ?? Text(selected.Npc, "name") ?? npcId,
                    "mortal_world", selected.Location, "active", true, inScene,
                    ComposeNpcSkills(selected.Npc, sources, issues),
                    Capabilities(sources),
                    Array.Empty<MortalWoundTreatmentAuthority.Consent>()));
            }
        }
        ComposeCombatActors(roots, context.CurrentLocationId, actors, issues);
        return actors;
    }

    private static IReadOnlyList<MortalWoundTreatmentAuthority.Skill> ComposePlayerSkills(
        IReadOnlyDictionary<string, JsonObject> roots,
        IReadOnlyList<MortalWoundTreatmentCapabilitySkillSource> sources,
        ICollection<ValidationIssue> issues)
    {
        var mastery = new Dictionary<string, int>(StringComparer.Ordinal);
        if (Get(roots, PlayerSkillMasteryPath)?["skillMasteryChanges"] is not JsonArray masteryRows)
        {
            issues.Add(Issue(PlayerSkillMasteryPath, "mortal_wound_treatment_accepted_state_collection_invalid", "canonical skillMasteryChanges array", "missing"));
        }
        else
        {
            foreach (var row in masteryRows)
            {
                if (row is not JsonObject value || !TryIdentifier(value, "skillName", out var name) ||
                    !TryPositiveInt(value["newMasteryLevel"], out var level) ||
                    !mastery.TryAdd(name, level))
                    issues.Add(Issue(PlayerSkillMasteryPath, "mortal_wound_treatment_accepted_state_skill_invalid", "exact unique mastery row", Describe(row)));
            }
        }
        return ComposeSkillRows(
            Get(roots, PlayerActiveSkillsPath), "activeSkillChanges", "active", sources,
            skill => Text(skill, "skillName") is { } name && mastery.TryGetValue(name, out var tier) ? tier : null,
            issues).Concat(ComposeSkillRows(
            Get(roots, PlayerPassiveSkillsPath), "passiveSkillChanges", "passive", sources,
            skill => TryPositiveInt(skill["masteryLevel"], out var tier) ? tier : null,
            issues)).ToArray();
    }

    private static IReadOnlyList<MortalWoundTreatmentAuthority.Skill> ComposeNpcSkills(
        JsonObject npc,
        IReadOnlyList<MortalWoundTreatmentCapabilitySkillSource> sources,
        ICollection<ValidationIssue> issues) =>
        ComposeSkillRows(npc, "activeSkills", "active", sources,
            skill => TryPositiveInt(skill["currentMasteryLevel"], out var tier) ? tier : null,
            issues).Concat(ComposeSkillRows(npc, "passiveSkills", "passive", sources,
            skill => TryPositiveInt(skill["masteryLevel"], out var tier) ? tier : null,
            issues)).ToArray();

    private static IEnumerable<MortalWoundTreatmentAuthority.Skill> ComposeSkillRows(
        JsonObject? root,
        string field,
        string kind,
        IReadOnlyList<MortalWoundTreatmentCapabilitySkillSource> sources,
        Func<JsonObject, int?> tierSelector,
        ICollection<ValidationIssue> issues)
    {
        if (root?[field] is null)
            yield break;
        if (root[field] is not JsonArray rows)
        {
            issues.Add(Issue(field, "mortal_wound_treatment_accepted_state_collection_invalid", "canonical skill array", Describe(root[field])));
            yield break;
        }
        foreach (var row in rows)
        {
            if (row is not JsonObject skill)
            {
                issues.Add(Issue(field, "mortal_wound_treatment_accepted_state_skill_invalid", "canonical skill object", Describe(row)));
                continue;
            }
            if (!TryIdentifier(skill, "skillId", out var skillId))
            {
                if (skill.ContainsKey("skillId"))
                    issues.Add(Issue(field, "mortal_wound_treatment_accepted_state_skill_invalid", "absent or exact skillId", Describe(row)));
                continue;
            }
            var tier = tierSelector(skill);
            if (tier is null)
                continue;
            var display = Text(skill, "displayName") ?? Text(skill, "skillName") ?? skillId;
            yield return new MortalWoundTreatmentAuthority.Skill(skillId, display, tier.Value, "active", true);
            foreach (var source in sources.Where(source =>
                         string.Equals(source.SkillKind, kind, StringComparison.Ordinal) &&
                         string.Equals(source.SkillId, skillId, StringComparison.Ordinal)))
            foreach (var capability in source.Capabilities)
                yield return new MortalWoundTreatmentAuthority.Skill(capability.CapabilityRef, display, tier.Value, "active", true);
        }
    }

    private static IReadOnlyList<MortalWoundTreatmentAuthority.Capability> Capabilities(
        IReadOnlyList<MortalWoundTreatmentCapabilitySkillSource> sources) =>
        sources.SelectMany(source => source.Capabilities.Select(capability =>
            new MortalWoundTreatmentAuthority.Capability(
                capability.CapabilityRef, source.DisplayName, "active", true))).ToArray();

    private static void ComposeCombatActors(
        IReadOnlyDictionary<string, JsonObject> roots,
        string currentLocationId,
        ICollection<MortalWoundTreatmentAuthority.Actor> actors,
        ICollection<ValidationIssue> issues)
    {
        var combatants = new JsonArray();
        var members = new JsonArray();
        foreach (var (path, field) in new[]
                 {
                     (EffectCarrierCatalog.EnemiesPath, "enemiesData"),
                     (EffectCarrierCatalog.AlliesPath, "alliesData")
                 })
        {
            if (!roots.TryGetValue(path, out var root))
                continue;
            if (root[field] is not JsonArray rows)
            {
                issues.Add(Issue(path + "." + field, "mortal_wound_treatment_accepted_state_collection_invalid", "canonical combat array", Describe(root[field])));
                continue;
            }
            foreach (var row in rows)
            {
                if (row is not JsonObject actor)
                {
                    combatants.Add(row?.DeepClone());
                    continue;
                }
                if (actor["isGroup"]?.GetValue<bool>() == true)
                {
                    if (actor["members"] is not JsonArray groupMembers)
                    {
                        issues.Add(Issue(path, "mortal_wound_treatment_accepted_state_actor_invalid", "group members array", Describe(actor)));
                        continue;
                    }
                    foreach (var member in groupMembers)
                        members.Add(member?.DeepClone());
                }
                else if (TryIdentifier(actor, "memberId", out _))
                {
                    members.Add(actor.DeepClone());
                }
                else
                {
                    combatants.Add(actor.DeepClone());
                }
            }
        }
        Add(issues, CombatantIdentityState.ValidateCanonical(combatants, members));
        foreach (var actor in combatants.OfType<JsonObject>())
        {
            if (!TryIdentifier(actor, "combatantId", out var id))
                continue;
            actors.Add(CombatActor("combatant", id, Text(actor, "displayName") ?? Text(actor, "name") ?? id, currentLocationId));
        }
        foreach (var actor in members.OfType<JsonObject>())
        {
            if (!TryIdentifier(actor, "memberId", out var id))
                continue;
            actors.Add(CombatActor("combatant_member", id, Text(actor, "displayName") ?? Text(actor, "name") ?? id, currentLocationId));
        }
    }

    private static MortalWoundTreatmentAuthority.Actor CombatActor(
        string kind,
        string id,
        string display,
        string location) => new(
            kind, id, display, "mortal_world", location, "active", true, true,
            Array.Empty<MortalWoundTreatmentAuthority.Skill>(),
            Array.Empty<MortalWoundTreatmentAuthority.Capability>(),
            Array.Empty<MortalWoundTreatmentAuthority.Consent>());

    private static MortalWoundTreatmentSceneAuthorityResult? ParseScene(
        JsonObject? location,
        ICollection<ValidationIssue> issues)
    {
        if (location?["customStates"] is not JsonArray states)
            return null;
        using var document = JsonDocument.Parse(states.ToJsonString());
        var scene = MortalWoundTreatmentSceneAuthorityContract.Parse(document.RootElement, CurrentLocationPath + ".customStates");
        Add(issues, scene.Issues);
        return scene;
    }

    private static void AttachConsents(
        MortalWoundTreatmentSceneAuthorityResult? scene,
        List<MortalWoundTreatmentAuthority.Actor> actors,
        JsonObject? location,
        ICollection<ValidationIssue> issues)
    {
        if (scene is null)
            return;
        foreach (var row in scene.Consents)
        {
            var providerIndexes = actors.Select((actor, index) => (actor, index)).Where(pair =>
                string.Equals(pair.actor.ActorKind, row.ProviderKind, StringComparison.Ordinal) &&
                string.Equals(pair.actor.ActorId, row.ProviderId, StringComparison.Ordinal)).ToArray();
            var targets = actors.Where(actor =>
                string.Equals(actor.ActorKind, row.TargetKind, StringComparison.Ordinal) &&
                string.Equals(actor.ActorId, row.TargetId, StringComparison.Ordinal)).ToArray();
            if (providerIndexes.Length != 1 || targets.Length != 1 ||
                !providerIndexes[0].actor.Reachable || !targets[0].Reachable)
            {
                issues.Add(Issue(CurrentLocationPath + ".customStates", "mortal_wound_treatment_accepted_state_consent_invalid", "exact co-present consent actors", row.ConsentRef));
                continue;
            }
            var provider = providerIndexes[0];
            actors[provider.index] = provider.actor with
            {
                Consents = provider.actor.Consents.Concat(new[]
                {
                    new MortalWoundTreatmentAuthority.Consent(
                        row.ConsentRef, row.DisplayName, row.ProviderKind, row.ProviderId,
                        row.TargetKind, row.TargetId, row.Status, "active", true)
                }).ToArray()
            };
        }
    }

    private static IReadOnlyList<MortalWoundTreatmentAuthority.Location> ComposeLocations(
        JsonObject? location,
        IReadOnlyList<MortalWoundTreatmentAuthority.Actor> actors)
    {
        if (location is null || !TryIdentifier(location, "locationId", out var locationId))
            return Array.Empty<MortalWoundTreatmentAuthority.Location>();
        var present = actors.Where(actor => actor.Reachable &&
                                           string.Equals(actor.CurrentLocationId, locationId, StringComparison.Ordinal))
            .Select(actor => new MortalWoundTreatmentAuthority.ActorCoordinate(actor.ActorKind, actor.ActorId))
            .ToArray();
        return new[]
        {
            new MortalWoundTreatmentAuthority.Location(
                locationId, Text(location, "displayName") ?? Text(location, "name") ?? locationId,
                "mortal_world", "active", true, present)
        };
    }

    private static void ValidateSelectedCoordinates(
        MortalWoundTreatmentAuthority.Context context,
        IReadOnlyList<MortalWoundTreatmentAuthority.Actor> actors,
        IReadOnlyList<MortalWoundTreatmentAuthority.Location> locations,
        ICollection<ValidationIssue> issues)
    {
        foreach (var coordinate in new[]
                 {
                     (context.TargetKind, context.TargetId),
                     (context.ProviderKind, context.ProviderId)
                 })
        {
            if (actors.Count(actor => string.Equals(actor.ActorKind, coordinate.Item1, StringComparison.Ordinal) &&
                                      string.Equals(actor.ActorId, coordinate.Item2, StringComparison.Ordinal) &&
                                      string.Equals(actor.Realm, context.Realm, StringComparison.Ordinal)) != 1)
                issues.Add(Issue(context.SourcePath, "mortal_wound_treatment_accepted_state_actor_ambiguous", "one exact current same-realm actor", $"{coordinate.Item1}/{coordinate.Item2}"));
        }
        if (locations.Count(location => string.Equals(location.LocationId, context.CurrentLocationId, StringComparison.Ordinal)) != 1)
            issues.Add(Issue(context.SourcePath, "mortal_wound_treatment_accepted_state_location_ambiguous", "one exact current location", context.CurrentLocationId));
    }

    private static IReadOnlyList<MortalWoundTreatmentAuthority.Quest> ComposeQuests(
        IReadOnlyDictionary<string, JsonObject> roots,
        ICollection<ValidationIssue> issues)
    {
        if (!roots.TryGetValue(RegularQuestsPath, out var root))
            return Array.Empty<MortalWoundTreatmentAuthority.Quest>();
        if (root["quests"] is not JsonArray rows)
        {
            issues.Add(Issue(RegularQuestsPath, "mortal_wound_treatment_accepted_state_collection_invalid", "canonical quests array", Describe(root["quests"])));
            return Array.Empty<MortalWoundTreatmentAuthority.Quest>();
        }
        var result = new List<MortalWoundTreatmentAuthority.Quest>();
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var row in rows)
        {
            if (row is not JsonObject quest ||
                !TryIdentifier(quest, "questId", out var questId) ||
                !TryIdentifier(quest, "questName", out var questName) ||
                !TryIdentifier(quest, "status", out var status) ||
                !seen.Add(MortalLocationIdentityState.BuildConfusableKey(questId)))
            {
                issues.Add(Issue(RegularQuestsPath, "mortal_wound_treatment_accepted_state_quest_invalid", "exact unique regular questId/questName/status", Describe(row)));
                continue;
            }
            result.Add(new MortalWoundTreatmentAuthority.Quest(
                questId, questName, "mortal_world", status, "active", true));
        }
        return result;
    }

    private static bool CarrierAgrees(JsonObject? carrier, MortalItemCarrierCoordinate expected)
    {
        if (carrier is null || !string.Equals(Text(carrier, "kind"), expected.Kind, StringComparison.Ordinal) ||
            !string.Equals(Text(carrier, "ownerId"), expected.OwnerId, StringComparison.Ordinal) ||
            !string.Equals(Text(carrier, "containerId"), expected.ContainerId, StringComparison.Ordinal) ||
            carrier["containerPath"] is not JsonArray path || path.Count != expected.ContainerPath.Count)
            return false;
        return path.Select(Text).SequenceEqual(expected.ContainerPath, StringComparer.Ordinal);
    }

    private static bool ContainsExact(JsonNode? node, string? expected) =>
        expected is not null && node is JsonArray array &&
        array.Any(value => string.Equals(Text(value), expected, StringComparison.Ordinal));

    private static string? ReadOptional(PendingTurnSnapshotReadAuthority signed, string path) =>
        signed.CoveredLogicalPaths.Contains(path, StringComparer.Ordinal) ? Read(signed, path) : null;

    private static string Read(PendingTurnSnapshotReadAuthority signed, string path)
        => CanonicalJsonUtf8.DecodeOneOptionalBom(
            signed.ReadRequiredBytes(path));

    private static JsonObject? Get(IReadOnlyDictionary<string, JsonObject> roots, string path) =>
        roots.TryGetValue(path, out var value) ? value : null;

    private static bool TryIdentifier(JsonObject value, string field, out string result)
    {
        result = Text(value, field) ?? string.Empty;
        return ResourceMaterializationContract.IsExactIdentifier(result);
    }

    private static string? Text(JsonObject value, string field) => Text(value[field]);

    private static string? Text(JsonNode? node) =>
        node is JsonValue value && value.TryGetValue<string>(out var text) &&
        !string.IsNullOrWhiteSpace(text) && string.Equals(text, text.Trim(), StringComparison.Ordinal)
            ? text
            : null;

    private static bool TryInt(JsonNode? node, out int value)
    {
        value = 0;
        return node is JsonValue json && json.TryGetValue(out value);
    }

    private static bool TryPositiveInt(JsonNode? node, out int value) =>
        TryInt(node, out value) && value > 0;

    private static void Add(ICollection<ValidationIssue> destination, IEnumerable<ValidationIssue> source)
    {
        foreach (var issue in source)
            destination.Add(issue);
    }

    private static ValidationIssue Issue(string path, string code, string expected, string actual) =>
        new(path, IssueSeverity.Error, "Canonical Mortal wound-treatment source is invalid.", code: code,
            section: "mortal_wound_treatment", expected: expected, actual: actual);

    private static string Describe(JsonNode? node) => node?.ToJsonString() ?? "missing";
}
