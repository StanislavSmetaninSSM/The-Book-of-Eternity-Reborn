using System.Text.Json.Nodes;
using BookOfEternityClient.Services;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed partial class MortalVehicleBirthReceiptTests
{
    private const string SourceFingerprint = "sha256:aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";
    private const string OtherFingerprint = "sha256:bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb";

    [Theory]
    [InlineData("type", "\"Unknown\"")]
    [InlineData("availability", "\"Unknown\"")]
    [InlineData("availability", "\"Parked\"")]
    [InlineData("currentLocationId", "\"location_camp\"")]
    [InlineData("actions", "[{\"actionName\":\"Empty\",\"effects\":[]}]")]
    [InlineData("resistances", "[{\"resistanceName\":\"Too strong\",\"resistanceValue\":\"100%\",\"resistType\":\"All\",\"resistTypeDisplayName\":\"All\"}]")]
    [InlineData("currentHealth", "100")]
    [InlineData("maxHealth", "100")]
    public void HistoricalBirth_RejectsInvalidExistingVehicleSemantics(string field, string json)
    {
        var root = Root();
        var receipt = root["birthReceipts"]![0]!.AsObject();
        receipt["birthState"]![field] = JsonNode.Parse(json);
        Reseal(receipt);
        var before = root.ToJsonString();
        Assert.NotEmpty(MortalVehicleBirthReceiptState.ValidateLocal(root));
        Assert.Equal(before, root.ToJsonString());
    }

    [Fact]
    public void HistoricalInitialization_RejectsMalformedAuthorityFingerprint()
    {
        var root = Root();
        var receipt = root["birthReceipts"]![0]!.AsObject();
        receipt["resourceInitialization"]![0]!["authorityFingerprint"] = "not_a_fingerprint";
        Reseal(receipt);
        Assert.NotEmpty(MortalVehicleBirthReceiptState.ValidateLocal(root));
    }

    [Theory]
    [InlineData("schemaVersion")]
    [InlineData("cargoMeasurements")]
    [InlineData("unknownField")]
    [InlineData("coverage")]
    [InlineData("count")]
    [InlineData("shape")]
    [InlineData("volume")]
    [InlineData("fingerprint")]
    [InlineData("itemReceipt")]
    [InlineData("sourceTurn")]
    [InlineData("sourceAuthority")]
    public void HistoricalCargo_RejectsResealedMalformedOrInconsistentEvidence(string mutation)
    {
        var root = RootWithCargo();
        var receipt = root["birthReceipts"]![0]!.AsObject();
        var birth = receipt["birthState"]!.AsObject();
        var evidence = birth["cargoBirthEvidence"]!.AsObject();
        switch (mutation)
        {
            case "schemaVersion": evidence["schemaVersion"] = 2; break;
            case "cargoMeasurements": evidence["cargoMeasurements"] = false; break;
            case "unknownField": evidence["unlisted"] = true; break;
            case "coverage": evidence["cargoMeasurements"]!.AsArray().Clear(); break;
            case "count": evidence["cargoMeasurements"]![0]!["count"] = 2; break;
            case "shape": evidence["cargoMeasurements"]![0]!["measuredConfiguration"]!["item"]!["name"] = "Different item"; break;
            case "volume": evidence["cargoMeasurements"]![0]!["externalVolumeDm3"] = -1; break;
            case "fingerprint": evidence["cargoMeasurements"]![0]!["configurationFingerprint"] = "wrong"; break;
            case "itemReceipt": birth["inventory"]![0]!["materializationReceipt"]!["seal"] = "wrong"; break;
            case "sourceTurn":
                birth["inventory"]![0]!["materialization"]!["sourceTurn"] = 41;
                birth["inventory"]![0]!["materializationReceipt"]!["acceptedAtTurn"] = 41;
                MortalItemTestFixture.ResealCanonical(birth["inventory"]![0]!.AsObject());
                break;
            case "sourceAuthority":
                birth["inventory"]![0]!["materialization"]!["sourceAuthority"]!["authorityId"] = "foreign_cart";
                MortalItemTestFixture.ResealCanonical(birth["inventory"]![0]!.AsObject());
                break;
        }
        Reseal(receipt);
        var before = root.ToJsonString();
        Assert.NotEmpty(MortalVehicleBirthReceiptState.ValidateLocal(root));
        Assert.Equal(before, root.ToJsonString());
    }

    [Fact]
    public void HistoricalCargo_ValidatesItsOwnSnapshotAfterLiveCargoLeaves()
    {
        var root = RootWithCargo();
        var receipt = root["birthReceipts"]![0]!.ToJsonString();
        root["vehicles"]![0]!["inventory"] = new JsonArray();
        root["vehicles"]![0]!["capacity"] = 0;
        root["vehicles"]![0]!["volume"] = 0;
        Assert.Empty(MortalVehicleBirthReceiptState.ValidateLocal(root));
        Assert.Equal(receipt, root["birthReceipts"]![0]!.ToJsonString());
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ResourceAgreement_AcceptsExactBirthAndTerminalHistory(bool retired)
    {
        var root = Root();
        var (state, history) = Resources(retired);
        if (retired) { root["vehicles"] = new JsonArray(); root["activeVehicleId"] = null; }
        Assert.Empty(MortalVehicleBirthReceiptState.ValidateResourceAgreement(root, state, history));
    }

    [Theory]
    [InlineData("sourceTurn")]
    [InlineData("maximum")]
    [InlineData("fingerprint")]
    [InlineData("sourceId")]
    [InlineData("missingHistory")]
    [InlineData("missingReceipt")]
    [InlineData("missingRoot")]
    [InlineData("missingRetirement")]
    [InlineData("retiredWithLiveResource")]
    public void ResourceAgreement_RefusesMissingOrMismatchedOriginalEvidence(string mutation)
    {
        JsonObject? root = Root();
        var (state, history) = Resources(mutation is "missingRoot" or "retiredWithLiveResource");
        var receipt = root["birthReceipts"]![0]!.AsObject();
        switch (mutation)
        {
            case "sourceTurn": receipt["sourceTurn"] = 43; receipt["birthState"]!["cargoBirthEvidence"]!["sourceTurn"] = 43; break;
            case "maximum": receipt["resourceInitialization"]![0]!["maximum"] = 99m; break;
            case "fingerprint": receipt["resourceInitialization"]![0]!["authorityFingerprint"] = OtherFingerprint; break;
            case "sourceId": receipt["vehicleRef"] = "other_ref"; receipt["resourceInitialization"]![0]!["sourceId"] = "other_ref"; break;
            case "missingHistory": history = null!; break;
            case "missingReceipt": root["vehicles"] = new JsonArray(); root["birthReceipts"] = new JsonArray(); break;
            case "missingRoot": root = null; break;
            case "missingRetirement": root["vehicles"] = new JsonArray(); state = new ResourceStateLedger([]); break;
            case "retiredWithLiveResource": root["vehicles"] = new JsonArray(); state = Resources(false).State; break;
        }
        Reseal(receipt);
        Assert.NotEmpty(MortalVehicleBirthReceiptState.ValidateResourceAgreement(root, state, history));
    }

    private static void Reseal(JsonObject receipt) => receipt["seal"] = MortalVehicleBirthReceiptState.ComputeSeal(receipt);

    /// <summary>Current independent item and cargo DATA from the real pure composers.</summary>
    private static JsonObject RootWithCargo()
    {
        var root = Root();
        var birth = root["birthReceipts"]![0]!["birthState"]!.AsObject();
        var item = MortalItemTestFixture.CreateCanonicalRoot("cargo_item");
        item["materialization"]!["route"] = "new_vehicle_inventory";
        item["materialization"]!["sourceAuthority"] = new JsonObject { ["kind"] = "new_vehicle", ["authorityId"] = "new_cart" };
        MortalItemTestFixture.ResealCanonical(item);
        birth["inventory"] = new JsonArray(item);
        var catalog = MortalItemCarrierCatalog.Build(new(null, null, null, null,
            new JsonObject { ["vehicles"] = new JsonArray(birth.DeepClone()) }, new Dictionary<string, JsonObject>()));
        var cargo = MortalCargoBirthCapacity.Validate("vehicle_cart", birth, catalog, new Dictionary<string, string>(), 42, "fixture");
        Assert.True(cargo.IsValid, string.Join(Environment.NewLine, cargo.Issues));
        birth["cargoBirthEvidence"] = cargo.Evidence;
        root["vehicles"] = new JsonArray(birth.DeepClone());
        Reseal(root["birthReceipts"]![0]!.AsObject());
        return root;
    }

    private static (ResourceStateLedger State, ResourceHistoryState History) Resources(bool retired)
    {
        var coordinate = new ResourceCoordinate("mortal_world", ResourceOwnerKind.Vehicle, "vehicle_cart", "health");
        var binding = new ResourceCapacityBinding(ResourceCapacityKind.RegisteredFormula, ResourceCapacityFormulaCatalog.MortalHealthCapacityV1, SourceFingerprint);
        var snapshot = new ResourceStateSnapshot(100, 100, binding, ResourceLifecycleState.Active);
        var initialize = new ResourceTransition("transition_birth", "operation_birth", "turn_42", "system_rule", "birth",
            ResourceMutationPhase.RegisteredSystemOutcome, 100, 0, coordinate, ResourceTransitionOperation.Initialize,
            0, 0, ResourceTransitionOutcome.Applied, ResourceCapacityDisposition.InitializeFromDefinition, null, snapshot,
            new ResourceSourceEvidence("owner_materialization", "new_cart", SourceFingerprint), OtherFingerprint, null, 42);
        var transitions = new List<ResourceTransition> { initialize };
        if (retired)
            transitions.Add(new ResourceTransition("transition_retire", "operation_retire", "turn_43", "system_rule", "removal",
                ResourceMutationPhase.RegisteredSystemOutcome, 100, 0, coordinate, ResourceTransitionOperation.Retire,
                0, 0, ResourceTransitionOutcome.Applied, null, snapshot, null,
                new ResourceSourceEvidence("owner_lifecycle", "vehicle_cart", SourceFingerprint), OtherFingerprint, null, 43));
        var history = ResourceHistoryState.ParseCanonical(ResourceHistoryState.SerializeCanonicalTransitions(transitions),
            ResourceDefinitionCatalog.CreateBuiltIn(), allowMissingPristine: false);
        Assert.True(history.IsValid, string.Join(Environment.NewLine, history.Issues.Select(i => i.Code + ":" + i.Expected)));
        return (new ResourceStateLedger(retired ? [] : new[] { new ResourceStateEntry(coordinate, 100, 100, binding,
            ResourceLifecycleState.Active, new ResourceChronology(42, "turn_42", "transition_birth", "turn_42", 42)) }), history.History!);
    }
}
