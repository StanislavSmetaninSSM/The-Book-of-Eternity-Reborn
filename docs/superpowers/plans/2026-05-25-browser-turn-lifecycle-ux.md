# Browser Turn Lifecycle UX Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers-subagent-driven-development (recommended) or superpowers-executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Close issue #686 by adding a player-facing Browser Client turn lifecycle state machine to `/api/game-screen` and React UI.

**Architecture:** C# `BrowserGameScreenService` remains the authority for lifecycle interpretation. React consumes typed DTO fields and renders player-facing guidance while advanced diagnostics keep raw details. The change is DTO/UI-only and does not add game-state artifacts or mutate `game_session`.

**Tech Stack:** .NET 8, xUnit, ASP.NET minimal API host tests, React, TypeScript, Vite.

---

## File Structure

- Modify: `BookOfEternityClient/WebUi/BrowserGameScreenService.cs`
  - Extend `BrowserGameScreenTurnStateDto` with `Phase`, `PhaseLabel`, `Severity`, `PlayerGuidance`, `RecommendedActions`, and `KnownPhases`.
  - Add records `BrowserGameScreenTurnActionDto` and `BrowserGameScreenTurnPhaseDto`.
  - Map current lifecycle artifacts into canonical phases.

- Modify: `BookOfEternityClient.WebFrontend/src/api/contracts.ts`
  - Add matching TypeScript interfaces for new DTO fields.

- Modify: `BookOfEternityClient.WebFrontend/src/App.tsx`
  - Render phase/guidance/actions in the game route and sidebar.
  - Use `phase` for lifecycle labels.
  - Keep validation issue details out of default UI.

- Modify: `BookOfEternityClient.Tests/LocalWebUiHostTests.cs`
  - Add focused smoke tests for lifecycle phase mapping from real `/api/game-screen` responses.

- Modify: `BookOfEternityClient.Tests/BrowserApiContractTests.cs`
  - Update representative DTO construction and add frontend source guard coverage.

- Modify: `BookOfEternityClient.WebFrontend/src/api/contract-fixtures/game-screen.json`
  - Update after C# DTO changes so fixture guard remains exact.

## Task 1: RED tests for lifecycle phase contract

**Files:**
- Modify: `BookOfEternityClient.Tests/LocalWebUiHostTests.cs`
- Modify: `BookOfEternityClient.Tests/BrowserApiContractTests.cs`

- [ ] **Step 1: Add failing smoke test for phase fields in `LocalWebUiHostTests.cs`**

Add this test after `GameScreenEndpoint_ReportsReadyAndErrorTurnStatesDistinctly`:

