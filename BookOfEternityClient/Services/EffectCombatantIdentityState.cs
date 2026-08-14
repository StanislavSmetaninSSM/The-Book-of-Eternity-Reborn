using System.Collections.ObjectModel;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Nodes;

namespace BookOfEternityClient.Services;

internal sealed record EffectCombatantIdentityBuildResult(
    EffectCombatantIdentityState? State,
    JsonArray RewrittenCombatants,
    IReadOnlyList<ValidationIssue> Issues);

internal sealed class EffectCombatantIdentityState
{
    private readonly Dictionary<string, string> _combatantIdsByRef;
    private readonly Dictionary<string, string> _npcIdsByRef;
    private readonly ReadOnlyDictionary<string, string> _readOnlyCombatantIdsByRef;

    private EffectCombatantIdentityState(
        Dictionary<string, string> combatantIdsByRef,
        Dictionary<string, string> npcIdsByRef)
    {
        _combatantIdsByRef = combatantIdsByRef;
        _npcIdsByRef = npcIdsByRef;
        _readOnlyCombatantIdsByRef = new ReadOnlyDictionary<string, string>(_combatantIdsByRef);
        var root = new JsonObject();
        foreach (var pair in _combatantIdsByRef.OrderBy(static pair => pair.Key, StringComparer.Ordinal))
        {
            root[pair.Key] = new JsonObject
            {
                ["combatantId"] = pair.Value,
                ["NPCId"] = _npcIdsByRef.GetValueOrDefault(pair.Key)
            };
        }
        Fingerprint = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(root.ToJsonString())));
    }

    internal string Fingerprint { get; }

    internal IReadOnlyDictionary<string, string> CombatantIdsByRef => _readOnlyCombatantIdsByRef;

    internal bool TryResolve(string combatantRef, out string combatantId) =>
        _combatantIdsByRef.TryGetValue(combatantRef, out combatantId!);

    internal bool TryGetBoundNpcId(string combatantRef, out string npcId) =>
        _npcIdsByRef.TryGetValue(combatantRef, out npcId!);

    internal static EffectCombatantIdentityBuildResult BuildNew(
        JsonArray rawCombatants,
        EffectIdentityFactory identityFactory)
    {
        ArgumentNullException.ThrowIfNull(rawCombatants);
        ArgumentNullException.ThrowIfNull(identityFactory);
        var issues = new List<ValidationIssue>();
        var refs = new List<(string Ref, JsonObject Combatant, int Index)>();
        var exact = new HashSet<string>(StringComparer.Ordinal);
        var aliases = new HashSet<string>(StringComparer.Ordinal);
        for (var index = 0; index < rawCombatants.Count; index++)
        {
            var path = $"combatants[{index}]";
            if (rawCombatants[index] is not JsonObject combatant)
            {
                Add(issues, path, "effect_target_combatant_invalid", "new combatant object", rawCombatants[index]?.ToJsonString() ?? "null");
                continue;
            }
            if (combatant.ContainsKey("combatantId"))
                Add(issues, path + ".combatantId", "effect_target_combatant_id_forbidden", "field absent; client allocates combatantId", combatant["combatantId"]?.ToJsonString() ?? "null");
            if (!TryReadExact(combatant["combatantRef"], out var combatantRef))
            {
                Add(issues, path + ".combatantRef", "effect_target_combatant_ref_invalid", "exact non-empty same-turn combatantRef", combatant["combatantRef"]?.ToJsonString() ?? "missing");
                continue;
            }
            if (!exact.Add(combatantRef))
                Add(issues, path + ".combatantRef", "effect_target_combatant_ref_duplicate", "one exact combatantRef", combatantRef);
            if (!aliases.Add(MortalLocationIdentityState.BuildConfusableKey(combatantRef)))
                Add(issues, path + ".combatantRef", "effect_target_combatant_ref_confusable", "one exact/confusable combatantRef", combatantRef);
            refs.Add((combatantRef, combatant, index));
        }

        if (issues.Count > 0)
            return new EffectCombatantIdentityBuildResult(null, rawCombatants.DeepClone().AsArray(), issues);

        var mapping = new Dictionary<string, string>(StringComparer.Ordinal);
        var npcBindings = new Dictionary<string, string>(StringComparer.Ordinal);
        var rewritten = new JsonArray();
        foreach (var candidate in refs)
        {
            var combatantId = identityFactory.CreateCombatantId();
            mapping.Add(candidate.Ref, combatantId);
            if (TryReadExact(candidate.Combatant["NPCId"], out var npcId))
                npcBindings.Add(candidate.Ref, npcId);
            var clone = candidate.Combatant.DeepClone().AsObject();
            clone.Remove("combatantRef");
            clone["combatantId"] = combatantId;
            rewritten.Add(clone);
        }
        return new EffectCombatantIdentityBuildResult(
            new EffectCombatantIdentityState(mapping, npcBindings),
            rewritten,
            Array.Empty<ValidationIssue>());
    }

    private static bool TryReadExact(JsonNode? node, out string value)
    {
        value = node is JsonValue jsonValue && jsonValue.TryGetValue<string>(out var text) ? text : string.Empty;
        return value.Length > 0 && string.Equals(value, value.Trim(), StringComparison.Ordinal);
    }

    private static void Add(List<ValidationIssue> issues, string path, string code, string expected, string actual) =>
        issues.Add(new ValidationIssue(
            path,
            IssueSeverity.Error,
            "Same-turn combatant identity violates client-owned target authority.",
            code: code,
            section: "effect_materialization",
            expected: expected,
            actual: actual,
            repairHint: "Submit one exact unique combatantRef and omit combatantId; the client allocates the stable combat-local identity."));
}
