using BookOfEternityClient.Core;

namespace BookOfEternityClient.Services;

internal static class CoordinatedStateWriteHelper
{
    internal sealed record PlannedWrite(
        string Path,
        string? PreviousJson,
        string? NextJson,
        bool RequireCurrentBaseline = false);

    public static async Task<bool> TryCommitAsync(
        FileSystemManager fs,
        params PlannedWrite[] writes)
    {
        var completedWrites = new List<PlannedWrite>();

        try
        {
            foreach (var write in writes)
            {
                if (write.RequireCurrentBaseline &&
                    !await CurrentMatchesExpectedBaselineAsync(fs, write))
                {
                    return false;
                }
            }

            foreach (var write in writes)
            {
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

    private static async Task<bool> CurrentMatchesExpectedBaselineAsync(FileSystemManager fs, PlannedWrite write)
    {
        var currentJson = await fs.ReadFileAsync(write.Path);
        return string.Equals(currentJson, write.PreviousJson, StringComparison.Ordinal);
    }

    private static async Task<bool> TryRestoreAsync(FileSystemManager fs, PlannedWrite write)
    {
        try
        {
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
