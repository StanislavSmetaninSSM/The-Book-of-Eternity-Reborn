# Tasks: NPC fixture fallback for /npc

**Source issue**: #928 https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/928

**Spec**: `specs/928-npc-fixture/spec.md`

**Plan**: `specs/928-npc-fixture/plan.md`

## TDD Tasks

- [X] T001 Capture RED for browser/read-only `/npc` fallback: create or update a focused C# test where `npc_journals.json` exists, `npc_core.json` is absent, `/npc` is invoked through `ExplorerMortalWorldCommandResultBuilder` or `ExplorerWebCommandService`, and the current result is the empty-state copy instead of `Торек Молотобой`.
- [X] T002 Capture RED for console-visible fallback: add the smallest practical console-friendly test/source guard proving the console NPC inspection path can render journal-only NPC data, or refactor to a shared projection helper and test that helper as the console/browser source.
- [X] T003 Implement a read-only NPC journal projection that extracts `npcId`, `npcName`, latest event/description, relationship delta, and entry count from `game_state/npcs/npc_journals.json`.
- [X] T004 Wire browser/read-only `/npc` command output to use the journal projection only when strict `npc_core.json` has no visible NPCs.
- [X] T005 Wire console NPC inspection to show equivalent journal fallback data without enabling talk/trade actions from journal-only authority.
- [X] T006 Keep or update `FileSystemExample/game_session/game_state/npcs/npc_journals.json` so it contains stable player-facing fixture data for at least `Торек Молотобой`; do not add stale `pending_turn_snapshot` files.
- [X] T007 Run focused RED/GREEN tests and record exact pass/fail counts in this file or PR evidence.
- [X] T008 Run affected command/fixture test slice: `dotnet test BookOfEternityClient.Tests/BookOfEternityClient.Tests.csproj -p:IsTestProject=true --filter "FullyQualifiedName~ConsoleE2ESandboxTests|FullyQualifiedName~AgentConsoleLiveSmokeTests|FullyQualifiedName~ExplorerWebCommandServiceTests|FullyQualifiedName~BrowserNpcSocialParityTests|FullyQualifiedName~ValidatorFixtureTests" --logger "console;verbosity=minimal"`.
- [X] T009 Run build gates for client and tests.
- [X] T010 Run Spec Kit prerequisite check and diff/static hygiene gates.
- [X] T011 Update this task list with implementation and verification evidence before PR.
- [ ] T012 Hermes-owned lifecycle: PR, squash merge, issue evidence comment, issue close, worktree cleanup.
  - Review evidence: independent Codex review returned `VERDICT: APPROVED`; no blocking findings. Non-blocking note: console coverage is source-guard based, acceptable because `ShowNPCs` uses the shared fallback renderer.

## Notes

- Spec Kit applies because the issue crosses console/browser read-only command parity and validation/pending-turn authority boundaries.
- Mutating NPC social/trade flows remain out of scope unless strict `npc_core.json` authority exists.

## Implementation Evidence

- RED focused tests: `dotnet test BookOfEternityClient.Tests/BookOfEternityClient.Tests.csproj -p:IsTestProject=true --filter "FullyQualifiedName~ExecuteAsync_NpcWithRepositoryJournalFixtureAndNoCore_ShowsKnownJournalNotes|FullyQualifiedName~ConsoleNpcInspectionSource_UsesJournalFallbackProjection" --logger "console;verbosity=minimal"` failed 2/2 as expected before implementation: browser `/npc` returned `Данные ещё не созданы`, and the console source did not reference the journal fallback projection.
- GREEN focused tests: same command passed 2/2 after implementation.
- Affected command/fixture slice: `dotnet test BookOfEternityClient.Tests/BookOfEternityClient.Tests.csproj -p:IsTestProject=true --filter "FullyQualifiedName~ConsoleE2ESandboxTests|FullyQualifiedName~AgentConsoleLiveSmokeTests|FullyQualifiedName~ExplorerWebCommandServiceTests|FullyQualifiedName~BrowserNpcSocialParityTests|FullyQualifiedName~ValidatorFixtureTests" --logger "console;verbosity=minimal"` passed 240/240.
- Build gates: `dotnet build BookOfEternityClient/BookOfEternityClient.csproj --no-restore` passed with 0 warnings/0 errors; `dotnet build BookOfEternityClient.Tests/BookOfEternityClient.Tests.csproj --no-restore -p:IsTestProject=true` passed with 0 warnings/0 errors.
- Spec Kit prerequisite check returned `FEATURE_DIR=E:\Games\worktrees\boe-928-npc-fixture\specs\928-npc-fixture` and `AVAILABLE_DOCS=["contracts/","tasks.md"]`.
- Static hygiene scan over added non-spec lines reported `NO_MATCHES`; final `git diff --check origin/main...HEAD` evidence is recorded in the handoff report after the commit diff is assembled.
