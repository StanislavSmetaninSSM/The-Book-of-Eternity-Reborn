namespace BookOfEternityClient.Tests;

internal sealed record ResourceTestBeforeImage(bool Existed, byte[]? Bytes);

internal sealed partial class ResourceMaterializationTestContext
{
    internal Task<IReadOnlyDictionary<string, ResourceTestBeforeImage>> CaptureAsync(
        params string[] paths) =>
        CaptureAsync((IEnumerable<string>)paths);

    internal async Task<IReadOnlyDictionary<string, ResourceTestBeforeImage>> CaptureAsync(
        IEnumerable<string> paths)
    {
        ArgumentNullException.ThrowIfNull(paths);
        var result = new Dictionary<string, ResourceTestBeforeImage>(StringComparer.Ordinal);
        foreach (var path in paths.Distinct(StringComparer.Ordinal))
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(path);
            var bytes = await FileSystem.ReadFileBytesAsync(path);
            result[path] = new ResourceTestBeforeImage(
                bytes != null,
                bytes?.ToArray());
        }

        return result;
    }

    internal async Task AssertUnchangedAsync(
        IReadOnlyDictionary<string, ResourceTestBeforeImage> beforeImages)
    {
        ArgumentNullException.ThrowIfNull(beforeImages);
        foreach (var pair in beforeImages)
        {
            var current = await FileSystem.ReadFileBytesAsync(pair.Key);
            if (pair.Value.Existed != (current != null) ||
                !BytesEqual(pair.Value.Bytes, current))
            {
                throw new InvalidOperationException(
                    $"Expected '{pair.Key}' to retain its exact bytes and prior existence.");
            }
        }
    }

    internal async Task AssertConsumedAsync(
        IReadOnlyDictionary<string, ResourceTestBeforeImage> beforeImages,
        string path)
    {
        var before = RequireBeforeImage(beforeImages, path);
        if (!before.Existed)
        {
            throw new InvalidOperationException(
                $"Cannot prove consumption for '{path}' because it was absent before the transition.");
        }

        if (await FileSystem.ReadFileBytesAsync(path) != null)
            throw new InvalidOperationException($"Expected consumed path '{path}' to be absent.");
    }

    internal async Task AssertCreatedFromPriorAbsenceAsync(
        IReadOnlyDictionary<string, ResourceTestBeforeImage> beforeImages,
        string path)
    {
        var before = RequireBeforeImage(beforeImages, path);
        if (before.Existed)
        {
            throw new InvalidOperationException(
                $"Cannot prove prior-absence creation for '{path}' because it existed before the transition.");
        }

        if (await FileSystem.ReadFileBytesAsync(path) == null)
            throw new InvalidOperationException($"Expected newly created path '{path}' to exist.");
    }

    internal async Task AssertExactBytesAsync(string path, byte[] expected)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        ArgumentNullException.ThrowIfNull(expected);
        var current = await FileSystem.ReadFileBytesAsync(path);
        if (!BytesEqual(expected, current))
            throw new InvalidOperationException($"Expected exact bytes at '{path}'.");
    }

    private static ResourceTestBeforeImage RequireBeforeImage(
        IReadOnlyDictionary<string, ResourceTestBeforeImage> beforeImages,
        string path)
    {
        ArgumentNullException.ThrowIfNull(beforeImages);
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        return beforeImages.TryGetValue(path, out var before)
            ? before
            : throw new InvalidOperationException($"No captured before-image exists for '{path}'.");
    }

    private static bool BytesEqual(byte[]? expected, byte[]? actual) =>
        expected == null
            ? actual == null
            : actual != null && expected.AsSpan().SequenceEqual(actual);
}
