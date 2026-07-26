using System.Text.Json;
using System.Text.Json.Nodes;

namespace BookOfEternityClient.Services;

internal sealed class MortalActorAcceptedTurnAuthority
{
    private const string MortalTrainingRequestKind = "mortal_teacher_showcase";

    private static readonly HashSet<string> DedicatedTrainingPatchFields = new(StringComparer.Ordinal)
    {
        "NPCId",
        "npcId",
        "id",
        "name",
        "npcName",
        "NPCName",
        "role",
        "trainingShowcase"
    };

    private static readonly string[] ActorIdentityFields = ["NPCId", "npcId", "id"];
    private static readonly string[] ActorNameFields = ["name", "npcName", "NPCName"];

    private readonly JsonObject _currentRoot;
    private readonly IReadOnlyList<NpcTradeRequestState.PendingNpcTradeInventoryRequest> _tradeRequests;
    private readonly IReadOnlyList<TrainingRequestState.PendingTrainingShowcaseRequest> _trainingRequests;

    private MortalActorAcceptedTurnAuthority(
        JsonObject currentRoot,
        IReadOnlyList<NpcTradeRequestState.PendingNpcTradeInventoryRequest> tradeRequests,
        IReadOnlyList<TrainingRequestState.PendingTrainingShowcaseRequest> trainingRequests)
    {
        _currentRoot = currentRoot;
        _tradeRequests = tradeRequests;
        _trainingRequests = trainingRequests;
    }

    internal static MortalActorAcceptedTurnAuthority Create(
        JsonObject currentRoot,
        string? validatedTradeRequestsJson,
        string? validatedTrainingRequestsJson) =>
        new(
            currentRoot,
            NpcTradeRequestState.ParseRequests(validatedTradeRequestsJson),
            TrainingRequestState.ParseRequests(validatedTrainingRequestsJson));

    internal bool AuthorizesFieldMutation(
        string actorId,
        JsonObject preTurnActor,
        JsonObject currentActor,
        string fieldName) =>
        fieldName switch
        {
            "tradeInventory" => AuthorizesTradeInventory(actorId, currentActor),
            "trainingShowcase" => AuthorizesTrainingShowcase(
                actorId,
                preTurnActor,
                currentActor,
                IsDedicatedTrainingPatch(currentActor)),
            _ => false
        };

    internal bool AuthorizesDedicatedTrainingPatch(
        string actorId,
        JsonObject preTurnActor,
        JsonObject currentActor) =>
        IsDedicatedTrainingPatch(currentActor) &&
        HasExactDedicatedPatchIdentityAndDisplay(actorId, preTurnActor, currentActor) &&
        AuthorizesTrainingShowcase(
            actorId,
            preTurnActor,
            currentActor,
            usePreTurnSource: true);

    private bool AuthorizesTradeInventory(string actorId, JsonObject currentActor)
    {
        if (currentActor["tradeInventory"] is not JsonObject tradeInventory)
            return false;

        var tradeCycleId = GetString(tradeInventory["tradeCycleId"]);
        if (tradeCycleId == null)
            return false;

        var matchingRequests = _tradeRequests
            .Where(request =>
                string.Equals(request.NpcId, actorId, StringComparison.Ordinal) &&
                string.Equals(request.TradeCycleId, tradeCycleId, StringComparison.Ordinal))
            .ToArray();
        if (matchingRequests.Length != 1)
            return false;

        var request = matchingRequests[0];
        if (string.IsNullOrWhiteSpace(request.RequestId) ||
            string.IsNullOrWhiteSpace(request.NpcName) ||
            string.IsNullOrWhiteSpace(request.MerchantProfile) ||
            request.DerivedTradeSlotCount <= 0 ||
            request.RefreshAfterWorldDate <= request.CreatedAtWorldDate ||
            !NpcTradeRequestState.InventoryMatchesRequestContract(tradeInventory, request) ||
            !HasExactTradeInventoryValues(tradeInventory, request))
        {
            return false;
        }

        if (_currentRoot[NpcTradeRequestState.UpdateReceiptsProperty] is not JsonArray receiptUpdates)
            return false;

        var matchingReceipts = receiptUpdates
            .OfType<JsonObject>()
            .Where(receipt =>
                HasExactTradeReceiptValues(receipt, request) &&
                NpcTradeRequestState.ReceiptMatchesRequestContract(
                    receipt,
                    request,
                    tradeInventory))
            .ToArray();
        return matchingReceipts.Length == 1;
    }

