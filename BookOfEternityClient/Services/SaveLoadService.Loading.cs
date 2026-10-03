using System.Buffers;
using System.Collections.Frozen;
using System.IO.Compression;
using System.Text.Json.Nodes;
using BookOfEternityClient.Configuration;
using BookOfEternityClient.Core;
using Microsoft.Extensions.Logging;

namespace BookOfEternityClient.Services;

/// <summary>Distinguishes load admission from a committed, restored or unresolved replacement.</summary>
internal enum LoadReplacementDisposition { NotLoaded, Committed, RolledBack, Uncertain }

/// <summary>Retains selected source and generation authority through replacement follow-up.</summary>
internal sealed record LoadReplacementResult(LoadReplacementDisposition Disposition,
    string? SelectedSourcePath, string? EstablishedGeneration, bool NeedsFollowUp,
    Exception? Failure, bool ContinuationBlocked = false)
{
    internal LoadReplacementResult WithFollowUp(Exception failure, bool blocksContinuation = false) => this with
    {
        NeedsFollowUp = true,
        ContinuationBlocked = ContinuationBlocked || blocksContinuation,
        Failure = Failure == null ? failure : new AggregateException(Failure, failure)
    };
}

/// <summary>
/// Retains the two failures and the owned private residue when load preparation cannot clean up.
/// This exception does not itself represent a canonical publication decision.
/// </summary>
internal sealed class LoadPreparationCleanupException : InvalidOperationException
{
    /// <summary>
    /// Records the admitted source, owned staging directory and both preparation failures.
    /// </summary>
    /// <param name="sourcePath">
    /// The exact admitted read-only archive path.
    /// </param>
    /// <param name="stagingRoot">
    /// The owned private staging directory that could not be cleaned up.
    /// </param>
    /// <param name="preparationFailure">
    /// The primary failure before publication authority was acquired.
    /// </param>
    /// <param name="cleanupFailure">
    /// The additional failure while cleaning the private staging directory.
    /// </param>
    internal LoadPreparationCleanupException(string sourcePath, string stagingRoot,
        Exception preparationFailure, Exception cleanupFailure)
        : base("Load preparation failed and its private staging directory requires follow-up.",
            new AggregateException(preparationFailure, cleanupFailure))
    {
        SourcePath = sourcePath;
        StagingRoot = stagingRoot;
    }

    /// <summary>
    /// Gets the admitted archive path, which remains read-only during preparation.
    /// </summary>
    internal string SourcePath { get; }

    /// <summary>
    /// Gets the owned private directory retained after its cleanup failed.
    /// </summary>
    internal string StagingRoot { get; }
}

/// <summary>Owns closed incoming images and the exact selected read-only archive until one decision.</summary>
internal sealed class PreparedLoadArchive : IAsyncDisposable
{
    private readonly string _stagingRoot;
    private readonly TrustedLocalFileScope _sourceScope;
    private readonly TrustedLocalFileImage _sourceImage;
    private readonly TrustedLocalFileScope _scratchScope;
    private readonly HashSet<string> _directories;
    private bool _disposed;

    internal PreparedLoadArchive(string sourcePath, TrustedLocalFileScope sourceScope,
        TrustedLocalFileImage sourceImage, string stagingRoot, IReadOnlyDictionary<string, TrustedLocalFileImage> images,
        GameSettings? archiveSettings)
    {
        SourcePath = sourcePath;
        _sourceScope = sourceScope;
        _sourceImage = sourceImage;
        _stagingRoot = stagingRoot;
        _scratchScope = new TrustedLocalFileScope([stagingRoot]);
        Images = images;
        ArchiveSettings = archiveSettings;
        _directories = EnumerateScratch().Directories;
    }

    internal string SourcePath { get; }
    internal IReadOnlyDictionary<string, TrustedLocalFileImage> Images { get; }
    internal GameSettings? ArchiveSettings { get; }

