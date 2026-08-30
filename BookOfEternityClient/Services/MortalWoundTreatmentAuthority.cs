using System.Collections.ObjectModel;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Text.Json;

namespace BookOfEternityClient.Services;

internal sealed class MortalWoundTreatmentAuthority
{
    private const int SchemaVersion = 1;
    private static readonly ConditionalWeakTable<Context, ContextParserProvenance>
        ContextParserProvenanceRegistry = new();
    private const string MortalRealm = "mortal_world";
    private const string ContextInvalidFieldCode =
        "mortal_wound_treatment_context_invalid_field";
    private const string SnapshotInvalidFieldCode =
        "mortal_wound_treatment_snapshot_invalid_field";

    private static readonly string[] ContextFields =
    {
        "schemaVersion",
        "realm",
        "targetKind",
        "targetId",
        "providerKind",
        "providerId",
        "currentLocationId"
    };

    private static readonly string[] ActorKinds =
    {
        "player",
        "npc",
        "combatant",
        "combatant_member"
    };

    private static readonly string[] SnapshotFields =
    {
        "schemaVersion",
        "snapshotToken",
        "items",
        "resources",
        "actors",
        "facilities",
        "locations",
        "quests",
        "effects",
        "environments"
    };

    private static readonly string[] ItemFields =
    {
        "itemId", "displayName", "realm", "ownerKind", "ownerId", "count",
        "availableCount", "reservationState", "lifecycle", "active"
    };

    private static readonly string[] ResourceFields =
    {
        "resourceRef", "displayName", "realm", "ownerKind", "ownerId",
        "currentValue", "availableValue", "reservationState", "lifecycle", "active"
    };

    private static readonly string[] ActorFields =
    {
        "actorKind", "actorId", "displayName", "realm", "currentLocationId",
        "lifecycle", "active", "reachable", "skills", "capabilities", "consents"
    };

    private static readonly string[] SkillFields =
    {
        "capabilityRef", "displayName", "tier", "lifecycle", "active"
    };

    private static readonly string[] CapabilityFields =
    {
        "capabilityRef", "displayName", "lifecycle", "active"
    };

    private static readonly string[] ConsentFields =
    {
        "consentRef", "displayName", "providerKind", "providerId", "targetKind",
        "targetId", "status", "lifecycle", "active"
    };

    private static readonly string[] FacilityFields =
    {
        "facilityId", "displayName", "realm", "locationId", "lifecycle", "active",
        "available"
    };

    private static readonly string[] LocationFields =
    {
        "locationId", "displayName", "realm", "lifecycle", "active", "presentActors"
    };

    private static readonly string[] ActorCoordinateFields =
    {
        "actorKind", "actorId"
    };

    private static readonly string[] QuestFields =
    {
        "questId", "displayName", "realm", "state", "lifecycle", "active"
    };

    private static readonly string[] EffectFields =
    {
        "effectId", "displayName", "realm", "targetKind", "targetId", "state",
        "lifecycle", "active"
    };

    private static readonly string[] EnvironmentFields =
    {
        "environmentId", "displayName", "realm", "locationId", "state", "lifecycle",
        "active"
    };

    internal sealed record Context(
        int SchemaVersion,
        string Realm,
        string TargetKind,
        string TargetId,
        string ProviderKind,
        string ProviderId,
        string CurrentLocationId)
    {
        internal string SourcePath { get; init; } = string.Empty;

        internal bool HasValidParserProvenance() =>
            HasContextParserProvenance(this);
    }

    internal sealed record Snapshot(
        int SchemaVersion,
        string SnapshotToken,
        IReadOnlyList<Item> Items,
        IReadOnlyList<Resource> Resources,
        IReadOnlyList<Actor> Actors,
        IReadOnlyList<Facility> Facilities,
        IReadOnlyList<Location> Locations,
        IReadOnlyList<Quest> Quests,
        IReadOnlyList<Effect> Effects,
        IReadOnlyList<EnvironmentState> Environments)
    {
        internal string SourcePath { get; init; } = string.Empty;
    }

    internal sealed record Item(
        string ItemId,
        string DisplayName,
        string Realm,
        string OwnerKind,
        string OwnerId,
        int Count,
        int AvailableCount,
        string ReservationState,
        string Lifecycle,
        bool Active);

    internal sealed record Resource(
        string ResourceRef,
        string DisplayName,
        string Realm,
        string OwnerKind,
        string OwnerId,
        int CurrentValue,
        int AvailableValue,
        string ReservationState,
        string Lifecycle,
        bool Active);

    internal sealed record Actor(
        string ActorKind,
        string ActorId,
        string DisplayName,
        string Realm,
        string CurrentLocationId,
        string Lifecycle,
        bool Active,
        bool Reachable,
        IReadOnlyList<Skill> Skills,
        IReadOnlyList<Capability> Capabilities,
        IReadOnlyList<Consent> Consents);

    internal sealed record Skill(
        string CapabilityRef,
        string DisplayName,
        int Tier,
        string Lifecycle,
        bool Active);

    internal sealed record Capability(
        string CapabilityRef,
        string DisplayName,
        string Lifecycle,
        bool Active);

    internal sealed record Consent(
        string ConsentRef,
        string DisplayName,
        string ProviderKind,
        string ProviderId,
        string TargetKind,
        string TargetId,
        string Status,
        string Lifecycle,
        bool Active);

    internal sealed record Facility(
        string FacilityId,
        string DisplayName,
        string Realm,
        string LocationId,
        string Lifecycle,
        bool Active,
        bool Available);

    internal sealed record Location(
        string LocationId,
        string DisplayName,
        string Realm,
        string Lifecycle,
        bool Active,
        IReadOnlyList<ActorCoordinate> PresentActors);

    internal sealed record ActorCoordinate(string ActorKind, string ActorId);

    internal sealed record Quest(
        string QuestId,
        string DisplayName,
        string Realm,
        string State,
        string Lifecycle,
        bool Active);

    internal sealed record Effect(
        string EffectId,
        string DisplayName,
        string Realm,
        string TargetKind,
        string TargetId,
        string State,
        string Lifecycle,
        bool Active);

    internal sealed record EnvironmentState(
        string EnvironmentId,
        string DisplayName,
        string Realm,
        string LocationId,
        string State,
        string Lifecycle,
        bool Active);

    internal sealed record ContextParseResult(
        bool IsValid,
        IReadOnlyList<ValidationIssue> Issues,
        Context? Context);

    internal sealed record SnapshotParseResult(
        bool IsValid,
        IReadOnlyList<ValidationIssue> Issues,
        Snapshot? Snapshot);

    internal static ContextParseResult ParseContext(string json, string path)
    {
        var issues = new List<ValidationIssue>();
        if (!TryParseRoot(json, path, ContextInvalidFieldCode, issues, out var document))
            return new ContextParseResult(false, Freeze(issues), null);

        using (document)
        {
            var root = document.RootElement;
            ValidateClosedObject(
                root,
                path,
                ContextFields,
                "mortal_wound_treatment_context_unknown_field",
                "mortal_wound_treatment_context_duplicate_property",
                "mortal_wound_treatment_context_missing_field",
                issues);
            var version = ReadSchemaVersion(
                root,
                path,
                "mortal_wound_treatment_context_schema_version_invalid",
                issues);
            var realm = ReadClosedIdentifier(
                root,
                "realm",
                path,
                new[] { MortalRealm },
                ContextInvalidFieldCode,
                issues);
            var targetKind = ReadActorKind(
                root,
                "targetKind",
                path,
                ContextInvalidFieldCode,
                issues);
            var targetId = ReadIdentifier(
                root,
                "targetId",
                path,
                ContextInvalidFieldCode,
                issues);
            var providerKind = ReadActorKind(
                root,
                "providerKind",
                path,
                ContextInvalidFieldCode,
                issues);
            var providerId = ReadIdentifier(
                root,
                "providerId",
                path,
                ContextInvalidFieldCode,
                issues);
            var currentLocationId = ReadIdentifier(
                root,
                "currentLocationId",
                path,
                ContextInvalidFieldCode,
                issues);

            if (issues.Count != 0)
                return new ContextParseResult(false, Freeze(issues), null);

            var context = new Context(
                    version,
                    realm,
                    targetKind,
                    targetId,
                    providerKind,
                    providerId,
                    currentLocationId)
                {
                    SourcePath = path
                };
            RegisterContextParserProvenance(context);
            return new ContextParseResult(true, Freeze(issues), context);
        }
    }

    internal static SnapshotParseResult ParseSnapshot(string json, string path)
    {
        var issues = new List<ValidationIssue>();
        if (!TryParseRoot(json, path, SnapshotInvalidFieldCode, issues, out var document))
            return new SnapshotParseResult(false, Freeze(issues), null);

        using (document)
        {
            var root = document.RootElement;
            ValidateClosedObject(
                root,
                path,
                SnapshotFields,
                "mortal_wound_treatment_snapshot_unknown_field",
                "mortal_wound_treatment_snapshot_duplicate_property",
                "mortal_wound_treatment_snapshot_missing_field",
                issues);
            var version = ReadSchemaVersion(
                root,
                path,
                "mortal_wound_treatment_snapshot_schema_version_invalid",
                issues);
            var snapshotToken = ReadIdentifier(
                root,
                "snapshotToken",
                path,
                SnapshotInvalidFieldCode,
                issues);
            var items = ParseItems(root, path, issues);
            var resources = ParseResources(root, path, issues);
            var actors = ParseActors(root, path, issues);
            var facilities = ParseFacilities(root, path, issues);
            var locations = ParseLocations(root, path, issues);
            var quests = ParseQuests(root, path, issues);
            var effects = ParseEffects(root, path, issues);
            var environments = ParseEnvironments(root, path, issues);

            if (issues.Count != 0)
                return new SnapshotParseResult(false, Freeze(issues), null);

            return new SnapshotParseResult(
                true,
                Freeze(issues),
                new Snapshot(
                    version,
                    snapshotToken,
                    Freeze(items),
                    Freeze(resources),
                    Freeze(actors),
                    Freeze(facilities),
                    Freeze(locations),
                    Freeze(quests),
                    Freeze(effects),
                    Freeze(environments))
                {
                    SourcePath = path
                });
        }
    }

