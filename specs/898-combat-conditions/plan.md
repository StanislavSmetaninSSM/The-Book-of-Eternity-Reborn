# Implementation Plan: Afterlife combatConditions layer (#898)

**Source issue:** https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/898

## Architecture

Add `combatConditions[]` as a GM-authored afterlife spiritual-combat contract, not as a generic RPG buff engine. The C# client remains the validation, canonical-state, command-output, and local web authority; the GM prompts/docs/examples teach the authoring contract. Conditions map to existing legal axes (`rollMode`, position, control, strain, tempo, counter payoff, action-cost audit) and are displayed only when visible to the player.

## Files and Responsibilities

Codex must inspect current names before editing, then keep changes near existing afterlife spiritual-conflict patterns.

Likely files to modify:

- `BookOfEternityClient/Services/Validation/ValidationService.AfterlifeSpiritualConflict.cs`: validate `combatConditions[]` shape, kind, lifecycle, legal axes/payoffs, and condition-backed roll/audit references.
- `BookOfEternityClient/Services/Validation/ValidationService.AfterlifeEntityProfiles.cs`: validate profile/recent-conflict surfaces if `combatConditions[]` appears there.
- `BookOfEternityClient/UI/ExplorerAfterlifeCombatCommandResultBuilder.cs`: render visible active conditions in player-facing afterlife combat output.
- `BookOfEternityClient/UI/ExplorerMode/ExplorerMode.Afterlife.SpiritualConflict.cs` and/or related afterlife profile/log builders: surface visible condition summaries if these commands already show current/recent conflict state.
- `BookOfEternityClient.Tests/*Afterlife*Tests.cs` and/or new focused tests: RED/GREEN validation and UI guards.
- `CLI_Agent_Daemon_Specification.md`: require the GM to create, consume, expire, and audit `combatConditions[]`.
- `OtherGuides/Afterlife_Contract_Matrix.md`: add the contract surface and lifecycle/audit requirements.
- `OtherGuides/Afterlife_Combat_Terminology_Glossary.md`: define condition kinds and legal mechanical axes.
- `Examples/E_CLI_Afterlife_Turns.txt`: add worked examples for mark/ward/burden/opening/vow and condition consumption/expiration.
- `Examples/example_validation_manifest.json` if examples are referenced there.
- `BookOfEternityClient.Tests/AfterlifeDocumentationCoverageTests.cs` and `BookOfEternityClient.Tests/ExampleDocumentationValidationTests.cs`: documentation/source guard updates.
- `BookOfEternityClient/game_master_daemon.ps1` and API/task guide duplicates only if they contain the active GM prompt text for afterlife spiritual conflict resolution.
- `specs/898-combat-conditions/*`: keep evidence synchronized with implementation.

## Method

- Use strict TDD: write failing tests/source guards before production/docs implementation for each behavior slice.
- Use systematic debugging for unexpected build/test failures: reproduce, trace root cause, compare working patterns, then fix.
- Keep the implementation focused on #898. Do not implement #897 `specialArts[].combatEffect` beyond neutral wording that says it is a future source for conditions/payoffs.
- Preserve backward compatibility for old saves/profiles without `combatConditions`.
- Update GM-facing docs/examples/tests in the same PR because #898 changes an afterlife contract.

## Task Decomposition

1. **Spec/checklist setup**: confirm this feature dir is discoverable from branch `codex/898-combat-conditions`, run Spec Kit prerequisite check, and keep `spec.md`, `plan.md`, `tasks.md`, and `contracts/combat-conditions.md` synchronized.
2. **Validation RED/GREEN**: add focused tests for absent/valid/invalid `combatConditions[]`, supported kinds, required fields, legal mechanical axes, lifecycle/expiration, condition-backed roll/audit references, and spoiler-safe visibility.
3. **Runtime/display RED/GREEN**: add player-facing output tests for visible active conditions and hidden/GM-only suppression; implement minimal shared rendering.
4. **Docs/examples RED/GREEN**: add GM prompt, contract matrix, glossary, worked examples, manifest/docs coverage guards, and any daemon prompt updates required by the docs guardrail.
5. **Integration verification**: run focused afterlife validation/docs/UI tests, docs coverage tests, build, `git diff --check`, static scan, and update `tasks.md` with exact evidence.
6. **Independent review and PR**: obtain independent review, fix blocking findings, then Hermes owns PR/merge/issue closure.

## Verification Plan

Run and record exact counts:

1. RED tests/guards introduced for validation and display; each must fail for the expected missing-contract reason before implementation.
2. Focused afterlife combat-condition validation/UI filter selected by actual test names.
3. `dotnet test BookOfEternityClient.Tests/BookOfEternityClient.Tests.csproj --filter "AfterlifeDocumentationCoverageTests|ExampleDocumentationValidationTests" --logger "console;verbosity=minimal"`
4. `dotnet build BookOfEternityClient.Tests/BookOfEternityClient.Tests.csproj --no-restore -p:IsTestProject=true --verbosity minimal`
5. `git diff --check origin/main...HEAD`
6. Added-line static security scan excluding `specs/**`.

## Spec Kit Applicability

#898 is a multi-file afterlife contract, validation, UI, GM prompt, documentation, and examples change. It requires Spec Kit under the project constitution and AGENTS.md guardrails.
