# Browser Reborn Panels Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers-subagent-driven-development (recommended) or superpowers-executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add player-facing Browser Client panels for Afterlife, Shining Abode, and Chaos Sea using existing C# game-screen state and action metadata.

**Architecture:** Keep C# authoritative and React presentation-only. Render a `RebornSystemsPanel` inside the existing `WorldRoute`, using `DetailSurfaceCard` for card → modal/full-panel behavior and filtered player-default action metadata for safe Shining/Chaos/Afterlife actions. Add source guards, a dependency-light visual smoke artifact, and docs that this is UI-only and does not change Afterlife runtime contracts.

**Tech Stack:** .NET 8/xUnit guard tests, React/TypeScript Vite frontend, existing `BrowserGameScreenDto`/`BrowserPlayerCommandMenuDto`, CSS modules imported through `src/styles.css`.

---

## File Structure

- Modify `BookOfEternityClient.Tests/BrowserFrontendWorkspaceTests.cs`
  - Add `BrowserRebornPanels_DefinePlayerFacingAfterlifeShiningAndChaosSections` source guard.
  - Assert panel/component names, detail-surface IDs, UI-only comments, docs, styles, and absence of raw contract/debug copy in the new default-player source slice.
- Modify `BookOfEternityClient.Tests/LocalWebUiBuiltFrontendSmokeTests.cs`
  - Generate `TestResults/browser-smoke/reborn-panels.html` next to existing visual smoke artifacts.
  - Assert locked and active Reborn panel states, desktop/mobile markers, and absence of raw debug/API terms.
- Modify `BookOfEternityClient.WebFrontend/src/App.tsx`
  - Add action matchers and helper functions near existing action-section helpers.
  - Add `RebornSystemsPanel` after the mortal-world overview and before the general `ActionMenu` in `WorldRoute`.
  - Use only existing `game.flags`, `game.afterlife`, `game.soul`, `game.actionMenu`, and player-default action metadata.
- Modify `BookOfEternityClient.WebFrontend/src/styles/components.css`
  - Add `.reborn-systems-panel`, `.reborn-systems-panel__header`, `.reborn-systems-panel__actions`, and locked/availability-friendly styling.
- Modify `BookOfEternityClient.WebFrontend/README.md`
  - Document issue #729 as player-facing Reborn panel work and state that C# remains authoritative.
- Modify `docs/web-ui/local-web-host.md`
  - Document `reborn-panels.html` as the dependency-light visual smoke artifact and note that GM-facing contract docs were not changed because this is UI-only.

---

### Task 1: Add failing source guard for Reborn panels

**Objective:** Prove the Browser Client must expose explicit player-facing Reborn panels that stay separate from mortal-world panels and advanced/debug surfaces.

**Files:**
- Modify: `BookOfEternityClient.Tests/BrowserFrontendWorkspaceTests.cs`

- [ ] **Step 1: Write the failing test**

Add this test before `FrontendHostContract_UsesExternalAssetsInsteadOfInlineShellBlob`:

```csharp
    [Fact]
    public void BrowserRebornPanels_DefinePlayerFacingAfterlifeShiningAndChaosSections()
    {
        var app = File.ReadAllText(Path.Combine(FrontendRoot, "src", "App.tsx"));
        var styles = ReadFrontendStyles();
        var readme = File.ReadAllText(Path.Combine(FrontendRoot, "README.md"));
        var hostDoc = File.ReadAllText(Path.Combine(RepoRoot, "docs", "web-ui", "local-web-host.md"));

        Assert.Contains("function RebornSystemsPanel", app, StringComparison.Ordinal);
        Assert.Contains("const rebornSectionMatchers", app, StringComparison.Ordinal);
        Assert.Contains("const shiningAbodeActionMatchers", app, StringComparison.Ordinal);
        Assert.Contains("const chaosSeaActionMatchers", app, StringComparison.Ordinal);
        Assert.Contains("<RebornSystemsPanel game={game} />", app, StringComparison.Ordinal);
        Assert.Contains("detailSurfaceId=\"reborn-afterlife-overview\"", app, StringComparison.Ordinal);
        Assert.Contains("detailSurfaceId=\"reborn-shining-abode\"", app, StringComparison.Ordinal);
        Assert.Contains("detailSurfaceId=\"reborn-chaos-sea\"", app, StringComparison.Ordinal);
        Assert.Contains("Посмертие Reborn", app, StringComparison.Ordinal);
        Assert.Contains("Сияющая Обитель", app, StringComparison.Ordinal);
        Assert.Contains("Море Хаоса", app, StringComparison.Ordinal);
        Assert.Contains("Посмертные панели откроются", app, StringComparison.Ordinal);
        Assert.Contains("UI-only mapping for #729", app, StringComparison.Ordinal);
        Assert.Contains("filterActionSections(game.actionMenu, rebornSectionMatchers)", app, StringComparison.Ordinal);
        Assert.Contains("filterActionsForPanel(rebornSections, shiningAbodeActionMatchers)", app, StringComparison.Ordinal);
        Assert.Contains("filterActionsForPanel(rebornSections, chaosSeaActionMatchers)", app, StringComparison.Ordinal);
        Assert.Contains("game.flags.isInAfterlifeRealm", app, StringComparison.Ordinal);
        Assert.Contains("game.flags.isInShiningAbode", app, StringComparison.Ordinal);
        Assert.Contains("game.flags.isInChaosSea", app, StringComparison.Ordinal);

        var worldRouteStart = app.IndexOf("function WorldRoute", StringComparison.Ordinal);
        var actionMenuIndex = app.IndexOf("<ActionMenu menu={game.actionMenu} />", worldRouteStart, StringComparison.Ordinal);
        var rebornPanelIndex = app.IndexOf("<RebornSystemsPanel game={game} />", worldRouteStart, StringComparison.Ordinal);
        Assert.True(rebornPanelIndex > worldRouteStart, "Reborn panel should render inside the world route after the mortal-world overview.");
        Assert.True(rebornPanelIndex < actionMenuIndex, "Reborn panel should be a conceptual section before the generic action catalogue.");

        var panelStart = app.IndexOf("function RebornSystemsPanel", StringComparison.Ordinal);
        var panelEnd = app.IndexOf("function FilteredActionSections", StringComparison.Ordinal);
        Assert.True(panelStart >= 0 && panelEnd > panelStart, "Reborn panel source slice should be bounded before generic action helpers.");
        var panelSource = app[panelStart..panelEnd];
        Assert.DoesNotContain("pending_", panelSource, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("control/", panelSource, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("/api/", panelSource, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("raw JSON", panelSource, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("debug", panelSource, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("advancedCommand}", panelSource, StringComparison.Ordinal);

        Assert.Contains(".reborn-systems-panel", styles, StringComparison.Ordinal);
        Assert.Contains(".reborn-systems-panel__header", styles, StringComparison.Ordinal);
        Assert.Contains(".reborn-systems-panel__actions", styles, StringComparison.Ordinal);

        Assert.Contains("#729", readme, StringComparison.Ordinal);
        Assert.Contains("Reborn panels", readme, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("UI-only mapping", readme, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("#729", hostDoc, StringComparison.Ordinal);
        Assert.Contains("reborn-panels.html", hostDoc, StringComparison.Ordinal);
        Assert.Contains("GM-facing afterlife contract docs were not changed", hostDoc, StringComparison.OrdinalIgnoreCase);
    }
```

- [ ] **Step 2: Run test to verify it fails**

Run:

```bash
dotnet test BookOfEternityClient.Tests/BookOfEternityClient.Tests.csproj --no-restore --filter "FullyQualifiedName~BrowserRebornPanels" --logger "console;verbosity=minimal"
```

Expected: FAIL because `RebornSystemsPanel`, `rebornSectionMatchers`, and `.reborn-systems-panel` do not exist yet.

- [ ] **Step 3: Commit only if this task is split from implementation**

Do not commit yet if immediately proceeding to Task 2 in the same closure unit.

---

### Task 2: Add failing built-frontend visual smoke guard

**Objective:** Require a dependency-light visual smoke artifact for locked and active Reborn states.

**Files:**
- Modify: `BookOfEternityClient.Tests/LocalWebUiBuiltFrontendSmokeTests.cs`

- [ ] **Step 1: Write the failing test changes**

In `BuiltFrontendSmoke_LaunchesHostWithViteDistAndCapturesDiagnostics`, add:

```csharp
        var rebornPanelsArtifactPath = Path.Combine(artifactRoot, "reborn-panels.html");
```

After existing writes for navigation/detail artifacts, add:

```csharp
        await File.WriteAllTextAsync(rebornPanelsArtifactPath, BuildRebornPanelsArtifact(appSource));
```

After detail-surface artifact assertions, add:

```csharp
        Assert.True(File.Exists(rebornPanelsArtifactPath), $"Missing browser Reborn panels visual smoke artifact at {rebornPanelsArtifactPath}");
        var rebornPanelsArtifact = await File.ReadAllTextAsync(rebornPanelsArtifactPath);
        Assert.Contains("data-artifact=\"browser-reborn-panels\"", rebornPanelsArtifact, StringComparison.Ordinal);
        Assert.Contains("data-viewport=\"desktop\"", rebornPanelsArtifact, StringComparison.Ordinal);
        Assert.Contains("data-state=\"mortal-locked\"", rebornPanelsArtifact, StringComparison.Ordinal);
        Assert.Contains("data-state=\"afterlife-active\"", rebornPanelsArtifact, StringComparison.Ordinal);
        Assert.Contains("data-viewport=\"mobile\"", rebornPanelsArtifact, StringComparison.Ordinal);
        Assert.Contains("Посмертие Reborn", rebornPanelsArtifact, StringComparison.Ordinal);
        Assert.Contains("Сияющая Обитель", rebornPanelsArtifact, StringComparison.Ordinal);
        Assert.Contains("Море Хаоса", rebornPanelsArtifact, StringComparison.Ordinal);
        Assert.Contains("Посмертные панели откроются", rebornPanelsArtifact, StringComparison.Ordinal);
        Assert.DoesNotContain("pending_", rebornPanelsArtifact, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("control/", rebornPanelsArtifact, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("/api/", rebornPanelsArtifact, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("raw JSON", rebornPanelsArtifact, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Debug", rebornPanelsArtifact, StringComparison.OrdinalIgnoreCase);
```

Add a `BuildRebornPanelsArtifact` helper near `BuildDetailSurfaceArtifact`:

```csharp
    private static string BuildRebornPanelsArtifact(string appSource)
    {
        Assert.Contains("detailSurfaceId=\"reborn-afterlife-overview\"", appSource, StringComparison.Ordinal);
        Assert.Contains("detailSurfaceId=\"reborn-shining-abode\"", appSource, StringComparison.Ordinal);
        Assert.Contains("detailSurfaceId=\"reborn-chaos-sea\"", appSource, StringComparison.Ordinal);
        Assert.Contains("Посмертие Reborn", appSource, StringComparison.Ordinal);
        Assert.Contains("Сияющая Обитель", appSource, StringComparison.Ordinal);
        Assert.Contains("Море Хаоса", appSource, StringComparison.Ordinal);

        return """
        <!doctype html>
        <html lang="ru" data-artifact="browser-reborn-panels">
        <head><meta charset="utf-8"><title>Browser Reborn Panels Visual Smoke</title></head>
        <body>
          <main>
            <section data-viewport="desktop" data-state="mortal-locked">
              <h1>Посмертие Reborn</h1>
              <article><strong>🕯️ Afterlife</strong><p>Посмертные панели откроются, когда душа перейдёт в посмертие.</p></article>
              <article><strong>✦ Сияющая Обитель</strong><p>Доступ к Обители появится после перехода в посмертный слой.</p></article>
              <article><strong>🌊 Море Хаоса</strong><p>Навигация Моря Хаоса ждёт подходящего царства.</p></article>
            </section>
            <section data-viewport="desktop" data-state="afterlife-active">
              <h1>Посмертие Reborn</h1>
              <article><strong>Afterlife</strong><p>Душа в посмертии · перья и просветление видны игроку.</p></article>
              <article><strong>Сияющая Обитель</strong><p>Сияние, искры света, залы и безопасные действия.</p></article>
              <article><strong>Море Хаоса</strong><p>Статус моря, ориентиры и доступные игровые действия.</p></article>
            </section>
            <section data-viewport="mobile" data-state="afterlife-active">
              <h1>Мобильный вид: Посмертие Reborn</h1>
              <p>Afterlife → Сияющая Обитель → Море Хаоса</p>
            </section>
          </main>
        </body>
        </html>
        """;
    }
```

