using System.Runtime.ExceptionServices;

namespace BookOfEternityClient.Core;

public partial class FileSystemManager
{
    // Endpoint/status discovery is diagnostic only. It must neither recover a
    // canonical journal nor acquire a write lease or infer authority from a PID.
    internal async Task<string?> ReadDiagnosticStatusAsync(string relativePath)
    {
        if(relativePath is not ("game_state/control/gm_bridge_status.json" or "game_state/control/gm_daemon_status.json"))
            throw new InvalidDataException("Unsupported diagnostic status path.");
        var path=ResolvePath(relativePath);
        var scope=new TrustedLocalFileScope([GameSessionPath]);
        if(!File.Exists(scope.ValidateFile(path)))return null;
        await using var stream=OpenValidatedOrdinaryFile(scope,path,asynchronous:true);
        if(stream==null)return null;
        if(stream.Length>65536)throw new InvalidDataException("Diagnostic status exceeds its bound.");
        var bytes=new byte[65537];var count=0;
        while(count<bytes.Length){var n=await stream.ReadAsync(bytes.AsMemory(count));if(n==0)break;count+=n;}
        if(count>65536)throw new InvalidDataException("Diagnostic status exceeds its bound.");
        return new System.Text.UTF8Encoding(false,true).GetString(bytes,0,count).TrimStart('\uFEFF');
    }

    // .NET's Linux FileStream reports an existing sharing lock as raw errno 11
    // (EWOULDBLOCK), rather than the Windows sharing/lock HRESULTs.
    private static bool IsTransientOrdinaryReadOpenException(Exception exception) =>
        IsTransientReadOpenException(exception) ||
        (OperatingSystem.IsLinux() && exception is IOException && exception.HResult == 11);

    // These entrypoints are deliberately explicit. Browser v6 rollback reads
    // tracked before-images before attaching its physical mutation recorder.
    internal Task<byte[]?> ReadOriginalFileBytesAsync(CanonicalWriteLease lease, string relativePath)
    {
        EnsureValidCanonicalWriteLease(lease);
        return ReadFileBytesCoreAsync(relativePath);
    }

    internal bool OriginalFileExists(CanonicalWriteLease lease, string relativePath)
    {
        EnsureValidCanonicalWriteLease(lease);
        return FileExistsCore(relativePath);
    }

    private async Task<string?> ReadOrdinaryFileCoreAsync(string relativePath, CanonicalWriteLease? lease = null) =>
        DecodeFileSnapshot(await ReadOrdinaryFileSnapshotCoreAsync(relativePath, CancellationToken.None, lease));

    private async Task<byte[]?> ReadOrdinaryFileBytesCoreAsync(string relativePath,
        CancellationToken cancellationToken, CanonicalWriteLease? lease = null) =>
        (await ReadOrdinaryFileSnapshotCoreAsync(relativePath, cancellationToken, lease))?.Content;

    /// <summary>
    /// Classifies ordinary file presence through the existing path and local-scope checks.
    /// </summary>
    /// <param name="relativePath">
    /// Session-relative file path passed unchanged to the canonical resolver and safety check.
    /// </param>
    /// <returns>
    /// <see langword="true"/> for an existing regular file; otherwise, <see langword="false"/> for absence.
    /// Invalid file authority retains its existing exception.
    /// </returns>
    private bool OrdinaryFileExistsCore(string relativePath)
    {
        using var t351Total = MeasureOperation("OrdinaryExists.Total");
        using var t351Resolve = MeasureOperation("OrdinaryExists.ResolvePath");
        var expectedPath = ResolvePath(relativePath);
        t351Resolve?.Dispose();
        using var t351Safety = MeasureOperation("OrdinaryExists.CanonicalSafety");
        EnsureCanonicalPathStillSafe(relativePath, expectedPath);
        t351Safety?.Dispose();
        using var t351Scope = MeasureOperation("OrdinaryExists.PrepareScope");
        var scope = new TrustedLocalFileScope([GameSessionPath]);
        t351Scope?.Dispose();
        using var t351Presence = MeasureOperation("OrdinaryExists.ValidateFileAndPresence");
        // ValidateFile is the non-following type/permission check. File.Exists
        // supplies presence only after invalid/unreadable entries have failed.
        return File.Exists(scope.ValidateFile(expectedPath));
    }

