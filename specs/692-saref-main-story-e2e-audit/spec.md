# Feature Specification: Saref Main Story E2E Audit

**Feature Branch**: `codex/692-saref-e2e-audit`

**Created**: 2026-06-08

**Status**: Ready for implementation

**Input**: GitHub issue #692 — `[Saref Main Story] E2E audit and progression walkthrough for the hidden main storyline`

## Source Issues & Scope *(mandatory)*

- **Source GitHub issue(s)**: #692 — https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/692
- **Issue type**: audit / task / hidden-story verification
- **Spec Kit justification**: #692 is a large hidden-story/afterlife/Saref audit spanning runtime state, validation, normalizer behavior, player commands, GM-facing documentation, worked examples, and follow-up issue creation. It needs durable decomposition and cross-session handoff.
- **Contract scope**: runtime-state, validation, canonical normalizer, afterlife pending/control lifecycle, console player commands, browser write surfaces where relevant, GM-facing prompts/docs/examples, documentation coverage tests.
- **Out of scope**: rewriting the Saref storyline, changing canon for `Крылья над Бездной`, introducing new browser/console UX beyond audited parity gaps, or requiring the unfinished #674-#679 true interactive E2E harness. If the audit discovers large missing behavior or new contracts, create tracked follow-up issues or update this spec before broadening implementation.

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Stage matrix and deterministic state fixtures (Priority: P1)

A maintainer can read one walkthrough matrix and run deterministic fixture tests that prove each Saref main-story stage has the required canonical state, player visibility, GM response surface, pending/control rules, and validation expectations.

**Why this priority**: Without the stage matrix and fixtures, the rest of the audit has no stable baseline and agents can only test isolated validators.

**Independent Test**: Run the focused Saref validation/normalizer/docs filter and confirm valid stage fixtures pass while negative variants fail with specific issue codes.

**Acceptance Scenarios**:

1. **Given** each key Saref stage (`unknown`, `shadow`, `name_revealed`, `wings_revealed`, `infiltration_active`, `confrontation_available`, `completed`, `oathbound_to_saref`, defeat, oath-break), **When** the stage fixture is validated, **Then** required canonical fields are present and validation succeeds.
2. **Given** an invalid early reveal, advantage, Wings request, final confrontation, deal, broken oath, or defeat state, **When** validation runs, **Then** a concrete validation issue identifies the missing proof or illegal transition.

---

### User Story 2 - Anti-spoiler player command and Memory layer checks (Priority: P1)

A player using `/сареф`, `/сареф найти_крылья`, and `/воспоминание` receives stage-appropriate non-spoiler output, valid blocking reasons, and proper Memory-scene quest-4 handling without leaking future Saref revelations.

**Why this priority**: Hidden-story player safety is the central risk of this audit. Commands must protect mystery progression before later lifecycle branches matter.

**Independent Test**: Run focused Explorer command/result tests for Saref and Memory commands, plus validation tests for quest-4 `memorySceneProof` requirements.

**Acceptance Scenarios**:

1. **Given** `unknown` or `shadow` Saref state, **When** `/сареф` is invoked, **Then** the player sees only non-spoiler text such as “Ты пока не знаешь, что искать.” and no Wings/final content.
2. **Given** `name_revealed` without `wings_revealed`, **When** `/сареф` is invoked, **Then** known fragments and available advantages may be summarized but Wings details stay hidden.
3. **Given** an invalid realm or missing route, **When** `/сареф найти_крылья` is invoked, **Then** it blocks with player-facing reasons and does not create a duplicate or illegal pending request.
4. **Given** quest 4 completion, **When** the GM records `Воспоминание`, **Then** the state uses `memoryScene.layer="Воспоминание"`, `memorySceneProof`, and does not create `pendingMemoryLegacy` or physical mortal-item transfer.

---

### User Story 3 - Wings lifecycle, final branches, deal, defeat, and oath-break audit coverage (Priority: P2)

