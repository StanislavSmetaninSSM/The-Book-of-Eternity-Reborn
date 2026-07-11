using BookOfEternityClient.CommandProtocol;
using BookOfEternityClient.Core;
using BookOfEternityClient.Services;
using Microsoft.Extensions.Logging.Abstractions;

namespace BookOfEternityClient.UI;

public static partial class ExplorerLifecycleLocalTurnCommandResultBuilder
{
    private static async Task<ExplorerCommandResult> BuildTrainingAsync(
        string command,
        FileSystemManager fs,
        StateManager stateManager)
    {
        var localTurn = BuildLocalTurnStatus(fs, playerFacing: true);
        var currentTurn = Math.Max(1, stateManager.CurrentState.TurnNumber);
        var trainingService = new TrainingService(fs, NullLogger<TrainingService>.Instance);
        var arguments = ReadCommandArguments(command);

        if (TrySplitLeadingArgument(arguments, out var operation, out var remaining))
        {
            if (IsTrainingBuyOperation(operation))
                return await BuildTrainingPurchaseAsync(command, localTurn, trainingService, currentTurn, remaining);

            if (IsTrainingTeacherOperation(operation))
                return await BuildTrainingTeacherDetailAsync(command, localTurn, trainingService, currentTurn, remaining);

            if (IsTrainingSelfOperation(operation))
                return await BuildTrainingSelfDetailAsync(command, localTurn, trainingService, currentTurn);

            return await BuildTrainingTeacherDetailAsync(command, localTurn, trainingService, currentTurn, arguments);
        }

        var view = await trainingService.EnsureTrainingAsync(currentTurn);
        var blocks = new List<UiBlock>
        {
            localTurn.Panel,
            BuildTrainingDossier(view, selectedTeacher: null, showOnlySelf: false)
        };

        if (ShouldDispatchTrainingPendingRequest(view))
        {
            return Result(
                command,
                CommandExecutionState.Pending,
                blocks,
                pendingGmAction: localTurn.HasActiveGmTurn ? null : view.PendingGmAction);
        }

        return Result(
            command,
            CommandExecutionState.Completed,
            blocks,
            actions: BuildTrainingActions(view));
    }

    private static async Task<ExplorerCommandResult> BuildTrainingTeacherDetailAsync(
        string command,
        LocalTurnStatus localTurn,
        TrainingService trainingService,
        int currentTurn,
        string selector)
    {
        var view = await trainingService.EnsureTrainingAsync(currentTurn, createPendingRequests: false);
        var teacher = FindTrainingTeacher(view, selector);
        if (teacher == null)
        {
            return Result(
                command,
                CommandExecutionState.Completed,
                [
                    localTurn.Panel,
                    Message(UiNotificationSeverity.Warning, "Наставник не найден", "Такого источника обучения сейчас нет в доступной витрине.")
                ],
                actions: [BackToTrainingAction()]);
        }

        return Result(
            command,
            CommandExecutionState.Completed,
            [
                localTurn.Panel,
                BuildTrainingDossier(view, teacher, showOnlySelf: false)
            ],
            actions: BuildTrainingActions(view, teacher));
    }

    private static async Task<ExplorerCommandResult> BuildTrainingSelfDetailAsync(
        string command,
        LocalTurnStatus localTurn,
        TrainingService trainingService,
        int currentTurn)
    {
        var view = await trainingService.EnsureTrainingAsync(currentTurn, createPendingRequests: false);
        return Result(
            command,
            CommandExecutionState.Completed,
            [
                localTurn.Panel,
                BuildTrainingDossier(view, selectedTeacher: null, showOnlySelf: true)
            ],
            actions: BuildTrainingActions(view, selectedTeacher: null, includeSelfOnly: true));
    }

