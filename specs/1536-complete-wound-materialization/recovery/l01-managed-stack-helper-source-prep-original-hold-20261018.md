# Independent helper SOURCE/PREP — original HOLD

**HOLD** at `33cb9a07180dd90046d17bb23043cd401cf38695`; S01 remains UNRUN and gameplay release NONE.

Original preparation is intact: 60 independent readback checks passed; all13 ZIP entries/current originals, six pins and output/dependency hashes match. Source0422f6c7 compiled without errors/warnings; metadata exit0; target starts/API calls0. Runtime8.0.32/Disable and compiled MVID `35065df3-7a06-4e1b-8b74-ac77cd578120` are bound to original bytes.

Three corrections are required:

- **S01-01:** unguarded handle/stdin/reader closes in `smoke.py` can escape before FINALIZE or lose known native results. Guard teardown and prove native0/7 observer-failure behavior with fake callbacks.
- **S01-02:** `diagnosticRequests=1` precedes collector launch. Separate launch/admission from evidenced API reach; preserve UNREACHED/UNKNOWN instead of inventing a call.
- **S01-03:** `Collect.ps1` handles post-success Emit failure as API failure7. Keep API result and output observer failures separate.

The collector-first timeout order is an acceptable documented deviation: that child is the IPC client, while the live supervisor retains its target pidfd and then immediately verifies/accounts for createdump before target cleanup. This does not prove cancellation/resumption; failed child accounting/join/EOF must HOLD. No global/name-based signalling is allowed.

The synthetic source and frame oracle are otherwise appropriately narrow: two actual TIDs, ordered known managed chains, module/MVID and nonzero tokens, private namespace/environment, no game references. Reuse of compiled outputs after correction requires an explicit old-record→corrected observer-source binding; preserve original ZIP/record/six pins and separately pin fake proofs. Do not relabel a source-only correction as a new compile.

Future gameplay snapshot remains a proposal with a concrete missing prerequisite: `JointInterfaceCrash.cs:128,144–150` buffers `ReadToEndAsync` until child exit/join, so no live custody/accepted notification is exposed. A future safe exact tee requires separate review and gameplay authorization; S01 must not alter or read that pipe.

No synthetic/game/diagnostic/native preparation was executed by the reviewer. Exact evidence hashes, source references and required corrections are in the paired JSON. M01, L0152/57, crash5 hold, oldUNKNOWN and all broader holds remain unchanged.
