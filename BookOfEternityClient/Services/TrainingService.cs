using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using BookOfEternityClient.Configuration;
using BookOfEternityClient.Core;
using Microsoft.Extensions.Logging;

namespace BookOfEternityClient.Services;

public sealed class TrainingService
{
    private const string NpcCorePath = "game_state/npcs/npc_core.json";
    private const string SoulStatePath = "game_state/meta/soul_state.json";
    private const string PlayerStatusPath = "game_state/core/player_status.json";
    private const string PlayerExperiencePath = "game_state/player/experience.json";
    private const string ActiveSkillsPath = "game_state/player/skills_active.json";
    private const string PassiveSkillsPath = "game_state/player/skills_passive.json";
    private const string SkillMasteryPath = "game_state/player/skill_mastery.json";
    private const string ShiningAbodeStatePath = "game_state/meta/shining_abode_state.json";
    private const string AfterlifeEntityProfilesPath = "game_state/meta/afterlife_entity_profiles.json";

    private const string RealmMortal = "mortal";
    private const string RealmAfterlife = "afterlife";
    private const string MortalRequestKind = "mortal_teacher_showcase";
    private const string AfterlifeRequestKind = "afterlife_teacher_showcase";
    private const int SpiritualArtMaxTier = 5;

    private static readonly JsonSerializerOptions JsonOpts = SharedJsonOptions.PrettyCamelCaseUnsafeRelaxed;

    private readonly FileSystemManager _fs;
    private readonly ILogger<TrainingService> _logger;

    public TrainingService(FileSystemManager fs, ILogger<TrainingService> logger)
    {
        _fs = fs;
        _logger = logger;
    }

    public sealed record TrainingView(
        string Realm,
        IReadOnlyList<TrainingTeacherView> Teachers,
        IReadOnlyList<TrainingOffer> SelfTrainingOffers,
        bool RequestPending,
        bool RequestCreatedThisCall,
        string? PendingGmAction);

    public sealed record TrainingTeacherView(
        string SourceActorId,
        string SourceActorName,
        string SourceActorKind,
        bool ShowcaseReady,
        bool ShowcaseStale,
        string? BlockReason,
        IReadOnlyList<TrainingOffer> Offers);

    public sealed record TrainingOffer(
        string OfferId,
        string TargetId,
        string TargetName,
        string TargetKind,
        int CurrentValue,
        int TargetValue,
        int SourceCap,
        bool Available,
        string? BlockReason,
        TrainingCost Cost,
        JsonObject Details);

    public sealed record TrainingCost(
        int Money,
        int CurrentLevelExperiencePercent,
        int CurrentLevelExperiencePoints,
        int InkFeathers,
        int LightSparks);

    public sealed record TrainingOperationResult(bool Success, bool StateChanged, string Message);

    public async Task<TrainingView> EnsureTrainingAsync(int currentTurn, bool createPendingRequests = true)
    {
        if (currentTurn <= 0)
            throw new ArgumentOutOfRangeException(nameof(currentTurn), "Проверка витрины обучения требует актуальный номер хода.");

        var realm = NormalizeRealm(await ReadCurrentRealmAsync());
        return realm == RealmAfterlife
            ? await BuildAfterlifeTrainingViewAsync(currentTurn, createPendingRequests)
            : await BuildMortalTrainingViewAsync(currentTurn, createPendingRequests);
    }

