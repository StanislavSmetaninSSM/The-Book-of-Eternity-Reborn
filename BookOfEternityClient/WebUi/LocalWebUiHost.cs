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
        builder.Services.AddSingleton<AudioService>();
        builder.Services.AddSingleton<StateDistributor>();
        builder.Services.AddSingleton<CanonicalStateNormalizer>();
        builder.Services.AddSingleton<QteSceneService>();
        builder.Services.AddSingleton<QteWebInteractionService>();
        builder.Services.AddSingleton<LocalUiSessionLockService>();
        builder.Services.AddSingleton<LocalWebUiSessionStatusService>();
        builder.Services.AddSingleton<ExplorerWebPromptSessionService>();
        builder.Services.AddSingleton<ExplorerWebCommandService>();

        var app = builder.Build();
        app.Services.GetRequiredService<FileSystemManager>().EnsureDirectoryStructure();

        app.MapGet("/", () => Results.Content(BuildShellHtml(), "text/html; charset=utf-8"));
        app.MapGet("/api/health", (LocalWebUiSessionStatusService status) => status.BuildStatus());
        app.MapGet("/api/session", (LocalWebUiSessionStatusService status) => status.BuildStatus());
        app.MapPost("/api/explorer/command", async (ExplorerWebCommandRequest request, ExplorerWebCommandService commandService) =>
            await commandService.ExecuteAsync(request));
        app.MapGet("/api/explorer/prompt-sessions/{sessionId}", (string sessionId, ExplorerWebCommandService commandService) =>
            commandService.GetPromptSession(sessionId));
        app.MapPost("/api/explorer/prompt-sessions/submit", async (ExplorerPromptSessionSubmitRequest request, ExplorerWebCommandService commandService) =>
            await commandService.SubmitPromptSessionAsync(request));
        app.MapPost("/api/explorer/prompt-sessions/cancel", async (ExplorerPromptSessionCancelRequest request, ExplorerWebCommandService commandService) =>
            await commandService.CancelPromptSessionAsync(request));
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
            .message.warning { border-color: rgba(229, 193, 109, .65); }
            .message.error { border-color: rgba(224, 111, 95, .7); }
            .message.success { border-color: rgba(158, 203, 134, .65); }
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
                <p>QTE API: <code>GET /api/qte/state</code></p>
                <p>Сейчас доступны только перенесённые DTO-команды; остальные вернут структурный блокер.</p>
                <button class="secondary" type="button" id="qte-button">Проверить QTE</button>
              </aside>
            </section>
            <section id="result" aria-live="polite">
              <div class="empty">Пока нет результата. Нажмите «Выполнить», чтобы отрисовать первую команду.</div>
            </section>
          </main>
          <script>
            const form = document.getElementById('command-form');
            const input = document.getElementById('command-input');
            const resultRoot = document.getElementById('result');
            document.getElementById('help-button').addEventListener('click', () => {
              input.value = '/help';
              executeCommand('/help');
            });
            document.getElementById('qte-button').addEventListener('click', () => loadQteState());
            form.addEventListener('submit', event => {
              event.preventDefault();
              executeCommand(input.value);
            });

            async function executeCommand(command) {
              resultRoot.replaceChildren(el('div', 'loading', 'Команда выполняется...'));
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

            async function submitPromptSession(sessionId, answers) {
              resultRoot.replaceChildren(el('div', 'loading', 'Отправляю ответы формы...'));
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
              resultRoot.replaceChildren(el('div', 'loading', 'Отменяю форму...'));
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
              resultRoot.replaceChildren(el('div', 'loading', 'Проверяю QTE-сцену...'));
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
              resultRoot.replaceChildren(el('div', 'loading', 'Обрабатываю выбор QTE...'));
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
              resultRoot.replaceChildren(el('div', 'loading', 'Разрешаю QTE-действие...'));
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
              const blocks = result?.blocks ?? [];
              if (blocks.length === 0 && !(result?.actions?.length) && !(result?.prompts?.length)) {
                resultRoot.append(el('div', 'empty', 'Пока нет результата для отображения.'));
              }
              renderNotifications(result?.notifications ?? []);
              for (const block of blocks) resultRoot.append(renderBlock(block));
              renderActions(result?.actions ?? []);
              renderPrompts(result?.prompts ?? [], result?.interactiveSession ?? null);
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
              const node = el('section', `block message ${(block.severity ?? '').toLowerCase()}`);
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
