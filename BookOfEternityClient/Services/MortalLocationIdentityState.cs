using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace BookOfEternityClient.Services;

internal sealed class MortalLocationIdentityFactory
{
    private readonly Func<Guid> _guidFactory;

    internal MortalLocationIdentityFactory(Func<Guid>? guidFactory = null)
    {
        _guidFactory = guidFactory ?? Guid.NewGuid;
    }

    internal string CreateLocationId() => "loc_" + Next();

    internal string CreateLocationReceiptId() => "mlocrec_" + Next();

    internal string CreateLinkId() => "lnk_" + Next();

    internal string CreateLinkReceiptId() => "mlinkrec_" + Next();

    internal string CreateTransitionId() => "mltrn_" + Next();

    internal string CreateThreatId() => "threat_" + Next();

    private string Next() => _guidFactory().ToString("N");
}

internal sealed class MortalLocationIdentityState
{
    internal const string StatePath = "game_state/world/location_identity_index.json";
    internal const int SchemaVersion = 1;

    private static readonly string[] RootFieldOrder =
    {
        "schemaVersion",
        "realm",
        "locationEntries",
        "linkEntries"
    };

    private static readonly string[] LocationEntryFieldOrder =
    {
        "locationId",
        "initialId",
        "materializationId",
        "receiptId",
        "realm",
        "route",
        "sourceTurn",
        "sourceAuthorityKind",
        "sourceAuthorityId",
        "coordinatesAtCreation",
        "state",
        "transitions"
    };

    private static readonly string[] LinkEntryFieldOrder =
    {
        "linkId",
        "initialId",
        "materializationId",
        "receiptId",
        "realm",
        "route",
        "sourceTurn",
        "sourceAuthorityKind",
        "sourceAuthorityId",
        "sourceLocationId",
        "targetLocationId",
        "state",
        "transitions"
    };

    private static readonly HashSet<string> RootFields = new(RootFieldOrder, StringComparer.Ordinal);
    private static readonly HashSet<string> LocationEntryFields = new(LocationEntryFieldOrder, StringComparer.Ordinal);
    private static readonly HashSet<string> LinkEntryFields = new(LinkEntryFieldOrder, StringComparer.Ordinal);
    private static readonly HashSet<string> CoordinatesFields = new(StringComparer.Ordinal) { "x", "y", "z" };
    private static readonly HashSet<string> EntryStates = new(StringComparer.Ordinal) { "active", "retired" };
    private static readonly HashSet<string> TransitionFields = new(StringComparer.Ordinal)
    {
        "transitionId",
        "kind",
        "turn",
        "entityId",
        "beforeState",
        "afterState",
        "sourceAuthorityKind",
        "sourceAuthorityId",
        "operationRef",
        "sourceLocationId",
        "targetLocationId"
    };
    private static readonly HashSet<string> ChildTransitionFields = new(
        TransitionFields.Append("childId"),
        StringComparer.Ordinal);
    private static readonly HashSet<string> LocationTransitionKinds = new(StringComparer.Ordinal)
    {
        "location_update",
        "location_discovery",
        "current_selection"
    };
    private static readonly HashSet<string> LocationChildTransitionKinds = new(StringComparer.Ordinal)
    {
        "storage_update",
        "storage_removal",
        "threat_addition",
        "threat_update",
        "threat_removal",
        "threat_activity_completion"
    };
    private static readonly HashSet<string> LinkTransitionKinds = new(StringComparer.Ordinal)
    {
        "link_update",
        "link_retirement"
    };
    private static readonly HashSet<string> LocationRoutes = new(StringComparer.Ordinal)
    {
        "current_scene_creation",
        "world_map_creation"
    };

    private readonly JsonObject _root;
    private readonly HashSet<string> _retiredLocationIdKeys;
    private readonly HashSet<string> _locationOriginKeys;
    private readonly HashSet<string> _linkOriginKeys;