    /// <summary>
    /// Reads one ordinary snapshot through the existing fresh admission, opened-handle and completion checks.
    /// </summary>
    /// <param name="relativePath">
    /// The session-relative file name passed unchanged to the canonical resolver.
    /// </param>
    /// <param name="cancellationToken">
    /// Cancellation observed by the existing admission, open, copy and retry path.
    /// </param>
    /// <param name="lease">
    /// Optional original active lease revalidated at the existing boundaries; <see langword="null"/> selects the lease-free path.
    /// </param>
    /// <returns>
    /// The opened file's content and timestamp, or <see langword="null"/> for observed absence; original validation and data errors propagate.
    /// </returns>
    private async Task<CanonicalFileReadSnapshot?> ReadOrdinaryFileSnapshotCoreAsync(
        string relativePath, CancellationToken cancellationToken, CanonicalWriteLease? lease = null)
    {
#if DEBUG
        var diagnosticOutcome = "fault";
        long diagnosticBytes = 0;
        try
        {
#endif
        using var t326Async0 = this.MeasureOperation("OrdinaryRead.Async.Total");
        cancellationToken.ThrowIfCancellationRequested();
        using var t326Async1 = this.MeasureOperation("OrdinaryRead.Async.PrepareScope");
        using var resolveTiming = MeasureOperation("OrdinaryRead.Async.ResolvePath");
        var expectedPath = ResolvePath(relativePath);
        resolveTiming?.Dispose();
        using var scopeTiming = MeasureOperation("OrdinaryRead.Async.CreateScope");
        var scope = new TrustedLocalFileScope([GameSessionPath]);
        scopeTiming?.Dispose();
        t326Async1?.Dispose();
        FileStream? stream;
        for (var attempt = 0; ; attempt++)
        {
#if DEBUG
            BrowserDiagnosticContent(relativePath, "ordinary-async", "physical-attempt", "started");
#endif
            cancellationToken.ThrowIfCancellationRequested();
            if (lease != null) EnsureValidCanonicalWriteLease(lease);
            stream = null;
            try
            {
                using var t326AsyncAdmission = MeasureOperation("OrdinaryRead.Async.Admission");
                using (MeasureOperation("OrdinaryRead.Async.Admission.CanonicalSafety"))
                    EnsureCanonicalPathStillSafe(relativePath, expectedPath);
                string admittedPath;
                using (MeasureOperation("OrdinaryRead.Async.Admission.ValidateFile"))
                    admittedPath = scope.ValidateFile(expectedPath);
                bool exists;
                using (MeasureOperation("OrdinaryRead.Async.Admission.FileExists"))
                    exists = File.Exists(admittedPath);
                if (!exists)
                {
#if DEBUG
                    diagnosticOutcome = "missing";
#endif
                    return null;
                }
                t326AsyncAdmission?.Dispose();
                using var t326AsyncHook = MeasureOperation("OrdinaryRead.Async.BeforeOpenHookAsync");
                await InvokeBeforeCanonicalReadOpenAsync(relativePath);
                t326AsyncHook?.Dispose();
                using var t326AsyncOpen = MeasureOperation("OrdinaryRead.Async.ValidateAndOpen");
                cancellationToken.ThrowIfCancellationRequested();
                if (lease != null) EnsureValidCanonicalWriteLease(lease);
                using (MeasureOperation("OrdinaryRead.Async.Open.CanonicalSafety"))
                    EnsureCanonicalPathStillSafe(relativePath, expectedPath);
                stream = OpenValidatedOrdinaryFile(scope, expectedPath, asynchronous: true);
                if (stream == null)
                {
#if DEBUG
                    diagnosticOutcome = "missing";
#endif
                    return null;
                }
                t326AsyncOpen?.Dispose();
                using var t326AsyncAfterHook = MeasureOperation("OrdinaryRead.Async.AfterOpenHookAsync");
                if (_hooks?.AfterCanonicalReadInitialValidationAsync != null)
                    await _hooks.AfterCanonicalReadInitialValidationAsync(relativePath);
                t326AsyncAfterHook?.Dispose();
                break;
            }
            catch (Exception ex) when (IsTransientOrdinaryReadOpenException(ex) && attempt < TransientFileAccessRetryCount)
            {
#if DEBUG
                BrowserDiagnosticContent(relativePath, "ordinary-async", "retry", "open-fault");
#endif
                if (stream != null) await stream.DisposeAsync();
                await Task.Delay(TransientFileAccessRetryDelay, cancellationToken);
            }
            catch
            {
                if (stream != null) await stream.DisposeAsync();
                throw;
            }
        }

        await using (stream)
        {
            using var t326AsyncRead = MeasureOperation("OrdinaryRead.Async.ReadOpenedSnapshotAsync");
            var snapshot = await ReadOpenedOrdinarySnapshotAsync(stream, scope, relativePath, expectedPath, cancellationToken);
            if (lease != null) EnsureValidCanonicalWriteLease(lease);
            t326AsyncRead?.Dispose();
#if DEBUG
            diagnosticOutcome = "present"; diagnosticBytes = snapshot.Content.Length;
#endif
            return snapshot;
        }

#if DEBUG
        }
        catch { diagnosticOutcome = "fault"; throw; }
        finally { BrowserDiagnosticContent(relativePath, "ordinary-async", "physical-result", diagnosticOutcome, diagnosticBytes); }
#endif
    }