    internal void Revalidate()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (!_sourceImage.MatchesFile(_sourceScope, SourcePath))
            throw new InvalidDataException("The selected archive changed after admission.");
        var actual = EnumerateScratch();
        if (!_directories.SetEquals(actual.Directories) || !actual.Files.SetEquals(Images.Keys))
            throw new InvalidDataException("The complete load staging namespace changed after preparation.");
        foreach (var (relative, image) in Images)
            if (!image.MatchesFile(_scratchScope, Path.Combine(_stagingRoot, relative.Replace('/', Path.DirectorySeparatorChar))))
                throw new InvalidDataException("A load staging image changed after preparation.");
    }

    private (HashSet<string> Files, HashSet<string> Directories) EnumerateScratch()
    {
        var comparer = OperatingSystem.IsWindows() ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal;
        var files = new HashSet<string>(comparer);
        var directories = new HashSet<string>(comparer);
        var pending = new Queue<string>();
        pending.Enqueue(_stagingRoot);
        while (pending.TryDequeue(out var directory))
        {
            directory = _scratchScope.ValidateDirectory(directory, allowMissing: false);
            directories.Add(directory);
            foreach (var entry in Directory.EnumerateFileSystemEntries(directory))
            {
                if ((File.GetAttributes(entry) & FileAttributes.Directory) != 0)
                    pending.Enqueue(_scratchScope.ValidateDirectory(entry, allowMissing: false));
                else
                    files.Add(FileSystemManager.GetLocalRelativePath(_stagingRoot,
                        _scratchScope.ValidateFile(entry, allowMissing: false), OperatingSystem.IsWindows()));
            }
        }
        return (files, directories);
    }

    public ValueTask DisposeAsync()
    {
        if (_disposed) return ValueTask.CompletedTask;
        _scratchScope.DeleteOwnedTree(_stagingRoot);
        _disposed = true;
        return ValueTask.CompletedTask;
    }
}

public partial class SaveLoadService
{
    /// <summary>
    /// Prepares and publishes an ordinary portable load while retaining its decision and follow-up failures.
    /// Public callers remain on their original implementation until the downstream cutover gates pass.
    /// </summary>
    /// <param name="saveFilePath">
    /// The selected archive's absolute path or session-relative file path.
    /// </param>
    /// <param name="cancellationToken">
    /// Cancels preparation or authority acquisition before an established publication decision.
    /// The default token does not request cancellation.
    /// </param>
    /// <returns>
    /// The confirmed committed or rolled-back generation, or an admission refusal or uncertain decision.
    /// Follow-up failures retain any decision already established; uncertainty blocks continuation.
    /// </returns>
    internal async Task<LoadReplacementResult> LoadGameWithOutcomeAsync(string saveFilePath,
        CancellationToken cancellationToken = default)
    {
        // Invalid/closing contexts retain their thrown fence. A valid binding is a known admission refusal.
        if (SessionOperationContext.TryGetExpectedGeneration(_fs.BasePath, out _))
            return new(LoadReplacementDisposition.NotLoaded, null, null, false,
                new InvalidOperationException("Load cannot run inside a generation-bound session operation."));

        PreparedLoadArchive? candidate = null;
        FileSystemManager.SessionLifecycleLease? lifecycleLease = null;
        FileSystemManager.CanonicalWriteLease? writeLease = null;
        LoadReplacementResult? result = null;
        try
        {
            candidate = await PrepareLoadArchiveAsync(saveFilePath, cancellationToken);
            if (_hooks?.BeforeLoadLeaseAcquisitionAsync != null)
                await _hooks.BeforeLoadLeaseAcquisitionAsync();
            candidate.Revalidate();
            lifecycleLease = await _fs.AcquireSessionLifecycleLeaseAsync();
            writeLease = await _fs.AcquireSessionReplacementWriteLeaseAsync(lifecycleLease, cancellationToken);
            _fs.ResolveBackupPublicationRecovery(writeLease);
            candidate.Revalidate();
            var generation = _fs.ReadLocalGenerationSnapshot(writeLease);
            var live = _fs.CaptureLoadReplacementNamespace(writeLease, candidate.SourcePath);
            var settings = candidate.ArchiveSettings ?? StateManager.PrepareLocalLoadSettings(
                await _fs.ReadLocalFileBytesAsync(writeLease, "config.json"));
            var namespacePlan = _fs.CreateLoadReplacementNamespacePlan(live, candidate.Images,
                preserveConfiguration: candidate.ArchiveSettings == null);

            var replacement = Guid.NewGuid().ToString("N");
            var outcome = await _fs.PublishLoadReplacementNamespaceAsync(writeLease, generation, namespacePlan, replacement,
                candidate.Revalidate, cancellationToken);
            result = new(outcome.Disposition switch
            {
                TrustedLocalPublicationDisposition.Committed => LoadReplacementDisposition.Committed,
                TrustedLocalPublicationDisposition.RolledBack => LoadReplacementDisposition.RolledBack,
                _ => LoadReplacementDisposition.Uncertain
            }, candidate.SourcePath,
                outcome.Disposition switch
                {
                    TrustedLocalPublicationDisposition.Committed => replacement,
                    TrustedLocalPublicationDisposition.RolledBack => generation.Binding.Id,
                    _ => null
                },
                outcome.Failure != null || outcome.Disposition == TrustedLocalPublicationDisposition.Uncertain,
                outcome.Failure,
                outcome.Disposition == TrustedLocalPublicationDisposition.Uncertain);

            if (result.Disposition == LoadReplacementDisposition.Committed)
            {
                // From this point every failure is follow-up to the established replacement.
                _fs.ResolveBackupPublicationRecovery(writeLease);
                if (_hooks?.AfterLoadPublicationValidatedAsync != null)
                    await _hooks.AfterLoadPublicationValidatedAsync();
                await _stateManager.RefreshGameStateAsync(writeLease);
                _stateManager.Settings.ApplyLoadedValues(settings);
            }
        }
        catch (Exception failure)
        {
            if (result != null) result = result.WithFollowUp(failure, blocksContinuation: true);
            else
            {
                var uncertain = failure is CoordinatedStatePublicationUncertainException;
                var preparationDebt = failure as LoadPreparationCleanupException;
                result = new(uncertain ? LoadReplacementDisposition.Uncertain : LoadReplacementDisposition.NotLoaded,
                    candidate?.SourcePath ?? preparationDebt?.SourcePath, null,
                    uncertain || preparationDebt != null, failure, uncertain || failure is SessionReplacedException);
            }
        }
        finally
        {
            // Unresolved decisions retain their private source as well as authoritative B1 evidence.
            if (candidate != null && result?.Disposition != LoadReplacementDisposition.Uncertain)
            {
                try { await candidate.DisposeAsync(); }
                catch (Exception failure) { result = result!.WithFollowUp(failure); }
            }
            if (writeLease != null)
            {
                try { await writeLease.DisposeAsync(); }
                catch (Exception failure) { result = result!.WithFollowUp(failure, blocksContinuation: true); }
            }
            if (lifecycleLease != null)
            {
                try { await lifecycleLease.DisposeAsync(); }
                catch (Exception failure) { result = result!.WithFollowUp(failure, blocksContinuation: true); }
            }
        }
        try
        {
            if (result!.Disposition == LoadReplacementDisposition.Committed)
                _logger.LogInformation("Игра загружена: {Path}", result.SelectedSourcePath);
            else _logger.LogError(result.Failure, "Ошибка загрузки: {Path}", saveFilePath);
        }
        catch (Exception failure) { result = result!.WithFollowUp(failure); }
        return result!;
    }

