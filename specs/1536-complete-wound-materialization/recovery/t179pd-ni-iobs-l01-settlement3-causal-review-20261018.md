Both frozen settlement failures have the same **fixture fault-classification cause**. The injected one-shot `IOException` reaches a real `MemberPublished` cut, but the existing storage owner verifies rollback and retries that transient error. The fixture then disarms its fault, allowing publication to succeed. Neither original reaches the intended owning compensation failure.

The original remains **native 1, 1 PASS / 2 FAIL**: complete planned 3-case selection, no timeout, 440.258s wall. `CategoryResults.Completed=false` reflects failure, not missing cases. Carrier `4406e7238ae254f74e516e67dc3d7bec7db9b295`; actual `f0bfcda832db851a2768d6af13ce60695c24a9cd`; compiled `e2d0a10f6b12cc1947ef9c9f71fd600146fcec2d`. All 11 raw/gzip/restored chains, 98 frozen pins, 9/full 1575 outputs and 18 recovered paths match.

| Case | Causal result and assertion limit |
|---|---|
| CommonFault | One fault and one capture are reached. A later common publication succeeds; runtime reaches 43 and normal story/turn completion clears the refresh. `Assert.NotNull` at Settlement.cs65 fails. RetryableHeld, retained-pending, no-common/no-effects and direct-restore assertions are **not reached**. Raw evidence shows exact Published I retained while 10 of 15 mechanical paths changed. |
| TerminalFault | ErrorPrepared, held 42 and 222-file direct/outer restore retention checks are reached first. The terminal batch then retries and returns TerminalReleased. Line 115 fails before post-terminal byte/pending-retention assertions. The failed assertion prevents the final raw snapshot/trace emission; its missing bytes are not reconstructed. |
| Terminal(false) | Original PASS with whole-terminal release and unaffected-byte preservation, retained as diagnostic evidence only. No settlement case is independently qualified here. |

The decisive code is `FileSystemManager.TrustedLocalStorage.cs:240–245`: only a known RolledBack transient failure retries, and `InvalidDataException` is explicitly excluded. `TrustedLocalFilePublication` verifies generation and every before-image before reporting RolledBack. The successful terminal return also requires the existing whole-cohort readback and prepared-claim release. This is frozen-storage retry under the existing owner, not new I projection or generic baseline restoration. No production compensation defect is established; those compensation paths still require qualification.

The smallest correction is **only `IOException` → `InvalidDataException` for `_settlementFailure`**, preserving the same message, real one-shot cut, counters, journal and every assertion. Both owning catch filters already handle it. Do not change production retry policy, make the fault persistent, relax assertions or raise budgets.

Required gates: separate candidate SOURCE; fresh BOTH/catalog plus whole 16/33/57, corrective settlement 1/2/3 and remaining 3/3/10 ZERO; saved/recovered originals and independent PREP; then one newly named corrective settlement3 actual with mandatory v2 pre/post boundaries. Retain 44 without replay. This report accepts the diagnosis and plan, **not the new candidate SOURCE or runtime**.

Native cleanup and all three GUID roots are clean. Saved v2 snapshots resolve 64/65 entries; before gap 2.676426s, after gap 2.914252s. These are finite dated observations. All further actuals remain frozen; original failures, old crash UNKNOWN and broader holds remain unchanged.

Artifacts: `t179pd-ni-iobs-l01-settlement3-causal-review-20261018.json`, `t179pd-ni-iobs-l01-settlement3-failure-independent-check-4406e723-20261018.json`, and `check_l01_settlement3_failure.py`, all under `/tmp/1536-cloud-tools/review`. The reviewer performed no build, test, discovery, observer, process-control or repository-write action.
