using System.Text.Json;
using System.Text.Json.Nodes;
using BookOfEternityClient.Configuration;
using BookOfEternityClient.Services;
using Spectre.Console;

namespace BookOfEternityClient.UI;

public partial class ExplorerMode
{
    private sealed record ShiningActorChoice(string Label, string ActorType, string ActorId);

    private async Task HandleShiningFoundingRequestAsync(ShiningContext context)
    {
        if (!EnsureActiveShiningAbodeAvailable("Политика Сияющей Обители"))
            return;

        var feathers = await ReadInkFeathersBalance();
        var cost = new
        {
            Feathers = ShiningFactionRequestState.FactionFoundingCostFeathers,
            LightSparks = ShiningFactionRequestState.FactionFoundingCostLightSparks
        };
        if (feathers < cost.Feathers)
        {
            MarkupLine($"[red]Недостаточно Перьев. Нужно {cost.Feathers}.[/]");
            WaitForKey();
            return;
        }

        if (GetNodeInt(context.Root["lightSparks"]) < cost.LightSparks)
        {
            MarkupLine($"[red]Недостаточно Искр Света. Нужно {cost.LightSparks}.[/]");
            WaitForKey();
            return;
        }

        var factionName = Ask("[cyan]Название новой фракции:[/]", "").Trim();
        if (string.IsNullOrWhiteSpace(factionName))
            return;

        var hallName = Ask("[cyan]Название нового зала:[/]", $"Зал {factionName}").Trim();
        if (string.IsNullOrWhiteSpace(hallName))
            return;

        var summary = Ask("[cyan]Краткая сводка устава:[/]", "").Trim();
        if (string.IsNullOrWhiteSpace(summary))
            return;

        var hallDescription = PromptLargeTextBlock("Описание нового зала", "");
        if (string.IsNullOrWhiteSpace(hallDescription))
            return;

        var favoredArchetypeChoices = new[]
        {
            (Value: ShiningAbodeState.ProjectArchetypeRevelation, Label: DescribeShiningProjectArchetype(ShiningAbodeState.ProjectArchetypeRevelation)),
            (Value: ShiningAbodeState.ProjectArchetypeAccord, Label: DescribeShiningProjectArchetype(ShiningAbodeState.ProjectArchetypeAccord)),
            (Value: ShiningAbodeState.ProjectArchetypeProvision, Label: DescribeShiningProjectArchetype(ShiningAbodeState.ProjectArchetypeProvision)),
            (Value: ShiningAbodeState.ProjectArchetypeRemembrance, Label: DescribeShiningProjectArchetype(ShiningAbodeState.ProjectArchetypeRemembrance)),
            (Value: ShiningAbodeState.ProjectArchetypeRefinement, Label: DescribeShiningProjectArchetype(ShiningAbodeState.ProjectArchetypeRefinement)),
            (Value: ShiningAbodeState.ProjectArchetypePassage, Label: DescribeShiningProjectArchetype(ShiningAbodeState.ProjectArchetypePassage)),
            (Value: ShiningAbodeState.ProjectArchetypeWarding, Label: DescribeShiningProjectArchetype(ShiningAbodeState.ProjectArchetypeWarding)),
            (Value: ShiningAbodeState.ProjectArchetypeSubversion, Label: DescribeShiningProjectArchetype(ShiningAbodeState.ProjectArchetypeSubversion))
        };
        var favoredArchetypeLabel = Prompt(new SelectionPrompt<string>()
            .Title("[bold yellow]Предпочитаемый архетип[/]")
            .HighlightStyle(new Style(Color.Gold1))
            .AddChoices(favoredArchetypeChoices.Select(choice => choice.Label)));
        var favoredArchetype = favoredArchetypeChoices.First(choice => choice.Label == favoredArchetypeLabel).Value;

        var patronEffectChoices = new[]
        {
            ShiningAbodeState.EffectFamilyLore,
            ShiningAbodeState.EffectFamilySocial,
            ShiningAbodeState.EffectFamilyResource,
            ShiningAbodeState.EffectFamilyMemory,
            ShiningAbodeState.EffectFamilyDescent,
            ShiningAbodeState.EffectFamilySurvival,
            ShiningAbodeState.EffectFamilyRelic,
            ShiningAbodeState.EffectFamilyRoute
        }.Select(family => (Value: family, Label: DescribeShiningEffectFamily(family))).ToList();
        var patronEffectLabel = Prompt(new SelectionPrompt<string>()
            .Title("[bold yellow]Семейство покровительствующего эффекта[/]")
            .HighlightStyle(new Style(Color.Gold1))
            .AddChoices(patronEffectChoices.Select(choice => choice.Label)));
        var patronEffectFamily = patronEffectChoices.First(choice => choice.Label == patronEffectLabel).Value;

        var serviceTags = PromptFoundingHallServiceTags(patronEffectFamily);
        if (serviceTags.Count == 0)
            return;

        var supporterIds = PromptShiningSupporterIds(
            context.ResidentRoot,
            "[bold yellow]Выберите минимум 3 вознесённых сторонника[/]",
            null);
        if (supporterIds.Count < 3)
        {
            MarkupLine("[yellow]Нужно выбрать минимум 3 сторонника.[/]");
            WaitForKey();
            return;
        }

        var slug = Slugify(factionName);
        var request = new ShiningFactionRequestState.PendingShiningFactionFoundingRequest
        {
            ProposedFactionId = $"faction_{slug}",
            ProposedHallId = $"hall_{slug}",
            ProposedHallName = hallName,
            ProposedHallDescription = hallDescription.Trim(),
            ProposedHallServiceTags = serviceTags,
            Charter = new ShiningFactionRequestState.FactionCharterPayload
            {
                FactionName = factionName,
                FavoredArchetype = favoredArchetype,
                PatronEffectFamily = patronEffectFamily,
                Summary = summary
            },
            SupportingResidentIds = supporterIds,
            QuotedCostFeathers = cost.Feathers,
            QuotedCostLightSparks = cost.LightSparks,
            CreatedAtTurn = _stateManager.CurrentState.TurnNumber + 1
        };

        var error = await ShiningFactionRequestState.ValidateFoundingRequestAgainstCurrentStateAsync(_fs, request);
        if (!string.IsNullOrWhiteSpace(error))
        {
            MarkupLine($"[yellow]{Markup.Escape(error)}[/]");
            WaitForKey();
            return;
        }

        if (!ConfirmShiningPoliticalRequestPreview(
                "Подтвердить основание сияющей фракции",
                ShiningFactionRequestState.PendingFoundingsRequestPath,
                SerializePoliticalRequestForPreview(request),
                BuildShiningFoundingRequestPreviewLines(context, request, feathers, cost.Feathers, cost.LightSparks)))
        {
            return;
        }

        await EnsurePendingLocalTurnRollbackSnapshotAsync(
            "game_state/meta/soul_state.json",
            ShiningAbodeState.StatePath,
            ShiningFactionRequestState.PendingFoundingsRequestPath);

        try
        {
            await ShiningFactionRequestState.WriteFoundingRequestAsync(_fs, request);
            if (!await DeductInkFeathers(cost.Feathers))
            {
                await RestorePendingLocalTurnRollbackSnapshotAsync();
                MarkupLine("[red]Не удалось списать Перья.[/]");
                WaitForKey();
                return;
            }

            context.Root["lightSparks"] = Math.Max(0, GetNodeInt(context.Root["lightSparks"]) - cost.LightSparks);
            await SaveShiningRootAsync(context.Root);
        }
        catch
        {
            await RestorePendingLocalTurnRollbackSnapshotAsync();
            throw;
        }

        MarkupLine($"[green]Создан ожидающий запрос на основание сияющей фракции. Зарезервировано {cost.Feathers} Перьев и {cost.LightSparks} Искр Света; эти суммы записаны в pending contract.[/]");
        WaitForKey();
    }

