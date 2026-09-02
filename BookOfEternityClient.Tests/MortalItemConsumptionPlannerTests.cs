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

/// <summary>Durable RED oracle for the frozen T070-B.4 pure item planners.</summary>
public sealed class MortalItemConsumptionPlannerTests
{
    private const int Turn = 42;
    private const string PlayerPath = "game_state/inventory/items.json";
    private const string NpcPath = "game_state/npcs/npc_core.json";
    private const string NpcCommandsPath = "game_state/npcs/npc_inventory.json";

    [Fact]
    public void Plan_PartialStackPreservesIdentityReceiptAndCarrier()
    {
        var fixture = CreateFixture("itm_partial", 3);
        var beforeItem = FindItem(RootMap(fixture.Roots), fixture.ItemId);
        var beforeReceipt = beforeItem["materializationReceipt"]!.DeepClone();
        var beforeCarrier = Entry(fixture.Index, fixture.ItemId)["currentCarrier"]!.DeepClone();
        const string transitionId = "mitrn_t070b4_partial_0001";

        var result = Plan(fixture, BuildCommand(1, fixture.ItemId, 1, transitionId));

        AssertValid(result);
        var afterItem = FindItem(result.Roots, fixture.ItemId);
        Assert.Equal(2, afterItem["count"]!.GetValue<int>());
        Assert.True(JsonNode.DeepEquals(beforeReceipt, afterItem["materializationReceipt"]));
        var afterEntry = Entry(result.Index!, fixture.ItemId);
        Assert.Equal("active", afterEntry["state"]!.GetValue<string>());
        Assert.Equal(beforeItem["materializationReceipt"]!["receiptId"]!.GetValue<string>(),
            afterEntry["receiptId"]!.GetValue<string>());
        Assert.True(JsonNode.DeepEquals(beforeCarrier, afterEntry["currentCarrier"]));
        AssertConsume(Assert.Single(result.Transitions), transitionId, fixture.ItemId,
            3, 2, beforeCarrier, beforeCarrier);
        Assert.Equal(transitionId, LastTransition(afterEntry)["transitionId"]!.GetValue<string>());
        Assert.Empty(result.Capacities);
        Assert.Empty(result.TerminalOwners);
    }

    [Fact]
    public void Plan_FullStackConsumesIdentityClearsEquipmentAndReturnsTerminalOwner()
    {
        var fixture = CreateFixture("itm_terminal", 1,
            item =>
            {
                Populate(item, "equipment");
                item["equipmentSlot"] = "MainHand";
            },
            (root, id) => root["equippedItems"]!["MainHand"] = id,
            Resource("itm_terminal", 5m, 3m));
        var catalog = MortalItemCarrierCatalog.Build(fixture.Roots);
        var equipment = Assert.Single(catalog.ByCompanionReference[fixture.ItemId]);
        Assert.Equal(PlayerPath, equipment.FilePath);
        Assert.Equal("MainHand", equipment.PropertyName);
        Assert.NotNull(equipment.ExpectedCarrier);
        var beforeCarrier = Entry(fixture.Index, fixture.ItemId)["currentCarrier"]!.DeepClone();
        const string transitionId = "mitrn_t070b4_terminal_0001";

        var result = Plan(fixture, BuildCommand(1, fixture.ItemId, 1, transitionId));

        AssertValid(result);
        Assert.False(HasItem(result.Roots, fixture.ItemId));
        Assert.DoesNotContain(fixture.ItemId, result.Roots.Values.SelectMany(Strings));
        var playerAfterImage = result.Roots[PlayerPath];
        Assert.True(playerAfterImage["equippedItems"]!["MainHand"] is null);
        var afterEntry = Entry(result.Index!, fixture.ItemId);
        Assert.Equal("consumed", afterEntry["state"]!.GetValue<string>());
        Assert.Null(afterEntry["currentCarrier"]);
        AssertConsume(Assert.Single(result.Transitions), transitionId, fixture.ItemId,
            1, 0, beforeCarrier, null);
        Assert.Equal(transitionId, LastTransition(afterEntry)["transitionId"]!.GetValue<string>());
        Assert.Empty(result.Capacities);
        Assert.Equal(new ResourceOwnerKey("mortal_world", ResourceOwnerKind.Item, fixture.ItemId),
            Assert.Single(result.TerminalOwners));
    }

