# Afterlife Progression And Combat Roadmap Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Close issues #457-#467 in a dependency-safe order, ending with a coherent afterlife combat/progression system and synchronized GM-facing documentation.

**Architecture:** Implement one issue at a time, with a small branch/commit/PR boundary per issue or tightly coupled issue pair. Stabilize the core combat contract first, then add action economy, player progression, entity profiles, entity-world progression, special arts, dice extensions, difficulty, soul dissipation, and final documentation coverage.

**Tech Stack:** C#/.NET client, JSON canonical game-state files, ExplorerMode UI commands, validation services, canonical normalizer, StateDistributor/FileMapping, xUnit tests, GM-facing Markdown/TXT contract docs.

---

## Issue Order

1. #457 - RPS rebalance of afterlife spiritual combat.
2. #458 - Spiritual action points and recovery.
3. #459 - Spirit Focus / `Средоточие Души` progression.
4. #460 - Unified afterlife entity profiles.
5. #461 - Custom states for afterlife entities.
6. #462 - Automatic entity progression by strategy.
7. #463 - Special spiritual arts and player learning.
8. #465 - Advantage/Disadvantage for afterlife dice.
9. #466 - Difficulty integration and reward multiplier.
10. #464 - Soul Dissipation as terminal post-victory action.
11. #467 - Final cross-cutting documentation and tests.

Do not start a later phase until the earlier phase builds, targeted tests pass, and the issue is reviewed or explicitly accepted.

---

## Cross-Cutting Rules

- [ ] Work only against a tracked GitHub issue.
- [ ] Keep each issue implementation reviewable; avoid combining unrelated runtime contracts.
- [ ] For every Chaos Sea/Shining Abode runtime contract change, update GM-facing docs in the same change.
- [ ] Prefer TDD: write failing validation/UI/doc tests before implementation.
- [ ] Keep Russian player-facing terminology primary; English enum/property names appear only as JSON contract identifiers.
- [ ] Run documentation-sensitive afterlife tests before every issue handoff:

```powershell
dotnet test BookOfEternityClient.Tests\BookOfEternityClient.Tests.csproj --no-restore --filter "ExampleDocumentationValidationTests|AfterlifeDocumentationCoverageTests"
```

---

## File Map

**Core combat contract**

- Modify: `BookOfEternityClient/Services/AfterlifeSpiritualConflictState.cs`
- Modify: `BookOfEternityClient/Services/Validation/ValidationService.AfterlifeSpiritualConflict.cs`
- Modify: `BookOfEternityClient/UI/ExplorerMode/ExplorerMode.Afterlife.SpiritualConflict.cs`
- Test: `BookOfEternityClient.Tests/AfterlifeSpiritualConflictValidationTests.cs`
- Test: `BookOfEternityClient.Tests/ExplorerModeCommandTests.Afterlife.cs`

**State routing and canonicalization**

- Modify: `BookOfEternityClient/Configuration/FileMapping.cs`
- Modify: `BookOfEternityClient/IO/StateDistributor.cs`
- Modify: `BookOfEternityClient/Services/CanonicalStateNormalizer.cs`
- Create as needed: `BookOfEternityClient/Services/CanonicalStateNormalizer/CanonicalStateNormalizer.AfterlifeEntityProfiles.cs`
- Modify snapshot/baseline files only if a new canonical tracked file is added.

**Entity profile subsystem**

- Create: `BookOfEternityClient/Services/AfterlifeEntityProfileState.cs`
- Create: `BookOfEternityClient/Services/Validation/ValidationService.AfterlifeEntityProfiles.cs`
- Create or extend: ExplorerMode profile/status command partials.
- Test: new `BookOfEternityClient.Tests/AfterlifeEntityProfileStateTests.cs`
- Test: new `BookOfEternityClient.Tests/AfterlifeEntityProfileValidationTests.cs`

**GM-facing docs**

- Modify: `OtherGuides/Afterlife_Contract_Matrix.md`
- Modify: `OtherGuides/Afterlife_Combat_Terminology_Glossary.md`
- Modify: `Examples/E_CLI_Afterlife_Turns.txt`
- Modify: `Examples/example_validation_manifest.json`
- Modify: `CLI_Agent_Daemon_Specification.md`
- Modify: `CLI_API_Specification.md`
- Modify: `TaskGuides/CLI_Step_Main.txt`
- Test: `BookOfEternityClient.Tests/AfterlifeDocumentationCoverageTests.cs`
- Test: `BookOfEternityClient.Tests/ExampleDocumentationValidationTests.cs`

---

## Phase 0: Preflight For Each Issue

**Files:**