A maintainer can prove the hidden main storyline progresses through Wings infiltration, final confrontation outcomes, deal/post-story assignments, defeat mitigation, and oath-break routes without inconsistent terminal states or untracked pending cleanup.

**Why this priority**: These branches are high-impact and GM-authored, but they depend on the stage and anti-spoiler foundations from US1 and US2.

**Independent Test**: Run focused lifecycle tests for `SarefMainStoryState`, validation, and canonical normalizer paths for Wings, final, deal, defeat, and oath-break state.

**Acceptance Scenarios**:

1. **Given** a valid Wings route, **When** `BuildWingsInfiltrationRequest` creates the request and the GM accepts a matching closure, **Then** reveal/refuse/block modes update or clean up pending state only according to the accepted closure.
2. **Given** each final route (`combat`, `political`, `oath_law`, `metaphysical`, `hybrid`, `deal`), **When** validation runs, **Then** required rewards/effects, Guardian relationship effects, relic/passive reward, and Shining faction links are present or explicitly audited as missing follow-ups.
3. **Given** deal/post-story agenda, defeat, or oath-break states, **When** validation and normalizer paths run, **Then** `postStoryAgenda`, mitigation, terminal/non-terminal defeat distinction, and oath-break consequences are consistent.

---

### User Story 4 - GM-facing documentation, examples, and follow-up issue report (Priority: P3)

A GM has enough examples and contract guidance to run the Saref line without guessing field names, and any remaining gaps are tracked as separate GitHub follow-up issues rather than hidden inside the audit PR.

**Why this priority**: AGENTS.md and the constitution require GM-facing docs/examples to stay synchronized with afterlife and hidden-story contracts.

**Independent Test**: Run `AfterlifeDocumentationCoverageTests` and `ExampleDocumentationValidationTests`; inspect the final PR/issue comment for verified follow-up issue links when gaps remain.

**Acceptance Scenarios**:

1. **Given** the updated GM-facing docs and examples, **When** documentation coverage tests run, **Then** required surfaces for quest-4 Memory scene, Wings search/closure, final confrontation, deal/post-story, defeat, and oath break are covered or linked to follow-up issues.
2. **Given** a discovered gap too large for this PR, **When** the audit is finalized, **Then** a separate GitHub issue is created with scope, evidence, and dependency notes.

### Edge Cases

- Early Saref reveal or advantage grant without quest-4 Memory proof must fail validation.
- Wings search must not start in Chaos Sea, without route fragments, or while another incompatible afterlife/Shining pending contract is active.
- Matching accepted closure must remove the exact pending Wings request; non-matching turns must not silently remove it.
- Deal route must not be treated as terminal game-over; `postStoryAgenda.state=oathbound_to_saref` remains active.
- Defeat outcomes must distinguish forced oath, exile, memory suppression, soul dissipation, and pyrrhic escape; only proved player soul dissipation may be terminal.
- Oath-break with intimate/personal oath must require tragic context and consequences, not free release.
- Player-facing command text must not leak raw JSON, file paths, API/DTO terms, or hidden stage names outside advanced/debug contexts.
- GM-facing examples must remain valid JSON fragments and must not invent fields absent from validation/normalizer authority.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: The audit MUST define or update a stage-by-stage Saref walkthrough matrix covering all stages named in #692.
- **FR-002**: Valid deterministic fixtures for key stages MUST pass game-state validation.
- **FR-003**: Negative deterministic states for early reveal, unsupported advantage, illegal Wings request, premature final, invalid deal, invalid oath break, and invalid defeat MUST fail with concrete validation issue evidence.
- **FR-004**: `/сареф`, `/сареф найти_крылья`, and `/воспоминание` MUST be checked for anti-spoiler, realm guard, duplicate pending, and player-facing copy behavior.
- **FR-005**: Quest-4 Memory handling MUST require `memorySceneProof` and must not use Memory Gates, `pendingMemoryLegacy`, or physical mortal item transfer as authority for Saref revelation/advantage grants.
- **FR-006**: Wings infiltration lifecycle MUST be audited from request creation to accepted closure and cleanup for reveal/refuse/block modes.
- **FR-007**: Final confrontation, deal/post-story, defeat mitigation, and oath-break branches MUST be covered by tests, fixtures, examples, or explicitly linked follow-up issues.
- **FR-008**: GM-facing docs, examples, manifest entries, and documentation coverage/source-guard tests MUST be updated when the audit changes or clarifies authoring expectations.
- **FR-009**: The final PR/issue closure evidence MUST list verified paths, test commands and counts, discovered gaps, follow-up issues, and remaining risks.
- **FR-010**: No runtime contract or canonical state change may land without synchronized docs/examples/tests and Spec Kit artifact updates.

