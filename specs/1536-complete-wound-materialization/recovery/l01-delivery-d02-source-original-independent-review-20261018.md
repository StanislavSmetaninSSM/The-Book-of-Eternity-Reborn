# D02 composite SOURCE — HOLD

Exact candidate `96f08c8bb2d534c00bcbca4ff5a279554445d58d`. **One blocking finding: D02-S01.**60bounded static/proof checks pass; this includes successful reproduction of the defect, not source acceptance.

`phase.py:86` writes the per-command record without guarding publication failure. At that point native exit is already known. If persistence throws, callers at153/164/198 never receive the result; outer nativeExit can remainNone and finalization masks native7/124/-9 with observer failure. Twelve fake cases cover native0/7/124/-9 with ordinary, output-close and persistence paths; all four persistence cases reproduce the escaping exception. No real process was launched.

Catch that publication exception, append it to the existing result's observerErrors, and return the exact native/raw/signal result. Nonzero native retains priority; native0 with observer error holds. Preserve the original proof/source snapshot and review only this correction with a bounded fake proof. **No ZERO or synthetic admission yet.**

The rest of the bounded proposal is supported: separate integration460/unit439 closures and per-project d53/d3 compilation provenance; exact149 unchanged pins;360 input hashes; SDK ArtifactsPath with mandatory exact property/import checks before discovery; fresh root/phase gates; aggregate600s catalog, bounded PlanOnly, original60s direct csc; no restore, DLL mixing, runner edit, game or production change. The actual routing/property observations remainUNRUN until a later admitted attempt. Four B01 primary output anchors exist inside the retained unit tree, including StorageCrashHost; common423files differ only in Client/TestSupport DLL/PDB.

After correction, the only prospective SOURCE admission is the three separately invoked ZERO stages. Independent PREP must bind exact catalog/plan/prepare originals, staged closures and direct compiled outputs before synthetic0/7. The final user endpoint remains a conditional future snapshot plan and STOP before gameplay/crash5. Original124/137, L0152/57 and old66529UNKNOWN are unchanged.
