using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using BookOfEternityClient.Configuration;
using BookOfEternityClient.Core;
using BookOfEternityClient.Services;
using Microsoft.Extensions.Logging.Abstractions;

namespace BookOfEternityClient.Tests;

internal sealed class ValidatorFixtureHarness : IDisposable
{
    private const string PendingTurnSnapshotPrefix =
        "game_state/control/pending_turn_snapshot/";

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
        await NormalizePendingTurnSnapshotFixtureAuthorityAsync();
        return await ExecuteAsync();
    }

    public async Task<FixtureRunResult> RunFixedAsync()
    {
        await ApplyMappingsAsync(_definition.Shared);
        await ApplyMappingsAsync(_definition.Fixed);
        await NormalizePendingTurnSnapshotFixtureAuthorityAsync();
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
            FixtureRunnerKind.StateOnly => await _validator.ValidateGameStateAsync(
                BuildStateOnlySelection(_definition)),
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

    internal static GameStateValidationSelection BuildStateOnlySelection(
        ValidatorFixtureDefinition definition)
    {
        ArgumentNullException.ThrowIfNull(definition);

        var stateFiles = definition.Shared
            .Concat(definition.Broken)
            .Concat(definition.Fixed)
            .Select(mapping => mapping.Target.Trim().Replace('\\', '/'))
            .Where(path => path.Length > 0)
            .SelectMany(path =>
                path.StartsWith(PendingTurnSnapshotPrefix, StringComparison.OrdinalIgnoreCase)
                    ? [path, path[PendingTurnSnapshotPrefix.Length..]]
                    : new[] { path })
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        return stateFiles.Length == 0
            ? GameStateValidationSelection.All
            : new GameStateValidationSelection(GameStateValidationPhase.All, stateFiles);
    }

    private async Task<List<ValidationIssue>> RunAcceptedTurnValidationAsync()
    {
        var issues = await _validator.ValidateGameStateAsync();
        issues.AddRange(await _validator.ValidateAcceptedTurnNarrativePayloadAsync());
        issues.AddRange(await _validator.ValidateAcceptedTurnInterfacePayloadAsync());
        issues.AddRange(await _validator.ValidateAcceptedTurnReasoningAsync());
        issues.AddRange(await _validator.ValidateAcceptedTurnSpecialActionOutcomesAsync());
        issues.AddRange(await _validator.ValidateAcceptedTurnQteOfferAsync());
        issues.AddRange(await _validator.ValidateAcceptedTurnMortalCombatMaterializationAsync());
        issues.AddRange(await _validator.ValidateAcceptedTurnMortalLevelUpMaterializationAsync());
        issues.AddRange(await _validator.ValidateAcceptedTurnRawMortalLocationMaterializationAsync());
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

    private async Task NormalizePendingTurnSnapshotFixtureAuthorityAsync()
    {
        const string manifestPath = "game_state/control/pending_turn_snapshot.json";
        var manifestJson = await _fs.ReadFileAsync(manifestPath);
        if (string.IsNullOrWhiteSpace(manifestJson))
            return;

        JsonObject? manifest;
        try
        {
            manifest = JsonNode.Parse(manifestJson) as JsonObject;
        }
        catch
        {
            return;
        }

        if (manifest == null)
            return;

        var sessionId = manifest["sessionId"]?.GetValue<string>() ?? "fixture-session";
        var requestId = manifest["requestId"]?.GetValue<string>() ?? "fixture-request";
        var turnNumber = manifest["turnNumber"]?.GetValue<int>() ?? 1;

        await _fs.WriteFileAtomicAsync(
            "input/turn_request.json",
            JsonSerializer.Serialize(new
            {
                sessionId,
                requestId,
                turnNumber
            }, new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                WriteIndented = true
            }));

        var files = manifest["files"] as JsonObject ?? new JsonObject();
        var snapshotFileHashes = manifest["snapshotFileHashes"] as JsonObject ?? new JsonObject();
        var clientOwnedValidationHashes = manifest["clientOwnedValidationHashes"] as JsonObject ?? new JsonObject();
        var rollbackBaselineFiles = manifest["rollbackBaselineFiles"] as JsonArray ?? new JsonArray();
        if (manifest["rollbackBackups"] is JsonObject rollbackBackups)
        {
            foreach (var pair in rollbackBackups)
            {
                if (pair.Value is not JsonValue backupPathNode ||
                    !backupPathNode.TryGetValue<string>(out var backupPath) ||
                    string.IsNullOrWhiteSpace(backupPath))
                {
                    continue;
                }

                var normalizedPath = backupPath.Replace('\\', '/');
                if (files[pair.Key] == null)
                    files[pair.Key] = normalizedPath;

                var content = await _fs.ReadFileAsync(normalizedPath);
                if (string.IsNullOrWhiteSpace(content))
                    continue;

                snapshotFileHashes[pair.Key] = ComputeSha256(content);
            }
        }

        foreach (var pair in files)
        {
            if (pair.Value is not JsonValue snapshotPathNode ||
                !snapshotPathNode.TryGetValue<string>(out var snapshotPath) ||
                string.IsNullOrWhiteSpace(snapshotPath))
            {
                continue;
            }

            var content = await _fs.ReadFileAsync(snapshotPath.Replace('\\', '/'));
            if (string.IsNullOrWhiteSpace(content))
                continue;

            snapshotFileHashes[pair.Key] = ComputeSha256(content);
        }

        if (rollbackBaselineFiles.Count == 0)
        {
            foreach (var pair in files.OrderBy(pair => pair.Key, StringComparer.OrdinalIgnoreCase))
                rollbackBaselineFiles.Add(pair.Key);
        }

        if (clientOwnedValidationHashes["game_state/history/chat_log.json"] == null)
        {
            var chatLogContent = await _fs.ReadFileAsync("game_state/history/chat_log.json");
            clientOwnedValidationHashes["game_state/history/chat_log.json"] = ComputeSha256(chatLogContent ?? string.Empty);
        }

        if (manifest["sourceLabel"] == null ||
            (manifest["sourceLabel"] is JsonValue sourceLabelNode &&
             (!sourceLabelNode.TryGetValue<string>(out var sourceLabel) || string.IsNullOrWhiteSpace(sourceLabel))))
        {
            manifest["sourceLabel"] = "validator-fixture-harness";
        }

        manifest["files"] = files;
        manifest["snapshotFileHashes"] = snapshotFileHashes;
        manifest["clientOwnedValidationHashes"] = clientOwnedValidationHashes;
        manifest["rollbackBaselineFiles"] = rollbackBaselineFiles;
        manifest["manifestPayloadHash"] = PendingTurnSnapshotTestAuthority.ComputeManifestPayloadHash(manifest);

        await _fs.WriteFileAtomicAsync(
            manifestPath,
            JsonSerializer.Serialize(manifest, SharedJsonOptions.PrettyCamelCaseUnsafeRelaxed));
        await PendingTurnSnapshotTestAuthority.SyncAuthorityForCurrentManifestAsync(_fs);
    }

    private static string ComputeSha256(string content)
    {
        using var sha = System.Security.Cryptography.SHA256.Create();
        var bytes = sha.ComputeHash(System.Text.Encoding.UTF8.GetBytes(content));
        return Convert.ToHexString(bytes);
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
        var explicitRoot = Environment.GetEnvironmentVariable("BOE_REPO_ROOT");
        if (!string.IsNullOrWhiteSpace(explicitRoot) &&
            Directory.Exists(Path.Combine(explicitRoot, "FileSystemExample")) &&
            Directory.Exists(Path.Combine(explicitRoot, "BookOfEternityClient")))
        {
            return explicitRoot;
        }

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