    public async Task<TrainingOperationResult> BuyTrainingAsync(string sourceActorId, string offerId, int currentTurn)
    {
        if (currentTurn <= 0)
            return new TrainingOperationResult(false, false, "Покупка обучения требует актуальный номер хода.");

        var realm = NormalizeRealm(await ReadCurrentRealmAsync());
        if (realm == RealmAfterlife)
        {
            if (string.Equals(sourceActorId, "self", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(sourceActorId, "self_fallback", StringComparison.OrdinalIgnoreCase))
            {
                return await BuyAfterlifeSelfTrainingAsync(sourceActorId, offerId, currentTurn);
            }

            return await BuyAfterlifeMentorTrainingAsync(sourceActorId, offerId, currentTurn);
        }

        if (realm != RealmMortal)
            return new TrainingOperationResult(false, false, "Этот локальный способ покупки обучения сейчас доступен только в смертном мире.");

        var npcRoot = await ReadObjectAsync(NpcCorePath);
        if (npcRoot == null)
            return new TrainingOperationResult(false, false, "Нет данных НПС для обучения.");

        var teacher = EnumerateNpcObjects(npcRoot)
            .FirstOrDefault(npc => string.Equals(ResolveMortalTeacherActorId(npc), sourceActorId, StringComparison.OrdinalIgnoreCase));
        if (teacher == null)
            return new TrainingOperationResult(false, false, "Учитель не найден.");

        var offer = TryReadFreshShowcaseOffer(teacher, offerId, out var showcaseBlockReason);
        if (offer == null)
            return new TrainingOperationResult(false, false, showcaseBlockReason ?? "Предложение обучения не найдено или витрина устарела.");

        var evaluatedOffer = EvaluateMortalOffer(teacher, offer);
        if (!evaluatedOffer.Available)
            return new TrainingOperationResult(false, false, evaluatedOffer.BlockReason ?? "Это обучение сейчас недоступно.");

        var statusRoot = await ReadObjectAsync(PlayerStatusPath) ?? new JsonObject();
        var experienceRoot = await ReadObjectAsync(PlayerExperiencePath) ?? new JsonObject();
        var currentMoney = GetNodeInt(statusRoot["money"]);
        if (currentMoney < evaluatedOffer.Cost.Money)
            return new TrainingOperationResult(false, false, "Недостаточно денег для обучения.");

        var currentLevelExperience = ReadCurrentLevelExperience(experienceRoot);
        if (currentLevelExperience < evaluatedOffer.Cost.CurrentLevelExperiencePoints)
        {
            return new TrainingOperationResult(
                false,
                false,
                "Недостаточно опыта текущего уровня для обучения: клиент не списывает опыт в минус и не понижает уровень.");
        }

        var activeRoot = await ReadObjectAsync(ActiveSkillsPath) ?? new JsonObject { ["activeSkillChanges"] = new JsonArray() };
        var passiveRoot = await ReadObjectAsync(PassiveSkillsPath) ?? new JsonObject { ["passiveSkillChanges"] = new JsonArray() };
        var masteryRoot = await ReadObjectAsync(SkillMasteryPath) ?? new JsonObject { ["skillMasteryChanges"] = new JsonArray() };

        statusRoot["money"] = currentMoney - evaluatedOffer.Cost.Money;
        experienceRoot["currentLevelExperience"] = currentLevelExperience - evaluatedOffer.Cost.CurrentLevelExperiencePoints;
        if (!experienceRoot.ContainsKey("experienceForNextLevel"))
            experienceRoot["experienceForNextLevel"] = InferExperienceForNextLevel(experienceRoot);

        ApplyMortalSkillTraining(activeRoot, passiveRoot, masteryRoot, evaluatedOffer);
        AppendMortalTrainingReceipt(npcRoot, teacher, evaluatedOffer, currentTurn);

        await _fs.WriteFileAtomicAsync(PlayerStatusPath, statusRoot.ToJsonString(JsonOpts));
        await _fs.WriteFileAtomicAsync(PlayerExperiencePath, experienceRoot.ToJsonString(JsonOpts));
        await _fs.WriteFileAtomicAsync(ActiveSkillsPath, activeRoot.ToJsonString(JsonOpts));
        await _fs.WriteFileAtomicAsync(PassiveSkillsPath, passiveRoot.ToJsonString(JsonOpts));
        await _fs.WriteFileAtomicAsync(SkillMasteryPath, masteryRoot.ToJsonString(JsonOpts));
        await _fs.WriteFileAtomicAsync(NpcCorePath, npcRoot.ToJsonString(JsonOpts));

        return new TrainingOperationResult(true, true, "Обучение завершено.");
    }

    private async Task<TrainingOperationResult> BuyAfterlifeSelfTrainingAsync(string sourceActorId, string offerId, int currentTurn)
    {
        if (!string.Equals(sourceActorId, "self", StringComparison.OrdinalIgnoreCase) &&
            !string.Equals(sourceActorId, "self_fallback", StringComparison.OrdinalIgnoreCase))
        {
            return new TrainingOperationResult(false, false, "Наставническая витрина посмертия ещё не подготовлена. Для самостоятельной прокачки используйте источник self.");
        }

        var soulRoot = await ReadObjectAsync(SoulStatePath);
        if (soulRoot == null)
            return new TrainingOperationResult(false, false, "Нет состояния души для обучения.");

        var shiningRoot = await ReadObjectAsync(ShiningAbodeStatePath);
        var profile = EnsureAfterlifeCombatProfile(soulRoot, shiningRoot);
        soulRoot[AfterlifeSpiritualConflictState.SoulStateProfileProperty] = profile;

        var offer = BuildAfterlifeSelfTrainingOffers(profile, soulRoot, shiningRoot)
            .FirstOrDefault(candidate => string.Equals(candidate.OfferId, offerId, StringComparison.OrdinalIgnoreCase));
        if (offer == null)
            return new TrainingOperationResult(false, false, "Предложение самостоятельной прокачки не найдено.");
        if (!offer.Available)
            return new TrainingOperationResult(false, false, offer.BlockReason ?? "Это обучение сейчас недоступно.");

        var inkFeathers = NormalizeInkFeathers(soulRoot);
        var currentFeathers = Math.Max(0, GetNodeInt(inkFeathers["current"]));
        if (currentFeathers < offer.Cost.InkFeathers)
            return new TrainingOperationResult(false, false, "Недостаточно Чернильных Перьев для самостоятельной прокачки.");

        var currentSparks = 0;
        if (offer.Cost.LightSparks > 0)
        {
            var shiningState = await ReadObjectAsync(ShiningAbodeStatePath) ?? new JsonObject();
            currentSparks = Math.Max(0, GetNodeInt(shiningState["lightSparks"]));
            if (currentSparks < offer.Cost.LightSparks)
                return new TrainingOperationResult(false, false, "Недостаточно Искр Света для самостоятельной прокачки.");
        }

        inkFeathers["current"] = currentFeathers - offer.Cost.InkFeathers;
        soulRoot["inkFeathers"] = inkFeathers;

        ApplyAfterlifeTraining(profile, offer);
        AppendAfterlifeTrainingReceipt(soulRoot, offer, currentTurn);

        await _fs.WriteFileAtomicAsync(SoulStatePath, GuardianPolicyContracts.CreateCanonicalSoulStateWriteRoot(soulRoot).ToJsonString(JsonOpts));
        if (offer.Cost.LightSparks > 0)
        {
            var shiningState = await ReadObjectAsync(ShiningAbodeStatePath) ?? new JsonObject();
            shiningState["lightSparks"] = currentSparks - offer.Cost.LightSparks;
            await _fs.WriteFileAtomicAsync(ShiningAbodeStatePath, shiningState.ToJsonString(JsonOpts));
        }

        return new TrainingOperationResult(true, true, "Самостоятельная прокачка завершена.");
    }

    private async Task<TrainingOperationResult> BuyAfterlifeMentorTrainingAsync(string sourceActorId, string offerId, int currentTurn)
    {
        var soulRoot = await ReadObjectAsync(SoulStatePath);
        if (soulRoot == null)
            return new TrainingOperationResult(false, false, "Нет состояния души для обучения.");

        var afterlifeProfilesRoot = await ReadObjectAsync(AfterlifeEntityProfilesPath);
        var mentor = EnumerateAfterlifeProfiles(afterlifeProfilesRoot)
            .FirstOrDefault(profile => string.Equals(ResolveAfterlifeActorId(profile), sourceActorId, StringComparison.OrdinalIgnoreCase));
        if (mentor == null)
            return new TrainingOperationResult(false, false, "Наставник не найден.");

        var offer = TryReadFreshMentorShowcaseOffer(mentor, offerId, out var showcaseBlockReason);
        if (offer == null)
            return new TrainingOperationResult(false, false, showcaseBlockReason ?? "Предложение наставника не найдено или витрина устарела.");

        var shiningRoot = await ReadObjectAsync(ShiningAbodeStatePath);
        var profile = EnsureAfterlifeCombatProfile(soulRoot, shiningRoot);
        soulRoot[AfterlifeSpiritualConflictState.SoulStateProfileProperty] = profile;

        var evaluatedOffer = EvaluateAfterlifeMentorOffer(mentor, offer, profile, shiningRoot);
        if (!evaluatedOffer.Available)
            return new TrainingOperationResult(false, false, evaluatedOffer.BlockReason ?? "Это обучение сейчас недоступно.");

        var inkFeathers = NormalizeInkFeathers(soulRoot);
        var currentFeathers = Math.Max(0, GetNodeInt(inkFeathers["current"]));
        if (currentFeathers < evaluatedOffer.Cost.InkFeathers)
            return new TrainingOperationResult(false, false, "Недостаточно Чернильных Перьев для обучения у наставника.");

        var currentSparks = 0;
        if (evaluatedOffer.Cost.LightSparks > 0)
        {
            var shiningState = shiningRoot ?? new JsonObject();
            currentSparks = Math.Max(0, GetNodeInt(shiningState["lightSparks"]));
            if (currentSparks < evaluatedOffer.Cost.LightSparks)
                return new TrainingOperationResult(false, false, "Недостаточно Искр Света для обучения у наставника.");
        }

        inkFeathers["current"] = currentFeathers - evaluatedOffer.Cost.InkFeathers;
        soulRoot["inkFeathers"] = inkFeathers;

        ApplyAfterlifeTraining(profile, evaluatedOffer);
        var mentorId = ResolveAfterlifeActorId(mentor);
        AppendAfterlifeTrainingReceipt(
            soulRoot,
            evaluatedOffer,
            currentTurn,
            mentorId,
            ResolveAfterlifeActorName(mentor, mentorId),
            "afterlife_mentor",
            ComputeSourceSnapshotHash(mentor));

        await _fs.WriteFileAtomicAsync(SoulStatePath, GuardianPolicyContracts.CreateCanonicalSoulStateWriteRoot(soulRoot).ToJsonString(JsonOpts));
        if (evaluatedOffer.Cost.LightSparks > 0)
        {
            var shiningState = shiningRoot ?? new JsonObject();
            shiningState["lightSparks"] = currentSparks - evaluatedOffer.Cost.LightSparks;
            await _fs.WriteFileAtomicAsync(ShiningAbodeStatePath, shiningState.ToJsonString(JsonOpts));
        }

        return new TrainingOperationResult(true, true, "Обучение у наставника завершено.");
    }

    public static string ComputeSourceSnapshotHash(JsonObject sourceActor)
    {
        var snapshot = BuildSourceSnapshotNode(sourceActor);
        var canonical = BuildCanonicalJson(snapshot);
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(canonical));
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }

    private async Task<TrainingView> BuildMortalTrainingViewAsync(int currentTurn, bool createPendingRequests)
    {
        var npcRoot = await ReadObjectAsync(NpcCorePath);
        var teachers = new List<TrainingTeacherView>();
        var requestPending = false;
        var requestCreated = false;
        string? pendingGmAction = null;

        foreach (var teacher in EnumerateNpcObjects(npcRoot).Where(IsMortalTeacher))
        {
            var sourceActorId = ResolveMortalTeacherActorId(teacher);
            var sourceActorName = GetNodeString(teacher["name"]) ?? sourceActorId;
            var showcase = teacher["trainingShowcase"] as JsonObject;
            var expectedHash = ComputeSourceSnapshotHash(teacher);
            var actualHash = GetNodeString(showcase?["sourceActorSnapshotHash"]);
            var hasFreshShowcase = showcase != null &&
                                   string.Equals(actualHash, expectedHash, StringComparison.OrdinalIgnoreCase);

            if (!hasFreshShowcase)
            {
                var reason = showcase == null ? "missing_showcase" : "stale_source_actor_snapshot";
                var existing = await TrainingRequestState.FindPendingRequestAsync(_fs, sourceActorId, MortalRequestKind);
                if (existing == null && createPendingRequests)
                {
                    existing = await TrainingRequestState.WriteRequestAsync(
                        _fs,
                        MortalRequestKind,
                        sourceActorId,
                        sourceActorName,
                        "npc_teacher",
                        RealmMortal,
                        currentTurn,
                        expectedHash,
                        reason);
                    requestCreated = true;
                }

                if (existing != null)
                {
                    requestPending = true;
                    pendingGmAction ??= BuildMortalTrainingPendingGmAction(existing);
                }

                teachers.Add(new TrainingTeacherView(
                    sourceActorId,
                    sourceActorName,
                    "npc_teacher",
                    ShowcaseReady: false,
                    ShowcaseStale: showcase != null,
                    BlockReason: showcase == null
                        ? "ГМ ещё не подготовил витрину обучения."
                        : "Витрина обучения устарела после изменения учителя.",
                    Offers: Array.Empty<TrainingOffer>()));
                continue;
            }

            var offers = ReadOfferObjects(showcase!)
                .Select(offer => EvaluateMortalOffer(teacher, offer))
                .ToArray();

            teachers.Add(new TrainingTeacherView(
                sourceActorId,
                sourceActorName,
                "npc_teacher",
                ShowcaseReady: true,
                ShowcaseStale: false,
                BlockReason: null,
                Offers: offers));
        }

        return new TrainingView(
            RealmMortal,
            teachers,
            Array.Empty<TrainingOffer>(),
            requestPending,
            requestCreated,
            pendingGmAction);
    }

    private async Task<TrainingView> BuildAfterlifeTrainingViewAsync(int currentTurn, bool createPendingRequests)
    {
        var soulRoot = await ReadObjectAsync(SoulStatePath) ?? new JsonObject();
        var shiningRoot = await ReadObjectAsync(ShiningAbodeStatePath);
        var afterlifeProfilesRoot = await ReadObjectAsync(AfterlifeEntityProfilesPath);
        var profile = EnsureAfterlifeCombatProfile(soulRoot, shiningRoot);
        var selfOffers = BuildAfterlifeSelfTrainingOffers(profile, soulRoot, shiningRoot).ToArray();
        var teachers = new List<TrainingTeacherView>();
        var requestPending = false;
        var requestCreated = false;
        string? pendingGmAction = null;

        foreach (var mentor in EnumerateAfterlifeProfiles(afterlifeProfilesRoot).Where(IsAfterlifeMentor))
        {
            var sourceActorId = ResolveAfterlifeActorId(mentor);
            if (string.IsNullOrWhiteSpace(sourceActorId))
                continue;

            var sourceActorName = ResolveAfterlifeActorName(mentor, sourceActorId);
            var sourceActorKind = "afterlife_mentor";
            var showcase = mentor["mentorTrainingShowcase"] as JsonObject;
            var expectedHash = ComputeSourceSnapshotHash(mentor);
            var actualHash = GetNodeString(showcase?["sourceActorSnapshotHash"]);
            var hasFreshShowcase = showcase != null &&
                                   string.Equals(actualHash, expectedHash, StringComparison.OrdinalIgnoreCase);

            if (!hasFreshShowcase)
            {
                var reason = showcase == null ? "missing_showcase" : "stale_source_actor_snapshot";
                var existing = await TrainingRequestState.FindPendingRequestAsync(_fs, sourceActorId, AfterlifeRequestKind);
                if (existing == null && createPendingRequests)
                {
                    existing = await TrainingRequestState.WriteRequestAsync(
                        _fs,
                        AfterlifeRequestKind,
                        sourceActorId,
                        sourceActorName,
                        sourceActorKind,
                        RealmAfterlife,
                        currentTurn,
                        expectedHash,
                        reason);
                    requestCreated = true;
                }

                if (existing != null)
                {
                    requestPending = true;
                    pendingGmAction ??= BuildAfterlifeTrainingPendingGmAction(existing);
                }

                teachers.Add(new TrainingTeacherView(
                    sourceActorId,
                    sourceActorName,
                    sourceActorKind,
                    ShowcaseReady: false,
                    ShowcaseStale: showcase != null,
                    BlockReason: showcase == null
                        ? "ГМ ещё не подготовил витрину наставника."
                        : "Витрина наставника устарела после изменения профиля.",
                    Offers: Array.Empty<TrainingOffer>()));
                continue;
            }

            var offers = ReadOfferObjects(showcase!)
                .Select(offer => EvaluateAfterlifeMentorOffer(mentor, offer, profile, shiningRoot))
                .ToArray();

            teachers.Add(new TrainingTeacherView(
                sourceActorId,
                sourceActorName,
                sourceActorKind,
                ShowcaseReady: true,
                ShowcaseStale: false,
                BlockReason: null,
                Offers: offers));
        }

        return new TrainingView(
            RealmAfterlife,
            teachers,
            selfOffers,
            requestPending,
            requestCreated,
            pendingGmAction);
    }

    private TrainingOffer EvaluateMortalOffer(JsonObject teacher, JsonObject offer)
    {
        var targetId = GetNodeString(offer["targetId"]) ?? "";
        var targetName = GetNodeString(offer["targetName"]) ?? GetNodeString(offer["skillName"]) ?? targetId;
        var targetKind = GetNodeString(offer["targetKind"]) ?? "active_skill_mastery";
        var currentValue = Math.Max(0, GetNodeInt(offer["currentValue"]));
        var targetValue = Math.Max(0, GetNodeInt(offer["targetValue"]));
        var sourceCap = Math.Max(0, GetNodeInt(offer["sourceCap"]));
        var cost = offer["cost"] as JsonObject;
        var experiencePercent = Math.Max(0, GetNodeInt(cost?["currentLevelExperiencePercent"]));
        var experienceForNextLevel = InferExperienceForNextLevelFromFile();
        var experiencePoints = checked(experienceForNextLevel * experiencePercent / 100);
        var trainingCost = new TrainingCost(
            Money: Math.Max(0, GetNodeInt(cost?["money"])),
            CurrentLevelExperiencePercent: experiencePercent,
            CurrentLevelExperiencePoints: experiencePoints,
            InkFeathers: 0,
            LightSparks: 0);

        string? blockReason = null;
        if (string.IsNullOrWhiteSpace(targetId))
            blockReason = "В предложении нет цели обучения.";
        else if (targetValue <= currentValue)
            blockReason = "Предложение не повышает текущий уровень навыка.";
        else if (sourceCap <= 0 || targetValue > sourceCap)
            blockReason = "Предложение превышает уровень, которым владеет учитель.";
        else if (trainingCost.Money <= 0 && trainingCost.CurrentLevelExperiencePoints <= 0)
            blockReason = "У обучения должна быть положительная цена.";
        else
        {
            var minimumRelationship = GetNodeInt((offer["requirements"] as JsonObject)?["minimumRelationship"]);
            var relationshipLevel = GetNodeInt((teacher["teacherProfile"] as JsonObject)?["relationshipLevel"]);
            if (minimumRelationship > relationshipLevel)
                blockReason = $"нужно отношение не ниже {minimumRelationship}, сейчас {relationshipLevel}";
        }

        var details = CloneObject(offer);
        details["sourceActorId"] = ResolveMortalTeacherActorId(teacher);
        details["sourceActorName"] = GetNodeString(teacher["name"]) ?? "";

        return new TrainingOffer(
            GetNodeString(offer["offerId"]) ?? "",
            targetId,
            targetName,
            targetKind,
            currentValue,
            targetValue,
            sourceCap,
            blockReason == null,
            blockReason,
            trainingCost,
            details);
    }

