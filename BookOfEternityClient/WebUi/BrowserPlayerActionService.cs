using System.Text.Json;
using System.Text.Json.Nodes;
using BookOfEternityClient.Core;

namespace BookOfEternityClient.WebUi;

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

    public BrowserPlayerActionService(
        FileSystemManager fs,
        BrowserLocalWriteCoordinator coordinator,
        TimeProvider? timeProvider = null)
    {
        _fs = fs;
        _coordinator = coordinator;
        _timeProvider = timeProvider ?? TimeProvider.System;
    }

    public async Task<BrowserPlayerActionResult> SubmitAsync(BrowserPlayerActionRequest? request)
    {
        var text = request?.Text?.Trim() ?? string.Empty;

        if (string.IsNullOrWhiteSpace(text))
            return new BrowserPlayerActionResult(false, "Введите текст действия.");

        if (text.StartsWith('/'))
            return new BrowserPlayerActionResult(false,
                "Служебные команды не отправляются через основной композитор. Используйте каталог команд.");

        var status = await _coordinator.BuildStatusAsync();
        if (!status.CanStartBrowserWrite)
        {
            var reason = status.PendingTurn.HasActiveGmTurn
                ? "Сейчас ход Мастера — дождитесь завершения текущего хода."
                : "Запись заблокирована другим процессом.";
            return new BrowserPlayerActionResult(false, reason);
        }

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

        var writeResult = await _coordinator.ExecuteAsync(
            writeRequest,
            [PendingPlayerActionPath],
            async () =>
            {
                await _fs.WriteFileAtomicAsync(
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
