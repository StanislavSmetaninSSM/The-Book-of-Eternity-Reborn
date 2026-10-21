# T368 — tiny public ordinary-read cost findings

Independent actual-result review pending. This investigation does not qualify T365 or admit T366.

One matrix at saved/recovered92f969e2308cc8fd2eafc2f00269dbce9de36515, existing game sourceffa87dcba1e6f1a1c7cb107a9427692e2d806543. Command5.419132s; harness including setup/closing3.7246362s. Complete1536measured+8warmup actual calls,2048bytes each;3094continuous trace rows, every begin matched. Exact payload/inventory, both exact newly owned roots removed,20probe and11original pins unchanged. Runtime.NET8.0.31; actualCore hash/version and shared-framework ILogger hash/path/version recorded. Full raw10indexedfiles, no quota; archive/index/CRC/all entry hashes checked before interpreting archived report/trace. Counterbalanced96C-first/96D-first per method.

User reports C SSD / D HDD. Primary within-location result (milliseconds,192measured calls per cell):

| Root | Payload | Plain mean | Guarded mean | Guarded-minus-plain mean |
| --- | --- | ---: | ---: | ---: |
| C: (SSD) | text | 0.354407 | 5.293206 | 4.938798 |
| C: (SSD) | bytes | 0.254726 | 4.928558 | 4.673833 |
| D: (HDD) | text | 0.343529 | 3.219251 | 2.875721 |
| D: (HDD) | bytes | 0.241163 | 3.111909 | 2.870747 |

All per-method/root/wave counts, mean/median/nearest-rankP95/min/max and interleaved within-root contrasts are preserved in T368-readcost-analysis.json; individual measurements remain in the raw archive. On these warmed tiny fixtures most measured guarded-call duration exceeds plain-call duration. This makes the existing validation/public-wrapper work a concrete next investigation target. The probe does not isolate metadata primitives, publication recovery, path checks, OS effects or their individual causal shares. Public calls had no timing observer or optional browser collector; no game objects, live authority or private reflection.

Read-only comparison with accepted earlier timings: T362 internal synchronous32791calls/26957.7842ms, mean0.8221ms; async12388calls/29066.1995ms. T365 synchronous56279calls/164534.3235ms, mean2.9235ms; async21700calls/171178.6900ms. These are different workloads and paths from the present public lease-free asynchronous matrix. They indicate frequent ordinary reads in the actual recovery graph, but their sums include nested work and cannot be added to each other or translated into a predicted game speedup. Original T365 still timed out at600seconds; its failed/settled evidence remains unchanged.

Limits: warmed2KB reads are not a disk-throughput benchmark. C:/Temp versus D:/Code includes path/filesystem/volume/cache/environment differences; C SSD being slower here does not establish why. Public wrapper includes publication checks; this spike does not reproduce internal synchronous/explicit-lease calls, full game recovery or the original Linux environment. No unchanged long run, bound increase, source42 regeneration, production change, T366 or Linux execution is authorized by these findings. Next scope should measure named internal validation sections on tiny fixtures before choosing a correctness-preserving change.