    private TrainingOffer EvaluateAfterlifeMentorOffer(
        JsonObject mentor,
        JsonObject offer,
        JsonObject playerProfile,
        JsonObject? shiningRoot)
    {
        var targetId = GetNodeString(offer["targetId"]) ?? "";
        var targetName = GetNodeString(offer["targetName"]) ?? targetId;
        var targetKind = GetNodeString(offer["targetKind"]) ?? "standard_spiritual_art";
        var currentValue = ResolveAfterlifePlayerTargetValue(playerProfile, targetKind, targetId);
        var targetValue = Math.Max(0, GetNodeInt(offer["targetValue"]));
        var offeredSourceCap = Math.Max(0, GetNodeInt(offer["sourceCap"]));
        var mentorSourceCap = ResolveAfterlifeMentorSourceCap(mentor, targetKind, targetId);
        var sourceCap = mentorSourceCap > 0 && offeredSourceCap > 0
            ? Math.Min(mentorSourceCap, offeredSourceCap)
            : Math.Max(mentorSourceCap, offeredSourceCap);
        var relationshipLevel = ResolveAfterlifeMentorRelationshipLevel(mentor);
        var mentorMultiplierPercent = AfterlifeTrainingCostPolicy.ResolveMentorMultiplierPercent(relationshipLevel);
        var baseInkFeatherCost = ResolveAfterlifeMentorBaseInkFeatherCost(playerProfile, targetKind, targetId, targetValue);
        var baseLightSparkCost = ResolveAfterlifeMentorBaseLightSparkCost(playerProfile, targetKind, targetId, targetValue);

        var cost = offer["cost"] as JsonObject;
        var authoredLightSparkCost = Math.Max(0, GetNodeInt(cost?["lightSparks"]));
        var trainingCost = new TrainingCost(
            Money: 0,
            CurrentLevelExperiencePercent: 0,
            CurrentLevelExperiencePoints: 0,
            InkFeathers: baseInkFeatherCost > 0
                ? AfterlifeTrainingCostPolicy.ComputeMentorCost(baseInkFeatherCost, relationshipLevel)
                : Math.Max(0, GetNodeInt(cost?["inkFeathers"])),
            LightSparks: authoredLightSparkCost > 0 && baseLightSparkCost > 0
                ? AfterlifeTrainingCostPolicy.ComputeMentorCost(baseLightSparkCost, relationshipLevel)
                : authoredLightSparkCost);

        var requirements = offer["requirements"] as JsonObject;
        var maxUnlockedTier = ResolveMaxUnlockedSpiritualArtTier(playerProfile, shiningRoot);
        var offerMaxUnlockedTier = Math.Max(0, GetNodeInt(requirements?["maxPlayerUnlockedTier"]));
        if (offerMaxUnlockedTier > 0)
            maxUnlockedTier = Math.Min(maxUnlockedTier, offerMaxUnlockedTier);

        string? blockReason = null;
        if (string.IsNullOrWhiteSpace(targetId))
            blockReason = "В предложении нет цели обучения.";
        else if (targetValue <= currentValue)
            blockReason = "Предложение не повышает текущий уровень.";
        else if (sourceCap <= 0 || targetValue > sourceCap)
            blockReason = "Предложение превышает уровень, которым владеет наставник.";
        else if (trainingCost.InkFeathers <= 0 && trainingCost.LightSparks <= 0)
            blockReason = "У обучения должна быть положительная цена.";
        else if (targetValue > maxUnlockedTier)
            blockReason = $"нужно открыть уровень искусства {targetValue}";
        else
        {
            var minimumRelationship = Math.Max(0, GetNodeInt(requirements?["minimumRelationship"]));
            if (minimumRelationship > relationshipLevel)
                blockReason = $"нужно отношение не ниже {minimumRelationship}, сейчас {relationshipLevel}";
            else if (IsAfterlifeSpecialArtTarget(targetKind) && !IsPlayerSpecialArtKnown(playerProfile, targetId))
                blockReason = "новое особое духовное искусство нельзя открыть через витрину прокачки; сначала нужно ролевое обучение";
        }

        var details = CloneObject(offer);
        var sourceActorId = ResolveAfterlifeActorId(mentor);
        details["sourceActorId"] = sourceActorId;
        details["sourceActorName"] = ResolveAfterlifeActorName(mentor, sourceActorId);
        details["sourceActorKind"] = "afterlife_mentor";
        details["relationshipLevel"] = relationshipLevel;
        details["mentorPriceMultiplierPercent"] = mentorMultiplierPercent;
        if (baseInkFeatherCost > 0)
            details["baseInkFeatherCost"] = baseInkFeatherCost;
        if (baseLightSparkCost > 0)
            details["baseLightSparkCost"] = baseLightSparkCost;

        return new TrainingOffer(
            GetNodeString(offer["offerId"]) ?? "",
            targetId,
            targetName,
            targetKind,
            currentValue,
            targetValue,
            sourceCap,
            blockReason == null,
            blockReason,
            trainingCost,
            details);
    }

    private IEnumerable<TrainingOffer> BuildAfterlifeSelfTrainingOffers(
        JsonObject profile,
        JsonObject soulRoot,
        JsonObject? shiningRoot)
    {
        var maxUnlockedTier = ResolveMaxUnlockedSpiritualArtTier(profile, shiningRoot);
        var artTiers = profile["artTiers"] as JsonObject ?? new JsonObject();

        foreach (var art in AfterlifeSpiritualConflictState.SpiritualArts)
        {
            var currentTier = Math.Clamp(AfterlifeSpiritualConflictState.GetNodeInt(artTiers[art.ArtId]), 0, SpiritualArtMaxTier);
            var nextTier = Math.Min(SpiritualArtMaxTier, currentTier + 1);
            var baseCost = AfterlifeTrainingCostPolicy.ComputeStandardArtBaseInkFeatherCost(art, nextTier);
            var selfCost = AfterlifeTrainingCostPolicy.ComputeSelfStandardArtInkFeatherCost(art, nextTier);
            string? blockReason = null;
            if (currentTier >= SpiritualArtMaxTier)
                blockReason = "уже достигнут максимальный уровень искусства";
            else if (maxUnlockedTier < art.MinUnlockTier)
                blockReason = $"нужно открыть уровень искусства {art.MinUnlockTier}";
            else if (nextTier > maxUnlockedTier)
                blockReason = $"нужно открыть уровень искусства {nextTier}";

            yield return new TrainingOffer(
                $"self_art_{art.ArtId}_tier_{nextTier}",
                art.ArtId,
                art.DisplayName,
                "spiritual_art_self_training",
                currentTier,
                nextTier,
                SpiritualArtMaxTier,
                blockReason == null,
                blockReason,
                new TrainingCost(0, 0, 0, selfCost, 0),
                new JsonObject
                {
                    ["sourceActorKind"] = "self_fallback",
                    ["mechanicalUse"] = art.MechanicalUse,
                    ["baseInkFeatherCost"] = baseCost,
                    ["fallbackMultiplierPercent"] = AfterlifeTrainingCostPolicy.SelfStandardArtMultiplierPercent
                });
        }

        var spiritFocusTier = Math.Clamp(
            AfterlifeSpiritualConflictState.GetNodeInt(profile[AfterlifeSpiritualConflictState.SpiritFocusTierProperty]),
            0,
            AfterlifeSpiritualConflictState.SpiritFocusMaxTier);
        var focusNextTier = Math.Min(AfterlifeSpiritualConflictState.SpiritFocusMaxTier, spiritFocusTier + 1);
        var focusBlock = spiritFocusTier >= AfterlifeSpiritualConflictState.SpiritFocusMaxTier
            ? "уже достигнут максимальный уровень Средоточия Души"
            : focusNextTier > maxUnlockedTier
                ? $"нужно открыть уровень искусства {focusNextTier}"
                : null;
        var focusBaseCost = AfterlifeTrainingCostPolicy.ComputeSpiritFocusBaseInkFeatherCost(focusNextTier);
        var focusSelfCost = AfterlifeTrainingCostPolicy.ComputeSelfSpiritFocusInkFeatherCost(focusNextTier);
        yield return new TrainingOffer(
            $"self_spirit_focus_tier_{focusNextTier}",
            "spirit_focus",
            "Средоточие Души",
            "spirit_focus_self_training",
            spiritFocusTier,
            focusNextTier,
            AfterlifeSpiritualConflictState.SpiritFocusMaxTier,
            focusBlock == null,
            focusBlock,
            new TrainingCost(0, 0, 0, focusSelfCost, 0),
            new JsonObject
            {
                ["sourceActorKind"] = "self_fallback",
                ["baseInkFeatherCost"] = focusBaseCost,
                ["fallbackMultiplierPercent"] = AfterlifeTrainingCostPolicy.SelfSpiritFocusMultiplierPercent
            });

        foreach (var specialArt in EnumerateSpecialArts(profile))
        {
            var artId = GetNodeString(specialArt["artId"]);
            if (string.IsNullOrWhiteSpace(artId))
                continue;

            var tier = Math.Clamp(GetNodeInt(specialArt["tier"]), 0, SpiritualArtMaxTier);
            var displayName = GetNodeString(specialArt["displayName"]) ?? artId;
            var upgradeCost = specialArt["upgradeCost"] as JsonObject;
            var baseInkCost = Math.Max(0, GetNodeInt(upgradeCost?["inkFeathers"]));
            var selfInkCost = AfterlifeTrainingCostPolicy.ComputeSelfSpecialArtInkFeatherCost(baseInkCost);
            var nextTier = Math.Min(SpiritualArtMaxTier, tier + 1);
            var isKnown = tier > 0 || GetNodeBool(specialArt["learned"]);
            var blockReason = isKnown
                ? tier >= SpiritualArtMaxTier ? "уже достигнут максимальный уровень особого искусства" : null
                : "новое особое духовное искусство нельзя открыть самостоятельно";

            yield return new TrainingOffer(
                $"self_special_art_{artId}_tier_{nextTier}",
                artId,
                displayName,
                "special_spiritual_art_self_training",
                tier,
                nextTier,
                SpiritualArtMaxTier,
                blockReason == null,
                blockReason,
                new TrainingCost(0, 0, 0, selfInkCost, 0),
                BuildSelfSpecialArtDetails(specialArt, baseInkCost));
        }
    }

