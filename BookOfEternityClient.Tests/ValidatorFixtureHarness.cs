using System.Text.Json;
using System.Text.Json.Serialization;
using BookOfEternityClient.Core;
using BookOfEternityClient.Services;
using Microsoft.Extensions.Logging.Abstractions;

namespace BookOfEternityClient.Tests;

internal sealed class ValidatorFixtureHarness : IDisposable
{
    private readonly ValidatorFixtureDefinition _definition;
    private readonly string _fixtureRoot;
    private readonly string _tempRoot;
    private readonly FileSystemManager _fs;
    private readonly ValidationService _validator;
    private readonly CriticalStateHealthService _criticalStateHealth;

    public ValidatorFixtureHarness(ValidatorFixtureDefinition definition)
    {
        _definition = definition;
        _fixtureRoot = Path.Combine(TestRepoPaths.ValidatorFixturesRoot, definition.Id);
        _tempRoot = Path.Combine(Path.GetTempPath(), "boe-fixture-" + definition.Id + "-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempRoot);

        CopyDirectory(TestRepoPaths.BaseSessionRoot, Path.Combine(_tempRoot, "game_session"));
        _fs = new FileSystemManager(_tempRoot, NullLogger<FileSystemManager>.Instance);
        _validator = new ValidationService(_fs, NullLogger<ValidationService>.Instance);
        _criticalStateHealth = new CriticalStateHealthService(_fs, NullLogger<CriticalStateHealthService>.Instance);
    }

    public async Task<FixtureRunResult> RunBrokenAsync()
    {
        await ApplyMappingsAsync(_definition.Shared);
        await ApplyMappingsAsync(_definition.Broken);
        return await ExecuteAsync();
    }

    public async Task<FixtureRunResult> RunFixedAsync()
    {
        await ApplyMappingsAsync(_definition.Shared);
        await ApplyMappingsAsync(_definition.Fixed);
        return await ExecuteAsync();
    }

    private async Task ApplyMappingsAsync(IReadOnlyList<FixtureFileMapping> mappings)
    {
        foreach (var mapping in mappings)
        {
            var sourcePath = Path.Combine(_fixtureRoot, mapping.Source.Replace('/', Path.DirectorySeparatorChar));
            var targetPath = _fs.ResolvePath(mapping.Target);
            Directory.CreateDirectory(Path.GetDirectoryName(targetPath)!);

            switch (mapping.Mode)
            {
                case FixtureOverlayMode.Replace:
                    File.Copy(sourcePath, targetPath, overwrite: true);
                    break;

                case FixtureOverlayMode.MergeObject:
                    var baseJson = await _fs.ReadFileAsync(mapping.Target);
                    using (var patchDoc = JsonDocument.Parse(await File.ReadAllTextAsync(sourcePath)))
                    using (var baseDoc = JsonDocument.Parse(baseJson ?? "{}"))
                    {
                        var merged = MergeTopLevelObjects(baseDoc.RootElement, patchDoc.RootElement);
                        await _fs.WriteFileAtomicAsync(mapping.Target, merged);
                    }
                    break;

                default:
                    throw new InvalidOperationException($"Unsupported fixture overlay mode: {mapping.Mode}");
            }
        }
    }

    private async Task<FixtureRunResult> ExecuteAsync()
    {
        var issues = _definition.Runner switch
        {
            FixtureRunnerKind.StateOnly => await _validator.ValidateGameStateAsync(),
            FixtureRunnerKind.AcceptedTurn => await RunAcceptedTurnValidationAsync(),
            FixtureRunnerKind.CriticalState => await RunCriticalStateValidationAsync(),
            _ => throw new InvalidOperationException($"Unsupported fixture runner: {_definition.Runner}")
        };

        return new FixtureRunResult(
            issues,
            issues.Where(issue => issue.Severity == IssueSeverity.Error && !string.IsNullOrWhiteSpace(issue.Code))
                  .Select(issue => issue.Code!)
                  .Distinct(StringComparer.OrdinalIgnoreCase)
                  .OrderBy(code => code, StringComparer.OrdinalIgnoreCase)
                  .ToArray());
    }

    private async Task<List<ValidationIssue>> RunAcceptedTurnValidationAsync()
    {
        var issues = await _validator.ValidateGameStateAsync();
        issues.AddRange(await _validator.ValidateAcceptedTurnNarrativePayloadAsync());
        issues.AddRange(await _validator.ValidateAcceptedTurnInterfacePayloadAsync());
        issues.AddRange(await _validator.ValidateAcceptedTurnReasoningAsync());
        issues.AddRange(await _validator.ValidateAcceptedTurnSpecialActionOutcomesAsync());
        issues.AddRange(await _validator.ValidateAcceptedTurnQteOfferAsync());
        issues.AddRange(await _validator.ValidatePendingMemoryLegacyApplicationAsync());
        return issues;
    }

    private async Task<List<ValidationIssue>> RunCriticalStateValidationAsync()
    {
        return _definition.CriticalStateMode switch
        {
            CriticalStateMode.Raw => await _criticalStateHealth.ValidateAcceptedTurnRawStateAsync(),
            CriticalStateMode.Canonical => await _criticalStateHealth.ValidateCriticalCanonicalStateAsync(),
            _ => throw new InvalidOperationException($"Unsupported critical-state mode: {_definition.CriticalStateMode}")
        };
    }

    private static string MergeTopLevelObjects(JsonElement baseRoot, JsonElement patchRoot)
    {
        if (baseRoot.ValueKind != JsonValueKind.Object || patchRoot.ValueKind != JsonValueKind.Object)
            throw new InvalidOperationException("MergeObject fixture mappings require JSON object roots.");

        var merged = new Dictionary<string, JsonElement>(StringComparer.OrdinalIgnoreCase);
        foreach (var prop in baseRoot.EnumerateObject())
            merged[prop.Name] = prop.Value.Clone();
        foreach (var prop in patchRoot.EnumerateObject())
            merged[prop.Name] = prop.Value.Clone();

        return JsonSerializer.Serialize(merged, new JsonSerializerOptions
        {
            WriteIndented = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
        });
    }

    private static void CopyDirectory(string sourceDir, string destinationDir)
    {
        Directory.CreateDirectory(destinationDir);

        foreach (var directory in Directory.GetDirectories(sourceDir, "*", SearchOption.AllDirectories))
            Directory.CreateDirectory(directory.Replace(sourceDir, destinationDir));

        foreach (var file in Directory.GetFiles(sourceDir, "*", SearchOption.AllDirectories))
            File.Copy(file, file.Replace(sourceDir, destinationDir), overwrite: true);
    }

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(_tempRoot))
                Directory.Delete(_tempRoot, recursive: true);
        }
        catch
        {
            // ignored
        }
    }
}

