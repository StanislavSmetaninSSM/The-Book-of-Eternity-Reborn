using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using BookOfEternityClient.Core;
using BookOfEternityClient.Configuration;
using BookOfEternityClient.Models;
using Microsoft.Extensions.Logging;

namespace BookOfEternityClient.Services;
public partial class ValidationService
{
    private void ValidateDifficultyProfileObject(JsonElement root, string contextPrefix, List<ValidationIssue> issues, string propName)
    {
        if (!root.TryGetProperty(propName, out var profile))
        {
            issues.Add(new ValidationIssue(
                $"{contextPrefix}.{propName}",
                IssueSeverity.Error,
                $"{propName} обязателен для локации",
                code: "location_missing_difficulty_profile",
                section: "Location",
                expected: $"{propName} object with combat/environment/social/exploration integer scales",
                actual: "missing property",
                repairHint: $"Добавь в location object поле {propName} с integer-полями combat, environment, social и exploration."));
            return;
        }

        if (!RequireObject(profile, $"{contextPrefix}.{propName}", issues))
            return;

        foreach (var scale in new[] { "combat", "environment", "social", "exploration" })
            ValidateNonNegativeIntegerField(profile, $"{contextPrefix}.{propName}", issues, scale, "Location");
    }


    private void ValidateLocationAdjacencyArray(JsonElement root, string contextPrefix, List<ValidationIssue> issues, string propName)
    {
        if (!root.TryGetProperty(propName, out var value))
        {
            issues.Add(new ValidationIssue(
                $"{contextPrefix}.{propName}",
                IssueSeverity.Error,
                $"{propName} обязателен и должен быть массивом или null",
                code: "location_missing_adjacency_array",
                section: "Location",
                expected: $"{propName} array or null",
                actual: "missing property",
                repairHint: $"Добавь в location object поле {propName}. Используй null, если явных adjacency links нет, или массив canonical link objects."));
            return;
        }

        if (value.ValueKind == JsonValueKind.Null)
            return;
        if (value.ValueKind != JsonValueKind.Array)
        {
            issues.Add(new ValidationIssue(
                $"{contextPrefix}.{propName}",
                IssueSeverity.Error,
                $"{propName} должен быть массивом или null",
                code: "location_invalid_adjacency_array_shape",
                section: "Location",
                expected: $"{propName} array or null",
                actual: value.ValueKind.ToString(),
                repairHint: $"Передавай {propName} как null или массив canonical adjacency links, а не как {value.ValueKind}."));
            return;
        }

        var index = 0;
        foreach (var item in value.EnumerateArray())
        {
            var itemContext = $"{contextPrefix}.{propName}[{index++}]";
            if (!RequireObject(item, itemContext, issues))
                continue;

            ValidateWorldMapAdjacencyLinkObject(item, itemContext, issues);
        }
    }


    private void ValidateLocationStorageArray(JsonElement root, string contextPrefix, List<ValidationIssue> issues, string propName)
    {
        if (!root.TryGetProperty(propName, out var value))
        {
            issues.Add(new ValidationIssue(
                $"{contextPrefix}.{propName}",
                IssueSeverity.Error,
                $"{propName} обязателен и должен быть массивом или null",
                code: "location_missing_storage_array",
                section: "Location",
                expected: $"{propName} array or null",
                actual: "missing property",
                repairHint: $"Добавь в location object поле {propName}. Используй null, если storage entries нет, или массив canonical storage objects."));
            return;
        }

        if (value.ValueKind == JsonValueKind.Null)
            return;
        if (value.ValueKind != JsonValueKind.Array)
        {
            issues.Add(new ValidationIssue(
                $"{contextPrefix}.{propName}",
                IssueSeverity.Error,
                $"{propName} должен быть массивом или null",
                code: "location_invalid_storage_array_shape",
                section: "Location",
                expected: $"{propName} array or null",
                actual: value.ValueKind.ToString(),
                repairHint: $"Передавай {propName} как null или массив canonical location storages, а не как {value.ValueKind}."));
            return;
        }

        var index = 0;
        foreach (var item in value.EnumerateArray())
        {
            var itemContext = $"{contextPrefix}.{propName}[{index++}]";
            if (!RequireObject(item, itemContext, issues))
                continue;

            RequireString(item, itemContext, issues, "name");
            if (item.TryGetProperty("owner", out var owner))
            {
                if (!RequireObject(owner, $"{itemContext}.owner", issues))
                    continue;
                ValidateOptionalString(owner, $"{itemContext}.owner", issues, "ownerName");
                ValidateOptionalString(owner, $"{itemContext}.owner", issues, "ownerType");
            }
        }
    }


    private void ValidateActiveThreatArray(JsonElement root, string contextPrefix, List<ValidationIssue> issues, string propName)
    {
        if (!root.TryGetProperty(propName, out var value))
        {
            issues.Add(new ValidationIssue(
                $"{contextPrefix}.{propName}",
                IssueSeverity.Error,
                $"{propName} обязателен и должен быть массивом или null",
                code: "location_missing_active_threat_array",
                section: "Location",
                expected: $"{propName} array or null",
                actual: "missing property",
                repairHint: $"Добавь в location object поле {propName}. Используй null, если активных угроз нет, или массив canonical Active Threat Objects."));
            return;
        }

        if (value.ValueKind == JsonValueKind.Null)
            return;
        if (value.ValueKind != JsonValueKind.Array)
        {
            issues.Add(new ValidationIssue(
                $"{contextPrefix}.{propName}",
                IssueSeverity.Error,
                $"{propName} должен быть массивом или null",
                code: "location_invalid_active_threat_array_shape",
                section: "Location",
                expected: $"{propName} array or null",
                actual: value.ValueKind.ToString(),
                repairHint: $"Передавай {propName} как null или массив canonical Active Threat Objects, а не как {value.ValueKind}."));
            return;
        }

        var index = 0;
        foreach (var item in value.EnumerateArray())
        {
            var itemContext = $"{contextPrefix}.{propName}[{index++}]";
            if (!RequireObject(item, itemContext, issues))
                continue;

            ValidateActiveThreatObject(item, itemContext, issues, requireNullThreatId: false);
        }
    }


    private void ValidateCurrentLocationData(JsonElement root, string contextPrefix, List<ValidationIssue> issues)
    {
        var found = false;
        if (root.TryGetProperty("currentLocationData", out var location))
        {
            found = true;
            var locationContext = $"{contextPrefix}.currentLocationData";
            ValidateCurrentLocationResponseObject(location, locationContext, issues);
        }
        else if (root.ValueKind == JsonValueKind.Object &&
                 (root.TryGetProperty("locationId", out _) || root.TryGetProperty("locationType", out _)))
        {
            found = true;
            ValidateLocationObject(root, contextPrefix, issues);
        }

        if (!found && contextPrefix.EndsWith("current_location.json", StringComparison.OrdinalIgnoreCase))
        {
            issues.Add(new ValidationIssue(
                contextPrefix,
                IssueSeverity.Warning,
                "Файл локации не содержит currentLocationData и не похож на нормализованный location object",
                code: "current_location_missing_root_object",
                section: "Location",
                expected: "currentLocationData object or normalized location object",
                actual: "missing recognizable location root",
                repairHint: "Сохрани либо currentLocationData, либо нормализованный location object. Не оставляй current_location.json пустым или с нерелевантным root shape."));
        }
    }


    private void ValidateCurrentLocationResponseObject(JsonElement location, string context, List<ValidationIssue> issues)
    {
        if (!RequireObject(location, context, issues))
            return;

        if (!location.TryGetProperty("locationId", out var locationId))
        {
            issues.Add(new ValidationIssue(
                $"{context}.locationId",
                IssueSeverity.Error,
                "currentLocationData должен явно задавать locationId: GUID для известной локации или null для новой",
                code: "current_location_missing_location_id",
                section: "Location",
                expected: "string locationId or null for a new location",
                actual: "missing",
                repairHint: "Для known location передай locationId существующей локации, её coordinates и only lastEventsDescription. Для новой локации передай полный объект с locationId = null."));
            return;
        }

        if (locationId.ValueKind == JsonValueKind.Null)
        {
            ValidateNewLocationObject(location, context, issues);
            return;
        }

        if (locationId.ValueKind != JsonValueKind.String || string.IsNullOrWhiteSpace(locationId.GetString()))
        {
            issues.Add(new ValidationIssue(
                $"{context}.locationId",
                IssueSeverity.Error,
                "currentLocationData.locationId должен быть непустым GUID или null",
                code: "current_location_invalid_location_id",
                section: "Location",
                expected: "non-empty string GUID or null",
                actual: locationId.ValueKind.ToString(),
                repairHint: "Для known location используй её existing GUID. Только для truly new location передавай locationId = null."));
            return;
        }

        if (!location.TryGetProperty("coordinates", out var coordinates))
        {
            issues.Add(new ValidationIssue(
                $"{context}.coordinates",
                IssueSeverity.Error,
                "Для известной currentLocationData обязательно передавать coordinates",
                code: "current_location_missing_coordinates",
                section: "Location",
                expected: "coordinates object from the known location",
                actual: "missing",
                repairHint: "При возврате в known location передай её existing locationId, coordinates и lastEventsDescription. Legacy локации без z всё ещё допускаются как x/y-only coordinates."));
            return;
        }

        ValidateLocationCoordinatesObject(
            coordinates,
            $"{context}.coordinates",
            issues,
            allowLegacyMissingZ: true,
            section: "Location");

        if (location.TryGetProperty("internalDifficultyProfile", out _))
            ValidateDifficultyProfileObject(location, context, issues, "internalDifficultyProfile");

        if (location.TryGetProperty("externalDifficultyProfile", out _))
            ValidateDifficultyProfileObject(location, context, issues, "externalDifficultyProfile");

        if (location.TryGetProperty("locationStorages", out _))
            ValidateLocationStorageArray(location, context, issues, "locationStorages");

        var lastEventsDescription = RequireString(location, context, issues, "lastEventsDescription");
        if (!string.IsNullOrWhiteSpace(lastEventsDescription) &&
            !MatchesHistoricalEntryContract(lastEventsDescription))
        {
            issues.Add(new ValidationIssue(
                $"{context}.lastEventsDescription",
                IssueSeverity.Error,
                "currentLocationData.lastEventsDescription должен использовать canonical historical-entry timestamp format",
                code: "current_location_last_events_timestamp_invalid",
                section: "Location",
                expected: "#[Turn] - [Day] [Month] [Year] г., [HH:MM]: ...",
                actual: lastEventsDescription,
                repairHint: "При переходе в известную локацию передавай locationId, coordinates и lastEventsDescription с canonical timestamp-префиксом из Block 18.A."));
        }

        var forbiddenFields = location.EnumerateObject()
            .Where(prop => !string.Equals(prop.Name, "locationId", StringComparison.OrdinalIgnoreCase) &&
                           !string.Equals(prop.Name, "coordinates", StringComparison.OrdinalIgnoreCase) &&
                           !string.Equals(prop.Name, "lastEventsDescription", StringComparison.OrdinalIgnoreCase) &&
                           !string.Equals(prop.Name, "internalDifficultyProfile", StringComparison.OrdinalIgnoreCase) &&
                           !string.Equals(prop.Name, "externalDifficultyProfile", StringComparison.OrdinalIgnoreCase) &&
                           !string.Equals(prop.Name, "locationStorages", StringComparison.OrdinalIgnoreCase))
            .Select(prop => prop.Name)
            .ToList();
        if (forbiddenFields.Count > 0)
        {
            issues.Add(new ValidationIssue(
                context,
                IssueSeverity.Error,
                "Для известной currentLocationData запрещён полный location object resend",
                code: "current_location_known_location_resends_full_object",
                section: "Location",
                expected: "locationId + coordinates + lastEventsDescription, optionally internalDifficultyProfile/externalDifficultyProfile/locationStorages",
                actual: string.Join(", ", forbiddenFields),
                repairHint: "Для known location передавай только locationId, coordinates, lastEventsDescription и при необходимости обновляемые internalDifficultyProfile/externalDifficultyProfile/locationStorages. Полный объект допустим только для truly new location с locationId = null."));
        }
    }


    private void ValidateWorldMapUpdates(JsonElement root, string contextPrefix, List<ValidationIssue> issues)
    {
        JsonElement updates;
        string context;
        if (root.TryGetProperty("worldMapUpdates", out updates))
        {
            context = $"{contextPrefix}.worldMapUpdates";
            if (!RequireObject(updates, context, issues))
                return;
        }
        else
        {
            updates = root;
            context = contextPrefix;
        }

        if (updates.TryGetProperty("newLocations", out var newLocations))
        {
            RequireArrayOfObjects(newLocations, $"{context}.newLocations", issues);
            if (newLocations.ValueKind == JsonValueKind.Array)
            {
                var index = 0;
                foreach (var item in newLocations.EnumerateArray())
                {
                    if (item.ValueKind == JsonValueKind.Object)
                        ValidateNewLocationObject(item, $"{context}.newLocations[{index}]", issues);
                    index++;
                }
            }
        }
        if (updates.TryGetProperty("newLinks", out var newLinks))
            ValidateWorldMapNewLinks(newLinks, $"{context}.newLinks", issues);
        if (updates.TryGetProperty("locationUpdates", out var locationUpdates))
            ValidateLocationUpdateArray(locationUpdates, $"{context}.locationUpdates", issues);
        if (updates.TryGetProperty("storageUpdates", out var storageUpdates))
            ValidateLocationStorageUpdates(storageUpdates, $"{context}.storageUpdates", issues);
        if (updates.TryGetProperty("storagesToRemove", out var storagesToRemove))
            ValidateLocationStorageRemovals(storagesToRemove, $"{context}.storagesToRemove", issues);
        if (updates.TryGetProperty("linkUpdates", out var linkUpdates))
            ValidateLocationLinkUpdates(linkUpdates, $"{context}.linkUpdates", issues);
        if (updates.TryGetProperty("linksToRemove", out var linksToRemove))
            ValidateLocationLinkRemovals(linksToRemove, $"{context}.linksToRemove", issues);
        if (updates.TryGetProperty("threatsToAdd", out var threatsToAdd))
            ValidateLocationThreatAdds(threatsToAdd, $"{context}.threatsToAdd", issues);
        if (updates.TryGetProperty("threatsToUpdate", out var threatsToUpdate))
            ValidateLocationThreatUpdates(threatsToUpdate, $"{context}.threatsToUpdate", issues);
        if (updates.TryGetProperty("threatsToRemove", out var threatsToRemove))
            ValidateLocationThreatRemovals(threatsToRemove, $"{context}.threatsToRemove", issues);
        if (updates.TryGetProperty("completeThreatActivities", out var completeThreatActivities))
            ValidateLocationThreatCompletions(completeThreatActivities, $"{context}.completeThreatActivities", issues);
    }


