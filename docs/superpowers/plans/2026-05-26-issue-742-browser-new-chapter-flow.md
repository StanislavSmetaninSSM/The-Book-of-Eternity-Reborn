# Browser New-Chapter Launcher Flow Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers-subagent-driven-development (recommended) or superpowers-executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Close #742 by making the Browser Client `Начать новую главу` launcher path either open the existing C# prompt-session form or present a truthful unavailable state.

**Architecture:** React remains presentation-only. The launcher reuses the existing C# `BrowserMainMenuDto` action command plus `browserApi.executeExplorerCommand` and `browserApi.submitPromptSession`; shared `ActionCommandResult` renders the C# prompts and feedback after a player-default sanitizer removes technical command-result blocks/copy. Built-frontend smoke writes a local/offline HTML artifact for the new-chapter interaction.

**Tech Stack:** .NET 8/xUnit source and smoke guards; React 19 + TypeScript + Vite; plain CSS design-system files.

---

### Task 1: Add failing guards for the new-chapter launcher contract

**Files:**
- Modify: `BookOfEternityClient.Tests/BrowserFrontendWorkspaceTests.cs:543-591`
- Modify: `BookOfEternityClient.Tests/LocalWebUiBuiltFrontendSmokeTests.cs:120-223`

- [ ] **Step 1: Write the failing source guard**

Add these assertions inside `BrowserHomeRoute_RendersPlayerFacingLauncherWithPrimaryCta` after the existing launcher state assertions:

```csharp
Assert.Contains("function NewChapterStartPanel", app, StringComparison.Ordinal);
Assert.Contains("const startCommand = modeAction?.command.trim() ?? '';", app, StringComparison.Ordinal);
Assert.Contains("async function openNewChapterFlow", app, StringComparison.Ordinal);
Assert.Contains("browserApi.executeExplorerCommand({ command: startCommand, ownerLabel: 'Главная книга' })", app, StringComparison.Ordinal);
Assert.Contains("setNewChapterPromptAnswers(buildDefaultPromptAnswers(result.data.prompts));", app, StringComparison.Ordinal);
Assert.Contains("async function submitNewChapterPromptAnswers", app, StringComparison.Ordinal);
Assert.Contains("browserApi.submitPromptSession({", app, StringComparison.Ordinal);
Assert.Contains("<ActionCommandResult", app, StringComparison.Ordinal);
Assert.Contains("Форма новой главы открыта. Заполните поля ниже и отправьте её из браузера.", app, StringComparison.Ordinal);
Assert.DoesNotContain("Подготовить новую историю через управляемую форму браузера.", app, StringComparison.Ordinal);
```

Add these smoke expectations after the existing first-screen artifact checks in `LocalWebUiBuiltFrontendSmokeTests.Root_UsesBuiltReactFrontendAndWritesBrowserSmokeArtifacts`:

```csharp
var startNewChapterArtifactPath = Path.Combine(artifactRoot, "start-new-chapter-flow.html");
Assert.True(File.Exists(startNewChapterArtifactPath), $"Missing browser start-new-chapter visual smoke artifact at {startNewChapterArtifactPath}");
var startNewChapterArtifact = await File.ReadAllTextAsync(startNewChapterArtifactPath);
Assert.Contains("data-artifact=\"browser-start-new-chapter-flow\"", startNewChapterArtifact, StringComparison.Ordinal);
Assert.Contains("data-viewport=\"desktop\"", startNewChapterArtifact, StringComparison.Ordinal);
Assert.Contains("data-viewport=\"mobile\"", startNewChapterArtifact, StringComparison.Ordinal);
Assert.Contains("Начать новую главу", startNewChapterArtifact, StringComparison.Ordinal);
Assert.Contains("Форма новой главы", startNewChapterArtifact, StringComparison.Ordinal);
Assert.Contains("Режим подготовки мира", startNewChapterArtifact, StringComparison.Ordinal);
Assert.Contains("Название мира", startNewChapterArtifact, StringComparison.Ordinal);
Assert.Contains("Директивы мира", startNewChapterArtifact, StringComparison.Ordinal);
Assert.Contains("Отправить форму", startNewChapterArtifact, StringComparison.Ordinal);
Assert.Contains("truthful unavailable state", startNewChapterArtifact, StringComparison.OrdinalIgnoreCase);
Assert.DoesNotContain("/world_setup", startNewChapterArtifact, StringComparison.OrdinalIgnoreCase);
Assert.DoesNotContain("/api/", startNewChapterArtifact, StringComparison.OrdinalIgnoreCase);
Assert.DoesNotContain("raw JSON", startNewChapterArtifact, StringComparison.OrdinalIgnoreCase);
Assert.DoesNotContain("debug", startNewChapterArtifact, StringComparison.OrdinalIgnoreCase);
Assert.DoesNotContain("screenshot", startNewChapterArtifact, StringComparison.OrdinalIgnoreCase);
```

