using System.Collections.ObjectModel;
using System.Text.Json;
using System.Text.Json.Nodes;
using BookOfEternityClient.Core;

namespace BookOfEternityClient.Services;

internal sealed class AfterlifeResourceOwnerRoots
{
    private readonly JsonObject _profiles;
    private readonly JsonObject _spiritualConflict;
    private readonly JsonObject _soulState;
    private readonly JsonObject _shiningAbode;
    private readonly JsonObject _guardians;

    internal AfterlifeResourceOwnerRoots(
        JsonObject? profiles,
        JsonObject? spiritualConflict,
        JsonObject? soulState = null,
        JsonObject? shiningAbode = null,
        JsonObject? guardians = null)
    {
        _profiles = profiles?.DeepClone().AsObject() ?? new JsonObject();
        _spiritualConflict = spiritualConflict?.DeepClone().AsObject() ??
            AfterlifeSpiritualConflictState.CreateDefaultRoot();
        _soulState = soulState?.DeepClone().AsObject() ?? new JsonObject();
        _shiningAbode = shiningAbode?.DeepClone().AsObject() ??
            ShiningAbodeState.CreateDefaultState();
        _guardians = guardians?.DeepClone().AsObject() ?? new JsonObject();
    }

    internal JsonObject Profiles => _profiles.DeepClone().AsObject();

    internal JsonObject SpiritualConflict => _spiritualConflict.DeepClone().AsObject();

    internal JsonObject SoulState => _soulState.DeepClone().AsObject();

    internal JsonObject ShiningAbode => _shiningAbode.DeepClone().AsObject();

    internal JsonObject Guardians => _guardians.DeepClone().AsObject();
}

internal sealed class AfterlifeResourceOwnerCompositionInput
{
    internal AfterlifeResourceOwnerCompositionInput(
        ResourceDefinitionCatalog definitions,
        AfterlifeResourceOwnerRoots preTurn,
        AfterlifeResourceOwnerRoots accepted)
    {
        Definitions = definitions ?? throw new ArgumentNullException(nameof(definitions));
        PreTurn = preTurn ?? throw new ArgumentNullException(nameof(preTurn));
        Accepted = accepted ?? throw new ArgumentNullException(nameof(accepted));
    }

    internal ResourceDefinitionCatalog Definitions { get; }

    internal AfterlifeResourceOwnerRoots PreTurn { get; }

    internal AfterlifeResourceOwnerRoots Accepted { get; }
}

internal static class AfterlifeResourceOwnerComposer
{
    private const string BindingsProperty =
        AfterlifeEntityProfileState.ResourceOwnerBindingsProperty;
    private const string OppositionBindingProperty = "opposition";
    private const string ResourceOwnerIdProperty = "resourceOwnerId";

    internal static ResourceOwnerCompositionResult Compose(
        AfterlifeResourceOwnerCompositionInput input)
    {
        ArgumentNullException.ThrowIfNull(input);
        var issues = new List<ValidationIssue>();
        var preTurnExports = new List<ResourceOwnerExport>();
        var sameTurnExports = new List<ResourceOwnerExport>();
        var historicalOwners = new List<ResourceOwnerKey>();
        var terminalOwners = new List<ResourceOwnerKey>();
        var capacityDrafts = new List<ResourceOwnerCapacityDraft>();
        var materializationCandidates = new List<ResourceOwnerMaterializationCandidate>();
        var afterImages = new Dictionary<string, JsonObject>(StringComparer.Ordinal);

        var acceptedProfiles = input.Accepted.Profiles;
        ComposeActorOwners(
            input.Definitions,
            input.PreTurn.Profiles,
            acceptedProfiles,
            preTurnExports,
            sameTurnExports,
            historicalOwners,
            terminalOwners,
            capacityDrafts,
            materializationCandidates,
            input.PreTurn.SoulState,
            input.Accepted.SoulState,
            issues);
        if (issues.Count != 0)
            return Invalid(issues);
        if (!JsonNode.DeepEquals(input.PreTurn.Profiles, acceptedProfiles))
        {
            afterImages[AfterlifeEntityProfileState.StatePath] =
                acceptedProfiles.DeepClone().AsObject();
        }

        ComposeGuardianReturnCapacities(
            input.Definitions,
            input.Accepted.Guardians,
            preTurnExports,
            sameTurnExports,
            capacityDrafts,
            issues);
        if (issues.Count != 0)
            return Invalid(issues);

        var acceptedConflict = input.Accepted.SpiritualConflict;
        ComposeConflictSideOwner(
            input.Definitions,
            input.PreTurn.SpiritualConflict,
            acceptedConflict,
            preTurnExports,
            sameTurnExports,
            historicalOwners,
            terminalOwners,
            capacityDrafts,
            issues);
        if (issues.Count != 0)
            return Invalid(issues);
        if (!JsonNode.DeepEquals(input.PreTurn.SpiritualConflict, acceptedConflict))
        {
            afterImages[AfterlifeSpiritualConflictState.StatePath] =
                acceptedConflict.DeepClone().AsObject();
        }

        var acceptedShining = input.Accepted.ShiningAbode;
        ComposeShiningReturnScope(
            input.Definitions,
            input.PreTurn.ShiningAbode,
            acceptedShining,
            preTurnExports,
            sameTurnExports,
            historicalOwners,
            terminalOwners,
            capacityDrafts,
            issues);
        if (issues.Count != 0)
            return Invalid(issues);
        if (!JsonNode.DeepEquals(input.Accepted.ShiningAbode, acceptedShining))
        {
            afterImages[ShiningAbodeState.StatePath] =
                acceptedShining.DeepClone().AsObject();
        }

        capacityDrafts.AddRange(
            ResourceOwnerMaterializationPlanner.ComposeCapacityDrafts(
                input.Definitions,
                sameTurnExports,
                materializationCandidates,
                issues));
        if (issues.Count != 0)
            return Invalid(issues);

        var authority = ResourceOwnerAuthority.Build(new ResourceOwnerAuthorityInput(
            preTurnExports,
            sameTurnExports,
            historicalOwners));
        if (authority.Issues.Count != 0)
            return Invalid(authority.Issues);

        return new ResourceOwnerCompositionResult(
            authority,
            new ReadOnlyDictionary<string, JsonObject>(afterImages),
            Array.Empty<AcceptedMechanicsOwnerTransition>(),
            null,
            Array.AsReadOnly(capacityDrafts.ToArray()),
            Array.AsReadOnly(terminalOwners.ToArray()),
            Array.Empty<ValidationIssue>());
    }

