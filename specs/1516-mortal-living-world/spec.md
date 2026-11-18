# Feature Specification: Mortal living-world materialization

**Feature Branch**: `1516-mortal-living-world`  
**Created**: 2026-11-16  
**Status**: WIP: full-engine journey verification reopened; not merged

## Source Issues & Scope

- Source: [#1516](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/1516).
- Contract-sensitive enhancement: validation, canonical publication, GM guidance, examples, and tests.
- Mortal only. No afterlife/spiritual combat, new simulation AI, forced event generation, UI redesign, or old-save migration. Current bootstrap/test fixtures follow the supported schema.
- #1368 is closed `not_planned`, not a successful live acceptance result; this work neither reopens it nor claims its acceptance. #1540 remains separate; new private evidence must not leak through current news views.
- The user delegated routine implementation choices and requested continued work without intermediate permission pauses. Technical choices below are implementer decisions, not claimed verbatim user approval.

## User Scenarios & Testing

### US1 — Account for elapsed world time (P1)
After a meaningful time advance, the player receives a world whose accepted changes are justified, rather than advancing only personal counters. The GM may legitimately conclude that no significant autonomous event occurred.

1. Crossing the existing configured world/faction cycle boundary, including during this turn, requires a correlated complete living-world assessment.
2. Missing, stale, wrong-realm or structurally incomplete assessments fail with actionable repair information.
3. A complete bounded no-significant-change assessment is accepted without inventing NPCs, projects, actors or state deltas.
4. Short ordinary player scene actions without autonomous events are outside the trigger.

### US2 — Materialize coherent events (P1)
An autonomous event/action identifies its time window, participating entities or systems, targets, causes, visibility/knowledge, concrete canonical effects, consequences, news/chronicle evidence and provenance.

1. Exact referenced NPC/faction/location/project identities resolve against authoritative supported state.
2. Declared effects prove actual changed semantic values between authenticated original and accepted canonical state. A pending command, `filesModified`, unrelated cosmetic edit or prose alone is not effect evidence.
3. New supported autonomous event/project records are complete on first acceptance; historical/bootstrap narrative records do not retroactively invent mechanics.
4. Existing reducers remain authoritative for NPC/faction/location/project lifecycle. The report is not an arbitrary JSON mutation API.

### US3 — Preserve atomicity, privacy and recovery (P1)

1. Event effects and their versioned client-owned receipt are accepted together; reached late failure restores exact original bytes/absence through the existing publication owner.
2. Retry cannot duplicate accepted history or grant permission from serialized receipt data.
3. Private causes, undiscovered references and audit/provenance stay on a private surface. Player news receives only explicitly visible information, with both console/browser retaining their common projection.
4. A repair is bounded to the offending package/row/reference and does not authorize new semantics to be guessed.

## Requirements

- **FR-001** Derive meaningful-time trigger from authoritative preturn clock/schedule and resulting accepted clock, covering already pending cycles and this turn's newly due cycles.
- **FR-002** Require exact session/request/turn correlation; reject foreign/future/replayed reports and malformed/duplicate properties.
- **FR-003** Support explicit autonomous-change packages and legal bounded no-significant-change assessments; never force change solely to satisfy a counter.
- **FR-004** Require complete event semantics, exact cross-surface identities, explicit visibility, original/canonical effect evidence, consequence and chronicle/news proof.
- **FR-005** Validate effects after existing reducers; do not invent IDs, actors, causes, rewards, progress or missing payloads during normalization.
- **FR-006** Retain authenticated source and current ownership checks until publication; integrate common rollback, including new report/history paths and their absence.
- **FR-007** Persist versioned, source-bound client-owned immutable receipt/history once per accepted package; reject history tampering and conflicting replay. "Immutable" means the accepted owner binds and rechecks the exact declaration, history and completed-state evidence across its supported publication/recovery flow. The portable local-storage fingerprints are integrity checks, not cryptographic provenance against a coordinated rewrite of the affected joint completion marker and living-world history by the same user process. Those two relevant client-owned witnesses can be rewritten coherently without changing the authenticated original or clocks.
- **FR-008** Preserve privacy of new private evidence and existing undiscovered references. No general #1540 redesign.
- **FR-009** Synchronize current GM entrypoints, guide, autonomous/no-change worked examples, validation manifest and source/documentation tests. Document no-update decisions.
- **FR-010** Use independent cheap typed unit fixtures plus real canonical publication/rollback tests; impact-selected official runner only, no hosted CI or broad suite.

## Edge Cases

Initial 24h skip; pending cycles with no new time; short scene; absent/malformed clock; wrong or ambiguous actor; new/deleted target; project completion; stale evidence; cosmetic-only change; duplicate event IDs; hidden location references; no actors/projects; negative/reversed window; late failure; repeated normalization; changed source after preparation; Bootstrap narrative-only opening; unsupported old saves.

## Success Criteria

- Missing audit for newly meaningful elapsed time cannot pass the selected real acceptance path.
- Autonomous and legal unchanged examples pass the actual contract implementation.
- Counter-only, wrong-owner, wrong-effect, partial-package, stale-source and private-news cases fail precisely.
- A real reached late failure restores affected canonical files and receipt absence; retry succeeds once without duplicate history.
- Independent review finds no unresolved blocking requirement in the bounded implementation; limitations are recorded instead of claiming live/Windows/full-suite coverage.

## Clarifications and assumptions

Existing cycle configuration supplies the meaningful-time threshold (default world 240 minutes, faction 1440); no new arbitrary hourly threshold. The client checks explicit declarations and state evidence, not the truth of unstructured natural-language assertions through NLP. No-significant-change is an explicit GM decision with a bounded review of current relevant systems, not a client simulation result. Any significant autonomous state claim must be declared and evidenced.

## Live-discovered NPC terminal activity contract, 2026-11-18 (bounded implementation verified)

Live investigation found that the documented `completeNPCActivities` command
lacked a canonical consumer. T019 now supplies the bounded implementation,
verified by the targeted outcome recorded in live-relay-phase-plan.md. This is
a bounded completion of existing Mortal activity
materialization, not a new simulation or reward system. The delegated working
choice is explicit outcome data, never a prose-derived/default outcome:

- Require `NPCId`, `NPCName`, `activityName`, `finalState`, `finalOutcome` and
  `narrativeSummary` for an existing original active NPC activity.
- `Completed` means brought to a terminal result and admits explicit `Success`,
  `SuccessWithComplication` or `Failure`. `Abandoned` admits only `Failure`.
  Missing/unknown outcomes and the other two Abandoned combinations fail closed.
- `completionTurn` is client-owned, obtained from the exact authenticated original
  request; a command-supplied value is rejected. Do not infer identity, outcome,
  actors, XP, rewards or references from narrative text.
- Keep `finalState` command-local. Canonical history retains its existing exact
  four fields: `activityName`, integer `completionTurn`, `finalOutcome`,
  `narrativeSummary`. Clear `currentActivity` to null and append exactly once;
  preserve previous history, unrelated state and agreeing canonical NPC mirrors.
- Resolve exact original NPC/name/activity and current-source agreement before
  writes. Missing original/owner, contradictory mirrors, duplicate targets,
  same-turn newly born NPCs, mixed progress/completion or unsupported specialized
  publication routes refuse. No generic unsourced normalizer call gains authority.
- Reuse existing normalizer rollback inventory for npc_core/npc_activities and
  consume the command only inside the same accepted publication. A failed turn
  must preserve/restore its original bytes and accepted history; same-request
  repair must not append twice.
- Current fixtures/prompts/examples migrate to the explicit field; there is no
  legacy save compatibility or automatic old-command fallback requirement.

The bounded T019 implementation does not qualify other activity/faction
commands, NPC progression or the whole living-world feature. Live outcomes and
their exact limits are recorded separately in live-relay-phase-plan.md.
