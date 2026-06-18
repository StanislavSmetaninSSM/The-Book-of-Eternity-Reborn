# Feature Specification: Browser Lifecycle Panel Player Copy

**Feature Branch**: `work/1088-browser-lifecycle-copy`
**Created**: 2026-06-18
**Status**: Draft
**Input**: GitHub issue #1088 — https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/1088

## Source Issues & Scope *(mandatory)*

- **Source GitHub issue(s)**: #1088 — https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/1088
- **Issue type**: bug / player-facing Browser Client UX copy
- **Spec Kit justification**: The repository instructions require Spec Kit for player-facing Browser Client UX changes. The implementation is intentionally small, but the acceptance criteria define the default lifecycle panel copy boundary that future browser work must preserve.
- **Contract scope**: player-facing browser C# DTO copy and focused browser API/source-guard tests; no GM-facing prompts, runtime state contract, validation contract, save format, console behavior, or afterlife pending/control contract changes.
- **Out of scope**: Browser turn submission/write mechanics, prompt-session behavior, advanced diagnostics, command-result DTO sanitization outside the idle lifecycle panel, visual redesign, and slash-command handling.

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Default lifecycle guidance is in-world and grammatical (Priority: P1)

A player opens the Browser Client scene while the game is ready for the next turn. The "Жизненный цикл хода" panel explains the next action in Russian game-facing language instead of describing browser implementation details or a future technical integration step.

**Why this priority**: This is the defect reported in #1088. It appears before the player executes any slash command, so it is a first-screen player experience problem.

**Independent Test**: Build the browser game-screen turn-state DTO for the idle lifecycle and assert the panel message and recommended action description use the accepted player-facing wording and exclude technical terms.

**Acceptance Scenarios**:

1. **Given** the browser lifecycle state is idle/ready, **When** the game-screen DTO is built, **Then** the turn-state message is exactly `Опишите следующее действие персонажа в прозе. После подтверждения ход будет подготовлен для ГМ.`
2. **Given** the ready lifecycle action is shown, **When** the recommended action description is rendered, **Then** it tells the player to confirm when ready to pass the turn to the GM.
3. **Given** the default player panel is shown, **When** the message and action description are inspected, **Then** they do not contain `Браузерный`, `DTO`, `pending`, `protocol`, `interactive/write`, `API`, or implementation phrasing around being `подключен`.

### Edge Cases

- The readiness state must still expose `CanStartBrowserWrite = true`; this issue changes copy only, not lifecycle state transitions.
- Advanced diagnostics may continue to show technical validation details behind explicit advanced mode; this issue only constrains default lifecycle panel copy.
- No GM-authored behavior changes are introduced, so no GM prompts, examples, or afterlife contract matrix updates are required.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: The idle/ready browser turn-state message MUST use the accepted player-facing Russian sentence: `Опишите следующее действие персонажа в прозе. После подтверждения ход будет подготовлен для ГМ.`
- **FR-002**: The idle/ready recommended action description MUST be grammatical Russian and refer to passing the turn to the GM, not to connecting a browser write implementation.
- **FR-003**: The default lifecycle message and action description MUST NOT expose technical terms or implementation placeholders listed in issue #1088.
- **FR-004**: The change MUST preserve existing lifecycle state IDs, phase IDs, severity, readiness flags, and recommended action IDs.
- **FR-005**: Regression coverage MUST fail on the pre-fix technical copy and pass after the copy is corrected.

### Key Entities

- **BrowserGameScreenTurnStateDto**: C# DTO consumed by the Browser Client scene lifecycle panel. Only default ready copy fields change.
- **BrowserGameScreenTurnActionDto**: C# DTO describing the default ready action. Only the player-facing description changes.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: Focused `BrowserApiContractTests.GameScreenIdleTurnState_UsesPlayerFacingRussianCopy` passes with non-zero test discovery.
- **SC-002**: Browser-focused contract/source guard tests pass for `BrowserApiContractTests`.
- **SC-003**: Frontend verification passes, proving TypeScript fixtures/components still compile against the DTO contract.
- **SC-004**: Static added-line scan finds no hardcoded secrets, shell execution, eval/exec, unsafe deserialization, or SQL string formatting in changed code.

## Verification Plan *(mandatory)*

- **C# verification**: `dotnet test BookOfEternityClient.Tests/BookOfEternityClient.Tests.csproj -p:IsTestProject=true --filter "FullyQualifiedName~BrowserApiContractTests.GameScreenIdleTurnState_UsesPlayerFacingRussianCopy" --logger "console;verbosity=minimal"`; then `dotnet test BookOfEternityClient.Tests/BookOfEternityClient.Tests.csproj -p:IsTestProject=true --filter "FullyQualifiedName~BrowserApiContractTests" --logger "console;verbosity=minimal"`.
- **Documentation/contract verification**: `git diff --check origin/main...HEAD`; no GM/afterlife documentation tests required because runtime/player command contracts are unchanged.
- **Frontend verification**: `npm run verify --prefix BookOfEternityClient.WebFrontend`.
- **Manual/player-facing verification**: Inspect the DTO/test output or built Browser Client lifecycle panel copy; closure report must state that the default lifecycle text is player-facing and contains no issue-listed technical terms.

## Assumptions

- The issue's example sentence is the accepted copy for the idle turn-state message.
- `ГМ` is the intended player-facing term for the game master in this UI surface.
- The reported defect is limited to the default scene lifecycle/turn panel, not command-result output already covered by #1087.
