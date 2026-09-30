using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text.Json.Nodes;
using BookOfEternityClient.Core;
using BookOfEternityClient.Services;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed partial class AfterlifeResourceCutoverTests
{
    private const string CandidateTransferItemId = "itm_original_candidate_transfer";
    private const string CandidateTransferNpcId = "npc_original_candidate_transfer";
    private const string CandidateQuestRewardId = "quest_reward_original_candidate";
    private const string CandidateMissingItemId = "itm_original_candidate_missing";

    /// <summary>
    /// Admits actual location and item owners from the retained original while both physical current carriers differ.
    /// </summary>
    [Fact]
    public async Task OriginalSpiritualCandidateReadView_UsesRetainedLocationAndItemInputs()
    {
        await using var context = await CreateCompleteConflictFrameContextAsync(
            seedOriginalInputs: SeedCandidateQuestBaselinesAsync);
        await WriteCompleteConflictFrameExchangeAsync(context);
        await WriteOriginalIntakeDraftAsync(context);
        var inventoryDraft = Assert.IsType<JsonObject>(
            await context.ReadJsonAsync(InventoryEquipmentService.ItemsPath));
        var questItem = Assert.IsType<JsonObject>(inventoryDraft["UpdateInventory"]![0]);
        questItem["materialization"]!["route"] = "quest_reward";
        questItem["materialization"]!["sourceAuthority"] = new JsonObject
        {
            ["kind"] = "quest_reward", ["authorityId"] = CandidateQuestRewardId
        };
        await context.WriteExactJsonAsync(InventoryEquipmentService.ItemsPath, inventoryDraft.ToJsonString());
        var questHistory = new JsonObject
        {
            ["questHistory"] = new JsonArray(),
            ["questRewards"] = new JsonArray(new JsonObject
            {
                ["questId"] = "quest_original_candidate",
                ["rewardId"] = CandidateQuestRewardId,
                ["itemsReceived"] = new JsonArray(new JsonObject
                {
                    ["creationRef"] = MortalItemTestFixture.CreationRef,
                    ["displayName"] = questItem["name"]!.GetValue<string>()
                })
            }),
            ["questChains"] = new JsonArray()
        };
        await context.WriteExactJsonAsync("game_state/quests/quest_history.json", questHistory.ToJsonString());
        var itemText = new JsonObject
        {
            ["updateItemTextContents"] = new JsonArray(new JsonObject
            {
                ["creationRef"] = MortalItemTestFixture.CreationRef,
                ["textToAppend"] = "Текст наградного предмета."
            })
        };
        await context.WriteExactJsonAsync("game_state/inventory/item_text_updates.json",
            itemText.ToJsonString());
        AssertNoConflictFrameErrors(await context.Validator.ValidateAcceptedTurnRawMortalLocationMaterializationAsync());
        AssertNoConflictFrameErrors(await context.Validator.ValidateAcceptedTurnRawMortalItemMaterializationAsync());
        await using var lease = await context.FileSystem.AcquireCanonicalWriteLeaseAsync();
        var captured = await context.Validator.CaptureSpiritualOriginalTurnWithIntakeAsync(lease);
        AssertNoConflictFrameErrors(captured.Issues);
        using var capture = Assert.IsType<ValidationService.SpiritualOriginalTurnCapture>(captured.Capture);
        var originalInputs = Assert.IsType<SpiritualOriginalDraftInputs>(OriginalCaptureField(capture, "_draftInputs"));
        var rows = capture.ReadAllocationJournal(lease).ToJsonString();
        var previousOwner = OriginalCaptureField(capture, "_allocations");
        Assert.NotNull(previousOwner);
        var previousItems = Assert.IsAssignableFrom<MortalItemIdentityFactory>(
            previousOwner.GetType().GetProperty("Items", BindingFlags.Instance | BindingFlags.NonPublic)!
                .GetValue(previousOwner));

        var rootSlots = ReadStaticField(typeof(AcceptedTurnAuthorityRegistry), "RootSlots");
        var slot = ReadWeakValue(rootSlots, context.FileSystem.CanonicalRootAuthorityIdentity);
        var state = ReadField(slot, "_state");
        var itemCache = ReadField(state, "_mortalItems");
        var previousFence = ReadField(itemCache, "_validatedFence");
        Assert.True(Assert.IsType<bool>(ReadField(itemCache, "_validated")));

        var ownerType = typeof(ValidationService).GetNestedType("SpiritualOriginalAllocationOwner",
            BindingFlags.NonPublic);
        Assert.NotNull(ownerType);
        var owner = Activator.CreateInstance(ownerType,
            BindingFlags.Instance | BindingFlags.NonPublic, binder: null,
            args: [rows, true, true], culture: null);
        Assert.NotNull(owner);
        var clock = Assert.IsType<SpiritualWoundProjectionClock>(ownerType.GetProperty("Clock",
            BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(owner));
        var method = typeof(ValidationService).GetMethod(
            "ValidateSpiritualOriginalLocationItemIntakeAsync", BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(method);
        using (var foreignScope = clock.BeginSpeculation())
        {
            var foreignTask = Assert.IsAssignableFrom<Task<IReadOnlyList<ValidationIssue>>>(
                method.Invoke(context.Validator, [lease, originalInputs, owner, foreignScope]));
            Assert.Contains(await foreignTask,
                issue => issue.Code == "spiritual_original_intake_claim_conflict");
        }
        const string changedPhysical = "{malformed-current-carrier";
        foreach (var path in ValidationService.SpiritualOriginalTurnCapture.FixedOriginalItemLocationCurrentPaths)
            await context.FileSystem.WriteFileAtomicAsync(lease, path, changedPhysical);
        context.FileSystem.DeleteFile(lease, MortalLocationMaterializationContract.WorldMapPath);
        context.FileSystem.DeleteFile(lease, InventoryEquipmentService.ItemsPath);
        Assert.Contains(await capture.CheckRetainedInputsAsync(lease),
            issue => issue.Code == "spiritual_original_input_changed" &&
                     ValidationService.SpiritualOriginalTurnCapture.FixedOriginalItemLocationCurrentPaths
                         .Contains(issue.FilePath, StringComparer.Ordinal));
        Assert.Same(previousFence, ReadField(itemCache, "_validatedFence"));
        Assert.True(Assert.IsType<bool>(ReadField(itemCache, "_validated")));
        var candidateItems = Assert.IsAssignableFrom<MortalItemIdentityFactory>(ownerType.GetProperty(
            "Items", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(owner));
        var foreignOwners = ReadField(itemCache, "_owners");
        MortalItemAcceptedTurnAuthority.InvalidateValidatedItems(context.FileSystem, lease, candidateItems);
        Assert.Same(previousFence, ReadField(itemCache, "_validatedFence"));
        Assert.Same(foreignOwners, ReadField(itemCache, "_owners"));
        Assert.True(Assert.IsType<bool>(ReadField(itemCache, "_validated")));
        MortalItemAcceptedTurnAuthority.InvalidateValidatedItems(context.FileSystem, lease, previousItems);

        using var scope = clock.BeginSpeculation();
        var task = Assert.IsAssignableFrom<Task<IReadOnlyList<ValidationIssue>>>(
            method.Invoke(context.Validator, [lease, originalInputs, owner, scope]));
        AssertNoConflictFrameErrors(await task);
        scope.Commit();
        var privateItems = Assert.IsAssignableFrom<MortalItemIdentityFactory>(ownerType.GetProperty(
            "Items", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(owner));
        Assert.Contains(MortalItemAcceptedTurnAuthority.GetValidatedOwners(context.FileSystem,
                lease, originalInputs.SessionId, originalInputs.SnapshotToken, privateItems),
            item => item.ItemRef == MortalItemTestFixture.CreationRef && item.SameTurn);
        Assert.Contains(MortalItemAcceptedTurnAuthority.GetValidatedEffectSources(context.FileSystem,
                lease, originalInputs.SessionId, originalInputs.SnapshotToken, privateItems),
            source => source.SourceRef == MortalItemTestFixture.CreationRef &&
                      source.Realm == "mortal_world" && source.Kind == "item" && source.SameTurn);
        Assert.Contains(CandidateMissingItemId,
            MortalItemAcceptedTurnAuthority.GetMissingGovernedItemIds(context.FileSystem,
                lease, originalInputs.SessionId, originalInputs.SnapshotToken, privateItems));
        Assert.Empty(MortalItemAcceptedTurnAuthority.GetValidatedEffectSources(context.FileSystem,
            lease, originalInputs.SessionId, originalInputs.SnapshotToken));
        Assert.Empty(MortalItemAcceptedTurnAuthority.GetMissingGovernedItemIds(context.FileSystem,
            lease, originalInputs.SessionId, originalInputs.SnapshotToken));
        Assert.Empty(MortalItemAcceptedTurnAuthority.GetValidatedOwners(context.FileSystem,
            lease, originalInputs.SessionId, originalInputs.SnapshotToken));
        var locationCaches = ReadStaticField(typeof(MortalLocationAcceptedTurnPlanAuthority), "Caches");
        var locationCache = ReadWeakValue(locationCaches, context.FileSystem);
        var locationResult = Assert.IsType<MortalLocationAcceptedTurnPlanningResult>(
            ReadField(locationCache, "_result"));
        Assert.True(locationResult.Success);
        Assert.Contains(MortalLocationTestFixture.LocationInitialId,
            locationResult.Plan!.LocationIdsByInitialId.Keys);

        var routes = Assert.IsAssignableFrom<IReadOnlyDictionary<string, MortalItemRouteAuthority>>(
            ReadField(itemCache, "_routesByCreationRef"));
        Assert.Contains(MortalItemTestFixture.CreationRef, routes.Keys);
        Assert.Equal("quest_reward", routes[MortalItemTestFixture.CreationRef].Route);
        Assert.Equal(CandidateQuestRewardId, routes[MortalItemTestFixture.CreationRef].AuthorityId);
        var currentRoots = Assert.IsAssignableFrom<IReadOnlyDictionary<string, JsonNode?>>(
            ReadField(itemCache, "_currentProjectionRoots"));
        Assert.True(JsonNode.DeepEquals(
            JsonNode.Parse(originalInputs.ReadText(InventoryEquipmentService.ItemsPath)!),
            currentRoots[InventoryEquipmentService.ItemsPath]));
        Assert.Null(currentRoots[MortalItemAcceptedTransferCatalog.PlayerRemovalPath]);
        foreach (var path in new[]
                 {
                     "game_state/inventory/item_bonds.json",
                     "game_state/inventory/item_text_updates.json",
                     "game_state/inventory/recipes.json",
                     "game_state/npcs/item_journals.json",
                     "game_state/quests/quest_history.json"
                 })
            Assert.True(JsonNode.DeepEquals(JsonNode.Parse(originalInputs.ReadText(path)!),
                currentRoots[path]));
        Assert.True(JsonNode.DeepEquals(questHistory,
            currentRoots["game_state/quests/quest_history.json"]));
        Assert.True(JsonNode.DeepEquals(itemText,
            currentRoots["game_state/inventory/item_text_updates.json"]));
        Assert.False(MortalItemAcceptedTurnAuthority.HasValidatedItems(context.FileSystem, lease));
        Assert.Null(await context.FileSystem.ReadFileAsync(lease,
            MortalLocationMaterializationContract.WorldMapPath));
        Assert.Null(await context.FileSystem.ReadFileAsync(lease,
            InventoryEquipmentService.ItemsPath));
        Assert.Equal(changedPhysical,
            await context.FileSystem.ReadFileAsync(lease,
                MortalItemAcceptedTransferCatalog.PlayerRemovalPath));

        var admittedIds = MortalItemAcceptedTurnAuthority.GetValidatedOwners(context.FileSystem,
            lease, originalInputs.SessionId, originalInputs.SnapshotToken, privateItems)
            .Select(item => item.ItemId).ToArray();
        using (var retryScope = clock.BeginSpeculation())
        {
            var retryTask = Assert.IsAssignableFrom<Task<IReadOnlyList<ValidationIssue>>>(
                method.Invoke(context.Validator, [lease, originalInputs, owner, retryScope]));
            AssertNoConflictFrameErrors(await retryTask);
            retryScope.Commit();
        }
        Assert.Equal(admittedIds,
            MortalItemAcceptedTurnAuthority.GetValidatedOwners(context.FileSystem,
                lease, originalInputs.SessionId, originalInputs.SnapshotToken, privateItems)
                .Select(item => item.ItemId).ToArray());
        var images = originalInputs.PathInventory.ToDictionary(
            path => path, originalInputs.ReadImage, StringComparer.Ordinal);
        foreach (var (session, request, snapshot, turn) in new[]
                 {
                     (originalInputs.SessionId + "_other", originalInputs.RequestId,
                         originalInputs.SnapshotToken, originalInputs.Turn),
                     (originalInputs.SessionId, originalInputs.RequestId + "_other",
                         originalInputs.SnapshotToken, originalInputs.Turn),
                     (originalInputs.SessionId, originalInputs.RequestId,
                         originalInputs.SnapshotToken + "_other", originalInputs.Turn),
                     (originalInputs.SessionId, originalInputs.RequestId,
                         originalInputs.SnapshotToken, originalInputs.Turn + 1)
                 })
        {
            var mismatched = SpiritualOriginalDraftInputs.Create(session, request, snapshot,
                turn, originalInputs.PathInventory, images);
            using var mismatchScope = clock.BeginSpeculation();
            var mismatchTask = Assert.IsAssignableFrom<Task<IReadOnlyList<ValidationIssue>>>(
                method.Invoke(context.Validator, [lease, mismatched, owner, mismatchScope]));
            Assert.Contains(await mismatchTask,
                issue => issue.Code == "spiritual_original_input_identity_mismatch");
        }
        var incompletePaths = originalInputs.PathInventory
            .Where(path => path != MortalItemAcceptedTransferCatalog.PlayerRemovalPath).ToArray();
        var incomplete = SpiritualOriginalDraftInputs.Create(originalInputs.SessionId,
            originalInputs.RequestId, originalInputs.SnapshotToken, originalInputs.Turn,
            incompletePaths, incompletePaths.ToDictionary(path => path,
                originalInputs.ReadImage, StringComparer.Ordinal));
        using (var missingScope = clock.BeginSpeculation())
        {
            var missingTask = Assert.IsAssignableFrom<Task<IReadOnlyList<ValidationIssue>>>(
                method.Invoke(context.Validator, [lease, incomplete, owner, missingScope]));
            await Assert.ThrowsAsync<KeyNotFoundException>(() => missingTask);
        }
        using (var closedScope = clock.BeginSpeculation())
        {
            closedScope.Dispose();
            var closedTask = Assert.IsAssignableFrom<Task<IReadOnlyList<ValidationIssue>>>(
                method.Invoke(context.Validator, [lease, originalInputs, owner, closedScope]));
            await Assert.ThrowsAsync<InvalidOperationException>(() => closedTask);
        }
        var nullScopeTask = Assert.IsAssignableFrom<Task<IReadOnlyList<ValidationIssue>>>(
            method.Invoke(context.Validator, [lease, originalInputs, owner, null]));
        await Assert.ThrowsAsync<ArgumentNullException>(() => nullScopeTask);
        var foreignOwner = Activator.CreateInstance(ownerType,
            BindingFlags.Instance | BindingFlags.NonPublic, binder: null,
            args: [rows, true, true], culture: null);
        Assert.NotNull(foreignOwner);
        var foreignClock = Assert.IsType<SpiritualWoundProjectionClock>(ownerType.GetProperty("Clock",
            BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(foreignOwner));
        using (var foreignScope = foreignClock.BeginSpeculation())
        {
            var foreignTask = Assert.IsAssignableFrom<Task<IReadOnlyList<ValidationIssue>>>(
                method.Invoke(context.Validator, [lease, originalInputs, owner, foreignScope]));
            await Assert.ThrowsAsync<InvalidOperationException>(() => foreignTask);
        }
        await using (var foreignContext = await ResourceMaterializationTestContext.CreateAsync())
        await using (var foreignLease = await foreignContext.FileSystem.AcquireCanonicalWriteLeaseAsync())
        using (var wrongLeaseScope = clock.BeginSpeculation())
        {
            var wrongLeaseTask = Assert.IsAssignableFrom<Task<IReadOnlyList<ValidationIssue>>>(
                method.Invoke(context.Validator, [foreignLease, originalInputs, owner, wrongLeaseScope]));
            await Assert.ThrowsAsync<InvalidOperationException>(() => wrongLeaseTask);
        }
        Assert.Equal(admittedIds,
            MortalItemAcceptedTurnAuthority.GetValidatedOwners(context.FileSystem,
                lease, originalInputs.SessionId, originalInputs.SnapshotToken, privateItems)
                .Select(item => item.ItemId).ToArray());
        await context.FileSystem.WriteFileAtomicAsync(lease,
            LiveTurnPreparationService.TurnRequestPath, changedPhysical);
        using var rejectedScope = clock.BeginSpeculation();
        var rejectedTask = Assert.IsAssignableFrom<Task<IReadOnlyList<ValidationIssue>>>(
            method.Invoke(context.Validator, [lease, originalInputs, owner, rejectedScope]));
        Assert.Contains(await rejectedTask, issue => issue.Severity == IssueSeverity.Error);
        Assert.Equal(admittedIds,
            MortalItemAcceptedTurnAuthority.GetValidatedOwners(context.FileSystem,
                lease, originalInputs.SessionId, originalInputs.SnapshotToken, privateItems)
                .Select(item => item.ItemId).ToArray());
        Assert.Equal(changedPhysical,
            await context.FileSystem.ReadFileAsync(lease, LiveTurnPreparationService.TurnRequestPath));
    }

    /// <summary>
    /// Rejects override, missing or faulted owners, expired leases and closed scopes without allocations.
    /// </summary>
    [Fact]
    public async Task OriginalSpiritualCandidateReadView_ZeroAllocationPreflightRejectsInvalidOwners()
    {
        await using var context = await CreateCompleteConflictFrameContextAsync(
            seedOriginalInputs: SeedOriginalIntakeBaselinesAsync);
        await WriteCompleteConflictFrameExchangeAsync(context);
        await WriteOriginalIntakeDraftAsync(context);
        AssertNoConflictFrameErrors(await context.Validator.ValidateAcceptedTurnRawMortalLocationMaterializationAsync());
        AssertNoConflictFrameErrors(await context.Validator.ValidateAcceptedTurnRawMortalItemMaterializationAsync());
        await using var lease = await context.FileSystem.AcquireCanonicalWriteLeaseAsync();
        var recorded = await context.Validator.CaptureSpiritualOriginalTurnWithIntakeAsync(lease);
        AssertNoConflictFrameErrors(recorded.Issues);
        using var capture = Assert.IsType<ValidationService.SpiritualOriginalTurnCapture>(recorded.Capture);
        var inputs = Assert.IsType<SpiritualOriginalDraftInputs>(OriginalCaptureField(capture, "_draftInputs"));
        var oldOwner = OriginalCaptureField(capture, "_allocations");
        Assert.NotNull(oldOwner);
        var oldItems = Assert.IsAssignableFrom<MortalItemIdentityFactory>(oldOwner.GetType()
            .GetProperty("Items", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(oldOwner));
        capture.Dispose();
        MortalItemAcceptedTurnAuthority.InvalidateValidatedItems(context.FileSystem, lease, oldItems);

        var ownerType = typeof(ValidationService).GetNestedType("SpiritualOriginalAllocationOwner",
            BindingFlags.NonPublic);
        Assert.NotNull(ownerType);
        var method = typeof(ValidationService).GetMethod(
            "ValidateSpiritualOriginalLocationItemIntakeAsync", BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(method);
        var owner = Activator.CreateInstance(ownerType, BindingFlags.Instance | BindingFlags.NonPublic,
            binder: null, args: ["[]", false, true], culture: null);
        Assert.NotNull(owner);
        var clock = Assert.IsType<SpiritualWoundProjectionClock>(ownerType.GetProperty("Clock",
            BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(owner));
        var journal = Assert.IsType<SpiritualWoundReplayJournal>(ownerType.GetProperty("Journal",
            BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(owner));
        var overrideField = typeof(ValidationService).GetField(
            "_prevalidatedPendingTurnSnapshotOverride", BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(overrideField);
        var overrideMarker = RuntimeHelpers.GetUninitializedObject(overrideField.FieldType);
        using (var scope = clock.BeginSpeculation())
        {
            overrideField.SetValue(context.Validator, overrideMarker);
            try
            {
                var blocked = Assert.IsAssignableFrom<Task<IReadOnlyList<ValidationIssue>>>(
                    method.Invoke(context.Validator, [lease, inputs, owner, scope]));
                Assert.Contains(await blocked,
                    issue => issue.Code == "spiritual_original_intake_claim_conflict");
                Assert.Same(overrideMarker, overrideField.GetValue(context.Validator));
            }
            finally
            {
                overrideField.SetValue(context.Validator, null);
            }
        }
        Assert.Empty(journal.Export());

        var nullScopeTask = Assert.IsAssignableFrom<Task<IReadOnlyList<ValidationIssue>>>(
            method.Invoke(context.Validator, [lease, inputs, owner, null]));
        await Assert.ThrowsAsync<ArgumentNullException>(() => nullScopeTask);
        var foreignScopeOwner = Activator.CreateInstance(ownerType,
            BindingFlags.Instance | BindingFlags.NonPublic,
            binder: null, args: ["[]", false, true], culture: null);
        Assert.NotNull(foreignScopeOwner);
        var foreignClock = Assert.IsType<SpiritualWoundProjectionClock>(ownerType.GetProperty("Clock",
            BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(foreignScopeOwner));
        using (var foreignScope = foreignClock.BeginSpeculation())
        {
            var blocked = Assert.IsAssignableFrom<Task<IReadOnlyList<ValidationIssue>>>(
                method.Invoke(context.Validator, [lease, inputs, owner, foreignScope]));
            await Assert.ThrowsAsync<InvalidOperationException>(() => blocked);
        }
        var disposed = clock.BeginSpeculation();
        disposed.Dispose();
        var disposedTask = Assert.IsAssignableFrom<Task<IReadOnlyList<ValidationIssue>>>(
            method.Invoke(context.Validator, [lease, inputs, owner, disposed]));
        await Assert.ThrowsAsync<InvalidOperationException>(() => disposedTask);

        var absent = Activator.CreateInstance(ownerType, BindingFlags.Instance | BindingFlags.NonPublic,
            binder: null, args: ["[]", false, false], culture: null);
        Assert.NotNull(absent);
        var absentClock = Assert.IsType<SpiritualWoundProjectionClock>(ownerType.GetProperty("Clock",
            BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(absent));
        using (var absentScope = absentClock.BeginSpeculation())
        {
            var blocked = Assert.IsAssignableFrom<Task<IReadOnlyList<ValidationIssue>>>(
                method.Invoke(context.Validator, [lease, inputs, absent, absentScope]));
            await Assert.ThrowsAsync<InvalidOperationException>(() => blocked);
        }

        var faulted = Activator.CreateInstance(ownerType, BindingFlags.Instance | BindingFlags.NonPublic,
            binder: null, args: ["[]", false, true], culture: null);
        Assert.NotNull(faulted);
        var faultedJournal = Assert.IsType<SpiritualWoundReplayJournal>(ownerType.GetProperty("Journal",
            BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(faulted));
        faultedJournal.Invalidate();
        using (var scope = clock.BeginSpeculation())
        {
            var blocked = Assert.IsAssignableFrom<Task<IReadOnlyList<ValidationIssue>>>(
                method.Invoke(context.Validator, [lease, inputs, faulted, scope]));
            await Assert.ThrowsAsync<InvalidOperationException>(() => blocked);
        }

        await using (var foreign = await ResourceMaterializationTestContext.CreateAsync())
        {
            var expired = await foreign.FileSystem.AcquireCanonicalWriteLeaseAsync();
            await expired.DisposeAsync();
            using var scope = clock.BeginSpeculation();
            var blocked = Assert.IsAssignableFrom<Task<IReadOnlyList<ValidationIssue>>>(
                method.Invoke(context.Validator, [expired, inputs, owner, scope]));
            await Assert.ThrowsAsync<InvalidOperationException>(() => blocked);
        }

        using (var committed = clock.BeginSpeculation())
        {
            committed.Commit();
            var blocked = Assert.IsAssignableFrom<Task<IReadOnlyList<ValidationIssue>>>(
                method.Invoke(context.Validator, [lease, inputs, owner, committed]));
            await Assert.ThrowsAsync<InvalidOperationException>(() => blocked);
        }
        Assert.Empty(journal.Export());
        Assert.True(journal.IsHealthy);
    }

    /// <summary>
    /// Rejects named intake before any revocation when the physical root has no session generation.
    /// </summary>
    [Fact]
    public async Task OriginalSpiritualCandidateReadView_MissingGenerationDoesNotCreateOne()
    {
        await using var context = await CreateCompleteConflictFrameContextAsync(
            seedOriginalInputs: SeedOriginalIntakeBaselinesAsync);
        await WriteCompleteConflictFrameExchangeAsync(context);
        await WriteOriginalIntakeDraftAsync(context);
        await using var lease = await context.FileSystem.AcquireCanonicalWriteLeaseAsync();
        File.Delete(context.FileSystem.SessionGenerationPath);
        Assert.Null(context.FileSystem.ReadExistingSessionGeneration(lease));
        var paths = context.FileSystem.EnumerateFiles(lease, "*").Order(StringComparer.Ordinal).ToArray();

        var rejected = await context.Validator.CaptureSpiritualOriginalTurnWithIntakeAsync(lease);

        Assert.Null(rejected.Capture);
        Assert.Contains(rejected.Issues, issue => issue.Code == "spiritual_original_intake_claim_conflict");
        Assert.Null(context.FileSystem.ReadExistingSessionGeneration(lease));
        Assert.Equal(paths, context.FileSystem.EnumerateFiles(lease, "*").Order(StringComparer.Ordinal));
    }

    /// <summary>
    /// Accepts an existing physical generation without a slot and rejects a stale slot without replacing it.
    /// </summary>
    [Fact]
    public async Task OriginalSpiritualCandidateReadView_GenerationProbeDoesNotRebindStaleSlot()
    {
        await using var context = await ResourceMaterializationTestContext.CreateAsync();
        var factory = new MortalItemIdentityFactory();
        string initial;
        await using (var lease = await context.FileSystem.AcquireCanonicalWriteLeaseAsync())
        {
            initial = context.FileSystem.GetOrCreateSessionGeneration(lease);
            var revision = context.FileSystem.CanonicalRootAuthorityIdentity.SessionGenerationRevision;
            Assert.True(AcceptedTurnAuthorityRegistry.CanBeginSpiritualOriginalIntake(
                context.FileSystem, lease, factory, checkPlansAndItems: true));
            Assert.Equal(revision, context.FileSystem.CanonicalRootAuthorityIdentity.SessionGenerationRevision);
            Assert.Equal(initial, context.FileSystem.ReadExistingSessionGeneration(lease));
            Assert.False(MortalItemAcceptedTurnAuthority.HasValidatedItems(context.FileSystem, lease));
            Assert.True(AcceptedTurnAuthorityRegistry.CanBeginSpiritualOriginalIntake(
                context.FileSystem, lease, factory, checkPlansAndItems: true));
        }

        string rotated;
        await using (var lifecycle = await context.FileSystem.AcquireSessionLifecycleLeaseAsync())
        await using (var replacement = await context.FileSystem.AcquireSessionReplacementWriteLeaseAsync(lifecycle))
            rotated = context.FileSystem.RotateSessionGeneration(replacement);
        await using var currentLease = await context.FileSystem.AcquireCanonicalWriteLeaseAsync();
        var rotatedRevision = context.FileSystem.CanonicalRootAuthorityIdentity.SessionGenerationRevision;
        Assert.NotEqual(initial, rotated);
        Assert.False(AcceptedTurnAuthorityRegistry.CanBeginSpiritualOriginalIntake(
            context.FileSystem, currentLease, factory, checkPlansAndItems: true));
        Assert.Equal(rotatedRevision, context.FileSystem.CanonicalRootAuthorityIdentity.SessionGenerationRevision);
        Assert.Equal(rotated, context.FileSystem.ReadExistingSessionGeneration(currentLease));
    }

    /// <summary>
    /// Retains a real transfer and player-removal projection when the physical commands change.
    /// </summary>
    [Fact]
    public async Task OriginalSpiritualCandidateReadView_AdmitsRetainedTransferCommands()
    {
        await using var context = await CreateCompleteConflictFrameContextAsync(
            seedOriginalInputs: SeedCandidateTransferBaselinesAsync);
        await WriteCompleteConflictFrameExchangeAsync(context);
        await WriteOriginalIntakeDraftAsync(context);
        var transferredItem = MortalItemTestFixture.CreateCanonicalRoot(CandidateTransferItemId);
        var removals = new JsonObject
        {
            ["removeInventoryItems"] = new JsonArray(new JsonObject
            {
                ["removedItemId"] = CandidateTransferItemId,
                ["itemName"] = transferredItem["name"]!.GetValue<string>(),
                ["currentContentsPath"] = null
            })
        };
        await context.WriteExactJsonAsync(MortalItemAcceptedTransferCatalog.PlayerRemovalPath,
            removals.ToJsonString());
        var npcCommands = new JsonObject
        {
            ["NPCInventoryAdds"] = new JsonArray(new JsonObject
            {
                ["NPCId"] = CandidateTransferNpcId,
                ["NPCName"] = "Получающий NPC",
                ["item"] = transferredItem.DeepClone(),
                ["destinationContainerId"] = null
            })
        };
        await context.WriteExactJsonAsync(MortalItemAcceptedTransferCatalog.NpcCommandsPath,
            npcCommands.ToJsonString());
        AssertNoConflictFrameErrors(await context.Validator.ValidateAcceptedTurnRawMortalLocationMaterializationAsync());
        AssertNoConflictFrameErrors(await context.Validator.ValidateAcceptedTurnRawMortalItemMaterializationAsync());
        await using var lease = await context.FileSystem.AcquireCanonicalWriteLeaseAsync();
        var recorded = await context.Validator.CaptureSpiritualOriginalTurnWithIntakeAsync(lease);
        AssertNoConflictFrameErrors(recorded.Issues);
        using var capture = Assert.IsType<ValidationService.SpiritualOriginalTurnCapture>(recorded.Capture);
        var originalInputs = Assert.IsType<SpiritualOriginalDraftInputs>(OriginalCaptureField(capture, "_draftInputs"));
        var rows = capture.ReadAllocationJournal(lease).ToJsonString();
        var oldOwner = OriginalCaptureField(capture, "_allocations");
        Assert.NotNull(oldOwner);
        var oldItems = Assert.IsAssignableFrom<MortalItemIdentityFactory>(oldOwner.GetType()
            .GetProperty("Items", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(oldOwner));
        capture.Dispose();
        MortalItemAcceptedTurnAuthority.InvalidateValidatedItems(context.FileSystem, lease, oldItems);

        const string changedPhysical = "{changed-transfer-command";
        await context.FileSystem.WriteFileAtomicAsync(lease,
            MortalItemAcceptedTransferCatalog.PlayerRemovalPath, changedPhysical);
        await context.FileSystem.WriteFileAtomicAsync(lease,
            MortalItemAcceptedTransferCatalog.NpcCommandsPath, changedPhysical);
        await context.FileSystem.WriteFileAtomicAsync(lease,
            NpcCoreChangesContract.NpcCorePath, changedPhysical);
        await context.FileSystem.WriteFileAtomicAsync(lease,
            FactionCoreChangesContract.FactionCorePath, changedPhysical);
        await context.FileSystem.WriteFileAtomicAsync(lease,
            MortalBootstrapLocationScaffold.StatePath, changedPhysical);
        var ownerType = typeof(ValidationService).GetNestedType("SpiritualOriginalAllocationOwner",
            BindingFlags.NonPublic);
        Assert.NotNull(ownerType);
        var owner = Activator.CreateInstance(ownerType, BindingFlags.Instance | BindingFlags.NonPublic,
            binder: null, args: [rows, true, true], culture: null);
        Assert.NotNull(owner);
        var clock = Assert.IsType<SpiritualWoundProjectionClock>(ownerType.GetProperty("Clock",
            BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(owner));
        using var scope = clock.BeginSpeculation();
        var method = typeof(ValidationService).GetMethod(
            "ValidateSpiritualOriginalLocationItemIntakeAsync", BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(method);
        var task = Assert.IsAssignableFrom<Task<IReadOnlyList<ValidationIssue>>>(
            method.Invoke(context.Validator, [lease, originalInputs, owner, scope]));
        AssertNoConflictFrameErrors(await task);
        scope.Commit();

        var slots = ReadStaticField(typeof(AcceptedTurnAuthorityRegistry), "RootSlots");
        var slot = ReadWeakValue(slots, context.FileSystem.CanonicalRootAuthorityIdentity);
        var cache = ReadField(ReadField(slot, "_state"), "_mortalItems");
        var transfers = Assert.IsAssignableFrom<IReadOnlyList<MortalItemAcceptedTransfer>>(
            ReadField(cache, "_transfers"));
        Assert.Contains(transfers, transfer => transfer.ItemId == CandidateTransferItemId);
        var replaced = Assert.IsAssignableFrom<IReadOnlySet<EffectSourceOwnerKey>>(
            ReadField(cache, "_replacedSourceOwners"));
        Assert.Contains(new EffectSourceOwnerKey("mortal_world", "item", CandidateTransferItemId), replaced);
        var currentRoots = Assert.IsAssignableFrom<IReadOnlyDictionary<string, JsonNode?>>(
            ReadField(cache, "_currentProjectionRoots"));
        Assert.True(JsonNode.DeepEquals(removals,
            currentRoots[MortalItemAcceptedTransferCatalog.PlayerRemovalPath]));
        Assert.True(JsonNode.DeepEquals(npcCommands,
            currentRoots[MortalItemAcceptedTransferCatalog.NpcCommandsPath]));
        Assert.True(JsonNode.DeepEquals(
            JsonNode.Parse(originalInputs.ReadText(NpcCoreChangesContract.NpcCorePath)!),
            currentRoots[NpcCoreChangesContract.NpcCorePath]));
        Assert.Equal(changedPhysical,
            await context.FileSystem.ReadFileAsync(lease,
                MortalItemAcceptedTransferCatalog.PlayerRemovalPath));
    }

    /// <summary>
    /// Rejects a named capture when a physical current root changes after its first retained read.
    /// </summary>
    [Fact]
    public async Task OriginalSpiritualCandidateReadView_NamedCaptureRejectsMidReadChange()
    {
        const string changedPhysical = "{changed-during-named-capture";
        const string watchedPath = "game_state/inventory/item_bonds.json";
        ResourceMaterializationTestContext? context = null;
        var armed = false;
        var watchedReads = 0;
        var hooks = new FileSystemManagerHooks
        {
            BeforeCanonicalReadOpenAsync = path =>
            {
                if (armed && string.Equals(path.Replace('\\', '/'), watchedPath,
                        StringComparison.Ordinal))
                {
                    watchedReads++;
                    if (watchedReads == 2)
                        File.WriteAllText(context!.FileSystem.ResolvePath(watchedPath), changedPhysical);
                }
                return Task.CompletedTask;
            }
        };
        await using (context = await CreateCompleteConflictFrameContextAsync(
                         hooks, SeedOriginalIntakeBaselinesAsync))
        {
            await WriteCompleteConflictFrameExchangeAsync(context);
            await WriteOriginalIntakeDraftAsync(context);
            AssertNoConflictFrameErrors(await context.Validator.ValidateAcceptedTurnRawMortalLocationMaterializationAsync());
            AssertNoConflictFrameErrors(await context.Validator.ValidateAcceptedTurnRawMortalItemMaterializationAsync());
            await using var lease = await context.FileSystem.AcquireCanonicalWriteLeaseAsync();
            armed = true;

            var rejected = await context.Validator.CaptureSpiritualOriginalTurnWithIntakeAsync(lease);

            Assert.True(watchedReads >= 2);
            Assert.Null(rejected.Capture);
            Assert.Contains(rejected.Issues,
                issue => issue.Code == "spiritual_original_input_changed" &&
                         issue.FilePath == watchedPath);
            Assert.Equal(changedPhysical,
                await context.FileSystem.ReadFileAsync(lease, watchedPath));
        }
    }

    /// <summary>
    /// Rejects named intake before revoking an item fence when the receipt field is occupied.
    /// </summary>
    [Fact]
    public async Task OriginalSpiritualCandidateReadView_NamedPreflightPreservesOccupiedReceiptField()
    {
        await using var context = await CreateCompleteConflictFrameContextAsync(
            seedOriginalInputs: SeedOriginalIntakeBaselinesAsync);
        await WriteCompleteConflictFrameExchangeAsync(context);
        await WriteOriginalIntakeDraftAsync(context);
        AssertNoConflictFrameErrors(await context.Validator.ValidateAcceptedTurnRawMortalLocationMaterializationAsync());
        AssertNoConflictFrameErrors(await context.Validator.ValidateAcceptedTurnRawMortalItemMaterializationAsync());
        await using var lease = await context.FileSystem.AcquireCanonicalWriteLeaseAsync();
        var slots = ReadStaticField(typeof(AcceptedTurnAuthorityRegistry), "RootSlots");
        var slot = ReadWeakValue(slots, context.FileSystem.CanonicalRootAuthorityIdentity);
        var state = ReadField(slot, "_state");
        var itemCache = ReadField(state, "_mortalItems");
        var receiptField = state.GetType().GetField("_openTreatmentPublicationReceipt",
            BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(receiptField);
        var marker = RuntimeHelpers.GetUninitializedObject(receiptField.FieldType);
        receiptField.SetValue(state, marker);
        var fence = ReadField(itemCache, "_validatedFence");
        var paths = context.FileSystem.EnumerateFiles(lease, "*").Order(StringComparer.Ordinal).ToArray();
        Assert.True(MortalItemAcceptedTurnAuthority.HasValidatedItems(context.FileSystem, lease));

        var rejected = await context.Validator.CaptureSpiritualOriginalTurnWithIntakeAsync(lease);

        Assert.Null(rejected.Capture);
        Assert.Contains(rejected.Issues, issue => issue.Code == "spiritual_original_intake_claim_conflict");
        Assert.Same(marker, receiptField.GetValue(state));
        Assert.Same(fence, ReadField(itemCache, "_validatedFence"));
        Assert.True(MortalItemAcceptedTurnAuthority.HasValidatedItems(context.FileSystem, lease));
        Assert.Equal(paths, context.FileSystem.EnumerateFiles(lease, "*").Order(StringComparer.Ordinal));
    }

    /// <summary>
    /// Seeds a governed absent item before the signed snapshot fixes client-owned identity state.
    /// </summary>
    /// <param name="context">
    /// Fixture preparing the initial signed location and item baselines.
    /// </param>
    private static async Task SeedCandidateQuestBaselinesAsync(ResourceMaterializationTestContext context)
    {
        await SeedOriginalIntakeBaselinesAsync(context);
        await context.WriteExactJsonAsync(MortalItemIdentityState.StatePath,
            MortalItemTestFixture.CreateIndex(
                MortalItemTestFixture.CreateCanonicalRoot(CandidateMissingItemId)).ToJsonString());
    }

    /// <summary>
    /// Seeds a canonical item and receiving NPC before signing the original pre-turn snapshot.
    /// </summary>
    /// <param name="context">
    /// Owned resource fixture preparing the signed transfer baseline.
    /// </param>
    private static async Task SeedCandidateTransferBaselinesAsync(ResourceMaterializationTestContext context)
    {
        await SeedOriginalIntakeBaselinesAsync(context);
        var item = MortalItemTestFixture.CreateCanonicalRoot(CandidateTransferItemId);
        var inventory = Assert.IsType<JsonObject>(await context.ReadJsonAsync(InventoryEquipmentService.ItemsPath));
        inventory["items"] = new JsonArray(item.DeepClone());
        await context.WriteExactJsonAsync(InventoryEquipmentService.ItemsPath, inventory.ToJsonString());
        await context.WriteExactJsonAsync(MortalItemIdentityState.StatePath,
            MortalItemTestFixture.CreateIndex(item).ToJsonString());
        await context.WriteExactJsonAsync(NpcCoreChangesContract.NpcCorePath,
            new JsonObject
            {
                ["NPCsInScene"] = new JsonArray(new JsonObject
                {
                    ["NPCId"] = CandidateTransferNpcId,
                    ["name"] = "Получающий NPC",
                    ["inventory"] = new JsonArray(),
                    ["equippedItems"] = new JsonObject()
                })
            }.ToJsonString());
        await context.WriteExactJsonAsync(MortalBootstrapLocationScaffold.StatePath, "{}");
        await context.WriteExactJsonAsync(FactionCoreChangesContract.FactionCorePath, "{}");
    }

    /// <summary>
    /// Reads one private static cache table for direct result inspection.
    /// </summary>
    /// <param name="owner">
    /// Type declaring the static table.
    /// </param>
    /// <param name="name">
    /// Exact private field name.
    /// </param>
    /// <returns>
    /// Existing table instance.
    /// </returns>
    private static object ReadStaticField(Type owner, string name)
    {
        var field = owner.GetField(name, BindingFlags.Static | BindingFlags.NonPublic);
        Assert.NotNull(field);
        var value = field.GetValue(null);
        Assert.NotNull(value);
        return value;
    }

    /// <summary>
    /// Reads an existing entry from a private weak cache without creating one.
    /// </summary>
    /// <param name="table">
    /// Private weak table under inspection.
    /// </param>
    /// <param name="key">
    /// Exact filesystem or root identity used by that table.
    /// </param>
    /// <returns>
    /// Existing cache entry.
    /// </returns>
    private static object ReadWeakValue(object table, object key)
    {
        var lookup = table.GetType().GetMethod("TryGetValue");
        Assert.NotNull(lookup);
        object?[] args = [key, null];
        Assert.True(Assert.IsType<bool>(lookup.Invoke(table, args)));
        Assert.NotNull(args[1]);
        return args[1]!;
    }

    /// <summary>
    /// Reads a private cache result or authority claim for verification.
    /// </summary>
    /// <param name="owner">
    /// Object containing the existing field.
    /// </param>
    /// <param name="name">
    /// Exact private field name.
    /// </param>
    /// <returns>
    /// Non-null field value.
    /// </returns>
    private static object ReadField(object owner, string name)
    {
        var field = owner.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(field);
        var value = field.GetValue(owner);
        Assert.NotNull(value);
        return value;
    }
}
