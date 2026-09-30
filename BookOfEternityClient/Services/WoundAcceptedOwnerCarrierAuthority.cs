using System.Text.Json.Nodes;

namespace BookOfEternityClient.Services;

internal sealed class WoundAcceptedOwnerCarrierCompositionResult
{
    private readonly WoundCarrierCatalogInput? _carriers;
    private readonly ValidationIssue[] _issues;

    internal WoundAcceptedOwnerCarrierCompositionResult(
        WoundCarrierCatalogInput? carriers,
        IReadOnlyList<ValidationIssue> issues)
    {
        _carriers = WoundAcceptedTurnData.CloneWoundCarriers(carriers);
        _issues = (issues ?? throw new ArgumentNullException(nameof(issues))).ToArray();
    }

    internal bool Success => _carriers is not null && _issues.Length == 0;

    internal WoundCarrierCatalogInput? Carriers =>
        WoundAcceptedTurnData.CloneWoundCarriers(_carriers);

    internal IReadOnlyList<ValidationIssue> Issues => _issues;
}

/// <summary>
/// Builds the only wound-carrier baseline accepted by a turn after owner identity
/// allocation. Shared combat/profile roots come from accepted owner authority, while
/// every activeWounds collection remains client-owned and is copied only from the
/// validated pre-turn wound baseline. New accepted owners receive an empty collection.
/// </summary>
internal static class WoundAcceptedOwnerCarrierAuthority
{
    internal static WoundAcceptedOwnerCarrierCompositionResult Compose(
        WoundCarrierCatalogInput preTurnCarriers,
        EffectCarrierCatalogInput acceptedCarriers)
    {
        ArgumentNullException.ThrowIfNull(preTurnCarriers);
        ArgumentNullException.ThrowIfNull(acceptedCarriers);
        var issues = new List<ValidationIssue>();
        var preTurnCatalog = WoundCarrierCatalog.Build(preTurnCarriers);
        issues.AddRange(preTurnCatalog.Issues);
        issues.AddRange(EffectCarrierCatalog.Build(acceptedCarriers).Issues);
        if (issues.Count != 0)
            return Failed(issues);

        var composed = new WoundCarrierCatalogInput(
            Clone(preTurnCarriers.PlayerWounds),
            ComposeNpcRoot(preTurnCarriers.NpcWounds, acceptedCarriers.NpcEffects),
            ComposeCombatRoot(
                preTurnCarriers.EnemyCombatants,
                acceptedCarriers.EnemyCombatants,
                WoundCarrierCatalog.EnemiesPath,
                "enemiesData"),
            ComposeCombatRoot(
                preTurnCarriers.AllyCombatants,
                acceptedCarriers.AllyCombatants,
                WoundCarrierCatalog.AlliesPath,
                "alliesData"),
            ComposeAfterlifeRoot(
                preTurnCarriers.AfterlifeProfiles,
                acceptedCarriers.AfterlifeProfiles));
        var composedCatalog = WoundCarrierCatalog.Build(composed);
        issues.AddRange(composedCatalog.Issues);
        if (issues.Count == 0)
            ValidatePreTurnWoundContinuity(preTurnCatalog, composedCatalog, issues);

        return issues.Count == 0
            ? new WoundAcceptedOwnerCarrierCompositionResult(composed, issues)
            : Failed(issues);
    }

    private static JsonObject? ComposeNpcRoot(
        JsonObject? preTurnRoot,
        JsonObject? acceptedEffectRoot)
    {
        if (preTurnRoot is null && acceptedEffectRoot is null)
            return null;

        var result = Clone(preTurnRoot) ?? new JsonObject
        {
            ["schemaVersion"] = WoundMaterializationContract.SchemaVersion,
            ["entries"] = new JsonArray()
        };
        if (result["entries"] is not JsonArray woundEntries ||
            acceptedEffectRoot?["entries"] is not JsonArray effectEntries)
        {
            return result;
        }

        var existingIds = woundEntries
            .OfType<JsonObject>()
            .Select(static entry => ReadExact(entry["npcId"]))
            .Where(static value => value is not null)
            .ToHashSet(StringComparer.Ordinal);
        foreach (var effectEntry in effectEntries.OfType<JsonObject>())
        {
            var npcId = ReadExact(effectEntry["NPCId"]);
            if (npcId is null || !existingIds.Add(npcId))
                continue;
            woundEntries.Add(new JsonObject
            {
                ["npcId"] = npcId,
                ["activeWounds"] = new JsonArray()
            });
        }
        return result;
    }

    private static JsonObject? ComposeCombatRoot(
        JsonObject? preTurnRoot,
        JsonObject? acceptedRoot,
        string carrierPath,
        string collectionName)
    {
        if (acceptedRoot is null)
            return Clone(preTurnRoot);

        var result = acceptedRoot.DeepClone().AsObject();
        if (result[collectionName] is not JsonArray combatants)
            return result;

        foreach (var combatant in combatants.OfType<JsonObject>())
        {
            PreserveCombatOwnerCollection(
                preTurnRoot,
                combatant,
                carrierPath,
                "combatant",
                "combatantId");
            if (combatant["members"] is not JsonArray members)
                continue;
            foreach (var member in members.OfType<JsonObject>())
            {
                PreserveCombatOwnerCollection(
                    preTurnRoot,
                    member,
                    carrierPath,
                    "combatant_member",
                    "memberId");
            }
        }
        return result;
    }

