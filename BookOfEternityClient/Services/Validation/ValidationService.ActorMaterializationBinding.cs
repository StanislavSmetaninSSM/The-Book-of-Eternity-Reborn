using System.Text.Json;

namespace BookOfEternityClient.Services;

public partial class ValidationService
{
    private const string GuardiansStatePath = "game_state/meta/guardians.json";

    private enum AfterlifeBindingSourceKind
    {
        Guardian,
        Resident,
        RadiantActor,
        SarefAgent
    }

    private readonly record struct AfterlifeActorBinding(
        string ActorType,
        string ActorId,
        string Context,
        bool HasTypeSpecificMemory)
    {
        public string IdentityKey => BuildAfterlifeActorIdentityKey(ActorType, ActorId);
    }

    private readonly record struct AfterlifeBindingSourceAuthority(
        string? CurrentJson,
        string? PreTurnJson,
        bool IsUsable);

    private async Task ValidateAcceptedTurnAfterlifeActorProfileBindingsAsync(
        JsonElement currentProfilesRoot,
        ValidationPendingTurnSnapshotManifest manifest,
        List<ValidationIssue> issues)
    {
        if (!TryBuildExactAfterlifeProfileIndex(currentProfilesRoot, out var profilesByIdentity))
        {
            AddUnusableAfterlifeActorBindingCurrentAuthorityIssue(
                AfterlifeEntityProfileState.StatePath,
                issues);
            return;
        }

        var currentSourceActors = new Dictionary<string, AfterlifeActorBinding>(StringComparer.Ordinal);
        var requiredBindings = new Dictionary<string, AfterlifeActorBinding>(StringComparer.Ordinal);
        foreach (var (path, kind) in new[]
                 {
                     (GuardiansStatePath, AfterlifeBindingSourceKind.Guardian),
                     (GuardianAbodeResidentState.StatePath, AfterlifeBindingSourceKind.Resident),
                     (ShiningAbodeState.StatePath, AfterlifeBindingSourceKind.RadiantActor),
                     (SarefMainStoryState.StatePath, AfterlifeBindingSourceKind.SarefAgent)
                 })
        {
            var authority = await ReadAfterlifeBindingSourceAuthorityAsync(manifest, path);
            if (!authority.IsUsable)
            {
                AddUnusableAfterlifeActorMaterializationPreTurnAuthorityIssue(path, issues);
                return;
            }

            if (!TryReadAfterlifeBindingSourceActors(
                    authority.CurrentJson,
                    path,
                    kind,
                    out var currentActors))
            {
                AddUnusableAfterlifeActorBindingCurrentAuthorityIssue(path, issues);
                return;
            }
            if (!TryReadAfterlifeBindingSourceActors(
                    authority.PreTurnJson,
                    path,
                    kind,
                    out var preTurnActors))
            {
                AddUnusableAfterlifeActorMaterializationPreTurnAuthorityIssue(path, issues);
                return;
            }

            foreach (var actor in currentActors.Values)
            {
                currentSourceActors[actor.IdentityKey] = actor;
                if (!preTurnActors.ContainsKey(actor.IdentityKey))
                    requiredBindings[actor.IdentityKey] = actor;
            }

            if (kind == AfterlifeBindingSourceKind.RadiantActor)
            {
                if (!TryReadShiningLeadershipBindings(authority.CurrentJson, out var currentLeadership))
                {
                    AddUnusableAfterlifeActorBindingCurrentAuthorityIssue(path, issues);
                    return;
                }
                if (!TryReadShiningLeadershipBindings(authority.PreTurnJson, out var preTurnLeadership))
                {
                    AddUnusableAfterlifeActorMaterializationPreTurnAuthorityIssue(path, issues);
                    return;
                }

                foreach (var actor in currentLeadership.Values)
                {
                    if (!preTurnLeadership.ContainsKey(actor.IdentityKey))
                        requiredBindings[actor.IdentityKey] = actor;
                }
            }
        }

        var guardianJournalMemory = await ReadGuardianThoughtJournalMemoryActorIdsAsync();
        foreach (var required in requiredBindings.Values)
        {
            if (!profilesByIdentity.TryGetValue(required.IdentityKey, out var matches) || matches.Count == 0)
            {
                AddMissingAfterlifeActorProfileIssue(required, issues);
                continue;
            }

            if (matches.Count != 1)
            {
                AddAmbiguousAfterlifeActorProfileIssue(required, matches.Count, issues);
                continue;
            }

            var (profile, context) = matches[0];
            issues.AddRange(ActorMaterializationContract.ValidateCanonicalAfterlifeProfile(
                profile,
                context,
                requireEnvelope: true));

            var hasTypeSpecificMemory = required.HasTypeSpecificMemory ||
                                        (currentSourceActors.TryGetValue(required.IdentityKey, out var sourceActor) &&
                                         sourceActor.HasTypeSpecificMemory) ||
                                        (string.Equals(required.ActorType, "guardian", StringComparison.Ordinal) &&
                                         guardianJournalMemory.Contains(required.ActorId));
            if (!hasTypeSpecificMemory && !HasCommonAfterlifeActorMemory(profile))
                AddMissingAfterlifeActorMemoryIssue(required, issues);
        }
    }

