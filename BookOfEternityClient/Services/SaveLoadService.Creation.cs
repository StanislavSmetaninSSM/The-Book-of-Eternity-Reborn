using BookOfEternityClient.Core;
using Microsoft.Extensions.Logging;

namespace BookOfEternityClient.Services;

/// <summary>
/// Distinguishes preparation rejection from a durable or unresolved archive publication decision.
/// </summary>
internal enum SaveCreationDisposition { NotCreated, Committed, RolledBack, Uncertain }

/// <summary>
/// Retains an established save decision through private cleanup, logging and other follow-up failures.
/// </summary>
/// <param name="Disposition">
/// The established publication outcome; follow-up failures never reverse a committed decision.
/// </param>
/// <param name="DestinationRelativePath">
/// The exact prepared destination, or null when preparation did not establish one.
/// </param>
/// <param name="NeedsFollowUp">
/// Whether cleanup, presentation or another post-decision step requires attention.
/// </param>
/// <param name="Failure">
/// The original failure and any later diagnostic failures, or null after complete success.
/// </param>
/// <param name="ContinuationBlocked">
/// Whether uncertain storage or lease ownership forbids automatic continuation after this decision.
/// </param>
internal sealed record SaveCreationResult(SaveCreationDisposition Disposition, string? DestinationRelativePath,
    bool NeedsFollowUp, Exception? Failure, bool ContinuationBlocked = false)
{
    /// <summary>
    /// Indicates a confirmed archive commit, even when follow-up remains outstanding.
    /// </summary>
    internal bool Committed => Disposition == SaveCreationDisposition.Committed;

    /// <summary>
    /// Retains the decision while adding a failure from a later operation.
    /// </summary>
    /// <param name="failure">
    /// The later failure to retain alongside the original diagnostic.
    /// </param>
    /// <param name="blocksContinuation">
    /// Marks unresolved storage or lease ownership that must stop automatic continuation.
    /// </param>
    /// <returns>
    /// The same archive decision with its accumulated follow-up state.
    /// </returns>
    internal SaveCreationResult WithFollowUp(Exception failure, bool blocksContinuation = false) => this with
    {
        NeedsFollowUp = true,
        ContinuationBlocked = ContinuationBlocked || blocksContinuation,
        Failure = Failure == null ? failure : new AggregateException(Failure, failure)
    };

    /// <summary>
    /// Projects a known decision for existing bool callers without swallowing uncertainty.
    /// </summary>
    /// <returns>
    /// True for a confirmed commit, false for known non-creation or rollback; unsafe continuation throws.
    /// </returns>
    internal bool ToBoolean()
    {
        if (Disposition == SaveCreationDisposition.Uncertain)
            throw new CoordinatedStatePublicationUncertainException(Failure);
        if (ContinuationBlocked)
        {
            if (Committed) throw new CommittedSaveContinuationException(this);
            throw new CoordinatedStatePublicationUncertainException(Failure);
        }
        return Committed;
    }
}

/// <summary>
/// Stops automatic continuation while preserving the fact that the archive was committed.
/// </summary>
/// <param name="result">
/// The known committed save whose follow-up could not establish safe continuation.
/// </param>
internal sealed class CommittedSaveContinuationException(SaveCreationResult result)
    : InvalidOperationException(PlayerMessage, result.Failure)
{
    internal const string PlayerMessage = "Сохранение создано, но служебное восстановление не завершено. Следующее действие остановлено до проверки хранилища.";

    /// <summary>
    /// Retains the exact committed destination and follow-up diagnostic.
    /// </summary>
    internal SaveCreationResult Result { get; } = result;
}

/// <summary>
/// Retains a preparation failure together with a failure to remove its private scratch.
/// </summary>
/// <param name="preparationFailure">
/// The original preparation error, or null if closing the prepared stream itself failed.
/// </param>
/// <param name="cleanupFailure">
/// The owned stream or private-directory cleanup error.
/// </param>
internal sealed class SavePreparationCleanupException(Exception? preparationFailure, Exception cleanupFailure)
    : IOException("Save preparation scratch requires follow-up.", preparationFailure == null
        ? cleanupFailure : new AggregateException(preparationFailure, cleanupFailure))
{
    /// <summary>
    /// Preserves the preparation error's classification independently of cleanup.
    /// </summary>
    internal Exception? PreparationFailure { get; } = preparationFailure;
}

