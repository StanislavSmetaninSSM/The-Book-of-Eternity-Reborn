# Feature Specification: Browser Home Action Hierarchy

**Feature Branch**: `work/791-browser-home-hierarchy`
**Created**: 2026-06-17
**Source GitHub issue**: [#791 feat(browser): Home page — кнопки действий и визуальная иерархия](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/791)
**Parent epic**: [#680 Browser Client](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/680)

## Summary

Improve the Browser Client home launcher (`GameLauncher`) so the first screen clearly separates interactive player actions from passive information, shows unavailable launcher actions as intentionally disabled with reasons, makes validation/error state visible through an in-world warning treatment, and keeps the home hero atmosphere readable even when no dynamic scene image is available.

## Spec Kit applicability

This issue changes the default Browser Client player-facing UX and visual hierarchy. It is frontend presentation work over shared C# main-menu state, so a Spec Kit feature is required. The feature must not alter C# gameplay authority, console behavior, GM-authored contracts, save/load rules, runtime image generation, or advanced/debug surfaces.

## User Stories and Tests

### User Story 1 - Launcher actions look interactive (Priority: P1)

As a player opening the browser client, I want the Home action choices to read as clickable game actions rather than passive cards, so I immediately understand how to continue, train, configure, or inspect the book.

**Independent Test**: Source/DOM guard verifies `GameLauncher` renders launcher action buttons with explicit action affordance classes, icon/arrow affordance, hover/focus-visible styling, and primary-action treatment distinct from passive `.launcher-mode-panel` / information blocks.

**Acceptance Scenarios**:

1. **Given** the Home route renders, **When** launcher actions are displayed, **Then** enabled actions have a distinct action style with accent border/gradient, hover or focus lift, and a visible arrow or local icon affordance.
2. **Given** a passive details panel is displayed below the launcher actions, **When** it is compared with enabled actions, **Then** it does not inherit the strongest CTA treatment or imply clickability unless it contains an explicit button.
3. **Given** the primary action is selected from shared main-menu state, **When** it renders, **Then** it remains visually stronger than secondary enabled actions without making disabled actions look active.

---

### User Story 2 - Disabled launcher actions explain themselves (Priority: P1)

As a player, I want unavailable Home actions such as load/new chapter to look disabled and tell me why, so I do not mistake them for broken controls.

**Independent Test**: Source/DOM guard verifies disabled launcher buttons expose a disabled state class/data attribute, use a player-facing reason from the shared `BrowserMainMenuDto` action, and do not receive active/primary styling.

**Acceptance Scenarios**:

1. **Given** `Загрузить сохранение` is unavailable, **When** the launcher action renders, **Then** it is disabled, subdued, and shows a short player-facing explanation instead of only `пока недоступно`.
2. **Given** `Начать новую главу` is unavailable, **When** the launcher action renders, **Then** it is disabled, subdued, and the mode panel repeats the reason in an accent/warning treatment.
3. **Given** an action is disabled, **When** the player hovers or focuses nearby actions, **Then** the disabled action does not lift, glow, or look clickable.

---

### User Story 3 - Validation/error state has visual priority without becoming debug UI (Priority: P2)

As a player returning to a save with validation warnings or errors, I want the Home screen to surface the state as an in-world warning pill or accent block, so I notice the problem without seeing raw API/debug framing.

**Independent Test**: Frontend guard verifies `GameLauncher` derives a visible warning element from `menu.session.validationLabel` / session status copy and that the default text remains Russian/player-facing with no raw `/api`, endpoint, DTO, JSON, or debug wording.

**Acceptance Scenarios**:

1. **Given** `menu.session.validationLabel` contains an error/warning state such as `Сессия читается, но валидация обнаружила ошибки: 9`, **When** Home renders, **Then** the message appears in a warning pill/accent-border element rather than plain body text.
2. **Given** there is no active session or no warning, **When** Home renders, **Then** the screen remains calm and intentional rather than showing a false error state.

---

### User Story 4 - Home hero has ambient fallback art/pattern (Priority: P2)

As a player, I want the Home hero to feel like a dark-fantasy game launcher even when no dynamic scene image exists, so the first screen does not collapse to plain gradients or a broken image.

**Independent Test**: Source and visual-smoke guard verifies Home has a local decorative image or CSS ambient pattern, a readability overlay, and a no-broken-image fallback path.

**Acceptance Scenarios**:

1. **Given** `/main-menu-bg.webp` loads, **When** Home renders, **Then** the image is decorative (`aria-hidden`, empty alt) and subdued behind readable foreground text.
2. **Given** the local image fails or dynamic scene images are absent, **When** Home renders, **Then** CSS ambient pattern/gradient remains visible and no broken image icon or raw file/path wording is player-visible.
3. **Given** desktop and narrow/mobile layouts, **When** the visual-smoke artifact is inspected, **Then** action text remains readable and images/patterns do not occlude player text.

## Functional Requirements

- **FR-001**: `GameLauncher` MUST keep using `BrowserMainMenuDto` and existing shell route transitions as authority for action availability and behavior.
- **FR-002**: Enabled launcher actions MUST have explicit action affordance styling distinct from passive panels.
- **FR-003**: Primary action styling MUST be stronger than ordinary enabled actions and MUST NOT apply to disabled actions.
- **FR-004**: Disabled launcher actions MUST display a player-facing reason derived from the matching main-menu action when available.
- **FR-005**: Validation/session warning copy on Home MUST render in a visible warning/accent treatment, not as plain muted text.
- **FR-006**: Home decorative art/pattern MUST be local/runtime-independent and remain readable under overlay on desktop and narrow layouts.
- **FR-007**: Default Home UI MUST remain Russian and player-facing. It MUST NOT expose raw API, DTO, endpoint, JSON, debug, Spec Kit, agent, or implementation language.
- **FR-008**: The change MUST NOT alter console behavior, C# gameplay rules, save/load/new-chapter contracts, GM-facing prompts/examples, afterlife/mortal runtime contracts, or runtime image generation.

## Scope Boundaries

**In scope**:

- `BookOfEternityClient.WebFrontend/src/components/GameLauncher.tsx` presentation markup.
- `BookOfEternityClient.WebFrontend/src/styles/*.css` launcher hierarchy, warning, disabled, and ambient fallback styles.
- Frontend source/DOM guard tests under `BookOfEternityClient.WebFrontend/test/`.
- Optional browser visual-smoke artifact under `TestResults/browser-smoke/` if useful for review evidence.
- Focused C# browser workspace/source guard updates only if existing guards need to know about new launcher selectors.

**Out of scope**:

- New browser game mechanics or React-side save/load/new-chapter rules.
- New runtime image generation, `ImageService`, or `game_session/images` changes.
- Sidebar navigation redesign (#790).
- Imagegen asset catalog and first asset set (#929).
- Parent Browser Client epic closure (#680).
- Console UI changes.
- GM-facing docs/examples, afterlife/mortal contract changes, validation contract changes, or prompt changes.

## Verification Requirements

Minimum local gates for this feature:

1. Frontend focused guard/test for launcher hierarchy.
2. `npm run verify --prefix BookOfEternityClient.WebFrontend`.
3. Focused .NET browser workspace/source guard when C# tests are relevant: `dotnet test BookOfEternityClient.Tests/BookOfEternityClient.Tests.csproj -p:IsTestProject=true --filter "FullyQualifiedName~BrowserFrontendWorkspaceTests|FullyQualifiedName~LocalWebUiBuiltFrontendSmokeTests|FullyQualifiedName~LocalWebUiDocumentationTests" --logger "console;verbosity=minimal"`.
4. `git diff --check origin/main...HEAD`.
5. Added-line static security scan excluding Spec Kit/docs false positives.
6. Visual smoke evidence: Vite preview observation or a dependency-light local HTML artifact that is explicitly not called an automated screenshot.