    internal static MortalWoundRequirementAuthorityResult ResolveRequirements(
        WoundTreatmentRoute route,
        Context context,
        Snapshot currentSnapshot)
    {
        ArgumentNullException.ThrowIfNull(route);
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(currentSnapshot);

        var issues = new List<ValidationIssue>();
        var resolved = new List<MortalWoundResolvedRequirement>();
        ValidateContextCoordinates(context, currentSnapshot, issues);
        var itemLedger = new Dictionary<LedgerKey, long>();
        var resourceLedger = new Dictionary<LedgerKey, long>();
        var requirementsPath = string.IsNullOrEmpty(route.SourcePath)
            ? "wound.treatment.routes[0].requirements"
            : route.SourcePath + ".requirements";

        for (var index = 0; index < route.Requirements.Count; index++)
        {
            var requirement = route.Requirements[index];
            var requirementPath = $"{requirementsPath}[{index}]";
            var kind = ReadRequirementString(requirement, "kind");
            MortalWoundResolvedRequirement? row = kind switch
            {
                "item_quantity" => ResolveItem(
                    requirement,
                    requirementPath,
                    index,
                    context,
                    currentSnapshot,
                    itemLedger,
                    issues),
                "resource_quantity" => ResolveResource(
                    requirement,
                    requirementPath,
                    index,
                    context,
                    currentSnapshot,
                    resourceLedger,
                    issues),
                "skill_tier" => ResolveSkill(
                    requirement,
                    requirementPath,
                    index,
                    context,
                    currentSnapshot,
                    issues),
                "source_capability" => ResolveCapability(
                    requirement,
                    requirementPath,
                    index,
                    context,
                    currentSnapshot,
                    issues),
                "provider" => ResolveProvider(
                    requirement,
                    requirementPath,
                    index,
                    context,
                    currentSnapshot,
                    issues),
                "consent" => ResolveConsent(
                    requirement,
                    requirementPath,
                    index,
                    context,
                    currentSnapshot,
                    issues),
                "facility" => ResolveFacility(
                    requirement,
                    requirementPath,
                    index,
                    context,
                    currentSnapshot,
                    issues),
                "location" => ResolveLocation(
                    requirement,
                    requirementPath,
                    index,
                    context,
                    currentSnapshot,
                    issues),
                "quest_state" => ResolveQuest(
                    requirement,
                    requirementPath,
                    index,
                    context,
                    currentSnapshot,
                    issues),
                "effect_state" => ResolveEffect(
                    requirement,
                    requirementPath,
                    index,
                    context,
                    currentSnapshot,
                    issues),
                "environment" => ResolveEnvironment(
                    requirement,
                    requirementPath,
                    index,
                    context,
                    currentSnapshot,
                    issues),
                _ => ResolveUnknownRequirement(requirementPath, kind, issues)
            };
            if (row is not null)
                resolved.Add(row);
        }

        var success = issues.Count == 0 && resolved.Count == route.Requirements.Count;
        var frozenIssues = Freeze(issues);
        var frozenResolved = Freeze(resolved);
        return new MortalWoundRequirementAuthorityResult(
            success,
            frozenIssues,
            frozenResolved,
            ComputeResultFingerprint(
                route,
                context,
                currentSnapshot,
                frozenResolved,
                frozenIssues));
    }

    private static MortalWoundResolvedRequirement? ResolveUnknownRequirement(
        string path,
        string? kind,
        ICollection<ValidationIssue> issues)
    {
        AddResolutionIssue(
            issues,
            path + ".kind",
            "mortal_wound_requirement_reference_unresolved",
            "one supported exact Mortal wound-treatment requirement kind",
            kind ?? "missing");
        return null;
    }

    private static MortalWoundResolvedRequirement? ResolveItem(
        JsonElement requirement,
        string path,
        int index,
        Context context,
        Snapshot snapshot,
        IDictionary<LedgerKey, long> ledger,
        ICollection<ValidationIssue> issues)
    {
        const string kind = "item_quantity";
        const string referenceField = "itemRef";
        var authorityRef = ReadRequirementString(requirement, referenceField);
        var ownerRole = ReadRequirementString(requirement, "ownerRole");
        var quantity = ReadRequirementInt32(requirement, "quantity");
        if (authorityRef is null)
            return Unresolved(path, referenceField, null, issues);

        var owner = SelectRole(context, ownerRole);
        if (owner is null)
        {
            AddResolutionIssue(
                issues,
                path,
                "mortal_wound_requirement_wrong_owner",
                "ownerRole provider or target bound to the exact treatment context",
                ownerRole ?? "missing");
            return null;
        }

        var all = snapshot.Items
            .Where(item => string.Equals(item.ItemId, authorityRef, StringComparison.Ordinal))
            .ToArray();
        var owned = all.Where(item => SameOwner(item.OwnerKind, item.OwnerId, owner.Value))
            .ToArray();
        if (owned.Length > 1)
            return Ambiguous(path, referenceField, authorityRef, issues);
        if (owned.Length == 0)
        {
            if (all.Length == 0)
                return Unresolved(path, referenceField, authorityRef, issues);
            AddWrongOwnerIssue(issues, path, referenceField, ownerRole, authorityRef);
            return null;
        }

        var item = owned[0];
        if (!string.Equals(item.Realm, context.Realm, StringComparison.Ordinal))
            return CrossRealm(path, referenceField, authorityRef, item.Realm, issues);
        if (!IsCurrent(item.Lifecycle))
            return Stale(path, referenceField, authorityRef, issues);
        if (!item.Active)
        {
            AddResolutionIssue(
                issues,
                path,
                "mortal_wound_requirement_item_unavailable",
                "active exact item authority",
                "inactive");
            return null;
        }
        if (!string.Equals(item.ReservationState, "available", StringComparison.Ordinal))
        {
            AddResolutionIssue(
                issues,
                path,
                "mortal_wound_requirement_item_reserved",
                "unreserved exact item authority",
                item.ReservationState);
            return null;
        }

        var requested = quantity ?? 0;
        var key = new LedgerKey(kind, authorityRef, owner.Value.Kind, owner.Value.Id);
        var cumulative = ledger.TryGetValue(key, out var prior)
            ? prior + requested
            : requested;
        ledger[key] = cumulative;
        if (requested <= 0 || cumulative > item.Count || cumulative > item.AvailableCount)
        {
            AddResolutionIssue(
                issues,
                path,
                "mortal_wound_requirement_item_quantity_insufficient",
                $"cumulative requested quantity <= current available count {item.AvailableCount}",
                cumulative.ToString(CultureInfo.InvariantCulture));
            return null;
        }

        return CreateResolved(
            index,
            kind,
            authorityRef,
            item.Realm,
            context,
            requirement,
            new string?[]
            {
                item.ItemId,
                item.Realm,
                item.OwnerKind,
                item.OwnerId,
                Number(item.Count),
                Number(item.AvailableCount),
                item.ReservationState,
                item.Lifecycle,
                Boolean(item.Active),
                Number(cumulative)
            },
            ownerKind: item.OwnerKind,
            ownerId: item.OwnerId,
            requestedQuantity: requested);
    }

    private static MortalWoundResolvedRequirement? ResolveResource(
        JsonElement requirement,
        string path,
        int index,
        Context context,
        Snapshot snapshot,
        IDictionary<LedgerKey, long> ledger,
        ICollection<ValidationIssue> issues)
    {
        const string kind = "resource_quantity";
        const string referenceField = "resourceRef";
        var authorityRef = ReadRequirementString(requirement, referenceField);
        var ownerRole = ReadRequirementString(requirement, "ownerRole");
        var quantity = ReadRequirementInt32(requirement, "quantity");
        if (authorityRef is null)
            return Unresolved(path, referenceField, null, issues);

        var owner = SelectRole(context, ownerRole);
        if (owner is null)
        {
            AddResolutionIssue(
                issues,
                path,
                "mortal_wound_requirement_wrong_owner",
                "ownerRole provider or target bound to the exact treatment context",
                ownerRole ?? "missing");
            return null;
        }

        var all = snapshot.Resources.Where(resource => string.Equals(
                resource.ResourceRef,
                authorityRef,
                StringComparison.Ordinal))
            .ToArray();
        var owned = all.Where(resource => SameOwner(
                resource.OwnerKind,
                resource.OwnerId,
                owner.Value))
            .ToArray();
        if (owned.Length > 1)
            return Ambiguous(path, referenceField, authorityRef, issues);
        if (owned.Length == 0)
        {
            if (all.Length == 0)
                return Unresolved(path, referenceField, authorityRef, issues);
            AddWrongOwnerIssue(issues, path, referenceField, ownerRole, authorityRef);
            return null;
        }

        var resource = owned[0];
        if (!string.Equals(resource.Realm, context.Realm, StringComparison.Ordinal))
            return CrossRealm(path, referenceField, authorityRef, resource.Realm, issues);
        if (!IsCurrent(resource.Lifecycle))
            return Stale(path, referenceField, authorityRef, issues);
        if (!resource.Active)
        {
            AddResolutionIssue(
                issues,
                path,
                "mortal_wound_requirement_resource_unavailable",
                "active exact resource authority",
                "inactive");
            return null;
        }
        if (!string.Equals(resource.ReservationState, "available", StringComparison.Ordinal))
        {
            AddResolutionIssue(
                issues,
                path,
                "mortal_wound_requirement_resource_reserved",
                "unreserved exact resource authority",
                resource.ReservationState);
            return null;
        }

        var requested = quantity ?? 0;
        var key = new LedgerKey(kind, authorityRef, owner.Value.Kind, owner.Value.Id);
        var cumulative = ledger.TryGetValue(key, out var prior)
            ? prior + requested
            : requested;
        ledger[key] = cumulative;
        if (requested <= 0 ||
            cumulative > resource.CurrentValue ||
            cumulative > resource.AvailableValue)
        {
            AddResolutionIssue(
                issues,
                path,
                "mortal_wound_requirement_resource_quantity_insufficient",
                $"cumulative requested quantity <= current available value {resource.AvailableValue}",
                cumulative.ToString(CultureInfo.InvariantCulture));
            return null;
        }

        return CreateResolved(
            index,
            kind,
            authorityRef,
            resource.Realm,
            context,
            requirement,
            new string?[]
            {
                resource.ResourceRef,
                resource.Realm,
                resource.OwnerKind,
                resource.OwnerId,
                Number(resource.CurrentValue),
                Number(resource.AvailableValue),
                resource.ReservationState,
                resource.Lifecycle,
                Boolean(resource.Active),
                Number(cumulative)
            },
            ownerKind: resource.OwnerKind,
            ownerId: resource.OwnerId,
            requestedQuantity: requested);
    }

    private static MortalWoundResolvedRequirement? ResolveSkill(
        JsonElement requirement,
        string path,
        int index,
        Context context,
        Snapshot snapshot,
        ICollection<ValidationIssue> issues)
    {
        const string kind = "skill_tier";
        const string referenceField = "capabilityRef";
        var authorityRef = ReadRequirementString(requirement, referenceField);
        var actorRole = ReadRequirementString(requirement, "actorRole");
        var minimumTier = ReadRequirementInt32(requirement, "minimumTier");
        if (authorityRef is null)
            return Unresolved(path, referenceField, null, issues);
        var owner = SelectRole(context, actorRole);
        if (owner is null)
        {
            AddResolutionIssue(
                issues,
                path,
                "mortal_wound_requirement_wrong_owner",
                "actorRole provider or target bound to the exact treatment context",
                actorRole ?? "missing");
            return null;
        }

        var selected = FindSkills(snapshot, owner.Value, authorityRef).ToArray();
        if (selected.Length > 1)
            return Ambiguous(path, referenceField, authorityRef, issues);
        if (selected.Length == 0)
        {
            if (FindAllSkills(snapshot, authorityRef).Any())
            {
                AddWrongOwnerIssue(issues, path, referenceField, actorRole, authorityRef);
                return null;
            }
            return Unresolved(path, referenceField, authorityRef, issues);
        }

        var (actor, skill) = selected[0];
        if (!string.Equals(actor.Realm, context.Realm, StringComparison.Ordinal))
            return CrossRealm(path, referenceField, authorityRef, actor.Realm, issues);
        if (!IsCurrent(actor.Lifecycle) || !IsCurrent(skill.Lifecycle))
            return Stale(path, referenceField, authorityRef, issues);
        if (!actor.Active || !skill.Active)
        {
            AddResolutionIssue(
                issues,
                path,
                "mortal_wound_requirement_skill_inactive",
                "active exact skill authority",
                "inactive");
            return null;
        }
        if (minimumTier is null || skill.Tier < minimumTier.Value)
        {
            AddResolutionIssue(
                issues,
                path,
                "mortal_wound_requirement_skill_tier_insufficient",
                minimumTier?.ToString(CultureInfo.InvariantCulture) ?? "integer minimumTier",
                skill.Tier.ToString(CultureInfo.InvariantCulture));
            return null;
        }

        var acceptedStateProjection = MortalWoundTreatmentAcceptedStateAuthority
            .IsAcceptedStateContextProjection(context);
        return CreateResolved(
            index,
            kind,
            authorityRef,
            actor.Realm,
            context,
            requirement,
            ActorMechanicalFields(actor)
                .Concat(new string?[]
                {
                    skill.CapabilityRef,
                    Number(skill.Tier),
                    skill.Lifecycle,
                    Boolean(skill.Active)
                })
                .ToArray(),
            ownerKind: actor.ActorKind,
            ownerId: actor.ActorId,
            providerKind: acceptedStateProjection ? context.ProviderKind : null,
            providerId: acceptedStateProjection ? context.ProviderId : null,
            targetKind: acceptedStateProjection ? context.TargetKind : null,
            targetId: acceptedStateProjection ? context.TargetId : null,
            locationId: acceptedStateProjection ? context.CurrentLocationId : null,
            minimumTier: minimumTier,
            currentTier: skill.Tier);
    }

