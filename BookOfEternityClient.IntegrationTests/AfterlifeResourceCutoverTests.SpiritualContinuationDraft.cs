using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using BookOfEternityClient.Core;
using BookOfEternityClient.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed partial class AfterlifeResourceCutoverTests
{
    /// <summary>
    /// Accepts only unchanged files or a bounded narrative replacement for a genuine current decision.
    /// </summary>
    /// <returns>
    /// A task completing after valid and forbidden proposed images have been checked without applying them.
    /// </returns>
    [Fact]
    public async Task OriginalSpiritualContinuationDraft_DecisionPreservesNarrativeSiblingsAndPrivateInputs()
    {
        await using var context = await CreateSpiritualContinuationTransportContextAsync(commitFirst: false);
        const string narrative = """
            {"response":"Духовный обмен завершён.","timestamp":"2026-08-15T00:00:00Z","_scene":{"label":"исходная сцена"}}
            """;
        await context.WriteExactJsonAsync(ProjectionNarrativePath, narrative);
        await using var lease = await context.FileSystem.AcquireCanonicalWriteLeaseAsync();
        await CommitSpiritualContinuationDraftFirstPairAsync(context, lease);
        var read = await context.Validator.ReadSpiritualWoundContinuationAsync(lease);
        Assert.Equal("decision", read.Disposition);
        Assert.Empty(read.Issues);
        var request = Assert.IsType<SpiritualWoundContinuationRequest>(read.Request);
        var response = CreateSpiritualContinuationDraftNoneResponse(request);
        var before = await ReadSpiritualContinuationTransportFilesAsync(context);

        await AssertSpiritualContinuationDraftAsync(context, lease, request, response,
            new Dictionary<string, byte[]?>(), before, accepted: true, "unchanged decision");
        var valid = JsonNode.Parse(narrative)!.AsObject();
        valid["response"] = "Хранитель выпрямился после духовного обмена.";
        valid["timestamp"] = "2026-08-15T00:01:00Z";
        await AssertSpiritualContinuationDraftAsync(context, lease, request, response,
            new Dictionary<string, byte[]?> { [ProjectionNarrativePath] = Encoding.UTF8.GetBytes(valid.ToJsonString()) },
            before, accepted: true, "narrative response and timestamp");

        var alteredSibling = valid.DeepClone();
        alteredSibling["_scene"]!["label"] = "другая сцена";
        var unknownField = valid.DeepClone().AsObject();
        unknownField["woundDecisions"] = new JsonArray();
        var invalidTimestamp = valid.DeepClone();
        invalidTimestamp["timestamp"] = "not-a-timestamp";
        var invalidNarratives = new (string Name, byte[]? Bytes)[]
        {
            ("frozen underscore sibling", Encoding.UTF8.GetBytes(alteredSibling.ToJsonString())),
            ("decision field inside narrative", Encoding.UTF8.GetBytes(unknownField.ToJsonString())),
            ("duplicate response", Encoding.UTF8.GetBytes("""
                {"response":"Первый текст.","response":"Второй текст.","timestamp":"2026-08-15T00:01:00Z","_scene":{"label":"исходная сцена"}}
                """)),
            ("invalid timestamp", Encoding.UTF8.GetBytes(invalidTimestamp.ToJsonString())),
            ("narrative deletion", null)
        };
        foreach (var invalid in invalidNarratives)
            await AssertSpiritualContinuationDraftAsync(context, lease, request, response,
                new Dictionary<string, byte[]?> { [ProjectionNarrativePath] = invalid.Bytes },
                before, accepted: false, invalid.Name);
        foreach (var path in new[]
                 {
                     AcceptedMechanicsPlan.WoundCommandPath, SpiritualWoundCaptureCheckpointState.StatePath,
                     SpiritualWoundDecisionPendingState.StatePath, ResourceMaterializationContract.StatePath,
                     AfterlifeSpiritualConflictState.StatePath
                 })
            await AssertSpiritualContinuationDraftAsync(context, lease, request, response,
                new Dictionary<string, byte[]?> { [path] = Encoding.UTF8.GetBytes("{}") },
                before, accepted: false, $"forbidden decision path {path}");
    }

    /// <summary>
    /// Uses the saved guard-burden choice to permit exact cost corrections while freezing source evidence.
    /// </summary>
    /// <returns>
    /// A task completing after detached corrections and unrelated mutations have left all physical files intact.
    /// </returns>
    [Fact]
    public async Task OriginalSpiritualContinuationDraft_DependentCorrectionPreservesActionsDiceAndSiblings()
    {
        await using var context = await CreateSpiritualContinuationTransportContextAsync(
            commitFirst: true, dependentCost: true);
        await using var lease = await context.FileSystem.AcquireCanonicalWriteLeaseAsync();
        var opened = await context.Validator.OpenC2PrivateSessionAsync(lease);
        Assert.Equal("offer", opened.Disposition);
        using (var session = Assert.IsType<ValidationService.SpiritualC2PrivateSession>(opened.Session))
        {
            var selected = await session.SubmitDecisionAsync(lease,
                OriginalSpiritualWoundDecision(session.Offer!.OpportunityRef, "spiritual_action_cost_burden", "guard"),
                "Чужое давление надломило волю хранителя.");
            Assert.Equal("dependent_continuation", selected.Disposition);
            selected.Session?.Dispose();
        }
        var read = await new ValidationService(context.FileSystem, NullLogger<ValidationService>.Instance)
            .ReadSpiritualWoundContinuationAsync(lease);
        Assert.Equal("dependent_draft", read.Disposition);
        var request = Assert.IsType<SpiritualWoundContinuationRequest>(read.Request);
        Assert.Equal(new[]
        {
            "/activeConflict/exchangeLog/1/actionCostAudit/opposition/after",
            "/activeConflict/exchangeLog/1/actionCostAudit/opposition/effectiveCost"
        }, request.DependentDraftFields.Select(field => field.JsonPointer).ToArray());
        var response = new SpiritualWoundContinuationResponse
        {
            ContinuationId = request.ContinuationId,
            WoundDecisions = []
        };
        var original = JsonNode.Parse((await context.FileSystem.ReadFileBytesAsync(lease,
            AfterlifeSpiritualConflictState.StatePath))!)!;
        var corrected = original.DeepClone();
        corrected["activeConflict"]!["exchangeLog"]![1]!["actionCostAudit"]!["opposition"]!["effectiveCost"] = 3;
        corrected["activeConflict"]!["exchangeLog"]![1]!["actionCostAudit"]!["opposition"]!["after"] = 0;
        var before = await ReadSpiritualContinuationTransportFilesAsync(context);

        await AssertSpiritualContinuationDraftAsync(context, lease, request, response,
            new Dictionary<string, byte[]?> { [AfterlifeSpiritualConflictState.StatePath] = Encoding.UTF8.GetBytes(corrected.ToJsonString()) },
            before, accepted: true, "guard burden cost correction");
        var sibling = corrected.DeepClone();
        sibling["activeConflict"]!["exchangeLog"]![1]!["actionCostAudit"]!["opposition"]!["baseCost"] = 3;
        var action = corrected.DeepClone();
        action["activeConflict"]!["exchangeLog"]![1]!["matchupAudit"]!["oppositionOperation"] = "pressure";
        var dice = corrected.DeepClone();
        dice["activeConflict"]!["exchangeLog"]![0]!["diceAudit"]!["diceUsed"]![0]!["value"] = 16;
        var nullAudit = corrected.DeepClone();
        nullAudit["activeConflict"]!["exchangeLog"]![1]!["actionCostAudit"]!["opposition"] = null;
        foreach (var invalid in new (string Name, byte[]? Bytes)[]
                 {
                     ("frozen base cost", Encoding.UTF8.GetBytes(sibling.ToJsonString())),
                     ("original action", Encoding.UTF8.GetBytes(action.ToJsonString())),
                     ("signed dice evidence", Encoding.UTF8.GetBytes(dice.ToJsonString())),
                     ("null ordinary audit", Encoding.UTF8.GetBytes(nullAudit.ToJsonString())),
                     ("conflict deletion", null)
                 })
            await AssertSpiritualContinuationDraftAsync(context, lease, request, response,
                new Dictionary<string, byte[]?> { [AfterlifeSpiritualConflictState.StatePath] = invalid.Bytes },
                before, accepted: false, invalid.Name);
    }

    /// <summary>
    /// Rejects a previously genuine request and response after their decision has actually advanced C2.
    /// </summary>
    /// <returns>
    /// A task completing after stale correlation is rejected without altering the completed private frontier.
    /// </returns>
    [Fact]
    public async Task OriginalSpiritualContinuationDraft_RejectsRequestAfterSavedAdvance()
    {
        await using var context = await CreateSpiritualContinuationTransportContextAsync(commitFirst: true);
        await using var lease = await context.FileSystem.AcquireCanonicalWriteLeaseAsync();
        var read = await context.Validator.ReadSpiritualWoundContinuationAsync(lease);
        Assert.Equal("decision", read.Disposition);
        var request = Assert.IsType<SpiritualWoundContinuationRequest>(read.Request);
        var response = CreateSpiritualContinuationDraftNoneResponse(request);
        var opened = await context.Validator.OpenC2PrivateSessionAsync(lease);
        Assert.Equal("offer", opened.Disposition);
        using (var session = Assert.IsType<ValidationService.SpiritualC2PrivateSession>(opened.Session))
        {
            var completed = await session.SubmitDecisionAsync(lease, Assert.Single(response.WoundDecisions), null);
            Assert.Equal("completed_unpublished", completed.Disposition);
            Assert.Empty(completed.Issues);
            completed.Session?.Dispose();
        }
        var before = await ReadSpiritualContinuationTransportFilesAsync(context);

        await AssertSpiritualContinuationDraftAsync(context, lease, request, response,
            new Dictionary<string, byte[]?>(), before, accepted: false, "already advanced decision");
    }

    /// <summary>
    /// Commits the prepared original draft without replacing its narrative or other fixture inputs.
    /// </summary>
    /// <param name="context">
    /// Signed original fixture whose raw intake checks already passed.
    /// </param>
    /// <param name="lease">
    /// Active lease covering the first resource exchange and private transport commit.
    /// </param>
    /// <returns>
    /// A task completing after the genuine first pair is committed and its capture is disposed.
    /// </returns>
    private static async Task CommitSpiritualContinuationDraftFirstPairAsync(
        ResourceMaterializationTestContext context, FileSystemManager.CanonicalWriteLease lease)
    {
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
    }

    /// <summary>
    /// Creates an explicit decline for the current genuine offer without manufacturing its identity.
    /// </summary>
    /// <param name="request">
    /// Current decision request whose offer permits none.
    /// </param>
    /// <returns>
    /// The correlated response containing exactly one explicit none decision.
    /// </returns>
    private static SpiritualWoundContinuationResponse CreateSpiritualContinuationDraftNoneResponse(
        SpiritualWoundContinuationRequest request)
    {
        var offer = Assert.IsType<SpiritualWoundContinuationOffer>(request.Offer);
        Assert.Contains("none", offer.AllowedDecisions);
        return new()
        {
            ContinuationId = request.ContinuationId,
            WoundDecisions = [JsonSerializer.SerializeToElement(new { opportunityRef = offer.OpportunityRef, decision = "none" })]
        };
    }

    /// <summary>
    /// Checks proposed bytes through a fresh validator and verifies that no session file was applied or rewritten.
    /// </summary>
    /// <param name="context">
    /// The real signed fixture whose physical images remain unchanged throughout the subcases.
    /// </param>
    /// <param name="lease">
    /// Active canonical lease retained across validation and the exact byte comparison.
    /// </param>
    /// <param name="request">
    /// Previously read genuine request to reauthenticate against the current C2 boundary.
    /// </param>
    /// <param name="response">
    /// Explicit decision or empty dependent response correlated with the request.
    /// </param>
    /// <param name="proposedImages">
    /// Detached replacements; null means deletion and omitted paths remain unchanged.
    /// </param>
    /// <param name="before">
    /// Exact physical session file set captured before any proposed-image checks.
    /// </param>
    /// <param name="accepted">
    /// True requires no issues; false requires an error rejecting the proposed draft.
    /// </param>
    /// <param name="scenario">
    /// Human-readable subcase included in a failing admission assertion.
    /// </param>
    /// <returns>
    /// A task completing after the expected admission result and unchanged physical files are asserted.
    /// </returns>
    private static async Task AssertSpiritualContinuationDraftAsync(ResourceMaterializationTestContext context,
        FileSystemManager.CanonicalWriteLease lease, SpiritualWoundContinuationRequest request,
        SpiritualWoundContinuationResponse response, IReadOnlyDictionary<string, byte[]?> proposedImages,
        IReadOnlyDictionary<string, byte[]> before, bool accepted, string scenario)
    {
        var fresh = new ValidationService(context.FileSystem, NullLogger<ValidationService>.Instance);
        var issues = await fresh.ValidateSpiritualWoundContinuationDraftAsync(lease, request, response, proposedImages);
        Assert.True(accepted ? issues.Count == 0 : issues.Any(issue => issue.Severity == IssueSeverity.Error),
            $"{scenario}: expected {(accepted ? "acceptance" : "rejection")}; {string.Join(Environment.NewLine, issues)}");
        await AssertSpiritualContinuationTransportFilesUnchangedAsync(context, before);
    }
}
