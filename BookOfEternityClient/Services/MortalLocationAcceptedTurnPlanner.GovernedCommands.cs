using System.Text.Json;
using System.Text.Json.Nodes;

namespace BookOfEternityClient.Services;

internal static partial class MortalLocationAcceptedTurnPlanner
{
    private static readonly HashSet<string> WorldMapCommandFields = new(StringComparer.Ordinal)
    {
        "newLocations",
        "newLinks",
        "locationUpdates",
        "locationDiscoveryTransitions",
        "storageUpdates",
        "storagesToRemove",
        "linkUpdates",
        "linkRemovals",
        "threatsToAdd",
        "threatsToUpdate",
        "threatsToRemove",
        "completeThreatActivities"
    };

    private static readonly HashSet<string> StorageUpdateFields = new(StringComparer.Ordinal)
    {
        "targetLocationId",
        "storageId",
        "update"
    };

    private static readonly HashSet<string> StoragePatchFields = new(StringComparer.Ordinal)
    {
        "newName",
        "newDescription",
        "newCapacity",
        "newOwner"
    };

    private static readonly HashSet<string> StorageRemovalFields = new(StringComparer.Ordinal)
    {
        "targetLocationId",
        "storageId"
    };

    private static readonly HashSet<string> ThreatAdditionFields = new(StringComparer.Ordinal)
    {
        "targetLocationId",
        "initialTargetLocationId",
        "threat"
    };

    private static readonly HashSet<string> ThreatUpdateFields = new(StringComparer.Ordinal)
    {
        "targetLocationId",
        "threatUpdate"
    };

    private static readonly HashSet<string> ThreatRemovalFields = new(StringComparer.Ordinal)
    {
        "targetLocationId",
        "threatId"
    };

    private static readonly HashSet<string> ThreatCompletionFields = new(StringComparer.Ordinal)
    {
        "targetLocationId",
        "threatId",
        "threatName",
        "finalState",
        "narrativeSummary"
    };

    private static void ValidateWorldMapCommandCatalog(
        JsonObject? updates,
        List<ValidationIssue> issues)
    {
        if (updates == null)
            return;

        foreach (var property in updates)
        {
            if (WorldMapCommandFields.Contains(property.Key))
                continue;

            issues.Add(Issue(
                "worldMapUpdates." + property.Key,
                "mortal_location_world_map_command_unknown",
                "Mortal world-map updates use one closed accepted command catalog.",
                string.Join(',', WorldMapCommandFields.OrderBy(static field => field, StringComparer.Ordinal)),
                property.Key));
        }
    }

    private static GovernedLocationCommands ReadGovernedLocationCommands(
        JsonObject? updates,
        JsonObject? preTurnCurrentLocation,
        IReadOnlyDictionary<MortalLocationStorageKey, JsonArray> preTurnOffscreenContents,
        IReadOnlyDictionary<string, JsonObject> preTurnLocationsById,
        IReadOnlyList<LocationCandidate> locationCandidates,
        List<ValidationIssue> issues)
    {
        var storageUpdates = ReadStorageUpdates(updates, preTurnLocationsById, issues);
        var storageRemovals = ReadStorageRemovals(
            updates,
            preTurnCurrentLocation,
            preTurnOffscreenContents,
            preTurnLocationsById,
            issues);
        var threatAdds = ReadThreatAdditions(
            updates,
            preTurnLocationsById,
            locationCandidates,
            issues);
        var threatUpdates = ReadThreatUpdates(updates, preTurnLocationsById, issues);
        var threatRemovals = ReadThreatRemovals(updates, preTurnLocationsById, issues);
        var threatCompletions = ReadThreatCompletions(updates, preTurnLocationsById, issues);

        ValidateGovernedCommandConflicts(
            storageUpdates,
            storageRemovals,
            threatUpdates,
            threatRemovals,
            threatCompletions,
            issues);
        return new GovernedLocationCommands(
            storageUpdates,
            storageRemovals,
            threatAdds,
            threatUpdates,
            threatRemovals,
            threatCompletions);
    }

