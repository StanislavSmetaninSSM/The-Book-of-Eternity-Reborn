using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using BookOfEternityClient.Configuration;
using BookOfEternityClient.Core;

namespace BookOfEternityClient.Services;

internal static class PlayerGuardianFoundationState
{
    public const string PendingRequestPath = "game_state/control/pending_player_guardian_foundation.json";
    public const string ActionTag = "PLAYER_GUARDIAN_FOUNDATION";
    public const string HistoryProperty = "playerGuardianFoundationHistory";
    public const string RequestMode = "player_founded_guardian";
    public const string OriginTypePlayerFoundedAscendedSoul = "player_founded_ascended_soul";
    public const string FounderLoyaltyTierSoulbound = "soulbound";
    public const string FoundationSourceShiningReturn = "shining_return";
    public const string SoulStateGuardianIdProperty = "playerFoundedGuardianId";
    public const string SoulStateFoundationStatusProperty = "playerGuardianFoundationStatus";
    public const string SoulStateFoundationStatusFounded = "founded";
    public const string GuardianRoleToPlayerProperty = "guardianRoleToPlayer";
    public const string GuardianRoleFormerPatron = "former_patron";
    public const string FounderBonusesProperty = "founderBonuses";
    public const string FounderBonusExtraGachaChargesProperty = "extraGachaChargesPerReturn";
    public const string FounderAbodeFeaturesProperty = "founderAbodeFeatures";
    public const string FounderAbodeFeatureTitleProperty = "featureTitle";
    public const string FounderAbodeFeatureSummaryProperty = "featureSummary";
    public const string FounderAbodeResidentAttractionModeProperty = "residentAttractionMode";
    public const string FounderAbodeResidentAttractionModeFounderCall = "founder_call";
    public const int DefaultFounderExtraGachaChargesPerReturn = 1;
    public const int SoulboundLegendaryReputationFloor = 230;
    public const int SoulboundCanonicalStartingReputation = 300;

    private static readonly JsonSerializerOptions JsonOpts = SharedJsonOptions.PrettyCamelCaseUnsafeRelaxed;

    internal enum PendingFoundationRequestReadStatus
    {
        Missing,
        Valid,
        Malformed
    }

    internal sealed record PendingFoundationRequestReadResult(
        PendingFoundationRequestReadStatus Status,
        PendingPlayerGuardianFoundationRequest? Request)
    {
        internal bool Exists => Status != PendingFoundationRequestReadStatus.Missing;
        internal bool IsMalformed => Status == PendingFoundationRequestReadStatus.Malformed;
    }

    public sealed class PendingPlayerGuardianFoundationRequest
    {
        [JsonPropertyName("requestId")]
        public string RequestId { get; set; } = $"player_guardian_foundation_{Guid.NewGuid():N}";

        [JsonPropertyName("mode")]
        public string Mode { get; set; } = RequestMode;

        [JsonPropertyName("founderSoulName")]
        public string FounderSoulName { get; set; } = "";

        [JsonPropertyName("previousGuardianId")]
        public string PreviousGuardianId { get; set; } = "";

        [JsonPropertyName("previousGuardianName")]
        public string PreviousGuardianName { get; set; } = "";

        [JsonPropertyName("sourceShiningAvailability")]
        public string SourceShiningAvailability { get; set; } = "";

        [JsonPropertyName("proposedDisplayName")]
        public string ProposedDisplayName { get; set; } = "";

        [JsonPropertyName("mantleSummary")]
        public string MantleSummary { get; set; } = "";

        [JsonPropertyName("mantleCreed")]
        public string MantleCreed { get; set; } = "";

        [JsonPropertyName("appearanceMotifs")]
        public List<string> AppearanceMotifs { get; set; } = new();

        [JsonPropertyName("dominantAspect")]
        public string DominantAspect { get; set; } = "";

        [JsonPropertyName("createdAtTurn")]
        public int CreatedAtTurn { get; set; }

        [JsonPropertyName("createdAtUtc")]
        public string CreatedAtUtc { get; set; } = DateTime.UtcNow.ToString("o");
    }

    public sealed class FoundationContext
    {
        public string CurrentRealm { get; init; } = "";
        public string SoulName { get; init; } = "";
        public string ShiningAvailability { get; init; } = "";
        public string FoundationStatus { get; init; } = "";
        public bool HasPreparedIncarnationPackage { get; init; }
        public AfterlifeReturnGuardSemanticState ReturnGuardState { get; init; }
        public string PreviousGuardianId { get; init; } = "";
        public string PreviousGuardianName { get; init; } = "";
        public PendingPlayerGuardianFoundationRequest? PendingRequest { get; init; }
        public string ExistingFoundedGuardianId { get; init; } = "";
        public string ExistingFoundedGuardianName { get; init; } = "";
        public string ExistingFoundedGuardianAbodeId { get; init; } = "";
        public string ExistingFoundedGuardianAbodeName { get; init; } = "";
        public int ExistingFoundedGuardianExtraGachaChargesPerReturn { get; init; }
        public string ExistingFoundedGuardianFeatureTitle { get; init; } = "";
        public string ExistingFoundedGuardianFeatureSummary { get; init; } = "";
        public string FormerPatronGuardianId { get; init; } = "";
        public string FormerPatronGuardianName { get; init; } = "";
        public string FoundationRequestId { get; init; } = "";
        public int FoundationResolvedAtTurn { get; init; }
        public string FoundationResolvedAtUtc { get; init; } = "";
        public bool CurrentActiveGuardianIsFounded { get; init; }
        public string BlockingReason { get; init; } = "";
        public bool CanCreateRequest { get; init; }
        public bool HasCompletedFoundation =>
            !string.IsNullOrWhiteSpace(ExistingFoundedGuardianId) ||
            string.Equals(FoundationStatus, SoulStateFoundationStatusFounded, StringComparison.OrdinalIgnoreCase);
    }

