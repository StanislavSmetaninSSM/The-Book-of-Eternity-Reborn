using System.Text.Json.Nodes;
using BookOfEternityClient.Services;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed class ResourceVehicleIntegrationTests
{
    [Fact]
    public async Task MetaMiscValidation_AcceptsTransientVehicleRefAndLedgerOnlyCanonicalShape()
    {
        await using var context = await ResourceMaterializationTestContext.CreateAsync();
        await context.WriteExactJsonAsync(
            StorageTransportMoveService.VehiclesPath,
            new JsonObject
            {
                ["vehicles"] = new JsonArray(
                    CanonicalVehicle("vehicle_cart_001", "location_market")),
                ["UpdateVehicles"] = new JsonArray(
                    VehicleCreation(
                        "vehicle_ref_wagon",
                        Materialization(("health", 160m))))
            }.ToJsonString());

        var issues = await context.Validator.ValidateGameStateAsync(
            GameStateValidationPhase.MetaMiscStateFiles);

        Assert.DoesNotContain(
            issues,
            issue => issue.Severity == IssueSeverity.Error);
    }

    [Fact]
    public async Task MetaMiscValidation_RejectsLegacyVehicleHealthAuthority()
    {
        await using var context = await ResourceMaterializationTestContext.CreateAsync();
        var vehicle = CanonicalVehicle("vehicle_cart_001", "location_market");
        vehicle["maxHealth"] = "100%";
        vehicle["currentHealth"] = "75%";
        await context.WriteExactJsonAsync(
            StorageTransportMoveService.VehiclesPath,
            new JsonObject
            {
                ["vehicles"] = new JsonArray(vehicle)
            }.ToJsonString());

        var issues = await context.Validator.ValidateGameStateAsync(
            GameStateValidationPhase.MetaMiscStateFiles);

        Assert.Contains(
            issues,
            issue => issue.Code == "resource_owner_legacy_value_forbidden" &&
                     issue.FilePath.EndsWith(".maxHealth", StringComparison.Ordinal));
        Assert.Contains(
            issues,
            issue => issue.Code == "resource_owner_legacy_value_forbidden" &&
                     issue.FilePath.EndsWith(".currentHealth", StringComparison.Ordinal));
    }

    [Fact]
    public async Task CrossReferenceValidation_RejectsPreassignedIdForNewVehicle()
    {
        await using var context = await ResourceMaterializationTestContext.CreateAsync();
        var forgedCreation = VehicleCreation(
            "vehicle_ref_wagon",
            Materialization(("health", 160m)));
        forgedCreation.Remove("vehicleRef");
        forgedCreation["vehicleId"] = "vehicle_gm_preassigned";
        await context.WriteExactJsonAsync(
            StorageTransportMoveService.VehiclesPath,
            new JsonObject
            {
                ["vehicles"] = new JsonArray(),
                ["UpdateVehicles"] = new JsonArray(forgedCreation)
            }.ToJsonString());

        var issues = await context.Validator.ValidateGameStateAsync(
            GameStateValidationPhase.CrossReferences);

        Assert.Contains(
            issues,
            issue => issue.Code == "resource_owner_vehicle_preassigned_id_forbidden");
    }

    [Fact]
    public async Task AcceptedTurn_NewVehicleThenDestructionInitializesAndRetiresHealthAtomically()
    {
        await using var context = await ResourceMaterializationTestContext.CreateAsync();
        var bootstrap = ResourceBootstrapStateBuilder.BuildPristine();
        Assert.True(bootstrap.IsValid, string.Join(Environment.NewLine, bootstrap.Issues));
        await context.WriteExactJsonAsync(
            ResourceMaterializationTestContext.DefinitionsPath,
            bootstrap.Definitions!.ToCanonicalJson());
        await context.WriteExactJsonAsync(
            ResourceMaterializationTestContext.StatePath,
            bootstrap.State!.ToCanonicalJson());
        await context.WriteExactJsonAsync(
            ResourceMaterializationTestContext.HistoryPath,
            bootstrap.History!.ToCanonicalJson());
        await context.WriteExactJsonAsync(
            StorageTransportMoveService.VehiclesPath,
            new JsonObject { ["vehicles"] = new JsonArray() }.ToJsonString());
        await context.CaptureValidatedPendingSnapshotAsync(turn: 42);
        await context.WriteExactJsonAsync(
            StorageTransportMoveService.VehiclesPath,
            new JsonObject
            {
                ["vehicles"] = new JsonArray(),
                ["UpdateVehicles"] = new JsonArray(
                    VehicleCreation(
                        "vehicle_ref_cart",
                        Materialization(("health", 140m))))
            }.ToJsonString());

        var creationIssues = await context.Validator
            .ValidateAcceptedTurnRawResourceMaterializationAsync();

        Assert.DoesNotContain(
            creationIssues,
            issue => issue.Severity == IssueSeverity.Error);
        Assert.True(AcceptedMechanicsPlanAuthority.TryPeekValidated(
            context.FileSystem,
            out _,
            out var creationPlanning));
        var creationPlan = Assert.IsType<AcceptedMechanicsPlan>(creationPlanning.Plan);
        var vehicleOwner = Assert.Single(
            creationPlan.OwnerAuthority.Entries.Values,
            entry => entry.Key.OwnerKind == ResourceOwnerKind.Vehicle);
        var vehicleId = vehicleOwner.Key.ResourceOwnerId;
        var createdState = ResourceStateContract.ParseCanonical(
            creationPlan.StateAfterImage.ToJsonString(),
            bootstrap.Definitions,
            allowMissingPristine: false);
        Assert.True(createdState.IsValid, string.Join(Environment.NewLine, createdState.Issues));
        var createdHealth = Assert.Single(createdState.Ledger!.Entries);
        Assert.Equal(vehicleId, createdHealth.Coordinate.ResourceOwnerId);
        Assert.Equal("health", createdHealth.Coordinate.ResourceKey);
        Assert.Equal(140m, createdHealth.Current);
        Assert.Equal(140m, createdHealth.Maximum);

        await using (var writeLease = await context.FileSystem.AcquireCanonicalWriteLeaseAsync())
        {
            var published = await context.Normalizer.BindTo(writeLease)
                .NormalizeAcceptedMechanicsAsync(backups: null);
            Assert.Same(creationPlan, published);
        }
        var canonicalVehicleRoot = Assert.IsType<JsonObject>(await context.ReadJsonAsync(
            StorageTransportMoveService.VehiclesPath));
        var canonicalVehicle = Assert.IsType<JsonObject>(
            Assert.Single(canonicalVehicleRoot["vehicles"]!.AsArray()));
        Assert.Equal(vehicleId, canonicalVehicle["vehicleId"]!.GetValue<string>());
        Assert.False(canonicalVehicle.ContainsKey("resourceMaterialization"));

        await context.CaptureValidatedPendingSnapshotAsync(turn: 43);
        canonicalVehicleRoot["removeVehicles"] = new JsonArray(vehicleId);
        await context.WriteExactJsonAsync(
            StorageTransportMoveService.VehiclesPath,
            canonicalVehicleRoot.ToJsonString());

        var destructionIssues = await context.Validator
            .ValidateAcceptedTurnRawResourceMaterializationAsync();

        Assert.DoesNotContain(
            destructionIssues,
            issue => issue.Severity == IssueSeverity.Error);
        Assert.True(AcceptedMechanicsPlanAuthority.TryPeekValidated(
            context.FileSystem,
            out _,
            out var destructionPlanning));
        var destructionPlan = Assert.IsType<AcceptedMechanicsPlan>(destructionPlanning.Plan);
        var retiredState = ResourceStateContract.ParseCanonical(
            destructionPlan.StateAfterImage.ToJsonString(),
            bootstrap.Definitions,
            allowMissingPristine: false);
        var retiredHistory = ResourceHistoryState.ParseCanonical(
            destructionPlan.HistoryAfterImage.ToJsonString(),
            bootstrap.Definitions,
            allowMissingPristine: false);
        Assert.True(retiredState.IsValid, string.Join(Environment.NewLine, retiredState.Issues));
        Assert.True(retiredHistory.IsValid, string.Join(Environment.NewLine, retiredHistory.Issues));
        Assert.Empty(retiredState.Ledger!.Entries);
        Assert.Equal(
            new[]
            {
                ResourceTransitionOperation.Initialize,
                ResourceTransitionOperation.Retire
            },
            retiredHistory.History!.Transitions.Select(static transition => transition.Operation));

        await using (var writeLease = await context.FileSystem.AcquireCanonicalWriteLeaseAsync())
        {
            var published = await context.Normalizer.BindTo(writeLease)
                .NormalizeAcceptedMechanicsAsync(backups: null);
            Assert.Same(destructionPlan, published);
        }
        var removedVehicleRoot = Assert.IsType<JsonObject>(await context.ReadJsonAsync(
            StorageTransportMoveService.VehiclesPath));
        Assert.Empty(removedVehicleRoot["vehicles"]!.AsArray());
        Assert.False(removedVehicleRoot.ContainsKey("removeVehicles"));
    }

    [Fact]
    public void NewVehicle_MaterializesHealthOnceAndPublishesNoResourceEnvelopeOrLegacyValues()
    {
        var acceptedVehicles = new JsonObject
        {
            ["vehicles"] = new JsonArray(),
            ["UpdateVehicles"] = new JsonArray(
                VehicleCreation(
                    "vehicle_ref_cart",
                    new JsonObject
                    {
                        ["resources"] = new JsonArray(
                            new JsonObject
                            {
                                ["resourceKey"] = "health",
                                ["maximum"] = 140
                            })
                    }))
        };
        var result = MortalResourceOwnerComposer.Compose(
            new MortalResourceOwnerCompositionInput(
                ResourceDefinitionCatalog.CreateBuiltIn(),
                Roots(new JsonObject { ["vehicles"] = new JsonArray() }),
                Roots(acceptedVehicles)),
            new CombatantIdentityFactory(),
            new FixedVehicleIdentityFactory());

        Assert.True(result.IsValid, string.Join(Environment.NewLine, result.Issues));
        var draft = Assert.Single(result.CapacityDrafts);
        Assert.Equal(ResourceOwnerKind.Vehicle, draft.Coordinate.OwnerKind);
        Assert.Equal("vehicle_cutover_001", draft.Coordinate.ResourceOwnerId);
        Assert.Equal("health", draft.Coordinate.ResourceKey);
        Assert.Equal(140m, draft.AcceptedMaximum);
        Assert.True(draft.ResolvedCapacity.IsValid);

        var afterImage = result.OwnerCompanionAfterImages[
            StorageTransportMoveService.VehiclesPath];
        var vehicle = Assert.IsType<JsonObject>(
            Assert.Single(afterImage["vehicles"]!.AsArray()));
        Assert.Equal("vehicle_cutover_001", vehicle["vehicleId"]!.GetValue<string>());
        Assert.False(vehicle.ContainsKey("vehicleRef"));
        Assert.False(vehicle.ContainsKey("resourceMaterialization"));
        Assert.False(vehicle.ContainsKey("currentHealth"));
        Assert.False(vehicle.ContainsKey("maxHealth"));
    }

    [Fact]
    public void ExistingVehicle_MovePreservesCoordinateAndCannotReinitializeCapacity()
    {
        var existing = CanonicalVehicle("vehicle_cart_001", "location_market");
        var preTurn = new JsonObject
        {
            ["vehicles"] = new JsonArray(existing.DeepClone())
        };
        var accepted = new JsonObject
        {
            ["vehicles"] = new JsonArray(existing.DeepClone()),
            ["UpdateVehicles"] = new JsonArray(
                new JsonObject
                {
                    ["vehicleId"] = "vehicle_cart_001",
                    ["availability"] = "Parked",
                    ["currentLocationId"] = "location_gate"
                })
        };

        var moved = MortalResourceOwnerComposer.Compose(
            new MortalResourceOwnerCompositionInput(
                ResourceDefinitionCatalog.CreateBuiltIn(),
                Roots(preTurn),
                Roots(accepted)));

        Assert.True(moved.IsValid, string.Join(Environment.NewLine, moved.Issues));
        Assert.Empty(moved.CapacityDrafts);
        var owner = Assert.Single(
            moved.Authority!.Entries.Values,
            value => value.Key.OwnerKind == ResourceOwnerKind.Vehicle);
        Assert.Equal("vehicle_cart_001", owner.Key.ResourceOwnerId);
        var vehicle = Assert.IsType<JsonObject>(Assert.Single(
            moved.OwnerCompanionAfterImages[StorageTransportMoveService.VehiclesPath]
                ["vehicles"]!.AsArray()));
        Assert.Equal("location_gate", vehicle["currentLocationId"]!.GetValue<string>());

        accepted["UpdateVehicles"]![0]!["resourceMaterialization"] =
            Materialization(("health", 200m));
        var forged = MortalResourceOwnerComposer.Compose(
            new MortalResourceOwnerCompositionInput(
                ResourceDefinitionCatalog.CreateBuiltIn(),
                Roots(preTurn),
                Roots(accepted)));

        Assert.False(forged.IsValid);
        Assert.Contains(forged.Issues, issue =>
            issue.Code == "resource_owner_materialization_existing_forbidden");
    }

    [Fact]
    public void VehicleDestruction_ExportsOneTerminalOwnerAndLegacyHealthIsRejected()
    {
        var existing = CanonicalVehicle("vehicle_cart_001", "location_market");
        var preTurn = new JsonObject
        {
            ["vehicles"] = new JsonArray(existing.DeepClone())
        };
        var removed = new JsonObject
        {
            ["vehicles"] = new JsonArray(existing.DeepClone()),
            ["removeVehicles"] = new JsonArray("vehicle_cart_001")
        };
        var result = MortalResourceOwnerComposer.Compose(
            new MortalResourceOwnerCompositionInput(
                ResourceDefinitionCatalog.CreateBuiltIn(),
                Roots(preTurn),
                Roots(removed)));

        Assert.True(result.IsValid, string.Join(Environment.NewLine, result.Issues));
        var terminal = Assert.Single(result.TerminalOwners);
        Assert.Equal(ResourceOwnerKind.Vehicle, terminal.OwnerKind);
        Assert.Equal("vehicle_cart_001", terminal.ResourceOwnerId);

        existing["maxHealth"] = "100%";
        existing["currentHealth"] = "50%";
        var legacy = MortalResourceOwnerComposer.Compose(
            new MortalResourceOwnerCompositionInput(
                ResourceDefinitionCatalog.CreateBuiltIn(),
                Roots(new JsonObject { ["vehicles"] = new JsonArray(existing.DeepClone()) }),
                Roots(new JsonObject { ["vehicles"] = new JsonArray(existing.DeepClone()) })));

        Assert.False(legacy.IsValid);
        Assert.Contains(legacy.Issues, issue =>
            issue.Code == "resource_owner_legacy_value_forbidden");
    }

    private static JsonObject VehicleCreation(
        string vehicleRef,
        JsonObject materialization) =>
        new()
        {
            ["vehicleRef"] = vehicleRef,
            ["name"] = "Телега",
            ["description"] = "Прочная дорожная телега.",
            ["image_prompt"] = "wooden cart",
            ["type"] = "Vehicle",
            ["isSentient"] = false,
            ["availability"] = "Parked",
            ["currentLocationId"] = "location_market",
            ["speedBonus"] = 0,
            ["actions"] = new JsonArray(),
            ["resistances"] = new JsonArray(),
            ["inventory"] = new JsonArray(),
            ["resourceMaterialization"] = materialization
        };

    private static JsonObject CanonicalVehicle(string vehicleId, string locationId) =>
        new()
        {
            ["vehicleId"] = vehicleId,
            ["name"] = "Телега",
            ["description"] = "Прочная дорожная телега.",
            ["image_prompt"] = "wooden cart",
            ["type"] = "Vehicle",
            ["isSentient"] = false,
            ["availability"] = "Parked",
            ["currentLocationId"] = locationId,
            ["speedBonus"] = 0,
            ["actions"] = new JsonArray(),
            ["resistances"] = new JsonArray(),
            ["inventory"] = new JsonArray()
        };

    private static JsonObject Materialization(params (string Key, decimal Maximum)[] values) =>
        new()
        {
            ["resources"] = new JsonArray(values
                .Select(value => (JsonNode)new JsonObject
                {
                    ["resourceKey"] = value.Key,
                    ["maximum"] = value.Maximum
                })
                .ToArray())
        };

    private static MortalResourceOwnerRoots Roots(JsonObject vehicles) =>
        new(
            new JsonObject { ["NPCsInScene"] = new JsonArray() },
            new JsonObject { ["enemiesData"] = new JsonArray() },
            new JsonObject { ["alliesData"] = new JsonArray() },
            vehicles);

    private sealed class FixedVehicleIdentityFactory : VehicleIdentityFactory
    {
        internal override string CreateVehicleId() => "vehicle_cutover_001";
    }
}
