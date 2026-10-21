# T374 — nested ordinary-read section findings

Independent actual-result review pending. Diagnostic change only; no performance improvement or game outcome established. T365 remains consumed FAIL/settled and T366 closed.

One actual matrix at accepted/recovered a037bd312d765b4862bfa9b871280be9e3daf403; current compiledsource f62087f51c36efaf8f8d000445983079566f3f7f, actualCore47B9C7C088CAE2EC503FA3CE2AB79D4F48D0DF72E74A82682D1B9DC78174710E. Command2.6406438s/harness2.3720587s. Complete576measured+12warmup actual2048byte text calls,1378continuous outer rows,11662complete buffered callbacks (4802closed scopes/2058WindowsPathMetadataGuard attempts/196observed calls). All payload/inventory/schedule/runtime/60original+20probe pins qualified; both exact new roots removed. Raw10indexedfiles archived before interpretation; archive/index/CRC/size/everymemberSHA checked and archived inputs alone parsed. No diagnostic quotas.

Selected nested per-call means in milliseconds (48measured calls per cell). User reports C SSD/D HDD. Each table entry is one named section's observed mean; parent/child scopes overlap, so do not add arbitrary rows.

| Section | C sync | D sync | C async | D async |
| --- | ---: | ---: | ---: | ---: |
| ordinary total | 2.192263 | 0.692558 | 2.862548 | 1.032596 |
| resolve path | 0.221688 | 0.066802 | 0.245265 | 0.073548 |
| construct scope | 0.301023 | 0.068467 | 0.309525 | 0.083177 |
| admission canonical safety | 0.209665 | 0.041583 | 0.228742 | 0.049842 |
| admission scope validation | 0.328669 | 0.077098 | 0.358656 | 0.091565 |
| presence | 0.059335 | 0.008990 | 0.060831 | 0.010340 |
| open scope validation | 0.313748 | 0.072038 | 0.348952 | 0.087625 |
| FileStream constructor | 0.143194 | 0.133369 | 0.170629 | 0.167744 |
| completion scope validation | 0.331190 | 0.093771 | 0.443319 | 0.130940 |
| completion canonical safety | 0.195400 | 0.043287 | 0.242588 | 0.055565 |

Actual opened-handle copy means: C/D sync HookAndCopy and C/D async CopyAsync are recorded below; sync section also includes the existing hook, timestamp and buffer preparation, so these two section families are not identical primitive operations.

- C:: sync HookAndCopy0.052008ms; async CopyAsync0.134810ms.
- D:: sync HookAndCopy0.049685ms; async CopyAsync0.132442ms.

The sampled C/D difference is concentrated in path resolution, canonical safety, scope construction and scope validation; actual FileStream opening and small opened-handle copy are much closer. These are named application sections, not a measured native syscall, filesystem-driver or scanner attribution. Scope validation performs fresh ancestor/leaf metadata checks; no check was removed. Hardware media type alone does not explain these warmed2KB results, and this is not a storage-throughput ranking.

All82named groups,12root/method distributions,36wave distributions,8observed/unobserved/plain contrasts and12metadata groups are saved in T374-readprofile-analysis.json; every event/path remains raw. Each observed async call has27closed scopes/12hook metadata attempts, sync22/9. Hook counts exclude metadata APIs reached elsewhere. Diagnostics are optional and exception-isolated; callback events are buffered and serialized outside API measurement. Observed-minus-guarded variation includes negative values and does not prove zero diagnostic cost.

T370 used the same harness workload at the older compiled source, but occurred at a different time with fewer timer callbacks. Current plain-control means also differ. Absolute before/after time changes cannot be assigned to the additive instrumentation or a performance regression/improvement from these two separate samples. Earlier T365 had56279sync/21700async ordinary reads and approximately10.35seconds in HasPending; none of the current tiny-fixture means establish an exact600second game-timeout share or speedup. Original proof/generation/lease/freshness/mechanics/EOF/join/closing/inert-repeat oracles remain unchanged and unqualified where never reached.

Historical source42 package remains source-bound toffa87 and explicitly unqualified for currentf620; no rebind, generation, crash or Linux execution. Further work should choose a separately tracked, measured intervention with preserved contracts, such as explicit owned-fixture storage placement or a proven narrower validation change. Simple retained-root batching was rejected by the source consultation because of permission/creation/retention differences; no such cache or shortcut is implemented here.
