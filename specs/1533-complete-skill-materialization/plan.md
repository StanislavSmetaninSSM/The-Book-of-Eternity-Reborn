# Implementation Plan: #1533 / PR #1579 review corrections

**Branch**: `1533-complete-skill-materialization` | **Date**: 2026-11-01
**Issue**: https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/1533
**Spec**: [spec.md](spec.md), correction revision 1 approved by user on 2026-11-01.

## Current publication reconciliation (2026-11-12)

Issue #1533; branch `1533-exact-skill-state-duplicates`. Read-only GitHub
branch verification confirms **`2cc3f4bab10cb97be73698821a31e255fc29b2db`**,
parent `baeb3e6fd11f5f61481eccf850fa5a812dc0c590`, tree
`d1150b5c33a0e417193aaa070d0409b796cb8283`.
[Published checkpoint](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/commit/2cc3f4bab10cb97be73698821a31e255fc29b2db)
records the authorized preservation of local source checkpoint `47979f0`.
The local source commit IDs in the dated entries below were not reproduced in
remote history; use this published commit for source recovery.

This supersedes the historical publication-blocked/local-only statements in
Parts 5–9 and research.md. Those statements describe their original dates, not
an outstanding authorization or publication requirement. Their recorded Linux
220/220 and later QTE 16/16 results remain separate historical runs at their
stated revisions; no tests, build, hosted CI or Windows verification ran during
this reconciliation. Publication is not whole-issue acceptance.

Current recovery check: the prior checkout is absent in the selected workspace.
A new single-branch clone through the configured Git mirror failed because it
requested an unavailable username. The GitHub connector successfully read the
exact-commit task documents into a fresh directory; their Git blob hashes were
checked against GitHub. This proves recovery of plan.md, tasks.md, research.md,
ValidationService.NpcSkillMasteryOwnership.cs, MortalNpcSkillMasteryOwnershipTests.cs
and MortalNpcSkillMasteryOwnershipPhaseTests.cs only, not a complete checkout or
new executable environment.

Scope of this correction: publication status only, with broader T102–T106
headings still unchecked. Original-criteria reconciliation in research.md stays
in force: no new concrete runtime defect is established. Remaining evidence
questions are route-to-common-gate coverage, non-mastery reference closure and
console/browser mechanics/privacy parity. They are not new-system requests.
Legacy-save migration is excluded by the current spec/guardrail; Windows
PowerShell 5.1 remains unrun. Next step: inspect existing route and consumer
coverage against those three questions before adding any code or tests.

Verification for this documentation-only block: exact-commit API reads, Git
blob SHA verification and whitespace/diff review; independent review is recorded
in the delivery report. No production/test/GM contract or category selection
changes, therefore no C# rerun. Optimization: reuse already fetched immutable
source text; no runtime/cache work or performance claim. No merge or issue
closure.

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