- Read issue body with `gh issue view <number>`.
- Read current git status.
- Read the relevant implementation and test files listed in the issue.

- [ ] **Step 1: Confirm the worktree state**

```powershell
git status --short --branch
```

- [ ] **Step 2: Create or switch to an issue branch**

```powershell
git switch -c issue-<number>-short-name
```

- [ ] **Step 3: Capture baseline targeted tests**

Run the narrowest relevant existing tests for the issue and record failures before editing.

- [ ] **Step 4: Re-read GM-facing docs**

For afterlife contract changes, read the matrix, glossary, daemon spec, API spec, task guide, and examples before writing tests.

---

## Phase 1: Close #457 - RPS Combat Semantics

**Goal:** Make every spiritual operation have a clear effect, counterplay, failure profile, and Russian help text.

**Files:**

- Modify: `ValidationService.AfterlifeSpiritualConflict.cs`
- Modify: `AfterlifeSpiritualConflictState.cs`
- Modify: `ExplorerMode.Afterlife.SpiritualConflict.cs`
- Modify: afterlife combat glossary, matrix, daemon/API/task docs, examples.
- Test: afterlife validation, ExplorerMode help/log, documentation coverage.

- [ ] **Step 1: Write failing tests for guard mitigation**

Successful guard prevents/reduces harm; failed guard still mitigates at least one step.

- [ ] **Step 2: Write failing tests for counter risk/payoff**

Counter must require a concrete incoming operation, has stronger payoff on success, and real downside on failure.

- [ ] **Step 3: Write failing tests for binding versus force binding**

Force binding must require stronger leverage and produce a distinct stronger payoff under documented constraints.

- [ ] **Step 4: Write failing tests for control levels**

`hindered`, `bound`, and `locked` must have explicit mechanical restrictions and counterplay.

- [ ] **Step 5: Implement validation and projection changes**

Keep operation lanes strict; do not let pressure/guard/maneuver act as hidden anti-control or control creation.

- [ ] **Step 6: Update UI help and combat log**

Use Russian terminology first and show clear action meanings.

- [ ] **Step 7: Update GM-facing docs and examples**

Docs must match validation exactly.

- [ ] **Step 8: Run targeted tests and review**

```powershell
dotnet test BookOfEternityClient.Tests\BookOfEternityClient.Tests.csproj --no-restore --filter "AfterlifeSpiritualConflictValidationTests|ExplorerModeCommandTests|AfterlifeDocumentationCoverageTests|ExampleDocumentationValidationTests"
```

- [ ] **Step 9: Commit, review, merge, close #457**

---

## Phase 2: Close #458 - OД And Recovery

**Goal:** Add afterlife action economy so combat cannot collapse into spamming one strongest operation.

**Files:**

- Modify: `AfterlifeSpiritualConflictState.cs`
- Modify: `ValidationService.AfterlifeSpiritualConflict.cs`
- Modify: `ExplorerMode.Afterlife.SpiritualConflict.cs`
- Modify: docs/examples/tests from Phase 1.

- [ ] **Step 1: Write failing tests for `actionEconomy` shape**

Active conflicts carry current/max OД for player and opposition.

- [ ] **Step 2: Write failing tests for `actionCostAudit`**

Each exchange must spend/restore exact OД according to operation cost and art tier.

- [ ] **Step 3: Write failing tests for `recover_spiritual_power`**

Recovery is strong against passive/guard/counter timing and weak against pressure, maneuver, binding, force binding, and force incarnation.

- [ ] **Step 4: Implement cost formula**

Use the issue table unless tests reveal an obvious exploit; document final values.

- [ ] **Step 5: Update help/log/docs**

Explain OД, cost, recovery, and terminal actions at 0 OД.

- [ ] **Step 6: Run targeted tests, review, commit, merge, close #458**

---

## Phase 3: Close #459 - `Средоточие Души`

**Goal:** Add permanent player progression that increases max OД.

**Files:**

- Modify: soul afterlife combat profile handling.
- Modify: spiritual arts UI.
- Modify: conflict start/projection logic.
- Test: ExplorerMode upgrades, validation, docs.

- [ ] **Step 1: Write failing tests for `spiritFocusTier` default and bounds**

- [ ] **Step 2: Write failing tests for max OД by tier**

- [ ] **Step 3: Write failing tests for local upgrade cost/blockers**

- [ ] **Step 4: Implement tier table and upgrade UI**

- [ ] **Step 5: Initialize combat OД from `Средоточие Души`**

- [ ] **Step 6: Update docs and run targeted tests**

- [ ] **Step 7: Commit, review, merge, close #459**

---

## Phase 4: Close #460 - Unified Entity Profiles

