using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using BookOfEternityClient.Configuration;
using BookOfEternityClient.Core;

namespace BookOfEternityClient.Services;

internal static class SourceOfLightCapstoneState
{
    public const string PendingRequestPath = "game_state/control/pending_source_of_light_capstone.json";
    public const string PassiveId = "light_incarnate";
    public const string RelicId = "source_of_light_incarnated_light";
    public const string ShiningStateProperty = "sourceOfLightCapstone";
    public const string CapstonesProperty = "capstones";
    public const string LightIncarnateProperty = "lightIncarnate";
    public const int RequiredRadianceTier = 4;
    public const int RequiredRadianceExperience = 580;
    public const int LeadDiceBonus = 8;
    public const int SupportDiceBonus = 4;
    public const int CoerciveOperationExtraBonus = 4;
    public const int MortalCharacteristicBonus = 25;

    private static readonly JsonSerializerOptions JsonOpts = SharedJsonOptions.PrettyCamelCaseUnsafeRelaxed;

    public sealed record SourceOfLightCapstoneRequest(
        string RequestId,
        int CreatedAtTurn,
        string CreatedAtUtc,
        int RadianceExperienceAtRequest,
        int RadianceTierAtRequest,
        string RewardPassiveId,
        string RewardRelicId);

    public sealed record SourceOfLightCapstoneReadState(
        SourceOfLightCapstoneRequest? Request,
        bool Exists,
        bool IsMalformed,
        string? Error,
        string? RawPayload);

    public static SourceOfLightCapstoneRequest CreateRequest(int createdAtTurn, int radianceExperience, int radianceTier) =>
        new(
            $"source_of_light_capstone:{Math.Max(0, createdAtTurn)}",
            createdAtTurn,
            DateTime.UtcNow.ToString("O"),
            radianceExperience,
            radianceTier,
            PassiveId,
            RelicId);

    public static async Task<SourceOfLightCapstoneReadState> ReadRequestStateAsync(FileSystemManager fs)
    {
        var raw = await fs.ReadFileAsync(PendingRequestPath);
        if (raw == null)
            return new SourceOfLightCapstoneReadState(null, false, false, null, null);

        if (string.IsNullOrWhiteSpace(raw))
            return new SourceOfLightCapstoneReadState(null, true, true, "empty/whitespace file", raw);

        try
        {
            var request = JsonSerializer.Deserialize<SourceOfLightCapstoneRequest>(raw, JsonOpts);
            var error = ValidateRequest(request);
            return error == null
                ? new SourceOfLightCapstoneReadState(request, true, false, null, raw)
                : new SourceOfLightCapstoneReadState(null, true, true, error, raw);
        }
        catch (Exception ex)
        {
            return new SourceOfLightCapstoneReadState(null, true, true, ex.GetType().Name, raw);
        }
    }

    public static SourceOfLightCapstoneReadState ReadRequestState(string? raw, bool exists)
    {
        if (!exists && raw == null)
            return new SourceOfLightCapstoneReadState(null, false, false, null, null);

        if (string.IsNullOrWhiteSpace(raw))
            return new SourceOfLightCapstoneReadState(null, exists, exists, exists ? "empty/whitespace file" : null, raw);

        try
        {
            var request = JsonSerializer.Deserialize<SourceOfLightCapstoneRequest>(raw, JsonOpts);
            var error = ValidateRequest(request);
            return error == null
                ? new SourceOfLightCapstoneReadState(request, true, false, null, raw)
                : new SourceOfLightCapstoneReadState(null, true, true, error, raw);
        }
        catch (Exception ex)
        {
            return new SourceOfLightCapstoneReadState(null, true, true, ex.GetType().Name, raw);
        }
    }

    public static async Task WriteRequestAsync(FileSystemManager fs, SourceOfLightCapstoneRequest request) =>
        await fs.WriteFileAtomicAsync(PendingRequestPath, JsonSerializer.Serialize(request, JsonOpts));

    public static void ClearRequest(FileSystemManager fs) => fs.DeleteFile(PendingRequestPath);

    public static async Task EnsureHealthyAsync(FileSystemManager fs, string? currentRealm)
    {
        if (!RealmSemantics.HasResolvedRealm(currentRealm) || !fs.FileExists(PendingRequestPath))
            return;

        var state = await ReadRequestStateAsync(fs);
        if (state.IsMalformed || state.Request == null)
            return;

        if (!RealmSemantics.IsShiningRealm(currentRealm))
            return;

        var shiningRoot = await ReadJsonObjectAsync(fs, ShiningAbodeState.StatePath);
        var soulRoot = await ReadJsonObjectAsync(fs, "game_state/meta/soul_state.json");
        if (HasMatchingClosure(shiningRoot, soulRoot, state.Request))
            ClearRequest(fs);
    }

