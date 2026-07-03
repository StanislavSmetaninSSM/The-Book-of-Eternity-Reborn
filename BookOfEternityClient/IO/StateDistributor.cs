using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using BookOfEternityClient.Configuration;
using BookOfEternityClient.Core;
using BookOfEternityClient.Models;
using BookOfEternityClient.Services;
using Microsoft.Extensions.Logging;

namespace BookOfEternityClient.IO;

/// <summary>
/// Distributes GameResponse fields to appropriate game state files per CLI API spec.
/// All operations are atomic with backup/rollback.
/// </summary>
public class StateDistributor
{
    private readonly Core.FileSystemManager _fs;
    private readonly ILogger<StateDistributor> _logger;
    private static readonly JsonSerializerOptions JsonOpts = SharedJsonOptions.PrettyCamelCaseUnsafeRelaxed;

    public StateDistributor(Core.FileSystemManager fs, ILogger<StateDistributor> logger)
    {
        _fs = fs;
        _logger = logger;
    }

    /// <summary>
    /// Distribute a complete GameResponse to all appropriate files.
    /// Returns list of modified files.
    /// </summary>
    public async Task<List<string>> DistributeAsync(GameResponse response)
    {
        var backups = new Dictionary<string, string>();
        var modifiedFiles = new List<string>();

        try
        {
            // Collect all fields that have values, grouped by target file
            var fileUpdates = CollectFileUpdates(response);

            // Phase 1: Create backups for all affected files
            foreach (var filePath in fileUpdates.Keys)
            {
                var backup = _fs.CreateBackup(filePath);
                if (backup != null)
                    backups[filePath] = backup;
            }

            // Phase 2: Apply updates atomically per file
            foreach (var (filePath, fields) in fileUpdates)
            {
                await MergeFieldsIntoFile(filePath, fields);
                modifiedFiles.Add(filePath);
            }

            // Phase 3: Write output interface files
            await WriteOutputFiles(response);

            // Phase 4: Cleanup backups on success
            foreach (var backup in backups.Values)
                _fs.CleanupBackup(backup);

            _logger.LogInformation("Распределено {Count} файлов", modifiedFiles.Count);
            return modifiedFiles;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ошибка распределения, откат изменений");

            // Rollback all changes
            foreach (var (filePath, backupPath) in backups)
                _fs.RestoreBackup(backupPath, filePath);

            throw;
        }
    }

    private Dictionary<string, Dictionary<string, JsonElement>> CollectFileUpdates(GameResponse response)
    {
        var result = new Dictionary<string, Dictionary<string, JsonElement>>();
        var responseJson = JsonSerializer.SerializeToElement(response, JsonOpts);

        if (responseJson.ValueKind != JsonValueKind.Object)
            return result;

        foreach (var prop in responseJson.EnumerateObject())
        {
            if (prop.Value.ValueKind == JsonValueKind.Null ||
                prop.Value.ValueKind == JsonValueKind.Undefined)
                continue;

            if (FileMapping.OutputOnlyResponseFields.Contains(prop.Name))
                continue;

            if (FileMapping.FieldToFile.TryGetValue(prop.Name, out var targetFile))
            {
                if (!result.ContainsKey(targetFile))
                    result[targetFile] = new Dictionary<string, JsonElement>();
                result[targetFile][prop.Name] = prop.Value.Clone();
            }
            else
            {
                _logger.LogWarning("Unknown GM response field '{FieldName}' — no file mapping defined, skipping", prop.Name);
            }
        }

        return result;
    }