**Goal:** Add `game_state/meta/afterlife_entity_profiles.json` as the shared profile surface for significant afterlife entities.

**Files:**

- Create: `AfterlifeEntityProfileState.cs`
- Create: `ValidationService.AfterlifeEntityProfiles.cs`
- Modify: `FileMapping.cs`
- Modify: `StateDistributor.cs`
- Modify: `CanonicalStateNormalizer.cs` and/or a new partial.
- Modify: snapshot/baseline enumeration if this becomes a tracked canonical file.
- Create tests for state, validation, normalizer, routing, UI.

- [ ] **Step 1: Write failing tests for default root and legacy absence**

Existing saves without the file must continue to validate.

- [ ] **Step 2: Write failing tests for canonical profile schema**

Profile includes actor ref, visible display fields, currencies, enlightenment/radiance, spiritual arts, special arts, soul dissipation tier, progression strategy stub, custom states stub, and ledger.

- [ ] **Step 3: Write failing tests for player visibility**

ExplorerMode must show the full profile immediately.

- [ ] **Step 4: Implement state helper and validator**

Keep schema strict enough for mechanics, but tolerant for legacy absence.

- [ ] **Step 5: Add routing and normalization**

Avoid creating missing-baseline failures for absent optional files.

- [ ] **Step 6: Update docs/examples/tests**

- [ ] **Step 7: Commit, review, merge, close #460**

---

## Phase 5: Close #461 - Afterlife Entity Custom States

**Goal:** Add removable custom states for afterlife entities.

**Files:**

- Modify: `AfterlifeEntityProfileState.cs`
- Modify: `ValidationService.AfterlifeEntityProfiles.cs`
- Modify: profile UI.
- Modify: FileMapping/StateDistributor if adding a command surface.
- Test: profile validation, command normalization, UI, docs.

- [ ] **Step 1: Write failing tests for add/update/remove**

- [ ] **Step 2: Write failing tests that malformed states block validation**

- [ ] **Step 3: Implement state changes and removal semantics**

- [ ] **Step 4: Show custom states in full profile UI**

- [ ] **Step 5: Document GM/mod creation rules and removal**

- [ ] **Step 6: Run targeted tests, commit, review, merge, close #461**

---

## Phase 6: Close #462 - Automatic Entity Progression

**Goal:** Let entities earn/spend resources on existing afterlife/Shining cycles by deterministic strategy.

**Files:**

- Modify: `AfterlifeEntityProfileState.cs`
- Modify: validation profile partial.
- Modify: progression scheduler integration files after locating exact cycle entrypoints.
- Modify: docs/examples/tests.

- [ ] **Step 1: Locate existing afterlife/Shining cycle hooks**

Do not introduce a new timer unless existing cycles cannot represent the requirement.

- [ ] **Step 2: Write failing tests for cycle income**

Chaos Sea grants Ink Feathers; Shining grants Ink Feathers and Light Sparks.

- [ ] **Step 3: Write failing tests for deterministic strategy spending**

No GM override means client applies profile strategy.

- [ ] **Step 4: Write failing tests for GM strategy update and forced override**

Override must be audited and cannot silently mutate resources.

- [ ] **Step 5: Implement progression ledger**

Record cycle id, income, spend, upgrades, and reason.

- [ ] **Step 6: Update profile UI and docs**

- [ ] **Step 7: Run targeted tests, commit, review, merge, close #462**

---

## Phase 7: Close #463 - Special Spiritual Arts

**Goal:** Add GM-authored special arts based on standard operations, plus learning by the player.

**Files:**

- Modify: profile state/validation.
- Modify: conflict validation to require special art audit when used.
- Modify: spiritual arts UI.
- Modify: docs/examples/tests.

- [ ] **Step 1: Write failing tests for special art definition schema**

Must include Russian display name, base operation, owner, cost multiplier, effects, and learning metadata.

- [ ] **Step 2: Write failing tests for use without required note**

If an art with GM-authored effect is used, require an effect note/audit; do not judge note quality.

- [ ] **Step 3: Write failing tests for learning receipt**

GM-recognized learning adds the art to the player's upgradeable list.

- [ ] **Step 4: Implement validation and UI**

- [ ] **Step 5: Update docs/examples**

- [ ] **Step 6: Run targeted tests, commit, review, merge, close #463**

---

## Phase 8: Close #465 - Advantage And Disadvantage

**Goal:** Add Mortal-style advantage/disadvantage to afterlife dice without making randomness dominant.

**Files:**

- Modify: `ValidationService.AfterlifeSpiritualConflict.cs`
- Modify: combat log UI.
- Modify: docs/examples/tests.

- [ ] **Step 1: Write failing tests for multiple dice per side**

