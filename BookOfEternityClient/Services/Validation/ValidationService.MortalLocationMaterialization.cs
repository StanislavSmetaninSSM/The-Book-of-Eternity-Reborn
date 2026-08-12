using System.Text.Json;
using System.Text.Json.Nodes;
using BookOfEternityClient.Core;

namespace BookOfEternityClient.Services;

public partial class ValidationService
{
    public async Task<IReadOnlyList<ValidationIssue>>
        ValidateAcceptedTurnCanonicalMortalLocationMaterializationAsync()
    {
        var issues = new List<ValidationIssue>();
        await ValidateAcceptedTurnCanonicalMortalLocationMaterializationAsync(
            issues,
            writeLease: null);
        return issues;
    }

    internal async Task<IReadOnlyList<ValidationIssue>>
        ValidateAcceptedTurnCanonicalMortalLocationMaterializationAsync(
            FileSystemManager.CanonicalWriteLease writeLease)
    {
        ArgumentNullException.ThrowIfNull(writeLease);
        var issues = new List<ValidationIssue>();
        await ValidateAcceptedTurnCanonicalMortalLocationMaterializationAsync(
            issues,
            writeLease);
        return issues;
    }

    public async Task<IReadOnlyList<ValidationIssue>>
        ValidateAcceptedTurnRawMortalLocationMaterializationAsync()
    {
        var issues = new List<ValidationIssue>();
        await ValidateAcceptedTurnRawMortalLocationFileAsync(
            MortalLocationMaterializationContract.CurrentLocationPath,
            validateCurrentLocation: true,
            issues);
        await ValidateAcceptedTurnRawMortalLocationFileAsync(
            MortalLocationMaterializationContract.WorldMapPath,
            validateCurrentLocation: false,
            issues);
        return issues;
    }

    private async Task ValidateAcceptedTurnCanonicalMortalLocationMaterializationAsync(
        List<ValidationIssue> issues)
        => await ValidateAcceptedTurnCanonicalMortalLocationMaterializationAsync(
            issues,
            writeLease: null);

    private async Task ValidateAcceptedTurnCanonicalMortalLocationMaterializationAsync(
        List<ValidationIssue> issues,
        FileSystemManager.CanonicalWriteLease? writeLease)
    {
        var mapJson = ShouldValidateStateFile(MortalLocationMaterializationContract.WorldMapPath)
            ? await ReadMortalLocationValidationFileAsync(
                MortalLocationMaterializationContract.WorldMapPath,
                writeLease)
            : null;
        var currentJson = ShouldValidateStateFile(MortalLocationMaterializationContract.CurrentLocationPath)
            ? await ReadMortalLocationValidationFileAsync(
                MortalLocationMaterializationContract.CurrentLocationPath,
                writeLease)
            : null;
        var indexJson = ShouldValidateStateFile(MortalLocationIdentityState.StatePath)
            ? await ReadMortalLocationValidationFileAsync(
                MortalLocationIdentityState.StatePath,
                writeLease)
            : null;

        if (string.IsNullOrWhiteSpace(mapJson) &&
            string.IsNullOrWhiteSpace(currentJson) &&
            string.IsNullOrWhiteSpace(indexJson))
        {
            return;
        }

        var map = ParseLocationStateObject(
            mapJson,
            MortalLocationMaterializationContract.WorldMapPath,
            issues);
        var identityState = MortalLocationIdentityState.Parse(indexJson);
        issues.AddRange(identityState.Issues);
        if (map == null)
            return;

        if (!TryReadCanonicalWorldMap(map, out var locations, out var links))
        {
            issues.Add(LocationIssue(
                MortalLocationMaterializationContract.WorldMapPath,
                "mortal_location_materialization_invalid_world_map",
                "Mortal world map must have the exact canonical root.",
                "schemaVersion=1, realm=mortal_world, locations[], links[]",
                map.ToJsonString()));
            return;
        }

        ValidateCanonicalLocationArray(locations, links: false, issues);
        ValidateCanonicalLocationArray(links, links: true, issues);
        issues.AddRange(identityState.ValidateCanonicalState(map));

        if (!string.IsNullOrWhiteSpace(currentJson))
        {
            var current = ParseLocationStateObject(
                currentJson,
                MortalLocationMaterializationContract.CurrentLocationPath,
                issues);
            if (current != null)
                ValidateCurrentLocationProjection(current, locations, issues);
        }
    }

    private Task<string?> ReadMortalLocationValidationFileAsync(
        string path,
        FileSystemManager.CanonicalWriteLease? writeLease) =>
        writeLease == null
            ? _fs.ReadFileAsync(path)
            : _fs.ReadFileAsync(writeLease, path);

