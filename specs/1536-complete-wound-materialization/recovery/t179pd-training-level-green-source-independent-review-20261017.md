# A1 implementation — independent affected SOURCE/FIXTURE/SELECTION review

**Verdict: ACCEPTED_SOURCE_FIXTURE_SELECTION_PREPARATION_AND_RECOVERY_CONDITIONAL_FIRST_LATE2.** No open material finding. The minimal implementation, unchanged finite fixtures/selection, fresh zero-execution preparation and complete source recovery are accepted for the next bounded gate.

**EVIDENCE status: current implementation actuals remain UNRUN.** This is not GREEN, A1 completion or runtime evidence acceptance. Historical structural RED at actual `693b33941edecda1ffad5d9944630a091b4ece3e` / compiled `c032138834ff35206c07ff673e06ab3dc40ab128` retains its original accepted provenance and was not rerun.

Reviewed final carrier: `12498e69f9475d71e628e554cb1b86b77312a954`. Compiled implementation: `e2474cfe0ed3302c91e1e7cda3fe653d587d4680`. Accepted RED decision before implementation: `36ee5b014cee46b852b156971e396231f5e60922`. Initial source/preparation packet: `3da75b412022f9268d10b289ddf620fee5ab63d3`.

Independent non-author reviewer: `/root/a1_reviewer`, parent-confirmed inherited GPT-6 Astra / XHigh; backend telemetry unavailable. Sole implementation author: `/root/a1_writer`. Review date: 2026-10-17. The current IMPLEMENTATION A1 mandate governs; historical DESIGN-only STOP text is not reinstated.

## Finding and verified closure

**A1-S01 — resolved, metadata only.** At initial carrier3da, `recovery/t179pd-training-level-green-source-20261017.json:228` named `GetTrainingStateAsync/TrainAsync` as ordinary consumers. Neither method exists in the inspected product/tests. Following that dependency map would omit the real training writer and point recovery/selection review at nonexistent APIs.

Carrier12498 changes the map to the actual `ComputeAsync / ApplyStatsIncreasedAsync`, with references and an explicit original-finding/correction trace. I independently confirmed the direct GetPlayerLevel calls at `CharacteristicsService.cs:71` and `:155`, and the retained training test's `ApplyStatsIncreasedAsync` invocation at `MortalJointAutomaticCalculationTests.cs:192`. The correction does not change production, tests, category membership, selection, runner, fixtures, compiled inputs or DLLs; repeating preparation is unnecessary.

Nonblocking reference precision: the corrected packet says the test invocation is at line194; line194 is its adjacent comparison of actual applied/blocked results, while the invocation is line192. The method and dependency are unambiguous and verified from exact source. This does not leave a material dependency or qualification gap.

## Source preservation and consumer impact

Production changes are exactly the new 27-line `CharacteristicsService.LevelPolicies.cs` partial and three parsed-leaf replacements in `CharacteristicsService.cs:GetPlayerLevel`. An independent textual reconstruction proves the entire new CharacteristicsService.cs equals the accepted RED source after only those three replacements; there is no incidental edit elsewhere in that file.

Both helpers are `internal static int?` and operate only on the supplied JsonElement. The predicates/GetInt32 calls preserve the original behavior:

- Experience checks exact numeric `level` first, then exact numeric `playerLevel`. A wrong-kind first field can reach the alias; a numeric first field's failed Int32 conversion throws before the alias.
- Single-source checks only exact numeric `level`. No matching numeric field returns null. Caller `is { } level` distinguishes absence from zero, negative values and Int32 endpoints, which return unchanged.
- Existing JsonElement lookup retains case sensitivity and last duplicate-key behavior. Non-object roots and numeric fraction/overflow retain the same exceptions; the helpers add no parse, catch, clamp or alternate validation.
- Each await/read stays outside its original parse catch; each JsonDocument remains alive through its helper call and no JsonElement escapes. Experience→player_status→soul priority, early returns, three independent catches, ordinary next-file fallback, joint rethrow and final1 are unchanged. IO failures still escape before parsing.

