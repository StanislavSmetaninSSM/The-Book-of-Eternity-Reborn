# Retired diagnostic copies: source index and restoration

The qualified Linux checkpoint remains
`04caad9111ed1bf57f63a72517c2a45a6354d92e` (R). The current HEAD retires only
26,914 compressed/raw log and TRX copies under the two recovery roots, totaling
3,241,093,559 Git-blob bytes. This is a recoverable checkout-weight change, not
purging evidence or retiring the actual external measurement stores.

[The source index](diagnostic-retirement-index.tsv) records every removed path,
its exact blob byte length and **Git blob OID (SHA-1 including the Git header)**.
It is a restoration manifest, not diagnostic payload. R remains an ancestor;
no history rewrite, GC, branch deletion, archive re-sealing or hash rebinding is
part of this change. The accepted runtime/build results belong to R and their
recorded binary pins, not to a newly claimed runtime run of this maintenance HEAD.

## Restore an exact historical member

Obtain R metadata from GitHub with `git fetch --filter=blob:none --no-tags origin
04caad9111ed1bf57f63a72517c2a45a6354d92e` if it is absent locally. Then use
`git show R:path` (substitute the full R SHA) and write the bytes to an explicitly
chosen directory **outside the checkout**. Verify its length against the index
and `git hash-object --no-filters <external-file>` against the recorded blob OID.
Do not reconstruct or substitute a similar fixture. Opening the pinned GitHub
file at R also preserves its original directory-relative evidence context.

Historical Python checkers/JSON manifests under recovery may require these
retired copies. Their original source plus complete dataset remain at R; their
reproducibility from the latest HEAD is intentionally retired. Use R together
with its dataset, and follow the current runtime authorization/safety rules.
This document does not authorize historical probes or G01/G02 operations.
No current test/production consumer is redirected to historical raw.

## Preserved inputs and findings

All #1536 `fixtures/**`, all ordinary JSON and ZIP archives, all `/saves/` paths,
source/diff/tar/image gzip artifacts, accepted human findings and checker sources
remain unchanged. In #1553 recovery, these whole input/provenance closures remain:

- `evidence/opencode-live/current-turn-paste-refusal/**` (terminal replay/config);
- `evidence/opencode-q1/**` (startup/draft PTY input);
- `journal-fresh-cloud-20261011/performance-current-c5-baseline/chain/gc-bd38c05c2df3/**`
  (pinned rollback-layout input and provenance);
- `fixtures/**` (including source-derived v1 pending/committed journals).

The legacy Windows worker SOURCE-review Markdown/JSON pins remain unchanged.
Current Linux qualification and its raw are recorded in
[the handoff](../specs/1536-complete-wound-materialization/recovery/crash5/linux-handoff.md).
External diagnostics under `/workspace/tboe-linux-evidence-20261025` are untouched.
Initial native-control 15-second intermittency/cause UNKNOWN and cross-OS DATA questions
remain open; neither blocks unrelated product development. Windows reruns are deferred.

## Collection and clone rules

Generate diagnostics outside the checkout through `BOE_TEST_DIAGNOSTIC_ROOT`.
No record/byte quota is introduced. Commit only source, genuine fixture inputs and
compact authored findings; ignored paths do not make existing tracked data vanish.
The new ignore patterns block the retired leaf families, with explicit input
closure exceptions. A future change must still review staged paths; `.gitignore`
is not a semantic staging guard for every possible JSON or source-looking dump.

An ordinary clone still obtains reachable historical blobs, including R. Removing
HEAD paths does not shrink that history or an existing `.git`. For the lighter
full current tree use a single-branch `--filter=blob:none` GitHub clone of the
current working branch. This retains current source completeness and defers
unneeded historical blob content; explicitly opening R evidence fetches it on demand.
