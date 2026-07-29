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
        bool GuardOnly = false);

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
            return await TryCommitCoreAsync(
                fs,
                writeLease: null,
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

    internal static async Task<bool> TryCommitWithHookAsync(
        FileSystemManager fs,
        Func<PlannedWrite, Task> afterWriteApplied,
        params PlannedWrite[] writes)
    {
        ArgumentNullException.ThrowIfNull(afterWriteApplied);
        await CommitGate.WaitAsync();
        try
        {
            return await TryCommitCoreAsync(
                fs,
                writeLease: null,
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
        var completedWrites = new List<PlannedWrite>();
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

                await ApplyWriteAsync(fs, writeLease, write.Path, write.NextJson);
                completedWrites.Add(write);
                if (afterWriteApplied != null)
                    await afterWriteApplied(write);
            }

            return true;
        }
        catch (Exception ex)
        {
            for (var index = completedWrites.Count - 1; index >= 0; index--)
            {
                if (await TryRestoreAsync(fs, writeLease, completedWrites[index]))
                    continue;

                throw new InvalidOperationException(
                    $"Не удалось безопасно откатить coordinated state write для {completedWrites[index].Path}.",
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
        PlannedWrite write)
    {
        try
        {
            var currentJson = await ReadAsync(fs, writeLease, write.Path);
            if (!JsonMatches(currentJson, write.NextJson))
                return false;

            await ApplyWriteAsync(fs, writeLease, write.Path, write.PreviousJson);
            return true;
        }
        catch
        {
            return false;
        }
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
