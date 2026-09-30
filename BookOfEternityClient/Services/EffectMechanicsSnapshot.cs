using System.Collections.ObjectModel;
using System.Text.Json;
using System.Text.Json.Nodes;
using BookOfEternityClient.Core;

namespace BookOfEternityClient.Services;

internal sealed record EffectMechanicsInput(
    EffectCarrierCatalogInput Carriers,
    JsonObject? IdentityIndex,
    EffectRollSkillScopeAuthority? SkillScopeAuthority = null);

internal sealed record EffectMechanicalComponent(
    string EffectId,
    string Realm,
    string TargetKind,
    string TargetId,
    bool IsPlayerVisible,
    string EffectName,
    string EffectDescription,
    string ComponentId,
    string Profile,
    int Priority,
    int CurrentStacks,
    JsonElement Payload)
{
    internal EffectSourceKey? Source { get; init; }
}

internal sealed record EffectMechanicsAuditEntry(
    string DisplayName,
    string Category,
    string TargetKind,
    string State,
    IReadOnlyList<string> Profiles);

internal sealed record EffectAcceptedInstance(
    string EffectId,
    string Realm,
    string TargetKind,
    string TargetId,
    string OwnerKind,
    string OwnerId,
    string? Category,
    JsonElement CanonicalEffect);

