using System.Text;
using System.Text.Json.Nodes;
using BookOfEternityClient.Core;
using BookOfEternityClient.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed partial class AfterlifeResourceCutoverTests
{
    /// <summary>
    /// Replays a corrected future exchange through the retained cold owners without reading or writing physical B.
    /// </summary>
    [Fact]
    public async Task SpiritualColdContinuationView_AcceptsDependentCorrectionOnlyThroughOwnerStep()
    {
        var armSourceRead = 0;
        var sourceReadBlocked = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var releaseSourceRead = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var hooks = new FileSystemManagerHooks
        {
            BeforeCanonicalReadOpenAsync = async path =>
            {
                if (string.Equals(path.Replace('\\', '/'), "input/turn_request.json",
                        StringComparison.Ordinal) &&
                    Interlocked.CompareExchange(ref armSourceRead, 0, 1) == 1)
                {
                    sourceReadBlocked.TrySetResult(true);
                    await releaseSourceRead.Task.WaitAsync(TimeSpan.FromSeconds(15));
                }
            }
        };
        await using var context = await CreateCompleteConflictFrameContextAsync(
            hooks, SeedOriginalIntakeBaselinesAsync);
        await WriteCompleteConflictFrameExchangeAsync(context);
        await WriteSourceContinuationAppendAsync(context, duplicateDice: false);
        var corrected = await ReadProjectedSourceContinuationCandidateAsync(context);
        var original = corrected.DeepClone().AsObject();
        original["activeConflict"]!["exchangeLog"]![1]!["diceAudit"]!["margin"] = 999;
        await context.WriteExactJsonAsync(AfterlifeSpiritualConflictState.StatePath, original.ToJsonString());
        await WriteOriginalIntakeDraftAsync(context);
        AssertNoConflictFrameErrors(await context.Validator.ValidateAcceptedTurnRawMortalLocationMaterializationAsync());
        AssertNoConflictFrameErrors(await context.Validator.ValidateAcceptedTurnRawMortalItemMaterializationAsync());
        await using var lease = await context.FileSystem.AcquireCanonicalWriteLeaseAsync();
        var recorded = await context.Validator.CaptureSpiritualOriginalTurnWithIntakeAsync(lease);
        AssertNoConflictFrameErrors(recorded.Issues);
        using var warm = Assert.IsType<ValidationService.SpiritualOriginalTurnCapture>(recorded.Capture);
        var originalInputs = Assert.IsType<SpiritualOriginalDraftInputs>(OriginalCaptureField(warm, "_draftInputs"));
        var physicalWitnesses = Assert.IsAssignableFrom<IReadOnlyDictionary<string, CanonicalBeforeImage>>(
            OriginalCaptureField(warm, "_physicalWitnesses"));
        warm.Dispose();
        await context.FileSystem.WriteFileAtomicBytesAsync(lease,
            AfterlifeSpiritualConflictState.StatePath, Encoding.UTF8.GetBytes(corrected.ToJsonString()));
        var goldRecorded = await context.Validator.CaptureSpiritualOriginalTurnWithIntakeAsync(lease);
        AssertNoConflictFrameErrors(goldRecorded.Issues);
        using var gold = Assert.IsType<ValidationService.SpiritualOriginalTurnCapture>(goldRecorded.Capture);
        var goldSource = Assert.IsType<ValidationService.SpiritualWoundSourceSession>(
            OriginalCaptureField(gold, "_source"));
        Assert.Equal(corrected.ToJsonString(), goldSource.ReadCandidate(AfterlifeSpiritualConflictState.StatePath));
        AssertNoConflictFrameErrors(await gold.BeginResourceExecutionAsync(lease));
        AssertNoConflictFrameErrors((await gold.AdvanceNextResourceExchangeAsync(lease)).Issues);
        var warmInventoryResult = gold.ReadOriginalActiveExchangeInventory(lease);
        AssertNoConflictFrameErrors(warmInventoryResult.Issues);
        var warmInventory = Assert.IsType<ValidationService.SpiritualOriginalActiveExchangeInventory>(
            warmInventoryResult.Inventory);
        Assert.Equal(2, warmInventory.ExchangeIds.Count);
        Assert.Equal(new[] { 15, 5, 12, 8 }, warmInventory.AcceptedD20Values);
        Assert.Throws<NotSupportedException>(() =>
        {
            ((IList<string>)warmInventory.ExchangeIds)[0] = "forged";
        });
        Assert.Throws<NotSupportedException>(() =>
        {
            ((IList<int>)warmInventory.AcceptedD20Values)[0] = 20;
        });
        AssertNoConflictFrameErrors((await gold.AdvanceNextResourceExchangeAsync(lease)).Issues);
        var allocationRows = gold.ReadAllocationJournal(lease);
        var checkpoint = CreateColdOriginCheckpoint(originalInputs, physicalWitnesses, allocationRows);
        gold.Dispose();

        const string laterPhysical = "{later-uncommitted-draft";
        await context.FileSystem.WriteFileAtomicBytesAsync(lease,
            AfterlifeSpiritualConflictState.StatePath, Encoding.UTF8.GetBytes(laterPhysical));
        var fresh = new ValidationService(context.FileSystem, NullLogger<ValidationService>.Instance);
        var replayed = await fresh.CaptureSpiritualOriginalTurnFromCheckpointOriginAsync(lease, checkpoint);
        AssertNoConflictFrameErrors(replayed.Issues);
        using var cold = Assert.IsType<ValidationService.SpiritualOriginalTurnCapture>(replayed.Capture);
        var beforePrefixInventory = cold.ReadOriginalActiveExchangeInventory(lease);
        Assert.Null(beforePrefixInventory.Inventory);
        Assert.Contains(beforePrefixInventory.Issues,
            issue => issue.Code == "spiritual_original_exchange_contour_pending");
        AssertNoConflictFrameErrors(await cold.BeginResourceExecutionAsync(lease));
        var coldSource = Assert.IsType<ValidationService.SpiritualWoundSourceSession>(
            OriginalCaptureField(cold, "_source"));
        var coldInitial = Assert.IsType<SpiritualOriginalDraftInputs>(
            OriginalCaptureField(cold, "_coldCurrentInputs"));
        var changes = new Dictionary<string, CanonicalBeforeImage>(StringComparer.Ordinal)
        {
            [AfterlifeSpiritualConflictState.StatePath] = new(true,
                Encoding.UTF8.GetBytes(corrected.ToJsonString()))
        };
        var allocationOwner = OriginalCaptureField(cold, "_allocations");
        Assert.NotNull(allocationOwner);
        var journal = ReadField(allocationOwner, "<Journal>k__BackingField");
        var initialJournalPosition = Assert.IsType<int>(ReadField(journal, "_position"));
        var initialAllocationRows = Assert.IsType<JsonArray>(ReadField(journal, "_rows")).DeepClone();
        Assert.Equal(initialJournalPosition, cold.ReadAllocationCursor(lease));
        var early = await cold.AdvanceNextColdResourceExchangeAsync(lease, changes);
        Assert.Null(early.Step);
        Assert.NotEmpty(early.Issues);
        Assert.Null(OriginalCaptureField(cold, "_originalPrefix"));
        Assert.Equal(0, Assert.IsType<int>(OriginalCaptureField(cold, "_nextResourceOrdinal")));
        Assert.Empty(coldSource.Sources);
        Assert.Same(coldInitial, OriginalCaptureField(cold, "_coldCurrentInputs"));
        Assert.Equal(initialJournalPosition, ReadField(journal, "_position"));
        Assert.Equal(initialJournalPosition, cold.ReadAllocationCursor(lease));
        Assert.True(JsonNode.DeepEquals(initialAllocationRows,
            Assert.IsType<JsonArray>(ReadField(journal, "_rows"))));
        AssertNoConflictFrameErrors((await cold.AdvanceNextResourceExchangeAsync(lease)).Issues);
        var coldInventoryResult = cold.ReadOriginalActiveExchangeInventory(lease);
        AssertNoConflictFrameErrors(coldInventoryResult.Issues);
        var coldInventory = Assert.IsType<ValidationService.SpiritualOriginalActiveExchangeInventory>(
            coldInventoryResult.Inventory);
        Assert.Equal(warmInventory.ConflictId, coldInventory.ConflictId);
        Assert.Equal(warmInventory.ExchangeIds, coldInventory.ExchangeIds);
        Assert.Equal(warmInventory.AcceptedD20Values, coldInventory.AcceptedD20Values);
        Assert.InRange(cold.ReadAllocationCursor(lease), initialJournalPosition, allocationRows.Count - 1);
        var sourceRevision = coldSource.ContinuationRevision;
        var sourceCount = coldSource.Sources.Count;
        var stalePrepared = await coldSource.PrepareContinuationAsync(lease);
        Assert.NotNull(stalePrepared.Ticket);
        foreach (var change in new[] { "action", "die", "overlong", "recent", "nonconflict", "malformed" })
        {
            var rejectedCandidate = corrected.DeepClone().AsObject();
            IReadOnlyDictionary<string, CanonicalBeforeImage> rejectedChanges;
            if (change == "nonconflict")
                rejectedChanges = new Dictionary<string, CanonicalBeforeImage>(StringComparer.Ordinal)
                {
                    [ResourceMaterializationContract.StatePath] = new(true, Encoding.UTF8.GetBytes("{}"))
                };
            else
            {
                if (change == "action")
                    rejectedCandidate["activeConflict"]!["exchangeLog"]![1]!["operationType"] = "guard";
                if (change == "die")
                    rejectedCandidate["activeConflict"]!["exchangeLog"]![1]!["diceAudit"]!["diceUsed"]![0]!["sourceIndex"] = 0;
                if (change == "overlong")
                    rejectedCandidate["activeConflict"]!["exchangeLog"]!.AsArray().Add(
                        rejectedCandidate["activeConflict"]!["exchangeLog"]![1]!.DeepClone());
                if (change == "recent")
                    rejectedCandidate["recentConflicts"]!.AsArray().Add(new JsonObject
                    {
                        ["conflictId"] = "new-unoriginal"
                    });
                rejectedChanges = new Dictionary<string, CanonicalBeforeImage>(StringComparer.Ordinal)
                {
                    [AfterlifeSpiritualConflictState.StatePath] = new(true,
                        Encoding.UTF8.GetBytes(change == "malformed"
                            ? "{malformed-saved-correction" : rejectedCandidate.ToJsonString()))
                };
            }
            var rejected = await cold.AdvanceNextColdResourceExchangeAsync(lease, rejectedChanges);
            Assert.Null(rejected.Step);
            Assert.NotEmpty(rejected.Issues);
            Assert.True(cold.IsCurrentOwner);
            Assert.Equal(sourceRevision, coldSource.ContinuationRevision);
            Assert.Equal(sourceCount, coldSource.Sources.Count);
            Assert.Same(coldInitial, OriginalCaptureField(cold, "_coldCurrentInputs"));
            Assert.Equal(Encoding.UTF8.GetBytes(laterPhysical),
                await context.FileSystem.ReadFileBytesAsync(lease, AfterlifeSpiritualConflictState.StatePath));
        }
        Interlocked.Exchange(ref armSourceRead, 1);
        var ordinarySource = coldSource.ContinueAsync(lease);
        await sourceReadBlocked.Task.WaitAsync(TimeSpan.FromSeconds(15));
        var coldAdvance = cold.AdvanceNextColdResourceExchangeAsync(lease, changes);
        try
        {
            for (var attempt = 0; attempt < 250 &&
                 OriginalCaptureField(cold, "_coldProposedInputs") is null; attempt++)
                await Task.Delay(20);
            Assert.NotNull(OriginalCaptureField(cold, "_coldProposedInputs"));
            Assert.Throws<InvalidOperationException>(() => cold.ReadAllocationCursor(lease));
        }
        finally
        {
            releaseSourceRead.TrySetResult(true);
        }
        var ordinaryResult = await ordinarySource;
        Assert.Null(ordinaryResult.Session);
        Assert.Contains(ordinaryResult.Issues,
            issue => issue.Code == "spiritual_cold_continuation_layer_stale");
        var advanced = await coldAdvance;
        AssertNoConflictFrameErrors(advanced.Issues);
        Assert.Equal(1, advanced.Step!.Interval!.Ordinal);
        Assert.Equal(warmInventory.ExchangeIds,
            cold.ReadOriginalActiveExchangeInventory(lease).Inventory!.ExchangeIds);
        Assert.Equal(allocationRows.Count, cold.ReadAllocationCursor(lease));
        var staleCommit = coldSource.CommitPreparedContinuation(lease, stalePrepared.Ticket!);
        Assert.Null(staleCommit.Session);
        Assert.Contains(staleCommit.Issues,
            issue => issue.Code == "spiritual_cold_continuation_layer_stale");
        Assert.True(JsonNode.DeepEquals(allocationRows, cold.ReadAllocationJournal(lease)));
        Assert.Equal(Encoding.UTF8.GetBytes(laterPhysical),
            await context.FileSystem.ReadFileBytesAsync(lease, AfterlifeSpiritualConflictState.StatePath));
        Assert.False(AcceptedMechanicsPlanAuthority.HasValidated(context.FileSystem, lease));
        cold.Dispose();
        Assert.Throws<ObjectDisposedException>(() => cold.ReadAllocationCursor(lease));
    }
}
