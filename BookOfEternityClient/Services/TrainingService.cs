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
    private const string MortalSkillEvolutionRequestKind = "mortal_training_skill_evolution";
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

    public sealed record TrainingOperationResult(bool Success, bool StateChanged, string Message, string? PendingGmAction = null);

    internal static bool NormalizeAfterlifeMentorShowcaseCosts(JsonObject profileRoot)
    {
        if (profileRoot[AfterlifeEntityProfileState.ProfilesProperty] is not JsonArray profiles)
            return false;

        var changed = false;
        foreach (var profile in profiles.OfType<JsonObject>())
            changed |= NormalizeAfterlifeMentorShowcaseCostsForProfile(profile);

        return changed;
    }

    private sealed record MortalTrainingApplicationPlan(
        bool RequiresGmEvolution,
        string Reason,
        string DedupeKey,
        bool IsPassive,
        JsonObject? ExistingSkill,
        JsonObject? ExistingMastery,
        int CurrentMasteryLevel,
        int CurrentMasteryProgress,
        int MasteryProgressNeeded,
        int MasteryProgressGain);

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

        RefreshMortalShowcaseHashesForPendingRequests(
            npcRoot,
            await TrainingRequestState.ReadRequestsAsync(_fs));

        var teacher = DeduplicateMortalTeachers(EnumerateNpcObjects(npcRoot).Where(IsMortalTeacher))
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
        var applicationPlan = BuildMortalTrainingApplicationPlan(evaluatedOffer, activeRoot, passiveRoot, masteryRoot);
        var sourceActorIdForPlan = ResolveMortalTeacherActorId(teacher);

        if (applicationPlan.RequiresGmEvolution)
        {
            var existingPending = await TrainingRequestState.FindPendingRequestAsync(
                _fs,
                sourceActorIdForPlan,
                MortalSkillEvolutionRequestKind,
                applicationPlan.DedupeKey);
            if (existingPending != null)
            {
                return new TrainingOperationResult(
                    false,
                    false,
                    "Это обучение уже оплачено и ожидает ГМ: мастер должен завершить изменение навыка перед повторной покупкой.");
            }
        }

        statusRoot["money"] = currentMoney - evaluatedOffer.Cost.Money;
        experienceRoot["currentLevelExperience"] = currentLevelExperience - evaluatedOffer.Cost.CurrentLevelExperiencePoints;
        if (!experienceRoot.ContainsKey("experienceForNextLevel"))
            experienceRoot["experienceForNextLevel"] = InferExperienceForNextLevel(experienceRoot);

        if (applicationPlan.RequiresGmEvolution)
        {
            var request = await WriteMortalSkillEvolutionRequestAsync(
                teacher,
                evaluatedOffer,
                applicationPlan,
                currentTurn);
            AppendMortalTrainingReceipt(
                npcRoot,
                teacher,
                evaluatedOffer,
                currentTurn,
                resolutionState: "pending_gm_skill_evolution",
                pendingRequestId: request.RequestId,
                pendingRequestKind: MortalSkillEvolutionRequestKind,
                pendingReason: applicationPlan.Reason);

            await _fs.WriteFileAtomicAsync(PlayerStatusPath, statusRoot.ToJsonString(JsonOpts));
            await _fs.WriteFileAtomicAsync(PlayerExperiencePath, experienceRoot.ToJsonString(JsonOpts));
            await _fs.WriteFileAtomicAsync(NpcCorePath, npcRoot.ToJsonString(JsonOpts));

            return new TrainingOperationResult(
                true,
                true,
                "Занятие оплачено и ожидает ГМ: мастер завершит изменение навыка и обновит его эффекты.",
                BuildMortalSkillEvolutionPendingGmAction(request));
        }

        ApplyMortalActiveSkillPractice(masteryRoot, evaluatedOffer, applicationPlan);
        AppendMortalTrainingReceipt(
            npcRoot,
            teacher,
            evaluatedOffer,
            currentTurn,
            resolutionState: "completed_local_practice");

        await _fs.WriteFileAtomicAsync(PlayerStatusPath, statusRoot.ToJsonString(JsonOpts));
        await _fs.WriteFileAtomicAsync(PlayerExperiencePath, experienceRoot.ToJsonString(JsonOpts));
        await _fs.WriteFileAtomicAsync(SkillMasteryPath, masteryRoot.ToJsonString(JsonOpts));
        await _fs.WriteFileAtomicAsync(NpcCorePath, npcRoot.ToJsonString(JsonOpts));

        return new TrainingOperationResult(true, true, "Практика завершена: мастерство навыка выросло без изменения эффектов.");
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
        var npcRootChanged = RefreshMortalShowcaseHashesForPendingRequests(
            npcRoot,
            await TrainingRequestState.ReadRequestsAsync(_fs));
        var teachers = new List<TrainingTeacherView>();
        var satisfiedRequests = new List<(string SourceActorId, string RequestKind)>();
        var requestPending = false;
        var requestCreated = false;
        string? pendingGmAction = null;

        foreach (var teacher in DeduplicateMortalTeachers(EnumerateNpcObjects(npcRoot).Where(IsMortalTeacher)))
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
            satisfiedRequests.Add((sourceActorId, MortalRequestKind));

            teachers.Add(new TrainingTeacherView(
                sourceActorId,
                sourceActorName,
                "npc_teacher",
                ShowcaseReady: true,
                ShowcaseStale: false,
                BlockReason: null,
                Offers: offers));
        }

        await CleanupSatisfiedMortalSkillEvolutionRequestsAsync();
        await ClearSatisfiedTrainingShowcaseRequestsAsync(satisfiedRequests);
        var remainingRequests = await TrainingRequestState.ReadRequestsAsync(_fs);
        var pendingSkillEvolution = remainingRequests.FirstOrDefault(request =>
            string.Equals(request.RequestKind, MortalSkillEvolutionRequestKind, StringComparison.OrdinalIgnoreCase));
        if (pendingSkillEvolution != null)
        {
            requestPending = true;
            pendingGmAction ??= BuildMortalSkillEvolutionPendingGmAction(pendingSkillEvolution);
        }

        if (npcRootChanged && npcRoot != null)
            await _fs.WriteFileAtomicAsync(NpcCorePath, npcRoot.ToJsonString(JsonOpts));

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
        var satisfiedRequests = new List<(string SourceActorId, string RequestKind)>();
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
            satisfiedRequests.Add((sourceActorId, AfterlifeRequestKind));

            teachers.Add(new TrainingTeacherView(
                sourceActorId,
                sourceActorName,
                sourceActorKind,
                ShowcaseReady: true,
                ShowcaseStale: false,
                BlockReason: null,
                Offers: offers));
        }

        await ClearSatisfiedTrainingShowcaseRequestsAsync(satisfiedRequests);

        return new TrainingView(
            RealmAfterlife,
            teachers,
            selfOffers,
            requestPending,
            requestCreated,
            pendingGmAction);
    }

    private async Task ClearSatisfiedTrainingShowcaseRequestsAsync(
        IReadOnlyCollection<(string SourceActorId, string RequestKind)> satisfiedRequests)
    {
        if (satisfiedRequests.Count == 0)
            return;

        var existing = await TrainingRequestState.ReadRequestsAsync(_fs);
        if (existing.Count == 0)
            return;

        var remaining = existing
            .Where(request => !satisfiedRequests.Any(satisfied =>
                string.Equals(request.SourceActorId, satisfied.SourceActorId, StringComparison.OrdinalIgnoreCase) &&
                string.Equals(request.RequestKind, satisfied.RequestKind, StringComparison.OrdinalIgnoreCase)))
            .ToArray();

        if (remaining.Length != existing.Count)
            await TrainingRequestState.WriteRequestsAsync(_fs, remaining);
    }

    public async Task CleanupSatisfiedMortalSkillEvolutionRequestsAsync()
    {
        var existing = await TrainingRequestState.ReadRequestsAsync(_fs);
        if (existing.Count == 0)
            return;

        var activeRoot = await ReadObjectAsync(ActiveSkillsPath) ?? new JsonObject();
        var passiveRoot = await ReadObjectAsync(PassiveSkillsPath) ?? new JsonObject();
        var masteryRoot = await ReadObjectAsync(SkillMasteryPath) ?? new JsonObject();

        var remaining = existing
            .Where(request => !string.Equals(request.RequestKind, MortalSkillEvolutionRequestKind, StringComparison.OrdinalIgnoreCase) ||
                              !IsMortalSkillEvolutionRequestSatisfied(request, activeRoot, passiveRoot, masteryRoot))
            .ToArray();

        if (remaining.Length != existing.Count)
            await TrainingRequestState.WriteRequestsAsync(_fs, remaining);
    }

    private static bool IsMortalSkillEvolutionRequestSatisfied(
        TrainingRequestState.PendingTrainingShowcaseRequest request,
        JsonObject activeRoot,
        JsonObject passiveRoot,
        JsonObject masteryRoot)
    {
        var details = request.Details;
        if (details == null)
            return false;

        var targetId = GetNodeString(details["targetId"]);
        var targetName = GetNodeString(details["targetName"]);
        if (string.IsNullOrWhiteSpace(targetId) && string.IsNullOrWhiteSpace(targetName))
            return false;

        var targetValue = GetNodeInt(details["targetValue"]);
        if (targetValue <= 0)
            return false;

        var targetKind = GetNodeString(details["targetKind"]) ?? "";
        if (IsExplicitPassiveMortalTrainingTarget(targetKind))
        {
            return IsMortalSkillEvolutionSatisfiedBy(
                FindMortalSkillObject(passiveRoot, "passiveSkillChanges", targetId, targetName),
                masteryRoot,
                targetId,
                targetName,
                targetValue);
        }

        if (IsExplicitActiveMortalTrainingTarget(targetKind))
        {
            return IsMortalSkillEvolutionSatisfiedBy(
                FindMortalSkillObject(activeRoot, "activeSkillChanges", targetId, targetName),
                masteryRoot,
                targetId,
                targetName,
                targetValue);
        }

        return IsMortalSkillEvolutionSatisfiedBy(
                   FindMortalSkillObject(activeRoot, "activeSkillChanges", targetId, targetName),
                   masteryRoot,
                   targetId,
                   targetName,
                   targetValue) ||
               IsMortalSkillEvolutionSatisfiedBy(
                   FindMortalSkillObject(passiveRoot, "passiveSkillChanges", targetId, targetName),
                   masteryRoot,
                   targetId,
                   targetName,
                   targetValue);
    }

    private static bool IsMortalSkillEvolutionSatisfiedBy(
        JsonObject? skill,
        JsonObject masteryRoot,
        string? targetId,
        string? targetName,
        int targetValue)
    {
        if (skill == null)
            return false;

        var mastery = FindMortalSkillObject(masteryRoot, "skillMasteryChanges", targetId, targetName);
        var resolvedLevel = FirstPositive(
            GetNodeInt(mastery?["newMasteryLevel"]),
            GetNodeInt(mastery?["masteryLevel"]),
            GetNodeInt(skill["currentMasteryLevel"]),
            GetNodeInt(skill["masteryLevel"]),
            GetNodeInt(skill["level"]));

        return resolvedLevel >= targetValue;
    }

    private TrainingOffer EvaluateMortalOffer(JsonObject teacher, JsonObject offer)
    {
        var targetId = GetNodeString(offer["targetId"]) ?? "";
        var targetName = GetNodeString(offer["targetName"]) ?? GetNodeString(offer["skillName"]) ?? targetId;
        var targetKind = GetNodeString(offer["targetKind"]) ?? "";
        if (!IsExplicitActiveMortalTrainingTarget(targetKind) &&
            !IsExplicitPassiveMortalTrainingTarget(targetKind))
        {
            var teacherSkillKind = ResolveMortalTeacherSkillKind(teacher, targetId, targetName);
            targetKind = NormalizeMortalTrainingTargetKind(
                targetKind,
                IsExplicitPassiveMortalTrainingTarget(teacherSkillKind));
        }

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
        var masteryProgressGain = ResolveMortalMasteryProgressGain(offer);

        if (string.IsNullOrWhiteSpace(targetId))
            blockReason = "В предложении нет цели обучения.";
        else if (targetValue <= currentValue && masteryProgressGain <= 0)
            blockReason = "Предложение не повышает текущий уровень навыка.";
        else if (sourceCap <= 0 || Math.Max(targetValue, currentValue) > sourceCap)
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
        details["targetKind"] = targetKind;
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
        var targetKind = NormalizeAfterlifeTrainingTargetKind(
            GetNodeString(offer["targetKind"]) ?? GetNodeString(offer["targetType"]),
            targetId);
        var targetName = ResolveAfterlifeTrainingTargetName(offer, targetKind, targetId);
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
        var authoredInkFeatherCost = Math.Max(
            0,
            GetTrainingCostAmount(cost, "inkFeathers", "inkFeatherCost", "inkFeathersCost", "costInFeathers"));
        var authoredLightSparkCost = Math.Max(
            0,
            GetTrainingCostAmount(cost, "lightSparks", "lightSparkCost", "lightSparksCost", "costInLightSparks"));
        var trainingCost = new TrainingCost(
            Money: 0,
            CurrentLevelExperiencePercent: 0,
            CurrentLevelExperiencePoints: 0,
            InkFeathers: baseInkFeatherCost > 0
                ? AfterlifeTrainingCostPolicy.ComputeMentorCost(baseInkFeatherCost, relationshipLevel)
                : authoredInkFeatherCost,
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

    private MortalTrainingApplicationPlan BuildMortalTrainingApplicationPlan(
        TrainingOffer offer,
        JsonObject activeRoot,
        JsonObject passiveRoot,
        JsonObject masteryRoot)
    {
        var explicitPassive = IsExplicitPassiveMortalTrainingTarget(offer.TargetKind);
        var explicitActive = IsExplicitActiveMortalTrainingTarget(offer.TargetKind);
        var activeSkill = explicitPassive ? null : FindMortalSkillObject(activeRoot, "activeSkillChanges", offer);
        var passiveSkill = explicitActive ? null : FindMortalSkillObject(passiveRoot, "passiveSkillChanges", offer);
        var isPassive = explicitPassive || (!explicitActive && passiveSkill != null && activeSkill == null);
        var existingSkill = isPassive ? passiveSkill : activeSkill;
        var existingMastery = FindMortalSkillObject(masteryRoot, "skillMasteryChanges", offer);
        var currentMasteryLevel = Math.Max(
            1,
            FirstPositive(
                GetNodeInt(existingMastery?["newMasteryLevel"]),
                GetNodeInt(existingSkill?["currentMasteryLevel"]),
                GetNodeInt(existingSkill?["masteryLevel"]),
                offer.CurrentValue));
        var currentProgress = Math.Max(
            0,
            FirstPositiveOrZero(
                GetNodeInt(existingMastery?["newCurrentMasteryProgress"], int.MinValue),
                GetNodeInt(existingSkill?["currentMasteryProgress"], int.MinValue),
                GetNodeInt(existingSkill?["masteryProgress"], int.MinValue)));
        var progressNeeded = Math.Max(
            1,
            FirstPositive(
                GetNodeInt(existingMastery?["newMasteryProgressNeeded"]),
                GetNodeInt(existingSkill?["masteryProgressNeeded"]),
                GetNodeInt(existingSkill?["progressNeeded"]),
                ComputeMasteryProgressNeeded(currentMasteryLevel)));
        var progressGain = ResolveMortalMasteryProgressGain(offer.Details);
        var dedupeKey = $"{offer.OfferId}:{offer.TargetId}:{offer.TargetValue}";

        if (existingSkill == null)
        {
            return new MortalTrainingApplicationPlan(
                RequiresGmEvolution: true,
                Reason: "unknown_skill_unlock",
                DedupeKey: dedupeKey,
                IsPassive: isPassive,
                ExistingSkill: null,
                ExistingMastery: existingMastery,
                CurrentMasteryLevel: currentMasteryLevel,
                CurrentMasteryProgress: currentProgress,
                MasteryProgressNeeded: progressNeeded,
                MasteryProgressGain: progressGain);
        }

        if (!isPassive && IsMortalPracticeOffer(offer) && progressGain > 0)
        {
            var newProgress = currentProgress + progressGain;
            if (newProgress < progressNeeded)
            {
                return new MortalTrainingApplicationPlan(
                    RequiresGmEvolution: false,
                    Reason: "local_mastery_practice",
                    DedupeKey: dedupeKey,
                    IsPassive: false,
                    ExistingSkill: existingSkill,
                    ExistingMastery: existingMastery,
                    CurrentMasteryLevel: currentMasteryLevel,
                    CurrentMasteryProgress: currentProgress,
                    MasteryProgressNeeded: progressNeeded,
                    MasteryProgressGain: progressGain);
            }
        }

        return new MortalTrainingApplicationPlan(
            RequiresGmEvolution: true,
            Reason: "mastery_threshold_crossed",
            DedupeKey: dedupeKey,
            IsPassive: isPassive,
            ExistingSkill: existingSkill,
            ExistingMastery: existingMastery,
            CurrentMasteryLevel: currentMasteryLevel,
            CurrentMasteryProgress: currentProgress,
            MasteryProgressNeeded: progressNeeded,
            MasteryProgressGain: progressGain);
    }

    private async Task<TrainingRequestState.PendingTrainingShowcaseRequest> WriteMortalSkillEvolutionRequestAsync(
        JsonObject teacher,
        TrainingOffer offer,
        MortalTrainingApplicationPlan plan,
        int currentTurn)
    {
        var sourceActorId = ResolveMortalTeacherActorId(teacher);
        var sourceActorName = GetNodeString(teacher["name"]) ?? sourceActorId;
        var details = BuildMortalSkillEvolutionRequestDetails(teacher, offer, plan);
        return await TrainingRequestState.WriteRequestAsync(
            _fs,
            MortalSkillEvolutionRequestKind,
            sourceActorId,
            sourceActorName,
            "npc_teacher",
            RealmMortal,
            currentTurn,
            ComputeSourceSnapshotHash(teacher),
            plan.Reason,
            details);
    }

    private static JsonObject BuildMortalSkillEvolutionRequestDetails(
        JsonObject teacher,
        TrainingOffer offer,
        MortalTrainingApplicationPlan plan)
    {
        var sourceActorId = ResolveMortalTeacherActorId(teacher);
        var details = new JsonObject
        {
            ["dedupeKey"] = plan.DedupeKey,
            ["offerId"] = offer.OfferId,
            ["targetId"] = offer.TargetId,
            ["targetName"] = offer.TargetName,
            ["targetKind"] = NormalizeMortalTrainingTargetKind(offer.TargetKind, plan.IsPassive),
            ["currentValue"] = offer.CurrentValue,
            ["targetValue"] = offer.TargetValue,
            ["sourceCap"] = offer.SourceCap,
            ["sourceActorId"] = sourceActorId,
            ["sourceActorName"] = GetNodeString(teacher["name"]) ?? sourceActorId,
            ["sourceActorSnapshotHash"] = ComputeSourceSnapshotHash(teacher),
            ["moneySpent"] = offer.Cost.Money,
            ["currentLevelExperiencePercent"] = offer.Cost.CurrentLevelExperiencePercent,
            ["currentLevelExperienceSpent"] = offer.Cost.CurrentLevelExperiencePoints,
            ["masteryProgressGain"] = plan.MasteryProgressGain,
            ["gmInstruction"] = plan.IsPassive
                ? "Создай или обнови полный passiveSkillChanges объект и matching skillMasteryChanges/уровень, не теряя structuredBonuses."
                : "Создай или обнови полный activeSkillChanges объект с combatEffect и matching skillMasteryChanges для нового уровня."
        };

        var summary = GetNodeString(offer.Details["summary"]);
        if (!string.IsNullOrWhiteSpace(summary))
            details["summary"] = summary;

        var skillState = new JsonObject
        {
            ["currentMasteryLevel"] = plan.CurrentMasteryLevel,
            ["currentMasteryProgress"] = plan.CurrentMasteryProgress,
            ["masteryProgressNeeded"] = plan.MasteryProgressNeeded
        };
        if (plan.ExistingSkill != null)
            skillState["skill"] = CloneObject(plan.ExistingSkill);
        if (plan.ExistingMastery != null)
            skillState["mastery"] = CloneObject(plan.ExistingMastery);
        details["skillStateBefore"] = skillState;

        return details;
    }

    private static void ApplyMortalActiveSkillPractice(
        JsonObject masteryRoot,
        TrainingOffer offer,
        MortalTrainingApplicationPlan plan)
    {
        var masteryArray = EnsureArray(masteryRoot, "skillMasteryChanges");
        var existingMastery = masteryArray.OfType<JsonObject>().FirstOrDefault(skill => MatchesMortalTrainingTarget(skill, offer));
        if (existingMastery == null)
        {
            existingMastery = new JsonObject
            {
                ["skillId"] = offer.TargetId,
                ["skillName"] = offer.TargetName
            };
            masteryArray.Add(existingMastery);
        }

        existingMastery["newMasteryLevel"] = plan.CurrentMasteryLevel;
        existingMastery["newCurrentMasteryProgress"] = plan.CurrentMasteryProgress + plan.MasteryProgressGain;
        existingMastery["newMasteryProgressNeeded"] = plan.MasteryProgressNeeded;
        existingMastery["masteryLeveledUp"] = false;
    }

    private static void AppendMortalTrainingReceipt(
        JsonObject npcRoot,
        JsonObject teacher,
        TrainingOffer offer,
        int currentTurn,
        string resolutionState = "completed_locally",
        string? pendingRequestId = null,
        string? pendingRequestKind = null,
        string? pendingReason = null)
    {
        var receipts = EnsureArray(npcRoot, "trainingPurchaseReceipts");
        var receipt = new JsonObject
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
            ["createdAtUtc"] = DateTime.UtcNow.ToString("O", CultureInfo.InvariantCulture),
            ["resolutionState"] = resolutionState
        };

        if (!string.IsNullOrWhiteSpace(pendingRequestId))
            receipt["pendingRequestId"] = pendingRequestId;
        if (!string.IsNullOrWhiteSpace(pendingRequestKind))
            receipt["pendingRequestKind"] = pendingRequestKind;
        if (!string.IsNullOrWhiteSpace(pendingReason))
            receipt["pendingReason"] = pendingReason;

        receipts.Add(receipt);
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

    private static IEnumerable<JsonObject> DeduplicateMortalTeachers(IEnumerable<JsonObject> teachers)
    {
        var deduplicated = new List<JsonObject>();
        var indexByActorId = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

        foreach (var teacher in teachers)
        {
            var sourceActorId = ResolveMortalTeacherActorId(teacher);
            if (string.IsNullOrWhiteSpace(sourceActorId))
            {
                deduplicated.Add(teacher);
                continue;
            }

            if (!indexByActorId.TryGetValue(sourceActorId, out var existingIndex))
            {
                indexByActorId[sourceActorId] = deduplicated.Count;
                deduplicated.Add(teacher);
                continue;
            }

            if (GetMortalTeacherPriority(teacher) > GetMortalTeacherPriority(deduplicated[existingIndex]))
                deduplicated[existingIndex] = teacher;
        }

        return deduplicated;
    }

    private static int GetMortalTeacherPriority(JsonObject teacher)
    {
        var score = 0;
        if (!string.IsNullOrWhiteSpace(GetNodeString(teacher["npcId"])) ||
            !string.IsNullOrWhiteSpace(GetNodeString(teacher["NPCId"])))
        {
            score += 20;
        }

        if (teacher["trainingShowcase"] is JsonObject showcase)
        {
            score += 10;
            var actualHash = GetNodeString(showcase["sourceActorSnapshotHash"]);
            var expectedHash = ComputeSourceSnapshotHash(teacher);
            if (string.Equals(actualHash, expectedHash, StringComparison.OrdinalIgnoreCase))
                score += 40;
        }

        score += teacher.Count;
        return score;
    }

    private static bool RefreshMortalShowcaseHashesForPendingRequests(
        JsonObject? npcRoot,
        IReadOnlyList<TrainingRequestState.PendingTrainingShowcaseRequest> pendingRequests)
    {
        if (npcRoot == null || pendingRequests.Count == 0)
            return false;

        var changed = false;
        foreach (var teacher in EnumerateNpcObjects(npcRoot).Where(IsMortalTeacher))
        {
            if (teacher["trainingShowcase"] is not JsonObject showcase)
                continue;

            var sourceActorId = ResolveMortalTeacherActorId(teacher);
            if (string.IsNullOrWhiteSpace(sourceActorId))
                continue;

            var pending = pendingRequests.FirstOrDefault(request =>
                string.Equals(request.RequestKind, MortalRequestKind, StringComparison.OrdinalIgnoreCase) &&
                string.Equals(request.SourceActorId, sourceActorId, StringComparison.OrdinalIgnoreCase));
            if (pending == null || string.IsNullOrWhiteSpace(pending.SourceActorSnapshotHash))
                continue;

            var showcaseRequestId = GetNodeString(showcase["requestId"]);
            if (!string.IsNullOrWhiteSpace(showcaseRequestId) &&
                !string.Equals(showcaseRequestId, pending.RequestId, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var actualHash = GetNodeString(showcase["sourceActorSnapshotHash"]);
            if (!string.Equals(actualHash, pending.SourceActorSnapshotHash, StringComparison.OrdinalIgnoreCase))
                continue;

            var expectedHash = ComputeSourceSnapshotHash(teacher);
            if (string.Equals(actualHash, expectedHash, StringComparison.OrdinalIgnoreCase))
                continue;

            showcase["sourceActorSnapshotHash"] = expectedHash;
            changed = true;
        }

        return changed;
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

    private static string ResolveAfterlifeTrainingTargetName(JsonObject offer, string targetKind, string targetId)
    {
        var explicitName =
            GetNodeString(offer["targetName"]) ??
            GetNodeString(offer["displayName"]) ??
            GetNodeString(offer["skillName"]);
        if (!string.IsNullOrWhiteSpace(explicitName))
            return explicitName;

        if (IsAfterlifeStandardArtTarget(targetKind))
            return FormatStandardSpiritualArtName(targetId);

        if (IsAfterlifeSpiritFocusTarget(targetKind))
            return "Средоточие Души";

        return targetId;
    }

    private static string NormalizeAfterlifeTrainingTargetKind(string? targetKind, string targetId)
    {
        var normalized = targetKind?.Trim();
        if (string.IsNullOrWhiteSpace(normalized))
        {
            return string.Equals(targetId, "spirit_focus", StringComparison.OrdinalIgnoreCase)
                ? "spirit_focus"
                : "standard_spiritual_art";
        }

        return normalized.ToLowerInvariant() switch
        {
            "standard_art" or "standardarts" or "standard_art_training" => "standard_spiritual_art",
            "special_art" or "specialarts" or "special_art_training" => "special_spiritual_art",
            "spiritfocus" or "spirit_focus_training" => "spirit_focus",
            _ => normalized
        };
    }

    private static string FormatStandardSpiritualArtName(string targetId) =>
        targetId.Trim().ToLowerInvariant() switch
        {
            "pressure" => "Давление",
            "counter" => "Контрприём",
            "guard" => "Защита",
            "maneuver" => "Манёвр",
            "binding" => "Оковы",
            "force_binding" => "Силовые оковы",
            "break_binding" => "Разрыв оков",
            "incarnation_resistance" => "Сопротивление воплощению",
            "champion_coordination" => "Согласование чемпиона",
            "recover_spiritual_power" => "Собрать Средоточие",
            _ => targetId
        };

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
        string.Equals(targetKind, "standard_art", StringComparison.OrdinalIgnoreCase) ||
        string.Equals(targetKind, "standardArts", StringComparison.OrdinalIgnoreCase) ||
        string.Equals(targetKind, "standard_art_training", StringComparison.OrdinalIgnoreCase) ||
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
        "каждое предложение должно иметь cost с Чернильными Перьями/Искрами Света, sourceActorSnapshotHash должен совпасть с requested hash " +
        $"{request.SourceActorSnapshotHash}. Клиент сам спишет валюту и поднимет уровень после покупки.";

    private static string BuildMortalSkillEvolutionPendingGmAction(
        TrainingRequestState.PendingTrainingShowcaseRequest request)
    {
        var details = request.Details;
        var targetName = GetNodeString(details?["targetName"]) ?? "навык";
        var targetKind = GetNodeString(details?["targetKind"]) ?? "skill";
        var targetValue = GetNodeInt(details?["targetValue"]);
        var offerId = GetNodeString(details?["offerId"]) ?? "unknown_offer";
        var instruction = GetNodeString(details?["gmInstruction"]) ??
                          "Создай или обнови полный объект навыка и matching skillMasteryChanges для оплаченного обучения.";

        return "Заверши оплаченное обучение в смертном мире для NPC-учителя " +
               $"{request.SourceActorName} ({request.SourceActorId}). " +
               $"Игрок уже оплатил offer {offerId}; не списывай деньги или опыт повторно. " +
               $"Цель: {targetName}, тип {targetKind}, новый уровень/мастерство {targetValue}. " +
               $"{instruction} Используй pending_training_showcase_requests.json requestId {request.RequestId} и details как источник аудита.";
    }

    private static bool IsExplicitPassiveMortalTrainingTarget(string targetKind) =>
        targetKind.Contains("passive", StringComparison.OrdinalIgnoreCase);

    private static bool IsExplicitActiveMortalTrainingTarget(string targetKind) =>
        targetKind.Contains("active", StringComparison.OrdinalIgnoreCase);

    private static string NormalizeMortalTrainingTargetKind(string targetKind, bool isPassive)
    {
        if (IsExplicitActiveMortalTrainingTarget(targetKind) ||
            IsExplicitPassiveMortalTrainingTarget(targetKind))
        {
            return targetKind;
        }

        if (targetKind.Contains("unlock", StringComparison.OrdinalIgnoreCase))
            return isPassive ? "passive_skill_unlock" : "active_skill_unlock";

        if (targetKind.Contains("progress", StringComparison.OrdinalIgnoreCase) ||
            targetKind.Contains("practice", StringComparison.OrdinalIgnoreCase))
        {
            return isPassive ? "passive_skill_mastery_progress" : "active_skill_mastery_progress";
        }

        if (targetKind.Contains("mastery", StringComparison.OrdinalIgnoreCase) ||
            targetKind.Contains("skill", StringComparison.OrdinalIgnoreCase) ||
            string.IsNullOrWhiteSpace(targetKind))
        {
            return isPassive ? "passive_skill_mastery" : "active_skill_mastery";
        }

        return targetKind;
    }

    private static bool IsMortalPracticeOffer(TrainingOffer offer) =>
        offer.TargetKind.Contains("progress", StringComparison.OrdinalIgnoreCase) ||
        offer.TargetKind.Contains("practice", StringComparison.OrdinalIgnoreCase);

    private static string ResolveMortalTeacherSkillKind(JsonObject teacher, string? targetId, string? targetName)
    {
        if (teacher["teacherProfile"] is not JsonObject profile ||
            profile["skills"] is not JsonArray skills)
        {
            return "";
        }

        var skill = skills.OfType<JsonObject>().FirstOrDefault(entry =>
            MatchesMortalTrainingTarget(entry, targetId, targetName));

        return GetNodeString(skill?["skillKind"]) ??
               GetNodeString(skill?["targetKind"]) ??
               GetNodeString(skill?["type"]) ??
               "";
    }

    private static JsonObject? FindMortalSkillObject(JsonObject root, string arrayName, TrainingOffer offer)
    {
        if (root[arrayName] is not JsonArray array)
            return null;

        return array.OfType<JsonObject>().FirstOrDefault(skill => MatchesMortalTrainingTarget(skill, offer));
    }

    private static JsonObject? FindMortalSkillObject(JsonObject root, string arrayName, string? targetId, string? targetName)
    {
        if (root[arrayName] is not JsonArray array)
            return null;

        return array.OfType<JsonObject>().FirstOrDefault(skill => MatchesMortalTrainingTarget(skill, targetId, targetName));
    }

    private static bool MatchesMortalTrainingTarget(JsonObject node, TrainingOffer offer)
    {
        return MatchesMortalTrainingTarget(node, offer.TargetId, offer.TargetName);
    }

    private static bool MatchesMortalTrainingTarget(JsonObject node, string? targetId, string? targetName)
    {
        var skillId = GetNodeString(node["skillId"]) ?? GetNodeString(node["id"]);
        var skillName =
            GetNodeString(node["skillName"]) ??
            GetNodeString(node["displayName"]) ??
            GetNodeString(node["name"]);
        return (!string.IsNullOrWhiteSpace(skillId) &&
                !string.IsNullOrWhiteSpace(targetId) &&
                string.Equals(skillId, targetId, StringComparison.OrdinalIgnoreCase)) ||
               (!string.IsNullOrWhiteSpace(skillName) &&
                !string.IsNullOrWhiteSpace(targetName) &&
                string.Equals(skillName, targetName, StringComparison.OrdinalIgnoreCase));
    }

    private static int ResolveMortalMasteryProgressGain(JsonObject offer)
    {
        foreach (var field in new[] { "masteryProgressGain", "progressGain", "masteryPoints", "practicePoints" })
        {
            var value = GetNodeInt(offer[field]);
            if (value > 0)
                return value;
        }

        return 0;
    }

    private static bool NormalizeAfterlifeMentorShowcaseCostsForProfile(JsonObject mentor)
    {
        if (mentor["mentorTrainingShowcase"] is not JsonObject showcase ||
            showcase["offers"] is not JsonArray offers)
        {
            return false;
        }

        var changed = false;
        foreach (var offer in offers.OfType<JsonObject>())
        {
            if (!TryBuildDeterministicAfterlifeMentorCost(mentor, offer, out var cost))
                continue;

            if (!TrainingCostsEqual(offer["cost"] as JsonObject, cost))
            {
                offer["cost"] = cost;
                changed = true;
            }
        }

        return changed;
    }

    private static bool TryBuildDeterministicAfterlifeMentorCost(
        JsonObject mentor,
        JsonObject offer,
        out JsonObject cost)
    {
        cost = new JsonObject();
        var targetId = GetNodeString(offer["targetId"]) ?? "";
        var targetKind = NormalizeAfterlifeTrainingTargetKind(
            GetNodeString(offer["targetKind"]) ?? GetNodeString(offer["targetType"]),
            targetId);
        var targetValue = Math.Max(0, GetNodeInt(offer["targetValue"]));
        if (string.IsNullOrWhiteSpace(targetId) || targetValue <= 0)
            return false;

        var baseInkFeatherCost = 0;
        if (IsAfterlifeStandardArtTarget(targetKind))
        {
            var art = AfterlifeSpiritualConflictState.SpiritualArts.FirstOrDefault(candidate =>
                string.Equals(candidate.ArtId, targetId, StringComparison.OrdinalIgnoreCase));
            if (art != null)
                baseInkFeatherCost = AfterlifeTrainingCostPolicy.ComputeStandardArtBaseInkFeatherCost(art, targetValue);
        }
        else if (IsAfterlifeSpiritFocusTarget(targetKind))
        {
            baseInkFeatherCost = AfterlifeTrainingCostPolicy.ComputeSpiritFocusBaseInkFeatherCost(targetValue);
        }

        if (baseInkFeatherCost <= 0)
            return false;

        var relationshipLevel = ResolveAfterlifeMentorRelationshipLevel(mentor);
        cost["inkFeathers"] = AfterlifeTrainingCostPolicy.ComputeMentorCost(baseInkFeatherCost, relationshipLevel);
        cost["lightSparks"] = 0;
        return true;
    }

    private static bool TrainingCostsEqual(JsonObject? left, JsonObject right)
    {
        if (left == null)
            return false;

        return GetNodeInt(left["money"]) == GetNodeInt(right["money"]) &&
               GetNodeInt(left["currentLevelExperiencePercent"]) == GetNodeInt(right["currentLevelExperiencePercent"]) &&
               GetTrainingCostAmount(left, "inkFeathers", "inkFeatherCost", "inkFeathersCost", "costInFeathers") ==
               GetTrainingCostAmount(right, "inkFeathers", "inkFeatherCost", "inkFeathersCost", "costInFeathers") &&
               GetTrainingCostAmount(left, "lightSparks", "lightSparkCost", "lightSparksCost", "costInLightSparks") ==
               GetTrainingCostAmount(right, "lightSparks", "lightSparkCost", "lightSparksCost", "costInLightSparks");
    }

    private static int FirstPositive(params int[] values)
    {
        foreach (var value in values)
        {
            if (value > 0)
                return value;
        }

        return 0;
    }

    private static int FirstPositiveOrZero(params int[] values)
    {
        foreach (var value in values)
        {
            if (value >= 0)
                return value;
        }

        return 0;
    }

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

    private static int GetTrainingCostAmount(JsonObject? cost, string canonicalCurrency, params string[] aliases)
    {
        if (cost == null)
            return 0;

        var direct = GetNodeInt(cost[canonicalCurrency]);
        if (direct != 0)
            return direct;

        foreach (var alias in aliases)
        {
            direct = GetNodeInt(cost[alias]);
            if (direct != 0)
                return direct;
        }

        var currency = GetNodeString(cost["currency"]) ?? GetNodeString(cost["costCurrency"]);
        if (!CurrencyMatches(currency, canonicalCurrency))
            return 0;

        return GetNodeInt(cost["amount"]);
    }

    private static bool CurrencyMatches(string? currency, string canonicalCurrency)
    {
        if (string.IsNullOrWhiteSpace(currency))
            return false;

        var normalized = currency.Trim().Replace(" ", "", StringComparison.OrdinalIgnoreCase);
        return canonicalCurrency switch
        {
            "inkFeathers" => string.Equals(normalized, "inkFeathers", StringComparison.OrdinalIgnoreCase) ||
                             string.Equals(normalized, "ЧернильныеПерья", StringComparison.OrdinalIgnoreCase),
            "lightSparks" => string.Equals(normalized, "lightSparks", StringComparison.OrdinalIgnoreCase) ||
                              string.Equals(normalized, "ИскрыСвета", StringComparison.OrdinalIgnoreCase),
            _ => false
        };
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