Each die consumes a unique pre-generated index.

- [ ] **Step 2: Write failing tests for best/worst selection**

Advantage chooses best; disadvantage chooses worst; cancellation follows Mortal rules.

- [ ] **Step 3: Write failing tests for selected-die criticals**

Natural 20/1 applies only to selected die.

- [ ] **Step 4: Implement dice audit extension**

- [ ] **Step 5: Update combat log and docs**

- [ ] **Step 6: Run targeted tests, commit, review, merge, close #465**

---

## Phase 9: Close #466 - Difficulty And Rewards

**Goal:** Make selected difficulty affect afterlife combat and reward multipliers without dice dominance.

**Files:**

- Modify: difficulty/settings reader after locating authoritative setting.
- Modify: conflict dice/reward validation.
- Modify: combat log/reward UI.
- Modify: docs/examples/tests.

- [ ] **Step 1: Locate authoritative difficulty setting**

Search game settings, save profile, and turn request context before designing the audit.

- [ ] **Step 2: Write failing tests for difficulty audit**

Validator rejects arbitrary modifiers/multipliers.

- [ ] **Step 3: Write failing tests for reward multiplier**

High difficulty increases Ink Feather/Light Spark rewards.

- [ ] **Step 4: Write balance tests or scenario tests**

Ensure strategy, tiers, OД, and matchup remain more important than a single die result.

- [ ] **Step 5: Implement final difficulty table**

Document the chosen values.

- [ ] **Step 6: Update docs/UI and run targeted tests**

- [ ] **Step 7: Commit, review, merge, close #466**

---

## Phase 10: Close #464 - Soul Dissipation

**Goal:** Add `Развеивание души` as a symmetric terminal post-victory action with motivation-gated GM resolution.

**Files:**

- Modify: profile state/validation.
- Modify: conflict resolve validation.
- Modify: terminal game-over handling after locating existing terminal/game over surfaces.
- Modify: UI danger display.
- Modify: docs/examples/tests.

- [ ] **Step 1: Write failing tests for dissipation tier and resistance coefficient**

Rule: dissipation tier must be greater than target resistance coefficient derived from enlightenment/radiance progression.

- [ ] **Step 2: Write failing tests for victory proof**

No dissipation without valid loss/surrender/concession/defeat proof.

- [ ] **Step 3: Write failing tests for motivation-gated GM resolution**

Enemy capability means risk, not automatic execution.

- [ ] **Step 4: Write failing tests for player game over**

Only validated dissipation of the player creates terminal state and blocks further play.

- [ ] **Step 5: Implement eligibility matrix and terminal flow**

Keep player and NPC rules symmetric.

- [ ] **Step 6: Add red danger display for capable enemies**

- [ ] **Step 7: Update docs/examples and run targeted tests**

- [ ] **Step 8: Commit, review, merge, close #464**

---

## Phase 11: Close #467 - Final Documentation And Tests

**Goal:** Verify every new afterlife progression/combat contract is documented, exampled, and covered.

**Files:**

- Modify all afterlife docs and coverage tests as needed.
- Run full test suite.

- [ ] **Step 1: Audit GM-facing docs against FileMapping and validators**

- [ ] **Step 2: Add missing coverage assertions per document**

Avoid tests that pass just because a term exists somewhere in combined docs.

- [ ] **Step 3: Validate examples**

Every new command surface and payload shape must have at least one valid example.

- [ ] **Step 4: Run documentation-sensitive tests**

```powershell
dotnet test BookOfEternityClient.Tests\BookOfEternityClient.Tests.csproj --no-restore --filter "ExampleDocumentationValidationTests|AfterlifeDocumentationCoverageTests"
```

- [ ] **Step 5: Run broader targeted afterlife tests**

```powershell
dotnet test BookOfEternityClient.Tests\BookOfEternityClient.Tests.csproj --no-restore --filter "Afterlife|ExplorerModeCommandTests"
```

- [ ] **Step 6: Run full suite**

```powershell
dotnet test BookOfEternityClient.Tests\BookOfEternityClient.Tests.csproj --no-restore
```

- [ ] **Step 7: Commit, review, merge, close #467**

---

## Final Closure Checklist

- [ ] All issues #457-#467 are closed.
- [ ] No open PR remains for the roadmap.
- [ ] `main` is up to date with origin.
- [ ] Full test suite passes.
- [ ] Documentation-sensitive afterlife tests pass.
- [ ] Player-facing help uses Russian terms consistently.
- [ ] GM-facing docs mention every new response surface, canonical state surface, audit object, and lifecycle blocker.
- [ ] No Mortal World combat/NPC/faction files are used as substitutes for afterlife combat/progression mechanics.
