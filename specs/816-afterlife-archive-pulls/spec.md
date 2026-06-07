# Feature Specification: Browser Afterlife Archive Actions and Direct Pull

**Feature Branch**: `codex/816-afterlife-archive-pulls`
**Created**: 2026-06-08
**Status**: Draft for autonomous implementation
**Input**: GitHub issue #816, "feat(web): Архив посмертия — консультация, топливо проектов, прямой pull"
**Source Issue**: [#816](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/816)
**Umbrella Issue**: [#817](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/817)

## Source Issues & Scope *(mandatory)*

- **Source GitHub issue(s)**: [#816](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/816), [#817](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/817)
- **Issue type**: browser-client parity enhancement for console afterlife archive actions and direct Chaos Sea gacha pull.
- **Spec Kit justification**: #816 changes browser/console parity, player-facing prompt UX, afterlife local write behavior, existing GM-facing pending/action contracts, command/action discovery, and tests across several C#/browser surfaces. It needs durable handoff across Spec Kit, Superpowers, and Codex.
- **Console source of truth**: `BookOfEternityClient/UI/ExplorerMode/ExplorerMode.Afterlife.InkFeathersAndOfferings.cs` methods `StartArchiveConsultationAsync`, `StartArchiveProjectFuelAsync`, `ReadGuardiansForArchiveOperationAsync`, `ResolveArchiveFuelTarget`, and `ShowGachaInfo`.
- **Runtime authority**: existing C# services and contracts: `AfterlifeArchiveConsultationService`, `AfterlifeArchiveProjectFuelService`, `AfterlifeArchiveActionState`, `AfterlifeArchiveState`, `BrowserAfterlifeWriteService`, `BrowserAfterlifeTurnRequestQueue`, `PendingTurnStateService`, and `game_state/meta/soul_state.json`.
- **Contract scope**: player-facing browser UI, console/browser parity, existing afterlife archive pending request files, direct Chaos Sea gacha queueing, C# tests, browser command metadata/fixtures. GM-facing docs/examples are required only if this work adds or changes runtime contract shapes; reuse of the existing archive/direct-gacha contracts should be documented in the PR/report as no new GM contract.
- **Out of scope**: umbrella #817 closure, new archive entry types, new guardian/gacha banners, new reward materialization mechanics, changing accepted-turn authority signatures, React-side gameplay mutation, and card-heavy Browser Client redesign.

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Request archive consultation from the browser (Priority: P1)

As an afterlife player with an eligible archive entry, I can open a browser guided form for archive consultation, choose a friendly Guardian, confirm the reservation, and create the same pending consultation request the console creates for GM resolution.

**Independent Test**: Seed `soul_state.json` with an allowed, unreserved archive entry and `guardians.json` with at least one friendly Guardian (`reputation >= 50`); open/submit the browser archive consultation prompt and verify the entry is reserved, `AfterlifeArchiveActionState.ConsultationRequestPath` is written with `requestedMode=consultation`, the GM action text uses the existing consultation action tag, and the browser result is Russian/player-facing.

**Acceptance Scenarios**:

1. **Given** an afterlife realm, an allowed unreserved archive entry, no pending consultation request, and a friendly Guardian, **When** the browser opens consultation, **Then** the prompt lists eligible Guardians with reputation/domain copy and requires confirmation.
2. **Given** the prompt remains valid and the player confirms, **When** consultation is submitted, **Then** the existing C# consultation service creates/commits the pending request and reserves the archive entry without inventing a new contract.
3. **Given** the player cancels, does not confirm, leaves afterlife, lacks eligible Guardians, has a reserved/unsupported entry, or a pending/malformed consultation request exists, **When** the browser opens/submits, **Then** the browser blocks with player-facing copy and writes nothing.

---

### User Story 2 - Fuel an active Guardian project from the archive in the browser (Priority: P1)

As an afterlife player, I can use a stored archive entry to support a friendly Guardian's active project from the browser, matching the console archive project fuel flow.

**Independent Test**: Seed an eligible archive entry, friendly Guardian, and `GuardianProjectState.TrackerPath` with an active project for that Guardian; open/submit the browser project-fuel prompt and verify `AfterlifeArchiveActionState.ProjectFuelRequestPath` is written with `requestedMode=project_fuel`, `targetProjectId`, the archive entry is reserved, and no write occurs for stale/cancelled/blocked paths.

**Acceptance Scenarios**:

1. **Given** a friendly Guardian has an active project, **When** the browser opens archive project fuel, **Then** only eligible Guardian/project choices are listed.
2. **Given** the prompt remains valid and the player confirms, **When** project fuel is submitted, **Then** the existing C# project fuel service creates/commits the pending request and returns a player-facing result summary.
3. **Given** no eligible Guardian project exists, a pending/malformed project fuel request exists, the archive entry is reserved, or the prompt is stale, **When** the browser opens/submits, **Then** the action is blocked and `soul_state.json` plus pending request files remain unchanged.

---

### User Story 3 - Direct Chaos Sea gacha pull is verified as browser parity for #816 (Priority: P2)

As a Chaos Sea player with Ink Feathers, I can perform or discover the browser direct Chaos Sea gacha pull using the existing direct-gacha contract, and #816 does not remain open because direct pull coverage was merely assumed.

**Independent Test**: Audit current `/gacha` browser support. If existing support is complete, add/update #816 coverage evidence without duplicating implementation; if gaps remain, add RED tests and implement the missing prompt/session/menu/coverage behavior. Tests must prove the browser path spends Ink Feathers once, stages rollback/snapshot evidence, queues the existing `[CHAOS_SEA_DIRECT_GACHA]` GM action, preserves the pending gacha base, and avoids concrete local relic materialization.

**Acceptance Scenarios**:

1. **Given** current browser direct gacha is complete, **When** #816 command coverage/fixtures are inspected, **Then** direct pull is listed as supported evidence for #816 without closing archive consultation/project fuel early.
2. **Given** direct gacha has gaps, **When** the browser submit path is implemented, **Then** it uses only the existing direct Chaos Sea gacha contract, existing rollback/snapshot authority, and queued GM turn request.
3. **Given** unsupported banners, missing confirmation, non-positive cost, insufficient Feathers, wrong realm, or malformed state, **When** direct gacha is submitted, **Then** the browser blocks and writes nothing new.

### Edge Cases

- `soul_state.json` is missing, malformed, has `afterlifeArchive.stored` missing/empty, or has entries in legacy-normalized shapes.
- Archive entry identifiers can be selected by stable `archiveId`; labels may include titles, rarity, entry type, and reservation state, but result/blocker copy must not expose local paths or raw JSON in default UI.
- Current realm changes between prompt open and submit.
- Friendly Guardian eligibility changes between prompt open and submit; project active state changes between prompt open and submit.
- Existing pending consultation/project fuel request files are present, malformed, or become present after prompt open.
- Direct gacha cost/balance/pending dice state changes between prompt open and submit.
- Browser forms may use internal ids as hidden values, but default labels, blockers, and results must remain Russian/player-facing and must not expose DTO/API/endpoint/validation/debug/exception wording.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: Browser command/action surfaces MUST expose guided prompt access for archive consultation and archive project fuel using Russian player-facing labels.
- **FR-002**: Browser prompt-open and submit paths MUST re-check afterlife/Chaos Sea realm, local write/GM-turn blockers, archive entry eligibility, reservation state, friendly Guardian eligibility, active project availability, pending request file state, and confirmation before writing.
- **FR-003**: Archive consultation submit MUST reuse `AfterlifeArchiveConsultationService.CreateRequestAsync(... commit:false)` plus `CommitPreparedRequestAsync` or an equivalent existing C# authority path; it MUST write the existing consultation pending request shape and reserve the selected archive entry.
- **FR-004**: Archive project fuel submit MUST reuse `AfterlifeArchiveProjectFuelService.CreateRequestAsync(... commit:false)` plus `CommitPreparedRequestAsync` or an equivalent existing C# authority path; it MUST write the existing project fuel pending request shape, include `targetProjectId`, and reserve the selected archive entry.
- **FR-005**: Failed, cancelled, unconfirmed, insufficient-eligibility, malformed-state, wrong-realm, blocked-write, and stale-submit paths MUST leave `soul_state.json` and existing archive pending request files unchanged.
- **FR-006**: Direct Chaos Sea gacha MUST be audited and covered for #816. If existing browser `/gacha` support already satisfies the issue, update tests/coverage/fixtures/evidence only; if not, implement missing pieces using the existing direct gacha write and queueing contract.
- **FR-007**: Browser help, command/action metadata, command coverage, API fixtures, and frontend fixtures MUST recognize #816 archive/direct-pull parity as covered when implementation lands; #817 remains open until all child parity work is verified.
- **FR-008**: React MUST remain generic prompt/result presentation. Gameplay rules, file writes, pending request creation, Ink Feather spending, and GM action queueing MUST stay in C# services.
- **FR-009**: Default browser labels, blockers, and results MUST be Russian/player-facing and avoid raw `.json`, local paths, DTO/API/endpoint/validation/debug/exception wording.
- **FR-010**: Focused RED tests/source guards MUST be added before production implementation and MUST cover successful consultation, successful project fuel, stale/cancelled/blocked paths, direct gacha evidence or gaps, command coverage, and player-facing copy.
- **FR-011**: Runtime contract shapes MUST stay unchanged unless the Spec Kit artifacts are revised and GM-facing docs/examples/tests are updated in the same branch.

### Key Entities

- **Archive Entry**: Existing `soul_state.json.afterlifeArchive.stored[]` item with stable `archiveId`, `title`, `entryType`, rarity/source metadata, and reservation fields.
- **Friendly Guardian Choice**: Existing guardian display entry with `guardianId`, display name, domain, and reputation threshold matching console (`>= 50`).
- **Archive Consultation Request**: Existing `AfterlifeArchiveActionState.PendingArchiveConsultationRequest` pending file and GM action tag.
- **Archive Project Fuel Request**: Existing `AfterlifeArchiveActionState.PendingArchiveProjectFuelRequest` pending file, target Guardian project, and GM action tag.
- **Direct Chaos Sea Gacha Pull**: Existing `/gacha` browser prompt/write/queue path and `[CHAOS_SEA_DIRECT_GACHA]` GM action contract.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: Focused browser parity tests prove archive consultation prompt open/submit creates the existing pending consultation request and reserves exactly one selected archive entry.
- **SC-002**: Focused browser parity tests prove archive project fuel prompt open/submit creates the existing pending project fuel request for an active Guardian project and reserves exactly one selected archive entry.
- **SC-003**: Stale/blocked submit tests prove no state mutation occurs when realm, archive entry, Guardian/project, pending request, confirmation, or local-write eligibility changes after prompt open.
- **SC-004**: Direct gacha tests/coverage either verify existing complete browser support for #816 or prove any missing behavior was added without new unsupported banner mechanics.
- **SC-005**: Browser command/help/menu/coverage/API fixtures no longer list #816 as unsupported after implementation; #817 remains tracked as the umbrella epic.
- **SC-006**: Local focused tests, relevant docs/contract tests if needed, C# builds, frontend verification when fixtures change, `git diff --check`, and added-line static scan all pass before PR/merge.

## Assumptions

- Archive consultation and project fuel are existing GM-facing afterlife contracts; browser parity should reuse them rather than changing their shapes.
- Existing `BrowserAfterlifeWriteService.ApplyGachaPullAsync` and `BrowserAfterlifeTurnRequestQueue` may already satisfy direct pull; #816 implementation must inspect and prove this instead of duplicating a second direct-gacha path.
- Browser discovery can use command descriptors, `/feathers`, command menu/action metadata, prompt sessions, or existing game-screen action surfaces as long as default UI stays player-facing and aligned with the minimalist command-composer direction.
