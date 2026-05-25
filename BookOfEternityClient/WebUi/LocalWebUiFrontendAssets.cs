using System.Diagnostics;

namespace BookOfEternityClient.WebUi;

internal sealed class LocalWebUiFrontendAssets
{
    private const string IndexFileName = "index.html";
    private const string FallbackShellFileName = "local-web-ui-shell.html";

    private LocalWebUiFrontendAssets(string rootPath, string indexPath, bool isFallbackShell)
    {
        RootPath = Path.GetFullPath(rootPath);
        IndexPath = Path.GetFullPath(indexPath);
        IsFallbackShell = isFallbackShell;
    }

    public string RootPath { get; }

    public string IndexPath { get; }

    public bool IsFallbackShell { get; }

    public static LocalWebUiFrontendAssets Resolve(string? overridePath = null)
    {
        if (!string.IsNullOrWhiteSpace(overridePath))
            return ResolveOverride(overridePath);

        foreach (var root in CandidateBuildRoots())
        {
            var shellPath = Path.Combine(root, FallbackShellFileName);
            if (File.Exists(shellPath))
                return new LocalWebUiFrontendAssets(root, shellPath, isFallbackShell: true);

            var indexPath = Path.Combine(root, IndexFileName);
            if (File.Exists(indexPath))
                return new LocalWebUiFrontendAssets(root, indexPath, isFallbackShell: false);
        }

        foreach (var shellPath in CandidateFallbackShells())
        {
            if (File.Exists(shellPath))
                return new LocalWebUiFrontendAssets(Path.GetDirectoryName(shellPath)!, shellPath, isFallbackShell: true);
        }

        throw new InvalidOperationException(
            "Browser frontend assets were not found. Run `npm run build --prefix BookOfEternityClient.WebFrontend` " +
            "from the repository root, or keep the tracked fallback shell at " +
            "BookOfEternityClient.WebFrontend/public/local-web-ui-shell.html.");
    }

    private static LocalWebUiFrontendAssets ResolveOverride(string overridePath)
    {
        var fullPath = Path.GetFullPath(overridePath);
        if (File.Exists(fullPath))
        {
            if (!IsHtmlFile(fullPath))
            {
                throw new InvalidOperationException(
                    $"Browser frontend asset override '{overridePath}' must point to an HTML file.");
            }

            return new LocalWebUiFrontendAssets(Path.GetDirectoryName(fullPath)!, fullPath, IsFallbackShellFile(fullPath));
        }

        if (Directory.Exists(fullPath))
        {
            var indexPath = Path.Combine(fullPath, IndexFileName);
            if (File.Exists(indexPath))
                return new LocalWebUiFrontendAssets(fullPath, indexPath, isFallbackShell: false);

            var fallbackPath = Path.Combine(fullPath, FallbackShellFileName);
            if (File.Exists(fallbackPath))
                return new LocalWebUiFrontendAssets(fullPath, fallbackPath, isFallbackShell: true);
        }

        throw new InvalidOperationException(
            $"Browser frontend asset override '{overridePath}' must be an HTML file or a directory containing " +
            $"{IndexFileName} or {FallbackShellFileName}.");
    }

    private static IEnumerable<string> CandidateBuildRoots()
    {
        foreach (var root in CandidateRepoRoots())
            yield return Path.Combine(root, "BookOfEternityClient.WebFrontend", "dist");

        yield return Path.Combine(AppContext.BaseDirectory, "wwwroot", "browser");
    }

    private static IEnumerable<string> CandidateFallbackShells()
    {
        foreach (var root in CandidateRepoRoots())
        {
            yield return Path.Combine(
                root,
                "BookOfEternityClient.WebFrontend",
                "public",
                FallbackShellFileName);
        }

        yield return Path.Combine(AppContext.BaseDirectory, "wwwroot", "browser", FallbackShellFileName);
    }

    private static IEnumerable<string> CandidateRepoRoots()
    {
        var seeds = new[]
        {
            Directory.GetCurrentDirectory(),
            AppContext.BaseDirectory,
            Path.GetDirectoryName(Environment.ProcessPath ?? string.Empty),
            Path.GetDirectoryName(Process.GetCurrentProcess().MainModule?.FileName ?? string.Empty)
        };

        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var seed in seeds.Where(seed => !string.IsNullOrWhiteSpace(seed)))
        {
            var directory = new DirectoryInfo(seed!);
            while (directory != null)
            {
                var candidate = directory.FullName;
                if (seen.Add(candidate) && Directory.Exists(Path.Combine(candidate, "BookOfEternityClient.WebFrontend")))
                    yield return candidate;

                directory = directory.Parent;
            }
        }
    }

    private static bool IsFallbackShellFile(string path) =>
        string.Equals(Path.GetFileName(path), FallbackShellFileName, StringComparison.OrdinalIgnoreCase);

    private static bool IsHtmlFile(string path)
    {
        var extension = Path.GetExtension(path);
        return string.Equals(extension, ".html", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(extension, ".htm", StringComparison.OrdinalIgnoreCase);
    }
}
