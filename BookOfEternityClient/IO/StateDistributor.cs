using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using System.Runtime.ExceptionServices;
using BookOfEternityClient.Configuration;
using BookOfEternityClient.Core;
using BookOfEternityClient.Models;
using BookOfEternityClient.Services;
using Microsoft.Extensions.Logging;

namespace BookOfEternityClient.IO;

internal sealed class StateDistributorHooks
{
    internal Func<Task>? AfterBackupsCapturedAsync { get; init; }
    internal Func<string, Task>? AfterFileMutationAppliedAsync { get; init; }
    internal Func<Task>? BeforeBackupCleanupAsync { get; init; }
}

/// <summary>
/// Distributes GameResponse fields to appropriate game state files per CLI API spec.
/// All operations are atomic with backup/rollback.
/// </summary>
public class StateDistributor
{
    private readonly Core.FileSystemManager _fs;
    private readonly ILogger<StateDistributor> _logger;
    private readonly StateDistributorHooks? _hooks;
    private static readonly JsonSerializerOptions JsonOpts = SharedJsonOptions.PrettyCamelCaseUnsafeRelaxed;

    public StateDistributor(Core.FileSystemManager fs, ILogger<StateDistributor> logger)
        : this(fs, logger, hooks: null)
    {
    }

    internal StateDistributor(
        Core.FileSystemManager fs,
        ILogger<StateDistributor> logger,
        StateDistributorHooks? hooks)
    {
        _fs = fs;
        _logger = logger;
        _hooks = hooks;
    }

    /// <summary>
    /// Distribute a complete GameResponse to all appropriate files.
    /// Returns list of modified files.
    /// </summary>
    public async Task<List<string>> DistributeAsync(GameResponse response)
    {
        await using var writeLease = await _fs.AcquireCanonicalWriteLeaseAsync();
        return await DistributeAsync(
            writeLease,
            response,
            acceptedWoundInput: null);
    }

    internal async Task<List<string>> DistributeAsync(
        GameResponse response,
        WoundResponseInputCompositionResult acceptedWoundInput)
    {
        ArgumentNullException.ThrowIfNull(acceptedWoundInput);
        await using var writeLease = await _fs.AcquireCanonicalWriteLeaseAsync();
        return await DistributeAsync(writeLease, response, acceptedWoundInput);
    }

    internal async Task<List<string>> DistributeAsync(
        FileSystemManager.CanonicalWriteLease writeLease,
        GameResponse response) =>
        await DistributeAsync(writeLease, response, acceptedWoundInput: null);