    private static void PreserveCombatOwnerCollection(
        JsonObject? preTurnRoot,
        JsonObject acceptedOwner,
        string carrierPath,
        string ownerKind,
        string identityField)
    {
        var ownerId = ReadExact(acceptedOwner[identityField]);
        if (ownerId is null || acceptedOwner["NPCId"] is not null)
        {
            acceptedOwner["activeWounds"] = new JsonArray();
            return;
        }

        var owner = new WoundOwnerCoordinate(
            "mortal_world",
            ownerKind,
            ownerId,
            carrierPath);
        acceptedOwner["activeWounds"] = TryResolvePreTurnCollection(
            preTurnRoot,
            owner,
            out var wounds)
            ? wounds.DeepClone()
            : new JsonArray();
    }

    private static JsonObject? ComposeAfterlifeRoot(
        JsonObject? preTurnRoot,
        JsonObject? acceptedRoot)
    {
        if (acceptedRoot is null)
            return Clone(preTurnRoot);

        var result = acceptedRoot.DeepClone().AsObject();
        if (result[AfterlifeEntityProfileState.ProfilesProperty] is not JsonArray profiles)
            return result;

        foreach (var profile in profiles.OfType<JsonObject>())
        {
            if (!TryResolveAfterlifeOwner(profile, out var owner))
            {
                profile.Remove("activeWounds");
                continue;
            }
            profile["activeWounds"] = TryResolvePreTurnCollection(
                preTurnRoot,
                owner,
                out var wounds)
                ? wounds.DeepClone()
                : new JsonArray();
        }
        return result;
    }

    private static bool TryResolveAfterlifeOwner(
        JsonObject profile,
        out WoundOwnerCoordinate owner)
    {
        owner = null!;
        if (!AfterlifeEntityProfileState.TryResolveEffectTarget(profile, out var target))
            return false;
        var ownerKind = target.Kind switch
        {
            "player" => "player_soul",
            "guardian" => "guardian",
            "resident" => "resident",
            "radiant_actor" => "radiant_actor",
            "afterlife_actor" => "afterlife_actor",
            _ => null
        };
        if (ownerKind is null)
            return false;
        owner = new WoundOwnerCoordinate(
            target.Realm,
            ownerKind,
            target.TargetId,
            WoundCarrierCatalog.AfterlifeProfilesPath);
        return true;
    }

    private static bool TryResolvePreTurnCollection(
        JsonObject? preTurnRoot,
        WoundOwnerCoordinate owner,
        out JsonArray wounds)
    {
        if (preTurnRoot is not null &&
            WoundCarrierCollectionAuthority.TryResolve(
                preTurnRoot,
                owner,
                out var resolved,
                out _))
        {
            wounds = resolved;
            return true;
        }

        wounds = null!;
        return false;
    }

    private static void ValidatePreTurnWoundContinuity(
        WoundCarrierCatalog preTurn,
        WoundCarrierCatalog composed,
        ICollection<ValidationIssue> issues)
    {
        foreach (var before in preTurn.Occurrences)
        {
            if (!composed.TryResolveOne(before.WoundId, out var after))
            {
                Add(
                    issues,
                    before.FilePath,
                    "wound_accepted_owner_carrier_missing",
                    "every active pre-turn wound preserved exactly once unless a separate typed owner transition moves it",
                    before.WoundId);
                continue;
            }
            var beforeJson = WoundMaterializationContract.SerializeCanonical(before.Wound);
            var afterJson = WoundMaterializationContract.SerializeCanonical(after.Wound);
            if (!string.Equals(beforeJson, afterJson, StringComparison.Ordinal))
            {
                Add(
                    issues,
                    after.FilePath,
                    "wound_accepted_owner_wound_mutated",
                    "byte-equivalent canonical pre-turn wound while only owner companion fields change",
                    after.WoundId);
            }
        }

        foreach (var after in composed.Occurrences)
        {
            if (preTurn.TryResolveOne(after.WoundId, out _))
                continue;
            Add(
                issues,
                after.FilePath,
                "wound_accepted_owner_injected",
                "no accepted owner-root wound outside the typed wound transition plan",
                after.WoundId);
        }
    }

    private static JsonObject? Clone(JsonObject? value) =>
        value?.DeepClone().AsObject();

    private static string? ReadExact(JsonNode? node) =>
        node is JsonValue value &&
        value.TryGetValue<string>(out var text) &&
        ResourceMaterializationContract.IsExactIdentifier(text)
            ? text
            : null;

    private static WoundAcceptedOwnerCarrierCompositionResult Failed(
        IReadOnlyList<ValidationIssue> issues) => new(null, issues);

    private static void Add(
        ICollection<ValidationIssue> issues,
        string path,
        string code,
        string expected,
        string actual) =>
        issues.Add(new ValidationIssue(
            path,
            IssueSeverity.Error,
            "Accepted owner projection violates client-owned wound-carrier continuity.",
            code: code,
            actor: "Client",
            section: "wound_materialization",
            expected: expected,
            actual: actual,
            repairHint:
                "Preserve exact pre-turn activeWounds collections, initialize new accepted owners empty, and route wound changes only through the typed wound transition plan.",
            category: IssueCategory.ClientOwnedSurface));
}