    private CanonicalFileReadSnapshot? ReadOrdinaryFileSnapshotCore(
        string relativePath, CanonicalWriteLease? lease = null) =>
        ReadOrdinaryFileSnapshotCore(relativePath, lease, out _, nullableDataFailure: false);

    /// <summary>
    /// Reads the original synchronous ordinary snapshot while keeping optional data failures distinct from path authority.
    /// </summary>
    /// <param name="relativePath">
    /// The session-relative file name passed unchanged to the existing resolver and hooks.
    /// </param>
    /// <param name="lease">
    /// Optional original active lease; <see langword="null"/> retains the lease-free ordinary path.
    /// </param>
    /// <param name="dataFailure">
    /// The original captured optional data error, or <see langword="null"/> when no qualifying data error occurred.
    /// </param>
    /// <param name="nullableDataFailure">
    /// Whether the existing World nullable adapter captures qualifying data failures; path and authority checks remain outside those catches.
    /// </param>
    /// <returns>
    /// The opened snapshot, or <see langword="null"/> for absence or a captured optional data failure.
    /// </returns>
    private CanonicalFileReadSnapshot? ReadOrdinaryFileSnapshotCore(string relativePath,
        CanonicalWriteLease? lease, out ExceptionDispatchInfo? dataFailure, bool nullableDataFailure)
    {
#if DEBUG
        var diagnosticOutcome = "fault";
        long diagnosticBytes = 0;
        try
        {
#endif
        using var t326Sync0 = this.MeasureOperation("OrdinaryRead.Sync.Total");
        dataFailure = null;
        using var t326Sync1 = this.MeasureOperation("OrdinaryRead.Sync.PrepareScope");
        using var resolveTiming = MeasureOperation("OrdinaryRead.Sync.ResolvePath");
        var expectedPath = ResolvePath(relativePath);
        resolveTiming?.Dispose();
        using var scopeTiming = MeasureOperation("OrdinaryRead.Sync.CreateScope");
        var scope = new TrustedLocalFileScope([GameSessionPath]);
        scopeTiming?.Dispose();
        t326Sync1?.Dispose();
        for (var attempt = 0; ; attempt++)
        {
#if DEBUG
            BrowserDiagnosticContent(relativePath, "ordinary-sync", "physical-attempt", "started");
#endif
            var initialValidationComplete = false;
            try
            {
                using var t326SyncAdmission = MeasureOperation("OrdinaryRead.Sync.Admission");
                if (lease != null) EnsureValidCanonicalWriteLease(lease);
                using (MeasureOperation("OrdinaryRead.Sync.Admission.CanonicalSafety"))
                    EnsureCanonicalPathStillSafe(relativePath, expectedPath);
                string admittedPath;
                using (MeasureOperation("OrdinaryRead.Sync.Admission.ValidateFile"))
                    admittedPath = scope.ValidateFile(expectedPath);
                bool exists;
                using (MeasureOperation("OrdinaryRead.Sync.Admission.FileExists"))
                    exists = File.Exists(admittedPath);
                if (!exists)
                {
#if DEBUG
                    diagnosticOutcome = "missing";
#endif
                    return null;
                }
                t326SyncAdmission?.Dispose();
                using var t326SyncOpen = MeasureOperation("OrdinaryRead.Sync.ValidateAndOpen");
                using var stream = OpenValidatedOrdinaryFile(scope, expectedPath, asynchronous: false,
                    out dataFailure, nullableDataFailure, attempt >= TransientFileAccessRetryCount);
                if (stream == null)
                {
                    // Open failures still prove neither data nor absence. Recheck
                    // authority and namespace outside the FileStream-only catch.
                    if (dataFailure != null)
                    {
                        if (lease != null) EnsureValidCanonicalWriteLease(lease);
                        CompleteOrdinaryRead(scope, relativePath, expectedPath);
                    }
#if DEBUG
                    diagnosticOutcome = dataFailure == null ? "missing" : "data-fault";
#endif
                    return null;
                }
                t326SyncOpen?.Dispose();
                initialValidationComplete = true;
                if (!nullableDataFailure)
                {
                    using var t326SyncCopy = MeasureOperation("OrdinaryRead.Sync.HookAndCopy");
                    // Preserve the default reader's original data/check/materialization order.
                    _hooks?.AfterCanonicalReadInitialValidationAsync?.Invoke(relativePath).GetAwaiter().GetResult();
                    var timestamp = File.GetLastWriteTimeUtc(stream.SafeFileHandle);
                    using var buffer = CreateOrdinaryReadBuffer(stream);
                    stream.CopyTo(buffer);
                    t326SyncCopy?.Dispose();
                    using var t326SyncComplete = MeasureOperation("OrdinaryRead.Sync.Complete");
                    CompleteOrdinaryRead(scope, relativePath, expectedPath);
                    if (lease != null) EnsureValidCanonicalWriteLease(lease);
                    t326SyncComplete?.Dispose();
                    var result = new CanonicalFileReadSnapshot(buffer.ToArray(), timestamp);
#if DEBUG
                    diagnosticOutcome = "present"; diagnosticBytes = result.Content.Length;
#endif
                    return result;
                }
                CanonicalFileReadSnapshot snapshot;
                try
                {
                    using var t326SyncNullableCopy = MeasureOperation("OrdinaryRead.Sync.NullableHookAndCopy");
                    // The existing synchronous reader has only the post-open hook.
                    _hooks?.AfterCanonicalReadInitialValidationAsync?.Invoke(relativePath).GetAwaiter().GetResult();
                    var timestamp = File.GetLastWriteTimeUtc(stream.SafeFileHandle);
                    using var buffer = CreateOrdinaryReadBuffer(stream);
                    stream.CopyTo(buffer);
                    snapshot = new(buffer.ToArray(), timestamp);
                }
                catch (Exception error) when (nullableDataFailure &&
                    (error is IOException or UnauthorizedAccessException))
                {
                    dataFailure = ExceptionDispatchInfo.Capture(error);
#if DEBUG
                    diagnosticOutcome = "data-fault";
#endif
                    return null;
                }
                using var t326SyncNullableComplete = MeasureOperation("OrdinaryRead.Sync.NullableComplete");
                CompleteOrdinaryRead(scope, relativePath, expectedPath);
                if (lease != null) EnsureValidCanonicalWriteLease(lease);
#if DEBUG
                diagnosticOutcome = "present"; diagnosticBytes = snapshot.Content.Length;
#endif
                return snapshot;
            }
            catch (Exception ex) when (!initialValidationComplete &&
                IsTransientOrdinaryReadOpenException(ex) && attempt < TransientFileAccessRetryCount)
            {
#if DEBUG
                BrowserDiagnosticContent(relativePath, "ordinary-sync", "retry", "open-fault");
#endif
                Thread.Sleep(TransientFileAccessRetryDelay);
            }
        }

#if DEBUG
        }
        catch { diagnosticOutcome = "fault"; throw; }
        finally { BrowserDiagnosticContent(relativePath, "ordinary-sync", "physical-result", diagnosticOutcome, diagnosticBytes); }
#endif
    }

