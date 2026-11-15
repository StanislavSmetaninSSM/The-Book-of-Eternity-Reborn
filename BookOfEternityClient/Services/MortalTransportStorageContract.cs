using System.Text.Json;

namespace BookOfEternityClient.Services;

/// <summary>
/// Structural first-birth carrier facts. This does not grant publication, faction/NPC
/// ownership, cargo validity or access; those require the accepted-turn authorities.
/// </summary>
internal static class MortalTransportStorageContract
{
    internal static IReadOnlyList<ValidationIssue> ValidateVehicleBirth(JsonElement vehicle, string context)
    {
        var issues = new List<ValidationIssue>();
        if (vehicle.ValueKind != JsonValueKind.Object)
        {
            Add(context, "complete vehicle creation object");
            return issues;
        }
        if (!vehicle.TryGetProperty("capacity", out var capacity) ||
            capacity.ValueKind != JsonValueKind.Number || !capacity.TryGetInt32(out var slots) || slots < 0)
            Add(context + ".capacity", "nonnegative integer direct-stack capacity");
        if (!vehicle.TryGetProperty("volume", out var volume) ||
            volume.ValueKind != JsonValueKind.Number || !volume.TryGetDouble(out var dm3) ||
            !double.IsFinite(dm3) || dm3 < 0)
            Add(context + ".volume", "finite nonnegative cargo volume in dm3");
        if (!vehicle.TryGetProperty("hasFullAccess", out var access) ||
            access.ValueKind is not (JsonValueKind.True or JsonValueKind.False))
            Add(context + ".hasFullAccess", "explicit boolean access declaration");

        if (!vehicle.TryGetProperty("owner", out var owner))
            Add(context + ".owner", "explicit owner object or null for unowned");
        else if (owner.ValueKind != JsonValueKind.Null)
        {
            var path = context + ".owner";
            if (Closed(owner, path, "ownerType", "ownerId", "ownerName"))
            {
                var kind = Text(owner, "ownerType", path, false);
                var id = Text(owner, "ownerId", path, true);
                Text(owner, "ownerName", path, false);
                if (kind is not ("Player" or "Shared" or "Faction"))
                    Add(path + ".ownerType", "Player, Shared or Faction");
                if (kind is "Player" or "Shared" && id != "player_current")
                    Add(path + ".ownerId", "existing single-player identity player_current; no inferred player registry");
            }
        }
        if (!vehicle.TryGetProperty("controller", out var controller))
            Add(context + ".controller", "explicit controller object or null for uncontrolled");
        else if (controller.ValueKind != JsonValueKind.Null)
        {
            var path = context + ".controller";
            if (Closed(controller, path, "actorType", "actorId"))
            {
                var kind = Text(controller, "actorType", path, false);
                var id = Text(controller, "actorId", path, true);
                if (kind is not ("player" or "npc"))
                    Add(path + ".actorType", "player or npc");
                if (kind == "player" && id != "player_current")
                    Add(path + ".actorId", "existing single-player identity player_current");
            }
        }
        if (!vehicle.TryGetProperty("authorizedUsers", out var users) ||
            users.ValueKind is not (JsonValueKind.Array or JsonValueKind.Null))
            Add(context + ".authorizedUsers", "explicit array or null");
        else if (users.ValueKind == JsonValueKind.Array)
        {
            var seen = new HashSet<string>(StringComparer.Ordinal);
            var index = 0;
            foreach (var user in users.EnumerateArray())
            {
                var path = $"{context}.authorizedUsers[{index++}]";
                if (!Closed(user, path, "playerId", "playerName"))
                    continue;
                var id = Text(user, "playerId", path, true);
                Text(user, "playerName", path, false);
                if (id != "player_current")
                    Add(path + ".playerId", "existing single-player identity player_current; unknown recipients are unsupported");
                else if (!seen.Add(id))
                    Add(path + ".playerId", "one row per exact player identity");
            }
        }
        return issues;

        bool Closed(JsonElement value, string path, params string[] fields)
        {
            if (value.ValueKind != JsonValueKind.Object)
            {
                Add(path, "closed object");
                return false;
            }
            foreach (var field in value.EnumerateObject())
                if (!fields.Contains(field.Name, StringComparer.Ordinal))
                    Add(path + "." + field.Name, "only " + string.Join(", ", fields));
            return true;
        }

        string? Text(JsonElement value, string field, string path, bool exact)
        {
            if (value.TryGetProperty(field, out var property) && property.ValueKind == JsonValueKind.String &&
                property.GetString() is string text && !string.IsNullOrWhiteSpace(text) &&
                (!exact || ResourceMaterializationContract.IsExactIdentifier(text)))
                return text;
            Add(path + "." + field, exact ? "exact nonempty identity" : "nonempty string");
            return null;
        }

        void Add(string path, string expected) => issues.Add(new ValidationIssue(path,
            IssueSeverity.Error, "Incomplete or invalid vehicle birth fact.",
            code: "resource_owner_vehicle_birth_invalid", section: "Vehicles", expected: expected,
            actual: "missing or invalid",
            repairHint: "Correct the indexed field in this new UpdateVehicles object only. Do not change identities, canonical state or historical receipts."));
    }
}
