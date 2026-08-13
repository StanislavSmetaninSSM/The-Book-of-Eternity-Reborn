using System.Text.Json;
using System.Text.Json.Nodes;

namespace BookOfEternityClient.Services;

internal static partial class MortalLocationAcceptedTurnPlanner
{
    private static readonly HashSet<string> ExistingCurrentFields = new(StringComparer.Ordinal)
    {
        "locationId",
        "lastEventsDescription",
        "currentWeather",
        "currentInteractions",
        "currentChronology",
        "locationStorages"
    };

    private static readonly HashSet<string> ExistingProtectedResendFields = new(StringComparer.Ordinal)
    {
        "initialId",
        "parentInitialId",
        "materialization",
        "materializationId",
        "materializationReceipt",
        "receiptId",
        "seal",
        "locationIdentityIndex",
        "linkIdentityIndex",
        "requestId",
        "sessionId",
        "reservationId",
        "transitionId",
        "transitions"
    };

    private static readonly string[] CurrentOperationalFields =
    {
        "currentWeather",
        "currentInteractions",
        "currentChronology"
    };

    private static readonly HashSet<string> MutableLocationUpdateFields = new(StringComparer.Ordinal)
    {
        "name",
        "displayName",
        "purpose",
        "description",
        "image_prompt",
        "internalDifficulty",
        "externalDifficulty",
        "lastEventsDescription",
        "factionControl",
        "actorBindings",
        "loreBindings",
        "customStates"
    };

    private static readonly HashSet<string> LocationDiscoveryTransitionFields = new(StringComparer.Ordinal)
    {
        "locationId",
        "fromTier",
        "toTier",
        "toAudience",
        "rumorSummary",
        "reason"
    };

    private static readonly HashSet<string> LinkUpdateFields = new(StringComparer.Ordinal)
    {
        "linkId",
        "name",
        "description",
        "directionLabel",
        "access",
        "discovery"
    };

    private static readonly HashSet<string> LinkDiscoveryTransitionFields = new(StringComparer.Ordinal)
    {
        "fromTier",
        "toTier",
        "toAudience",
        "rumorSummary"
    };

    private static readonly HashSet<string> LinkRemovalFields = new(StringComparer.Ordinal)
    {
        "linkId",
        "sourceLocationId",
        "targetLocationId",
        "reason"
    };

    private static readonly HashSet<string> LinkAccessFields = new(StringComparer.Ordinal)
    {
        "state",
        "reason",
        "requirements"
    };

    private static readonly IReadOnlySet<string> DiscoveryTiers =
        new HashSet<string>(StringComparer.Ordinal) { "hidden", "rumored", "discovered", "visited" };

    private static readonly IReadOnlySet<string> ForwardDiscoveryEdges =
        new HashSet<string>(StringComparer.Ordinal)
        {
            "hidden\u001frumored",
            "hidden\u001fdiscovered",
            "hidden\u001fvisited",
            "rumored\u001fdiscovered",
            "rumored\u001fvisited",
            "discovered\u001fvisited"
        };

    internal static MortalLocationAcceptedTurnPlanningResult Build(
        MortalLocationAcceptedTurnInput input,
        MortalLocationIdentityFactory? identityFactory = null)
    {
        ArgumentNullException.ThrowIfNull(input);
        identityFactory ??= new MortalLocationIdentityFactory();
        var companionAuthority =
            input.CompanionAuthority ?? MortalLocationCompanionAuthority.Empty;
        var issues = new List<ValidationIssue>();
        var preTurnWorldMap = input.PreTurnWorldMap.DeepClone().AsObject();
        var preTurnIndex = MortalLocationIdentityState.Parse(input.PreTurnIdentityIndex);
        issues.AddRange(preTurnIndex.Issues);
        var preTurnStorageContents = MortalLocationStorageContentsState.Parse(
            input.PreTurnStorageContents);
        issues.AddRange(preTurnStorageContents.Issues);
        MortalBootstrapLocationReservationSet? bootstrapReservations = null;
        if (input.BootstrapScaffold != null &&
            !MortalBootstrapLocationScaffold.TryReadRequest(
                input.BootstrapScaffold,
                out bootstrapReservations,
                out var scaffoldError))
        {
            issues.Add(Issue(
                MortalBootstrapLocationScaffold.StatePath + ".locationMaterializationRequest",
                "mortal_bootstrap_location_scaffold_invalid",
                "The client-owned Mortal bootstrap location request is malformed.",
                "exact current bootstrap location request",
                scaffoldError));
        }

        if (!TryReadCanonicalMap(preTurnWorldMap, out var preTurnLocations, out var preTurnLinks))
        {
            issues.Add(Issue(
                MortalLocationMaterializationContract.WorldMapPath,
                "mortal_location_materialization_invalid_world_map",
                "The pre-turn world map must use the exact canonical root.",
                "schemaVersion=1, realm=mortal_world, locations[], links[]",
                preTurnWorldMap.ToJsonString()));
            return Failed(issues);
        }

        ValidatePreTurnCanonicalState(preTurnWorldMap, preTurnLocations, preTurnLinks, preTurnIndex, issues);
        var preTurnLocationsById = BuildExactObjectIndex(preTurnLocations, "locationId", issues);
        var preTurnLinksById = BuildExactObjectIndex(preTurnLinks, "linkId", issues);
        var locationCandidates = new List<LocationCandidate>();
        ExistingSelection? existingSelection = null;

        if (input.RawCurrentLocationData != null)
        {
            var current = Unwrap(input.RawCurrentLocationData, "currentLocationData");
            if (current.TryGetPropertyValue("locationId", out var identityNode) && identityNode == null)
            {
                locationCandidates.Add(new LocationCandidate(
                    current.DeepClone().AsObject(),
                    "currentLocationData",
                    "current_scene_creation",
                    SelectCurrent: true));
            }
            else if (TryGetExactIdentity(current, "locationId", out var existingId))
            {
                existingSelection = ValidateExistingSelection(
                    current,
                    existingId,
                    input.PreTurnCurrentLocation,
                    preTurnLocationsById,
                    preTurnLinks,
                    issues);
            }
            else
            {
                issues.Add(Issue(
                    "currentLocationData.locationId",
                    "mortal_location_materialization_identity_conflict",
                    "Current location route requires explicit null creation identity or one exact existing locationId.",
                    "null or exact active locationId",
                    current["locationId"]?.ToJsonString() ?? "missing"));
            }
        }

        var updates = input.RawWorldMapUpdates == null
            ? null
            : Unwrap(input.RawWorldMapUpdates, "worldMapUpdates");
        ValidateWorldMapCommandCatalog(updates, issues);
        if (updates != null)
        {
            if (updates["newLocations"] is JsonArray newLocations)
            {
                for (var index = 0; index < newLocations.Count; index++)
                {
                    if (newLocations[index] is not JsonObject location)
                    {
                        issues.Add(Issue(
                            $"worldMapUpdates.newLocations[{index}]",
                            "mortal_location_materialization_invalid_root",
                            "Each newLocations member must be one complete object.",
                            "object",
                            newLocations[index]?.ToJsonString() ?? "null"));
                        continue;
                    }
                    locationCandidates.Add(new LocationCandidate(
                        location.DeepClone().AsObject(),
                        $"worldMapUpdates.newLocations[{index}]",
                        "world_map_creation",
                        SelectCurrent: false));
                }
            }
            else if (updates.ContainsKey("newLocations"))
            {
                issues.Add(Issue(
                    "worldMapUpdates.newLocations",
                    "mortal_location_materialization_invalid_root",
                    "newLocations must be an array.",
                    "array",
                    updates["newLocations"]?.ToJsonString() ?? "null"));
            }
        }

        ValidateLocationCandidates(
            locationCandidates,
            preTurnLocations,
            preTurnIndex,
            input.Turn,
            bootstrapReservations,
            issues);
        var actorBindingAuthority = BuildActorBindingAuthority(input.RawNpcCore);
        var factionControlAuthority = BuildFactionControlAuthority(input.RawFactionCore);
        ValidateLocationCompanionReferences(
            locationCandidates,
            companionAuthority,
            actorBindingAuthority,
            factionControlAuthority,
            issues);
        ValidateParentGraph(
            locationCandidates,
            preTurnLocationsById,
            preTurnIndex,
            issues);

        var linkCandidates = ReadLinkCandidates(
            updates,
            preTurnIndex,
            input.Turn,
            bootstrapReservations,
            issues);
        ValidateRawLinkEndpoints(
            linkCandidates,
            preTurnLocationsById,
            locationCandidates,
            preTurnIndex,
            issues);
        ValidateCreationTopologyDisposition(locationCandidates, linkCandidates, issues);

        var locationUpdates = ReadLocationUpdates(updates, preTurnLocationsById, issues);
        ValidateLocationUpdateCompanionReferences(
            locationUpdates,
            companionAuthority,
            actorBindingAuthority,
            factionControlAuthority,
            issues);
        var discoveryTransitions = ReadLocationDiscoveryTransitions(updates, preTurnLocationsById, issues);
        var linkUpdates = ReadLinkUpdates(updates, preTurnLinksById, issues);
        var linkRemovals = ReadLinkRemovals(updates, preTurnLinksById, issues);
        ValidateLifecycleOperationConflicts(linkUpdates, linkRemovals, issues);
        var governedCommands = ReadGovernedLocationCommands(
            updates,
            input.PreTurnCurrentLocation,
            preTurnStorageContents.Entries,
            preTurnLocationsById,
            locationCandidates,
            issues);

        BootstrapPlanningContext? bootstrap = null;
        if (bootstrapReservations != null)
        {
            bootstrap = ValidateBootstrapReservations(
                bootstrapReservations,
                locationCandidates,
                linkCandidates,
                input.Turn,
                issues);
        }

        if (issues.Count != 0)
            return Failed(issues);

        AssignThreatIds(
            governedCommands.ThreatAdds,
            preTurnLocations,
            locationCandidates,
            identityFactory,
            issues);
        if (issues.Count != 0)
            return Failed(issues);

        var locationIdsByInitialId = new Dictionary<string, string>(StringComparer.Ordinal);
        var locationReceiptIdsByInitialId = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var candidate in locationCandidates)
        {
            var initialId = ReadExactString(candidate.Raw, "initialId")!;
            var locationId = bootstrap?.ReservedLocationIdsByInitialId.TryGetValue(initialId, out var reservedId) == true
                ? reservedId
                : identityFactory.CreateLocationId();
            if (preTurnLocationsById.ContainsKey(locationId) ||
                locationIdsByInitialId.Values.Contains(locationId, StringComparer.Ordinal))
            {
                issues.Add(Issue(
                    candidate.Context + ".initialId",
                    "mortal_bootstrap_location_reservation_conflict",
                    "A reserved bootstrap permanent location identity is already active or duplicated.",
                    "unused client reservation",
                    locationId));
                continue;
            }
            locationIdsByInitialId.Add(initialId, locationId);
            locationReceiptIdsByInitialId.Add(initialId, identityFactory.CreateLocationReceiptId());
        }

        if (issues.Count != 0)
            return Failed(issues);

        var finalWorldMap = preTurnWorldMap.DeepClone().AsObject();
        var finalLocations = finalWorldMap["locations"]!.AsArray();
        var finalLinks = finalWorldMap["links"]!.AsArray();
        var finalIdentityIndex = preTurnIndex.ToJson();
        var finalLocationEntries = finalIdentityIndex["locationEntries"]!.AsArray();
        var finalLinkEntries = finalIdentityIndex["linkEntries"]!.AsArray();
        var canonicalByInitialId = new Dictionary<string, JsonObject>(StringComparer.Ordinal);
        JsonObject? finalCurrent = input.PreTurnCurrentLocation?.DeepClone().AsObject();

        foreach (var candidate in locationCandidates)
        {
            var initialId = ReadExactString(candidate.Raw, "initialId")!;
            var canonical = CreateCanonicalLocation(
                candidate,
                locationIdsByInitialId[initialId],
                locationReceiptIdsByInitialId[initialId],
                locationIdsByInitialId);
            canonicalByInitialId.Add(initialId, canonical);
            finalLocations.Add(canonical.DeepClone());
            finalLocationEntries.Add(CreateLocationIndexEntry(canonical));
            if (candidate.SelectCurrent)
                finalCurrent = CreateCurrentProjection(canonical, candidate.Raw);
        }

        if (existingSelection != null)
        {
            finalCurrent = CreateCurrentProjection(
                existingSelection.CanonicalLocation,
                CreateExistingProjectionSource(
                    input.PreTurnCurrentLocation,
                    existingSelection));
        }