    /// <summary>
    /// Validates the original archive and captures closed private images without changing the live session.
    /// </summary>
    /// <param name="saveFilePath">
    /// The selected archive's absolute path or session-relative file path, which must identify an existing file.
    /// </param>
    /// <param name="cancellationToken">
    /// Cancels preparation; the default token does not request cancellation.
    /// </param>
    /// <returns>
    /// The admitted source, detached settings and captured images. The caller owns disposal of the private
    /// extraction directory and must revalidate the candidate before publication.
    /// </returns>
    internal async Task<PreparedLoadArchive> PrepareLoadArchiveAsync(string saveFilePath,
        CancellationToken cancellationToken = default)
    {
        if (SessionOperationContext.TryGetExpectedGeneration(_fs.BasePath, out _))
            throw new InvalidOperationException("Load cannot run inside a generation-bound session operation.");
        cancellationToken.ThrowIfCancellationRequested();
        var fullPath = Path.IsPathRooted(saveFilePath) ? saveFilePath : _fs.ResolvePath(saveFilePath);
        fullPath = Path.GetFullPath(fullPath);
        var sourceScope = new TrustedLocalFileScope([], [fullPath]);
        fullPath = sourceScope.ValidateFile(fullPath, allowMissing: false);
        var sourceImage = TrustedLocalFileImage.CaptureFile(sourceScope, fullPath);
        string? stagingRoot = null;
        try
        {
            stagingRoot = _fs.CreateRuntimeLoadStagingRoot();
            var scratch = new TrustedLocalFileScope([stagingRoot]);
            var entries = new Dictionary<string, ZipArchiveEntry>(OperatingSystem.IsWindows()
                ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal);
            using (var source = sourceImage.OpenRead())
            {
                ValidateTrustedArchiveBeforeMaterialization(source);
                using var archive = new ZipArchive(source, ZipArchiveMode.Read, leaveOpen: true);
                await ValidateArchiveStructureAsync(archive, stagingRoot);
                foreach (var entry in archive.Entries)
                {
                    if (string.IsNullOrEmpty(entry.Name)) continue;
                    // Windows GetFullPath can erase trailing spaces. Check original
                    // spelling after original manifest/hash admission, before resolution.
                    if (!entry.FullName.Equals(entry.FullName.Trim(), StringComparison.Ordinal))
                        throw new InvalidDataException($"Load payload '{entry.FullName}' does not use canonical spelling.");
                    var relative = NormalizeArchiveEntryPath(stagingRoot, entry.FullName);
                    if (!relative.Equals(relative.Trim(), StringComparison.Ordinal))
                        throw new InvalidDataException($"Load payload '{relative}' does not use canonical spelling.");
                    relative = CanonicalizeFixedLoadStatePath(relative);
                    if (relative.Equals("saves", StringComparison.OrdinalIgnoreCase) ||
                        relative.StartsWith("saves/", StringComparison.OrdinalIgnoreCase))
                        throw new InvalidDataException("A save archive cannot replace the live saves library.");
                    if (relative.Equals(SaveManifestArchivePath, StringComparison.OrdinalIgnoreCase) || IsEphemeralArchivePath(relative)) continue;
                    ValidateLoadSourceCollision(_fs.ResolvePath(relative), fullPath);
                    entries.Add(relative, entry);
                }
                // Validate every destination topology before creating any extraction file.
                foreach (var relative in entries.Keys)
                {
                    var parent = Path.GetDirectoryName(relative.Replace('/', Path.DirectorySeparatorChar));
                    while (!string.IsNullOrEmpty(parent))
                    {
                        if (entries.ContainsKey(parent.Replace(Path.DirectorySeparatorChar, '/')))
                            throw new InvalidDataException("A save archive contains conflicting file and directory payloads.");
                        parent = Path.GetDirectoryName(parent);
                    }
                }
                foreach (var (relative, entry) in entries)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    var target = Path.Combine(stagingRoot, relative.Replace('/', Path.DirectorySeparatorChar));
                    scratch.EnsureDirectory(Path.GetDirectoryName(target)!);
                    using var input = entry.Open();
                    using var output = new FileStream(scratch.ValidateFile(target), FileMode.CreateNew,
                        FileAccess.Write, FileShare.None, TrustedLocalFileImage.CopyBufferSize, FileOptions.SequentialScan);
                    var buffer = ArrayPool<byte>.Shared.Rent(TrustedLocalFileImage.CopyBufferSize);
                    try
                    {
                        long count = 0;
                        int read;
                        while ((read = await input.ReadAsync(buffer.AsMemory(0, TrustedLocalFileImage.CopyBufferSize), cancellationToken)) != 0)
                        {
                            if (read > entry.Length - count) throw new InvalidDataException("A load entry exceeded its declared size.");
                            await output.WriteAsync(buffer.AsMemory(0, read), cancellationToken);
                            count += read;
                        }
                        if (count != entry.Length) throw new InvalidDataException("A load entry was truncated.");
                        output.Flush(flushToDisk: true);
                    }
                    finally { ArrayPool<byte>.Shared.Return(buffer, clearArray: true); }
                }
            }
            if (!sourceImage.MatchesFile(sourceScope, fullPath))
                throw new InvalidDataException("The selected archive changed during preparation.");
            if (_hooks?.AfterLoadArchiveExtractedAsync != null)
                await _hooks.AfterLoadArchiveExtractedAsync(stagingRoot);
            PrepareDetachedLoadProfile(stagingRoot, entries.Keys);
            var images = entries.Keys.ToDictionary(relative => relative,
                relative => TrustedLocalFileImage.CaptureFile(scratch,
                    Path.Combine(stagingRoot, relative.Replace('/', Path.DirectorySeparatorChar))),
                OperatingSystem.IsWindows() ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal);
            var config = Path.Combine(stagingRoot, "config.json");
            var settings = images.ContainsKey("config.json") ? StateManager.PrepareLocalLoadSettings(File.ReadAllBytes(config)) : null;
            var candidate = new PreparedLoadArchive(fullPath, sourceScope, sourceImage, stagingRoot, images, settings);
            candidate.Revalidate();
            stagingRoot = null;
            return candidate;
        }
        catch (Exception failure)
        {
            if (stagingRoot != null)
            {
                try
                {
                    if (_hooks?.BeforeLoadPreparationCleanupAsync != null)
                        await _hooks.BeforeLoadPreparationCleanupAsync(stagingRoot);
                    new TrustedLocalFileScope([stagingRoot]).DeleteOwnedTree(stagingRoot);
                }
                catch (Exception cleanup)
                {
                    throw new LoadPreparationCleanupException(fullPath, stagingRoot, failure, cleanup);
                }
            }
            throw;
        }
    }

    /// <summary>
    /// Maps only the fixed runtime-consumed state paths after original archive validation.
    /// </summary>
    /// <param name="relative">
    /// The normalized, canonically spaced relative payload path.
    /// </param>
    /// <returns>
    /// The fixed canonical path when matched ignoring case; otherwise the unchanged payload path.
    /// </returns>
    private static string CanonicalizeFixedLoadStatePath(string relative)
        => FixedLoadStatePaths.TryGetValue(relative, out var canonical) ? canonical : relative;

    private static readonly FrozenDictionary<string, string> FixedLoadStatePaths = CreateFixedLoadStatePaths();

    /// <summary>
    /// Captures existing explicit runtime path authorities without granting import or write authority.
    /// </summary>
    /// <returns>
    /// An immutable lookup from case aliases to each declared whole-file spelling.
    /// Contradictory declarations fail rather than choosing a spelling implicitly.
    /// </returns>
    private static FrozenDictionary<string, string> CreateFixedLoadStatePaths()
    {
        string[] supplements =
        [
            SoulStateArchivePath,
            ResourceMaterializationContract.DefinitionsPath,
            ResourceMaterializationContract.StatePath,
            ResourceMaterializationContract.HistoryPath,
            CanonicalResourceOwnerAuthorityComposer.AuthorityPath,
            "config.json",
            "game_state/history/chat_log.json",
            AfterlifeEntityProfileState.StatePath,
            ShiningAbodeState.StatePath,
            WorldDirectiveService.ActiveDirectivesPath,
            AfterlifeSpiritualConflictState.DifficultySettingsPath,
            PendingTurnStateService.PendingDiceStatePath,
            ResourcePendingResolutionState.PendingPath
        ];
        var declarations = FileMapping.FieldToFile.Values.Concat(FileMapping.OutputFiles.Values)
            .Concat(CanonicalStateNormalizer.NormalizerRollbackTrackedFiles)
            .Concat(AfterlifeContractRegistry.All.Select(surface => surface.Path))
            .Concat(QteSceneService.BrowserTransactionRollbackPaths)
            .Concat(supplements);
        var paths = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var path in declarations)
        {
            if (paths.TryGetValue(path, out var existing) && !existing.Equals(path, StringComparison.Ordinal))
                throw new InvalidOperationException($"Conflicting fixed load path declarations: '{existing}' and '{path}'.");
            paths[path] = path;
        }
        return paths.ToFrozenDictionary(StringComparer.OrdinalIgnoreCase);
    }

    private static void ValidateLoadSourceCollision(string destination, string source)
    {
        var windows = OperatingSystem.IsWindows();
        var comparison = windows ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
        destination = TrustedLocalFilePublication.NormalizeAuthorityPath(destination, windows);
        source = TrustedLocalFilePublication.NormalizeAuthorityPath(source, windows);
        if (destination.Equals(source, comparison) || destination.StartsWith(source + Path.DirectorySeparatorChar, comparison) ||
            source.StartsWith(destination + Path.DirectorySeparatorChar, comparison))
            throw new InvalidDataException("Incoming payload collides with the selected archive or its topology.");
    }

    private static void PrepareDetachedLoadProfile(string stagingRoot, IEnumerable<string> paths)
    {
        var set = paths.ToHashSet(OperatingSystem.IsWindows() ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal);
        byte[]? Read(string relative) => set.Contains(relative)
            ? File.ReadAllBytes(Path.Combine(stagingRoot, relative.Replace('/', Path.DirectorySeparatorChar))) : null;
        var original = AfterlifeEntityProfileState.DecodeMirrorObject(Read(AfterlifeEntityProfileState.StatePath));
        if (original == null) return;
        var projected = original.DeepClone().AsObject();
        AfterlifeEntityProfileState.ApplyPlayerSoulProfileClientAuthority(projected,
            AfterlifeEntityProfileState.DecodeMirrorObject(Read(SoulStateArchivePath)),
            AfterlifeEntityProfileState.DecodeMirrorObject(Read(ShiningAbodeState.StatePath)));
        if (!JsonNode.DeepEquals(original, projected))
        {
            var path = Path.Combine(stagingRoot, AfterlifeEntityProfileState.StatePath.Replace('/', Path.DirectorySeparatorChar));
            using var output = new FileStream(path, FileMode.Truncate, FileAccess.Write, FileShare.None);
            output.Write(FileSystemManager.EncodeUtf8WithPreamble(projected.ToJsonString(SharedJsonOptions.PrettyCamelCaseUnsafeRelaxed)));
            output.Flush(flushToDisk: true);
        }
    }
}