- [ ] **Step 2: Run test to verify it fails**

Run:

```bash
dotnet test BookOfEternityClient.Tests/BookOfEternityClient.Tests.csproj --no-restore --filter "FullyQualifiedName~BuiltFrontendSmoke" --logger "console;verbosity=minimal"
```

Expected: FAIL until the frontend build and source include the new panel IDs.

---

### Task 3: Implement the React Reborn panel

**Objective:** Render the three explicit Reborn system cards using existing read-only game-screen data.

**Files:**
- Modify: `BookOfEternityClient.WebFrontend/src/App.tsx`

- [ ] **Step 1: Add matchers near existing `journalSectionMatchers`**

```typescript
const rebornSectionMatchers = ['afterlife', 'посмер', 'soul', 'душ', 'shining', 'сияющ', 'abode', 'обител', 'chaos', 'хаос', 'guardian', 'хранител', 'gate', 'врат'];
const shiningAbodeActionMatchers = ['shining', 'сияющ', 'abode', 'обител', 'radiance', 'сияни', 'spark', 'искра', 'hall', 'зал', 'gate', 'врат'];
const chaosSeaActionMatchers = ['chaos', 'хаос', 'sea', 'море', 'guardian', 'хранител', 'abode', 'обител'];
```

- [ ] **Step 2: Render `RebornSystemsPanel` in `WorldRoute`**

Insert after the mortal-world `split-grid three` and before `<ActionMenu menu={game.actionMenu} />`:

```tsx
      <RebornSystemsPanel game={game} />
```

- [ ] **Step 3: Add helper functions before `FilteredActionSections`**

Add `RebornSystemsPanel`, `filterActionsForPanel`, `formatRebornLockStatus`, `formatShiningGateStatus`, `formatActionPreview`, and `ActionPreviewList` using copy/paste from the implementation diff. These helpers must:

- include the comment `// UI-only mapping for #729: React renders existing C# game-screen state and action metadata without changing afterlife contracts.`;
- use `DetailSurfaceCard` IDs `reborn-afterlife-overview`, `reborn-shining-abode`, and `reborn-chaos-sea`;
- read `game.flags.isInAfterlifeRealm`, `game.flags.isInShiningAbode`, `game.flags.isInChaosSea`, `game.afterlife.*`, `game.soul.*`, and filtered action metadata;
- avoid raw filenames/endpoints/debug copy in default strings.

- [ ] **Step 4: Run focused test to verify pass**

Run:

```bash
dotnet test BookOfEternityClient.Tests/BookOfEternityClient.Tests.csproj --no-restore --filter "FullyQualifiedName~BrowserRebornPanels" --logger "console;verbosity=minimal"
```

Expected: PASS.

---

### Task 4: Add panel styling

**Objective:** Make Reborn panels read as a distinct conceptual group while sharing existing visual language.

**Files:**
- Modify: `BookOfEternityClient.WebFrontend/src/styles/components.css`

- [ ] **Step 1: Add styles**

Append near other shell/detail/action styles:

```css
.reborn-systems-panel {
  display: grid;
  gap: 18px;
  margin-top: 22px;
  padding: 20px;
  border: 1px solid rgba(216, 179, 106, 0.24);
  border-radius: 24px;
  background:
    radial-gradient(circle at top left, color-mix(in srgb, var(--realm-accent, #d8b36a) 18%, transparent), transparent 42%),
    rgba(255, 255, 255, 0.045);
}

.reborn-systems-panel__header {
  display: grid;
  gap: 8px;
  max-width: 780px;
}

.reborn-systems-panel__header h2 {
  margin: 0;
  color: #ffe2a6;
}

.reborn-systems-panel__actions {
  display: grid;
  gap: 10px;
}

.reborn-systems-panel__actions ul {
  margin: 0;
  padding-left: 1.1rem;
}

.reborn-systems-panel__actions li + li {
  margin-top: 8px;
}
```

- [ ] **Step 2: Run source guard**

Run the same `BrowserRebornPanels` test. Expected: PASS.

---

### Task 5: Update docs for UI-only contract impact

**Objective:** Document the Reborn panel boundary and visual smoke artifact.

