using System.Text.Json.Nodes;
using BookOfEternityClient.Services;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed class MortalStorageBirthHistoryTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void FinalizeDetachesExpectedSlotsAndPreservesStableIdentities(bool empty)
    {
        var (prepared, phase, catalog) = Arrange(empty);
        var before = prepared.FinalIdentityIndex.ToJsonString();
        Assert.NotEmpty(MortalLocationStorageBirthEvidence.ValidateExpectedCoverage(prepared, prepared.FinalIdentityIndex, Source));
        var result = MortalLocationStorageBirthEvidence.Finalize(prepared, phase, catalog, new Dictionary<string, string>(), Source);
        Assert.True(result.IsValid, string.Join(";", result.Issues.Select(i => i.Code + ":" + i.FilePath)));
        Assert.Equal(before, prepared.FinalIdentityIndex.ToJsonString());
        Assert.Empty(result.Completion!.ValidateForPublication(prepared, Source));
        var completed = result.Completion.Plan;
        Assert.Equal(prepared.StorageBirthDrafts[0].TransitionId, Transition(completed)["transitionId"]!.GetValue<string>());
        Assert.True(JsonNode.DeepEquals(prepared.FinalWorldMap, completed.FinalWorldMap));
        Assert.Empty(MortalLocationIdentityState.Parse(completed.FinalIdentityIndex).Issues);
        // Mutating a detached handed-out plan cannot change retained completion DATA.
        completed.FinalWorldMap["hostile"] = true;
        completed.FinalIdentityIndex["locationEntries"] = new JsonArray();
        Assert.Empty(result.Completion.ValidateForPublication(prepared, Source));
        Assert.Null(result.Completion.Plan.FinalWorldMap["hostile"]);
        Assert.NotEmpty(result.Completion.ValidateReadback(completed.FinalIdentityIndex));
        Assert.NotEmpty(result.Completion.ValidateForPublication(prepared, Source with { RequestId = "other_request" }));
    }

    [Theory]
    [InlineData("child")]
    [InlineData("turn")]
    [InlineData("source")]
    [InlineData("missing-field")]
    [InlineData("fingerprint")]
    [InlineData("capacity")]
    [InlineData("volume")]
    [InlineData("unknown")]
    [InlineData("semantic-name")]
    [InlineData("semantic-price")]
    [InlineData("semantic-type")]
    public void PresentHistoricalEvidenceRejectsDetachedMutation(string mutation)
    {
        var (prepared, phase, catalog) = Arrange(false);
        var result = MortalLocationStorageBirthEvidence.Finalize(prepared, phase, catalog, new Dictionary<string, string>(), Source);
        Assert.True(result.IsValid);
        var plan = result.Completion!.Plan;
        var transition = Transition(plan);
        var after = transition["afterState"]!.AsObject();
        var evidence = after["cargoBirthEvidence"]!.AsObject();
        var row = evidence["cargoMeasurements"]![0]!.AsObject();
        switch (mutation)
        {
            case "child": evidence["storageId"] = "other"; break;
            case "turn": evidence["sourceTurn"] = 43; break;
            case "source": evidence["sourceAuthority"]!["requestId"] = ""; break;
            case "missing-field": row.Remove("materializationId"); break;
            case "fingerprint": row["configurationFingerprint"] = "invalid"; break;
            case "capacity": after["capacity"] = 0; break;
            case "volume": after["volume"] = 0; break;
            case "unknown": evidence["untrusted"] = true; break;
            default:
                var basis = row["measuredConfiguration"]!.AsObject();
                basis["item"]!.AsObject().Remove(mutation["semantic-".Length..]);
                row["configurationFingerprint"] = MortalCargoBirthCapacity.ConfigurationFingerprint(basis);
                break;
        }
        Assert.Contains(MortalLocationIdentityState.Parse(plan.FinalIdentityIndex).Issues,
            i => i.Code == "mortal_location_storage_birth_evidence_invalid");
    }

    [Fact]
    public void HistoricalBirthDoesNotRecheckMutableContentsOrRetiredStorage()
    {
        var (prepared, phase, catalog) = Arrange(false);
        var completion = MortalLocationStorageBirthEvidence.Finalize(prepared, phase, catalog,
            new Dictionary<string, string>(), Source).Completion!;
        var detached = completion.Plan;
        detached.FinalCurrentLocation!["locationStorages"] = new JsonArray();
        detached.FinalWorldMap["locations"]![0]!["locationStorages"] = new JsonArray();
        Assert.Empty(MortalLocationIdentityState.Parse(detached.FinalIdentityIndex).Issues);
    }

    [Fact]
    public void CapturedContainerRebuildsNestedGraphWithoutLiveItemAuthority()
    {
        var raw = Raw(false);
        var storage = raw["locationStorages"]![0]!.AsObject();
        var parent = storage["contents"]![0]!.AsObject();
        parent["isContainer"] = true; parent["capacity"] = 2;
        var child = MortalItemTestFixture.CreateRawRoot(creationRef: "new_nested", materializationId: "mat_nested");
        child["itemId"] = "item_nested"; child.Remove("creationRef");
        child["contentsPath"] = new JsonArray("item_historical");
        storage["contents"]!.AsArray().Add(child);
        JsonObject Shape(JsonObject item)
        {
            var shape = item.DeepClone().AsObject();
            foreach (var key in shape.Select(p => p.Key).Where(MortalCargoBirthCapacity.IsIdentityShapeField).ToArray()) shape.Remove(key);
            return shape;
        }
        var basis = new JsonObject { ["schemaVersion"] = 1, ["item"] = Shape(parent), ["contents"] = new JsonArray(
            new JsonObject { ["itemId"] = "item_nested", ["count"] = 1, ["item"] = Shape(child), ["contents"] = new JsonArray() }) };
        storage["cargoMeasurements"] = new JsonArray(new JsonObject { ["itemId"] = "item_historical", ["count"] = 1,
            ["externalVolumeDm3"] = 1d, ["measuredConfiguration"] = basis });
        var (prepared, phase, catalog) = Arrange(raw);
        var finalized = MortalLocationStorageBirthEvidence.Finalize(prepared, phase, catalog, new Dictionary<string, string>(), Source);
        Assert.True(finalized.IsValid, string.Join(";", finalized.Issues.Select(i => i.Code + ":" + i.FilePath)));
        Assert.Empty(MortalLocationIdentityState.Parse(finalized.Completion!.Plan.FinalIdentityIndex).Issues);
    }

    [Fact]
    public void RawHistoricalEvidenceInjectionFailsBeforeDraftAllocation()
    {
        var raw = Raw(false);
        raw["locationStorages"]![0]!["cargoBirthEvidence"] = new JsonObject();
        Assert.Contains(Prepare(raw).Issues, i => i.Code == "mortal_location_storage_birth_raw_evidence_forbidden");
    }

    private static readonly MortalStorageBirthSourceBinding Source = new("session", "request", "snapshot", 42, "projection");
    private static JsonObject Transition(MortalLocationAcceptedTurnPlan plan) =>
        plan.FinalIdentityIndex["locationEntries"]![0]!["transitions"]![0]!.AsObject();

    private static (MortalLocationAcceptedTurnPlan, MortalItemCanonicalProjectionResult, MortalItemCarrierCatalog) Arrange(bool empty)
        => Arrange(Raw(empty));

    private static (MortalLocationAcceptedTurnPlan, MortalItemCanonicalProjectionResult, MortalItemCarrierCatalog) Arrange(JsonObject raw)
    {
        var planning = Prepare(raw);
        Assert.True(planning.Success, string.Join(";", planning.Issues.Select(i => i.Code + ":" + i.FilePath)));
        var plan = planning.Plan!;
        var current = plan.FinalCurrentLocation!.DeepClone().AsObject();
        var contents = current["locationStorages"]![0]!["contents"]!.AsArray();
        if (contents.Count > 0)
        {
            contents[0]!["itemId"] = "item_historical";
            contents[0]!.AsObject().Remove("creationRef");
        }
        var roots = new Dictionary<string, JsonNode?> { [StorageTransportMoveService.CurrentLocationPath] = current };
        var phase = new MortalItemCanonicalProjectionResult(roots, MortalItemIdentityState.CreateEmptyRoot(), Array.Empty<ValidationIssue>(), "phase");
        var catalog = MortalItemCarrierCatalog.Build(new(null, null, null, current, null, new Dictionary<string, JsonObject>()));
        return (plan, phase, catalog);
    }

    private static MortalLocationAcceptedTurnPlanningResult Prepare(JsonObject raw) => MortalLocationAcceptedTurnPlanner.Build(new(
        new JsonObject { ["schemaVersion"] = 1, ["realm"] = "mortal_world", ["locations"] = new JsonArray(), ["links"] = new JsonArray() },
        null, MortalLocationIdentityState.CreateEmptyRoot(), raw, null, 42));

    private static JsonObject Raw(bool empty)
    {
        var raw = MortalLocationTestFixture.CreateRawLocation("current_scene_creation");
        raw["locationStorages"] = new JsonArray(new JsonObject
        {
            ["storageId"] = "storage_birth", ["name"] = "Chest", ["description"] = "A sturdy chest.",
            ["image_prompt"] = "A plain wooden chest", ["capacity"] = 1, ["volume"] = 1,
            ["owner"] = null,
            ["authorizedUsers"] = new JsonArray(), ["hasFullAccess"] = true,
            ["contents"] = empty ? new JsonArray() : new JsonArray(MortalItemTestFixture.CreateRawRoot())
        });
        if (!empty) raw["locationStorages"]![0]!["contents"]![0]!["volume"] = 1;
        raw["materialization"]!["sections"]!["storageMetadata"]!["state"] = "populated";
        raw["materialization"]!["sections"]!["storageMetadata"]!["reason"] = null;
        return raw;
    }
}
