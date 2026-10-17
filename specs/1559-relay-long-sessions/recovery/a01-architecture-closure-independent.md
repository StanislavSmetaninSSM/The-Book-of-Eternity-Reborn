# #1559 targeted A01 ARCH closure — 2026-10-17

**ACCEPT_ARCH_FOR_G1_TESTS_ONLY** at `25713634cdc7e33ffa48147de84e2ca9f6656674`, parent `3930bd49da3b173df6e2ba2acedfced4c628b9ce`. No remaining material ARCH blocker.

A01 is closed by the amended architecture, not by an implementation or runtime result. [Plan §D](/workspace/The-Book-of-Eternity-Reborn-1559/specs/1559-relay-long-sessions/plan.md) now assigns the two exact operational records a lifecycle independent of ordinary rollback: latest dispatch/receipt bytes survive rejection, repair and lost ACK; real generation replacement invalidates their authority. It names both engine/live snapshot owners, fixed-path admission, browser restored-settlement inventory, recoverable FilesModified, client-owned repair/validation predicates, frozen spiritual inputs and save/load ephemeral handling. Ordinary neighbors remain tracked; there is no whole-directory exemption, additional helper root, new journal or gameplay authority.

The required controls cover absent-before/created-after and preexisting/changed operational records through actual rollback; restoration/removal of ordinary neighbors; reject/repair/lost-ACK read-only reconciliation; same-binding restart; missing/corrupt receipt refusal; and archive/generation invalidation. These are concrete later tests, all currently unrun.

Independently checked the immediate GitHub delta: 18 exact recovered files / all 14 metadata paths, including hashes, lengths, blobs, modes and Git-show equality. The 21 new source pins are exact and disjoint from the unchanged original 78. Original3930 `CHANGES_REQUIRED_NARROW_A01_BEFORE_G1` MD/JSON/support are preserved byte-for-byte.

One narrow dependency note is resolved at source-readback level: actual load filtering is `SaveLoadService.Loading.cs:337`, which calls `IsEphemeralArchivePath`; the main-partial `DeleteEphemeralArtifacts` method alone is not load evidence. Independently verified its newly recovered exact bytes through Root receipt `276f424818b2fa6ebcf50b5d08fb04dfe7f9f8700352f290fa38d261c04210a2`. This makes 100 distinct baseline sources when that additive receipt is included; the published amendment inventory itself contains 99. Persist this caller binding with concrete G1 controls.

C01–C06 remain mandatory unchanged: real batch/client-reader recovery ordering; sequence/retry/expiry/ownership; total resource accounting; exact neutral framing; actual existing computer-tool exchange; and real client acceptance plus finite selection/platform separation. Proposed budget numbers and future discovered counts are not accepted measurements.

Root may persist this closure and release **G1 tests-only authoring**. Production, concrete SOURCE/FIXTURE/SELECTION/PREP and runtime releases remain separate. No test, build, producer, host or browser was run; no repository file was changed. No prior accepted family or full base was replayed.

Support: `/tmp/gm1559-a01-architecture-closure-independent-check-20261017.json` (`f27613a85dbbeb92c2e6962ea176ac8222bd53693068bf5228904d72c774445f`).