    private MortalLocationIdentityState(
        JsonObject root,
        IReadOnlyDictionary<string, JsonObject> locationEntriesById,
        IReadOnlyDictionary<string, JsonObject> linkEntriesById,
        IReadOnlyList<ValidationIssue> issues,
        HashSet<string> retiredLocationIdKeys,
        HashSet<string> locationOriginKeys,
        HashSet<string> linkOriginKeys,
        int entriesScanned)
    {
        _root = root;
        LocationEntriesById = locationEntriesById;
        LinkEntriesById = linkEntriesById;
        Issues = issues;
        _retiredLocationIdKeys = retiredLocationIdKeys;
        _locationOriginKeys = locationOriginKeys;
        _linkOriginKeys = linkOriginKeys;
        EntriesScanned = entriesScanned;
    }

    internal IReadOnlyDictionary<string, JsonObject> LocationEntriesById { get; }

    internal IReadOnlyDictionary<string, JsonObject> LinkEntriesById { get; }

    internal IReadOnlyList<ValidationIssue> Issues { get; }

    internal int EntriesScanned { get; }

    internal static JsonObject CreateEmptyRoot() =>
        new()
        {
            ["schemaVersion"] = SchemaVersion,
            ["realm"] = "mortal_world",
            ["locationEntries"] = new JsonArray(),
            ["linkEntries"] = new JsonArray()
        };

    internal static MortalLocationIdentityState Parse(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
            return InvalidState("missing or blank index JSON");

        try
        {
            using var document = JsonDocument.Parse(json);
            var duplicateIssues = new List<ValidationIssue>();
            CollectDuplicateProperties(document.RootElement, StatePath, duplicateIssues);
            var node = ConvertElement(document.RootElement);
            return ParseCore(node, duplicateIssues);
        }
        catch (JsonException exception)
        {
            return InvalidState(exception.Message);
        }
    }

    internal static MortalLocationIdentityState Parse(JsonNode? node) =>
        ParseCore(node, Array.Empty<ValidationIssue>());

    internal bool ContainsHistoricalLocationOrigin(string? initialId, string? materializationId) =>
        MatchesHistoricalOrigin(_locationOriginKeys, initialId) ||
        MatchesHistoricalOrigin(_locationOriginKeys, materializationId);

    internal bool ContainsRetiredLocationId(string? locationId) =>
        MatchesHistoricalOrigin(_retiredLocationIdKeys, locationId);

    internal bool ContainsHistoricalLinkOrigin(string? initialId, string? materializationId) =>
        MatchesHistoricalOrigin(_linkOriginKeys, initialId) ||
        MatchesHistoricalOrigin(_linkOriginKeys, materializationId);

    internal JsonObject ToJson() => _root.DeepClone().AsObject();

    internal bool IsAcceptedCanonicalLocation(JsonObject location)
    {
        ArgumentNullException.ThrowIfNull(location);
        return TryGetExactIdentity(location, "locationId", out var locationId) &&
               LocationEntriesById.TryGetValue(locationId, out var entry) &&
               EntryMatchesCanonical(location, entry, isLink: false);
    }

    internal bool IsAcceptedCanonicalLink(JsonObject link)
    {
        ArgumentNullException.ThrowIfNull(link);
        return TryGetExactIdentity(link, "linkId", out var linkId) &&
               LinkEntriesById.TryGetValue(linkId, out var entry) &&
               EntryMatchesCanonical(link, entry, isLink: true);
    }

