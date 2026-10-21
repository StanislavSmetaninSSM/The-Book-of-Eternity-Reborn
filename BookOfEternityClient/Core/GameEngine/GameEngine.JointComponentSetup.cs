using System.Text.Json;
using BookOfEternityClient.Models;
using BookOfEternityClient.Services;

namespace BookOfEternityClient.Core;

public partial class GameEngine
{
    /// <summary>
    /// Identifies the existing component entry refusal for an actually live engine owner.
    /// </summary>
    private const string JointComponentBusyOwnerMessage = "Joint component setup cannot replace or borrow a live engine owner.";

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
    /// Snapshot authentication reuses the caller's existing lease for every authority, source and context read.
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

        var frame = await LoadValidatedPendingTurnSnapshotContextCoreAsync(
            await LoadPendingTurnSnapshotManifestCoreAsync(lease), requireCurrentContext: true, writeLease: lease) ??
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
    /// Contains observations from a real unissued Beginning owner; this data grants no lifecycle or publication authority.
    /// </summary>
    /// <param name="Ordinal">
    /// The stage ordinal allocated to the actual Beginning owner.
    /// </param>
    /// <param name="Refusals">
    /// The operation names observed throwing the exact existing busy-owner refusal.
    /// </param>
    /// <param name="NoAuthorityIssued">
    /// Whether the actual owner's producer, evidence, preparation and custody fields remain unissued.
    /// </param>
    /// <param name="Ended">
    /// Whether that same current owner ended with its allocated stage ordinal unchanged.
    /// </param>
    /// <param name="ReadyUnchanged">
    /// Whether the initially absent ready observation and its ordinal remain unchanged.
    /// </param>
    internal sealed record BeginningOwnerComponentRefusalReport(long Ordinal, IReadOnlyList<string> Refusals,
        bool NoAuthorityIssued, bool Ended, bool ReadyUnchanged);

