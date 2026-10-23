using System.Runtime.ExceptionServices;
using System.Text.Json;
using System.Text;
using BookOfEternityClient.Services;
using Microsoft.Extensions.Logging;

namespace BookOfEternityClient.Core;

internal sealed record CanonicalLocalFileChange(string RelativePath, byte[]? Before, byte[]? After);

public partial class FileSystemManager
{
    private string LocalPublicationRoot => Path.Combine(RuntimeRootPath, "trusted-local-publication-v1");

    /// <summary>
    /// Runs the existing recovery callback with its legacy recovery flag and mutation reservation retained until cleanup.
    /// </summary>
    /// <param name="lease">
    /// Original active canonical lease; ownership and disposal remain with its caller.
    /// </param>
    /// <param name="recovery">
    /// Existing recovery callback; its completion precedes recovery flag restoration.
    /// </param>
    /// <returns>
    /// Completion after the operation and its required cleanup finish.
    /// </returns>
    internal async Task RunLegacyStorageRecoveryAsync(CanonicalWriteLease lease, Func<Task> recovery)
    {
        using var independentSource = ValidationReuseSuppression.Value == 0 ? _sourceDataOperation.Value?.Suspend() : null;
        using var validationMutation = BeginValidationMutation(lease);
        EnsureCanonicalWriteLeaseActive(lease);
        lease.EnsureNoPendingLocalDecision();
        var previous = lease.IsLegacyStorageRecovery;
        lease.IsLegacyStorageRecovery = true;
        try { await recovery(); }
        finally { lease.IsLegacyStorageRecovery = previous; }
    }

    private IEnumerable<string> LegacyStorageRoots =>
    [
        PhysicalPublicationTransactionsRootPath,
        Path.GetDirectoryName(ActiveLoadTransactionJournalPath)!,
        Path.GetDirectoryName(ActiveWorkerApplyTransactionJournalPath)!,
        ResolvePath(ExplorerLocalTurnRollbackArtifacts.Root)
    ];

    /// <summary>
    /// Probes the existing storage root without following namespace links and checks for entries.
    /// </summary>
    /// <param name="root">
    /// Absolute storage root passed to the existing authority probe.
    /// </param>
    /// <returns>
    /// <see langword="true"/> for a nonempty ordinary directory;
    /// otherwise, <see langword="false"/> for a missing or empty directory.
    /// Invalid namespace entries retain their existing refusal.
    /// </returns>
    private bool HasStorageEvidence(string root)
    {
        using var t351Evidence = MeasureOperation("StorageEvidence.ProbeAndEnumerate");
        PhysicalFileAuthority.NamespaceEntryKind kind;
        using (MeasureOperation("StorageEvidence.Probe"))
        using (EnterOperationTimingContext())
            kind = PhysicalFileAuthority.ProbeStorageEvidenceNamespaceEntryFromRoot(_basePath, root, "Storage evidence root");
        if (kind == PhysicalFileAuthority.NamespaceEntryKind.Missing) return false;
        if (kind != PhysicalFileAuthority.NamespaceEntryKind.Directory)
            throw new InvalidDataException("Storage evidence root is not an ordinary directory: " + root);
        using (MeasureOperation("StorageEvidence.Enumerate"))
            return Directory.EnumerateFileSystemEntries(root).Any();
    }