    internal IReadOnlyList<ValidationIssue> ValidateCanonicalState(JsonNode? worldMapNode)
    {
        var issues = new List<ValidationIssue>();
        if (worldMapNode is not JsonObject worldMap ||
            worldMap["locations"] is not JsonArray locations ||
            worldMap["links"] is not JsonArray links)
        {
            issues.Add(Issue(
                MortalLocationMaterializationContract.WorldMapPath,
                "mortal_location_identity_canonical_mismatch",
                "Canonical world map must expose location and link arrays for identity reconciliation.",
                "world map object with locations[] and links[]",
                worldMapNode?.GetType().Name ?? "missing"));
            return issues;
        }

        var seenLocations = new HashSet<string>(StringComparer.Ordinal);
        foreach (var locationNode in locations)
        {
            if (locationNode is not JsonObject location ||
                !TryGetExactIdentity(location, "locationId", out var locationId) ||
                !seenLocations.Add(locationId) ||
                !LocationEntriesById.TryGetValue(locationId, out var entry) ||
                !EntryMatchesCanonical(location, entry, isLink: false))
            {
                issues.Add(Issue(
                    MortalLocationMaterializationContract.WorldMapPath + ".locations",
                    "mortal_location_identity_canonical_mismatch",
                    "Every canonical location must match exactly one active identity-index entry and receipt.",
                    "one exact active location entry",
                    locationNode?.ToJsonString() ?? "null"));
            }
        }

        var seenLinks = new HashSet<string>(StringComparer.Ordinal);
        foreach (var linkNode in links)
        {
            if (linkNode is not JsonObject link ||
                !TryGetExactIdentity(link, "linkId", out var linkId) ||
                !seenLinks.Add(linkId) ||
                !LinkEntriesById.TryGetValue(linkId, out var entry) ||
                !EntryMatchesCanonical(link, entry, isLink: true))
            {
                issues.Add(Issue(
                    MortalLocationMaterializationContract.WorldMapPath + ".links",
                    "mortal_location_identity_canonical_mismatch",
                    "Every canonical link must match exactly one active identity-index entry and receipt.",
                    "one exact active link entry",
                    linkNode?.ToJsonString() ?? "null"));
            }
        }

        foreach (var pair in LocationEntriesById)
        {
            if (ReadExactString(pair.Value, "state") == "active" && !seenLocations.Contains(pair.Key))
            {
                issues.Add(Issue(
                    StatePath + ".locationEntries",
                    "mortal_location_identity_canonical_mismatch",
                    "An active location index entry requires one canonical map carrier.",
                    pair.Key,
                    "missing"));
            }
        }

        foreach (var pair in LinkEntriesById)
        {
            if (ReadExactString(pair.Value, "state") == "active" && !seenLinks.Contains(pair.Key))
            {
                issues.Add(Issue(
                    StatePath + ".linkEntries",
                    "mortal_location_identity_canonical_mismatch",
                    "An active link index entry requires one canonical map carrier.",
                    pair.Key,
                    "missing"));
            }
        }

        return issues;
    }

