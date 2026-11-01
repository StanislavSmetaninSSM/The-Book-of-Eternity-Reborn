using System.Globalization;

namespace BookOfEternityClient.Tests;

/// <summary>
/// Reads the validation and repair partial family as one ordered source for source guards.
/// </summary>
internal static class ValidationAndRepairSource
{
    private const string BaseName = "GameEngine.ValidationAndRepair";
    private const string ClassHeader = "public partial class GameEngine";

    /// <summary>
    /// Reads one source file, expanding the validation and repair base into its numbered partials.
    /// </summary>
    /// <param name="absolutePath">
    /// The source file to read. The validation and repair base includes its complete family;
    /// other source files are read individually.
    /// </param>
    /// <returns>
    /// Source text with validation and repair members in base-then-numeric-part order.
    /// Class wrappers between those members are removed so method boundaries remain local.
    /// </returns>
    internal static string Read(string absolutePath)
    {
        var paths = GetPaths(absolutePath);
        var source = File.ReadAllText(paths[0]);
        if (paths.Count == 1)
            return source;

        var members = paths.Skip(1).Select(path =>
        {
            var partial = File.ReadAllText(path);
            var header = partial.IndexOf(ClassHeader, StringComparison.Ordinal);
            var openingBrace = header < 0 ? -1 : partial.IndexOf('{', header + ClassHeader.Length);
            var closingBrace = partial.LastIndexOf('}');
            if (openingBrace < 0 || closingBrace <= openingBrace)
                throw new InvalidDataException($"Missing GameEngine partial wrapper in {path}.");
            return partial[(openingBrace + 1)..closingBrace];
        });
        var baseClosingBrace = source.LastIndexOf('}');
        if (baseClosingBrace < 0)
            throw new InvalidDataException($"Missing GameEngine closing brace in {absolutePath}.");

        return source[..baseClosingBrace] + "\n" + string.Join("\n", members) + "\n}";
    }

    /// <summary>
    /// Resolves the physical files owned by a source guard's logical source path.
    /// </summary>
    /// <param name="absolutePath">
    /// The validation and repair base path, or an individual source path.
    /// </param>
    /// <returns>
    /// The base followed by numbered partials in numeric order, or only the requested file.
    /// </returns>
    internal static IReadOnlyList<string> GetPaths(string absolutePath)
    {
        if (!string.Equals(Path.GetFileName(absolutePath), BaseName + ".cs", StringComparison.Ordinal))
            return new[] { absolutePath };

        return new[] { absolutePath }.Concat(Directory.EnumerateFiles(
                Path.GetDirectoryName(absolutePath)!, BaseName + ".Part*.cs")
            .OrderBy(static path => int.Parse(
                Path.GetFileNameWithoutExtension(path)[(BaseName.Length + ".Part".Length)..],
                CultureInfo.InvariantCulture)))
            .ToArray();
    }

    /// <summary>
    /// Maps numbered validation and repair files to their logical base for source inventories.
    /// </summary>
    /// <param name="path">
    /// A source path with either slash convention. Other source paths retain their identity.
    /// </param>
    /// <returns>
    /// The logical base path for a numbered partial, or the original path otherwise.
    /// </returns>
    internal static string GetLogicalPath(string path)
    {
        var name = Path.GetFileNameWithoutExtension(path);
        if (name.StartsWith(BaseName + ".Part", StringComparison.Ordinal) &&
            int.TryParse(name[(BaseName.Length + ".Part".Length)..], out _))
            return Path.Combine(Path.GetDirectoryName(path)!, BaseName + ".cs");
        return path;
    }
}
