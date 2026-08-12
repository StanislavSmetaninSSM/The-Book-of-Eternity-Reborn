using System.Text.Json.Nodes;

namespace BookOfEternityClient.Services;

internal sealed record MortalBootstrapLocationReservation(
    string InitialId,
    string ReservedLocationId,
    string Route,
    int X,
    int Y,
    int Z);

internal sealed record MortalBootstrapLinkReservation(
    string InitialId,
    string ReservedLinkId,
    string Route,
    string SourceInitialId,
    string TargetInitialId);

internal sealed record MortalBootstrapLocationReservationSet(
    string State,
    string SessionId,
    string RequestId,
    int TurnNumber,
    string AuthorityKind,
    string AuthorityId,
    MortalBootstrapLocationReservation Start,
    MortalBootstrapLocationReservation Neighbor,
    MortalBootstrapLinkReservation Link);

internal static class MortalBootstrapLocationScaffold
{
    internal const string StatePath = "game_state/control/mortal_bootstrap_scaffold.json";

    internal const string AuthorityKind = "mortal_bootstrap_scaffold";
    internal const string MaterializedNeighborBranch = "materialized_neighbor_link";
    internal const string NarrativeOnlyBranch = "narrative_only_unresolved_exit";

    internal static JsonObject CreatePendingRequest(
        int incarnationNumber,
        string sessionId,
        string requestId,
        int turnNumber)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sessionId);
        ArgumentException.ThrowIfNullOrWhiteSpace(requestId);
        if (!string.Equals(sessionId, sessionId.Trim(), StringComparison.Ordinal))
            throw new ArgumentException("Bootstrap sessionId must be exact.", nameof(sessionId));
        if (!string.Equals(requestId, requestId.Trim(), StringComparison.Ordinal))
            throw new ArgumentException("Bootstrap requestId must be exact.", nameof(requestId));

        var suffix = $"life_{Math.Max(incarnationNumber, 1):D3}";
        var startInitialId = $"locref_{suffix}_start";
        var neighborInitialId = $"locref_{suffix}_neighbor";

        return new JsonObject
        {
            ["schemaVersion"] = 1,
            ["state"] = "pending",
            ["sessionId"] = sessionId,
            ["requestId"] = requestId,
            ["turnNumber"] = Math.Max(turnNumber, 1),
            ["sourceAuthority"] = new JsonObject
            {
                ["kind"] = AuthorityKind,
                ["authorityId"] = requestId
            },
            ["startReservation"] = new JsonObject
            {
                ["initialId"] = startInitialId,
                ["reservedLocationId"] = $"loc_{suffix}_start",
                ["route"] = "current_scene_creation",
                ["coordinates"] = Coordinates(0, 0, 0),
                ["requiredDiscovery"] = new JsonObject
                {
                    ["tier"] = "visited",
                    ["audience"] = "player_known"
                }
            },
            ["neighborReservation"] = new JsonObject
            {
                ["initialId"] = neighborInitialId,
                ["reservedLocationId"] = $"loc_{suffix}_neighbor",
                ["route"] = "world_map_creation",
                ["coordinates"] = Coordinates(1, 0, 0)
            },
            ["linkReservation"] = new JsonObject
            {
                ["initialId"] = $"linkref_{suffix}_start_to_neighbor",
                ["reservedLinkId"] = $"lnk_{suffix}_start_to_neighbor",
                ["route"] = "world_map_link_creation",
                ["sourceInitialId"] = startInitialId,
                ["targetInitialId"] = neighborInitialId
            },
            ["allowedCompletionBranches"] = new JsonArray(
                MaterializedNeighborBranch,
                NarrativeOnlyBranch),
            ["narrativeOnlyRule"] =
                "If no complete neighbor is materialized, omit both reserved neighbor and link identities, endpoints, coordinates, and canonical entries; describe only an unresolved possible exit in player-facing narrative."
        };
    }

    internal static bool TryReadRequest(
        JsonObject? request,
        out MortalBootstrapLocationReservationSet reservations,
        out string error)
    {
        reservations = null!;
        error = string.Empty;
        if (request == null || ReadInt(request, "schemaVersion") != 1)
            return Fail("locationMaterializationRequest requires schemaVersion=1.", out error);

        var state = ReadExactString(request, "state");
        var sessionId = ReadExactString(request, "sessionId");
        var requestId = ReadExactString(request, "requestId");
        var turnNumber = ReadInt(request, "turnNumber");
        var authority = request["sourceAuthority"] as JsonObject;
        var authorityKind = ReadExactString(authority, "kind");
        var authorityId = ReadExactString(authority, "authorityId");
        if (state is not "pending" and not "settled" ||
            sessionId == null ||
            requestId == null ||
            turnNumber is null or < 1 ||
            !string.Equals(authorityKind, AuthorityKind, StringComparison.Ordinal) ||
            !string.Equals(authorityId, requestId, StringComparison.Ordinal))
        {
            return Fail(
                "locationMaterializationRequest requires exact state, request identity, turn, and scaffold source authority.",
                out error);
        }

        if (!TryReadLocationReservation(
                request["startReservation"] as JsonObject,
                "current_scene_creation",
                out var start,
                out error) ||
            !TryReadLocationReservation(
                request["neighborReservation"] as JsonObject,
                "world_map_creation",
                out var neighbor,
                out error) ||
            !TryReadLinkReservation(
                request["linkReservation"] as JsonObject,
                start.InitialId,
                neighbor.InitialId,
                out var link,
                out error))
        {
            return false;
        }

        if (request["allowedCompletionBranches"] is not JsonArray branches ||
            branches.Count != 2 ||
            ReadExactString(branches[0]) != MaterializedNeighborBranch ||
            ReadExactString(branches[1]) != NarrativeOnlyBranch)
        {
            return Fail("Bootstrap completion branches must use the exact closed pair.", out error);
        }

        reservations = new MortalBootstrapLocationReservationSet(
            state,
            sessionId,
            requestId,
            turnNumber.Value,
            authorityKind!,
            authorityId!,
            start,
            neighbor,
            link);
        return true;
    }

    internal static JsonObject CreateSettledRequest(
        JsonObject pendingRequest,
        string branch,
        int acceptedTurn,
        string startLocationId,
        string? neighborLocationId,
        string? linkId)
    {
        ArgumentNullException.ThrowIfNull(pendingRequest);
        if (branch is not MaterializedNeighborBranch and not NarrativeOnlyBranch)
            throw new ArgumentOutOfRangeException(nameof(branch), branch, "Unknown bootstrap completion branch.");
        ArgumentException.ThrowIfNullOrWhiteSpace(startLocationId);

        var result = pendingRequest.DeepClone().AsObject();
        result["state"] = "settled";
        result["settlement"] = new JsonObject
        {
            ["requestId"] = result["requestId"]!.DeepClone(),
            ["acceptedTurn"] = acceptedTurn,
            ["branch"] = branch,
            ["startLocationId"] = startLocationId,
            ["neighborLocationId"] = neighborLocationId,
            ["linkId"] = linkId
        };
        return result;
    }

    private static bool TryReadLocationReservation(
        JsonObject? reservation,
        string expectedRoute,
        out MortalBootstrapLocationReservation result,
        out string error)
    {
        result = null!;
        error = string.Empty;
        var initialId = ReadExactString(reservation, "initialId");
        var reservedLocationId = ReadExactString(reservation, "reservedLocationId");
        var route = ReadExactString(reservation, "route");
        var coordinates = reservation?["coordinates"] as JsonObject;
        var x = ReadInt(coordinates, "x");
        var y = ReadInt(coordinates, "y");
        var z = ReadInt(coordinates, "z");
        if (initialId == null ||
            reservedLocationId == null ||
            !string.Equals(route, expectedRoute, StringComparison.Ordinal) ||
            x == null || y == null || z == null)
        {
            return Fail($"Bootstrap {expectedRoute} reservation is incomplete.", out error);
        }

        result = new MortalBootstrapLocationReservation(
            initialId,
            reservedLocationId,
            route!,
            x.Value,
            y.Value,
            z.Value);
        return true;
    }

    private static bool TryReadLinkReservation(
        JsonObject? reservation,
        string expectedSourceInitialId,
        string expectedTargetInitialId,
        out MortalBootstrapLinkReservation result,
        out string error)
    {
        result = null!;
        error = string.Empty;
        var initialId = ReadExactString(reservation, "initialId");
        var reservedLinkId = ReadExactString(reservation, "reservedLinkId");
        var route = ReadExactString(reservation, "route");
        var sourceInitialId = ReadExactString(reservation, "sourceInitialId");
        var targetInitialId = ReadExactString(reservation, "targetInitialId");
        if (initialId == null ||
            reservedLinkId == null ||
            route != "world_map_link_creation" ||
            sourceInitialId != expectedSourceInitialId ||
            targetInitialId != expectedTargetInitialId)
        {
            return Fail("Bootstrap link reservation is incomplete or has mismatched endpoints.", out error);
        }

        result = new MortalBootstrapLinkReservation(
            initialId,
            reservedLinkId,
            route,
            sourceInitialId,
            targetInitialId);
        return true;
    }

    private static string? ReadExactString(JsonObject? root, string field) =>
        root == null ? null : ReadExactString(root[field]);

    private static string? ReadExactString(JsonNode? node)
    {
        if (node is not JsonValue value ||
            !value.TryGetValue<string>(out var text) ||
            string.IsNullOrEmpty(text) ||
            !string.Equals(text, text.Trim(), StringComparison.Ordinal))
        {
            return null;
        }
        return text;
    }

    private static int? ReadInt(JsonObject? root, string field) =>
        root?[field] is JsonValue value && value.TryGetValue<int>(out var result)
            ? result
            : null;

    private static bool Fail(string message, out string error)
    {
        error = message;
        return false;
    }

    private static JsonObject Coordinates(int x, int y, int z) =>
        new()
        {
            ["x"] = x,
            ["y"] = y,
            ["z"] = z
        };
}
