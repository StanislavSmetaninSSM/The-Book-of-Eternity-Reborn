using System.Text.Json;
using System.Text.Json.Nodes;

namespace BookOfEternityClient.Services;

internal static class MortalLocationAcceptedTurnPlanner
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

    private static readonly string[] CurrentOperationalFields =
    {
        "currentWeather",
        "currentInteractions",
        "currentChronology"
    };

    internal static MortalLocationAcceptedTurnPlanningResult Build(
        MortalLocationAcceptedTurnInput input,
        MortalLocationIdentityFactory? identityFactory = null)
    {
        ArgumentNullException.ThrowIfNull(input);
        identityFactory ??= new MortalLocationIdentityFactory();
        var issues = new List<ValidationIssue>();
        var preTurnWorldMap = input.PreTurnWorldMap.DeepClone().AsObject();
        var preTurnIndex = MortalLocationIdentityState.Parse(input.PreTurnIdentityIndex);
        issues.AddRange(preTurnIndex.Issues);

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
                existingSelection = ValidateExistingSelection(current, existingId, preTurnLocationsById, issues);
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

        ValidateLocationCandidates(locationCandidates, preTurnLocations, preTurnIndex, input.Turn, issues);
        ValidateParentGraph(locationCandidates, issues);

        var linkCandidates = ReadLinkCandidates(updates, preTurnIndex, input.Turn, issues);
        ValidateRawLinkEndpoints(linkCandidates, preTurnLocationsById, locationCandidates, issues);

        BootstrapPlanningContext? bootstrap = null;
        if (input.BootstrapScaffold != null)
        {
            if (!MortalBootstrapLocationScaffold.TryReadRequest(
                    input.BootstrapScaffold,
                    out var reservations,
                    out var scaffoldError))
            {
                issues.Add(Issue(
                    MortalBootstrapLocationScaffold.StatePath + ".locationMaterializationRequest",
                    "mortal_bootstrap_location_scaffold_invalid",
                    "The client-owned Mortal bootstrap location request is malformed.",
                    "exact current bootstrap location request",
                    scaffoldError));
            }
            else
            {
                bootstrap = ValidateBootstrapReservations(
                    reservations,
                    locationCandidates,
                    linkCandidates,
                    input.Turn,
                    issues);
            }
        }

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
            finalCurrent = CreateCurrentProjection(existingSelection.CanonicalLocation, existingSelection.RawSelection);

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

        ValidateComposedState(finalWorldMap, finalIdentityIndex, finalCurrent, issues);
        if (issues.Count != 0)
            return Failed(issues);

        var storageCoordinates = BuildAcceptedStorageCoordinates(finalCurrent);
        var touchedPaths = new List<string>();
        if (locationCandidates.Count > 0 || linkCandidates.Count > 0)
        {
            touchedPaths.Add(MortalLocationMaterializationContract.WorldMapPath);
            touchedPaths.Add(MortalLocationIdentityState.StatePath);
        }
        if (input.RawCurrentLocationData != null)
            touchedPaths.Add(MortalLocationMaterializationContract.CurrentLocationPath);

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
            locationIdsByInitialId,
            linkIdsByInitialId,
            storageCoordinates,
            Array.Empty<MortalLocationGovernedRewrite>(),
            touchedPaths.Distinct(StringComparer.Ordinal).ToArray(),
            Array.Empty<MortalLocationRepairContext>(),
            finalBootstrapScaffold);
        return new MortalLocationAcceptedTurnPlanningResult(plan, Array.Empty<ValidationIssue>());
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

        issues.AddRange(identityState.ValidateCanonicalState(worldMap));
    }

    private static void ValidateLocationCandidates(
        IReadOnlyList<LocationCandidate> candidates,
        JsonArray preTurnLocations,
        MortalLocationIdentityState identityState,
        int turn,
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
        List<ValidationIssue> issues)
    {
        var parents = new Dictionary<string, string>(StringComparer.Ordinal);
        var candidateIds = candidates
            .Select(candidate => ReadExactString(candidate.Raw, "initialId"))
            .Where(static value => value != null)
            .Cast<string>()
            .ToHashSet(StringComparer.Ordinal);
        foreach (var candidate in candidates)
        {
            var initialId = ReadExactString(candidate.Raw, "initialId");
            var parentInitialId = ReadExactString(candidate.Raw, "parentInitialId");
            if (initialId == null || parentInitialId == null)
                continue;
            if (!candidateIds.Contains(parentInitialId))
            {
                issues.Add(Issue(
                    candidate.Context + ".parentInitialId",
                    "mortal_location_materialization_parent_unresolved",
                    "Same-turn parentInitialId must resolve to one accepted location candidate.",
                    "exact same-turn initialId",
                    parentInitialId));
                continue;
            }
            parents[initialId] = parentInitialId;
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

    private static void ValidateRawLinkEndpoints(
        IReadOnlyList<LinkCandidate> links,
        IReadOnlyDictionary<string, JsonObject> preTurnLocationsById,
        IReadOnlyList<LocationCandidate> locationCandidates,
        List<ValidationIssue> issues)
    {
        var sameTurnInitialIds = locationCandidates
            .Select(candidate => ReadExactString(candidate.Raw, "initialId"))
            .Where(static value => value != null)
            .Cast<string>()
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
                issues.Add(Issue(
                    candidate.Context + "." + endpoint,
                    "mortal_location_link_endpoint_unresolved",
                    "Each link endpoint must resolve exactly once to pre-turn or same-turn location authority.",
                    "exact active locationId or accepted initialId",
                    permanent ?? temporary ?? "missing"));
            }
        }
    }

    private static ExistingSelection? ValidateExistingSelection(
        JsonObject raw,
        string existingId,
        IReadOnlyDictionary<string, JsonObject> preTurnLocationsById,
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
        return new ExistingSelection(raw.DeepClone().AsObject(), canonical.DeepClone().AsObject());
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

    private static void StripStorageContents(JsonObject location)
    {
        if (location["locationStorages"] is not JsonArray storages)
            return;
        foreach (var storage in storages.OfType<JsonObject>())
            storage.Remove("contents");
    }

    private static IReadOnlyList<MortalLocationStorageCoordinate> BuildAcceptedStorageCoordinates(
        JsonObject? current)
    {
        if (current == null ||
            !TryGetExactIdentity(current, "locationId", out var locationId) ||
            current["locationStorages"] is not JsonArray storages)
        {
            return Array.Empty<MortalLocationStorageCoordinate>();
        }

        return storages.OfType<JsonObject>()
            .Select(static storage => ReadExactString(storage, "storageId"))
            .Where(static storageId => storageId != null)
            .Cast<string>()
            .Distinct(StringComparer.Ordinal)
            .Select(storageId => new MortalLocationStorageCoordinate(locationId, storageId))
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

    private sealed record BootstrapPlanningContext(
        MortalBootstrapLocationReservationSet Reservations,
        string Branch,
        IReadOnlyDictionary<string, string> ReservedLocationIdsByInitialId,
        IReadOnlyDictionary<string, string> ReservedLinkIdsByInitialId);
}
