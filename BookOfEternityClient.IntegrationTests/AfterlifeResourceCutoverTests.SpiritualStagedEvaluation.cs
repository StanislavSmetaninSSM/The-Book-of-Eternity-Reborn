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
    /// Keeps A active after read-only evaluation and activates B only after authenticated durable progress is committed.
    /// </summary>
    /// <returns>
    /// A task completing after A is evaluated and committed separately, then B resolves without another choice.
    /// </returns>
    [Fact]
    public async Task OriginalSpiritualStagedFrontier_PublicEvaluationAdvancesAThenResolvesB()
    {
        await using var context = await CreateSpiritualStagedContinuationContextAsync();
        await using var lease = await context.FileSystem.AcquireCanonicalWriteLeaseAsync();
        var issuedA = Assert.IsType<SpiritualWoundContinuationRequest>((await context.Validator.ReadSpiritualWoundContinuationAsync(lease)).Request);
        var responseA = new SpiritualWoundContinuationResponse { ContinuationId = issuedA.ContinuationId, WoundDecisions = [] };
        var original = Assert.IsType<JsonObject>(await context.ReadJsonAsync(AfterlifeSpiritualConflictState.StatePath));
        var onlyA = CreateSpiritualStagedCorrectionA(original);
        var allCorrected = CreateSpiritualStagedCorrectionB(onlyA);
        var originalFiles = await ReadSpiritualContinuationTransportFilesAsync(context);
        var missingTransport = await context.Validator.EvaluateAndCommitSpiritualWoundDependentResponseAsync(
            lease, issuedA, responseA, expectedRequestBytes: [1]);
        Assert.Null(missingTransport.Evaluation);
        Assert.Equal("blocked", missingTransport.Progress.Disposition);
        Assert.Contains(missingTransport.Progress.Issues, issue => issue.Code == "spiritual_dependent_progress_transport_changed");
        await AssertSpiritualContinuationTransportFilesUnchangedAsync(context, originalFiles);
        var incompleteResponse = await context.Validator.EvaluateAndCommitSpiritualWoundDependentResponseAsync(lease, issuedA, responseA);
        var incomplete = Assert.IsType<ValidationService.SpiritualWoundContinuationEvaluation>(incompleteResponse.Evaluation);
        Assert.Equal(ValidationService.SpiritualWoundContinuationDisposition.Rejected, incomplete.Disposition);
        Assert.Null(incomplete.NextRequest);
        Assert.Equal("blocked", incompleteResponse.Progress.Disposition);
        await AssertSpiritualContinuationTransportFilesUnchangedAsync(context, originalFiles);
        await context.FileSystem.WriteFileAtomicBytesAsync(lease, AfterlifeSpiritualConflictState.StatePath,
            Encoding.UTF8.GetBytes(onlyA.ToJsonString()));
        var afterA = await ReadSpiritualContinuationTransportFilesAsync(context);
        Assert.NotEmpty(await context.Validator.ValidateSpiritualWoundContinuationDraftAsync(lease, issuedA, responseA,
            new Dictionary<string, byte[]?>(), requireResolvedDraft: true));
        var advanced = await context.Validator.EvaluateSpiritualWoundContinuationDraftAsync(lease, issuedA, responseA);
        Assert.True(advanced.Disposition == ValidationService.SpiritualWoundContinuationDisposition.Advanced,
            advanced.Disposition + Environment.NewLine + string.Join(Environment.NewLine,
                advanced.Issues.Select(issue => issue.Code + " " + issue.FilePath + " " + issue.Message + " " + issue.Actual)));
        Assert.Null(advanced.NextRequest);
        Assert.Equal(JsonSerializer.Serialize(issuedA), JsonSerializer.Serialize((await context.Validator.ReadSpiritualWoundContinuationAsync(lease)).Request));
        Assert.NotEmpty(await context.Validator.ValidateSpiritualWoundContinuationDraftAsync(lease, issuedA, responseA,
            new Dictionary<string, byte[]?> { [AfterlifeSpiritualConflictState.StatePath] = Encoding.UTF8.GetBytes(allCorrected.ToJsonString()) }));
        await AssertSpiritualContinuationTransportFilesUnchangedAsync(context, afterA);

        var processed = await context.Validator.EvaluateAndCommitSpiritualWoundDependentResponseAsync(lease, issuedA, responseA);
        Assert.Equal(ValidationService.SpiritualWoundContinuationDisposition.Advanced, processed.Evaluation!.Disposition);
        var committed = processed.Progress;
        Assert.True(committed.Disposition == "committed", string.Join("\n", committed.Issues));
        var issuedB = Assert.IsType<SpiritualWoundContinuationRequest>(committed.NextRequest);
        Assert.NotEqual(issuedA.ContinuationId, issuedB.ContinuationId);
        Assert.Equal("dependent_draft", issuedB.Phase);
        Assert.Null(issuedB.Offer);
        Assert.Equal(3, issuedB.DependentDraftFields.Count);
        foreach (var field in new[] { "margin", "modifierBreakdown", "oppositionTotal" })
            Assert.Contains(issuedB.DependentDraftFields, row => row.JsonPointer == "/activeConflict/exchangeLog/2/diceAudit/" + field);
        Assert.Equal(JsonSerializer.Serialize(issuedB), JsonSerializer.Serialize((await context.Validator.ReadSpiritualWoundContinuationAsync(lease)).Request));
        Assert.NotEmpty(SpiritualWoundContinuationProtocol.ValidateResponse(issuedB, responseA));
        foreach (var pair in afterA)
        {
            var checkpointKey = Path.GetRelativePath(context.FileSystem.GameSessionPath,
                context.FileSystem.ResolvePath(SpiritualWoundCaptureCheckpointState.StatePath));
            if (pair.Key != checkpointKey)
                Assert.Equal(pair.Value, await File.ReadAllBytesAsync(Path.Combine(context.FileSystem.GameSessionPath, pair.Key)));
        }
        var afterCommit = await ReadSpiritualContinuationTransportFilesAsync(context);
        var staleA = await context.Validator.EvaluateSpiritualWoundContinuationDraftAsync(lease, issuedA, responseA);
        Assert.Equal(ValidationService.SpiritualWoundContinuationDisposition.Rejected, staleA.Disposition);
        var responseB = new SpiritualWoundContinuationResponse { ContinuationId = issuedB.ContinuationId, WoundDecisions = [] };
        Assert.Empty(await context.Validator.ValidateSpiritualWoundContinuationDraftAsync(lease, issuedB, responseB,
            new Dictionary<string, byte[]?> { [AfterlifeSpiritualConflictState.StatePath] = Encoding.UTF8.GetBytes(allCorrected.ToJsonString()) }));
        await AssertSpiritualContinuationTransportFilesUnchangedAsync(context, afterCommit);
        await context.FileSystem.WriteFileAtomicBytesAsync(lease, AfterlifeSpiritualConflictState.StatePath,
            Encoding.UTF8.GetBytes(allCorrected.ToJsonString()));
        var afterB = await ReadSpiritualContinuationTransportFilesAsync(context);
        var cold = new ValidationService(context.FileSystem, NullLogger<ValidationService>.Instance);
        var finalResponse = await cold.EvaluateAndCommitSpiritualWoundDependentResponseAsync(lease, issuedB, responseB);
        var resolved = Assert.IsType<ValidationService.SpiritualWoundContinuationEvaluation>(finalResponse.Evaluation);
        Assert.Equal(ValidationService.SpiritualWoundContinuationDisposition.Resolved, resolved.Disposition);
        Assert.Null(resolved.NextRequest);
        Assert.Empty(resolved.Issues);
        Assert.Equal("blocked", finalResponse.Progress.Disposition);
        Assert.Contains(finalResponse.Progress.Issues, issue => issue.Code == "spiritual_dependent_progress_not_advanced");
        Assert.Empty(await cold.ValidateSpiritualWoundContinuationDraftAsync(lease, issuedB, responseB,
            new Dictionary<string, byte[]?>(), requireResolvedDraft: true));
        await AssertSpiritualContinuationTransportFilesUnchangedAsync(context, afterB);
    }
}