    private async Task<AfterlifeBindingSourceAuthority> ReadAfterlifeBindingSourceAuthorityAsync(
        ValidationPendingTurnSnapshotManifest manifest,
        string path)
    {
        var currentJson = await _fs.ReadFileAsync(path);
        var hasSnapshotEntry = manifest.Files?.ContainsKey(path) == true;
        var wasBaselineTracked = manifest.RollbackBaselineFiles?.Contains(path, StringComparer.OrdinalIgnoreCase) == true;
        if (hasSnapshotEntry != wasBaselineTracked)
            return new AfterlifeBindingSourceAuthority(currentJson, null, IsUsable: false);
        if (!hasSnapshotEntry)
            return new AfterlifeBindingSourceAuthority(currentJson, null, IsUsable: true);

        var preTurnJson = await ReadValidatedPendingTurnSnapshotFileAsync(manifest, path);
        return new AfterlifeBindingSourceAuthority(currentJson, preTurnJson, preTurnJson != null);
    }

    private static bool TryBuildExactAfterlifeProfileIndex(
        JsonElement root,
        out Dictionary<string, List<(JsonElement Profile, string Context)>> result)
    {
        result = new Dictionary<string, List<(JsonElement, string)>>(StringComparer.Ordinal);
        if (root.ValueKind != JsonValueKind.Object ||
            !root.TryGetProperty(AfterlifeEntityProfileState.ProfilesProperty, out var profiles) ||
            profiles.ValueKind != JsonValueKind.Array)
        {
            return false;
        }

        var index = 0;
        foreach (var profile in profiles.EnumerateArray())
        {
            var context = $"{AfterlifeEntityProfileState.StatePath}.{AfterlifeEntityProfileState.ProfilesProperty}[{index++}]";
            if (!TryReadExactNonEmptyString(profile, "actorType", out var actorType) ||
                !TryReadExactNonEmptyString(profile, "actorId", out var actorId) ||
                IsPlayerSoulIdentity(actorType, actorId))
            {
                continue;
            }

            var identityKey = BuildAfterlifeActorIdentityKey(actorType, actorId);
            if (!result.TryGetValue(identityKey, out var matches))
            {
                matches = new List<(JsonElement, string)>();
                result[identityKey] = matches;
            }

            matches.Add((profile, context));
        }

        return true;
    }

    private static bool TryReadAfterlifeBindingSourceActors(
        string? json,
        string path,
        AfterlifeBindingSourceKind kind,
        out Dictionary<string, AfterlifeActorBinding> result)
    {
        result = new Dictionary<string, AfterlifeActorBinding>(StringComparer.Ordinal);
        if (json == null)
            return true;

        try
        {
            using var document = JsonDocument.Parse(json);
            var root = document.RootElement;
            return kind switch
            {
                AfterlifeBindingSourceKind.Guardian => TryCollectGuardianBindings(root, path, result),
                AfterlifeBindingSourceKind.Resident => TryCollectResidentBindings(root, path, result),
                AfterlifeBindingSourceKind.RadiantActor => TryCollectRadiantActorBindings(root, path, result),
                AfterlifeBindingSourceKind.SarefAgent => TryCollectSarefAgentBindings(root, path, result),
                _ => false
            };
        }
        catch (JsonException)
        {
            return false;
        }
    }

    private static bool TryCollectGuardianBindings(
        JsonElement root,
        string path,
        Dictionary<string, AfterlifeActorBinding> result)
    {
        if (!TryReadOptionalArray(root, "guardians", out var guardians))
            return false;

        var index = 0;
        foreach (var guardian in guardians.EnumerateArray())
        {
            var context = $"{path}.guardians[{index++}]";
            if (!TryReadExactNonEmptyString(guardian, "guardianId", out var actorId))
                return false;
            var binding = new AfterlifeActorBinding(
                "guardian",
                actorId,
                context,
                HasNonEmptyObjectArray(guardian, "musings"));
            if (!result.TryAdd(binding.IdentityKey, binding))
                return false;
        }

        return true;
    }

