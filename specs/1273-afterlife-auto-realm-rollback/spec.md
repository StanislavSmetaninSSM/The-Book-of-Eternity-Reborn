# Feature Specification: Afterlife Forbidden Realm Auto-Rollback

**Feature Branch**: `1266-universal-command-audit`

**Created**: 2026-06-25

**Status**: Draft

**Input**: User description: "GM should not have to manually roll back forbidden realm files during afterlife repair; the client should roll them back automatically before repair."

## Source Issues & Scope

- **Source GitHub issue(s)**:
  - #1273 https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/1273
  - Related live-test issue: #1249 https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/1249
- **Issue type**: task / validation / afterlife contract / reliability
- **Spec Kit justification**: This changes validation, repair lifecycle behavior, pending snapshot use, GM-facing repair instructions, and afterlife contract documentation.
- **Contract scope**: runtime validation, afterlife repair protocol, GM prompts/docs/examples, documentation/source guards.
- **Out of scope**: Browser UI rendering, QTE mechanics, unrelated Mortal World output polish.

## User Scenarios & Testing

### User Story 1 - Player Is Not Stuck By GM Wrong-Realm Mutations (Priority: P1)

During a Chaos Sea or Shining Abode turn, if the GM accidentally changes Mortal World files, the client restores or deletes those forbidden files from the pending snapshot before asking the GM to repair remaining errors.

**Why this priority**: Live #1249 showed Codex-GM can understand the rule but still get trapped in byte-level rollback repair.

**Independent Test**: Build a pending afterlife snapshot, mutate a Mortal World file, run auto-rollback, and assert the file matches the snapshot and a report is written.

**Acceptance Scenarios**:

1. **Given** a Chaos Sea pending turn snapshot with `game_state/factions/faction_core.json`, **When** the accepted turn mutates that file, **Then** auto-rollback restores it from snapshot before GM repair.
2. **Given** a Chaos Sea pending turn snapshot without a newly created mortal file, **When** the accepted turn creates that forbidden file, **Then** auto-rollback deletes it before GM repair.
3. **Given** auto-rollback restores/deletes forbidden files, **When** repair request is written, **Then** it no longer contains `realm_segregation_violation` for those auto-rolled-back paths.

### User Story 2 - GM Still Gets Clear Accountability (Priority: P2)

The GM and developers can see that wrong-realm mutations happened, but the GM is not asked to perform low-level filesystem restoration.

**Independent Test**: Inspect `game_state/control/validation_auto_rollback_report.json` after auto-rollback.

**Acceptance Scenarios**:

1. **Given** forbidden mutations were auto-rolled back, **When** the report is written, **Then** it lists action, path, source realm, requestId, turnNumber, and reason.
2. **Given** GM repair instructions are generated after auto-rollback, **When** the GM reads them, **Then** they describe remaining validation errors rather than manual rollback steps for already restored forbidden files.

## Edge Cases

- If the pending snapshot is missing or invalid, do not guess; keep strict validation and let existing repair diagnostics handle snapshot authority.
- If a forbidden file existed before the turn, restore it from validated snapshot content.
- If a forbidden file did not exist before the turn, delete the newly created file.
- If a path is not safe relative to `game_session`, do not mutate it.
- Do not auto-rollback allowed cross-realm exceptions or explicitly allowed Mortal guardian quest progress deltas.

## Requirements

- **FR-001**: Detect forbidden realm mutation errors from validation before creating a GM repair request.
- **FR-002**: Restore forbidden changed files from validated pending snapshot content when snapshot coverage exists.
- **FR-003**: Delete forbidden newly created files that lack pre-turn snapshot coverage.
- **FR-004**: Write `game_state/control/validation_auto_rollback_report.json` with source issue context, realm, turn metadata, actions, paths, and reasons.
- **FR-005**: Re-run validation after auto-rollback and only then decide whether GM repair is needed.
- **FR-006**: Update GM-facing afterlife documentation, examples, daemon wording, and source-guard tests as needed.
- **FR-007**: Update repository agent instructions to require prompt/docs/example impact checks for both Mortal World and afterlife when gameplay or GM-authored contracts change.

## Success Criteria

- **SC-001**: Focused auto-rollback tests pass.
- **SC-002**: Focused afterlife documentation/source-guard tests pass.
- **SC-003**: Re-running the #1249 Chaos Sea live test no longer stalls on manual rollback of Mortal World files.

## Verification Plan

- **C# focused runtime tests**: `dotnet test BookOfEternityClient.Tests\BookOfEternityClient.Tests.csproj --no-restore --filter "AfterlifeRealmAutoRollbackTests"`
- **Documentation/source guards**: `dotnet test BookOfEternityClient.Tests\BookOfEternityClient.Tests.csproj --no-restore --filter "ExampleDocumentationValidationTests|AfterlifeDocumentationCoverageTests"`
- **Live verification**: repeat #1249 Chaos Sea Codex GM bridge test after focused tests pass.
- **General**: `git diff --check`
