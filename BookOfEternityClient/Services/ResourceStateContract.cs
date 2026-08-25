using System.Buffers.Binary;
using System.Collections.Frozen;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace BookOfEternityClient.Services;

internal sealed record ResourceCoordinate(
    string Realm,
    ResourceOwnerKind OwnerKind,
    string ResourceOwnerId,
    string ResourceKey);

internal enum ResourceLifecycleState
{
    Active,
    Suspended
}

internal sealed record ResourceCapacityBinding(
    ResourceCapacityKind Kind,
    string AuthorityKey,
    string AuthorityFingerprint);

internal sealed record ResourceChronology(
    int CreatedAtTurn,
    string CreatedEventRef,
    string LastTransitionId,
    string LastEventRef,
    int LastTransitionTurn);

internal sealed record ResourceStateSnapshot(
    decimal Current,
    decimal Maximum,
    ResourceCapacityBinding CapacityBinding,
    ResourceLifecycleState State);

internal sealed record ResourceStateEntry(
    ResourceCoordinate Coordinate,
    decimal Current,
    decimal Maximum,
    ResourceCapacityBinding CapacityBinding,
    ResourceLifecycleState State,
    ResourceChronology Chronology)
{
    internal ResourceStateSnapshot Snapshot =>
        new(Current, Maximum, CapacityBinding, State);
}

internal sealed record ResourceStateContractResult(
    ResourceStateLedger? Ledger,
    IReadOnlyList<ValidationIssue> Issues,
    bool IsMissing = false)
{
    internal bool IsValid => Ledger != null && Issues.Count == 0;
}

internal sealed class ResourceStateLedger
{
    private readonly FrozenDictionary<ResourceCoordinate, ResourceStateEntry> _byCoordinate;

    internal ResourceStateLedger(
        IEnumerable<ResourceStateEntry> entries,
        ResourceAuthorityWorkMeter? workMeter = null)
    {
        var candidates = entries.ToArray();
        foreach (var _ in candidates)
            workMeter?.VisitStateConstruction();
        IComparer<ResourceStateEntry> comparer = ResourceStateEntryComparer.Instance;
        if (workMeter != null)
        {
            comparer = new ResourceAuthorityCountingComparer<ResourceStateEntry>(
                comparer,
                workMeter.CompareStateEntries);
        }
        var ordered = candidates
            .OrderBy(static entry => entry, comparer)
            .ToArray();
        Entries = new ReadOnlyCollection<ResourceStateEntry>(ordered);
        foreach (var _ in ordered)
            workMeter?.VisitStateIndex();
        _byCoordinate = ordered.ToFrozenDictionary(
            static entry => entry.Coordinate,
            ResourceCoordinateComparer.Instance);
        Fingerprint = ResourceStateContract.ComputeFingerprint(ordered, workMeter);
    }

    internal IReadOnlyList<ResourceStateEntry> Entries { get; }

    internal string Fingerprint { get; }

    internal bool TryResolveExact(
        ResourceCoordinate coordinate,
        out ResourceStateEntry? entry) =>
        _byCoordinate.TryGetValue(coordinate, out entry);

    internal string ToCanonicalJson()
    {
        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream))
        {
            writer.WriteStartObject();
            writer.WriteNumber("schemaVersion", ResourceMaterializationContract.SchemaVersion);
            writer.WriteStartArray("entries");
            foreach (var entry in Entries)
                ResourceStateContract.WriteEntry(writer, entry);
            writer.WriteEndArray();
            writer.WriteEndObject();
        }

        return Encoding.UTF8.GetString(stream.ToArray());
    }

    private sealed class ResourceStateEntryComparer : IComparer<ResourceStateEntry>
    {
        internal static ResourceStateEntryComparer Instance { get; } = new();

        public int Compare(ResourceStateEntry? left, ResourceStateEntry? right)
        {
            if (ReferenceEquals(left, right))
                return 0;
            if (left == null)
                return -1;
            if (right == null)
                return 1;
            var result = StringComparer.Ordinal.Compare(
                left.Coordinate.Realm,
                right.Coordinate.Realm);
            if (result != 0)
                return result;
            result = left.Coordinate.OwnerKind.CompareTo(right.Coordinate.OwnerKind);
            if (result != 0)
                return result;
            result = StringComparer.Ordinal.Compare(
                left.Coordinate.ResourceOwnerId,
                right.Coordinate.ResourceOwnerId);
            return result != 0
                ? result
                : StringComparer.Ordinal.Compare(
                    left.Coordinate.ResourceKey,
                    right.Coordinate.ResourceKey);
        }
    }
}

internal static class ResourceStateContract
{
    private static readonly FrozenSet<string> Realms = Set(
        "mortal_world",
        "chaos_sea",
        "shining_abode");

    private static readonly FrozenSet<string> RootFields = Set(
        "schemaVersion",
        "entries");

    private static readonly FrozenSet<string> EntryFields = Set(
        "realm",
        "ownerKind",
        "resourceOwnerId",
        "resourceKey",
        "current",
        "maximum",
        "capacityBinding",
        "state",
        "chronology");

