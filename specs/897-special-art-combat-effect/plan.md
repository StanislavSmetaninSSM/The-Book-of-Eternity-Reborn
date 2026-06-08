# Implementation Plan: Structured special-art combatEffect (#897)

**Branch**: `codex/897-special-art-combat-effect` | **Date**: 2026-06-08 | **Spec**: `specs/897-special-art-combat-effect/spec.md`

**Input**: Feature specification from `/specs/897-special-art-combat-effect/spec.md`

## Summary

Add a structured afterlife-only `specialArts[].combatEffect` contract so teachable/special spiritual arts carry an ordinary combat niche separate from story/Saref `effectSummary`. Validation, player-facing special-art/profile output, GM prompts/docs, worked examples, and documentation coverage must all agree on the field shape and on legal payoff axes introduced by #898.

## Technical Context

**Language/Version**: C# / .NET 8 client and tests; Markdown/JSON examples; optional TypeScript only if shared browser rendering requires it.

**Primary Dependencies**: Existing `System.Text.Json` validation patterns, Explorer mode command-result builders, afterlife entity profile and spiritual conflict services, documentation coverage tests.

**Storage**: File-backed canonical game state under `game_state/meta/afterlife_entity_profiles.json` and spiritual-conflict state; no new storage file.

**Testing**: `dotnet test` for focused validation/UI/docs tests; `dotnet build` for test project; `git diff --check`; static added-line scan excluding `specs/**`.

**Target Platform**: Local Windows repo; console and browser share C# client/application authority.

**Project Type**: Existing .NET game client with console/browser frontends.

**Performance Goals**: Validation/rendering overhead must remain negligible and file-local; no broad new runtime engine.

**Constraints**: Preserve backward compatibility for old profiles, keep player output spoiler-safe, avoid automatic mini-engine semantics, avoid modifying Mortal item `combatEffect`, and keep GM-facing docs/examples synchronized.

**Scale/Scope**: One afterlife contract slice touching validation, output, docs/examples, and tests; follow-up content rewrite #894 and coverage #896 remain separate.

**Source Issue(s)**: #897 — https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/897

**Contract Scope**: afterlife entity profile `specialArts[]`, special-art audit usage, validation, console/browser shared output, GM prompts/docs/examples, documentation coverage.

**Verification Commands**:

1. Baseline already run before implementation: `dotnet test BookOfEternityClient.Tests/BookOfEternityClient.Tests.csproj -p:IsTestProject=true --filter "FullyQualifiedName~AfterlifeSpiritualConflict|FullyQualifiedName~AfterlifeEntityProfiles|FullyQualifiedName~ExplorerAfterlife|FullyQualifiedName~AfterlifeDocumentationCoverageTests|FullyQualifiedName~ExampleDocumentationValidationTests" --logger "console;verbosity=minimal"` -> 485 passed, 0 failed, 0 skipped.
2. Focused RED/GREEN tests chosen by actual test names for special-art `combatEffect` validation and rendering.
3. `dotnet test BookOfEternityClient.Tests/BookOfEternityClient.Tests.csproj --filter "AfterlifeDocumentationCoverageTests|ExampleDocumentationValidationTests" --logger "console;verbosity=minimal"`.
4. `dotnet build BookOfEternityClient.Tests/BookOfEternityClient.Tests.csproj --no-restore -p:IsTestProject=true --verbosity minimal`.
5. `git diff --check origin/main...HEAD`.
6. Added-line static scan excluding `specs/**`.

## Constitution Check

- **GitHub traceability**: PASS — source issue #897 is linked in `spec.md`, `plan.md`, and `tasks.md`; #895/#894/#896 are related but not auto-closed by this slice.
- **Spec Kit fit**: PASS — afterlife contract, validation, GM docs/examples, and player-facing output changes require durable specs under AGENTS.md.
- **Player-facing integrity**: PASS — output must be Russian/in-world where applicable, player-safe, spoiler-safe, and free from raw debug/API terms in default surfaces.
- **Contract/state authority**: PASS — C# validation and GM docs/examples remain authoritative; docs/examples/source guards are planned with code changes.
- **Test-first path**: PASS — tasks require RED tests/source guards before validation/output/docs implementation.
- **Verification evidence**: PASS — focused afterlife tests, docs tests, build, diff check, and static scan are listed.
- **Agent orchestration**: PASS — Hermes will pass these Spec Kit paths plus Superpowers method requirements into Codex; Hermes owns final PR/merge/closure.

## Project Structure

### Documentation (this feature)

```text
specs/897-special-art-combat-effect/
├── spec.md
├── plan.md
├── tasks.md
└── contracts/
    └── special-art-combat-effect.md
```

### Source Code (repository root)

Likely files to inspect/modify; Codex must confirm actual patterns before editing:

```text
BookOfEternityClient/Services/Validation/ValidationService.AfterlifeEntityProfiles.cs
BookOfEternityClient/Services/Validation/ValidationService.AfterlifeSpiritualConflict.cs
BookOfEternityClient/UI/ExplorerMode/ExplorerMode.Afterlife.EntityProfiles.cs
BookOfEternityClient/UI/ExplorerMode/ExplorerMode.Afterlife.SpiritualConflict.cs
BookOfEternityClient/UI/ExplorerAfterlifeCombatCommandResultBuilder.cs
BookOfEternityClient/WebUi/BrowserAfterlifeWriteService.cs (inspect only unless prompt/write flows require display metadata)
BookOfEternityClient.Tests/AfterlifeEntityProfilesValidationTests.cs or nearby validation tests
BookOfEternityClient.Tests/AfterlifeSpiritualConflictValidationTests.cs
BookOfEternityClient.Tests/ExplorerMode*Afterlife*Tests.cs or nearby command-output tests
BookOfEternityClient.Tests/AfterlifeDocumentationCoverageTests.cs
BookOfEternityClient.Tests/ExampleDocumentationValidationTests.cs
CLI_Agent_Daemon_Specification.md
OtherGuides/Afterlife_Contract_Matrix.md
OtherGuides/Afterlife_Combat_Terminology_Glossary.md
Examples/E_CLI_Afterlife_Turns.txt
Examples/example_validation_manifest.json
BookOfEternityClient/game_master_daemon.ps1
```

**Structure Decision**: Keep implementation in existing afterlife validation/output/doc patterns. Do not add a new special-art execution engine. If display code needs shared helpers, prefer a focused internal helper near existing afterlife profile/spiritual art rendering.

## Complexity Tracking

No constitution violation planned. The extra Spec Kit overhead is justified by afterlife contract sensitivity and follow-up dependency chain (#894/#896).

## Architecture

`combatEffect` is contract metadata attached to `specialArts[]`, not an executable rules script. The GM remains the authority for applying it during an exchange, but the field must constrain legal application: `baseOperation` still determines the operation lane, and `combatEffect` identifies a legal ordinary-combat payoff axis and audit requirement. Validation enforces shape and obvious generic/unsupported values; docs/examples teach use; output surfaces summarize the effect so players can make upgrade decisions.

## Data Contract Decision

Initial required shape:

```json
{
  "combatEffect": {
    "summary": "Short player-facing ordinary-combat niche.",
    "trigger": "When the art's extra effect can apply.",
    "mechanicalAxis": "rollMode|conflictPosition|controlState|sideStrain|tempoAdvantage|counterPayoff|actionEconomy|actionCostAudit|combatCondition",
    "allowedPayoff": "What may change inside existing afterlife combat rules.",
    "limit": "Per-conflict/per-scene/condition/counterplay limit.",
    "auditRequirement": "What specialArtAudit.effectNote must mention when used."
  }
}
```

Implementation may add a small nested `payoff` object only if it improves consistency with #898 and is documented in `contracts/special-art-combat-effect.md`, docs, examples, validation, and tasks evidence. If Codex changes field names or requirements, update `spec.md`, `plan.md`, `tasks.md`, and contract docs in the same branch before implementation evidence is considered complete.

## Method

- Follow TDD: add focused failing tests/source guards before production/docs implementation.
- Follow systematic debugging for unexpected failures: reproduce, inspect root cause, compare existing working afterlife validation/output patterns, then fix one cause at a time.
- Preserve user changes and unrelated scratch files; work only in the ASCII worktree.
- Keep commits focused and use `[skip ci]` in commit message unless impossible.
- Do not mark tasks complete until implementation and verification evidence exists.

## Task Decomposition

1. **Spec/preflight**: confirm feature path discovery, read AGENTS/constitution/spec/plan/tasks/contract, issue #897, and #898 contract docs.
2. **Validation RED/GREEN**: add tests for complete/missing/generic/unsupported `combatEffect` on current teachable special arts; implement validation and compatibility rules.
3. **Player-facing display RED/GREEN**: add tests for `/spiritual_arts` and/or `/afterlife_profiles`/shared output showing safe combat-effect summaries and hiding raw/private/spoiler data; implement minimal rendering.
4. **GM docs/examples RED/GREEN**: update prompts/docs/examples/source guards so `combatEffect` use, legal axes, and `specialArtAudit.effectNote` are required and demonstrated.
5. **Spec Kit evidence reconciliation**: update contract/tasks evidence and prerequisite check output after implementation reality stabilizes.
6. **Final verification/review/PR**: Hermes performs independent review, fixes blockers, PR/merge/issue closure.

## Verification Plan

Run and record exact counts:

1. `.specify/scripts/powershell/check-prerequisites.ps1 -Json -RequireTasks -IncludeTasks` from the feature branch/worktree; `FEATURE_DIR` must point to `specs/897-special-art-combat-effect`.
2. Focused RED validation/output/docs tests and their expected failures before implementation.
3. Focused GREEN validation/output/docs tests after implementation.
4. Required docs/examples filter: `dotnet test BookOfEternityClient.Tests/BookOfEternityClient.Tests.csproj --filter "AfterlifeDocumentationCoverageTests|ExampleDocumentationValidationTests" --logger "console;verbosity=minimal"`.
5. Build: `dotnet build BookOfEternityClient.Tests/BookOfEternityClient.Tests.csproj --no-restore -p:IsTestProject=true --verbosity minimal`.
6. `git diff --check origin/main...HEAD`.
7. Added-line static scan excluding `specs/**`.
8. Independent review approves or all Critical/Important findings are fixed and re-reviewed before PR/merge.

## Spec Kit Applicability

#897 is an afterlife contract/validation/docs/examples/player-facing surface change and is a prerequisite for #894/#896. Spec Kit is required under AGENTS.md and the Book skill's afterlife contract guardrails.