    private static MortalWoundResolvedRequirement? ResolveCapability(
        JsonElement requirement,
        string path,
        int index,
        Context context,
        Snapshot snapshot,
        ICollection<ValidationIssue> issues)
    {
        const string kind = "source_capability";
        const string referenceField = "capabilityRef";
        var authorityRef = ReadRequirementString(requirement, referenceField);
        var actorRole = ReadRequirementString(requirement, "actorRole");
        if (authorityRef is null)
            return Unresolved(path, referenceField, null, issues);
        var owner = SelectRole(context, actorRole);
        if (owner is null)
        {
            AddResolutionIssue(
                issues,
                path,
                "mortal_wound_requirement_wrong_owner",
                "actorRole provider or target bound to the exact treatment context",
                actorRole ?? "missing");
            return null;
        }

        var selected = FindCapabilities(snapshot, owner.Value, authorityRef).ToArray();
        if (selected.Length > 1)
            return Ambiguous(path, referenceField, authorityRef, issues);
        if (selected.Length == 0)
        {
            if (FindAllCapabilities(snapshot, authorityRef).Any())
            {
                AddWrongOwnerIssue(issues, path, referenceField, actorRole, authorityRef);
                return null;
            }
            return Unresolved(path, referenceField, authorityRef, issues);
        }

        var (actor, capability) = selected[0];
        if (!string.Equals(actor.Realm, context.Realm, StringComparison.Ordinal))
            return CrossRealm(path, referenceField, authorityRef, actor.Realm, issues);
        if (!IsCurrent(actor.Lifecycle) || !IsCurrent(capability.Lifecycle))
            return Stale(path, referenceField, authorityRef, issues);
        if (!actor.Active || !capability.Active)
        {
            AddResolutionIssue(
                issues,
                path,
                "mortal_wound_requirement_capability_inactive",
                "active exact source capability authority",
                "inactive");
            return null;
        }

        var acceptedStateProjection = MortalWoundTreatmentAcceptedStateAuthority
            .IsAcceptedStateContextProjection(context);
        return CreateResolved(
            index,
            kind,
            authorityRef,
            actor.Realm,
            context,
            requirement,
            ActorMechanicalFields(actor)
                .Concat(new string?[]
                {
                    capability.CapabilityRef,
                    capability.Lifecycle,
                    Boolean(capability.Active)
                })
                .ToArray(),
            ownerKind: actor.ActorKind,
            ownerId: actor.ActorId,
            providerKind: acceptedStateProjection ? context.ProviderKind : null,
            providerId: acceptedStateProjection ? context.ProviderId : null,
            targetKind: acceptedStateProjection ? context.TargetKind : null,
            targetId: acceptedStateProjection ? context.TargetId : null,
            locationId: acceptedStateProjection ? context.CurrentLocationId : null);
    }

    private static MortalWoundResolvedRequirement? ResolveProvider(
        JsonElement requirement,
        string path,
        int index,
        Context context,
        Snapshot snapshot,
        ICollection<ValidationIssue> issues)
    {
        const string kind = "provider";
        const string referenceField = "providerRef";
        var authorityRef = ReadRequirementString(requirement, referenceField);
        if (authorityRef is null)
            return Unresolved(path, referenceField, null, issues);

        var exactId = snapshot.Actors.Where(actor => string.Equals(
                actor.ActorId,
                authorityRef,
                StringComparison.Ordinal))
            .ToArray();
        if (exactId.Length == 0)
            return Unresolved(path, referenceField, authorityRef, issues);
        var selected = exactId.Where(actor =>
                string.Equals(actor.ActorKind, context.ProviderKind, StringComparison.Ordinal) &&
                string.Equals(actor.ActorId, context.ProviderId, StringComparison.Ordinal))
            .ToArray();
        if (selected.Length > 1)
            return Ambiguous(path, referenceField, authorityRef, issues);
        if (selected.Length == 0)
        {
            AddResolutionIssue(
                issues,
                path,
                "mortal_wound_requirement_provider_binding_mismatch",
                $"selected provider ({context.ProviderKind}, {context.ProviderId})",
                authorityRef);
            return null;
        }

        var actor = selected[0];
        if (!string.Equals(actor.Realm, context.Realm, StringComparison.Ordinal))
            return CrossRealm(path, referenceField, authorityRef, actor.Realm, issues);
        if (!IsCurrent(actor.Lifecycle))
            return Stale(path, referenceField, authorityRef, issues);
        if (!actor.Active)
        {
            AddResolutionIssue(
                issues,
                path,
                "mortal_wound_requirement_provider_inactive",
                "active exact provider authority",
                "inactive");
            return null;
        }

        var present = IsPresent(
            snapshot,
            context.CurrentLocationId,
            actor.ActorKind,
            actor.ActorId);
        if (!actor.Reachable ||
            !string.Equals(
                actor.CurrentLocationId,
                context.CurrentLocationId,
                StringComparison.Ordinal) ||
            !present)
        {
            AddResolutionIssue(
                issues,
                path,
                "mortal_wound_requirement_provider_unreachable",
                $"reachable provider present at {context.CurrentLocationId}",
                $"reachable={Boolean(actor.Reachable)}, location={actor.CurrentLocationId}, present={Boolean(present)}");
            return null;
        }

        return CreateResolved(
            index,
            kind,
            authorityRef,
            actor.Realm,
            context,
            requirement,
            ActorMechanicalFields(actor).Concat(new[] { Boolean(present) }).ToArray(),
            providerKind: actor.ActorKind,
            providerId: actor.ActorId,
            locationId: context.CurrentLocationId);
    }

    private static MortalWoundResolvedRequirement? ResolveConsent(
        JsonElement requirement,
        string path,
        int index,
        Context context,
        Snapshot snapshot,
        ICollection<ValidationIssue> issues)
    {
        const string kind = "consent";
        const string referenceField = "consentRef";
        var authorityRef = ReadRequirementString(requirement, referenceField);
        if (authorityRef is null)
            return Unresolved(path, referenceField, null, issues);

        var selected = FindConsents(
                snapshot,
                new ActorKey(context.ProviderKind, context.ProviderId),
                authorityRef)
            .ToArray();
        if (selected.Length > 1)
            return Ambiguous(path, referenceField, authorityRef, issues);
        if (selected.Length == 0)
        {
            if (FindAllConsents(snapshot, authorityRef).Any())
            {
                AddResolutionIssue(
                    issues,
                    path,
                    "mortal_wound_requirement_consent_binding_mismatch",
                    "consent owned by the selected exact provider",
                    authorityRef);
                return null;
            }
            return Unresolved(path, referenceField, authorityRef, issues);
        }

        var (actor, consent) = selected[0];
        if (!string.Equals(actor.Realm, context.Realm, StringComparison.Ordinal))
            return CrossRealm(path, referenceField, authorityRef, actor.Realm, issues);
        if (!IsCurrent(actor.Lifecycle) || !IsCurrent(consent.Lifecycle))
            return Stale(path, referenceField, authorityRef, issues);

        var requiredProviderRef = ReadRequirementString(requirement, "providerRef");
        var requiredTargetRef = ReadRequirementString(requirement, "targetRef");
        var bindingMatches =
            string.Equals(requiredProviderRef, context.ProviderId, StringComparison.Ordinal) &&
            string.Equals(requiredTargetRef, context.TargetId, StringComparison.Ordinal) &&
            string.Equals(consent.ProviderKind, context.ProviderKind, StringComparison.Ordinal) &&
            string.Equals(consent.ProviderId, context.ProviderId, StringComparison.Ordinal) &&
            string.Equals(consent.TargetKind, context.TargetKind, StringComparison.Ordinal) &&
            string.Equals(consent.TargetId, context.TargetId, StringComparison.Ordinal);
        if (!bindingMatches)
        {
            AddResolutionIssue(
                issues,
                path,
                "mortal_wound_requirement_consent_binding_mismatch",
                $"consent from ({context.ProviderKind}, {context.ProviderId}) to ({context.TargetKind}, {context.TargetId})",
                $"from ({consent.ProviderKind}, {consent.ProviderId}) to ({consent.TargetKind}, {consent.TargetId})");
            return null;
        }
        if (!actor.Active ||
            !consent.Active ||
            !string.Equals(consent.Status, "granted", StringComparison.Ordinal))
        {
            AddResolutionIssue(
                issues,
                path,
                "mortal_wound_requirement_consent_missing",
                "active granted current consent",
                consent.Status);
            return null;
        }

        return CreateResolved(
            index,
            kind,
            authorityRef,
            actor.Realm,
            context,
            requirement,
            ActorMechanicalFields(actor)
                .Concat(new string?[]
                {
                    consent.ConsentRef,
                    consent.ProviderKind,
                    consent.ProviderId,
                    consent.TargetKind,
                    consent.TargetId,
                    consent.Status,
                    consent.Lifecycle,
                    Boolean(consent.Active)
                })
                .ToArray(),
            providerKind: consent.ProviderKind,
            providerId: consent.ProviderId,
            targetKind: consent.TargetKind,
            targetId: consent.TargetId,
            currentState: consent.Status);
    }

