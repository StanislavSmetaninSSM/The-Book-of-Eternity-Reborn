using BookOfEternityClient.Core;
using BookOfEternityClient.Services;
using Microsoft.Extensions.Logging.Abstractions;
using System.Text.Json.Nodes;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed partial class CanonicalStateNormalizerTests : IDisposable
{
    private readonly string _rootPath;
    private readonly FileSystemManager _fs;

    public CanonicalStateNormalizerTests()
    {
        _rootPath = Path.Combine(Path.GetTempPath(), "boe-normalizer-tests-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_rootPath);

        _fs = new FileSystemManager(_rootPath, NullLogger<FileSystemManager>.Instance);
        _fs.EnsureDirectoryStructure();
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
            // Ignore temp cleanup failures.
        }
    }

    private async Task NormalizeAccumulatedStateWithTrackerBaselineAsync(
        CanonicalStateNormalizer normalizer,
        IReadOnlyDictionary<string, string>? additionalBackups = null)
    {
        var currentTrackerJson = await _fs.ReadFileAsync(GuardianProjectState.TrackerPath);
        if (string.IsNullOrWhiteSpace(currentTrackerJson))
        {
            await normalizer.NormalizeAccumulatedStateAsync(additionalBackups);
            return;
        }

        var currentTrackerRoot = JsonNode.Parse(currentTrackerJson) as JsonObject;
        if (currentTrackerRoot == null)
        {
            await normalizer.NormalizeAccumulatedStateAsync(additionalBackups);
            return;
        }

        var activeProjects = currentTrackerRoot["activeProjects"] as JsonArray;
        var completedProjects = currentTrackerRoot["completedProjects"] as JsonArray;
        var temporaryModifiers = currentTrackerRoot["temporaryProjectModifiers"] as JsonArray;

        if (activeProjects == null &&
            completedProjects == null &&
            temporaryModifiers == null)
        {
            await normalizer.NormalizeAccumulatedStateAsync(additionalBackups);
            return;
        }

        var baselineRoot = new JsonObject
        {
            ["activeProjects"] = activeProjects?.DeepClone() ?? new JsonArray(),
            ["completedProjects"] = completedProjects?.DeepClone() ?? new JsonArray(),
            ["temporaryProjectModifiers"] = temporaryModifiers?.DeepClone() ?? new JsonArray()
        };

        var commandRoot = new JsonObject();
        if (currentTrackerRoot["startGuardianProjects"] is JsonArray startCommands)
            commandRoot["startGuardianProjects"] = startCommands.DeepClone();
        if (currentTrackerRoot["guardianProjectUpdates"] is JsonArray updateCommands)
            commandRoot["guardianProjectUpdates"] = updateCommands.DeepClone();
        if (currentTrackerRoot["completeGuardianProjects"] is JsonArray completeCommands)
            commandRoot["completeGuardianProjects"] = completeCommands.DeepClone();

        const string trackerBaselineBackupPath = "test_backups/normalizer_tracker_authority_baseline.json";
        await _fs.WriteFileAtomicAsync(trackerBaselineBackupPath, baselineRoot.ToJsonString());
        await _fs.WriteFileAtomicAsync(GuardianProjectState.TrackerPath, commandRoot.ToJsonString());

        var effectiveBackups = additionalBackups != null
            ? new Dictionary<string, string>(additionalBackups, StringComparer.OrdinalIgnoreCase)
            : new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        effectiveBackups[GuardianProjectState.TrackerPath] = trackerBaselineBackupPath;

        await normalizer.NormalizeAccumulatedStateAsync(effectiveBackups);
    }

}
