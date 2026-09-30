using System.Reflection;
using System.Text.Json.Nodes;
using BookOfEternityClient.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed partial class AfterlifeResourceCutoverTests
{
    private const string IntakeBaselineLocationId = "loc_intake_before";

    /// <summary>
    /// Reconstructs all reachable raw item and location allocations through a fresh validator on the same root.
    /// </summary>
    [Fact]
    public async Task OriginalSpiritualIntake_ReplaysRealOwnersWithoutCanonicalWrites()
    {
        await using var context = await CreateCompleteConflictFrameContextAsync(
            seedOriginalInputs: SeedOriginalIntakeBaselinesAsync);
        await WriteCompleteConflictFrameExchangeAsync(context);
        await WriteOriginalIntakeDraftAsync(context);
        AssertNoConflictFrameErrors(await context.Validator.ValidateAcceptedTurnRawMortalLocationMaterializationAsync());
        AssertNoConflictFrameErrors(await context.Validator.ValidateAcceptedTurnRawMortalItemMaterializationAsync());
        await using var lease = await context.FileSystem.AcquireCanonicalWriteLeaseAsync();
        var paths = context.FileSystem.EnumerateFiles(lease, "*").Order(StringComparer.Ordinal).ToArray();
        var before = new Dictionary<string, byte[]?>();
        foreach (var path in paths) before[path] = await context.FileSystem.ReadFileBytesAsync(lease, path);

        var recorded = await context.Validator.CaptureSpiritualOriginalTurnWithIntakeAsync(lease);
        AssertNoConflictFrameErrors(recorded.Issues);
        using var first = Assert.IsType<ValidationService.SpiritualOriginalTurnCapture>(recorded.Capture);
        var input = Assert.IsType<AcceptedMechanicsInput>(OriginalCaptureField(first, "_input"));
        var owner = Assert.Single(input.PlanningContext!.Owners.Entries.Values,
            value => value.Key.OwnerKind == ResourceOwnerKind.Item);
        Assert.True(owner.SameTurn);
        Assert.Equal(MortalItemTestFixture.CreationRef, owner.SameTurnRef);
        AssertNoConflictFrameErrors(await first.BeginResourceExecutionAsync(lease));
        AssertNoConflictFrameErrors((await first.AdvanceNextResourceExchangeAsync(lease)).Issues);
        var rows = first.ReadAllocationJournal(lease).ToJsonString();
        var kinds = JsonNode.Parse(rows)!.AsArray().Select(row => row!["kind"]!.GetValue<string>()).ToArray();
        foreach (var kind in new[] { "item", "location", "location_receipt", "location_link",
                     "location_link_receipt", "location_transition", "location_threat" })
            Assert.Contains(kind, kinds);
        Assert.False(MortalItemAcceptedTurnAuthority.HasValidatedItems(context.FileSystem, lease));
        Assert.Empty(MortalItemAcceptedTurnAuthority.GetValidatedOwners(context.FileSystem, lease,
            input.SessionId, input.SnapshotToken));
        first.Dispose();

        var fresh = new ValidationService(context.FileSystem, NullLogger<ValidationService>.Instance);
        var replayed = await fresh.CaptureSpiritualOriginalTurnWithIntakeAsync(lease, rows, replayAllocations: true);
        AssertNoConflictFrameErrors(replayed.Issues);
        using var second = Assert.IsType<ValidationService.SpiritualOriginalTurnCapture>(replayed.Capture);
        var nextInput = Assert.IsType<AcceptedMechanicsInput>(OriginalCaptureField(second, "_input"));
        Assert.Equal(input.PlanningContext.Owners.Fingerprint, nextInput.PlanningContext!.Owners.Fingerprint);
        AssertNoConflictFrameErrors(await second.BeginResourceExecutionAsync(lease));
        AssertNoConflictFrameErrors((await second.AdvanceNextResourceExchangeAsync(lease)).Issues);
        Assert.Equal(rows, second.ReadAllocationJournal(lease).ToJsonString());
        Assert.False(AcceptedMechanicsPlanAuthority.HasValidated(context.FileSystem, lease));
        Assert.Equal(paths, context.FileSystem.EnumerateFiles(lease, "*").Order(StringComparer.Ordinal));
        foreach (var pair in before)
            Assert.Equal(pair.Value, await context.FileSystem.ReadFileBytesAsync(lease, pair.Key));
    }

    /// <summary>
    /// Rejects mismatched replay or later resource admission without exposing a temporary item handoff.
    /// </summary>
    /// <param name="corruptJournal">
    /// Changes a retained causal coordinate when <see langword="true"/>; otherwise rejects resource input after item intake.
    /// </param>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task OriginalSpiritualIntake_RejectionRevokesTemporaryAdmission(bool corruptJournal)
    {
        await using var context = await CreateCompleteConflictFrameContextAsync(
            seedOriginalInputs: SeedOriginalIntakeBaselinesAsync);
        await WriteCompleteConflictFrameExchangeAsync(context);
        await WriteOriginalIntakeDraftAsync(context);
        string rows;
        MortalItemIdentityFactory firstItems;
        await using (var initialLease = await context.FileSystem.AcquireCanonicalWriteLeaseAsync())
        {
            _ = context.FileSystem.GetOrCreateSessionGeneration(initialLease);
            var initial = await context.Validator.CaptureSpiritualOriginalTurnWithIntakeAsync(initialLease);
            AssertNoConflictFrameErrors(initial.Issues);
            using var capture = Assert.IsType<ValidationService.SpiritualOriginalTurnCapture>(initial.Capture);
            rows = capture.ReadAllocationJournal(initialLease).ToJsonString();
            var firstOwner = OriginalCaptureField(capture, "_allocations");
            Assert.NotNull(firstOwner);
            firstItems = Assert.IsAssignableFrom<MortalItemIdentityFactory>(firstOwner.GetType()
                .GetProperty("Items", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(firstOwner));
        }
        if (!corruptJournal)
            await context.WriteExactJsonAsync(ResourceMaterializationTestContext.CommandsPath, "{\"schemaVersion\":999}");
        await using var lease = await context.FileSystem.AcquireCanonicalWriteLeaseAsync();
        var paths = context.FileSystem.EnumerateFiles(lease, "*").Order(StringComparer.Ordinal).ToArray();
        var before = new Dictionary<string, byte[]?>();
        foreach (var path in paths) before[path] = await context.FileSystem.ReadFileBytesAsync(lease, path);
        if (corruptJournal)
        {
            var changed = JsonNode.Parse(rows)!.AsArray();
            changed[0]!["coordinate"] = "changed-cause";
            await Assert.ThrowsAsync<InvalidOperationException>(() =>
                context.Validator.CaptureSpiritualOriginalTurnWithIntakeAsync(lease, changed.ToJsonString(), true));
        }
        else
        {
            var rejected = await context.Validator.CaptureSpiritualOriginalTurnWithIntakeAsync(lease, rows, true);
            Assert.Null(rejected.Capture);
            Assert.Contains(rejected.Issues, issue => issue.Severity == IssueSeverity.Error);
        }
        Assert.False(MortalItemAcceptedTurnAuthority.HasValidatedItems(context.FileSystem, lease));
        if (!corruptJournal)
        {
            var slots = ReadStaticField(typeof(AcceptedTurnAuthorityRegistry), "RootSlots");
            var slot = ReadWeakValue(slots, context.FileSystem.CanonicalRootAuthorityIdentity);
            var cache = ReadField(ReadField(slot, "_state"), "_mortalItems");
            var rejectedFactory = Assert.IsAssignableFrom<MortalItemIdentityFactory>(
                ReadField(cache, "_identityFactory"));
            Assert.NotSame(firstItems, rejectedFactory);
            Assert.True(rejectedFactory.IsAttemptScoped);
            Assert.False(Assert.IsType<bool>(ReadField(cache, "_validated")));
            Assert.Contains(Assert.IsType<MortalItemAcceptedTurnOwner[]>(ReadField(cache, "_owners")),
                owner => owner.ItemRef == MortalItemTestFixture.CreationRef);
        }
        Assert.False(AcceptedMechanicsPlanAuthority.HasValidated(context.FileSystem, lease));
        Assert.False(EffectAcceptedTurnPlanAuthority.TryPeekValidated(context.FileSystem, lease, out _, out _));
        foreach (var pair in before)
            Assert.Equal(pair.Value, await context.FileSystem.ReadFileBytesAsync(lease, pair.Key));
        Assert.Equal(paths, context.FileSystem.EnumerateFiles(lease, "*").Order(StringComparer.Ordinal));
        if (corruptJournal)
        {
            var retried = await context.Validator.CaptureSpiritualOriginalTurnWithIntakeAsync(lease, rows, true);
            AssertNoConflictFrameErrors(retried.Issues);
            using var capture = Assert.IsType<ValidationService.SpiritualOriginalTurnCapture>(retried.Capture);
            Assert.Equal(rows, capture.ReadAllocationJournal(lease).ToJsonString());
        }
    }

    /// <summary>
    /// Seeds item and location owner baselines before the fixture signs its only original snapshot.
    /// </summary>
    /// <param name="context">
    /// Owned fixture whose original baselines are being constructed.
    /// </param>
    private static async Task SeedOriginalIntakeBaselinesAsync(ResourceMaterializationTestContext context)
    {
        var location = MortalLocationTestFixture.CreateCanonicalLocationWithIdentity(
            IntakeBaselineLocationId, "Исходный брод", x: 40);
        await context.WriteExactJsonAsync(InventoryEquipmentService.ItemsPath,
            new JsonObject { ["items"] = new JsonArray(), ["equippedItems"] = new JsonObject() }.ToJsonString());
        await context.WriteExactJsonAsync(MortalItemIdentityState.StatePath,
            MortalItemIdentityState.CreateEmptyRoot().ToJsonString());
        await context.WriteExactJsonAsync(MortalLocationMaterializationContract.WorldMapPath,
            MortalLocationTestFixture.CreateWorldMap(location).ToJsonString());
        await context.WriteExactJsonAsync(MortalLocationMaterializationContract.CurrentLocationPath,
            MortalLocationTestFixture.CreateCurrentProjection(location).ToJsonString());
        await context.WriteExactJsonAsync(MortalLocationIdentityState.StatePath,
            MortalLocationTestFixture.CreateIdentityIndex(location).ToJsonString());
        foreach (var path in new[]
                 {
                     "game_state/inventory/item_bonds.json",
                     "game_state/inventory/item_text_updates.json",
                     "game_state/inventory/recipes.json",
                     "game_state/npcs/item_journals.json",
                     "game_state/quests/quest_history.json"
                 })
            await context.WriteExactJsonAsync(path, "{}");
    }

    /// <summary>
    /// Writes actual admitted raw owner proposals that reach every upstream allocation family.
    /// </summary>
    /// <param name="context">
    /// Signed fixture whose distributed original draft is still under construction.
    /// </param>
    private static async Task WriteOriginalIntakeDraftAsync(ResourceMaterializationTestContext context)
    {
        var item = MortalItemTestFixture.CreateRawRoot();
        item["resourceMaterialization"] = new JsonObject
        {
            ["resources"] = new JsonArray(new JsonObject { ["resourceKey"] = "durability", ["maximum"] = 100 })
        };
        var inventory = Assert.IsType<JsonObject>(await context.ReadJsonAsync(InventoryEquipmentService.ItemsPath));
        inventory["UpdateInventory"] = new JsonArray(item);
        await context.WriteExactJsonAsync(InventoryEquipmentService.ItemsPath, inventory.ToJsonString());
        var map = Assert.IsType<JsonObject>(await context.ReadJsonAsync(MortalLocationMaterializationContract.WorldMapPath));
        var link = MortalLocationTestFixture.CreateRawLink(IntakeBaselineLocationId, "temporary_target");
        link["targetLocationId"] = null;
        link["targetInitialId"] = MortalLocationTestFixture.LocationInitialId;
        var location = MortalLocationTestFixture.CreateRawLocation();
        location["materialization"]!["sections"]!["topology"] = new JsonObject
        {
            ["disposition"] = "populated", ["reason"] = null
        };
        map["worldMapUpdates"] = new JsonObject
        {
            ["newLocations"] = new JsonArray(location),
            ["newLinks"] = new JsonArray(link),
            ["locationUpdates"] = new JsonArray(new JsonObject
            {
                ["locationId"] = IntakeBaselineLocationId, ["displayName"] = "Обновлённый брод"
            }),
            ["threatsToAdd"] = new JsonArray(new JsonObject
            {
                ["targetLocationId"] = IntakeBaselineLocationId,
                ["threat"] = new JsonObject
                {
                    ["threatId"] = null, ["name"] = "Рейд", ["description"] = "Угроза у брода.",
                    ["intensity"] = 3, ["longTermGoal"] = "Захватить брод.", ["currentActivity"] = null,
                    ["threatArchetype"] = new JsonObject
                    {
                        ["motivation"] = "Domination", ["method"] = "Overt",
                        ["customMotivation"] = null, ["customMethod"] = null
                    },
                    ["impactProfile"] = new JsonObject
                    {
                        ["primaryTargetType"] = "Location", ["primaryTargetId"] = null,
                        ["primaryTargetName"] = "Брод", ["primaryImpact"] = "Stability", ["baseImpactValue"] = 2
                    }
                }
            })
        };
        await context.WriteExactJsonAsync(MortalLocationMaterializationContract.WorldMapPath, map.ToJsonString());
    }
}
