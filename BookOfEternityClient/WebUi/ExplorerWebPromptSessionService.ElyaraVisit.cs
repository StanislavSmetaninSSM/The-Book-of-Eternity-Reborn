using System.Text.Json.Nodes;
using BookOfEternityClient.CommandProtocol;
using BookOfEternityClient.Services;

namespace BookOfEternityClient.WebUi;

public sealed partial class ExplorerWebPromptSessionService
{
    // Same current-generation transaction, prompt identity and local UI lease
    // used by wound confirmation. The existing ordinary queue owns publication.
    private Task<ExplorerCommandResult> SubmitElyaraVisitAsync(string sessionId, PromptSessionSnapshot snapshot,
        IReadOnlyDictionary<string, JsonNode?> answers) => _woundCoordinator.RunBoundTransactionAsync(async lease =>
    {
        if (!WoundSnapshotCurrent(sessionId, snapshot, lease)) return MissingSessionResult(sessionId);
        if (BrowserPendingTurnInspector.Build(_fs, lease).HasActiveGmTurn)
            return WoundRefusal(snapshot, "Сначала завершите текущий ход. Переход к Элиаре не отправлен.");
        if (snapshot.Owner.Lease is not { } token ||
            !(await _lockService.RefreshAsync(lease, token, "Путь к Элиаре")).Acquired)
            return WoundRefusal(snapshot, "Форма потеряла право записи. Откройте путь к Элиаре заново.");
        if (snapshot.Result.ElyaraVisitCommandState is not { } state) return MissingSessionResult(sessionId);
        try
        {
            if (answers.GetValueOrDefault("confirm_elyara_visit") is not JsonValue flag ||
                !flag.TryGetValue<bool>(out var confirmed) || !confirmed)
                return WoundRefusal(snapshot, "Подтвердите переход и один цикл мира без лечения и оплаты.");
            var action = await ElyaraVisitService.ConfirmAsync(_fs, lease, state);
            if (!WoundSnapshotCurrent(sessionId, snapshot, lease)) return MissingSessionResult(sessionId);
            var pending = PendingPlayerActionService.PrepareQueued(_fs, lease, action,
                ElyaraVisitService.BrowserSource, DateTime.UtcNow.ToString("o"));
            var publication = await _woundCoordinator.ExecuteAtomicWithinTransactionAsync(lease,
                new(snapshot.Owner.OwnerId, snapshot.Owner.OwnerLabel, "Переход к Элиаре", snapshot.Owner.Lease),
                [PendingPlayerActionService.PendingPath], writeLease =>
                {
                    if (!WoundSnapshotCurrent(sessionId, snapshot, lease))
                        throw new InvalidDataException("Форма изменилась до публикации перехода.");
                    return _fs.WriteFileAtomicAsync(writeLease, PendingPlayerActionService.PendingPath, pending.ToJsonString());
                });
            var result = (publication.Success
                ? BrowserPromptWriteResult.Completed("Путь к Элиаре отправлен", "Цель проверена заново. После прибытия лечение и его цену нужно подтвердить отдельно.")
                : BrowserPromptWriteResult.Failed(CommandExecutionState.Blocked, UiNotificationSeverity.Warning,
                    "Переход не отправлен", publication.Message)).RetainPublication(publication);
            var removed = RemoveSnapshot(sessionId, snapshot);
            if (removed && !result.Success && !result.ContinuationBlocked) await ReleaseWoundLockAsync(lease, snapshot);
            return BuildDomainWriteResult(snapshot, result);
        }
        catch (Exception error) when (error is not CoordinatedStatePublicationUncertainException &&
            error is (InvalidDataException or InvalidOperationException or KeyNotFoundException))
        { return WoundRefusal(snapshot, "Местонахождение Элиары или путь изменились. Закройте форму и откройте предложение заново."); }
    });
}
