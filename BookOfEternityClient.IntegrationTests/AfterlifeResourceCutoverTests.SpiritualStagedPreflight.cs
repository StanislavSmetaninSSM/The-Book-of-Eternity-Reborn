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
    /// Requires the actual public request A to reject future B edits and every malformed or independently changed critical correction.
    /// </summary>
    /// <returns>
    /// A task completing after exact A admission and rejected proposals leave all private and canonical files unchanged.
    /// </returns>
    [Fact]
    public async Task OriginalSpiritualStagedFrontier_IssuedARejectsFutureAndInvalidCriticalDrafts()
    {
        await using var context = await CreateSpiritualStagedContinuationContextAsync();
        await using var lease = await context.FileSystem.AcquireCanonicalWriteLeaseAsync();
        var projection = await context.Validator.ReadSpiritualWoundContinuationAsync(lease);
        Assert.True(projection.Request is not null,
            $"Expected issued A; disposition={projection.Disposition}; {string.Join("; ", projection.Issues.Select(issue => issue.Code))}");
        var issuedA = projection.Request!;
        Assert.Equal("dependent_draft", issuedA.Phase);
        Assert.Null(issuedA.Offer);
        Assert.Equal(new[] { "/activeConflict/exchangeLog/1/diceAudit/criticalResult",
            "/activeConflict/exchangeLog/1/diceAudit/margin", "/activeConflict/exchangeLog/1/diceAudit/modifierBreakdown",
            "/activeConflict/exchangeLog/1/diceAudit/oppositionTotal" }, issuedA.DependentDraftFields.Select(field => field.JsonPointer));
        var responseA = new SpiritualWoundContinuationResponse { ContinuationId = issuedA.ContinuationId, WoundDecisions = [] };
        var original = Assert.IsType<JsonObject>(await context.ReadJsonAsync(AfterlifeSpiritualConflictState.StatePath));
        var validA = CreateSpiritualStagedCorrectionA(original);
        var files = await ReadSpiritualContinuationTransportFilesAsync(context);
        foreach (var mutation in new[] { "future_b", "missing_scale", "blank_constraint", "critical_scalar", "signed_die", "independent_modifier" })
        {
            var candidate = mutation == "future_b" ? CreateSpiritualStagedCorrectionB(validA) : validA.DeepClone().AsObject();
            var dice = candidate["activeConflict"]!["exchangeLog"]![1]!["diceAudit"]!;
            switch (mutation)
            {
                case "missing_scale": dice["criticalResult"]!.AsObject().Remove("scaleLimit"); break;
                case "blank_constraint": dice["criticalResult"]!["narrativeConstraint"] = " \t "; break;
                case "critical_scalar": dice["criticalResult"]!["playerNaturalRoll"] = 19; break;
                case "signed_die": dice["diceUsed"]![0]!["value"] = 19; break;
                case "independent_modifier":
                    dice["modifierBreakdown"]!["player"]![0]!["value"] = 2;
                    dice["playerTotal"] = 22;
                    dice["margin"] = 2;
                    break;
            }
            await AssertSpiritualContinuationDraftAsync(context, lease, issuedA, responseA,
                new Dictionary<string, byte[]?> { [AfterlifeSpiritualConflictState.StatePath] = Encoding.UTF8.GetBytes(candidate.ToJsonString()) },
                files, accepted: false, mutation);
        }
        await AssertSpiritualContinuationDraftAsync(context, lease, issuedA, responseA,
            new Dictionary<string, byte[]?> { [AfterlifeSpiritualConflictState.StatePath] = Encoding.UTF8.GetBytes(validA.ToJsonString()) },
            files, accepted: true, "exact issued A with actual critical text");
        var repeated = await context.Validator.ReadSpiritualWoundContinuationAsync(lease);
        Assert.Equal(JsonSerializer.Serialize(issuedA), JsonSerializer.Serialize(repeated.Request));
        await AssertSpiritualContinuationTransportFilesUnchangedAsync(context, files);
    }

    /// <summary>
    /// Rejects an issued public A after replacement with another genuine fixture's private pair and selected command.
    /// </summary>
    /// <returns>
    /// A task completing after the old envelope grants no correction and validation preserves the replaced files exactly.
    /// </returns>
    [Fact]
    public async Task OriginalSpiritualStagedFrontier_IssuedARejectsReplacementPrivatePair()
    {
        await using var context = await CreateSpiritualStagedContinuationContextAsync();
        SpiritualWoundContinuationRequest issuedA;
        await using (var lease = await context.FileSystem.AcquireCanonicalWriteLeaseAsync())
            issuedA = Assert.IsType<SpiritualWoundContinuationRequest>((await context.Validator.ReadSpiritualWoundContinuationAsync(lease)).Request);
        await using var foreign = await CreateSpiritualStagedContinuationContextAsync();
        var foreignImages = new Dictionary<string, byte[]>(StringComparer.Ordinal);
        await using (var foreignLease = await foreign.FileSystem.AcquireCanonicalWriteLeaseAsync())
        {
            var foreignRequest = Assert.IsType<SpiritualWoundContinuationRequest>((await foreign.Validator.ReadSpiritualWoundContinuationAsync(foreignLease)).Request);
            Assert.NotEqual(issuedA.ContinuationId, foreignRequest.ContinuationId);
            foreach (var path in new[] { SpiritualWoundCaptureCheckpointState.StatePath,
                SpiritualWoundDecisionPendingState.StatePath, AcceptedMechanicsPlan.WoundCommandPath })
                foreignImages[path] = (await foreign.FileSystem.ReadFileBytesAsync(foreignLease, path))!;
        }
        await using var currentLease = await context.FileSystem.AcquireCanonicalWriteLeaseAsync();
        foreach (var pair in foreignImages)
            await context.FileSystem.WriteFileAtomicBytesAsync(currentLease, pair.Key, pair.Value);
        var before = await ReadSpiritualContinuationTransportFilesAsync(context);
        var candidate = CreateSpiritualStagedCorrectionA(Assert.IsType<JsonObject>(await context.ReadJsonAsync(AfterlifeSpiritualConflictState.StatePath)));
        var validator = new ValidationService(context.FileSystem, NullLogger<ValidationService>.Instance);
        var issues = await validator.ValidateSpiritualWoundContinuationDraftAsync(currentLease, issuedA,
            new SpiritualWoundContinuationResponse { ContinuationId = issuedA.ContinuationId, WoundDecisions = [] },
            new Dictionary<string, byte[]?> { [AfterlifeSpiritualConflictState.StatePath] = Encoding.UTF8.GetBytes(candidate.ToJsonString()) });
        Assert.Contains(issues, issue => issue.Severity == IssueSeverity.Error);
        await AssertSpiritualContinuationTransportFilesUnchangedAsync(context, before);
    }
}