        var linkIdsByInitialId = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var candidate in linkCandidates)
        {
            var initialId = ReadExactString(candidate.Raw, "initialId")!;
            var linkId = bootstrap?.ReservedLinkIdsByInitialId.TryGetValue(initialId, out var reservedId) == true
                ? reservedId
                : identityFactory.CreateLinkId();
            var receiptId = identityFactory.CreateLinkReceiptId();
            linkIdsByInitialId.Add(initialId, linkId);
            var canonical = CreateCanonicalLink(
                candidate.Raw,
                linkId,
                receiptId,
                locationIdsByInitialId);
            finalLinks.Add(canonical.DeepClone());
            finalLinkEntries.Add(CreateLinkIndexEntry(canonical));
        }

        ApplyLocationUpdates(
            finalLocations,
            finalLocationEntries,
            locationUpdates,
            input.Turn,
            identityFactory);
        ApplyLocationDiscoveryTransitions(
            finalLocations,
            finalLocationEntries,
            discoveryTransitions,
            input.Turn,
            identityFactory);
        ApplyLinkUpdates(
            finalLinks,
            finalLinkEntries,
            linkUpdates,
            input.Turn,
            identityFactory);
        ApplyLinkRemovals(
            finalLinks,
            finalLinkEntries,
            linkRemovals,
            input.Turn,
            identityFactory);
        ApplyGovernedLocationCommands(
            finalLocations,
            finalLocationEntries,
            governedCommands,
            locationIdsByInitialId,
            input.Turn,
            identityFactory);

        JsonObject? currentProjectionSource = finalCurrent?.DeepClone().AsObject();
        if (existingSelection != null)
        {
            ApplyExistingMovement(
                finalLocations,
                finalLocationEntries,
                existingSelection,
                input.Turn,
                identityFactory);
            currentProjectionSource = CreateExistingProjectionSource(
                input.PreTurnCurrentLocation,
                existingSelection);
        }

        if (finalCurrent != null || existingSelection != null)
        {
            var currentId = existingSelection == null
                ? ReadExactString(finalCurrent, "locationId")
                : ReadExactString(existingSelection.RawSelection, "locationId");
            var canonicalCurrent = currentId == null
                ? null
                : FindExactObject(finalLocations, "locationId", currentId);
            if (canonicalCurrent != null)
            {
                finalCurrent = CreateCurrentProjection(
                    canonicalCurrent,
                    currentProjectionSource ?? new JsonObject());
                RebuildDerivedCurrentTopology(finalCurrent, finalLocations, finalLinks);
            }
        }

        var finalStorageContents = ReconcileLocationStorageContents(
            finalWorldMap,
            input.PreTurnCurrentLocation,
            input.PreTurnStorageContents,
            ReadExactString(finalCurrent, "locationId"),
            finalCurrent,
            issues);
        if (issues.Count != 0)
            return Failed(issues);

        ValidateComposedState(finalWorldMap, finalIdentityIndex, finalCurrent, issues);
        if (issues.Count != 0)
            return Failed(issues);

        var storageCoordinates = BuildAcceptedStorageCoordinates(
            finalCurrent,
            locationIdsByInitialId);
        var governedRewrites = BuildNpcLocationRewrites(
            input.RawNpcCore,
            locationIdsByInitialId,
            issues);
        if (issues.Count != 0)
            return Failed(issues);
        var touchedPaths = new List<string>();
        var hasLifecycleMutation = locationUpdates.Count > 0 || discoveryTransitions.Count > 0 ||
                                   linkUpdates.Count > 0 || linkRemovals.Count > 0 ||
                                   governedCommands.HasCommands ||
                                   existingSelection != null;
        if (locationCandidates.Count > 0 || linkCandidates.Count > 0 || hasLifecycleMutation)
        {
            touchedPaths.Add(MortalLocationMaterializationContract.WorldMapPath);
            touchedPaths.Add(MortalLocationIdentityState.StatePath);
        }
        if (input.RawCurrentLocationData != null ||
            finalCurrent != null && (locationUpdates.Count > 0 || discoveryTransitions.Count > 0 ||
                                     linkUpdates.Count > 0 || linkRemovals.Count > 0 ||
                                     governedCommands.HasCommands))
            touchedPaths.Add(MortalLocationMaterializationContract.CurrentLocationPath);
        if (!JsonNode.DeepEquals(preTurnStorageContents.Root, finalStorageContents))
            touchedPaths.Add(MortalLocationStorageContentsState.StatePath);
        touchedPaths.AddRange(governedRewrites.Select(static rewrite => rewrite.CarrierPath));

        JsonObject? finalBootstrapScaffold = null;
        if (bootstrap != null)
        {
            finalBootstrapScaffold = MortalBootstrapLocationScaffold.CreateSettledRequest(
                input.BootstrapScaffold!,
                bootstrap.Branch,
                input.Turn,
                locationIdsByInitialId[bootstrap.Reservations.Start.InitialId],
                bootstrap.Branch == MortalBootstrapLocationScaffold.MaterializedNeighborBranch
                    ? locationIdsByInitialId[bootstrap.Reservations.Neighbor.InitialId]
                    : null,
                bootstrap.Branch == MortalBootstrapLocationScaffold.MaterializedNeighborBranch
                    ? linkIdsByInitialId[bootstrap.Reservations.Link.InitialId]
                    : null);
            touchedPaths.Add(MortalBootstrapLocationScaffold.StatePath);
        }

        var plan = new MortalLocationAcceptedTurnPlan(
            finalWorldMap,
            finalCurrent,
            finalIdentityIndex,
            finalStorageContents,
            locationIdsByInitialId,
            linkIdsByInitialId,
            storageCoordinates,
            governedRewrites,
            touchedPaths.Distinct(StringComparer.Ordinal).ToArray(),
            Array.Empty<MortalLocationRepairContext>(),
            finalBootstrapScaffold);
        return new MortalLocationAcceptedTurnPlanningResult(plan, Array.Empty<ValidationIssue>());
    }

    internal static IReadOnlyList<ValidationIssue> ValidateCanonicalActorBindings(
        JsonObject location,
        string context,
        JsonObject? npcRoot)
    {
        var issues = new List<ValidationIssue>();
        if (location["actorBindings"] is not JsonArray bindings)
            return issues;

        var locationId = ReadExactString(location, "locationId");
        var authority = BuildActorBindingAuthority(npcRoot);
        for (var index = 0; index < bindings.Count; index++)
        {
            if (bindings[index] is not JsonObject binding ||
                !TryGetExactIdentity(binding, "actorId", out var actorId))
            {
                continue;
            }

            var resolution = authority.ResolveEffective(actorId, out var placement);
            if (resolution != MortalLocationReferenceResolution.Exact || placement == null)
            {
                var code = resolution switch
                {
                    MortalLocationReferenceResolution.Confusable =>
                        "mortal_location_actor_binding_target_confusable",
                    MortalLocationReferenceResolution.Ambiguous =>
                        "mortal_location_actor_binding_target_ambiguous",
                    _ => "mortal_location_actor_binding_target_unknown"
                };
                issues.Add(Issue(
                    $"{context}.actorBindings[{index}].actorId",
                    code,
                    "Canonical actor binding must resolve to one exact accepted Mortal actor.",
                    "one exact effective actor identity",
                    actorId));
                continue;
            }

            if (locationId == null ||
                !string.Equals(
                    placement.PermanentLocationId,
                    locationId,
                    StringComparison.Ordinal) ||
                placement.InitialLocationId != null)
            {
                issues.Add(Issue(
                    $"{context}.actorBindings[{index}]",
                    "mortal_location_actor_binding_physical_conflict",
                    "Canonical actor binding must agree with the actor's exact permanent location.",
                    "currentLocationId=" + (locationId ?? "missing"),
                    placement.Describe()));
            }
        }

        return issues;
    }

    internal static IReadOnlyList<ValidationIssue> ValidateCanonicalFactionControls(
        JsonObject location,
        string context,
        JsonObject? factionRoot)
    {
        var issues = new List<ValidationIssue>();
        if (location["factionControl"] is not JsonArray controls)
            return issues;

        var authority = BuildFactionControlAuthority(factionRoot);
        for (var index = 0; index < controls.Count; index++)
        {
            if (controls[index] is not JsonObject control ||
                !TryGetExactIdentity(control, "factionId", out var factionId))
            {
                continue;
            }

            var resolution = authority.ResolveEffective(factionId);
            if (resolution == MortalLocationReferenceResolution.Exact)
                continue;

            var code = resolution switch
            {
                MortalLocationReferenceResolution.Confusable =>
                    "mortal_location_faction_control_target_confusable",
                MortalLocationReferenceResolution.Ambiguous =>
                    "mortal_location_faction_control_target_ambiguous",
                _ => "mortal_location_faction_control_target_unknown"
            };
            issues.Add(Issue(
                $"{context}.factionControl[{index}].factionId",
                code,
                "Canonical faction control must resolve to one exact accepted Mortal faction.",
                "one exact effective faction identity",
                factionId));
        }

        return issues;
    }

    private static IReadOnlyList<MortalLocationGovernedRewrite> BuildNpcLocationRewrites(
        JsonObject? npcRoot,
        IReadOnlyDictionary<string, string> locationIdsByInitialId,
        List<ValidationIssue> issues)
    {
        if (npcRoot == null || locationIdsByInitialId.Count == 0)
        {
            return Array.Empty<MortalLocationGovernedRewrite>();
        }

        var rewrites = new List<MortalLocationGovernedRewrite>();
        if (npcRoot[NpcCoreChangesContract.PropertyName] is JsonArray commands)
        {
            for (var index = 0; index < commands.Count; index++)
            {
                if (commands[index] is not JsonObject command ||
                    command["location"] is not JsonObject location ||
                    location["currentLocationId"] != null)
                {
                    continue;
                }

                var initialId = ReadExactString(location, "initialLocationId");
                if (initialId == null ||
                    !locationIdsByInitialId.TryGetValue(initialId, out var permanentId))
                {
                    continue;
                }

                var npcId = ReadExactString(command, "NPCId");
                if (npcId == null)
                {
                    issues.Add(Issue(
                        $"{NpcCoreChangesContract.PropertyName}[{index}].NPCId",
                        "mortal_location_companion_rewrite_identity_invalid",
                        "A governed NPC location rewrite requires one exact permanent NPCId.",
                        "non-empty exact NPCId",
                        command["NPCId"]?.ToJsonString() ?? "missing"));
                    continue;
                }

                rewrites.Add(new MortalLocationGovernedRewrite(
                    NpcCoreChangesContract.NpcCorePath,
                    $"{NpcCoreChangesContract.PropertyName}[{index}]",
                    "NPCId",
                    npcId,
                    "location.initialLocationId",
                    "location.currentLocationId",
                    initialId,
                    permanentId));
            }
        }

        foreach (var section in GuardianPolicyContracts.NpcCoreCanonicalNpcObjectSections)
        {
            if (npcRoot[section] is not JsonArray actors)
                continue;
            for (var index = 0; index < actors.Count; index++)
            {
                if (actors[index] is not JsonObject actor ||
                    TryReadExactPermanentActorIdentity(actor, out _) ||
                    ReadExactString(actor, "initialId") is not { } actorInitialId ||
                    actor["currentLocationId"] != null ||
                    ReadExactString(actor, "initialLocationId") is not { } locationInitialId ||
                    !locationIdsByInitialId.TryGetValue(locationInitialId, out var permanentId))
                {
                    continue;
                }

                rewrites.Add(new MortalLocationGovernedRewrite(
                    NpcCoreChangesContract.NpcCorePath,
                    $"{section}[{index}]",
                    "initialId",
                    actorInitialId,
                    "initialLocationId",
                    "currentLocationId",
                    locationInitialId,
                    permanentId));
            }
        }

        return rewrites;
    }

    private static void ValidateLocationCompanionReferences(
        IReadOnlyList<LocationCandidate> candidates,
        MortalLocationCompanionAuthority authority,
        ActorBindingAuthority actorAuthority,
        FactionControlAuthority factionAuthority,
        List<ValidationIssue> issues)
    {
        foreach (var candidate in candidates)
        {
            issues.AddRange(authority.ValidateLoreBindings(candidate.Raw, candidate.Context));
            ValidateAndRewriteActorBindings(candidate, actorAuthority, issues);
            ValidateAndRewriteFactionControls(candidate, factionAuthority, issues);
        }
    }

    private static void ValidateAndRewriteFactionControls(
        LocationCandidate candidate,
        FactionControlAuthority authority,
        List<ValidationIssue> issues)
    {
        if (candidate.Raw["factionControl"] is not JsonArray controls)
            return;

        for (var index = 0; index < controls.Count; index++)
        {
            if (controls[index] is not JsonObject control)
                continue;

            var usesTemporaryIdentity = TryGetExactIdentity(
                control,
                "initialFactionId",
                out var identity);
            var identityField = usesTemporaryIdentity
                ? "initialFactionId"
                : "factionId";
            if (!usesTemporaryIdentity &&
                !TryGetExactIdentity(control, identityField, out identity))
            {
                continue;
            }

            var resolution = authority.Resolve(identity, usesTemporaryIdentity);
            if (resolution != MortalLocationReferenceResolution.Exact)
            {
                var code = resolution switch
                {
                    MortalLocationReferenceResolution.Confusable =>
                        "mortal_location_faction_control_target_confusable",
                    MortalLocationReferenceResolution.Ambiguous =>
                        "mortal_location_faction_control_target_ambiguous",
                    _ => "mortal_location_faction_control_target_unknown"
                };
                issues.Add(Issue(
                    $"{candidate.Context}.factionControl[{index}].{identityField}",
                    code,
                    "Faction control must resolve to one exact accepted Mortal faction.",
                    usesTemporaryIdentity
                        ? "one exact same-turn initial faction identity"
                        : "one exact permanent faction identity",
                    identity));
                continue;
            }

            if (usesTemporaryIdentity)
            {
                control["factionId"] = identity;
                control.Remove("initialFactionId");
            }
        }
    }

    private static void ValidateLocationUpdateCompanionReferences(
        IReadOnlyList<LocationUpdate> updates,
        MortalLocationCompanionAuthority companionAuthority,
        ActorBindingAuthority actorAuthority,
        FactionControlAuthority factionAuthority,
        List<ValidationIssue> issues)
    {
        foreach (var update in updates)
        {
            issues.AddRange(companionAuthority.ValidateLoreBindings(
                update.Raw,
                update.Context));
            ValidateAndRewriteLocationUpdateActorBindings(
                update,
                actorAuthority,
                issues);
            ValidateAndRewriteLocationUpdateFactionControls(
                update,
                factionAuthority,
                issues);
        }
    }

    private static void ValidateAndRewriteLocationUpdateActorBindings(
        LocationUpdate update,
        ActorBindingAuthority authority,
        List<ValidationIssue> issues)
    {
        if (update.Raw["actorBindings"] is not JsonArray bindings)
            return;

        for (var index = 0; index < bindings.Count; index++)
        {
            if (bindings[index] is not JsonObject binding)
                continue;

            var usesTemporaryIdentity = TryGetExactIdentity(
                binding,
                "initialActorId",
                out var identity);
            var identityField = usesTemporaryIdentity ? "initialActorId" : "actorId";
            if (!usesTemporaryIdentity &&
                !TryGetExactIdentity(binding, identityField, out identity))
            {
                continue;
            }

            var resolution = authority.Resolve(
                identity,
                usesTemporaryIdentity,
                out var placement);
            if (resolution != MortalLocationReferenceResolution.Exact || placement == null)
            {
                var code = resolution switch
                {
                    MortalLocationReferenceResolution.Confusable =>
                        "mortal_location_actor_binding_target_confusable",
                    MortalLocationReferenceResolution.Ambiguous =>
                        "mortal_location_actor_binding_target_ambiguous",
                    _ => "mortal_location_actor_binding_target_unknown"
                };
                issues.Add(Issue(
                    $"{update.Context}.actorBindings[{index}].{identityField}",
                    code,
                    "Updated actor binding must resolve to one exact accepted Mortal actor.",
                    usesTemporaryIdentity
                        ? "one exact same-turn initial actor identity"
                        : "one exact permanent actor identity",
                    identity));
                continue;
            }

            if (!string.Equals(
                    placement.PermanentLocationId,
                    update.LocationId,
                    StringComparison.Ordinal) ||
                placement.InitialLocationId != null)
            {
                issues.Add(Issue(
                    $"{update.Context}.actorBindings[{index}]",
                    "mortal_location_actor_binding_physical_conflict",
                    "An updated physical actor binding must agree with the actor's exact accepted location.",
                    $"currentLocationId={update.LocationId}",
                    placement.Describe()));
                continue;
            }

            if (usesTemporaryIdentity)
            {
                binding["actorId"] = identity;
                binding.Remove("initialActorId");
            }
        }
    }

    private static void ValidateAndRewriteLocationUpdateFactionControls(
        LocationUpdate update,
        FactionControlAuthority authority,
        List<ValidationIssue> issues)
    {
        if (update.Raw["factionControl"] is not JsonArray controls)
            return;

        for (var index = 0; index < controls.Count; index++)
        {
            if (controls[index] is not JsonObject control)
                continue;

            var usesTemporaryIdentity = TryGetExactIdentity(
                control,
                "initialFactionId",
                out var identity);
            var identityField = usesTemporaryIdentity
                ? "initialFactionId"
                : "factionId";
            if (!usesTemporaryIdentity &&
                !TryGetExactIdentity(control, identityField, out identity))
            {
                continue;
            }

            var resolution = authority.Resolve(identity, usesTemporaryIdentity);
            if (resolution != MortalLocationReferenceResolution.Exact)
            {
                var code = resolution switch
                {
                    MortalLocationReferenceResolution.Confusable =>
                        "mortal_location_faction_control_target_confusable",
                    MortalLocationReferenceResolution.Ambiguous =>
                        "mortal_location_faction_control_target_ambiguous",
                    _ => "mortal_location_faction_control_target_unknown"
                };
                issues.Add(Issue(
                    $"{update.Context}.factionControl[{index}].{identityField}",
                    code,
                    "Updated faction control must resolve to one exact accepted Mortal faction.",
                    usesTemporaryIdentity
                        ? "one exact same-turn initial faction identity"
                        : "one exact permanent faction identity",
                    identity));
                continue;
            }

            if (usesTemporaryIdentity)
            {
                control["factionId"] = identity;
                control.Remove("initialFactionId");
            }
        }
    }

    private static void ValidateAndRewriteActorBindings(
        LocationCandidate candidate,
        ActorBindingAuthority authority,
        List<ValidationIssue> issues)
    {
        if (candidate.Raw["actorBindings"] is not JsonArray bindings)
            return;

        var locationInitialId = ReadExactString(candidate.Raw, "initialId")!;
        for (var index = 0; index < bindings.Count; index++)
        {
            if (bindings[index] is not JsonObject binding)
                continue;

            var usesTemporaryIdentity = TryGetExactIdentity(
                binding,
                "initialActorId",
                out var identity);
            var identityField = usesTemporaryIdentity ? "initialActorId" : "actorId";
            if (!usesTemporaryIdentity &&
                !TryGetExactIdentity(binding, identityField, out identity))
            {
                continue;
            }

            var resolution = authority.Resolve(
                identity,
                usesTemporaryIdentity,
                out var placement);
            if (resolution != MortalLocationReferenceResolution.Exact || placement == null)
            {
                var code = resolution switch
                {
                    MortalLocationReferenceResolution.Confusable =>
                        "mortal_location_actor_binding_target_confusable",
                    MortalLocationReferenceResolution.Ambiguous =>
                        "mortal_location_actor_binding_target_ambiguous",
                    _ => "mortal_location_actor_binding_target_unknown"
                };
                issues.Add(Issue(
                    $"{candidate.Context}.actorBindings[{index}].{identityField}",
                    code,
                    "Actor binding must resolve to one exact accepted Mortal actor.",
                    usesTemporaryIdentity
                        ? "one exact same-turn initial actor identity"
                        : "one exact permanent actor identity",
                    identity));
                continue;
            }

            if (!string.Equals(
                    placement.InitialLocationId,
                    locationInitialId,
                    StringComparison.Ordinal) ||
                placement.PermanentLocationId != null)
            {
                issues.Add(Issue(
                    $"{candidate.Context}.actorBindings[{index}]",
                    "mortal_location_actor_binding_physical_conflict",
                    "A physical actor binding must agree with the actor's exact accepted location.",
                    $"initialLocationId={locationInitialId}",
                    placement.Describe()));
                continue;
            }

            if (usesTemporaryIdentity)
            {
                binding["actorId"] = identity;
                binding.Remove("initialActorId");
            }
        }
    }

    private static ActorBindingAuthority BuildActorBindingAuthority(JsonObject? npcRoot)
    {
        var authority = new ActorBindingAuthority();
        if (npcRoot == null)
            return authority;

        var commandPlacements = new Dictionary<string, List<ActorPlacement>>(StringComparer.Ordinal);
        if (npcRoot[NpcCoreChangesContract.PropertyName] is JsonArray commands)
        {
            foreach (var command in commands.OfType<JsonObject>())
            {
                var npcId = ReadExactString(command, "NPCId");
                var placement = ReadActorPlacement(command["location"] as JsonObject);
                if (npcId == null || placement == null)
                    continue;
                if (!commandPlacements.TryGetValue(npcId, out var placements))
                {
                    placements = [];
                    commandPlacements.Add(npcId, placements);
                }
                placements.Add(placement);
            }
        }

        foreach (var section in GuardianPolicyContracts.NpcCoreCanonicalNpcObjectSections)
        {
            if (npcRoot[section] is not JsonArray actors)
                continue;
            foreach (var actor in actors.OfType<JsonObject>())
            {
                if (TryReadExactPermanentActorIdentity(actor, out var permanentId))
                {
                    if (commandPlacements.TryGetValue(permanentId, out var commandValues))
                    {
                        foreach (var commandPlacement in commandValues)
                            authority.AddPermanent(permanentId, commandPlacement);
                    }
                    else if (ReadActorPlacement(actor) is { } existingPlacement)
                    {
                        authority.AddPermanent(permanentId, existingPlacement);
                    }
                    continue;
                }

                var initialId = ReadExactString(actor, "initialId");
                if (initialId == null ||
                    actor[ActorMaterializationContract.PropertyName] is not JsonObject envelope ||
                    !string.Equals(
                        ReadExactString(envelope, "actorType"),
                        "mortal_npc",
                        StringComparison.Ordinal) ||
                    !string.Equals(
                        ReadExactString(envelope, "actorId"),
                        initialId,
                        StringComparison.Ordinal) ||
                    !string.Equals(
                        ReadExactString(envelope, "state"),
                        "complete",
                        StringComparison.Ordinal) ||
                    ReadActorPlacement(actor) is not { } placement)
                {
                    continue;
                }

                authority.AddTemporary(initialId, placement);
            }
        }

        return authority;
    }

    private static FactionControlAuthority BuildFactionControlAuthority(
        JsonObject? factionRoot)
    {
        var authority = new FactionControlAuthority();
        if (factionRoot == null)
            return authority;

        if (factionRoot["factions"] is JsonArray canonicalFactions)
        {
            foreach (var faction in canonicalFactions.OfType<JsonObject>())
            {
                var factionId = ReadExactString(faction, "factionId");
                if (factionId != null &&
                    !faction.ContainsKey("initialId") &&
                    HasExactMortalFactionEnvelope(faction, factionId))
                {
                    authority.AddPermanent(factionId);
                }
            }
        }

        if (factionRoot["factionDataChanges"] is JsonArray factionChanges)
        {
            foreach (var faction in factionChanges.OfType<JsonObject>())
            {
                var initialId = ReadExactString(faction, "initialId");
                if (initialId != null &&
                    faction.ContainsKey("factionId") &&
                    faction["factionId"] == null &&
                    TryReadExactBoolean(faction, "isNewFaction", out var isNewFaction) &&
                    isNewFaction &&
                    HasExactMortalFactionEnvelope(faction, initialId))
                {
                    authority.AddTemporary(initialId);
                }
            }
        }

        return authority;
    }

    private static bool HasExactMortalFactionEnvelope(
        JsonObject faction,
        string factionId) =>
        faction[FactionMaterializationContract.PropertyName] is JsonObject envelope &&
        string.Equals(
            ReadExactString(envelope, "factionType"),
            "mortal_faction",
            StringComparison.Ordinal) &&
        string.Equals(
            ReadExactString(envelope, "factionId"),
            factionId,
            StringComparison.Ordinal) &&
        string.Equals(
            ReadExactString(envelope, "state"),
            "complete",
            StringComparison.Ordinal);

    private static bool TryReadExactBoolean(
        JsonObject value,
        string propertyName,
        out bool result)
    {
        result = false;
        return value[propertyName] is JsonValue scalar &&
               scalar.TryGetValue<bool>(out result);
    }

    private static bool TryReadExactPermanentActorIdentity(
        JsonObject actor,
        out string identity)
    {
        identity = string.Empty;
        var hasNullAlias = false;
        string? resolved = null;
        foreach (var field in new[] { "NPCId", "npcId", "id" })
        {
            if (!actor.TryGetPropertyValue(field, out var node))
                continue;
            if (node == null)
            {
                hasNullAlias = true;
                continue;
            }
            if (ReadExactString(actor, field) is not { } candidate ||
                resolved != null && !string.Equals(resolved, candidate, StringComparison.Ordinal))
            {
                return false;
            }
            resolved = candidate;
        }

        if (resolved == null || hasNullAlias)
            return false;
        identity = resolved;
        return true;
    }

    private static ActorPlacement? ReadActorPlacement(JsonObject? actor)
    {
        if (actor == null)
            return null;
        var current = ReadExactString(actor, "currentLocationId");
        var initial = ReadExactString(actor, "initialLocationId");
        return (current != null) == (initial != null)
            ? null
            : new ActorPlacement(current, initial);
    }

    private static BootstrapPlanningContext? ValidateBootstrapReservations(
        MortalBootstrapLocationReservationSet reservations,
        IReadOnlyList<LocationCandidate> locations,
        IReadOnlyList<LinkCandidate> links,
        int turn,
        List<ValidationIssue> issues)
    {
        var hasBootstrapEvidence = locations.Any(candidate =>
                HasBootstrapAuthority(candidate.Raw) ||
                IsReservedLocationInitialId(
                    ReadExactString(candidate.Raw, "initialId"),
                    reservations)) ||
            links.Any(candidate =>
                HasBootstrapAuthority(candidate.Raw) ||
                string.Equals(
                    ReadExactString(candidate.Raw, "initialId"),
                    reservations.Link.InitialId,
                    StringComparison.Ordinal));

        if (reservations.State == "settled")
        {
            if (hasBootstrapEvidence)
            {
                issues.Add(Issue(
                    MortalBootstrapLocationScaffold.StatePath + ".locationMaterializationRequest.state",
                    "mortal_bootstrap_location_reservation_replay",
                    "Settled Mortal bootstrap location reservations cannot be reused.",
                    "ordinary turn authority with new exact origins",
                    "settled bootstrap reservation evidence"));
            }
            return null;
        }

        if (reservations.TurnNumber != turn)
        {
            issues.Add(Issue(
                MortalBootstrapLocationScaffold.StatePath + ".locationMaterializationRequest.turnNumber",
                "mortal_bootstrap_location_authority_mismatch",
                "Bootstrap reservation turn must match the accepted turn exactly.",
                turn.ToString(),
                reservations.TurnNumber.ToString()));
        }

        var startMatches = locations
            .Where(candidate => string.Equals(
                ReadExactString(candidate.Raw, "initialId"),
                reservations.Start.InitialId,
                StringComparison.Ordinal))
            .ToArray();
        var selectedStart = startMatches.SingleOrDefault(candidate => candidate.SelectCurrent);
        if (selectedStart == null)
        {
            issues.Add(Issue(
                "currentLocationData",
                "mortal_bootstrap_location_start_required",
                "The first Mortal result must materialize the exact reserved visited start through currentLocationData.",
                reservations.Start.InitialId,
                startMatches.Length == 0 ? "missing" : "wrong carrier"));
        }
        else
        {
            ValidateBootstrapLocationReservation(
                selectedStart,
                reservations.Start,
                reservations,
                requireVisited: true,
                issues);
        }

        foreach (var candidate in locations)
        {
            if (!HasBootstrapAuthority(candidate.Raw))
                continue;
            var initialId = ReadExactString(candidate.Raw, "initialId");
            var allowed = candidate.SelectCurrent
                ? string.Equals(initialId, reservations.Start.InitialId, StringComparison.Ordinal)
                : string.Equals(initialId, reservations.Neighbor.InitialId, StringComparison.Ordinal);
            if (!allowed)
            {
                issues.Add(Issue(
                    candidate.Context + ".initialId",
                    "mortal_bootstrap_location_reservation_mismatch",
                    "Bootstrap source authority may use only the exact reservation assigned to that ordinary route.",
                    candidate.SelectCurrent ? reservations.Start.InitialId : reservations.Neighbor.InitialId,
                    initialId ?? "missing"));
            }
        }

        foreach (var candidate in links)
        {
            if (!HasBootstrapAuthority(candidate.Raw))
                continue;
            var initialId = ReadExactString(candidate.Raw, "initialId");
            if (!string.Equals(initialId, reservations.Link.InitialId, StringComparison.Ordinal))
            {
                issues.Add(Issue(
                    candidate.Context + ".initialId",
                    "mortal_bootstrap_location_reservation_mismatch",
                    "Bootstrap link source authority may use only the exact reserved link origin.",
                    reservations.Link.InitialId,
                    initialId ?? "missing"));
            }
        }

        var neighborMatches = locations
            .Where(candidate => !candidate.SelectCurrent && string.Equals(
                ReadExactString(candidate.Raw, "initialId"),
                reservations.Neighbor.InitialId,
                StringComparison.Ordinal))
            .ToArray();
        var linkMatches = links
            .Where(candidate => string.Equals(
                ReadExactString(candidate.Raw, "initialId"),
                reservations.Link.InitialId,
                StringComparison.Ordinal))
            .ToArray();

        string branch;
        if (neighborMatches.Length == 0 && linkMatches.Length == 0)
        {
            branch = MortalBootstrapLocationScaffold.NarrativeOnlyBranch;
        }
        else if (neighborMatches.Length == 1 && linkMatches.Length == 1)
        {
            branch = MortalBootstrapLocationScaffold.MaterializedNeighborBranch;
            ValidateBootstrapLocationReservation(
                neighborMatches[0],
                reservations.Neighbor,
                reservations,
                requireVisited: false,
                issues);
            ValidateBootstrapLinkReservation(linkMatches[0], reservations, issues);
        }
        else
        {
            branch = MortalBootstrapLocationScaffold.MaterializedNeighborBranch;
            issues.Add(Issue(
                "worldMapUpdates",
                "mortal_bootstrap_location_branch_incomplete",
                "Bootstrap neighbor materialization requires exactly one reserved neighbor and exactly one reserved directed link.",
                "one neighbor plus one link, or neither",
                $"neighbors={neighborMatches.Length}; links={linkMatches.Length}"));
        }

        if (issues.Count != 0)
            return null;

        var locationIds = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            [reservations.Start.InitialId] = reservations.Start.ReservedLocationId
        };
        var linkIds = new Dictionary<string, string>(StringComparer.Ordinal);
        if (branch == MortalBootstrapLocationScaffold.MaterializedNeighborBranch)
        {
            locationIds[reservations.Neighbor.InitialId] = reservations.Neighbor.ReservedLocationId;
            linkIds[reservations.Link.InitialId] = reservations.Link.ReservedLinkId;
        }
        return new BootstrapPlanningContext(reservations, branch, locationIds, linkIds);
    }

    private static void ValidateBootstrapLocationReservation(
        LocationCandidate candidate,
        MortalBootstrapLocationReservation reservation,
        MortalBootstrapLocationReservationSet request,
        bool requireVisited,
        List<ValidationIssue> issues)
    {
        if (!string.Equals(candidate.ExpectedRoute, reservation.Route, StringComparison.Ordinal))
        {
            issues.Add(Issue(
                candidate.Context,
                "mortal_bootstrap_location_reservation_mismatch",
                "Bootstrap location reservation must use its assigned ordinary route.",
                reservation.Route,
                candidate.ExpectedRoute));
        }

        var authority = candidate.Raw["materialization"]?["sourceAuthority"] as JsonObject;
        if (!string.Equals(ReadExactString(authority, "kind"), request.AuthorityKind, StringComparison.Ordinal) ||
            !string.Equals(ReadExactString(authority, "authorityId"), request.AuthorityId, StringComparison.Ordinal))
        {
            issues.Add(Issue(
                candidate.Context + ".materialization.sourceAuthority",
                "mortal_bootstrap_location_authority_mismatch",
                "Bootstrap materialization must use the exact open scaffold authority.",
                $"{request.AuthorityKind}:{request.AuthorityId}",
                authority?.ToJsonString() ?? "missing"));
        }

        if (!CoordinateEquals(candidate.Raw, reservation))
        {
            issues.Add(Issue(
                candidate.Context + ".coordinates",
                "mortal_bootstrap_location_reservation_mismatch",
                "Bootstrap location coordinates must match the exact client reservation.",
                $"{reservation.X},{reservation.Y},{reservation.Z}",
                candidate.Raw["coordinates"]?.ToJsonString() ?? "missing"));
        }

        if (requireVisited)
        {
            var discovery = candidate.Raw["discovery"] as JsonObject;
            if (!string.Equals(ReadExactString(discovery, "tier"), "visited", StringComparison.Ordinal) ||
                !string.Equals(ReadExactString(discovery, "audience"), "player_known", StringComparison.Ordinal))
            {
                issues.Add(Issue(
                    candidate.Context + ".discovery",
                    "mortal_bootstrap_location_reservation_mismatch",
                    "Bootstrap start must be visited and player-known.",
                    "visited/player_known",
                    discovery?.ToJsonString() ?? "missing"));
            }
        }
    }

    private static void ValidateBootstrapLinkReservation(
        LinkCandidate candidate,
        MortalBootstrapLocationReservationSet request,
        List<ValidationIssue> issues)
    {
        var authority = candidate.Raw["materialization"]?["sourceAuthority"] as JsonObject;
        if (!string.Equals(ReadExactString(authority, "kind"), request.AuthorityKind, StringComparison.Ordinal) ||
            !string.Equals(ReadExactString(authority, "authorityId"), request.AuthorityId, StringComparison.Ordinal))
        {
            issues.Add(Issue(
                candidate.Context + ".materialization.sourceAuthority",
                "mortal_bootstrap_location_authority_mismatch",
                "Bootstrap link materialization must use the exact open scaffold authority.",
                $"{request.AuthorityKind}:{request.AuthorityId}",
                authority?.ToJsonString() ?? "missing"));
        }

        if (!string.Equals(
                ReadExactString(candidate.Raw, "sourceInitialId"),
                request.Link.SourceInitialId,
                StringComparison.Ordinal) ||
            !string.Equals(
                ReadExactString(candidate.Raw, "targetInitialId"),
                request.Link.TargetInitialId,
                StringComparison.Ordinal) ||
            candidate.Raw["sourceLocationId"] != null ||
            candidate.Raw["targetLocationId"] != null)
        {
            issues.Add(Issue(
                candidate.Context,
                "mortal_bootstrap_location_reservation_mismatch",
                "Bootstrap link must use the exact reserved temporary endpoints and no permanent endpoint selectors.",
                $"{request.Link.SourceInitialId}->{request.Link.TargetInitialId}",
                candidate.Raw.ToJsonString()));
        }
    }

    private static bool CoordinateEquals(
        JsonObject candidate,
        MortalBootstrapLocationReservation reservation) =>
        candidate["coordinates"] is JsonObject coordinates &&
        ReadInt(coordinates, "x") == reservation.X &&
        ReadInt(coordinates, "y") == reservation.Y &&
        ReadInt(coordinates, "z") == reservation.Z;

    private static bool HasBootstrapAuthority(JsonObject candidate) =>
        string.Equals(
            ReadExactString(
                candidate["materialization"]?["sourceAuthority"] as JsonObject,
                "kind"),
            MortalBootstrapLocationScaffold.AuthorityKind,
            StringComparison.Ordinal);

    private static bool IsReservedLocationInitialId(
        string? initialId,
        MortalBootstrapLocationReservationSet reservations) =>
        string.Equals(initialId, reservations.Start.InitialId, StringComparison.Ordinal) ||
        string.Equals(initialId, reservations.Neighbor.InitialId, StringComparison.Ordinal);

    private static void ValidatePreTurnCanonicalState(
        JsonObject worldMap,
        JsonArray locations,
        JsonArray links,
        MortalLocationIdentityState identityState,
        List<ValidationIssue> issues)
    {
        for (var index = 0; index < locations.Count; index++)
        {
            if (locations[index] is not JsonObject location)
                continue;
            using var document = JsonDocument.Parse(location.ToJsonString());
            issues.AddRange(MortalLocationMaterializationContract.ValidateCanonicalLocation(
                document.RootElement,
                $"{MortalLocationMaterializationContract.WorldMapPath}.locations[{index}]"));
        }

        for (var index = 0; index < links.Count; index++)
        {
            if (links[index] is not JsonObject link)
                continue;
            using var document = JsonDocument.Parse(link.ToJsonString());
            issues.AddRange(MortalLocationMaterializationContract.ValidateCanonicalLink(
                document.RootElement,
                $"{MortalLocationMaterializationContract.WorldMapPath}.links[{index}]"));
        }

        ValidatePreTurnTopology(locations, links, issues);
        issues.AddRange(identityState.ValidateCanonicalState(worldMap));
    }

    private static void ValidatePreTurnTopology(
        JsonArray locations,
        JsonArray links,
        List<ValidationIssue> issues)
    {
        var locationIds = new HashSet<string>(StringComparer.Ordinal);
        var locationIdentityKeys = new HashSet<string>(StringComparer.Ordinal);
        var coordinates = new HashSet<string>(StringComparer.Ordinal);
        var parents = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var location in locations.OfType<JsonObject>())
        {
            var locationId = ReadExactString(location, "locationId");
            if (locationId == null)
                continue;
            locationIds.Add(locationId);
            if (!locationIdentityKeys.Add(MortalLocationIdentityState.BuildConfusableKey(locationId)))
            {
                issues.Add(Issue(
                    MortalLocationMaterializationContract.WorldMapPath + ".locations",
                    "mortal_location_materialization_confusable_canonical_identity",
                    "Canonical location IDs must be unique under case and Unicode confusable normalization.",
                    "unique non-confusable locationId",
                    locationId));
            }
            if (TryReadCoordinateKey(location, out var coordinate) && !coordinates.Add(coordinate))
            {
                issues.Add(Issue(
                    MortalLocationMaterializationContract.WorldMapPath + ".locations",
                    "mortal_location_materialization_coordinate_collision",
                    "Active Mortal location coordinates must be unique.",
                    "unique x/y/z tuple",
                    coordinate));
            }
            var parent = ReadExactString(location, "parentLocationId");
            if (parent != null)
                parents[locationId] = parent;
        }

        foreach (var pair in parents)
        {
            if (locationIds.Contains(pair.Value))
                continue;
            issues.Add(Issue(
                MortalLocationMaterializationContract.WorldMapPath + ".locations.parentLocationId",
                "mortal_location_materialization_parent_unresolved",
                "A canonical parentLocationId must resolve to one exact active Mortal location.",
                "exact active locationId",
                pair.Value));
        }

        foreach (var start in parents.Keys)
        {
            var visited = new HashSet<string>(StringComparer.Ordinal);
            var cursor = start;
            while (parents.TryGetValue(cursor, out var parent))
            {
                if (!visited.Add(cursor) || string.Equals(parent, start, StringComparison.Ordinal))
                {
                    issues.Add(Issue(
                        MortalLocationMaterializationContract.WorldMapPath + ".locations.parentLocationId",
                        "mortal_location_materialization_parent_cycle",
                        "Mortal location parent graph must be acyclic and cannot contain self-parenting.",
                        "acyclic exact parent graph",
                        start));
                    break;
                }
                cursor = parent;
            }
        }

        var linkIdentityKeys = new HashSet<string>(StringComparer.Ordinal);
        foreach (var link in links.OfType<JsonObject>())
        {
            var linkId = ReadExactString(link, "linkId");
            if (linkId != null &&
                !linkIdentityKeys.Add(MortalLocationIdentityState.BuildConfusableKey(linkId)))
            {
                issues.Add(Issue(
                    MortalLocationMaterializationContract.WorldMapPath + ".links",
                    "mortal_location_materialization_confusable_canonical_identity",
                    "Canonical link IDs must be unique under case and Unicode confusable normalization.",
                    "unique non-confusable linkId",
                    linkId));
            }

            var source = ReadExactString(link, "sourceLocationId");
            var target = ReadExactString(link, "targetLocationId");
            if (source == null || target == null ||
                !locationIds.Contains(source) || !locationIds.Contains(target))
            {
                issues.Add(Issue(
                    MortalLocationMaterializationContract.WorldMapPath + ".links",
                    "mortal_location_link_endpoint_unresolved",
                    "Every canonical link endpoint must resolve exactly to an active Mortal location.",
                    "two exact active locationIds",
                    $"{source ?? "missing"}->{target ?? "missing"}"));
            }
            else if (string.Equals(source, target, StringComparison.Ordinal))
            {
                issues.Add(Issue(
                    MortalLocationMaterializationContract.WorldMapPath + ".links",
                    "mortal_location_link_endpoint_selector_invalid",
                    "A directed link cannot point from a location to itself.",
                    "two different exact endpoint IDs",
                    source));
            }
        }
    }

    private static void ValidateLocationCandidates(
        IReadOnlyList<LocationCandidate> candidates,
        JsonArray preTurnLocations,
        MortalLocationIdentityState identityState,
        int turn,
        MortalBootstrapLocationReservationSet? bootstrapReservations,
        List<ValidationIssue> issues)
    {
        var routeByOriginKey = new Dictionary<string, string>(StringComparer.Ordinal);
        var materializationKeys = new HashSet<string>(StringComparer.Ordinal);
        var coordinates = new HashSet<string>(StringComparer.Ordinal);
        foreach (var existing in preTurnLocations.OfType<JsonObject>())
        {
            if (TryReadCoordinateKey(existing, out var coordinateKey))
                coordinates.Add(coordinateKey);
        }

        foreach (var candidate in candidates)
        {
            using var document = JsonDocument.Parse(candidate.Raw.ToJsonString());
            issues.AddRange(MortalLocationMaterializationContract.ValidateRawLocation(
                document.RootElement,
                candidate.Context,
                candidate.ExpectedRoute));

            var initialId = ReadExactString(candidate.Raw, "initialId");
            var materializationId = ReadExactString(
                candidate.Raw["materialization"] as JsonObject,
                "materializationId");
            var sourceTurn = ReadInt(candidate.Raw["materialization"] as JsonObject, "sourceTurn");
            if (sourceTurn != turn)
            {
                issues.Add(Issue(
                    candidate.Context + ".materialization.sourceTurn",
                    "mortal_location_materialization_source_turn_mismatch",
                    "Materialization source turn must match the accepted turn exactly.",
                    turn.ToString(),
                    sourceTurn?.ToString() ?? "missing"));
            }
            ValidateAcceptedTurnSourceAuthority(
                candidate.Raw,
                candidate.Context,
                candidate.SelectCurrent,
                isLink: false,
                turn,
                bootstrapReservations,
                issues);

            if (initialId != null)
            {
                var originKey = MortalLocationIdentityState.BuildConfusableKey(initialId);
                if (routeByOriginKey.TryGetValue(originKey, out var firstRoute))
                {
                    issues.Add(Issue(
                        candidate.Context + ".initialId",
                        "mortal_location_materialization_duplicate_creation_route",
                        "One location creation origin may occur in exactly one raw carrier.",
                        firstRoute,
                        candidate.ExpectedRoute));
                }
                else
                {
                    routeByOriginKey[originKey] = candidate.ExpectedRoute;
                }
            }

            if (materializationId != null &&
                !materializationKeys.Add(MortalLocationIdentityState.BuildConfusableKey(materializationId)))
            {
                issues.Add(Issue(
                    candidate.Context + ".materialization.materializationId",
                    "mortal_location_materialization_duplicate_creation_route",
                    "Materialization identity may authorize exactly one creation.",
                    "unique materializationId",
                    materializationId));
            }

            if (identityState.ContainsHistoricalLocationOrigin(initialId, materializationId))
            {
                issues.Add(Issue(
                    candidate.Context,
                    "mortal_location_materialization_historical_replay",
                    "Active or retired location origin evidence cannot be reused.",
                    "new exact origin evidence",
                    $"initialId={initialId}; materializationId={materializationId}"));
            }

            if (TryReadCoordinateKey(candidate.Raw, out var coordinate) && !coordinates.Add(coordinate))
            {
                issues.Add(Issue(
                    candidate.Context + ".coordinates",
                    "mortal_location_materialization_coordinate_collision",
                    "Active Mortal location coordinates must be unique.",
                    "unused x/y/z tuple",
                    coordinate));
            }
        }
    }

    private static void ValidateParentGraph(
        IReadOnlyList<LocationCandidate> candidates,
        IReadOnlyDictionary<string, JsonObject> preTurnLocationsById,
        MortalLocationIdentityState identityState,
        List<ValidationIssue> issues)
    {
        var parents = new Dictionary<string, string>(StringComparer.Ordinal);
        var candidateIds = candidates
            .Select(candidate => ReadExactString(candidate.Raw, "initialId"))
            .Where(static value => value != null)
            .Cast<string>()
            .ToHashSet(StringComparer.Ordinal);
        var candidateIdentityKeys = candidateIds
            .Select(MortalLocationIdentityState.BuildConfusableKey)
            .ToHashSet(StringComparer.Ordinal);
        var activePermanentIdentityKeys = preTurnLocationsById.Keys
            .Select(MortalLocationIdentityState.BuildConfusableKey)
            .ToHashSet(StringComparer.Ordinal);
        foreach (var candidate in candidates)
        {
            var initialId = ReadExactString(candidate.Raw, "initialId");
            if (initialId == null)
                continue;

            var parentLocationId = ReadExactString(candidate.Raw, "parentLocationId");
            var parentInitialId = ReadExactString(candidate.Raw, "parentInitialId");
            if (parentLocationId != null &&
                preTurnLocationsById.ContainsKey(parentLocationId))
            {
                continue;
            }
            if (parentInitialId != null && candidateIds.Contains(parentInitialId))
            {
                if (string.Equals(initialId, parentInitialId, StringComparison.Ordinal))
                {
                    issues.Add(Issue(
                        candidate.Context + ".parentInitialId",
                        "mortal_location_materialization_parent_cycle",
                        "A location cannot name its own creation identity as parent.",
                        "a different exact same-turn initialId or null",
                        parentInitialId));
                    continue;
                }
                parents[initialId] = parentInitialId;
                continue;
            }

            var rawParentLocationId = ReadStringScalar(candidate.Raw, "parentLocationId");
            var rawParentInitialId = ReadStringScalar(candidate.Raw, "parentInitialId");
            if (rawParentLocationId == null && rawParentInitialId == null)
                continue;

            var historical =
                identityState.ContainsRetiredLocationId(rawParentLocationId) ||
                identityState.ContainsHistoricalLocationOrigin(rawParentInitialId, null);
            if (historical)
            {
                issues.Add(Issue(
                    candidate.Context + (rawParentLocationId != null
                        ? ".parentLocationId"
                        : ".parentInitialId"),
                    "mortal_location_materialization_parent_historical_replay",
                    "A parent selector cannot reuse retired permanent identity or historical creation-origin evidence.",
                    "exact active locationId or accepted same-turn initialId",
                    rawParentLocationId ?? rawParentInitialId!));
                continue;
            }

            var confusable =
                rawParentLocationId != null &&
                activePermanentIdentityKeys.Contains(
                    MortalLocationIdentityState.BuildConfusableKey(rawParentLocationId)) ||
                rawParentInitialId != null &&
                candidateIdentityKeys.Contains(
                    MortalLocationIdentityState.BuildConfusableKey(rawParentInitialId));
            if (confusable)
            {
                issues.Add(Issue(
                    candidate.Context + (rawParentLocationId != null
                        ? ".parentLocationId"
                        : ".parentInitialId"),
                    "mortal_location_materialization_parent_confusable",
                    "A parent selector must match exact case-sensitive identity authority and cannot use a Unicode or whitespace alias.",
                    "exact active locationId or accepted same-turn initialId",
                    rawParentLocationId ?? rawParentInitialId!));
                continue;
            }

            var unresolvedField = rawParentLocationId != null
                ? "parentLocationId"
                : "parentInitialId";
            var unresolvedSelector = rawParentLocationId ?? rawParentInitialId!;
            issues.Add(Issue(
                candidate.Context + "." + unresolvedField,
                "mortal_location_materialization_parent_unresolved",
                "A parent selector must resolve to one exact active or accepted same-turn Mortal location.",
                "exact active locationId or accepted same-turn initialId",
                unresolvedSelector));
        }

        foreach (var start in parents.Keys)
        {
            var visited = new HashSet<string>(StringComparer.Ordinal);
            var cursor = start;
            while (parents.TryGetValue(cursor, out var parent))
            {
                if (!visited.Add(cursor) || string.Equals(parent, start, StringComparison.Ordinal))
                {
                    issues.Add(Issue(
                        "worldMapUpdates.newLocations",
                        "mortal_location_materialization_parent_cycle",
                        "Mortal location parent graph must be acyclic.",
                        "acyclic exact parent graph",
                        start));
                    break;
                }
                cursor = parent;
            }
        }
    }

    private static List<LinkCandidate> ReadLinkCandidates(
        JsonObject? updates,
        MortalLocationIdentityState identityState,
        int turn,
        MortalBootstrapLocationReservationSet? bootstrapReservations,
        List<ValidationIssue> issues)
    {
        var result = new List<LinkCandidate>();
        if (updates == null)
            return result;
        if (updates["newLinks"] is not JsonArray newLinks)
        {
            if (updates.ContainsKey("newLinks"))
            {
                issues.Add(Issue(
                    "worldMapUpdates.newLinks",
                    "mortal_location_materialization_invalid_root",
                    "newLinks must be an array.",
                    "array",
                    updates["newLinks"]?.ToJsonString() ?? "null"));
            }
            return result;
        }

        var originKeys = new HashSet<string>(StringComparer.Ordinal);
        var materializationKeys = new HashSet<string>(StringComparer.Ordinal);
        for (var index = 0; index < newLinks.Count; index++)
        {
            if (newLinks[index] is not JsonObject link)
            {
                issues.Add(Issue(
                    $"worldMapUpdates.newLinks[{index}]",
                    "mortal_location_materialization_invalid_root",
                    "Each newLinks member must be one complete object.",
                    "object",
                    newLinks[index]?.ToJsonString() ?? "null"));
                continue;
            }

            var context = $"worldMapUpdates.newLinks[{index}]";
            using var document = JsonDocument.Parse(link.ToJsonString());
            issues.AddRange(MortalLocationMaterializationContract.ValidateRawLink(
                document.RootElement,
                context,
                "world_map_link_creation"));
            var initialId = ReadExactString(link, "initialId");
            var materializationId = ReadExactString(link["materialization"] as JsonObject, "materializationId");
            var sourceTurn = ReadInt(link["materialization"] as JsonObject, "sourceTurn");
            if (sourceTurn != turn)
            {
                issues.Add(Issue(
                    context + ".materialization.sourceTurn",
                    "mortal_location_materialization_source_turn_mismatch",
                    "Link materialization source turn must match the accepted turn exactly.",
                    turn.ToString(),
                    sourceTurn?.ToString() ?? "missing"));
            }
            ValidateAcceptedTurnSourceAuthority(
                link,
                context,
                selectCurrent: false,
                isLink: true,
                turn,
                bootstrapReservations,
                issues);
            if ((initialId != null && !originKeys.Add(MortalLocationIdentityState.BuildConfusableKey(initialId))) ||
                (materializationId != null && !materializationKeys.Add(MortalLocationIdentityState.BuildConfusableKey(materializationId))))
            {
                issues.Add(Issue(
                    context,
                    "mortal_location_materialization_duplicate_creation_route",
                    "Each link origin may authorize exactly one creation.",
                    "unique link origin evidence",
                    initialId ?? materializationId ?? "missing"));
            }
            if (identityState.ContainsHistoricalLinkOrigin(initialId, materializationId))
            {
                issues.Add(Issue(
                    context,
                    "mortal_location_materialization_historical_replay",
                    "Active or retired link origin evidence cannot be reused.",
                    "new exact origin evidence",
                    $"initialId={initialId}; materializationId={materializationId}"));
            }
            result.Add(new LinkCandidate(link.DeepClone().AsObject(), context));
        }
        return result;
    }

    private static void ValidateAcceptedTurnSourceAuthority(
        JsonObject candidate,
        string context,
        bool selectCurrent,
        bool isLink,
        int turn,
        MortalBootstrapLocationReservationSet? bootstrapReservations,
        List<ValidationIssue> issues)
    {
        var initialId = ReadExactString(candidate, "initialId");
        var usesReservedBootstrapOrigin = bootstrapReservations is { State: "pending" } &&
            (isLink
                ? string.Equals(
                    initialId,
                    bootstrapReservations.Link.InitialId,
                    StringComparison.Ordinal)
                : selectCurrent
                    ? string.Equals(
                        initialId,
                        bootstrapReservations.Start.InitialId,
                        StringComparison.Ordinal)
                    : string.Equals(
                        initialId,
                        bootstrapReservations.Neighbor.InitialId,
                        StringComparison.Ordinal));
        var expectedKind = usesReservedBootstrapOrigin
            ? bootstrapReservations!.AuthorityKind
            : "turn_outcome";
        var expectedId = usesReservedBootstrapOrigin
            ? bootstrapReservations!.AuthorityId
            : "turn_" + turn;
        var authority = candidate["materialization"]?["sourceAuthority"] as JsonObject;
        var actualKind = ReadExactString(authority, "kind");
        var actualId = ReadExactString(authority, "authorityId");
        if (string.Equals(actualKind, expectedKind, StringComparison.Ordinal) &&
            string.Equals(actualId, expectedId, StringComparison.Ordinal))
        {
            return;
        }

        issues.Add(Issue(
            context + ".materialization.sourceAuthority",
            "mortal_location_materialization_source_authority_mismatch",
            "Materialization source authority must match the exact accepted-turn authority.",
            expectedKind + ":" + expectedId,
            authority?.ToJsonString() ?? "missing"));
    }

    private static void ValidateRawLinkEndpoints(
        IReadOnlyList<LinkCandidate> links,
        IReadOnlyDictionary<string, JsonObject> preTurnLocationsById,
        IReadOnlyList<LocationCandidate> locationCandidates,
        MortalLocationIdentityState identityState,
        List<ValidationIssue> issues)
    {
        var sameTurnInitialIds = locationCandidates
            .Select(candidate => ReadExactString(candidate.Raw, "initialId"))
            .Where(static value => value != null)
            .Cast<string>()
            .ToHashSet(StringComparer.Ordinal);
        var sameTurnIdentityKeys = sameTurnInitialIds
            .Select(MortalLocationIdentityState.BuildConfusableKey)
            .ToHashSet(StringComparer.Ordinal);
        var activePermanentIdentityKeys = preTurnLocationsById.Keys
            .Select(MortalLocationIdentityState.BuildConfusableKey)
            .ToHashSet(StringComparer.Ordinal);
        foreach (var candidate in links)
        {
            foreach (var endpoint in new[] { "source", "target" })
            {
                var permanent = ReadExactString(candidate.Raw, endpoint + "LocationId");
                var temporary = ReadExactString(candidate.Raw, endpoint + "InitialId");
                var resolved = permanent != null && preTurnLocationsById.ContainsKey(permanent) ||
                               temporary != null && sameTurnInitialIds.Contains(temporary);
                if (resolved)
                    continue;

                var permanentField = endpoint + "LocationId";
                var temporaryField = endpoint + "InitialId";
                var rawPermanent = ReadStringScalar(candidate.Raw, permanentField);
                var rawTemporary = ReadStringScalar(candidate.Raw, temporaryField);
                var selectorField = rawPermanent != null
                    ? permanentField
                    : rawTemporary != null
                        ? temporaryField
                        : permanentField;
                var selector = rawPermanent ?? rawTemporary;
                var historical =
                    identityState.ContainsRetiredLocationId(rawPermanent) ||
                    identityState.ContainsHistoricalLocationOrigin(rawTemporary, null);
                if (historical)
                {
                    issues.Add(Issue(
                        candidate.Context + "." + selectorField,
                        "mortal_location_link_endpoint_historical_replay",
                        "A link endpoint cannot reuse retired permanent identity or historical creation-origin evidence.",
                        "exact active locationId or accepted same-turn initialId",
                        selector ?? "missing"));
                    continue;
                }

                var confusable =
                    rawPermanent != null &&
                    activePermanentIdentityKeys.Contains(
                        MortalLocationIdentityState.BuildConfusableKey(rawPermanent)) ||
                    rawTemporary != null &&
                    sameTurnIdentityKeys.Contains(
                        MortalLocationIdentityState.BuildConfusableKey(rawTemporary));
                if (confusable)
                {
                    issues.Add(Issue(
                        candidate.Context + "." + selectorField,
                        "mortal_location_link_endpoint_confusable",
                        "A link endpoint must match exact case-sensitive identity authority and cannot use a Unicode or whitespace alias.",
                        "exact active locationId or accepted same-turn initialId",
                        selector ?? "missing"));
                    continue;
                }

                issues.Add(Issue(
                    candidate.Context + "." + selectorField,
                    "mortal_location_link_endpoint_unresolved",
                    "Each link endpoint must resolve exactly once to pre-turn or same-turn location authority.",
                    "exact active locationId or accepted initialId",
                    selector ?? "missing"));
            }
        }
    }

    private static void ValidateCreationTopologyDisposition(
        IReadOnlyList<LocationCandidate> locations,
        IReadOnlyList<LinkCandidate> links,
        List<ValidationIssue> issues)
    {
        foreach (var location in locations)
        {
            var initialId = ReadExactString(location.Raw, "initialId");
            if (initialId == null)
                continue;

            var incidentLinkCount = links.Count(link =>
                string.Equals(ReadExactString(link.Raw, "sourceInitialId"), initialId, StringComparison.Ordinal) ||
                string.Equals(ReadExactString(link.Raw, "targetInitialId"), initialId, StringComparison.Ordinal));
            var disposition = ReadExactString(
                location.Raw["materialization"]?["sections"]?["topology"] as JsonObject,
                "disposition");
            var matches = disposition == "populated"
                ? incidentLinkCount > 0
                : disposition == "empty_by_design" && incidentLinkCount == 0;
            if (matches)
                continue;

            issues.Add(Issue(
                location.Context + ".materialization.sections.topology.disposition",
                "mortal_location_materialization_topology_disposition_mismatch",
                "A new location topology disposition must match its accepted same-turn incident links.",
                incidentLinkCount == 0 ? "empty_by_design" : "populated",
                disposition ?? "missing"));
        }
    }

    private static List<LocationUpdate> ReadLocationUpdates(
        JsonObject? updates,
        IReadOnlyDictionary<string, JsonObject> preTurnLocationsById,
        List<ValidationIssue> issues)
    {
        var result = new List<LocationUpdate>();
        if (!TryReadOperationArray(updates, "locationUpdates", out var values, issues))
            return result;

        var seenTargets = new HashSet<string>(StringComparer.Ordinal);
        for (var index = 0; index < values.Count; index++)
        {
            var context = $"worldMapUpdates.locationUpdates[{index}]";
            if (values[index] is not JsonObject update)
            {
                issues.Add(InvalidOperationRoot(context, values[index]));
                continue;
            }

            var forbidden = update.Select(static pair => pair.Key)
                .Where(field => field != "locationId" && !MutableLocationUpdateFields.Contains(field))
                .ToArray();
            if (forbidden.Length > 0)
            {
                issues.Add(Issue(
                    context,
                    "mortal_location_update_field_forbidden",
                    "Location updates use a closed mutable semantic field catalog.",
                    "locationId plus mutable presentation, difficulty, chronicle, or governed semantic fields",
                    string.Join(',', forbidden)));
            }

            if (!TryGetExactIdentity(update, "locationId", out var locationId) ||
                !preTurnLocationsById.ContainsKey(locationId))
            {
                issues.Add(Issue(
                    context + ".locationId",
                    "mortal_location_update_target_unresolved",
                    "A location update must bind one exact active pre-turn locationId.",
                    "exact active locationId",
                    update["locationId"]?.ToJsonString() ?? "missing"));
                continue;
            }

            if (!seenTargets.Add(MortalLocationIdentityState.BuildConfusableKey(locationId)))
            {
                issues.Add(Issue(
                    context + ".locationId",
                    "mortal_location_update_target_ambiguous",
                    "A location may have at most one narrow update in an accepted turn.",
                    "unique exact locationId",
                    locationId));
                continue;
            }

            if (!update.Any(pair => MutableLocationUpdateFields.Contains(pair.Key)))
            {
                issues.Add(Issue(
                    context,
                    "mortal_location_update_empty",
                    "A location update must change at least one mutable field.",
                    "one mutable field",
                    update.ToJsonString()));
                continue;
            }

            result.Add(new LocationUpdate(update.DeepClone().AsObject(), context, locationId));
        }
        return result;
    }

    private static List<LocationDiscoveryTransition> ReadLocationDiscoveryTransitions(
        JsonObject? updates,
        IReadOnlyDictionary<string, JsonObject> preTurnLocationsById,
        List<ValidationIssue> issues)
    {
        var result = new List<LocationDiscoveryTransition>();
        if (!TryReadOperationArray(updates, "locationDiscoveryTransitions", out var values, issues))
            return result;

        var seenTargets = new HashSet<string>(StringComparer.Ordinal);
        for (var index = 0; index < values.Count; index++)
        {
            var context = $"worldMapUpdates.locationDiscoveryTransitions[{index}]";
            if (values[index] is not JsonObject transition)
            {
                issues.Add(InvalidOperationRoot(context, values[index]));
                continue;
            }

            ValidateExactOperationFields(
                transition,
                LocationDiscoveryTransitionFields,
                context,
                "mortal_location_discovery_transition_invalid",
                issues);
            if (!TryGetExactIdentity(transition, "locationId", out var locationId) ||
                !preTurnLocationsById.TryGetValue(locationId, out var canonical))
            {
                issues.Add(Issue(
                    context + ".locationId",
                    "mortal_location_discovery_target_unresolved",
                    "A discovery transition must bind one exact active pre-turn locationId.",
                    "exact active locationId",
                    transition["locationId"]?.ToJsonString() ?? "missing"));
                continue;
            }

            if (!seenTargets.Add(MortalLocationIdentityState.BuildConfusableKey(locationId)))
            {
                issues.Add(Issue(
                    context + ".locationId",
                    "mortal_location_discovery_target_ambiguous",
                    "A location may have at most one discovery transition in an accepted turn.",
                    "unique exact locationId",
                    locationId));
                continue;
            }

            var actualTier = ReadExactString(canonical["discovery"] as JsonObject, "tier");
            var fromTier = ReadExactString(transition, "fromTier");
            var toTier = ReadExactString(transition, "toTier");
            var audience = ReadExactString(transition, "toAudience");
            var reason = ReadExactString(transition, "reason");
            if (!string.Equals(actualTier, fromTier, StringComparison.Ordinal))
            {
                issues.Add(Issue(
                    context + ".fromTier",
                    "mortal_location_discovery_precondition_mismatch",
                    "A discovery transition must match the exact canonical pre-state.",
                    actualTier ?? "missing",
                    fromTier ?? "missing"));
            }
            if (!IsValidDiscoveryTransition(fromTier, toTier, audience, transition["rumorSummary"]))
            {
                issues.Add(Issue(
                    context,
                    "mortal_location_discovery_transition_invalid",
                    "Discovery may advance only through an allowed forward edge with matching audience and rumor semantics.",
                    "allowed forward discovery edge",
                    transition.ToJsonString()));
            }
            if (reason == null)
            {
                issues.Add(Issue(
                    context + ".reason",
                    "mortal_location_discovery_transition_invalid",
                    "A discovery transition requires one non-empty in-world reason.",
                    "non-empty reason",
                    transition["reason"]?.ToJsonString() ?? "missing"));
            }

            result.Add(new LocationDiscoveryTransition(
                transition.DeepClone().AsObject(),
                context,
                locationId,
                fromTier,
                toTier,
                audience));
        }
        return result;
    }

    private static List<LinkUpdate> ReadLinkUpdates(
        JsonObject? updates,
        IReadOnlyDictionary<string, JsonObject> preTurnLinksById,
        List<ValidationIssue> issues)
    {
        var result = new List<LinkUpdate>();
        if (!TryReadOperationArray(updates, "linkUpdates", out var values, issues))
            return result;

        var seenTargets = new HashSet<string>(StringComparer.Ordinal);
        for (var index = 0; index < values.Count; index++)
        {
            var context = $"worldMapUpdates.linkUpdates[{index}]";
            if (values[index] is not JsonObject update)
            {
                issues.Add(InvalidOperationRoot(context, values[index]));
                continue;
            }

            ValidateAllowedOperationFields(
                update,
                LinkUpdateFields,
                context,
                "mortal_location_link_update_field_forbidden",
                issues);
            if (!TryGetExactIdentity(update, "linkId", out var linkId) ||
                !preTurnLinksById.TryGetValue(linkId, out var canonical))
            {
                issues.Add(Issue(
                    context + ".linkId",
                    "mortal_location_link_update_target_unresolved",
                    "A link update must bind one exact active pre-turn linkId.",
                    "exact active linkId",
                    update["linkId"]?.ToJsonString() ?? "missing"));
                continue;
            }

            if (!seenTargets.Add(MortalLocationIdentityState.BuildConfusableKey(linkId)))
            {
                issues.Add(Issue(
                    context + ".linkId",
                    "mortal_location_link_update_target_ambiguous",
                    "A link may have at most one update in an accepted turn.",
                    "unique exact linkId",
                    linkId));
                continue;
            }

            if (update.Count <= 1)
            {
                issues.Add(Issue(
                    context,
                    "mortal_location_link_update_empty",
                    "A link update must change at least one mutable field.",
                    "one mutable field",
                    update.ToJsonString()));
            }

            if (update["access"] is JsonObject access)
                ValidateLinkAccessPatch(access, context + ".access", issues);
            else if (update.ContainsKey("access"))
                issues.Add(InvalidLifecycleShape(context + ".access", "mortal_location_link_update_invalid", update["access"]));

            if (update["discovery"] is JsonObject discovery)
                ValidateLinkDiscoveryPatch(canonical, discovery, context + ".discovery", issues);
            else if (update.ContainsKey("discovery"))
                issues.Add(InvalidLifecycleShape(context + ".discovery", "mortal_location_link_update_invalid", update["discovery"]));

            result.Add(new LinkUpdate(update.DeepClone().AsObject(), context, linkId));
        }
        return result;
    }

    private static List<LinkRemoval> ReadLinkRemovals(
        JsonObject? updates,
        IReadOnlyDictionary<string, JsonObject> preTurnLinksById,
        List<ValidationIssue> issues)
    {
        var result = new List<LinkRemoval>();
        if (!TryReadOperationArray(updates, "linkRemovals", out var values, issues))
            return result;

        var seenTargets = new HashSet<string>(StringComparer.Ordinal);
        for (var index = 0; index < values.Count; index++)
        {
            var context = $"worldMapUpdates.linkRemovals[{index}]";
            if (values[index] is not JsonObject removal)
            {
                issues.Add(InvalidOperationRoot(context, values[index]));
                continue;
            }

            ValidateExactOperationFields(
                removal,
                LinkRemovalFields,
                context,
                "mortal_location_link_removal_invalid",
                issues);
            if (!TryGetExactIdentity(removal, "linkId", out var linkId) ||
                !preTurnLinksById.TryGetValue(linkId, out var canonical))
            {
                issues.Add(Issue(
                    context + ".linkId",
                    "mortal_location_link_removal_target_unresolved",
                    "A link removal must bind one exact active pre-turn linkId.",
                    "exact active linkId",
                    removal["linkId"]?.ToJsonString() ?? "missing"));
                continue;
            }

            if (!seenTargets.Add(MortalLocationIdentityState.BuildConfusableKey(linkId)))
            {
                issues.Add(Issue(
                    context + ".linkId",
                    "mortal_location_link_removal_target_ambiguous",
                    "A link may be retired at most once in an accepted turn.",
                    "unique exact linkId",
                    linkId));
                continue;
            }

            var source = ReadExactString(removal, "sourceLocationId");
            var target = ReadExactString(removal, "targetLocationId");
            if (!string.Equals(source, ReadExactString(canonical, "sourceLocationId"), StringComparison.Ordinal) ||
                !string.Equals(target, ReadExactString(canonical, "targetLocationId"), StringComparison.Ordinal))
            {
                issues.Add(Issue(
                    context,
                    "mortal_location_link_transition_precondition_mismatch",
                    "Link removal endpoints must match the exact accepted link pre-state.",
                    $"{ReadExactString(canonical, "sourceLocationId")}->{ReadExactString(canonical, "targetLocationId")}",
                    $"{source ?? "missing"}->{target ?? "missing"}"));
            }
            if (ReadExactString(removal, "reason") == null)
            {
                issues.Add(Issue(
                    context + ".reason",
                    "mortal_location_link_removal_invalid",
                    "Link removal requires one non-empty in-world reason.",
                    "non-empty reason",
                    removal["reason"]?.ToJsonString() ?? "missing"));
            }

            result.Add(new LinkRemoval(removal.DeepClone().AsObject(), context, linkId));
        }
        return result;
    }

    private static void ValidateLifecycleOperationConflicts(
        IReadOnlyList<LinkUpdate> updates,
        IReadOnlyList<LinkRemoval> removals,
        List<ValidationIssue> issues)
    {
        var updated = updates.Select(static update => update.LinkId).ToHashSet(StringComparer.Ordinal);
        foreach (var removal in removals.Where(removal => updated.Contains(removal.LinkId)))
        {
            issues.Add(Issue(
                removal.Context + ".linkId",
                "mortal_location_link_transition_conflict",
                "A link cannot be updated and retired in the same accepted turn.",
                "one lifecycle operation per link",
                removal.LinkId));
        }
    }

    private static bool TryReadOperationArray(
        JsonObject? updates,
        string field,
        out JsonArray values,
        List<ValidationIssue> issues)
    {
        values = new JsonArray();
        if (updates == null || !updates.ContainsKey(field))
            return false;
        if (updates[field] is JsonArray array)
        {
            values = array;
            return true;
        }

        issues.Add(Issue(
            "worldMapUpdates." + field,
            "mortal_location_materialization_invalid_root",
            field + " must be an array.",
            "array",
            updates[field]?.ToJsonString() ?? "null"));
        return false;
    }

    private static void ValidateAllowedOperationFields(
        JsonObject operation,
        IReadOnlySet<string> allowed,
        string context,
        string code,
        List<ValidationIssue> issues)
    {
        var forbidden = operation.Select(static pair => pair.Key)
            .Where(field => !allowed.Contains(field))
            .ToArray();
        if (forbidden.Length == 0)
            return;
        issues.Add(Issue(
            context,
            code,
            "This lifecycle operation uses a closed field catalog.",
            string.Join(',', allowed.OrderBy(static field => field, StringComparer.Ordinal)),
            string.Join(',', forbidden)));
    }

    private static void ValidateExactOperationFields(
        JsonObject operation,
        IReadOnlySet<string> expected,
        string context,
        string code,
        List<ValidationIssue> issues)
    {
        ValidateAllowedOperationFields(operation, expected, context, code, issues);
        var missing = expected.Where(field => !operation.ContainsKey(field)).ToArray();
        if (missing.Length == 0)
            return;
        issues.Add(Issue(
            context,
            code,
            "This lifecycle operation is missing required fields.",
            string.Join(',', expected.OrderBy(static field => field, StringComparer.Ordinal)),
            "missing=" + string.Join(',', missing)));
    }

    private static void ValidateLinkAccessPatch(
        JsonObject access,
        string context,
        List<ValidationIssue> issues)
    {
        ValidateExactOperationFields(
            access,
            LinkAccessFields,
            context,
            "mortal_location_link_update_invalid",
            issues);
        var state = ReadExactString(access, "state");
        var requiresReason = state is "conditional" or "sealed";
        if (state is not ("open" or "conditional" or "sealed") ||
            requiresReason && ReadExactString(access, "reason") == null ||
            access["requirements"] is not JsonArray)
        {
            issues.Add(InvalidLifecycleShape(context, "mortal_location_link_update_invalid", access));
        }
    }

    private static void ValidateLinkDiscoveryPatch(
        JsonObject canonical,
        JsonObject discovery,
        string context,
        List<ValidationIssue> issues)
    {
        ValidateExactOperationFields(
            discovery,
            LinkDiscoveryTransitionFields,
            context,
            "mortal_location_link_update_invalid",
            issues);
        var actualTier = ReadExactString(canonical["discovery"] as JsonObject, "tier");
        var fromTier = ReadExactString(discovery, "fromTier");
        var toTier = ReadExactString(discovery, "toTier");
        var audience = ReadExactString(discovery, "toAudience");
        if (!string.Equals(actualTier, fromTier, StringComparison.Ordinal))
        {
            issues.Add(Issue(
                context + ".fromTier",
                "mortal_location_link_transition_precondition_mismatch",
                "A link discovery update must match its exact canonical pre-state.",
                actualTier ?? "missing",
                fromTier ?? "missing"));
        }
        if (!IsValidDiscoveryTransition(fromTier, toTier, audience, discovery["rumorSummary"]))
            issues.Add(InvalidLifecycleShape(context, "mortal_location_link_update_invalid", discovery));
    }

    private static bool IsValidDiscoveryTransition(
        string? fromTier,
        string? toTier,
        string? audience,
        JsonNode? rumorSummary)
    {
        if (fromTier == null || toTier == null ||
            !DiscoveryTiers.Contains(fromTier) || !DiscoveryTiers.Contains(toTier) ||
            !ForwardDiscoveryEdges.Contains(fromTier + "\u001f" + toTier) ||
            !string.Equals(audience, "player_known", StringComparison.Ordinal))
        {
            return false;
        }

        var summary = rumorSummary is JsonValue value && value.TryGetValue<string>(out var text) &&
                      !string.IsNullOrEmpty(text) && string.Equals(text, text.Trim(), StringComparison.Ordinal)
            ? text
            : null;
        return toTier == "rumored" ? summary != null : rumorSummary == null;
    }

    private static ValidationIssue InvalidOperationRoot(string context, JsonNode? actual) =>
        Issue(
            context,
            "mortal_location_materialization_invalid_root",
            "Each lifecycle operation must be one object.",
            "object",
            actual?.ToJsonString() ?? "null");

    private static ValidationIssue InvalidLifecycleShape(string context, string code, JsonNode? actual) =>
        Issue(
            context,
            code,
            "The lifecycle operation has an invalid closed shape.",
            "complete operation-specific object",
            actual?.ToJsonString() ?? "null");

    private static ExistingSelection? ValidateExistingSelection(
        JsonObject raw,
        string existingId,
        JsonObject? preTurnCurrentLocation,
        IReadOnlyDictionary<string, JsonObject> preTurnLocationsById,
        JsonArray preTurnLinks,
        List<ValidationIssue> issues)
    {
        var forbidden = raw.Select(static pair => pair.Key)
            .Where(field => !ExistingCurrentFields.Contains(field))
            .ToArray();
        if (forbidden.Length > 0)
        {
            issues.Add(Issue(
                "currentLocationData",
                "mortal_location_materialization_existing_full_resend",
                "Existing movement may carry only exact identity and current operational fields.",
                string.Join(',', ExistingCurrentFields.OrderBy(static field => field, StringComparer.Ordinal)),
                string.Join(',', forbidden)));
        }
        if (raw["locationStorages"] is JsonArray rawStorages)
        {
            for (var index = 0; index < rawStorages.Count; index++)
            {
                if (rawStorages[index] is not JsonObject storage ||
                    !storage.ContainsKey("contents"))
                {
                    continue;
                }

                issues.Add(Issue(
                    $"currentLocationData.locationStorages[{index}].contents",
                    "mortal_location_movement_client_owned_item_contents_forbidden",
                    "Existing movement cannot author or echo client-owned storage item contents.",
                    "field absent; the client relocates accepted contents atomically",
                    storage["contents"]?.ToJsonString() ?? "null"));
            }
        }
        if (!preTurnLocationsById.TryGetValue(existingId, out var canonical))
        {
            issues.Add(Issue(
                "currentLocationData.locationId",
                "mortal_location_materialization_existing_target_unresolved",
                "Existing current selection must resolve one exact active map location.",
                "exact active locationId",
                existingId));
            return null;
        }

        var preTurnCurrentId = ReadExactString(preTurnCurrentLocation, "locationId");
        if (!string.Equals(preTurnCurrentId, existingId, StringComparison.Ordinal) &&
            (preTurnCurrentId == null ||
             !IsPlayerKnown(canonical) ||
             !preTurnLinks.OfType<JsonObject>().Any(link =>
                 IsAuthorizedTraversal(link, preTurnCurrentId, existingId))))
        {
            issues.Add(Issue(
                "currentLocationData.locationId",
                "mortal_location_movement_not_authorized",
                "Changing the current location requires one exact visible open outgoing link from the pre-turn current location.",
                "same current location or exact player-known open directed link",
                existingId));
        }

        foreach (var field in raw.Select(static property => property.Key)
                     .Where(ExistingProtectedResendFields.Contains))
        {
            var code = field switch
            {
                "materialization" or "materializationId" or "initialId" or "parentInitialId" =>
                    "mortal_location_materialization_identity_conflict",
                "materializationReceipt" or "receiptId" or "seal" =>
                    "mortal_location_materialization_receipt_conflict",
                _ => "mortal_location_materialization_gm_authored_client_field"
            };
            issues.Add(Issue(
                "currentLocationData." + field,
                code,
                "An existing-location resend cannot carry historical or client-owned authority fields.",
                "field absent from the GM-authored current-selection route",
                raw[field]?.ToJsonString() ?? "null"));
        }
        return new ExistingSelection(raw.DeepClone().AsObject(), canonical.DeepClone().AsObject());
    }

    private static bool IsAuthorizedTraversal(
        JsonObject link,
        string sourceLocationId,
        string targetLocationId)
    {
        if (!string.Equals(ReadExactString(link, "sourceLocationId"), sourceLocationId, StringComparison.Ordinal) ||
            !string.Equals(ReadExactString(link, "targetLocationId"), targetLocationId, StringComparison.Ordinal) ||
            link["access"] is not JsonObject access ||
            !string.Equals(ReadExactString(access, "state"), "open", StringComparison.Ordinal) ||
            access["requirements"] is not JsonArray requirements ||
            requirements.Count != 0 ||
            link["discovery"] is not JsonObject discovery ||
            string.Equals(ReadExactString(discovery, "tier"), "hidden", StringComparison.Ordinal) ||
            !string.Equals(ReadExactString(discovery, "audience"), "player_known", StringComparison.Ordinal))
        {
            return false;
        }

        return true;
    }

    private static bool IsPlayerKnown(JsonObject location) =>
        location["discovery"] is JsonObject discovery &&
        !string.Equals(ReadExactString(discovery, "tier"), "hidden", StringComparison.Ordinal) &&
        string.Equals(ReadExactString(discovery, "audience"), "player_known", StringComparison.Ordinal);

    private static void ApplyLocationUpdates(
        JsonArray locations,
        JsonArray indexEntries,
        IReadOnlyList<LocationUpdate> updates,
        int turn,
        MortalLocationIdentityFactory identityFactory)
    {
        foreach (var update in updates)
        {
            var canonical = FindExactObject(locations, "locationId", update.LocationId)!;
            var before = new JsonObject();
            var after = new JsonObject();
            foreach (var field in MutableLocationUpdateFields)
            {
                if (!update.Raw.ContainsKey(field))
                    continue;
                before[field] = canonical[field]?.DeepClone();
                canonical[field] = update.Raw[field]?.DeepClone();
                after[field] = canonical[field]?.DeepClone();
            }
            AppendLifecycleTransition(
                indexEntries,
                "locationId",
                update.LocationId,
                CreateLifecycleTransition(
                    identityFactory,
                    "location_update",
                    turn,
                    update.LocationId,
                    before,
                    after,
                    update.Context));
        }
    }

    private static void ApplyLocationDiscoveryTransitions(
        JsonArray locations,
        JsonArray indexEntries,
        IReadOnlyList<LocationDiscoveryTransition> transitions,
        int turn,
        MortalLocationIdentityFactory identityFactory)
    {
        foreach (var transition in transitions)
        {
            var canonical = FindExactObject(locations, "locationId", transition.LocationId)!;
            var before = canonical["discovery"]!.DeepClone().AsObject();
            canonical["discovery"] = CreateDiscoveryState(
                transition.ToTier!,
                transition.Audience!,
                transition.Raw["rumorSummary"]);
            var after = canonical["discovery"]!.DeepClone().AsObject();
            AppendLifecycleTransition(
                indexEntries,
                "locationId",
                transition.LocationId,
                CreateLifecycleTransition(
                    identityFactory,
                    "location_discovery",
                    turn,
                    transition.LocationId,
                    before,
                    after,
                    transition.Context));
        }
    }

    private static void ApplyLinkUpdates(
        JsonArray links,
        JsonArray indexEntries,
        IReadOnlyList<LinkUpdate> updates,
        int turn,
        MortalLocationIdentityFactory identityFactory)
    {
        foreach (var update in updates)
        {
            var canonical = FindExactObject(links, "linkId", update.LinkId)!;
            var before = new JsonObject();
            var after = new JsonObject();
            foreach (var field in new[] { "name", "description", "directionLabel", "access" })
            {
                if (!update.Raw.ContainsKey(field))
                    continue;
                before[field] = canonical[field]?.DeepClone();
                canonical[field] = update.Raw[field]?.DeepClone();
                after[field] = canonical[field]?.DeepClone();
            }
            if (update.Raw["discovery"] is JsonObject discovery)
            {
                before["discovery"] = canonical["discovery"]?.DeepClone();
                canonical["discovery"] = CreateDiscoveryState(
                    ReadExactString(discovery, "toTier")!,
                    ReadExactString(discovery, "toAudience")!,
                    discovery["rumorSummary"]);
                after["discovery"] = canonical["discovery"]?.DeepClone();
            }

            AppendLifecycleTransition(
                indexEntries,
                "linkId",
                update.LinkId,
                CreateLifecycleTransition(
                    identityFactory,
                    "link_update",
                    turn,
                    update.LinkId,
                    before,
                    after,
                    update.Context,
                    ReadExactString(canonical, "sourceLocationId"),
                    ReadExactString(canonical, "targetLocationId")));
        }
    }

    private static void ApplyLinkRemovals(
        JsonArray links,
        JsonArray indexEntries,
        IReadOnlyList<LinkRemoval> removals,
        int turn,
        MortalLocationIdentityFactory identityFactory)
    {
        foreach (var removal in removals)
        {
            var canonical = FindExactObject(links, "linkId", removal.LinkId)!;
            var indexEntry = FindExactObject(indexEntries, "linkId", removal.LinkId)!;
            var before = new JsonObject
            {
                ["state"] = "active",
                ["access"] = canonical["access"]?.DeepClone(),
                ["discovery"] = canonical["discovery"]?.DeepClone()
            };
            var after = new JsonObject
            {
                ["state"] = "retired",
                ["reason"] = removal.Raw["reason"]?.DeepClone()
            };
            indexEntry["state"] = "retired";
            indexEntry["transitions"]!.AsArray().Add(CreateLifecycleTransition(
                identityFactory,
                "link_retirement",
                turn,
                removal.LinkId,
                before,
                after,
                removal.Context,
                ReadExactString(canonical, "sourceLocationId"),
                ReadExactString(canonical, "targetLocationId")));
            links.Remove(canonical);
        }
    }

    private static void ApplyExistingMovement(
        JsonArray locations,
        JsonArray indexEntries,
        ExistingSelection selection,
        int turn,
        MortalLocationIdentityFactory identityFactory)
    {
        var locationId = ReadExactString(selection.RawSelection, "locationId")!;
        var canonical = FindExactObject(locations, "locationId", locationId)!;
        var before = new JsonObject
        {
            ["discovery"] = canonical["discovery"]?.DeepClone(),
            ["lastEventsDescription"] = canonical["lastEventsDescription"]?.DeepClone()
        };
        if (selection.RawSelection.ContainsKey("lastEventsDescription"))
            canonical["lastEventsDescription"] = selection.RawSelection["lastEventsDescription"]?.DeepClone();
        canonical["discovery"] = CreateDiscoveryState("visited", "player_known", null);
        var after = new JsonObject
        {
            ["discovery"] = canonical["discovery"]?.DeepClone(),
            ["lastEventsDescription"] = canonical["lastEventsDescription"]?.DeepClone()
        };
        AppendLifecycleTransition(
            indexEntries,
            "locationId",
            locationId,
            CreateLifecycleTransition(
                identityFactory,
                "current_selection",
                turn,
                locationId,
                before,
                after,
                "currentLocationData"));
    }

    private static JsonObject CreateLifecycleTransition(
        MortalLocationIdentityFactory identityFactory,
        string kind,
        int turn,
        string entityId,
        JsonObject beforeState,
        JsonObject afterState,
        string operationRef,
        string? sourceLocationId = null,
        string? targetLocationId = null) =>
        new()
        {
            ["transitionId"] = identityFactory.CreateTransitionId(),
            ["kind"] = kind,
            ["turn"] = turn,
            ["entityId"] = entityId,
            ["beforeState"] = beforeState.DeepClone(),
            ["afterState"] = afterState.DeepClone(),
            ["sourceAuthorityKind"] = "turn_outcome",
            ["sourceAuthorityId"] = "turn_" + turn,
            ["operationRef"] = operationRef,
            ["sourceLocationId"] = sourceLocationId,
            ["targetLocationId"] = targetLocationId
        };

    private static void AppendLifecycleTransition(
        JsonArray indexEntries,
        string identityField,
        string identity,
        JsonObject transition)
    {
        var entry = FindExactObject(indexEntries, identityField, identity)
            ?? throw new InvalidOperationException("Accepted location identity entry is missing.");
        entry["transitions"]!.AsArray().Add(transition);
    }

    private static JsonObject CreateDiscoveryState(
        string tier,
        string audience,
        JsonNode? rumorSummary) =>
        new()
        {
            ["tier"] = tier,
            ["audience"] = audience,
            ["rumorSummary"] = rumorSummary?.DeepClone()
        };

    private static void RebuildDerivedCurrentTopology(
        JsonObject current,
        JsonArray locations,
        JsonArray links)
    {
        current.Remove("knownExits");
        current.Remove("adjacencyMap");
        var currentId = ReadExactString(current, "locationId");
        if (currentId == null)
            return;

        var locationsById = locations.OfType<JsonObject>()
            .Where(static location => ReadExactString(location, "locationId") != null)
            .ToDictionary(static location => ReadExactString(location, "locationId")!, StringComparer.Ordinal);
        var exits = new JsonArray();
        var adjacency = new JsonArray();
        foreach (var link in links.OfType<JsonObject>())
        {
            if (!string.Equals(ReadExactString(link, "sourceLocationId"), currentId, StringComparison.Ordinal) ||
                string.Equals(ReadExactString(link["discovery"] as JsonObject, "tier"), "hidden", StringComparison.Ordinal))
            {
                continue;
            }

            var targetId = ReadExactString(link, "targetLocationId");
            if (targetId == null || !locationsById.TryGetValue(targetId, out var target) ||
                string.Equals(ReadExactString(target["discovery"] as JsonObject, "tier"), "hidden", StringComparison.Ordinal))
            {
                continue;
            }

            var direction = ReadExactString(link, "directionLabel")!;
            exits.Add(direction);
            adjacency.Add(new JsonObject
            {
                ["linkId"] = link["linkId"]?.DeepClone(),
                ["targetLocationId"] = target["locationId"]?.DeepClone(),
                ["targetLocationName"] = target["displayName"]?.DeepClone(),
                ["name"] = target["displayName"]?.DeepClone(),
                ["direction"] = direction,
                ["linkState"] = link["access"]?["state"]?.DeepClone(),
                ["linkType"] = link["linkType"]?.DeepClone(),
                ["travelMode"] = link["travelMode"]?.DeepClone(),
                ["description"] = link["description"]?.DeepClone()
            });
        }

        if (adjacency.Count == 0)
            return;
        current["knownExits"] = exits;
        current["adjacencyMap"] = adjacency;
    }

    private static JsonObject? FindExactObject(JsonArray values, string identityField, string identity)
    {
        var matches = values.OfType<JsonObject>()
            .Where(value => string.Equals(ReadExactString(value, identityField), identity, StringComparison.Ordinal))
            .Take(2)
            .ToArray();
        return matches.Length == 1 ? matches[0] : null;
    }

    private static JsonObject CreateCanonicalLocation(
        LocationCandidate candidate,
        string locationId,
        string receiptId,
        IReadOnlyDictionary<string, string> idsByInitialId)
    {
        var canonical = candidate.Raw.DeepClone().AsObject();
        var initialId = ReadExactString(canonical, "initialId")!;
        canonical["locationId"] = locationId;
        canonical.Remove("initialId");
        var parentInitialId = ReadExactString(canonical, "parentInitialId");
        canonical.Remove("parentInitialId");
        if (parentInitialId != null)
            canonical["parentLocationId"] = idsByInitialId[parentInitialId];
        foreach (var field in CurrentOperationalFields)
            canonical.Remove(field);
        StripStorageContents(canonical);

        var envelope = canonical[MortalLocationMaterializationContract.EnvelopeProperty]!.AsObject();
        var authority = envelope["sourceAuthority"]!.AsObject();
        var receipt = new JsonObject
        {
            ["schemaVersion"] = 1,
            ["receiptId"] = receiptId,
            ["locationId"] = locationId,
            ["initialId"] = initialId,
            ["materializationId"] = envelope["materializationId"]!.DeepClone(),
            ["realm"] = "mortal_world",
            ["route"] = envelope["route"]!.DeepClone(),
            ["sourceTurn"] = envelope["sourceTurn"]!.DeepClone(),
            ["sourceAuthorityKind"] = authority["kind"]!.DeepClone(),
            ["sourceAuthorityId"] = authority["authorityId"]!.DeepClone()
        };
        receipt["seal"] = MortalLocationMaterializationContract.ComputeSeal(envelope, receipt);
        canonical[MortalLocationMaterializationContract.ReceiptProperty] = receipt;
        return canonical;
    }

    private static JsonObject CreateCanonicalLink(
        JsonObject raw,
        string linkId,
        string receiptId,
        IReadOnlyDictionary<string, string> locationIdsByInitialId)
    {
        var canonical = raw.DeepClone().AsObject();
        var initialId = ReadExactString(canonical, "initialId")!;
        canonical["linkId"] = linkId;
        canonical.Remove("initialId");
        foreach (var endpoint in new[] { "source", "target" })
        {
            var temporaryField = endpoint + "InitialId";
            var permanentField = endpoint + "LocationId";
            var temporary = ReadExactString(canonical, temporaryField);
            canonical.Remove(temporaryField);
            if (temporary != null)
                canonical[permanentField] = locationIdsByInitialId[temporary];
        }

        var envelope = canonical[MortalLocationMaterializationContract.EnvelopeProperty]!.AsObject();
        var authority = envelope["sourceAuthority"]!.AsObject();
        var receipt = new JsonObject
        {
            ["schemaVersion"] = 1,
            ["receiptId"] = receiptId,
            ["linkId"] = linkId,
            ["initialId"] = initialId,
            ["materializationId"] = envelope["materializationId"]!.DeepClone(),
            ["realm"] = "mortal_world",
            ["route"] = envelope["route"]!.DeepClone(),
            ["sourceTurn"] = envelope["sourceTurn"]!.DeepClone(),
            ["sourceAuthorityKind"] = authority["kind"]!.DeepClone(),
            ["sourceAuthorityId"] = authority["authorityId"]!.DeepClone(),
            ["sourceLocationId"] = canonical["sourceLocationId"]!.DeepClone(),
            ["targetLocationId"] = canonical["targetLocationId"]!.DeepClone()
        };
        receipt["seal"] = MortalLocationMaterializationContract.ComputeSeal(envelope, receipt);
        canonical[MortalLocationMaterializationContract.ReceiptProperty] = receipt;
        return canonical;
    }

    private static JsonObject CreateLocationIndexEntry(JsonObject canonical)
    {
        var envelope = canonical[MortalLocationMaterializationContract.EnvelopeProperty]!.AsObject();
        var receipt = canonical[MortalLocationMaterializationContract.ReceiptProperty]!.AsObject();
        return new JsonObject
        {
            ["locationId"] = canonical["locationId"]!.DeepClone(),
            ["initialId"] = receipt["initialId"]!.DeepClone(),
            ["materializationId"] = receipt["materializationId"]!.DeepClone(),
            ["receiptId"] = receipt["receiptId"]!.DeepClone(),
            ["realm"] = "mortal_world",
            ["route"] = receipt["route"]!.DeepClone(),
            ["sourceTurn"] = receipt["sourceTurn"]!.DeepClone(),
            ["sourceAuthorityKind"] = receipt["sourceAuthorityKind"]!.DeepClone(),
            ["sourceAuthorityId"] = receipt["sourceAuthorityId"]!.DeepClone(),
            ["coordinatesAtCreation"] = canonical["coordinates"]!.DeepClone(),
            ["state"] = "active",
            ["transitions"] = new JsonArray()
        };
    }

    private static JsonObject CreateLinkIndexEntry(JsonObject canonical)
    {
        var receipt = canonical[MortalLocationMaterializationContract.ReceiptProperty]!.AsObject();
        return new JsonObject
        {
            ["linkId"] = canonical["linkId"]!.DeepClone(),
            ["initialId"] = receipt["initialId"]!.DeepClone(),
            ["materializationId"] = receipt["materializationId"]!.DeepClone(),
            ["receiptId"] = receipt["receiptId"]!.DeepClone(),
            ["realm"] = "mortal_world",
            ["route"] = receipt["route"]!.DeepClone(),
            ["sourceTurn"] = receipt["sourceTurn"]!.DeepClone(),
            ["sourceAuthorityKind"] = receipt["sourceAuthorityKind"]!.DeepClone(),
            ["sourceAuthorityId"] = receipt["sourceAuthorityId"]!.DeepClone(),
            ["sourceLocationId"] = canonical["sourceLocationId"]!.DeepClone(),
            ["targetLocationId"] = canonical["targetLocationId"]!.DeepClone(),
            ["state"] = "active",
            ["transitions"] = new JsonArray()
        };
    }

    private static JsonObject CreateCurrentProjection(JsonObject canonical, JsonObject rawCurrent)
    {
        var projection = canonical.DeepClone().AsObject();
        foreach (var field in CurrentOperationalFields)
        {
            if (rawCurrent[field] != null)
                projection[field] = rawCurrent[field]!.DeepClone();
        }
        if (rawCurrent["locationStorages"] is JsonArray rawStorages &&
            projection["locationStorages"] is JsonArray projectedStorages)
        {
            var rawById = rawStorages.OfType<JsonObject>()
                .Where(static storage => ReadExactString(storage, "storageId") != null)
                .ToDictionary(static storage => ReadExactString(storage, "storageId")!, StringComparer.Ordinal);
            foreach (var storage in projectedStorages.OfType<JsonObject>())
            {
                var storageId = ReadExactString(storage, "storageId");
                if (storageId != null && rawById.TryGetValue(storageId, out var rawStorage) &&
                    rawStorage["contents"] is JsonArray contents)
                {
                    storage["contents"] = contents.DeepClone();
                }
            }
        }
        return projection;
    }

    private static JsonObject CreateExistingProjectionSource(
        JsonObject? preTurnCurrent,
        ExistingSelection selection)
    {
        var source = selection.RawSelection.DeepClone().AsObject();
        var selectedLocationId = ReadExactString(selection.RawSelection, "locationId");
        if (preTurnCurrent == null ||
            selectedLocationId == null ||
            !string.Equals(
                ReadExactString(preTurnCurrent, "locationId"),
                selectedLocationId,
                StringComparison.Ordinal) ||
            preTurnCurrent["locationStorages"] is not JsonArray preTurnStorages)
        {
            return source;
        }

        source["locationStorages"] = preTurnStorages.DeepClone();
        return source;
    }

    private static JsonObject ReconcileLocationStorageContents(
        JsonObject finalWorldMap,
        JsonObject? preTurnCurrent,
        JsonObject? preTurnOffscreen,
        string? selectedLocationId,
        JsonObject? finalCurrent,
        List<ValidationIssue> issues)
    {
        var parsed = MortalLocationStorageContentsState.Parse(preTurnOffscreen);
        issues.AddRange(parsed.Issues);
        if (parsed.Issues.Count != 0)
            return parsed.Root;

        var entries = parsed.Entries.ToDictionary(
            static pair => pair.Key,
            static pair => pair.Value.DeepClone().AsArray());
        var preTurnLocationId = ReadExactString(preTurnCurrent, "locationId");
        issues.AddRange(MortalLocationStorageContentsState.ValidateCoordinates(
            entries,
            finalWorldMap,
            preTurnLocationId));
        if (issues.Count != 0)
            return parsed.Root;

        var storageMetadata = new HashSet<MortalLocationStorageKey>();
        foreach (var location in finalWorldMap["locations"]!
                     .AsArray()
                     .OfType<JsonObject>())
        {
            var locationId = ReadExactString(location, "locationId");
            if (locationId == null || location["locationStorages"] is not JsonArray storages)
                continue;
            foreach (var storage in storages.OfType<JsonObject>())
            {
                var storageId = ReadExactString(storage, "storageId");
                if (storageId != null)
                {
                    storageMetadata.Add(
                        new MortalLocationStorageKey(locationId, storageId));
                }
            }
        }

        var selectionChanged = preTurnLocationId != null &&
                               selectedLocationId != null &&
                               !string.Equals(
                                   preTurnLocationId,
                                   selectedLocationId,
                                   StringComparison.Ordinal);
        if (selectionChanged && preTurnCurrent?["locationStorages"] is JsonArray sourceStorages)
        {
            foreach (var storage in sourceStorages.OfType<JsonObject>())
            {
                var storageId = ReadExactString(storage, "storageId");
                if (storageId == null || storage["contents"] is not JsonArray contents || contents.Count == 0)
                    continue;

                var key = new MortalLocationStorageKey(preTurnLocationId!, storageId);
                if (!storageMetadata.Contains(key) || entries.ContainsKey(key))
                {
                    issues.Add(Issue(
                        MortalLocationStorageContentsState.StatePath,
                        "mortal_location_storage_contents_source_ambiguous",
                        "Current storage contents can be parked only at one exact active metadata coordinate.",
                        "one exact empty offscreen coordinate",
                        $"{key.LocationId}/{key.StorageId}"));
                    continue;
                }
                entries.Add(key, contents.DeepClone().AsArray());
            }
        }

        if (selectionChanged &&
            selectedLocationId != null &&
            finalCurrent?["locationStorages"] is JsonArray targetStorages)
        {
            foreach (var storage in targetStorages.OfType<JsonObject>())
            {
                var storageId = ReadExactString(storage, "storageId");
                if (storageId == null)
                    continue;
                var key = new MortalLocationStorageKey(selectedLocationId, storageId);
                storage["contents"] = entries.Remove(key, out var contents)
                    ? contents.DeepClone()
                    : new JsonArray();
            }
        }

        if (issues.Count != 0)
            return parsed.Root;
        return MortalLocationStorageContentsState.BuildCanonicalRoot(entries);
    }

    private static void StripStorageContents(JsonObject location)
    {
        if (location["locationStorages"] is not JsonArray storages)
            return;
        foreach (var storage in storages.OfType<JsonObject>())
            storage.Remove("contents");
    }

    private static IReadOnlyList<MortalLocationStorageCoordinate> BuildAcceptedStorageCoordinates(
        JsonObject? current,
        IReadOnlyDictionary<string, string> locationIdsByInitialId)
    {
        if (current == null ||
            !TryGetExactIdentity(current, "locationId", out var locationId) ||
            current["locationStorages"] is not JsonArray storages)
        {
            return Array.Empty<MortalLocationStorageCoordinate>();
        }

        var initialLocationId = locationIdsByInitialId
            .Where(pair => string.Equals(
                pair.Value,
                locationId,
                StringComparison.Ordinal))
            .Select(static pair => pair.Key)
            .SingleOrDefault();
        return storages.OfType<JsonObject>()
            .Select(static storage => ReadExactString(storage, "storageId"))
            .Where(static storageId => storageId != null)
            .Cast<string>()
            .Distinct(StringComparer.Ordinal)
            .Select(storageId => new MortalLocationStorageCoordinate(
                locationId,
                storageId,
                initialLocationId))
            .ToArray();
    }

    private static void ValidateComposedState(
        JsonObject worldMap,
        JsonObject identityIndex,
        JsonObject? current,
        List<ValidationIssue> issues)
    {
        var locations = worldMap["locations"]!.AsArray();
        var links = worldMap["links"]!.AsArray();
        for (var index = 0; index < locations.Count; index++)
        {
            using var document = JsonDocument.Parse(locations[index]!.ToJsonString());
            issues.AddRange(MortalLocationMaterializationContract.ValidateCanonicalLocation(
                document.RootElement,
                $"{MortalLocationMaterializationContract.WorldMapPath}.locations[{index}]"));
        }
        for (var index = 0; index < links.Count; index++)
        {
            using var document = JsonDocument.Parse(links[index]!.ToJsonString());
            issues.AddRange(MortalLocationMaterializationContract.ValidateCanonicalLink(
                document.RootElement,
                $"{MortalLocationMaterializationContract.WorldMapPath}.links[{index}]"));
        }
        var parsedIndex = MortalLocationIdentityState.Parse(identityIndex);
        issues.AddRange(parsedIndex.Issues);
        issues.AddRange(parsedIndex.ValidateCanonicalState(worldMap));

        if (current != null && TryGetExactIdentity(current, "locationId", out var currentId))
        {
            var mapLocation = locations.OfType<JsonObject>()
                .SingleOrDefault(location => string.Equals(ReadExactString(location, "locationId"), currentId, StringComparison.Ordinal));
            if (mapLocation == null)
            {
                issues.Add(Issue(
                    MortalLocationMaterializationContract.CurrentLocationPath,
                    "mortal_location_materialization_current_projection_mismatch",
                    "Current projection must select one canonical map location.",
                    "exact canonical locationId",
                    currentId));
            }
        }
    }

    private static IReadOnlyDictionary<string, JsonObject> BuildExactObjectIndex(
        JsonArray values,
        string identityField,
        List<ValidationIssue> issues)
    {
        var result = new Dictionary<string, JsonObject>(StringComparer.Ordinal);
        foreach (var value in values.OfType<JsonObject>())
        {
            if (!TryGetExactIdentity(value, identityField, out var identity) || !result.TryAdd(identity, value))
            {
                issues.Add(Issue(
                    MortalLocationMaterializationContract.WorldMapPath,
                    "mortal_location_materialization_duplicate_canonical_identity",
                    "Canonical location/link identities must be exact and unique.",
                    "one exact identity",
                    identityField));
            }
        }
        return result;
    }

    private static bool TryReadCanonicalMap(
        JsonObject root,
        out JsonArray locations,
        out JsonArray links)
    {
        locations = root["locations"] as JsonArray ?? new JsonArray();
        links = root["links"] as JsonArray ?? new JsonArray();
        return root.Count == 4 &&
               root["schemaVersion"] is JsonValue schema && schema.TryGetValue<int>(out var version) && version == 1 &&
               string.Equals(ReadExactString(root, "realm"), "mortal_world", StringComparison.Ordinal) &&
               root["locations"] is JsonArray &&
               root["links"] is JsonArray;
    }

    private static JsonObject Unwrap(JsonObject root, string wrapper)
    {
        return root[wrapper] is JsonObject value ? value : root;
    }

    private static bool TryReadCoordinateKey(JsonObject location, out string key)
    {
        key = string.Empty;
        if (location["coordinates"] is not JsonObject coordinates ||
            ReadInt(coordinates, "x") is not int x ||
            ReadInt(coordinates, "y") is not int y ||
            ReadInt(coordinates, "z") is not int z)
        {
            return false;
        }
        key = $"{x}\u001f{y}\u001f{z}";
        return true;
    }

    private static bool TryGetExactIdentity(JsonObject root, string field, out string identity)
    {
        identity = ReadExactString(root, field) ?? string.Empty;
        return identity.Length > 0;
    }

    private static string? ReadExactString(JsonObject? root, string field)
    {
        if (root?[field] is not JsonValue value || !value.TryGetValue<string>(out var text) ||
            string.IsNullOrEmpty(text) || !string.Equals(text, text.Trim(), StringComparison.Ordinal))
        {
            return null;
        }
        return text;
    }

    private static string? ReadStringScalar(JsonObject? root, string field)
    {
        return root?[field] is JsonValue value &&
               value.TryGetValue<string>(out var text) &&
               !string.IsNullOrEmpty(text)
            ? text
            : null;
    }

    private static int? ReadInt(JsonObject? root, string field)
    {
        return root?[field] is JsonValue value && value.TryGetValue<int>(out var result)
            ? result
            : null;
    }

    private static MortalLocationAcceptedTurnPlanningResult Failed(List<ValidationIssue> issues) =>
        new(null, issues.ToArray());

    private static ValidationIssue Issue(
        string path,
        string code,
        string message,
        string expected,
        string actual) =>
        new(
            path,
            IssueSeverity.Error,
            message,
            code: code,
            section: "mortal_location_materialization",
            expected: expected,
            actual: actual,
            repairHint: "Correct only the exact GM-owned carrier named by this issue.");

    private sealed record LocationCandidate(
        JsonObject Raw,
        string Context,
        string ExpectedRoute,
        bool SelectCurrent);

    private sealed record LinkCandidate(JsonObject Raw, string Context);

    private sealed record ExistingSelection(JsonObject RawSelection, JsonObject CanonicalLocation);

    private sealed record LocationUpdate(JsonObject Raw, string Context, string LocationId);

    private sealed record LocationDiscoveryTransition(
        JsonObject Raw,
        string Context,
        string LocationId,
        string? FromTier,
        string? ToTier,
        string? Audience);

    private sealed record LinkUpdate(JsonObject Raw, string Context, string LinkId);

    private sealed record LinkRemoval(JsonObject Raw, string Context, string LinkId);

    private sealed record ActorPlacement(
        string? PermanentLocationId,
        string? InitialLocationId)
    {
        internal string Describe() => PermanentLocationId != null
            ? "currentLocationId=" + PermanentLocationId
            : "initialLocationId=" + (InitialLocationId ?? "missing");
    }

    private sealed class ActorBindingAuthority
    {
        private readonly ActorBindingIndex _permanent = new();
        private readonly ActorBindingIndex _temporary = new();

        internal void AddPermanent(string identity, ActorPlacement placement) =>
            _permanent.Add(identity, placement);

        internal void AddTemporary(string identity, ActorPlacement placement) =>
            _temporary.Add(identity, placement);

        internal MortalLocationReferenceResolution Resolve(
            string identity,
            bool temporary,
            out ActorPlacement? placement) =>
            (temporary ? _temporary : _permanent).Resolve(identity, out placement);

        internal MortalLocationReferenceResolution ResolveEffective(
            string identity,
            out ActorPlacement? placement)
        {
            var permanentResolution = _permanent.Resolve(identity, out var permanentPlacement);
            var temporaryResolution = _temporary.Resolve(identity, out var temporaryPlacement);
            if (permanentResolution == MortalLocationReferenceResolution.Exact &&
                temporaryResolution == MortalLocationReferenceResolution.Missing)
            {
                placement = permanentPlacement;
                return MortalLocationReferenceResolution.Exact;
            }
            if (temporaryResolution == MortalLocationReferenceResolution.Exact &&
                permanentResolution == MortalLocationReferenceResolution.Missing)
            {
                placement = temporaryPlacement;
                return MortalLocationReferenceResolution.Exact;
            }

            placement = null;
            if (permanentResolution == MortalLocationReferenceResolution.Ambiguous ||
                temporaryResolution == MortalLocationReferenceResolution.Ambiguous ||
                permanentResolution == MortalLocationReferenceResolution.Exact ||
                temporaryResolution == MortalLocationReferenceResolution.Exact)
            {
                return MortalLocationReferenceResolution.Ambiguous;
            }
            if (permanentResolution == MortalLocationReferenceResolution.Confusable ||
                temporaryResolution == MortalLocationReferenceResolution.Confusable)
            {
                return MortalLocationReferenceResolution.Confusable;
            }
            return MortalLocationReferenceResolution.Missing;
        }
    }

    private sealed class ActorBindingIndex
    {
        private readonly Dictionary<string, HashSet<ActorPlacement>> _placementsByIdentity =
            new(StringComparer.Ordinal);
        private readonly Dictionary<string, HashSet<string>> _identitiesByConfusableKey =
            new(StringComparer.Ordinal);

        internal void Add(string identity, ActorPlacement placement)
        {
            if (!_placementsByIdentity.TryGetValue(identity, out var placements))
            {
                placements = [];
                _placementsByIdentity.Add(identity, placements);
            }
            placements.Add(placement);

            var key = MortalLocationIdentityState.BuildConfusableKey(identity);
            if (!_identitiesByConfusableKey.TryGetValue(key, out var identities))
            {
                identities = new HashSet<string>(StringComparer.Ordinal);
                _identitiesByConfusableKey.Add(key, identities);
            }
            identities.Add(identity);
        }

        internal MortalLocationReferenceResolution Resolve(
            string identity,
            out ActorPlacement? placement)
        {
            placement = null;
            var key = MortalLocationIdentityState.BuildConfusableKey(identity);
            _identitiesByConfusableKey.TryGetValue(key, out var confusableIdentities);
            if (_placementsByIdentity.TryGetValue(identity, out var placements))
            {
                if (placements.Count == 1 && confusableIdentities?.Count == 1)
                {
                    placement = placements.Single();
                    return MortalLocationReferenceResolution.Exact;
                }
                return MortalLocationReferenceResolution.Ambiguous;
            }

            return confusableIdentities is { Count: > 0 }
                ? MortalLocationReferenceResolution.Confusable
                : MortalLocationReferenceResolution.Missing;
        }
    }

    private sealed class FactionControlAuthority
    {
        private readonly ExactIdentityIndex _permanent = new();
        private readonly ExactIdentityIndex _temporary = new();

        internal void AddPermanent(string identity) => _permanent.Add(identity);

        internal void AddTemporary(string identity) => _temporary.Add(identity);

        internal MortalLocationReferenceResolution Resolve(
            string identity,
            bool temporary) =>
            (temporary ? _temporary : _permanent).Resolve(identity);

        internal MortalLocationReferenceResolution ResolveEffective(string identity)
        {
            var permanentResolution = _permanent.Resolve(identity);
            var temporaryResolution = _temporary.Resolve(identity);
            if (permanentResolution == MortalLocationReferenceResolution.Exact &&
                temporaryResolution == MortalLocationReferenceResolution.Missing ||
                temporaryResolution == MortalLocationReferenceResolution.Exact &&
                permanentResolution == MortalLocationReferenceResolution.Missing)
            {
                return MortalLocationReferenceResolution.Exact;
            }

            if (permanentResolution == MortalLocationReferenceResolution.Ambiguous ||
                temporaryResolution == MortalLocationReferenceResolution.Ambiguous ||
                permanentResolution == MortalLocationReferenceResolution.Exact ||
                temporaryResolution == MortalLocationReferenceResolution.Exact)
            {
                return MortalLocationReferenceResolution.Ambiguous;
            }

            return permanentResolution == MortalLocationReferenceResolution.Confusable ||
                   temporaryResolution == MortalLocationReferenceResolution.Confusable
                ? MortalLocationReferenceResolution.Confusable
                : MortalLocationReferenceResolution.Missing;
        }
    }

    private sealed class ExactIdentityIndex
    {
        private readonly Dictionary<string, int> _countsByIdentity =
            new(StringComparer.Ordinal);
        private readonly Dictionary<string, HashSet<string>> _identitiesByConfusableKey =
            new(StringComparer.Ordinal);

        internal void Add(string identity)
        {
            _countsByIdentity.TryGetValue(identity, out var count);
            _countsByIdentity[identity] = count + 1;

            var key = MortalLocationIdentityState.BuildConfusableKey(identity);
            if (!_identitiesByConfusableKey.TryGetValue(key, out var identities))
            {
                identities = new HashSet<string>(StringComparer.Ordinal);
                _identitiesByConfusableKey.Add(key, identities);
            }
            identities.Add(identity);
        }

        internal MortalLocationReferenceResolution Resolve(string identity)
        {
            var key = MortalLocationIdentityState.BuildConfusableKey(identity);
            _identitiesByConfusableKey.TryGetValue(key, out var confusableIdentities);
            if (_countsByIdentity.TryGetValue(identity, out var count))
            {
                return count == 1 && confusableIdentities?.Count == 1
                    ? MortalLocationReferenceResolution.Exact
                    : MortalLocationReferenceResolution.Ambiguous;
            }

            return confusableIdentities is { Count: > 0 }
                ? MortalLocationReferenceResolution.Confusable
                : MortalLocationReferenceResolution.Missing;
        }
    }

    private sealed record BootstrapPlanningContext(
        MortalBootstrapLocationReservationSet Reservations,
        string Branch,
        IReadOnlyDictionary<string, string> ReservedLocationIdsByInitialId,
        IReadOnlyDictionary<string, string> ReservedLinkIdsByInitialId);
}