    private async Task HandleShiningRealignmentRequestAsync(ShiningContext context)
    {
        if (!EnsureActiveShiningAbodeAvailable("Политика Сияющей Обители"))
            return;

        var resident = PromptForShiningResidentReadyToRealign(context.ResidentRoot);
        if (resident == null)
            return;

        var residentId = GetNodeString(resident["residentId"]) ?? string.Empty;
        var residentName = GetNodeString(resident["displayName"]) ?? residentId;
        var sourceFactionId = GetNodeString(resident["shiningFactionId"]) ?? string.Empty;
        var sourceFaction = FindShiningFactionNode(context.Root, sourceFactionId);
        var sourceFactionName = GetNodeString(sourceFaction?["charter"]?["factionName"]) ?? sourceFactionId;

        var modeChoice = Prompt(new SelectionPrompt<string>()
            .Title("[bold yellow]Режим перестройки[/]")
            .HighlightStyle(new Style(Color.Gold1))
            .AddChoices("Перейти в другую фракцию", "Уйти в нейтральное состояние", "← Назад"));
        if (modeChoice.Contains("Назад", StringComparison.Ordinal))
            return;

        var request = new ShiningFactionRequestState.PendingShiningFactionRealignmentRequest
        {
            ResidentId = residentId,
            ResidentName = residentName,
            SourceFactionId = sourceFactionId,
            SourceFactionName = sourceFactionName,
            FactionLoyaltyLevel = GetNodeInt(resident["factionLoyaltyLevel"]),
            FactionLoyaltyTier = GetNodeString(resident["factionLoyaltyTier"]) ?? ShiningAbodeState.FactionLoyaltyTierAlienated,
            FactionRestlessness = GetNodeInt(resident["factionRestlessness"]),
            FactionRealignmentState = GetNodeString(resident["factionRealignmentState"]) ?? ShiningAbodeState.FactionRealignmentStateReadyToRealign,
            CreatedAtTurn = _stateManager.CurrentState.TurnNumber + 1
        };

        if (modeChoice.Contains("нейтраль", StringComparison.OrdinalIgnoreCase))
        {
            request.RealignmentMode = ShiningFactionRequestState.RealignmentModeDepartureToNeutral;
        }
        else
        {
            var targetFaction = PromptForShiningFactionTarget(context.Root, sourceFactionId, "Выберите целевую фракцию");
            if (targetFaction == null)
                return;

            request.RealignmentMode = ShiningFactionRequestState.RealignmentModeAcceptedTransfer;
            request.TargetFactionId = GetNodeString(targetFaction["factionId"]) ?? string.Empty;
            request.TargetFactionName = GetNodeString(targetFaction["charter"]?["factionName"]) ?? request.TargetFactionId;
        }

        var error = await ShiningFactionRequestState.ValidateRealignmentRequestAgainstCurrentStateAsync(_fs, request);
        if (!string.IsNullOrWhiteSpace(error))
        {
            MarkupLine($"[yellow]{Markup.Escape(error)}[/]");
            WaitForKey();
            return;
        }

        if (!ConfirmShiningPoliticalRequestPreview(
                "Подтвердить перестройку резидента",
                ShiningFactionRequestState.PendingRealignmentsRequestPath,
                SerializePoliticalRequestForPreview(request),
                BuildShiningRealignmentRequestPreviewLines(context, request)))
        {
            return;
        }

        await ShiningFactionRequestState.WriteRealignmentRequestAsync(_fs, request);
        MarkupLine("[green]Создан ожидающий запрос на перестройку резидента.[/]");
        WaitForKey();
    }

