using System.Text.Json;
using BookOfEternityClient.Models;

namespace BookOfEternityClient.Services;

public partial class ValidationService
{
    private async Task ValidateMortalBootstrapContentAnchorsAsync(List<ValidationIssue> issues)
    {
        if (!IsMortalRealmName(await TryResolveCurrentRealmAsync()))
            return;

        var scaffoldRaw = await _fs.ReadFileAsync(MortalBootstrapScaffoldContractPath);
        if (string.IsNullOrWhiteSpace(scaffoldRaw))
            return;

        try
        {
            using var scaffold = JsonDocument.Parse(scaffoldRaw);
            if (scaffold.RootElement.ValueKind != JsonValueKind.Object)
                return;
            if (!await IsCurrentMortalBootstrapScaffoldTurnAsync(scaffold.RootElement))
                return;

            await ValidateMortalBootstrapStarterCompetenciesAsync(scaffold.RootElement, issues);
            await ValidateMortalBootstrapOpeningWorldEventAsync(scaffold.RootElement, issues);
        }
        catch (JsonException)
        {
            // JSON integrity validation reports malformed scaffold files.
        }
    }

    private async Task<bool> IsCurrentMortalBootstrapScaffoldTurnAsync(JsonElement scaffold)
    {
        var readyRaw = await _fs.ReadFileAsync("ready/turn_complete.json");
        if (string.IsNullOrWhiteSpace(readyRaw))
            return false;

        try
        {
            using var ready = JsonDocument.Parse(readyRaw);
            if (ready.RootElement.ValueKind != JsonValueKind.Object)
                return false;

            var scaffoldRequestId = GetMortalBootstrapAnchorString(scaffold, "requestId");
            var readyRequestId = GetMortalBootstrapAnchorString(ready.RootElement, "requestId");
            if (!string.IsNullOrWhiteSpace(scaffoldRequestId) &&
                !string.Equals(scaffoldRequestId, readyRequestId, StringComparison.Ordinal))
            {
                return false;
            }

            if (scaffold.TryGetProperty("turnNumber", out var scaffoldTurn) &&
                scaffoldTurn.TryGetInt32(out var expectedTurn))
            {
                return ready.RootElement.TryGetProperty("turnNumber", out var readyTurn) &&
                       readyTurn.TryGetInt32(out var actualTurn) &&
                       actualTurn == expectedTurn;
            }

            return true;
        }
        catch (JsonException)
        {
            return false;
        }
    }

    private async Task ValidateMortalBootstrapStarterCompetenciesAsync(
        JsonElement scaffold,
        List<ValidationIssue> issues)
    {
        if (!scaffold.TryGetProperty("starterCompetencyRequirements", out var requirements) ||
            requirements.ValueKind != JsonValueKind.Array)
        {
            return;
        }

        foreach (var requirement in requirements.EnumerateArray())
        {
            if (requirement.ValueKind != JsonValueKind.Object)
                continue;

            var skillKind = GetMortalBootstrapAnchorString(requirement, "skillKind");
            var skillId = GetMortalBootstrapAnchorString(requirement, "skillId");
            var skillName = GetMortalBootstrapAnchorString(requirement, "skillName");
            var active = string.Equals(skillKind, "active", StringComparison.OrdinalIgnoreCase);
            var path = active
                ? "game_state/player/skills_active.json"
                : "game_state/player/skills_passive.json";
            var collectionName = active ? "activeSkillChanges" : "passiveSkillChanges";

            if (!await MortalBootstrapSkillExistsAsync(path, collectionName, skillId, skillName))
            {
                AddMissingMortalBootstrapCompetencyIssue(
                    $"{path}.{collectionName}",
                    skillKind,
                    skillId,
                    skillName,
                    "missing from canonical player skill state",
                    issues);
            }

            if (active && !await MortalBootstrapActiveSkillMasteryExistsAsync(skillId, skillName))
            {
                AddMissingMortalBootstrapCompetencyIssue(
                    "game_state/player/skill_mastery.json.skillMasteryChanges",
                    skillKind,
                    skillId,
                    skillName,
                    "missing matching active skill mastery entry",
                    issues);
            }
        }
    }

    private static void AddMissingMortalBootstrapCompetencyIssue(
        string path,
        string skillKind,
        string skillId,
        string skillName,
        string actual,
        List<ValidationIssue> issues)
    {
        issues.Add(new ValidationIssue(
            path,
            IssueSeverity.Error,
            $"Mortal bootstrap потерял явно заданную компетенцию персонажа «{skillName}».",
            code: "mortal_bootstrap_explicit_competency_missing",
            section: "MortalBootstrap",
            expected: $"{skillKind} skill {skillId}/{skillName} from starterCompetencyRequirements",
            actual: actual,
            repairHint: "Восстанови навык из game_state/control/mortal_bootstrap_scaffold.json.starterCompetencyRequirements в соответствующем skills_active/skills_passive файле. Для active-навыка сохрани matching skill mastery entry; не заменяй явную компетенцию одной прозой."));
    }