### Key Entities

- **Saref stage matrix**: Human-readable audit artifact mapping hidden-story stage to required fields, player visibility, GM actions, pending/control files, validators, and tests.
- **Saref deterministic fixture**: Minimal file-backed game-state root or test builder state for one stage or invalid scenario.
- **Memory scene proof**: Quest-4 evidence object proving `Воспоминание` completion and legal Saref revelation/advantage authority.
- **Wings infiltration request**: Pending afterlife contract for finding/revealing/refusing/blocking the Wings route.
- **Final outcome / post-story agenda / defeat / oath-break state**: Canonical branch records that determine whether the line is complete, oathbound, mitigated, terminal, or reopened.
- **Follow-up issue**: GitHub issue created when the audit discovers a distinct missing implementation or docs gap too large for the current PR.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: The focused Saref/docs verification command reports non-zero discovered tests and passes after implementation.
- **SC-002**: The audit PR includes at least one durable stage matrix or fixture-builder note that covers every stage named in #692.
- **SC-003**: At least the player command anti-spoiler path and quest-4 Memory proof path have automated coverage or an explicit tracked follow-up if impossible in the current architecture.
- **SC-004**: Any GM-facing authoring change is reflected in `OtherGuides/`, `Examples/`, manifests, and documentation/source-guard tests in the same PR.
- **SC-005**: The final issue comment names all follow-up issues for gaps not closed by this audit and does not close #692 until local verification and independent review pass.

## Verification Plan *(mandatory)*

- **C# verification**: `dotnet test BookOfEternityClient.Tests/BookOfEternityClient.Tests.csproj -p:IsTestProject=true --filter "FullyQualifiedName~SarefMainStory|FullyQualifiedName~CanonicalStateNormalizerTests.SarefMainStory|FullyQualifiedName~AfterlifeDocumentationCoverageTests|FullyQualifiedName~ExampleDocumentationValidationTests" --logger "console;verbosity=minimal"`
- **Documentation/contract verification**: `dotnet test BookOfEternityClient.Tests/BookOfEternityClient.Tests.csproj --no-restore -p:IsTestProject=true --filter "FullyQualifiedName~AfterlifeDocumentationCoverageTests|FullyQualifiedName~ExampleDocumentationValidationTests" --logger "console;verbosity=minimal"`
- **Frontend verification**: N/A unless the audit changes browser Saref write surfaces; if touched, also run `npm run verify --prefix BookOfEternityClient.WebFrontend`.
- **Manual/player-facing verification**: Review command-result output or focused tests for `/сареф`, `/сареф найти_крылья`, and `/воспоминание`; true keyboard E2E is deferred until the #674-#679 harness is available.

## Assumptions

- Agent Console live-control prerequisites #749-#753 are already closed, satisfying the scheduling note that moved #692 behind that series.
- The audit may start before the full console E2E harness from #674-#679 by using deterministic state, validation, normalizer, command-result, docs, and example tests.
- The existing C# runtime remains the source of canonical game logic; React/browser code may only be touched if existing Saref browser write parity is directly implicated.
- The current PR should close auditable gaps found during implementation, but broad new mechanics discovered by the audit should become follow-up issues instead of unbounded scope creep.
