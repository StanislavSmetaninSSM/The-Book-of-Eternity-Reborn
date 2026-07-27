using System.Text.Json;
using System.Text.Json.Nodes;
using BookOfEternityClient.Core;

namespace BookOfEternityClient.WebUi;

internal sealed class BrowserPlayerActionServiceHooks
{
    internal Func<Task>? AfterPreflightAsync { get; init; }
}

public sealed record BrowserPlayerActionRequest(
    string Text,
    string? OwnerId = null,
    string? OwnerLabel = null);

public sealed record BrowserPlayerActionResult(
    bool Success,
    string PlayerMessage,
    string? TechnicalDetail = null);

public sealed class BrowserPlayerActionService
{
    private const string PendingPlayerActionPath = "input/pending_player_action.json";

    private readonly FileSystemManager _fs;
    private readonly BrowserLocalWriteCoordinator _coordinator;
    private readonly TimeProvider _timeProvider;
    private readonly BrowserPlayerActionServiceHooks? _hooks;

    public BrowserPlayerActionService(
        FileSystemManager fs,
        BrowserLocalWriteCoordinator coordinator,
        TimeProvider? timeProvider = null)
        : this(fs, coordinator, timeProvider, hooks: null)
    {
    }

    internal BrowserPlayerActionService(
        FileSystemManager fs,
        BrowserLocalWriteCoordinator coordinator,
        TimeProvider? timeProvider,
        BrowserPlayerActionServiceHooks? hooks)
    {
        _fs = fs;
        _coordinator = coordinator;
        _timeProvider = timeProvider ?? TimeProvider.System;
        _hooks = hooks;
    }

    public async Task<BrowserPlayerActionResult> SubmitAsync(BrowserPlayerActionRequest? request)
    {
        var text = request?.Text?.Trim() ?? string.Empty;

        if (string.IsNullOrWhiteSpace(text))
            return new BrowserPlayerActionResult(false, "Введите текст действия.");

        if (text.StartsWith('/'))
            return new BrowserPlayerActionResult(false,
                "Служебные команды не отправляются через основной композитор. Используйте каталог команд.");

        try
        {
            return await _coordinator.RunBoundTransactionAsync(
                writeLease => SubmitBoundAsync(writeLease, request, text));
        }
        catch (SessionReplacedException)
        {
            return new BrowserPlayerActionResult(
                false,
                "Игровая сессия изменилась во время отправки действия. Повторите попытку.");
        }
    }

    private async Task<BrowserPlayerActionResult> SubmitBoundAsync(
        FileSystemManager.CanonicalWriteLease writeLease,
        BrowserPlayerActionRequest? request,
        string text)
    {
        var status = await _coordinator.BuildStatusAsync();
        if (!status.CanStartBrowserWrite)
        {
            var reason = status.PendingTurn.HasActiveGmTurn
                ? "Сейчас ход Мастера — дождитесь завершения текущего хода."
                : "Запись заблокирована другим процессом.";
            return new BrowserPlayerActionResult(false, reason);
        }
        if (_hooks?.AfterPreflightAsync != null)
            await _hooks.AfterPreflightAsync();

        var writeRequest = new BrowserLocalWriteRequest(
            request?.OwnerId,
            request?.OwnerLabel ?? "Композитор действий",
            "Запись действия игрока");

        var payload = new JsonObject
        {
            ["playerAction"] = text,
            ["submittedAtUtc"] = _timeProvider.GetUtcNow().UtcDateTime.ToString("O"),
            ["source"] = "browser-composer"
        };

        var writeResult = await _coordinator.ExecuteAtomicWithinTransactionAsync(
            writeLease,
            writeRequest,
            [PendingPlayerActionPath],
            async transactionLease =>
            {
                await _fs.WriteFileAtomicAsync(
                    transactionLease,
                    PendingPlayerActionPath,
                    JsonSerializer.Serialize(payload, new JsonSerializerOptions { WriteIndented = true }));
            });

        if (!writeResult.Success)
        {
            return new BrowserPlayerActionResult(false,
                writeResult.IsBlocked
                    ? "Запись заблокирована. Повторите попытку позже."
                    : "Не удалось записать действие. Повторите попытку.");
        }

        return new BrowserPlayerActionResult(true,
            "Действие отправлено. Мастер обработает его при следующем ходе.");
    }
}
