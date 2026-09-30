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
    /// Requires an actual failed binding response before a separate terminal-control response, preserving mixed-cost validation and cold history.
    /// </summary>
    /// <returns>
    /// A task completing after real A acceptance, cold B recovery and ordinary validation of the cumulative corrected draft.
    /// </returns>
    [Fact]
    public Task OriginalSpiritualC2_LastBindingClosesAThenSeparatelyCorrectsFinalControl() =>
        VerifyOriginalLastBindingAsync(strong: false, alternative: "mixed", continueFinalControl: true);

    /// <summary>
    /// Preserves wrapper precedence while force binding loses dominance and closes through its own terminal response.
    /// </summary>
    /// <returns>
    /// A task completing after actual A/B admission and cold journal verification on the replacement carrier.
    /// </returns>
    [Fact]
    public Task OriginalSpiritualC2_ForceBindingWrapperClosesAThenFinalControl() =>
        VerifyOriginalLastBindingAsync(strong: true, alternative: "lost", continueFinalControl: true, wrapper: true);

    /// <summary>
    /// Exercises the existing public continuation protocol with a real saved wound and separately authored A/B drafts.
    /// </summary>
    /// <param name="context">
    /// Isolated original-valid two-exchange fixture whose mixed wound has already been saved.
    /// </param>
    /// <param name="lease">
    /// Active lease retained through validation and private progress persistence.
    /// </param>
    /// <param name="correctedA">
    /// Exact failed-result, position and cost correction preserving the original successful terminal echo.
    /// </param>
    /// <param name="selectedCommand">
    /// Exact command bytes that must survive both replies and cold reconstruction.
    /// </param>
    /// <param name="preserved">
    /// Original canonical resources, wound roots, request and receipt images that must remain unpublished.
    /// </param>
    /// <param name="wrapper">
    /// Whether the final echo and last exchange belong to the replacement wrapper carrier.
    /// </param>
    /// <param name="mixed">
    /// Whether A independently permits the additional cost fields exercised by the invalid-payment check.
    /// </param>
    /// <returns>
    /// A task completing after the separate responses resolve, or a failing assertion at the unimplemented boundary.
    /// </returns>
    private static async Task VerifyBindingFinalControlAsync(ResourceMaterializationTestContext context,
        FileSystemManager.CanonicalWriteLease lease, JsonObject correctedA, byte[] selectedCommand,
        IReadOnlyDictionary<string, byte[]?> preserved, bool wrapper, bool mixed)
    {
        var validator = new ValidationService(context.FileSystem, NullLogger<ValidationService>.Instance);
        var issued = await validator.ReadSpiritualWoundContinuationAsync(lease);
        Assert.Equal("dependent_draft", issued.Disposition);
        var a = Assert.IsType<SpiritualWoundContinuationRequest>(issued.Request);
        Assert.Equal(mixed ? 7 : 5, a.DependentDraftFields.Count);
        var responseA = CreateSpiritualStagedEmptyResponse(a);
        var checkpointBefore = await context.FileSystem.ReadFileBytesAsync(lease, SpiritualWoundCaptureCheckpointState.StatePath);
        var pendingBefore = await context.FileSystem.ReadFileBytesAsync(lease, SpiritualWoundDecisionPendingState.StatePath);

        // These numeric leaves are permitted, but only ordinary validation can reject the wrong payment.
        if (mixed)
        {
            var invalidCost = correctedA.DeepClone().AsObject();
            var cost = BindingRawActive(invalidCost, wrapper)["exchangeLog"]![1]!["actionCostAudit"]!["player"]!;
            cost["effectiveCost"] = 2;
            cost["after"] = 1;
            await WriteDraftAsync(invalidCost);
            var invalid = await validator.EvaluateSpiritualWoundContinuationDraftAsync(lease, a, responseA);
            Assert.Equal(ValidationService.SpiritualWoundContinuationDisposition.Rejected, invalid.Disposition);
            Assert.Equal(checkpointBefore, await context.FileSystem.ReadFileBytesAsync(lease, SpiritualWoundCaptureCheckpointState.StatePath));
        }

        await WriteDraftAsync(correctedA);
        var advanced = await validator.EvaluateSpiritualWoundContinuationDraftAsync(lease, a, responseA);
        Assert.True(advanced.Disposition == ValidationService.SpiritualWoundContinuationDisposition.Advanced,
            advanced.Disposition + ": " + string.Join("\n", advanced.Issues.Select(issue => issue.Code + " " + issue.FilePath)));
        Assert.Null(advanced.NextRequest);
        Assert.True(JsonNode.DeepEquals(correctedA,
            JsonNode.Parse((await context.FileSystem.ReadFileBytesAsync(lease, AfterlifeSpiritualConflictState.StatePath))!)));
        Assert.Equal(checkpointBefore, await context.FileSystem.ReadFileBytesAsync(lease, SpiritualWoundCaptureCheckpointState.StatePath));
        Assert.Equal(JsonSerializer.Serialize(a), JsonSerializer.Serialize((await validator.ReadSpiritualWoundContinuationAsync(lease)).Request));

        var committed = await validator.CommitSpiritualWoundDependentProgressAsync(lease, a, responseA);
        Assert.True(committed.Disposition == "committed", committed.Disposition + ": " + string.Join("\n", committed.Issues));
        var b = Assert.IsType<SpiritualWoundContinuationRequest>(committed.NextRequest);
        Assert.NotEqual(a.ContinuationId, b.ContinuationId);
        Assert.Equal(wrapper ? "/afterlifeSpiritualConflictUpdate/activeConflictAfter/controlState" :
            "/activeConflict/controlState", Assert.Single(b.DependentDraftFields).JsonPointer);
        Assert.Equal(AfterlifeSpiritualConflictState.StatePath, b.DependentDraftFields[0].Path);
        Assert.Null(b.Offer);
        Assert.Equal("dependent_draft", b.Phase);
        var checkpointAfterA = await context.FileSystem.ReadFileBytesAsync(lease, SpiritualWoundCaptureCheckpointState.StatePath);
        var progress = JsonNode.Parse(checkpointAfterA!)!["checkpoint"]!["pendingSubmission"]!["dependentDraftProgress"]!.AsArray();
        Assert.Single(progress);
        Assert.Equal(a.ContinuationId, progress[0]!["acceptedContinuationId"]!.GetValue<string>());
        Assert.Equal(pendingBefore, await context.FileSystem.ReadFileBytesAsync(lease, SpiritualWoundDecisionPendingState.StatePath));

        var cold = new ValidationService(context.FileSystem, NullLogger<ValidationService>.Instance);
        var recovered = await cold.ReadSpiritualWoundContinuationAsync(lease);
        Assert.Equal(JsonSerializer.Serialize(b), JsonSerializer.Serialize(recovered.Request));
        Assert.Equal(JsonSerializer.Serialize(a), JsonSerializer.Serialize(recovered.AcceptedRequest));
        var responseB = CreateSpiritualStagedEmptyResponse(b);
        Assert.NotEmpty(SpiritualWoundContinuationProtocol.ValidateResponse(b, responseA));
        var correctedB = correctedA.DeepClone().AsObject();
        var finalActive = BindingRawActive(correctedB, wrapper);
        finalActive["controlState"] = finalActive["exchangeLog"]![1]!["after"]!["controlState"]!.DeepClone();
        var changedA = correctedB.DeepClone().AsObject();
        BindingRawActive(changedA, wrapper)["exchangeLog"]![1]!["outcome"] = "blocked";
        Assert.NotEmpty(await cold.ValidateSpiritualWoundContinuationDraftAsync(lease, b, responseB,
            new Dictionary<string, byte[]?> { [AfterlifeSpiritualConflictState.StatePath] = Encoding.UTF8.GetBytes(changedA.ToJsonString()) }));
        await WriteDraftAsync(correctedB);
        var resolved = await cold.EvaluateSpiritualWoundContinuationDraftAsync(lease, b, responseB);
        Assert.True(resolved.Disposition == ValidationService.SpiritualWoundContinuationDisposition.Resolved,
            resolved.Disposition + ": " + string.Join("\n", resolved.Issues));
        Assert.Empty(resolved.Issues);
        Assert.Null(resolved.NextRequest);
        Assert.Equal(checkpointAfterA, await context.FileSystem.ReadFileBytesAsync(lease, SpiritualWoundCaptureCheckpointState.StatePath));
        Assert.Equal(selectedCommand, await context.FileSystem.ReadFileBytesAsync(lease, AcceptedMechanicsPlan.WoundCommandPath));
        foreach (var image in preserved)
            Assert.Equal(image.Value, await context.FileSystem.ReadFileBytesAsync(lease, image.Key));

        Task WriteDraftAsync(JsonObject draft) => context.FileSystem.WriteFileAtomicBytesAsync(lease,
            AfterlifeSpiritualConflictState.StatePath, Encoding.UTF8.GetBytes(draft.ToJsonString()));
    }
}
