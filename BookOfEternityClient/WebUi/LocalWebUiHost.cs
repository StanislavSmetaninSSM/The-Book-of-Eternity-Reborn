using System.Net;
using System.Text.Json;
using BookOfEternityClient.Configuration;
using BookOfEternityClient.Core;
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
        builder.Services.AddSingleton<LocalWebUiSessionStatusService>();
        builder.Services.AddSingleton<ExplorerWebCommandService>();

        var app = builder.Build();
        app.Services.GetRequiredService<FileSystemManager>().EnsureDirectoryStructure();

        app.MapGet("/", () => Results.Content(BuildShellHtml(), "text/html; charset=utf-8"));
        app.MapGet("/api/health", (LocalWebUiSessionStatusService status) => status.BuildStatus());
        app.MapGet("/api/session", (LocalWebUiSessionStatusService status) => status.BuildStatus());
        app.MapPost("/api/explorer/command", async (ExplorerWebCommandRequest request, ExplorerWebCommandService commandService) =>
            await commandService.ExecuteAsync(request));

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
            input {
              flex: 1;
              border: 1px solid var(--line);
              border-radius: .85rem;
              background: rgba(0, 0, 0, .28);
              color: var(--text);
              padding: .85rem 1rem;
              font: inherit;
            }
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
                <p>Сейчас доступны только перенесённые DTO-команды; остальные вернут структурный блокер.</p>
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

            function renderCommandResult(result) {
              resultRoot.replaceChildren();
              const blocks = result?.blocks ?? [];
              if (blocks.length === 0 && !(result?.actions?.length) && !(result?.prompts?.length)) {
                resultRoot.append(el('div', 'empty', 'Пока нет результата для отображения.'));
              }
              renderNotifications(result?.notifications ?? []);
              for (const block of blocks) resultRoot.append(renderBlock(block));
              renderActions(result?.actions ?? []);
              renderPrompts(result?.prompts ?? []);
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

            function renderPrompts(prompts) {
              if (prompts.length === 0) return;
              const node = el('div', 'prompts');
              for (const prompt of prompts) node.append(renderPrompt(prompt));
              resultRoot.append(node);
            }

            function renderPrompt(prompt) {
              const node = el('div', 'block prompt');
              node.append(el('div', 'message-title', prompt.prompt ?? prompt.id ?? 'Требуется ввод'));
              node.append(el('div', 'prompt-kind', `Тип ввода: ${prompt.kind ?? 'unknown'}${prompt.required ? ', обязательно' : ''}`));
              if (Array.isArray(prompt.options) && prompt.options.length > 0) {
                const list = document.createElement('ul');
                for (const option of prompt.options) {
                  const label = option.description ? `${option.label ?? option.value}: ${option.description}` : option.label ?? option.value;
                  list.append(el('li', '', label));
                }
                node.append(list);
              }
              return node;
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