    private static async Task<ExplorerCommandResult> BuildTrainingPurchaseAsync(
        string command,
        LocalTurnStatus localTurn,
        TrainingService trainingService,
        int currentTurn,
        string arguments)
    {
        if (localTurn.HasActiveGmTurn)
        {
            return Result(
                command,
                CommandExecutionState.Pending,
                [
                    localTurn.Panel,
                    Message(UiNotificationSeverity.Warning, "Обучение отложено", "Сейчас есть активный ход ГМ. Завершите его перед локальной покупкой обучения.")
                ]);
        }

        if (!TryReadTrainingPurchaseArguments(arguments, out var sourceActorId, out var offerId))
        {
            return Result(
                command,
                CommandExecutionState.Failed,
                [
                    localTurn.Panel,
                    Message(UiNotificationSeverity.Error, "Некорректная покупка", "Используйте формат /обучение купить <источник> <предложение>.")
                ],
                actions: [BackToTrainingAction()]);
        }

        var beforeView = await trainingService.EnsureTrainingAsync(currentTurn, createPendingRequests: false);
        var offer = FindTrainingOffer(beforeView, sourceActorId, offerId, out var sourceName);
        var result = await trainingService.BuyTrainingAsync(sourceActorId, offerId, currentTurn);
        var severity = result.Success ? UiNotificationSeverity.Success : UiNotificationSeverity.Warning;
        var state = result.Success
            ? IsTrainingAwaitingGmFinalization(result) ? CommandExecutionState.Pending : CommandExecutionState.Completed
            : CommandExecutionState.Blocked;
        var awaitingGmFinalization = IsTrainingAwaitingGmFinalization(result);
        var resultTitle = result.Success
            ? awaitingGmFinalization ? "Обучение оплачено" : "Обучение завершено"
            : "Обучение не выполнено";
        var blocks = new List<UiBlock>
        {
            localTurn.Panel,
            Message(severity, resultTitle, result.Message)
        };

        if (offer != null)
            blocks.Add(BuildTrainingPurchaseReceiptDossier(sourceName, offer, result));

        return Result(
            command,
            state,
            blocks,
            actions: state == CommandExecutionState.Pending ? [] : [BackToTrainingAction()],
            pendingGmAction: state == CommandExecutionState.Pending && !localTurn.HasActiveGmTurn ? result.PendingGmAction : null);
    }