    private static List<StorageUpdateCommand> ReadStorageUpdates(
        JsonObject? updates,
        IReadOnlyDictionary<string, JsonObject> preTurnLocationsById,
        List<ValidationIssue> issues)
    {
        var result = new List<StorageUpdateCommand>();
        if (!TryReadOperationArray(updates, "storageUpdates", out var values, issues))
            return result;

        var seen = new HashSet<string>(StringComparer.Ordinal);
        for (var index = 0; index < values.Count; index++)
        {
            var context = $"worldMapUpdates.storageUpdates[{index}]";
            if (values[index] is not JsonObject operation)
            {
                issues.Add(InvalidOperationRoot(context, values[index]));
                continue;
            }

            ValidateExactOperationFields(
                operation,
                StorageUpdateFields,
                context,
                "mortal_location_storage_update_invalid",
                issues);
            var target = ReadGovernedTargetLocation(
                operation,
                context,
                "mortal_location_storage_update_target_unresolved",
                preTurnLocationsById,
                issues);
            var storageId = ReadExactString(operation, "storageId");
            var storage = target == null || storageId == null
                ? null
                : FindExactObject(target, "locationStorages", "storageId", storageId);
            if (storage == null)
            {
                issues.Add(Issue(
                    context + ".storageId",
                    "mortal_location_storage_update_target_unresolved",
                    "A storage update must bind one exact active storage in one exact accepted location.",
                    "exact targetLocationId plus exact storageId",
                    operation["storageId"]?.ToJsonString() ?? "missing"));
            }

            if (operation["update"] is not JsonObject patch)
            {
                issues.Add(InvalidLifecycleShape(
                    context + ".update",
                    "mortal_location_storage_update_invalid",
                    operation["update"]));
                continue;
            }
            ValidateAllowedOperationFields(
                patch,
                StoragePatchFields,
                context + ".update",
                "mortal_location_storage_update_invalid",
                issues);
            if (patch.Count == 0)
            {
                issues.Add(InvalidLifecycleShape(
                    context + ".update",
                    "mortal_location_storage_update_invalid",
                    patch));
            }
            ValidateStoragePatch(patch, context + ".update", issues);

            if (target == null || storageId == null || storage == null)
                continue;
            var key = ReadExactString(target, "locationId") + "\u001f" + storageId;
            if (!seen.Add(MortalLocationIdentityState.BuildConfusableKey(key)))
            {
                issues.Add(Issue(
                    context + ".storageId",
                    "mortal_location_storage_update_target_ambiguous",
                    "A storage may have at most one update in an accepted turn.",
                    "one exact storage update",
                    key));
                continue;
            }
            result.Add(new StorageUpdateCommand(
                ReadExactString(target, "locationId")!,
                storageId,
                patch.DeepClone().AsObject(),
                context));
        }
        return result;
    }

    private static void ValidateStoragePatch(
        JsonObject patch,
        string context,
        List<ValidationIssue> issues)
    {
        foreach (var field in new[] { "newName", "newDescription" })
        {
            if (patch.ContainsKey(field) && ReadExactString(patch, field) == null)
            {
                issues.Add(InvalidLifecycleShape(
                    context + "." + field,
                    "mortal_location_storage_update_invalid",
                    patch[field]));
            }
        }

        if (patch.ContainsKey("newCapacity") &&
            (ReadInt(patch, "newCapacity") is not int capacity || capacity < 0))
        {
            issues.Add(InvalidLifecycleShape(
                context + ".newCapacity",
                "mortal_location_storage_update_invalid",
                patch["newCapacity"]));
        }

        if (patch.ContainsKey("newOwner") && patch["newOwner"] is not (null or JsonObject))
        {
            issues.Add(InvalidLifecycleShape(
                context + ".newOwner",
                "mortal_location_storage_update_invalid",
                patch["newOwner"]));
        }
    }