    private void ValidateRawMortalLocationMaterializationResponse(
        JsonElement response,
        List<ValidationIssue> issues)
    {
        if (response.ValueKind != JsonValueKind.Object)
            return;

        ValidateRawMortalLocationClientOwnedRootFields(response, "response", issues);

        if (response.TryGetProperty("currentLocationData", out var current) &&
            current.ValueKind == JsonValueKind.Object)
        {
            ValidateRawCurrentLocationCreation(
                current,
                "response.currentLocationData",
                "currentLocationData",
                issues);
        }

        if (!response.TryGetProperty("worldMapUpdates", out var updates) ||
            updates.ValueKind != JsonValueKind.Object)
        {
            return;
        }

        ValidateRawLocationResponseArray(
            updates,
            "newLocations",
            "world_map_creation",
            isLink: false,
            "response.worldMapUpdates",
            "worldMapUpdates",
            issues);
        ValidateRawLocationResponseArray(
            updates,
            "newLinks",
            "world_map_link_creation",
            isLink: true,
            "response.worldMapUpdates",
            "worldMapUpdates",
            issues);
    }

    private static void ValidateRawMortalLocationClientOwnedRootFields(
        JsonElement root,
        string context,
        List<ValidationIssue> issues)
    {
        foreach (var property in root.EnumerateObject())
        {
            if (!string.Equals(property.Name, "locationIdentityIndex", StringComparison.OrdinalIgnoreCase) &&
                !string.Equals(property.Name, "linkIdentityIndex", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            issues.Add(new ValidationIssue(
                $"{context}.{property.Name}",
                IssueSeverity.Error,
                "The GM cannot author Mortal location identity-index state.",
                code: "mortal_location_materialization_client_owned_surface_forbidden",
                section: "mortal_location_materialization",
                expected: "client-owned field absent",
                actual: "present",
                repairHint: "Remove the identity-index field; the client creates and updates it after acceptance.",
                category: IssueCategory.ClientOwnedSurface));
        }
    }

    private async Task ValidateAcceptedTurnRawMortalLocationFileAsync(
        string path,
        bool validateCurrentLocation,
        List<ValidationIssue> issues)
    {
        var json = await _fs.ReadFileAsync(path);
        if (string.IsNullOrWhiteSpace(json))
            return;

        try
        {
            using var document = JsonDocument.Parse(json);
            if (document.RootElement.ValueKind != JsonValueKind.Object)
                return;

            if (validateCurrentLocation)
            {
                if (document.RootElement.TryGetProperty("currentLocationData", out var current) &&
                    current.ValueKind == JsonValueKind.Object)
                {
                    ValidateRawCurrentLocationCreation(
                        current,
                        $"{path}.currentLocationData",
                        "currentLocationData",
                        issues);
                }
                return;
            }

            if (!document.RootElement.TryGetProperty("worldMapUpdates", out var updates) ||
                updates.ValueKind != JsonValueKind.Object)
            {
                return;
            }

            ValidateRawLocationResponseArray(
                updates,
                "newLocations",
                "world_map_creation",
                isLink: false,
                $"{path}.worldMapUpdates",
                "worldMapUpdates",
                issues);
            ValidateRawLocationResponseArray(
                updates,
                "newLinks",
                "world_map_link_creation",
                isLink: true,
                $"{path}.worldMapUpdates",
                "worldMapUpdates",
                issues);
        }
        catch (JsonException)
        {
            // JsonIntegrity owns malformed-file diagnostics before this phase.
        }
    }

    private static void ValidateRawCurrentLocationCreation(
        JsonElement current,
        string issuePath,
        string carrierPath,
        List<ValidationIssue> issues)
    {
        if (!IsRawCurrentLocationCreationCandidate(current))
            return;

        var candidateIssues = MortalLocationMaterializationContract.ValidateRawLocation(
            current,
            issuePath,
            "current_scene_creation");
        AttachMortalLocationRepairContexts(
            candidateIssues,
            current,
            issuePath,
            carrierPath,
            "mortal_location");
        issues.AddRange(candidateIssues);
    }

    private static bool IsRawCurrentLocationCreationCandidate(JsonElement current)
    {
        if (current.TryGetProperty("locationId", out var currentIdentity) &&
            currentIdentity.ValueKind == JsonValueKind.Null)
        {
            return true;
        }

        return current.TryGetProperty("initialId", out _) ||
            current.TryGetProperty("materialization", out _);
    }

    private static void ValidateRawLocationResponseArray(
        JsonElement updates,
        string property,
        string route,
        bool isLink,
        string issuePathPrefix,
        string carrierPathPrefix,
        List<ValidationIssue> issues)
    {
        if (!updates.TryGetProperty(property, out var values))
            return;
        if (values.ValueKind != JsonValueKind.Array)
        {
            issues.Add(LocationIssue(
                $"{issuePathPrefix}.{property}",
                "mortal_location_materialization_invalid_root",
                $"{property} must be an array.",
                "array",
                values.ValueKind.ToString()));
            return;
        }

        var index = 0;
        foreach (var value in values.EnumerateArray())
        {
            var context = $"{issuePathPrefix}.{property}[{index}]";
            var candidateIssues = isLink
                ? MortalLocationMaterializationContract.ValidateRawLink(value, context, route)
                : MortalLocationMaterializationContract.ValidateRawLocation(value, context, route);
            AttachMortalLocationRepairContexts(
                candidateIssues,
                value,
                context,
                $"{carrierPathPrefix}.{property}[{index}]",
                isLink ? "mortal_location_link" : "mortal_location");
            issues.AddRange(candidateIssues);
            index++;
        }
    }

    private static void AttachMortalLocationRepairContexts(
        IReadOnlyList<ValidationIssue> candidateIssues,
        JsonElement candidate,
        string issuePath,
        string carrierPath,
        string entityKind)
    {
        if (candidateIssues.Count == 0)
            return;

        var repairableFields = candidateIssues
            .Where(issue => IsRepairableMortalLocationField(issue, issuePath))
            .Select(issue => ReadRelativeMortalLocationIssuePath(issue.FilePath, issuePath))
            .Distinct(StringComparer.Ordinal)
            .OrderBy(static field => field, StringComparer.Ordinal)
            .ToArray();
        var context = new MortalLocationRepairContext(
            carrierPath,
            entityKind,
            ReadExactMortalLocationRepairString(candidate, "initialId"),
            candidate.TryGetProperty("materialization", out var envelope) &&
            envelope.ValueKind == JsonValueKind.Object
                ? ReadExactMortalLocationRepairString(envelope, "materializationId")
                : null,
            repairableFields);

        foreach (var issue in candidateIssues)
            issue.MortalLocationRepairContext = context;
    }

    private static bool IsRepairableMortalLocationField(
        ValidationIssue issue,
        string issuePath)
    {
        if (issue.Code == null ||
            issue.Code.Contains("identity", StringComparison.Ordinal) ||
            issue.Code.Contains("receipt", StringComparison.Ordinal) ||
            issue.Code.Contains("seal", StringComparison.Ordinal) ||
            issue.Code.Contains("replay", StringComparison.Ordinal) ||
            issue.Code.Contains("duplicate_creation_route", StringComparison.Ordinal) ||
            issue.Code.Contains("client_owned", StringComparison.Ordinal))
        {
            return false;
        }

        var field = ReadRelativeMortalLocationIssuePath(issue.FilePath, issuePath);
        return field is not "locationId" and
            not "linkId" and
            not "initialId" and
            not "materialization.materializationId" and
            not "materialization.route" and
            not "materializationReceipt" &&
            !field.StartsWith("materializationReceipt.", StringComparison.Ordinal);
    }

    private static string ReadRelativeMortalLocationIssuePath(
        string issueFilePath,
        string issuePath)
    {
        var prefix = issuePath + ".";
        return issueFilePath.StartsWith(prefix, StringComparison.Ordinal)
            ? issueFilePath[prefix.Length..]
            : issueFilePath;
    }

    private static string? ReadExactMortalLocationRepairString(
        JsonElement root,
        string field)
    {
        if (!root.TryGetProperty(field, out var value) || value.ValueKind != JsonValueKind.String)
            return null;
        var text = value.GetString();
        return !string.IsNullOrEmpty(text) && string.Equals(text, text.Trim(), StringComparison.Ordinal)
            ? text
            : null;
    }

    private static JsonObject? ParseLocationStateObject(
        string? json,
        string path,
        List<ValidationIssue> issues)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            issues.Add(LocationIssue(
                path,
                "mortal_location_materialization_missing_state",
                "Mortal location canonical state is missing.",
                "JSON object",
                "missing"));
            return null;
        }

        try
        {
            return JsonNode.Parse(json) as JsonObject ?? throw new JsonException("root is not an object");
        }
        catch (JsonException exception)
        {
            issues.Add(LocationIssue(
                path,
                "mortal_location_materialization_invalid_state_json",
                "Mortal location canonical state is not valid object JSON.",
                "JSON object",
                exception.Message));
            return null;
        }
    }