    private static void ComposeActorOwners(
        ResourceDefinitionCatalog definitions,
        JsonObject preTurnRoot,
        JsonObject acceptedRoot,
        List<ResourceOwnerExport> preTurnExports,
        List<ResourceOwnerExport> sameTurnExports,
        List<ResourceOwnerKey> historicalOwners,
        List<ResourceOwnerKey> terminalOwners,
        List<ResourceOwnerCapacityDraft> capacityDrafts,
        List<ResourceOwnerMaterializationCandidate> materializationCandidates,
        JsonObject preTurnSoulState,
        JsonObject acceptedSoulState,
        List<ValidationIssue> issues)
    {
        if (!TryReadProfiles(preTurnRoot, "preTurnProfiles", out var preTurnProfiles, issues) ||
            !TryReadProfiles(acceptedRoot, "acceptedProfiles", out var acceptedProfiles, issues))
        {
            return;
        }

        var previous = new Dictionary<string, ActorOwner>(StringComparer.Ordinal);
        var previousAliases = new HashSet<string>(StringComparer.Ordinal);
        var preTurnPlayerSoulInMortalWorld =
            preTurnSoulState["currentRealm"] is JsonValue preTurnRealmNode &&
            preTurnRealmNode.TryGetValue<string>(out var preTurnRealm) &&
            RealmSemantics.IsMortalRealm(preTurnRealm);
        for (var index = 0; index < preTurnProfiles.Count; index++)
        {
            if (!TryReadCanonicalActor(
                    preTurnProfiles[index],
                    $"{AfterlifeEntityProfileState.StatePath}.profiles[{index}]",
                    preTurnPlayerSoulInMortalWorld,
                    out var actor,
                    issues))
            {
                continue;
            }
            if (!previous.TryAdd(actor.ActorId, actor) ||
                !previousAliases.Add(ResourceMaterializationContract.BuildConfusableKey(actor.ActorId)))
            {
                Add(
                    issues,
                    actor.Path,
                    "resource_owner_afterlife_actor_identity_ambiguous",
                    "one exact/confusable-unique pre-turn actor identity",
                    actor.ActorId);
            }
        }
        if (issues.Count != 0)
            return;

        var currentActorIds = new HashSet<string>(StringComparer.Ordinal);
        var currentAliases = new HashSet<string>(StringComparer.Ordinal);
        for (var index = 0; index < acceptedProfiles.Count; index++)
        {
            var path = $"{AfterlifeEntityProfileState.StatePath}.profiles[{index}]";
            if (acceptedProfiles[index] is not JsonObject profile)
            {
                Add(
                    issues,
                    path,
                    "resource_owner_afterlife_actor_entry_invalid",
                    "afterlife profile object",
                    Describe(acceptedProfiles[index]));
                continue;
            }

            if (TryReadExact(profile["actorId"], out var actorId))
            {
                if (profile.ContainsKey("actorRef") ||
                    !TryReadRealm(profile, path, out var realm, issues))
                {
                    continue;
                }
                if (!previous.TryGetValue(actorId, out var previousActor))
                {
                    if (!TryComposeSameTurnMaterializedActor(
                            definitions,
                            profile,
                            path,
                            actorId,
                            realm,
                            previousAliases,
                            currentActorIds,
                            currentAliases,
                            sameTurnExports,
                            capacityDrafts,
                            materializationCandidates,
                            acceptedSoulState,
                            issues))
                    {
                        continue;
                    }
                    continue;
                }
                if (profile.ContainsKey("resourceMaterialization"))
                {
                    Add(
                        issues,
                        path + ".resourceMaterialization",
                        "resource_owner_materialization_existing_forbidden",
                        "field absent for every existing owner",
                        Describe(profile["resourceMaterialization"]));
                    continue;
                }
                if (!currentActorIds.Add(actorId) ||
                    !currentAliases.Add(ResourceMaterializationContract.BuildConfusableKey(actorId)))
                {
                    Add(
                        issues,
                        path,
                        "resource_owner_afterlife_actor_identity_ambiguous",
                        "one exact/confusable-unique accepted actor identity",
                        actorId);
                    continue;
                }

                if (!JsonNode.DeepEquals(
                        profile[BindingsProperty],
                        previousActor.Profile[BindingsProperty]))
                {
                    Add(
                        issues,
                        path + "." + BindingsProperty,
                        "resource_owner_afterlife_actor_binding_changed",
                        "exact client-owned pre-turn realm bindings",
                        Describe(profile[BindingsProperty]));
                    continue;
                }

                var playerSoulInMortalWorld = IsPlayerSoul(profile) &&
                                              acceptedSoulState["currentRealm"] is JsonValue soulRealmNode &&
                                              soulRealmNode.TryGetValue<string>(out var soulRealm) &&
                                              RealmSemantics.IsMortalRealm(soulRealm);
                var bindings = previousActor.Bindings
                    .Select(binding => binding with
                    {
                        Lifecycle = !playerSoulInMortalWorld &&
                                    string.Equals(
                                        binding.Realm,
                                        realm,
                                        StringComparison.Ordinal)
                            ? ResourceOwnerLifecycle.Active
                            : ResourceOwnerLifecycle.Suspended
                    })
                    .ToList();
                if (!playerSoulInMortalWorld &&
                    bindings.All(binding => !string.Equals(
                        binding.Realm,
                        realm,
                        StringComparison.Ordinal)))
                {
                    bindings.Add(new ActorRealmBinding(
                        realm,
                        actorId,
                        ResourceOwnerLifecycle.Active,
                        path + "." + BindingsProperty));
                }

                profile[BindingsProperty] = BuildActorBindings(bindings);
                foreach (var binding in bindings.OrderBy(
                             static value => value.Realm,
                             StringComparer.Ordinal))
                {
                    var isNewRealm = previousActor.Bindings.All(previousBinding =>
                        !string.Equals(
                            previousBinding.Realm,
                            binding.Realm,
                            StringComparison.Ordinal));
                    var ownerRef = isNewRealm
                        ? "afterlife_actor_realm_ref_" + Guid.NewGuid().ToString("N")
                        : null;
                    var export = CreateExport(
                        definitions,
                        binding.Realm,
                        ResourceOwnerKind.AfterlifeActor,
                        actorId,
                        profile,
                        isNewRealm,
                        ownerRef,
                        binding.Lifecycle);
                    (isNewRealm ? sameTurnExports : preTurnExports).Add(export);
                    if (IsPlayerSoul(profile))
                    {
                        if (!TryCreateActorActionPointCapacityDraft(
                                definitions,
                                export,
                                ownerRef,
                                acceptedSoulState,
                                out var draft,
                                issues))
                        {
                            continue;
                        }
                        capacityDrafts.Add(draft!);
                    }
                }
                continue;
            }

            Add(
                issues,
                path,
                "resource_owner_afterlife_actor_identity_invalid",
                "exact canonical actorId backed by pre-turn authority or one complete same-turn actor materialization envelope; actorRef is forbidden",
                profile.ToJsonString());
        }

        foreach (var removed in previous.Values.Where(actor =>
                     !currentActorIds.Contains(actor.ActorId)))
        {
            foreach (var binding in removed.Bindings)
            {
                var key = new ResourceOwnerKey(
                    binding.Realm,
                    ResourceOwnerKind.AfterlifeActor,
                    removed.ActorId);
                historicalOwners.Add(key);
                terminalOwners.Add(key);
            }
        }
    }