    private static List<StorageRemovalCommand> ReadStorageRemovals(
        JsonObject? updates,
        JsonObject? preTurnCurrentLocation,
        IReadOnlyDictionary<MortalLocationStorageKey, JsonArray> preTurnOffscreenContents,
        IReadOnlyDictionary<string, JsonObject> preTurnLocationsById,
        List<ValidationIssue> issues)
    {
        var result = new List<StorageRemovalCommand>();
        if (!TryReadOperationArray(updates, "storagesToRemove", out var values, issues))
            return result;

        var seen = new HashSet<string>(StringComparer.Ordinal);
        for (var index = 0; index < values.Count; index++)
        {
            var context = $"worldMapUpdates.storagesToRemove[{index}]";
            if (values[index] is not JsonObject operation)
            {
                issues.Add(InvalidOperationRoot(context, values[index]));
                continue;
            }
            ValidateExactOperationFields(
                operation,
                StorageRemovalFields,
                context,
                "mortal_location_storage_removal_invalid",
                issues);
            var target = ReadGovernedTargetLocation(
                operation,
                context,
                "mortal_location_storage_removal_target_unresolved",
                preTurnLocationsById,
                issues);
            var storageId = ReadExactString(operation, "storageId");
            var storage = target == null || storageId == null
                ? null
                : FindExactObject(target, "locationStorages", "storageId", storageId);
            if (storage == null)
            {
                issues.Add(Issue(
                    context + ".storageId",
                    "mortal_location_storage_removal_target_unresolved",
                    "Storage removal must bind one exact active storage in one exact accepted location.",
                    "exact targetLocationId plus exact storageId",
                    operation["storageId"]?.ToJsonString() ?? "missing"));
                continue;
            }

            var locationId = ReadExactString(target, "locationId")!;
            var key = locationId + "\u001f" + storageId;
            if (!seen.Add(MortalLocationIdentityState.BuildConfusableKey(key)))
            {
                issues.Add(Issue(
                    context + ".storageId",
                    "mortal_location_storage_removal_target_ambiguous",
                    "A storage may be removed at most once in an accepted turn.",
                    "one exact storage removal",
                    key));
                continue;
            }
            if (HasStorageContents(storage) ||
                CurrentProjectionHasStorageContents(preTurnCurrentLocation, locationId, storageId!) ||
                preTurnOffscreenContents.ContainsKey(
                    new MortalLocationStorageKey(locationId, storageId!)))
            {
                issues.Add(Issue(
                    context + ".storageId",
                    "mortal_location_storage_removal_not_empty",
                    "A storage with accepted item contents cannot be removed by location lifecycle authority.",
                    "empty exact storage; move or destroy items through Mortal Item lifecycle first",
                    storageId!));
                continue;
            }
            result.Add(new StorageRemovalCommand(locationId, storageId!, context));
        }
        return result;
    }

    private static bool CurrentProjectionHasStorageContents(
        JsonObject? current,
        string locationId,
        string storageId)
    {
        if (current == null)
            return false;
        var projection = current["currentLocationData"] as JsonObject ?? current;
        return string.Equals(ReadExactString(projection, "locationId"), locationId, StringComparison.Ordinal) &&
               FindExactObject(projection, "locationStorages", "storageId", storageId) is JsonObject storage &&
               HasStorageContents(storage);
    }

    private static bool HasStorageContents(JsonObject storage) =>
        storage.ContainsKey("contents") &&
        (storage["contents"] is not JsonArray contents || contents.Count > 0);

    private static List<ThreatAdditionCommand> ReadThreatAdditions(
        JsonObject? updates,
        IReadOnlyDictionary<string, JsonObject> preTurnLocationsById,
        IReadOnlyList<LocationCandidate> locationCandidates,
        List<ValidationIssue> issues)
    {
        var result = new List<ThreatAdditionCommand>();
        if (!TryReadOperationArray(updates, "threatsToAdd", out var values, issues))
            return result;

        for (var index = 0; index < values.Count; index++)
        {
            var context = $"worldMapUpdates.threatsToAdd[{index}]";
            if (values[index] is not JsonObject operation)
            {
                issues.Add(InvalidOperationRoot(context, values[index]));
                continue;
            }
            ValidateAllowedOperationFields(
                operation,
                ThreatAdditionFields,
                context,
                "mortal_location_threat_add_invalid",
                issues);
            if (!operation.ContainsKey("targetLocationId") || !operation.ContainsKey("threat"))
            {
                issues.Add(InvalidLifecycleShape(
                    context,
                    "mortal_location_threat_add_invalid",
                    operation));
            }
            var targetLocationId = ReadExactString(operation, "targetLocationId");
            var initialTargetLocationId = ReadExactString(operation, "initialTargetLocationId");
            var targetLocationIsNull = operation.ContainsKey("targetLocationId") &&
                                       operation["targetLocationId"] == null;
            var initialTargetIsNull = !operation.ContainsKey("initialTargetLocationId") ||
                                      operation["initialTargetLocationId"] == null;
            var existingTargetValid = targetLocationId != null &&
                                      initialTargetIsNull &&
                                      preTurnLocationsById.ContainsKey(targetLocationId);
            var sameTurnTargetValid = targetLocationIsNull &&
                                      initialTargetLocationId != null &&
                                      locationCandidates.Count(candidate => string.Equals(
                                          ReadExactString(candidate.Raw, "initialId"),
                                          initialTargetLocationId,
                                          StringComparison.Ordinal)) == 1;
            if (!existingTargetValid && !sameTurnTargetValid)
            {
                issues.Add(Issue(
                    context + ".targetLocationId",
                    "mortal_location_threat_add_target_unresolved",
                    "A new threat must bind one exact existing or same-turn location authority.",
                    "exact targetLocationId, or null plus exact same-turn initialTargetLocationId",
                    operation.ToJsonString()));
            }

            if (operation["threat"] is not JsonObject threat ||
                !threat.ContainsKey("threatId") ||
                threat["threatId"] != null ||
                !HasCompleteThreatShape(threat))
            {
                issues.Add(InvalidLifecycleShape(
                    context + ".threat",
                    "mortal_location_threat_add_invalid",
                    operation["threat"]));
                continue;
            }
            if (!existingTargetValid && !sameTurnTargetValid)
                continue;
            result.Add(new ThreatAdditionCommand(
                targetLocationId,
                initialTargetLocationId,
                threat.DeepClone().AsObject(),
                context));
        }
        return result;
    }