    private static MortalLocationIdentityState ParseCore(
        JsonNode? node,
        IReadOnlyCollection<ValidationIssue> initialIssues)
    {
        var issues = new List<ValidationIssue>(initialIssues);
        if (node is not JsonObject root)
            return InvalidState(node == null ? "missing" : "non-object root", issues);

        ValidateExactFields(root, RootFields, StatePath, issues);
        if (!TryGetInt(root, "schemaVersion", out var schemaVersion) || schemaVersion != SchemaVersion ||
            !string.Equals(ReadExactString(root, "realm"), "mortal_world", StringComparison.Ordinal))
        {
            issues.Add(Issue(
                StatePath,
                "mortal_location_identity_invalid_index",
                "Mortal location identity index has an unsupported root shape.",
                "schemaVersion=1; realm=mortal_world",
                root.ToJsonString()));
        }

        var locationEntries = root["locationEntries"] as JsonArray;
        var linkEntries = root["linkEntries"] as JsonArray;
        if (locationEntries == null || linkEntries == null)
        {
            issues.Add(Issue(
                StatePath,
                "mortal_location_identity_invalid_index",
                "Mortal location identity index requires physical locationEntries/linkEntries arrays.",
                "locationEntries[] and linkEntries[]",
                root.ToJsonString()));
        }

        var locationById = new Dictionary<string, JsonObject>(StringComparer.Ordinal);
        var linkById = new Dictionary<string, JsonObject>(StringComparer.Ordinal);
        var locationIds = new HashSet<string>(StringComparer.Ordinal);
        var linkIds = new HashSet<string>(StringComparer.Ordinal);
        var receiptIds = new HashSet<string>(StringComparer.Ordinal);
        var locationOriginsExact = new HashSet<string>(StringComparer.Ordinal);
        var linkOriginsExact = new HashSet<string>(StringComparer.Ordinal);
        var locationOriginKeys = new HashSet<string>(StringComparer.Ordinal);
        var linkOriginKeys = new HashSet<string>(StringComparer.Ordinal);
        var transitionIds = new HashSet<string>(StringComparer.Ordinal);
        var transitionIdKeys = new HashSet<string>(StringComparer.Ordinal);
        var scanned = 0;

        if (locationEntries != null)
        {
            for (var index = 0; index < locationEntries.Count; index++)
            {
                scanned++;
                if (locationEntries[index] is not JsonObject entry)
                {
                    issues.Add(InvalidEntry($"{StatePath}.locationEntries[{index}]", "non-object entry"));
                    continue;
                }
                ValidateEntry(
                    entry,
                    $"{StatePath}.locationEntries[{index}]",
                    isLink: false,
                    locationIds,
                    receiptIds,
                    locationOriginsExact,
                    locationOriginKeys,
                    transitionIds,
                    transitionIdKeys,
                    issues);
                if (TryGetExactIdentity(entry, "locationId", out var locationId) &&
                    !locationById.ContainsKey(locationId))
                {
                    locationById[locationId] = entry.DeepClone().AsObject();
                }
            }
        }

        if (linkEntries != null)
        {
            for (var index = 0; index < linkEntries.Count; index++)
            {
                scanned++;
                if (linkEntries[index] is not JsonObject entry)
                {
                    issues.Add(InvalidEntry($"{StatePath}.linkEntries[{index}]", "non-object entry"));
                    continue;
                }
                ValidateEntry(
                    entry,
                    $"{StatePath}.linkEntries[{index}]",
                    isLink: true,
                    linkIds,
                    receiptIds,
                    linkOriginsExact,
                    linkOriginKeys,
                    transitionIds,
                    transitionIdKeys,
                    issues);
                if (TryGetExactIdentity(entry, "linkId", out var linkId) &&
                    !linkById.ContainsKey(linkId))
                {
                    linkById[linkId] = entry.DeepClone().AsObject();
                }
            }
        }

        var normalizedRoot = new JsonObject
        {
            ["schemaVersion"] = SchemaVersion,
            ["realm"] = "mortal_world",
            ["locationEntries"] = new JsonArray(
                locationById.Values
                    .OrderBy(static entry => ReadExactString(entry, "locationId"), StringComparer.Ordinal)
                    .Select(static entry => (JsonNode?)NormalizeEntry(entry, isLink: false))
                    .ToArray()),
            ["linkEntries"] = new JsonArray(
                linkById.Values
                    .OrderBy(static entry => ReadExactString(entry, "linkId"), StringComparer.Ordinal)
                    .Select(static entry => (JsonNode?)NormalizeEntry(entry, isLink: true))
                    .ToArray())
        };
        var retiredLocationIdKeys = locationById.Values
            .Where(static entry =>
                string.Equals(
                    ReadExactString(entry, "state"),
                    "retired",
                    StringComparison.Ordinal))
            .Select(static entry => ReadExactString(entry, "locationId"))
            .Where(static value => value != null)
            .Cast<string>()
            .Select(BuildConfusableKey)
            .ToHashSet(StringComparer.Ordinal);

        return new MortalLocationIdentityState(
            normalizedRoot,
            locationById,
            linkById,
            issues,
            retiredLocationIdKeys,
            locationOriginKeys,
            linkOriginKeys,
            scanned);
    }

