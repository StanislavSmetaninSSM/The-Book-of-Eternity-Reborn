# T284 — short physical snapshot component: measured findings

Status: component PASS; investigation T281–284 completed, independent T285 review/recovery pending. Original Windows five remain **0/5 PASS**, T279 seeded cut0 remains **FAIL (resumer600s)**, Linux remains **UNRUN locally**. No full crash, Stage43, source42 generation, Linux runtime or production optimization was executed in this slice.

## What was measured

Runtime source/evidence checkpoint `b1204998a12ae8a7d58a0ba92735f3662433c717`; compiled game/test source `f479d42a72f7412871e797575a1b1bd2c6dabd07`. Throwaway .NET8 diagnostic source is retained under [diagnostics/spec13](diagnostics/spec13/); its separate harness SHA256 is `17FA2257AA11303AF60DD33C46328C90FB09853547C4A8198D5A86A6CDD7E6B5`. Three game/test assemblies retained their accepted hashes; no game/test rebuild or compiled-source change occurred. The exact415-byte source42 soul input retained SHA256 `19E66A7A2059C8F569862E55FE728B6A6A3BA62C93FDBE42265E821B7DE68430`.

Both roots were newly owned tiny ordinary fixtures. The actual engine created its rollback object and canonical baseline; the harness passed that real object unchanged, then acquired a new ordinary canonical lease. No captured old owner, fabricated authorization, full game turn or resumer was used. Fresh session/request/token bindings, turn1/MortalWorld, exact selected soul bytes and9 observed optional absences passed on every successful projection.

| Genuine original / rollback rows | Successful ReadSnapshot calls | Observed content-open callbacks per call | Mean call, ms | Range, ms | Raw16 reads, ms | Leased async16 reads, ms |
| --- | --- | --- | --- | --- | --- | --- |
| 1 / 1 | 8 | 5 | 29.862 | 21.223–68.643 | 2.1285 | 262.6289 |
| 17 / 17 | 8 | 21 | 58.278 | 52.669–77.634 | 1.8245 | 263.0693 |

All8 calls in each root had the same observed path population. It consisted of manifest + detached authority + every physical rollback backup + matching request + selected soul snapshot copy: **rollback rows+4**. Adding16 unselected lore files increased each population from5 to21 while the selected result still exposed one file. The positive game_session inventory and all hashes were unchanged. The16 raw controls returned exact bytes without production hook calls; the16 explicit leased async controls returned exact bytes and exactly16 soul callbacks each.

The expanded-root negative appended one space only to the actual rollback backup for `lore/probe/member_00.json`. The next actual projection rejected `mortal_qte_projection_authority_invalid: pending_turn_snapshot_reader_authority_invalid@game_state/control/pending_turn_snapshot.authority.json`. Manifest and detached authority bytes remained unchanged; the physical inventory comparison confirmed exactly that one backup changed. Nothing was resealed or repaired.

## Scope of the conclusion

The experiment confirms **repeated full rollback-map authentication even for a narrow selected result** on this genuine component path. It separately shows that the ordinary leased primitive is substantially more expensive here than direct cached file reads. It does not isolate which internal lease/path-validation operations cause that cost, or attribute it to Windows, security software, storage, CPU or network.

Callbacks observe the canonical initial-open boundary; they are not a count of OS system calls, disk operations or successful whole reads. The corresponding positive calls separately passed exact output checks. Three source-level existence probes are not content reads and are excluded. Per-call timers cover the production projection invocation; binding/output assertions happen afterward. Raw/leased timings cover their16-call loops including exact-byte checks. The negative's raw elapsed field includes subsequent invariance checks and is not a projection-only timing.

Only two tiny fixtures were measured in fixed order in one child; cache/JIT/environment effects were not controlled as a benchmark. Do not extrapolate these timings or the earlier conditional64-read source bound to all600s. T279's last limited activity sample does not identify the final active method; its unsampled final approximately284s remain unknown. No infinite loop or security-policy cause is established.

The next justified work is a separately tracked bounded investigation of repeated canonical/lease validation within one real operation, with explicit phase boundaries and counts. Any reuse of a validated view must preserve lease ownership, physical rollback tamper rejection, writer invalidation and authority revocation; the existing borrowed-validation scope is not an automatic caching fix. This finding does not authorize removing checks or implementing production caching in spec13.

## Ownership, failure history and evidence

Exactly one target-measurement child executed: PID22652, `.NET 8.0.31`, child **9.867407s**, original-owner parent **10.121652s**, below unchanged30s/120s bounds. Original production Windows Job attached before the release gate; StopAndObserveAsync confirmed complete scope/no retained authority/exit0; EOF, join and owner disposal passed. Both fixture leases and audio services disposed. The measurement root `C:\Temp\boe-spec13-probe-3176a936662c4723b91e04c2c54c1d14` was removed and independently observed absent. Historical T219 remains present and untouched.

An earlier setup child failed in diagnostic JSON BOM parsing before the first targetReadSnapshot and before lease acquisition. It is **not** a failed measured component or a successful probe. [T284-setup-failure.json](T284-setup-failure.json) and its original ZIP/attempt guard remain preserved. The strict UTF8 parse-view correction changes no signed input bytes; pure metadata controls accepted plain/BOM input, preserved both arrays and rejected malformed UTF8. The explicitly recorded S13-3 setup exception permitted one corrected measurement, with a separate consumed guard; an executed target measurement is never retried.

The earlier setup root `C:\Temp\boe-spec13-probe-083fc595ea1546119b1fcf69f143b998` remains deliberately retained: an automatic tool-policy check rejected a command containing its deletion before execution. No alternate deletion was attempted. This is distinct from confirmed process shutdown and from successful cleanup of the new measurement root; it does not explain the historical cloud blocker.

- [T284-outcome.json](T284-outcome.json): exact attempt, parent, raw child records, measured arrays and indexed archive members.
- [T284-probe-raw.zip](T284-probe-raw.zip): raw streams/parent/guard, compiled throwaway harness and XML, exact diagnostic source, build/metadata logs, source references and immutable input. ZIP SHA256 **3C9D1DB28646810F223A9028D0A6D33B52B5A5BB1E76FA26AEB621E7CEAA6BFB**, 64763bytes; CRC, distinct paths and every indexed member hash checked.
- [T282-source-localization.md](T282-source-localization.md): pre-execution call map and rejected empty-rollback proposal, retained as design history rather than rewritten as runtime evidence.

Final independent review and clean GitHub recovery are recorded separately by T285; no issue closure, CI/PR/merge or external message is part of this slice.