    private static bool HasCompleteThreatShape(JsonObject threat) =>
        ReadExactString(threat, "name") != null &&
        ReadInt(threat, "intensity") is int intensity && intensity >= 0 &&
        ReadExactString(threat, "longTermGoal") != null &&
        threat.ContainsKey("currentActivity") &&
        threat["currentActivity"] is null or JsonObject &&
        threat["threatArchetype"] is JsonObject &&
        threat["impactProfile"] is JsonObject;

    private static List<ThreatUpdateCommand> ReadThreatUpdates(
        JsonObject? updates,
        IReadOnlyDictionary<string, JsonObject> preTurnLocationsById,
        List<ValidationIssue> issues)
    {
        var result = new List<ThreatUpdateCommand>();
        if (!TryReadOperationArray(updates, "threatsToUpdate", out var values, issues))
            return result;

        var seen = new HashSet<string>(StringComparer.Ordinal);
        for (var index = 0; index < values.Count; index++)
        {
            var context = $"worldMapUpdates.threatsToUpdate[{index}]";
            if (values[index] is not JsonObject operation)
            {
                issues.Add(InvalidOperationRoot(context, values[index]));
                continue;
            }
            ValidateExactOperationFields(
                operation,
                ThreatUpdateFields,
                context,
                "mortal_location_threat_update_invalid",
                issues);
            var target = ReadGovernedTargetLocation(
                operation,
                context,
                "mortal_location_threat_update_target_unresolved",
                preTurnLocationsById,
                issues);
            if (operation["threatUpdate"] is not JsonObject patch ||
                ReadExactString(patch, "threatId") is not string threatId ||
                patch.Count <= 1 ||
                patch.ContainsKey("currentActivity") &&
                (patch["currentActivity"] is not JsonObject activity || activity.Count == 0 ||
                 ReadExactString(activity, "activeState") is "Completed" or "Abandoned"))
            {
                issues.Add(InvalidLifecycleShape(
                    context + ".threatUpdate",
                    "mortal_location_threat_update_invalid",
                    operation["threatUpdate"]));
                continue;
            }
            var threat = target == null
                ? null
                : FindExactObject(target, "activeThreats", "threatId", threatId);
            if (threat == null)
            {
                issues.Add(Issue(
                    context + ".threatUpdate.threatId",
                    "mortal_location_threat_update_target_unresolved",
                    "A threat update must bind one exact active pre-turn threat.",
                    "exact active threatId in exact targetLocationId",
                    threatId));
                continue;
            }
            var locationId = ReadExactString(target, "locationId")!;
            AddUniqueThreatCommand(seen, locationId, threatId, context, "update", issues);
            var mergedThreat = threat.DeepClone().AsObject();
            MergePatch(mergedThreat, patch, "threatId");
            using var mergedDocument = JsonDocument.Parse(mergedThreat.ToJsonString());
            var mergedIssues = MortalLocationActiveThreatContract.Validate(
                mergedDocument.RootElement,
                context + ".threatUpdate.result");
            if (mergedIssues.Count != 0)
            {
                issues.AddRange(mergedIssues);
                continue;
            }
            result.Add(new ThreatUpdateCommand(locationId, threatId, patch.DeepClone().AsObject(), context));
        }
        return result;
    }

