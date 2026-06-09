# Feature Specification: NPC fixture fallback for /npc

**Feature Branch**: `work/928-npc-fixture`

**Created**: 2026-06-10

**Status**: Draft for implementation

**Input**: GitHub issue #928 — make the checked-in `FileSystemExample/game_session` useful for NPC command smoke checks without restoring a stale pending-turn snapshot.

## Source Issues & Scope *(mandatory)*

- **Source GitHub issue(s)**: #928 https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/928
- **Parent / related issue(s)**: #927 test `game_session` audit/enrichment task.
- **Issue type**: test fixture / console-browser read-only command parity bugfix.
- **Spec Kit justification**: This issue touches a player-facing `/npc` command surface shared by console/browser and interacts with validation/pending-turn authority boundaries. It needs a durable source of truth so the fixture does not reintroduce stale `pending_turn_snapshot` state.
- **Contract scope**: read-only player-facing `/npc` command output and repository fixture data. No new GM-authored runtime contract, pending/control file, local-write flow, or browser gameplay logic is introduced.
- **Out of scope**: mutating `/npc_talk` or `/npc_trade` availability when strict `npc_core.json` is absent; signed pending-turn snapshot generation; changing accepted-turn NPC command contracts; afterlife/Chaos Sea/Shining Abode surfaces.

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Test fixture validates without stale pending turn (Priority: P1)

A repository worker can use `FileSystemExample/game_session` as a quick console/browser smoke fixture. Validation passes while local/browser actions remain unblocked because the fixture does not contain a stale `pending_turn_snapshot` baseline.

**Independent Test**: A fixture validation or web command test points at `FileSystemExample/game_session`, invokes `/validate` through existing services or validates the copied sandbox, and asserts no stale pending-turn snapshot issue.

**Acceptance Scenarios**:

1. **Given** `FileSystemExample/game_session`, **When** validation runs, **Then** validation succeeds without requiring `pending_turn_snapshot`.
2. **Given** the fixture is used for local/browser actions, **When** no GM turn is pending, **Then** the client does not report a stale pending-turn snapshot blocker.

---

### User Story 2 - Browser `/npc` has meaningful data from safe fixture authority (Priority: P1)

The browser command result for `/npc` shows at least one meaningful NPC name and journal summary from the checked-in fixture even when strict `npc_core.json` is intentionally absent.

**Independent Test**: A focused `ExplorerWebCommandService` or `ExplorerMortalWorldCommandResultBuilder` test creates a session with `npc_journals.json` and no `npc_core.json`, invokes `/npc`, and asserts the result contains `Торек Молотобой` and does not contain the empty-state copy `Данные ещё не созданы` as the primary result.

**Acceptance Scenarios**:

1. **Given** `game_state/npcs/npc_journals.json` contains NPC journal entries and `npc_core.json` is absent, **When** `/npc` is invoked through the browser command pipeline, **Then** the result lists NPC journal data with player-facing Russian copy.
2. **Given** no strict NPC core exists, **When** `/npc` renders fallback data, **Then** it clearly labels the data as journal/known NPC notes and does not imply that talk/trade actions are available from incomplete authority.

---

### User Story 3 - Console NPC inspection is not empty for the same fixture (Priority: P1)

The console client can inspect the same fixture and see meaningful NPC information instead of `НПС не обнаружены` when only journal fixture data is available.

**Independent Test**: A focused source/command-result guard or console-friendly service test proves the console-visible NPC path uses the same fallback DTO/helper as browser `/npc`, or otherwise asserts that the console NPC panel can render `Торек Молотобой` from `npc_journals.json` without strict `npc_core.json`.

**Acceptance Scenarios**:

1. **Given** the same journal-only NPC fixture, **When** the console NPC inspection path is exercised, **Then** the player can see at least NPC name and latest journal event/description.
2. **Given** journal fallback data is displayed, **When** the player tries to infer actions, **Then** the copy keeps mutating actions out of scope unless strict NPC core authority exists.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: `FileSystemExample/game_session` MUST continue to pass validation without adding a stale `pending_turn_snapshot` or accepted-turn authority baseline.
- **FR-002**: `/npc` MUST render meaningful read-only fallback data from `game_state/npcs/npc_journals.json` when strict `game_state/npcs/npc_core.json` is missing or has no NPC entries.
- **FR-003**: The fallback MUST include stable NPC identity (`npcId` and/or `npcName`) and at least one player-readable journal event/description when present.
- **FR-004**: The fallback MUST be shared by browser command result and console-visible NPC inspection, or the implementation MUST add equivalent focused coverage for both paths.
- **FR-005**: The fallback MUST use player-facing Russian copy and MUST NOT expose raw API/DTO/debug framing in default player surfaces.
- **FR-006**: The fallback MUST NOT enable or imply mutating `/npc_talk`, `/npc_trade`, trade, or local-turn actions without strict `npc_core.json` authority.
- **FR-007**: No new afterlife, pending/control, GM-authored runtime, or browser React gameplay contract is introduced.

### Data Entities

- **NPC journal fixture**: `game_state/npcs/npc_journals.json`, currently containing `npcJournals[]` with `npcId`, `npcName`, and `journalEntries[]`.
- **NPC fallback summary**: Read-only player-facing projection built from NPC journal entries when `npc_core.json` is absent.
- **Strict NPC core authority**: `game_state/npcs/npc_core.json`; still required for mutating NPC social/trade flows and accepted-turn NPC updates.

## Success Criteria *(mandatory)*

- **SC-001**: Focused tests fail before the fix and pass after the fix for journal-only `/npc` fallback output.
- **SC-002**: Focused validation/fixture tests prove `FileSystemExample/game_session` remains valid and does not require `pending_turn_snapshot`.
- **SC-003**: Browser command output and console-visible NPC inspection both have evidence for meaningful NPC data.
- **SC-004**: `git diff --check`, focused C# tests, and client/test build pass before PR.

## Verification Plan *(mandatory)*

- **Baseline observed before implementation**: `dotnet test BookOfEternityClient.Tests/BookOfEternityClient.Tests.csproj -p:IsTestProject=true --filter "FullyQualifiedName~ConsoleE2ESandboxTests|FullyQualifiedName~AgentConsoleLiveSmokeTests|FullyQualifiedName~ExplorerWebCommandServiceTests|FullyQualifiedName~BrowserNpcSocialParityTests|FullyQualifiedName~ValidatorFixtureTests" --logger "console;verbosity=minimal"` — passed 238/238.
- **Focused expected gates**: add/run a focused test covering journal-only `/npc` fallback; run the same command above or a narrower updated filter named by the implementation.
- **Build**: `dotnet build BookOfEternityClient/BookOfEternityClient.csproj --no-restore` and `dotnet build BookOfEternityClient.Tests/BookOfEternityClient.Tests.csproj --no-restore -p:IsTestProject=true` when tests change.
- **Spec Kit**: `powershell.exe -NoProfile -ExecutionPolicy Bypass -File .specify/scripts/powershell/check-prerequisites.ps1 -Json -RequireTasks -IncludeTasks`.
- **Diff/static hygiene**: `git diff --check origin/main...HEAD`; added-line static scan excluding `specs/**`.
- **Frontend**: `npm run verify --prefix BookOfEternityClient.WebFrontend` only if React/frontend files change.

## Assumptions

- `npc_journals.json` is safe read-only display data and does not itself authorize social/trade mutations.
- The preferred fix is to teach `/npc` to display existing NPC journals when strict NPC core is absent, rather than restoring stale pending-turn snapshots or inventing a signed fixture generator in this slice.
- If implementation discovers validation currently ignores `FileSystemExample/game_session`, it should add the smallest focused fixture test needed for #928 rather than broadening the issue into the whole #927 audit.
