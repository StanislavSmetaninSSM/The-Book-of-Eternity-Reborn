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
    internal Task<JointComponentOriginal> PrepareOrdinaryOriginalForComponentAsync(
        FileSystemManager.CanonicalWriteLease lease,
        TurnRequest request)
    {
        return Task.FromException<JointComponentOriginal>(new NotSupportedException("T383 component setup is not implemented yet."));
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
    internal Task<MortalJointRepairMechanicalAdmission> CreateJointMechanicalAdmissionForComponentAsync(
        FileSystemManager.CanonicalWriteLease lease,
        JointComponentOriginal expected,
        AcceptedMechanicsWoundStageBundle bundle,
        object continuation)
    {
        return Task.FromException<MortalJointRepairMechanicalAdmission>(new NotSupportedException("T383 component setup is not implemented yet."));
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
    internal Task<MortalJointProgressionSource> CaptureJointProgressionForComponentAsync(
        FileSystemManager.CanonicalWriteLease lease,
        JointComponentOriginal expected)
    {
        return Task.FromException<MortalJointProgressionSource>(new NotSupportedException("T383 component setup is not implemented yet."));
    }
}
