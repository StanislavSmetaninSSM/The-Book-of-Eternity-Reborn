using Xunit;

namespace BookOfEternityClient.Tests;

public sealed class RepositoryEncodingScannerTests
{
    private static readonly string[] DirectoryRoots =
    [
        "BookOfEternityClient",
        "BookOfEternityClient.Tests",
        "Examples",
        "OtherGuides",
        "TaskGuides",
        "docs"
    ];

    private static readonly string[] RootFiles =
    [
        "CLI_Agent_Daemon_Specification.md",
        "CLI_API_Specification.md"
    ];

    private static readonly HashSet<string> TextExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".cs",
        ".md",
        ".txt",
        ".json",
        ".yml",
        ".yaml"
    };

    [Fact]
    public void UserFacingSourcesAndFixtures_MustNotContainCommonMojibakeMarkers()
    {
        var markers = BuildMojibakeMarkers();
        var hits = new List<string>();

        foreach (var path in EnumerateScannedFiles())
        {
            var text = File.ReadAllText(path);
            var relativePath = NormalizeRelativePath(path);

            foreach (var marker in markers)
            {
                if (text.Contains(marker, StringComparison.Ordinal))
                    hits.Add($"{relativePath}: contains mojibake marker {FormatMarker(marker)}");
            }
        }

        Assert.True(
            hits.Count == 0,
            "Common mojibake markers were found in user-facing sources or fixtures:" +
            Environment.NewLine +
            string.Join(Environment.NewLine, hits.Take(100)));
    }

    private static string[] BuildMojibakeMarkers() =>
    [
        string.Concat('\uFFFD'),
        string.Concat('\u00D0'),
        string.Concat('\u00D1'),
        string.Concat('\u0420', '\u0452'),
        string.Concat('\u0420', '\u00B0'),
        string.Concat('\u0421', '\u040F')
    ];

    private static IEnumerable<string> EnumerateScannedFiles()
    {
        foreach (var root in DirectoryRoots)
        {
            var fullRoot = Path.Combine(TestRepoPaths.RepoRoot, root);
            if (!Directory.Exists(fullRoot))
                continue;

            foreach (var path in Directory.EnumerateFiles(fullRoot, "*", SearchOption.AllDirectories))
            {
                if (ShouldScan(path))
                    yield return path;
            }
        }

        foreach (var rootFile in RootFiles)
        {
            var path = Path.Combine(TestRepoPaths.RepoRoot, rootFile);
            if (File.Exists(path))
                yield return path;
        }
    }

    private static bool ShouldScan(string path)
    {
        var relativePath = NormalizeRelativePath(path);
        if (!TextExtensions.Contains(Path.GetExtension(path)))
            return false;

        return !relativePath.Contains("/bin/", StringComparison.OrdinalIgnoreCase) &&
               !relativePath.Contains("/obj/", StringComparison.OrdinalIgnoreCase) &&
               !relativePath.StartsWith("docs/audits/", StringComparison.OrdinalIgnoreCase) &&
               !relativePath.StartsWith("docs/superpowers/", StringComparison.OrdinalIgnoreCase);
    }

    private static string NormalizeRelativePath(string path) =>
        Path.GetRelativePath(TestRepoPaths.RepoRoot, path).Replace('\\', '/');

    private static string FormatMarker(string marker) =>
        string.Join("+", marker.Select(character => $"U+{(int)character:X4}"));
}