    private JsonObject? TryReadFreshShowcaseOffer(JsonObject teacher, string offerId, out string? blockReason)
    {
        blockReason = null;
        if (teacher["trainingShowcase"] is not JsonObject showcase)
        {
            blockReason = "ГМ ещё не подготовил витрину обучения.";
            return null;
        }

        var expectedHash = ComputeSourceSnapshotHash(teacher);
        var actualHash = GetNodeString(showcase["sourceActorSnapshotHash"]);
        if (!string.Equals(actualHash, expectedHash, StringComparison.OrdinalIgnoreCase))
        {
            blockReason = "Витрина обучения устарела после изменения учителя. Сначала обновите витрину.";
            return null;
        }

        return ReadOfferObjects(showcase)
            .FirstOrDefault(offer => string.Equals(GetNodeString(offer["offerId"]), offerId, StringComparison.OrdinalIgnoreCase));
    }

    private JsonObject? TryReadFreshMentorShowcaseOffer(JsonObject mentor, string offerId, out string? blockReason)
    {
        blockReason = null;
        if (mentor["mentorTrainingShowcase"] is not JsonObject showcase)
        {
            blockReason = "ГМ ещё не подготовил витрину наставника.";
            return null;
        }

        var expectedHash = ComputeSourceSnapshotHash(mentor);
        var actualHash = GetNodeString(showcase["sourceActorSnapshotHash"]);
        if (!string.Equals(actualHash, expectedHash, StringComparison.OrdinalIgnoreCase))
        {
            blockReason = "Витрина наставника устарела после изменения профиля. Сначала обновите витрину.";
            return null;
        }

        return ReadOfferObjects(showcase)
            .FirstOrDefault(offer => string.Equals(GetNodeString(offer["offerId"]), offerId, StringComparison.OrdinalIgnoreCase));
    }