/// <summary>
/// Owns one closed archive and its disposable source directory until publication or abandonment.
/// </summary>
/// <param name="owner">
/// The manager that owns the snapshot lease and private staging namespace.
/// </param>
/// <param name="snapshotLease">
/// The exact caller-owned lease held continuously from snapshot preparation through publication.
/// </param>
/// <param name="destinationRelativePath">
/// The exact validated create-only canonical destination.
/// </param>
/// <param name="stagingRoot">
/// The unique owned directory returned by the save staging factory.
/// </param>
/// <param name="image">
/// The captured complete closed ZIP image, with no retained open stream.
/// </param>
/// <param name="generation">
/// The logical generation captured before reading the save snapshot.
/// </param>
internal sealed class PreparedSaveArchive(FileSystemManager owner, FileSystemManager.CanonicalWriteLease snapshotLease,
    string destinationRelativePath, string stagingRoot,
    TrustedLocalFileImage image, TrustedLocalGeneration generation) : IAsyncDisposable
{
    private bool _disposed;

    /// <summary>
    /// Identifies the prepared destination without querying a later save list.
    /// </summary>
    internal string DestinationRelativePath { get; } = destinationRelativePath;

    /// <summary>
    /// Admits this candidate against its original owner, active lease and snapshot generation.
    /// </summary>
    /// <param name="files">
    /// The manager about to publish this candidate.
    /// </param>
    /// <param name="lease">
    /// The same held snapshot lease used by the caller.
    /// </param>
    /// <returns>
    /// One absent-before image change; existing destination names cannot become replacements.
    /// </returns>
    internal CanonicalLocalImageChange Admit(FileSystemManager files, FileSystemManager.CanonicalWriteLease lease)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (!ReferenceEquals(files, owner)) throw new InvalidOperationException("The save candidate belongs to another manager.");
        if (!ReferenceEquals(lease, snapshotLease)) throw new InvalidOperationException("The save candidate requires its continuously held snapshot lease.");
        files.ResolveBackupPublicationRecovery(lease);
        if (files.ReadLocalGenerationSnapshot(lease).Binding != generation)
            throw new InvalidDataException("The session generation changed during save preparation.");
        return new(DestinationRelativePath, TrustedLocalFileImage.FromBytes(null), image);
    }

    /// <summary>
    /// Removes only the validated private source directory after its image is no longer needed.
    /// </summary>
    /// <returns>
    /// Completed cleanup; an invalid private entry or filesystem failure remains visible to the caller.
    /// </returns>
    public ValueTask DisposeAsync()
    {
        if (_disposed) return ValueTask.CompletedTask;
        // Shared managed path for Windows/Linux; native Linux verification is pending in the task handoff.
        new TrustedLocalFileScope([stagingRoot]).DeleteOwnedTree(stagingRoot);
        _disposed = true;
        return ValueTask.CompletedTask;
    }
}

public partial class SaveLoadService
{
    /// <summary>
    /// Creates an ordinary save and preserves uncertainty for the caller instead of returning a false failure.
    /// </summary>
    /// <param name="saveName">
    /// The display name and sanitized filename prefix.
    /// </param>
    /// <param name="description">
    /// The description stored in save metadata.
    /// </param>
    /// <param name="saveDir">
    /// The canonical relative destination directory, defaulting to manual saves.
    /// </param>
    /// <param name="turnNumber">
    /// A positive explicit turn number, or zero to use the current aggregated state.
    /// </param>
    /// <returns>
    /// True after a confirmed commit, false after known rejection or rollback; unresolved outcomes throw.
    /// </returns>
    public async Task<bool> SaveGameAsync(string saveName, string description, string saveDir = "saves/manual_saves", int turnNumber = 0) =>
        (await CreateSaveAsync(saveName, description, saveDir, turnNumber)).ToBoolean();

