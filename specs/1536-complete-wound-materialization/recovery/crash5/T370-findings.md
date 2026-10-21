# T370 — named guarded-read component findings

Independent actual-result review pending. Investigation only: T365 consumed FAIL remains unsatisfied, T366 closed.

One actual matrix at accepted/recoveredba2ff07895174544b2c94a7cd91d95833b8d49b8; unchanged game sourceffa87dcba1e6f1a1c7cb107a9427692e2d806543. Command1.9004912seconds, harness1.6099617seconds including setup/closing. Complete576measured+12warmup calls,1378continuous outer rows,7938buffered internal events. Exact schedule/root-first24/24/method-position8, payload2048bytes, inventory/closing, runtimeCore/probe/ILogger and20new+31preserved pins qualified. Both exact newly owned roots absent. No game/world/explicit-lease/source42/Linux execution, main/test rebuild, overwritten repository assembly or private reflection. All10raw members archived before analysis, CRC/size/SHA/archive/index verified; every parsed input comes from archived bytes. Diagnostics uncapped.

Mean milliseconds per measured call (48calls per cell); user reports C SSD/D HDD:

| Drive | Plain async | Guarded async | Observed async | Plain sync | Guarded sync | Observed sync |
| --- | ---: | ---: | ---: | ---: | ---: | ---: |
| C: | 0.299423 | 4.292958 | 4.267406 | 0.178890 | 3.761833 | 3.759500 |
| D: | 0.290515 | 2.641965 | 2.592179 | 0.162544 | 2.413127 | 2.416454 |

Observed named sections, sums divided by48outer calls. HasPending is called twice per read; its child StorageEvidence.ProbeAndEnumerate runs four times per read. These columns overlap: StorageEvidence is nested within HasPending; do not add them.

| Drive | Method | HasPending total per read | StorageEvidence total per read | OrdinaryRead total per read |
| --- | --- | ---: | ---: | ---: |
| C: | async | 2.360225 | 2.350473 | 1.877075 |
| C: | sync | 2.183712 | 2.175440 | 1.555142 |
| D: | async | 1.925044 | 1.916123 | 0.640806 |
| D: | sync | 1.880069 | 1.871775 | 0.515771 |

This localizes a concrete sampled cost: two pending-publication checks account for a substantial portion of the guarded public/lease-free text wrapper, on these tiny no-journal fixtures. Source inspection matches FileSystemManager.HasPendingFilePublications short-circuit OR of local and physical evidence roots, checked before and after reads; each storage root uses PhysicalFileAuthority.ProbeStorageEvidenceNamespaceEntryFromRoot then enumeration if ordinary. Both temporal checks remain required across the actual read/await. No evidence yet proves that merging any checks preserves the original refusal/race behavior; a new correctness-preserving design and regression tests are required before production changes.

Every observed async call has17closed named timing scopes and12managed metadata attempts; sync has13scopes/9attempts, including warmups. Metadata counts cover only attempts emitted by the existing WindowsPathMetadataGuard observer, not all filesystem metadata calls elsewhere in the read graph, native syscalls or primitive durations. All callback sequences/nesting/ticks closed and matched; raw paths are preserved. NamedInclusiveSections/ManagedMetadataAttemptGroups/EveryObservedCallCounts retain all groups; full events remain raw.

Observed-minus-unobserved means vary slightly around zero in this interleaved sample, including negative values. This measures sampling variation plus possible observer perturbation; it does not establish zero observer cost and must not be subtracted as an exact correction. Callback buffering and post-API serialization avoid disk writes within the external observer, but internal timings remain inclusive and overlapping. No disk throughput, OS/scanner cause, full-game bottleneck share or predicted application speedup is established. Earlier T368 and T362/T365 evidence remains separate and unchanged. Next investigation/design may examine sharing common ancestor/root admission inside a single synchronous HasPending check, while preserving independent fresh pre/post-read checks, short-circuit/refusal ordering and no cache across awaits or calls.
