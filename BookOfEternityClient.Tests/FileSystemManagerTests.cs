using BookOfEternityClient.Core;
using BookOfEternityClient.Services;
using BookOfEternityClient.WebUi;
using Microsoft.Extensions.Logging.Abstractions;
using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text.Json;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed class FileSystemManagerTests : IDisposable
{
    private readonly string _rootPath;
    private readonly FileSystemManager _fs;

    public FileSystemManagerTests()
    {
        _rootPath = Path.Combine(Path.GetTempPath(), "boe-filesystem-manager-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_rootPath);
        _fs = new FileSystemManager(_rootPath, NullLogger<FileSystemManager>.Instance);
        _fs.EnsureDirectoryStructure();
    }

    [Fact]
    public async Task ReadFileAsync_WhenFileIsBrieflyLocked_RetriesUntilContentIsReadable()
    {
        await _fs.WriteFileAtomicAsync("input/turn_request.json", "stable content");
        var fullPath = _fs.ResolvePath("input/turn_request.json");
        using var lockStream = new FileStream(fullPath, FileMode.Open, FileAccess.Read, FileShare.None);

        var readTask = _fs.ReadFileAsync("input/turn_request.json");
        await Task.Delay(100);
        await lockStream.DisposeAsync();

        var content = await readTask;

        Assert.Equal("stable content", content);
    }

    [Fact]
    public async Task WriteFileAtomicAsync_WhenTargetIsBrieflyLocked_RetriesUntilReplacementSucceeds()
    {
        await _fs.WriteFileAtomicAsync("input/turn_request.json", "old content");
        var fullPath = _fs.ResolvePath("input/turn_request.json");
        using var lockStream = new FileStream(fullPath, FileMode.Open, FileAccess.Read, FileShare.Read);

        var writeTask = _fs.WriteFileAtomicAsync("input/turn_request.json", "new content");
        await Task.Delay(100);
        await lockStream.DisposeAsync();

        await writeTask;

        Assert.Equal("new content", await _fs.ReadFileAsync("input/turn_request.json"));
    }

    [Fact]
    public async Task WriteFileAtomicAsync_PreservesUtf8BomContract()
    {
        await _fs.WriteFileAtomicAsync("input/turn_request.json", "content");

        var bytes = await File.ReadAllBytesAsync(_fs.ResolvePath("input/turn_request.json"));
        var preamble = System.Text.Encoding.UTF8.GetPreamble();

        Assert.Equal(preamble, bytes.Take(preamble.Length).ToArray());
    }

    [Fact]
    public async Task ClearGameStateAsync_DoesNotTraverseDirectoryJunction()
    {
        if (!OperatingSystem.IsWindows())
            return;

        var outsideRoot = Path.Combine(
            Path.GetTempPath(),
            "boe-filesystem-manager-outside-" + Guid.NewGuid().ToString("N"));
        var outsideFile = Path.Combine(outsideRoot, "external.json");
        var junctionPath = _fs.ResolvePath("game_state/world/external-link");
        Directory.CreateDirectory(outsideRoot);
        await File.WriteAllTextAsync(outsideFile, "{\"mustRemain\":true}");
        try
        {
            CreateDirectoryJunction(junctionPath, outsideRoot);

            await _fs.ClearGameStateAsync();

            Assert.True(File.Exists(outsideFile));
            Assert.Equal("{\"mustRemain\":true}", await File.ReadAllTextAsync(outsideFile));
        }
        finally
        {
            if (Directory.Exists(junctionPath))
                Directory.Delete(junctionPath, recursive: false);
            if (Directory.Exists(outsideRoot))
                Directory.Delete(outsideRoot, recursive: true);
        }
    }

    [Fact]
    public async Task ClearGameStateAsync_RemovesLocalUiLockNamespaceDirectory()
    {
        var lockNode = _fs.ResolvePath(LocalUiSessionLockService.LockPath);
        Directory.CreateDirectory(Path.Combine(lockNode, "nested"));
        await File.WriteAllTextAsync(
            Path.Combine(lockNode, "nested", "lock.json"),
            "{\"crafted\":true}");

        await _fs.ClearGameStateAsync();

        Assert.False(File.Exists(lockNode));
        Assert.False(Directory.Exists(lockNode));
    }

    [Fact]
    public async Task ClearGameStateAsync_RemovesLocalUiLockJunctionWithoutTraversingTarget()
    {
        if (!OperatingSystem.IsWindows())
            return;

        var outsideRoot = Path.Combine(
            Path.GetTempPath(),
            "boe-local-ui-lock-outside-" + Guid.NewGuid().ToString("N"));
        var outsideFile = Path.Combine(outsideRoot, "sentinel.json");
        var lockNode = _fs.ResolvePath(LocalUiSessionLockService.LockPath);
        Directory.CreateDirectory(outsideRoot);
        await File.WriteAllTextAsync(outsideFile, "{\"mustRemain\":true}");
        try
        {
            CreateDirectoryJunction(lockNode, outsideRoot);

            await _fs.ClearGameStateAsync();

            Assert.False(Directory.Exists(lockNode));
            Assert.True(File.Exists(outsideFile));
            Assert.Equal(
                "{\"mustRemain\":true}",
                await File.ReadAllTextAsync(outsideFile));
        }
        finally
        {
            if (Directory.Exists(lockNode))
                Directory.Delete(lockNode, recursive: false);
            if (Directory.Exists(outsideRoot))
                Directory.Delete(outsideRoot, recursive: true);
        }
    }

    [Fact]
    public async Task ClearGameStateAsync_RemovesDanglingLocalUiLockNode()
    {
        if (!OperatingSystem.IsWindows())
            return;

        var lockNode = _fs.ResolvePath(LocalUiSessionLockService.LockPath);
        var missingTarget = Path.Combine(
            _rootPath,
            "missing-local-ui-lock-target");
        Directory.CreateDirectory(missingTarget);
        if (!TryCreateDirectoryLink(lockNode, missingTarget))
        {
            Directory.Delete(missingTarget);
            return;
        }
        Directory.Delete(missingTarget);
        Assert.True(
            File.GetAttributes(lockNode).HasFlag(FileAttributes.ReparsePoint));

        await _fs.ClearGameStateAsync();

        Assert.Throws<FileNotFoundException>(
            () => File.GetAttributes(lockNode));
    }

    [Fact]
    public async Task WriteFileAtomicBytesAsync_PreservesExactBytes()
    {
        byte[] expected = [0xEF, 0xBB, 0xBF, 0x00, 0xFF, 0x41];

        await _fs.WriteFileAtomicBytesAsync("input/turn_request.json", expected);

        var actual = await File.ReadAllBytesAsync(_fs.ResolvePath("input/turn_request.json"));
        Assert.Equal(expected, actual);
    }

    [Fact]
    public async Task WriteFileAtomicAsync_RelativeTraversalFailsClosed()
    {
        var escapedPath = Path.Combine(_rootPath, "escaped.json");

        await Assert.ThrowsAsync<InvalidDataException>(
            () => _fs.WriteFileAtomicAsync("../escaped.json", """{ "escaped": true }"""));

        Assert.False(File.Exists(escapedPath));
    }

    [Fact]
    public async Task CanonicalWrite_GameSessionJunctionAliasFailsClosed()
    {
        if (!OperatingSystem.IsWindows())
            return;

        var aliasRoot = Path.Combine(
            Path.GetTempPath(),
            "boe-filesystem-manager-alias-" + Guid.NewGuid().ToString("N"));
        var aliasSession = Path.Combine(aliasRoot, "game_session");
        Directory.CreateDirectory(aliasRoot);
        try
        {
            CreateDirectoryJunction(aliasSession, _fs.GameSessionPath);
            var aliasFileSystem = new FileSystemManager(
                aliasRoot,
                NullLogger<FileSystemManager>.Instance);

            await Assert.ThrowsAsync<InvalidDataException>(
                () => aliasFileSystem.WriteFileAtomicAsync(
                    "game_state/meta/alias-write.json",
                    """{ "session": "stale-alias" }"""));

            Assert.False(_fs.FileExists("game_state/meta/alias-write.json"));
        }
        finally
        {
            if (Directory.Exists(aliasSession))
                Directory.Delete(aliasSession, recursive: false);
            if (Directory.Exists(aliasRoot))
                Directory.Delete(aliasRoot, recursive: true);
        }
    }

    [Fact]
    public async Task WriteFileAtomicBytesAsync_ParentReplacedByJunctionBeforeCommitFailsClosed()
    {
        if (!OperatingSystem.IsWindows())
            return;

        const string relativePath = "game_state/meta/race-target.bin";
        var raceRoot = Path.Combine(
            Path.GetTempPath(),
            "boe-filesystem-mutation-race-" + Guid.NewGuid().ToString("N"));
        var outsideRoot = Path.Combine(
            Path.GetTempPath(),
            "boe-filesystem-mutation-outside-" + Guid.NewGuid().ToString("N"));
        var canonicalMeta = Path.Combine(raceRoot, "game_session", "game_state", "meta");
        var displacedMeta = canonicalMeta + "-before-race";
        var outsideTarget = Path.Combine(outsideRoot, "race-target.bin");
        var armed = false;
        var swapped = false;
        var hooks = new FileSystemManagerHooks
        {
            BeforeCanonicalMutationAsync = mutationPath =>
            {
                if (!armed ||
                    swapped ||
                    !string.Equals(mutationPath, relativePath, StringComparison.OrdinalIgnoreCase))
                {
                    return Task.CompletedTask;
                }

                Directory.Move(canonicalMeta, displacedMeta);
                CreateDirectoryJunction(canonicalMeta, outsideRoot);
                swapped = true;
                return Task.CompletedTask;
            }
        };
        Directory.CreateDirectory(raceRoot);
        Directory.CreateDirectory(outsideRoot);
        await File.WriteAllBytesAsync(outsideTarget, [0xA1]);

        try
        {
            var fs = new FileSystemManager(
                raceRoot,
                NullLogger<FileSystemManager>.Instance,
                PhysicalLoadTransactionOperations.Instance,
                hooks);
            fs.EnsureDirectoryStructure();
            await fs.WriteFileAtomicBytesAsync(relativePath, [0x11]);
            armed = true;

            await Assert.ThrowsAsync<InvalidDataException>(
                () => fs.WriteFileAtomicBytesAsync(relativePath, [0x22]));

            Assert.True(swapped);
            Assert.Equal(new byte[] { 0xA1 }, await File.ReadAllBytesAsync(outsideTarget));
        }
        finally
        {
            if (Directory.Exists(canonicalMeta) &&
                (File.GetAttributes(canonicalMeta) & FileAttributes.ReparsePoint) != 0)
            {
                Directory.Delete(canonicalMeta, recursive: false);
            }

            if (Directory.Exists(displacedMeta) && !Directory.Exists(canonicalMeta))
                Directory.Move(displacedMeta, canonicalMeta);
            if (Directory.Exists(raceRoot))
                Directory.Delete(raceRoot, recursive: true);
            if (Directory.Exists(outsideRoot))
                Directory.Delete(outsideRoot, recursive: true);
        }
    }

    [Fact]
    public async Task ReadFileBytesAsync_ParentReplacedByJunctionBeforeOpenFailsClosed()
    {
        if (!OperatingSystem.IsWindows())
            return;

        const string relativePath = "game_state/meta/read-race-target.bin";
        var raceRoot = Path.Combine(
            Path.GetTempPath(),
            "boe-filesystem-read-race-" + Guid.NewGuid().ToString("N"));
        var outsideRoot = Path.Combine(
            Path.GetTempPath(),
            "boe-filesystem-read-outside-" + Guid.NewGuid().ToString("N"));
        var canonicalMeta = Path.Combine(raceRoot, "game_session", "game_state", "meta");
        var displacedMeta = canonicalMeta + "-before-race";
        var outsideTarget = Path.Combine(outsideRoot, "read-race-target.bin");
        var armed = false;
        var swapped = false;
        var hooks = new FileSystemManagerHooks
        {
            BeforeCanonicalReadOpenAsync = readPath =>
            {
                if (!armed ||
                    swapped ||
                    !string.Equals(readPath, relativePath, StringComparison.OrdinalIgnoreCase))
                {
                    return Task.CompletedTask;
                }

                Directory.Move(canonicalMeta, displacedMeta);
                CreateDirectoryJunction(canonicalMeta, outsideRoot);
                swapped = true;
                return Task.CompletedTask;
            }
        };
        Directory.CreateDirectory(raceRoot);
        Directory.CreateDirectory(outsideRoot);
        await File.WriteAllBytesAsync(outsideTarget, [0xD4]);

        try
        {
            var fs = new FileSystemManager(
                raceRoot,
                NullLogger<FileSystemManager>.Instance,
                PhysicalLoadTransactionOperations.Instance,
                hooks);
            fs.EnsureDirectoryStructure();
            await fs.WriteFileAtomicBytesAsync(relativePath, [0x44]);
            armed = true;

            await Assert.ThrowsAsync<InvalidDataException>(
                () => fs.ReadFileBytesAsync(relativePath));

            Assert.True(swapped);
        }
        finally
        {
            if (Directory.Exists(canonicalMeta) &&
                (File.GetAttributes(canonicalMeta) & FileAttributes.ReparsePoint) != 0)
            {
                Directory.Delete(canonicalMeta, recursive: false);
            }

            if (Directory.Exists(displacedMeta) && !Directory.Exists(canonicalMeta))
                Directory.Move(displacedMeta, canonicalMeta);
            if (Directory.Exists(raceRoot))
                Directory.Delete(raceRoot, recursive: true);
            if (Directory.Exists(outsideRoot))
                Directory.Delete(outsideRoot, recursive: true);
        }
    }

    [Theory]
    [InlineData("append")]
    [InlineData("compare-exchange")]
    [InlineData("create-backup")]
    [InlineData("restore-backup")]
    public async Task CanonicalReadModifyWriteOperation_ParentReplacedByJunctionFailsClosed(
        string operation)
    {
        if (!OperatingSystem.IsWindows())
            return;

        const string relativePath = "game_state/meta/read-modify-write.bin";
        const string backupRelativePath = "game_state/meta/read-modify-write.bin.test-backup";
        var raceRoot = Path.Combine(
            Path.GetTempPath(),
            "boe-filesystem-rmw-race-" + Guid.NewGuid().ToString("N"));
        var outsideRoot = Path.Combine(
            Path.GetTempPath(),
            "boe-filesystem-rmw-outside-" + Guid.NewGuid().ToString("N"));
        var canonicalMeta = Path.Combine(raceRoot, "game_session", "game_state", "meta");
        var displacedMeta = canonicalMeta + "-before-race";
        var readPath = string.Equals(operation, "restore-backup", StringComparison.Ordinal)
            ? backupRelativePath
            : relativePath;
        var armed = false;
        var swapped = false;
        var hooks = new FileSystemManagerHooks
        {
            BeforeCanonicalReadOpenAsync = candidatePath =>
            {
                if (!armed ||
                    swapped ||
                    !string.Equals(candidatePath, readPath, StringComparison.OrdinalIgnoreCase))
                {
                    return Task.CompletedTask;
                }

                Directory.Move(canonicalMeta, displacedMeta);
                CreateDirectoryJunction(canonicalMeta, outsideRoot);
                swapped = true;
                return Task.CompletedTask;
            }
        };
        Directory.CreateDirectory(raceRoot);
        Directory.CreateDirectory(outsideRoot);
        await File.WriteAllBytesAsync(
            Path.Combine(outsideRoot, Path.GetFileName(readPath)),
            [0xE5]);

        try
        {
            var fs = new FileSystemManager(
                raceRoot,
                NullLogger<FileSystemManager>.Instance,
                PhysicalLoadTransactionOperations.Instance,
                hooks);
            fs.EnsureDirectoryStructure();
            await fs.WriteFileAtomicBytesAsync(relativePath, [0x55]);
            await fs.WriteFileAtomicBytesAsync(backupRelativePath, [0x66]);
            var backupFullPath = fs.ResolvePath(backupRelativePath);
            armed = true;

            Task operationTask = operation switch
            {
                "append" => fs.AppendFileAtomicAsync(relativePath, "x"),
                "compare-exchange" => fs.CompareExchangeFileBytesAsync(
                    relativePath,
                    [0x55],
                    [0x77]),
                "create-backup" => Task.Run(() => fs.CreateBackup(relativePath)),
                "restore-backup" => Task.Run(() => fs.RestoreBackup(
                    backupFullPath,
                    relativePath)),
                _ => throw new ArgumentOutOfRangeException(nameof(operation))
            };

            await Assert.ThrowsAsync<InvalidDataException>(() => operationTask);
            Assert.True(swapped);
        }
        finally
        {
            if (Directory.Exists(canonicalMeta) &&
                (File.GetAttributes(canonicalMeta) & FileAttributes.ReparsePoint) != 0)
            {
                Directory.Delete(canonicalMeta, recursive: false);
            }

            if (Directory.Exists(displacedMeta) && !Directory.Exists(canonicalMeta))
                Directory.Move(displacedMeta, canonicalMeta);
            if (Directory.Exists(raceRoot))
                Directory.Delete(raceRoot, recursive: true);
            if (Directory.Exists(outsideRoot))
                Directory.Delete(outsideRoot, recursive: true);
        }
    }

    [Fact]
    public async Task DeleteFile_ParentReplacedByJunctionBeforeMutationFailsClosed()
    {
        if (!OperatingSystem.IsWindows())
            return;

        const string relativePath = "game_state/meta/delete-target.bin";
        var raceRoot = Path.Combine(
            Path.GetTempPath(),
            "boe-filesystem-delete-race-" + Guid.NewGuid().ToString("N"));
        var outsideRoot = Path.Combine(
            Path.GetTempPath(),
            "boe-filesystem-delete-outside-" + Guid.NewGuid().ToString("N"));
        var canonicalMeta = Path.Combine(raceRoot, "game_session", "game_state", "meta");
        var displacedMeta = canonicalMeta + "-before-race";
        var outsideTarget = Path.Combine(outsideRoot, "delete-target.bin");
        var armed = false;
        var swapped = false;
        var hooks = new FileSystemManagerHooks
        {
            BeforeCanonicalMutationAsync = mutationPath =>
            {
                if (!armed ||
                    swapped ||
                    !string.Equals(mutationPath, relativePath, StringComparison.OrdinalIgnoreCase))
                {
                    return Task.CompletedTask;
                }

                Directory.Move(canonicalMeta, displacedMeta);
                CreateDirectoryJunction(canonicalMeta, outsideRoot);
                swapped = true;
                return Task.CompletedTask;
            }
        };
        Directory.CreateDirectory(raceRoot);
        Directory.CreateDirectory(outsideRoot);
        File.WriteAllBytes(outsideTarget, [0xB2]);

        try
        {
            var fs = new FileSystemManager(
                raceRoot,
                NullLogger<FileSystemManager>.Instance,
                PhysicalLoadTransactionOperations.Instance,
                hooks);
            fs.EnsureDirectoryStructure();
            await fs.WriteFileAtomicBytesAsync(relativePath, [0x33]);
            armed = true;

            Assert.Throws<InvalidDataException>(() => fs.DeleteFile(relativePath));

            Assert.True(swapped);
            Assert.Equal(new byte[] { 0xB2 }, File.ReadAllBytes(outsideTarget));
        }
        finally
        {
            if (Directory.Exists(canonicalMeta) &&
                (File.GetAttributes(canonicalMeta) & FileAttributes.ReparsePoint) != 0)
            {
                Directory.Delete(canonicalMeta, recursive: false);
            }

            if (Directory.Exists(displacedMeta) && !Directory.Exists(canonicalMeta))
                Directory.Move(displacedMeta, canonicalMeta);
            if (Directory.Exists(raceRoot))
                Directory.Delete(raceRoot, recursive: true);
            if (Directory.Exists(outsideRoot))
                Directory.Delete(outsideRoot, recursive: true);
        }
    }

    [Fact]
    public async Task CompareExchangeFileBytesAsync_RejectsStaleExpectedBytesWithoutWriting()
    {
        byte[] baseline = [0x01, 0x02, 0x03];
        byte[] concurrent = [0x04, 0x05, 0x06];
        byte[] proposed = [0x07, 0x08, 0x09];
        await _fs.WriteFileAtomicBytesAsync("game_state/world/weather.json", baseline);
        await _fs.WriteFileAtomicBytesAsync("game_state/world/weather.json", concurrent);

        var result = await _fs.CompareExchangeFileBytesAsync(
            "game_state/world/weather.json",
            baseline,
            proposed);

        Assert.Equal(CanonicalFileMutationResult.Conflict, result);
        Assert.Equal(concurrent, await _fs.ReadFileBytesAsync("game_state/world/weather.json"));
    }

    [Fact]
    public async Task CompareExchangeFileBytesAsync_ConcurrentReplacementsAllowExactlyOneWinner()
    {
        byte[] baseline = [0x10];
        byte[] first = [0x20];
        byte[] second = [0x30];
        await _fs.WriteFileAtomicBytesAsync("game_state/world/weather.json", baseline);

        var results = await Task.WhenAll(
            _fs.CompareExchangeFileBytesAsync("game_state/world/weather.json", baseline, first),
            _fs.CompareExchangeFileBytesAsync("game_state/world/weather.json", baseline, second));

        Assert.Equal(1, results.Count(result => result == CanonicalFileMutationResult.Applied));
        Assert.Equal(1, results.Count(result => result == CanonicalFileMutationResult.Conflict));
        var actual = await _fs.ReadFileBytesAsync("game_state/world/weather.json");
        Assert.NotNull(actual);
        Assert.True(actual.SequenceEqual(first) || actual.SequenceEqual(second));
    }

    [Fact]
    public async Task TryAcquireSessionLifecycleLeaseAsync_StaleGenerationReturnsNull()
    {
        string staleGeneration;
        await using (var canonicalLease = await _fs.AcquireCanonicalWriteLeaseAsync())
            staleGeneration = _fs.GetOrCreateSessionGeneration(canonicalLease);
        await using (var replacementLifecycleLease = await _fs.AcquireSessionLifecycleLeaseAsync())
        await using (var replacementLease =
                     await _fs.AcquireSessionReplacementWriteLeaseAsync(replacementLifecycleLease))
            _fs.RotateSessionGeneration(replacementLease);

        var lifecycleLease = await _fs.TryAcquireSessionLifecycleLeaseAsync(staleGeneration);

        Assert.Null(lifecycleLease);
    }

    [Fact]
    public async Task SessionMutationLease_CannotRotateSessionGeneration()
    {
        await using var canonicalLease = await _fs.AcquireCanonicalWriteLeaseAsync();

        var exception = Assert.Throws<InvalidOperationException>(
            () => _fs.RotateSessionGeneration(canonicalLease));

        Assert.Contains("replacement", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task SessionReplacementLease_RequiresActiveLifecycleAuthority()
    {
        var lifecycleLease = await _fs.AcquireSessionLifecycleLeaseAsync();
        await lifecycleLease.DisposeAsync();

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => _fs.AcquireSessionReplacementWriteLeaseAsync(lifecycleLease));
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => _fs.AcquireCanonicalWriteLeaseAsync(CanonicalWritePurpose.SessionReplacement));
    }

    [Fact]
    public async Task SessionReplacementLease_CanRotateGenerationWhileLifecycleAuthorityIsActive()
    {
        string previousGeneration;
        await using (var mutationLease = await _fs.AcquireCanonicalWriteLeaseAsync())
            previousGeneration = _fs.GetOrCreateSessionGeneration(mutationLease);

        await using var lifecycleLease = await _fs.AcquireSessionLifecycleLeaseAsync();
        await using var replacementLease =
            await _fs.AcquireSessionReplacementWriteLeaseAsync(lifecycleLease);

        var replacementGeneration = _fs.RotateSessionGeneration(replacementLease);

        Assert.NotEqual(previousGeneration, replacementGeneration);
        Assert.True(_fs.IsCurrentSessionGeneration(replacementLease, replacementGeneration));
    }

    [Fact]
    public async Task ClearGameStateAsync_WaitsForActiveSessionLifecycleLease()
    {
        var lifecycleContended = new TaskCompletionSource<bool>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var competingFs = new FileSystemManager(
            _rootPath,
            NullLogger<FileSystemManager>.Instance,
            PhysicalLoadTransactionOperations.Instance,
            new FileSystemManagerHooks
            {
                SessionLifecycleLockContendedAsync = () =>
                {
                    lifecycleContended.TrySetResult(true);
                    return Task.CompletedTask;
                }
            });
        competingFs.EnsureDirectoryStructure();

        var lifecycleLease = await _fs.AcquireSessionLifecycleLeaseAsync();
        Task clearTask;
        try
        {
            clearTask = competingFs.ClearGameStateAsync();
            await lifecycleContended.Task.WaitAsync(TimeSpan.FromSeconds(5));
            Assert.False(clearTask.IsCompleted);
        }
        finally
        {
            await lifecycleLease.DisposeAsync();
        }

        await clearTask;
    }

    [Fact]
    public async Task CompareExchangeFileBytesAsync_RollbackDoesNotOverwriteNewerBytes()
    {
        byte[] baseline = [0x41];
        byte[] worker = [0x42];
        byte[] newer = [0x43];
        await _fs.WriteFileAtomicBytesAsync("game_state/world/weather.json", worker);
        await _fs.WriteFileAtomicBytesAsync("game_state/world/weather.json", newer);

        var result = await _fs.CompareExchangeFileBytesAsync(
            "game_state/world/weather.json",
            worker,
            baseline);

        Assert.Equal(CanonicalFileMutationResult.Conflict, result);
        Assert.Equal(newer, await _fs.ReadFileBytesAsync("game_state/world/weather.json"));
    }

    [Fact]
    public async Task DeleteFile_WhenTargetIsBrieflyLocked_RetriesUntilDeleteSucceeds()
    {
        await _fs.WriteFileAtomicAsync("input/turn_request.json", "content");
        var fullPath = _fs.ResolvePath("input/turn_request.json");
        await using var lockStream = new FileStream(fullPath, FileMode.Open, FileAccess.Read, FileShare.Read);

        var deleteTask = Task.Run(() => _fs.DeleteFile("input/turn_request.json"));
        await Task.Delay(100);
        await lockStream.DisposeAsync();

        await deleteTask;

        Assert.False(File.Exists(fullPath));
    }

    [Fact]
    public async Task CreateBackup_WaitsForCanonicalWriteLease()
    {
        const string path = "game_state/meta/soul_state.json";
        await _fs.WriteFileAtomicAsync(path, "before");
        Task<string?> backupTask;

        await using (await _fs.AcquireCanonicalWriteLeaseAsync())
        {
            backupTask = Task.Run(() => _fs.CreateBackup(path));
            await Task.Delay(150);
            Assert.False(backupTask.IsCompleted);
        }

        var backupPath = await backupTask;
        Assert.NotNull(backupPath);
        Assert.True(File.Exists(backupPath));
    }

    [Fact]
    public async Task RestoreBackup_WaitsForCanonicalWriteLease()
    {
        const string path = "game_state/meta/soul_state.json";
        await _fs.WriteFileAtomicAsync(path, "current");
        var backupPath = _fs.ResolvePath(path) + ".test-backup";
        await File.WriteAllTextAsync(backupPath, "restored");
        Task restoreTask;

        await using (await _fs.AcquireCanonicalWriteLeaseAsync())
        {
            restoreTask = Task.Run(() => _fs.RestoreBackup(backupPath, path));
            await Task.Delay(150);
            Assert.False(restoreTask.IsCompleted);
            Assert.Equal("current", await _fs.ReadFileAsync(path));
        }

        await restoreTask;
        Assert.Equal("restored", await _fs.ReadFileAsync(path));
    }

    [Fact]
    public async Task CleanupBackup_WaitsForCanonicalWriteLease()
    {
        const string path = "game_state/meta/soul_state.json";
        await _fs.WriteFileAtomicAsync(path, "current");
        var backupPath = _fs.ResolvePath(path) + ".test-backup";
        await File.WriteAllTextAsync(backupPath, "backup");
        Task cleanupTask;

        await using (await _fs.AcquireCanonicalWriteLeaseAsync())
        {
            cleanupTask = Task.Run(() => _fs.CleanupBackup(backupPath));
            await Task.Delay(150);
            Assert.False(cleanupTask.IsCompleted);
            Assert.True(File.Exists(backupPath));
        }

        await cleanupTask;
        Assert.False(File.Exists(backupPath));
    }

    [Fact]
    public async Task ClearGameState_WaitsForCanonicalWriteLease()
    {
        const string path = "game_state/meta/soul_state.json";
        await _fs.WriteFileAtomicAsync(path, "state");
        Task clearTask;

        await using (await _fs.AcquireCanonicalWriteLeaseAsync())
        {
            clearTask = Task.Run(_fs.ClearGameState);
            await Task.Delay(150);
            Assert.False(clearTask.IsCompleted);
            Assert.True(_fs.FileExists(path));
        }

        await clearTask;
        Assert.False(_fs.FileExists(path));
    }

    [Fact]
    public async Task ClearCurrentWorldLore_WaitsForCanonicalWriteLease()
    {
        const string path = "lore/current_world/setting.md";
        await _fs.WriteFileAtomicAsync(path, "setting");
        Task clearTask;

        await using (await _fs.AcquireCanonicalWriteLeaseAsync())
        {
            clearTask = Task.Run(_fs.ClearCurrentWorldLore);
            await Task.Delay(150);
            Assert.False(clearTask.IsCompleted);
            Assert.True(_fs.FileExists(path));
        }

        await clearTask;
        Assert.False(_fs.FileExists(path));
    }

    [Fact]
    public async Task ClearGameState_PreservesGmBridgeRuntimeStatus()
    {
        await _fs.WriteFileAtomicAsync("game_state/control/gm_bridge_status.json", """{"ready":true}""");
        await _fs.WriteFileAtomicAsync("game_state/meta/soul_state.json", """{"currentRealm":"Chaos Sea"}""");

        _fs.ClearGameState();

        Assert.Equal("""{"ready":true}""", await _fs.ReadFileAsync("game_state/control/gm_bridge_status.json"));
        Assert.False(_fs.FileExists("game_state/meta/soul_state.json"));
    }

    [Fact]
    public async Task ClearGameState_PreservesGmContextPackJsonArtifacts()
    {
        await _fs.WriteFileAtomicAsync("game_state/control/gm_context_pack/context_pack_manifest.json", """{"schemaVersion":1}""");
        await _fs.WriteFileAtomicAsync("game_state/control/gm_context_pack/Templates/PROGRESSION_REPORT_TEMPLATE.json", """{"progressionProcessingReport":{}}""");
        await _fs.WriteFileAtomicAsync("game_state/control/gm_context_pack/Templates/AFTERLIFE_TEMPO_ADVANTAGE_TEMPLATE.json", """{"tempoAdvantage":{}}""");
        await _fs.WriteFileAtomicAsync("game_state/control/gm_context_pack/Probes/GM_SAFE_PROBES.json", """{"probes":[]}""");
        await _fs.WriteFileAtomicAsync("game_state/control/gm_context_pack/Rubrics/GM_LIVE_TEST_RUBRIC.json", """{"dimensions":[]}""");
        await _fs.WriteFileAtomicAsync("game_state/meta/soul_state.json", """{"currentRealm":"Chaos Sea"}""");

        _fs.ClearGameState();

        Assert.True(_fs.FileExists("game_state/control/gm_context_pack/context_pack_manifest.json"));
        Assert.True(_fs.FileExists("game_state/control/gm_context_pack/Templates/PROGRESSION_REPORT_TEMPLATE.json"));
        Assert.True(_fs.FileExists("game_state/control/gm_context_pack/Templates/AFTERLIFE_TEMPO_ADVANTAGE_TEMPLATE.json"));
        Assert.True(_fs.FileExists("game_state/control/gm_context_pack/Probes/GM_SAFE_PROBES.json"));
        Assert.True(_fs.FileExists("game_state/control/gm_context_pack/Rubrics/GM_LIVE_TEST_RUBRIC.json"));
        Assert.False(_fs.FileExists("game_state/meta/soul_state.json"));
    }

    [Fact]
    public async Task EnsureDirectoryStructure_RecoversInterruptedLoadBeforeCreatingEmptySession()
    {
        const string markerPath = "game_state/world/recovery_marker.json";
        await _fs.WriteFileAtomicAsync(markerPath, "{\"state\":\"last-valid\"}");

        var transactionId = Guid.NewGuid().ToString("N");
        var transactionRoot = Path.Combine(
            _rootPath,
            ".boe_runtime",
            "load-transactions",
            transactionId);
        var backupSessionPath = Path.Combine(transactionRoot, "backup", "game_session");
        Directory.CreateDirectory(Path.GetDirectoryName(backupSessionPath)!);
        Directory.Move(_fs.GameSessionPath, backupSessionPath);

        var journalPath = Path.Combine(
            _rootPath,
            ".boe_runtime",
            "load-transactions",
            "active.json");
        Directory.CreateDirectory(Path.GetDirectoryName(journalPath)!);
        await File.WriteAllTextAsync(
            journalPath,
            JsonSerializer.Serialize(new
            {
                schemaVersion = 1,
                transactionId,
                committed = false
            }));

        _fs.EnsureDirectoryStructure();

        Assert.Equal("{\"state\":\"last-valid\"}", await _fs.ReadFileAsync(markerPath));
        Assert.False(File.Exists(journalPath));
        Assert.False(Directory.Exists(transactionRoot));
    }

    [Fact]
    public async Task CanonicalWriter_RecoversInterruptedWorkerApplyBeforeWriting()
    {
        const string firstPath = "game_state/world/weather.json";
        const string secondPath = "game_state/world/current_location.json";
        byte[] firstBaseline = [0x10, 0x11];
        byte[] firstApplied = [0x20, 0x21];
        byte[] secondBaseline = [0x30, 0x31];
        byte[] secondApplied = [0x40, 0x41];
        await _fs.WriteFileAtomicBytesAsync(firstPath, firstBaseline);
        await _fs.WriteFileAtomicBytesAsync(secondPath, secondBaseline);

        var transactionId = Guid.NewGuid().ToString("N");
        var transactionRoot = Path.Combine(
            _rootPath,
            ".boe_runtime",
            "worker-apply-transactions",
            transactionId);
        var beforeRoot = Path.Combine(transactionRoot, "before");
        Directory.CreateDirectory(beforeRoot);
        await File.WriteAllBytesAsync(Path.Combine(beforeRoot, "0000.bin"), firstBaseline);
        await File.WriteAllBytesAsync(Path.Combine(beforeRoot, "0001.bin"), secondBaseline);
        await File.WriteAllTextAsync(
            Path.Combine(transactionRoot, "manifest.json"),
            JsonSerializer.Serialize(new
            {
                schemaVersion = 1,
                transactionId,
                entries = new[]
                {
                    new
                    {
                        path = firstPath,
                        baselineExists = true,
                        beforeImage = "before/0000.bin",
                        beforeSha256 = Sha256(firstBaseline),
                        appliedSha256 = Sha256(firstApplied)
                    },
                    new
                    {
                        path = secondPath,
                        baselineExists = true,
                        beforeImage = "before/0001.bin",
                        beforeSha256 = Sha256(secondBaseline),
                        appliedSha256 = Sha256(secondApplied)
                    }
                }
            }));
        var activeJournalPath = Path.Combine(
            _rootPath,
            ".boe_runtime",
            "worker-apply-transactions",
            "active.json");
        await File.WriteAllTextAsync(
            activeJournalPath,
            JsonSerializer.Serialize(new
            {
                schemaVersion = 1,
                transactionId,
                committed = false
            }));

        await File.WriteAllBytesAsync(_fs.ResolvePath(firstPath), firstApplied);

        await _fs.WriteFileAtomicBytesAsync("game_state/world/after_recovery.json", [0x55]);

        Assert.Equal(firstBaseline, await _fs.ReadFileBytesAsync(firstPath));
        Assert.Equal(secondBaseline, await _fs.ReadFileBytesAsync(secondPath));
        Assert.Equal([0x55], await _fs.ReadFileBytesAsync("game_state/world/after_recovery.json"));
        Assert.False(File.Exists(activeJournalPath));
        Assert.False(Directory.Exists(transactionRoot));
    }

    [Fact]
    public async Task WorkerRecovery_BeforeImageLinkAddedAfterInitialValidationFailsAndRetainsEvidence()
    {
        if (!OperatingSystem.IsWindows())
            return;

        const string trackedPath =
            "game_state/world/worker-before-image-completion.json";
        const string triggerPath =
            "game_state/world/worker-before-image-trigger.json";
        byte[] baseline = [0x11, 0x22];
        byte[] applied = [0x33, 0x44];
        await _fs.WriteFileAtomicBytesAsync(trackedPath, baseline);

        var transactionId = Guid.NewGuid().ToString("N");
        var transactionRoot = Path.Combine(
            _rootPath,
            ".boe_runtime",
            "worker-apply-transactions",
            transactionId);
        var beforeRoot = Path.Combine(transactionRoot, "before");
        var beforePath = Path.Combine(beforeRoot, "0000.bin");
        var aliasPath = Path.Combine(
            _rootPath,
            "worker-before-image-alias.bin");
        Directory.CreateDirectory(beforeRoot);
        await File.WriteAllBytesAsync(beforePath, baseline);
        await File.WriteAllTextAsync(
            Path.Combine(transactionRoot, "manifest.json"),
            JsonSerializer.Serialize(new
            {
                schemaVersion = 1,
                transactionId,
                entries = new[]
                {
                    new
                    {
                        path = trackedPath,
                        baselineExists = true,
                        beforeImage = "before/0000.bin",
                        beforeSha256 = Sha256(baseline),
                        appliedSha256 = Sha256(applied)
                    }
                }
            }));
        var activeJournalPath = Path.Combine(
            _rootPath,
            ".boe_runtime",
            "worker-apply-transactions",
            "active.json");
        await File.WriteAllTextAsync(
            activeJournalPath,
            JsonSerializer.Serialize(new
            {
                schemaVersion = 1,
                transactionId,
                committed = false
            }));
        await File.WriteAllBytesAsync(
            _fs.ResolvePath(trackedPath),
            applied);

        var linked = false;
        var hooks = new FileSystemManagerHooks
        {
            AfterExactPhysicalReadInitialValidationAsync = path =>
            {
                if (!linked &&
                    path.Equals(
                        beforePath,
                        StringComparison.OrdinalIgnoreCase))
                {
                    WindowsHardLinkTestHelper.Create(aliasPath, beforePath);
                    linked = true;
                }

                return Task.CompletedTask;
            }
        };
        var raceFs = new FileSystemManager(
            _rootPath,
            NullLogger<FileSystemManager>.Instance,
            PhysicalLoadTransactionOperations.Instance,
            hooks);

        try
        {
            await Assert.ThrowsAsync<InvalidDataException>(
                () => raceFs.WriteFileAtomicBytesAsync(
                    triggerPath,
                    [0x55]));

            Assert.True(linked);
            Assert.Equal(
                applied,
                await File.ReadAllBytesAsync(
                    raceFs.ResolvePath(trackedPath)));
            Assert.True(File.Exists(activeJournalPath));
            Assert.True(Directory.Exists(transactionRoot));
            Assert.False(File.Exists(raceFs.ResolvePath(triggerPath)));
        }
        finally
        {
            if (File.Exists(aliasPath))
                File.Delete(aliasPath);
        }
    }

    [Fact]
    public async Task WorkerRecovery_BeforeImageReplacementAfterInitialValidationIsBlocked()
    {
        if (!OperatingSystem.IsWindows())
            return;

        const string trackedPath =
            "game_state/world/worker-before-image-replacement.json";
        const string triggerPath =
            "game_state/world/worker-before-image-replacement-trigger.json";
        byte[] baseline = [0x21, 0x32];
        byte[] applied = [0x43, 0x54];
        await _fs.WriteFileAtomicBytesAsync(trackedPath, baseline);

        var transactionId = Guid.NewGuid().ToString("N");
        var transactionRoot = Path.Combine(
            _rootPath,
            ".boe_runtime",
            "worker-apply-transactions",
            transactionId);
        var beforeRoot = Path.Combine(transactionRoot, "before");
        var beforePath = Path.Combine(beforeRoot, "0000.bin");
        var displacedPath = beforePath + ".displaced";
        Directory.CreateDirectory(beforeRoot);
        await File.WriteAllBytesAsync(beforePath, baseline);
        await File.WriteAllTextAsync(
            Path.Combine(transactionRoot, "manifest.json"),
            JsonSerializer.Serialize(new
            {
                schemaVersion = 1,
                transactionId,
                entries = new[]
                {
                    new
                    {
                        path = trackedPath,
                        baselineExists = true,
                        beforeImage = "before/0000.bin",
                        beforeSha256 = Sha256(baseline),
                        appliedSha256 = Sha256(applied)
                    }
                }
            }));
        var activeJournalPath = Path.Combine(
            _rootPath,
            ".boe_runtime",
            "worker-apply-transactions",
            "active.json");
        await File.WriteAllTextAsync(
            activeJournalPath,
            JsonSerializer.Serialize(new
            {
                schemaVersion = 1,
                transactionId,
                committed = false
            }));
        await File.WriteAllBytesAsync(
            _fs.ResolvePath(trackedPath),
            applied);

        var replacementBlocked = false;
        var hooks = new FileSystemManagerHooks
        {
            AfterExactPhysicalReadInitialValidationAsync = path =>
            {
                if (path.Equals(
                        beforePath,
                        StringComparison.OrdinalIgnoreCase))
                {
                    try
                    {
                        File.Move(beforePath, displacedPath);
                    }
                    catch (Exception ex) when (
                        ex is IOException or UnauthorizedAccessException)
                    {
                        replacementBlocked = true;
                    }
                }

                return Task.CompletedTask;
            }
        };
        var raceFs = new FileSystemManager(
            _rootPath,
            NullLogger<FileSystemManager>.Instance,
            PhysicalLoadTransactionOperations.Instance,
            hooks);

        await raceFs.WriteFileAtomicBytesAsync(
            triggerPath,
            [0x65]);

        Assert.True(replacementBlocked);
        Assert.False(File.Exists(displacedPath));
        Assert.Equal(
            baseline,
            await raceFs.ReadFileBytesAsync(trackedPath));
        Assert.Equal(
            [0x65],
            await raceFs.ReadFileBytesAsync(triggerPath));
        Assert.False(File.Exists(activeJournalPath));
        Assert.False(Directory.Exists(transactionRoot));
    }

    [Fact]
    public async Task CanonicalWriter_CompletesEveryRecoverableRollbackEntryBeforeFailingClosed()
    {
        const string restorablePath = "game_state/world/weather.json";
        const string unownedPath = "game_state/world/current_location.json";
        byte[] restorableBaseline = [0x10];
        byte[] restorableApplied = [0x20];
        byte[] unownedBaseline = [0x30];
        byte[] unownedApplied = [0x40];
        byte[] unownedNewer = [0x50];
        await _fs.WriteFileAtomicBytesAsync(restorablePath, restorableBaseline);
        await _fs.WriteFileAtomicBytesAsync(unownedPath, unownedBaseline);

        var transactionId = Guid.NewGuid().ToString("N");
        var transactionRoot = Path.Combine(
            _rootPath,
            ".boe_runtime",
            "worker-apply-transactions",
            transactionId);
        var beforeRoot = Path.Combine(transactionRoot, "before");
        Directory.CreateDirectory(beforeRoot);
        await File.WriteAllBytesAsync(Path.Combine(beforeRoot, "0000.bin"), restorableBaseline);
        await File.WriteAllBytesAsync(Path.Combine(beforeRoot, "0001.bin"), unownedBaseline);
        await File.WriteAllTextAsync(
            Path.Combine(transactionRoot, "manifest.json"),
            JsonSerializer.Serialize(new
            {
                schemaVersion = 1,
                transactionId,
                entries = new[]
                {
                    new
                    {
                        path = restorablePath,
                        baselineExists = true,
                        beforeImage = "before/0000.bin",
                        beforeSha256 = Sha256(restorableBaseline),
                        appliedSha256 = Sha256(restorableApplied)
                    },
                    new
                    {
                        path = unownedPath,
                        baselineExists = true,
                        beforeImage = "before/0001.bin",
                        beforeSha256 = Sha256(unownedBaseline),
                        appliedSha256 = Sha256(unownedApplied)
                    }
                }
            }));
        var activeJournalPath = Path.Combine(
            _rootPath,
            ".boe_runtime",
            "worker-apply-transactions",
            "active.json");
        await File.WriteAllTextAsync(
            activeJournalPath,
            JsonSerializer.Serialize(new { schemaVersion = 1, transactionId, committed = false }));
        await File.WriteAllBytesAsync(_fs.ResolvePath(restorablePath), restorableApplied);
        await File.WriteAllBytesAsync(_fs.ResolvePath(unownedPath), unownedNewer);

        var exception = await Assert.ThrowsAsync<InvalidDataException>(() =>
            _fs.WriteFileAtomicBytesAsync("game_state/world/must_not_write.json", [0x60]));

        Assert.Contains("unowned canonical bytes", exception.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(restorableBaseline, await _fs.ReadFileBytesAsync(restorablePath));
        Assert.Equal(unownedNewer, await _fs.ReadFileBytesAsync(unownedPath));
        Assert.False(_fs.FileExists("game_state/world/must_not_write.json"));
        Assert.True(File.Exists(activeJournalPath));
    }

    [Fact]
    public async Task CanonicalWriter_CleansCommittedWorkerApplyWithoutRollingItBack()
    {
        const string path = "game_state/world/weather.json";
        byte[] baseline = [0x10];
        byte[] applied = [0x20];
        await _fs.WriteFileAtomicBytesAsync(path, applied);

        var transactionId = Guid.NewGuid().ToString("N");
        var transactionRoot = Path.Combine(
            _rootPath,
            ".boe_runtime",
            "worker-apply-transactions",
            transactionId);
        var beforeRoot = Path.Combine(transactionRoot, "before");
        Directory.CreateDirectory(beforeRoot);
        await File.WriteAllBytesAsync(Path.Combine(beforeRoot, "0000.bin"), baseline);
        await File.WriteAllTextAsync(
            Path.Combine(transactionRoot, "manifest.json"),
            JsonSerializer.Serialize(new
            {
                schemaVersion = 1,
                transactionId,
                entries = new[]
                {
                    new
                    {
                        path,
                        baselineExists = true,
                        beforeImage = "before/0000.bin",
                        beforeSha256 = Sha256(baseline),
                        appliedSha256 = Sha256(applied)
                    }
                }
            }));
        var activeJournalPath = Path.Combine(
            _rootPath,
            ".boe_runtime",
            "worker-apply-transactions",
            "active.json");
        await File.WriteAllTextAsync(
            activeJournalPath,
            JsonSerializer.Serialize(new { schemaVersion = 1, transactionId, committed = true }));

        await _fs.WriteFileAtomicBytesAsync("game_state/world/after_commit.json", [0x70]);

        Assert.Equal(applied, await _fs.ReadFileBytesAsync(path));
        Assert.Equal([0x70], await _fs.ReadFileBytesAsync("game_state/world/after_commit.json"));
        Assert.False(File.Exists(activeJournalPath));
        Assert.False(Directory.Exists(transactionRoot));
    }

    [Fact]
    public async Task WorkerRecovery_DirectoryAtMissingDestinationRetainsEvidence()
    {
        const string trackedPath =
            "game_state/world/malformed-worker-destination.json";
        const string triggerPath =
            "game_state/world/malformed-worker-destination-trigger.json";
        CanonicalWorkerApplyTransaction transaction;
        await using (var lease =
                     await _fs.AcquireCanonicalWriteLeaseAsync())
        {
            transaction = await _fs.BeginWorkerApplyTransactionAsync(
                lease,
                [new CanonicalWorkerApplyChange(
                    trackedPath,
                    BaselineBytes: null,
                    AppliedBytes: null)]);
        }

        var destinationPath = _fs.ResolvePath(trackedPath);
        Directory.CreateDirectory(destinationPath);

        await Assert.ThrowsAsync<InvalidDataException>(
            () => _fs.WriteFileAtomicBytesAsync(triggerPath, [0x51]));

        Assert.True(Directory.Exists(destinationPath));
        Assert.True(File.Exists(
            _fs.ActiveWorkerApplyTransactionJournalPath));
        Assert.True(Directory.Exists(transaction.TransactionRoot));
        Assert.False(File.Exists(_fs.ResolvePath(triggerPath)));
    }

    [Fact]
    public async Task CommittedWorkerApply_CleanupFailureRetainsJournalForNextWriterRetry()
    {
        const string path = "game_state/world/weather.json";
        byte[] baseline = [0x10];
        byte[] applied = [0x20];
        var operations = new WorkerCleanupFaultOperations();
        var fs = new FileSystemManager(
            _rootPath,
            NullLogger<FileSystemManager>.Instance,
            operations);
        fs.EnsureDirectoryStructure();
        await fs.WriteFileAtomicBytesAsync(path, baseline);

        CanonicalWorkerApplyTransaction transaction;
        await using (var lease = await fs.AcquireCanonicalWriteLeaseAsync())
        {
            transaction = await fs.BeginWorkerApplyTransactionAsync(
                lease,
                [new CanonicalWorkerApplyChange(path, baseline, applied)]);
            Assert.Equal(
                CanonicalFileMutationResult.Applied,
                await fs.CompareExchangeFileBytesAsync(lease, path, baseline, applied));
            operations.FailWorkerTransactionDelete = true;
            fs.CommitWorkerApplyTransaction(lease, transaction);
        }

        Assert.True(File.Exists(fs.ActiveWorkerApplyTransactionJournalPath));
        Assert.True(Directory.Exists(transaction.TransactionRoot));
        Assert.Equal(applied, await fs.ReadFileBytesAsync(path));

        operations.FailWorkerTransactionDelete = false;
        await fs.WriteFileAtomicBytesAsync("game_state/world/after_retry.json", [0x30]);

        Assert.Equal(applied, await fs.ReadFileBytesAsync(path));
        Assert.Equal([0x30], await fs.ReadFileBytesAsync("game_state/world/after_retry.json"));
        Assert.False(File.Exists(fs.ActiveWorkerApplyTransactionJournalPath));
        Assert.False(Directory.Exists(transaction.TransactionRoot));
    }

    [Fact]
    public async Task RolledBackWorkerApply_JournalDeleteFailureDoesNotPoisonNextWriter()
    {
        const string path = "game_state/world/weather.json";
        byte[] baseline = [0x10];
        byte[] applied = [0x20];
        var operations = new WorkerCleanupFaultOperations();
        var fs = new FileSystemManager(
            _rootPath,
            NullLogger<FileSystemManager>.Instance,
            operations);
        fs.EnsureDirectoryStructure();
        await fs.WriteFileAtomicBytesAsync(path, baseline);

        CanonicalWorkerApplyTransaction transaction;
        IReadOnlyList<string> rollbackErrors;
        await using (var lease = await fs.AcquireCanonicalWriteLeaseAsync())
        {
            transaction = await fs.BeginWorkerApplyTransactionAsync(
                lease,
                [new CanonicalWorkerApplyChange(path, baseline, applied)]);
            Assert.Equal(
                CanonicalFileMutationResult.Applied,
                await fs.CompareExchangeFileBytesAsync(lease, path, baseline, applied));
            operations.FailWorkerTransactionJournalDelete = true;
            rollbackErrors = await fs.RollbackWorkerApplyTransactionAsync(lease, transaction);
        }

        Assert.NotEmpty(rollbackErrors);
        Assert.Equal(baseline, await fs.ReadFileBytesAsync(path));
        Assert.False(Directory.Exists(transaction.TransactionRoot));
        Assert.True(File.Exists(fs.ActiveWorkerApplyTransactionJournalPath));

        operations.FailWorkerTransactionJournalDelete = false;
        await fs.WriteFileAtomicBytesAsync("game_state/world/after_rollback_retry.json", [0x30]);

        Assert.Equal(baseline, await fs.ReadFileBytesAsync(path));
        Assert.Equal([0x30], await fs.ReadFileBytesAsync("game_state/world/after_rollback_retry.json"));
        Assert.False(File.Exists(fs.ActiveWorkerApplyTransactionJournalPath));
    }

    [Fact]
    public async Task AppendFileAtomicIfCurrentSessionAsync_DropsStaleSessionTelemetry()
    {
        const string path = "game_state/control/gm_trajectory_ledger.jsonl";
        string capturedGeneration;
        await using (var lease = await _fs.AcquireCanonicalWriteLeaseAsync())
            capturedGeneration = _fs.GetOrCreateSessionGeneration(lease);

        Assert.True(await _fs.AppendFileAtomicIfCurrentSessionAsync(
            path,
            "{\"record\":1}" + Environment.NewLine,
            capturedGeneration));
        await using (var lifecycleLease = await _fs.AcquireSessionLifecycleLeaseAsync())
        await using (var replacementLease =
                     await _fs.AcquireSessionReplacementWriteLeaseAsync(lifecycleLease))
            _fs.RotateSessionGeneration(replacementLease);

        Assert.False(await _fs.AppendFileAtomicIfCurrentSessionAsync(
            path,
            "{\"record\":\"stale\"}" + Environment.NewLine,
            capturedGeneration));

        var ledger = await _fs.ReadFileAsync(path);
        Assert.Contains("\"record\":1", ledger, StringComparison.Ordinal);
        Assert.DoesNotContain("stale", ledger, StringComparison.Ordinal);
    }

    [Fact]
    public async Task SessionGeneration_RejectsUppercaseNonCanonicalGuidText()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(_fs.SessionGenerationPath)!);
        await File.WriteAllTextAsync(
            _fs.SessionGenerationPath,
            "{\"schemaVersion\":1,\"generationId\":\"AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA\"}");

        await using var lease = await _fs.AcquireCanonicalWriteLeaseAsync();
        var exception = Assert.Throws<InvalidDataException>(() => _fs.GetOrCreateSessionGeneration(lease));

        Assert.Contains("invalid", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task CanonicalFileOperations_RejectTraversalOutsideGameSession()
    {
        var outsidePath = Path.Combine(_rootPath, "outside.json");

        await Assert.ThrowsAsync<InvalidDataException>(
            () => _fs.WriteFileAtomicAsync("../outside.json", "{\"escaped\":true}"));
        await Assert.ThrowsAsync<InvalidDataException>(
            () => _fs.ReadFileBytesAsync("../outside.json"));

        Assert.False(File.Exists(outsidePath));
    }

    [Fact]
    public async Task CanonicalFileOperations_RejectExistingReparsePointAncestor()
    {
        var outsideDirectory = Path.Combine(_rootPath, "outside-target");
        Directory.CreateDirectory(outsideDirectory);
        var linkPath = _fs.ResolvePath("game_state/world/linked");
        Directory.CreateDirectory(Path.GetDirectoryName(linkPath)!);
        if (!TryCreateDirectoryLink(linkPath, outsideDirectory))
            return;

        await Assert.ThrowsAsync<InvalidDataException>(
            () => _fs.WriteFileAtomicAsync(
                "game_state/world/linked/escaped.json",
                "{\"escaped\":true}"));

        Assert.False(File.Exists(Path.Combine(outsideDirectory, "escaped.json")));
    }

    [Fact]
    public async Task AtomicWrite_RechecksReparseConfinementAtMutationBoundary()
    {
        const string relativePath = "game_state/world/mutation-boundary/state.json";
        var parentPath = _fs.ResolvePath("game_state/world/mutation-boundary");
        var displacedParentPath = _fs.ResolvePath("game_state/world/mutation-boundary-original");
        var outsideDirectory = Path.Combine(_rootPath, "mutation-boundary-outside");
        var probeLink = Path.Combine(_rootPath, "mutation-boundary-link-probe");
        Directory.CreateDirectory(parentPath);
        Directory.CreateDirectory(outsideDirectory);
        if (!TryCreateDirectoryLink(probeLink, outsideDirectory))
            return;
        Directory.Delete(probeLink);

        var swapped = false;
        var raceFs = new FileSystemManager(
            _rootPath,
            NullLogger<FileSystemManager>.Instance,
            PhysicalLoadTransactionOperations.Instance,
            new FileSystemManagerHooks
            {
                BeforeCanonicalMutationBoundaryAsync = path =>
                {
                    if (swapped || !path.Equals(relativePath, StringComparison.OrdinalIgnoreCase))
                        return Task.CompletedTask;

                    swapped = true;
                    Directory.Move(parentPath, displacedParentPath);
                    Directory.CreateSymbolicLink(parentPath, outsideDirectory);
                    return Task.CompletedTask;
                }
            });

        try
        {
            await Assert.ThrowsAsync<InvalidDataException>(
                () => raceFs.WriteFileAtomicAsync(relativePath, "{\"escaped\":true}"));

            Assert.True(swapped);
            Assert.False(File.Exists(Path.Combine(outsideDirectory, "state.json")));
        }
        finally
        {
            if (Directory.Exists(parentPath) && FileSystemManager.IsReparsePoint(parentPath))
                Directory.Delete(parentPath);
            if (Directory.Exists(displacedParentPath))
                Directory.Move(displacedParentPath, parentPath);
        }
    }

    [Fact]
    public async Task ClearGameStateAsync_RemovesManifestlessBrowserRollbackRoot()
    {
        var orphanPath =
            $"{ExplorerLocalTurnRollbackArtifacts.Root}/orphan/evidence.bin";
        await _fs.WriteFileAtomicBytesAsync(orphanPath, [1, 2, 3, 4]);

        await _fs.ClearGameStateAsync();

        var rollbackRoot = _fs.ResolvePath(
            ExplorerLocalTurnRollbackArtifacts.Root);
        Assert.False(File.Exists(rollbackRoot));
        Assert.False(Directory.Exists(rollbackRoot));
    }

    [Fact]
    public async Task RuntimeDirectoryMove_RechecksReparseConfinementAtMutationBoundary()
    {
        const string relativeDestination = "worker_proposals/proposal-race";
        var proposalRoot = _fs.ResolvePath("worker_proposals");
        var displacedProposalRoot = _fs.ResolvePath("worker-proposals-original");
        var outsideDirectory = Path.Combine(_rootPath, "proposal-move-outside");
        var stagingRoot = Path.Combine(
            _fs.BasePath,
            ".boe_runtime",
            "proposal-staging",
            Guid.NewGuid().ToString("N"),
            "proposal-race");
        Directory.CreateDirectory(proposalRoot);
        Directory.CreateDirectory(outsideDirectory);
        Directory.CreateDirectory(stagingRoot);
        await File.WriteAllTextAsync(Path.Combine(stagingRoot, "proposal.json"), "{}");
        var probeLink = Path.Combine(_rootPath, "proposal-move-link-probe");
        if (!TryCreateDirectoryLink(probeLink, outsideDirectory))
            return;
        Directory.Delete(probeLink);

        var swapped = false;
        var raceFs = new FileSystemManager(
            _rootPath,
            NullLogger<FileSystemManager>.Instance,
            PhysicalLoadTransactionOperations.Instance,
            new FileSystemManagerHooks
            {
                BeforeCanonicalMutationBoundaryAsync = path =>
                {
                    if (swapped || !path.Equals(relativeDestination, StringComparison.OrdinalIgnoreCase))
                        return Task.CompletedTask;

                    swapped = true;
                    Directory.Move(proposalRoot, displacedProposalRoot);
                    Directory.CreateSymbolicLink(proposalRoot, outsideDirectory);
                    return Task.CompletedTask;
                }
            });

        try
        {
            await using var writeLease = await raceFs.AcquireCanonicalWriteLeaseAsync();
            await Assert.ThrowsAsync<InvalidDataException>(
                () => raceFs.MoveRuntimeDirectoryIntoCanonicalSessionAsync(
                    writeLease,
                    stagingRoot,
                    relativeDestination));

            Assert.True(swapped);
            Assert.False(Directory.Exists(Path.Combine(outsideDirectory, "proposal-race")));
            Assert.True(File.Exists(Path.Combine(stagingRoot, "proposal.json")));
        }
        finally
        {
            if (Directory.Exists(proposalRoot) && FileSystemManager.IsReparsePoint(proposalRoot))
                Directory.Delete(proposalRoot);
            if (Directory.Exists(displacedProposalRoot))
                Directory.Move(displacedProposalRoot, proposalRoot);
        }
    }

    [Fact]
    public async Task RuntimeDirectoryMove_RejectsRegularFileRacedIntoDirectorySource()
    {
        if (!OperatingSystem.IsWindows())
            return;

        const string destination = "worker_proposals/type-confused-publication";
        var sourcePath = _fs.CreateRuntimeProposalStagingRoot();
        var displacedSourcePath = sourcePath + "-original";
        await File.WriteAllTextAsync(
            Path.Combine(sourcePath, "proposal.json"),
            """{ "status": "completed" }""");
        var swapped = false;
        var raceFs = new FileSystemManager(
            _rootPath,
            NullLogger<FileSystemManager>.Instance,
            PhysicalLoadTransactionOperations.Instance,
            new FileSystemManagerHooks
            {
                BeforeCanonicalMutationBoundaryAsync = path =>
                {
                    if (swapped ||
                        !path.Equals(destination, StringComparison.OrdinalIgnoreCase))
                    {
                        return Task.CompletedTask;
                    }

                    swapped = true;
                    Directory.Move(sourcePath, displacedSourcePath);
                    File.WriteAllBytes(sourcePath, [0x41, 0x42, 0x43]);
                    return Task.CompletedTask;
                }
            });

        try
        {
            await using var writeLease =
                await raceFs.AcquireCanonicalWriteLeaseAsync();
            await Assert.ThrowsAsync<InvalidDataException>(
                () => raceFs.MoveRuntimeDirectoryIntoCanonicalSessionAsync(
                    writeLease,
                    sourcePath,
                    destination));

            Assert.True(swapped);
            Assert.True(File.Exists(sourcePath));
            Assert.Equal([0x41, 0x42, 0x43], File.ReadAllBytes(sourcePath));
            Assert.False(File.Exists(raceFs.ResolvePath(destination)));
            Assert.False(Directory.Exists(raceFs.ResolvePath(destination)));
            Assert.True(File.Exists(Path.Combine(
                displacedSourcePath,
                "proposal.json")));
        }
        finally
        {
            if (File.Exists(sourcePath))
                File.Delete(sourcePath);
            if (Directory.Exists(displacedSourcePath))
                Directory.Delete(displacedSourcePath, recursive: true);
        }
    }

    [Fact]
    public async Task AcquireCanonicalWriteLease_RejectsRuntimeRootReparsePoint()
    {
        if (!OperatingSystem.IsWindows())
            return;

        var runtimeRoot = Path.Combine(_fs.BasePath, ".boe_runtime");
        var displacedRuntimeRoot = Path.Combine(_fs.BasePath, ".boe_runtime-original");
        var outsideDirectory = Path.Combine(_rootPath, "runtime-root-outside");
        Directory.CreateDirectory(outsideDirectory);
        Directory.Move(runtimeRoot, displacedRuntimeRoot);
        if (!TryCreateDirectoryLink(runtimeRoot, outsideDirectory))
        {
            Directory.Move(displacedRuntimeRoot, runtimeRoot);
            return;
        }

        try
        {
            await Assert.ThrowsAsync<InvalidDataException>(
                async () => await _fs.AcquireCanonicalWriteLeaseAsync());
        }
        finally
        {
            if (Directory.Exists(runtimeRoot) && FileSystemManager.IsReparsePoint(runtimeRoot))
                Directory.Delete(runtimeRoot);
            if (Directory.Exists(displacedRuntimeRoot))
                Directory.Move(displacedRuntimeRoot, runtimeRoot);
        }
    }

    [Fact]
    public async Task AcquireCanonicalWriteLease_RejectsRuntimeLockDirectoryReparsePoint()
    {
        if (!OperatingSystem.IsWindows())
            return;

        var runtimeRoot = Path.Combine(_fs.BasePath, ".boe_runtime");
        var lockRoot = Path.Combine(runtimeRoot, "locks");
        var displacedLockRoot = Path.Combine(runtimeRoot, "locks-original");
        var outsideDirectory = Path.Combine(_rootPath, "runtime-lock-outside");
        Directory.CreateDirectory(outsideDirectory);
        Directory.Move(lockRoot, displacedLockRoot);
        if (!TryCreateDirectoryLink(lockRoot, outsideDirectory))
        {
            Directory.Move(displacedLockRoot, lockRoot);
            return;
        }

        try
        {
            await Assert.ThrowsAsync<InvalidDataException>(
                async () => await _fs.AcquireCanonicalWriteLeaseAsync());
        }
        finally
        {
            if (Directory.Exists(lockRoot) && FileSystemManager.IsReparsePoint(lockRoot))
                Directory.Delete(lockRoot);
            if (Directory.Exists(displacedLockRoot))
                Directory.Move(displacedLockRoot, lockRoot);
        }
    }

    [Fact]
    public async Task SessionGeneration_RejectsRuntimeSiblingReparsePoint()
    {
        if (!OperatingSystem.IsWindows())
            return;

        var generationRoot = Path.Combine(_fs.RuntimeRootPath, "session-generation");
        var outsideRoot = Path.Combine(_rootPath, "session-generation-outside");
        Directory.CreateDirectory(outsideRoot);
        if (Directory.Exists(generationRoot))
            Directory.Delete(generationRoot, recursive: true);
        if (!TryCreateDirectoryLink(generationRoot, outsideRoot))
            return;

        try
        {
            await using var writeLease = await _fs.AcquireCanonicalWriteLeaseAsync();
            Assert.Throws<InvalidDataException>(
                () => _fs.GetOrCreateSessionGeneration(writeLease));
            Assert.False(File.Exists(Path.Combine(outsideRoot, "current.json")));
        }
        finally
        {
            if (Directory.Exists(generationRoot) &&
                FileSystemManager.IsReparsePoint(generationRoot))
            {
                Directory.Delete(generationRoot);
            }
        }
    }

    [Fact]
    public async Task LoadTransactionStaging_RejectsRuntimeSiblingReparsePoint()
    {
        if (!OperatingSystem.IsWindows())
            return;

        var loadRoot = Path.Combine(_fs.RuntimeRootPath, "load-transactions");
        var outsideRoot = Path.Combine(_rootPath, "load-transactions-outside");
        Directory.CreateDirectory(outsideRoot);
        if (Directory.Exists(loadRoot))
            Directory.Delete(loadRoot, recursive: true);
        if (!TryCreateDirectoryLink(loadRoot, outsideRoot))
            return;

        try
        {
            var paths = _fs.GetLoadTransactionPaths(Guid.NewGuid().ToString("N"));
            Assert.Throws<InvalidDataException>(
                () => _fs.CreateLoadDirectory(paths.StagingSessionPath));
            Assert.Empty(Directory.EnumerateFileSystemEntries(outsideRoot));
        }
        finally
        {
            if (Directory.Exists(loadRoot) && FileSystemManager.IsReparsePoint(loadRoot))
                Directory.Delete(loadRoot);
        }
    }

    [Fact]
    public async Task WorkerApplyTransaction_RejectsRuntimeSiblingReparsePoint()
    {
        if (!OperatingSystem.IsWindows())
            return;

        var workerRoot = Path.Combine(_fs.RuntimeRootPath, "worker-apply-transactions");
        var outsideRoot = Path.Combine(_rootPath, "worker-apply-transactions-outside");
        Directory.CreateDirectory(outsideRoot);
        if (Directory.Exists(workerRoot))
            Directory.Delete(workerRoot, recursive: true);
        if (!TryCreateDirectoryLink(workerRoot, outsideRoot))
            return;

        try
        {
            await Assert.ThrowsAsync<InvalidDataException>(
                async () =>
                {
                    await using var writeLease = await _fs.AcquireCanonicalWriteLeaseAsync();
                    await _fs.BeginWorkerApplyTransactionAsync(
                        writeLease,
                        [
                            new CanonicalWorkerApplyChange(
                                "game_state/world/runtime-authority.json",
                                BaselineBytes: null,
                                AppliedBytes: System.Text.Encoding.UTF8.GetBytes("{}"))
                        ]);
                });
            Assert.Empty(Directory.EnumerateFileSystemEntries(outsideRoot));
        }
        finally
        {
            if (Directory.Exists(workerRoot) && FileSystemManager.IsReparsePoint(workerRoot))
                Directory.Delete(workerRoot);
        }
    }

    [Fact]
    public async Task AcquireCanonicalWriteLease_RejectsOpenedExternalLockHandleAfterPathSwap()
    {
        if (!OperatingSystem.IsWindows())
            return;

        await AssertOpenedExternalLockHandleRejectedAsync(isLifecycleLease: false);
    }

    [Fact]
    public async Task AcquireSessionLifecycleLease_RejectsOpenedExternalLockHandleAfterPathSwap()
    {
        if (!OperatingSystem.IsWindows())
            return;

        await AssertOpenedExternalLockHandleRejectedAsync(isLifecycleLease: true);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task LeaseAcquisition_RejectsHardLinkedLockFile(bool isLifecycleLease)
    {
        if (!OperatingSystem.IsWindows())
            return;

        var lockPath = isLifecycleLease
            ? _fs.SessionLifecycleLockPath
            : _fs.CanonicalWriteLockPath;
        var externalLockPath = Path.Combine(
            _rootPath,
            isLifecycleLease
                ? "external-session-lifecycle.lock"
                : "external-canonical-write.lock");
        await File.WriteAllTextAsync(externalLockPath, "external-lock");
        if (File.Exists(lockPath))
            File.Delete(lockPath);
        CreateHardLink(lockPath, externalLockPath);

        if (isLifecycleLease)
        {
            await Assert.ThrowsAsync<InvalidDataException>(
                async () => await _fs.AcquireSessionLifecycleLeaseAsync());
        }
        else
        {
            await Assert.ThrowsAsync<InvalidDataException>(
                async () => await _fs.AcquireCanonicalWriteLeaseAsync());
        }

        Assert.Equal("external-lock", await File.ReadAllTextAsync(externalLockPath));
    }

    [Fact]
    public async Task SessionGeneration_RejectsHardLinkedAuthorityFile()
    {
        if (!OperatingSystem.IsWindows())
            return;

        await using (var setupLease = await _fs.AcquireCanonicalWriteLeaseAsync())
            _ = _fs.GetOrCreateSessionGeneration(setupLease);

        var externalGeneration = Guid.NewGuid().ToString("N");
        var externalPath = Path.Combine(_rootPath, "external-session-generation.json");
        var externalBytes = JsonSerializer.SerializeToUtf8Bytes(new
        {
            schemaVersion = 1,
            generationId = externalGeneration
        });
        await File.WriteAllBytesAsync(externalPath, externalBytes);
        File.Delete(_fs.SessionGenerationPath);
        CreateHardLink(_fs.SessionGenerationPath, externalPath);

        await using var writeLease = await _fs.AcquireCanonicalWriteLeaseAsync();
        Assert.Throws<InvalidDataException>(
            () => _fs.IsCurrentSessionGeneration(writeLease, externalGeneration));
        Assert.Equal(externalBytes, await File.ReadAllBytesAsync(externalPath));
    }

    [Fact]
    public async Task SessionGeneration_DuplicateGenerationPropertyFailsClosed()
    {
        var firstGeneration = Guid.NewGuid().ToString("N");
        var secondGeneration = Guid.NewGuid().ToString("N");
        Directory.CreateDirectory(
            Path.GetDirectoryName(_fs.SessionGenerationPath)!);
        await File.WriteAllTextAsync(
            _fs.SessionGenerationPath,
            $$"""
              {
                "schemaVersion": 1,
                "generationId": "{{firstGeneration}}",
                "GenerationId": "{{secondGeneration}}"
              }
              """);
        await using var writeLease =
            await _fs.AcquireCanonicalWriteLeaseAsync();

        Assert.Throws<InvalidDataException>(
            () => _fs.IsCurrentSessionGeneration(
                writeLease,
                secondGeneration));

        Assert.True(File.Exists(_fs.SessionGenerationPath));
    }

    [Fact]
    public async Task SessionGeneration_LinkAddedAfterInitialValidationFailsClosed()
    {
        if (!OperatingSystem.IsWindows())
            return;

        string generation;
        await using (var setupLease = await _fs.AcquireCanonicalWriteLeaseAsync())
            generation = _fs.GetOrCreateSessionGeneration(setupLease);

        var aliasPath = Path.Combine(
            _rootPath,
            "runtime-generation-completion-alias.json");
        var armed = false;
        var linked = false;
        var hooks = FileSystemManagerHookTestHelper.WithPathHook(
            "AfterRuntimeFileReadInitialValidationAsync",
            path =>
            {
                if (armed &&
                    !linked &&
                    path.Equals(
                        _fs.SessionGenerationPath,
                        StringComparison.OrdinalIgnoreCase))
                {
                    WindowsHardLinkTestHelper.Create(aliasPath, path);
                    linked = true;
                }

                return Task.CompletedTask;
            });
        var raceFs = new FileSystemManager(
            _rootPath,
            NullLogger<FileSystemManager>.Instance,
            PhysicalLoadTransactionOperations.Instance,
            hooks);

        await using var writeLease =
            await raceFs.AcquireCanonicalWriteLeaseAsync();
        armed = true;

        Assert.Throws<InvalidDataException>(
            () => raceFs.IsCurrentSessionGeneration(writeLease, generation));
        Assert.True(linked);
    }

    [Fact]
    public async Task OpenExactPhysicalReadFile_CompletionRejectsLinkAddedAfterConsumption()
    {
        if (!OperatingSystem.IsWindows())
            return;

        var archivePath = Path.Combine(_rootPath, "completion-gated-save.zip");
        var aliasPath = Path.Combine(
            _rootPath,
            "completion-gated-save-alias.zip");
        await File.WriteAllBytesAsync(
            archivePath,
            [0x50, 0x4B, 0x05, 0x06]);
        var openedFile = Assert.IsType<FileSystemManager.StableReadFile>(
            _fs.OpenExactPhysicalReadFile(
                archivePath,
                "Completion-gated save archive"));

        await using (openedFile)
        {
            await openedFile.Stream.CopyToAsync(Stream.Null);
            WindowsHardLinkTestHelper.Create(aliasPath, archivePath);
            var completeMethod = openedFile.GetType().GetMethod(
                "Complete",
                System.Reflection.BindingFlags.Instance |
                System.Reflection.BindingFlags.NonPublic);
            Assert.NotNull(completeMethod);

            var exception = Assert.Throws<System.Reflection.TargetInvocationException>(
                () => completeMethod!.Invoke(openedFile, null));
            Assert.IsType<InvalidDataException>(exception.InnerException);

            var abandonMethod = openedFile.GetType().GetMethod(
                "Abandon",
                System.Reflection.BindingFlags.Instance |
                System.Reflection.BindingFlags.NonPublic);
            Assert.NotNull(abandonMethod);
            abandonMethod!.Invoke(openedFile, null);
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task CanonicalWriter_RejectsHardLinkedTransactionJournal(
        bool isWorkerJournal)
    {
        if (!OperatingSystem.IsWindows())
            return;

        var transactionId = Guid.NewGuid().ToString("N");
        var journalPath = isWorkerJournal
            ? _fs.ActiveWorkerApplyTransactionJournalPath
            : _fs.ActiveLoadTransactionJournalPath;
        var externalPath = Path.Combine(
            _rootPath,
            isWorkerJournal
                ? "external-worker-journal.json"
                : "external-load-journal.json");
        var journalBytes = isWorkerJournal
            ? JsonSerializer.SerializeToUtf8Bytes(new
            {
                schemaVersion = 1,
                transactionId,
                committed = true,
                rolledBack = false
            })
            : JsonSerializer.SerializeToUtf8Bytes(new
            {
                schemaVersion = 1,
                transactionId,
                committed = true
            });
        await File.WriteAllBytesAsync(externalPath, journalBytes);
        Directory.CreateDirectory(
            Path.GetDirectoryName(journalPath)
            ?? throw new InvalidDataException(
                "Transaction journal has no parent directory."));
        if (File.Exists(journalPath))
            File.Delete(journalPath);
        CreateHardLink(journalPath, externalPath);

        await Assert.ThrowsAsync<InvalidDataException>(
            async () => await _fs.AcquireCanonicalWriteLeaseAsync());

        Assert.Equal(journalBytes, await File.ReadAllBytesAsync(externalPath));
        Assert.True(File.Exists(journalPath));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task CanonicalWriter_DirectoryAtActiveTransactionJournalFailsClosed(
        bool isWorkerJournal)
    {
        var journalPath = isWorkerJournal
            ? _fs.ActiveWorkerApplyTransactionJournalPath
            : _fs.ActiveLoadTransactionJournalPath;
        Directory.CreateDirectory(journalPath);

        await Assert.ThrowsAsync<InvalidDataException>(
            async () =>
            {
                await using var lease =
                    await _fs.AcquireCanonicalWriteLeaseAsync();
            });

        Assert.True(Directory.Exists(journalPath));
    }

    [Fact]
    public async Task LoadRecovery_RegularFileAtBackupDirectoryRetainsEvidence()
    {
        var transactionId = Guid.NewGuid().ToString("N");
        var replacementGenerationId = Guid.NewGuid().ToString("N");
        var transactionRoot = Path.Combine(
            _rootPath,
            ".boe_runtime",
            "load-transactions",
            transactionId);
        var malformedBackupPath = Path.Combine(
            transactionRoot,
            "backup",
            "game_session");
        Directory.CreateDirectory(Path.GetDirectoryName(malformedBackupPath)!);
        byte[] evidence = [0x31, 0x42, 0x53];
        await File.WriteAllBytesAsync(malformedBackupPath, evidence);
        Directory.CreateDirectory(
            Path.GetDirectoryName(_fs.ActiveLoadTransactionJournalPath)!);
        await File.WriteAllTextAsync(
            _fs.ActiveLoadTransactionJournalPath,
            JsonSerializer.Serialize(new
            {
                schemaVersion = 2,
                transactionId,
                committed = false,
                previousGenerationId = (string?)null,
                replacementGenerationId
            }));

        await Assert.ThrowsAsync<InvalidDataException>(
            async () =>
            {
                await using var lease =
                    await _fs.AcquireCanonicalWriteLeaseAsync();
            });

        Assert.Equal(
            evidence,
            await File.ReadAllBytesAsync(malformedBackupPath));
        Assert.True(File.Exists(_fs.ActiveLoadTransactionJournalPath));
        Assert.True(Directory.Exists(transactionRoot));
    }

    [Fact]
    public async Task LoadRecovery_DuplicateCommittedPropertyRetainsEvidence()
    {
        var transactionId = Guid.NewGuid().ToString("N");
        var replacementGenerationId = Guid.NewGuid().ToString("N");
        var transactionRoot = _fs.GetLoadTransactionPaths(transactionId).TransactionRoot;
        Directory.CreateDirectory(transactionRoot);
        var evidencePath = Path.Combine(transactionRoot, "ambiguous-load-evidence.bin");
        byte[] evidence = [0x15, 0x26, 0x37];
        await File.WriteAllBytesAsync(evidencePath, evidence);
        Directory.CreateDirectory(
            Path.GetDirectoryName(_fs.ActiveLoadTransactionJournalPath)!);
        var journal =
            $$"""
              {
                "schemaVersion": 2,
                "transactionId": "{{transactionId}}",
                "committed": false,
                "Committed": true,
                "previousGenerationId": null,
                "replacementGenerationId": "{{replacementGenerationId}}"
              }
              """;
        await File.WriteAllTextAsync(
            _fs.ActiveLoadTransactionJournalPath,
            journal);

        await Assert.ThrowsAsync<InvalidDataException>(
            async () =>
            {
                await using var lease =
                    await _fs.AcquireCanonicalWriteLeaseAsync();
            });

        Assert.Equal(evidence, await File.ReadAllBytesAsync(evidencePath));
        Assert.Equal(
            journal,
            await File.ReadAllTextAsync(_fs.ActiveLoadTransactionJournalPath));
    }

    [Fact]
    public async Task WorkerRecovery_DuplicateJournalPropertyRetainsEvidence()
    {
        var transactionId = Guid.NewGuid().ToString("N");
        var transactionRoot = Path.Combine(
            _fs.RuntimeRootPath,
            "worker-apply-transactions",
            transactionId);
        Directory.CreateDirectory(transactionRoot);
        var evidencePath = Path.Combine(transactionRoot, "ambiguous-worker-evidence.bin");
        byte[] evidence = [0x48, 0x59, 0x6A];
        await File.WriteAllBytesAsync(evidencePath, evidence);
        Directory.CreateDirectory(
            Path.GetDirectoryName(_fs.ActiveWorkerApplyTransactionJournalPath)!);
        var journal =
            $$"""
              {
                "schemaVersion": 1,
                "transactionId": "{{transactionId}}",
                "committed": false,
                "Committed": true,
                "rolledBack": false
              }
              """;
        await File.WriteAllTextAsync(
            _fs.ActiveWorkerApplyTransactionJournalPath,
            journal);

        await Assert.ThrowsAsync<InvalidDataException>(
            async () =>
            {
                await using var lease =
                    await _fs.AcquireCanonicalWriteLeaseAsync();
            });

        Assert.Equal(evidence, await File.ReadAllBytesAsync(evidencePath));
        Assert.Equal(
            journal,
            await File.ReadAllTextAsync(
                _fs.ActiveWorkerApplyTransactionJournalPath));
    }

    [Fact]
    public async Task WorkerRecovery_DuplicateManifestEntriesRetainsEvidence()
    {
        var transactionId = Guid.NewGuid().ToString("N");
        var transactionRoot = Path.Combine(
            _fs.RuntimeRootPath,
            "worker-apply-transactions",
            transactionId);
        Directory.CreateDirectory(transactionRoot);
        const string trackedPath =
            "game_state/world/ambiguous-worker-manifest.json";
        var entry =
            $$"""
              {
                "path": "{{trackedPath}}",
                "baselineExists": false,
                "beforeImage": null,
                "beforeSha256": "missing",
                "appliedSha256": "missing"
              }
              """;
        var manifest =
            $$"""
              {
                "schemaVersion": 1,
                "transactionId": "{{transactionId}}",
                "entries": [{{entry}}],
                "Entries": [{{entry}}]
              }
              """;
        var manifestPath = Path.Combine(transactionRoot, "manifest.json");
        await File.WriteAllTextAsync(manifestPath, manifest);
        Directory.CreateDirectory(
            Path.GetDirectoryName(_fs.ActiveWorkerApplyTransactionJournalPath)!);
        await File.WriteAllTextAsync(
            _fs.ActiveWorkerApplyTransactionJournalPath,
            $$"""
              {
                "schemaVersion": 1,
                "transactionId": "{{transactionId}}",
                "committed": false,
                "rolledBack": false
              }
              """);

        await Assert.ThrowsAsync<InvalidDataException>(
            async () =>
            {
                await using var lease =
                    await _fs.AcquireCanonicalWriteLeaseAsync();
            });

        Assert.Equal(manifest, await File.ReadAllTextAsync(manifestPath));
        Assert.True(File.Exists(_fs.ActiveWorkerApplyTransactionJournalPath));
        Assert.True(Directory.Exists(transactionRoot));
    }

    [Fact]
    public async Task ReadFileAsync_RejectsHardLinkedCanonicalState()
    {
        if (!OperatingSystem.IsWindows())
            return;

        const string relativePath = "game_state/world/hard-linked-state.json";
        var canonicalPath = _fs.ResolvePath(relativePath);
        var externalPath = Path.Combine(_rootPath, "external-state.json");
        byte[] externalBytes = [0x7B, 0x22, 0x76, 0x22, 0x3A, 0x31, 0x7D];
        await File.WriteAllBytesAsync(externalPath, externalBytes);
        CreateHardLink(canonicalPath, externalPath);

        await Assert.ThrowsAsync<InvalidDataException>(
            () => _fs.ReadFileAsync(relativePath));

        Assert.Equal(externalBytes, await File.ReadAllBytesAsync(externalPath));
    }

    [Fact]
    public async Task ReadFileAsync_DirectoryAtOptionalFileBoundaryFailsClosed()
    {
        const string relativePath =
            "game_state/world/directory-at-async-read.json";
        var fullPath = _fs.ResolvePath(relativePath);
        Directory.CreateDirectory(fullPath);

        await Assert.ThrowsAsync<InvalidDataException>(
            () => _fs.ReadFileAsync(relativePath));

        Assert.True(Directory.Exists(fullPath));
    }

    [Fact]
    public void ReadFileSync_DirectoryAtOptionalFileBoundaryFailsClosed()
    {
        const string relativePath =
            "game_state/world/directory-at-sync-read.json";
        var fullPath = _fs.ResolvePath(relativePath);
        Directory.CreateDirectory(fullPath);

        Assert.Throws<InvalidDataException>(
            () => _fs.ReadFileSync(relativePath));

        Assert.True(Directory.Exists(fullPath));
    }

    [Fact]
    public async Task ReadFileAsync_RegularFileAtIntermediateParentFailsClosed()
    {
        const string parentRelativePath =
            "game_state/world/async-read-parent-file";
        const string relativePath =
            parentRelativePath + "/state.json";
        var parentPath = _fs.ResolvePath(parentRelativePath);
        await File.WriteAllTextAsync(parentPath, "{}");

        await Assert.ThrowsAsync<InvalidDataException>(
            () => _fs.ReadFileAsync(relativePath));

        Assert.True(File.Exists(parentPath));
    }

    [Fact]
    public void ReadFileSync_RegularFileAtIntermediateParentFailsClosed()
    {
        const string parentRelativePath =
            "game_state/world/sync-read-parent-file";
        const string relativePath =
            parentRelativePath + "/state.json";
        var parentPath = _fs.ResolvePath(parentRelativePath);
        File.WriteAllText(parentPath, "{}");

        Assert.Throws<InvalidDataException>(
            () => _fs.ReadFileSync(relativePath));

        Assert.True(File.Exists(parentPath));
    }

    [Fact]
    public void FileExists_RegularFileAtIntermediateParentFailsClosed()
    {
        const string parentRelativePath =
            "game_state/world/existence-parent-file";
        const string relativePath =
            parentRelativePath + "/state.json";
        var parentPath = _fs.ResolvePath(parentRelativePath);
        File.WriteAllText(parentPath, "{}");

        Assert.Throws<InvalidDataException>(
            () => _fs.FileExists(relativePath));

        Assert.True(File.Exists(parentPath));
    }

    [Fact]
    public async Task AtomicWrite_RejectsHardLinkedExistingDestination()
    {
        if (!OperatingSystem.IsWindows())
            return;

        const string relativePath =
            "game_state/world/hard-linked-replacement-target.json";
        var canonicalPath = _fs.ResolvePath(relativePath);
        var externalPath = Path.Combine(
            _rootPath,
            "external-replacement-target.json");
        byte[] originalBytes = [0x7B, 0x22, 0x76, 0x22, 0x3A, 0x31, 0x7D];
        await File.WriteAllBytesAsync(externalPath, originalBytes);
        CreateHardLink(canonicalPath, externalPath);

        await Assert.ThrowsAsync<InvalidDataException>(
            () => _fs.WriteFileAtomicBytesAsync(
                relativePath,
                [0x7B, 0x22, 0x76, 0x22, 0x3A, 0x32, 0x7D]));

        Assert.Equal(originalBytes, await File.ReadAllBytesAsync(externalPath));
        Assert.Equal(originalBytes, await File.ReadAllBytesAsync(canonicalPath));
    }

    [Fact]
    public async Task AtomicWrite_PostPublicationSourceLinkRestoresExactPriorDestination()
    {
        if (!OperatingSystem.IsWindows())
            return;

        const string relativePath =
            "game_state/world/post-publication-source-link.json";
        byte[] priorBytes = [0x10, 0x21, 0x32, 0x43];
        byte[] replacementBytes = [0x54, 0x65, 0x76, 0x87];
        await _fs.WriteFileAtomicBytesAsync(relativePath, priorBytes);
        var destinationPath = _fs.ResolvePath(relativePath);
        var priorIdentity =
            WindowsHardLinkTestHelper.CaptureIdentity(destinationPath);
        var aliasPath = Path.Combine(
            _rootPath,
            "post-publication-source-alias.json");
        var hookCount = 0;
        var hooks = FileSystemManagerHookTestHelper.WithPathHook(
            "AfterPhysicalFilePublishedAsync",
            path =>
            {
                if (path.Equals(destinationPath, StringComparison.OrdinalIgnoreCase))
                {
                    hookCount++;
                    WindowsHardLinkTestHelper.Create(aliasPath, path);
                }

                return Task.CompletedTask;
            });
        var raceFs = new FileSystemManager(
            _rootPath,
            NullLogger<FileSystemManager>.Instance,
            PhysicalLoadTransactionOperations.Instance,
            hooks);

        await Assert.ThrowsAsync<InvalidDataException>(
            () => raceFs.WriteFileAtomicBytesAsync(
                relativePath,
                replacementBytes));

        Assert.Equal(1, hookCount);
        Assert.Equal(priorBytes, await File.ReadAllBytesAsync(destinationPath));
        Assert.Equal(
            priorIdentity,
            WindowsHardLinkTestHelper.CaptureIdentity(destinationPath));
        Assert.Equal(
            replacementBytes,
            await File.ReadAllBytesAsync(aliasPath));
        Assert.NotEmpty(Directory.GetDirectories(
            Path.Combine(
                raceFs.RuntimeRootPath,
                "file-publication-transactions")));
    }

    [Fact]
    public async Task CanonicalWriter_RegularFileAtPublicationJournalRootFailsClosed()
    {
        var journalRoot = _fs.PhysicalPublicationTransactionsRootPath;
        Directory.CreateDirectory(Path.GetDirectoryName(journalRoot)!);
        byte[] evidence = [0x64, 0x75, 0x86];
        await File.WriteAllBytesAsync(journalRoot, evidence);

        await Assert.ThrowsAsync<InvalidDataException>(
            async () =>
            {
                await using var lease =
                    await _fs.AcquireCanonicalWriteLeaseAsync();
            });

        Assert.Equal(evidence, await File.ReadAllBytesAsync(journalRoot));
    }

    [Fact]
    public async Task AtomicWrite_PostPublicationSourceLinkRestoresExactPriorAbsence()
    {
        if (!OperatingSystem.IsWindows())
            return;

        const string relativePath =
            "game_state/world/post-publication-source-link-absent.json";
        byte[] replacementBytes = [0x98, 0x89, 0x7A, 0x6B];
        var destinationPath = _fs.ResolvePath(relativePath);
        var aliasPath = Path.Combine(
            _rootPath,
            "post-publication-absence-alias.json");
        var hooks = FileSystemManagerHookTestHelper.WithPathHook(
            "AfterPhysicalFilePublishedAsync",
            path =>
            {
                if (path.Equals(destinationPath, StringComparison.OrdinalIgnoreCase))
                    WindowsHardLinkTestHelper.Create(aliasPath, path);
                return Task.CompletedTask;
            });
        var raceFs = new FileSystemManager(
            _rootPath,
            NullLogger<FileSystemManager>.Instance,
            PhysicalLoadTransactionOperations.Instance,
            hooks);

        await Assert.ThrowsAsync<InvalidDataException>(
            () => raceFs.WriteFileAtomicBytesAsync(
                relativePath,
                replacementBytes));

        Assert.False(File.Exists(destinationPath));
        Assert.Equal(
            replacementBytes,
            await File.ReadAllBytesAsync(aliasPath));
    }

    [Fact]
    public async Task AtomicWrite_RollbackFinalAbsenceRaceRetainsEvidence()
    {
        if (!OperatingSystem.IsWindows())
            return;

        const string relativePath =
            "game_state/world/rollback-final-absence-race.json";
        var destinationPath = _fs.ResolvePath(relativePath);
        var missingTarget = Path.Combine(
            _rootPath,
            "missing-rollback-final-target.json");
        var hookInvoked = false;
        var hooks = new FileSystemManagerHooks
        {
            AfterPhysicalFilePublishedAsync = path =>
                path.Equals(
                    destinationPath,
                    StringComparison.OrdinalIgnoreCase)
                    ? Task.FromException(
                        new IOException(
                            "Injected post-publication failure."))
                    : Task.CompletedTask
        };
        FileSystemManagerHookTestHelper.SetPathHook(
            hooks,
            "BeforePhysicalRollbackAbsenceFinalValidationAsync",
            path =>
            {
                if (path.Equals(
                        destinationPath,
                        StringComparison.OrdinalIgnoreCase))
                {
                    hookInvoked = true;
                    File.CreateSymbolicLink(path, missingTarget);
                }

                return Task.CompletedTask;
            });
        var raceFs = new FileSystemManager(
            _rootPath,
            NullLogger<FileSystemManager>.Instance,
            PhysicalLoadTransactionOperations.Instance,
            hooks);

        try
        {
            await Assert.ThrowsAsync<InvalidDataException>(
                () => raceFs.WriteFileAtomicBytesAsync(
                    relativePath,
                    [0x18, 0x29, 0x3A]));

            Assert.True(hookInvoked);
            Assert.True(
                File.GetAttributes(destinationPath)
                    .HasFlag(FileAttributes.ReparsePoint));
            Assert.NotEmpty(Directory.EnumerateFileSystemEntries(
                raceFs.PhysicalPublicationTransactionsRootPath));
        }
        finally
        {
            try
            {
                File.Delete(destinationPath);
            }
            catch (FileNotFoundException)
            {
            }
        }
    }

    [Fact]
    public async Task AtomicWrite_DestinationLinkAfterAuthorityValidationFencesWithoutPublishing()
    {
        if (!OperatingSystem.IsWindows())
            return;

        const string relativePath =
            "game_state/world/destination-link-race.json";
        byte[] priorBytes = [0xA1, 0xB2, 0xC3];
        await _fs.WriteFileAtomicBytesAsync(relativePath, priorBytes);
        var destinationPath = _fs.ResolvePath(relativePath);
        var priorIdentity =
            WindowsHardLinkTestHelper.CaptureIdentity(destinationPath);
        var aliasPath = Path.Combine(
            _rootPath,
            "destination-link-race-alias.json");
        var hooks = FileSystemManagerHookTestHelper.WithPathHook(
            "AfterPhysicalFileAuthorityValidatedAsync",
            path =>
            {
                if (path.Equals(destinationPath, StringComparison.OrdinalIgnoreCase))
                    WindowsHardLinkTestHelper.Create(aliasPath, path);
                return Task.CompletedTask;
            });
        var raceFs = new FileSystemManager(
            _rootPath,
            NullLogger<FileSystemManager>.Instance,
            PhysicalLoadTransactionOperations.Instance,
            hooks);

        await Assert.ThrowsAsync<InvalidDataException>(
            () => raceFs.WriteFileAtomicBytesAsync(
                relativePath,
                [0xD4, 0xE5, 0xF6]));

        Assert.Equal(priorBytes, await File.ReadAllBytesAsync(destinationPath));
        Assert.Equal(
            priorIdentity with { NumberOfLinks = 2 },
            WindowsHardLinkTestHelper.CaptureIdentity(destinationPath));
        Assert.Equal(priorBytes, await File.ReadAllBytesAsync(aliasPath));
    }

    [Fact]
    public async Task AtomicWrite_PostPublicationFailureRestoresPriorIdentityAndCleansJournal()
    {
        if (!OperatingSystem.IsWindows())
            return;

        const string relativePath =
            "game_state/world/post-publication-failure.json";
        byte[] priorBytes = [0x01, 0x12, 0x23];
        await _fs.WriteFileAtomicBytesAsync(relativePath, priorBytes);
        var destinationPath = _fs.ResolvePath(relativePath);
        var priorIdentity =
            WindowsHardLinkTestHelper.CaptureIdentity(destinationPath);
        var hooks = FileSystemManagerHookTestHelper.WithPathHook(
            "AfterPhysicalFilePublishedAsync",
            path => path.Equals(
                    destinationPath,
                    StringComparison.OrdinalIgnoreCase)
                ? Task.FromException(
                    new IOException("Injected post-publication failure."))
                : Task.CompletedTask);
        var raceFs = new FileSystemManager(
            _rootPath,
            NullLogger<FileSystemManager>.Instance,
            PhysicalLoadTransactionOperations.Instance,
            hooks);

        await Assert.ThrowsAsync<IOException>(
            () => raceFs.WriteFileAtomicBytesAsync(
                relativePath,
                [0x34, 0x45, 0x56]));

        Assert.Equal(priorBytes, await File.ReadAllBytesAsync(destinationPath));
        Assert.Equal(
            priorIdentity,
            WindowsHardLinkTestHelper.CaptureIdentity(destinationPath));
        var journalRoot = Path.Combine(
            raceFs.RuntimeRootPath,
            "file-publication-transactions");
        Assert.True(
            !Directory.Exists(journalRoot) ||
            Directory.GetDirectories(journalRoot).Length == 0);
    }

    [Fact]
    public async Task AtomicWrite_CommittedCleanupDebtNeverRollsBackPublishedBytes()
    {
        if (!OperatingSystem.IsWindows())
            return;

        const string relativePath =
            "game_state/world/committed-cleanup-debt.json";
        byte[] priorBytes = [0x14, 0x25, 0x36, 0x47];
        byte[] publishedBytes = [0x58, 0x69, 0x7A, 0x8B];
        await _fs.WriteFileAtomicBytesAsync(relativePath, priorBytes);
        var destinationPath = _fs.ResolvePath(relativePath);
        string? blockedQuarantinePath = null;
        var hooks = FileSystemManagerHookTestHelper.WithPathHook(
            "AfterPhysicalFilePublishedAsync",
            path =>
            {
                if (path.Equals(
                        destinationPath,
                        StringComparison.OrdinalIgnoreCase))
                {
                    var quarantinePath = Assert.Single(
                        Directory.GetFiles(
                            Path.GetDirectoryName(destinationPath)!,
                            ".boe-prior-*.quarantine"));
                    File.SetAttributes(
                        quarantinePath,
                        File.GetAttributes(quarantinePath) |
                        FileAttributes.ReadOnly);
                    blockedQuarantinePath = quarantinePath;
                }

                return Task.CompletedTask;
            });
        var raceFs = new FileSystemManager(
            _rootPath,
            NullLogger<FileSystemManager>.Instance,
            PhysicalLoadTransactionOperations.Instance,
            hooks);
        WindowsHardLinkTestHelper.FileIdentity publishedIdentity;

        try
        {
            await raceFs.WriteFileAtomicBytesAsync(
                relativePath,
                publishedBytes);

            Assert.NotNull(blockedQuarantinePath);
            Assert.Equal(
                publishedBytes,
                await File.ReadAllBytesAsync(destinationPath));
            publishedIdentity =
                WindowsHardLinkTestHelper.CaptureIdentity(destinationPath);
            Assert.NotEmpty(Directory.GetDirectories(
                raceFs.PhysicalPublicationTransactionsRootPath));
        }
        finally
        {
            if (blockedQuarantinePath != null &&
                File.Exists(blockedQuarantinePath))
            {
                File.SetAttributes(
                    blockedQuarantinePath,
                    File.GetAttributes(blockedQuarantinePath) &
                    ~FileAttributes.ReadOnly);
            }
        }

        var recovered = await raceFs.ReadFileBytesAsync(relativePath);

        Assert.Equal(publishedBytes, recovered);
        Assert.Equal(
            publishedIdentity,
            WindowsHardLinkTestHelper.CaptureIdentity(destinationPath));
        Assert.Empty(Directory.GetFiles(
            Path.GetDirectoryName(destinationPath)!,
            ".boe-prior-*.quarantine"));
        Assert.Empty(Directory.EnumerateFileSystemEntries(
            raceFs.PhysicalPublicationTransactionsRootPath));
    }

    [Fact]
    public async Task AtomicWrite_UnsupportedOverwriteFailsBeforeTempJournalOrHook()
    {
        const string relativePath =
            "game_state/world/unsupported-overwrite.json";
        byte[] priorBytes = [0x61, 0x72, 0x83];
        await _fs.WriteFileAtomicBytesAsync(relativePath, priorBytes);
        var hookCount = 0;
        var hooks = FileSystemManagerHookTestHelper.WithBooleanOverride(
            "SupportsReversibleFileReplacementOverride",
            false);
        var beforeMutationProperty = typeof(FileSystemManagerHooks).GetProperty(
            "BeforeCanonicalMutationAsync",
            System.Reflection.BindingFlags.Instance |
            System.Reflection.BindingFlags.NonPublic);
        Assert.NotNull(beforeMutationProperty);
        beforeMutationProperty!.SetValue(
            hooks,
            (Func<string, Task>)(_ =>
            {
                hookCount++;
                return Task.CompletedTask;
            }));
        var raceFs = new FileSystemManager(
            _rootPath,
            NullLogger<FileSystemManager>.Instance,
            PhysicalLoadTransactionOperations.Instance,
            hooks);

        await Assert.ThrowsAsync<PlatformNotSupportedException>(
            () => raceFs.WriteFileAtomicBytesAsync(
                relativePath,
                [0x94, 0xA5, 0xB6]));

        Assert.Equal(0, hookCount);
        Assert.Equal(
            priorBytes,
            await File.ReadAllBytesAsync(_fs.ResolvePath(relativePath)));
        Assert.Empty(Directory.GetFiles(
            Path.GetDirectoryName(_fs.ResolvePath(relativePath))!,
            "*.tmp.*"));
    }

    [Fact]
    public async Task AtomicWrite_UnsupportedBackendStillAllowsCreateOnlyPublication()
    {
        const string relativePath =
            "game_state/world/unsupported-create-only.json";
        byte[] content = [0xC7, 0xD8, 0xE9];
        var hooks = FileSystemManagerHookTestHelper.WithBooleanOverride(
            "SupportsReversibleFileReplacementOverride",
            false);
        var raceFs = new FileSystemManager(
            _rootPath,
            NullLogger<FileSystemManager>.Instance,
            PhysicalLoadTransactionOperations.Instance,
            hooks);

        await raceFs.WriteFileAtomicBytesAsync(relativePath, content);

        Assert.Equal(
            content,
            await File.ReadAllBytesAsync(_fs.ResolvePath(relativePath)));
    }

    [Fact]
    public async Task AtomicWrite_MissingDescriptorCreateOnlyBackendFailsBeforeTempOrHook()
    {
        const string relativePath =
            "game_state/world/missing-descriptor-create-only.json";
        var hookCount = 0;
        var hooks = FileSystemManagerHookTestHelper.WithBooleanOverride(
            "SupportsReversibleFileReplacementOverride",
            false);
        FileSystemManagerHookTestHelper.SetBooleanOverride(
            hooks,
            "SupportsDescriptorBoundCreateOnlyPublicationOverride",
            false);
        FileSystemManagerHookTestHelper.SetPathHook(
            hooks,
            "BeforeCanonicalMutationAsync",
            _ =>
            {
                hookCount++;
                return Task.CompletedTask;
            });
        var raceFs = new FileSystemManager(
            _rootPath,
            NullLogger<FileSystemManager>.Instance,
            PhysicalLoadTransactionOperations.Instance,
            hooks);

        await Assert.ThrowsAsync<PlatformNotSupportedException>(
            () => raceFs.WriteFileAtomicBytesAsync(
                relativePath,
                [0xA1, 0xB2, 0xC3]));

        Assert.Equal(0, hookCount);
        Assert.False(File.Exists(raceFs.ResolvePath(relativePath)));
        Assert.Empty(Directory.GetFiles(
            Path.GetDirectoryName(raceFs.ResolvePath(relativePath))!,
            "*.tmp.*"));
    }

    [Fact]
    public async Task AtomicWrite_DirectoryDestinationFailsBeforeMutationHookOrStaging()
    {
        const string relativePath =
            "game_state/world/directory-publication-destination.json";
        var destinationPath = _fs.ResolvePath(relativePath);
        Directory.CreateDirectory(destinationPath);
        var hookCount = 0;
        var hooks = new FileSystemManagerHooks
        {
            BeforeCanonicalMutationAsync = _ =>
            {
                hookCount++;
                return Task.CompletedTask;
            }
        };
        var raceFs = new FileSystemManager(
            _rootPath,
            NullLogger<FileSystemManager>.Instance,
            PhysicalLoadTransactionOperations.Instance,
            hooks);

        await Assert.ThrowsAsync<InvalidDataException>(
            () => raceFs.WriteFileAtomicBytesAsync(
                relativePath,
                [0xB4, 0xC5, 0xD6]));

        Assert.Equal(0, hookCount);
        Assert.True(Directory.Exists(destinationPath));
        Assert.Empty(Directory.GetFiles(
            Path.GetDirectoryName(destinationPath)!,
            "*.tmp.*"));
    }

    [Fact]
    public async Task AtomicWrite_FileAtIntermediateParentFailsBeforeMutationHook()
    {
        const string parentRelativePath =
            "game_state/world/malformed-publication-parent";
        const string relativePath =
            parentRelativePath + "/destination.json";
        var parentPath = _fs.ResolvePath(parentRelativePath);
        await File.WriteAllTextAsync(parentPath, "not-a-directory");
        var hookCount = 0;
        var hooks = new FileSystemManagerHooks
        {
            BeforeCanonicalMutationAsync = _ =>
            {
                hookCount++;
                return Task.CompletedTask;
            }
        };
        var raceFs = new FileSystemManager(
            _rootPath,
            NullLogger<FileSystemManager>.Instance,
            PhysicalLoadTransactionOperations.Instance,
            hooks);

        await Assert.ThrowsAsync<InvalidDataException>(
            () => raceFs.WriteFileAtomicBytesAsync(
                relativePath,
                [0xE7, 0xF8]));

        Assert.Equal(0, hookCount);
        Assert.Equal(
            "not-a-directory",
            await File.ReadAllTextAsync(parentPath));
    }

    [Fact]
    public async Task AtomicWrite_DanglingDestinationLinkFailsClosedWithoutReplacing()
    {
        if (!OperatingSystem.IsWindows())
            return;

        const string relativePath =
            "game_state/world/dangling-publication-destination.json";
        var destinationPath = _fs.ResolvePath(relativePath);
        var missingTarget = Path.Combine(
            _rootPath,
            "missing-publication-target.json");
        if (!TryCreateFileLink(destinationPath, missingTarget))
            return;

        try
        {
            await Assert.ThrowsAsync<InvalidDataException>(
                () => _fs.WriteFileAtomicBytesAsync(
                    relativePath,
                    [0xD1, 0xE2, 0xF3]));

            Assert.True(
                File.GetAttributes(destinationPath)
                    .HasFlag(FileAttributes.ReparsePoint));
            Assert.Empty(Directory.GetFiles(
                Path.GetDirectoryName(destinationPath)!,
                "*.tmp.*"));
            Assert.False(
                Directory.Exists(_fs.PhysicalPublicationTransactionsRootPath) &&
                Directory.EnumerateFileSystemEntries(
                    _fs.PhysicalPublicationTransactionsRootPath).Any());
        }
        finally
        {
            try
            {
                File.Delete(destinationPath);
            }
            catch (FileNotFoundException)
            {
            }
        }
    }

    [Fact]
    public async Task RuntimeAtomicWrite_UnsupportedOverwriteFailsBeforeTempJournalOrHook()
    {
        string originalGeneration;
        await using (var setupLease =
                     await _fs.AcquireCanonicalWriteLeaseAsync())
        {
            originalGeneration =
                _fs.GetOrCreateSessionGeneration(setupLease);
        }

        var hookCount = 0;
        var hooks = new FileSystemManagerHooks
        {
            SupportsReversibleFileReplacementOverride = false,
            AfterRuntimeMutationBoundaryValidatedAsync = _ =>
            {
                hookCount++;
                return Task.CompletedTask;
            }
        };
        var raceFs = new FileSystemManager(
            _rootPath,
            NullLogger<FileSystemManager>.Instance,
            PhysicalLoadTransactionOperations.Instance,
            hooks);
        await using var lifecycleLease =
            await raceFs.AcquireSessionLifecycleLeaseAsync();
        await using var replacementLease =
            await raceFs.AcquireSessionReplacementWriteLeaseAsync(
                lifecycleLease);

        Assert.Throws<PlatformNotSupportedException>(
            () => raceFs.RotateSessionGeneration(replacementLease));

        Assert.Equal(0, hookCount);
        Assert.Equal(
            originalGeneration,
            raceFs.GetOrCreateSessionGeneration(replacementLease));
        Assert.Empty(Directory.GetFiles(
            Path.GetDirectoryName(raceFs.SessionGenerationPath)!,
            "*.tmp.*"));
    }

    [Fact]
    public async Task RuntimeAtomicWrite_UnsupportedBackendAllowsCreateOnlyPublication()
    {
        var isolatedRoot = Path.Combine(
            Path.GetTempPath(),
            "boe-runtime-create-only-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(isolatedRoot);
        try
        {
            var hooks = new FileSystemManagerHooks
            {
                SupportsReversibleFileReplacementOverride = false
            };
            var fs = new FileSystemManager(
                isolatedRoot,
                NullLogger<FileSystemManager>.Instance,
                PhysicalLoadTransactionOperations.Instance,
                hooks);
            fs.EnsureDirectoryStructure();
            await using var writeLease =
                await fs.AcquireCanonicalWriteLeaseAsync();

            var generation = fs.GetOrCreateSessionGeneration(writeLease);

            Assert.Equal(
                generation,
                fs.GetOrCreateSessionGeneration(writeLease));
        }
        finally
        {
            Directory.Delete(isolatedRoot, recursive: true);
        }
    }

    [Fact]
    public async Task RuntimeAtomicWrite_MissingDescriptorCreateOnlyBackendFailsBeforeTempOrHook()
    {
        var isolatedRoot = Path.Combine(
            Path.GetTempPath(),
            "boe-runtime-no-descriptor-create-only-" +
            Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(isolatedRoot);
        try
        {
            var hookCount = 0;
            var hooks = FileSystemManagerHookTestHelper.WithBooleanOverride(
                "SupportsReversibleFileReplacementOverride",
                false);
            FileSystemManagerHookTestHelper.SetBooleanOverride(
                hooks,
                "SupportsDescriptorBoundCreateOnlyPublicationOverride",
                false);
            FileSystemManagerHookTestHelper.SetPathHook(
                hooks,
                "AfterRuntimeMutationBoundaryValidatedAsync",
                _ =>
                {
                    hookCount++;
                    return Task.CompletedTask;
                });
            var fs = new FileSystemManager(
                isolatedRoot,
                NullLogger<FileSystemManager>.Instance,
                PhysicalLoadTransactionOperations.Instance,
                hooks);
            fs.EnsureDirectoryStructure();
            await using var writeLease =
                await fs.AcquireCanonicalWriteLeaseAsync();

            Assert.Throws<PlatformNotSupportedException>(
                () => fs.GetOrCreateSessionGeneration(writeLease));

            Assert.Equal(0, hookCount);
            Assert.False(File.Exists(fs.SessionGenerationPath));
            var generationParent =
                Path.GetDirectoryName(fs.SessionGenerationPath)!;
            Assert.False(
                Directory.Exists(generationParent) &&
                Directory.GetFiles(generationParent, "*.tmp.*").Length > 0);
        }
        finally
        {
            Directory.Delete(isolatedRoot, recursive: true);
        }
    }

    [Fact]
    public async Task RuntimeAtomicWrite_DirectoryDestinationFailsBeforeMutationHook()
    {
        var hookCount = 0;
        var hooks = new FileSystemManagerHooks
        {
            AfterRuntimeMutationBoundaryValidatedAsync = _ =>
            {
                hookCount++;
                return Task.CompletedTask;
            }
        };
        var raceFs = new FileSystemManager(
            _rootPath,
            NullLogger<FileSystemManager>.Instance,
            PhysicalLoadTransactionOperations.Instance,
            hooks);
        var destinationPath = raceFs.SessionGenerationPath;
        Directory.CreateDirectory(destinationPath);
        await using var writeLease =
            await raceFs.AcquireCanonicalWriteLeaseAsync();

        Assert.Throws<InvalidDataException>(
            () => raceFs.GetOrCreateSessionGeneration(writeLease));

        Assert.Equal(0, hookCount);
        Assert.True(Directory.Exists(destinationPath));
        Assert.Empty(Directory.GetFiles(
            Path.GetDirectoryName(destinationPath)!,
            "*.tmp.*"));
    }

    [Fact]
    public async Task ReadFileBytesAsync_RecoversPublishedUncommittedPublicationBeforeCanonicalRead()
    {
        if (!OperatingSystem.IsWindows())
            return;

        const string relativePath =
            "game_state/world/crash-recovery-uncommitted.json";
        byte[] priorBytes = [0x11, 0x22, 0x33, 0x44];
        byte[] publishedBytes = [0x55, 0x66, 0x77, 0x88];
        await _fs.WriteFileAtomicBytesAsync(relativePath, priorBytes);
        var destinationPath = _fs.ResolvePath(relativePath);
        var priorIdentity =
            WindowsHardLinkTestHelper.CaptureIdentity(destinationPath);
        var transactionId = Guid.NewGuid().ToString("N");
        var sourcePath = destinationPath + ".tmp.crash";
        var quarantinePath = Path.Combine(
            Path.GetDirectoryName(destinationPath)!,
            $".boe-prior-{transactionId}.quarantine");
        var failedSourcePath = Path.Combine(
            Path.GetDirectoryName(destinationPath)!,
            $".boe-source-{transactionId}.evidence");
        await File.WriteAllBytesAsync(sourcePath, publishedBytes);
        var sourceIdentity =
            WindowsHardLinkTestHelper.CaptureIdentity(sourcePath);
        File.Move(destinationPath, quarantinePath);
        File.Move(sourcePath, destinationPath);
        WritePublicationCrashJournal(
            transactionId,
            sourcePath,
            destinationPath,
            quarantinePath,
            failedSourcePath,
            sourceIdentity,
            publishedBytes,
            priorIdentity,
            priorBytes,
            committed: false);

        var recovered = await _fs.ReadFileBytesAsync(relativePath);

        Assert.Equal(priorBytes, recovered);
        Assert.Equal(
            priorIdentity,
            WindowsHardLinkTestHelper.CaptureIdentity(destinationPath));
        Assert.False(File.Exists(quarantinePath));
        Assert.False(Directory.Exists(Path.Combine(
            _fs.PhysicalPublicationTransactionsRootPath,
            transactionId)));
    }

    [Fact]
    public async Task ReadFileBytesAsync_FinalizesCommittedPublicationBeforeCanonicalRead()
    {
        if (!OperatingSystem.IsWindows())
            return;

        const string relativePath =
            "game_state/world/crash-recovery-committed.json";
        byte[] priorBytes = [0x91, 0x82, 0x73];
        byte[] publishedBytes = [0x64, 0x55, 0x46];
        await _fs.WriteFileAtomicBytesAsync(relativePath, priorBytes);
        var destinationPath = _fs.ResolvePath(relativePath);
        var priorIdentity =
            WindowsHardLinkTestHelper.CaptureIdentity(destinationPath);
        var transactionId = Guid.NewGuid().ToString("N");
        var sourcePath = destinationPath + ".tmp.crash";
        var quarantinePath = Path.Combine(
            Path.GetDirectoryName(destinationPath)!,
            $".boe-prior-{transactionId}.quarantine");
        var failedSourcePath = Path.Combine(
            Path.GetDirectoryName(destinationPath)!,
            $".boe-source-{transactionId}.evidence");
        await File.WriteAllBytesAsync(sourcePath, publishedBytes);
        var sourceIdentity =
            WindowsHardLinkTestHelper.CaptureIdentity(sourcePath);
        File.Move(destinationPath, quarantinePath);
        File.Move(sourcePath, destinationPath);
        WritePublicationCrashJournal(
            transactionId,
            sourcePath,
            destinationPath,
            quarantinePath,
            failedSourcePath,
            sourceIdentity,
            publishedBytes,
            priorIdentity,
            priorBytes,
            committed: true);

        var recovered = await _fs.ReadFileBytesAsync(relativePath);

        Assert.Equal(publishedBytes, recovered);
        Assert.Equal(
            sourceIdentity,
            WindowsHardLinkTestHelper.CaptureIdentity(destinationPath));
        Assert.False(File.Exists(quarantinePath));
        Assert.False(Directory.Exists(Path.Combine(
            _fs.PhysicalPublicationTransactionsRootPath,
            transactionId)));
    }

    [Fact]
    public async Task ReadFileBytesAsync_UnknownPublicationIdentityFencesAndRetainsEvidence()
    {
        if (!OperatingSystem.IsWindows())
            return;

        const string relativePath =
            "game_state/world/crash-recovery-unknown.json";
        byte[] priorBytes = [0x09, 0x18, 0x27];
        byte[] publishedBytes = [0x36, 0x45, 0x54];
        byte[] unrelatedBytes = [0x63, 0x72, 0x81];
        await _fs.WriteFileAtomicBytesAsync(relativePath, priorBytes);
        var destinationPath = _fs.ResolvePath(relativePath);
        var priorIdentity =
            WindowsHardLinkTestHelper.CaptureIdentity(destinationPath);
        var transactionId = Guid.NewGuid().ToString("N");
        var sourcePath = destinationPath + ".tmp.crash";
        var quarantinePath = Path.Combine(
            Path.GetDirectoryName(destinationPath)!,
            $".boe-prior-{transactionId}.quarantine");
        var failedSourcePath = Path.Combine(
            Path.GetDirectoryName(destinationPath)!,
            $".boe-source-{transactionId}.evidence");
        await File.WriteAllBytesAsync(sourcePath, publishedBytes);
        var sourceIdentity =
            WindowsHardLinkTestHelper.CaptureIdentity(sourcePath);
        File.Move(destinationPath, quarantinePath);
        File.Move(sourcePath, failedSourcePath);
        await File.WriteAllBytesAsync(destinationPath, unrelatedBytes);
        WritePublicationCrashJournal(
            transactionId,
            sourcePath,
            destinationPath,
            quarantinePath,
            failedSourcePath,
            sourceIdentity,
            publishedBytes,
            priorIdentity,
            priorBytes,
            committed: false);

        await Assert.ThrowsAsync<InvalidDataException>(
            () => _fs.ReadFileBytesAsync(relativePath));

        Assert.Equal(
            unrelatedBytes,
            await File.ReadAllBytesAsync(destinationPath));
        Assert.Equal(priorBytes, await File.ReadAllBytesAsync(quarantinePath));
        Assert.Equal(
            publishedBytes,
            await File.ReadAllBytesAsync(failedSourcePath));
        Assert.True(Directory.Exists(Path.Combine(
            _fs.PhysicalPublicationTransactionsRootPath,
            transactionId)));
    }

    [Fact]
    public async Task PublicationRecovery_DuplicateIntentPropertyRetainsEvidence()
    {
        if (!OperatingSystem.IsWindows())
            return;

        const string relativePath =
            "game_state/world/ambiguous-publication-intent.json";
        byte[] priorBytes = [0x17, 0x28, 0x39];
        byte[] publishedBytes = [0x4A, 0x5B, 0x6C];
        await _fs.WriteFileAtomicBytesAsync(relativePath, priorBytes);
        var destinationPath = _fs.ResolvePath(relativePath);
        var priorIdentity =
            WindowsHardLinkTestHelper.CaptureIdentity(destinationPath);
        var transactionId = Guid.NewGuid().ToString("N");
        var sourcePath = destinationPath + ".tmp.ambiguous";
        var quarantinePath = Path.Combine(
            Path.GetDirectoryName(destinationPath)!,
            $".boe-prior-{transactionId}.quarantine");
        var failedSourcePath = Path.Combine(
            Path.GetDirectoryName(destinationPath)!,
            $".boe-source-{transactionId}.evidence");
        await File.WriteAllBytesAsync(sourcePath, publishedBytes);
        var sourceIdentity =
            WindowsHardLinkTestHelper.CaptureIdentity(sourcePath);
        File.Move(destinationPath, quarantinePath);
        File.Move(sourcePath, destinationPath);
        WritePublicationCrashJournal(
            transactionId,
            sourcePath,
            destinationPath,
            quarantinePath,
            failedSourcePath,
            sourceIdentity,
            publishedBytes,
            priorIdentity,
            priorBytes,
            committed: false);
        var transactionRoot = Path.Combine(
            _fs.PhysicalPublicationTransactionsRootPath,
            transactionId);
        var intentPath = Path.Combine(transactionRoot, "intent.json");
        var intent = await File.ReadAllTextAsync(intentPath);
        intent = intent.Replace(
            "\"authorityName\":\"Crash recovery test\"",
            "\"authorityName\":\"Crash recovery test\",\"AuthorityName\":\"Conflicting authority\"",
            StringComparison.Ordinal);
        await File.WriteAllTextAsync(intentPath, intent);

        await Assert.ThrowsAsync<InvalidDataException>(
            () => _fs.ReadFileBytesAsync(relativePath));

        Assert.Equal(publishedBytes, await File.ReadAllBytesAsync(destinationPath));
        Assert.Equal(priorBytes, await File.ReadAllBytesAsync(quarantinePath));
        Assert.Equal(intent, await File.ReadAllTextAsync(intentPath));
        Assert.True(Directory.Exists(transactionRoot));
    }

    [Fact]
    public async Task PublicationRecovery_DirectoryAtSourceCandidateRetainsEvidence()
    {
        if (!OperatingSystem.IsWindows())
            return;

        const string relativePath =
            "game_state/world/wrong-kind-publication-source.json";
        byte[] priorBytes = [0x1A, 0x2B, 0x3C];
        byte[] publishedBytes = [0x4D, 0x5E, 0x6F];
        await _fs.WriteFileAtomicBytesAsync(relativePath, priorBytes);
        var destinationPath = _fs.ResolvePath(relativePath);
        var priorIdentity =
            WindowsHardLinkTestHelper.CaptureIdentity(destinationPath);
        var transactionId = Guid.NewGuid().ToString("N");
        var sourcePath = destinationPath + ".tmp.wrong-kind";
        var quarantinePath = Path.Combine(
            Path.GetDirectoryName(destinationPath)!,
            $".boe-prior-{transactionId}.quarantine");
        var failedSourcePath = Path.Combine(
            Path.GetDirectoryName(destinationPath)!,
            $".boe-source-{transactionId}.evidence");
        await File.WriteAllBytesAsync(sourcePath, publishedBytes);
        var sourceIdentity =
            WindowsHardLinkTestHelper.CaptureIdentity(sourcePath);
        File.Move(destinationPath, quarantinePath);
        File.Move(sourcePath, destinationPath);
        Directory.CreateDirectory(sourcePath);
        WritePublicationCrashJournal(
            transactionId,
            sourcePath,
            destinationPath,
            quarantinePath,
            failedSourcePath,
            sourceIdentity,
            publishedBytes,
            priorIdentity,
            priorBytes,
            committed: false);
        var transactionRoot = Path.Combine(
            _fs.PhysicalPublicationTransactionsRootPath,
            transactionId);

        await Assert.ThrowsAsync<InvalidDataException>(
            () => _fs.ReadFileBytesAsync(relativePath));

        Assert.Equal(publishedBytes, await File.ReadAllBytesAsync(destinationPath));
        Assert.Equal(priorBytes, await File.ReadAllBytesAsync(quarantinePath));
        Assert.True(Directory.Exists(sourcePath));
        Assert.True(Directory.Exists(transactionRoot));
    }

    [Theory]
    [InlineData("committed.marker")]
    [InlineData("source-published.marker")]
    [InlineData("destination-quarantined.marker")]
    [InlineData("intent.json")]
    public async Task DeferredPublication_CleanupCrashAtEachAuthorityFilePreservesCommit(
        string blockedFileName)
    {
        if (!OperatingSystem.IsWindows())
            return;

        const string relativePath =
            "game_state/world/deferred-cleanup-crash.json";
        byte[] priorBytes = [0x19, 0x2A, 0x3B];
        byte[] publishedBytes = [0x4C, 0x5D, 0x6E];
        await _fs.WriteFileAtomicBytesAsync(relativePath, priorBytes);
        var destinationPath = _fs.ResolvePath(relativePath);
        var parentPath = Path.GetDirectoryName(destinationPath)!;
        var sourcePath = Path.Combine(
            parentPath,
            ".deferred-cleanup-source.tmp");
        string transactionId;

        {
            using var parentAuthority =
                PhysicalFileAuthority.EnsureStableDirectory(
                    _rootPath,
                    parentPath,
                    "Deferred cleanup crash test");
            await using var sourceStream =
                PhysicalFileAuthority.CreateNewWritableFile(
                    parentAuthority,
                    sourcePath,
                    "Deferred cleanup crash source",
                    asynchronous: true);
            await sourceStream.WriteAsync(publishedBytes);
            await sourceStream.FlushAsync();
            sourceStream.Flush(flushToDisk: true);
            using var pending =
                await ReversibleFilePublication.PublishDeferredAsync(
                    _rootPath,
                    _fs.PhysicalPublicationTransactionsRootPath,
                    parentAuthority,
                    sourcePath,
                    sourceStream,
                    parentAuthority,
                    destinationPath,
                    "Deferred cleanup crash test",
                    retainedDestinationHandle: null,
                    afterAuthorityValidated: null,
                    beforeSourcePublished: null,
                    afterPublished: null,
                    CancellationToken.None);
            pending.Commit();
            transactionId = pending.TransactionId;
            var transactionRoot = Path.Combine(
                _fs.PhysicalPublicationTransactionsRootPath,
                transactionId);
            using (var cleanupBlocker = new FileStream(
                       Path.Combine(transactionRoot, blockedFileName),
                       FileMode.Open,
                       FileAccess.Read,
                       FileShare.ReadWrite))
            {
                Assert.False(pending.TryAcknowledgeCommittedJournal());
                var retainedTransactionRoot = Assert.Single(
                    Directory.GetDirectories(
                        _fs.PhysicalPublicationTransactionsRootPath),
                    path => Path.GetFileName(path).Contains(
                        transactionId,
                        StringComparison.Ordinal));
                Assert.True(File.Exists(Path.Combine(
                    retainedTransactionRoot,
                    "intent.json")));
            }
        }

        var recovered = await _fs.ReadFileBytesAsync(relativePath);
        ReversibleFilePublication.AcknowledgeDeferredCommit(
            _rootPath,
            _fs.PhysicalPublicationTransactionsRootPath,
            transactionId);

        Assert.Equal(publishedBytes, recovered);
        Assert.Empty(Directory.EnumerateFileSystemEntries(
            _fs.PhysicalPublicationTransactionsRootPath));
    }

    [Fact]
    public async Task DeferredPublication_CleanupDebtRetainsDirectoryIdentityAcrossRename()
    {
        if (!OperatingSystem.IsWindows())
            return;

        const string relativePath =
            "game_state/world/deferred-cleanup-identity.json";
        byte[] priorBytes = [0x13, 0x24, 0x35];
        byte[] publishedBytes = [0x46, 0x57, 0x68];
        await _fs.WriteFileAtomicBytesAsync(relativePath, priorBytes);
        var destinationPath = _fs.ResolvePath(relativePath);
        var parentPath = Path.GetDirectoryName(destinationPath)!;
        var sourcePath = Path.Combine(
            parentPath,
            ".deferred-cleanup-identity-source.tmp");
        string? cleanupRoot = null;
        string? displacedRoot = null;
        var callbackInvoked = false;
        var swapBlocked = false;
        var swapped = false;
        string transactionId;

        {
            using var parentAuthority =
                PhysicalFileAuthority.EnsureStableDirectory(
                    _rootPath,
                    parentPath,
                    "Deferred cleanup identity test");
            await using var sourceStream =
                PhysicalFileAuthority.CreateNewWritableFile(
                    parentAuthority,
                    sourcePath,
                    "Deferred cleanup identity source",
                    asynchronous: true);
            await sourceStream.WriteAsync(publishedBytes);
            await sourceStream.FlushAsync();
            sourceStream.Flush(flushToDisk: true);
            using var pending =
                await ReversibleFilePublication.PublishDeferredAsync(
                    _rootPath,
                    _fs.PhysicalPublicationTransactionsRootPath,
                    parentAuthority,
                    sourcePath,
                    sourceStream,
                    parentAuthority,
                    destinationPath,
                    "Deferred cleanup identity test",
                    retainedDestinationHandle: null,
                    afterAuthorityValidated: null,
                    beforeSourcePublished: null,
                    afterPublished: null,
                    CancellationToken.None);
            pending.Commit();
            transactionId = pending.TransactionId;
            Assert.True(pending.TryAcknowledgeCommittedJournal(path =>
            {
                callbackInvoked = true;
                cleanupRoot = path;
                displacedRoot = path + ".attacker";
                try
                {
                    Directory.Move(path, displacedRoot);
                    swapped = true;
                    Directory.CreateDirectory(path);
                }
                catch (Exception ex) when (
                    ex is IOException or UnauthorizedAccessException)
                {
                    swapBlocked = true;
                }
            }));
        }

        try
        {
            Assert.True(callbackInvoked);
            Assert.True(
                swapBlocked,
                "The renamed cleanup directory must remain bound to its retained handle.");
            Assert.False(swapped);
        }
        finally
        {
            if (swapped &&
                cleanupRoot != null &&
                displacedRoot != null)
            {
                if (Directory.Exists(cleanupRoot))
                    Directory.Delete(cleanupRoot, recursive: true);
                if (Directory.Exists(displacedRoot))
                    Directory.Move(displacedRoot, cleanupRoot);
            }
        }

        if (Directory.Exists(_fs.PhysicalPublicationTransactionsRootPath) &&
            Directory.EnumerateFileSystemEntries(
                _fs.PhysicalPublicationTransactionsRootPath).Any())
        {
            ReversibleFilePublication.AcknowledgeDeferredCommit(
                _rootPath,
                _fs.PhysicalPublicationTransactionsRootPath,
                transactionId);
        }
        Assert.Equal(publishedBytes, await _fs.ReadFileBytesAsync(relativePath));
    }

    [Fact]
    public async Task ReadFileBytesAsync_AbsentDuringQuarantineWaitsForCommittedPublication()
    {
        if (!OperatingSystem.IsWindows())
            return;

        const string relativePath =
            "game_state/world/quarantine-gap-read.json";
        byte[] priorBytes = [0x71, 0x62, 0x53];
        byte[] publishedBytes = [0x44, 0x35, 0x26];
        await _fs.WriteFileAtomicBytesAsync(relativePath, priorBytes);
        var destinationPath = _fs.ResolvePath(relativePath);
        var readerAtOpen = NewBarrier();
        var allowReaderOpen = NewBarrier();
        var writerAtQuarantine = NewBarrier();
        var allowWriterCommit = NewBarrier();
        var readerObservedAbsence = NewBarrier();
        var writerCompleted = NewBarrier();
        var hooks = new FileSystemManagerHooks();
        FileSystemManagerHookTestHelper.SetPathHook(
            hooks,
            "BeforeCanonicalReadOpenAsync",
            async path =>
            {
                if (!path.Equals(
                        relativePath,
                        StringComparison.OrdinalIgnoreCase))
                    return;
                readerAtOpen.TrySetResult();
                await allowReaderOpen.Task.WaitAsync(TimeSpan.FromSeconds(10));
            });
        FileSystemManagerHookTestHelper.SetPathHook(
            hooks,
            "BeforePhysicalSourcePublishedAsync",
            async path =>
            {
                if (!path.Equals(
                        destinationPath,
                        StringComparison.OrdinalIgnoreCase))
                    return;
                writerAtQuarantine.TrySetResult();
                await allowWriterCommit.Task.WaitAsync(
                    TimeSpan.FromSeconds(10));
            });
        FileSystemManagerHookTestHelper.SetPathHook(
            hooks,
            "AfterCanonicalReadAttemptAsync",
            async path =>
            {
                if (!path.Equals(
                        relativePath,
                        StringComparison.OrdinalIgnoreCase))
                    return;
                readerObservedAbsence.TrySetResult();
                await writerCompleted.Task.WaitAsync(
                    TimeSpan.FromSeconds(10));
            });
        var raceFs = new FileSystemManager(
            _rootPath,
            NullLogger<FileSystemManager>.Instance,
            PhysicalLoadTransactionOperations.Instance,
            hooks);

        var readTask = raceFs.ReadFileBytesAsync(relativePath);
        await readerAtOpen.Task.WaitAsync(TimeSpan.FromSeconds(10));
        var writeTask = raceFs.WriteFileAtomicBytesAsync(
            relativePath,
            publishedBytes);
        await writerAtQuarantine.Task.WaitAsync(TimeSpan.FromSeconds(10));
        allowReaderOpen.TrySetResult();
        await readerObservedAbsence.Task.WaitAsync(TimeSpan.FromSeconds(10));
        allowWriterCommit.TrySetResult();
        try
        {
            await writeTask;
            writerCompleted.TrySetResult();
        }
        catch (Exception ex)
        {
            writerCompleted.TrySetException(ex);
            throw;
        }

        var recovered = await readTask;

        Assert.Equal(publishedBytes, recovered);
        Assert.Empty(Directory.EnumerateFileSystemEntries(
            raceFs.PhysicalPublicationTransactionsRootPath));
    }

    [Fact]
    public void FileExists_FilePublishedAfterInitialAbsentProbe_RechecksUnderQuiescenceLease()
    {
        const string relativePath =
            "game_state/world/file-exists-publication-gap.json";
        var published = false;
        var hooks = new FileSystemManagerHooks
        {
            AfterCanonicalReadAttemptAsync = async path =>
            {
                if (published ||
                    !path.Equals(relativePath, StringComparison.OrdinalIgnoreCase))
                {
                    return;
                }

                published = true;
                await _fs.WriteFileAtomicBytesAsync(relativePath, [0x41]);
            }
        };
        var raceFs = new FileSystemManager(
            _rootPath,
            NullLogger<FileSystemManager>.Instance,
            PhysicalLoadTransactionOperations.Instance,
            hooks);

        Assert.True(raceFs.FileExists(relativePath));
        Assert.True(published);
    }

    [Fact]
    public async Task FileExists_RuntimeSavePublicationWaitsForLeaseBeforeReportingAbsence()
    {
        const string destinationRelativePath =
            "saves/manual_saves/pre-journal-save.zip";
        var writerAtBoundary = NewBarrier();
        var allowPublication = NewBarrier();
        var readerContended = NewBarrier();
        var hooks = new FileSystemManagerHooks
        {
            CanonicalWriteLockContendedAsync = () =>
            {
                readerContended.TrySetResult();
                return Task.CompletedTask;
            },
            AfterCanonicalMutationBoundaryValidatedAsync = async path =>
            {
                if (!path.Equals(
                        destinationRelativePath,
                        StringComparison.OrdinalIgnoreCase))
                {
                    return;
                }

                writerAtBoundary.TrySetResult();
                await allowPublication.Task.WaitAsync(
                    TimeSpan.FromSeconds(10));
            }
        };
        var raceFs = new FileSystemManager(
            _rootPath,
            NullLogger<FileSystemManager>.Instance,
            PhysicalLoadTransactionOperations.Instance,
            hooks);
        var stagingRoot = raceFs.CreateRuntimeSaveStagingRoot();
        await using var staged = await raceFs.CreateRuntimeStagedFileAsync(
            Path.Combine(stagingRoot, "pre-journal-save.zip"));
        await staged.Stream.WriteAsync(
            new byte[] { 0x50, 0x4B, 0x05, 0x06 });
        Task<bool> readerTask;
        Task first;
        await using (var writeLease =
                     await raceFs.AcquireCanonicalWriteLeaseAsync())
        {
            var publicationTask =
                raceFs.MoveRuntimeFileIntoCanonicalSessionAsync(
                    writeLease,
                    staged,
                    destinationRelativePath);
            await writerAtBoundary.Task.WaitAsync(
                TimeSpan.FromSeconds(10));

            using (ExecutionContext.SuppressFlow())
            {
                readerTask = Task.Run(
                    () => raceFs.FileExists(
                        destinationRelativePath));
            }

            first = await Task.WhenAny(
                    readerTask,
                    readerContended.Task)
                .WaitAsync(TimeSpan.FromSeconds(10));
            allowPublication.TrySetResult();
            await publicationTask;
        }

        Assert.Same(readerContended.Task, first);
        Assert.True(await readerTask);
    }

    [Fact]
    public async Task FileExists_RuntimeDirectoryPublicationWaitsForLeaseBeforeClassifyingDestination()
    {
        const string destinationRelativePath =
            "game_state/control/pre-journal-proposal";
        var writerAtBoundary = NewBarrier();
        var allowPublication = NewBarrier();
        var readerContended = NewBarrier();
        var hooks = new FileSystemManagerHooks
        {
            CanonicalWriteLockContendedAsync = () =>
            {
                readerContended.TrySetResult();
                return Task.CompletedTask;
            },
            AfterCanonicalMutationBoundaryValidatedAsync = async path =>
            {
                if (!path.Equals(
                        destinationRelativePath,
                        StringComparison.OrdinalIgnoreCase))
                {
                    return;
                }

                writerAtBoundary.TrySetResult();
                await allowPublication.Task.WaitAsync(
                    TimeSpan.FromSeconds(10));
            }
        };
        var raceFs = new FileSystemManager(
            _rootPath,
            NullLogger<FileSystemManager>.Instance,
            PhysicalLoadTransactionOperations.Instance,
            hooks);
        var stagingRoot = raceFs.CreateRuntimeProposalStagingRoot();
        var sourceDirectory = Path.Combine(
            stagingRoot,
            "pre-journal-proposal");
        Directory.CreateDirectory(sourceDirectory);
        await File.WriteAllTextAsync(
            Path.Combine(sourceDirectory, "proposal.json"),
            "{}");
        Task<Exception?> readerTask;
        Task first;
        await using (var writeLease =
                     await raceFs.AcquireCanonicalWriteLeaseAsync())
        {
            var publicationTask =
                raceFs.MoveRuntimeDirectoryIntoCanonicalSessionAsync(
                    writeLease,
                    sourceDirectory,
                    destinationRelativePath);
            await writerAtBoundary.Task.WaitAsync(
                TimeSpan.FromSeconds(10));

            using (ExecutionContext.SuppressFlow())
            {
                readerTask = Task.Run<Exception?>(
                    () => Record.Exception(
                        () => raceFs.FileExists(
                            destinationRelativePath)));
            }

            first = await Task.WhenAny(
                    readerTask,
                    readerContended.Task)
                .WaitAsync(TimeSpan.FromSeconds(10));
            allowPublication.TrySetResult();
            await publicationTask;
        }

        Assert.Same(readerContended.Task, first);
        Assert.IsType<InvalidDataException>(await readerTask);
    }

    [Fact]
    public void FileExists_DirectoryAtFileAuthorityFailsClosed()
    {
        const string relativePath = "input/turn_request.json";
        Directory.CreateDirectory(_fs.ResolvePath(relativePath));

        Assert.Throws<InvalidDataException>(
            () => _fs.FileExists(relativePath));
    }

    [Fact]
    public void BrowserPendingTurnInspector_DanglingFileLinkFailsClosed()
    {
        if (!OperatingSystem.IsWindows())
            return;

        var linkPath = _fs.ResolvePath(
            BrowserPendingTurnInspector.TurnRequestPath);
        var missingTarget = Path.Combine(
            _rootPath,
            "missing-turn-request-target.json");
        if (!TryCreateFileLink(linkPath, missingTarget))
            return;

        try
        {
            Assert.Throws<InvalidDataException>(
                () => BrowserPendingTurnInspector.Build(_fs));
        }
        finally
        {
            try
            {
                File.Delete(linkPath);
            }
            catch (FileNotFoundException)
            {
            }
        }
    }

    [Fact]
    public void BrowserPendingTurnInspector_RegularFileAtDirectoryArtifactFailsClosed()
    {
        var artifactPath = _fs.ResolvePath(
            BrowserPendingTurnInspector.PendingTurnSnapshotDirectory);
        File.WriteAllText(artifactPath, "not-a-directory");

        Assert.Throws<InvalidDataException>(
            () => BrowserPendingTurnInspector.Build(_fs));

        Assert.Equal("not-a-directory", File.ReadAllText(artifactPath));
    }

    [Fact]
    public async Task FileExists_OwnedLeaseWithPendingPublicationDoesNotReacquire()
    {
        if (!OperatingSystem.IsWindows())
            return;

        const string publicationPath =
            "game_state/world/file-exists-owned-publication.json";
        const string probePath =
            "game_state/world/file-exists-owned-probe.json";
        await _fs.WriteFileAtomicBytesAsync(publicationPath, [0x10]);
        await _fs.WriteFileAtomicBytesAsync(probePath, [0x20]);
        var destinationPath = _fs.ResolvePath(publicationPath);
        var lockOpenCount = 0;
        FileSystemManager? raceFs = null;
        var hooks = new FileSystemManagerHooks
        {
            BeforeCanonicalWriteLockOpenAsync = () =>
            {
                if (Interlocked.Increment(ref lockOpenCount) > 1)
                {
                    throw new InvalidOperationException(
                        "Canonical write lock was reacquired by its owner.");
                }

                return Task.CompletedTask;
            },
            BeforePhysicalSourcePublishedAsync = path =>
            {
                if (path.Equals(
                        destinationPath,
                        StringComparison.OrdinalIgnoreCase))
                {
                    Assert.NotNull(raceFs);
                    Assert.True(raceFs.FileExists(probePath));
                }

                return Task.CompletedTask;
            }
        };
        raceFs = new FileSystemManager(
            _rootPath,
            NullLogger<FileSystemManager>.Instance,
            PhysicalLoadTransactionOperations.Instance,
            hooks);

        await raceFs.WriteFileAtomicBytesAsync(publicationPath, [0x30]);

        Assert.Equal(1, Volatile.Read(ref lockOpenCount));
        Assert.Equal([0x30], await raceFs.ReadFileBytesAsync(publicationPath));
    }

    [Fact]
    public void FileExists_AbsentTargetWithoutPublication_DoesNotAcquireCanonicalWriteLease()
    {
        var lockOpenCount = 0;
        var hooks = new FileSystemManagerHooks
        {
            BeforeCanonicalWriteLockOpenAsync = () =>
            {
                Interlocked.Increment(ref lockOpenCount);
                return Task.CompletedTask;
            }
        };
        var raceFs = new FileSystemManager(
            _rootPath,
            NullLogger<FileSystemManager>.Instance,
            PhysicalLoadTransactionOperations.Instance,
            hooks);

        Assert.False(raceFs.FileExists(
            "game_state/control/absent-repair-signal.json"));
        Assert.Equal(0, Volatile.Read(ref lockOpenCount));
    }

    [Fact]
    public async Task FileExists_WriterRegistersInsideFollowUpProbeGap_WaitsForPublication()
    {
        const string relativePath =
            "game_state/control/exact-registration-race.json";
        var readerAtGap = NewBarrier();
        var allowFollowUpProbe = NewBarrier();
        var writerRegistered = NewBarrier();
        var allowPublication = NewBarrier();
        var readerContended = NewBarrier();
        var hooks = new FileSystemManagerHooks
        {
            BeforeCanonicalExistenceFollowUpProbeAsync = async path =>
            {
                if (!path.Equals(relativePath, StringComparison.OrdinalIgnoreCase))
                    return;

                readerAtGap.TrySetResult();
                await allowFollowUpProbe.Task.WaitAsync(
                    TimeSpan.FromSeconds(10));
            },
            BeforeCanonicalMutationBoundaryAsync = async path =>
            {
                if (!path.Equals(relativePath, StringComparison.OrdinalIgnoreCase))
                    return;

                writerRegistered.TrySetResult();
                await allowPublication.Task.WaitAsync(
                    TimeSpan.FromSeconds(10));
            },
            CanonicalWriteLockContendedAsync = () =>
            {
                readerContended.TrySetResult();
                return Task.CompletedTask;
            }
        };
        var raceFs = new FileSystemManager(
            _rootPath,
            NullLogger<FileSystemManager>.Instance,
            PhysicalLoadTransactionOperations.Instance,
            hooks);
        Task<bool> readerTask;
        using (ExecutionContext.SuppressFlow())
        {
            readerTask = Task.Run(() => raceFs.FileExists(relativePath));
        }

        await readerAtGap.Task.WaitAsync(TimeSpan.FromSeconds(10));
        var writerTask = raceFs.WriteFileAtomicBytesAsync(
            relativePath,
            [0x71, 0x82]);
        await writerRegistered.Task.WaitAsync(TimeSpan.FromSeconds(10));
        allowFollowUpProbe.TrySetResult();

        var first = await Task.WhenAny(
                readerTask,
                readerContended.Task)
            .WaitAsync(TimeSpan.FromSeconds(10));
        allowPublication.TrySetResult();
        await writerTask;

        Assert.Same(readerContended.Task, first);
        Assert.True(await readerTask);
    }

    [Fact]
    public async Task AcquireCanonicalWriteLeaseAsync_DoesNotClaimAmbientOwnershipBeforeLockOpens()
    {
        var firstLockAttempted = NewBarrier();
        var allowFirstLockAttempt = NewBarrier();
        var lockOpenCount = 0;
        var hooks = new FileSystemManagerHooks
        {
            BeforeCanonicalWriteLockOpenAsync = async () =>
            {
                if (Interlocked.Increment(ref lockOpenCount) == 1)
                {
                    firstLockAttempted.TrySetResult();
                    await allowFirstLockAttempt.Task.WaitAsync(
                        TimeSpan.FromSeconds(10));
                    return;
                }

                throw new InvalidOperationException(
                    "Absent read sought canonical quiescence.");
            }
        };
        var raceFs = new FileSystemManager(
            _rootPath,
            NullLogger<FileSystemManager>.Instance,
            PhysicalLoadTransactionOperations.Instance,
            hooks);
        var pendingLease = raceFs.AcquireCanonicalWriteLeaseAsync();
        await firstLockAttempted.Task.WaitAsync(TimeSpan.FromSeconds(10));

        try
        {
            var exception = await Assert.ThrowsAsync<InvalidOperationException>(
                () => raceFs.ReadFileBytesAsync(
                    "game_state/world/ambient-before-open.json"));
            Assert.Contains(
                "sought canonical quiescence",
                exception.Message,
                StringComparison.Ordinal);
        }
        finally
        {
            allowFirstLockAttempt.TrySetResult();
            await using var lease = await pendingLease;
            Assert.Equal(1, GetAmbientCanonicalLeaseDepth(raceFs));
            Assert.Equal(1, GetActiveAmbientCanonicalLeaseCount(raceFs));
        }
    }

    [Fact]
    public async Task AcquireCanonicalWriteLeaseAsync_PendingRegistrationSurvivesCallerSideObservation()
    {
        var firstLockAttempted = NewBarrier();
        var allowFirstLockAttempt = NewBarrier();
        var hooks = new FileSystemManagerHooks
        {
            BeforeCanonicalWriteLockOpenAsync = async () =>
            {
                firstLockAttempted.TrySetResult();
                await allowFirstLockAttempt.Task.WaitAsync(
                    TimeSpan.FromSeconds(10));
            }
        };
        var raceFs = new FileSystemManager(
            _rootPath,
            NullLogger<FileSystemManager>.Instance,
            PhysicalLoadTransactionOperations.Instance,
            hooks);
        var pendingLease = raceFs.AcquireCanonicalWriteLeaseAsync();
        await firstLockAttempted.Task.WaitAsync(TimeSpan.FromSeconds(10));

        try
        {
            Assert.False(raceFs.FileExists(
                "game_state/world/pending-ambient-observation.json"));
        }
        finally
        {
            allowFirstLockAttempt.TrySetResult();
        }

        await using var lease = await pendingLease;
        Assert.Equal(1, GetAmbientCanonicalLeaseDepth(raceFs));
        Assert.Equal(1, GetActiveAmbientCanonicalLeaseCount(raceFs));
    }

    [Fact]
    public async Task AcquireCanonicalWriteLeaseAsync_CancelledContentionDoesNotAccumulateAmbientRegistrations()
    {
        var raceFs = new FileSystemManager(
            _rootPath,
            NullLogger<FileSystemManager>.Instance);
        var heldLease = await raceFs.AcquireCanonicalWriteLeaseAsync();
        try
        {
            for (var attempt = 0; attempt < 5; attempt++)
            {
                using var cancellation = new CancellationTokenSource(
                    TimeSpan.FromMilliseconds(100));
                await Assert.ThrowsAnyAsync<OperationCanceledException>(
                    () => raceFs.AcquireCanonicalWriteLeaseAsync(
                        cancellationToken: cancellation.Token));
            }

            Assert.Equal(1, GetAmbientCanonicalLeaseDepth(raceFs));
            Assert.Equal(1, GetActiveAmbientCanonicalLeaseCount(raceFs));
        }
        finally
        {
            await heldLease.DisposeAsync();
        }

        Assert.False(raceFs.FileExists(
            "game_state/world/ambient-registration-cleanup.json"));
        Assert.Equal(0, GetAmbientCanonicalLeaseDepth(raceFs));
    }

    [Fact]
    public async Task AcquireCanonicalWriteLeaseAsync_OverlappingCancelledContentionPrunesInactiveInteriorRegistrations()
    {
        var raceFs = new FileSystemManager(
            _rootPath,
            NullLogger<FileSystemManager>.Instance);
        var heldLease = await raceFs.AcquireCanonicalWriteLeaseAsync();
        CancellationTokenSource? pendingCancellation = null;
        Task<FileSystemManager.CanonicalWriteLease>? pendingLease = null;
        try
        {
            for (var attempt = 0; attempt < 8; attempt++)
            {
                var nextCancellation = new CancellationTokenSource();
                var nextLease = raceFs.AcquireCanonicalWriteLeaseAsync(
                    cancellationToken: nextCancellation.Token);

                if (pendingCancellation != null && pendingLease != null)
                {
                    pendingCancellation.Cancel();
                    await Assert.ThrowsAnyAsync<OperationCanceledException>(
                        () => pendingLease);
                    pendingCancellation.Dispose();
                }

                pendingCancellation = nextCancellation;
                pendingLease = nextLease;
                Assert.InRange(
                    GetAmbientCanonicalLeaseDepth(raceFs),
                    low: 2,
                    high: 3);
                Assert.Equal(
                    1,
                    GetActiveAmbientCanonicalLeaseCount(raceFs));
            }
        }
        finally
        {
            if (pendingCancellation != null && pendingLease != null)
            {
                pendingCancellation.Cancel();
                await Assert.ThrowsAnyAsync<OperationCanceledException>(
                    () => pendingLease);
                pendingCancellation.Dispose();
            }

            await heldLease.DisposeAsync();
        }

        Assert.False(raceFs.FileExists(
            "game_state/world/ambient-overlap-cleanup.json"));
        Assert.Equal(0, GetAmbientCanonicalLeaseDepth(raceFs));
    }

    [Fact]
    public void OpenExactPhysicalReadFile_RejectsHardLinkedSaveArchive()
    {
        if (!OperatingSystem.IsWindows())
            return;

        var externalPath = Path.Combine(_rootPath, "external-save.zip");
        var savePath = _fs.ResolvePath("saves/manual_saves/hard-linked-save.zip");
        File.WriteAllBytes(externalPath, [0x50, 0x4B, 0x05, 0x06]);
        CreateHardLink(savePath, externalPath);

        Assert.Throws<InvalidDataException>(
            () => _fs.OpenExactPhysicalReadFile(
                savePath,
                "Selected save archive"));

        Assert.Equal(
            [0x50, 0x4B, 0x05, 0x06],
            File.ReadAllBytes(externalPath));
    }

    [Fact]
    public async Task DeleteFile_RejectsHardLinkedCanonicalState()
    {
        if (!OperatingSystem.IsWindows())
            return;

        const string relativePath = "game_state/world/hard-linked-delete.json";
        var canonicalPath = _fs.ResolvePath(relativePath);
        var externalPath = Path.Combine(_rootPath, "external-delete.json");
        byte[] externalBytes = [0x10, 0x20, 0x30];
        await File.WriteAllBytesAsync(externalPath, externalBytes);
        CreateHardLink(canonicalPath, externalPath);

        Assert.Throws<InvalidDataException>(() => _fs.DeleteFile(relativePath));

        Assert.True(File.Exists(canonicalPath));
        Assert.Equal(externalBytes, await File.ReadAllBytesAsync(externalPath));
    }

    [Fact]
    public async Task RestoreBackup_RejectsBackupOutsideCanonicalSession()
    {
        const string originalPath = "game_state/world/restore-target.json";
        var outsideBackupPath = Path.Combine(_rootPath, "outside-restore.backup");
        await _fs.WriteFileAtomicAsync(originalPath, "{\"marker\":\"canonical\"}");
        await File.WriteAllTextAsync(outsideBackupPath, "{\"marker\":\"external\"}");

        Assert.Throws<InvalidDataException>(
            () => _fs.RestoreBackup(outsideBackupPath, originalPath));

        Assert.Contains(
            "canonical",
            await _fs.ReadFileAsync(originalPath),
            StringComparison.Ordinal);
        Assert.True(File.Exists(outsideBackupPath));
    }

    [Fact]
    public async Task RestoreBackup_MissingBeforeImageFailsClosed()
    {
        const string originalPath = "game_state/world/missing-before-image.json";
        await _fs.WriteFileAtomicAsync(originalPath, "{\"value\":\"before\"}");
        var backupPath = Assert.IsType<string>(_fs.CreateBackup(originalPath));
        await _fs.WriteFileAtomicAsync(originalPath, "{\"value\":\"rejected\"}");
        File.Delete(backupPath);

        Assert.Throws<FileNotFoundException>(
            () => _fs.RestoreBackup(backupPath, originalPath));

        Assert.Equal(
            "{\"value\":\"rejected\"}",
            await _fs.ReadFileAsync(originalPath));
    }

    [Fact]
    public async Task AtomicWrite_ParentSwapAfterFinalValidationIsBlocked()
    {
        if (!OperatingSystem.IsWindows())
            return;

        const string relativePath = "game_state/meta/post-validation-write.bin";
        var raceRoot = Path.Combine(
            Path.GetTempPath(),
            "boe-post-validation-write-" + Guid.NewGuid().ToString("N"));
        var outsideRoot = Path.Combine(
            Path.GetTempPath(),
            "boe-post-validation-write-outside-" + Guid.NewGuid().ToString("N"));
        var canonicalMeta = Path.Combine(raceRoot, "game_session", "game_state", "meta");
        var displacedMeta = canonicalMeta + "-displaced";
        var outsideTarget = Path.Combine(outsideRoot, "post-validation-write.bin");
        var armed = false;
        var swapAttempted = false;
        var swapBlocked = false;
        var swapped = false;
        var hooks = new FileSystemManagerHooks
        {
            AfterCanonicalMutationBoundaryValidatedAsync = path =>
            {
                if (!armed ||
                    swapAttempted ||
                    !path.Equals(relativePath, StringComparison.OrdinalIgnoreCase))
                {
                    return Task.CompletedTask;
                }

                swapAttempted = true;
                try
                {
                    Directory.Move(canonicalMeta, displacedMeta);
                    CreateDirectoryJunction(canonicalMeta, outsideRoot);
                    swapped = true;

                    var displacedTemp = Assert.Single(
                        Directory.EnumerateFiles(
                            displacedMeta,
                            "post-validation-write.bin.tmp.*"));
                    File.WriteAllBytes(
                        Path.Combine(outsideRoot, Path.GetFileName(displacedTemp)),
                        [0xEE]);
                }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
                {
                    swapBlocked = true;
                }

                return Task.CompletedTask;
            }
        };
        Directory.CreateDirectory(raceRoot);
        Directory.CreateDirectory(outsideRoot);
        await File.WriteAllBytesAsync(outsideTarget, [0xA1, 0xB2]);

        try
        {
            var fs = new FileSystemManager(
                raceRoot,
                NullLogger<FileSystemManager>.Instance,
                PhysicalLoadTransactionOperations.Instance,
                hooks);
            fs.EnsureDirectoryStructure();
            await fs.WriteFileAtomicBytesAsync(relativePath, [0x11]);
            armed = true;

            await fs.WriteFileAtomicBytesAsync(relativePath, [0x22, 0x33]);

            Assert.True(swapAttempted);
            Assert.True(swapBlocked);
            Assert.False(swapped);
            Assert.Equal(
                new byte[] { 0xA1, 0xB2 },
                await File.ReadAllBytesAsync(outsideTarget));
            Assert.Equal(
                new byte[] { 0x22, 0x33 },
                await File.ReadAllBytesAsync(Path.Combine(
                    canonicalMeta,
                    "post-validation-write.bin")));
        }
        finally
        {
            if (Directory.Exists(canonicalMeta) &&
                (File.GetAttributes(canonicalMeta) & FileAttributes.ReparsePoint) != 0)
            {
                Directory.Delete(canonicalMeta, recursive: false);
            }

            if (Directory.Exists(displacedMeta) && !Directory.Exists(canonicalMeta))
                Directory.Move(displacedMeta, canonicalMeta);
            if (Directory.Exists(raceRoot))
                Directory.Delete(raceRoot, recursive: true);
            if (Directory.Exists(outsideRoot))
                Directory.Delete(outsideRoot, recursive: true);
        }
    }

    [Fact]
    public async Task DeleteFile_ParentSwapAfterFinalValidationIsBlocked()
    {
        if (!OperatingSystem.IsWindows())
            return;

        const string relativePath = "game_state/meta/post-validation-delete.bin";
        var raceRoot = Path.Combine(
            Path.GetTempPath(),
            "boe-post-validation-delete-" + Guid.NewGuid().ToString("N"));
        var outsideRoot = Path.Combine(
            Path.GetTempPath(),
            "boe-post-validation-delete-outside-" + Guid.NewGuid().ToString("N"));
        var canonicalMeta = Path.Combine(raceRoot, "game_session", "game_state", "meta");
        var displacedMeta = canonicalMeta + "-displaced";
        var outsideTarget = Path.Combine(outsideRoot, "post-validation-delete.bin");
        var armed = false;
        var swapAttempted = false;
        var swapBlocked = false;
        var swapped = false;
        var hooks = new FileSystemManagerHooks
        {
            AfterCanonicalMutationBoundaryValidatedAsync = path =>
            {
                if (!armed ||
                    swapAttempted ||
                    !path.Equals(relativePath, StringComparison.OrdinalIgnoreCase))
                {
                    return Task.CompletedTask;
                }

                swapAttempted = true;
                try
                {
                    Directory.Move(canonicalMeta, displacedMeta);
                    CreateDirectoryJunction(canonicalMeta, outsideRoot);
                    swapped = true;
                }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
                {
                    swapBlocked = true;
                }

                return Task.CompletedTask;
            }
        };
        Directory.CreateDirectory(raceRoot);
        Directory.CreateDirectory(outsideRoot);
        await File.WriteAllBytesAsync(outsideTarget, [0xC3, 0xD4]);

        try
        {
            var fs = new FileSystemManager(
                raceRoot,
                NullLogger<FileSystemManager>.Instance,
                PhysicalLoadTransactionOperations.Instance,
                hooks);
            fs.EnsureDirectoryStructure();
            await fs.WriteFileAtomicBytesAsync(relativePath, [0x55]);
            armed = true;

            fs.DeleteFile(relativePath);

            Assert.True(swapAttempted);
            Assert.True(swapBlocked);
            Assert.False(swapped);
            Assert.Equal(
                new byte[] { 0xC3, 0xD4 },
                await File.ReadAllBytesAsync(outsideTarget));
            Assert.False(File.Exists(Path.Combine(
                canonicalMeta,
                "post-validation-delete.bin")));
        }
        finally
        {
            if (Directory.Exists(canonicalMeta) &&
                (File.GetAttributes(canonicalMeta) & FileAttributes.ReparsePoint) != 0)
            {
                Directory.Delete(canonicalMeta, recursive: false);
            }

            if (Directory.Exists(displacedMeta) && !Directory.Exists(canonicalMeta))
                Directory.Move(displacedMeta, canonicalMeta);
            if (Directory.Exists(raceRoot))
                Directory.Delete(raceRoot, recursive: true);
            if (Directory.Exists(outsideRoot))
                Directory.Delete(outsideRoot, recursive: true);
        }
    }

    [Fact]
    public async Task RuntimeDirectoryMove_ParentSwapAfterFinalValidationIsBlocked()
    {
        if (!OperatingSystem.IsWindows())
            return;

        const string destination = "worker_proposals/post-validation-proposal";
        var proposalRoot = _fs.ResolvePath("worker_proposals");
        var displacedProposalRoot = _fs.ResolvePath("worker-proposals-displaced");
        var outsideRoot = Path.Combine(
            _rootPath,
            "post-validation-proposal-outside");
        Directory.CreateDirectory(proposalRoot);
        Directory.CreateDirectory(outsideRoot);
        var stagingRoot = _fs.CreateRuntimeProposalStagingRoot();
        await File.WriteAllTextAsync(
            Path.Combine(stagingRoot, "proposal.json"),
            """{"status":"completed"}""");

        var armed = false;
        var swapAttempted = false;
        var swapBlocked = false;
        var swapped = false;
        var hooks = new FileSystemManagerHooks
        {
            AfterCanonicalMutationBoundaryValidatedAsync = path =>
            {
                if (!armed ||
                    swapAttempted ||
                    !path.Equals(destination, StringComparison.OrdinalIgnoreCase))
                {
                    return Task.CompletedTask;
                }

                swapAttempted = true;
                try
                {
                    Directory.Move(proposalRoot, displacedProposalRoot);
                    CreateDirectoryJunction(proposalRoot, outsideRoot);
                    swapped = true;
                }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
                {
                    swapBlocked = true;
                }

                return Task.CompletedTask;
            }
        };
        var raceFs = new FileSystemManager(
            _rootPath,
            NullLogger<FileSystemManager>.Instance,
            PhysicalLoadTransactionOperations.Instance,
            hooks);

        try
        {
            await using var writeLease = await raceFs.AcquireCanonicalWriteLeaseAsync();
            armed = true;
            await raceFs.MoveRuntimeDirectoryIntoCanonicalSessionAsync(
                writeLease,
                stagingRoot,
                destination);

            Assert.True(swapAttempted);
            Assert.True(swapBlocked);
            Assert.False(swapped);
            Assert.False(Directory.Exists(Path.Combine(
                outsideRoot,
                "post-validation-proposal")));
            Assert.True(File.Exists(raceFs.ResolvePath(
                destination + "/proposal.json")));
        }
        finally
        {
            if (Directory.Exists(proposalRoot) &&
                (File.GetAttributes(proposalRoot) & FileAttributes.ReparsePoint) != 0)
            {
                Directory.Delete(proposalRoot, recursive: false);
            }

            if (Directory.Exists(displacedProposalRoot) &&
                !Directory.Exists(proposalRoot))
            {
                Directory.Move(displacedProposalRoot, proposalRoot);
            }

            if (Directory.Exists(outsideRoot))
                Directory.Delete(outsideRoot, recursive: true);
        }
    }

    [Fact]
    public async Task SessionGeneration_SwapBackReadCannotAuthorizeExternalGeneration()
    {
        if (!OperatingSystem.IsWindows())
            return;

        string canonicalGeneration;
        await using (var setupLease = await _fs.AcquireCanonicalWriteLeaseAsync())
            canonicalGeneration = _fs.GetOrCreateSessionGeneration(setupLease);

        var externalGeneration = Guid.NewGuid().ToString("N");
        var generationRoot = Path.GetDirectoryName(_fs.SessionGenerationPath)!;
        var displacedGenerationPath = _fs.SessionGenerationPath + ".displaced";
        var outsideRoot = Path.Combine(_rootPath, "generation-swap-back-outside");
        var externalGenerationPath = Path.Combine(outsideRoot, "current.json");
        var probeLink = Path.Combine(outsideRoot, "file-link-probe.json");
        Directory.CreateDirectory(outsideRoot);
        await File.WriteAllTextAsync(
            externalGenerationPath,
            JsonSerializer.Serialize(new
            {
                schemaVersion = 1,
                generationId = externalGeneration
            }));
        if (!TryCreateFileLink(probeLink, externalGenerationPath))
            return;
        File.Delete(probeLink);

        var armed = false;
        var swapped = false;
        var restored = false;
        var openedHandleHeldDuringHook = false;
        var hooks = new FileSystemManagerHooks
        {
            BeforeRuntimeFileReadOpenAsync = path =>
            {
                if (!armed ||
                    swapped ||
                    !path.Equals(
                        _fs.SessionGenerationPath,
                        StringComparison.OrdinalIgnoreCase))
                {
                    return Task.CompletedTask;
                }

                File.Move(_fs.SessionGenerationPath, displacedGenerationPath);
                File.CreateSymbolicLink(
                    _fs.SessionGenerationPath,
                    externalGenerationPath);
                swapped = true;
                return Task.CompletedTask;
            },
            AfterRuntimeFileReadOpenedAsync = path =>
            {
                if (!swapped ||
                    restored ||
                    !path.Equals(
                        _fs.SessionGenerationPath,
                        StringComparison.OrdinalIgnoreCase))
                {
                    return Task.CompletedTask;
                }

                try
                {
                    File.Delete(externalGenerationPath);
                }
                catch (IOException)
                {
                    openedHandleHeldDuringHook = true;
                }
                catch (UnauthorizedAccessException)
                {
                    openedHandleHeldDuringHook = true;
                }

                File.Delete(_fs.SessionGenerationPath);
                File.Move(displacedGenerationPath, _fs.SessionGenerationPath);
                restored = true;
                return Task.CompletedTask;
            }
        };
        var raceFs = new FileSystemManager(
            _rootPath,
            NullLogger<FileSystemManager>.Instance,
            PhysicalLoadTransactionOperations.Instance,
            hooks);

        try
        {
            await using var writeLease = await raceFs.AcquireCanonicalWriteLeaseAsync();
            armed = true;

            Assert.Throws<InvalidDataException>(
                () => raceFs.IsCurrentSessionGeneration(
                    writeLease,
                    externalGeneration));

            Assert.True(swapped);
            Assert.True(restored);
            Assert.True(openedHandleHeldDuringHook);
            Assert.True(raceFs.IsCurrentSessionGeneration(
                writeLease,
                canonicalGeneration));
        }
        finally
        {
            if (File.GetAttributes(generationRoot)
                    .HasFlag(FileAttributes.ReparsePoint))
            {
                Directory.Delete(generationRoot, recursive: false);
            }

            if (File.Exists(_fs.SessionGenerationPath) &&
                File.GetAttributes(_fs.SessionGenerationPath)
                    .HasFlag(FileAttributes.ReparsePoint))
            {
                File.Delete(_fs.SessionGenerationPath);
            }

            if (File.Exists(displacedGenerationPath) &&
                !File.Exists(_fs.SessionGenerationPath))
            {
                File.Move(displacedGenerationPath, _fs.SessionGenerationPath);
            }

            if (Directory.Exists(outsideRoot))
                Directory.Delete(outsideRoot, recursive: true);
        }
    }

    [Fact]
    public async Task LoadTransactionCreate_ParentSwapCannotTruncateExternalFile()
    {
        if (!OperatingSystem.IsWindows())
            return;

        var transactionPaths = _fs.GetLoadTransactionPaths(
            Guid.NewGuid().ToString("N"));
        var stagingRoot = transactionPaths.StagingSessionPath;
        var displacedStagingRoot = stagingRoot + "-displaced";
        var stagedFile = Path.Combine(stagingRoot, "payload.bin");
        var outsideRoot = Path.Combine(
            _rootPath,
            "load-create-swap-outside");
        var outsideFile = Path.Combine(outsideRoot, "payload.bin");
        Directory.CreateDirectory(outsideRoot);
        await File.WriteAllBytesAsync(outsideFile, [0x91, 0x82, 0x73]);

        var armed = false;
        var swapAttempted = false;
        var swapBlocked = false;
        var swapped = false;
        var hooks = new FileSystemManagerHooks
        {
            BeforeRuntimeFileCreateAsync = path =>
            {
                if (!armed ||
                    swapAttempted ||
                    !path.Equals(stagedFile, StringComparison.OrdinalIgnoreCase))
                {
                    return Task.CompletedTask;
                }

                swapAttempted = true;
                try
                {
                    Directory.Move(stagingRoot, displacedStagingRoot);
                    CreateDirectoryJunction(stagingRoot, outsideRoot);
                    swapped = true;
                }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
                {
                    swapBlocked = true;
                }

                return Task.CompletedTask;
            }
        };
        var raceFs = new FileSystemManager(
            _rootPath,
            NullLogger<FileSystemManager>.Instance,
            PhysicalLoadTransactionOperations.Instance,
            hooks);

        try
        {
            raceFs.CreateLoadDirectory(stagingRoot);
            armed = true;
            await using var source = new MemoryStream([0x10, 0x20, 0x30]);

            await raceFs.WriteLoadTransactionFileAsync(stagedFile, source);

            Assert.True(swapAttempted);
            Assert.True(swapBlocked);
            Assert.False(swapped);
            Assert.Equal(
                new byte[] { 0x91, 0x82, 0x73 },
                await File.ReadAllBytesAsync(outsideFile));
            Assert.Equal(
                new byte[] { 0x10, 0x20, 0x30 },
                await File.ReadAllBytesAsync(stagedFile));
        }
        finally
        {
            if (Directory.Exists(stagingRoot) &&
                (File.GetAttributes(stagingRoot) & FileAttributes.ReparsePoint) != 0)
            {
                Directory.Delete(stagingRoot, recursive: false);
            }

            if (Directory.Exists(displacedStagingRoot) &&
                !Directory.Exists(stagingRoot))
            {
                Directory.Move(displacedStagingRoot, stagingRoot);
            }

            if (Directory.Exists(transactionPaths.TransactionRoot))
                Directory.Delete(transactionPaths.TransactionRoot, recursive: true);
            if (Directory.Exists(outsideRoot))
                Directory.Delete(outsideRoot, recursive: true);
        }
    }

    [Fact]
    public async Task SessionGenerationWrite_ParentSwapAfterAuthorityValidationIsBlocked()
    {
        if (!OperatingSystem.IsWindows())
            return;

        await using (var setupLease = await _fs.AcquireCanonicalWriteLeaseAsync())
            _fs.GetOrCreateSessionGeneration(setupLease);

        var generationRoot = Path.GetDirectoryName(_fs.SessionGenerationPath)!;
        var displacedGenerationRoot = generationRoot + "-write-displaced";
        var outsideRoot = Path.Combine(_rootPath, "generation-write-outside");
        var outsideFile = Path.Combine(outsideRoot, "current.json");
        Directory.CreateDirectory(outsideRoot);
        var outsideSentinel = new byte[] { 0x61, 0x72, 0x83, 0x94 };
        await File.WriteAllBytesAsync(outsideFile, outsideSentinel);

        var armed = false;
        var swapAttempted = false;
        var swapBlocked = false;
        var swapped = false;
        var hooks = new FileSystemManagerHooks
        {
            AfterRuntimeMutationBoundaryValidatedAsync = path =>
            {
                if (!armed ||
                    swapAttempted ||
                    !path.Equals(
                        _fs.SessionGenerationPath,
                        StringComparison.OrdinalIgnoreCase))
                {
                    return Task.CompletedTask;
                }

                swapAttempted = true;
                try
                {
                    Directory.Move(generationRoot, displacedGenerationRoot);
                    CreateDirectoryJunction(generationRoot, outsideRoot);
                    swapped = true;
                }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
                {
                    swapBlocked = true;
                }

                return Task.CompletedTask;
            }
        };
        var raceFs = new FileSystemManager(
            _rootPath,
            NullLogger<FileSystemManager>.Instance,
            PhysicalLoadTransactionOperations.Instance,
            hooks);

        try
        {
            await using var lifecycleLease =
                await raceFs.AcquireSessionLifecycleLeaseAsync();
            await using var replacementLease =
                await raceFs.AcquireSessionReplacementWriteLeaseAsync(
                    lifecycleLease);
            armed = true;
            var generation = raceFs.RotateSessionGeneration(replacementLease);

            Assert.True(swapAttempted);
            Assert.True(swapBlocked);
            Assert.False(swapped);
            Assert.Equal(
                outsideSentinel,
                await File.ReadAllBytesAsync(outsideFile));
            Assert.True(raceFs.IsCurrentSessionGeneration(
                replacementLease,
                generation));
        }
        finally
        {
            if (Directory.Exists(generationRoot) &&
                (File.GetAttributes(generationRoot) & FileAttributes.ReparsePoint) != 0)
            {
                Directory.Delete(generationRoot, recursive: false);
            }

            if (Directory.Exists(displacedGenerationRoot) &&
                !Directory.Exists(generationRoot))
            {
                Directory.Move(displacedGenerationRoot, generationRoot);
            }

            if (Directory.Exists(outsideRoot))
                Directory.Delete(outsideRoot, recursive: true);
        }
    }

    [Fact]
    public void LoadDirectoryMove_SourceParentSwapAtOperationBoundaryIsBlocked()
    {
        if (!OperatingSystem.IsWindows())
            return;

        var transactionPaths = _fs.GetLoadTransactionPaths(
            Guid.NewGuid().ToString("N"));
        var sourcePath = transactionPaths.StagingSessionPath;
        var sourceParent = Path.GetDirectoryName(sourcePath)!;
        var displacedSourceParent = sourceParent + "-displaced";
        var destinationPath = transactionPaths.FailedSessionPath;
        var outsideParent = Path.Combine(
            _rootPath,
            "load-move-swap-outside");
        var outsideSource = Path.Combine(
            outsideParent,
            Path.GetFileName(sourcePath));
        var outsideSentinel = Path.Combine(outsideSource, "sentinel.json");
        var canonicalMarker = Path.Combine(sourcePath, "canonical.json");
        Directory.CreateDirectory(sourcePath);
        Directory.CreateDirectory(Path.GetDirectoryName(destinationPath)!);
        Directory.CreateDirectory(outsideSource);
        File.WriteAllText(canonicalMarker, "{\"source\":\"canonical\"}");
        File.WriteAllText(outsideSentinel, "{\"source\":\"external\"}");
        var expectedOutsideBytes = File.ReadAllBytes(outsideSentinel);

        var swapAttempted = false;
        var swapBlocked = false;
        var swapped = false;
        var hooks = new FileSystemManagerHooks
        {
            BeforeLoadDirectoryMoveAsync = (actualSource, actualDestination) =>
            {
                if (swapAttempted ||
                    !actualSource.Equals(
                        sourcePath,
                        StringComparison.OrdinalIgnoreCase) ||
                    !actualDestination.Equals(
                        destinationPath,
                        StringComparison.OrdinalIgnoreCase))
                {
                    return Task.CompletedTask;
                }

                swapAttempted = true;
                try
                {
                    Directory.Move(sourceParent, displacedSourceParent);
                    CreateDirectoryJunction(sourceParent, outsideParent);
                    swapped = true;
                }
                catch (IOException)
                {
                    swapBlocked = true;
                }

                return Task.CompletedTask;
            }
        };
        var raceFs = new FileSystemManager(
            _rootPath,
            NullLogger<FileSystemManager>.Instance,
            PhysicalLoadTransactionOperations.Instance,
            hooks);

        try
        {
            raceFs.MoveLoadDirectory(sourcePath, destinationPath);

            Assert.True(swapAttempted);
            Assert.True(swapBlocked);
            Assert.False(swapped);
            Assert.Equal(
                "{\"source\":\"canonical\"}",
                File.ReadAllText(Path.Combine(destinationPath, "canonical.json")));
            Assert.Equal(
                expectedOutsideBytes,
                File.ReadAllBytes(outsideSentinel));
        }
        finally
        {
            if (Directory.Exists(sourceParent) &&
                (File.GetAttributes(sourceParent) &
                 FileAttributes.ReparsePoint) != 0)
            {
                Directory.Delete(sourceParent, recursive: false);
            }

            if (Directory.Exists(displacedSourceParent) &&
                !Directory.Exists(sourceParent))
            {
                Directory.Move(displacedSourceParent, sourceParent);
            }

            if (Directory.Exists(transactionPaths.TransactionRoot))
                Directory.Delete(transactionPaths.TransactionRoot, recursive: true);
            if (Directory.Exists(outsideParent))
                Directory.Delete(outsideParent, recursive: true);
        }
    }

    [Fact]
    public async Task AtomicWrite_HandleAuthoritySupportsLongCanonicalPaths()
    {
        var nested = string.Join(
            '/',
            Enumerable.Range(0, 6)
                .Select(index => $"{index:D2}-{new string('x', 40)}"));
        var relativePath = $"game_state/misc/{nested}/state.json";
        var expectedPath = _fs.ResolvePath(relativePath);
        Assert.True(expectedPath.Length > 260);

        await _fs.WriteFileAtomicAsync(
            relativePath,
            "{\"longPath\":true}");

        Assert.Equal(
            "{\"longPath\":true}",
            await _fs.ReadFileAsync(relativePath));
    }

    [Fact]
    public void FileSystemManagers_ForPhysicalRootAliases_ShareCanonicalRuntimeIdentity()
    {
        var aliasPath = Path.Combine(
            Path.GetDirectoryName(_rootPath)!,
            "boe-fs-alias-" + Guid.NewGuid().ToString("N"));
        if (!TryCreateDirectoryLink(aliasPath, _rootPath))
            return;

        try
        {
            var aliased = new FileSystemManager(
                aliasPath,
                NullLogger<FileSystemManager>.Instance);

            Assert.Equal(_fs.BasePath, aliased.BasePath, ignoreCase: true);
            Assert.Equal(
                _fs.CanonicalWriteLockPath,
                aliased.CanonicalWriteLockPath,
                ignoreCase: true);
        }
        finally
        {
            Directory.Delete(aliasPath);
        }
    }

    [Fact]
    public void PhysicalFileAuthority_RetainedLeafAuthorityBlocksEveryAncestorRename()
    {
        if (!OperatingSystem.IsWindows())
            return;

        var authorityRoot = Path.Combine(
            _rootPath,
            "stable-directory-ancestor-root");
        var intermediatePath = Path.Combine(
            authorityRoot,
            "level-one");
        var leafPath = Path.Combine(
            intermediatePath,
            "level-two",
            "level-three");
        Directory.CreateDirectory(leafPath);
        try
        {
            using (PhysicalFileAuthority.EnsureStableDirectory(
                       authorityRoot,
                       leafPath,
                       "Stable ancestor retention test"))
            {
                foreach (var candidate in new[]
                         {
                             authorityRoot,
                             intermediatePath,
                             Path.Combine(intermediatePath, "level-two"),
                             leafPath
                         })
                {
                    var displacedPath = candidate + ".displaced";
                    var blocked = false;
                    try
                    {
                        Directory.Move(candidate, displacedPath);
                    }
                    catch (Exception ex) when (
                        ex is IOException or UnauthorizedAccessException)
                    {
                        blocked = true;
                    }

                    Assert.True(
                        blocked,
                        $"Retained leaf authority must deny rename of '{candidate}'.");
                }
            }
        }
        finally
        {
            if (Directory.Exists(authorityRoot))
                Directory.Delete(authorityRoot, recursive: true);
        }
    }

    [Fact]
    public void AuthorityPublication_DoesNotUsePathnameMoveFallback()
    {
        var source = File.ReadAllText(Path.Combine(
            TestRepoPaths.RepoRoot,
            "BookOfEternityClient",
            "Core",
            "FileSystemManager.cs"));

        Assert.DoesNotContain("File.Move(", source, StringComparison.Ordinal);
        Assert.DoesNotContain("Directory.Move(", source, StringComparison.Ordinal);
    }

    private static bool TryCreateDirectoryLink(string linkPath, string targetPath)
    {
        try
        {
            Directory.CreateSymbolicLink(linkPath, targetPath);
            return true;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or PlatformNotSupportedException)
        {
            return false;
        }
    }

    private static bool TryCreateFileLink(string linkPath, string targetPath)
    {
        try
        {
            File.CreateSymbolicLink(linkPath, targetPath);
            return true;
        }
        catch (Exception ex) when (
            ex is IOException or
            UnauthorizedAccessException or
            PlatformNotSupportedException)
        {
            return false;
        }
    }

    private static void CreateHardLink(string linkPath, string targetPath)
    {
        if (CreateHardLinkNative(linkPath, targetPath, IntPtr.Zero))
            return;

        throw new Win32Exception(
            Marshal.GetLastWin32Error(),
            $"Could not create hard link '{linkPath}'.");
    }

    private async Task AssertOpenedExternalLockHandleRejectedAsync(bool isLifecycleLease)
    {
        var lockRoot = Path.Combine(_fs.RuntimeRootPath, "locks");
        var displacedLockRoot = Path.Combine(_fs.RuntimeRootPath, "locks-safe");
        var outsideRoot = Path.Combine(_rootPath, "opened-lock-outside");
        var probeLink = Path.Combine(_rootPath, "opened-lock-probe");
        Directory.CreateDirectory(outsideRoot);
        if (!TryCreateDirectoryLink(probeLink, outsideRoot))
            return;
        Directory.Delete(probeLink);

        var swappedToOutside = false;
        Task BeforeOpen()
        {
            Directory.Move(lockRoot, displacedLockRoot);
            Directory.CreateSymbolicLink(lockRoot, outsideRoot);
            swappedToOutside = true;
            return Task.CompletedTask;
        }

        var hooks = isLifecycleLease
            ? new FileSystemManagerHooks
            {
                BeforeSessionLifecycleLockOpenAsync = BeforeOpen
            }
            : new FileSystemManagerHooks
            {
                BeforeCanonicalWriteLockOpenAsync = BeforeOpen
            };
        var raceFs = new FileSystemManager(
            _rootPath,
            NullLogger<FileSystemManager>.Instance,
            PhysicalLoadTransactionOperations.Instance,
            hooks);

        try
        {
            if (isLifecycleLease)
            {
                await Assert.ThrowsAsync<InvalidDataException>(
                    async () => await raceFs.AcquireSessionLifecycleLeaseAsync());
            }
            else
            {
                await Assert.ThrowsAsync<InvalidDataException>(
                    async () => await raceFs.AcquireCanonicalWriteLeaseAsync());
            }

            Assert.True(swappedToOutside);
            Assert.Empty(Directory.EnumerateFileSystemEntries(outsideRoot));
        }
        finally
        {
            if (Directory.Exists(lockRoot) && FileSystemManager.IsReparsePoint(lockRoot))
                Directory.Delete(lockRoot);
            if (Directory.Exists(displacedLockRoot))
                Directory.Move(displacedLockRoot, lockRoot);
        }
    }

    [DllImport(
        "kernel32.dll",
        EntryPoint = "CreateHardLinkW",
        CharSet = CharSet.Unicode,
        SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool CreateHardLinkNative(
        string lpFileName,
        string lpExistingFileName,
        IntPtr lpSecurityAttributes);

    private static string Sha256(byte[] content) =>
        Convert.ToHexString(SHA256.HashData(content)).ToLowerInvariant();

    private static TaskCompletionSource NewBarrier() =>
        new(TaskCreationOptions.RunContinuationsAsynchronously);

    private static int GetAmbientCanonicalLeaseDepth(
        FileSystemManager fs)
    {
        var current = GetAmbientCanonicalLeaseHead(fs);
        var depth = 0;
        while (current != null)
        {
            depth++;
            current = current.Previous;
        }

        return depth;
    }

    private static int GetActiveAmbientCanonicalLeaseCount(
        FileSystemManager fs)
    {
        var current = GetAmbientCanonicalLeaseHead(fs);
        var active = 0;
        while (current != null)
        {
            if (current.Active)
                active++;
            current = current.Previous;
        }

        return active;
    }

    private static FileSystemManager.AmbientCanonicalLeaseRegistration?
        GetAmbientCanonicalLeaseHead(FileSystemManager fs)
    {
        var field = typeof(FileSystemManager).GetField(
            "_ambientCanonicalLease",
            System.Reflection.BindingFlags.Instance |
            System.Reflection.BindingFlags.NonPublic);
        Assert.NotNull(field);
        var ambient = Assert.IsType<
            AsyncLocal<FileSystemManager.AmbientCanonicalLeaseRegistration?>>(
            field!.GetValue(fs));
        return ambient.Value;
    }

    private void WritePublicationCrashJournal(
        string transactionId,
        string sourcePath,
        string destinationPath,
        string quarantinePath,
        string failedSourcePath,
        WindowsHardLinkTestHelper.FileIdentity sourceIdentity,
        byte[] sourceBytes,
        WindowsHardLinkTestHelper.FileIdentity? destinationIdentity,
        byte[] destinationBytes,
        bool committed)
    {
        var transactionPath = Path.Combine(
            _fs.PhysicalPublicationTransactionsRootPath,
            transactionId);
        Directory.CreateDirectory(transactionPath);
        var intent = new
        {
            schemaVersion = 1,
            transactionId,
            authorityName = "Crash recovery test",
            sourcePath,
            destinationPath,
            destinationQuarantinePath = quarantinePath,
            failedSourcePath,
            sourceIdentity,
            sourceSha256 = Sha256(sourceBytes),
            destinationExisted = destinationIdentity is not null,
            destinationIdentity,
            destinationSha256 = destinationIdentity is null
                ? null
                : Sha256(destinationBytes)
        };
        File.WriteAllText(
            Path.Combine(transactionPath, "intent.json"),
            JsonSerializer.Serialize(intent));
        File.WriteAllBytes(
            Path.Combine(transactionPath, "destination-quarantined.marker"),
            []);
        File.WriteAllBytes(
            Path.Combine(transactionPath, "source-published.marker"),
            []);
        if (committed)
        {
            File.WriteAllBytes(
                Path.Combine(transactionPath, "committed.marker"),
                []);
        }
    }

    private sealed class WorkerCleanupFaultOperations : ILoadTransactionOperations
    {
        internal bool FailWorkerTransactionDelete { get; set; }
        internal bool FailWorkerTransactionJournalDelete { get; set; }

        public bool DirectoryExists(string path) => Directory.Exists(path);
        public bool FileExists(string path) => File.Exists(path);

        public void BeforeDeleteDirectory(string path)
        {
            if (FailWorkerTransactionDelete &&
                path.Contains(
                    $"{Path.DirectorySeparatorChar}worker-apply-transactions{Path.DirectorySeparatorChar}",
                    StringComparison.OrdinalIgnoreCase))
            {
                throw new IOException("Injected worker apply transaction cleanup failure.");
            }
        }

        public void BeforeDeleteFile(string path)
        {
            if (FailWorkerTransactionJournalDelete &&
                path.EndsWith(
                    $"{Path.DirectorySeparatorChar}worker-apply-transactions{Path.DirectorySeparatorChar}active.json",
                    StringComparison.OrdinalIgnoreCase))
            {
                throw new IOException("Injected worker apply active journal cleanup failure.");
            }
        }
    }

    private static void CreateDirectoryJunction(string junctionPath, string targetPath)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(junctionPath)!);
        using var process = Process.Start(new ProcessStartInfo
        {
            FileName = "cmd.exe",
            UseShellExecute = false,
            CreateNoWindow = true,
            Arguments = $"/d /c mklink /J \"{junctionPath}\" \"{targetPath}\""
        });
        if (process == null)
            throw new InvalidOperationException("Failed to start junction helper.");
        process.WaitForExit();
        if (process.ExitCode != 0)
            throw new InvalidOperationException($"Failed to create test junction: exit code {process.ExitCode}.");
    }

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(_rootPath))
                Directory.Delete(_rootPath, recursive: true);
        }
        catch
        {
            // Best effort cleanup for temp test data.
        }
    }
}
