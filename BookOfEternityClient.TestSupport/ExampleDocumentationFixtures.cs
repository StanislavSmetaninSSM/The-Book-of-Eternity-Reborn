using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;

namespace BookOfEternityClient.Tests;

internal static class ExampleSnippetExtractor
{
    private static readonly Regex XmlContentRegex = new(
        @"<content\b[^>]*type\s*=\s*""json(?:_fragment)?""[^>]*>(?<body>[\s\S]*?)</content>",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    public static IEnumerable<ExampleSnippet> ExtractAll()
    {
        var examplesRoot = Path.Combine(TestRepoPaths.RepoRoot, "Examples");
        foreach (var filePath in Directory.EnumerateFiles(examplesRoot, "*.*", SearchOption.TopDirectoryOnly)
                     .Where(path => path.EndsWith(".txt", StringComparison.OrdinalIgnoreCase) ||
                                    path.EndsWith(".md", StringComparison.OrdinalIgnoreCase))
                     .OrderBy(path => path, StringComparer.OrdinalIgnoreCase))
        {
            foreach (var snippet in ExtractMarkdownJsonFences(filePath))
                yield return snippet;

            foreach (var snippet in ExtractXmlJsonContent(filePath))
                yield return snippet;
        }
    }

    private static IEnumerable<ExampleSnippet> ExtractMarkdownJsonFences(string filePath)
    {
        var lines = File.ReadAllLines(filePath);
        for (var i = 0; i < lines.Length; i++)
        {
            if (!Regex.IsMatch(lines[i], @"^\s*```json\s*$", RegexOptions.IgnoreCase))
                continue;

            var body = new StringBuilder();
            var bodyStartLine = i + 2;
            var j = i + 1;
            for (; j < lines.Length; j++)
            {
                if (Regex.IsMatch(lines[j], @"^\s*```\s*$"))
                    break;

                body.AppendLine(lines[j]);
            }

            yield return new ExampleSnippet(
                NormalizeFile(filePath),
                bodyStartLine,
                "markdown-json-fence",
                body.ToString(),
                InferExpectedFromLines(lines, i));

            i = j;
        }
    }

    private static IEnumerable<ExampleSnippet> ExtractXmlJsonContent(string filePath)
    {
        var text = File.ReadAllText(filePath);
        foreach (Match match in XmlContentRegex.Matches(text))
        {
            var line = CountLineNumber(text, match.Index);
            yield return new ExampleSnippet(
                NormalizeFile(filePath),
                line,
                "xml-json-content",
                match.Groups["body"].Value,
                InferExpectedFromXmlContext(text, match.Index));
        }
    }

    private static ExampleExpected InferExpectedFromLines(string[] lines, int fenceLineIndex)
    {
        var start = Math.Max(0, fenceLineIndex - 12);
        var context = string.Join('\n', lines.Skip(start).Take(fenceLineIndex - start + 1));
        return ContainsInvalidMarker(context) ? ExampleExpected.Invalid : ExampleExpected.Valid;
    }

    private static ExampleExpected InferExpectedFromXmlContext(string text, int contentIndex)
    {
        var before = text[..contentIndex];
        var lastExampleStart = LastIndexOfIgnoreCase(before, "<example");
        var lastUpperExampleStart = LastIndexOfIgnoreCase(before, "<Example");
        lastExampleStart = Math.Max(lastExampleStart, lastUpperExampleStart);
        var lastExampleEnd = LastIndexOfIgnoreCase(before, "</example");

        if (lastExampleStart > lastExampleEnd)
        {
            var tagEnd = text.IndexOf('>', lastExampleStart);
            if (tagEnd > lastExampleStart)
            {
                var tag = text.Substring(lastExampleStart, tagEnd - lastExampleStart + 1);
                if (Regex.IsMatch(tag, @"type\s*=\s*""bad""", RegexOptions.IgnoreCase))
                    return ExampleExpected.Invalid;
            }
        }

        var contextStart = Math.Max(0, contentIndex - 600);
        var context = text.Substring(contextStart, contentIndex - contextStart);
        return ContainsInvalidMarker(context) ? ExampleExpected.Invalid : ExampleExpected.Valid;
    }

    private static bool ContainsInvalidMarker(string context) =>
        context.Contains("INVALID", StringComparison.OrdinalIgnoreCase) ||
        context.Contains("INCORRECT", StringComparison.OrdinalIgnoreCase) ||
        context.Contains("VIOLATION", StringComparison.OrdinalIgnoreCase) ||
        context.Contains("НЕПРАВ", StringComparison.OrdinalIgnoreCase);

    private static int LastIndexOfIgnoreCase(string text, string value) =>
        text.LastIndexOf(value, StringComparison.OrdinalIgnoreCase);

    private static int CountLineNumber(string text, int index) =>
        text.Take(index).Count(ch => ch == '\n') + 1;

    private static string NormalizeFile(string filePath) =>
        Path.GetRelativePath(Path.Combine(TestRepoPaths.RepoRoot, "Examples"), filePath)
            .Replace('\\', '/');
}
internal sealed record ExampleSnippet(
    string File,
    int Line,
    string Kind,
    string RawText,
    ExampleExpected Expected)
{
    public string Location => $"Examples/{File}:{Line}";
}

internal enum ExampleExpected
{
    Valid,
    Invalid
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
internal sealed class ExampleValidationManifest
{
    public int Version { get; set; }
    public List<ExampleSyntaxExemption> SyntaxExemptions { get; set; } = new();
    public List<ExampleSyntaxExemption> ShapeExemptions { get; set; } = new();
    public List<InkFeatherReceiptCoverage> InkFeatherReceiptCoverage { get; set; } = new();
    public List<ExampleRuntimeScenario> RuntimeScenarios { get; set; } = new();
    public List<AfterlifeExampleCoverage> AfterlifeExampleCoverage { get; set; } = new();
    public List<ActorMaterializationExampleCoverage> MortalActorMaterializationCoverage { get; set; } = new();
    public List<ActorMaterializationExampleCoverage> MortalNpcCoreChangesCoverage { get; set; } = new();
    public List<ActorMaterializationExampleCoverage> AfterlifeEntityProfileCoverage { get; set; } = new();
    public List<ActorMaterializationExampleCoverage> FactionMaterializationCoverage { get; set; } = new();
    public List<ActorMaterializationExampleCoverage> MortalItemMaterializationCoverage { get; set; } = new();
    public List<ActorMaterializationExampleCoverage> MortalLocationMaterializationCoverage { get; set; } = new();
    public List<ExampleContractCoverage> TrainingShowcaseCoverage { get; set; } = new();
    public List<ExampleContractCoverage> GmWorkerBridgeCoverage { get; set; } = new();