    private static MortalWoundResolvedRequirement? ResolveFacility(
        JsonElement requirement,
        string path,
        int index,
        Context context,
        Snapshot snapshot,
        ICollection<ValidationIssue> issues)
    {
        const string kind = "facility";
        const string referenceField = "facilityRef";
        var authorityRef = ReadRequirementString(requirement, referenceField);
        if (authorityRef is null)
            return Unresolved(path, referenceField, null, issues);
        var selected = snapshot.Facilities.Where(facility => string.Equals(
                facility.FacilityId,
                authorityRef,
                StringComparison.Ordinal))
            .ToArray();
        if (selected.Length > 1)
            return Ambiguous(path, referenceField, authorityRef, issues);
        if (selected.Length == 0)
            return Unresolved(path, referenceField, authorityRef, issues);
        var facility = selected[0];
        if (!string.Equals(facility.Realm, context.Realm, StringComparison.Ordinal))
            return CrossRealm(path, referenceField, authorityRef, facility.Realm, issues);
        if (!IsCurrent(facility.Lifecycle))
            return Stale(path, referenceField, authorityRef, issues);
        if (!string.Equals(
                facility.LocationId,
                context.CurrentLocationId,
                StringComparison.Ordinal))
        {
            AddResolutionIssue(
                issues,
                path,
                "mortal_wound_requirement_facility_location_mismatch",
                context.CurrentLocationId,
                facility.LocationId);
            return null;
        }
        if (!facility.Active || !facility.Available)
        {
            AddResolutionIssue(
                issues,
                path,
                "mortal_wound_requirement_facility_unavailable",
                "active available facility",
                $"active={Boolean(facility.Active)}, available={Boolean(facility.Available)}");
            return null;
        }

        return CreateResolved(
            index,
            kind,
            authorityRef,
            facility.Realm,
            context,
            requirement,
            new string?[]
            {
                facility.FacilityId,
                facility.Realm,
                facility.LocationId,
                facility.Lifecycle,
                Boolean(facility.Active),
                Boolean(facility.Available)
            },
            locationId: facility.LocationId);
    }

    private static MortalWoundResolvedRequirement? ResolveLocation(
        JsonElement requirement,
        string path,
        int index,
        Context context,
        Snapshot snapshot,
        ICollection<ValidationIssue> issues)
    {
        const string kind = "location";
        const string referenceField = "locationRef";
        var authorityRef = ReadRequirementString(requirement, referenceField);
        if (authorityRef is null)
            return Unresolved(path, referenceField, null, issues);
        var selected = snapshot.Locations.Where(location => string.Equals(
                location.LocationId,
                authorityRef,
                StringComparison.Ordinal))
            .ToArray();
        if (selected.Length > 1)
            return Ambiguous(path, referenceField, authorityRef, issues);
        if (selected.Length == 0)
            return Unresolved(path, referenceField, authorityRef, issues);
        var location = selected[0];
        if (!string.Equals(location.Realm, context.Realm, StringComparison.Ordinal))
            return CrossRealm(path, referenceField, authorityRef, location.Realm, issues);
        if (!IsCurrent(location.Lifecycle) || !location.Active)
            return Stale(path, referenceField, authorityRef, issues);

        var present = location.PresentActors.Any(actor =>
            string.Equals(actor.ActorKind, context.TargetKind, StringComparison.Ordinal) &&
            string.Equals(actor.ActorId, context.TargetId, StringComparison.Ordinal));
        if (!string.Equals(
                authorityRef,
                context.CurrentLocationId,
                StringComparison.Ordinal) ||
            !present)
        {
            AddResolutionIssue(
                issues,
                path,
                "mortal_wound_requirement_target_not_present",
                $"target ({context.TargetKind}, {context.TargetId}) at {context.CurrentLocationId}",
                $"location={location.LocationId}, present={Boolean(present)}");
            return null;
        }

        return CreateResolved(
            index,
            kind,
            authorityRef,
            location.Realm,
            context,
            requirement,
            LocationMechanicalFields(location),
            targetKind: context.TargetKind,
            targetId: context.TargetId,
            locationId: location.LocationId);
    }

    private static MortalWoundResolvedRequirement? ResolveQuest(
        JsonElement requirement,
        string path,
        int index,
        Context context,
        Snapshot snapshot,
        ICollection<ValidationIssue> issues)
    {
        const string kind = "quest_state";
        const string referenceField = "questRef";
        var authorityRef = ReadRequirementString(requirement, referenceField);
        if (authorityRef is null)
            return Unresolved(path, referenceField, null, issues);
        var selected = snapshot.Quests.Where(quest => string.Equals(
                quest.QuestId,
                authorityRef,
                StringComparison.Ordinal))
            .ToArray();
        if (selected.Length > 1)
            return Ambiguous(path, referenceField, authorityRef, issues);
        if (selected.Length == 0)
            return Unresolved(path, referenceField, authorityRef, issues);
        var quest = selected[0];
        if (!string.Equals(quest.Realm, context.Realm, StringComparison.Ordinal))
            return CrossRealm(path, referenceField, authorityRef, quest.Realm, issues);
        if (!IsCurrent(quest.Lifecycle) || !quest.Active)
            return Stale(path, referenceField, authorityRef, issues);
        if (!ValidateRequiredState(requirement, path, quest.State, issues))
            return null;

        return CreateResolved(
            index,
            kind,
            authorityRef,
            quest.Realm,
            context,
            requirement,
            new string?[]
            {
                quest.QuestId,
                quest.Realm,
                quest.State,
                quest.Lifecycle,
                Boolean(quest.Active)
            },
            currentState: quest.State);
    }

    private static MortalWoundResolvedRequirement? ResolveEffect(
        JsonElement requirement,
        string path,
        int index,
        Context context,
        Snapshot snapshot,
        ICollection<ValidationIssue> issues)
    {
        const string kind = "effect_state";
        const string referenceField = "effectRef";
        var authorityRef = ReadRequirementString(requirement, referenceField);
        if (authorityRef is null)
            return Unresolved(path, referenceField, null, issues);
        var selected = snapshot.Effects.Where(effect => string.Equals(
                effect.EffectId,
                authorityRef,
                StringComparison.Ordinal))
            .ToArray();
        if (selected.Length > 1)
            return Ambiguous(path, referenceField, authorityRef, issues);
        if (selected.Length == 0)
            return Unresolved(path, referenceField, authorityRef, issues);
        var effect = selected[0];
        if (!string.Equals(effect.Realm, context.Realm, StringComparison.Ordinal))
            return CrossRealm(path, referenceField, authorityRef, effect.Realm, issues);
        if (!IsCurrent(effect.Lifecycle) || !effect.Active)
            return Stale(path, referenceField, authorityRef, issues);
        if (!string.Equals(effect.TargetKind, context.TargetKind, StringComparison.Ordinal) ||
            !string.Equals(effect.TargetId, context.TargetId, StringComparison.Ordinal))
        {
            AddResolutionIssue(
                issues,
                path,
                "mortal_wound_requirement_effect_target_mismatch",
                $"effect target ({context.TargetKind}, {context.TargetId})",
                $"({effect.TargetKind}, {effect.TargetId})");
            return null;
        }
        if (!ValidateRequiredState(requirement, path, effect.State, issues))
            return null;

        return CreateResolved(
            index,
            kind,
            authorityRef,
            effect.Realm,
            context,
            requirement,
            new string?[]
            {
                effect.EffectId,
                effect.Realm,
                effect.TargetKind,
                effect.TargetId,
                effect.State,
                effect.Lifecycle,
                Boolean(effect.Active)
            },
            targetKind: effect.TargetKind,
            targetId: effect.TargetId,
            currentState: effect.State);
    }

    private static MortalWoundResolvedRequirement? ResolveEnvironment(
        JsonElement requirement,
        string path,
        int index,
        Context context,
        Snapshot snapshot,
        ICollection<ValidationIssue> issues)
    {
        const string kind = "environment";
        const string referenceField = "environmentRef";
        var authorityRef = ReadRequirementString(requirement, referenceField);
        if (authorityRef is null)
            return Unresolved(path, referenceField, null, issues);
        var selected = snapshot.Environments.Where(environment => string.Equals(
                environment.EnvironmentId,
                authorityRef,
                StringComparison.Ordinal))
            .ToArray();
        if (selected.Length > 1)
            return Ambiguous(path, referenceField, authorityRef, issues);
        if (selected.Length == 0)
            return Unresolved(path, referenceField, authorityRef, issues);
        var environment = selected[0];
        if (!string.Equals(environment.Realm, context.Realm, StringComparison.Ordinal))
            return CrossRealm(path, referenceField, authorityRef, environment.Realm, issues);
        if (!IsCurrent(environment.Lifecycle) || !environment.Active)
            return Stale(path, referenceField, authorityRef, issues);
        if (!string.Equals(
                environment.LocationId,
                context.CurrentLocationId,
                StringComparison.Ordinal))
        {
            AddResolutionIssue(
                issues,
                path,
                "mortal_wound_requirement_environment_location_mismatch",
                context.CurrentLocationId,
                environment.LocationId);
            return null;
        }
        if (!ValidateRequiredState(requirement, path, environment.State, issues))
            return null;

        return CreateResolved(
            index,
            kind,
            authorityRef,
            environment.Realm,
            context,
            requirement,
            new string?[]
            {
                environment.EnvironmentId,
                environment.Realm,
                environment.LocationId,
                environment.State,
                environment.Lifecycle,
                Boolean(environment.Active)
            },
            locationId: environment.LocationId,
            currentState: environment.State);
    }

    private static bool ValidateRequiredState(
        JsonElement requirement,
        string path,
        string currentState,
        ICollection<ValidationIssue> issues)
    {
        var requiredState = ReadRequirementString(requirement, "requiredState");
        if (requiredState is not null &&
            string.Equals(requiredState, currentState, StringComparison.Ordinal))
        {
            return true;
        }

        AddResolutionIssue(
            issues,
            path,
            "mortal_wound_requirement_state_mismatch",
            requiredState ?? "exact requiredState",
            currentState);
        AddResolutionIssue(
            issues,
            path + ".requiredState",
            "mortal_wound_requirement_state_mismatch",
            requiredState ?? "exact requiredState",
            currentState);
        return false;
    }

    private static void ValidateContextCoordinates(
        Context context,
        Snapshot snapshot,
        ICollection<ValidationIssue> issues)
    {
        ValidateContextActor(
            context,
            snapshot,
            context.TargetKind,
            context.TargetId,
            "targetKind",
            "targetId",
            "mortal_wound_treatment_context_target_mismatch",
            issues);
        ValidateContextActor(
            context,
            snapshot,
            context.ProviderKind,
            context.ProviderId,
            "providerKind",
            "providerId",
            "mortal_wound_treatment_context_provider_mismatch",
            issues);
        var exactLocations = snapshot.Locations.Where(location => string.Equals(
                location.LocationId,
                context.CurrentLocationId,
                StringComparison.Ordinal))
            .ToArray();
        var currentLocations = exactLocations.Where(location =>
                string.Equals(location.Realm, context.Realm, StringComparison.Ordinal) &&
                IsCurrent(location.Lifecycle) &&
                location.Active)
            .ToArray();
        if (exactLocations.Length != 1 || currentLocations.Length != 1)
        {
            AddResolutionIssue(
                issues,
                Path(context.SourcePath, "currentLocationId"),
                "mortal_wound_treatment_context_location_mismatch",
                "exactly one current active same-realm location in the fresh authority snapshot",
                $"{context.CurrentLocationId}: exact={exactLocations.Length}, current={currentLocations.Length}");
        }
    }

