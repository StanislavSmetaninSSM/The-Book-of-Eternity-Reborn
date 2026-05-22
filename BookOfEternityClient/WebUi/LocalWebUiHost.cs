using System.Net;
using System.Text.Json;
using BookOfEternityClient.Configuration;
using BookOfEternityClient.Core;
using BookOfEternityClient.IO;
using BookOfEternityClient.Services;
using BookOfEternityClient.UI;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace BookOfEternityClient.WebUi;

public sealed record LocalWebUiHostOptions(string BasePath, string Url);

public static class LocalWebUiHost
{
    private static readonly JsonSerializerOptions WebJsonOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true
    };

    public static WebApplication Build(string[] args, LocalWebUiHostOptions options)
    {
        if (!IsLocalUrl(options.Url))
            throw new InvalidOperationException("Local Web UI can only bind to localhost/loopback URLs.");

        var builder = WebApplication.CreateBuilder(new WebApplicationOptions
        {
            Args = args,
            ContentRootPath = options.BasePath
        });

        builder.Logging.ClearProviders();
        builder.Logging.AddDebug();
        builder.WebHost.UseUrls(options.Url);

        builder.Services.Configure<JsonOptions>(json =>
        {
            json.SerializerOptions.PropertyNamingPolicy = WebJsonOptions.PropertyNamingPolicy;
            json.SerializerOptions.WriteIndented = WebJsonOptions.WriteIndented;
        });

        builder.Services.AddSingleton(sp =>
            new FileSystemManager(options.BasePath, sp.GetRequiredService<ILogger<FileSystemManager>>()));
        builder.Services.AddSingleton(new GameSettings());
        builder.Services.AddSingleton(sp =>
            new StateManager(
                sp.GetRequiredService<FileSystemManager>(),
                sp.GetRequiredService<GameSettings>(),
                sp.GetRequiredService<ILogger<StateManager>>()));
        builder.Services.AddSingleton<LocalizationManager>();
        builder.Services.AddSingleton<ValidationService>();
        builder.Services.AddSingleton<CharacteristicsService>();
        builder.Services.AddSingleton<ImageService>();
        builder.Services.AddSingleton<LocalMediaService>();
        builder.Services.AddSingleton<AudioService>();
        builder.Services.AddSingleton<StateDistributor>();
        builder.Services.AddSingleton<CanonicalStateNormalizer>();
        builder.Services.AddSingleton<ScenarioCoreService>();
        builder.Services.AddSingleton<QteSceneService>();
        builder.Services.AddSingleton<QteWebInteractionService>();
        builder.Services.AddSingleton<LocalUiSessionLockService>();
        builder.Services.AddSingleton<BrowserLocalWriteCoordinator>();
        builder.Services.AddSingleton<BrowserMortalWorldWriteService>();
        builder.Services.AddSingleton<LocalWebUiSessionStatusService>();
        builder.Services.AddSingleton<BrowserLifecycleDashboardService>();
        builder.Services.AddSingleton<ExplorerWebPromptSessionService>();
        builder.Services.AddSingleton<ExplorerWebCommandService>();

        var app = builder.Build();
        app.Services.GetRequiredService<FileSystemManager>().EnsureDirectoryStructure();

        app.MapGet("/", () => Results.Content(BuildShellHtml(), "text/html; charset=utf-8"));
        app.MapGet("/assets/map-viewer.css", () => Results.Content(LocalMapViewerAssets.StyleSheet, "text/css; charset=utf-8"));
        app.MapGet("/assets/map-viewer.js", () => Results.Content(LocalMapViewerAssets.Script, "application/javascript; charset=utf-8"));
        app.MapGet("/api/health", async (LocalWebUiSessionStatusService status) => await status.BuildStatusAsync());
        app.MapGet("/api/session", async (LocalWebUiSessionStatusService status) => await status.BuildStatusAsync());
        app.MapGet("/api/lifecycle/dashboard", async (BrowserLifecycleDashboardService lifecycle) =>
            await lifecycle.BuildDashboardAsync());
        app.MapPost("/api/lifecycle/validate", async (BrowserLifecycleDashboardService lifecycle) =>
            await lifecycle.BuildValidationAsync());
        app.MapPost("/api/explorer/command", async (ExplorerWebCommandRequest request, ExplorerWebCommandService commandService) =>
            await commandService.ExecuteAsync(request));
        app.MapGet("/api/explorer/prompt-sessions/{sessionId}", (string sessionId, ExplorerWebCommandService commandService) =>
            commandService.GetPromptSession(sessionId));
        app.MapPost("/api/explorer/prompt-sessions/submit", async (ExplorerPromptSessionSubmitRequest request, ExplorerWebCommandService commandService) =>
            await commandService.SubmitPromptSessionAsync(request));
        app.MapPost("/api/explorer/prompt-sessions/cancel", async (ExplorerPromptSessionCancelRequest request, ExplorerWebCommandService commandService) =>
            await commandService.CancelPromptSessionAsync(request));
        app.MapGet("/api/media/{mediaId}", (string mediaId, LocalMediaService media) =>
        {
            if (!media.TryResolveMediaId(mediaId, out var file, out var error) || file == null)
            {
                var statusCode = error.Contains("не найден", StringComparison.OrdinalIgnoreCase)
                    ? StatusCodes.Status404NotFound
                    : StatusCodes.Status400BadRequest;
                return Results.Json(new { error }, statusCode: statusCode);
            }

            return Results.File(
                file.FullPath,
                file.ContentType,
                fileDownloadName: null,
                enableRangeProcessing: true);
        });
        app.MapGet("/api/qte/state", async (QteWebInteractionService qte) =>
            await qte.BuildStateAsync());
        app.MapPost("/api/qte/offer", async (QteWebOfferDecisionRequest request, QteWebInteractionService qte) =>
            await qte.ResolveOfferDecisionAsync(request));
        app.MapPost("/api/qte/action", async (QteWebActionRequest request, QteWebInteractionService qte) =>
            await qte.ResolveActionAsync(request));

        return app;
    }

    private static bool IsLocalUrl(string value)
    {
        if (!Uri.TryCreate(value, UriKind.Absolute, out var uri))
            return false;

        if (!string.Equals(uri.Scheme, Uri.UriSchemeHttp, StringComparison.OrdinalIgnoreCase) &&
            !string.Equals(uri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (string.Equals(uri.Host, "localhost", StringComparison.OrdinalIgnoreCase))
            return true;

        return IPAddress.TryParse(uri.Host, out var address) && IPAddress.IsLoopback(address);
    }

    private static string BuildShellHtml() =>
        """
        <!doctype html>
        <html lang="ru">
        <head>
          <meta charset="utf-8">
          <meta name="viewport" content="width=device-width, initial-scale=1">
          <title>The Book of Eternity: Reborn - Local Web UI</title>
          <link rel="stylesheet" href="/assets/map-viewer.css">
          <style>
            :root {
              color-scheme: dark;
              --bg: #0f1415;
              --panel: rgba(21, 30, 34, .9);
              --panel-2: rgba(34, 43, 40, .92);
              --line: rgba(222, 183, 99, .32);
              --text: #f1efe3;
              --muted: #a9b2a4;
              --accent: #e1b85e;
              --danger: #e06f5f;
              --warning: #e5c16d;
              --success: #9ecb86;
            }
            * { box-sizing: border-box; }
            body {
              margin: 0;
              min-height: 100vh;
              background:
                radial-gradient(circle at 15% 8%, rgba(225, 184, 94, .2), transparent 24rem),
                radial-gradient(circle at 92% 16%, rgba(82, 127, 120, .18), transparent 22rem),
                linear-gradient(135deg, #0f1415, #172120 52%, #101010);
              color: var(--text);
              font: 16px/1.5 Georgia, "Times New Roman", serif;
            }
            main {
              width: min(78rem, calc(100vw - 2rem));
              margin: 0 auto;
              padding: 2rem 0 3rem;
            }
            .hero {
              display: grid;
              grid-template-columns: minmax(0, 1fr) minmax(18rem, 28rem);
              gap: 1rem;
              align-items: stretch;
            }
            .card {
              border: 1px solid var(--line);
              border-radius: 1.25rem;
              background: var(--panel);
              box-shadow: 0 2rem 5rem rgba(0, 0, 0, .35);
              padding: 1.25rem;
            }
            h1 {
              margin: 0 0 .75rem;
              color: var(--accent);
              font-size: clamp(2.4rem, 7vw, 5rem);
              line-height: .9;
              letter-spacing: -.04em;
            }
            p { color: var(--muted); max-width: 45rem; }
            code { color: var(--accent); }
            form { display: flex; gap: .75rem; margin-top: 1rem; }
            input, select, textarea {
              flex: 1;
              border: 1px solid var(--line);
              border-radius: .85rem;
              background: rgba(0, 0, 0, .28);
              color: var(--text);
              padding: .85rem 1rem;
              font: inherit;
            }
            textarea { width: 100%; resize: vertical; }
            select, .prompt input { width: 100%; margin-top: .65rem; }
            button {
              border: 0;
              border-radius: .85rem;
              background: var(--accent);
              color: #1b1710;
              cursor: pointer;
              font: 700 1rem/1 Georgia, "Times New Roman", serif;
              padding: .9rem 1.1rem;
            }
            button.secondary {
              border: 1px solid var(--line);
              background: transparent;
              color: var(--accent);
            }
            button.danger {
              background: var(--danger);
              color: #1f0d0a;
            }
            button:disabled {
              cursor: not-allowed;
              opacity: .62;
            }
            #result {
              display: grid;
              gap: 1rem;
              margin-top: 1rem;
            }
            .game-shell {
              display: grid;
              grid-template-columns: minmax(16rem, 22rem) 1fr;
              gap: 1rem;
              margin-top: 1rem;
              align-items: start;
            }
            .command-palette {
              position: sticky;
              top: 1rem;
            }
            .nav-group {
              border-top: 1px solid rgba(222, 183, 99, .18);
              margin-top: 1rem;
              padding-top: 1rem;
            }
            .nav-group h3 {
              color: var(--accent);
              font-size: 1rem;
              margin: 0 0 .6rem;
            }
            .nav-actions {
              display: grid;
              gap: .45rem;
            }
            .nav-actions button {
              border: 1px solid rgba(222, 183, 99, .2);
              background: rgba(0, 0, 0, .18);
              color: var(--text);
              line-height: 1.25;
              text-align: left;
            }
            .empty, .loading {
              border: 1px dashed var(--line);
              border-radius: 1rem;
              color: var(--muted);
              padding: 1rem;
            }
            .block {
              border: 1px solid var(--line);
              border-radius: 1rem;
              background: var(--panel-2);
              padding: 1rem;
            }
            .block h2, .block h3 { color: var(--accent); margin: 0 0 .75rem; }
            .image-block {
              display: grid;
              gap: .75rem;
            }
            .image-block img {
              width: min(100%, 46rem);
              max-height: 34rem;
              object-fit: contain;
              border: 1px solid rgba(222, 183, 99, .24);
              border-radius: .85rem;
              background: rgba(0, 0, 0, .28);
            }
            .image-meta {
              color: var(--muted);
              font-size: .92rem;
            }
            .message.warning { border-color: rgba(229, 193, 109, .65); }
            .message.error { border-color: rgba(224, 111, 95, .7); }
            .message.success { border-color: rgba(158, 203, 134, .65); }
            .progress-state {
              border-color: rgba(225, 184, 94, .6);
              background: linear-gradient(135deg, rgba(225, 184, 94, .14), rgba(0, 0, 0, .16));
            }
            .message-title { color: var(--accent); font-weight: 700; margin-bottom: .35rem; }
            table { width: 100%; border-collapse: collapse; }
            th, td {
              border-bottom: 1px solid rgba(222, 183, 99, .18);
              padding: .55rem .5rem;
              text-align: left;
              vertical-align: top;
            }
            th { color: var(--accent); font-weight: 700; }
            ul, ol { margin: .35rem 0 0 1.25rem; padding: 0; }
            .kv { display: grid; grid-template-columns: minmax(8rem, 16rem) 1fr; gap: .45rem .9rem; }
            .kv-key { color: var(--muted); }
            pre {
              overflow: auto;
              border-radius: .8rem;
              background: rgba(0, 0, 0, .34);
              padding: 1rem;
            }
            .actions, .prompts { display: flex; flex-wrap: wrap; gap: .5rem; margin-top: .75rem; }
            .lifecycle {
              margin-top: 1rem;
            }
            .lifecycle-grid {
              display: grid;
              gap: .75rem;
              grid-template-columns: repeat(auto-fit, minmax(13rem, 1fr));
              margin-top: .75rem;
            }
            .status-pill {
              display: inline-block;
              border: 1px solid var(--line);
              border-radius: 999px;
              color: var(--accent);
              padding: .25rem .6rem;
            }
            .lifecycle-actions {
              display: flex;
              flex-wrap: wrap;
              gap: .5rem;
              margin-top: .9rem;
            }
            .qte-actions { display: flex; flex-wrap: wrap; gap: .5rem; margin-top: .9rem; }
            .qte-action-card {
              border: 1px solid rgba(222, 183, 99, .18);
              border-radius: .9rem;
              background: rgba(0, 0, 0, .18);
              padding: .85rem;
              margin-top: .75rem;
            }
            .qte-meta {
              color: var(--muted);
              font-size: .92rem;
              margin-top: .25rem;
            }
            .prompt {
              flex: 1 1 18rem;
            }
            .prompt-kind {
              color: var(--muted);
              font-size: .9rem;
              margin-top: .25rem;
            }
            @media (max-width: 760px) {
              .hero { grid-template-columns: 1fr; }
              .game-shell { grid-template-columns: 1fr; }
              .command-palette { position: static; }
              form { flex-direction: column; }
              .kv { grid-template-columns: 1fr; }
            }
          </style>
        </head>
        <body>
          <main>
            <section class="hero">
              <div class="card">
                <h1>The Book of Eternity</h1>
                <p>Локальная браузерная оболочка подключается к тому же C# клиенту и тем же данным <code>game_session</code>. Игровая логика остаётся на стороне клиента; браузер только отправляет команды и рендерит DTO.</p>
                <form id="command-form">
                  <input id="command-input" name="command" value="/help" autocomplete="off" aria-label="Команда ExplorerMode">
                  <button type="submit">Выполнить</button>
                  <button class="secondary" type="button" id="help-button">/help</button>
                </form>
              </div>
              <aside class="card">
                <h2>Статус</h2>
                <p>Проверка сессии: <code>/api/health</code></p>
                <p>Командный API: <code>POST /api/explorer/command</code></p>
                <p>Формы команд: <code>POST /api/explorer/prompt-sessions/submit</code></p>
                <p>Изображения: <code>/api/media/{id}</code></p>
                <p>QTE API: <code>GET /api/qte/state</code></p>
                <p>Панель состояния: <code>/api/lifecycle/dashboard</code></p>
                <p>Валидация: <code>POST /api/lifecycle/validate</code></p>
                <p>Сейчас доступны только перенесённые DTO-команды; остальные вернут структурный блокер.</p>
                <button class="secondary" type="button" id="lifecycle-dashboard-button">Обновить панель состояния</button>
                <button class="secondary" type="button" id="lifecycle-validate-button">Проверить валидацию</button>
                <button class="secondary" type="button" id="qte-button">Проверить QTE</button>
              </aside>
            </section>
            <section id="lifecycle-panel" class="card lifecycle" aria-live="polite">
              <h2>Панель состояния</h2>
              <div class="empty">Загружаю состояние локальной сессии...</div>
            </section>
            <section class="game-shell">
              <aside class="card command-palette">
                <h2>Командная палитра</h2>
                <p>Выберите раздел или введите команду вручную. Английские токены показаны только как технические команды.</p>
                <input id="command-palette-filter" type="search" placeholder="Фильтр: квесты, бой, Сияющая Обитель..." aria-label="Фильтр командной палитры">
                <div class="nav-group">
                  <h3>Мир смертных</h3>
                  <div class="nav-actions">
                    <button type="button" data-command="/status">Статус героя</button>
                    <button type="button" data-command="/quests">Квесты</button>
                    <button type="button" data-command="/inv">Инвентарь</button>
                    <button type="button" data-command="/map">Карта</button>
                    <button type="button" data-command="/npc">Персонажи</button>
                    <button type="button" data-command="/factions">Фракции</button>
                    <button type="button" data-command="/combat">Бой</button>
                    <button type="button" data-command="/gallery">Галерея</button>
                  </div>
                </div>
                <div class="nav-group">
                  <h3>Море Хаоса</h3>
                  <div class="nav-actions">
                    <button type="button" data-command="/chaos_sea">Обзор Моря Хаоса</button>
                    <button type="button" data-command="/guardians">Хранители</button>
                    <button type="button" data-command="/abode_power">Сила Обители</button>
                    <button type="button" data-command="/guardian_projects">Проекты Хранителей</button>
                    <button type="button" data-command="/abode_offering">Подношение Обители</button>
                    <button type="button" data-command="/found_guardian_mantle">Основание мантии</button>
                  </div>
                </div>
                <div class="nav-group">
                  <h3>Сияющая Обитель</h3>
                  <div class="nav-actions">
                    <button type="button" data-command="/shining_abode">Обзор Обители</button>
                    <button type="button" data-command="/shining_politics">Политика</button>
                    <button type="button" data-command="/shining_treasury">Казначейство</button>
                    <button type="button" data-command="/source_of_light">Источник Света</button>
                    <button type="button" data-command="/сареф">Скрытая нить</button>
                    <button type="button" data-command="/сареф найти_крылья">Поиск Крыльев</button>
                  </div>
                </div>
                <div class="nav-group">
                  <h3>Духовный бой</h3>
                  <div class="nav-actions">
                    <button type="button" data-command="/spiritual_conflict">Текущий конфликт</button>
                    <button type="button" data-command="/spiritual_combat_log">Журнал боя</button>
                    <button type="button" data-command="/spiritual_combat_help">Справка боя</button>
                    <button type="button" data-command="/spiritual_arts">Духовные искусства</button>
                    <button type="button" data-command="/spiritual_action">Духовное действие</button>
                  </div>
                </div>
                <div class="nav-group">
                  <h3>История и архив</h3>
                  <div class="nav-actions">
                    <button type="button" data-command="/story">Рассказ</button>
                    <button type="button" data-command="/chronicle">Хроника</button>
                    <button type="button" data-command="/codex">Кодекс</button>
                    <button type="button" data-command="/soul">Душа</button>
                    <button type="button" data-command="/afterlife_archive">Архив души</button>
                    <button type="button" data-command="/archive_candidates">Кандидаты в Архив</button>
                    <button type="button" data-command="/воспоминание">Воспоминание</button>
                  </div>
                </div>
                <div class="nav-group">
                  <h3>Диагностика</h3>
                  <div class="nav-actions">
                    <button type="button" data-command="/validate">Валидация</button>
                    <button type="button" data-command="/debug">Отладка</button>
                    <button type="button" data-command="/math">Математик</button>
                    <button type="button" data-command="/gm">Заметки ГМа</button>
                    <button type="button" data-command="/mods">Моды</button>
                    <button type="button" data-command="/system_guardians">Извечные Хранители</button>
                  </div>
                </div>
              </aside>
              <section id="result" aria-live="polite">
                <div class="empty">Пока нет результата. Нажмите «Выполнить», чтобы отрисовать первую команду.</div>
              </section>
            </section>
          </main>
          <script src="/assets/map-viewer.js"></script>
          <script>
            const form = document.getElementById('command-form');
            const input = document.getElementById('command-input');
            const resultRoot = document.getElementById('result');
            const lifecyclePanel = document.getElementById('lifecycle-panel');
            const paletteFilter = document.getElementById('command-palette-filter');
            const commandButtons = [...document.querySelectorAll('[data-command]')];
            document.getElementById('help-button').addEventListener('click', () => {
              input.value = '/help';
              executeCommand('/help');
            });
            document.getElementById('qte-button').addEventListener('click', () => loadQteState());
            document.getElementById('lifecycle-dashboard-button').addEventListener('click', () => loadLifecycleDashboard());
            document.getElementById('lifecycle-validate-button').addEventListener('click', () => runLifecycleValidation());
            form.addEventListener('submit', event => {
              event.preventDefault();
              executeCommand(input.value);
            });
            for (const button of commandButtons) {
              button.addEventListener('click', () => {
                const command = button.dataset.command ?? '';
                input.value = command;
                executeCommand(command);
              });
            }
            paletteFilter.addEventListener('input', filterCommandPalette);
            loadLifecycleDashboard();

            async function executeCommand(command) {
              resultRoot.replaceChildren(renderProgressState('Команда выполняется', 'Отправляю запрос локальному C# клиенту...'));
              try {
                const response = await fetch('/api/explorer/command', {
                  method: 'POST',
                  headers: { 'Content-Type': 'application/json' },
                  body: JSON.stringify({ command })
                });
                const payload = await response.json();
                if (!response.ok) {
                  renderError(`HTTP ${response.status}`, payload);
                  return;
                }
                renderCommandResult(payload);
              } catch (error) {
                renderError('Не удалось выполнить команду', error?.message ?? String(error));
              }
            }

            async function loadLifecycleDashboard() {
              lifecyclePanel.replaceChildren(el('h2', '', 'Панель состояния'), el('div', 'loading', 'Читаю локальную сессию...'));
              try {
                const response = await fetch('/api/lifecycle/dashboard');
                const payload = await response.json();
                if (!response.ok) {
                  renderLifecycleError(`HTTP ${response.status}`, payload);
                  return;
                }
                renderLifecycleDashboard(payload);
              } catch (error) {
                renderLifecycleError('Не удалось прочитать панель состояния', error?.message ?? String(error));
              }
            }

            async function runLifecycleValidation() {
              lifecyclePanel.replaceChildren(el('h2', '', 'Панель состояния'), el('div', 'loading', 'Запускаю валидацию...'));
              try {
                const response = await fetch('/api/lifecycle/validate', { method: 'POST' });
                const payload = await response.json();
                if (!response.ok) {
                  renderLifecycleError(`HTTP ${response.status}`, payload);
                  return;
                }
                lifecyclePanel.replaceChildren(el('h2', '', 'Проверка валидации'));
                lifecyclePanel.append(renderValidationSummary(payload));
              } catch (error) {
                renderLifecycleError('Не удалось запустить валидацию', error?.message ?? String(error));
              }
            }

            function renderLifecycleDashboard(dashboard) {
              lifecyclePanel.replaceChildren(el('h2', '', 'Панель состояния'));
              const grid = el('div', 'lifecycle-grid');
              grid.append(renderLifecycleMetric('Душа', dashboard?.soul?.name ?? 'Неизвестная душа'));
              grid.append(renderLifecycleMetric('Царство', dashboard?.soul?.realmLabel ?? dashboard?.soul?.currentRealm ?? 'Неизвестно'));
              grid.append(renderLifecycleMetric('Воплощение', String(dashboard?.soul?.currentIncarnation ?? 0)));
              grid.append(renderLifecycleMetric('Локальная запись', dashboard?.canStartBrowserWrite ? 'доступна' : 'заблокирована'));
              grid.append(renderLifecycleMetric('Ход ГМа', dashboard?.pendingTurn?.hasActiveGmTurn ? 'активен' : 'нет'));
              grid.append(renderLifecycleMetric('Валидация', dashboard?.validation?.statusLabel ?? 'нет данных'));
              lifecyclePanel.append(grid);

              if (dashboard?.pendingTurn?.message) {
                lifecyclePanel.append(renderMessage({ severity: dashboard.pendingTurn.hasActiveGmTurn ? 'Warning' : 'Success', title: 'Протокол хода ГМа', message: dashboard.pendingTurn.message }));
              }
              for (const item of dashboard?.guidance ?? []) {
                lifecyclePanel.append(renderMessage({ severity: item.severity ?? 'Info', title: item.title, message: item.message }));
              }
              lifecyclePanel.append(renderValidationSummary(dashboard?.validation));
              renderLifecycleEntrypoints(dashboard?.entrypoints ?? []);
            }

            function renderLifecycleMetric(label, value) {
              const node = el('div', 'block');
              node.append(el('div', 'kv-key', label));
              node.append(el('div', 'status-pill', value));
              return node;
            }

            function renderValidationSummary(validation) {
              const node = el('section', 'block');
              node.append(el('h3', '', validation?.statusLabel ?? 'Валидация недоступна'));
              node.append(el('p', '', `Всего: ${validation?.issueCount ?? 0}, ошибок: ${validation?.errorCount ?? 0}, предупреждений: ${validation?.warningCount ?? 0}`));
              if ((validation?.groups ?? []).length > 0) {
                const table = document.createElement('table');
                const thead = document.createElement('thead');
                const headerRow = document.createElement('tr');
                for (const column of ['Severity', 'Category', 'Section', 'Count']) headerRow.append(el('th', '', column));
                thead.append(headerRow);
                table.append(thead);
                const tbody = document.createElement('tbody');
                for (const group of validation.groups ?? []) {
                  const tr = document.createElement('tr');
                  tr.append(el('td', '', group.severity ?? ''));
                  tr.append(el('td', '', group.category ?? ''));
                  tr.append(el('td', '', group.section ?? ''));
                  tr.append(el('td', '', String(group.count ?? 0)));
                  tbody.append(tr);
                }
                table.append(tbody);
                node.append(table);
              }
              const issues = validation?.issues ?? [];
              if (issues.length > 0) {
                const list = document.createElement('ul');
                for (const issue of issues.slice(0, 12)) {
                  const code = issue.code ? ` [${issue.code}]` : '';
                  list.append(el('li', '', `${issue.filePath}${code}: ${issue.message}`));
                }
                node.append(list);
              }
              return node;
            }

            function renderLifecycleEntrypoints(entrypoints) {
              if (entrypoints.length === 0) return;
              const actions = el('div', 'lifecycle-actions');
              for (const entry of entrypoints) {
                const button = el('button', entry.enabled ? 'secondary' : 'secondary', entry.label ?? entry.command ?? 'Действие');
                button.type = 'button';
                button.disabled = entry.enabled === false;
                if (entry.command) {
                  button.addEventListener('click', () => {
                    input.value = entry.command;
                    executeCommand(entry.command);
                  });
                }
                actions.append(button);
              }
              lifecyclePanel.append(actions);
            }

            function renderLifecycleError(title, details) {
              lifecyclePanel.replaceChildren(el('h2', '', 'Панель состояния'), renderMessage({ severity: 'Error', title, message: typeof details === 'string' ? details : JSON.stringify(details) }));
            }

            async function submitPromptSession(sessionId, answers) {
              resultRoot.replaceChildren(renderProgressState('Форма отправляется', 'Проверяю ответы и выполняю локальный write-flow...'));
              try {
                const response = await fetch('/api/explorer/prompt-sessions/submit', {
                  method: 'POST',
                  headers: { 'Content-Type': 'application/json' },
                  body: JSON.stringify({ sessionId, answers })
                });
                const payload = await response.json();
                if (!response.ok) {
                  renderError(`HTTP ${response.status}`, payload);
                  return;
                }
                renderCommandResult(payload);
              } catch (error) {
                renderError('Не удалось отправить форму', error?.message ?? String(error));
              }
            }

            async function cancelPromptSession(sessionId) {
              resultRoot.replaceChildren(renderProgressState('Форма отменяется', 'Освобождаю локальную UI-блокировку...'));
              try {
                const response = await fetch('/api/explorer/prompt-sessions/cancel', {
                  method: 'POST',
                  headers: { 'Content-Type': 'application/json' },
                  body: JSON.stringify({ sessionId })
                });
                const payload = await response.json();
                if (!response.ok) {
                  renderError(`HTTP ${response.status}`, payload);
                  return;
                }
                renderCommandResult(payload);
              } catch (error) {
                renderError('Не удалось отменить форму', error?.message ?? String(error));
              }
            }

            async function loadQteState() {
              resultRoot.replaceChildren(renderProgressState('QTE', 'Проверяю QTE-сцену...'));
              try {
                const response = await fetch('/api/qte/state');
                const payload = await response.json();
                if (!response.ok) {
                  renderError(`HTTP ${response.status}`, payload);
                  return;
                }
                renderQteState(payload);
              } catch (error) {
                renderError('Не удалось прочитать QTE', error?.message ?? String(error));
              }
            }

            async function postQteOffer(decision) {
              resultRoot.replaceChildren(renderProgressState('QTE', 'Обрабатываю выбор QTE...'));
              try {
                const response = await fetch('/api/qte/offer', {
                  method: 'POST',
                  headers: { 'Content-Type': 'application/json' },
                  body: JSON.stringify({ decision })
                });
                const payload = await response.json();
                if (!response.ok) {
                  renderError(`HTTP ${response.status}`, payload);
                  return;
                }
                renderQteState(payload);
              } catch (error) {
                renderError('Не удалось обработать QTE', error?.message ?? String(error));
              }
            }

            async function postQteAction(actionId, grade) {
              resultRoot.replaceChildren(renderProgressState('QTE', 'Разрешаю QTE-действие...'));
              try {
                const response = await fetch('/api/qte/action', {
                  method: 'POST',
                  headers: { 'Content-Type': 'application/json' },
                  body: JSON.stringify({ actionId, grade })
                });
                const payload = await response.json();
                if (!response.ok) {
                  renderError(`HTTP ${response.status}`, payload);
                  return;
                }
                renderQteState(payload);
              } catch (error) {
                renderError('Не удалось разрешить QTE-действие', error?.message ?? String(error));
              }
            }

            function renderCommandResult(result) {
              resultRoot.replaceChildren();
              if (result?.state) {
                resultRoot.append(renderProgressState(describeExecutionState(result.state), describeExecutionHint(result)));
              }
              const blocks = result?.blocks ?? [];
              if (blocks.length === 0 && !(result?.actions?.length) && !(result?.prompts?.length)) {
                resultRoot.append(el('div', 'empty', 'Пока нет результата для отображения.'));
              }
              renderNotifications(result?.notifications ?? []);
              for (const block of blocks) resultRoot.append(renderBlock(block));
              renderActions(result?.actions ?? []);
              renderPrompts(result?.prompts ?? [], result?.interactiveSession ?? null);
            }

            function filterCommandPalette() {
              const query = (paletteFilter.value ?? '').trim().toLowerCase();
              for (const button of commandButtons) {
                const haystack = `${button.textContent ?? ''} ${button.dataset.command ?? ''}`.toLowerCase();
                button.hidden = query.length > 0 && !haystack.includes(query);
              }
            }

            function renderProgressState(title, message) {
              return renderMessage({ severity: 'Info', title, message, extraClass: 'progress-state' });
            }

            function describeExecutionState(state) {
              switch (state) {
                case 'Completed': return 'Готово';
                case 'RequiresInput': return 'Требуется ввод';
                case 'Pending': return 'Ожидание';
                case 'Blocked': return 'Заблокировано';
                case 'Failed': return 'Ошибка';
                default: return state ?? 'Состояние';
              }
            }

            function describeExecutionHint(result) {
              if (result?.state === 'RequiresInput') return 'Заполните форму ниже. Локальная блокировка удерживается только для команд, которые пишут файлы.';
              if (result?.state === 'Pending') return 'Есть активный ход ГМа, rollback/snapshot или другой локальный блокер. Проверьте панель состояния.';
              if (result?.state === 'Blocked') return 'Команда распознана, но сейчас не может быть выполнена.';
              if (result?.state === 'Failed') return 'Команда завершилась ошибкой; детали показаны ниже.';
              return 'Результат получен от локального клиента.';
            }

            function renderQteState(state) {
              resultRoot.replaceChildren();
              if (state?.error) {
                resultRoot.append(renderMessage({ severity: 'Error', title: 'QTE ошибка', message: state.error }));
                return;
              }
              if (state?.notification) {
                resultRoot.append(renderMessage({ severity: 'Info', title: 'QTE', message: state.notification }));
              }
              if (state?.state === 'Offer' && state.offer) {
                resultRoot.append(renderQteOffer(state.offer));
                return;
              }
              if ((state?.state === 'Active' || state?.activeScene) && state.activeScene) {
                if (state.resolution?.resultText) {
                  resultRoot.append(renderMessage({ severity: 'Info', title: 'Промежуточный результат', message: state.resolution.resultText }));
                }
                resultRoot.append(renderQteActiveScene(state.activeScene));
                return;
              }
              if (state?.state === 'Completed' && state.completion) {
                resultRoot.append(renderMessage({ severity: 'Success', title: 'QTE завершено', message: state.completion.summary ?? state.completion.outcomeId ?? 'Сцена завершена.' }));
                return;
              }
              if (state?.state === 'Declined') {
                resultRoot.append(renderMessage({ severity: 'Warning', title: 'QTE отклонено', message: state.notification ?? 'Сцена отклонена.' }));
                return;
              }
              resultRoot.append(el('div', 'empty', state?.lastResolvedReminder ?? 'Активной QTE-сцены нет.'));
            }

            function renderQteOffer(offer) {
              const node = el('section', 'block qte-offer');
              node.append(el('h2', '', offer.title ?? 'QTE событие'));
              if (offer.offerText) node.append(el('p', '', offer.offerText));
              if (offer.introNarrative) node.append(el('p', '', offer.introNarrative));
              if (offer.cinematicJustification) node.append(el('div', 'qte-meta', `Почему QTE: ${offer.cinematicJustification}`));
              if (offer.declineHint) node.append(el('div', 'qte-meta', offer.declineHint));
              const actions = el('div', 'qte-actions');
              const accept = el('button', '', 'Принять QTE');
              accept.type = 'button';
              accept.addEventListener('click', () => postQteOffer('accept'));
              const decline = el('button', 'secondary', 'Отклонить');
              decline.type = 'button';
              decline.addEventListener('click', () => postQteOffer('decline'));
              actions.append(accept, decline);
              node.append(actions);
              return node;
            }

            function renderQteActiveScene(scene) {
              const node = el('section', 'block qte-active');
              node.append(el('h2', '', scene.title ?? 'QTE сцена'));
              const chapter = scene.currentChapter;
              if (!chapter) {
                node.append(el('div', 'empty', 'Текущая глава QTE не найдена.'));
                return node;
              }
              if (chapter.title) node.append(el('h3', '', chapter.title));
              if (chapter.narrative) node.append(el('p', '', chapter.narrative));
              for (const action of chapter.actions ?? []) node.append(renderQteAction(action));
              return node;
            }

            function renderQteAction(action) {
              const node = el('div', 'qte-action-card');
              node.append(el('div', 'message-title', action.label ?? action.actionId ?? 'Действие'));
              node.append(el('div', 'qte-meta', `Проверка: ${action.checkType ?? 'unknown'}, сложность ${action.baseDifficulty ?? '?'}, характеристика ${action.primaryCharacteristic ?? '-'}`));
              const actions = el('div', 'qte-actions');
              if (action.requiresSubmittedGrade === false) {
                const button = el('button', '', 'Выбрать');
                button.type = 'button';
                button.addEventListener('click', () => postQteAction(action.actionId, null));
                actions.append(button);
              } else {
                for (const grade of action.gradeOptions ?? ['success', 'partial', 'fail']) {
                  const button = el('button', grade === 'fail' ? 'danger' : grade === 'partial' ? 'secondary' : '', qteGradeLabel(grade));
                  button.type = 'button';
                  button.addEventListener('click', () => postQteAction(action.actionId, grade));
                  actions.append(button);
                }
              }
              node.append(actions);
              return node;
            }

            function qteGradeLabel(grade) {
              switch (grade) {
                case 'success': return 'Успех';
                case 'partial': return 'Частичный успех';
                case 'fail': return 'Провал';
                default: return grade ?? 'Результат';
              }
            }

            function renderBlock(block) {
              switch (block?.kind) {
                case 'text': return el('div', `block text ${block.tone ?? ''}`, block.text ?? '');
                case 'panel': return renderPanel(block);
                case 'table': return renderTable(block);
                case 'list': return renderList(block);
                case 'keyValueGrid': return renderKeyValueGrid(block);
                case 'message': return renderMessage(block);
                case 'rawJson': return renderRawJson(block);
                case 'image': return renderImageBlock(block);
                case 'map': return BookOfEternityMapViewer.renderMapBlock(block, { blockClass: 'block' });
                default: return renderMessage({ severity: 'Warning', title: 'Неизвестный блок', message: JSON.stringify(block) });
              }
            }

            function renderPanel(block) {
              const node = el('section', 'block panel');
              if (block.title) node.append(el('h2', '', block.title));
              for (const child of block.blocks ?? []) node.append(renderBlock(child));
              return node;
            }

            function renderTable(block) {
              const wrap = el('section', 'block table-block');
              if (block.title) wrap.append(el('h2', '', block.title));
              const table = document.createElement('table');
              const thead = document.createElement('thead');
              const headerRow = document.createElement('tr');
              for (const column of block.columns ?? []) headerRow.append(el('th', '', column));
              thead.append(headerRow);
              table.append(thead);
              const tbody = document.createElement('tbody');
              for (const row of block.rows ?? []) {
                const tr = document.createElement('tr');
                for (const cell of row.cells ?? []) tr.append(el('td', '', cell));
                tbody.append(tr);
              }
              table.append(tbody);
              wrap.append(table);
              return wrap;
            }

            function renderList(block) {
              const node = el('section', 'block list-block');
              const list = document.createElement(block.ordered ? 'ol' : 'ul');
              for (const item of block.items ?? []) list.append(el('li', '', item));
              node.append(list);
              return node;
            }

            function renderKeyValueGrid(block) {
              const node = el('section', 'block kv');
              for (const item of block.items ?? []) {
                node.append(el('div', 'kv-key', item.key ?? ''));
                node.append(el('div', 'kv-value', item.value ?? ''));
              }
              return node;
            }

            function renderMessage(block) {
              const extraClass = block.extraClass ? ` ${block.extraClass}` : '';
              const node = el('section', `block message ${(block.severity ?? '').toLowerCase()}${extraClass}`);
              if (block.title) node.append(el('div', 'message-title', block.title));
              node.append(el('div', '', block.message ?? ''));
              return node;
            }

            function renderRawJson(block) {
              const node = el('section', 'block raw-json');
              if (block.title) node.append(el('h2', '', block.title));
              node.append(el('pre', '', JSON.stringify(block.json ?? null, null, 2)));
              return node;
            }

            function renderImageBlock(block) {
              const node = el('section', 'block image-block');
              if (block.title) node.append(el('h2', '', block.title));
              const image = document.createElement('img');
              image.src = block.url ?? '';
              image.alt = block.altText ?? block.title ?? 'Изображение';
              image.loading = 'lazy';
              image.addEventListener('error', () => {
                image.replaceWith(el('div', 'empty', 'Изображение не найдено или недоступно.'));
              });
              node.append(image);
              node.append(el('div', 'image-meta', `${block.relativePath ?? ''} · ${block.contentType ?? 'image'} · ${block.length ?? 0} байт`));
              return node;
            }

            function renderNotifications(notifications) {
              for (const notification of notifications) {
                resultRoot.append(renderMessage({
                  severity: notification.severity ?? 'Info',
                  title: notification.title ?? 'Уведомление',
                  message: notification.message ?? ''
                }));
              }
            }

            function renderActions(actions) {
              if (actions.length === 0) return;
              const node = el('div', 'actions');
              for (const action of actions) {
                const button = el('button', action.style === 'Danger' ? 'danger' : 'secondary', action.label ?? action.id ?? 'Действие');
                button.type = 'button';
                button.dataset.actionId = action.id ?? '';
                button.dataset.command = action.command ?? '';
                button.disabled = action.requiresConfirmation === true || !action.command;
                if (action.command) {
                  button.addEventListener('click', () => {
                    input.value = action.command;
                    executeCommand(action.command);
                  });
                }
                node.append(button);
              }
              resultRoot.append(node);
            }

            function renderPrompts(prompts, session) {
              if (prompts.length === 0) return;
              const node = session ? el('form', 'prompts') : el('div', 'prompts');
              for (const prompt of prompts) node.append(renderPrompt(prompt, Boolean(session)));
              if (session) {
                const submit = el('button', '', 'Отправить форму');
                submit.type = 'submit';
                const cancel = el('button', 'secondary', 'Отменить форму');
                cancel.type = 'button';
                cancel.addEventListener('click', () => cancelPromptSession(session.sessionId));
                node.append(submit, cancel);
                node.addEventListener('submit', event => {
                  event.preventDefault();
                  submitPromptSession(session.sessionId, collectPromptAnswers(node));
                });
              }
              resultRoot.append(node);
            }

            function renderPrompt(prompt, interactive) {
              const node = el('div', 'block prompt');
              node.append(el('div', 'message-title', prompt.prompt ?? prompt.id ?? 'Требуется ввод'));
              node.append(el('div', 'prompt-kind', `Тип ввода: ${prompt.kind ?? 'unknown'}${prompt.required ? ', обязательно' : ''}`));
              if (interactive) {
                node.append(renderPromptInput(prompt));
              } else if (Array.isArray(prompt.options) && prompt.options.length > 0) {
                const list = document.createElement('ul');
                for (const option of prompt.options) {
                  const label = option.description ? `${option.label ?? option.value}: ${option.description}` : option.label ?? option.value;
                  list.append(el('li', '', label));
                }
                node.append(list);
              }
              return node;
            }

            function renderPromptInput(prompt) {
              if (prompt.kind === 'selection') {
                const select = document.createElement('select');
                select.dataset.promptId = prompt.id ?? '';
                for (const option of prompt.options ?? []) {
                  const opt = document.createElement('option');
                  opt.value = option.value ?? '';
                  opt.textContent = option.description ? `${option.label ?? option.value} — ${option.description}` : option.label ?? option.value ?? '';
                  opt.disabled = option.disabled === true;
                  select.append(opt);
                }
                return select;
              }
              if (prompt.kind === 'confirmation') {
                const label = document.createElement('label');
                const input = document.createElement('input');
                input.type = 'checkbox';
                input.dataset.promptId = prompt.id ?? '';
                input.checked = prompt.defaultValue === true;
                label.append(input, document.createTextNode(' Да'));
                return label;
              }
              if (prompt.kind === 'longTextInput') {
                const textarea = document.createElement('textarea');
                textarea.dataset.promptId = prompt.id ?? '';
                textarea.placeholder = prompt.placeholder ?? '';
                textarea.value = prompt.defaultValue ?? '';
                textarea.rows = prompt.minLines ?? 4;
                return textarea;
              }
              const input = document.createElement('input');
              input.dataset.promptId = prompt.id ?? '';
              input.placeholder = prompt.placeholder ?? '';
              input.value = prompt.defaultValue ?? '';
              return input;
            }

            function collectPromptAnswers(form) {
              const answers = {};
              for (const field of form.querySelectorAll('[data-prompt-id]')) {
                const id = field.dataset.promptId;
                if (!id) continue;
                if (field.type === 'checkbox') answers[id] = field.checked;
                else answers[id] = field.value ?? '';
              }
              return answers;
            }

            function renderError(title, details) {
              resultRoot.replaceChildren(renderMessage({ severity: 'Error', title, message: typeof details === 'string' ? details : JSON.stringify(details) }));
            }

            function el(tag, className, text) {
              const node = document.createElement(tag);
              if (className) node.className = className;
              if (text !== undefined && text !== null) node.textContent = text;
              return node;
            }
          </script>
        </body>
        </html>
        """;
}
