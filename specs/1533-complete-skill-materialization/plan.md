# Implementation Plan: #1533 / PR #1579 review corrections

**Branch**: `1533-complete-skill-materialization` | **Date**: 2026-11-01
**Issue**: https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/1533
**Spec**: [spec.md](spec.md), correction revision 1 approved by user on 2026-11-01.

## Scope and engineering method

Correct the five findings against `673c531710ec3acf5132369f42a43f1a7ce8c13c` without extending part 1. One primary developer owns identity validation, repair, tests and integration; a bounded independent implementation task updates source-file guards. A separate Astra High reviewer examines the complete final diff, contracts and test selection. Use Superpowers writing-plans / executing-plans, systematic debugging, TDD, independent review and verification. This file and tasks.md are the canonical execution ledger.

## Technical context and constitution check

C# / .NET 8 target, current SDK compiler, xUnit, file-backed JSON. Existing Spec Kit scaffolding and issue #1533 reused. No schema migration, player UI or actor identity change. Keep shared parser defaults, actor dictionaries, same-turn retained reads, pre-turn detection and progression fields. Only the identity layer requests ordinal skill-name sets. The unknown/missing backstops remain; remove only a precisely matching legacy issue when a confusable classification replaces it. Independent invalid fields and other references remain errors. All verification uses impact-selected categories; no full-suite or aggregate run.

## Design and interfaces

1. Add optional `StringComparer? skillNameComparer = null` to the player/NPC skill-name parsers and current-player-name reader in ValidationService.AcceptedTurnAndInkFeathers.cs. Default remains OrdinalIgnoreCase, including all existing consumers; exact identity calls pass Ordinal. Document all changed signatures. Actor dictionaries retain OrdinalIgnoreCase.
2. In ValidationService.SkillReferenceIdentity.cs preserve exact canonical names and indexed mastery references. Keep pre-turn/new-skill comparison case-insensitive. Classify confusable coverage against canonical skill sets before proposing a target; ambiguous matches have no literal target. Replace only the same-path unknown issue, or same actor/section/skill missing-initialization issue. Initialization issues point at the real mastery entry's `.skillName`, expected is the canonical name (or explicitly unresolved on ambiguity), actual is the actual reference. Exact coverage wins regardless of entry order.
3. In GameEngine.SkillReferenceIdentityRepair.cs preserve distinct correction coordinates, include repair hints and avoid treating descriptive ambiguity text as a literal name. In ambiguous cases the GM must stop rather than guess. Mixed truly unknown references continue through their original packet. Retain progression and actor fields.
4. Update source guards to read the complete ordered ValidationAndRepair partial family for methods moved by the original PR. Do not concatenate unrelated partials into a method body or weaken assertions. Guard families and any integration source-inspection cases are included in verification.
5. Register all new tests in a focused mortal-skill-reference-identity category. Add a bounded moved-source-guards category for the affected tests instead of running unrelated domains. Update tests/selection.json with reasons for identity, validation-pipeline and source guards. Run discovery audit and inspect exact category plans.
6. Rules/Block_7.txt already requires exact names. Add a bounded GM worked example for the repaired contract, including multi-NPC corrections and ambiguity refusal, register it in the example manifest if it has an executable fixture, and add a documentation coverage assertion. No afterlife contract change.

## TDD steps and expected observations

- T011: Add phase-level tests in MortalSkillReferenceAuthorityTests.cs for Unicode versus unknown mixed payloads, canonical ambiguity/exact names, and player/NPC active/passive coverage order. Add repair tests for actual validator output, repeated actor/skill coordinates and ambiguity in GameEngineTurnLifecycleTests.SkillReferenceIdentity.cs. Before fixes: failures reproduce the review.
- T012: Preserve ordinal names and suppress only precisely replaced legacy codes; rerun identity tests, expected all green.
- T013: Correct packet coordinates/retention and ambiguity instructions; actual validator -> request tests green, including unrelated progression fields unchanged.
- T014: Update all source guards for the partial split, retaining their assertions. Original five failures already RED at reviewed SHA; affected guards all green afterward.
- T015: Register category owners and current selection, verify discovery and plans. No orphan new tests; all new scenarios selected.
- T016: Synchronize contract/research/data-model/quickstart and add GM example/coverage. Inspect XML comments in the entire diff and build with GenerateDocumentationFile=true.
- T017: Commit/push WIP before long tests and review. Run selected categories once after integration; inspect completion, failures, skips and owned cleanup. Independently review the coherent block and address validated findings, rerunning only affected checks.
- T018: Update task evidence, push to PR, verify remote SHA and restore changed sources from GitHub into a new empty directory. Report results and limitations; do not merge.

## Consistency analysis (Spec Kit analyze, pre-implementation)