    private void EnsureNoLegacyStorageEvidence(CanonicalWriteLease? ownedBrowser = null,
        bool allowPortableBrowser = false, CanonicalWriteLease? currentPendingClear = null)
    {
        foreach (var root in LegacyStorageRoots)
        {
            if (!HasStorageEvidence(root)) continue;
            if (root == ResolvePath(ExplorerLocalTurnRollbackArtifacts.Root))
            {
                if (allowPortableBrowser) continue; // The original handler preflights schema7 before any recovery.
                if (ownedBrowser == null)
                {
                    var directories = new List<string>();
                    var files = EnumerateLocalTreeFiles(new TrustedLocalFileScope([GameSessionPath]), root,
                        inspectedDirectories: directories);
                    var directGachaStructure = directories.All(directory =>
                    {
                        var relative = GetLocalRelativePath(GameSessionPath, directory, OperatingSystem.IsWindows());
                        return relative == ExplorerLocalTurnRollbackArtifacts.Root ||
                            (ExplorerLocalTurnRollbackArtifacts.IsLocalDirectGachaPath(relative) &&
                             !ExplorerLocalTurnRollbackArtifacts.IsLocalDirectGachaBackup(relative));
                    });
                    // File deletion can leave these ordinary directories even after
                    // crash recovery. Structure alone is not retained byte evidence.
                    if (directGachaStructure && files.Count == 0) continue;
                    if (currentPendingClear != null && directGachaStructure)
                    {
                        var backups = ListBrowserStorageEvidence(currentPendingClear);
                        if (backups.Count > 0 && backups.All(ExplorerLocalTurnRollbackArtifacts.IsLocalDirectGachaBackup))
                        {
                            var authority = ExplorerLocalTurnRollbackArtifacts.RequireCurrentPendingRollbackAuthority(
                                this, currentPendingClear);
                            var mapped = authority.RollbackBackups.Values
                                .Where(ExplorerLocalTurnRollbackArtifacts.IsLocalDirectGachaBackup)
                                .ToHashSet(StringComparer.Ordinal);
                            if (mapped.SetEquals(backups)) continue;
                        }
                    }
                }
                if (ownedBrowser != null)
                {
                    ownedBrowser.BrowserLocalAccess?.Validate();
                    if (ListBrowserStorageEvidence(ownedBrowser).All(path =>
                        ExplorerLocalTurnRollbackArtifacts.IsLocalDirectGachaBackup(path) ||
                        ownedBrowser.BrowserLocalAccess?.OwnsArtifact(path) == true)) continue;
                }
            }
            throw new InvalidDataException("Unresolved legacy storage evidence requires its original supported recovery handler: " + root);
        }
    }

    internal bool UsesTrustedLocalWriter(CanonicalWriteLease lease, string relativePath)
    {
        if (lease.MutationIntentRecorder != null || lease.IsLegacyStorageRecovery) return false;
        // The old browser recorder owns its own manifest/before-image namespace,
        // including recorder-free cleanup. Do not journal those artifacts anew.
        if (lease.BrowserLocalAccess is { } access)
        {
            access.Validate();
            var relative = GetLocalRelativePath(GameSessionPath, ResolvePath(relativePath), OperatingSystem.IsWindows());
            if (access.OwnsArtifact(relative)) return true;
        }
        var path = ResolvePath(relativePath);
        if (ExplorerLocalTurnRollbackArtifacts.IsLocalDirectGachaPath(
                GetLocalRelativePath(GameSessionPath, path, OperatingSystem.IsWindows()))) return true;
        var root = ResolvePath(ExplorerLocalTurnRollbackArtifacts.Root);
        var comparison = OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
        return !path.Equals(root, comparison) && !path.StartsWith(root + Path.DirectorySeparatorChar, comparison);
    }

    /// <summary>
    /// Recovers existing trusted-local publication evidence and updates generation revision when recovery changes it.
    /// </summary>
    /// <param name="lease">
    /// Original active canonical lease; ownership and disposal remain with its caller.
    /// </param>
    private void RecoverTrustedLocalStorage(CanonicalWriteLease lease)
    {
        bool hasEvidence;
        try
        {
            EnsurePhysicalCanonicalWriteLease(lease);
            (lease.MainAdmission ?? throw new InvalidOperationException("Recovery requires its original admission."))
                .RequireMutationAvailable();
            EnsureWorkerRecoveryAdmission(lease,"trusted-local-recovery-wrapper");
            lease.EnsureNoPendingLocalDecision();
            hasEvidence = HasStorageEvidence(LocalPublicationRoot);
        }
        catch
        {
            InvalidateRecoveryValidationAfterFailure();
            throw;
        }
#if DEBUG
        _browserAdmissionDiagnostic?.Recovery(hasEvidence);
#endif
        if (!hasEvidence) return;
        // Existing journal authority owns recovery; provisional DATA must never obstruct compensation.
        using var independentSource = ValidationReuseSuppression.Value == 0 ? _sourceDataOperation.Value?.Suspend() : null;
        using var validationMutation = BeginValidationMutation(lease);
        EnsureWorkerRecoveryAdmission(lease,"trusted-local-recovery-wrapper");
        lease.EnsureNoPendingLocalDecision();
        if (!HasStorageEvidence(LocalPublicationRoot)) return;
        var before = ReadExistingSessionGeneration(lease);
        new TrustedLocalFilePublication(this, new TrustedLocalFileScope([BasePath]))
            .Recover(lease, _hooks?.LocalPublicationRecoveryObserver);
        if (before != ReadExistingSessionGeneration(lease))
            CanonicalRootAuthorityIdentity.AdvanceSessionGenerationRevision();
    }