    private static bool TryComposeSameTurnMaterializedActor(
        ResourceDefinitionCatalog definitions,
        JsonObject profile,
        string path,
        string actorId,
        string realm,
        IReadOnlySet<string> previousAliases,
        HashSet<string> currentActorIds,
        HashSet<string> currentAliases,
        List<ResourceOwnerExport> sameTurnExports,
        List<ResourceOwnerCapacityDraft> capacityDrafts,
        List<ResourceOwnerMaterializationCandidate> materializationCandidates,
        JsonObject acceptedSoulState,
        List<ValidationIssue> issues)
    {
        if (profile.ContainsKey(BindingsProperty))
        {
            Add(
                issues,
                path + "." + BindingsProperty,
                "resource_owner_afterlife_actor_binding_preassigned",
                "client-owned binding absent on a new accepted actor",
                Describe(profile[BindingsProperty]));
            return false;
        }

        var issueCount = issues.Count;
        try
        {
            using var document = JsonDocument.Parse(profile.ToJsonString());
            issues.AddRange(ActorMaterializationContract.ValidateHistoricalAfterlifeProfile(
                document.RootElement,
                path));
        }
        catch (JsonException)
        {
            Add(
                issues,
                path,
                "resource_owner_afterlife_actor_materialization_invalid",
                "one complete same-turn actor materialization envelope",
                profile.ToJsonString());
        }
        if (issues.Count != issueCount)
            return false;

        if (profile[ActorMaterializationContract.PropertyName] is not JsonObject actorEnvelope ||
            !TryReadExact(actorEnvelope["materializationId"], out var materializationId))
        {
            Add(
                issues,
                path + "." + ActorMaterializationContract.PropertyName + ".materializationId",
                "resource_owner_afterlife_actor_materialization_invalid",
                "exact actor-bound materializationId",
                Describe(profile[ActorMaterializationContract.PropertyName]));
            return false;
        }

        var actorAlias = ResourceMaterializationContract.BuildConfusableKey(actorId);
        if (!currentActorIds.Add(actorId) ||
            !currentAliases.Add(actorAlias) ||
            previousAliases.Contains(actorAlias))
        {
            Add(
                issues,
                path + ".actorId",
                "resource_owner_afterlife_actor_identity_ambiguous",
                "one exact/confusable-unique accepted actor identity",
                actorId);
            return false;
        }

        var binding = new ActorRealmBinding(
            realm,
            actorId,
            ResourceOwnerLifecycle.Active,
            path + "." + BindingsProperty);
        profile[BindingsProperty] = BuildActorBindings(new[] { binding });
        if (profile.TryGetPropertyValue("resourceMaterialization", out var materialization))
        {
            if (materialization is not JsonObject envelope)
            {
                Add(
                    issues,
                    path + ".resourceMaterialization",
                    "resource_owner_materialization_shape_invalid",
                    "closed object containing only a non-empty resources array",
                    Describe(materialization));
                return false;
            }
            materializationCandidates.Add(new ResourceOwnerMaterializationCandidate(
                path,
                new ResourceOwnerKey(
                    realm,
                    ResourceOwnerKind.AfterlifeActor,
                    actorId),
                materializationId,
                envelope.DeepClone().AsObject(),
                Array.Empty<string>()));
            profile.Remove("resourceMaterialization");
        }

        var sameTurnExport = CreateExport(
            definitions,
            realm,
            ResourceOwnerKind.AfterlifeActor,
            actorId,
            profile,
            sameTurn: true,
            ownerRef: materializationId,
            lifecycle: ResourceOwnerLifecycle.Active);
        sameTurnExports.Add(sameTurnExport);
        if (!IsPlayerSoul(profile))
            return true;

        if (!TryCreateActorActionPointCapacityDraft(
                definitions,
                sameTurnExport,
                materializationId,
                acceptedSoulState,
                out var draft,
                issues))
        {
            return false;
        }
        capacityDrafts.Add(draft!);
        return true;
    }

    private static bool TryCreateActorActionPointCapacityDraft(
        ResourceDefinitionCatalog definitions,
        ResourceOwnerExport owner,
        string? ownerRef,
        JsonObject soulState,
        out ResourceOwnerCapacityDraft? draft,
        List<ValidationIssue> issues)
    {
        draft = null;
        const string resourceKey = "spiritual_action_points";
        if (soulState[AfterlifeSpiritualConflictState.SoulStateProfileProperty]
                is not JsonObject profile ||
            profile[AfterlifeSpiritualConflictState.SpiritFocusTierProperty]
                is not JsonValue tierNode ||
            !tierNode.TryGetValue<int>(out var spiritFocusTier) ||
            spiritFocusTier is < 0 or > AfterlifeSpiritualConflictState.SpiritFocusMaxTier)
        {
            Add(
                issues,
                "game_state/meta/soul_state.json." +
                AfterlifeSpiritualConflictState.SoulStateProfileProperty + "." +
                AfterlifeSpiritualConflictState.SpiritFocusTierProperty,
                "resource_owner_afterlife_spirit_focus_invalid",
                $"integer spirit focus tier 0..{AfterlifeSpiritualConflictState.SpiritFocusMaxTier}",
                Describe(soulState[AfterlifeSpiritualConflictState.SoulStateProfileProperty]));
            return false;
        }
        if (!definitions.TryResolveExact(resourceKey, out var definition) ||
            definition == null ||
            !definition.AllowedOwnerKinds.Contains(ResourceOwnerKind.AfterlifeActor))
        {
            Add(
                issues,
                "game_state/resources/resource_definitions.json",
                "resource_owner_afterlife_actor_definition_invalid",
                "sealed spiritual_action_points definition for afterlife actors",
                resourceKey);
            return false;
        }

        var coordinate = new ResourceCoordinate(
            owner.Key.Realm,
            owner.Key.OwnerKind,
            owner.Key.ResourceOwnerId,
            resourceKey);
        var resolved = ResolvedResourceCapacity.Resolve(
            definition,
            coordinate,
            new RegisteredFormulaCapacityInput(
                new SpiritFocusActionPointsFormulaInput(
                    new ResourceFormulaOwner(
                        coordinate.Realm,
                        coordinate.OwnerKind,
                        coordinate.ResourceOwnerId),
                    owner.AuthorityFingerprint,
                    spiritFocusTier)),
            instanceAuthorityKey: null,
            includeInitialization: true);
        if (!resolved.IsValid || resolved.Capacity == null)
        {
            issues.AddRange(resolved.Issues);
            return false;
        }

        var sourceKind = owner.SameTurn
            ? "owner_materialization"
            : "owner_capacity_state";
        var sourceId = owner.SameTurn
            ? ownerRef
            : owner.Key.ResourceOwnerId;
        if (!ResourceMaterializationContract.IsExactIdentifier(sourceId))
        {
            Add(
                issues,
                "game_state/meta/afterlife_entity_profiles.json.resourceOwnerBindings",
                "resource_owner_afterlife_actor_capacity_source_invalid",
                "exact same-turn ownerRef or persistent actor ID",
                sourceId ?? "missing");
            return false;
        }

        using var fingerprint = new ResourceFingerprintBuilder(
            "afterlife-actor-capacity-v1");
        fingerprint.Append(owner.AuthorityFingerprint);
        fingerprint.Append(sourceKind);
        fingerprint.Append(sourceId!);
        ResourceStateContract.AppendCoordinate(fingerprint, coordinate);
        fingerprint.Append(spiritFocusTier);
        fingerprint.Append(resolved.Capacity.Binding.AuthorityFingerprint);
        draft = new ResourceOwnerCapacityDraft(
            coordinate,
            resolved.Capacity.Maximum,
            resolved,
            new ResourceSourceEvidence(
                sourceKind,
                sourceId!,
                fingerprint.Build()));
        return true;
    }

