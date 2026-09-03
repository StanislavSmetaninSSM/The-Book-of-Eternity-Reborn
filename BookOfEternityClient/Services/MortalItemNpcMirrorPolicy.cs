using System.Text.Json.Nodes;

namespace BookOfEternityClient.Services;

internal sealed record MortalItemNpcActorMirror(
    string Section,
    int Index,
    string ActorId,
    JsonObject Actor,
    bool HasPermanentId,
    bool HasInitialId);

/// <summary>
/// Resolves one logical Mortal NPC while preserving the sole supported physical
/// mirror: structurally identical permanent rows split across UpdateNPCs/NPCsInScene.
/// </summary>
internal static class MortalItemNpcMirrorPolicy
{
    private static readonly string[] IdentityProperties =
        { "NPCId", "npcId", "id", "initialId" };

    internal static IReadOnlyDictionary<string, IReadOnlyList<MortalItemNpcActorMirror>>
        BuildExactActorCatalog(JsonObject root, string stage)
    {
        ArgumentNullException.ThrowIfNull(root);
        var mutable = new Dictionary<string, List<MortalItemNpcActorMirror>>(
            StringComparer.Ordinal);
        var confusables = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var section in GuardianPolicyContracts.NpcCoreCanonicalNpcObjectSections)
        {
            if (root[section] is not JsonArray actors)
                continue;
            for (var index = 0; index < actors.Count; index++)
            {
                if (actors[index] is not JsonObject actor)
                    continue;
                var identity = ReadIdentity(actor, stage);
                var confusableKey =
                    ResourceMaterializationContract.BuildConfusableKey(identity);
                if (confusables.TryGetValue(confusableKey, out var existing) &&
                    !string.Equals(existing, identity, StringComparison.Ordinal))
                {
                    throw new InvalidDataException(
                        $"The {stage} NPC graph contains confusable actor IDs " +
                        $"'{existing}' and '{identity}'.");
                }
                confusables[confusableKey] = identity;
                if (!mutable.TryGetValue(identity, out var mirrors))
                {
                    mirrors = new List<MortalItemNpcActorMirror>();
                    mutable.Add(identity, mirrors);
                }
                mirrors.Add(new MortalItemNpcActorMirror(
                    section,
                    index,
                    identity,
                    actor,
                    HasExactIdentity(actor, "NPCId", identity),
                    HasExactIdentity(actor, "initialId", identity)));
            }
        }

        var result = new Dictionary<string, IReadOnlyList<MortalItemNpcActorMirror>>(
            StringComparer.Ordinal);
        foreach (var pair in mutable)
        {
            if (!IsValidLogicalActor(pair.Value))
            {
                throw new InvalidDataException(
                    $"The {stage} NPC identity '{pair.Key}' is duplicated outside the " +
                    "single supported identical permanent cross-section mirror.");
            }
            result.Add(pair.Key, pair.Value.ToArray());
        }
        return result;
    }

    internal static IReadOnlyList<JsonObject> ResolveExactActors(
        JsonObject? root,
        string actorId,
        string stage)
    {
        if (root == null)
            return Array.Empty<JsonObject>();
        var catalog = BuildExactActorCatalog(root, stage);
        return catalog.TryGetValue(actorId, out var mirrors)
            ? mirrors.Select(static mirror => mirror.Actor).ToArray()
            : Array.Empty<JsonObject>();
    }

    internal static bool IsSameTurnCreation(
        IReadOnlyList<MortalItemNpcActorMirror> mirrors) =>
        mirrors.Count == 1 &&
        string.Equals(
            mirrors[0].Section,
            GuardianPolicyContracts.NpcCoreUpdateSectionName,
            StringComparison.Ordinal) &&
        !mirrors[0].HasPermanentId &&
        mirrors[0].HasInitialId;

    internal static bool IsSupportedLogicalActor(
        string actorId,
        IReadOnlyList<(string Section, JsonObject Actor)> mirrors)
    {
        if (mirrors.Count == 0 || mirrors.Any(mirror =>
                !HasConsistentIdentity(mirror.Actor, actorId)))
        {
            return false;
        }
        if (mirrors.Count == 1)
            return true;
        if (mirrors.Count != 2 ||
            mirrors.Any(mirror => !HasExactIdentity(
                mirror.Actor,
                "NPCId",
                actorId)) ||
            string.Equals(
                mirrors[0].Section,
                mirrors[1].Section,
                StringComparison.Ordinal))
        {
            return false;
        }
        var sections = mirrors.Select(static mirror => mirror.Section)
            .ToHashSet(StringComparer.Ordinal);
        return sections.SetEquals(
                   GuardianPolicyContracts.NpcCoreCanonicalNpcObjectSections) &&
               JsonNode.DeepEquals(mirrors[0].Actor, mirrors[1].Actor);
    }

    private static bool IsValidLogicalActor(
        IReadOnlyList<MortalItemNpcActorMirror> mirrors)
    {
        return IsSupportedLogicalActor(
            mirrors[0].ActorId,
            mirrors.Select(static mirror => (mirror.Section, mirror.Actor)).ToArray());
    }

    private static string ReadIdentity(JsonObject actor, string stage)
    {
        var identities = new List<string>();
        foreach (var property in IdentityProperties)
        {
            if (!actor.TryGetPropertyValue(property, out var node) || node == null)
                continue;
            if (node is not JsonValue value ||
                !value.TryGetValue<string>(out var identity) ||
                !ResourceMaterializationContract.IsExactIdentifier(identity))
            {
                throw new InvalidDataException(
                    $"The {stage} NPC property '{property}' is not one exact actor ID.");
            }
            identities.Add(identity);
        }
        if (identities.Count == 0 || identities.Any(identity => !string.Equals(
                identity,
                identities[0],
                StringComparison.Ordinal)))
        {
            throw new InvalidDataException(
                $"The {stage} NPC graph contains a missing or conflicting actor identity.");
        }
        return identities[0];
    }

    private static bool HasExactIdentity(
        JsonObject actor,
        string property,
        string expected) =>
        actor[property] is JsonValue value &&
        value.TryGetValue<string>(out var identity) &&
        string.Equals(identity, expected, StringComparison.Ordinal);

    private static bool HasConsistentIdentity(
        JsonObject actor,
        string expected)
    {
        var found = false;
        foreach (var property in IdentityProperties)
        {
            if (!actor.TryGetPropertyValue(property, out var node) || node == null)
                continue;
            found = true;
            if (node is not JsonValue value ||
                !value.TryGetValue<string>(out var identity) ||
                !ResourceMaterializationContract.IsExactIdentifier(identity) ||
                !string.Equals(identity, expected, StringComparison.Ordinal))
            {
                return false;
            }
        }
        return found;
    }
}