    /// <summary>
    /// Publishes missing initial configuration or generation while preserving existing local storage authority.
    /// </summary>
    /// <param name="lease">
    /// Original active canonical lease; ownership and disposal remain with its caller.
    /// </param>
    /// <param name="beforeConfig">
    /// Current configuration bytes, or <see langword="null"/> when configuration is absent.
    /// </param>
    /// <param name="desiredConfig">
    /// Initial configuration bytes to publish when configuration is absent.
    /// </param>
    /// <returns>
    /// The existing or newly published logical session generation.
    /// </returns>
    internal string BootstrapLocalStorage(CanonicalWriteLease lease, byte[]? beforeConfig, byte[] desiredConfig)
    {
        using var validationMutation = BeginValidationMutation(lease);
        EnsureWorkerGeneralMutationAllowed(lease);
        VerifyCurrentSessionOperation(lease);
        if (lease.IsLegacyStorageRecovery || lease.MutationIntentRecorder != null)
            throw new InvalidOperationException("Local bootstrap cannot run inside a legacy transaction.");
        EnsureNoLegacyStorageEvidence();
        var generation = ReadExistingSessionGeneration(lease);
        if (generation != null && beforeConfig != null) return generation;
        var changes = new List<TrustedLocalFileChange>();
        if (beforeConfig == null) changes.Add(new(ResolvePath("config.json"), null, desiredConfig));
        var binding = generation == null ? TrustedLocalGeneration.Absent : TrustedLocalGeneration.Existing(generation);
        if (generation == null)
        {
            generation = Guid.NewGuid().ToString("N");
            changes.Add(GenerationCreation(generation));
        }
        var outcome = PublishLocalCoreAsync(lease, binding, changes, CancellationToken.None).GetAwaiter().GetResult();
        RequireCommittedLocalPublication(outcome);
        if (!binding.Exists) CanonicalRootAuthorityIdentity.AdvanceSessionGenerationRevision();
        return generation;
    }

    private TrustedLocalFileChange GenerationCreation(string generation) => new(SessionGenerationPath, null,
        JsonSerializer.SerializeToUtf8Bytes(new SessionGenerationDocument(1, generation)));

    /// <summary>
    /// Publishes a missing session generation through the existing trusted-local transaction.
    /// </summary>
    /// <param name="lease">
    /// Original active canonical lease; ownership and disposal remain with its caller.
    /// </param>
    /// <returns>
    /// The newly published logical session generation.
    /// </returns>
    private string CreateTrustedLocalGeneration(CanonicalWriteLease lease)
    {
        using var validationMutation = BeginValidationMutation(lease);
        EnsureWorkerGeneralMutationAllowed(lease);
        VerifyCurrentSessionOperation(lease);
        if (lease.IsLegacyStorageRecovery)
            throw new InvalidDataException("Legacy recovery cannot invent a missing session generation.");
        var generation = Guid.NewGuid().ToString("N");
        var outcome = PublishLocalCoreAsync(lease, TrustedLocalGeneration.Absent,
            [GenerationCreation(generation)], CancellationToken.None).GetAwaiter().GetResult();
        RequireCommittedLocalPublication(outcome);
        CanonicalRootAuthorityIdentity.AdvanceSessionGenerationRevision();
        return generation;
    }

