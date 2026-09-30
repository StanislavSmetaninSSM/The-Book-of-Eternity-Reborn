# Provisional FullValidation diagnostic headroom execution plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:executing-plans with test-driven-development and verification-before-completion. Parent controls checkout edits/build handoff and independent review. No agents, staging or commits from this unit.

**Goal:** Increase only FullValidation's default and hard budget from 15 to 30 minutes as provisional diagnostic headroom after repeated timed-out selections; keep performance investigation open.

**Architecture:** The runner derives both FullValidation default timeout and hard cap from its lane-definition `TimeoutMinutes`. Change that one value, the existing exact boundary guard, and the current policy documentation together. Preserve all categories, routing, scheduling, cleanup and other lane budgets.

**Tech Stack:** PowerShell 7, C# xUnit boundary source guard, Markdown policy.

**Spec/task:** Issue https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/1536, tracked T081-B2C-J2-C2-V2 in `specs/1536-complete-wound-materialization/tasks.md`. User explicitly authorized documented bounded headroom while the performance cause remains tracked. Existing repository test policy is `docs/testing.md`.

## Global constraints

- Use actual checkout preimages. `docs/testing.md` already includes the parent's revised budget policy; do not restore it from HEAD.
- Keep Fast 5, Focused default 5/cap 15, LifecycleIntegration default 10/cap 30, PreMerge/Complete 30, all other lane budgets, category selectors, process parallelism, deadlines and cleanup behavior unchanged.
- Thirty minutes is provisional bounded headroom, not a measured successful-run duration. Reconcile residual timings before claiming the new cap is sufficient.
- The original FullValidation timeout and isolated residual timeout remain incomplete evidence. Do not rerun the full lane merely to obtain a passing label for this configuration change.
- Wait until parent finishes residual reconciliation, reviews this revised proposal, rebases the separate migration shared files, and releases its binary/build lock. This scratch plan itself grants no execution handoff. Rebuild scratch preimages/postimages from those current shared files before any apply; the task tracker is not a budget target.

## Task 1: one lane budget and its guard

**Modify exactly three files:**

- `scripts/test-csharp.ps1`: `FullValidation.TimeoutMinutes` only, 15 РІвЂ вЂ™ 30.
- `BookOfEternityClient.IntegrationTests/IntegrationTestBoundaryTests.cs`: only the FullValidation literal inside `CSharpLaneRunner_DefinesNonOverlappingProjectRoutedPreMergeSchedule`, 15 РІвЂ вЂ™ 30.
- `docs/testing.md`: FullValidation table cell 15 РІвЂ вЂ™ 30 plus the measured evidence paragraph after the existing РІР‚СљRevising lane budgetsРІР‚Сњ policy.

Complete postimages and exact current-checkout preimages are in `fullvalidation-budget-proposed/`; hashes are in its `pins.json`; full adjacent delta is `fullvalidation-budget-proposal.patch`. `build-fullvalidation-budget-proposal.py` regenerates from the actual checkout and rejects missing/multiple edit anchors.

- [ ] After parent handoff, verify current preimage hashes against `pins.json`; if a file changed, regenerate/review the scratch delta first. Keep any newly reconciled timing evidence instead of replacing current docs with an older copy.
- [ ] Apply only the guard postimage to its target:

```powershell
Copy-Item -LiteralPath '.superpowers/sdd/2026-09-13-spiritual-journal-c2-planning/fullvalidation-budget-proposed/BookOfEternityClient.IntegrationTests/IntegrationTestBoundaryTests.cs' -Destination 'BookOfEternityClient.IntegrationTests/IntegrationTestBoundaryTests.cs'
```

The exact changed assertion input is:

```csharp
"FullValidation = @{ Project = \"Integration\" " +
"Filter = \"Category=FullValidation\" TimeoutMinutes = 30 }",
```

- [ ] Run semantic OLD RED. It compiles against the unchanged runner, then the existing guard reads that real script and fails because the required FullValidation budget is still 15:

```powershell
. ./.superpowers/sdd/2026-09-08-spiritual-journal-c1-implementation/enter-net8.ps1
./scripts/test-csharp.ps1 -Lane Focused -FocusedProject Integration -Filter 'FullyQualifiedName~IntegrationTestBoundaryTests.CSharpLaneRunner_DefinesNonOverlappingProjectRoutedPreMergeSchedule'
```

Expected: one failed assertion, clean build, ordinary bounded lane and successful cleanup. A compilation/discovery failure is not the intended RED.

- [ ] Apply the runner and documentation postimages:

```powershell
Copy-Item -LiteralPath '.superpowers/sdd/2026-09-13-spiritual-journal-c2-planning/fullvalidation-budget-proposed/scripts/test-csharp.ps1' -Destination 'scripts/test-csharp.ps1'
Copy-Item -LiteralPath '.superpowers/sdd/2026-09-13-spiritual-journal-c2-planning/fullvalidation-budget-proposed/docs/testing.md' -Destination 'docs/testing.md'
```

Complete runner change:

```powershell
FullValidation = @{
    Project = "Integration"
    Filter = "Category=FullValidation"
    TimeoutMinutes = 30
}
```

The docs postimage preserves existing parent policy and records both official and exact mixed09 timeouts, displayed-row/runtime-expansion limitations, QTE08 unstarted, distinct-source baseline, current isolated timings and the still-open performance investigation. It deliberately makes no inferred missing-case total or completion guarantee.

- [ ] Re-run the same Focused guard once for GREEN, inspect its actual TRX and summary. The test also checks all other existing diagnostic lane definitions and route selectors.
- [ ] Execute the following `-PlanOnly` acceptance/rejection checks in separate PowerShell 7 child processes so a rejected runner exit does not end the whole verification session. `-PlanOnly` skips build and test execution but performs bounded discovery for accepted plans; wait for the binary lock even for these commands.