```csharp
    [Fact]
    [Trait("Category", "BrowserWebUiSmoke")]
    public async Task GameScreenEndpoint_MapsPendingArtifactsToPlayerFacingLifecyclePhases()
    {
        WriteSessionFile("game_state/meta/soul_state.json", """
        { "soulName": "Lifecycle Soul", "currentRealm": "Mortal World" }
        """);
        WriteSessionFile("input/turn_request.json", "{}");

        var url = "http://127.0.0.1:" + GetFreeLoopbackPort();
        await using var app = LocalWebUiHost.Build(Array.Empty<string>(), CreateHostOptions(url));
        await app.StartAsync();

        using var client = new HttpClient { BaseAddress = new Uri(url) };
        var waitingRoot = JsonNode.Parse(await client.GetStringAsync("/api/game-screen"))!.AsObject();
        Assert.Equal("waiting-gm", waitingRoot["turnState"]!["phase"]!.GetValue<string>());
        Assert.Equal("Ожидаем ответ ГМа", waitingRoot["turnState"]!["phaseLabel"]!.GetValue<string>());
        Assert.Equal("warning", waitingRoot["turnState"]!["severity"]!.GetValue<string>());
        Assert.Contains("ГМ", waitingRoot["turnState"]!["playerGuidance"]!.GetValue<string>(), StringComparison.OrdinalIgnoreCase);
        Assert.Contains(
            waitingRoot["turnState"]!["recommendedActions"]!.AsArray(),
            action => action!["id"]!.GetValue<string>() == "wait-for-gm" && action!["surface"]!.GetValue<string>() == "player-default");
        Assert.Contains(
            waitingRoot["turnState"]!["knownPhases"]!.AsArray(),
            phase => phase!["id"]!.GetValue<string>() == "cancelled");

        File.Delete(Path.Combine(_rootPath, "game_session", "input", "turn_request.json"));
        WriteSessionFile("ready/turn_complete.json", "{}");
        var readyRoot = JsonNode.Parse(await client.GetStringAsync("/api/game-screen"))!.AsObject();
        Assert.Equal("ready", readyRoot["turnState"]!["phase"]!.GetValue<string>());
        Assert.Equal("success", readyRoot["turnState"]!["severity"]!.GetValue<string>());
        Assert.Contains("принять", readyRoot["turnState"]!["playerGuidance"]!.GetValue<string>(), StringComparison.OrdinalIgnoreCase);

        File.Delete(Path.Combine(_rootPath, "game_session", "ready", "turn_complete.json"));
        WriteSessionFile("ready/turn_error.json", "{ \"error\": \"GM timeout\" }");
        var errorRoot = JsonNode.Parse(await client.GetStringAsync("/api/game-screen"))!.AsObject();
        Assert.Equal("error-restored", errorRoot["turnState"]!["phase"]!.GetValue<string>());
        Assert.Equal("error", errorRoot["turnState"]!["severity"]!.GetValue<string>());
        Assert.Contains("repair", errorRoot["turnState"]!["playerGuidance"]!.GetValue<string>(), StringComparison.OrdinalIgnoreCase);
    }
```

- [ ] **Step 2: Add failing smoke test for validation/local-lock phases**

Add this test after `GameScreenEndpoint_DisablesComposerWhenValidationRequiresRepair`:

```csharp
    [Fact]
    [Trait("Category", "BrowserWebUiSmoke")]
    public async Task GameScreenEndpoint_ExplainsValidationAndLocalLockLifecyclePhases()
    {
        WriteSessionFile("game_state/meta/soul_state.json", "not json");
        var url = "http://127.0.0.1:" + GetFreeLoopbackPort();
        await using var app = LocalWebUiHost.Build(Array.Empty<string>(), CreateHostOptions(url));
        await app.StartAsync();

        using var client = new HttpClient { BaseAddress = new Uri(url) };
        var validationRoot = JsonNode.Parse(await client.GetStringAsync("/api/game-screen"))!.AsObject();
        Assert.Equal("validation-failed", validationRoot["turnState"]!["phase"]!.GetValue<string>());
        Assert.Equal("error", validationRoot["turnState"]!["severity"]!.GetValue<string>());
        Assert.DoesNotContain("game_state/meta/soul_state.json", validationRoot["turnState"]!["playerGuidance"]!.GetValue<string>(), StringComparison.OrdinalIgnoreCase);

        Directory.CreateDirectory(Path.Combine(_rootPath, "game_session", "game_state", "meta"));
        await File.WriteAllTextAsync(Path.Combine(_rootPath, "game_session", "game_state", "meta", "soul_state.json"), """
        { "soulName": "Locked Soul", "currentRealm": "Mortal World" }
        """);
        Directory.CreateDirectory(Path.Combine(_rootPath, "game_session", "game_state", "control"));
        await File.WriteAllTextAsync(Path.Combine(_rootPath, "game_session", "game_state", "control", "local_ui_lock.json"), """
        {
          "ownerId": "browser:test",
          "ownerKind": "browser",
          "ownerLabel": "Browser test",
          "acquiredAtUtc": "2099-01-01T00:00:00Z",
          "heartbeatAtUtc": "2099-01-01T00:00:00Z",
          "leaseSeconds": 120,
          "lastOperation": "submitting turn"
        }
        """);

        var lockedRoot = JsonNode.Parse(await client.GetStringAsync("/api/game-screen"))!.AsObject();
        Assert.Equal("turn-submitted", lockedRoot["turnState"]!["phase"]!.GetValue<string>());
        Assert.Equal("warning", lockedRoot["turnState"]!["severity"]!.GetValue<string>());
        Assert.False(lockedRoot["actionComposer"]!["canSubmit"]!.GetValue<bool>());
    }
```

