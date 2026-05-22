using System.Text;
using BookOfEternityClient.Core;

namespace BookOfEternityClient.Services;

public sealed record LocalMediaReference(
    string MediaId,
    string Url,
    string RelativePath,
    string FileName,
    string ContentType,
    long Length,
    DateTimeOffset ModifiedAtUtc);

public sealed record LocalMediaFile(
    string FullPath,
    string RelativePath,
    string ContentType,
    long Length,
    DateTimeOffset ModifiedAtUtc);

public sealed class LocalMediaService
{
    private static readonly string[] AllowedRootRelativePaths = ["images", "output"];
    private readonly FileSystemManager _fs;

    public LocalMediaService(FileSystemManager fs)
    {
        _fs = fs;
    }

    public IReadOnlyList<LocalMediaReference> EnumerateGallery(int maxItems = 80)
    {
        var imagesRoot = _fs.ResolvePath("images");
        if (!Directory.Exists(imagesRoot))
            return [];

        return Directory.EnumerateFiles(imagesRoot, "*.*", SearchOption.AllDirectories)
            .Where(IsSupportedImageFile)
            .OrderByDescending(File.GetLastWriteTimeUtc)
            .ThenBy(static path => path, StringComparer.OrdinalIgnoreCase)
            .Take(Math.Max(1, maxItems))
            .Select(TryCreateReference)
            .OfType<LocalMediaReference>()
            .ToList();
    }

    public LocalMediaReference? TryCreateReference(string fullPath)
    {
        if (!TryBuildMediaFile(fullPath, out var mediaFile, out _))
            return null;

        var resolved = mediaFile!;
        return new LocalMediaReference(
            CreateMediaIdForRelativePath(resolved.RelativePath),
            "/api/media/" + Uri.EscapeDataString(CreateMediaIdForRelativePath(resolved.RelativePath)),
            resolved.RelativePath,
            Path.GetFileName(resolved.RelativePath),
            resolved.ContentType,
            resolved.Length,
            resolved.ModifiedAtUtc);
    }

    public bool TryResolveMediaId(string mediaId, out LocalMediaFile? mediaFile, out string error)
    {
        mediaFile = null;
        error = string.Empty;

        if (!TryDecodeMediaId(mediaId, out var relativePath))
        {
            error = "Некорректный media-id.";
            return false;
        }

        if (!IsSafeRelativePath(relativePath))
        {
            error = "Путь изображения не входит в разрешённые media-корни.";
            return false;
        }

        var fullPath = Path.GetFullPath(_fs.ResolvePath(relativePath));
        if (!TryBuildMediaFile(fullPath, out mediaFile, out error))
            return false;

        return true;
    }

    public static string CreateMediaIdForRelativePath(string relativePath)
    {
        var normalized = NormalizeRelativePathText(relativePath);
        var bytes = Encoding.UTF8.GetBytes(normalized);
        return Convert.ToBase64String(bytes)
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');
    }

    private bool TryBuildMediaFile(string fullPath, out LocalMediaFile? mediaFile, out string error)
    {
        mediaFile = null;
        error = string.Empty;

        string normalizedFullPath;
        try
        {
            normalizedFullPath = Path.GetFullPath(fullPath);
        }
        catch (Exception ex) when (ex is ArgumentException or NotSupportedException or PathTooLongException)
        {
            error = "Некорректный путь изображения.";
            return false;
        }

        if (!IsUnderAllowedRoot(normalizedFullPath, out var relativePath))
        {
            error = "Путь изображения не входит в разрешённые media-корни.";
            return false;
        }

        if (!IsSupportedImageFile(normalizedFullPath))
        {
            error = "Формат файла не поддерживается как изображение.";
            return false;
        }

        if (!File.Exists(normalizedFullPath))
        {
            error = "Изображение не найдено.";
            return false;
        }

        var info = new FileInfo(normalizedFullPath);
        mediaFile = new LocalMediaFile(
            normalizedFullPath,
            relativePath,
            ResolveContentType(normalizedFullPath),
            info.Length,
            info.LastWriteTimeUtc);
        return true;
    }

    private bool IsUnderAllowedRoot(string fullPath, out string relativePath)
    {
        relativePath = string.Empty;
        foreach (var rootRelativePath in AllowedRootRelativePaths)
        {
            var root = Path.GetFullPath(_fs.ResolvePath(rootRelativePath));
            if (!IsSubPathOf(fullPath, root))
                continue;

            var relativeToSession = Path.GetRelativePath(_fs.GameSessionPath, fullPath);
            relativePath = NormalizeRelativePathText(relativeToSession);
            return true;
        }

        return false;
    }

    private bool IsSafeRelativePath(string relativePath)
    {
        var normalized = NormalizeRelativePathText(relativePath);
        if (string.IsNullOrWhiteSpace(normalized) ||
            Path.IsPathRooted(normalized) ||
            normalized.Split('/').Any(static part => part is "" or "." or ".."))
        {
            return false;
        }

        var fullPath = Path.GetFullPath(_fs.ResolvePath(normalized));
        return IsUnderAllowedRoot(fullPath, out _);
    }

    private static bool TryDecodeMediaId(string mediaId, out string relativePath)
    {
        relativePath = string.Empty;
        if (string.IsNullOrWhiteSpace(mediaId))
            return false;

        try
        {
            var base64 = mediaId.Trim().Replace('-', '+').Replace('_', '/');
            base64 = base64.PadRight(base64.Length + (4 - base64.Length % 4) % 4, '=');
            relativePath = NormalizeRelativePathText(Encoding.UTF8.GetString(Convert.FromBase64String(base64)));
            return true;
        }
        catch (FormatException)
        {
            return false;
        }
    }

    private static bool IsSubPathOf(string fullPath, string rootPath)
    {
        var normalizedRoot = rootPath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar;
        var normalizedFull = fullPath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        return normalizedFull.StartsWith(normalizedRoot, StringComparison.OrdinalIgnoreCase);
    }

    private static string NormalizeRelativePathText(string value) =>
        value.Trim().Replace('\\', '/').TrimStart('/');

    private static bool IsSupportedImageFile(string path)
    {
        var ext = Path.GetExtension(path).ToLowerInvariant();
        return ext is ".png" or ".jpg" or ".jpeg" or ".gif" or ".bmp" or ".webp";
    }

    private static string ResolveContentType(string path)
    {
        var ext = Path.GetExtension(path).ToLowerInvariant();
        return ext switch
        {
            ".png" => "image/png",
            ".jpg" or ".jpeg" => "image/jpeg",
            ".gif" => "image/gif",
            ".bmp" => "image/bmp",
            ".webp" => "image/webp",
            _ => "application/octet-stream"
        };
    }
}