    private static bool TryReadCanonicalWorldMap(
        JsonObject map,
        out JsonArray locations,
        out JsonArray links)
    {
        locations = map["locations"] as JsonArray ?? new JsonArray();
        links = map["links"] as JsonArray ?? new JsonArray();
        return map.Count == 4 &&
               map["schemaVersion"] is JsonValue schema && schema.TryGetValue<int>(out var version) && version == 1 &&
               map["realm"] is JsonValue realm && realm.TryGetValue<string>(out var realmName) &&
               string.Equals(realmName, "mortal_world", StringComparison.Ordinal) &&
               map["locations"] is JsonArray &&
               map["links"] is JsonArray;
    }

    private static void ValidateCanonicalLocationArray(
        JsonArray values,
        bool links,
        List<ValidationIssue> issues)
    {
        for (var index = 0; index < values.Count; index++)
        {
            var value = values[index];
            if (value == null)
            {
                issues.Add(LocationIssue(
                    $"{MortalLocationMaterializationContract.WorldMapPath}.{(links ? "links" : "locations")}[{index}]",
                    "mortal_location_materialization_invalid_root",
                    "Canonical map members must be objects.",
                    "object",
                    "null"));
                continue;
            }
            using var document = JsonDocument.Parse(value.ToJsonString());
            var context = $"{MortalLocationMaterializationContract.WorldMapPath}.{(links ? "links" : "locations")}[{index}]";
            issues.AddRange(links
                ? MortalLocationMaterializationContract.ValidateCanonicalLink(document.RootElement, context)
                : MortalLocationMaterializationContract.ValidateCanonicalLocation(document.RootElement, context));
        }
    }

