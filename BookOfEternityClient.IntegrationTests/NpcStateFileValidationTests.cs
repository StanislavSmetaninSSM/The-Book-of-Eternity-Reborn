using System.Text.Json;
using BookOfEternityClient.Core;
using BookOfEternityClient.Models;
using BookOfEternityClient.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace BookOfEternityClient.Tests;

[Trait("Category", "FullValidation")]
public sealed class NpcStateFileValidationTests : IDisposable
{
    private readonly string _rootPath;
    private readonly FileSystemManager _fs;
    private readonly ValidationService _validator;

    public NpcStateFileValidationTests()
    {
        _rootPath = Path.Combine(Path.GetTempPath(), "boe-npc-state-validation-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_rootPath);
        CopyDirectory(TestRepoPaths.BaseSessionRoot, Path.Combine(_rootPath, "game_session"));

        _fs = new FileSystemManager(_rootPath, NullLogger<FileSystemManager>.Instance);
        _validator = new ValidationService(_fs, NullLogger<ValidationService>.Instance);
    }

    [Fact]
    public async Task ValidateGameStateAsync_NpcEffectsUnsupportedRoot_ReportsTopLevelContractIssue()
    {
        await WriteJsonAsync(
            "game_state/npcs/npc_effects.json",
            new { unsupportedNpcEffectsRoot = Array.Empty<object>() });

        var issues = await _validator.ValidateGameStateAsync();

        Assert.Contains(issues, issue =>
            IsFlexibleTopLevelKeyIssue(issue) &&
            issue.FilePath.Contains("game_state/npcs/npc_effects.json", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task ValidateGameStateAsync_NpcPersonalityUnsupportedRoot_ReportsTopLevelContractIssue()
    {
        await WriteJsonAsync(
            "game_state/npcs/npc_personality.json",
            new { unsupportedNpcPersonalityRoot = Array.Empty<object>() });

        var issues = await _validator.ValidateGameStateAsync();

        Assert.Contains(issues, issue =>
            IsFlexibleTopLevelKeyIssue(issue) &&
            issue.FilePath.Contains("game_state/npcs/npc_personality.json", StringComparison.OrdinalIgnoreCase));
    }

    private static bool IsFlexibleTopLevelKeyIssue(ValidationIssue issue) =>
        string.Equals(issue.Code, "flexible_state_unknown_top_level_key", StringComparison.OrdinalIgnoreCase) ||
        string.Equals(issue.Code, "missing_allowed_top_level_key", StringComparison.OrdinalIgnoreCase) ||
        string.Equals(issue.Code, "npc_contract_unknown_top_level_key", StringComparison.OrdinalIgnoreCase) ||
        string.Equals(issue.Code, "npc_contract_missing_allowed_top_level_key", StringComparison.OrdinalIgnoreCase);

    private async Task WriteJsonAsync(string relativePath, object payload)
    {
        await _fs.WriteFileAtomicAsync(relativePath, JsonSerializer.Serialize(payload, new JsonSerializerOptions
        {
            WriteIndented = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
        }));
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
            if (Directory.Exists(_rootPath))
                Directory.Delete(_rootPath, recursive: true);
        }
        catch
        {
            // ignore temp cleanup failures
        }
    }
}
