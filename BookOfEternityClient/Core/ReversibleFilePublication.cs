using System.Runtime.ExceptionServices;
using System.Text.Json;
using Microsoft.Win32.SafeHandles;

namespace BookOfEternityClient.Core;

internal static class ReversibleFilePublication
{
    private const string IntentFileName = "intent.json";
    private const string DestinationQuarantinedMarker =
        "destination-quarantined.marker";
    private const string SourcePublishedMarker = "source-published.marker";
    private const string CommittedMarker = "committed.marker";
    private const string CommittedCleanupPrefix = ".cleanup-committed-";
    private const string RolledBackCleanupPrefix = ".cleanup-rolled-back-";
    private const int InitialDestinationOpenRetryCount = 20;
    private static readonly TimeSpan InitialDestinationOpenRetryDelay =
        TimeSpan.FromMilliseconds(50);

    private static readonly JsonSerializerOptions JournalJsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        WriteIndented = true
    };

    internal sealed record PublicationResult(
        PhysicalFileAuthority.FileIdentity PublishedIdentity,
        string PublishedSha256);

    internal enum DeferredPublicationState
    {
        Missing,
        Pending,
        Committed
    }

    internal enum PublicationResolution
    {
        Committed,
        RolledBack
    }

    internal sealed record PublicationIntent(
        int SchemaVersion,
        string TransactionId,
        string AuthorityName,
        string SourcePath,
        string DestinationPath,
        string DestinationQuarantinePath,
        string FailedSourcePath,
        PhysicalFileAuthority.FileIdentity SourceIdentity,
        string SourceSha256,
        bool DestinationExisted,
        PhysicalFileAuthority.FileIdentity? DestinationIdentity,
        string? DestinationSha256,
        bool RetainCommittedJournal = false);

    internal sealed class DurableJournal : IDisposable
    {
        private PhysicalFileAuthority.StableDirectory? _rootAuthority;
        private PhysicalFileAuthority.StableDirectory? _transactionAuthority;

        internal DurableJournal(
            string transactionRoot,
            string transactionId,
            PhysicalFileAuthority.StableDirectory rootAuthority,
            PhysicalFileAuthority.StableDirectory transactionAuthority)
        {
            TransactionRoot = transactionRoot;
            TransactionId = transactionId;
            _rootAuthority = rootAuthority;
            _transactionAuthority = transactionAuthority;
        }

        internal string TransactionRoot { get; private set; }
        internal string TransactionId { get; }
        internal PhysicalFileAuthority.StableDirectory TransactionAuthority =>
            _transactionAuthority ??
            throw new ObjectDisposedException(nameof(DurableJournal));

        internal void CreateMarker(string fileName)
        {
            var path = Path.Combine(TransactionRoot, fileName);
            using var stream = PhysicalFileAuthority.CreateNewWritableFile(
                TransactionAuthority,
                path,
                "File publication phase marker",
                asynchronous: false);
            stream.Flush(flushToDisk: true);
        }

        internal bool TryCleanup(
            PublicationResolution resolution,
            Action<string>? beforeCleanupAuthorityRebind = null)
        {
            try
            {
                MoveToCleanupDebt(
                    resolution,
                    beforeCleanupAuthorityRebind);
                foreach (var fileName in new[]
                         {
                             DestinationQuarantinedMarker,
                             SourcePublishedMarker,
                             CommittedMarker
                         })
                {
                    PhysicalFileAuthority.TryDeleteFile(
                        TransactionAuthority,
                        Path.Combine(TransactionRoot, fileName),
                        "File publication journal cleanup");
                }

                var unexpectedEntry = Directory
                    .EnumerateFileSystemEntries(
                        TransactionRoot,
                        "*",
                        SearchOption.TopDirectoryOnly)
                    .FirstOrDefault(path => !string.Equals(
                        Path.GetFileName(path),
                        IntentFileName,
                        StringComparison.Ordinal));
                if (unexpectedEntry != null)
                {
                    throw new InvalidDataException(
                        "File publication cleanup debt contains unknown evidence.");
                }

                PhysicalFileAuthority.TryDeleteFile(
                    TransactionAuthority,
                    Path.Combine(TransactionRoot, IntentFileName),
                    "File publication durable intent cleanup");
                var transactionAuthority = _transactionAuthority ??
                    throw new ObjectDisposedException(nameof(DurableJournal));
                _transactionAuthority = null;
                transactionAuthority.Dispose();
                var rootAuthority = _rootAuthority ??
                    throw new ObjectDisposedException(nameof(DurableJournal));
                return PhysicalFileAuthority.TryDeleteEmptyDirectory(
                    rootAuthority,
                    TransactionRoot,
                    "File publication journal cleanup");
            }
            catch (Exception ex) when (IsCleanupDebt(ex))
            {
                return false;
            }
        }

        private void MoveToCleanupDebt(
            PublicationResolution resolution,
            Action<string>? beforeCleanupAuthorityRebind)
        {
            var expectedPrefix = resolution == PublicationResolution.Committed
                ? CommittedCleanupPrefix
                : RolledBackCleanupPrefix;
            var currentName = Path.GetFileName(TransactionRoot);
            if (currentName.StartsWith(
                    expectedPrefix,
                    StringComparison.Ordinal))
            {
                return;
            }
            if (!string.Equals(
                    currentName,
                    TransactionId,
                    StringComparison.Ordinal))
            {
                throw new InvalidDataException(
                    "File publication cleanup debt outcome changed.");
            }

            var cleanupRoot = Path.Combine(
                Path.GetDirectoryName(TransactionRoot)
                ?? throw new InvalidDataException(
                    "File publication journal has no parent."),
                expectedPrefix + TransactionId);
            var transactionAuthority = _transactionAuthority ??
                throw new ObjectDisposedException(nameof(DurableJournal));
            var rootAuthority = _rootAuthority ??
                throw new ObjectDisposedException(nameof(DurableJournal));
            PhysicalFileAuthority.RenameOpenedObjectRelative(
                transactionAuthority.Handle ??
                throw new PlatformNotSupportedException(
                    "File publication cleanup requires a retained directory handle."),
                rootAuthority,
                cleanupRoot,
                replaceExisting: false,
                "File publication cleanup debt",
                requireSingleLink: false);
            beforeCleanupAuthorityRebind?.Invoke(cleanupRoot);
            transactionAuthority.RebindFullPathAfterRename(
                cleanupRoot,
                "File publication cleanup debt");
            TransactionRoot = cleanupRoot;
        }

        private static bool IsCleanupDebt(Exception exception)
        {
            if (exception is UnauthorizedAccessException)
                return true;
            if (exception is not IOException)
                return false;

            var error = exception.InnerException is
                System.ComponentModel.Win32Exception win32
                ? win32.NativeErrorCode
                : exception.HResult & 0xFFFF;
            return error is 5 or 32 or 33 or 145;
        }

        public void Dispose()
        {
            var transactionAuthority = _transactionAuthority;
            var rootAuthority = _rootAuthority;
            _transactionAuthority = null;
            _rootAuthority = null;
            transactionAuthority?.Dispose();
            rootAuthority?.Dispose();
        }
    }

    internal sealed class PendingPublication : IDisposable
    {
        private readonly PublicationIntent _intent;
        private readonly SafeFileHandle _sourceHandle;
        private readonly SafeFileHandle? _destinationHandle;
        private readonly bool _ownsDestinationHandle;
        private readonly PhysicalFileAuthority.StableDirectory _sourceParent;
        private readonly PhysicalFileAuthority.StableDirectory _destinationParent;
        private DurableJournal? _journal;
        private bool _priorDestinationCleaned;
        private bool _resolved;

        internal PendingPublication(
            PublicationIntent intent,
            SafeFileHandle sourceHandle,
            SafeFileHandle? destinationHandle,
            bool ownsDestinationHandle,
            PhysicalFileAuthority.StableDirectory sourceParent,
            PhysicalFileAuthority.StableDirectory destinationParent,
            DurableJournal journal,
            PublicationResult result)
        {
            _intent = intent;
            _sourceHandle = sourceHandle;
            _destinationHandle = destinationHandle;
            _ownsDestinationHandle = ownsDestinationHandle;
            _sourceParent = sourceParent;
            _destinationParent = destinationParent;
            _journal = journal;
            Result = result;
        }

        internal PublicationResult Result { get; }
        internal string TransactionId => _intent.TransactionId;
        internal bool IsCommitted { get; private set; }
        internal bool RetainedEvidence { get; private set; }

        internal void ValidateForCommit()
        {
            if (_resolved)
                throw new InvalidOperationException(
                    "File publication transaction is already resolved.");

            PhysicalFileAuthority.EnsureExactFileIdentity(
                _sourceHandle,
                _intent.DestinationPath,
                _intent.SourceIdentity,
                _intent.AuthorityName + " deferred commit source");
            EnsureHash(
                _sourceHandle,
                _intent.SourceSha256,
                _intent.AuthorityName + " deferred commit source");
            if (_destinationHandle == null)
                return;

            PhysicalFileAuthority.EnsureExactFileIdentity(
                _destinationHandle,
                _intent.DestinationQuarantinePath,
                _intent.DestinationIdentity!,
                _intent.AuthorityName + " deferred commit prior destination");
            EnsureHash(
                _destinationHandle,
                _intent.DestinationSha256!,
                _intent.AuthorityName + " deferred commit prior destination");
        }

        internal void Commit()
        {
            ValidateForCommit();
            var journal = _journal ??
                throw new ObjectDisposedException(nameof(PendingPublication));
            journal.CreateMarker(CommittedMarker);
            IsCommitted = true;
            _resolved = true;

            ValidateCommittedSource(_intent, _sourceHandle);
            if (_destinationHandle != null)
            {
                _priorDestinationCleaned = TryDeleteCommittedPriorDestination(
                    _intent,
                    _destinationHandle,
                    _intent.AuthorityName +
                    " committed prior destination");
            }

            if (!_intent.RetainCommittedJournal &&
                (_destinationHandle == null ||
                 _priorDestinationCleaned))
            {
                _ = journal.TryCleanup(PublicationResolution.Committed);
            }
        }

        internal void RollBack()
        {
            if (IsCommitted)
            {
                throw new InvalidOperationException(
                    "A committed file publication cannot be rolled back.");
            }
            if (_resolved)
                return;

            var exactRollback = ReversibleFilePublication.RollBack(
                _intent,
                _sourceHandle,
                _destinationHandle,
                _sourceParent,
                _destinationParent,
                beforeAbsenceFinalValidation: null,
                out var sourceEvidenceRetained);
            RetainedEvidence = sourceEvidenceRetained;
            _resolved = exactRollback;
            if (exactRollback && !sourceEvidenceRetained)
            {
                _ = _journal?.TryCleanup(
                    PublicationResolution.RolledBack);
            }
        }

        internal bool TryAcknowledgeCommittedJournal(
            Action<string>? beforeCleanupAuthorityRebind = null)
        {
            if (!IsCommitted)
                return false;
            ValidateCommittedSource(_intent, _sourceHandle);
            if (_destinationHandle != null &&
                !_priorDestinationCleaned)
            {
                _priorDestinationCleaned =
                    TryDeleteCommittedPriorDestination(
                        _intent,
                        _destinationHandle,
                        _intent.AuthorityName +
                        " committed prior destination");
                if (!_priorDestinationCleaned)
                    return false;
            }

            return _journal?.TryCleanup(
                PublicationResolution.Committed,
                beforeCleanupAuthorityRebind) ?? false;
        }

        public void Dispose()
        {
            if (_ownsDestinationHandle)
                _destinationHandle?.Dispose();
            var journal = _journal;
            _journal = null;
            journal?.Dispose();
        }
    }

    internal static async Task<PublicationResult> PublishAsync(
        string authorityRoot,
        string journalRoot,
        PhysicalFileAuthority.StableDirectory sourceParent,
        string sourcePath,
        FileStream sourceStream,
        PhysicalFileAuthority.StableDirectory destinationParent,
        string destinationPath,
        string authorityName,
        Func<string, Task>? afterAuthorityValidated,
        Func<string, Task>? beforeSourcePublished,
        Func<string, Task>? afterPublished,
        CancellationToken cancellationToken,
        Func<string, Task>? beforeAbsenceFinalValidation = null,
        IReadOnlySet<string>? allowedDestinationSha256s = null,
        bool allowMissingDestination = false,
        PhysicalFileAuthority.FileIdentity? expectedDestinationIdentity = null,
        string? expectedDestinationSha256 = null,
        Func<Task>? afterPublicationNotCommitted = null)
    {
        if (!OperatingSystem.IsWindows())
        {
            throw new PlatformNotSupportedException(
                "Reversible opened-handle file replacement is available only on Windows.");
        }
        if ((expectedDestinationIdentity == null) !=
            string.IsNullOrWhiteSpace(expectedDestinationSha256) ||
            expectedDestinationIdentity is
        {
            IsDirectory: true
        } or
        {
            NumberOfLinks: not 1
        } ||
            expectedDestinationSha256 is { } expectedHash &&
            !IsSha256(expectedHash))
        {
            throw new InvalidDataException(
                $"{authorityName} expected destination authority is incomplete.");
        }

        cancellationToken.ThrowIfCancellationRequested();
        var normalizedSourcePath = Path.GetFullPath(sourcePath);
        var normalizedDestinationPath = Path.GetFullPath(destinationPath);
        var sourceIdentity = PhysicalFileAuthority.CaptureFileIdentity(
            sourceStream.SafeFileHandle,
            authorityName + " source");
        EnsureInitialIdentity(sourceIdentity, authorityName + " source");
        PhysicalFileAuthority.EnsureExactFileIdentity(
            sourceStream.SafeFileHandle,
            normalizedSourcePath,
            sourceIdentity,
            authorityName + " source");
        var sourceSha256 = PhysicalFileAuthority.ComputeOpenedFileSha256(
            sourceStream.SafeFileHandle,
            authorityName + " source");

        SafeFileHandle? destinationHandle = null;
        DurableJournal? journal = null;
        PublicationIntent? intent = null;
        var committed = false;
        try
        {
            destinationHandle = await TryOpenDestinationWithRetryAsync(
                destinationParent,
                normalizedDestinationPath,
                authorityName,
                cancellationToken,
                denyConcurrentWrites:
                    allowedDestinationSha256s != null ||
                    expectedDestinationIdentity != null);
            var destinationIdentity = destinationHandle == null
                ? null
                : PhysicalFileAuthority.CaptureFileIdentity(
                    destinationHandle,
                    authorityName + " prior destination");
            if (destinationIdentity != null)
                EnsureInitialIdentity(
                    destinationIdentity,
                    authorityName + " prior destination");
            var destinationSha256 = destinationHandle == null
                ? null
                : PhysicalFileAuthority.ComputeOpenedFileSha256(
                    destinationHandle,
                    authorityName + " prior destination");
            if (expectedDestinationIdentity != null)
            {
                if (destinationIdentity == null ||
                    !SameObject(
                        destinationIdentity,
                        expectedDestinationIdentity) ||
                    !string.Equals(
                        destinationSha256,
                        expectedDestinationSha256,
                        StringComparison.OrdinalIgnoreCase))
                {
                    throw new InvalidDataException(
                        $"{authorityName} destination does not match exact physical publication authority.");
                }
            }
            if (allowedDestinationSha256s != null)
            {
                if (destinationSha256 == null)
                {
                    if (!allowMissingDestination)
                    {
                        throw new InvalidDataException(
                            $"{authorityName} destination is missing instead of matching transaction-owned authority.");
                    }
                }
                else if (!allowedDestinationSha256s.Contains(
                             destinationSha256))
                {
                    throw new InvalidDataException(
                        $"{authorityName} destination does not match transaction-owned authority.");
                }
            }

            var transactionId = Guid.NewGuid().ToString("N");
            var destinationParentPath = Path.GetDirectoryName(
                normalizedDestinationPath)
                ?? throw new InvalidDataException(
                    $"{authorityName} destination has no parent.");
            var quarantinePath = Path.Combine(
                destinationParentPath,
                $".boe-prior-{transactionId}.quarantine");
            var failedSourcePath = Path.Combine(
                destinationParentPath,
                $".boe-source-{transactionId}.evidence");
            intent = new PublicationIntent(
                SchemaVersion: 1,
                TransactionId: transactionId,
                AuthorityName: authorityName,
                SourcePath: normalizedSourcePath,
                DestinationPath: normalizedDestinationPath,
                DestinationQuarantinePath: quarantinePath,
                FailedSourcePath: failedSourcePath,
                SourceIdentity: sourceIdentity,
                SourceSha256: sourceSha256,
                DestinationExisted: destinationHandle != null,
                DestinationIdentity: destinationIdentity,
                DestinationSha256: destinationSha256);
            journal = CreateJournal(
                authorityRoot,
                journalRoot,
                intent);

            if (afterAuthorityValidated != null)
                await afterAuthorityValidated(normalizedDestinationPath);

            cancellationToken.ThrowIfCancellationRequested();
            PhysicalFileAuthority.EnsureExactFileIdentity(
                sourceStream.SafeFileHandle,
                normalizedSourcePath,
                sourceIdentity,
                authorityName + " source final validation");
            if (destinationHandle != null)
            {
                PhysicalFileAuthority.EnsureExactFileIdentity(
                    destinationHandle,
                    normalizedDestinationPath,
                    destinationIdentity!,
                    authorityName + " prior destination final validation");
                EnsureHash(
                    destinationHandle,
                    destinationSha256!,
                    authorityName + " prior destination final validation");
                PhysicalFileAuthority.RenameOpenedObjectRelative(
                    destinationHandle,
                    destinationParent,
                    quarantinePath,
                    replaceExisting: false,
                    authorityName + " prior destination quarantine");
                PhysicalFileAuthority.EnsureExactFileIdentity(
                    destinationHandle,
                    quarantinePath,
                    destinationIdentity!,
                    authorityName + " quarantined destination");
                EnsureHash(
                    destinationHandle,
                    destinationSha256!,
                    authorityName + " quarantined destination");
                journal.CreateMarker(DestinationQuarantinedMarker);
            }

            if (beforeSourcePublished != null)
                await beforeSourcePublished(normalizedDestinationPath);
            cancellationToken.ThrowIfCancellationRequested();
            PhysicalFileAuthority.RenameOpenedObjectRelative(
                sourceStream.SafeFileHandle,
                destinationParent,
                normalizedDestinationPath,
                replaceExisting: false,
                authorityName + " publication");
            journal.CreateMarker(SourcePublishedMarker);

            if (afterPublished != null)
                await afterPublished(normalizedDestinationPath);

            var publishedIdentity =
                PhysicalFileAuthority.EnsureExactFileIdentity(
                    sourceStream.SafeFileHandle,
                    normalizedDestinationPath,
                    sourceIdentity,
                    authorityName + " post-publication validation");
            EnsureHash(
                sourceStream.SafeFileHandle,
                sourceSha256,
                authorityName + " post-publication validation");
            journal.CreateMarker(CommittedMarker);
            committed = true;
            ValidateCommittedSource(
                intent,
                sourceStream.SafeFileHandle);

            var priorDestinationCleaned = destinationHandle == null ||
                TryDeleteCommittedPriorDestination(
                    intent,
                    destinationHandle,
                    authorityName + " committed prior destination");
            if (priorDestinationCleaned)
            {
                _ = journal.TryCleanup(
                    PublicationResolution.Committed);
            }
            return new PublicationResult(
                publishedIdentity,
                sourceSha256);
        }
        catch (Exception publicationFailure)
        {
            if (committed)
                ExceptionDispatchInfo.Capture(publicationFailure).Throw();
            if (journal == null || intent == null)
            {
                if (afterPublicationNotCommitted != null)
                {
                    try
                    {
                        await afterPublicationNotCommitted();
                    }
                    catch (Exception recorderFailure)
                    {
                        throw new InvalidDataException(
                            $"{authorityName} was not published, but its durable mutation intent could not be cleared.",
                            new AggregateException(
                                publicationFailure,
                                recorderFailure));
                    }
                }

                ExceptionDispatchInfo.Capture(publicationFailure).Throw();
            }

            Exception? rollbackFailure = null;
            try
            {
                var exactRollback = RollBack(
                    intent,
                    sourceStream.SafeFileHandle,
                    destinationHandle,
                    sourceParent,
                    destinationParent,
                    beforeAbsenceFinalValidation,
                    out var sourceEvidenceRetained);
                if (exactRollback && !sourceEvidenceRetained)
                {
                    _ = journal.TryCleanup(
                        PublicationResolution.RolledBack);
                }
            }
            catch (Exception ex)
            {
                rollbackFailure = ex;
            }

            if (rollbackFailure != null)
            {
                throw new InvalidDataException(
                    $"{authorityName} publication failed and exact rollback could not be verified. Durable evidence was retained.",
                    new AggregateException(
                        publicationFailure,
                        rollbackFailure));
            }

            if (afterPublicationNotCommitted != null)
            {
                try
                {
                    await afterPublicationNotCommitted();
                }
                catch (Exception recorderFailure)
                {
                    throw new InvalidDataException(
                        $"{authorityName} was rolled back exactly, but its durable mutation intent could not be cleared.",
                        new AggregateException(
                            publicationFailure,
                            recorderFailure));
                }
            }

            ExceptionDispatchInfo.Capture(publicationFailure).Throw();
            throw;
        }
        finally
        {
            destinationHandle?.Dispose();
            journal?.Dispose();
        }
    }

    internal static async Task<PendingPublication> PublishDeferredAsync(
        string authorityRoot,
        string journalRoot,
        PhysicalFileAuthority.StableDirectory sourceParent,
        string sourcePath,
        FileStream sourceStream,
        PhysicalFileAuthority.StableDirectory destinationParent,
        string destinationPath,
        string authorityName,
        SafeFileHandle? retainedDestinationHandle,
        Func<string, Task>? afterAuthorityValidated,
        Func<string, Task>? beforeSourcePublished,
        Func<string, Task>? afterPublished,
        CancellationToken cancellationToken,
        Func<string, Task>? beforeAbsenceFinalValidation = null)
    {
        if (!OperatingSystem.IsWindows())
        {
            throw new PlatformNotSupportedException(
                "Deferred reversible opened-handle replacement is available only on Windows.");
        }

        cancellationToken.ThrowIfCancellationRequested();
        var normalizedSourcePath = Path.GetFullPath(sourcePath);
        var normalizedDestinationPath = Path.GetFullPath(destinationPath);
        var sourceIdentity = PhysicalFileAuthority.CaptureFileIdentity(
            sourceStream.SafeFileHandle,
            authorityName + " source");
        EnsureInitialIdentity(sourceIdentity, authorityName + " source");
        PhysicalFileAuthority.EnsureExactFileIdentity(
            sourceStream.SafeFileHandle,
            normalizedSourcePath,
            sourceIdentity,
            authorityName + " source");
        var sourceSha256 = PhysicalFileAuthority.ComputeOpenedFileSha256(
            sourceStream.SafeFileHandle,
            authorityName + " source");

        var destinationHandle = retainedDestinationHandle;
        var ownsDestinationHandle = retainedDestinationHandle == null;
        DurableJournal? journal = null;
        PublicationIntent? intent = null;
        var transferred = false;
        try
        {
            destinationHandle ??= TryOpenDestination(
                destinationParent,
                normalizedDestinationPath,
                authorityName);
            var destinationIdentity = destinationHandle == null
                ? null
                : PhysicalFileAuthority.CaptureFileIdentity(
                    destinationHandle,
                    authorityName + " prior destination");
            if (destinationIdentity != null)
            {
                EnsureInitialIdentity(
                    destinationIdentity,
                    authorityName + " prior destination");
            }

            var destinationSha256 = destinationHandle == null
                ? null
                : PhysicalFileAuthority.ComputeOpenedFileSha256(
                    destinationHandle,
                    authorityName + " prior destination");
            var transactionId = Guid.NewGuid().ToString("N");
            var destinationParentPath = Path.GetDirectoryName(
                normalizedDestinationPath)
                ?? throw new InvalidDataException(
                    $"{authorityName} destination has no parent.");
            var quarantinePath = Path.Combine(
                destinationParentPath,
                $".boe-prior-{transactionId}.quarantine");
            var failedSourcePath = Path.Combine(
                destinationParentPath,
                $".boe-source-{transactionId}.evidence");
            intent = new PublicationIntent(
                SchemaVersion: 2,
                TransactionId: transactionId,
                AuthorityName: authorityName,
                SourcePath: normalizedSourcePath,
                DestinationPath: normalizedDestinationPath,
                DestinationQuarantinePath: quarantinePath,
                FailedSourcePath: failedSourcePath,
                SourceIdentity: sourceIdentity,
                SourceSha256: sourceSha256,
                DestinationExisted: destinationHandle != null,
                DestinationIdentity: destinationIdentity,
                DestinationSha256: destinationSha256,
                RetainCommittedJournal: true);
            journal = CreateJournal(
                authorityRoot,
                journalRoot,
                intent);

            if (afterAuthorityValidated != null)
                await afterAuthorityValidated(normalizedDestinationPath);

            cancellationToken.ThrowIfCancellationRequested();
            PhysicalFileAuthority.EnsureExactFileIdentity(
                sourceStream.SafeFileHandle,
                normalizedSourcePath,
                sourceIdentity,
                authorityName + " source final validation");
            if (destinationHandle != null)
            {
                PhysicalFileAuthority.EnsureExactFileIdentity(
                    destinationHandle,
                    normalizedDestinationPath,
                    destinationIdentity!,
                    authorityName + " prior destination final validation");
                EnsureHash(
                    destinationHandle,
                    destinationSha256!,
                    authorityName + " prior destination final validation");
                PhysicalFileAuthority.RenameOpenedObjectRelative(
                    destinationHandle,
                    destinationParent,
                    quarantinePath,
                    replaceExisting: false,
                    authorityName + " prior destination quarantine");
                PhysicalFileAuthority.EnsureExactFileIdentity(
                    destinationHandle,
                    quarantinePath,
                    destinationIdentity!,
                    authorityName + " quarantined destination");
                EnsureHash(
                    destinationHandle,
                    destinationSha256!,
                    authorityName + " quarantined destination");
                journal.CreateMarker(DestinationQuarantinedMarker);
            }

            if (beforeSourcePublished != null)
                await beforeSourcePublished(normalizedDestinationPath);
            cancellationToken.ThrowIfCancellationRequested();
            PhysicalFileAuthority.RenameOpenedObjectRelative(
                sourceStream.SafeFileHandle,
                destinationParent,
                normalizedDestinationPath,
                replaceExisting: false,
                authorityName + " publication");
            journal.CreateMarker(SourcePublishedMarker);

            if (afterPublished != null)
                await afterPublished(normalizedDestinationPath);

            var publishedIdentity =
                PhysicalFileAuthority.EnsureExactFileIdentity(
                    sourceStream.SafeFileHandle,
                    normalizedDestinationPath,
                    sourceIdentity,
                    authorityName + " post-publication validation");
            EnsureHash(
                sourceStream.SafeFileHandle,
                sourceSha256,
                authorityName + " post-publication validation");
            var pending = new PendingPublication(
                intent,
                sourceStream.SafeFileHandle,
                destinationHandle,
                ownsDestinationHandle,
                sourceParent,
                destinationParent,
                journal,
                new PublicationResult(
                    publishedIdentity,
                    sourceSha256));
            transferred = true;
            return pending;
        }
        catch (Exception publicationFailure)
        {
            if (journal == null || intent == null)
                ExceptionDispatchInfo.Capture(publicationFailure).Throw();

            Exception? rollbackFailure = null;
            try
            {
                var exactRollback = RollBack(
                    intent,
                    sourceStream.SafeFileHandle,
                    destinationHandle,
                    sourceParent,
                    destinationParent,
                    beforeAbsenceFinalValidation,
                    out var sourceEvidenceRetained);
                if (exactRollback && !sourceEvidenceRetained)
                {
                    _ = journal.TryCleanup(
                        PublicationResolution.RolledBack);
                }
            }
            catch (Exception ex)
            {
                rollbackFailure = ex;
            }

            if (rollbackFailure != null)
            {
                throw new InvalidDataException(
                    $"{authorityName} deferred publication failed and exact rollback could not be verified. Durable evidence was retained.",
                    new AggregateException(
                        publicationFailure,
                        rollbackFailure));
            }

            ExceptionDispatchInfo.Capture(publicationFailure).Throw();
            throw;
        }
        finally
        {
            if (!transferred)
            {
                if (ownsDestinationHandle)
                    destinationHandle?.Dispose();
                journal?.Dispose();
            }
        }
    }

    internal static void RecoverPending(
        string authorityRoot,
        string journalRoot)
    {
        if (!OperatingSystem.IsWindows())
            return;

        var journalRootKind =
            PhysicalFileAuthority.ProbeNamespaceEntryFromRoot(
                authorityRoot,
                journalRoot,
                "File publication recovery root");
        if (journalRootKind ==
            PhysicalFileAuthority.NamespaceEntryKind.Missing)
        {
            return;
        }
        if (journalRootKind !=
            PhysicalFileAuthority.NamespaceEntryKind.Directory)
        {
            throw new InvalidDataException(
                "File publication recovery root is not a physical directory.");
        }

        using var rootAuthority = PhysicalFileAuthority.OpenStableDirectory(
            journalRoot,
            "File publication recovery");
        foreach (var transactionRoot in Directory.EnumerateFileSystemEntries(
                     journalRoot,
                     "*",
                     SearchOption.TopDirectoryOnly).ToArray())
        {
            if (PhysicalFileAuthority.ProbeNamespaceEntry(
                    rootAuthority,
                    transactionRoot,
                    "File publication recovery transaction") !=
                PhysicalFileAuthority.NamespaceEntryKind.Directory)
            {
                throw new InvalidDataException(
                    "File publication recovery root contains an unknown entry.");
            }

            var directoryName = Path.GetFileName(transactionRoot);
            PublicationResolution? cleanupResolution = null;
            string transactionId;
            if (TryParseCleanupDebt(
                    directoryName,
                    out var parsedResolution,
                    out transactionId))
            {
                cleanupResolution = parsedResolution;
            }
            else if (Guid.TryParseExact(directoryName, "N", out _))
            {
                transactionId = directoryName;
            }
            else
            {
                throw new InvalidDataException(
                    "File publication recovery root contains an unknown transaction.");
            }

            RecoverOne(
                authorityRoot,
                rootAuthority,
                transactionRoot,
                acknowledgeDeferredCommit: false,
                expectedTransactionId: transactionId,
                cleanupResolution: cleanupResolution);
        }
    }

    internal static DeferredPublicationState GetDeferredState(
        string authorityRoot,
        string journalRoot,
        string transactionId)
    {
        if (!Guid.TryParseExact(transactionId, "N", out _))
        {
            throw new InvalidDataException(
                "Deferred file publication transaction ID is invalid.");
        }
        if (!OperatingSystem.IsWindows())
            return DeferredPublicationState.Missing;

        var transactionRoot = Path.Combine(journalRoot, transactionId);
        var journalRootKind =
            PhysicalFileAuthority.ProbeNamespaceEntryFromRoot(
                authorityRoot,
                journalRoot,
                "Deferred file publication root");
        if (journalRootKind ==
            PhysicalFileAuthority.NamespaceEntryKind.Missing)
        {
            return DeferredPublicationState.Missing;
        }
        if (journalRootKind !=
            PhysicalFileAuthority.NamespaceEntryKind.Directory)
        {
            throw new InvalidDataException(
                "Deferred file publication root is not a physical directory.");
        }

        using var rootAuthority = PhysicalFileAuthority.OpenStableDirectory(
            journalRoot,
            "Deferred file publication root");
        var transactionKind = PhysicalFileAuthority.ProbeNamespaceEntry(
            rootAuthority,
            transactionRoot,
            "Deferred file publication transaction");
        if (transactionKind ==
            PhysicalFileAuthority.NamespaceEntryKind.Missing)
        {
            return DeferredPublicationState.Missing;
        }
        if (transactionKind !=
            PhysicalFileAuthority.NamespaceEntryKind.Directory)
        {
            throw new InvalidDataException(
                "Deferred file publication transaction is not a physical directory.");
        }

        using var transactionAuthority =
            PhysicalFileAuthority.OpenStableDirectory(
                transactionRoot,
                "Deferred file publication state");
        var intent = ReadIntent(transactionAuthority, transactionRoot);
        ValidateIntent(
            authorityRoot,
            transactionRoot,
            transactionId,
            intent);
        if (!intent.RetainCommittedJournal)
        {
            throw new InvalidDataException(
                "File publication transaction is not controller-retained.");
        }

        return ValidateMarker(
            transactionAuthority,
            transactionRoot,
            CommittedMarker)
            ? DeferredPublicationState.Committed
            : DeferredPublicationState.Pending;
    }

    internal static void AcknowledgeDeferredCommit(
        string authorityRoot,
        string journalRoot,
        string transactionId)
    {
        var state = GetDeferredState(
            authorityRoot,
            journalRoot,
            transactionId);
        if (state == DeferredPublicationState.Missing)
            return;
        if (state != DeferredPublicationState.Committed)
        {
            throw new InvalidDataException(
                "Cannot acknowledge an uncommitted deferred file publication.");
        }

        using var rootAuthority = PhysicalFileAuthority.EnsureStableDirectory(
            authorityRoot,
            journalRoot,
            "Deferred file publication acknowledgement");
        RecoverOne(
            authorityRoot,
            rootAuthority,
            Path.Combine(journalRoot, transactionId),
            acknowledgeDeferredCommit: true);
    }

    private static void RecoverOne(
        string authorityRoot,
        PhysicalFileAuthority.StableDirectory journalRootAuthority,
        string transactionRoot,
        bool acknowledgeDeferredCommit,
        string? expectedTransactionId = null,
        PublicationResolution? cleanupResolution = null)
    {
        if (FileSystemManager.IsReparsePoint(transactionRoot))
        {
            throw new InvalidDataException(
                "File publication recovery journal cannot be a reparse point.");
        }

        var transactionAuthority =
            PhysicalFileAuthority.OpenStableDirectory(
                transactionRoot,
                "File publication recovery",
                allowRename: true);
        var transactionId = expectedTransactionId ??
            Path.GetFileName(transactionRoot);
        var intentPath = Path.Combine(transactionRoot, IntentFileName);
        if (cleanupResolution != null &&
            !File.Exists(intentPath))
        {
            using (transactionAuthority)
            {
                if (Directory.EnumerateFileSystemEntries(
                        transactionRoot,
                        "*",
                        SearchOption.TopDirectoryOnly).Any())
                {
                    throw new InvalidDataException(
                        "Resolved file publication cleanup debt lost its durable intent before other evidence.");
                }
            }

            _ = PhysicalFileAuthority.TryDeleteEmptyDirectory(
                journalRootAuthority,
                transactionRoot,
                "File publication cleanup debt");
            return;
        }

        using var journal = new DurableJournal(
            transactionRoot,
            transactionId,
            CloneRootAuthority(
                authorityRoot,
                journalRootAuthority.FullPath),
            transactionAuthority);
        var intent = ReadIntent(journal.TransactionAuthority, transactionRoot);
        ValidateIntent(
            authorityRoot,
            transactionRoot,
            transactionId,
            intent);
        var destinationQuarantined = ValidateMarker(
            journal.TransactionAuthority,
            transactionRoot,
            DestinationQuarantinedMarker);
        var sourcePublished = ValidateMarker(
            journal.TransactionAuthority,
            transactionRoot,
            SourcePublishedMarker);
        var committedMarker = ValidateMarker(
            journal.TransactionAuthority,
            transactionRoot,
            CommittedMarker);
        if (cleanupResolution == PublicationResolution.RolledBack &&
            committedMarker)
        {
            throw new InvalidDataException(
                "Rolled-back file publication cleanup debt contains a commit marker.");
        }

        var committed =
            cleanupResolution == PublicationResolution.Committed ||
            committedMarker;
        if (cleanupResolution == null &&
            (!intent.DestinationExisted && destinationQuarantined ||
             sourcePublished &&
             intent.DestinationExisted &&
             !destinationQuarantined ||
             committed && !sourcePublished))
        {
            throw new InvalidDataException(
                "File publication phase markers have an invalid order.");
        }

        using var sourceParent = PhysicalFileAuthority.EnsureStableDirectory(
            authorityRoot,
            Path.GetDirectoryName(intent.SourcePath)!,
            "File publication source recovery");
        using var destinationParent =
            PhysicalFileAuthority.EnsureStableDirectory(
                authorityRoot,
                Path.GetDirectoryName(intent.DestinationPath)!,
                "File publication destination recovery");
        using var sourceHandle = OpenIdentityFromCandidates(
            sourceParent,
            destinationParent,
            intent.SourceIdentity,
            intent.AuthorityName + " source recovery",
            intent.SourcePath,
            intent.DestinationPath,
            intent.FailedSourcePath);
        SafeFileHandle? destinationHandle = null;
        try
        {
            if (intent.DestinationExisted)
            {
                destinationHandle = committed
                    ? OpenIdentityFromCandidates(
                        sourceParent,
                        destinationParent,
                        intent.DestinationIdentity!,
                        intent.AuthorityName +
                        " prior destination recovery",
                        intent.DestinationQuarantinePath)
                    : OpenIdentityFromCandidates(
                        sourceParent,
                        destinationParent,
                        intent.DestinationIdentity!,
                        intent.AuthorityName +
                        " prior destination recovery",
                        intent.DestinationQuarantinePath,
                        intent.DestinationPath);
            }

            if (committed)
            {
                if (sourceHandle == null)
                {
                    throw new InvalidDataException(
                        "Committed publication source identity is missing.");
                }

                PhysicalFileAuthority.EnsureExactFileIdentity(
                    sourceHandle,
                    intent.DestinationPath,
                    intent.SourceIdentity,
                    intent.AuthorityName + " committed recovery");
                EnsureHash(
                    sourceHandle,
                    intent.SourceSha256,
                    intent.AuthorityName + " committed recovery");
                if (destinationHandle != null &&
                    !TryDeleteCommittedPriorDestination(
                        intent,
                        destinationHandle,
                        intent.AuthorityName +
                        " committed prior recovery"))
                {
                    return;
                }

                if (cleanupResolution != null ||
                    !intent.RetainCommittedJournal ||
                    acknowledgeDeferredCommit)
                {
                    _ = journal.TryCleanup(
                        PublicationResolution.Committed);
                }
                return;
            }

            var exactRollback = RollBack(
                intent,
                sourceHandle,
                destinationHandle,
                sourceParent,
                destinationParent,
                beforeAbsenceFinalValidation: null,
                out var sourceEvidenceRetained);
            if (exactRollback && !sourceEvidenceRetained)
            {
                _ = journal.TryCleanup(
                    PublicationResolution.RolledBack);
            }
        }
        finally
        {
            destinationHandle?.Dispose();
        }
    }

    private static bool RollBack(
        PublicationIntent intent,
        SafeFileHandle? sourceHandle,
        SafeFileHandle? destinationHandle,
        PhysicalFileAuthority.StableDirectory sourceParent,
        PhysicalFileAuthority.StableDirectory destinationParent,
        Func<string, Task>? beforeAbsenceFinalValidation,
        out bool sourceEvidenceRetained)
    {
        sourceEvidenceRetained = false;
        if (sourceHandle != null)
        {
            var sourceCurrentPath = GetOpenedPath(sourceHandle);
            if (PathsEqual(sourceCurrentPath, intent.DestinationPath))
            {
                PhysicalFileAuthority.EnsureExactFileIdentity(
                    sourceHandle,
                    intent.DestinationPath,
                    intent.SourceIdentity,
                    intent.AuthorityName + " rollback source",
                    requireSingleLink: false);
                EnsureHash(
                    sourceHandle,
                    intent.SourceSha256,
                    intent.AuthorityName + " rollback source");
                PhysicalFileAuthority.RenameOpenedObjectRelative(
                    sourceHandle,
                    destinationParent,
                    intent.FailedSourcePath,
                    replaceExisting: false,
                    intent.AuthorityName + " rollback source evidence",
                    requireSingleLink: false);
                sourceCurrentPath = intent.FailedSourcePath;
            }

            if (!PathsEqual(sourceCurrentPath, intent.SourcePath) &&
                !PathsEqual(sourceCurrentPath, intent.FailedSourcePath))
            {
                throw new InvalidDataException(
                    $"{intent.AuthorityName} rollback source identity is at an unknown path.");
            }
        }

        if (intent.DestinationExisted)
        {
            if (destinationHandle == null)
            {
                throw new InvalidDataException(
                    $"{intent.AuthorityName} prior destination identity is missing.");
            }

            var priorCurrentPath = GetOpenedPath(destinationHandle);
            if (PathsEqual(
                    priorCurrentPath,
                    intent.DestinationQuarantinePath))
            {
                EnsureDestinationAbsentOrOwnedSource(
                    destinationParent,
                    intent,
                    sourceHandle);
                PhysicalFileAuthority.RenameOpenedObjectRelative(
                    destinationHandle,
                    destinationParent,
                    intent.DestinationPath,
                    replaceExisting: false,
                    intent.AuthorityName + " prior destination rollback",
                    requireSingleLink: false);
                priorCurrentPath = intent.DestinationPath;
            }

            if (!PathsEqual(priorCurrentPath, intent.DestinationPath))
            {
                throw new InvalidDataException(
                    $"{intent.AuthorityName} prior destination identity is at an unknown path.");
            }

            PhysicalFileAuthority.EnsureExactFileIdentity(
                destinationHandle,
                intent.DestinationPath,
                intent.DestinationIdentity!,
                intent.AuthorityName + " exact prior destination rollback");
            EnsureHash(
                destinationHandle,
                intent.DestinationSha256!,
                intent.AuthorityName + " exact prior destination rollback");
        }
        else
        {
            EnsureDestinationAbsentOrOwnedSource(
                destinationParent,
                intent,
                sourceHandle);
            beforeAbsenceFinalValidation
                ?.Invoke(intent.DestinationPath)
                .GetAwaiter()
                .GetResult();
            if (PhysicalFileAuthority.ProbeNamespaceEntry(
                    destinationParent,
                    intent.DestinationPath,
                    intent.AuthorityName +
                    " rollback final absence") !=
                PhysicalFileAuthority.NamespaceEntryKind.Missing)
            {
                throw new InvalidDataException(
                    $"{intent.AuthorityName} rollback could not restore exact prior absence.");
            }
        }

        if (sourceHandle != null)
        {
            var sourcePath = GetOpenedPath(sourceHandle);
            var sourceIdentity = PhysicalFileAuthority.EnsureExactFileIdentity(
                sourceHandle,
                sourcePath,
                intent.SourceIdentity,
                intent.AuthorityName + " rollback source cleanup",
                requireSingleLink: false);
            EnsureHash(
                sourceHandle,
                intent.SourceSha256,
                intent.AuthorityName + " rollback source cleanup");
            if (sourceIdentity.NumberOfLinks == 1)
            {
                PhysicalFileAuthority.DeleteOpenedFile(
                    sourceHandle,
                    intent.AuthorityName + " rollback source cleanup");
            }
            else
            {
                sourceEvidenceRetained = true;
            }
        }

        return true;
    }

    private static void EnsureDestinationAbsentOrOwnedSource(
        PhysicalFileAuthority.StableDirectory destinationParent,
        PublicationIntent intent,
        SafeFileHandle? sourceHandle)
    {
        var current = TryOpenDestination(
            destinationParent,
            intent.DestinationPath,
            intent.AuthorityName + " rollback destination");
        if (current == null)
            return;

        using (current)
        {
            var identity = PhysicalFileAuthority.CaptureFileIdentity(
                current,
                intent.AuthorityName + " rollback destination");
            if (sourceHandle == null ||
                identity.VolumeSerialNumber !=
                    intent.SourceIdentity.VolumeSerialNumber ||
                identity.FileIdLow != intent.SourceIdentity.FileIdLow ||
                identity.FileIdHigh != intent.SourceIdentity.FileIdHigh)
            {
                throw new InvalidDataException(
                    $"{intent.AuthorityName} rollback found an unknown destination identity.");
            }

            var sourcePath = GetOpenedPath(sourceHandle);
            if (PathsEqual(sourcePath, intent.DestinationPath))
            {
                throw new InvalidDataException(
                    $"{intent.AuthorityName} rollback source still occupies the destination.");
            }
        }
    }

    private static SafeFileHandle? TryOpenDestination(
        PhysicalFileAuthority.StableDirectory destinationParent,
        string destinationPath,
        string authorityName,
        bool denyConcurrentWrites = false)
    {
        var entry = PhysicalFileAuthority.ProbeNamespaceEntry(
            destinationParent,
            destinationPath,
            authorityName);
        if (entry == PhysicalFileAuthority.NamespaceEntryKind.Missing)
        {
            return null;
        }
        if (entry != PhysicalFileAuthority.NamespaceEntryKind.RegularFile)
        {
            throw new InvalidDataException(
                $"{authorityName} destination is not a physical regular file.");
        }

        return PhysicalFileAuthority.OpenForRename(
            destinationParent,
            destinationPath,
            isDirectory: false,
            authorityName,
            denyConcurrentWrites: denyConcurrentWrites);
    }

    private static async Task<SafeFileHandle?>
        TryOpenDestinationWithRetryAsync(
            PhysicalFileAuthority.StableDirectory destinationParent,
            string destinationPath,
            string authorityName,
            CancellationToken cancellationToken,
            bool denyConcurrentWrites = false)
    {
        for (var attempt = 0; ; attempt++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                return TryOpenDestination(
                    destinationParent,
                    destinationPath,
                    authorityName,
                    denyConcurrentWrites);
            }
            catch (IOException ex) when (
                IsSharingViolation(ex) &&
                attempt < InitialDestinationOpenRetryCount)
            {
                await Task.Delay(
                    InitialDestinationOpenRetryDelay,
                    cancellationToken);
            }
        }
    }

    private static bool IsSharingViolation(IOException exception) =>
        (exception.HResult & 0xFFFF) is 32 or 33 ||
        exception.InnerException is System.ComponentModel.Win32Exception
        {
            NativeErrorCode: 32 or 33
        };

    private static SafeFileHandle? OpenIdentityFromCandidates(
        PhysicalFileAuthority.StableDirectory sourceParent,
        PhysicalFileAuthority.StableDirectory destinationParent,
        PhysicalFileAuthority.FileIdentity expectedIdentity,
        string authorityName,
        params string[] candidates)
    {
        foreach (var candidate in candidates.Distinct(
                     StringComparer.OrdinalIgnoreCase))
        {
            var parent = PathsEqual(
                    Path.GetDirectoryName(candidate)!,
                    sourceParent.FullPath)
                ? sourceParent
                : destinationParent;
            var entry = PhysicalFileAuthority.ProbeNamespaceEntry(
                parent,
                candidate,
                authorityName + " candidate");
            if (entry == PhysicalFileAuthority.NamespaceEntryKind.Missing)
                continue;
            if (entry != PhysicalFileAuthority.NamespaceEntryKind.RegularFile)
            {
                throw new InvalidDataException(
                    $"{authorityName} candidate is not a physical regular file.");
            }

            SafeFileHandle? handle = null;
            try
            {
                handle = PhysicalFileAuthority.OpenForRename(
                    parent,
                    candidate,
                    isDirectory: false,
                    authorityName);
                var actual = PhysicalFileAuthority.CaptureFileIdentity(
                    handle,
                    authorityName);
                if (SameObject(actual, expectedIdentity))
                    return handle;
            }
            catch
            {
                handle?.Dispose();
                throw;
            }

            handle.Dispose();
        }

        return null;
    }

    private static DurableJournal CreateJournal(
        string authorityRoot,
        string journalRoot,
        PublicationIntent intent)
    {
        Directory.CreateDirectory(journalRoot);
        var rootAuthority = PhysicalFileAuthority.EnsureStableDirectory(
            authorityRoot,
            journalRoot,
            "File publication journal");
        var transactionRoot = Path.Combine(
            journalRoot,
            intent.TransactionId);
        try
        {
            if (Directory.Exists(transactionRoot) ||
                File.Exists(transactionRoot))
            {
                throw new IOException(
                    "File publication transaction identity already exists.");
            }

            Directory.CreateDirectory(transactionRoot);
            var transactionAuthority =
                PhysicalFileAuthority.OpenStableDirectory(
                    transactionRoot,
                    "File publication journal",
                    allowRename: true);
            var journal = new DurableJournal(
                transactionRoot,
                intent.TransactionId,
                rootAuthority,
                transactionAuthority);
            try
            {
                var bytes = JsonSerializer.SerializeToUtf8Bytes(
                    intent,
                    JournalJsonOptions);
                using var stream =
                    PhysicalFileAuthority.CreateNewWritableFile(
                        journal.TransactionAuthority,
                        Path.Combine(transactionRoot, IntentFileName),
                        "File publication durable intent",
                        asynchronous: false);
                stream.Write(bytes);
                stream.Flush(flushToDisk: true);
                return journal;
            }
            catch
            {
                journal.Dispose();
                throw;
            }
        }
        catch
        {
            rootAuthority.Dispose();
            throw;
        }
    }

    private static PublicationIntent ReadIntent(
        PhysicalFileAuthority.StableDirectory transactionAuthority,
        string transactionRoot)
    {
        var path = Path.Combine(transactionRoot, IntentFileName);
        using var stream = PhysicalFileAuthority.OpenReadFile(
            transactionAuthority,
            path,
            "File publication durable intent",
            asynchronous: false)
            ?? throw new FileNotFoundException(
                "File publication durable intent is missing.",
                path);
        using var buffer = new MemoryStream();
        stream.CopyTo(buffer);
        PhysicalFileAuthority.EnsureRegularFileHandleMatchesExpectedPath(
            stream.SafeFileHandle,
            path,
            "File publication durable intent completion");
        try
        {
            return StrictJsonAuthority.Deserialize<PublicationIntent>(
                       buffer.ToArray(),
                       JournalJsonOptions,
                       "File publication durable intent")
                   ?? throw new InvalidDataException(
                       "File publication durable intent is empty.");
        }
        catch (JsonException ex)
        {
            throw new InvalidDataException(
                "File publication durable intent is malformed.",
                ex);
        }
    }

    private static bool ValidateMarker(
        PhysicalFileAuthority.StableDirectory transactionAuthority,
        string transactionRoot,
        string markerName)
    {
        var path = Path.Combine(transactionRoot, markerName);
        using var stream = PhysicalFileAuthority.OpenReadFile(
            transactionAuthority,
            path,
            "File publication phase marker",
            asynchronous: false);
        if (stream == null)
            return false;
        PhysicalFileAuthority.EnsureRegularFileHandleMatchesExpectedPath(
            stream.SafeFileHandle,
            path,
            "File publication phase marker completion");
        return true;
    }

    private static void ValidateIntent(
        string authorityRoot,
        string transactionRoot,
        string expectedTransactionId,
        PublicationIntent intent)
    {
        if (intent.SchemaVersion is not (1 or 2) ||
            intent.SchemaVersion == 1 && intent.RetainCommittedJournal ||
            intent.SchemaVersion == 2 && !intent.RetainCommittedJournal ||
            !Guid.TryParseExact(intent.TransactionId, "N", out _) ||
            !string.Equals(
                expectedTransactionId,
                intent.TransactionId,
                StringComparison.Ordinal) ||
            string.IsNullOrWhiteSpace(intent.AuthorityName) ||
            !IsSha256(intent.SourceSha256) ||
            intent.SourceIdentity.IsDirectory ||
            intent.SourceIdentity.NumberOfLinks != 1 ||
            intent.DestinationExisted !=
                (intent.DestinationIdentity != null) ||
            intent.DestinationExisted !=
                !string.IsNullOrWhiteSpace(intent.DestinationSha256) ||
            intent.DestinationIdentity is { IsDirectory: true } ||
            intent.DestinationIdentity is { NumberOfLinks: not 1 } ||
            intent.DestinationSha256 is { } destinationHash &&
            !IsSha256(destinationHash))
        {
            throw new InvalidDataException(
                "File publication durable intent has an invalid contract.");
        }

        foreach (var path in new[]
                 {
                     intent.SourcePath,
                     intent.DestinationPath,
                     intent.DestinationQuarantinePath,
                     intent.FailedSourcePath
                 })
        {
            EnsureWithinRoot(authorityRoot, path);
        }

        var destinationParent = Path.GetDirectoryName(
            intent.DestinationPath);
        if (!PathsEqual(
                destinationParent!,
                Path.GetDirectoryName(
                    intent.DestinationQuarantinePath)!) ||
            !PathsEqual(
                destinationParent!,
                Path.GetDirectoryName(intent.FailedSourcePath)!))
        {
            throw new InvalidDataException(
                "File publication recovery paths do not share one retained destination parent.");
        }
    }

    private static void EnsureInitialIdentity(
        PhysicalFileAuthority.FileIdentity identity,
        string authorityName)
    {
        if (identity.IsDirectory || identity.NumberOfLinks != 1)
        {
            throw new InvalidDataException(
                $"{authorityName} must be one single-link regular file.");
        }
    }

    private static void EnsureHash(
        SafeFileHandle handle,
        string expectedSha256,
        string authorityName)
    {
        var actual = PhysicalFileAuthority.ComputeOpenedFileSha256(
            handle,
            authorityName);
        if (!string.Equals(
                actual,
                expectedSha256,
                StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidDataException(
                $"{authorityName} exact-byte hash changed.");
        }
    }

    private static void ValidateCommittedSource(
        PublicationIntent intent,
        SafeFileHandle sourceHandle)
    {
        PhysicalFileAuthority.EnsureExactFileIdentity(
            sourceHandle,
            intent.DestinationPath,
            intent.SourceIdentity,
            intent.AuthorityName + " committed source");
        EnsureHash(
            sourceHandle,
            intent.SourceSha256,
            intent.AuthorityName + " committed source");
    }

    private static bool TryDeleteCommittedPriorDestination(
        PublicationIntent intent,
        SafeFileHandle destinationHandle,
        string authorityName)
    {
        PhysicalFileAuthority.EnsureExactFileIdentity(
            destinationHandle,
            intent.DestinationQuarantinePath,
            intent.DestinationIdentity!,
            authorityName);
        EnsureHash(
            destinationHandle,
            intent.DestinationSha256!,
            authorityName);
        try
        {
            PhysicalFileAuthority.DeleteOpenedFile(
                destinationHandle,
                authorityName);
            return true;
        }
        catch (Exception ex) when (IsCommittedCleanupDebt(ex))
        {
            return false;
        }
    }

    private static bool IsCommittedCleanupDebt(Exception exception)
    {
        if (exception is UnauthorizedAccessException)
            return true;
        if (exception is not IOException)
            return false;

        var error = exception.InnerException is
            System.ComponentModel.Win32Exception win32
            ? win32.NativeErrorCode
            : exception.HResult & 0xFFFF;
        return error is 5 or 32 or 33;
    }

    private static bool TryParseCleanupDebt(
        string directoryName,
        out PublicationResolution resolution,
        out string transactionId)
    {
        string? prefix = null;
        resolution = default;
        if (directoryName.StartsWith(
                CommittedCleanupPrefix,
                StringComparison.Ordinal))
        {
            prefix = CommittedCleanupPrefix;
            resolution = PublicationResolution.Committed;
        }
        else if (directoryName.StartsWith(
                     RolledBackCleanupPrefix,
                     StringComparison.Ordinal))
        {
            prefix = RolledBackCleanupPrefix;
            resolution = PublicationResolution.RolledBack;
        }

        transactionId = prefix == null
            ? ""
            : directoryName[prefix.Length..];
        return prefix != null &&
               Guid.TryParseExact(transactionId, "N", out _);
    }

    private static string GetOpenedPath(SafeFileHandle handle) =>
        NormalizeWindowsHandlePath(
            PhysicalFileAuthority.GetFinalPath(handle));

    private static string NormalizeWindowsHandlePath(string path)
    {
        if (path.StartsWith(@"\\?\UNC\", StringComparison.OrdinalIgnoreCase))
            return @"\\" + path[8..];
        if (path.StartsWith(@"\\?\", StringComparison.OrdinalIgnoreCase))
            return path[4..];
        return path;
    }

    private static bool SameObject(
        PhysicalFileAuthority.FileIdentity left,
        PhysicalFileAuthority.FileIdentity right) =>
        left.VolumeSerialNumber == right.VolumeSerialNumber &&
        left.FileIdLow == right.FileIdLow &&
        left.FileIdHigh == right.FileIdHigh &&
        left.IsDirectory == right.IsDirectory;

    private static bool PathsEqual(string left, string right) =>
        string.Equals(
            Path.GetFullPath(left),
            Path.GetFullPath(right),
            OperatingSystem.IsWindows()
                ? StringComparison.OrdinalIgnoreCase
                : StringComparison.Ordinal);

    private static void EnsureWithinRoot(
        string authorityRoot,
        string path)
    {
        var root = Path.GetFullPath(authorityRoot).TrimEnd(
            Path.DirectorySeparatorChar,
            Path.AltDirectorySeparatorChar);
        var candidate = Path.GetFullPath(path);
        if (!candidate.StartsWith(
                root + Path.DirectorySeparatorChar,
                StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidDataException(
                "File publication path is outside its physical authority root.");
        }
    }

    private static bool IsSha256(string value) =>
        value.Length == 64 &&
        value.All(static ch =>
            ch is >= '0' and <= '9' or >= 'a' and <= 'f');

    private static PhysicalFileAuthority.StableDirectory CloneRootAuthority(
        string authorityRoot,
        string journalRoot) =>
        PhysicalFileAuthority.EnsureStableDirectory(
            authorityRoot,
            journalRoot,
            "File publication journal recovery");
}