    private static bool TryCollectResidentBindings(
        JsonElement root,
        string path,
        Dictionary<string, AfterlifeActorBinding> result)
    {
        if (!TryReadOptionalArray(root, GuardianAbodeResidentState.EntriesProperty, out var residents))
            return false;

        var residentMemory = ReadExactJournalActorIds(
            root,
            GuardianAbodeResidentState.ThoughtJournalProperty,
            "residentId");
        var index = 0;
        foreach (var resident in residents.EnumerateArray())
        {
            var context = $"{path}.{GuardianAbodeResidentState.EntriesProperty}[{index++}]";
            if (!TryReadExactNonEmptyString(resident, "residentId", out var actorId))
                return false;
            var binding = new AfterlifeActorBinding(
                "resident",
                actorId,
                context,
                residentMemory.Contains(actorId));
            if (!result.TryAdd(binding.IdentityKey, binding))
                return false;
        }

        return true;
    }

    private static bool TryCollectRadiantActorBindings(
        JsonElement root,
        string path,
        Dictionary<string, AfterlifeActorBinding> result)
    {
        if (!TryReadOptionalArray(root, "shiningPoliticalActors", out var actors))
            return false;

        var index = 0;
        foreach (var actor in actors.EnumerateArray())
        {
            var context = $"{path}.shiningPoliticalActors[{index++}]";
            if (!TryReadExactNonEmptyString(actor, "actorType", out var actorType) ||
                !string.Equals(actorType, "radiant_actor", StringComparison.Ordinal) ||
                !TryReadExactNonEmptyString(actor, "actorId", out var actorId))
            {
                return false;
            }
            var binding = new AfterlifeActorBinding("radiant_actor", actorId, context, false);
            if (!result.TryAdd(binding.IdentityKey, binding))
                return false;
        }

        return true;
    }

    private static bool TryCollectSarefAgentBindings(
        JsonElement root,
        string path,
        Dictionary<string, AfterlifeActorBinding> result)
    {
        if (root.ValueKind != JsonValueKind.Object)
            return false;
        if (!root.TryGetProperty("factionLinks", out var factionLinks) ||
            factionLinks.ValueKind == JsonValueKind.Null)
        {
            return true;
        }
        if (factionLinks.ValueKind != JsonValueKind.Object ||
            !TryReadOptionalArray(factionLinks, "knownAgents", out var agents))
        {
            return false;
        }

        var index = 0;
        foreach (var agent in agents.EnumerateArray())
        {
            var context = $"{path}.factionLinks.knownAgents[{index++}]";
            if (!TryReadExactNonEmptyString(agent, "agentId", out var actorId))
                return false;
            var binding = new AfterlifeActorBinding("saref_agent", actorId, context, false);
            if (!result.TryAdd(binding.IdentityKey, binding))
                return false;
        }

        return true;
    }

    private static bool TryReadShiningLeadershipBindings(
        string? json,
        out Dictionary<string, AfterlifeActorBinding> result)
    {
        result = new Dictionary<string, AfterlifeActorBinding>(StringComparer.Ordinal);
        if (json == null)
            return true;

        try
        {
            using var document = JsonDocument.Parse(json);
            var root = document.RootElement;
            if (!TryReadOptionalArray(root, "factions", out var factions))
                return false;

            var index = 0;
            foreach (var faction in factions.EnumerateArray())
            {
                var context = $"{ShiningAbodeState.StatePath}.factions[{index++}].leadership";
                if (faction.ValueKind != JsonValueKind.Object ||
                    !TryReadExactOptionalProperty(faction, "leadership", out var leadership, out var hasLeadership))
                {
                    return false;
                }

                if (!hasLeadership)
                    continue;
                if (leadership.ValueKind != JsonValueKind.Object ||
                    !TryReadExactNonEmptyString(leadership, "leadershipState", out var state))
                {
                    return false;
                }

                if (string.Equals(state, ShiningAbodeState.LeadershipStateVacant, StringComparison.Ordinal))
                {
                    if (!TryReadExactOptionalProperty(leadership, "headActorType", out var vacantActorType, out var hasVacantActorType) ||
                        !TryReadExactOptionalProperty(leadership, "headActorId", out var vacantActorId, out var hasVacantActorId) ||
                        hasVacantActorType && !IsEmptyLeadershipSlot(vacantActorType) ||
                        hasVacantActorId && !IsEmptyLeadershipSlot(vacantActorId))
                    {
                        return false;
                    }

                    continue;
                }

                if (state is not (ShiningAbodeState.LeadershipStateSecure or ShiningAbodeState.LeadershipStateContested))
                    return false;

                if (!TryReadExactNonEmptyString(leadership, "headActorType", out var actorType) ||
                    !TryReadExactNonEmptyString(leadership, "headActorId", out var actorId))
                {
                    return false;
                }

                if (IsPlayerSoulIdentity(actorType, actorId))
                    continue;
                if (actorType is not ("guardian" or "resident" or "radiant_actor"))
                    return false;

                var binding = new AfterlifeActorBinding(actorType, actorId, context, false);
                if (!result.TryAdd(binding.IdentityKey, binding))
                    return false;
            }

            return true;
        }
        catch (JsonException)
        {
            return false;
        }
    }

