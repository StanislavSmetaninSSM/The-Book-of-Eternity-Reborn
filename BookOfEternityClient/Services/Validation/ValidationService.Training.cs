using System.Text.Json;
using System.Text.Json.Nodes;
using BookOfEternityClient.Models;

namespace BookOfEternityClient.Services;

public partial class ValidationService
{
    private const string TrainingNpcCorePath = "game_state/npcs/npc_core.json";

    private async Task ValidateTrainingShowcasesAsync(List<ValidationIssue> issues)
    {
        await ValidatePendingTrainingRequestsAsync(issues);
        await ValidateAfterlifeMentorTrainingShowcasesAsync(issues);
        await ValidateMortalTeacherTrainingShowcasesAsync(issues);
    }

    private async Task ValidatePendingTrainingRequestsAsync(List<ValidationIssue> issues)
    {
        var raw = await _fs.ReadFileAsync(TrainingRequestState.PendingRequestPath);
        if (string.IsNullOrWhiteSpace(raw))
            return;

        try
        {
            using var doc = JsonDocument.Parse(raw);
            if (!doc.RootElement.TryGetProperty("requests", out var requests) ||
                requests.ValueKind != JsonValueKind.Array)
            {
                return;
            }

            var index = 0;
            foreach (var request in requests.EnumerateArray())
            {
                var context = $"{TrainingRequestState.PendingRequestPath}.requests[{index++}]";
                if (request.ValueKind != JsonValueKind.Object)
                    continue;

                var requestKind = GetTrainingString(request, "requestKind");
                if (!string.Equals(requestKind, "mortal_training_skill_evolution", StringComparison.OrdinalIgnoreCase))
                    continue;

                ValidateMortalTrainingSkillEvolutionRequest(request, context, issues);
            }
        }
        catch (JsonException)
        {
            // JSON integrity validation reports malformed files.
        }
    }

    private static void ValidateMortalTrainingSkillEvolutionRequest(
        JsonElement request,
        string requestContext,
        List<ValidationIssue> issues)
    {
        if (!request.TryGetProperty("details", out var details) ||
            details.ValueKind != JsonValueKind.Object)
        {
            AddTrainingIssue(
                $"{requestContext}.details",
                "training_skill_evolution_missing_details",
                "Mortal training skill evolution request требует details object с оплаченным offer audit и состоянием навыка.",
                "details object",
                "missing",
                "Сохрани request.details с offerId, targetId, targetName, targetKind, targetValue, sourceCap, spent resources, skillStateBefore и gmInstruction.",
                issues);
            return;
        }

        foreach (var required in new[] { "offerId", "targetId", "targetName", "targetKind", "gmInstruction" })
        {
            if (string.IsNullOrWhiteSpace(GetTrainingString(details, required)))
            {
                AddTrainingIssue(
                    $"{requestContext}.details.{required}",
                    "training_skill_evolution_missing_required_field",
                    "Mortal training skill evolution request lacks required text field.",
                    $"non-empty details.{required}",
                    "missing",
                    "Восстанови request.details from the paid training offer audit.",
                    issues);
            }
        }

        var targetValue = GetTrainingInt(details, "targetValue");
        var sourceCap = GetTrainingInt(details, "sourceCap");
        if (targetValue <= 0 || sourceCap <= 0 || targetValue > sourceCap)
        {
            AddTrainingIssue(
                $"{requestContext}.details.targetValue",
                "training_skill_evolution_invalid_target_or_cap",
                "Mortal training skill evolution request has invalid target/sourceCap audit.",
                "0 < targetValue <= sourceCap",
                $"targetValue={targetValue}, sourceCap={sourceCap}",
                "Пересоздай request через клиентскую покупку из свежей витрины учителя.",
                issues);
        }

        if (GetTrainingInt(details, "moneySpent") < 0 ||
            GetTrainingInt(details, "currentLevelExperienceSpent") < 0 ||
            GetTrainingInt(details, "currentLevelExperiencePercent") < 0)
        {
            AddTrainingIssue(
                $"{requestContext}.details",
                "training_skill_evolution_negative_spent_resources",
                "Mortal training skill evolution request cannot contain negative spent resources.",
                "moneySpent/currentLevelExperienceSpent/currentLevelExperiencePercent >= 0",
                details.ToString(),
                "Откати поврежденный request или пересоздай покупку через клиент.",
                issues);
        }

        if (!details.TryGetProperty("skillStateBefore", out var skillStateBefore) ||
            skillStateBefore.ValueKind != JsonValueKind.Object)
        {
            AddTrainingIssue(
                $"{requestContext}.details.skillStateBefore",
                "training_skill_evolution_missing_skill_state_before",
                "Mortal training skill evolution request должен хранить снимок навыка до авторского обновления.",
                "skillStateBefore object",
                "missing",
                "Добавь skillStateBefore с currentMasteryLevel/currentMasteryProgress/masteryProgressNeeded и текущим skill/mastery snapshot, если он есть.",
                issues);
        }
    }