    private void ValidateLocationObject(JsonElement location, string context, List<ValidationIssue> issues)
    {
        if (!RequireObject(location, context, issues))
            return;

        if (!HasAnyNonEmptyString(location, "locationId", "name"))
        {
            issues.Add(new ValidationIssue(context, IssueSeverity.Error,
                "Локация должна содержать locationId или name",
                code: "location_missing_identity",
                section: "Location",
                expected: "locationId or name",
                actual: "missing identity fields",
                repairHint: "Для location object передай locationId и/или name, чтобы клиент мог однозначно разрешить или создать локацию."));
        }

        ValidateOptionalString(location, context, issues, "name");
        ValidateOptionalString(location, context, issues, "description");
        ValidateOptionalString(location, context, issues, "lastEventsDescription");
        var lastEventsDescription = GetFirstNonEmptyString(location, "lastEventsDescription");
        if (!string.IsNullOrWhiteSpace(lastEventsDescription) &&
            !MatchesHistoricalEntryContract(lastEventsDescription))
        {
            issues.Add(new ValidationIssue(
                $"{context}.lastEventsDescription",
                IssueSeverity.Error,
                "lastEventsDescription должен использовать canonical historical-entry timestamp format",
                code: "location_last_events_timestamp_invalid",
                section: "Location",
                expected: "#[Turn] - [Day] [Month] [Year] г., [HH:MM]: ...",
                actual: lastEventsDescription,
                repairHint: "Начинай lastEventsDescription с полного timestamp-префикса из Block 18.A. В переходный период также допустим legacy '#[turn_number]. ...' если ты выравниваешь старый контент."));
        }
        if ((context.Contains(".currentLocationData", StringComparison.OrdinalIgnoreCase) ||
             context.Contains(".newLocations[", StringComparison.OrdinalIgnoreCase) ||
             context.Contains(".locationUpdates[", StringComparison.OrdinalIgnoreCase)) &&
            location.TryGetProperty("eventDescriptions", out _))
        {
            issues.Add(new ValidationIssue(
                $"{context}.eventDescriptions",
                IssueSeverity.Error,
                "eventDescriptions является read-only историческим логом и не должен приходить из GM-authored location update",
                code: "location_event_descriptions_forbidden",
                section: "Location",
                repairHint: "Не отправляй eventDescriptions в currentLocationData / worldMapUpdates. Читай историю оттуда, а в ответ пиши только lastEventsDescription."));
        }

        var locationType = RequireString(location, context, issues, "locationType");
        if (!string.IsNullOrWhiteSpace(locationType) &&
            !string.Equals(locationType, "outdoor", StringComparison.OrdinalIgnoreCase) &&
            !string.Equals(locationType, "indoor", StringComparison.OrdinalIgnoreCase))
        {
            issues.Add(new ValidationIssue(
                $"{context}.locationType",
                IssueSeverity.Error,
                "locationType должен быть 'outdoor' или 'indoor'",
                code: "location_type_invalid",
                section: "Location",
                expected: "outdoor or indoor",
                actual: locationType,
                repairHint: "Используй для locationType только canonical значения outdoor или indoor."));
        }

        if (location.TryGetProperty("coordinates", out var coordinates))
        {
            ValidateLocationCoordinatesObject(
                coordinates,
                $"{context}.coordinates",
                issues,
                allowLegacyMissingZ: HasNonEmptyString(location, "locationId"),
                section: "Location");
        }
        else
        {
            issues.Add(new ValidationIssue(
                $"{context}.coordinates",
                IssueSeverity.Error,
                "Локация должна содержать объект coordinates",
                code: "location_coordinates_missing",
                section: "Location",
                expected: "coordinates object",
                actual: "missing",
                repairHint: "Добавь coordinates с canonical x/y и при необходимости z. Даже для known-location shorthand coordinates остаются обязательными."));
        }

        ValidateDifficultyProfileObject(location, context, issues, "internalDifficultyProfile");
        ValidateDifficultyProfileObject(location, context, issues, "externalDifficultyProfile");

        if (string.Equals(locationType, "outdoor", StringComparison.OrdinalIgnoreCase) &&
            !HasNonEmptyString(location, "biome"))
        {
            issues.Add(new ValidationIssue(
                $"{context}.biome",
                IssueSeverity.Error,
                "Outdoor location обязан содержать biome"));
        }
        else if (string.Equals(locationType, "outdoor", StringComparison.OrdinalIgnoreCase))
        {
            var biome = GetFirstNonEmptyString(location, "biome");
            if (!string.IsNullOrWhiteSpace(biome) && !AllowedOutdoorBiomes.Contains(biome))
            {
                issues.Add(new ValidationIssue(
                    $"{context}.biome",
                    IssueSeverity.Error,
                    "Outdoor biome должен быть одним из canonical enum значений",
                    code: "location_invalid_biome",
                    section: "Location",
                    expected: string.Join(" | ", AllowedOutdoorBiomes),
                    actual: biome,
                    repairHint: "Используй для outdoor locations только canonical biome values из Block 20.5."));
            }

            if (string.Equals(biome, "Unique", StringComparison.OrdinalIgnoreCase) &&
                !HasNonEmptyString(location, "biomeDescription"))
            {
                issues.Add(new ValidationIssue(
                    $"{context}.biomeDescription",
                    IssueSeverity.Error,
                    "Outdoor biome = Unique требует непустой biomeDescription",
                    code: "location_unique_biome_missing_description",
                    section: "Location",
                    repairHint: "Для biome=Unique добавь biomeDescription, объясняющий уникальную природу среды."));
            }
        }

        if (!string.Equals(locationType, "outdoor", StringComparison.OrdinalIgnoreCase) &&
            location.TryGetProperty("biome", out var indoorBiome) &&
            indoorBiome.ValueKind != JsonValueKind.Null)
        {
            issues.Add(new ValidationIssue(
                $"{context}.biome",
                IssueSeverity.Error,
                "biome используется только для outdoor locations",
                code: "location_biome_forbidden_for_indoor",
                section: "Location",
                repairHint: "Убери biome у indoor location и при необходимости используй indoorType."));
        }

        if (location.TryGetProperty("indoorType", out var indoorType) && indoorType.ValueKind != JsonValueKind.Null)
        {
            if (!string.Equals(locationType, "indoor", StringComparison.OrdinalIgnoreCase))
            {
                issues.Add(new ValidationIssue(
                    $"{context}.indoorType",
                    IssueSeverity.Error,
                    "indoorType допустим только для indoor locations",
                    code: "location_indoor_type_forbidden_for_outdoor",
                    section: "Location",
                    repairHint: "Используй indoorType только при locationType = indoor."));
            }
            else if (indoorType.ValueKind != JsonValueKind.String || string.IsNullOrWhiteSpace(indoorType.GetString()))
            {
                issues.Add(new ValidationIssue(
                    $"{context}.indoorType",
                    IssueSeverity.Error,
                    "indoorType должен быть непустой строкой из canonical indoor type enum",
                    code: "location_invalid_indoor_type",
                    section: "Location",
                    expected: string.Join(" | ", AllowedIndoorLocationTypes),
                    actual: indoorType.ValueKind.ToString(),
                    repairHint: "Используй для indoorType только Building, Dungeon, CaveSystem, Vehicle или UniqueIndoor."));
            }
            else if (!AllowedIndoorLocationTypes.Contains(indoorType.GetString()!))
            {
                issues.Add(new ValidationIssue(
                    $"{context}.indoorType",
                    IssueSeverity.Error,
                    "indoorType должен быть одним из canonical enum значений",
                    code: "location_invalid_indoor_type",
                    section: "Location",
                    expected: string.Join(" | ", AllowedIndoorLocationTypes),
                    actual: indoorType.GetString(),
                    repairHint: "Используй для indoorType только Building, Dungeon, CaveSystem, Vehicle или UniqueIndoor."));
            }
        }

        ValidateOptionalString(location, context, issues, "image_prompt");
        if (!HasNonEmptyString(location, "locationId") && !HasNonEmptyString(location, "image_prompt"))
        {
            issues.Add(new ValidationIssue(
                $"{context}.image_prompt",
                IssueSeverity.Error,
                "Новая локация без locationId должна содержать image_prompt"));
        }
        ValidateFactionControlArray(location, context, issues, "factionControl");
        ValidateLocationAdjacencyArray(location, context, issues, "adjacencyMap");
        ValidateLocationStorageArray(location, context, issues, "locationStorages");
        ValidateActiveThreatArray(location, context, issues, "activeThreats");
    }


    private void ValidateNewLocationObject(JsonElement location, string context, List<ValidationIssue> issues)
    {
        ValidateLocationObject(location, context, issues);

        if (!location.TryGetProperty("locationId", out var locationId) || locationId.ValueKind != JsonValueKind.Null)
        {
            issues.Add(new ValidationIssue(
                $"{context}.locationId",
                IssueSeverity.Error,
                "newLocations entry должен явно передавать locationId = null",
                code: "world_map_new_location_requires_null_location_id",
                section: "WorldMap",
                expected: "locationId = null for a new off-screen location",
                actual: location.TryGetProperty("locationId", out var actualLocationId) ? actualLocationId.ValueKind.ToString() : "missing",
                repairHint: "Для создания новой off-screen location передай полный Location Object с locationId = null; permanent GUID назначит система."));
        }

        if (!HasNonEmptyString(location, "description"))
        {
            issues.Add(new ValidationIssue(
                $"{context}.description",
                IssueSeverity.Error,
                "newLocations entry должен содержать полное description",
                code: "world_map_new_location_missing_description",
                section: "WorldMap",
                repairHint: "Для новой off-screen location передай полный description, а не skeletal location stub."));
        }
    }


    private void ValidateLocationCoordinatesObject(
        JsonElement coordinates,
        string context,
        List<ValidationIssue> issues,
        bool allowLegacyMissingZ,
        string section)
    {
        if (!RequireObject(coordinates, context, issues))
            return;

        ValidateIntegerField(coordinates, context, issues, "x");
        ValidateIntegerField(coordinates, context, issues, "y");

        if (coordinates.TryGetProperty("z", out _))
        {
            ValidateIntegerField(coordinates, context, issues, "z");
            return;
        }

        if (allowLegacyMissingZ)
            return;

        issues.Add(new ValidationIssue(
            $"{context}.z",
            IssueSeverity.Error,
            "coordinates должен содержать обязательный z",
            code: "location_coordinates_missing_z",
            section: section,
            expected: "integer z coordinate",
            actual: "missing",
            repairHint: "Передай coordinates как { x, y, z }. Legacy x/y-only format временно допускается только для уже существующих старых локаций без Z-координаты."));
    }


    private void ValidateLocationUpdateArray(JsonElement arr, string context, List<ValidationIssue> issues)
    {
        RequireArrayOfObjects(arr, context, issues);
        if (arr.ValueKind != JsonValueKind.Array)
            return;

        var index = 0;
        foreach (var item in arr.EnumerateArray())
        {
            var itemContext = $"{context}[{index++}]";
            if (!RequireObject(item, itemContext, issues))
                continue;

            RequireString(item, itemContext, issues, "locationId");
            var hasAnyUpdateField =
                HasAnyNonEmptyString(item, "newName", "newDescription", "newLastEventsDescription", "newImagePrompt") ||
                item.TryGetProperty("coordinates", out _) ||
                item.TryGetProperty("newInternalDifficultyProfile", out _) ||
                item.TryGetProperty("newExternalDifficultyProfile", out _) ||
                item.TryGetProperty("factionControl", out _);
            if (!hasAnyUpdateField)
            {
                issues.Add(new ValidationIssue(
                    itemContext,
                    IssueSeverity.Error,
                    "locationUpdates partial update должен содержать хотя бы одно реально изменяемое поле",
                    code: "location_update_missing_changes",
                    section: "Location",
                    expected: "locationId plus at least one changed field (new*, coordinates, or factionControl)",
                    actual: "locationId only",
                    repairHint: "Для locationUpdates передай locationId и хотя бы одно изменяемое поле: newName/newDescription/newLastEventsDescription/newImagePrompt, coordinates, difficulty profile или factionControl. Не отправляй пустой no-op update только с locationId."));
            }

            ValidateOptionalString(item, itemContext, issues, "newName");
            ValidateOptionalString(item, itemContext, issues, "newDescription");
            ValidateOptionalString(item, itemContext, issues, "newLastEventsDescription");
            ValidateOptionalString(item, itemContext, issues, "newImagePrompt");
            if (item.TryGetProperty("coordinates", out var updatedCoordinates))
                ValidateLocationCoordinatesObject(updatedCoordinates, $"{itemContext}.coordinates", issues, allowLegacyMissingZ: false, section: "Location");
            if (item.TryGetProperty("factionControl", out _))
                ValidateFactionControlArray(item, itemContext, issues, "factionControl");

            var newLastEventsDescription = GetFirstNonEmptyString(item, "newLastEventsDescription");
            if (!string.IsNullOrWhiteSpace(newLastEventsDescription) &&
                !MatchesHistoricalEntryContract(newLastEventsDescription))
            {
                issues.Add(new ValidationIssue(
                    $"{itemContext}.newLastEventsDescription",
                    IssueSeverity.Error,
                    "newLastEventsDescription должен использовать canonical historical-entry timestamp format",
                    code: "location_update_last_events_prefix_invalid",
                    section: "Location",
                    expected: "#[Turn] - [Day] [Month] [Year] г., [HH:MM]: ...",
                    actual: newLastEventsDescription,
                    repairHint: "Начинай newLastEventsDescription с полного timestamp-префикса из Block 18.A. Из-за старых примеров также допустим legacy формат '#[turn_number]. ...', но не произвольная строка без turn anchor."));
            }

            if (item.TryGetProperty("newInternalDifficultyProfile", out var newInternal) &&
                RequireObject(newInternal, $"{itemContext}.newInternalDifficultyProfile", issues))
            {
                foreach (var scale in new[] { "combat", "environment", "social", "exploration" })
                    ValidateNonNegativeIntegerField(newInternal, $"{itemContext}.newInternalDifficultyProfile", issues, scale, "Location");
            }

            if (item.TryGetProperty("newExternalDifficultyProfile", out var newExternal) &&
                RequireObject(newExternal, $"{itemContext}.newExternalDifficultyProfile", issues))
            {
                foreach (var scale in new[] { "combat", "environment", "social", "exploration" })
                    ValidateNonNegativeIntegerField(newExternal, $"{itemContext}.newExternalDifficultyProfile", issues, scale, "Location");
            }

            if (item.TryGetProperty("eventDescriptions", out _))
            {
                issues.Add(new ValidationIssue(
                    $"{itemContext}.eventDescriptions",
                    IssueSeverity.Error,
                    "locationUpdates не должен пересылать eventDescriptions; это read-only исторический архив",
                    code: "location_update_event_descriptions_forbidden",
                    section: "Location",
                    repairHint: "Для off-screen обновлений локации используй newLastEventsDescription, а не eventDescriptions."));
            }
        }
    }


    private void ValidateWorldMapNewLinks(JsonElement arr, string context, List<ValidationIssue> issues)
    {
        RequireArrayOfObjects(arr, context, issues);
        if (arr.ValueKind != JsonValueKind.Array)
            return;

        var index = 0;
        foreach (var item in arr.EnumerateArray())
        {
            var itemContext = $"{context}[{index++}]";
            if (!RequireObject(item, itemContext, issues))
                continue;

            var missingFields = GetMissingRequiredNonEmptyStringProperties(item, "sourceLocationId");
            if (missingFields.Count > 0 || !item.TryGetProperty("link", out _))
            {
                var actualMissing = new List<string>(missingFields);
                if (!item.TryGetProperty("link", out _))
                    actualMissing.Add("link");

                issues.Add(new ValidationIssue(
                    itemContext,
                    IssueSeverity.Error,
                    "worldMapUpdates.newLinks entry не содержит обязательные корневые поля",
                    code: "world_map_new_link_missing_required_fields",
                    section: "WorldMap",
                    expected: "sourceLocationId + link adjacency entry object",
                    actual: string.Join(", ", actualMissing),
                    repairHint: "Для dynamic path discovery передай sourceLocationId локации-источника и nested link с полным adjacency entry object."));
                continue;
            }

            RequireString(item, itemContext, issues, "sourceLocationId");
            if (!item.TryGetProperty("link", out var link) || !RequireObject(link, $"{itemContext}.link", issues))
                continue;

            ValidateWorldMapAdjacencyLinkObject(link, $"{itemContext}.link", issues);
        }
    }