    private static readonly FrozenSet<string> CoordinateFields = Set(
        "realm",
        "ownerKind",
        "resourceOwnerId",
        "resourceKey");

    private static readonly FrozenSet<string> SnapshotFields = Set(
        "current",
        "maximum",
        "capacityBinding",
        "state");

    private static readonly FrozenSet<string> CapacityBindingFields = Set(
        "kind",
        "authorityKey",
        "authorityFingerprint");

    private static readonly FrozenSet<string> ChronologyFields = Set(
        "createdAtTurn",
        "createdEventRef",
        "lastTransitionId",
        "lastEventRef",
        "lastTransitionTurn");

    internal static ResourceStateContractResult ParseCanonical(
        string? json,
        ResourceDefinitionCatalog definitions,
        bool allowMissingPristine,
        ResourceAuthorityWorkMeter? workMeter = null)
    {
        ArgumentNullException.ThrowIfNull(definitions);
        if (json == null)
        {
            if (allowMissingPristine)
            {
                return new ResourceStateContractResult(
                    new ResourceStateLedger(
                        Array.Empty<ResourceStateEntry>(),
                        workMeter),
                    Array.Empty<ValidationIssue>(),
                    IsMissing: true);
            }

            return Failure(
                "resource_state_root_missing",
                "present canonical resource state root",
                "missing",
                isMissing: true);
        }

        if (string.IsNullOrWhiteSpace(json))
        {
            return Failure(
                "resource_state_invalid_root",
                "non-empty strict JSON object",
                "empty or whitespace-only file");
        }

        JsonDocument document;
        try
        {
            document = JsonDocument.Parse(json);
        }
        catch (JsonException exception)
        {
            return Failure(
                "resource_state_invalid_json",
                "well-formed strict JSON object",
                exception.GetType().Name);
        }

        using (document)
        {
            var root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object)
            {
                return Failure(
                    "resource_state_invalid_root",
                    "strict JSON object",
                    root.ValueKind.ToString());
            }

            var issues = new List<ValidationIssue>();
            ResourceMaterializationContract.FindDuplicateProperties(
                root,
                ResourceMaterializationContract.StatePath,
                issues,
                "resource_state_duplicate_property");
            ResourceMaterializationContract.ValidateClosedObject(
                root,
                ResourceMaterializationContract.StatePath,
                RootFields,
                issues,
                "resource_state_unknown_field");

            if (!TryReadExactInt(root, "schemaVersion", out var schemaVersion) ||
                schemaVersion != ResourceMaterializationContract.SchemaVersion)
            {
                Add(
                    issues,
                    ResourceMaterializationContract.StatePath + ".schemaVersion",
                    "resource_state_invalid_field",
                    ResourceMaterializationContract.SchemaVersion.ToString(
                        CultureInfo.InvariantCulture),
                    ResourceMaterializationContract.Describe(root, "schemaVersion"));
            }

            if (!root.TryGetProperty("entries", out var entries) ||
                entries.ValueKind != JsonValueKind.Array)
            {
                Add(
                    issues,
                    ResourceMaterializationContract.StatePath + ".entries",
                    "resource_state_invalid_field",
                    "array",
                    ResourceMaterializationContract.Describe(root, "entries"));
                return new ResourceStateContractResult(null, issues.ToArray());
            }

            if (entries.GetArrayLength() > ResourceMaterializationContract.MaxLiveEntries)
            {
                Add(
                    issues,
                    ResourceMaterializationContract.StatePath + ".entries",
                    "resource_state_limit_exceeded",
                    $"at most {ResourceMaterializationContract.MaxLiveEntries} live entries",
                    entries.GetArrayLength().ToString(CultureInfo.InvariantCulture));
                return new ResourceStateContractResult(null, issues.ToArray());
            }

            var parsed = ParseEntries(entries, definitions, issues, workMeter);
            return issues.Count == 0
                ? new ResourceStateContractResult(
                    new ResourceStateLedger(parsed, workMeter),
                    issues)
                : new ResourceStateContractResult(null, issues.ToArray());
        }
    }

    internal static bool TryValidateSnapshot(
        ResourceCoordinate coordinate,
        ResourceStateSnapshot snapshot,
        ResourceDefinitionCatalog definitions,
        string path,
        List<ValidationIssue> issues)
    {
        if (!definitions.TryResolveExact(coordinate.ResourceKey, out var definition) ||
            definition == null)
        {
            Add(
                issues,
                path + ".resourceKey",
                "resource_state_definition_unknown",
                "exact sealed resource definition",
                coordinate.ResourceKey);
            return false;
        }

        if (!definition.AllowedOwnerKinds.Contains(coordinate.OwnerKind))
        {
            Add(
                issues,
                path + ".ownerKind",
                "resource_state_owner_kind_forbidden",
                "owner kind allowed by the sealed resource definition",
                ResourceDefinitionCatalog.GetOwnerKindToken(coordinate.OwnerKind));
        }

        ValidateNumericState(coordinate, snapshot, definition, path, issues);
        ValidateCapacityBinding(coordinate, snapshot, definition, path, issues);
        return issues.Count == 0;
    }

    internal static ResourceCoordinate? ParseCoordinateObject(
        JsonElement value,
        string path,
        List<ValidationIssue> issues)
    {
        if (value.ValueKind != JsonValueKind.Object)
        {
            Add(
                issues,
                path,
                "resource_state_coordinate_invalid",
                "strict resource-coordinate object",
                value.ValueKind.ToString());
            return null;
        }

        ResourceMaterializationContract.ValidateClosedObject(
            value,
            path,
            CoordinateFields,
            issues,
            "resource_state_unknown_field");
        var realm = ReadIdentifier(value, path, "realm", issues);
        var ownerToken = ReadIdentifier(value, path, "ownerKind", issues);
        var ownerId = ReadIdentifier(value, path, "resourceOwnerId", issues);
        var resourceKey = ReadIdentifier(value, path, "resourceKey", issues);
        if (realm == null || ownerToken == null || ownerId == null || resourceKey == null)
            return null;
        if (!Realms.Contains(realm) ||
            !ResourceDefinitionCatalog.TryParseOwnerKind(ownerToken, out var ownerKind))
        {
            Add(
                issues,
                path,
                "resource_state_coordinate_invalid",
                "registered realm and owner kind",
                $"realm={realm};ownerKind={ownerToken}");
            return null;
        }

        var coordinate = new ResourceCoordinate(realm, ownerKind, ownerId, resourceKey);
        ValidateRealmOwnerKind(coordinate, path, issues);
        return coordinate;
    }

    internal static ResourceStateSnapshot? ParseSnapshotObject(
        JsonElement value,
        ResourceCoordinate coordinate,
        ResourceDefinitionCatalog definitions,
        string path,
        List<ValidationIssue> issues)
    {
        if (value.ValueKind != JsonValueKind.Object)
        {
            Add(
                issues,
                path,
                "resource_state_invalid_entry",
                "strict resource-state snapshot object",
                value.ValueKind.ToString());
            return null;
        }

        ResourceMaterializationContract.ValidateClosedObject(
            value,
            path,
            SnapshotFields,
            issues,
            "resource_state_unknown_field");
        var current = ReadDecimal(value, path, "current", issues);
        var maximum = ReadDecimal(value, path, "maximum", issues);
        var capacity = ParseCapacityBinding(value, path, issues);
        var state = ParseLifecycle(value, path, issues);
        if (!current.HasValue || !maximum.HasValue || capacity == null || state == null)
            return null;

        var snapshot = new ResourceStateSnapshot(
            current.Value,
            maximum.Value,
            capacity,
            state.Value);
        TryValidateSnapshot(coordinate, snapshot, definitions, path, issues);
        return snapshot;
    }

    internal static string ComputeFingerprint(
        IEnumerable<ResourceStateEntry> entries,
        ResourceAuthorityWorkMeter? workMeter = null)
    {
        using var builder = new ResourceFingerprintBuilder("resource-state-v1");
        foreach (var entry in entries)
        {
            workMeter?.VisitStateFingerprint();
            AppendCoordinate(builder, entry.Coordinate);
            AppendSnapshot(builder, entry.Snapshot);
            builder.Append(entry.Chronology.CreatedAtTurn);
            builder.Append(entry.Chronology.CreatedEventRef);
            builder.Append(entry.Chronology.LastTransitionId);
            builder.Append(entry.Chronology.LastEventRef);
            builder.Append(entry.Chronology.LastTransitionTurn);
        }

        return builder.Build();
    }

    internal static void AppendCoordinate(
        ResourceFingerprintBuilder builder,
        ResourceCoordinate coordinate)
    {
        builder.Append(coordinate.Realm);
        builder.Append(ResourceDefinitionCatalog.GetOwnerKindToken(coordinate.OwnerKind));
        builder.Append(coordinate.ResourceOwnerId);
        builder.Append(coordinate.ResourceKey);
    }

    internal static void AppendSnapshot(
        ResourceFingerprintBuilder builder,
        ResourceStateSnapshot snapshot)
    {
        builder.Append(snapshot.Current);
        builder.Append(snapshot.Maximum);
        builder.Append(GetCapacityKindToken(snapshot.CapacityBinding.Kind));
        builder.Append(snapshot.CapacityBinding.AuthorityKey);
        builder.Append(snapshot.CapacityBinding.AuthorityFingerprint);
        builder.Append(GetLifecycleToken(snapshot.State));
    }

    internal static void WriteEntry(Utf8JsonWriter writer, ResourceStateEntry entry)
    {
        writer.WriteStartObject();
        WriteCoordinateFields(writer, entry.Coordinate);
        WriteCanonicalDecimal(writer, "current", entry.Current);
        WriteCanonicalDecimal(writer, "maximum", entry.Maximum);
        WriteCapacityBinding(writer, entry.CapacityBinding);
        writer.WriteString("state", GetLifecycleToken(entry.State));
        writer.WriteStartObject("chronology");
        writer.WriteNumber("createdAtTurn", entry.Chronology.CreatedAtTurn);
        writer.WriteString("createdEventRef", entry.Chronology.CreatedEventRef);
        writer.WriteString("lastTransitionId", entry.Chronology.LastTransitionId);
        writer.WriteString("lastEventRef", entry.Chronology.LastEventRef);
        writer.WriteNumber("lastTransitionTurn", entry.Chronology.LastTransitionTurn);
        writer.WriteEndObject();
        writer.WriteEndObject();
    }

    internal static void WriteSnapshot(
        Utf8JsonWriter writer,
        string propertyName,
        ResourceStateSnapshot? snapshot)
    {
        if (snapshot == null)
        {
            writer.WriteNull(propertyName);
            return;
        }

        writer.WriteStartObject(propertyName);
        WriteCanonicalDecimal(writer, "current", snapshot.Current);
        WriteCanonicalDecimal(writer, "maximum", snapshot.Maximum);
        WriteCapacityBinding(writer, snapshot.CapacityBinding);
        writer.WriteString("state", GetLifecycleToken(snapshot.State));
        writer.WriteEndObject();
    }

    internal static void WriteCoordinateObject(
        Utf8JsonWriter writer,
        string propertyName,
        ResourceCoordinate coordinate)
    {
        writer.WriteStartObject(propertyName);
        WriteCoordinateFields(writer, coordinate);
        writer.WriteEndObject();
    }

    internal static void WriteCanonicalDecimal(
        Utf8JsonWriter writer,
        string propertyName,
        decimal value)
    {
        writer.WritePropertyName(propertyName);
        writer.WriteRawValue(
            value.ToString("G29", CultureInfo.InvariantCulture),
            skipInputValidation: true);
    }

    internal static string GetLifecycleToken(ResourceLifecycleState state) => state switch
    {
        ResourceLifecycleState.Active => "active",
        ResourceLifecycleState.Suspended => "suspended",
        _ => throw new ArgumentOutOfRangeException(nameof(state), state, null)
    };

    internal static string GetCapacityKindToken(ResourceCapacityKind kind) => kind switch
    {
        ResourceCapacityKind.DefinitionFixed => "definition_fixed",
        ResourceCapacityKind.InstanceFixed => "instance_fixed",
        ResourceCapacityKind.RegisteredFormula => "registered_formula",
        _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, null)
    };

    private static IReadOnlyList<ResourceStateEntry> ParseEntries(
        JsonElement entries,
        ResourceDefinitionCatalog definitions,
        List<ValidationIssue> issues,
        ResourceAuthorityWorkMeter? workMeter)
    {
        var parsed = new List<ResourceStateEntry>();
        var exactCoordinates = new HashSet<ResourceCoordinate>(ResourceCoordinateComparer.Instance);
        var confusableCoordinates = new HashSet<ConfusableResourceCoordinate>();
        var exactTransitionIds = new HashSet<string>(StringComparer.Ordinal);
        var confusableTransitionIds = new HashSet<string>(StringComparer.Ordinal);
        var index = 0;
        foreach (var value in entries.EnumerateArray())
        {
            workMeter?.VisitStateDescriptor();
            var path = $"{ResourceMaterializationContract.StatePath}.entries[{index++}]";
            var entry = ParseEntry(value, path, definitions, issues);
            if (entry == null)
                continue;

            if (!exactCoordinates.Add(entry.Coordinate))
            {
                Add(
                    issues,
                    path,
                    "resource_state_duplicate_coordinate",
                    "one live entry per exact resource coordinate",
                    DescribeCoordinate(entry.Coordinate));
            }
            else if (!confusableCoordinates.Add(ToConfusable(entry.Coordinate)))
            {
                Add(
                    issues,
                    path,
                    "resource_state_confusable_coordinate",
                    "one exact/confusable-unique resource coordinate",
                    DescribeCoordinate(entry.Coordinate));
            }

            if (!exactTransitionIds.Add(entry.Chronology.LastTransitionId))
            {
                Add(
                    issues,
                    path + ".chronology.lastTransitionId",
                    "resource_state_duplicate_transition_id",
                    "globally unique latest transition identity",
                    entry.Chronology.LastTransitionId);
            }
            else if (!confusableTransitionIds.Add(
                         ResourceMaterializationContract.BuildConfusableKey(
                             entry.Chronology.LastTransitionId)))
            {
                Add(
                    issues,
                    path + ".chronology.lastTransitionId",
                    "resource_state_confusable_transition_id",
                    "globally exact/confusable-unique latest transition identity",
                    entry.Chronology.LastTransitionId);
            }

            parsed.Add(entry);
        }

        return parsed;
    }

    private static ResourceStateEntry? ParseEntry(
        JsonElement value,
        string path,
        ResourceDefinitionCatalog definitions,
        List<ValidationIssue> issues)
    {
        if (value.ValueKind != JsonValueKind.Object)
        {
            Add(
                issues,
                path,
                "resource_state_invalid_entry",
                "strict resource-state object",
                value.ValueKind.ToString());
            return null;
        }

        ResourceMaterializationContract.ValidateClosedObject(
            value,
            path,
            EntryFields,
            issues,
            "resource_state_unknown_field");

        var realm = ReadIdentifier(value, path, "realm", issues);
        if (realm != null && !Realms.Contains(realm))
        {
            Add(
                issues,
                path + ".realm",
                "resource_state_coordinate_invalid",
                "registered exact realm",
                realm);
        }

        var ownerToken = ReadIdentifier(value, path, "ownerKind", issues);
        ResourceOwnerKind? ownerKind = null;
        if (ownerToken != null &&
            ResourceDefinitionCatalog.TryParseOwnerKind(ownerToken, out var parsedOwnerKind))
        {
            ownerKind = parsedOwnerKind;
        }
        else if (ownerToken != null)
        {
            Add(
                issues,
                path + ".ownerKind",
                "resource_state_coordinate_invalid",
                "registered exact resource owner kind",
                ownerToken);
        }

        var ownerId = ReadIdentifier(value, path, "resourceOwnerId", issues);
        var resourceKey = ReadIdentifier(value, path, "resourceKey", issues);
        if (realm == null || ownerKind == null || ownerId == null || resourceKey == null)
            return null;

        var coordinate = new ResourceCoordinate(realm, ownerKind.Value, ownerId, resourceKey);
        ValidateRealmOwnerKind(coordinate, path, issues);

        var current = ReadDecimal(value, path, "current", issues);
        var maximum = ReadDecimal(value, path, "maximum", issues);
        var state = ParseLifecycle(value, path, issues);
        var capacity = ParseCapacityBinding(value, path, issues);
        var chronology = ParseChronology(value, path, issues);
        if (!current.HasValue ||
            !maximum.HasValue ||
            state == null ||
            capacity == null ||
            chronology == null)
        {
            return null;
        }

        var entry = new ResourceStateEntry(
            coordinate,
            current.Value,
            maximum.Value,
            capacity,
            state.Value,
            chronology);
        TryValidateSnapshot(coordinate, entry.Snapshot, definitions, path, issues);
        return entry;
    }

    private static ResourceCapacityBinding? ParseCapacityBinding(
        JsonElement root,
        string path,
        List<ValidationIssue> issues)
    {
        var bindingPath = path + ".capacityBinding";
        if (!root.TryGetProperty("capacityBinding", out var value) ||
            value.ValueKind != JsonValueKind.Object)
        {
            Add(
                issues,
                bindingPath,
                "resource_state_capacity_binding_invalid",
                "strict capacity-binding object",
                ResourceMaterializationContract.Describe(root, "capacityBinding"));
            return null;
        }

        ResourceMaterializationContract.ValidateClosedObject(
            value,
            bindingPath,
            CapacityBindingFields,
            issues,
            "resource_state_unknown_field");
        var kindToken = ReadIdentifier(value, bindingPath, "kind", issues);
        var authorityKey = ReadIdentifier(value, bindingPath, "authorityKey", issues);
        var fingerprint = ReadIdentifier(
            value,
            bindingPath,
            "authorityFingerprint",
            issues);
        ResourceCapacityKind? kind = kindToken switch
        {
            "definition_fixed" => ResourceCapacityKind.DefinitionFixed,
            "instance_fixed" => ResourceCapacityKind.InstanceFixed,
            "registered_formula" => ResourceCapacityKind.RegisteredFormula,
            null => null,
            _ => null
        };
        if (kindToken != null && kind == null)
        {
            Add(
                issues,
                bindingPath + ".kind",
                "resource_state_capacity_binding_invalid",
                "definition_fixed, instance_fixed, or registered_formula",
                kindToken);
        }

        if (fingerprint != null &&
            !ResourceMaterializationContract.IsAuthorityFingerprint(fingerprint))
        {
            Add(
                issues,
                bindingPath + ".authorityFingerprint",
                "resource_state_capacity_binding_invalid",
                "exact lowercase SHA-256 authority fingerprint",
                fingerprint);
        }

        return kind.HasValue && authorityKey != null && fingerprint != null &&
               ResourceMaterializationContract.IsAuthorityFingerprint(fingerprint)
            ? new ResourceCapacityBinding(kind.Value, authorityKey, fingerprint)
            : null;
    }

    private static ResourceChronology? ParseChronology(
        JsonElement root,
        string path,
        List<ValidationIssue> issues)
    {
        var chronologyPath = path + ".chronology";
        if (!root.TryGetProperty("chronology", out var value) ||
            value.ValueKind != JsonValueKind.Object)
        {
            Add(
                issues,
                chronologyPath,
                "resource_state_chronology_invalid",
                "strict chronology object",
                ResourceMaterializationContract.Describe(root, "chronology"));
            return null;
        }

        ResourceMaterializationContract.ValidateClosedObject(
            value,
            chronologyPath,
            ChronologyFields,
            issues,
            "resource_state_unknown_field");
        var createdAtTurn = ReadInt(value, chronologyPath, "createdAtTurn", issues);
        var createdEventRef = ReadIdentifier(value, chronologyPath, "createdEventRef", issues);
        var lastTransitionId = ReadIdentifier(
            value,
            chronologyPath,
            "lastTransitionId",
            issues);
        var lastEventRef = ReadIdentifier(value, chronologyPath, "lastEventRef", issues);
        var lastTransitionTurn = ReadInt(
            value,
            chronologyPath,
            "lastTransitionTurn",
            issues);
        if (!createdAtTurn.HasValue ||
            createdEventRef == null ||
            lastTransitionId == null ||
            lastEventRef == null ||
            !lastTransitionTurn.HasValue)
        {
            return null;
        }

        if (createdAtTurn.Value < 0 ||
            lastTransitionTurn.Value < createdAtTurn.Value)
        {
            Add(
                issues,
                chronologyPath,
                "resource_state_chronology_invalid",
                "non-negative creation turn and latest turn not before creation",
                $"created={createdAtTurn.Value};last={lastTransitionTurn.Value}");
        }

        return new ResourceChronology(
            createdAtTurn.Value,
            createdEventRef,
            lastTransitionId,
            lastEventRef,
            lastTransitionTurn.Value);
    }

    private static ResourceLifecycleState? ParseLifecycle(
        JsonElement root,
        string path,
        List<ValidationIssue> issues)
    {
        var token = ReadIdentifier(root, path, "state", issues);
        var state = token switch
        {
            "active" => ResourceLifecycleState.Active,
            "suspended" => ResourceLifecycleState.Suspended,
            _ => (ResourceLifecycleState?)null
        };
        if (token != null && !state.HasValue)
        {
            Add(
                issues,
                path + ".state",
                "resource_state_lifecycle_invalid",
                "active or suspended",
                token);
        }

        return state;
    }

    private static void ValidateNumericState(
        ResourceCoordinate coordinate,
        ResourceStateSnapshot snapshot,
        ResourceDefinition definition,
        string path,
        List<ValidationIssue> issues)
    {
        var minimum = definition.MinimumPolicy.Value;
        if (definition.NumericKind == ResourceNumericKind.Integer &&
            (!ResourceMaterializationContract.IsIntegral(snapshot.Current) ||
             !ResourceMaterializationContract.IsIntegral(snapshot.Maximum)))
        {
            Add(
                issues,
                path,
                "resource_state_value_invalid",
                "integral current and maximum for integer resource",
                $"current={snapshot.Current};maximum={snapshot.Maximum}");
        }

        if (!ResourceMaterializationContract.IsQuantumAligned(
                snapshot.Current,
                minimum,
                definition.Quantum) ||
            !ResourceMaterializationContract.IsQuantumAligned(
                snapshot.Maximum,
                minimum,
                definition.Quantum))
        {
            Add(
                issues,
                path,
                "resource_state_value_invalid",
                $"current and maximum aligned to quantum {definition.Quantum}",
                $"current={snapshot.Current};maximum={snapshot.Maximum}");
        }

        if (snapshot.Current < minimum || snapshot.Current > snapshot.Maximum)
        {
            Add(
                issues,
                path + ".current",
                "resource_state_value_out_of_range",
                $"value between minimum {minimum} and maximum {snapshot.Maximum}",
                snapshot.Current.ToString(CultureInfo.InvariantCulture));
        }

        var maximumMayEqualMinimum =
            definition.CapacityPolicy.Kind == ResourceCapacityKind.RegisteredFormula;
        if (snapshot.Maximum < minimum ||
            (!maximumMayEqualMinimum && snapshot.Maximum == minimum))
        {
            Add(
                issues,
                path + ".maximum",
                "resource_state_capacity_invalid",
                maximumMayEqualMinimum
                    ? $"maximum at or above minimum {minimum}"
                    : $"maximum above minimum {minimum}",
                snapshot.Maximum.ToString(CultureInfo.InvariantCulture));
        }

        _ = coordinate;
    }

    private static void ValidateCapacityBinding(
        ResourceCoordinate coordinate,
        ResourceStateSnapshot snapshot,
        ResourceDefinition definition,
        string path,
        List<ValidationIssue> issues)
    {
        var binding = snapshot.CapacityBinding;
        if (binding.Kind != definition.CapacityPolicy.Kind)
        {
            Add(
                issues,
                path + ".capacityBinding.kind",
                "resource_state_capacity_kind_mismatch",
                GetCapacityKindToken(definition.CapacityPolicy.Kind),
                GetCapacityKindToken(binding.Kind));
            return;
        }

        switch (binding.Kind)
        {
            case ResourceCapacityKind.DefinitionFixed:
                if (!string.Equals(
                        binding.AuthorityKey,
                        coordinate.ResourceKey,
                        StringComparison.Ordinal) ||
                    definition.CapacityPolicy.Value != snapshot.Maximum)
                {
                    Add(
                        issues,
                        path + ".capacityBinding",
                        "resource_state_capacity_invalid",
                        "sealed definition-fixed key and exact maximum",
                        $"key={binding.AuthorityKey};maximum={snapshot.Maximum}");
                    return;
                }

                var capacity = ResourceCapacityFormulaCatalog.ResolveCapacity(
                    definition,
                    new DefinitionFixedCapacityInput(
                        new ResourceFormulaOwner(
                            coordinate.Realm,
                            coordinate.OwnerKind,
                            coordinate.ResourceOwnerId)));
                if (!capacity.IsValid ||
                    !string.Equals(
                        binding.AuthorityFingerprint,
                        capacity.AuthorityFingerprint,
                        StringComparison.Ordinal))
                {
                    Add(
                        issues,
                        path + ".capacityBinding.authorityFingerprint",
                        "resource_state_capacity_invalid",
                        "fingerprint of sealed definition-fixed capacity and exact owner",
                        binding.AuthorityFingerprint);
                }
                break;

            case ResourceCapacityKind.InstanceFixed:
                if (!ResourceMaterializationContract.IsExactIdentifier(binding.AuthorityKey))
                {
                    Add(
                        issues,
                        path + ".capacityBinding.authorityKey",
                        "resource_state_capacity_binding_invalid",
                        "exact accepted capacity-source transition identity",
                        binding.AuthorityKey);
                }
                break;

            case ResourceCapacityKind.RegisteredFormula:
                if (!string.Equals(
                        binding.AuthorityKey,
                        definition.CapacityPolicy.FormulaKey,
                        StringComparison.Ordinal))
                {
                    Add(
                        issues,
                        path + ".capacityBinding.authorityKey",
                        "resource_state_capacity_kind_mismatch",
                        definition.CapacityPolicy.FormulaKey ?? "registered formula key",
                        binding.AuthorityKey);
                }
                break;

            default:
                throw new ArgumentOutOfRangeException();
        }
    }

    private static void ValidateRealmOwnerKind(
        ResourceCoordinate coordinate,
        string path,
        List<ValidationIssue> issues)
    {
        var isAfterlifeOwner = coordinate.OwnerKind is
            ResourceOwnerKind.AfterlifeActor or
            ResourceOwnerKind.AfterlifeConflictSide or
            ResourceOwnerKind.AfterlifeScope;
        var realmMatches = isAfterlifeOwner
            ? coordinate.Realm is "chaos_sea" or "shining_abode"
            : string.Equals(coordinate.Realm, "mortal_world", StringComparison.Ordinal);
        if (!realmMatches)
        {
            Add(
                issues,
                path + ".realm",
                "resource_state_coordinate_invalid",
                "realm compatible with exact owner kind",
                coordinate.Realm);
        }
    }

    private static string? ReadIdentifier(
        JsonElement root,
        string path,
        string field,
        List<ValidationIssue> issues)
    {
        if (!root.TryGetProperty(field, out var value) ||
            value.ValueKind != JsonValueKind.String)
        {
            Add(
                issues,
                path + "." + field,
                "resource_state_invalid_field",
                "exact non-empty identifier",
                ResourceMaterializationContract.Describe(root, field));
            return null;
        }

        var token = value.GetString();
        if (!ResourceMaterializationContract.IsExactIdentifier(token))
        {
            Add(
                issues,
                path + "." + field,
                field == "authorityKey" || field == "authorityFingerprint"
                    ? "resource_state_capacity_binding_invalid"
                    : "resource_state_invalid_field",
                "trimmed FormKC-safe exact identifier",
                token ?? "null");
            return null;
        }

        return token;
    }

    private static decimal? ReadDecimal(
        JsonElement root,
        string path,
        string field,
        List<ValidationIssue> issues)
    {
        if (!root.TryGetProperty(field, out var value) ||
            !ResourceMaterializationContract.TryReadExactDecimal(value, out var number))
        {
            Add(
                issues,
                path + "." + field,
                "resource_state_value_invalid",
                "exact supported decimal",
                ResourceMaterializationContract.Describe(root, field));
            return null;
        }

        return number;
    }

    private static int? ReadInt(
        JsonElement root,
        string path,
        string field,
        List<ValidationIssue> issues)
    {
        if (!TryReadExactInt(root, field, out var number))
        {
            Add(
                issues,
                path + "." + field,
                "resource_state_chronology_invalid",
                "exact integer turn",
                ResourceMaterializationContract.Describe(root, field));
            return null;
        }

        return number;
    }

    private static bool TryReadExactInt(
        JsonElement root,
        string field,
        out int number)
    {
        number = 0;
        return root.TryGetProperty(field, out var value) &&
               value.ValueKind == JsonValueKind.Number &&
               value.TryGetInt32(out number);
    }

    private static void WriteCoordinateFields(
        Utf8JsonWriter writer,
        ResourceCoordinate coordinate)
    {
        writer.WriteString("realm", coordinate.Realm);
        writer.WriteString(
            "ownerKind",
            ResourceDefinitionCatalog.GetOwnerKindToken(coordinate.OwnerKind));
        writer.WriteString("resourceOwnerId", coordinate.ResourceOwnerId);
        writer.WriteString("resourceKey", coordinate.ResourceKey);
    }

    private static void WriteCapacityBinding(
        Utf8JsonWriter writer,
        ResourceCapacityBinding binding)
    {
        writer.WriteStartObject("capacityBinding");
        writer.WriteString("kind", GetCapacityKindToken(binding.Kind));
        writer.WriteString("authorityKey", binding.AuthorityKey);
        writer.WriteString("authorityFingerprint", binding.AuthorityFingerprint);
        writer.WriteEndObject();
    }

    private static ConfusableResourceCoordinate ToConfusable(ResourceCoordinate coordinate) =>
        new(
            ResourceMaterializationContract.BuildConfusableKey(coordinate.Realm),
            coordinate.OwnerKind,
            ResourceMaterializationContract.BuildConfusableKey(
                coordinate.ResourceOwnerId),
            ResourceMaterializationContract.BuildConfusableKey(coordinate.ResourceKey));

    private static string DescribeCoordinate(ResourceCoordinate coordinate) =>
        $"{coordinate.Realm}/{ResourceDefinitionCatalog.GetOwnerKindToken(coordinate.OwnerKind)}/" +
        $"{coordinate.ResourceOwnerId}/{coordinate.ResourceKey}";

    private static ResourceStateContractResult Failure(
        string code,
        string expected,
        string actual,
        bool isMissing = false)
    {
        var issues = new List<ValidationIssue>();
        Add(
            issues,
            ResourceMaterializationContract.StatePath,
            code,
            expected,
            actual);
        return new ResourceStateContractResult(null, issues, isMissing);
    }

    private static void Add(
        List<ValidationIssue> issues,
        string path,
        string code,
        string expected,
        string actual) =>
        ResourceMaterializationContract.AddIssue(
            issues,
            path,
            code,
            expected,
            actual);

    private static FrozenSet<string> Set(params string[] values) =>
        values.ToFrozenSet(StringComparer.Ordinal);

    private sealed record ConfusableResourceCoordinate(
        string Realm,
        ResourceOwnerKind OwnerKind,
        string ResourceOwnerId,
        string ResourceKey);
}