    private static List<ThreatRemovalCommand> ReadThreatRemovals(
        JsonObject? updates,
        IReadOnlyDictionary<string, JsonObject> preTurnLocationsById,
        List<ValidationIssue> issues)
    {
        var result = new List<ThreatRemovalCommand>();
        if (!TryReadOperationArray(updates, "threatsToRemove", out var values, issues))
            return result;

        var seen = new HashSet<string>(StringComparer.Ordinal);
        for (var index = 0; index < values.Count; index++)
        {
            var context = $"worldMapUpdates.threatsToRemove[{index}]";
            if (!TryReadExistingThreatOperation(
                    values[index],
                    context,
                    ThreatRemovalFields,
                    "mortal_location_threat_removal_target_unresolved",
                    preTurnLocationsById,
                    issues,
                    out var locationId,
                    out var threatId,
                    out _))
            {
                continue;
            }
            AddUniqueThreatCommand(seen, locationId, threatId, context, "removal", issues);
            result.Add(new ThreatRemovalCommand(locationId, threatId, context));
        }
        return result;
    }

    private static List<ThreatCompletionCommand> ReadThreatCompletions(
        JsonObject? updates,
        IReadOnlyDictionary<string, JsonObject> preTurnLocationsById,
        List<ValidationIssue> issues)
    {
        var result = new List<ThreatCompletionCommand>();
        if (!TryReadOperationArray(updates, "completeThreatActivities", out var values, issues))
            return result;

        var seen = new HashSet<string>(StringComparer.Ordinal);
        for (var index = 0; index < values.Count; index++)
        {
            var context = $"worldMapUpdates.completeThreatActivities[{index}]";
            if (!TryReadExistingThreatOperation(
                    values[index],
                    context,
                    ThreatCompletionFields,
                    "mortal_location_threat_completion_target_unresolved",
                    preTurnLocationsById,
                    issues,
                    out var locationId,
                    out var threatId,
                    out var threat))
            {
                continue;
            }
            var operation = (JsonObject)values[index]!;
            var finalState = ReadExactString(operation, "finalState");
            var summary = ReadExactString(operation, "narrativeSummary");
            var threatName = ReadExactString(operation, "threatName");
            if (finalState is not ("Completed" or "Abandoned") ||
                summary == null ||
                !string.Equals(threatName, ReadExactString(threat, "name"), StringComparison.Ordinal) ||
                threat["currentActivity"] is not JsonObject)
            {
                issues.Add(InvalidLifecycleShape(
                    context,
                    "mortal_location_threat_completion_invalid",
                    operation));
                continue;
            }
            AddUniqueThreatCommand(seen, locationId, threatId, context, "completion", issues);
            result.Add(new ThreatCompletionCommand(
                locationId,
                threatId,
                threatName!,
                finalState,
                summary,
                context));
        }
        return result;
    }

    private static bool TryReadExistingThreatOperation(
        JsonNode? value,
        string context,
        IReadOnlySet<string> fields,
        string unresolvedCode,
        IReadOnlyDictionary<string, JsonObject> preTurnLocationsById,
        List<ValidationIssue> issues,
        out string locationId,
        out string threatId,
        out JsonObject threat)
    {
        locationId = string.Empty;
        threatId = string.Empty;
        threat = null!;
        if (value is not JsonObject operation)
        {
            issues.Add(InvalidOperationRoot(context, value));
            return false;
        }
        ValidateExactOperationFields(
            operation,
            fields,
            context,
            "mortal_location_threat_command_invalid",
            issues);
        var target = ReadGovernedTargetLocation(
            operation,
            context,
            unresolvedCode,
            preTurnLocationsById,
            issues);
        threatId = ReadExactString(operation, "threatId") ?? string.Empty;
        threat = target == null || threatId.Length == 0
            ? null!
            : FindExactObject(target, "activeThreats", "threatId", threatId)!;
        if (threat == null)
        {
            issues.Add(Issue(
                context + ".threatId",
                unresolvedCode,
                "This threat command must bind one exact active pre-turn threat.",
                "exact active threatId in exact targetLocationId",
                operation["threatId"]?.ToJsonString() ?? "missing"));
            return false;
        }
        locationId = ReadExactString(target, "locationId")!;
        return true;
    }

