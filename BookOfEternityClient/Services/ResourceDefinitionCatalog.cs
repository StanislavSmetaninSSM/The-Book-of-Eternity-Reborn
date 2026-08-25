using System.Collections.Frozen;
using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace BookOfEternityClient.Services;

internal enum ResourceNumericKind
{
    Integer,
    Decimal
}

internal enum ResourceOwnerKind
{
    Player,
    Npc,
    Combatant,
    CombatGroupMember,
    Vehicle,
    Item,
    AfterlifeActor,
    AfterlifeConflictSide,
    AfterlifeScope
}

internal enum ResourceOperation
{
    Damage,
    Restore,
    Spend,
    Gain
}

internal enum ResourceMinimumKind
{
    DefinitionFixed
}

internal enum ResourceCapacityKind
{
    DefinitionFixed,
    InstanceFixed,
    RegisteredFormula
}

internal enum ResourceInitializationKind
{
    Minimum,
    Maximum,
    Fixed,
    RegisteredFormula
}

internal enum ResourceBoundPolicy
{
    RejectBelowMinimum,
    ClampToMinimum,
    RejectAboveMaximum,
    ClampToMaximum
}

internal enum ResourceVisibility
{
    PlayerVisible,
    OwnerVisible,
    GmOnly,
    Hidden
}

internal sealed record ResourceMinimumPolicy(
    ResourceMinimumKind Kind,
    decimal Value);

internal sealed record ResourceCapacityPolicy(
    ResourceCapacityKind Kind,
    decimal? Value,
    string? FormulaKey);

internal sealed record ResourceInitializationPolicy(
    ResourceInitializationKind Kind,
    decimal? Value,
    string? FormulaKey);

internal sealed record ResourceDefinitionMaterialization(
    int SchemaVersion,
    string DefinitionId,
    string Seal,
    int CreatedAtTurn,
    string CreatedEventRef);

internal sealed record ResourceDefinitionIdentity(
    string DefinitionId,
    string Seal);

internal sealed record ResourceDefinition(
    string ResourceKey,
    int DefinitionVersion,
    string DisplayName,
    ResourceNumericKind NumericKind,
    string Unit,
    decimal Quantum,
    ResourceMinimumPolicy MinimumPolicy,
    ResourceCapacityPolicy CapacityPolicy,
    ResourceInitializationPolicy InitializationPolicy,
    IReadOnlySet<ResourceOwnerKind> AllowedOwnerKinds,
    IReadOnlySet<ResourceOperation> AllowedOperations,
    ResourceBoundPolicy FloorPolicy,
    ResourceBoundPolicy CapPolicy,
    ResourceVisibility Visibility,
    ResourceDefinitionMaterialization Materialization);

internal sealed record ResourceDefinitionCatalogResult(
    ResourceDefinitionCatalog? Catalog,
    IReadOnlyList<ValidationIssue> Issues,
    bool IsMissing = false)
{
    internal bool IsValid => Issues.Count == 0;
}

internal sealed record ResourceDefinitionMaterializationResult(
    ResourceDefinition? Definition,
    IReadOnlyList<ValidationIssue> Issues)
{
    internal bool IsValid => Definition != null && Issues.Count == 0;
}

internal sealed class ResourceDefinitionCatalog
{
    private static readonly FrozenSet<string> RawDefinitionFields = Set(
        "resourceKey",
        "definitionVersion",
        "displayName",
        "numericKind",
        "unit",
        "quantum",
        "minimumPolicy",
        "capacityPolicy",
        "initializationPolicy",
        "allowedOwnerKinds",
        "allowedOperations",
        "defaultFloorPolicy",
        "defaultCapPolicy",
        "visibility");

    private static readonly FrozenSet<string> CanonicalDefinitionFields =
        RawDefinitionFields.Append("materialization")
            .ToFrozenSet(StringComparer.Ordinal);

    private static readonly FrozenSet<string> OutOfScopeResourceKeys = new[]
        {
            "money",
            "ink_feathers",
            "light_sparks",
            "treasury_balance",
            "faction_resource_ledger",
            "experience",
            "mastery",
            "level",
            "reputation",
            "relationship",
            "owner_bond_level",
            "spiritual_power",
            "spiritual_strain",
            "spiritual_shield",
            "effect_stacks",
            "effect_uses",
            "effect_duration",
            "turns_remaining",
            "qte_progress",
            "qte_mistakes",
            "qte_noise",
            "lock_pin_durability",
            "item_stack_count",
            "project_fuel",
            "free_shape",
            "free_retune"
        }
        .Select(ResourceMaterializationContract.BuildConfusableKey)
        .ToFrozenSet(StringComparer.Ordinal);

    private static readonly FrozenSet<string> MinimumFixedFields = Set("kind", "value");
    private static readonly FrozenSet<string> KindOnlyFields = Set("kind");
    private static readonly FrozenSet<string> FormulaFields = Set("kind", "formulaKey");
    private static readonly FrozenSet<string> MaterializationFields = Set(
        "schemaVersion", "definitionId", "seal", "createdAtTurn", "createdEventRef");

    private static readonly FrozenDictionary<string, ResourceNumericKind> NumericKinds =
        Map(
            ("integer", ResourceNumericKind.Integer),
            ("decimal", ResourceNumericKind.Decimal));

    private static readonly FrozenDictionary<string, ResourceOwnerKind> OwnerKinds =
        Map(
            ("player", ResourceOwnerKind.Player),
            ("npc", ResourceOwnerKind.Npc),
            ("combatant", ResourceOwnerKind.Combatant),
            ("combat_group_member", ResourceOwnerKind.CombatGroupMember),
            ("vehicle", ResourceOwnerKind.Vehicle),
            ("item", ResourceOwnerKind.Item),
            ("afterlife_actor", ResourceOwnerKind.AfterlifeActor),
            ("afterlife_conflict_side", ResourceOwnerKind.AfterlifeConflictSide),
            ("afterlife_scope", ResourceOwnerKind.AfterlifeScope));

    private static readonly FrozenDictionary<string, ResourceOperation> Operations =
        Map(
            ("damage", ResourceOperation.Damage),
            ("restore", ResourceOperation.Restore),
            ("spend", ResourceOperation.Spend),
            ("gain", ResourceOperation.Gain));

    private static readonly FrozenDictionary<string, ResourceBoundPolicy> FloorPolicies =
        Map(
            ("reject_below_minimum", ResourceBoundPolicy.RejectBelowMinimum),
            ("clamp_to_minimum", ResourceBoundPolicy.ClampToMinimum));

    private static readonly FrozenDictionary<string, ResourceBoundPolicy> CapPolicies =
        Map(
            ("reject_above_maximum", ResourceBoundPolicy.RejectAboveMaximum),
            ("clamp_to_maximum", ResourceBoundPolicy.ClampToMaximum));

    private static readonly FrozenDictionary<string, ResourceVisibility> Visibilities =
        Map(
            ("player_visible", ResourceVisibility.PlayerVisible),
            ("owner_visible", ResourceVisibility.OwnerVisible),
            ("gm_only", ResourceVisibility.GmOnly),
            ("hidden", ResourceVisibility.Hidden));

