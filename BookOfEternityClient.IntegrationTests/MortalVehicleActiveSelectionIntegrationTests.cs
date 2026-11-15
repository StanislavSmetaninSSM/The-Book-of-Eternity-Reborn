using System.Text.Json.Nodes;
using BookOfEternityClient.Services;
using BookOfEternityClient.Core;
using Xunit.Abstractions;
using Xunit;

namespace BookOfEternityClient.Tests;

/// <summary>Exercises existing admission and common publication, not a parallel vehicle writer.</summary>
public sealed class MortalVehicleActiveSelectionIntegrationTests(ITestOutputHelper output)
{
    [Theory]
    [InlineData("new_cart", false)]
    [InlineData("NEW_CART", true)]
    [InlineData(" new_cart ", true)]
    public async Task CrossReferences_AllowExactSameTurnVehicleRefSelection(string selected, bool rejected)
    {
        await using var context = await ResourceMaterializationTestContext.CreateAsync();
        var raw = RawCreation();
        raw["activeVehicleChange"] = selected;
        await context.WriteExactJsonAsync(StorageTransportMoveService.VehiclesPath, raw.ToJsonString());
        var issues = await context.Validator.ValidateGameStateAsync(GameStateValidationPhase.CrossReferences);
        Assert.Equal(rejected, issues.Any(issue => issue.Code == "vehicle_active_change_unknown_target"));
        Assert.DoesNotContain(issues, issue => issue.Code == "resource_owner_vehicle_preassigned_id_forbidden");
    }

    [Fact]
    public async Task MetaMisc_AcceptsCanonicalEmptySelection()
    {
        await using var context = await ResourceMaterializationTestContext.CreateAsync();
        await context.WriteExactJsonAsync(StorageTransportMoveService.VehiclesPath,
            new JsonObject { ["vehicles"] = new JsonArray(), ["activeVehicleId"] = null }.ToJsonString());
        var issues = await context.Validator.ValidateGameStateAsync(GameStateValidationPhase.MetaMiscStateFiles);
        Assert.DoesNotContain(issues, issue => issue.Severity == IssueSeverity.Error);
    }

    [Fact]
    public async Task AcceptedTurn_ActiveBirthAndRemovalPublishWithHealthOwner()
    {
        await using var context = await ResourceMaterializationTestContext.CreateAsync();
        await CanonicalResourceQuartetTestFixture.CommitFreshBootstrapAsync(context.FileSystem);
        await context.WriteExactJsonAsync(StorageTransportMoveService.VehiclesPath,
            new JsonObject { ["vehicles"] = new JsonArray(), ["activeVehicleId"] = null }.ToJsonString());
        await context.CaptureValidatedPendingSnapshotAsync(turn: 42);
        await context.WriteExactJsonAsync(StorageTransportMoveService.VehiclesPath, RawCreation().ToJsonString());
        var issues = await context.Validator.ValidateAcceptedTurnRawResourceMaterializationAsync();
        Assert.DoesNotContain(issues, issue => issue.Severity == IssueSeverity.Error);
        var handoff = await AcceptedMechanicsAuthorityTestProbe.PeekCommonAsync(context.FileSystem);
        Assert.NotNull(handoff);
        var plan = Assert.IsType<AcceptedMechanicsPlan>(handoff.Result.Plan);
        var owner = Assert.Single(plan.OwnerAuthority.Entries.Values, e => e.Key.OwnerKind == ResourceOwnerKind.Vehicle);
        await using (var lease = await context.FileSystem.AcquireCanonicalWriteLeaseAsync())
            Assert.Same(plan, await context.Normalizer.BindTo(lease).NormalizeAcceptedMechanicsAsync(backups: null));
        var canonical = Assert.IsType<JsonObject>(await context.ReadJsonAsync(StorageTransportMoveService.VehiclesPath));
        Assert.Equal(owner.Key.ResourceOwnerId, canonical["activeVehicleId"]!.GetValue<string>());
        Assert.Equal(owner.Key.ResourceOwnerId, Assert.Single(canonical["vehicles"]!.AsArray())!["vehicleId"]!.GetValue<string>());
        Assert.False(canonical.ContainsKey("activeVehicleChange"));
        Assert.False(canonical.ContainsKey("UpdateVehicles"));

        await context.CaptureValidatedPendingSnapshotAsync(turn: 43);
        canonical["removeVehicles"] = new JsonArray(owner.Key.ResourceOwnerId);
        await context.WriteExactJsonAsync(StorageTransportMoveService.VehiclesPath, canonical.ToJsonString());
        var removalIssues = await context.Validator.ValidateAcceptedTurnRawResourceMaterializationAsync();
        Assert.DoesNotContain(removalIssues, issue => issue.Severity == IssueSeverity.Error);
        await using (var lease = await context.FileSystem.AcquireCanonicalWriteLeaseAsync())
            Assert.NotNull(await context.Normalizer.BindTo(lease).NormalizeAcceptedMechanicsAsync(backups: null));
        var removed = Assert.IsType<JsonObject>(await context.ReadJsonAsync(StorageTransportMoveService.VehiclesPath));
        Assert.Null(removed["activeVehicleId"]);
        Assert.Empty(removed["vehicles"]!.AsArray());
        Assert.False(removed.ContainsKey("removeVehicles"));
    }

