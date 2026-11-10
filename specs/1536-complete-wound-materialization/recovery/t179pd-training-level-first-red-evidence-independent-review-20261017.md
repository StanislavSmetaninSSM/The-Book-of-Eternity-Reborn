# A1 FIRST RED — independent causal EVIDENCE review

**EVIDENCE verdict: ACCEPTED_EVIDENCE_QUALIFIED_STRUCTURAL_MISSING_POLICY_RED2.** No confirmed material defect or evidence blocker. The saved first execution qualifies as the exact, narrow missing-helper RED authorized for A1. It is not semantic admission evidence or a passing implementation.

**SOURCE verdict: retained prior ACCEPTED_SOURCE_FIRST_A1_CAUSAL_RED2; no new implementation SOURCE acceptance.** The prior review's permitted first boundary execution matches the actual packet. This review independently checked the causal source and its continuity; it does not accept future production changes.

Reviewer: independent non-author `/root/a1_reviewer`, parent-assigned GPT-6 Astra / XHigh review role; backend telemetry not exposed. Review date: 2026-10-17. The live task mandate is IMPLEMENTATION A1 and supersedes the erroneous newer DESIGN-only STOP banner. That mandate does not remove the next concrete SOURCE and EVIDENCE gates.

## Exact evidence and verification

- Carrier: `0734df4c63d7f6ecd0f9efee73d0314fee822850`.
- Actual source: `693b33941edecda1ffad5d9944630a091b4ece3e`.
- Compiled source: `c032138834ff35206c07ff673e06ab3dc40ab128`.
- Product baseline retained: `04f5fff200350772bc8fe78a0c435c0a1c5866ac`.
- Actual packet: `specs/1536-complete-wound-materialization/recovery/t179pd-training-level-first-red-20261017.json` and its six preserved raw files.
- Preparation packet: `specs/1536-complete-wound-materialization/recovery/t179pd-training-level-red-preparation-20261016.json` and its twelve preserved raw files.

I retrieved exact GitHub commit-addressed source/evidence bytes into `/tmp`, recomputed stored SHA256, decompressed raw SHA256 and size, reproduced gzip mtime0 bytes, and checked Git blob IDs for all **18 artifact chains**. Each manifest command is exactly the decoded corresponding raw record; stdout hashes match. The root inspection was compared only after deriving the results independently. Its recorded hashes and two proof projections agree with raw evidence.

The independent checker `/tmp/a1-red-evidence-review-check-20261017.py` completed with native exit 0. Its generated JSON records the complete input inventory, hashes, nine source pins, three GitHub comparison responses, exact case IDs/proofs, four DLL records and limits. No build, discovery, test, application, CI or Windows-artifact operation was performed. Repository files and Git state were not mutated.

Repository policy was read at exact `7a9eca35af13fe48c959b8d431087e9c058a45dc`: AGENTS.md, development workflow, current A1 plan/recovery entries, implementation-plan protocol G and A1 contract lines 378–396. Testing policy/catalog were read at c032. The branch checkout was still main while the parent fetched; therefore the review relies on these exact remote bytes, not unqualified current-worktree source.

## Source and compiled binding

GitHub compares show baseline→c032 has only the new test source, category/selection changes and task documents; production is unchanged. c032→693 has **21 task-document/evidence paths only**, and 693→0734 has **11 task-document/evidence paths only**. No production, test, runner or selection correction occurred between compilation and actual execution. Five critical c032/693 files were also fetched separately and are byte-identical: TrainingLevelPolicyTests.cs, CharacteristicsService.cs, categories.json, selection.json and test-csharp.ps1.

Fresh preparation records have clean c032 HEAD, native 0 for BOTH/catalog and PlanOnly, completed integration/unit builds with zero errors, and zero executed tests. Catalog reports 780 categories / 11,732 methods with no unmapped or stale selectors. The plan has 4 descriptors / 54 cases: boundary2, pure33, ordinary17 and conditional Late2. Only boundary2 was actually executed in this packet.

The four recorded fresh-build DLL hashes equal PlanOnly before/after and actual before/after:

| Binary | SHA256 |
|---|---|
| Product, both unit/integration outputs | `c494c4e4af02480a2546be4ed86e330052d6e88e8c29afbba741253b2c2e882c` |
| Unit | `4ccd27554adc6044c9c7b408abec151745bc2a023a3cf8fc03e51dd96cfe8b9f` |
| Integration | `2f2d6cd63037daf0b98c70f07fd3c089fcb49d5c9835d6c0632ac330f08b231f` |