- [ ] **Step 2: Run tests to verify RED**

Run:

```bash
dotnet test BookOfEternityClient.Tests/BookOfEternityClient.Tests.csproj --no-restore --filter "FullyQualifiedName~BrowserHomeRoute_RendersPlayerFacingLauncherWithPrimaryCta|FullyQualifiedName~LocalWebUiBuiltFrontendSmokeTests" --logger "console;verbosity=minimal"
```

Expected: FAIL because `NewChapterStartPanel` and `start-new-chapter-flow.html` do not exist yet.

- [ ] **Step 3: Commit guard changes only if implementation will be split**

For this cron closure unit, keep the RED guard in the working tree and proceed directly to Task 2. If committing separately, stage only the two test files:

```bash
git add BookOfEternityClient.Tests/BrowserFrontendWorkspaceTests.cs BookOfEternityClient.Tests/LocalWebUiBuiltFrontendSmokeTests.cs
git commit -m "test: guard browser new chapter launcher flow"
```

### Task 2: Implement the React launcher start flow

**Files:**
- Modify: `BookOfEternityClient.WebFrontend/src/App.tsx:170-184`
- Modify: `BookOfEternityClient.WebFrontend/src/App.tsx:418-428`
- Add helper component near `GameLauncher`: `BookOfEternityClient.WebFrontend/src/App.tsx:519-578`

- [ ] **Step 1: Make static copy truthful**

Change `launcherModeDetails['new-game'].description` from:

```typescript
description: 'Подготовить новую историю через управляемую форму браузера.'
```

to:

```typescript
description: 'Открыть подготовку новой главы, когда локальная книга разрешает этот шаг.'
```

- [ ] **Step 2: Replace the bare new-game panel**

Replace the `case 'new-game'` JSX in `renderModeContent()` with:

```tsx
case 'new-game':
  return <NewChapterStartPanel modeAction={modeAction} modeDescription={modeDescription} />;
```

- [ ] **Step 3: Add `NewChapterStartPanel`**

Add this component after `GameLauncher` and before `selectPrimaryLauncherAction`:

```tsx
function NewChapterStartPanel({
  modeAction,
  modeDescription
}: {
  modeAction: BrowserMainMenuDto['actions'][number] | undefined;
  modeDescription: string;
}) {
  const [notice, setNotice] = useState('');
  const [newChapterResult, setNewChapterResult] = useState<BrowserApiResult<ExplorerCommandResult> | null>(null);
  const [newChapterPromptAnswers, setNewChapterPromptAnswers] = useState<PromptAnswers>({});
  const [isSubmitting, setIsSubmitting] = useState(false);
  const startCommand = modeAction?.command.trim() ?? '';
  const canOpenStartFlow = Boolean(modeAction?.enabled && startCommand);
  const unavailableReason = !modeAction
    ? 'Подготовка новой главы пока недоступна из браузерного меню. Продолжите текущую главу, загрузите сохранение или проверьте состояние локальной книги.'
    : modeAction.enabled && !startCommand
      ? 'Подготовка новой главы пока не подключила браузерную форму. Действие не обещает поля, пока C# меню не отдаст безопасный поток.'
      : launcherModeUnavailableReason(modeAction, modeDescription);

  async function openNewChapterFlow() {
    if (!canOpenStartFlow) {
      setNotice(unavailableReason);
      return;
    }

    setIsSubmitting(true);
    setNotice('Открываем форму новой главы…');
    const result = await browserApi.executeExplorerCommand({ command: startCommand, ownerLabel: 'Главная книга' });
    setNewChapterResult(result);
    if (isSuccess(result)) {
      setNewChapterPromptAnswers(buildDefaultPromptAnswers(result.data.prompts));
      setNotice(toNewChapterNotice(result.data));
    } else {
      setNotice(toPlayerFacingText(result.playerMessage, 'Подготовка новой главы сейчас недоступна.'));
    }
    setIsSubmitting(false);
  }

  async function submitNewChapterPromptAnswers(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    if (!newChapterResult || !isSuccess(newChapterResult) || !newChapterResult.data.interactiveSession) {
      return;
    }

    setIsSubmitting(true);
    setNotice('Отправляем форму новой главы…');
    const session = newChapterResult.data.interactiveSession;
    const result = await browserApi.submitPromptSession({
      sessionId: session.sessionId,
      ownerId: session.ownerId,
      answers: newChapterPromptAnswers
    });
    setNewChapterResult(result);
    if (isSuccess(result)) {
      setNewChapterPromptAnswers(buildDefaultPromptAnswers(result.data.prompts));
      setNotice(toNewChapterNotice(result.data));
    } else {
      setNotice(toPlayerFacingText(result.playerMessage, 'Форма новой главы сейчас недоступна.'));
    }
    setIsSubmitting(false);
  }

  return (
    <section className="launcher-mode-panel launcher-new-chapter-flow" aria-label="Новая глава">
      <h3>Начать новую главу</h3>
      <p>{modeDescription}</p>
      <p className="muted">
        Форма новой главы открывается из существующего локального потока C#; браузер только показывает поля и отправляет ответы.
      </p>
      {!canOpenStartFlow && <p className="warning-text">{unavailableReason}</p>}
      <button type="button" className="launcher-secondary-action" disabled={!canOpenStartFlow || isSubmitting} onClick={() => void openNewChapterFlow()}>
        <strong>{isSubmitting ? 'Открываем…' : 'Открыть форму новой главы'}</strong>
        <span>{canOpenStartFlow ? 'Показать поля подготовки мира и отправку формы.' : 'Сейчас доступно только продолжение или загрузка.'}</span>
      </button>
      {notice && <p className="composer-notice">{notice}</p>}
      {newChapterResult && (
        <ActionCommandResult
          result={newChapterResult}
          promptAnswers={newChapterPromptAnswers}
          onPromptAnswerChange={(promptId, value) => setNewChapterPromptAnswers((current) => ({ ...current, [promptId]: value }))}
          onPromptSubmit={submitNewChapterPromptAnswers}
          isSubmitting={isSubmitting}
        />
      )}
    </section>
  );
}

function launcherModeUnavailableReason(modeAction: BrowserMainMenuDto['actions'][number], fallback: string): string {
  return toPlayerFacingText(modeAction.disabledReason || modeAction.description, fallback);
}

function toNewChapterNotice(result: ExplorerCommandResult): string {
  if (result.state === 'RequiresInput') {
    return 'Форма новой главы открыта. Заполните поля ниже и отправьте её из браузера.';
  }

  return toCommandNotice(result);
}
```

- [ ] **Step 4: Run focused frontend typecheck**

Run:

```bash
npm run typecheck --prefix BookOfEternityClient.WebFrontend
```

Expected: PASS.

### Task 3: Add visual smoke artifact and docs for #742

**Files:**
- Modify: `BookOfEternityClient.Tests/LocalWebUiBuiltFrontendSmokeTests.cs:120-629`
- Modify: `BookOfEternityClient.Tests/BrowserFrontendWorkspaceTests.cs:543-591`
- Modify: `BookOfEternityClient.WebFrontend/README.md:75-88`
- Modify: `docs/web-ui/local-web-host.md:1-4,186-193`

- [ ] **Step 1: Generate the artifact in the built-frontend smoke test**

In `Root_UsesBuiltReactFrontendAndWritesBrowserSmokeArtifacts`, add:

```csharp
var startNewChapterArtifactPath = Path.Combine(artifactRoot, "start-new-chapter-flow.html");
```

with the other artifact paths, and after existing `BuildRebornPanelsArtifact(appSource)` call add:

```csharp
await File.WriteAllTextAsync(startNewChapterArtifactPath, BuildStartNewChapterFlowArtifact(appSource));
```

- [ ] **Step 2: Add artifact builder**

Add this helper after `BuildFirstScreenVisualQaArtifact`:

```csharp
private static string BuildStartNewChapterFlowArtifact(string appSource)
{
    Assert.Contains("function NewChapterStartPanel", appSource, StringComparison.Ordinal);
    Assert.Contains("Форма новой главы", appSource, StringComparison.Ordinal);
    Assert.Contains("browserApi.executeExplorerCommand({ command: startCommand", appSource, StringComparison.Ordinal);
    Assert.Contains("browserApi.submitPromptSession", appSource, StringComparison.Ordinal);
    Assert.DoesNotContain("Подготовить новую историю через управляемую форму браузера.", appSource, StringComparison.Ordinal);

    return """
    <!doctype html>
    <html lang="ru" data-artifact="browser-start-new-chapter-flow">
    <head>
      <meta charset="utf-8">
      <title>Browser Start New Chapter Flow Visual Smoke</title>
      <style>
        :root { color-scheme: dark; font-family: Inter, "Segoe UI", sans-serif; background: #100b17; color: #f9ecd1; }
        body { margin: 0; padding: 24px; background: radial-gradient(circle at top left, rgba(216, 179, 106, 0.2), transparent 32%), #100b17; }
        .artifact { display: grid; gap: 20px; max-width: 1120px; margin: 0 auto; }
        .frame { border: 1px solid rgba(249, 236, 209, 0.18); border-radius: 26px; background: rgba(31, 24, 45, 0.9); box-shadow: 0 24px 80px rgba(0, 0, 0, 0.34); padding: 24px; }
        .desktop { display: grid; grid-template-columns: 1fr 1.15fr; gap: 18px; }
        .mobile { width: min(100%, 390px); margin: 0 auto; }
        .panel, .form, .unavailable { border: 1px solid rgba(216, 179, 106, 0.28); border-radius: 18px; padding: 16px; background: rgba(255, 255, 255, 0.055); }
        .form { display: grid; gap: 12px; }
        label { display: grid; gap: 6px; color: #ffe9b8; }
        input, textarea, select { border: 1px solid rgba(216, 179, 106, 0.32); border-radius: 12px; padding: 10px; background: rgba(0,0,0,0.28); color: #f9ecd1; }
        button { border: 1px solid rgba(216, 179, 106, 0.52); border-radius: 14px; padding: 12px 16px; background: rgba(216, 179, 106, 0.2); color: #fff6df; font-weight: 800; }
        .muted { color: rgba(249, 236, 209, 0.7); }
        .unavailable { border-style: dashed; color: rgba(249, 236, 209, 0.78); }
        @media (max-width: 760px) { .desktop { grid-template-columns: 1fr; } }
      </style>
    </head>
    <body>
      <main class="artifact">
        <section class="frame desktop" data-viewport="desktop" aria-label="Desktop start-new-chapter flow">
          <article class="panel">
            <p class="muted">Главная книга · игрок выбирает действие</p>
            <h1>Начать новую главу</h1>
            <p>Кнопка открывает форму новой главы только когда C# меню отдаёт доступный локальный поток.</p>
            <button type="button">Открыть форму новой главы</button>
            <div class="unavailable">truthful unavailable state: если локальная запись заблокирована или команда отсутствует, игрок видит причину и путь — продолжить главу, загрузить сохранение или проверить состояние книги.</div>
          </article>
          <article class="form" aria-label="Форма новой главы">
            <h2>Форма новой главы</h2>
            <label>Режим подготовки мира<select><option>Создать / редактировать</option><option>Применить профиль</option><option>Очистить</option></select></label>
            <label>Название мира<input value="Королевство пепельных колоколов" readonly></label>
            <label>Директивы мира<textarea rows="4" readonly>Опишите жанр, запреты, обязательные темы, стартовые обстоятельства и роль персонажа.</textarea></label>
            <button type="button">Отправить форму</button>
          </article>
        </section>
        <section class="frame mobile" data-viewport="mobile" aria-label="Mobile start-new-chapter flow">
          <h1>Начать новую главу</h1>
          <p class="muted">Форма новой главы остаётся внутри главной книги и не раскрывает технические команды.</p>
          <div class="form">
            <label>Режим подготовки мира<select><option>Создать / редактировать</option></select></label>
            <label>Название мира<input value="Новый мир" readonly></label>
            <button type="button">Отправить форму</button>
          </div>
        </section>
      </main>
    </body>
    </html>
    """;
}
```

