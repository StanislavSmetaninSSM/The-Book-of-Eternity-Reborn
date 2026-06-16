# Feature Specification: Spiritual Conflict Exchange and Art Drill-Downs

**Feature Branch**: `work/1067-spiritual-conflict-art-drilldowns`

**Created**: 2026-06-17

**Status**: Drafted for autonomous implementation

**Input**: GitHub issue #1067 — "[Task] Add spiritual conflict exchange and art drill-downs"

## Source Issues & Scope *(mandatory)*

- **Source GitHub issue(s)**: #1067 — https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/1067
- **Origin audit**: #949 AFD-006 — https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/949 and `docs/audits/afterlife-drilldown-audit.md`.
- **Issue type**: Browser Client read-only/detail parity for spiritual-conflict exchange rows, combat-log events, and spiritual-art rows.
- **Spec Kit justification**: Required. #1067 changes player-facing Browser Client UX and console/browser parity across multiple afterlife command-result surfaces, and it is the final child of the #949 afterlife drill-down audit sequence.
- **Contract scope**: shared C# command-result blocks/actions for existing spiritual-conflict read-only browser/console-compatible surfaces. Existing dice, rewards, validation, normalizers, pending/control files, write/prompt authority, GM prompts/examples/manifests, and React gameplay logic are out of scope unless implementation proves a runtime contract change and updates required docs/tests in the same PR.
- **Primary surfaces**: `/spiritual_conflict`, `/spiritual_combat_log`, `/spiritual_arts`, and contextual `/spiritual_combat_help` links where they help players understand an exchange/art without creating a selected-row requirement for help itself.
- **Explicitly out of scope**: already-closed #1063 Guardian/Abode drill-downs, #1064 Soul Relic/Archive details, #1065 Shining Abode inspection details, #1066 profile/inbox follow-through, new afterlife write operations, new pending/control files, new GM-authored contracts, spiritual-combat dice/reward mechanics, React-side gameplay authority, and broad visual redesign.

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Players can inspect one active conflict exchange (Priority: P1)

A player reading `/spiritual_conflict` in the browser can open a concrete exchange/action row from the active conflict and understand why the current combat state changed without reading raw state.

**Why this priority**: AFD-006 found that browser overview surfaces exist, but selected exchange details are not consistently available as drill-down actions. Spiritual conflict decisions are hard to interpret from broad summaries alone.

**Independent Test**: Focused browser command-result tests seed an active spiritual conflict with `exchangeLog[]`, execute `/spiritual_conflict`, assert that concrete exchange rows expose safe read-only detail actions, execute the selected-detail command/action path, and verify Russian/in-world output for actor, target/opposition, action, dice/rolls, position, tension, costs, and result without raw JSON, API/DTO/debug wording, hidden/gm-only fields, or local paths.

**Acceptance Scenarios**:

1. **Given** an active spiritual conflict has visible exchange entries, **When** `/spiritual_conflict` renders in default browser mode, **Then** each concrete visible exchange row exposes a safe inspect action.
2. **Given** the player opens one exchange detail, **When** the exchange can be resolved from canonical state, **Then** the result keeps the overview available and adds a focused detail describing the exchange in Russian/in-world copy.
3. **Given** an exchange id/index is stale or missing, **When** the detail action is submitted, **Then** the result explains that the exchange is unavailable and does not leak raw ids, file paths, JSON, parser details, or generic protocol text.

---

### User Story 2 - Players can inspect combat-log and recent-conflict events (Priority: P1)

A player reading `/spiritual_combat_log` can open a specific log event or recent conflict outcome and see the decision/outcome context rather than a raw summary or generic completion surface.

**Why this priority**: The combat log is the historical record players use to understand previous spiritual-conflict outcomes and reward context; selected detail should not require manual state reconstruction.