- [ ] **Step 3: Add failing API contract/source guard in `BrowserApiContractTests.cs`**

In `BrowserGameScreenContract_IncludesPlayerCommandActionMenu`, add:

```csharp
        Assert.Equal("idle", screen.TurnState.Phase);
        Assert.Equal("Можно готовить ход", screen.TurnState.PhaseLabel);
        Assert.Equal("success", screen.TurnState.Severity);
        Assert.Contains(screen.TurnState.RecommendedActions, action => action.Id == "compose-action" && action.Surface == "player-default");
        Assert.Contains(screen.TurnState.KnownPhases, phase => phase.Id == "accepted");
        Assert.Contains(screen.TurnState.KnownPhases, phase => phase.Id == "cancelled");
```

Add a new source guard test:

```csharp
    [Fact]
    public void FrontendShell_RendersLifecyclePhaseMachineWithoutDefaultRawValidationDetails()
    {
        var app = File.ReadAllText(Path.Combine(FrontendRoot, "src", "App.tsx"));

        Assert.Contains("turnState.phase", app, StringComparison.Ordinal);
        Assert.Contains("turnState.playerGuidance", app, StringComparison.Ordinal);
        Assert.Contains("recommendedActions", app, StringComparison.Ordinal);
        Assert.Contains("knownPhases", app, StringComparison.Ordinal);
        Assert.Contains("Жизненный цикл хода", app, StringComparison.Ordinal);

        var advancedIndex = app.IndexOf("function AdvancedDiagnosticsPanel", StringComparison.Ordinal);
        Assert.True(advancedIndex > 0, "Advanced diagnostics function must stay explicit.");
        var defaultApp = app[..advancedIndex];
        Assert.DoesNotContain("validation.issues.map", defaultApp, StringComparison.Ordinal);
        Assert.DoesNotContain("issue.filePath", defaultApp, StringComparison.Ordinal);
    }
```

- [ ] **Step 4: Run tests and verify RED**

Run:

```bash
dotnet test BookOfEternityClient.Tests/BookOfEternityClient.Tests.csproj --no-restore --filter "GameScreenEndpoint_MapsPendingArtifactsToPlayerFacingLifecyclePhases|GameScreenEndpoint_ExplainsValidationAndLocalLockLifecyclePhases|BrowserGameScreenContract_IncludesPlayerCommandActionMenu|FrontendShell_RendersLifecyclePhaseMachineWithoutDefaultRawValidationDetails" --logger "console;verbosity=minimal"
```

Expected: FAIL because `phase`, `phaseLabel`, `severity`, `playerGuidance`, `recommendedActions`, and `knownPhases` do not exist yet.

## Task 2: GREEN C# lifecycle DTO and contract fixture

**Files:**
- Modify: `BookOfEternityClient/WebUi/BrowserGameScreenService.cs`
- Modify: `BookOfEternityClient.Tests/BrowserApiContractTests.cs`
- Modify: `BookOfEternityClient.WebFrontend/src/api/contracts.ts`
- Modify: `BookOfEternityClient.WebFrontend/src/api/contract-fixtures/game-screen.json`

- [ ] **Step 1: Extend C# turn-state DTO**

In `BrowserGameScreenService.cs`, replace `BrowserGameScreenTurnStateDto` with a record that includes the existing fields plus:

```csharp
    string Phase,
    string PhaseLabel,
    string Severity,
    string PlayerGuidance,
    IReadOnlyList<BrowserGameScreenTurnActionDto> RecommendedActions,
    IReadOnlyList<BrowserGameScreenTurnPhaseDto> KnownPhases
```

Create two records below it:

```csharp
public sealed record BrowserGameScreenTurnActionDto(
    string Id,
    string Label,
    string Description,
    string Surface,
    bool Enabled,
    string DisabledReason);

public sealed record BrowserGameScreenTurnPhaseDto(
    string Id,
    string Label,
    string Description,
    string Surface);
```