    /// <summary>
    /// Publishes declared canonical changes while retaining mutation admission from generation preparation through completion.
    /// </summary>
    /// <param name="lease">
    /// Original active canonical lease; ownership and disposal remain with its caller.
    /// </param>
    /// <param name="changes">
    /// Declared members and their original before and desired after states; the publication route validates the set.
    /// </param>
    /// <param name="cancellationToken">
    /// Cancellation observed by the existing operation; the default token does not request cancellation.
    /// </param>
    /// <param name="validatePreparedNamespace">
    /// Optional namespace validation callback before publication; <see langword="null"/> adds no callback.
    /// </param>
    /// <param name="reusePreparedProofData">
    /// Requests cleanup proof DATA reuse inside the prepared callback. The default
    /// <see langword="false"/> retains the ordinary publication path.
    /// </param>
    /// <returns>
    /// The original committed, rolled-back or uncertain publication outcome.
    /// </returns>
    internal async Task<TrustedLocalPublicationOutcome> PublishLocalFilesAsync(CanonicalWriteLease lease,
        IReadOnlyList<CanonicalLocalFileChange> changes, CancellationToken cancellationToken = default,
        Func<Task>? validatePreparedNamespace = null, bool reusePreparedProofData = false)
    {
        using var totalTiming = MeasureOperation("LocalPublication.Total");
        using var admissionTiming = MeasureOperation("LocalPublication.Admission");
        using var validationMutation = BeginValidationMutation(lease);
        lease.EnsureNoPendingLocalDecision();
        VerifyCurrentSessionOperation(lease);
        if (changes.Count == 0 || changes.Any(change => !UsesTrustedLocalWriter(lease, change.RelativePath)))
            throw new InvalidOperationException("A local publication requires ordinary declared canonical members.");
        admissionTiming?.Dispose();
        string generation;
        using (MeasureOperation("LocalPublication.GetGeneration"))
            generation = GetOrCreateSessionGeneration(lease);
        var binding = TrustedLocalGeneration.Existing(generation);
        TrustedLocalFileChange[] preparedChanges;
        using (MeasureOperation("LocalPublication.PrepareChanges"))
            preparedChanges = changes.Select(change => new TrustedLocalFileChange(ResolvePath(change.RelativePath), change.Before, change.After)).ToArray();
        using (MeasureOperation("LocalPublication.PublishCore"))
            return await PublishLocalCoreAsync(lease, binding, preparedChanges,
                cancellationToken, validatePreparedNamespace,
                preparedProofMutation: reusePreparedProofData ? validationMutation : null);
    }