    private async Task ValidateAfterlifeMentorTrainingShowcasesAsync(List<ValidationIssue> issues)
    {
        var raw = await _fs.ReadFileAsync(AfterlifeEntityProfileState.StatePath);
        if (string.IsNullOrWhiteSpace(raw))
            return;

        try
        {
            using var doc = JsonDocument.Parse(raw);
            if (!doc.RootElement.TryGetProperty(AfterlifeEntityProfileState.ProfilesProperty, out var profiles) ||
                profiles.ValueKind != JsonValueKind.Array)
            {
                return;
            }

            var index = 0;
            var profileList = new List<JsonElement>();
            foreach (var profile in profiles.EnumerateArray())
            {
                var context = $"{AfterlifeEntityProfileState.StatePath}.profiles[{index++}]";
                if (profile.ValueKind == JsonValueKind.Object)
                    profileList.Add(profile.Clone());
                if (profile.ValueKind != JsonValueKind.Object ||
                    !profile.TryGetProperty("mentorTrainingShowcase", out var showcase) ||
                    showcase.ValueKind != JsonValueKind.Object)
                {
                    continue;
                }

                ValidateTrainingShowcaseActorMetadata(
                    profile,
                    showcase,
                    context,
                    "mentorTrainingShowcase",
                    ResolveAfterlifeTrainingActorId(profile),
                    ["afterlife", NormalizeTrainingRealm(GetTrainingString(profile, "realm") ?? "afterlife")],
                    issues);
                ValidateTrainingTeachingCapability(
                    profile,
                    context,
                    "mentorTrainingShowcase",
                    "mentorProfile",
                    issues);
                ValidateTrainingShowcaseSnapshot(
                    profile,
                    showcase,
                    context,
                    "mentorTrainingShowcase",
                    issues);
                ValidateTrainingOffers(
                    showcase,
                    context,
                    "mentorTrainingShowcase",
                    (offer, offerContext) => ResolveAfterlifeMentorTrainingActorCap(profile, offer, offerContext, issues),
                    issues);
            }

            await ValidateAfterlifeTrainingReceiptsAsync(profileList, issues);
        }
        catch (JsonException)
        {
            // JSON integrity validation reports malformed files.
        }
    }

    private async Task ValidateMortalTeacherTrainingShowcasesAsync(List<ValidationIssue> issues)
    {
        var raw = await _fs.ReadFileAsync(TrainingNpcCorePath);
        if (string.IsNullOrWhiteSpace(raw))
            return;

        try
        {
            using var doc = JsonDocument.Parse(raw);
            foreach (var (teacher, context) in EnumerateMortalNpcTrainingCandidates(doc.RootElement))
            {
                if (!teacher.TryGetProperty("trainingShowcase", out var showcase) ||
                    showcase.ValueKind != JsonValueKind.Object)
                {
                    continue;
                }

                ValidateTrainingShowcaseActorMetadata(
                    teacher,
                    showcase,
                    context,
                    "trainingShowcase",
                    ResolveMortalTrainingActorId(teacher),
                    ["mortal", "mortal_world"],
                    issues);
                ValidateTrainingTeachingCapability(
                    teacher,
                    context,
                    "trainingShowcase",
                    "teacherProfile",
                    issues);
                ValidateTrainingShowcaseSnapshot(
                    teacher,
                    showcase,
                    context,
                    "trainingShowcase",
                    issues);
                ValidateTrainingOffers(
                    showcase,
                    context,
                    "trainingShowcase",
                    (offer, offerContext) => ResolveMortalTeacherTrainingActorCap(teacher, offer, offerContext, issues),
                    issues);
            }

            ValidateMortalTrainingReceipts(doc.RootElement, issues);
        }
        catch (JsonException)
        {
            // JSON integrity validation reports malformed files.
        }
    }

    private static void ValidateTrainingShowcaseActorMetadata(
        JsonElement sourceActor,
        JsonElement showcase,
        string sourceContext,
        string showcaseProperty,
        string? expectedSourceActorId,
        IReadOnlyCollection<string> acceptedRealms,
        List<ValidationIssue> issues)
    {
        var sourceActorId = GetTrainingString(showcase, "sourceActorId");
        if (!string.IsNullOrWhiteSpace(sourceActorId) &&
            !string.IsNullOrWhiteSpace(expectedSourceActorId) &&
            !string.Equals(sourceActorId, expectedSourceActorId, StringComparison.OrdinalIgnoreCase))
        {
            AddTrainingIssue(
                $"{sourceContext}.{showcaseProperty}.sourceActorId",
                "training_showcase_source_actor_mismatch",
                "Витрина обучения прикреплена к одному источнику, но sourceActorId указывает на другого.",
                expectedSourceActorId,
                sourceActorId,
                "Исправь sourceActorId на id текущего учителя/наставника или перенеси витрину к правильному actor profile.",
                issues);
        }

        var realm = NormalizeTrainingRealm(GetTrainingString(showcase, "realm"));
        if (!string.IsNullOrWhiteSpace(realm) &&
            !acceptedRealms.Contains(realm, StringComparer.OrdinalIgnoreCase))
        {
            AddTrainingIssue(
                $"{sourceContext}.{showcaseProperty}.realm",
                "training_showcase_wrong_realm",
                "Витрина обучения заявляет realm, который не соответствует источнику обучения.",
                string.Join(" or ", acceptedRealms),
                realm,
                "Синхронизируй realm витрины с текущим царством источника обучения; не смешивай mortal_world, chaos_sea и shining_abode.",
                issues);
        }
    }