    [Theory]
    [InlineData("container")]
    [InlineData("quest")]
    [InlineData("bond")]
    [InlineData("other")]
    public void Plan_FullStackRejectsContainerQuestBondOrOtherCompanionWithoutAfterImages(string kind)
    {
        var companion = CreateCompanionFixture(kind);
        var catalog = MortalItemCarrierCatalog.Build(companion.Fixture.Roots);
        Assert.Empty(catalog.Issues);
        var references = catalog.ByCompanionReference[companion.Fixture.ItemId];
        Assert.NotEmpty(references);
        Assert.All(references, reference => Assert.Equal(companion.Path, reference.FilePath));

        var result = Plan(companion.Fixture,
            BuildCommand(1, companion.Fixture.ItemId, 1, $"mitrn_t070b4_{kind}_0001"));

        AssertInvalidEmpty(result);
        Assert.Contains(result.Issues, issue =>
            issue.Code?.Contains("companion", StringComparison.Ordinal) == true ||
            issue.Message.Contains("companion", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Plan_RepeatedClaimsEmitSequentialTransitionsInFinalizationOrder()
    {
        var fixture = CreateFixture("itm_repeated", 2);
        var carrier = Entry(fixture.Index, fixture.ItemId)["currentCarrier"]!.DeepClone();
        const string firstId = "mitrn_t070b4_repeated_0001";
        const string secondId = "mitrn_t070b4_repeated_0002";

        var result = Plan(fixture,
            BuildCommand(1, fixture.ItemId, 1, firstId),
            BuildCommand(2, fixture.ItemId, 1, secondId));

        AssertValid(result);
        Assert.Equal(new[] { firstId, secondId }, result.Transitions
            .Select(value => value["transitionId"]!.GetValue<string>()));
        AssertConsume(result.Transitions[0], firstId, fixture.ItemId, 2, 1, carrier, carrier);
        AssertConsume(result.Transitions[1], secondId, fixture.ItemId, 1, 0, carrier, null);
        Assert.False(HasItem(result.Roots, fixture.ItemId));
        var entry = Entry(result.Index!, fixture.ItemId);
        Assert.Equal("consumed", entry["state"]!.GetValue<string>());
        Assert.Equal(new[] { firstId, secondId }, entry["transitions"]!.AsArray()
            .OfType<JsonObject>().TakeLast(2)
            .Select(value => value["transitionId"]!.GetValue<string>()));
        Assert.Equal(new ResourceOwnerKey("mortal_world", ResourceOwnerKind.Item, fixture.ItemId),
            Assert.Single(result.TerminalOwners));
    }

    [Fact]
    public void Plan_ResourceBearingPartialScalesMaximumAndCurrentExactly()
    {
        var state = Resource("itm_resource_exact", 8m, 6m);
        var fixture = CreateFixture("itm_resource_exact", 4, resource: state);
        var definition = Definition(fixture.Definitions, "durability");

        var result = Plan(fixture,
            BuildCommand(1, fixture.ItemId, 2, "mitrn_t070b4_resource_exact_0001"));

        AssertValid(result);
        Assert.Equal(2, FindItem(result.Roots, fixture.ItemId)["count"]!.GetValue<int>());
        var capacity = Assert.Single(result.Capacities);
        Assert.Equal(state.Coordinate, capacity.Coordinate);
        Assert.Equal(ResourceCapacityOperation.Reconfigure, capacity.Operation);
        Assert.NotNull(capacity.ResolvedCapacity);
        Assert.Equal(4m, capacity.ResolvedCapacity!.Maximum);
        Assert.Equal(ResourceCapacityKind.InstanceFixed, capacity.ResolvedCapacity.Binding.Kind);
        Assert.Equal(ResourceCurrentDisposition.ScaleRatioExact, capacity.CurrentDisposition);
        Assert.Equal(ResourceMutationPhase.RegisteredSystemOutcome, capacity.Phase);
        Assert.Equal(70, capacity.Priority);
        Assert.Equal(fixture.Source.SourceId, capacity.OriginId);
        Assert.Equal(fixture.Source, capacity.SourceEvidence);
        Assert.Equal(fixture.PolicyFingerprint, capacity.PolicyFingerprint);
        Assert.Equal(1m, definition.Quantum);
        Assert.Equal(3m, ExactScale(state.Current, 2, 4, definition.Quantum));
        AssertOrdinal(capacity.EventRef, 1);
        Assert.Empty(result.TerminalOwners);
    }

    [Theory]
    [InlineData("inexact_quantum")]
    [InlineData("non_instance_fixed")]
    public void Plan_InexactOrNonInstanceFixedCapacityRejectsWithoutAfterImages(string axis)
    {
        var kind = axis == "inexact_quantum"
            ? ResourceCapacityKind.InstanceFixed
            : ResourceCapacityKind.RegisteredFormula;
        var state = Resource("itm_resource_" + axis, 1m, 1m, kind);
        var fixture = CreateFixture(state.Coordinate.ResourceOwnerId, 3, resource: state);
        Assert.Equal(kind, Assert.Single(fixture.State.Entries).CapacityBinding.Kind);
        Assert.Equal(1m, Definition(fixture.Definitions, "durability").Quantum);

        var result = Plan(fixture,
            BuildCommand(1, fixture.ItemId, 1, $"mitrn_t070b4_{axis}_0001"));

        AssertInvalidEmpty(result);
        Assert.Contains(result.Issues, issue =>
            issue.Code?.Contains("capacity", StringComparison.Ordinal) == true ||
            issue.Message.Contains("capacity", StringComparison.OrdinalIgnoreCase) ||
            issue.Message.Contains("quantum", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Plan_IsWriteFreeAndDeterministicAcrossDetachedInputs()
    {
        var canonicalRoot = Path.Combine(FindRepo(), "FileSystemExample", "game_session", "game_state");
        var filesBefore = ReadFiles(canonicalRoot);
        var firstFixture = CreateFixture("itm_deterministic", 3,
            resource: Resource("itm_deterministic", 6m, 3m));
        var secondFixture = CreateFixture("itm_deterministic", 3,
            resource: Resource("itm_deterministic", 6m, 3m));
        var firstInput = Describe(firstFixture);
        var secondInput = Describe(secondFixture);
        var commands = new[]
        {
            BuildCommand(1, "itm_deterministic", 1, "mitrn_t070b4_deterministic_0001"),
            BuildCommand(2, "itm_deterministic", 2, "mitrn_t070b4_deterministic_0002")
        };

        var first = Plan(firstFixture, commands);
        var second = Plan(secondFixture, commands);

        AssertPlanEqual(first, second);
        Assert.Equal(firstInput, Describe(firstFixture));
        Assert.Equal(secondInput, Describe(secondFixture));
        AssertFilesEqual(filesBefore, ReadFiles(canonicalRoot));
    }

    [Fact]
    public async Task Project_SameTurnCreateAndTransferUseSnapshotDeterministicReceiptAndTransitionIds()
    {
        var scenario = await ProductionProjectionScenarioAsync();
        var owner = ExactType("BookOfEternityClient.Services.MortalItemCanonicalProjectionPlanner");
        var inputType = ExactType("BookOfEternityClient.Services.MortalItemCanonicalProjectionInput");
        var resultType = ExactType("BookOfEternityClient.Services.MortalItemCanonicalProjectionResult");
        ProjectionInputShape(inputType);
        ProjectionResultShape(resultType);
        var method = ExactMethod(owner, "Project", inputType, resultType);
        var first = ProjectionInput(inputType, scenario);
        var second = ProjectionInput(inputType, scenario);
        var receiptId = SnapshotOwnedMapValue(
            scenario.Snapshot, scenario.CreationRef, "mirec_");
        var createTransitionId = SnapshotOwnedMapValue(
            scenario.Snapshot, scenario.CreationRef, "mitrn_");
        var transferTransitionId = SnapshotOwnedMapValue(
            scenario.Snapshot, scenario.TransferredItemId, "mitrn_");

        var firstResult = ProjectionResult(method.Invoke(null, new[] { first })!);
        var secondResult = ProjectionResult(method.Invoke(null, new[] { second })!);

        Assert.True(firstResult.IsValid);
        Assert.Empty(firstResult.Issues);
        AssertProjectionEqual(firstResult, secondResult);
        var created = FindItem(firstResult.Roots, scenario.CreatedItemId);
        Assert.Equal(receiptId,
            created["materializationReceipt"]!["receiptId"]!.GetValue<string>());
        var createdEntry = Entry(firstResult.Index, scenario.CreatedItemId);
        Assert.Equal(createTransitionId,
            Assert.Single(createdEntry["transitions"]!.AsArray().OfType<JsonObject>())
                ["transitionId"]!.GetValue<string>());
        var transferredEntry = Entry(firstResult.Index, scenario.TransferredItemId);
        Assert.Equal(transferTransitionId,
            LastTransition(transferredEntry)["transitionId"]!.GetValue<string>());
        Assert.Equal("player_inventory",
            transferredEntry["currentCarrier"]!["kind"]!.GetValue<string>());
        Assert.NotNull(FindItem(firstResult.Roots, scenario.TransferredItemId));
        AssertFingerprint(firstResult.Fingerprint);
    }

    private static Fixture CreateFixture(string id, int count,
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

    private static Companion CreateCompanionFixture(string kind)
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

    private static ResourceStateEntry Resource(string id, decimal maximum, decimal current,
        ResourceCapacityKind kind = ResourceCapacityKind.InstanceFixed) => new(
        new ResourceCoordinate("mortal_world", ResourceOwnerKind.Item, id, "durability"),
        current, maximum,
        new ResourceCapacityBinding(kind, "capacity_" + id + "_durability",
            Fingerprint("binding_" + id + "_" + kind)),
        ResourceLifecycleState.Active,
        new ResourceChronology(41, "turn_41:resource:item_durability",
            "resource_transition_initialize_" + id,
            "turn_41:resource:item_durability", 41));

    private static Command BuildCommand(int ordinal, string id, int quantity, string transitionId) =>
        new(ordinal, id, quantity, Fingerprint("claim_" + ordinal + "_" + id), transitionId,
            "mortal_wound_treatment", "mwta_t070b4_finalization_" + ordinal);

    private static Result Plan(Fixture fixture, params Command[] commands)
    {
        var planner = ExactType("BookOfEternityClient.Services.MortalItemConsumptionPlanner");
        var inputType = ExactType("BookOfEternityClient.Services.MortalItemConsumptionPlanningInput");
        var commandType = ExactType("BookOfEternityClient.Services.MortalItemConsumptionCommand");
        var resultType = ExactType("BookOfEternityClient.Services.MortalItemConsumptionPlanningResult");
        PlanShape(inputType, commandType, resultType);
        var method = ExactMethod(planner, "Plan", inputType, resultType);
        var commandArray = Array.CreateInstance(commandType, commands.Length);
        var commandCtor = Ctor(commandType, typeof(int), typeof(string), typeof(int),
            typeof(string), typeof(string), typeof(string), typeof(string));
        for (var i = 0; i < commands.Length; i++)
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
                Turn, Fingerprint("baseline_" + fixture.ItemId), Clone(fixture.Roots),
                MortalItemIdentityState.Parse(fixture.Index.ToJsonString()), commandArray,
                fixture.Definitions, new ResourceStateLedger(fixture.State.Entries),
                fixture.Source, fixture.PolicyFingerprint]);
        return ReadResult(method.Invoke(null, [input])!);
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

    private static async Task<ProjectionScenario> ProductionProjectionScenarioAsync()
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
            var transferred = MortalItemTestFixture.CreateCanonicalRootAtTurn(
                transferredId, 42, "npc_acquisition", "npc_inventory_add",
                "npc_inventory_add:42:0:npc_projection",
                name: "Transferred projection item");
            var npcBefore = ProjectionNpcRoot(npcId, transferred);
            var indexRoot = MortalItemTestFixture.CreateIndexForCarrier(
                transferred, "npc_inventory", npcId);
            await WriteJsonAsync(fileSystem, NpcPath, npcBefore);
            await WriteJsonAsync(fileSystem, MortalItemIdentityState.StatePath, indexRoot);
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
            var current = new Dictionary<string, JsonObject?>(StringComparer.Ordinal)
            {
                [PlayerPath] = playerCurrent,
                [NpcPath] = npcBefore.DeepClone().AsObject(),
                [NpcCommandsPath] = commandsCurrent
            };
            var backup = new Dictionary<string, JsonObject?>(StringComparer.Ordinal)
            {
                [PlayerPath] = playerBefore,
                [NpcPath] = npcBefore,
                [NpcCommandsPath] = null
            };
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

    private static object ProjectionInput(Type inputType, ProjectionScenario scenario)
    {
        var current = scenario.CurrentRoots.ToDictionary(pair => pair.Key,
            pair => pair.Value?.DeepClone().AsObject(), StringComparer.Ordinal);
        var backup = scenario.BackupRoots.ToDictionary(pair => pair.Key,
            pair => pair.Value?.DeepClone().AsObject(), StringComparer.Ordinal);
        var identity = MortalItemIdentityState.Parse(scenario.IdentityRoot.ToJsonString());
        Assert.Empty(identity.Issues);
        return Ctor(inputType, typeof(int),
            typeof(MortalItemAcceptedTurnNormalizationSnapshot),
            typeof(MortalItemRouteAuthorityCatalog),
            typeof(IReadOnlyDictionary<string, JsonObject?>),
            typeof(IReadOnlyDictionary<string, JsonObject?>),
            typeof(MortalItemIdentityParseResult)).Invoke([
                scenario.Turn, scenario.Snapshot, scenario.RouteCatalog,
                current, backup, identity]);
    }

    private static async Task SeedProjectionBootstrapAsync(FileSystemManager fileSystem)
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

    private static Task WriteJsonAsync(
        FileSystemManager fileSystem, string path, JsonNode node) =>
        fileSystem.WriteFileAtomicAsync(path, node.ToJsonString());

    private static string SnapshotOwnedMapValue(
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

    private static void ProjectionInputShape(Type type)
    {
        Property(type, "Turn", typeof(int));
        Property(type, "Snapshot", typeof(MortalItemAcceptedTurnNormalizationSnapshot));
        Property(type, "RouteCatalog", typeof(MortalItemRouteAuthorityCatalog));
        Property(type, "CurrentRoots", typeof(IReadOnlyDictionary<string, JsonObject?>));
        Property(type, "BackupRoots", typeof(IReadOnlyDictionary<string, JsonObject?>));
        Property(type, "IdentityState", typeof(MortalItemIdentityParseResult));
    }

    private static void ProjectionResultShape(Type type)
    {
        Property(type, "ItemPhaseAfterImages", typeof(IReadOnlyDictionary<string, JsonObject>));
        Property(type, "IdentityIndexAfterImage", typeof(JsonObject));
        Property(type, "Issues", typeof(IReadOnlyList<ValidationIssue>));
        Property(type, "Fingerprint", typeof(string));
        Property(type, "IsValid", typeof(bool));
    }

    private static ProjectionOutput ProjectionResult(object value) => new(
        Read<IReadOnlyDictionary<string, JsonObject>>(value, "ItemPhaseAfterImages"),
        Read<JsonObject>(value, "IdentityIndexAfterImage"),
        Read<IReadOnlyList<ValidationIssue>>(value, "Issues"),
        Read<string>(value, "Fingerprint"),
        Read<bool>(value, "IsValid"));

    private static void AssertValid(Result result)
    {
        Assert.True(result.IsValid);
        Assert.Empty(result.Issues);
        Assert.NotNull(result.Index);
        Assert.NotEmpty(result.Roots);
        AssertFingerprint(result.Fingerprint);
    }

    private static void AssertInvalidEmpty(Result result)
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

    private static void AssertConsume(JsonObject value, string transitionId, string itemId,
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

    private static void AssertPlanEqual(Result expected, Result actual)
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

    private static void AssertProjectionEqual(ProjectionOutput expected, ProjectionOutput actual)
    {
        AssertRoots(expected.Roots, actual.Roots);
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

    private static MortalItemCarrierCatalogInput Clone(MortalItemCarrierCatalogInput value) => new(
        value.PlayerInventory?.DeepClone().AsObject(), value.NpcCore?.DeepClone().AsObject(),
        value.NpcInventoryCommands?.DeepClone().AsObject(), value.CurrentLocation?.DeepClone().AsObject(),
        value.Vehicles?.DeepClone().AsObject(), value.CompanionRoots.ToDictionary(
            pair => pair.Key, pair => pair.Value.DeepClone().AsObject(), StringComparer.Ordinal),
        value.OffscreenLocationStorageContents?.DeepClone().AsObject());

    private static string Describe(Fixture fixture) => string.Join("|",
        RootMap(fixture.Roots).OrderBy(pair => pair.Key, StringComparer.Ordinal)
            .Select(pair => pair.Key + "=" + WoundAcceptedTurnFingerprintWriter.CanonicalJson(pair.Value))
            .Append(WoundAcceptedTurnFingerprintWriter.CanonicalJson(fixture.Index))
            .Append(fixture.State.ToCanonicalJson()).Append(fixture.State.Fingerprint));

    private static IReadOnlyDictionary<string, JsonObject> RootMap(MortalItemCarrierCatalogInput roots)
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

    private static JsonObject FindItem(IReadOnlyDictionary<string, JsonObject> roots, string id) =>
        Assert.Single(roots.Values.SelectMany(Objects), value =>
            string.Equals(value["itemId"]?.GetValue<string>(), id, StringComparison.Ordinal) &&
            value["materializationReceipt"] is JsonObject);

    private static bool HasItem(IReadOnlyDictionary<string, JsonObject> roots, string id) =>
        roots.Values.SelectMany(Objects).Any(value =>
            string.Equals(value["itemId"]?.GetValue<string>(), id, StringComparison.Ordinal) &&
            value["materializationReceipt"] is JsonObject);

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

    private static IEnumerable<string> Strings(JsonNode? node)
    {
        if (node is JsonObject obj)
            foreach (var value in obj.SelectMany(pair => Strings(pair.Value))) yield return value;
        else if (node is JsonArray array)
            foreach (var value in array.SelectMany(Strings)) yield return value;
        else if (node is JsonValue value && value.TryGetValue<string>(out var text)) yield return text;
    }

    private static JsonObject Entry(JsonObject root, string id) =>
        Assert.Single(root["entries"]!.AsArray().OfType<JsonObject>(), value =>
            string.Equals(value["itemId"]?.GetValue<string>(), id, StringComparison.Ordinal));
    private static JsonObject LastTransition(JsonObject entry) =>
        Assert.IsType<JsonObject>(entry["transitions"]!.AsArray()[^1]);
    private static JsonObject MergeIndexes(params JsonObject[] indexes) => new()
    {
        ["schemaVersion"] = 1,
        ["entries"] = new JsonArray(indexes.SelectMany(index => index["entries"]!.AsArray())
            .Select(value => value!.DeepClone()).ToArray())
    };
    private static void Populate(JsonObject item, string section) =>
        item["materialization"]!["sections"]![section] = new JsonObject
        { ["state"] = "populated", ["reason"] = null };
    private static ResourceDefinition Definition(ResourceDefinitionCatalog catalog, string key)
    { Assert.True(catalog.TryResolveExact(key, out var value)); return Assert.IsType<ResourceDefinition>(value); }
    private static decimal ExactScale(decimal value, int remaining, int source, decimal quantum)
    { var result = checked(value * remaining) / source; Assert.Equal(0m, result % quantum); return result; }
    private static void AssertOrdinal(string eventRef, int ordinal) => Assert.Single(Regex.Matches(
        eventRef, $@"(?<!\d){ordinal:D4}(?!\d)"));

    private static Type ExactType(string fullName)
    {
        var assembly = typeof(MortalItemIdentityState).Assembly;
        var candidate = assembly.GetType(fullName, false, false);
        Assert.NotNull(candidate);
        Assert.Equal(fullName, candidate!.FullName);
        Assert.Same(assembly, candidate.Assembly);
        return candidate;
    }
    private static MethodInfo ExactMethod(Type owner, string name, Type input, Type? result = null)
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
    private static void AssertFingerprint(string value) => Assert.True(
        ResourceMaterializationContract.IsAuthorityFingerprint(value),
        $"Expected sha256 plus 64 lowercase hexadecimal digits, got '{value}'.");
    private static string Fingerprint(string value) => "sha256:" + Hex(value);
    private static string Hex(string value) => Convert.ToHexString(
        SHA256.HashData(Encoding.UTF8.GetBytes(value))).ToLowerInvariant();

    private static IReadOnlyDictionary<string, byte[]> ReadFiles(string root)
    {
        Assert.True(Directory.Exists(root), root);
        return Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories)
            .OrderBy(value => value, StringComparer.Ordinal).ToDictionary(
                value => Path.GetRelativePath(root, value).Replace('\\', '/'),
                File.ReadAllBytes, StringComparer.Ordinal);
    }
    private static void AssertFilesEqual(IReadOnlyDictionary<string, byte[]> expected,
        IReadOnlyDictionary<string, byte[]> actual)
    {
        Assert.Equal(expected.Keys, actual.Keys);
        foreach (var pair in expected) Assert.True(pair.Value.SequenceEqual(actual[pair.Key]), pair.Key);
    }
    private static string FindRepo()
    {
        for (var current = new DirectoryInfo(AppContext.BaseDirectory); current != null; current = current.Parent)
            if (File.Exists(Path.Combine(current.FullName, "AGENTS.md")) &&
                Directory.Exists(Path.Combine(current.FullName, "FileSystemExample"))) return current.FullName;
        throw new DirectoryNotFoundException("Repository root was not found.");
    }

    private sealed record Fixture(string ItemId, MortalItemCarrierCatalogInput Roots,
        JsonObject Index, MortalItemIdentityParseResult Identity,
        ResourceDefinitionCatalog Definitions, ResourceStateLedger State,
        ResourceSourceEvidence Source, string PolicyFingerprint);
    private sealed record Companion(string Path, Fixture Fixture);
    private sealed record Command(int Ordinal, string ItemId, int Quantity,
        string ClaimFingerprint, string TransitionId, string AuthorityKind, string AuthorityId);
    private sealed record Result(IReadOnlyDictionary<string, JsonObject> Roots, JsonObject? Index,
        IReadOnlyList<JsonObject> Transitions, IReadOnlyList<ResourceCapacityIntent> Capacities,
        IReadOnlyList<ResourceOwnerKey> TerminalOwners, IReadOnlyList<ValidationIssue> Issues,
        string Fingerprint, bool IsValid);
    private sealed record ProjectionScenario(int Turn,
        MortalItemAcceptedTurnNormalizationSnapshot Snapshot,
        MortalItemRouteAuthorityCatalog RouteCatalog,
        IReadOnlyDictionary<string, JsonObject?> CurrentRoots,
        IReadOnlyDictionary<string, JsonObject?> BackupRoots,
        JsonObject IdentityRoot,
        string CreatedItemId,
        string CreationRef,
        string TransferredItemId);
    private sealed record ProjectionOutput(IReadOnlyDictionary<string, JsonObject> Roots,
        JsonObject Index, IReadOnlyList<ValidationIssue> Issues, string Fingerprint, bool IsValid);
}