    private static void ValidateEntry(
        JsonObject entry,
        string path,
        bool isLink,
        HashSet<string> entityIds,
        HashSet<string> receiptIds,
        HashSet<string> originsExact,
        HashSet<string> originKeys,
        HashSet<string> transitionIds,
        HashSet<string> transitionIdKeys,
        List<ValidationIssue> issues)
    {
        ValidateExactFields(entry, isLink ? LinkEntryFields : LocationEntryFields, path, issues);
        var identityField = isLink ? "linkId" : "locationId";
        var requiredIdentityFields = isLink
            ? new[]
            {
                "linkId", "initialId", "materializationId", "receiptId", "sourceAuthorityKind",
                "sourceAuthorityId", "sourceLocationId", "targetLocationId"
            }
            : new[]
            {
                "locationId", "initialId", "materializationId", "receiptId", "sourceAuthorityKind",
                "sourceAuthorityId"
            };

        var valid = true;
        foreach (var field in requiredIdentityFields)
        {
            if (TryGetExactIdentity(entry, field, out _))
                continue;
            issues.Add(InvalidEntry($"{path}.{field}", "missing or non-exact identity"));
            valid = false;
        }

        var route = ReadExactString(entry, "route");
        var hasSourceTurn = TryGetInt(entry, "sourceTurn", out var sourceTurn);
        if (!string.Equals(ReadExactString(entry, "realm"), "mortal_world", StringComparison.Ordinal) ||
            route == null || isLink && route != "world_map_link_creation" ||
            !isLink && !LocationRoutes.Contains(route) ||
            !hasSourceTurn || sourceTurn < 1 ||
            !EntryStates.Contains(ReadExactString(entry, "state") ?? string.Empty) ||
            entry["transitions"] is not JsonArray)
        {
            issues.Add(InvalidEntry(path, "invalid realm, route, source turn, lifecycle state, or transitions"));
            valid = false;
        }

        if (!isLink && !ValidateCoordinates(entry["coordinatesAtCreation"]))
        {
            issues.Add(InvalidEntry($"{path}.coordinatesAtCreation", "invalid creation coordinates"));
            valid = false;
        }

        if (!valid)
            return;

        var identity = ReadExactString(entry, identityField)!;
        if (!entityIds.Add(identity))
        {
            issues.Add(Issue(
                $"{path}.{identityField}",
                isLink
                    ? "mortal_location_identity_duplicate_link_id"
                    : "mortal_location_identity_duplicate_location_id",
                "Permanent location/link IDs must be unique across active and retired history.",
                "unique exact identity",
                identity));
        }

        var receiptId = ReadExactString(entry, "receiptId")!;
        if (!receiptIds.Add(receiptId))
        {
            issues.Add(Issue(
                $"{path}.receiptId",
                "mortal_location_identity_duplicate_receipt_id",
                "Location/link receipt IDs must be globally unique.",
                "unique exact receiptId",
                receiptId));
        }

        foreach (var originField in new[] { "initialId", "materializationId" })
        {
            var origin = ReadExactString(entry, originField)!;
            var confusableKey = BuildConfusableKey(origin);
            if (!originsExact.Add(origin) || !originKeys.Add(confusableKey))
            {
                issues.Add(Issue(
                    $"{path}.{originField}",
                    "mortal_location_identity_duplicate_origin",
                    "Origin identity evidence cannot be reused or represented by a confusable alias.",
                    "globally unique exact/confusable origin",
                    origin));
            }
        }

        ValidateTransitions(
            entry["transitions"]!.AsArray(),
            path,
            isLink,
            identity,
            sourceTurn,
            isLink ? ReadExactString(entry, "sourceLocationId") : null,
            isLink ? ReadExactString(entry, "targetLocationId") : null,
            transitionIds,
            transitionIdKeys,
            issues);
    }

