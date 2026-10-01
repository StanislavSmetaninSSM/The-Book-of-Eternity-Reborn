using BookOfEternityClient.Configuration;
using BookOfEternityClient.Services;
using BookOfEternityClient.WebUi;

namespace BookOfEternityClient.Core;

// A menu draft owns no filesystem lease between operations. Publication and
// runtime acceptance use the existing local-write coordinator and journal.
internal sealed class ConsoleSettingsSession
{
    private readonly FileSystemManager _files;
    private readonly StateManager _state;
    private readonly SystemModService _mods;
    private readonly LocalSettingsPreparation _preparation;
    private readonly BrowserLocalWriteCoordinator _coordinator;
    private readonly string _ownerId = $"console:{Environment.MachineName}:{Environment.ProcessId}:{Guid.NewGuid():N}";
    private LocalSettingsBaseline _baseline;

    private ConsoleSettingsSession(FileSystemManager files, StateManager state, SystemModService mods,
        LocalSettingsBaseline baseline, GameSettings draft)
    {
        _files = files;
        _state = state;
        _mods = mods;
        _preparation = new(files, state, mods);
        _coordinator = new(files, new LocalUiSessionLockService(files));
        _baseline = baseline;
        Draft = draft;
    }

    internal GameSettings Draft { get; }
    internal bool RequiresReload { get; private set; }

    internal static async Task<ConsoleSettingsSession> OpenAsync(FileSystemManager files,
        StateManager state, SystemModService mods)
    {
        await BrowserAudioService.SettingsWriteGate.WaitAsync();
        try
        {
            await using var lease = await files.AcquireCanonicalWriteLeaseAsync();
            var generation = files.ReadExistingSessionGeneration(lease)
                ?? throw new InvalidDataException("Сначала завершите начальную подготовку локального хранилища.");
            var snapshot = await state.ReadLocalSettingsAsync(lease);
            files.VerifyCurrentSessionOperation(lease);
            return new(files, state, mods, new(generation, snapshot.Bytes), snapshot.Settings);
        }
        finally { BrowserAudioService.SettingsWriteGate.Release(); }
    }

    internal Task<bool> IsCurrentSetSynchronizedAsync() => throw new NotImplementedException();

    internal async Task ReloadAsync(Func<Task>? refreshRuntime = null)
    {
        await BrowserAudioService.SettingsWriteGate.WaitAsync();
        try
        {
            RequiresReload = true;
            await using var lease = await _files.AcquireCanonicalWriteLeaseAsync();
            VerifyGeneration(lease);
            var snapshot = await _state.ReadLocalSettingsAsync(lease);
            VerifyGeneration(lease);
            _state.Settings.ApplyLoadedValues(snapshot.Settings);
            Draft.ApplyLoadedValues(_state.CaptureRuntimeSnapshot().Settings);
            _baseline = _baseline with { ConfigBytes = snapshot.Bytes };
            if (refreshRuntime != null) await refreshRuntime();
            RequiresReload = false;
        }
        finally { BrowserAudioService.SettingsWriteGate.Release(); }
    }

    internal async Task<IReadOnlyList<SystemModService.SystemModDescriptor>> ReadModsAsync()
    {
        await BrowserAudioService.SettingsWriteGate.WaitAsync();
        try
        {
            await using var lease = await _files.AcquireCanonicalWriteLeaseAsync();
            VerifyGeneration(lease);
            return await _mods.ReadAvailableModsAsync(lease, Draft.EnabledSystemMods);
        }
        finally { BrowserAudioService.SettingsWriteGate.Release(); }
    }

    private void VerifyGeneration(FileSystemManager.CanonicalWriteLease lease)
    {
        _files.VerifyCurrentSessionOperation(lease);
        var actual = _files.ReadExistingSessionGeneration(lease);
        if (!string.Equals(actual, _baseline.Generation, StringComparison.Ordinal))
            throw new SessionReplacedException("Сессия изменилась; текущий экран настроек не может принять другую сессию.",
                _baseline.Generation, actual);
    }

    internal async Task<BrowserPreparedWriteResult> SaveAsync(Func<Task>? refreshRuntime = null)
    {
        await BrowserAudioService.SettingsWriteGate.WaitAsync();
        try
        {
            if (RequiresReload)
                return new(BrowserPreparedWriteDisposition.Blocked, true,
                    "Перед следующей записью перечитайте подтверждённые настройки. Текущий черновик не сохранён повторно.");

            BrowserPreparedWriteResult? established = null;
            PreparedLocalSettings? prepared = null;
            try
            {
                await SessionOperationContext.RunBoundAsync(_files, _baseline.Generation, async () =>
                {
                    established = await _coordinator.ExecutePreparedAsync(
                        new(_ownerId, "Локальные настройки консоли", "Сохранение настроек консоли", OwnerKind: "console"),
                        async lease =>
                        {
                            try { prepared = await _preparation.PrepareAsync(lease, _baseline, Draft); }
                            catch (LocalSettingsBaselineChangedException) { RequiresReload = true; throw; }
                            return new(prepared.Changes, async () =>
                            {
                                _state.Settings.ApplyLoadedValues(prepared.Settings);
                                // A later menu edit must not share nested profile arrays
                                // with accepted settings or with the prepared image.
                                Draft.ApplyLoadedValues(_state.CaptureRuntimeSnapshot().Settings);
                                if (refreshRuntime != null) await refreshRuntime();
                            });
                        });
                });
            }
            catch
            {
                // The outer operation's closing fence can fail after the shared
                // coordinator established a durable or uncertain publication.
                established = established == null
                    ? new(BrowserPreparedWriteDisposition.Blocked, true,
                        "Сохранение заблокировано: игровая сессия или локальное хранилище изменились. Перечитайте настройки.")
                    : established with
                    {
                        NeedsFollowUp = true,
                        Message = established.Disposition == BrowserPreparedWriteDisposition.Committed
                            ? "Настройки сохранены. Обновление консоли или служебная очистка требуют проверки; сохранённые файлы не отменены."
                            : established.Message
                    };
            }

            var result = established!;
            if (result.Disposition == BrowserPreparedWriteDisposition.Committed && prepared != null)
                _baseline = _baseline with { ConfigBytes = prepared.Changes[0].After! };
            RequiresReload |= result.NeedsFollowUp || result.Disposition == BrowserPreparedWriteDisposition.Uncertain;
            if (RequiresReload && result.Disposition == BrowserPreparedWriteDisposition.Blocked)
                result = result with { NeedsFollowUp = true, Message = result.Message + " Перечитайте подтверждённые настройки перед повторной записью." };
            return result;
        }
        finally { BrowserAudioService.SettingsWriteGate.Release(); }
    }
}
