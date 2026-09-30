using System.Reflection;
using System.Text.Json.Nodes;
using BookOfEternityClient.Core;
using BookOfEternityClient.Services;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed partial class AfterlifeResourceCutoverTests
{
    /// <summary>
    /// Keeps the retained effect carrier in the named original attempt until the final physical freshness check.
    /// </summary>
    /// <param name="retainedPresent">
    /// Whether the retained original draft contains a valid player effect carrier before physical B changes its presence.
    /// </param>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task SpiritualEffectCurrentReadView_RejectsPhysicalChangeAtFinalGuard(
        bool retainedPresent)
    {
        const string changedPhysical = "{malformed-effect-carrier-B";
        const string changedPath = EffectCarrierCatalog.PlayerPath;
        const string watchedPath = CanonicalResourceOwnerAuthorityComposer.AuthorityPath;
        ResourceMaterializationTestContext? context = null;
        var armed = false;
        var watchedReads = 0;
        var hooks = new FileSystemManagerHooks
        {
            BeforeCanonicalReadOpenAsync = path =>
            {
                if (armed && string.Equals(path.Replace('\\', '/'), watchedPath,
                        StringComparison.Ordinal))
                {
                    watchedReads++;
                    if (watchedReads == 2)
                    {
                        var physicalPath = context!.FileSystem.ResolvePath(changedPath);
                        if (retainedPresent)
                            File.Delete(physicalPath);
                        else
                            File.WriteAllText(physicalPath, changedPhysical);
                    }
                }
                return Task.CompletedTask;
            }
        };
        await using (context = await CreateCompleteConflictFrameContextAsync(
                         hooks, async fixture =>
                         {
                             await SeedOriginalIntakeBaselinesAsync(fixture);
                             if (retainedPresent)
                                 await fixture.WriteExactJsonAsync(changedPath,
                                     new JsonObject
                                     {
                                         ["schemaVersion"] = EffectMaterializationContract.SchemaVersion,
                                         ["activeEffects"] = new JsonArray()
                                     }.ToJsonString());
                         }))
        {
            await WriteCompleteConflictFrameExchangeAsync(context);
            await WriteOriginalIntakeDraftAsync(context);
            AssertNoConflictFrameErrors(await context.Validator.ValidateAcceptedTurnRawMortalLocationMaterializationAsync());
            AssertNoConflictFrameErrors(await context.Validator.ValidateAcceptedTurnRawMortalItemMaterializationAsync());
            await using var lease = await context.FileSystem.AcquireCanonicalWriteLeaseAsync();
            armed = true;

            var rejected = await context.Validator.CaptureSpiritualOriginalTurnWithIntakeAsync(lease);

            Assert.True(watchedReads >= 2);
            Assert.Null(rejected.Capture);
            Assert.Contains(rejected.Issues,
                issue => issue.Code == "spiritual_original_input_changed" &&
                         issue.FilePath == changedPath);
            Assert.DoesNotContain(rejected.Issues,
                issue => issue.Code == "effect_materialization_invalid_carrier_root");
            if (retainedPresent)
                Assert.Null(await context.FileSystem.ReadFileAsync(lease, changedPath));
            else
            {
                Assert.Equal(changedPhysical, await context.FileSystem.ReadFileAsync(lease, changedPath));
                Assert.Contains(await context.Validator.ValidateAcceptedTurnRawEffectMaterializationAsync(lease),
                    issue => issue.Code == "effect_materialization_invalid_carrier_root" &&
                             issue.FilePath == changedPath);
            }
        }
    }

    /// <summary>
    /// Rejects a foreign named effect view and an incomplete selected path inventory before candidate reads.
    /// </summary>
    [Fact]
    public async Task SpiritualEffectCurrentReadView_RejectsForeignAndIncompleteDraft()
    {
        await using var context = await CreateCompleteConflictFrameContextAsync(
            seedOriginalInputs: SeedOriginalIntakeBaselinesAsync);
        await WriteCompleteConflictFrameExchangeAsync(context);
        await WriteOriginalIntakeDraftAsync(context);
        AssertNoConflictFrameErrors(await context.Validator.ValidateAcceptedTurnRawMortalLocationMaterializationAsync());
        AssertNoConflictFrameErrors(await context.Validator.ValidateAcceptedTurnRawMortalItemMaterializationAsync());
        await using var lease = await context.FileSystem.AcquireCanonicalWriteLeaseAsync();
        var captured = await context.Validator.CaptureSpiritualOriginalTurnWithIntakeAsync(lease);
        AssertNoConflictFrameErrors(captured.Issues);
        using var capture = Assert.IsType<ValidationService.SpiritualOriginalTurnCapture>(captured.Capture);
        var draft = Assert.IsType<SpiritualOriginalDraftInputs>(OriginalCaptureField(capture, "_draftInputs"));
        var manifest = await ReadGenuinelyValidatedConflictFrameManifestAsync(context.Validator);
        var method = Assert.Single(typeof(ValidationService).GetMethods(
            BindingFlags.Instance | BindingFlags.NonPublic), candidate =>
            candidate.Name == "ValidateAcceptedTurnRawEffectMaterializationAsync" &&
            candidate.GetParameters().Length == 12);
        var images = draft.PathInventory.ToDictionary(path => path, draft.ReadImage, StringComparer.Ordinal);
        Assert.True(EffectAcceptedTurnPlanAuthority.TryPeekValidated(context.FileSystem,
            lease, out _));

        foreach (var (session, request, snapshot, turn) in new[]
                 {
                     (draft.SessionId + "_other", draft.RequestId, draft.SnapshotToken, draft.Turn),
                     (draft.SessionId, draft.RequestId + "_other", draft.SnapshotToken, draft.Turn),
                     (draft.SessionId, draft.RequestId, draft.SnapshotToken + "_other", draft.Turn),
                     (draft.SessionId, draft.RequestId, draft.SnapshotToken, draft.Turn + 1)
                 })
        {
            var foreign = SpiritualOriginalDraftInputs.Create(session, request, snapshot, turn,
                draft.PathInventory, images);
            var issues = new List<ValidationIssue>();
            var task = Assert.IsAssignableFrom<Task>(method.Invoke(context.Validator,
                [issues, null, null, null, false, manifest, lease, null, false, null, null, foreign]));
            await task;
            Assert.Contains(issues, issue => issue.Code == "spiritual_original_input_identity_mismatch");
            Assert.False(EffectAcceptedTurnPlanAuthority.TryPeekValidated(context.FileSystem,
                lease, out _));
        }

        var incompletePaths = draft.PathInventory
            .Where(path => path != EffectCarrierCatalog.PlayerPath).ToArray();
        var incomplete = SpiritualOriginalDraftInputs.Create(draft.SessionId, draft.RequestId,
            draft.SnapshotToken, draft.Turn, incompletePaths,
            incompletePaths.ToDictionary(path => path, draft.ReadImage, StringComparer.Ordinal));
        var missingIssues = new List<ValidationIssue>();
        var missingTask = Assert.IsAssignableFrom<Task>(method.Invoke(context.Validator,
            [missingIssues, null, null, null, false, manifest, lease, null, false, null, null,
                incomplete]));
        await missingTask;
        Assert.Contains(missingIssues,
            issue => issue.Code == "spiritual_original_effect_input_unregistered");
    }
}
