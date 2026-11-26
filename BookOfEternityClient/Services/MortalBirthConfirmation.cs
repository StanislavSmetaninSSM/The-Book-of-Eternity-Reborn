using System.Text.Json;
using System.Text.Json.Nodes;

namespace BookOfEternityClient.Services;

/// <summary>Player-visible statements about accepted first births, never commands or new state authority.</summary>
internal static class MortalBirthConfirmation
{
    internal const string Property = "mortalBirthConfirmations";
    internal const string VehicleOwnership = "vehicleOwnership";
    internal const string StorageAccess = "storageAccess";
    internal sealed record Source(string SessionId, string RequestId, string SnapshotToken, int Turn);
    internal sealed record Claim(string Kind, string Ref);

    // A missing optional read must not turn an already accepted move into a half-finished turn.
    // Invalid authority, generation replacement, and uncertain publication are not optional I/O.
    internal static async Task<string?> ReadOptionalAsync(Func<Task<string?>> read)
    {
        try { return await read(); }
        catch (JsonException) { return null; }
        catch (IOException ex) when (ex.GetType() == typeof(IOException) ||
            ex is FileNotFoundException or DirectoryNotFoundException)
        { return null; }
    }

    internal static Claim[] ReadClaims(JsonElement debug)
    {
        if (debug.ValueKind != JsonValueKind.Object) return [];
        var fields = debug.EnumerateObject()
            .Where(p => string.Equals(p.Name, Property, StringComparison.OrdinalIgnoreCase)).Take(2).ToArray();
        if (fields.Length != 1 || fields[0].Name != Property ||
            fields[0].Value.ValueKind != JsonValueKind.Array || fields[0].Value.GetArrayLength() > 16)
            return [];

        var claims = new List<Claim>();
        var seen = new HashSet<Claim>();
        foreach (var row in fields[0].Value.EnumerateArray())
        {
            if (row.ValueKind != JsonValueKind.Object || row.EnumerateObject().Count() != 2 ||
                !row.TryGetProperty("kind", out var kindNode) || kindNode.ValueKind != JsonValueKind.String ||
                !row.TryGetProperty("ref", out var refNode) || refNode.ValueKind != JsonValueKind.String ||
                kindNode.GetString() is not (VehicleOwnership or StorageAccess) ||
                refNode.GetString() is not { Length: <= 160 } reference ||
                !ResourceMaterializationContract.IsExactIdentifier(reference))
                return [];
            var claim = new Claim(kindNode.GetString()!, reference);
            if (!seen.Add(claim)) return [];
            claims.Add(claim);
        }
        return claims.ToArray();
    }

    // Input roots already passed canonical post-materialization validation under the game's accepted turn owner.
    internal static string? Project(string? prose, IReadOnlyList<Claim> claims, Source source,
        JsonObject? current, JsonObject? vehicles, JsonObject? index, JsonObject? access)
    {
        if (claims.Count == 0 || source.Turn <= 0 ||
            !ResourceMaterializationContract.IsExactIdentifier(source.SessionId) ||
            !ResourceMaterializationContract.IsExactIdentifier(source.RequestId) ||
            !ResourceMaterializationContract.IsExactIdentifier(source.SnapshotToken) ||
            Text(current?["realm"]) != "mortal_world" ||
            Text(current?["locationId"]) is not { } location)
            return prose;

        var receipts = Objects(vehicles?["birthReceipts"]).Where(r => Matches(r, "sourceTurn", source))
            .ToLookup(r => Text(r["vehicleRef"]), StringComparer.Ordinal);
        var liveVehicles = Objects(vehicles?["vehicles"]).ToLookup(v => Text(v["vehicleId"]), StringComparer.Ordinal);
        var entry = One(Objects(index?["locationEntries"]).Where(l => Text(l["locationId"]) == location));
        var transitions = Objects(entry?["transitions"]).Where(t => Text(t["kind"]) == "storage_addition" &&
                Integer(t["turn"]) == source.Turn)
            .ToLookup(t => Text(t["childId"]), StringComparer.Ordinal);
        var storages = Objects(current?["locationStorages"]).ToLookup(s => Text(s["storageId"]), StringComparer.Ordinal);
        var effective = Objects(access?["entries"]).Where(s => Text(s["locationId"]) == location)
            .ToLookup(s => Text(s["storageId"]), StringComparer.Ordinal);

        var lines = new List<string>(claims.Count);
        foreach (var claim in claims)
        {
            string? label;
            switch (claim.Kind)
            {
                case VehicleOwnership:
                    var receipt = One(receipts[claim.Ref]);
                    if (Text(receipt?["vehicleId"]) is not { } id) return prose;
                    var vehicle = One(liveVehicles[id]);
                    var owner = vehicle?["owner"] as JsonObject;
                    if ((Text(vehicle?["currentLocationId"]) != location &&
                         !(Text(vehicle?["availability"]) == "Active" && Text(vehicles?["activeVehicleId"]) == id)) ||
                        Text(owner?["ownerType"]) != "Player" || Text(owner?["ownerId"]) != "player_current" ||
                        (label = Label(vehicle!["name"])) is null) return prose;
                    lines.Add($"Теперь вам принадлежит транспорт «{label}».");
                    break;
                case StorageAccess:
                    var transition = One(transitions[claim.Ref]);
                    var evidence = (transition?["afterState"] as JsonObject)?["cargoBirthEvidence"] as JsonObject;
                    var storage = One(storages[claim.Ref]);
                    var mirror = One(effective[claim.Ref]);
                    if (evidence is null || !AuthorityMatches(evidence, source) ||
                        Text(evidence["locationId"]) != location || Text(evidence["storageId"]) != claim.Ref ||
                        Integer(evidence["sourceTurn"]) != source.Turn ||
                        Integer(access?["schemaVersion"]) != 1 ||
                        !True(storage?["hasFullAccess"]) || !True(mirror?["hasFullAccess"]) ||
                        storage is null || mirror is null || !storage.ContainsKey("owner") || !mirror.ContainsKey("owner") ||
                        !storage.ContainsKey("authorizedUsers") || !mirror.ContainsKey("authorizedUsers") ||
                        !JsonNode.DeepEquals(storage["owner"], mirror["owner"]) ||
                        !JsonNode.DeepEquals(storage["authorizedUsers"], mirror["authorizedUsers"]) ||
                        (label = Label(storage["name"])) is null) return prose;
                    lines.Add($"Вам доступно хранилище «{label}».");
                    break;
                default: return prose;
            }
        }

        return string.IsNullOrEmpty(prose) ? string.Join('\n', lines) : prose + "\n\n" + string.Join('\n', lines);
    }

