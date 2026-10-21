using System.Text.Json;
using BookOfEternityClient.Models;
using BookOfEternityClient.Services;

namespace BookOfEternityClient.Core;

public partial class GameEngine
{
    /// <summary>
    /// Identifies an observed ordinary original without granting snapshot or publication authority.
    /// </summary>
    /// <param name="SessionId">
    /// The session identifier read from the authenticated original.
    /// </param>
    /// <param name="RequestId">
    /// The request identifier read from the authenticated original.
    /// </param>
    /// <param name="SnapshotToken">
    /// The authenticated manifest payload hash.
    /// </param>
    /// <param name="Turn">
    /// The turn number read from the authenticated original.
    /// </param>
    /// <param name="Generation">
    /// The existing physical session generation observed under the caller's lease.
    /// </param>
    internal sealed record JointComponentOriginal(
        string SessionId,
        string RequestId,
        string SnapshotToken,
        int Turn,
        string Generation);

    /// <summary>
    /// Creates an ordinary original through the engine's backup and snapshot producers for component work.
    /// This operation does not create a hot staging owner or enter the game loop.
    /// Failure may leave preparation artifacts for the caller to retain or clean; no rollback completion is implied.
    /// </summary>
    /// <param name="lease">
    /// The active ordinary canonical write lease owned and eventually closed by the caller.
    /// </param>
    /// <param name="request">
    /// The complete request with nonempty session and request identifiers, a positive turn number,
    /// and non-null progression control. The request is serialized and detached before preparation.
    /// </param>
    /// <returns>
    /// Data coordinates of the newly written, authenticated current ordinary original.
    /// </returns>
    internal async Task<JointComponentOriginal> PrepareOrdinaryOriginalForComponentAsync(
        FileSystemManager.CanonicalWriteLease lease,
        TurnRequest request)
    {
        RequireJointComponentSetupEntry(lease);
        ArgumentNullException.ThrowIfNull(request);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.SessionId);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.RequestId);
        if (request.TurnNumber < 1)
            throw new ArgumentOutOfRangeException(nameof(request), "An ordinary original requires a positive turn number.");
        if (request.ProgressionControl is null)
            throw new ArgumentException("A joint component original requires progression control.", nameof(request));

        var requestJson = JsonSerializer.Serialize(request, JsonOpts);
        var detachedRequest = JsonSerializer.Deserialize<TurnRequest>(requestJson, JsonOpts) ??
            throw new InvalidDataException("The component request could not be detached.");
        var generation = _fs.ReadLocalGenerationSnapshot(lease).Binding.Id ??
            throw new InvalidDataException("A joint component original requires an existing session generation.");
        var backup = await CreatePreTurnBackup(lease, Guid.NewGuid().ToString("N"));
        RegisterOrdinaryTurnStagingValidationSnapshotFiles(backup);
        await CreateCanonicalBaselineSnapshotAsync(
            lease, detachedRequest, backup, OrdinaryPlayerTurnSourceLabel);
        await _fs.WriteFileAtomicAsync(lease, LiveTurnPreparationService.TurnRequestPath, requestJson);

        var frame = await LoadValidatedPendingTurnSnapshotContextAsync(
            await LoadPendingTurnSnapshotManifestAsync(), requireCurrentContext: true) ??
            throw new InvalidDataException("The component original failed current snapshot authentication.");
        var original = new JointComponentOriginal(detachedRequest.SessionId, detachedRequest.RequestId,
            frame.Payload.ManifestPayloadHash, detachedRequest.TurnNumber, generation);
        RequireJointComponentOriginalAgreement(lease, original, frame);
        return original;
    }

    /// <summary>
    /// Authenticates the current ordinary original and invokes the existing complete mechanical admission.
    /// </summary>
    /// <param name="lease">
    /// The active ordinary canonical write lease owned by the caller.
    /// </param>
    /// <param name="expected">
    /// Non-null expected coordinates used only to compare the freshly authenticated original.
    /// </param>
    /// <param name="bundle">
    /// The non-null complete wound stages produced by the actual joint preparation.
    /// </param>
    /// <param name="continuation">
    /// The non-null continuation that the unchanged mechanical factory authenticates against the stages.
    /// </param>
    /// <returns>
    /// The genuine mechanical admission issued by the existing engine factory under this lease.
    /// </returns>
    internal async Task<MortalJointRepairMechanicalAdmission> CreateJointMechanicalAdmissionForComponentAsync(
        FileSystemManager.CanonicalWriteLease lease,
        JointComponentOriginal expected,
        AcceptedMechanicsWoundStageBundle bundle,
        object continuation)
    {
        RequireJointComponentSetupEntry(lease);
        ArgumentNullException.ThrowIfNull(bundle);
        ArgumentNullException.ThrowIfNull(continuation);
        var frame = await LoadJointComponentOriginalFrameAsync(lease, expected);
        return await CreateMortalJointRepairMechanicalAdmissionAsync(lease, frame, bundle, continuation);
    }

    /// <summary>
    /// Authenticates the current ordinary original and invokes the existing complete progression capture.
    /// </summary>
    /// <param name="lease">
    /// The active ordinary canonical write lease owned by the caller.
    /// </param>
    /// <param name="expected">
    /// Non-null expected coordinates used only to compare the freshly authenticated original.
    /// </param>
    /// <returns>
    /// The genuine progression source issued by the existing engine factory under this lease.
    /// </returns>
    internal async Task<MortalJointProgressionSource> CaptureJointProgressionForComponentAsync(
        FileSystemManager.CanonicalWriteLease lease,
        JointComponentOriginal expected)
    {
        var frame = await LoadJointComponentOriginalFrameAsync(lease, expected);
        return await CaptureMortalJointProgressionSourceAsync(lease, frame);
    }

    /// <summary>
    /// Requires ordinary lease ownership and refuses interference with a live original or publication owner.
    /// </summary>
    /// <param name="lease">
    /// The active ordinary canonical write lease belonging to this engine's filesystem.
    /// </param>
    private void RequireJointComponentSetupEntry(FileSystemManager.CanonicalWriteLease lease)
    {
        ArgumentNullException.ThrowIfNull(lease);
        _fs.EnsureCanonicalWriteLeaseActive(lease);
        RequireOrdinaryJointRepairLease(lease);
        if (_hotOrdinaryOriginal is { State: not "Ended" } ||
            _mortalJointPublicationAttempt is { Live: true })
            throw new InvalidOperationException("Joint component setup cannot replace or borrow a live engine owner.");
    }

    /// <summary>
    /// Reloads a private authenticated frame and compares the caller's data coordinates against it.
    /// </summary>
    /// <param name="lease">
    /// The active ordinary canonical write lease owned by the caller.
    /// </param>
    /// <param name="expected">
    /// Non-null data coordinates that confer no authority to construct or replace a frame.
    /// </param>
    /// <returns>
    /// The private frame authenticated from the actual manifest, authority, source bytes and current request.
    /// </returns>
    private async Task<ValidatedPendingTurnSnapshotContext> LoadJointComponentOriginalFrameAsync(
        FileSystemManager.CanonicalWriteLease lease,
        JointComponentOriginal expected)
    {
        RequireJointComponentSetupEntry(lease);
        ArgumentNullException.ThrowIfNull(expected);
        var frame = await LoadValidatedPendingTurnSnapshotContextAsync(
            await LoadPendingTurnSnapshotManifestAsync(), requireCurrentContext: true) ??
            throw new InvalidDataException("The current component original failed snapshot authentication.");
        RequireJointComponentOriginalAgreement(lease, expected, frame);
        return frame;
    }

    /// <summary>
    /// Compares ordinary source identity and physical generation after actual snapshot authentication.
    /// </summary>
    /// <param name="lease">
    /// The active ordinary canonical write lease owned by the caller.
    /// </param>
    /// <param name="expected">
    /// The data coordinates expected by the component caller.
    /// </param>
    /// <param name="frame">
    /// The actual private frame returned by current snapshot authentication.
    /// </param>
    private void RequireJointComponentOriginalAgreement(
        FileSystemManager.CanonicalWriteLease lease,
        JointComponentOriginal expected,
        ValidatedPendingTurnSnapshotContext frame)
    {
        RequireJointComponentSetupEntry(lease);
        if (frame.SourceLabel != OrdinaryPlayerTurnSourceLabel ||
            string.IsNullOrWhiteSpace(expected.SessionId) ||
            string.IsNullOrWhiteSpace(expected.RequestId) ||
            string.IsNullOrWhiteSpace(expected.SnapshotToken) ||
            expected.Turn < 1 ||
            frame.SessionId != expected.SessionId ||
            frame.RequestId != expected.RequestId ||
            frame.Payload.ManifestPayloadHash != expected.SnapshotToken ||
            frame.TurnNumber != expected.Turn ||
            !_fs.IsCurrentSessionGeneration(lease, expected.Generation))
            throw new InvalidDataException("The component coordinates do not match the current ordinary original and generation.");
    }
}