    private static void ValidateTrainingTeachingCapability(
        JsonElement sourceActor,
        string sourceContext,
        string showcaseProperty,
        string profileProperty,
        List<ValidationIssue> issues)
    {
        if (sourceActor.TryGetProperty(profileProperty, out var teachingProfile) &&
            teachingProfile.ValueKind == JsonValueKind.Object &&
            teachingProfile.TryGetProperty("canTeach", out var canTeach) &&
            TryGetTrainingBool(canTeach, out var canTeachValue) &&
            canTeachValue)
        {
            return;
        }

        if (string.Equals(profileProperty, "mentorProfile", StringComparison.OrdinalIgnoreCase))
        {
            if (sourceActor.TryGetProperty("canTeachPlayer", out var canTeachPlayer) &&
                TryGetTrainingBool(canTeachPlayer, out var canTeachPlayerValue) &&
                canTeachPlayerValue)
            {
                return;
            }

            foreach (var specialArt in EnumerateTrainingSpecialArts(sourceActor))
            {
                if (specialArt.TryGetProperty("canTeachPlayer", out var specialArtCanTeachPlayer) &&
                    TryGetTrainingBool(specialArtCanTeachPlayer, out var specialArtCanTeachPlayerValue) &&
                    specialArtCanTeachPlayerValue)
                {
                    return;
                }
            }
        }

        var expected = string.Equals(profileProperty, "mentorProfile", StringComparison.OrdinalIgnoreCase)
            ? $"{profileProperty}.canTeach = true or canTeachPlayer = true or specialArts[].canTeachPlayer = true"
            : $"{profileProperty}.canTeach = true";
        var repairHint = string.Equals(profileProperty, "mentorProfile", StringComparison.OrdinalIgnoreCase)
            ? $"Добавь {profileProperty}.canTeach = true, canTeachPlayer=true или teachable specialArts[].canTeachPlayer=true, если источник действительно обучает, или убери витрину обучения."
            : $"Добавь {profileProperty}.canTeach = true, если источник действительно обучает, или убери витрину обучения.";

        AddTrainingIssue(
            $"{sourceContext}.{showcaseProperty}",
            "training_showcase_source_cannot_teach",
            "Витрина обучения есть у источника, который не помечен как учитель/наставник.",
            expected,
            "missing or false",
            repairHint,
            issues);
    }

    private static IEnumerable<(JsonElement Teacher, string Context)> EnumerateMortalNpcTrainingCandidates(JsonElement root)
    {
        foreach (var sectionName in new[] { "UpdateNPCs", "NPCsInScene", "NPCs", "npcs", "npcDataChanges" })
        {
            if (!root.TryGetProperty(sectionName, out var array) || array.ValueKind != JsonValueKind.Array)
                continue;

            var index = 0;
            foreach (var item in array.EnumerateArray())
            {
                if (item.ValueKind == JsonValueKind.Object)
                    yield return (item, $"{TrainingNpcCorePath}.{sectionName}[{index}]");
                index++;
            }
        }
    }

    private static void ValidateTrainingShowcaseSnapshot(
        JsonElement sourceActor,
        JsonElement showcase,
        string sourceContext,
        string showcaseProperty,
        List<ValidationIssue> issues)
    {
        var sourceNode = JsonNode.Parse(sourceActor.GetRawText()) as JsonObject;
        if (sourceNode == null)
            return;

        var expectedHash = TrainingService.ComputeSourceSnapshotHash(sourceNode);
        var actualHash = GetTrainingString(showcase, "sourceActorSnapshotHash");
        if (!string.Equals(actualHash, expectedHash, StringComparison.OrdinalIgnoreCase))
        {
            AddTrainingIssue(
                $"{sourceContext}.{showcaseProperty}.sourceActorSnapshotHash",
                "training_showcase_stale_source_actor_snapshot",
                "Витрина обучения устарела: sourceActorSnapshotHash не совпадает с текущим профилем источника.",
                expectedHash,
                actualHash ?? "missing",
                $"Обнови витрину обучения по pending_training_showcase_requests.json и запиши свежий sourceActorSnapshotHash: {expectedHash}. Если профиль учителя меняется в этом же ремонте, сначала зафиксируй профиль, затем используй exactFieldCorrections[] из repair packet.",
                issues);
        }
    }