internal sealed record EffectMechanicsSnapshot(
    bool IsAccepted,
    IReadOnlyList<EffectMechanicalComponent> Components,
    IReadOnlyList<EffectMechanicsAuditEntry> Audit,
    IReadOnlyList<ValidationIssue> Issues)
{
    internal const string Source = "accepted_effect_mechanics_snapshot_v1";
    private static readonly EffectRollSkillScopeAuthority EmptySkillScopeAuthority =
        EffectRollSkillScopeAuthority.Build(new EffectRollSkillScopeAuthorityInput(
            new Dictionary<string, JsonNode?>(),
            new Dictionary<string, JsonNode?>()));

    internal IReadOnlyList<EffectAcceptedInstance> Effects { get; init; } =
        Array.Empty<EffectAcceptedInstance>();

    internal IReadOnlyList<FateShieldReactionCandidate> FateShieldReactionCandidates
        { get; init; } = Array.Empty<FateShieldReactionCandidate>();

    internal EffectRollSkillScopeAuthority SkillScopeAuthority { get; init; } = EmptySkillScopeAuthority;

    internal static EffectMechanicsSnapshot Build(EffectMechanicsInput input)
    {
        ArgumentNullException.ThrowIfNull(input);
        ArgumentNullException.ThrowIfNull(input.Carriers);

        var catalog = EffectCarrierCatalog.Build(input.Carriers);
        var issues = new List<ValidationIssue>(catalog.Issues);
        EffectIdentityState? identity = null;

        if (input.IdentityIndex == null)
        {
            if (catalog.Occurrences.Count > 0)
            {
                issues.Add(NewIssue(
                    EffectIdentityState.StatePath,
                    "effect_mechanics_identity_index_missing",
                    "one complete identity index for every active carrier occurrence",
                    "missing"));
            }
        }
        else
        {
            using var document = JsonDocument.Parse(input.IdentityIndex.ToJsonString());
            var parsed = EffectIdentityState.Parse(
                document.RootElement,
                EffectIdentityState.StatePath);
            issues.AddRange(parsed.Issues);
            identity = parsed.State;
        }

        if (identity != null)
            ValidationService.ValidateEffectCarrierIndexAgreement(catalog, identity, issues);

        if (issues.Count > 0)
            return Rejected(issues);

        var components = new List<EffectMechanicalComponent>();
        var audit = new List<EffectMechanicsAuditEntry>();
        var effects = new List<EffectAcceptedInstance>();
        foreach (var occurrence in catalog.Occurrences
                     .OrderBy(static occurrence => occurrence.EffectId, StringComparer.Ordinal))
        {
            var effect = occurrence.Effect;
            var state = ReadExact(effect["state"]) ?? string.Empty;
            var display = effect["display"] as JsonObject;
            var isVisible = string.Equals(
                ReadExact(display?["visibility"]),
                "visible",
                StringComparison.Ordinal);
            var displayName = isVisible
                ? ReadExact(display?["name"]) ?? "Эффект"
                : "Скрытый эффект";
            var displayDescription = isVisible
                ? ReadExact(display?["description"]) ?? string.Empty
                : string.Empty;
            var category = isVisible
                ? ReadExact(display?["category"]) ?? "effect"
                : "hidden";
            var target = effect["target"] as JsonObject;
            var realm = ReadExact(effect["realm"]) ?? string.Empty;
            var targetKind = ReadExact(target?["kind"]) ?? string.Empty;
            var targetId = ReadExact(target?["targetId"]) ?? string.Empty;
            var source = effect["source"] as JsonObject;
            var sourceKey = new EffectSourceKey(
                realm,
                ReadExact(source?["kind"]) ?? string.Empty,
                ReadExact(source?["sourceId"]) ?? string.Empty,
                ReadExact(source?["definitionKey"]) ?? string.Empty);
            var profiles = new List<string>();

            effects.Add(new EffectAcceptedInstance(
                occurrence.EffectId,
                realm,
                targetKind,
                targetId,
                occurrence.Coordinate.Kind,
                occurrence.Coordinate.OwnerId,
                occurrence.Coordinate.Category,
                ToDetachedElement(effect)));

            if (effect["components"] is JsonArray effectComponents)
            {
                foreach (var component in effectComponents.OfType<JsonObject>())
                {
                    var profile = ReadExact(component["profile"]) ?? string.Empty;
                    profiles.Add(profile);
                    if (!string.Equals(state, "active", StringComparison.Ordinal))
                        continue;

                    var payload = component["payload"]!.AsObject();
                    components.Add(new EffectMechanicalComponent(
                        occurrence.EffectId,
                        realm,
                        targetKind,
                        targetId,
                        isVisible,
                        displayName,
                        displayDescription,
                        ReadExact(component["componentId"]) ?? string.Empty,
                        profile,
                        component["priority"]!.GetValue<int>(),
                        ReadPositiveInt(effect["stacking"]?["currentStacks"], 1),
                        ToDetachedElement(payload))
                    {
                        Source = new EffectSourceKey(
                            sourceKey.Realm,
                            sourceKey.Kind,
                            sourceKey.SourceId,
                            sourceKey.DefinitionKey)
                    });
                }
            }

            if (isVisible)
            {
                audit.Add(new EffectMechanicsAuditEntry(
                    displayName,
                    category,
                    targetKind,
                    state,
                    ReadOnly(profiles
                        .Distinct(StringComparer.Ordinal)
                        .OrderBy(static profile => profile, StringComparer.Ordinal))));
            }
        }

        return new EffectMechanicsSnapshot(
            true,
            ReadOnly(components
                .OrderBy(static component => component.Priority)
                .ThenBy(static component => component.EffectId, StringComparer.Ordinal)
                .ThenBy(static component => component.ComponentId, StringComparer.Ordinal)),
            ReadOnly(audit),
            Array.Empty<ValidationIssue>())
        {
            Effects = ReadOnly(effects),
            FateShieldReactionCandidates =
                FateShieldReactionArbiter.ProjectEligibleCandidates(catalog.Occurrences),
            SkillScopeAuthority = input.SkillScopeAuthority ?? EmptySkillScopeAuthority
        };
    }

    internal static async Task<EffectMechanicsSnapshot> LoadAsync(FileSystemManager fs)
    {
        ArgumentNullException.ThrowIfNull(fs);
        await using var readLease = await fs.AcquireCanonicalWriteLeaseAsync(
            CanonicalWritePurpose.PublicationReadQuiescence);
        return await LoadAsync(fs, readLease);
    }

    internal static async Task<EffectRollSkillScopeAuthority> LoadCurrentSkillScopeAuthorityAsync(
        FileSystemManager fs)
    {
        ArgumentNullException.ThrowIfNull(fs);
        await using var readLease = await fs.AcquireCanonicalWriteLeaseAsync(
            CanonicalWritePurpose.PublicationReadQuiescence);
        return await LoadCurrentSkillScopeAuthorityAsync(fs, readLease);
    }

    internal static async Task<EffectRollSkillScopeAuthority> LoadCurrentSkillScopeAuthorityAsync(
        FileSystemManager fs,
        FileSystemManager.CanonicalWriteLease readLease)
    {
        ArgumentNullException.ThrowIfNull(fs);
        ArgumentNullException.ThrowIfNull(readLease);
        fs.EnsureCanonicalWriteLeaseActive(readLease);
        var readIssues = new List<ValidationIssue>();
        var authority = await ReadCurrentSkillScopeAuthorityAsync(fs, readLease, readIssues);
        return readIssues.Count == 0 ? authority : EmptySkillScopeAuthority;
    }

    internal static async Task<EffectMechanicsSnapshot> LoadAsync(
        FileSystemManager fs,
        FileSystemManager.CanonicalWriteLease readLease)
    {
        ArgumentNullException.ThrowIfNull(fs);
        ArgumentNullException.ThrowIfNull(readLease);
        fs.EnsureCanonicalWriteLeaseActive(readLease);
        var readIssues = new List<ValidationIssue>();

        var skillScopeAuthority = await ReadCurrentSkillScopeAuthorityAsync(
            fs,
            readLease,
            readIssues);

        var input = new EffectMechanicsInput(
            new EffectCarrierCatalogInput(
                await ReadObjectAsync(fs, readLease, EffectCarrierCatalog.PlayerPath, readIssues),
                await ReadObjectAsync(fs, readLease, EffectCarrierCatalog.NpcPath, readIssues),
                await ReadObjectAsync(fs, readLease, EffectCarrierCatalog.EnemiesPath, readIssues),
                await ReadObjectAsync(fs, readLease, EffectCarrierCatalog.AlliesPath, readIssues),
                await ReadObjectAsync(fs, readLease, EffectCarrierCatalog.AfterlifeProfilesPath, readIssues),
                await ReadObjectAsync(fs, readLease, EffectCarrierCatalog.SpiritualConflictPath, readIssues)),
            await ReadObjectAsync(fs, readLease, EffectIdentityState.StatePath, readIssues),
            skillScopeAuthority);

        var built = Build(input);
        if (readIssues.Count == 0)
            return built;

        return Rejected(readIssues.Concat(built.Issues));
    }

    private static async Task<EffectRollSkillScopeAuthority> ReadCurrentSkillScopeAuthorityAsync(
        FileSystemManager fs,
        FileSystemManager.CanonicalWriteLease readLease,
        List<ValidationIssue> readIssues)
    {
        var activeSkills = await ReadObjectAsync(
            fs, readLease, "game_state/player/skills_active.json", readIssues);
        var passiveSkills = await ReadObjectAsync(
            fs, readLease, "game_state/player/skills_passive.json", readIssues);
        var npcSkills = await ReadObjectAsync(
            fs, readLease, "game_state/npcs/npc_core.json", readIssues);
        var roots = new Dictionary<string, JsonNode?>(StringComparer.Ordinal)
        {
            ["game_state/player/skills_active.json"] = activeSkills,
            ["game_state/player/skills_passive.json"] = passiveSkills,
            ["game_state/npcs/npc_core.json"] = npcSkills
        };
        return EffectRollSkillScopeAuthority.Build(
            new EffectRollSkillScopeAuthorityInput(roots, roots));
    }

    private static async Task<JsonObject?> ReadObjectAsync(
        FileSystemManager fs,
        FileSystemManager.CanonicalWriteLease readLease,
        string path,
        List<ValidationIssue> issues)
    {
        string? json;
        try
        {
            json = await fs.ReadFileAsync(readLease, path);
        }
        catch (Exception exception) when (
            exception is IOException or UnauthorizedAccessException or InvalidDataException)
        {
            issues.Add(NewIssue(
                path,
                "effect_mechanics_authority_read_failed",
                "readable canonical authority file or absent pristine file",
                exception.Message));
            return null;
        }
        if (json == null)
            return null;

        try
        {
            using var document = JsonDocument.Parse(json);
            if (TryFindDuplicateProperty(document.RootElement, path, out var duplicatePath))
            {
                issues.Add(NewIssue(
                    duplicatePath,
                    "effect_mechanics_duplicate_property",
                    "every authority JSON property occurs exactly once",
                    "duplicate property"));
                return null;
            }
            if (document.RootElement.ValueKind != JsonValueKind.Object)
            {
                issues.Add(NewIssue(
                    path,
                    "effect_mechanics_invalid_authority_root",
                    "canonical object root or absent pristine file",
                    document.RootElement.ValueKind.ToString()));
                return null;
            }
            return JsonNode.Parse(document.RootElement.GetRawText())!.AsObject();
        }
        catch (Exception exception) when (
            exception is JsonException or InvalidOperationException or ArgumentException)
        {
            issues.Add(NewIssue(
                path,
                "effect_mechanics_invalid_authority_root",
                "parseable canonical object root or absent pristine file",
                exception.Message));
            return null;
        }
    }

    private static bool TryFindDuplicateProperty(
        JsonElement value,
        string path,
        out string duplicatePath)
    {
        if (value.ValueKind == JsonValueKind.Object)
        {
            var names = new HashSet<string>(StringComparer.Ordinal);
            foreach (var property in value.EnumerateObject())
            {
                var propertyPath = path + "." + property.Name;
                if (!names.Add(property.Name))
                {
                    duplicatePath = propertyPath;
                    return true;
                }
                if (TryFindDuplicateProperty(property.Value, propertyPath, out duplicatePath))
                    return true;
            }
        }
        else if (value.ValueKind == JsonValueKind.Array)
        {
            var index = 0;
            foreach (var item in value.EnumerateArray())
            {
                if (TryFindDuplicateProperty(item, $"{path}[{index++}]", out duplicatePath))
                    return true;
            }
        }

        duplicatePath = string.Empty;
        return false;
    }

    private static JsonElement ToDetachedElement(JsonNode value)
    {
        using var document = JsonDocument.Parse(value.ToJsonString());
        return document.RootElement.Clone();
    }

    private static string? ReadExact(JsonNode? node)
    {
        if (node is not JsonValue value ||
            !value.TryGetValue<string>(out var text) ||
            string.IsNullOrWhiteSpace(text) ||
            !string.Equals(text, text.Trim(), StringComparison.Ordinal))
        {
            return null;
        }
        return text;
    }

    private static int ReadPositiveInt(JsonNode? node, int fallback) =>
        node is JsonValue value &&
        value.TryGetValue<int>(out var result) &&
        result > 0
            ? result
            : fallback;

    private static IReadOnlyList<T> ReadOnly<T>(IEnumerable<T> values) =>
        new ReadOnlyCollection<T>(values.ToArray());

    private static EffectMechanicsSnapshot Rejected(IEnumerable<ValidationIssue> issues) =>
        new(
            false,
            Array.Empty<EffectMechanicalComponent>(),
            Array.Empty<EffectMechanicsAuditEntry>(),
            ReadOnly(issues));

    private static ValidationIssue NewIssue(
        string path,
        string code,
        string expected,
        string actual) =>
        new(
            path,
            IssueSeverity.Error,
            "Active effect mechanics require one complete accepted carrier and identity snapshot.",
            code: code,
            section: "effect_materialization",
            expected: expected,
            actual: actual,
            repairHint: "Restore the complete accepted effect carrier/index set before deriving any mechanics.");
}
