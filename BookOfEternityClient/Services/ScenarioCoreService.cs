using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using BookOfEternityClient.Core;
using Microsoft.Extensions.Logging;

namespace BookOfEternityClient.Services;

public sealed class ScenarioCoreService
{
    public const string ManifestPath = "game_state/control/next_life_scenario_core.json";

    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };

    private static readonly Dictionary<string, string[]> SlotTypesByCategory = new(StringComparer.OrdinalIgnoreCase)
    {
        ["role_status"] = ["ally_thread", "rival_thread", "debt_or_oath", "protection_or_omen"],
        ["start_location"] = ["social_hidden_layer", "occult_hidden_layer", "resource_complication", "protection_or_omen"],
        ["world_premise"] = ["occult_hidden_layer", "rival_thread", "ally_thread"],
        ["world_condition"] = ["political_hidden_layer", "social_hidden_layer", "occult_hidden_layer", "rival_thread", "protection_or_omen"],
        ["starting_resources"] = ["resource_complication", "resource_blessing", "debt_or_oath"],
        ["starting_relationship"] = ["ally_thread", "rival_thread", "debt_or_oath"],
        ["identity_anchor"] = ["protection_or_omen", "rival_thread"]
    };

    private static readonly HashSet<string> StrongSlots = new(StringComparer.OrdinalIgnoreCase)
    {
        "political_hidden_layer",
        "social_hidden_layer",
        "occult_hidden_layer",
        "debt_or_oath",
        "rival_thread"
    };

    private static readonly string[] RoleKeywords =
    {
        "king", "queen", "emperor", "empress", "prince", "princess", "duke", "count", "lord", "lady",
        "король", "королева", "император", "императрица", "принц", "принцесса", "герцог", "граф", "лорд", "леди", "вожд", "патриарх"
    };

    private static readonly string[] RelationshipKeywords =
    {
        "advisor", "mentor", "friend", "ally", "wife", "husband", "lover", "sister", "brother", "father", "mother",
        "советник", "наставник", "друг", "союзник", "жена", "муж", "любов", "сестра", "брат", "отец", "мать", "телохранитель"
    };

    private static readonly string[] ResourceKeywords =
    {
        "gold", "wealth", "army", "treasury", "artifact", "ship", "estate", "company", "inheritance", "fortune",
        "золото", "богат", "армия", "казна", "артефакт", "кораб", "имение", "компания", "наслед", "состояние", "ресурс", "деньги"
    };

    private static readonly string[] StartLocationKeywords =
    {
        "palace", "castle", "capital", "city", "village", "academy", "temple", "court", "dungeon", "prison",
        "дворец", "замок", "столиц", "город", "деревн", "академ", "храм", "двор", "подземел", "тюрьм", "секта", "особняк"
    };

    private static readonly string[] WorldConditionKeywords =
    {
        "prosper", "peace", "stable", "golden age", "war", "decline", "prosperous", "safe",
        "процвет", "мирн", "стабил", "золот", "войн", "упад", "безопас", "богат", "кризис", "спокой"
    };

    private readonly FileSystemManager _fs;
    private readonly ILogger<ScenarioCoreService> _logger;

    public ScenarioCoreService(FileSystemManager fs, ILogger<ScenarioCoreService> logger)
    {
        _fs = fs;
        _logger = logger;
    }

    public sealed class ScenarioCoreManifest
    {
        [JsonPropertyName("sourcePath")]
        public string SourcePath { get; set; } = WorldDirectiveService.PendingSetupPath;

        [JsonPropertyName("sourceLastUpdated")]
        public string? SourceLastUpdated { get; set; }

        [JsonPropertyName("lastExtractedAt")]
        public string LastExtractedAt { get; set; } = DateTime.UtcNow.ToString("o");

        [JsonPropertyName("candidateAssertions")]
        public List<ScenarioCandidateAssertion> CandidateAssertions { get; set; } = new();

        [JsonPropertyName("scenarioCoreAssertions")]
        public List<ScenarioCoreAssertion> ScenarioCoreAssertions { get; set; } = new();

        [JsonPropertyName("openCorrectionSlots")]
        public List<ScenarioCorrectionSlot> OpenCorrectionSlots { get; set; } = new();
    }

    public sealed class ScenarioCandidateAssertion
    {
        [JsonPropertyName("candidateId")]
        public string CandidateId { get; set; } = "";

        [JsonPropertyName("source")]
        public string Source { get; set; } = "";

        [JsonPropertyName("text")]
        public string Text { get; set; } = "";

        [JsonPropertyName("category")]
        public string Category { get; set; } = "";

        [JsonPropertyName("confidence")]
        public double Confidence { get; set; }
    }

    public sealed class ScenarioCoreAssertion
    {
        [JsonPropertyName("assertionId")]
        public string AssertionId { get; set; } = "";

        [JsonPropertyName("category")]
        public string Category { get; set; } = "";

        [JsonPropertyName("value")]
        public string Value { get; set; } = "";

        [JsonPropertyName("explicit")]
        public bool Explicit { get; set; }

        [JsonPropertyName("source")]
        public string Source { get; set; } = "";

        [JsonPropertyName("candidateId")]
        public string? CandidateId { get; set; }
    }

    public sealed class ScenarioCorrectionSlot
    {
        [JsonPropertyName("slotId")]
        public string SlotId { get; set; } = "";

        [JsonPropertyName("slotType")]
        public string SlotType { get; set; } = "";

        [JsonPropertyName("maxSeverity")]
        public string MaxSeverity { get; set; } = "medium";

        [JsonPropertyName("allowsFriendly")]
        public bool AllowsFriendly { get; set; } = true;

        [JsonPropertyName("allowsHostile")]
        public bool AllowsHostile { get; set; } = true;

        [JsonPropertyName("sourceAssertionId")]
        public string SourceAssertionId { get; set; } = "";
    }

    public async Task<ScenarioCoreManifest?> ReadAsync()
    {
        var raw = await _fs.ReadFileAsync(ManifestPath);
        if (string.IsNullOrWhiteSpace(raw))
            return null;

        try
        {
            return JsonSerializer.Deserialize<ScenarioCoreManifest>(raw, JsonOpts);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Не удалось прочитать next-life scenario core manifest");
            return null;
        }
    }

    public async Task RefreshFromPendingSetupAsync()
    {
        var pendingRaw = await _fs.ReadFileAsync(WorldDirectiveService.PendingSetupPath);
        if (string.IsNullOrWhiteSpace(pendingRaw))
        {
            _fs.DeleteFile(ManifestPath);
            return;
        }

        WorldDirectiveService.PendingWorldSetup? pendingSetup;
        try
        {
            pendingSetup = JsonSerializer.Deserialize<WorldDirectiveService.PendingWorldSetup>(pendingRaw, JsonOpts);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Не удалось распарсить pending world setup для scenario core");
            return;
        }

        if (pendingSetup == null)
        {
            _fs.DeleteFile(ManifestPath);
            return;
        }

        var existing = await ReadAsync();
        var confirmedCandidateIds = existing?.ScenarioCoreAssertions
            .Where(assertion =>
                string.Equals(assertion.Source, "extracted_freeform_confirmed", StringComparison.OrdinalIgnoreCase) &&
                assertion.CandidateId is { Length: > 0 })
            .Select(assertion => assertion.CandidateId!)
            .ToHashSet(StringComparer.OrdinalIgnoreCase) ?? new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        var manifest = BuildManifest(pendingSetup, confirmedCandidateIds);
        await _fs.WriteFileAtomicAsync(ManifestPath, JsonSerializer.Serialize(manifest, JsonOpts));
    }

    public async Task SetCandidateConfirmedAsync(string candidateId, bool confirmed)
    {
        if (string.IsNullOrWhiteSpace(candidateId))
            return;

        var pendingRaw = await _fs.ReadFileAsync(WorldDirectiveService.PendingSetupPath);
        if (string.IsNullOrWhiteSpace(pendingRaw))
            return;

        WorldDirectiveService.PendingWorldSetup? pendingSetup;
        try
        {
            pendingSetup = JsonSerializer.Deserialize<WorldDirectiveService.PendingWorldSetup>(pendingRaw, JsonOpts);
        }
        catch
        {
            return;
        }

        if (pendingSetup == null)
            return;

        var existing = await ReadAsync();
        var confirmedCandidateIds = existing?.ScenarioCoreAssertions
            .Where(assertion =>
                string.Equals(assertion.Source, "extracted_freeform_confirmed", StringComparison.OrdinalIgnoreCase) &&
                assertion.CandidateId is { Length: > 0 })
            .Select(assertion => assertion.CandidateId!)
            .ToHashSet(StringComparer.OrdinalIgnoreCase) ?? new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        if (confirmed)
            confirmedCandidateIds.Add(candidateId);
        else
            confirmedCandidateIds.Remove(candidateId);

        var manifest = BuildManifest(pendingSetup, confirmedCandidateIds);
        await _fs.WriteFileAtomicAsync(ManifestPath, JsonSerializer.Serialize(manifest, JsonOpts));
    }

    public async Task ClearAsync()
    {
        _fs.DeleteFile(ManifestPath);
        await Task.CompletedTask;
    }

    public async Task<string?> BuildSystemReminderFragmentAsync(string? currentRealm)
    {
        var manifest = await ReadAsync();
        if (manifest == null)
            return null;

        var realm = currentRealm ?? "";
        var isRelevantRealm =
            string.Equals(realm, "Chaos Sea", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(realm, "Shining Abode", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(realm, "Mortal World", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(realm, "Море Хаоса", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(realm, "Сияющая Обитель", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(realm, "Мир Смертных", StringComparison.OrdinalIgnoreCase);

        if (!isRelevantRealm)
            return null;

        var parts = new List<string>
        {
            "NEXT-LIFE SCENARIO CORE:",
            $"  - Client-authored manifest exists at {ManifestPath}.",
            "  - scenarioCoreAssertions are hard confirmed start facts and must not be contradicted.",
            "  - candidateAssertions are not binding until they are explicitly confirmed by the player."
        };

        foreach (var assertion in manifest.ScenarioCoreAssertions.Take(8))
            parts.Add($"  - [{assertion.Category}] {assertion.Value}");

        if (manifest.CandidateAssertions.Count > 0)
            parts.Add($"  - Pending unconfirmed extracted facts: {manifest.CandidateAssertions.Count}");

        if (manifest.OpenCorrectionSlots.Count > 0)
            parts.Add($"  - Open correction slots available for compatible additions: {manifest.OpenCorrectionSlots.Count}");

        return string.Join(Environment.NewLine, parts);
    }

    private ScenarioCoreManifest BuildManifest(WorldDirectiveService.PendingWorldSetup setup, HashSet<string> confirmedCandidateIds)
    {
        var candidates = new List<ScenarioCandidateAssertion>();
        var core = new List<ScenarioCoreAssertion>();

        AddStructuredCoreAssertion(core, "identity_anchor", setup.CharacterDescription, "structured_field");
        AddStructuredCoreAssertion(core, "start_location", setup.StartingCircumstances, "structured_field");
        AddStructuredCoreAssertion(core, "world_premise", setup.WorldDirectives.WorldTitle, "structured_field");
        AddStructuredCoreAssertion(core, "world_premise", setup.WorldDirectives.Genre, "structured_field");
        AddStructuredCoreAssertion(core, "world_premise", setup.WorldDirectives.Era, "structured_field");
        AddStructuredCoreAssertion(core, "world_premise", setup.WorldDirectives.Tone, "structured_field");

        foreach (var item in setup.WorldDirectives.HardRules)
            AddStructuredCoreAssertion(core, "world_condition", item, "structured_field");
        foreach (var item in setup.WorldDirectives.RequiredElements)
            AddStructuredCoreAssertion(core, "world_condition", item, "structured_field");
        foreach (var item in setup.WorldDirectives.ForbiddenElements)
            AddStructuredCoreAssertion(core, "world_condition", item, "structured_field");
        foreach (var item in setup.WorldDirectives.SpecialMechanics)
            AddStructuredCoreAssertion(core, "world_condition", item, "structured_field");
        foreach (var item in setup.WorldDirectives.ContinuityNotes)
            AddStructuredCoreAssertion(core, "world_condition", item, "structured_field");
        foreach (var item in setup.WorldDirectives.PlayerAmendments)
            AddStructuredCoreAssertion(core, "world_condition", item, "structured_field");

        ExtractAssertionsFromText(
            setup.CharacterDescription,
            "character_description",
            addAsCore: true,
            candidates,
            core,
            confirmedCandidateIds);
        ExtractAssertionsFromText(
            setup.StartingCircumstances,
            "starting_circumstances",
            addAsCore: true,
            candidates,
            core,
            confirmedCandidateIds);
        ExtractAssertionsFromText(
            setup.WorldDirectives.SettingSummary,
            "setting_summary",
            addAsCore: false,
            candidates,
            core,
            confirmedCandidateIds);
        ExtractAssertionsFromText(
            setup.WorldDirectives.DetailedWorldDescription,
            "detailed_world_description",
            addAsCore: false,
            candidates,
            core,
            confirmedCandidateIds);

        core = core
            .GroupBy(assertion => $"{assertion.Category}::{NormalizeForId(assertion.Value)}", StringComparer.OrdinalIgnoreCase)
            .Select(group => group.First())
            .ToList();
        candidates = candidates
            .Where(candidate => core.All(assertion => !string.Equals(assertion.CandidateId, candidate.CandidateId, StringComparison.OrdinalIgnoreCase)))
            .GroupBy(candidate => candidate.CandidateId, StringComparer.OrdinalIgnoreCase)
            .Select(group => group.First())
            .ToList();

        var slots = BuildCorrectionSlots(core);
        return new ScenarioCoreManifest
        {
            SourceLastUpdated = setup.LastUpdated,
            LastExtractedAt = DateTime.UtcNow.ToString("o"),
            CandidateAssertions = candidates,
            ScenarioCoreAssertions = core,
            OpenCorrectionSlots = slots
        };
    }

    private static List<ScenarioCorrectionSlot> BuildCorrectionSlots(IEnumerable<ScenarioCoreAssertion> coreAssertions)
    {
        var slots = new List<ScenarioCorrectionSlot>();
        foreach (var assertion in coreAssertions)
        {
            if (!SlotTypesByCategory.TryGetValue(assertion.Category, out var slotTypes))
                continue;

            foreach (var slotType in slotTypes)
            {
                slots.Add(new ScenarioCorrectionSlot
                {
                    SlotId = BuildStableId("slot", slotType, assertion.AssertionId),
                    SlotType = slotType,
                    MaxSeverity = StrongSlots.Contains(slotType) ? "strong" : "medium",
                    AllowsFriendly = true,
                    AllowsHostile = true,
                    SourceAssertionId = assertion.AssertionId
                });
            }
        }

        return slots
            .GroupBy(slot => slot.SlotId, StringComparer.OrdinalIgnoreCase)
            .Select(group => group.First())
            .ToList();
    }

    private void ExtractAssertionsFromText(
        string? text,
        string sourceField,
        bool addAsCore,
        List<ScenarioCandidateAssertion> candidates,
        List<ScenarioCoreAssertion> core,
        HashSet<string> confirmedCandidateIds)
    {
        foreach (var fragment in SplitIntoFragments(text))
        {
            var category = ClassifyFragment(fragment);
            if (string.IsNullOrWhiteSpace(category))
                continue;

            var candidateId = BuildStableId("cand", category, fragment);
            var candidate = new ScenarioCandidateAssertion
            {
                CandidateId = candidateId,
                Source = "extracted_freeform",
                Text = fragment,
                Category = category,
                Confidence = addAsCore ? 0.9 : 0.72
            };

            candidates.Add(candidate);

            if (addAsCore)
            {
                core.Add(new ScenarioCoreAssertion
                {
                    AssertionId = BuildStableId("core", category, fragment),
                    Category = category,
                    Value = fragment,
                    Explicit = true,
                    Source = "structured_field",
                    CandidateId = candidateId
                });
                continue;
            }

            if (!confirmedCandidateIds.Contains(candidateId))
                continue;

            core.Add(new ScenarioCoreAssertion
            {
                AssertionId = BuildStableId("core", category, fragment),
                Category = category,
                Value = fragment,
                Explicit = true,
                Source = "extracted_freeform_confirmed",
                CandidateId = candidateId
            });
        }
    }

    private static void AddStructuredCoreAssertion(List<ScenarioCoreAssertion> core, string category, string? value, string source)
    {
        if (string.IsNullOrWhiteSpace(value))
            return;

        var normalized = value.Trim();
        core.Add(new ScenarioCoreAssertion
        {
            AssertionId = BuildStableId("core", category, normalized),
            Category = category,
            Value = normalized,
            Explicit = true,
            Source = source
        });
    }

    private static IEnumerable<string> SplitIntoFragments(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
            yield break;

        foreach (var raw in text
                     .Replace("\r\n", "\n", StringComparison.Ordinal)
                     .Split(['\n', '.', ';', '!', '?'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var fragment = raw.Trim();
            if (fragment.Length < 8)
                continue;

            yield return fragment;
        }
    }

    private static string ClassifyFragment(string fragment)
    {
        if (ContainsAny(fragment, RoleKeywords))
            return "role_status";
        if (ContainsAny(fragment, RelationshipKeywords))
            return "starting_relationship";
        if (ContainsAny(fragment, ResourceKeywords))
            return "starting_resources";
        if (ContainsAny(fragment, StartLocationKeywords))
            return "start_location";
        if (ContainsAny(fragment, WorldConditionKeywords))
            return "world_condition";
        return "world_premise";
    }

    private static bool ContainsAny(string text, IEnumerable<string> keywords)
    {
        foreach (var keyword in keywords)
        {
            if (text.Contains(keyword, StringComparison.OrdinalIgnoreCase))
                return true;
        }

        return false;
    }

    private static string BuildStableId(string prefix, params string[] parts)
    {
        var normalized = string.Join("|", parts.Select(NormalizeForId));
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(normalized));
        var hash = Convert.ToHexString(bytes)[..12].ToLowerInvariant();
        return $"{prefix}_{hash}";
    }

    private static string NormalizeForId(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return string.Empty;

        var lowered = value.Trim().ToLowerInvariant();
        var builder = new StringBuilder(lowered.Length);
        foreach (var ch in lowered)
        {
            if (char.IsLetterOrDigit(ch))
                builder.Append(ch);
            else if (!char.IsWhiteSpace(ch))
                builder.Append('_');
        }

        return builder.ToString();
    }
}
