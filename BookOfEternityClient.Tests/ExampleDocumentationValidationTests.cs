using System.Reflection;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using BookOfEternityClient.Configuration;
using BookOfEternityClient.Core;
using BookOfEternityClient.IO;
using BookOfEternityClient.Models;
using BookOfEternityClient.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed class ExampleDocumentationValidationTests
{
    private static readonly JsonDocumentOptions DocumentOptions = new()
    {
        AllowTrailingCommas = true,
        CommentHandling = JsonCommentHandling.Skip
    };

    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        AllowTrailingCommas = true,
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip
    };

    [Fact]
    public void JsonExamples_AreParseableOrExplicitlyExempted()
    {
        var manifest = ExampleValidationManifest.Load();
        var snippets = ExampleSnippetExtractor.ExtractAll().ToArray();

        Assert.NotEmpty(snippets);

        var failures = new List<string>();
        foreach (var snippet in snippets)
        {
            if (!TryBuildJsonDocument(snippet.RawText, out _, out var parseMode, out var error) &&
                !manifest.IsSyntaxExempt(snippet))
            {
                failures.Add($"{snippet.Location}: {parseMode}: {error}");
            }
        }

        Assert.True(
            failures.Count == 0,
            "Every JSON-labeled example in Examples/ must parse as JSON, JSON fragment, or documented exemption." +
            Environment.NewLine +
            string.Join(Environment.NewLine, failures.Take(50)));

        var staleExemptions = manifest.SyntaxExemptions
            .Where(exemption => !snippets.Any(exemption.Matches))
            .Select(exemption => exemption.ToString())
            .ToArray();

        Assert.True(
            staleExemptions.Length == 0,
            "Stale example syntax exemptions must be removed or updated." +
            Environment.NewLine +
            string.Join(Environment.NewLine, staleExemptions));

        var staleShapeExemptions = manifest.ShapeExemptions
            .Where(exemption => !snippets.Any(exemption.Matches))
            .Select(exemption => exemption.ToString())
            .ToArray();

        Assert.True(
            staleShapeExemptions.Length == 0,
            "Stale example shape exemptions must be removed or updated." +
            Environment.NewLine +
            string.Join(Environment.NewLine, staleShapeExemptions));
    }

    [Fact]
    public void GameResponseShapedExamples_DoNotUseUnknownTopLevelFields()
    {
        var manifest = ExampleValidationManifest.Load();
        var snippets = ExampleSnippetExtractor.ExtractAll().ToArray();
        var knownResponseFields = GetKnownGameResponseFields();

        var failures = new List<string>();

        foreach (var snippet in snippets)
        {
            if (snippet.Expected == ExampleExpected.Invalid ||
                manifest.IsSyntaxExempt(snippet) ||
                manifest.IsShapeExempt(snippet) ||
                !TryBuildJsonDocument(snippet.RawText, out var normalizedJson, out _, out _) ||
                !TryGetObjectProperties(normalizedJson, out var propertyNames) ||
                !LooksLikeGameResponse(propertyNames, knownResponseFields))
            {
                continue;
            }

            var unknownFields = propertyNames
                .Where(field => !knownResponseFields.Contains(field))
                .OrderBy(field => field, StringComparer.Ordinal)
                .ToArray();

            if (unknownFields.Length > 0)
            {
                failures.Add($"{snippet.Location}: unknown GameResponse fields: {string.Join(", ", unknownFields)}");
            }
        }

        Assert.True(
            failures.Count == 0,
            "Examples that look like GM GameResponse JSON must not advertise unsupported top-level fields." +
            Environment.NewLine +
            string.Join(Environment.NewLine, failures.Take(50)));
    }

    [Fact]
    public async Task RuntimeManifestScenarios_DistributeThroughClientSurfaces()
    {
        var manifest = ExampleValidationManifest.Load();
        var snippets = ExampleSnippetExtractor.ExtractAll().ToArray();
        var knownResponseFields = GetKnownGameResponseFields();
        var failures = new List<string>();

        foreach (var scenario in manifest.RuntimeScenarios)
        {
            var matches = snippets.Where(scenario.Matches).ToArray();
            if (matches.Length != 1)
            {
                failures.Add($"{scenario.Id}: expected exactly one matching snippet, found {matches.Length}.");
                continue;
            }

            var snippet = matches[0];
            if (!TryBuildJsonDocument(snippet.RawText, out var normalizedJson, out _, out var parseError))
            {
                failures.Add($"{scenario.Id}: scenario JSON is not parseable at {snippet.Location}: {parseError}");
                continue;
            }

            if (!TryGetObjectProperties(normalizedJson, out var propertyNames))
            {
                failures.Add($"{scenario.Id}: scenario at {snippet.Location} must be a JSON object.");
                continue;
            }

            var unknownFields = propertyNames
                .Where(field => !knownResponseFields.Contains(field))
                .OrderBy(field => field, StringComparer.Ordinal)
                .ToArray();
            if (unknownFields.Length > 0)
            {
                failures.Add($"{scenario.Id}: unsupported GameResponse fields at {snippet.Location}: {string.Join(", ", unknownFields)}");
                continue;
            }

            var response = JsonSerializer.Deserialize<GameResponse>(normalizedJson, SerializerOptions);
            if (response == null)
            {
                failures.Add($"{scenario.Id}: failed to deserialize GameResponse at {snippet.Location}.");
                continue;
            }

            if (!string.Equals(scenario.Runner, "gameResponseDistribution", StringComparison.Ordinal) &&
                !string.Equals(scenario.Runner, "acceptedTurnDistribution", StringComparison.Ordinal))
            {
                failures.Add($"{scenario.Id}: unsupported example runtime runner '{scenario.Runner}'.");
                continue;
            }

            var tempRoot = Path.Combine(Path.GetTempPath(), "boe-example-doc-" + scenario.Id + "-" + Guid.NewGuid().ToString("N"));
            try
            {
                CopyDirectory(TestRepoPaths.BaseSessionRoot, Path.Combine(tempRoot, "game_session"));
                var fs = new FileSystemManager(tempRoot, NullLogger<FileSystemManager>.Instance);
                await ApplyScenarioPreStateFilesAsync(fs, scenario.PreStateFiles);
                var unchangedBefore = await SnapshotScenarioFilesAsync(fs, scenario.ExpectedFilesUnchanged);

                var distributor = new StateDistributor(fs, NullLogger<StateDistributor>.Instance);

                var modifiedFiles = await distributor.DistributeAsync(response);
                var normalizedModifiedFiles = modifiedFiles
                    .Select(NormalizeSeparators)
                    .ToHashSet(StringComparer.OrdinalIgnoreCase);

                foreach (var expectedFile in scenario.ExpectedModifiedFiles)
                {
                    if (!normalizedModifiedFiles.Contains(NormalizeSeparators(expectedFile)))
                    {
                        failures.Add($"{scenario.Id}: expected distribution to modify '{expectedFile}', actual: {string.Join(", ", normalizedModifiedFiles)}");
                    }
                }

                foreach (var expectedFile in scenario.ExpectedFilesAbsent)
                {
                    if (File.Exists(fs.ResolvePath(expectedFile)))
                        failures.Add($"{scenario.Id}: expected '{expectedFile}' to remain absent.");
                }

                foreach (var (relativePath, beforeContent) in unchangedBefore)
                {
                    var afterContent = await ReadScenarioFileAsync(fs, relativePath);
                    if (!string.Equals(beforeContent, afterContent, StringComparison.Ordinal))
                        failures.Add($"{scenario.Id}: expected '{relativePath}' to remain unchanged.");
                }

                foreach (var assertion in scenario.ExpectedFileContains)
                {
                    var content = await ReadScenarioFileAsync(fs, assertion.Path);
                    if (content == null)
                    {
                        failures.Add($"{scenario.Id}: expected '{assertion.Path}' to exist.");
                        continue;
                    }

                    foreach (var requiredText in assertion.RequiredText)
                    {
                        if (!content.Contains(requiredText, StringComparison.Ordinal))
                            failures.Add($"{scenario.Id}: expected '{assertion.Path}' to contain '{requiredText}'.");
                    }
                }

                foreach (var assertion in scenario.ExpectedFileDoesNotContain)
                {
                    var content = await ReadScenarioFileAsync(fs, assertion.Path);
                    if (content == null)
                        continue;

                    foreach (var forbiddenText in assertion.ForbiddenText)
                    {
                        if (content.Contains(forbiddenText, StringComparison.Ordinal))
                            failures.Add($"{scenario.Id}: expected '{assertion.Path}' not to contain '{forbiddenText}'.");
                    }
                }

                if (string.Equals(scenario.Runner, "acceptedTurnDistribution", StringComparison.Ordinal))
                    failures.AddRange(await RunAcceptedTurnScenarioValidationAsync(fs, scenario.Id));

                if (response.Response != null &&
                    !File.Exists(Path.Combine(tempRoot, "game_session", "output", "narrative_response.json")))
                {
                    failures.Add($"{scenario.Id}: response text did not produce output/narrative_response.json.");
                }

                if (response.GmThoughtsMarkdown != null &&
                    !File.Exists(Path.Combine(tempRoot, "game_session", "output", "debug_logs.json")))
                {
                    failures.Add($"{scenario.Id}: gm_thoughts_markdown did not produce output/debug_logs.json.");
                }
            }
            finally
            {
                try
                {
                    if (Directory.Exists(tempRoot))
                        Directory.Delete(tempRoot, recursive: true);
                }
                catch
                {
                    // Best-effort cleanup for test temp directories.
                }
            }
        }

        Assert.True(
            failures.Count == 0,
            "Manifest-backed example runtime scenarios must execute through the client distribution surfaces." +
            Environment.NewLine +
            string.Join(Environment.NewLine, failures));
    }

    [Fact]
    public void InkFeatherActionReceiptExamples_IncludeRequiredContractFields()
    {
        var manifest = ExampleValidationManifest.Load();
        var snippets = ExampleSnippetExtractor.ExtractAll().ToArray();
        var failures = new List<string>();

        foreach (var snippet in snippets)
        {
            if (snippet.Expected == ExampleExpected.Invalid ||
                manifest.IsSyntaxExempt(snippet) ||
                !TryBuildJsonDocument(snippet.RawText, out var normalizedJson, out _, out _) ||
                !TryParseJsonObject(normalizedJson, out var root) ||
                !root.TryGetProperty("actionTag", out var actionTagElement))
            {
                continue;
            }

            var missing = new[]
                {
                    "sessionId",
                    "requestId",
                    "turnNumber",
                    "actionTag",
                    "resolved",
                    "costInFeathers",
                    "resolutionType",
                    "summary",
                    "stateEvidence"
                }
                .Where(field => !root.TryGetProperty(field, out _))
                .ToArray();

            if (missing.Length > 0)
            {
                failures.Add($"{snippet.Location}: ink-feather receipt is missing required fields: {string.Join(", ", missing)}");
                continue;
            }

            if (actionTagElement.GetString() is "ABODE_OFFERING" &&
                root.TryGetProperty("stateEvidence", out var stateEvidence) &&
                stateEvidence.ValueKind == JsonValueKind.Object)
            {
                var missingEvidence = new[] { "powerGain", "powerEventId" }
                    .Where(field => !stateEvidence.TryGetProperty(field, out _))
                    .ToArray();
                if (missingEvidence.Length > 0)
                    failures.Add($"{snippet.Location}: ABODE_OFFERING receipt stateEvidence is missing: {string.Join(", ", missingEvidence)}");
            }
        }

        Assert.True(
            failures.Count == 0,
            "Ink Feather action receipt examples must match the output/ink_feather_action_result.json contract." +
            Environment.NewLine +
            string.Join(Environment.NewLine, failures.Take(50)));
    }

    private static bool LooksLikeGameResponse(IReadOnlyCollection<string> propertyNames, ISet<string> knownResponseFields)
    {
        if (propertyNames.Count == 0)
            return false;

        var knownCount = propertyNames.Count(knownResponseFields.Contains);
        if (knownCount == 0)
            return false;

        return propertyNames.Contains("response") ||
               propertyNames.Contains("gm_thoughts_markdown") ||
               propertyNames.Any(FileMapping.FieldToFile.ContainsKey);
    }

    private static HashSet<string> GetKnownGameResponseFields()
    {
        var fields = typeof(GameResponse)
            .GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Select(property => property.GetCustomAttribute<JsonPropertyNameAttribute>()?.Name ?? property.Name)
            .ToHashSet(StringComparer.Ordinal);

        fields.UnionWith(FileMapping.OutputOnlyResponseFields);
        fields.UnionWith(FileMapping.FieldToFile.Keys);
        return fields;
    }

    private static bool TryGetObjectProperties(string json, out string[] propertyNames)
    {
        try
        {
            using var document = JsonDocument.Parse(json, DocumentOptions);
            if (document.RootElement.ValueKind != JsonValueKind.Object)
            {
                propertyNames = [];
                return false;
            }

            propertyNames = document.RootElement
                .EnumerateObject()
                .Select(property => property.Name)
                .ToArray();
            return true;
        }
        catch
        {
            propertyNames = [];
            return false;
        }
    }

    private static bool TryParseJsonObject(string json, out JsonElement root)
    {
        try
        {
            using var document = JsonDocument.Parse(json, DocumentOptions);
            if (document.RootElement.ValueKind != JsonValueKind.Object)
            {
                root = default;
                return false;
            }

            root = document.RootElement.Clone();
            return true;
        }
        catch
        {
            root = default;
            return false;
        }
    }

    private static bool TryBuildJsonDocument(string rawText, out string normalizedJson, out string parseMode, out string? error)
    {
        var cleaned = CleanJsonLikeText(rawText);
        var candidates = new List<(string Mode, string Text)> { ("json", cleaned) };

        if (NeedsObjectWrapper(cleaned))
            candidates.Add(("json-fragment", "{" + Environment.NewLine + cleaned + Environment.NewLine + "}"));

        foreach (var (mode, text) in candidates)
        {
            if (TryParseSingleJson(text, out error))
            {
                normalizedJson = text;
                parseMode = mode;
                error = null;
                return true;
            }
        }

        if (TryParseJsonSequence(cleaned, out error))
        {
            normalizedJson = cleaned;
            parseMode = "json-sequence";
            error = null;
            return true;
        }

        normalizedJson = "";
        parseMode = "json";
        return false;
    }

    private static string CleanJsonLikeText(string rawText)
    {
        var text = rawText.Trim();
        text = Regex.Replace(text, @"^\s*<!\[CDATA\[", "", RegexOptions.IgnoreCase);
        text = Regex.Replace(text, @"\]\]>\s*$", "", RegexOptions.IgnoreCase);
        return text.Trim();
    }

    private static bool NeedsObjectWrapper(string text)
    {
        var trimmed = text.TrimStart();
        return trimmed.StartsWith("\"", StringComparison.Ordinal) ||
               trimmed.StartsWith("//", StringComparison.Ordinal);
    }

    private static bool TryParseSingleJson(string text, out string? error)
    {
        try
        {
            using var _ = JsonDocument.Parse(text, DocumentOptions);
            error = null;
            return true;
        }
        catch (JsonException ex)
        {
            error = ex.Message;
            return false;
        }
    }

    private static bool TryParseJsonSequence(string text, out string? error)
    {
        try
        {
            var bytes = Encoding.UTF8.GetBytes(text);
            var reader = new Utf8JsonReader(bytes, new JsonReaderOptions
            {
                AllowTrailingCommas = true,
                CommentHandling = JsonCommentHandling.Skip
            });

            var values = 0;
            while (reader.Read())
            {
                using var _ = JsonDocument.ParseValue(ref reader);
                values++;
            }

            error = null;
            return values > 0;
        }
        catch (JsonException ex)
        {
            error = ex.Message;
            return false;
        }
    }

    private static string NormalizeSeparators(string path) =>
        path.Replace('\\', '/');

    private static async Task ApplyScenarioPreStateFilesAsync(
        FileSystemManager fs,
        IReadOnlyList<ExampleRuntimePreStateFile> preStateFiles)
    {
        foreach (var file in preStateFiles)
        {
            var content = JsonSerializer.Serialize(file.Content, SharedJsonOptions.PrettyCamelCaseUnsafeRelaxed);
            await fs.WriteFileAtomicAsync(file.Path, content);
        }
    }

    private static async Task<Dictionary<string, string?>> SnapshotScenarioFilesAsync(
        FileSystemManager fs,
        IReadOnlyList<string> relativePaths)
    {
        var snapshots = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
        foreach (var relativePath in relativePaths)
            snapshots[relativePath] = await ReadScenarioFileAsync(fs, relativePath);
        return snapshots;
    }

    private static async Task<string?> ReadScenarioFileAsync(FileSystemManager fs, string relativePath)
    {
        if (!File.Exists(fs.ResolvePath(relativePath)))
            return null;

        return await fs.ReadFileAsync(relativePath);
    }

    private static async Task<List<string>> RunAcceptedTurnScenarioValidationAsync(
        FileSystemManager fs,
        string scenarioId)
    {
        var validator = new ValidationService(fs, NullLogger<ValidationService>.Instance);
        var issues = await validator.ValidateGameStateAsync();
        issues.AddRange(await validator.ValidateAcceptedTurnNarrativePayloadAsync());
        issues.AddRange(await validator.ValidateAcceptedTurnInterfacePayloadAsync());
        issues.AddRange(await validator.ValidateAcceptedTurnReasoningAsync());
        issues.AddRange(await validator.ValidateAcceptedTurnSpecialActionOutcomesAsync());
        issues.AddRange(await validator.ValidateAcceptedTurnQteOfferAsync());
        issues.AddRange(await validator.ValidatePendingMemoryLegacyApplicationAsync());

        return issues
            .Where(issue => issue.Severity == IssueSeverity.Error)
            .Select(issue => $"{scenarioId}: accepted-turn validation error {issue.Code}: {issue.Message}")
            .ToList();
    }

    private static void CopyDirectory(string sourceDir, string destinationDir)
    {
        Directory.CreateDirectory(destinationDir);

        foreach (var directory in Directory.GetDirectories(sourceDir, "*", SearchOption.AllDirectories))
            Directory.CreateDirectory(directory.Replace(sourceDir, destinationDir));

        foreach (var file in Directory.GetFiles(sourceDir, "*", SearchOption.AllDirectories))
            File.Copy(file, file.Replace(sourceDir, destinationDir), overwrite: true);
    }
}

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

internal sealed class ExampleValidationManifest
{
    public int Version { get; set; }
    public List<ExampleSyntaxExemption> SyntaxExemptions { get; set; } = new();
    public List<ExampleSyntaxExemption> ShapeExemptions { get; set; } = new();
    public List<ExampleRuntimeScenario> RuntimeScenarios { get; set; } = new();

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
    public List<ExampleRuntimePreStateFile> PreStateFiles { get; set; } = new();
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

internal sealed class ExampleRuntimePreStateFile
{
    public string Path { get; set; } = "";
    public JsonElement Content { get; set; }
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