- [ ] **Step 2: Add helper constructors and mapping methods**

Inside `BrowserGameScreenTurnStateDto`, add helpers equivalent to:

```csharp
    private static readonly IReadOnlyList<BrowserGameScreenTurnPhaseDto> PhaseCatalog =
    [
        new("idle", "Можно готовить ход", "Игра не ждёт ГМа; игрок может писать следующий художественный ход.", "player-default"),
        new("composing-action", "Игрок готовит действие", "Текст или быстрая сцена находятся в фазе подготовки до безопасной записи.", "player-default"),
        new("turn-submitted", "Ход отправляется", "Локальная запись уже начата; повторные действия заблокированы.", "player-default"),
        new("waiting-gm", "Ожидаем ответ ГМа", "Ход отправлен, и нужно дождаться результата ГМа.", "player-default"),
        new("ready", "Ответ ГМа готов", "Ответ ГМа готов к принятию через обычный turn lifecycle.", "player-default"),
        new("accepted", "Ответ ГМа принят", "Результат ответа ГМа уже принят в локальное состояние.", "player-default"),
        new("validation-failed", "Проверка не прошла", "Состояние требует ремонта перед продолжением.", "player-default"),
        new("repair-required", "Нужен ремонт", "Snapshot/rollback artifacts требуют repair перед новыми действиями.", "player-default"),
        new("error-restored", "Ошибка восстановлена", "GM turn завершился ошибкой; rollback/repair должен быть разобран.", "player-default"),
        new("cancelled", "Ход отменён", "Ожидающий ход был отменён или очищен безопасным lifecycle-действием.", "player-default")
    ];
```

Build factory helpers so each return includes all fields. Preserve existing `State`, `Title`, `Message`, `CanStartBrowserWrite`, `ValidationState`, and `ValidationLabel` values.

- [ ] **Step 3: Map current states to phases**

Use these phase mappings:

- `turn_error` artifact -> `Phase = "error-restored"`, `Severity = "error"`, action `open-advanced-repair` with `Surface = "advanced-only"`.
- `turn_complete` artifact -> `Phase = "ready"`, `Severity = "success"`, action `accept-gm-response` with `Surface = "advanced-only"` until a default accept flow exists.
- snapshot/rollback artifacts -> `Phase = "repair-required"`, `Severity = "error"`, action `open-repair-guidance`.
- any other active pending turn -> `Phase = "waiting-gm"`, `Severity = "warning"`, action `wait-for-gm`.
- QTE offer/active -> `Phase = "composing-action"`, `Severity = "warning"`, action `resolve-qte`.
- validation errors -> `Phase = "validation-failed"`, `Severity = "error"`, action `review-validation`.
- local lock block without pending turn -> `Phase = "turn-submitted"`, `Severity = "warning"`, action `wait-local-write`.
- ready/no blockers -> `Phase = "idle"`, `Severity = "success"`, action `compose-action`.

- [ ] **Step 4: Update representative C# DTO builder**

In `BrowserApiContractTests.BuildGameScreen()`, update `new BrowserGameScreenTurnStateDto(...)` to include:

```csharp
                Phase: "idle",
                PhaseLabel: "Можно готовить ход",
                Severity: "success",
                PlayerGuidance: "Игра не ждёт ГМа; можно подготовить следующий художественный ход.",
                RecommendedActions:
                [
                    new BrowserGameScreenTurnActionDto(
                        Id: "compose-action",
                        Label: "Подготовить действие",
                        Description: "Заполните основной художественный ввод и подтвердите действие, когда запись хода будет подключена.",
                        Surface: "player-default",
                        Enabled: true,
                        DisabledReason: string.Empty)
                ],
                KnownPhases: BrowserGameScreenTurnStateDto.KnownPhaseCatalog
```

- [ ] **Step 5: Update TypeScript contracts**

Add to `BrowserGameScreenTurnStateDto` interface:

```ts
  phase: string;
  phaseLabel: string;
  severity: string;
  playerGuidance: string;
  recommendedActions: BrowserGameScreenTurnActionDto[];
  knownPhases: BrowserGameScreenTurnPhaseDto[];
```

