# Independent F1-R2 corrected DESIGN / affected-plan review

**Verdict: READY FOR USER DISCUSSION.** F1-R2-D01 is resolved as a design defect. No confirmed material finding remains in this correction and its affected dependencies. Grants, implementation and runtime acceptance remain unapproved.

Reviewed `bddf930414b12944b5383ec2857404c26f7b01a9` against previous review `1a999496fa9ff068d40a65fe439d74a0e181f44c`; complete R2 base `e7f86f590892e4c335ad29ece88c265ad94b8138`.

- Full R2 binary diff SHA256: `64fde5b9e850fc467ad26bf0754fc9e46f62f86cfe9819ee7b76ddaa8aa83ff4`.
- Correction binary diff SHA256: `e4bd778eed50b9285db1b33f36e0fbef7241cf25f24ee5099981c746acf65d2b`.

Requested and launch-configured reviewer: **gpt-6-astra / xhigh**, isolated reviewer context retained for affected re-review. Backend resolution telemetry is unavailable. Same reviewer; no implementation participation.

## Resolved and addressed items

### F1-R2-D01 — RESOLVED_DESIGN

The mandatory numeric interval was replaced with source-bound resolved_incremental, resolved_numeric and unresolved_absolute projections. Calendar-only setWorldTime is explicitly accepted without forced minutes/repair; progression fallback/report/control output remains distinct from nullable effect time. Incremental time without an absolute epoch remains accepted; absolute jumps/rewinds are not inferred elapsed durations. The no-new-command branch carries post-prefix state without replaying the old timeChange. R203 and T192/T195 now require calendar-only, numeric jump/rewind and genuine elapsed future checks.

Locations: `specs/1536-complete-wound-materialization/design/lawful-active-joint-tail-b-ordinary-outcomes.md:55-58,91-143,145-163,413`; `specs/1536-complete-wound-materialization/design/lawful-active-joint-tail-b-implementation-plan.md:421-436,456-463`.

Unchanged source: `ValidationService.QuestsRivalsFactionsAndWorld.cs:1831-1875`, `ProgressionScheduleService.MortalJointInputs.cs:84-91,125-142`, `ProgressionScheduleService.MortalJointBasis.cs:167-186`, `EffectAcceptedTurnInputComposer.cs:707-737`, `EffectLifecycleScheduler.cs:770-787`.

### F1-R2-O01 — ADDRESSED_DESIGN

The immutable actionBoundarySource now has an explicit field in S and raw B before CommonAuthority/input/prepared/cache/take sealing. Planned closure/time inputs/results belong to genuine C/common proof; mutable acknowledgement overlays are excluded from the immutable seal. Later unknown outcomes enter their later basis only. This resolves the previously implicit seal mapping without introducing a final-marker hash cycle.

Locations: `specs/1536-complete-wound-materialization/design/lawful-active-joint-tail-b-ordinary-outcomes.md:63-74`; `specs/1536-complete-wound-materialization/design/lawful-active-joint-tail-b.md:183-189,227-228,276-279`; `specs/1536-complete-wound-materialization/design/lawful-active-joint-tail-b-implementation-plan.md:425-435`.

### F1-R2-O02 — ADDRESSED_DESIGN

The proposed OrdinaryGeneratedPathSourceAuthority now explicitly depends on a signed, complete, owner-specific original catalog and confinement version. Existing paths require selected original bytes; new paths require genuine owner-covered nonmembership. Missing dictionary keys, current candidate bytes and mere declared paths are expressly insufficient. This is tracked as new D10 source/read-absence authority and a pre-activation dependency, not a claim that the current reader already supports it.

Locations: `specs/1536-complete-wound-materialization/design/lawful-active-joint-tail-b-ordinary-outcomes.md:216-231,418`; `specs/1536-complete-wound-materialization/design/lawful-active-joint-tail-b-implementation-plan.md:438-445`.

## Nonblocking precision note

### F1-R2-O03 — Name every deferred-QTE shared composer caller in T192 qualification

D9 admission must apply at the shared BuildCommonPlan boundary during acceptance identity preallocation, first selected-terminal preparation and receipt-resume recomposition, not merely at the final terminal publisher. The current proposal already gives waves/retries zero extra budget and lists QteDeferredAcceptedMechanicsPlanner in T192; Q07/Q08 and B13 also retain deferred-wave coverage. I found no contradictory permission or confirmed per-wave durable tick defect. Explicitly naming those callers and their cumulative known source/receipt proof would improve the future test packet.

Do not invent a per-wave elapsed/turn consumption or require a new gameplay event merely because a receipt wave was transported. A genuine event budget acknowledgement belongs with the owning actual publication; pure recomposition must preserve the same accepted source and cannot mint a second budget.

Static source evidence:

- QteDeferredAcceptedMechanicsPlanner.cs:74-91,173-185,249-299,302-364,588-635
- QteDeferredAcceptedMechanicsPlanner.cs:438-486 only emits mechanical after-images when !AwaitsPendingResolution; pending plans emit pending/consumed controls.
- QteSceneService.cs:1805-1865 dispatches first/resumed pending wave handling from those prepared plans.

This note is not a confirmed runtime defect and does not prevent discussion of the corrected proposal. Runtime checks remain UNRUN.

## Verification and scope

- **PASS_STATIC:** HEAD equals reviewed commit and working tree is clean; no repository changes by reviewer.
- **PASS_STATIC:** git diff --check previous reviewed -- passed. Correction diff contains six proposal/status documents plus the exact immutable initial MD/JSON reports; production/tests/tools/catalog/selection/governance remain unchanged.
- **PASS_STATIC:** Initial /tmp report pair and recovery copies are byte-identical and retain their original SHA256 values.
- **PASS_STATIC:** All 23 original source pins remain unchanged at base/previous/reviewed; two additionally consulted source files are also unchanged (25 pins total below).
- **PASS_STATIC:** Static inventory still has 182 exact union rows (169 DTO / 176 mapped / 83 destination paths / 13 raw-only), no route/path mismatches, 18 unique R2 matrix IDs and 21 unique unchecked T178-T198 tasks.
- **PASS_STATIC:** D01 future calendar-only/resolved-absolute/elapsed cases are named in R203/T192/T195; O02 source-absence negatives are in R208/T193. Existing category-only, Late2/independent evidence and actual platform/DOM requirements remain future gates.

The preserved governance/test policy was checked for changes and is unchanged. No runtime, build, tests, discovery, repository runtime tools, GM, console/browser, DOM/visual, Linux or Windows execution occurred. Static source inspection and hash/text checks are the only evidence. Parent-reported remote readback and GitHub-only recovery were not independently repeated.

Correction changed files:

- `specs/1536-complete-wound-materialization/design/lawful-active-joint-tail-b-implementation-plan.md`
- `specs/1536-complete-wound-materialization/design/lawful-active-joint-tail-b-ordinary-outcomes.md`
- `specs/1536-complete-wound-materialization/design/lawful-active-joint-tail-b.md`
- `specs/1536-complete-wound-materialization/plan.md`
- `specs/1536-complete-wound-materialization/recovery/lawful-active-f1-r2-independent-design-initial-20261016-review.json`
- `specs/1536-complete-wound-materialization/recovery/lawful-active-f1-r2-independent-design-initial-20261016-review.md`
- `specs/1536-complete-wound-materialization/spec.md`
- `specs/1536-complete-wound-materialization/tasks.md`

Initial report hashes remain MD `f49aa036c66499e2f61fe82d948705f6a924f0b4cf877584dcf83a46202e35d3`, JSON `c877ba756c9893020afedfe82c37cbb6b8d20da20f1759e107db9d51b4ddba80`. The initial reports were not rewritten.

## Remaining dependencies

- Written F1/D9-D11 contract, schema, authority and affected-plan acceptance; separate implementation authorization.
- All T178-T198 remain unchecked and all implementation/runtime checks remain UNRUN. Complete per-owner catalogs, pure transforms, source/DAG composition, repair/cold/cleanup and real client/platform qualification are future work.
- T197 still requires the exact durable MortalDeath-to-LifeEvaluation target receipt/cohorts/current-generation protocol before activation; unchanged sequential handler is not a substitute.
- D4 allocator, guided US5, D8 narrow raw terminal payload/earned-XP scope and DOM/visual/Linux/Windows qualification remain open. F2 has not been selected and no F1-impossibility claim was established.

These are explicit design/release dependencies, not a claim that the current product implements the proposal. In particular, this review neither approves the LifeEvaluation handoff nor substitutes a restricted ordinary outcome family for the requested full lawful result.

## Relevant unchanged source pins

Each SHA256 matches the complete R2 base, previous reviewed commit, corrected commit and checked-out bytes.

