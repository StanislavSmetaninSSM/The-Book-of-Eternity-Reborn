The first supplementary boundary snapshot is accepted as a dated observation only. The observer requires two small corrections before authoritative future use.

The original source is SHA256 `6b83a2ad35fe762164fe50e0f42974442c4fb12f20489b01449c770cb570d13a`; its exact bytes are preserved as `l01-process-census-v1-reviewed.py`. The snapshot is SHA256 `de7c55114f89c0f95c472705101b25cf57cacedb0ae861c620c8085cc7a8c31e`, covering 10:51:39.407479–10:51:39.418288 UTC. All36 enumerated PIDs have entries with stable PID/kernel-field22 identity:8 other readable,23 exited/nonexecuting,4 build infrastructure,1 observer. Known MSBuild10433:477854 and self11534:530730 match; MSBuild remains visible despite optional exe EACCES. There are no active target/runner entries, unknowns, disappeared entries or owned roots. Preserved argv hashes reconcile. This does not reconstruct an earlier boundary.

The core approach is sound: argv/stat precede optional exe, PID birth is checked around collection, known mismatches and unreadable identities block clean status, target DLL/apphost/runner/discovery classification is conservative, and only the declared new output file is written. Unrelated argv is normally hashed. No product/test/controller source is changed.

Two corrections are required:

- A FileNotFoundError during collection currently becomes confirmedGone immediately. Re-probe stat: a still-live or reused identity must become UNKNOWN; only confirmed absence may be called gone. A known identity missing because of read/parse errors must be reported UNKNOWN, not GONE. Existing unknown[] already blocks those errors, but the known-visibility label must agree.
- UNKNOWN managed-runtime rows currently retain their full argv. Keep hash/count/argv0/comm/reason for unknown or unrelated processes; retain full argv only for positively identified task/game/build entries. This prevents unrelated command arguments from entering the recovery ledger.

Neither deficient branch occurred in the supplied snapshot, so its present-time evidence remains usable. Preserve it and the original observer source unchanged; version the corrected observer separately. No repeated catalog/build or case execution is required.

The mandatory plan remains: save the reviewed observer source/hash and boundary records, qualify fresh PREP independently, and require clean authoritative observations immediately before/after each named actual alongside unchanged inventory/root/child-join/EOF assertions. Serialize modes; UNKNOWN or active prior task freezes progression. The census is a finite observation interval, not an atomic global snapshot or proof of historical absence. This review releases no actual and accepts no fresh PREP. No builds/tests, process intervention or repository edits were performed.