Add interfaces:

```ts
export interface BrowserGameScreenTurnActionDto {
  id: string;
  label: string;
  description: string;
  surface: 'player-default' | 'advanced-only';
  enabled: boolean;
  disabledReason: string;
}

export interface BrowserGameScreenTurnPhaseDto {
  id: string;
  label: string;
  description: string;
  surface: 'player-default' | 'advanced-only';
}
```

- [ ] **Step 6: Update contract fixture**

Run the focused contract test once to get the expected JSON mismatch output, then update `BookOfEternityClient.WebFrontend/src/api/contract-fixtures/game-screen.json` to match the new C# DTO exactly.

- [ ] **Step 7: Verify GREEN for C# contract tests**

Run:

```bash
dotnet test BookOfEternityClient.Tests/BookOfEternityClient.Tests.csproj --no-restore --filter "GameScreenEndpoint_MapsPendingArtifactsToPlayerFacingLifecyclePhases|GameScreenEndpoint_ExplainsValidationAndLocalLockLifecyclePhases|BrowserGameScreenContract_IncludesPlayerCommandActionMenu|FrontendContractFixtures_MatchRepresentativeCSharpDtos" --logger "console;verbosity=minimal"
```

Expected: PASS.

## Task 3: React lifecycle rendering and frontend verification

**Files:**
- Modify: `BookOfEternityClient.WebFrontend/src/App.tsx`
- Modify: `BookOfEternityClient.WebFrontend/src/styles/components.css` if a small visual class is needed
- Modify: `BookOfEternityClient.Tests/BrowserApiContractTests.cs`

- [ ] **Step 1: Render lifecycle phase fields in `GameRoute`**

Inside the `Состояние хода` panel, render:

```tsx
          <p className={`status-pill turn-phase turn-phase--${game.turnState.severity}`}>{formatTurnStateTitle(game.turnState)}</p>
          <p>{formatTurnStateMessage(game.turnState)}</p>
          <p className="muted">{toPlayerFacingText(game.turnState.playerGuidance, 'Следуйте безопасному состоянию хода.')}</p>
          <TurnLifecycleActions turnState={game.turnState} />
```

- [ ] **Step 2: Add `TurnLifecycleActions` helper**

Add below `ActionMenu`:

```tsx
function TurnLifecycleActions({ turnState }: { turnState: BrowserGameScreenDto['turnState'] }) {
  const playerActions = turnState.recommendedActions.filter((action) => action.surface === 'player-default');
  const advancedActions = turnState.recommendedActions.filter((action) => action.surface === 'advanced-only');

  return (
    <div className="turn-lifecycle-actions" aria-label="Рекомендуемые действия состояния хода">
      {playerActions.length > 0 && (
        <ul className="choice-list">
          {playerActions.map((action) => (
            <li key={action.id}>
              <strong>{toPlayerFacingText(action.label, 'Действие')}</strong>
              <span>{toPlayerFacingText(action.enabled ? action.description : action.disabledReason, 'Действие сейчас недоступно.')}</span>
            </li>
          ))}
        </ul>
      )}
      {advancedActions.length > 0 && (
        <p className="muted">Технические действия для этого состояния доступны только через «Расширенный режим».</p>
      )}
    </div>
  );
}
```

- [ ] **Step 3: Render phase catalog summary**

Add a small `Жизненный цикл хода` section to the game route after the composer:

```tsx
      <section className="summary-card" aria-label="Жизненный цикл хода">
        <h3>Жизненный цикл хода</h3>
        <p className="muted">{toPlayerFacingText(game.turnState.phaseLabel, 'Текущее состояние хода')}</p>
        <div className="phase-chip-grid">
          {game.turnState.knownPhases.map((phase) => (
            <span key={phase.id} className={phase.id === game.turnState.phase ? 'status-pill' : 'status-pill is-muted'}>
              {toPlayerFacingText(phase.label, 'Этап')}
            </span>
          ))}
        </div>
      </section>
```

