using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization.Metadata;
using BookOfEternityClient.Configuration;
using BookOfEternityClient.Core;
using BookOfEternityClient.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed class ShiningCoreActionResolutionValidationTests : IDisposable
{
    private readonly string _rootPath;
    private readonly FileSystemManager _fs;
    private readonly ValidationService _validator;

    public ShiningCoreActionResolutionValidationTests()
    {
        _rootPath = Path.Combine(Path.GetTempPath(), "boe-shining-core-resolution-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_rootPath);
        _fs = new FileSystemManager(_rootPath, NullLogger<FileSystemManager>.Instance);
        _fs.EnsureDirectoryStructure();
        _validator = new ValidationService(_fs, NullLogger<ValidationService>.Instance);
    }

    [Fact]
    public async Task ValidatePendingShiningCoreActionResolutionAsync_AcceptedOpenGatesWithCanonicalDraft_Passes()
    {
        var preTurnShiningRoot = CreateBaseShiningRoot();
        var preTurnResidentRoot = CreateBaseResidentRoot();
        var preTurnSoulRoot = CreateBaseSoulRoot();

        var currentShiningRoot = CloneJsonObject(preTurnShiningRoot);
        Assert.True(ShiningAbodeState.TryOpenGates(currentShiningRoot, CloneJsonObject(preTurnResidentRoot), out _));
        ShiningAbodeState.EnsureCoreActionReceiptsArray(currentShiningRoot).Add(new JsonObject
        {
            ["requestId"] = "core_req_open_gates",
            ["actionType"] = ShiningCoreActionRequestState.ActionTypeOpenGates,
            ["status"] = ShiningCoreActionRequestState.RequestStatusAccepted,
            ["factionId"] = "",
            ["projectId"] = "",
            ["hallId"] = "",
            ["resolvedFactionId"] = "",
            ["selectedCardIds"] = new JsonArray(),
            ["newResidentIds"] = new JsonArray(),
            ["seededProjectIds"] = new JsonArray(),
            ["generatedDraftVersion"] = GetNodeInt(currentShiningRoot["gates"]?["draftVersion"]),
            ["resolvedAtTurn"] = 14,
            ["resolvedAtUtc"] = "2026-04-16T12:30:00Z",
            ["reason"] = "gates_opened"
        });

        await SeedCurrentStateAsync(currentShiningRoot, preTurnResidentRoot, preTurnSoulRoot);
        await WriteNodeAsync(ShiningCoreActionRequestState.PendingActionsRequestPath, new JsonObject
        {
            [ShiningCoreActionRequestState.RequestsProperty] = new JsonArray(new JsonObject
            {
                ["requestId"] = "core_req_open_gates",
                ["actionType"] = ShiningCoreActionRequestState.ActionTypeOpenGates,
                ["factionId"] = "",
                ["factionName"] = "",
                ["projectId"] = "",
                ["projectDisplayName"] = "",
                ["radianceTierAtRequest"] = 0,
                ["quotedCostFeathers"] = 0,
                ["quotedCostLightSparks"] = 0,
                ["sourceDraftVersion"] = 0,
                ["selectedCardIds"] = new JsonArray(),
                ["createdAtTurn"] = 14,
                ["createdAtUtc"] = "2026-04-16T12:25:00Z"
            })
        });
        await WritePendingTurnSnapshotManifestAsync(preTurnShiningRoot, preTurnResidentRoot, preTurnSoulRoot, new JsonObject
        {
            [ShiningCoreActionRequestState.RequestsProperty] = new JsonArray(new JsonObject
            {
                ["requestId"] = "core_req_open_gates",
                ["actionType"] = ShiningCoreActionRequestState.ActionTypeOpenGates,
                ["factionId"] = "",
                ["factionName"] = "",
                ["projectId"] = "",
                ["projectDisplayName"] = "",
                ["radianceTierAtRequest"] = 0,
                ["quotedCostFeathers"] = 0,
                ["quotedCostLightSparks"] = 0,
                ["sourceDraftVersion"] = 0,
                ["selectedCardIds"] = new JsonArray(),
                ["createdAtTurn"] = 14,
                ["createdAtUtc"] = "2026-04-16T12:25:00Z"
            })
        });

        var issues = await InvokeValidationAsync("ValidatePendingShiningCoreActionResolutionAsync");

        Assert.DoesNotContain(issues, issue => string.Equals(issue.Code, "shining_core_action_missing_resolution", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(issues, issue => string.Equals(issue.Code, "shining_core_action_projected_state_mismatch", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task ValidatePendingShiningCoreActionResolutionAsync_DiscoverNativeFactionReusingExistingIds_Fails()
    {
        var preTurnShiningRoot = CreateBaseShiningRoot();
        var preTurnResidentRoot = CreateBaseResidentRoot();
        var preTurnSoulRoot = CreateBaseSoulRoot();

        var currentShiningRoot = CloneJsonObject(preTurnShiningRoot);
        currentShiningRoot["radiance"]!["experience"] = GetNodeInt(preTurnShiningRoot["radiance"]?["experience"]) + 20;
        currentShiningRoot["lightSparks"] = 60;
        var reusedFaction = ShiningAbodeState.FindFaction(currentShiningRoot, "faction_old")!;
        reusedFaction["originType"] = ShiningAbodeState.OriginTypeNativeRadiant;
        reusedFaction["projects"]!.AsArray().Add(CreateDiscoveryProject("project_seeded_new"));
        ShiningAbodeState.EnsureCoreActionReceiptsArray(currentShiningRoot).Add(new JsonObject
        {
            ["requestId"] = "core_req_discover",
            ["actionType"] = ShiningCoreActionRequestState.ActionTypeDiscoverNativeFaction,
            ["status"] = ShiningCoreActionRequestState.RequestStatusAccepted,
            ["factionId"] = "",
            ["projectId"] = "",
            ["hallId"] = "hall_old",
            ["resolvedFactionId"] = "faction_old",
            ["selectedCardIds"] = new JsonArray(),
            ["newResidentIds"] = new JsonArray("resident_new_a", "resident_new_b"),
            ["seededProjectIds"] = new JsonArray("project_passage", "project_seeded_new"),
            ["resolvedAtTurn"] = 16,
            ["resolvedAtUtc"] = "2026-04-16T12:50:00Z",
            ["reason"] = "discovered"
        });

        var currentResidentRoot = CloneJsonObject(preTurnResidentRoot);
        var residents = currentResidentRoot["entries"]!.AsArray();
        residents.Add(CreateDiscoveryResident("resident_new_a", "faction_old"));
        residents.Add(CreateDiscoveryResident("resident_new_b", "faction_old"));

        var currentSoulRoot = CloneJsonObject(preTurnSoulRoot);
        currentSoulRoot["inkFeathers"]!["current"] = 25;

        var requestRoot = new JsonObject
        {
            [ShiningCoreActionRequestState.RequestsProperty] = new JsonArray(CreateDiscoverNativeFactionRequest())
        };

        await SeedCurrentStateAsync(currentShiningRoot, currentResidentRoot, currentSoulRoot);
        await WriteNodeAsync(ShiningCoreActionRequestState.PendingActionsRequestPath, requestRoot);
        await WritePendingTurnSnapshotManifestAsync(preTurnShiningRoot, preTurnResidentRoot, preTurnSoulRoot, CloneJsonObject(requestRoot));

        var issues = await InvokeValidationAsync("ValidatePendingShiningCoreActionResolutionAsync");

        Assert.Contains(issues, issue => string.Equals(issue.Code, "shining_discovery_reused_existing_hall_id", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(issues, issue => string.Equals(issue.Code, "shining_discovery_reused_existing_faction_id", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(issues, issue => string.Equals(issue.Code, "shining_discovery_reused_existing_project_id", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task ValidateLegacyPendingShiningNativeFactionDiscoveryResolutionAsync_UnresolvedPending_Fails()
    {
        var preTurnShiningRoot = CreateBaseShiningRootWithLegacyNativeDiscovery();
        var preTurnResidentRoot = CreateBaseResidentRoot();
        var preTurnSoulRoot = CreateBaseSoulRoot();
        var currentShiningRoot = CloneJsonObject(preTurnShiningRoot);

        await SeedCurrentStateAsync(currentShiningRoot, preTurnResidentRoot, preTurnSoulRoot);
        await WriteLegacyNativeDiscoverySnapshotManifestAsync(preTurnShiningRoot, preTurnResidentRoot, preTurnSoulRoot);

        var issues = await InvokeValidationAsync("ValidateLegacyPendingShiningNativeFactionDiscoveryResolutionAsync");

        Assert.Contains(issues, issue => string.Equals(issue.Code, "shining_legacy_native_discovery_missing_resolution", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task ValidateLegacyPendingShiningNativeFactionDiscoveryResolutionAsync_AcceptedClosure_Passes()
    {
        var preTurnShiningRoot = CreateBaseShiningRootWithLegacyNativeDiscovery();
        var preTurnResidentRoot = CreateBaseResidentRoot();
        var preTurnSoulRoot = CreateBaseSoulRoot();
        var currentShiningRoot = CloneJsonObject(preTurnShiningRoot);
        var currentResidentRoot = CloneJsonObject(preTurnResidentRoot);
        var currentSoulRoot = CloneJsonObject(preTurnSoulRoot);
        MaterializeLegacyNativeDiscoveryClosure(currentShiningRoot, currentResidentRoot, currentSoulRoot);

        await SeedCurrentStateAsync(currentShiningRoot, currentResidentRoot, currentSoulRoot);
        await WriteLegacyNativeDiscoverySnapshotManifestAsync(preTurnShiningRoot, preTurnResidentRoot, preTurnSoulRoot);

        var issues = await InvokeValidationAsync("ValidateLegacyPendingShiningNativeFactionDiscoveryResolutionAsync");

        Assert.DoesNotContain(issues, issue => string.Equals(issue.Code, "shining_legacy_native_discovery_missing_resolution", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(issues, issue => string.Equals(issue.Code, "shining_legacy_native_discovery_not_cleared", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(issues, issue => string.Equals(issue.Code, "shining_discovery_light_sparks_cost_mismatch", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(issues, issue => string.Equals(issue.Code, "shining_discovery_feather_cost_mismatch", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task ValidateLegacyPendingShiningNativeFactionDiscoveryResolutionAsync_DoubleSpendsReservedLightSparks_Fails()
    {
        var preTurnShiningRoot = CreateBaseShiningRootWithLegacyNativeDiscovery();
        var preTurnResidentRoot = CreateBaseResidentRoot();
        var preTurnSoulRoot = CreateBaseSoulRoot();
        var currentShiningRoot = CloneJsonObject(preTurnShiningRoot);
        var currentResidentRoot = CloneJsonObject(preTurnResidentRoot);
        var currentSoulRoot = CloneJsonObject(preTurnSoulRoot);
        MaterializeLegacyNativeDiscoveryClosure(currentShiningRoot, currentResidentRoot, currentSoulRoot);
        currentShiningRoot["lightSparks"] = GetNodeInt(preTurnShiningRoot["lightSparks"]) - ShiningAbodeState.GetNativeDiscoveryCost().LightSparks;

        await SeedCurrentStateAsync(currentShiningRoot, currentResidentRoot, currentSoulRoot);
        await WriteLegacyNativeDiscoverySnapshotManifestAsync(preTurnShiningRoot, preTurnResidentRoot, preTurnSoulRoot);

        var issues = await InvokeValidationAsync("ValidateLegacyPendingShiningNativeFactionDiscoveryResolutionAsync");

        Assert.Contains(issues, issue => string.Equals(issue.Code, "shining_discovery_light_sparks_cost_mismatch", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task ValidatePendingShiningCoreActionResolutionAsync_AcceptedPreparePackageWithoutMaterializedPackage_Fails()
    {
        var preTurnShiningRoot = CreateBaseShiningRoot();
        var gates = preTurnShiningRoot["gates"]!.AsObject();
        gates["selectedBlessingCardIds"] = new JsonArray("card_social");
        var preTurnResidentRoot = CreateBaseResidentRoot();
        var preTurnSoulRoot = CreateBaseSoulRoot();

        var currentShiningRoot = CloneJsonObject(preTurnShiningRoot);
        ShiningAbodeState.EnsureCoreActionReceiptsArray(currentShiningRoot).Add(new JsonObject
        {
            ["requestId"] = "core_req_prepare_package",
            ["actionType"] = ShiningCoreActionRequestState.ActionTypePrepareIncarnationPackage,
            ["status"] = ShiningCoreActionRequestState.RequestStatusAccepted,
            ["factionId"] = "",
            ["projectId"] = "",
            ["hallId"] = "",
            ["resolvedFactionId"] = "",
            ["selectedCardIds"] = new JsonArray("card_social"),
            ["newResidentIds"] = new JsonArray(),
            ["seededProjectIds"] = new JsonArray(),
            ["generatedDraftVersion"] = 1,
            ["resolvedAtTurn"] = 15,
            ["resolvedAtUtc"] = "2026-04-16T12:40:00Z",
            ["reason"] = "package_prepared"
        });

        await SeedCurrentStateAsync(currentShiningRoot, preTurnResidentRoot, preTurnSoulRoot);
        await WriteNodeAsync(ShiningCoreActionRequestState.PendingActionsRequestPath, new JsonObject
        {
            [ShiningCoreActionRequestState.RequestsProperty] = new JsonArray(new JsonObject
            {
                ["requestId"] = "core_req_prepare_package",
                ["actionType"] = ShiningCoreActionRequestState.ActionTypePrepareIncarnationPackage,
                ["factionId"] = "",
                ["factionName"] = "",
                ["projectId"] = "",
                ["projectDisplayName"] = "",
                ["radianceTierAtRequest"] = 0,
                ["quotedCostFeathers"] = 0,
                ["quotedCostLightSparks"] = 0,
                ["sourceDraftVersion"] = 1,
                ["selectedCardIds"] = new JsonArray("card_social"),
                ["createdAtTurn"] = 15,
                ["createdAtUtc"] = "2026-04-16T12:39:00Z"
            })
        });
        await WritePendingTurnSnapshotManifestAsync(preTurnShiningRoot, preTurnResidentRoot, preTurnSoulRoot, new JsonObject
        {
            [ShiningCoreActionRequestState.RequestsProperty] = new JsonArray(new JsonObject
            {
                ["requestId"] = "core_req_prepare_package",
                ["actionType"] = ShiningCoreActionRequestState.ActionTypePrepareIncarnationPackage,
                ["factionId"] = "",
                ["factionName"] = "",
                ["projectId"] = "",
                ["projectDisplayName"] = "",
                ["radianceTierAtRequest"] = 0,
                ["quotedCostFeathers"] = 0,
                ["quotedCostLightSparks"] = 0,
                ["sourceDraftVersion"] = 1,
                ["selectedCardIds"] = new JsonArray("card_social"),
                ["createdAtTurn"] = 15,
                ["createdAtUtc"] = "2026-04-16T12:39:00Z"
            })
        });

        var issues = await InvokeValidationAsync("ValidatePendingShiningCoreActionResolutionAsync");

        Assert.Contains(issues, issue => string.Equals(issue.Code, "shining_prepare_package_state_mismatch", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task ValidatePendingShiningCoreActionResolutionAsync_AcceptedPreparePackageWithoutReceiptSnapshot_Fails()
    {
        var preTurnShiningRoot = CreateBaseShiningRoot();
        var gates = preTurnShiningRoot["gates"]!.AsObject();
        gates["selectedBlessingCardIds"] = new JsonArray("card_social");
        var preTurnResidentRoot = CreateBaseResidentRoot();
        var preTurnSoulRoot = CreateBaseSoulRoot();

        var currentShiningRoot = CloneJsonObject(preTurnShiningRoot);
        Assert.True(ShiningAbodeState.TryPrepareIncarnationPackage(currentShiningRoot, 15, out _, "2026-04-16T12:40:00Z"));
        ShiningAbodeState.EnsureCoreActionReceiptsArray(currentShiningRoot).Add(new JsonObject
        {
            ["requestId"] = "core_req_prepare_package",
            ["actionType"] = ShiningCoreActionRequestState.ActionTypePrepareIncarnationPackage,
            ["status"] = ShiningCoreActionRequestState.RequestStatusAccepted,
            ["factionId"] = "",
            ["projectId"] = "",
            ["hallId"] = "",
            ["resolvedFactionId"] = "",
            ["selectedCardIds"] = new JsonArray("card_social"),
            ["newResidentIds"] = new JsonArray(),
            ["seededProjectIds"] = new JsonArray(),
            ["generatedDraftVersion"] = 1,
            ["resolvedAtTurn"] = 15,
            ["resolvedAtUtc"] = "2026-04-16T12:40:00Z",
            ["reason"] = "package_prepared"
        });

        await SeedCurrentStateAsync(currentShiningRoot, preTurnResidentRoot, preTurnSoulRoot);
        await WriteNodeAsync(ShiningCoreActionRequestState.PendingActionsRequestPath, new JsonObject
        {
            [ShiningCoreActionRequestState.RequestsProperty] = new JsonArray(new JsonObject
            {
                ["requestId"] = "core_req_prepare_package",
                ["actionType"] = ShiningCoreActionRequestState.ActionTypePrepareIncarnationPackage,
                ["factionId"] = "",
                ["factionName"] = "",
                ["projectId"] = "",
                ["projectDisplayName"] = "",
                ["radianceTierAtRequest"] = 0,
                ["quotedCostFeathers"] = 0,
                ["quotedCostLightSparks"] = 0,
                ["sourceDraftVersion"] = 1,
                ["selectedCardIds"] = new JsonArray("card_social"),
                ["createdAtTurn"] = 15,
                ["createdAtUtc"] = "2026-04-16T12:39:00Z"
            })
        });
        await WritePendingTurnSnapshotManifestAsync(preTurnShiningRoot, preTurnResidentRoot, preTurnSoulRoot, new JsonObject
        {
            [ShiningCoreActionRequestState.RequestsProperty] = new JsonArray(new JsonObject
            {
                ["requestId"] = "core_req_prepare_package",
                ["actionType"] = ShiningCoreActionRequestState.ActionTypePrepareIncarnationPackage,
                ["factionId"] = "",
                ["factionName"] = "",
                ["projectId"] = "",
                ["projectDisplayName"] = "",
                ["radianceTierAtRequest"] = 0,
                ["quotedCostFeathers"] = 0,
                ["quotedCostLightSparks"] = 0,
                ["sourceDraftVersion"] = 1,
                ["selectedCardIds"] = new JsonArray("card_social"),
                ["createdAtTurn"] = 15,
                ["createdAtUtc"] = "2026-04-16T12:39:00Z"
            })
        });

        var issues = await InvokeValidationAsync("ValidatePendingShiningCoreActionResolutionAsync");

        Assert.Contains(issues, issue => string.Equals(issue.Code, "shining_prepare_package_receipt_snapshot_mismatch", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task ValidatePendingShiningCoreActionResolutionAsync_AcceptedPreparePackageWithReceiptSnapshot_Passes()
    {
        var preTurnShiningRoot = CreateBaseShiningRoot();
        var gates = preTurnShiningRoot["gates"]!.AsObject();
        gates["selectedBlessingCardIds"] = new JsonArray("card_social");
        var preTurnResidentRoot = CreateBaseResidentRoot();
        var preTurnSoulRoot = CreateBaseSoulRoot();

        var currentShiningRoot = CloneJsonObject(preTurnShiningRoot);
        Assert.True(ShiningAbodeState.TryPrepareIncarnationPackage(currentShiningRoot, 15, out _, "2026-04-16T12:40:00Z"));
        var selectedCards = (currentShiningRoot["preparedIncarnationPackage"]?["selectedCards"] as JsonArray)?.DeepClone().AsArray()
            ?? throw new InvalidOperationException("Expected selectedCards snapshot from prepared package.");
        ShiningAbodeState.EnsureCoreActionReceiptsArray(currentShiningRoot).Add(new JsonObject
        {
            ["requestId"] = "core_req_prepare_package",
            ["actionType"] = ShiningCoreActionRequestState.ActionTypePrepareIncarnationPackage,
            ["status"] = ShiningCoreActionRequestState.RequestStatusAccepted,
            ["factionId"] = "",
            ["projectId"] = "",
            ["hallId"] = "",
            ["resolvedFactionId"] = "",
            ["selectedCardIds"] = new JsonArray("card_social"),
            ["selectedCards"] = selectedCards,
            ["newResidentIds"] = new JsonArray(),
            ["seededProjectIds"] = new JsonArray(),
            ["generatedDraftVersion"] = 1,
            ["resolvedAtTurn"] = 15,
            ["resolvedAtUtc"] = "2026-04-16T12:40:00Z",
            ["reason"] = "package_prepared"
        });

        await SeedCurrentStateAsync(currentShiningRoot, preTurnResidentRoot, preTurnSoulRoot);
        await WriteNodeAsync(ShiningCoreActionRequestState.PendingActionsRequestPath, new JsonObject
        {
            [ShiningCoreActionRequestState.RequestsProperty] = new JsonArray(new JsonObject
            {
                ["requestId"] = "core_req_prepare_package",
                ["actionType"] = ShiningCoreActionRequestState.ActionTypePrepareIncarnationPackage,
                ["factionId"] = "",
                ["factionName"] = "",
                ["projectId"] = "",
                ["projectDisplayName"] = "",
                ["radianceTierAtRequest"] = 0,
                ["quotedCostFeathers"] = 0,
                ["quotedCostLightSparks"] = 0,
                ["sourceDraftVersion"] = 1,
                ["selectedCardIds"] = new JsonArray("card_social"),
                ["createdAtTurn"] = 15,
                ["createdAtUtc"] = "2026-04-16T12:39:00Z"
            })
        });
        await WritePendingTurnSnapshotManifestAsync(preTurnShiningRoot, preTurnResidentRoot, preTurnSoulRoot, new JsonObject
        {
            [ShiningCoreActionRequestState.RequestsProperty] = new JsonArray(new JsonObject
            {
                ["requestId"] = "core_req_prepare_package",
                ["actionType"] = ShiningCoreActionRequestState.ActionTypePrepareIncarnationPackage,
                ["factionId"] = "",
                ["factionName"] = "",
                ["projectId"] = "",
                ["projectDisplayName"] = "",
                ["radianceTierAtRequest"] = 0,
                ["quotedCostFeathers"] = 0,
                ["quotedCostLightSparks"] = 0,
                ["sourceDraftVersion"] = 1,
                ["selectedCardIds"] = new JsonArray("card_social"),
                ["createdAtTurn"] = 15,
                ["createdAtUtc"] = "2026-04-16T12:39:00Z"
            })
        });

        var issues = await InvokeValidationAsync("ValidatePendingShiningCoreActionResolutionAsync");

        Assert.DoesNotContain(issues, issue => string.Equals(issue.Code, "shining_prepare_package_state_mismatch", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(issues, issue => string.Equals(issue.Code, "shining_prepare_package_receipt_snapshot_mismatch", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task ValidatePendingShiningCoreActionResolutionAsync_AcceptedForgeReshapeWithCanonicalSoulMutation_Passes()
    {
        var preTurnShiningRoot = CreateBaseShiningRoot();
        var faction = preTurnShiningRoot["factions"]!.AsArray()[0]!.AsObject();
        var projects = faction["projects"]!.AsArray();
        projects.Add(new JsonObject
        {
            ["projectId"] = "project_refinement",
            ["displayName"] = "Кузня Отголосков",
            ["summary"] = "Поддерживает refinement.",
            ["toneTags"] = new JsonArray("relic"),
            ["targetFactionIds"] = new JsonArray(),
            ["projectArchetype"] = ShiningAbodeState.ProjectArchetypeRefinement,
            ["outputEffectFamily"] = ShiningAbodeState.EffectFamilyRelic,
            ["tier"] = 2,
            ["status"] = ShiningAbodeState.ProjectStatusCompleted,
            ["isSupported"] = true,
            ["strengthReward"] = 12
        });
        var preTurnResidentRoot = CreateBaseResidentRoot();
        preTurnResidentRoot["entries"]!.AsArray().Add(new JsonObject
        {
            ["residentId"] = "resident_smith",
            ["displayName"] = "Кузнец",
            ["ascensionState"] = ShiningAbodeState.AscensionStateAscended,
            ["shiningFactionId"] = "faction_old",
            ["residentRole"] = ShiningAbodeState.ResidentRoleForgeSupport,
            ["factionLoyaltyLevel"] = 60,
            ["factionLoyaltyTier"] = ShiningAbodeState.ResolveFactionLoyaltyTier(60),
            ["factionRestlessness"] = 20,
            ["factionRealignmentState"] = ShiningAbodeState.ResolveFactionRealignmentState(60, 20)
        });
        var preTurnSoulRoot = CreateBaseSoulRoot();
        preTurnSoulRoot[ShiningBlessingEffectState.SoulStateProperty] = CreatePendingForgeBlessingState();

        var currentShiningRoot = CloneJsonObject(preTurnShiningRoot);
        var currentSoulRoot = CloneJsonObject(preTurnSoulRoot);
        Assert.True(ShiningAbodeState.TryApplyForgeAction(
            currentShiningRoot,
            currentSoulRoot,
            CloneJsonObject(preTurnResidentRoot),
            ShiningCoreActionRequestState.ActionTypeForgeRelicReshape,
            "faction_old",
            "relic_old",
            "lance",
            -1,
            null,
            null,
            currentTurnNumber: 16,
            resolvedAtUtc: "2026-04-16T12:50:00Z",
            out _,
            out _));
        Assert.Equal(50, currentSoulRoot["inkFeathers"]?["current"]?.GetValue<int>());
        ShiningAbodeState.EnsureCoreActionReceiptsArray(currentShiningRoot).Add(new JsonObject
        {
            ["requestId"] = "core_req_forge_reshape",
            ["actionType"] = ShiningCoreActionRequestState.ActionTypeForgeRelicReshape,
            ["status"] = ShiningCoreActionRequestState.RequestStatusAccepted,
            ["factionId"] = "faction_old",
            ["projectId"] = "",
            ["hallId"] = "",
            ["resolvedFactionId"] = "",
            ["relicId"] = "relic_old",
            ["relicName"] = "Старый Клинок",
            ["targetFormTag"] = "lance",
            ["selectedCardIds"] = new JsonArray(),
            ["newResidentIds"] = new JsonArray(),
            ["seededProjectIds"] = new JsonArray(),
            ["generatedDraftVersion"] = 0,
            ["resolvedAtTurn"] = 16,
            ["resolvedAtUtc"] = "2026-04-16T12:50:00Z",
            ["reason"] = "forge_reshape_accepted"
        });

        await SeedCurrentStateAsync(currentShiningRoot, preTurnResidentRoot, currentSoulRoot);
        await WriteNodeAsync(ShiningCoreActionRequestState.PendingActionsRequestPath, new JsonObject
        {
            [ShiningCoreActionRequestState.RequestsProperty] = new JsonArray(new JsonObject
            {
                ["requestId"] = "core_req_forge_reshape",
                ["actionType"] = ShiningCoreActionRequestState.ActionTypeForgeRelicReshape,
                ["factionId"] = "faction_old",
                ["factionName"] = "Старый Дом",
                ["projectId"] = "",
                ["projectDisplayName"] = "",
                ["radianceTierAtRequest"] = 2,
                ["quotedCostFeathers"] = 0,
                ["quotedCostLightSparks"] = 0,
                ["sourceDraftVersion"] = 0,
                ["selectedCardIds"] = new JsonArray(),
                ["relicId"] = "relic_old",
                ["relicName"] = "Старый Клинок",
                ["targetFormTag"] = "lance",
                ["propertyIndex"] = -1,
                ["createdAtTurn"] = 16,
                ["createdAtUtc"] = "2026-04-16T12:49:00Z"
            })
        });
        await WritePendingTurnSnapshotManifestAsync(preTurnShiningRoot, preTurnResidentRoot, preTurnSoulRoot, new JsonObject
        {
            [ShiningCoreActionRequestState.RequestsProperty] = new JsonArray(new JsonObject
            {
                ["requestId"] = "core_req_forge_reshape",
                ["actionType"] = ShiningCoreActionRequestState.ActionTypeForgeRelicReshape,
                ["factionId"] = "faction_old",
                ["factionName"] = "Старый Дом",
                ["projectId"] = "",
                ["projectDisplayName"] = "",
                ["radianceTierAtRequest"] = 2,
                ["quotedCostFeathers"] = 0,
                ["quotedCostLightSparks"] = 0,
                ["sourceDraftVersion"] = 0,
                ["selectedCardIds"] = new JsonArray(),
                ["relicId"] = "relic_old",
                ["relicName"] = "Старый Клинок",
                ["targetFormTag"] = "lance",
                ["propertyIndex"] = -1,
                ["createdAtTurn"] = 16,
                ["createdAtUtc"] = "2026-04-16T12:49:00Z"
            })
        });

        var issues = await InvokeValidationAsync("ValidatePendingShiningCoreActionResolutionAsync");

        Assert.DoesNotContain(issues, issue => string.Equals(issue.Code, "shining_forge_action_projection_failed", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(issues, issue => string.Equals(issue.Code, "shining_forge_action_soul_state_mismatch", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(issues, issue => string.Equals(issue.Code, "shining_forge_action_blessing_entitlement_mismatch", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task ValidatePendingShiningCoreActionResolutionAsync_AcceptedForgeReshapeWithoutFeatherDebit_Fails()
    {
        var preTurnShiningRoot = CreateBaseShiningRoot();
        var faction = preTurnShiningRoot["factions"]!.AsArray()[0]!.AsObject();
        faction["projects"]!.AsArray().Add(new JsonObject
        {
            ["projectId"] = "project_refinement",
            ["displayName"] = "Кузня Отголосков",
            ["summary"] = "Поддерживает refinement.",
            ["toneTags"] = new JsonArray("relic"),
            ["targetFactionIds"] = new JsonArray(),
            ["projectArchetype"] = ShiningAbodeState.ProjectArchetypeRefinement,
            ["outputEffectFamily"] = ShiningAbodeState.EffectFamilyRelic,
            ["tier"] = 2,
            ["status"] = ShiningAbodeState.ProjectStatusCompleted,
            ["isSupported"] = true,
            ["strengthReward"] = 12
        });
        var preTurnResidentRoot = CreateBaseResidentRoot();
        preTurnResidentRoot["entries"]!.AsArray().Add(new JsonObject
        {
            ["residentId"] = "resident_smith",
            ["displayName"] = "Кузнец",
            ["ascensionState"] = ShiningAbodeState.AscensionStateAscended,
            ["shiningFactionId"] = "faction_old",
            ["residentRole"] = ShiningAbodeState.ResidentRoleForgeSupport,
            ["factionLoyaltyLevel"] = 60,
            ["factionLoyaltyTier"] = ShiningAbodeState.ResolveFactionLoyaltyTier(60),
            ["factionRestlessness"] = 20,
            ["factionRealignmentState"] = ShiningAbodeState.ResolveFactionRealignmentState(60, 20)
        });
        var preTurnSoulRoot = CreateBaseSoulRoot();

        var currentShiningRoot = CloneJsonObject(preTurnShiningRoot);
        var currentSoulRoot = CloneJsonObject(preTurnSoulRoot);
        Assert.True(ShiningAbodeState.TryApplyForgeAction(
            currentShiningRoot,
            currentSoulRoot,
            CloneJsonObject(preTurnResidentRoot),
            ShiningCoreActionRequestState.ActionTypeForgeRelicReshape,
            "faction_old",
            "relic_old",
            "lance",
            -1,
            null,
            null,
            currentTurnNumber: 16,
            resolvedAtUtc: "2026-04-16T12:50:00Z",
            out _,
            out _));
        Assert.Equal(45, currentSoulRoot["inkFeathers"]?["current"]?.GetValue<int>());
        currentSoulRoot["inkFeathers"]!["current"] = 50;
        ShiningAbodeState.EnsureCoreActionReceiptsArray(currentShiningRoot).Add(new JsonObject
        {
            ["requestId"] = "core_req_forge_reshape",
            ["actionType"] = ShiningCoreActionRequestState.ActionTypeForgeRelicReshape,
            ["status"] = ShiningCoreActionRequestState.RequestStatusAccepted,
            ["factionId"] = "faction_old",
            ["projectId"] = "",
            ["hallId"] = "",
            ["resolvedFactionId"] = "",
            ["relicId"] = "relic_old",
            ["relicName"] = "Старый Клинок",
            ["targetFormTag"] = "lance",
            ["selectedCardIds"] = new JsonArray(),
            ["newResidentIds"] = new JsonArray(),
            ["seededProjectIds"] = new JsonArray(),
            ["generatedDraftVersion"] = 0,
            ["resolvedAtTurn"] = 16,
            ["resolvedAtUtc"] = "2026-04-16T12:50:00Z",
            ["reason"] = "forge_reshape_accepted"
        });

        await SeedCurrentStateAsync(currentShiningRoot, preTurnResidentRoot, currentSoulRoot);
        await WriteNodeAsync(ShiningCoreActionRequestState.PendingActionsRequestPath, new JsonObject
        {
            [ShiningCoreActionRequestState.RequestsProperty] = new JsonArray(new JsonObject
            {
                ["requestId"] = "core_req_forge_reshape",
                ["actionType"] = ShiningCoreActionRequestState.ActionTypeForgeRelicReshape,
                ["factionId"] = "faction_old",
                ["factionName"] = "Старый Дом",
                ["projectId"] = "",
                ["projectDisplayName"] = "",
                ["radianceTierAtRequest"] = 2,
                ["quotedCostFeathers"] = 5,
                ["quotedCostLightSparks"] = 5,
                ["sourceDraftVersion"] = 0,
                ["selectedCardIds"] = new JsonArray(),
                ["relicId"] = "relic_old",
                ["relicName"] = "Старый Клинок",
                ["targetFormTag"] = "lance",
                ["propertyIndex"] = -1,
                ["createdAtTurn"] = 16,
                ["createdAtUtc"] = "2026-04-16T12:49:00Z"
            })
        });
        await WritePendingTurnSnapshotManifestAsync(preTurnShiningRoot, preTurnResidentRoot, preTurnSoulRoot, new JsonObject
        {
            [ShiningCoreActionRequestState.RequestsProperty] = new JsonArray(new JsonObject
            {
                ["requestId"] = "core_req_forge_reshape",
                ["actionType"] = ShiningCoreActionRequestState.ActionTypeForgeRelicReshape,
                ["factionId"] = "faction_old",
                ["factionName"] = "Старый Дом",
                ["projectId"] = "",
                ["projectDisplayName"] = "",
                ["radianceTierAtRequest"] = 2,
                ["quotedCostFeathers"] = 5,
                ["quotedCostLightSparks"] = 5,
                ["sourceDraftVersion"] = 0,
                ["selectedCardIds"] = new JsonArray(),
                ["relicId"] = "relic_old",
                ["relicName"] = "Старый Клинок",
                ["targetFormTag"] = "lance",
                ["propertyIndex"] = -1,
                ["createdAtTurn"] = 16,
                ["createdAtUtc"] = "2026-04-16T12:49:00Z"
            })
        });

        var issues = await InvokeValidationAsync("ValidatePendingShiningCoreActionResolutionAsync");

        Assert.Contains(issues, issue => string.Equals(issue.Code, "shining_forge_action_soul_state_mismatch", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task ValidatePendingShiningCoreActionResolutionAsync_AcceptedForgeReshapeWithWrongBlessingConsumptionMarkers_Fails()
    {
        var preTurnShiningRoot = CreateBaseShiningRoot();
        var faction = preTurnShiningRoot["factions"]!.AsArray()[0]!.AsObject();
        faction["projects"]!.AsArray().Add(new JsonObject
        {
            ["projectId"] = "project_refinement",
            ["displayName"] = "Кузня Отголосков",
            ["summary"] = "Поддерживает refinement.",
            ["toneTags"] = new JsonArray("relic"),
            ["targetFactionIds"] = new JsonArray(),
            ["projectArchetype"] = ShiningAbodeState.ProjectArchetypeRefinement,
            ["outputEffectFamily"] = ShiningAbodeState.EffectFamilyRelic,
            ["tier"] = 2,
            ["status"] = ShiningAbodeState.ProjectStatusCompleted,
            ["isSupported"] = true,
            ["strengthReward"] = 12
        });
        var preTurnResidentRoot = CreateBaseResidentRoot();
        preTurnResidentRoot["entries"]!.AsArray().Add(new JsonObject
        {
            ["residentId"] = "resident_smith",
            ["displayName"] = "Кузнец",
            ["ascensionState"] = ShiningAbodeState.AscensionStateAscended,
            ["shiningFactionId"] = "faction_old",
            ["residentRole"] = ShiningAbodeState.ResidentRoleForgeSupport,
            ["factionLoyaltyLevel"] = 60,
            ["factionLoyaltyTier"] = ShiningAbodeState.ResolveFactionLoyaltyTier(60),
            ["factionRestlessness"] = 20,
            ["factionRealignmentState"] = ShiningAbodeState.ResolveFactionRealignmentState(60, 20)
        });
        var preTurnSoulRoot = CreateBaseSoulRoot();
        preTurnSoulRoot[ShiningBlessingEffectState.SoulStateProperty] = CreatePendingForgeBlessingState();

        var currentShiningRoot = CloneJsonObject(preTurnShiningRoot);
        var currentSoulRoot = CloneJsonObject(preTurnSoulRoot);
        Assert.True(ShiningAbodeState.TryApplyForgeAction(
            currentShiningRoot,
            currentSoulRoot,
            CloneJsonObject(preTurnResidentRoot),
            ShiningCoreActionRequestState.ActionTypeForgeRelicReshape,
            "faction_old",
            "relic_old",
            "lance",
            -1,
            null,
            null,
            currentTurnNumber: 16,
            resolvedAtUtc: "2026-04-16T12:50:00Z",
            out _,
            out _));
        var entitlements = currentSoulRoot[ShiningBlessingEffectState.SoulStateProperty]!["relicRefinementEntitlements"]!.AsObject();
        entitlements["consumedAtTurn"] = 0;
        entitlements["consumedAtUtc"] = "2026-04-16T12:10:00Z";
        ShiningAbodeState.EnsureCoreActionReceiptsArray(currentShiningRoot).Add(new JsonObject
        {
            ["requestId"] = "core_req_forge_reshape",
            ["actionType"] = ShiningCoreActionRequestState.ActionTypeForgeRelicReshape,
            ["status"] = ShiningCoreActionRequestState.RequestStatusAccepted,
            ["factionId"] = "faction_old",
            ["projectId"] = "",
            ["hallId"] = "",
            ["resolvedFactionId"] = "",
            ["relicId"] = "relic_old",
            ["relicName"] = "Старый Клинок",
            ["targetFormTag"] = "lance",
            ["selectedCardIds"] = new JsonArray(),
            ["newResidentIds"] = new JsonArray(),
            ["seededProjectIds"] = new JsonArray(),
            ["generatedDraftVersion"] = 0,
            ["resolvedAtTurn"] = 16,
            ["resolvedAtUtc"] = "2026-04-16T12:50:00Z",
            ["reason"] = "forge_reshape_accepted"
        });

        await SeedCurrentStateAsync(currentShiningRoot, preTurnResidentRoot, currentSoulRoot);
        await WriteNodeAsync(ShiningCoreActionRequestState.PendingActionsRequestPath, new JsonObject
        {
            [ShiningCoreActionRequestState.RequestsProperty] = new JsonArray(new JsonObject
            {
                ["requestId"] = "core_req_forge_reshape",
                ["actionType"] = ShiningCoreActionRequestState.ActionTypeForgeRelicReshape,
                ["factionId"] = "faction_old",
                ["factionName"] = "Старый Дом",
                ["projectId"] = "",
                ["projectDisplayName"] = "",
                ["radianceTierAtRequest"] = 2,
                ["quotedCostFeathers"] = 5,
                ["quotedCostLightSparks"] = 5,
                ["sourceDraftVersion"] = 0,
                ["selectedCardIds"] = new JsonArray(),
                ["relicId"] = "relic_old",
                ["relicName"] = "Старый Клинок",
                ["targetFormTag"] = "lance",
                ["propertyIndex"] = -1,
                ["createdAtTurn"] = 16,
                ["createdAtUtc"] = "2026-04-16T12:49:00Z"
            })
        });
        await WritePendingTurnSnapshotManifestAsync(preTurnShiningRoot, preTurnResidentRoot, preTurnSoulRoot, new JsonObject
        {
            [ShiningCoreActionRequestState.RequestsProperty] = new JsonArray(new JsonObject
            {
                ["requestId"] = "core_req_forge_reshape",
                ["actionType"] = ShiningCoreActionRequestState.ActionTypeForgeRelicReshape,
                ["factionId"] = "faction_old",
                ["factionName"] = "Старый Дом",
                ["projectId"] = "",
                ["projectDisplayName"] = "",
                ["radianceTierAtRequest"] = 2,
                ["quotedCostFeathers"] = 5,
                ["quotedCostLightSparks"] = 5,
                ["sourceDraftVersion"] = 0,
                ["selectedCardIds"] = new JsonArray(),
                ["relicId"] = "relic_old",
                ["relicName"] = "Старый Клинок",
                ["targetFormTag"] = "lance",
                ["propertyIndex"] = -1,
                ["createdAtTurn"] = 16,
                ["createdAtUtc"] = "2026-04-16T12:49:00Z"
            })
        });

        var issues = await InvokeValidationAsync("ValidatePendingShiningCoreActionResolutionAsync");

        Assert.Contains(issues, issue => string.Equals(issue.Code, "shining_forge_action_blessing_entitlement_mismatch", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task ValidatePendingShiningCoreActionResolutionAsync_AcceptedRelicGachaWithCanonicalAccounting_Passes()
    {
        var preTurnShiningRoot = CreateBaseShiningRoot();
        var preTurnResidentRoot = CreateBaseResidentRoot();
        var preTurnSoulRoot = CreateBaseSoulRoot();

        var currentShiningRoot = CloneJsonObject(preTurnShiningRoot);
        var currentSoulRoot = CloneJsonObject(preTurnSoulRoot);
        Assert.True(ShiningAbodeState.TryApplyRelicGachaAccounting(
            currentShiningRoot,
            currentSoulRoot,
            CloneJsonObject(preTurnResidentRoot),
            "faction_old",
            "core_req_shining_gacha",
            "relic_gacha_1",
            "Сияющий Осколок",
            "Uncommon",
            "Rare",
            17,
            "2026-04-16T13:00:00Z",
            out _,
            out _,
            out _));
        currentSoulRoot["soulRelics"]!["stored"]!.AsArray().Add(new JsonObject
        {
            ["relicId"] = "relic_gacha_1",
            ["name"] = "Сияющий Осколок",
            ["rarity"] = "Rare"
        });
        ShiningAbodeState.EnsureCoreActionReceiptsArray(currentShiningRoot).Add(new JsonObject
        {
            ["requestId"] = "core_req_shining_gacha",
            ["actionType"] = ShiningCoreActionRequestState.ActionTypePullRelicGacha,
            ["status"] = ShiningCoreActionRequestState.RequestStatusAccepted,
            ["factionId"] = "faction_old",
            ["projectId"] = "",
            ["hallId"] = "",
            ["resolvedFactionId"] = "",
            ["relicId"] = "relic_gacha_1",
            ["relicName"] = "Сияющий Осколок",
            ["returnCycleId"] = "shining_return_2",
            ["baseRarity"] = "Uncommon",
            ["finalRarity"] = "Rare",
            ["selectedCardIds"] = new JsonArray(),
            ["newResidentIds"] = new JsonArray(),
            ["seededProjectIds"] = new JsonArray(),
            ["generatedDraftVersion"] = 0,
            ["resolvedAtTurn"] = 17,
            ["resolvedAtUtc"] = "2026-04-16T13:00:00Z",
            ["reason"] = "shining_gacha_resolved"
        });

        await SeedCurrentStateAsync(currentShiningRoot, preTurnResidentRoot, currentSoulRoot);
        await WriteNodeAsync(ShiningCoreActionRequestState.PendingActionsRequestPath, new JsonObject
        {
            [ShiningCoreActionRequestState.RequestsProperty] = new JsonArray(new JsonObject
            {
                ["requestId"] = "core_req_shining_gacha",
                ["actionType"] = ShiningCoreActionRequestState.ActionTypePullRelicGacha,
                ["factionId"] = "faction_old",
                ["factionName"] = "Старый Дом",
                ["projectId"] = "",
                ["projectDisplayName"] = "",
                ["radianceTierAtRequest"] = 2,
                ["quotedCostFeathers"] = 30,
                ["quotedCostLightSparks"] = 0,
                ["sourceDraftVersion"] = 0,
                ["selectedCardIds"] = new JsonArray(),
                ["returnCycleId"] = "shining_return_2",
                ["projectedGachaBonusSteps"] = 1,
                ["createdAtTurn"] = 17,
                ["createdAtUtc"] = "2026-04-16T12:59:00Z"
            })
        });
        await WritePendingTurnSnapshotManifestAsync(preTurnShiningRoot, preTurnResidentRoot, preTurnSoulRoot, new JsonObject
        {
            [ShiningCoreActionRequestState.RequestsProperty] = new JsonArray(new JsonObject
            {
                ["requestId"] = "core_req_shining_gacha",
                ["actionType"] = ShiningCoreActionRequestState.ActionTypePullRelicGacha,
                ["factionId"] = "faction_old",
                ["factionName"] = "Старый Дом",
                ["projectId"] = "",
                ["projectDisplayName"] = "",
                ["radianceTierAtRequest"] = 2,
                ["quotedCostFeathers"] = 30,
                ["quotedCostLightSparks"] = 0,
                ["sourceDraftVersion"] = 0,
                ["selectedCardIds"] = new JsonArray(),
                ["returnCycleId"] = "shining_return_2",
                ["projectedGachaBonusSteps"] = 1,
                ["createdAtTurn"] = 17,
                ["createdAtUtc"] = "2026-04-16T12:59:00Z"
            })
        });
        await WriteNodeAsync("input/turn_request.json", new JsonObject
        {
            ["sessionId"] = "test-session",
            ["requestId"] = "test-request",
            ["turnNumber"] = 12,
            ["gachaBaseResult"] = new JsonObject
            {
                ["baseRarity"] = "Uncommon"
            }
        });

        var issues = await InvokeValidationAsync("ValidatePendingShiningCoreActionResolutionAsync");

        Assert.DoesNotContain(issues, issue => string.Equals(issue.Code, "shining_gacha_projection_failed", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(issues, issue => string.Equals(issue.Code, "shining_gacha_system_mismatch", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(issues, issue => string.Equals(issue.Code, "shining_gacha_missing_new_relic_materialization", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task ValidatePendingShiningCoreActionResolutionAsync_AcceptedRelicGachaWithExistingRelicMutation_Fails()
    {
        var preTurnShiningRoot = CreateBaseShiningRoot();
        var preTurnResidentRoot = CreateBaseResidentRoot();
        var preTurnSoulRoot = CreateBaseSoulRoot();

        var currentShiningRoot = CloneJsonObject(preTurnShiningRoot);
        var currentSoulRoot = CloneJsonObject(preTurnSoulRoot);
        Assert.True(ShiningAbodeState.TryApplyRelicGachaAccounting(
            currentShiningRoot,
            currentSoulRoot,
            CloneJsonObject(preTurnResidentRoot),
            "faction_old",
            "core_req_shining_gacha_mutated_old",
            "relic_gacha_mutated_old",
            "Сияющий Осколок",
            "Uncommon",
            "Rare",
            17,
            "2026-04-16T13:00:00Z",
            out _,
            out _,
            out _));
        currentSoulRoot["soulRelics"]!["stored"]!.AsArray()[0]!.AsObject()["name"] = "Подменённый старый клинок";
        currentSoulRoot["soulRelics"]!["stored"]!.AsArray().Add(new JsonObject
        {
            ["relicId"] = "relic_gacha_mutated_old",
            ["name"] = "Сияющий Осколок",
            ["rarity"] = "Rare"
        });
        ShiningAbodeState.EnsureCoreActionReceiptsArray(currentShiningRoot).Add(new JsonObject
        {
            ["requestId"] = "core_req_shining_gacha_mutated_old",
            ["actionType"] = ShiningCoreActionRequestState.ActionTypePullRelicGacha,
            ["status"] = ShiningCoreActionRequestState.RequestStatusAccepted,
            ["factionId"] = "faction_old",
            ["projectId"] = "",
            ["hallId"] = "",
            ["resolvedFactionId"] = "",
            ["relicId"] = "relic_gacha_mutated_old",
            ["relicName"] = "Сияющий Осколок",
            ["returnCycleId"] = "shining_return_2",
            ["baseRarity"] = "Uncommon",
            ["finalRarity"] = "Rare",
            ["selectedCardIds"] = new JsonArray(),
            ["newResidentIds"] = new JsonArray(),
            ["seededProjectIds"] = new JsonArray(),
            ["generatedDraftVersion"] = 0,
            ["resolvedAtTurn"] = 17,
            ["resolvedAtUtc"] = "2026-04-16T13:00:00Z",
            ["reason"] = "shining_gacha_resolved"
        });

        await SeedCurrentStateAsync(currentShiningRoot, preTurnResidentRoot, currentSoulRoot);
        var requestRoot = CreateShiningGachaRequestRoot("core_req_shining_gacha_mutated_old", "shining_return_2");
        await WriteNodeAsync(ShiningCoreActionRequestState.PendingActionsRequestPath, requestRoot);
        await WritePendingTurnSnapshotManifestAsync(preTurnShiningRoot, preTurnResidentRoot, preTurnSoulRoot, requestRoot);
        await WriteNodeAsync("input/turn_request.json", CreateTurnRequestWithBaseRarity("Uncommon"));

        var issues = await InvokeValidationAsync("ValidatePendingShiningCoreActionResolutionAsync");

        Assert.Contains(issues, issue => string.Equals(issue.Code, "shining_gacha_soul_state_diff_mismatch", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task ValidatePendingShiningCoreActionResolutionAsync_AcceptedRelicGachaWithUnrelatedSoulMutation_Fails()
    {
        var preTurnShiningRoot = CreateBaseShiningRoot();
        var preTurnResidentRoot = CreateBaseResidentRoot();
        var preTurnSoulRoot = CreateBaseSoulRoot();

        var currentShiningRoot = CloneJsonObject(preTurnShiningRoot);
        var currentSoulRoot = CloneJsonObject(preTurnSoulRoot);
        Assert.True(ShiningAbodeState.TryApplyRelicGachaAccounting(
            currentShiningRoot,
            currentSoulRoot,
            CloneJsonObject(preTurnResidentRoot),
            "faction_old",
            "core_req_shining_gacha_unrelated_soul",
            "relic_gacha_unrelated_soul",
            "Сияющий Осколок",
            "Uncommon",
            "Rare",
            17,
            "2026-04-16T13:00:00Z",
            out _,
            out _,
            out _));
        currentSoulRoot["soulName"] = "Недопустимо изменённое имя";
        currentSoulRoot["soulRelics"]!["stored"]!.AsArray().Add(new JsonObject
        {
            ["relicId"] = "relic_gacha_unrelated_soul",
            ["name"] = "Сияющий Осколок",
            ["rarity"] = "Rare"
        });
        ShiningAbodeState.EnsureCoreActionReceiptsArray(currentShiningRoot).Add(new JsonObject
        {
            ["requestId"] = "core_req_shining_gacha_unrelated_soul",
            ["actionType"] = ShiningCoreActionRequestState.ActionTypePullRelicGacha,
            ["status"] = ShiningCoreActionRequestState.RequestStatusAccepted,
            ["factionId"] = "faction_old",
            ["projectId"] = "",
            ["hallId"] = "",
            ["resolvedFactionId"] = "",
            ["relicId"] = "relic_gacha_unrelated_soul",
            ["relicName"] = "Сияющий Осколок",
            ["returnCycleId"] = "shining_return_2",
            ["baseRarity"] = "Uncommon",
            ["finalRarity"] = "Rare",
            ["selectedCardIds"] = new JsonArray(),
            ["newResidentIds"] = new JsonArray(),
            ["seededProjectIds"] = new JsonArray(),
            ["generatedDraftVersion"] = 0,
            ["resolvedAtTurn"] = 17,
            ["resolvedAtUtc"] = "2026-04-16T13:00:00Z",
            ["reason"] = "shining_gacha_resolved"
        });

        await SeedCurrentStateAsync(currentShiningRoot, preTurnResidentRoot, currentSoulRoot);
        var requestRoot = CreateShiningGachaRequestRoot("core_req_shining_gacha_unrelated_soul", "shining_return_2");
        await WriteNodeAsync(ShiningCoreActionRequestState.PendingActionsRequestPath, requestRoot);
        await WritePendingTurnSnapshotManifestAsync(preTurnShiningRoot, preTurnResidentRoot, preTurnSoulRoot, requestRoot);
        await WriteNodeAsync("input/turn_request.json", CreateTurnRequestWithBaseRarity("Uncommon"));

        var issues = await InvokeValidationAsync("ValidatePendingShiningCoreActionResolutionAsync");

        Assert.Contains(issues, issue => string.Equals(issue.Code, "shining_gacha_soul_state_diff_mismatch", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task ValidatePendingShiningCoreActionResolutionAsync_AcceptedInvestWithoutLightSparksDebit_Fails()
    {
        var preTurnShiningRoot = CreateBaseShiningRoot();
        var preTurnResidentRoot = CreateBaseResidentRoot();
        var preTurnSoulRoot = CreateBaseSoulRoot();

        var currentShiningRoot = CloneJsonObject(preTurnShiningRoot);
        Assert.True(ShiningAbodeState.TryInvestInFaction(currentShiningRoot, CloneJsonObject(preTurnResidentRoot), "faction_old", out _));
        currentShiningRoot["lightSparks"] = GetNodeInt(preTurnShiningRoot["lightSparks"]);
        ShiningAbodeState.EnsureCoreActionReceiptsArray(currentShiningRoot).Add(new JsonObject
        {
            ["requestId"] = "core_req_invest_1",
            ["actionType"] = ShiningCoreActionRequestState.ActionTypeInvestInFaction,
            ["status"] = ShiningCoreActionRequestState.RequestStatusAccepted,
            ["factionId"] = "faction_old",
            ["projectId"] = "",
            ["hallId"] = "",
            ["resolvedFactionId"] = "",
            ["selectedCardIds"] = new JsonArray(),
            ["newResidentIds"] = new JsonArray(),
            ["seededProjectIds"] = new JsonArray(),
            ["resolvedAtTurn"] = 18,
            ["resolvedAtUtc"] = "2026-04-16T13:05:00Z",
            ["reason"] = "invested"
        });

        await SeedCurrentStateAsync(currentShiningRoot, preTurnResidentRoot, preTurnSoulRoot);
        var requestRoot = new JsonObject
        {
            [ShiningCoreActionRequestState.RequestsProperty] = new JsonArray(new JsonObject
            {
                ["requestId"] = "core_req_invest_1",
                ["actionType"] = ShiningCoreActionRequestState.ActionTypeInvestInFaction,
                ["factionId"] = "faction_old",
                ["factionName"] = "Старый Дом",
                ["projectId"] = "",
                ["projectDisplayName"] = "",
                ["radianceTierAtRequest"] = 2,
                ["quotedCostFeathers"] = 0,
                ["quotedCostLightSparks"] = ShiningAbodeState.GetFactionInvestmentCost().LightSparks,
                ["sourceDraftVersion"] = 0,
                ["selectedCardIds"] = new JsonArray(),
                ["createdAtTurn"] = 18,
                ["createdAtUtc"] = "2026-04-16T13:04:00Z"
            })
        };
        await WriteNodeAsync(ShiningCoreActionRequestState.PendingActionsRequestPath, requestRoot);
        await WritePendingTurnSnapshotManifestAsync(preTurnShiningRoot, preTurnResidentRoot, preTurnSoulRoot, requestRoot);

        var issues = await InvokeValidationAsync("ValidatePendingShiningCoreActionResolutionAsync");

        Assert.Contains(issues, issue => string.Equals(issue.Code, "shining_core_action_light_sparks_cost_mismatch", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task ValidatePendingShiningCoreActionResolutionAsync_AcceptedInvestWithUnrelatedShiningMutation_Fails()
    {
        var preTurnShiningRoot = CreateBaseShiningRoot();
        var preTurnResidentRoot = CreateBaseResidentRoot();
        var preTurnSoulRoot = CreateBaseSoulRoot();

        var currentShiningRoot = CloneJsonObject(preTurnShiningRoot);
        Assert.True(ShiningAbodeState.TryInvestInFaction(currentShiningRoot, CloneJsonObject(preTurnResidentRoot), "faction_old", out _));
        currentShiningRoot["halls"]!.AsArray()[0]!.AsObject()["hallName"] = "Недопустимо изменённый зал";
        var currentSoulRoot = CloneJsonObject(preTurnSoulRoot);
        ShiningAbodeState.EnsureCoreActionReceiptsArray(currentShiningRoot).Add(new JsonObject
        {
            ["requestId"] = "core_req_invest_unrelated_shining",
            ["actionType"] = ShiningCoreActionRequestState.ActionTypeInvestInFaction,
            ["status"] = ShiningCoreActionRequestState.RequestStatusAccepted,
            ["factionId"] = "faction_old",
            ["projectId"] = "",
            ["hallId"] = "",
            ["resolvedFactionId"] = "",
            ["selectedCardIds"] = new JsonArray(),
            ["newResidentIds"] = new JsonArray(),
            ["seededProjectIds"] = new JsonArray(),
            ["resolvedAtTurn"] = 18,
            ["resolvedAtUtc"] = "2026-04-16T13:05:00Z",
            ["reason"] = "invested"
        });

        await SeedCurrentStateAsync(currentShiningRoot, preTurnResidentRoot, currentSoulRoot);
        var requestRoot = new JsonObject
        {
            [ShiningCoreActionRequestState.RequestsProperty] = new JsonArray(new JsonObject
            {
                ["requestId"] = "core_req_invest_unrelated_shining",
                ["actionType"] = ShiningCoreActionRequestState.ActionTypeInvestInFaction,
                ["factionId"] = "faction_old",
                ["factionName"] = "Старый Дом",
                ["projectId"] = "",
                ["projectDisplayName"] = "",
                ["radianceTierAtRequest"] = 2,
                ["quotedCostFeathers"] = 0,
                ["quotedCostLightSparks"] = ShiningAbodeState.GetFactionInvestmentCost().LightSparks,
                ["sourceDraftVersion"] = 0,
                ["selectedCardIds"] = new JsonArray(),
                ["createdAtTurn"] = 18,
                ["createdAtUtc"] = "2026-04-16T13:04:00Z"
            })
        };
        await WriteNodeAsync(ShiningCoreActionRequestState.PendingActionsRequestPath, requestRoot);
        await WritePendingTurnSnapshotManifestAsync(preTurnShiningRoot, preTurnResidentRoot, preTurnSoulRoot, requestRoot);

        var issues = await InvokeValidationAsync("ValidatePendingShiningCoreActionResolutionAsync");

        Assert.Contains(issues, issue => string.Equals(issue.Code, "shining_core_action_projected_state_mismatch", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task ValidatePendingShiningCoreActionResolutionAsync_AcceptedInvestWithUnrelatedResidentMutation_Fails()
    {
        var preTurnShiningRoot = CreateBaseShiningRoot();
        var preTurnResidentRoot = CreateBaseResidentRoot();
        var preTurnSoulRoot = CreateBaseSoulRoot();

        var currentShiningRoot = CloneJsonObject(preTurnShiningRoot);
        Assert.True(ShiningAbodeState.TryInvestInFaction(currentShiningRoot, CloneJsonObject(preTurnResidentRoot), "faction_old", out _));
        var currentResidentRoot = CloneJsonObject(preTurnResidentRoot);
        currentResidentRoot["entries"]!.AsArray()[0]!.AsObject()["displayName"] = "Недопустимо изменённая Лиора";
        var currentSoulRoot = CloneJsonObject(preTurnSoulRoot);
        ShiningAbodeState.EnsureCoreActionReceiptsArray(currentShiningRoot).Add(new JsonObject
        {
            ["requestId"] = "core_req_invest_unrelated_resident",
            ["actionType"] = ShiningCoreActionRequestState.ActionTypeInvestInFaction,
            ["status"] = ShiningCoreActionRequestState.RequestStatusAccepted,
            ["factionId"] = "faction_old",
            ["projectId"] = "",
            ["hallId"] = "",
            ["resolvedFactionId"] = "",
            ["selectedCardIds"] = new JsonArray(),
            ["newResidentIds"] = new JsonArray(),
            ["seededProjectIds"] = new JsonArray(),
            ["resolvedAtTurn"] = 18,
            ["resolvedAtUtc"] = "2026-04-16T13:05:00Z",
            ["reason"] = "invested"
        });

        await SeedCurrentStateAsync(currentShiningRoot, currentResidentRoot, currentSoulRoot);
        var requestRoot = new JsonObject
        {
            [ShiningCoreActionRequestState.RequestsProperty] = new JsonArray(new JsonObject
            {
                ["requestId"] = "core_req_invest_unrelated_resident",
                ["actionType"] = ShiningCoreActionRequestState.ActionTypeInvestInFaction,
                ["factionId"] = "faction_old",
                ["factionName"] = "Старый Дом",
                ["projectId"] = "",
                ["projectDisplayName"] = "",
                ["radianceTierAtRequest"] = 2,
                ["quotedCostFeathers"] = 0,
                ["quotedCostLightSparks"] = ShiningAbodeState.GetFactionInvestmentCost().LightSparks,
                ["sourceDraftVersion"] = 0,
                ["selectedCardIds"] = new JsonArray(),
                ["createdAtTurn"] = 18,
                ["createdAtUtc"] = "2026-04-16T13:04:00Z"
            })
        };
        await WriteNodeAsync(ShiningCoreActionRequestState.PendingActionsRequestPath, requestRoot);
        await WritePendingTurnSnapshotManifestAsync(preTurnShiningRoot, preTurnResidentRoot, preTurnSoulRoot, requestRoot);

        var issues = await InvokeValidationAsync("ValidatePendingShiningCoreActionResolutionAsync");

        Assert.Contains(issues, issue => string.Equals(issue.Code, "shining_core_action_unexpected_resident_state_change", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task ValidatePendingShiningCoreActionResolutionAsync_NonAcceptedInvestWithLightSparksMutation_Fails()
    {
        var preTurnShiningRoot = CreateBaseShiningRoot();
        var preTurnResidentRoot = CreateBaseResidentRoot();
        var preTurnSoulRoot = CreateBaseSoulRoot();

        var currentShiningRoot = CloneJsonObject(preTurnShiningRoot);
        currentShiningRoot["lightSparks"] = GetNodeInt(preTurnShiningRoot["lightSparks"]) - ShiningAbodeState.GetFactionInvestmentCost().LightSparks;
        ShiningAbodeState.EnsureCoreActionReceiptsArray(currentShiningRoot).Add(new JsonObject
        {
            ["requestId"] = "core_req_invest_refused",
            ["actionType"] = ShiningCoreActionRequestState.ActionTypeInvestInFaction,
            ["status"] = ShiningCoreActionRequestState.RequestStatusRefused,
            ["factionId"] = "faction_old",
            ["projectId"] = "",
            ["hallId"] = "",
            ["resolvedFactionId"] = "",
            ["selectedCardIds"] = new JsonArray(),
            ["newResidentIds"] = new JsonArray(),
            ["seededProjectIds"] = new JsonArray(),
            ["resolvedAtTurn"] = 18,
            ["resolvedAtUtc"] = "2026-04-16T13:05:00Z",
            ["reason"] = "refused"
        });

        await SeedCurrentStateAsync(currentShiningRoot, preTurnResidentRoot, preTurnSoulRoot);
        var requestRoot = new JsonObject
        {
            [ShiningCoreActionRequestState.RequestsProperty] = new JsonArray(new JsonObject
            {
                ["requestId"] = "core_req_invest_refused",
                ["actionType"] = ShiningCoreActionRequestState.ActionTypeInvestInFaction,
                ["factionId"] = "faction_old",
                ["factionName"] = "Старый Дом",
                ["projectId"] = "",
                ["projectDisplayName"] = "",
                ["radianceTierAtRequest"] = 2,
                ["quotedCostFeathers"] = 0,
                ["quotedCostLightSparks"] = ShiningAbodeState.GetFactionInvestmentCost().LightSparks,
                ["sourceDraftVersion"] = 0,
                ["selectedCardIds"] = new JsonArray(),
                ["createdAtTurn"] = 18,
                ["createdAtUtc"] = "2026-04-16T13:04:00Z"
            })
        };
        await WriteNodeAsync(ShiningCoreActionRequestState.PendingActionsRequestPath, requestRoot);
        await WritePendingTurnSnapshotManifestAsync(preTurnShiningRoot, preTurnResidentRoot, preTurnSoulRoot, requestRoot);

        var issues = await InvokeValidationAsync("ValidatePendingShiningCoreActionResolutionAsync");

        Assert.Contains(issues, issue => string.Equals(issue.Code, "shining_core_action_unexpected_state_change_after_non_accept", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task ValidatePendingShiningCoreActionResolutionAsync_NonAcceptedInvestWithUnrelatedResidentMutation_Fails()
    {
        var preTurnShiningRoot = CreateBaseShiningRoot();
        var preTurnResidentRoot = CreateBaseResidentRoot();
        var preTurnSoulRoot = CreateBaseSoulRoot();

        var currentShiningRoot = CloneJsonObject(preTurnShiningRoot);
        var currentResidentRoot = CloneJsonObject(preTurnResidentRoot);
        currentResidentRoot["entries"]!.AsArray()[0]!.AsObject()["displayName"] = "Недопустимо изменённая Лиора";
        ShiningAbodeState.EnsureCoreActionReceiptsArray(currentShiningRoot).Add(new JsonObject
        {
            ["requestId"] = "core_req_invest_refused_resident",
            ["actionType"] = ShiningCoreActionRequestState.ActionTypeInvestInFaction,
            ["status"] = ShiningCoreActionRequestState.RequestStatusRefused,
            ["factionId"] = "faction_old",
            ["projectId"] = "",
            ["hallId"] = "",
            ["resolvedFactionId"] = "",
            ["selectedCardIds"] = new JsonArray(),
            ["newResidentIds"] = new JsonArray(),
            ["seededProjectIds"] = new JsonArray(),
            ["resolvedAtTurn"] = 18,
            ["resolvedAtUtc"] = "2026-04-16T13:05:00Z",
            ["reason"] = "refused"
        });

        await SeedCurrentStateAsync(currentShiningRoot, currentResidentRoot, preTurnSoulRoot);
        var requestRoot = new JsonObject
        {
            [ShiningCoreActionRequestState.RequestsProperty] = new JsonArray(new JsonObject
            {
                ["requestId"] = "core_req_invest_refused_resident",
                ["actionType"] = ShiningCoreActionRequestState.ActionTypeInvestInFaction,
                ["factionId"] = "faction_old",
                ["factionName"] = "Старый Дом",
                ["projectId"] = "",
                ["projectDisplayName"] = "",
                ["radianceTierAtRequest"] = 2,
                ["quotedCostFeathers"] = 0,
                ["quotedCostLightSparks"] = ShiningAbodeState.GetFactionInvestmentCost().LightSparks,
                ["sourceDraftVersion"] = 0,
                ["selectedCardIds"] = new JsonArray(),
                ["createdAtTurn"] = 18,
                ["createdAtUtc"] = "2026-04-16T13:04:00Z"
            })
        };
        await WriteNodeAsync(ShiningCoreActionRequestState.PendingActionsRequestPath, requestRoot);
        await WritePendingTurnSnapshotManifestAsync(preTurnShiningRoot, preTurnResidentRoot, preTurnSoulRoot, requestRoot);

        var issues = await InvokeValidationAsync("ValidatePendingShiningCoreActionResolutionAsync");

        Assert.Contains(issues, issue => string.Equals(issue.Code, "shining_core_action_unexpected_state_change_after_non_accept", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task ValidatePendingShiningCoreActionResolutionAsync_NonAcceptedForgeWithEntitlementMutation_Fails()
    {
        var preTurnShiningRoot = CreateBaseShiningRoot();
        preTurnShiningRoot["factions"]!.AsArray()[0]!.AsObject()["projects"]!.AsArray().Add(new JsonObject
        {
            ["projectId"] = "project_refinement",
            ["displayName"] = "Кузня Отзвука",
            ["summary"] = "Поддерживает перековку реликвий.",
            ["toneTags"] = new JsonArray("forge"),
            ["targetFactionIds"] = new JsonArray(),
            ["projectArchetype"] = ShiningAbodeState.ProjectArchetypeRefinement,
            ["outputEffectFamily"] = ShiningAbodeState.EffectFamilyRelic,
            ["tier"] = 2,
            ["status"] = ShiningAbodeState.ProjectStatusCompleted,
            ["isSupported"] = true,
            ["strengthReward"] = 12
        });
        var preTurnResidentRoot = CreateBaseResidentRoot();
        preTurnResidentRoot["entries"]!.AsArray().Add(new JsonObject
        {
            ["residentId"] = "resident_smith",
            ["displayName"] = "Кузнец",
            ["ascensionState"] = ShiningAbodeState.AscensionStateAscended,
            ["shiningFactionId"] = "faction_old",
            ["residentRole"] = ShiningAbodeState.ResidentRoleForgeSupport,
            ["factionLoyaltyLevel"] = 60,
            ["factionLoyaltyTier"] = ShiningAbodeState.ResolveFactionLoyaltyTier(60),
            ["factionRestlessness"] = 20,
            ["factionRealignmentState"] = ShiningAbodeState.ResolveFactionRealignmentState(60, 20)
        });
        var preTurnSoulRoot = CreateBaseSoulRoot();
        preTurnSoulRoot[ShiningBlessingEffectState.SoulStateProperty] = CreatePendingForgeBlessingState();

        var currentShiningRoot = CloneJsonObject(preTurnShiningRoot);
        var currentSoulRoot = CloneJsonObject(preTurnSoulRoot);
        Assert.True(ShiningBlessingEffectState.ConsumeForgeEntitlements(
            currentSoulRoot,
            ShiningCoreActionRequestState.ActionTypeForgeRelicReshape,
            19,
            "2026-04-16T13:10:00Z"));
        ShiningAbodeState.EnsureCoreActionReceiptsArray(currentShiningRoot).Add(new JsonObject
        {
            ["requestId"] = "core_req_forge_refused",
            ["actionType"] = ShiningCoreActionRequestState.ActionTypeForgeRelicReshape,
            ["status"] = ShiningCoreActionRequestState.RequestStatusRefused,
            ["factionId"] = "faction_old",
            ["projectId"] = "",
            ["hallId"] = "",
            ["resolvedFactionId"] = "",
            ["relicId"] = "relic_old",
            ["relicName"] = "Старый Клинок",
            ["targetFormTag"] = "lance",
            ["selectedCardIds"] = new JsonArray(),
            ["newResidentIds"] = new JsonArray(),
            ["seededProjectIds"] = new JsonArray(),
            ["generatedDraftVersion"] = 0,
            ["resolvedAtTurn"] = 19,
            ["resolvedAtUtc"] = "2026-04-16T13:10:00Z",
            ["reason"] = "refused"
        });

        await SeedCurrentStateAsync(currentShiningRoot, preTurnResidentRoot, currentSoulRoot);
        var requestRoot = new JsonObject
        {
            [ShiningCoreActionRequestState.RequestsProperty] = new JsonArray(new JsonObject
            {
                ["requestId"] = "core_req_forge_refused",
                ["actionType"] = ShiningCoreActionRequestState.ActionTypeForgeRelicReshape,
                ["factionId"] = "faction_old",
                ["factionName"] = "Старый Дом",
                ["projectId"] = "",
                ["projectDisplayName"] = "",
                ["radianceTierAtRequest"] = 2,
                ["quotedCostFeathers"] = 5,
                ["quotedCostLightSparks"] = 5,
                ["sourceDraftVersion"] = 0,
                ["selectedCardIds"] = new JsonArray(),
                ["relicId"] = "relic_old",
                ["relicName"] = "Старый Клинок",
                ["targetFormTag"] = "lance",
                ["propertyIndex"] = -1,
                ["createdAtTurn"] = 19,
                ["createdAtUtc"] = "2026-04-16T13:09:00Z"
            })
        };
        await WriteNodeAsync(ShiningCoreActionRequestState.PendingActionsRequestPath, requestRoot);
        await WritePendingTurnSnapshotManifestAsync(preTurnShiningRoot, preTurnResidentRoot, preTurnSoulRoot, requestRoot);

        var issues = await InvokeValidationAsync("ValidatePendingShiningCoreActionResolutionAsync");

        Assert.Contains(issues, issue => string.Equals(issue.Code, "shining_core_action_unexpected_state_change_after_non_accept", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task ValidatePendingShiningCoreActionResolutionAsync_AcceptedCompleteProjectWithStableReceiptIdentity_Passes()
    {
        var preTurnShiningRoot = CreateBaseShiningRoot();
        var preTurnResidentRoot = CreateBaseResidentRoot();
        var preTurnSoulRoot = CreateBaseSoulRoot();
        var projectDraft = new JsonObject
        {
            ["displayName"] = "Хор Согласия",
            ["summary"] = "Укрепляет общий ритм Обители.",
            ["toneTags"] = new JsonArray("radiant", "choral"),
            ["targetFactionIds"] = new JsonArray(),
            ["projectArchetype"] = ShiningAbodeState.ProjectArchetypeAccord,
            ["outputEffectFamily"] = ShiningAbodeState.EffectFamilySocial,
            ["tier"] = 2
        };
        Assert.True(ShiningAbodeState.TryQuoteProjectCompletion(
            CloneJsonObject(preTurnShiningRoot),
            CloneJsonObject(preTurnResidentRoot),
            "faction_old",
            projectDraft,
            out var quotedCost,
            out var quoteError),
            quoteError);

        var currentShiningRoot = CloneJsonObject(preTurnShiningRoot);
        Assert.True(ShiningAbodeState.TryCompleteProject(
            currentShiningRoot,
            CloneJsonObject(preTurnResidentRoot),
            "faction_old",
            projectDraft,
            18,
            "project_completed_1",
            "2026-04-16T13:10:00Z",
            out _,
            out var completionError),
            completionError);

        var currentSoulRoot = CloneJsonObject(preTurnSoulRoot);
        currentSoulRoot["inkFeathers"]!["current"] = GetNodeInt(preTurnSoulRoot["inkFeathers"]?["current"]) - quotedCost.Feathers;
        ShiningAbodeState.EnsureCoreActionReceiptsArray(currentShiningRoot).Add(new JsonObject
        {
            ["requestId"] = "core_req_complete_1",
            ["actionType"] = ShiningCoreActionRequestState.ActionTypeCompleteProject,
            ["status"] = ShiningCoreActionRequestState.RequestStatusAccepted,
            ["factionId"] = "faction_old",
            ["factionName"] = "Старый Дом",
            ["projectId"] = "project_completed_1",
            ["projectName"] = "Хор Согласия",
            ["hallId"] = "",
            ["resolvedFactionId"] = "",
            ["selectedCardIds"] = new JsonArray(),
            ["newResidentIds"] = new JsonArray(),
            ["seededProjectIds"] = new JsonArray(),
            ["resolvedAtTurn"] = 18,
            ["resolvedAtUtc"] = "2026-04-16T13:10:00Z",
            ["reason"] = "project_completed"
        });

        await SeedCurrentStateAsync(currentShiningRoot, preTurnResidentRoot, currentSoulRoot);
        var requestRoot = new JsonObject
        {
            [ShiningCoreActionRequestState.RequestsProperty] = new JsonArray(new JsonObject
            {
                ["requestId"] = "core_req_complete_1",
                ["actionType"] = ShiningCoreActionRequestState.ActionTypeCompleteProject,
                ["factionId"] = "faction_old",
                ["factionName"] = "Старый Дом",
                ["projectId"] = "",
                ["projectDisplayName"] = "",
                ["projectDraft"] = projectDraft.DeepClone(),
                ["radianceTierAtRequest"] = 2,
                ["quotedCostFeathers"] = quotedCost.Feathers,
                ["quotedCostLightSparks"] = quotedCost.LightSparks,
                ["sourceDraftVersion"] = 0,
                ["selectedCardIds"] = new JsonArray(),
                ["createdAtTurn"] = 18,
                ["createdAtUtc"] = "2026-04-16T13:09:00Z"
            })
        };
        await WriteNodeAsync(ShiningCoreActionRequestState.PendingActionsRequestPath, requestRoot);
        await WritePendingTurnSnapshotManifestAsync(preTurnShiningRoot, preTurnResidentRoot, preTurnSoulRoot, requestRoot);

        var issues = await InvokeValidationAsync("ValidatePendingShiningCoreActionResolutionAsync");

        Assert.DoesNotContain(issues, issue => string.Equals(issue.Code, "shining_core_action_projected_state_mismatch", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(issues, issue => string.Equals(issue.Code, "shining_core_action_light_sparks_cost_mismatch", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(issues, issue => string.Equals(issue.Code, "shining_core_action_feather_cost_mismatch", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task ValidatePendingShiningCoreActionResolutionAsync_StubReceiptWithoutResolvedMarkers_FailsMissingResolution()
    {
        var preTurnShiningRoot = CreateBaseShiningRoot();
        var preTurnResidentRoot = CreateBaseResidentRoot();
        var preTurnSoulRoot = CreateBaseSoulRoot();

        var currentShiningRoot = CloneJsonObject(preTurnShiningRoot);
        ShiningAbodeState.EnsureCoreActionReceiptsArray(currentShiningRoot).Add(new JsonObject
        {
            ["requestId"] = "core_req_open_gates_stub",
            ["actionType"] = ShiningCoreActionRequestState.ActionTypeOpenGates,
            ["status"] = ShiningCoreActionRequestState.RequestStatusAccepted,
            ["factionId"] = "",
            ["projectId"] = "",
            ["hallId"] = "",
            ["resolvedFactionId"] = "",
            ["selectedCardIds"] = new JsonArray(),
            ["newResidentIds"] = new JsonArray(),
            ["seededProjectIds"] = new JsonArray(),
            ["generatedDraftVersion"] = 2,
            ["resolvedAtTurn"] = 0,
            ["resolvedAtUtc"] = ""
        });

        await SeedCurrentStateAsync(currentShiningRoot, preTurnResidentRoot, preTurnSoulRoot);
        var requestRoot = new JsonObject
        {
            [ShiningCoreActionRequestState.RequestsProperty] = new JsonArray(new JsonObject
            {
                ["requestId"] = "core_req_open_gates_stub",
                ["actionType"] = ShiningCoreActionRequestState.ActionTypeOpenGates,
                ["factionId"] = "",
                ["factionName"] = "",
                ["projectId"] = "",
                ["projectDisplayName"] = "",
                ["radianceTierAtRequest"] = 0,
                ["quotedCostFeathers"] = 0,
                ["quotedCostLightSparks"] = 0,
                ["sourceDraftVersion"] = 0,
                ["selectedCardIds"] = new JsonArray(),
                ["createdAtTurn"] = 14,
                ["createdAtUtc"] = "2026-04-16T12:25:00Z"
            })
        };
        await WriteNodeAsync(ShiningCoreActionRequestState.PendingActionsRequestPath, requestRoot);
        await WritePendingTurnSnapshotManifestAsync(preTurnShiningRoot, preTurnResidentRoot, preTurnSoulRoot, requestRoot);

        var issues = await InvokeValidationAsync("ValidatePendingShiningCoreActionResolutionAsync");

        Assert.Contains(issues, issue => string.Equals(issue.Code, "shining_core_action_receipt_mismatch", StringComparison.OrdinalIgnoreCase));
    }

    private async Task SeedCurrentStateAsync(JsonObject shiningRoot, JsonObject residentRoot, JsonObject soulRoot)
    {
        await WriteNodeAsync("game_state/meta/soul_state.json", soulRoot);
        await WriteNodeAsync("game_state/meta/guardians.json", new JsonObject
        {
            ["guardians"] = new JsonArray
            {
                new JsonObject
                {
                    ["guardianId"] = "guardian_old",
                    ["guardianName"] = "Азалия"
                }
            }
        });
        await WriteNodeAsync(ShiningAbodeState.StatePath, shiningRoot);
        await WriteNodeAsync(GuardianAbodeResidentState.StatePath, residentRoot);
        await WriteNodeAsync("ready/turn_complete.json", new JsonObject
        {
            ["accepted"] = true
        });
    }

    private static JsonObject CreateShiningGachaRequestRoot(string requestId, string returnCycleId) => new()
    {
        [ShiningCoreActionRequestState.RequestsProperty] = new JsonArray(new JsonObject
        {
            ["requestId"] = requestId,
            ["actionType"] = ShiningCoreActionRequestState.ActionTypePullRelicGacha,
            ["factionId"] = "faction_old",
            ["factionName"] = "Старый Дом",
            ["projectId"] = "",
            ["projectDisplayName"] = "",
            ["radianceTierAtRequest"] = 2,
            ["quotedCostFeathers"] = 30,
            ["quotedCostLightSparks"] = 0,
            ["sourceDraftVersion"] = 0,
            ["selectedCardIds"] = new JsonArray(),
            ["returnCycleId"] = returnCycleId,
            ["projectedGachaBonusSteps"] = 1,
            ["createdAtTurn"] = 17,
            ["createdAtUtc"] = "2026-04-16T12:59:00Z"
        })
    };

    private static JsonObject CreateTurnRequestWithBaseRarity(string baseRarity) => new()
    {
        ["sessionId"] = "test-session",
        ["requestId"] = "test-request",
        ["turnNumber"] = 12,
        ["gachaBaseResult"] = new JsonObject
        {
            ["baseRarity"] = baseRarity
        }
    };

    private async Task WritePendingTurnSnapshotManifestAsync(JsonObject preTurnShiningRoot, JsonObject preTurnResidentRoot, JsonObject preTurnSoulRoot, JsonObject requestRoot)
    {
        const string requestSnapshotPath = "game_state/control/pending_turn_snapshot/pre_shining_core_action_request.json";
        const string shiningSnapshotPath = "game_state/control/pending_turn_snapshot/game_state/meta/shining_abode_state.json";
        const string residentSnapshotPath = "game_state/control/pending_turn_snapshot/game_state/meta/guardian_abode_residents.json";
        const string soulSnapshotPath = "game_state/control/pending_turn_snapshot/game_state/meta/soul_state.json";

        await WriteNodeAsync(requestSnapshotPath, requestRoot);
        await WriteNodeAsync(shiningSnapshotPath, preTurnShiningRoot);
        await WriteNodeAsync(residentSnapshotPath, preTurnResidentRoot);
        await WriteNodeAsync(soulSnapshotPath, preTurnSoulRoot);
        await WriteNodeAsync("input/turn_request.json", new JsonObject
        {
            ["sessionId"] = "test-session",
            ["requestId"] = "test-request",
            ["turnNumber"] = 12
        });

        var manifest = new JsonObject
        {
            ["sessionId"] = "test-session",
            ["requestId"] = "test-request",
            ["turnNumber"] = 12,
            ["requestTimestamp"] = "2026-04-16T00:00:00Z",
            ["playerAction"] = "test",
            ["files"] = new JsonObject
            {
                [NormalizeRelativePath(ShiningCoreActionRequestState.PendingActionsRequestPath)] = requestSnapshotPath,
                ["game_state/meta/shining_abode_state.json"] = shiningSnapshotPath,
                ["game_state/meta/guardian_abode_residents.json"] = residentSnapshotPath,
                ["game_state/meta/soul_state.json"] = soulSnapshotPath
            },
            ["snapshotFileHashes"] = new JsonObject
            {
                [NormalizeRelativePath(ShiningCoreActionRequestState.PendingActionsRequestPath)] = ComputeSha256(await _fs.ReadFileAsync(requestSnapshotPath) ?? ""),
                ["game_state/meta/shining_abode_state.json"] = ComputeSha256(await _fs.ReadFileAsync(shiningSnapshotPath) ?? ""),
                ["game_state/meta/guardian_abode_residents.json"] = ComputeSha256(await _fs.ReadFileAsync(residentSnapshotPath) ?? ""),
                ["game_state/meta/soul_state.json"] = ComputeSha256(await _fs.ReadFileAsync(soulSnapshotPath) ?? "")
            },
            ["clientOwnedValidationHashes"] = new JsonObject(),
            ["rollbackBackups"] = new JsonObject
            {
                [NormalizeRelativePath(ShiningCoreActionRequestState.PendingActionsRequestPath)] = requestSnapshotPath,
                ["game_state/meta/shining_abode_state.json"] = shiningSnapshotPath,
                ["game_state/meta/guardian_abode_residents.json"] = residentSnapshotPath,
                ["game_state/meta/soul_state.json"] = soulSnapshotPath
            },
            ["rollbackBaselineFiles"] = new JsonArray(
                NormalizeRelativePath(ShiningCoreActionRequestState.PendingActionsRequestPath),
                "game_state/meta/shining_abode_state.json",
                "game_state/meta/guardian_abode_residents.json",
                "game_state/meta/soul_state.json"),
            ["sourceLabel"] = "shining-core-resolution-tests",
            ["manifestPayloadHash"] = string.Empty
        };

        manifest["manifestPayloadHash"] = PendingTurnSnapshotTestAuthority.ComputeManifestPayloadHash(manifest);
        await WriteNodeAsync("game_state/control/pending_turn_snapshot.json", manifest);
        await PendingTurnSnapshotTestAuthority.SyncAuthorityForCurrentManifestAsync(_fs);
    }

    private async Task WriteLegacyNativeDiscoverySnapshotManifestAsync(JsonObject preTurnShiningRoot, JsonObject preTurnResidentRoot, JsonObject preTurnSoulRoot)
    {
        const string shiningSnapshotPath = "game_state/control/pending_turn_snapshot/game_state/meta/shining_abode_state.json";
        const string residentSnapshotPath = "game_state/control/pending_turn_snapshot/game_state/meta/guardian_abode_residents.json";
        const string soulSnapshotPath = "game_state/control/pending_turn_snapshot/game_state/meta/soul_state.json";

        await WriteNodeAsync(shiningSnapshotPath, preTurnShiningRoot);
        await WriteNodeAsync(residentSnapshotPath, preTurnResidentRoot);
        await WriteNodeAsync(soulSnapshotPath, preTurnSoulRoot);
        await WriteNodeAsync("input/turn_request.json", new JsonObject
        {
            ["sessionId"] = "test-session",
            ["requestId"] = "test-request",
            ["turnNumber"] = 12
        });

        var manifest = new JsonObject
        {
            ["sessionId"] = "test-session",
            ["requestId"] = "test-request",
            ["turnNumber"] = 12,
            ["requestTimestamp"] = "2026-04-16T00:00:00Z",
            ["playerAction"] = "legacy-native-discovery",
            ["files"] = new JsonObject
            {
                ["game_state/meta/shining_abode_state.json"] = shiningSnapshotPath,
                ["game_state/meta/guardian_abode_residents.json"] = residentSnapshotPath,
                ["game_state/meta/soul_state.json"] = soulSnapshotPath
            },
            ["snapshotFileHashes"] = new JsonObject
            {
                ["game_state/meta/shining_abode_state.json"] = ComputeSha256(await _fs.ReadFileAsync(shiningSnapshotPath) ?? ""),
                ["game_state/meta/guardian_abode_residents.json"] = ComputeSha256(await _fs.ReadFileAsync(residentSnapshotPath) ?? ""),
                ["game_state/meta/soul_state.json"] = ComputeSha256(await _fs.ReadFileAsync(soulSnapshotPath) ?? "")
            },
            ["clientOwnedValidationHashes"] = new JsonObject(),
            ["rollbackBackups"] = new JsonObject
            {
                ["game_state/meta/shining_abode_state.json"] = shiningSnapshotPath,
                ["game_state/meta/guardian_abode_residents.json"] = residentSnapshotPath,
                ["game_state/meta/soul_state.json"] = soulSnapshotPath
            },
            ["rollbackBaselineFiles"] = new JsonArray(
                "game_state/meta/shining_abode_state.json",
                "game_state/meta/guardian_abode_residents.json",
                "game_state/meta/soul_state.json"),
            ["sourceLabel"] = "shining-legacy-native-discovery-tests",
            ["manifestPayloadHash"] = string.Empty
        };

        manifest["manifestPayloadHash"] = PendingTurnSnapshotTestAuthority.ComputeManifestPayloadHash(manifest);
        await WriteNodeAsync("game_state/control/pending_turn_snapshot.json", manifest);
        await PendingTurnSnapshotTestAuthority.SyncAuthorityForCurrentManifestAsync(_fs);
    }

    private async Task<List<ValidationIssue>> InvokeValidationAsync(string methodName)
    {
        var issues = new List<ValidationIssue>();
        var method = typeof(ValidationService).GetMethod(methodName, BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(method);
        var task = method!.Invoke(_validator, new object[] { issues }) as Task;
        Assert.NotNull(task);
        await task!;
        return issues;
    }

    private static JsonObject CreateBaseSoulRoot() => new()
    {
        ["currentRealm"] = "Shining Abode",
        ["currentIncarnation"] = 2,
        ["soulName"] = "Тестовая душа",
        ["inkFeathers"] = new JsonObject
        {
            ["current"] = 50
        },
        ["soulRelics"] = new JsonObject
        {
            ["equipped"] = new JsonArray(),
            ["stored"] = new JsonArray
            {
                new JsonObject
                {
                    ["relicId"] = "relic_old",
                    ["name"] = "Старый Клинок",
                    ["rarity"] = "rare",
                    ["formTag"] = "blade",
                    ["properties"] = new JsonArray
                    {
                        new JsonObject
                        {
                            ["propertyId"] = "edge",
                            ["band"] = "rare"
                        }
                    }
                }
            }
        }
    };

    private static JsonObject CreatePendingForgeBlessingState() => new()
    {
        ["applicationState"] = "active",
        ["materializedAtUtc"] = "2026-04-16T12:00:00Z",
        ["currentIncarnation"] = 2,
        ["sourcePackagePreparedAtTurn"] = 11,
        ["sourceCardIds"] = new JsonArray("card_relic"),
        ["sourceCardCount"] = 1,
        ["relicRefinementEntitlements"] = new JsonObject
        {
            ["rerolls"] = 0,
            ["freeShape"] = true,
            ["freeRetune"] = false,
            ["status"] = ShiningBlessingEffectState.RelicStatusPendingEntitlement,
            ["sourceCardIds"] = new JsonArray("card_relic")
        }
    };

    private static JsonObject CreateBaseResidentRoot() => new()
    {
        ["entries"] = new JsonArray
        {
            new JsonObject
            {
                ["residentId"] = "resident_liora",
                ["displayName"] = "Лиора",
                ["ascensionState"] = ShiningAbodeState.AscensionStateAscended,
                ["shiningFactionId"] = "faction_old",
                ["residentRole"] = ShiningAbodeState.ResidentRoleDescentSupport,
                ["grantedRelicId"] = "relic_liora",
                ["factionLoyaltyLevel"] = 20,
                ["factionLoyaltyTier"] = ShiningAbodeState.ResolveFactionLoyaltyTier(20),
                ["factionRestlessness"] = 20,
                ["factionRealignmentState"] = ShiningAbodeState.ResolveFactionRealignmentState(20, 20)
            }
        }
    };

    private static JsonObject CreateBaseShiningRoot()
    {
        var root = ShiningAbodeState.CreateDefaultState();
        root["availability"] = ShiningAbodeState.AvailabilityActive;
        root["radiance"] = new JsonObject
        {
            ["experience"] = 250,
            ["tier"] = 2
        };
        root["lightSparks"] = 80;
        root["halls"] = new JsonArray
        {
            new JsonObject
            {
                ["hallId"] = "hall_old",
                ["hallName"] = "Старый Зал",
                ["description"] = "Старый зал союза.",
                ["serviceTags"] = new JsonArray("social", "descent")
            }
        };
        root["factions"] = new JsonArray
        {
            new JsonObject
            {
                ["factionId"] = "faction_old",
                ["originType"] = ShiningAbodeState.OriginTypeAscendedGuardian,
                ["hallId"] = "hall_old",
                ["charter"] = new JsonObject
                {
                    ["factionName"] = "Старый Дом",
                    ["favoredArchetype"] = ShiningAbodeState.ProjectArchetypePassage,
                    ["patronEffectFamily"] = ShiningAbodeState.EffectFamilySocial,
                    ["summary"] = "Старая сияющая фракция."
                },
                ["leadership"] = new JsonObject
                {
                    ["headActorType"] = ShiningAbodeState.HeadActorTypeGuardian,
                    ["headActorId"] = "guardian_old",
                    ["leadershipState"] = ShiningAbodeState.LeadershipStateSecure
                },
                ["baseStrength"] = 35,
                ["factionStrength"] = 46,
                ["investCountThisAscension"] = 0,
                ["projectArchetypesCountedThisAscension"] = new JsonArray(),
                ["projects"] = new JsonArray
                {
                    new JsonObject
                    {
                        ["projectId"] = "project_passage",
                        ["displayName"] = "Путь Света",
                        ["summary"] = "Открывает descent-карты.",
                        ["toneTags"] = new JsonArray("descent"),
                        ["targetFactionIds"] = new JsonArray(),
                        ["projectArchetype"] = ShiningAbodeState.ProjectArchetypePassage,
                        ["outputEffectFamily"] = ShiningAbodeState.EffectFamilyDescent,
                        ["tier"] = 1,
                        ["status"] = ShiningAbodeState.ProjectStatusCompleted,
                        ["isSupported"] = true,
                        ["strengthReward"] = 8
                    }
                },
                ["leadershipReceipts"] = new JsonArray(),
                ["leadershipHistory"] = new JsonArray()
            }
        };
        root["gates"] = new JsonObject
        {
            ["draftVersion"] = 1,
            ["hasOpenDraft"] = true,
            ["isStale"] = false,
            ["allCandidateBlessingCards"] = new JsonArray
            {
                CreateCard("card_social", ShiningAbodeState.CardSourceTypeHead, "guardian_old", ShiningAbodeState.EffectFamilySocial, "uncommon"),
                CreateCard("card_descent", ShiningAbodeState.CardSourceTypeResidentDescent, "resident_liora", ShiningAbodeState.EffectFamilyDescent, "rare")
            },
            ["availableBlessingCards"] = new JsonArray
            {
                CreateCard("card_social", ShiningAbodeState.CardSourceTypeHead, "guardian_old", ShiningAbodeState.EffectFamilySocial, "uncommon"),
                CreateCard("card_descent", ShiningAbodeState.CardSourceTypeResidentDescent, "resident_liora", ShiningAbodeState.EffectFamilyDescent, "rare")
            },
            ["shownBlessingCardIds"] = new JsonArray("card_social", "card_descent"),
            ["selectedBlessingCardIds"] = new JsonArray(),
            ["nextCandidateCursor"] = 2,
            ["rerollsRemaining"] = 0
        };
        return root;
    }

    private static JsonObject CreateBaseShiningRootWithLegacyNativeDiscovery()
    {
        var root = CreateBaseShiningRoot();
        var discoveryCost = ShiningAbodeState.GetNativeDiscoveryCost();
        root["lightSparks"] = GetNodeInt(root["lightSparks"]) - discoveryCost.LightSparks;
        root["pendingNativeFactionDiscovery"] = new JsonObject
        {
            ["requestId"] = "discover_native_faction:0016",
            ["createdAtTurn"] = 16,
            ["createdAtUtc"] = "2026-04-16T12:49:00Z",
            ["radianceTierAtRequest"] = 2,
            ["costFeathers"] = discoveryCost.Feathers,
            ["costLightSparks"] = discoveryCost.LightSparks
        };
        return root;
    }

    private static void MaterializeLegacyNativeDiscoveryClosure(JsonObject currentShiningRoot, JsonObject currentResidentRoot, JsonObject currentSoulRoot)
    {
        currentShiningRoot["pendingNativeFactionDiscovery"] = null;
        currentShiningRoot["radiance"]!["experience"] = GetNodeInt(currentShiningRoot["radiance"]?["experience"]) + 20;
        currentShiningRoot["halls"]!.AsArray().Add(new JsonObject
        {
            ["hallId"] = "hall_native",
            ["hallName"] = "Нативный Зал",
            ["description"] = "Новая сияющая фракция.",
            ["serviceTags"] = new JsonArray("social", "memory")
        });
        currentShiningRoot["factions"]!.AsArray().Add(new JsonObject
        {
            ["factionId"] = "faction_native",
            ["originType"] = ShiningAbodeState.OriginTypeNativeRadiant,
            ["hallId"] = "hall_native",
            ["charter"] = new JsonObject
            {
                ["factionName"] = "Нативный Хор",
                ["favoredArchetype"] = ShiningAbodeState.ProjectArchetypeAccord,
                ["patronEffectFamily"] = ShiningAbodeState.EffectFamilySocial,
                ["summary"] = "Открытая нативная фракция."
            },
            ["leadership"] = new JsonObject
            {
                ["headActorType"] = ShiningAbodeState.HeadActorTypeRadiantActor,
                ["headActorId"] = "actor_native_head",
                ["leadershipState"] = ShiningAbodeState.LeadershipStateSecure
            },
            ["baseStrength"] = 35,
            ["factionStrength"] = 43,
            ["investCountThisAscension"] = 0,
            ["projectArchetypesCountedThisAscension"] = new JsonArray(),
            ["projects"] = new JsonArray(
                CreateDiscoveryProject("project_native_a"),
                CreateDiscoveryProject("project_native_b")),
            ["tradeInventoryReceipts"] = new JsonArray(),
            ["leadershipReceipts"] = new JsonArray(),
            ["leadershipHistory"] = new JsonArray()
        });
        currentShiningRoot["shiningPoliticalActors"] = new JsonArray(new JsonObject
        {
            ["actorId"] = "actor_native_head",
            ["actorType"] = ShiningAbodeState.HeadActorTypeRadiantActor,
            ["displayName"] = "Архон Нативного Хора",
            ["summary"] = "Глава новой фракции.",
            ["originFactionId"] = "faction_native",
            ["currentFactionId"] = "faction_native",
            ["politicalStatus"] = ShiningAbodeState.PoliticalStatusHead
        });
        ShiningAbodeState.EnsureCoreActionReceiptsArray(currentShiningRoot).Add(new JsonObject
        {
            ["requestId"] = "discover_native_faction:0016",
            ["actionType"] = ShiningCoreActionRequestState.ActionTypeDiscoverNativeFaction,
            ["status"] = ShiningCoreActionRequestState.RequestStatusAccepted,
            ["factionId"] = "",
            ["projectId"] = "",
            ["hallId"] = "hall_native",
            ["resolvedFactionId"] = "faction_native",
            ["selectedCardIds"] = new JsonArray(),
            ["newResidentIds"] = new JsonArray("resident_native_a", "resident_native_b"),
            ["seededProjectIds"] = new JsonArray("project_native_a", "project_native_b"),
            ["resolvedAtTurn"] = 16,
            ["resolvedAtUtc"] = "2026-04-16T12:50:00Z",
            ["reason"] = "legacy_discovery_resolved"
        });

        var residents = currentResidentRoot["entries"]!.AsArray();
        residents.Add(CreateDiscoveryResident("resident_native_a", "faction_native"));
        residents.Add(CreateDiscoveryResident("resident_native_b", "faction_native"));
        currentSoulRoot["inkFeathers"]!["current"] = GetNodeInt(currentSoulRoot["inkFeathers"]?["current"]) - ShiningAbodeState.GetNativeDiscoveryCost().Feathers;
    }

    private static JsonObject CreateCard(string cardId, string sourceType, string sourceActorId, string effectFamily, string rarity) => new()
    {
        ["cardId"] = cardId,
        ["dedupeKey"] = $"{effectFamily}:{cardId}",
        ["sourceType"] = sourceType,
        ["sourceFactionId"] = "faction_old",
        ["sourceActorId"] = sourceActorId,
        ["effectFamily"] = effectFamily,
        ["rarity"] = rarity,
        ["displayName"] = cardId,
        ["displaySummary"] = "summary",
        ["effectPayload"] = new JsonObject
        {
            ["type"] = "noop"
        }
    };

    private static JsonObject CreateDiscoverNativeFactionRequest() => new()
    {
        ["requestId"] = "core_req_discover",
        ["actionType"] = ShiningCoreActionRequestState.ActionTypeDiscoverNativeFaction,
        ["factionId"] = "",
        ["factionName"] = "",
        ["projectId"] = "",
        ["projectDisplayName"] = "",
        ["radianceTierAtRequest"] = 2,
        ["quotedCostFeathers"] = 25,
        ["quotedCostLightSparks"] = 20,
        ["sourceDraftVersion"] = 0,
        ["selectedCardIds"] = new JsonArray(),
        ["createdAtTurn"] = 16,
        ["createdAtUtc"] = "2026-04-16T12:49:00Z"
    };

    private static JsonObject CreateDiscoveryResident(string residentId, string factionId) => new()
    {
        ["residentId"] = residentId,
        ["displayName"] = residentId,
        ["ascensionState"] = ShiningAbodeState.AscensionStateAscended,
        ["shiningFactionId"] = factionId,
        ["residentRole"] = ShiningAbodeState.ResidentRoleDescentSupport,
        ["factionLoyaltyLevel"] = 50,
        ["factionLoyaltyTier"] = ShiningAbodeState.ResolveFactionLoyaltyTier(50),
        ["factionRestlessness"] = 10,
        ["factionRealignmentState"] = ShiningAbodeState.ResolveFactionRealignmentState(50, 10)
    };

    private static JsonObject CreateDiscoveryProject(string projectId) => new()
    {
        ["projectId"] = projectId,
        ["displayName"] = projectId,
        ["summary"] = projectId,
        ["toneTags"] = new JsonArray("social"),
        ["targetFactionIds"] = new JsonArray(),
        ["projectArchetype"] = ShiningAbodeState.ProjectArchetypePassage,
        ["outputEffectFamily"] = ShiningAbodeState.EffectFamilySocial,
        ["tier"] = 1,
        ["status"] = ShiningAbodeState.ProjectStatusCompleted,
        ["isSupported"] = false,
        ["strengthReward"] = 8,
        ["completedAtTurn"] = 16,
        ["completedAtUtc"] = "2026-04-16T12:50:00Z"
    };

    private async Task WriteNodeAsync(string relativePath, JsonNode node)
    {
        await _fs.WriteFileAtomicAsync(relativePath, node.ToJsonString(new JsonSerializerOptions
        {
            WriteIndented = true,
            TypeInfoResolver = new DefaultJsonTypeInfoResolver()
        }));
    }

    private static JsonObject CloneJsonObject(JsonObject source) => JsonNode.Parse(source.ToJsonString())!.AsObject();

    private static string ComputeSha256(string content)
    {
        using var sha = SHA256.Create();
        return Convert.ToHexString(sha.ComputeHash(Encoding.UTF8.GetBytes(content)));
    }

    private static int GetNodeInt(JsonNode? node)
    {
        if (node is JsonValue value && value.TryGetValue<int>(out var intValue))
            return intValue;
        return 0;
    }

    private static string NormalizeRelativePath(string path) => path.Replace('\\', '/');

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(_rootPath))
                Directory.Delete(_rootPath, recursive: true);
        }
        catch
        {
            // ignore cleanup issues
        }
    }
}
