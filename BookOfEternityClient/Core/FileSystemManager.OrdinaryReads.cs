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

    private bool OrdinaryFileExistsCore(string relativePath)
    {
        var expectedPath = ResolvePath(relativePath);
        EnsureCanonicalPathStillSafe(relativePath, expectedPath);
        var scope = new TrustedLocalFileScope([GameSessionPath]);
        // ValidateFile is the non-following type/permission check. File.Exists
        // supplies presence only after invalid/unreadable entries have failed.
        return File.Exists(scope.ValidateFile(expectedPath));
    }

    private async Task<CanonicalFileReadSnapshot?> ReadOrdinaryFileSnapshotCoreAsync(
        string relativePath, CancellationToken cancellationToken, CanonicalWriteLease? lease = null)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var expectedPath = ResolvePath(relativePath);
        var scope = new TrustedLocalFileScope([GameSessionPath]);
        FileStream? stream;
        for (var attempt = 0; ; attempt++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (lease != null) EnsureValidCanonicalWriteLease(lease);
            stream = null;
            try
            {
                EnsureCanonicalPathStillSafe(relativePath, expectedPath);
                if (!File.Exists(scope.ValidateFile(expectedPath))) return null;
                await InvokeBeforeCanonicalReadOpenAsync(relativePath);
                cancellationToken.ThrowIfCancellationRequested();
                if (lease != null) EnsureValidCanonicalWriteLease(lease);
                EnsureCanonicalPathStillSafe(relativePath, expectedPath);
                stream = OpenValidatedOrdinaryFile(scope, expectedPath, asynchronous: true);
                if (stream == null) return null;
                if (_hooks?.AfterCanonicalReadInitialValidationAsync != null)
                    await _hooks.AfterCanonicalReadInitialValidationAsync(relativePath);
                break;
            }
            catch (Exception ex) when (IsTransientOrdinaryReadOpenException(ex) && attempt < TransientFileAccessRetryCount)
            {
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
            var snapshot = await ReadOpenedOrdinarySnapshotAsync(stream, scope, relativePath, expectedPath, cancellationToken);
            if (lease != null) EnsureValidCanonicalWriteLease(lease);
            return snapshot;
        }
    }

    private CanonicalFileReadSnapshot? ReadOrdinaryFileSnapshotCore(
        string relativePath, CanonicalWriteLease? lease = null)
    {
        var expectedPath = ResolvePath(relativePath);
        var scope = new TrustedLocalFileScope([GameSessionPath]);
        for (var attempt = 0; ; attempt++)
        {
            var initialValidationComplete = false;
            try
            {
                if (lease != null) EnsureValidCanonicalWriteLease(lease);
                EnsureCanonicalPathStillSafe(relativePath, expectedPath);
                if (!File.Exists(scope.ValidateFile(expectedPath))) return null;
                using var stream = OpenValidatedOrdinaryFile(scope, expectedPath, asynchronous: false);
                if (stream == null) return null;
                initialValidationComplete = true;
                // The existing synchronous reader has only the post-open hook.
                _hooks?.AfterCanonicalReadInitialValidationAsync?.Invoke(relativePath).GetAwaiter().GetResult();
                var timestamp = File.GetLastWriteTimeUtc(stream.SafeFileHandle);
                using var buffer = CreateOrdinaryReadBuffer(stream);
                stream.CopyTo(buffer);
                CompleteOrdinaryRead(scope, relativePath, expectedPath);
                if (lease != null) EnsureValidCanonicalWriteLease(lease);
                return new(buffer.ToArray(), timestamp);
            }
            catch (Exception ex) when (!initialValidationComplete &&
                IsTransientOrdinaryReadOpenException(ex) && attempt < TransientFileAccessRetryCount)
            {
                Thread.Sleep(TransientFileAccessRetryDelay);
            }
        }
    }

    // Shared by ordinary readers and the leased B2 before-image reader. Every
    // actual open has a fresh common type check, including after boundary hooks.
    private static FileStream? OpenValidatedOrdinaryFile(
        TrustedLocalFileScope scope, string expectedPath, bool asynchronous)
    {
        var path = scope.ValidateFile(expectedPath);
        try
        {
            return new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read | FileShare.Delete,
                bufferSize: 4096, FileOptions.SequentialScan | (asynchronous ? FileOptions.Asynchronous : FileOptions.None));
        }
        catch (FileNotFoundException) { scope.ValidateFile(path); return null; }
        catch (DirectoryNotFoundException) { scope.ValidateFile(path); return null; }
    }

    private async Task<CanonicalFileReadSnapshot> ReadOpenedOrdinarySnapshotAsync(FileStream stream,
        TrustedLocalFileScope scope, string relativePath, string expectedPath, CancellationToken cancellationToken)
    {
        var timestamp = File.GetLastWriteTimeUtc(stream.SafeFileHandle);
        using var buffer = CreateOrdinaryReadBuffer(stream);
        await stream.CopyToAsync(buffer, cancellationToken);
        CompleteOrdinaryRead(scope, relativePath, expectedPath);
        return new(buffer.ToArray(), timestamp);
    }

    private static MemoryStream CreateOrdinaryReadBuffer(FileStream stream) =>
        stream.Length is > 0 and <= int.MaxValue ? new MemoryStream((int)stream.Length) : new MemoryStream();

    private void CompleteOrdinaryRead(TrustedLocalFileScope scope, string relativePath, string expectedPath)
    {
        scope.ValidateFile(expectedPath, allowMissing: false);
        EnsureCanonicalPathStillSafe(relativePath, expectedPath);
    }
}