    /// <summary>
    /// Creates a save using the caller's held snapshot lease without acquiring another lease.
    /// </summary>
    /// <param name="canonicalSnapshotLease">
    /// The active lease retained across snapshot preparation and publication.
    /// </param>
    /// <param name="saveName">
    /// The display name and sanitized filename prefix.
    /// </param>
    /// <param name="description">
    /// The description stored in save metadata.
    /// </param>
    /// <param name="saveDir">
    /// The canonical relative destination directory.
    /// </param>
    /// <param name="turnNumber">
    /// A positive explicit turn number, or zero to use the current aggregated state.
    /// </param>
    /// <returns>
    /// A known boolean outcome; uncertainty remains an exception for existing callers.
    /// </returns>
    internal async Task<bool> SaveGameAsync(FileSystemManager.CanonicalWriteLease canonicalSnapshotLease,
        string saveName, string description, string saveDir = "saves/manual_saves", int turnNumber = 0) =>
        (await CreateSaveAsync(canonicalSnapshotLease, saveName, description, saveDir, turnNumber)).ToBoolean();

    /// <summary>
    /// Owns one snapshot lease and retains the save result through lease release.
    /// </summary>
    /// <param name="saveName">
    /// The display name and sanitized filename prefix.
    /// </param>
    /// <param name="description">
    /// The description stored in save metadata.
    /// </param>
    /// <param name="saveDir">
    /// The canonical relative destination directory.
    /// </param>
    /// <param name="turnNumber">
    /// A positive explicit turn number, or zero to use the current aggregated state.
    /// </param>
    /// <returns>
    /// The exact publication decision and any remaining follow-up.
    /// </returns>
    internal async Task<SaveCreationResult> CreateSaveAsync(string saveName, string description,
        string saveDir = "saves/manual_saves", int turnNumber = 0)
    {
        FileSystemManager.CanonicalWriteLease? lease = null;
        SaveCreationResult? result = null;
        try
        {
            lease = await _fs.AcquireCanonicalWriteLeaseAsync();
            result = await CreateSaveAsync(lease, saveName, description, saveDir, turnNumber);
        }
        catch (Exception failure)
        {
            result = result?.WithFollowUp(failure, blocksContinuation: true) ??
                new(SaveCreationDisposition.Uncertain, null, true, failure, true);
        }
        finally
        {
            if (lease != null)
            {
                try { await lease.DisposeAsync(); }
                catch (Exception failure)
                {
                    result = result?.WithFollowUp(failure, blocksContinuation: true) ??
                        new(SaveCreationDisposition.Uncertain, null, true, failure, true);
                }
            }
        }
        return result!;
    }

    /// <summary>
    /// Prepares, publishes and releases one owned candidate while retaining every established decision.
    /// </summary>
    /// <param name="lease">
    /// The caller's held snapshot lease.
    /// </param>
    /// <param name="saveName">
    /// The display name and filename prefix.
    /// </param>
    /// <param name="description">
    /// The metadata description.
    /// </param>
    /// <param name="saveDir">
    /// The canonical relative destination directory.
    /// </param>
    /// <param name="turnNumber">
    /// A positive explicit turn, or zero to use the current state.
    /// </param>
    /// <returns>
    /// The exact archive decision with private cleanup and logging follow-up retained.
    /// </returns>
    internal async Task<SaveCreationResult> CreateSaveAsync(FileSystemManager.CanonicalWriteLease lease,
        string saveName, string description, string saveDir = "saves/manual_saves", int turnNumber = 0)
    {
        PreparedSaveArchive? candidate = null;
        SaveCreationResult result;
        try
        {
            candidate = await PrepareSaveArchiveAsync(lease, saveName, description, saveDir, turnNumber);
            result = await PublishPreparedSaveAsync(lease, candidate);
        }
        catch (Exception failure)
        {
            var cause = failure is SavePreparationCleanupException cleanup ? cleanup.PreparationFailure : failure;
            var uncertain = cause is CoordinatedStatePublicationUncertainException;
            result = new(uncertain ? SaveCreationDisposition.Uncertain : SaveCreationDisposition.NotCreated,
                candidate?.DestinationRelativePath, uncertain || failure is SavePreparationCleanupException,
                failure, uncertain || cause is SessionReplacedException);
        }
        if (candidate != null)
        {
            try { await candidate.DisposeAsync(); }
            catch (Exception failure) { result = result.WithFollowUp(failure); }
        }
        try
        {
            if (result.Committed) _logger.LogInformation("Игра сохранена: {Name}", saveName);
            else _logger.LogError(result.Failure, "Ошибка сохранения: {Name}", saveName);
            if (result.NeedsFollowUp) _logger.LogWarning(result.Failure, "Сохранение требует служебной проверки: {Name}", saveName);
        }
        catch (Exception failure) { result = result.WithFollowUp(failure); }
        return result;
    }

