using System.Text.Json;
using System.Text.Json.Nodes;
using BookOfEternityClient.Core;

namespace BookOfEternityClient.Services;

public partial class ValidationService
{
    public async Task<IReadOnlyList<ValidationIssue>>
        ValidateAcceptedTurnCanonicalMortalLocationMaterializationAsync()
    {
        var issues = new List<ValidationIssue>();
        await ValidateAcceptedTurnCanonicalMortalLocationMaterializationAsync(
            issues,
            writeLease: null);
        return issues;
    }

    internal async Task<IReadOnlyList<ValidationIssue>>
        ValidateAcceptedTurnCanonicalMortalLocationMaterializationAsync(
            FileSystemManager.CanonicalWriteLease writeLease)
    {
        ArgumentNullException.ThrowIfNull(writeLease);
        var issues = new List<ValidationIssue>();
        await ValidateAcceptedTurnCanonicalMortalLocationMaterializationAsync(
            issues,
            writeLease);
        return issues;
    }

    public async Task<IReadOnlyList<ValidationIssue>>
        ValidateAcceptedTurnRawMortalLocationMaterializationAsync()
    {
        var issues = new List<ValidationIssue>();
        await ValidateAcceptedTurnRawMortalLocationFileAsync(
            MortalLocationMaterializationContract.CurrentLocationPath,
            validateCurrentLocation: true,
            issues);
        await ValidateAcceptedTurnRawMortalLocationFileAsync(
            MortalLocationMaterializationContract.WorldMapPath,
            validateCurrentLocation: false,
            issues);
        await ValidateRawMortalBootstrapLocationReservationsAsync(issues);
        return issues;
    }