    private static void ValidateTransitions(
        JsonArray transitions,
        string entryPath,
        bool isLink,
        string entityId,
        int sourceTurn,
        string? sourceLocationId,
        string? targetLocationId,
        HashSet<string> transitionIds,
        HashSet<string> transitionIdKeys,
        List<ValidationIssue> issues)
    {
        for (var index = 0; index < transitions.Count; index++)
        {
            var path = $"{entryPath}.transitions[{index}]";
            if (transitions[index] is not JsonObject transition)
            {
                issues.Add(InvalidEntry(path, "non-object lifecycle transition"));
                continue;
            }

            var kind = ReadExactString(transition, "kind");
            var isChildTransition = kind != null &&
                                    LocationChildTransitionKinds.Contains(kind);
            var expectedFields = isChildTransition
                ? ChildTransitionFields
                : TransitionFields;
            var fieldsAreExact = transition
                .Select(static pair => pair.Key)
                .ToHashSet(StringComparer.Ordinal)
                .SetEquals(expectedFields);

            var transitionId = ReadExactString(transition, "transitionId");
            if (transitionId != null)
            {
                var exactUnique = transitionIds.Add(transitionId);
                var confusableUnique = transitionIdKeys.Add(BuildConfusableKey(transitionId));
                if (!exactUnique || !confusableUnique)
                {
                    issues.Add(Issue(
                        $"{path}.transitionId",
                        "mortal_location_identity_duplicate_transition_id",
                        "Lifecycle transition IDs must be globally unique across location and link history.",
                        "globally unique exact/confusable transitionId",
                        transitionId));
                }
            }

            var hasTurn = TryGetInt(transition, "turn", out var turn);
            var sourceAuthorityId = ReadExactString(transition, "sourceAuthorityId");
            var valid = fieldsAreExact &&
                        transitionId != null &&
                        kind != null &&
                        hasTurn &&
                        turn >= sourceTurn &&
                        string.Equals(
                            ReadExactString(transition, "entityId"),
                            entityId,
                            StringComparison.Ordinal) &&
                        transition["beforeState"] is JsonObject &&
                        transition["afterState"] is JsonObject &&
                        string.Equals(
                            ReadExactString(transition, "sourceAuthorityKind"),
                            "turn_outcome",
                            StringComparison.Ordinal) &&
                        string.Equals(
                            sourceAuthorityId,
                            "turn_" + turn,
                            StringComparison.Ordinal) &&
                        ReadExactString(transition, "operationRef") != null;

            if (isLink)
            {
                valid = valid &&
                        LinkTransitionKinds.Contains(kind ?? string.Empty) &&
                        string.Equals(
                            ReadExactString(transition, "sourceLocationId"),
                            sourceLocationId,
                            StringComparison.Ordinal) &&
                        string.Equals(
                            ReadExactString(transition, "targetLocationId"),
                            targetLocationId,
                            StringComparison.Ordinal);
            }
            else if (isChildTransition)
            {
                valid = valid &&
                        TryGetExactIdentity(transition, "childId", out _) &&
                        string.Equals(
                            ReadExactString(transition, "sourceLocationId"),
                            entityId,
                            StringComparison.Ordinal) &&
                        string.Equals(
                            ReadExactString(transition, "targetLocationId"),
                            entityId,
                            StringComparison.Ordinal);
            }
            else
            {
                valid = valid &&
                        LocationTransitionKinds.Contains(kind ?? string.Empty) &&
                        transition["sourceLocationId"] == null &&
                        transition["targetLocationId"] == null;
            }

            if (!valid)
            {
                issues.Add(InvalidEntry(
                    path,
                    "incomplete, inconsistent, or non-exact lifecycle transition evidence"));
            }
        }
    }

