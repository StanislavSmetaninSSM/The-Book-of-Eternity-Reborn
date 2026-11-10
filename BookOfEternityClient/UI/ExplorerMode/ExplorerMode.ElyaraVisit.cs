using BookOfEternityClient.CommandProtocol;
using BookOfEternityClient.Services;

namespace BookOfEternityClient.UI;

public partial class ExplorerMode
{
    private sealed record PendingElyaraVisit(string Action, ElyaraVisitIntent Intent);
    private PendingElyaraVisit? _pendingElyaraVisit;

    internal ElyaraVisitIntent? ConsumePendingElyaraVisit(string action, string generation)
    {
        var pending = _pendingElyaraVisit; _pendingElyaraVisit = null;
        if (pending is null) return null;
        if (pending.Action != action || _pendingGmAction != action || pending.Intent.Generation != generation ||
            ElyaraVisitService.ActionFor(pending.Intent) != action)
            throw new InvalidDataException("Путь к Элиаре устарел; откройте предложение заново.");
        return pending.Intent;
    }

    private async Task ShowElyaraVisitAsync()
    {
        var result = await ElyaraVisitService.OpenAsync(_fs);
        if (result.WoundCommandState is not null)
        {
            // Reuse the fresh local treatment read; its own final confirmation
            // and Engine staging still revalidate all existing authorities.
            await ShowWoundCommandAsync(treatment: true, initial: result);
            return;
        }
        ExplorerCommandResultConsoleRenderer.Render(_console, result);
        if (result.ElyaraVisitCommandState is not { } state) { WaitForKey(); return; }
        if (!_console.Confirm("Подтвердить переход к Элиаре и один цикл мира без лечения и оплаты?")) return;
        await using var lease = await _fs.AcquireCanonicalWriteLeaseAsync();
        var action = await ElyaraVisitService.ConfirmAsync(_fs, lease, state);
        _pendingGmAction = action;
        _pendingElyaraVisit = new(action, state.Intent);
    }
}