**Independent Test**: Tests seed `exchangeLog[]` and `recentConflicts[]`, execute `/spiritual_combat_log`, assert safe actions for concrete log/recent-conflict rows, execute selected details, and verify readable event/outcome/reward/context copy with hidden/gm-only material suppressed in default mode.

**Acceptance Scenarios**:

1. **Given** recent conflicts exist, **When** `/spiritual_combat_log` renders, **Then** each concrete event/outcome row exposes a read-only detail action.
2. **Given** a recent conflict includes resolution/reward/context fields, **When** its detail is opened, **Then** player-facing output explains the result and reward context without changing state.
3. **Given** combat-log state is sparse or partially malformed, **When** overview or detail renders, **Then** default output remains safe and diagnostic/raw parser details stay out of the ordinary player path.

---

### User Story 3 - Players can inspect one spiritual art and reach relevant help (Priority: P2)

A player reading `/spiritual_arts` can inspect a specific spiritual art before choosing a local upgrade/use flow, and can reach contextual `/spiritual_combat_help` where it clarifies the art or exchange.

**Why this priority**: `/spiritual_arts` is a mutating-parity surface, but the missing slice is read-only inspection before acting. Help is explanatory, so it should support context only where useful rather than becoming a new entity-detail system.

**Independent Test**: Tests seed spiritual-art rank/level/effect/cost data, execute `/spiritual_arts`, assert read-only art detail actions exist alongside existing local-turn actions, open one art detail, and verify no pending/write state is created. Optional contextual help links are tested only if implementation exposes them.

**Acceptance Scenarios**:

1. **Given** spiritual arts are visible, **When** `/spiritual_arts` renders, **Then** a concrete art row exposes a read-only inspect action that does not replace or bypass existing local upgrade/write authority.
2. **Given** the player opens one art detail, **When** the art can be resolved, **Then** the detail shows current rank/level, cost/reduction/effect, availability, and relevant combat use in Russian/in-world terms.
3. **Given** contextual help is available, **When** an exchange or art detail links to help, **Then** `/spiritual_combat_help` remains explanatory and does not require a selected-row lifecycle.

---

### User Story 4 - Read-only boundary and existing contracts are preserved (Priority: P2)

The feature improves selected-detail presentation only. Spiritual-conflict dice/reward/validation mechanics, pending/control state, spiritual-arts write authority, and GM-facing contracts remain authoritative and unchanged.

**Why this priority**: #1067 is an AFD-006 browser parity child, not a runtime contract rewrite.

**Independent Test**: Command-result tests plus existing migration/afterlife audit tests continue to pass; docs/prompts impact review records no runtime contract changes unless implementation discovers a true contract mismatch and updates required docs/tests.

**Acceptance Scenarios**:

1. **Given** existing overview/help commands still render, **When** selected details/actions are added, **Then** overview output remains present and useful.
2. **Given** existing `/spiritual_arts` local upgrade/write flows exist, **When** read-only art details are opened, **Then** no pending/write state is created and mutating flows still route through existing C# prompt/write services.
3. **Given** no runtime contract changes are made, **When** closure evidence is written, **Then** it explicitly states that afterlife GM docs/examples/manifests were not changed because the diff is presentation/read-only only.

### Edge Cases