    public static async Task<string?> BuildSystemReminderFragmentAsync(FileSystemManager fs, string? currentRealm)
    {
        if (!RealmSemantics.IsAfterlifeRealm(currentRealm))
            return null;

        var state = await ReadRequestStateAsync(fs);
        if (!state.Exists)
            return null;

        if (state.IsMalformed || state.Request == null)
        {
            return "SOURCE OF LIGHT CAPSTONE CORRUPTION:\n" +
                   $"  - {PendingRequestPath} unreadable or malformed.\n" +
                   "  - Preserve the file and repair the client-authored capstone request before resolving Source of Light.";
        }

        if (!RealmSemantics.IsShiningRealm(currentRealm))
        {
            return "SOURCE OF LIGHT CAPSTONE WRONG REALM:\n" +
                   $"  - {PendingRequestPath} is Shining Abode-only and is repair-only context outside ordinary active Shining Abode.\n" +
                   "  - Preserve the file; do not grant Light Incarnate or Incarnated Light from a Chaos Sea/Mortal turn.";
        }

        var sb = new StringBuilder();
        sb.AppendLine("SOURCE OF LIGHT CAPSTONE:");
        sb.AppendLine("  - Resolve only in ordinary active Shining Abode with no preparedIncarnationPackage.");
        sb.AppendLine("  - Author the Source of Light roleplay scene, then mark shining_abode_state.sourceOfLightCapstone.completed=true.");
        sb.AppendLine("  - Grant exactly one soul-owned capstone soul_state.afterlifeCombatProfile.capstones.lightIncarnate.");
        sb.AppendLine("  - Add exactly one Soul Relic source_of_light_incarnated_light through metaStateUpdates.soulRelicOperations.addRelic or equivalent canonical soulRelics.stored[] materialization.");
        sb.AppendLine($"  - Request: requestId={state.Request.RequestId}, radiance={state.Request.RadianceExperienceAtRequest}/tier {state.Request.RadianceTierAtRequest}, passive={state.Request.RewardPassiveId}, relic={state.Request.RewardRelicId}.");
        return sb.ToString();
    }

    public static string? ValidateRequest(SourceOfLightCapstoneRequest? request)
    {
        if (request == null)
            return "root is not a source_of_light_capstone request";

        if (string.IsNullOrWhiteSpace(request.RequestId) ||
            !request.RequestId.StartsWith("source_of_light_capstone:", StringComparison.OrdinalIgnoreCase))
            return "missing or invalid requestId";

        if (request.CreatedAtTurn <= 0)
            return "createdAtTurn must be positive";

        if (string.IsNullOrWhiteSpace(request.CreatedAtUtc))
            return "createdAtUtc must be non-empty";

        if (request.RadianceExperienceAtRequest < RequiredRadianceExperience)
            return "radianceExperienceAtRequest below Source of Light threshold";

        if (request.RadianceTierAtRequest != RequiredRadianceTier)
            return "radianceTierAtRequest must be 4";

        if (!string.Equals(request.RewardPassiveId, PassiveId, StringComparison.OrdinalIgnoreCase))
            return "rewardPassiveId mismatch";

        if (!string.Equals(request.RewardRelicId, RelicId, StringComparison.OrdinalIgnoreCase))
            return "rewardRelicId mismatch";

        return null;
    }