    private async Task MergeFieldsIntoFile(string relativePath, Dictionary<string, JsonElement> fields)
    {
        // Load existing file content or create new object
        var existingJson = await _fs.ReadFileAsync(relativePath);
        Dictionary<string, JsonElement> existingData;

        if (!string.IsNullOrWhiteSpace(existingJson))
        {
            try
            {
                var doc = JsonDocument.Parse(existingJson);
                existingData = new Dictionary<string, JsonElement>();
                if (doc.RootElement.ValueKind == JsonValueKind.Object)
                {
                    foreach (var prop in doc.RootElement.EnumerateObject())
                        existingData[prop.Name] = prop.Value.Clone();
                }
                // If root is array, wrap it under a data key to preserve it
                else if (doc.RootElement.ValueKind == JsonValueKind.Array)
                {
                    existingData["_previousData"] = doc.RootElement.Clone();
                }
            }
            catch
            {
                existingData = new Dictionary<string, JsonElement>();
            }
        }
        else
        {
            existingData = new Dictionary<string, JsonElement>();
        }

        // Merge new fields into existing data
        foreach (var (key, value) in fields)
        {
            if (relativePath.Equals("game_state/core/player_status.json", StringComparison.OrdinalIgnoreCase) &&
                key.Equals("playerStatus", StringComparison.OrdinalIgnoreCase) &&
                value.ValueKind == JsonValueKind.Object)
            {
                foreach (var prop in value.EnumerateObject())
                    existingData[prop.Name] = prop.Value.Clone();
                continue;
            }

            if (relativePath.Equals("game_state/control/life_transitions.json", StringComparison.OrdinalIgnoreCase) &&
                key.Equals("TriggerLifeEnd", StringComparison.OrdinalIgnoreCase) &&
                value.ValueKind == JsonValueKind.Object)
            {
                existingData.Clear();
                foreach (var prop in value.EnumerateObject())
                    existingData[prop.Name] = prop.Value.Clone();
                continue;
            }

            if (relativePath.Equals("game_state/control/incarnation_trigger.json", StringComparison.OrdinalIgnoreCase) &&
                key.Equals("TriggerIncarnation", StringComparison.OrdinalIgnoreCase) &&
                value.ValueKind == JsonValueKind.Object)
            {
                existingData.Clear();
                foreach (var prop in value.EnumerateObject())
                    existingData[prop.Name] = prop.Value.Clone();
                continue;
            }

            if (relativePath.Equals("game_state/control/ascension.json", StringComparison.OrdinalIgnoreCase) &&
                key.Equals("AscensionTrigger", StringComparison.OrdinalIgnoreCase) &&
                value.ValueKind == JsonValueKind.Object)
            {
                existingData.Clear();
                foreach (var prop in value.EnumerateObject())
                    existingData[prop.Name] = prop.Value.Clone();
                continue;
            }

            if (relativePath.Equals(AfterlifeSpiritualConflictState.StatePath, StringComparison.OrdinalIgnoreCase) &&
                key.Equals(AfterlifeSpiritualConflictState.ResponseField, StringComparison.OrdinalIgnoreCase) &&
                value.ValueKind == JsonValueKind.Object)
            {
                var existingRoot = DictionaryToJsonObject(existingData);
                existingRoot.Remove(AfterlifeSpiritualConflictState.ResponseField);
                var updateRoot = AfterlifeSpiritualConflictState.CloneJsonElement(value) as JsonObject ?? new JsonObject();
                var projected = AfterlifeSpiritualConflictState.ApplyUpdate(
                    existingRoot,
                    updateRoot,
                    await ResolveCurrentSpiritFocusTierAsync());
                projected.Remove(AfterlifeSpiritualConflictState.ResponseField);
                existingData.Clear();
                foreach (var prop in projected)
                    existingData[prop.Key] = JsonNodeToElement(prop.Value);
                continue;
            }

            if (relativePath.Equals(SarefMainStoryState.StatePath, StringComparison.OrdinalIgnoreCase) &&
                key.Equals(SarefMainStoryState.StateResponseField, StringComparison.OrdinalIgnoreCase) &&
                value.ValueKind == JsonValueKind.Object)
            {
                existingData.Clear();
                foreach (var prop in value.EnumerateObject())
                    existingData[prop.Name] = prop.Value.Clone();
                continue;
            }

            if (relativePath.Equals(SarefMainStoryState.StatePath, StringComparison.OrdinalIgnoreCase) &&
                key.Equals(SarefMainStoryState.ResponseField, StringComparison.OrdinalIgnoreCase) &&
                value.ValueKind == JsonValueKind.Object)
            {
                var existingRoot = DictionaryToJsonObject(existingData);
                existingRoot.Remove(SarefMainStoryState.ResponseField);
                var updateRoot = AfterlifeSpiritualConflictState.CloneJsonElement(value) as JsonObject ?? new JsonObject();
                var projected = SarefMainStoryState.ApplyUpdate(existingRoot, updateRoot);
                projected.Remove(SarefMainStoryState.ResponseField);
                existingData.Clear();
                foreach (var prop in projected)
                    existingData[prop.Key] = JsonNodeToElement(prop.Value);
                continue;
            }

            existingData[key] = value;
        }

        // Add metadata
        existingData["_lastUpdated"] = JsonSerializer.SerializeToElement(DateTime.UtcNow.ToString("o"));

        // Serialize and write
        var merged = JsonSerializer.Serialize(existingData, JsonOpts);
        await _fs.WriteFileAtomicAsync(relativePath, merged);
    }

