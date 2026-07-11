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
            var completedWrites = new List<PlannedWrite>();
            foreach (var write in writes)
            {
                if (write.RequireCurrentBaseline &&
                    !await CurrentMatchesExpectedBaselineAsync(fs, write))
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

                    await ApplyWriteAsync(fs, write.Path, write.NextJson);
                    completedWrites.Add(write);
                }

                return true;
            }
            catch (Exception ex)
            {
                for (var index = completedWrites.Count - 1; index >= 0; index--)
                {
                    if (await TryRestoreAsync(fs, completedWrites[index]))
                        continue;

                    throw new InvalidOperationException(
                        $"Не удалось безопасно откатить coordinated state write для {completedWrites[index].Path}.",
                        ex);
                }

                return false;
            }
        }
        finally
        {
            CommitGate.Release();
        }
    }

    private static async Task<bool> CurrentMatchesExpectedBaselineAsync(FileSystemManager fs, PlannedWrite write)
    {
        var currentJson = await fs.ReadFileAsync(write.Path);
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

    private static async Task<bool> TryRestoreAsync(FileSystemManager fs, PlannedWrite write)
    {
        try
        {
            var currentJson = await fs.ReadFileAsync(write.Path);
            if (!JsonMatches(currentJson, write.NextJson))
                return false;

            await ApplyWriteAsync(fs, write.Path, write.PreviousJson);
            return true;
        }
        catch
        {
            return false;
        }
    }

    private static async Task ApplyWriteAsync(FileSystemManager fs, string path, string? json)
    {
        if (json == null)
        {
            if (fs.FileExists(path))
                fs.DeleteFile(path);
            return;
        }

        await fs.WriteFileAtomicAsync(path, json);
    }
}