    private async Task ValidateRawMortalBootstrapLocationReservationsAsync(
        List<ValidationIssue> issues)
    {
        var scaffoldJson = await _fs.ReadFileAsync(MortalBootstrapLocationScaffold.StatePath);
        if (string.IsNullOrWhiteSpace(scaffoldJson))
            return;

        JsonObject? scaffoldRoot;
        JsonObject? request;
        try
        {
            scaffoldRoot = JsonNode.Parse(scaffoldJson) as JsonObject;
            request = scaffoldRoot?["locationMaterializationRequest"] as JsonObject;
        }
        catch (JsonException exception)
        {
            issues.Add(LocationIssue(
                MortalBootstrapLocationScaffold.StatePath,
                "mortal_bootstrap_location_scaffold_invalid",
                "The client-owned Mortal bootstrap scaffold is not readable object JSON.",
                "exact client scaffold",
                exception.Message));
            return;
        }

        if (request == null)
            return;
        if (!MortalBootstrapLocationScaffold.TryReadRequest(
                request,
                out var reservationSet,
                out var scaffoldError))
        {
            issues.Add(LocationIssue(
                MortalBootstrapLocationScaffold.StatePath + ".locationMaterializationRequest",
                "mortal_bootstrap_location_scaffold_invalid",
                "The client-owned Mortal bootstrap location request is malformed.",
                "exact current bootstrap location request",
                scaffoldError));
            return;
        }

        var currentRoot = await ReadOptionalLocationObjectAsync(
            MortalLocationMaterializationContract.CurrentLocationPath);
        var mapRoot = await ReadOptionalLocationObjectAsync(
            MortalLocationMaterializationContract.WorldMapPath);
        var hasRawCommands = currentRoot?["currentLocationData"] is JsonObject ||
                             mapRoot?["worldMapUpdates"] is JsonObject;

        var lookup = await LoadValidatedPendingTurnSnapshotLookupAsync();
        if (lookup.Status != ValidatedPendingTurnSnapshotStatus.Usable ||
            lookup.Manifest == null)
        {
            if (hasRawCommands || reservationSet.State == "pending")
            {
                issues.Add(LocationIssue(
                    MortalBootstrapLocationScaffold.StatePath,
                    "mortal_bootstrap_location_snapshot_required",
                    "Bootstrap location reservations require a validated pending-turn snapshot.",
                    "tracked neutral map/current/index/scaffold baseline",
                    DescribeValidatedPendingTurnSnapshotStatus(lookup.Status)));
            }
            return;
        }

        var preMap = ParseOptionalLocationObject(
            await ReadValidatedPendingTurnSnapshotFileAsync(
                lookup.Manifest,
                MortalLocationMaterializationContract.WorldMapPath));
        var preCurrent = ParseOptionalLocationObject(
            await ReadValidatedPendingTurnSnapshotFileAsync(
                lookup.Manifest,
                MortalLocationMaterializationContract.CurrentLocationPath));
        var preIndex = ParseOptionalLocationObject(
            await ReadValidatedPendingTurnSnapshotFileAsync(
                lookup.Manifest,
                MortalLocationIdentityState.StatePath));
        var preScaffoldRoot = ParseOptionalLocationObject(
            await ReadValidatedPendingTurnSnapshotFileAsync(
                lookup.Manifest,
                MortalBootstrapLocationScaffold.StatePath));
        if (preMap == null || preCurrent == null || preIndex == null || preScaffoldRoot == null)
        {
            issues.Add(LocationIssue(
                MortalBootstrapLocationScaffold.StatePath,
                "mortal_bootstrap_location_snapshot_required",
                "Bootstrap location reservations require all four readable tracked baseline files.",
                "world map, current location, location index, scaffold",
                "one or more snapshot files missing"));
            return;
        }

        if (hasRawCommands &&
            reservationSet.State == "pending" &&
            !JsonNode.DeepEquals(scaffoldRoot, preScaffoldRoot))
        {
            issues.Add(LocationIssue(
                MortalBootstrapLocationScaffold.StatePath,
                "mortal_bootstrap_location_scaffold_mutated",
                "The GM cannot mutate the client-owned bootstrap reservation request.",
                "byte-equivalent pre-turn scaffold semantics",
                "current scaffold differs from validated snapshot"));
            return;
        }

        var planningResult = MortalLocationAcceptedTurnPlanner.Build(
            new MortalLocationAcceptedTurnInput(
                preMap,
                preCurrent,
                preIndex,
                currentRoot?["currentLocationData"] is JsonObject ? currentRoot : null,
                mapRoot?["worldMapUpdates"] is JsonObject ? mapRoot : null,
                lookup.Manifest.TurnNumber,
                request));
        foreach (var issue in planningResult.Issues)
        {
            if (issue.Code?.StartsWith("mortal_bootstrap_location_", StringComparison.Ordinal) == true ||
                string.Equals(
                    issue.Code,
                    "mortal_location_materialization_duplicate_creation_route",
                    StringComparison.Ordinal))
            {
                issues.Add(issue);
            }
        }
    }

    private async Task<JsonObject?> ReadOptionalLocationObjectAsync(string path) =>
        ParseOptionalLocationObject(await _fs.ReadFileAsync(path));

