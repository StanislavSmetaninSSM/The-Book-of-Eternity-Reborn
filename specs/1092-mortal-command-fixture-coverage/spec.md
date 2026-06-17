# Feature Specification: Mortal Command Fixture Coverage

**Feature Branch**: `codex/1092-mortal-command-test-data`

**Created**: 2026-06-17

**Status**: Ready for Review

**Input**: User description: "Пройдись по всем командам смертных миров и убедись, что на каждую команду у нас есть подходящие тестовые данные. Это нужно, чтобы я мог посмотреть как что отображается. Подчеркиваю, нужно не только популярные команды, а желательно все"

## Source Issues & Scope *(mandatory)*

- **Source GitHub issue(s)**: #1092 - https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/1092
- **Issue type**: task / audit / test-data hardening
- **Spec Kit justification**: The task spans the Mortal World command catalog, ignored local game-session state, console/browser parity checks, and fixture validation. A durable spec is needed so future agents know the intended fixture coverage and verification path.
- **Contract scope**: player-facing, runtime-state fixture, validation, console, browser, docs
- **Out of scope**: Afterlife-only command data, new gameplay mechanics, redesigning command UI, and changing GM-authored contracts. If the audit finds command output that cannot be made useful through test data alone, create a follow-up issue instead of broadening this task.

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Preview Every Mortal Display Command (Priority: P1)

As the project owner, I can open the test game session and run every Mortal World display command to see a meaningful, representative screen instead of empty or malformed output.

**Why this priority**: This is the core value of the fixture: manual inspection of player-facing command output in console and browser.

**Independent Test**: Run the Mortal World read-only command set against the local test session and confirm each command returns completed, non-empty player-facing blocks.

**Acceptance Scenarios**:

1. **Given** the local test game session is in Mortal World, **When** a read-only Mortal World command is executed, **Then** the result contains representative data for that command's primary surface.
2. **Given** a command supports detail arguments, **When** a representative detail command is executed, **Then** the selected entity has detailed data rather than a "not found" or empty-state result.

---

### User Story 2 - Exercise Mortal Action Forms (Priority: P2)

As the project owner, I can open browser/console command forms for Mortal World local-turn actions and see real selectable options where the current game state should provide them.

**Why this priority**: Several player actions are not read-only screens, but they still need fixture data to make prompts and action buttons reviewable.

**Independent Test**: Execute representative local-turn Mortal World commands without submitting final GM turns and confirm the prompt/results expose meaningful options.

**Acceptance Scenarios**:

1. **Given** the test session contains equippable items, NPCs, factions, storage, transport, and feathers, **When** the corresponding local-turn commands are opened, **Then** the command result explains the action and offers usable prompt choices.

---

### User Story 3 - Keep Coverage Visible (Priority: P3)

As a future maintainer, I can see which command each fixture file is meant to exercise and detect obvious coverage regressions.

**Why this priority**: The local `game_session` is ignored by git, so the repository needs a durable matrix and verification command to prevent future drift.

**Independent Test**: Compare the command catalog to the coverage matrix and run the fixture validation command documented in quickstart.

**Acceptance Scenarios**:

1. **Given** a Mortal World command is added, **When** maintainers review the matrix, **Then** the missing fixture coverage is visible.

### Edge Cases

- The local `BookOfEternityClient/game_session` folder is ignored by git and may not exist in a clean checkout.
- Some commands are universal but materially used in Mortal World, such as `/статус`, `/душа`, `/хроника`, `/достижения`, `/кодекс`, and `/книги`; these should be included in the practical preview set when they depend on Mortal World data.
- Local-turn commands must not be auto-submitted to the GM while validating display coverage.
- If a command legitimately has no data in a scenario, the fixture should include a player-facing reason, not a raw empty/debug state.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: The fixture coverage matrix MUST list every `ExplorerCommandGroup.MortalWorld` command from the command catalog.
- **FR-002**: The fixture coverage matrix MUST identify important universal commands that are previewed in a Mortal World session.
- **FR-003**: The local test game session MUST contain representative data for each Mortal World read-only command surface.
- **FR-004**: Detail-capable commands MUST have at least one representative detail target where the current command supports detail arguments.
- **FR-005**: Local-turn Mortal World commands MUST have representative state for opening their prompts/actions without requiring GM submission.
- **FR-006**: The local test game session MUST pass validation or document any intentional warnings with a follow-up issue.
- **FR-007**: Verification steps MUST be documented so another maintainer can repeat the command coverage check.

### Key Entities *(include if feature involves data)*

- **Mortal Command Coverage Entry**: A command id, aliases, mode, representative command invocation, required fixture files, and expected visible data.
- **Local Test Game Session**: The ignored `BookOfEternityClient/game_session` folder used for manual console/browser preview.
- **Fixture Data Surface**: A state file or lore file that feeds one or more command outputs.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: 100% of `ExplorerCommandGroup.MortalWorld` command ids appear in the coverage matrix.
- **SC-002**: 100% of Mortal World read-only command ids have representative local fixture data or an explicit tracked follow-up.
- **SC-003**: At least one representative detail invocation is documented for each detail-capable Mortal World command.
- **SC-004**: The local test game session validation command completes without blocking errors, or each remaining issue has a documented reason and follow-up.

## Verification Plan *(mandatory)*

- **C# verification**: `dotnet test BookOfEternityClient.Tests\BookOfEternityClient.Tests.csproj --no-restore --filter "Mortal|BrowserMortalWorld|Inventory|Trade|Storage|ExplorerWebCommandService"`
- **Documentation/contract verification**: Review `specs/1092-mortal-command-fixture-coverage/contracts/mortal-command-fixture-matrix.md` against `BookOfEternityClient/CommandProtocol/ExplorerCommandCatalog.cs`.
- **Frontend verification**: Browser client manual command smoke check against the local host when needed; no frontend code changes are expected.
- **Manual/player-facing verification**: Run the command set from `quickstart.md` against `BookOfEternityClient/game_session`.

## Assumptions

- The user's real preview fixture is the ignored folder `E:\Games\The Book of Eternity Reborn\BookOfEternityClient\game_session`.
- Repository commits can include the coverage matrix and helper tests, but ignored local fixture changes may need to remain local unless the project later creates a tracked sample-session package.
- This task should not change afterlife contracts or GM prompt contracts unless a validator defect is found and explicitly tracked separately.
