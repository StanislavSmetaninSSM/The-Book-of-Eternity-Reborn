using System.Collections.Frozen;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace BookOfEternityClient.Services;

internal enum ResourceCapacityOperation
{
    Initialize,
    Reconfigure,
    Suspend,
    Resume,
    Retire
}

internal enum ResourceCurrentDisposition
{
    InitializeFromDefinition,
    Preserve,
    ClampToNewMaximum,
    ScaleRatioExact
}

internal sealed record ResourceCommandTarget(
    ResourceOwnerKind OwnerKind,
    string? TargetId,
    string? TargetRef);

internal sealed record ResourceCommandSource(string Kind, string SourceId);

internal sealed record ResourceCapacityProposal(
    ResourceCapacityKind Kind,
    decimal? Maximum,
    string? FormulaKey);

internal sealed class ResourceDefinitionCreationCommand
{
    private readonly JsonObject _definition;

    internal ResourceDefinitionCreationCommand(
        int commandOrdinal,
        string definitionRef,
        JsonObject definition,
        string eventRef,
        string reason)
    {
        CommandOrdinal = commandOrdinal;
        DefinitionRef = definitionRef;
        _definition = definition.DeepClone().AsObject();
        EventRef = eventRef;
        Reason = reason;
    }

    internal int CommandOrdinal { get; }
    internal string DefinitionRef { get; }
    internal JsonObject Definition => _definition.DeepClone().AsObject();
    internal string EventRef { get; }
    internal string Reason { get; }
}

internal sealed record ResourceCapacityCommand(
    int CommandOrdinal,
    ResourceCapacityOperation Operation,
    ResourceCommandTarget Target,
    string? ResourceKey,
    string? ResourceDefinitionRef,
    ResourceCapacityProposal? Capacity,
    ResourceCurrentDisposition? CurrentDisposition,
    ResourceCommandSource Source,
    string EventRef,
    string Reason);

internal sealed record ResourceOrdinaryCommand(
    int CommandOrdinal,
    ResourceOperation Operation,
    ResourceCommandTarget Target,
    string ResourceKey,
    decimal Amount,
    ResourceCommandSource Source,
    string EventRef,
    string Reason);

internal sealed class ResourceCommandCompositionResult
{
    private readonly JsonObject _root;
    private readonly ResourceDefinitionCreationCommand[] _definitionCreations;
    private readonly ResourceCapacityCommand[] _capacityChanges;
    private readonly ResourceOrdinaryCommand[] _resourceChanges;
    private readonly ValidationIssue[] _issues;

    internal ResourceCommandCompositionResult(
        bool isMissing,
        JsonObject root,
        IEnumerable<ResourceDefinitionCreationCommand> definitionCreations,
        IEnumerable<ResourceCapacityCommand> capacityChanges,
        IEnumerable<ResourceOrdinaryCommand> resourceChanges,
        IEnumerable<ValidationIssue> issues)
    {
        IsMissing = isMissing;
        _root = root.DeepClone().AsObject();
        _definitionCreations = definitionCreations.ToArray();
        _capacityChanges = capacityChanges.ToArray();
        _resourceChanges = resourceChanges.ToArray();
        _issues = issues.ToArray();
    }

    internal bool IsMissing { get; }
    internal bool IsValid => _issues.Length == 0;
    internal JsonObject Root => _root.DeepClone().AsObject();
    internal IReadOnlyList<ResourceDefinitionCreationCommand> DefinitionCreations =>
        Array.AsReadOnly(_definitionCreations.ToArray());
    internal IReadOnlyList<ResourceCapacityCommand> CapacityChanges =>
        Array.AsReadOnly(_capacityChanges.ToArray());
    internal IReadOnlyList<ResourceOrdinaryCommand> ResourceChanges =>
        Array.AsReadOnly(_resourceChanges.ToArray());
    internal IReadOnlyList<ValidationIssue> Issues =>
        Array.AsReadOnly(_issues.ToArray());
}