    private static void ValidateTrainingOffers(
        JsonElement showcase,
        string sourceContext,
        string showcaseProperty,
        Func<JsonElement, string, int> resolveActorCap,
        List<ValidationIssue> issues)
    {
        if (!showcase.TryGetProperty("offers", out var offers) || offers.ValueKind != JsonValueKind.Array)
        {
            AddTrainingIssue(
                $"{sourceContext}.{showcaseProperty}.offers",
                "training_showcase_missing_offers",
                "Витрина обучения должна содержать offers array.",
                "offers array",
                "missing",
                "Добавь хотя бы одно offer или убери пустую витрину до подготовки данных.",
                issues);
            return;
        }

        var seenOfferIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var index = 0;
        foreach (var offer in offers.EnumerateArray())
        {
            var offerContext = $"{sourceContext}.{showcaseProperty}.offers[{index++}]";
            if (offer.ValueKind != JsonValueKind.Object)
            {
                AddTrainingIssue(
                    offerContext,
                    "training_showcase_offer_not_object",
                    "Предложение обучения должно быть object.",
                    "object",
                    offer.ValueKind.ToString(),
                    "Замени offer на объект с offerId, targetKind, targetId, targetValue, sourceCap и cost.",
                    issues);
                continue;
            }

            var offerId = GetTrainingString(offer, "offerId");
            if (string.IsNullOrWhiteSpace(offerId))
            {
                AddTrainingIssue(
                    $"{offerContext}.offerId",
                    "training_showcase_offer_missing_id",
                    "Предложение обучения должно иметь offerId.",
                    "non-empty offerId",
                    "missing",
                    "Добавь уникальный offerId.",
                    issues);
            }
            else if (!seenOfferIds.Add(offerId))
            {
                AddTrainingIssue(
                    $"{offerContext}.offerId",
                    "training_showcase_duplicate_offer_id",
                    "offerId в витрине обучения должен быть уникальным.",
                    "unique offerId",
                    offerId,
                    "Переименуй один из duplicate offerId.",
                    issues);
            }

            var targetId = GetTrainingString(offer, "targetId");
            if (string.IsNullOrWhiteSpace(targetId))
            {
                AddTrainingIssue(
                    $"{offerContext}.targetId",
                    "training_showcase_offer_missing_target",
                    "Предложение обучения должно указывать targetId.",
                    "non-empty targetId",
                    "missing",
                    "Укажи skillId/artId цели обучения.",
                    issues);
            }

            var targetValue = GetTrainingInt(offer, "targetValue");
            var sourceCap = GetTrainingInt(offer, "sourceCap");
            if (targetValue <= 0)
            {
                AddTrainingIssue(
                    $"{offerContext}.targetValue",
                    "training_showcase_invalid_target_value",
                    "targetValue должен быть положительным уровнем обучения.",
                    "integer > 0",
                    targetValue.ToString(),
                    "Укажи целевой уровень/мастерство выше текущего.",
                    issues);
            }

            if (sourceCap <= 0)
            {
                AddTrainingIssue(
                    $"{offerContext}.sourceCap",
                    "training_showcase_invalid_source_cap",
                    "sourceCap должен быть положительным пределом источника.",
                    "integer > 0",
                    sourceCap.ToString(),
                    "Запиши реальный максимум навыка/искусства у учителя или наставника.",
                    issues);
            }
            else if (targetValue > sourceCap)
            {
                AddTrainingIssue(
                    $"{offerContext}.targetValue",
                    "training_showcase_target_exceeds_source_cap",
                    "targetValue не может превышать sourceCap предложения.",
                    "targetValue <= sourceCap",
                    $"{targetValue} > {sourceCap}",
                    "Понизь targetValue или обнови источник обучения, если он действительно знает более высокий уровень.",
                    issues);
            }

            var actorCap = resolveActorCap(offer, offerContext);
            if (actorCap >= 0 && sourceCap > actorCap)
            {
                AddTrainingIssue(
                    $"{offerContext}.sourceCap",
                    "training_showcase_source_cap_exceeds_actor_cap",
                    "sourceCap в предложении превышает фактический уровень источника обучения.",
                    $"sourceCap <= {actorCap}",
                    sourceCap.ToString(),
                    "Синхронизируй sourceCap с навыками/духовными искусствами учителя или наставника.",
                    issues);
            }

            ValidateTrainingOfferCost(offer, offerContext, issues);
        }
    }

    private static void ValidateTrainingOfferCost(JsonElement offer, string offerContext, List<ValidationIssue> issues)
    {
        if (!offer.TryGetProperty("cost", out var cost) || cost.ValueKind != JsonValueKind.Object)
        {
            AddTrainingIssue(
                $"{offerContext}.cost",
                "training_showcase_missing_cost",
                "Предложение обучения должно иметь cost object.",
                "cost object",
                "missing",
                "Добавь цену в деньгах/процентах опыта или afterlife currency.",
                issues);
            return;
        }

        var money = GetTrainingInt(cost, "money");
        var currentLevelExperiencePercent = GetTrainingInt(cost, "currentLevelExperiencePercent");
        var inkFeathers = ResolveTrainingCostCurrencyAmount(cost, "inkFeathers");
        var lightSparks = ResolveTrainingCostCurrencyAmount(cost, "lightSparks");
        if (money < 0 || currentLevelExperiencePercent < 0 || inkFeathers < 0 || lightSparks < 0)
        {
            AddTrainingIssue(
                $"{offerContext}.cost",
                "training_showcase_negative_cost",
                "Цена обучения не может быть отрицательной.",
                "all cost fields >= 0",
                cost.ToString(),
                "Убери отрицательные значения из cost.",
                issues);
        }

        if (money <= 0 && currentLevelExperiencePercent <= 0 && inkFeathers <= 0 && lightSparks <= 0)
        {
            AddTrainingIssue(
                $"{offerContext}.cost",
                "training_showcase_zero_cost",
                "Предложение обучения должно иметь положительную цену.",
                "at least one positive cost field",
                cost.ToString(),
                "Укажи деньги/процент опыта/Чернильные Перья/Искры Света.",
                issues);
        }
    }

    private static int ResolveTrainingCostCurrencyAmount(JsonElement cost, string canonicalCurrency)
    {
        var direct = GetTrainingInt(cost, canonicalCurrency);
        if (direct != 0)
            return direct;

        var currency = GetTrainingString(cost, "currency");
        if (string.IsNullOrWhiteSpace(currency) ||
            !CurrencyMatches(currency, canonicalCurrency))
        {
            return direct;
        }

        return GetTrainingInt(cost, "amount");
    }

