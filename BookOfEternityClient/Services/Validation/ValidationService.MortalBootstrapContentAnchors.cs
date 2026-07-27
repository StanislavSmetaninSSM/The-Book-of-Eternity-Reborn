using System.Text.Json;
using System.Text.Json.Nodes;
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
            await ValidateMortalBootstrapSettingOwnedMechanicsAuthorityAsync(
                scaffold.RootElement,
                issues);
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

    private async Task ValidateMortalBootstrapSettingOwnedMechanicsAuthorityAsync(
        JsonElement scaffold,
        List<ValidationIssue> issues)
    {
        var authority = scaffold.TryGetProperty("structuredGmAuthority", out var candidate) &&
                        candidate.ValueKind == JsonValueKind.Object
            ? candidate
            : default;

        var progressionClaims = await ReadMortalBootstrapRootMechanicClaimsAsync(
            "game_state/player/experience.json",
            [
                "playerLevel",
                "level",
                "currentExperience",
                "experience",
                "totalExperience",
                "experienceForNextLevel",
                "experienceGained"
            ]);
        if (progressionClaims.Count > 0 &&
            !MortalBootstrapAuthorityBindsAllClaims(
                authority,
                "playerProgression",
                progressionClaims))
        {
            issues.Add(new ValidationIssue(
                "game_state/player/experience.json",
                IssueSeverity.Error,
                "Стартовая прогрессия смертного мира появилась без явного решения ГМа.",
                code: "mortal_bootstrap_progression_requires_structured_gm_authority",
                section: "MortalBootstrap",
                expected: "structuredGmAuthority.playerProgression entry with canonicalPath=game_state/player/experience.json and non-empty exact-value values",
                actual: "progression fields exist without structured GM authority",
                repairHint: "Добавь в structuredGmAuthority.playerProgression запись с canonicalPath=game_state/player/experience.json и values, точно повторяющим полный progression tuple из experience.json. Не используй клиентский порог по умолчанию; reason без values не даёт authority."));
        }

        var carryingClaims = await ReadMortalBootstrapRootMechanicClaimsAsync(
            "game_state/inventory/items.json",
            ["maxWeight", "totalWeight"]);
        if (carryingClaims.Count > 0 &&
            !MortalBootstrapAuthorityBindsAllClaims(
                authority,
                "carryingRules",
                carryingClaims))
        {
            issues.Add(new ValidationIssue(
                "game_state/inventory/items.json",
                IssueSeverity.Error,
                "Стартовая грузоподъёмность появилась без явного правила текущего мира.",
                code: "mortal_bootstrap_carrying_requires_structured_gm_authority",
                section: "MortalBootstrap",
                expected: "structuredGmAuthority.carryingRules entry with canonicalPath=game_state/inventory/items.json and non-empty exact-value values",
                actual: "carrying fields exist without structured GM authority",
                repairHint: "Добавь в structuredGmAuthority.carryingRules запись с canonicalPath=game_state/inventory/items.json и values, точно повторяющим maxWeight/totalWeight. Если мир не задаёт такую механику, убери эти поля вместо изобретения универсальной формулы."));
        }

        var factionClaims = await ReadMortalBootstrapFactionMechanicClaimsAsync();
        if (factionClaims.Count > 0 &&
            !MortalBootstrapAuthorityBindsAllClaims(
                authority,
                "factionMechanics",
                factionClaims))
        {
            issues.Add(new ValidationIssue(
                "game_state/factions/faction_core.json",
                IssueSeverity.Error,
                "Стартовые числовые механики фракции появились без явного решения ГМа.",
                code: "mortal_bootstrap_faction_mechanics_require_structured_gm_authority",
                section: "MortalBootstrap",
                expected: "structuredGmAuthority.factionMechanics entries with exact canonicalPath, factionId, and non-empty exact-value values",
                actual: "faction mechanics exist without structured GM authority",
                repairHint: "Для каждой затронутой canonical surface добавь factionMechanics запись с точными canonicalPath, factionId и values, повторяющими её механику. Не копируй универсальный фэнтезийный powerProfile и не заменяй структурную authority прозой."));
        }
    }

    private static bool MortalBootstrapAuthorityBindsAllClaims(
        JsonElement authority,
        string propertyName,
        IReadOnlyCollection<MortalBootstrapAuthorityClaim> claims)
    {
        if (authority.ValueKind != JsonValueKind.Object ||
            !authority.TryGetProperty(propertyName, out var entries) ||
            entries.ValueKind != JsonValueKind.Array)
        {
            return false;
        }

        var authorityEntries = entries
            .EnumerateArray()
            .Where(entry => entry.ValueKind == JsonValueKind.Object)
            .ToArray();
        return claims.All(claim => authorityEntries.Any(entry =>
            MortalBootstrapAuthorityEntryBindsClaim(entry, claim)));
    }

    private static bool MortalBootstrapAuthorityEntryBindsClaim(
        JsonElement entry,
        MortalBootstrapAuthorityClaim claim)
    {
        if (!entry.TryGetProperty("canonicalPath", out var pathNode) ||
            pathNode.ValueKind != JsonValueKind.String ||
            !string.Equals(
                pathNode.GetString()?.Trim(),
                claim.CanonicalPath,
                StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (!string.IsNullOrWhiteSpace(claim.FactionId) &&
            (!entry.TryGetProperty("factionId", out var factionIdNode) ||
             factionIdNode.ValueKind != JsonValueKind.String ||
             !string.Equals(
                 factionIdNode.GetString()?.Trim(),
                 claim.FactionId,
                 StringComparison.Ordinal)))
        {
            return false;
        }

        if (!entry.TryGetProperty("values", out var valuesNode) ||
            valuesNode.ValueKind != JsonValueKind.Object ||
            !valuesNode.EnumerateObject().Any())
        {
            return false;
        }

        foreach (var (propertyName, expectedValue) in claim.Values)
        {
            if (!valuesNode.TryGetProperty(propertyName, out var actualValue) ||
                !JsonNode.DeepEquals(
                    JsonNode.Parse(actualValue.GetRawText()),
                    expectedValue))
            {
                return false;
            }
        }

        return true;
    }

    private async Task<IReadOnlyList<MortalBootstrapAuthorityClaim>> ReadMortalBootstrapRootMechanicClaimsAsync(
        string path,
        IReadOnlyCollection<string> propertyNames)
    {
        var root = await ReadMortalBootstrapObjectAsync(path);
        if (root == null)
            return [];

        var values = new JsonObject();
        foreach (var propertyName in propertyNames)
        {
            if (root.TryGetPropertyValue(propertyName, out var value))
                values[propertyName] = value?.DeepClone();
        }

        return values.Count == 0
            ? []
            : [new MortalBootstrapAuthorityClaim(path, null, values)];
    }

    private async Task<IReadOnlyList<MortalBootstrapAuthorityClaim>> ReadMortalBootstrapFactionMechanicClaimsAsync()
    {
        var claims = new List<MortalBootstrapAuthorityClaim>();
        var factionRoot = await ReadMortalBootstrapObjectAsync(
            "game_state/factions/faction_core.json");
        if (factionRoot?["factions"] is JsonArray factions)
        {
            var mechanicalProperties = new[]
            {
                "developmentArchetype",
                "level",
                "experience",
                "experienceForNextLevel",
                "isPlayerFaction",
                "isPlayerMember",
                "reputation",
                "influence",
                "powerProfile",
                "resources"
            };
            foreach (var faction in factions.OfType<JsonObject>())
            {
                var values = new JsonObject();
                foreach (var propertyName in mechanicalProperties)
                {
                    if (faction.TryGetPropertyValue(propertyName, out var value))
                        values[propertyName] = value?.DeepClone();
                }

                if (values.Count == 0)
                    continue;

                claims.Add(new MortalBootstrapAuthorityClaim(
                    "game_state/factions/faction_core.json",
                    ReadMortalBootstrapNodeString(faction["factionId"]),
                    values));
            }
        }

        await AddMortalBootstrapArrayMechanicClaimsAsync(
            claims,
            "game_state/factions/faction_resources.json",
            "entries",
            ["factionId", "factionName"]);
        await AddMortalBootstrapArrayMechanicClaimsAsync(
            claims,
            "game_state/world/current_location.json",
            "factionControl",
            ["factionId", "factionName"]);
        return claims;
    }

    private async Task AddMortalBootstrapArrayMechanicClaimsAsync(
        ICollection<MortalBootstrapAuthorityClaim> claims,
        string path,
        string arrayProperty,
        IReadOnlyCollection<string> identityProperties)
    {
        var root = await ReadMortalBootstrapObjectAsync(path);
        if (root?[arrayProperty] is not JsonArray entries)
            return;

        foreach (var entry in entries.OfType<JsonObject>())
        {
            var values = new JsonObject();
            foreach (var (propertyName, value) in entry)
            {
                if (!identityProperties.Contains(propertyName, StringComparer.OrdinalIgnoreCase))
                    values[propertyName] = value?.DeepClone();
            }

            if (values.Count == 0)
                continue;

            claims.Add(new MortalBootstrapAuthorityClaim(
                path,
                ReadMortalBootstrapNodeString(entry["factionId"]),
                values));
        }
    }

    private async Task<JsonObject?> ReadMortalBootstrapObjectAsync(string path)
    {
        var raw = await _fs.ReadFileAsync(path);
        if (string.IsNullOrWhiteSpace(raw))
            return null;

        try
        {
            return JsonNode.Parse(raw) as JsonObject;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static string? ReadMortalBootstrapNodeString(JsonNode? node)
    {
        try
        {
            return node?.GetValue<string>()?.Trim();
        }
        catch (InvalidOperationException)
        {
            return null;
        }
    }

    private sealed record MortalBootstrapAuthorityClaim(
        string CanonicalPath,
        string? FactionId,
        JsonObject Values);

    private async Task ValidateMortalBootstrapStarterCompetenciesAsync(
        JsonElement scaffold,
        List<ValidationIssue> issues)
    {
        if (!scaffold.TryGetProperty("structuredGmAuthority", out var authority) ||
            authority.ValueKind != JsonValueKind.Object ||
            !authority.TryGetProperty("playerSkills", out var requirements) ||
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
            expected: $"{skillKind} skill {skillId}/{skillName} from structuredGmAuthority.playerSkills",
            actual: actual,
            repairHint: "Восстанови явно объявленный ГМ навык из game_state/control/mortal_bootstrap_scaffold.json.structuredGmAuthority.playerSkills в соответствующем skills_active/skills_passive файле. Для active-навыка сохрани matching skill mastery entry; playerAuthoredStart не является механическим авторитетом."));
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