    private static UiEntityDossierBlock BuildTrainingDossier(
        TrainingService.TrainingView view,
        TrainingService.TrainingTeacherView? selectedTeacher,
        bool showOnlySelf)
    {
        var isAfterlife = string.Equals(view.Realm, "afterlife", StringComparison.OrdinalIgnoreCase);
        var readyCount = view.Teachers.Count(static teacher => teacher.ShowcaseReady);
        var pendingCount = view.Teachers.Count - readyCount;
        if (view.RequestPending && pendingCount == 0)
            pendingCount = 1;
        var sections = new List<UiEntityDossierSection>();

        if (!showOnlySelf)
        {
            var teacherCards = selectedTeacher == null
                ? view.Teachers.Select(teacher => BuildTrainingTeacherCard(view, teacher, compact: false)).ToList()
                : [BuildTrainingTeacherCard(view, selectedTeacher, compact: false)];
            sections.Add(new UiEntityDossierSection
            {
                Id = isAfterlife ? "training-mentors" : "training-teachers",
                Title = isAfterlife ? "Наставники" : "Учителя",
                Summary = isAfterlife
                    ? "Наставники обучают дешевле самостоятельной прокачки, но не выше собственного уровня и вашего открытого предела."
                    : "Учителя обучают навыкам и мастерству за деньги и опыт текущего уровня.",
                Icon = "graduation-cap",
                Presentation = "collection",
                CollectionLabel = selectedTeacher == null
                    ? $"{view.Teachers.Count} источников обучения"
                    : selectedTeacher.SourceActorName,
                Collapsible = false,
                InitiallyExpanded = true,
                Cards = teacherCards
            });
        }

        if (isAfterlife && view.SelfTrainingOffers.Count > 0)
        {
            sections.Add(new UiEntityDossierSection
            {
                Id = "training-self-fallback",
                Title = "Самостоятельная прокачка",
                Summary = "Запасной путь для души: стандартные искусства стоят 400%, Средоточие Души 300%, известные особые искусства 500%. Новые особые искусства так не открываются.",
                Icon = "sparkles",
                Presentation = "collection",
                CollectionLabel = $"{view.SelfTrainingOffers.Count} предложений",
                Collapsible = view.SelfTrainingOffers.Count > 6,
                InitiallyExpanded = true,
                Cards = view.SelfTrainingOffers.Select(offer => BuildTrainingOfferCard("self", "Самостоятельная прокачка", offer)).ToList()
            });
        }

        var facts = new List<UiEntityFact>
        {
            new() { Label = "Режим", Value = isAfterlife ? "Посмертие" : "Смертный мир" },
            new() { Label = isAfterlife ? "Наставников" : "Учителей", Value = view.Teachers.Count.ToString() },
            new() { Label = "Готовых витрин", Value = readyCount.ToString() },
            new() { Label = "Ожидают ГМ", Value = pendingCount.ToString() }
        };
        if (isAfterlife)
            facts.Add(new UiEntityFact { Label = "Самостоятельных предложений", Value = view.SelfTrainingOffers.Count.ToString() });

        var hints = new List<UiEntityHint>();
        if (!string.IsNullOrWhiteSpace(view.ScopeUnavailableReason))
        {
            hints.Add(new UiEntityHint
            {
                Title = "Локальные наставники недоступны",
                Text = view.ScopeUnavailableReason,
                Tone = UiTone.Warning
            });
        }
        if (view.RequestPending)
        {
            hints.Add(new UiEntityHint
            {
                Title = "Нужна свежая витрина",
                Text = "ГМ уже получил запрос на подготовку или обновление источника обучения. Покупка из устаревшей витрины блокируется.",
                Tone = UiTone.Warning
            });
        }

        if (isAfterlife)
        {
            hints.Add(new UiEntityHint
            {
                Title = "Почему наставник выгоднее",
                Text = "Нейтральный наставник берёт 100% базовой цены, хороший 80%, отличный 60%. Самостоятельная прокачка намеренно кратно дороже.",
                Tone = UiTone.Accent
            });
        }

        return new UiEntityDossierBlock
        {
            EntityType = "training-showcase",
            Title = showOnlySelf ? "Самостоятельная прокачка души" : "Витрина обучения",
            Subtitle = isAfterlife ? "Духовные искусства и наставники" : "Навыки, учителя и мастерство",
            Summary = isAfterlife
                ? "Выберите наставника или запасной самостоятельный путь. Все цены и пределы проверяются клиентом перед покупкой."
                : "Выберите учителя и предложение. Покупка списывает деньги и опыт текущего уровня, но не может понизить уровень персонажа.",
            Badges =
            [
                new UiEntityBadge { Label = isAfterlife ? "Посмертие" : "Смертный мир", Tone = UiTone.Accent, Icon = isAfterlife ? "sparkles" : "graduation-cap" },
                new UiEntityBadge { Label = view.RequestPending ? "ожидает ГМ" : "готово к просмотру", Tone = view.RequestPending ? UiTone.Warning : UiTone.Success, Icon = "refresh-cw" }
            ],
            Facts = facts,
            Hints = hints,
            Sections = sections,
            PrimaryAction = showOnlySelf ? BackToTrainingAction() : null
        };
    }

