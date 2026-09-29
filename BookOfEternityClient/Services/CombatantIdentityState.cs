using System.Collections.ObjectModel;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Nodes;

namespace BookOfEternityClient.Services;

internal class CombatantIdentityFactory
{
    /// <summary>
    /// Allocates an ordinary combatant identity with its exact admitted same-turn reference.
    /// </summary>
    /// <param name="combatantRef">
    /// Nonempty same-turn reference supplied by the validated combatant owner.
    /// </param>
    /// <returns>
    /// The identity returned by the existing allocation policy.
    /// </returns>
    internal virtual string CreateCombatantId(string combatantRef)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(combatantRef);
        return CreateCombatantId();
    }

    /// <summary>
    /// Allocates an ordinary member identity with its exact admitted same-turn reference.
    /// </summary>
    /// <param name="memberRef">
    /// Nonempty same-turn reference supplied by the validated combatant owner.
    /// </param>
    /// <returns>
    /// The identity returned by the existing allocation policy.
    /// </returns>
    internal virtual string CreateMemberId(string memberRef)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(memberRef);
        return CreateMemberId();
    }

    internal virtual string CreateCombatantId() =>
        "combatant_" + Guid.NewGuid().ToString("N");

    internal virtual string CreateMemberId() =>
        "member_" + Guid.NewGuid().ToString("N");
}

internal sealed record CombatantIdentityBuildResult(
    CombatantIdentityState? State,
    JsonArray RewrittenCombatants,
    JsonArray RewrittenMembers,
    IReadOnlyList<ValidationIssue> Issues);

internal sealed class CombatantIdentityState
{
    private readonly Dictionary<string, string> _combatantIdsByRef;
    private readonly Dictionary<string, string> _memberIdsByRef;
    private readonly Dictionary<string, string> _npcIdsByCombatantRef;
    private readonly ReadOnlyDictionary<string, string> _readOnlyCombatantIdsByRef;
    private readonly ReadOnlyDictionary<string, string> _readOnlyMemberIdsByRef;
    private readonly ResourceReadOnlySet<string> _combatantIds;
    private readonly ResourceReadOnlySet<string> _memberIds;

    private CombatantIdentityState(
        Dictionary<string, string> combatantIdsByRef,
        Dictionary<string, string> memberIdsByRef,
        Dictionary<string, string> npcIdsByCombatantRef,
        IEnumerable<string> combatantIds,
        IEnumerable<string> memberIds,
        IEnumerable<(string Key, string NpcId)> existingNpcBindings)
    {
        _combatantIdsByRef = new Dictionary<string, string>(
            combatantIdsByRef,
            StringComparer.Ordinal);
        _memberIdsByRef = new Dictionary<string, string>(
            memberIdsByRef,
            StringComparer.Ordinal);
        _npcIdsByCombatantRef = new Dictionary<string, string>(
            npcIdsByCombatantRef,
            StringComparer.Ordinal);
        _readOnlyCombatantIdsByRef = new ReadOnlyDictionary<string, string>(
            _combatantIdsByRef);
        _readOnlyMemberIdsByRef = new ReadOnlyDictionary<string, string>(
            _memberIdsByRef);
        _combatantIds = new ResourceReadOnlySet<string>(combatantIds, StringComparer.Ordinal);
        _memberIds = new ResourceReadOnlySet<string>(memberIds, StringComparer.Ordinal);
        Fingerprint = CreateFingerprint(existingNpcBindings);
    }

    internal IReadOnlyDictionary<string, string> CombatantIdsByRef =>
        _readOnlyCombatantIdsByRef;

    internal IReadOnlyDictionary<string, string> MemberIdsByRef =>
        _readOnlyMemberIdsByRef;

    internal IReadOnlySet<string> CombatantIds => _combatantIds;

    internal IReadOnlySet<string> MemberIds => _memberIds;

    internal string Fingerprint { get; }

    internal bool TryResolveCombatant(string combatantRef, out string combatantId) =>
        _combatantIdsByRef.TryGetValue(combatantRef, out combatantId!);

    internal bool TryResolveMember(string memberRef, out string memberId) =>
        _memberIdsByRef.TryGetValue(memberRef, out memberId!);

    internal bool TryGetBoundNpcId(string combatantRef, out string npcId) =>
        _npcIdsByCombatantRef.TryGetValue(combatantRef, out npcId!);

    internal static CombatantIdentityBuildResult BuildNew(
        JsonArray rawCombatants,
        CombatantIdentityFactory identityFactory) =>
        BuildNew(rawCombatants, new JsonArray(), identityFactory);

