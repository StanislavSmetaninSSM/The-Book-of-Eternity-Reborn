using BookOfEternityClient.Core;
using BookOfEternityClient.Services;

namespace BookOfEternityClient.WebUi;

internal enum BrowserPreparedWriteDisposition { Blocked, Committed, RolledBack, Uncertain }
internal sealed record BrowserPreparedWriteResult(BrowserPreparedWriteDisposition Disposition,
    bool NeedsFollowUp, string Message);
internal sealed record PreparedBrowserLocalWrite(IReadOnlyList<CanonicalLocalFileChange> Changes,
    Func<Task> ApplyCommittedRuntime);

public sealed partial class BrowserLocalWriteCoordinator
{
    // prepare reads exact images only; ApplyCommittedRuntime changes runtime
    // references only. Neither callback is a second filesystem transaction.
    internal async Task<BrowserPreparedWriteResult> ExecutePreparedAsync(BrowserLocalWriteRequest request,
        Func<FileSystemManager.CanonicalWriteLease, Task<PreparedBrowserLocalWrite>> prepare)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(prepare);
        BrowserPreparedWriteResult? committed = null;
        try
        {
            return await RunBoundTransactionAsync(async writeLease =>
            {
                var admission = await AcquireLocalWriteGuardAsync(writeLease, request);
                if (!admission.Acquired || admission.Lease == null)
                    return new BrowserPreparedWriteResult(BrowserPreparedWriteDisposition.Blocked, false, admission.BlockerMessage);

                var result = new BrowserPreparedWriteResult(BrowserPreparedWriteDisposition.Blocked, false,
                    "Настройки не изменены: не удалось подготовить запись.");
                try
                {
                    var prepared = await prepare(writeLease);
                    var publication = await _fs.PublishLocalFilesAsync(writeLease, prepared.Changes);
                    result = publication.Disposition switch
                    {
                        TrustedLocalPublicationDisposition.Committed => new(BrowserPreparedWriteDisposition.Committed,
                            publication.Failure != null, "Настройки сохранены."),
                        TrustedLocalPublicationDisposition.RolledBack => new(BrowserPreparedWriteDisposition.RolledBack,
                            false, "Настройки не сохранены: предыдущие файлы восстановлены."),
                        _ => new(BrowserPreparedWriteDisposition.Uncertain, true,
                            "Не удалось подтвердить состояние сохранения настроек. Требуется восстановление сохранённых свидетельств; прежние настройки не считаются подтверждёнными.")
                    };
                    if (result.Disposition == BrowserPreparedWriteDisposition.Committed)
                    {
                        // Record this before any callback or owner cleanup. Even
                        // a later bound-operation fence cannot revoke this fact.
                        committed = result;
                        try
                        {
                            _fs.VerifyCurrentSessionOperation(writeLease);
                            await prepared.ApplyCommittedRuntime();
                        }
                        catch
                        {
                            result = WithPreparedFollowUp(result);
                            committed = result;
                        }
                    }
                }
                catch (SessionReplacedException) { throw; }
                catch
                {
                    // PublishLocalFilesAsync returns an explicit outcome once
                    // publication is attempted. Exceptions here precede it.
                    result = new(BrowserPreparedWriteDisposition.Blocked, false,
                        "Настройки не изменены: не удалось безопасно подготовить запись.");
                }
                finally
                {
                    if (!await TryReleaseAsync(writeLease, admission.Lease))
                        result = WithPreparedFollowUp(result);
                    if (result.Disposition == BrowserPreparedWriteDisposition.Committed)
                        committed = result;
                }
                return result.NeedsFollowUp && result.Disposition == BrowserPreparedWriteDisposition.Committed
                    ? WithPreparedFollowUp(result) : result;
            });
        }
        catch (SessionReplacedException)
        {
            return committed != null ? WithPreparedFollowUp(committed) : new(
                BrowserPreparedWriteDisposition.Blocked, true,
                "Игровая сессия изменилась до сохранения настроек. Обновите состояние книги.");
        }
        catch
        {
            return committed != null ? WithPreparedFollowUp(committed) : new(
                BrowserPreparedWriteDisposition.Blocked, true,
                "Настройки не изменены: локальное хранилище требует проверки перед записью.");
        }
    }

    private static BrowserPreparedWriteResult WithPreparedFollowUp(BrowserPreparedWriteResult result) => result with
    {
        NeedsFollowUp = true,
        Message = result.Disposition == BrowserPreparedWriteDisposition.Committed
            ? "Настройки сохранены. Обновление текущего интерфейса или служебная очистка требуют повторной проверки; сохранённые файлы не отменены."
            : result.Message
    };

    private async Task<LocalUiSessionLockResult> AcquireLocalWriteGuardAsync(
        FileSystemManager.CanonicalWriteLease writeLease, BrowserLocalWriteRequest request)
    {
        BrowserPendingTurnStatus pending;
        try { pending = BrowserPendingTurnInspector.Build(_fs, writeLease); }
        catch (InvalidDataException ex)
        {
            return LocalUiSessionLockResult.BlockedBy(null,
                $"Browser-write заблокирован: повреждена служебная разметка активного хода ({ex.Message}).");
        }
        if (pending.HasActiveGmTurn)
            return LocalUiSessionLockResult.BlockedBy(null,
                "Browser-write заблокирован: активный GM-turn или rollback/snapshot artifact должен быть завершён до локальной записи.");
        return request.ExistingLease == null
            ? await _lockService.AcquireOrRefreshAsync(writeLease, BuildOwner(request), request.OperationLabel)
            : await _lockService.RefreshAsync(writeLease, request.ExistingLease, request.OperationLabel);
    }
}
