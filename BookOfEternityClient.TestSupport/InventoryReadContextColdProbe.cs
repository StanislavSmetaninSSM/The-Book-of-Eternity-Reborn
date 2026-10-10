using System.Security.Cryptography;
using System.Text.Json;
using BookOfEternityClient.Core;
using BookOfEternityClient.Services;
using Microsoft.Extensions.Logging.Abstractions;

namespace BookOfEternityClient.Tests;

internal static class InventoryReadContextColdProbe
{
    internal static async Task<int> RunAsync(string root, string output)
    {
        var files = new FileSystemManager(root, NullLogger<FileSystemManager>.Instance);
        var context = await SessionOperationContext.RunParticipatingCurrentSessionAsync(files,
            () => InventoryManagementService.ReadContextAsync(files));
        if (context == null) return 2;
        var state = Directory.EnumerateFiles(files.GameSessionPath, "*", SearchOption.AllDirectories)
            .OrderBy(p => p, StringComparer.Ordinal).ToDictionary(p => Path.GetRelativePath(files.GameSessionPath, p),
                p => Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(p))).ToLowerInvariant());
        await File.WriteAllTextAsync(output, JsonSerializer.Serialize(new
        {
            Scope = "new process actual InventoryManagementService.ReadContextAsync; not Program/game chain",
            ProcessId = Environment.ProcessId,
            Items = context.Items.Select(item => new { item.Identity, item.Count }),
            State = state, Generation = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(files.SessionGenerationPath))).ToLowerInvariant()
        }));
        return 0;
    }
}