    private readonly FrozenDictionary<string, ResourceDefinition> _byKey;

    private ResourceDefinitionCatalog(
        IEnumerable<ResourceDefinition> definitions,
        ResourceAuthorityWorkMeter? workMeter = null)
    {
        workMeter?.BuildDefinitionCatalog();
        var candidates = definitions.ToArray();
        foreach (var _ in candidates)
            workMeter?.VisitDefinitionConstruction();
        IComparer<ResourceDefinition> comparer = ResourceDefinitionComparer.Instance;
        if (workMeter != null)
        {
            comparer = new ResourceAuthorityCountingComparer<ResourceDefinition>(
                comparer,
                workMeter.CompareDefinitions);
        }
        Definitions = candidates
            .OrderBy(static definition => definition, comparer)
            .ToArray();
        foreach (var _ in Definitions)
            workMeter?.VisitDefinitionIndex();
        _byKey = Definitions.ToFrozenDictionary(
            static definition => definition.ResourceKey,
            StringComparer.Ordinal);
    }

    internal IReadOnlyList<ResourceDefinition> Definitions { get; }

    internal static ResourceDefinitionCatalog CreateBuiltIn() =>
        new(CreateBuiltInDefinitions());

    internal ResourceDefinitionCatalog With(ResourceDefinition definition)
    {
        ArgumentNullException.ThrowIfNull(definition);
        return new ResourceDefinitionCatalog(Definitions.Append(definition));
    }

    internal ResourceDefinitionCatalog WithRange(
        IEnumerable<ResourceDefinition> definitions,
        ResourceAuthorityWorkMeter? workMeter = null)
    {
        ArgumentNullException.ThrowIfNull(definitions);
        return new ResourceDefinitionCatalog(Definitions.Concat(definitions), workMeter);
    }

    internal JsonObject ToCanonicalRoot() => new()
    {
        ["schemaVersion"] = ResourceMaterializationContract.SchemaVersion,
        ["definitions"] = new JsonArray(Definitions
            .Select(static definition => (JsonNode)ToCanonicalDefinition(definition))
            .ToArray())
    };

    internal string ToCanonicalJson() => ToCanonicalRoot().ToJsonString();

    internal bool TryResolveExact(string resourceKey, out ResourceDefinition? definition) =>
        _byKey.TryGetValue(resourceKey, out definition);

    internal static ResourceDefinitionCatalogResult ParseCanonical(
        string? json,
        bool allowMissingPristine,
        ResourceAuthorityWorkMeter? workMeter = null)
    {
        var rootResult = ResourceMaterializationContract.ParseDefinitions(
            json,
            allowMissingPristine);
        if (!rootResult.IsValid || rootResult.Root == null)
        {
            return rootResult.IsMissing && rootResult.IsValid
                ? new ResourceDefinitionCatalogResult(
                    new ResourceDefinitionCatalog(
                        Array.Empty<ResourceDefinition>(),
                        workMeter),
                    Array.Empty<ValidationIssue>(),
                    IsMissing: true)
                : new ResourceDefinitionCatalogResult(null, rootResult.Issues, rootResult.IsMissing);
        }

        var issues = new List<ValidationIssue>();
        var definitions = new List<ResourceDefinition>();
        var exactKeys = new HashSet<string>(StringComparer.Ordinal);
        var confusableKeys = new HashSet<string>(StringComparer.Ordinal);
        var exactDefinitionIds = new HashSet<string>(StringComparer.Ordinal);
        var confusableDefinitionIds = new HashSet<string>(StringComparer.Ordinal);
        var exactSeals = new HashSet<string>(StringComparer.Ordinal);
        var confusableSeals = new HashSet<string>(StringComparer.Ordinal);
        var index = 0;
        foreach (var element in rootResult.Root.Value
                     .GetProperty("definitions")
                     .EnumerateArray())
        {
            workMeter?.VisitDefinitionDescriptor();
            var path = $"{ResourceMaterializationContract.DefinitionsPath}.definitions[{index++}]";
            var definition = ParseDefinition(
                element,
                path,
                requireMaterialization: true,
                issues);
            if (definition == null)
                continue;

            if (!exactKeys.Add(definition.ResourceKey))
            {
                Add(
                    issues,
                    path + ".resourceKey",
                    "resource_definition_duplicate_key",
                    "one exact resourceKey in the sealed catalog",
                    definition.ResourceKey);
            }
            else if (!confusableKeys.Add(
                         ResourceMaterializationContract.BuildConfusableKey(
                             definition.ResourceKey)))
            {
                Add(
                    issues,
                    path + ".resourceKey",
                    "resource_definition_confusable_key",
                    "one exact/confusable resourceKey in the sealed catalog",
                    definition.ResourceKey);
            }

            ValidateUniqueMaterializationValue(
                definition.Materialization.DefinitionId,
                path + ".materialization.definitionId",
                exactDefinitionIds,
                confusableDefinitionIds,
                "resource_definition_duplicate_materialization_id",
                "resource_definition_confusable_materialization_id",
                issues);
            ValidateUniqueMaterializationValue(
                definition.Materialization.Seal,
                path + ".materialization.seal",
                exactSeals,
                confusableSeals,
                "resource_definition_duplicate_seal",
                "resource_definition_confusable_seal",
                issues);

            definitions.Add(definition);
        }

        return issues.Count == 0
            ? new ResourceDefinitionCatalogResult(
                new ResourceDefinitionCatalog(definitions, workMeter),
                Array.Empty<ValidationIssue>())
            : new ResourceDefinitionCatalogResult(null, issues.ToArray());
    }

    internal static IReadOnlyList<ValidationIssue> ValidateRawProposal(
        JsonElement proposal,
        string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        var issues = new List<ValidationIssue>();
        ResourceMaterializationContract.FindDuplicateProperties(
            proposal,
            path,
            issues,
            "resource_materialization_duplicate_property");
        issues.AddRange(ResourceMaterializationContract.ValidateRawDefinitionFields(
            proposal,
            path));
        _ = ParseDefinition(
            proposal,
            path,
            requireMaterialization: false,
            issues);
        return issues.ToArray();
    }

    internal static ResourceDefinitionMaterializationResult MaterializeProposal(
        JsonElement proposal,
        ResourceDefinitionCatalog existingCatalog,
        int createdAtTurn,
        string? createdEventRef,
        Func<ResourceDefinitionIdentity> allocateIdentity)
    {
        ArgumentNullException.ThrowIfNull(existingCatalog);
        return BeginMaterializationBatch(existingCatalog)
            .MaterializeProposal(
                proposal,
                createdAtTurn,
                createdEventRef,
                allocateIdentity);
    }

    internal static MaterializationBatch BeginMaterializationBatch(
        ResourceDefinitionCatalog existingCatalog,
        ResourceAuthorityWorkMeter? workMeter = null) =>
        new(existingCatalog, workMeter);