    /// <summary>
    /// Opens an ordinary read stream after the shared fresh type check, including after caller boundary hooks.
    /// </summary>
    /// <param name="scope">
    /// The existing namespace grants used for fresh file validation and missing-open revalidation.
    /// </param>
    /// <param name="expectedPath">
    /// The exact absolute file name selected by the caller.
    /// </param>
    /// <param name="asynchronous">
    /// Whether to add <see cref="FileOptions.Asynchronous"/> to the unchanged sequential read options.
    /// </param>
    /// <returns>
    /// The stream owned by the caller, or <see langword="null"/> after freshly revalidated absence; other errors propagate.
    /// </returns>
    private FileStream? OpenValidatedOrdinaryFile(
        TrustedLocalFileScope scope, string expectedPath, bool asynchronous) =>
        OpenValidatedOrdinaryFile(scope, expectedPath, asynchronous, out _, false, false);

    /// <summary>
    /// Performs the shared ordinary open while preserving the original FileStream-only optional data-error catch.
    /// </summary>
    /// <param name="scope">
    /// Existing grants used before opening and again after a missing-name open failure.
    /// </param>
    /// <param name="expectedPath">
    /// The caller's exact absolute target name.
    /// </param>
    /// <param name="asynchronous">
    /// Whether the existing FileStream options include asynchronous access.
    /// </param>
    /// <param name="dataFailure">
    /// The captured qualifying open error, or <see langword="null"/> for success, absence or a propagated error.
    /// </param>
    /// <param name="nullableDataFailure">
    /// Whether qualifying FileStream errors are captured for the existing nullable reader instead of propagated.
    /// </param>
    /// <param name="retriesExhausted">
    /// Whether the caller's original retry budget permits capturing a transient open error in nullable mode.
    /// </param>
    /// <returns>
    /// The caller-owned stream, or <see langword="null"/> for revalidated absence or a captured optional data failure.
    /// </returns>
    private FileStream? OpenValidatedOrdinaryFile(TrustedLocalFileScope scope,
        string expectedPath, bool asynchronous, out ExceptionDispatchInfo? dataFailure,
        bool nullableDataFailure, bool retriesExhausted)
    {
        dataFailure = null;
        string path;
        using (MeasureOperation("OrdinaryRead.Open.ValidateFile"))
            path = scope.ValidateFile(expectedPath);
        try
        {
            using var streamTiming = MeasureOperation("OrdinaryRead.Open.FileStream");
            return new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read | FileShare.Delete,
                bufferSize: 4096, FileOptions.SequentialScan | (asynchronous ? FileOptions.Asynchronous : FileOptions.None));
        }
        catch (FileNotFoundException)
        {
            using (MeasureOperation("OrdinaryRead.Open.RevalidateMissing")) scope.ValidateFile(path);
            return null;
        }
        catch (DirectoryNotFoundException)
        {
            using (MeasureOperation("OrdinaryRead.Open.RevalidateMissing")) scope.ValidateFile(path);
            return null;
        }
        catch (Exception error) when (nullableDataFailure &&
            (error is IOException or UnauthorizedAccessException) &&
            (retriesExhausted || !IsTransientOrdinaryReadOpenException(error)))
        {
            dataFailure = ExceptionDispatchInfo.Capture(error);
            return null;
        }
    }

    private async Task<CanonicalFileReadSnapshot> ReadOpenedOrdinarySnapshotAsync(FileStream stream,
        TrustedLocalFileScope scope, string relativePath, string expectedPath, CancellationToken cancellationToken)
    {
        using var t326Opened0 = this.MeasureOperation("OrdinaryRead.OpenedSnapshot.PrepareBuffer");
        var timestamp = File.GetLastWriteTimeUtc(stream.SafeFileHandle);
        using var buffer = CreateOrdinaryReadBuffer(stream);
        t326Opened0?.Dispose();
        using var t326Opened1 = this.MeasureOperation("OrdinaryRead.OpenedSnapshot.CopyAsync");
        await stream.CopyToAsync(buffer, cancellationToken);
        t326Opened1?.Dispose();
        using var t326Opened2 = this.MeasureOperation("OrdinaryRead.OpenedSnapshot.Complete");
        CompleteOrdinaryRead(scope, relativePath, expectedPath);
        t326Opened2?.Dispose();
        return new(buffer.ToArray(), timestamp);
    }

    private static MemoryStream CreateOrdinaryReadBuffer(FileStream stream) =>
        stream.Length is > 0 and <= int.MaxValue ? new MemoryStream((int)stream.Length) : new MemoryStream();

    /// <summary>
    /// Performs the existing required-file and canonical-safety checks after an ordinary read.
    /// </summary>
    /// <param name="scope">
    /// The read's existing namespace grants, freshly checked at completion.
    /// </param>
    /// <param name="relativePath">
    /// The unchanged session-relative name used by canonical safety validation.
    /// </param>
    /// <param name="expectedPath">
    /// The exact absolute target selected before opening the stream.
    /// </param>
    private void CompleteOrdinaryRead(TrustedLocalFileScope scope, string relativePath, string expectedPath)
    {
        using (MeasureOperation("OrdinaryRead.Complete.ValidateFile"))
            scope.ValidateFile(expectedPath, allowMissing: false);
        using (MeasureOperation("OrdinaryRead.Complete.CanonicalSafety"))
            EnsureCanonicalPathStillSafe(relativePath, expectedPath);
    }
}