    private async Task<bool> MortalBootstrapSkillExistsAsync(
        string path,
        string collectionName,
        string skillId,
        string skillName)
    {
        var raw = await _fs.ReadFileAsync(path);
        if (string.IsNullOrWhiteSpace(raw))
            return false;

        try
        {
            using var doc = JsonDocument.Parse(raw);
            if (!doc.RootElement.TryGetProperty(collectionName, out var skills) ||
                skills.ValueKind != JsonValueKind.Array)
            {
                return false;
            }

            return skills.EnumerateArray().Any(skill =>
                skill.ValueKind == JsonValueKind.Object &&
                ((!string.IsNullOrWhiteSpace(skillId) &&
                  string.Equals(GetMortalBootstrapAnchorString(skill, "skillId"), skillId, StringComparison.OrdinalIgnoreCase)) ||
                 (!string.IsNullOrWhiteSpace(skillName) &&
                  string.Equals(GetMortalBootstrapAnchorString(skill, "skillName"), skillName, StringComparison.OrdinalIgnoreCase))));
        }
        catch (JsonException)
        {
            return false;
        }
    }

    private async Task<bool> MortalBootstrapActiveSkillMasteryExistsAsync(string skillId, string skillName)
    {
        var raw = await _fs.ReadFileAsync("game_state/player/skill_mastery.json");
        if (string.IsNullOrWhiteSpace(raw))
            return false;

        try
        {
            using var doc = JsonDocument.Parse(raw);
            if (!doc.RootElement.TryGetProperty("skillMasteryChanges", out var mastery) ||
                mastery.ValueKind != JsonValueKind.Array)
            {
                return false;
            }

            return mastery.EnumerateArray().Any(entry =>
                entry.ValueKind == JsonValueKind.Object &&
                ((!string.IsNullOrWhiteSpace(skillId) &&
                  string.Equals(GetMortalBootstrapAnchorString(entry, "skillId"), skillId, StringComparison.OrdinalIgnoreCase)) ||
                 (!string.IsNullOrWhiteSpace(skillName) &&
                  string.Equals(GetMortalBootstrapAnchorString(entry, "skillName"), skillName, StringComparison.OrdinalIgnoreCase))));
        }
        catch (JsonException)
        {
            return false;
        }
    }

    private async Task ValidateMortalBootstrapOpeningWorldEventAsync(
        JsonElement scaffold,
        List<ValidationIssue> issues)
    {
        if (!scaffold.TryGetProperty("worldEventRequirements", out var requirements) ||
            requirements.ValueKind != JsonValueKind.Object)
        {
            return;
        }

        var minimumCount = requirements.TryGetProperty("minimumCount", out var minimumElement) &&
                           minimumElement.TryGetInt32(out var parsedMinimum)
            ? Math.Max(0, parsedMinimum)
            : 0;
        var requiredIds = requirements.TryGetProperty("requiredEventIds", out var idsElement) &&
                          idsElement.ValueKind == JsonValueKind.Array
            ? idsElement.EnumerateArray()
                .Where(item => item.ValueKind == JsonValueKind.String)
                .Select(item => item.GetString() ?? string.Empty)
                .Where(item => !string.IsNullOrWhiteSpace(item))
                .ToArray()
            : [];

        const string path = "game_state/world/world_events.json";
        var eventIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var eventCount = 0;
        var raw = await _fs.ReadFileAsync(path);
        if (!string.IsNullOrWhiteSpace(raw))
        {
            try
            {
                using var doc = JsonDocument.Parse(raw);
                var events = default(JsonElement);
                if (doc.RootElement.ValueKind == JsonValueKind.Array)
                {
                    events = doc.RootElement;
                }
                else if (doc.RootElement.ValueKind == JsonValueKind.Object &&
                         doc.RootElement.TryGetProperty("worldEventsLog", out var worldEvents) &&
                         worldEvents.ValueKind == JsonValueKind.Array)
                {
                    events = worldEvents;
                }
                if (events.ValueKind == JsonValueKind.Array)
                {
                    foreach (var worldEvent in events.EnumerateArray())
                    {
                        if (worldEvent.ValueKind != JsonValueKind.Object)
                            continue;

                        eventCount++;
                        var eventId = GetMortalBootstrapAnchorString(worldEvent, "eventId");
                        if (!string.IsNullOrWhiteSpace(eventId))
                            eventIds.Add(eventId);
                    }
                }
            }
            catch (JsonException)
            {
                // JSON integrity validation reports malformed world event files.
            }
        }

        var missingIds = requiredIds.Where(id => !eventIds.Contains(id)).ToArray();
        if (eventCount >= minimumCount && missingIds.Length == 0)
            return;

        issues.Add(new ValidationIssue(
            $"{path}.worldEventsLog",
            IssueSeverity.Error,
            "Mortal bootstrap оставил /новости_мира без обязательного события стартовой сцены.",
            code: "mortal_bootstrap_world_event_missing",
            section: "MortalBootstrap",
            expected: $">= {minimumCount} events and ids [{string.Join(", ", requiredIds)}]",
            actual: $"count={eventCount}; missingIds=[{string.Join(", ", missingIds)}]",
            repairHint: "Восстанови client-authored opening event из mortal_bootstrap_scaffold.json.worldEventRequirements в game_state/world/world_events.json.worldEventsLog. Сохрани конкретное событие из playerAuthoredStart, чтобы /новости_мира было полезно сразу после воплощения."));
    }

    private static string GetMortalBootstrapAnchorString(JsonElement element, string propertyName) =>
        element.ValueKind == JsonValueKind.Object &&
        element.TryGetProperty(propertyName, out var value) &&
        value.ValueKind == JsonValueKind.String
            ? value.GetString() ?? string.Empty
            : string.Empty;
}
