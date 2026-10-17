namespace BookOfEternityClient.Tests;

// Test-only body inventory. Kept explicit so a moved monolith guard cannot silently
// inspect only a router or a zero-row wildcard. This does not select GM reading.
internal static class GmDocumentationBodyFiles
{
    private static readonly string[] Topics =
        ["Lifecycle", "Scheduler", "Guardian_Contracts", "Abodes_Residents", "Shining_Actions",
            "Spiritual_Conflict", "Effects_Resources", "Actor_Profiles", "Story_Memory", "Wound_Continuation"];

    internal static bool IsThematicExample(string file) =>
        Topics.Any(topic => file == "E_CLI_Afterlife_" + topic + ".txt");

    internal static string Read(string root, string relativePath)
    {
        var relative = relativePath.Replace('\\', '/');
        var rules = relative == "OtherGuides/Afterlife_Contract_Matrix.md";
        var examples = relative == "Examples/E_CLI_Afterlife_Turns.txt";
        if (!rules && !examples) return File.ReadAllText(Path.Combine(root, relative));
        var files = new[] { relative, "OtherGuides/GM_Common_Core.md" }.Concat(Topics.Select(topic =>
            rules ? "OtherGuides/Afterlife_" + topic + ".md" : "Examples/E_CLI_Afterlife_" + topic + ".txt"));
        return string.Join("\n", files.Select(path =>
        {
            var text = File.ReadAllText(Path.Combine(root, path));
            if (!text.Contains("<!-- GM1557:", StringComparison.Ordinal))
                throw new InvalidDataException("Missing actual mapped body in " + path);
            return text;
        }));
    }
}