    private static void ComposeConflictSideOwner(
        ResourceDefinitionCatalog definitions,
        JsonObject preTurnRoot,
        JsonObject acceptedRoot,
        List<ResourceOwnerExport> preTurnExports,
        List<ResourceOwnerExport> sameTurnExports,
        List<ResourceOwnerKey> historicalOwners,
        List<ResourceOwnerKey> terminalOwners,
        List<ResourceOwnerCapacityDraft> capacityDrafts,
        List<ValidationIssue> issues)
    {
        var previous = ReadConflict(preTurnRoot, "preTurnConflict", issues);
        var current = ReadConflict(acceptedRoot, "acceptedConflict", issues);
        if (issues.Count != 0)
            return;

        if (previous == null && current == null)
            return;
        if (previous != null && current == null)
        {
            if (!TryReadOppositionOwnerId(previous.Value.Conflict, out var removedId))
            {
                Add(
                    issues,
                    previous.Value.Path + ".resourceOwnerBindings.opposition",
                    "resource_owner_afterlife_conflict_binding_missing",
                    "one canonical client-owned opposition resourceOwnerId",
                    "missing");
                return;
            }
            var terminal = new ResourceOwnerKey(
                previous.Value.Realm,
                ResourceOwnerKind.AfterlifeConflictSide,
                removedId!);
            historicalOwners.Add(terminal);
            terminalOwners.Add(terminal);
            return;
        }

        if (previous == null)
        {
            if (current!.Value.Conflict.ContainsKey(BindingsProperty))
            {
                Add(
                    issues,
                    current.Value.Path + "." + BindingsProperty,
                    "resource_owner_afterlife_conflict_binding_submission_forbidden",
                    "client-owned binding omitted from a new conflict",
                    Describe(current.Value.Conflict[BindingsProperty]));
                return;
            }
            var ownerId = "afterlife_conflict_side_" + Guid.NewGuid().ToString("N");
            var ownerRef = "afterlife_conflict_side_ref_" + Guid.NewGuid().ToString("N");
            if (!TryConsumeConflictSideMaterialization(
                    current.Value,
                    out var acceptedMaximum,
                    issues))
            {
                return;
            }
            current.Value.Conflict[BindingsProperty] = new JsonObject
            {
                [OppositionBindingProperty] = new JsonObject
                {
                    [ResourceOwnerIdProperty] = ownerId
                }
            };
            var export = CreateExport(
                definitions,
                current.Value.Realm,
                ResourceOwnerKind.AfterlifeConflictSide,
                ownerId,
                current.Value.Conflict,
                sameTurn: true,
                ownerRef);
            sameTurnExports.Add(export);
            if (!TryCreateConflictSideCapacityDraft(
                    definitions,
                    current.Value,
                    export,
                    ownerRef,
                    acceptedMaximum,
                    out var draft,
                    issues))
            {
                return;
            }
            capacityDrafts.Add(draft!);
            return;
        }

        if (!string.Equals(
                previous.Value.ConflictId,
                current!.Value.ConflictId,
                StringComparison.Ordinal) ||
            !string.Equals(
                previous.Value.Realm,
                current.Value.Realm,
                StringComparison.Ordinal))
        {
            Add(
                issues,
                current.Value.Path,
                "resource_owner_afterlife_conflict_replacement_forbidden",
                "close the prior exact conflict before starting another",
                current.Value.ConflictId);
            return;
        }
        var hasPreviousId = TryReadOppositionOwnerId(
            previous.Value.Conflict,
            out var previousId);
        var hasCurrentId = TryReadOppositionOwnerId(
            current.Value.Conflict,
            out var currentId);
        if (!hasPreviousId ||
            !hasCurrentId ||
            !string.Equals(previousId, currentId, StringComparison.Ordinal))
        {
            Add(
                issues,
                current.Value.Path + ".resourceOwnerBindings.opposition.resourceOwnerId",
                "resource_owner_afterlife_conflict_binding_changed",
                "exact pre-turn client-owned opposition resourceOwnerId",
                currentId ?? "missing");
            return;
        }

        preTurnExports.Add(CreateExport(
            definitions,
            current.Value.Realm,
            ResourceOwnerKind.AfterlifeConflictSide,
            currentId!,
            current.Value.Conflict,
            sameTurn: false,
            ownerRef: null));
    }

    private static void ComposeGuardianReturnCapacities(
        ResourceDefinitionCatalog definitions,
        JsonObject guardiansRoot,
        IReadOnlyList<ResourceOwnerExport> preTurnExports,
        IReadOnlyList<ResourceOwnerExport> sameTurnExports,
        List<ResourceOwnerCapacityDraft> capacityDrafts,
        List<ValidationIssue> issues)
    {
        if (!guardiansRoot.TryGetPropertyValue("guardians", out var guardiansNode) ||
            guardiansNode == null)
        {
            return;
        }
        if (guardiansNode is not JsonArray guardians)
        {
            Add(
                issues,
                "game_state/meta/guardians.json.guardians",
                "resource_owner_guardian_collection_invalid",
                "canonical guardian array",
                Describe(guardiansNode));
            return;
        }

        var guardianIds = new HashSet<string>(StringComparer.Ordinal);
        var guardianAliases = new HashSet<string>(StringComparer.Ordinal);
        for (var index = 0; index < guardians.Count; index++)
        {
            var path = $"game_state/meta/guardians.json.guardians[{index}]";
            if (guardians[index] is not JsonObject guardian ||
                !TryReadExact(guardian["guardianId"], out var guardianId))
            {
                Add(
                    issues,
                    path + ".guardianId",
                    "resource_owner_guardian_identity_invalid",
                    "exact guardian identity",
                    Describe(guardians[index]));
                continue;
            }
            if (!guardianIds.Add(guardianId) ||
                !guardianAliases.Add(
                    ResourceMaterializationContract.BuildConfusableKey(guardianId)))
            {
                Add(
                    issues,
                    path + ".guardianId",
                    "resource_owner_guardian_identity_ambiguous",
                    "exact/confusable-unique guardian identity",
                    guardianId);
                continue;
            }

            if (guardian["gachaSystem"] is not JsonObject gacha)
                continue;
            if (gacha.ContainsKey("chargesPerReturn") ||
                gacha.ContainsKey("chargesUsedThisReturn"))
            {
                Add(
                    issues,
                    path + ".gachaSystem",
                    "resource_owner_guardian_legacy_gacha_counters_forbidden",
                    "no legacy gacha counters; use the common gacha_attempts ledger",
                    gacha.ToJsonString());
                continue;
            }
            if (gacha["currentReturnCycleId"] is JsonValue emptyCycle &&
                emptyCycle.TryGetValue<string>(out var emptyText) &&
                string.IsNullOrEmpty(emptyText))
            {
                continue;
            }
            if (!TryReadExact(gacha["currentReturnCycleId"], out var returnCycleId) ||
                guardian["relationshipData"] is not JsonObject relationship ||
                !TryReadInt(relationship["currentReputation"], out var reputation) ||
                reputation is < -100 or > 300 ||
                guardian["abodePower"] is not JsonObject abodePowerRoot ||
                !TryReadInt(abodePowerRoot["currentPower"], out var abodePower) ||
                abodePower is < AbodePowerRules.MinPower or > AbodePowerRules.MaxPower)
            {
                Add(
                    issues,
                    path + ".gachaSystem",
                    "resource_owner_guardian_gacha_authority_invalid",
                    "exact return cycle, reputation -100..300, and abode power 0..100",
                    guardian.ToJsonString());
                continue;
            }

            var ownerMatches = preTurnExports.Concat(sameTurnExports)
                .Where(export =>
                    export.Key.OwnerKind == ResourceOwnerKind.AfterlifeActor &&
                    string.Equals(export.Key.Realm, "chaos_sea", StringComparison.Ordinal) &&
                    string.Equals(
                        export.Key.ResourceOwnerId,
                        guardianId,
                        StringComparison.Ordinal) &&
                    export.Lifecycle == ResourceOwnerLifecycle.Active &&
                    export.ResourceCapabilities.Contains("gacha_attempts"))
                .ToArray();
            if (ownerMatches.Length != 1)
            {
                Add(
                    issues,
                    path + ".guardianId",
                    "resource_owner_guardian_profile_binding_invalid",
                    "one exact active Chaos Sea guardian profile owner",
                    $"guardianId={guardianId};matches={ownerMatches.Length}");
                continue;
            }

            if (!TryCreateGuardianReturnCapacityDraft(
                    definitions,
                    guardian,
                    ownerMatches[0],
                    returnCycleId,
                    reputation,
                    abodePower,
                    out var draft,
                    issues))
            {
                continue;
            }
            capacityDrafts.Add(draft!);
        }
    }