    private static bool EntryMatchesCanonical(JsonObject canonical, JsonObject entry, bool isLink)
    {
        if (!string.Equals(ReadExactString(entry, "state"), "active", StringComparison.Ordinal) ||
            canonical[MortalLocationMaterializationContract.ReceiptProperty] is not JsonObject receipt ||
            canonical[MortalLocationMaterializationContract.EnvelopeProperty] is not JsonObject envelope)
        {
            return false;
        }

        var identityField = isLink ? "linkId" : "locationId";
        var pairs = new[]
        {
            (canonical, identityField, entry, identityField),
            (receipt, "initialId", entry, "initialId"),
            (receipt, "materializationId", entry, "materializationId"),
            (receipt, "receiptId", entry, "receiptId"),
            (receipt, "realm", entry, "realm"),
            (receipt, "route", entry, "route"),
            (receipt, "sourceTurn", entry, "sourceTurn"),
            (receipt, "sourceAuthorityKind", entry, "sourceAuthorityKind"),
            (receipt, "sourceAuthorityId", entry, "sourceAuthorityId"),
            (envelope, "initialId", entry, "initialId"),
            (envelope, "materializationId", entry, "materializationId")
        };
        if (pairs.Any(static pair => !NodeValuesEqual(pair.Item1[pair.Item2], pair.Item3[pair.Item4])))
            return false;

        return !isLink ||
               NodeValuesEqual(canonical["sourceLocationId"], entry["sourceLocationId"]) &&
               NodeValuesEqual(canonical["targetLocationId"], entry["targetLocationId"]);
    }

    private static bool NodeValuesEqual(JsonNode? left, JsonNode? right) =>
        left != null && right != null && JsonNode.DeepEquals(left, right);

    private static bool MatchesHistoricalOrigin(HashSet<string> historicalKeys, string? candidate)
    {
        return !string.IsNullOrEmpty(candidate) && historicalKeys.Contains(BuildConfusableKey(candidate));
    }

    internal static string BuildConfusableKey(string value)
    {
        string normalized;
        try
        {
            normalized = value.Trim().Normalize(NormalizationForm.FormKC).ToUpper(CultureInfo.InvariantCulture);
        }
        catch (ArgumentException)
        {
            normalized = value.Trim().ToUpper(CultureInfo.InvariantCulture);
        }

        var result = new StringBuilder(normalized.Length);
        foreach (var character in normalized)
        {
            result.Append(character switch
            {
                '\u0410' or '\u0391' => 'A',
                '\u0412' or '\u0392' => 'B',
                '\u0421' or '\u03F9' => 'C',
                '\u0415' or '\u0395' => 'E',
                '\u041D' or '\u0397' => 'H',
                '\u0406' or '\u0399' => 'I',
                '\u041A' or '\u039A' => 'K',
                '\u041C' or '\u039C' => 'M',
                '\u041E' or '\u039F' => 'O',
                '\u0420' or '\u03A1' => 'P',
                '\u0422' or '\u03A4' => 'T',
                '\u0425' or '\u03A7' => 'X',
                '\u0423' or '\u03A5' => 'Y',
                '\u0408' => 'J',
                '\u0405' => 'S',
                '\u04C0' => 'I',
                _ => character
            });
        }
        return result.ToString();
    }

    private static bool ValidateCoordinates(JsonNode? node)
    {
        if (node is not JsonObject coordinates ||
            coordinates.Select(static pair => pair.Key).ToHashSet(StringComparer.Ordinal)
                .SetEquals(CoordinatesFields) == false)
        {
            return false;
        }

        return CoordinatesFields.All(field => TryGetInt(coordinates, field, out _));
    }

    private static void ValidateExactFields(
        JsonObject value,
        IReadOnlySet<string> expectedFields,
        string path,
        List<ValidationIssue> issues)
    {
        foreach (var pair in value)
        {
            if (expectedFields.Contains(pair.Key))
                continue;
            issues.Add(Issue(
                $"{path}.{pair.Key}",
                "mortal_location_identity_unknown_field",
                "Client-owned location identity state has an unknown field.",
                string.Join(',', expectedFields.OrderBy(static field => field, StringComparer.Ordinal)),
                pair.Key));
        }

        foreach (var field in expectedFields)
        {
            if (value.ContainsKey(field))
                continue;
            issues.Add(InvalidEntry($"{path}.{field}", "missing required field"));
        }
    }

    private static JsonObject NormalizeEntry(JsonObject entry, bool isLink)
    {
        var result = new JsonObject();
        foreach (var field in isLink ? LinkEntryFieldOrder : LocationEntryFieldOrder)
            result[field] = entry[field]?.DeepClone();
        return result;
    }