    public static ExampleValidationManifest Load()
    {
        var path = Path.Combine(TestRepoPaths.RepoRoot, "Examples", "example_validation_manifest.json");
        var manifest = JsonSerializer.Deserialize<ExampleValidationManifest>(
            File.ReadAllText(path),
            new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            }) ?? throw new InvalidOperationException($"Failed to read example validation manifest: {path}");

        if (manifest.Version != 1)
            throw new InvalidOperationException($"Unsupported example validation manifest version: {manifest.Version}");

        return manifest;
    }

    public bool IsSyntaxExempt(ExampleSnippet snippet) =>
        SyntaxExemptions.Any(exemption => exemption.Matches(snippet));

    public bool IsShapeExempt(ExampleSnippet snippet) =>
        ShapeExemptions.Any(exemption => exemption.Matches(snippet));
}

internal sealed class ExampleContractCoverage
{
    public string ContractId { get; set; } = "";
    public string File { get; set; } = "";
    public string StatePath { get; set; } = "";
    public string ResponseSurface { get; set; } = "";
    public string Description { get; set; } = "";
}

internal sealed class ActorMaterializationExampleCoverage
{
    public string ContractId { get; set; } = "";
    public string File { get; set; } = "";
    public string StatePath { get; set; } = "";
    public string ResponseSurface { get; set; } = "";
    public string Description { get; set; } = "";
    public string[] Realms { get; set; } = [];
    public string ValidationKind { get; set; } = "";
    public string ValidationRoute { get; set; } = "";
    public string FocusedFragmentReason { get; set; } = "";
    public string CoverageLimit { get; set; } = "";
    public string[] RequiredText { get; set; } = [];
    public string[] ExpectedDiagnostics { get; set; } = [];
}

internal sealed class InkFeatherReceiptCoverage
{
    public string ActionTag { get; set; } = "";
    public string File { get; set; } = "";
    public string CoverageKind { get; set; } = "";
    public string ExemptionReason { get; set; } = "";
}

internal sealed class ExampleSyntaxExemption
{
    public string File { get; set; } = "";
    public int? Line { get; set; }
    public string[] RequiredText { get; set; } = [];
    public string Reason { get; set; } = "";

