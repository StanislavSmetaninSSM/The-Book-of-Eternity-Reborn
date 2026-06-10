# Feature Specification: NPC detail-section drill-down menus

**Feature Branch**: `work/946-npc-drilldown`

**Created**: 2026-06-11

**Status**: Draft for autonomous implementation

**Input**: GitHub issue #946 — add NPC detail-section drill-down menus for rich mortal-world NPCs.

## Source Issues & Scope *(mandatory)*

- **Source GitHub issue(s)**: #946 https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/946
- **Related issue(s)**: #928 NPC fixture fallback; #947 books document selection; #948 mortal read-only detail-drilldown audit.
- **Issue type**: player-facing console/browser UX and read-only command-result parity.
- **Spec Kit justification**: The issue changes a player-facing `/npc` inspection flow, spans console and browser surfaces, and needs durable parity/UX acceptance criteria. It is not a tiny one-file fix.
- **Contract scope**: read-only player-facing NPC inspection and command-result/detail metadata. No new GM-authored runtime contract, local-write flow, pending/control file, NPC mutation, afterlife/Chaos Sea/Shining Abode contract, or React gameplay authority is introduced.
- **Out of scope**: changing `/npc_talk`, `/npc_trade`, NPC relationship/trade mutations, accepted-turn update contracts, broad #948 mortal audit work, `/книги` reading flow (#947), and afterlife detail surfaces (#949).

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Console player can open NPC detail sections (Priority: P1)

After choosing an NPC from `/нпс` / `/npc`, the console still shows the existing overview, but the player can then choose focused sections such as journal/thoughts, personal quests, activities, relationships, skills/effects/inventory, and memory/custom states when that data exists.

**Independent Test**: A focused console-friendly test or source guard proves a selected NPC exposes a second-level section menu/projection with entries for journal/thoughts, personal quests, and activities when fixture data contains those sections.

**Acceptance Scenarios**:

1. **Given** an NPC with journal/thought entries, personal quest data, and activity data, **When** the player selects that NPC in the console `/npc` flow, **Then** the overview remains available and a second-level section menu offers the populated sections.
2. **Given** an NPC lacks a section, **When** the section menu is built, **Then** the missing section is omitted rather than showing an empty raw panel.
3. **Given** the player opens a section, **When** the section is rendered, **Then** it contains only that focused data plus player-facing back/overview navigation.

---

### User Story 2 - NPC thoughts/journals are inspectable separately (Priority: P1)

A player can inspect an NPC's thoughts/journal separately from the long mixed overview, with count/status hints and no raw JSON.

**Independent Test**: A focused test builds or executes the NPC detail-section projection for an NPC with `npc_journals.json` and `npc_interaction_journal.json` data and asserts a journal/thoughts section exists with player-facing Russian labels and content.

**Acceptance Scenarios**:

1. **Given** an NPC has journal entries, **When** the player opens the journal/thoughts section, **Then** entries are shown in a readable focused panel.
2. **Given** journal data is absent, **When** the section menu is shown, **Then** the journal/thoughts entry is absent or clearly disabled with a player-facing reason only if a disabled entry pattern already exists.

---

### User Story 3 - NPC personal quests and activities are inspectable separately (Priority: P1)

A player can inspect personal quests, objectives/rewards/failure consequences, and current/completed activities without scrolling the full NPC overview.

**Independent Test**: Focused tests cover an NPC with one personal quest containing objectives/rewards/failure consequences and one activity, then assert quest and activity section detail blocks are separate from the overview.

**Acceptance Scenarios**:

1. **Given** an NPC has personal quest data, **When** the player opens the personal quests section, **Then** the quest details, objectives, rewards, and failure consequences are readable in that section.
2. **Given** an NPC has current/completed activities, **When** the player opens the activities section, **Then** the current/completed activity details are readable in that section.

---

### User Story 4 - Browser parity is explicit and player-facing (Priority: P1)

The browser `/npc` command result exposes equivalent drill-down data/actions or a deliberately linked follow-up if full browser interactivity proves too large for this focused issue.

**Independent Test**: Browser command-result tests assert NPC section summaries/detail blocks or action metadata are present for rich NPCs, use Russian player-facing copy, and do not expose raw `/api`, DTO, debug, or JSON framing in default mode.

**Acceptance Scenarios**:

1. **Given** the same rich NPC fixture, **When** `/npc` is executed through the browser command pipeline, **Then** the result contains section-level detail affordances or structured blocks equivalent to the console sections.
2. **Given** a section can be opened from browser, **When** the browser submits the selected read-only detail action, **Then** C# command/result authority provides the detail; React does not invent NPC gameplay rules.
3. **Given** full browser interactivity is larger than this issue, **When** implementation closes #946, **Then** a dedicated linked follow-up issue records the exact browser gap and #946 evidence explains the boundary.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: The existing full NPC overview MUST remain available as the default first detail screen or an explicit `overview/current full summary` section after selecting an NPC.
- **FR-002**: Console `/npc` MUST provide a second-level section menu or equivalent navigation for a selected NPC when two or more rich data sections exist.
- **FR-003**: The section list MUST include player-facing entries for populated journal/thoughts, personal quests, activities/current/completed, relationships/locks, skills/effects/inventory/equipment, and memory/interactions/custom states where those data sets exist.
- **FR-004**: Empty sections MUST not appear as raw/blank panels; they should be omitted unless an existing disabled-entry pattern with a clear player-facing reason is used.
- **FR-005**: Section labels SHOULD include counts or short status hints when the underlying data supports them.
- **FR-006**: Each focused section MUST render readable Russian player-facing content and MUST NOT expose raw JSON, API, DTO, endpoint, or debug language in default mode.
- **FR-007**: Browser `/npc` command results MUST expose equivalent section-level data/actions or the implementation MUST create and link a scoped follow-up before closing #946.
- **FR-008**: Browser UI/React changes, if any, MUST remain presentation-only over typed C# DTO/command-result authority and MUST preserve advanced/debug separation.
- **FR-009**: The implementation MUST NOT enable, imply, or change mutating NPC talk/trade/local-turn actions unless strict existing authority already permits them.
- **FR-010**: Focused tests MUST include an NPC containing at least thoughts/journals, one personal quest with objectives/rewards, and one activity.
- **FR-011**: No afterlife/Chaos Sea/Shining Abode runtime contract or pending/control surface is changed by this issue.

### Data Entities

- **NPC overview**: existing selected-NPC summary assembled from `npc_core.json` plus supplementary NPC files.
- **NPC detail section**: read-only projection for one topic, with label, count/status hint, availability, and focused player-facing blocks.
- **NPC rich data files**: `npc_journals.json`, `npc_interaction_journal.json`, `npc_goals.json`, `npc_activities.json`, `npc_relationships.json`, `npc_inventory.json`, `npc_effects.json`, `npc_skills.json`, `npc_memory.json`, `npc_masks.json`, `npc_fate_cards.json`, and `npc_custom_states.json` where already used by the NPC overview.
- **Browser command result**: typed C# `ExplorerCommandResult`/`UiBlock` surface consumed by the browser frontend.

## Success Criteria *(mandatory)*

- **SC-001**: A focused RED test proves rich NPC section drill-down is missing before implementation.
- **SC-002**: After implementation, focused NPC section tests pass and cover journal/thoughts, personal quest detail, and activities.
- **SC-003**: Existing NPC overview tests and prompt-escaping/source-guard tests remain green.
- **SC-004**: Browser command-result evidence exists for equivalent section-level data/actions, or a linked follow-up issue documents any intentionally deferred browser-interactivity slice.
- **SC-005**: `git diff --check`, focused C# tests, relevant build gates, and static scan pass before PR.

## Verification Plan *(mandatory)*

- **Baseline before implementation**: run a focused C# slice covering NPC command/browser/console/source-guard surfaces before Codex starts, then record exact counts in `tasks.md` and the Codex prompt.
- **Focused tests**: `dotnet test BookOfEternityClient.Tests/BookOfEternityClient.Tests.csproj -p:IsTestProject=true --filter "FullyQualifiedName~Npc|FullyQualifiedName~ExplorerWebCommandServiceTests|FullyQualifiedName~GameInterfaceTests|FullyQualifiedName~ExplorerModeSourceGuardTests" --logger "console;verbosity=minimal"` or a narrower updated filter named by the implementation.
- **Build**: `dotnet build BookOfEternityClient/BookOfEternityClient.csproj --no-restore`; build tests project when tests change.
- **Frontend**: `npm run verify --prefix BookOfEternityClient.WebFrontend` if React/frontend files change.
- **Spec Kit**: `powershell.exe -NoProfile -ExecutionPolicy Bypass -File .specify/scripts/powershell/check-prerequisites.ps1 -Json -RequireTasks -IncludeTasks` from the feature branch.
- **Diff/static hygiene**: `git diff --check origin/main...HEAD`; added-line static security scan excluding `specs/**` and scratch plans.

## Assumptions

- The first implementation slice should prefer reusable read-only projections over duplicating section parsing separately in console and browser.
- The console path may keep the full overview as the first view, but the player must have a natural way to jump into focused sections afterward.
- If a browser detail selection protocol is already present, reuse it. If not, add the smallest C# command-result/action metadata needed or create a follow-up if full browser UI would exceed #946.
- GM-facing prompt updates are not required unless implementation changes what the GM must author. Player-facing command/docs may still need updates if command behavior/capabilities change.
