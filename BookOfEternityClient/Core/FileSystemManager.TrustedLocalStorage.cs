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

    internal async Task RunLegacyStorageRecoveryAsync(CanonicalWriteLease lease, Func<Task> recovery)
    {
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

    private bool HasStorageEvidence(string root)
    {
        var kind = PhysicalFileAuthority.ProbeNamespaceEntryFromRoot(_basePath, root, "Storage evidence root");
        if (kind == PhysicalFileAuthority.NamespaceEntryKind.Missing) return false;
        if (kind != PhysicalFileAuthority.NamespaceEntryKind.Directory)
            throw new InvalidDataException("Storage evidence root is not an ordinary directory: " + root);
        return Directory.EnumerateFileSystemEntries(root).Any();
    }

    private void EnsureNoLegacyStorageEvidence(CanonicalWriteLease? ownedBrowser = null, bool allowPortableBrowser = false)
    {
        foreach (var root in LegacyStorageRoots)
        {
            if (!HasStorageEvidence(root)) continue;
            if (root == ResolvePath(ExplorerLocalTurnRollbackArtifacts.Root))
            {
                if (allowPortableBrowser) continue; // The original handler preflights schema7 before any recovery.
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

    private void RecoverTrustedLocalStorage(CanonicalWriteLease lease)
    {
        EnsureWorkerRecoveryAdmission(lease);
        lease.EnsureNoPendingLocalDecision();
        if (!HasStorageEvidence(LocalPublicationRoot)) return;
        var before = ReadExistingSessionGeneration(lease);
        new TrustedLocalFilePublication(this, new TrustedLocalFileScope([BasePath]))
            .Recover(lease, _hooks?.LocalPublicationRecoveryObserver);
        if (before != ReadExistingSessionGeneration(lease))
            CanonicalRootAuthorityIdentity.AdvanceSessionGenerationRevision();
    }

    internal string BootstrapLocalStorage(CanonicalWriteLease lease, byte[]? beforeConfig, byte[] desiredConfig)
    {
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

    private string CreateTrustedLocalGeneration(CanonicalWriteLease lease)
    {
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

    internal Task<TrustedLocalPublicationOutcome> PublishLocalFilesAsync(CanonicalWriteLease lease,
        IReadOnlyList<CanonicalLocalFileChange> changes, CancellationToken cancellationToken = default,
        Func<Task>? validatePreparedNamespace = null)
    {
        lease.EnsureNoPendingLocalDecision();
        VerifyCurrentSessionOperation(lease);
        if (changes.Count == 0 || changes.Any(change => !UsesTrustedLocalWriter(lease, change.RelativePath)))
            throw new InvalidOperationException("A local publication requires ordinary declared canonical members.");
        var generation = GetOrCreateSessionGeneration(lease);
        return PublishLocalCoreAsync(lease, TrustedLocalGeneration.Existing(generation),
            changes.Select(change => new TrustedLocalFileChange(ResolvePath(change.RelativePath), change.Before, change.After)).ToArray(),
            cancellationToken, validatePreparedNamespace);
    }

    private async Task<TrustedLocalPublicationOutcome> PublishLocalCoreAsync(CanonicalWriteLease lease,
        TrustedLocalGeneration generation, IReadOnlyList<TrustedLocalFileChange> changes, CancellationToken cancellationToken,
        Func<Task>? validatePreparedNamespace = null, bool standaloneDarenProfile = false)
    {
        lease.EnsureNoPendingLocalDecision();
        if (standaloneDarenProfile)
        {
            RequireStandaloneDarenProfile(lease);
            if (changes.Count != 1 || changes[0].Path != RegisteredDarenProfilePath)
                throw new InvalidOperationException("Standalone Daren publication requires its exact registered member.");
        }
        EnsureWorkerPurposePublication(lease, generation, changes);
        if (lease.BrowserLocalAccess is { } browser)
        {
            browser.Validate();
            foreach (var change in changes)
                if (browser.RecordIntent != null) await browser.RecordIntent(change.Path, change.After);
        }
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
            cancellationToken.ThrowIfCancellationRequested();
            VerifyCurrentSessionOperation(lease);
            EnsureWorkerPurposePublication(lease, generation, changes);
            var publisher = new TrustedLocalFilePublication(this, scope);
            for (var attempt = 0; ; attempt++)
            {
                EnsureWorkerPurposePublication(lease, generation, changes);
                if (validatePreparedNamespace != null) await validatePreparedNamespace();
                var outcome = publisher.PublishWithOutcome(lease, generation, changes, _hooks?.LocalPublicationObserver);
                if (outcome.Disposition != TrustedLocalPublicationDisposition.RolledBack ||
                    outcome.Failure is InvalidDataException || outcome.Failure == null ||
                    !IsTransientFileAccessException(outcome.Failure) || attempt >= TransientFileAccessRetryCount - 1)
                    return outcome;
                await Task.Delay(TransientFileAccessRetryDelay, cancellationToken);
                VerifyCurrentSessionOperation(lease);
            }
        }
        finally
        {
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
                var snapshot = await ReadOpenedOrdinarySnapshotAsync(stream, scope, relativePath, expectedPath, cancellationToken);
                VerifyCurrentSessionOperation(lease);
                return snapshot.Content;
            }
        }
    }

    private async Task WriteTrustedLocalFileAsync(CanonicalWriteLease lease, string relativePath, byte[]? desired)
    {
        RequireCommittedLocalPublication(await PublishOrdinaryFileWithOutcomeAsync(lease, relativePath, desired));
    }

    // The ordinary facade and retained script command use the same publication.
    // Returning its original outcome permits capture before facade throw/lease close.
    internal async Task<TrustedLocalPublicationOutcome> PublishOrdinaryFileWithOutcomeAsync(
        CanonicalWriteLease lease, string relativePath, byte[]? desired)
    {
        EnsureWorkerGeneralMutationAllowed(lease);
        lease.EnsureNoPendingLocalDecision();
        if (!UsesTrustedLocalWriter(lease, relativePath))
            throw new InvalidOperationException("An ordinary outcome requires the current local writer.");
        var before = await ReadLocalFileBytesAsync(lease, relativePath);
        return await PublishLocalFilesAsync(lease, [new(relativePath, before, desired)]);
    }

    internal async Task<TrustedLocalPublicationOutcome> AppendOrdinaryFileWithOutcomeAsync(
        CanonicalWriteLease lease, string relativePath, string content, CancellationToken token = default)
    {
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