    /// <summary>
    /// Publishes trusted-local changes under their declared generation through preparation, retry, compensation and cleanup.
    /// </summary>
    /// <param name="lease">
    /// Original active canonical lease; ownership and disposal remain with its caller.
    /// </param>
    /// <param name="generation">
    /// Original trusted-local generation binding for this publication.
    /// </param>
    /// <param name="changes">
    /// Declared members and their original before and desired after states; the publication route validates the set.
    /// </param>
    /// <param name="cancellationToken">
    /// Cancellation observed by the existing operation; the default token does not request cancellation.
    /// </param>
    /// <param name="validatePreparedNamespace">
    /// Optional namespace validation callback before publication; <see langword="null"/> adds no callback.
    /// </param>
    /// <param name="standaloneDarenProfile">
    /// Whether publication is restricted to the registered standalone profile member.
    /// </param>
    /// <param name="preparedProofMutation">
    /// The opted-in cleanup publisher's actual outer mutation token, or null for ordinary publication.
    /// </param>
    /// <returns>
    /// The original committed, rolled-back or uncertain publication outcome.
    /// </returns>
    private async Task<TrustedLocalPublicationOutcome> PublishLocalCoreAsync(CanonicalWriteLease lease,
        TrustedLocalGeneration generation, IReadOnlyList<TrustedLocalFileChange> changes, CancellationToken cancellationToken,
        Func<Task>? validatePreparedNamespace = null, bool standaloneDarenProfile = false,
        MainMutationReservation? preparedProofMutation = null)
    {
        using var totalTiming = MeasureOperation("LocalPublication.Core.Total");
        using var admissionTiming = MeasureOperation("LocalPublication.Core.Admission");
        using var validationMutation = BeginValidationMutation(lease);
        lease.EnsureNoPendingLocalDecision();
        if (standaloneDarenProfile)
        {
            RequireStandaloneDarenProfile(lease);
            if (changes.Count != 1 || changes[0].Path != RegisteredDarenProfilePath)
                throw new InvalidOperationException("Standalone Daren publication requires its exact registered member.");
        }
        EnsureWorkerPurposePublication(lease, generation, changes);
        admissionTiming?.Dispose();
        if (lease.BrowserLocalAccess is { } browser)
        {
            using var browserIntentTiming = MeasureOperation("LocalPublication.Core.BrowserIntent");
            browser.Validate();
            foreach (var change in changes)
                if (browser.RecordIntent != null) await browser.RecordIntent(change.Path, change.After);
        }
        using var prepareScopeTiming = MeasureOperation("LocalPublication.Core.PrepareScope");
        var scope = new TrustedLocalFileScope([BasePath]);
        var registrations = new List<InProcessMutationRegistration>();
        try
        {
            foreach (var change in changes)
            {
                scope.EnsureDirectory(Path.GetDirectoryName(change.Path)!);
                scope.ValidateFile(change.Path);
                registrations.Add(new InProcessMutationRegistration(change.Path));
            }
            prepareScopeTiming?.Dispose();
            using var boundaryTiming = MeasureOperation("LocalPublication.Core.ValidateMutationBoundaries");
            var windows = OperatingSystem.IsWindows();
            var generationPath = TrustedLocalFilePublication.NormalizeAuthorityPath(SessionGenerationPath, windows);
            foreach (var change in changes)
            {
                var memberPath = scope.ValidateFile(change.Path);
                if (string.Equals(memberPath, generationPath,
                        windows ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal)) continue;
                if (lease.BrowserLocalAccess?.ProfilePath == memberPath || standaloneDarenProfile)
                {
                    if (standaloneDarenProfile) RequireStandaloneDarenProfile(lease);
                    else RequireDeclaredDarenProfile(lease);
                    await InvokeBeforeCanonicalMutationBoundaryAsync("@daren_reward_profile");
                    scope.ValidateFile(memberPath);
                    await InvokeAfterCanonicalMutationBoundaryValidatedAsync("@daren_reward_profile");
                    if (standaloneDarenProfile) RequireStandaloneDarenProfile(lease);
                    continue;
                }
                var relative = GetLocalRelativePath(GameSessionPath, memberPath, windows);
                var canonicalPath = ResolvePath(relative);
                await InvokeBeforeCanonicalMutationBoundaryAsync(relative);
                EnsureCanonicalMutationBoundary(relative, canonicalPath);
                await InvokeAfterCanonicalMutationBoundaryValidatedAsync(relative);
            }
            boundaryTiming?.Dispose();
            using var beforePublishTiming = MeasureOperation("LocalPublication.Core.AdmissionBeforePublish");
            cancellationToken.ThrowIfCancellationRequested();
            VerifyCurrentSessionOperation(lease);
            EnsureWorkerPurposePublication(lease, generation, changes);
            beforePublishTiming?.Dispose();
            var publisher = new TrustedLocalFilePublication(this, scope);
            for (var attempt = 0; ; attempt++)
            {
                using (MeasureOperation("LocalPublication.Core.AdmissionBeforePublish"))
                    EnsureWorkerPurposePublication(lease, generation, changes);
                if (validatePreparedNamespace != null)
                {
                    using var namespaceTiming = MeasureOperation("LocalPublication.Core.ValidatePreparedNamespace");
                    var previous = _cleanupProofReadContext.Value;
                    var context = preparedProofMutation == null ? null :
                        new CleanupProofReadContext(lease, preparedProofMutation, validationMutation, generation.Id);
                    _cleanupProofReadContext.Value = context;
                    try { await validatePreparedNamespace(); }
                    finally
                    {
                        if (context != null) context.Active = false;
                        _cleanupProofReadContext.Value = previous;
                    }
                }
                TrustedLocalPublicationOutcome outcome;
                using (MeasureOperation("LocalPublication.Attempt.PublishWithOutcome"))
                using (EnterOperationTimingContext())
                {
                    outcome = publisher.PublishWithOutcome(lease, generation, changes, _hooks?.LocalPublicationObserver);
                }
                if (outcome.Disposition != TrustedLocalPublicationDisposition.RolledBack ||
                    outcome.Failure is InvalidDataException || outcome.Failure == null ||
                    !IsTransientFileAccessException(outcome.Failure) || attempt >= TransientFileAccessRetryCount - 1)
                    return outcome;
                using (MeasureOperation("LocalPublication.Core.RetryDelay"))
                    await Task.Delay(TransientFileAccessRetryDelay, cancellationToken);
                VerifyCurrentSessionOperation(lease);
            }
        }
        finally
        {
            using var registrationsTiming = MeasureOperation("LocalPublication.Core.FinallyRegistrations");
            foreach (var registration in registrations) registration.Dispose();
        }
    }

