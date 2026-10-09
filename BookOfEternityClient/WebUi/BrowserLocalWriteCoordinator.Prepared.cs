using BookOfEternityClient.Core;
using BookOfEternityClient.Services;
using BookOfEternityClient.Services.GmRuntime;

namespace BookOfEternityClient.WebUi;

public enum BrowserPreparedWriteDisposition { Blocked, Committed, RolledBack, Uncertain }
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
        BrowserPreparedWriteResult? publicationOutcome = null;
        try
        {
            return await RunBoundTransactionAsync(async writeLease =>
            {
                var admission = await AcquireLocalWriteGuardAsync(writeLease, request);
                if (!admission.Acquired || admission.Lease == null)
                    return CapturePreparedResult(new BrowserPreparedWriteResult(BrowserPreparedWriteDisposition.Blocked, false, admission.BlockerMessage));

                var result = new BrowserPreparedWriteResult(BrowserPreparedWriteDisposition.Blocked, false,
                    "Не удалось подготовить новую запись настроек.");
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
                    // Retain every established outcome before later callbacks,
                    // owner cleanup or bound close can throw. In particular,
                    // unknown evidence cannot become an unchanged-files claim.
                    publicationOutcome = CapturePreparedResult(result);
                    if (result.Disposition == BrowserPreparedWriteDisposition.Committed)
                    {
                        try
                        {
                            _fs.VerifyCurrentSessionOperation(writeLease);
                            await prepared.ApplyCommittedRuntime();
                        }
                        catch
                        {
                            result = WithPreparedFollowUp(result);
                            publicationOutcome = result;
                        }
                    }
                }
                catch (SessionReplacedException) { throw; }
                catch
                {
                    // PublishLocalFilesAsync returns an explicit outcome once
                    // publication is attempted. Exceptions here precede it.
                    result = new(BrowserPreparedWriteDisposition.Blocked, false,
                        "Не удалось безопасно подготовить новую запись настроек.");
                }
                finally
                {
                    if (result.Disposition != BrowserPreparedWriteDisposition.Uncertain &&
                        !await TryReleaseAsync(writeLease, admission.Lease))
                        result = WithPreparedFollowUp(result);
                    if (result.Disposition != BrowserPreparedWriteDisposition.Blocked)
                        publicationOutcome = result;
                }
                return CapturePreparedResult(result.NeedsFollowUp && result.Disposition == BrowserPreparedWriteDisposition.Committed
                    ? WithPreparedFollowUp(result) : result);
            });
        }
        catch (SessionReplacedException)
        {
            return publicationOutcome != null ? WithPreparedFollowUp(publicationOutcome) : new(
                BrowserPreparedWriteDisposition.Blocked, true,
                "Игровая сессия изменилась до сохранения настроек. Обновите состояние книги.");
        }
        catch
        {
            return publicationOutcome != null ? WithPreparedFollowUp(publicationOutcome) : new(
                BrowserPreparedWriteDisposition.Blocked, true,
                "Запрос на изменение настроек заблокирован: локальное хранилище требует проверки перед записью.");
        }
    }

    private BrowserPreparedWriteResult CapturePreparedResult(BrowserPreparedWriteResult result)
    {
        // The original participating scope closes from this capture. An explicit
        // publication result must be retained before runtime callbacks or cleanup.
        if (_browserDecision.Value is { } captured)
            captured.Outcome = result.Disposition switch
            {
                BrowserPreparedWriteDisposition.Committed => MainOperationOutcome.Committed,
                BrowserPreparedWriteDisposition.RolledBack => MainOperationOutcome.RolledBack,
                BrowserPreparedWriteDisposition.Uncertain => MainOperationOutcome.Uncertain,
                _ => MainOperationOutcome.Failed
            };
        return result;
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
        var writeLabel = request.OwnerKind == "console" ? "Сохранение настроек" : "Browser-write";
        BrowserPendingTurnStatus pending;
        try { pending = BrowserPendingTurnInspector.Build(_fs, writeLease); }
        catch (InvalidDataException ex)
        {
            return LocalUiSessionLockResult.BlockedBy(null,
                $"{writeLabel} заблокировано: повреждена служебная разметка активного хода ({ex.Message}).");
        }
        if (pending.HasActiveGmTurn)
            return LocalUiSessionLockResult.BlockedBy(null,
                $"{writeLabel} заблокировано: активный GM-turn или rollback/snapshot artifact должен быть завершён до локальной записи.");
        return request.ExistingLease == null
            ? await _lockService.AcquireOrRefreshAsync(writeLease, BuildOwner(request), request.OperationLabel)
            : await _lockService.RefreshAsync(writeLease, request.ExistingLease, request.OperationLabel);
    }
}
