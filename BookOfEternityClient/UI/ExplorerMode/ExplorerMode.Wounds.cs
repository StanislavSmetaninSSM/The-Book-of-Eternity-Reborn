using BookOfEternityClient.CommandProtocol;
using BookOfEternityClient.Services;
using Spectre.Console;

namespace BookOfEternityClient.UI;

public partial class ExplorerMode
{
    private sealed record PendingWoundTreatmentAction(string Action, string Generation, SpiritualSelfTreatmentSelection Selection);
    private PendingWoundTreatmentAction? _pendingSpiritualSelfTreatment;

    // One-shot companion to this console's existing pending GM action, never a browser queue entry.
    internal SpiritualSelfTreatmentSelection? ConsumePendingSpiritualSelfTreatment(string action, string generation)
    {
        var pending = _pendingSpiritualSelfTreatment;
        _pendingSpiritualSelfTreatment = null;
        if (pending is null) return null;
        if (pending.Action != action || _pendingGmAction != action || pending.Generation != generation)
            throw new InvalidDataException("Выбор лечения устарел; откройте /лечить заново.");
        return pending.Selection;
    }

    private Task ShowWoundsAsync() => ShowWoundCommandAsync(treatment: false);
    private Task ShowWoundTreatmentAsync() => ShowWoundCommandAsync(treatment: true);
    private async Task ShowWoundCommandAsync(bool treatment)
    {
        var result = await WoundApplicationService.OpenAsync(_fs, treatment ? "/лечить" : "/раны", treatment);
        ExplorerCommandResultConsoleRenderer.Render(_console, result);
        if (result.State != CommandExecutionState.RequiresInput || result.WoundCommandState is not { } state)
        {
            WaitForKey();
            return;
        }
        var choices = state.Choices.Where(c => c.Available).ToArray();
        var labels = choices.Select((c, i) => (i + 1) + ". " + c.Name + " — " + c.Severity).Append("Отмена").ToArray();
        var label = _console.Prompt(new SelectionPrompt<string>().Title("Какую рану лечить?")
            .UseConverter(GameInterface.EscapeMarkup).AddChoices(labels));
        var index = Array.IndexOf(labels, label);
        if (index < 0 || index >= choices.Length) return;
        var confirmation = choices[index].Selection.Combat is not null
            ? "Подтвердить прерываемое лечение за ОД в этом обмене?"
            : "Подтвердить лечение без затрат ОД и один цикл мира?";
        if (!_console.Confirm(confirmation)) return;
        await using var lease = await _fs.AcquireCanonicalWriteLeaseAsync();
        var selection = await WoundApplicationService.ConfirmAsync(_fs, lease, state, choices[index].Value);
        _pendingGmAction = WoundApplicationService.ActionFor(selection);
        _pendingSpiritualSelfTreatment = new(_pendingGmAction, state.Generation, selection);
    }
}
