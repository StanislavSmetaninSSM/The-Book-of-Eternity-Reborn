using System.Reflection;
using System.Text;
using System.Text.Json.Nodes;
using BookOfEternityClient.Services;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed partial class MortalWoundTreatmentResolverTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void TreatmentAcceptedState_MalformedVehicleEncodingReturnsTypedRootIssue(
        bool doubleBom)
    {
        var scenario = CreateGuaranteedResourcePublicationScenario(
            resourceQuantities: new[] { 2 },
            selectedResourceOrder: new[] { 0 });
        using var fixture = AcceptedStateFixture.Create(scenario);
        var malformed = doubleBom
            ? Encoding.UTF8.GetPreamble()
                .Concat(Encoding.UTF8.GetPreamble())
                .Concat(Encoding.UTF8.GetBytes("[]"))
                .ToArray()
            : new byte[] { 0xC3, 0x28 };
        WriteTreatmentProjectionRootBytes(
            fixture,
            StorageTransportMoveService.VehiclesPath,
            malformed);
        fixture.PrepareFreshSnapshot(
            doubleBom ? "vehicle_double_bom" : "vehicle_invalid_utf8");

        var result = fixture.ExportCurrent();

        Assert.False(Convert.ToBoolean(ReadRequiredProperty(result, "IsValid")));
        var issues = AsObjects(ReadRequiredProperty(result, "Issues"))
            .Select(Assert.IsType<ValidationIssue>)
            .ToArray();
        var issue = Assert.Single(issues, static issue =>
            string.Equals(
                issue.FilePath,
                StorageTransportMoveService.VehiclesPath,
                StringComparison.Ordinal) &&
            string.Equals(
                issue.Code,
                "mortal_wound_treatment_accepted_state_root_invalid",
                StringComparison.Ordinal));
        Assert.Equal(IssueSeverity.Error, issue.Severity);
        Assert.DoesNotContain(issues, static issue => string.Equals(
            issue.Code,
            "mortal_wound_treatment_accepted_state_lease_invalid",
            StringComparison.Ordinal));
    }

    [Fact]
    public void GuaranteedItemConsumption_UnrelatedLegacyVehicleArrayIsUntouchedAndReplayPreservesExactBytes()
    {
        var scenario = CreateGuaranteedResourcePublicationScenario(
            resourceQuantities: new[] { 2 },
            selectedResourceOrder: new[] { 0 },
            includeReusableItem: true,
            selectReusableItem: true,
            reusableItemCount: 2);
        using var fixture = AcceptedStateFixture.Create(scenario);
        fixture.SetCanonicalPlayerEnergyForResourcePublicationTest(10);
        var legacyBytes = SeedVehicleTopologyBeforeTreatmentSnapshot(
            fixture,
            legacyArray: true,
            "unrelated_legacy_vehicle_array");
        var flow = PersistAndRehydrateResourcePublication(
            fixture,
            scenario,
            "unrelated_legacy_vehicle_array");
        var plan = ComposeResourcePublication(fixture, flow);

        Assert.DoesNotContain(
            StorageTransportMoveService.VehiclesPath,
            plan.OwnerCompanionAfterImages.Keys);
        var itemAuthority = Assert.IsType<MortalWoundTreatmentItemPublicationAuthority>(
            plan.TreatmentResourcePublicationAuthority?.ItemPublicationAuthority);
        var objectAfterImages = itemAuthority.PublicationAfterImages;
        var exactAfterImages = itemAuthority.CloneExactPublicationAfterImages();
        Assert.NotEmpty(exactAfterImages);
        Assert.Equal(
            objectAfterImages.Keys.OrderBy(static path => path, StringComparer.Ordinal)
                .ToArray(),
            exactAfterImages.Keys.OrderBy(static path => path, StringComparer.Ordinal)
                .ToArray());
        Assert.DoesNotContain(
            StorageTransportMoveService.VehiclesPath,
            exactAfterImages.Keys);
        var detachedPath = exactAfterImages.Keys.OrderBy(
                static path => path,
                StringComparer.Ordinal)
            .First();
        Assert.IsType<JsonObject>(exactAfterImages[detachedPath])[
            "detachedMutation"] = true;
        Assert.True(itemAuthority.HasValidSeal());
        Assert.False(JsonNode.DeepEquals(
            exactAfterImages[detachedPath],
            itemAuthority.CloneExactPublicationAfterImages()[detachedPath]));
        Assert.Equal(
            legacyBytes,
            ReadCanonicalBytes(fixture, StorageTransportMoveService.VehiclesPath));

        using (var publication = PublishCachedResourcePlanOpen(fixture, flow, plan))
        {
            Assert.Equal(
                legacyBytes,
                ReadCanonicalBytes(fixture, StorageTransportMoveService.VehiclesPath));
            publication.CompleteAtFullPipelineEnd();
        }

        fixture.PrepareFreshSnapshot("unrelated_legacy_vehicle_array_replay");
        fixture.RestartForReplay();
        var resourceSpendsAfterPublication =
            ReadTreatmentResourceSpendTransitions(fixture).Count;
        var itemTransitionsAfterPublication = ReadIdentityTransitionCount(
            ReadItemIdentityEntry(fixture, "reusable_field_kit"));
        var replay = ProbePublishedTreatment(fixture, flow.Request);

        Assert.Equal(
            "ExactReplay",
            Convert.ToString(ReadRequiredProperty(replay, "Status")));
        Assert.Equal(
            legacyBytes,
            ReadCanonicalBytes(fixture, StorageTransportMoveService.VehiclesPath));
        Assert.Equal(
            resourceSpendsAfterPublication,
            ReadTreatmentResourceSpendTransitions(fixture).Count);
        Assert.Equal(
            itemTransitionsAfterPublication,
            ReadIdentityTransitionCount(ReadItemIdentityEntry(
                fixture,
                "reusable_field_kit")));

        var finalRootsField = typeof(MortalWoundTreatmentItemPublicationAuthority)
            .GetField("_finalRoots", BindingFlags.Instance | BindingFlags.NonPublic);
        var sealedFinalRoots = Assert.IsAssignableFrom<
            IDictionary<string, JsonNode?>>(Assert.IsAssignableFrom<FieldInfo>(
                finalRootsField).GetValue(itemAuthority));
        Assert.IsType<JsonObject>(sealedFinalRoots[detachedPath])[
            "sealedTopologyTamper"] = true;
        Assert.False(itemAuthority.HasValidSeal());
        Assert.Throws<InvalidDataException>(
            itemAuthority.CloneExactPublicationAfterImages);
    }

    [Theory]
    [InlineData(2, 1)]
    [InlineData(1, 0)]
    public void TreatmentItemPublicationTopology_LegacyVehicleArrayRestoresPartialAndFullConsumption(
        int initialCount,
        int expectedCount)
    {
        var baseline = CreateVehicleProjectionRoot(
            legacyArray: true,
            initialCount);
        var plannerView = MortalItemProjectionRootParser.ToCarrierCatalogObject(
            baseline,
            StorageTransportMoveService.VehiclesPath)!;
        var plannerBefore = plannerView.DeepClone();
        var inventory = plannerView["vehicles"]![0]!["inventory"]!.AsArray();
        if (expectedCount == 0)
            inventory.Clear();
        else
            inventory[0]!["count"] = expectedCount;

        var publication = MortalItemProjectionRootParser.RestorePublicationRoot(
            baseline,
            plannerView,
            StorageTransportMoveService.VehiclesPath);

        var array = Assert.IsType<JsonArray>(publication);
        Assert.Equal(expectedCount, ReadVehicleSelectedItemCount(array));
        Assert.Equal(initialCount, ReadVehicleSelectedItemCount(
            Assert.IsType<JsonArray>(baseline)));
        Assert.False(JsonNode.DeepEquals(plannerBefore, plannerView));
        array.Add(new JsonObject { ["vehicleId"] = "detached_mutation" });
        Assert.Single(plannerView["vehicles"]!.AsArray());
        Assert.Single(Assert.IsType<JsonArray>(baseline));
    }

    [Fact]
    public void TreatmentItemPublicationTopology_ObjectVehicleRootRetainsObjectParityAndCloneIsolation()
    {
        var baseline = CreateVehicleProjectionRoot(
            legacyArray: false,
            initialCount: 2);
        var plannerView = MortalItemProjectionRootParser.ToCarrierCatalogObject(
            baseline,
            StorageTransportMoveService.VehiclesPath)!;
        plannerView["vehicles"]![0]!["inventory"]![0]!["count"] = 1;

        var publication = MortalItemProjectionRootParser.RestorePublicationRoot(
            baseline,
            plannerView,
            StorageTransportMoveService.VehiclesPath);

        var obj = Assert.IsType<JsonObject>(publication);
        Assert.Equal(
            1,
            ReadVehicleSelectedItemCount(obj["vehicles"]!.AsArray()));
        obj["vehicles"]!.AsArray().Clear();
        Assert.Equal(
            1,
            ReadVehicleSelectedItemCount(
                plannerView["vehicles"]!.AsArray()));
        Assert.Equal(
            2,
            ReadVehicleSelectedItemCount(
                Assert.IsType<JsonObject>(baseline)["vehicles"]!.AsArray()));
    }

    [Fact]
    public void TreatmentItemPublicationTopology_InvalidBaselineOrWrappedViewFailsClosed()
    {
        var legacyBaseline = CreateVehicleProjectionRoot(
            legacyArray: true,
            initialCount: 2);
        var validPlannerView = MortalItemProjectionRootParser.ToCarrierCatalogObject(
            legacyBaseline,
            StorageTransportMoveService.VehiclesPath)!;

        Assert.Throws<InvalidOperationException>(() =>
            MortalItemProjectionRootParser.RestorePublicationRoot(
                JsonValue.Create("invalid"),
                validPlannerView,
                StorageTransportMoveService.VehiclesPath));
        Assert.Throws<InvalidOperationException>(() =>
            MortalItemProjectionRootParser.RestorePublicationRoot(
                legacyBaseline,
                new JsonObject { ["notVehicles"] = new JsonArray() },
                StorageTransportMoveService.VehiclesPath));
        Assert.Throws<InvalidOperationException>(() =>
            MortalItemProjectionRootParser.RestorePublicationRoot(
                legacyBaseline,
                new JsonObject
                {
                    ["vehicles"] = new JsonArray(),
                    ["unexpected"] = true
                },
                StorageTransportMoveService.VehiclesPath));
        Assert.Throws<InvalidOperationException>(() =>
            MortalItemProjectionRootParser.RestorePublicationRoot(
                legacyBaseline,
                validPlannerView,
                InventoryEquipmentService.ItemsPath));
        Assert.Throws<InvalidOperationException>(() =>
            MortalItemProjectionRootParser.RestorePublicationRoot(
                baselineRoot: null,
                validPlannerView,
                InventoryEquipmentService.ItemsPath));
        Assert.Throws<InvalidOperationException>(() =>
            MortalItemProjectionRootParser.RestorePublicationRoot(
                JsonValue.Create("invalid"),
                validPlannerView,
                InventoryEquipmentService.ItemsPath));
        Assert.Throws<InvalidOperationException>(() =>
            MortalItemProjectionRootParser.RestorePublicationRoot(
                baselineRoot: null,
                validPlannerView,
                StorageTransportMoveService.VehiclesPath));
    }

    [Fact]
    public void TreatmentItemPublicationTopology_ProductionUsesSharedAdapterAndSealedExactNodes()
    {
        var repositoryRoot = FindRepositoryRootForB4SourceGuard();
        var acceptedState = File.ReadAllText(Path.Combine(
            repositoryRoot,
            "BookOfEternityClient",
            "Services",
            "MortalWoundTreatmentAcceptedStateAuthority.cs"));
        var resourceBuild = File.ReadAllText(Path.Combine(
            repositoryRoot,
            "BookOfEternityClient",
            "Services",
            "MortalWoundTreatmentResourcePublication.cs"));
        var recomposition = File.ReadAllText(Path.Combine(
            repositoryRoot,
            "BookOfEternityClient",
            "Services",
            "MortalWoundTreatmentItemPublication.Recomposition.cs"));
        var proof = File.ReadAllText(Path.Combine(
            repositoryRoot,
            "BookOfEternityClient",
            "Services",
            "MortalWoundTreatmentItemPublication.Proof.cs"));
        var normalizer = File.ReadAllText(Path.Combine(
            repositoryRoot,
            "BookOfEternityClient",
            "Services",
            "CanonicalStateNormalizer",
            "CanonicalStateNormalizer.AcceptedMechanics.cs"));

        Assert.Contains(
            "MortalItemProjectionRootParser.Parse(",
            acceptedState,
            StringComparison.Ordinal);
        Assert.Contains(
            "MortalItemProjectionRootParser.ToCarrierCatalogObject(",
            acceptedState,
            StringComparison.Ordinal);
        Assert.Contains(
            "MortalItemProjectionRootParser.RestorePublicationRoot(",
            resourceBuild,
            StringComparison.Ordinal);
        Assert.Contains(
            "MortalItemProjectionRootParser.RestorePublicationRoot(",
            recomposition,
            StringComparison.Ordinal);
        Assert.Contains(
            "CloneExactPublicationAfterImages(",
            proof,
            StringComparison.Ordinal);
        Assert.Contains(
            "_finalRoots.TryGetValue(",
            proof,
            StringComparison.Ordinal);
        Assert.Contains(
            "CloneExactPublicationAfterImages(",
            normalizer,
            StringComparison.Ordinal);
        Assert.DoesNotContain(
            "StorageTransportMoveService.VehiclesPath",
            normalizer,
            StringComparison.Ordinal);
    }

    private static byte[] SeedVehicleTopologyBeforeTreatmentSnapshot(
        AcceptedStateFixture fixture,
        bool legacyArray,
        string label)
    {
        var vehicleItem = MortalItemTestFixture.CreateCanonicalRoot(
            "item_vehicle_t070b4_unrelated");
        var vehicleId = "vehicle_t070b4_unrelated";
        WriteTreatmentProjectionRootBytes(
            fixture,
            StorageTransportMoveService.VehiclesPath,
            Encoding.UTF8.GetBytes(new JsonObject
            {
                ["vehicles"] = new JsonArray(new JsonObject
                {
                    ["vehicleId"] = vehicleId,
                    ["inventory"] = new JsonArray(vehicleItem.DeepClone())
                })
            }.ToJsonString()));
        var identity = ReadCanonicalObject(
            fixture,
            MortalItemIdentityState.StatePath);
        var vehicleIdentity = MortalItemTestFixture.CreateIndexForCarrier(
            vehicleItem,
            "vehicle_inventory",
            vehicleId);
        identity["entries"]!.AsArray().Add(
            vehicleIdentity["entries"]![0]!.DeepClone());
        WriteTreatmentProjectionRootBytes(
            fixture,
            MortalItemIdentityState.StatePath,
            Encoding.UTF8.GetBytes(identity.ToJsonString()));
        WriteCanonicalResourceAuthority(fixture.FileSystem);

        var wrapper = ReadCanonicalObject(
            fixture,
            StorageTransportMoveService.VehiclesPath);
        JsonNode root = legacyArray
            ? wrapper["vehicles"]!.DeepClone()
            : wrapper.DeepClone();
        var bytes = Encoding.UTF8.GetBytes(root.ToJsonString());
        WriteTreatmentProjectionRootBytes(
            fixture,
            StorageTransportMoveService.VehiclesPath,
            bytes);
        fixture.PrepareFreshSnapshot(label);
        return bytes;
    }

    private static JsonNode CreateVehicleProjectionRoot(
        bool legacyArray,
        int initialCount)
    {
        var item = MortalItemTestFixture.CreateCanonicalRoot("antibiotic_dose");
        item["count"] = initialCount;
        MortalItemTestFixture.ResealCanonical(item);
        var vehicles = new JsonArray(new JsonObject
        {
            ["vehicleId"] = "vehicle_t070b4_topology",
            ["name"] = "Topology wagon",
            ["availability"] = "Active",
            ["inventory"] = new JsonArray(item)
        });
        return legacyArray
            ? vehicles
            : new JsonObject { ["vehicles"] = vehicles };
    }

    private static int ReadVehicleSelectedItemCount(JsonArray vehicles)
    {
        var vehicle = Assert.Single(
            vehicles.OfType<JsonObject>(),
            static candidate => candidate["vehicleId"]?.GetValue<string>() ==
                                "vehicle_t070b4_topology");
        var matches = vehicle["inventory"]!.AsArray().OfType<JsonObject>()
            .Where(static item => item["itemId"]?.GetValue<string>() ==
                                  "antibiotic_dose")
            .ToArray();
        if (matches.Length == 0)
            return 0;
        return Assert.Single(matches)["count"]!.GetValue<int>();
    }
}
