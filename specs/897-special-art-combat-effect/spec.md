# Feature Specification: Structured special-art combatEffect (#897)

**Feature Branch**: `codex/897-special-art-combat-effect`
**Created**: 2026-06-08
**Status**: Draft for autonomous implementation
**Input**: GitHub issue #897 — https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/897

## Source Issues & Scope

- **Source GitHub issue(s)**: #897 — https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/897
- **Related, non-closing context**: #895 defines the broader mechanically-actionable special-art need; #894 and #896 are follow-ups that depend on this contract shape.
- **Issue type**: afterlife contract / validation / GM prompt / docs / examples / player-facing UI enhancement.
- **Spec Kit justification**: This changes a GM-authored afterlife contract, validation rules, examples, and player-facing special-art surfaces after #898 introduced the shared `combatConditions[]` vocabulary. AGENTS.md requires Spec Kit for afterlife contract and validation changes.
- **Contract scope**: afterlife entity profile `specialArts[]`, spiritual-conflict audit usage, validation, console/browser shared command output, GM prompts/docs, examples, and documentation coverage tests.
- **Out of scope**: rewriting the ten Predvechnye Guardian dossier arts (#894), final broad coverage after those dossiers are stable (#896), deterministic automatic execution of every art as a mini-engine, or changing Mortal-world `combatEffect` item semantics.

## User Scenarios & Testing

### User Story 1 — GM authors a teachable special art with explicit ordinary combat value (Priority: P1)

As a GM/agent authoring an afterlife entity profile, I need every newly-authored/current teachable special art to carry a structured `combatEffect` so the art's ordinary afterlife-combat value is explicit beyond story/Saref utility.

**Why this priority**: Without a first-class field, learned special arts can be expensive narrative flavor with no auditable combat niche.

**Independent Test**: A validation test constructs an afterlife entity profile with a teachable `specialArts[]` entry and proves a meaningful `combatEffect` object is accepted while missing or generic values are rejected where the current contract applies.

**Acceptance Scenarios**:

1. **Given** a current teachable special art with `effectSummary` and a complete `combatEffect`, **When** validation runs, **Then** the profile is accepted and no repair-only ambiguity remains.
2. **Given** a current teachable special art with only `effectSummary`, **When** validation runs under the current contract examples/path, **Then** validation reports a missing or repair-worthy `combatEffect` according to the compatibility rule chosen in `plan.md`.
3. **Given** a special art whose `combatEffect.summary` is empty/generic such as "unique effect applies", **When** validation runs, **Then** validation rejects it with a specific field-level issue.

---

### User Story 2 — Player sees enough special-art combat niche text to choose upgrades (Priority: P1)

As a player using `/spiritual_arts`, `/afterlife_profiles`, or shared browser-rendered command results, I need visible special arts to show their ordinary combat niche without raw JSON, debug language, or premature Saref/Wings spoilers.

**Why this priority**: The player must be able to decide whether a special art is worth upgrading over a standard art with the same `baseOperation`.

**Independent Test**: A focused command-result/UI test loads a profile with visible special arts and asserts the rendered output includes combat-effect summary/trigger/limit/payoff text while hiding debug/raw fields and unsafe story spoilers.

**Acceptance Scenarios**:

1. **Given** a visible player-owned learned special art, **When** the player views a special-art/profile surface, **Then** the output shows `effectSummary` plus combat-effect niche, trigger, limit/counterplay, and allowed payoff in player-facing wording.
2. **Given** a hidden/GM-only or spoiler-gated special-art effect note, **When** ordinary player output is rendered, **Then** private details do not leak.

---

### User Story 3 — GM resolves named special arts through existing legal combat axes (Priority: P1)

As the GM resolving a spiritual conflict, I need prompt/docs/examples to require reading `specialArts[].combatEffect`, preserving `baseOperation`, and recording the applied effect through `specialArtAudit.effectNote` / `specialArtAudits[].effectNote` and legal existing surfaces.

**Why this priority**: The field must constrain GM-authored combat outcomes instead of becoming free-form power creep.

**Independent Test**: Documentation coverage and example-validation tests require the field, legal axes, and concrete `effectNote` usage in player-owned and non-player special-art examples.

**Acceptance Scenarios**:

1. **Given** a player-owned special art with `mechanicalAxis=rollMode`, **When** an exchange uses it, **Then** `specialArtAudit.effectNote` ties the applied advantage/disadvantage to the art's `combatEffect` and the dice audit uses legal sources.
2. **Given** an opposition Guardian special art with multiplied ОД cost, **When** the exchange is documented, **Then** `actionCostAudit` and `specialArtAudit.effectNote` explain the multiplier and combat-effect payoff.
3. **Given** a proposed combat effect, **When** it tries to bypass `baseOperation`, ignore the tactical matrix, or become a passive unlimited bonus, **Then** docs/validation/tests reject or flag the authoring pattern.

---

### User Story 4 — Backward-compatible transition for older profiles (Priority: P2)

As a maintainer, I need old saves/profiles that only have `effectSummary` to remain readable while current examples and newly-authored teachable arts move to `combatEffect`.

**Why this priority**: Existing saves must not become unusable, but current contract examples must teach the new field.

**Independent Test**: Validation distinguishes legacy tolerated data from current-contract examples or newly-authored teachable entries that require `combatEffect`, and the compatibility decision is documented.

**Acceptance Scenarios**:

1. **Given** an older profile entry that lacks `combatEffect` but has a non-empty `effectSummary`, **When** loaded/read for display, **Then** the client remains usable and displays a compatibility fallback if needed.
2. **Given** a current example or newly authored teachable art, **When** `combatEffect` is absent, **Then** validation/docs coverage treats it as missing contract authority.

### Edge Cases

- `combatEffect` must not collide with Mortal item/equipment `combatEffect` shapes; this issue scopes the field to afterlife entity-profile `specialArts[]`.
- `combatEffect.mechanicalAxis` must map to legal afterlife axes introduced or documented by #898: `rollMode`, `conflictPosition`, `controlState`, `sideStrain`, `tempoAdvantage`, `counterPayoff`, `actionEconomy`, `actionCostAudit`, and condition/payoff surfaces that remain inside the shared afterlife contract.
- `combatEffect` must not duplicate hard `controlState` as a condition, grant unlimited stacking, or silently bypass the spiritual-combat tactical matchup matrix.
- Player-facing output must not expose raw `game_state/` paths, debug DTOs, GM thoughts, hidden conditions, or Saref/Wings spoilers.
- GM-facing docs/examples must prove both player-owned and non-player special-art use, including multiplied ОД cost and concrete `specialArtAudit.effectNote`.

## Requirements

### Functional Requirements

- **FR-001**: The afterlife special-art contract MUST document `specialArts[].combatEffect` as a first-class structured field separate from `effectSummary`.
- **FR-002**: `combatEffect` MUST include at least meaningful `summary`, `trigger`, `mechanicalAxis`, `allowedPayoff`, `limit`, and `auditRequirement` fields, or an explicitly documented equivalent that covers the same semantics.
- **FR-003**: `combatEffect.mechanicalAxis` MUST be constrained to legal afterlife spiritual-combat surfaces; unsupported or free-form axes MUST be rejected or reported by validation.
- **FR-004**: Validation MUST accept meaningful current-contract `combatEffect` objects and reject empty/generic summaries, missing required subfields, unsupported axes, or passive power-creep wording where the current contract applies.
- **FR-005**: Backward compatibility MUST be explicitly handled for existing save/profile JSON that only has `effectSummary`; old data remains loadable, while current examples/newly-authored teachable arts require the structured field.
- **FR-006**: Special-art/player-facing UI surfaces MUST show enough `combatEffect` text for upgrade decisions and MUST keep technical/raw/private/spoiler details out of default output.
- **FR-007**: GM prompts/docs MUST require reading `combatEffect`, preserving `baseOperation` as the primary operation lane, and explaining any applied effect through `specialArtAudit.effectNote` / `specialArtAudits[].effectNote`.
- **FR-008**: Worked examples MUST include one player-owned learned special art and one non-player Guardian/opposition special art using `combatEffect`; at least one maps to `rollMode`/`conflictPosition`, and at least one maps to `controlState`, `tempoAdvantage`, `sideStrain`, `counterPayoff`, `actionEconomy`, or `actionCostAudit`.
- **FR-009**: Documentation/source-guard tests MUST prevent future GM-facing docs/examples from reducing special arts to generic flavor-only effects.
- **FR-010**: Implementation MUST avoid closing or editing #894/#896 content beyond minimal compatibility examples required for #897; Predvechnye dossier rewrites remain a follow-up.

### Key Entities

- **Afterlife special art**: An entry in `afterlife_entity_profiles.json` `profiles[].specialArts[]` with owner identity, `artId`, `name`, `baseOperation`, `costMultiplierPercent`, `upgradeCost`, `effectSummary`, training fields, and the new `combatEffect` object for ordinary combat niche authority.
- **Special-art combatEffect**: Structured afterlife-only effect metadata describing a player-safe summary, trigger, legal mechanical axis, allowed payoff, limit/counterplay, and audit requirement.
- **Special-art audit**: `specialArtAudit` / `specialArtAudits[]` inside spiritual-conflict exchanges, including `effectNote` that references the applied `combatEffect` and the legal state/audit surface used.
- **Combat condition/payoff vocabulary**: The #898 `combatConditions[]` and legal mechanical-axis vocabulary that `combatEffect` may reference but must not replace with a separate mini-engine.

## Success Criteria

### Measurable Outcomes

- **SC-001**: Focused validation tests pass for meaningful `combatEffect` accepted and missing/generic/unsupported values rejected with non-zero test counts.
- **SC-002**: Focused UI/command-result tests pass showing combat-effect text on default special-art surfaces and no raw/private/spoiler leakage.
- **SC-003**: Documentation/example tests pass for `AfterlifeDocumentationCoverageTests|ExampleDocumentationValidationTests` with non-zero counts.
- **SC-004**: At least two worked examples demonstrate player-owned and non-player special-art `combatEffect` use with concrete `effectNote` and legal payoff surfaces.
- **SC-005**: `git diff --check origin/main...HEAD`, build, focused afterlife tests, and static added-line scan excluding `specs/**` pass before PR/merge.

## Verification Plan

- **C# verification**: Focused validation/UI tests around afterlife special arts, afterlife profiles, spiritual conflict special-art audits, and special-art/player command rendering; build test project.
- **Documentation/contract verification**: `dotnet test BookOfEternityClient.Tests/BookOfEternityClient.Tests.csproj --filter "AfterlifeDocumentationCoverageTests|ExampleDocumentationValidationTests" --logger "console;verbosity=minimal"` plus any new focused source guards.
- **Frontend verification**: Not required unless implementation touches React/Vite frontend; shared browser DTO/source guards are enough if rendering comes from C# command results.
- **Manual/player-facing verification**: Inspect representative `/spiritual_arts` or `/afterlife_profiles` output if feasible; otherwise rely on command-result tests and examples.

## Assumptions

- #898 combatConditions and payoff vocabulary is already merged in `main` and is the immediate dependency for this feature.
- The C# client remains the source of validation/output authority; React/browser remains presentation-only if touched.
- `effectSummary` continues to exist for story/Saref-safe summary, while `combatEffect` expresses ordinary afterlife-combat value.
- Old profile JSON without `combatEffect` should not crash or become unreadable; current contract examples and newly authored teachable arts should carry the field.
- GitHub Actions are not a required gate for this closure unit; local verification and independent review are the gate.
