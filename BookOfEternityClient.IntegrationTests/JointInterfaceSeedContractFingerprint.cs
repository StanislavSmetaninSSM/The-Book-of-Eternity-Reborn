using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text;

namespace BookOfEternityClient.Tests;

/// <summary>
/// Identifies the source and base-world inputs of a generated seed independently of checkout location.
/// </summary>
internal static class JointInterfaceSeedContractFingerprint
{
    /// <summary>
    /// Hashes the declared generator and game input closure without build outputs or documentation.
    /// </summary>
    /// <param name="repoRoot">
    /// The repository containing the three C# projects and base game-session assets.
    /// </param>
    /// <returns>
    /// A canonical uppercase SHA256 digest of relative names and exact input bytes.
    /// </returns>
    internal static string Compute(string repoRoot)
    {
        var root = Path.GetFullPath(repoRoot);
        var inputs = new List<string>();
        foreach (var project in new[] { "BookOfEternityClient", "BookOfEternityClient.IntegrationTests", "BookOfEternityClient.TestSupport" })
            inputs.AddRange(EnumerateInputs(Path.Combine(root, project), codeOnly: true));
        inputs.AddRange(EnumerateInputs(Path.Combine(root, "FileSystemExample", "game_session"), codeOnly: false));
        inputs.AddRange(Directory.EnumerateFiles(root).Where(path =>
            Path.GetExtension(path) is ".props" or ".targets" ||
            Path.GetFileName(path).Equals("global.json", StringComparison.OrdinalIgnoreCase) ||
            Path.GetFileName(path).Equals("NuGet.Config", StringComparison.OrdinalIgnoreCase)));

        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        var lengthBytes = new byte[8];
        foreach (var path in inputs.OrderBy(path => Path.GetRelativePath(root, path).Replace('\\', '/'), StringComparer.Ordinal))
        {
            if ((File.GetAttributes(path) & FileAttributes.ReparsePoint) != 0)
                throw new InvalidDataException("A seed contract input must be an ordinary file.");
            var bytes = File.ReadAllBytes(path);
            hash.AppendData(Encoding.UTF8.GetBytes(Path.GetRelativePath(root, path).Replace('\\', '/')));
            hash.AppendData(new byte[] { 0 });
            BinaryPrimitives.WriteInt64LittleEndian(lengthBytes, bytes.LongLength);
            hash.AppendData(lengthBytes);
            hash.AppendData(SHA256.HashData(bytes));
        }
        return Convert.ToHexString(hash.GetHashAndReset());
    }

    /// <summary>
    /// Enumerates ordinary source inputs while excluding generated project output directories.
    /// </summary>
    /// <param name="root">
    /// An existing project or base-world directory whose files belong to the closure.
    /// </param>
    /// <param name="codeOnly">
    /// <see langword="true"/> selects C# and build inputs and skips bin/obj; otherwise all assets are included.
    /// </param>
    /// <returns>
    /// Physical ordinary file paths whose relative names and bytes will be hashed.
    /// </returns>
    private static IEnumerable<string> EnumerateInputs(string root, bool codeOnly)
    {
        if (!Directory.Exists(root)) throw new DirectoryNotFoundException("A seed contract input directory is missing: " + root);
        var pending = new Stack<string>();
        pending.Push(root);
        while (pending.TryPop(out var directory))
        {
            foreach (var path in Directory.EnumerateFileSystemEntries(directory))
            {
                var name = Path.GetFileName(path);
                if (codeOnly && (name.Equals("bin", StringComparison.OrdinalIgnoreCase) || name.Equals("obj", StringComparison.OrdinalIgnoreCase))) continue;
                var attributes = File.GetAttributes(path);
                if ((attributes & FileAttributes.ReparsePoint) != 0)
                    throw new InvalidDataException("A seed contract input must be ordinary.");
                if ((attributes & FileAttributes.Directory) != 0) pending.Push(path);
                else if (!codeOnly || Path.GetExtension(path) is ".cs" or ".csproj" or ".props" or ".targets") yield return path;
            }
        }
    }
}