    private async Task<HashSet<string>> ReadGuardianThoughtJournalMemoryActorIdsAsync()
    {
        var json = await _fs.ReadFileAsync(GuardianThoughtJournalState.StatePath);
        if (string.IsNullOrWhiteSpace(json))
            return new HashSet<string>(StringComparer.Ordinal);

        try
        {
            using var document = JsonDocument.Parse(json);
            return ReadExactJournalActorIds(document.RootElement, "entries", "guardianId");
        }
        catch (JsonException)
        {
            return new HashSet<string>(StringComparer.Ordinal);
        }
    }

    private static HashSet<string> ReadExactJournalActorIds(
        JsonElement root,
        string arrayProperty,
        string actorIdProperty)
    {
        var result = new HashSet<string>(StringComparer.Ordinal);
        if (!TryReadOptionalArray(root, arrayProperty, out var entries))
            return result;

        foreach (var entry in entries.EnumerateArray())
        {
            if (TryReadExactNonEmptyString(entry, actorIdProperty, out var actorId) &&
                HasMeaningfulJournalEntry(entry))
            {
                result.Add(actorId);
            }
        }

        return result;
    }

    private static bool HasMeaningfulJournalEntry(JsonElement entry) =>
        entry.ValueKind == JsonValueKind.Object && entry.EnumerateObject().Any(property =>
            property.Value.ValueKind == JsonValueKind.String &&
            property.Name is not ("guardianId" or "residentId" or "entryId") &&
            !string.IsNullOrWhiteSpace(property.Value.GetString()));

    private static bool HasCommonAfterlifeActorMemory(JsonElement profile) =>
        TryReadExactNonEmptyString(profile, "gmThoughtsSummary", out _) ||
        HasNonEmptyObjectArray(profile, "ledger") ||
        HasNonEmptyObjectArray(profile, "progressionLedger");

    private static bool HasNonEmptyObjectArray(JsonElement root, string propertyName)
    {
        if (root.ValueKind != JsonValueKind.Object ||
            !root.TryGetProperty(propertyName, out var array) ||
            array.ValueKind != JsonValueKind.Array)
        {
            return false;
        }

        return array.EnumerateArray().Any(entry => entry.ValueKind == JsonValueKind.Object);
    }

    private static bool TryReadOptionalArray(JsonElement root, string propertyName, out JsonElement array)
    {
        array = default;
        if (root.ValueKind != JsonValueKind.Object)
            return false;
        if (!root.TryGetProperty(propertyName, out array))
        {
            using var emptyDocument = JsonDocument.Parse("[]");
            array = emptyDocument.RootElement.Clone();
            return true;
        }

        return array.ValueKind == JsonValueKind.Array;
    }

    private static bool TryReadExactNonEmptyString(
        JsonElement root,
        string propertyName,
        out string value)
    {
        value = string.Empty;
        if (root.ValueKind != JsonValueKind.Object)
            return false;

        var matches = 0;
        foreach (var property in root.EnumerateObject())
        {
            if (!string.Equals(property.Name, propertyName, StringComparison.OrdinalIgnoreCase))
                continue;
            if (!string.Equals(property.Name, propertyName, StringComparison.Ordinal))
                return false;
            matches++;
            if (property.Value.ValueKind != JsonValueKind.String)
                return false;
            value = property.Value.GetString() ?? string.Empty;
        }

        return matches == 1 && !string.IsNullOrWhiteSpace(value);
    }

    private static bool TryReadExactOptionalProperty(
        JsonElement root,
        string propertyName,
        out JsonElement value,
        out bool exists)
    {
        value = default;
        exists = false;
        if (root.ValueKind != JsonValueKind.Object)
            return false;

        foreach (var property in root.EnumerateObject())
        {
            if (!string.Equals(property.Name, propertyName, StringComparison.OrdinalIgnoreCase))
                continue;
            if (!string.Equals(property.Name, propertyName, StringComparison.Ordinal) || exists)
                return false;

            exists = true;
            value = property.Value;
        }

        return true;
    }