    /// <summary>
    /// Probes the actual Beginning-owner guard through three fixed component entries and one nested probe.
    /// The private owner never enters its producer, acquires custody or issues original evidence.
    /// Its own phase ends in finally; an existing failure keeps its stack and receives a data report.
    /// </summary>
    /// <param name="lease">
    /// The caller-owned active ordinary canonical lease, reused for every authentication and request read.
    /// </param>
    /// <param name="expected">
    /// The real ordinary-original coordinates compared with the authenticated current private frame.
    /// </param>
    /// <param name="bundle">
    /// The genuinely completed joint wound stages agreeing with the actual continuation and original.
    /// </param>
    /// <param name="continuation">
    /// The genuine joint continuation whose current private seal and source inputs are revalidated.
    /// </param>
    /// <param name="throwAfterProbes">
    /// Defaults to <see langword="false"/>. When <see langword="true"/>, deliberately throws
    /// <see cref="InvalidOperationException"/> after all refusals to verify the same finally path.
    /// Its data contains a <see cref="BeginningOwnerComponentRefusalReport"/> under that type's name.
    /// </param>
    /// <returns>
    /// Data observations after the owned phase ends. A finally invariant failure propagates on its own,
    /// or is attached to an already propagating failure without replacing that failure's stack.
    /// </returns>
    internal async Task<BeginningOwnerComponentRefusalReport> ProbeBeginningOwnerRefusalsForComponentAsync(
        FileSystemManager.CanonicalWriteLease lease, JointComponentOriginal expected,
        AcceptedMechanicsWoundStageBundle bundle, object continuation, bool throwAfterProbes = false)
    {
        RequireJointComponentSetupEntry(lease);
        ArgumentNullException.ThrowIfNull(bundle);
        ArgumentNullException.ThrowIfNull(continuation);
        if (_hotRepairReady is not null)
            throw new InvalidOperationException("Beginning component probing requires no retained ready observation.");
        var previousOrdinal = _hotStageOrdinal;
        var readyOrdinal = _hotReadyOrdinal;
        var frame = await LoadJointComponentOriginalFrameAsync(lease, expected);
        RequireJointComponentSetupEntry(lease);
        if (!ReferenceEquals(bundle.PreparedPlan.JointRepairContinuationAuthority, continuation) ||
            !WoundAcceptedTurnPlanner.JointRepairFinalPlanAgrees(bundle))
            throw new InvalidDataException("Beginning component probing requires the genuine complete joint stages.");
        var binding = bundle.Input.Binding;
        if (binding.Realm != "mortal_world" || binding.SessionId != frame.SessionId ||
            binding.RequestId != frame.RequestId || binding.SnapshotToken != frame.Payload.ManifestPayloadHash ||
            binding.Turn != frame.TurnNumber)
            throw new InvalidDataException("Beginning component probing requires the same authenticated ordinary original.");
        await WoundAcceptedTurnPlanner.ValidateJointRepairCurrentAsync(_fs, lease, continuation);
        RequireJointComponentSetupEntry(lease);
        var requestJson = await _fs.ReadFileAsync(lease, LiveTurnPreparationService.TurnRequestPath) ??
            throw new InvalidDataException("Beginning component probing requires its actual current request.");
        var request = JsonSerializer.Deserialize<TurnRequest>(requestJson, JsonOpts) ??
            throw new InvalidDataException("Beginning component probing requires a readable current request.");
        if (request.SessionId != expected.SessionId || request.RequestId != expected.RequestId ||
            request.TurnNumber != expected.Turn || request.ProgressionControl is null)
            throw new InvalidDataException("Beginning component probing requires the same complete current request.");
        RequireJointComponentOriginalAgreement(lease, expected, frame);
        if (_hotRepairReady is not null || _hotReadyOrdinal != readyOrdinal || _hotStageOrdinal != previousOrdinal)
            throw new InvalidOperationException("The component engine changed during Beginning probe admission.");

        var refusals = new List<string>();
        Exception? failure = null;
        RequireJointComponentSetupEntry(lease);
        var ordinal = ++_hotStageOrdinal;
        var owner = BeginHotOrdinaryOriginal(ordinal, expected.Generation, browserBinding: null);
        try
        {
            RequireBeginningOwnerProbeState(owner, ordinal, readyOrdinal, "Beginning");
            await ObserveBeginningOwnerRefusalAsync(nameof(PrepareOrdinaryOriginalForComponentAsync),
                PrepareOrdinaryOriginalForComponentAsync(lease, request), refusals);
            RequireBeginningOwnerProbeState(owner, ordinal, readyOrdinal, "Beginning");
            await ObserveBeginningOwnerRefusalAsync(nameof(CreateJointMechanicalAdmissionForComponentAsync),
                CreateJointMechanicalAdmissionForComponentAsync(lease, expected, bundle, continuation), refusals);
            RequireBeginningOwnerProbeState(owner, ordinal, readyOrdinal, "Beginning");
            await ObserveBeginningOwnerRefusalAsync(nameof(CaptureJointProgressionForComponentAsync),
                CaptureJointProgressionForComponentAsync(lease, expected), refusals);
            RequireBeginningOwnerProbeState(owner, ordinal, readyOrdinal, "Beginning");
            await ObserveBeginningOwnerRefusalAsync(nameof(ProbeBeginningOwnerRefusalsForComponentAsync),
                ProbeBeginningOwnerRefusalsForComponentAsync(lease, expected, bundle, continuation), refusals);
            RequireBeginningOwnerProbeState(owner, ordinal, readyOrdinal, "Beginning");
            if (throwAfterProbes)
                throw new InvalidOperationException("Deliberate Beginning-owner component probe failure.");
        }
        catch (Exception error)
        {
            failure = error;
            throw;
        }
        finally
        {
            owner.State = "Ended";
            try { RequireBeginningOwnerProbeState(owner, ordinal, readyOrdinal, "Ended"); }
            catch (Exception closingFailure)
            {
                if (failure is null) throw;
                failure.Data["BeginningOwnerProbeFinallyFailure"] = closingFailure;
            }
            if (failure is not null)
                failure.Data[nameof(BeginningOwnerComponentRefusalReport)] =
                    ReadBeginningOwnerProbeReport(owner, ordinal, readyOrdinal, refusals);
        }

        return ReadBeginningOwnerProbeReport(owner, ordinal, readyOrdinal, refusals);
    }

