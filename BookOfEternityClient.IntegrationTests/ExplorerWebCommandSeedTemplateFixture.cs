using System.Collections.Concurrent;
using System.Security.Cryptography;
using BookOfEternityClient.Core;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed class ExplorerWebCommandSeedTemplateFixture : IAsyncLifetime
{
    private readonly string _fixtureRootPath = Path.Combine(
        Path.GetTempPath(),
        "boe-explorer-web-command-seeds-" + Guid.NewGuid().ToString("N"));
    private readonly ConcurrentDictionary<string, Lazy<Task<PreparedSeedProfile>>> _profiles =
        new(StringComparer.Ordinal);
    private readonly ConcurrentDictionary<string, int> _seedFactoryInvocationCounts =
        new(StringComparer.Ordinal);
    private readonly Lazy<Task<PreparedSeedProfile>> _emptySkeleton;

    public ExplorerWebCommandSeedTemplateFixture()
    {
        _emptySkeleton = new Lazy<Task<PreparedSeedProfile>>(
            PrepareEmptySkeletonAsync,
            LazyThreadSafetyMode.ExecutionAndPublication);
    }

    public IReadOnlyDictionary<string, int> SeedFactoryInvocationCounts =>
        _seedFactoryInvocationCounts;

    public Task InitializeAsync() => Task.CompletedTask;

    public string CreateIsolatedCaseRoot()
    {
        var emptySkeleton = _emptySkeleton.Value.GetAwaiter().GetResult();
        var rootPath = Path.Combine(
            _fixtureRootPath,
            "cases",
            Guid.NewGuid().ToString("N"));
        CopyDirectory(emptySkeleton.RootPath, rootPath);
        return rootPath;
    }

    public async Task PrepareSeededRootAsync(
        string profileKey,
        string destinationRootPath,
        Func<Task> seedFactory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(profileKey);
        ArgumentException.ThrowIfNullOrWhiteSpace(destinationRootPath);
        ArgumentNullException.ThrowIfNull(seedFactory);

        var prepared = await _profiles.GetOrAdd(
            profileKey,
            _ => new Lazy<Task<PreparedSeedProfile>>(
                () => PrepareProfileAsync(profileKey, destinationRootPath, seedFactory),
                LazyThreadSafetyMode.ExecutionAndPublication)).Value;

        CopyDirectory(prepared.RootPath, destinationRootPath);
    }

    public async Task DisposeAsync()
    {
        try
        {
            await VerifyPreparedRootsUnchangedAsync();
        }
        finally
        {
            DeleteOwnedFixtureRoot();
        }
    }

    private async Task<PreparedSeedProfile> PrepareProfileAsync(
        string profileKey,
        string sourceRootPath,
        Func<Task> seedFactory)
    {
        _seedFactoryInvocationCounts.AddOrUpdate(profileKey, 1, static (_, count) => count + 1);
        await seedFactory();

        var rootPath = Path.Combine(_fixtureRootPath, Guid.NewGuid().ToString("N"));
        CopyDirectory(sourceRootPath, rootPath);
        return new PreparedSeedProfile(
            rootPath,
            await CaptureFileHashesAsync(rootPath));
    }

    private async Task<PreparedSeedProfile> PrepareEmptySkeletonAsync()
    {
        var rootPath = Path.Combine(_fixtureRootPath, "empty-skeleton");
        Directory.CreateDirectory(rootPath);
        var fileSystem = new FileSystemManager(
            rootPath,
            NullLogger<FileSystemManager>.Instance);
        fileSystem.EnsureDirectoryStructure();
        return new PreparedSeedProfile(
            rootPath,
            await CaptureFileHashesAsync(rootPath));
    }

    private async Task VerifyPreparedRootsUnchangedAsync()
    {
        var createdProfiles = _profiles.Values
            .Where(static profile => profile.IsValueCreated)
            .ToList();
        if (_emptySkeleton.IsValueCreated)
            createdProfiles.Add(_emptySkeleton);

        foreach (var lazyProfile in createdProfiles)
        {
            var prepared = await lazyProfile.Value;
            var currentHashes = await CaptureFileHashesAsync(prepared.RootPath);
            var differences = DescribeHashDifferences(prepared.FileHashes, currentHashes);
            if (differences.Count > 0)
            {
                throw new InvalidOperationException(
                    "An Explorer web command audit changed its prepared seed profile:" +
                    Environment.NewLine +
                    string.Join(Environment.NewLine, differences));
            }
        }
    }

    private static async Task<IReadOnlyDictionary<string, string>> CaptureFileHashesAsync(
        string rootPath)
    {
        var hashes = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var directoryPath in Directory
                     .EnumerateDirectories(rootPath, "*", SearchOption.AllDirectories)
                     .Order(StringComparer.Ordinal))
        {
            var relativePath = Path.GetRelativePath(rootPath, directoryPath)
                .Replace(Path.DirectorySeparatorChar, '/');
            hashes.Add(relativePath + "/", "<directory>");
        }

        foreach (var filePath in Directory
                     .EnumerateFiles(rootPath, "*", SearchOption.AllDirectories)
                     .Order(StringComparer.Ordinal))
        {
            await using var stream = File.OpenRead(filePath);
            var hash = await SHA256.HashDataAsync(stream);
            var relativePath = Path.GetRelativePath(rootPath, filePath)
                .Replace(Path.DirectorySeparatorChar, '/');
            hashes.Add(relativePath, Convert.ToHexString(hash));
        }

        return hashes;
    }

    private static IReadOnlyList<string> DescribeHashDifferences(
        IReadOnlyDictionary<string, string> expected,
        IReadOnlyDictionary<string, string> actual)
    {
        var differences = new List<string>();
        foreach (var path in expected.Keys.Union(actual.Keys).Order(StringComparer.Ordinal))
        {
            if (!expected.TryGetValue(path, out var expectedHash))
                differences.Add($"added: {path}");
            else if (!actual.TryGetValue(path, out var actualHash))
                differences.Add($"removed: {path}");
            else if (!string.Equals(expectedHash, actualHash, StringComparison.Ordinal))
                differences.Add($"changed: {path}");
        }

        return differences;
    }

    private static void CopyDirectory(string source, string destination)
    {
        Directory.CreateDirectory(destination);
        foreach (var directory in Directory.GetDirectories(source, "*", SearchOption.AllDirectories))
        {
            var target = Path.Combine(destination, Path.GetRelativePath(source, directory));
            Directory.CreateDirectory(target);
        }

        foreach (var file in Directory.GetFiles(source, "*", SearchOption.AllDirectories))
        {
            var target = Path.Combine(destination, Path.GetRelativePath(source, file));
            Directory.CreateDirectory(Path.GetDirectoryName(target)!);
            File.Copy(file, target, overwrite: true);
        }
    }

    private void DeleteOwnedFixtureRoot()
    {
        var candidate = Path.GetFullPath(_fixtureRootPath)
            .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        var expectedPrefix = Path.GetFullPath(Path.GetTempPath())
            .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) +
            Path.DirectorySeparatorChar +
            "boe-explorer-web-command-seeds-";

        if (!candidate.StartsWith(expectedPrefix, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException($"Refusing to delete unowned seed fixture root '{candidate}'.");

        if (Directory.Exists(candidate))
            Directory.Delete(candidate, recursive: true);
    }

    private sealed record PreparedSeedProfile(
        string RootPath,
        IReadOnlyDictionary<string, string> FileHashes);
}