    private static bool CurrencyMatches(string currency, string canonicalCurrency)
    {
        var normalized = currency.Trim().Replace(" ", "", StringComparison.OrdinalIgnoreCase);
        return canonicalCurrency switch
        {
            "inkFeathers" => string.Equals(normalized, "inkFeathers", StringComparison.OrdinalIgnoreCase) ||
                             string.Equals(normalized, "InkFeathers", StringComparison.OrdinalIgnoreCase) ||
                             string.Equals(normalized, "ЧернильныеПерья", StringComparison.OrdinalIgnoreCase),
            "lightSparks" => string.Equals(normalized, "lightSparks", StringComparison.OrdinalIgnoreCase) ||
                              string.Equals(normalized, "LightSparks", StringComparison.OrdinalIgnoreCase) ||
                              string.Equals(normalized, "ИскрыСвета", StringComparison.OrdinalIgnoreCase),
            _ => false
        };
    }

    private static void ValidateMortalTrainingReceipts(JsonElement root, List<ValidationIssue> issues)
    {
        if (!root.TryGetProperty("trainingPurchaseReceipts", out var receipts) ||
            receipts.ValueKind != JsonValueKind.Array)
        {
            return;
        }

        var teachers = EnumerateMortalNpcTrainingCandidates(root).ToArray();
        var index = 0;
        foreach (var receipt in receipts.EnumerateArray())
        {
            var receiptContext = $"{TrainingNpcCorePath}.trainingPurchaseReceipts[{index++}]";
            if (receipt.ValueKind != JsonValueKind.Object)
            {
                AddTrainingIssue(
                    receiptContext,
                    "training_purchase_receipt_not_object",
                    "Training purchase receipt должен быть object.",
                    "object",
                    receipt.ValueKind.ToString(),
                    "Замени receipt на объект с sourceActorId, offerId, realm и списанными ресурсами.",
                    issues);
                continue;
            }

            ValidateTrainingReceiptRealm(receipt, receiptContext, ["mortal", "mortal_world"], issues);
            var sourceActorId = GetTrainingString(receipt, "sourceActorId");
            var teacher = teachers.FirstOrDefault(candidate =>
                string.Equals(ResolveMortalTrainingActorId(candidate.Teacher), sourceActorId, StringComparison.OrdinalIgnoreCase));
            if (teacher.Teacher.ValueKind == JsonValueKind.Undefined)
            {
                AddTrainingIssue(
                    $"{receiptContext}.sourceActorId",
                    "training_purchase_receipt_missing_source_actor",
                    "Training receipt ссылается на отсутствующего учителя.",
                    "existing teacher sourceActorId",
                    sourceActorId ?? "missing",
                    "Исправь receipt.sourceActorId или восстанови учителя с matching npcId/NPCId/initialId в npc_core.",
                    issues);
                continue;
            }

            ValidateTrainingReceiptAgainstShowcaseOffer(
                receipt,
                receiptContext,
                teacher.Teacher,
                "trainingShowcase",
                isAfterlife: false,
                issues);
        }
    }

