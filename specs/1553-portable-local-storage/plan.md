# Implementation Plan: Trusted local storage and cross-platform runtime

**Branch**: `1553-cross-platform-runtime` | **Date**: 2026-09-30 | **Spec**: [spec.md](spec.md)
**Source**: [#1553](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/1553)

## Current checkpoint — B1a path-scope review corrections, WIP

Base: `f6dc2a1ce3e73f5e6940f686c97b863c9f7a8173`, the explicitly authorized wound merge into main. New branch was published from that exact base. Remote persistence instructions were adopted at `3970a182fe6b4abd4f16a51e86f7c903b0626768`; their two blobs equal reviewed docs tip `ab646806234e02e75619cb460d0b80325af328ad`.

Old local portability commits, including 39dbb90/512eb2d, remain unavailable. This is new implementation from surviving source, not recovered old code or revalidated old test results. #1536 acceptance remains deferred. The former preparation branch is preserved but is not the active branch.

The approved four-page storage design was reconstructed and passed independent Astra XHigh review at 80d5fc52. B1a introduces a common path/cleanup helper, still disconnected from application writers. Its initial 16-case Linux check passed; review found three path-boundary defects. All three now have failing regression evidence and corrections; the next step is corrected GREEN, discovery-only catalog audit and focused re-review. Publication journals, application startup, settings and real gameplay are not implemented by this helper.

Latest owner instruction, 2026-09-30 18:26:06 UTC: “отлично. Тогда работай над кросплатформенностю игры как договаривались, с конвертацией всех windows only механизмов в платформонезависимые, также убрать всю сложную защиту сохранений и т.д. (игра одиночная, и бесплатная, если игрок хочет взломать сохранение это его дело) и сделать полностью кросплатформенной, работающей и в linux, и в windows. У тебя там стоит только linux, поэтому достаточно если ты отсчитаешься что на linux радотает все как надо, после этого на windows я сам проверю.”

## Goal and architecture

First deliver the approved common storage/recovery layer and real Linux menu/settings persistence in both clients. Then complete GM/gameplay/save-load and remaining platform mechanisms without weakening their behavior. Common managed filesystem operations publish by name; content/presence/generation replace owner-resistant physical identity as authority. Existing Win32 journal recovery remains a bounded transition handler only, never the new portable backend.

## Technical Context

- C# app targets .NET 8; current test compiler requires SDK 10; PowerShell 7 category runner; React/TypeScript/Vite frontend
- .NET BCL filesystem/JSON/hash operations; no new storage dependency or privileged service
- Debian 13 x64 current executor. SDK 10.0.401, .NET/ASP.NET 8.0.31, PowerShell 7.6.6, Node 24.19.0, npm 11.9.0, Spec Kit CLI 1.0.13
- Actual setup and portable reproduction commands: [quickstart.md](quickstart.md)
- One participating application writer. Files can be large, so no arbitrary new size limit is introduced; journal resource failure aborts safely
- Windows execution belongs to the owner; Linux tests cannot establish Windows results

## Constitution Check

Issue traceability, client parity, existing schemas/GM write boundaries, test-first work and pre-release save policy are retained. The approved trusted-player constraint is added to the constitution. No gameplay capability/field changes are introduced by B1; no worked GM state example is needed for a private storage primitive. Integrating new recovery behavior requires operational documentation; changing GM workflow additionally requires prompts/examples/guards. Repository category-only and per-block independent-review rules override generic aggregate-test or final-review-only suggestions.

## Files and bounded blocks

### B1 — Common path scope and durable file publication

- Create `BookOfEternityClient/Core/TrustedLocalFileScope.cs`: validate approved roots/exact files, existing links/types, same-parent staging and safe cleanup
- Create `BookOfEternityClient/Core/TrustedLocalFilePublication.cs`: byte-preserving create/replace/delete, explicit content authority, durable journal and deterministic restart recovery
- Add focused tests in `BookOfEternityClient.Tests/TrustedLocalFileScopeTests.cs` and `TrustedLocalFilePublicationTests.cs`; add narrow category owners in `tests/categories.json`
- Input: validated scope, journal root, destination path, expected current bytes/absence and desired bytes/absence. Output: explicit committed logical content result; no counterfeit FileIdentity
- Keep new primitives disconnected from application writers until their negative/recovery paths pass and are reviewed
- RED: nonexistent scope/publication behavior, exact bytes/absence, conflicts, links/types/traversal, hard-link outside-byte preservation, all journal phase cuts and cleanup restart
- GREEN: implement only this contract, run selected categories and discovery ownership, record exact cases/time, remote-save and independent Astra XHigh review

### B2 — Route ordinary writes, generation and settings through the common backend

- Modify `Core/FileSystemManager.cs`, `Core/StateManager.cs` and precise affected consumers; split new portable helpers into partial/focused files if needed instead of enlarging the existing 6,020-line manager
- Consume B1 logical publication results; remove physical-identity requirements from ordinary supported writes on both OSes, not only Linux
- Retain write/lifecycle leases and stale-generation fencing. Legacy journals recover with the original handler or block cutover. No synthetic identity and no call into Win32 rename on Linux
- Add test-first startup/configuration/restart and generation/conflict scenarios; update `tests/selection.json` from actual changed consumers
- Run actual console and browser menu/settings flows in separate isolated roots, with complete process restarts. Update operational docs

### B3 — Multi-file transactions, accepted-turn recovery and save/load

- Modify `Core/FileSystemManager.cs`, `Services/ExplorerLocalTurnRollbackArtifacts.cs`, `Services/DarenRewardProfileRollbackTransaction.cs`, `Services/DarenRewardProfileFileStore.cs`, `Services/SaveLoadService.cs` and only proven downstream consumers
- One durable transaction binds the member set, generation, before-images and result hashes. Preflight the whole set; unknown contents retain all evidence. Resume committed cleanup without rollback
- Replace physical-identity receipt requirements with explicit logical publication authority while preserving current-schema immutable history and accepted-turn ownership
- Tests first for multi-member partial application, generation replacement, unknown-byte conflict, crash after commit/during cleanup, invalid staging and exactly-once accepted result
- Run selected transaction/save-load/turn consumers, then real file-GM game turn and save/load in both Linux clients

### B4 — Persistent interactive GM, process lifecycle and IPC

Detailed implementation is gated on the concrete architectural review of current bridge/daemon/provider behavior, not a new approval of settled storage. Preserve the mandatory live arbitrary CLI and automatic text input. Resolve owned-tree termination, unknown stop, cold recovery, input arbitration and readiness. Add real CLI and adversarial-but-supported lifecycle fixtures before claiming completion. Common behavior may use necessary thin platform adapters; neither manual copy nor one-shot stdin substitutes for the workflow.

### B5 — Remaining system mechanisms and full Linux acceptance

Convert audio, clipboard, launcher/system helpers and cross-platform test infrastructure where needed; preserve equivalent behavior and honest unavailable-capability errors. The five historical QTE LF/CRLF inventory issues need a narrow contract, not global string normalization. Run a final scope audit and Linux end-to-end console/browser/GM/save-load evidence; provide Windows checklist without claiming it was executed.

## Review focus

1. Link/directory at any root, ancestor, leaf or journal entry cannot redirect mutation or cleanup outside scope
2. Crash around intent, replacement, commit and partial cleanup cannot lose the old bytes or roll back a committed result
3. Unknown current bytes and stale generation retain evidence and block mutation, rather than silently overwrite
4. Multiple members and shared consumers preserve one accepted-turn boundary; logical authority cannot be confused with physical identity
5. A surviving GM descendant or uncertain stop cannot allow session reuse, rollback or destructive cleanup

## Verification and evidence ledger

- Source recovery at 116fa939: independent GitHub clone to an empty folder; exact HEAD, clean checkout, AGENTS/workflow present. Download proof only
- Baseline app source at 116fa939 (same app tree as f6dc2a1): `dotnet build BookOfEternityClient/BookOfEternityClient.csproj --nologo -v minimal` succeeded, 0 errors, 2 NU1900 warnings, 1:50.88. NuGet vulnerability cache tried the read-only default home; next commands explicitly set all NuGet cache paths. This is a build, not gameplay/test evidence
- `npm ci --prefix BookOfEternityClient.WebFrontend`: 64 packages installed; no frontend tests or visual checks run
- PowerShell official 7.6.6 Linux x64 archive SHA256 matched published `ddbc4a2d113bbd46d283cfedcbcd117a70caefd7673f41f2b4e0000badf103bc`; `pwsh --version` succeeded
- `specify version`: 1.0.13; `specify integration list` from checkout: Codex installed/default. Existing repository scaffolding metadata 0.9.3 is historical and was not blindly regenerated
- Unrun: all new storage categories, real console/browser/game/GM acceptance, Windows. R0 changes only requirements/governance; no C# execution required for prose alone
- Source recovery at 3970a182: a second independent GitHub clone into a new empty directory yielded that exact HEAD and clean checkout; AGENTS/workflow blobs matched efc6028a/9ecaea56. This proves source retrieval, not test execution
- R0 Spec Kit checks: existing-branch hook with explicit branch and AllowExistingBranch, check-prerequisites -Json -PathsOnly, setup-plan -Json, setup-tasks -Json and check-prerequisites -Json -RequireTasks -IncludeTasks all exited 0 and resolved this feature. Existing plan was preserved. Initial worktree git diff --check passed but did not include new untracked Markdown; post-publication full-range check found four Markdown hard-break spaces, now removed
- R0 consistency pass: all FR-001..015 and SC-001..006 map to B1..B5/T010..T053; no uncovered storage requirement or contradiction with the new trusted-player constitution was found. B4 concrete architecture remains intentionally pending; no implementation or result is claimed. Independent review is still pending

## Consistency and sequence

US1 → B1/B2; US2 → B1/B3; US3 → B3/B4/B5; US4 → B4; US5 → B5. B2 consumes B1 logical content, never physical identity. B3 extends the same journal semantics rather than inventing a competing protocol. B4 can be researched in parallel without shared storage edits. All implementation blocks publish source and checkpoint before review; new edits stop if remote preservation fails.

## B4 research checkpoint — proposal only

[research.md](research.md) preserves the read-only investigation of persistent terminal, automatic input, IPC and process ownership. No candidate was installed or tested. The dependency and rare final-supervisor-loss recovery choices remain under bounded technical evaluation. Favor the existing stack and maintainability while preserving the mandatory interaction; do not substitute a one-shot job or claim generic TUI readiness.

## B1a checkpoint — path scope, RED pending

R0 passed independent GPT-6 Astra XHigh review at 80d5fc52; #1500 references, checksum fail-fast and full-range whitespace issues were corrected and verified. B4 research remains a proposal, not a validated design. B1a adds a compile-only throwing API scaffold plus behavioral tests and narrow portable-storage-paths selection. There is no implemented new runtime behavior or application cutover. Next step: execute RED through scripts/test-csharp.ps1, then implement the scope and verify/review.

Baseline preparation: canonical-storage -PlanOnly at e1dffabe failed during parallel build, 0 tests. Direct single-node diagnostic build succeeded. With DOTNET_PROCESSOR_COUNT=1, canonical-storage -PlanOnly at 80d5fc52 completed in 3:52.587, 5 descriptors/263 planned cases, 0 executed tests, cleanup complete. This is discovery, not a test pass. Use the one-processor environment for subsequent local runner commands until the environment-specific parallel-MSBuild issue is resolved; no category budget was raised.

B1a first RED at e03a93af: portable-storage-paths ran 16/16 cases in 2:17.082, all failed; 15 reached the expected NotImplementedException scaffold, one was blocked earlier by denied Unix socket Bind. Replaced only the Linux special-file setup with a file-only FIFO fixture; Windows retains its AF_UNIX special-file fixture. No network/security permission changes. This RED is not yet a complete behavioral baseline; corrected-fixture RED is next.

B1a corrected-fixture RED at a8676212: 16/16 cases reached the expected unimplemented-scope failure, 0 passes, 2:42.081, cleanup complete. Implemented the common scope after this RED: explicit roots/exact external grants, ancestor/type/link checks, complete cleanup preflight and non-following deletion. Linux uses the stable statx type-only query because .NET 8 FileAttributes reports FIFO/socket nodes as Normal; no inode identity, anti-owner handle retention or privilege is introduced. Source references: .NET runtime v8.0.0 FileStatus.Unix.cs and Linux statx(2). GREEN and independent code review are pending. No application writer uses the new scope yet.

B1a GREEN at af11d871: 16/16 passed, no skips, complete selection and cleanup, 2:34.857. Independent GPT-6 Astra XHigh review found three P2 defects: absent/replaced directory grant accepted as a file; Windows case-insensitive comparison can broaden a grant on a case-sensitive directory; Windows trailing-dot/space normalization can skip a literal link during cleanup preflight. Review verified Linux counts and source, but Windows findings are source-backed inferences, not Windows execution. Added regression tests and throwing pure-policy scaffolds before implementing corrections. Conservative exact grant spelling and rejection of ambiguous raw Windows names are the intended fix; corrected RED is next. No other review findings, and no application cutover.

Review-regression RED at 22b2aefa: 31/31 cases executed, 16 passed and 15 failed as expected (two real root-boundary failures and 13 unimplemented pure-policy cases), 2:39.603, cleanup complete. Corrections now reject directory-grant file targets, use ordinal grant spelling on every OS and reject ambiguous raw Windows names before normalization/probing. Windows lexical policy is testable on Linux; this is not a Windows filesystem execution result. Corrected GREEN and catalog/re-review remain pending.