    private static JsonObject? ReadGovernedTargetLocation(
        JsonObject operation,
        string context,
        string code,
        IReadOnlyDictionary<string, JsonObject> preTurnLocationsById,
        List<ValidationIssue> issues)
    {
        var locationId = ReadExactString(operation, "targetLocationId");
        if (locationId != null && preTurnLocationsById.TryGetValue(locationId, out var target))
            return target;

        issues.Add(Issue(
            context + ".targetLocationId",
            code,
            "This governed location command must bind one exact active pre-turn location.",
            "exact active targetLocationId",
            operation["targetLocationId"]?.ToJsonString() ?? "missing"));
        return null;
    }

    private static JsonObject? FindExactObject(
        JsonObject owner,
        string arrayField,
        string identityField,
        string identity)
    {
        if (owner[arrayField] is not JsonArray values)
            return null;
        var matches = values.OfType<JsonObject>()
            .Where(value => string.Equals(
                ReadExactString(value, identityField),
                identity,
                StringComparison.Ordinal))
            .Take(2)
            .ToArray();
        return matches.Length == 1 ? matches[0] : null;
    }

    private static void AddUniqueThreatCommand(
        ISet<string> seen,
        string locationId,
        string threatId,
        string context,
        string operation,
        List<ValidationIssue> issues)
    {
        var key = locationId + "\u001f" + threatId;
        if (seen.Add(MortalLocationIdentityState.BuildConfusableKey(key)))
            return;
        issues.Add(Issue(
            context + ".threatId",
            "mortal_location_threat_target_ambiguous",
            $"A threat may have at most one {operation} command in an accepted turn.",
            "one exact command per threat",
            key));
    }