    public static bool IsUnlockSatisfied(JsonObject? soulRoot, JsonObject? shiningRoot, out string blocker)
    {
        blocker = string.Empty;
        if (!RealmSemantics.IsShiningRealm(GetNodeString(soulRoot?["currentRealm"])))
        {
            blocker = "Источник Света доступен только в Сияющей Обители.";
            return false;
        }

        if (shiningRoot == null)
        {
            blocker = "shining_abode_state.json отсутствует или нечитаем.";
            return false;
        }

        if (!string.Equals(GetNodeString(shiningRoot["availability"]), ShiningAbodeState.AvailabilityActive, StringComparison.OrdinalIgnoreCase))
        {
            blocker = "Сияющая Обитель должна быть ordinary active: availability=active.";
            return false;
        }

        if (ShiningAbodeState.GetPreparedIncarnationPackageMode(shiningRoot) != ShiningAbodeState.PreparedIncarnationPackageMode.Absent)
        {
            blocker = "Источник Света недоступен во время pending-bootstrap handoff preparedIncarnationPackage.";
            return false;
        }

        var radiance = shiningRoot["radiance"] as JsonObject;
        var experience = GetNodeInt(radiance?["experience"]);
        var tier = GetNodeInt(radiance?["tier"]);
        if (tier != RequiredRadianceTier || experience < RequiredRadianceExperience)
        {
            blocker = $"Нужно полное Сияние: radiance.tier={RequiredRadianceTier} и radiance.experience>={RequiredRadianceExperience}. Сейчас tier={tier}, experience={experience}.";
            return false;
        }

        return true;
    }

    public static bool HasMatchingClosure(JsonObject? shiningRoot, JsonObject? soulRoot, SourceOfLightCapstoneRequest request) =>
        HasCompletedCapstone(shiningRoot, request) &&
        HasLightIncarnate(soulRoot) &&
        CountIncarnatedLightRelics(soulRoot) == 1;

    public static bool HasCompletedCapstone(JsonObject? shiningRoot, SourceOfLightCapstoneRequest? request = null)
    {
        if (shiningRoot?[ShiningStateProperty] is not JsonObject capstone)
            return false;

        if (!GetNodeBool(capstone["completed"]))
            return false;

        if (request == null)
            return true;

        return string.Equals(GetNodeString(capstone["requestId"]), request.RequestId, StringComparison.OrdinalIgnoreCase) &&
               string.Equals(GetNodeString(capstone["rewardPassiveId"]), request.RewardPassiveId, StringComparison.OrdinalIgnoreCase) &&
               string.Equals(GetNodeString(capstone["rewardRelicId"]), request.RewardRelicId, StringComparison.OrdinalIgnoreCase) &&
               GetNodeInt(capstone["radianceExperienceAtRequest"]) == request.RadianceExperienceAtRequest &&
               GetNodeInt(capstone["radianceTierAtRequest"]) == request.RadianceTierAtRequest;
    }

    public static bool HasLightIncarnate(JsonObject? soulRoot) =>
        soulRoot?[AfterlifeSpiritualConflictState.SoulStateProfileProperty] is JsonObject profile &&
        profile[CapstonesProperty] is JsonObject capstones &&
        capstones[LightIncarnateProperty] is JsonObject lightIncarnate &&
        (string.Equals(GetNodeString(lightIncarnate["passiveId"]), PassiveId, StringComparison.OrdinalIgnoreCase) ||
         string.Equals(GetNodeString(lightIncarnate["id"]), PassiveId, StringComparison.OrdinalIgnoreCase));

    public static int CountIncarnatedLightRelics(JsonObject? soulRoot)
    {
        var count = 0;
        foreach (var relic in EnumerateSoulRelics(soulRoot))
        {
            var id = GetNodeString(relic["relicId"]) ?? GetNodeString(relic["id"]);
            if (string.Equals(id, RelicId, StringComparison.OrdinalIgnoreCase))
                count++;
        }

        return count;
    }

    public static JsonObject CreateLightIncarnatePassive(SourceOfLightCapstoneRequest request) =>
        new()
        {
            ["id"] = PassiveId,
            ["passiveId"] = PassiveId,
            ["displayNameRu"] = "Воплощение Света",
            ["displayNameEn"] = "Light Incarnate",
            ["source"] = "source_of_light_capstone",
            ["status"] = "completed",
            ["requestId"] = request.RequestId,
            ["grantedAtTurn"] = request.CreatedAtTurn,
            ["afterlifeDiceFormulaVersion"] = "afterlife_spiritual_conflict_v1",
            ["leadContestantBonus"] = LeadDiceBonus,
            ["supporterBonus"] = SupportDiceBonus,
            ["coerciveOperationExtraBonus"] = CoerciveOperationExtraBonus,
            ["coerciveOperations"] = new JsonArray("force_incarnation", "force_binding", "break_binding")
        };