    // Presentation only: durable accepted story is the authority for prior captions.
    // Never recompute past ownership/access from later world data or recognize a free-prose claim.
    internal static string? ReadCaptionedAcceptedStory(string prose, IReadOnlyList<Claim> claims,
        string realm, int turn, JsonObject? accepted)
    {
        if (claims.Count == 0 || turn <= 0 || string.IsNullOrWhiteSpace(prose) ||
            !RealmSemantics.IsMortalRealm(realm) || Text(accepted?["realm"]) != realm ||
            Integer(accepted?["turn"]) != turn || Text(accepted?["narrative"]) is not { } narrative ||
            !narrative.StartsWith(prose + "\n\n", StringComparison.Ordinal))
            return null;

        var lines = narrative[(prose.Length + 2)..].Split('\n');
        if (lines.Length != claims.Count)
            return null;
        for (var i = 0; i < lines.Length; i++)
        {
            var prefix = claims[i].Kind switch
            {
                VehicleOwnership => "Теперь вам принадлежит транспорт «",
                StorageAccess => "Вам доступно хранилище «",
                _ => null
            };
            var line = lines[i];
            if (prefix is null || !line.StartsWith(prefix, StringComparison.Ordinal) ||
                !line.EndsWith("».", StringComparison.Ordinal) || line.Length <= prefix.Length + 2 ||
                !IsLiteralLabel(line[prefix.Length..^2]))
                return null;
        }
        return narrative;
    }

    private static bool Matches(JsonObject evidence, string turnProperty, Source source) =>
        Integer(evidence[turnProperty]) == source.Turn && AuthorityMatches(evidence, source);

    private static bool AuthorityMatches(JsonObject evidence, Source source) =>
        evidence["sourceAuthority"] is JsonObject authority &&
        Text(authority["sessionId"]) == source.SessionId && Text(authority["requestId"]) == source.RequestId &&
        Text(authority["snapshotToken"]) == source.SnapshotToken;

    private static IEnumerable<JsonObject> Objects(JsonNode? node) =>
        node is JsonArray array ? array.OfType<JsonObject>() : Enumerable.Empty<JsonObject>();

    private static JsonObject? One(IEnumerable<JsonObject> rows)
    {
        using var sequence = rows.GetEnumerator();
        if (!sequence.MoveNext()) return null;
        var row = sequence.Current;
        return sequence.MoveNext() ? null : row;
    }

    private static string? Text(JsonNode? node) => node is JsonValue v && v.TryGetValue<string>(out var s) && !string.IsNullOrWhiteSpace(s) ? s : null;
    private static int? Integer(JsonNode? node) => node is JsonValue v && v.TryGetValue<int>(out var n) ? n : null;
    private static bool True(JsonNode? node) => node is JsonValue v && v.TryGetValue<bool>(out var b) && b;
    private static string? Label(JsonNode? node) => Text(node) is { } label && IsLiteralLabel(label) ? label : null;
    private static bool IsLiteralLabel(string label) => !string.IsNullOrWhiteSpace(label) &&
        label.Length <= 160 && label.All(c => !char.IsControl(c) && c is not ('\u2028' or '\u2029')) &&
        !label.Contains("[ACHIEVEMENT_UNLOCK:", StringComparison.OrdinalIgnoreCase);
}
