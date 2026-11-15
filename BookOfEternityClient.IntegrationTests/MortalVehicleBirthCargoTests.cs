using System.Text.Json.Nodes;
using BookOfEternityClient.Services;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed class MortalVehicleBirthCargoTests
{
    [Theory]
    [InlineData("faction", false)]
    [InlineData("faction", true)]
    [InlineData("npc", false)]
    [InlineData("npc", true)]
    public async Task BirthReferences_RejectUnknownOwnerOrController(string kind, bool withCargo)
    {
        await using var context = await MortalItemMaterializationTestContext.CreateAsync();
        await context.ArrangeEmptyMortalTurnAsync();
        var root = withCargo ? CargoBirth("new_cart") : BirthRoot();
        var vehicle = root["UpdateVehicles"]![0]!.AsObject();
        if (kind == "faction")
            vehicle["owner"] = new JsonObject
            { ["ownerType"] = "Faction", ["ownerId"] = "missing_faction", ["ownerName"] = "Missing faction" };
        else
            vehicle["controller"] = new JsonObject
            { ["actorType"] = "npc", ["actorId"] = "missing_npc" };
        await context.WriteJsonAsync(StorageTransportMoveService.VehiclesPath, root);
        Assert.DoesNotContain(await context.Validator.ValidateAcceptedTurnRawMortalItemMaterializationAsync(),
            issue => issue.Severity == IssueSeverity.Error);
        var before = await context.CaptureExactBytesAsync(new[]
        { StorageTransportMoveService.VehiclesPath, ResourceMaterializationContract.StatePath,
            ResourceMaterializationContract.HistoryPath, MortalItemIdentityState.StatePath });

        var issues = await context.Validator.ValidateAcceptedTurnRawResourceMaterializationAsync();

        var field = kind == "faction" ? "owner.ownerId" : "controller.actorId";
        Assert.Contains(issues, issue => issue.Severity == IssueSeverity.Error &&
            issue.Code == "resource_owner_vehicle_birth_reference_unknown" &&
            issue.FilePath == StorageTransportMoveService.VehiclesPath + ".UpdateVehicles[0]." + field);
        await context.AssertExactBytesAsync(before);
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    [InlineData(true, true)]
    public async Task BirthFactionReference_PublishesOrRejectsDependencyDrift(bool withCargo, bool drift)
    {
        await using var context = await MortalItemMaterializationTestContext.CreateAsync();
        await context.ArrangeEmptyMortalTurnAsync();
        var faction = EffectMaterializationTestFixture.CreateSameTurnMortalFaction("faction_cart");
        faction["factionId"] = "faction_cart";
        faction.Remove("initialId");
        faction.Remove("isNewFaction");
        faction.Remove("activeEffectDefinitions");
        var factions = new JsonObject { ["factions"] = new JsonArray(faction) };
        await context.WriteJsonAsync(FactionCoreChangesContract.FactionCorePath, factions);
        await context.CaptureValidatedPendingSnapshotAsync();
        var root = withCargo ? CargoBirth("new_cart") : BirthRoot();
        root["UpdateVehicles"]![0]!["owner"] = new JsonObject
        { ["ownerType"] = "Faction", ["ownerId"] = "faction_cart", ["ownerName"] = faction["name"]!.DeepClone() };
        await context.WriteJsonAsync(StorageTransportMoveService.VehiclesPath, root);
        Assert.DoesNotContain(await context.ValidateAcceptedTurnRawMaterializationAsync(),
            issue => issue.Severity == IssueSeverity.Error);

        if (drift)
        {
            faction["name"] = "Changed after admission";
            await context.WriteJsonAsync(FactionCoreChangesContract.FactionCorePath, factions);
            var before = await context.CaptureExactBytesAsync(new[]
            { StorageTransportMoveService.VehiclesPath, FactionCoreChangesContract.FactionCorePath,
                MortalItemIdentityState.StatePath, ResourceMaterializationContract.StatePath,
                ResourceMaterializationContract.HistoryPath });
            await Assert.ThrowsAnyAsync<InvalidDataException>(() => context.NormalizeAcceptedTurnWithIssuesAsync());
            await context.AssertExactBytesAsync(before);
        }
        else
        {
            await context.NormalizeAcceptedTurnAsync();
            var acceptedRoot = (await context.ReadJsonAsync(StorageTransportMoveService.VehiclesPath))!;
            var vehicle = Assert.Single(acceptedRoot["vehicles"]!.AsArray())!;
            Assert.False(acceptedRoot.AsObject().ContainsKey("UpdateVehicles"));
            Assert.Equal("faction_cart", vehicle["owner"]!["ownerId"]!.GetValue<string>());
            Assert.Equal(faction["name"]!.GetValue<string>(), vehicle["owner"]!["ownerName"]!.GetValue<string>());
            Assert.Equal(withCargo ? 1 : 0, vehicle["inventory"]!.AsArray().Count);
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task BirthFactionRename_UsesAcceptedName(bool useAcceptedName)
    {
        await using var context = await MortalItemMaterializationTestContext.CreateAsync();
        await context.ArrangeEmptyMortalTurnAsync();
        var faction = EffectMaterializationTestFixture.CreateSameTurnMortalFaction("faction_cart");
        faction["factionId"] = "faction_cart";
        faction.Remove("initialId"); faction.Remove("isNewFaction"); faction.Remove("activeEffectDefinitions");
        var oldName = faction["name"]!.GetValue<string>();
        var factions = new JsonObject { ["factions"] = new JsonArray(faction) };
        await context.WriteJsonAsync(FactionCoreChangesContract.FactionCorePath, factions);
        await context.CaptureValidatedPendingSnapshotAsync();
        factions["factionCoreChanges"] = new JsonArray(new JsonObject
        {
            ["factionId"] = "faction_cart", ["reason"] = "The guild has adopted a new public name.",
            ["profile"] = new JsonObject { ["name"] = "Renamed caravan guild" }
        });
        await context.WriteJsonAsync(FactionCoreChangesContract.FactionCorePath, factions);
        Assert.DoesNotContain(await context.Validator.ValidateFactionCoreChangesBeforeNormalizationAsync(),
            issue => issue.Severity == IssueSeverity.Error);
        var root = BirthRoot();
        root["UpdateVehicles"]![0]!["owner"] = new JsonObject
        { ["ownerType"] = "Faction", ["ownerId"] = "faction_cart",
            ["ownerName"] = useAcceptedName ? "Renamed caravan guild" : oldName };
        await context.WriteJsonAsync(StorageTransportMoveService.VehiclesPath, root);
        Assert.DoesNotContain(await context.Validator.ValidateAcceptedTurnRawMortalItemMaterializationAsync(),
            issue => issue.Severity == IssueSeverity.Error);
        var issues = await context.Validator.ValidateAcceptedTurnRawResourceMaterializationAsync();
        if (!useAcceptedName)
        {
            Assert.Contains(issues, issue => issue.Code == "resource_owner_vehicle_birth_reference_name_mismatch");
            return;
        }
        Assert.DoesNotContain(issues, issue => issue.Severity == IssueSeverity.Error);
        await context.NormalizeAcceptedTurnAsync();
        var afterVehicle = Assert.Single((await context.ReadJsonAsync(StorageTransportMoveService.VehiclesPath))!["vehicles"]!.AsArray())!;
        var afterFaction = Assert.Single((await context.ReadJsonAsync(FactionCoreChangesContract.FactionCorePath))!["factions"]!.AsArray())!;
        Assert.Equal("Renamed caravan guild", afterFaction["name"]!.GetValue<string>());
        Assert.Equal(afterFaction["name"]!.GetValue<string>(), afterVehicle["owner"]!["ownerName"]!.GetValue<string>());
    }

    [Fact]
    public async Task SameTurnVehicleBirth_SealsCargoWithPermanentCarrierAndHealth()
    {
        await using var context = await MortalItemMaterializationTestContext.CreateAsync();
        await context.ArrangeEmptyMortalTurnAsync();
        await context.WriteJsonAsync(StorageTransportMoveService.VehiclesPath, CargoBirth("new_cart"));

        await context.NormalizeAcceptedTurnWithValidatedCommonPlanAsync();

        var root = (await context.ReadJsonAsync(StorageTransportMoveService.VehiclesPath))!.AsObject();
        Assert.False(root.ContainsKey("UpdateVehicles"));
        var vehicle = Assert.Single(root["vehicles"]!.AsArray())!.AsObject();
        var vehicleId = vehicle["vehicleId"]!.GetValue<string>();
        Assert.False(string.IsNullOrWhiteSpace(vehicleId));
        Assert.NotEqual("new_cart", vehicleId);
        Assert.Equal(vehicleId, root["activeVehicleId"]!.GetValue<string>());
        Assert.False(vehicle.ContainsKey("vehicleRef"));
        Assert.False(vehicle.ContainsKey("resourceMaterialization"));
        var cargo = Assert.Single(vehicle["inventory"]!.AsArray())!.AsObject();
        var itemId = cargo["itemId"]!.GetValue<string>();
        var birthEvidence = vehicle["cargoBirthEvidence"]!.AsObject();
        Assert.Equal(42, birthEvidence["sourceTurn"]!.GetValue<int>());
        Assert.Equal(vehicleId, birthEvidence["vehicleId"]!.GetValue<string>());
        var measured = Assert.Single(birthEvidence["cargoMeasurements"]!.AsArray())!;
        Assert.Equal(itemId, measured["itemId"]!.GetValue<string>());
        Assert.Equal(1, measured["count"]!.GetValue<int>());
        Assert.NotNull(measured["configurationFingerprint"]);
        Assert.Equal(cargo["volume"]!.GetValue<double>(), measured["externalVolumeDm3"]!.GetValue<double>());

        Assert.Equal(itemId, cargo["existedId"]!.GetValue<string>());
        Assert.NotNull(cargo["materializationReceipt"]);
        Assert.False(cargo.ContainsKey("creationRef"));
        var index = MortalItemIdentityState.Parse(
            (await context.ReadJsonAsync(MortalItemIdentityState.StatePath))!.AsObject());
        Assert.Empty(index.Issues);
        Assert.True(index.EntriesByItemId.TryGetValue(itemId, out var entry));
        var carrier = entry!["currentCarrier"]!.AsObject();
        Assert.Equal("vehicle_inventory", carrier["kind"]!.GetValue<string>());
        Assert.Equal(vehicleId, carrier["ownerId"]!.GetValue<string>());
        Assert.Null(carrier["containerId"]);
        Assert.Empty(carrier["containerPath"]!.AsArray());
        Assert.DoesNotContain(
            await context.Validator.ValidateAcceptedTurnCanonicalMortalItemMaterializationAsync(),
            issue => issue.Severity == IssueSeverity.Error);

        var definitions = ResourceDefinitionCatalog.ParseCanonical(
            (await context.ReadJsonAsync(ResourceMaterializationContract.DefinitionsPath))!.ToJsonString(),
            allowMissingPristine: false);
        Assert.NotNull(definitions.Catalog);
        var state = ResourceStateContract.ParseCanonical(
            (await context.ReadJsonAsync(ResourceMaterializationContract.StatePath))!.ToJsonString(),
            definitions.Catalog!, allowMissingPristine: false);
        Assert.True(state.IsValid, string.Join(Environment.NewLine, state.Issues));
        var health = Assert.Single(state.Ledger!.Entries,
            value => value.Coordinate.ResourceOwnerId == vehicleId && value.Coordinate.ResourceKey == "health");
        Assert.Equal(100m, health.Current);
        Assert.Equal(100m, health.Maximum);
    }

    [Fact]
    public async Task MeasuredStackBirth_PublishesBoundHistoricalConfiguration()
    {
        await using var context = await MortalItemMaterializationTestContext.CreateAsync();
        await context.ArrangeEmptyMortalTurnAsync();
        var root = CargoBirth("new_cart");
        var vehicle = root["UpdateVehicles"]![0]!.AsObject();
        var item = vehicle["inventory"]![0]!.AsObject();
        item["count"] = 2;
        var shape = item.DeepClone().AsObject();
        foreach (var field in new[] { "existedId", "creationRef", "materialization", "count", "contentsPath" })
            shape.Remove(field);
        vehicle["capacity"] = 1;
        vehicle["volume"] = 0.1;
        vehicle["cargoMeasurements"] = new JsonArray(new JsonObject
        {
            ["creationRef"] = "cargo_birth_item", ["count"] = 2, ["externalVolumeDm3"] = 0.1,
            ["measuredConfiguration"] = new JsonObject
            { ["schemaVersion"] = 1, ["item"] = shape, ["contents"] = new JsonArray() }
        });
        await context.WriteJsonAsync(StorageTransportMoveService.VehiclesPath, root);

        await context.NormalizeAcceptedTurnWithValidatedCommonPlanAsync();

        var after = (await context.ReadJsonAsync(StorageTransportMoveService.VehiclesPath))!;
        var accepted = Assert.Single(after["vehicles"]!.AsArray())!.AsObject();
        Assert.False(accepted.ContainsKey("cargoMeasurements"));
        var cargo = Assert.Single(accepted["inventory"]!.AsArray())!;
        var receipt = Assert.Single(accepted["cargoBirthEvidence"]!["cargoMeasurements"]!.AsArray())!;
        Assert.Equal(cargo["itemId"]!.GetValue<string>(), receipt["itemId"]!.GetValue<string>());
        Assert.Equal(2, receipt["count"]!.GetValue<int>());
        Assert.Equal(0.1, receipt["externalVolumeDm3"]!.GetValue<double>());
        Assert.NotNull(receipt["configurationFingerprint"]);
        Assert.True(MortalItemMaterializationContract.ImmutableEvidenceEquals(shape,
            receipt["measuredConfiguration"]!["item"]!));
        Assert.DoesNotContain(await context.Validator.ValidateAcceptedTurnCanonicalMortalItemMaterializationAsync(),
            issue => issue.Severity == IssueSeverity.Error);
    }

    [Theory]
    [InlineData("slots")]
    [InlineData("volume")]
    [InlineData("measurement")]
    public async Task CargoCapacityAdmission_RejectsUnprovedBirthLoadWithoutMutation(string defect)
    {
        await using var context = await MortalItemMaterializationTestContext.CreateAsync();
        await context.ArrangeEmptyMortalTurnAsync();
        var root = CargoBirth("new_cart");
        var vehicle = root["UpdateVehicles"]![0]!.AsObject();
        var item = vehicle["inventory"]![0]!.AsObject();
        if (defect == "slots") vehicle["capacity"] = 0;
        else if (defect == "volume") vehicle["volume"] = 0;
        else item["count"] = 2;
        await context.WriteJsonAsync(StorageTransportMoveService.VehiclesPath, root);
        Assert.DoesNotContain(await context.Validator.ValidateAcceptedTurnRawMortalItemMaterializationAsync(),
            issue => issue.Severity == IssueSeverity.Error);
        var before = await context.CaptureExactBytesAsync(new[]
        {
            StorageTransportMoveService.VehiclesPath, MortalItemIdentityState.StatePath,
            ResourceMaterializationContract.DefinitionsPath, ResourceMaterializationContract.StatePath,
            ResourceMaterializationContract.HistoryPath, CanonicalResourceOwnerAuthorityComposer.AuthorityPath
        });

        var issues = await context.Validator.ValidateAcceptedTurnRawResourceMaterializationAsync();

        Assert.Contains(issues, issue => issue.Severity == IssueSeverity.Error &&
            issue.Code == "resource_owner_vehicle_cargo_capacity_invalid");
        await context.AssertExactBytesAsync(before);
    }

    [Fact]
    public async Task SameTurnVehicleCargo_ForeignSourceRefIsRejectedWithoutRawMutation()
    {
        await using var context = await MortalItemMaterializationTestContext.CreateAsync();
        await context.ArrangeEmptyMortalTurnAsync();
        await context.WriteJsonAsync(StorageTransportMoveService.VehiclesPath, CargoBirth("foreign_cart"));
        var before = await context.CaptureExactBytesAsync(new[]
        {
            StorageTransportMoveService.VehiclesPath,
            MortalItemIdentityState.StatePath,
            ResourceMaterializationContract.DefinitionsPath,
            ResourceMaterializationContract.StatePath,
            ResourceMaterializationContract.HistoryPath,
            CanonicalResourceOwnerAuthorityComposer.AuthorityPath
        });

        var issues = await context.Validator.ValidateAcceptedTurnRawMortalItemMaterializationAsync();

        Assert.Contains(issues, issue => issue.Severity == IssueSeverity.Error &&
            issue.Code == "mortal_item_materialization_route_authority_mismatch");
        await context.AssertExactBytesAsync(before);
    }

    [Theory]
    [InlineData("missing")]
    [InlineData("null")]
    [InlineData("string")]
    [InlineData("mixedScalar")]
    public async Task RawVehicleCargoAdmission_RejectsMalformedInventoryWithoutMutation(string shape)
    {
        await using var context = await MortalItemMaterializationTestContext.CreateAsync();
        await context.ArrangeEmptyMortalTurnAsync();
        var root = CargoBirth("new_cart");
        var vehicle = root["UpdateVehicles"]![0]!.AsObject();
        switch (shape)
        {
            case "missing":
                vehicle.Remove("inventory");
                break;
            case "null":
                vehicle["inventory"] = null;
                break;
            case "string":
                vehicle["inventory"] = "not an inventory array";
                break;
            case "mixedScalar":
                vehicle["inventory"]!.AsArray().Add(17);
                break;
            default:
                throw new InvalidOperationException("Unknown inventory-shape case.");
        }
        await context.WriteJsonAsync(StorageTransportMoveService.VehiclesPath, root);
        var before = await context.CaptureExactBytesAsync(new[]
        {
            StorageTransportMoveService.VehiclesPath,
            MortalItemIdentityState.StatePath,
            ResourceMaterializationContract.DefinitionsPath,
            ResourceMaterializationContract.StatePath,
            ResourceMaterializationContract.HistoryPath,
            CanonicalResourceOwnerAuthorityComposer.AuthorityPath
        });

        var issues = await context.Validator.ValidateAcceptedTurnRawMortalItemMaterializationAsync();

        var expectedPath = StorageTransportMoveService.VehiclesPath +
            ".UpdateVehicles[0].inventory" + (shape == "mixedScalar" ? "[1]" : string.Empty);
        Assert.Contains(issues, issue => issue.Severity == IssueSeverity.Error &&
            issue.Code == "resource_owner_vehicle_birth_invalid" &&
            string.Equals(issue.FilePath, expectedPath, StringComparison.Ordinal));
        await context.AssertExactBytesAsync(before);
    }

    [Fact]
    public async Task CargoResourceAdmission_RejectsItemIndexDriftAfterItemAdmission()
    {
        await using var context = await MortalItemMaterializationTestContext.CreateAsync();
        await context.ArrangeEmptyMortalTurnAsync();
        await context.WriteJsonAsync(StorageTransportMoveService.VehiclesPath, CargoBirth("new_cart"));
        Assert.DoesNotContain(await context.Validator.ValidateAcceptedTurnRawMortalItemMaterializationAsync(),
            issue => issue.Severity == IssueSeverity.Error);
        var index = (await context.ReadJsonAsync(MortalItemIdentityState.StatePath))!.AsObject();
        index["unacceptedDrift"] = true;
        await context.WriteJsonAsync(MortalItemIdentityState.StatePath, index);
        var before = await context.CaptureExactBytesAsync(new[]
        {
            StorageTransportMoveService.VehiclesPath, MortalItemIdentityState.StatePath,
            ResourceMaterializationContract.StatePath
        });
        var issues = await context.Validator.ValidateAcceptedTurnRawResourceMaterializationAsync();
        Assert.Contains(issues, issue => issue.Severity == IssueSeverity.Error &&
            issue.Code == "resource_owner_vehicle_cargo_projection_invalid");
        await context.AssertExactBytesAsync(before);
    }

    [Fact]
    public async Task CargoPublication_RejectsItemIndexDriftAfterResourceAdmissionBeforeWrites()
    {
        await using var context = await MortalItemMaterializationTestContext.CreateAsync();
        await context.ArrangeEmptyMortalTurnAsync();
        await context.WriteJsonAsync(StorageTransportMoveService.VehiclesPath, CargoBirth("new_cart"));
        Assert.DoesNotContain(await context.ValidateAcceptedTurnRawMaterializationAsync(),
            issue => issue.Severity == IssueSeverity.Error);
        var index = (await context.ReadJsonAsync(MortalItemIdentityState.StatePath))!.AsObject();
        index["unacceptedDrift"] = true;
        await context.WriteJsonAsync(MortalItemIdentityState.StatePath, index);
        var before = await context.CaptureExactBytesAsync(new[]
        {
            StorageTransportMoveService.VehiclesPath, MortalItemIdentityState.StatePath,
            ResourceMaterializationContract.DefinitionsPath, ResourceMaterializationContract.StatePath,
            ResourceMaterializationContract.HistoryPath, CanonicalResourceOwnerAuthorityComposer.AuthorityPath
        });
        await Assert.ThrowsAnyAsync<InvalidDataException>(() => context.NormalizeAcceptedTurnWithIssuesAsync());
        await context.AssertExactBytesAsync(before);
    }

    [Fact]
    public async Task SameTurnVehicleCargo_HistoryPublicationCutRestoresExactRawInputs()
    {
        const string cutMessage = "vehicle cargo accepted publication fixture";
        var armed = false;
        var cutHits = 0;
        MortalItemMaterializationTestContext? active = null;
        var hooks = new BookOfEternityClient.Core.FileSystemManagerHooks
        {
            LocalPublicationObserver = (phase, index) =>
            {
                if (!armed || cutHits != 0 ||
                    phase != BookOfEternityClient.Core.TrustedLocalPublicationPhase.MemberPublished)
                    return;
                var files = active!.FileSystem;
                var journalPath = Path.Combine(files.RuntimeRootPath,
                    "trusted-local-publication-v1", "active.json");
                using var document = System.Text.Json.JsonDocument.Parse(File.ReadAllBytes(journalPath));
                var member = document.RootElement.GetProperty("Members")[index];
                var historyPath = files.ResolvePath(ResourceMaterializationContract.HistoryPath);
                if (member.GetProperty("Path").GetString() != historyPath)
                    return;
                Assert.True(member.GetProperty("After").GetProperty("Exists").GetBoolean());
                var publishedBytes = member.GetProperty("After").GetProperty("Bytes").GetBytesFromBase64();
                Assert.Equal(publishedBytes, File.ReadAllBytes(historyPath));
                cutHits++;
                // InvalidDataException is an IOException, but is explicitly not retried by
                // PublishCanonicalImagesAsync. A plain IOException would retry after rollback.
                throw new InvalidDataException(cutMessage);
            }
        };
        await using var context = await MortalItemMaterializationTestContext.CreateAsync(hooks);
        active = context;
        await context.ArrangeEmptyMortalTurnAsync();
        await context.WriteJsonAsync(StorageTransportMoveService.VehiclesPath, CargoBirth("new_cart"));
        Assert.DoesNotContain(await context.Validator.ValidateAcceptedTurnRawMortalItemMaterializationAsync(),
            issue => issue.Severity == IssueSeverity.Error);
        Assert.DoesNotContain(await context.Validator.ValidateAcceptedTurnRawResourceMaterializationAsync(),
            issue => issue.Severity == IssueSeverity.Error);
        var before = await context.CaptureExactBytesAsync(new[]
        {
            StorageTransportMoveService.VehiclesPath,
            MortalItemIdentityState.StatePath,
            ResourceMaterializationContract.DefinitionsPath,
            ResourceMaterializationContract.StatePath,
            ResourceMaterializationContract.HistoryPath,
            CanonicalResourceOwnerAuthorityComposer.AuthorityPath
        });

        armed = true;
        var failure = await Record.ExceptionAsync(() => context.NormalizeAcceptedTurnAsync());
        armed = false;

        Assert.True(cutHits == 1, $"Vehicle cargo publication cut hits={cutHits}; failure={failure}");
        Assert.NotNull(failure);
        // Match the real known-rollback route, which can wrap the publication failure.
        Assert.Contains(cutMessage, failure.ToString(), StringComparison.Ordinal);
        await context.AssertExactBytesAsync(before);
        Assert.False(File.Exists(Path.Combine(context.FileSystem.RuntimeRootPath,
            "trusted-local-publication-v1", "active.json")));
    }

    private static JsonObject CargoBirth(string authorityId)
    {
        var root = BirthRoot();
        root["UpdateVehicles"]![0]!["inventory"]!.AsArray().Add(
            MortalItemTestFixture.CreateRawRoot(
                route: "new_vehicle_inventory", authorityKind: "new_vehicle", authorityId: authorityId,
                sourceTurn: 42, creationRef: "cargo_birth_item", materializationId: "cargo_birth_materialization"));
        return root;
    }

    private static JsonObject BirthRoot() => JsonNode.Parse("""
        {
          "vehicles": [],
          "activeVehicleChange": "new_cart",
          "UpdateVehicles": [{
            "vehicleRef": "new_cart",
            "name": "Cart",
            "description": "A serviceable cart used to carry supplies.",
            "image_prompt": "A wooden supply cart with iron-rimmed wheels",
            "type": "Vehicle",
            "isSentient": false,
            "availability": "Active",
            "currentLocationId": null,
            "speedBonus": 0,
            "actions": [],
            "resistances": [],
            "inventory": [],
            "capacity": 12,
            "volume": 100.0,
            "owner": { "ownerType": "Player", "ownerId": "player_current", "ownerName": "Current player" },
            "controller": { "actorType": "player", "actorId": "player_current" },
            "authorizedUsers": [],
            "hasFullAccess": true,
            "resourceMaterialization": { "resources": [{ "resourceKey": "health", "maximum": 100 }] }
          }]
        }
        """)!.AsObject();
}
