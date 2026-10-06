# T031 browser rollback Linux — bounded handoff

2026-10-06 · [#1553](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/1553) · `codex/1553-load-filesystem`.
Base accepted M1 `4d456d5d5e12289d6fc97e5b2e7a0c523207bc08`.
Runtime `56d43924f31a79a69bbc9949b9b3a2e69d8739c9`; clean tested source
`b08159ee8ea818c4339fc0554f24388a441c261d`. Sole writer/root and independent
actual Sol6.1/xhigh design/source/evidence reviewers; no Astra.

The real BrowserLocalWriteCoordinator now stages its existing atomic browser
transaction on Linux. Schema7 uses the same manifest/backup/marker/cleanup names,
original active lease and existing trusted-local publication journal; no second
journal or anti-player model. Shared Write/Append/CAS/Delete and directory changes
publish cumulative intent before member publication. Recovery settles the existing
publisher first, then the original browser transaction. Exact declared Daren profile
publication/rollback and the actual QTE reward-producing action plus read-only state
refresh pass through this same scope. Windows schema6/physical profile source remains.

Original main + worker + generation admission, held leases, UI acquisition/release/
refresh and quiescent Load guard remain conjunctive. Both actual Browser Load entries
retain the full same-response refresh bundle. Known commit/rollback remains typed
through cleanup/finalization debt; authority loss retains Uncertain/evidence and
blocks automatic continuation. No mint from JSON/PID/status, command replay or old
session resume. Original partial recovery may settle an earlier admitted member and
then retain Uncertain at a later refusal; no new all-or-none rollback promise.

## Evidence and independent reviews

[Plan](browser-rollback-linux-plan.md) ·
[qualification](recovery/browser-rollback-linux-qualification.json) ·
[run index](recovery/browser-rollback-linux-run-index.json) ·
[source review](recovery/browser-rollback-linux-source-review.json) ·
[evidence audit](recovery/browser-rollback-linux-evidence-audit.json).

- Independent plan PASS at `b4053063bb68cf8ee09036aa5777d1b67d4929b2` before code.
- Source/selection PASS at exact tested `b08159ee8ea818c4339fc0554f24388a441c261d`.
- Final evidence/metadata/selection **PASS** at candidate09dd0af5, no blockers;
  [review record](recovery/browser-rollback-linux-final-review.json).
- Fresh narrow runner: **71/71**, three descriptors, zero failures/skips;
  41 new cases +30 exact affected regressions. All41 new fixture roots removed;
  runner owned-tree/runtime cleanup complete. Only two new categories used.
- Fresh unit and integration builds at tested source; historical unbuilt unit
  binary observations are explicitly excluded from current-source qualification.
- Catalog354/11003 and PlanOnly3/71 execute0tests. Their only dirty preparation
  items were nine newly captured GREEN evidence files, with runtime/catalog pins
  equal to the clean tested commit. No full-suite or unrelated cohort executed.
- Eleven manifests,1126 source pins,57 artifacts,21 gzip payloads verified.
  Historical214 observed executions186PASS/28FAIL are not distinct acceptance:
  24 clean causal failures,3 first dirty-preparation failures,1 wrong QTE grade
  fixture oracle. One separate argument preparation failure executed0.
  First historical RED contains only one untracked SDD ledger named in the preserved
  New-paths log and is excluded from clean exact-source acceptance; later clean causal blocks and final clean GREEN supply the proof.
- Candidate `09dd0af58cbbcd2e45258ebabd67c74b1d6de34e` fresh clone from GitHub only:
  19804 tracked files byte-equal to independent fetched blobs, clean/tree/parent/
  ancestry/full fsck/no alternates/remote SHA verified,0 tests during restoration.
  Final verdict carrier receives its own ordinary push/readback/fresh restoration.

Actual environment: Linux x86_64, SDK10.0.401, runtime8.0.31/10.0.12,
PowerShell7.5.4; compiler provenance Debian GCC14.2.0-19 observed only,
no new native build/probe. Exact commands, revisions, hashes, artifacts and results
are in qualification/run index. No active test sessions or fixture resources remain.

## Historical four F2 cases — still unpassed

No execution of these four IDs in T031; no native Windows qualification:

- `BookOfEternityClient.Tests.BrowserLocalWriteCoordinatorTests.ExecuteAsync_SessionReplacementWaitsForWholeLegacyTransaction` — original F2 Linux timed out before callback.
- `BookOfEternityClient.Tests.BrowserLocalWriteCoordinatorTests.ExecuteAtomicAsync_ConcurrentReplacementWaitsForCompleteTransaction` — original F2 Linux timed out before callback.
- `BookOfEternityClient.Tests.BrowserLocalWriteCoordinatorTests.ExecuteAtomicAsync_LockReleaseFailureDoesNotRollbackCommittedMutation` — original F2 Linux descriptor-bound create-only refusal.
- `BookOfEternityClient.Tests.BrowserLocalWriteCoordinatorTests.ExecuteAtomicAsync_ExplicitLeaseWritesWithoutAmbientAuthority` — original F2 Linux descriptor-bound create-only refusal.

New distinct tests reach the actual Linux consumer and cover held replacement,
explicit held writes, exact bytes/absence, cumulative intents, recovery/cleanup,
closing/debt/authority loss and generic typed carriers. Their PASS does not rewrite
those historical failures.

## Remaining practical work and stop boundary

- **T031-BROWSER-DIRECT-GACHA-LINUX**: unchanged real BrowserAfterlifeWriteService
  direct Chaos Sea gacha → StageFileAsync long-lived pending-turn backup is outside
  this atomic root and still uses physical publication. Separate task retained.
- **T031-DAREN-STANDALONE-LINUX**: standalone console/profile writes remain physical;
  only original declared browser participant and read-only refresh qualified here.
- Accepted Load UX,2026-10-06 21:11UTC: automatically request stop; only confirmed
  stop followed by confirmed successful Load permits fresh launch. Uncertain stop/
  load halts and reports a clear message; never resume old session or replay an
  unknown command. Implement separately; current low-level refusal remains.
- Primary systemd-user backend remains a separate mandatory stage, absent/unqualified
  here; native Windows execution and full T031/T041/game acceptance remain open.
- Codex Q1/Q2, VT/renderer and real CLI/provider qualification remain separate;
  no TERM/trust prompt accepted and no provider/model request or model change occurred.

Live browser is not required for this task: automated real backend consumers are the
agreed boundary. No real saves, public rollout, service/auth/network/security/settings
changes, live GM or cold exactly-once/reboot promises. Stop here before another slice;
branch and checkout are preserved, no merge, force push, branch deletion or issue closure.