    private static bool TryCreateGuardianReturnCapacityDraft(
        ResourceDefinitionCatalog definitions,
        JsonObject guardian,
        ResourceOwnerExport owner,
        string returnCycleId,
        int reputation,
        int abodePower,
        out ResourceOwnerCapacityDraft? draft,
        List<ValidationIssue> issues)
    {
        draft = null;
        const string resourceKey = "gacha_attempts";
        if (!definitions.TryResolveExact(resourceKey, out var definition) ||
            definition == null ||
            !definition.AllowedOwnerKinds.Contains(ResourceOwnerKind.AfterlifeActor))
        {
            Add(
                issues,
                ResourceMaterializationContract.DefinitionsPath,
                "resource_owner_guardian_gacha_definition_invalid",
                "sealed gacha_attempts definition for afterlife actors",
                resourceKey);
            return false;
        }

        var coordinate = new ResourceCoordinate(
            "chaos_sea",
            ResourceOwnerKind.AfterlifeActor,
            owner.Key.ResourceOwnerId,
            resourceKey);
        var founderExtraCharges = PlayerGuardianFoundationState
            .GetFounderExtraGachaCharges(guardian);
        var resolved = ResolvedResourceCapacity.Resolve(
            definition,
            coordinate,
            new RegisteredFormulaCapacityInput(
                new GuardianReturnGachaFormulaInput(
                    new ResourceFormulaOwner(
                        coordinate.Realm,
                        coordinate.OwnerKind,
                        coordinate.ResourceOwnerId),
                    owner.AuthorityFingerprint,
                    reputation,
                    abodePower,
                    founderExtraCharges,
                    returnCycleId)),
            instanceAuthorityKey: null,
            includeInitialization: true);
        if (!resolved.IsValid || resolved.Capacity == null)
        {
            issues.AddRange(resolved.Issues);
            return false;
        }

        using var fingerprint = new ResourceFingerprintBuilder(
            "afterlife-guardian-return-capacity-v1");
        fingerprint.Append(owner.AuthorityFingerprint);
        fingerprint.Append(returnCycleId);
        fingerprint.Append(reputation);
        fingerprint.Append(abodePower);
        fingerprint.Append(founderExtraCharges);
        ResourceStateContract.AppendCoordinate(fingerprint, coordinate);
        fingerprint.Append(resolved.Capacity.Binding.AuthorityFingerprint);
        draft = new ResourceOwnerCapacityDraft(
            coordinate,
            resolved.Capacity.Maximum,
            resolved,
            new ResourceSourceEvidence(
                "owner_capacity_cycle",
                returnCycleId,
                fingerprint.Build()));
        return true;
    }

    private static bool TryConsumeConflictSideMaterialization(
        ConflictOwner conflict,
        out decimal acceptedMaximum,
        List<ValidationIssue> issues)
    {
        acceptedMaximum = default;
        var path = conflict.Path + ".oppositionSide.resourceMaterialization";
        if (conflict.Conflict.ContainsKey("actionEconomy"))
        {
            Add(
                issues,
                conflict.Path + ".actionEconomy",
                "resource_owner_afterlife_conflict_legacy_action_economy_forbidden",
                "no legacy actionEconomy; use the common spiritual_action_points ledger",
                Describe(conflict.Conflict["actionEconomy"]));
            return false;
        }

        if (conflict.Conflict["oppositionSide"] is not JsonObject opposition ||
            opposition["resourceMaterialization"] is not JsonObject envelope ||
            envelope.Count != 1 ||
            envelope["resources"] is not JsonArray { Count: 1 } resources ||
            resources[0] is not JsonObject resource ||
            resource.Count != 2 ||
            !TryReadExact(resource["resourceKey"], out var resourceKey) ||
            !string.Equals(resourceKey, "spiritual_action_points", StringComparison.Ordinal) ||
            !TryReadDecimal(resource["maximum"], out acceptedMaximum) ||
            acceptedMaximum <= 0m ||
            !ResourceMaterializationContract.IsIntegral(acceptedMaximum))
        {
            Add(
                issues,
                path,
                "resource_owner_afterlife_conflict_materialization_invalid",
                "closed one-entry spiritual_action_points materialization with a positive integral maximum",
                Describe(conflict.Conflict["oppositionSide"]));
            return false;
        }

        opposition.Remove("resourceMaterialization");
        return true;
    }

    private static void ComposeShiningReturnScope(
        ResourceDefinitionCatalog definitions,
        JsonObject preTurnRoot,
        JsonObject acceptedRoot,
        List<ResourceOwnerExport> preTurnExports,
        List<ResourceOwnerExport> sameTurnExports,
        List<ResourceOwnerKey> historicalOwners,
        List<ResourceOwnerKey> terminalOwners,
        List<ResourceOwnerCapacityDraft> capacityDrafts,
        List<ValidationIssue> issues)
    {
        if (!TryReadShiningReturnScope(
                preTurnRoot,
                "preTurnShiningAbode",
                requireBinding: true,
                out var previous,
                issues) ||
            !TryReadShiningReturnScope(
                acceptedRoot,
                "acceptedShiningAbode",
                requireBinding: false,
                out var current,
                issues))
        {
            return;
        }

        if (previous != null &&
            !string.Equals(previous.Value.ReturnCycleId, current?.ReturnCycleId,
                StringComparison.Ordinal))
        {
            var terminal = new ResourceOwnerKey(
                "shining_abode",
                ResourceOwnerKind.AfterlifeScope,
                previous.Value.ResourceOwnerId!);
            historicalOwners.Add(terminal);
            terminalOwners.Add(terminal);
        }

        if (current == null)
        {
            if (previous != null)
            {
                StripLegacyShiningGachaCounters(acceptedRoot);
                acceptedRoot.Remove(BindingsProperty);
            }
            return;
        }

        StripLegacyShiningGachaCounters(acceptedRoot);

        if (previous != null &&
            string.Equals(previous.Value.ReturnCycleId, current.Value.ReturnCycleId,
                StringComparison.Ordinal))
        {
            if (!string.Equals(
                    previous.Value.ResourceOwnerId,
                    current.Value.ResourceOwnerId,
                    StringComparison.Ordinal) ||
                !string.Equals(
                    current.Value.BoundReturnCycleId,
                    current.Value.ReturnCycleId,
                    StringComparison.Ordinal))
            {
                Add(
                    issues,
                    current.Value.Path + ".resourceOwnerBindings.gachaReturn.resourceOwnerId",
                    "resource_owner_afterlife_scope_binding_changed",
                    "exact pre-turn client-owned scope resourceOwnerId",
                    current.Value.ResourceOwnerId ?? "missing");
                return;
            }
            var existingExport = CreateExport(
                definitions,
                "shining_abode",
                ResourceOwnerKind.AfterlifeScope,
                current.Value.ResourceOwnerId!,
                acceptedRoot,
                sameTurn: false,
                ownerRef: null);
            preTurnExports.Add(existingExport);
            if (!TryCreateShiningReturnCapacityDraft(
                    definitions,
                    current.Value,
                    existingExport,
                    ownerRef: null,
                    out var existingDraft,
                    issues))
            {
                return;
            }
            capacityDrafts.Add(existingDraft!);
            return;
        }

        if (current.Value.ResourceOwnerId != null &&
            (previous == null ||
             !string.Equals(
                 current.Value.ResourceOwnerId,
                 previous.Value.ResourceOwnerId,
                 StringComparison.Ordinal)))
        {
            Add(
                issues,
                current.Value.Path + ".resourceOwnerBindings.gachaReturn.resourceOwnerId",
                "resource_owner_afterlife_scope_binding_submission_forbidden",
                "prior client-owned binding or no binding for a new return cycle",
                current.Value.ResourceOwnerId);
            return;
        }

        var ownerId = "afterlife_scope_" + Guid.NewGuid().ToString("N");
        var ownerRef = "afterlife_scope_ref_" + Guid.NewGuid().ToString("N");
        acceptedRoot[BindingsProperty] = new JsonObject
        {
            ["gachaReturn"] = new JsonObject
            {
                ["returnCycleId"] = current.Value.ReturnCycleId,
                [ResourceOwnerIdProperty] = ownerId
            }
        };
        var export = CreateExport(
            definitions,
            "shining_abode",
            ResourceOwnerKind.AfterlifeScope,
            ownerId,
            acceptedRoot,
            sameTurn: true,
            ownerRef);
        sameTurnExports.Add(export);
        if (!TryCreateShiningReturnCapacityDraft(
                definitions,
                current.Value,
                export,
                ownerRef,
                out var draft,
                issues))
        {
            return;
        }
        capacityDrafts.Add(draft!);
    }

