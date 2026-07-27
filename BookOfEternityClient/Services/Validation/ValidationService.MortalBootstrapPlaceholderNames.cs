using System.Text.Json;
using System.Text.RegularExpressions;
using BookOfEternityClient.Models;

namespace BookOfEternityClient.Services;

public partial class ValidationService
{
    private const string MortalBootstrapPlaceholderNameIssueCode = "mortal_bootstrap_placeholder_player_visible_name";
    private const string MortalBootstrapScaffoldContractPath = "game_state/control/mortal_bootstrap_scaffold.json";

    private static readonly Regex[] MortalBootstrapPlayerVisiblePlaceholderPatterns =
    [
        new(@"\bстартов(ая|ой|ую|ые|ых|ыми)?\s+сцен", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant),
        new(@"\bсил[аы]\s+стартов(ой|ая)?\s+сцен", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant),
        new(@"\bпуть\s+из\s+стартов(ой|ая)?\s+сцен", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant),
        new(@"\bближайш(ий|ая|ее)\s+выход\s+из\s+стартов(ой|ая)?\s+сцен", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant),
        new(@"\bнаставник(ца)?\s+стартов(ой|ая)?\s+сцен", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant),
        new(@"\b(start|starting|starter)\s+scene\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant),
        new(@"\bplaceholder\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant),
        new(@"\bscaffold\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant),
        new(@"\bdefault\s+(location|faction|npc|exit|scene)\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant),
        new(@"\bunnamed\s+(location|faction|npc|exit|scene)\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)
    ];

    private static readonly string[] MortalBootstrapPlayerVisibleNameFiles =
    [
        "game_state/world/current_location.json",
        "game_state/world/world_map.json",
        "game_state/factions/faction_core.json",
        "game_state/factions/faction_resources.json",
        "game_state/npcs/npc_core.json"
    ];

    private static readonly HashSet<string> MortalBootstrapPlayerVisibleIdentityFields =
        new(StringComparer.OrdinalIgnoreCase)
        {
            "name",
            "displayName",
            "title",
            "targetName",
            "npcName",
            "locationName",
            "factionName",
            "questName"
        };

    private async Task ValidateMortalBootstrapPlayerVisibleNamesAsync(List<ValidationIssue> issues)
    {
        if (!await IsAcceptedMortalBootstrapMaterializationAsync())
            return;

        foreach (var relativePath in MortalBootstrapPlayerVisibleNameFiles)
            await ValidateMortalBootstrapPlayerVisibleNamesInFileAsync(relativePath, issues);
    }

    private async Task<bool> IsAcceptedMortalBootstrapMaterializationAsync()
    {
        if (!IsMortalRealmName(await TryResolveCurrentRealmAsync()))
            return false;

        if (!_fs.FileExists(MortalBootstrapScaffoldContractPath) ||
            !_fs.FileExists("ready/turn_complete.json"))
        {
            return false;
        }

        var manifest = await LoadValidatedCurrentPendingTurnSnapshotManifestAsync();
        return IsLoreBootstrapPendingTransitionSource(manifest?.SourceLabel) ||
               await MortalBootstrapScaffoldHasFreshPurposeAsync();
    }

    private async Task<bool> MortalBootstrapScaffoldHasFreshPurposeAsync()
    {
        var raw = await _fs.ReadFileAsync(MortalBootstrapScaffoldContractPath);
        if (string.IsNullOrWhiteSpace(raw))
            return false;

        try
        {
            using var doc = JsonDocument.Parse(raw);
            if (doc.RootElement.ValueKind != JsonValueKind.Object)
                return false;

            return doc.RootElement.TryGetProperty("purpose", out var purpose) &&
                   purpose.ValueKind == JsonValueKind.String &&
                   string.Equals(purpose.GetString(), "fresh_mortal_world_bootstrap", StringComparison.OrdinalIgnoreCase);
        }
        catch (JsonException)
        {
            return false;
        }
    }

    private async Task ValidateMortalBootstrapPlayerVisibleNamesInFileAsync(
        string relativePath,
        List<ValidationIssue> issues)
    {
        var raw = await _fs.ReadFileAsync(relativePath);
        if (string.IsNullOrWhiteSpace(raw))
            return;

        try
        {
            using var doc = JsonDocument.Parse(raw);
            ValidateMortalBootstrapPlayerVisibleNamesInElement(
                doc.RootElement,
                relativePath,
                propertyName: null,
                issues);
        }
        catch (JsonException)
        {
            // JSON integrity validation reports malformed files.
        }
    }

    private void ValidateMortalBootstrapPlayerVisibleNamesInElement(
        JsonElement element,
        string context,
        string? propertyName,
        List<ValidationIssue> issues)
    {
        switch (element.ValueKind)
        {
            case JsonValueKind.Object:
                foreach (var property in element.EnumerateObject())
                    ValidateMortalBootstrapPlayerVisibleNamesInElement(
                        property.Value,
                        $"{context}.{property.Name}",
                        property.Name,
                        issues);
                break;
            case JsonValueKind.Array:
                var index = 0;
                foreach (var item in element.EnumerateArray())
                    ValidateMortalBootstrapPlayerVisibleNamesInElement(
                        item,
                        $"{context}[{index++}]",
                        propertyName,
                        issues);
                break;
            case JsonValueKind.String:
                var value = element.GetString();
                if (MortalBootstrapPlayerVisibleIdentityFields.Contains(propertyName ?? string.Empty) &&
                    ContainsMortalBootstrapPlaceholderName(value))
                    AddMortalBootstrapPlaceholderNameIssue(context, value!, issues);
                break;
        }
    }

    private static bool ContainsMortalBootstrapPlaceholderName(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return false;

        return MortalBootstrapPlayerVisiblePlaceholderPatterns.Any(pattern => pattern.IsMatch(value));
    }

    private static void AddMortalBootstrapPlaceholderNameIssue(
        string context,
        string actual,
        List<ValidationIssue> issues)
    {
        issues.Add(new ValidationIssue(
            context,
            IssueSeverity.Error,
            "Accepted Mortal bootstrap оставил player-visible scaffold/placeholder name вместо конкретного названия мира.",
            code: MortalBootstrapPlaceholderNameIssueCode,
            section: "MortalBootstrap",
            expected: "конкретное in-world название локации, выхода, фракции или NPC",
            actual: actual,
            repairHint: "Не удаляй сущность и не жди автогенерации клиента. Замени scaffold label на художественно уместное название, основанное на playerAuthoredStart.characterDescription/worldDescription/startingCircumstances из game_state/control/mortal_bootstrap_scaffold.json."));
    }
}