    private async Task HandleShiningLeadershipTransitionRequestAsync(ShiningContext context)
    {
        if (!EnsureActiveShiningAbodeAvailable("Политика Сияющей Обители"))
            return;

        var faction = PromptForFaction(context.Root, "Выберите фракцию для смены главы");
        if (faction == null)
            return;

        var factionId = GetNodeString(faction["factionId"]) ?? string.Empty;
        var factionName = GetNodeString(faction["charter"]?["factionName"]) ?? factionId;
        var leadership = faction["leadership"] as JsonObject ?? new JsonObject();

        var transitionChoices = new[]
        {
            (Value: ShiningFactionRequestState.TransitionModeAbdication, Label: DescribeShiningLeadershipMode(ShiningFactionRequestState.TransitionModeAbdication)),
            (Value: ShiningFactionRequestState.TransitionModePeacefulSuccession, Label: DescribeShiningLeadershipMode(ShiningFactionRequestState.TransitionModePeacefulSuccession)),
            (Value: ShiningFactionRequestState.TransitionModeRevolt, Label: DescribeShiningLeadershipMode(ShiningFactionRequestState.TransitionModeRevolt))
        };
        var modeLabel = Prompt(new SelectionPrompt<string>()
            .Title("[bold yellow]Режим смены главы[/]")
            .HighlightStyle(new Style(Color.Gold1))
            .AddChoices(transitionChoices.Select(choice => choice.Label).Append("← Назад")));
        if (modeLabel.Contains("Назад", StringComparison.Ordinal))
            return;
        var mode = transitionChoices.First(choice => choice.Label == modeLabel).Value;

        ShiningActorChoice? candidate = null;
        if (!string.Equals(mode, ShiningFactionRequestState.TransitionModeAbdication, StringComparison.OrdinalIgnoreCase) ||
            _console.Confirm("[yellow]Указать преемника вместо вакантного состояния?[/]", false))
        {
            candidate = PromptShiningLeadershipCandidate(context, factionId);
            if (candidate == null)
                return;
        }

        var supporterIds = string.Equals(mode, ShiningFactionRequestState.TransitionModeAbdication, StringComparison.OrdinalIgnoreCase)
            ? new List<string>()
            : PromptShiningSupporterIds(
                context.ResidentRoot,
                "[bold yellow]Выберите вознесённых сторонников из той же фракции[/]",
                factionId);

        var request = new ShiningFactionRequestState.PendingShiningFactionLeadershipTransitionRequest
        {
            FactionId = factionId,
            FactionName = factionName,
            TransitionMode = mode,
            IncumbentHeadActorType = GetNodeString(leadership["headActorType"]) ?? string.Empty,
            IncumbentHeadActorId = GetNodeString(leadership["headActorId"]) ?? string.Empty,
            CandidateHeadActorType = candidate?.ActorType ?? string.Empty,
            CandidateHeadActorId = candidate?.ActorId ?? string.Empty,
            SupportingResidentIds = supporterIds,
            CreatedAtTurn = _stateManager.CurrentState.TurnNumber + 1
        };

        var error = await ShiningFactionRequestState.ValidateLeadershipTransitionRequestAgainstCurrentStateAsync(_fs, request);
        if (!string.IsNullOrWhiteSpace(error))
        {
            MarkupLine($"[yellow]{Markup.Escape(error)}[/]");
            WaitForKey();
            return;
        }

        if (!ConfirmShiningPoliticalRequestPreview(
                "Подтвердить смену главы фракции",
                ShiningFactionRequestState.PendingLeadershipTransitionsRequestPath,
                SerializePoliticalRequestForPreview(request),
                BuildShiningLeadershipRequestPreviewLines(context, request)))
        {
            return;
        }

        await ShiningFactionRequestState.WriteLeadershipTransitionRequestAsync(_fs, request);
        MarkupLine("[green]Создан ожидающий запрос на смену главы фракции.[/]");
        WaitForKey();
    }

