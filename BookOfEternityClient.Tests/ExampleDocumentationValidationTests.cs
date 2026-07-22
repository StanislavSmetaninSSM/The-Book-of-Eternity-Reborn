using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
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
    public void ActorMaterializationManifestCoverage_IsLoadedAndCoversEveryRealm()
    {
        var manifest = ExampleValidationManifest.Load();

        var mortal = Assert.Single(manifest.MortalActorMaterializationCoverage,
            entry => string.Equals(entry.ContractId, "mortal_actor_materialization_v1", StringComparison.Ordinal));
        AssertMaterializationCoverageEntry(mortal, "Mortal World");
        AssertTruthfulValidationMetadata(mortal);
        Assert.Equal("production-validator", mortal.ValidationKind);
        Assert.Contains("ValidationService.ValidateResponse", mortal.ValidationRoute, StringComparison.Ordinal);
        Assert.Contains("ValidateNpcContract", mortal.ValidationRoute, StringComparison.Ordinal);

        var npcCoreChanges = Assert.Single(manifest.MortalNpcCoreChangesCoverage,
            entry => string.Equals(entry.ContractId, "mortal_npc_core_changes_v1", StringComparison.Ordinal));
        AssertMaterializationCoverageEntry(npcCoreChanges, "Mortal World");
        AssertTruthfulValidationMetadata(npcCoreChanges);
        Assert.Equal("focused-fragment", npcCoreChanges.ValidationKind);
        var npcCoreChangesExample = File.ReadAllText(Path.Combine(
            TestRepoPaths.RepoRoot,
            "Examples",
            npcCoreChanges.File));
        Assert.NotEmpty(npcCoreChanges.RequiredText);
        Assert.All(npcCoreChanges.RequiredText, requiredText =>
            Assert.Contains(requiredText, npcCoreChangesExample, StringComparison.Ordinal));

        Assert.NotNull(typeof(GameResponse).GetProperty(nameof(GameResponse.NPCCoreChanges)));
        var focusedCoreChangeSnippets = ExampleSnippetExtractor.ExtractAll()
            .Where(snippet =>
                string.Equals(snippet.File, npcCoreChanges.File, StringComparison.OrdinalIgnoreCase) &&
                snippet.RawText.Contains("\"NPCCoreChanges\"", StringComparison.Ordinal))
            .ToArray();
        Assert.True(focusedCoreChangeSnippets.Length >= 2);

        var allowedGroups = new HashSet<string>(StringComparer.Ordinal)
        {
            "profile",
            "location",
            "progression",
            "characteristicValues",
            "factionAffiliationsToUpsert",
            "fateCardsToAdd",
            "fateCardIdsToRemove"
        };
        foreach (var snippet in focusedCoreChangeSnippets)
        {
            using var document = JsonDocument.Parse(snippet.RawText);
            var changes = document.RootElement.GetProperty("NPCCoreChanges");
            Assert.Equal(JsonValueKind.Array, changes.ValueKind);
            Assert.NotEmpty(changes.EnumerateArray());
            Assert.NotNull(JsonSerializer.Deserialize<GameResponse>(snippet.RawText, SerializerOptions)?.NPCCoreChanges);

            Assert.All(changes.EnumerateArray(), entry =>
            {
                Assert.Equal(JsonValueKind.String, entry.GetProperty("NPCId").ValueKind);
                Assert.False(string.IsNullOrWhiteSpace(entry.GetProperty("NPCId").GetString()));
                Assert.Equal(JsonValueKind.String, entry.GetProperty("reason").ValueKind);
                Assert.False(string.IsNullOrWhiteSpace(entry.GetProperty("reason").GetString()));

                var groups = entry.EnumerateObject()
                    .Where(property => property.Name is not "NPCId" and not "reason")
                    .Select(property => property.Name)
                    .ToArray();
                Assert.NotEmpty(groups);
                Assert.All(groups, group => Assert.Contains(group, allowedGroups));
            });
        }

        var afterlife = manifest.AfterlifeEntityProfileCoverage
            .Where(entry => entry.ContractId.StartsWith("afterlife_actor_materialization_", StringComparison.Ordinal))
            .ToArray();
        Assert.Contains(afterlife, entry => entry.Realms.Contains("Chaos Sea", StringComparer.Ordinal));
        Assert.Contains(afterlife, entry => entry.Realms.Contains("Shining Abode", StringComparer.Ordinal));
        foreach (var entry in afterlife)
            AssertMaterializationCoverageEntry(entry, entry.Realms.Single());
    }

    private static void AssertMaterializationCoverageEntry(
        ActorMaterializationExampleCoverage entry,
        string expectedRealm)
    {
        Assert.False(string.IsNullOrWhiteSpace(entry.ContractId));
        Assert.False(string.IsNullOrWhiteSpace(entry.File));
        Assert.False(string.IsNullOrWhiteSpace(entry.StatePath));
        Assert.False(string.IsNullOrWhiteSpace(entry.ResponseSurface));
        Assert.False(string.IsNullOrWhiteSpace(entry.Description));
        Assert.Contains(expectedRealm, entry.Realms, StringComparer.Ordinal);
        Assert.True(File.Exists(Path.Combine(TestRepoPaths.RepoRoot, "Examples", entry.File)));
    }

    private static void AssertTruthfulValidationMetadata(ActorMaterializationExampleCoverage entry)
    {
        Assert.Contains(entry.ValidationKind, new[] { "production-validator", "focused-fragment" });
        Assert.False(string.IsNullOrWhiteSpace(entry.CoverageLimit));

        if (string.Equals(entry.ValidationKind, "production-validator", StringComparison.Ordinal))
        {
            Assert.False(string.IsNullOrWhiteSpace(entry.ValidationRoute));
            Assert.True(string.IsNullOrWhiteSpace(entry.FocusedFragmentReason));
            return;
        }

        Assert.True(string.IsNullOrWhiteSpace(entry.ValidationRoute));
        Assert.False(string.IsNullOrWhiteSpace(entry.FocusedFragmentReason));
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
                await ApplyScenarioBaselineAsync(fs, scenario.BaselineKind);
                await ApplyScenarioPreStateFilesAsync(fs, scenario.PreStateFiles);
                if (string.Equals(scenario.Runner, "acceptedTurnDistribution", StringComparison.Ordinal))
                    await ApplyScenarioAcceptedTurnValidationBaselineAsync(fs, scenario);
                failures.AddRange(await BuildScenarioPendingTurnSnapshotAsync(fs, scenario));
                if (string.Equals(scenario.Runner, "acceptedTurnDistribution", StringComparison.Ordinal))
                    ClearScenarioTransientOutputFiles(fs);
                var unchangedBefore = await SnapshotScenarioFilesAsync(fs, scenario.ExpectedFilesUnchanged);

                var distributor = new StateDistributor(fs, NullLogger<StateDistributor>.Instance);

                var modifiedFiles = await distributor.DistributeAsync(response);
                string? rawGuardianProjectTrackerJson = null;
                if (string.Equals(scenario.Runner, "acceptedTurnDistribution", StringComparison.Ordinal))
                {
                    rawGuardianProjectTrackerJson = await CaptureScenarioRawGuardianProjectTrackerAsync(fs);
                    await NormalizeScenarioAccumulatedStateAsync(fs, scenario);
                    failures.AddRange(await ApplyScenarioCompanionFilesAsync(fs, scenario, snippets));
                    failures.AddRange(await WriteScenarioTurnCompleteSignalAsync(fs, scenario.Id));
                }
                else
                {
                    failures.AddRange(await ApplyScenarioCompanionFilesAsync(fs, scenario, snippets));
                }
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
                    failures.AddRange(await RunAcceptedTurnScenarioValidationAsync(fs, scenario.Id, rawGuardianProjectTrackerJson));

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

            failures.AddRange(ValidateInkFeatherActionSpecificEvidence(snippet, root, actionTagElement.GetString()));
        }

        Assert.True(
            failures.Count == 0,
            "Ink Feather action receipt examples must match the output/ink_feather_action_result.json contract." +
            Environment.NewLine +
            string.Join(Environment.NewLine, failures.Take(50)));
    }

    [Fact]
    public void AfterlifeInkFeatherNonOfferingActionsHaveManifestReceiptCoverage()
    {
        var manifest = ExampleValidationManifest.Load();
        var snippets = ExampleSnippetExtractor.ExtractAll().ToArray();
        var failures = new List<string>();
        var requiredActions = new[]
        {
            "DONATE_TO_GUARDIAN",
            "CULTIVATE_ENLIGHTENMENT",
            "GUARDIAN_FAVOR",
            "MEMORY_GATES",
            "SOUL_IMPRINT"
        };

        foreach (var action in requiredActions)
        {
            var coverage = manifest.InkFeatherReceiptCoverage
                .FirstOrDefault(item => string.Equals(item.ActionTag, action, StringComparison.Ordinal));
            if (coverage == null)
            {
                failures.Add($"{action}: missing inkFeatherReceiptCoverage manifest entry.");
                continue;
            }

            if (string.IsNullOrWhiteSpace(coverage.ExemptionReason))
                failures.Add($"{action}: manifest coverage must explicitly state why this is receipt-contract validation instead of a full GameResponse runtime scenario.");

            var matches = snippets
                .Where(snippet => string.Equals(snippet.File, coverage.File, StringComparison.OrdinalIgnoreCase) &&
                                  snippet.RawText.Contains($"\"actionTag\": \"{action}\"", StringComparison.Ordinal))
                .ToArray();
            if (matches.Length != 1)
                failures.Add($"{action}: expected exactly one manifest-covered receipt snippet in {coverage.File}, found {matches.Length}.");
        }

        Assert.True(
            failures.Count == 0,
            "Non-offering afterlife Ink Feather actions must have explicit manifest coverage." +
            Environment.NewLine +
            string.Join(Environment.NewLine, failures));
    }

    private static IEnumerable<string> ValidateInkFeatherActionSpecificEvidence(
        ExampleSnippet snippet,
        JsonElement root,
        string? actionTag)
    {
        if (string.IsNullOrWhiteSpace(actionTag) ||
            !root.TryGetProperty("stateEvidence", out var stateEvidence) ||
            stateEvidence.ValueKind != JsonValueKind.Object)
        {
            yield break;
        }

        string[] requiredEvidence = actionTag switch
        {
            "DONATE_TO_GUARDIAN" => new[] { "guardianId", "reputationChange" },
            "GUARDIAN_FAVOR" => new[] { "guardianId", "reputationChange" },
            "CULTIVATE_ENLIGHTENMENT" => new[] { "experienceGain" },
            "MEMORY_GATES" => new[] { "legacyId", "legacyType" },
            "SOUL_IMPRINT" => new[] { "imprintId", "companionName" },
            _ => Array.Empty<string>()
        };

        foreach (var field in requiredEvidence)
        {
            if (!stateEvidence.TryGetProperty(field, out _))
                yield return $"{snippet.Location}: {actionTag} receipt stateEvidence is missing {field}.";
        }
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

    private static async Task ApplyScenarioAcceptedTurnValidationBaselineAsync(
        FileSystemManager fs,
        ExampleRuntimeScenario scenario)
    {
        await fs.WriteFileAtomicAsync("game_state/meta/achievements.json", """
{
  "unlockedAchievements": [],
  "trackedProgress": [],
  "stats": {
    "totalUnlocked": 0,
    "byCategory": {
      "combat": 0,
      "exploration": 0,
      "story": 0,
      "social": 0,
      "crafting": 0,
      "meta": 0,
      "death": 0,
      "secret": 0
    },
    "byRarity": {
      "common": 0,
      "uncommon": 0,
      "rare": 0,
      "epic": 0,
      "legendary": 0
    }
  }
}
""");

        await fs.WriteFileAtomicAsync("lore/codex_entries.json", """
{
  "entries": [],
  "totalEntries": 0,
  "categories": {
    "cosmology": 0,
    "geography": 0,
    "history": 0,
    "cultures": 0,
    "creatures": 0,
    "characters": 0,
    "artifacts": 0,
    "factions": 0,
    "magic": 0,
    "other": 0
  }
}
""");

        await fs.WriteFileAtomicAsync("lore/chaos_sea/soul_system_lore.json", """
{
  "summary": "Production-equivalent test baseline for the Chaos Sea soul system lore."
}
""");
        await fs.WriteFileAtomicAsync("lore/chaos_sea/guardians_lore.json", """
{
  "summary": "Production-equivalent test baseline for Guardian lore."
}
""");
        await fs.WriteFileAtomicAsync("lore/chaos_sea/player_chronicle.json", """
{
  "entries": []
}
""");

        await fs.WriteFileAtomicAsync("game_state/inventory/items.json", """
{
  "items": [],
  "equippedItems": {
    "MainHand": null,
    "OffHand": null,
    "Head": null,
    "Chest": null,
    "Legs": null,
    "Feet": null,
    "Accessory1": null,
    "Accessory2": null
  },
  "totalWeight": "0.0kg",
  "maxWeight": "45.0kg"
}
""");

        await fs.WriteFileAtomicAsync("game_state/world/current_location.json", """
{
  "locationId": null,
  "name": "Production-equivalent afterlife validation baseline",
  "coordinates": { "x": 0, "y": 0, "z": 0 },
  "locationType": "indoor",
  "internalDifficultyProfile": {
    "combat": 0,
    "environment": 0,
    "social": 0,
    "exploration": 0
  },
  "externalDifficultyProfile": {
    "combat": 0,
    "environment": 0,
    "social": 0,
    "exploration": 0
  },
  "tendency": "NO_CHANGE",
  "description": "Neutral baseline location retained only so unrelated FileSystemExample mortal-world data cannot fail afterlife documentation validation.",
  "lastEventsDescription": "#[1] - 24 апреля 2026 г., 09:10: baseline afterlife documentation validation state.",
  "image_prompt": "neutral afterlife validation baseline",
  "factionControl": [],
  "adjacencyMap": [],
  "locationStorages": [],
  "activeThreats": []
}
""");

        await fs.WriteFileAtomicAsync("game_state/meta/abode_power_journal.json", """
{
  "entries": []
}
""");

        await fs.WriteFileAtomicAsync("game_state/world/weather.json", """
{
  "tendency": "NO_CHANGE",
  "description": "Neutral afterlife validation baseline weather."
}
""");

        foreach (var staleFixtureFile in new[]
        {
            "game_state/misc/multipliers.json",
            "game_state/misc/stealth_state.json",
            "game_state/npcs/item_journals.json",
            "game_state/npcs/npc_journals.json",
            "game_state/player/player_status.json"
        })
        {
            if (fs.FileExists(staleFixtureFile))
                fs.DeleteFile(staleFixtureFile);
        }

        if (ScenarioExpectsModifiedFile(scenario, "game_state/control/incarnation_trigger.json"))
            await fs.WriteFileAtomicAsync("game_state/control/incarnation_trigger.json", "{}");

        if (ScenarioExpectsModifiedFile(scenario, "game_state/control/progression_report.json"))
            await fs.WriteFileAtomicAsync("game_state/control/progression_report.json", "{}");

        await fs.WriteFileAtomicAsync("game_state/meta/guardian_project_journal.json", """{ "entries": [] }""");

        if (ScenarioExpectsModifiedFile(scenario, "game_state/meta/guardian_thought_journal.json"))
            await fs.WriteFileAtomicAsync("game_state/meta/guardian_thought_journal.json", """{ "entries": [] }""");
    }

    private static bool ScenarioExpectsModifiedFile(ExampleRuntimeScenario scenario, string relativePath)
    {
        return scenario.ExpectedModifiedFiles
            .Any(path => string.Equals(NormalizeSeparators(path), relativePath, StringComparison.OrdinalIgnoreCase));
    }

    private static async Task ApplyScenarioBaselineAsync(FileSystemManager fs, string baselineKind)
    {
        if (string.IsNullOrWhiteSpace(baselineKind))
            return;

        switch (baselineKind)
        {
            case "chaosSeaAzaliaLivingWorld":
                await WriteChaosSeaAzaliaLivingWorldBaselineAsync(fs);
                return;

            default:
                throw new InvalidOperationException($"Unsupported example scenario baseline kind: {baselineKind}");
        }
    }

    private static async Task WriteChaosSeaAzaliaLivingWorldBaselineAsync(FileSystemManager fs)
    {
        await fs.WriteFileAtomicAsync("game_state/meta/soul_state.json", """
{
  "soulName": "Пепельная Искра",
  "currentRealm": "Chaos Sea",
  "currentIncarnation": 2,
  "enlightenment": {
    "currentTier": "Новичок",
    "experience": 0,
    "level": 0
  },
  "inkFeathers": {
    "current": 200,
    "total": 200
  },
  "soulRelics": {
    "equipped": [],
    "stored": []
  },
  "afterlifeArchive": {
    "stored": []
  },
  "livesHistory": [
    {
      "lifeId": "life_example_001",
      "summary": "Предыдущая смертная жизнь завершена; душа вернулась в Море Хаоса."
    }
  ],
  "pendingMemoryLegacy": null
}
""");

        await fs.WriteFileAtomicAsync("game_state/meta/guardians.json", BuildAzaliaGuardiansJson());
        await fs.WriteFileAtomicAsync("game_state/meta/guardian_projects.json", """
{
  "activeProjects": [
    {
      "guardianId": "guard_social_azalia_001",
      "project": {
        "projectId": "gproj_azalia_silk_hall_001",
        "projectType": "abode_fortification",
        "projectTier": "minor",
        "projectMode": "internal",
        "projectName": "Тихий шёлковый зал",
        "activeState": "active",
        "totalWork": 100,
        "workDone": 17,
        "totalStages": 5,
        "currentStage": 1,
        "pressure": 1,
        "stability": 6,
        "description": "Азалия укрепляет зал, где резиденты могут выдерживать давление Моря.",
        "startedTurn": 390,
        "estimatedCompletionTurn": 470,
        "playerCanAssist": true,
        "assistDescription": "Душа может помогать offering, разговором или архивным материалом."
      }
    }
  ],
  "completedProjects": [],
  "temporaryProjectModifiers": []
}
""");

        await fs.WriteFileAtomicAsync("game_state/meta/guardian_abode_residents.json", """
{
  "entries": [
    {
      "residentId": "res_azalia_liora_001",
      "guardianId": "guard_social_azalia_001",
      "abodeId": "abode_azalia_memory_silk_001",
      "displayName": "Лиора",
      "residentKind": "wayfaring_soul",
      "originType": "traveler_soul",
      "roleLabel": "тихая свидетельница",
      "summary": "Душа, которая учится не путать безопасность с клеткой.",
      "bondLevel": 34,
      "bondTier": "familiar",
      "canGrantCompanionRelic": false,
      "bondRewardState": "none",
      "historyRevealed": false,
      "isPresent": true,
      "personalityProfile": {
        "archetype": "осторожная свидетельница",
        "speechPattern": "тихая, образная",
        "coreValues": [
          "безопасность",
          "честность",
          "память"
        ]
      },
      "abodeDisposition": "cautious",
      "abodeDevotionLevel": 28,
      "abodeDevotionTier": "uncertain",
      "restlessness": 12,
      "migrationState": "restless",
      "mortalWorldImprint": {
        "originWorldSummary": "Город речных храмов, где Лиора была переписчицей долгов.",
        "futureCompanionPrompt": "Если Лиора когда-нибудь станет спутницей, покажи её как спокойного свидетеля, который замечает, где память становится насилием.",
        "bondReason": "Душа однажды помогла ей не потерять собственное имя.",
        "coreTraits": [
          "наблюдательная",
          "осторожная"
        ],
        "archetypeHints": [
          "witness",
          "scribe"
        ],
        "appearanceMotifs": [
          "серебряные чернила",
          "водяная ткань"
        ]
      },
      "availableInteractions": [
        "talk",
        "history"
      ]
    }
  ],
  "thoughtJournal": [],
  "interactionLog": [],
  "historyLog": [],
  "rosterReceipts": [],
  "interactionReceipts": [],
  "transferReceipts": []
}
""");

        await fs.WriteFileAtomicAsync("game_state/meta/abode_power_journal.json", """
{
  "entries": []
}
""");
    }

    private static string BuildAzaliaGuardiansJson()
    {
        const string guardian = """
{
  "guardianId": "guard_social_azalia_001",
  "canonicalName": "Азалия",
  "domain": "Social",
  "abode": {
    "abodeId": "abode_azalia_memory_silk_001",
    "isDiscovered": true
  },
  "nameVariants": {
    "default": "Азалия",
    "feminine": "Азалия",
    "masculine": "Азалия",
    "neutral": "Азалия"
  },
  "manifestation": {
    "formFlexibility": "selective",
    "currentDisplayName": "Азалия",
    "currentPresentationStyle": "feminine",
    "currentPronouns": "она/её",
    "appearanceDescription": "Хранительница в мягком шёлке, вокруг которой память собирается в тёплые складки."
  },
  "manifestationHistory": [],
  "personalityProfile": {
    "archetype": "Charming Diplomat",
    "speechPattern": "мягкая, внимательная",
    "coreValues": [
      "убежище",
      "память",
      "бережная власть"
    ]
  },
  "socialProfile": {
    "jealousyFactor": 20,
    "curiosityFactor": 70,
    "competitiveFactor": 30,
    "generosityFactor": 65,
    "isolationistTendency": 25
  },
  "guardianRelationships": [],
  "relationshipData": {
    "currentReputation": 45,
    "reputationHistory": [],
    "lastInteraction": "2026-04-24T15:00:00Z"
  },
  "abodePower": {
    "currentPower": 42,
    "tier": "Стабильная",
    "lastUpdatedAt": "2026-04-24T15:00:00Z",
    "history": []
  },
  "questManagement": {
    "availableQuests": [],
    "activeQuests": [],
    "completedQuests": []
  },
  "gachaSystem": {
    "chargesPerReturn": 2,
    "chargesUsedThisReturn": 0,
    "gachaHistory": []
  },
  "mood": {
    "current": "contemplative",
    "intensity": 35,
    "reason": "Обитель пережидает тихий цикл Моря.",
    "since": 418
  },
  "loreFragments": [
    {
      "fragmentId": "lore_guard_social_azalia_001_origin",
      "category": "personal_history",
      "title": "Первый зал Азалии",
      "content": null,
      "requiredReputation": 0
    },
    {
      "fragmentId": "lore_guard_social_azalia_001_secret",
      "category": "cosmic_secret",
      "title": "Тайна шёлковой памяти",
      "content": null,
      "requiredReputation": 50
    },
    {
      "fragmentId": "lore_guard_social_azalia_001_domain",
      "category": "domain_mastery",
      "title": "Власть мягкого убеждения",
      "content": null,
      "requiredReputation": 130
    },
    {
      "fragmentId": "lore_guard_social_azalia_001_lost_world",
      "category": "lost_world",
      "title": "Мир утраченных салонов",
      "content": null,
      "requiredReputation": 230
    },
    {
      "fragmentId": "lore_guard_social_azalia_001_allies",
      "category": "other_guardians",
      "title": "Союзы памяти",
      "content": null,
      "requiredReputation": 0
    },
    {
      "fragmentId": "lore_guard_social_azalia_001_soul",
      "category": "soul_mechanics",
      "title": "Шёлк между воплощениями",
      "content": null,
      "requiredReputation": 50
    },
    {
      "fragmentId": "lore_guard_social_azalia_001_return",
      "category": "personal_history",
      "title": "Возвращение к краю Моря",
      "content": null,
      "requiredReputation": 130
    }
  ],
  "musings": []
}
""";

        return $$"""
{
  "guardians": [
    {{guardian}}
  ],
  "activeGuardian": {{guardian}},
  "chaosSeaNavigation": {
    "currentAbodeId": "abode_azalia_memory_silk_001",
    "discoveredAbodes": [
      "abode_azalia_memory_silk_001"
    ]
  }
}
""";
    }

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
        string scenarioId,
        string? rawGuardianProjectTrackerJson)
    {
        var validator = new ValidationService(fs, NullLogger<ValidationService>.Instance);
        var issues = await validator.ValidateGameStateAsync();
        issues = await FilterPostNormalizerGuardianProjectAuthorityIssuesAsync(fs, issues, rawGuardianProjectTrackerJson);
        issues.AddRange(await validator.ValidateAcceptedTurnNarrativePayloadAsync());
        issues.AddRange(await validator.ValidateAcceptedTurnInterfacePayloadAsync());
        issues.AddRange(await validator.ValidateAcceptedTurnReasoningAsync());
        issues.AddRange(await validator.ValidateAcceptedTurnSpecialActionOutcomesAsync());
        issues.AddRange(await validator.ValidateAcceptedTurnQteOfferAsync());
        issues.AddRange(await validator.ValidateAcceptedTurnMortalCombatMaterializationAsync());
        issues.AddRange(await validator.ValidateAcceptedTurnMortalLevelUpMaterializationAsync());
        issues.AddRange(await validator.ValidatePendingMemoryLegacyApplicationAsync());
        issues.AddRange(await RunProgressionReportScenarioValidationAsync(fs));

        return issues
            .Where(issue => issue.Severity == IssueSeverity.Error)
            .Select(issue => $"{scenarioId}: accepted-turn validation error {issue.FilePath} {issue.Code}: {issue.Message}")
            .ToList();
    }

    private static async Task<string?> CaptureScenarioRawGuardianProjectTrackerAsync(FileSystemManager fs)
    {
        return fs.FileExists(GuardianProjectState.TrackerPath)
            ? await fs.ReadFileAsync(GuardianProjectState.TrackerPath)
            : null;
    }

    private static async Task<List<ValidationIssue>> FilterPostNormalizerGuardianProjectAuthorityIssuesAsync(
        FileSystemManager fs,
        List<ValidationIssue> issues,
        string? rawGuardianProjectTrackerJson)
    {
        // Production validates after canonical normalization, when project command surfaces have been consumed.
        // Rebuild the same authority from the raw distributed commands so docs tests still catch real divergence.
        if (!issues.Any(IsPostNormalizerGuardianProjectAuthorityIssue) ||
            !HasGuardianProjectCommandSurface(rawGuardianProjectTrackerJson) ||
            !await NormalizedGuardianProjectStateMatchesRawCommandAuthorityAsync(fs, rawGuardianProjectTrackerJson))
        {
            return issues;
        }

        return issues
            .Where(issue => !IsPostNormalizerGuardianProjectAuthorityIssue(issue))
            .ToList();
    }

    private static bool IsPostNormalizerGuardianProjectAuthorityIssue(ValidationIssue issue) =>
        string.Equals(issue.Code, "guardian_project_materialized_state_outside_authority", StringComparison.OrdinalIgnoreCase) &&
        issue.FilePath.StartsWith(GuardianProjectState.TrackerPath, StringComparison.OrdinalIgnoreCase);

    private static bool HasGuardianProjectCommandSurface(string? trackerJson)
    {
        var trackerRoot = TryParseJsonObject(trackerJson);
        return trackerRoot?["startGuardianProjects"] is JsonArray ||
               trackerRoot?["guardianProjectUpdates"] is JsonArray ||
               trackerRoot?["completeGuardianProjects"] is JsonArray;
    }

    private static async Task<bool> NormalizedGuardianProjectStateMatchesRawCommandAuthorityAsync(
        FileSystemManager fs,
        string? rawGuardianProjectTrackerJson)
    {
        var rawTrackerRoot = TryParseJsonObject(rawGuardianProjectTrackerJson);
        var currentTrackerRoot = TryParseJsonObject(await ReadScenarioFileAsync(fs, GuardianProjectState.TrackerPath));
        var preTurnTrackerRoot = TryParseJsonObject(await ReadScenarioFileAsync(fs, $"game_state/control/pending_turn_snapshot/{GuardianProjectState.TrackerPath}"));
        var preTurnGuardiansRoot = TryParseJsonObject(await ReadScenarioFileAsync(fs, "game_state/control/pending_turn_snapshot/game_state/meta/guardians.json"));
        var currentGuardiansRoot = TryParseJsonObject(await ReadScenarioFileAsync(fs, "game_state/meta/guardians.json"));
        if (rawTrackerRoot == null ||
            currentTrackerRoot == null ||
            preTurnTrackerRoot == null ||
            preTurnGuardiansRoot == null ||
            currentGuardiansRoot == null)
        {
            return false;
        }

        var turnNumber = await ReadScenarioTurnNumberAsync(fs);
        var requirements = CanonicalStateNormalizer.ResolveRequiredCurrentGuardianProjectSoulContext(rawTrackerRoot, preTurnTrackerRoot);
        var currentSoulStateJson = await ReadScenarioFileAsync(fs, "game_state/meta/soul_state.json");
        var preTurnSoulStateJson = await ReadScenarioFileAsync(fs, "game_state/control/pending_turn_snapshot/game_state/meta/soul_state.json");
        var currentLifeTransitionsJson = await ReadScenarioFileAsync(fs, "game_state/control/life_transitions.json");
        if (!CanonicalStateNormalizer.TryResolveGuardianProjectAuthoritySoulContext(
                currentSoulStateJson,
                preTurnSoulStateJson,
                currentLifeTransitionsJson,
                turnNumber,
                requirements,
                out var currentIncarnation,
                out var currentRealm,
                out _))
        {
            return false;
        }

        var expectedTrackerRoot = CanonicalStateNormalizer.BuildGuardianProjectAuthorityRootForValidation(
            preTurnTrackerRoot,
            rawTrackerRoot,
            preTurnGuardiansRoot,
            currentGuardiansRoot,
            turnNumber,
            currentIncarnation,
            currentRealm);

        return JsonNode.DeepEquals(currentTrackerRoot, expectedTrackerRoot);
    }

    private static JsonObject? TryParseJsonObject(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
            return null;

        try
        {
            return JsonNode.Parse(json, nodeOptions: null, documentOptions: DocumentOptions) as JsonObject;
        }
        catch
        {
            return null;
        }
    }

    private static async Task<int> ReadScenarioTurnNumberAsync(FileSystemManager fs)
    {
        var turnRequestJson = await ReadScenarioFileAsync(fs, "input/turn_request.json");
        if (string.IsNullOrWhiteSpace(turnRequestJson))
            return 0;

        try
        {
            using var doc = JsonDocument.Parse(turnRequestJson, DocumentOptions);
            return doc.RootElement.TryGetProperty("turnNumber", out var turnNumber) &&
                   turnNumber.ValueKind == JsonValueKind.Number &&
                   turnNumber.TryGetInt32(out var value)
                ? value
                : 0;
        }
        catch
        {
            return 0;
        }
    }

    private static async Task<List<ValidationIssue>> RunProgressionReportScenarioValidationAsync(FileSystemManager fs)
    {
        var requestJson = await fs.ReadFileAsync("input/turn_request.json");
        if (string.IsNullOrWhiteSpace(requestJson))
            return [];

        try
        {
            var request = JsonSerializer.Deserialize<TurnRequest>(requestJson, SerializerOptions);
            if (request?.ProgressionControl == null)
                return [];

            var progression = new ProgressionScheduleService(
                fs,
                NullLogger<ProgressionScheduleService>.Instance);
            return await progression.ValidateAcceptedTurnOutcomeAsync(request.ProgressionControl);
        }
        catch (Exception ex)
        {
            return
            [
                new ValidationIssue(
                    "input/turn_request.json",
                    IssueSeverity.Error,
                    $"Progression report scenario validation failed: {ex.GetType().Name}: {ex.Message}",
                    code: "example_progression_validation_failed",
                    section: "ExampleDocumentationValidation")
            ];
        }
    }

    private static async Task<List<string>> ApplyScenarioCompanionFilesAsync(
        FileSystemManager fs,
        ExampleRuntimeScenario scenario,
        IReadOnlyCollection<ExampleSnippet> snippets)
    {
        var failures = new List<string>();
        foreach (var companion in scenario.CompanionFiles)
        {
            var matches = snippets.Where(companion.Matches).ToArray();
            if (matches.Length != 1)
            {
                failures.Add($"{scenario.Id}: expected exactly one companion snippet for '{companion.Path}', found {matches.Length}.");
                continue;
            }

            if (!TryBuildJsonDocument(matches[0].RawText, out var normalizedJson, out _, out var error))
            {
                failures.Add($"{scenario.Id}: companion snippet for '{companion.Path}' is not parseable JSON at {matches[0].Location}: {error}");
                continue;
            }

            await fs.WriteFileAtomicAsync(companion.Path, normalizedJson);
        }

        return failures;
    }

    private static async Task NormalizeScenarioAccumulatedStateAsync(
        FileSystemManager fs,
        ExampleRuntimeScenario scenario)
    {
        var normalizer = new CanonicalStateNormalizer(
            fs,
            NullLogger<CanonicalStateNormalizer>.Instance);

        await normalizer.NormalizeAccumulatedStateAsync(BuildScenarioNormalizerBackups(scenario));
    }

    private static IReadOnlyDictionary<string, string>? BuildScenarioNormalizerBackups(ExampleRuntimeScenario scenario)
    {
        if (scenario.PendingSnapshotFiles.Count == 0)
            return null;

        return scenario.PendingSnapshotFiles
            .Select(NormalizeSeparators)
            .ToDictionary(
                path => path,
                path => $"game_state/control/pending_turn_snapshot/{path}",
                StringComparer.OrdinalIgnoreCase);
    }

    private static async Task<List<string>> BuildScenarioPendingTurnSnapshotAsync(
        FileSystemManager fs,
        ExampleRuntimeScenario scenario)
    {
        var failures = new List<string>();
        if (scenario.PendingSnapshotFiles.Count == 0)
        {
            await NormalizeExistingPendingTurnSnapshotAsync(fs);
            return failures;
        }

        var requestJson = await fs.ReadFileAsync("input/turn_request.json");
        if (string.IsNullOrWhiteSpace(requestJson))
        {
            failures.Add($"{scenario.Id}: pendingSnapshotFiles requires preStateFiles entry for input/turn_request.json.");
            return failures;
        }

        JsonObject? requestRoot;
        try
        {
            requestRoot = JsonNode.Parse(requestJson) as JsonObject;
        }
        catch
        {
            requestRoot = null;
        }

        if (requestRoot == null)
        {
            failures.Add($"{scenario.Id}: input/turn_request.json is not a JSON object.");
            return failures;
        }

        var sessionId = GetRequiredString(requestRoot, "sessionId");
        var requestId = GetRequiredString(requestRoot, "requestId");
        var turnNumber = requestRoot["turnNumber"]?.GetValue<int>() ?? 0;
        if (string.IsNullOrWhiteSpace(sessionId) ||
            string.IsNullOrWhiteSpace(requestId) ||
            turnNumber <= 0)
        {
            failures.Add($"{scenario.Id}: input/turn_request.json must contain sessionId, requestId, and positive turnNumber.");
            return failures;
        }

        var files = new JsonObject();
        var snapshotHashes = new JsonObject();
        var rollbackBaselineFiles = new JsonArray();
        foreach (var relativePath in EnumerateScenarioPendingSnapshotFiles(fs, scenario))
        {
            var normalizedPath = NormalizeSeparators(relativePath);
            var content = await ReadScenarioFileAsync(fs, normalizedPath);
            if (content == null)
            {
                failures.Add($"{scenario.Id}: pending snapshot source '{normalizedPath}' does not exist.");
                continue;
            }

            var snapshotPath = $"game_state/control/pending_turn_snapshot/{normalizedPath}";
            await fs.WriteFileAtomicAsync(snapshotPath, content);
            files[normalizedPath] = snapshotPath;
            snapshotHashes[normalizedPath] = ComputeSha256(content);
            rollbackBaselineFiles.Add(normalizedPath);
        }

        if (failures.Count > 0)
            return failures;

        var clientOwnedHashes = new JsonObject();

        var manifest = new JsonObject
        {
            ["sessionId"] = sessionId,
            ["requestId"] = requestId,
            ["turnNumber"] = turnNumber,
            ["requestTimestamp"] = GetRequiredString(requestRoot, "timestamp"),
            ["playerAction"] = GetRequiredString(requestRoot, "playerAction"),
            ["files"] = files,
            ["snapshotFileHashes"] = snapshotHashes,
            ["clientOwnedValidationHashes"] = clientOwnedHashes,
            ["rollbackBackups"] = new JsonObject(),
            ["rollbackBaselineFiles"] = rollbackBaselineFiles,
            ["sourceLabel"] = string.IsNullOrWhiteSpace(scenario.PendingSnapshotSourceLabel)
                ? "example-documentation-validation"
                : scenario.PendingSnapshotSourceLabel,
            ["manifestPayloadHash"] = string.Empty
        };
        manifest["manifestPayloadHash"] = PendingTurnSnapshotTestAuthority.ComputeManifestPayloadHash(manifest);
        await fs.WriteFileAtomicAsync(
            "game_state/control/pending_turn_snapshot.json",
            JsonSerializer.Serialize(manifest, SharedJsonOptions.PrettyCamelCaseUnsafeRelaxed));
        await PendingTurnSnapshotTestAuthority.SyncAuthorityForCurrentManifestAsync(fs);
        return failures;
    }

    private static IReadOnlyList<string> EnumerateScenarioPendingSnapshotFiles(
        FileSystemManager fs,
        ExampleRuntimeScenario scenario)
    {
        var files = new HashSet<string>(scenario.PendingSnapshotFiles.Select(NormalizeSeparators), StringComparer.OrdinalIgnoreCase);
        if (!string.Equals(scenario.Runner, "acceptedTurnDistribution", StringComparison.Ordinal))
            return files.OrderBy(path => path, StringComparer.OrdinalIgnoreCase).ToArray();

        foreach (var relativePath in EnumerateScenarioRollbackTrackedFiles(fs))
            files.Add(relativePath);

        return files.OrderBy(path => path, StringComparer.OrdinalIgnoreCase).ToArray();
    }

    private static IEnumerable<string> EnumerateScenarioRollbackTrackedFiles(FileSystemManager fs)
    {
        var root = fs.ResolvePath("");
        foreach (var relativeRoot in new[] { "game_state", "lore" })
        {
            var absoluteRoot = fs.ResolvePath(relativeRoot);
            if (!Directory.Exists(absoluteRoot))
                continue;

            foreach (var absoluteFile in Directory.EnumerateFiles(absoluteRoot, "*", SearchOption.AllDirectories))
            {
                var relative = Path.GetRelativePath(root, absoluteFile).Replace('\\', '/');
                if (IsScenarioSnapshotExcludedPath(relative))
                    continue;

                yield return relative;
            }
        }

        foreach (var outputFile in new[]
        {
            "output/narrative_response.json",
            "output/interface_updates.json",
            "output/debug_logs.json",
            "output/qte_offer.json",
            "output/ink_feather_action_result.json",
            "game_state/control/progression_report.json"
        })
        {
            if (fs.FileExists(outputFile))
                yield return outputFile;
        }
    }

    private static bool IsScenarioSnapshotExcludedPath(string relativePath)
    {
        var normalized = NormalizeSeparators(relativePath);
        return normalized.Equals("game_state/control/validation_repair_ready.json", StringComparison.OrdinalIgnoreCase) ||
               normalized.Equals("game_state/control/validation_repair_request.json", StringComparison.OrdinalIgnoreCase) ||
               normalized.Equals("game_state/control/terminal_protocol_failure_request.json", StringComparison.OrdinalIgnoreCase) ||
               normalized.Equals("game_state/control/pending_turn_snapshot.json", StringComparison.OrdinalIgnoreCase) ||
               normalized.Equals(PendingTurnSnapshotAuthority.AuthorityPath, StringComparison.OrdinalIgnoreCase) ||
               normalized.Equals("game_state/history/chat_log.json", StringComparison.OrdinalIgnoreCase) ||
               normalized.StartsWith("game_state/control/pending_turn_snapshot/", StringComparison.OrdinalIgnoreCase) ||
               normalized.Contains(".rollback.", StringComparison.OrdinalIgnoreCase);
    }

    private static void ClearScenarioTransientOutputFiles(FileSystemManager fs)
    {
        foreach (var file in new[]
        {
            "output/narrative_response.json",
            "output/interface_updates.json",
            "output/debug_logs.json",
            "output/ink_feather_action_result.json",
            "output/qte_offer.json",
            "game_state/control/progression_report.json"
        })
        {
            if (fs.FileExists(file))
                fs.DeleteFile(file);
        }
    }

    private static async Task NormalizeExistingPendingTurnSnapshotAsync(FileSystemManager fs)
    {
        if (!fs.FileExists("game_state/control/pending_turn_snapshot.json"))
        {
            await PendingTurnSnapshotTestAuthority.SyncAuthorityForCurrentManifestAsync(fs);
            return;
        }

        var manifestJson = await fs.ReadFileAsync("game_state/control/pending_turn_snapshot.json");
        if (string.IsNullOrWhiteSpace(manifestJson) || JsonNode.Parse(manifestJson) is not JsonObject manifest)
        {
            await PendingTurnSnapshotTestAuthority.SyncAuthorityForCurrentManifestAsync(fs);
            return;
        }

        manifest["manifestPayloadHash"] = PendingTurnSnapshotTestAuthority.ComputeManifestPayloadHash(manifest);
        await fs.WriteFileAtomicAsync(
            "game_state/control/pending_turn_snapshot.json",
            JsonSerializer.Serialize(manifest, SharedJsonOptions.PrettyCamelCaseUnsafeRelaxed));
        await PendingTurnSnapshotTestAuthority.SyncAuthorityForCurrentManifestAsync(fs);
    }

    private static async Task<List<string>> WriteScenarioTurnCompleteSignalAsync(FileSystemManager fs, string scenarioId)
    {
        var failures = new List<string>();
        var requestJson = await fs.ReadFileAsync("input/turn_request.json");
        if (string.IsNullOrWhiteSpace(requestJson))
        {
            failures.Add($"{scenarioId}: acceptedTurnDistribution requires input/turn_request.json.");
            return failures;
        }

        JsonObject? requestRoot;
        try
        {
            requestRoot = JsonNode.Parse(requestJson) as JsonObject;
        }
        catch
        {
            requestRoot = null;
        }

        if (requestRoot == null)
        {
            failures.Add($"{scenarioId}: input/turn_request.json is not a JSON object.");
            return failures;
        }

        var sessionId = GetRequiredString(requestRoot, "sessionId");
        var requestId = GetRequiredString(requestRoot, "requestId");
        var turnNumber = requestRoot["turnNumber"]?.GetValue<int>() ?? 0;
        if (string.IsNullOrWhiteSpace(sessionId) ||
            string.IsNullOrWhiteSpace(requestId) ||
            turnNumber <= 0)
        {
            failures.Add($"{scenarioId}: turn_complete signal requires sessionId/requestId/turnNumber in input/turn_request.json.");
            return failures;
        }

        await fs.WriteFileAtomicAsync(
            "ready/turn_complete.json",
            JsonSerializer.Serialize(new
            {
                sessionId,
                requestId,
                turnNumber,
                timestamp = "2026-04-24T16:20:00Z",
                status = "success",
                filesModified = Array.Empty<string>()
            }, SharedJsonOptions.PrettyCamelCaseUnsafeRelaxed));
        return failures;
    }

    private static string GetRequiredString(JsonObject root, string propertyName)
    {
        if (root[propertyName] is not JsonValue value ||
            !value.TryGetValue<string>(out var result))
        {
            return string.Empty;
        }

        return result ?? string.Empty;
    }

    private static string ComputeSha256(string content) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(content)));

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
    public List<InkFeatherReceiptCoverage> InkFeatherReceiptCoverage { get; set; } = new();
    public List<ExampleRuntimeScenario> RuntimeScenarios { get; set; } = new();
    public List<AfterlifeExampleCoverage> AfterlifeExampleCoverage { get; set; } = new();
    public List<ActorMaterializationExampleCoverage> MortalActorMaterializationCoverage { get; set; } = new();
    public List<ActorMaterializationExampleCoverage> MortalNpcCoreChangesCoverage { get; set; } = new();
    public List<ActorMaterializationExampleCoverage> AfterlifeEntityProfileCoverage { get; set; } = new();

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
