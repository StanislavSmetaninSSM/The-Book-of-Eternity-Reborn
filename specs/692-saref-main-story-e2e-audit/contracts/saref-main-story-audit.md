# Saref Main Story E2E Audit Contract

Source issue: #692 — https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/692

This file defines the audit boundary for the Saref / `Крылья над Бездной` hidden main-story closure unit. It is not permission to invent a new runtime contract. It names the existing surfaces that must be verified, documented, or split into follow-up issues.

## Existing Surfaces to Audit

- Canonical state: `BookOfEternityClient/Services/SarefMainStoryState.cs`.
- Validation: `BookOfEternityClient/Services/Validation/ValidationService.SarefMainStory.cs`.
- Normalizer: `BookOfEternityClient/Services/CanonicalStateNormalizer/CanonicalStateNormalizer.SarefMainStory.cs`.
- Console player commands: `ExplorerMode.Afterlife.SarefStory.cs` and `ExplorerMode.Afterlife.MemoryScene.cs`.
- Browser write parity when implicated: `BookOfEternityClient/WebUi/BrowserSarefStoryWriteService.cs`.
- GM-facing docs and examples: Saref guides, `OtherGuides/Afterlife_Contract_Matrix.md`, `Examples/E_CLI_Afterlife_Turns.txt`, and `Examples/example_validation_manifest.json`.
- Documentation/source guards: `AfterlifeDocumentationCoverageTests` and `ExampleDocumentationValidationTests`.

## Required Audit Axes

For each stage or branch, the implementation must identify:

1. Required canonical fields.
2. Player-visible command behavior and spoiler boundary.
3. GM-authored response surface and allowed proof fields.
4. Pending/control files that may exist.
5. Validation issue(s) that catch illegal states.
6. Normalizer behavior after accepted turns.
7. Documentation/example/source-guard evidence, or a linked follow-up issue.

## Stages and Branches

- `unknown`
- `shadow`
- `name_revealed`
- `wings_revealed`
- `infiltration_active`
- `confrontation_available`
- `completed`
- `oathbound_to_saref` / deal post-story agenda
- Defeat outcomes: forced oath, exile, memory suppression, soul dissipation, pyrrhic escape
- Oath-break routes: Seret, Lucian, Ilarion, Veyra, deep story evidence

## Change Rules

- Small validation, normalizer, command, docs, or example gaps found directly by #692 may be fixed in this PR with RED/GREEN evidence.
- Broad new mechanics, missing true interactive E2E harness work, or separate browser parity work must become follow-up issues unless this spec is updated before implementation continues.
- Any runtime contract or GM authoring change must update docs/examples/manifests/source guards in the same PR.
- Player-facing output must stay in-world and spoiler-safe; raw JSON, file paths, DTO/API names, and hidden stage identifiers belong only in explicit advanced/debug contexts.

## Completion Evidence

The PR and final #692 issue comment must include:

- Stage matrix/audit artifact path.
- Tests added or updated.
- Docs/examples/manifests updated.
- Focused verification commands with pass/fail/skip counts.
- Independent review result.
- Follow-up issues for any unresolved gaps.
- `GitHub Actions: not used/not required` unless Stanislav explicitly requests CI.
