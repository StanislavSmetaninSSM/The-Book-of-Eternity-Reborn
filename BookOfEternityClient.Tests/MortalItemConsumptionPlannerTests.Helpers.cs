using System.Collections;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using BookOfEternityClient.Core;
using BookOfEternityClient.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace BookOfEternityClient.Tests;

/// <summary>
/// Supplies isolated fixture values and exact production API assertions to item planner tests in either test assembly.
/// </summary>
public abstract class MortalItemConsumptionPlannerTestFixture
{
    /// <summary>
    /// The fixed original turn used by pure planning fixtures.
    /// </summary>
    private protected const int Turn = 42;
    /// <summary>
    /// The player inventory carrier path.
    /// </summary>
    private protected const string PlayerPath = "game_state/inventory/items.json";
    /// <summary>
    /// The NPC core carrier path.
    /// </summary>
    private protected const string NpcPath = "game_state/npcs/npc_core.json";
    /// <summary>
    /// The NPC inventory command path.
    /// </summary>
    private protected const string NpcCommandsPath = "game_state/npcs/npc_inventory.json";
    /// <summary>
    /// The exact production item projection root inventory.
    /// </summary>
    private protected static readonly string[] ProjectionRootPaths =
    {
        PlayerPath,
        NpcPath,
        NpcCommandsPath,
        MortalItemAcceptedTransferCatalog.PlayerRemovalPath,
        StorageTransportMoveService.CurrentLocationPath,
        MortalLocationStorageContentsState.StatePath,
        StorageTransportMoveService.VehiclesPath,
        MortalItemIdentityState.StatePath,
        "game_state/quests/quest_history.json",
        "game_state/inventory/item_bonds.json",
        "game_state/inventory/item_text_updates.json",
        "game_state/inventory/recipes.json",
        "game_state/npcs/item_journals.json"
    };