```powershell
pwsh -NoProfile -File ./scripts/test-csharp.ps1 -Lane FullValidation -PlanOnly
pwsh -NoProfile -File ./scripts/test-csharp.ps1 -Lane FullValidation -TimeoutMinutes 30 -PlanOnly
pwsh -NoProfile -File ./scripts/test-csharp.ps1 -Lane FullValidation -TimeoutMinutes 31 -PlanOnly
pwsh -NoProfile -File ./scripts/test-csharp.ps1 -Lane Fast -TimeoutMinutes 6 -PlanOnly
pwsh -NoProfile -File ./scripts/test-csharp.ps1 -Lane Focused -FocusedProject Integration -Filter 'FullyQualifiedName~IntegrationTestBoundaryTests.CSharpLaneRunner_DefinesNonOverlappingProjectRoutedPreMergeSchedule' -TimeoutMinutes 15 -PlanOnly
pwsh -NoProfile -File ./scripts/test-csharp.ps1 -Lane Focused -FocusedProject Integration -Filter 'FullyQualifiedName~IntegrationTestBoundaryTests.CSharpLaneRunner_DefinesNonOverlappingProjectRoutedPreMergeSchedule' -TimeoutMinutes 16 -PlanOnly
```

| Check | Expected exit/result |
|---|---|
| FullValidation default | Exit 0; timeout 30; Integration descriptors for the unchanged `Category=FullValidation` selection |
| FullValidation explicit 30 | Exit 0; timeout 30; same discovery/selection as default |
| FullValidation 31 | Nonzero; exact hard-limit message naming 30; no execution plan or tests |
| Fast 6 | Nonzero; hard limit 5; no execution plan or tests |
| Focused explicit 15 | Exit 0; timeout 15; exact Integration guard selection |
| Focused 16 | Nonzero; hard limit 15; no execution plan or tests |

For each child, record `$LASTEXITCODE` immediately and its generated summary/log path. Accepted `PlanOnly` runs intentionally have zero executed tests; count them as configuration/discovery evidence only. Check that accepted plan logs contain no `Build-Integration`, `Build-Fast` or executed test descriptors, and that rejected inputs fail before discovery. Do not conflate default Fast/Focused test runs with these rejection probes; source diff and existing guards retain their unchanged five-minute defaults.

- [ ] Inspect the complete actual diff: exactly one runner value, one guard value and the intended documentation changes. Run `git diff --check` restricted to these three paths. Record OLD/GREEN/PlanOnly artifacts, preserve both earlier timeout directories and the parent's exact unfinished-selection ledger, refresh final postimages/pins, and request parent review.
- [ ] Parent owns the next meaningful Fast checkpoint. No extra FullValidation, PreMerge, unbounded tests or category moves are required solely to prove this budget edit. Refine 30 only when reconciled residual timing evidence warrants a further tracked change.

## Evidence and remaining limitations

Use `fullvalidation-budget-proposal.md`, `fullvalidation-scheduling-audit.md` and `fullvalidation-isolated-baseline-comparison.json` as the scoped evidence ledger. Official Full171812 remains a 15-minute timeout with1240 completed PASS; exact mixed09 separately timed out15 minutes without completed TRX. Neither result is relabeled by the proposed cap or residual PASS results. Both discovery logs have1810 displayed rows; the old run expanded to1920 executed PASS in12:26.460. Baseline source/binaries differ; same filters are not proof of same implementation/workload. QTE08 was unstarted in the current full run, and runtime expansion forbids deriving a current unfinished count from discovery minus executed rows.

Five current isolated groups (Example46, Mechanical24, ChaosPending23, Realm13, Quest10;116 total) pass with lower matched test-second sums than baseline. GameEngine8 PASS in6:53.7507498 lane wall,397.978 summed test seconds versus217.087 baseline test seconds. ValidatorFixture current run `20260915-183050-879-24564-3966542d59364ac992b6ed56ee1c287d-focused` completed 94/94 PASS in 11:39.5679247 lane wall time, with 683.2162373 summed TRX test seconds, exit 0, zero failed/duplicates and successful cleanup. Its actual summary and TRX were inspected. Exact mixed09 now has A149 + B227 = 376 separately executed passing cases, including runtime expansion. Those controls do not change either combined timeout outcome. Fixture683.216 + exclusive GameEngine397.978 gives an estimated serial critical path of1,081.194 seconds (about18:01) before overhead. This estimate from separate controls supports headroom above15, but is not a combined-run timing or proof that30 is sufficient. These observations support more bounded diagnostic time but neither prove the cause of slowdown nor establish30 minutes as sufficient. Raw discovery weights, coarse mixed bins, exclusive collection tail and the manifest-check stack remain separate performance leads; no scheduling or production optimization is bundled here.

- [x] Fixture-final summary/TRX inspected and incorporated into the scratch generator documentation rationale, proposal and this plan. All94 pass; wall11:39.5679247; summed test duration683.2162373s; exit0; cleanuptrue. This checkbox records evidence preparation only, not implementation or runner verification.
- [ ] Finish parent residual coverage reconciliation independently; preserve exact runtime-expanded cases and any truly unstarted selection, including QTE08.
- [ ] After migration shared-file rebasing and parent review, regenerate the three-file budget package from current checkout; verify no prior migration/category/source-guard changes are lost. Verify script/docs/Integration baseline SHA against the new pins. `specs/.../tasks.md` is not one of these targets and is not copied from any budget postimage.
- [ ] Keep performance-cause work open after any future budget config GREEN. A passing source guard or PlanOnly check proves configuration only; an actual Full run, if separately scheduled, must retain its own result and time limit.