    private static bool HasExactTradeInventoryValues(
        JsonObject tradeInventory,
        NpcTradeRequestState.PendingNpcTradeInventoryRequest request)
    {
        if (!string.Equals(
                GetString(tradeInventory["tradeCycleId"]),
                request.TradeCycleId,
                StringComparison.Ordinal) ||
            GetInt(tradeInventory["generatedAtWorldDate"]) != request.CreatedAtWorldDate ||
            tradeInventory["items"] is not JsonArray items ||
            items.Count != request.DerivedTradeSlotCount)
        {
            return false;
        }

        return items.All(item =>
            item is JsonObject itemObject &&
            string.Equals(
                GetString(itemObject["merchantProfile"]),
                request.MerchantProfile,
                StringComparison.Ordinal));
    }

    private static bool HasExactTradeReceiptValues(
        JsonObject receipt,
        NpcTradeRequestState.PendingNpcTradeInventoryRequest request) =>
        string.Equals(GetString(receipt["requestId"]), request.RequestId, StringComparison.Ordinal) &&
        string.Equals(GetString(receipt["npcId"]), request.NpcId, StringComparison.Ordinal) &&
        string.Equals(GetString(receipt["npcName"]), request.NpcName, StringComparison.Ordinal) &&
        string.Equals(GetString(receipt["tradeCycleId"]), request.TradeCycleId, StringComparison.Ordinal) &&
        string.Equals(GetString(receipt["merchantProfile"]), request.MerchantProfile, StringComparison.Ordinal) &&
        GetInt(receipt["itemCount"]) == request.DerivedTradeSlotCount;

    private bool AuthorizesTrainingShowcase(
        string actorId,
        JsonObject preTurnActor,
        JsonObject currentActor,
        bool usePreTurnSource)
    {
        if (currentActor["trainingShowcase"] is not JsonObject showcase)
            return false;

        var requestId = GetString(showcase["requestId"]);
        var matchingRequests = _trainingRequests
            .Where(request =>
                string.Equals(request.RequestId, requestId, StringComparison.Ordinal) &&
                string.Equals(request.RequestKind, MortalTrainingRequestKind, StringComparison.Ordinal) &&
                string.Equals(request.SourceActorId, actorId, StringComparison.Ordinal))
            .ToArray();
        if (matchingRequests.Length != 1)
            return false;

        var request = matchingRequests[0];
        var requestHash = request.SourceActorSnapshotHash;
        var sourceActor = usePreTurnSource ? preTurnActor : currentActor;
        var expectedHash = TrainingService.ComputeSourceSnapshotHash(sourceActor);
        return !string.IsNullOrWhiteSpace(requestHash) &&
               string.Equals(GetString(showcase["requestKind"]), request.RequestKind, StringComparison.Ordinal) &&
               string.Equals(GetString(showcase["sourceActorId"]), request.SourceActorId, StringComparison.Ordinal) &&
               string.Equals(GetString(showcase["sourceActorName"]), request.SourceActorName, StringComparison.Ordinal) &&
               string.Equals(GetString(showcase["sourceActorSnapshotHash"]), requestHash, StringComparison.OrdinalIgnoreCase) &&
               string.Equals(requestHash, expectedHash, StringComparison.OrdinalIgnoreCase) &&
               GetInt(showcase["preparedAtTurn"]) == request.CreatedAtTurn &&
               showcase["offers"] is JsonArray;
    }

    private static bool IsDedicatedTrainingPatch(JsonObject actor) =>
        actor.ContainsKey("trainingShowcase") &&
        actor.All(property => DedicatedTrainingPatchFields.Contains(property.Key));

    private static bool HasExactDedicatedPatchIdentityAndDisplay(
        string actorId,
        JsonObject preTurnActor,
        JsonObject currentActor)
    {
        var hasIdentity = false;
        foreach (var field in ActorIdentityFields)
        {
            if (!currentActor.TryGetPropertyValue(field, out var value))
                continue;

            hasIdentity = true;
            if (!string.Equals(GetExactString(value), actorId, StringComparison.Ordinal))
                return false;
        }

        var baselineName = ActorNameFields
            .Select(field => GetExactString(preTurnActor[field]))
            .FirstOrDefault(value => value != null);
        var hasName = false;
        foreach (var field in ActorNameFields)
        {
            if (!currentActor.TryGetPropertyValue(field, out var value))
                continue;

            hasName = true;
            if (!string.Equals(GetExactString(value), baselineName, StringComparison.Ordinal))
                return false;
        }

        if (currentActor.TryGetPropertyValue("role", out var role) &&
            !JsonNode.DeepEquals(role, preTurnActor["role"]))
        {
            return false;
        }

        return hasIdentity && hasName && baselineName != null;
    }