    private static MortalLocationIdentityState InvalidState(
        string actual,
        IReadOnlyCollection<ValidationIssue>? existingIssues = null)
    {
        var issues = existingIssues?.ToList() ?? new List<ValidationIssue>();
        issues.Add(Issue(
            StatePath,
            "mortal_location_identity_invalid_index",
            "Mortal location identity index is missing or malformed.",
            "current identity-index object",
            actual));
        return new MortalLocationIdentityState(
            CreateEmptyRoot(),
            new Dictionary<string, JsonObject>(StringComparer.Ordinal),
            new Dictionary<string, JsonObject>(StringComparer.Ordinal),
            issues,
            new HashSet<string>(StringComparer.Ordinal),
            new HashSet<string>(StringComparer.Ordinal),
            new HashSet<string>(StringComparer.Ordinal),
            0);
    }

    private static ValidationIssue InvalidEntry(string path, string actual) =>
        Issue(
            path,
            "mortal_location_identity_invalid_entry",
            "A location identity-index entry is malformed.",
            "complete exact entry",
            actual);

    private static ValidationIssue Issue(
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
            section: "mortal_location_identity",
            expected: expected,
            actual: actual,
            repairHint: "Restore client-owned identity state from the validated pending snapshot.");

    private static bool TryGetExactIdentity(JsonObject root, string field, out string identity)
    {
        identity = ReadExactString(root, field) ?? string.Empty;
        return identity.Length > 0;
    }

    private static string? ReadExactString(JsonObject root, string field)
    {
        if (root[field] is not JsonValue value || !value.TryGetValue<string>(out var text) ||
            string.IsNullOrEmpty(text) || !string.Equals(text, text.Trim(), StringComparison.Ordinal))
        {
            return null;
        }
        return text;
    }

    private static bool TryGetInt(JsonObject root, string field, out int result)
    {
        result = default;
        return root[field] is JsonValue value && value.TryGetValue<int>(out result);
    }

    private static void CollectDuplicateProperties(
        JsonElement value,
        string path,
        List<ValidationIssue> issues)
    {
        if (value.ValueKind == JsonValueKind.Object)
        {
            var fields = new HashSet<string>(StringComparer.Ordinal);
            foreach (var property in value.EnumerateObject())
            {
                var propertyPath = $"{path}.{property.Name}";
                if (!fields.Add(property.Name))
                {
                    issues.Add(Issue(
                        propertyPath,
                        "mortal_location_identity_duplicate_property",
                        "Duplicate JSON properties are forbidden in client-owned location identity state.",
                        "unique exact property names",
                        property.Name));
                }
                CollectDuplicateProperties(property.Value, propertyPath, issues);
            }
            return;
        }

        if (value.ValueKind != JsonValueKind.Array)
            return;
        var index = 0;
        foreach (var element in value.EnumerateArray())
        {
            CollectDuplicateProperties(element, $"{path}[{index}]", issues);
            index++;
        }
    }

    private static JsonNode? ConvertElement(JsonElement value) =>
        value.ValueKind switch
        {
            JsonValueKind.Object => ConvertObject(value),
            JsonValueKind.Array => new JsonArray(value.EnumerateArray().Select(ConvertElement).ToArray()),
            JsonValueKind.String => JsonValue.Create(value.GetString()),
            JsonValueKind.Number when value.TryGetInt32(out var integer) => JsonValue.Create(integer),
            JsonValueKind.Number when value.TryGetInt64(out var longInteger) => JsonValue.Create(longInteger),
            JsonValueKind.Number => JsonValue.Create(value.GetDouble()),
            JsonValueKind.True => JsonValue.Create(true),
            JsonValueKind.False => JsonValue.Create(false),
            JsonValueKind.Null => null,
            _ => null
        };

    private static JsonObject ConvertObject(JsonElement value)
    {
        var result = new JsonObject();
        foreach (var property in value.EnumerateObject())
        {
            result.Remove(property.Name);
            result[property.Name] = ConvertElement(property.Value);
        }
        return result;
    }
}