internal static class ResourceAcceptedTurnInputComposer
{
    private static readonly FrozenSet<string> RootFields = Set(
        "resourceDefinitionCreations",
        "resourceCapacityChanges",
        "resourceChanges");
    private static readonly FrozenSet<string> DefinitionCreationFields = Set(
        "definitionRef", "definition", "eventRef", "reason");
    private static readonly FrozenSet<string> CapacityChangeFields = Set(
        "operation", "target", "resourceKey", "resourceDefinitionRef", "capacity",
        "currentDisposition", "source", "eventRef", "reason");
    private static readonly FrozenSet<string> OrdinaryChangeFields = Set(
        "operation", "target", "resourceKey", "amount", "source", "eventRef", "reason");
    private static readonly FrozenSet<string> TargetFields = Set(
        "kind", "targetId", "targetRef");
    private static readonly FrozenSet<string> SourceFields = Set("kind", "sourceId");
    private static readonly FrozenSet<string> InstanceCapacityFields = Set("kind", "maximum");
    private static readonly FrozenSet<string> FormulaCapacityFields = Set("kind", "formulaKey");

    internal static ResourceCommandCompositionResult Parse(string? json)
    {
        if (json == null)
            return Result(isMissing: true, new JsonObject());

        var issues = new List<ValidationIssue>();
        if (string.IsNullOrWhiteSpace(json))
        {
            Add(issues, ResourceMaterializationContract.CommandPath,
                "resource_command_invalid_root", "non-empty strict JSON object",
                "empty or whitespace-only file");
            return Result(isMissing: false, new JsonObject(), issues: issues);
        }

        JsonDocument document;
        try
        {
            document = JsonDocument.Parse(json);
        }
        catch (JsonException exception)
        {
            Add(issues, ResourceMaterializationContract.CommandPath,
                "resource_command_invalid_json", "well-formed strict JSON object",
                exception.GetType().Name);
            return Result(isMissing: false, new JsonObject(), issues: issues);
        }

        using (document)
        {
            var root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object)
            {
                Add(issues, ResourceMaterializationContract.CommandPath,
                    "resource_command_invalid_root", "strict JSON object",
                    root.ValueKind.ToString());
                return Result(isMissing: false, new JsonObject(), issues: issues);
            }

            ResourceMaterializationContract.FindDuplicateProperties(
                root,
                ResourceMaterializationContract.CommandPath,
                issues,
                "resource_command_duplicate_property");
            ResourceMaterializationContract.ValidateClosedObject(
                root,
                ResourceMaterializationContract.CommandPath,
                RootFields,
                issues,
                "resource_command_unknown_field");

            var definitionCreations = new List<ResourceDefinitionCreationCommand>();
            var capacityChanges = new List<ResourceCapacityCommand>();
            var resourceChanges = new List<ResourceOrdinaryCommand>();
            var refs = new HashSet<string>(StringComparer.Ordinal);
            var refAliases = new HashSet<string>(StringComparer.Ordinal);
            var ordinal = 0;

            ParseDefinitions(root, definitionCreations, refs, refAliases, issues, ref ordinal);
            ParseCapacityChanges(root, capacityChanges, issues, ref ordinal);
            ParseOrdinaryChanges(root, resourceChanges, issues, ref ordinal);

            var parsedRoot = JsonNode.Parse(root.GetRawText())!.AsObject();
            return Result(
                isMissing: false,
                parsedRoot,
                definitionCreations,
                capacityChanges,
                resourceChanges,
                issues);
        }
    }

    private static void ParseDefinitions(
        JsonElement root,
        List<ResourceDefinitionCreationCommand> result,
        HashSet<string> exactRefs,
        HashSet<string> refAliases,
        List<ValidationIssue> issues,
        ref int ordinal)
    {
        if (!TryReadArray(root, "resourceDefinitionCreations", issues, out var values))
            return;
        if (values.GetArrayLength() > ResourceMaterializationContract.MaxDefinitions)
        {
            Add(issues, "resourceDefinitionCreations", "resource_command_limit_exceeded",
                $"at most {ResourceMaterializationContract.MaxDefinitions} definition creations",
                values.GetArrayLength().ToString());
            ordinal += values.GetArrayLength();
            return;
        }

        var index = 0;
        foreach (var value in values.EnumerateArray())
        {
            ordinal++;
            var path = $"resourceDefinitionCreations[{index++}]";
            var issueCount = issues.Count;
            if (!RequireObject(value, path, issues))
                continue;
            ResourceMaterializationContract.ValidateClosedObject(
                value, path, DefinitionCreationFields, issues,
                "resource_command_unknown_field");

            var definitionRef = ReadExact(value, "definitionRef", path, issues);
            if (definitionRef != null)
            {
                var alias = ResourceMaterializationContract.BuildConfusableKey(definitionRef);
                if (!exactRefs.Add(definitionRef) || !refAliases.Add(alias))
                {
                    Add(issues, path + ".definitionRef",
                        "resource_command_definition_ref_ambiguous",
                        "exact/confusable-unique same-turn definitionRef",
                        definitionRef);
                }
            }

            JsonObject? definition = null;
            if (!value.TryGetProperty("definition", out var definitionElement) ||
                definitionElement.ValueKind != JsonValueKind.Object)
            {
                Add(issues, path + ".definition", "resource_command_invalid_field",
                    "complete raw resource definition object",
                    ResourceMaterializationContract.Describe(value, "definition"));
            }
            else
            {
                issues.AddRange(ResourceDefinitionCatalog.ValidateRawProposal(
                    definitionElement,
                    path + ".definition"));
                definition = JsonNode.Parse(definitionElement.GetRawText())!.AsObject();
            }

            var eventRef = ReadExact(value, "eventRef", path, issues);
            var reason = ReadReason(value, path, issues);
            if (issues.Count == issueCount && definitionRef != null && definition != null &&
                eventRef != null && reason != null)
            {
                result.Add(new ResourceDefinitionCreationCommand(
                    ordinal, definitionRef, definition, eventRef, reason));
            }
        }
    }

    private static void ParseCapacityChanges(
        JsonElement root,
        List<ResourceCapacityCommand> result,
        List<ValidationIssue> issues,
        ref int ordinal)
    {
        if (!TryReadArray(root, "resourceCapacityChanges", issues, out var values))
            return;
        if (values.GetArrayLength() > ResourceMaterializationContract.MaxCapacityTransitionsPerTurn)
        {
            Add(issues, "resourceCapacityChanges", "resource_command_limit_exceeded",
                $"at most {ResourceMaterializationContract.MaxCapacityTransitionsPerTurn} capacity changes",
                values.GetArrayLength().ToString());
            ordinal += values.GetArrayLength();
            return;
        }

        var index = 0;
        foreach (var value in values.EnumerateArray())
        {
            ordinal++;
            var path = $"resourceCapacityChanges[{index++}]";
            var issueCount = issues.Count;
            if (!RequireObject(value, path, issues))
                continue;
            ResourceMaterializationContract.ValidateClosedObject(
                value, path, CapacityChangeFields, issues,
                "resource_command_unknown_field");

            var operation = ParseCapacityOperation(value, path, issues);
            var target = ParseTarget(value, path, issues);
            ParseDefinitionSelector(
                value, path, issues, out var resourceKey, out var definitionRef);
            var capacity = ParseCapacity(value, path, issues);
            var disposition = ParseDisposition(value, operation, path, issues);
            var source = ParseSource(value, path, issues);
            var eventRef = ReadExact(value, "eventRef", path, issues);
            var reason = ReadReason(value, path, issues);

            if ((operation is ResourceCapacityOperation.Suspend or
                 ResourceCapacityOperation.Resume or ResourceCapacityOperation.Retire) &&
                (value.TryGetProperty("capacity", out _) ||
                 value.TryGetProperty("currentDisposition", out _)))
            {
                Add(issues, path, "resource_command_capacity_invalid",
                    "capacity and currentDisposition omitted for lifecycle-only operation",
                    value.GetRawText());
            }

            if (issues.Count == issueCount && operation != null && target != null &&
                source != null && eventRef != null && reason != null)
            {
                result.Add(new ResourceCapacityCommand(
                    ordinal,
                    operation.Value,
                    target,
                    resourceKey,
                    definitionRef,
                    capacity,
                    disposition,
                    source,
                    eventRef,
                    reason));
            }
        }
    }

    private static void ParseOrdinaryChanges(
        JsonElement root,
        List<ResourceOrdinaryCommand> result,
        List<ValidationIssue> issues,
        ref int ordinal)
    {
        if (!TryReadArray(root, "resourceChanges", issues, out var values))
            return;
        if (values.GetArrayLength() > ResourceMaterializationContract.MaxMutationsBeforeTriggers)
        {
            Add(issues, "resourceChanges", "resource_command_limit_exceeded",
                $"at most {ResourceMaterializationContract.MaxMutationsBeforeTriggers} ordinary changes",
                values.GetArrayLength().ToString());
            ordinal += values.GetArrayLength();
            return;
        }

        var index = 0;
        foreach (var value in values.EnumerateArray())
        {
            ordinal++;
            var path = $"resourceChanges[{index++}]";
            var issueCount = issues.Count;
            if (!RequireObject(value, path, issues))
                continue;
            ResourceMaterializationContract.ValidateClosedObject(
                value, path, OrdinaryChangeFields, issues,
                "resource_command_unknown_field");

            var operation = ParseOrdinaryOperation(value, path, issues);
            var target = ParseTarget(value, path, issues);
            var resourceKey = ReadExact(value, "resourceKey", path, issues);
            decimal? amount = null;
            if (!value.TryGetProperty("amount", out var amountElement) ||
                !ResourceMaterializationContract.TryReadExactDecimal(amountElement, out var parsedAmount) ||
                parsedAmount <= 0m)
            {
                Add(issues, path + ".amount", "resource_command_amount_invalid",
                    "positive exactly representable decimal",
                    ResourceMaterializationContract.Describe(value, "amount"));
            }
            else
            {
                amount = parsedAmount;
            }
            var source = ParseSource(value, path, issues);
            var eventRef = ReadExact(value, "eventRef", path, issues);
            var reason = ReadReason(value, path, issues);

            if (issues.Count == issueCount && operation != null && target != null &&
                resourceKey != null && amount != null && source != null &&
                eventRef != null && reason != null)
            {
                result.Add(new ResourceOrdinaryCommand(
                    ordinal,
                    operation.Value,
                    target,
                    resourceKey,
                    amount.Value,
                    source,
                    eventRef,
                    reason));
            }
        }
    }

    private static ResourceCommandTarget? ParseTarget(
        JsonElement command,
        string path,
        List<ValidationIssue> issues)
    {
        if (!command.TryGetProperty("target", out var target) ||
            target.ValueKind != JsonValueKind.Object)
        {
            Add(issues, path + ".target", "resource_command_target_invalid",
                "closed exact resource target", ResourceMaterializationContract.Describe(command, "target"));
            return null;
        }

        var issueCount = issues.Count;
        ResourceMaterializationContract.ValidateClosedObject(
            target, path + ".target", TargetFields, issues,
            "resource_command_unknown_field");
        var kind = ReadExact(target, "kind", path + ".target", issues);
        var hasId = target.TryGetProperty("targetId", out var idNode);
        var hasRef = target.TryGetProperty("targetRef", out var refNode);
        var targetId = hasId && idNode.ValueKind == JsonValueKind.String
            ? idNode.GetString()
            : null;
        var targetRef = hasRef && refNode.ValueKind == JsonValueKind.String
            ? refNode.GetString()
            : null;
        if (hasId == hasRef ||
            (hasId && !ResourceMaterializationContract.IsExactIdentifier(targetId)) ||
            (hasRef && !ResourceMaterializationContract.IsExactIdentifier(targetRef)) ||
            kind == null || !ResourceDefinitionCatalog.TryParseOwnerKind(kind, out var ownerKind))
        {
            Add(issues, path + ".target", "resource_command_target_invalid",
                "registered owner kind and exactly one exact targetId or targetRef",
                target.GetRawText());
            return null;
        }

        return issues.Count == issueCount
            ? new ResourceCommandTarget(ownerKind, targetId, targetRef)
            : null;
    }

    private static ResourceCommandSource? ParseSource(
        JsonElement command,
        string path,
        List<ValidationIssue> issues)
    {
        if (!command.TryGetProperty("source", out var source) ||
            source.ValueKind != JsonValueKind.Object)
        {
            Add(issues, path + ".source", "resource_command_source_invalid",
                "closed exact registered source selector",
                ResourceMaterializationContract.Describe(command, "source"));
            return null;
        }
        var issueCount = issues.Count;
        ResourceMaterializationContract.ValidateClosedObject(
            source, path + ".source", SourceFields, issues,
            "resource_command_unknown_field");
        var kind = ReadExact(source, "kind", path + ".source", issues);
        var sourceId = ReadExact(source, "sourceId", path + ".source", issues);
        return issues.Count == issueCount && kind != null && sourceId != null
            ? new ResourceCommandSource(kind, sourceId)
            : null;
    }

    private static void ParseDefinitionSelector(
        JsonElement command,
        string path,
        List<ValidationIssue> issues,
        out string? resourceKey,
        out string? definitionRef)
    {
        var hasKey = command.TryGetProperty("resourceKey", out var keyNode);
        var hasRef = command.TryGetProperty("resourceDefinitionRef", out var refNode);
        resourceKey = hasKey && keyNode.ValueKind == JsonValueKind.String
            ? keyNode.GetString()
            : null;
        definitionRef = hasRef && refNode.ValueKind == JsonValueKind.String
            ? refNode.GetString()
            : null;
        if (hasKey == hasRef ||
            (hasKey && !ResourceMaterializationContract.IsExactIdentifier(resourceKey)) ||
            (hasRef && !ResourceMaterializationContract.IsExactIdentifier(definitionRef)))
        {
            Add(issues, path, "resource_command_definition_selector_invalid",
                "exactly one exact resourceKey or resourceDefinitionRef",
                command.GetRawText());
        }
    }

    private static ResourceCapacityProposal? ParseCapacity(
        JsonElement command,
        string path,
        List<ValidationIssue> issues)
    {
        if (!command.TryGetProperty("capacity", out var value))
            return null;
        if (value.ValueKind != JsonValueKind.Object ||
            !value.TryGetProperty("kind", out var kindNode) ||
            kindNode.ValueKind != JsonValueKind.String)
        {
            Add(issues, path + ".capacity", "resource_command_capacity_invalid",
                "closed instance_fixed or registered_formula capacity proposal",
                value.GetRawText());
            return null;
        }

        var kind = kindNode.GetString();
        if (kind == "instance_fixed")
        {
            var issueCount = issues.Count;
            ResourceMaterializationContract.ValidateClosedObject(
                value, path + ".capacity", InstanceCapacityFields, issues,
                "resource_command_unknown_field");
            if (!value.TryGetProperty("maximum", out var maximumNode) ||
                !ResourceMaterializationContract.TryReadExactDecimal(maximumNode, out var maximum) ||
                maximum <= 0m)
            {
                Add(issues, path + ".capacity.maximum",
                    "resource_command_capacity_invalid", "positive exact maximum",
                    ResourceMaterializationContract.Describe(value, "maximum"));
                return null;
            }
            return issues.Count == issueCount
                ? new ResourceCapacityProposal(ResourceCapacityKind.InstanceFixed, maximum, null)
                : null;
        }

        if (kind == "registered_formula")
        {
            var issueCount = issues.Count;
            ResourceMaterializationContract.ValidateClosedObject(
                value, path + ".capacity", FormulaCapacityFields, issues,
                "resource_command_unknown_field");
            var formulaKey = ReadExact(value, "formulaKey", path + ".capacity", issues);
            return issues.Count == issueCount && formulaKey != null
                ? new ResourceCapacityProposal(ResourceCapacityKind.RegisteredFormula, null, formulaKey)
                : null;
        }

        Add(issues, path + ".capacity.kind", "resource_command_capacity_invalid",
            "instance_fixed or registered_formula", kind ?? "null");
        return null;
    }

    private static ResourceCurrentDisposition? ParseDisposition(
        JsonElement command,
        ResourceCapacityOperation? operation,
        string path,
        List<ValidationIssue> issues)
    {
        var present = command.TryGetProperty("currentDisposition", out var node);
        var token = present && node.ValueKind == JsonValueKind.String ? node.GetString() : null;
        if (operation == ResourceCapacityOperation.Initialize)
        {
            if (token != "initialize_from_definition")
                AddCapacityShapeIssue(command, path, issues);
            return token == "initialize_from_definition"
                ? ResourceCurrentDisposition.InitializeFromDefinition
                : null;
        }
        if (operation == ResourceCapacityOperation.Reconfigure)
        {
            var parsed = token switch
            {
                "preserve" => ResourceCurrentDisposition.Preserve,
                "clamp_to_new_maximum" => ResourceCurrentDisposition.ClampToNewMaximum,
                "scale_ratio_exact" => ResourceCurrentDisposition.ScaleRatioExact,
                _ => (ResourceCurrentDisposition?)null
            };
            if (parsed == null)
                AddCapacityShapeIssue(command, path, issues);
            return parsed;
        }
        if (present)
            AddCapacityShapeIssue(command, path, issues);
        return null;
    }

    private static ResourceCapacityOperation? ParseCapacityOperation(
        JsonElement command,
        string path,
        List<ValidationIssue> issues)
    {
        var token = ReadExact(command, "operation", path, issues);
        var result = token switch
        {
            "initialize" => ResourceCapacityOperation.Initialize,
            "reconfigure" => ResourceCapacityOperation.Reconfigure,
            "suspend" => ResourceCapacityOperation.Suspend,
            "resume" => ResourceCapacityOperation.Resume,
            "retire" => ResourceCapacityOperation.Retire,
            _ => (ResourceCapacityOperation?)null
        };
        if (token != null && result == null)
            AddCapacityShapeIssue(command, path, issues);
        return result;
    }

    private static ResourceOperation? ParseOrdinaryOperation(
        JsonElement command,
        string path,
        List<ValidationIssue> issues)
    {
        var token = ReadExact(command, "operation", path, issues);
        var result = token switch
        {
            "damage" => ResourceOperation.Damage,
            "restore" => ResourceOperation.Restore,
            "spend" => ResourceOperation.Spend,
            "gain" => ResourceOperation.Gain,
            _ => (ResourceOperation?)null
        };
        if (token != null && result == null)
        {
            Add(issues, path + ".operation", "resource_command_operation_invalid",
                "damage, restore, spend, or gain", token);
        }
        return result;
    }

    private static bool TryReadArray(
        JsonElement root,
        string field,
        List<ValidationIssue> issues,
        out JsonElement array)
    {
        if (!root.TryGetProperty(field, out array))
        {
            using var empty = JsonDocument.Parse("[]");
            array = empty.RootElement.Clone();
            return true;
        }
        if (array.ValueKind == JsonValueKind.Array)
            return true;
        Add(issues, field, "resource_command_invalid_field", "array", array.GetRawText());
        return false;
    }

    private static bool RequireObject(
        JsonElement value,
        string path,
        List<ValidationIssue> issues)
    {
        if (value.ValueKind == JsonValueKind.Object)
            return true;
        Add(issues, path, "resource_command_invalid_field", "closed command object",
            value.ValueKind.ToString());
        return false;
    }

    private static string? ReadExact(
        JsonElement value,
        string field,
        string path,
        List<ValidationIssue> issues)
    {
        var result = value.TryGetProperty(field, out var node) &&
            node.ValueKind == JsonValueKind.String
                ? node.GetString()
                : null;
        if (ResourceMaterializationContract.IsExactIdentifier(result))
            return result;
        Add(issues, path + "." + field, "resource_command_invalid_field",
            "exact non-empty identifier", ResourceMaterializationContract.Describe(value, field));
        return null;
    }

    private static string? ReadReason(
        JsonElement value,
        string path,
        List<ValidationIssue> issues)
    {
        var reason = value.TryGetProperty("reason", out var node) &&
            node.ValueKind == JsonValueKind.String
                ? node.GetString()
                : null;
        if (ResourceMaterializationContract.IsExactIdentifier(reason) &&
            reason!.Any(char.IsLetterOrDigit))
        {
            return reason;
        }
        Add(issues, path + ".reason", "resource_command_invalid_field",
            "non-empty trimmed readable reason",
            ResourceMaterializationContract.Describe(value, "reason"));
        return null;
    }

    private static void AddCapacityShapeIssue(
        JsonElement command,
        string path,
        List<ValidationIssue> issues) =>
        Add(issues, path, "resource_command_capacity_invalid",
            "registered capacity lifecycle shape and disposition", command.GetRawText());

    private static void Add(
        List<ValidationIssue> issues,
        string path,
        string code,
        string expected,
        string actual,
        IssueCategory category = IssueCategory.StateConsistency) =>
        ResourceMaterializationContract.AddIssue(
            issues, path, code, expected, actual, category);

    private static ResourceCommandCompositionResult Result(
        bool isMissing,
        JsonObject root,
        IEnumerable<ResourceDefinitionCreationCommand>? definitions = null,
        IEnumerable<ResourceCapacityCommand>? capacities = null,
        IEnumerable<ResourceOrdinaryCommand>? changes = null,
        IEnumerable<ValidationIssue>? issues = null) =>
        new(
            isMissing,
            root,
            definitions ?? Array.Empty<ResourceDefinitionCreationCommand>(),
            capacities ?? Array.Empty<ResourceCapacityCommand>(),
            changes ?? Array.Empty<ResourceOrdinaryCommand>(),
            issues ?? Array.Empty<ValidationIssue>());

    private static FrozenSet<string> Set(params string[] values) =>
        values.ToFrozenSet(StringComparer.Ordinal);
}