internal sealed record FixtureRunResult(
    IReadOnlyList<ValidationIssue> Issues,
    IReadOnlyList<string> ErrorCodes);

public sealed class ValidatorFixtureDefinition
{
    public string Id { get; set; } = "";
    public FixtureRunnerKind Runner { get; set; }
    public string Description { get; set; } = "";
    public List<FixtureFileMapping> Shared { get; set; } = new();
    public List<FixtureFileMapping> Broken { get; set; } = new();
    public List<FixtureFileMapping> Fixed { get; set; } = new();
    public List<string> ExpectedBrokenCodes { get; set; } = new();
    public List<string> ForbiddenBrokenCodes { get; set; } = new();
    public List<string> ForbiddenFixedCodes { get; set; } = new();
    public bool AllowExtraBrokenCodes { get; set; }
    public CriticalStateMode CriticalStateMode { get; set; } = CriticalStateMode.Canonical;
}

public sealed class FixtureFileMapping
{
    public string Source { get; set; } = "";
    public string Target { get; set; } = "";
    public FixtureOverlayMode Mode { get; set; } = FixtureOverlayMode.Replace;
}

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum FixtureRunnerKind
{
    StateOnly,
    AcceptedTurn,
    CriticalState
}

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum FixtureOverlayMode
{
    Replace,
    MergeObject
}

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum CriticalStateMode
{
    Raw,
    Canonical
}

internal static class TestRepoPaths
{
    private static string? _repoRoot;

    public static string RepoRoot => _repoRoot ??= ResolveRepoRoot();
    public static string BaseSessionRoot => Path.Combine(RepoRoot, "FileSystemExample", "game_session");
    public static string ValidatorFixturesRoot => Path.Combine(RepoRoot, "FileSystemExample", "validator_fixtures");

    private static string ResolveRepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null)
        {
            if (Directory.Exists(Path.Combine(dir.FullName, "FileSystemExample")) &&
                Directory.Exists(Path.Combine(dir.FullName, "BookOfEternityClient")))
            {
                return dir.FullName;
            }

            dir = dir.Parent;
        }

        throw new DirectoryNotFoundException("Could not resolve repository root for validator fixture tests.");
    }
}