Same-turn retained current reads; ordinal canonical ambiguity survives parsing; Unicode replacement cannot drop another issue; two NPCs or multiple skills never collapse by path; exact coverage is order-independent; ambiguous repair never offers a literal target; source guards still verify method-local ordering; selection includes all changed contracts. T107 completed in Part 2 (merged via PR #1583); T101 is active as Part 3 on branch `1533-skill-schema-consolidation`; T102-T106 remain deferred.

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

## Part 3 plan — T101 canonical skill profile schema consolidation (2026-11-02, branch `1533-skill-schema-consolidation`)

**Base**: `6ef42415ef2d16d4b54a62da380e87da71913ae2` (merge of PR #1583 into main), verified via `git ls-remote` after publishing the task branch. Same constitution: TDD, bounded categories, independent review by a separate fresh-context agent. Push checkpoints freely; do not merge.

### Design

1. New `MortalSkillProfileContract` (Services/) owns the complete active/passive profile validation and the canonical enum sets (skill `rarity`: Common|Uncommon|Rare|Epic|Legendary; passive `type`: the existing five values, single source). `ValidateActiveSkillObject` / `ValidatePassiveSkillObject` delegate to it; every diagnostic keeps its existing code/section/severity/expected/actual/repairHint byte-compatible (FR-108). `NpcCoreChangesContract.AllowedPassiveSkillTypes` re-points to the shared set (dedupe).
2. New `rarity` enum membership check on both profiles, added inside the shared contract (new code `skill_invalid_rarity`-family; was: non-empty string only).
3. Constructor gate: `EffectAcceptedTurnInputComposer.ComposeSkillAcceptedRootCore` validates every skill object this turn's accepted changes write (new entries and full-object updates; carried-over untouched entries are out of the gate's scope per FR-107) through the shared contract, mapping violations into the bounded `effect_skill_composition_*` issue channel with a new dedicated code; identity/removal semantics untouched (FR-107).
4. Passive `group` stays free-form (spec Part 3 recorded decision); its non-empty-string check moves into the shared contract unchanged.

### TDD steps and expected observations

- T101-B [RED]: tests in `MortalSkillReferenceAuthorityTests`-adjacent composition/validation classes (exact classes fixed during RED; category ownership via existing wildcards or catalog update): (a) composition materializes an active skill missing `combatEffect` -> bounded issue (today: silent); (b) unknown `rarity` rejected on player delta, NPC delta and composition with one shared code; (c) unknown passive `type` diagnosed identically on player and NPC surfaces; (d) positive guards: free-form `group` accepted, all five rarity/type values accepted on both actor kinds; (e) byte-compatibility: an already-diagnosed payload produces the same codes as before the refactor.
- T101-C [GREEN]: extract the shared contract, dedupe enums, add the rarity check, wire the composer gate.
- T101-D: GM-facing sync — the compact skill template in `game_master_daemon.ps1`, `TaskGuides/CLI_Step_Main.txt`, launcher scripts and `Examples/E_CLI_Step_Main.txt` gain the canonical rarity ladder sentence (group stays free-form, stated explicitly); spec/contracts sync; tests/selection.json update; ValidateCatalog.
- T101-E: bounded verification, independent review, checkpoint with remote SHA verification.

### Checkpoint ledger (part 2)

- 2026-11-02: branch `1533-prose-state-delta-coupling` created from verified base `7d978bdfaf616bc8487ed89b406a42068fca561d` and published; remote SHA matches base. Spec/plan/tasks revision 2 recorded (this text). No product code changed yet. Next: T107-B RED tests.
- 2026-11-02 (T107-B/C): RED->GREEN. 4 coupling tests in `AcceptedTurnNarrativePayloadValidationTests.cs` (wrong-target delta, confusable-only delta, exact per-skill delta, new-skill delta) failed RED, then implementation in `ValidationService.AcceptedTurnAndInkFeathers.cs` (per-skill `AcceptedTurnSkillDeltaContext`, claim detection before delta reads) turned them GREEN. Commits `783fb5360` (RED) and `68e64acb8` (GREEN). Codes and quest branch unchanged; `actual` names real delta targets.
- 2026-11-02 (T107-D): GM-facing doc sync — `BookOfEternityClient/Launcher/CLI_Launch_Script.md`, `Launcher/Generate_CLI_Launch_Script.ps1`, `TaskGuides/CLI_Step_Main.txt`, `Examples/E_CLI_Step_Main.txt`; new `tests/selection.json` (console-explorer, mortal-skill-reference-identity, documentation-examples). Commit `f39778aa3`.
- 2026-11-02 (T107-E, review round 1): independent review by a separate fresh-context agent (not the implementer) found one substantive defect: a carried-over mastery entry satisfied the claim when the file changed for an unrelated skill. Fixed with entry-level pre/post diff: with a validated pre-turn snapshot the entry must be new or changed; without a snapshot the documented weaker `filesModified` fallback applies. 2 new tests (carried-over rejected, changed entry accepted); class 14/14. Resolution review: defect closed, no regressions. Commit `64c09dc46`. Non-blocking reviewer note kept open at that point: byte comparison via `GetRawText()` is sensitive to lexical drift (reordered keys falsely satisfy the claim).
- 2026-11-02 (T107-E, drift fix): `ReadMasteryEntriesByExactName` now parses via `JsonNode` and stores `IReadOnlyDictionary<string, JsonNode>`; both comparisons use `JsonNode.DeepEquals`, so purely lexical serialization drift (reordered keys, whitespace) no longer counts as a change. New `InvalidOperationException` catch degrades duplicate-key payloads to the weaker fallback instead of crashing. New RED->GREEN test `...RejectsSkillClaimWhenCarriedOverEntryOnlyDriftsLexically` (RED confirmed before the fix). FR-101(a) reworded byte-different -> structurally different under `JsonNode.DeepEquals`; SatisfiesSkillClaim XML doc updated. Resolution review by the same independent reviewer: RESOLVED WITH NOTES (all notes non-blocking: double parse of the mastery payload, lumped InvalidOperationException catch, one extra degenerate fail-open case for duplicate-keyed pre-turn snapshots). No new defects.
- 2026-11-02 (T107-F..I, PR #1583 external review round): external reviewer (Codex) reported 3 findings on `0cb4d736`, all reproduced and confirmed before fixing. (1) P1: progression of an existing **passive** skill was rejected — passive skills advance via progression fields inside their own `passiveSkillChanges[]` entry (`masteryLevel`, `currentMasteryLevel`, `currentMasteryProgress`, `masteryProgressNeeded`), and writing them to `skillMasteryChanges` correctly yields part-1 `skill_mastery_unknown_active_skill`; the coupling audit now accepts entry-level progression changes (`JsonNode.DeepEquals` per progression field vs the validated pre-turn counterpart; `skills_passive.json` file-changed is the documented weaker fallback only when no snapshot exists). (2) P2: duplicate JSON keys crashed the audit via lazy `ArgumentException` from `JsonObject` dictionary initialization; `ReadSkillEntriesByExactName` (renamed from `ReadMasteryEntriesByExactName`, now parameterized by section) returns `Missing/Unreadable/Ok`, catches `ArgumentException` alongside `JsonException`/`InvalidOperationException`, and an **unreadable** current file or snapshot fails closed with a repair diagnostic — no fallback, unlike the documented absent-snapshot case. (3) P2: canonical name sets were collapsed with `OrdinalIgnoreCase`, dropping `FIREBALL` when `Fireball` existed; parsing now preserves ordinal case differences, and claim detection fails closed across ordinal case-colliding canonical names — an exact-case prose mention claims only that member, an unmatched-case mention claims the whole colliding group. Six RED-first tests (4 failed RED exactly on the findings, 2 characterization guards passed immediately), then GREEN. Spec FR-101 extended (paths (c)/(d), unreadable fail-closed, ordinal case-collision rule) with acceptance scenarios 6-8; GM docs re-synced on all four surfaces. Class `AcceptedTurnNarrativePayloadValidationTests` 21/21 + doc-guard 1/1; `mortal-skill-reference-identity` 43/43; `console-explorer` 266/267 (sole failure = pre-existing #1581 `TryProcessCommand_SystemMods_RendersDetailLoopWithoutHiddenErrors`); `documentation-examples` 171/172 (sole failure = pre-existing #1580 mojibake U+FFFD in TextComposer.cs; descriptor `documentation-examples-integration-001` still exit 1 with zero tests = pre-existing #1582); `-ValidateCatalog` exit 0, the 6 new tests are owned by the existing `console-explorer` wildcard `AcceptedTurnNarrativePayloadValidationTests.*` (category grew 261 -> 267 cases).
- 2026-11-02 (T107-I, resolution review): the same independent reviewer (separate fresh-context subagent, not an external model) verdicted all three external findings closed, with one residual low-severity defect: the no-snapshot mastery weak fallback returned true without checking the current file's readability, contradicting FR-101's "snapshot **or current file** unreadable -> fail closed" and the sibling passive branch. Fixed by requiring `CurrentMastery.State == Ok` + entry presence in the fallback (mirrors the passive branch); new test `...RejectsSkillClaimWhenCurrentMasteryUnreadableWithoutSnapshot` (RED confirmed on the pre-fix branch, GREEN after). Re-runs after the fix: narrative class 22/22; `mortal-skill-reference-identity` 43/43; `console-explorer` 267/268 (sole failure unchanged = pre-existing #1581). Non-blocking reviewer notes kept open: progression-field removal counts as a delta (degenerate edit); two new private methods carry no XML doc comments. Commit `0ce848787`, remote SHA verified.
- 2026-11-02 (T107-J..M, PR #1583 external review round 2): external reviewer (Codex) confirmed all three round-1 repros fixed on `0ce848787` and reported 2 new P2 findings, both reproduced and confirmed before fixing. (1) A corrupted pre-turn snapshot could still satisfy the audit: the snapshot reader collapsed blank/whitespace payloads to null (treated as absent -> weak fallback unlocked), and `CollectNewSkillNamesThisTurnAsync` parsed names with a swallow-all parser, so a malformed snapshot (`{`) yielded an empty name set and made existing skills look "new this turn". Fixed at both layers: `ReadValidatedPendingTurnSnapshotFileAsync` gained an opt-out `treatBlankAsMissing` parameter (default true preserves every existing consumer); the coupling audit reads pre-turn payloads through the new `ReadCouplingPreTurnSnapshotAsync` with blank preserved -> `ReadSkillEntriesByExactName` classifies blank as Unreadable -> fail closed, no fallback; new-skill detection now uses the same tri-state entry reads and never diffs against an unparseable payload; unreadable pre-turn passive snapshot got its own repair diagnostic line. (2) An exact-case mention suppressed ambiguous mentions in other sentences: `CollectClaimedSkillNames` resolved collision groups once for the whole text. Fixed per mention: each sentence spelling group members in exact canonical case claims only those members; a sentence matching only case-insensitively claims the whole group. Four RED-first tests (3 failed RED exactly on the findings, 1 positive guard passed immediately); GM docs gained the exact-canonical-casing sentence on all four surfaces; spec FR-101 + scenarios 9-10. Verification: narrative class 26/26; doc-guard 1/1; `mortal-skill-reference-identity` 43/43; `console-explorer` 271/272 (sole failure = pre-existing #1581; the runner's fail-fast on that failure aborted shard unit-005 before it started — its 42 planned cases were run manually, all green); `documentation-examples` 171/172 (sole failure = pre-existing #1580 mojibake U+FFFD in TextComposer.cs only — edited docs clean; descriptor integration-001 again aborted by fail-fast = pre-existing #1582 pattern). Commit `1d36beea9`, remote SHA verified.
- 2026-11-02 (T107-N..Q, proactive adversarial round): process change — before external re-review, the independent reviewer (separate fresh-context subagent) was tasked to BREAK the change with executable probes instead of confirming fixes. It found 3 confirmed defects: (D1, fail-open) `HasPassiveProgressionDelta` counted a dropped/mis-cased progression field as progression (`DeepEquals(null, node)`); (D2, diagnostic honesty) `DescribeActualForMissingClaim` reported a deleted mastery file as "skill state unchanged" and a removed mastery entry as "carried over unchanged"; (D3, degenerate input) a whitespace-padded canonical name defeated every satisfaction path because references were trimmed but the claimed name was not. Unverified suspicions (substring exact-mentions without token boundaries; blank-collapsing reader inside `HasAcceptedTurnStateDeltaAsync` feeding only the file-changed flag) recorded as non-blocking notes. Five RED-first tests (exactly 5 RED failures), then GREEN: progression counts only fields still present in the current entry; `actual` names deleted file/removed entry explicitly; `ParsePlayerSkillNames` trims names consistently. No GM-doc change needed (harness-internal semantics; recorded as the no-update rationale). Verification: narrative class 31/31; doc-guard 1/1; `mortal-skill-reference-identity` 43/43; `console-explorer` all 277 planned cases accounted (runner executed 90 incl. pre-existing #1581 fail-fast abort; the 187 unit-selector cases were run manually in one filter — all green); `documentation-examples` 171/172 (pre-existing #1580 only; #1582 pattern unchanged).
- 2026-11-02 (T107-R, PR #1583 external review round 3): external reviewer (Codex) confirmed the adversarial fixes on `e374204e` and reported one P2: the D3 trim was applied only to `ParsePlayerSkillNames`, while `ParsePlayerSkillMasteryNames` kept untrimmed names — a new active skill and its mastery reference padded with the same trailing whitespace produced a false `player_active_skill_missing_mastery_initialization` while the prose check passed. Finding reproduced and confirmed by code inspection (mastery parser `result.Add(skillName)` vs skill parser `result.Add(skillName.Trim())`) and a RED-first test `SkillContractConsistency_PaddedNewSkillAndMastery_DoNotProduceFalseMissingInitialization` in `MortalSkillReferenceAuthorityTests.ReviewCorrections.cs` (failed RED exactly on the finding, 1/1). GREEN: mastery parser now trims too; NPC-side parsers (`ParseNpcSkillChangesByActor`/`ParseNpcSkillMasteryByActor`) verified symmetric — both untrimmed and only compared against each other, so untouched. Editorial: three single-line XML `<param>` blocks on `ReadValidatedPendingTurnSnapshotFileAsync` reformatted to the file's multi-line style. Verification: padded test 1/1; class `MortalSkillReferenceAuthorityTests` 40/40; narrative class + doc-guard 32/32 (31 + 1); `mortal-skill-reference-identity` 44/44 (category grew 43 -> 44, the new test is owned by the existing wildcard); `console-explorer` all 277 planned cases accounted (runner executed 90 incl. pre-existing #1581 fail-fast abort; the 187 unit-selector cases run manually in one combined filter — all green); `documentation-examples` 171/172 (sole failure = pre-existing #1580 mojibake U+FFFD in TextComposer.cs only; descriptor integration-001 zero-test abort = pre-existing #1582 pattern). No GM-doc change needed (consistency fix inside a part-1 surface, semantics of trimming already documented; recorded as the no-update rationale).

### Checkpoint ledger (part 3)

- 2026-11-02: branch `1533-skill-schema-consolidation` created from verified base `6ef42415ef2d16d4b54a62da380e87da71913ae2` (merge of PR #1583 into main) and published; remote SHA matches base. Spec/plan/tasks revision for T101 recorded (spec Part 3 slice, this plan section, tasks Part 3). No product code changed yet. Note: the local `origin/main` ref was stale after the PR merge (fetch updated only FETCH_HEAD); fixed with an explicit refspec fetch before branching. Next: T101-B RED tests.
- 2026-11-02 (T101-B, RED): commit `462752692`. Two new partial test files: `MortalSkillReferenceAuthorityTests.ProfileSchema.cs` (unit, player/NPC delta paths) and `EffectMaterializationValidationTests.SkillProfileGate.cs` (integration, composition gate). RED confirmed exactly as designed: 6 failures (3 delta-path `skill_invalid_rarity` with empty issue collections, 3 composition-gate cases), 7 guards passing immediately (free-form group, 5 canonical rarities, legacy mastery code byte-compat, carried-over scope).
- 2026-11-02 (T101-C, GREEN): new `BookOfEternityClient/Services/MortalSkillProfileContract.cs` holds the single `AllowedSkillRarities` ladder (5 values, OrdinalIgnoreCase) and `AllowedPassiveSkillTypes`; `ValidateSkillRarity` emits `skill_invalid_rarity` on both delta validators (`ValidateActiveSkillObject`/`ValidatePassiveSkillObject`, now internal and shared). Enum dedupe: the copies in `ValidationService.PrivateImplementation.cs` and `NpcCoreChangesContract.cs` were removed/aliased to the contract. Composition gate: `ValidateAcceptedSkillComposition` passes `validateProfiles: true` into `ComposeSkillAcceptedRootCore`; every this-turn write (new entry or full-object update) is validated through the shared profile validators and each violation is folded into one `effect_skill_composition_invalid_profile` issue; carried-over pre-turn entries stay out of scope. Design correction during GREEN: the gate is scoped to the validation entry point only — the throwing `ComposeSkillAcceptedRoot` path (normalizer, same-turn export collection) keeps pre-change behavior, because it operates on already-validated state and existing minimal same-turn patch fixtures (proved by `SameTurnSkillPatchPreservesUntouchedExactSourceAuthority`). RED-test harness fix: the two new-write gate tests needed a baseline `CaptureValidatedPendingSnapshotAsync` because raw validation returns early without a usable snapshot. tests/categories.json: 4 new gate tests added to `effects-materialization` (unit side covered by existing wildcards); ValidateCatalog exit 0.
- 2026-11-02 (T101-D, GM doc sync): canonical rarity ladder + passive type enum + explicitly free-form `group` sentence added to the compact skill template in `game_master_daemon.ps1`, `TaskGuides/CLI_Step_Main.txt`, `Launcher/CLI_Launch_Script.md`, `Launcher/Generate_CLI_Launch_Script.ps1` and `Examples/E_CLI_Step_Main.txt`; new doc-guard `PromptDocumentationCoverageTests.SkillProfileSchemaEnums_AreDocumentedForGm` pins all five surfaces (owned by the existing `documentation-examples` wildcard); tests/selection.json rewritten for T101.
- 2026-11-02 (T101 verification, pre-review): fresh builds 0 errors. `mortal-skill-reference-identity` 54/54 exit 0 (run twice, incl. post-refactor rerun). `effects-materialization` fully accounted via manual shards (runner wall time exceeds the shell cap): integration 274/274 (001: 34+26+22, 002: 23+30+33, 003: 22+47+25, 004: 12), unit 176/176 (005: 157, 006: 19). `canonical-normalization-mortal`: 53/54 integration + 2/2 unit — sole failure `CanonicalStateNormalizerWoundTests.CompleteWoundPlan_ReadBackRejectsIdentityDriftAfterPublication` confirmed PRE-EXISTING (reproduces with all T101 changes stashed, production code == main); tracked as new issue #1585, unrelated to skills. `npc-social-trade` affected surface: `NpcCoreChangesTests.*` 91/91. `console-explorer` fully accounted: integration 20+31+20 (sole failure = pre-existing #1581 `SystemMods_RendersDetailLoopWithoutHiddenErrors`), unit 78+62+47 all green. `documentation-examples`: integration 30+29 all green; unit 60+67+45 (sole failure = pre-existing #1580 mojibake scanner). Next: independent adversarial review, then commit+push with remote SHA verification.
- 2026-11-02 (T101-E, review + resolution): independent adversarial review (fresh-context subagent, executable probes) returned NOT RESOLVED with one confirmed P1: the GREEN commit had stripped the UTF-8 BOM from `game_master_daemon.ps1`; the file is launched by Windows PowerShell 5.1, which decodes BOM-less .ps1 as ANSI, silently corrupting 90 Cyrillic literals (realm-name matching, GM scaffold labels, error sniffing). Reproduced under real powershell.exe 5.1. Fixed in `32124340c` (BOM restored; diff vs base is again exactly the one template line; PSParser parse OK; doc-guard 1/1). Encoding sweep over all 18 touched files: no other BOM/line-ending regressions. Resolution round verdict: RESOLVED, no remaining confirmed defects. Non-blocking notes recorded: N1 theoretical programmatic-candidate crash in the gate (no production caller); N2 legacy off-ladder rarity now rejected on delta surfaces (pre-release, no migration per guardrail; release-note candidate); N3 doc-guard tokens partly pre-exist on Examples/daemon surfaces; N4 folded gate issues re-path to the candidate root; N5 pre-existing `skills`-shape path labeling. Remote SHA verified at each push (9ecae843b, 32124340c). T101 complete; next contours T102-T106.

### Part 2 verification evidence (2026-11-02)

Environment: Windows x64, .NET SDK 10.0.401 targeting .NET 8, PowerShell 7.6.6. The agent shell lacks `ProgramFiles`/`ProgramData`/`CommonProgramFiles*`; a wrapper restores them before every `dotnet`/`test-csharp.ps1` invocation, and `dotnet build-server shutdown` was required once to clear stale MSBuild nodes with the old environment. Categories were run one at a time (comma lists break nested invocation).

- Test class `AcceptedTurnNarrativePayloadValidationTests`: 15/15 passed (drift fix included).
- Category `mortal-skill-reference-identity`: 43/43 passed (`TestResults/test-categories/20261102-071702-484-...`).
- Category `console-explorer`: 260/261; the single failure is `ExplorerModeCommandTests.TryProcessCommand_SystemMods_RendersDetailLoopWithoutHiddenErrors`, proven pre-existing on clean main via a separate worktree (`20261102-071821-312-...`).
- Category `documentation-examples`: 171/172; the single failure is `RepositoryEncodingScannerTests.UserFacingSourcesAndFixtures_MustNotContainCommonMojibakeMarkers` (legal U+FFFD literal in TextComposer.cs, blob identical to origin/main). Descriptor `documentation-examples-integration-001` exits 1 with zero executed tests; identical before and after this change (logs `20261102-022140`, `20261102-072005`, `20261102-072212`), so it is not caused by this block. Both are pre-existing main issues tracked as separate findings.
- Doc-guard `PromptDocumentationCoverageTests.AcceptedTurnProseStateDeltaContract_IsDocumentedForGmAndLiveTests`: 1/1 passed after the FR-101(a) rewording.

Reviewer mapping note: the configured independent reviewer in this environment is a separate fresh-context subagent (parent = implementer, reviewer = distinct agent instance per the user's approved Sol/Astra-style scheme); it is a Kimi subagent, not an external model.

### T101 review correction plan (2026-11-10, PR #1586)

Base is verified remote merge `d233303036a4a81996077e781d5e774fc613c6ef`
(original profile branch plus main `604a722d`); parent owns remote publication.
The correction stays within T101, not a new contour.

- RED: direct typed production tests reproduce duplicate NPC Fate Card passive-type
  diagnostics, the omitted NPC composition gate, and lost `structuredBonuses` path.
  Cover actor ownership, UpdateNPCs, full updates, unchanged carry-over and positive enums.
- GREEN: move full active/passive algorithms to MortalSkillProfileContract, retain
  generic shape/combat/effect validation utilities and thin existing service wrappers.
  NPC reward validation keeps only closed-member restrictions; shared rules run once
  through the existing production Fate Card callback. Preserve independent NPC-only
  restrictions, and existing individual diagnostic fields, except redundant reports.
- Gate actor-local new/changed profiles in accepted npc_core canonical arrays,
  UpdateNPCs and NPCCoreChanges reward additions. Compare exact owner identity and
  complete structural profile data with the pre-turn root. No global name matching,
  permission substitution, mutation, or validation of untouched carry-over.
- Preserve the original profile field path and diagnostic details while mapping the
  composition code/section. Keep normalizer's non-validating composer unchanged.
- Verification: small mortal-skill-profile-contract category for direct regressions
  and real raw-validation skill tests; existing identity category for delta parity;
  split NPC core category if needed. Catalog discovery, bounded builds and runs only.
  Parent coordinates independent review and remote preservation before long runs.

Optimization assessment: use the already parsed pre-turn/current source roots in
memory; no extra file reads or new cache. Full profile validation is cheap and runs
once per changed object. Exact unchanged owner/profile comparisons avoid carry-over
revalidation; no cached authority or success crosses operation boundaries. Mutable
fixtures are per test, pure typed tests need no GameEngine or filesystem. Real raw
snapshot integration remains covered. No runtime speedup claim without measurement.

T101-F RED evidence: fresh merged-tree unit build succeeded (0 errors). Direct
MortalSkillProfileContractTests ran 16 cases: 9 expected failures, 7 positive
controls passed, 0 skipped, 233 ms test duration. Failures exactly reproduce the
three findings and NPC new/update/passive/owner/reward routes. The first PlanOnly
was attempted before the fresh test build finished and correctly rejected a stale
selector (0 tests, not RED); rerun required after both fresh builds. Integration
RED build also succeeded with 0 errors. Raw diagnostics stay outside checkout in
/tmp/pr1586-profile-* and sibling pr1586-profile-*.log files.

Independent review found one stricter NPC-only acceptance boundary omitted during
initial deduplication: shared combat validation treats absent effects as optional.
Causal RED executed the checked-in typed test method through a tiny binary-reference
runner: missing_effects failed with CanApply=true, while empty_effects,
missing_action_name, missing_activation and false_activation correctly rejected.
Restore only the previously stricter NPC active-reward shape, keeping original
npc_core_changes_fate_card_invalid payload for the absent-field gap. Empty/invalid
arrays and canonically required names retain shared diagnostics without duplication.

### T101-F final verification and review checkpoint (2026-11-10)

Source `980248f2c409911543c093ff5ac99ebe06876dca`, clean tree
`5edceaa9b9a1593442086c651754735bcc8a667a`; coordinator verified equivalent
remote code `413658d3`. Final docs-only publication/recovery remains coordinator-owned.
Linux x64, SDK 10.0.402 / .NET 8 / PowerShell 7.6.8. Frozen-source fresh builds
`dotnet build <unit/integration.csproj> --no-restore -m:1`: both 0 errors,
2m35.17s and 1m31.30s. Warnings recorded, including one xUnit2031 test-style warning.
Actual typed NPC boundary checks passed 6/6 on production DLL SHA-256
`704c303647dfaaea2ec664c52790eb37016d4d2359a1baf6041b25bc6b0df697`.

Commands through `pwsh -NoProfile -File scripts/test-csharp.ps1`:

- `-SelectionFile tests/selection.json -PlanOnly -NoBuild`: 186 cases / 5 descriptors,
  valid planning only, 15.70s.
- `-Category mortal-skill-profile-contract -NoBuild`: 41/41 (29 unit + 12 real
  raw-validation/composition/normalization integration), 2/2 descriptors, 24.55s.
- `-Category mortal-skill-reference-identity -NoBuild`: 54/54, 2/2 descriptors, 27.01s.
- `-Category npc-core-changes -NoBuild`: 91/91, 1/1 descriptors, 42.47s.
- `-ValidateCatalog -NoBuild`: VALID, 1666 categories / 13854 methods-files,
  no unmapped/stale selectors, discovery only, 16.87s.

Total 186/186, 5/5 descriptors, 0 failures/skips; every owned-tree/runtime cleanup
succeeded. Independent exact-DLL review: READY, no remaining findings; all original
reproductions resolved and 179 baseline/current variants showed zero acceptance
weakenings or new rejections. Missing effects and whitespace/false-activation
retain their NPC-only shape diagnostics. Relocated shared algorithm/diagnostic
literal bytes were also compared unchanged against the merged base.

Optimization: reuse already parsed per-operation roots, no added filesystem reads
or persistent cache. Structural equality is confined to the exact NPC owner/slot;
changed profiles remain fresh. No owner/source/effect/generation/publication fence
was removed or cached. Pure typed fixtures avoid GameEngine/filesystem setup;
real integration stays covered. These timings are not a global speedup claim.
GM guide/worked repair example and their guard are synchronized. The daemon file
was not changed by these review corrections; its UTF-8 BOM is retained. Raw diagnostics stay outside Git at the recorded local paths.

Preparation limits: two stale-selector PlanOnly attempts executed zero tests; one
intermediate stale-binary build pipeline was canceled before tests. A test-only
actor expectation was corrected to existing `mortal_npc:npc_a`. None counts as
final acceptance evidence; the final frozen selections were not rerun. Full suite,
manual hosted CI and Windows runtime checks were not run. No PR merge into main
or issue closure was performed.

## Part 4 plan — T102-A exact current-state duplicates (2026-11-10)

Issue [#1533](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/1533).
Branch `1533-exact-skill-state-duplicates` was created remotely and verified at
merged main `efa28884833cecd72a1a33872350dbd14178d5bb` before editing.

Implement FR-109..111 in the existing stateless collection validators, with one
small shared operation-local first-occurrence tracker. Reuse each already parsed
JsonElement and existing BuildNpcIdentityKey semantics. The tracker owns only
first indexed paths for the current owner/collection call, is discarded on return,
and compares exact skill names ordinally (player Trim, existing NPC byte names).
It is not an authority cache. No new reads, JSON parses, persistent cache, or
cross-operation success reuse are needed for this cheap linear scan; all existing
field, reference, initialization and publication checks stay fresh and unchanged.

TDD: make existing stateless collection methods internal for direct typed tests
without changing behavior; prove duplicate RED before production logic edits.
Cover all seven collections, identical/conflicting rows, repeated NPC groups,
owner casing/ID precedence, single rows, collection isolation, ordinal siblings,
malformed rows and non-mutation. Add real PlayerStateFiles/NpcStateFiles tests
without snapshots, including independent invalid-field errors. Synchronize compact
GM rule + worked duplicate example + semantic guard. Reuse the existing feature
artifacts; no new plan framework or schema. No product-policy choice is needed.

Impact selection: new `mortal-skill-state-duplicates` owns typed/phase/doc tests;
`mortal-skill-reference-identity` protects exact/confusable/initialization behavior
of the same callers. No composer or npc_core contract behavior changes, so those
broader categories are unnecessary unless review finds a changed consumer.
Fresh frozen-source builds, PlanOnly, these bounded categories and discovery-only
ValidateCatalog precede final acceptance. Independent fresh-context whole-block
review is coordinator-owned. Raw diagnostics remain outside Git under
`/tmp/issue1533-state-duplicates-20261110`. No full suite/manual hosted CI.

Consistency pass: FR-109 maps to T102-B/C, FR-110 to T102-B/C/E, FR-111 to
T102-D/E; no constitutional conflict or unresolved clarification. Optional Spec Kit
commit/context hooks are not needed; existing feature artifacts are updated in
place, and the unrelated `.specify/feature.json` selection is left unchanged.

T102-A causal RED checkpoint: test-source commit `9e5c92c13` and later expanded
first-occurrence guards compile and execute in a tiny external friend assembly
linking the exact checked-in unit and phase test files. Direct calls, no reflection,
no copied validator logic. Production binary SHA-256
`741e8e00f753ace9562bc514df9c725c0be0867eb47959f49f81ac3644a27029`
contains only internal/static stateless entrypoint seams, no duplicate logic.
First 56 cases: 20 positive guards passed, 36 failures due to absent duplicate
findings. Expanded every-later-occurrence guard: 63 cases, 20 passed / 43 expected
failures, including 8 real state-phase duplicate failures and 1 phase sibling pass.
Input trees and owned temporary directories were not retained/shared.

Preparation failures are not RED: two unit-project attempts failed on test data
LINQ inference (3m09.94s, 2 errors; 3m17.08s, 1 error), so investigation moved to
the cheap linked-source harness. Plain arrays and explicit object[] MemberData
removed the ambiguity. The external runner then had one variable-shadowing compile
error fixed before execution. No integration project build or runner selection
has yet completed at this checkpoint. Raw evidence is in the recorded external
`/tmp/issue1533-state-duplicates-20261110` directory.

Implementation/doc checkpoint: the tracker is collection-call-local, shares only
first indexed coordinates across groups with the same existing NPC actor key,
and preserves player Trim versus NPC byte names. It never skips subsequent field
validation or mutates rows. NPC owner keys are computed once per group. Generic
repair already serializes each issue's RepairHint; no custom packet is needed.
Rules/Block_7.txt rule 7.8, the main GM step guide and worked example are synchronized;
the semantic guard parses the example and calls the actual mastery validator.
No afterlife/spiritual-art contract changes, so no afterlife prompt update is needed.
Final source is frozen for real-project builds and independent review; acceptance
and remote-recovery confirmation remain pending.

### T102-A frozen-source verification checkpoint (2026-11-10)

Implementation/test/doc source `81fa8d1641fd1eed7b8c202c9effcaf9cb657873`,
clean tree `a850ff80` (coordinator verified identical remote snapshot
`8d674e4fedaa9060e1c5a6f86fe1e246f429bca9`). Linux x64, SDK 10.0.402 /
.NET 8 / PowerShell 7.6.8. No source edits occurred during builds or verification.

- `dotnet build BookOfEternityClient.Tests/BookOfEternityClient.Tests.csproj --no-restore -m:1`:
  0 errors, 80 warnings, 2m28.14s.
- `dotnet build BookOfEternityClient.IntegrationTests/BookOfEternityClient.IntegrationTests.csproj --no-restore -m:1`:
  0 errors, 118 warnings, 1m48.97s. Warnings include two unit and three
  integration xUnit2031 style warnings in the new tests (Where before Assert.Single);
  these do not affect execution and were not changed after the source freeze.
- Actual checked-in representative tests through the external typed friend runner:
  3/3 passed (identical player active, conflicting player mastery, real NPC state
  phase). Production DLL SHA-256
  `e49d7d73cef3af747ff1d250a762fbcc88ab935eefa42d71f6e27ec6dbb3a8ea`.
- `pwsh -NoProfile -File scripts/test-csharp.ps1 -SelectionFile tests/selection.json -PlanOnly -NoBuild`:
  118 cases / 4 descriptors, valid planning only, 15.67s.
- Same command without `-PlanOnly`: 118/118 passed, 4/4 descriptors complete,
  0 failures/skips/duplicate IDs, 62.41s wall. New duplicate-state category 64/64
  (55 unit including executable GM example guard, 9 real phase integration),
  identity regression category 54/54. Category execution costs 11.98s and 14.58s
  respectively; discovery/setup accounts for remaining wall time.
- `pwsh -NoProfile -File scripts/test-csharp.ps1 -ValidateCatalog -NoBuild`:
  VALID, 1667 categories / 13868 methods-files, 23.05s, discovery only.

Every owned-tree and runtime cleanup succeeded; no `boe-skill-duplicate-*` temporary
roots remain. Summary records the implementation SHA and zero changed files.
Raw build/runner/TRX diagnostics remain outside Git in the recorded `/tmp` directory.

Optimization assessment: reuse the already parsed root and compute each NPC owner
key once per group. The new scan is linear in rows, with only first-path coordinates
retained inside that single collection invocation. No added source reads, JSON
parses, shared mutable fixtures, persistent cache or cached validation success;
no fresh source/owner/effect/publication check is removed. Pure typed cases use no
filesystem/GameEngine; real file-validation coverage remains independent. No runtime
speedup or full-suite/Windows/hosted-CI result is claimed.

Status: FR-109..111 implementation and bounded verification pass. Independent
fresh-context whole-block review reports no defects and 27/27 probe assertions,
including simultaneous real-phase duplicate, confusable-reference and invalid-level
diagnostics with unchanged input bytes. Review accepted with the recorded Linux
and category-selection limits. Clean remote recovery/final publication remain
coordinator owned and pending at this checkpoint. T102's broader confusable-state contour and
T103-T106 remain open; #1533 is not complete and was not closed or merged here.


## Part 5 plan — T104-A current NPC mastery ownership (2026-11-10)

Source issue [#1533](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/1533).
Base local `59a8acdb08fd4f50e7b3a0f59de8d11b97cdcf34`, tree
`21b36a8afe18c9b35e907855ea1c4c46af90e6f1`, equivalent verified remote branch
`1533-exact-skill-state-duplicates` at `baeb3e6fd11f5f61481eccf850fa5a812dc0c590`.
T102-E is complete: coordinator verified the remote ref and a clean independent
GitHub fetch/checkout in a new recovery directory with required files present.
This was source recovery only, with no repeated builds. Broader T102 remains open.

Acceptance/design is FR-112..114. Existing `ValidateNpcSkillMastery` remains the
stateless shape/duplicate validator. Add a separate ownership pass only to current
`npc_skills` validation under `NpcStateFiles`; initialization stays unchanged.

Authority evidence: `GuardianPolicyContracts.EnumerateCanonicalNpcObjects` reads
`UpdateNPCs` and `NPCsInScene`. T101 composer skill-profile validation and effect
source export consume their actor-local `activeSkills` / `passiveSkills`.
`NpcDetailSectionProjection.AddMechanicsSection` presents those profiles alongside
current `npc_skills`. Requiring a skillChanges row for every mastery update would
therefore reject retained core skills. Conversely, `npc_skills` has no canonical
accumulation/normalization path; only player skill state is composed from backups.
The existing NPC initialization path uses pre-turn names only to decide which
current skillChanges names are new, not as current ownership. Do not restore
snapshot-only names or borrow another actor's profile.

Approaches considered: (1) same-root-only ownership is simple but rejects valid
retained core skills; (2) current owner-local core plus current skillChanges is the
chosen proposal and follows both real sources; (3) pre-turn union adds historical
fallback authority and is excluded from this bounded current-state rule.

Implementation: a fresh operation-local NPC skill-name collector receives the
already parsed current core root during the existing `ValidateNpcFile` call,
then adds the already parsed current skillChanges before checking mastery rows.
Use the existing canonical NPC enumerator and BuildNpcIdentityKey semantics.
An optional parsed-root callback on the existing reader avoids a second read or
JSON parse; no root/tree clone is retained. When only npc_skills is selected,
read current core once in ValidateNpcFile as a dependency without emitting its
unselected file-shape findings; when npc_skills is excluded, do not construct the collector. Keep ordinary reader
behavior unchanged when no callback is supplied. The collector keeps ordinal name sets
by case-insensitive owner and skill kind only for this `ValidateNpcStateFiles`
call. Malformed roots/rows continue through existing shape diagnostics.

TDD: direct typed tests use minimal parsed roots and the same collector/pass as
production, no GameEngine or reflection. Confirm causal RED before ownership
logic: active/passive foreign owner, orphan, wrong kind and ID precedence.
Positive guards cover current core-only mastery updates, same-turn skillChanges,
canonical carriers, exact siblings, two owners with the same skill, name fallback,
repeated owner groups, shape-error independence and non-mutation. Real phase tests
prove ordinary and repeated unchanged validation, retained-core mastery-only state,
new-skill initialization, a valid pending snapshot and snapshot-only refusal.
Also pin skills-only selection with an unselected current-core dependency and
suppressed file-shape findings from that dependency; malformed selected core keeps
its independent file diagnostic. The separate NPCCoreChanges guard is unchanged.

Optimization assessment: current root DATA is reused while each existing read is
open; the collector is not a cache or stored permission. Full input is the current
parsed core and skill roots, owner key and active/passive kind. Lifetime begins
and ends inside one NpcStateFiles call; next validation builds a fresh collector,
so changed/replaced/absent files cannot hit stale DATA. Ordinary full-file selection
shares the existing ValidateNpcFile read/parse; skills-only selection adds one
current-core dependency read/parse. The independent NPCCoreChanges command guard
keeps its existing separate reads and findings, including under file selection.
No persistent service or shared mutable fixture. No existing fresh owner/source/
publication fence is removed. No runtime speedup claim is planned.

Impact selection: focused `mortal-npc-skill-mastery-ownership` owns direct, real
phase and executable GM example tests; keep `mortal-skill-state-duplicates` and
`mortal-skill-reference-identity` to protect the same reader/owner callers and
initialization diagnostics. Update tests/selection.json, inspect PlanOnly, run
fresh serial project builds, selected categories and discovery-only ValidateCatalog.
No full suite or manual hosted CI. Freeze source before expensive builds; if test
authoring/build fails twice, use the cheap typed binary-reference reproducer.
Raw diagnostics stay outside Git in `/tmp/issue1533-npc-ownership-20261110`.

Consistency pass: FR-112 -> T104-B/C/E; FR-113 -> T104-B/C/E; FR-114 -> T104-D/E.
No new schema, afterlife, player UI or unresolved product choice. No afterlife GM
sync is needed; Mortal rules/guide/example are updated in the same block.
Spec Kit prerequisites were verified with `SPECIFY_FEATURE_DIRECTORY` targeting
this existing feature; unrelated `.specify/feature.json` remains unchanged.
Optional commit/context hooks are coordinator-owned rather than run implicitly.

Status: design/spec-only WIP, no production or test edits, no checks claimed.
Coordinator approval/publication is required before implementation. Independent
whole-block review, publication and clean recovery remain coordinator-owned.

### T104 continuation checkpoint (2026-11-11, local WIP)

Latest user direction is to continue #1533 independently rather than wait on a
routine publication blocker. The earlier publication denial has not been retried
or bypassed. Remote `1533-exact-skill-state-duplicates` is still baeb3e6fd11f5f61481eccf850fa5a812dc0c590;
local T104 design is 7418f58390e8b88d2a96f1ec823578c379958cc5. This checkpoint and
implementation are local-only until the coordinator resolves publication.

Ruling: continue the explicitly requested bounded local implementation/testing,
without weakening the publication restriction or claiming remote preservation.
The earlier spec/plan approval wording does not introduce a new product decision.

Causal RED: the existing eight-case production-binary probe was rerun, reproducing
four ownership failures and four passing controls. Six permanent real-phase
regressions (active/passive foreign, orphan and wrong kind) were compiled into a
small typed friend runner against the pre-fix production binary: all six failed
on the missing ownership diagnostic. No reflection or copied algorithm was used.
Two initial .NET attempts failed on read-only runtime directories (not test RED);
writable XDG/TMP paths fixed those. Full baseline builds were interrupted before
results, then the small binary-reference runner supplied the causal evidence.
Raw output is in /tmp/issue1533-npc-ownership-20261110, outside Git.

Implementation follows FR-112..114: operation-local names from already parsed
current core/skill roots; core dependency read even for skills-only selection;
no snapshot union, extra parse, retained JSON tree or cross-operation cache.
Current production and permanent test edits are WIP, not yet verified. Next:
finish GM sync/snapshot coverage, freeze source, fresh bounded builds/tests and
independent whole-block review. No CI, merge, or issue closure is requested here.

### T104 frozen implementation results (2026-11-11)

Implementation SHA fb61de94f9a066dc3a5c0d499e9279dff868a729, Linux x64 Debian 13,
.NET SDK 10.0.402, target net8.0, PowerShell 7.6.8. Both project builds used
`dotnet build <project> --no-restore -m:1 -nodeReuse:false -p:UseSharedCompilation=false`
with DOTNET_CLI_USE_MSBUILD_SERVER=0 and writable XDG/TMP/CLI directories:
unit: 0 errors / 80 existing warnings, 3m22.40; integration: 0 errors / 118 existing
warnings, 2m12.28. No new T104 source/test warnings were reported.

`pwsh -NoProfile -File scripts/test-csharp.ps1 -SelectionFile tests/selection.json -PlanOnly -NoBuild`:
159 planned cases, six descriptors, successful planning, 29.708s; no tests executed.
Same command without PlanOnly: 159/159 passed, six/six descriptors complete,
zero failures/skips/duplicates, 76.597s and complete owned-tree/runtime cleanup.
Ownership: 23 unit (including executable GM example) + 18 phase = 41;
identity regressions: 54; duplicate-state regressions: 64.
Raw logs/TRX remain outside Git under /tmp/issue1533-npc-ownership-20261110.

Independent fresh-context gpt-6.1-sol/xhigh reviewed the whole T104 diff and relevant
consumers. No Critical/Important defect or required selection expansion. One
minor wording overstatement was corrected: the existing separate NPCCoreChanges
guard still performs its own reads and may report findings under skills-only
selection. Only ValidateNpcFile's unselected file-shape findings are suppressed.
The phase-test name now states that boundary; its assertions and production code
are unchanged. Fresh integration rebuild, affected ownership run and discovery
follow this name-only correction; no repeat of unrelated passing categories.

Ruling: keep the existing NPCCoreChanges guard's selection, fresh reads and findings
unchanged instead of broadening T104. Preserved non-goals: name/ID alias redesign,
removal-selector handling, core mirror uniqueness, global confusable-name rejection
and broader progression provenance. These remain the later issue contours.

Optimization: two operation-local owner maps retain only ordinal name sets for
one NpcStateFiles call, reusing parsed roots; no new persistent cache, retained
permission, source/owner/publication fence change, or runtime speedup claim.
Selected fixtures are independent and typed; real current-file/snapshot paths are
retained. Mortal GM rules, step guide, worked example and semantic guard are synced;
there is no afterlife, UI, daemon entrypoint, or schema change in this slice.
Publication/recovery remains blocked, local-only, and unclaimed. #1533 stays open.

Final name-correction verification at 1e46affdd2d25581518b68271c4e269c005694db:
- Fresh serial integration build, same command above: 0 errors / 153 existing
  warnings including rebuilt production/TestSupport dependencies, 4m21.90.
  No T104 warnings. Unit source/assertions and production behavior are unchanged.
- `pwsh -NoProfile -File scripts/test-csharp.ps1 -Category mortal-npc-skill-mastery-ownership -NoBuild`:
  41/41 passed, two/two descriptors complete, zero failures/skips/duplicate IDs,
  40.576s, complete owned cleanup.
- `pwsh -NoProfile -File scripts/test-csharp.ps1 -ValidateCatalog -NoBuild`:
  VALID, 1668 categories / 13882 methods-files, no unmapped tests or stale selectors,
  discovery only, 25.378s, complete owned cleanup.
- Both summaries record clean source 1e46affdd with zero changed files. The new
  test-owned temporary roots are absent after cleanup. `git diff --check` passes.
- Independent reviewer verified the narrow correction and reports no remaining
  findings. No broad suite, hosted CI or Windows result is claimed.

T104-B/C/D and the local verification/review portion of T104-E are complete.
T104-E remains open only for authorized remote publication and clean recovery.
The last remote read still returned baeb3e6fd11f5f61481eccf850fa5a812dc0c590;
the denied publication was not retried through another tool or route. These
commits are local preservation, not a durable remote checkpoint. Next action:
coordinator resolves that exact publication restriction and verifies remote
source/recovery before claiming the bounded block remotely saved. #1533 and its
remaining lifecycle contours stay open.

## Part 6 — T105 display-only passive summary authority (2026-11-11)

Source #1533; local base 01b2881bc. Root directs continued permitted local work;
publication remains denied and will not be retried. The existing Block_8 and
MortalSkillProfileContract already define playerStatBonus as display-only.
Causal isolated production probe found valid null/empty structuredBonuses +
summary '+5 strength' adds +5; no-summary control adds 0, structured +2 adds 2.
Raw probe: /tmp/issue1533-governed-companions-20261111 (outside Git).

Chosen fix: remove the sole ExtractLegacyStatBonus call/helper in
CharacteristicsService. Preserving that fallback contradicts the current schema;
a new parser or cache would expand scope. Keep the structured extractor and all
accepted-effect/item/relic consumers unchanged. No additional reads, parses,
cache or shared mutable fixture; removal reduces work but claims no speedup.

- [X] Add cheap typed ComputeAsync RED regressions for valid null/empty summaries,
  malformed/missing structured data, structured permanent/conditional controls,
  source replacement and no input mutation. New test class owns independent files.
- [X] Delete prose fallback, update the exact contradictory Block_12/14 passages,
  and add a worked example plus semantic documentation guard. Do not alter other
  skill/effect rules or introduce broader T103/T106 changes.
- [X] Add narrow mortal-skill-characteristic-authority category including the new
  tests and existing CharacteristicsServiceTests; preserve T102/T104 selections
  for the intended local branch; plan/discover, fresh serial builds, bounded checks.
- [X] Independent whole-block review, compact local evidence checkpoint. Remote
  publication/recovery and remaining T103/T105/T106 contours stay open.

Review focus: narrative numbers never become mechanics; structured permanent and
conditional sources still apply once; no field loss or cached stale result;
all playerStatBonus fallback instructions are removed without changing unrelated
knowledge adjudication or active-effect arithmetic. Existing methods and cheap
fixtures cover these, so no new test-only production entrypoint is necessary.

Part 6 RED evidence: the permanent direct ComputeAsync tests, compiled separately against the pre-fix production DLL, produced seven causal failures (null/empty/missing/malformed structured shape, negative/case display summaries, and structured-to-display-only replacement) with four passing controls (permanent, conditional, absent summary and whole-skill removal). The initial harness-only read used the wrong physical root and was corrected to FileSystemManager.ReadFileAsync before the causal run. Raw probe/build logs remain outside Git. The semantic GM example exercises the same production ComputeAsync path and validates both passive profiles.

### Part 6 local verification checkpoint (2026-11-11)

- Frozen production/test change: `7b76330c6`; final runtime-read example correction: `f07ea2e3d5516374f081b0c87ef09ff1221edb4d`. The example now explicitly distinguishes inconsistent stale input from valid authoring and directs summary-only correction. No C# changed after the frozen source.
- Fresh serial build: `dotnet build BookOfEternityClient.Tests/BookOfEternityClient.Tests.csproj --no-restore -m:1 -nodeReuse:false -p:UseSharedCompilation=false`: **0 errors**, 80 existing warnings, 3m20.38s. The same command for BookOfEternityClient.IntegrationTests: **0 errors**, 153 existing warnings, 4m10.88s. Used the previously verified SDK/PowerShell toolchain with writable per-run runtime/cache paths; no CI or broad suite.
- `pwsh -NoProfile -File scripts/test-csharp.ps1 -SelectionFile tests/selection.json -PlanOnly -NoBuild`: seven descriptors, **187 selected cases**, 22.651s; no tests executed.
- Same selected command without `-PlanOnly`: **187/187 Passed**, 7/7 descriptors, zero failures/skips/duplicate IDs, complete selection and owned/runtime cleanup, 83.606s. Captured clean source HEAD `f07ea2e3d`, zero changed files. Category elapsed seconds: T105 characteristic authority **11.969**; T104 ownership 14.840; identity 19.790; duplicates 11.931. Raw summary: `/tmp/issue1533-t105-characteristics-20261111/categories/test-categories/20261111-082946-869-5-5ba0b1dfb400472591bcf2ea7f37ac90-categories/summary.json`.
- `pwsh -NoProfile -File scripts/test-csharp.ps1 -ValidateCatalog -NoBuild`: **CATALOG-VALID**, 1669 categories, 13887 methods/files, no unmapped/stale selectors; discovery only, no tests executed, 30.680s, complete cleanup. Raw summary is in the same external diagnostic root under `20261111-083120-757-5-7a0615eb60694e99b1089ad4ea5557ff-categories`.
- All four modified rule/example XML documents parse successfully; `git diff --check` passed; no leftover passive-characteristic fixture roots. The complete permanent test set contains 11 new ComputeAsync cases, one executable documentation example and 16 existing characteristic/item/relic/effect controls, alongside 159 prior branch cases.
- Independent whole-block review: no runtime correctness findings. One minor stale-example authoring ambiguity was corrected in `f07ea2e3d` and independently rechecked as resolved. No remaining findings. Review did not execute tests; the verification above is the implementation-side evidence. Existing knowledge adjudication/conditional predicate behavior and broader T103/T105/T106 contours were explicitly not claimed as fixed.
- Optimization assessment: same canonical source reads and parsed structured inputs, no added cache or retained JSON, no new framework or repeated production computation. Removes the characteristic-name scan and regex parsing of prose rather than caching them. Every ComputeAsync still reads current state; replacement/removal regressions prove no stale retained bonus. Source bytes are unchanged. Category timings measure verification cost, not a claimed runtime speedup.
- Publication remains blocked by the prior reviewer denial and was **not retried**. All changes/checkpoints are local only; no push, upload, merge, CI run, clean remote recovery claim or issue closure. The latest explicit direction was to continue permitted local work independently. T103 canonical closure/save admission, the remaining T105 companions and T106 generated-template discipline remain open; concrete audit findings are recorded in research.md.

## Part 7 — T103 current canonical mastery closure (2026-11-11)

Current local base d0af7175d; read-only remote check still baeb3e6fd11f5f61481eccf850fa5a812dc0c590. Prior publication denial is not retried; explicit continuation authorizes local work.

1. Add causal real-refresh and real save/load tests, exact/confusable/missing-source controls, unchanged rejected live/archive bytes and retained raw mastery-only behavior.
2. Reuse existing mastery-core and exact identity classifiers in one parsed-current closure contract. Invoke under the existing post-normalization canonical write lease and existing compensation path. Keep general raw validators' pre-turn fallback unchanged; do not recompose already-normalized state.
3. Reuse the closure contract at existing save/archive admission before publication. Use existing leased/strict UTF-8/archive-budget reads, no alternate publication machinery.
4. Narrow category selection, fresh serial builds, independent whole-block review and local evidence. Then the separate bounded T106 generated-template gap.

Optimization: closure parses each of its two current dependency roots once per admission, creates no retained cache and never reads snapshots. Existing neighboring resource/effect owners do not expose a reusable mastery root; fresh leased reads are needed to prove the actual published pair and prevent stale acceptance. No byte/read-count speedup is claimed. Pure-contract cases stay cheap; real refresh/save/load cases use independent filesystem fixtures rather than a GameEngine.

Part 7 causal RED: all nine permanent lifecycle cases compiled separately against the pre-fix production DLL. Six failed for the intended defect: both removed/renamed real accepted refreshes returned no finding, both invalid saves returned true, both correctly resealed invalid archives committed. Three controls passed: retained mastery-only delta, same-turn new skill and exact save/load round trip. Existing portable save seed plus a pristine item identity index avoided unrelated resource/item failures; raw logs remain outside Git in /tmp/issue1533-t103-closure-20261111. Snapshot source bytes, rejected state/generation and selected archive bytes are asserted by the permanent tests. No GameEngine/reflection was introduced.

## Part 8 — T106 generated skill template guidance (2026-11-11)

Local source base `4edc7fc48`; publication remains denied and is not retried.
This is the bounded generated-template gap recorded in research.md, not a new
runtime identity policy or completion of issue #1533.

1. Extend the existing real-render test before changing the template paragraph.
   Reuse `GmHelperContractScenario.PrepareSessionAsync` for genuine canonical
   bootstrap and the established Windows/pwsh OS choice. Only this test opts into
   a shell parameter; all other daemon callers retain their previous default.
2. Run the permanent test through an isolated typed executable referencing the
   existing production/TestSupport binaries, without rebuilding shared bin/obj.
   Wait for the generated manifest, prove its exact template role/path, then
   assert generated guidance and Russian labels. Join owned process and streams,
   reacquire the canonical lease and prove the independent root was removed.
3. Add one paragraph to the daemon here-string, retaining its UTF-8 BOM/LF.
   Reuse the existing worked example and launcher guidance without duplication.
4. Split the exact render selector from `gm-turn-helper-process` into
   `mortal-skill-template-guidance`, alongside the existing worked-example guard.
   Preserve the prior T102/T103/T104/T105 selection entries. The coordinator owns
   fresh selected-project builds, official bounded category execution, discovery
   and independent whole-block review; those gates remain pending here.

Causal RED: the actual complete daemon generated its manifest/template; existing
shape/Russian assertions passed, and the new case/Unicode guidance assertion
failed because the paragraph was missing. After adding the paragraph, the same
compiled permanent test passed in **5.085s**, including exact manifest routing,
all new guidance assertions and process/stream/lease/root cleanup. Both executions
used Linux PowerShell **7.6.8** and the existing production DLL SHA-256
`0cd437cc48e8c60116db64124d771d70e6087aaef3d9d170197380bbd9ef4bc7`.
The isolated current-test compilation had zero errors/warnings. This establishes
T106 rendering, not verification of newly changed T103 production code. Actual
PowerShell parsing and `git diff --check` passed. Raw build/RED/GREEN logs remain
outside Git under `/tmp/issue1533-t106-template-20261111`; no raw artifacts were
staged. Windows PowerShell 5.1 and official category execution remain UNRUN.

Optimization: one daemon, one real context-generation operation and independent
session per render test; one read each of template/manifest/README. The complete
generator is the consumer under test, so replacing it with a copied string or
shared mutable cached context would lose required coverage. Existing typed
bootstrap avoids a GameEngine/world fixture. No production reads or cache were
added, and the measured test duration is not a runtime speedup claim. No remote
writes, uploads, CI, full-suite run or publication/recovery claim.

Part 8 review correction: independent review found that cleanup's
`AcquireCanonicalWriteLeaseAsync().WaitAsync(5s)` abandoned the underlying
acquisition after timeout and could later leak a lease. It now passes a timed
CancellationToken directly into acquisition and awaits its settlement/disposal.
Nested finally blocks attempt root removal even after process or lease failure;
all cleanup failures and any original test failure are retained in an explicit
aggregate rather than swallowed. Process exit waiting also receives cancellation.

An isolated typed held-lease control reproduced the old pending-after-timeout
behavior, explicitly disposed its late lease, then proved token-based acquisition
settled as canceled while the holder remained active and a later fresh lease
could be acquired/disposed. The corrected permanent full-daemon test passed on
Linux PowerShell 7 in **7.264s**, including cleanup; isolated compilation had zero
errors/warnings. These are targeted cleanup/render checks, not official category
or Windows qualification. Logs remain outside Git beside the earlier T106 logs.

### Parts 7–8 verified local checkpoint (2026-11-11)

- T103 source: `4edc7fc48`; T106 source: `8f61557fa`; cleanup correction and final tested source: **`16fbfeb4b4a8e7e6b1562af959e5a98ff81b9e8b`**. T103 production source was unchanged during T106 work. Both parts remain bounded local slices, not completion of all #1533.
- Fresh serial unit build with `dotnet build BookOfEternityClient.Tests/BookOfEternityClient.Tests.csproj --no-restore -m:1 -nodeReuse:false -p:UseSharedCompilation=false`: **0 errors**, 80 existing warnings, **3m50.71s**. Same command for BookOfEternityClient.IntegrationTests after the cleanup correction: **0 errors**, 153 existing warnings, **4m31.66s**. Used the verified SDK and writable runtime/cache environment; no CI/full suite.
- After the production build, the same nine permanent typed lifecycle tests used for RED passed **9/9** in the isolated runner: both removed/renamed normal-refresh cases reject and compensate, both invalid save and both correctly resealed invalid archive cases reject, both retained/new-skill refresh controls and exact save/load round trip pass. Raw log: `/tmp/issue1533-t103-closure-20261111/green.log`.
- `pwsh -NoProfile -File scripts/test-csharp.ps1 -SelectionFile tests/selection.json -PlanOnly -NoBuild`: **220 cases, 11 descriptors**, 20.601s; no tests executed. Six selected bounded categories preserve the earlier T102/T104/T105/identity regression coverage, adding T103 current closure and T106 actual generated-template guidance. Existing example ownership is deduplicated.
- Same command without `-PlanOnly`: **220/220 Passed**, **11/11 descriptors complete**, zero failures/skips/duplicate IDs; owned-tree and runtime cleanup both complete; **120.603s**. Captured source HEAD `16fbfeb4b`, zero changed files. Category seconds: canonical closure **13.105**, generated-template guidance **23.046**, NPC mastery ownership 16.799, characteristic authority 11.515, reference identity 18.478, state duplicates 14.831. Every category stayed inside its five-minute budget.
- Raw selected-run evidence: `/tmp/issue1533-t103-closure-20261111/categories/test-categories/20261111-090126-706-5-6d65985fe8d9454ebfdfda65a01b2867-categories/summary.json`. No raw diagnostic artifacts added to Git.
- `pwsh -NoProfile -File scripts/test-csharp.ps1 -ValidateCatalog -NoBuild`: **CATALOG-VALID**, **1671 categories / 13897 methods-files**, no unmapped/stale selectors, **22.811s**, complete cleanup; discovery only, no tests executed. Summary under the same diagnostic root: `20261111-090341-155-5-b5584440a1d84eb99a58c58b034869f2-categories/summary.json`.
- Independent T103 source review found no actionable findings; it confirmed lease/compensation and admission ordering plus the fresh-read rationale. Independent T106 review found one Important cleanup defect in test-only timeout handling; timed acquisition cancellation and nested cleanup resolved it, and focused re-review found no remaining issues. All required bounded verification is now complete. Windows PowerShell 5.1 remains **UNRUN**; Linux PowerShell 7.6.8 executed the full production daemon and real storage/bootstrap route, not a string extraction or shim.
- `git diff --check` passed; no leftover owned skill-closure or daemon-render roots. Optimization assessments above remain valid: no new production cache or snapshot reads, one current-root parse per dependency/admission, existing cheap fixtures and one actual daemon render. Measured category times are verification costs, not runtime-speedup claims.
- Remaining scope: the unverified QTE stale-reference hypothesis is recorded in research.md; no claim about all accepted-turn routes or complete T103/T105/T106 closure. No new publication machinery, identity policy, save migration, selector redesign or broad companion expansion.
- **Local only.** The prior publication denial was not retried; no push/upload/merge, remote recovery claim, CI run or issue closure. Latest explicit direction was to continue permitted local implementation and verification independently. Remote publication/recovery remain outside this completed local checkpoint.

## Part 9 — QTE terminal current mastery closure (2026-11-11)

Base 4e42b08d2; remote read remains baeb3e6fd11f5f61481eccf850fa5a812dc0c590. No publication retry. This follows the explicit request to test only the remaining QTE hypothesis against the original issue and fix it only if real.

1. Reuse the independent Part7 filesystem fixture and typed QTE terminal API for removal, same-ID replacement, retained/new exact skill controls and pre-existing invalid-mastery refusal. Validate indexed issue, exact restoration, unchanged generation and complete QTE backup cleanup. No GameEngine/reflection.
2. Call the existing current-only closure inside QteSceneService's existing terminal lease after composition/general validation and after unrelated pre-existing-error filtering. Deduplicate only identical diagnostics using the existing fingerprint. Preserve all compensation and cleanup paths; no new authority model/cache.
3. Narrow integration-only category: new QTE cases, existing ordinary closure controls and affected QTE proof/restoration controls. Prior 220-case evidence remains valid history; do not rerun unrelated NPC/passive/template categories for this one QTE callsite.
4. Causal RED/GREEN, fresh selected integration build, official bounded category/discovery, independent review and compact local checkpoint. Record original acceptance-criteria evidence and verification boundaries without inventing work from broad unchecked headings.

Optimization: two current-root reads/parses via the existing closure, once at the final QTE boundary. General validation's raw/snapshot authority cannot substitute for the published pair. No retained cache or mutation added; reuse fixture/bootstrap, one real terminal operation per case and existing error fingerprint.

Part 9 RED: six permanent typed QTE terminal cases ran against the pre-fix production DLL. Four failed for the intended missing-gate defect (removed skill, same-ID replacement, pre-existing unknown mastery and pre-existing confusable mastery all returned success). Two exact retained/new-skill controls passed. The signed snapshot includes active skills and canonical soul realm; only deliberately unrelated missing player-status/lore bootstrap issues are waived. Probe/source artifacts are outside Git in /tmp/issue1533-qte-closure-20261111. The first exploratory compile omitted the StateDistributor namespace; adding the correct existing IO namespace resolved that harness-only error before any behavioral result.

### Part 9 verified local checkpoint (2026-11-11)

- Frozen C# source: `fc1405bbb`; final tested category metadata: **`faa0c3d2fab1d9f6b718d7d31d677e6babc76c56`**. Runtime change is nine lines in the existing QTE terminal gate. No C# changed after the build.
- Fresh selected integration build: `dotnet build BookOfEternityClient.IntegrationTests/BookOfEternityClient.IntegrationTests.csproj --no-restore -m:1 -nodeReuse:false -p:UseSharedCompilation=false`: **0 errors**, 153 existing warnings, **5m35.89s**. No unit/frontend project was selected for execution; their unchanged inventory remained available for discovery. No CI/full-suite run.
- Initial official PlanOnly stopped before discovery/tests because `related` metadata was a string rather than the catalog's required `{id, when}` object. Corrected in `faa0c3d2f`; formal `Read-TestCategoryCatalog` passed. Initial failure log retained outside Git as `plan-only-initial-catalog-error.log`. No behavioral test failed in this step.
- `pwsh -NoProfile -File scripts/test-csharp.ps1 -SelectionFile tests/selection.json -PlanOnly -NoBuild`: **one descriptor, nine methods, 16 cases**, **11.619s**; no tests executed. The category includes six new QTE cases, four existing ordinary closure controls and six existing QTE borrowed-proof/restoration controls. Old category ownership was explicitly narrowed to its five original lifecycle methods rather than silently absorbing the new partial-file tests.
- Same selected command without `-PlanOnly`: **16/16 Passed**, full selection, zero failures/skips/duplicate IDs, complete runtime and owned-tree cleanup. Wall time **131.470s**, category cost **121.571s**, within its five-minute budget. Captured clean HEAD `faa0c3d2f`, zero changed files. Raw summary: `/tmp/issue1533-qte-closure-20261111/categories/test-categories/20261111-092817-052-5-e363c7e0ccab425689c07f54c597f043-categories/summary.json`.
- `pwsh -NoProfile -File scripts/test-csharp.ps1 -ValidateCatalog -NoBuild`: **CATALOG-VALID**, **1672 categories / 13900 methods-files**, no unmapped/stale selectors, **25.103s**, cleanup complete. Discovery only, no tests executed. Summary under the same diagnostic root: `20261111-093103-122-5-8f9aec344e5641b496dbcd5aadad5de5-categories/summary.json`.
- Independent review found no runtime/test findings; focused re-review accepted the metadata schema correction. Reviewer then independently read the build log, official plan, summary and TRX and ran the formal parser: 16 unique passing cases, complete cleanup and clean tested source confirmed. No remaining review findings. `git diff --check` passed; no leftover owned skill-closure roots.
- Optimization: reuse of the existing current-only closure and the existing typed fixture; no GameEngine, reflection, new cache, persisted flag or publication system. Two fresh current dependency reads/parses are necessary after raw/snapshot validation. Real terminal validation/restoration coverage is retained despite its measured cost; no runtime-speedup claim.
- Original acceptance-criteria status is recorded in research.md. The concrete QTE hypothesis is now resolved; no other concrete defect was reproduced. Broader route/parity/privacy criteria remain unproven by this bounded audit rather than declared implemented or broken.
- **Local only:** prior publication denial not retried; no push, upload, merge, remote-recovery claim, hosted CI or issue closure. Prior 220-case evidence belongs to its earlier frozen source; it is not represented as rerun here.

## Part 10 — exact quest reward skill references (2026-11-12, WIP)

Base `20fbd5651467b01092a05f2a24d45a74102b4b3e`, same task branch.
Direct GitHub HTTPS clone subsequently succeeded and its clean HEAD matched;
the earlier mirror-only failure no longer blocks full source recovery.

A typed exploratory probe linked the unchanged QuestRewardAuthority.cs and
confirmed that a quest reward for absent `Fireball` resolves to current
`fireball` with a different skillId: zero validation issues and the replacement
label displayed. Only unrelated item dependencies were throwing compile stubs;
this is not an integration test. Root cause is the case-insensitive skill map.

Bounded plan: add permanent direct contract and existing real QuestReward phase
regressions; capture causal RED; preserve ordinal skill identities in the existing
map/registration deduplication; keep item/relationship matching unchanged; retain
HistoricalOnly/Unavailable plus reason. Synchronize existing GM guidance/example
and guard, select only affected quest-reference tests and documentation. No new
system, spiritual combat work or broader UI redesign. Separate UI case-folding
and browser mastery findings remain hypotheses, not included in this fix.

Optimization: reuse existing per-operation parsed roots and map, no new file reads
or persistent cache. Cheap independent JSON fixtures test actual production
resolver; limited real validation tests prove wiring. No speedup claim. New tests
and fix remain unrun until results below; full suite/hosted CI are not authorized.

### Part 10 verified bounded checkpoint (2026-11-12)

- Tests saved at 83cbff1cded86d268c9fe441a3227e3aed565d05; minimal resolver/docs
  fix saved at 66c01049668f2cb759cb9725482d385dccf59785, tree
  3c35dd55e2dbe022dda11e90dd5e2028e458f010. Remote fetched and exact clean
  worktree verified. Main and issue state are unchanged.
- Causal official RED: 3 FAIL / 4 PASS unit cases, no skips/duplicates, 12.985s,
  cleanup complete. Fail-fast stopped integration before a completed result:
  no integration RED claim. Permanent tests also reproduced the same three
  failures against the real production DLL without item stubs.
- Fresh serial selected builds using dotnet build --no-restore -m:1
  -nodeReuse:false -p:UseSharedCompilation=false: unit 0 errors / 43 warnings,
  55.15s (including changed production); integration 0 errors / 0 warnings,
  1.42s incremental, copying the new production output. Its unchanged test
  source was freshly compiled before RED: 0 errors / 141 warnings, 47.47s.
- Official scripts/test-csharp.ps1 -SelectionFile tests/selection.json -PlanOnly
  -NoBuild: 2 descriptors / 19 cases, 10.889s, zero execution. Same command
  without PlanOnly: 19/19 PASS (7 unit, 12 integration), 20.997s, both descriptors
  complete, zero skips/duplicates and complete owned-tree cleanup.
  Run identity: 20261112-071049-326-152-52426853895e4b1fbba5347c3a246dc3-categories.
- scripts/test-csharp.ps1 -ValidateCatalog -NoBuild: CATALOG-VALID,
  1673 categories / 13906 methods-files, 10.697s, complete cleanup; discovery only.
  Raw logs/TRX remain outside Git. No hosted CI or broad-suite execution.
- Independent source review: READY, no Critical/Important findings. It checked
  ordinal map and both deduplication stages, unchanged other reward kinds/history,
  real phase wiring, documentation and existing map reuse. No new cache/read.
- This closes the reproduced quest reward retargeting defect only. The original
  case-sensitive skill contract is unchanged; no new system or spiritual combat
  integration. Console mastery lookup and NPC case-folding plus browser mastery
  parity remain source-level hypotheses requiring a causal check, not completed
  work or proof that all of T103/#1533 is done.

## Part 11 — existing UI exact mastery audit (2026-11-12, WIP)

Follow the original exact-name contract through existing console/NPC/browser
projections only. Source candidates: console and NPC case-insensitive maps;
browser missing canonical mastery read and case-folded entry lookup/dedup.
Add causal typed/public-command regressions before any behavior correction.
A narrow internal static NPC projection seam exposes the existing pure operation;
no reflection or duplicate algorithm. Preserve actor matching, sorting, other
reference commands and all spiritual combat behavior. Reuse existing fixture
and parsed roots; no new system/cache. Results are pending, not claimed defects
until RED. Test selection is limited to these projections and their old controls.

### Part 11 verified existing UI checkpoint (2026-11-12)

- Final tested source: e94b14f3f7b13a8b3ae15a7e68e246702b439c17.
  Earlier WIP db682a8c (tests/seam), 17e81cbd (NPC/browser fix), 34fab1a5
  (console fix/oracle) preserve progress. Full separate HTTPS clone at
  34fab1a5, then fast-forward to e94b14f3, verified clean and exact remote SHA.
- Reproduced defects: NPC active/passive case siblings collapsed and case-only
  removal deleted the other skill; console bound Fireball to fireball mastery;
  browser omitted canonical mastery, selected the wrong exact-name sibling,
  and deduplicated distinct names without IDs. Only existing projections changed.
- Official NPC causal RED: 4 FAIL / 2 PASS, 8.687s. Final corrected-oracle
  integration causal RED against original browser and console behavior:
  6 FAIL / 7 PASS, 11.365s (console one; browser five). Complete cleanup,
  no skips/duplicates. Source was restored after this deliberate causal run.
- Test preparation mistakes are not product failures: initial console test
  omitted state refresh; its first compile asserted a string as bool.
  Browser public output normalizes grids into section Facts and fractional
  progress into Metrics, so early grid/Fact-progress assertions were incorrect.
  Both oracles were corrected without relaxing expected values. The old console
  localization assertion requires a Russian numeric culture; final commands
  used LANG=ru_RU.UTF-8 and LC_ALL=ru_RU.UTF-8, not a weakened assertion.
  Intermediate 11/13 pass was not represented as complete.
- Final serial builds: unit 0 errors / 80 warnings, 63.56s; final integration
  0 errors / 153 warnings, 78.08s. Commands: dotnet build <selected project>
  --no-restore -m:1 -nodeReuse:false -p:UseSharedCompilation=false.
- Official scripts/test-csharp.ps1 -SelectionFile tests/selection.json
  -PlanOnly -NoBuild: 2 descriptors / 19 cases, 9.620s, zero execution.
  Same selection without PlanOnly: 19/19 PASS, 19.550s, 2/2 descriptors,
  no skipped/duplicate cases, complete owned-tree cleanup.
  Run: 20261112-073041-958-201-03365ef7ed1c4ab6a952e16d5038d3d9-categories.
- scripts/test-csharp.ps1 -ValidateCatalog -NoBuild: CATALOG-VALID,
  1675 categories / 13916 methods-files, 10.664s, complete cleanup.
  Discovery only. No broad suite, hosted CI, spiritual combat or new systems.
- Independent source review/re-review found no Critical/Important findings.
  Final Facts/Metric oracle reviewed against the real normalizer; outcome
  review is recorded separately. Raw logs/TRX stay outside Git.
- Optimization: console/NPC reuse their existing operation-local maps; only
  comparers change. Browser reads current mastery once for the selected active
  detail, scans once with Ordinal identity and rejects ambiguous duplicate
  matches. No read for passive/overview and no persistent cache or mutation.
  Fresh next-detail regression proves changed mastery is observed. Actor matching,
  NPC sort order and non-skill reference behavior remain unchanged.
- This resolves the specific UI hypotheses recorded in Part10. No further
  concrete defect is established by this bounded audit. Earlier broad checklist
  headings are not grounds for inventing additional systems or spiritual-combat
  integration; this checkpoint does not assert whole-issue acceptance or merge.
- Final independent outcome review verified all 19 unique TRX passes, cleanup,
  catalog, build logs and clean recovery. Production DLL hashes differed because
  unit and integration builds embedded revisions 17e81cbd and 34fab1a5.
  A read-only portable-PDB check matched each DLL CodeView GUID to its own PDB:
  1227 documents in each, only generated AssemblyInfo.cs differs; all 1223
  non-generated production source checksums match the current source exactly.
  Thus existing unit results use the same runtime source, not a stale NPC fix;
  no redundant rerun was needed. The probe/log remain outside Git.
- GM-authored skill schema and commands are unchanged; existing exact-name
  guidance already states the contract. This consumer-only fix needs no new
  GM prompt/example or new gameplay rule.

## Part 12 — Mortal training residual audit (2026-11-12, WIP)

Owner clarified that no real old saves exist, only test/system saves: no legacy
migration is required. Current test templates/fixtures may follow current schema.
All remaining audit work is Mortal; spiritual combat does not explain or defer
Mortal criteria. "Governed companions" in the source issue means linked skill
mechanics, not an automatically required new companion-entity subsystem.

Bounded causal candidates in the existing local TrainingService purchase route:
case-insensitive target binding may select another exact skill; local unlock
checks only activation/actionCost and may bypass the shared complete profile.
Use existing public purchase fixtures to prove them before fixing. Preserve
teacher/location/resource guards, paid GM-evolution fallback, actor semantics and
all afterlife routes. No broad training rewrite or new output protocol.
If confirmed, reuse the already parsed prospective skill and common validator,
not a new cache or additional file reads. NPC prose remains a separate contract
assessment: FR-104 intentionally scoped it out; free-text name co-occurrence is
not reliable actor ownership and must not be silently made authority.

Part 12 causal preparation update: original 13-case run was 5 FAIL / 8 PASS; two practice failures first stopped at formatting-only byte equality. Corrected that oracle to full JSON equality without changing production; rerun gave 4 genuine FAIL / 9 PASS (three invalid active profiles and lower-case sibling). Added passive invalid-profile and conflicting exact ID/name cases for independent review findings; these two are UNRUN. No production fix yet.

Part 12 implementation checkpoint (UNVERIFIED): expanded RED completed 15 cases: 6 FAIL / 9 PASS, 11.6734240 seconds, cleanup complete. Passive invalid profile and conflicting ID/name failed causally. Training now validates prospective active/passive local unlock with the shared profile contract; invalid profiles retain existing GM evolution. Exact consistent unique target resolution protects purchase and pending cleanup; conflicts refuse before payment. Added existing name-only pending completion control (16 planned cases). No new cache: already-loaded roots reused; small candidate scans/profile construction are operation-local, no filesystem reads added. Current authority/location/payment/coordinated publication checks remain fresh. Build/GREEN/review pending.