    /// <summary>
    /// Publishes the prepared create-only image once without owning or reacquiring its snapshot lease.
    /// </summary>
    /// <param name="lease">
    /// The still-held snapshot lease.
    /// </param>
    /// <param name="candidate">
    /// The closed candidate to publish; its caller retains cleanup ownership.
    /// </param>
    /// <returns>
    /// The mapped shared publication decision and exact prepared destination.
    /// </returns>
    internal async Task<SaveCreationResult> PublishPreparedSaveAsync(FileSystemManager.CanonicalWriteLease lease, PreparedSaveArchive candidate)
    {
        var change = candidate.Admit(_fs, lease);
        var outcome = await _fs.PublishLocalImageFilesAsync(lease, [change]);
        return new(outcome.Disposition switch
        {
            TrustedLocalPublicationDisposition.Committed => SaveCreationDisposition.Committed,
            TrustedLocalPublicationDisposition.RolledBack => SaveCreationDisposition.RolledBack,
            _ => SaveCreationDisposition.Uncertain
        }, candidate.DestinationRelativePath, outcome.Failure != null, outcome.Failure,
            outcome.Disposition == TrustedLocalPublicationDisposition.Uncertain);
    }

    /// <summary>
    /// Creates an autosave, preserving committed success and refusing unsafe retention after uncertainty.
    /// </summary>
    /// <param name="turnNumber">
    /// The accepted turn number used in the archive name and metadata.
    /// </param>
    /// <returns>
    /// True for a confirmed save, false for known non-creation; unsafe continuation throws.
    /// </returns>
    public async Task<bool> AutosaveAsync(int turnNumber) => (await CreateAutosaveAsync(turnNumber)).ToBoolean();

    /// <summary>
    /// Keeps retention as a separate post-commit operation and retains its failure without reversing the archive.
    /// </summary>
    /// <param name="turnNumber">
    /// The accepted turn number used in the archive name and metadata.
    /// </param>
    /// <returns>
    /// The save decision with any retention follow-up and continuation block.
    /// </returns>
    internal async Task<SaveCreationResult> CreateAutosaveAsync(int turnNumber)
    {
        const string directory = "saves/autosaves";
        var result = await CreateSaveAsync($"autosave_turn{turnNumber}", $"Автосохранение - ход {turnNumber}", directory, turnNumber);
        if (!result.Committed || result.ContinuationBlocked) return result;
        try
        {
            if (_hooks?.BeforeAutosaveCleanupLeaseAcquisitionAsync != null) await _hooks.BeforeAutosaveCleanupLeaseAcquisitionAsync();
            await CleanupOldSaves(directory, _stateManager.Settings.MaxAutosaves);
        }
        catch (Exception failure)
        {
            result = result.WithFollowUp(failure, failure is CoordinatedStatePublicationUncertainException or SessionReplacedException);
        }
        return result;
    }

    /// <summary>
    /// Releases failed preparation resources without losing the original error when cleanup also fails.
    /// </summary>
    /// <param name="stagedFile">
    /// The still-open owned stream, or null when already closed.
    /// </param>
    /// <param name="stagingRoot">
    /// The owned private directory, or null after candidate ownership transferred to the caller.
    /// </param>
    /// <param name="preparationFailure">
    /// The original preparation error, if one was already observed.
    /// </param>
    /// <returns>
    /// Successful cleanup, or a typed failure retaining both preparation and cleanup diagnostics.
    /// </returns>
    private static async Task ReleaseFailedSavePreparationAsync(FileSystemManager.RuntimeStagedFile? stagedFile,
        string? stagingRoot, Exception? preparationFailure)
    {
        Exception? cleanupFailure = null;
        if (stagedFile != null)
        {
            try { await stagedFile.DisposeAsync(); }
            catch (Exception failure) { cleanupFailure = failure; }
        }
        if (stagingRoot != null)
        {
            try { new TrustedLocalFileScope([stagingRoot]).DeleteOwnedTree(stagingRoot); }
            catch (Exception failure) { cleanupFailure = cleanupFailure == null ? failure : new AggregateException(cleanupFailure, failure); }
        }
        if (cleanupFailure != null) throw new SavePreparationCleanupException(preparationFailure, cleanupFailure);
    }
}
