# B2a catalog recovery procedure

The task branch contains the current source and its evidence normally. The catalog is preserved as an exact small patch because full-catalog upload stalled. This is a publication boundary, not a statement that the current source is an untested scaffold; see `../plan.md` for current source, test and review status.

After a fresh clone, verify the branch HEAD against the latest recorded remote checkpoint and confirm the checkout is clean. From the repository root:

1. Run `git apply --check specs/1553-portable-local-storage/recovery/categories-pending.patch`
2. Run `git apply specs/1553-portable-local-storage/recovery/categories-pending.patch` once
3. Verify `git hash-object tests/categories.json` equals `ba9b41236b5df635fbf4bb151f8d5710f3b44435`

Do not apply twice to an implementation checkout whose patch is already applied. Before application, the catalog and selection intentionally describe an incomplete WIP publication. Keep the exact patch until the normal catalog blob has been published and verified, then remove this pending notice and patch. Do not retry a large upload blindly; bounded source/evidence checkpoints may continue with this recovery procedure.

Historical whole-tree proofs in the plan apply only to their named source commits. In particular, the original scaffold tree `4212f4f36a3e5fc51bb80ea27eb193bf9b96859b` was reconstructed from `017eee06ed23b05f835842e8e7da2613aa4b5eaa` after applying the patch and excluding its two recovery-only files; it is not the expected tree of later checkpoints. Sanitized newly executed summary/TRX bundles are retained under `evidence/`, with exact source/patch identity and original/sanitized hashes in each manifest.
