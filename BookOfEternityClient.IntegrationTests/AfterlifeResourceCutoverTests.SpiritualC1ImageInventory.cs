using BookOfEternityClient.Services;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed partial class AfterlifeResourceCutoverTests
{
    /// <summary>
    /// Keeps signed receipt A distinct from the physical candidate and rollback receipt B.
    /// </summary>
    [Fact]
    public async Task OriginalSpiritualC1SignedReceiptOrigin_PreservesExactAAndBImages()
    {
        const string receiptPath = SpiritualWoundOpportunityReceiptState.StatePath;
        const string signedA = """
            {"schemaVersion":1,"nextInstanceOrdinal":1,"nextClosureOrdinal":1,
            "nextSourceOrdinal":1,"nextDecisionOrdinal":1,"instances":[],
            "closures":[],"sources":[],"decisions":[]}
            """;
        const string physicalB = """
            { "schemaVersion": 1, "nextInstanceOrdinal": 1,
              "nextClosureOrdinal": 1, "nextSourceOrdinal": 1,
              "nextDecisionOrdinal": 1, "instances": [], "closures": [],
              "sources": [], "decisions": [] }
            """;
        await using var context = await CreateCompleteConflictFrameContextAsync(
            seedOriginalInputs: async fixture =>
            {
                await SeedOriginalIntakeBaselinesAsync(fixture);
                await fixture.WriteExactJsonAsync(receiptPath, signedA);
            });
        await context.WriteExactJsonAsync(receiptPath, physicalB);
        await WriteCompleteConflictFrameExchangeAsync(context);
        await WriteOriginalIntakeDraftAsync(context);
        AssertNoConflictFrameErrors(await context.Validator.ValidateAcceptedTurnRawMortalLocationMaterializationAsync());
        AssertNoConflictFrameErrors(await context.Validator.ValidateAcceptedTurnRawMortalItemMaterializationAsync());
        await using var lease = await context.FileSystem.AcquireCanonicalWriteLeaseAsync();
        var recorded = await context.Validator.CaptureSpiritualOriginalTurnWithIntakeAsync(lease);
        AssertNoConflictFrameErrors(recorded.Issues);
        using var capture = Assert.IsType<ValidationService.SpiritualOriginalTurnCapture>(recorded.Capture);

        var signed = capture.ReadVerifiedSignedC1Origin(lease);
        Assert.Equal(System.Text.Encoding.UTF8.GetBytes(signedA), signed.Receipt.Bytes);
        var rollback = capture.ReadC1ImageInventory(lease).BeforeImages[receiptPath];
        Assert.Equal(System.Text.Encoding.UTF8.GetBytes(physicalB), rollback.Bytes);
        Assert.NotEqual(signed.Receipt.Fingerprint, rollback.Fingerprint);

        await context.FileSystem.WriteFileAtomicBytesAsync(lease,
            LiveTurnPreparationService.PendingTurnSnapshotManifestPath,
            System.Text.Encoding.UTF8.GetBytes("{}"));
        Assert.Throws<InvalidOperationException>(() =>
            capture.ReadVerifiedSignedC1Origin(lease));
    }

    /// <summary>
    /// Requires explicit signed receipt absence and rejects a revoked named capture.
    /// </summary>
    [Fact]
    public async Task OriginalSpiritualC1SignedReceiptOrigin_PreservesSignedAbsence()
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

        var signed = capture.ReadVerifiedSignedC1Origin(lease);
        Assert.False(signed.Receipt.Existed);
        Assert.Null(signed.Receipt.Bytes);
        Assert.True(signed.Conflict.Existed);
        capture.Dispose();
        Assert.Throws<ObjectDisposedException>(() => capture.ReadVerifiedSignedC1Origin(lease));
    }

    /// <summary>
    /// Keeps legacy original capture executable without granting it C1 signed-service authority.
    /// </summary>
    [Fact]
    public async Task OriginalSpiritualC1ImageInventory_LegacyCaptureFailsClosed()
    {
        await using var context = await CreateCompleteConflictFrameContextAsync();
        await WriteCompleteConflictFrameExchangeAsync(context);
        await using var lease = await context.FileSystem.AcquireCanonicalWriteLeaseAsync();
        var recorded = await context.Validator.CaptureSpiritualOriginalTurnWithPrefixAsync(lease);
        AssertNoConflictFrameErrors(recorded.Issues);
        using var capture = Assert.IsType<ValidationService.SpiritualOriginalTurnCapture>(recorded.Capture);
        AssertNoConflictFrameErrors(await capture.BeginResourceExecutionAsync(lease));
        AssertNoConflictFrameErrors((await capture.AdvanceNextResourceExchangeAsync(lease)).Issues);
        Assert.Throws<InvalidOperationException>(() => capture.ReadC1ImageInventory(lease));
    }

    /// <summary>
    /// Retains the registered rollback paths from their original owners without exposing mutable aliases.
    /// </summary>
    [Fact]
    public async Task OriginalSpiritualC1ImageInventory_RetainsOwnerRollbackAndPrivateAbsence()
    {
        await using var context = await CreateCompleteConflictFrameContextAsync(
            seedOriginalInputs: SeedOriginalIntakeBaselinesAsync);
        await WriteCompleteConflictFrameExchangeAsync(context);
        const string dynamicPath = "game_state/custom/c1_registered_example.json";
        await context.WriteExactJsonAsync(dynamicPath, "{\"registered\":true}");
        await WriteOriginalIntakeDraftAsync(context);
        AssertNoConflictFrameErrors(await context.Validator.ValidateAcceptedTurnRawMortalLocationMaterializationAsync());
        AssertNoConflictFrameErrors(await context.Validator.ValidateAcceptedTurnRawMortalItemMaterializationAsync());
        await using var lease = await context.FileSystem.AcquireCanonicalWriteLeaseAsync();
        var recorded = await context.Validator.CaptureSpiritualOriginalTurnWithIntakeAsync(lease);
        AssertNoConflictFrameErrors(recorded.Issues);
        using var capture = Assert.IsType<ValidationService.SpiritualOriginalTurnCapture>(recorded.Capture);
        AssertNoConflictFrameErrors(await capture.BeginResourceExecutionAsync(lease));
        AssertNoConflictFrameErrors((await capture.AdvanceNextResourceExchangeAsync(lease)).Issues);

        var inventory = capture.ReadC1ImageInventory(lease);
        Assert.Contains(ResourceMaterializationContract.StatePath, inventory.RegisteredPaths);
        Assert.Contains(SpiritualWoundDecisionPendingState.StatePath, inventory.RegisteredPaths);
        Assert.Contains(SpiritualWoundCaptureCheckpointState.StatePath, inventory.RegisteredPaths);
        Assert.Contains(dynamicPath, inventory.RegisteredPaths);
        Assert.DoesNotContain(LiveTurnPreparationService.PendingTurnSnapshotManifestPath,
            inventory.RegisteredPaths);
        Assert.DoesNotContain(PendingTurnSnapshotAuthority.AuthorityPath, inventory.RegisteredPaths);
        Assert.DoesNotContain(LiveTurnPreparationService.TurnRequestPath, inventory.RegisteredPaths);
        var input = Assert.IsType<AcceptedMechanicsInput>(OriginalCaptureField(capture, "_input"));
        foreach (var pair in input.BeforeImages.Where(pair => inventory.RegisteredPaths.Contains(pair.Key)))
            Assert.Equal(pair.Value.Fingerprint, inventory.BeforeImages[pair.Key].Fingerprint);
        var signed = PendingTurnSnapshotReader.ReadCurrent(context.FileSystem, lease,
            [ValidationService.SpiritualWoundSourceSession.SoulPath,
                AfterlifeSpiritualConflictState.StatePath]);
        AssertNoConflictFrameErrors(signed.Issues);
        var signedConflict = Assert.IsType<PendingTurnSnapshotReadAuthority>(signed.Snapshot)
            .ReadRequiredBytes(AfterlifeSpiritualConflictState.StatePath);
        Assert.NotEqual(new CanonicalBeforeImage(true, signedConflict).Fingerprint,
            inventory.BeforeImages[AfterlifeSpiritualConflictState.StatePath].Fingerprint);
        Assert.False(inventory.BeforeImages[SpiritualWoundDecisionPendingState.StatePath].Existed);
        Assert.False(inventory.BeforeImages[SpiritualWoundCaptureCheckpointState.StatePath].Existed);
        Assert.Throws<NotSupportedException>(() =>
            ((IList<string>)inventory.RegisteredPaths)[0] = "changed");
        Assert.Throws<NotSupportedException>(() =>
            ((IDictionary<string, CanonicalBeforeImage>)inventory.BeforeImages)[
                ResourceMaterializationContract.StatePath] =
                inventory.BeforeImages[ResourceMaterializationContract.StatePath]);

        capture.Dispose();
        Assert.Throws<ObjectDisposedException>(() => capture.ReadC1ImageInventory(lease));
    }
}
