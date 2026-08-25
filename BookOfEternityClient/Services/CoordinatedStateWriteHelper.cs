using System.Text.Json.Nodes;
using BookOfEternityClient.Core;

namespace BookOfEternityClient.Services;

internal static class CoordinatedStateWriteHelper
{
    private static readonly SemaphoreSlim CommitGate = new(1, 1);

    internal sealed record PlannedWrite(
        string Path,
        string? PreviousJson,
        string? NextJson,
        bool RequireCurrentBaseline = false,
        bool GuardOnly = false,
        CanonicalBeforeImage? ExactPrevious = null);

    internal static PlannedWrite CreateExactGuardWrite(
        string path,
        CanonicalBeforeImage beforeImage)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        ArgumentNullException.ThrowIfNull(beforeImage);
        return new PlannedWrite(
            path,
            PreviousJson: null,
            NextJson: null,
            RequireCurrentBaseline: true,
            GuardOnly: true,
            ExactPrevious: new CanonicalBeforeImage(
                beforeImage.Existed,
                beforeImage.Bytes));
    }

    internal static PlannedWrite[] CreateAuthorityGuardWrites(LocalInteractionScope scope) =>
        scope.AuthoritySnapshots
            .GroupBy(snapshot => snapshot.Path, StringComparer.OrdinalIgnoreCase)
            .Select(group => group.Last())
            .Select(snapshot => new PlannedWrite(
                snapshot.Path,
                snapshot.Json,
                snapshot.Json,
                RequireCurrentBaseline: true,
                GuardOnly: true))
            .ToArray();

    internal static PlannedWrite CreateGuardWrite(string path, string? json) =>
        new(
            path,
            json,
            json,
            RequireCurrentBaseline: true,
            GuardOnly: true);

    public static async Task<bool> TryCommitAsync(
        FileSystemManager fs,
        params PlannedWrite[] writes)
    {
        await CommitGate.WaitAsync();
        try
        {
            await using var writeLease = await fs.AcquireCanonicalWriteLeaseAsync();
            return await TryCommitCoreAsync(
                fs,
                writeLease,
                afterWriteApplied: null,
                writes);
        }
        finally
        {
            CommitGate.Release();
        }
    }

    internal static async Task<bool> TryCommitAsync(
        FileSystemManager fs,
        FileSystemManager.CanonicalWriteLease writeLease,
        params PlannedWrite[] writes) =>
        await TryCommitCoreAsync(
            fs,
            writeLease,
            afterWriteApplied: null,
            writes);

    internal static Task<bool> TryCommitWithHookAsync(
        FileSystemManager fs,
        FileSystemManager.CanonicalWriteLease writeLease,
        Func<PlannedWrite, Task> afterWriteApplied,
        params PlannedWrite[] writes)
    {
        ArgumentNullException.ThrowIfNull(writeLease);
        ArgumentNullException.ThrowIfNull(afterWriteApplied);
        return TryCommitCoreAsync(
            fs,
            writeLease,
            afterWriteApplied,
            writes);
    }

    internal static async Task<bool> TryCommitWithHookAsync(
        FileSystemManager fs,
        Func<PlannedWrite, Task> afterWriteApplied,
        params PlannedWrite[] writes)
    {
        ArgumentNullException.ThrowIfNull(afterWriteApplied);
        await CommitGate.WaitAsync();
        try
        {
            await using var writeLease = await fs.AcquireCanonicalWriteLeaseAsync();
            return await TryCommitCoreAsync(
                fs,
                writeLease,
                afterWriteApplied,
                writes);
        }
        finally
        {
            CommitGate.Release();
        }
    }

    private static async Task<bool> TryCommitCoreAsync(
        FileSystemManager fs,
        FileSystemManager.CanonicalWriteLease? writeLease,
        Func<PlannedWrite, Task>? afterWriteApplied,
        PlannedWrite[] writes)
    {
        var completedWrites = new List<(PlannedWrite Write, byte[]? PreviousBytes)>();
        foreach (var write in writes)
        {
            if (write.RequireCurrentBaseline &&
                !await CurrentMatchesExpectedBaselineAsync(fs, writeLease, write))
            {
                return false;
            }
        }

        try
        {
            foreach (var write in writes)
            {
                if (write.GuardOnly)
                    continue;

                var previousBytes = await ReadBytesAsync(
                    fs,
                    writeLease,
                    write.Path);
                await ApplyWriteAsync(fs, writeLease, write.Path, write.NextJson);
                completedWrites.Add((write, previousBytes));
                if (afterWriteApplied != null)
                    await afterWriteApplied(write);
            }

            return true;
        }
        catch (Exception ex)
        {
            for (var index = completedWrites.Count - 1; index >= 0; index--)
            {
                if (await TryRestoreAsync(
                        fs,
                        writeLease,
                        completedWrites[index].Write,
                        completedWrites[index].PreviousBytes))
                    continue;

                throw new InvalidOperationException(
                    $"Не удалось безопасно откатить coordinated state write для {completedWrites[index].Write.Path}.",
                    ex);
            }

            return false;
        }
    }

    private static async Task<bool> CurrentMatchesExpectedBaselineAsync(
        FileSystemManager fs,
        FileSystemManager.CanonicalWriteLease? writeLease,
        PlannedWrite write)
    {
        if (write.ExactPrevious != null)
        {
            var currentBytes = await ReadBytesAsync(
                fs,
                writeLease,
                write.Path);
            if (write.ExactPrevious.Existed != (currentBytes != null))
                return false;
            return currentBytes == null ||
                   currentBytes.AsSpan().SequenceEqual(
                       write.ExactPrevious.Bytes!);
        }

        var currentJson = await ReadAsync(fs, writeLease, write.Path);
        return JsonMatches(currentJson, write.PreviousJson);
    }

    private static bool JsonMatches(string? currentJson, string? expectedJson)
    {
        if (string.Equals(currentJson, expectedJson, StringComparison.Ordinal))
            return true;
        if (currentJson == null || expectedJson == null)
            return false;

        try
        {
            return JsonNode.DeepEquals(JsonNode.Parse(currentJson), JsonNode.Parse(expectedJson));
        }
        catch
        {
            return false;
        }
    }

    private static async Task<bool> TryRestoreAsync(
        FileSystemManager fs,
        FileSystemManager.CanonicalWriteLease? writeLease,
        PlannedWrite write,
        byte[]? previousBytes)
    {
        try
        {
            var currentJson = await ReadAsync(fs, writeLease, write.Path);
            if (!JsonMatches(currentJson, write.NextJson))
                return false;

            await RestoreExactBytesAsync(
                fs,
                writeLease,
                write.Path,
                previousBytes);
            return true;
        }
        catch
        {
            return false;
        }
    }

    private static Task<byte[]?> ReadBytesAsync(
        FileSystemManager fs,
        FileSystemManager.CanonicalWriteLease? writeLease,
        string path) =>
        writeLease == null
            ? fs.ReadFileBytesAsync(path)
            : fs.ReadFileBytesAsync(writeLease, path);

    private static async Task RestoreExactBytesAsync(
        FileSystemManager fs,
        FileSystemManager.CanonicalWriteLease? writeLease,
        string path,
        byte[]? bytes)
    {
        if (bytes == null)
        {
            if (writeLease == null)
            {
                if (fs.FileExists(path))
                    fs.DeleteFile(path);
            }
            else if (fs.FileExists(writeLease, path))
            {
                fs.DeleteFile(writeLease, path);
            }
            return;
        }

        if (writeLease == null)
            await fs.WriteFileAtomicBytesAsync(path, bytes);
        else
            await fs.WriteFileAtomicBytesAsync(writeLease, path, bytes);
    }

    private static async Task ApplyWriteAsync(
        FileSystemManager fs,
        FileSystemManager.CanonicalWriteLease? writeLease,
        string path,
        string? json)
    {
        if (json == null)
        {
            if (writeLease == null)
            {
                if (fs.FileExists(path))
                    fs.DeleteFile(path);
            }
            else if (fs.FileExists(writeLease, path))
            {
                fs.DeleteFile(writeLease, path);
            }
            return;
        }

        if (writeLease == null)
            await fs.WriteFileAtomicAsync(path, json);
        else
            await fs.WriteFileAtomicAsync(writeLease, path, json);
    }

    private static Task<string?> ReadAsync(
        FileSystemManager fs,
        FileSystemManager.CanonicalWriteLease? writeLease,
        string path) =>
        writeLease == null
            ? fs.ReadFileAsync(path)
            : fs.ReadFileAsync(writeLease, path);
}