    public sealed class PlayerGuardianFoundationHistoryEntry
    {
        [JsonPropertyName("requestId")]
        public string RequestId { get; set; } = "";

        [JsonPropertyName("guardianId")]
        public string GuardianId { get; set; } = "";

        [JsonPropertyName("guardianDisplayName")]
        public string GuardianDisplayName { get; set; } = "";

        [JsonPropertyName("founderSoulName")]
        public string FounderSoulName { get; set; } = "";

        [JsonPropertyName("formerPatronGuardianId")]
        public string FormerPatronGuardianId { get; set; } = "";

        [JsonPropertyName("formerPatronGuardianName")]
        public string FormerPatronGuardianName { get; set; } = "";

        [JsonPropertyName("foundationSource")]
        public string FoundationSource { get; set; } = FoundationSourceShiningReturn;

        [JsonPropertyName("resolvedAtTurn")]
        public int ResolvedAtTurn { get; set; }

        [JsonPropertyName("resolvedAtUtc")]
        public string ResolvedAtUtc { get; set; } = "";
    }

    public static async Task<PendingPlayerGuardianFoundationRequest?> ReadAsync(FileSystemManager fs)
        => (await ReadStateAsync(fs)).Request;

    public static Task WriteAsync(
        FileSystemManager fs,
        PendingPlayerGuardianFoundationRequest request) =>
        WriteCoreAsync(fs, writeLease: null, request);

    internal static Task WriteAsync(
        FileSystemManager fs,
        FileSystemManager.CanonicalWriteLease writeLease,
        PendingPlayerGuardianFoundationRequest request) =>
        WriteCoreAsync(fs, writeLease, request);

    private static async Task WriteCoreAsync(
        FileSystemManager fs,
        FileSystemManager.CanonicalWriteLease? writeLease,
        PendingPlayerGuardianFoundationRequest request)
    {
        var existingState = await ReadStateCoreAsync(fs, writeLease);
        if (existingState.IsMalformed)
            throw new InvalidOperationException("pending_player_guardian_foundation.json повреждён и должен быть исправлен или очищен до записи нового foundation request.");
        if (existingState.Request != null &&
            !string.Equals(existingState.Request.RequestId, request.RequestId, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("pending_player_guardian_foundation.json already contains a live player guardian foundation contract and cannot be overwritten without explicit canonical closure.");
        }

        var json = JsonSerializer.Serialize(request, JsonOpts);
        if (writeLease == null)
            await fs.WriteFileAtomicAsync(PendingRequestPath, json);
        else
            await fs.WriteFileAtomicAsync(writeLease, PendingRequestPath, json);
    }

    internal static async Task<PendingFoundationRequestReadResult> ReadStateAsync(FileSystemManager fs)
        => await ReadStateCoreAsync(fs, writeLease: null);

    internal static async Task<PendingFoundationRequestReadResult> ReadStateAsync(
        FileSystemManager fs,
        FileSystemManager.CanonicalWriteLease writeLease)
        => await ReadStateCoreAsync(fs, writeLease);

    private static async Task<PendingFoundationRequestReadResult> ReadStateCoreAsync(
        FileSystemManager fs,
        FileSystemManager.CanonicalWriteLease? writeLease)
    {
        var json = writeLease == null
            ? await fs.ReadFileAsync(PendingRequestPath)
            : await fs.ReadFileAsync(writeLease, PendingRequestPath);
        var fileExists = writeLease == null
            ? fs.FileExists(PendingRequestPath)
            : fs.FileExists(writeLease, PendingRequestPath);
        return ParseState(json, fileExists);
    }

    internal static PendingFoundationRequestReadResult ParseState(string? json, bool fileExists)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return new PendingFoundationRequestReadResult(
                fileExists ? PendingFoundationRequestReadStatus.Malformed : PendingFoundationRequestReadStatus.Missing,
                null);
        }

