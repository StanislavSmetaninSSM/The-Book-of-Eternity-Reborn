using System.Text.Json.Nodes;
using BookOfEternityClient.Core;
using BookOfEternityClient.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace BookOfEternityClient.Tests;

internal sealed partial class MortalItemMaterializationTestContext
{
    internal async Task NormalizeAcceptedTurnAsync()
    {
        var issues = await NormalizeAcceptedTurnWithIssuesAsync();
        if (issues.Any(issue => issue.Severity == IssueSeverity.Error))
        {
            throw new InvalidOperationException(
                $"Post-seal validation failed: {issues[0].Code}.");
        }
    }

    internal async Task<IReadOnlyList<ValidationIssue>> NormalizeAcceptedTurnWithIssuesAsync()
    {
        var manifest = await ReadJsonAsync(
            "game_state/control/pending_turn_snapshot.json") as JsonObject ??
                       throw new InvalidOperationException("Pending snapshot manifest is missing.");
        var files = manifest["files"] as JsonObject ??
                    throw new InvalidOperationException("Pending snapshot file map is missing.");
        var backups = files.ToDictionary(
            pair => pair.Key,
            pair => pair.Value!.GetValue<string>(),
            StringComparer.OrdinalIgnoreCase);

        return await AcceptedTurnCanonicalStateRefresh.NormalizeAndValidateAsync(
            FileSystem,
            Normalizer,
            Validator,
            backups);
    }

    internal async Task<IReadOnlyDictionary<string, byte[]?>> CaptureExactBytesAsync(
        IEnumerable<string> paths)
    {
        var result = new Dictionary<string, byte[]?>(StringComparer.OrdinalIgnoreCase);
        foreach (var path in paths.Distinct(StringComparer.OrdinalIgnoreCase))
            result[path] = await FileSystem.ReadFileBytesAsync(path);
        return result;
    }

    internal async Task AssertExactBytesAsync(
        IReadOnlyDictionary<string, byte[]?> expected)
    {
        foreach (var (path, expectedBytes) in expected)
        {
            var actualBytes = await FileSystem.ReadFileBytesAsync(path);
            if (expectedBytes == null)
            {
                Assert.Null(actualBytes);
                continue;
            }

            Assert.NotNull(actualBytes);
            Assert.True(
                expectedBytes.AsSpan().SequenceEqual(actualBytes),
                $"Canonical rollback changed exact bytes for '{path}'.");
        }
    }

    internal void InjectWriteFailureBefore(string path)
    {
        var injected = 0;
        FileSystem = new FileSystemManager(
            RootPath,
            NullLogger<FileSystemManager>.Instance,
            PhysicalLoadTransactionOperations.Instance,
            new FileSystemManagerHooks
            {
                BeforeCanonicalMutationBoundaryAsync = relativePath =>
                {
                    if (string.Equals(relativePath, path, StringComparison.OrdinalIgnoreCase) &&
                        Interlocked.Exchange(ref injected, 1) == 0)
                    {
                        throw new InvalidOperationException(
                            $"Injected canonical write failure before '{relativePath}'.");
                    }

                    return Task.CompletedTask;
                }
            });
        FileSystem.EnsureDirectoryStructure();
        Validator = new ValidationService(
            FileSystem,
            NullLogger<ValidationService>.Instance);
        Normalizer = new CanonicalStateNormalizer(
            FileSystem,
            NullLogger<CanonicalStateNormalizer>.Instance);
    }
}