internal sealed class ResourceCoordinateComparer : IEqualityComparer<ResourceCoordinate>
{
    internal static ResourceCoordinateComparer Instance { get; } = new();

    public bool Equals(ResourceCoordinate? x, ResourceCoordinate? y) =>
        ReferenceEquals(x, y) ||
        x != null && y != null &&
        string.Equals(x.Realm, y.Realm, StringComparison.Ordinal) &&
        x.OwnerKind == y.OwnerKind &&
        string.Equals(x.ResourceOwnerId, y.ResourceOwnerId, StringComparison.Ordinal) &&
        string.Equals(x.ResourceKey, y.ResourceKey, StringComparison.Ordinal);

    public int GetHashCode(ResourceCoordinate obj)
    {
        var hash = new HashCode();
        hash.Add(obj.Realm, StringComparer.Ordinal);
        hash.Add(obj.OwnerKind);
        hash.Add(obj.ResourceOwnerId, StringComparer.Ordinal);
        hash.Add(obj.ResourceKey, StringComparer.Ordinal);
        return hash.ToHashCode();
    }
}

internal sealed class ResourceFingerprintBuilder : IDisposable
{
    private readonly IncrementalHash _hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
    private bool _built;

    internal ResourceFingerprintBuilder(string domain)
    {
        Append(domain);
        Append(ResourceMaterializationContract.SchemaVersion);
    }

    internal void Append(string value)
    {
        ObjectDisposedException.ThrowIf(_built, this);
        var bytes = Encoding.UTF8.GetBytes(value);
        Span<byte> length = stackalloc byte[sizeof(int)];
        BinaryPrimitives.WriteInt32LittleEndian(length, bytes.Length);
        _hash.AppendData(length);
        _hash.AppendData(bytes);
    }

    internal void Append(int value)
    {
        ObjectDisposedException.ThrowIf(_built, this);
        Span<byte> bytes = stackalloc byte[sizeof(int)];
        BinaryPrimitives.WriteInt32LittleEndian(bytes, value);
        _hash.AppendData(bytes);
    }

    internal void Append(decimal value) =>
        Append(value.ToString("G29", CultureInfo.InvariantCulture));

    internal void Append(bool value) => Append(value ? 1 : 0);

    internal string Build()
    {
        ObjectDisposedException.ThrowIf(_built, this);
        _built = true;
        return "sha256:" + Convert.ToHexString(_hash.GetHashAndReset()).ToLowerInvariant();
    }

    public void Dispose()
    {
        _built = true;
        _hash.Dispose();
    }
}
