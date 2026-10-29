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

    private async Task ShowWoundsAsync()
    {
        var command = string.IsNullOrWhiteSpace(_currentCommandInput) ? "/раны" : _currentCommandInput;
        while (true)
        {
            var result = await WoundApplicationService.OpenAsync(_fs, command, treatment: false);
            ExplorerCommandResultConsoleRenderer.Render(_console, result);
            if (result.Actions.Count == 0) { WaitForKey(); return; }
            var labels = new[] { "Закрыть" }.Concat(result.Actions.Select(a => a.Label)).ToArray();
            var selected = _console.Prompt(new SelectionPrompt<string>().Title("Действие с ранами")
                .UseConverter(GameInterface.EscapeMarkup).AddChoices(labels));
            var action = result.Actions.SingleOrDefault(a => a.Label == selected);
            if (action is null) return;
            if (action.Command == ElyaraVisitService.Command) { await ShowElyaraVisitAsync(); return; }
            command = action.Command; // Every action reopens fresh current DATA; no retained projection.
        }
    }
    private Task ShowWoundTreatmentAsync() => ShowWoundCommandAsync(treatment: true);
    private async Task ShowWoundCommandAsync(bool treatment, ExplorerCommandResult? initial = null)
    {
        var result = initial ?? await WoundApplicationService.OpenAsync(_fs, treatment ? "/лечить" : "/раны", treatment);
        IEnumerable<string> VisitLabels() => result.Actions.Where(a => a.Command == ElyaraVisitService.Command).Select(a => a.Label);
        async Task<bool> VisitSelected(string label)
        {
            if (!VisitLabels().Contains(label, StringComparer.Ordinal)) return false;
            await ShowElyaraVisitAsync(); return true;
        }
        ExplorerCommandResultConsoleRenderer.Render(_console, result);
        if (result.WoundCommandState is { IsTargetSelection: true } targets)
        {
            var offered = targets.Targets.Where(t => t.Available).ToArray();
            var targetLabels = offered.Select((t, i) => (i + 1) + ". " + t.Label).Concat(VisitLabels()).Append("Отмена").ToArray();
            var targetLabel = _console.Prompt(new SelectionPrompt<string>().Title("Кому требуется помощь?")
                .UseConverter(GameInterface.EscapeMarkup).AddChoices(targetLabels));
            if (await VisitSelected(targetLabel)) return;
            var targetIndex = Array.IndexOf(targetLabels, targetLabel);
            if (targetIndex < 0 || targetIndex >= offered.Length) return;
            await using (var targetLease = await _fs.AcquireCanonicalWriteLeaseAsync())
                result = await WoundApplicationService.SelectTargetAsync(_fs, targetLease, targets,
                    offered[targetIndex].Value, treatment ? "/лечить" : "/раны");
            ExplorerCommandResultConsoleRenderer.Render(_console, result);
        }
        if (result.State != CommandExecutionState.RequiresInput || result.WoundCommandState is not { } state)
        {
            if (VisitLabels().Any())
            {
                var navigation = _console.Prompt(new SelectionPrompt<string>().Title("Помощь Элиары")
                    .UseConverter(GameInterface.EscapeMarkup).AddChoices(VisitLabels().Append("Отмена")));
                _ = await VisitSelected(navigation);
                return;
            }
            WaitForKey();
            return;
        }
        var choices = state.Choices.Where(c => c.Available).ToArray();
        var labels = choices.Select((c, i) => (i + 1) + ". " + c.Name + " — " + c.Severity).Concat(VisitLabels()).Append("Отмена").ToArray();
        var label = _console.Prompt(new SelectionPrompt<string>().Title("Какую рану лечить?")
            .UseConverter(GameInterface.EscapeMarkup).AddChoices(labels));
        if (await VisitSelected(label)) return;
        var index = Array.IndexOf(labels, label);
        if (index < 0 || index >= choices.Length) return;
        var confirmation = WoundApplicationService.ConfirmationFor(choices[index].Selection);
        if (!_console.Confirm(confirmation)) return;
        await using var lease = await _fs.AcquireCanonicalWriteLeaseAsync();
        var selection = await WoundApplicationService.ConfirmAsync(_fs, lease, state, choices[index].Value);
        _pendingGmAction = WoundApplicationService.ActionFor(selection);
        _pendingSpiritualSelfTreatment = new(_pendingGmAction, state.Generation, selection);
    }
}
