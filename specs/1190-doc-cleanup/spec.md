# Feature Specification: Repository Documentation Cleanup

**Feature Branch**: `task/1190-doc-cleanup`

**Created**: 2026-06-21

**Status**: Draft

**Input**: User description: "Clean up noisy obsolete developer/agent files from game-facing directories, especially OtherGuides, while preserving docs/superpowers as the technical workspace."

## Source Issues & Scope

- **Source GitHub issue(s)**:
  - #1190 https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/1190
  - #1248 https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/1248
- **Issue type**: task / documentation / tech-debt
- **Spec Kit justification**: This is a repo-wide documentation cleanup that changes GM-facing directories and may remove or relocate files referenced by prompts, tests, examples, or manifests.
- **Contract scope**: GM-facing docs, repository documentation, examples/reference integrity.
- **Out of scope**: `docs/superpowers/**`, `docs/superpowers/specs/**`, runtime code changes, browser UI, console UX, and rewriting current GM guidance beyond reference fixes.

## User Scenarios & Testing

### User Story 1 - GM Sees Only Relevant Game Guidance (Priority: P1)

A GM or GM-agent browsing repository guidance should not find obsolete coding-agent implementation plans in `OtherGuides` or other game-facing directories.

**Why this priority**: Misleading files can cause the GM to read stale implementation work instead of current game-authoring instructions.

**Independent Test**: Inspect `OtherGuides` and root-level guidance files after cleanup; remaining files should be current GM-facing guidance, contract references, or clearly named developer docs in technical directories.

**Acceptance Scenarios**:

1. **Given** `OtherGuides`, **When** a GM scans the directory, **Then** files describe current game, story, prompt, rule, or contract guidance rather than completed implementation plans.
2. **Given** an obsolete agent implementation plan outside `docs/superpowers`, **When** it has no live references, **Then** it is removed.

---

### User Story 2 - References Stay Valid (Priority: P2)

A developer or agent should not encounter broken references after noisy files are removed.

**Why this priority**: Deleting documentation without checking references can break prompt entrypoints, tests, manifests, or examples.

**Independent Test**: Search for every deleted filename/path and run targeted documentation/source-guard verification.

**Acceptance Scenarios**:

1. **Given** a deleted file, **When** the repository is searched for its filename and path, **Then** no live reference remains unless it is intentionally documented as removed.
2. **Given** a docs-sensitive change, **When** relevant documentation tests run, **Then** they pass or any unrelated pre-existing failure is documented.

---

### Edge Cases

- A file may be developer-facing but still required by source-guard tests or prompt entrypoints; do not delete it without updating the source of truth.
- A file may be old but still useful as GM narrative/style guidance; keep it if it helps current GM play.
- `docs/superpowers/**` is intentionally technical and should not be cleaned in this pass.
- Generated build outputs, packages, and game saves are not part of this cleanup unless they are tracked repository noise.

## Requirements

### Functional Requirements

- **FR-001**: Audit markdown, text, JSON manifest, and README-like files outside `docs/superpowers/**` for obsolete agent/developer noise.
- **FR-002**: Classify suspect files as keep, remove, relocate, or follow-up.
- **FR-003**: Remove or archive files that are obsolete, development-only, completed implementation plans, or misleading to GM usage, after live GM rules are preserved in current guidance.
- **FR-004**: Preserve current GM-facing guidance, examples, validation manifests, afterlife contract docs, and prompt-linked documents.
- **FR-005**: Update or remove references to deleted files in docs, prompts, tests, manifests, and scripts.
- **FR-006**: Record ambiguous files in the PR summary or a follow-up issue instead of deleting them silently.

## Success Criteria

### Measurable Outcomes

- **SC-001**: `OtherGuides` contains no completed coding-agent implementation plans after cleanup.
- **SC-002**: Every deleted file path or filename has been searched for live references.
- **SC-003**: Targeted docs/source-guard verification is run and reported.
- **SC-004**: Remaining known technical development artifacts outside `docs/superpowers/**` are either justified by current project use or tracked as follow-up.

## Verification Plan

- **C# verification**: `dotnet test BookOfEternityClient.Tests\BookOfEternityClient.Tests.csproj --filter "ExampleDocumentationValidationTests|AfterlifeDocumentationCoverageTests|BrowserFrontendWorkspaceTests"`
- **Documentation/contract verification**: `git diff --check`; `rg` searches for each deleted filename/path.
- **Frontend verification**: N/A.
- **Manual/player-facing verification**: Inspect `OtherGuides` file list and root guidance list after cleanup.

## Assumptions

- `OtherGuides` is intended primarily for GM/game guidance and contract documentation.
- `docs/superpowers/**` is the accepted location for technical Superpowers working files and can remain unchanged.
- Removing stale documentation does not require adding new GM examples unless a live contract document is changed semantically.