    internal sealed class MaterializationBatch
    {
        private const string Path = "resourceDefinitionCreation.definition";

        private readonly ResourceDefinitionCatalog _existingCatalog;
        private readonly ResourceAuthorityWorkMeter? _workMeter;
        private readonly List<ResourceDefinition> _accepted = new();
        private readonly HashSet<string> _exactKeys = new(StringComparer.Ordinal);
        private readonly HashSet<string> _confusableKeys = new(StringComparer.Ordinal);
        private readonly HashSet<string> _exactDefinitionIds = new(StringComparer.Ordinal);
        private readonly HashSet<string> _confusableDefinitionIds = new(StringComparer.Ordinal);
        private readonly HashSet<string> _exactSeals = new(StringComparer.Ordinal);
        private readonly HashSet<string> _confusableSeals = new(StringComparer.Ordinal);
        private bool _frozen;

        internal MaterializationBatch(
            ResourceDefinitionCatalog existingCatalog,
            ResourceAuthorityWorkMeter? workMeter)
        {
            _existingCatalog = existingCatalog ??
                throw new ArgumentNullException(nameof(existingCatalog));
            _workMeter = workMeter;
            foreach (var definition in existingCatalog.Definitions)
            {
                _workMeter?.VisitDefinitionMaterialization();
                AddIdentity(
                    definition.ResourceKey,
                    _exactKeys,
                    _confusableKeys);
                AddIdentity(
                    definition.Materialization.DefinitionId,
                    _exactDefinitionIds,
                    _confusableDefinitionIds);
                AddIdentity(
                    definition.Materialization.Seal,
                    _exactSeals,
                    _confusableSeals);
            }
        }

        internal ResourceDefinitionMaterializationResult MaterializeProposal(
            JsonElement proposal,
            int createdAtTurn,
            string? createdEventRef,
            Func<ResourceDefinitionIdentity> allocateIdentity)
        {
            EnsureMutable();
            ArgumentNullException.ThrowIfNull(allocateIdentity);
            _workMeter?.VisitDefinitionMaterialization();
            var issues = new List<ValidationIssue>();

            var definitionCount = _existingCatalog.Definitions.Count + _accepted.Count;
            if (definitionCount >= ResourceMaterializationContract.MaxDefinitions)
            {
                Add(
                    issues,
                    "resourceDefinitionCreation",
                    "resource_definition_limit_exceeded",
                    $"fewer than {ResourceMaterializationContract.MaxDefinitions} existing definitions before creation",
                    definitionCount.ToString(CultureInfo.InvariantCulture));
            }

            ResourceMaterializationContract.FindDuplicateProperties(
                proposal,
                Path,
                issues,
                "resource_materialization_duplicate_property");
            issues.AddRange(ResourceMaterializationContract.ValidateRawDefinitionFields(
                proposal,
                Path));
            var parsed = ParseDefinition(
                proposal,
                Path,
                requireMaterialization: false,
                issues);
            if (createdAtTurn < 0)
            {
                Add(
                    issues,
                    Path + ".materialization.createdAtTurn",
                    "resource_definition_invalid_materialization",
                    "non-negative accepted turn",
                    createdAtTurn.ToString(CultureInfo.InvariantCulture));
            }
            var validatedCreatedEventRef =
                ResourceMaterializationContract.IsExactIdentifier(createdEventRef)
                    ? createdEventRef
                    : null;
            if (validatedCreatedEventRef == null)
            {
                Add(
                    issues,
                    Path + ".materialization.createdEventRef",
                    "resource_definition_invalid_materialization",
                    "exact accepted event reference",
                    createdEventRef ?? "null");
            }

            if (parsed != null)
            {
                ValidateIndexedIdentity(
                    parsed.ResourceKey,
                    Path + ".resourceKey",
                    _exactKeys,
                    _confusableKeys,
                    "resource_definition_rewrite_forbidden",
                    "resource_definition_confusable_key",
                    "new exact resourceKey",
                    "new exact/confusable resourceKey",
                    issues);
            }

            if (parsed == null || validatedCreatedEventRef == null || issues.Count != 0)
                return new ResourceDefinitionMaterializationResult(null, issues.ToArray());

            var identity = allocateIdentity();
            if (identity == null ||
                !ResourceMaterializationContract.IsExactIdentifier(identity.DefinitionId) ||
                !ResourceMaterializationContract.IsExactIdentifier(identity.Seal))
            {
                Add(
                    issues,
                    Path + ".materialization",
                    "resource_definition_invalid_materialization",
                    "client-generated exact definition identity and seal",
                    identity == null
                        ? "null"
                        : $"definitionId={identity.DefinitionId}; seal={identity.Seal}");
                return new ResourceDefinitionMaterializationResult(null, issues.ToArray());
            }

            ValidateIndexedIdentity(
                identity.DefinitionId,
                Path + ".materialization.definitionId",
                _exactDefinitionIds,
                _confusableDefinitionIds,
                "resource_definition_duplicate_materialization_id",
                "resource_definition_confusable_materialization_id",
                "new exact client-owned materialization identity",
                "new exact/confusable client-owned materialization identity",
                issues);
            ValidateIndexedIdentity(
                identity.Seal,
                Path + ".materialization.seal",
                _exactSeals,
                _confusableSeals,
                "resource_definition_duplicate_seal",
                "resource_definition_confusable_seal",
                "new exact client-owned materialization identity",
                "new exact/confusable client-owned materialization identity",
                issues);
            if (issues.Count != 0)
                return new ResourceDefinitionMaterializationResult(null, issues.ToArray());

            var materialized = parsed with
            {
                Materialization = new ResourceDefinitionMaterialization(
                    ResourceMaterializationContract.SchemaVersion,
                    identity.DefinitionId,
                    identity.Seal,
                    createdAtTurn,
                    validatedCreatedEventRef)
            };
            AddIdentity(materialized.ResourceKey, _exactKeys, _confusableKeys);
            AddIdentity(
                materialized.Materialization.DefinitionId,
                _exactDefinitionIds,
                _confusableDefinitionIds);
            AddIdentity(
                materialized.Materialization.Seal,
                _exactSeals,
                _confusableSeals);
            _accepted.Add(materialized);
            return new ResourceDefinitionMaterializationResult(
                materialized,
                Array.Empty<ValidationIssue>());
        }

        internal ResourceDefinitionCatalog Freeze()
        {
            EnsureMutable();
            _frozen = true;
            return _existingCatalog.WithRange(_accepted, _workMeter);
        }

        private static void ValidateIndexedIdentity(
            string value,
            string path,
            IReadOnlySet<string> exactValues,
            IReadOnlySet<string> confusableValues,
            string duplicateCode,
            string confusableCode,
            string exactExpectation,
            string confusableExpectation,
            List<ValidationIssue> issues)
        {
            if (exactValues.Contains(value))
            {
                Add(
                    issues,
                    path,
                    duplicateCode,
                    exactExpectation,
                    value);
                return;
            }

            if (confusableValues.Contains(
                    ResourceMaterializationContract.BuildConfusableKey(value)))
            {
                Add(
                    issues,
                    path,
                    confusableCode,
                    confusableExpectation,
                    value);
            }
        }