    /// <summary>
    /// Verifies the production projection root registry matches the fixture inventory.
    /// </summary>
    /// <param name="owner">
    /// The production type whose registered root inventory is checked.
    /// </param>
    private protected static void AssertProjectionRootRegistry(Type owner)
    {
        var property = owner.GetProperty(
            "ProjectionRootPaths",
            BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
        Assert.NotNull(property);
        Assert.Equal(typeof(IReadOnlyList<string>), property!.PropertyType);
        Assert.Equal(
            ProjectionRootPaths,
            Assert.IsAssignableFrom<IReadOnlyList<string>>(property.GetValue(null))
                .ToArray());
    }

    /// <summary>
    /// Verifies exact snapshot root values, vehicle representation and detached current/original root clones.
    /// </summary>
    /// <param name="scenario">
    /// The prepared signed projection scenario.
    /// </param>
    /// <param name="vehiclesUseLegacyArrayRoot">
    /// Whether the fixture uses the supported legacy vehicle array representation.
    /// </param>
    private protected static void AssertSnapshotProjectionRoots(
        ProjectionScenario scenario,
        bool vehiclesUseLegacyArrayRoot)
    {
        var current = ReadSnapshotProjectionRoots(
            scenario.Snapshot,
            "CloneCurrentProjectionRoots");
        var backup = ReadSnapshotProjectionRoots(
            scenario.Snapshot,
            "CloneBackupProjectionRoots");
        AssertProjectionInputRoots(scenario.CurrentRoots, current);
        AssertProjectionInputRoots(scenario.BackupRoots, backup);
        var expectedVehicleType = vehiclesUseLegacyArrayRoot
            ? typeof(JsonArray)
            : typeof(JsonObject);
        Assert.Equal(expectedVehicleType,
            current[StorageTransportMoveService.VehiclesPath]!.GetType());
        Assert.Equal(expectedVehicleType,
            backup[StorageTransportMoveService.VehiclesPath]!.GetType());

        current[PlayerPath]!.AsObject()["detachedProbe"] = true;
        var fresh = ReadSnapshotProjectionRoots(
            scenario.Snapshot,
            "CloneCurrentProjectionRoots");
        Assert.False(fresh[PlayerPath]!.AsObject().ContainsKey("detachedProbe"));

        backup[PlayerPath]!.AsObject()["detachedBackupProbe"] = true;
        var freshBackup = ReadSnapshotProjectionRoots(
            scenario.Snapshot,
            "CloneBackupProjectionRoots");
        Assert.False(freshBackup[PlayerPath]!.AsObject()
            .ContainsKey("detachedBackupProbe"));
    }

    private static IReadOnlyDictionary<string, JsonNode?> ReadSnapshotProjectionRoots(
        MortalItemAcceptedTurnNormalizationSnapshot snapshot,
        string methodName)
    {
        var method = snapshot.GetType().GetMethod(
            methodName,
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
            binder: null,
            types: Type.EmptyTypes,
            modifiers: null);
        Assert.NotNull(method);
        Assert.Equal(
            typeof(IReadOnlyDictionary<string, JsonNode?>),
            method!.ReturnType);
        return Assert.IsAssignableFrom<IReadOnlyDictionary<string, JsonNode?>>(
            method.Invoke(snapshot, null));
    }

    private static void AssertProjectionInputRoots(
        IReadOnlyDictionary<string, JsonNode?> expected,
        IReadOnlyDictionary<string, JsonNode?> actual)
    {
        Assert.Equal(
            ProjectionRootPaths.OrderBy(static path => path, StringComparer.Ordinal),
            actual.Keys.OrderBy(static path => path, StringComparer.Ordinal));
        Assert.Equal(
            expected.Keys.OrderBy(static path => path, StringComparer.Ordinal),
            actual.Keys.OrderBy(static path => path, StringComparer.Ordinal));
        foreach (var pair in expected)
            Assert.True(JsonNode.DeepEquals(pair.Value, actual[pair.Key]), pair.Key);
    }

    /// <summary>
    /// Creates a player item fixture with optional item, carrier and resource configuration.
    /// </summary>
    /// <param name="id">
    /// The exact item identifier.
    /// </param>
    /// <param name="count">
    /// The item stack count.
    /// </param>
    /// <param name="configureItem">
    /// Optional item mutation before resealing; <see langword="null"/> leaves the default item unchanged.
    /// </param>
    /// <param name="configureRoot">
    /// Optional carrier mutation after item creation; <see langword="null"/> leaves the default carrier unchanged.
    /// </param>
    /// <param name="resource">
    /// Optional item resource entry; <see langword="null"/> creates no item resource.
    /// </param>
    /// <returns>
    /// The item fixture with its parsed identity and resource authority.
    /// </returns>
    private protected static Fixture CreateFixture(string id, int count,
        Action<JsonObject>? configureItem = null,
        Action<JsonObject, string>? configureRoot = null,
        ResourceStateEntry? resource = null)
    {
        var item = MortalItemTestFixture.CreateCanonicalRoot(id);
        item["count"] = count;
        configureItem?.Invoke(item);
        MortalItemTestFixture.ResealCanonical(item);
        var root = MortalItemTestFixture.CreateCarrier(item, "player_inventory", "player");
        configureRoot?.Invoke(root, id);
        var index = MortalItemTestFixture.CreateIndexForCarrier(item, "player_inventory", "player");
        return BuildFixture(id,
            new MortalItemCarrierCatalogInput(root, null, null, null, null,
                new Dictionary<string, JsonObject>(StringComparer.Ordinal)), index, resource);
    }

    /// <summary>
    /// Creates a canonical NPC equipment item fixture using the selected equipment surface.
    /// </summary>
    /// <param name="id">
    /// The exact item identifier.
    /// </param>
    /// <param name="legacyEquipmentSurface">
    /// Whether to use the legacy equipment surface; defaults to <see langword="false"/>.
    /// </param>
    /// <returns>
    /// The NPC equipment fixture and its exact companion roots.
    /// </returns>
    private protected static Fixture CreateNpcEquipmentFixture(
        string id,
        bool legacyEquipmentSurface = false)
    {
        const string ownerId = "npc_terminal_owner";
        var item = MortalItemTestFixture.CreateCanonicalRoot(id);
        item["count"] = 1;
        Populate(item, "equipment");
        item["equipmentSlot"] = "MainHand";
        MortalItemTestFixture.ResealCanonical(item);
        var owner = new JsonObject
        {
            ["NPCId"] = ownerId,
            ["name"] = "Terminal NPC owner",
            ["inventory"] = new JsonArray(item.DeepClone()),
            ["equippedItems"] = new JsonObject
            {
                ["mainHand"] = legacyEquipmentSurface ? null : id,
                ["offHand"] = "itm_npc_equipment_sibling"
            },
            ["equipment"] = new JsonObject
            {
                ["mainHand"] = legacyEquipmentSurface ? id : null,
                ["offHand"] = "itm_npc_legacy_equipment_sibling"
            },
            ["unchanged"] = "preserve owner sibling"
        };
        var npcRoot = new JsonObject
        {
            ["UpdateNPCs"] = new JsonArray(owner.DeepClone()),
            ["NPCsInScene"] = new JsonArray(
                owner.DeepClone(),
                new JsonObject
                {
                    ["NPCId"] = "npc_terminal_other",
                    ["name"] = "Other preserved NPC",
                    ["inventory"] = new JsonArray(),
                    ["equippedItems"] = new JsonObject
                    {
                        ["mainHand"] = "itm_other_npc_equipment"
                    },
                    ["unchanged"] = "preserve other NPC"
                })
        };
        var index = MortalItemTestFixture.CreateIndexForCarrier(
            item,
            "npc_inventory",
            ownerId);
        return BuildFixture(
            id,
            new MortalItemCarrierCatalogInput(
                null,
                npcRoot,
                null,
                null,
                null,
                new Dictionary<string, JsonObject>(StringComparer.Ordinal)),
            index,
            null);
    }

    /// <summary>
    /// Creates a publication baseline retaining current and original NPC companion roots.
    /// </summary>
    /// <param name="npcRoot">
    /// The current NPC companion root.
    /// </param>
    /// <param name="backupNpcRoot">
    /// The original NPC root override; <see langword="null"/> uses a clone of the current root.
    /// </param>
    /// <returns>
    /// The item-phase baseline with exact NPC and tail authority inputs.
    /// </returns>
    private protected static MortalItemPublicationBaselineInput CreateBaselineInput(
        JsonObject npcRoot,
        JsonObject? backupNpcRoot = null)
    {
        const string itemId = "itm_baseline_player";
        var fixture = CreateFixture(itemId, 1);
        var roots = ProjectionRootPaths.ToDictionary(
            static path => path,
            static _ => (JsonNode?)null,
            StringComparer.Ordinal);
        roots[PlayerPath] = fixture.Roots.PlayerInventory!.DeepClone();
        roots[NpcPath] = npcRoot.DeepClone();
        roots[MortalItemIdentityState.StatePath] = fixture.Index.DeepClone();
        var itemPhase = new MortalItemCanonicalProjectionResult(
            roots,
            fixture.Index.DeepClone().AsObject(),
            Array.Empty<ValidationIssue>(),
            Fingerprint("baseline_item_phase"));
        var backupRoots = roots.ToDictionary(
            static pair => pair.Key,
            static pair => pair.Value?.DeepClone(),
            StringComparer.Ordinal);
        if (backupNpcRoot != null)
            backupRoots[NpcPath] = backupNpcRoot.DeepClone();
        return new MortalItemPublicationBaselineInput(
            itemPhase,
            new MortalTreatmentItemCommandEnvelope(
                null,
                null,
                null,
                null,
                null,
                null,
                null),
            new NpcCoreChangesContract.Authority(
                new HashSet<string>(StringComparer.Ordinal),
                new HashSet<string>(StringComparer.Ordinal),
                new Dictionary<string, string>(StringComparer.Ordinal),
                new HashSet<string>(StringComparer.Ordinal)),
            new CanonicalBeforeImage(false, null),
            new CanonicalBeforeImage(false, null),
            MortalItemNpcTradeTailDisposition.SkipUntouchedTreatmentContinuation,
            backupRoots);
    }

    /// <summary>
    /// Creates matching NPC state rows for exact companion projection checks.
    /// </summary>
    /// <returns>
    /// A fresh root containing mirrored NPC state surfaces.
    /// </returns>
    private protected static JsonObject CreateMirroredNpcRoot()
    {
        var npc = new JsonObject
        {
            ["NPCId"] = "npc_baseline_mirror",
            ["name"] = "Baseline mirror",
            ["inventory"] = new JsonArray(),
            ["equippedItems"] = new JsonObject(),
            ["unchanged"] = "preserve baseline mirror"
        };
        return new JsonObject
        {
            ["UpdateNPCs"] = new JsonArray(npc.DeepClone()),
            ["NPCsInScene"] = new JsonArray(npc.DeepClone())
        };
    }

    /// <summary>
    /// Creates an item fixture with the requested companion reference.
    /// </summary>
    /// <param name="kind">
    /// The companion surface: container, quest, bond or other.
    /// </param>
    /// <returns>
    /// The companion path and its configured item fixture.
    /// </returns>
    private protected static Companion CreateCompanionFixture(string kind)
    {
        var id = "itm_companion_" + kind;
        var item = MortalItemTestFixture.CreateCanonicalRoot(id);
        item["count"] = 1;
        var companions = new Dictionary<string, JsonObject>(StringComparer.Ordinal);
        JsonObject player;
        JsonObject index;
        string path;
        if (kind == "container")
        {
            Populate(item, "container");
            item["isContainer"] = true;
            item["capacity"] = 10;
            MortalItemTestFixture.ResealCanonical(item);
            var child = MortalItemTestFixture.CreateCanonicalRoot("itm_companion_container_child");
            child["contentsPath"] = new JsonArray(id);
            MortalItemTestFixture.ResealCanonical(child);
            player = new JsonObject
            {
                ["items"] = new JsonArray(item.DeepClone(), child.DeepClone()),
                ["equippedItems"] = new JsonObject()
            };
            index = MergeIndexes(
                MortalItemTestFixture.CreateIndexForCarrier(item, "player_inventory", "player"),
                MortalItemTestFixture.CreateIndexForCarrier(child, "player_inventory", "player",
                    containerPath: new JsonArray(id)));
            path = PlayerPath;
        }
        else
        {
            if (kind == "quest")
            {
                Populate(item, "questRole");
                item["questLinks"] = new JsonArray("quest_t070b4");
                path = "game_state/quests/quest_history.json";
                companions[path] = new JsonObject
                {
                    ["questHistory"] = new JsonArray(),
                    ["questRewards"] = new JsonArray(new JsonObject
                    {
                        ["questId"] = "quest_t070b4",
                        ["itemsReceived"] = new JsonArray(new JsonObject { ["itemId"] = id })
                    }),
                    ["questChains"] = new JsonArray()
                };
            }
            else if (kind == "bond")
            {
                Populate(item, "bondsAndFateCards");
                item["ownerBondLevelCurrent"] = 1;
                item["ownerBondLevelMax"] = 10;
                path = "game_state/inventory/item_bonds.json";
                companions[path] = new JsonObject
                {
                    ["itemBondLevelChanges"] = new JsonArray(new JsonObject
                    {
                        ["itemId"] = id, ["newBondLevel"] = 1,
                        ["changeReason"] = "T070-B.4 durable bond fixture."
                    })
                };
            }
            else if (kind == "other")
            {
                path = "game_state/npcs/item_journals.json";
                companions[path] = new JsonObject
                {
                    ["entries"] = new JsonArray(new JsonObject
                    {
                        ["itemId"] = id,
                        ["journalEntries"] = new JsonArray(new JsonObject
                        {
                            ["turn"] = 41, ["description"] = "Durable journal reference."
                        })
                    })
                };
            }
            else throw new ArgumentOutOfRangeException(nameof(kind), kind, null);
            MortalItemTestFixture.ResealCanonical(item);
            player = MortalItemTestFixture.CreateCarrier(item, "player_inventory", "player");
            index = MortalItemTestFixture.CreateIndexForCarrier(item, "player_inventory", "player");
        }
        return new Companion(path, BuildFixture(id,
            new MortalItemCarrierCatalogInput(player, null, null, null, null, companions),
            index, null));
    }

    private static Fixture BuildFixture(string id, MortalItemCarrierCatalogInput roots,
        JsonObject index, ResourceStateEntry? resource)
    {
        var identity = MortalItemIdentityState.Parse(index.ToJsonString());
        Assert.Empty(identity.Issues);
        return new Fixture(id, roots, index, identity, ResourceDefinitionCatalog.CreateBuiltIn(),
            new ResourceStateLedger(resource == null ? [] : [resource]),
            new ResourceSourceEvidence("mortal_wound_treatment", "mwta_t070b4_attempt",
                Fingerprint("capacity_source")), Fingerprint("capacity_policy"));
    }

    /// <summary>
    /// Creates an active item durability resource with deterministic capacity and chronology evidence.
    /// </summary>
    /// <param name="id">
    /// The exact item identifier.
    /// </param>
    /// <param name="maximum">
    /// The resource maximum.
    /// </param>
    /// <param name="current">
    /// The resource current value.
    /// </param>
    /// <param name="kind">
    /// The capacity policy kind; defaults to <see cref="ResourceCapacityKind.InstanceFixed"/>.
    /// </param>
    /// <returns>
    /// The item durability state entry.
    /// </returns>
    private protected static ResourceStateEntry Resource(string id, decimal maximum, decimal current,
        ResourceCapacityKind kind = ResourceCapacityKind.InstanceFixed) => new(
        new ResourceCoordinate("mortal_world", ResourceOwnerKind.Item, id, "durability"),
        current, maximum,
        new ResourceCapacityBinding(kind, "capacity_" + id + "_durability",
            Fingerprint("binding_" + id + "_" + kind)),
        ResourceLifecycleState.Active,
        new ResourceChronology(41, "turn_41:resource:item_durability",
            "resource_transition_initialize_" + id,
            "turn_41:resource:item_durability", 41));

    /// <summary>
    /// Creates one ordered treatment item-consumption command with deterministic claim evidence.
    /// </summary>
    /// <param name="ordinal">
    /// The consumption command ordinal.
    /// </param>
    /// <param name="id">
    /// The exact item identifier.
    /// </param>
    /// <param name="quantity">
    /// The consumed item quantity.
    /// </param>
    /// <param name="transitionId">
    /// The exact transition identifier.
    /// </param>
    /// <returns>
    /// The consumption command for the supplied quantity and transition.
    /// </returns>
    private protected static Command BuildCommand(int ordinal, string id, int quantity, string transitionId) =>
        new(ordinal, id, quantity, Fingerprint("claim_" + ordinal + "_" + id), transitionId,
            "mortal_wound_treatment", "mwta_t070b4_finalization_" + ordinal);

    /// <summary>
    /// Runs the production item-consumption planner using the fixture authority.
    /// </summary>
    /// <param name="fixture">
    /// The isolated item fixture and its authority inputs.
    /// </param>
    /// <param name="commands">
    /// The ordered consumption commands; an empty list requests no consumption.
    /// </param>
    /// <returns>
    /// The planner's carrier/index after-images, transitions, resource consequences and issues.
    /// </returns>
    private protected static Result Plan(Fixture fixture, params Command[] commands) =>
        InvokePlan(CreatePlanningInput(fixture, commands));

    /// <summary>
    /// Runs item-consumption planning with explicitly supplied identity authority.
    /// </summary>
    /// <param name="fixture">
    /// The isolated item fixture and its authority inputs.
    /// </param>
    /// <param name="identity">
    /// The explicit parsed item identity authority.
    /// </param>
    /// <param name="commands">
    /// The ordered consumption commands; an empty list requests no consumption.
    /// </param>
    /// <returns>
    /// The planner's result for the supplied identity authority.
    /// </returns>
    private protected static Result PlanWithIdentity(
        Fixture fixture,
        MortalItemIdentityParseResult identity,
        params Command[] commands) =>
        InvokePlan(CreatePlanningInput(fixture, commands, identityState: identity));

    /// <summary>
    /// Runs item-consumption planning with explicitly supplied carrier roots.
    /// </summary>
    /// <param name="fixture">
    /// The isolated item fixture and its authority inputs.
    /// </param>
    /// <param name="roots">
    /// The item carrier roots to inspect or plan against.
    /// </param>
    /// <param name="commands">
    /// The ordered consumption commands; an empty list requests no consumption.
    /// </param>
    /// <returns>
    /// The planner's result for the supplied carrier roots.
    /// </returns>
    private protected static Result PlanWithRoots(
        Fixture fixture,
        MortalItemCarrierCatalogInput roots,
        params Command[] commands) =>
        InvokePlan(CreatePlanningInput(fixture, commands, carrierRoots: roots));

    /// <summary>
    /// Creates the production consumption-planning input while checking its reflected API shape.
    /// </summary>
    /// <param name="fixture">
    /// The isolated item fixture and its authority inputs.
    /// </param>
    /// <param name="commands">
    /// The ordered consumption commands; an empty list requests no consumption.
    /// </param>
    /// <param name="carrierRoots">
    /// The carrier override; <see langword="null"/> uses detached fixture roots.
    /// </param>
    /// <param name="identityState">
    /// The identity override; <see langword="null"/> parses the fixture identity root.
    /// </param>
    /// <returns>
    /// The production input populated with the selected roots, identity and ordered commands.
    /// </returns>
    private protected static object CreatePlanningInput(
        Fixture fixture,
        IReadOnlyList<Command> commands,
        MortalItemCarrierCatalogInput? carrierRoots = null,
        MortalItemIdentityParseResult? identityState = null)
    {
        var inputType = ExactType("BookOfEternityClient.Services.MortalItemConsumptionPlanningInput");
        var commandType = ExactType("BookOfEternityClient.Services.MortalItemConsumptionCommand");
        var resultType = ExactType("BookOfEternityClient.Services.MortalItemConsumptionPlanningResult");
        PlanShape(inputType, commandType, resultType);
        var commandArray = Array.CreateInstance(commandType, commands.Count);
        var commandCtor = Ctor(commandType, typeof(int), typeof(string), typeof(int),
            typeof(string), typeof(string), typeof(string), typeof(string));
        for (var i = 0; i < commands.Count; i++)
        {
            var value = commands[i];
            commandArray.SetValue(commandCtor.Invoke([
                value.Ordinal, value.ItemId, value.Quantity, value.ClaimFingerprint,
                value.TransitionId, value.AuthorityKind, value.AuthorityId]), i);
        }
        var input = Ctor(inputType, typeof(int), typeof(string),
            typeof(MortalItemCarrierCatalogInput), typeof(MortalItemIdentityParseResult),
            typeof(IReadOnlyList<>).MakeGenericType(commandType),
            typeof(ResourceDefinitionCatalog), typeof(ResourceStateLedger),
            typeof(ResourceSourceEvidence), typeof(string)).Invoke([
                Turn, Fingerprint("baseline_" + fixture.ItemId),
                carrierRoots ?? Clone(fixture.Roots),
                identityState ?? MortalItemIdentityState.Parse(fixture.Index.ToJsonString()), commandArray,
                fixture.Definitions, new ResourceStateLedger(fixture.State.Entries),
                fixture.Source, fixture.PolicyFingerprint]);
        return input;
    }

    private static Result InvokePlan(object input)
    {
        var planner = ExactType("BookOfEternityClient.Services.MortalItemConsumptionPlanner");
        var inputType = ExactType("BookOfEternityClient.Services.MortalItemConsumptionPlanningInput");
        var resultType = ExactType("BookOfEternityClient.Services.MortalItemConsumptionPlanningResult");
        var method = ExactMethod(planner, "Plan", inputType, resultType);
        return ReadResult(method.Invoke(null, [input])!);
    }

    /// <summary>
    /// Creates a planning result containing only the selected capacity intents and issues.
    /// </summary>
    /// <param name="capacities">
    /// Capacity intents included in the result; <see langword="null"/> means no intents.
    /// </param>
    /// <param name="issues">
    /// Validation issues included in the result; <see langword="null"/> means no issues.
    /// </param>
    /// <returns>
    /// The production result used to check fingerprint sensitivity.
    /// </returns>
    private protected static object ResultForFingerprint(
        IReadOnlyList<ResourceCapacityIntent>? capacities = null,
        IReadOnlyList<ValidationIssue>? issues = null) =>
        new MortalItemConsumptionPlanningResult(
            new Dictionary<string, JsonObject>(StringComparer.Ordinal),
            null,
            Array.Empty<JsonObject>(),
            capacities ?? Array.Empty<ResourceCapacityIntent>(),
            Array.Empty<ResourceOwnerKey>(),
            issues ?? Array.Empty<ValidationIssue>(),
            string.Empty);

    /// <summary>
    /// Invokes the production planning fingerprint function for the supplied input and result.
    /// </summary>
    /// <param name="input">
    /// The production consumption-planning input.
    /// </param>
    /// <param name="result">
    /// The production consumption-planning result whose fingerprint is recomputed.
    /// </param>
    /// <returns>
    /// The computed planning fingerprint.
    /// </returns>
    private protected static string InvokeFingerprint(object input, object result)
    {
        var planner = ExactType("BookOfEternityClient.Services.MortalItemConsumptionPlanner");
        var method = Assert.Single(planner.GetMethods(
            BindingFlags.Static | BindingFlags.NonPublic), value =>
            value.Name == "Fingerprint" && value.GetParameters().Length == 2);
        return Assert.IsType<string>(method.Invoke(null, [input, result]));
    }

    private static Result ReadResult(object value) => new(
        Read<IReadOnlyDictionary<string, JsonObject>>(value, "CarrierAfterImages"),
        ReadNullableObject(value, "IdentityIndexAfterImage"),
        Read<IReadOnlyList<JsonObject>>(value, "IdentityTransitions"),
        Read<IReadOnlyList<ResourceCapacityIntent>>(value, "CapacityTransitions"),
        Read<IReadOnlyList<ResourceOwnerKey>>(value, "TerminalOwners"),
        Read<IReadOnlyList<ValidationIssue>>(value, "Issues"),
        Read<string>(value, "Fingerprint"),
        Read<bool>(value, "IsValid"));

    private static void PlanShape(Type input, Type command, Type result)
    {
        Assert.True(input.IsSealed && command.IsSealed && result.IsSealed);
        Property(input, "Turn", typeof(int));
        Property(input, "BaselineFingerprint", typeof(string));
        Property(input, "CarrierRoots", typeof(MortalItemCarrierCatalogInput));
        Property(input, "IdentityState", typeof(MortalItemIdentityParseResult));
        Property(input, "Commands", typeof(IReadOnlyList<>).MakeGenericType(command));
        Property(input, "Definitions", typeof(ResourceDefinitionCatalog));
        Property(input, "ResourceState", typeof(ResourceStateLedger));
        Property(input, "CapacitySourceEvidence", typeof(ResourceSourceEvidence));
        Property(input, "CapacityPolicyFingerprint", typeof(string));
        Property(command, "FinalizationOrdinal", typeof(int));
        Property(command, "ItemId", typeof(string));
        Property(command, "Quantity", typeof(int));
        Property(command, "ClaimFingerprint", typeof(string));
        Property(command, "TransitionId", typeof(string));
        Property(command, "AuthorityKind", typeof(string));
        Property(command, "AuthorityId", typeof(string));
        Property(result, "CarrierAfterImages", typeof(IReadOnlyDictionary<string, JsonObject>));
        Property(result, "IdentityIndexAfterImage", typeof(JsonObject));
        Property(result, "IdentityTransitions", typeof(IReadOnlyList<JsonObject>));
        Property(result, "CapacityTransitions", typeof(IReadOnlyList<ResourceCapacityIntent>));
        Property(result, "TerminalOwners", typeof(IReadOnlyList<ResourceOwnerKey>));
        Property(result, "Issues", typeof(IReadOnlyList<ValidationIssue>));
        Property(result, "Fingerprint", typeof(string));
        Property(result, "IsValid", typeof(bool));
    }

    /// <summary>
    /// Prepares a fresh signed filesystem scenario containing item creation and transfer authority.
    /// </summary>
    /// <param name="vehiclesUseLegacyArrayRoot">
    /// Whether the fixture uses the supported legacy vehicle array representation.
    /// </param>
    /// <returns>
    /// A task returning the scenario's snapshot, route, root and identity inputs.
    /// </returns>
    private protected static async Task<ProjectionScenario> ProductionProjectionScenarioAsync(
        bool vehiclesUseLegacyArrayRoot)
    {
        const int turn = 43;
        const string sessionId = "session_t070b4_projection";
        const string creationRef = "new_item_projection_created";
        const string transferredId = "itm_projection_transferred";
        const string npcId = "npc_projection";
        var root = Path.Combine(Path.GetTempPath(),
            "boe-t070b4-projection-" + Guid.NewGuid().ToString("N"));
        var expectedParent = Path.GetFullPath(Path.GetTempPath()).TrimEnd(
            Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) +
            Path.DirectorySeparatorChar;
        try
        {
            var fileSystem = new FileSystemManager(root, NullLogger<FileSystemManager>.Instance);
            fileSystem.EnsureDirectoryStructure();
            await SeedProjectionBootstrapAsync(fileSystem);
            await WriteJsonAsync(fileSystem, NpcCommandsPath, new JsonObject());
            await WriteJsonAsync(
                fileSystem,
                MortalItemAcceptedTransferCatalog.PlayerRemovalPath,
                new JsonObject());
            await WriteJsonAsync(
                fileSystem,
                MortalLocationStorageContentsState.StatePath,
                MortalLocationStorageContentsState.CreateEmptyRoot());
            await WriteJsonAsync(
                fileSystem,
                StorageTransportMoveService.VehiclesPath,
                vehiclesUseLegacyArrayRoot
                    ? new JsonArray()
                    : new JsonObject { ["vehicles"] = new JsonArray() });
            await WriteJsonAsync(
                fileSystem,
                "game_state/quests/quest_history.json",
                new JsonObject { ["questHistory"] = new JsonArray() });
            var transferred = MortalItemTestFixture.CreateCanonicalRootAtTurn(
                transferredId, 42, "npc_acquisition", "npc_inventory_add",
                "npc_inventory_add:42:0:npc_projection",
                name: "Transferred projection item");
            var npcBefore = ProjectionNpcRoot(npcId, transferred);
            var indexRoot = MortalItemTestFixture.CreateIndexForCarrier(
                transferred, "npc_inventory", npcId);
            await WriteJsonAsync(fileSystem, NpcPath, npcBefore);
            await WriteJsonAsync(fileSystem, MortalItemIdentityState.StatePath, indexRoot);
            var backup = await ReadProjectionRootsAsync(fileSystem);
            await CapturePendingSnapshotAsync(fileSystem, sessionId, turn);

            var playerBeforeJson = await fileSystem.ReadFileAsync(PlayerPath);
            Assert.NotNull(playerBeforeJson);
            var playerBefore = Assert.IsType<JsonObject>(JsonNode.Parse(
                playerBeforeJson!));
            var raw = MortalItemTestFixture.CreateRawRoot(
                "player_acquisition", "turn_outcome", $"turn_{turn}", turn,
                creationRef, "mat_item_projection_created");
            var playerCurrent = playerBefore.DeepClone().AsObject();
            playerCurrent["UpdateInventory"] = new JsonArray(
                transferred.DeepClone(), raw.DeepClone());
            var commandsCurrent = new JsonObject
            {
                ["NPCInventoryRemovals"] = new JsonArray(new JsonObject
                {
                    ["NPCId"] = npcId,
                    ["NPCName"] = "Projection owner",
                    ["itemId"] = transferredId
                })
            };
            await WriteJsonAsync(fileSystem, PlayerPath, playerCurrent);
            await WriteJsonAsync(fileSystem, NpcCommandsPath, commandsCurrent);

            var validator = new ValidationService(
                fileSystem, NullLogger<ValidationService>.Instance);
            var validationIssues = await validator
                .ValidateAcceptedTurnRawMortalItemMaterializationAsync();
            Assert.True(
                validationIssues.All(issue => issue.Severity != IssueSeverity.Error),
                string.Join(" | ", validationIssues.Select(issue =>
                    $"{issue.Code}@{issue.FilePath}: {issue.Expected} -> {issue.Actual}")));

            MortalItemAcceptedTurnNormalizationSnapshot snapshot;
            string createdId;
            var snapshotToken = await PendingSnapshotTokenAsync(fileSystem);
            await using (var lease = await fileSystem.AcquireCanonicalWriteLeaseAsync())
            {
                Assert.True(MortalItemAcceptedTurnAuthority.TryGetAllocatedItemId(
                    fileSystem, lease, sessionId, snapshotToken,
                    creationRef, out createdId));
                Assert.True(MortalItemAcceptedTurnAuthority.TryCaptureNormalizationSnapshot(
                    fileSystem, lease, sessionId, snapshotToken,
                    turn, out snapshot));
            }
            var routes = await MortalItemRouteAuthorityCatalog.BuildAsync(fileSystem);
            Assert.Empty(routes.Issues);
            var current = await ReadProjectionRootsAsync(fileSystem);
            return new ProjectionScenario(turn, snapshot, routes, current, backup,
                indexRoot, createdId, creationRef, transferredId);
        }
        finally
        {
            var fullRoot = Path.GetFullPath(root);
            if (!fullRoot.StartsWith(expectedParent, StringComparison.OrdinalIgnoreCase) ||
                !Path.GetFileName(fullRoot).StartsWith(
                    "boe-t070b4-projection-", StringComparison.Ordinal))
                throw new InvalidOperationException($"Unsafe projection test root '{fullRoot}'.");
            if (Directory.Exists(fullRoot)) Directory.Delete(fullRoot, recursive: true);
        }
    }

    /// <summary>
    /// Creates unmaterialized item occurrences in the five production collector surfaces.
    /// </summary>
    /// <param name="turn">
    /// The original positive turn number.
    /// </param>
    /// <param name="creationRefs">
    /// Exactly five creation references in player, NPC state, NPC command, current-storage and offscreen-storage collector order.
    /// </param>
    /// <returns>
    /// The carrier catalog input preserving the supplied creation-reference order.
    /// </returns>
    private protected static MortalItemCarrierCatalogInput FiveCollectorInput(
        int turn,
        IReadOnlyList<string> creationRefs)
    {
        Assert.Equal(5, creationRefs.Count);
        var player = Raw(
            creationRefs[0],
            "player_acquisition",
            "turn_outcome",
            $"turn_{turn}");
        var npcCoreItem = Raw(
            creationRefs[1],
            "new_npc_inventory",
            "new_npc",
            "npc_five_new");
        var npcCommandItem = Raw(
            creationRefs[2],
            "npc_acquisition",
            "npc_inventory_add",
            $"npc_inventory_add:{turn}:0:npc_five_command");
        var currentLocationItem = Raw(
            creationRefs[3],
            "storage_placement",
            "location_storage",
            "loc_five_storage:storage_five_current");
        var offscreenItem = Raw(
            creationRefs[4],
            "storage_placement",
            "location_storage",
            "loc_five_storage:storage_five_offscreen");
        var mirroredCommandOwner = new JsonObject
        {
            ["NPCId"] = "npc_five_command",
            ["name"] = "Five collector mirrored command owner",
            ["inventory"] = new JsonArray(),
            ["equippedItems"] = new JsonObject(),
            ["equipment"] = new JsonObject()
        };

        return new MortalItemCarrierCatalogInput(
            new JsonObject
            {
                ["items"] = new JsonArray(),
                ["equippedItems"] = new JsonObject(),
                ["UpdateInventory"] = new JsonArray(player)
            },
            new JsonObject
            {
                ["UpdateNPCs"] = new JsonArray(
                    new JsonObject
                    {
                        ["initialId"] = "npc_five_new",
                        ["name"] = "Five collector new NPC",
                        ["inventory"] = new JsonArray(npcCoreItem),
                        ["equippedItems"] = new JsonObject()
                    },
                    mirroredCommandOwner.DeepClone()),
                ["NPCsInScene"] = new JsonArray(mirroredCommandOwner.DeepClone())
            },
            new JsonObject
            {
                ["NPCInventoryAdds"] = new JsonArray(new JsonObject
                {
                    ["NPCId"] = "npc_five_command",
                    ["NPCName"] = "Five collector command owner",
                    ["item"] = npcCommandItem,
                    ["destinationContainerId"] = null
                }),
                ["NPCEquipmentChanges"] = new JsonArray(new JsonObject
                {
                    ["NPCId"] = "npc_five_command",
                    ["itemId"] = creationRefs[2],
                    ["action"] = "equip",
                    ["targetSlots"] = new JsonArray("mainHand")
                })
            },
            new JsonObject
            {
                ["locationId"] = "loc_five_storage",
                ["locationStorages"] = new JsonArray(
                    new JsonObject
                    {
                        ["storageId"] = "storage_five_current",
                        ["contents"] = new JsonArray(currentLocationItem)
                    },
                    new JsonObject
                    {
                        ["storageId"] = "storage_five_offscreen",
                        ["contents"] = new JsonArray()
                    })
            },
            null,
            new Dictionary<string, JsonObject>(StringComparer.Ordinal),
            MortalLocationStorageContentsState.BuildCanonicalRoot(
                new Dictionary<MortalLocationStorageKey, JsonArray>
                {
                    [new MortalLocationStorageKey(
                        "loc_five_storage",
                        "storage_five_offscreen")] = new JsonArray(offscreenItem)
                }));

        JsonObject Raw(
            string creationRef,
            string route,
            string authorityKind,
            string authorityId) =>
            MortalItemTestFixture.CreateRawRoot(
                route,
                authorityKind,
                authorityId,
                turn,
                creationRef,
                "mat_" + creationRef);
    }

    /// <summary>
    /// Creates complete projection roots from the five-collector carrier input.
    /// </summary>
    /// <param name="input">
    /// The five-collector carrier catalog input.
    /// </param>
    /// <returns>
    /// Detached carrier roots and empty companion authority surfaces.
    /// </returns>
    private protected static IReadOnlyDictionary<string, JsonNode?> FiveCollectorProjectionRoots(
        MortalItemCarrierCatalogInput input)
    {
        var roots = ProjectionRootPaths.ToDictionary(
            static path => path,
            static _ => (JsonNode?)null,
            StringComparer.Ordinal);
        roots[PlayerPath] = input.PlayerInventory!.DeepClone();
        roots[NpcPath] = input.NpcCore!.DeepClone();
        roots[NpcCommandsPath] = input.NpcInventoryCommands!.DeepClone();
        roots[MortalItemAcceptedTransferCatalog.PlayerRemovalPath] = new JsonObject();
        roots[StorageTransportMoveService.CurrentLocationPath] =
            input.CurrentLocation!.DeepClone();
        roots[MortalLocationStorageContentsState.StatePath] =
            input.OffscreenLocationStorageContents!.DeepClone();
        roots[StorageTransportMoveService.VehiclesPath] =
            new JsonObject { ["vehicles"] = new JsonArray() };
        roots[MortalItemIdentityState.StatePath] =
            MortalItemIdentityState.CreateEmptyRoot();
        roots["game_state/quests/quest_history.json"] =
            new JsonObject { ["questHistory"] = new JsonArray() };
        roots["game_state/inventory/item_bonds.json"] = new JsonObject();
        roots["game_state/inventory/item_text_updates.json"] = new JsonObject();
        roots["game_state/npcs/item_journals.json"] = new JsonObject();
        return roots;
    }

    /// <summary>
    /// Writes five-collector carrier and original route-owner evidence to the isolated filesystem.
    /// </summary>
    /// <param name="fileSystem">
    /// The fresh filesystem owning the fixture files.
    /// </param>
    /// <param name="input">
    /// The five-collector carrier input written to the route fixture files.
    /// </param>
    /// <param name="turn">
    /// The original positive turn number.
    /// </param>
    /// <returns>
    /// A task completing after the route authority fixture files have been written.
    /// </returns>
    private protected static async Task SeedFiveCollectorRouteAuthorityAsync(
        FileSystemManager fileSystem,
        MortalItemCarrierCatalogInput input,
        int turn)
    {
        await WriteJsonAsync(fileSystem, PlayerPath, input.PlayerInventory!);
        await WriteJsonAsync(fileSystem, NpcPath, input.NpcCore!);
        await WriteJsonAsync(fileSystem, NpcCommandsPath, input.NpcInventoryCommands!);
        await WriteJsonAsync(
            fileSystem,
            StorageTransportMoveService.CurrentLocationPath,
            input.CurrentLocation!);
        await WriteJsonAsync(
            fileSystem,
            MortalLocationStorageContentsState.StatePath,
            input.OffscreenLocationStorageContents!);
        await WriteJsonAsync(
            fileSystem,
            StorageTransportMoveService.VehiclesPath,
            new JsonObject { ["vehicles"] = new JsonArray() });
        await WriteJsonAsync(
            fileSystem,
            "game_state/quests/quest_history.json",
            new JsonObject { ["questHistory"] = new JsonArray() });
        await WriteJsonAsync(fileSystem, "input/turn_request.json", new JsonObject
        {
            ["sessionId"] = "session_t070b4_five_collectors",
            ["requestId"] = "request_t070b4_five_collectors",
            ["turnNumber"] = turn,
            ["playerAction"] = "Validate all five item creation collectors."
        });

        const string npcSnapshotPath =
            "game_state/control/five_collectors_snapshot/npc_core.json";
        const string locationSnapshotPath =
            "game_state/control/five_collectors_snapshot/current_location.json";
        var mirroredCommandOwner = new JsonObject
        {
            ["NPCId"] = "npc_five_command",
            ["name"] = "Five collector mirrored command owner",
            ["inventory"] = new JsonArray(),
            ["equippedItems"] = new JsonObject(),
            ["equipment"] = new JsonObject()
        };
        await WriteJsonAsync(fileSystem, npcSnapshotPath, new JsonObject
        {
            ["UpdateNPCs"] = new JsonArray(mirroredCommandOwner.DeepClone()),
            ["NPCsInScene"] = new JsonArray(mirroredCommandOwner.DeepClone())
        });
        await WriteJsonAsync(fileSystem, locationSnapshotPath, new JsonObject
        {
            ["locationId"] = "loc_five_storage",
            ["locationStorages"] = new JsonArray(
                new JsonObject
                {
                    ["storageId"] = "storage_five_current",
                    ["contents"] = new JsonArray()
                },
                new JsonObject
                {
                    ["storageId"] = "storage_five_offscreen",
                    ["contents"] = new JsonArray()
                })
        });
        await WriteJsonAsync(
            fileSystem,
            "game_state/control/pending_turn_snapshot.json",
            new JsonObject
            {
                ["files"] = new JsonObject
                {
                    [NpcPath] = npcSnapshotPath,
                    [StorageTransportMoveService.CurrentLocationPath] =
                        locationSnapshotPath
                }
            });
    }

    /// <summary>
    /// Registers ordinary validated item candidates while verifying the exact production API and optional allocation defaults.
    /// </summary>
    /// <param name="fileSystem">
    /// The fresh filesystem owning the fixture files.
    /// </param>
    /// <param name="lease">
    /// The active canonical write lease for the supplied filesystem.
    /// </param>
    /// <param name="sessionId">
    /// The exact original session identifier.
    /// </param>
    /// <param name="snapshotToken">
    /// The exact original snapshot token.
    /// </param>
    /// <param name="catalog">
    /// The validated raw item carrier occurrences registered for ordinary allocation.
    /// </param>
    /// <param name="routeCatalog">
    /// The admitted route authorities indexed by creation reference.
    /// </param>
    /// <param name="currentRoots">
    /// The detached current projection roots.
    /// </param>
    /// <param name="backupRoots">
    /// The detached authenticated original projection roots.
    /// </param>
    private protected static void RegisterFiveCollectorItems(
        FileSystemManager fileSystem,
        FileSystemManager.CanonicalWriteLease lease,
        string sessionId,
        string snapshotToken,
        MortalItemCarrierCatalog catalog,
        MortalItemRouteAuthorityCatalog routeCatalog,
        IReadOnlyDictionary<string, JsonNode?> currentRoots,
        IReadOnlyDictionary<string, JsonNode?> backupRoots)
    {
        var method = Assert.Single(typeof(MortalItemAcceptedTurnAuthority).GetMethods(
                BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic),
            static candidate => string.Equals(
                candidate.Name,
                "RegisterValidatedItems",
                StringComparison.Ordinal));
        var parameters = method.GetParameters();
        Assert.Equal(new[]
        {
            "fs", "writeLease", "sessionId", "snapshotToken", "catalog", "knownItemIds",
            "routeAuthorities", "transferCatalog", "currentProjectionRoots", "backupProjectionRoots",
            "identityFactory", "requestId", "turn"
        }, parameters.Select(static parameter => parameter.Name));
        Assert.All(parameters.Skip(10), static parameter =>
        {
            Assert.True(parameter.IsOptional);
            Assert.Null(parameter.DefaultValue);
        });
        var arguments = new object?[parameters.Length];
        arguments[0] = fileSystem;
        arguments[1] = lease;
        arguments[2] = sessionId;
        arguments[3] = snapshotToken;
        arguments[4] = catalog;
        arguments[5] = Array.Empty<string>();
        arguments[6] = routeCatalog;
        arguments[7] = null;
        arguments[8] = ConvertProjectionRoots(
            parameters[8].ParameterType,
            currentRoots);
        arguments[9] = ConvertProjectionRoots(
            parameters[9].ParameterType,
            backupRoots);
        arguments[10] = null;
        arguments[11] = null;
        arguments[12] = null;
        method.Invoke(null, arguments);
    }

    private static object ConvertProjectionRoots(
        Type targetType,
        IReadOnlyDictionary<string, JsonNode?> roots)
    {
        var dictionaryInterface = targetType.IsGenericType
            ? targetType
            : Assert.Single(targetType.GetInterfaces(), static candidate =>
                candidate.IsGenericType &&
                candidate.GetGenericTypeDefinition() == typeof(IReadOnlyDictionary<,>));
        Assert.Equal(typeof(IReadOnlyDictionary<,>),
            dictionaryInterface.GetGenericTypeDefinition());
        var genericArguments = dictionaryInterface.GetGenericArguments();
        Assert.Equal(typeof(string), genericArguments[0]);
        Assert.True(genericArguments[1] == typeof(JsonObject) ||
                    genericArguments[1] == typeof(JsonNode));
        var dictionaryType = typeof(Dictionary<,>).MakeGenericType(genericArguments);
        var result = Assert.IsAssignableFrom<IDictionary>(
            Activator.CreateInstance(dictionaryType));
        foreach (var pair in roots)
        {
            var node = pair.Value?.DeepClone();
            Assert.True(node is null || genericArguments[1].IsInstanceOfType(node));
            result.Add(pair.Key, node);
        }
        return result;
    }

    /// <summary>
    /// Reconstructs an accepted creation identity from its exact route and production collector ordinal.
    /// </summary>
    /// <param name="prefix">
    /// The required identity prefix.
    /// </param>
    /// <param name="domain">
    /// The receipt or transition identity domain.
    /// </param>
    /// <param name="sessionId">
    /// The exact original session identifier.
    /// </param>
    /// <param name="snapshotToken">
    /// The exact original snapshot token.
    /// </param>
    /// <param name="turn">
    /// The original positive turn number.
    /// </param>
    /// <param name="creationRef">
    /// The exact item creation reference.
    /// </param>
    /// <param name="route">
    /// The admitted creation route and its source authority.
    /// </param>
    /// <param name="ordinal">
    /// The one-based ordinal in the production creation collector sequence.
    /// </param>
    /// <returns>
    /// The expected prefixed identity fingerprint.
    /// </returns>
    private protected static string ExpectedAcceptedCreationIdentityId(
        string prefix,
        string domain,
        string sessionId,
        string snapshotToken,
        int turn,
        string creationRef,
        MortalItemRouteAuthority route,
        int ordinal)
    {
        var routeFingerprint = WoundAcceptedTurnFingerprintWriter.Compute(new string?[]
        {
            "book_of_eternity.mortal_item.route_authority",
            "1",
            route.Route,
            route.AuthorityKind,
            route.AuthorityId,
            WoundAcceptedTurnFingerprintWriter.CanonicalJson(new JsonObject
            {
                ["kind"] = route.Destination.Kind,
                ["ownerId"] = route.Destination.OwnerId,
                ["containerId"] = route.Destination.ContainerId,
                ["containerPath"] = new JsonArray(route.Destination.ContainerPath
                    .Select(static value => (JsonNode?)value)
                    .ToArray())
            }),
            string.Join("\0", route.SourceItemIds)
        });
        var fingerprint = WoundAcceptedTurnFingerprintWriter.Compute(new string?[]
        {
            "book_of_eternity.mortal_item.accepted_turn_identity",
            "1",
            domain,
            sessionId,
            snapshotToken,
            turn.ToString(System.Globalization.CultureInfo.InvariantCulture),
            creationRef,
            routeFingerprint,
            ordinal.ToString(System.Globalization.CultureInfo.InvariantCulture)
        });
        return prefix + fingerprint["sha256:".Length..];
    }

    /// <summary>
    /// Creates the production projection input from signed scenario authority and selected roots.
    /// </summary>
    /// <param name="inputType">
    /// The exact production projection input type.
    /// </param>
    /// <param name="scenario">
    /// The prepared signed projection scenario.
    /// </param>
    /// <param name="currentRoots">
    /// The detached current projection roots; <see langword="null"/> uses the prepared scenario roots.
    /// </param>
    /// <param name="backupRoots">
    /// The detached original projection roots; <see langword="null"/> uses the prepared scenario roots.
    /// </param>
    /// <param name="identityRoot">
    /// The identity root override; <see langword="null"/> uses the prepared scenario identity root.
    /// </param>
    /// <returns>
    /// The production projection input with detached current and original roots.
    /// </returns>
    private protected static object ProjectionInput(
        Type inputType,
        ProjectionScenario scenario,
        IReadOnlyDictionary<string, JsonNode?>? currentRoots = null,
        IReadOnlyDictionary<string, JsonNode?>? backupRoots = null,
        JsonObject? identityRoot = null)
    {
        var current = (currentRoots ?? scenario.CurrentRoots).ToDictionary(pair => pair.Key,
            pair => pair.Value?.DeepClone(), StringComparer.Ordinal);
        var backup = (backupRoots ?? scenario.BackupRoots).ToDictionary(pair => pair.Key,
            pair => pair.Value?.DeepClone(), StringComparer.Ordinal);
        var identity = MortalItemIdentityState.Parse(
            (identityRoot ?? scenario.IdentityRoot).ToJsonString());
        Assert.Empty(identity.Issues);
        return Ctor(inputType, typeof(int),
            typeof(MortalItemAcceptedTurnNormalizationSnapshot),
            typeof(MortalItemRouteAuthorityCatalog),
            typeof(IReadOnlyDictionary<string, JsonNode?>),
            typeof(IReadOnlyDictionary<string, JsonNode?>),
            typeof(MortalItemIdentityParseResult)).Invoke([
                scenario.Turn, scenario.Snapshot, scenario.RouteCatalog,
                current, backup, identity]);
    }

    private static async Task<IReadOnlyDictionary<string, JsonNode?>>
        ReadProjectionRootsAsync(FileSystemManager fileSystem)
    {
        var roots = new Dictionary<string, JsonNode?>(StringComparer.Ordinal);
        foreach (var path in ProjectionRootPaths)
        {
            var json = await fileSystem.ReadFileAsync(path);
            roots.Add(path, json == null ? null : JsonNode.Parse(json));
        }
        return roots;
    }

    /// <summary>
    /// Seeds canonical baseline files needed by the signed item projection scenario.
    /// </summary>
    /// <param name="fileSystem">
    /// The fresh filesystem owning the fixture files.
    /// </param>
    /// <returns>
    /// A task completing after the baseline fixture files have been written.
    /// </returns>
    private protected static async Task SeedProjectionBootstrapAsync(FileSystemManager fileSystem)
    {
        var files = MortalBootstrapStateBuilder.BuildFreshMortalBootstrapFiles(
            1, 1, "Projection test mortal.", "Projection test world.",
            "Projection test beginning.",
            DateTimeOffset.Parse("2026-08-11T00:00:00Z"));
        foreach (var pair in files) await WriteJsonAsync(fileSystem, pair.Key, pair.Value);
        var resources = ResourceBootstrapStateBuilder.BuildPristine();
        Assert.True(resources.IsValid, string.Join(Environment.NewLine, resources.Issues));
        await WriteJsonAsync(fileSystem, ResourceMaterializationContract.DefinitionsPath,
            JsonNode.Parse(resources.Definitions!.ToCanonicalJson())!);
        await WriteJsonAsync(fileSystem, ResourceMaterializationContract.StatePath,
            JsonNode.Parse(resources.State!.ToCanonicalJson())!);
        await WriteJsonAsync(fileSystem, ResourceMaterializationContract.HistoryPath,
            JsonNode.Parse(resources.History!.ToCanonicalJson())!);
        var authority = await CanonicalResourceOwnerAuthorityComposer.ComposeAsync(
            resources.Definitions, fileSystem.ReadFileAsync, resources.State,
            resources.History, CanonicalResourceOwnerAuthorityPurpose.ExplicitBootstrap);
        Assert.True(authority.IsValid, string.Join(Environment.NewLine, authority.Issues));
        await WriteJsonAsync(fileSystem, CanonicalResourceOwnerAuthorityComposer.AuthorityPath,
            JsonNode.Parse(authority.CanonicalAuthorityJson!)!);
    }

    private static async Task CapturePendingSnapshotAsync(
        FileSystemManager fileSystem, string sessionId, int turn)
    {
        const string requestId = "request_t070b4_projection";
        const string playerAction = "Validate projection create and transfer.";
        await WriteJsonAsync(fileSystem, "input/turn_request.json", new JsonObject
        {
            ["sessionId"] = sessionId,
            ["requestId"] = requestId,
            ["turnNumber"] = turn,
            ["playerAction"] = playerAction
        });
        var files = new JsonObject();
        var hashes = new JsonObject();
        var baselines = new JsonArray();
        foreach (var path in CanonicalStateNormalizer.NormalizerRollbackTrackedFiles
                     .Distinct(StringComparer.OrdinalIgnoreCase)
                     .OrderBy(value => value, StringComparer.Ordinal))
        {
            var bytes = await fileSystem.ReadFileBytesAsync(path);
            if (bytes == null) continue;
            var snapshotPath = $"game_state/control/pending_turn_snapshot/{path}";
            await fileSystem.WriteFileAtomicBytesAsync(snapshotPath, bytes);
            files[path] = snapshotPath;
            hashes[path] = PendingTurnSnapshotAuthority.ComputeSha256(bytes);
            baselines.Add(path);
        }
        var manifest = new JsonObject
        {
            ["sessionId"] = sessionId,
            ["requestId"] = requestId,
            ["turnNumber"] = turn,
            ["requestTimestamp"] = "2026-08-11T00:00:00Z",
            ["playerAction"] = playerAction,
            ["files"] = files,
            ["snapshotFileHashes"] = hashes,
            ["clientOwnedValidationHashes"] = new JsonObject(),
            ["rollbackBackups"] = new JsonObject(),
            ["rollbackBaselineFiles"] = baselines,
            ["sourceLabel"] = "T070-B.4 projection oracle",
            ["manifestPayloadHash"] = string.Empty
        };
        manifest["manifestPayloadHash"] =
            PendingTurnSnapshotTestAuthority.ComputeManifestPayloadHash(manifest);
        await WriteJsonAsync(fileSystem,
            "game_state/control/pending_turn_snapshot.json", manifest);
        await PendingTurnSnapshotTestAuthority
            .SyncAuthorityForCurrentManifestAsync(fileSystem);
    }

    private static async Task<string> PendingSnapshotTokenAsync(FileSystemManager fileSystem)
    {
        var json = await fileSystem.ReadFileAsync(
            "game_state/control/pending_turn_snapshot.json");
        Assert.NotNull(json);
        return JsonNode.Parse(json!)!["manifestPayloadHash"]!.GetValue<string>();
    }

    private static JsonObject ProjectionNpcRoot(string npcId, JsonObject item) => new()
    {
        ["UpdateNPCs"] = new JsonArray(),
        ["NPCsInScene"] = new JsonArray(new JsonObject
        {
            ["NPCId"] = npcId,
            ["name"] = "Projection owner",
            ["inventory"] = new JsonArray(item.DeepClone()),
            ["equippedItems"] = new JsonObject
            {
                ["mainHand"] = item["itemId"]!.GetValue<string>()
            }
        })
    };

    /// <summary>
    /// Writes the supplied JSON node atomically to an isolated fixture path.
    /// </summary>
    /// <param name="fileSystem">
    /// The fresh filesystem owning the fixture files.
    /// </param>
    /// <param name="path">
    /// The fixture-relative JSON output path.
    /// </param>
    /// <param name="node">
    /// The JSON node to serialize; <see langword="null"/> is not supported.
    /// </param>
    /// <returns>
    /// A task completing after the JSON file has been published.
    /// </returns>
    private protected static Task WriteJsonAsync(
        FileSystemManager fileSystem, string path, JsonNode node) =>
        fileSystem.WriteFileAtomicAsync(path, node.ToJsonString());

    /// <summary>
    /// Finds the exact snapshot-owned identity value for a creation reference and identity prefix.
    /// </summary>
    /// <param name="snapshot">
    /// The accepted item normalization snapshot.
    /// </param>
    /// <param name="expectedKey">
    /// The exact creation reference to locate.
    /// </param>
    /// <param name="valuePrefix">
    /// The identity prefix identifying the required snapshot-owned map.
    /// </param>
    /// <returns>
    /// The unique snapshot-owned identity value.
    /// </returns>
    private protected static string SnapshotOwnedMapValue(
        MortalItemAcceptedTurnNormalizationSnapshot snapshot,
        string expectedKey,
        string valuePrefix)
    {
        var matches = new HashSet<string>(StringComparer.Ordinal);
        var visited = new HashSet<object>(ReferenceEqualityComparer.Instance);
        Visit(snapshot);
        return Assert.Single(matches);

        void Visit(object? value)
        {
            if (value == null || value is string || !visited.Add(value)) return;
            if (value is IDictionary dictionary)
            {
                foreach (DictionaryEntry entry in dictionary)
                {
                    Match(entry.Key, entry.Value);
                    Visit(entry.Key); Visit(entry.Value);
                }
                return;
            }
            if (value is IEnumerable enumerable)
            {
                foreach (var entry in enumerable)
                {
                    if (entry != null)
                    {
                        var pairType = entry.GetType();
                        if (pairType.IsGenericType &&
                            pairType.GetGenericTypeDefinition() == typeof(KeyValuePair<,>))
                            Match(pairType.GetProperty("Key")!.GetValue(entry),
                                pairType.GetProperty("Value")!.GetValue(entry));
                    }
                    Visit(entry);
                }
                return;
            }
            var type = value.GetType();
            if (type.IsPrimitive || type.IsEnum || type == typeof(decimal) ||
                type == typeof(DateTime) || type == typeof(DateTimeOffset)) return;
            foreach (var field in type.GetFields(
                         BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public))
                Visit(field.GetValue(value));
        }

        void Match(object? key, object? value)
        {
            if (key is string text && value is string id &&
                string.Equals(text, expectedKey, StringComparison.Ordinal) &&
                id.StartsWith(valuePrefix, StringComparison.Ordinal))
                matches.Add(id);
        }
    }

    /// <summary>
    /// Verifies the exact production item projection input API shape.
    /// </summary>
    /// <param name="type">
    /// The reflected production type whose API shape is checked.
    /// </param>
    private protected static void ProjectionInputShape(Type type)
    {
        Property(type, "Turn", typeof(int));
        Property(type, "Snapshot", typeof(MortalItemAcceptedTurnNormalizationSnapshot));
        Property(type, "RouteCatalog", typeof(MortalItemRouteAuthorityCatalog));
        Property(type, "CurrentRoots", typeof(IReadOnlyDictionary<string, JsonNode?>));
        Property(type, "BackupRoots", typeof(IReadOnlyDictionary<string, JsonNode?>));
        Property(type, "IdentityState", typeof(MortalItemIdentityParseResult));
    }

    /// <summary>
    /// Verifies the exact production item projection result API shape.
    /// </summary>
    /// <param name="type">
    /// The reflected production type whose API shape is checked.
    /// </param>
    private protected static void ProjectionResultShape(Type type)
    {
        Property(type, "ItemPhaseAfterImages", typeof(IReadOnlyDictionary<string, JsonNode?>));
        Property(type, "IdentityIndexAfterImage", typeof(JsonObject));
        Property(type, "Issues", typeof(IReadOnlyList<ValidationIssue>));
        Property(type, "Fingerprint", typeof(string));
        Property(type, "IsValid", typeof(bool));
    }

    /// <summary>
    /// Reads the carrier roots, identity index and diagnostics of a production projection result.
    /// </summary>
    /// <param name="value">
    /// The production projection result returned by the reflected planner.
    /// </param>
    /// <returns>
    /// The projected roots, identity, issues, fingerprint and validity result.
    /// </returns>
    private protected static ProjectionOutput ProjectionResult(object value) => new(
        Read<IReadOnlyDictionary<string, JsonNode?>>(value, "ItemPhaseAfterImages"),
        Read<JsonObject>(value, "IdentityIndexAfterImage"),
        Read<IReadOnlyList<ValidationIssue>>(value, "Issues"),
        Read<string>(value, "Fingerprint"),
        Read<bool>(value, "IsValid"));

    /// <summary>
    /// Verifies a rejected projection has issues, no carrier after-images and a formatted fingerprint.
    /// </summary>
    /// <param name="result">
    /// The rejected item projection result to check.
    /// </param>
    private protected static void AssertProjectionInvalidEmpty(ProjectionOutput result)
    {
        Assert.False(result.IsValid);
        Assert.NotEmpty(result.Issues);
        Assert.Empty(result.Roots);
        AssertFingerprint(result.Fingerprint);
    }

    /// <summary>
    /// Verifies a valid consumption plan has carrier/index after-images, no issues and a formatted fingerprint.
    /// </summary>
    /// <param name="result">
    /// The accepted consumption-planning result to check.
    /// </param>
    private protected static void AssertValid(Result result)
    {
        Assert.True(result.IsValid, string.Join(
            Environment.NewLine,
            result.Issues.Select(issue => $"{issue.Code}: {issue.Actual}")));
        Assert.Empty(result.Issues);
        Assert.NotNull(result.Index);
        Assert.NotEmpty(result.Roots);
        AssertFingerprint(result.Fingerprint);
    }

    /// <summary>
    /// Verifies an invalid item-consumption plan exposes no publication changes.
    /// </summary>
    /// <param name="result">
    /// The rejected consumption-planning result to check.
    /// </param>
    private protected static void AssertInvalidEmpty(Result result)
    {
        Assert.False(result.IsValid);
        Assert.NotEmpty(result.Issues);
        Assert.Empty(result.Roots);
        Assert.Null(result.Index);
        Assert.Empty(result.Transitions);
        Assert.Empty(result.Capacities);
        Assert.Empty(result.TerminalOwners);
        AssertFingerprint(result.Fingerprint);
    }

    /// <summary>
    /// Verifies exact consumption transition identity, quantities and carrier coordinates.
    /// </summary>
    /// <param name="value">
    /// The consumption identity transition object to check.
    /// </param>
    /// <param name="transitionId">
    /// The exact transition identifier.
    /// </param>
    /// <param name="itemId">
    /// The exact consumed item identifier.
    /// </param>
    /// <param name="before">
    /// The original stack count.
    /// </param>
    /// <param name="after">
    /// The remaining stack count.
    /// </param>
    /// <param name="source">
    /// The original carrier coordinate.
    /// </param>
    /// <param name="destination">
    /// The destination carrier coordinate; <see langword="null"/> represents terminal consumption.
    /// </param>
    private protected static void AssertConsume(JsonObject value, string transitionId, string itemId,
        int before, int after, JsonNode source, JsonNode? destination)
    {
        Assert.Equal(transitionId, value["transitionId"]!.GetValue<string>());
        Assert.Equal("consume", value["kind"]!.GetValue<string>());
        Assert.Equal(Turn, value["turn"]!.GetValue<int>());
        Assert.Equal(new[] { itemId }, value["sourceItemIds"]!.AsArray()
            .Select(node => node!.GetValue<string>()));
        Assert.True(JsonNode.DeepEquals(source, value["sourceCarrier"]));
        Assert.True(JsonNode.DeepEquals(destination, value["destinationCarrier"]));
        Assert.Equal(before, value["quantityBefore"]!.GetValue<int>());
        Assert.Equal(after, value["quantityAfter"]!.GetValue<int>());
        Assert.Equal("mortal_wound_treatment", value["authorityKind"]!.GetValue<string>());
        Assert.StartsWith("mwta_t070b4_finalization_", value["authorityId"]!.GetValue<string>());
    }

    /// <summary>
    /// Compares complete item-consumption result semantics and root after-images.
    /// </summary>
    /// <param name="expected">
    /// The expected consumption-planning result.
    /// </param>
    /// <param name="actual">
    /// The actual consumption-planning result.
    /// </param>
    private protected static void AssertPlanEqual(Result expected, Result actual)
    {
        AssertRoots(expected.Roots, actual.Roots);
        Assert.True(JsonNode.DeepEquals(expected.Index, actual.Index));
        Assert.Equal(expected.Transitions.Count, actual.Transitions.Count);
        for (var i = 0; i < expected.Transitions.Count; i++)
            Assert.True(JsonNode.DeepEquals(expected.Transitions[i], actual.Transitions[i]));
        Assert.Equal(expected.Capacities.Count, actual.Capacities.Count);
        for (var i = 0; i < expected.Capacities.Count; i++)
            AssertCapacity(expected.Capacities[i], actual.Capacities[i]);
        Assert.Equal(expected.TerminalOwners, actual.TerminalOwners);
        Assert.Equal(expected.Issues.Count, actual.Issues.Count);
        for (var i = 0; i < expected.Issues.Count; i++) AssertIssue(expected.Issues[i], actual.Issues[i]);
        Assert.Equal(expected.Fingerprint, actual.Fingerprint);
        Assert.Equal(expected.IsValid, actual.IsValid);
    }

    /// <summary>
    /// Compares complete item projection semantics and root after-images.
    /// </summary>
    /// <param name="expected">
    /// The expected item projection result.
    /// </param>
    /// <param name="actual">
    /// The actual item projection result.
    /// </param>
    private protected static void AssertProjectionEqual(ProjectionOutput expected, ProjectionOutput actual)
    {
        AssertProjectionRoots(expected.Roots, actual.Roots);
        Assert.True(JsonNode.DeepEquals(expected.Index, actual.Index));
        Assert.Equal(expected.Issues.Count, actual.Issues.Count);
        for (var i = 0; i < expected.Issues.Count; i++) AssertIssue(expected.Issues[i], actual.Issues[i]);
        Assert.Equal(expected.Fingerprint, actual.Fingerprint);
        Assert.Equal(expected.IsValid, actual.IsValid);
    }

    private static void AssertRoots(IReadOnlyDictionary<string, JsonObject> expected,
        IReadOnlyDictionary<string, JsonObject> actual)
    {
        Assert.Equal(expected.Keys.OrderBy(v => v, StringComparer.Ordinal),
            actual.Keys.OrderBy(v => v, StringComparer.Ordinal));
        foreach (var pair in expected)
            Assert.True(JsonNode.DeepEquals(pair.Value, actual[pair.Key]), pair.Key);
    }

    private static void AssertProjectionRoots(
        IReadOnlyDictionary<string, JsonNode?> expected,
        IReadOnlyDictionary<string, JsonNode?> actual)
    {
        Assert.Equal(expected.Keys.OrderBy(v => v, StringComparer.Ordinal),
            actual.Keys.OrderBy(v => v, StringComparer.Ordinal));
        foreach (var pair in expected)
            Assert.True(JsonNode.DeepEquals(pair.Value, actual[pair.Key]), pair.Key);
    }

    private static void AssertCapacity(ResourceCapacityIntent expected, ResourceCapacityIntent actual)
    {
        Assert.Equal(expected.EventRef, actual.EventRef);
        Assert.Equal(expected.OriginKind, actual.OriginKind);
        Assert.Equal(expected.OriginId, actual.OriginId);
        Assert.Equal(expected.Coordinate, actual.Coordinate);
        Assert.Equal(expected.Operation, actual.Operation);
        Assert.Equal(expected.ResolvedCapacity?.Maximum, actual.ResolvedCapacity?.Maximum);
        Assert.Equal(expected.ResolvedCapacity?.Binding, actual.ResolvedCapacity?.Binding);
        Assert.Equal(expected.ResolvedCapacity?.Initialization, actual.ResolvedCapacity?.Initialization);
        Assert.Equal(expected.CurrentDisposition, actual.CurrentDisposition);
        Assert.Equal(expected.Phase, actual.Phase);
        Assert.Equal(expected.Priority, actual.Priority);
        Assert.Equal(expected.SourceEvidence, actual.SourceEvidence);
        Assert.Equal(expected.PolicyFingerprint, actual.PolicyFingerprint);
        Assert.Equal(expected.ReceiptId, actual.ReceiptId);
    }

    private static void AssertIssue(ValidationIssue expected, ValidationIssue actual)
    {
        Assert.Equal(expected.FilePath, actual.FilePath);
        Assert.Equal(expected.Severity, actual.Severity);
        Assert.Equal(expected.Message, actual.Message);
        Assert.Equal(expected.Category, actual.Category);
        Assert.Equal(expected.Code, actual.Code);
        Assert.Equal(expected.Actor, actual.Actor);
        Assert.Equal(expected.Section, actual.Section);
        Assert.Equal(expected.Expected, actual.Expected);
        Assert.Equal(expected.Actual, actual.Actual);
        Assert.Equal(expected.RepairHint, actual.RepairHint);
        Assert.Equal(expected.RepairTargetFiles, actual.RepairTargetFiles);
        Assert.Equal(expected.FactionRepairClassification, actual.FactionRepairClassification);
        Assert.Equal(expected.MortalItemRepairContext, actual.MortalItemRepairContext);
        Assert.Equal(expected.MortalLocationRepairContext, actual.MortalLocationRepairContext);
        Assert.Equal(expected.EffectRepairContext, actual.EffectRepairContext);
        Assert.Equal(expected.WoundRepairContext, actual.WoundRepairContext);
    }

    /// <summary>
    /// Clones every item carrier root without retaining mutable input nodes.
    /// </summary>
    /// <param name="value">
    /// The carrier catalog input whose root nodes are cloned.
    /// </param>
    /// <returns>
    /// The detached carrier catalog input.
    /// </returns>
    private protected static MortalItemCarrierCatalogInput Clone(MortalItemCarrierCatalogInput value) => new(
        value.PlayerInventory?.DeepClone().AsObject(), value.NpcCore?.DeepClone().AsObject(),
        value.NpcInventoryCommands?.DeepClone().AsObject(), value.CurrentLocation?.DeepClone().AsObject(),
        value.Vehicles?.DeepClone().AsObject(), value.CompanionRoots.ToDictionary(
            pair => pair.Key, pair => pair.Value.DeepClone().AsObject(), StringComparer.Ordinal),
        value.OffscreenLocationStorageContents?.DeepClone().AsObject());

    /// <summary>
    /// Formats the fixture carrier roots, identity index and resource state for assertion diagnostics.
    /// </summary>
    /// <param name="fixture">
    /// The isolated item fixture and its authority inputs.
    /// </param>
    /// <returns>
    /// The canonical fixture roots and resource state formatted as diagnostic text.
    /// </returns>
    private protected static string Describe(Fixture fixture) => string.Join("|",
        RootMap(fixture.Roots).OrderBy(pair => pair.Key, StringComparer.Ordinal)
            .Select(pair => pair.Key + "=" + WoundAcceptedTurnFingerprintWriter.CanonicalJson(pair.Value))
            .Append(WoundAcceptedTurnFingerprintWriter.CanonicalJson(fixture.Index))
            .Append(fixture.State.ToCanonicalJson()).Append(fixture.State.Fingerprint));

    /// <summary>
    /// Builds a path-indexed view of the present item carrier and companion roots.
    /// </summary>
    /// <param name="roots">
    /// The carrier input containing present item and companion roots.
    /// </param>
    /// <returns>
    /// The present roots indexed by their governed paths; JSON nodes are not cloned.
    /// </returns>
    private protected static IReadOnlyDictionary<string, JsonObject> RootMap(MortalItemCarrierCatalogInput roots)
    {
        var result = new Dictionary<string, JsonObject>(StringComparer.Ordinal);
        Add(PlayerPath, roots.PlayerInventory); Add(NpcPath, roots.NpcCore);
        Add(NpcCommandsPath, roots.NpcInventoryCommands);
        Add("game_state/world/current_location.json", roots.CurrentLocation);
        Add("game_state/misc/vehicles.json", roots.Vehicles);
        Add("game_state/world/location_storage_contents.json", roots.OffscreenLocationStorageContents);
        foreach (var pair in roots.CompanionRoots) result.Add(pair.Key, pair.Value);
        return result;
        void Add(string path, JsonObject? root) { if (root != null) result.Add(path, root); }
    }

    /// <summary>
    /// Finds the unique canonical materialized item in the supplied roots.
    /// </summary>
    /// <param name="roots">
    /// The path-indexed JSON roots searched for the materialized item.
    /// </param>
    /// <param name="id">
    /// The exact item identifier.
    /// </param>
    /// <returns>
    /// The item object with the exact identifier and materialization receipt.
    /// </returns>
    private protected static JsonObject FindItem(IReadOnlyDictionary<string, JsonObject> roots, string id) =>
        Assert.Single(roots.Values.SelectMany(Objects), value =>
            string.Equals(value["itemId"]?.GetValue<string>(), id, StringComparison.Ordinal) &&
            value["materializationReceipt"] is JsonObject);

    /// <summary>
    /// Finds the unique canonical materialized item in the supplied roots.
    /// </summary>
    /// <param name="roots">
    /// The path-indexed JSON roots searched for the materialized item.
    /// </param>
    /// <param name="id">
    /// The exact item identifier.
    /// </param>
    /// <returns>
    /// The item object with the exact identifier and materialization receipt.
    /// </returns>
    private protected static JsonObject FindItem(IReadOnlyDictionary<string, JsonNode?> roots, string id) =>
        Assert.Single(roots.Values.SelectMany(Objects), value =>
            string.Equals(value["itemId"]?.GetValue<string>(), id, StringComparison.Ordinal) &&
            value["materializationReceipt"] is JsonObject);

    /// <summary>
    /// Checks whether the supplied roots contain a canonical materialized item.
    /// </summary>
    /// <param name="roots">
    /// The path-indexed JSON roots searched for the materialized item.
    /// </param>
    /// <param name="id">
    /// The exact item identifier.
    /// </param>
    /// <returns>
    /// <see langword="true"/> when a matching item with a receipt exists; otherwise, <see langword="false"/>.
    /// </returns>
    private protected static bool HasItem(IReadOnlyDictionary<string, JsonObject> roots, string id) =>
        roots.Values.SelectMany(Objects).Any(value =>
            string.Equals(value["itemId"]?.GetValue<string>(), id, StringComparison.Ordinal) &&
            value["materializationReceipt"] is JsonObject);

    /// <summary>
    /// Finds matching NPC rows in both supported NPC state surfaces.
    /// </summary>
    /// <param name="root">
    /// The NPC core root containing UpdateNPCs and NPCsInScene surfaces.
    /// </param>
    /// <param name="npcId">
    /// The exact NPC identifier.
    /// </param>
    /// <returns>
    /// The NPC rows with the exact supplied identifier.
    /// </returns>
    private protected static IReadOnlyList<JsonObject> Npcs(JsonObject root, string npcId) =>
        new[] { "UpdateNPCs", "NPCsInScene" }
            .SelectMany(section => root[section] is JsonArray values
                ? values.OfType<JsonObject>()
                : Enumerable.Empty<JsonObject>())
            .Where(npc => string.Equals(
                npc["NPCId"]?.GetValue<string>(),
                npcId,
                StringComparison.Ordinal))
            .ToArray();

    private static IEnumerable<JsonObject> Objects(JsonNode? node)
    {
        if (node is JsonObject obj)
        {
            yield return obj;
            foreach (var child in obj.SelectMany(pair => Objects(pair.Value))) yield return child;
        }
        else if (node is JsonArray array)
            foreach (var child in array.SelectMany(Objects)) yield return child;
    }

    /// <summary>
    /// Enumerates string values recursively from a JSON fixture node.
    /// </summary>
    /// <param name="node">
    /// The JSON node to inspect; <see langword="null"/> produces an empty sequence.
    /// </param>
    /// <returns>
    /// The nested string values; an empty sequence for a <see langword="null"/> node.
    /// </returns>
    private protected static IEnumerable<string> Strings(JsonNode? node)
    {
        if (node is JsonObject obj)
            foreach (var value in obj.SelectMany(pair => Strings(pair.Value))) yield return value;
        else if (node is JsonArray array)
            foreach (var value in array.SelectMany(Strings)) yield return value;
        else if (node is JsonValue value && value.TryGetValue<string>(out var text)) yield return text;
    }

    /// <summary>
    /// Finds the unique item identity entry by exact identifier.
    /// </summary>
    /// <param name="root">
    /// The item identity root containing the entries array.
    /// </param>
    /// <param name="id">
    /// The exact item identifier.
    /// </param>
    /// <returns>
    /// The matching identity entry object.
    /// </returns>
    private protected static JsonObject Entry(JsonObject root, string id) =>
        Assert.Single(root["entries"]!.AsArray().OfType<JsonObject>(), value =>
            string.Equals(value["itemId"]?.GetValue<string>(), id, StringComparison.Ordinal));
    /// <summary>
    /// Reads the last transition from an identity entry.
    /// </summary>
    /// <param name="entry">
    /// The identity entry containing ordered transitions.
    /// </param>
    /// <returns>
    /// The final transition object in the entry.
    /// </returns>
    private protected static JsonObject LastTransition(JsonObject entry) =>
        Assert.IsType<JsonObject>(entry["transitions"]!.AsArray()[^1]);
    private static JsonObject MergeIndexes(params JsonObject[] indexes) => new()
    {
        ["schemaVersion"] = 1,
        ["entries"] = new JsonArray(indexes.SelectMany(index => index["entries"]!.AsArray())
            .Select(value => value!.DeepClone()).ToArray())
    };
    /// <summary>
    /// Marks the selected item materialization section populated without a reason.
    /// </summary>
    /// <param name="item">
    /// The canonical item object.
    /// </param>
    /// <param name="section">
    /// The materialization section to mark populated.
    /// </param>
    private protected static void Populate(JsonObject item, string section) =>
        item["materialization"]!["sections"]![section] = new JsonObject
        { ["state"] = "populated", ["reason"] = null };
    /// <summary>
    /// Resolves and asserts an exact resource definition key.
    /// </summary>
    /// <param name="catalog">
    /// The resource definition catalog queried by exact key.
    /// </param>
    /// <param name="key">
    /// The exact resource definition key.
    /// </param>
    /// <returns>
    /// The resolved resource definition.
    /// </returns>
    private protected static ResourceDefinition Definition(ResourceDefinitionCatalog catalog, string key)
    { Assert.True(catalog.TryResolveExact(key, out var value)); return Assert.IsType<ResourceDefinition>(value); }
    /// <summary>
    /// Scales a resource value by the remaining stack ratio and checks exact quantum alignment.
    /// </summary>
    /// <param name="value">
    /// The original resource value scaled by the remaining stack ratio.
    /// </param>
    /// <param name="remaining">
    /// The remaining stack quantity.
    /// </param>
    /// <param name="source">
    /// The original positive stack quantity used as the divisor.
    /// </param>
    /// <param name="quantum">
    /// The resource quantum required to divide the scaled value exactly.
    /// </param>
    /// <returns>
    /// The proportionally scaled value aligned to the supplied quantum.
    /// </returns>
    private protected static decimal ExactScale(decimal value, int remaining, int source, decimal quantum)
    { var result = checked(value * remaining) / source; Assert.Equal(0m, result % quantum); return result; }
    /// <summary>
    /// Verifies an event reference contains exactly one formatted collector ordinal.
    /// </summary>
    /// <param name="eventRef">
    /// The event reference carrying the formatted ordinal.
    /// </param>
    /// <param name="ordinal">
    /// The expected number formatted with at least four decimal digits.
    /// </param>
    private protected static void AssertOrdinal(string eventRef, int ordinal) => Assert.Single(Regex.Matches(
        eventRef, $@"(?<!\d){ordinal:D4}(?!\d)"));

    /// <summary>
    /// Finds the exact type in the production item identity assembly.
    /// </summary>
    /// <param name="fullName">
    /// The exact full production type name.
    /// </param>
    /// <returns>
    /// The production type with the supplied full name.
    /// </returns>
    private protected static Type ExactType(string fullName)
    {
        var assembly = typeof(MortalItemIdentityState).Assembly;
        var candidate = assembly.GetType(fullName, false, false);
        Assert.NotNull(candidate);
        Assert.Equal(fullName, candidate!.FullName);
        Assert.Same(assembly, candidate.Assembly);
        return candidate;
    }
    /// <summary>
    /// Finds the exact internal static single-input production method.
    /// </summary>
    /// <param name="owner">
    /// The production type containing the required planning method.
    /// </param>
    /// <param name="name">
    /// The production method name.
    /// </param>
    /// <param name="input">
    /// The required single production parameter type.
    /// </param>
    /// <param name="result">
    /// The required return type; <see langword="null"/> accepts any return type.
    /// </param>
    /// <returns>
    /// The matching production method descriptor.
    /// </returns>
    private protected static MethodInfo ExactMethod(Type owner, string name, Type input, Type? result = null)
    {
        var method = Assert.Single(owner.GetMethods(BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public),
            value => value.Name == name && value.GetParameters().Length == 1 &&
                     value.GetParameters()[0].ParameterType == input &&
                     (result == null || value.ReturnType == result));
        Assert.True(method.IsStatic && method.IsAssembly);
        return method;
    }
    private static ConstructorInfo Ctor(Type type, params Type[] parameters)
    {
        var value = type.GetConstructor(BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public,
            null, parameters, null); Assert.NotNull(value); return value!;
    }
    private static PropertyInfo Property(Type type, string name, Type expected)
    {
        var value = type.GetProperty(name, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
        Assert.NotNull(value); Assert.Equal(expected, value!.PropertyType); return value;
    }
    private static T Read<T>(object value, string name) => Assert.IsAssignableFrom<T>(
        value.GetType().GetProperty(name, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public)!
            .GetValue(value));
    private static JsonObject? ReadNullableObject(object value, string name)
    {
        var result = value.GetType().GetProperty(name,
            BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public)!.GetValue(value);
        return result == null ? null : Assert.IsType<JsonObject>(result);
    }
    /// <summary>
    /// Verifies a fingerprint uses the expected SHA-256 textual shape.
    /// </summary>
    /// <param name="value">
    /// The fingerprint whose textual format is checked.
    /// </param>
    private protected static void AssertFingerprint(string value) => Assert.True(
        ResourceMaterializationContract.IsAuthorityFingerprint(value),
        $"Expected sha256 plus 64 lowercase hexadecimal digits, got '{value}'.");
    /// <summary>
    /// Creates a prefixed SHA-256 fingerprint from fixture text.
    /// </summary>
    /// <param name="value">
    /// The fixture text hashed as UTF-8.
    /// </param>
    /// <returns>
    /// The lowercase SHA-256 fingerprint with its schema prefix.
    /// </returns>
    private protected static string Fingerprint(string value) => "sha256:" + Hex(value);
    /// <summary>
    /// Hashes fixture text as lowercase SHA-256 hexadecimal.
    /// </summary>
    /// <param name="value">
    /// The fixture text hashed as UTF-8.
    /// </param>
    /// <returns>
    /// The lowercase hexadecimal hash of the UTF-8 text.
    /// </returns>
    private protected static string Hex(string value) => Convert.ToHexString(
        SHA256.HashData(Encoding.UTF8.GetBytes(value))).ToLowerInvariant();

    /// <summary>
    /// Captures fixture files for exact before-and-after byte comparisons.
    /// </summary>
    /// <param name="root">
    /// The existing owned fixture directory whose files are captured.
    /// </param>
    /// <returns>
    /// The captured file bytes indexed by relative path.
    /// </returns>
    private protected static IReadOnlyDictionary<string, byte[]> ReadFiles(string root)
    {
        Assert.True(Directory.Exists(root), root);
        return Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories)
            .OrderBy(value => value, StringComparer.Ordinal).ToDictionary(
                value => Path.GetRelativePath(root, value).Replace('\\', '/'),
                File.ReadAllBytes, StringComparer.Ordinal);
    }
    /// <summary>
    /// Compares the exact path set and bytes of two fixture trees.
    /// </summary>
    /// <param name="expected">
    /// The expected relative file paths and their exact bytes.
    /// </param>
    /// <param name="actual">
    /// The actual relative file paths and their exact bytes.
    /// </param>
    private protected static void AssertFilesEqual(IReadOnlyDictionary<string, byte[]> expected,
        IReadOnlyDictionary<string, byte[]> actual)
    {
        Assert.Equal(expected.Keys, actual.Keys);
        foreach (var pair in expected) Assert.True(pair.Value.SequenceEqual(actual[pair.Key]), pair.Key);
    }
    /// <summary>
    /// Finds the repository root used by source inventory assertions.
    /// </summary>
    /// <returns>
    /// The repository root containing the AGENTS.md and FileSystemExample markers.
    /// </returns>
    private protected static string FindRepo()
    {
        for (var current = new DirectoryInfo(AppContext.BaseDirectory); current != null; current = current.Parent)
            if (File.Exists(Path.Combine(current.FullName, "AGENTS.md")) &&
                Directory.Exists(Path.Combine(current.FullName, "FileSystemExample"))) return current.FullName;
        throw new DirectoryNotFoundException("Repository root was not found.");
    }

    /// <summary>
    /// Holds detached item, carrier, identity and resource authority for one case.
    /// </summary>
    /// <param name="ItemId">
    /// The exact fixture item identifier.
    /// </param>
    /// <param name="Roots">
    /// The detached governed carrier roots.
    /// </param>
    /// <param name="Index">
    /// The detached identity index root.
    /// </param>
    /// <param name="Identity">
    /// The parsed identity authority.
    /// </param>
    /// <param name="Definitions">
    /// The registered resource definitions.
    /// </param>
    /// <param name="State">
    /// The item resource state ledger.
    /// </param>
    /// <param name="Source">
    /// The exact resource source evidence.
    /// </param>
    /// <param name="PolicyFingerprint">
    /// The resource capacity policy fingerprint.
    /// </param>
    private protected sealed record Fixture(string ItemId, MortalItemCarrierCatalogInput Roots,
        JsonObject Index, MortalItemIdentityParseResult Identity,
        ResourceDefinitionCatalog Definitions, ResourceStateLedger State,
        ResourceSourceEvidence Source, string PolicyFingerprint);
    /// <summary>
    /// Pairs a companion path with its configured item fixture.
    /// </summary>
    /// <param name="Path">
    /// The governed companion path.
    /// </param>
    /// <param name="Fixture">
    /// The configured isolated item fixture.
    /// </param>
    private protected sealed record Companion(string Path, Fixture Fixture);
    /// <summary>
    /// Holds the ordered consumption request and exact authority coordinates.
    /// </summary>
    /// <param name="Ordinal">
    /// The ordered consumption command ordinal.
    /// </param>
    /// <param name="ItemId">
    /// The exact fixture item identifier.
    /// </param>
    /// <param name="Quantity">
    /// The consumed stack quantity.
    /// </param>
    /// <param name="ClaimFingerprint">
    /// The exact consumption claim fingerprint.
    /// </param>
    /// <param name="TransitionId">
    /// The exact consumption transition identifier.
    /// </param>
    /// <param name="AuthorityKind">
    /// The consumption authority kind.
    /// </param>
    /// <param name="AuthorityId">
    /// The consumption authority identifier.
    /// </param>
    private protected sealed record Command(int Ordinal, string ItemId, int Quantity,
        string ClaimFingerprint, string TransitionId, string AuthorityKind, string AuthorityId);
    /// <summary>
    /// Holds detached item-consumption after-images, resource consequences and validation results.
    /// </summary>
    /// <param name="Roots">
    /// The detached governed carrier roots.
    /// </param>
    /// <param name="Index">
    /// The detached identity index root.
    /// </param>
    /// <param name="Transitions">
    /// The ordered identity transitions.
    /// </param>
    /// <param name="Capacities">
    /// The resulting resource capacity intents.
    /// </param>
    /// <param name="TerminalOwners">
    /// The item owners terminal after consumption.
    /// </param>
    /// <param name="Issues">
    /// The planner validation issues.
    /// </param>
    /// <param name="Fingerprint">
    /// The complete planning result fingerprint.
    /// </param>
    /// <param name="IsValid">
    /// Whether the planner accepted the input without issues.
    /// </param>
    private protected sealed record Result(IReadOnlyDictionary<string, JsonObject> Roots, JsonObject? Index,
        IReadOnlyList<JsonObject> Transitions, IReadOnlyList<ResourceCapacityIntent> Capacities,
        IReadOnlyList<ResourceOwnerKey> TerminalOwners, IReadOnlyList<ValidationIssue> Issues,
        string Fingerprint, bool IsValid);
    /// <summary>
    /// Holds detached signed snapshot and projection inputs from a fresh filesystem scenario.
    /// </summary>
    /// <param name="Turn">
    /// The original signed turn number.
    /// </param>
    /// <param name="Snapshot">
    /// The accepted normalization snapshot.
    /// </param>
    /// <param name="RouteCatalog">
    /// The validated item route authority.
    /// </param>
    /// <param name="CurrentRoots">
    /// The detached current projection input roots.
    /// </param>
    /// <param name="BackupRoots">
    /// The detached authenticated original roots.
    /// </param>
    /// <param name="IdentityRoot">
    /// The detached current identity root.
    /// </param>
    /// <param name="CreatedItemId">
    /// The expected materialized creation identity.
    /// </param>
    /// <param name="CreationRef">
    /// The exact original creation reference.
    /// </param>
    /// <param name="TransferredItemId">
    /// The existing transferred item identifier.
    /// </param>
    private protected sealed record ProjectionScenario(int Turn,
        MortalItemAcceptedTurnNormalizationSnapshot Snapshot,
        MortalItemRouteAuthorityCatalog RouteCatalog,
        IReadOnlyDictionary<string, JsonNode?> CurrentRoots,
        IReadOnlyDictionary<string, JsonNode?> BackupRoots,
        JsonObject IdentityRoot,
        string CreatedItemId,
        string CreationRef,
        string TransferredItemId);
    /// <summary>
    /// Holds detached item projection after-images and validation results.
    /// </summary>
    /// <param name="Roots">
    /// The detached governed carrier roots.
    /// </param>
    /// <param name="Index">
    /// The detached identity index root.
    /// </param>
    /// <param name="Issues">
    /// The planner validation issues.
    /// </param>
    /// <param name="Fingerprint">
    /// The complete planning result fingerprint.
    /// </param>
    /// <param name="IsValid">
    /// Whether the planner accepted the input without issues.
    /// </param>
    private protected sealed record ProjectionOutput(IReadOnlyDictionary<string, JsonNode?> Roots,
        JsonObject Index, IReadOnlyList<ValidationIssue> Issues, string Fingerprint, bool IsValid);
}
