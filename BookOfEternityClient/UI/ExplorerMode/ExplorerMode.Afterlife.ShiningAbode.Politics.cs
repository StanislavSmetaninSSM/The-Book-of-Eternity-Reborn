using System.Text.Json.Nodes;
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
        var cost = new { Feathers = 25, LightSparks = 15 };
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
            CreatedAtTurn = _stateManager.CurrentState.TurnNumber + 1
        };

        var error = await ShiningFactionRequestState.ValidateFoundingRequestAgainstCurrentStateAsync(_fs, request);
        if (!string.IsNullOrWhiteSpace(error))
        {
            MarkupLine($"[yellow]{Markup.Escape(error)}[/]");
            WaitForKey();
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

        MarkupLine("[green]Создан ожидающий запрос на основание сияющей фракции.[/]");
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

        var secondary = Prompt(new SelectionPrompt<string>()
            .Title($"[bold yellow]Дополнительная служба зала[/]\n[dim]Обязательная основная служба уже зафиксирована: {requiredPrimary}[/]")
            .HighlightStyle(new Style(Color.Gold1))
            .AddChoices(options.Append("без второго тега")));

        var tags = new List<string> { requiredPrimary };
        if (!secondary.Contains("без", StringComparison.OrdinalIgnoreCase))
            tags.Add(secondary);
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
                var label = $"{displayName} [dim]({factionId} • лояльность {GetNodeInt(entry["factionLoyaltyLevel"])} • брожение {GetNodeInt(entry["factionRestlessness"])})[/]";
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
                var label = $"{factionName} [dim](strength {GetNodeInt(faction["factionStrength"])} • {factionId})[/]";
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
                : $"[dim]Выбрано: {Markup.Escape(string.Join(", ", selected))}[/]";
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
                var displayName = GetNodeString(entry["displayName"]) ?? residentId;
                var factionId = GetNodeString(entry["shiningFactionId"]) ?? "none";
                var label = $"{displayName} [dim]({factionId} • {residentId})[/]";
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
                var residentName = GetNodeString(resident["displayName"]) ?? residentId;
                choices.Add(new ShiningActorChoice(
                    $"{residentName} [dim](резидент:{residentId})[/]",
                    ShiningAbodeState.HeadActorTypeResident,
                    residentId));
            }
        }

        var seenGuardianIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (context.GuardiansRoot?["activeGuardian"] is JsonObject activeGuardian)
        {
            var guardianId = GetNodeString(activeGuardian["guardianId"]) ?? string.Empty;
            var guardianName = GetNodeString(activeGuardian["guardianName"]) ?? guardianId;
            if (!string.IsNullOrWhiteSpace(guardianId))
            {
                seenGuardianIds.Add(guardianId);
                choices.Add(new ShiningActorChoice(
                    $"{guardianName} [dim](хранитель:{guardianId})[/]",
                    ShiningAbodeState.HeadActorTypeGuardian,
                    guardianId));
            }
        }

        if (context.GuardiansRoot?["guardians"] is JsonArray guardians)
        {
            foreach (var guardian in guardians.OfType<JsonObject>())
            {
                var guardianId = GetNodeString(guardian["guardianId"]) ?? string.Empty;
                var guardianName = GetNodeString(guardian["guardianName"]) ?? guardianId;
                if (string.IsNullOrWhiteSpace(guardianId) || !seenGuardianIds.Add(guardianId))
                    continue;
                choices.Add(new ShiningActorChoice(
                    $"{guardianName} [dim](хранитель:{guardianId})[/]",
                    ShiningAbodeState.HeadActorTypeGuardian,
                    guardianId));
            }
        }

        if (context.Root["shiningPoliticalActors"] is JsonArray politicalActors)
        {
            foreach (var actor in politicalActors.OfType<JsonObject>())
            {
                var actorId = GetNodeString(actor["actorId"]) ?? string.Empty;
                var displayName = GetNodeString(actor["displayName"]) ?? actorId;
                choices.Add(new ShiningActorChoice(
                    $"{displayName} [dim](светозарный актор:{actorId})[/]",
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