This accepts the preserved build/run binding. Original live binary files were not available for an independent current-environment rehash; no claim of such a rehash is made. GitHub's large recursive tree response was truncated; the needed omitted tests subtree was separately retrieved and its SHA bound to the exact carrier root tree. The review makes no complete-repository recovery claim.

## Actual outcome and causality

Exact actual command: `pwsh -NoLogo -NoProfile -File scripts/test-csharp.ps1 -Category fr063-training-level-policy-boundary -NoBuild`.

The raw record, plan, runner log, stdout, summary and TRX agree: native exit **1**; **1/1 descriptors, 2/2 cases completed, 0 PASS, 2 FAIL**; no skipped, duplicate, underfilled, aborted or timed-out cases. Command wall is **7.5061676040058956 s**, runner wall **6.6046617 s**, category **2.8535778 s** under its unchanged five-minute budget. Summary `CategoryResults.Completed=false` records the failing workload, not an incomplete selection: selection completion and TRX counts are separately complete. The runner's execution source sets that category success flag only after workload completion without a thrown failure.

At exact compiled `BookOfEternityClient.Tests/TrainingLevelPolicyTests.cs:24–33`, each row creates real files and a real CharacteristicsService through WithOwner, awaits public `ComputeAsync()` at line 26, asserts PlayerLevel=2 at line 27, emits the actual result at lines 28–32, and only then calls ReadPolicy. CharacteristicsService.ComputeAsync calls its real GetPlayerLevel; ordinary reads reach `_fs.ReadFileAsync` because no joint projection reader is installed. The fixture uses PhysicalLoadTransactionOperations and does not install a mock helper or fabricated projection owner.

Each raw TRX case has its own matching method diagnostic, actual/expected level 2, and exactly `Assert.NotNull() Failure: Value is null`, with ReadPolicy line **128**, causal caller line **33**, and WithOwner line **177**. Line 128 checks the exact nonpublic static method with a JsonElement parameter. Both intended helpers are absent from the inspected product partials, and LevelPolicies.cs does not exist at this source. No numeric-result assertion, conversion, setup or cleanup failure is substituted for the missing helper.

| Case | TRX duration | Strict cleanup root |
|---|---|---|
| ReadTrainingExperienceLevel | `00:00:00.0465143` | `/tmp/boe-training-level-a1-cb170031191548c38f7836327d000121` |
| ReadTrainingSingleLevel | `00:00:00.2165429` | `/tmp/boe-training-level-a1-960d2d5842d242fb80a2be9106f4cc87` |

Both raw cleanup receipts say Removed=true. The exact finally block deletes its own directory, emits observed absence, and then strictly asserts absence at line 193. Both remaining errors preserve the earlier line128 failure rather than a finally assertion or deletion failure. The actual wrapper's before/after owned-prefix lists are empty, source HEAD is unchanged, and status is clean. This is adequate historical removal evidence for both distinct roots. Current absence in a different execution environment is not used to prove historical cleanup.

**Fixture repair versus intended RED:** the genuine ordinary prerequisite already succeeds in both cases and there is no source/fixture change between compiled and actual commits. The only reached failure is the specified missing method. Therefore no fixture repair is needed to qualify this saved RED. Merely changing fixture expectations or rerunning it would add no missing causal evidence.

## Findings, proof limits and next gate

Confirmed defects: **none**. No file/scenario/consequence finding requires correction before accepting this narrow RED.

The following limits are material to reporting but are not defects in the accepted A1 contract:

- `TrainingLevelPolicyTests.cs:177–184`: missing-helper failure prevents the callback from returning, so later source-byte assertions and the ordinary source-image diagnostic are not reached. Treating this RED as no-write/exact-byte proof would overstate its evidence.
- `TrainingLevelPolicyTests.cs:26` / `CharacteristicsService.cs:68`: complete ComputeAsync has legitimate later reads. These two rows do not prove all-computation read order, physical reread counts or fallback/exception semantics.
- `TrainingLevelPolicyTests.cs:128`: failure proves absence of the required helper signature, not correctness of future helper bodies or full-family semantic admission. The pure33 and ordinary17 remain unexecuted here.

The causal gate is satisfied for proceeding with the already accepted bounded A1 extraction under the current user mandate. Preserve this verdict before production edits. Future code must keep the two pure nullable helpers and only the three parsed-leaf replacements, existing IO/catches/file priority/default and joint rethrow. Fresh preparation and independent affected SOURCE remain required; changed shared production then requires FIRST protected Late2, immutable freeze and separate EVIDENCE before release of A1 GREEN controls. This report itself releases no new actual command, no Windows/CI operation and no broader family completion.

**FINALSTOP for this review. Await the parent's next exact SOURCE or EVIDENCE packet.**