2026-11-01: correction criteria 1 -> T011/T012/T013; 2/3 -> T011/T012; 4 -> T011/T013; 5 -> T014; 6 -> T015; 7 -> T016/T017/T018. Preserved contracts map to parser-default regressions, indexed actor tests, progression assertions and source guards. No scope or contract changes from approved correction revision 1. Old claims of byte-identical legacy behavior/no Unicode double-reporting were disproved by review and are superseded here. No unresolved HIGH/CRITICAL planning inconsistency. Source-guard work can run independently of identity code; catalog edits remain with the parent.

## Review focus

Same-turn retained current reads; ordinal canonical ambiguity survives parsing; Unicode replacement cannot drop another issue; two NPCs or multiple skills never collapse by path; exact coverage is order-independent; ambiguous repair never offers a literal target; source guards still verify method-local ordering; selection includes all changed contracts. Later parts T101-T107 remain deferred.

## Checkpoint / evidence

- User approved the exact specification revision at commit `14014224584cab3ec3b3a10f6632ebbe607d91f8`. Remote SHA verified; clean GitHub restore matched spec SHA256 AAED1E1147C54738D2FC0053B4A38B0B41596390D99835A1CC9CD9AB6B48D8E3 before approval status metadata update.
- Reviewed SHA 673c5317 Windows: test projects built with XML docs; new tests 19/19 + 1/1 passed; validation-pipeline 56 passed / 5 failed of 61 executed (228 planned, stopped). Five source guards pass against base source using identical test code. Runtime probes reproduced ambiguity loss, duplicate Unicode packets, order dependence and two NPC issues -> one correction.
- Implementation status: pending T011. Independent correction review: not yet run. Next: add regression tests and observe RED.

- T011 RED: category ran 4 integration cases: 1 passed / 3 failed as expected (Unicode packet duplication, lost correction, invented ambiguous target). The runner stopped before the unit descriptor; a bounded direct unit diagnosis then ran all 31 identity cases: 23 passed / 8 failed, confirming Unicode/missing diagnostics, canonical ambiguity, order dependence and coordinate loss. Initial concurrent-edit build saw the new shared source helper before its assembly was rebuilt; serialized fresh builds succeeded. No behavior fix was written until the expected failures were read.
- Ruling: Indexed mastery entries replace the need for optional comparers on mastery-name parsers; those two shared methods remain untouched. Only the player/NPC skill-name parsers and retained-current reader add a comparer, minimizing the changed surface.

- First integrated selection on ef496aa6: 292 planned, 282 executed (280 pass /2 fail), stopped before 10 Roslyn integration cases; cleanup complete. Identity 36/36 and validation-pipeline 228/228 passed. The two guard failures were missing specs/1113 and specs/1535 documents excluded from this review checkout's sparse patterns; those tracked directories are now materialized without code changes. Re-run the affected guard category after this environment correction.
- Independent Astra High review of ef496aa6 found one P2: exact mastery bound to a distinct canonical sibling was labelled an ambiguous correction, hiding/overriding missing initialization. Added 6 player/NPC active/passive cases; all 6 failed RED before the fix. Filter initialization candidates to genuinely non-exact canonical references; if none exist, preserve/create the same skill's legacy missing issue. Both exact sibling initializations are a positive control. Other review findings: none. Resolution review and fresh verification pending.
- Catalog discovery on ef496aa6 succeeded: 443 categories /11237 method-file identities, no execution, complete owned cleanup. Repeat discovery after adding the sibling tests; no new category is needed because the class wildcard already owns them.


## Final checkpoint — verified correction block (2026-11-01)

Implementation/test SHA: `8ac29afdb768d0e1d7d5e42ee7f89aebc891eeb7` on Windows x64, .NET SDK 10.0.401 targeting .NET 8, PowerShell 7.6.6. Source issue #1533, PR #1579. Primary implementation: parent gpt-6.1-sol; bounded T014 implementation: separate gpt-6.1-sol/high; independent whole-block review and focused P2 resolution: separate gpt-6-astra/high. One substantive reviewer finding (exact sibling binding) was fixed with 6 RED->GREEN cases plus a both-exact positive control; reviewer found no remaining actionable issues. Later T101-T107 and merging remain outside this block.

Commands/evidence:

- Fresh builds of both test projects: `dotnet build <project> --no-restore -p:GenerateDocumentationFile=true -p:NoWarn=1591`, both exit 0. XML comments in the diff were inspected. Existing compiler/platform warnings and cached NU1900 vulnerability-feed warnings remained; no build errors.
- `pwsh -NoProfile -File scripts/test-csharp.ps1 -SelectionFile tests/selection.json -NoBuild` at implementation SHA: identity 43/43, validation-pipeline 228/228 passed. It stopped on 2 source-guard missing-document errors in the temporary sparse checkout (not assertion failures), with 299 planned /289 executed /287 passed /2 failed, complete owned cleanup. The complete set of spec-root and contract Markdown dependencies was then materialized from tracked Git objects; no product source changed.
- `pwsh -NoProfile -File scripts/test-csharp.ps1 -Category validation-repair-partial-source-guards -NoBuild`: 33/33 passed, 2/2 descriptors complete, 38.118 seconds, no skips/failures/timeouts/duplicates, owned cleanup complete. This includes all 23 unit source guards and all 10 Roslyn integration cases. Its five pipeline-overlap cases are not counted twice.
- Composed final intended selection: 299 unique test IDs/cases, all Passed, at the same implementation SHA. This is complete coverage across the selected runs, not a claim that the first combined invocation was green. Original five split-file regressions now pass; assertions remain unchanged.
- `pwsh -NoProfile -File scripts/test-csharp.ps1 -ValidateCatalog -NoBuild`: exit 0, 443 categories /11240 method-file identities, discovery only, 17.877 seconds, owned cleanup complete. New original and correction tests have owners; the actual execution plans selected the new scenarios.
- Recovery: fresh `git clone --filter=blob:none --depth 1 --no-checkout --single-branch --branch 1533-complete-skill-materialization` from GitHub into a new directory; sparse materialization of all 28 changed files plus active feature/instructions; HEAD matched implementation SHA; checkout clean; each changed file's blob hash matched. No local Git objects/source cache copied. This verifies recovery of the listed sources/instructions; it does not claim a fresh-environment build/test run.

Verification result directories (relative to the implementation checkout): `TestResults/test-categories/20261101-221843-441-9512-02e88728a6bd4b34924b46545957c87e-categories`, `20261101-222232-742-30384-eb7fb63302784cc497cbe228bef54ae4-categories`, and inventory `20261101-221920-750-732-123d27f87fb54baebce28430d5fa096c-categories`. Local output archive contains raw TRX/logs. This checkpoint preserves the substantive evidence in GitHub text.

Status: correction code verified and independently reviewed; final checkpoint publication and remote-ref verification follow this documentation commit. No open correction finding, no discarded behavior requirement, no deferred reviewer minor. No migration or later skill lifecycle contour is claimed complete. No measured cost savings are claimed; the observed one reviewer correction supports retaining independent review.

## Part 2 plan — T107 prose↔delta coupling (2026-11-02, branch `1533-prose-state-delta-coupling`)