    internal void RequireCommittedLocalPublication(TrustedLocalPublicationOutcome outcome)
    {
        if (outcome.Disposition == TrustedLocalPublicationDisposition.Committed)
        {
            if (outcome.Failure != null)
            {
                try { _logger.LogWarning(outcome.Failure, "Local publication committed; journal cleanup remains pending."); }
                catch { /* Diagnostic transport cannot reverse an established committed result. */ }
            }
            return;
        }
        if (outcome.Disposition == TrustedLocalPublicationDisposition.RolledBack && outcome.Failure != null)
            ExceptionDispatchInfo.Capture(outcome.Failure).Throw();
        throw new CoordinatedStatePublicationUncertainException(outcome.Failure);
    }

    internal void LogCompletedCoordinatedWriteReleaseFailure(Exception failure) =>
        _logger.LogWarning(failure, "Coordinated write completed; canonical lease release requires follow-up.");

    // Byte authority for the trusted-local route. The legacy physical reader
    // both accepts Unix special-file attributes and requires one Windows link;
    // neither is the contract of a participating-writer, by-name publication.
    internal async Task<byte[]?> ReadLocalFileBytesAsync(CanonicalWriteLease lease, string relativePath,
        CancellationToken cancellationToken = default)
    {
        VerifyCurrentSessionOperation(lease);
        if (!UsesTrustedLocalWriter(lease, relativePath))
            throw new InvalidOperationException("A local byte read cannot replace a legacy transaction's physical authority.");
        var scope = new TrustedLocalFileScope([GameSessionPath]);
        var expectedPath = ResolvePath(relativePath);
        for (var attempt = 0; ; attempt++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var path = scope.ValidateFile(expectedPath);
            if (!File.Exists(path)) return null;
            await InvokeBeforeCanonicalReadOpenAsync(relativePath);
            cancellationToken.ThrowIfCancellationRequested();
            VerifyCurrentSessionOperation(lease);
            EnsureCanonicalPathStillSafe(relativePath, expectedPath);
            scope.ValidateFile(path); // Revalidate after the read boundary hook, before opening.
            FileStream? stream;
            try
            {
                stream = OpenValidatedOrdinaryFile(scope, path, asynchronous: true);
                if (stream == null) return null;
            }
            catch (Exception ex) when (IsTransientOrdinaryReadOpenException(ex) && attempt < TransientFileAccessRetryCount)
            {
                await Task.Delay(TransientFileAccessRetryDelay, cancellationToken);
                continue;
            }
            await using (stream)
            {
                if (_hooks?.AfterCanonicalReadInitialValidationAsync != null)
                    await _hooks.AfterCanonicalReadInitialValidationAsync(relativePath);
                ObservePassiveRead(_hooks?.CanonicalReadActivityObserver, relativePath);
                var snapshot = await ReadOpenedOrdinarySnapshotAsync(stream, scope, relativePath, expectedPath, cancellationToken);
                VerifyCurrentSessionOperation(lease);
                return snapshot.Content;
            }
        }
    }