    private async Task<List<string>> DistributeAsync(
        FileSystemManager.CanonicalWriteLease writeLease,
        GameResponse response,
        WoundResponseInputCompositionResult? acceptedWoundInput)
    {
        ArgumentNullException.ThrowIfNull(writeLease);
        ArgumentNullException.ThrowIfNull(response);

        var acceptedWoundCommand = ResolveAcceptedWoundCommand(
            response,
            acceptedWoundInput);
        var modifiedFiles = new List<string>();
        var fileUpdates = CollectFileUpdates(response);
        var targetPaths = fileUpdates.Keys
            .Concat(CollectOutputPaths(response))
            .Concat(acceptedWoundCommand is null
                ? Array.Empty<string>()
                : new[] { AcceptedMechanicsPlan.WoundCommandPath })
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        var mutations = new Dictionary<string, DistributionMutation>(
            StringComparer.OrdinalIgnoreCase);

        try
        {
            // Phase 1: Create backups for all affected files
            foreach (var filePath in targetPaths)
            {
                var mutation = new DistributionMutation
                {
                    Path = filePath,
                    ExistedBefore = _fs.FileExists(writeLease, filePath)
                };
                mutations[filePath] = mutation;
                mutation.BackupPath = _fs.CreateBackup(writeLease, filePath);
            }
            if (_hooks?.AfterBackupsCapturedAsync != null)
                await _hooks.AfterBackupsCapturedAsync();

            // Phase 2: Apply updates atomically per file
            foreach (var (filePath, fields) in fileUpdates)
            {
                await MergeFieldsIntoFile(writeLease, filePath, fields);
                mutations[filePath].MutationApplied = true;
                modifiedFiles.Add(filePath);
                if (_hooks?.AfterFileMutationAppliedAsync != null)
                    await _hooks.AfterFileMutationAppliedAsync(filePath);
            }

            if (acceptedWoundCommand is not null)
            {
                await WriteAcceptedWoundCommandAsync(
                    writeLease,
                    acceptedWoundCommand,
                    mutations,
                    modifiedFiles);
            }

            // Phase 3: Write output interface files
            await WriteOutputFiles(writeLease, response, mutations);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ошибка распределения, откат изменений");
            var rollbackFailures = RollbackMutations(writeLease, mutations.Values);
            if (rollbackFailures.Count > 0)
            {
                throw new AggregateException(
                    "State distribution failed and one or more rollback operations also failed.",
                    [ex, .. rollbackFailures]);
            }

            ExceptionDispatchInfo.Capture(ex).Throw();
            throw;
        }

        await CleanupCommittedBackupsAsync(writeLease, mutations.Values);
        _logger.LogInformation("Распределено {Count} файлов", modifiedFiles.Count);
        return modifiedFiles;
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

            if (FileMapping.ClientConsumedResponseFields.Contains(prop.Name))
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

    private static JsonObject? ResolveAcceptedWoundCommand(
        GameResponse response,
        WoundResponseInputCompositionResult? acceptedWoundInput)
    {
        var hasRawDecisions = response.WoundDecisions is { Length: > 0 };
        if (acceptedWoundInput is null)
        {
            if (hasRawDecisions)
            {
                throw new InvalidDataException(
                    "wound_decisions_require_accepted_command: raw wound decisions cannot enter generic state distribution.");
            }

            return null;
        }

        if (!acceptedWoundInput.Success || acceptedWoundInput.CommandRoot is not { } root)
        {
            throw new InvalidDataException(
                "wound_accepted_command_invalid: wound distribution requires one successful typed composition result.");
        }

        var parsed = WoundResponseInputComposer.ParseCommandRoot(
            JsonSerializer.SerializeToElement(root, JsonOpts));
        if (!parsed.Success)
        {
            throw new InvalidDataException(
                "wound_accepted_command_invalid: the typed wound command failed independent strict parsing.");
        }

        if (root["commands"] is not JsonArray commands)
        {
            throw new InvalidDataException(
                "wound_accepted_command_invalid: the typed wound command has no commands array.");
        }
        if (commands.Count == 0)
            return null;
        if (!hasRawDecisions)
        {
            throw new InvalidDataException(
                "wound_accepted_command_unbound: a non-empty typed wound command has no response decisions.");
        }
        var rawResponseDecisions = response.WoundDecisions!;
        if (rawResponseDecisions.Length !=
            acceptedWoundInput.DecisionReceipts.Count)
        {
            throw new InvalidDataException(
                "wound_accepted_command_unbound: the response decision set changed after typed composition.");
        }

        var responseDecisions = rawResponseDecisions
            .Select(static value => JsonNode.Parse(value.GetRawText()))
            .ToArray();
        var matchedDecisionIndexes = new HashSet<int>();
        var publishedScene = PlayerFacingTextNormalizer
            .NormalizeEscapedLineBreakArtifacts(response.Response);
        foreach (var commandNode in commands)
        {
            if (commandNode is not JsonObject command ||
                !TryReadNullableString(command["finalSceneText"], out var commandScene) ||
                !string.Equals(commandScene, publishedScene, StringComparison.Ordinal) ||
                command["decision"] is not JsonObject commandDecision)
            {
                throw new InvalidDataException(
                    "wound_accepted_command_unbound: the typed command no longer matches the distributed player response.");
            }

            var matchingIndexes = responseDecisions
                .Select((decision, index) => (decision, index))
                .Where(value =>
                    !matchedDecisionIndexes.Contains(value.index) &&
                    JsonNode.DeepEquals(value.decision, commandDecision))
                .Select(static value => value.index)
                .ToArray();
            if (matchingIndexes.Length != 1)
            {
                throw new InvalidDataException(
                    "wound_accepted_command_unbound: the typed command decision is missing or ambiguous in the response.");
            }

            matchedDecisionIndexes.Add(matchingIndexes[0]);
        }

        return root.DeepClone().AsObject();
    }

    private static bool TryReadNullableString(JsonNode? node, out string? value)
    {
        value = null;
        if (node is null)
            return true;
        return node is JsonValue scalar &&
               scalar.TryGetValue<string>(out value);
    }

    private async Task WriteAcceptedWoundCommandAsync(
        FileSystemManager.CanonicalWriteLease writeLease,
        JsonObject commandRoot,
        IReadOnlyDictionary<string, DistributionMutation> mutations,
        ICollection<string> modifiedFiles)
    {
        var path = AcceptedMechanicsPlan.WoundCommandPath;
        await _fs.WriteFileAtomicAsync(
            writeLease,
            path,
            commandRoot.ToJsonString(JsonOpts));
        mutations[path].MutationApplied = true;
        modifiedFiles.Add(path);
        if (_hooks?.AfterFileMutationAppliedAsync != null)
            await _hooks.AfterFileMutationAppliedAsync(path);
    }

    private async Task MergeFieldsIntoFile(
        FileSystemManager.CanonicalWriteLease writeLease,
        string relativePath,
        Dictionary<string, JsonElement> fields)
    {
        // Load existing file content or create new object
        var existingJson = await _fs.ReadFileAsync(writeLease, relativePath);
        Dictionary<string, JsonElement> existingData;

        if (!string.IsNullOrWhiteSpace(existingJson))
        {
            try
            {
                using var doc = JsonDocument.Parse(existingJson);
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
            catch (JsonException ex)
            {
                throw new InvalidDataException(
                    $"Canonical state file '{relativePath}' contains malformed JSON and cannot be merged safely.",
                    ex);
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
                    updateRoot);
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

        // Transient mechanics commands are closed accepted-turn input contracts,
        // not canonical state documents with distribution metadata.
        if (!relativePath.Equals(EffectAcceptedTurnPlan.CommandPath, StringComparison.OrdinalIgnoreCase) &&
            !relativePath.Equals(ResourceMaterializationContract.CommandPath, StringComparison.OrdinalIgnoreCase))
            existingData["_lastUpdated"] = JsonSerializer.SerializeToElement(DateTime.UtcNow.ToString("o"));

        // Serialize and write
        var merged = JsonSerializer.Serialize(existingData, JsonOpts);
        await _fs.WriteFileAtomicAsync(writeLease, relativePath, merged);
    }

    private static JsonObject DictionaryToJsonObject(Dictionary<string, JsonElement> data)
    {
        var root = new JsonObject();
        foreach (var (key, value) in data)
            root[key] = AfterlifeSpiritualConflictState.CloneJsonElement(value);
        return root;
    }

    private static JsonElement JsonNodeToElement(JsonNode? node)
    {
        using var doc = JsonDocument.Parse(node?.ToJsonString(JsonOpts) ?? "null");
        return doc.RootElement.Clone();
    }

    private async Task WriteOutputFiles(
        FileSystemManager.CanonicalWriteLease writeLease,
        GameResponse response,
        IReadOnlyDictionary<string, DistributionMutation> mutations)
    {
        // Narrative response
        if (response.Response != null)
        {
            var narrative = new
            {
                response = PlayerFacingTextNormalizer.NormalizeEscapedLineBreakArtifacts(response.Response),
                timestamp = DateTime.UtcNow.ToString("o")
            };
            const string path = "output/narrative_response.json";
            await _fs.WriteFileAtomicAsync(writeLease, path,
                JsonSerializer.Serialize(narrative, JsonOpts));
            await MarkMutationAppliedAsync(path, mutations);
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
            const string path = "output/interface_updates.json";
            await _fs.WriteFileAtomicAsync(writeLease, path,
                JsonSerializer.Serialize(ui, JsonOpts));
            await MarkMutationAppliedAsync(path, mutations);
        }

        // Debug logs
        if (response.GmThoughtsMarkdown != null)
        {
            var debug = new
            {
                gm_thoughts_markdown = response.GmThoughtsMarkdown,
                timestamp = DateTime.UtcNow.ToString("o")
            };
            const string path = "output/debug_logs.json";
            await _fs.WriteFileAtomicAsync(writeLease, path,
                JsonSerializer.Serialize(debug, JsonOpts));
            await MarkMutationAppliedAsync(path, mutations);
        }
    }

    private static IEnumerable<string> CollectOutputPaths(GameResponse response)
    {
        if (response.Response != null)
            yield return "output/narrative_response.json";
        if (response.DialogueOptions != null || response.ImagePrompt != null)
            yield return "output/interface_updates.json";
        if (response.GmThoughtsMarkdown != null)
            yield return "output/debug_logs.json";
    }

    private async Task MarkMutationAppliedAsync(
        string path,
        IReadOnlyDictionary<string, DistributionMutation> mutations)
    {
        mutations[path].MutationApplied = true;
        if (_hooks?.AfterFileMutationAppliedAsync != null)
            await _hooks.AfterFileMutationAppliedAsync(path);
    }

    private List<Exception> RollbackMutations(
        FileSystemManager.CanonicalWriteLease writeLease,
        IEnumerable<DistributionMutation> mutations)
    {
        var orderedMutations = mutations.ToArray();
        var rollbackFailures = new List<Exception>();
        foreach (var mutation in orderedMutations.Reverse())
        {
            if (!mutation.MutationApplied)
                continue;

            try
            {
                if (mutation.ExistedBefore)
                {
                    if (string.IsNullOrWhiteSpace(mutation.BackupPath))
                    {
                        throw new InvalidDataException(
                            $"Missing before-image for state distribution rollback: {mutation.Path}.");
                    }

                    _fs.RestoreBackup(writeLease, mutation.BackupPath, mutation.Path);
                    mutation.BackupPath = null;
                }
                else
                {
                    _fs.DeleteFile(writeLease, mutation.Path);
                }
            }
            catch (Exception rollbackFailure)
            {
                rollbackFailures.Add(rollbackFailure);
            }
        }

        foreach (var mutation in orderedMutations.Where(item => !item.MutationApplied))
        {
            if (string.IsNullOrWhiteSpace(mutation.BackupPath))
                continue;

            try
            {
                _fs.CleanupBackup(writeLease, mutation.BackupPath);
                mutation.BackupPath = null;
            }
            catch (Exception cleanupFailure)
            {
                rollbackFailures.Add(cleanupFailure);
            }
        }

        return rollbackFailures;
    }

    private async Task CleanupCommittedBackupsAsync(
        FileSystemManager.CanonicalWriteLease writeLease,
        IEnumerable<DistributionMutation> mutations)
    {
        try
        {
            if (_hooks?.BeforeBackupCleanupAsync != null)
                await _hooks.BeforeBackupCleanupAsync();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(
                ex,
                "Распределение состояния принято, но подготовка очистки backup завершилась ошибкой; backup оставлены как evidence.");
            return;
        }

        foreach (var mutation in mutations)
        {
            if (string.IsNullOrWhiteSpace(mutation.BackupPath))
                continue;

            try
            {
                _fs.CleanupBackup(writeLease, mutation.BackupPath);
                mutation.BackupPath = null;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(
                    ex,
                    "Распределение состояния принято, но backup {BackupPath} не удалось удалить.",
                    mutation.BackupPath);
            }
        }
    }

    private sealed class DistributionMutation
    {
        internal required string Path { get; init; }
        internal bool ExistedBefore { get; init; }
        internal string? BackupPath { get; set; }
        internal bool MutationApplied { get; set; }
    }

    private static DialogueOption[]? NormalizeDialogueOptions(DialogueOption[]? options)
    {
        if (options == null)
            return null;

        return options
            .Select(option => new DialogueOption
            {
                OptionId = option.OptionId,
                Text = DialogueOptionControlTagNormalizer.NormalizeVisibleText(
                    PlayerFacingTextNormalizer.NormalizeEscapedLineBreakArtifacts(option.Text)),
                InputValue = DialogueOptionControlTagNormalizer.ResolveInputValue(
                    PlayerFacingTextNormalizer.NormalizeEscapedLineBreakArtifacts(option.Text),
                    PlayerFacingTextNormalizer.NormalizeEscapedLineBreakArtifacts(option.InputValue)),
                Category = option.Category
            })
            .ToArray();
    }
}