    private void ValidateWorldMapAdjacencyLinkObject(JsonElement link, string context, List<ValidationIssue> issues)
    {
        RequireString(link, context, issues, "name");
        RequireString(link, context, issues, "shortDescription");
        RequireString(link, context, issues, "linkType");
        RequireString(link, context, issues, "linkState");

        if (!link.TryGetProperty("targetCoordinates", out var targetCoordinates) ||
            !RequireObject(targetCoordinates, $"{context}.targetCoordinates", issues))
        {
            issues.Add(new ValidationIssue(
                $"{context}.targetCoordinates",
                IssueSeverity.Error,
                "Adjacency link должен содержать targetCoordinates object",
                code: "world_map_link_missing_target_coordinates",
                section: "WorldMap",
                expected: "targetCoordinates object with x/y and optional z",
                actual: link.TryGetProperty("targetCoordinates", out var actualTargetCoordinates)
                    ? actualTargetCoordinates.ValueKind.ToString()
                    : "missing",
                repairHint: "Для adjacency link всегда передавай targetCoordinates object с canonical integer x/y и optional z."));
        }
        else
        {
            ValidateIntegerField(targetCoordinates, $"{context}.targetCoordinates", issues, "x");
            ValidateIntegerField(targetCoordinates, $"{context}.targetCoordinates", issues, "y");
            if (targetCoordinates.TryGetProperty("z", out _))
                ValidateIntegerField(targetCoordinates, $"{context}.targetCoordinates", issues, "z");
        }

        foreach (var propName in new[] { "estimatedInternalDifficultyProfile", "estimatedExternalDifficultyProfile" })
        {
            if (!link.TryGetProperty(propName, out var profile) ||
                !RequireObject(profile, $"{context}.{propName}", issues))
            {
                issues.Add(new ValidationIssue(
                    $"{context}.{propName}",
                    IssueSeverity.Error,
                    $"{propName} обязателен для adjacency link preview",
                    code: "world_map_link_preview_missing_difficulty_profile",
                    section: "WorldMap",
                    expected: $"{propName} object with combat/environment/social/exploration integer scales",
                    actual: "missing or non-object",
                    repairHint: $"Добавь в adjacency link preview объект {propName} с integer-полями combat, environment, social и exploration."));
                continue;
            }

            foreach (var scale in new[] { "combat", "environment", "social", "exploration" })
                ValidateNonNegativeIntegerField(profile, $"{context}.{propName}", issues, scale, "WorldMap");
        }
    }