    private static void ValidateContextActor(
        Context context,
        Snapshot snapshot,
        string actorKind,
        string actorId,
        string kindField,
        string idField,
        string code,
        ICollection<ValidationIssue> issues)
    {
        var exactActors = snapshot.Actors.Where(actor =>
                string.Equals(actor.ActorKind, actorKind, StringComparison.Ordinal) &&
                string.Equals(actor.ActorId, actorId, StringComparison.Ordinal))
            .ToArray();
        var currentActors = exactActors.Where(actor =>
                string.Equals(actor.Realm, context.Realm, StringComparison.Ordinal) &&
                IsCurrent(actor.Lifecycle) &&
                actor.Active)
            .ToArray();
        if (exactActors.Length == 1 && currentActors.Length == 1)
        {
            return;
        }

        var mismatchedKind = exactActors.Length == 0 &&
                             snapshot.Actors.Any(actor => string.Equals(
                                 actor.ActorId,
                                 actorId,
                                 StringComparison.Ordinal));
        AddResolutionIssue(
            issues,
            Path(context.SourcePath, mismatchedKind ? kindField : idField),
            code,
            "exactly one current active same-realm actor (kind, id) in the fresh authority snapshot",
            $"({actorKind}, {actorId}): exact={exactActors.Length}, current={currentActors.Length}");
    }

    private static MortalWoundResolvedRequirement CreateResolved(
        int requirementIndex,
        string kind,
        string authorityRef,
        string realm,
        Context context,
        JsonElement requirement,
        IReadOnlyList<string?> mechanicalFields,
        string? ownerKind = null,
        string? ownerId = null,
        string? providerKind = null,
        string? providerId = null,
        string? targetKind = null,
        string? targetId = null,
        string? locationId = null,
        int? requestedQuantity = null,
        int? minimumTier = null,
        int? currentTier = null,
        string? currentState = null)
    {
        var fields = new List<string?>
        {
            "mortal_wound_requirement_authority_row",
            "1",
            Number(requirementIndex),
            kind,
            authorityRef,
            realm,
            ownerKind,
            ownerId,
            providerKind,
            providerId,
            targetKind,
            targetId,
            locationId,
            Number(requestedQuantity),
            Number(minimumTier),
            Number(currentTier),
            currentState
        };
        AppendContextFields(fields, context);
        AppendRequirementFields(fields, requirementIndex, requirement);
        AppendSequence(fields, mechanicalFields);
        var fingerprint = WoundAcceptedTurnFingerprintWriter.Compute(fields);
        return new MortalWoundResolvedRequirement(
            requirementIndex,
            kind,
            authorityRef,
            realm,
            ownerKind,
            ownerId,
            providerKind,
            providerId,
            targetKind,
            targetId,
            locationId,
            requestedQuantity,
            minimumTier,
            currentTier,
            currentState,
            fingerprint);
    }

    internal static string RecomputeResolvedRequirementFingerprint(
        MortalWoundResolvedRequirement row,
        Context context,
        JsonElement requirement,
        IReadOnlyList<string?> mechanicalFields)
    {
        ArgumentNullException.ThrowIfNull(row);
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(mechanicalFields);
        return CreateResolved(
            row.RequirementIndex,
            row.Kind,
            row.AuthorityRef,
            row.Realm,
            context,
            requirement,
            mechanicalFields,
            row.OwnerKind,
            row.OwnerId,
            row.ProviderKind,
            row.ProviderId,
            row.TargetKind,
            row.TargetId,
            row.LocationId,
            row.RequestedQuantity,
            row.MinimumTier,
            row.CurrentTier,
            row.CurrentState).AuthorityFingerprint;
    }

    private static string ComputeResultFingerprint(
        WoundTreatmentRoute route,
        Context context,
        Snapshot snapshot,
        IReadOnlyList<MortalWoundResolvedRequirement> resolved,
        IReadOnlyList<ValidationIssue> issues)
    {
        var fields = new List<string?>
        {
            "mortal_wound_requirement_authority_result",
            "1"
        };
        AppendContextFields(fields, context);
        fields.Add(snapshot.SnapshotToken);
        fields.Add(Number(route.Requirements.Count));
        for (var index = 0; index < route.Requirements.Count; index++)
            AppendRequirementFields(fields, index, route.Requirements[index]);
        AppendSnapshotMechanicalFields(fields, snapshot);
        fields.Add(Number(resolved.Count));
        foreach (var row in resolved)
        {
            fields.Add(Number(row.RequirementIndex));
            fields.Add(row.AuthorityFingerprint);
        }
        fields.Add(Number(issues.Count));
        foreach (var issue in issues)
        {
            fields.Add(issue.Code);
            fields.Add(issue.FilePath);
            fields.Add(issue.Expected);
            fields.Add(issue.Actual);
        }
        return WoundAcceptedTurnFingerprintWriter.Compute(fields);
    }

    private static void AppendContextFields(ICollection<string?> fields, Context context)
    {
        fields.Add(Number(context.SchemaVersion));
        fields.Add(context.Realm);
        fields.Add(context.TargetKind);
        fields.Add(context.TargetId);
        fields.Add(context.ProviderKind);
        fields.Add(context.ProviderId);
        fields.Add(context.CurrentLocationId);
    }

    private static string ComputeContextParserSeal(Context context) =>
        WoundAcceptedTurnFingerprintWriter.Compute(new string?[]
        {
            "mortal_wound_treatment_parsed_context",
            "1",
            Number(context.SchemaVersion),
            context.Realm,
            context.TargetKind,
            context.TargetId,
            context.ProviderKind,
            context.ProviderId,
            context.CurrentLocationId,
            context.SourcePath
        });

    private static void RegisterContextParserProvenance(Context context) =>
        ContextParserProvenanceRegistry.Add(
            context,
            new ContextParserProvenance(ComputeContextParserSeal(context)));

    private static bool HasContextParserProvenance(Context context) =>
        ContextParserProvenanceRegistry.TryGetValue(context, out var provenance) &&
        string.Equals(
            provenance.Seal,
            ComputeContextParserSeal(context),
            StringComparison.Ordinal);

    private sealed record ContextParserProvenance(string Seal);

    private static void AppendRequirementFields(
        ICollection<string?> fields,
        int index,
        JsonElement requirement)
    {
        var kind = ReadRequirementString(requirement, "kind");
        fields.Add(Number(index));
        fields.Add(kind);
        switch (kind)
        {
            case "item_quantity":
                fields.Add(ReadRequirementString(requirement, "itemRef"));
                fields.Add(Number(ReadRequirementInt32(requirement, "quantity")));
                fields.Add(ReadRequirementString(requirement, "ownerRole"));
                break;
            case "resource_quantity":
                fields.Add(ReadRequirementString(requirement, "resourceRef"));
                fields.Add(Number(ReadRequirementInt32(requirement, "quantity")));
                fields.Add(ReadRequirementString(requirement, "ownerRole"));
                break;
            case "skill_tier":
                fields.Add(ReadRequirementString(requirement, "capabilityRef"));
                fields.Add(Number(ReadRequirementInt32(requirement, "minimumTier")));
                fields.Add(ReadRequirementString(requirement, "actorRole"));
                break;
            case "source_capability":
                fields.Add(ReadRequirementString(requirement, "capabilityRef"));
                fields.Add(ReadRequirementString(requirement, "actorRole"));
                break;
            case "provider":
                fields.Add(ReadRequirementString(requirement, "providerRef"));
                break;
            case "consent":
                fields.Add(ReadRequirementString(requirement, "consentRef"));
                fields.Add(ReadRequirementString(requirement, "providerRef"));
                fields.Add(ReadRequirementString(requirement, "targetRef"));
                break;
            case "facility":
                fields.Add(ReadRequirementString(requirement, "facilityRef"));
                break;
            case "location":
                fields.Add(ReadRequirementString(requirement, "locationRef"));
                fields.Add(ReadRequirementString(requirement, "targetRole"));
                break;
            case "quest_state":
                fields.Add(ReadRequirementString(requirement, "questRef"));
                fields.Add(ReadRequirementString(requirement, "requiredState"));
                break;
            case "effect_state":
                fields.Add(ReadRequirementString(requirement, "effectRef"));
                fields.Add(ReadRequirementString(requirement, "requiredState"));
                fields.Add(ReadRequirementString(requirement, "targetRole"));
                break;
            case "environment":
                fields.Add(ReadRequirementString(requirement, "environmentRef"));
                fields.Add(ReadRequirementString(requirement, "requiredState"));
                break;
            default:
                fields.Add(requirement.ValueKind == JsonValueKind.Undefined
                    ? null
                    : requirement.GetRawText());
                break;
        }
    }

    private static void AppendSnapshotMechanicalFields(
        ICollection<string?> fields,
        Snapshot snapshot)
    {
        fields.Add(Number(snapshot.SchemaVersion));
        AppendSequence(fields, snapshot.Items.SelectMany(item => new string?[]
        {
            item.ItemId,
            item.Realm,
            item.OwnerKind,
            item.OwnerId,
            Number(item.Count),
            Number(item.AvailableCount),
            item.ReservationState,
            item.Lifecycle,
            Boolean(item.Active)
        }).ToArray());
        AppendSequence(fields, snapshot.Resources.SelectMany(resource => new string?[]
        {
            resource.ResourceRef,
            resource.Realm,
            resource.OwnerKind,
            resource.OwnerId,
            Number(resource.CurrentValue),
            Number(resource.AvailableValue),
            resource.ReservationState,
            resource.Lifecycle,
            Boolean(resource.Active)
        }).ToArray());
        fields.Add(Number(snapshot.Actors.Count));
        foreach (var actor in snapshot.Actors)
        {
            AppendSequence(fields, ActorMechanicalFields(actor));
            AppendSequence(fields, actor.Skills.SelectMany(skill => new string?[]
            {
                skill.CapabilityRef,
                Number(skill.Tier),
                skill.Lifecycle,
                Boolean(skill.Active)
            }).ToArray());
            AppendSequence(fields, actor.Capabilities.SelectMany(capability => new string?[]
            {
                capability.CapabilityRef,
                capability.Lifecycle,
                Boolean(capability.Active)
            }).ToArray());
            AppendSequence(fields, actor.Consents.SelectMany(consent => new string?[]
            {
                consent.ConsentRef,
                consent.ProviderKind,
                consent.ProviderId,
                consent.TargetKind,
                consent.TargetId,
                consent.Status,
                consent.Lifecycle,
                Boolean(consent.Active)
            }).ToArray());
        }
        AppendSequence(fields, snapshot.Facilities.SelectMany(facility => new string?[]
        {
            facility.FacilityId,
            facility.Realm,
            facility.LocationId,
            facility.Lifecycle,
            Boolean(facility.Active),
            Boolean(facility.Available)
        }).ToArray());
        fields.Add(Number(snapshot.Locations.Count));
        foreach (var location in snapshot.Locations)
            AppendSequence(fields, LocationMechanicalFields(location));
        AppendSequence(fields, snapshot.Quests.SelectMany(quest => new string?[]
        {
            quest.QuestId,
            quest.Realm,
            quest.State,
            quest.Lifecycle,
            Boolean(quest.Active)
        }).ToArray());
        AppendSequence(fields, snapshot.Effects.SelectMany(effect => new string?[]
        {
            effect.EffectId,
            effect.Realm,
            effect.TargetKind,
            effect.TargetId,
            effect.State,
            effect.Lifecycle,
            Boolean(effect.Active)
        }).ToArray());
        AppendSequence(fields, snapshot.Environments.SelectMany(environment => new string?[]
        {
            environment.EnvironmentId,
            environment.Realm,
            environment.LocationId,
            environment.State,
            environment.Lifecycle,
            Boolean(environment.Active)
        }).ToArray());
    }