    /// <summary>
    /// Publishes an ordinary member and enforces the original trusted-local committed-outcome contract.
    /// </summary>
    /// <param name="lease">
    /// The caller-owned active canonical write lease.
    /// </param>
    /// <param name="relativePath">
    /// The ordinary canonical session-relative member to publish.
    /// </param>
    /// <param name="desired">
    /// Desired bytes, or <see langword="null"/> to request deletion.
    /// </param>
    /// <returns>
    /// Completion after the original publication outcome is required to be committed; original failure mapping is retained.
    /// </returns>
    private async Task WriteTrustedLocalFileAsync(CanonicalWriteLease lease, string relativePath, byte[]? desired)
    {
        using var totalTiming = MeasureOperation("TrustedWrite.Total");
        TrustedLocalPublicationOutcome outcome;
        using (MeasureOperation("TrustedWrite.PublishOutcome"))
            outcome = await PublishOrdinaryFileWithOutcomeAsync(lease, relativePath, desired);
        using (MeasureOperation("TrustedWrite.RequireCommitted"))
            RequireCommittedLocalPublication(outcome);
    }

    // The ordinary facade and retained script command use the same publication.
    // Returning its original outcome permits capture before facade throw/lease close.
    /// <summary>
    /// Reads the current member and publishes its requested bytes through the existing ordinary outcome protocol.
    /// </summary>
    /// <param name="lease">
    /// Original active canonical lease; ownership and disposal remain with its caller.
    /// </param>
    /// <param name="relativePath">
    /// Canonical session-relative member or subtree to operate on.
    /// </param>
    /// <param name="desired">
    /// Desired member bytes; <see langword="null"/> requests deletion.
    /// </param>
    /// <returns>
    /// The original committed, rolled-back or uncertain publication outcome.
    /// </returns>
    internal async Task<TrustedLocalPublicationOutcome> PublishOrdinaryFileWithOutcomeAsync(
        CanonicalWriteLease lease, string relativePath, byte[]? desired)
    {
        using var totalTiming = MeasureOperation("OrdinaryPublication.Total");
        using var admissionTiming = MeasureOperation("OrdinaryPublication.Admission");
        using var validationMutation = BeginValidationMutation(lease);
        EnsureWorkerGeneralMutationAllowed(lease);
        lease.EnsureNoPendingLocalDecision();
        if (!UsesTrustedLocalWriter(lease, relativePath))
            throw new InvalidOperationException("An ordinary outcome requires the current local writer.");
        admissionTiming?.Dispose();
        byte[]? before;
        using (MeasureOperation("OrdinaryPublication.ReadBefore"))
            before = await ReadLocalFileBytesAsync(lease, relativePath);
        using (MeasureOperation("OrdinaryPublication.PublishLocal"))
            return await PublishLocalFilesAsync(lease, [new(relativePath, before, desired)]);
    }

    /// <summary>
    /// Reads and appends text through the existing ordinary publication protocol, returning its original outcome.
    /// </summary>
    /// <param name="lease">
    /// Original active canonical lease; ownership and disposal remain with its caller.
    /// </param>
    /// <param name="relativePath">
    /// Canonical session-relative member or subtree to operate on.
    /// </param>
    /// <param name="content">
    /// Content to publish or append.
    /// </param>
    /// <param name="token">
    /// Cancellation observed by the existing operation; the default token does not request cancellation.
    /// </param>
    /// <returns>
    /// The original committed, rolled-back or uncertain publication outcome.
    /// </returns>
    internal async Task<TrustedLocalPublicationOutcome> AppendOrdinaryFileWithOutcomeAsync(
        CanonicalWriteLease lease, string relativePath, string content, CancellationToken token = default)
    {
        using var validationMutation = BeginValidationMutation(lease);
        EnsureValidCanonicalWriteLease(lease);
        lease.EnsureNoPendingLocalDecision();
        var appended = Encoding.UTF8.GetBytes(content);
        EnsureWorkerAuditAppend(lease, relativePath, appended);
        token.ThrowIfCancellationRequested();
        if (!UsesTrustedLocalWriter(lease, relativePath))
            throw new InvalidOperationException("An ordinary outcome requires the current local writer.");
        var before = await ReadLocalFileBytesAsync(lease, relativePath, token);
        var next = (before ?? Encoding.UTF8.GetPreamble()).Concat(appended).ToArray();
        EnsureWorkerAuditAppend(lease, relativePath, appended);
        return await PublishLocalFilesAsync(lease, [new(relativePath, before, next)], token);
    }
}