    public bool Matches(ExampleSnippet snippet)
    {
        if (!string.Equals(File, snippet.File, StringComparison.OrdinalIgnoreCase))
            return false;

        if (Line.HasValue && Line.Value != snippet.Line)
            return false;

        return RequiredText.All(text => snippet.RawText.Contains(text, StringComparison.Ordinal));
    }

    public override string ToString() =>
        $"Examples/{File}:{Line?.ToString() ?? "*"} ({Reason})";
}

internal sealed class ExampleRuntimeScenario
{
    public string Id { get; set; } = "";
    public string File { get; set; } = "";
    public string Runner { get; set; } = "";
    public string[] RequiredText { get; set; } = [];
    public string BaselineKind { get; set; } = "";
    public List<ExampleRuntimePreStateFile> PreStateFiles { get; set; } = new();
    public List<string> PendingSnapshotFiles { get; set; } = new();
    public string PendingSnapshotSourceLabel { get; set; } = "";
    public List<ExampleRuntimeCompanionFile> CompanionFiles { get; set; } = new();
    public string[] ExpectedModifiedFiles { get; set; } = [];
    public string[] ExpectedFilesAbsent { get; set; } = [];
    public string[] ExpectedFilesUnchanged { get; set; } = [];
    public List<ExampleRuntimeFileContainsAssertion> ExpectedFileContains { get; set; } = new();
    public List<ExampleRuntimeFileDoesNotContainAssertion> ExpectedFileDoesNotContain { get; set; } = new();

    public bool Matches(ExampleSnippet snippet)
    {
        if (!string.Equals(File, snippet.File, StringComparison.OrdinalIgnoreCase))
            return false;

        return RequiredText.All(text => snippet.RawText.Contains(text, StringComparison.Ordinal));
    }
}

internal sealed class AfterlifeExampleCoverage
{
    public int ExampleNumber { get; set; }
    public string[] RuntimeScenarioIds { get; set; } = [];
    public string ExemptionReason { get; set; } = "";
}

internal sealed class ExampleRuntimePreStateFile
{
    public string Path { get; set; } = "";
    public JsonElement Content { get; set; }
}

internal sealed class ExampleRuntimeCompanionFile
{
    public string Path { get; set; } = "";
    public string File { get; set; } = "";
    public string[] RequiredText { get; set; } = [];

    public bool Matches(ExampleSnippet snippet)
    {
        if (!string.Equals(File, snippet.File, StringComparison.OrdinalIgnoreCase))
            return false;

        return RequiredText.All(text => snippet.RawText.Contains(text, StringComparison.Ordinal));
    }
}

internal sealed class ExampleRuntimeFileContainsAssertion
{
    public string Path { get; set; } = "";
    public string[] RequiredText { get; set; } = [];
}

internal sealed class ExampleRuntimeFileDoesNotContainAssertion
{
    public string Path { get; set; } = "";
    public string[] ForbiddenText { get; set; } = [];
}