    private static JsonObject? ParseOptionalLocationObject(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
            return null;
        try
        {
            return JsonNode.Parse(json) as JsonObject;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private async Task ValidateAcceptedTurnCanonicalMortalLocationMaterializationAsync(
        List<ValidationIssue> issues)
        => await ValidateAcceptedTurnCanonicalMortalLocationMaterializationAsync(
            issues,
            writeLease: null);

    private async Task ValidateAcceptedTurnCanonicalMortalLocationMaterializationAsync(
        List<ValidationIssue> issues,
        FileSystemManager.CanonicalWriteLease? writeLease)
    {
        var mapJson = ShouldValidateStateFile(MortalLocationMaterializationContract.WorldMapPath)
            ? await ReadMortalLocationValidationFileAsync(
                MortalLocationMaterializationContract.WorldMapPath,
                writeLease)
            : null;
        var currentJson = ShouldValidateStateFile(MortalLocationMaterializationContract.CurrentLocationPath)
            ? await ReadMortalLocationValidationFileAsync(
                MortalLocationMaterializationContract.CurrentLocationPath,
                writeLease)
            : null;
        var indexJson = ShouldValidateStateFile(MortalLocationIdentityState.StatePath)
            ? await ReadMortalLocationValidationFileAsync(
                MortalLocationIdentityState.StatePath,
                writeLease)
            : null;
        var scaffoldJson = await ReadMortalLocationValidationFileAsync(
            MortalBootstrapLocationScaffold.StatePath,
            writeLease);

        if (string.IsNullOrWhiteSpace(mapJson) &&
            string.IsNullOrWhiteSpace(currentJson) &&
            string.IsNullOrWhiteSpace(indexJson))
        {
            return;
        }

        var map = ParseLocationStateObject(
            mapJson,
            MortalLocationMaterializationContract.WorldMapPath,
            issues);
        var identityState = MortalLocationIdentityState.Parse(indexJson);
        issues.AddRange(identityState.Issues);
        if (map == null)
            return;

        if (!TryReadCanonicalWorldMap(map, out var locations, out var links))
        {
            issues.Add(LocationIssue(
                MortalLocationMaterializationContract.WorldMapPath,
                "mortal_location_materialization_invalid_world_map",
                "Mortal world map must have the exact canonical root.",
                "schemaVersion=1, realm=mortal_world, locations[], links[]",
                map.ToJsonString()));
            return;
        }

        ValidateCanonicalLocationArray(locations, links: false, issues);
        ValidateCanonicalLocationArray(links, links: true, issues);
        issues.AddRange(identityState.ValidateCanonicalState(map));

        if (!string.IsNullOrWhiteSpace(currentJson))
        {
            var current = ParseLocationStateObject(
                currentJson,
                MortalLocationMaterializationContract.CurrentLocationPath,
                issues);
            if (current != null)
            {
                if (IsPendingMortalLocationRoot(current))
                {
                    if (locations.Count != 0 ||
                        links.Count != 0 ||
                        identityState.LocationEntriesById.Count != 0 ||
                        identityState.LinkEntriesById.Count != 0)
                    {
                        issues.Add(LocationIssue(
                            MortalLocationMaterializationContract.CurrentLocationPath,
                            "mortal_bootstrap_location_pending_state_mismatch",
                            "Pending current-location state is allowed only with an empty canonical map and identity index.",
                            "empty map/index",
                            $"locations={locations.Count}; links={links.Count}; locationEntries={identityState.LocationEntriesById.Count}; linkEntries={identityState.LinkEntriesById.Count}"));
                    }
                }
                else
                {
                    ValidateCurrentLocationProjection(current, locations, issues);
                }
            }
        }

        ValidateCanonicalMortalBootstrapLocationSettlement(
            scaffoldJson,
            currentJson,
            locations,
            links,
            issues);
    }

    private static void ValidateCanonicalMortalBootstrapLocationSettlement(
        string? scaffoldJson,
        string? currentJson,
        JsonArray locations,
        JsonArray links,
        List<ValidationIssue> issues)
    {
        if (string.IsNullOrWhiteSpace(scaffoldJson))
            return;

        var scaffoldRoot = ParseOptionalLocationObject(scaffoldJson);
        var request = scaffoldRoot?["locationMaterializationRequest"] as JsonObject;
        if (request == null)
            return;
        if (!MortalBootstrapLocationScaffold.TryReadRequest(
                request,
                out var reservations,
                out var error))
        {
            issues.Add(LocationIssue(
                MortalBootstrapLocationScaffold.StatePath + ".locationMaterializationRequest",
                "mortal_bootstrap_location_scaffold_invalid",
                "The client-owned bootstrap location request is malformed.",
                "exact pending or settled request",
                error));
            return;
        }

        var current = ParseOptionalLocationObject(currentJson);
        if (reservations.State == "pending")
        {
            if (current == null ||
                !IsPendingMortalLocationRoot(current) ||
                locations.Count != 0 ||
                links.Count != 0)
            {
                issues.Add(LocationIssue(
                    MortalBootstrapLocationScaffold.StatePath + ".locationMaterializationRequest.state",
                    "mortal_bootstrap_location_pending_state_mismatch",
                    "An open bootstrap request must retain the exact neutral location roots until accepted normalization succeeds.",
                    "pending current root and empty map",
                    "materialized or malformed canonical state"));
            }
            return;
        }

        if (request["settlement"] is not JsonObject settlement ||
            ReadExactLocationNodeString(settlement, "requestId") != reservations.RequestId ||
            ReadLocationNodeInt(settlement, "acceptedTurn") != reservations.TurnNumber)
        {
            issues.Add(LocationIssue(
                MortalBootstrapLocationScaffold.StatePath + ".locationMaterializationRequest.settlement",
                "mortal_bootstrap_location_settlement_invalid",
                "A settled bootstrap request requires exact request and accepted-turn evidence.",
                $"requestId={reservations.RequestId}; acceptedTurn={reservations.TurnNumber}",
                request["settlement"]?.ToJsonString() ?? "missing"));
            return;
        }

        var branch = ReadExactLocationNodeString(settlement, "branch");
        var startLocationId = ReadExactLocationNodeString(settlement, "startLocationId");
        var start = FindExactBootstrapLocation(
            locations,
            startLocationId,
            reservations.Start,
            reservations);
        if (start == null ||
            current == null ||
            !string.Equals(
                ReadExactLocationNodeString(current, "locationId"),
                startLocationId,
                StringComparison.Ordinal))
        {
            issues.Add(LocationIssue(
                MortalBootstrapLocationScaffold.StatePath + ".locationMaterializationRequest.settlement.startLocationId",
                "mortal_bootstrap_location_settlement_invalid",
                "Settled bootstrap start must resolve to the exact accepted current canonical location and receipt authority.",
                reservations.Start.ReservedLocationId,
                startLocationId ?? "missing"));
        }

        if (branch == MortalBootstrapLocationScaffold.NarrativeOnlyBranch)
        {
            if (settlement["neighborLocationId"] != null || settlement["linkId"] != null)
            {
                issues.Add(LocationIssue(
                    MortalBootstrapLocationScaffold.StatePath + ".locationMaterializationRequest.settlement",
                    "mortal_bootstrap_location_settlement_invalid",
                    "Narrative-only bootstrap completion must not settle neighbor or link identity.",
                    "neighborLocationId=null and linkId=null",
                    settlement.ToJsonString()));
            }
            return;
        }

        if (branch != MortalBootstrapLocationScaffold.MaterializedNeighborBranch)
        {
            issues.Add(LocationIssue(
                MortalBootstrapLocationScaffold.StatePath + ".locationMaterializationRequest.settlement.branch",
                "mortal_bootstrap_location_settlement_invalid",
                "Bootstrap settlement branch is outside the closed completion catalog.",
                $"{MortalBootstrapLocationScaffold.MaterializedNeighborBranch}|{MortalBootstrapLocationScaffold.NarrativeOnlyBranch}",
                branch ?? "missing"));
            return;
        }

        var neighborLocationId = ReadExactLocationNodeString(settlement, "neighborLocationId");
        var linkId = ReadExactLocationNodeString(settlement, "linkId");
        var neighbor = FindExactBootstrapLocation(
            locations,
            neighborLocationId,
            reservations.Neighbor,
            reservations);
        var linkMatches = links.OfType<JsonObject>()
            .Where(candidate =>
                string.Equals(ReadExactLocationNodeString(candidate, "linkId"), linkId, StringComparison.Ordinal) &&
                string.Equals(ReadExactLocationNodeString(candidate, "sourceLocationId"), startLocationId, StringComparison.Ordinal) &&
                string.Equals(ReadExactLocationNodeString(candidate, "targetLocationId"), neighborLocationId, StringComparison.Ordinal) &&
                BootstrapReceiptMatches(
                    candidate,
                    reservations.Link.InitialId,
                    reservations))
            .Take(2)
            .ToArray();
        var link = linkMatches.Length == 1 ? linkMatches[0] : null;
        if (neighbor == null || link == null ||
            neighborLocationId != reservations.Neighbor.ReservedLocationId ||
            linkId != reservations.Link.ReservedLinkId)
        {
            issues.Add(LocationIssue(
                MortalBootstrapLocationScaffold.StatePath + ".locationMaterializationRequest.settlement",
                "mortal_bootstrap_location_settlement_invalid",
                "Materialized bootstrap completion must resolve the exact reserved neighbor and directed link.",
                $"{reservations.Neighbor.ReservedLocationId}; {reservations.Link.ReservedLinkId}",
                settlement.ToJsonString()));
        }
    }

    private static JsonObject? FindExactBootstrapLocation(
        JsonArray locations,
        string? locationId,
        MortalBootstrapLocationReservation reservation,
        MortalBootstrapLocationReservationSet request)
    {
        var matches = locations.OfType<JsonObject>()
            .Where(candidate =>
                string.Equals(ReadExactLocationNodeString(candidate, "locationId"), locationId, StringComparison.Ordinal) &&
                locationId == reservation.ReservedLocationId &&
                BootstrapReceiptMatches(candidate, reservation.InitialId, request))
            .Take(2)
            .ToArray();
        return matches.Length == 1 ? matches[0] : null;
    }

    private static bool BootstrapReceiptMatches(
        JsonObject candidate,
        string initialId,
        MortalBootstrapLocationReservationSet request) =>
        candidate["materializationReceipt"] is JsonObject receipt &&
        string.Equals(ReadExactLocationNodeString(receipt, "initialId"), initialId, StringComparison.Ordinal) &&
        string.Equals(
            ReadExactLocationNodeString(receipt, "sourceAuthorityKind"),
            request.AuthorityKind,
            StringComparison.Ordinal) &&
        string.Equals(
            ReadExactLocationNodeString(receipt, "sourceAuthorityId"),
            request.AuthorityId,
            StringComparison.Ordinal);

    private static bool IsPendingMortalLocationRoot(JsonObject current) =>
        current.Count == 4 &&
        ReadLocationNodeInt(current, "schemaVersion") == 1 &&
        string.Equals(ReadExactLocationNodeString(current, "realm"), "mortal_world", StringComparison.Ordinal) &&
        current.ContainsKey("locationId") &&
        current["locationId"] == null &&
        string.Equals(ReadExactLocationNodeString(current, "state"), "pending_materialization", StringComparison.Ordinal);

    private static string? ReadExactLocationNodeString(JsonObject? root, string field)
    {
        if (root?[field] is not JsonValue value ||
            !value.TryGetValue<string>(out var text) ||
            string.IsNullOrEmpty(text) ||
            !string.Equals(text, text.Trim(), StringComparison.Ordinal))
        {
            return null;
        }
        return text;
    }

    private static int? ReadLocationNodeInt(JsonObject? root, string field) =>
        root?[field] is JsonValue value && value.TryGetValue<int>(out var result)
            ? result
            : null;

    private Task<string?> ReadMortalLocationValidationFileAsync(
        string path,
        FileSystemManager.CanonicalWriteLease? writeLease) =>
        writeLease == null
            ? _fs.ReadFileAsync(path)
            : _fs.ReadFileAsync(writeLease, path);

    private void ValidateRawMortalLocationMaterializationResponse(
        JsonElement response,
        List<ValidationIssue> issues)
    {
        if (response.ValueKind != JsonValueKind.Object)
            return;

        ValidateRawMortalLocationClientOwnedRootFields(response, "response", issues);

        if (response.TryGetProperty("currentLocationData", out var current) &&
            current.ValueKind == JsonValueKind.Object)
        {
            ValidateRawCurrentLocationCreation(
                current,
                "response.currentLocationData",
                "currentLocationData",
                issues);
        }

        if (!response.TryGetProperty("worldMapUpdates", out var updates) ||
            updates.ValueKind != JsonValueKind.Object)
        {
            return;
        }

        ValidateRawLocationResponseArray(
            updates,
            "newLocations",
            "world_map_creation",
            isLink: false,
            "response.worldMapUpdates",
            "worldMapUpdates",
            issues);
        ValidateRawLocationResponseArray(
            updates,
            "newLinks",
            "world_map_link_creation",
            isLink: true,
            "response.worldMapUpdates",
            "worldMapUpdates",
            issues);
    }

    private static void ValidateRawMortalLocationClientOwnedRootFields(
        JsonElement root,
        string context,
        List<ValidationIssue> issues)
    {
        foreach (var property in root.EnumerateObject())
        {
            if (!string.Equals(property.Name, "locationIdentityIndex", StringComparison.OrdinalIgnoreCase) &&
                !string.Equals(property.Name, "linkIdentityIndex", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            issues.Add(new ValidationIssue(
                $"{context}.{property.Name}",
                IssueSeverity.Error,
                "The GM cannot author Mortal location identity-index state.",
                code: "mortal_location_materialization_client_owned_surface_forbidden",
                section: "mortal_location_materialization",
                expected: "client-owned field absent",
                actual: "present",
                repairHint: "Remove the identity-index field; the client creates and updates it after acceptance.",
                category: IssueCategory.ClientOwnedSurface));
        }
    }

    private async Task ValidateAcceptedTurnRawMortalLocationFileAsync(
        string path,
        bool validateCurrentLocation,
        List<ValidationIssue> issues)
    {
        var json = await _fs.ReadFileAsync(path);
        if (string.IsNullOrWhiteSpace(json))
            return;

        try
        {
            using var document = JsonDocument.Parse(json);
            if (document.RootElement.ValueKind != JsonValueKind.Object)
                return;

            if (validateCurrentLocation)
            {
                if (document.RootElement.TryGetProperty("currentLocationData", out var current) &&
                    current.ValueKind == JsonValueKind.Object)
                {
                    ValidateRawCurrentLocationCreation(
                        current,
                        $"{path}.currentLocationData",
                        "currentLocationData",
                        issues);
                }
                return;
            }

            if (!document.RootElement.TryGetProperty("worldMapUpdates", out var updates) ||
                updates.ValueKind != JsonValueKind.Object)
            {
                return;
            }

            ValidateRawLocationResponseArray(
                updates,
                "newLocations",
                "world_map_creation",
                isLink: false,
                $"{path}.worldMapUpdates",
                "worldMapUpdates",
                issues);
            ValidateRawLocationResponseArray(
                updates,
                "newLinks",
                "world_map_link_creation",
                isLink: true,
                $"{path}.worldMapUpdates",
                "worldMapUpdates",
                issues);
        }
        catch (JsonException)
        {
            // JsonIntegrity owns malformed-file diagnostics before this phase.
        }
    }

    private static void ValidateRawCurrentLocationCreation(
        JsonElement current,
        string issuePath,
        string carrierPath,
        List<ValidationIssue> issues)
    {
        if (!IsRawCurrentLocationCreationCandidate(current))
            return;

        var candidateIssues = MortalLocationMaterializationContract.ValidateRawLocation(
            current,
            issuePath,
            "current_scene_creation");
        AttachMortalLocationRepairContexts(
            candidateIssues,
            current,
            issuePath,
            carrierPath,
            "mortal_location");
        issues.AddRange(candidateIssues);
    }

    private static bool IsRawCurrentLocationCreationCandidate(JsonElement current)
    {
        if (current.TryGetProperty("locationId", out var currentIdentity) &&
            currentIdentity.ValueKind == JsonValueKind.Null)
        {
            return true;
        }

        return current.TryGetProperty("initialId", out _) ||
            current.TryGetProperty("materialization", out _);
    }

    private static void ValidateRawLocationResponseArray(
        JsonElement updates,
        string property,
        string route,
        bool isLink,
        string issuePathPrefix,
        string carrierPathPrefix,
        List<ValidationIssue> issues)
    {
        if (!updates.TryGetProperty(property, out var values))
            return;
        if (values.ValueKind != JsonValueKind.Array)
        {
            issues.Add(LocationIssue(
                $"{issuePathPrefix}.{property}",
                "mortal_location_materialization_invalid_root",
                $"{property} must be an array.",
                "array",
                values.ValueKind.ToString()));
            return;
        }

        var index = 0;
        foreach (var value in values.EnumerateArray())
        {
            var context = $"{issuePathPrefix}.{property}[{index}]";
            var candidateIssues = isLink
                ? MortalLocationMaterializationContract.ValidateRawLink(value, context, route)
                : MortalLocationMaterializationContract.ValidateRawLocation(value, context, route);
            AttachMortalLocationRepairContexts(
                candidateIssues,
                value,
                context,
                $"{carrierPathPrefix}.{property}[{index}]",
                isLink ? "mortal_location_link" : "mortal_location");
            issues.AddRange(candidateIssues);
            index++;
        }
    }

    private static void AttachMortalLocationRepairContexts(
        IReadOnlyList<ValidationIssue> candidateIssues,
        JsonElement candidate,
        string issuePath,
        string carrierPath,
        string entityKind)
    {
        if (candidateIssues.Count == 0)
            return;

        var repairableFields = candidateIssues
            .Where(issue => IsRepairableMortalLocationField(issue, issuePath))
            .Select(issue => ReadRelativeMortalLocationIssuePath(issue.FilePath, issuePath))
            .Distinct(StringComparer.Ordinal)
            .OrderBy(static field => field, StringComparer.Ordinal)
            .ToArray();
        var context = new MortalLocationRepairContext(
            carrierPath,
            entityKind,
            ReadExactMortalLocationRepairString(candidate, "initialId"),
            candidate.TryGetProperty("materialization", out var envelope) &&
            envelope.ValueKind == JsonValueKind.Object
                ? ReadExactMortalLocationRepairString(envelope, "materializationId")
                : null,
            repairableFields);

        foreach (var issue in candidateIssues)
            issue.MortalLocationRepairContext = context;
    }

    private static bool IsRepairableMortalLocationField(
        ValidationIssue issue,
        string issuePath)
    {
        if (issue.Code == null ||
            issue.Code.Contains("identity", StringComparison.Ordinal) ||
            issue.Code.Contains("receipt", StringComparison.Ordinal) ||
            issue.Code.Contains("seal", StringComparison.Ordinal) ||
            issue.Code.Contains("replay", StringComparison.Ordinal) ||
            issue.Code.Contains("duplicate_creation_route", StringComparison.Ordinal) ||
            issue.Code.Contains("client_owned", StringComparison.Ordinal))
        {
            return false;
        }

        var field = ReadRelativeMortalLocationIssuePath(issue.FilePath, issuePath);
        return field is not "locationId" and
            not "linkId" and
            not "initialId" and
            not "materialization.materializationId" and
            not "materialization.route" and
            not "materializationReceipt" &&
            !field.StartsWith("materializationReceipt.", StringComparison.Ordinal);
    }

    private static string ReadRelativeMortalLocationIssuePath(
        string issueFilePath,
        string issuePath)
    {
        var prefix = issuePath + ".";
        return issueFilePath.StartsWith(prefix, StringComparison.Ordinal)
            ? issueFilePath[prefix.Length..]
            : issueFilePath;
    }

    private static string? ReadExactMortalLocationRepairString(
        JsonElement root,
        string field)
    {
        if (!root.TryGetProperty(field, out var value) || value.ValueKind != JsonValueKind.String)
            return null;
        var text = value.GetString();
        return !string.IsNullOrEmpty(text) && string.Equals(text, text.Trim(), StringComparison.Ordinal)
            ? text
            : null;
    }

    private static JsonObject? ParseLocationStateObject(
        string? json,
        string path,
        List<ValidationIssue> issues)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            issues.Add(LocationIssue(
                path,
                "mortal_location_materialization_missing_state",
                "Mortal location canonical state is missing.",
                "JSON object",
                "missing"));
            return null;
        }

        try
        {
            return JsonNode.Parse(json) as JsonObject ?? throw new JsonException("root is not an object");
        }
        catch (JsonException exception)
        {
            issues.Add(LocationIssue(
                path,
                "mortal_location_materialization_invalid_state_json",
                "Mortal location canonical state is not valid object JSON.",
                "JSON object",
                exception.Message));
            return null;
        }
    }

    private static bool TryReadCanonicalWorldMap(
        JsonObject map,
        out JsonArray locations,
        out JsonArray links)
    {
        locations = map["locations"] as JsonArray ?? new JsonArray();
        links = map["links"] as JsonArray ?? new JsonArray();
        return map.Count == 4 &&
               map["schemaVersion"] is JsonValue schema && schema.TryGetValue<int>(out var version) && version == 1 &&
               map["realm"] is JsonValue realm && realm.TryGetValue<string>(out var realmName) &&
               string.Equals(realmName, "mortal_world", StringComparison.Ordinal) &&
               map["locations"] is JsonArray &&
               map["links"] is JsonArray;
    }

    private static void ValidateCanonicalLocationArray(
        JsonArray values,
        bool links,
        List<ValidationIssue> issues)
    {
        for (var index = 0; index < values.Count; index++)
        {
            var value = values[index];
            if (value == null)
            {
                issues.Add(LocationIssue(
                    $"{MortalLocationMaterializationContract.WorldMapPath}.{(links ? "links" : "locations")}[{index}]",
                    "mortal_location_materialization_invalid_root",
                    "Canonical map members must be objects.",
                    "object",
                    "null"));
                continue;
            }
            using var document = JsonDocument.Parse(value.ToJsonString());
            var context = $"{MortalLocationMaterializationContract.WorldMapPath}.{(links ? "links" : "locations")}[{index}]";
            issues.AddRange(links
                ? MortalLocationMaterializationContract.ValidateCanonicalLink(document.RootElement, context)
                : MortalLocationMaterializationContract.ValidateCanonicalLocation(document.RootElement, context));
        }
    }

    private static void ValidateCurrentLocationProjection(
        JsonObject current,
        JsonArray locations,
        List<ValidationIssue> issues)
    {
        using var document = JsonDocument.Parse(current.ToJsonString());
        issues.AddRange(MortalLocationMaterializationContract.ValidateCanonicalLocation(
            document.RootElement,
            MortalLocationMaterializationContract.CurrentLocationPath));
        if (!TryReadExactString(current, "locationId", out var currentId))
            return;

        var matches = locations.OfType<JsonObject>()
            .Where(location => TryReadExactString(location, "locationId", out var locationId) &&
                               string.Equals(locationId, currentId, StringComparison.Ordinal))
            .ToArray();
        if (matches.Length != 1)
        {
            issues.Add(LocationIssue(
                MortalLocationMaterializationContract.CurrentLocationPath,
                "mortal_location_materialization_current_projection_mismatch",
                "Current location must resolve exactly once in canonical world map.",
                "one exact map location",
                currentId));
            return;
        }

        foreach (var field in CurrentProjectionSharedFields)
        {
            if (JsonNode.DeepEquals(current[field], matches[0][field]))
                continue;
            issues.Add(LocationIssue(
                $"{MortalLocationMaterializationContract.CurrentLocationPath}.{field}",
                "mortal_location_materialization_current_projection_mismatch",
                "Current projection shared semantics must equal the canonical map location.",
                matches[0][field]?.ToJsonString() ?? "null",
                current[field]?.ToJsonString() ?? "missing"));
        }
    }

    private static readonly string[] CurrentProjectionSharedFields =
    {
        "locationId",
        "realm",
        "name",
        "displayName",
        "purpose",
        "description",
        "image_prompt",
        "locationType",
        "biome",
        "biomeDescription",
        "indoorType",
        "features",
        "region",
        "parentLocationId",
        "coordinates",
        "discovery",
        "internalDifficulty",
        "externalDifficulty",
        "lastEventsDescription",
        "eventDescriptions",
        "factionControl",
        "actorBindings",
        "activeThreats",
        "loreBindings",
        "customStates",
        "materialization",
        "materializationReceipt"
    };

    private static bool TryReadExactString(JsonObject root, string field, out string value)
    {
        value = string.Empty;
        if (root[field] is not JsonValue node || !node.TryGetValue<string>(out var text) ||
            string.IsNullOrEmpty(text) || !string.Equals(text, text.Trim(), StringComparison.Ordinal))
        {
            return false;
        }
        value = text;
        return true;
    }

    private static ValidationIssue LocationIssue(
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
            repairHint: "Restore client-owned state or correct the exact GM-owned location carrier.");
}
