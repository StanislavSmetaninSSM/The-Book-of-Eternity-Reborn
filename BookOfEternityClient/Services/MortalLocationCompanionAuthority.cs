using System.Text.Json.Nodes;

namespace BookOfEternityClient.Services;

internal enum MortalLocationReferenceResolution
{
    Exact,
    Missing,
    Confusable,
    Ambiguous
}

internal sealed class MortalLocationCompanionAuthority
{
    internal const string CodexPath = "lore/codex_entries.json";
    internal const string RegularQuestsPath = "game_state/quests/regular_quests.json";
    internal const string WorldEventsPath = "game_state/world/world_events.json";

    private readonly ExactIdentityIndex _codexEntries;
    private readonly ExactIdentityIndex _quests;
    private readonly ExactIdentityIndex _worldEvents;

    private MortalLocationCompanionAuthority(
        ExactIdentityIndex codexEntries,
        ExactIdentityIndex quests,
        ExactIdentityIndex worldEvents)
    {
        _codexEntries = codexEntries;
        _quests = quests;
        _worldEvents = worldEvents;
    }

    internal static MortalLocationCompanionAuthority Empty { get; } =
        FromCanonicalRoots(codexRoot: null, questRoot: null, worldEventRoot: null);

    internal static MortalLocationCompanionAuthority FromCanonicalRoots(
        JsonNode? codexRoot,
        JsonNode? questRoot,
        JsonNode? worldEventRoot)
    {
        var codexEntries = new ExactIdentityIndex();
        AddObjectArrayIdentities(codexRoot, "entries", "entryId", codexEntries);

        var quests = new ExactIdentityIndex();
        AddObjectArrayIdentities(questRoot, "quests", "questId", quests);

        var worldEvents = new ExactIdentityIndex();
        AddObjectArrayIdentities(worldEventRoot, "worldEventsLog", "eventId", worldEvents);

        return new MortalLocationCompanionAuthority(
            codexEntries,
            quests,
            worldEvents);
    }

    internal MortalLocationReferenceResolution ResolveLoreBinding(
        string kind,
        string identity) => kind switch
        {
            "codex" => _codexEntries.Resolve(identity),
            "quest" => _quests.Resolve(identity),
            "world_event" => _worldEvents.Resolve(identity),
            _ => MortalLocationReferenceResolution.Missing
        };

    internal IReadOnlyList<ValidationIssue> ValidateLoreBindings(
        JsonObject location,
        string context)
    {
        var issues = new List<ValidationIssue>();
        if (location["loreBindings"] is not JsonArray bindings)
            return issues;

        for (var index = 0; index < bindings.Count; index++)
        {
            if (bindings[index] is not JsonObject binding ||
                !TryReadExactString(binding, "kind", out var kind))
            {
                continue;
            }

            var identityField = kind switch
            {
                "codex" => "codexEntryId",
                "quest" => "questId",
                "world_event" => "worldEventId",
                _ => null
            };
            if (identityField == null ||
                !TryReadExactString(binding, identityField, out var identity))
            {
                continue;
            }

            var resolution = ResolveLoreBinding(kind, identity);
            if (resolution == MortalLocationReferenceResolution.Exact)
                continue;

            var code = resolution switch
            {
                MortalLocationReferenceResolution.Confusable =>
                    "mortal_location_lore_binding_target_confusable",
                MortalLocationReferenceResolution.Ambiguous =>
                    "mortal_location_lore_binding_target_ambiguous",
                _ => "mortal_location_lore_binding_target_unknown"
            };
            issues.Add(new ValidationIssue(
                $"{context}.loreBindings[{index}].{identityField}",
                IssueSeverity.Error,
                "Lore binding must resolve to one exact canonical Mortal authority.",
                code: code,
                section: "mortal_location_materialization",
                expected: $"one exact canonical {identityField}",
                actual: identity,
                repairHint: "Use the exact permanent identity from its canonical Mortal authority."));
        }

        return issues;
    }

    private static void AddObjectArrayIdentities(
        JsonNode? root,
        string collectionName,
        string identityField,
        ExactIdentityIndex target)
    {
        if (root is not JsonObject obj || obj[collectionName] is not JsonArray entries)
            return;

        foreach (var entry in entries.OfType<JsonObject>())
        {
            if (TryReadExactString(entry, identityField, out var identity))
                target.Add(identity);
        }
    }

    private static bool TryReadExactString(
        JsonObject value,
        string propertyName,
        out string result)
    {
        result = string.Empty;
        if (value[propertyName] is not JsonValue node ||
            !node.TryGetValue<string>(out var candidate) ||
            string.IsNullOrEmpty(candidate) ||
            !string.Equals(candidate, candidate.Trim(), StringComparison.Ordinal))
        {
            return false;
        }

        result = candidate;
        return true;
    }

    private sealed class ExactIdentityIndex
    {
        private readonly Dictionary<string, int> _exactCounts = new(StringComparer.Ordinal);
        private readonly Dictionary<string, HashSet<string>> _identitiesByConfusableKey =
            new(StringComparer.Ordinal);

        internal void Add(string identity)
        {
            _exactCounts[identity] = _exactCounts.GetValueOrDefault(identity) + 1;
            var confusableKey = MortalLocationIdentityState.BuildConfusableKey(identity);
            if (!_identitiesByConfusableKey.TryGetValue(confusableKey, out var identities))
            {
                identities = new HashSet<string>(StringComparer.Ordinal);
                _identitiesByConfusableKey.Add(confusableKey, identities);
            }
            identities.Add(identity);
        }

        internal MortalLocationReferenceResolution Resolve(string identity)
        {
            var confusableKey = MortalLocationIdentityState.BuildConfusableKey(identity);
            _identitiesByConfusableKey.TryGetValue(confusableKey, out var confusableIdentities);
            var exactCount = _exactCounts.GetValueOrDefault(identity);
            if (exactCount == 1 && confusableIdentities?.Count == 1)
                return MortalLocationReferenceResolution.Exact;
            if (exactCount > 1 || confusableIdentities is { Count: > 1 })
                return MortalLocationReferenceResolution.Ambiguous;
            return confusableIdentities is { Count: > 0 }
                ? MortalLocationReferenceResolution.Confusable
                : MortalLocationReferenceResolution.Missing;
        }
    }
}