        private static void AddIdentity(
            string value,
            HashSet<string> exactValues,
            HashSet<string> confusableValues)
        {
            exactValues.Add(value);
            confusableValues.Add(
                ResourceMaterializationContract.BuildConfusableKey(value));
        }

        private void EnsureMutable()
        {
            if (_frozen)
            {
                throw new InvalidOperationException(
                    "A frozen resource definition materialization batch cannot be reused.");
            }
        }
    }

    internal static bool TryParseOwnerKind(string token, out ResourceOwnerKind kind) =>
        OwnerKinds.TryGetValue(token, out kind);

    internal static string GetOwnerKindToken(ResourceOwnerKind kind) => kind switch
    {
        ResourceOwnerKind.Player => "player",
        ResourceOwnerKind.Npc => "npc",
        ResourceOwnerKind.Combatant => "combatant",
        ResourceOwnerKind.CombatGroupMember => "combat_group_member",
        ResourceOwnerKind.Vehicle => "vehicle",
        ResourceOwnerKind.Item => "item",
        ResourceOwnerKind.AfterlifeActor => "afterlife_actor",
        ResourceOwnerKind.AfterlifeConflictSide => "afterlife_conflict_side",
        ResourceOwnerKind.AfterlifeScope => "afterlife_scope",
        _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, null)
    };

    private static JsonObject ToCanonicalDefinition(ResourceDefinition definition)
    {
        var capacity = new JsonObject
        {
            ["kind"] = definition.CapacityPolicy.Kind switch
            {
                ResourceCapacityKind.DefinitionFixed => "definition_fixed",
                ResourceCapacityKind.InstanceFixed => "instance_fixed",
                ResourceCapacityKind.RegisteredFormula => "registered_formula",
                _ => throw new ArgumentOutOfRangeException()
            }
        };
        if (definition.CapacityPolicy.Value.HasValue)
            capacity["value"] = definition.CapacityPolicy.Value.Value;
        if (definition.CapacityPolicy.FormulaKey != null)
            capacity["formulaKey"] = definition.CapacityPolicy.FormulaKey;

        var initialization = new JsonObject
        {
            ["kind"] = definition.InitializationPolicy.Kind switch
            {
                ResourceInitializationKind.Minimum => "minimum",
                ResourceInitializationKind.Maximum => "maximum",
                ResourceInitializationKind.Fixed => "fixed",
                ResourceInitializationKind.RegisteredFormula => "registered_formula",
                _ => throw new ArgumentOutOfRangeException()
            }
        };
        if (definition.InitializationPolicy.Value.HasValue)
            initialization["value"] = definition.InitializationPolicy.Value.Value;
        if (definition.InitializationPolicy.FormulaKey != null)
            initialization["formulaKey"] = definition.InitializationPolicy.FormulaKey;

        return new JsonObject
        {
            ["resourceKey"] = definition.ResourceKey,
            ["definitionVersion"] = definition.DefinitionVersion,
            ["displayName"] = definition.DisplayName,
            ["numericKind"] = definition.NumericKind == ResourceNumericKind.Integer
                ? "integer"
                : "decimal",
            ["unit"] = definition.Unit,
            ["quantum"] = definition.Quantum,
            ["minimumPolicy"] = new JsonObject
            {
                ["kind"] = "definition_fixed",
                ["value"] = definition.MinimumPolicy.Value
            },
            ["capacityPolicy"] = capacity,
            ["initializationPolicy"] = initialization,
            ["allowedOwnerKinds"] = new JsonArray(definition.AllowedOwnerKinds
                .OrderBy(GetOwnerKindToken, StringComparer.Ordinal)
                .Select(static kind => (JsonNode)GetOwnerKindToken(kind))
                .ToArray()),
            ["allowedOperations"] = new JsonArray(definition.AllowedOperations
                .Select(static operation => operation switch
                {
                    ResourceOperation.Damage => "damage",
                    ResourceOperation.Restore => "restore",
                    ResourceOperation.Spend => "spend",
                    ResourceOperation.Gain => "gain",
                    _ => throw new ArgumentOutOfRangeException()
                })
                .OrderBy(static token => token, StringComparer.Ordinal)
                .Select(static token => (JsonNode)token)
                .ToArray()),
            ["defaultFloorPolicy"] = definition.FloorPolicy switch
            {
                ResourceBoundPolicy.RejectBelowMinimum => "reject_below_minimum",
                ResourceBoundPolicy.ClampToMinimum => "clamp_to_minimum",
                _ => throw new InvalidOperationException("Invalid floor policy.")
            },
            ["defaultCapPolicy"] = definition.CapPolicy switch
            {
                ResourceBoundPolicy.RejectAboveMaximum => "reject_above_maximum",
                ResourceBoundPolicy.ClampToMaximum => "clamp_to_maximum",
                _ => throw new InvalidOperationException("Invalid cap policy.")
            },
            ["visibility"] = definition.Visibility switch
            {
                ResourceVisibility.PlayerVisible => "player_visible",
                ResourceVisibility.OwnerVisible => "owner_visible",
                ResourceVisibility.GmOnly => "gm_only",
                ResourceVisibility.Hidden => "hidden",
                _ => throw new ArgumentOutOfRangeException()
            },
            ["materialization"] = new JsonObject
            {
                ["schemaVersion"] = definition.Materialization.SchemaVersion,
                ["definitionId"] = definition.Materialization.DefinitionId,
                ["seal"] = definition.Materialization.Seal,
                ["createdAtTurn"] = definition.Materialization.CreatedAtTurn,
                ["createdEventRef"] = definition.Materialization.CreatedEventRef
            }
        };
    }

    private static ResourceDefinition? ParseDefinition(
        JsonElement element,
        string path,
        bool requireMaterialization,
        List<ValidationIssue> issues)
    {
        if (element.ValueKind != JsonValueKind.Object)
        {
            Add(
                issues,
                path,
                "resource_definition_invalid_field",
                "closed resource definition object",
                element.ValueKind.ToString());
            return null;
        }

        var startIssueCount = issues.Count;
        var allowedFields = requireMaterialization
            ? CanonicalDefinitionFields
            : RawDefinitionFields;
        ResourceMaterializationContract.ValidateClosedObject(
            element,
            path,
            allowedFields,
            issues,
            "resource_definition_unknown_field");
        foreach (var field in allowedFields)
        {
            if (!element.TryGetProperty(field, out _))
            {
                Add(
                    issues,
                    path + "." + field,
                    "resource_definition_missing_field",
                    "required complete definition field",
                    "missing");
            }
        }

        var resourceKey = ReadExactIdentifier(element, path, "resourceKey", issues);
        if (resourceKey != null &&
            OutOfScopeResourceKeys.Contains(
                ResourceMaterializationContract.BuildConfusableKey(resourceKey)))
        {
            Add(
                issues,
                path + ".resourceKey",
                "resource_definition_out_of_scope_key",
                "a bounded scalar reserve outside currency, accounting, progression, relationship, and specialized-axis authority",
                resourceKey);
        }
        var version = ReadExactInt(element, path, "definitionVersion", 1, issues);
        var displayName = ReadReadableString(element, path, "displayName", issues);
        var numericKind = ReadClosedToken(
            element,
            path,
            "numericKind",
            NumericKinds,
            issues);
        var unit = ReadExactIdentifier(element, path, "unit", issues);
        var quantum = ReadDecimal(element, path, "quantum", issues);
        var minimumPolicy = ParseMinimumPolicy(element, path, issues);
        var capacityPolicy = ParseCapacityPolicy(element, path, issues);
        var initializationPolicy = ParseInitializationPolicy(element, path, issues);
        var ownerKinds = ParseClosedArray(
            element,
            path,
            "allowedOwnerKinds",
            OwnerKinds,
            "owner_kind",
            issues);
        var operations = ParseClosedArray(
            element,
            path,
            "allowedOperations",
            Operations,
            "operation",
            issues);
        var floorPolicy = ReadClosedToken(
            element,
            path,
            "defaultFloorPolicy",
            FloorPolicies,
            issues);
        var capPolicy = ReadClosedToken(
            element,
            path,
            "defaultCapPolicy",
            CapPolicies,
            issues);
        var visibility = ReadClosedToken(
            element,
            path,
            "visibility",
            Visibilities,
            issues);
        var materialization = requireMaterialization
            ? ParseMaterialization(element, path, issues)
            : PlaceholderMaterialization;

        if (numericKind.HasValue &&
            quantum.HasValue &&
            minimumPolicy != null &&
            capacityPolicy != null &&
            initializationPolicy != null &&
            ownerKinds != null)
        {
            ValidateNumericPolicy(
                numericKind.Value,
                quantum.Value,
                minimumPolicy,
                capacityPolicy,
                initializationPolicy,
                ownerKinds,
                path,
                issues);
        }

        if (issues.Count != startIssueCount ||
            resourceKey == null ||
            !version.HasValue ||
            displayName == null ||
            !numericKind.HasValue ||
            unit == null ||
            !quantum.HasValue ||
            minimumPolicy == null ||
            capacityPolicy == null ||
            initializationPolicy == null ||
            ownerKinds == null ||
            operations == null ||
            !floorPolicy.HasValue ||
            !capPolicy.HasValue ||
            !visibility.HasValue ||
            materialization == null)
        {
            return null;
        }

        return new ResourceDefinition(
            resourceKey,
            version.Value,
            displayName,
            numericKind.Value,
            unit,
            quantum.Value,
            minimumPolicy,
            capacityPolicy,
            initializationPolicy,
            ownerKinds,
            operations,
            floorPolicy.Value,
            capPolicy.Value,
            visibility.Value,
            materialization);
    }

    private static ResourceMinimumPolicy? ParseMinimumPolicy(
        JsonElement root,
        string path,
        List<ValidationIssue> issues)
    {
        var policyPath = path + ".minimumPolicy";
        if (!TryGetObject(root, "minimumPolicy", policyPath, issues, out var policy))
            return null;
        ResourceMaterializationContract.ValidateClosedObject(
            policy,
            policyPath,
            MinimumFixedFields,
            issues,
            "resource_definition_unknown_field");
        var kind = ReadExactIdentifier(policy, policyPath, "kind", issues);
        var value = ReadDecimal(policy, policyPath, "value", issues);
        if (!string.Equals(kind, "definition_fixed", StringComparison.Ordinal))
        {
            Add(
                issues,
                policyPath + ".kind",
                "resource_definition_invalid_field",
                "definition_fixed",
                kind ?? "missing");
            return null;
        }

        return value.HasValue
            ? new ResourceMinimumPolicy(ResourceMinimumKind.DefinitionFixed, value.Value)
            : null;
    }

    private static ResourceCapacityPolicy? ParseCapacityPolicy(
        JsonElement root,
        string path,
        List<ValidationIssue> issues)
    {
        var policyPath = path + ".capacityPolicy";
        if (!TryGetObject(root, "capacityPolicy", policyPath, issues, out var policy))
            return null;
        var kind = ReadExactIdentifier(policy, policyPath, "kind", issues);
        switch (kind)
        {
            case "definition_fixed":
                ResourceMaterializationContract.ValidateClosedObject(
                    policy,
                    policyPath,
                    MinimumFixedFields,
                    issues,
                    "resource_definition_unknown_field");
                var value = ReadDecimal(policy, policyPath, "value", issues);
                return value.HasValue
                    ? new ResourceCapacityPolicy(
                        ResourceCapacityKind.DefinitionFixed,
                        value.Value,
                        null)
                    : null;

            case "instance_fixed":
                ResourceMaterializationContract.ValidateClosedObject(
                    policy,
                    policyPath,
                    KindOnlyFields,
                    issues,
                    "resource_definition_unknown_field");
                return new ResourceCapacityPolicy(
                    ResourceCapacityKind.InstanceFixed,
                    null,
                    null);

            case "registered_formula":
                ResourceMaterializationContract.ValidateClosedObject(
                    policy,
                    policyPath,
                    FormulaFields,
                    issues,
                    "resource_definition_unknown_field");
                var formulaKey = ReadExactIdentifier(
                    policy,
                    policyPath,
                    "formulaKey",
                    issues);
                if (formulaKey != null &&
                    !ResourceCapacityFormulaCatalog.IsRegisteredFormulaKey(formulaKey))
                {
                    Add(
                        issues,
                        policyPath + ".formulaKey",
                        "resource_definition_formula_unknown",
                        "registered client capacity formula key",
                        formulaKey);
                    return null;
                }
                return formulaKey == null
                    ? null
                    : new ResourceCapacityPolicy(
                        ResourceCapacityKind.RegisteredFormula,
                        null,
                        formulaKey);

            default:
                Add(
                    issues,
                    policyPath + ".kind",
                    "resource_definition_invalid_field",
                    "definition_fixed | instance_fixed | registered_formula",
                    kind ?? "missing");
                return null;
        }
    }

    private static ResourceInitializationPolicy? ParseInitializationPolicy(
        JsonElement root,
        string path,
        List<ValidationIssue> issues)
    {
        var policyPath = path + ".initializationPolicy";
        if (!TryGetObject(root, "initializationPolicy", policyPath, issues, out var policy))
            return null;
        var kind = ReadExactIdentifier(policy, policyPath, "kind", issues);
        switch (kind)
        {
            case "minimum":
                ResourceMaterializationContract.ValidateClosedObject(
                    policy, policyPath, KindOnlyFields, issues, "resource_definition_unknown_field");
                return new ResourceInitializationPolicy(
                    ResourceInitializationKind.Minimum, null, null);
            case "maximum":
                ResourceMaterializationContract.ValidateClosedObject(
                    policy, policyPath, KindOnlyFields, issues, "resource_definition_unknown_field");
                return new ResourceInitializationPolicy(
                    ResourceInitializationKind.Maximum, null, null);
            case "fixed":
                ResourceMaterializationContract.ValidateClosedObject(
                    policy, policyPath, MinimumFixedFields, issues, "resource_definition_unknown_field");
                var value = ReadDecimal(policy, policyPath, "value", issues);
                return value.HasValue
                    ? new ResourceInitializationPolicy(
                        ResourceInitializationKind.Fixed, value.Value, null)
                    : null;
            case "registered_formula":
                ResourceMaterializationContract.ValidateClosedObject(
                    policy, policyPath, FormulaFields, issues, "resource_definition_unknown_field");
                var formulaKey = ReadExactIdentifier(
                    policy,
                    policyPath,
                    "formulaKey",
                    issues);
                if (formulaKey != null &&
                    !ResourceCapacityFormulaCatalog.IsRegisteredFormulaKey(formulaKey))
                {
                    Add(
                        issues,
                        policyPath + ".formulaKey",
                        "resource_definition_formula_unknown",
                        "registered client initialization formula key",
                        formulaKey);
                    return null;
                }
                return formulaKey == null
                    ? null
                    : new ResourceInitializationPolicy(
                        ResourceInitializationKind.RegisteredFormula,
                        null,
                        formulaKey);
            default:
                Add(
                    issues,
                    policyPath + ".kind",
                    "resource_definition_invalid_field",
                    "minimum | maximum | fixed | registered_formula",
                    kind ?? "missing");
                return null;
        }
    }

    private static ResourceDefinitionMaterialization? ParseMaterialization(
        JsonElement root,
        string path,
        List<ValidationIssue> issues)
    {
        var materializationPath = path + ".materialization";
        if (!TryGetObject(
                root,
                "materialization",
                materializationPath,
                issues,
                out var materialization))
        {
            return null;
        }

        ResourceMaterializationContract.ValidateClosedObject(
            materialization,
            materializationPath,
            MaterializationFields,
            issues,
            "resource_definition_unknown_field");
        var schemaVersion = ReadExactInt(
            materialization,
            materializationPath,
            "schemaVersion",
            ResourceMaterializationContract.SchemaVersion,
            issues);
        var definitionId = ReadExactIdentifier(
            materialization,
            materializationPath,
            "definitionId",
            issues);
        var seal = ReadExactIdentifier(
            materialization,
            materializationPath,
            "seal",
            issues);
        var turn = ReadNonNegativeInt(
            materialization,
            materializationPath,
            "createdAtTurn",
            issues);
        var eventRef = ReadExactIdentifier(
            materialization,
            materializationPath,
            "createdEventRef",
            issues);
        return schemaVersion.HasValue && definitionId != null && seal != null &&
               turn.HasValue && eventRef != null
            ? new ResourceDefinitionMaterialization(
                schemaVersion.Value,
                definitionId,
                seal,
                turn.Value,
                eventRef)
            : null;
    }

    private static void ValidateNumericPolicy(
        ResourceNumericKind numericKind,
        decimal quantum,
        ResourceMinimumPolicy minimum,
        ResourceCapacityPolicy capacity,
        ResourceInitializationPolicy initialization,
        IReadOnlySet<ResourceOwnerKind> ownerKinds,
        string path,
        List<ValidationIssue> issues)
    {
        var invalid = quantum <= 0m;
        if (numericKind == ResourceNumericKind.Integer)
        {
            invalid |= !ResourceMaterializationContract.IsIntegral(quantum) ||
                       !ResourceMaterializationContract.IsIntegral(minimum.Value);
        }

        if (capacity.Kind == ResourceCapacityKind.DefinitionFixed)
        {
            var maximum = capacity.Value!.Value;
            invalid |= maximum <= minimum.Value ||
                       !ResourceMaterializationContract.IsQuantumAligned(
                           maximum,
                           minimum.Value,
                           quantum);
            if (numericKind == ResourceNumericKind.Integer)
                invalid |= !ResourceMaterializationContract.IsIntegral(maximum);

            if (initialization.Kind == ResourceInitializationKind.Fixed &&
                initialization.Value.HasValue)
            {
                invalid |= initialization.Value.Value < minimum.Value ||
                           initialization.Value.Value > maximum;
            }
        }

        if (initialization.Kind == ResourceInitializationKind.Fixed &&
            initialization.Value.HasValue)
        {
            invalid |= initialization.Value.Value < minimum.Value ||
                       !ResourceMaterializationContract.IsQuantumAligned(
                initialization.Value.Value,
                minimum.Value,
                quantum);
            if (numericKind == ResourceNumericKind.Integer)
                invalid |= !ResourceMaterializationContract.IsIntegral(
                    initialization.Value.Value);
        }

        if (invalid)
        {
            Add(
                issues,
                path,
                "resource_definition_invalid_numeric_policy",
                "positive exact quantum and definition values aligned to numeric kind/minimum/capacity",
                $"numericKind={numericKind}; quantum={quantum}; minimum={minimum.Value}; capacity={capacity.Value}; initialization={initialization.Value}");
        }

        ValidateFormulaOwnerCompatibility(
            capacity.FormulaKey,
            ownerKinds,
            path + ".capacityPolicy.formulaKey",
            issues);
        ValidateFormulaOwnerCompatibility(
            initialization.FormulaKey,
            ownerKinds,
            path + ".initializationPolicy.formulaKey",
            issues);
    }

    private static void ValidateFormulaOwnerCompatibility(
        string? formulaKey,
        IReadOnlySet<ResourceOwnerKind> ownerKinds,
        string path,
        List<ValidationIssue> issues)
    {
        if (formulaKey == null ||
            ResourceCapacityFormulaCatalog.SupportsEveryOwnerKind(formulaKey, ownerKinds))
        {
            return;
        }

        Add(
            issues,
            path,
            "resource_definition_formula_owner_mismatch",
            "registered formula supporting every allowed owner kind",
            formulaKey);
    }

    private static FrozenSet<T>? ParseClosedArray<T>(
        JsonElement root,
        string path,
        string field,
        IReadOnlyDictionary<string, T> catalog,
        string label,
        List<ValidationIssue> issues)
        where T : struct, Enum
    {
        var arrayPath = path + "." + field;
        if (!root.TryGetProperty(field, out var array) ||
            array.ValueKind != JsonValueKind.Array ||
            array.GetArrayLength() == 0)
        {
            Add(
                issues,
                arrayPath,
                "resource_definition_invalid_field",
                $"non-empty registered {label} array",
                ResourceMaterializationContract.Describe(root, field));
            return null;
        }

        var result = new HashSet<T>();
        var exact = new HashSet<string>(StringComparer.Ordinal);
        var confusable = new HashSet<string>(StringComparer.Ordinal);
        var index = 0;
        foreach (var value in array.EnumerateArray())
        {
            var valuePath = $"{arrayPath}[{index++}]";
            if (value.ValueKind != JsonValueKind.String ||
                !ResourceMaterializationContract.IsExactIdentifier(value.GetString()))
            {
                Add(
                    issues,
                    valuePath,
                    "resource_definition_invalid_field",
                    $"exact registered {label}",
                    value.GetRawText());
                continue;
            }

            var token = value.GetString()!;
            if (!exact.Add(token))
            {
                Add(
                    issues,
                    valuePath,
                    $"resource_definition_duplicate_{label}",
                    $"one exact {label}",
                    token);
                continue;
            }
            if (!confusable.Add(ResourceMaterializationContract.BuildConfusableKey(token)))
            {
                Add(
                    issues,
                    valuePath,
                    $"resource_definition_confusable_{label}",
                    $"one exact/confusable {label}",
                    token);
                continue;
            }
            if (!catalog.TryGetValue(token, out var parsed))
            {
                Add(
                    issues,
                    valuePath,
                    "resource_definition_invalid_field",
                    $"registered {label}",
                    token);
                continue;
            }

            result.Add(parsed);
        }

        return result.Count == 0 ? null : result.ToFrozenSet();
    }

    private static void ValidateUniqueMaterializationValue(
        string value,
        string path,
        HashSet<string> exactValues,
        HashSet<string> confusableValues,
        string duplicateCode,
        string confusableCode,
        List<ValidationIssue> issues)
    {
        if (!exactValues.Add(value))
        {
            Add(
                issues,
                path,
                duplicateCode,
                "one exact client-owned materialization identity",
                value);
            return;
        }

        if (!confusableValues.Add(ResourceMaterializationContract.BuildConfusableKey(value)))
        {
            Add(
                issues,
                path,
                confusableCode,
                "one exact/confusable client-owned materialization identity",
                value);
        }
    }

    private static void ValidateNewMaterializationValue(
        string value,
        string path,
        IEnumerable<string> existingValues,
        string duplicateCode,
        string confusableCode,
        List<ValidationIssue> issues)
    {
        var values = existingValues.ToArray();
        if (values.Any(existing => string.Equals(existing, value, StringComparison.Ordinal)))
        {
            Add(
                issues,
                path,
                duplicateCode,
                "new exact client-owned materialization identity",
                value);
            return;
        }

        var confusable = ResourceMaterializationContract.BuildConfusableKey(value);
        if (values.Any(existing => string.Equals(
                ResourceMaterializationContract.BuildConfusableKey(existing),
                confusable,
                StringComparison.Ordinal)))
        {
            Add(
                issues,
                path,
                confusableCode,
                "new exact/confusable client-owned materialization identity",
                value);
        }
    }

    private static string? ReadExactIdentifier(
        JsonElement root,
        string path,
        string field,
        List<ValidationIssue> issues)
    {
        if (root.TryGetProperty(field, out var value) &&
            value.ValueKind == JsonValueKind.String &&
            ResourceMaterializationContract.IsExactIdentifier(value.GetString()))
        {
            return value.GetString();
        }

        Add(
            issues,
            path + "." + field,
            "resource_definition_invalid_field",
            "exact non-empty normalization-stable string",
            ResourceMaterializationContract.Describe(root, field));
        return null;
    }

    private static string? ReadReadableString(
        JsonElement root,
        string path,
        string field,
        List<ValidationIssue> issues)
    {
        if (root.TryGetProperty(field, out var value) &&
            value.ValueKind == JsonValueKind.String &&
            ResourceMaterializationContract.IsExactIdentifier(value.GetString()))
        {
            return value.GetString();
        }

        Add(
            issues,
            path + "." + field,
            "resource_definition_invalid_field",
            "non-empty normalized display string without surrounding whitespace",
            ResourceMaterializationContract.Describe(root, field));
        return null;
    }

    private static decimal? ReadDecimal(
        JsonElement root,
        string path,
        string field,
        List<ValidationIssue> issues)
    {
        if (root.TryGetProperty(field, out var value) &&
            ResourceMaterializationContract.TryReadExactDecimal(value, out var number))
        {
            return number;
        }

        Add(
            issues,
            path + "." + field,
            "resource_definition_invalid_field",
            "exact JSON decimal number",
            ResourceMaterializationContract.Describe(root, field));
        return null;
    }

    private static int? ReadExactInt(
        JsonElement root,
        string path,
        string field,
        int expected,
        List<ValidationIssue> issues)
    {
        if (root.TryGetProperty(field, out var value) &&
            value.ValueKind == JsonValueKind.Number &&
            value.TryGetInt32(out var number) &&
            number == expected)
        {
            return number;
        }

        Add(
            issues,
            path + "." + field,
            "resource_definition_invalid_field",
            expected.ToString(CultureInfo.InvariantCulture),
            ResourceMaterializationContract.Describe(root, field));
        return null;
    }

    private static int? ReadNonNegativeInt(
        JsonElement root,
        string path,
        string field,
        List<ValidationIssue> issues)
    {
        if (root.TryGetProperty(field, out var value) &&
            value.ValueKind == JsonValueKind.Number &&
            value.TryGetInt32(out var number) &&
            number >= 0)
        {
            return number;
        }

        Add(
            issues,
            path + "." + field,
            "resource_definition_invalid_materialization",
            "non-negative integer",
            ResourceMaterializationContract.Describe(root, field));
        return null;
    }

    private static T? ReadClosedToken<T>(
        JsonElement root,
        string path,
        string field,
        IReadOnlyDictionary<string, T> catalog,
        List<ValidationIssue> issues)
        where T : struct, Enum
    {
        var token = ReadExactIdentifier(root, path, field, issues);
        if (token != null && catalog.TryGetValue(token, out var value))
            return value;

        if (token != null)
        {
            Add(
                issues,
                path + "." + field,
                "resource_definition_invalid_field",
                string.Join(" | ", catalog.Keys.OrderBy(static key => key, StringComparer.Ordinal)),
                token);
        }
        return null;
    }

    private static bool TryGetObject(
        JsonElement root,
        string field,
        string path,
        List<ValidationIssue> issues,
        out JsonElement value)
    {
        if (root.TryGetProperty(field, out value) &&
            value.ValueKind == JsonValueKind.Object)
        {
            return true;
        }

        Add(
            issues,
            path,
            "resource_definition_invalid_field",
            "object",
            ResourceMaterializationContract.Describe(root, field));
        return false;
    }

    private static IReadOnlyList<ResourceDefinition> CreateBuiltInDefinitions()
    {
        var minimum = new ResourceMinimumPolicy(ResourceMinimumKind.DefinitionFixed, 0m);
        var instanceFixed = new ResourceCapacityPolicy(
            ResourceCapacityKind.InstanceFixed, null, null);
        var initializeMaximum = new ResourceInitializationPolicy(
            ResourceInitializationKind.Maximum, null, null);
        var initializeMinimum = new ResourceInitializationPolicy(
            ResourceInitializationKind.Minimum, null, null);

        return
        [
            BuiltIn(
                "health", "Здоровье", "percent_point", minimum,
                new ResourceCapacityPolicy(
                    ResourceCapacityKind.RegisteredFormula,
                    null,
                    ResourceCapacityFormulaCatalog.MortalHealthCapacityV1),
                initializeMaximum,
                Owners(
                    ResourceOwnerKind.Player,
                    ResourceOwnerKind.Npc,
                    ResourceOwnerKind.Combatant,
                    ResourceOwnerKind.CombatGroupMember,
                    ResourceOwnerKind.Vehicle),
                OperationsSet(ResourceOperation.Damage, ResourceOperation.Restore),
                ResourceBoundPolicy.ClampToMinimum,
                ResourceBoundPolicy.ClampToMaximum),
            BuiltIn(
                "energy", "Энергия", "point", minimum,
                new ResourceCapacityPolicy(
                    ResourceCapacityKind.RegisteredFormula,
                    null,
                    ResourceCapacityFormulaCatalog.MortalEnergyCapacityV1),
                initializeMaximum,
                Owners(ResourceOwnerKind.Player, ResourceOwnerKind.Npc, ResourceOwnerKind.Combatant),
                OperationsSet(ResourceOperation.Spend, ResourceOperation.Gain),
                ResourceBoundPolicy.RejectBelowMinimum,
                ResourceBoundPolicy.ClampToMaximum),
            BuiltIn(
                "poise", "Стойкость", "point", minimum,
                new ResourceCapacityPolicy(
                    ResourceCapacityKind.RegisteredFormula,
                    null,
                    ResourceCapacityFormulaCatalog.MortalPoiseCapacityV1),
                initializeMaximum,
                Owners(ResourceOwnerKind.Player, ResourceOwnerKind.Npc, ResourceOwnerKind.Combatant, ResourceOwnerKind.CombatGroupMember),
                OperationsSet(ResourceOperation.Damage, ResourceOperation.Restore),
                ResourceBoundPolicy.ClampToMinimum,
                ResourceBoundPolicy.ClampToMaximum),
            BuiltIn(
                "durability", "Прочность", "point", minimum, instanceFixed,
                initializeMaximum,
                Owners(ResourceOwnerKind.Item),
                OperationsSet(ResourceOperation.Damage, ResourceOperation.Restore),
                ResourceBoundPolicy.ClampToMinimum,
                ResourceBoundPolicy.ClampToMaximum),
            BuiltIn(
                "charges", "Заряды", "charge", minimum, instanceFixed,
                initializeMaximum,
                Owners(ResourceOwnerKind.Item),
                OperationsSet(ResourceOperation.Spend, ResourceOperation.Gain),
                ResourceBoundPolicy.RejectBelowMinimum,
                ResourceBoundPolicy.ClampToMaximum),
            BuiltIn(
                "ammunition", "Боезапас", "round", minimum, instanceFixed,
                initializeMaximum,
                Owners(ResourceOwnerKind.Item),
                OperationsSet(ResourceOperation.Spend, ResourceOperation.Gain),
                ResourceBoundPolicy.RejectBelowMinimum,
                ResourceBoundPolicy.ClampToMaximum),
            BuiltIn(
                "spiritual_action_points", "Очки духовного действия", "point", minimum,
                new ResourceCapacityPolicy(
                    ResourceCapacityKind.RegisteredFormula,
                    null,
                    ResourceCapacityFormulaCatalog.AfterlifeSpiritualActionPointsV1),
                initializeMaximum,
                Owners(ResourceOwnerKind.AfterlifeActor, ResourceOwnerKind.AfterlifeConflictSide),
                OperationsSet(ResourceOperation.Spend, ResourceOperation.Gain),
                ResourceBoundPolicy.RejectBelowMinimum,
                ResourceBoundPolicy.ClampToMaximum),
            BuiltIn(
                "gacha_attempts", "Попытки призыва", "attempt", minimum,
                new ResourceCapacityPolicy(
                    ResourceCapacityKind.RegisteredFormula,
                    null,
                    ResourceCapacityFormulaCatalog.AfterlifeReturnGachaAttemptsV1),
                initializeMaximum,
                Owners(ResourceOwnerKind.AfterlifeActor, ResourceOwnerKind.AfterlifeScope),
                OperationsSet(ResourceOperation.Spend, ResourceOperation.Gain),
                ResourceBoundPolicy.RejectBelowMinimum,
                ResourceBoundPolicy.ClampToMaximum),
            BuiltIn(
                "blessing_rerolls", "Перебросы благословения", "reroll", minimum,
                instanceFixed,
                initializeMinimum,
                Owners(ResourceOwnerKind.AfterlifeActor),
                OperationsSet(ResourceOperation.Spend, ResourceOperation.Gain),
                ResourceBoundPolicy.RejectBelowMinimum,
                ResourceBoundPolicy.ClampToMaximum)
        ];
    }

    private static ResourceDefinition BuiltIn(
        string key,
        string displayName,
        string unit,
        ResourceMinimumPolicy minimum,
        ResourceCapacityPolicy capacity,
        ResourceInitializationPolicy initialization,
        FrozenSet<ResourceOwnerKind> owners,
        FrozenSet<ResourceOperation> operations,
        ResourceBoundPolicy floorPolicy,
        ResourceBoundPolicy capPolicy) =>
        new(
            key,
            1,
            displayName,
            ResourceNumericKind.Integer,
            unit,
            1m,
            minimum,
            capacity,
            initialization,
            owners,
            operations,
            floorPolicy,
            capPolicy,
            ResourceVisibility.PlayerVisible,
            new ResourceDefinitionMaterialization(
                ResourceMaterializationContract.SchemaVersion,
                $"resource_definition_builtin_{key}_v1",
                $"resource_definition_seal_builtin_{key}_v1",
                0,
                "bootstrap_0"));

    private static ResourceDefinitionMaterialization PlaceholderMaterialization { get; } =
        new(0, string.Empty, string.Empty, 0, string.Empty);

    private static FrozenSet<ResourceOwnerKind> Owners(params ResourceOwnerKind[] values) =>
        values.ToFrozenSet();

    private static FrozenSet<ResourceOperation> OperationsSet(params ResourceOperation[] values) =>
        values.ToFrozenSet();

    private static FrozenSet<string> Set(params string[] values) =>
        values.ToFrozenSet(StringComparer.Ordinal);

    private static FrozenDictionary<string, T> Map<T>(params (string Key, T Value)[] values)
        where T : struct, Enum =>
        values.ToFrozenDictionary(static pair => pair.Key, static pair => pair.Value, StringComparer.Ordinal);

    private sealed class ResourceDefinitionComparer : IComparer<ResourceDefinition>
    {
        internal static ResourceDefinitionComparer Instance { get; } = new();

        public int Compare(ResourceDefinition? left, ResourceDefinition? right) =>
            StringComparer.Ordinal.Compare(left?.ResourceKey, right?.ResourceKey);
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
            actual,
            code.Contains("client", StringComparison.Ordinal)
                ? IssueCategory.ClientOwnedSurface
                : IssueCategory.StateConsistency);
}
