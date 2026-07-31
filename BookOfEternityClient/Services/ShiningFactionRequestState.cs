using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using BookOfEternityClient.Configuration;
using BookOfEternityClient.Core;

namespace BookOfEternityClient.Services;

internal static class ShiningFactionRequestState
{
    public const string PendingFoundingsRequestPath = "game_state/control/pending_shining_faction_foundings.json";
    public const string PendingRealignmentsRequestPath = "game_state/control/pending_shining_faction_realignments.json";
    public const string PendingLeadershipTransitionsRequestPath = "game_state/control/pending_shining_faction_leadership_transitions.json";

    public const string RequestsProperty = "requests";
    public const int FactionFoundingCostFeathers = 25;
    public const int FactionFoundingCostLightSparks = 15;

    public const string RealignmentModeAcceptedTransfer = "accepted_transfer";
    public const string RealignmentModeRefusedTransfer = "refused_transfer";
    public const string RealignmentModeDepartureToNeutral = "departure_to_neutral";

    public const string TransitionModeAbdication = "abdication";
    public const string TransitionModePeacefulSuccession = "peaceful_succession";
    public const string TransitionModeRevolt = "revolt";

    public const string RequestStatusAccepted = "accepted";
    public const string RequestStatusRefused = "refused";
    public const string RequestStatusWithdrawn = "withdrawn";
    public const string RequestStatusDepartedToNeutral = "departed_to_neutral";

    private static readonly JsonSerializerOptions JsonOpts = SharedJsonOptions.PrettyCamelCaseUnsafeRelaxed;

    internal sealed record PendingPoliticalRequestReadState<TRequest>(
        bool FilePresent,
        bool IsMalformed,
        IReadOnlyList<TRequest> Requests)
        where TRequest : class;

    private static readonly HashSet<string> AllowedRealignmentModes = new(StringComparer.OrdinalIgnoreCase)
    {
        RealignmentModeAcceptedTransfer,
        RealignmentModeRefusedTransfer,
        RealignmentModeDepartureToNeutral
    };

    private static readonly HashSet<string> AllowedTransitionModes = new(StringComparer.OrdinalIgnoreCase)
    {
        TransitionModeAbdication,
        TransitionModePeacefulSuccession,
        TransitionModeRevolt
    };

    private static readonly HashSet<string> AllowedFoundingStatuses = new(StringComparer.OrdinalIgnoreCase)
    {
        RequestStatusAccepted,
        RequestStatusRefused,
        RequestStatusWithdrawn
    };

    private static readonly HashSet<string> AllowedRealignmentStatuses = new(StringComparer.OrdinalIgnoreCase)
    {
        RequestStatusAccepted,
        RequestStatusRefused,
        RequestStatusWithdrawn,
        RequestStatusDepartedToNeutral
    };

    private static readonly HashSet<string> AllowedLeadershipStatuses = new(StringComparer.OrdinalIgnoreCase)
    {
        RequestStatusAccepted,
        RequestStatusRefused,
        RequestStatusWithdrawn
    };