    private static bool TryReadShiningReturnScope(
        JsonObject root,
        string context,
        bool requireBinding,
        out ShiningReturnScope? scope,
        List<ValidationIssue> issues)
    {
        scope = null;
        var path = ShiningAbodeState.StatePath + "." + context;
        if (root["gachaSystem"] is not JsonObject gacha ||
            gacha["currentReturnCycleId"] is not JsonValue cycleNode ||
            !cycleNode.TryGetValue<string>(out var rawCycle))
        {
            Add(
                issues,
                path + ".gachaSystem.currentReturnCycleId",
                "resource_owner_afterlife_scope_cycle_invalid",
                "empty or exact return-cycle identifier",
                Describe(root["gachaSystem"]));
            return false;
        }
        if (string.IsNullOrEmpty(rawCycle))
        {
            if (root.ContainsKey(BindingsProperty))
            {
                Add(
                    issues,
                    path + ".resourceOwnerBindings",
                    "resource_owner_afterlife_scope_binding_without_cycle",
                    "no scope binding while return cycle is empty",
                    Describe(root[BindingsProperty]));
                return false;
            }
            return true;
        }
        if (!ResourceMaterializationContract.IsExactIdentifier(rawCycle) ||
            root["radiance"] is not JsonObject radiance ||
            radiance["tier"] is not JsonValue tierNode ||
            !tierNode.TryGetValue<int>(out var radianceTier) ||
            radianceTier is < 0 or > 4)
        {
            Add(
                issues,
                path,
                "resource_owner_afterlife_scope_authority_invalid",
                "exact return-cycle identifier and radiance tier 0..4",
                root.ToJsonString());
            return false;
        }

        string? ownerId = null;
        string? boundReturnCycleId = null;
        if (root[BindingsProperty] is JsonObject bindings &&
            bindings["gachaReturn"] is JsonObject binding)
        {
            if (!TryReadExact(binding[ResourceOwnerIdProperty], out var exactOwnerId) ||
                !TryReadExact(binding["returnCycleId"], out var boundCycle) ||
                (requireBinding &&
                 !string.Equals(boundCycle, rawCycle, StringComparison.Ordinal)))
            {
                Add(
                    issues,
                    path + ".resourceOwnerBindings.gachaReturn",
                    "resource_owner_afterlife_scope_binding_invalid",
                    "exact owner ID bound to the current exact return cycle",
                    binding.ToJsonString());
                return false;
            }
            ownerId = exactOwnerId;
            boundReturnCycleId = boundCycle;
        }
        else if (requireBinding)
        {
            Add(
                issues,
                path + ".resourceOwnerBindings.gachaReturn",
                "resource_owner_afterlife_scope_binding_missing",
                "canonical client-owned scope binding for an active return cycle",
                "missing");
            return false;
        }

        scope = new ShiningReturnScope(
            rawCycle,
            radianceTier,
            ownerId,
            boundReturnCycleId,
            path);
        return true;
    }

    private static void StripLegacyShiningGachaCounters(JsonObject root)
    {
        if (root["gachaSystem"] is not JsonObject gacha)
            return;
        gacha.Remove("chargesPerReturn");
        gacha.Remove("chargesUsedThisReturn");
    }

    private static bool TryCreateShiningReturnCapacityDraft(
        ResourceDefinitionCatalog definitions,
        ShiningReturnScope scope,
        ResourceOwnerExport owner,
        string? ownerRef,
        out ResourceOwnerCapacityDraft? draft,
        List<ValidationIssue> issues)
    {
        draft = null;
        const string resourceKey = "gacha_attempts";
        if (!definitions.TryResolveExact(resourceKey, out var definition) ||
            definition == null ||
            !definition.AllowedOwnerKinds.Contains(ResourceOwnerKind.AfterlifeScope))
        {
            Add(
                issues,
                "game_state/resources/resource_definitions.json",
                "resource_owner_afterlife_scope_definition_invalid",
                "sealed gacha_attempts definition for afterlife scopes",
                resourceKey);
            return false;
        }
        var coordinate = new ResourceCoordinate(
            "shining_abode",
            ResourceOwnerKind.AfterlifeScope,
            owner.Key.ResourceOwnerId,
            resourceKey);
        var resolved = ResolvedResourceCapacity.Resolve(
            definition,
            coordinate,
            new RegisteredFormulaCapacityInput(
                new ShiningReturnGachaFormulaInput(
                    new ResourceFormulaOwner(
                        coordinate.Realm,
                        coordinate.OwnerKind,
                        coordinate.ResourceOwnerId),
                    owner.AuthorityFingerprint,
                    scope.RadianceTier,
                    scope.ReturnCycleId)),
            instanceAuthorityKey: null,
            includeInitialization: true);
        if (!resolved.IsValid || resolved.Capacity == null)
        {
            issues.AddRange(resolved.Issues);
            return false;
        }
        var sourceKind = owner.SameTurn
            ? "owner_materialization"
            : "owner_capacity_cycle";
        var sourceId = owner.SameTurn
            ? ownerRef
            : scope.ReturnCycleId;
        if (!ResourceMaterializationContract.IsExactIdentifier(sourceId))
        {
            Add(
                issues,
                scope.Path + ".resourceOwnerBindings.gachaReturn",
                "resource_owner_afterlife_scope_capacity_source_invalid",
                "exact same-turn ownerRef or current return-cycle ID",
                sourceId ?? "missing");
            return false;
        }
        using var fingerprint = new ResourceFingerprintBuilder(
            "afterlife-shining-return-capacity-v1");
        fingerprint.Append(owner.AuthorityFingerprint);
        fingerprint.Append(sourceKind);
        fingerprint.Append(sourceId!);
        fingerprint.Append(scope.ReturnCycleId);
        fingerprint.Append(scope.RadianceTier);
        ResourceStateContract.AppendCoordinate(fingerprint, coordinate);
        fingerprint.Append(resolved.Capacity.Binding.AuthorityFingerprint);
        draft = new ResourceOwnerCapacityDraft(
            coordinate,
            resolved.Capacity.Maximum,
            resolved,
            new ResourceSourceEvidence(
                sourceKind,
                sourceId!,
                fingerprint.Build()));
        return true;
    }