    private static string?[] ActorMechanicalFields(Actor actor) =>
        new string?[]
        {
            actor.ActorKind,
            actor.ActorId,
            actor.Realm,
            actor.CurrentLocationId,
            actor.Lifecycle,
            Boolean(actor.Active),
            Boolean(actor.Reachable)
        };

    private static string?[] LocationMechanicalFields(Location location)
    {
        var fields = new List<string?>
        {
            location.LocationId,
            location.Realm,
            location.Lifecycle,
            Boolean(location.Active),
            Number(location.PresentActors.Count)
        };
        foreach (var actor in location.PresentActors)
        {
            fields.Add(actor.ActorKind);
            fields.Add(actor.ActorId);
        }
        return fields.ToArray();
    }

    private static void AppendSequence(
        ICollection<string?> fields,
        IReadOnlyCollection<string?> values)
    {
        fields.Add(Number(values.Count));
        foreach (var value in values)
            fields.Add(value);
    }

    private static IEnumerable<(Actor Actor, Skill Skill)> FindSkills(
        Snapshot snapshot,
        ActorKey owner,
        string authorityRef) =>
        snapshot.Actors
            .Where(actor => SameActor(actor, owner))
            .SelectMany(actor => actor.Skills
                .Where(skill => string.Equals(
                    skill.CapabilityRef,
                    authorityRef,
                    StringComparison.Ordinal))
                .Select(skill => (actor, skill)));

    private static IEnumerable<(Actor Actor, Skill Skill)> FindAllSkills(
        Snapshot snapshot,
        string authorityRef) =>
        snapshot.Actors.SelectMany(actor => actor.Skills
            .Where(skill => string.Equals(
                skill.CapabilityRef,
                authorityRef,
                StringComparison.Ordinal))
            .Select(skill => (actor, skill)));

    private static IEnumerable<(Actor Actor, Capability Capability)> FindCapabilities(
        Snapshot snapshot,
        ActorKey owner,
        string authorityRef) =>
        snapshot.Actors
            .Where(actor => SameActor(actor, owner))
            .SelectMany(actor => actor.Capabilities
                .Where(capability => string.Equals(
                    capability.CapabilityRef,
                    authorityRef,
                    StringComparison.Ordinal))
                .Select(capability => (actor, capability)));

    private static IEnumerable<(Actor Actor, Capability Capability)> FindAllCapabilities(
        Snapshot snapshot,
        string authorityRef) =>
        snapshot.Actors.SelectMany(actor => actor.Capabilities
            .Where(capability => string.Equals(
                capability.CapabilityRef,
                authorityRef,
                StringComparison.Ordinal))
            .Select(capability => (actor, capability)));

    private static IEnumerable<(Actor Actor, Consent Consent)> FindConsents(
        Snapshot snapshot,
        ActorKey owner,
        string authorityRef) =>
        snapshot.Actors
            .Where(actor => SameActor(actor, owner))
            .SelectMany(actor => actor.Consents
                .Where(consent => string.Equals(
                    consent.ConsentRef,
                    authorityRef,
                    StringComparison.Ordinal))
                .Select(consent => (actor, consent)));

    private static IEnumerable<(Actor Actor, Consent Consent)> FindAllConsents(
        Snapshot snapshot,
        string authorityRef) =>
        snapshot.Actors.SelectMany(actor => actor.Consents
            .Where(consent => string.Equals(
                consent.ConsentRef,
                authorityRef,
                StringComparison.Ordinal))
            .Select(consent => (actor, consent)));

    private static ActorKey? SelectRole(Context context, string? role) => role switch
    {
        "provider" => new ActorKey(context.ProviderKind, context.ProviderId),
        "target" => new ActorKey(context.TargetKind, context.TargetId),
        _ => null
    };

    private static bool SameActor(Actor actor, ActorKey expected) =>
        string.Equals(actor.ActorKind, expected.Kind, StringComparison.Ordinal) &&
        string.Equals(actor.ActorId, expected.Id, StringComparison.Ordinal);

    private static bool SameOwner(string kind, string id, ActorKey expected) =>
        string.Equals(kind, expected.Kind, StringComparison.Ordinal) &&
        string.Equals(id, expected.Id, StringComparison.Ordinal);

    private static bool IsPresent(
        Snapshot snapshot,
        string locationId,
        string actorKind,
        string actorId) =>
        snapshot.Locations
            .Where(location => string.Equals(
                location.LocationId,
                locationId,
                StringComparison.Ordinal))
            .SelectMany(location => location.PresentActors)
            .Any(actor =>
                string.Equals(actor.ActorKind, actorKind, StringComparison.Ordinal) &&
                string.Equals(actor.ActorId, actorId, StringComparison.Ordinal));

    private static bool IsCurrent(string lifecycle) =>
        string.Equals(lifecycle, "active", StringComparison.Ordinal);

    private static MortalWoundResolvedRequirement? Unresolved(
        string path,
        string field,
        string? authorityRef,
        ICollection<ValidationIssue> issues)
    {
        AddResolutionIssue(
            issues,
            path + "." + field,
            "mortal_wound_requirement_reference_unresolved",
            "one exact ordinal current authority reference",
            authorityRef ?? "missing");
        return null;
    }

    private static MortalWoundResolvedRequirement? Ambiguous(
        string path,
        string field,
        string authorityRef,
        ICollection<ValidationIssue> issues)
    {
        AddResolutionIssue(
            issues,
            path,
            "mortal_wound_requirement_reference_ambiguous",
            "exactly one current authority row",
            authorityRef);
        AddResolutionIssue(
            issues,
            path + "." + field,
            "mortal_wound_requirement_reference_ambiguous",
            "exactly one current authority row",
            authorityRef);
        return null;
    }

    private static MortalWoundResolvedRequirement? CrossRealm(
        string path,
        string field,
        string authorityRef,
        string actualRealm,
        ICollection<ValidationIssue> issues)
    {
        AddResolutionIssue(
            issues,
            path + "." + field,
            "mortal_wound_requirement_cross_realm_reference",
            MortalRealm,
            $"{authorityRef}@{actualRealm}");
        return null;
    }

    private static MortalWoundResolvedRequirement? Stale(
        string path,
        string field,
        string authorityRef,
        ICollection<ValidationIssue> issues)
    {
        AddResolutionIssue(
            issues,
            path + "." + field,
            "mortal_wound_requirement_stale_reference",
            "current active-lifecycle authority row",
            authorityRef);
        return null;
    }

    private static void AddWrongOwnerIssue(
        ICollection<ValidationIssue> issues,
        string path,
        string referenceField,
        string? role,
        string authorityRef) =>
        AddResolutionIssue(
            issues,
            string.Equals(role, "target", StringComparison.Ordinal)
                ? path + "." + referenceField
                : path,
            "mortal_wound_requirement_wrong_owner",
            $"authority owned by selected {role ?? "actor"}",
            authorityRef);

    private static void AddResolutionIssue(
        ICollection<ValidationIssue> issues,
        string path,
        string code,
        string expected,
        string actual) =>
        issues.Add(new ValidationIssue(
            path,
            IssueSeverity.Error,
            "Mortal wound-treatment requirement authority did not resolve exactly.",
            code: code,
            actor: "Client",
            section: "wound_materialization",
            expected: expected,
            actual: actual,
            repairHint:
                "Use one exact current Mortal authority row and re-evaluate against a fresh snapshot.",
            category: IssueCategory.ClientOwnedSurface));

    private static string? ReadRequirementString(JsonElement requirement, string field)
    {
        if (requirement.ValueKind != JsonValueKind.Object ||
            !requirement.TryGetProperty(field, out var value) ||
            value.ValueKind != JsonValueKind.String)
        {
            return null;
        }
        var result = value.GetString();
        return result is not null &&
               ResourceMaterializationContract.IsExactIdentifier(result)
            ? result
            : null;
    }

    private static int? ReadRequirementInt32(JsonElement requirement, string field) =>
        requirement.ValueKind == JsonValueKind.Object &&
        requirement.TryGetProperty(field, out var value) &&
        value.ValueKind == JsonValueKind.Number &&
        value.TryGetInt32(out var result)
            ? result
            : null;

    private static bool TryParseRoot(
        string json,
        string path,
        string invalidCode,
        ICollection<ValidationIssue> issues,
        out JsonDocument document)
    {
        document = null!;
        if (string.IsNullOrWhiteSpace(json))
        {
            AddParseIssue(issues, path, invalidCode, "JSON object", "empty input");
            return false;
        }
        try
        {
            document = JsonDocument.Parse(json, new JsonDocumentOptions
            {
                AllowTrailingCommas = false,
                CommentHandling = JsonCommentHandling.Disallow,
                MaxDepth = 64
            });
        }
        catch (JsonException exception)
        {
            AddParseIssue(issues, path, invalidCode, "valid JSON object", exception.Message);
            return false;
        }
        if (document.RootElement.ValueKind == JsonValueKind.Object)
            return true;
        AddParseIssue(
            issues,
            path,
            invalidCode,
            "JSON object",
            document.RootElement.ValueKind.ToString());
        document.Dispose();
        document = null!;
        return false;
    }

