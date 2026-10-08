using BookOfEternityClient.Core;
using BookOfEternityClient.Services;

namespace BookOfEternityClient.WebUi;

public sealed partial class BrowserLocalWriteCoordinator
{
    /// <summary>
    /// Publishes one prepared save under the browser's existing snapshot lease and owner guard.
    /// </summary>
    /// <param name="writeLease">
    /// The continuously held canonical snapshot lease; this method does not acquire another lease.
    /// </param>
    /// <param name="request">
    /// The browser owner and operation admitted by the local UI guard.
    /// </param>
    /// <param name="prepare">
    /// Produces one closed archive candidate on the supplied lease without publishing it.
    /// </param>
    /// <param name="publish">
    /// Publishes that candidate once through the shared image publisher on the supplied lease.
    /// </param>
    /// <returns>
    /// The established archive decision, exact destination and any cleanup or admission follow-up.
    /// </returns>
    internal async Task<SaveCreationResult> ExecutePreparedSaveWithinTransactionAsync(
        FileSystemManager.CanonicalWriteLease writeLease,
        BrowserLocalWriteRequest request,
        Func<FileSystemManager.CanonicalWriteLease, Task<PreparedSaveArchive>> prepare,
        Func<FileSystemManager.CanonicalWriteLease, PreparedSaveArchive, Task<SaveCreationResult>> publish)
    {
        ArgumentNullException.ThrowIfNull(writeLease);
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(prepare);
        ArgumentNullException.ThrowIfNull(publish);
        _fs.ResolveBackupPublicationRecovery(writeLease);
        _fs.VerifyCurrentSessionOperation(writeLease);
        var admission = await AcquireLocalWriteGuardAsync(writeLease, request);
        if (!admission.Acquired || admission.Lease == null)
            return new(SaveCreationDisposition.NotCreated, null, false,
                new InvalidOperationException(admission.BlockerMessage));

        PreparedSaveArchive? candidate = null;
        SaveCreationResult result;
        try
        {
            candidate = await prepare(writeLease);
            // The shared publisher owns the only canonical image decision.
            // Keep that result before private cleanup or guard release can fail.
            result = await publish(writeLease, candidate);
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
            // Shared managed cleanup is implemented for Windows and Linux;
            // native Linux execution remains unverified in this caller block.
            try { await candidate.DisposeAsync(); }
            catch (Exception failure) { result = result.WithFollowUp(failure); }
        }
        if (result.Disposition == SaveCreationDisposition.Uncertain || result.ContinuationBlocked)
            return result;

        try
        {
            // Resolve even known committed cleanup debt before another canonical
            // owner write or the caller's menu/list refresh can occur.
            _fs.ResolveBackupPublicationRecovery(writeLease);
            _fs.VerifyCurrentSessionOperation(writeLease);
        }
        catch (Exception failure)
        {
            return result.WithFollowUp(failure, blocksContinuation: true);
        }
        if (!await TryReleaseAsync(writeLease, admission.Lease))
            result = result.WithFollowUp(new IOException("The browser save owner guard could not be released."),
                blocksContinuation: true);
        return result;
    }
}