    private static bool TryCreateConflictSideCapacityDraft(
        ResourceDefinitionCatalog definitions,
        ConflictOwner conflict,
        ResourceOwnerExport owner,
        string ownerRef,
        decimal acceptedMaximum,
        out ResourceOwnerCapacityDraft? draft,
        List<ValidationIssue> issues)
    {
        draft = null;
        const string resourceKey = "spiritual_action_points";
        if (!definitions.TryResolveExact(resourceKey, out var definition) ||
            definition == null ||
            !definition.AllowedOwnerKinds.Contains(ResourceOwnerKind.AfterlifeConflictSide))
        {
            Add(
                issues,
                conflict.Path + ".oppositionSide.resourceMaterialization.resources[0].resourceKey",
                "resource_owner_afterlife_conflict_definition_invalid",
                "sealed spiritual_action_points definition for afterlife conflict sides",
                resourceKey);
            return false;
        }

        var coordinate = new ResourceCoordinate(
            conflict.Realm,
            ResourceOwnerKind.AfterlifeConflictSide,
            owner.Key.ResourceOwnerId,
            resourceKey);
        var capacityInput = new RegisteredFormulaCapacityInput(
            new ConflictSideActionPointsFormulaInput(
                new ResourceFormulaOwner(
                    coordinate.Realm,
                    coordinate.OwnerKind,
                    coordinate.ResourceOwnerId),
                owner.AuthorityFingerprint,
                conflict.ConflictId,
                acceptedMaximum));
        var resolved = ResolvedResourceCapacity.Resolve(
            definition,
            coordinate,
            capacityInput,
            instanceAuthorityKey: null,
            includeInitialization: true);
        if (!resolved.IsValid || resolved.Capacity == null)
        {
            issues.AddRange(resolved.Issues);
            return false;
        }

        using var fingerprint = new ResourceFingerprintBuilder(
            "afterlife-conflict-side-materialization-v1");
        fingerprint.Append(owner.AuthorityFingerprint);
        fingerprint.Append(ownerRef);
        fingerprint.Append(conflict.ConflictId);
        ResourceStateContract.AppendCoordinate(fingerprint, coordinate);
        fingerprint.Append(acceptedMaximum);
        fingerprint.Append(resolved.Capacity.Binding.AuthorityFingerprint);
        draft = new ResourceOwnerCapacityDraft(
            coordinate,
            acceptedMaximum,
            resolved,
            new ResourceSourceEvidence(
                "owner_materialization",
                ownerRef,
                fingerprint.Build()));
        return true;
    }

    private static bool TryReadProfiles(
        JsonObject root,
        string context,
        out JsonArray profiles,
        List<ValidationIssue> issues)
    {
        if (!root.TryGetPropertyValue(AfterlifeEntityProfileState.ProfilesProperty, out var node))
        {
            profiles = new JsonArray();
            return true;
        }
        if (node is JsonArray array)
        {
            profiles = array;
            return true;
        }
        profiles = new JsonArray();
        Add(
            issues,
            context + ".profiles",
            "resource_owner_afterlife_profile_collection_invalid",
            "profiles array",
            Describe(node));
        return false;
    }

    private static bool TryReadCanonicalActor(
        JsonNode? node,
        string path,
        bool allowSuspendedPlayerSoul,
        out ActorOwner actor,
        List<ValidationIssue> issues)
    {
        actor = default;
        if (node is not JsonObject profile ||
            !TryReadExact(profile["actorId"], out var actorId) ||
            profile.ContainsKey("actorRef") ||
            !TryReadRealm(profile, path, out var realm, issues))
        {
            if (!issues.Any(issue => string.Equals(issue.FilePath, path + ".realm", StringComparison.Ordinal)))
            {
                Add(
                    issues,
                    path,
                    "resource_owner_afterlife_actor_canonical_identity_invalid",
                    "exact permanent actorId, no actorRef, and one supported realm",
                    Describe(node));
            }
            return false;
        }
        if (profile[BindingsProperty] is not JsonArray bindingNodes ||
            bindingNodes.Count == 0)
        {
            Add(
                issues,
                path + "." + BindingsProperty,
                "resource_owner_afterlife_actor_binding_missing",
                "nonempty client-owned realm binding array",
                Describe(profile[BindingsProperty]));
            return false;
        }

        var bindings = new List<ActorRealmBinding>();
        var realms = new HashSet<string>(StringComparer.Ordinal);
        var realmAliases = new HashSet<string>(StringComparer.Ordinal);
        for (var index = 0; index < bindingNodes.Count; index++)
        {
            var bindingPath = $"{path}.{BindingsProperty}[{index}]";
            if (bindingNodes[index] is not JsonObject binding ||
                !TryReadRealm(binding, bindingPath, out var bindingRealm, issues) ||
                !TryReadExact(binding[ResourceOwnerIdProperty], out var bindingOwnerId) ||
                !string.Equals(bindingOwnerId, actorId, StringComparison.Ordinal) ||
                !TryReadLifecycle(binding["state"], out var lifecycle))
            {
                Add(
                    issues,
                    bindingPath,
                    "resource_owner_afterlife_actor_binding_invalid",
                    "exact realm, matching actor resourceOwnerId, and active/suspended state",
                    Describe(bindingNodes[index]));
                continue;
            }
            if (!realms.Add(bindingRealm) ||
                !realmAliases.Add(ResourceMaterializationContract.BuildConfusableKey(bindingRealm)))
            {
                Add(
                    issues,
                    bindingPath + ".realm",
                    "resource_owner_afterlife_actor_binding_ambiguous",
                    "one exact/confusable-unique realm binding",
                    bindingRealm);
                continue;
            }
            bindings.Add(new ActorRealmBinding(
                bindingRealm,
                bindingOwnerId,
                lifecycle,
                bindingPath));
        }
        if (issues.Count != 0)
            return false;
        var active = bindings.Where(static binding =>
            binding.Lifecycle == ResourceOwnerLifecycle.Active).ToArray();
        var expectsSuspendedPlayerSoul =
            allowSuspendedPlayerSoul && IsPlayerSoul(profile);
        if (expectsSuspendedPlayerSoul
                ? active.Length != 0
                : active.Length != 1 ||
                  !string.Equals(active[0].Realm, realm, StringComparison.Ordinal))
        {
            Add(
                issues,
                path + "." + BindingsProperty,
                "resource_owner_afterlife_actor_active_realm_mismatch",
                expectsSuspendedPlayerSoul
                    ? "no active realm binding while persistent player_soul is in Mortal World"
                    : "exactly one active binding matching profile.realm",
                string.Join(",", active.Select(static binding => binding.Realm)));
            return false;
        }

        actor = new ActorOwner(
            actorId,
            profile.DeepClone().AsObject(),
            bindings.ToArray(),
            path);
        return true;
    }

    private static JsonArray BuildActorBindings(
        IEnumerable<ActorRealmBinding> bindings) =>
        new(bindings
            .OrderBy(static binding => binding.Realm, StringComparer.Ordinal)
            .Select(binding => (JsonNode)new JsonObject
            {
                ["realm"] = binding.Realm,
                [ResourceOwnerIdProperty] = binding.ResourceOwnerId,
                ["state"] = binding.Lifecycle == ResourceOwnerLifecycle.Active
                    ? "active"
                    : "suspended"
            })
            .ToArray());

    private static bool TryReadLifecycle(
        JsonNode? node,
        out ResourceOwnerLifecycle lifecycle)
    {
        lifecycle = default;
        if (node is not JsonValue value ||
            !value.TryGetValue<string>(out var token))
        {
            return false;
        }
        if (string.Equals(token, "active", StringComparison.Ordinal))
        {
            lifecycle = ResourceOwnerLifecycle.Active;
            return true;
        }
        if (string.Equals(token, "suspended", StringComparison.Ordinal))
        {
            lifecycle = ResourceOwnerLifecycle.Suspended;
            return true;
        }
        return false;
    }

    private static ConflictOwner? ReadConflict(
        JsonObject root,
        string context,
        List<ValidationIssue> issues)
    {
        if (!root.TryGetPropertyValue("activeConflict", out var node) || node == null)
            return null;
        if (node is not JsonObject conflict ||
            !TryReadExact(conflict["conflictId"], out var conflictId) ||
            !TryReadRealm(conflict, context + ".activeConflict", out var realm, issues))
        {
            Add(
                issues,
                AfterlifeSpiritualConflictState.StatePath + ".activeConflict",
                "resource_owner_afterlife_conflict_identity_invalid",
                "active conflict with exact conflictId and supported realm",
                Describe(node));
            return null;
        }
        return new ConflictOwner(
            conflict,
            conflictId,
            realm,
            AfterlifeSpiritualConflictState.StatePath + ".activeConflict");
    }