    [Fact]
    public async Task ActiveBirth_LatePhysicalVehicleWriteRestoresExactBeforeImages()
    {
        KnownRollbackPublicationCut? cut = null;
        var hooks = new FileSystemManagerHooks
        {
            LocalPublicationObserver = (phase, index) => cut?.Hooks.LocalPublicationObserver?.Invoke(phase, index)
        };
        await using var context = await ResourceMaterializationTestContext.CreateAsync(hooks);
        await CanonicalResourceQuartetTestFixture.CommitFreshBootstrapAsync(context.FileSystem);
        await context.WriteExactJsonAsync(MortalItemIdentityState.StatePath, MortalItemIdentityState.CreateEmptyRoot().ToJsonString());
        await context.WriteExactJsonAsync(StorageTransportMoveService.VehiclesPath,
            new JsonObject { ["vehicles"] = new JsonArray(), ["activeVehicleId"] = null }.ToJsonString());
        await context.CaptureValidatedPendingSnapshotAsync(turn: 42);
        await context.WriteExactJsonAsync(StorageTransportMoveService.VehiclesPath, RawCreation().ToJsonString());
        var itemIssues = await context.Validator.ValidateAcceptedTurnRawMortalItemMaterializationAsync();
        Assert.DoesNotContain(itemIssues, issue => issue.Severity == IssueSeverity.Error);
        var issues = await context.Validator.ValidateAcceptedTurnRawResourceMaterializationAsync();
        Assert.DoesNotContain(issues, issue => issue.Severity == IssueSeverity.Error);
        var paths = CanonicalStateNormalizer.NormalizerRollbackTrackedFiles.Distinct(StringComparer.Ordinal).ToArray();
        var captured = await context.CaptureAsync(paths);
        var before = captured.ToDictionary(pair => pair.Key, pair => pair.Value.Bytes, StringComparer.Ordinal);
        var generation = CleanupPublicationCut.ReadOptional(context.FileSystem.SessionGenerationPath);
        cut = new KnownRollbackPublicationCut(context.FileSystem.ResolvePath(StorageTransportMoveService.VehiclesPath));
        cut.Attach(context.FileSystem);
        var failure = await Record.ExceptionAsync(() => AcceptedTurnCanonicalStateRefresh.NormalizeAndValidateAsync(
            context.FileSystem, context.Normalizer, context.Validator, new Dictionary<string, string>(StringComparer.Ordinal)));
        cut.AssertRestored(before, generation, failure, output.WriteLine);
        var writeFailure = Assert.IsType<CanonicalStateWriteException>(failure);
        Assert.Equal(StorageTransportMoveService.VehiclesPath, writeFailure.RelativePath);
        Assert.Same(cut.Failure, writeFailure.InnerException);
        var published = JsonNode.Parse(cut.PublishedBytes!)!.AsObject();
        Assert.NotNull(published["activeVehicleId"]);
        Assert.Equal(published["vehicles"]![0]!["vehicleId"]!.GetValue<string>(), published["activeVehicleId"]!.GetValue<string>());
    }

    private static JsonObject RawCreation() => new()
    {
        ["vehicles"] = new JsonArray(), ["activeVehicleId"] = null,
        ["activeVehicleChange"] = "new_cart",
        ["UpdateVehicles"] = new JsonArray(new JsonObject
        {
            ["vehicleRef"] = "new_cart", ["name"] = "Cart",
            ["description"] = "Serviceable wooden cargo cart.", ["image_prompt"] = "wooden cart",
            ["type"] = "Vehicle", ["isSentient"] = false, ["availability"] = "Active",
            ["currentLocationId"] = null, ["speedBonus"] = 0,
            ["actions"] = new JsonArray(), ["resistances"] = new JsonArray(), ["inventory"] = new JsonArray(),
            ["capacity"] = 4, ["volume"] = 20,
            ["owner"] = new JsonObject { ["ownerType"] = "Player", ["ownerId"] = "player_current", ["ownerName"] = "Player" },
            ["controller"] = new JsonObject { ["actorType"] = "player", ["actorId"] = "player_current" },
            ["authorizedUsers"] = new JsonArray(), ["hasFullAccess"] = true,
            ["resourceMaterialization"] = new JsonObject
            {
                ["resources"] = new JsonArray(new JsonObject { ["resourceKey"] = "health", ["maximum"] = 100 })
            }
        })
    };
}