    internal static CombatantIdentityBuildResult BuildNew(
        JsonArray rawCombatants,
        JsonArray rawMembers,
        CombatantIdentityFactory identityFactory)
    {
        ArgumentNullException.ThrowIfNull(rawCombatants);
        ArgumentNullException.ThrowIfNull(rawMembers);
        ArgumentNullException.ThrowIfNull(identityFactory);
        var issues = new List<ValidationIssue>();
        var refs = new IdentityRegistry();
        var combatantIds = new IdentityRegistry();
        var memberIds = new IdentityRegistry();
        var combatantCandidates = new List<(string Ref, JsonObject Value, int Index, string? NpcId)>();
        var memberCandidates = new List<(string Ref, JsonObject Value, int Index)>();
        var existingNpcBindings = new List<(string Key, string NpcId)>();

        for (var index = 0; index < rawCombatants.Count; index++)
        {
            var path = $"combatants[{index}]";
            if (rawCombatants[index] is not JsonObject combatant)
            {
                Add(issues, path, "mechanics_combatant_invalid", "combatant identity object", Describe(rawCombatants[index]));
                continue;
            }
            var hasRef = combatant.ContainsKey("combatantRef") && combatant["combatantRef"] != null;
            var hasId = combatant.ContainsKey("combatantId") && combatant["combatantId"] != null;
            var npcValid = TryReadOptionalExact(
                combatant,
                "NPCId",
                path,
                "mechanics_combatant_npc_binding_invalid",
                issues,
                out var npcId);
            if (hasRef && hasId)
            {
                Add(issues, path + ".combatantId", "mechanics_combatant_id_forbidden", "omit combatantId when combatantRef is present", Describe(combatant["combatantId"]));
            }
            if (hasRef)
            {
                if (!TryReadExact(combatant["combatantRef"], out var combatantRef))
                {
                    Add(issues, path + ".combatantRef", "mechanics_combatant_ref_invalid", "exact same-turn combatantRef", Describe(combatant["combatantRef"]));
                }
                else
                {
                    refs.Add(combatantRef, path + ".combatantRef", "mechanics_identity_ref", issues);
                    combatantCandidates.Add((combatantRef, combatant, index, npcValid ? npcId : null));
                }
                continue;
            }
            if (hasId)
            {
                if (!TryReadExact(combatant["combatantId"], out var combatantId))
                {
                    Add(issues, path + ".combatantId", "mechanics_combatant_id_invalid", "exact client-owned combatantId", Describe(combatant["combatantId"]));
                }
                else
                {
                    combatantIds.Add(combatantId, path + ".combatantId", "mechanics_combatant_id", issues);
                    if (npcValid && npcId != null)
                        existingNpcBindings.Add((combatantId, npcId));
                }
                continue;
            }
            if (npcId == null)
            {
                Add(issues, path, "mechanics_combatant_id_missing", "combatantId, combatantRef, or exact named NPC binding", combatant.ToJsonString());
            }
            else
            {
                existingNpcBindings.Add(("npc:" + npcId, npcId));
            }
        }

        for (var index = 0; index < rawMembers.Count; index++)
        {
            var path = $"members[{index}]";
            if (rawMembers[index] is not JsonObject member)
            {
                Add(issues, path, "mechanics_member_invalid", "group member identity object", Describe(rawMembers[index]));
                continue;
            }
            var hasRef = member.ContainsKey("memberRef") && member["memberRef"] != null;
            var hasId = member.ContainsKey("memberId") && member["memberId"] != null;
            if (hasRef && hasId)
            {
                Add(issues, path + ".memberId", "mechanics_member_id_forbidden", "omit memberId when memberRef is present", Describe(member["memberId"]));
            }
            if (hasRef)
            {
                if (!TryReadExact(member["memberRef"], out var memberRef))
                {
                    Add(issues, path + ".memberRef", "mechanics_member_ref_invalid", "exact same-turn memberRef", Describe(member["memberRef"]));
                }
                else
                {
                    refs.Add(memberRef, path + ".memberRef", "mechanics_identity_ref", issues);
                    memberCandidates.Add((memberRef, member, index));
                }
                continue;
            }
            if (hasId)
            {
                if (!TryReadExact(member["memberId"], out var memberId))
                {
                    Add(issues, path + ".memberId", "mechanics_member_id_invalid", "exact client-owned memberId", Describe(member["memberId"]));
                }
                else
                {
                    memberIds.Add(memberId, path + ".memberId", "mechanics_member_id", issues);
                }
                continue;
            }
            Add(issues, path, "mechanics_member_id_missing", "exact memberId or same-turn memberRef", member.ToJsonString());
        }

        if (issues.Count > 0)
            return Failed(rawCombatants, rawMembers, issues);

        var combatantMap = new Dictionary<string, string>(StringComparer.Ordinal);
        var memberMap = new Dictionary<string, string>(StringComparer.Ordinal);
        var npcByRef = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var candidate in combatantCandidates)
        {
            var id = identityFactory.CreateCombatantId(candidate.Ref);
            if (!TryReadExact(JsonValue.Create(id), out id))
            {
                Add(issues, $"combatants[{candidate.Index}].combatantId", "mechanics_combatant_id_invalid", "exact generated combatantId", id);
                continue;
            }
            combatantIds.Add(id, $"combatants[{candidate.Index}].combatantId", "mechanics_combatant_id", issues);
            combatantMap[candidate.Ref] = id;
            if (candidate.NpcId != null)
                npcByRef[candidate.Ref] = candidate.NpcId;
        }
        foreach (var candidate in memberCandidates)
        {
            var id = identityFactory.CreateMemberId(candidate.Ref);
            if (!TryReadExact(JsonValue.Create(id), out id))
            {
                Add(issues, $"members[{candidate.Index}].memberId", "mechanics_member_id_invalid", "exact generated memberId", id);
                continue;
            }
            memberIds.Add(id, $"members[{candidate.Index}].memberId", "mechanics_member_id", issues);
            memberMap[candidate.Ref] = id;
        }
        if (issues.Count > 0)
            return Failed(rawCombatants, rawMembers, issues);

