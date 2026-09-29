using BookOfEternityClient.Core;
using BookOfEternityClient.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed partial class AfterlifeResourceCutoverTests
{
    /// <summary>
    /// Keeps a declined wound silent while still binding the accepted scene against later replacement.
    /// </summary>
    /// <returns>
    /// A task completing after genuine decline publication retains an empty guarded presentation.
    /// </returns>
    [Fact]
    public async Task OriginalSpiritualC4_DeclineOutputStillRejectsDrift()
    {
        await using var context = await CreateCompleteConflictFrameContextAsync(
            seedOriginalInputs: SeedOriginalIntakeBaselinesAsync);
        await PrepareSpiritualC4PublicationAsync(context, materialize: false);
        var published = await AcceptedTurnCanonicalStateRefresh.NormalizeAndValidateWithPlanAsync(
            context.FileSystem, context.Normalizer, context.Validator, await ReadSpiritualC4BackupsAsync(context));
        AssertNoConflictFrameErrors(published.Issues);
        var output = Assert.IsType<SpiritualWoundPublishedOutput>(published.SpiritualWoundOutput);
        Assert.Empty(output.Notifications);
        await output.RequireCurrentAsync(context.FileSystem);
        await File.WriteAllTextAsync(context.FileSystem.ResolvePath(ProjectionNarrativePath), "{\"response\":\"Changed\"}");
        await Assert.ThrowsAsync<InvalidDataException>(() => output.RequireCurrentAsync(context.FileSystem));
    }

    /// <summary>
    /// Authors the selected narration in the physical scene before the C2 decision commits its input image.
    /// </summary>
    /// <param name="context">
    /// Original fixture whose output retains its existing sibling fields.
    /// </param>
    /// <returns>
    /// A task completing after the same scene supplied to SubmitDecisionAsync is physically present.
    /// </returns>
    private static async Task WriteSpiritualC4SelectedSceneAsync(ResourceMaterializationTestContext context)
    {
        var root = Assert.IsType<System.Text.Json.Nodes.JsonObject>(await context.ReadJsonAsync(ProjectionNarrativePath));
        root["response"] = "Чужое давление надломило волю хранителя.";
        root["timestamp"] = DateTime.UtcNow.ToString("O");
        await context.WriteExactJsonAsync(ProjectionNarrativePath, root.ToJsonString());
    }

    /// <summary>
    /// Restores signed original state when output changes during common publication before presentation is accepted.
    /// </summary>
    /// <returns>
    /// A task completing after real read-back rejects the substituted scene and rolls back all tracked images.
    /// </returns>
    [Fact]
    public async Task OriginalSpiritualC4_OutputDriftDuringPublicationRestoresSignedOriginal()
    {
        var armed = false;
        var wroteResources = false;
        var injected = false;
        string? narrativePath = null;
        var hooks = new FileSystemManagerHooks
        {
            BeforeCanonicalMutationAsync = path =>
            {
                if (armed && path == ResourceMaterializationContract.StatePath)
                    wroteResources = true;
                return Task.CompletedTask;
            },
            BeforeCanonicalReadOpenAsync = async path =>
            {
                if (armed && wroteResources && !injected && path == "output/narrative_response.json")
                {
                    injected = true;
                    armed = false;
                    await File.WriteAllTextAsync(narrativePath!, "{\"response\":\"Подменённая сцена.\"}");
                }
            }
        };
        await using var context = await CreateCompleteConflictFrameContextAsync(hooks,
            seedOriginalInputs: SeedOriginalIntakeBaselinesAsync);
        narrativePath = context.FileSystem.ResolvePath("output/narrative_response.json");
        var originals = new Dictionary<string, byte[]?>(StringComparer.Ordinal);
        foreach (var path in CanonicalStateNormalizer.NormalizerRollbackTrackedFiles.Concat(new[]
        {
            SpiritualWoundCaptureCheckpointState.StatePath, SpiritualWoundDecisionPendingState.StatePath,
            SpiritualWoundOpportunityReceiptState.StatePath, "output/narrative_response.json"
        }).Distinct(StringComparer.Ordinal))
            originals.Add(path, await context.FileSystem.ReadFileBytesAsync(path));
        await PrepareSpiritualC4PublicationAsync(context);
        var backups = await ReadSpiritualC4BackupsAsync(context);
        armed = true;
        await Assert.ThrowsAnyAsync<InvalidDataException>(() =>
            AcceptedTurnCanonicalStateRefresh.NormalizeAndValidateWithPlanAsync(
                context.FileSystem, context.Normalizer, context.Validator, backups));
        Assert.True(injected);
        foreach (var pair in originals)
            Assert.Equal(pair.Value, await context.FileSystem.ReadFileBytesAsync(pair.Key));
        await using var lease = await context.FileSystem.AcquireCanonicalWriteLeaseAsync();
        Assert.False(AcceptedMechanicsPlanAuthority.HasValidated(context.FileSystem, lease));
    }

    /// <summary>
    /// Retains one genuine publication's presentation while rejecting changed or foreign output at delivery.
    /// </summary>
    /// <returns>
    /// A task completing after the actual publisher produces a notification and every output witness is checked.
    /// </returns>
    [Fact]
    public async Task OriginalSpiritualC4_PublishedOutputRejectsDriftAndForeignDelivery()
    {
        await using var context = await CreateCompleteConflictFrameContextAsync(
            seedOriginalInputs: SeedOriginalIntakeBaselinesAsync);
        await PrepareSpiritualC4PublicationAsync(context);
        var published = await AcceptedTurnCanonicalStateRefresh.NormalizeAndValidateWithPlanAsync(
            context.FileSystem, context.Normalizer, context.Validator, await ReadSpiritualC4BackupsAsync(context));
        AssertNoConflictFrameErrors(published.Issues);
        var output = Assert.IsType<SpiritualWoundPublishedOutput>(published.SpiritualWoundOutput);
        Assert.Empty(output.Issues);
        Assert.Contains("Получена духовная рана", Assert.Single(output.Notifications).Text.PlainText,
            StringComparison.Ordinal);
        await output.RequireCurrentAsync(context.FileSystem);
        var foreign = new FileSystemManager(context.RootPath, NullLogger<FileSystemManager>.Instance);
        await Assert.ThrowsAsync<InvalidDataException>(() => output.RequireCurrentAsync(foreign));
        foreach (var path in WoundAcceptedTurnSnapshotContract.OutputPaths)
        {
            var original = await context.FileSystem.ReadFileBytesAsync(path);
            await File.WriteAllTextAsync(context.FileSystem.ResolvePath(path), "{\"response\":\"Changed after publication\"}");
            await Assert.ThrowsAsync<InvalidDataException>(() => output.RequireCurrentAsync(context.FileSystem));
            if (original is null)
                File.Delete(context.FileSystem.ResolvePath(path));
            else
                await File.WriteAllBytesAsync(context.FileSystem.ResolvePath(path), original);
        }
        await output.RequireCurrentAsync(context.FileSystem);
    }
}