- Missing, sparse, stale, malformed, hidden, or gm-only rows must produce safe default output and never raw state/path/debug leakage.
- Dynamic GM-authored text must remain escaped/sanitized before Spectre.Console markup or browser-rendered HTML.
- Detail actions should prefer stable IDs already present in canonical state/action metadata; if an ID cannot be resolved safely, show a player-facing unavailable reason rather than inventing state.
- Advanced/debug mode may expose raw identifiers/diagnostics where existing advanced pathways allow it; ordinary default output must not.
- `/spiritual_combat_help` is explanatory; do not invent help-row entities unless implementation discovers already-existing help anchors that can be linked safely.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: `/spiritual_conflict` default browser command results MUST expose safe read-only detail actions for concrete visible active-conflict exchange rows when canonical state contains resolvable exchange entries.
- **FR-002**: Selected exchange details MUST render actor/opposition, action intent, dice/roll or contest context, position/tension changes, costs, outcome, and reward/reason fields when present, using Russian/in-world player copy.
- **FR-003**: `/spiritual_combat_log` default browser command results MUST expose safe read-only detail actions for concrete visible log events and recent-conflict rows when canonical state contains resolvable entries.
- **FR-004**: Selected combat-log/recent-conflict details MUST render event/outcome/resolution/reward context and suppress hidden/gm-only material in default mode.
- **FR-005**: `/spiritual_arts` default browser command results MUST expose a read-only inspect action for concrete spiritual-art rows without replacing existing local-turn upgrade/write actions.
- **FR-006**: Selected spiritual-art details MUST render rank/level, cost or action-point impact, effect/usage context, availability, and existing upgrade/write boundaries in Russian/in-world copy.
- **FR-007**: Missing, stale, unsupported, sparse, or malformed selected targets MUST return a clear player-facing unavailable state and MUST NOT leak raw JSON, `JsonException`, local paths, file names, `Path:`, `LineNumber`, `BytePositionInLine`, API/DTO/endpoint/protocol/debug wording, or hidden/gm-only fields in default mode.
- **FR-008**: Read-only exchange/log/art detail actions MUST NOT mutate game state, mark anything acknowledged/read, create pending/control files, or route through write/prompt services.
- **FR-009**: Existing overview/help output MUST remain available and useful after detail actions are added.
- **FR-010**: The implementation MUST link closure evidence back to #949 AFD-006 and this #1067 Spec Kit feature.

### Non-Functional Requirements

- **NFR-001**: Keep C# gameplay/application logic authoritative; React/browser frontend must remain presentation-only unless existing renderer gaps require pure presentation changes.
- **NFR-002**: Preserve console/browser semantic parity for the exposed command-result actions; browser details may differ visually but must expose equivalent player meaning.
- **NFR-003**: Prefer focused helper functions in existing afterlife command-result builders/services; avoid broad refactors or new runtime schema layers.
- **NFR-004**: Verification must include focused RED/GREEN tests, broad afterlife/browser/console slice, C# builds if C# changes, Spec Kit prerequisite check, `git diff --check`, and added-line static/security scans.

### Key Entities

- **Spiritual Conflict Exchange**: A visible active-conflict exchange/log entry authored in canonical spiritual-conflict state. The detail resolves one entry by stable id or safe index and renders action/dice/position/tension/outcome context.
- **Spiritual Combat Log Event**: A visible historical log or recent-conflict row from spiritual-conflict state. The detail resolves one entry and renders outcome/reward/resolution context.
- **Spiritual Art**: A visible spiritual-art/rank/level entry used by spiritual-conflict systems. The detail renders current level/rank, effect, cost, availability, and existing write boundaries.
- **Contextual Help Link**: A read-only link to explanatory `/spiritual_combat_help` output where useful. It does not create new mutable state or help-row lifecycle.

## Success Criteria *(mandatory)*

- **SC-001**: Focused tests fail before implementation for missing exchange/log/art detail actions and pass after implementation.
- **SC-002**: Browser command-result tests prove selected exchange, combat-log/recent-conflict, and spiritual-art details render without raw/default diagnostic leakage.
- **SC-003**: Tests prove missing/stale/sparse targets render player-facing unavailable copy and do not mutate pending/write state.
- **SC-004**: Existing afterlife/browser/console migration and audit tests pass after the change.
- **SC-005**: Closure evidence records no afterlife docs/examples impact when no runtime/GM contract changes occur; if runtime/GM contracts change, required docs/tests are updated in the same PR.

## Review & Completion Notes

Hermes owns final acceptance, independent review, PR, merge, issue evidence comment, label transition, and cleanup. Codex may implement and commit the feature, but should not close GitHub issues or mark Hermes-owned lifecycle tasks complete without evidence.
