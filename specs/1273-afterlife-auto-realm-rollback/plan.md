# Implementation Plan: Afterlife Forbidden Realm Auto-Rollback

**Branch**: `1266-universal-command-audit` | **Date**: 2026-06-25 | **Spec**: `specs/1273-afterlife-auto-realm-rollback/spec.md`

**Source Issue(s)**:

- #1273 https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/1273
- #1249 https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/1249

## Summary

Add a client-side auto-rollback pass before GM repair. When accepted-turn validation detects afterlife realm segregation violations, the engine restores/deletes forbidden Mortal World files using the validated pending snapshot, writes an audit report, and reruns validation. The GM receives only remaining meaningful repair items.

## Technical Context

**Language/Version**: C# / .NET 8, PowerShell GM daemon prompts, Markdown/TXT examples.

**Primary Dependencies**: `FileSystemManager`, pending turn snapshot manifest, `ValidationService`, `GameEngine` repair loop.

**Storage**: JSON files under `game_session/game_state/control`, pending snapshot files, game state files.

**Testing**: xUnit focused tests, documentation/source guard tests, live Codex GM bridge test.

## Design

- Add an isolated auto-rollback component around `FileSystemManager` and pending snapshot manifest.
- Keep `ValidationService.ValidateGameStateAsync()` strict and non-healing.
- In `GameEngine.ValidateCurrentGameStateOrShowErrorsAsync`, after collecting validation errors and before `WaitForContractRepairAsync`, run auto-rollback when errors include `realm_segregation_violation`.
- If rollback changes anything, write `validation_auto_rollback_report.json`, refresh runtime state, and restart the validation loop without incrementing GM repair more than necessary.
- Do not auto-rollback when snapshot authority is missing or unsafe.

## Documentation Impact

- Update `AGENTS.md` general prompt/docs guardrail for all worlds.
- Update afterlife docs/examples/daemon prompts to say wrong-realm mutations are forbidden and client may auto-rollback them before GM repair.
- Keep afterlife-specific guardrail as a stricter checklist for Chaos Sea/Shining Abode surfaces.

## Verification Commands

- `dotnet test BookOfEternityClient.Tests\BookOfEternityClient.Tests.csproj --no-restore --filter "AfterlifeRealmAutoRollbackTests"`
- `dotnet test BookOfEternityClient.Tests\BookOfEternityClient.Tests.csproj --no-restore --filter "ExampleDocumentationValidationTests|AfterlifeDocumentationCoverageTests"`
- `git diff --check`
- Repeat #1249 live Chaos Sea bridge test.

## Constitution Check

- **GitHub traceability**: Pass. Source issue #1273 exists.
- **Spec Kit fit**: Pass. The issue changes validation, repair lifecycle, afterlife contract docs, and GM prompts.
- **Player-facing integrity**: Pass. The change prevents player-facing repair deadlock; no debug output should surface to players.
- **Contract/state authority**: Pass. Pending snapshot remains the source of truth for rollback.
- **Test-first path**: Pass. Runtime behavior starts with failing focused tests.
- **Verification evidence**: Required before final report.

## Risks

- Auto-rollback must not hide broader GM prompt failures; the report preserves accountability.
- Auto-rollback must not delete legitimate cross-realm exception files.
- Snapshot authority must remain validated; unsafe or missing snapshots must not trigger blind restoration.