    private List<string> PromptFoundingHallServiceTags(string patronEffectFamily)
    {
        var requiredPrimary = MapPatronFamilyToHallServiceTag(patronEffectFamily);
        var options = new[]
        {
            ShiningAbodeState.HallServiceTagSocial,
            ShiningAbodeState.HallServiceTagLore,
            ShiningAbodeState.HallServiceTagResource,
            ShiningAbodeState.HallServiceTagMemory,
            ShiningAbodeState.HallServiceTagDescent,
            ShiningAbodeState.HallServiceTagRelic
        }.Where(tag => !string.Equals(tag, requiredPrimary, StringComparison.OrdinalIgnoreCase)).ToList();
        var optionLabels = options
            .Select(tag => (Tag: tag, Label: DescribeShiningHallServiceTag(tag)))
            .ToList();
        const string noSecondaryLabel = "без второго тега";

        var secondary = Prompt(new SelectionPrompt<string>()
            .Title($"[bold yellow]Дополнительная служба зала[/]\n[dim]Обязательная основная служба уже зафиксирована: {DescribeShiningHallServiceTag(requiredPrimary)}[/]")
            .HighlightStyle(new Style(Color.Gold1))
            .AddChoices(optionLabels.Select(option => option.Label).Append(noSecondaryLabel)));

        var tags = new List<string> { requiredPrimary };
        if (!secondary.Contains("без", StringComparison.OrdinalIgnoreCase))
            tags.Add(optionLabels.First(option => string.Equals(option.Label, secondary, StringComparison.Ordinal)).Tag);
        return tags;
    }

    private JsonObject? PromptForShiningResidentReadyToRealign(JsonObject? residentRoot)
    {
        if (residentRoot?["entries"] is not JsonArray entries)
        {
            MarkupLine("[yellow]В списке обитателей нет записей для сияющей перестройки.[/]");
            WaitForKey();
            return null;
        }

        var choices = entries.OfType<JsonObject>()
            .Where(entry =>
                string.Equals(GetNodeString(entry["ascensionState"]), ShiningAbodeState.AscensionStateAscended, StringComparison.OrdinalIgnoreCase) &&
                string.Equals(GetNodeString(entry["factionRealignmentState"]), ShiningAbodeState.FactionRealignmentStateReadyToRealign, StringComparison.OrdinalIgnoreCase))
            .Select(entry =>
            {
                var residentId = GetNodeString(entry["residentId"]) ?? string.Empty;
                var displayName = GetNodeString(entry["displayName"]) ?? residentId;
                var factionId = GetNodeString(entry["shiningFactionId"]) ?? "none";
                var factionLabel = GetNodeString(entry["shiningFactionName"]) ?? factionId;
                var label = $"{displayName} [dim](фракция {factionLabel}, лояльность {GetNodeInt(entry["factionLoyaltyLevel"])}, брожение {GetNodeInt(entry["factionRestlessness"])})[/]";
                return (Label: label, Entry: entry);
            })
            .ToList();

        if (choices.Count == 0)
        {
            MarkupLine("[yellow]Сейчас нет обитателей в состоянии готовности к перестройке.[/]");
            WaitForKey();
            return null;
        }

        var selected = Prompt(new SelectionPrompt<string>()
            .Title("[bold yellow]Выберите обитателя для сияющей перестройки[/]")
            .HighlightStyle(new Style(Color.Gold1))
            .AddChoices(choices.Select(item => item.Label).Append("← Назад")));
        if (selected.Contains("Назад", StringComparison.Ordinal))
            return null;

        return choices.First(item => item.Label == selected).Entry;
    }