    private static UiEntityCard BuildTrainingTeacherCard(
        TrainingService.TrainingView view,
        TrainingService.TrainingTeacherView teacher,
        bool compact)
    {
        var availableCount = teacher.Offers.Count(static offer => offer.Available);
        var isAfterlife = string.Equals(view.Realm, "afterlife", StringComparison.OrdinalIgnoreCase);
        var facts = new List<UiEntityFact>
        {
            new() { Label = "Источник", Value = teacher.SourceActorName },
            new() { Label = "Тип", Value = FormatTrainingSourceKind(teacher.SourceActorKind, isAfterlife) },
            new() { Label = "Витрина", Value = teacher.ShowcaseReady ? "свежая" : teacher.ShowcaseStale ? "устарела" : "ожидает ГМ" },
            new() { Label = "Доступно сейчас", Value = $"{availableCount} из {teacher.Offers.Count}" }
        };

        var hints = new List<UiEntityHint>();
        if (!teacher.ShowcaseReady)
        {
            hints.Add(new UiEntityHint
            {
                Title = "Витрина не готова",
                Text = teacher.BlockReason ?? "ГМ должен подготовить или обновить предложения обучения.",
                Tone = UiTone.Warning
            });
        }

        return new UiEntityCard
        {
            Title = teacher.SourceActorName,
            Subtitle = isAfterlife ? "Наставник" : "Учитель",
            Summary = teacher.ShowcaseReady
                ? $"{availableCount} предложений доступно для покупки сейчас."
                : teacher.BlockReason ?? "Витрина обучения ещё не подготовлена.",
            Icon = isAfterlife ? "sparkles" : "graduation-cap",
            Badges =
            [
                new UiEntityBadge
                {
                    Label = teacher.ShowcaseReady ? "готово" : teacher.ShowcaseStale ? "обновить" : "ожидает ГМ",
                    Tone = teacher.ShowcaseReady ? UiTone.Success : UiTone.Warning,
                    Icon = teacher.ShowcaseReady ? "check-circle" : "refresh-cw"
                }
            ],
            Facts = facts,
            Hints = hints,
            Nested = compact ? [] : teacher.Offers.Select(offer => BuildTrainingOfferCard(teacher.SourceActorId, teacher.SourceActorName, offer)).ToList(),
            PrimaryAction = new UiAction
            {
                Id = SoulRelicEquipmentService.BuildActionId("training-open-source", teacher.SourceActorId),
                Label = (isAfterlife ? "Открыть наставника: " : "Открыть учителя: ") + teacher.SourceActorName,
                Command = "/training teacher " + FormatTrainingCommandArgument(teacher.SourceActorId),
                Style = UiActionStyle.Secondary,
                RequiresConfirmation = false
            }
        };
    }

    private static UiEntityCard BuildTrainingOfferCard(string sourceActorId, string sourceActorName, TrainingService.TrainingOffer offer)
    {
        var facts = new List<UiEntityFact>
        {
            new() { Label = "Цель", Value = offer.TargetName },
            new() { Label = "Тип обучения", Value = FormatTrainingTargetKindForBrowser(offer.TargetKind) },
            new() { Label = "Текущий уровень", Value = offer.CurrentValue.ToString() },
            new() { Label = "После обучения", Value = offer.TargetValue.ToString() },
            new() { Label = "Предел учителя", Value = offer.SourceCap.ToString() }
        };
        AddTrainingCostFacts(facts, offer.Cost);

        AddOfferDetailFact(facts, offer, "minimumRelationship", "Требование");
        AddOfferDetailFact(facts, offer, "relationshipLevel", "Отношение");
        AddOfferDetailFact(facts, offer, "mentorPriceMultiplierPercent", "Цена наставника");
        AddOfferDetailFact(facts, offer, "baseInkFeatherCost", "Базовая цена в Перьях");
        AddOfferDetailFact(facts, offer, "baseLightSparkCost", "Базовая цена в Искрах");
        AddOfferDetailFact(facts, offer, "fallbackMultiplierPercent", "Множитель самостоятельной прокачки");

        var summary = FirstNonEmpty(
            GetTrainingOfferDetail(offer, "summary"),
            offer.Available
                ? "Предложение можно купить сейчас."
                : offer.BlockReason ?? "Предложение сейчас закрыто.");
        var hints = new List<UiEntityHint>();
        if (!offer.Available)
        {
            hints.Add(new UiEntityHint
            {
                Title = "Почему недоступно",
                Text = offer.BlockReason ?? "Условие обучения не выполнено.",
                Tone = UiTone.Warning
            });
        }

        return new UiEntityCard
        {
            Title = offer.TargetName,
            Subtitle = FormatTrainingTargetKindForBrowser(offer.TargetKind),
            Summary = summary,
            Icon = offer.TargetKind.Contains("spirit", StringComparison.OrdinalIgnoreCase) ? "sparkles" : "graduation-cap",
            Badges =
            [
                new UiEntityBadge
                {
                    Label = offer.Available ? "доступно" : "закрыто",
                    Tone = offer.Available ? UiTone.Success : UiTone.Warning,
                    Icon = offer.Available ? "check-circle" : "lock"
                }
            ],
            Facts = facts,
            Hints = hints,
            PrimaryAction = offer.Available
                ? new UiAction
                {
                    Id = SoulRelicEquipmentService.BuildActionId("training-buy", sourceActorId + "-" + offer.OfferId),
                    Label = $"Купить: {offer.TargetName}",
                    Command = "/training buy " + FormatTrainingCommandArgument(sourceActorId) + " " + FormatTrainingCommandArgument(offer.OfferId),
                    Style = UiActionStyle.Primary,
                    RequiresConfirmation = true
                }
                : null
        };
    }