    private static void ValidateClosedObject(
        JsonElement value,
        string path,
        IReadOnlyCollection<string> fields,
        string unknownCode,
        string duplicateCode,
        string missingCode,
        ICollection<ValidationIssue> issues)
    {
        if (value.ValueKind != JsonValueKind.Object)
            return;
        var allowed = new HashSet<string>(fields, StringComparer.Ordinal);
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var property in value.EnumerateObject())
        {
            if (!seen.Add(property.Name))
            {
                AddParseIssue(
                    issues,
                    Path(path, property.Name),
                    duplicateCode,
                    "one occurrence of each exact property name",
                    property.Name);
            }
            if (!allowed.Contains(property.Name))
            {
                AddParseIssue(
                    issues,
                    Path(path, property.Name),
                    unknownCode,
                    "no unknown members in this closed object",
                    property.Name);
            }
        }
        foreach (var field in fields)
        {
            if (!value.TryGetProperty(field, out _))
            {
                AddParseIssue(
                    issues,
                    Path(path, field),
                    missingCode,
                    "required closed-object member",
                    "missing");
            }
        }
    }

    private static int ReadSchemaVersion(
        JsonElement parent,
        string path,
        string code,
        ICollection<ValidationIssue> issues)
    {
        if (!parent.TryGetProperty("schemaVersion", out var value))
            return 0;
        if (value.ValueKind == JsonValueKind.Number &&
            value.TryGetInt32(out var version) &&
            version == SchemaVersion)
        {
            return version;
        }
        AddParseIssue(
            issues,
            Path(path, "schemaVersion"),
            code,
            SchemaVersion.ToString(CultureInfo.InvariantCulture),
            value.GetRawText());
        return 0;
    }

    private static string ReadIdentifier(
        JsonElement parent,
        string field,
        string path,
        string invalidCode,
        ICollection<ValidationIssue> issues)
    {
        if (!parent.TryGetProperty(field, out var value))
            return string.Empty;
        if (value.ValueKind == JsonValueKind.String)
        {
            var candidate = value.GetString();
            if (ResourceMaterializationContract.IsExactIdentifier(candidate))
                return candidate!;
        }
        AddParseIssue(
            issues,
            Path(path, field),
            invalidCode,
            "exact ordinal identifier string",
            value.GetRawText());
        return string.Empty;
    }

    private static string ReadClosedIdentifier(
        JsonElement parent,
        string field,
        string path,
        IReadOnlyCollection<string> allowed,
        string invalidCode,
        ICollection<ValidationIssue> issues)
    {
        var value = ReadIdentifier(parent, field, path, invalidCode, issues);
        if (value.Length == 0 || allowed.Contains(value, StringComparer.Ordinal))
            return value;
        AddParseIssue(
            issues,
            Path(path, field),
            invalidCode,
            string.Join("|", allowed),
            value);
        return string.Empty;
    }

    private static string ReadActorKind(
        JsonElement parent,
        string field,
        string path,
        string invalidCode,
        ICollection<ValidationIssue> issues) =>
        ReadClosedIdentifier(
            parent,
            field,
            path,
            ActorKinds,
            invalidCode,
            issues);

    private static string ReadText(
        JsonElement parent,
        string field,
        string path,
        ICollection<ValidationIssue> issues)
    {
        if (!parent.TryGetProperty(field, out var value))
            return string.Empty;
        if (value.ValueKind == JsonValueKind.String)
        {
            var candidate = value.GetString();
            if (!string.IsNullOrWhiteSpace(candidate) &&
                string.Equals(candidate, candidate.Trim(), StringComparison.Ordinal))
            {
                return candidate;
            }
        }
        AddParseIssue(
            issues,
            Path(path, field),
            SnapshotInvalidFieldCode,
            "non-empty trimmed diagnostic text",
            value.GetRawText());
        return string.Empty;
    }

    private static int ReadInt32(
        JsonElement parent,
        string field,
        string path,
        ICollection<ValidationIssue> issues,
        out bool valid)
    {
        valid = false;
        if (!parent.TryGetProperty(field, out var value))
            return 0;
        if (value.ValueKind == JsonValueKind.Number && value.TryGetInt32(out var result))
        {
            valid = true;
            return result;
        }
        AddParseIssue(
            issues,
            Path(path, field),
            SnapshotInvalidFieldCode,
            "32-bit integer",
            value.GetRawText());
        return 0;
    }

    private static bool ReadBoolean(
        JsonElement parent,
        string field,
        string path,
        ICollection<ValidationIssue> issues)
    {
        if (!parent.TryGetProperty(field, out var value))
            return false;
        if (value.ValueKind is JsonValueKind.True or JsonValueKind.False)
            return value.GetBoolean();
        AddParseIssue(
            issues,
            Path(path, field),
            SnapshotInvalidFieldCode,
            "boolean",
            value.GetRawText());
        return false;
    }

    private static bool TryReadArray(
        JsonElement parent,
        string field,
        string path,
        ICollection<ValidationIssue> issues,
        out JsonElement array)
    {
        array = default;
        if (!parent.TryGetProperty(field, out var value))
            return false;
        if (value.ValueKind == JsonValueKind.Array)
        {
            array = value;
            return true;
        }
        AddParseIssue(
            issues,
            Path(path, field),
            SnapshotInvalidFieldCode,
            "array",
            value.ValueKind.ToString());
        return false;
    }

    private static List<Item> ParseItems(
        JsonElement root,
        string path,
        ICollection<ValidationIssue> issues)
    {
        var result = new List<Item>();
        if (!TryReadArray(root, "items", path, issues, out var array))
            return result;
        var index = 0;
        foreach (var value in array.EnumerateArray())
        {
            var itemPath = $"{Path(path, "items")}[{index++}]";
            if (!RequireObject(value, itemPath, issues))
                continue;
            ValidateSnapshotObject(value, itemPath, ItemFields, issues);
            var count = ReadInt32(value, "count", itemPath, issues, out var countValid);
            var available = ReadInt32(
                value,
                "availableCount",
                itemPath,
                issues,
                out var availableValid);
            if (countValid && count <= 0)
                AddNumericBoundsIssue(issues, Path(itemPath, "count"), "positive", count);
            if (availableValid && available < 0)
                AddNumericBoundsIssue(
                    issues,
                    Path(itemPath, "availableCount"),
                    "non-negative",
                    available);
            if (countValid && availableValid && available > count)
                AddNumericBoundsIssue(
                    issues,
                    Path(itemPath, "availableCount"),
                    $"<= count {count}",
                    available);
            result.Add(new Item(
                ReadIdentifier(value, "itemId", itemPath, SnapshotInvalidFieldCode, issues),
                ReadText(value, "displayName", itemPath, issues),
                ReadIdentifier(value, "realm", itemPath, SnapshotInvalidFieldCode, issues),
                ReadActorKind(
                    value,
                    "ownerKind",
                    itemPath,
                    SnapshotInvalidFieldCode,
                    issues),
                ReadIdentifier(value, "ownerId", itemPath, SnapshotInvalidFieldCode, issues),
                count,
                available,
                ReadClosedIdentifier(
                    value,
                    "reservationState",
                    itemPath,
                    new[] { "available", "reserved" },
                    SnapshotInvalidFieldCode,
                    issues),
                ReadLifecycle(value, itemPath, issues),
                ReadBoolean(value, "active", itemPath, issues)));
        }
        return result;
    }

    private static List<Resource> ParseResources(
        JsonElement root,
        string path,
        ICollection<ValidationIssue> issues)
    {
        var result = new List<Resource>();
        if (!TryReadArray(root, "resources", path, issues, out var array))
            return result;
        var index = 0;
        foreach (var value in array.EnumerateArray())
        {
            var itemPath = $"{Path(path, "resources")}[{index++}]";
            if (!RequireObject(value, itemPath, issues))
                continue;
            ValidateSnapshotObject(value, itemPath, ResourceFields, issues);
            var current = ReadInt32(
                value,
                "currentValue",
                itemPath,
                issues,
                out var currentValid);
            var available = ReadInt32(
                value,
                "availableValue",
                itemPath,
                issues,
                out var availableValid);
            if (currentValid && current < 0)
                AddNumericBoundsIssue(
                    issues,
                    Path(itemPath, "currentValue"),
                    "non-negative",
                    current);
            if (availableValid && available < 0)
                AddNumericBoundsIssue(
                    issues,
                    Path(itemPath, "availableValue"),
                    "non-negative",
                    available);
            if (currentValid && availableValid && available > current)
                AddNumericBoundsIssue(
                    issues,
                    Path(itemPath, "availableValue"),
                    $"<= currentValue {current}",
                    available);
            result.Add(new Resource(
                ReadIdentifier(
                    value,
                    "resourceRef",
                    itemPath,
                    SnapshotInvalidFieldCode,
                    issues),
                ReadText(value, "displayName", itemPath, issues),
                ReadIdentifier(value, "realm", itemPath, SnapshotInvalidFieldCode, issues),
                ReadActorKind(
                    value,
                    "ownerKind",
                    itemPath,
                    SnapshotInvalidFieldCode,
                    issues),
                ReadIdentifier(value, "ownerId", itemPath, SnapshotInvalidFieldCode, issues),
                current,
                available,
                ReadClosedIdentifier(
                    value,
                    "reservationState",
                    itemPath,
                    new[] { "available", "reserved" },
                    SnapshotInvalidFieldCode,
                    issues),
                ReadLifecycle(value, itemPath, issues),
                ReadBoolean(value, "active", itemPath, issues)));
        }
        return result;
    }

    private static List<Actor> ParseActors(
        JsonElement root,
        string path,
        ICollection<ValidationIssue> issues)
    {
        var result = new List<Actor>();
        if (!TryReadArray(root, "actors", path, issues, out var array))
            return result;
        var index = 0;
        foreach (var value in array.EnumerateArray())
        {
            var itemPath = $"{Path(path, "actors")}[{index++}]";
            if (!RequireObject(value, itemPath, issues))
                continue;
            ValidateSnapshotObject(value, itemPath, ActorFields, issues);
            result.Add(new Actor(
                ReadActorKind(
                    value,
                    "actorKind",
                    itemPath,
                    SnapshotInvalidFieldCode,
                    issues),
                ReadIdentifier(value, "actorId", itemPath, SnapshotInvalidFieldCode, issues),
                ReadText(value, "displayName", itemPath, issues),
                ReadIdentifier(value, "realm", itemPath, SnapshotInvalidFieldCode, issues),
                ReadIdentifier(
                    value,
                    "currentLocationId",
                    itemPath,
                    SnapshotInvalidFieldCode,
                    issues),
                ReadLifecycle(value, itemPath, issues),
                ReadBoolean(value, "active", itemPath, issues),
                ReadBoolean(value, "reachable", itemPath, issues),
                Freeze(ParseSkills(value, itemPath, issues)),
                Freeze(ParseCapabilities(value, itemPath, issues)),
                Freeze(ParseConsents(value, itemPath, issues))));
        }
        return result;
    }

    private static List<Skill> ParseSkills(
        JsonElement actor,
        string path,
        ICollection<ValidationIssue> issues)
    {
        var result = new List<Skill>();
        if (!TryReadArray(actor, "skills", path, issues, out var array))
            return result;
        var index = 0;
        foreach (var value in array.EnumerateArray())
        {
            var itemPath = $"{Path(path, "skills")}[{index++}]";
            if (!RequireObject(value, itemPath, issues))
                continue;
            ValidateSnapshotObject(value, itemPath, SkillFields, issues);
            var tier = ReadInt32(value, "tier", itemPath, issues, out _);
            result.Add(new Skill(
                ReadIdentifier(
                    value,
                    "capabilityRef",
                    itemPath,
                    SnapshotInvalidFieldCode,
                    issues),
                ReadText(value, "displayName", itemPath, issues),
                tier,
                ReadLifecycle(value, itemPath, issues),
                ReadBoolean(value, "active", itemPath, issues)));
        }
        return result;
    }

    private static List<Capability> ParseCapabilities(
        JsonElement actor,
        string path,
        ICollection<ValidationIssue> issues)
    {
        var result = new List<Capability>();
        if (!TryReadArray(actor, "capabilities", path, issues, out var array))
            return result;
        var index = 0;
        foreach (var value in array.EnumerateArray())
        {
            var itemPath = $"{Path(path, "capabilities")}[{index++}]";
            if (!RequireObject(value, itemPath, issues))
                continue;
            ValidateSnapshotObject(value, itemPath, CapabilityFields, issues);
            result.Add(new Capability(
                ReadIdentifier(
                    value,
                    "capabilityRef",
                    itemPath,
                    SnapshotInvalidFieldCode,
                    issues),
                ReadText(value, "displayName", itemPath, issues),
                ReadLifecycle(value, itemPath, issues),
                ReadBoolean(value, "active", itemPath, issues)));
        }
        return result;
    }

    private static List<Consent> ParseConsents(
        JsonElement actor,
        string path,
        ICollection<ValidationIssue> issues)
    {
        var result = new List<Consent>();
        if (!TryReadArray(actor, "consents", path, issues, out var array))
            return result;
        var index = 0;
        foreach (var value in array.EnumerateArray())
        {
            var itemPath = $"{Path(path, "consents")}[{index++}]";
            if (!RequireObject(value, itemPath, issues))
                continue;
            ValidateSnapshotObject(value, itemPath, ConsentFields, issues);
            result.Add(new Consent(
                ReadIdentifier(
                    value,
                    "consentRef",
                    itemPath,
                    SnapshotInvalidFieldCode,
                    issues),
                ReadText(value, "displayName", itemPath, issues),
                ReadActorKind(
                    value,
                    "providerKind",
                    itemPath,
                    SnapshotInvalidFieldCode,
                    issues),
                ReadIdentifier(
                    value,
                    "providerId",
                    itemPath,
                    SnapshotInvalidFieldCode,
                    issues),
                ReadActorKind(
                    value,
                    "targetKind",
                    itemPath,
                    SnapshotInvalidFieldCode,
                    issues),
                ReadIdentifier(
                    value,
                    "targetId",
                    itemPath,
                    SnapshotInvalidFieldCode,
                    issues),
                ReadClosedIdentifier(
                    value,
                    "status",
                    itemPath,
                    new[] { "granted", "withdrawn" },
                    SnapshotInvalidFieldCode,
                    issues),
                ReadLifecycle(value, itemPath, issues),
                ReadBoolean(value, "active", itemPath, issues)));
        }
        return result;
    }

    private static List<Facility> ParseFacilities(
        JsonElement root,
        string path,
        ICollection<ValidationIssue> issues)
    {
        var result = new List<Facility>();
        if (!TryReadArray(root, "facilities", path, issues, out var array))
            return result;
        var index = 0;
        foreach (var value in array.EnumerateArray())
        {
            var itemPath = $"{Path(path, "facilities")}[{index++}]";
            if (!RequireObject(value, itemPath, issues))
                continue;
            ValidateSnapshotObject(value, itemPath, FacilityFields, issues);
            result.Add(new Facility(
                ReadIdentifier(
                    value,
                    "facilityId",
                    itemPath,
                    SnapshotInvalidFieldCode,
                    issues),
                ReadText(value, "displayName", itemPath, issues),
                ReadIdentifier(value, "realm", itemPath, SnapshotInvalidFieldCode, issues),
                ReadIdentifier(
                    value,
                    "locationId",
                    itemPath,
                    SnapshotInvalidFieldCode,
                    issues),
                ReadLifecycle(value, itemPath, issues),
                ReadBoolean(value, "active", itemPath, issues),
                ReadBoolean(value, "available", itemPath, issues)));
        }
        return result;
    }

    private static List<Location> ParseLocations(
        JsonElement root,
        string path,
        ICollection<ValidationIssue> issues)
    {
        var result = new List<Location>();
        if (!TryReadArray(root, "locations", path, issues, out var array))
            return result;
        var index = 0;
        foreach (var value in array.EnumerateArray())
        {
            var itemPath = $"{Path(path, "locations")}[{index++}]";
            if (!RequireObject(value, itemPath, issues))
                continue;
            ValidateSnapshotObject(value, itemPath, LocationFields, issues);
            result.Add(new Location(
                ReadIdentifier(
                    value,
                    "locationId",
                    itemPath,
                    SnapshotInvalidFieldCode,
                    issues),
                ReadText(value, "displayName", itemPath, issues),
                ReadIdentifier(value, "realm", itemPath, SnapshotInvalidFieldCode, issues),
                ReadLifecycle(value, itemPath, issues),
                ReadBoolean(value, "active", itemPath, issues),
                Freeze(ParseActorCoordinates(value, itemPath, issues))));
        }
        return result;
    }

    private static List<ActorCoordinate> ParseActorCoordinates(
        JsonElement location,
        string path,
        ICollection<ValidationIssue> issues)
    {
        var result = new List<ActorCoordinate>();
        if (!TryReadArray(location, "presentActors", path, issues, out var array))
            return result;
        var index = 0;
        foreach (var value in array.EnumerateArray())
        {
            var itemPath = $"{Path(path, "presentActors")}[{index++}]";
            if (!RequireObject(value, itemPath, issues))
                continue;
            ValidateSnapshotObject(value, itemPath, ActorCoordinateFields, issues);
            result.Add(new ActorCoordinate(
                ReadActorKind(
                    value,
                    "actorKind",
                    itemPath,
                    SnapshotInvalidFieldCode,
                    issues),
                ReadIdentifier(
                    value,
                    "actorId",
                    itemPath,
                    SnapshotInvalidFieldCode,
                    issues)));
        }
        return result;
    }

    private static List<Quest> ParseQuests(
        JsonElement root,
        string path,
        ICollection<ValidationIssue> issues)
    {
        var result = new List<Quest>();
        if (!TryReadArray(root, "quests", path, issues, out var array))
            return result;
        var index = 0;
        foreach (var value in array.EnumerateArray())
        {
            var itemPath = $"{Path(path, "quests")}[{index++}]";
            if (!RequireObject(value, itemPath, issues))
                continue;
            ValidateSnapshotObject(value, itemPath, QuestFields, issues);
            result.Add(new Quest(
                ReadIdentifier(
                    value,
                    "questId",
                    itemPath,
                    SnapshotInvalidFieldCode,
                    issues),
                ReadText(value, "displayName", itemPath, issues),
                ReadIdentifier(value, "realm", itemPath, SnapshotInvalidFieldCode, issues),
                ReadIdentifier(value, "state", itemPath, SnapshotInvalidFieldCode, issues),
                ReadLifecycle(value, itemPath, issues),
                ReadBoolean(value, "active", itemPath, issues)));
        }
        return result;
    }

    private static List<Effect> ParseEffects(
        JsonElement root,
        string path,
        ICollection<ValidationIssue> issues)
    {
        var result = new List<Effect>();
        if (!TryReadArray(root, "effects", path, issues, out var array))
            return result;
        var index = 0;
        foreach (var value in array.EnumerateArray())
        {
            var itemPath = $"{Path(path, "effects")}[{index++}]";
            if (!RequireObject(value, itemPath, issues))
                continue;
            ValidateSnapshotObject(value, itemPath, EffectFields, issues);
            result.Add(new Effect(
                ReadIdentifier(
                    value,
                    "effectId",
                    itemPath,
                    SnapshotInvalidFieldCode,
                    issues),
                ReadText(value, "displayName", itemPath, issues),
                ReadIdentifier(value, "realm", itemPath, SnapshotInvalidFieldCode, issues),
                ReadActorKind(
                    value,
                    "targetKind",
                    itemPath,
                    SnapshotInvalidFieldCode,
                    issues),
                ReadIdentifier(
                    value,
                    "targetId",
                    itemPath,
                    SnapshotInvalidFieldCode,
                    issues),
                ReadIdentifier(value, "state", itemPath, SnapshotInvalidFieldCode, issues),
                ReadLifecycle(value, itemPath, issues),
                ReadBoolean(value, "active", itemPath, issues)));
        }
        return result;
    }

    private static List<EnvironmentState> ParseEnvironments(
        JsonElement root,
        string path,
        ICollection<ValidationIssue> issues)
    {
        var result = new List<EnvironmentState>();
        if (!TryReadArray(root, "environments", path, issues, out var array))
            return result;
        var index = 0;
        foreach (var value in array.EnumerateArray())
        {
            var itemPath = $"{Path(path, "environments")}[{index++}]";
            if (!RequireObject(value, itemPath, issues))
                continue;
            ValidateSnapshotObject(value, itemPath, EnvironmentFields, issues);
            result.Add(new EnvironmentState(
                ReadIdentifier(
                    value,
                    "environmentId",
                    itemPath,
                    SnapshotInvalidFieldCode,
                    issues),
                ReadText(value, "displayName", itemPath, issues),
                ReadIdentifier(value, "realm", itemPath, SnapshotInvalidFieldCode, issues),
                ReadIdentifier(
                    value,
                    "locationId",
                    itemPath,
                    SnapshotInvalidFieldCode,
                    issues),
                ReadIdentifier(value, "state", itemPath, SnapshotInvalidFieldCode, issues),
                ReadLifecycle(value, itemPath, issues),
                ReadBoolean(value, "active", itemPath, issues)));
        }
        return result;
    }

    private static string ReadLifecycle(
        JsonElement value,
        string path,
        ICollection<ValidationIssue> issues) =>
        ReadClosedIdentifier(
            value,
            "lifecycle",
            path,
            new[] { "active", "retired" },
            SnapshotInvalidFieldCode,
            issues);

    private static void ValidateSnapshotObject(
        JsonElement value,
        string path,
        IReadOnlyCollection<string> fields,
        ICollection<ValidationIssue> issues) =>
        ValidateClosedObject(
            value,
            path,
            fields,
            "mortal_wound_treatment_snapshot_unknown_field",
            "mortal_wound_treatment_snapshot_duplicate_property",
            "mortal_wound_treatment_snapshot_missing_field",
            issues);

    private static bool RequireObject(
        JsonElement value,
        string path,
        ICollection<ValidationIssue> issues)
    {
        if (value.ValueKind == JsonValueKind.Object)
            return true;
        AddParseIssue(
            issues,
            path,
            SnapshotInvalidFieldCode,
            "object",
            value.ValueKind.ToString());
        return false;
    }

    private static void AddNumericBoundsIssue(
        ICollection<ValidationIssue> issues,
        string path,
        string expected,
        int actual) =>
        AddParseIssue(
            issues,
            path,
            "mortal_wound_treatment_snapshot_numeric_bounds_invalid",
            expected,
            actual.ToString(CultureInfo.InvariantCulture));

    private static void AddParseIssue(
        ICollection<ValidationIssue> issues,
        string path,
        string code,
        string expected,
        string actual) =>
        issues.Add(new ValidationIssue(
            path,
            IssueSeverity.Error,
            "Mortal wound-treatment authority projection is invalid.",
            code: code,
            actor: "Client",
            section: "wound_materialization",
            expected: expected,
            actual: actual,
            repairHint:
                "Recompose the closed transient projection from current canonical authority.",
            category: IssueCategory.ClientOwnedSurface));

    private static string Path(string root, string field) =>
        string.IsNullOrEmpty(root) ? field : root + "." + field;

    private static ReadOnlyCollection<T> Freeze<T>(IEnumerable<T> values) =>
        Array.AsReadOnly(values.ToArray());

    private static string Number(int value) =>
        value.ToString(CultureInfo.InvariantCulture);

    private static string? Number(int? value) =>
        value?.ToString(CultureInfo.InvariantCulture);

    private static string Number(long value) =>
        value.ToString(CultureInfo.InvariantCulture);

    private static string Boolean(bool value) => value ? "true" : "false";

    private readonly record struct ActorKey(string Kind, string Id);

    private readonly record struct LedgerKey(
        string Kind,
        string AuthorityRef,
        string OwnerKind,
        string OwnerId);
}

internal sealed record MortalWoundRequirementAuthorityResult(
    bool Success,
    IReadOnlyList<ValidationIssue> Issues,
    IReadOnlyList<MortalWoundResolvedRequirement> ResolvedRequirements,
    string AuthorityFingerprint);

internal sealed record MortalWoundResolvedRequirement(
    int RequirementIndex,
    string Kind,
    string AuthorityRef,
    string Realm,
    string? OwnerKind,
    string? OwnerId,
    string? ProviderKind,
    string? ProviderId,
    string? TargetKind,
    string? TargetId,
    string? LocationId,
    int? RequestedQuantity,
    int? MinimumTier,
    int? CurrentTier,
    string? CurrentState,
    string AuthorityFingerprint);