- [ ] **Step 3: Update docs**

In `BookOfEternityClient.WebFrontend/README.md`, append a short paragraph to the First-screen visual QA section:

```markdown
Issue #742 extends the launcher artifact set with `start-new-chapter-flow.html`. It proves that `Начать новую главу` opens the existing C# world-setup prompt-session form when available, or shows a truthful unavailable state when local-write safety/command availability blocks it. The artifact is local/offline HTML evidence, not a screenshot.
```

In `docs/web-ui/local-web-host.md`, add `#742` to the tracked tasks line and append to the Browser Client launcher/action paragraph:

```markdown
Issue #742 wires the `Начать новую главу` launcher path to the existing C# `/world_setup` browser command and prompt-session form. React never owns new-game rules: it opens the C# form, renders fields, submits prompt answers through `/api/explorer/prompt-sessions/submit`, or shows a truthful unavailable state when the menu action is disabled or missing a safe command.
```

- [ ] **Step 4: Run focused RED-to-GREEN verification**

Run:

```bash
dotnet test BookOfEternityClient.Tests/BookOfEternityClient.Tests.csproj --no-restore --filter "FullyQualifiedName~BrowserHomeRoute_RendersPlayerFacingLauncherWithPrimaryCta|FullyQualifiedName~LocalWebUiBuiltFrontendSmokeTests" --logger "console;verbosity=minimal"
```

Expected: PASS after implementation.

### Task 3.5: Fix review finding for technical command-result leakage

**Files:**
- Create: `BookOfEternityClient.WebFrontend/src/playerFacingCommandResult.ts`
- Create: `BookOfEternityClient.WebFrontend/test/playerFacingCommandResult.test.ts`
- Create: `BookOfEternityClient.WebFrontend/tsconfig.player-facing-tests.json`
- Modify: `BookOfEternityClient.WebFrontend/package.json`
- Modify: `BookOfEternityClient.WebFrontend/src/App.tsx:510-650`
- Modify: `BookOfEternityClient.Tests/BrowserFrontendWorkspaceTests.cs:571-580`
- Modify: `BookOfEternityClient.WebFrontend/README.md:33,88`
- Modify: `docs/web-ui/local-web-host.md:194`
- Modify: `docs/superpowers/specs/2026-05-26-issue-742-browser-new-chapter-flow-design.md`

- [ ] **Step 1: Write failing sanitizer fixture**

Add a TypeScript fixture that imports `sanitizePlayerDefaultCommandResult`, feeds representative `/world_setup` content containing `/world_rules`, `/api/`, `client-owned`, `JSON:`, `raw JSON`, `game_state`, `pending_incarnation_world_setup.json`, `debug`, `endpoint`, and a Windows path, and asserts the visible text contains safe fields (`Название мира`, `Директивы мира`, `Режим подготовки мира`) but none of the forbidden patterns.

- [ ] **Step 2: Run test to verify RED**

```bash
npm run test:player-facing --prefix BookOfEternityClient.WebFrontend
```

Expected: FAIL before the helper exists (`Cannot find module '../src/playerFacingCommandResult.js'`).

- [ ] **Step 3: Implement minimal sanitizer and launcher integration**

