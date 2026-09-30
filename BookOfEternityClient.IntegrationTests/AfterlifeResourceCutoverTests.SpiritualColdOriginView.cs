using System.Text.Json.Nodes;
using BookOfEternityClient.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed partial class AfterlifeResourceCutoverTests
{
    /// <summary>
    /// Reconstructs original owners from checkpoint A while later physical draft and transport files differ.
    /// </summary>
    [Fact]
    public async Task SpiritualColdOriginView_ReplaysOriginalOwnersWhilePhysicalDraftHasChanged()
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
        using var first = Assert.IsType<ValidationService.SpiritualOriginalTurnCapture>(recorded.Capture);
        AssertNoConflictFrameErrors(await first.BeginResourceExecutionAsync(lease));
        AssertNoConflictFrameErrors((await first.AdvanceNextResourceExchangeAsync(lease)).Issues);
        var warmImages = first.ReadC1ImageInventory(lease);
        Assert.False(warmImages.BeforeImages[SpiritualWoundCaptureCheckpointState.StatePath].Existed);
        var originalInputs = Assert.IsType<SpiritualOriginalDraftInputs>(OriginalCaptureField(first, "_draftInputs"));
        var physicalWitnesses = Assert.IsAssignableFrom<IReadOnlyDictionary<string, CanonicalBeforeImage>>(
            OriginalCaptureField(first, "_physicalWitnesses"));
        var allocationRows = first.ReadAllocationJournal(lease);
        var checkpoint = CreateColdOriginCheckpoint(originalInputs, physicalWitnesses, allocationRows);
        var warmSource = Assert.IsType<ValidationService.SpiritualWoundSourceSession>(
            OriginalCaptureField(first, "_source"));
        Assert.Throws<InvalidOperationException>(() => warmSource.BindColdOriginalInputs(lease, first));
        first.Dispose();

        const string laterDraft = "{malformed-current-draft";
        await context.FileSystem.WriteFileAtomicAsync(lease,
            SpiritualWoundCaptureCheckpointState.StatePath,
            SpiritualWoundCaptureCheckpointState.SerializeCanonical(checkpoint));
        foreach (var path in new[]
                 {
                     InventoryEquipmentService.ItemsPath,
                     MortalLocationMaterializationContract.WorldMapPath,
                     ResourceMaterializationContract.StatePath,
                     AfterlifeSpiritualConflictState.StatePath,
                     EffectAcceptedTurnPlan.CommandPath
                 })
            await context.FileSystem.WriteFileAtomicAsync(lease, path, laterDraft);
        const string laterAlias = "Lore/later_candidate.json";
        var canonicalLore = Path.Combine(context.FileSystem.GameSessionPath, "lore");
        var stagedLore = Path.Combine(context.FileSystem.GameSessionPath, "lore_case_stage");
        var aliasedLore = Path.Combine(context.FileSystem.GameSessionPath, "Lore");
        if (Directory.Exists(canonicalLore))
        {
            Directory.Move(canonicalLore, stagedLore);
            Directory.Move(stagedLore, aliasedLore);
        }
        else
            Directory.CreateDirectory(aliasedLore);
        var laterAliasPhysical = Path.Combine(aliasedLore, "later_candidate.json");
        await File.WriteAllTextAsync(laterAliasPhysical, "{}");
        Assert.Contains(laterAlias, context.FileSystem.EnumerateFiles(lease, "*"));
        var inventoryBefore = await context.FileSystem.ReadFileBytesAsync(lease, InventoryEquipmentService.ItemsPath);
        var mapBefore = await context.FileSystem.ReadFileBytesAsync(lease,
            MortalLocationMaterializationContract.WorldMapPath);
        var checkpointBefore = await context.FileSystem.ReadFileBytesAsync(lease,
            SpiritualWoundCaptureCheckpointState.StatePath);
        var conflictBefore = await context.FileSystem.ReadFileBytesAsync(lease,
            AfterlifeSpiritualConflictState.StatePath);
        var resourceBefore = await context.FileSystem.ReadFileBytesAsync(lease,
            ResourceMaterializationContract.StatePath);
        var effectBefore = await context.FileSystem.ReadFileBytesAsync(lease,
            EffectAcceptedTurnPlan.CommandPath);
        var aliasBefore = await File.ReadAllBytesAsync(laterAliasPhysical);
        var fresh = new ValidationService(context.FileSystem, NullLogger<ValidationService>.Instance);
        var replayed = await fresh.CaptureSpiritualOriginalTurnFromCheckpointOriginAsync(lease, checkpoint);
        AssertNoConflictFrameErrors(replayed.Issues);
        using var second = Assert.IsType<ValidationService.SpiritualOriginalTurnCapture>(replayed.Capture);
        var coldSource = Assert.IsType<ValidationService.SpiritualWoundSourceSession>(
            OriginalCaptureField(second, "_source"));
        Assert.Throws<InvalidOperationException>(() => coldSource.BindColdOriginalInputs(lease, second));
        Assert.Throws<InvalidOperationException>(() => coldSource.BindColdOriginalInputs(lease, first));
        AssertNoConflictFrameErrors(await second.BeginResourceExecutionAsync(lease));
        AssertNoConflictFrameErrors((await second.AdvanceNextResourceExchangeAsync(lease)).Issues);
        var coldImages = second.ReadC1ImageInventory(lease);
        Assert.Equal(warmImages.BeforeImages[SpiritualWoundCaptureCheckpointState.StatePath].Fingerprint,
            coldImages.BeforeImages[SpiritualWoundCaptureCheckpointState.StatePath].Fingerprint);
        Assert.True(JsonNode.DeepEquals(allocationRows, second.ReadAllocationJournal(lease)));
        Assert.Equal(inventoryBefore,
            await context.FileSystem.ReadFileBytesAsync(lease, InventoryEquipmentService.ItemsPath));
        Assert.Equal(mapBefore,
            await context.FileSystem.ReadFileBytesAsync(lease,
                MortalLocationMaterializationContract.WorldMapPath));
        Assert.Equal(checkpointBefore,
            await context.FileSystem.ReadFileBytesAsync(lease,
                SpiritualWoundCaptureCheckpointState.StatePath));
        Assert.Equal(conflictBefore,
            await context.FileSystem.ReadFileBytesAsync(lease,
                AfterlifeSpiritualConflictState.StatePath));
        Assert.Equal(resourceBefore,
            await context.FileSystem.ReadFileBytesAsync(lease,
                ResourceMaterializationContract.StatePath));
        Assert.Equal(effectBefore,
            await context.FileSystem.ReadFileBytesAsync(lease,
                EffectAcceptedTurnPlan.CommandPath));
        Assert.Equal(aliasBefore, await File.ReadAllBytesAsync(laterAliasPhysical));
        Assert.False(AcceptedMechanicsPlanAuthority.HasValidated(context.FileSystem, lease));
    }

    /// <summary>
    /// Rejects a forged immutable checkpoint witness before any new owner is registered.
    /// </summary>
    [Fact]
    public async Task SpiritualColdOriginView_RejectsChangedImmutableWitnessBeforeOwnerAdmission()
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
        using var first = Assert.IsType<ValidationService.SpiritualOriginalTurnCapture>(recorded.Capture);
        var originalInputs = Assert.IsType<SpiritualOriginalDraftInputs>(OriginalCaptureField(first, "_draftInputs"));
        var physicalWitnesses = Assert.IsAssignableFrom<IReadOnlyDictionary<string, CanonicalBeforeImage>>(
            OriginalCaptureField(first, "_physicalWitnesses"));
        var checkpoint = CreateColdOriginCheckpoint(originalInputs, physicalWitnesses,
            first.ReadAllocationJournal(lease), witnessFingerprintOverride: "sha256:" + new string('b', 64));
        first.Dispose();

        var fresh = new ValidationService(context.FileSystem, NullLogger<ValidationService>.Instance);
        var rejected = await fresh.CaptureSpiritualOriginalTurnFromCheckpointOriginAsync(lease, checkpoint);
        Assert.Null(rejected.Capture);
        Assert.Contains(rejected.Issues,
            issue => issue.Code == "spiritual_checkpoint_origin_mismatch" &&
                     issue.FilePath == PendingTurnSnapshotAuthority.AuthorityPath);
        Assert.False(AcceptedMechanicsPlanAuthority.HasValidated(context.FileSystem, lease));
        Assert.False(MortalItemAcceptedTurnAuthority.HasValidatedItems(context.FileSystem, lease));
    }

    /// <summary>
    /// Rejects a checkpoint whose realm differs from the reopened signed snapshot.
    /// </summary>
    [Fact]
    public async Task SpiritualColdOriginView_RejectsOppositeRealmBeforeOwnerAdmission()
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
        using var first = Assert.IsType<ValidationService.SpiritualOriginalTurnCapture>(recorded.Capture);
        var originalInputs = Assert.IsType<SpiritualOriginalDraftInputs>(OriginalCaptureField(first, "_draftInputs"));
        var physicalWitnesses = Assert.IsAssignableFrom<IReadOnlyDictionary<string, CanonicalBeforeImage>>(
            OriginalCaptureField(first, "_physicalWitnesses"));
        var checkpoint = CreateColdOriginCheckpoint(originalInputs, physicalWitnesses,
            first.ReadAllocationJournal(lease), realm: "shining_abode");
        first.Dispose();

        var fresh = new ValidationService(context.FileSystem, NullLogger<ValidationService>.Instance);
        var rejected = await fresh.CaptureSpiritualOriginalTurnFromCheckpointOriginAsync(lease, checkpoint);
        Assert.Null(rejected.Capture);
        Assert.Contains(rejected.Issues, issue => issue.Code == "spiritual_checkpoint_origin_mismatch");
        Assert.False(AcceptedMechanicsPlanAuthority.HasValidated(context.FileSystem, lease));
        Assert.False(MortalItemAcceptedTurnAuthority.HasValidatedItems(context.FileSystem, lease));
    }

    /// <summary>
    /// Preserves a foreign validated item claim when cold origin preflight declines admission.
    /// </summary>
    [Fact]
    public async Task SpiritualColdOriginView_PreservesForeignItemClaimOnPreflightRejection()
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
        using var first = Assert.IsType<ValidationService.SpiritualOriginalTurnCapture>(recorded.Capture);
        var originalInputs = Assert.IsType<SpiritualOriginalDraftInputs>(OriginalCaptureField(first, "_draftInputs"));
        var physicalWitnesses = Assert.IsAssignableFrom<IReadOnlyDictionary<string, CanonicalBeforeImage>>(
            OriginalCaptureField(first, "_physicalWitnesses"));
        var checkpoint = CreateColdOriginCheckpoint(originalInputs, physicalWitnesses,
            first.ReadAllocationJournal(lease));
        first.Dispose();
        AssertNoConflictFrameErrors(await context.Validator.ValidateAcceptedTurnRawMortalItemMaterializationAsync(lease));
        Assert.True(MortalItemAcceptedTurnAuthority.HasValidatedItems(context.FileSystem, lease));

        var fresh = new ValidationService(context.FileSystem, NullLogger<ValidationService>.Instance);
        var rejected = await fresh.CaptureSpiritualOriginalTurnFromCheckpointOriginAsync(lease, checkpoint);
        Assert.Null(rejected.Capture);
        Assert.Contains(rejected.Issues, issue => issue.Code == "spiritual_original_intake_claim_conflict");
        Assert.True(MortalItemAcceptedTurnAuthority.HasValidatedItems(context.FileSystem, lease));
    }

    /// <summary>
    /// Builds structurally valid private comparison evidence from one recorded original capture.
    /// </summary>
    /// <param name="originalInputs">
    /// Exact retained draft A and its authenticated identity.
    /// </param>
    /// <param name="physicalWitnesses">
    /// Physical witnesses observed by the original capture.
    /// </param>
    /// <param name="allocations">
    /// Retained allocation rows for strict owner replay.
    /// </param>
    /// <param name="witnessFingerprintOverride">
    /// Optional authority witness fingerprint for rejection testing.
    /// </param>
    /// <param name="realm">
    /// Checkpoint realm declaration; defaults to the signed Chaos Sea fixture realm.
    /// </param>
    /// <returns>
    /// Parsed detached checkpoint evidence without publication authority.
    /// </returns>
    private static SpiritualWoundCaptureCheckpointState CreateColdOriginCheckpoint(
        SpiritualOriginalDraftInputs originalInputs,
        IReadOnlyDictionary<string, CanonicalBeforeImage> physicalWitnesses,
        JsonArray allocations, string? witnessFingerprintOverride = null, string realm = "chaos_sea")
    {
        var witnessPaths = new[]
        {
            PendingTurnSnapshotAuthority.AuthorityPath,
            LiveTurnPreparationService.PendingTurnSnapshotManifestPath,
            LiveTurnPreparationService.TurnRequestPath
        };
        var packetFingerprint = "sha256:" + new string('a', 64);
        var checkpoint = new JsonObject
        {
            ["sessionId"] = originalInputs.SessionId,
            ["requestId"] = originalInputs.RequestId,
            ["snapshotToken"] = originalInputs.SnapshotToken,
            ["turn"] = originalInputs.Turn,
            ["realm"] = realm,
            ["originalSnapshotFingerprint"] = "sha256:" + originalInputs.SnapshotToken.ToLowerInvariant(),
            ["originalDraftImages"] = new JsonArray(originalInputs.PathInventory
                .Select(path =>
                {
                    var image = originalInputs.ReadImage(path);
                    return new JsonObject
                    {
                        ["path"] = path,
                        ["existed"] = image.Existed,
                        ["contentBase64"] = image.Existed ? Convert.ToBase64String(image.Bytes!) : null,
                        ["contentFingerprint"] = image.Fingerprint
                    };
                }).ToArray()),
            ["physicalWitnesses"] = new JsonArray(witnessPaths.Select(path => new JsonObject
            {
                ["path"] = path,
                ["existed"] = true,
                ["contentFingerprint"] = path == PendingTurnSnapshotAuthority.AuthorityPath &&
                    witnessFingerprintOverride != null
                    ? witnessFingerprintOverride
                    : physicalWitnesses[path].Fingerprint
            }).ToArray()),
            ["initialAllocationCount"] = allocations.Count,
            ["initialPendingPacketFingerprint"] = packetFingerprint,
            ["advances"] = new JsonArray(),
            ["committedAdvance"] = 0,
            ["allocations"] = allocations.DeepClone(),
            ["expectedPendingPacketFingerprint"] = packetFingerprint,
            ["checkpointFingerprint"] = ""
        };
        checkpoint["checkpointFingerprint"] = SpiritualWoundStateJson.Hash(
            checkpoint, "spiritual_capture_checkpoint_v1", "checkpointFingerprint");
        var root = new JsonObject { ["schemaVersion"] = 1, ["checkpoint"] = checkpoint };
        var parsed = SpiritualWoundCaptureCheckpointState.Parse(root.ToJsonString(),
            SpiritualWoundCaptureCheckpointState.StatePath, originalInputs.PathInventory);
        Assert.True(parsed.IsValid);
        return Assert.IsType<SpiritualWoundCaptureCheckpointState>(parsed.State);
    }
}
