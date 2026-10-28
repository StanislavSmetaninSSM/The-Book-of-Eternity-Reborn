using System.Text.Json.Nodes;
using BookOfEternityClient.CommandProtocol;
using BookOfEternityClient.Core;
using BookOfEternityClient.Services;

namespace BookOfEternityClient.WebUi;

internal sealed class BrowserWoundWriteService(FileSystemManager files, BrowserLocalWriteCoordinator coordinator)
{
    internal Task<BrowserPromptWriteResult> ApplyAsync(WoundCommandState state,
        IReadOnlyDictionary<string, JsonNode?> answers, LocalUiSessionLockOwner owner) =>
        coordinator.RunBoundTransactionAsync(async lease =>
        {
            if (!answers.TryGetValue("confirm_wound_treatment", out var confirmation) ||
                confirmation is not JsonValue flag || !flag.TryGetValue<bool>(out var confirmed) || !confirmed)
                return BrowserPromptWriteResult.ValidationError("Подтвердите лечение и один обычный цикл мира.");
            try
            {
                var value = answers["wound_selection"]?.GetValue<string>() ?? string.Empty;
                var selection = await WoundApplicationService.ConfirmAsync(files, lease, state, value);
                var pending = PendingPlayerActionService.PrepareQueued(files, lease, WoundApplicationService.ActionFor(selection),
                    "browser-wound-action", DateTime.UtcNow.ToString("o"), selection);
                var result = await coordinator.ExecuteAtomicWithinTransactionAsync(lease,
                    new(owner.OwnerId, owner.OwnerLabel, "Отправка лечения", owner.Lease), [PendingPlayerActionService.PendingPath],
                    writeLease => files.WriteFileAtomicAsync(writeLease, PendingPlayerActionService.PendingPath, pending.ToJsonString()));
                return (result.Success
                    ? BrowserPromptWriteResult.Completed("Лечение отправлено", "Выбранная рана повторно проверена. Мир продолжит один обычный цикл; результат появится после хода Мастера.")
                    : BrowserPromptWriteResult.Failed(CommandExecutionState.Blocked, UiNotificationSeverity.Warning,
                        "Лечение не отправлено", result.Message)).RetainPublication(result);
            }
            catch (Exception failure) when (failure is not CoordinatedStatePublicationUncertainException &&
                failure is (InvalidDataException or InvalidOperationException or KeyNotFoundException))
            {
                return BrowserPromptWriteResult.ValidationError("Выбор или доступность лечения изменились. Закройте форму и откройте /лечить заново.");
            }
        });
}
