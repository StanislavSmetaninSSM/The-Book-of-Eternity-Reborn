# Standalone Daren Linux — bounded PASS

Issue [#1553](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/1553), task T031-DAREN-STANDALONE-LINUX, branch `codex/1553-load-filesystem`. Accepted base `953c48f81f06ef78010bf668133e2b974934e142`. User-facing date: 2026-10-06 America/Los_Angeles. Root is the sole writer. Independent reviews used actual Sol6.1/xhigh; no Astra.

The existing standalone DarenRewardProfileFileStore now writes, checks support and restores exact profile bytes on Linux through the existing trusted-local B1 publisher. Runtime commit: `ac01f7733761e74daa258de3528f835963735016`. Its fixed adapter admits only `client_profile/qte_showcase_rewards.json` under the original active lease, main/worker/generation/session conditions. The private publisher option defaults false. Browser/legacy dispatch precedence and Windows physical bodies remain unchanged.

Missing→missing restore and support/pre-cancel checks create no generation, directory, journal or mutation hooks. Original rollback exceptions, Committed with cleanup debt, and retained Uncertain outcomes remain distinct. Fresh recovery settles the original decision without replaying a completion. No second journal, player-save protection, reward/economy/game text/model/schema/UX change was introduced.

## Verification

| Bounded run | Clean source | Result |
|---|---|---|
| Actual profile-service causal RED | `00ce5557` | 1 executed FAIL at original FileStore line113, descriptor publication exception; preparation succeeded |
| Same actual service GREEN | `ac01f773` | 1 PASS: create, better-tier update, fresh read, original one-time grant |
| Actual consumers | `9dbc67e8` | 6 PASS: public console QTE (15 actions), exact original QTE rollback/store, normalization, support/no-op/pre-cancel |
| Publication/debt/Uncertain | `dae98f3e` | 16 PASS: exact rollback/absence, retained Committed debt and post-intent authority-loss refusal |
| Original admission/cancel/failure | `f2a7e0e1` | 16 PASS: 14 new cases plus 2 changed recovery assertions |
| Affected declared browser store | `f2a7e0e1` | Exactly 4 PASS after fresh integration build, using `-NoBuild` |

Latest **41 distinct PASS cases = 37 new + 4 affected**. Historical totals are 44 executions, 43 PASS and one causal RED. No preparation failures, skips, duplicates or timeouts. Only two successful rows repeated after assertions changed; unchanged cohorts were not rerun. Final category ownership is 7 + 30 + 4.

Tests use real profile/store/QTE/rollback/coordinator and original task-dispatch/ledger/lease APIs under independent GUID roots. They cover inactive/foreign/pending authority, cold/held main refusal, worker/task purpose, typed stale generation, held-write session replacement, cancellation and undeclared browser refusal. Console terminal failure reports no completion or award. These are isolated automated consumer checks, not a live timed console/game run.

Official PlanOnly inventory confirmed 41 cases with zero execution. Fresh-build ValidateCatalog passed: 378 categories, 11,050 method/file entries, no unmapped or stale selectors, zero tests executed. No full suite was run. Evidence retains 8 manifests, 391 source pins, 37 artifacts and 15 verified gzip payloads. All 44 named fixture roots were removed; four original browser fixtures supplied generic cleanup receipts. Runner cleanup succeeded. Logical Uncertain was asserted before removing fixtures; fixture cleanup grants no success or replay authority. Historical process resources were not touched.

Actual tooling: SDK10.0.401, runtime8.0.31 (10.0.12 also installed), PowerShell7.5.4, GCC14.2.0 (Debian14.2.0-19), x86_64. Executable provenance is retained. No native compilation, frontend/protocol or GM-authored contract change occurred in this slice.

## Reviews and persistence

Independent Sol design PASS at `6565a441` resolved the absent-null restore no-op gap. Source/test-design PASS at `f2a7e0e1` resolved recovery-probe disposal/type and exact stale-generation assertions. Separate final evidence/selection PASS at **`399564194f11e2ad336023fedceb971c2f5fa699`** independently verified the hashes, counts, source continuity, cleanup, catalog and restoration.

Candidate `b97157ef6154bb9b85d69176ab0674f63b9d10ba` was restored only from GitHub HTTPS. Writer and independent reviewer compared all **20,686 tracked files** to the clone's GitHub blobs, checked clean HEAD/tree/parent, accepted ancestry, no alternates and full `fsck`. [Candidate proof](recovery/evidence/daren-standalone-github-restore.json).

The final carrier changes only verdict/task/status/handoff metadata. Its ordinary push must be followed by exact remote SHA, independent changed-file byte readback and fresh restoration of the exact tip before writer stop. The final response and owned host proof carry that exact-tip result; no recursive self-SHA commit is needed.

[Execution plan](daren-standalone-linux-plan.md) · [Qualification, source hashes and artifact references](recovery/daren-standalone-linux-qualification.json).

## Historical nonpasses and remaining scope

These exact F2 IDs remain unpassed and unexecuted here, all in `BookOfEternityClient.Tests.BrowserLocalWriteCoordinatorTests`:

- `ExecuteAsync_SessionReplacementWaitsForWholeLegacyTransaction` — original Linux timeout before callback in a legacy transaction fixture.
- `ExecuteAtomicAsync_ConcurrentReplacementWaitsForCompleteTransaction` — same original reason.
- `ExecuteAtomicAsync_LockReleaseFailureDoesNotRollbackCommittedMutation` — original descriptor-bound create-only publication refusal during legacy preparation.
- `ExecuteAtomicAsync_ExplicitLeaseWritesWithoutAmbientAuthority` — same original reason.

Actual coordinator/profile/standalone consumers have separate portable coverage from accepted T031 and this block. That does not convert the four historical IDs into PASS. No remaining Linux defect or new product/access decision was found within this bounded source map.

Full T031/T041 and the cross-platform game remain open. Systemd-user is a separate mandatory backend stage; no manager was installed or configured. Native Windows, Codex Q1/Q2, live browser/GM/provider/model requests, ordinary live game or real saves, power-loss/reboot salvage and cold guarantees remain unqualified. Existing arbitrary CLI/model profile and accepted Load UX are unchanged. No merge, force push, branch deletion, issue closure, public rollout, setup/auth/network/security change.

Stop at the reviewed and restored checkpoint. No next slice is authorized by this handoff.
