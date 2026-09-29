using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using BookOfEternityClient.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed partial class AfterlifeResourceCutoverTests
{
    /// <summary>
    /// Projects the genuine first C2 offer with stable correlation without changing any session file.
    /// </summary>
    /// <returns>
    /// A task completing after two fresh validators have independently read the same safe request.
    /// </returns>
    [Fact]
    public async Task OriginalSpiritualContinuationTransport_ReadsStableDecisionWithoutWriting()
    {
        await using var context = await CreateSpiritualContinuationTransportContextAsync(commitFirst: true);
        await using var lease = await context.FileSystem.AcquireCanonicalWriteLeaseAsync();
        ValidationService.SpiritualC2PrivateOffer expectedOffer;
        var oracle = new ValidationService(context.FileSystem, NullLogger<ValidationService>.Instance);
        var opened = await oracle.OpenC2PrivateSessionAsync(lease);
        Assert.Empty(opened.Issues);
        Assert.Equal("offer", opened.Disposition);
        using (var session = Assert.IsType<ValidationService.SpiritualC2PrivateSession>(opened.Session))
            expectedOffer = Assert.IsType<ValidationService.SpiritualC2PrivateOffer>(session.Offer);

        var before = await ReadSpiritualContinuationTransportFilesAsync(context);
        string? firstProjection = null;
        for (var attempt = 0; attempt < 2; attempt++)
        {
            var fresh = new ValidationService(context.FileSystem, NullLogger<ValidationService>.Instance);

            var result = await fresh.ReadSpiritualWoundContinuationAsync(lease);

            Assert.Equal("decision", result.Disposition);
            Assert.Empty(result.Issues);
            var request = Assert.IsType<SpiritualWoundContinuationRequest>(result.Request);
            Assert.Empty(SpiritualWoundContinuationProtocol.ValidateRequest(request));
            Assert.Equal(1, request.SchemaVersion);
            Assert.Equal("decision", request.Phase);
            Assert.False(string.IsNullOrWhiteSpace(request.ContinuationId));
            Assert.Empty(request.DependentDraftFields);
            Assert.Equal("output/narrative_response.json", request.SceneTextSource.Path);
            Assert.Equal("response", request.SceneTextSource.Field);
            var offer = Assert.IsType<SpiritualWoundContinuationOffer>(request.Offer);
            Assert.Equal(expectedOffer.OpportunityRef, offer.OpportunityRef);
            Assert.Equal(expectedOffer.MinimumSeverityRank, offer.MinimumSeverityRank);
            Assert.Equal(expectedOffer.RequiredSeverityRank, offer.RequiredSeverityRank);
            Assert.Equal(expectedOffer.MaximumSeverityRank, offer.MaximumSeverityRank);
            Assert.Equal(expectedOffer.Target, offer.Target);
            Assert.Equal(expectedOffer.Cause, offer.Cause);
            Assert.Equal(expectedOffer.AllowedLocationKinds.ToArray(), offer.AllowedLocationKinds.ToArray());
            Assert.Equal(expectedOffer.AllowedDecisions.ToArray(), offer.AllowedDecisions.ToArray());
            var projection = JsonSerializer.Serialize(request);
            if (firstProjection is null)
                firstProjection = projection;
            else
                Assert.Equal(firstProjection, projection);
            await AssertSpiritualContinuationTransportFilesUnchangedAsync(context, before);
        }

        var execution = new ValidationService(context.FileSystem, NullLogger<ValidationService>.Instance);
        var current = await execution.OpenC2PrivateSessionAsync(lease);
        Assert.Equal("offer", current.Disposition);
        using (var session = current.Session!)
        {
            var completed = await session.SubmitDecisionAsync(lease, JsonSerializer.SerializeToElement(new
            {
                opportunityRef = session.Offer!.OpportunityRef, decision = "none"
            }), null);
            Assert.Equal("completed_unpublished", completed.Disposition);
            Assert.Empty(completed.Issues);
            completed.Session?.Dispose();
        }
        var beforeCompletedRead = await ReadSpiritualContinuationTransportFilesAsync(context);
        var completedRead = await new ValidationService(context.FileSystem, NullLogger<ValidationService>.Instance)
            .ReadSpiritualWoundContinuationAsync(lease);
        Assert.Equal("completed_unpublished", completedRead.Disposition);
        Assert.Null(completedRead.Request);
        Assert.Empty(completedRead.Issues);
        await AssertSpiritualContinuationTransportFilesUnchangedAsync(context, beforeCompletedRead);
    }

    /// <summary>
    /// Retains a selected wound's transport correlation and exact fields after a real dependent correction.
    /// </summary>
    /// <returns>
    /// A task completing after both uncorrected and corrected drafts have been read without writes.
    /// </returns>
    [Fact]
    public async Task OriginalSpiritualContinuationTransport_ReadsStableDependentDraftAfterCorrection()
    {
        await using var context = await CreateSpiritualContinuationTransportContextAsync(
            commitFirst: true, dependentCost: true);
        await using var lease = await context.FileSystem.AcquireCanonicalWriteLeaseAsync();
        var execution = new ValidationService(context.FileSystem, NullLogger<ValidationService>.Instance);
        var opened = await execution.OpenC2PrivateSessionAsync(lease);
        Assert.Equal("offer", opened.Disposition);
        using (var session = opened.Session!)
        {
            var selected = await session.SubmitDecisionAsync(lease,
                OriginalSpiritualWoundDecision(session.Offer!.OpportunityRef,
                    "spiritual_action_cost_burden", "guard"),
                "Чужое давление надломило волю хранителя.");
            Assert.Equal("dependent_continuation", selected.Disposition);
            selected.Session?.Dispose();
        }
        string? originalId = null;
        string[]? originalFields = null;
        for (var correction = 0; correction < 2; correction++)
        {
            if (correction == 1)
            {
                var candidate = (await context.ReadJsonAsync(AfterlifeSpiritualConflictState.StatePath))!;
                candidate["activeConflict"]!["exchangeLog"]![1]!["actionCostAudit"]!["opposition"]!["effectiveCost"] = 3;
                candidate["activeConflict"]!["exchangeLog"]![1]!["actionCostAudit"]!["opposition"]!["after"] = 0;
                await context.FileSystem.WriteFileAtomicBytesAsync(lease, AfterlifeSpiritualConflictState.StatePath,
                    Encoding.UTF8.GetBytes(candidate.ToJsonString()));
            }
            var before = await ReadSpiritualContinuationTransportFilesAsync(context);
            var read = await new ValidationService(context.FileSystem, NullLogger<ValidationService>.Instance)
                .ReadSpiritualWoundContinuationAsync(lease);
            Assert.Equal("dependent_draft", read.Disposition);
            var request = Assert.IsType<SpiritualWoundContinuationRequest>(read.Request);
            Assert.Empty(SpiritualWoundContinuationProtocol.ValidateRequest(request));
            Assert.Equal("dependent_draft", request.Phase);
            Assert.Null(request.Offer);
            Assert.All(request.DependentDraftFields,
                field => Assert.Equal(AfterlifeSpiritualConflictState.StatePath, field.Path));
            var fields = request.DependentDraftFields.Select(field => field.JsonPointer).ToArray();
            Assert.Equal(new[]
            {
                "/activeConflict/exchangeLog/1/actionCostAudit/opposition/after",
                "/activeConflict/exchangeLog/1/actionCostAudit/opposition/effectiveCost"
            }, fields);
            if (correction == 0)
            {
                Assert.NotEmpty(read.Issues);
                originalId = request.ContinuationId;
                originalFields = fields;
            }
            else
            {
                Assert.Empty(read.Issues);
                Assert.Equal(originalId, request.ContinuationId);
                Assert.Equal(originalFields, fields);
            }
            await AssertSpiritualContinuationTransportFilesUnchangedAsync(context, before);
        }
    }

    /// <summary>
    /// Changes the safe correlation only after a genuine saved advance reaches the next opportunity.
    /// </summary>
    /// <returns>
    /// A task completing after both offered frontiers have been read without physical mutations.
    /// </returns>
    [Fact]
    public async Task OriginalSpiritualContinuationTransport_ChangesCorrelationAfterSavedAdvance()
    {
        await using var context = await CreateTwoSourceC2ContextAsync();
        await using var lease = await context.FileSystem.AcquireCanonicalWriteLeaseAsync();
        string? initialId = null;
        string? initialOpportunity = null;
        for (var advance = 0; advance < 2; advance++)
        {
            var before = await ReadSpiritualContinuationTransportFilesAsync(context);
            var read = await new ValidationService(context.FileSystem, NullLogger<ValidationService>.Instance)
                .ReadSpiritualWoundContinuationAsync(lease);
            Assert.Equal("decision", read.Disposition);
            Assert.Empty(read.Issues);
            var request = Assert.IsType<SpiritualWoundContinuationRequest>(read.Request);
            Assert.Empty(SpiritualWoundContinuationProtocol.ValidateRequest(request));
            await AssertSpiritualContinuationTransportFilesUnchangedAsync(context, before);
            if (advance == 1)
            {
                Assert.NotEqual(initialId, request.ContinuationId);
                Assert.NotEqual(initialOpportunity, request.Offer!.OpportunityRef);
                break;
            }
            initialId = request.ContinuationId;
            initialOpportunity = request.Offer!.OpportunityRef;
            var opened = await new ValidationService(context.FileSystem, NullLogger<ValidationService>.Instance)
                .OpenC2PrivateSessionAsync(lease);
            Assert.Equal("offer", opened.Disposition);
            using var session = opened.Session!;
            var next = await session.SubmitDecisionAsync(lease, JsonSerializer.SerializeToElement(new
            {
                opportunityRef = session.Offer!.OpportunityRef, decision = "none"
            }), null);
            Assert.Equal("offer", next.Disposition);
            Assert.Empty(next.Issues);
            next.Session?.Dispose();
        }
    }

    /// <summary>
    /// Refuses to manufacture a C2 checkpoint from a signed original exchange during a transport read.
    /// </summary>
    /// <returns>
    /// A task completing after the absent checkpoint and all original draft bytes are preserved.
    /// </returns>
    [Fact]
    public async Task OriginalSpiritualContinuationTransport_WithoutCheckpointDoesNotCreateAuthority()
    {
        await using var context = await CreateSpiritualContinuationTransportContextAsync(commitFirst: false);
        await using var lease = await context.FileSystem.AcquireCanonicalWriteLeaseAsync();
        Assert.Null(await context.FileSystem.ReadFileBytesAsync(lease, SpiritualWoundCaptureCheckpointState.StatePath));
        Assert.Null(await context.FileSystem.ReadFileBytesAsync(lease, SpiritualWoundDecisionPendingState.StatePath));
        var before = await ReadSpiritualContinuationTransportFilesAsync(context);
        var fresh = new ValidationService(context.FileSystem, NullLogger<ValidationService>.Instance);

        var result = await fresh.ReadSpiritualWoundContinuationAsync(lease);

        Assert.Equal("no_checkpoint", result.Disposition);
        Assert.Null(result.Request);
        Assert.Empty(result.Issues);
        await AssertSpiritualContinuationTransportFilesUnchangedAsync(context, before);
    }

    /// <summary>
    /// Fails closed on an incomplete or corrupt private pair without repairing it during a read.
    /// </summary>
    /// <param name="corruptCheckpoint">
    /// Whether to corrupt the committed checkpoint; false removes only its derived pending projection.
    /// </param>
    /// <returns>
    /// A task completing after the damaged pair and every other session file remain byte-identical.
    /// </returns>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task OriginalSpiritualContinuationTransport_InvalidPrivatePairDoesNotSelfRepair(
        bool corruptCheckpoint)
    {
        await using var context = await CreateSpiritualContinuationTransportContextAsync(commitFirst: true);
        await using var lease = await context.FileSystem.AcquireCanonicalWriteLeaseAsync();
        if (corruptCheckpoint)
            await context.FileSystem.WriteFileAtomicBytesAsync(lease,
                SpiritualWoundCaptureCheckpointState.StatePath, Encoding.UTF8.GetBytes("{broken"));
        else
            context.FileSystem.DeleteFile(lease, SpiritualWoundDecisionPendingState.StatePath);
        var before = await ReadSpiritualContinuationTransportFilesAsync(context);
        var fresh = new ValidationService(context.FileSystem, NullLogger<ValidationService>.Instance);

        var result = await fresh.ReadSpiritualWoundContinuationAsync(lease);

        Assert.Equal("blocked", result.Disposition);
        Assert.Null(result.Request);
        Assert.NotEmpty(result.Issues);
        await AssertSpiritualContinuationTransportFilesUnchangedAsync(context, before);
    }

    /// <summary>
    /// Builds a signed original turn and optionally commits its genuine first private C2 pair.
    /// </summary>
    /// <param name="commitFirst">
    /// Whether to capture intake, execute the first exchange and commit its checkpoint before returning.
    /// </param>
    /// <param name="dependentCost">
    /// Whether to include a later guard whose cost changes after selecting the wound burden.
    /// </param>
    /// <returns>
    /// The owned fixture with no retained capture or canonical lease; the caller must dispose it.
    /// </returns>
    private static async Task<ResourceMaterializationTestContext> CreateSpiritualContinuationTransportContextAsync(
        bool commitFirst, bool dependentCost = false)
    {
        var context = await CreateCompleteConflictFrameContextAsync(
            seedOriginalInputs: SeedOriginalIntakeBaselinesAsync);
        try
        {
            await WriteCompleteConflictFrameExchangeAsync(context);
            if (dependentCost)
            {
                await WriteSourceContinuationAppendAsync(context, duplicateDice: false);
                var candidate = await ReadProjectedSourceContinuationCandidateAsync(context);
                var active = candidate["activeConflict"]!;
                var second = active["exchangeLog"]![1]!;
                second["outcome"] = "no_effect";
                second["after"] = second["before"]!.DeepClone();
                second.AsObject().Remove("diceAudit");
                second["matchupAudit"]!["oppositionOperation"] = "guard";
                second["actionCostAudit"]!["opposition"] = CostAudit("guard", 2, 3, 1);
                active["oppositionSideStrain"] = second["before"]!["oppositionSideStrain"]!.DeepClone();
                await context.WriteExactJsonAsync(AfterlifeSpiritualConflictState.StatePath, candidate.ToJsonString());
            }
            await WriteOriginalIntakeDraftAsync(context);
            await context.WriteExactJsonAsync(ProjectionNarrativePath,
                "{\"response\":\"Духовный обмен завершён.\",\"timestamp\":\"2026-08-15T00:00:00Z\"}");
            AssertNoConflictFrameErrors(await context.Validator.ValidateAcceptedTurnRawMortalLocationMaterializationAsync());
            AssertNoConflictFrameErrors(await context.Validator.ValidateAcceptedTurnRawMortalItemMaterializationAsync());
            if (commitFirst)
            {
                await using var lease = await context.FileSystem.AcquireCanonicalWriteLeaseAsync();
                var recorded = await context.Validator.CaptureSpiritualOriginalTurnWithIntakeAsync(lease);
                AssertNoConflictFrameErrors(recorded.Issues);
                using var capture = Assert.IsType<ValidationService.SpiritualOriginalTurnCapture>(recorded.Capture);
                AssertNoConflictFrameErrors(await capture.BeginResourceExecutionAsync(lease));
                var advanced = await capture.AdvanceNextResourceExchangeAsync(lease);
                AssertNoConflictFrameErrors(advanced.Issues);
                var interval = Assert.IsType<AcceptedMechanicsPlanner.SpiritualExchangeInterval>(advanced.Step?.Interval);
                var committed = await capture.CommitC2FirstTransportAsync(lease, interval);
                AssertNoConflictFrameErrors(committed.Issues);
                Assert.Equal("committed", committed.Disposition);
                Assert.NotNull(committed.Checkpoint);
                Assert.NotNull(committed.Pending);
            }
            return context;
        }
        catch
        {
            await context.DisposeAsync();
            throw;
        }
    }

    /// <summary>
    /// Reads every physical session file, including signed snapshots and absent-control evidence from its file list.
    /// </summary>
    /// <param name="context">
    /// Fixture whose session tree is stable under the caller's canonical lease.
    /// </param>
    /// <returns>
    /// Exact bytes keyed by relative physical session path, excluding external runtime lock files.
    /// </returns>
    private static async Task<Dictionary<string, byte[]>> ReadSpiritualContinuationTransportFilesAsync(
        ResourceMaterializationTestContext context)
    {
        var files = new Dictionary<string, byte[]>(StringComparer.Ordinal);
        foreach (var path in Directory.EnumerateFiles(context.FileSystem.GameSessionPath, "*", SearchOption.AllDirectories))
            files.Add(Path.GetRelativePath(context.FileSystem.GameSessionPath, path), await File.ReadAllBytesAsync(path));
        return files;
    }

    /// <summary>
    /// Checks that transport projection neither changes existing bytes nor creates or deletes session files.
    /// </summary>
    /// <param name="context">
    /// Fixture still protected by the caller's canonical lease.
    /// </param>
    /// <param name="before">
    /// Exact physical file set captured immediately before the read under test.
    /// </param>
    /// <returns>
    /// A task completing after all physical session files have been compared.
    /// </returns>
    private static async Task AssertSpiritualContinuationTransportFilesUnchangedAsync(
        ResourceMaterializationTestContext context, IReadOnlyDictionary<string, byte[]> before)
    {
        var after = await ReadSpiritualContinuationTransportFilesAsync(context);
        Assert.Equal(before.Keys.OrderBy(path => path, StringComparer.Ordinal).ToArray(),
            after.Keys.OrderBy(path => path, StringComparer.Ordinal).ToArray());
        foreach (var pair in before)
            Assert.True(pair.Value.AsSpan().SequenceEqual(after[pair.Key]), $"Transport read changed {pair.Key}.");
    }
}
