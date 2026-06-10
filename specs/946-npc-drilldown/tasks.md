# Tasks: NPC detail-section drill-down menus

**Source issue**: #946 https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/946

**Spec**: `specs/946-npc-drilldown/spec.md`

**Plan**: `specs/946-npc-drilldown/plan.md`

## TDD / Implementation Tasks

- [X] T001 Record focused baseline for NPC command/browser/console/source-guard slice before implementation.
- [X] T002 Capture RED for rich selected-NPC section availability: an NPC with journals/thoughts, one personal quest with objectives/rewards/failure consequences, and one activity currently lacks separate drill-down section affordances.
- [X] T003 Capture RED for focused section rendering: journal/thoughts, personal quest detail, and activity detail can be rendered independently from the full overview.
- [X] T004 Capture RED for browser parity: `/npc` browser command-result output lacks equivalent section-level affordances/detail data for the same rich NPC or leaks raw/all-in-one-only output.
- [X] T005 Implement a shared read-only NPC detail-section projection with label, count/status hint, availability, and focused player-facing content for populated sections.
- [X] T006 Wire console selected-NPC flow to preserve the existing overview and add second-level section navigation/back behavior.
- [X] T007 Wire browser `/npc` command result to expose equivalent section summaries/detail blocks/action metadata through C# authority, or create/link a dedicated browser follow-up if full interactivity exceeds #946.
- [X] T008 Preserve #928 journal-only fallback behavior and ensure read-only sections do not enable/ imply `/npc_talk`, `/npc_trade`, or other mutating flows without strict existing authority.
- [X] T009 Update player-facing docs/examples only if command capability documentation changes; otherwise record docs/prompts impact as not required because this is client-owned read-only inspection.
- [X] T010 Run focused RED/GREEN tests and record exact pass/fail counts.
- [X] T011 Run affected C# test slice and build gates.
- [X] T012 Run frontend verification if React/frontend files changed.
- [X] T013 Run Spec Kit prerequisite check, `git diff --check`, and added-line static scan.
- [ ] T014 Independent review: verify #946 acceptance, console/browser parity, no raw/debug default output, no mutation authority leak, and no scope creep into #947/#948/#949.
- [ ] T015 Hermes-owned lifecycle: PR, squash merge, issue evidence comment, issue close, worktree cleanup.

## Notes

- Spec Kit applies because #946 changes a player-facing `/npc` UX flow and crosses console/browser read-only parity.
- This issue is a focused NPC detail-section task, not the broader mortal read-only drill-down audit (#948).
- Browser parity may be satisfied by typed C# command-result/detail metadata; React must remain presentation-only.

## Implementation Evidence

- Baseline: `powershell.exe -NoProfile -ExecutionPolicy Bypass -File .specify/scripts/powershell/check-prerequisites.ps1 -Json -RequireTasks -IncludeTasks` returned `FEATURE_DIR=E:\\Games\\worktrees\\boe-946-npc-drilldown\\specs\\946-npc-drilldown` and `AVAILABLE_DOCS=["contracts/","tasks.md"]`; focused slice `dotnet test BookOfEternityClient.Tests/BookOfEternityClient.Tests.csproj -p:IsTestProject=true --filter "FullyQualifiedName~Npc|FullyQualifiedName~ExplorerWebCommandServiceTests|FullyQualifiedName~GameInterfaceTests|FullyQualifiedName~ExplorerModeSourceGuardTests" --logger "console;verbosity=minimal"` passed 294/294 before implementation.
- RED tests: `dotnet test BookOfEternityClient.Tests/BookOfEternityClient.Tests.csproj -p:IsTestProject=true --filter "FullyQualifiedName~ExecuteAsync_NpcRichDetails_ExposesPlayerFacingDrilldownSections|FullyQualifiedName~TryProcessCommand_Npcs_RichNpcShowsDetailSectionMenu|FullyQualifiedName~ExecuteAsync_NpcBundle_HidesPathsAndSkipsMissingFiles" --logger "console;verbosity=minimal"` failed 3/3 before implementation: `/npc` still emitted raw JSON, browser lacked `Разделы НПС`, and console selected-NPC flow only showed the NPC list/back prompt.
- GREEN tests: the same focused command passed 3/3 after implementation. Affected slice `dotnet test BookOfEternityClient.Tests/BookOfEternityClient.Tests.csproj -p:IsTestProject=true --filter "FullyQualifiedName~Npc|FullyQualifiedName~ExplorerWebCommandServiceTests|FullyQualifiedName~GameInterfaceTests|FullyQualifiedName~ExplorerModeSourceGuardTests" --logger "console;verbosity=minimal"` passed 296/296. Builds `dotnet build BookOfEternityClient/BookOfEternityClient.csproj --no-restore` and `dotnet build BookOfEternityClient.Tests/BookOfEternityClient.Tests.csproj --no-restore -p:IsTestProject=true` both completed with 0 warnings and 0 errors.
- Hygiene: `powershell.exe -NoProfile -ExecutionPolicy Bypass -File .specify/scripts/powershell/check-prerequisites.ps1 -Json -RequireTasks -IncludeTasks` returned `FEATURE_DIR=E:\\Games\\worktrees\\boe-946-npc-drilldown\\specs\\946-npc-drilldown` and `AVAILABLE_DOCS=["contracts/","tasks.md"]`; `git diff --check origin/main...HEAD` passed; added-line static scan outside `specs/**` reported no hits.
- Docs/prompts impact: no GM prompt, example, afterlife, pending/control, or React/frontend update was required. The change is client-owned read-only NPC inspection over existing Mortal World NPC state, and browser parity is satisfied through C# `UiBlock` command-result data.
- Frontend verification: not run because no `BookOfEternityClient.WebFrontend` or React/frontend files changed.
- Review: pending.