    /// <summary>
    /// Records only the actual fixed operation's exact busy-owner guard exception.
    /// </summary>
    /// <param name="name">
    /// The name of the actual component operation already invoked by the closed probe.
    /// </param>
    /// <param name="operation">
    /// Its already created task; this private observer supplies no callback, result or authority.
    /// </param>
    /// <param name="refusals">
    /// The private list receiving the name only after the existing guard actually refuses.
    /// </param>
    /// <returns>
    /// A task completing after the exact refusal; any other exception propagates unchanged.
    /// </returns>
    private static async Task ObserveBeginningOwnerRefusalAsync(string name, Task operation, List<string> refusals)
    {
        try { await operation; }
        catch (InvalidOperationException error) when (error.Message == JointComponentBusyOwnerMessage)
        {
            refusals.Add(name);
            return;
        }
        throw new InvalidOperationException("A Beginning component operation unexpectedly succeeded: " + name);
    }

    /// <summary>
    /// Checks the real private owner, stage counter and initially absent ready observation without changing them.
    /// </summary>
    /// <param name="owner">
    /// The actual unissued object created by the shared Beginning factory for this probe.
    /// </param>
    /// <param name="ordinal">
    /// Its allocated ordinal, which nested refusal must not change.
    /// </param>
    /// <param name="readyOrdinal">
    /// The ready-observation ordinal captured before probe authentication.
    /// </param>
    /// <param name="state">
    /// The required phase, fixed by the closed probe to Beginning or Ended.
    /// </param>
    private void RequireBeginningOwnerProbeState(HotOrdinaryOriginal owner, long ordinal, long readyOrdinal, string state)
    {
        if (!ReferenceEquals(_hotOrdinaryOriginal, owner) || _hotStageOrdinal != ordinal ||
            owner.State != state || _hotRepairReady is not null || _hotReadyOrdinal != readyOrdinal ||
            !HasNoBeginningOwnerIssuance(owner))
            throw new InvalidOperationException("The Beginning probe's actual owner, unissued fields or ready observation changed.");
    }

    /// <summary>
    /// Inspects the actual owner's producer, evidence and custody fields without exporting any of their values.
    /// </summary>
    /// <param name="owner">
    /// The genuine private Beginning owner retained by this engine.
    /// </param>
    /// <returns>
    /// <see langword="true"/> when no producer entry, evidence, preparation or custody was established;
    /// otherwise, <see langword="false"/>.
    /// </returns>
    private static bool HasNoBeginningOwnerIssuance(HotOrdinaryOriginal owner) =>
        !owner.ProducerEntered && !owner.CleanupConsumed && owner.BrowserBinding is null &&
        owner.ProducerRequest is null && owner.ProducerBackup is null && owner.GenerationBytes is null &&
        owner.ManifestBytes is null && owner.AuthorityBytes is null && owner.RequestBytes is null &&
        owner.SessionId is null && owner.RequestId is null && owner.SnapshotToken is null && owner.Turn == 0 &&
        owner.Preparation is null && owner.ConsoleOwner is null;

    /// <summary>
    /// Copies only observation data from the real owner and counters for a serialized component caller.
    /// </summary>
    /// <param name="owner">
    /// The genuine private owner created for this probe.
    /// </param>
    /// <param name="ordinal">
    /// The stage ordinal allocated to that owner.
    /// </param>
    /// <param name="readyOrdinal">
    /// The ready-observation ordinal captured before probe authentication.
    /// </param>
    /// <param name="refusals">
    /// Actual operation names observed refusing through the existing busy-owner guard.
    /// </param>
    /// <returns>
    /// Detached report data carrying no original, frame, issuer, custody or publication authority.
    /// </returns>
    private BeginningOwnerComponentRefusalReport ReadBeginningOwnerProbeReport(
        HotOrdinaryOriginal owner, long ordinal, long readyOrdinal, IReadOnlyList<string> refusals) =>
        new(owner.Ordinal, Array.AsReadOnly(refusals.ToArray()), HasNoBeginningOwnerIssuance(owner),
            ReferenceEquals(_hotOrdinaryOriginal, owner) && _hotStageOrdinal == ordinal && owner.State == "Ended",
            _hotRepairReady is null && _hotReadyOrdinal == readyOrdinal);

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
            throw new InvalidOperationException(JointComponentBusyOwnerMessage);
    }

    /// <summary>
    /// Reloads a private authenticated frame through the caller's existing lease and compares
    /// the caller's data coordinates against it.
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
        var frame = await LoadValidatedPendingTurnSnapshotContextCoreAsync(
            await LoadPendingTurnSnapshotManifestCoreAsync(lease), requireCurrentContext: true, writeLease: lease) ??
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