The actual ordinary consumers immediately use the extracted body: public ComputeAsync and ApplyStatsIncreasedAsync. The unchanged joint projection constructs its scoped CharacteristicsService and calls the same ComputeAsync; its stricter physical read adapter, source validation and catch behavior remain intact. This shared dependency makes FIRST protected Late2 meaningful after the production edit. The extraction adds no HOT owner, authority, filesystem API, clock, ID, write, award or time mechanism.

Tests, integration fixtures, scripts, catalog and workflows are byte-unchanged from c032 to e247. Both existing CharacteristicsService joint partials are unchanged. e247→12498 is task-document/evidence metadata only. Prior independent RED report MD/JSON bytes remain exact. Diff whitespace verification passed.

## Fixture and selection sufficiency

The four existing category owners select **10 distinct methods / 54 cases**: boundary2 + pure33 + ordinary17 = **52 A1**, plus **2 protected Late2**. Every category retains its five-minute budget and frontend=false. The exact fresh plan matches source/catalog counts and updated reasons; no duplicated selected method or broader training/provenance/header/worker family is introduced.

The unchanged A1 fixture uses real filesystem/service ownership with a separate root per case. Boundary2 reaches public ComputeAsync and then the helpers. Pure33 checks parsed values and exceptions without filesystem or owner claims. Ordinary17 consists of fourteen actual GetPlayerLevel source/fallback/order/byte controls, one genuine pre-parse read-fault control, and the two existing ApplyStatsIncreasedAsync ordered-cap/exact-byte controls. The intentional existing category ownership overlap is documented. The isolated private-reader controls do not falsely treat the entire ComputeAsync's later reads as level-reader reads.

Protected Late2's two entry bodies, shared fixture and strict cleanup helper are unchanged. Their actual staged/Late path remains distinct from live ProcessPlayerTurn or cold replay claims. Their owned root prefix is included in the execution wrapper's before/after observation. No new claim is made about those future runtime outcomes. Pure controls own zero roots and must not receive fabricated fixture-cleanup PASS.

No private original/source/checkpoint/generation/header/worker dependency changed, so their conditional additional owners are not activated. The selected Late2 and finite A1 controls are sufficient for this literal extraction; the unchanged historical passing families are retained with original provenance.

The GM no-update rationale is accepted: this is an internal extraction with identical returned/emitted gameplay/schema/authoring behavior. Mortal/afterlife prompts, examples, manifests and guards need no change for this diff.

## Fresh preparation and exact binding

I independently checked **18 source pins**, exact production/unchanged-scope/selection projections, **12 raw/local/gzip/committed-Git artifact chains**, the complete preserved build/discovery logs and plan, and all **four live DLLs**. The writer inspection was treated as supporting material, not independent acceptance.

At clean e247, the saved commands are:

1. `pwsh -NoLogo -NoProfile -File scripts/test-csharp.ps1 -ValidateCatalog` — native0, BOTH builds complete, catalog780 / methods11732, zero stale/unmapped selectors. Integration133/unit35 warnings, zero errors and no new LevelPolicies/TrainingLevelPolicyTests diagnostic. Wrapper wall91.58100382399994s; integration build73.586s and unit11.201s approximately.
2. `pwsh -NoLogo -NoProfile -File scripts/test-csharp.ps1 -SelectionFile tests/selection.json -PlanOnly -NoBuild` — native0, four descriptors /54 planned cases, **zero completed descriptors/cases and zero executed tests**. Wrapper wall5.8649749819999215s. Planning-only incomplete execution counters are expected and not reported as PASS.

Both recorded source HEADs stay clean e247; no timeout, duplicate results or process/runtime cleanup failure. There is no TRX or actual-category execution in this preparation. Catalog fresh-build outputs equal PlanOnly before/after and the currently rehashed DLLs:

| Binary | SHA256 |
|---|---|
| Product, both outputs | `0816ddb12ab8eb434b4c39ddb8a10e321906ea727af3305840e4932b10b043d6` |
| Unit | `f3227ead87bffc76734cf9ee810bb35b0b76fbf5633fcb9fd8cf915eba9aeb1b` |
| Integration | `abae9956053e461f87c03eabb2d6cbfcee891f983810118a0ceb8cc1b112d2f8` |

The frozen preparation manifest SHA256 is `bb49e543fc19a80bcc425bb236712611f5a26b1ed063388ee4cc67bb004a8fde`. The runner and freeze helpers preserve actual native exit, fresh result-directory identity, source/working-tree state, four binary pins and raw evidence. Their category allowlist is not itself an execution grant; the explicit gate below controls order.

## Complete source recovery

The independent checker re-read **all 54,526 files / 3,681,361,580 bytes** of the new GitHub archive restoration at `/workspace/a1-github-full-source-7a9eca35-20261017`, recomputing each Git blob, SHA256, executable mode and exact file-set membership against the pinned7a tree. This is not merely a receipt-metadata comparison or a sample of critical files.

It then independently checked every file of the exact GitHub deltas: **30 files covering all26 changed paths** from7a→3da, followed by **10 files covering all6 changed paths** from3da→12498, including mandatory governance files. Composition equals **all54,549 tracked paths at12498**, with no deletion or uncovered new dependency. LevelPolicies.cs and all new checkpoint/evidence paths are covered. This is a complete recovered base plus exact deltas, not a claim that the final tree was separately cloned into one new directory or that runtime was run there.

The final full-recovery receipt, including its added transport metadata, has raw SHA256 `51d5a45a739fe6f5342efa035ca45f4c41c61b10e671a4cf3bcb4d62aa16741e`; the committed `.json.gz` decompresses byte-exactly to it. The earlier a434… hash predates that metadata augmentation. The recorded source archive is the exact GitHub7a tarball, 3,344,871,045 bytes, SHA256 `b35405527e4645cd505149632483671c9087323981fbee77cf3584105ea8eb5f`. This reviewer did not download or execute an archive. The committed prepared-delta receipt also equals its independently checked local raw bytes.

At verification completion, remote task ref and clean checkout both equal12498; remote main remains `d0241e71e349fbe2020e4a41b6be7c627c81cbb4`. The JSON report preserves exact recovery receipt hashes, the two small delta receipts, full current coverage, raw chains and read-only command records. New recovery is still required for the next decision carrier's newly added report/checkpoint paths.

## Exact conditional release

After this decision/report is saved, non-force pushed, read back and its complete changed delta recovered, and while compiled e247 inputs/four DLLs remain exact, release **only FIRST singleton** `fr063-joint-late-completion -NoBuild` under its unchanged five-minute category budget. Preserve the first complete native/plan/TRX/raw/source-binary/strict-cleanup outcome, then stop further actuals, publish/read back/recover it and obtain **separate independent EVIDENCE** before any specifically released A1 GREEN owner. First unexpected failure/skip/underfill/timeout/evidence or cleanup defect remains a stop; no hidden retry or aggregate SelectionFile actual.

This report does not release boundary/pure/ordinary actuals directly, accept current runtime behavior, close A1/full3c or authorize Windows/CI/worker/artifact-transfer/settings changes. Full HOT/S/B/M3/F1/T197/new-QTE and D4/guidedUS5/actualDOM+visual/Windows/D8 remain open.

The reviewer performed only repository/network read-only checks and local evidence inspection/hashing, writing review artifacts under `/tmp`. No repository modification, build, discovery, test, application, CI, Windows artifact download or delegation was performed by this reviewer.

**FINALSTOP. Await the exact FIRST Late2 EVIDENCE packet.**