**Files:**
- Modify: `BookOfEternityClient.WebFrontend/README.md`
- Modify: `docs/web-ui/local-web-host.md`

- [ ] **Step 1: Add README section**

Add a section containing these exact facts:

```markdown
## Reborn panels (#729)

The Browser Client renders player-facing Reborn panels for Afterlife, Shining Abode, and Chaos Sea from the existing `/api/game-screen` DTO and player-default action metadata. This is a UI-only mapping: the C# runtime remains the authority for afterlife state, command availability, validation, and all gameplay rules.

The panels intentionally avoid raw contract filenames, pending/control files, endpoint names, GM thoughts, and debug logs in the default player UI. Those details stay in advanced diagnostics or GM-facing documentation.

Because #729 does not add or rename afterlife action types, pending/control files, validation rules, canonical state fields, normalizer behavior, or GM-authored prompts, GM-facing afterlife contract docs were not changed for this UI slice.
```

- [ ] **Step 2: Add host doc note**

Add a bullet or paragraph mentioning:

```markdown
- `reborn-panels.html` — a dependency-light visual smoke artifact for the #729 Reborn panel grouping, including mortal locked and afterlife active states. The #729 change is UI-only; GM-facing afterlife contract docs were not changed because no runtime contract, pending/control file, validation rule, canonical state surface, or GM-authored behavior changed.
```

- [ ] **Step 3: Run focused test**

Run:

```bash
dotnet test BookOfEternityClient.Tests/BookOfEternityClient.Tests.csproj --no-restore --filter "FullyQualifiedName~BrowserRebornPanels" --logger "console;verbosity=minimal"
```

Expected: PASS.

---

### Task 6: Verify frontend, docs-sensitive tests, and commit

**Objective:** Prove the closure unit is correct and safe to publish.

**Files:**
- All files above plus plan/spec docs.

- [ ] **Step 1: Run focused Browser/Reborn tests**

```bash
dotnet test BookOfEternityClient.Tests/BookOfEternityClient.Tests.csproj --no-restore --filter "FullyQualifiedName~BrowserRebornPanels|FullyQualifiedName~BuiltFrontendSmoke" --logger "console;verbosity=minimal"
```

Expected: PASS. If `BuiltFrontendSmoke` requires `dist`, run `npm run verify --prefix BookOfEternityClient.WebFrontend` first and rerun.

- [ ] **Step 2: Run docs/afterlife/browser slice**

```bash
dotnet test BookOfEternityClient.Tests/BookOfEternityClient.Tests.csproj --no-restore --filter "AfterlifeDocumentationCoverageTests|ExampleDocumentationValidationTests|Browser" --logger "console;verbosity=minimal"
```

Expected: PASS.

- [ ] **Step 3: Run frontend verification**

```bash
npm run verify --prefix BookOfEternityClient.WebFrontend
```

Expected: PASS (`typecheck` then `vite build`).

- [ ] **Step 4: Run diff checks and security scan**

```bash
git diff --check
git diff --cached | grep "^+" | grep -iE "(api_key|secret|password|token|passwd)\s*=\s*['\"][^'\"]{6,}['\"]|os\.system\(|subprocess.*shell=True|\beval\(|\bexec\(|pickle\.loads?\(|execute\(f\"|\.format\(.*SELECT|\.format\(.*INSERT" || true
```

Expected: `git diff --check` exits 0; scan prints nothing relevant.

- [ ] **Step 5: Independent review**

Dispatch a reviewer with the final diff. Fix Critical/Important issues, rerun focused verification, and re-review until approved.

- [ ] **Step 6: Commit**

```bash
git add BookOfEternityClient.Tests/BrowserFrontendWorkspaceTests.cs \
  BookOfEternityClient.Tests/LocalWebUiBuiltFrontendSmokeTests.cs \
  BookOfEternityClient.WebFrontend/src/App.tsx \
  BookOfEternityClient.WebFrontend/src/styles/components.css \
  BookOfEternityClient.WebFrontend/README.md \
  docs/web-ui/local-web-host.md \
  docs/superpowers/specs/2026-05-26-browser-reborn-panels-design.md \
  docs/superpowers/plans/2026-05-26-browser-reborn-panels.md
git commit -m "feat(web): add Reborn afterlife panels"
```

Expected: commit succeeds with no generated artifacts staged.