    private static UiEntityDossierBlock BuildTrainingPurchaseReceiptDossier(
        string sourceName,
        TrainingService.TrainingOffer offer,
        TrainingService.TrainingOperationResult result)
    {
        return new UiEntityDossierBlock
        {
            EntityType = "training-receipt",
            Title = result.Success
                ? IsTrainingAwaitingGmFinalization(result) ? "Обучение оплачено" : "Обучение завершено"
                : "Обучение не выполнено",
            Subtitle = offer.TargetName,
            Summary = result.Message,
            Badges =
            [
                new UiEntityBadge
                {
                    Label = result.Success
                        ? IsTrainingAwaitingGmFinalization(result) ? "ожидает ГМ" : "успешно"
                        : "заблокировано",
                    Tone = result.Success ? UiTone.Success : UiTone.Warning,
                    Icon = result.Success ? "check-circle" : "alert-triangle"
                }
            ],
            Facts = BuildTrainingPurchaseReceiptFacts(sourceName, offer)
        };
    }

    private static bool IsTrainingAwaitingGmFinalization(TrainingService.TrainingOperationResult result) =>
        result.Success &&
        (!string.IsNullOrWhiteSpace(result.PendingGmAction) ||
         result.Message.Contains("ожидает ГМ", StringComparison.OrdinalIgnoreCase));

    private static List<UiEntityFact> BuildTrainingPurchaseReceiptFacts(
        string sourceName,
        TrainingService.TrainingOffer offer)
    {
        var facts = new List<UiEntityFact>
        {
            new() { Label = "Источник", Value = sourceName },
            new() { Label = "Цель", Value = offer.TargetName },
            new() { Label = "Тип обучения", Value = FormatTrainingTargetKindForBrowser(offer.TargetKind) },
            new() { Label = "До обучения", Value = offer.CurrentValue.ToString() },
            new() { Label = "После обучения", Value = offer.TargetValue.ToString() }
        };
        AddTrainingCostFacts(facts, offer.Cost);
        return facts;
    }

    private static IEnumerable<UiAction> BuildTrainingActions(
        TrainingService.TrainingView view,
        TrainingService.TrainingTeacherView? selectedTeacher = null,
        bool includeSelfOnly = false)
    {
        if (selectedTeacher != null || includeSelfOnly)
            yield return BackToTrainingAction();

        if (!includeSelfOnly)
        {
            foreach (var teacher in view.Teachers)
            {
                yield return new UiAction
                {
                    Id = SoulRelicEquipmentService.BuildActionId("training-open-source", teacher.SourceActorId),
                    Label = (string.Equals(view.Realm, "afterlife", StringComparison.OrdinalIgnoreCase) ? "Наставник: " : "Учитель: ") + teacher.SourceActorName,
                    Command = "/training teacher " + FormatTrainingCommandArgument(teacher.SourceActorId),
                    Style = UiActionStyle.Secondary,
                    RequiresConfirmation = false
                };

                foreach (var offer in teacher.Offers.Where(static offer => offer.Available))
                    yield return BuildTrainingBuyAction(teacher.SourceActorId, offer);
            }
        }

        if (string.Equals(view.Realm, "afterlife", StringComparison.OrdinalIgnoreCase) && !includeSelfOnly)
        {
            yield return new UiAction
            {
                Id = "training-open-self-fallback",
                Label = "Самостоятельная прокачка",
                Command = "/training self",
                Style = UiActionStyle.Secondary,
                RequiresConfirmation = false
            };
        }

        foreach (var offer in view.SelfTrainingOffers.Where(static offer => offer.Available))
            yield return BuildTrainingBuyAction("self", offer);
    }

