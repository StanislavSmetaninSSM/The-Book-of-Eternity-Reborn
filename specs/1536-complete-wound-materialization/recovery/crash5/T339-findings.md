# T339 — genuine cleanup component measurement

One reviewed Windows attempt passed in **17.0272496 seconds**. Compiled source `69cb4714f713780a0e49bbdc3aa37a40d654a0af`; metadata/release `1936db2c4e6747318aaab69dfd03a09017025a4a`. This validates the captured DATA on a fresh real manager/lease/existing generation and participating operation. It does not reproduce the historical coordinator, worker inventory, GameEngine/game loop, remote pipeline or full cleanup publication.

| Component | Wall ms | Process CPU ms | Nested completed-proof validations |
|---|---:|---:|---:|
| Current original | 197.3082 | 203.125 | 0 |
| Manifest DTO parsing/read | 38.7395 | 62.5 | 0 |
| Applied phase | 2241.0610 | 2421.875 | 1 |
| Already-appended story | 9065.0954 | 11421.875 | 8 |
| Existing projection | 3696.9635 | 3843.75 | 3 |
| Terminal signal | 17.2603 | 15.625 | 0 |
| Destructive-original/backup DATA | 333.6989 | 359.375 | 0 |
| Read-only state aggregation | 113.9413 | 140.625 | 0 |

Setup/import/acquisition/oracle capture:1006.7628ms. Canonical owner close:3.0210ms; owned cleanup:53.4857ms. These costs and component scopes do not sum to the entire test duration: bound-operation entry/finalization, final image oracles, reporting and test infrastructure remain outside individual component scopes.

The story owner reads its authenticated source twice: initial4850.2637ms and retained readback4212.1338ms. Its applied-before/after and continuation-binding scopes account for8699.9640ms (95.9721% of story wall time in this sample). They are serial siblings; their own nested proofs must not be added again. Eight completed-proof validations occur inside story, three inside projection, one in the explicit applied read. Repetition is measured; this alone does not prove any fence redundant or removable. The T325 transient pending-wave ABA counterexample remains binding.

The complete profile contains6082 ordinary-read invocation scopes:4577 synchronous and1505 asynchronous. Story contains2900+892; projection1215+415; explicit phase381+113. There are1109 asynchronous opened-snapshot scopes across the profile. These are managed instrumentation counts, not native syscalls; actual returned byte totals remain UNKNOWN. CPU is process-wide and can include multiple threads and other test-host work. No OS/scanner explanation follows from it.

All80364 timing records were retained, with40182 completed intervals, no unfinished interval, no sequence gap, no parse error and no diagnostic storage failure. Direct UTF-8 JSONL is14839962bytes, SHA256 `6563A1A0A662884AEC1ACCA0882412DDC2D4C014052B48A93B67658EA6E8E7DB`; writer closing is confirmed, without a pipe-EOF claim. Full raw19indexedfiles/CRC/hashes are in `T339-profile-raw.zip`, SHA256 `45EE11C191B71A8E59C23CC5C660FC64EFE5A29A9B061D25A57A06EF125B814A`. Full intervals/aggregates are in `T339-timings.json`; per-component counts are in `T339-component-map.json`.

Game byte/directory and existing-generation oracles passed; the immutable archive and4DLLs+4copies stayed unchanged. The real lease closed, and the unique owned root was removed and independently observed absent. Story returned AlreadyAppended with a genuine proof; projection returned ExistingVerified. No append, repair, publication, deletion runner, generation bootstrap or GameEngine construction was used.

The minute-scale original hang is **not reproduced or explained** by this successful component run. The next useful source investigation is the exact read-only AlreadyAppended story path, its retained readback/continuation proof boundaries and the runtime/remaining-cleanup work absent from this profile. Establish which contracts and mutation windows each repeated proof protects before proposing any reduction; no cache or check removal is justified by these timings alone. No unchanged profile retry, generator/rebind, original/five or Linux run is released by this result.