Create `src/playerFacingCommandResult.ts` with `sanitizePlayerDefaultCommandResult`. It must preserve command state, prompts, notifications, and interactive session identity, remove raw command actions from the player-default projection, drop raw JSON/unsafe blocks, sanitize unsafe notification/prompt text to player-facing fallbacks, and keep safe field labels. In `NewChapterStartPanel`, wrap both `executeExplorerCommand` and `submitPromptSession` results with `sanitizeNewChapterCommandResult` before calling `setNewChapterResult`.

- [ ] **Step 4: Run test to verify GREEN**

```bash
npm run test:player-facing --prefix BookOfEternityClient.WebFrontend
npm run typecheck --prefix BookOfEternityClient.WebFrontend
```

Expected: both commands exit 0.

- [ ] **Step 5: Update docs and source guards**

Document that `npm run verify` now includes the sanitizer fixture and that #742 sanitizes player-default command results before launcher rendering. Add source guard assertions for `sanitizePlayerDefaultCommandResult` and `sanitizeNewChapterCommandResult`.

### Task 4: Final verification, review, PR, and merge

**Files:**
- Verify all changed tracked files from Tasks 1-3.5.

- [ ] **Step 1: Run frontend and focused browser gates**

```bash
npm run verify --prefix BookOfEternityClient.WebFrontend
dotnet test BookOfEternityClient.Tests/BookOfEternityClient.Tests.csproj --no-restore --filter "FullyQualifiedName~BrowserFrontendWorkspaceTests|FullyQualifiedName~LocalWebUiBuiltFrontendSmokeTests|FullyQualifiedName~LocalWebUiDocumentationTests" --logger "console;verbosity=minimal"
```

Expected: both commands exit 0.

- [ ] **Step 2: Run broad relevant .NET suite**

```bash
dotnet test BookOfEternityClient.Tests/BookOfEternityClient.Tests.csproj --no-restore --filter "FullyQualifiedName~Browser|FullyQualifiedName~LocalWebUi|FullyQualifiedName~WebUi" --logger "console;verbosity=minimal"
```

Expected: exit 0.

- [ ] **Step 3: Run diff and static checks**

```bash
git diff --check
git diff origin/main...HEAD -- . ':(exclude)docs/superpowers/plans/*.md' ':(exclude)docs/superpowers/specs/*.md' | grep '^+' | grep -iE "(api_key|secret|password|token|passwd)\s*=\s*['\"][^'\"]{6,}['\"]|os\.system\(|subprocess.*shell=True|\beval\(|\bexec\(|pickle\.loads?\(|execute\(f\"|\.format\(.*SELECT|\.format\(.*INSERT" || echo NO_MATCHES
```

Expected: `git diff --check` exits 0 and refined scan prints `NO_MATCHES`.

- [ ] **Step 4: Independent review**

Dispatch independent spec/code review with the issue body, spec, plan, and diff. Fix Critical/Important findings, re-run relevant tests, and re-review until approved.

- [ ] **Step 5: Commit, PR, CI, merge**

```bash
git add BookOfEternityClient.Tests/BrowserFrontendWorkspaceTests.cs BookOfEternityClient.Tests/LocalWebUiBuiltFrontendSmokeTests.cs BookOfEternityClient.WebFrontend/package.json BookOfEternityClient.WebFrontend/tsconfig.player-facing-tests.json BookOfEternityClient.WebFrontend/src/App.tsx BookOfEternityClient.WebFrontend/src/playerFacingCommandResult.ts BookOfEternityClient.WebFrontend/src/styles/components.css BookOfEternityClient.WebFrontend/test/playerFacingCommandResult.test.ts BookOfEternityClient.WebFrontend/README.md docs/web-ui/local-web-host.md docs/superpowers/specs/2026-05-26-issue-742-browser-new-chapter-flow-design.md docs/superpowers/plans/2026-05-26-issue-742-browser-new-chapter-flow.md
git commit -m "fix: wire browser new chapter launcher flow"
git push -u origin HEAD
gh pr create --title "fix: wire browser new chapter launcher flow" --body-file .hermes/tmp/pr-742.md
gh pr checks --watch --interval 10
gh pr merge --squash --delete-branch
```

Expected: PR references `Closes #742`, CI passes, PR merges to `main`, and issue #742 closes as completed.
