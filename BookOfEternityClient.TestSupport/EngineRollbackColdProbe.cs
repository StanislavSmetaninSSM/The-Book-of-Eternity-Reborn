using System.Text.Json;
using BookOfEternityClient.Core;
using Microsoft.Extensions.Logging.Abstractions;

namespace BookOfEternityClient.Tests;

// Fresh-process production acquisition/read or refusal; no engine continuation,
// injected hooks, manufactured lease, game provider or mutable fixture authority.
internal static class EngineRollbackColdProbe
{
    internal static Dictionary<string, byte[]> Images(FileSystemManager files) =>
        new[] { files.GameSessionPath, files.RuntimeRootPath }
            .Where(Directory.Exists).SelectMany(root => Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories))
            .Order(StringComparer.Ordinal).ToDictionary(path => Path.GetRelativePath(files.BasePath, path),
                File.ReadAllBytes, StringComparer.Ordinal);

    internal static async Task<int> RunAsync(string root, string output)
    {
        var files = new FileSystemManager(root, NullLogger<FileSystemManager>.Instance);
        var before = Images(files);
        Exception? failure = null;
        var acquired = false;
        string? generation = null;
        var reads = new Dictionary<string, byte[]?>(StringComparer.Ordinal);
        try
        {
            await using var lease = await files.AcquireCanonicalWriteLeaseAsync();
            acquired = true;
            generation = files.ReadExistingSessionGeneration(lease);
            foreach (var path in Directory.EnumerateFiles(files.GameSessionPath, "*", SearchOption.AllDirectories)
                         .Order(StringComparer.Ordinal))
            {
                var relative = Path.GetRelativePath(files.GameSessionPath, path);
                reads.Add(relative, await files.ReadFileBytesAsync(lease, relative));
            }
        }
        catch (Exception error) { failure = error; }
        await File.WriteAllTextAsync(output, JsonSerializer.Serialize(new
        {
            Scope = "fresh process actual canonical acquisition/recovery and leased canonical reads or original refusal; not GameEngine continuation",
            ProcessId = Environment.ProcessId, acquired, generation, reads,
            Failure = failure?.ToString(), FailureType = failure?.GetType().FullName,
            Before = before, After = Images(files)
        }));
        return 0; // A recorded authority refusal is a result, not a successful acquisition.
    }
}