| Path | SHA256 |
|---|---|
| `BookOfEternityClient/Models/GameResponse.cs` | `dcc178fdcb21ed5bd5d14d2fc48c03371b8f0858e0ada86d65ab9778271f27bb` |
| `BookOfEternityClient/Configuration/FileMapping.cs` | `0e657357c315ffe740983d3f9c3347475dc1907d84f059753eddcf95f707b538` |
| `BookOfEternityClient/IO/StateDistributor.cs` | `0ceecfb1f5d517673504e413f138bad5593065791f1f85a222238c7f19a3893d` |
| `BookOfEternityClient/Services/CanonicalStateNormalizer.cs` | `af3eed10ef8f253aea0aa4d4b47b3b4690a7ba81dab4163a16aef5c62bc2dab9` |
| `BookOfEternityClient/Services/CanonicalStateNormalizer/CanonicalStateNormalizer.AcceptedMechanics.cs` | `9d6a6b4c9fa688def2b1452050cf139cffac81a3e78ab2bd3717dd4c924e432d` |
| `BookOfEternityClient/Services/CanonicalStateNormalizer/CanonicalStateNormalizer.MortalLocations.cs` | `12855ca308dc2ede3c35129db38920992a7d69286aa6599ee9110a838b9b0c08` |
| `BookOfEternityClient/Services/CanonicalStateNormalizer/CanonicalStateNormalizer.InventorySidecars.cs` | `392c25ee6bce6cf92933c33ae85798466ef107095678777dec279f84d7d49ac8` |
| `BookOfEternityClient/Services/EffectAcceptedTurnInputComposer.cs` | `486eb3820a6b6706560f21987ebcc00cc6cbfff62f2f05485d1ef9602c59c753` |
| `BookOfEternityClient/Services/EffectLifecycleScheduler.cs` | `f2b2b853dc727bd7d254f705c2169537411fbc199df8b0f4489dc3b2411d8d86` |
| `BookOfEternityClient/Services/QteDeferredAcceptedMechanicsPlanner.cs` | `8558d2df4ad3d2362361c9b1ff3ae38656432711a5bf39487aacfbf47ad7a18f` |
| `BookOfEternityClient/Services/ProgressionScheduleService.cs` | `d0cb8cbf0b6b67f7ca8f351847817fa2d5ad6e98712c8a39ecbbd18ebb7b88ff` |
| `BookOfEternityClient/Services/ProgressionScheduleService.MortalJointInputs.cs` | `893039cbe630faa690eba3273e0a495f525a9807534bea10987bb2c18f560cc9` |
| `BookOfEternityClient/Services/ProgressionScheduleService.MortalJointBasis.cs` | `7ae0231ad50a41a70ed9f8a6b592601d004b936b3210001f9c1aa50de6dbb43c` |
| `BookOfEternityClient/Services/Validation/ValidationService.QuestsRivalsFactionsAndWorld.cs` | `96042bf2e790c80932810689d5dc0ee2f13ed567c2be47487effa05c6730c0a0` |
| `BookOfEternityClient/Services/WoundAcceptedTurnPlanner.MortalJointRepairCommonPlan.cs` | `226fd4843afec175f4101373b70de20e00524d3d402c92a88018a17010105c50` |
| `BookOfEternityClient/Services/WoundAcceptedTurnPlanner.MortalJointRepairResources.cs` | `df187c38fb0ce70e3d1bb7cb155f5b09573dea08ae94811ca6999255b125aa98` |
| `BookOfEternityClient/Services/AcceptedMechanicsWoundCommonInputComposer.MortalJointRepair.cs` | `a239d38f87b345929e95b52b4c3df4cb5363d01e3b4231e32cd49592dd773a93` |
| `BookOfEternityClient/Services/AcceptedMechanicsPlan.cs` | `1482737a717b071c781c8a520b92e44e32680fc85d3f56e86e9191b0f2f63fdd` |
| `BookOfEternityClient/Services/PendingTurnSnapshotReader.cs` | `11a6fe03846c74145329ad557917f8bf1bad693d1f40e3654efe0577404a5b6c` |
| `BookOfEternityClient/Services/MortalItemAcceptedEffectSourceAuthority.cs` | `46ef4f16f2c02f3fa3e5c457a88e6fd3f7f2e78a3650468e10aae8dc86d50a4a` |
| `BookOfEternityClient/Core/GameEngine/GameEngine.TurnLifecycle.cs` | `e2ea305eee08627d224e91fbf10ea274fafb0197cc0c6e54ed03fc5e7db4190a` |
| `Rules/Block_23.A.txt` | `8ed13efeaede715b857712b7591afa71b7b807da4202d3700c229e98f2e99c56` |
| `Rules/Block_2.txt` | `c08a78da541546e90b4885a77891405361d7101ef47bf44668be81d599158200` |
| `BookOfEternityClient/Services/Validation/ValidationService.NpcWorldAndMeta.cs` | `b04ec022135f62fd7336156a340dcc37bf3afef05bd5d8a4f25d1921ae788f0e` |
| `BookOfEternityClient/Services/QteSceneService.cs` | `c7c672bf7fbc6bbe47d15415d29a4b878502542bfcede000aa56f36318176836` |

**FINALSTOP — no pending writes.** Corrected immutable reports: `/tmp/f1-r2-corrected-review.md` and `/tmp/f1-r2-corrected-review.json`. Initial report pair preserved.