**Base**: `7d978bdfaf616bc8487ed89b406a42068fca561d` (merge of PR #1579 into main), verified via `git ls-remote` after publishing the task branch. Work follows the same constitution: TDD, bounded categories, independent review by a separate agent that did not implement the block. User instruction 2026-11-02: push checkpoints freely, do not merge. Reviewer model/effort mapping to this environment is recorded in the final checkpoint; the configured reviewer is a separate fresh-context agent, not the implementer.

### Design

1. Rework only the skill-claim branch of `ValidateAcceptedTurnProseStateDeltasAsync` (ValidationService.AcceptedTurnAndInkFeathers.cs). Quest-clue branch, claim-detection heuristics, rationale escape hatch and codes stay unchanged.
2. Read the current mastery payload once per audit; reuse `ReadMasteryIdentityReferences(json, "skillMasteryChanges")` from the part-1 partial for exact-spelling indexed names (no second parse, no new JSON reader).
3. Build `MortalSkillReferenceAuthority` from the player's canonical exact skill names (active + passive, current and pre-turn retained reads, ordinal) — the same name source the part-1 player mastery identity check uses.
4. Claim for X satisfied iff an Exact-bound-to-X mastery entry exists with file-change evidence (existing `HasAcceptedTurnStateDeltaAsync` semantics), or X is newly present in post-turn skill name sets vs pre-turn snapshot. Confusable/ambiguous references never satisfy; they keep their part-1 diagnosis.
5. Issue `actual` lists the delta's real targets so repair stays narrow; code/filePath/severity unchanged.

### TDD steps and expected observations

- T107-B: add coupling tests to `AcceptedTurnNarrativePayloadValidationTests.cs` (already owned by the `console-explorer` class wildcard; no new category needed, selection reasons updated). Expected RED: wrong-target delta and confusable-only delta currently pass the file-level check.
- T107-C: implement per-skill coupling; identity and narrative-audit tests green; no part-1 behavior change.
- T107-D: sync the two launcher-script lines describing the codes with the narrowed semantics; update tests/selection.json; run ValidateCatalog.
- T107-E: run selected categories, independent review, checkpoint with SHA verification.

### Checkpoint ledger (part 2)

- 2026-11-02: branch `1533-prose-state-delta-coupling` created from verified base `7d978bdfaf616bc8487ed89b406a42068fca561d` and published; remote SHA matches base. Spec/plan/tasks revision 2 recorded (this text). No product code changed yet. Next: T107-B RED tests.
- 2026-11-02 (T107-B/C): RED->GREEN. 4 coupling tests in `AcceptedTurnNarrativePayloadValidationTests.cs` (wrong-target delta, confusable-only delta, exact per-skill delta, new-skill delta) failed RED, then implementation in `ValidationService.AcceptedTurnAndInkFeathers.cs` (per-skill `AcceptedTurnSkillDeltaContext`, claim detection before delta reads) turned them GREEN. Commits `783fb5360` (RED) and `68e64acb8` (GREEN). Codes and quest branch unchanged; `actual` names real delta targets.
- 2026-11-02 (T107-D): GM-facing doc sync — `BookOfEternityClient/Launcher/CLI_Launch_Script.md`, `Launcher/Generate_CLI_Launch_Script.ps1`, `TaskGuides/CLI_Step_Main.txt`, `Examples/E_CLI_Step_Main.txt`; new `tests/selection.json` (console-explorer, mortal-skill-reference-identity, documentation-examples). Commit `f39778aa3`.
- 2026-11-02 (T107-E, review round 1): independent review by a separate fresh-context agent (not the implementer) found one substantive defect: a carried-over mastery entry satisfied the claim when the file changed for an unrelated skill. Fixed with entry-level pre/post diff: with a validated pre-turn snapshot the entry must be new or changed; without a snapshot the documented weaker `filesModified` fallback applies. 2 new tests (carried-over rejected, changed entry accepted); class 14/14. Resolution review: defect closed, no regressions. Commit `64c09dc46`. Non-blocking reviewer note kept open at that point: byte comparison via `GetRawText()` is sensitive to lexical drift (reordered keys falsely satisfy the claim).
- 2026-11-02 (T107-E, drift fix): `ReadMasteryEntriesByExactName` now parses via `JsonNode` and stores `IReadOnlyDictionary<string, JsonNode>`; both comparisons use `JsonNode.DeepEquals`, so purely lexical serialization drift (reordered keys, whitespace) no longer counts as a change. New `InvalidOperationException` catch degrades duplicate-key payloads to the weaker fallback instead of crashing. New RED->GREEN test `...RejectsSkillClaimWhenCarriedOverEntryOnlyDriftsLexically` (RED confirmed before the fix). FR-101(a) reworded byte-different -> structurally different under `JsonNode.DeepEquals`; SatisfiesSkillClaim XML doc updated. Resolution review by the same independent reviewer: RESOLVED WITH NOTES (all notes non-blocking: double parse of the mastery payload, lumped InvalidOperationException catch, one extra degenerate fail-open case for duplicate-keyed pre-turn snapshots). No new defects.

### Part 2 verification evidence (2026-11-02)

Environment: Windows x64, .NET SDK 10.0.401 targeting .NET 8, PowerShell 7.6.6. The agent shell lacks `ProgramFiles`/`ProgramData`/`CommonProgramFiles*`; a wrapper restores them before every `dotnet`/`test-csharp.ps1` invocation, and `dotnet build-server shutdown` was required once to clear stale MSBuild nodes with the old environment. Categories were run one at a time (comma lists break nested invocation).

- Test class `AcceptedTurnNarrativePayloadValidationTests`: 15/15 passed (drift fix included).
- Category `mortal-skill-reference-identity`: 43/43 passed (`TestResults/test-categories/20261102-071702-484-...`).
- Category `console-explorer`: 260/261; the single failure is `ExplorerModeCommandTests.TryProcessCommand_SystemMods_RendersDetailLoopWithoutHiddenErrors`, proven pre-existing on clean main via a separate worktree (`20261102-071821-312-...`).
- Category `documentation-examples`: 171/172; the single failure is `RepositoryEncodingScannerTests.UserFacingSourcesAndFixtures_MustNotContainCommonMojibakeMarkers` (legal U+FFFD literal in TextComposer.cs, blob identical to origin/main). Descriptor `documentation-examples-integration-001` exits 1 with zero executed tests; identical before and after this change (logs `20261102-022140`, `20261102-072005`, `20261102-072212`), so it is not caused by this block. Both are pre-existing main issues tracked as separate findings.
- Doc-guard `PromptDocumentationCoverageTests.AcceptedTurnProseStateDeltaContract_IsDocumentedForGmAndLiveTests`: 1/1 passed after the FR-101(a) rewording.

Reviewer mapping note: the configured independent reviewer in this environment is a separate fresh-context subagent (parent = implementer, reviewer = distinct agent instance per the user's approved Sol/Astra-style scheme); it is a Kimi subagent, not an external model.