    private static void ValidateGovernedCommandConflicts(
        IReadOnlyList<StorageUpdateCommand> storageUpdates,
        IReadOnlyList<StorageRemovalCommand> storageRemovals,
        IReadOnlyList<ThreatUpdateCommand> threatUpdates,
        IReadOnlyList<ThreatRemovalCommand> threatRemovals,
        IReadOnlyList<ThreatCompletionCommand> threatCompletions,
        List<ValidationIssue> issues)
    {
        var updatedStorages = storageUpdates
            .Select(static command => command.Key)
            .ToHashSet(StringComparer.Ordinal);
        foreach (var removal in storageRemovals.Where(command => updatedStorages.Contains(command.Key)))
        {
            issues.Add(Issue(
                removal.Context + ".storageId",
                "mortal_location_storage_transition_conflict",
                "A storage cannot be updated and removed in the same accepted turn.",
                "one lifecycle command per storage",
                removal.Key));
        }

        var operations = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var command in threatUpdates.Select(static value => (value.Key, value.Context, Kind: "update"))
                     .Concat(threatRemovals.Select(static value => (value.Key, value.Context, Kind: "removal")))
                     .Concat(threatCompletions.Select(static value => (value.Key, value.Context, Kind: "completion"))))
        {
            if (operations.TryAdd(command.Key, command.Kind))
                continue;
            issues.Add(Issue(
                command.Context + ".threatId",
                "mortal_location_threat_transition_conflict",
                "A threat cannot receive multiple lifecycle commands in the same accepted turn.",
                "one update, removal, or completion per threat",
                command.Key));
        }
    }

    private static void AssignThreatIds(
        IReadOnlyList<ThreatAdditionCommand> additions,
        JsonArray preTurnLocations,
        IReadOnlyList<LocationCandidate> locationCandidates,
        MortalLocationIdentityFactory identityFactory,
        List<ValidationIssue> issues)
    {
        var exact = new HashSet<string>(StringComparer.Ordinal);
        var confusable = new HashSet<string>(StringComparer.Ordinal);
        foreach (var location in preTurnLocations.OfType<JsonObject>()
                     .Concat(locationCandidates.Select(static candidate => candidate.Raw)))
        {
            if (location["activeThreats"] is not JsonArray threats)
                continue;
            foreach (var threatId in threats.OfType<JsonObject>()
                         .Select(static threat => ReadExactString(threat, "threatId"))
                         .Where(static threatId => threatId != null)
                         .Cast<string>())
            {
                exact.Add(threatId);
                confusable.Add(MortalLocationIdentityState.BuildConfusableKey(threatId));
            }
        }

        foreach (var addition in additions)
        {
            var threatId = identityFactory.CreateThreatId();
            if (!exact.Add(threatId) ||
                !confusable.Add(MortalLocationIdentityState.BuildConfusableKey(threatId)))
            {
                issues.Add(Issue(
                    addition.Context + ".threat.threatId",
                    "mortal_location_threat_identity_conflict",
                    "The client-generated permanent threat identity collides with accepted authority.",
                    "unused permanent threatId",
                    threatId));
                continue;
            }
            addition.AssignedThreatId = threatId;
        }
    }

    private static void ApplyGovernedLocationCommands(
        JsonArray locations,
        JsonArray indexEntries,
        GovernedLocationCommands commands,
        IReadOnlyDictionary<string, string> locationIdsByInitialId,
        int turn,
        MortalLocationIdentityFactory identityFactory)
    {
        foreach (var command in commands.StorageUpdates)
        {
            var location = FindExactObject(locations, "locationId", command.LocationId)!;
            var storage = FindExactObject(location, "locationStorages", "storageId", command.StorageId)!;
            var before = storage.DeepClone().AsObject();
            foreach (var (source, target) in new[]
                     {
                         ("newName", "name"),
                         ("newDescription", "description"),
                         ("newCapacity", "capacity"),
                         ("newOwner", "owner")
                     })
            {
                if (command.Patch.ContainsKey(source))
                    storage[target] = command.Patch[source]?.DeepClone();
            }
            AppendLocationChildTransition(
                indexEntries,
                command.LocationId,
                "storage_update",
                command.StorageId,
                before,
                storage,
                command.Context,
                turn,
                identityFactory);
        }

        foreach (var command in commands.StorageRemovals)
        {
            var location = FindExactObject(locations, "locationId", command.LocationId)!;
            var storage = FindExactObject(location, "locationStorages", "storageId", command.StorageId)!;
            var before = storage.DeepClone().AsObject();
            location["locationStorages"]!.AsArray().Remove(storage);
            AppendLocationChildTransition(
                indexEntries,
                command.LocationId,
                "storage_removal",
                command.StorageId,
                before,
                new JsonObject { ["state"] = "removed" },
                command.Context,
                turn,
                identityFactory);
        }

        foreach (var command in commands.ThreatAdds)
        {
            var locationId = command.LocationId ?? locationIdsByInitialId[command.InitialLocationId!];
            var location = FindExactObject(locations, "locationId", locationId)!;
            var threat = command.Threat.DeepClone().AsObject();
            threat["threatId"] = command.AssignedThreatId;
            location["activeThreats"]!.AsArray().Add(threat);
            AppendLocationChildTransition(
                indexEntries,
                locationId,
                "threat_addition",
                command.AssignedThreatId!,
                new JsonObject { ["state"] = "absent" },
                threat,
                command.Context,
                turn,
                identityFactory);
        }

        foreach (var command in commands.ThreatUpdates)
        {
            var location = FindExactObject(locations, "locationId", command.LocationId)!;
            var threat = FindExactObject(location, "activeThreats", "threatId", command.ThreatId)!;
            var before = threat.DeepClone().AsObject();
            MergePatch(threat, command.Patch, "threatId");
            AppendLocationChildTransition(
                indexEntries,
                command.LocationId,
                "threat_update",
                command.ThreatId,
                before,
                threat,
                command.Context,
                turn,
                identityFactory);
        }

        foreach (var command in commands.ThreatRemovals)
        {
            var location = FindExactObject(locations, "locationId", command.LocationId)!;
            var threat = FindExactObject(location, "activeThreats", "threatId", command.ThreatId)!;
            var before = threat.DeepClone().AsObject();
            location["activeThreats"]!.AsArray().Remove(threat);
            AppendLocationChildTransition(
                indexEntries,
                command.LocationId,
                "threat_removal",
                command.ThreatId,
                before,
                new JsonObject { ["state"] = "removed" },
                command.Context,
                turn,
                identityFactory);
        }

        foreach (var command in commands.ThreatCompletions)
        {
            var location = FindExactObject(locations, "locationId", command.LocationId)!;
            var threat = FindExactObject(location, "activeThreats", "threatId", command.ThreatId)!;
            var before = threat.DeepClone().AsObject();
            var activity = threat["currentActivity"]!.DeepClone();
            threat["currentActivity"] = null;
            location["eventDescriptions"]!.AsArray().Add(new JsonObject
            {
                ["eventType"] = "threat_activity_completion",
                ["threatId"] = command.ThreatId,
                ["title"] = command.ThreatName,
                ["finalState"] = command.FinalState,
                ["description"] = command.NarrativeSummary,
                ["completionTurn"] = turn,
                ["activity"] = activity
            });
            AppendLocationChildTransition(
                indexEntries,
                command.LocationId,
                "threat_activity_completion",
                command.ThreatId,
                before,
                threat,
                command.Context,
                turn,
                identityFactory);
        }
    }

    private static void MergePatch(JsonObject target, JsonObject patch, string excludedField)
    {
        foreach (var property in patch)
        {
            if (string.Equals(property.Key, excludedField, StringComparison.Ordinal))
                continue;
            if (property.Value is JsonObject patchObject && target[property.Key] is JsonObject targetObject)
                MergePatch(targetObject, patchObject, excludedField: string.Empty);
            else
                target[property.Key] = property.Value?.DeepClone();
        }
    }

    private static void AppendLocationChildTransition(
        JsonArray indexEntries,
        string locationId,
        string kind,
        string childId,
        JsonObject before,
        JsonObject after,
        string context,
        int turn,
        MortalLocationIdentityFactory identityFactory)
    {
        var transition = CreateLifecycleTransition(
            identityFactory,
            kind,
            turn,
            locationId,
            before,
            after,
            context,
            locationId,
            locationId);
        transition["childId"] = childId;
        AppendLifecycleTransition(
            indexEntries,
            "locationId",
            locationId,
            transition);
    }

    private sealed record GovernedLocationCommands(
        IReadOnlyList<StorageUpdateCommand> StorageUpdates,
        IReadOnlyList<StorageRemovalCommand> StorageRemovals,
        IReadOnlyList<ThreatAdditionCommand> ThreatAdds,
        IReadOnlyList<ThreatUpdateCommand> ThreatUpdates,
        IReadOnlyList<ThreatRemovalCommand> ThreatRemovals,
        IReadOnlyList<ThreatCompletionCommand> ThreatCompletions)
    {
        internal bool HasCommands => StorageUpdates.Count > 0 || StorageRemovals.Count > 0 ||
                                     ThreatAdds.Count > 0 || ThreatUpdates.Count > 0 ||
                                     ThreatRemovals.Count > 0 || ThreatCompletions.Count > 0;
    }

    private sealed record StorageUpdateCommand(
        string LocationId,
        string StorageId,
        JsonObject Patch,
        string Context)
    {
        internal string Key => LocationId + "\u001f" + StorageId;
    }

    private sealed record StorageRemovalCommand(
        string LocationId,
        string StorageId,
        string Context)
    {
        internal string Key => LocationId + "\u001f" + StorageId;
    }

    private sealed class ThreatAdditionCommand
    {
        internal ThreatAdditionCommand(
            string? locationId,
            string? initialLocationId,
            JsonObject threat,
            string context)
        {
            LocationId = locationId;
            InitialLocationId = initialLocationId;
            Threat = threat;
            Context = context;
        }

        internal string? LocationId { get; }
        internal string? InitialLocationId { get; }
        internal JsonObject Threat { get; }
        internal string Context { get; }
        internal string? AssignedThreatId { get; set; }
    }

    private sealed record ThreatUpdateCommand(
        string LocationId,
        string ThreatId,
        JsonObject Patch,
        string Context)
    {
        internal string Key => LocationId + "\u001f" + ThreatId;
    }

    private sealed record ThreatRemovalCommand(
        string LocationId,
        string ThreatId,
        string Context)
    {
        internal string Key => LocationId + "\u001f" + ThreatId;
    }

    private sealed record ThreatCompletionCommand(
        string LocationId,
        string ThreatId,
        string ThreatName,
        string FinalState,
        string NarrativeSummary,
        string Context)
    {
        internal string Key => LocationId + "\u001f" + ThreatId;
    }
}
