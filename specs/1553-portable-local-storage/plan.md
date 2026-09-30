# Implementation Plan: Trusted local storage and cross-platform runtime

**Branch**: `1553-cross-platform-runtime` | **Date**: 2026-09-30 | **Spec**: [spec.md](spec.md)
**Source**: [#1553](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/1553)

## Current checkpoint — B1b journal accepted; B2 client integration next

Base: `f6dc2a1ce3e73f5e6940f686c97b863c9f7a8173`, the explicitly authorized wound merge into main. New branch was published from that exact base. Remote persistence instructions were adopted at `3970a182fe6b4abd4f16a51e86f7c903b0626768`; their two blobs equal reviewed docs tip `ab646806234e02e75619cb460d0b80325af328ad`.

Old local portability commits, including 39dbb90/512eb2d, remain unavailable. This is new implementation from surviving source, not recovered old code or revalidated old test results. #1536 acceptance remains deferred. The former preparation branch is preserved but is not the active branch.

The approved four-page storage design was reconstructed and passed independent Astra XHigh review at 80d5fc52. B1a's common path/cleanup helper is accepted at source `18461c67fccfd478eb4dadb4d2d3136b3ce952e6`: 35/35 selected Linux cases passed, and independent GPT-6 Astra XHigh closed all findings. The helper remains disconnected from application writers. The B1b common publication/member journal is accepted at reviewed checkpoint `795d79bec71459ad2d90121df06cba9d85a2cf32`, with runtime source `8d36e7083d9fe89cde70a2f145ba6109c3d6c649`: 66/66 Linux-run cases including cold-process recovery, BOM-generation handling and pure Windows spelling checks, discovery-only ownership validation, and independent Astra XHigh acceptance after both P2 findings were corrected. B2 is next: actual two-client bootstrap/settings integration. Browser settings require the two-member/generation slice already represented by this engine. Startup, settings and gameplay are not established by these disconnected infrastructure tests.

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
- Create `BookOfEternityClient/Core/TrustedLocalFilePublication.cs`: byte-preserving create/replace/delete, explicit content authority, one durable journal protocol for single or declared multiple members, and deterministic restart recovery
- Add focused tests in `BookOfEternityClient.Tests/TrustedLocalFileScopeTests.cs` and `TrustedLocalFilePublicationTests.cs`; add narrow category owners in `tests/categories.json`
- Input: validated scope, journal root, destination path, expected current bytes/absence and desired bytes/absence. Output: explicit committed logical content result; no counterfeit FileIdentity
- Keep new primitives disconnected from application writers until their negative/recovery paths pass and are reviewed
- RED: nonexistent scope/publication behavior, exact bytes/absence, conflicts, links/types/traversal, hard-link outside-byte preservation, all journal phase cuts and cleanup restart
- GREEN: implement only this contract, run selected categories and discovery ownership, record exact cases/time, remote-save and independent Astra XHigh review

### B2 — Route ordinary writes, generation and settings through the common backend

- Modify `Core/FileSystemManager.cs`, `Core/StateManager.cs` and precise affected consumers; split new portable helpers into partial/focused files if needed instead of enlarging the existing 6,020-line manager
- Consume B1 logical publication results; remove physical-identity requirements from ordinary supported writes on both OSes, not only Linux
- Retain write/lifecycle leases and stale-generation fencing. Legacy journals recover with the original handler or block cutover. No synthetic identity and no call into Win32 rename on Linux
- Introduce a shared leased bootstrap that explicitly creates/validates both config and generation in each client. Fresh console currently has no observed generation creation; fresh browser can show defaults without a config file
- Include the bounded common multi-member transaction route for browser settings/audio here. Config and GM projection are already one declared browser transaction; generation binding and whole-member conflict preflight are required before this stage can claim settings persistence
- Prepare complete desired config/projection images before the single B1 member-set publication; do not reuse the old browser imperative immediate-write callback as a group journal or create a settings-only protocol
- Give the common integration an explicit committed/rolled-back/uncertain outcome. Cleanup failure after durable commit must never trigger the old coordinator's unconditional runtime-snapshot rollback; reconcile runtime settings against the established durable outcome
- Preserve in-process mutation boundaries, generation revision invalidation, local UI ownership and pending-turn checks. Keep the physical mutation-recorder path explicit for B3; logical results must not masquerade as old physical receipts
- Bootstrap distinguishes absent configuration from invalid existing configuration; StateManager's default-on-parse-error read is insufficient authority for silently overwriting an existing file
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

At f8b1655 the corrected category passed 31/31 with no skips, complete cleanup, 2:30.158. Discovery-only ValidateCatalog also passed in 1:37.657, zero executed tests. Focused review accepted the original ordinary cases but found remaining Windows aliases in the same boundaries: reserved stems with spaces before an extension, and directory-root aliases under overlapping broad grants (case and extended namespace spellings). Existing test methods now include these cases; no test method or category owner was added. Pure-policy scaffolds preserve test-first execution. The fix will keep allow grants ordinal, conservatively deny Windows aliases of directory grants, and canonicalize supported extended drive/UNC paths without physical-identity machinery. Other Windows device namespaces will fail closed. No runtime acceptance claim changes.

Alias-regression RED at c75e1b4116cdf3943bbd2d13c764513a90710336: 35/35 executed, 26 passed, 9 failed as expected (four accepted-invalid aliases/namespaces and five pending pure-policy cases), 2:41.829, cleanup complete. Replaced scattered alias handling with one pure Windows namespace canonicalizer; supported extended drive/UNC paths become one ordinary spelling, and directory-target denials conservatively include case aliases even under broad overlapping grants. Allow grants remain ordinal and reserved stems trim trailing ASCII spaces before device comparison. Corrected GREEN/re-review are pending. The prior discovery-only audit at f8b165503ccc00ece2bdeab3344ae890677c8def recorded 10,209 methods and 103 categories with no unmapped/stale selectors; subsequent edits changed only data/assertions of existing test methods, and the selected category will rediscover all 35 cases.

### B1a accepted evidence, 2026-09-30 20:04 UTC

- Tested source: `18461c67fccfd478eb4dadb4d2d3136b3ce952e6`, Debian 13 x64; tool versions/environment in quickstart.md
- Command: `pwsh -NoLogo -NoProfile -File scripts/test-csharp.ps1 -Category portable-storage-paths`, with `DOTNET_PROCESSOR_COUNT=1` and documented writable caches
- Result: exit 0, 35/35 passed, 0 failed/skipped, 1/1 descriptor complete, 2:42.7018133 wall time; owned-process and runtime cleanup complete; duplicate IDs 0
- Independent reviewer: separate GPT-6 Astra, actual XHigh. Reviewed source, full diff check, summary and TRX without rerunning passing checks. All three initial P2 findings and their alias-dependent follow-ups resolved; no remaining blocking finding in this helper
- Catalog ownership: discovery-only audit at `f8b165503ccc00ece2bdeab3344ae890677c8def` passed, 10,209 methods, 103 categories, 0 unmapped/stale selectors, 0 executed tests, 1:37.657. Later changes added no test methods or category owners; the final category discovered/executed all 35 cases
- GM synchronization: no GM-authored field, capability or game command changed; only disconnected file-scope infrastructure. No worked GM-state example is required for B1a
- Limits: Windows filesystem execution is still owner-run. Pure Windows spelling cases ran on Linux. No file journal, application cutover, menu or game/GM workflow is accepted by these results

## B1b journal design and first WIP checkpoint, 2026-09-30

Verified starting ref: `0f972ddc8529ab72c2515e4c603fbaaea9f30a3e`. Independent fresh GitHub clone restored all 3,387 tracked files at that exact SHA into a new clean directory; `git fsck --connectivity-only` exited 0 and scope/test/plan blobs matched the working checkout. This proves source restoration only, not build/test execution in the recovery directory.

Implement T012/T013/T013-M as one disconnected engine. The canonical lease is required and validated by FileSystemManager; no second writer-coordination framework is introduced. A fixed private `trusted-local-publication-v1` runtime area holds one strict JSON manifest with transaction ID, explicit generation presence/ID before and after, declared normalized member paths, exact before bytes/absence plus hashes, desired presence/hash, and committed state. The initial intent is atomically published before any destination or sibling staging mutation. A later atomic manifest replacement records group commit before cleanup. Private staged manifest files without an active intent are preparation-only and cannot authorize destination recovery. Malformed/unknown evidence remains a blocking conflict. Original physical/other journal locations are untouched; B2 must explicitly run original supported recovery or block before bootstrap.

Each destination is published from a flushed, create-new sibling stage on the same filesystem; rollback uses its own create-new sibling name and exact retained bytes. Create/replace/delete preserve presence and never rewrite a hard-linked destination in place. Recovery preflights the complete manifest, generation, every current member and all owned sibling artifacts before any rollback or cleanup. Only known before/after content is recoverable; committed members must still match the accepted after images. Cleanup removes sibling artifacts before the active decision, so interrupted committed cleanup cannot become rollback. Explicit absent generation plus a declared generation member supports later leased fresh bootstrap. Generation transitions are validated against the existing current-schema generation document; absence is never an implicit wildcard.

Files: new Core/TrustedLocalFilePublication.cs, unit tests, narrow `portable-storage-publication` owner/selection, and this existing plan/tasks. Test-first scaffold throws; RED, implementation, cold-process fixture, GREEN, catalog ownership and independent Astra XHigh review remain pending. No application writer is connected and no menu/settings/gameplay/Windows execution claim is made. No GM-authored gameplay field or command changes, so no GM example update is required in B1b.

B1b first RED at `c130852634d1fd53b253868d40242cfd484ae5ed` plus its exact published catalog recovery patch: 29/29 selected cases executed, all failed at the expected NotImplementedException scaffold, 0 skips, 2:27.1798919, owned cleanup complete. The initial implementation follows those failures. The journal stores exact desired images too (in addition to their hashes), keeping a small complete data contract without a second blob format. Known private intent/member staging files are non-authoritative scratch and may contain partial bytes after a crash; their recorded names can be removed without following them. Canonical member or journal-image uncertainty remains a blocking evidence-preserving conflict. The legacy-separation fixture now creates its old-format sentinel after acquiring the existing lease, preventing the old Windows handler from consuming that deliberately invalid fixture. GREEN and independent review remain pending.

Normal catalog publication recovered at `4e31a0fc51250f4947bbd06ff81ec9125a3dbfba`: verified remote tree `ccc5c782e889c61606ccc34170bb8ce1249f5046`, catalog blob `4b805af95ad56487e7811eb2287b74dc8b26af5d`, clean synchronized checkout. Temporary recovery-only patch/instructions were removed. The RED source is identical; no duplicate RED run is needed. GitHub tree inline-content publication succeeded where the large blob request stalled; future writes remain bounded and verified.

B1b initial GREEN at `bedb410f201dd7a3698aa53009116ed2c9bb2aa7`: portable-storage-publication passed 29/29, 0 failed/skipped, 2:20.8734654; descriptor/case selection and owned cleanup complete. Added test-only executable fixtures for abrupt Environment.Exit at publication/commit/cleanup/rollback, a separate recovery process and cross-process canonical-lease exclusion; no product entrypoint or coordination mechanism changes. Added malformed/member/hash/path/link and partial-scratch coverage. Duplicate generation-member validation currently throws the wrong exception before the manifest validator; its new regression should fail before correction. The enlarged focused run, catalog audit and review remain pending.

Expanded run at `13ce571e2397f3e8f84bc9f21442f3e1a27c9b40`: 42/42 executed, 41 passed, only the expected duplicate-generation-member exception regression failed; 0 skips, 2:53.4050263, complete cleanup. All four abrupt-exit/separate-process recovery cases and cross-process lease exclusion passed. Source review found that FileSystemManager's current generation reader accepts case-insensitive property names and extension properties; add an explicit baseline/assertion before matching that existing schema behavior. This is not compatibility with historical saves. Fixture bin/obj paths are now explicitly ignored. Duplicate-member and generation-reader corrections are pending RED evidence for the latter.

Generation-contract RED at `9444fef8b49f8c81f7732fc806d4eef483e6f656`: 43/43 executed, 41 passed and exactly the two intended failures (duplicate generation member reached LINQ SingleOrDefault; a current-schema case-insensitive generation reached the stricter journal JSON options). 0 skips, 2:26.4673813, complete cleanup. Corrections validate duplicate input paths before interpreting generation and use the existing generation reader's case-insensitive/extension-tolerant options only for generation images; the new journal remains strict. Next: final focused GREEN, discovery-only ownership audit, then the single independent Astra XHigh review.

### B1b initial candidate evidence, 2026-09-30 21:04 UTC

- Source: `d8ecd9dc04dc073830cff80f9dcd738b663f9449`, verified remote ref and exact tree `c37c7d2e30b626e9b04d624b00614b5734d5ea3f`; Debian 13 x64, toolchain/environment in quickstart.md
- Command: `pwsh -NoLogo -NoProfile -File scripts/test-csharp.ps1 -Category portable-storage-publication`
- Result: exit 0, 43/43 passed, 0 failed/skipped, 1/1 descriptor complete, 2:27.6235939 wall time, duplicate IDs 0, owned-process/runtime cleanup complete
- Covers exact BOM/non-UTF8/empty/absence, create/replace/delete/no-op and hard-link outside-byte safety; declared member/generation bootstrap and stale-generation conflict; full-member/evidence preflight; strict malformed/version/hash/path/link evidence; all declared publication, rollback and cleanup cuts; partial private scratch; separate-process recovery after abrupt exit at publication/commit/cleanup/rollback and competing-process lease exclusion
- Discovery-only command: `pwsh -NoLogo -NoProfile -File scripts/test-csharp.ps1 -ValidateCatalog`; exit 0, 104 categories, 10,230 methods/files, no unmapped/stale selectors, **0 executed tests**, 1:35.4003472, owned cleanup complete
- Selection: only new portable-storage-publication. Existing scope implementation and application consumers are unchanged; paths category is conditional on scope changes. The test executable is owned by this category through its invoking cases, with per-test roots and no shared mutable fixtures
- Journal v1 API: construct with FileSystemManager plus current explicit TrustedLocalFileScope; Publish requires that manager's active canonical lease, explicit generation presence/ID and a nonempty declared set of `(absolute path, exact before bytes/null absence, exact desired bytes/null absence)`. Recover uses the same lease/scope. Results carry transaction ID, resulting generation and member presence/hash; never physical identity. Destination parents must already exist and be safe; B2 bootstrap remains responsible for creating its directory layout
- Original journals remain untouched by this engine. B2 must explicitly resolve them through original supported recovery or block before new bootstrap. Process-crash recovery is the guarantee; power-loss directory synchronization is not claimed
- Independent Astra XHigh review is pending. B1b is a verified candidate, not an accepted block. Windows filesystem execution, application cutover, menus/settings, gameplay and GM/save-load remain unverified here

### B1b independent review round 1, 2026-09-30 21:12 UTC

Independent actual GPT-6 Astra XHigh review of `0f972ddc..681e9069` found two P2 defects. (1) The publisher deserializes raw generation bytes, unlike FileSystemManager's BOM-aware StreamReader; the reviewer reproduced valid UTF-8-BOM and UTF-16-BOM images accepted by the existing reader but rejected by the publisher. (2) Normalized member paths are compared against raw root-derived authority paths; supported extended Windows drive/UNC roots therefore miss generation-member and reserved journal/lock comparisons. The latter is source/pure-policy evidence, not Windows filesystem execution. Both findings are accepted for a narrow correction batch.

Before fixing, add exact UTF-8/16/32 BOM bootstrap/transition/rollback cases with an independent existing-reader baseline, plus a pure throwing authority-normalization seam and drive/UNC generation/journal/lock comparison cases. Store original image bytes/hashes unchanged and match existing text decoding only when reading generation semantics. Normalize engine authority paths consistently through the accepted scope spelling policy; no new identity or owner-race machinery. RED, correction, GREEN and focused re-review are pending.

Fresh source recovery of the reviewed candidate is verified: independent GitHub shallow single-branch clone restored 3,392 tracked files at `681e906939a2df66dd03392487970b8e19703bbb`, tree `f150a6984b5906cb3f85a0cda3d1dd2d8a57313a`, clean status and `git fsck --connectivity-only` exit 0. Engine/test/fixture/project/program/catalog/plan blobs matched the implementation checkout. No build/test was run in that recovery directory.

Review round-1 RED at `82f181b9377aff9ae9b69ca1ac63396909a9fb59`: 66/66 cases executed, original 43 passed, all 23 new regressions failed for the expected causes (15 raw BOM/encoding parse failures and 8 pure-policy scaffold failures); no skips, 2:15.5350692, complete cleanup. The correction now decodes generation semantics using the same BOM-aware UTF-8 StreamReader as FileSystemManager while retaining exact image bytes/hashes. All four root-derived engine authority paths are normalized once through the existing scope Windows spelling policy, then used consistently for generation and reserved-path comparisons. The accepted B1a helper is unchanged; no extra path category rerun. GREEN and focused re-review remain pending.

### B1b corrected candidate evidence, 2026-09-30 21:24 UTC

- Corrected runtime source: `8d36e7083d9fe89cde70a2f145ba6109c3d6c649`; remote ref and exact tree `f9167cbe6892058d4f7a2ed39c4d41db0db65e1e` verified before tests
- Selected GREEN: `pwsh -NoLogo -NoProfile -File scripts/test-csharp.ps1 -Category portable-storage-publication`, exit 0, **66/66 passed**, 0 failed/skipped, 1/1 descriptor complete, duplicate IDs 0, **2:50.4216932**, owned-process/runtime cleanup complete
- The 23 added review cases passed: 15 BOM encoding/bootstrap/transition/rollback cases preserve exact bytes, and 8 pure extended drive/UNC generation/journal/lock comparisons use the same normalized authority paths as the engine. Windows filesystem behavior is still owner-run, not established by these pure-policy cases
- Refreshed discovery-only ownership audit for the two new test methods: `pwsh -NoLogo -NoProfile -File scripts/test-csharp.ps1 -ValidateCatalog`, exit 0, 104 categories / **10,232 methods/files**, no unmapped/stale selectors, **0 executed tests**, **1:21.7828005**, cleanup complete
- Independent recovery checkout, originally cloned directly from GitHub, was refetched directly from GitHub at this exact corrected source: 3,392 tracked files, exact tree match, clean status, connectivity fsck exit 0. Engine blob `679bbb60f6f8a94edab85a66f1b3eed92f3e61df`, test blob `49355e039d91be3a7fdbcd7b2a5d64716f3fbad1` and plan blob matched the implementation checkout. No build/test was run there
- Fix range begins at reviewed candidate `681e906939a2df66dd03392487970b8e19703bbb`. Full fix-range whitespace check passed. Only decoder/path comparisons, their tests and checkpoint/owner-check instructions changed; B1a helper and application writers remain unchanged
- Both review findings have covering RED/GREEN evidence. Focused independent Astra XHigh re-review is pending; B1b acceptance and B2 cutover remain gated on it

### B1b acceptance checkpoint, 2026-09-30 21:42 UTC

Independent GPT-6 Astra with actual XHigh accepted `795d79bec71459ad2d90121df06cba9d85a2cf32` after scoped review of the corrections from `681e9069`. Both P2 findings are **addressed**; no new consequential defect remains. Specification fit and code/test quality were accepted. Runtime source is `8d36e7083d9fe89cde70a2f145ba6109c3d6c649`; the subsequent checkpoint changed prose only. The reviewer inspected the 66/66 GREEN and 104-category / 10,232-method discovery evidence recorded above without repeating passing runs. Independent GitHub source restoration at the exact tested source is also recorded above; it proves download/tree/blob integrity, not an additional build.

T012/T013/T013-M/T014 are complete. The accepted result is the disconnected common file/member journal, not completed #1553. Linux console/browser startup, settings persistence, gameplay/GM/save-load, remaining platform mechanisms and actual Windows filesystem execution are still pending. B2 retains the original accepted scope and existing cutover map, with the concrete preparation/outcome/boundary seams above. This acceptance-record and next-step prose does not change C# behavior and requires no repeated C# run.