    private static UiAction BuildTrainingBuyAction(string sourceActorId, TrainingService.TrainingOffer offer) =>
        new()
        {
            Id = SoulRelicEquipmentService.BuildActionId("training-buy", sourceActorId + "-" + offer.OfferId),
            Label = $"Купить: {offer.TargetName}",
            Command = "/training buy " + FormatTrainingCommandArgument(sourceActorId) + " " + FormatTrainingCommandArgument(offer.OfferId),
            Style = UiActionStyle.Primary,
            RequiresConfirmation = true
        };

    private static UiAction BackToTrainingAction() =>
        new()
        {
            Id = "training-back",
            Label = "← К витрине обучения",
            Command = "/training",
            Style = UiActionStyle.Secondary,
            RequiresConfirmation = false
        };

    private static bool ShouldDispatchTrainingPendingRequest(TrainingService.TrainingView view) =>
        !string.IsNullOrWhiteSpace(view.PendingGmAction) &&
        view.RequestPending;

    private static TrainingService.TrainingTeacherView? FindTrainingTeacher(
        TrainingService.TrainingView view,
        string selector)
    {
        var normalized = selector.Trim().Trim('"');
        if (string.IsNullOrWhiteSpace(normalized))
            return null;

        return view.Teachers.FirstOrDefault(teacher =>
            string.Equals(teacher.SourceActorId, normalized, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(teacher.SourceActorName, normalized, StringComparison.OrdinalIgnoreCase));
    }

    private static TrainingService.TrainingOffer? FindTrainingOffer(
        TrainingService.TrainingView view,
        string sourceActorId,
        string offerId,
        out string sourceName)
    {
        sourceName = sourceActorId;
        if (string.Equals(sourceActorId, "self", StringComparison.OrdinalIgnoreCase))
        {
            sourceName = "Самостоятельная прокачка";
            return view.SelfTrainingOffers.FirstOrDefault(offer => string.Equals(offer.OfferId, offerId, StringComparison.OrdinalIgnoreCase));
        }

        var teacher = FindTrainingTeacher(view, sourceActorId);
        if (teacher == null)
            return null;

        sourceName = teacher.SourceActorName;
        return teacher.Offers.FirstOrDefault(offer => string.Equals(offer.OfferId, offerId, StringComparison.OrdinalIgnoreCase));
    }

    private static bool TryReadTrainingPurchaseArguments(string arguments, out string sourceActorId, out string offerId)
    {
        sourceActorId = string.Empty;
        offerId = string.Empty;
        if (!TrySplitLeadingArgument(arguments, out sourceActorId, out var remaining))
            return false;
        if (!TrySplitLeadingArgument(remaining, out offerId, out _))
            return false;
        return !string.IsNullOrWhiteSpace(sourceActorId) && !string.IsNullOrWhiteSpace(offerId);
    }

    private static bool IsTrainingBuyOperation(string value) =>
        string.Equals(value, "buy", StringComparison.OrdinalIgnoreCase) ||
        string.Equals(value, "купить", StringComparison.OrdinalIgnoreCase);

    private static bool IsTrainingTeacherOperation(string value) =>
        string.Equals(value, "teacher", StringComparison.OrdinalIgnoreCase) ||
        string.Equals(value, "source", StringComparison.OrdinalIgnoreCase) ||
        string.Equals(value, "mentor", StringComparison.OrdinalIgnoreCase) ||
        string.Equals(value, "учитель", StringComparison.OrdinalIgnoreCase) ||
        string.Equals(value, "наставник", StringComparison.OrdinalIgnoreCase);

    private static bool IsTrainingSelfOperation(string value) =>
        string.Equals(value, "self", StringComparison.OrdinalIgnoreCase) ||
        string.Equals(value, "самостоятельно", StringComparison.OrdinalIgnoreCase) ||
        string.Equals(value, "сам", StringComparison.OrdinalIgnoreCase);

    private static void AddOfferDetailFact(List<UiEntityFact> facts, TrainingService.TrainingOffer offer, string detailKey, string label)
    {
        var value = GetTrainingOfferDetail(offer, detailKey);
        if (string.IsNullOrWhiteSpace(value))
            return;

        if (detailKey == "minimumRelationship")
            value = $"отношение не ниже {value}";
        else if (detailKey is "mentorPriceMultiplierPercent" or "fallbackMultiplierPercent")
            value += "%";

        facts.Add(new UiEntityFact { Label = label, Value = value });
    }

    private static void AddTrainingCostFacts(List<UiEntityFact> facts, TrainingService.TrainingCost cost)
    {
        var costFacts = BuildTrainingCostFacts(cost).ToArray();
        if (costFacts.Length == 0)
        {
            facts.Add(new UiEntityFact { Label = "Стоимость", Value = "цена не указана" });
            return;
        }

        facts.AddRange(costFacts);
    }

    private static IEnumerable<UiEntityFact> BuildTrainingCostFacts(TrainingService.TrainingCost cost)
    {
        if (cost.Money > 0)
            yield return new UiEntityFact { Label = "Деньги", Value = cost.Money.ToString() };
        if (cost.CurrentLevelExperiencePoints > 0 || cost.CurrentLevelExperiencePercent > 0)
            yield return new UiEntityFact
            {
                Label = "Опыт текущего уровня",
                Value = $"{cost.CurrentLevelExperiencePoints} ({cost.CurrentLevelExperiencePercent}%)"
            };
        if (cost.InkFeathers > 0)
            yield return new UiEntityFact { Label = "Чернильные Перья", Value = cost.InkFeathers.ToString() };
        if (cost.LightSparks > 0)
            yield return new UiEntityFact { Label = "Искры Света", Value = cost.LightSparks.ToString() };
    }

    private static string GetTrainingOfferDetail(TrainingService.TrainingOffer offer, string key)
    {
        if (!offer.Details.TryGetPropertyValue(key, out var node) || node == null)
        {
            if (offer.Details["requirements"] is System.Text.Json.Nodes.JsonObject requirements &&
                requirements.TryGetPropertyValue(key, out var requirementNode))
            {
                node = requirementNode;
            }
            else
            {
                return string.Empty;
            }
        }

        return node switch
        {
            System.Text.Json.Nodes.JsonValue value when value.TryGetValue<string>(out var text) => text ?? string.Empty,
            System.Text.Json.Nodes.JsonValue value when value.TryGetValue<int>(out var number) => number.ToString(),
            System.Text.Json.Nodes.JsonValue value when value.TryGetValue<bool>(out var flag) => flag ? "да" : "нет",
            _ => string.Empty
        };
    }

    private static string FormatTrainingSourceKind(string sourceKind, bool afterlife) =>
        sourceKind switch
        {
            "npc_teacher" => "NPC-учитель",
            "afterlife_mentor" => afterlife ? "наставник посмертия" : "наставник",
            "self_fallback" => "самостоятельная прокачка",
            _ => afterlife ? "наставник" : "учитель"
        };

    private static string FormatTrainingTargetKindForBrowser(string kind) =>
        kind switch
        {
            "active_skill_mastery" => "Активный навык: мастерство",
            "passive_skill_mastery" => "Пассивный навык: мастерство",
            "active_skill_unlock" => "Новый активный навык",
            "passive_skill_unlock" => "Новый пассивный навык",
            "standard_spiritual_art" or "spiritual_art" or "spiritual_art_training" => "Духовное искусство",
            "spiritual_art_self_training" => "Духовное искусство: самостоятельная прокачка",
            "spirit_focus" or "spirit_focus_training" => "Средоточие Души",
            "spirit_focus_self_training" => "Средоточие Души: самостоятельная прокачка",
            "special_spiritual_art" or "special_spiritual_art_training" => "Особое духовное искусство",
            "special_spiritual_art_self_training" => "Особое духовное искусство: самостоятельная прокачка",
            _ => kind.Replace('_', ' ')
        };

    private static string FormatTrainingCommandArgument(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return "\"\"";

        return value.Contains(' ', StringComparison.Ordinal) || value.Contains('"', StringComparison.Ordinal)
            ? "\"" + value.Replace("\\", "\\\\", StringComparison.Ordinal).Replace("\"", "\\\"", StringComparison.Ordinal) + "\""
            : value;
    }
}