    private void ApplyMortalSkillTraining(
        JsonObject activeRoot,
        JsonObject passiveRoot,
        JsonObject masteryRoot,
        TrainingOffer offer)
    {
        var isPassive = offer.TargetKind.Contains("passive", StringComparison.OrdinalIgnoreCase);
        var targetArrayName = isPassive ? "passiveSkillChanges" : "activeSkillChanges";
        var targetRoot = isPassive ? passiveRoot : activeRoot;
        var targetArray = EnsureArray(targetRoot, targetArrayName);
        var existingSkill = targetArray.OfType<JsonObject>().FirstOrDefault(skill =>
            string.Equals(GetNodeString(skill["skillName"]), offer.TargetName, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(GetNodeString(skill["skillId"]), offer.TargetId, StringComparison.OrdinalIgnoreCase));

        if (existingSkill == null)
        {
            existingSkill = new JsonObject
            {
                ["skillId"] = offer.TargetId,
                ["skillName"] = offer.TargetName,
                ["skillDescription"] = GetNodeString(offer.Details["summary"]) ?? "Навык получен через обучение.",
                ["rarity"] = "Common",
                ["currentMasteryLevel"] = offer.TargetValue,
                ["maxMasteryLevel"] = Math.Max(offer.SourceCap, offer.TargetValue)
            };
            if (isPassive)
                existingSkill["masteryLevel"] = offer.TargetValue;
            else
                existingSkill["category"] = "Utility";
            targetArray.Add(existingSkill);
        }
        else
        {
            existingSkill["currentMasteryLevel"] = offer.TargetValue;
            existingSkill["maxMasteryLevel"] = Math.Max(offer.SourceCap, GetNodeInt(existingSkill["maxMasteryLevel"]));
            if (isPassive)
                existingSkill["masteryLevel"] = offer.TargetValue;
        }

        var masteryArray = EnsureArray(masteryRoot, "skillMasteryChanges");
        var existingMastery = masteryArray.OfType<JsonObject>().FirstOrDefault(skill =>
            string.Equals(GetNodeString(skill["skillName"]), offer.TargetName, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(GetNodeString(skill["skillId"]), offer.TargetId, StringComparison.OrdinalIgnoreCase));
        if (existingMastery == null)
        {
            existingMastery = new JsonObject
            {
                ["skillId"] = offer.TargetId,
                ["skillName"] = offer.TargetName
            };
            masteryArray.Add(existingMastery);
        }

        existingMastery["newMasteryLevel"] = offer.TargetValue;
        existingMastery["newCurrentMasteryProgress"] = 0;
        existingMastery["newMasteryProgressNeeded"] = ComputeMasteryProgressNeeded(offer.TargetValue);
        existingMastery["masteryLeveledUp"] = true;
    }

    private static void AppendMortalTrainingReceipt(JsonObject npcRoot, JsonObject teacher, TrainingOffer offer, int currentTurn)
    {
        var receipts = EnsureArray(npcRoot, "trainingPurchaseReceipts");
        receipts.Add(new JsonObject
        {
            ["receiptId"] = $"training_receipt_{Guid.NewGuid():N}",
            ["realm"] = RealmMortal,
            ["sourceActorId"] = ResolveMortalTeacherActorId(teacher),
            ["sourceActorName"] = GetNodeString(teacher["name"]) ?? "",
            ["offerId"] = offer.OfferId,
            ["targetId"] = offer.TargetId,
            ["targetName"] = offer.TargetName,
            ["targetKind"] = offer.TargetKind,
            ["targetValue"] = offer.TargetValue,
            ["sourceCap"] = offer.SourceCap,
            ["sourceActorSnapshotHash"] = ComputeSourceSnapshotHash(teacher),
            ["moneySpent"] = offer.Cost.Money,
            ["currentLevelExperiencePercent"] = offer.Cost.CurrentLevelExperiencePercent,
            ["currentLevelExperienceSpent"] = offer.Cost.CurrentLevelExperiencePoints,
            ["createdAtTurn"] = currentTurn,
            ["createdAtUtc"] = DateTime.UtcNow.ToString("O", CultureInfo.InvariantCulture)
        });
    }

    private static void ApplyAfterlifeTraining(JsonObject profile, TrainingOffer offer)
    {
        if (IsAfterlifeStandardArtTarget(offer.TargetKind))
        {
            var artTiers = profile["artTiers"] as JsonObject ?? new JsonObject();
            artTiers[offer.TargetId] = offer.TargetValue;
            profile["artTiers"] = artTiers;
            return;
        }

        if (IsAfterlifeSpiritFocusTarget(offer.TargetKind))
        {
            profile[AfterlifeSpiritualConflictState.SpiritFocusTierProperty] = offer.TargetValue;
            return;
        }

        if (IsAfterlifeSpecialArtTarget(offer.TargetKind))
        {
            foreach (var specialArt in EnumerateSpecialArts(profile))
            {
                if (string.Equals(GetNodeString(specialArt["artId"]), offer.TargetId, StringComparison.OrdinalIgnoreCase))
                {
                    specialArt["tier"] = offer.TargetValue;
                    specialArt["learned"] = true;
                    return;
                }
            }
        }
    }

    private static void AppendAfterlifeTrainingReceipt(
        JsonObject soulRoot,
        TrainingOffer offer,
        int currentTurn,
        string sourceActorId = "self",
        string sourceActorName = "",
        string sourceActorKind = "self_fallback",
        string? sourceActorSnapshotHash = null)
    {
        var receipts = EnsureArray(soulRoot, TrainingRequestState.AfterlifePurchaseReceiptsProperty);
        var receipt = new JsonObject
        {
            ["receiptId"] = $"afterlife_training_receipt_{Guid.NewGuid():N}",
            ["realm"] = RealmAfterlife,
            ["sourceActorId"] = sourceActorId,
            ["sourceActorKind"] = sourceActorKind,
            ["offerId"] = offer.OfferId,
            ["targetId"] = offer.TargetId,
            ["targetName"] = offer.TargetName,
            ["targetKind"] = offer.TargetKind,
            ["targetValue"] = offer.TargetValue,
            ["inkFeathersSpent"] = offer.Cost.InkFeathers,
            ["lightSparksSpent"] = offer.Cost.LightSparks,
            ["createdAtTurn"] = currentTurn,
            ["createdAtUtc"] = DateTime.UtcNow.ToString("O", CultureInfo.InvariantCulture)
        };
        if (!string.IsNullOrWhiteSpace(sourceActorName))
            receipt["sourceActorName"] = sourceActorName;
        if (!string.IsNullOrWhiteSpace(sourceActorSnapshotHash))
            receipt["sourceActorSnapshotHash"] = sourceActorSnapshotHash;

        receipts.Add(receipt);
    }

    private static JsonObject NormalizeInkFeathers(JsonObject soulRoot)
    {
        if (soulRoot["inkFeathers"] is JsonObject objectRoot)
        {
            if (!objectRoot.ContainsKey("current"))
                objectRoot["current"] = 0;
            return objectRoot;
        }

        var current = GetNodeInt(soulRoot["inkFeathers"]);
        return new JsonObject
        {
            ["current"] = Math.Max(0, current),
            ["total"] = Math.Max(0, current)
        };
    }

    private static JsonObject EnsureAfterlifeCombatProfile(JsonObject soulRoot, JsonObject? shiningRoot)
    {
        var profile = soulRoot[AfterlifeSpiritualConflictState.SoulStateProfileProperty] as JsonObject;
        if (profile == null)
        {
            profile = AfterlifeSpiritualConflictState.CreateDefaultCombatProfile();
            profile["enlightenmentRank"] = ResolveEnlightenmentRank(soulRoot);
            profile["radianceRank"] = ResolveRadianceRank(shiningRoot);
        }

        if (profile["artTiers"] is not JsonObject)
            profile["artTiers"] = new JsonObject();
        if (!profile.ContainsKey(AfterlifeSpiritualConflictState.SpiritFocusTierProperty))
            profile[AfterlifeSpiritualConflictState.SpiritFocusTierProperty] = 0;

        return profile;
    }

    private static IEnumerable<JsonObject> EnumerateSpecialArts(JsonObject profile)
    {
        if (profile["specialArts"] is JsonArray direct)
        {
            foreach (var item in direct.OfType<JsonObject>())
                yield return item;
        }

        if (profile["specialSpiritualArts"] is JsonArray legacy)
        {
            foreach (var item in legacy.OfType<JsonObject>())
                yield return item;
        }
    }

    private static IEnumerable<JsonObject> EnumerateNpcObjects(JsonObject? root)
    {
        if (root == null)
            yield break;

        foreach (var sectionName in new[] { "UpdateNPCs", "NPCsInScene", "NPCs", "npcs", "npcDataChanges" })
        {
            if (root[sectionName] is not JsonArray array)
                continue;

            foreach (var item in array.OfType<JsonObject>())
                yield return item;
        }
    }

    private static IEnumerable<JsonObject> EnumerateAfterlifeProfiles(JsonObject? root)
    {
        if (root == null)
            yield break;

        foreach (var sectionName in new[] { "profiles", "afterlifeEntityProfileUpdates", "entities", "actors" })
        {
            if (root[sectionName] is not JsonArray array)
                continue;

            foreach (var item in array.OfType<JsonObject>())
                yield return item;
        }
    }

    private static bool IsMortalTeacher(JsonObject npc)
    {
        if (npc["teacherProfile"] is not JsonObject profile)
            return false;

        if (profile.ContainsKey("canTeach"))
            return GetNodeBool(profile["canTeach"]);

        return profile["skills"] is JsonArray skills && skills.Count > 0;
    }

    private static string ResolveMortalTeacherActorId(JsonObject teacher) =>
        GetNodeString(teacher["npcId"]) ??
        GetNodeString(teacher["NPCId"]) ??
        GetNodeString(teacher["initialId"]) ??
        GetNodeString(teacher["initialNPCId"]) ??
        GetNodeString(teacher["id"]) ??
        "";

    private static bool IsAfterlifeMentor(JsonObject profile)
    {
        if (profile["mentorTrainingShowcase"] is JsonObject)
            return true;

        if (profile["mentorProfile"] is JsonObject mentorProfile && GetNodeBool(mentorProfile["canTeach"]))
            return true;

        if (EnumerateSpecialArts(profile).Any(specialArt => GetNodeBool(specialArt["canTeachPlayer"])))
            return true;

        return GetNodeBool(profile["canTeachPlayer"]);
    }

    private static string ResolveAfterlifeActorId(JsonObject profile) =>
        GetNodeString(profile["actorId"]) ??
        GetNodeString(profile["actorRef"]) ??
        GetNodeString(profile["id"]) ??
        "";

    private static string ResolveAfterlifeActorName(JsonObject profile, string actorId) =>
        GetNodeString(profile["displayName"]) ??
        GetNodeString(profile["name"]) ??
        GetNodeString(profile["title"]) ??
        actorId;

    private static int ResolveAfterlifePlayerTargetValue(JsonObject playerProfile, string targetKind, string targetId)
    {
        if (IsAfterlifeStandardArtTarget(targetKind))
        {
            var artTiers = playerProfile["artTiers"] as JsonObject;
            return Math.Clamp(GetNodeInt(artTiers?[targetId]), 0, SpiritualArtMaxTier);
        }

        if (IsAfterlifeSpiritFocusTarget(targetKind))
        {
            return Math.Clamp(
                GetNodeInt(playerProfile[AfterlifeSpiritualConflictState.SpiritFocusTierProperty]),
                0,
                AfterlifeSpiritualConflictState.SpiritFocusMaxTier);
        }

        if (IsAfterlifeSpecialArtTarget(targetKind))
        {
            foreach (var specialArt in EnumerateSpecialArts(playerProfile))
            {
                if (string.Equals(GetNodeString(specialArt["artId"]), targetId, StringComparison.OrdinalIgnoreCase))
                    return Math.Clamp(GetNodeInt(specialArt["tier"]), 0, SpiritualArtMaxTier);
            }
        }

        return 0;
    }

    private static int ResolveAfterlifeMentorSourceCap(JsonObject mentor, string targetKind, string targetId)
    {
        if (IsAfterlifeStandardArtTarget(targetKind))
            return Math.Clamp(GetNodeInt((mentor["standardArts"] as JsonObject)?[targetId]), 0, SpiritualArtMaxTier);

        if (IsAfterlifeSpiritFocusTarget(targetKind))
        {
            var profile = mentor["mentorProfile"] as JsonObject;
            return Math.Clamp(
                Math.Max(
                    GetNodeInt(mentor[AfterlifeSpiritualConflictState.SpiritFocusTierProperty]),
                    Math.Max(
                        GetNodeInt(profile?[AfterlifeSpiritualConflictState.SpiritFocusTierProperty]),
                        GetNodeInt((mentor["progression"] as JsonObject)?[AfterlifeSpiritualConflictState.SpiritFocusTierProperty]))),
                0,
                AfterlifeSpiritualConflictState.SpiritFocusMaxTier);
        }

        if (IsAfterlifeSpecialArtTarget(targetKind))
        {
            foreach (var specialArt in EnumerateSpecialArts(mentor))
            {
                if (string.Equals(GetNodeString(specialArt["artId"]), targetId, StringComparison.OrdinalIgnoreCase))
                    return Math.Clamp(GetNodeInt(specialArt["tier"]), 0, SpiritualArtMaxTier);
            }
        }

        return 0;
    }

    private static int ResolveAfterlifeMentorRelationshipLevel(JsonObject mentor)
    {
        var mentorProfile = mentor["mentorProfile"] as JsonObject;
        var level = Math.Max(GetNodeInt(mentor["relationshipLevel"]), GetNodeInt(mentorProfile?["relationshipLevel"]));
        if (mentor["relationships"] is JsonArray relationships)
        {
            foreach (var relationship in relationships.OfType<JsonObject>())
                level = Math.Max(level, GetNodeInt(relationship["value"]));
        }

        return level;
    }

    private static int ResolveAfterlifeMentorBaseInkFeatherCost(
        JsonObject playerProfile,
        string targetKind,
        string targetId,
        int targetValue)
    {
        if (IsAfterlifeStandardArtTarget(targetKind))
        {
            var art = AfterlifeSpiritualConflictState.SpiritualArts.FirstOrDefault(candidate =>
                string.Equals(candidate.ArtId, targetId, StringComparison.OrdinalIgnoreCase));
            return art == null
                ? 0
                : AfterlifeTrainingCostPolicy.ComputeStandardArtBaseInkFeatherCost(art, targetValue);
        }

        if (IsAfterlifeSpiritFocusTarget(targetKind))
            return AfterlifeTrainingCostPolicy.ComputeSpiritFocusBaseInkFeatherCost(targetValue);

        if (IsAfterlifeSpecialArtTarget(targetKind))
        {
            var specialArt = FindSpecialArt(playerProfile, targetId);
            return Math.Max(0, GetNodeInt((specialArt?["upgradeCost"] as JsonObject)?["inkFeathers"]));
        }

        return 0;
    }

    private static int ResolveAfterlifeMentorBaseLightSparkCost(
        JsonObject playerProfile,
        string targetKind,
        string targetId,
        int targetValue)
    {
        if (IsAfterlifeStandardArtTarget(targetKind))
        {
            var art = AfterlifeSpiritualConflictState.SpiritualArts.FirstOrDefault(candidate =>
                string.Equals(candidate.ArtId, targetId, StringComparison.OrdinalIgnoreCase));
            return art == null
                ? 0
                : AfterlifeTrainingCostPolicy.ComputeStandardArtBaseLightSparkCost(art, targetValue);
        }

        if (IsAfterlifeSpiritFocusTarget(targetKind))
            return AfterlifeTrainingCostPolicy.ComputeSpiritFocusBaseLightSparkCost(targetValue);

        if (IsAfterlifeSpecialArtTarget(targetKind))
        {
            var specialArt = FindSpecialArt(playerProfile, targetId);
            return Math.Max(0, GetNodeInt((specialArt?["upgradeCost"] as JsonObject)?["lightSparks"]));
        }

        return 0;
    }

    private static bool IsPlayerSpecialArtKnown(JsonObject playerProfile, string artId)
    {
        var specialArt = FindSpecialArt(playerProfile, artId);
        return specialArt != null && (GetNodeBool(specialArt["learned"]) || GetNodeInt(specialArt["tier"]) > 0);
    }

    private static JsonObject? FindSpecialArt(JsonObject profile, string artId) =>
        EnumerateSpecialArts(profile)
            .FirstOrDefault(specialArt => string.Equals(GetNodeString(specialArt["artId"]), artId, StringComparison.OrdinalIgnoreCase));


    private static bool IsAfterlifeStandardArtTarget(string targetKind) =>
        string.Equals(targetKind, "standard_spiritual_art", StringComparison.OrdinalIgnoreCase) ||
        string.Equals(targetKind, "spiritual_art", StringComparison.OrdinalIgnoreCase) ||
        string.Equals(targetKind, "spiritual_art_training", StringComparison.OrdinalIgnoreCase) ||
        string.Equals(targetKind, "spiritual_art_self_training", StringComparison.OrdinalIgnoreCase);

    private static bool IsAfterlifeSpiritFocusTarget(string targetKind) =>
        string.Equals(targetKind, "spirit_focus", StringComparison.OrdinalIgnoreCase) ||
        string.Equals(targetKind, "spirit_focus_training", StringComparison.OrdinalIgnoreCase) ||
        string.Equals(targetKind, "spirit_focus_self_training", StringComparison.OrdinalIgnoreCase);

    private static bool IsAfterlifeSpecialArtTarget(string targetKind) =>
        string.Equals(targetKind, "special_spiritual_art", StringComparison.OrdinalIgnoreCase) ||
        string.Equals(targetKind, "special_spiritual_art_training", StringComparison.OrdinalIgnoreCase) ||
        string.Equals(targetKind, "special_spiritual_art_self_training", StringComparison.OrdinalIgnoreCase);

    private static IEnumerable<JsonObject> ReadOfferObjects(JsonObject showcase)
    {
        if (showcase["offers"] is not JsonArray offers)
            yield break;

        foreach (var offer in offers.OfType<JsonObject>())
            yield return offer;
    }

    private async Task<string?> ReadCurrentRealmAsync()
    {
        var root = await ReadObjectAsync(SoulStatePath);
        return GetNodeString(root?["currentRealm"]);
    }

    private static string NormalizeRealm(string? realm)
    {
        if (string.IsNullOrWhiteSpace(realm))
            return RealmMortal;
        if (realm.Contains("chaos", StringComparison.OrdinalIgnoreCase) ||
            realm.Contains("afterlife", StringComparison.OrdinalIgnoreCase) ||
            realm.Contains("shining", StringComparison.OrdinalIgnoreCase) ||
            realm.Contains("обитель", StringComparison.OrdinalIgnoreCase) ||
            realm.Contains("море", StringComparison.OrdinalIgnoreCase))
        {
            return RealmAfterlife;
        }

        return RealmMortal;
    }

    private async Task<JsonObject?> ReadObjectAsync(string relativePath)
    {
        var raw = await _fs.ReadFileAsync(relativePath);
        if (string.IsNullOrWhiteSpace(raw))
            return null;

        try
        {
            return JsonNode.Parse(raw) as JsonObject;
        }
        catch (JsonException ex)
        {
            _logger.LogWarning(ex, "Не удалось разобрать JSON {Path}", relativePath);
            return null;
        }
    }

    private int InferExperienceForNextLevelFromFile()
    {
        var raw = _fs.ReadFileAsync(PlayerExperiencePath).GetAwaiter().GetResult();
        if (string.IsNullOrWhiteSpace(raw))
            return 100;

        try
        {
            return InferExperienceForNextLevel(JsonNode.Parse(raw) as JsonObject);
        }
        catch (JsonException)
        {
            return 100;
        }
    }

    private static int InferExperienceForNextLevel(JsonObject? experienceRoot)
    {
        if (experienceRoot == null)
            return 100;

        var value = GetNodeInt(experienceRoot["experienceForNextLevel"]);
        if (value > 0)
            return value;

        value = GetNodeInt(experienceRoot["nextLevelExperience"]);
        return value > 0 ? value : 100;
    }

    private static int ReadCurrentLevelExperience(JsonObject experienceRoot)
    {
        var current = GetNodeInt(experienceRoot["currentLevelExperience"], int.MinValue);
        if (current != int.MinValue)
            return Math.Max(0, current);

        return Math.Max(0, GetNodeInt(experienceRoot["totalExperience"]));
    }

    private static int ResolveMaxUnlockedSpiritualArtTier(JsonObject profile, JsonObject? shiningRoot)
    {
        var enlightenmentRank = AfterlifeSpiritualConflictState.GetNodeInt(profile["enlightenmentRank"]);
        var radianceRank = Math.Max(
            AfterlifeSpiritualConflictState.GetNodeInt(profile["radianceRank"]),
            ResolveRadianceRank(shiningRoot));
        var retainedRadianceRank = AfterlifeSpiritualConflictState.GetNodeInt(profile["retainedRadianceRank"]);

        return Math.Clamp(
            Math.Max(
                ResolveUnlockedTierFromRanks(AfterlifeSpiritualConflictState.EnlightenmentRanks, enlightenmentRank),
                Math.Max(
                    ResolveUnlockedTierFromRanks(AfterlifeSpiritualConflictState.RadianceRanks, radianceRank),
                    ResolveUnlockedTierFromRanks(AfterlifeSpiritualConflictState.RadianceRanks, retainedRadianceRank))),
            0,
            SpiritualArtMaxTier);
    }

    private static int ResolveUnlockedTierFromRanks(
        IReadOnlyList<AfterlifeSpiritualConflictState.RankDefinition> ranks,
        int rank) =>
        ranks
            .Where(definition => definition.Rank <= rank)
            .Select(definition => definition.UnlocksArtTier)
            .DefaultIfEmpty(0)
            .Max();

    private static int ResolveEnlightenmentRank(JsonObject soulRoot)
    {
        var directProgress = AfterlifeSpiritualConflictState.GetNodeInt(soulRoot["enlightenment"]);
        var enlightenment = soulRoot["enlightenment"] as JsonObject;
        var soulProgression = soulRoot["soulProgression"] as JsonObject;
        var progress = Math.Max(
            Math.Max(directProgress, AfterlifeSpiritualConflictState.GetNodeInt(enlightenment?["experience"])),
            Math.Max(
                AfterlifeSpiritualConflictState.GetNodeInt(soulProgression?["totalExperience"]),
                AfterlifeSpiritualConflictState.GetNodeInt(soulProgression?["progressPercent"])));
        var tier = Math.Max(
            AfterlifeSpiritualConflictState.GetNodeInt(enlightenment?["level"]),
            AfterlifeSpiritualConflictState.GetNodeInt(soulProgression?["tier"]));
        return Math.Clamp(
            Math.Max(tier, ResolveRankFromProgress(AfterlifeSpiritualConflictState.EnlightenmentRanks, progress)),
            0,
            AfterlifeSpiritualConflictState.EnlightenmentRanks.Max(rank => rank.Rank));
    }

    private static int ResolveRadianceRank(JsonObject? shiningRoot)
    {
        var radiance = shiningRoot?["radiance"] as JsonObject;
        var progress = AfterlifeSpiritualConflictState.GetNodeInt(radiance?["experience"]);
        var tier = AfterlifeSpiritualConflictState.GetNodeInt(radiance?["tier"]);
        return Math.Clamp(
            Math.Max(tier, ResolveRankFromProgress(AfterlifeSpiritualConflictState.RadianceRanks, progress)),
            0,
            AfterlifeSpiritualConflictState.RadianceRanks.Max(rank => rank.Rank));
    }

    private static int ResolveRankFromProgress(
        IReadOnlyList<AfterlifeSpiritualConflictState.RankDefinition> ranks,
        int progress) =>
        ranks
            .Where(rank => progress >= rank.RequiredProgress)
            .Select(rank => rank.Rank)
            .DefaultIfEmpty(0)
            .Max();

    private static int ComputeMasteryProgressNeeded(int masteryLevel) =>
        Math.Max(100, masteryLevel * 100);

    private static JsonObject BuildSelfSpecialArtDetails(JsonObject specialArt, int baseInkCost)
    {
        var details = CloneObject(specialArt);
        details["sourceActorKind"] = "self_fallback";
        details["baseInkFeatherCost"] = baseInkCost;
        details["fallbackMultiplierPercent"] = AfterlifeTrainingCostPolicy.SelfSpecialArtMultiplierPercent;
        return details;
    }

    private static string BuildMortalTrainingPendingGmAction(
        TrainingRequestState.PendingTrainingShowcaseRequest request) =>
        "Подготовь витрину обучения для NPC-учителя " +
        $"{request.SourceActorName} ({request.SourceActorId}). " +
        "Заполни trainingShowcase свежими offer-ами, ценой в деньгах и процентах опыта текущего уровня, " +
        "sourceCap не выше навыков учителя, sourceActorSnapshotHash должен совпасть с requested hash " +
        $"{request.SourceActorSnapshotHash}.";

    private static string BuildAfterlifeTrainingPendingGmAction(
        TrainingRequestState.PendingTrainingShowcaseRequest request) =>
        "Подготовь витрину обучения для наставника посмертия " +
        $"{request.SourceActorName} ({request.SourceActorId}). " +
        "Заполни mentorTrainingShowcase через afterlifeEntityProfileUpdates: предложения могут учить стандартным духовным искусствам, " +
        "Средоточию Души или прокачивать уже известные особые искусства; sourceCap не выше профиля наставника, " +
        "sourceActorSnapshotHash должен совпасть с requested hash " +
        $"{request.SourceActorSnapshotHash}. Клиент сам спишет валюту и поднимет уровень после покупки.";

    private static JsonArray EnsureArray(JsonObject root, string propertyName)
    {
        if (root[propertyName] is JsonArray existing)
            return existing;

        var created = new JsonArray();
        root[propertyName] = created;
        return created;
    }

    private static JsonObject CloneObject(JsonObject source) =>
        source.DeepClone() as JsonObject ?? new JsonObject();

    private static JsonObject BuildSourceSnapshotNode(JsonObject sourceActor)
    {
        var clone = CloneObject(sourceActor);
        clone.Remove("trainingShowcase");
        clone.Remove("mentorTrainingShowcase");
        clone.Remove("trainingPurchaseReceipts");
        clone.Remove(TrainingRequestState.AfterlifePurchaseReceiptsProperty);
        clone.Remove("tradeInventory");
        clone.Remove("tradeInventoryReceipt");
        clone.Remove("tradeInventoryReceipts");
        clone.Remove("buybackInventory");
        return clone;
    }

    private static string BuildCanonicalJson(JsonNode? node)
    {
        if (node == null)
            return "null";

        return node switch
        {
            JsonObject obj => "{" + string.Join(
                ",",
                obj.OrderBy(pair => pair.Key, StringComparer.Ordinal)
                    .Select(pair => JsonSerializer.Serialize(pair.Key) + ":" + BuildCanonicalJson(pair.Value))) + "}",
            JsonArray arr => "[" + string.Join(",", arr.Select(BuildCanonicalJson)) + "]",
            JsonValue value => value.ToJsonString(),
            _ => node.ToJsonString()
        };
    }

    private static string? GetNodeString(JsonNode? node)
    {
        if (node is JsonValue value && value.TryGetValue<string>(out var text))
            return string.IsNullOrWhiteSpace(text) ? null : text.Trim();
        return null;
    }

    private static int GetNodeInt(JsonNode? node, int defaultValue = 0)
    {
        if (node is JsonValue value)
        {
            if (value.TryGetValue<int>(out var intValue))
                return intValue;
            if (value.TryGetValue<long>(out var longValue))
                return (int)Math.Clamp(longValue, int.MinValue, int.MaxValue);
            if (value.TryGetValue<double>(out var doubleValue))
                return (int)Math.Round(doubleValue, MidpointRounding.AwayFromZero);
            if (value.TryGetValue<string>(out var text) &&
                int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed))
            {
                return parsed;
            }
        }

        return defaultValue;
    }

    private static bool GetNodeBool(JsonNode? node)
    {
        if (node is JsonValue value)
        {
            if (value.TryGetValue<bool>(out var boolValue))
                return boolValue;
            if (value.TryGetValue<string>(out var text) &&
                bool.TryParse(text, out var parsed))
            {
                return parsed;
            }
        }

        return false;
    }
}
