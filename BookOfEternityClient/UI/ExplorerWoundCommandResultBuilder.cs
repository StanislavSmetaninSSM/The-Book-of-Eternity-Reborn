using BookOfEternityClient.CommandProtocol;
using BookOfEternityClient.Services;

namespace BookOfEternityClient.UI;

internal static class ExplorerWoundCommandResultBuilder
{
    internal static ExplorerCommandResult Build(string command, WoundPlayerProjectionResult projection,
        WoundCommandState? state, bool treatment)
    {
        if (treatment && state is { IsTargetSelection: true }) return TargetChoice(command, state);
        var blocks = new List<UiBlock>();
        if (state?.SelectedTarget is { } target)
        {
            blocks.Add(new UiTextBlock { Text = "Цель: " + target.Name + " — " + target.Context });
            if (target.Owner.OwnerKind is not ("player" or "player_soul"))
            {
                projection = new(true, "У этой цели сейчас нет известных активных ран.", target.Wounds.Select(w =>
                    new WoundPlayerEntry(w.Name, w.Severity, w.Description, [], [], [])).ToArray(), []);
                if (!state.Choices.Any(c => c.Available))
                    blocks.Add(new UiTextBlock { Text = "Лечение пока недоступно: нужны достаточное Исцеление и действующее согласие обитателя. Выбор цели не начинает лечение." });
            }
        }
        if (!projection.IsAvailable || projection.Active.Count == 0)
            blocks.Add(new UiTextBlock { Text = projection.StatusMessage });
        foreach (var wound in projection.Active)
        {
            var lines = new List<string> { wound.Description };
            lines.AddRange(wound.Facts.Select(f => f.Label + ": " + f.Value));
            lines.AddRange(wound.Diagnosis);
            lines.AddRange(wound.Methods.Select(m => m.Name + ": " + string.Join("; ", m.Requirements) + ". " + m.Policy));
            blocks.Add(new UiPanelBlock { Title = wound.Name + " — " + wound.Severity,
                Blocks = lines.Where(l => !string.IsNullOrWhiteSpace(l)).Select(l => (UiBlock)new UiTextBlock { Text = l }).ToList() });
        }
        if (projection.History.Count > 0)
            blocks.Add(new UiPanelBlock { Title = "История заживших ран", Blocks = projection.History.Select(h =>
                (UiBlock)new UiTextBlock { Text = h.Name + " — " + h.Summary }).ToList() });
        if (treatment && state is not null)
            foreach (var choice in state.Choices.Where(c => !c.Available))
                blocks.Add(new UiPanelBlock { Title = "Самостоятельное лечение — " + choice.Name + " — " + choice.Severity,
                    Blocks = [new UiTextBlock { Text = choice.Reason }] });
        var canTreat = treatment && state?.Choices.Any(c => c.Available) == true;
        var combat = state?.Choices.Any(c => c.Selection.Combat is not null) == true;
        if (treatment)
            blocks.Add(new UiTextBlock { Text = canTreat
                ? combat ? "Выберите собственную рану для лечения в духовной дуэли. Противник может прервать действие; ОД списываются один раз и при прерывании. Безопасный цикл и естественное восстановление не проходят. Используется бросок этого обмена."
                    : "Выберите известную рану и доступный способ лечения. Помощь сознательному обитателю требует его согласия; принятие частной помощи подтверждаете вы. ОД не тратятся; мир совершит один обычный цикл и при неудаче. Бросок сохраняется после отправки."
                : "Сейчас лечение духовной раны недоступно. Нужны видимая активная рана I–IV степени, достаточное Исцеление и согласие участников." });
        return new()
        {
            Command = command, State = canTreat ? CommandExecutionState.RequiresInput : CommandExecutionState.Completed,
            Blocks = blocks, WoundCommandState = state,
            Prompts = !canTreat ? [] :
            [
                new UiSelectionPrompt { Id = "wound_selection", Prompt = "Какую рану лечить?", Required = true,
                    Options = state!.Choices.Select(c => new UiSelectionOption { Value = c.Value, Label = c.Name + " — " + c.Severity,
                        Description = c.Reason, Disabled = !c.Available }).ToList() },
                new UiConfirmationPrompt { Id = "confirm_wound_treatment", Prompt = combat ? "Подтвердить прерываемое лечение за ОД в этом обмене?" : "Подтвердить лечение и один цикл мира?", Required = true }
            ]
        };
    }

    private static ExplorerCommandResult TargetChoice(string command, WoundCommandState state)
    {
        return new()
        {
            Command = command, State = CommandExecutionState.RequiresInput, WoundCommandState = state,
            Blocks = new List<UiBlock> { new UiTextBlock { Text = "Кому требуется помощь? Сначала вы, затем видимые обитатели рядом. Выбор покажет известные раны и требования; лечение начнётся только после отдельного подтверждения." } }
                .Concat(state.Targets.Where(t => !t.Available).Select(t => (UiBlock)new UiPanelBlock
                { Title = t.Label, Blocks = [new UiTextBlock { Text = t.Reason }] })).ToList(),
            Prompts = [new UiSelectionPrompt
            {
                Id = "wound_target", Prompt = "Кому требуется помощь?", Required = true,
                Options = state.Targets.Select(t => new UiSelectionOption
                {
                    Value = t.Value, Label = t.Label,
                    Description = t.Context + (t.Reason.Length == 0 ? "" : ". " + t.Reason), Disabled = !t.Available
                }).ToList()
            }]
        };
    }
}