    private static HashSet<string> GetExpectedNpcAttitudeValues(int relationshipLevel)
    {
        return relationshipLevel switch
        {
            <= -201 => new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                "Непримиримый Враг",
                "Implacable Foe"
            },
            <= -51 => new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                "Противник",
                "Adversary"
            },
            <= -1 => new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                "Неприязнь",
                "Dislike"
            },
            <= 100 => new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                "Нейтралитет",
                "Neutral"
            },
            <= 250 => new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                "Доверие и Расположение",
                "Familiarity & Trust"
            },
            <= 350 => new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                "Глубокая Связь",
                "Deep Bond"
            },
            _ => new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                "Легендарная Преданность",
                "Legendary Bond"
            }
        };
    }


    private void ValidateCombatantArray(JsonElement root, string contextPrefix, List<ValidationIssue> issues, string propName)
    {
        if (!TryGetArray(root, propName, $"{contextPrefix}.{propName}", issues, out var arr))
            return;

        var index = 0;
        foreach (var item in arr.EnumerateArray())
        {
            var itemContext = $"{contextPrefix}.{propName}[{index++}]";
            if (!RequireObject(item, itemContext, issues)) continue;

            if (!HasAnyNonEmptyString(item, "name", "enemyName", "allyName"))
            {
                issues.Add(new ValidationIssue(itemContext, IssueSeverity.Error,
                    "Боевой объект должен содержать name/enemyName/allyName"));
            }

            ValidateRequiredNullableStringField(item, itemContext, issues, "NPCId");
            RequireString(item, itemContext, issues, "image_prompt");
            RequireString(item, itemContext, issues, "description");
            RequireString(item, itemContext, issues, "type");
            RequireBooleanField(item, itemContext, issues, "isGroup");
            ValidatePercentageStringField(item, itemContext, issues, "maxHealth", requirePositive: true);
            RequireString(item, itemContext, issues, "maxPoise");
            ValidateRequiredNullableStringField(item, itemContext, issues, "currentHealth");
            ValidateRequiredNullableStringField(item, itemContext, issues, "currentPoise");
            RequireObjectArrayField(item, itemContext, issues, "actions");
            RequireObjectArrayField(item, itemContext, issues, "resistances");
            RequireObjectArrayField(item, itemContext, issues, "activeBuffs");
            RequireObjectArrayField(item, itemContext, issues, "activeDebuffs");

            if (item.TryGetProperty("actions", out var actions))
                ValidateCombatActionArray(actions, $"{itemContext}.actions", issues, section: "Combat");
            if (item.TryGetProperty("resistances", out var resistances))
                ValidateCombatResistanceArray(resistances, $"{itemContext}.resistances", issues);
            if (item.TryGetProperty("activeBuffs", out var activeBuffs))
                ValidateCombatantActiveEffectArray(activeBuffs, $"{itemContext}.activeBuffs", issues);
            if (item.TryGetProperty("activeDebuffs", out var activeDebuffs))
                ValidateCombatantActiveEffectArray(activeDebuffs, $"{itemContext}.activeDebuffs", issues);

            if (item.TryGetProperty("currentHealth", out var currentHealthNode) &&
                currentHealthNode.ValueKind == JsonValueKind.String)
            {
                ValidatePercentageStringField(item, itemContext, issues, "currentHealth", requirePositive: false);
            }

            if (item.TryGetProperty("maxPoise", out var maxPoiseNode) &&
                maxPoiseNode.ValueKind == JsonValueKind.String)
            {
                ValidatePercentageStringField(item, itemContext, issues, "maxPoise", requirePositive: true);
            }

            if (item.TryGetProperty("currentPoise", out var currentPoiseNode) &&
                currentPoiseNode.ValueKind == JsonValueKind.String)
            {
                ValidatePercentageStringField(item, itemContext, issues, "currentPoise", requirePositive: false);
            }

            if (item.TryGetProperty("isGroup", out var isGroup) &&
                isGroup.ValueKind is JsonValueKind.True)
            {
                ValidatePositiveIntegerField(item, itemContext, issues, "count");
                RequireString(item, itemContext, issues, "unitName");
                ValidateRequiredNullableStringArrayField(item, itemContext, issues, "healthStates");
                if (!item.TryGetProperty("healthStates", out var healthStates) || healthStates.ValueKind == JsonValueKind.Null)
                {
                    issues.Add(new ValidationIssue(
                        $"{itemContext}.healthStates",
                        IssueSeverity.Error,
                        "Group combatant обязан содержать массив healthStates",
                        code: "combat_group_missing_health_states",
                        section: "Combat",
                        repairHint: "Для isGroup=true передай healthStates[] с состоянием каждого юнита; null недопустим."));
                }
                else if (healthStates.ValueKind == JsonValueKind.Array)
                {
                    ValidatePercentageStringArrayValues(healthStates, $"{itemContext}.healthStates", issues);
                    if (item.TryGetProperty("count", out var countNode) &&
                        countNode.ValueKind == JsonValueKind.Number &&
                        countNode.TryGetInt32(out var count) &&
                        count > 0 &&
                        healthStates.GetArrayLength() != count)
                    {
                        issues.Add(new ValidationIssue(
                            $"{itemContext}.healthStates",
                            IssueSeverity.Error,
                            "Group combatant.healthStates должен содержать ровно count percentage entries",
                            code: "combat_group_health_states_count_mismatch",
                            section: "Combat",
                            expected: count.ToString(),
                            actual: healthStates.GetArrayLength().ToString(),
                            repairHint: "Для isGroup=true заполни healthStates[] состоянием каждого юнита; длина массива должна совпадать с count."));
                    }
                }
                if (item.TryGetProperty("currentHealth", out var currentHealth) && currentHealth.ValueKind != JsonValueKind.Null)
                {
                    issues.Add(new ValidationIssue(
                        $"{itemContext}.currentHealth",
                        IssueSeverity.Error,
                        "Group combatant должен передавать currentHealth = null",
                        code: "combat_group_current_health_must_be_null",
                        section: "Combat",
                        repairHint: "Для isGroup=true используй healthStates[] и оставляй currentHealth = null."));
                }

                if (item.TryGetProperty("currentPoise", out var currentPoise) && currentPoise.ValueKind != JsonValueKind.Null)
                {
                    issues.Add(new ValidationIssue(
                        $"{itemContext}.currentPoise",
                        IssueSeverity.Error,
                        "Group combatant должен передавать currentPoise = null",
                        code: "combat_group_current_poise_must_be_null",
                        section: "Combat",
                        repairHint: "Для isGroup=true оставляй currentPoise = null и описывай состояние группы через unitName/healthStates/actions."));
                }
            }
            else if (item.TryGetProperty("currentHealth", out var currentHealth) && currentHealth.ValueKind == JsonValueKind.Null)
            {
                issues.Add(new ValidationIssue(
                    $"{itemContext}.currentHealth",
                    IssueSeverity.Error,
                    "Individual combatant не должен передавать currentHealth = null",
                    code: "combat_individual_current_health_null",
                    section: "Combat",
                    repairHint: "Для одиночного combatant передай currentHealth как percentage string; null допустим только для групп."));
            }
            else if (item.TryGetProperty("currentPoise", out var currentPoise) && currentPoise.ValueKind == JsonValueKind.Null)
            {
                issues.Add(new ValidationIssue(
                    $"{itemContext}.currentPoise",
                    IssueSeverity.Error,
                    "Individual combatant не должен передавать currentPoise = null",
                    code: "combat_individual_current_poise_null",
                    section: "Combat",
                    repairHint: "Для одиночного combatant передай currentPoise как строку текущей стойкости; null допустим только для групп."));
            }
        }
    }


    private void ValidateCombatLog(JsonElement root, string contextPrefix, List<ValidationIssue> issues)
    {
        if (!root.TryGetProperty("combat_log_markdown", out var value))
            return;

        if (value.ValueKind != JsonValueKind.String)
        {
            issues.Add(new ValidationIssue($"{contextPrefix}.combat_log_markdown", IssueSeverity.Error,
                "combat_log_markdown должен быть строкой"));
        }
    }


    private void RequireObjectArrayField(JsonElement root, string contextPrefix, List<ValidationIssue> issues, string propName)
    {
        if (!root.TryGetProperty(propName, out var value))
        {
            issues.Add(new ValidationIssue(
                $"{contextPrefix}.{propName}",
                IssueSeverity.Error,
                $"Отсутствует обязательное поле: {propName}",
                code: "missing_required_object_array_field",
                expected: $"{propName} array<object>",
                actual: "missing",
                repairHint: $"Добавь обязательный массив объектов {propName} в canonical contract."));
            return;
        }

        RequireArrayOfObjects(value, $"{contextPrefix}.{propName}", issues);
    }


    private void ValidateLocationStorageUpdates(JsonElement arr, string context, List<ValidationIssue> issues)
    {
        RequireArrayOfObjects(arr, context, issues);
        if (arr.ValueKind != JsonValueKind.Array)
            return;

        var index = 0;
        foreach (var item in arr.EnumerateArray())
        {
            var itemContext = $"{context}[{index++}]";
            if (!RequireObject(item, itemContext, issues))
                continue;

            RequireString(item, itemContext, issues, "targetLocationId");
            RequireString(item, itemContext, issues, "storageId");
            if (!item.TryGetProperty("update", out var update))
                issues.Add(new ValidationIssue(
                    $"{itemContext}.update",
                    IssueSeverity.Error,
                    "storageUpdates item должен содержать update",
                    code: "world_map_storage_update_missing_payload",
                    section: "WorldMap",
                    expected: "update object with changed storage fields",
                    actual: "missing",
                    repairHint: "Для storageUpdates передавай targetLocationId, storageId и nested update object только с реально изменившимися полями storage."));
            else if (!RequireObject(update, $"{itemContext}.update", issues))
                issues.Add(new ValidationIssue(
                    $"{itemContext}.update",
                    IssueSeverity.Error,
                    "storageUpdates item должен содержать update object",
                    code: "world_map_storage_update_invalid_payload_shape",
                    section: "WorldMap",
                    expected: "update object with changed storage fields",
                    actual: update.ValueKind.ToString(),
                    repairHint: "Для storageUpdates передавай targetLocationId, storageId и nested update object только с реально изменившимися полями storage."));
        }
    }


    private void ValidateLocationStorageRemovals(JsonElement arr, string context, List<ValidationIssue> issues)
    {
        RequireArrayOfObjects(arr, context, issues);
        if (arr.ValueKind != JsonValueKind.Array)
            return;

        var index = 0;
        foreach (var item in arr.EnumerateArray())
        {
            var itemContext = $"{context}[{index++}]";
            if (!RequireObject(item, itemContext, issues))
                continue;
            RequireString(item, itemContext, issues, "targetLocationId");
            RequireString(item, itemContext, issues, "storageId");
        }
    }


    private void ValidateLocationLinkUpdates(JsonElement arr, string context, List<ValidationIssue> issues)
    {
        RequireArrayOfObjects(arr, context, issues);
        if (arr.ValueKind != JsonValueKind.Array)
            return;

        var index = 0;
        foreach (var item in arr.EnumerateArray())
        {
            var itemContext = $"{context}[{index++}]";
            if (!RequireObject(item, itemContext, issues))
                continue;

            RequireString(item, itemContext, issues, "sourceLocationId");
            RequireObjectProperty(item, itemContext, issues, "targetCoordinates");
            if (item.TryGetProperty("targetCoordinates", out var targetCoordinates) &&
                RequireObject(targetCoordinates, $"{itemContext}.targetCoordinates", issues))
            {
                ValidateIntegerField(targetCoordinates, $"{itemContext}.targetCoordinates", issues, "x");
                ValidateIntegerField(targetCoordinates, $"{itemContext}.targetCoordinates", issues, "y");
                if (targetCoordinates.TryGetProperty("z", out _))
                    ValidateIntegerField(targetCoordinates, $"{itemContext}.targetCoordinates", issues, "z");
            }

            if (!item.TryGetProperty("updatedLink", out var updatedLink))
            {
                issues.Add(new ValidationIssue(
                    $"{itemContext}.updatedLink",
                    IssueSeverity.Error,
                    "linkUpdates item должен содержать обязательный updatedLink object",
                    code: "world_map_link_update_missing_payload",
                    section: "WorldMap",
                    expected: "updatedLink object with at least one changed field",
                    actual: "missing",
                    repairHint: "Для linkUpdates передай updatedLink object с хотя бы одним реально изменённым полем ссылки: newName, newShortDescription и/или newLinkState."));
            }
            else if (!RequireObject(updatedLink, $"{itemContext}.updatedLink", issues))
            {
                issues.Add(new ValidationIssue(
                    $"{itemContext}.updatedLink",
                    IssueSeverity.Error,
                    "linkUpdates item должен содержать updatedLink object",
                    code: "world_map_link_update_invalid_payload_shape",
                    section: "WorldMap",
                    expected: "updatedLink object with at least one changed field",
                    actual: updatedLink.ValueKind.ToString(),
                    repairHint: "Для linkUpdates передай updatedLink object с хотя бы одним реально изменённым полем ссылки: newName, newShortDescription и/или newLinkState."));
            }
            else
            {
                var updatedLinkContext = $"{itemContext}.updatedLink";
                var visibleProps = updatedLink.EnumerateObject()
                    .Where(prop => !prop.Name.StartsWith("_", StringComparison.OrdinalIgnoreCase))
                    .ToList();

                if (visibleProps.Count == 0)
                {
                    issues.Add(new ValidationIssue(
                        updatedLinkContext,
                        IssueSeverity.Error,
                        "updatedLink должен содержать хотя бы одно реально изменённое поле",
                        code: "world_map_link_update_missing_changes",
                        section: "WorldMap",
                        repairHint: "Передай в updatedLink только реально изменившиеся поля ссылки: newName, newShortDescription и/или newLinkState."));
                }
                else
                {
                    foreach (var prop in visibleProps)
                    {
                        switch (prop.Name)
                        {
                            case "newName":
                            case "newShortDescription":
                            case "newLinkState":
                                if (prop.Value.ValueKind != JsonValueKind.String || string.IsNullOrWhiteSpace(prop.Value.GetString()))
                                {
                                    issues.Add(new ValidationIssue(
                                        $"{updatedLinkContext}.{prop.Name}",
                                        IssueSeverity.Error,
                                        $"{prop.Name} должен быть непустой строкой",
                                        code: "world_map_link_update_invalid_field_value",
                                        section: "WorldMap",
                                        expected: "non-empty string",
                                        actual: prop.Value.ValueKind == JsonValueKind.String ? "empty string" : prop.Value.ValueKind.ToString(),
                                        repairHint: "Для updatedLink используй только непустые строковые значения в newName, newShortDescription и newLinkState."));
                                }
                                break;
                            default:
                                issues.Add(new ValidationIssue(
                                    $"{updatedLinkContext}.{prop.Name}",
                                    IssueSeverity.Error,
                                    "updatedLink содержит неподдерживаемое поле partial update",
                                    code: "world_map_link_update_unknown_field",
                                    section: "WorldMap",
                                    expected: "newName | newShortDescription | newLinkState",
                                    actual: prop.Name,
                                    repairHint: "Для linkUpdates используй только documented partial fields ссылки: newName, newShortDescription, newLinkState."));
                                break;
                        }
                    }
                }
            }
        }
    }


    private void ValidateLocationLinkRemovals(JsonElement arr, string context, List<ValidationIssue> issues)
    {
        RequireArrayOfObjects(arr, context, issues);
        if (arr.ValueKind != JsonValueKind.Array)
            return;

        var index = 0;
        foreach (var item in arr.EnumerateArray())
        {
            var itemContext = $"{context}[{index++}]";
            if (!RequireObject(item, itemContext, issues))
                continue;

            RequireString(item, itemContext, issues, "sourceLocationId");
            RequireObjectProperty(item, itemContext, issues, "targetCoordinates");
            if (item.TryGetProperty("targetCoordinates", out var targetCoordinates) &&
                RequireObject(targetCoordinates, $"{itemContext}.targetCoordinates", issues))
            {
                ValidateIntegerField(targetCoordinates, $"{itemContext}.targetCoordinates", issues, "x");
                ValidateIntegerField(targetCoordinates, $"{itemContext}.targetCoordinates", issues, "y");
                if (targetCoordinates.TryGetProperty("z", out _))
                    ValidateIntegerField(targetCoordinates, $"{itemContext}.targetCoordinates", issues, "z");
            }
        }
    }


    private void ValidateLocationThreatAdds(JsonElement arr, string context, List<ValidationIssue> issues)
    {
        RequireArrayOfObjects(arr, context, issues);
        if (arr.ValueKind != JsonValueKind.Array)
            return;

        var index = 0;
        foreach (var item in arr.EnumerateArray())
        {
            var itemContext = $"{context}[{index++}]";
            if (!RequireObject(item, itemContext, issues))
                continue;

            if (!item.TryGetProperty("targetLocationId", out var targetLocationIdNode))
            {
                issues.Add(new ValidationIssue(
                    $"{itemContext}.targetLocationId",
                    IssueSeverity.Error,
                    "threatsToAdd должен явно задавать targetLocationId",
                    code: "world_map_threat_add_missing_target_location_id",
                    section: "WorldMap",
                    expected: "targetLocationId as string for existing location or null for same-turn new location",
                    actual: "missing",
                    repairHint: "Для threatsToAdd всегда передавай targetLocationId. Для existing location используй её permanent id, а для same-turn new location укажи targetLocationId = null и exact initialTargetLocationId."));
            }
            else
            {
                ValidateRequiredNullableStringField(item, itemContext, issues, "targetLocationId");
            }

            if (item.TryGetProperty("initialTargetLocationId", out _))
                ValidateOptionalNullableStringField(item, itemContext, issues, "initialTargetLocationId");

            var targetLocationId = targetLocationIdNode.ValueKind == JsonValueKind.String
                ? targetLocationIdNode.GetString()
                : null;
            var initialTargetLocationId = GetFirstNonEmptyString(item, "initialTargetLocationId");
            if (targetLocationIdNode.ValueKind == JsonValueKind.Null)
            {
                if (string.IsNullOrWhiteSpace(initialTargetLocationId))
                {
                    issues.Add(new ValidationIssue(
                        $"{itemContext}.initialTargetLocationId",
                        IssueSeverity.Error,
                        "threatsToAdd для same-turn новой локации требует initialTargetLocationId",
                        code: "world_map_threat_add_missing_same_turn_initial_target",
                        section: "WorldMap",
                        expected: "exact initialTargetLocationId of the same-turn new location",
                        actual: "missing or empty",
                        repairHint: "Если угроза создаётся в same-turn новой локации, оставь targetLocationId = null и передай exact initialTargetLocationId этой локации."));
                }
            }
            else if (!string.IsNullOrWhiteSpace(targetLocationId) && !string.IsNullOrWhiteSpace(initialTargetLocationId))
            {
                issues.Add(new ValidationIssue(
                    $"{itemContext}.initialTargetLocationId",
                    IssueSeverity.Error,
                    "threatsToAdd не должен смешивать targetLocationId existing локации с initialTargetLocationId same-turn новой локации",
                    code: "world_map_threat_add_mixed_targeting_modes",
                    section: "WorldMap",
                    expected: "either targetLocationId for existing location, or targetLocationId=null + initialTargetLocationId for same-turn new location",
                    actual: $"targetLocationId={targetLocationId}, initialTargetLocationId={initialTargetLocationId}",
                    repairHint: "Для existing location оставь только targetLocationId. Для same-turn new location укажи targetLocationId = null и используй только initialTargetLocationId."));
            }

            if (item.TryGetProperty("threat", out var threat) &&
                RequireObject(threat, $"{itemContext}.threat", issues))
            {
                ValidateActiveThreatObject(threat, $"{itemContext}.threat", issues, requireNullThreatId: true);
            }
            else
            {
                issues.Add(new ValidationIssue($"{itemContext}.threat", IssueSeverity.Error,
                    "threatsToAdd item должен содержать threat object"));
            }
        }
    }


    private void ValidateActiveThreatObject(JsonElement threat, string context, List<ValidationIssue> issues, bool requireNullThreatId)
    {
        if (!threat.TryGetProperty("threatId", out var threatIdNode))
        {
            issues.Add(new ValidationIssue(
                $"{context}.threatId",
                IssueSeverity.Error,
                "Active Threat Object должен содержать threatId",
                code: requireNullThreatId ? "world_map_new_threat_missing_id" : "world_map_active_threat_missing_id",
                section: "WorldMap",
                expected: requireNullThreatId ? "threatId = null for a new threat" : "string threatId or null for a newly introduced canonical threat",
                actual: "missing",
                repairHint: requireNullThreatId
                    ? "Для threatsToAdd передай complete Active Threat Object и установи threatId = null."
                    : "В canonical activeThreats всегда сохраняй threatId. Для угрозы, созданной в этом же ходе, допустим null до системной нормализации."));
        }
        else if (requireNullThreatId)
        {
            if (threatIdNode.ValueKind != JsonValueKind.Null)
            {
                issues.Add(new ValidationIssue(
                    $"{context}.threatId",
                    IssueSeverity.Error,
                    "Новая угроза в threatsToAdd должна иметь threatId = null",
                    code: "world_map_new_threat_non_null_id_forbidden",
                    section: "WorldMap",
                    expected: "null",
                    actual: threatIdNode.ValueKind == JsonValueKind.String ? threatIdNode.GetString() ?? string.Empty : threatIdNode.ValueKind.ToString(),
                    repairHint: "Для brand-new threatsToAdd оставляй threatId = null. Система назначит permanent id после принятия хода."));
            }
        }
        else if (threatIdNode.ValueKind != JsonValueKind.Null &&
                 (threatIdNode.ValueKind != JsonValueKind.String || string.IsNullOrWhiteSpace(threatIdNode.GetString())))
        {
            issues.Add(new ValidationIssue(
                $"{context}.threatId",
                IssueSeverity.Error,
                "Active Threat Object threatId должен быть непустой строкой или null",
                code: "world_map_active_threat_invalid_id",
                section: "WorldMap",
                expected: "non-empty string threatId or null",
                actual: threatIdNode.ValueKind.ToString(),
                repairHint: "Сохраняй threatId как непустую строку. Null допустим только для brand-new threat до системной нормализации."));
        }

        RequireString(threat, context, issues, "name");
        ValidateOptionalString(threat, context, issues, "description");
        ValidateNonNegativeIntegerField(threat, context, issues, "intensity", "WorldMap");
        RequireString(threat, context, issues, "longTermGoal");

        if (!threat.TryGetProperty("currentActivity", out var currentActivity))
        {
            issues.Add(new ValidationIssue(
                $"{context}.currentActivity",
                IssueSeverity.Error,
                "Active Threat Object должен содержать currentActivity object или null",
                code: "world_map_active_threat_missing_current_activity",
                section: "WorldMap",
                expected: "currentActivity object or null",
                actual: "missing",
                repairHint: "Передай в Active Threat Object currentActivity как объект текущего шага или null для idle threat."));
        }
        else if (currentActivity.ValueKind != JsonValueKind.Null)
        {
            ValidateNpcCurrentActivityObject(currentActivity, $"{context}.currentActivity", issues);
        }

        if (!threat.TryGetProperty("threatArchetype", out var threatArchetype) ||
            !RequireObject(threatArchetype, $"{context}.threatArchetype", issues))
        {
            issues.Add(new ValidationIssue(
                $"{context}.threatArchetype",
                IssueSeverity.Error,
                "Active Threat Object должен содержать threatArchetype",
                code: "world_map_active_threat_missing_archetype",
                section: "WorldMap",
                expected: "threatArchetype object with motivation and method",
                actual: "missing or invalid",
                repairHint: "Передай в Active Threat Object threatArchetype с canonical motivation/method и custom* полями при необходимости."));
        }
        else
        {
            var motivation = RequireString(threatArchetype, $"{context}.threatArchetype", issues, "motivation");
            var method = RequireString(threatArchetype, $"{context}.threatArchetype", issues, "method");
            if (!string.IsNullOrWhiteSpace(motivation) && !AllowedThreatMotivations.Contains(motivation))
            {
                issues.Add(new ValidationIssue(
                    $"{context}.threatArchetype.motivation",
                    IssueSeverity.Error,
                    "threatArchetype.motivation должен быть одним из canonical enum значений",
                    code: "world_map_threat_invalid_motivation",
                    section: "WorldMap",
                    expected: string.Join(" | ", AllowedThreatMotivations),
                    actual: motivation,
                    repairHint: "Используй в threatArchetype.motivation только canonical значения из Block 20 или Custom с заполненным customMotivation."));
            }

            if (!string.IsNullOrWhiteSpace(method) && !AllowedThreatMethods.Contains(method))
            {
                issues.Add(new ValidationIssue(
                    $"{context}.threatArchetype.method",
                    IssueSeverity.Error,
                    "threatArchetype.method должен быть одним из canonical enum значений",
                    code: "world_map_threat_invalid_method",
                    section: "WorldMap",
                    expected: string.Join(" | ", AllowedThreatMethods),
                    actual: method,
                    repairHint: "Используй в threatArchetype.method только canonical значения из Block 20 или Custom с заполненным customMethod."));
            }

            if (string.Equals(motivation, "Custom", StringComparison.OrdinalIgnoreCase))
                RequireString(threatArchetype, $"{context}.threatArchetype", issues, "customMotivation");
            else if (threatArchetype.TryGetProperty("customMotivation", out _))
                ValidateOptionalNullableStringField(threatArchetype, $"{context}.threatArchetype", issues, "customMotivation");

            if (string.Equals(method, "Custom", StringComparison.OrdinalIgnoreCase))
                RequireString(threatArchetype, $"{context}.threatArchetype", issues, "customMethod");
            else if (threatArchetype.TryGetProperty("customMethod", out _))
                ValidateOptionalNullableStringField(threatArchetype, $"{context}.threatArchetype", issues, "customMethod");
        }

        if (!threat.TryGetProperty("impactProfile", out var impactProfile) ||
            !RequireObject(impactProfile, $"{context}.impactProfile", issues))
        {
            issues.Add(new ValidationIssue(
                $"{context}.impactProfile",
                IssueSeverity.Error,
                "Active Threat Object должен содержать impactProfile",
                code: "world_map_active_threat_missing_impact_profile",
                section: "WorldMap",
                expected: "impactProfile object with target type/id/name and primary impact",
                actual: "missing or invalid",
                repairHint: "Передай в Active Threat Object impactProfile с primaryTargetType, primaryTargetId, primaryTargetName, primaryImpact и baseImpactValue."));
        }
        else
        {
            var primaryTargetType = RequireString(impactProfile, $"{context}.impactProfile", issues, "primaryTargetType");
            if (!string.IsNullOrWhiteSpace(primaryTargetType) && !AllowedThreatPrimaryTargetTypes.Contains(primaryTargetType))
            {
                issues.Add(new ValidationIssue(
                    $"{context}.impactProfile.primaryTargetType",
                    IssueSeverity.Error,
                    "impactProfile.primaryTargetType должен быть одним из canonical enum значений",
                    code: "world_map_threat_invalid_primary_target_type",
                    section: "WorldMap",
                    expected: string.Join(" | ", AllowedThreatPrimaryTargetTypes),
                    actual: primaryTargetType,
                    repairHint: "Используй в impactProfile.primaryTargetType только Faction, Location или Resource."));
            }

            ValidateRequiredNullableStringField(impactProfile, $"{context}.impactProfile", issues, "primaryTargetId");
            RequireString(impactProfile, $"{context}.impactProfile", issues, "primaryTargetName");
            var primaryImpact = RequireString(impactProfile, $"{context}.impactProfile", issues, "primaryImpact");
            if (!string.IsNullOrWhiteSpace(primaryImpact) && !AllowedThreatPrimaryImpacts.Contains(primaryImpact))
            {
                issues.Add(new ValidationIssue(
                    $"{context}.impactProfile.primaryImpact",
                    IssueSeverity.Error,
                    "impactProfile.primaryImpact должен быть одним из canonical enum значений",
                    code: "world_map_threat_invalid_primary_impact",
                    section: "WorldMap",
                    expected: string.Join(" | ", AllowedThreatPrimaryImpacts),
                    actual: primaryImpact,
                    repairHint: "Используй в impactProfile.primaryImpact только Military, Economic, Social, Covert, Stability или Environment."));
            }

            ValidateIntegerField(impactProfile, $"{context}.impactProfile", issues, "baseImpactValue");
        }
    }


    private void ValidateLocationThreatUpdates(JsonElement arr, string context, List<ValidationIssue> issues)
    {
        RequireArrayOfObjects(arr, context, issues);
        if (arr.ValueKind != JsonValueKind.Array)
            return;

        var index = 0;
        foreach (var item in arr.EnumerateArray())
        {
            var itemContext = $"{context}[{index++}]";
            if (!RequireObject(item, itemContext, issues))
                continue;

            RequireString(item, itemContext, issues, "targetLocationId");
            if (item.TryGetProperty("threatUpdate", out var threatUpdate) &&
                RequireObject(threatUpdate, $"{itemContext}.threatUpdate", issues))
            {
                RequireString(threatUpdate, $"{itemContext}.threatUpdate", issues, "threatId");
                if (threatUpdate.TryGetProperty("currentActivity", out var currentActivity) &&
                    currentActivity.ValueKind == JsonValueKind.Null)
                {
                    issues.Add(new ValidationIssue(
                        $"{itemContext}.threatUpdate.currentActivity",
                        IssueSeverity.Error,
                        "threatsToUpdate не должен обнулять currentActivity через null",
                        code: "world_map_threat_update_null_current_activity_forbidden",
                        section: "WorldMap",
                        expected: "non-null partial currentActivity update or completeThreatActivities command",
                        actual: "null",
                        repairHint: "Если активность угрозы завершена или abandoned, используй completeThreatActivities с finalState. threatsToUpdate оставляй только для non-terminal partial changes."));
                }
                else if (threatUpdate.TryGetProperty("currentActivity", out currentActivity) &&
                         currentActivity.ValueKind == JsonValueKind.Object)
                {
                    var activeState = GetFirstNonEmptyString(currentActivity, "activeState");
                    if (string.Equals(activeState, "Completed", StringComparison.OrdinalIgnoreCase) ||
                        string.Equals(activeState, "Abandoned", StringComparison.OrdinalIgnoreCase))
                    {
                        issues.Add(new ValidationIssue(
                            $"{itemContext}.threatUpdate.currentActivity.activeState",
                            IssueSeverity.Error,
                            "threatsToUpdate не должен завершать активность угрозы через currentActivity.activeState",
                            code: "world_map_threat_update_terminal_activity_state_forbidden",
                            section: "WorldMap",
                            expected: "non-terminal currentActivity patch; terminal completion belongs to completeThreatActivities",
                            actual: activeState,
                            repairHint: "Если активность угрозы завершена или abandoned, не ставь terminal activeState внутри threatsToUpdate. Используй completeThreatActivities с finalState = Completed или Abandoned."));
                    }

                    ValidatePartialThreatActivityUpdateObject(currentActivity, $"{itemContext}.threatUpdate.currentActivity", issues);
                }
            }
            else
            {
                issues.Add(new ValidationIssue(
                    $"{itemContext}.threatUpdate",
                    IssueSeverity.Error,
                    "threatsToUpdate item должен содержать обязательный threatUpdate object",
                    code: "world_map_threat_update_missing_payload",
                    section: "WorldMap",
                    expected: "threatUpdate object with threatId and changed fields",
                    actual: "missing",
                    repairHint: "Для threatsToUpdate передай threatUpdate object с threatId и хотя бы одним реально изменяемым полем угрозы."));
            }
        }
    }


    private void ValidateLocationThreatRemovals(JsonElement arr, string context, List<ValidationIssue> issues)
    {
        RequireArrayOfObjects(arr, context, issues);
        if (arr.ValueKind != JsonValueKind.Array)
            return;

        var index = 0;
        foreach (var item in arr.EnumerateArray())
        {
            var itemContext = $"{context}[{index++}]";
            if (!RequireObject(item, itemContext, issues))
                continue;

            RequireString(item, itemContext, issues, "targetLocationId");
            RequireString(item, itemContext, issues, "threatId");
        }
    }


    private void ValidateLocationThreatCompletions(JsonElement arr, string context, List<ValidationIssue> issues)
    {
        RequireArrayOfObjects(arr, context, issues);
        if (arr.ValueKind != JsonValueKind.Array)
            return;

        var index = 0;
        foreach (var item in arr.EnumerateArray())
        {
            var itemContext = $"{context}[{index++}]";
            if (!RequireObject(item, itemContext, issues))
                continue;

            RequireString(item, itemContext, issues, "targetLocationId");
            RequireString(item, itemContext, issues, "threatId");
            RequireString(item, itemContext, issues, "threatName");
            var finalState = RequireString(item, itemContext, issues, "finalState");
            if (!string.IsNullOrWhiteSpace(finalState) &&
                !string.Equals(finalState, "Completed", StringComparison.OrdinalIgnoreCase) &&
                !string.Equals(finalState, "Abandoned", StringComparison.OrdinalIgnoreCase))
            {
                issues.Add(new ValidationIssue(
                    $"{itemContext}.finalState",
                    IssueSeverity.Error,
                    "completeThreatActivities.finalState должен быть Completed или Abandoned",
                    code: "world_map_threat_completion_invalid_final_state",
                    section: "WorldMap",
                    expected: "Completed | Abandoned",
                    actual: finalState,
                    repairHint: "Для completeThreatActivities используй только finalState = Completed или Abandoned."));
            }
            RequireString(item, itemContext, issues, "narrativeSummary");
        }
    }


	    private void ValidateCanonicalFactionIdentity(JsonElement item, string itemContext, List<ValidationIssue> issues)
	    {
	        var factionId = GetFirstNonEmptyString(item, "factionId");
	        if (string.IsNullOrWhiteSpace(factionId))
	        {
	            issues.Add(new ValidationIssue(
	                itemContext,
	                IssueSeverity.Error,
	                "Каноническое faction state должно содержать permanent factionId",
	                code: "canonical_faction_sidecar_requires_permanent_faction_id",
	                section: "Factions",
	                expected: "existing permanent factionId from faction_core.json",
	                actual: item.TryGetProperty("factionId", out var actualFactionIdNode)
	                    ? actualFactionIdNode.ValueKind.ToString()
	                    : "missing",
	                repairHint: "Sidecar faction-файлы описывают только уже закреплённые фракции. Для новой same-turn фракции не создавай sidecar entry до появления permanent factionId; используй faction_core.json с factionId=null, initialId и isNewFaction=true."));
	        }
	        else
	        {
	            var knownFactionIds = GetKnownCanonicalFactionIds();
	            if (knownFactionIds.Count > 0 && !knownFactionIds.Contains(factionId))
	            {
	                issues.Add(new ValidationIssue(
	                    $"{itemContext}.factionId",
	                    IssueSeverity.Error,
	                    $"Canonical faction sidecar entry ссылается на неизвестный factionId '{factionId}'",
	                    code: "canonical_faction_sidecar_unknown_faction_id",
	                    section: "Factions",
	                    expected: "existing factionId from canonical faction_core.json",
	                    actual: factionId,
	                    repairHint: "Используй существующий factionId из faction_core.json и не создавай orphan sidecar entries по ошибочному идентификатору."));
	            }
	        }

	        ValidateOptionalString(item, itemContext, issues, "factionId");
	        ValidateOptionalString(item, itemContext, issues, "factionName");
	        ValidateOptionalString(item, itemContext, issues, "name");
	    }


	    private HashSet<string> GetKnownCanonicalFactionIds()
	    {
	        if (_knownCanonicalFactionIdsCache != null)
	            return _knownCanonicalFactionIdsCache;

	        var ids = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
	        var factionCorePath = _fs.ResolvePath("game_state/factions/faction_core.json");
	        if (File.Exists(factionCorePath))
	        {
	            try
	            {
	                using var doc = JsonDocument.Parse(File.ReadAllText(factionCorePath));
	                foreach (var propName in new[] { "factions", "factionDataChanges" })
	                {
	                    if (!doc.RootElement.TryGetProperty(propName, out var arr) || arr.ValueKind != JsonValueKind.Array)
	                        continue;

	                    foreach (var item in arr.EnumerateArray())
	                    {
	                        var factionId = GetFirstNonEmptyString(item, "factionId");
	                        if (!string.IsNullOrWhiteSpace(factionId))
	                            ids.Add(factionId);
	                    }
	                }
	            }
	            catch
	            {
	                // ignored
	            }
	        }

	        _knownCanonicalFactionIdsCache = ids;
	        return ids;
	    }


    private void ValidateTimeChangeField(JsonElement root, string contextPrefix, List<ValidationIssue> issues, string propName)
    {
        if (!root.TryGetProperty(propName, out var value) || value.ValueKind == JsonValueKind.Null)
            return;

        var context = $"{contextPrefix}.{propName}";
        if (value.ValueKind != JsonValueKind.Number || !value.TryGetInt32(out var minutes))
        {
            issues.Add(new ValidationIssue(
                context,
                IssueSeverity.Error,
                "timeChange должен быть null или неотрицательным целым числом минут",
                code: "time_change_invalid_type",
                section: "WorldTime",
                expected: "null or non-negative integer minutes passed this turn",
                actual: value.ValueKind == JsonValueKind.Number ? "non-integer number" : value.ValueKind.ToString(),
                repairHint: "Для timeChange передай null, 0 или неотрицательное целое число минут по Block 17.3/Block 2 contract."));
            return;
        }

        if (minutes < 0)
        {
            issues.Add(new ValidationIssue(
                context,
                IssueSeverity.Error,
                "timeChange не может быть отрицательным",
                code: "time_change_negative",
                section: "WorldTime",
                expected: "null or non-negative integer minutes passed this turn",
                actual: minutes.ToString(),
                repairHint: "Используй в timeChange только фактически прошедшие минуты этого хода. Если значимого времени не прошло, передай 0 или null."));
        }
    }


    private void ValidateWorldTimeObject(JsonElement root, string contextPrefix, List<ValidationIssue> issues, string propName)
    {
        if (!root.TryGetProperty(propName, out var value) || value.ValueKind == JsonValueKind.Null)
            return;

        var context = $"{contextPrefix}.{propName}";
        if (value.ValueKind != JsonValueKind.Object)
        {
            issues.Add(new ValidationIssue(
                context,
                IssueSeverity.Error,
                "setWorldTime должен быть object или null",
                code: "world_time_invalid_root",
                section: "WorldTime",
                expected: "object with absolute date fields or null",
                actual: value.ValueKind.ToString(),
                repairHint: "Используй для setWorldTime либо null, либо полный absolute date object с year, monthName, dayOfMonth и timeOfDay. Если календарное имя месяца неоднозначно, не пытайся подгонять его под непроверяемую эвристику клиента."));
            return;
        }

        var missingFields = new List<string>();
        if (!value.TryGetProperty("year", out _))
            missingFields.Add("year");
        if (!HasNonEmptyString(value, "monthName"))
            missingFields.Add("monthName");
        if (!value.TryGetProperty("dayOfMonth", out _))
            missingFields.Add("dayOfMonth");
        if (!HasNonEmptyString(value, "timeOfDay"))
            missingFields.Add("timeOfDay");
        if (missingFields.Count > 0)
        {
            issues.Add(new ValidationIssue(
                context,
                IssueSeverity.Error,
                "setWorldTime должен содержать полный absolute date object",
                code: "world_time_missing_required_fields",
                section: "WorldTime",
                expected: "year, monthName, dayOfMonth, timeOfDay",
                actual: string.Join(", ", missingFields),
                repairHint: "Для setWorldTime передай полный absolute date object с year, monthName, dayOfMonth и timeOfDay по Block 23.A. currentTimeInMinutes на command surface не обязателен; при его отсутствии scheduler должен работать в fail-open режиме."));
            return;
        }

        ValidateIntegerField(value, context, issues, "year");
        ValidateIntegerField(value, context, issues, "dayOfMonth");
        if (value.TryGetProperty("dayOfMonth", out var dayOfMonthNode) &&
            dayOfMonthNode.ValueKind == JsonValueKind.Number &&
            dayOfMonthNode.TryGetInt32(out var dayOfMonth) &&
            dayOfMonth <= 0)
        {
            issues.Add(new ValidationIssue(
                $"{context}.dayOfMonth",
                IssueSeverity.Error,
                "setWorldTime.dayOfMonth должен быть положительным номером дня",
                code: "world_time_invalid_day_of_month",
                section: "WorldTime",
                expected: "positive integer dayOfMonth",
                actual: dayOfMonth.ToString(),
                repairHint: "Передай в setWorldTime.dayOfMonth фактический номер дня месяца, начиная с 1."));
        }
        var timeOfDay = RequireString(value, context, issues, "timeOfDay");
        if (!string.IsNullOrWhiteSpace(timeOfDay) && !TimeOfDayRegex.IsMatch(timeOfDay))
        {
            issues.Add(new ValidationIssue(
                $"{context}.timeOfDay",
                IssueSeverity.Error,
                "setWorldTime.timeOfDay должен быть в canonical HH:MM формате",
                code: "world_time_invalid_time_of_day_format",
                section: "WorldTime",
                expected: "HH:MM (24-hour format)",
                actual: timeOfDay,
                repairHint: "Для setWorldTime передай timeOfDay в формате HH:MM по Block 23.A, например 08:15 или 19:30."));
        }
    }


    private void ValidateDirectWorldTimeState(JsonElement root, string contextPrefix, List<ValidationIssue> issues)
    {
        var hasDirectAbsoluteField =
            root.TryGetProperty("year", out _) ||
            root.TryGetProperty("monthName", out _) ||
            root.TryGetProperty("dayOfMonth", out _) ||
            root.TryGetProperty("timeOfDay", out _) ||
            root.TryGetProperty("currentTimeInMinutes", out _);

        if (!hasDirectAbsoluteField)
            return;

        var context = $"{contextPrefix}.normalizedAbsoluteState";
        var missingFields = new List<string>();
        if (!root.TryGetProperty("year", out _))
            missingFields.Add("year");
        if (!HasNonEmptyString(root, "monthName"))
            missingFields.Add("monthName");
        if (!root.TryGetProperty("dayOfMonth", out _))
            missingFields.Add("dayOfMonth");
        if (!HasNonEmptyString(root, "timeOfDay"))
            missingFields.Add("timeOfDay");
        if (!root.TryGetProperty("currentTimeInMinutes", out _))
            missingFields.Add("currentTimeInMinutes");
        if (missingFields.Count > 0)
        {
            issues.Add(new ValidationIssue(
                context,
                IssueSeverity.Error,
                "world_time normalized absolute state должен содержать полный набор canonical date fields",
                code: "world_time_direct_state_missing_required_fields",
                section: "WorldTime",
                expected: "year, monthName, dayOfMonth, timeOfDay, currentTimeInMinutes",
                actual: string.Join(", ", missingFields),
                repairHint: "Если world_time.json хранится в normalized absolute state, передай полный root object с year, monthName, dayOfMonth, timeOfDay и currentTimeInMinutes. Не оставляй partial absolute state без полного набора полей."));
            return;
        }

        ValidateIntegerField(root, contextPrefix, issues, "year");
        ValidateIntegerField(root, contextPrefix, issues, "dayOfMonth");
        ValidateIntegerField(root, contextPrefix, issues, "currentTimeInMinutes");
        if (root.TryGetProperty("dayOfMonth", out var dayOfMonthNode) &&
            dayOfMonthNode.ValueKind == JsonValueKind.Number &&
            dayOfMonthNode.TryGetInt32(out var dayOfMonth) &&
            dayOfMonth <= 0)
        {
            issues.Add(new ValidationIssue(
                $"{contextPrefix}.dayOfMonth",
                IssueSeverity.Error,
                "world_time.dayOfMonth должен быть положительным номером дня",
                code: "world_time_direct_state_invalid_day_of_month",
                section: "WorldTime",
                expected: "positive integer dayOfMonth",
                actual: dayOfMonth.ToString(),
                repairHint: "Для normalized absolute world_time передай dayOfMonth как положительное целое число, начиная с 1."));
        }

        if (root.TryGetProperty("currentTimeInMinutes", out var currentTimeNode) &&
            currentTimeNode.ValueKind == JsonValueKind.Number &&
            currentTimeNode.TryGetInt32(out var currentTimeInMinutes) &&
            currentTimeInMinutes < 0)
        {
            issues.Add(new ValidationIssue(
                $"{contextPrefix}.currentTimeInMinutes",
                IssueSeverity.Error,
                "world_time.currentTimeInMinutes не может быть отрицательным",
                code: "world_time_direct_state_negative_minutes",
                section: "WorldTime",
                expected: "non-negative integer currentTimeInMinutes",
                actual: currentTimeInMinutes.ToString(),
                repairHint: "Для normalized absolute world_time передай currentTimeInMinutes как неотрицательное целое число."));
        }

        var timeOfDay = RequireString(root, contextPrefix, issues, "timeOfDay");
        if (!string.IsNullOrWhiteSpace(timeOfDay) && !TimeOfDayRegex.IsMatch(timeOfDay))
        {
            issues.Add(new ValidationIssue(
                $"{contextPrefix}.timeOfDay",
                IssueSeverity.Error,
                "world_time.timeOfDay должен быть в canonical HH:MM формате",
                code: "world_time_direct_state_invalid_time_of_day_format",
                section: "WorldTime",
                expected: "HH:MM (24-hour format)",
                actual: timeOfDay,
                repairHint: "Для normalized absolute world_time передай timeOfDay в формате HH:MM, например 08:15 или 19:30."));
        }
    }


    private void ValidateWeatherObject(JsonElement root, string contextPrefix, List<ValidationIssue> issues, string propName)
    {
        if (!root.TryGetProperty(propName, out var value) || value.ValueKind == JsonValueKind.Null)
            return;

        var context = $"{contextPrefix}.{propName}";
        if (value.ValueKind != JsonValueKind.Object)
        {
            issues.Add(new ValidationIssue(
                context,
                IssueSeverity.Error,
                "weatherChange должен быть object или null",
                code: "weather_change_invalid_root",
                section: "Weather",
                expected: "object with tendency and description or null",
                actual: value.ValueKind.ToString(),
                repairHint: "Используй для weatherChange либо null, либо объект с canonical tendency и непустым description."));
            return;
        }

        var missingFields = new List<string>();
        if (!HasNonEmptyString(value, "tendency"))
            missingFields.Add("tendency");
        if (!HasNonEmptyString(value, "description"))
            missingFields.Add("description");
        if (missingFields.Count > 0)
        {
            issues.Add(new ValidationIssue(
                context,
                IssueSeverity.Error,
                "weatherChange должен содержать и tendency, и description",
                code: "weather_change_missing_required_fields",
                section: "Weather",
                expected: "tendency and description",
                actual: string.Join(", ", missingFields),
                repairHint: "Для weatherChange передай both tendency и description по Block 27 contract."));
            return;
        }

        var tendency = RequireString(value, context, issues, "tendency");
        if (!string.IsNullOrWhiteSpace(tendency) && !AllowedWeatherTendencies.Contains(tendency))
        {
            issues.Add(new ValidationIssue(
                $"{context}.tendency",
                IssueSeverity.Error,
                "weatherChange.tendency должен быть одним из canonical weather commands",
                code: "weather_change_invalid_tendency",
                section: "Weather",
                expected: string.Join(" | ", AllowedWeatherTendencies),
                actual: tendency,
                repairHint: "Используй в weatherChange.tendency только IMPROVE, WORSEN, NO_CHANGE или один из JUMP_TO_* commands из Block 27.2. Если biome context неочевиден, не рассчитывай на эвристику клиента и оставляй корректный canonical tendency + description."));
        }
    }


    private void ValidateDirectWeatherState(JsonElement root, string contextPrefix, List<ValidationIssue> issues)
    {
        if (!contextPrefix.EndsWith("weather.json", StringComparison.OrdinalIgnoreCase))
            return;

        var hasDirectCommandField =
            root.TryGetProperty("tendency", out _) ||
            root.TryGetProperty("description", out _);

        if (!hasDirectCommandField)
            return;

        var context = $"{contextPrefix}.normalizedWeatherState";
        var missingFields = new List<string>();
        if (!HasNonEmptyString(root, "tendency"))
            missingFields.Add("tendency");
        if (!HasNonEmptyString(root, "description"))
            missingFields.Add("description");
        if (missingFields.Count > 0)
        {
            issues.Add(new ValidationIssue(
                context,
                IssueSeverity.Error,
                "weather.json direct root weather state должен содержать и tendency, и description",
                code: "weather_direct_state_missing_required_fields",
                section: "Weather",
                expected: "tendency and description",
                actual: string.Join(", ", missingFields),
                repairHint: "Если weather.json хранится в direct root форме, оставь на корне и tendency, и description. Иначе используй canonical wrapper weatherChange."));
            return;
        }

        var tendency = RequireString(root, contextPrefix, issues, "tendency");
        if (!string.IsNullOrWhiteSpace(tendency) && !AllowedWeatherTendencies.Contains(tendency))
        {
            issues.Add(new ValidationIssue(
                $"{contextPrefix}.tendency",
                IssueSeverity.Error,
                "weather.json direct root tendency должен быть одним из canonical weather commands",
                code: "weather_direct_state_invalid_tendency",
                section: "Weather",
                expected: string.Join(" | ", AllowedWeatherTendencies),
                actual: tendency,
                repairHint: "Если weather.json хранится в direct root форме, используй в tendency только IMPROVE, WORSEN, NO_CHANGE или один из JUMP_TO_* commands из Block 27.2. Если biome context неочевиден, не рассчитывай на клиентский lookup и оставляй корректный canonical tendency + description."));
        }
    }


    private void ValidateObjectOrArrayOfObjectsField(JsonElement root, string contextPrefix, List<ValidationIssue> issues, string propName)
    {
        if (!root.TryGetProperty(propName, out var value))
            return;

        var context = $"{contextPrefix}.{propName}";
        if (value.ValueKind == JsonValueKind.Object)
            return;

        if (value.ValueKind == JsonValueKind.Array)
        {
            RequireArrayOfObjects(value, context, issues);
            return;
        }

        issues.Add(new ValidationIssue(context, IssueSeverity.Error,
            "Поле должно быть объектом или массивом объектов"));
    }


    private void ValidateArrayOfObjectsField(JsonElement root, string contextPrefix, List<ValidationIssue> issues, string propName)
    {
        if (root.TryGetProperty(propName, out var value))
            RequireArrayOfObjects(value, $"{contextPrefix}.{propName}", issues);
    }


    private void ValidateArrayOfStringsField(JsonElement root, string contextPrefix, List<ValidationIssue> issues, string propName)
    {
        if (root.TryGetProperty(propName, out var value))
            RequireArrayOfStrings(value, $"{contextPrefix}.{propName}", issues);
    }


    private void ValidateNumberField(JsonElement root, string contextPrefix, List<ValidationIssue> issues, string propName)
    {
        if (!root.TryGetProperty(propName, out var value))
            return;

        if (value.ValueKind == JsonValueKind.Number)
            return;

        issues.Add(new ValidationIssue($"{contextPrefix}.{propName}", IssueSeverity.Error,
            "Поле должно быть числом"));
    }


    private void ValidateIntegerField(JsonElement root, string contextPrefix, List<ValidationIssue> issues, string propName)
    {
        if (!root.TryGetProperty(propName, out var value))
            return;

        if (value.ValueKind == JsonValueKind.Number && value.TryGetInt32(out _))
            return;

        issues.Add(new ValidationIssue($"{contextPrefix}.{propName}", IssueSeverity.Error,
            "Поле должно быть целым числом"));
    }


    private void ValidateNonNegativeNumberField(JsonElement root, string contextPrefix, List<ValidationIssue> issues, string propName)
    {
        if (!root.TryGetProperty(propName, out var value))
            return;

        if (value.ValueKind != JsonValueKind.Number || !value.TryGetInt32(out var intValue))
        {
            issues.Add(new ValidationIssue($"{contextPrefix}.{propName}", IssueSeverity.Error,
                "Поле должно быть неотрицательным целым числом"));
            return;
        }

        if (intValue < 0)
        {
            issues.Add(new ValidationIssue($"{contextPrefix}.{propName}", IssueSeverity.Error,
                "Поле не может быть отрицательным"));
        }
    }


    private void ValidateNonNegativeIntegerField(
        JsonElement root,
        string contextPrefix,
        List<ValidationIssue> issues,
        string propName,
        string section)
    {
        ValidateIntegerField(root, contextPrefix, issues, propName);
        if (TryReadInt(root, propName, out var intValue) && intValue < 0)
        {
            issues.Add(new ValidationIssue(
                $"{contextPrefix}.{propName}",
                IssueSeverity.Error,
                $"{propName} не может быть отрицательным",
                code: "non_negative_integer_field_negative",
                section: section,
                expected: "non-negative integer",
                actual: intValue.ToString(),
                repairHint: $"Сохраняй {propName} как неотрицательное целое число по canonical contract."));
        }
    }


    private void RequireNonNegativeNumberField(JsonElement root, string contextPrefix, List<ValidationIssue> issues, string propName)
    {
        if (!root.TryGetProperty(propName, out _))
        {
            issues.Add(new ValidationIssue(
                $"{contextPrefix}.{propName}",
                IssueSeverity.Error,
                $"Отсутствует обязательное неотрицательное числовое поле: {propName}",
                code: "missing_required_non_negative_number_field",
                expected: "non-negative number",
                actual: "missing",
                repairHint: $"Добавь обязательное числовое поле {propName} и сохраняй его как неотрицательное значение."));
            return;
        }

        ValidateNonNegativeNumberField(root, contextPrefix, issues, propName);
    }


    private void ValidatePositiveNumberField(JsonElement root, string contextPrefix, List<ValidationIssue> issues, string propName)
    {
        if (!root.TryGetProperty(propName, out var value))
            return;

        if (value.ValueKind != JsonValueKind.Number || !value.TryGetInt32(out var intValue))
        {
            issues.Add(new ValidationIssue(
                $"{contextPrefix}.{propName}",
                IssueSeverity.Error,
                "Поле должно быть положительным целым числом",
                code: "invalid_positive_integer_field",
                expected: "positive integer",
                actual: value.ValueKind.ToString(),
                repairHint: $"Сохраняй {propName} как положительное целое число без строковых alias-ов."));
            return;
        }

        if (intValue <= 0)
        {
            issues.Add(new ValidationIssue(
                $"{contextPrefix}.{propName}",
                IssueSeverity.Error,
                "Поле должно быть больше нуля",
                code: "positive_integer_field_non_positive",
                expected: "> 0",
                actual: intValue.ToString(),
                repairHint: $"Используй для {propName} положительное целое число больше нуля."));
        }
    }


    private void ValidatePositiveIntegerField(JsonElement root, string contextPrefix, List<ValidationIssue> issues, string propName)
    {
        if (!root.TryGetProperty(propName, out var value))
            return;

        if (value.ValueKind != JsonValueKind.Number || !value.TryGetInt32(out var intValue) || intValue <= 0)
        {
            issues.Add(new ValidationIssue(
                $"{contextPrefix}.{propName}",
                IssueSeverity.Error,
                "Поле должно быть положительным целым числом",
                code: "invalid_positive_integer_field",
                expected: "positive integer",
                actual: value.ValueKind == JsonValueKind.Number && value.TryGetInt32(out var parsedValue) ? parsedValue.ToString() : value.ValueKind.ToString(),
                repairHint: $"Сохраняй {propName} как положительное целое число больше нуля."));
        }
    }


    private static bool GetBoolean(JsonElement root, string propName, bool defaultValue)
    {
        if (!root.TryGetProperty(propName, out var value))
            return defaultValue;

        return value.ValueKind switch
        {
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            JsonValueKind.String when bool.TryParse(value.GetString(), out var parsed) => parsed,
            _ => defaultValue
        };
    }


    private static int GetIntOrDefault(JsonElement root, string propName, int defaultValue = 0)
    {
        if (!root.TryGetProperty(propName, out var value))
            return defaultValue;

        if (value.ValueKind == JsonValueKind.Number && value.TryGetInt32(out var parsed))
            return parsed;

        if (value.ValueKind == JsonValueKind.String && int.TryParse(value.GetString(), out var parsedFromString))
            return parsedFromString;

        return defaultValue;
    }


    private void ValidateNonNegativeNumericLikeField(JsonElement root, string contextPrefix, List<ValidationIssue> issues, string propName)
    {
        if (!root.TryGetProperty(propName, out var value))
            return;

        if (!TryReadDouble(root, propName, out var numericValue) || numericValue < 0)
        {
            issues.Add(new ValidationIssue(
                $"{contextPrefix}.{propName}",
                IssueSeverity.Error,
                $"{propName} должен быть неотрицательным числом",
                code: "numeric_value_invalid",
                expected: "non-negative number",
                actual: value.ValueKind == JsonValueKind.String ? (value.GetString() ?? string.Empty) : value.ValueKind.ToString(),
                repairHint: $"Передай {propName} как неотрицательное число без произвольных строковых alias-ов."));
        }
    }


    private void ValidateNumericUpperBound(JsonElement root, string contextPrefix, List<ValidationIssue> issues, string valueProp, string maxProp, string code)
    {
        if (!TryReadDouble(root, valueProp, out var value) ||
            !TryReadDouble(root, maxProp, out var maxValue))
        {
            return;
        }

        if (value > maxValue)
        {
            issues.Add(new ValidationIssue(
                $"{contextPrefix}.{valueProp}",
                IssueSeverity.Error,
                $"{valueProp} не должен превышать {maxProp}",
                code: code,
                section: "Inventory",
                expected: $"<= {maxProp} ({maxValue})",
                actual: value.ToString(),
                repairHint: $"Сохраняй {valueProp} в пределах {maxProp}. Для item resources текущее значение не может быть выше максимума."));
        }
    }


    private static bool TryGetArray(JsonElement root, string propName, string context, List<ValidationIssue> issues, out JsonElement array)
    {
        array = default;
        if (!root.TryGetProperty(propName, out var value))
            return false;

        if (value.ValueKind != JsonValueKind.Array)
        {
            issues.Add(new ValidationIssue(
                context,
                IssueSeverity.Error,
                "Поле должно быть массивом",
                code: "expected_array",
                expected: "JSON array",
                actual: value.ValueKind.ToString(),
                repairHint: $"Сохрани {propName} как массив по canonical contract."));
            return false;
        }

        array = value;
        return true;
    }


    private static bool RequireObject(JsonElement item, string context, List<ValidationIssue> issues)
    {
        if (item.ValueKind == JsonValueKind.Object) return true;

        issues.Add(new ValidationIssue(
            context,
            IssueSeverity.Error,
            "Элемент должен быть объектом",
            code: "expected_object",
            expected: "JSON object",
            actual: item.ValueKind.ToString(),
            repairHint: "Исправь элемент до JSON object перед заполнением его обязательных полей."));
        return false;
    }


    private static void RequireNpcIdentity(JsonElement item, string context, List<ValidationIssue> issues)
    {
        if (!HasAnyNonEmptyString(item, "NPCId", "npcId", "id", "NPCName", "npcName", "name"))
        {
            issues.Add(new ValidationIssue(
                context,
                IssueSeverity.Error,
                "Требуется хотя бы одно поле идентификации NPC: NPCId/npcId/id/NPCName/npcName/name",
                code: "missing_npc_identity",
                expected: "at least one of NPCId/npcId/id/NPCName/npcName/name",
                actual: "missing",
                repairHint: "Добавь хотя бы один canonical идентификатор NPC, чтобы клиент мог однозначно разрешить цель команды."));
        }
    }


    private static void RequireAnyString(JsonElement item, string context, List<ValidationIssue> issues, params string[] props)
    {
        if (!HasAnyNonEmptyString(item, props))
        {
            issues.Add(new ValidationIssue(
                context,
                IssueSeverity.Error,
                $"Требуется хотя бы одно поле: {string.Join("/", props)}",
                code: "missing_any_required_string",
                expected: $"at least one of {string.Join(", ", props)}",
                actual: "missing",
                repairHint: $"Добавь хотя бы одно непустое строковое поле из списка: {string.Join(", ", props)}."));
        }
    }


    private static List<string> GetMissingRequiredNonEmptyStringProperties(JsonElement item, params string[] props)
    {
        var missing = new List<string>();
        foreach (var prop in props)
        {
            if (!HasNonEmptyString(item, prop))
                missing.Add(prop);
        }

        return missing;
    }


    private static string RequireString(JsonElement item, string context, List<ValidationIssue> issues, string propName)
    {
        if (item.TryGetProperty(propName, out var value) &&
            value.ValueKind == JsonValueKind.String &&
            !string.IsNullOrWhiteSpace(value.GetString()))
        {
            return value.GetString() ?? "";
        }

        issues.Add(new ValidationIssue(
            $"{context}.{propName}",
            IssueSeverity.Error,
            $"Отсутствует обязательное строковое поле: {propName}",
            code: "missing_required_string",
            expected: $"{propName} as non-empty string",
            actual: item.TryGetProperty(propName, out var actualValue) ? actualValue.ValueKind.ToString() : "missing",
            repairHint: $"Добавь непустое строковое поле {propName} в canonical contract."));
        return "";
    }


    private static void RequireNumberOrString(JsonElement item, string context, List<ValidationIssue> issues, string propName)
    {
        if (item.TryGetProperty(propName, out var value))
        {
            if (value.ValueKind == JsonValueKind.Number) return;
            if (value.ValueKind == JsonValueKind.String && !string.IsNullOrWhiteSpace(value.GetString())) return;
        }

        issues.Add(new ValidationIssue(
            $"{context}.{propName}",
            IssueSeverity.Error,
            $"Отсутствует обязательное числовое или строковое поле: {propName}",
            code: "missing_required_number_or_string",
            expected: $"{propName} as number or non-empty string",
            actual: item.TryGetProperty(propName, out var actualValue) ? actualValue.ValueKind.ToString() : "missing",
            repairHint: $"Добавь поле {propName} как число или непустую строку по canonical contract."));
    }


    private static void RequireObjectProperty(JsonElement item, string context, List<ValidationIssue> issues, string propName)
    {
        if (item.TryGetProperty(propName, out var value) && value.ValueKind == JsonValueKind.Object)
            return;

        issues.Add(new ValidationIssue(
            $"{context}.{propName}",
            IssueSeverity.Error,
            $"Отсутствует обязательный объект: {propName}",
            code: "missing_required_object_property",
            expected: $"{propName} as JSON object",
            actual: item.TryGetProperty(propName, out var actualValue) ? actualValue.ValueKind.ToString() : "missing",
            repairHint: $"Добавь обязательный объект {propName} в canonical contract."));
    }


    private static void RequireArrayOfObjects(JsonElement value, string context, List<ValidationIssue> issues)
    {
        if (value.ValueKind != JsonValueKind.Array)
        {
            issues.Add(new ValidationIssue(
                context,
                IssueSeverity.Error,
                "Поле должно быть массивом объектов",
                code: "expected_array_of_objects",
                expected: "array<object>",
                actual: value.ValueKind.ToString(),
                repairHint: "Сохрани поле как массив JSON-объектов по canonical contract."));
            return;
        }

        var index = 0;
        foreach (var item in value.EnumerateArray())
        {
            if (item.ValueKind != JsonValueKind.Object)
            {
                issues.Add(new ValidationIssue(
                    $"{context}[{index}]",
                    IssueSeverity.Error,
                    "Элемент должен быть объектом",
                    code: "expected_object_in_array",
                    expected: "JSON object",
                    actual: item.ValueKind.ToString(),
                    repairHint: "Исправь элемент массива до JSON object перед заполнением его обязательных полей."));
            }
            index++;
        }
    }


    private static void RequireArrayOfStrings(JsonElement value, string context, List<ValidationIssue> issues)
    {
        if (value.ValueKind != JsonValueKind.Array)
        {
            issues.Add(new ValidationIssue(
                context,
                IssueSeverity.Error,
                "Поле должно быть массивом строк",
                code: "expected_string_array",
                expected: "JSON array of non-empty strings",
                actual: value.ValueKind.ToString(),
                repairHint: "Сохрани поле как массив непустых строк по canonical contract."));
            return;
        }

        var index = 0;
        foreach (var item in value.EnumerateArray())
        {
            if (item.ValueKind != JsonValueKind.String || string.IsNullOrWhiteSpace(item.GetString()))
            {
                issues.Add(new ValidationIssue(
                    $"{context}[{index}]",
                    IssueSeverity.Error,
                    "Элемент должен быть непустой строкой",
                    code: "invalid_string_array_item",
                    expected: "non-empty string",
                    actual: item.ValueKind == JsonValueKind.String ? (item.GetString() ?? string.Empty) : item.ValueKind.ToString(),
                    repairHint: "Исправь элемент массива до непустой строки."));
            }
            index++;
        }
    }


    private static void RequireArrayOfStringsProperty(JsonElement item, string context, List<ValidationIssue> issues, string propName)
    {
        if (item.TryGetProperty(propName, out var value))
            RequireArrayOfStrings(value, $"{context}.{propName}", issues);
        else
            issues.Add(new ValidationIssue(
                $"{context}.{propName}",
                IssueSeverity.Error,
                $"Отсутствует обязательное поле: {propName}",
                code: "missing_required_string_array_field",
                expected: "array of non-empty strings",
                actual: "missing",
                repairHint: $"Добавь обязательное поле {propName} как массив непустых строк."));
    }


    private static bool HasAnyNonEmptyString(JsonElement item, params string[] props)
    {
        foreach (var prop in props)
            if (HasNonEmptyString(item, prop))
                return true;
        return false;
    }


    private static bool HasNonEmptyString(JsonElement item, string prop)
    {
        return item.TryGetProperty(prop, out var value) &&
               value.ValueKind == JsonValueKind.String &&
               !string.IsNullOrWhiteSpace(value.GetString());
    }


    private static void ValidatePercentageField(JsonElement parent, string fieldName,
        List<ValidationIssue> issues)
    {
        if (!parent.TryGetProperty(fieldName, out var val)) return;

        if (val.ValueKind == JsonValueKind.String)
        {
            var str = val.GetString()?.Replace("%", "").Trim();
            if (!int.TryParse(str, out var pct) || pct < 0 || pct > 100)
            {
                issues.Add(new ValidationIssue(
                    "playerStatus", IssueSeverity.Warning,
                    $"{fieldName} = '{val.GetString()}' — некорректный процент"));
            }
        }
    }


    private static bool MatchesHistoricalEntryContract(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return false;

        return HistoricalEntryTimestampRegex.IsMatch(value) || LegacyTurnPrefixedEntryRegex.IsMatch(value);
    }


    private static List<string> CollectAchievementNamesRequiringNarrativeMarkers(JsonElement currentRoot, JsonElement? previousRoot)
    {
        var requiredNames = new List<string>();
        var seenKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var entry in EnumerateAchievementIdentityAndName(currentRoot, "achievementUnlocks"))
        {
            if (seenKeys.Add(entry.Key))
                requiredNames.Add(entry.Name);
        }

        var previousUnlocked = previousRoot.HasValue
            ? EnumerateAchievementIdentityAndName(previousRoot.Value, "unlockedAchievements")
                .ToDictionary(entry => entry.Key, entry => entry.Name, StringComparer.OrdinalIgnoreCase)
            : new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        foreach (var entry in EnumerateAchievementIdentityAndName(currentRoot, "unlockedAchievements"))
        {
            if (!previousUnlocked.ContainsKey(entry.Key) && seenKeys.Add(entry.Key))
                requiredNames.Add(entry.Name);
        }

        return requiredNames;
    }


    private static IEnumerable<(string Key, string Name)> EnumerateAchievementIdentityAndName(JsonElement root, string propName)
    {
        if (!root.TryGetProperty(propName, out var arr) || arr.ValueKind != JsonValueKind.Array)
            yield break;

        foreach (var item in arr.EnumerateArray())
        {
            if (item.ValueKind != JsonValueKind.Object)
                continue;

            var name = GetFirstNonEmptyString(item, "name");
            var key = GetFirstNonEmptyString(item, "achievementId", "name");
            if (string.IsNullOrWhiteSpace(name) || string.IsNullOrWhiteSpace(key))
                continue;

            yield return (key, name);
        }
    }


    private string? TryReadCurrentTurnGachaBaseRaritySync()
    {
        var requestPath = _fs.ResolvePath("input/turn_request.json");
        if (File.Exists(requestPath))
        {
            try
            {
                using var doc = JsonDocument.Parse(File.ReadAllText(requestPath));
                if (doc.RootElement.TryGetProperty("gachaBaseResult", out var gachaBaseResult) &&
                    gachaBaseResult.ValueKind == JsonValueKind.Object &&
                    gachaBaseResult.TryGetProperty("baseRarity", out var baseRarity) &&
                    baseRarity.ValueKind == JsonValueKind.String)
                {
                    return baseRarity.GetString();
                }
            }
            catch
            {
                // ignored
            }
        }

        return TryReadValidatedTurnGachaBaseRaritySync();
    }

    private string? TryReadValidatedTurnGachaBaseRaritySync()
    {
        var manifest = LoadValidatedCurrentPendingTurnSnapshotManifestSync();
        if (manifest?.GachaBaseResult != null &&
            manifest.GachaBaseResult.TryGetPropertyValue("baseRarity", out var manifestBaseRarityNode) &&
            manifestBaseRarityNode is JsonValue manifestBaseRarityValue &&
            manifestBaseRarityValue.TryGetValue<string>(out var manifestBaseRarity) &&
            !string.IsNullOrWhiteSpace(manifestBaseRarity))
        {
            return manifestBaseRarity;
        }

        return null;
    }


    private void ValidateMinimalSoulRelicObject(JsonElement relic, string context, List<ValidationIssue> issues, string section)
    {
        RequireString(relic, context, issues, "relicId");
        RequireString(relic, context, issues, "name");

        var rarity = GetFirstNonEmptyString(relic, "rarity", "quality");
        if (!relic.TryGetProperty("rarity", out _) && !relic.TryGetProperty("quality", out _))
        {
            issues.Add(new ValidationIssue(
                $"{context}.rarity",
                IssueSeverity.Error,
                "Soul Relic object должен содержать rarity или quality",
                code: "soul_relic_missing_rarity",
                section: section,
                expected: "Common | Uncommon | Rare | Epic | Legendary",
                actual: "missing",
                repairHint: "Передай в Soul Relic object canonical rarity/quality и не используй skeletal relic payload без редкости."));
            return;
        }

        if (!string.IsNullOrWhiteSpace(rarity) && GetRarityRank(rarity) == 0)
        {
            issues.Add(new ValidationIssue(
                $"{context}.rarity",
                IssueSeverity.Error,
                "Soul Relic rarity должна быть одним из canonical enum значений",
                code: "soul_relic_invalid_rarity",
                section: section,
                expected: "Common | Uncommon | Rare | Epic | Legendary",
                actual: rarity,
                repairHint: "Используй для Soul Relic только canonical rarity/quality enum из Block 31."));
        }

        var relicType = GetFirstNonEmptyString(relic, "relicType", "type");
        var hasCompanionSeed = relic.TryGetProperty("companionSeed", out var companionSeed);
        if (string.Equals(relicType, GuardianAbodeResidentState.RelicTypeCompanionEcho, StringComparison.OrdinalIgnoreCase) || hasCompanionSeed)
        {
            if (!hasCompanionSeed || !RequireObject(companionSeed, $"{context}.companionSeed", issues))
            {
                issues.Add(new ValidationIssue(
                    $"{context}.companionSeed",
                    IssueSeverity.Error,
                    "companion_echo Soul Relic должна содержать companionSeed object",
                    code: "companion_echo_relic_missing_seed",
                    section: section,
                    expected: "companionSeed object",
                    actual: hasCompanionSeed ? companionSeed.ValueKind.ToString() : "missing",
                    repairHint: "Для companion_echo реликвии обязательно сохраняй companionSeed с sourceResidentId, sourceGuardianId, companionNameHint, originWorldSummary и futureCompanionPrompt."));
                return;
            }

            RequireString(companionSeed, $"{context}.companionSeed", issues, "sourceResidentId");
            RequireString(companionSeed, $"{context}.companionSeed", issues, "sourceGuardianId");
            RequireString(companionSeed, $"{context}.companionSeed", issues, "companionNameHint");
            RequireString(companionSeed, $"{context}.companionSeed", issues, "originWorldSummary");
            RequireString(companionSeed, $"{context}.companionSeed", issues, "futureCompanionPrompt");
            ValidateOptionalString(companionSeed, $"{context}.companionSeed", issues, "bondReason");

            if (companionSeed.TryGetProperty("coreTraits", out var coreTraits))
                RequireArrayOfStrings(coreTraits, $"{context}.companionSeed.coreTraits", issues);
            if (companionSeed.TryGetProperty("archetypeHints", out var archetypeHints))
                RequireArrayOfStrings(archetypeHints, $"{context}.companionSeed.archetypeHints", issues);
            if (companionSeed.TryGetProperty("appearanceMotifs", out var appearanceMotifs))
                RequireArrayOfStrings(appearanceMotifs, $"{context}.companionSeed.appearanceMotifs", issues);
            ValidateResidentCompanionSnapshotFields(companionSeed, $"{context}.companionSeed", issues, section);
        }

        var hasEmbeddedSoulImprint = relic.TryGetProperty("soulImprint", out var soulImprint) || relic.TryGetProperty("npcSoulImprint", out soulImprint);
        if (hasEmbeddedSoulImprint)
        {
            var imprintContext = relic.TryGetProperty("soulImprint", out var _) ? $"{context}.soulImprint" : $"{context}.npcSoulImprint";
            if (!RequireObject(soulImprint, imprintContext, issues))
            {
                issues.Add(new ValidationIssue(
                    imprintContext,
                    IssueSeverity.Error,
                    "Soul Relic со слепком НПС должна содержать object-представление imprint",
                    code: "soul_relic_embedded_imprint_invalid",
                    section: section,
                    expected: "soulImprint or npcSoulImprint object",
                    actual: soulImprint.ValueKind.ToString(),
                    repairHint: "Сохраняй embedded NPC imprint внутри реликвии как object с идентичностью, summary и core traits."));
                return;
            }

            var imprintName = GetFirstNonEmptyString(soulImprint, "NPCName", "npcName", "name", "companionName", "originalName");
            var imprintId = GetFirstNonEmptyString(soulImprint, "imprintId", "id");
            var imprintDescription = GetFirstNonEmptyString(soulImprint, "description", "summary", "backgroundStory", "history");
            if (string.IsNullOrWhiteSpace(imprintName) && string.IsNullOrWhiteSpace(imprintId))
            {
                issues.Add(new ValidationIssue(
                    imprintContext,
                    IssueSeverity.Error,
                    "Embedded NPC imprint в реликвии должен содержать имя или imprintId",
                    code: "soul_relic_embedded_imprint_missing_identity",
                    section: section,
                    repairHint: "Сохраняй в soulImprint/npcSoulImprint хотя бы NPCName/name или imprintId/id."));
            }

            if (string.IsNullOrWhiteSpace(imprintDescription))
            {
                issues.Add(new ValidationIssue(
                    imprintContext,
                    IssueSeverity.Error,
                    "Embedded NPC imprint в реликвии должен содержать summary/description",
                    code: "soul_relic_embedded_imprint_missing_summary",
                    section: section,
                    repairHint: "Сохраняй в soulImprint/npcSoulImprint краткое описание прошлого NPC."));
            }

            var hasCoreTraits =
                (soulImprint.TryGetProperty("coreTraitsPreserved", out var coreTraitsPreserved) && coreTraitsPreserved.ValueKind == JsonValueKind.Array && coreTraitsPreserved.GetArrayLength() > 0) ||
                (soulImprint.TryGetProperty("coreTraits", out var coreTraits) && coreTraits.ValueKind == JsonValueKind.Array && coreTraits.GetArrayLength() > 0) ||
                (soulImprint.TryGetProperty("personalityTraits", out var personalityTraits) && personalityTraits.ValueKind == JsonValueKind.Array && personalityTraits.GetArrayLength() > 0);
            if (!hasCoreTraits)
            {
                issues.Add(new ValidationIssue(
                    imprintContext,
                    IssueSeverity.Error,
                    "Embedded NPC imprint в реликвии должен сохранять core traits или personality traits",
                    code: "soul_relic_embedded_imprint_missing_traits",
                    section: section,
                    repairHint: "Сохраняй в soulImprint/npcSoulImprint coreTraitsPreserved, coreTraits или personalityTraits."));
            }
        }
    }

    private void ValidateResidentCompanionSnapshotFields(JsonElement root, string contextPrefix, List<ValidationIssue> issues, string section)
    {
        if (root.TryGetProperty("personalityProfile", out var personalityProfile) &&
            personalityProfile.ValueKind != JsonValueKind.Null)
        {
            ValidateResidentCompanionPersonalityProfile(personalityProfile, $"{contextPrefix}.personalityProfile", issues, section);
        }

        if (root.TryGetProperty("abodeDisposition", out var abodeDisposition) &&
            abodeDisposition.ValueKind != JsonValueKind.Null)
        {
            ValidateResidentCompanionAbodeDisposition(abodeDisposition, $"{contextPrefix}.abodeDisposition", issues, section);
        }

        var hasAnyAbodeRelationField =
            root.TryGetProperty("abodeDevotionLevel", out _) ||
            root.TryGetProperty("abodeDevotionTier", out _) ||
            root.TryGetProperty("restlessness", out _) ||
            root.TryGetProperty("migrationState", out _);
        if (hasAnyAbodeRelationField)
            ValidateResidentCompanionAbodeRelation(root, contextPrefix, issues, section);
    }

    private void ValidateResidentCompanionPersonalityProfile(JsonElement value, string contextPrefix, List<ValidationIssue> issues, string section)
    {
        if (!RequireObject(value, contextPrefix, issues))
            return;

        RequireString(value, contextPrefix, issues, "archetype");
        RequireString(value, contextPrefix, issues, "worldview");
        RequireString(value, contextPrefix, issues, "culturalLayer");
        if (value.TryGetProperty("coreValues", out var coreValues))
            RequireArrayOfStrings(coreValues, $"{contextPrefix}.coreValues", issues);
        if (value.TryGetProperty("personalityTraits", out var personalityTraits))
            ValidateArrayItems(personalityTraits, $"{contextPrefix}.personalityTraits", issues, ValidateResidentCompanionPersonalityTrait);
    }

    private void ValidateResidentCompanionPersonalityTrait(JsonElement value, string contextPrefix, List<ValidationIssue> issues)
    {
        if (!RequireObject(value, contextPrefix, issues))
            return;

        RequireString(value, contextPrefix, issues, "traitName");
        RequireString(value, contextPrefix, issues, "valueDescription");
        ValidateIntegerField(value, contextPrefix, issues, "value");
        ValidateOptionalString(value, contextPrefix, issues, "description");

        if (TryReadInt(value, "value", out var traitValue) && (traitValue < 1 || traitValue > 10))
        {
            issues.Add(new ValidationIssue(
                $"{contextPrefix}.value",
                IssueSeverity.Error,
                "Resident companion personality trait value должен быть в диапазоне 1..10",
                code: "companion_seed_personality_trait_value_out_of_bounds",
                section: "SoulRelics",
                expected: "1..10",
                actual: traitValue.ToString(),
                repairHint: "Сохраняй companionSeed.personalityProfile.personalityTraits[].value как integer от 1 до 10."));
        }
    }

    private void ValidateResidentCompanionAbodeDisposition(JsonElement value, string contextPrefix, List<ValidationIssue> issues, string section)
    {
        if (!RequireObject(value, contextPrefix, issues))
            return;

        var powerSensitivity = RequireString(value, contextPrefix, issues, "powerSensitivity");
        if (!string.IsNullOrWhiteSpace(powerSensitivity) && !GuardianAbodeResidentState.IsSupportedPowerSensitivity(powerSensitivity))
        {
            issues.Add(new ValidationIssue(
                $"{contextPrefix}.powerSensitivity",
                IssueSeverity.Error,
                "Resident companion abodeDisposition.powerSensitivity должен быть canonical enum значением",
                code: "companion_seed_invalid_power_sensitivity",
                section: section,
                expected: "low | medium | high",
                actual: powerSensitivity,
                repairHint: "Используй для companionSeed.abodeDisposition.powerSensitivity только low, medium или high."));
        }

        var migrationDisposition = RequireString(value, contextPrefix, issues, "migrationDisposition");
        if (!string.IsNullOrWhiteSpace(migrationDisposition) && !GuardianAbodeResidentState.IsSupportedMigrationDisposition(migrationDisposition))
        {
            issues.Add(new ValidationIssue(
                $"{contextPrefix}.migrationDisposition",
                IssueSeverity.Error,
                "Resident companion abodeDisposition.migrationDisposition должен быть canonical enum значением",
                code: "companion_seed_invalid_migration_disposition",
                section: section,
                expected: "rooted | selective | opportunistic | wandering",
                actual: migrationDisposition,
                repairHint: "Используй для companionSeed.abodeDisposition.migrationDisposition только rooted, selective, opportunistic или wandering."));
        }

        var communalOrientation = RequireString(value, contextPrefix, issues, "communalOrientation");
        if (!string.IsNullOrWhiteSpace(communalOrientation) && !GuardianAbodeResidentState.IsSupportedCommunalOrientation(communalOrientation))
        {
            issues.Add(new ValidationIssue(
                $"{contextPrefix}.communalOrientation",
                IssueSeverity.Error,
                "Resident companion abodeDisposition.communalOrientation должен быть canonical enum значением",
                code: "companion_seed_invalid_communal_orientation",
                section: section,
                expected: "low | medium | high",
                actual: communalOrientation,
                repairHint: "Используй для companionSeed.abodeDisposition.communalOrientation только low, medium или high."));
        }

        var stabilityNeed = RequireString(value, contextPrefix, issues, "stabilityNeed");
        if (!string.IsNullOrWhiteSpace(stabilityNeed) && !GuardianAbodeResidentState.IsSupportedStabilityNeed(stabilityNeed))
        {
            issues.Add(new ValidationIssue(
                $"{contextPrefix}.stabilityNeed",
                IssueSeverity.Error,
                "Resident companion abodeDisposition.stabilityNeed должен быть canonical enum значением",
                code: "companion_seed_invalid_stability_need",
                section: section,
                expected: "low | medium | high",
                actual: stabilityNeed,
                repairHint: "Используй для companionSeed.abodeDisposition.stabilityNeed только low, medium или high."));
        }
    }

    private void ValidateResidentCompanionAbodeRelation(JsonElement root, string contextPrefix, List<ValidationIssue> issues, string section)
    {
        ValidateIntegerField(root, contextPrefix, issues, "abodeDevotionLevel");
        ValidateIntegerField(root, contextPrefix, issues, "restlessness");
        RequireString(root, contextPrefix, issues, "abodeDevotionTier");
        RequireString(root, contextPrefix, issues, "migrationState");

        if (TryReadInt(root, "abodeDevotionLevel", out var abodeDevotionLevel) &&
            (abodeDevotionLevel < 0 || abodeDevotionLevel > 100))
        {
            issues.Add(new ValidationIssue(
                $"{contextPrefix}.abodeDevotionLevel",
                IssueSeverity.Error,
                "Resident companion abodeDevotionLevel должен быть в диапазоне 0..100",
                code: "companion_seed_abode_devotion_out_of_bounds",
                section: section,
                expected: "0..100",
                actual: abodeDevotionLevel.ToString(),
                repairHint: "Сохраняй companionSeed.abodeDevotionLevel как integer от 0 до 100."));
        }

        if (TryReadInt(root, "restlessness", out var restlessness) &&
            (restlessness < 0 || restlessness > 100))
        {
            issues.Add(new ValidationIssue(
                $"{contextPrefix}.restlessness",
                IssueSeverity.Error,
                "Resident companion restlessness должен быть в диапазоне 0..100",
                code: "companion_seed_restlessness_out_of_bounds",
                section: section,
                expected: "0..100",
                actual: restlessness.ToString(),
                repairHint: "Сохраняй companionSeed.restlessness как integer от 0 до 100."));
        }

        var actualTier = GetFirstNonEmptyString(root, "abodeDevotionTier");
        if (!string.IsNullOrWhiteSpace(actualTier) && !GuardianAbodeResidentState.IsSupportedAbodeDevotionTier(actualTier))
        {
            issues.Add(new ValidationIssue(
                $"{contextPrefix}.abodeDevotionTier",
                IssueSeverity.Error,
                "Resident companion abodeDevotionTier должен быть canonical devotion tier",
                code: "companion_seed_invalid_abode_devotion_tier",
                section: section,
                expected: "alienated | uncertain | attached | devoted | steadfast",
                actual: actualTier,
                repairHint: "Используй для companionSeed.abodeDevotionTier только alienated, uncertain, attached, devoted или steadfast."));
        }
        else if (TryReadInt(root, "abodeDevotionLevel", out var devotionLevel))
        {
            var expectedTier = GuardianAbodeResidentState.ResolveAbodeDevotionTier(devotionLevel);
            if (!string.IsNullOrWhiteSpace(actualTier) &&
                !string.Equals(actualTier, expectedTier, StringComparison.OrdinalIgnoreCase))
            {
                issues.Add(new ValidationIssue(
                    $"{contextPrefix}.abodeDevotionTier",
                    IssueSeverity.Error,
                    "Resident companion abodeDevotionTier должен совпадать с tier, выведенным из abodeDevotionLevel",
                    code: "companion_seed_abode_devotion_tier_mismatch",
                    section: section,
                    expected: expectedTier,
                    actual: actualTier,
                    repairHint: "Синхронизируй companionSeed.abodeDevotionTier с abodeDevotionLevel по canonical 5-tier mapping."));
            }
        }

        var actualMigrationState = GetFirstNonEmptyString(root, "migrationState");
        if (!string.IsNullOrWhiteSpace(actualMigrationState) && !GuardianAbodeResidentState.IsSupportedMigrationState(actualMigrationState))
        {
            issues.Add(new ValidationIssue(
                $"{contextPrefix}.migrationState",
                IssueSeverity.Error,
                "Resident companion migrationState должен быть canonical migration state",
                code: "companion_seed_invalid_migration_state",
                section: section,
                expected: "settled | wavering | restless | considering_departure | ready_to_transfer",
                actual: actualMigrationState,
                repairHint: "Используй для companionSeed.migrationState только settled, wavering, restless, considering_departure или ready_to_transfer."));
        }
        else if (TryReadInt(root, "abodeDevotionLevel", out var currentDevotionLevel) &&
                 TryReadInt(root, "restlessness", out var currentRestlessness))
        {
            var expectedMigrationState = GuardianAbodeResidentState.ResolveMigrationState(currentDevotionLevel, currentRestlessness);
            if (!string.IsNullOrWhiteSpace(actualMigrationState) &&
                !string.Equals(actualMigrationState, expectedMigrationState, StringComparison.OrdinalIgnoreCase))
            {
                issues.Add(new ValidationIssue(
                    $"{contextPrefix}.migrationState",
                    IssueSeverity.Error,
                    "Resident companion migrationState должен совпадать с state, выведенным из abodeDevotionLevel и restlessness",
                    code: "companion_seed_migration_state_mismatch",
                    section: section,
                    expected: expectedMigrationState,
                    actual: actualMigrationState,
                    repairHint: "Синхронизируй companionSeed.migrationState с canonical resolver, зависящим от abodeDevotionLevel и restlessness."));
            }
        }
    }


    private static int GetRarityRank(string? rarity) => rarity?.Trim().ToLowerInvariant() switch
    {
        "common" => 1,
        "uncommon" => 2,
        "rare" => 3,
        "epic" => 4,
        "legendary" => 5,
        _ => 0
    };


    private static bool LooksLikeEnglishImagePrompt(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return false;

        return value.Length <= 150 && !CyrillicRegex.IsMatch(value);
    }


    private static void ValidatePercentageStringField(
        JsonElement root,
        string contextPrefix,
        List<ValidationIssue> issues,
        string propName,
        bool requirePositive)
    {
        if (!root.TryGetProperty(propName, out var value))
            return;

        if (value.ValueKind != JsonValueKind.String)
        {
            issues.Add(new ValidationIssue(
                $"{contextPrefix}.{propName}",
                IssueSeverity.Error,
                $"{propName} должен быть percentage string"));
            return;
        }

        if (!TryParsePercentageString(value.GetString(), requirePositive, out _))
        {
            issues.Add(new ValidationIssue(
                $"{contextPrefix}.{propName}",
                IssueSeverity.Error,
                requirePositive
                    ? $"{propName} должен быть положительным percentage string"
                    : $"{propName} должен быть неотрицательным percentage string"));
        }
    }


    private static bool TryParsePercentageString(string? raw, bool requirePositive, out int parsed)
    {
        parsed = 0;
        if (string.IsNullOrWhiteSpace(raw))
            return false;

        var trimmed = raw.Trim();
        if (!trimmed.EndsWith("%", StringComparison.Ordinal))
            return false;

        var numericPart = trimmed[..^1].Trim();
        if (!int.TryParse(numericPart, out parsed) || parsed < 0 || (requirePositive && parsed == 0))
            return false;

        return true;
    }
}
