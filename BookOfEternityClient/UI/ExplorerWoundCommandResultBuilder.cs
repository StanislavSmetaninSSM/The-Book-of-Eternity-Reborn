using BookOfEternityClient.CommandProtocol;
using BookOfEternityClient.Services;

namespace BookOfEternityClient.UI;

internal static class ExplorerWoundCommandResultBuilder
{
    internal static ExplorerCommandResult Build(string command, WoundPlayerProjectionResult projection,
        WoundCommandState? state, bool treatment)
    {
        var blocks = new List<UiBlock>();
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
                    : "Выберите рану и способ лечения: сначала свои раны, затем доступные раны текущего Хранителя. Принятие частной помощи Хранителя требует вашего подтверждения. ОД не тратятся; мир совершит один обычный цикл и при неудаче. Бросок сохраняется после отправки."
                : "Сейчас лечение духовной раны вне конфликта недоступно. Нужны видимая активная рана I–IV степени и достаточное собственное Исцеление либо принятое предложение частной помощи текущего Хранителя." });
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
}