    private static bool TryReadRealm(
        JsonObject owner,
        string path,
        out string realm,
        List<ValidationIssue> issues)
    {
        var raw = owner["realm"] is JsonValue value &&
                  value.TryGetValue<string>(out var text)
            ? text
            : null;
        if (AfterlifeEntityProfileState.TryNormalizeEffectRealm(raw, out realm))
            return true;
        Add(
            issues,
            path + ".realm",
            "resource_owner_afterlife_realm_invalid",
            "Chaos Sea or Shining Abode realm",
            raw ?? "missing");
        realm = string.Empty;
        return false;
    }

    private static bool TryReadOppositionOwnerId(
        JsonObject conflict,
        out string? ownerId)
    {
        ownerId = null;
        if (conflict[BindingsProperty] is not JsonObject bindings ||
            bindings[OppositionBindingProperty] is not JsonObject opposition ||
            !TryReadExact(opposition[ResourceOwnerIdProperty], out var exact))
        {
            return false;
        }
        ownerId = exact;
        return true;
    }

    private static ResourceOwnerExport CreateExport(
        ResourceDefinitionCatalog definitions,
        string realm,
        ResourceOwnerKind ownerKind,
        string ownerId,
        JsonObject owner,
        bool sameTurn,
        string? ownerRef,
        ResourceOwnerLifecycle lifecycle = ResourceOwnerLifecycle.Active)
    {
        var capabilities = definitions.Definitions
            .Where(definition =>
                definition.AllowedOwnerKinds.Contains(ownerKind) &&
                IsAfterlifeCapabilityAllowed(ownerKind, owner, definition))
            .Select(static definition => definition.ResourceKey)
            .ToHashSet(StringComparer.Ordinal);
        using var fingerprint = new ResourceFingerprintBuilder(
            "afterlife-resource-owner-v1");
        fingerprint.Append(realm);
        fingerprint.Append(ResourceDefinitionCatalog.GetOwnerKindToken(ownerKind));
        fingerprint.Append(ownerId);
        if (ownerKind == ResourceOwnerKind.AfterlifeActor &&
            TryReadExact(owner["actorType"], out var actorType))
        {
            fingerprint.Append(actorType);
        }
        if (ownerKind == ResourceOwnerKind.AfterlifeConflictSide &&
            TryReadExact(owner["conflictId"], out var conflictId))
        {
            fingerprint.Append(conflictId);
        }
        if (ownerKind == ResourceOwnerKind.AfterlifeScope &&
            owner["gachaSystem"] is JsonObject gacha &&
            TryReadExact(gacha["currentReturnCycleId"], out var returnCycleId))
        {
            fingerprint.Append(returnCycleId);
        }
        foreach (var capability in capabilities.OrderBy(
                     static value => value,
                     StringComparer.Ordinal))
        {
            fingerprint.Append(capability);
        }

        var realmIndependentCapabilities = IsPlayerSoul(owner) &&
                                           capabilities.Contains("blessing_rerolls")
            ? new HashSet<string>(StringComparer.Ordinal) { "blessing_rerolls" }
            : new HashSet<string>(StringComparer.Ordinal);
        foreach (var capability in realmIndependentCapabilities.OrderBy(
                     static value => value,
                     StringComparer.Ordinal))
        {
            fingerprint.Append("realm_independent");
            fingerprint.Append(capability);
        }

        return new ResourceOwnerExport(
            new ResourceOwnerKey(realm, ownerKind, ownerId),
            lifecycle,
            sameTurn,
            ownerRef,
            BoundNpcId: null,
            capabilities,
            fingerprint.Build())
        {
            RealmIndependentResourceCapabilities = realmIndependentCapabilities
        };
    }

    private static bool IsAfterlifeCapabilityAllowed(
        ResourceOwnerKind ownerKind,
        JsonObject owner,
        ResourceDefinition definition)
    {
        // Setting-defined resources use the same sealed allowedOwnerKinds authority as
        // Mortal owners. Built-ins retain their narrower afterlife role contracts.
        if (definition.Materialization.CreatedAtTurn > 0)
            return true;

        var resourceKey = definition.ResourceKey;
        return ownerKind switch
        {
            ResourceOwnerKind.AfterlifeConflictSide =>
                string.Equals(resourceKey, "spiritual_action_points", StringComparison.Ordinal),
            ResourceOwnerKind.AfterlifeScope =>
                string.Equals(resourceKey, "gacha_attempts", StringComparison.Ordinal),
            ResourceOwnerKind.AfterlifeActor when IsPlayerSoul(owner) =>
                string.Equals(resourceKey, "spiritual_action_points", StringComparison.Ordinal) ||
                string.Equals(resourceKey, "blessing_rerolls", StringComparison.Ordinal),
            ResourceOwnerKind.AfterlifeActor when
                TryReadExact(owner["actorType"], out var actorType) &&
                string.Equals(actorType, "guardian", StringComparison.Ordinal) =>
                string.Equals(resourceKey, "gacha_attempts", StringComparison.Ordinal),
            _ => false
        };
    }

    private static bool IsPlayerSoul(JsonObject owner) =>
        TryReadExact(owner["actorType"], out var actorType) &&
        string.Equals(actorType, "player_soul", StringComparison.Ordinal);

    private static bool TryReadExact(JsonNode? node, out string value)
    {
        value = string.Empty;
        if (node is not JsonValue scalar ||
            !scalar.TryGetValue<string>(out var text) ||
            !ResourceMaterializationContract.IsExactIdentifier(text))
        {
            return false;
        }
        value = text;
        return true;
    }

    private static bool TryReadDecimal(JsonNode? node, out decimal value)
    {
        value = default;
        if (node == null)
            return false;
        try
        {
            using var document = JsonDocument.Parse(node.ToJsonString());
            return document.RootElement.ValueKind == JsonValueKind.Number &&
                   document.RootElement.TryGetDecimal(out value);
        }
        catch (JsonException)
        {
            return false;
        }
    }

    private static bool TryReadInt(JsonNode? node, out int value)
    {
        value = default;
        return node is JsonValue scalar && scalar.TryGetValue<int>(out value);
    }

    private static ResourceOwnerCompositionResult Invalid(
        IReadOnlyList<ValidationIssue> issues) =>
        new(
            null,
            new ReadOnlyDictionary<string, JsonObject>(
                new Dictionary<string, JsonObject>(StringComparer.Ordinal)),
            Array.Empty<AcceptedMechanicsOwnerTransition>(),
            null,
            Array.Empty<ResourceOwnerCapacityDraft>(),
            Array.Empty<ResourceOwnerKey>(),
            issues.ToArray());

    private static void Add(
        List<ValidationIssue> issues,
        string path,
        string code,
        string expected,
        string actual) =>
        issues.Add(new ValidationIssue(
            path,
            IssueSeverity.Error,
            "Afterlife resource owner authority is invalid.",
            code,
            "ResourceMaterialization",
            expected,
            actual));

    private static string Describe(JsonNode? node) =>
        node?.ToJsonString() ?? "missing/null";

    private readonly record struct ActorOwner(
        string ActorId,
        JsonObject Profile,
        IReadOnlyList<ActorRealmBinding> Bindings,
        string Path);

    private readonly record struct ActorRealmBinding(
        string Realm,
        string ResourceOwnerId,
        ResourceOwnerLifecycle Lifecycle,
        string Path);

    private readonly record struct ConflictOwner(
        JsonObject Conflict,
        string ConflictId,
        string Realm,
        string Path);

    private readonly record struct ShiningReturnScope(
        string ReturnCycleId,
        int RadianceTier,
        string? ResourceOwnerId,
        string? BoundReturnCycleId,
        string Path);
}