    public static JsonObject CreateCompletedShiningMarker(SourceOfLightCapstoneRequest request) =>
        new()
        {
            ["completed"] = true,
            ["requestId"] = request.RequestId,
            ["completedAtTurn"] = request.CreatedAtTurn,
            ["completedAtUtc"] = DateTime.UtcNow.ToString("O"),
            ["radianceExperienceAtRequest"] = request.RadianceExperienceAtRequest,
            ["radianceTierAtRequest"] = request.RadianceTierAtRequest,
            ["rewardPassiveId"] = request.RewardPassiveId,
            ["rewardRelicId"] = request.RewardRelicId,
            ["sceneNameRu"] = "Источник Света",
            ["sceneNameEn"] = "Source of Light"
        };

    public static JsonObject CreateIncarnatedLightRelic(SourceOfLightCapstoneRequest request)
    {
        var bonuses = new JsonObject();
        foreach (var characteristic in Characteristics.All)
            bonuses[characteristic] = MortalCharacteristicBonus;

        return new JsonObject
        {
            ["relicId"] = RelicId,
            ["name"] = "Воплощенный Свет",
            ["displayNameRu"] = "Воплощенный Свет",
            ["displayNameEn"] = "Incarnated Light",
            ["rarity"] = ShiningAbodeState.RarityLegendary,
            ["quality"] = ShiningAbodeState.RarityLegendary,
            ["shiningRarity"] = ShiningAbodeState.RarityRadiant,
            ["unique"] = true,
            ["source"] = "source_of_light_capstone",
            ["sourceRequestId"] = request.RequestId,
            ["description"] = "Реликвия Источника Света. В смертной жизни при экипировке даёт +25 ко всем основным характеристикам.",
            ["effects"] = new JsonObject
            {
                ["characteristicBonuses"] = bonuses
            }
        };
    }

    public static int SumLightIncarnatePlayerModifiers(JsonObject diceAudit)
    {
        if (diceAudit["modifierBreakdown"] is not JsonObject breakdown ||
            breakdown["player"] is not JsonArray modifiers)
        {
            return 0;
        }

        var total = 0;
        foreach (var modifier in modifiers.OfType<JsonObject>())
        {
            if (!ModifierReferencesLightIncarnate(modifier))
                continue;

            total += GetNodeInt(modifier["value"]);
        }

        return total;
    }

    public static bool IsCoerciveOperation(string? operationType) =>
        string.Equals(operationType, "force_incarnation", StringComparison.OrdinalIgnoreCase) ||
        string.Equals(operationType, "force_binding", StringComparison.OrdinalIgnoreCase) ||
        string.Equals(operationType, "break_binding", StringComparison.OrdinalIgnoreCase);

    public static string? GetNodeString(JsonNode? node)
    {
        if (node == null)
            return null;
        if (node is JsonValue value && value.TryGetValue<string>(out var str))
            return str;
        return null;
    }

    public static int GetNodeInt(JsonNode? node, int fallback = 0)
    {
        if (node is JsonValue value)
        {
            if (value.TryGetValue<int>(out var intValue))
                return intValue;
            if (value.TryGetValue<long>(out var longValue) && longValue is >= int.MinValue and <= int.MaxValue)
                return (int)longValue;
        }

        return fallback;
    }

    private static async Task<JsonObject?> ReadJsonObjectAsync(FileSystemManager fs, string path)
    {
        var raw = await fs.ReadFileAsync(path);
        if (string.IsNullOrWhiteSpace(raw))
            return null;

        try
        {
            return JsonNode.Parse(raw) as JsonObject;
        }
        catch
        {
            return null;
        }
    }

    private static bool GetNodeBool(JsonNode? node) =>
        node is JsonValue value && value.TryGetValue<bool>(out var result) && result;

    private static IEnumerable<JsonObject> EnumerateSoulRelics(JsonObject? soulRoot)
    {
        if (soulRoot?["soulRelics"] is JsonObject soulRelicsObject)
        {
            foreach (var collectionName in new[] { "equipped", "stored" })
            {
                if (soulRelicsObject[collectionName] is not JsonArray collection)
                    continue;

                foreach (var relic in collection.OfType<JsonObject>())
                    yield return relic;
            }
        }
        else if (soulRoot?["soulRelics"] is JsonArray flatCollection)
        {
            foreach (var relic in flatCollection.OfType<JsonObject>())
                yield return relic;
        }
    }

    private static bool ModifierReferencesLightIncarnate(JsonObject modifier)
    {
        foreach (var key in new[] { "source", "id", "modifierId", "effectId", "capstoneId", "passiveId" })
        {
            if (string.Equals(GetNodeString(modifier[key]), PassiveId, StringComparison.OrdinalIgnoreCase))
                return true;
        }

        return false;
    }
}