    private JsonObject? PromptForShiningFactionTarget(JsonObject shiningRoot, string excludedFactionId, string title)
    {
        if (shiningRoot["factions"] is not JsonArray factions)
            return null;

        var choices = factions.OfType<JsonObject>()
            .Where(faction => !string.Equals(GetNodeString(faction["factionId"]), excludedFactionId, StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(faction => GetNodeInt(faction["factionStrength"]))
            .Select(faction =>
            {
                var factionId = GetNodeString(faction["factionId"]) ?? string.Empty;
                var factionName = GetNodeString(faction["charter"]?["factionName"]) ?? factionId;
                var label = $"{factionName} [dim](сила {GetNodeInt(faction["factionStrength"])})[/]";
                return (Label: label, Faction: faction);
            })
            .ToList();

        if (choices.Count == 0)
        {
            MarkupLine("[yellow]Нет доступных целевых фракций.[/]");
            WaitForKey();
            return null;
        }

        var selected = Prompt(new SelectionPrompt<string>()
            .Title($"[bold yellow]{Markup.Escape(title)}[/]")
            .HighlightStyle(new Style(Color.Gold1))
            .AddChoices(choices.Select(item => item.Label).Append("← Назад")));
        if (selected.Contains("Назад", StringComparison.Ordinal))
            return null;

        return choices.First(item => item.Label == selected).Faction;
    }

    private List<string> PromptShiningSupporterIds(JsonObject? residentRoot, string title, string? sameFactionId)
    {
        var selected = new List<string>();
        while (true)
        {
            var available = BuildEligibleSupporterChoices(residentRoot, sameFactionId, selected);
            var summary = selected.Count == 0
                ? "[dim]Пока сторонники не выбраны.[/]"
                : $"[dim]Выбрано: {Markup.Escape(BuildSelectedSupporterSummary(residentRoot, selected))}[/]";
            var choice = Prompt(new SelectionPrompt<string>()
                .Title($"{title}\n{summary}")
                .HighlightStyle(new Style(Color.Gold1))
                .AddChoices(available.Select(item => item.Label).Append(selected.Count == 0 ? "← Отмена" : "✅ Готово")));

            if (choice.Contains("Отмена", StringComparison.Ordinal) || choice.Contains("Готово", StringComparison.Ordinal))
                return selected;

            var picked = available.First(item => item.Label == choice);
            selected.Add(picked.ResidentId);
        }
    }

    private List<(string Label, string ResidentId)> BuildEligibleSupporterChoices(JsonObject? residentRoot, string? sameFactionId, IReadOnlyCollection<string> alreadySelected)
    {
        if (residentRoot?["entries"] is not JsonArray entries)
            return new List<(string Label, string ResidentId)>();

        return entries.OfType<JsonObject>()
            .Where(entry =>
                string.Equals(GetNodeString(entry["ascensionState"]), ShiningAbodeState.AscensionStateAscended, StringComparison.OrdinalIgnoreCase) &&
                (string.IsNullOrWhiteSpace(sameFactionId) ||
                 string.Equals(GetNodeString(entry["shiningFactionId"]), sameFactionId, StringComparison.OrdinalIgnoreCase)))
            .Select(entry =>
            {
                var residentId = GetNodeString(entry["residentId"]) ?? string.Empty;
                var label = BuildShiningResidentPoliticalChoiceLabel(entry);
                return (Label: label, ResidentId: residentId);
            })
            .Where(item => !alreadySelected.Any(selected => string.Equals(selected, item.ResidentId, StringComparison.OrdinalIgnoreCase)))
            .ToList();
    }

    private ShiningActorChoice? PromptShiningLeadershipCandidate(ShiningContext context, string factionId)
    {
        var choices = new List<ShiningActorChoice>
        {
            new("Душа игрока [dim](игрок)[/]", ShiningAbodeState.HeadActorTypePlayerSoul, ShiningAbodeState.HeadActorTypePlayerSoul)
        };

        if (context.ResidentRoot?["entries"] is JsonArray residentEntries)
        {
            foreach (var resident in residentEntries.OfType<JsonObject>()
                         .Where(entry =>
                             string.Equals(GetNodeString(entry["ascensionState"]), ShiningAbodeState.AscensionStateAscended, StringComparison.OrdinalIgnoreCase) &&
                             string.Equals(GetNodeString(entry["shiningFactionId"]), factionId, StringComparison.OrdinalIgnoreCase)))
            {
                var residentId = GetNodeString(resident["residentId"]) ?? string.Empty;
                choices.Add(new ShiningActorChoice(
                    BuildShiningResidentPoliticalChoiceLabel(resident),
                    ShiningAbodeState.HeadActorTypeResident,
                    residentId));
            }
        }

        var seenGuardianIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (context.GuardiansRoot?["activeGuardian"] is JsonObject activeGuardian)
        {
            var guardianId = GetNodeString(activeGuardian["guardianId"]) ?? string.Empty;
            if (!string.IsNullOrWhiteSpace(guardianId))
            {
                seenGuardianIds.Add(guardianId);
                choices.Add(new ShiningActorChoice(
                    BuildShiningGuardianPoliticalChoiceLabel(activeGuardian, isActive: true),
                    ShiningAbodeState.HeadActorTypeGuardian,
                    guardianId));
            }
        }

        if (context.GuardiansRoot?["guardians"] is JsonArray guardians)
        {
            foreach (var guardian in guardians.OfType<JsonObject>())
            {
                var guardianId = GetNodeString(guardian["guardianId"]) ?? string.Empty;
                if (string.IsNullOrWhiteSpace(guardianId) || !seenGuardianIds.Add(guardianId))
                    continue;
                choices.Add(new ShiningActorChoice(
                    BuildShiningGuardianPoliticalChoiceLabel(guardian, isActive: false),
                    ShiningAbodeState.HeadActorTypeGuardian,
                    guardianId));
            }
        }

        if (context.Root["shiningPoliticalActors"] is JsonArray politicalActors)
        {
            foreach (var actor in politicalActors.OfType<JsonObject>())
            {
                var actorId = GetNodeString(actor["actorId"]) ?? string.Empty;
                choices.Add(new ShiningActorChoice(
                    BuildShiningRadiantActorPoliticalChoiceLabel(actor, context.Root),
                    ShiningAbodeState.HeadActorTypeRadiantActor,
                    actorId));
            }
        }

        var selected = Prompt(new SelectionPrompt<string>()
            .Title("[bold yellow]Выберите нового главу[/]")
            .HighlightStyle(new Style(Color.Gold1))
            .AddChoices(choices.Select(choice => choice.Label).Append("← Назад")));
        if (selected.Contains("Назад", StringComparison.Ordinal))
            return null;

        return choices.First(choice => choice.Label == selected);
    }

    private static string BuildShiningResidentPoliticalChoiceLabel(JsonObject resident)
    {
        var residentId = GetNodeString(resident["residentId"]) ?? string.Empty;
        var residentName = GetNodeString(resident["displayName"]) ?? GetNodeString(resident["residentName"]) ?? residentId;
        var ascensionState = GetNodeString(resident["ascensionState"]) ?? "unknown";
        var factionId = GetNodeString(resident["shiningFactionId"]) ?? "none";
        var factionLabel = GetNodeString(resident["shiningFactionName"]) ?? factionId;
        var role = DescribeShiningResidentRole(GetNodeString(resident["residentRole"]));
        var loyaltyLevel = GetNodeInt(resident["factionLoyaltyLevel"]);
        var loyaltyTier = DescribeShiningFactionLoyaltyTier(GetNodeString(resident["factionLoyaltyTier"]));
        var restlessness = GetNodeInt(resident["factionRestlessness"]);
        var realignmentState = DescribeShiningFactionRealignmentState(GetNodeString(resident["factionRealignmentState"]));
        return $"{Markup.Escape(residentName)} [dim]({Markup.Escape(residentId)}; фракция {Markup.Escape(factionLabel)}/{Markup.Escape(factionId)}; ascension={Markup.Escape(ascensionState)}; роль {Markup.Escape(role)}; лояльность {loyaltyLevel}/{Markup.Escape(loyaltyTier)}; брожение {restlessness}; перестройка {Markup.Escape(realignmentState)})[/]";
    }

    private static string BuildShiningGuardianPoliticalChoiceLabel(JsonObject guardian, bool isActive)
    {
        var guardianId = GetNodeString(guardian["guardianId"]) ?? string.Empty;
        var guardianName = GetNodeString(guardian["guardianName"]) ?? GetNodeString(guardian["name"]) ?? guardianId;
        var domain = GetNodeString(guardian["domain"]) ?? "domain не указан";
        var roleToPlayer = GetNodeString(guardian["guardianRoleToPlayer"]) ?? GetNodeString(guardian["roleToPlayer"]) ?? "role не указана";
        var activeTag = isActive ? "activeGuardian" : "known guardian";
        return $"{Markup.Escape(guardianName)} [dim]({Markup.Escape(guardianId)}; хранитель; {activeTag}; domain={Markup.Escape(domain)}; roleToPlayer={Markup.Escape(roleToPlayer)})[/]";
    }

    private static string BuildShiningRadiantActorPoliticalChoiceLabel(JsonObject actor, JsonObject shiningRoot)
    {
        var actorId = GetNodeString(actor["actorId"]) ?? string.Empty;
        var displayName = GetNodeString(actor["displayName"]) ?? actorId;
        var status = DescribeShiningPoliticalStatus(GetNodeString(actor["politicalStatus"]));
        var factionId = GetNodeString(actor["currentFactionId"]) ?? "none";
        var factionName = ResolveShiningFactionLabel(shiningRoot, factionId);
        var summary = GetNodeString(actor["summary"]);
        var suffix = string.IsNullOrWhiteSpace(summary) ? string.Empty : $"; {summary}";
        return $"{Markup.Escape(displayName)} [dim]({Markup.Escape(actorId)}; светозарный актор; статус {Markup.Escape(status)}; фракция {Markup.Escape(factionName)}/{Markup.Escape(factionId)}{Markup.Escape(suffix)})[/]";
    }

    private static JsonObject? FindShiningFactionNode(JsonObject shiningRoot, string factionId)
    {
        if (shiningRoot["factions"] is not JsonArray factions)
            return null;

        return factions.OfType<JsonObject>()
            .FirstOrDefault(faction => string.Equals(GetNodeString(faction["factionId"]), factionId, StringComparison.OrdinalIgnoreCase));
    }

    private static string Slugify(string value)
    {
        var normalized = value.Trim().ToLowerInvariant();
        var builder = new System.Text.StringBuilder(normalized.Length);
        foreach (var ch in normalized)
        {
            if (char.IsLetterOrDigit(ch))
                builder.Append(ch);
            else if (builder.Length == 0 || builder[^1] != '_')
                builder.Append('_');
        }

        return builder.ToString().Trim('_');
    }

    private static string BuildSelectedSupporterSummary(JsonObject? residentRoot, IReadOnlyCollection<string> selectedIds)
    {
        if (selectedIds.Count == 0)
            return string.Empty;

        return string.Join(", ", selectedIds.Select(selectedId =>
        {
            if (residentRoot?["entries"] is JsonArray entries)
            {
                var resident = entries.OfType<JsonObject>()
                    .FirstOrDefault(entry => string.Equals(GetNodeString(entry["residentId"]), selectedId, StringComparison.OrdinalIgnoreCase));
                var displayName = GetNodeString(resident?["displayName"]) ?? selectedId;
                return displayName;
            }

            return selectedId;
        }));
    }

    private bool ConfirmShiningPoliticalRequestPreview(
        string confirmationTitle,
        string pendingPath,
        JsonObject? requestAudit,
        IReadOnlyList<string> lines)
    {
        Clear();
        Write(new Panel(GameInterface.SafeMarkup(string.Join("\n", lines)))
        {
            Header = new PanelHeader(" 🏛 Полный предпросмотр политического контракта ", Justify.Center),
            Border = BoxBorder.Double,
            BorderStyle = new Style(Color.Orange1),
            Padding = new Padding(2, 1),
            Expand = true
        });

        WriteJsonAuditPanel($"Полный JSON {pendingPath}.requests[0]", requestAudit, Color.Orange1);

        var choice = Prompt(new SelectionPrompt<string>()
            .Title($"[bold yellow]{Markup.Escape(confirmationTitle)}[/]")
            .HighlightStyle(new Style(Color.Orange1))
            .AddChoices("✅ Создать pending request", "← Отмена"));

        return choice.Contains("Создать", StringComparison.OrdinalIgnoreCase) ||
               choice.Contains("Подтвердить", StringComparison.OrdinalIgnoreCase);
    }

    private static JsonObject? SerializePoliticalRequestForPreview<TRequest>(TRequest request)
    {
        return JsonSerializer.SerializeToNode(request, SharedJsonOptions.PrettyCamelCaseUnsafeRelaxed) as JsonObject;
    }

    private List<string> BuildShiningFoundingRequestPreviewLines(
        ShiningContext context,
        ShiningFactionRequestState.PendingShiningFactionFoundingRequest request,
        int currentFeathers,
        int costFeathers,
        int costLightSparks)
    {
        var currentLightSparks = GetNodeInt(context.Root["lightSparks"]);
        var lines = BuildShiningPoliticalPreviewHeader(
            "Основание новой сияющей фракции",
            ShiningFactionRequestState.PendingFoundingsRequestPath,
            request.RequestId,
            request.CreatedAtTurn,
            request.CreatedAtUtc);

        lines.Add("");
        lines.Add("[bold]Материализуемая основа:[/]");
        lines.Add($"  • proposedFactionId: [dim]{Markup.Escape(request.ProposedFactionId)}[/]");
        lines.Add($"  • proposedHallId: [dim]{Markup.Escape(request.ProposedHallId)}[/]");
        lines.Add($"  • Зал: [white]{Markup.Escape(request.ProposedHallName)}[/]");
        lines.Add($"  • Описание зала: {Markup.Escape(request.ProposedHallDescription)}");
        lines.Add($"  • Службы зала: {Markup.Escape(string.Join(", ", request.ProposedHallServiceTags.Select(DescribeShiningHallServiceTag)))}");
        lines.Add($"  • Фракция: [white]{Markup.Escape(request.Charter.FactionName)}[/]");
        lines.Add($"  • Устав: {Markup.Escape(request.Charter.Summary)}");
        lines.Add($"  • Любимый архетип: [dim]{Markup.Escape(DescribeShiningProjectArchetype(request.Charter.FavoredArchetype))}[/]");
        lines.Add($"  • Покровительствующий эффект: [dim]{Markup.Escape(DescribeShiningEffectFamily(request.Charter.PatronEffectFamily))}[/]");
        AppendPoliticalResidentList(lines, "Сторонники", context.ResidentRoot, request.SupportingResidentIds);

        lines.Add("");
        lines.Add("[bold]Резервируемые ресурсы:[/]");
        lines.Add($"  • Чернильные Перья: [white]{currentFeathers}[/] -> [white]{Math.Max(0, currentFeathers - costFeathers)}[/] [dim](quotedCostFeathers={request.QuotedCostFeathers})[/]");
        lines.Add($"  • Искры Света: [white]{currentLightSparks}[/] -> [white]{Math.Max(0, currentLightSparks - costLightSparks)}[/] [dim](quotedCostLightSparks={request.QuotedCostLightSparks})[/]");
        lines.Add("  • Отмена на этом экране не пишет pending file и не списывает ресурсы.");

        lines.Add("");
        lines.Add("[bold]Контракт закрытия для GM:[/]");
        lines.Add("  • accepted: создать `halls[]` и `factions[]` с exact proposed ids, services, charter and supporter alignment.");
        lines.Add("  • accepted: записать `factionFoundingReceipts[]` с requestId, costs, supportingResidentIds, resolvedAtTurn/resolvedAtUtc/status/reason.");
        lines.Add("  • refused/withdrawn: не создавать hall/faction; закрыть только receipt с canonical status.");
        lines.Add("  • GM не переписывает pending file как output; pending contract остаётся client-owned input.");
        return lines;
    }

    private List<string> BuildShiningRealignmentRequestPreviewLines(
        ShiningContext context,
        ShiningFactionRequestState.PendingShiningFactionRealignmentRequest request)
    {
        var lines = BuildShiningPoliticalPreviewHeader(
            "Перестройка сияющего резидента",
            ShiningFactionRequestState.PendingRealignmentsRequestPath,
            request.RequestId,
            request.CreatedAtTurn,
            request.CreatedAtUtc);

        lines.Add("");
        lines.Add("[bold]Резидент и политический путь:[/]");
        lines.Add($"  • Резидент: [white]{Markup.Escape(request.ResidentName)}[/] [dim]({Markup.Escape(request.ResidentId)})[/]");
        lines.Add($"  • Исходная фракция: [white]{Markup.Escape(request.SourceFactionName)}[/] [dim]({Markup.Escape(request.SourceFactionId)})[/]");
        lines.Add($"  • Режим: [dim]{Markup.Escape(DescribeShiningRealignmentMode(request.RealignmentMode))}[/] [dim]({Markup.Escape(request.RealignmentMode)})[/]");
        if (!string.IsNullOrWhiteSpace(request.TargetFactionId))
            lines.Add($"  • Целевая фракция: [white]{Markup.Escape(request.TargetFactionName)}[/] [dim]({Markup.Escape(request.TargetFactionId)})[/]");
        lines.Add($"  • Лояльность: [dim]{request.FactionLoyaltyLevel} / {Markup.Escape(DescribeShiningFactionLoyaltyTier(request.FactionLoyaltyTier))}[/]");
        lines.Add($"  • Брожение: [dim]{request.FactionRestlessness}[/]");
        lines.Add($"  • Состояние перестройки: [dim]{Markup.Escape(DescribeShiningFactionRealignmentState(request.FactionRealignmentState))}[/]");

        lines.Add("");
        lines.Add("[bold]Контракт закрытия для GM:[/]");
        lines.Add("  • accepted_transfer: обновить canonical resident shiningFactionId/name, loyalty/restlessness state and write resident history.");
        lines.Add("  • departure_to_neutral: очистить faction binding у резидента и зафиксировать departed_to_neutral receipt.");
        lines.Add("  • refused/withdrawn: не менять resident faction binding; закрыть только receipt/status/reason.");
        lines.Add("  • Обязательно записать `factionRealignmentReceipts[]` with requestId, residentId, source/target, realignmentMode, status, resolvedAtTurn/resolvedAtUtc.");
        return lines;
    }

    private List<string> BuildShiningLeadershipRequestPreviewLines(
        ShiningContext context,
        ShiningFactionRequestState.PendingShiningFactionLeadershipTransitionRequest request)
    {
        var lines = BuildShiningPoliticalPreviewHeader(
            "Смена главы сияющей фракции",
            ShiningFactionRequestState.PendingLeadershipTransitionsRequestPath,
            request.RequestId,
            request.CreatedAtTurn,
            request.CreatedAtUtc);

        lines.Add("");
        lines.Add("[bold]Смена власти:[/]");
        lines.Add($"  • Фракция: [white]{Markup.Escape(request.FactionName)}[/] [dim]({Markup.Escape(request.FactionId)})[/]");
        lines.Add($"  • Режим: [dim]{Markup.Escape(DescribeShiningLeadershipMode(request.TransitionMode))}[/] [dim]({Markup.Escape(request.TransitionMode)})[/]");
        lines.Add($"  • Текущий глава: [dim]{Markup.Escape(BuildHeadActorLabel(request.IncumbentHeadActorType, request.IncumbentHeadActorId))}[/]");
        lines.Add(string.IsNullOrWhiteSpace(request.CandidateHeadActorId)
            ? "  • Новый глава: [dim]не указан; accepted abdication оставит место вакантным[/]"
            : $"  • Новый глава: [white]{Markup.Escape(BuildHeadActorLabel(request.CandidateHeadActorType, request.CandidateHeadActorId))}[/]");
        AppendPoliticalResidentList(lines, "Сторонники перехода", context.ResidentRoot, request.SupportingResidentIds);

        lines.Add("");
        lines.Add("[bold]Контракт закрытия для GM:[/]");
        lines.Add("  • accepted: обновить faction.leadership and, for radiant_actor heads, matching `shiningPoliticalActors[]` currentFactionId/politicalStatus.");
        lines.Add("  • accepted: записать `leadershipReceipts[]` and `leadershipHistory[]` with succeeded/abdicated/revolted/vacated event mapping.");
        lines.Add("  • refused/withdrawn: leadership state remains unchanged except canonical receipt/history refusal marker.");
        lines.Add("  • Candidate/supporter ids must be echoed from this request; GM must not invent a different hidden electorate.");
        return lines;
    }

    private static List<string> BuildShiningPoliticalPreviewHeader(
        string actionLabel,
        string pendingPath,
        string requestId,
        int createdAtTurn,
        string createdAtUtc)
    {
        return new List<string>
        {
            "[bold yellow]Перед записью political pending-контракта[/]",
            "",
            $"  Действие: [white]{Markup.Escape(actionLabel)}[/]",
            $"  Файл: [dim]{Markup.Escape(pendingPath)}[/]",
            $"  requestId: [dim]{Markup.Escape(requestId)}[/]",
            $"  createdAtTurn: [dim]{createdAtTurn}[/]",
            $"  createdAtUtc: [dim]{Markup.Escape(createdAtUtc)}[/]",
            "",
            "[bold]Правило очереди и владения:[/]",
            "  • Этот pending file является client-owned input для следующего accepted/refused/withdrawn хода.",
            "  • GM закрывает exact requestId через canonical receipt/history/state surfaces.",
            "  • Mortal World factions/NPC/location/time outputs здесь запрещены."
        };
    }

    private static void AppendPoliticalResidentList(
        List<string> lines,
        string label,
        JsonObject? residentRoot,
        IReadOnlyList<string> residentIds)
    {
        lines.Add($"  • {label}:");
        if (residentIds.Count == 0)
        {
            lines.Add("    [dim]нет[/]");
            return;
        }

        foreach (var residentId in residentIds)
        {
            var resident = (residentRoot?["entries"] as JsonArray)?.OfType<JsonObject>()
                .FirstOrDefault(entry => string.Equals(GetNodeString(entry["residentId"]), residentId, StringComparison.OrdinalIgnoreCase));
            var displayName = GetNodeString(resident?["displayName"]) ?? GetNodeString(resident?["residentName"]) ?? residentId;
            var factionName = GetNodeString(resident?["shiningFactionName"]) ?? GetNodeString(resident?["shiningFactionId"]) ?? "none";
            lines.Add($"    - {Markup.Escape(displayName)} [dim]({Markup.Escape(residentId)}, faction {Markup.Escape(factionName)})[/]");
        }
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
}
