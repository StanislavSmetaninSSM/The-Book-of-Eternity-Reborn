# Feature Specification: Trusted local storage and cross-platform runtime

**Feature Branch**: `1553-cross-platform-runtime`
**Created**: 2026-09-30
**Status**: Approved storage design reconstructed; implementation pending

## Source Issues & Scope

- Source: [#1553](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/1553)
- Type: cross-platform enhancement spanning multiple sessions and both clients
- Contract scope: runtime-state, console, browser, process/IPC, GM-facing operational guidance, tests and documentation
- Save compatibility: no general pre-release compatibility promise. Current-format transition states are explicit below
- Approved source: “Поддержка Linux и Windows”, revision 2, four pages, 2026-09-30; owner approved at 02:27 UTC and requested autonomous execution
- Latest direction, 18:26 UTC: replace Windows-only mechanisms, remove protection against the owner editing their own saves, demonstrate complete Linux behavior; the owner will execute Windows checks
- Wound implementation was merged by explicit owner request in [PR #1554](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/pull/1554). Its unfinished acceptance remains deferred under #1536, not declared complete by this feature

## User Scenarios & Testing

### US1 — Open either client and retain settings (P1)

A player starts a fresh local session in the console or browser, changes an ordinary setting, exits, then sees the same setting after restarting.

Acceptance:
1. Both clients on Linux open a usable main menu and create valid configuration and session generation
2. Setting changes survive a full process restart in each client
3. A second participating writer cannot interleave a transaction; stale-generation operations are refused
4. Invalid paths, existing symbolic links/reparse points, and directories in place of files fail without changing outside data

### US2 — Recover an interrupted save operation (P1)

A process crash during a supported write is recovered before the next writer or accepted game result.

Acceptance:
1. At every journal phase, restart yields exact previous bytes/absence unless a durable commit recorded the complete new state
2. A multi-file operation recovers as one declared member set, bound to its session generation
3. Unexpected current bytes produce a conflict; the journal and unknown data remain available
4. Committed cleanup never rolls back accepted data, including interruption during cleanup
5. A hard-linked destination is replaced by name rather than modified in place, leaving the other name's bytes untouched

### US3 — Play and save through the actual GM workflow (P2)

Both Linux clients complete the main game path with real GM input, then save/load and restart without losing accepted state.

Acceptance:
1. Real console and browser actions produce visible results and accepted state; a build, static page, HTTP 200 or service-only test is insufficient
2. Accepted-turn history, player choices, generation fencing and exactly-once resources/results are preserved
3. Save/load validates the complete staging set and restores the previous logical session on failure

### US4 — Keep one interactive GM session running (P2)

The bridge owns a persistent arbitrary interactive CLI. The daemon automatically sends text as human typing/pasting would, across multiple requests in the same session.

Acceptance:
1. The main GM remains interactive and persistent; one-shot stdin or manual copy/export is not an equivalent replacement
2. Manual input, automatic delivery, output/readiness, restart, cancellation and owned process cleanup use a common supported contract on Linux and Windows
3. Unknown termination outcome preserves the workspace and blocks unsafe reuse/rollback while a writer may still be alive
4. Real CLI evidence is required in addition to controlled terminal fixtures; ordinary provider descendants are not dismissed as malicious escape

### US5 — Use other local system capabilities (P3)

Audio, clipboard, launcher and system helpers have working cross-platform behavior or an explicit user-visible capability result; silently skipping Linux is not completion.

### Edge cases

Empty files, non-UTF8 bytes, BOMs, absent files, case-distinct Linux names, duplicate journal entries/properties, stale generations, read-only files, interrupted staging/commit/cleanup, same-content replacements, unknown concurrent edits, symlink ancestors/dangling links, external hard links and failed process ownership recovery.

## Requirements

- **FR-001**: Use the same trusted-local-player storage contract on both OSes. Closed-game edits are allowed if the current schema remains valid; simultaneous external editing is unsupported
- **FR-002**: Preserve validated roots, exact permitted external paths, type/link checks, schema/GM-output/archive validation and non-following cleanup
- **FR-003**: Single-file writes stage complete flushed bytes in the destination filesystem, record intent and before-image/absence, publish atomically by name, record commit, then clean up. Create and replace remain distinct
- **FR-004**: Multi-file journals bind transaction members, generation, exact before-images/absence, expected result hashes and commit state; recover before new mutations
- **FR-005**: Recovery restores exact bytes/absence, not the same physical identity. Unknown contents cause evidence-preserving conflict; uncertainty is never success
- **FR-006**: Preserve one participating writer, current generation, accepted-turn atomicity, saved choices, immutable accepted history and once-only publication
- **FR-007**: Resolve old journals using their original handler before cutover. Unsupported legacy recovery blocks cutover and retains evidence; never reinterpret or silently delete an old journal
- **FR-008**: Explicitly support fresh roots and current-schema, quiescent roots with no unresolved old journal. Back up the existing state before any format conversion. Other historical saves are not promised compatibility
- **FR-009**: Do not require a privileged service, security-setting change, protection from the computer owner, synthetic physical FileIdentity, or Windows APIs activated merely by deleting IsWindows guards
- **FR-010**: Promise process-crash recovery. Do not claim power-loss durability without separately verified file/directory synchronization ordering
- **FR-011**: Both actual Linux clients must satisfy US1 and US3. Windows uses the same intended behavior; provide a reproducible owner-run checklist and disclose that local Windows execution is unavailable
- **FR-012**: Replace Windows-only process, IPC, bridge, daemon, launcher, audio and clipboard mechanisms with supported cross-platform behavior. Thin platform adapters are permissible where a native primitive is necessary, with equivalent declared behavior
- **FR-013**: Persistent arbitrary interactive CLI and automatic daemon text delivery are mandatory. No special game API is required of the CLI
- **FR-014**: Resolve process ownership, cancellation, timeout and restart uncertainty without deleting or reusing files while an old writer may still run
- **FR-015**: Run only affected categories via scripts/test-csharp.ps1, retain isolated mutable test state, publish source/checkpoint before lengthy tests or review, and independently review completed blocks

### Superseded storage guarantees

Approved LOCAL-FR-01 replaces physical-identity/adversarial-swap requirements in [#1500, Complete Actor Materialization](../1500-complete-actor-materialization/spec.md) FR-073 and FR-090–092 with schema, generation, content/journal authority and exact-byte rollback. LOCAL-FR-02 replaces platform restrictions in FR-095–096 with same-filesystem atomic name publication. LOCAL-FR-03 preserves FR-123 staging/session integrity without mandatory protective hard links or retained Win32 handles. LOCAL-FR-05 changes SC-029 storage acceptance accordingly. Related identity-only clauses are superseded to the same extent: in particular FR-081 does not prohibit replacing a hard-linked destination name when the other name's bytes remain untouched. Existing symbolic links/reparse points, invalid types and out-of-scope paths still fail; this is not permission to edit through a link or weaken logical accepted-state authority. The cited FR/SC numbers belong to #1500, not #1536; wound gameplay criteria are unchanged. LOCAL-FR-04's foreground process-group proposal is only a bounded candidate; later requirements cover actual provider lifecycle and persistent interactive CLI behavior.

### Key entities

- Validated local scope: permitted root or exact external file set
- Logical content authority: presence, exact bytes/hash, generation and operation ownership, distinct from physical FileIdentity
- Durable transaction: unique ID, format, member set, before-images, expected after hashes and commit state
- Participating writer lease: serializes application writers; not a sandbox against the owner
- Persistent GM session: terminal/process ownership and correlated automatic input across requests

## Success Criteria

- **SC-001**: Both Linux clients pass fresh startup, setting change and full restart scenarios
- **SC-002**: Every declared storage crash phase has a passing recovery/conflict test; no outside sentinel file changes
- **SC-003**: Both Linux clients complete the selected core game/GM/save/load path with inspected visible results and persisted accepted state
- **SC-004**: Real interactive CLI receives multiple automatic requests in one retained bridge session and has tested termination/uncertainty behavior
- **SC-005**: Each converted system capability has Linux evidence and a Windows owner-run check; no unimplemented Windows-only mechanism is presented as converted
- **SC-006**: Every completed block has a verified remote SHA, required targeted checks and independent Astra XHigh review; a fresh GitHub checkout restores source and instructions

## Verification Plan

Add small storage-path, publication/recovery and client-startup categories as their tests are introduced. Select existing canonical, session, browser/console and save/load consumers by actual changed contracts; split oversized mixed categories instead of running them indiscriminately. Validate category ownership by discovery only. Update tests/selection.json for each reviewed code block. No full-suite, Fast, PreMerge or sequential all-category run.

This specification itself changes no GM-authored game field. Storage/runtime documentation must explain supported editing/recovery. Later GM workflow changes require prompts, operational examples and guards in the same block. Linux console and browser evidence is separate; Windows checks are handed to the owner and not represented as locally passed.

## Clarifications and limits

The approved storage decisions are settled. The former GM design document is a proposal, not blanket approval; implementation must preserve FR-013 and resolve concrete lifecycle risks. New material behavior losses require direction rather than silent redesign. Final all-platform claims remain limited by actual Windows results.
