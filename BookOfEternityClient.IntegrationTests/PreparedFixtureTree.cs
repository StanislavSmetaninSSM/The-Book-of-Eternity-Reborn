namespace BookOfEternityClient.Tests;

/// <summary>
/// Retains prepared fixture bytes without retaining writable roots or runtime owners.
/// </summary>
internal sealed class PreparedFixtureTree
{
    private readonly string[] _directories;
    private readonly FixtureFile[] _files;

    private PreparedFixtureTree(string[] directories, FixtureFile[] files)
    {
        _directories = directories;
        _files = files;
    }

    /// <summary>
    /// Captures a stable fixture tree into private immutable-by-convention byte arrays.
    /// </summary>
    /// <param name="rootPath">
    /// Existing prepared corpus root; the caller must exclude live lock files and runtime secrets.
    /// </param>
    /// <returns>
    /// An independent byte snapshot that does not retain the source directory or its owners.
    /// </returns>
    internal static PreparedFixtureTree Capture(string rootPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(rootPath);
        var root = Path.GetFullPath(rootPath);
        var directories = Directory.EnumerateDirectories(root, "*", SearchOption.AllDirectories)
            .Select(path => Path.GetRelativePath(root, path))
            .OrderBy(path => path.Length).ToArray();
        var files = Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories)
            .Select(path => new FixtureFile(Path.GetRelativePath(root, path), File.ReadAllBytes(path)))
            .ToArray();
        return new PreparedFixtureTree(directories, files);
    }

    /// <summary>
    /// Copies prepared bytes to an isolated writable fixture without exposing cached arrays.
    /// </summary>
    /// <param name="rootPath">
    /// Fresh owned destination corpus root; existing empty filesystem scaffold directories are allowed.
    /// </param>
    internal void Materialize(string rootPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(rootPath);
        var root = Path.GetFullPath(rootPath);
        Directory.CreateDirectory(root);
        foreach (var directory in _directories)
            Directory.CreateDirectory(Path.Combine(root, directory));
        foreach (var file in _files)
        {
            var destination = Path.Combine(root, file.RelativePath);
            Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
            File.WriteAllBytes(destination, file.Content);
        }
    }

    private sealed record FixtureFile(string RelativePath, byte[] Content);
}
