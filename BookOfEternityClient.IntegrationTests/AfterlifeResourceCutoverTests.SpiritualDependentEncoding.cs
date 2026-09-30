using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using BookOfEternityClient.Services;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed partial class AfterlifeResourceCutoverTests
{
    /// <summary>
    /// Preserves the issued saved-choice continuation after applying a permitted correction with either supported UTF-8 encoding.
    /// </summary>
    /// <param name="withPreamble">
    /// Whether physical correction bytes include the UTF-8 preamble written by the canonical text writer.
    /// </param>
    /// <returns>
    /// A task completing after unchanged request identity, successful resolved postflight and exact file preservation are verified.
    /// </returns>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task OriginalSpiritualDependentCorrection_Utf8PostflightPreservesIssuedRequest(bool withPreamble)
    {
        await using var context = await CreateSpiritualContinuationTransportContextAsync(commitFirst: true, dependentCost: true);
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
        var read = await context.Validator.ReadSpiritualWoundContinuationAsync(lease);
        var issued = Assert.IsType<SpiritualWoundContinuationRequest>(read.Request);
        Assert.Equal("dependent_draft", issued.Phase);
        Assert.NotEmpty(read.Issues);
        var raw = JsonNode.Parse((await context.FileSystem.ReadFileAsync(lease, AfterlifeSpiritualConflictState.StatePath))!)!;
        raw["activeConflict"]!["exchangeLog"]![1]!["actionCostAudit"]!["opposition"]!["effectiveCost"] = 3;
        raw["activeConflict"]!["exchangeLog"]![1]!["actionCostAudit"]!["opposition"]!["after"] = 0;
        var originalFiles = await ReadSpiritualContinuationTransportFilesAsync(context);
        byte[] duplicatePreamble = [.. Encoding.UTF8.GetPreamble(), .. Encoding.UTF8.GetPreamble(),
            .. Encoding.UTF8.GetBytes(raw.ToJsonString())];
        var rejected = await context.Validator.ValidateSpiritualWoundContinuationDraftAsync(lease, issued,
            new SpiritualWoundContinuationResponse { ContinuationId = issued.ContinuationId, WoundDecisions = [] },
            new Dictionary<string, byte[]?> { [AfterlifeSpiritualConflictState.StatePath] = duplicatePreamble });
        Assert.NotEmpty(rejected);
        await AssertSpiritualContinuationTransportFilesUnchangedAsync(context, originalFiles);
        byte[] bytes = [.. withPreamble ? Encoding.UTF8.GetPreamble() : [], .. Encoding.UTF8.GetBytes(raw.ToJsonString())];
        await context.FileSystem.WriteFileAtomicBytesAsync(lease, AfterlifeSpiritualConflictState.StatePath, bytes);
        var before = await ReadSpiritualContinuationTransportFilesAsync(context);
        var current = await context.Validator.ReadSpiritualWoundContinuationAsync(lease);
        Assert.True(current.Request is not null,
            $"Projection={current.Disposition}; issues={string.Join("; ", current.Issues.Select(issue => issue.Code))}");
        Assert.Equal(JsonSerializer.Serialize(issued), JsonSerializer.Serialize(current.Request));
        Assert.Empty(current.Issues);
        var issues = await context.Validator.ValidateSpiritualWoundContinuationDraftAsync(lease, issued,
            new SpiritualWoundContinuationResponse { ContinuationId = issued.ContinuationId, WoundDecisions = [] },
            new Dictionary<string, byte[]?>(), requireResolvedDraft: true);
        Assert.Empty(issues);
        await AssertSpiritualContinuationTransportFilesUnchangedAsync(context, before);
        Assert.Equal(bytes, await context.FileSystem.ReadFileBytesAsync(lease, AfterlifeSpiritualConflictState.StatePath));
    }
}