    private static void ValidateCurrentLocationProjection(
        JsonObject current,
        JsonArray locations,
        List<ValidationIssue> issues)
    {
        using var document = JsonDocument.Parse(current.ToJsonString());
        issues.AddRange(MortalLocationMaterializationContract.ValidateCanonicalLocation(
            document.RootElement,
            MortalLocationMaterializationContract.CurrentLocationPath));
        if (!TryReadExactString(current, "locationId", out var currentId))
            return;

        var matches = locations.OfType<JsonObject>()
            .Where(location => TryReadExactString(location, "locationId", out var locationId) &&
                               string.Equals(locationId, currentId, StringComparison.Ordinal))
            .ToArray();
        if (matches.Length != 1)
        {
            issues.Add(LocationIssue(
                MortalLocationMaterializationContract.CurrentLocationPath,
                "mortal_location_materialization_current_projection_mismatch",
                "Current location must resolve exactly once in canonical world map.",
                "one exact map location",
                currentId));
            return;
        }

        foreach (var field in CurrentProjectionSharedFields)
        {
            if (JsonNode.DeepEquals(current[field], matches[0][field]))
                continue;
            issues.Add(LocationIssue(
                $"{MortalLocationMaterializationContract.CurrentLocationPath}.{field}",
                "mortal_location_materialization_current_projection_mismatch",
                "Current projection shared semantics must equal the canonical map location.",
                matches[0][field]?.ToJsonString() ?? "null",
                current[field]?.ToJsonString() ?? "missing"));
        }
    }

    private static readonly string[] CurrentProjectionSharedFields =
    {
        "locationId",
        "realm",
        "name",
        "displayName",
        "purpose",
        "description",
        "image_prompt",
        "locationType",
        "biome",
        "biomeDescription",
        "indoorType",
        "features",
        "region",
        "parentLocationId",
        "coordinates",
        "discovery",
        "internalDifficulty",
        "externalDifficulty",
        "lastEventsDescription",
        "eventDescriptions",
        "factionControl",
        "actorBindings",
        "activeThreats",
        "loreBindings",
        "customStates",
        "materialization",
        "materializationReceipt"
    };

    private static bool TryReadExactString(JsonObject root, string field, out string value)
    {
        value = string.Empty;
        if (root[field] is not JsonValue node || !node.TryGetValue<string>(out var text) ||
            string.IsNullOrEmpty(text) || !string.Equals(text, text.Trim(), StringComparison.Ordinal))
        {
            return false;
        }
        value = text;
        return true;
    }

    private static ValidationIssue LocationIssue(
        string path,
        string code,
        string message,
        string expected,
        string actual) =>
        new(
            path,
            IssueSeverity.Error,
            message,
            code: code,
            section: "mortal_location_materialization",
            expected: expected,
            actual: actual,
            repairHint: "Restore client-owned state or correct the exact GM-owned location carrier.");
}
