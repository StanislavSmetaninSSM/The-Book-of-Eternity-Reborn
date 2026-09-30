using System.Text;
using BookOfEternityClient.Core;
using BookOfEternityClient.Services;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed partial class AfterlifeResourceCutoverTests
{
    /// <summary>
    /// Reads initial spiritual source candidates from the retained original draft while ordinary acquisition sees physical changes.
    /// </summary>
    [Fact]
    public async Task SpiritualSourceCurrentReadView_UsesRetainedCandidatesAndPhysicalOriginal()
    {
        await using var context = await CreateCompleteConflictFrameContextAsync(
            seedOriginalInputs: SeedCandidateQuestBaselinesAsync);
        await WriteCompleteConflictFrameExchangeAsync(context);
        await WriteOriginalIntakeDraftAsync(context);
        AssertNoConflictFrameErrors(await context.Validator.ValidateAcceptedTurnRawMortalLocationMaterializationAsync());
        AssertNoConflictFrameErrors(await context.Validator.ValidateAcceptedTurnRawMortalItemMaterializationAsync());
        await using var lease = await context.FileSystem.AcquireCanonicalWriteLeaseAsync();
        var captured = await context.Validator.CaptureSpiritualOriginalTurnWithIntakeAsync(lease);
        AssertNoConflictFrameErrors(captured.Issues);
        using var capture = Assert.IsType<ValidationService.SpiritualOriginalTurnCapture>(captured.Capture);
        var draft = Assert.IsType<SpiritualOriginalDraftInputs>(OriginalCaptureField(capture, "_draftInputs"));
        var conflictPath = AfterlifeSpiritualConflictState.StatePath;
        var absentPath = ShiningAbodeState.StatePath;
        Assert.True(draft.ReadImage(conflictPath).Existed);
        Assert.False(draft.ReadImage(absentPath).Existed);
        var originalRequest = await context.FileSystem.ReadFileBytesAsync(lease,
            LiveTurnPreparationService.TurnRequestPath);

        context.FileSystem.DeleteFile(lease, conflictPath);
        await context.FileSystem.WriteFileAtomicAsync(lease, absentPath, "{physical B");
        await context.FileSystem.WriteFileAtomicAsync(lease,
            ValidationService.SpiritualWoundSourceSession.SoulPath, "{physical B");

        var retained = await context.Validator.BeginLiveSpiritualWoundSourceSessionAsync(
            lease, awaitOriginalPrefix: true, currentInputs: draft);
        AssertNoConflictFrameErrors(retained.Issues);
        var retainedSource = Assert.IsType<ValidationService.SpiritualWoundSourceSession>(retained.Session);
        Assert.Equal(draft.ReadText(conflictPath), retainedSource.ReadCandidate(conflictPath));
        Assert.Null(retainedSource.ReadCandidate(absentPath));
        Assert.Equal(draft.ReadText(ValidationService.SpiritualWoundSourceSession.SoulPath),
            retainedSource.ReadCandidate(ValidationService.SpiritualWoundSourceSession.SoulPath));
        var strictUtf8 = new UTF8Encoding(false, true);
        foreach (var path in retainedSource.SelectedPaths)
        {
            var image = draft.ReadImage(path);
            Assert.Equal(image.Existed ? strictUtf8.GetString(image.Bytes!) : null,
                retainedSource.ReadCandidate(path));
        }
        var ordinary = await context.Validator.BeginLiveSpiritualWoundSourceSessionAsync(
            lease, awaitOriginalPrefix: true);
        AssertNoConflictFrameErrors(ordinary.Issues);
        var ordinarySource = Assert.IsType<ValidationService.SpiritualWoundSourceSession>(ordinary.Session);
        Assert.Null(ordinarySource.ReadCandidate(conflictPath));
        Assert.Equal("{physical B", ordinarySource.ReadCandidate(absentPath));
        Assert.Equal("{physical B", ordinarySource.ReadCandidate(
            ValidationService.SpiritualWoundSourceSession.SoulPath));
        Assert.Equal(retainedSource.ReadOriginal(conflictPath), ordinarySource.ReadOriginal(conflictPath));

        var images = draft.PathInventory.ToDictionary(path => path, draft.ReadImage, StringComparer.Ordinal);
        foreach (var (session, request, snapshot, turn) in new[]
                 {
                     (draft.SessionId + "_other", draft.RequestId, draft.SnapshotToken, draft.Turn),
                     (draft.SessionId, draft.RequestId + "_other", draft.SnapshotToken, draft.Turn),
                     (draft.SessionId, draft.RequestId, draft.SnapshotToken + "_other", draft.Turn),
                     (draft.SessionId, draft.RequestId, draft.SnapshotToken, draft.Turn + 1)
                 })
        {
            var mismatched = SpiritualOriginalDraftInputs.Create(session, request, snapshot, turn,
                draft.PathInventory, images);
            var rejected = await context.Validator.BeginLiveSpiritualWoundSourceSessionAsync(
                lease, awaitOriginalPrefix: true, currentInputs: mismatched);
            Assert.Null(rejected.Session);
            Assert.Contains(rejected.Issues,
                issue => issue.Code == "spiritual_original_input_identity_mismatch");
        }

        var incompletePaths = draft.PathInventory
            .Where(path => path != ValidationService.SpiritualWoundSourceSession.SoulPath).ToArray();
        var incomplete = SpiritualOriginalDraftInputs.Create(draft.SessionId, draft.RequestId,
            draft.SnapshotToken, draft.Turn, incompletePaths,
            incompletePaths.ToDictionary(path => path, draft.ReadImage, StringComparer.Ordinal));
        var missing = await context.Validator.BeginLiveSpiritualWoundSourceSessionAsync(
            lease, awaitOriginalPrefix: true, currentInputs: incomplete);
        Assert.Null(missing.Session);
        Assert.Contains(missing.Issues, issue => issue.Code == "spiritual_source_input_invalid");

        images[ValidationService.SpiritualWoundSourceSession.SoulPath] =
            new CanonicalBeforeImage(true, [0xff]);
        var invalidUtf8 = SpiritualOriginalDraftInputs.Create(draft.SessionId, draft.RequestId,
            draft.SnapshotToken, draft.Turn, draft.PathInventory, images);
        var malformed = await context.Validator.BeginLiveSpiritualWoundSourceSessionAsync(
            lease, awaitOriginalPrefix: true, currentInputs: invalidUtf8);
        Assert.Null(malformed.Session);
        Assert.Contains(malformed.Issues, issue => issue.Code == "spiritual_source_input_invalid");
        Assert.Equal(originalRequest, await context.FileSystem.ReadFileBytesAsync(lease,
            LiveTurnPreparationService.TurnRequestPath));
    }
}