    private static string? GetExactString(JsonNode? node)
    {
        if (node is JsonValue value &&
            value.TryGetValue<string>(out var text) &&
            !string.IsNullOrWhiteSpace(text))
        {
            return text;
        }

        return null;
    }

    private static string? GetString(JsonNode? node)
    {
        if (node is JsonValue value &&
            value.TryGetValue<string>(out var text) &&
            !string.IsNullOrWhiteSpace(text))
        {
            return text.Trim();
        }

        return null;
    }

    private static int GetInt(JsonNode? node)
    {
        if (node is not JsonValue value)
            return int.MinValue;
        if (value.TryGetValue<int>(out var intValue))
            return intValue;
        if (value.TryGetValue<long>(out var longValue) &&
            longValue is >= int.MinValue and <= int.MaxValue)
        {
            return (int)longValue;
        }

        return int.MinValue;
    }
}

internal static class MortalActorLegacyPromotionAuthority
{
    internal static IReadOnlySet<string> ResolveAuthorizedFields(
        JsonObject preTurnActor,
        JsonObject currentActor)
    {
        if (preTurnActor.ContainsKey(ActorMaterializationContract.PropertyName) ||
            currentActor[ActorMaterializationContract.PropertyName] is not JsonObject)
        {
            return new HashSet<string>(StringComparer.Ordinal);
        }

        using var preTurnDocument = JsonDocument.Parse(preTurnActor.ToJsonString());
        using var currentDocument = JsonDocument.Parse(currentActor.ToJsonString());
        var preTurn = preTurnDocument.RootElement;
        var current = currentDocument.RootElement;
        if (ActorMaterializationContract.ValidateCanonicalMortalNpc(
                current,
                "acceptedTurn.legacyPromotion",
                requireEnvelope: true)
            .Any(issue => issue.Severity == IssueSeverity.Error))
        {
            return new HashSet<string>(StringComparer.Ordinal);
        }

        var authorized = new HashSet<string>(StringComparer.Ordinal)
        {
            ActorMaterializationContract.PropertyName
        };
        var hasPromotion = false;

        if (ActorMaterializationContract.HasUsableMortalTeacherAuthority(current) &&
            !ActorMaterializationContract.HasUsableMortalTeacherAuthority(preTurn))
        {
            authorized.Add("teacherProfile");
            hasPromotion = true;
        }

        if (ActorMaterializationContract.HasExplicitMortalTradeAuthority(current) &&
            !ActorMaterializationContract.HasExplicitMortalTradeAuthority(preTurn))
        {
            authorized.Add("tradeState");
            hasPromotion = true;
        }

        if (ActorMaterializationContract.HasUsableMortalCombatSkill(current) &&
            !ActorMaterializationContract.HasUsableMortalCombatSkill(preTurn))
        {
            authorized.Add("activeSkills");
            authorized.Add("passiveSkills");
            hasPromotion = true;
        }

        if (HasActorBrainScope(current) && !HasActorBrainScope(preTurn))
        {
            authorized.Add("plans");
            authorized.Add("currentActivity");
            authorized.Add("completedActivities");
            hasPromotion = true;
        }

        if (!hasPromotion ||
            !JsonNode.DeepEquals(preTurnActor["inventory"], currentActor["inventory"]))
        {
            return new HashSet<string>(StringComparer.Ordinal);
        }

        return authorized;
    }

    private static bool HasActorBrainScope(JsonElement actor) =>
        HasNonEmptyString(actor, "plans") ||
        actor.TryGetProperty("currentActivity", out var currentActivity) &&
        currentActivity.ValueKind == JsonValueKind.Object ||
        HasNonEmptyArray(actor, "completedActivities");

    private static bool HasNonEmptyString(JsonElement actor, string propertyName) =>
        actor.TryGetProperty(propertyName, out var value) &&
        value.ValueKind == JsonValueKind.String &&
        !string.IsNullOrWhiteSpace(value.GetString());

    private static bool HasNonEmptyArray(JsonElement actor, string propertyName) =>
        actor.TryGetProperty(propertyName, out var value) &&
        value.ValueKind == JsonValueKind.Array &&
        value.GetArrayLength() > 0;
}