    private async Task ValidateAfterlifeTrainingReceiptsAsync(
        IReadOnlyList<JsonElement> profiles,
        List<ValidationIssue> issues)
    {
        var raw = await _fs.ReadFileAsync("game_state/meta/soul_state.json");
        if (string.IsNullOrWhiteSpace(raw))
            return;

        try
        {
            using var doc = JsonDocument.Parse(raw);
            if (!doc.RootElement.TryGetProperty(TrainingRequestState.AfterlifePurchaseReceiptsProperty, out var receipts) ||
                receipts.ValueKind != JsonValueKind.Array)
            {
                return;
            }

            var index = 0;
            foreach (var receipt in receipts.EnumerateArray())
            {
                var receiptContext = $"game_state/meta/soul_state.json.{TrainingRequestState.AfterlifePurchaseReceiptsProperty}[{index++}]";
                if (receipt.ValueKind != JsonValueKind.Object)
                {
                    AddTrainingIssue(
                        receiptContext,
                        "training_purchase_receipt_not_object",
                        "Afterlife training receipt должен быть object.",
                        "object",
                        receipt.ValueKind.ToString(),
                        "Замени receipt на объект с sourceActorId, offerId, realm и списанными ресурсами.",
                        issues);
                    continue;
                }

                ValidateTrainingReceiptRealm(receipt, receiptContext, ["afterlife", "chaos_sea", "shining_abode"], issues);
                var sourceActorId = GetTrainingString(receipt, "sourceActorId");
                if (string.Equals(sourceActorId, "self", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(sourceActorId, "self_fallback", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                var profile = profiles.FirstOrDefault(candidate =>
                    string.Equals(ResolveAfterlifeTrainingActorId(candidate), sourceActorId, StringComparison.OrdinalIgnoreCase));
                if (profile.ValueKind == JsonValueKind.Undefined)
                {
                    AddTrainingIssue(
                        $"{receiptContext}.sourceActorId",
                        "training_purchase_receipt_missing_source_actor",
                        "Afterlife training receipt ссылается на отсутствующего наставника.",
                        "existing afterlife actorId",
                        sourceActorId ?? "missing",
                        "Исправь receipt.sourceActorId или восстанови профиль наставника в afterlife_entity_profiles.",
                        issues);
                    continue;
                }

                ValidateTrainingReceiptAgainstShowcaseOffer(
                    receipt,
                    receiptContext,
                    profile,
                    "mentorTrainingShowcase",
                    isAfterlife: true,
                    issues);
            }
        }
        catch (JsonException)
        {
            // JSON integrity validation reports malformed files.
        }
    }

    private static void ValidateTrainingReceiptRealm(
        JsonElement receipt,
        string receiptContext,
        IReadOnlyCollection<string> acceptedRealms,
        List<ValidationIssue> issues)
    {
        var realm = NormalizeTrainingRealm(GetTrainingString(receipt, "realm"));
        if (string.IsNullOrWhiteSpace(realm) ||
            acceptedRealms.Contains(realm, StringComparer.OrdinalIgnoreCase))
        {
            return;
        }

        AddTrainingIssue(
            $"{receiptContext}.realm",
            "training_purchase_receipt_wrong_realm",
            "Training receipt заявляет realm, несовместимый с местом обучения.",
            string.Join(" or ", acceptedRealms),
            realm,
            "Исправь receipt.realm и не переносите чеки mortal/afterlife между разными режимами.",
            issues);
    }

    private static void ValidateTrainingReceiptAgainstShowcaseOffer(
        JsonElement receipt,
        string receiptContext,
        JsonElement sourceActor,
        string showcaseProperty,
        bool isAfterlife,
        List<ValidationIssue> issues)
    {
        if (!sourceActor.TryGetProperty(showcaseProperty, out var showcase) ||
            showcase.ValueKind != JsonValueKind.Object)
        {
            AddTrainingIssue(
                $"{receiptContext}.offerId",
                "training_purchase_receipt_missing_fresh_offer",
                "Training receipt не имеет matching fresh showcase у источника обучения.",
                $"{showcaseProperty}.offers contains receipt.offerId",
                GetTrainingString(receipt, "offerId") ?? "missing",
                "Восстанови свежую витрину обучения или удали receipt, который невозможно проверить.",
                issues);
            return;
        }

        var expectedHash = TrainingService.ComputeSourceSnapshotHash(JsonNode.Parse(sourceActor.GetRawText())!.AsObject());
        var receiptHash = GetTrainingString(receipt, "sourceActorSnapshotHash");
        if (!string.IsNullOrWhiteSpace(receiptHash) &&
            !string.Equals(receiptHash, expectedHash, StringComparison.OrdinalIgnoreCase))
        {
            AddTrainingIssue(
                $"{receiptContext}.sourceActorSnapshotHash",
                "training_purchase_receipt_stale_source_actor_snapshot",
                "Training receipt создан против устаревшего sourceActorSnapshotHash.",
                expectedHash,
                receiptHash,
                "Пересоздай receipt из свежей витрины или откати неверную покупку.",
                issues);
        }

        var offerId = GetTrainingString(receipt, "offerId");
        var offer = FindTrainingOfferById(showcase, offerId);
        if (offer == null)
        {
            AddTrainingIssue(
                $"{receiptContext}.offerId",
                "training_purchase_receipt_missing_fresh_offer",
                "Training receipt ссылается на offerId, которого нет в свежей витрине.",
                "matching offerId in showcase.offers",
                offerId ?? "missing",
                "Исправь receipt.offerId или восстанови matching offer в свежей витрине обучения.",
                issues);
            return;
        }

        ValidateTrainingReceiptTargetAudit(receipt, receiptContext, offer.Value, issues);
        ValidateTrainingReceiptResourceAudit(receipt, receiptContext, offer.Value, isAfterlife, issues);
    }

    private static JsonElement? FindTrainingOfferById(JsonElement showcase, string? offerId)
    {
        if (string.IsNullOrWhiteSpace(offerId) ||
            !showcase.TryGetProperty("offers", out var offers) ||
            offers.ValueKind != JsonValueKind.Array)
        {
            return null;
        }

        foreach (var offer in offers.EnumerateArray())
        {
            if (offer.ValueKind == JsonValueKind.Object &&
                string.Equals(GetTrainingString(offer, "offerId"), offerId, StringComparison.OrdinalIgnoreCase))
            {
                return offer.Clone();
            }
        }

        return null;
    }

    private static void ValidateTrainingReceiptTargetAudit(
        JsonElement receipt,
        string receiptContext,
        JsonElement offer,
        List<ValidationIssue> issues)
    {
        var mismatches = new List<string>();
        AddTrainingStringMismatch(mismatches, "targetId", GetTrainingString(offer, "targetId"), GetTrainingString(receipt, "targetId"));
        AddTrainingStringMismatch(mismatches, "targetKind", GetTrainingString(offer, "targetKind"), GetTrainingString(receipt, "targetKind"));
        AddTrainingIntMismatch(mismatches, "targetValue", GetTrainingInt(offer, "targetValue"), GetTrainingInt(receipt, "targetValue"));
        AddTrainingIntMismatch(mismatches, "sourceCap", GetTrainingInt(offer, "sourceCap"), GetTrainingInt(receipt, "sourceCap"));
        if (mismatches.Count == 0)
            return;

        AddTrainingIssue(
            receiptContext,
            "training_purchase_receipt_offer_mismatch",
            "Training receipt не совпадает с matching offer.",
            "receipt target audit equals showcase offer",
            string.Join(", ", mismatches),
            "Синхронизируй receipt с offer из свежей витрины или пересоздай покупку через клиент.",
            issues);
    }

    private static void ValidateTrainingReceiptResourceAudit(
        JsonElement receipt,
        string receiptContext,
        JsonElement offer,
        bool isAfterlife,
        List<ValidationIssue> issues)
    {
        var cost = offer.TryGetProperty("cost", out var costElement) && costElement.ValueKind == JsonValueKind.Object
            ? costElement
            : default;
        if (cost.ValueKind != JsonValueKind.Object)
            return;

        var mismatches = new List<string>();
        if (isAfterlife)
        {
            AddTrainingIntMismatch(mismatches, "inkFeathersSpent", GetTrainingInt(cost, "inkFeathers"), GetTrainingInt(receipt, "inkFeathersSpent"));
            AddTrainingIntMismatch(mismatches, "lightSparksSpent", GetTrainingInt(cost, "lightSparks"), GetTrainingInt(receipt, "lightSparksSpent"));
        }
        else
        {
            AddTrainingIntMismatch(mismatches, "moneySpent", GetTrainingInt(cost, "money"), GetTrainingInt(receipt, "moneySpent"));
            AddTrainingIntMismatch(mismatches, "currentLevelExperiencePercent", GetTrainingInt(cost, "currentLevelExperiencePercent"), GetTrainingInt(receipt, "currentLevelExperiencePercent"));
        }

        if (mismatches.Count == 0)
            return;

        AddTrainingIssue(
            receiptContext,
            "training_purchase_receipt_resource_mismatch",
            "Training receipt заявляет списание ресурсов, которое не совпадает с matching offer.",
            "receipt resource audit equals showcase cost",
            string.Join(", ", mismatches),
            "Исправь receipt resource fields или пересоздай receipt покупкой через клиент из свежей витрины.",
            issues);
    }

    private static void AddTrainingStringMismatch(
        List<string> mismatches,
        string field,
        string? expected,
        string? actual)
    {
        if (string.Equals(expected ?? string.Empty, actual ?? string.Empty, StringComparison.OrdinalIgnoreCase))
            return;
        mismatches.Add($"{field}: expected {expected ?? "missing"}, actual {actual ?? "missing"}");
    }

    private static void AddTrainingIntMismatch(List<string> mismatches, string field, int expected, int actual)
    {
        if (expected == actual)
            return;
        mismatches.Add($"{field}: expected {expected}, actual {actual}");
    }

    private static int ResolveAfterlifeMentorTrainingActorCap(
        JsonElement profile,
        JsonElement offer,
        string offerContext,
        List<ValidationIssue> issues)
    {
        var targetKind = GetTrainingString(offer, "targetKind") ?? "standard_spiritual_art";
        var targetId = GetTrainingString(offer, "targetId") ?? "";
        if (string.IsNullOrWhiteSpace(targetId))
            return -1;

        if (IsTrainingStandardSpiritualArtTarget(targetKind))
        {
            if (profile.TryGetProperty("standardArts", out var standardArts) &&
                standardArts.ValueKind == JsonValueKind.Object &&
                standardArts.TryGetProperty(targetId, out var tier) &&
                TryGetTrainingInt(tier, out var cap))
            {
                return cap;
            }

            AddTrainingIssue(
                $"{offerContext}.targetId",
                "training_showcase_missing_actor_art",
                "Наставник не имеет духовного искусства, указанного в offer.targetId.",
                "standardArts[targetId]",
                targetId,
                "Укажи targetId из standardArts наставника или добавь искусство в профиль.",
                issues);
            return 0;
        }

        if (IsTrainingSpiritFocusTarget(targetKind))
        {
            if (profile.TryGetProperty("mentorProfile", out var mentorProfile) &&
                mentorProfile.ValueKind == JsonValueKind.Object &&
                mentorProfile.TryGetProperty("spiritFocusTier", out var focusTier) &&
                TryGetTrainingInt(focusTier, out var cap))
            {
                return cap;
            }

            return -1;
        }

        if (IsTrainingSpecialSpiritualArtTarget(targetKind))
        {
            foreach (var specialArt in EnumerateTrainingSpecialArts(profile))
            {
                if (string.Equals(GetTrainingString(specialArt, "artId"), targetId, StringComparison.OrdinalIgnoreCase))
                    return GetTrainingInt(specialArt, "tier");
            }

            AddTrainingIssue(
                $"{offerContext}.targetId",
                "training_showcase_missing_actor_special_art",
                "Наставник не имеет особого духовного искусства, указанного в offer.targetId.",
                "specialArts[].artId",
                targetId,
                "Укажи artId из specialArts наставника.",
                issues);
            return 0;
        }

        return -1;
    }

    private static int ResolveMortalTeacherTrainingActorCap(
        JsonElement teacher,
        JsonElement offer,
        string offerContext,
        List<ValidationIssue> issues)
    {
        var targetId = GetTrainingString(offer, "targetId");
        var targetName = GetTrainingString(offer, "targetName");
        if (teacher.TryGetProperty("teacherProfile", out var teacherProfile) &&
            teacherProfile.ValueKind == JsonValueKind.Object &&
            teacherProfile.TryGetProperty("skills", out var skills) &&
            skills.ValueKind == JsonValueKind.Array)
        {
            foreach (var skill in skills.EnumerateArray())
            {
                var skillId = GetTrainingString(skill, "skillId");
                var skillName = GetTrainingString(skill, "skillName");
                if (!string.Equals(skillId, targetId, StringComparison.OrdinalIgnoreCase) &&
                    !string.Equals(skillName, targetName, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                return Math.Max(
                    GetTrainingInt(skill, "masteryLevel"),
                    Math.Max(
                        GetTrainingInt(skill, "currentMasteryLevel"),
                        GetTrainingInt(skill, "maxMasteryLevel")));
            }
        }

        AddTrainingIssue(
            $"{offerContext}.targetId",
            "training_showcase_missing_teacher_skill",
            "NPC-учитель не имеет навыка, указанного в offer.targetId/targetName.",
            "teacherProfile.skills[] contains target",
            targetId ?? targetName ?? "missing",
            "Синхронизируй offer с teacherProfile.skills.",
            issues);
        return 0;
    }

    private static IEnumerable<JsonElement> EnumerateTrainingSpecialArts(JsonElement profile)
    {
        if (profile.TryGetProperty("specialArts", out var specialArts) && specialArts.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in specialArts.EnumerateArray())
                if (item.ValueKind == JsonValueKind.Object)
                    yield return item;
        }

        if (profile.TryGetProperty("specialSpiritualArts", out var legacy) && legacy.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in legacy.EnumerateArray())
                if (item.ValueKind == JsonValueKind.Object)
                    yield return item;
        }
    }

    private static bool IsTrainingStandardSpiritualArtTarget(string targetKind) =>
        string.Equals(targetKind, "standard_spiritual_art", StringComparison.OrdinalIgnoreCase) ||
        string.Equals(targetKind, "spiritual_art", StringComparison.OrdinalIgnoreCase) ||
        string.Equals(targetKind, "spiritual_art_training", StringComparison.OrdinalIgnoreCase);

    private static bool IsTrainingSpiritFocusTarget(string targetKind) =>
        string.Equals(targetKind, "spirit_focus", StringComparison.OrdinalIgnoreCase) ||
        string.Equals(targetKind, "spirit_focus_training", StringComparison.OrdinalIgnoreCase);

    private static bool IsTrainingSpecialSpiritualArtTarget(string targetKind) =>
        string.Equals(targetKind, "special_spiritual_art", StringComparison.OrdinalIgnoreCase) ||
        string.Equals(targetKind, "special_spiritual_art_training", StringComparison.OrdinalIgnoreCase);

    private static string? ResolveAfterlifeTrainingActorId(JsonElement profile) =>
        GetTrainingString(profile, "actorId") ??
        GetTrainingString(profile, "guardianId") ??
        GetTrainingString(profile, "residentId") ??
        GetTrainingString(profile, "id");

    private static string? ResolveMortalTrainingActorId(JsonElement teacher) =>
        GetTrainingString(teacher, "npcId") ??
        GetTrainingString(teacher, "NPCId") ??
        GetTrainingString(teacher, "initialId") ??
        GetTrainingString(teacher, "initialNPCId") ??
        GetTrainingString(teacher, "id");

    private static string NormalizeTrainingRealm(string? realm)
    {
        if (string.IsNullOrWhiteSpace(realm))
            return string.Empty;

        var normalized = realm.Trim().Replace('-', '_').Replace(' ', '_').ToLowerInvariant();
        return normalized switch
        {
            "chaossea" => "chaos_sea",
            "chaos_sea" => "chaos_sea",
            "море_хаоса" => "chaos_sea",
            "shiningabode" => "shining_abode",
            "shining_abode" => "shining_abode",
            "сияющая_обитель" => "shining_abode",
            "mortalworld" => "mortal_world",
            "mortal_world" => "mortal_world",
            "mortal" => "mortal",
            "afterlife" => "afterlife",
            "posmertie" => "afterlife",
            "посмертие" => "afterlife",
            _ => normalized
        };
    }

    private static string? GetTrainingString(JsonElement element, string propertyName)
    {
        if (!element.TryGetProperty(propertyName, out var property))
            return null;
        return property.ValueKind == JsonValueKind.String
            ? property.GetString()
            : null;
    }

    private static int GetTrainingInt(JsonElement element, string propertyName)
    {
        if (!element.TryGetProperty(propertyName, out var property))
            return 0;
        return TryGetTrainingInt(property, out var value) ? value : 0;
    }

    private static bool TryGetTrainingInt(JsonElement element, out int value)
    {
        if (element.ValueKind == JsonValueKind.Number && element.TryGetInt32(out value))
            return true;
        if (element.ValueKind == JsonValueKind.String &&
            int.TryParse(element.GetString(), System.Globalization.NumberStyles.Integer, System.Globalization.CultureInfo.InvariantCulture, out value))
        {
            return true;
        }

        value = 0;
        return false;
    }

    private static bool TryGetTrainingBool(JsonElement element, out bool value)
    {
        if (element.ValueKind is JsonValueKind.True or JsonValueKind.False)
        {
            value = element.GetBoolean();
            return true;
        }

        if (element.ValueKind == JsonValueKind.String &&
            bool.TryParse(element.GetString(), out value))
        {
            return true;
        }

        value = false;
        return false;
    }

    private static void AddTrainingIssue(
        string filePath,
        string code,
        string message,
        string expected,
        string actual,
        string repairHint,
        List<ValidationIssue> issues) =>
        issues.Add(new ValidationIssue(
            filePath,
            IssueSeverity.Error,
            message,
            code: code,
            expected: expected,
            actual: actual,
            repairHint: repairHint,
            category: IssueCategory.StateConsistency));
}