        try
        {
            var request = JsonSerializer.Deserialize<PendingPlayerGuardianFoundationRequest>(json, JsonOpts);
            return new PendingFoundationRequestReadResult(
                request == null ? PendingFoundationRequestReadStatus.Malformed : PendingFoundationRequestReadStatus.Valid,
                request);
        }
        catch
        {
            return new PendingFoundationRequestReadResult(PendingFoundationRequestReadStatus.Malformed, null);
        }
    }

    public static void Clear(FileSystemManager fs) => fs.DeleteFile(PendingRequestPath);

    public static Task<FoundationContext> ReadContextAsync(FileSystemManager fs) =>
        ReadContextCoreAsync(fs, writeLease: null);

    internal static Task<FoundationContext> ReadContextAsync(
        FileSystemManager fs,
        FileSystemManager.CanonicalWriteLease writeLease) =>
        ReadContextCoreAsync(fs, writeLease);

    private static async Task<FoundationContext> ReadContextCoreAsync(
        FileSystemManager fs,
        FileSystemManager.CanonicalWriteLease? writeLease)
    {
        var soulRoot = await ReadJsonObjectAsync(fs, writeLease, "game_state/meta/soul_state.json");
        var guardiansRoot = await ReadJsonObjectAsync(fs, writeLease, "game_state/meta/guardians.json");
        var shiningRoot = await ReadJsonObjectAsync(fs, writeLease, ShiningAbodeState.StatePath);
        var pendingRequestState = await ReadStateCoreAsync(fs, writeLease);
        var pendingRequest = pendingRequestState.Request;

        var currentRealm = GetNodeString(soulRoot?["currentRealm"]) ?? "";
        var soulName = GetNodeString(soulRoot?["soulName"]) ?? "";
        var foundationStatus = GetNodeString(soulRoot?[SoulStateFoundationStatusProperty]) ?? "";
        var availability = GetNodeString(shiningRoot?["availability"]) ?? "";
        var hasPreparedPackage = shiningRoot?["preparedIncarnationPackage"] is JsonObject;

        var previousGuardianId = "";
        var previousGuardianName = "";
        if (guardiansRoot?["activeGuardian"] is JsonObject activeGuardian)
        {
            previousGuardianId = GetNodeString(activeGuardian["guardianId"]) ?? "";
            previousGuardianName = GuardianManifestation.GetDisplayName(ToJsonElement(activeGuardian)) ??
                                   GetNodeString(activeGuardian["canonicalName"]) ??
                                   previousGuardianId;
        }

        var linkedFoundedGuardianId = GetNodeString(soulRoot?[SoulStateGuardianIdProperty]) ?? "";
        var existingFoundedGuardian = FindGuardianById(guardiansRoot, linkedFoundedGuardianId) ??
                                     FindPlayerFoundedGuardian(guardiansRoot);
        var existingFoundedGuardianId = GetNodeString(existingFoundedGuardian?["guardianId"]) ?? "";
        var existingFoundedGuardianName = existingFoundedGuardian != null
            ? GuardianManifestation.GetDisplayName(ToJsonElement(existingFoundedGuardian)) ??
              GetNodeString(existingFoundedGuardian["canonicalName"]) ??
              existingFoundedGuardianId
            : "";
        var existingFoundedGuardianAbodeId = existingFoundedGuardian?["abode"] is JsonObject existingFoundedGuardianAbode
            ? GetNodeString(existingFoundedGuardianAbode["abodeId"]) ?? ""
            : "";
        var existingFoundedGuardianAbodeName = existingFoundedGuardian?["abode"] is JsonObject existingFoundedGuardianAbodeNameNode
            ? GetNodeString(existingFoundedGuardianAbodeNameNode["name"]) ?? ""
            : "";
        var existingFoundedGuardianExtraGachaChargesPerReturn = GetFounderExtraGachaCharges(existingFoundedGuardian);
        var existingFoundedGuardianFeatureTitle = GetFounderAbodeFeatureTitle(existingFoundedGuardian);
        var existingFoundedGuardianFeatureSummary = GetFounderAbodeFeatureSummary(existingFoundedGuardian);
        var latestHistoryEntry = FindHistoryEntryByGuardianId(guardiansRoot, existingFoundedGuardianId) ??
                                 FindLatestHistoryEntry(guardiansRoot);
        var formerPatronGuardianId = GetNodeString(latestHistoryEntry?["formerPatronGuardianId"]) ?? "";
        var formerPatronGuardianName = GetNodeString(latestHistoryEntry?["formerPatronGuardianName"]) ?? "";
        var foundationRequestId = GetNodeString(latestHistoryEntry?["requestId"]) ?? "";
        var foundationResolvedAtTurn = TryGetNodeInt(latestHistoryEntry?["resolvedAtTurn"]);
        var foundationResolvedAtUtc = GetNodeString(latestHistoryEntry?["resolvedAtUtc"]) ?? "";
        if (string.IsNullOrWhiteSpace(existingFoundedGuardianId))
            existingFoundedGuardianId = GetNodeString(latestHistoryEntry?["guardianId"]) ?? "";
        if (string.IsNullOrWhiteSpace(existingFoundedGuardianName))
            existingFoundedGuardianName = GetNodeString(latestHistoryEntry?["guardianDisplayName"]) ?? existingFoundedGuardianId;

        var returnGuardRaw = writeLease == null
            ? await fs.ReadFileAsync(AfterlifeReturnGuardService.GuardPath)
            : await fs.ReadFileAsync(writeLease, AfterlifeReturnGuardService.GuardPath);
        var returnGuardState = AfterlifeReturnGuardService.Classify(returnGuardRaw, out _);
        var currentActiveGuardianIsFounded = !string.IsNullOrWhiteSpace(existingFoundedGuardianId) &&
                                             string.Equals(previousGuardianId, existingFoundedGuardianId, StringComparison.OrdinalIgnoreCase);

        var blockingReason = ResolveBlockingReason(
            currentRealm,
            availability,
            foundationStatus,
            hasPreparedPackage,
            returnGuardState,
            soulName,
            previousGuardianId,
            pendingRequestState.IsMalformed,
            pendingRequest,
            existingFoundedGuardianId);

        return new FoundationContext
        {
            CurrentRealm = currentRealm,
            SoulName = soulName,
            ShiningAvailability = availability,
            FoundationStatus = foundationStatus,
            HasPreparedIncarnationPackage = hasPreparedPackage,
            ReturnGuardState = returnGuardState,
            PreviousGuardianId = previousGuardianId,
            PreviousGuardianName = previousGuardianName,
            PendingRequest = pendingRequest,
            ExistingFoundedGuardianId = existingFoundedGuardianId,
            ExistingFoundedGuardianName = existingFoundedGuardianName,
            ExistingFoundedGuardianAbodeId = existingFoundedGuardianAbodeId,
            ExistingFoundedGuardianAbodeName = existingFoundedGuardianAbodeName,
            ExistingFoundedGuardianExtraGachaChargesPerReturn = existingFoundedGuardianExtraGachaChargesPerReturn,
            ExistingFoundedGuardianFeatureTitle = existingFoundedGuardianFeatureTitle,
            ExistingFoundedGuardianFeatureSummary = existingFoundedGuardianFeatureSummary,
            FormerPatronGuardianId = formerPatronGuardianId,
            FormerPatronGuardianName = formerPatronGuardianName,
            FoundationRequestId = foundationRequestId,
            FoundationResolvedAtTurn = foundationResolvedAtTurn,
            FoundationResolvedAtUtc = foundationResolvedAtUtc,
            CurrentActiveGuardianIsFounded = currentActiveGuardianIsFounded,
            BlockingReason = blockingReason,
            CanCreateRequest = string.IsNullOrWhiteSpace(blockingReason)
        };
    }

    public static Task<string?> ValidateRequestAgainstCurrentStateAsync(
        FileSystemManager fs,
        PendingPlayerGuardianFoundationRequest request) =>
        ValidateRequestAgainstCurrentStateCoreAsync(fs, writeLease: null, request);

    internal static Task<string?> ValidateRequestAgainstCurrentStateAsync(
        FileSystemManager fs,
        FileSystemManager.CanonicalWriteLease writeLease,
        PendingPlayerGuardianFoundationRequest request) =>
        ValidateRequestAgainstCurrentStateCoreAsync(fs, writeLease, request);

    private static async Task<string?> ValidateRequestAgainstCurrentStateCoreAsync(
        FileSystemManager fs,
        FileSystemManager.CanonicalWriteLease? writeLease,
        PendingPlayerGuardianFoundationRequest request)
    {
        var context = await ReadContextCoreAsync(fs, writeLease);
        if (!string.IsNullOrWhiteSpace(context.BlockingReason))
            return context.BlockingReason;

        if (!string.Equals(request.Mode, RequestMode, StringComparison.OrdinalIgnoreCase))
            return "mode должен быть player_founded_guardian.";
        if (string.IsNullOrWhiteSpace(request.RequestId) ||
            string.IsNullOrWhiteSpace(request.FounderSoulName) ||
            string.IsNullOrWhiteSpace(request.PreviousGuardianId) ||
            string.IsNullOrWhiteSpace(request.PreviousGuardianName) ||
            string.IsNullOrWhiteSpace(request.ProposedDisplayName) ||
            string.IsNullOrWhiteSpace(request.MantleSummary) ||
            string.IsNullOrWhiteSpace(request.MantleCreed) ||
            request.AppearanceMotifs.Count == 0 ||
            request.CreatedAtTurn < 0)
        {
            return "foundation request должен содержать полный client-authored ritual contract.";
        }

        if (!DateTimeOffset.TryParse(request.CreatedAtUtc, out _))
            return "createdAtUtc должен быть ISO 8601 timestamp.";

        if (!string.Equals(request.SourceShiningAvailability, ShiningAbodeState.AvailabilitySealedUntilNextAscension, StringComparison.OrdinalIgnoreCase))
            return "Источник должен подтверждать sealed_until_next_ascension state Сияющей Обители.";

        if (!string.Equals(request.FounderSoulName, context.SoulName, StringComparison.OrdinalIgnoreCase))
            return "founderSoulName должен совпадать с текущим именем души.";

        if (!string.Equals(request.PreviousGuardianId, context.PreviousGuardianId, StringComparison.OrdinalIgnoreCase))
            return "previousGuardianId должен совпадать с текущим activeGuardian.";

        if (!string.Equals(request.PreviousGuardianName, context.PreviousGuardianName, StringComparison.OrdinalIgnoreCase))
            return "previousGuardianName должен совпадать с текущим activeGuardian.";

        var distinctMotifs = request.AppearanceMotifs
            .Where(motif => !string.IsNullOrWhiteSpace(motif))
            .Select(motif => motif.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
        if (distinctMotifs.Count == 0)
            return "appearanceMotifs должен содержать хотя бы один непустой мотив.";

        return null;
    }

    public static async Task EnsureHealthyAsync(FileSystemManager fs, string? currentRealm)
    {
        if (!fs.FileExists(PendingRequestPath))
            return;

        if (string.IsNullOrWhiteSpace(currentRealm))
            return;

        if (!IsChaosSeaRealm(currentRealm))
        {
            Clear(fs);
            return;
        }

        var json = await fs.ReadFileAsync(PendingRequestPath);
        if (string.IsNullOrWhiteSpace(json))
            return;

        PendingPlayerGuardianFoundationRequest? request;
        try
        {
            request = JsonSerializer.Deserialize<PendingPlayerGuardianFoundationRequest>(json, JsonOpts);
        }
        catch
        {
            return;
        }

        if (request == null ||
            string.IsNullOrWhiteSpace(request.RequestId) ||
            string.IsNullOrWhiteSpace(request.FounderSoulName) ||
            string.IsNullOrWhiteSpace(request.PreviousGuardianId) ||
            string.IsNullOrWhiteSpace(request.PreviousGuardianName) ||
            string.IsNullOrWhiteSpace(request.ProposedDisplayName) ||
            string.IsNullOrWhiteSpace(request.MantleSummary) ||
            string.IsNullOrWhiteSpace(request.MantleCreed) ||
            request.AppearanceMotifs.Count == 0 ||
            !DateTimeOffset.TryParse(request.CreatedAtUtc, out _))
        {
            return;
        }

        var guardiansRoot = await ReadJsonObjectAsync(fs, "game_state/meta/guardians.json");
        var soulRoot = await ReadJsonObjectAsync(fs, "game_state/meta/soul_state.json");
        var historyEntry = FindHistoryEntry(guardiansRoot, request.RequestId);
        var foundedGuardian = FindGuardianByFoundationRequestId(guardiansRoot, request.RequestId);
        var linkedGuardianId = GetNodeString(soulRoot?[SoulStateGuardianIdProperty]);

        if (historyEntry != null &&
            foundedGuardian != null &&
            string.Equals(GetNodeString(historyEntry["guardianId"]), GetNodeString(foundedGuardian["guardianId"]), StringComparison.OrdinalIgnoreCase) &&
            string.Equals(linkedGuardianId, GetNodeString(foundedGuardian["guardianId"]), StringComparison.OrdinalIgnoreCase))
        {
            Clear(fs);
        }
    }

    public static async Task<string?> BuildSystemReminderFragmentAsync(FileSystemManager fs, string? currentRealm)
    {
        if (!IsChaosSeaRealm(currentRealm))
            return null;

        var requestState = await ReadStateAsync(fs);
        if (requestState.IsMalformed)
        {
            return "PLAYER-FOUNDED GUARDIAN FOUNDATION CORRUPTION:" + Environment.NewLine +
                   $"  - {PendingRequestPath} unreadable or malformed." + Environment.NewLine +
                   "  - Preserve the pending foundation contract and repair it before writing a new Chaos Sea foundation ritual.";
        }

        var request = requestState.Request;
        if (request == null)
            return null;

        var parts = new List<string>
        {
            "PLAYER-FOUNDED GUARDIAN FOUNDATION:",
            $"  - {PendingRequestPath} is a client-authored late-game Chaos Sea foundation ritual.",
            "  - The player does NOT become a guardian directly and remains player_soul.",
            "  - Resolve this request by materializing a NEW guardian actor through UpdateGuardians.create, not by rewriting the player's soul identity.",
            $"  - New guardian originType must be {OriginTypePlayerFoundedAscendedSoul}, founderLoyaltyTier must be {FounderLoyaltyTierSoulbound}, foundationSource must be {FoundationSourceShiningReturn}.",
            $"  - Keep previous guardian {request.PreviousGuardianName} ({request.PreviousGuardianId}) in guardians[]. Do NOT delete or convert them. Mark their relationshipData.{GuardianRoleToPlayerProperty} as {GuardianRoleFormerPatron}.",
            "  - Make the new guardian the current activeGuardian and bind chaosSeaNavigation.currentAbodeId to the new abode.",
            $"  - Write soul_state.{SoulStateGuardianIdProperty}, soul_state.{SoulStateFoundationStatusProperty}={SoulStateFoundationStatusFounded} and append guardians.json.{HistoryProperty} receipt history.",
            $"  - Founder bonuses should include {FounderBonusesProperty}.{FounderBonusExtraGachaChargesProperty}={DefaultFounderExtraGachaChargesPerReturn}.",
            $"  - Founder abode features should include {FounderAbodeFeaturesProperty}.{FounderAbodeResidentAttractionModeProperty}={FounderAbodeResidentAttractionModeFounderCall} with a concise title/summary. Do NOT migrate the former patron's residents automatically.",
            "  - The former patron may receive ordinary GM-driven narrative follow-up only through allowed afterlife surfaces such as Guardian dialogue, Guardian musings/lore, Guardian relationship/project state, or Soul Quest hooks. Do NOT write Mortal World events or client-derived afterlife_notifications for this.",
            $"  - Proposed display name: {request.ProposedDisplayName}.",
            $"  - Mantle summary: {request.MantleSummary}.",
            $"  - Creed: {request.MantleCreed}.",
            $"  - Appearance motifs: {string.Join(", ", request.AppearanceMotifs)}."
        };

        if (!string.IsNullOrWhiteSpace(request.DominantAspect))
            parts.Add($"  - Dominant aspect: {request.DominantAspect}.");

        return string.Join(Environment.NewLine, parts);
    }

    public static string BuildPendingGmActionText(PendingPlayerGuardianFoundationRequest request)
    {
        var sb = new StringBuilder();
        sb.Append($"[{ActionTag}] Игрок в Море Хаоса учреждает собственного Хранителя «{request.ProposedDisplayName}». ");
        sb.Append($"Обязательно прочитай {PendingRequestPath} как client-authored ritual contract. ");
        sb.Append("Игрок остаётся player_soul: не переписывай душу в guardian actor. ");
        sb.Append("Создай нового Хранителя через UpdateGuardians.create, сохрани прежнего activeGuardian в guardians[] и пометь его relationshipData.guardianRoleToPlayer=former_patron, ");
        sb.Append("сделай новую мантия текущим activeGuardian, привяжи chaosSeaNavigation.currentAbodeId к её обители и запиши soul_state.playerFoundedGuardianId, soul_state.playerGuardianFoundationStatus=founded вместе с guardians.json.playerGuardianFoundationHistory. ");
        sb.Append($"Добавь founderBonuses.{FounderBonusExtraGachaChargesProperty}={DefaultFounderExtraGachaChargesPerReturn} и founderAbodeFeatures с residentAttractionMode={FounderAbodeResidentAttractionModeFounderCall}; старые residents не переносятся автоматически. ");
        sb.Append("former_patron остаётся narrative hook для обычных GM-driven сцен, а не отдельной diplomacy-механикой.");
        return sb.ToString();
    }

    public static PlayerGuardianFoundationHistoryEntry BuildCanonicalHistoryEntry(
        PendingPlayerGuardianFoundationRequest request,
        string guardianId,
        string guardianDisplayName,
        int resolvedAtTurn,
        string? resolvedAtUtc = null)
        => new()
        {
            RequestId = request.RequestId,
            GuardianId = guardianId,
            GuardianDisplayName = guardianDisplayName,
            FounderSoulName = request.FounderSoulName,
            FormerPatronGuardianId = request.PreviousGuardianId,
            FormerPatronGuardianName = request.PreviousGuardianName,
            FoundationSource = FoundationSourceShiningReturn,
            ResolvedAtTurn = Math.Max(0, resolvedAtTurn),
            ResolvedAtUtc = string.IsNullOrWhiteSpace(resolvedAtUtc) ? DateTime.UtcNow.ToString("o") : resolvedAtUtc
        };

    public static void ApplyCanonicalFoundedGuardianSemantics(
        JsonObject guardian,
        PendingPlayerGuardianFoundationRequest request,
        string? guardianId = null)
    {
        guardian["originType"] = OriginTypePlayerFoundedAscendedSoul;
        guardian["founderSoulName"] = request.FounderSoulName;
        guardian["founderLoyaltyTier"] = FounderLoyaltyTierSoulbound;
        guardian["formerPatronGuardianId"] = request.PreviousGuardianId;
        guardian["foundationSource"] = FoundationSourceShiningReturn;
        guardian["foundationRequestId"] = request.RequestId;
        guardian[FounderBonusesProperty] = BuildDefaultFounderBonusesObject();
        guardian[FounderAbodeFeaturesProperty] = BuildDefaultFounderAbodeFeaturesObject(request);
        if (!string.IsNullOrWhiteSpace(guardianId))
            guardian["guardianId"] = guardianId;

        var relationshipData = EnsureGuardianRelationshipData(guardian);
        var currentReputation = TryGetNodeInt(relationshipData["currentReputation"]);
        relationshipData["currentReputation"] = Math.Max(
            SoulboundCanonicalStartingReputation,
            currentReputation > 0 ? currentReputation : 0);
    }

    public static void ApplyCanonicalFormerPatronSemantics(JsonObject guardian)
    {
        var relationshipData = EnsureGuardianRelationshipData(guardian);
        relationshipData[GuardianRoleToPlayerProperty] = GuardianRoleFormerPatron;
    }

    public static JsonArray EnsureFoundationHistoryArray(JsonObject guardiansRoot)
    {
        if (guardiansRoot[HistoryProperty] is not JsonArray history)
        {
            guardiansRoot[HistoryProperty] = new JsonArray();
            history = guardiansRoot[HistoryProperty]!.AsArray();
        }

        return history;
    }

    public static JsonObject? FindHistoryEntry(JsonObject? guardiansRoot, string? requestId)
    {
        if (guardiansRoot?[HistoryProperty] is not JsonArray history || string.IsNullOrWhiteSpace(requestId))
            return null;

        return history.OfType<JsonObject>()
            .FirstOrDefault(entry => string.Equals(GetNodeString(entry["requestId"]), requestId, StringComparison.OrdinalIgnoreCase));
    }

    public static JsonObject? FindLatestHistoryEntry(JsonObject? guardiansRoot)
    {
        if (guardiansRoot?[HistoryProperty] is not JsonArray history)
            return null;

        return history.OfType<JsonObject>()
            .OrderByDescending(entry => TryGetNodeInt(entry["resolvedAtTurn"]))
            .ThenByDescending(entry => GetNodeString(entry["resolvedAtUtc"]) ?? string.Empty, StringComparer.OrdinalIgnoreCase)
            .FirstOrDefault();
    }

    public static JsonObject? FindHistoryEntryByGuardianId(JsonObject? guardiansRoot, string? guardianId)
    {
        if (guardiansRoot?[HistoryProperty] is not JsonArray history || string.IsNullOrWhiteSpace(guardianId))
            return null;

        return history.OfType<JsonObject>()
            .Where(entry => string.Equals(GetNodeString(entry["guardianId"]), guardianId, StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(entry => TryGetNodeInt(entry["resolvedAtTurn"]))
            .ThenByDescending(entry => GetNodeString(entry["resolvedAtUtc"]) ?? string.Empty, StringComparer.OrdinalIgnoreCase)
            .FirstOrDefault();
    }

    public static JsonObject? FindPlayerFoundedGuardian(JsonObject? guardiansRoot)
    {
        if (guardiansRoot?["guardians"] is not JsonArray guardians)
            return null;

        return guardians.OfType<JsonObject>()
            .FirstOrDefault(guardian => string.Equals(GetNodeString(guardian["originType"]), OriginTypePlayerFoundedAscendedSoul, StringComparison.OrdinalIgnoreCase));
    }

    public static JsonObject? FindGuardianById(JsonObject? guardiansRoot, string? guardianId)
    {
        if (guardiansRoot?["guardians"] is not JsonArray guardians || string.IsNullOrWhiteSpace(guardianId))
            return null;

        return guardians.OfType<JsonObject>()
            .FirstOrDefault(guardian => string.Equals(GetNodeString(guardian["guardianId"]), guardianId, StringComparison.OrdinalIgnoreCase));
    }

    public static JsonObject? FindGuardianByFoundationRequestId(JsonObject? guardiansRoot, string? requestId)
    {
        if (guardiansRoot?["guardians"] is not JsonArray guardians || string.IsNullOrWhiteSpace(requestId))
            return null;

        return guardians.OfType<JsonObject>()
            .FirstOrDefault(guardian => string.Equals(GetNodeString(guardian["foundationRequestId"]), requestId, StringComparison.OrdinalIgnoreCase));
    }

    public static bool HasPlayerFoundedGuardian(JsonObject? guardiansRoot) => FindPlayerFoundedGuardian(guardiansRoot) != null;

    public static bool IsPlayerFoundedGuardian(JsonObject? guardian) =>
        string.Equals(GetNodeString(guardian?["originType"]), OriginTypePlayerFoundedAscendedSoul, StringComparison.OrdinalIgnoreCase);

    public static bool IsPlayerFoundedGuardian(JsonElement guardian)
    {
        if (!guardian.TryGetProperty("originType", out var originType) || originType.ValueKind != JsonValueKind.String)
            return false;

        return string.Equals(originType.GetString(), OriginTypePlayerFoundedAscendedSoul, StringComparison.OrdinalIgnoreCase);
    }

    public static string? TryReadGuardianRoleToPlayer(JsonObject? guardian)
        => guardian?["relationshipData"] is JsonObject relationshipData
            ? GetNodeString(relationshipData[GuardianRoleToPlayerProperty])
            : null;

    public static string? TryReadGuardianRoleToPlayer(JsonElement guardian)
    {
        if (!guardian.TryGetProperty("relationshipData", out var relationshipData) ||
            relationshipData.ValueKind != JsonValueKind.Object ||
            !relationshipData.TryGetProperty(GuardianRoleToPlayerProperty, out var guardianRoleToPlayer) ||
            guardianRoleToPlayer.ValueKind != JsonValueKind.String)
        {
            return null;
        }

        return guardianRoleToPlayer.GetString();
    }

    public static bool IsSoulboundReputationSatisfied(int currentReputation)
        => currentReputation >= SoulboundLegendaryReputationFloor;

    public static int GetFounderExtraGachaCharges(JsonObject? guardian)
    {
        if (!IsPlayerFoundedGuardian(guardian))
            return 0;

        var configured = TryGetNodeInt(guardian?[FounderBonusesProperty]?[FounderBonusExtraGachaChargesProperty]);
        return Math.Max(0, configured > 0 ? configured : DefaultFounderExtraGachaChargesPerReturn);
    }

    public static int GetFounderExtraGachaCharges(JsonElement guardian)
    {
        if (!IsPlayerFoundedGuardian(guardian))
            return 0;

        if (guardian.TryGetProperty(FounderBonusesProperty, out var founderBonuses) &&
            founderBonuses.ValueKind == JsonValueKind.Object &&
            founderBonuses.TryGetProperty(FounderBonusExtraGachaChargesProperty, out var configuredNode) &&
            configuredNode.ValueKind == JsonValueKind.Number &&
            configuredNode.TryGetInt32(out var configured) &&
            configured > 0)
        {
            return configured;
        }

        return DefaultFounderExtraGachaChargesPerReturn;
    }

    public static string GetFounderAbodeFeatureTitle(JsonObject? guardian)
    {
        if (!IsPlayerFoundedGuardian(guardian))
            return string.Empty;

        return GetNodeString(guardian?[FounderAbodeFeaturesProperty]?[FounderAbodeFeatureTitleProperty]) ??
               "Зов основанной мантии";
    }

    public static string GetFounderAbodeFeatureTitle(JsonElement guardian)
    {
        if (!IsPlayerFoundedGuardian(guardian))
            return string.Empty;

        if (guardian.TryGetProperty(FounderAbodeFeaturesProperty, out var features) &&
            features.ValueKind == JsonValueKind.Object &&
            features.TryGetProperty(FounderAbodeFeatureTitleProperty, out var title) &&
            title.ValueKind == JsonValueKind.String)
        {
            return title.GetString() ?? string.Empty;
        }

        return "Зов основанной мантии";
    }

    public static string GetFounderAbodeFeatureSummary(JsonObject? guardian)
    {
        if (!IsPlayerFoundedGuardian(guardian))
            return string.Empty;

        return GetNodeString(guardian?[FounderAbodeFeaturesProperty]?[FounderAbodeFeatureSummaryProperty]) ??
               "Новая Обитель начинает притягивать первых резидентов, откликнувшихся на основанную мантию. Старые резиденты не переходят автоматически.";
    }

    public static string GetFounderAbodeFeatureSummary(JsonElement guardian)
    {
        if (!IsPlayerFoundedGuardian(guardian))
            return string.Empty;

        if (guardian.TryGetProperty(FounderAbodeFeaturesProperty, out var features) &&
            features.ValueKind == JsonValueKind.Object &&
            features.TryGetProperty(FounderAbodeFeatureSummaryProperty, out var summary) &&
            summary.ValueKind == JsonValueKind.String)
        {
            return summary.GetString() ?? string.Empty;
        }

        return "Новая Обитель начинает притягивать первых резидентов, откликнувшихся на основанную мантию. Старые резиденты не переходят автоматически.";
    }

    public static string GetFounderAbodeResidentAttractionMode(JsonObject? guardian)
    {
        if (!IsPlayerFoundedGuardian(guardian))
            return string.Empty;

        return GetNodeString(guardian?[FounderAbodeFeaturesProperty]?[FounderAbodeResidentAttractionModeProperty]) ??
               FounderAbodeResidentAttractionModeFounderCall;
    }

    public static bool TryDescribeFoundedGuardianContractMismatch(
        JsonObject guardian,
        PendingPlayerGuardianFoundationRequest request,
        out string actual)
    {
        actual = string.Empty;

        if (!string.Equals(GetNodeString(guardian["originType"]), OriginTypePlayerFoundedAscendedSoul, StringComparison.OrdinalIgnoreCase))
        {
            actual = $"guardian originType is not {OriginTypePlayerFoundedAscendedSoul}";
            return false;
        }

        if (!string.Equals(GetNodeString(guardian["foundationRequestId"]), request.RequestId, StringComparison.OrdinalIgnoreCase))
        {
            actual = "guardian foundationRequestId does not match requestId";
            return false;
        }

        if (!string.Equals(GetNodeString(guardian["founderSoulName"]), request.FounderSoulName, StringComparison.OrdinalIgnoreCase))
        {
            actual = "guardian founderSoulName does not match request founderSoulName";
            return false;
        }

        if (!string.Equals(GetNodeString(guardian["founderLoyaltyTier"]), FounderLoyaltyTierSoulbound, StringComparison.OrdinalIgnoreCase))
        {
            actual = $"guardian founderLoyaltyTier is not {FounderLoyaltyTierSoulbound}";
            return false;
        }

        if (!string.Equals(GetNodeString(guardian["formerPatronGuardianId"]), request.PreviousGuardianId, StringComparison.OrdinalIgnoreCase))
        {
            actual = "guardian formerPatronGuardianId does not match request.previousGuardianId";
            return false;
        }

        if (!string.Equals(GetNodeString(guardian["foundationSource"]), FoundationSourceShiningReturn, StringComparison.OrdinalIgnoreCase))
        {
            actual = $"guardian foundationSource is not {FoundationSourceShiningReturn}";
            return false;
        }

        if (guardian[FounderBonusesProperty] is not JsonObject)
        {
            actual = $"guardian {FounderBonusesProperty} is missing";
            return false;
        }

        if (GetFounderExtraGachaCharges(guardian) < DefaultFounderExtraGachaChargesPerReturn)
        {
            actual = $"guardian {FounderBonusesProperty}.{FounderBonusExtraGachaChargesProperty} is below canonical founder bonus";
            return false;
        }

        if (guardian[FounderAbodeFeaturesProperty] is not JsonObject)
        {
            actual = $"guardian {FounderAbodeFeaturesProperty} is missing";
            return false;
        }

        if (!string.Equals(GetFounderAbodeResidentAttractionMode(guardian), FounderAbodeResidentAttractionModeFounderCall, StringComparison.OrdinalIgnoreCase))
        {
            actual = $"guardian {FounderAbodeFeaturesProperty}.{FounderAbodeResidentAttractionModeProperty} is not {FounderAbodeResidentAttractionModeFounderCall}";
            return false;
        }

        var relationshipData = guardian["relationshipData"] as JsonObject;
        var currentReputation = relationshipData != null ? TryGetNodeInt(relationshipData["currentReputation"]) : 0;
        if (!IsSoulboundReputationSatisfied(currentReputation))
        {
            actual = $"guardian currentReputation {currentReputation} is below soulbound legendary floor {SoulboundLegendaryReputationFloor}";
            return false;
        }

        return true;
    }

    private static async Task<JsonObject?> ReadJsonObjectAsync(
        FileSystemManager fs,
        string relativePath) =>
        await ReadJsonObjectAsync(fs, writeLease: null, relativePath);

    private static async Task<JsonObject?> ReadJsonObjectAsync(
        FileSystemManager fs,
        FileSystemManager.CanonicalWriteLease? writeLease,
        string relativePath)
    {
        var json = writeLease == null
            ? await fs.ReadFileAsync(relativePath)
            : await fs.ReadFileAsync(writeLease, relativePath);
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

    private static string ResolveBlockingReason(
        string currentRealm,
        string shiningAvailability,
        string foundationStatus,
        bool hasPreparedPackage,
        AfterlifeReturnGuardSemanticState returnGuardState,
        string soulName,
        string previousGuardianId,
        bool pendingRequestMalformed,
        PendingPlayerGuardianFoundationRequest? pendingRequest,
        string existingFoundedGuardianId)
    {
        if (!IsChaosSeaRealm(currentRealm))
            return "Основывать собственного Хранителя можно только в Море Хаоса.";
        if (string.IsNullOrWhiteSpace(soulName))
            return "Текущее имя души недоступно.";
        if (!string.Equals(shiningAvailability, ShiningAbodeState.AvailabilitySealedUntilNextAscension, StringComparison.OrdinalIgnoreCase))
            return "Сначала нужно сознательно запечатать Сияющую Обитель через return_to_chaos_sea.";
        if (hasPreparedPackage)
            return "Foundation branch недоступна, пока существует preparedIncarnationPackage handoff.";
        if (returnGuardState is AfterlifeReturnGuardSemanticState.ActiveValid or AfterlifeReturnGuardSemanticState.BlockingInvalid)
            return "После возврата из смертной жизни сначала нужен хотя бы один обычный ход Моря Хаоса без active afterlife return guard.";
        if (pendingRequestMalformed)
            return "pending_player_guardian_foundation.json повреждён. Исправьте или очистите pending foundation contract перед новой попыткой.";
        if (pendingRequest != null)
            return "Уже существует незавершённый ritual foundation request.";
        if (!string.IsNullOrWhiteSpace(existingFoundedGuardianId) ||
            string.Equals(foundationStatus, SoulStateFoundationStatusFounded, StringComparison.OrdinalIgnoreCase))
        {
            return "В этом сохранении foundation route уже завершена: основанный Хранитель остаётся с вами как single-use late-game ветка.";
        }
        if (string.IsNullOrWhiteSpace(previousGuardianId))
            return "Foundation route требует текущего activeGuardian.";
        return string.Empty;
    }

    private static bool IsChaosSeaRealm(string? currentRealm) =>
        string.Equals(currentRealm, "Chaos Sea", StringComparison.OrdinalIgnoreCase) ||
        string.Equals(currentRealm, "Море Хаоса", StringComparison.OrdinalIgnoreCase);

    private static string? GetNodeString(JsonNode? node)
    {
        if (node is JsonValue value && value.TryGetValue<string>(out var text))
            return text;

        return null;
    }

    private static int TryGetNodeInt(JsonNode? node)
    {
        if (node is JsonValue value && value.TryGetValue<int>(out var number))
            return number;

        return 0;
    }

    private static JsonObject EnsureGuardianRelationshipData(JsonObject guardian)
    {
        if (guardian["relationshipData"] is not JsonObject relationshipData)
        {
            guardian["relationshipData"] = new JsonObject
            {
                ["currentReputation"] = SoulboundCanonicalStartingReputation,
                ["reputationHistory"] = new JsonArray(),
                ["lastInteraction"] = null
            };
            relationshipData = guardian["relationshipData"]!.AsObject();
        }

        if (relationshipData["reputationHistory"] is not JsonArray)
            relationshipData["reputationHistory"] = new JsonArray();
        if (!relationshipData.ContainsKey("lastInteraction"))
            relationshipData["lastInteraction"] = null;

        return relationshipData;
    }

    private static JsonObject BuildDefaultFounderBonusesObject() => new()
    {
        [FounderBonusExtraGachaChargesProperty] = DefaultFounderExtraGachaChargesPerReturn
    };

    private static JsonObject BuildDefaultFounderAbodeFeaturesObject(PendingPlayerGuardianFoundationRequest request) => new()
    {
        [FounderAbodeResidentAttractionModeProperty] = FounderAbodeResidentAttractionModeFounderCall,
        [FounderAbodeFeatureTitleProperty] = ResolveFounderAbodeFeatureTitle(request.DominantAspect),
        [FounderAbodeFeatureSummaryProperty] = ResolveFounderAbodeFeatureSummary(request)
    };

    private static string ResolveFounderAbodeFeatureTitle(string? dominantAspect) =>
        (dominantAspect ?? string.Empty).Trim().ToLowerInvariant() switch
        {
            "memory" => "Зов памяти",
            "forge" => "Зов кузни",
            "knowledge" => "Зов знания",
            "patronage" => "Зов покровительства",
            "power" => "Зов власти",
            "path" => "Зов пути",
            _ => "Зов основанной мантии"
        };

    private static string ResolveFounderAbodeFeatureSummary(PendingPlayerGuardianFoundationRequest request)
    {
        var creed = string.IsNullOrWhiteSpace(request.MantleCreed) ? "её собственному закону" : $"кредо «{request.MantleCreed}»";
        return $"Новая Обитель начинает притягивать первых резидентов, откликнувшихся на {creed}. Старые резиденты не переходят автоматически из бывшей patron-ветки.";
    }

    private static JsonElement ToJsonElement(JsonObject obj)
    {
        using var doc = JsonDocument.Parse(obj.ToJsonString(JsonOpts));
        return doc.RootElement.Clone();
    }
}
