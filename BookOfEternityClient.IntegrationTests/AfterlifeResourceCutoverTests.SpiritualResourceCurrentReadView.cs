using System.Reflection;
using BookOfEternityClient.Core;
using BookOfEternityClient.Services;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed partial class AfterlifeResourceCutoverTests
{
    /// <summary>
    /// Keeps the original resource root through admission and rejects physical B at the final freshness guard.
    /// </summary>
    /// <param name="changedPath">
    /// Current resource or afterlife owner root changed after its original image was frozen.
    /// </param>
    /// <param name="earlyFailureCode">
    /// Diagnostic that would reveal an inadmissible physical B read during resource composition.
    /// </param>
    [Theory]
    [InlineData(ResourceMaterializationContract.DefinitionsPath, "resource_definition_root_invalid")]
    [InlineData(AfterlifeEntityProfileState.StatePath, "resource_owner_afterlife_root_invalid")]
    [InlineData("game_state/meta/guardians.json", "resource_owner_afterlife_root_invalid")]
    public async Task SpiritualResourceCurrentReadView_RejectsPhysicalChangeAtFinalGuard(
        string changedPath, string earlyFailureCode)
    {
        const string changedPhysical = "{malformed-resource-B";
        ResourceMaterializationTestContext? context = null;
        var armed = false;
        var frozenTarget = false;
        var changed = false;
        var hooks = new FileSystemManagerHooks
        {
            BeforeCanonicalReadOpenAsync = path =>
            {
                if (!armed)
                    return Task.CompletedTask;
                var logicalPath = path.Replace('\\', '/');
                if (logicalPath == changedPath ||
                    changedPath == "game_state/meta/guardians.json" &&
                    logicalPath == ResourceMaterializationContract.DefinitionsPath)
                    frozenTarget = true;
                if (frozenTarget && !changed &&
                    logicalPath == LiveTurnPreparationService.PendingTurnSnapshotManifestPath)
                {
                    File.WriteAllText(context!.FileSystem.ResolvePath(changedPath), changedPhysical);
                    changed = true;
                }
                return Task.CompletedTask;
            }
        };
        await using (context = await CreateCompleteConflictFrameContextAsync(
                         hooks, SeedOriginalIntakeBaselinesAsync))
        {
            await WriteCompleteConflictFrameExchangeAsync(context);
            await WriteOriginalIntakeDraftAsync(context);
            AssertNoConflictFrameErrors(await context.Validator.ValidateAcceptedTurnRawMortalLocationMaterializationAsync());
            AssertNoConflictFrameErrors(await context.Validator.ValidateAcceptedTurnRawMortalItemMaterializationAsync());
            await using var lease = await context.FileSystem.AcquireCanonicalWriteLeaseAsync();
            if (changedPath == "game_state/meta/guardians.json")
                Assert.Null(await context.FileSystem.ReadFileAsync(lease, changedPath));
            armed = true;

            var rejected = await context.Validator.CaptureSpiritualOriginalTurnWithIntakeAsync(lease);

            Assert.True(frozenTarget);
            Assert.True(changed);
            Assert.Null(rejected.Capture);
            Assert.Contains(rejected.Issues, issue =>
                issue.Code == "spiritual_original_input_changed" && issue.FilePath == changedPath);
            Assert.DoesNotContain(rejected.Issues, issue => issue.Code == earlyFailureCode);
            Assert.Equal(changedPhysical, await context.FileSystem.ReadFileAsync(lease, changedPath));
            Assert.Contains(await context.Validator.ValidateAcceptedTurnRawResourceMaterializationAsync(lease),
                issue => issue.FilePath == changedPath &&
                         issue.Code != "spiritual_original_input_changed");
        }
    }

    /// <summary>
    /// Rejects a foreign resource view and missing selected current path before composing a plan.
    /// </summary>
    [Fact]
    public async Task SpiritualResourceCurrentReadView_RejectsForeignAndIncompleteDraft()
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
        var sinkType = Assert.IsAssignableFrom<Type>(typeof(ValidationService).GetNestedType(
            "SpiritualOriginalInputSink", BindingFlags.NonPublic));
        var currentField = Assert.IsAssignableFrom<FieldInfo>(sinkType.GetField("CurrentInputs",
            BindingFlags.Instance | BindingFlags.NonPublic));
        var method = Assert.IsAssignableFrom<MethodInfo>(typeof(ValidationService).GetMethod(
            "ValidateAcceptedTurnRawResourceMaterializationCoreAsync",
            BindingFlags.Instance | BindingFlags.NonPublic));
        var images = draft.PathInventory.ToDictionary(path => path, draft.ReadImage, StringComparer.Ordinal);

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
            var sink = Activator.CreateInstance(sinkType, nonPublic: true)!;
            currentField.SetValue(sink, foreign);
            var task = Assert.IsAssignableFrom<Task<IReadOnlyList<ValidationIssue>>>(method.Invoke(
                context.Validator, [lease, sink, null]));
            var issues = await task;
            Assert.Contains(issues, issue => issue.Code == "spiritual_original_input_identity_mismatch");
        }

        var incompletePaths = draft.PathInventory
            .Where(path => path != ResourceMaterializationContract.DefinitionsPath).ToArray();
        var incomplete = SpiritualOriginalDraftInputs.Create(draft.SessionId, draft.RequestId,
            draft.SnapshotToken, draft.Turn, incompletePaths,
            incompletePaths.ToDictionary(path => path, draft.ReadImage, StringComparer.Ordinal));
        var incompleteSink = Activator.CreateInstance(sinkType, nonPublic: true)!;
        currentField.SetValue(incompleteSink, incomplete);
        var incompleteTask = Assert.IsAssignableFrom<Task<IReadOnlyList<ValidationIssue>>>(method.Invoke(
            context.Validator, [lease, incompleteSink, null]));
        Assert.Contains(await incompleteTask,
            issue => issue.Code == "spiritual_original_resource_input_unregistered");
    }
}