    private static JsonObject DictionaryToJsonObject(Dictionary<string, JsonElement> data)
    {
        var root = new JsonObject();
        foreach (var (key, value) in data)
            root[key] = AfterlifeSpiritualConflictState.CloneJsonElement(value);
        return root;
    }

    private async Task<int> ResolveCurrentSpiritFocusTierAsync()
    {
        try
        {
            var soulJson = await _fs.ReadFileAsync("game_state/meta/soul_state.json");
            if (string.IsNullOrWhiteSpace(soulJson))
                return 0;

            return JsonNode.Parse(soulJson) is JsonObject soulRoot
                ? AfterlifeSpiritualConflictState.ResolveSpiritFocusTier(soulRoot)
                : 0;
        }
        catch
        {
            return 0;
        }
    }

    private static JsonElement JsonNodeToElement(JsonNode? node)
    {
        using var doc = JsonDocument.Parse(node?.ToJsonString(JsonOpts) ?? "null");
        return doc.RootElement.Clone();
    }

    private async Task WriteOutputFiles(GameResponse response)
    {
        // Narrative response
        if (response.Response != null)
        {
            var narrative = new
            {
                response = PlayerFacingTextNormalizer.NormalizeEscapedLineBreakArtifacts(response.Response),
                timestamp = DateTime.UtcNow.ToString("o")
            };
            await _fs.WriteFileAtomicAsync("output/narrative_response.json",
                JsonSerializer.Serialize(narrative, JsonOpts));
        }

        // Interface updates
        if (response.DialogueOptions != null || response.ImagePrompt != null)
        {
            var ui = new
            {
                dialogueOptions = NormalizeDialogueOptions(response.DialogueOptions),
                image_prompt = response.ImagePrompt,
                timestamp = DateTime.UtcNow.ToString("o")
            };
            await _fs.WriteFileAtomicAsync("output/interface_updates.json",
                JsonSerializer.Serialize(ui, JsonOpts));
        }

        // Debug logs
        if (response.GmThoughtsMarkdown != null)
        {
            var debug = new
            {
                gm_thoughts_markdown = response.GmThoughtsMarkdown,
                timestamp = DateTime.UtcNow.ToString("o")
            };
            await _fs.WriteFileAtomicAsync("output/debug_logs.json",
                JsonSerializer.Serialize(debug, JsonOpts));
        }
    }

    private static DialogueOption[]? NormalizeDialogueOptions(DialogueOption[]? options)
    {
        if (options == null)
            return null;

        return options
            .Select(option => new DialogueOption
            {
                OptionId = option.OptionId,
                Text = PlayerFacingTextNormalizer.NormalizeEscapedLineBreakArtifacts(option.Text),
                InputValue = option.InputValue,
                Category = option.Category
            })
            .ToArray();
    }
}
