# B2a catalog recovery procedure

The task branch contains the current source and its evidence normally. The catalog is preserved as an exact small patch because full-catalog upload stalled. This is a publication boundary, not a statement that the current source is an untested scaffold; see `../plan.md` for current source, test and review status.

**Additional untested menu WIP:** after the catalog step below, apply `menu-implementation-pending.patch` exactly once with `git apply --unidiff-zero` (first use `--check --unidiff-zero`) and verify all ten source blobs listed in `menu-implementation-pending.json`. The active implementation checkout already has both patches applied. These source bytes have not been built or tested; the latest observed RED evidence is in the plan. The authorized native publication step will store both source and catalog normally, then retire these pending instructions/patches.

After a fresh clone, verify the branch HEAD against the latest recorded remote checkpoint and confirm the checkout is clean. From the repository root:

1. Run `git apply --check specs/1553-portable-local-storage/recovery/categories-pending.patch`
2. Run `git apply specs/1553-portable-local-storage/recovery/categories-pending.patch` once
3. Verify `git hash-object tests/categories.json` equals `161d1afde1d97bc3d158863c92b51d90eaec48c9`

Do not apply twice to an implementation checkout whose patch is already applied. Before application, the catalog and selection intentionally describe an incomplete WIP publication. Keep the exact patch until the normal catalog blob has been published and verified, then remove this pending notice and patch. Do not retry a large upload blindly; bounded source/evidence checkpoints may continue with this recovery procedure.

Historical whole-tree proofs in the plan apply only to their named source commits. In particular, the original scaffold tree `4212f4f36a3e5fc51bb80ea27eb193bf9b96859b` was reconstructed from `017eee06ed23b05f835842e8e7da2613aa4b5eaa` after applying the patch and excluding its two recovery-only files; it is not the expected tree of later checkpoints. Sanitized newly executed summary/TRX bundles are retained under `evidence/`, with exact source/patch identity and original/sanitized hashes in each manifest.

## Replaceable environment observations

A bounded read-only diagnostic on 2026-10-01 did not support ordinary /tmp age-based cleanup as the cause of the old workspace disappearance. Actual directory removal, a changed mount/filesystem view, and lifecycle replacement remain unproven alternatives; no daily-reset evidence was found. Current scratch remains replaceable. The original diagnostic SHA256 is `b52e81e50254039c97d8818a4c9e12b80b5d19e395779fa15071fb477475c95f`; its unrelated inventory is not published here.

For future checkpoints retain a small role-based environment/canary fingerprint, as in `environment-fingerprint-20261001.json`: observation UTC, source/verified remote SHA, existence, filesystem kind and device/inode/timestamps. A non-secret local canary supplements these observations; it cannot replace remote commits or prove a historical deletion actor. No daemon, timer, cleanup exception, permission or security change is involved. Interpretation references: [inode timestamps](https://www.man7.org/linux/man-pages/man7/inode.7.html), [mount namespaces](https://www.man7.org/linux/man-pages/man7/mount_namespaces.7.html).