- [ ] **Step 4: Prefer `phase` in label helper**

Change `formatTurnStateTitle` fallback to call `formatTurnStateLabel(turnState.phase || turnState.state)` and update `formatTurnStateLabel` cases to include `idle`, `composing-action`, `turn-submitted`, `validation-failed`, `repair-required`, `error-restored`, and `cancelled`.

- [ ] **Step 5: Add minimal styles only if needed**

If classes are absent, add compact styles in `BookOfEternityClient.WebFrontend/src/styles/components.css`:

```css
.turn-lifecycle-actions {
  display: grid;
  gap: var(--space-2);
}

.phase-chip-grid {
  display: flex;
  flex-wrap: wrap;
  gap: var(--space-2);
}
```

- [ ] **Step 6: Verify frontend and focused browser suite**

Run:

```bash
dotnet test BookOfEternityClient.Tests/BookOfEternityClient.Tests.csproj --no-restore --filter "Category=BrowserWebUiSmoke|FullyQualifiedName~BrowserApiContractTests" --logger "console;verbosity=minimal"
npm run typecheck --prefix BookOfEternityClient.WebFrontend
npm run build --prefix BookOfEternityClient.WebFrontend
```

Expected: all commands exit 0.

## Task 4: Final verification, review, PR, merge

**Files:**
- All modified files from tasks 1–3.

- [ ] **Step 1: Run diff and static checks**

Run:

```bash
git diff --check
git diff --cached | grep "^+" | grep -iE "(api_key|secret|password|token|passwd)\s*=\s*['\"][^'\"]{6,}['\"]" || true
git diff --cached | grep "^+" | grep -E "os\.system\(|subprocess.*shell=True|\beval\(|\bexec\(|pickle\.loads?\(|execute\(f\"|\.format\(.*SELECT|\.format\(.*INSERT" || true
```

Expected: `git diff --check` exits 0; static scans print nothing.

- [ ] **Step 2: Run broad browser/web verification**

Run:

```bash
dotnet test BookOfEternityClient.Tests/BookOfEternityClient.Tests.csproj --no-restore --filter "WebUi|LocalWebUi|ExplorerWeb|CommandMigration" --logger "console;verbosity=minimal"
```

Expected: PASS.

- [ ] **Step 3: Run full relevant project test when practical**

Run:

```bash
dotnet test BookOfEternityClient.Tests/BookOfEternityClient.Tests.csproj --no-restore --logger "console;verbosity=minimal"
```

Expected: PASS.

- [ ] **Step 4: Independent review**

Dispatch an independent review with the issue acceptance criteria, design, plan, and `git diff`. Fix Critical/Important findings and re-run focused verification.

- [ ] **Step 5: Commit only tracked task files**

Run:

```bash
git add docs/superpowers/specs/2026-05-25-browser-turn-lifecycle-ux-design.md docs/superpowers/plans/2026-05-25-browser-turn-lifecycle-ux.md BookOfEternityClient/WebUi/BrowserGameScreenService.cs BookOfEternityClient.Tests/LocalWebUiHostTests.cs BookOfEternityClient.Tests/BrowserApiContractTests.cs BookOfEternityClient.WebFrontend/src/api/contracts.ts BookOfEternityClient.WebFrontend/src/api/contract-fixtures/game-screen.json BookOfEternityClient.WebFrontend/src/App.tsx BookOfEternityClient.WebFrontend/src/styles/components.css
git commit -m "feat(web-ui): add browser turn lifecycle phases"
```

Do not add `.hermes/`, `.review-diffs/`, `.review-worktrees/`, `.superpowers/`, `bin/`, `obj/`, `tmp/`, or unrelated old plan files.

- [ ] **Step 6: Push PR and merge after green CI**

Run:

```bash
git push -u origin task/686-browser-turn-lifecycle
gh pr create --title "feat(web-ui): add browser turn lifecycle phases" --body "Closes #686"
gh pr checks --watch --interval 10
gh pr merge --squash --delete-branch
```

Expected: CI green before merge; PR merged to `main`; issue #686 auto-closed or closed with a verification comment.