    private static readonly HashSet<string> AllowedLeadershipHistoryEventTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "abdicated",
        "succeeded",
        "revolted",
        "refused",
        "vacated"
    };

    public sealed class FactionCharterPayload
    {
        [JsonPropertyName("factionName")]
        public string FactionName { get; set; } = "";

        [JsonPropertyName("favoredArchetype")]
        public string FavoredArchetype { get; set; } = ShiningAbodeState.ProjectArchetypeAccord;

        [JsonPropertyName("patronEffectFamily")]
        public string PatronEffectFamily { get; set; } = ShiningAbodeState.EffectFamilySocial;

        [JsonPropertyName("summary")]
        public string Summary { get; set; } = "";
    }

    public sealed class PendingShiningFactionFoundingRequest
    {
        [JsonPropertyName("requestId")]
        public string RequestId { get; set; } = $"shining_founding_{Guid.NewGuid():N}";

        [JsonPropertyName("proposedFactionId")]
        public string ProposedFactionId { get; set; } = "";

        [JsonPropertyName("proposedHallId")]
        public string ProposedHallId { get; set; } = "";

        [JsonPropertyName("proposedHallName")]
        public string ProposedHallName { get; set; } = "";

        [JsonPropertyName("proposedHallDescription")]
        public string ProposedHallDescription { get; set; } = "";

        [JsonPropertyName("proposedHallServiceTags")]
        public List<string> ProposedHallServiceTags { get; set; } = new();

        [JsonPropertyName("charter")]
        public FactionCharterPayload Charter { get; set; } = new();

        [JsonPropertyName("supportingResidentIds")]
        public List<string> SupportingResidentIds { get; set; } = new();

        [JsonPropertyName("quotedCostFeathers")]
        public int QuotedCostFeathers { get; set; } = FactionFoundingCostFeathers;

        [JsonPropertyName("quotedCostLightSparks")]
        public int QuotedCostLightSparks { get; set; } = FactionFoundingCostLightSparks;

        [JsonPropertyName("reservedInkFeathersBefore")]
        public int ReservedInkFeathersBefore { get; set; } = -1;

        [JsonPropertyName("reservedLightSparksBefore")]
        public int ReservedLightSparksBefore { get; set; } = -1;

        [JsonPropertyName("createdAtTurn")]
        public int CreatedAtTurn { get; set; }

        [JsonPropertyName("createdAtUtc")]
        public string CreatedAtUtc { get; set; } = DateTime.UtcNow.ToString("o");
    }

    public sealed class PendingShiningFactionRealignmentRequest
    {
        [JsonPropertyName("requestId")]
        public string RequestId { get; set; } = $"shining_realignment_{Guid.NewGuid():N}";

        [JsonPropertyName("residentId")]
        public string ResidentId { get; set; } = "";

        [JsonPropertyName("residentName")]
        public string ResidentName { get; set; } = "";

        [JsonPropertyName("sourceFactionId")]
        public string SourceFactionId { get; set; } = "";

        [JsonPropertyName("sourceFactionName")]
        public string SourceFactionName { get; set; } = "";

        [JsonPropertyName("targetFactionId")]
        public string TargetFactionId { get; set; } = "";

        [JsonPropertyName("targetFactionName")]
        public string TargetFactionName { get; set; } = "";

        [JsonPropertyName("realignmentMode")]
        public string RealignmentMode { get; set; } = RealignmentModeAcceptedTransfer;

        [JsonPropertyName("factionLoyaltyLevel")]
        public int FactionLoyaltyLevel { get; set; }

        [JsonPropertyName("factionLoyaltyTier")]
        public string FactionLoyaltyTier { get; set; } = ShiningAbodeState.FactionLoyaltyTierAlienated;

        [JsonPropertyName("factionRestlessness")]
        public int FactionRestlessness { get; set; }

        [JsonPropertyName("factionRealignmentState")]
        public string FactionRealignmentState { get; set; } = ShiningAbodeState.FactionRealignmentStateReadyToRealign;

        [JsonPropertyName("createdAtTurn")]
        public int CreatedAtTurn { get; set; }

        [JsonPropertyName("createdAtUtc")]
        public string CreatedAtUtc { get; set; } = DateTime.UtcNow.ToString("o");
    }

    public sealed class PendingShiningFactionLeadershipTransitionRequest
    {
        [JsonPropertyName("requestId")]
        public string RequestId { get; set; } = $"shining_leadership_{Guid.NewGuid():N}";

        [JsonPropertyName("factionId")]
        public string FactionId { get; set; } = "";

        [JsonPropertyName("factionName")]
        public string FactionName { get; set; } = "";

        [JsonPropertyName("transitionMode")]
        public string TransitionMode { get; set; } = TransitionModePeacefulSuccession;

        [JsonPropertyName("incumbentHeadActorType")]
        public string IncumbentHeadActorType { get; set; } = "";

        [JsonPropertyName("incumbentHeadActorId")]
        public string IncumbentHeadActorId { get; set; } = "";

        [JsonPropertyName("candidateHeadActorType")]
        public string CandidateHeadActorType { get; set; } = "";

        [JsonPropertyName("candidateHeadActorId")]
        public string CandidateHeadActorId { get; set; } = "";

        [JsonPropertyName("supportingResidentIds")]
        public List<string> SupportingResidentIds { get; set; } = new();

        [JsonPropertyName("createdAtTurn")]
        public int CreatedAtTurn { get; set; }

        [JsonPropertyName("createdAtUtc")]
        public string CreatedAtUtc { get; set; } = DateTime.UtcNow.ToString("o");
    }

    public static bool IsSupportedRealignmentMode(string? value) => !string.IsNullOrWhiteSpace(value) && AllowedRealignmentModes.Contains(value);
    public static bool IsSupportedTransitionMode(string? value) => !string.IsNullOrWhiteSpace(value) && AllowedTransitionModes.Contains(value);
    public static bool IsSupportedFoundingStatus(string? value) => !string.IsNullOrWhiteSpace(value) && AllowedFoundingStatuses.Contains(value);
    public static bool IsSupportedRealignmentStatus(string? value) => !string.IsNullOrWhiteSpace(value) && AllowedRealignmentStatuses.Contains(value);
    public static bool IsSupportedLeadershipStatus(string? value) => !string.IsNullOrWhiteSpace(value) && AllowedLeadershipStatuses.Contains(value);
    public static bool IsSupportedLeadershipHistoryEventType(string? value) => !string.IsNullOrWhiteSpace(value) && AllowedLeadershipHistoryEventTypes.Contains(value);

    public static Task<string?> ValidateFoundingRequestAgainstCurrentStateAsync(
        FileSystemManager fs,
        PendingShiningFactionFoundingRequest request) =>
        ValidateFoundingRequestAgainstCurrentStateCoreAsync(fs, writeLease: null, request);

    internal static Task<string?> ValidateFoundingRequestAgainstCurrentStateAsync(
        FileSystemManager fs,
        FileSystemManager.CanonicalWriteLease writeLease,
        PendingShiningFactionFoundingRequest request) =>
        ValidateFoundingRequestAgainstCurrentStateCoreAsync(fs, writeLease, request);

    private static async Task<string?> ValidateFoundingRequestAgainstCurrentStateCoreAsync(
        FileSystemManager fs,
        FileSystemManager.CanonicalWriteLease? writeLease,
        PendingShiningFactionFoundingRequest request)
    {
        if (await IsRequestFileMalformedCoreAsync(
                fs,
                writeLease,
                PendingFoundingsRequestPath,
                static json => JsonSerializer.Deserialize<PendingShiningFactionFoundingRequest>(json, JsonOpts)))
        {
            return "pending_shining_faction_foundings.json повреждён. Исправьте или очистите pending founding contract перед созданием нового запроса.";
        }

        var soulRoot = await ReadJsonObjectAsync(fs, writeLease, "game_state/meta/soul_state.json");
        var shiningRoot = await ReadJsonObjectAsync(fs, writeLease, ShiningAbodeState.StatePath);
        var residentRoot = await ReadJsonObjectAsync(fs, writeLease, GuardianAbodeResidentState.StatePath);
        var guardiansRoot = await ReadJsonObjectAsync(fs, writeLease, "game_state/meta/guardians.json");
        if (shiningRoot == null)
            return "shining_abode_state.json недоступен.";
        var rawOwnerStateError = ShiningAbodeState.ValidateRawOwnerStateForActionableMode(shiningRoot);
        if (rawOwnerStateError != null)
            return rawOwnerStateError;

        ShiningAbodeState.NormalizeStateRoot(shiningRoot, residentRoot, guardiansRoot);
        var ordinaryModeError = ValidateOrdinaryActiveShiningMode(soulRoot, shiningRoot);
        if (ordinaryModeError != null)
            return ordinaryModeError;

        if (string.IsNullOrWhiteSpace(request.ProposedFactionId) ||
            string.IsNullOrWhiteSpace(request.ProposedHallId) ||
            string.IsNullOrWhiteSpace(request.ProposedHallName) ||
            string.IsNullOrWhiteSpace(request.ProposedHallDescription))
        {
            return "Founding request должен содержать proposedFactionId, proposedHallId, proposedHallName и proposedHallDescription.";
        }

        if (!ShiningAbodeState.IsSupportedProjectArchetype(request.Charter.FavoredArchetype) ||
            !ShiningAbodeState.IsSupportedEffectFamily(request.Charter.PatronEffectFamily) ||
            string.IsNullOrWhiteSpace(request.Charter.FactionName) ||
            string.IsNullOrWhiteSpace(request.Charter.Summary))
        {
            return "Charter founding-фракции должен содержать валидные factionName, favoredArchetype, patronEffectFamily и summary.";
        }

        if (request.QuotedCostFeathers != FactionFoundingCostFeathers ||
            request.QuotedCostLightSparks != FactionFoundingCostLightSparks)
        {
            return $"Founding request должен фиксировать canonical cost: {FactionFoundingCostFeathers} Ink Feathers и {FactionFoundingCostLightSparks} Light Sparks.";
        }

        var hallTags = request.ProposedHallServiceTags
            .Where(tag => !string.IsNullOrWhiteSpace(tag))
            .Select(tag => tag.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
        if (hallTags.Count is < 1 or > 2 || hallTags.Any(tag => !ShiningAbodeState.IsSupportedHallServiceTag(tag)))
            return "proposedHallServiceTags должны содержать 1..2 уникальных supported hall service tags.";

        var requiredPrimaryTag = MapPatronFamilyToHallServiceTag(request.Charter.PatronEffectFamily);
        if (!hallTags.Contains(requiredPrimaryTag, StringComparer.OrdinalIgnoreCase))
            return $"Hall должен включать обязательный service tag '{requiredPrimaryTag}' для patron family '{request.Charter.PatronEffectFamily}'.";

        var currentFactions = shiningRoot["factions"] as JsonArray ?? new JsonArray();
        if (currentFactions.OfType<JsonObject>().Any(faction =>
                string.Equals(GetNodeString(faction["factionId"]), request.ProposedFactionId, StringComparison.OrdinalIgnoreCase)))
        {
            return "Фракция с таким proposedFactionId уже materialized в Сияющей Обители.";
        }

        var currentHalls = shiningRoot["halls"] as JsonArray ?? new JsonArray();
        if (currentHalls.OfType<JsonObject>().Any(hall =>
                string.Equals(GetNodeString(hall["hallId"]), request.ProposedHallId, StringComparison.OrdinalIgnoreCase)))
        {
            return "Зал с таким proposedHallId уже materialized в Сияющей Обители.";
        }

        var foundingRequests = (await ReadFoundingRequestsCoreAsync(fs, writeLease)).ToList();
        if (!string.IsNullOrWhiteSpace(request.RequestId))
        {
            var duplicateRequestIdEntries = foundingRequests
                .Where(existing => string.Equals(existing.RequestId, request.RequestId, StringComparison.OrdinalIgnoreCase))
                .ToList();
            if (duplicateRequestIdEntries.Count > 1 ||
                duplicateRequestIdEntries.Any(existing => !IsSameFoundingLogicalRequest(existing, request)))
            {
                return "Pending founding requests используют duplicated requestId. requestId должен быть уникальным внутри pending founding set.";
            }
        }

        var otherFoundings = foundingRequests
            .Where(existing => !IsSameFoundingLogicalRequest(existing, request))
            .ToList();
        if (otherFoundings.Any(existing =>
                string.Equals(existing.ProposedFactionId, request.ProposedFactionId, StringComparison.OrdinalIgnoreCase)))
        {
            return "Pending founding request уже использует этот proposedFactionId. Дождитесь закрытия текущего founding request или выберите другой factionId.";
        }

        if (otherFoundings.Any(existing =>
                string.Equals(existing.ProposedHallId, request.ProposedHallId, StringComparison.OrdinalIgnoreCase)))
        {
            return "Pending founding request уже использует этот proposedHallId. Дождитесь закрытия текущего founding request или выберите другой hallId.";
        }

        if (otherFoundings.Count > 0)
            return "В Сияющей Обители может быть только один live player-soul founding request: все founding outcomes создают player_soul head и конфликтуют с single-head invariant.";

        var isLiveRequest = foundingRequests.Any(existing => IsSameFoundingLogicalRequest(existing, request));
        if (isLiveRequest)
        {
            var reservationError = ValidateFoundingResourceReservation(request, soulRoot, shiningRoot);
            if (reservationError != null)
                return reservationError;
        }

        if (HasCurrentHeadFaction(
                shiningRoot,
                ShiningAbodeState.HeadActorTypePlayerSoul,
                ShiningAbodeState.HeadActorTypePlayerSoul,
                excludingFactionId: null,
                out _))
            return "Игрок уже является текущим главой другой materialized Shining-фракции.";

        var supporterIds = request.SupportingResidentIds
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .Select(id => id.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
        if (supporterIds.Count < 3)
            return "Для founding нужны минимум 3 уникальных ascended supporters.";

        foreach (var supporterId in supporterIds)
        {
            var supporter = FindResident(residentRoot, supporterId);
            if (supporter == null)
                return $"Supporter resident '{supporterId}' не найден.";
            if (!string.Equals(GetNodeString(supporter["ascensionState"]), ShiningAbodeState.AscensionStateAscended, StringComparison.OrdinalIgnoreCase))
                return $"Supporter resident '{supporterId}' не находится в ascended state.";
            if (TryGetCurrentResidentHeadFactionId(shiningRoot, supporterId, out _))
                return $"Supporter resident '{supporterId}' сейчас является главой фракции и не может поддерживать founding.";
            if (await HasPendingOrdinaryTransferAsync(fs, writeLease, supporterId))
                return $"Supporter resident '{supporterId}' уже участвует в ordinary inter-Abode transfer.";
            if (await IsResidentLockedByPendingFlowInternalAsync(fs, writeLease, supporterId, excludeFoundingFactionId: request.ProposedFactionId, excludeRealignmentResidentId: null, excludeLeadershipFactionId: null))
                return $"Supporter resident '{supporterId}' уже заблокирован другим pending Shining flow.";
        }

        return null;
    }

    public static Task<string?> ValidateRealignmentRequestAgainstCurrentStateAsync(
        FileSystemManager fs,
        PendingShiningFactionRealignmentRequest request) =>
        ValidateRealignmentRequestAgainstCurrentStateCoreAsync(fs, writeLease: null, request);

    internal static Task<string?> ValidateRealignmentRequestAgainstCurrentStateAsync(
        FileSystemManager fs,
        FileSystemManager.CanonicalWriteLease writeLease,
        PendingShiningFactionRealignmentRequest request) =>
        ValidateRealignmentRequestAgainstCurrentStateCoreAsync(fs, writeLease, request);

    private static async Task<string?> ValidateRealignmentRequestAgainstCurrentStateCoreAsync(
        FileSystemManager fs,
        FileSystemManager.CanonicalWriteLease? writeLease,
        PendingShiningFactionRealignmentRequest request)
    {
        if (await IsRequestFileMalformedCoreAsync(
                fs,
                writeLease,
                PendingRealignmentsRequestPath,
                static json => JsonSerializer.Deserialize<PendingShiningFactionRealignmentRequest>(json, JsonOpts)))
        {
            return "pending_shining_faction_realignments.json повреждён. Исправьте или очистите pending realignment contract перед созданием нового запроса.";
        }

        var soulRoot = await ReadJsonObjectAsync(fs, writeLease, "game_state/meta/soul_state.json");
        var shiningRoot = await ReadJsonObjectAsync(fs, writeLease, ShiningAbodeState.StatePath);
        var residentRoot = await ReadJsonObjectAsync(fs, writeLease, GuardianAbodeResidentState.StatePath);
        var guardiansRoot = await ReadJsonObjectAsync(fs, writeLease, "game_state/meta/guardians.json");
        if (shiningRoot == null)
            return "shining_abode_state.json недоступен.";
        var rawOwnerStateError = ShiningAbodeState.ValidateRawOwnerStateForActionableMode(shiningRoot);
        if (rawOwnerStateError != null)
            return rawOwnerStateError;

        ShiningAbodeState.NormalizeStateRoot(shiningRoot, residentRoot, guardiansRoot);
        var ordinaryModeError = ValidateOrdinaryActiveShiningMode(soulRoot, shiningRoot);
        if (ordinaryModeError != null)
            return ordinaryModeError;

        if (!IsSupportedRealignmentMode(request.RealignmentMode))
            return "realignmentMode использует неподдерживаемое значение.";

        var resident = FindResident(residentRoot, request.ResidentId);
        if (resident == null)
            return $"Resident '{request.ResidentId}' не найден.";
        if (!string.Equals(GetNodeString(resident["ascensionState"]), ShiningAbodeState.AscensionStateAscended, StringComparison.OrdinalIgnoreCase))
            return "Faction realignment доступен только ascended residents.";
        if (TryGetCurrentResidentHeadFactionId(shiningRoot, request.ResidentId, out _))
            return "Current resident-head не может открыть Shining faction realignment до leadership resolution.";

        var actualSourceFactionId = GetNodeString(resident["shiningFactionId"]) ?? string.Empty;
        if (!string.Equals(actualSourceFactionId, request.SourceFactionId, StringComparison.OrdinalIgnoreCase))
            return "Resident уже не принадлежит указанной source faction.";

        var actualRealignmentState = GetNodeString(resident["factionRealignmentState"]) ?? string.Empty;
        if (!string.Equals(actualRealignmentState, ShiningAbodeState.FactionRealignmentStateReadyToRealign, StringComparison.OrdinalIgnoreCase))
            return "Faction realignment request допустим только для resident в состоянии ready_to_realign.";

        if (await HasForeignPendingRealignmentForResidentAsync(fs, writeLease, request.ResidentId, request.RequestId))
            return "Resident уже имеет live foreign pending Shining realignment contract.";
        if (await HasPendingOrdinaryTransferAsync(fs, writeLease, request.ResidentId))
            return "Resident уже участвует в ordinary inter-Abode transfer.";
        if (await IsResidentLockedByPendingFlowInternalAsync(fs, writeLease, request.ResidentId, excludeFoundingFactionId: null, excludeRealignmentResidentId: request.ResidentId, excludeLeadershipFactionId: null))
            return "Resident уже заблокирован другим pending Shining flow.";

        var sourceFaction = FindFaction(shiningRoot, request.SourceFactionId);
        if (sourceFaction == null)
            return "sourceFactionId не найден в текущем Shining state.";

        if (string.Equals(request.RealignmentMode, RealignmentModeDepartureToNeutral, StringComparison.OrdinalIgnoreCase))
        {
            if (!string.IsNullOrWhiteSpace(request.TargetFactionId) || !string.IsNullOrWhiteSpace(request.TargetFactionName))
                return "departure_to_neutral request не должен содержать target faction.";
        }
        else
        {
            if (string.IsNullOrWhiteSpace(request.TargetFactionId))
                return "Целевая фракция обязательна для направленного перехода.";
            if (string.Equals(request.TargetFactionId, request.SourceFactionId, StringComparison.OrdinalIgnoreCase))
                return "Целевая фракция должна отличаться от исходной фракции.";
            if (FindFaction(shiningRoot, request.TargetFactionId) == null)
                return "targetFactionId не найден в текущем Shining state.";
        }

        if (!string.Equals(request.FactionLoyaltyTier, ShiningAbodeState.ResolveFactionLoyaltyTier(request.FactionLoyaltyLevel), StringComparison.OrdinalIgnoreCase))
            return "factionLoyaltyTier должен совпадать с canonical tier от factionLoyaltyLevel.";
        if (!string.Equals(request.FactionRealignmentState, ShiningAbodeState.ResolveFactionRealignmentState(request.FactionLoyaltyLevel, request.FactionRestlessness), StringComparison.OrdinalIgnoreCase))
            return "factionRealignmentState должен совпадать с canonical derived state.";

        return null;
    }

    public static Task<string?> ValidateLeadershipTransitionRequestAgainstCurrentStateAsync(
        FileSystemManager fs,
        PendingShiningFactionLeadershipTransitionRequest request) =>
        ValidateLeadershipTransitionRequestAgainstCurrentStateCoreAsync(fs, writeLease: null, request);

    internal static Task<string?> ValidateLeadershipTransitionRequestAgainstCurrentStateAsync(
        FileSystemManager fs,
        FileSystemManager.CanonicalWriteLease writeLease,
        PendingShiningFactionLeadershipTransitionRequest request) =>
        ValidateLeadershipTransitionRequestAgainstCurrentStateCoreAsync(fs, writeLease, request);

    private static async Task<string?> ValidateLeadershipTransitionRequestAgainstCurrentStateCoreAsync(
        FileSystemManager fs,
        FileSystemManager.CanonicalWriteLease? writeLease,
        PendingShiningFactionLeadershipTransitionRequest request)
    {
        if (await IsRequestFileMalformedCoreAsync(
                fs,
                writeLease,
                PendingLeadershipTransitionsRequestPath,
                static json => JsonSerializer.Deserialize<PendingShiningFactionLeadershipTransitionRequest>(json, JsonOpts)))
        {
            return "pending_shining_faction_leadership_transitions.json повреждён. Исправьте или очистите pending leadership contract перед созданием нового запроса.";
        }

        var soulRoot = await ReadJsonObjectAsync(fs, writeLease, "game_state/meta/soul_state.json");
        var shiningRoot = await ReadJsonObjectAsync(fs, writeLease, ShiningAbodeState.StatePath);
        var residentRoot = await ReadJsonObjectAsync(fs, writeLease, GuardianAbodeResidentState.StatePath);
        var guardiansRoot = await ReadJsonObjectAsync(fs, writeLease, "game_state/meta/guardians.json");
        if (shiningRoot == null)
            return "shining_abode_state.json недоступен.";
        var rawOwnerStateError = ShiningAbodeState.ValidateRawOwnerStateForActionableMode(shiningRoot);
        if (rawOwnerStateError != null)
            return rawOwnerStateError;

        ShiningAbodeState.NormalizeStateRoot(shiningRoot, residentRoot, guardiansRoot);
        var ordinaryModeError = ValidateOrdinaryActiveShiningMode(soulRoot, shiningRoot);
        if (ordinaryModeError != null)
            return ordinaryModeError;

        if (!IsSupportedTransitionMode(request.TransitionMode))
            return "transitionMode использует неподдерживаемое значение.";

        var faction = FindFaction(shiningRoot, request.FactionId);
        if (faction == null)
            return "Указанная factionId не найдена в текущем Shining state.";

        var leadership = faction["leadership"] as JsonObject ?? new JsonObject();
        var leadershipState = GetNodeString(leadership["leadershipState"]);
        if (string.Equals(leadershipState, ShiningAbodeState.LeadershipStateVacant, StringComparison.OrdinalIgnoreCase))
        {
            return "Leadership transition недоступен для faction с vacant leadership: нет действующего incumbent head; vacancy filling пока не реализован.";
        }

        var actualIncumbentType = GetNodeString(leadership["headActorType"]) ?? string.Empty;
        var actualIncumbentId = GetNodeString(leadership["headActorId"]) ?? string.Empty;
        if (!string.Equals(actualIncumbentType, request.IncumbentHeadActorType, StringComparison.OrdinalIgnoreCase) ||
            !string.Equals(actualIncumbentId, request.IncumbentHeadActorId, StringComparison.OrdinalIgnoreCase))
        {
            return "Leadership request должен ссылаться на текущего incumbent head из faction.leadership.";
        }

        var incumbentLockError = await ValidateLeadershipActorPendingLocksAsync(
            fs,
            writeLease,
            "Incumbent",
            request.IncumbentHeadActorType,
            request.IncumbentHeadActorId,
            request.FactionId);
        if (incumbentLockError != null)
            return incumbentLockError;

        if (string.Equals(request.TransitionMode, TransitionModeRevolt, StringComparison.OrdinalIgnoreCase) &&
            !string.Equals(leadershipState, ShiningAbodeState.LeadershipStateContested, StringComparison.OrdinalIgnoreCase))
        {
            return "Revolt допустим только для faction в состоянии contested.";
        }

        var candidateRequired = !string.Equals(request.TransitionMode, TransitionModeAbdication, StringComparison.OrdinalIgnoreCase) ||
                                !string.IsNullOrWhiteSpace(request.CandidateHeadActorType) ||
                                !string.IsNullOrWhiteSpace(request.CandidateHeadActorId);
        if (candidateRequired)
        {
            if (!ShiningAbodeState.IsSupportedHeadActorType(request.CandidateHeadActorType))
                return "candidateHeadActorType использует неподдерживаемое значение.";
            if (string.IsNullOrWhiteSpace(request.CandidateHeadActorId))
                return "candidateHeadActorId обязателен для указанного transitionMode.";
            if (string.Equals(request.CandidateHeadActorType, request.IncumbentHeadActorType, StringComparison.OrdinalIgnoreCase) &&
                string.Equals(request.CandidateHeadActorId, request.IncumbentHeadActorId, StringComparison.OrdinalIgnoreCase))
            {
                return "Candidate должен отличаться от incumbent.";
            }

            var candidateError = ValidateLeadershipCandidate(shiningRoot, residentRoot, guardiansRoot, request.FactionId, request.CandidateHeadActorType, request.CandidateHeadActorId);
            if (candidateError != null)
                return candidateError;

            if (string.Equals(request.CandidateHeadActorType, ShiningAbodeState.HeadActorTypeGuardian, StringComparison.OrdinalIgnoreCase) &&
                IsActiveGuardianAutoFaction(faction, guardiansRoot, out var activeGuardianId) &&
                !string.Equals(request.CandidateHeadActorId, activeGuardianId, StringComparison.OrdinalIgnoreCase))
            {
                return "Active-guardian auto faction не может передать leadership другому guardian: normalizer вернёт headActorId к activeGuardianId.";
            }

            if (HasCurrentHeadFaction(
                    shiningRoot,
                    request.CandidateHeadActorType,
                    request.CandidateHeadActorId,
                    excludingFactionId: request.FactionId,
                    out var candidateCurrentHeadFactionId))
            {
                return $"Candidate уже является current head другой Shining-фракции '{candidateCurrentHeadFactionId}'.";
            }

            var candidateLockError = await ValidateLeadershipActorPendingLocksAsync(
                fs,
                writeLease,
                "Candidate",
                request.CandidateHeadActorType,
                request.CandidateHeadActorId,
                request.FactionId);
            if (candidateLockError != null)
                return candidateLockError;
        }

        if (await HasForeignPendingLeadershipForFactionAsync(fs, writeLease, request.FactionId, request.RequestId))
            return "Faction уже имеет live foreign pending Shining leadership contract.";

        var supporterIds = request.SupportingResidentIds
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .Select(id => id.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (string.Equals(request.TransitionMode, TransitionModeRevolt, StringComparison.OrdinalIgnoreCase) &&
            string.Equals(request.IncumbentHeadActorType, ShiningAbodeState.HeadActorTypeResident, StringComparison.OrdinalIgnoreCase) &&
            supporterIds.Contains(request.IncumbentHeadActorId, StringComparer.OrdinalIgnoreCase))
        {
            return "Incumbent resident не может быть listed supporter собственного revolt.";
        }

        var ascendedFactionResidents = CountAscendedFactionResidents(residentRoot, request.FactionId);
        var minimumSupport = string.Equals(request.TransitionMode, TransitionModePeacefulSuccession, StringComparison.OrdinalIgnoreCase)
            ? Math.Max(2, (int)Math.Ceiling(ascendedFactionResidents / 3.0))
            : string.Equals(request.TransitionMode, TransitionModeRevolt, StringComparison.OrdinalIgnoreCase)
                ? Math.Max(3, (int)Math.Ceiling(ascendedFactionResidents / 2.0))
                : 0;

        if (supporterIds.Count < minimumSupport)
            return $"Для {request.TransitionMode} нужны минимум {minimumSupport} same-faction ascended supporters.";

        foreach (var supporterId in supporterIds)
        {
            var supporter = FindResident(residentRoot, supporterId);
            if (supporter == null)
                return $"Supporter resident '{supporterId}' не найден.";
            if (!string.Equals(GetNodeString(supporter["ascensionState"]), ShiningAbodeState.AscensionStateAscended, StringComparison.OrdinalIgnoreCase))
                return $"Supporter resident '{supporterId}' не находится в ascended state.";
            if (!string.Equals(GetNodeString(supporter["shiningFactionId"]), request.FactionId, StringComparison.OrdinalIgnoreCase))
                return $"Supporter resident '{supporterId}' не принадлежит той же фракции.";

            var isRequestResident = string.Equals(request.CandidateHeadActorType, ShiningAbodeState.HeadActorTypeResident, StringComparison.OrdinalIgnoreCase) &&
                                    string.Equals(request.CandidateHeadActorId, supporterId, StringComparison.OrdinalIgnoreCase);
            var isIncumbentResident = string.Equals(request.IncumbentHeadActorType, ShiningAbodeState.HeadActorTypeResident, StringComparison.OrdinalIgnoreCase) &&
                                      string.Equals(request.IncumbentHeadActorId, supporterId, StringComparison.OrdinalIgnoreCase);
            if (TryGetCurrentResidentHeadFactionId(shiningRoot, supporterId, out var headFactionId) &&
                !string.Equals(headFactionId, request.FactionId, StringComparison.OrdinalIgnoreCase) &&
                !isRequestResident &&
                !isIncumbentResident)
            {
                return $"Supporter resident '{supporterId}' уже является current head другой фракции.";
            }

            if (await HasPendingOrdinaryTransferAsync(fs, writeLease, supporterId))
                return $"Supporter resident '{supporterId}' уже участвует в ordinary inter-Abode transfer.";
            if (await IsResidentLockedByPendingFlowInternalAsync(fs, writeLease, supporterId, excludeFoundingFactionId: null, excludeRealignmentResidentId: null, excludeLeadershipFactionId: request.FactionId))
                return $"Supporter resident '{supporterId}' уже заблокирован другим pending Shining flow.";
        }

        return null;
    }

    public static async Task<IReadOnlyList<PendingShiningFactionFoundingRequest>> ReadFoundingRequestsAsync(FileSystemManager fs) =>
        (await ReadRequestsStateAsync(fs, PendingFoundingsRequestPath, static json => JsonSerializer.Deserialize<PendingShiningFactionFoundingRequest>(json, JsonOpts))).Requests;

    internal static async Task<IReadOnlyList<PendingShiningFactionFoundingRequest>> ReadFoundingRequestsAsync(
        FileSystemManager fs,
        FileSystemManager.CanonicalWriteLease writeLease) =>
        (await ReadRequestsStateAsync(fs, writeLease, PendingFoundingsRequestPath, static json => JsonSerializer.Deserialize<PendingShiningFactionFoundingRequest>(json, JsonOpts))).Requests;

    private static Task<IReadOnlyList<PendingShiningFactionFoundingRequest>> ReadFoundingRequestsCoreAsync(
        FileSystemManager fs,
        FileSystemManager.CanonicalWriteLease? writeLease) =>
        writeLease == null
            ? ReadFoundingRequestsAsync(fs)
            : ReadFoundingRequestsAsync(fs, writeLease);

    public static async Task<IReadOnlyList<PendingShiningFactionRealignmentRequest>> ReadRealignmentRequestsAsync(FileSystemManager fs) =>
        (await ReadRequestsStateAsync(fs, PendingRealignmentsRequestPath, static json => JsonSerializer.Deserialize<PendingShiningFactionRealignmentRequest>(json, JsonOpts))).Requests;

    internal static async Task<IReadOnlyList<PendingShiningFactionRealignmentRequest>> ReadRealignmentRequestsAsync(
        FileSystemManager fs,
        FileSystemManager.CanonicalWriteLease writeLease) =>
        (await ReadRequestsStateAsync(fs, writeLease, PendingRealignmentsRequestPath, static json => JsonSerializer.Deserialize<PendingShiningFactionRealignmentRequest>(json, JsonOpts))).Requests;

    public static async Task<IReadOnlyList<PendingShiningFactionLeadershipTransitionRequest>> ReadLeadershipTransitionRequestsAsync(FileSystemManager fs) =>
        (await ReadRequestsStateAsync(fs, PendingLeadershipTransitionsRequestPath, static json => JsonSerializer.Deserialize<PendingShiningFactionLeadershipTransitionRequest>(json, JsonOpts))).Requests;

    internal static async Task<IReadOnlyList<PendingShiningFactionLeadershipTransitionRequest>> ReadLeadershipTransitionRequestsAsync(
        FileSystemManager fs,
        FileSystemManager.CanonicalWriteLease writeLease) =>
        (await ReadRequestsStateAsync(fs, writeLease, PendingLeadershipTransitionsRequestPath, static json => JsonSerializer.Deserialize<PendingShiningFactionLeadershipTransitionRequest>(json, JsonOpts))).Requests;

    public static IReadOnlyList<PendingShiningFactionFoundingRequest> ReadFoundingRequests(string? json) =>
        ReadRequestsState(json, !string.IsNullOrEmpty(json), static itemJson => JsonSerializer.Deserialize<PendingShiningFactionFoundingRequest>(itemJson, JsonOpts)).Requests;

    public static IReadOnlyList<PendingShiningFactionRealignmentRequest> ReadRealignmentRequests(string? json) =>
        ReadRequestsState(json, !string.IsNullOrEmpty(json), static itemJson => JsonSerializer.Deserialize<PendingShiningFactionRealignmentRequest>(itemJson, JsonOpts)).Requests;

    public static IReadOnlyList<PendingShiningFactionLeadershipTransitionRequest> ReadLeadershipTransitionRequests(string? json) =>
        ReadRequestsState(json, !string.IsNullOrEmpty(json), static itemJson => JsonSerializer.Deserialize<PendingShiningFactionLeadershipTransitionRequest>(itemJson, JsonOpts)).Requests;

    public static Task WriteFoundingRequestAsync(FileSystemManager fs, PendingShiningFactionFoundingRequest request) =>
        WriteSingleRequestAsync(
            fs,
            PendingFoundingsRequestPath,
            request,
            static (_, _) => true);

    internal static Task WriteFoundingRequestAsync(
        FileSystemManager fs,
        FileSystemManager.CanonicalWriteLease writeLease,
        PendingShiningFactionFoundingRequest request) =>
        WriteSingleRequestAsync(
            fs,
            writeLease,
            PendingFoundingsRequestPath,
            request,
            static (_, _) => true);

    public static Task WriteRealignmentRequestAsync(FileSystemManager fs, PendingShiningFactionRealignmentRequest request) =>
        WriteSingleRequestAsync(
            fs,
            PendingRealignmentsRequestPath,
            request,
            static (existing, pending) =>
                string.Equals(existing.RequestId, pending.RequestId, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(existing.ResidentId, pending.ResidentId, StringComparison.OrdinalIgnoreCase));

    internal static Task WriteRealignmentRequestAsync(
        FileSystemManager fs,
        FileSystemManager.CanonicalWriteLease writeLease,
        PendingShiningFactionRealignmentRequest request) =>
        WriteSingleRequestAsync(
            fs,
            writeLease,
            PendingRealignmentsRequestPath,
            request,
            static (existing, pending) =>
                string.Equals(existing.RequestId, pending.RequestId, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(existing.ResidentId, pending.ResidentId, StringComparison.OrdinalIgnoreCase));

    public static Task WriteLeadershipTransitionRequestAsync(FileSystemManager fs, PendingShiningFactionLeadershipTransitionRequest request) =>
        WriteSingleRequestAsync(fs, PendingLeadershipTransitionsRequestPath, request, static item => item.FactionId);

    internal static Task WriteLeadershipTransitionRequestAsync(
        FileSystemManager fs,
        FileSystemManager.CanonicalWriteLease writeLease,
        PendingShiningFactionLeadershipTransitionRequest request) =>
        WriteSingleRequestAsync(
            fs,
            writeLease,
            PendingLeadershipTransitionsRequestPath,
            request,
            static (existing, pending) =>
                string.Equals(existing.RequestId, pending.RequestId, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(existing.FactionId, pending.FactionId, StringComparison.OrdinalIgnoreCase));

    public static void ClearFoundingRequests(FileSystemManager fs) => fs.DeleteFile(PendingFoundingsRequestPath);
    public static void ClearRealignmentRequests(FileSystemManager fs) => fs.DeleteFile(PendingRealignmentsRequestPath);
    public static void ClearLeadershipTransitionRequests(FileSystemManager fs) => fs.DeleteFile(PendingLeadershipTransitionsRequestPath);
    public static void ClearAllRequests(FileSystemManager fs)
    {
        ClearFoundingRequests(fs);
        ClearRealignmentRequests(fs);
        ClearLeadershipTransitionRequests(fs);
    }

    public static async Task EnsureHealthyAsync(FileSystemManager fs, string? currentRealm)
    {
        if (!RealmSemantics.HasResolvedRealm(currentRealm))
            return;

        if (!IsShiningRealm(currentRealm))
        {
            await ClearOnlyValidEmptyRequestsAsync(fs, PendingFoundingsRequestPath,
                static json => JsonSerializer.Deserialize<PendingShiningFactionFoundingRequest>(json, JsonOpts));
            await ClearOnlyValidEmptyRequestsAsync(fs, PendingRealignmentsRequestPath,
                static json => JsonSerializer.Deserialize<PendingShiningFactionRealignmentRequest>(json, JsonOpts));
            await ClearOnlyValidEmptyRequestsAsync(fs, PendingLeadershipTransitionsRequestPath,
                static json => JsonSerializer.Deserialize<PendingShiningFactionLeadershipTransitionRequest>(json, JsonOpts));
            return;
        }

        var hasPendingFiles = fs.FileExists(PendingFoundingsRequestPath) ||
                              fs.FileExists(PendingRealignmentsRequestPath) ||
                              fs.FileExists(PendingLeadershipTransitionsRequestPath);
        if (!hasPendingFiles)
            return;

        var shiningRoot = await ReadJsonObjectAsync(fs, ShiningAbodeState.StatePath);
        if (shiningRoot == null)
            return;
        if (ShiningAbodeState.ValidateRawOwnerStateForActionableMode(shiningRoot) != null)
            return;

        if (!string.Equals(GetNodeString(shiningRoot["availability"]), ShiningAbodeState.AvailabilityActive, StringComparison.OrdinalIgnoreCase))
        {
            await ClearOnlyValidEmptyRequestsAsync(fs, PendingFoundingsRequestPath,
                static json => JsonSerializer.Deserialize<PendingShiningFactionFoundingRequest>(json, JsonOpts));
            await ClearOnlyValidEmptyRequestsAsync(fs, PendingRealignmentsRequestPath,
                static json => JsonSerializer.Deserialize<PendingShiningFactionRealignmentRequest>(json, JsonOpts));
            await ClearOnlyValidEmptyRequestsAsync(fs, PendingLeadershipTransitionsRequestPath,
                static json => JsonSerializer.Deserialize<PendingShiningFactionLeadershipTransitionRequest>(json, JsonOpts));
            return;
        }
        if (ShiningAbodeState.GetPreparedIncarnationPackageMode(shiningRoot) != ShiningAbodeState.PreparedIncarnationPackageMode.Absent)
            return;

        var residentRoot = await ReadJsonObjectAsync(fs, GuardianAbodeResidentState.StatePath);
        if (residentRoot != null)
            GuardianAbodeResidentState.NormalizeShape(residentRoot);

        var foundingState = await ReadRequestsStateAsync(
            fs,
            PendingFoundingsRequestPath,
            static json => JsonSerializer.Deserialize<PendingShiningFactionFoundingRequest>(json, JsonOpts));
        if (!foundingState.IsMalformed && foundingState.Requests.Count > 0)
        {
            var unresolvedFoundings = foundingState.Requests
                .Where(request => !HasMatchingFoundingClosure(shiningRoot, residentRoot, request))
                .ToList();
            if (unresolvedFoundings.Count != foundingState.Requests.Count)
                await PersistRequestsAsync(fs, PendingFoundingsRequestPath, unresolvedFoundings);
        }

        var realignmentState = await ReadRequestsStateAsync(
            fs,
            PendingRealignmentsRequestPath,
            static json => JsonSerializer.Deserialize<PendingShiningFactionRealignmentRequest>(json, JsonOpts));
        if (!realignmentState.IsMalformed && realignmentState.Requests.Count > 0)
        {
            var unresolvedRealignments = realignmentState.Requests
                .Where(request => !HasMatchingRealignmentClosure(shiningRoot, residentRoot, request))
                .ToList();
            if (unresolvedRealignments.Count != realignmentState.Requests.Count)
                await PersistRequestsAsync(fs, PendingRealignmentsRequestPath, unresolvedRealignments);
        }

        var leadershipState = await ReadRequestsStateAsync(
            fs,
            PendingLeadershipTransitionsRequestPath,
            static json => JsonSerializer.Deserialize<PendingShiningFactionLeadershipTransitionRequest>(json, JsonOpts));
        if (!leadershipState.IsMalformed && leadershipState.Requests.Count > 0)
        {
            var unresolvedLeadershipTransitions = leadershipState.Requests
                .Where(request => !HasMatchingLeadershipClosure(shiningRoot, request))
                .ToList();
            if (unresolvedLeadershipTransitions.Count != leadershipState.Requests.Count)
                await PersistRequestsAsync(fs, PendingLeadershipTransitionsRequestPath, unresolvedLeadershipTransitions);
        }
    }

    private static async Task ClearOnlyValidEmptyRequestsAsync<TRequest>(
        FileSystemManager fs,
        string path,
        Func<string, TRequest?> deserialize)
        where TRequest : class
    {
        var state = await ReadRequestsStateAsync(fs, path, deserialize);
        if (!state.IsMalformed && state.Requests.Count == 0 && fs.FileExists(path))
            fs.DeleteFile(path);
    }

    public static async Task<string?> BuildSystemReminderFragmentAsync(FileSystemManager fs, string? currentRealm)
    {
        if (!RealmSemantics.IsAfterlifeRealm(currentRealm))
            return null;

        var foundingMalformed = await IsRequestFileMalformedAsync(
            fs,
            PendingFoundingsRequestPath,
            static json => JsonSerializer.Deserialize<PendingShiningFactionFoundingRequest>(json, JsonOpts));
        var realignmentMalformed = await IsRequestFileMalformedAsync(
            fs,
            PendingRealignmentsRequestPath,
            static json => JsonSerializer.Deserialize<PendingShiningFactionRealignmentRequest>(json, JsonOpts));
        var leadershipMalformed = await IsRequestFileMalformedAsync(
            fs,
            PendingLeadershipTransitionsRequestPath,
            static json => JsonSerializer.Deserialize<PendingShiningFactionLeadershipTransitionRequest>(json, JsonOpts));
        if (foundingMalformed || realignmentMalformed || leadershipMalformed)
        {
            var brokenFiles = new List<string>();
            if (foundingMalformed)
                brokenFiles.Add(Path.GetFileName(PendingFoundingsRequestPath));
            if (realignmentMalformed)
                brokenFiles.Add(Path.GetFileName(PendingRealignmentsRequestPath));
            if (leadershipMalformed)
                brokenFiles.Add(Path.GetFileName(PendingLeadershipTransitionsRequestPath));

            return "SHINING ABODE POLITICAL REQUEST CORRUPTION:\n" +
                   $"  - unreadable или malformed pending file(s): {string.Join(", ", brokenFiles)}.\n" +
                   "  - Preserve the file(s) and repair the political request contract before authoring new requests.";
        }

        var foundingRequests = await ReadFoundingRequestsAsync(fs);
        var realignmentRequests = await ReadRealignmentRequestsAsync(fs);
        var leadershipRequests = await ReadLeadershipTransitionRequestsAsync(fs);
        if (foundingRequests.Count == 0 && realignmentRequests.Count == 0 && leadershipRequests.Count == 0)
            return null;

        if (!IsShiningRealm(currentRealm))
        {
            var wrongRealm = new StringBuilder();
            wrongRealm.AppendLine("SHINING ABODE POLITICAL REQUESTS WRONG REALM:");
            wrongRealm.AppendLine("  - currentRealm is not Shining Abode, so Shining political pending files are repair-only context here.");
            wrongRealm.AppendLine("  - Preserve the files; do not resolve factionFoundingReceipts[], factionRealignmentReceipts[], leadershipReceipts[], or Shining political state from this Chaos Sea turn.");
            wrongRealm.AppendLine("  - Re-enter Shining Abode or repair/clear the contracts through the proper Shining runtime path before ordinary processing.");
            wrongRealm.AppendLine($"  - Pending requests detected: {foundingRequests.Count + realignmentRequests.Count + leadershipRequests.Count}");
            foreach (var request in foundingRequests)
                AppendSerializedJsonBlock(wrongRealm, "Wrong-realm pending founding DTO", request);
            foreach (var request in realignmentRequests)
                AppendSerializedJsonBlock(wrongRealm, "Wrong-realm pending realignment DTO", request);
            foreach (var request in leadershipRequests)
                AppendSerializedJsonBlock(wrongRealm, "Wrong-realm pending leadership DTO", request);
            return wrongRealm.ToString();
        }

        if (IsShiningRealm(currentRealm))
        {
            var shiningRoot = await ReadJsonObjectAsync(fs, ShiningAbodeState.StatePath);
            var packageMode = ShiningAbodeState.GetPreparedIncarnationPackageMode(shiningRoot);
            if (packageMode != ShiningAbodeState.PreparedIncarnationPackageMode.Absent)
            {
                var blocked = new StringBuilder();
                blocked.AppendLine("SHINING ABODE POLITICAL REQUESTS BLOCKED:");
                blocked.AppendLine(packageMode == ShiningAbodeState.PreparedIncarnationPackageMode.ValidHandoff
                    ? "  - Valid preparedIncarnationPackage puts the realm in pending-bootstrap handoff mode."
                    : "  - preparedIncarnationPackage is malformed or fails bootstrap validation, so the realm mode is fail-closed.");
                blocked.AppendLine("  - Preserve Shining political pending files; do not delete, truncate, or process ordinary Shining politics during this mode.");
                blocked.AppendLine($"  - Pending requests detected: {foundingRequests.Count + realignmentRequests.Count + leadershipRequests.Count}");
                foreach (var request in foundingRequests)
                    AppendSerializedJsonBlock(blocked, "Blocked pending founding DTO", request);
                foreach (var request in realignmentRequests)
                    AppendSerializedJsonBlock(blocked, "Blocked pending realignment DTO", request);
                foreach (var request in leadershipRequests)
                    AppendSerializedJsonBlock(blocked, "Blocked pending leadership DTO", request);
                return blocked.ToString();
            }
        }

        var sb = new StringBuilder();
        sb.AppendLine("SHINING ABODE POLITICAL REQUESTS:");
        sb.AppendLine("  - Treat every pending Shining political file as client-authored contract, not as optional prose.");
        sb.AppendLine("  - Resolve founding/realignment/leadership through canonical receipts and state mutation; no silent political rewrites.");

        foreach (var request in foundingRequests)
        {
            sb.AppendLine($"  - Founding pending: {request.Charter.FactionName} ({request.ProposedFactionId}) with {request.SupportingResidentIds.Count} supporters.");
            AppendSerializedJsonBlock(sb, "Full pending founding DTO", request);
        }

        foreach (var request in realignmentRequests)
        {
            sb.AppendLine($"  - Realignment pending: {request.ResidentName} {request.SourceFactionName} -> {(string.IsNullOrWhiteSpace(request.TargetFactionName) ? "neutral" : request.TargetFactionName)} [{request.RealignmentMode}].");
            AppendSerializedJsonBlock(sb, "Full pending realignment DTO", request);
        }

        foreach (var request in leadershipRequests)
        {
            sb.AppendLine($"  - Leadership pending: {request.FactionName} via {request.TransitionMode}; candidate={request.CandidateHeadActorType}:{request.CandidateHeadActorId}.");
            AppendSerializedJsonBlock(sb, "Full pending leadership DTO", request);
        }

        return sb.ToString();
    }

    private static void AppendSerializedJsonBlock(StringBuilder sb, string title, object payload)
    {
        sb.AppendLine($"  - {title}:");
        var json = JsonSerializer.Serialize(payload, JsonOpts).Replace("\r\n", "\n", StringComparison.Ordinal);
        foreach (var line in json.Split('\n'))
            sb.AppendLine($"    {line}");
    }

    public static async Task<bool> IsResidentLockedByPendingFlowAsync(FileSystemManager fs, string residentId)
    {
        if (string.IsNullOrWhiteSpace(residentId))
            return false;

        return await IsResidentLockedByPendingFlowInternalAsync(fs, residentId, null, null, null);
    }

    private static async Task<IReadOnlyList<TRequest>> ReadRequestsAsync<TRequest>(
        FileSystemManager fs,
        string path,
        Func<string, TRequest?> deserialize)
        where TRequest : class
    {
        var json = await fs.ReadFileAsync(path);
        return ReadRequestsState(json, fs.FileExists(path), deserialize).Requests;
    }

    internal static async Task<bool> IsRequestFileMalformedAsync<TRequest>(
        FileSystemManager fs,
        string path,
        Func<string, TRequest?> deserialize)
        where TRequest : class
    {
        var json = await fs.ReadFileAsync(path);
        return ReadRequestsState(json, fs.FileExists(path), deserialize).IsMalformed;
    }

    internal static async Task<bool> IsRequestFileMalformedAsync<TRequest>(
        FileSystemManager fs,
        FileSystemManager.CanonicalWriteLease writeLease,
        string path,
        Func<string, TRequest?> deserialize)
        where TRequest : class
    {
        var json = await fs.ReadFileAsync(writeLease, path);
        return ReadRequestsState(json, fs.FileExists(writeLease, path), deserialize).IsMalformed;
    }

    private static Task<bool> IsRequestFileMalformedCoreAsync<TRequest>(
        FileSystemManager fs,
        FileSystemManager.CanonicalWriteLease? writeLease,
        string path,
        Func<string, TRequest?> deserialize)
        where TRequest : class =>
        writeLease == null
            ? IsRequestFileMalformedAsync(fs, path, deserialize)
            : IsRequestFileMalformedAsync(fs, writeLease, path, deserialize);

    private static async Task<PendingPoliticalRequestReadState<TRequest>> ReadRequestsStateAsync<TRequest>(
        FileSystemManager fs,
        string path,
        Func<string, TRequest?> deserialize)
        where TRequest : class
    {
        var json = await fs.ReadFileAsync(path);
        return ReadRequestsState(json, fs.FileExists(path), deserialize);
    }

    private static async Task<PendingPoliticalRequestReadState<TRequest>> ReadRequestsStateAsync<TRequest>(
        FileSystemManager fs,
        FileSystemManager.CanonicalWriteLease writeLease,
        string path,
        Func<string, TRequest?> deserialize)
        where TRequest : class
    {
        var json = await fs.ReadFileAsync(writeLease, path);
        return ReadRequestsState(json, fs.FileExists(writeLease, path), deserialize);
    }

    private static PendingPoliticalRequestReadState<TRequest> ReadRequestsState<TRequest>(string? json, bool filePresent, Func<string, TRequest?> deserialize)
        where TRequest : class
    {
        if (string.IsNullOrWhiteSpace(json))
            return new PendingPoliticalRequestReadState<TRequest>(filePresent, filePresent, Array.Empty<TRequest>());

        try
        {
            using var document = JsonDocument.Parse(json);
            if (document.RootElement.ValueKind != JsonValueKind.Object ||
                !document.RootElement.TryGetProperty(RequestsProperty, out var requestsNode) ||
                requestsNode.ValueKind != JsonValueKind.Array)
            {
                return new PendingPoliticalRequestReadState<TRequest>(filePresent, true, Array.Empty<TRequest>());
            }

            var result = new List<TRequest>();
            foreach (var requestNode in requestsNode.EnumerateArray())
            {
                var request = deserialize(requestNode.GetRawText());
                if (request == null)
                    return new PendingPoliticalRequestReadState<TRequest>(filePresent, true, Array.Empty<TRequest>());

                result.Add(request);
            }

            return new PendingPoliticalRequestReadState<TRequest>(filePresent, false, result);
        }
        catch
        {
            return new PendingPoliticalRequestReadState<TRequest>(filePresent, true, Array.Empty<TRequest>());
        }
    }

    private static Task<JsonObject?> ReadJsonObjectAsync(FileSystemManager fs, string path) =>
        ReadJsonObjectAsync(fs, writeLease: null, path);

    private static async Task<JsonObject?> ReadJsonObjectAsync(
        FileSystemManager fs,
        FileSystemManager.CanonicalWriteLease? writeLease,
        string path)
    {
        var json = writeLease == null
            ? await fs.ReadFileAsync(path)
            : await fs.ReadFileAsync(writeLease, path);
        if (string.IsNullOrWhiteSpace(json))
            return null;

        try
        {
            return JsonNode.Parse(json) as JsonObject;
        }
        catch
        {
            return null;
        }
    }

    private static string? ValidateOrdinaryActiveShiningMode(JsonObject? soulRoot, JsonObject shiningRoot)
    {
        var currentRealm = GetNodeString(soulRoot?["currentRealm"]);
        if (!string.Equals(currentRealm, "Shining Abode", StringComparison.OrdinalIgnoreCase) &&
            !string.Equals(currentRealm, "Сияющая Обитель", StringComparison.OrdinalIgnoreCase))
        {
            return "Shining political request допустим только при currentRealm = Shining Abode.";
        }

        if (!string.Equals(GetNodeString(shiningRoot["availability"]), ShiningAbodeState.AvailabilityActive, StringComparison.OrdinalIgnoreCase))
            return "Shining political request допустим только при availability = active.";
        var packageMode = ShiningAbodeState.GetPreparedIncarnationPackageMode(shiningRoot);
        if (packageMode == ShiningAbodeState.PreparedIncarnationPackageMode.ValidHandoff)
            return "Shining political request недопустим, пока preparedIncarnationPackage ожидает bootstrap.";
        if (packageMode == ShiningAbodeState.PreparedIncarnationPackageMode.InvalidFault)
            return "Shining political request недопустим: preparedIncarnationPackage повреждён или не проходит bootstrap validation.";

        return null;
    }

    private static JsonObject? FindFaction(JsonObject? shiningRoot, string factionId)
    {
        if (shiningRoot?["factions"] is not JsonArray factions || string.IsNullOrWhiteSpace(factionId))
            return null;

        return factions.OfType<JsonObject>()
            .FirstOrDefault(faction => string.Equals(GetNodeString(faction["factionId"]), factionId, StringComparison.OrdinalIgnoreCase));
    }

    private static JsonObject? FindResident(JsonObject? residentRoot, string residentId)
    {
        if (residentRoot?["entries"] is not JsonArray entries || string.IsNullOrWhiteSpace(residentId))
            return null;

        return entries.OfType<JsonObject>()
            .FirstOrDefault(entry => string.Equals(GetNodeString(entry["residentId"]), residentId, StringComparison.OrdinalIgnoreCase));
    }

    private static JsonObject? FindRadiantActor(JsonObject? shiningRoot, string actorId)
    {
        if (shiningRoot?["shiningPoliticalActors"] is not JsonArray actors || string.IsNullOrWhiteSpace(actorId))
            return null;

        return actors.OfType<JsonObject>()
            .FirstOrDefault(actor => string.Equals(GetNodeString(actor["actorId"]), actorId, StringComparison.OrdinalIgnoreCase));
    }

    private static bool GuardianExists(JsonObject? guardiansRoot, string guardianId)
    {
        if (guardiansRoot == null || string.IsNullOrWhiteSpace(guardianId))
            return false;

        if (guardiansRoot["activeGuardian"] is JsonObject activeGuardian &&
            string.Equals(GetNodeString(activeGuardian["guardianId"]), guardianId, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return guardiansRoot["guardians"] is JsonArray guardians &&
                   guardians.OfType<JsonObject>()
                   .Any(guardian => string.Equals(GetNodeString(guardian["guardianId"]), guardianId, StringComparison.OrdinalIgnoreCase));
    }

    private static bool IsActiveGuardianAutoFaction(JsonObject faction, JsonObject? guardiansRoot, out string activeGuardianId)
    {
        activeGuardianId = string.Empty;
        if (guardiansRoot?["activeGuardian"] is not JsonObject activeGuardian)
            return false;

        activeGuardianId = GetNodeString(activeGuardian["guardianId"]) ?? string.Empty;
        if (string.IsNullOrWhiteSpace(activeGuardianId))
            return false;

        var slug = Slugify(activeGuardianId);
        var expectedFactionId = $"faction_{slug}";
        var expectedHallId = $"hall_{slug}";
        var originType = GetNodeString(faction["originType"]);
        var hasAutoOrigin =
            string.Equals(originType, ShiningAbodeState.OriginTypeAscendedGuardian, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(originType, ShiningAbodeState.OriginTypePlayerFounded, StringComparison.OrdinalIgnoreCase);

        return hasAutoOrigin &&
               (string.Equals(GetNodeString(faction["factionId"]), expectedFactionId, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(GetNodeString(faction["hallId"]), expectedHallId, StringComparison.OrdinalIgnoreCase));
    }

    private static string Slugify(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return "item";

        var buffer = new char[value.Length];
        var index = 0;
        foreach (var ch in value.Trim())
        {
            if (char.IsLetterOrDigit(ch))
                buffer[index++] = char.ToLowerInvariant(ch);
            else if (index == 0 || buffer[index - 1] != '_')
                buffer[index++] = '_';
        }

        var result = new string(buffer, 0, index).Trim('_');
        return string.IsNullOrWhiteSpace(result) ? "item" : result;
    }

    private static bool TryGetCurrentResidentHeadFactionId(JsonObject? shiningRoot, string residentId, out string factionId)
    {
        return HasCurrentHeadFaction(shiningRoot, ShiningAbodeState.HeadActorTypeResident, residentId, excludingFactionId: null, out factionId);
    }

    private static bool HasCurrentHeadFaction(
        JsonObject? shiningRoot,
        string? actorType,
        string? actorId,
        string? excludingFactionId,
        out string factionId)
    {
        factionId = string.Empty;
        if (shiningRoot?["factions"] is not JsonArray factions ||
            string.IsNullOrWhiteSpace(actorType) ||
            string.IsNullOrWhiteSpace(actorId))
        {
            return false;
        }

        foreach (var faction in factions.OfType<JsonObject>())
        {
            var currentFactionId = GetNodeString(faction["factionId"]) ?? string.Empty;
            if (!string.IsNullOrWhiteSpace(excludingFactionId) &&
                string.Equals(currentFactionId, excludingFactionId, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (!string.Equals(GetNodeString(faction["leadership"]?["headActorType"]), actorType, StringComparison.OrdinalIgnoreCase) ||
                !string.Equals(GetNodeString(faction["leadership"]?["headActorId"]), actorId, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(GetNodeString(faction["leadership"]?["leadershipState"]), ShiningAbodeState.LeadershipStateVacant, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            factionId = currentFactionId;
            return true;
        }

        return false;
    }

    private static int CountAscendedFactionResidents(JsonObject? residentRoot, string factionId)
    {
        if (residentRoot?["entries"] is not JsonArray entries || string.IsNullOrWhiteSpace(factionId))
            return 0;

        return entries.OfType<JsonObject>().Count(entry =>
            string.Equals(GetNodeString(entry["ascensionState"]), ShiningAbodeState.AscensionStateAscended, StringComparison.OrdinalIgnoreCase) &&
            string.Equals(GetNodeString(entry["shiningFactionId"]), factionId, StringComparison.OrdinalIgnoreCase));
    }

    private static Task<bool> HasForeignPendingRealignmentForResidentAsync(
        FileSystemManager fs,
        string residentId,
        string requestId) =>
        HasForeignPendingRealignmentForResidentAsync(fs, writeLease: null, residentId, requestId);

    private static async Task<bool> HasForeignPendingRealignmentForResidentAsync(
        FileSystemManager fs,
        FileSystemManager.CanonicalWriteLease? writeLease,
        string residentId,
        string requestId)
    {
        if (string.IsNullOrWhiteSpace(residentId))
            return false;

        var requests = writeLease == null
            ? await ReadRealignmentRequestsAsync(fs)
            : await ReadRealignmentRequestsAsync(fs, writeLease);
        return requests.Any(request =>
            string.Equals(request.ResidentId, residentId, StringComparison.OrdinalIgnoreCase) &&
            !string.Equals(request.RequestId, requestId, StringComparison.OrdinalIgnoreCase));
    }

    private static Task<bool> HasForeignPendingLeadershipForFactionAsync(
        FileSystemManager fs,
        string factionId,
        string requestId) =>
        HasForeignPendingLeadershipForFactionAsync(fs, writeLease: null, factionId, requestId);

    private static async Task<bool> HasForeignPendingLeadershipForFactionAsync(
        FileSystemManager fs,
        FileSystemManager.CanonicalWriteLease? writeLease,
        string factionId,
        string requestId)
    {
        if (string.IsNullOrWhiteSpace(factionId))
            return false;

        var requests = writeLease == null
            ? await ReadLeadershipTransitionRequestsAsync(fs)
            : await ReadLeadershipTransitionRequestsAsync(fs, writeLease);
        return requests.Any(request =>
            string.Equals(request.FactionId, factionId, StringComparison.OrdinalIgnoreCase) &&
            !string.Equals(request.RequestId, requestId, StringComparison.OrdinalIgnoreCase));
    }

    private static Task<bool> HasPendingOrdinaryTransferAsync(
        FileSystemManager fs,
        string residentId) =>
        HasPendingOrdinaryTransferAsync(fs, writeLease: null, residentId);

    private static async Task<bool> HasPendingOrdinaryTransferAsync(
        FileSystemManager fs,
        FileSystemManager.CanonicalWriteLease? writeLease,
        string residentId)
    {
        if (string.IsNullOrWhiteSpace(residentId))
            return false;

        var transferRequests = writeLease == null
            ? await GuardianAbodeResidentRequestState.ReadTransferRequestsAsync(fs)
            : await GuardianAbodeResidentRequestState.ReadTransferRequestsAsync(fs, writeLease);
        return transferRequests.Any(request => string.Equals(request.ResidentId, residentId, StringComparison.OrdinalIgnoreCase));
    }

    private static Task<bool> IsResidentLockedByPendingFlowInternalAsync(
        FileSystemManager fs,
        string residentId,
        string? excludeFoundingFactionId,
        string? excludeRealignmentResidentId,
        string? excludeLeadershipFactionId) =>
        IsResidentLockedByPendingFlowInternalAsync(
            fs,
            writeLease: null,
            residentId,
            excludeFoundingFactionId,
            excludeRealignmentResidentId,
            excludeLeadershipFactionId);

    private static async Task<bool> IsResidentLockedByPendingFlowInternalAsync(
        FileSystemManager fs,
        FileSystemManager.CanonicalWriteLease? writeLease,
        string residentId,
        string? excludeFoundingFactionId,
        string? excludeRealignmentResidentId,
        string? excludeLeadershipFactionId)
    {
        if (string.IsNullOrWhiteSpace(residentId))
            return false;

        var realignmentRequests = writeLease == null
            ? await ReadRealignmentRequestsAsync(fs)
            : await ReadRealignmentRequestsAsync(fs, writeLease);
        if (realignmentRequests.Any(request =>
                !string.Equals(request.ResidentId, excludeRealignmentResidentId, StringComparison.OrdinalIgnoreCase) &&
                string.Equals(request.ResidentId, residentId, StringComparison.OrdinalIgnoreCase)))
        {
            return true;
        }

        var foundingRequests = await ReadFoundingRequestsCoreAsync(fs, writeLease);
        if (foundingRequests.Any(request =>
                !string.Equals(request.ProposedFactionId, excludeFoundingFactionId, StringComparison.OrdinalIgnoreCase) &&
                request.SupportingResidentIds.Any(id => string.Equals(id, residentId, StringComparison.OrdinalIgnoreCase))))
        {
            return true;
        }

        var leadershipRequests = writeLease == null
            ? await ReadLeadershipTransitionRequestsAsync(fs)
            : await ReadLeadershipTransitionRequestsAsync(fs, writeLease);
        return leadershipRequests.Any(request =>
            !string.Equals(request.FactionId, excludeLeadershipFactionId, StringComparison.OrdinalIgnoreCase) &&
            (
                request.SupportingResidentIds.Any(id => string.Equals(id, residentId, StringComparison.OrdinalIgnoreCase)) ||
                (string.Equals(request.CandidateHeadActorType, ShiningAbodeState.HeadActorTypeResident, StringComparison.OrdinalIgnoreCase) &&
                 string.Equals(request.CandidateHeadActorId, residentId, StringComparison.OrdinalIgnoreCase)) ||
                (string.Equals(request.IncumbentHeadActorType, ShiningAbodeState.HeadActorTypeResident, StringComparison.OrdinalIgnoreCase) &&
                 string.Equals(request.IncumbentHeadActorId, residentId, StringComparison.OrdinalIgnoreCase))
            ));
    }

    private static Task<string?> ValidateLeadershipActorPendingLocksAsync(
        FileSystemManager fs,
        string label,
        string actorType,
        string actorId,
        string currentFactionId) =>
        ValidateLeadershipActorPendingLocksAsync(
            fs,
            writeLease: null,
            label,
            actorType,
            actorId,
            currentFactionId);

    private static async Task<string?> ValidateLeadershipActorPendingLocksAsync(
        FileSystemManager fs,
        FileSystemManager.CanonicalWriteLease? writeLease,
        string label,
        string actorType,
        string actorId,
        string currentFactionId)
    {
        if (string.IsNullOrWhiteSpace(actorType) || string.IsNullOrWhiteSpace(actorId))
            return null;

        if (string.Equals(actorType, ShiningAbodeState.HeadActorTypeResident, StringComparison.OrdinalIgnoreCase))
        {
            if (await HasPendingOrdinaryTransferAsync(fs, writeLease, actorId))
                return $"{label} resident '{actorId}' уже участвует в ordinary inter-Abode transfer.";
            if (await IsResidentLockedByPendingFlowInternalAsync(fs, writeLease, actorId, excludeFoundingFactionId: null, excludeRealignmentResidentId: null, excludeLeadershipFactionId: currentFactionId))
                return $"{label} resident '{actorId}' уже заблокирован другим pending Shining flow.";

            return null;
        }

        var leadershipRequests = writeLease == null
            ? await ReadLeadershipTransitionRequestsAsync(fs)
            : await ReadLeadershipTransitionRequestsAsync(fs, writeLease);
        return leadershipRequests.Any(request =>
            !string.Equals(request.FactionId, currentFactionId, StringComparison.OrdinalIgnoreCase) &&
            LeadershipRequestReferencesActor(request, actorType, actorId))
            ? $"{label} actor '{actorType}:{actorId}' уже заблокирован другим pending Shining leadership flow."
            : null;
    }

    private static bool LeadershipRequestReferencesActor(
        PendingShiningFactionLeadershipTransitionRequest request,
        string actorType,
        string actorId)
    {
        if (string.IsNullOrWhiteSpace(actorType) || string.IsNullOrWhiteSpace(actorId))
            return false;

        return
            (string.Equals(request.CandidateHeadActorType, actorType, StringComparison.OrdinalIgnoreCase) &&
             string.Equals(request.CandidateHeadActorId, actorId, StringComparison.OrdinalIgnoreCase)) ||
            (string.Equals(request.IncumbentHeadActorType, actorType, StringComparison.OrdinalIgnoreCase) &&
             string.Equals(request.IncumbentHeadActorId, actorId, StringComparison.OrdinalIgnoreCase));
    }

    private static string? ValidateLeadershipCandidate(
        JsonObject shiningRoot,
        JsonObject? residentRoot,
        JsonObject? guardiansRoot,
        string factionId,
        string candidateHeadActorType,
        string candidateHeadActorId)
    {
        if (string.Equals(candidateHeadActorType, ShiningAbodeState.HeadActorTypePlayerSoul, StringComparison.OrdinalIgnoreCase))
        {
            return string.Equals(candidateHeadActorId, ShiningAbodeState.HeadActorTypePlayerSoul, StringComparison.OrdinalIgnoreCase)
                ? null
                : "candidateHeadActorId для player_soul должен быть ровно 'player_soul'.";
        }

        if (string.Equals(candidateHeadActorType, ShiningAbodeState.HeadActorTypeGuardian, StringComparison.OrdinalIgnoreCase))
            return GuardianExists(guardiansRoot, candidateHeadActorId) ? null : "candidate guardian не найден в guardians.json.";

        if (string.Equals(candidateHeadActorType, ShiningAbodeState.HeadActorTypeResident, StringComparison.OrdinalIgnoreCase))
        {
            var resident = FindResident(residentRoot, candidateHeadActorId);
            if (resident == null)
                return "candidate resident не найден.";
            if (!string.Equals(GetNodeString(resident["ascensionState"]), ShiningAbodeState.AscensionStateAscended, StringComparison.OrdinalIgnoreCase))
                return "candidate resident должен быть ascended.";
            if (!string.Equals(GetNodeString(resident["shiningFactionId"]), factionId, StringComparison.OrdinalIgnoreCase))
                return "candidate resident должен принадлежать той же фракции.";
            return null;
        }

        if (string.Equals(candidateHeadActorType, ShiningAbodeState.HeadActorTypeRadiantActor, StringComparison.OrdinalIgnoreCase))
        {
            var radiantActor = FindRadiantActor(shiningRoot, candidateHeadActorId);
            if (radiantActor == null)
                return "candidate radiant_actor не найден в shiningPoliticalActors[].";
            var currentFactionId = GetNodeString(radiantActor["currentFactionId"]);
            if (!string.IsNullOrWhiteSpace(currentFactionId) &&
                !string.Equals(currentFactionId, factionId, StringComparison.OrdinalIgnoreCase))
            {
                return "candidate radiant_actor уже принадлежит другой текущей фракции.";
            }

            return null;
        }

        return "candidateHeadActorType использует неподдерживаемое значение.";
    }

    private static string MapPatronFamilyToHallServiceTag(string patronEffectFamily) => patronEffectFamily switch
    {
        ShiningAbodeState.EffectFamilyLore => ShiningAbodeState.HallServiceTagLore,
        ShiningAbodeState.EffectFamilyMemory => ShiningAbodeState.HallServiceTagMemory,
        ShiningAbodeState.EffectFamilyResource => ShiningAbodeState.HallServiceTagResource,
        ShiningAbodeState.EffectFamilyRelic => ShiningAbodeState.HallServiceTagRelic,
        ShiningAbodeState.EffectFamilyDescent or ShiningAbodeState.EffectFamilyRoute => ShiningAbodeState.HallServiceTagDescent,
        _ => ShiningAbodeState.HallServiceTagSocial
    };

    private static string? GetNodeString(JsonNode? node)
    {
        if (node == null)
            return null;

        try
        {
            return node.GetValue<string>();
        }
        catch
        {
            return node.ToJsonString().Trim('"');
        }
    }

    private static int GetNodeInt(JsonNode? node, int fallback = 0)
    {
        if (node == null)
            return fallback;

        try
        {
            return node.GetValue<int>();
        }
        catch
        {
            return fallback;
        }
    }

    private static async Task WriteSingleRequestAsync<TRequest>(
        FileSystemManager fs,
        string path,
        TRequest request,
        Func<TRequest, string> identitySelector)
        where TRequest : class =>
        await WriteSingleRequestAsync(
            fs,
            path,
            request,
            (existing, pending) => string.Equals(
                identitySelector(existing),
                identitySelector(pending),
                StringComparison.OrdinalIgnoreCase));

    private static async Task WriteSingleRequestAsync<TRequest>(
        FileSystemManager fs,
        string path,
        TRequest request,
        Func<TRequest, TRequest, bool> conflictSelector)
        where TRequest : class
    {
        var existingState = await ReadRequestsStateAsync(fs, path, static json => JsonSerializer.Deserialize<TRequest>(json, JsonOpts));
        if (existingState.IsMalformed)
            throw new InvalidOperationException($"{Path.GetFileName(path)} повреждён и должен быть исправлен или очищен до записи нового политического запроса.");

        var requestId = GetPoliticalRequestId(request);
        if (existingState.Requests.Any(existingRequest =>
                conflictSelector(existingRequest, request) &&
                !string.Equals(GetPoliticalRequestId(existingRequest), requestId, StringComparison.OrdinalIgnoreCase)))
        {
            throw new InvalidOperationException($"{Path.GetFileName(path)} уже содержит live foreign Shining political contract with the same target identity; guarded writer не заменяет unresolved contract.");
        }

        var existing = existingState.Requests.ToList();
        existing.RemoveAll(existingRequest => conflictSelector(existingRequest, request));
        existing.Add(request);

        await PersistRequestsAsync(fs, path, existing);
    }

    private static async Task WriteSingleRequestAsync<TRequest>(
        FileSystemManager fs,
        FileSystemManager.CanonicalWriteLease writeLease,
        string path,
        TRequest request,
        Func<TRequest, TRequest, bool> conflictSelector)
        where TRequest : class
    {
        var json = await fs.ReadFileAsync(writeLease, path);
        var existingState = ReadRequestsState(
            json,
            fs.FileExists(writeLease, path),
            static itemJson => JsonSerializer.Deserialize<TRequest>(itemJson, JsonOpts));
        if (existingState.IsMalformed)
            throw new InvalidOperationException($"{Path.GetFileName(path)} повреждён и должен быть исправлен или очищен до записи нового политического запроса.");

        var requestId = GetPoliticalRequestId(request);
        if (existingState.Requests.Any(existingRequest =>
                conflictSelector(existingRequest, request) &&
                !string.Equals(GetPoliticalRequestId(existingRequest), requestId, StringComparison.OrdinalIgnoreCase)))
        {
            throw new InvalidOperationException($"{Path.GetFileName(path)} уже содержит live foreign Shining political contract with the same target identity; guarded writer не заменяет unresolved contract.");
        }

        var existing = existingState.Requests.ToList();
        existing.RemoveAll(existingRequest => conflictSelector(existingRequest, request));
        existing.Add(request);

        await PersistRequestsAsync(fs, writeLease, path, existing);
    }

    private static string GetPoliticalRequestId<TRequest>(TRequest request)
        where TRequest : class =>
        request switch
        {
            PendingShiningFactionFoundingRequest founding => founding.RequestId,
            PendingShiningFactionRealignmentRequest realignment => realignment.RequestId,
            PendingShiningFactionLeadershipTransitionRequest leadership => leadership.RequestId,
            _ => string.Empty
        };

    private static async Task PersistRequestsAsync<TRequest>(FileSystemManager fs, string path, IReadOnlyCollection<TRequest> requests)
        where TRequest : class
    {
        if (requests.Count == 0)
        {
            fs.DeleteFile(path);
            return;
        }

        await fs.WriteFileAtomicAsync(path, JsonSerializer.Serialize(new Dictionary<string, object?>
        {
            [RequestsProperty] = requests
        }, JsonOpts));
    }

    private static async Task PersistRequestsAsync<TRequest>(
        FileSystemManager fs,
        FileSystemManager.CanonicalWriteLease writeLease,
        string path,
        IReadOnlyCollection<TRequest> requests)
        where TRequest : class
    {
        if (requests.Count == 0)
        {
            fs.DeleteFile(writeLease, path);
            return;
        }

        await fs.WriteFileAtomicAsync(
            writeLease,
            path,
            JsonSerializer.Serialize(new Dictionary<string, object?>
            {
                [RequestsProperty] = requests
            }, JsonOpts));
    }

    private static bool HasMatchingFoundingClosure(JsonObject shiningRoot, JsonObject? residentRoot, PendingShiningFactionFoundingRequest request)
    {
        var receipt = ShiningAbodeState.FindReceipt(ShiningAbodeState.EnsureFactionFoundingReceiptsArray(shiningRoot), request.RequestId);
        if (receipt == null || !HasCanonicalResolutionMarkers(receipt) || !IsSupportedFoundingStatus(GetNodeString(receipt["status"])))
            return false;

        var receiptSupporters = ReadStringSet(receipt["supportingResidentIds"]);
        var requestSupporters = request.SupportingResidentIds
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .Select(id => id.Trim())
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        if (!string.Equals(GetNodeString(receipt["proposedFactionId"]), request.ProposedFactionId, StringComparison.OrdinalIgnoreCase) ||
            !string.Equals(GetNodeString(receipt["proposedHallId"]), request.ProposedHallId, StringComparison.OrdinalIgnoreCase) ||
            GetNodeInt(receipt["quotedCostFeathers"]) != request.QuotedCostFeathers ||
            GetNodeInt(receipt["quotedCostLightSparks"]) != request.QuotedCostLightSparks ||
            !receiptSupporters.SetEquals(requestSupporters))
        {
            return false;
        }

        var status = GetNodeString(receipt["status"]);
        if (!string.Equals(status, RequestStatusAccepted, StringComparison.OrdinalIgnoreCase))
            return FindHall(shiningRoot, request.ProposedHallId) == null &&
                   FindFaction(shiningRoot, request.ProposedFactionId) == null;

        var hall = FindHall(shiningRoot, request.ProposedHallId);
        var faction = FindFaction(shiningRoot, request.ProposedFactionId);
        if (hall == null || faction == null || !HallMatchesFoundingRequest(hall, request) || !FactionMatchesAcceptedFounding(faction, request))
            return false;

        if (residentRoot == null)
            return false;

        return request.SupportingResidentIds
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .All(supporterId =>
            {
                var resident = FindResident(residentRoot, supporterId);
                return resident != null &&
                       string.Equals(GetNodeString(resident["shiningFactionId"]), request.ProposedFactionId, StringComparison.OrdinalIgnoreCase);
            });
    }

    private static bool HasMatchingRealignmentClosure(JsonObject shiningRoot, JsonObject? residentRoot, PendingShiningFactionRealignmentRequest request)
    {
        var receipt = ShiningAbodeState.FindReceipt(ShiningAbodeState.EnsureFactionRealignmentReceiptsArray(shiningRoot), request.RequestId);
        if (receipt == null || !HasCanonicalResolutionMarkers(receipt) || !IsSupportedRealignmentStatus(GetNodeString(receipt["status"])))
            return false;

        if (!string.Equals(GetNodeString(receipt["residentId"]), request.ResidentId, StringComparison.OrdinalIgnoreCase) ||
            !string.Equals(GetNodeString(receipt["sourceFactionId"]), request.SourceFactionId, StringComparison.OrdinalIgnoreCase) ||
            !string.Equals(GetNodeString(receipt["targetFactionId"]) ?? string.Empty, request.TargetFactionId ?? string.Empty, StringComparison.OrdinalIgnoreCase) ||
            !string.Equals(GetNodeString(receipt["realignmentMode"]), request.RealignmentMode, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (residentRoot == null)
            return false;

        var resident = FindResident(residentRoot, request.ResidentId);
        if (resident == null)
            return false;

        var status = GetNodeString(receipt["status"]);
        var residentFactionId = GetNodeString(resident["shiningFactionId"]) ?? string.Empty;
        if (string.Equals(status, RequestStatusAccepted, StringComparison.OrdinalIgnoreCase))
        {
            var historyEntryId = GetNodeString(receipt["residentHistoryEntryId"]);
            return !string.IsNullOrWhiteSpace(request.TargetFactionId) &&
                   FindFaction(shiningRoot, request.TargetFactionId) != null &&
                   string.Equals(residentFactionId, request.TargetFactionId, StringComparison.OrdinalIgnoreCase) &&
                   HasResidentHistoryEntry(residentRoot, historyEntryId);
        }

        if (string.Equals(status, RequestStatusDepartedToNeutral, StringComparison.OrdinalIgnoreCase))
            return string.IsNullOrWhiteSpace(residentFactionId) &&
                   HasResidentHistoryEntry(residentRoot, GetNodeString(receipt["residentHistoryEntryId"]));

        return string.Equals(residentFactionId, request.SourceFactionId, StringComparison.OrdinalIgnoreCase);
    }

    private static bool HasMatchingLeadershipClosure(JsonObject shiningRoot, PendingShiningFactionLeadershipTransitionRequest request)
    {
        var faction = FindFaction(shiningRoot, request.FactionId);
        if (faction == null)
            return false;

        var receipt = ShiningAbodeState.FindReceipt(faction["leadershipReceipts"] as JsonArray ?? new JsonArray(), request.RequestId);
        if (receipt == null || !HasCanonicalResolutionMarkers(receipt) || !IsSupportedLeadershipStatus(GetNodeString(receipt["status"])))
            return false;

        if (!string.Equals(GetNodeString(receipt["transitionMode"]), request.TransitionMode, StringComparison.OrdinalIgnoreCase) ||
            !string.Equals(GetNodeString(receipt["previousHeadActorType"]) ?? string.Empty, request.IncumbentHeadActorType ?? string.Empty, StringComparison.OrdinalIgnoreCase) ||
            !string.Equals(GetNodeString(receipt["previousHeadActorId"]) ?? string.Empty, request.IncumbentHeadActorId ?? string.Empty, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var status = GetNodeString(receipt["status"]);
        if (string.Equals(status, RequestStatusAccepted, StringComparison.OrdinalIgnoreCase))
        {
            if (!string.Equals(GetNodeString(receipt["newHeadActorType"]) ?? string.Empty, request.CandidateHeadActorType ?? string.Empty, StringComparison.OrdinalIgnoreCase) ||
                !string.Equals(GetNodeString(receipt["newHeadActorId"]) ?? string.Empty, request.CandidateHeadActorId ?? string.Empty, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }
        }

        if (string.Equals(status, RequestStatusAccepted, StringComparison.OrdinalIgnoreCase))
        {
            var history = faction["leadershipHistory"] as JsonArray ?? new JsonArray();
            if (ShiningAbodeState.FindLeadershipHistoryEntry(history, request.RequestId) == null)
                return false;

            var leadership = faction["leadership"] as JsonObject ?? new JsonObject();
            if (string.IsNullOrWhiteSpace(request.CandidateHeadActorType) &&
                string.IsNullOrWhiteSpace(request.CandidateHeadActorId))
            {
                return string.Equals(GetNodeString(leadership["leadershipState"]), ShiningAbodeState.LeadershipStateVacant, StringComparison.OrdinalIgnoreCase) &&
                       string.IsNullOrWhiteSpace(GetNodeString(leadership["headActorType"])) &&
                       string.IsNullOrWhiteSpace(GetNodeString(leadership["headActorId"]));
            }

            return string.Equals(GetNodeString(leadership["headActorType"]) ?? string.Empty, request.CandidateHeadActorType ?? string.Empty, StringComparison.OrdinalIgnoreCase) &&
                   string.Equals(GetNodeString(leadership["headActorId"]) ?? string.Empty, request.CandidateHeadActorId ?? string.Empty, StringComparison.OrdinalIgnoreCase) &&
                   string.Equals(GetNodeString(leadership["leadershipState"]), ShiningAbodeState.LeadershipStateSecure, StringComparison.OrdinalIgnoreCase);
        }

        if (string.Equals(status, RequestStatusRefused, StringComparison.OrdinalIgnoreCase))
        {
            var history = faction["leadershipHistory"] as JsonArray ?? new JsonArray();
            if (ShiningAbodeState.FindLeadershipHistoryEntry(history, request.RequestId) == null)
                return false;
        }

        var currentLeadership = faction["leadership"] as JsonObject ?? new JsonObject();
        return string.Equals(GetNodeString(currentLeadership["headActorType"]) ?? string.Empty, request.IncumbentHeadActorType ?? string.Empty, StringComparison.OrdinalIgnoreCase) &&
               string.Equals(GetNodeString(currentLeadership["headActorId"]) ?? string.Empty, request.IncumbentHeadActorId ?? string.Empty, StringComparison.OrdinalIgnoreCase);
    }

    private static JsonObject? FindHall(JsonObject? shiningRoot, string hallId)
    {
        if (shiningRoot?["halls"] is not JsonArray halls || string.IsNullOrWhiteSpace(hallId))
            return null;

        return halls.OfType<JsonObject>()
            .FirstOrDefault(hall => string.Equals(GetNodeString(hall["hallId"]), hallId, StringComparison.OrdinalIgnoreCase));
    }

    private static bool HallMatchesFoundingRequest(JsonObject hall, PendingShiningFactionFoundingRequest request)
    {
        return string.Equals(GetNodeString(hall["hallId"]), request.ProposedHallId, StringComparison.OrdinalIgnoreCase) &&
               string.Equals(GetNodeString(hall["hallName"]), request.ProposedHallName, StringComparison.Ordinal) &&
               string.Equals(GetNodeString(hall["description"]), request.ProposedHallDescription, StringComparison.Ordinal) &&
               ReadStringSet(hall["serviceTags"]).SetEquals(request.ProposedHallServiceTags
                   .Where(tag => !string.IsNullOrWhiteSpace(tag))
                   .Select(tag => tag.Trim()));
    }

    private static bool FactionMatchesAcceptedFounding(JsonObject faction, PendingShiningFactionFoundingRequest request)
    {
        var leadership = faction["leadership"] as JsonObject ?? new JsonObject();
        return string.Equals(GetNodeString(faction["factionId"]), request.ProposedFactionId, StringComparison.OrdinalIgnoreCase) &&
               string.Equals(GetNodeString(faction["hallId"]), request.ProposedHallId, StringComparison.OrdinalIgnoreCase) &&
               string.Equals(GetNodeString(faction["originType"]), ShiningAbodeState.OriginTypePlayerFounded, StringComparison.OrdinalIgnoreCase) &&
               GetNodeInt(faction["baseStrength"]) == 35 &&
               string.Equals(GetNodeString(faction["charter"]?["factionName"]), request.Charter.FactionName, StringComparison.Ordinal) &&
               string.Equals(GetNodeString(faction["charter"]?["favoredArchetype"]), request.Charter.FavoredArchetype, StringComparison.OrdinalIgnoreCase) &&
               string.Equals(GetNodeString(faction["charter"]?["patronEffectFamily"]), request.Charter.PatronEffectFamily, StringComparison.OrdinalIgnoreCase) &&
               string.Equals(GetNodeString(faction["charter"]?["summary"]), request.Charter.Summary, StringComparison.Ordinal) &&
               string.Equals(GetNodeString(leadership["headActorType"]), ShiningAbodeState.HeadActorTypePlayerSoul, StringComparison.OrdinalIgnoreCase) &&
               string.Equals(GetNodeString(leadership["headActorId"]), ShiningAbodeState.HeadActorTypePlayerSoul, StringComparison.OrdinalIgnoreCase) &&
               string.Equals(GetNodeString(leadership["leadershipState"]), ShiningAbodeState.LeadershipStateSecure, StringComparison.OrdinalIgnoreCase);
    }

    private static bool HasResidentHistoryEntry(JsonObject residentRoot, string? historyEntryId)
    {
        if (string.IsNullOrWhiteSpace(historyEntryId))
            return false;

        var historyLog = GuardianAbodeResidentState.EnsureHistoryLogArray(residentRoot);
        return GuardianAbodeResidentState.HasHistoryLogEntry(historyLog, historyEntryId);
    }

    private static bool HasCanonicalResolutionMarkers(JsonObject receipt) =>
        GetNodeInt(receipt["resolvedAtTurn"], 0) > 0 &&
        !string.IsNullOrWhiteSpace(GetNodeString(receipt["resolvedAtUtc"]));

    private static HashSet<string> ReadStringSet(JsonNode? node)
    {
        var result = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (node is not JsonArray array)
            return result;

        foreach (var valueNode in array)
        {
            var value = GetNodeString(valueNode);
            if (!string.IsNullOrWhiteSpace(value))
                result.Add(value.Trim());
        }

        return result;
    }

    private static string? ValidateFoundingResourceReservation(
        PendingShiningFactionFoundingRequest request,
        JsonObject? soulRoot,
        JsonObject shiningRoot)
    {
        if (request.ReservedInkFeathersBefore < FactionFoundingCostFeathers ||
            request.ReservedLightSparksBefore < FactionFoundingCostLightSparks)
        {
            return "Live founding request должен содержать client-authored reservedInkFeathersBefore и reservedLightSparksBefore snapshot до локальной резервации.";
        }

        var expectedCurrentFeathers = request.ReservedInkFeathersBefore - FactionFoundingCostFeathers;
        var actualCurrentFeathers = CurrentSoulFeathers(soulRoot);
        if (actualCurrentFeathers != expectedCurrentFeathers)
        {
            return $"Founding request не подтверждает локальную резервацию Ink Feathers: expected current={expectedCurrentFeathers} from reservedInkFeathersBefore-cost, actual current={actualCurrentFeathers}.";
        }

        var expectedCurrentLightSparks = request.ReservedLightSparksBefore - FactionFoundingCostLightSparks;
        var actualCurrentLightSparks = GetNodeInt(shiningRoot["lightSparks"]);
        if (actualCurrentLightSparks != expectedCurrentLightSparks)
        {
            return $"Founding request не подтверждает локальную резервацию Light Sparks: expected current={expectedCurrentLightSparks} from reservedLightSparksBefore-cost, actual current={actualCurrentLightSparks}.";
        }

        return null;
    }

    private static int CurrentSoulFeathers(JsonObject? soulRoot)
    {
        if (soulRoot?["inkFeathers"] is JsonObject feathers)
            return GetNodeInt(feathers["current"]);

        return GetNodeInt(soulRoot?["inkFeathers"]);
    }

    private static bool IsSameFoundingLogicalRequest(
        PendingShiningFactionFoundingRequest existing,
        PendingShiningFactionFoundingRequest pending) =>
        string.Equals(existing.RequestId, pending.RequestId, StringComparison.OrdinalIgnoreCase) &&
        string.Equals(existing.ProposedFactionId, pending.ProposedFactionId, StringComparison.OrdinalIgnoreCase) &&
        string.Equals(existing.ProposedHallId, pending.ProposedHallId, StringComparison.OrdinalIgnoreCase);

    private static bool IsShiningRealm(string? currentRealm) => RealmSemantics.IsShiningRealm(currentRealm);
}