    private static bool IsEmptyLeadershipSlot(JsonElement value) =>
        value.ValueKind == JsonValueKind.Null ||
        value.ValueKind == JsonValueKind.String && string.IsNullOrWhiteSpace(value.GetString());

    private static bool IsPlayerSoulIdentity(string actorType, string actorId) =>
        string.Equals(actorType, "player_soul", StringComparison.Ordinal) &&
        string.Equals(actorId, "player_soul", StringComparison.Ordinal);

    private static string BuildAfterlifeActorIdentityKey(string actorType, string actorId) =>
        $"{actorType}\u001f{actorId}";

    private static void AddMissingAfterlifeActorProfileIssue(
        AfterlifeActorBinding binding,
        List<ValidationIssue> issues)
    {
        issues.Add(new ValidationIssue(
            binding.Context,
            IssueSeverity.Error,
            "Новая значимая сущность посмертия не связана с точным common profile.",
            code: "afterlife_actor_materialization_profile_missing",
            actor: $"{binding.ActorType}:{binding.ActorId}",
            section: "ActorMaterialization",
            expected: $"exact {binding.ActorType}:{binding.ActorId} profile in {AfterlifeEntityProfileState.StatePath}",
            actual: "no exact actorType + actorId profile",
            repairHint: $"Добавь ровно один профиль actorType='{binding.ActorType}', actorId='{binding.ActorId}' с полным materialization envelope; не связывай сущность по имени или описанию."));
    }

    private static void AddAmbiguousAfterlifeActorProfileIssue(
        AfterlifeActorBinding binding,
        int count,
        List<ValidationIssue> issues)
    {
        issues.Add(new ValidationIssue(
            binding.Context,
            IssueSeverity.Error,
            "Для значимой сущности посмертия найдено несколько точных common profiles.",
            code: "afterlife_actor_materialization_profile_ambiguous",
            actor: $"{binding.ActorType}:{binding.ActorId}",
            section: "ActorMaterialization",
            expected: "exactly one profile",
            actual: $"{count} exact profiles",
            repairHint: "Сохрани один canonical profile для точной пары actorType + actorId; не сливай записи по displayName и не удаляй валидные разделы выбранного профиля."));
    }

    private static void AddMissingAfterlifeActorMemoryIssue(
        AfterlifeActorBinding binding,
        List<ValidationIssue> issues)
    {
        issues.Add(new ValidationIssue(
            binding.Context,
            IssueSeverity.Error,
            "Первичная материализация сущности посмертия не инициализировала actor-owned memory.",
            code: "afterlife_actor_materialization_memory_missing",
            actor: $"{binding.ActorType}:{binding.ActorId}",
            section: "ActorMemory",
            expected: "gmThoughtsSummary, ledger/progressionLedger entry, or exact type-specific thought journal entry",
            actual: "actor-owned memory is empty",
            repairHint: "Инициализируй память этой сущности в её common profile или точном type-specific журнале; не подменяй внутреннюю память внешней хроникой."));
    }

    private static void AddUnusableAfterlifeActorMaterializationPreTurnAuthorityIssue(
        string path,
        List<ValidationIssue> issues)
    {
        issues.Add(new ValidationIssue(
            path,
            IssueSeverity.Error,
            "Validated pre-turn authority сущностей посмертия отсутствует или повреждена; exact binding нельзя вычислять по догадке.",
            code: "actor_materialization_pre_turn_authority_unusable",
            section: "ActorMaterialization",
            expected: "readable validated pre-turn afterlife actor authority",
            actual: "missing, unreadable, hash-invalid, or ambiguous source authority",
            repairHint: "Откати ход к client-owned validated snapshot и восстанови authority штатным rollback/recovery; ГМ не должен реконструировать baseline из текущего состояния."));
    }

    private static void AddUnusableAfterlifeActorBindingCurrentAuthorityIssue(
        string path,
        List<ValidationIssue> issues)
    {
        issues.Add(new ValidationIssue(
            path,
            IssueSeverity.Error,
            "Текущая canonical authority сущностей посмертия повреждена; exact actor binding нельзя вычислять по догадке.",
            code: "afterlife_actor_binding_current_authority_unusable",
            section: "ActorMaterialization",
            expected: "object state with canonical arrays and exact ordinal actor identities",
            actual: "malformed root, collection, actor identity, actor type, or duplicate identity",
            repairHint: "Исправь только указанную canonical source/profile authority по её документированной схеме; не связывай сущности по имени, описанию или жанровым словам и не переписывай валидные записи."));
    }
}