        var rewrittenCombatants = rawCombatants.DeepClone().AsArray();
        foreach (var candidate in combatantCandidates)
        {
            var clone = candidate.Value.DeepClone().AsObject();
            clone.Remove("combatantRef");
            clone["combatantId"] = combatantMap[candidate.Ref];
            rewrittenCombatants[candidate.Index] = clone;
        }
        var rewrittenMembers = rawMembers.DeepClone().AsArray();
        foreach (var candidate in memberCandidates)
        {
            var clone = candidate.Value.DeepClone().AsObject();
            clone.Remove("memberRef");
            clone["memberId"] = memberMap[candidate.Ref];
            rewrittenMembers[candidate.Index] = clone;
        }
        return new CombatantIdentityBuildResult(
            new CombatantIdentityState(
                combatantMap,
                memberMap,
                npcByRef,
                combatantIds.Values,
                memberIds.Values,
                existingNpcBindings),
            rewrittenCombatants,
            rewrittenMembers,
            Array.Empty<ValidationIssue>());
    }

    internal static IReadOnlyList<ValidationIssue> ValidateCanonical(
        JsonArray combatants,
        JsonArray members)
    {
        ArgumentNullException.ThrowIfNull(combatants);
        ArgumentNullException.ThrowIfNull(members);
        var issues = new List<ValidationIssue>();
        var combatantIds = new IdentityRegistry();
        var memberIds = new IdentityRegistry();
        for (var index = 0; index < combatants.Count; index++)
        {
            var path = $"combatants[{index}]";
            if (combatants[index] is not JsonObject combatant)
            {
                Add(issues, path, "mechanics_combatant_invalid", "canonical combatant object", Describe(combatants[index]));
                continue;
            }
            if (combatant.ContainsKey("combatantRef"))
                Add(issues, path + ".combatantRef", "mechanics_combatant_ref_residual", "field absent after identity allocation", Describe(combatant["combatantRef"]));
            if (TryReadExact(combatant["combatantId"], out var combatantId))
            {
                combatantIds.Add(combatantId, path + ".combatantId", "mechanics_combatant_id", issues);
            }
            else if (!TryReadExact(combatant["NPCId"], out _))
            {
                Add(issues, path + ".combatantId", "mechanics_combatant_id_missing", "exact combatantId or exact NPCId binding", Describe(combatant["combatantId"]));
            }
        }
        for (var index = 0; index < members.Count; index++)
        {
            var path = $"members[{index}]";
            if (members[index] is not JsonObject member)
            {
                Add(issues, path, "mechanics_member_invalid", "canonical member object", Describe(members[index]));
                continue;
            }
            if (member.ContainsKey("memberRef"))
                Add(issues, path + ".memberRef", "mechanics_member_ref_residual", "field absent after identity allocation", Describe(member["memberRef"]));
            if (TryReadExact(member["memberId"], out var memberId))
                memberIds.Add(memberId, path + ".memberId", "mechanics_member_id", issues);
            else
                Add(issues, path + ".memberId", "mechanics_member_id_missing", "exact client-owned memberId", Describe(member["memberId"]));
        }
        return issues;
    }

    private string CreateFingerprint(IEnumerable<(string Key, string NpcId)> existingNpcBindings)
    {
        var root = new JsonObject
        {
            ["combatants"] = new JsonArray(_combatantIds
                .OrderBy(static id => id, StringComparer.Ordinal)
                .Select(static id => (JsonNode)id)
                .ToArray()),
            ["members"] = new JsonArray(_memberIds
                .OrderBy(static id => id, StringComparer.Ordinal)
                .Select(static id => (JsonNode)id)
                .ToArray()),
            ["combatantRefs"] = MappingArray(_combatantIdsByRef, "combatantRef", "combatantId"),
            ["memberRefs"] = MappingArray(_memberIdsByRef, "memberRef", "memberId"),
            ["npcBindings"] = new JsonArray(existingNpcBindings
                .Concat(_npcIdsByCombatantRef.Select(pair =>
                    (Key: pair.Key, NpcId: pair.Value)))
                .OrderBy(static pair => pair.Key, StringComparer.Ordinal)
                .ThenBy(static pair => pair.NpcId, StringComparer.Ordinal)
                .Select(pair => (JsonNode)new JsonObject
                {
                    ["key"] = pair.Key,
                    ["npcId"] = pair.NpcId
                }).ToArray())
        };
        return "sha256:" + Convert.ToHexString(
            SHA256.HashData(Encoding.UTF8.GetBytes(root.ToJsonString())))
            .ToLowerInvariant();
    }

    private static JsonArray MappingArray(
        IReadOnlyDictionary<string, string> mapping,
        string refName,
        string idName) =>
        new(mapping
            .OrderBy(static pair => pair.Key, StringComparer.Ordinal)
            .Select(pair => (JsonNode)new JsonObject
            {
                [refName] = pair.Key,
                [idName] = pair.Value
            }).ToArray());

    private static CombatantIdentityBuildResult Failed(
        JsonArray combatants,
        JsonArray members,
        IReadOnlyList<ValidationIssue> issues) =>
        new(
            null,
            combatants.DeepClone().AsArray(),
            members.DeepClone().AsArray(),
            issues.ToArray());

    private static bool TryReadOptionalExact(
        JsonObject owner,
        string propertyName,
        string path,
        string issueCode,
        List<ValidationIssue> issues,
        out string? value)
    {
        value = null;
        if (!owner.TryGetPropertyValue(propertyName, out var node) || node == null)
            return true;
        if (TryReadExact(node, out var exact))
        {
            value = exact;
            return true;
        }
        Add(issues, path + "." + propertyName, issueCode, "null or exact identity", Describe(node));
        return false;
    }

    private static bool TryReadExact(JsonNode? node, out string value)
    {
        value = node is JsonValue jsonValue &&
                jsonValue.TryGetValue<string>(out var text)
            ? text
            : string.Empty;
        return ResourceMaterializationContract.IsExactIdentifier(value);
    }

    private static string Describe(JsonNode? node) => node?.ToJsonString() ?? "missing";

    private static void Add(
        List<ValidationIssue> issues,
        string path,
        string code,
        string expected,
        string actual) =>
        issues.Add(new ValidationIssue(
            path,
            IssueSeverity.Error,
            "Combat or group-member identity violates common mechanics authority.",
            code: code,
            section: "accepted_mechanics",
            expected: expected,
            actual: actual,
            repairHint: "Use one exact unique same-turn ref and omit its permanent ID; the client allocates and consumes every combatant/member ref before canonical publication."));

    private sealed class IdentityRegistry
    {
        private readonly HashSet<string> _exact = new(StringComparer.Ordinal);
        private readonly HashSet<string> _confusable = new(StringComparer.Ordinal);

        internal IReadOnlySet<string> Values => _exact;

        internal void Add(
            string value,
            string path,
            string codePrefix,
            List<ValidationIssue> issues)
        {
            if (!_exact.Add(value))
            {
                CombatantIdentityState.Add(issues, path, codePrefix + "_duplicate", "one exact identity", value);
                return;
            }
            if (!_confusable.Add(ResourceMaterializationContract.BuildConfusableKey(value)))
            {
                CombatantIdentityState.Add(issues, path, codePrefix + "_confusable", "one exact/confusable identity", value);
            }
        }
    }
}
