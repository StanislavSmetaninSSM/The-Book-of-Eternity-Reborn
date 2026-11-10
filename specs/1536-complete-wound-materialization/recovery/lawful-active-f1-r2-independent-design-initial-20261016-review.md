# Independent F1-R2 DESIGN / affected-plan review

**Verdict: CHANGES REQUESTED.** One confirmed material contract mismatch (F1-R2-D01). This is a static design review, not implementation approval or runtime qualification.

Reviewed `1a999496fa9ff068d40a65fe439d74a0e181f44c` against `e7f86f590892e4c335ad29ece88c265ad94b8138` on `1536-wound-b2-cloud-20261012`. Exact `git diff --binary e7f86f590892e4c335ad29ece88c265ad94b8138 1a999496fa9ff068d40a65fe439d74a0e181f44c --` SHA256: `f2650aabe99df5e24dbc3768d2af5d1e0e2b8bf62c9217aaf3184388dd687070`.

Requested and launch-configured reviewer: **gpt-6-astra / xhigh**, isolated context. Backend resolution telemetry is unavailable; configuration is reported from the parent launch/task, not independently measured backend telemetry.

Read AGENTS.md, docs/development-workflow.md, .specify/memory/constitution.md, relevant #1536 spec/plan/tasks and all three B design documents, plus docs/testing.md and relevant tests/categories.json entries as static documentation. The review used read-only Git/source inspection and static text/hash checks. No repository files were changed. The parent supplied additional focus questions during review; those were independently checked rather than adopted as findings.

## Confirmed material finding

### F1-R2-D01 — Preserve accepted unresolved absolute world time (P2)

**Location:** `design/lawful-active-joint-tail-b-ordinary-outcomes.md:55-57,79-90`; affected T192/T195 at `design/lawful-active-joint-tail-b-implementation-plan.md:421-434,460-461`, relative to `specs/1536-complete-wound-materialization/`.

**Scenario:** After declining, a genuine ordinary result resolves a plot-driven time skip with a complete calendar-only setWorldTime (year, monthName, dayOfMonth, timeOfDay), with no currentTimeInMinutes. This is a currently lawful ordinary command, including when the client cannot convert that calendar to numeric minutes.

**Requirement:** Every currently lawful ordinary outcome must resolve through its actual owners; decline must preserve their existing time behavior without unsupported refusal, invented elapsed time, or new validation requirements.

**Defect:** R2 stores time as prefix/remainder {from,to} intervals, says to normalize canonical [from,to] before publication, and routes an uncomputable interval to ordinary validation/repair semantics. The actual ordinary contract has an accepted unresolved/fail-open result, not that repair requirement. The declared closed shape does not define an accepted unresolved projection. Generic promises to preserve resolver semantics in T195 do not resolve the contradictory concrete interval contract.

**Consequence:** A faithful implementation of the stated interval/repair branch would reject or stall a lawful result, or invent a numeric/zero interval to complete it. Both violate the full ordinary-outcome requirement and can alter effect/progression behavior.

**Correction:** Make the time projection explicitly distinguish resolved incremental/absolute time from an accepted unresolved absolute command. Retain raw source bytes and each genuine owner's actual result, including progression fallback/report handling and nullable effect CurrentTime. Require numeric intervals only when the actual source resolves them; do not infer elapsed duration from an absolute jump or add a calendar-conversion policy implicitly. Add a future acceptance case for calendar-only setWorldTime, plus resolved absolute/rewind and genuine elapsed cases. Any stronger conversion policy is a separate proposed dependency/grant.

**Static source evidence:**

- ValidationService.QuestsRivalsFactionsAndWorld.cs:1831-1875 validates the complete calendar command and explicitly states at 1870 that currentTimeInMinutes is optional and the scheduler must fail open when it is absent.
- ProgressionScheduleService.MortalJointInputs.cs:84-91 skips ValidateMortalOutcome for HasUnresolvedAbsoluteOverride; ApplyMortalOutcomeFromInputs at 125-142 takes the maximum of existing fallback/report/control values instead of requiring repair.
- ProgressionScheduleService.MortalJointBasis.cs:167-186 preserves the unresolved flag when a complete absolute date has no numeric minutes.
- EffectAcceptedTurnInputComposer.cs:707-737 yields null numeric time for such a command; EffectLifecycleScheduler.cs:770-787 leaves an until_time lifetime unchanged when CurrentTime is absent.
- Rules/Block_23.A.txt:19-40 and Rules/Block_2.txt:668-679 explicitly define calendar-only setWorldTime, including plot time skips and time travel.

This finding is confirmed from the current contracts and source. No runtime reproduction or test execution was attempted. Add the calendar-only accepted case to the future matrix; executing it is outside this task.

## Hypotheses / optional design precision

### F1-R2-O01 — Make the immutable action-boundary seal relationship explicit

The old closed basis field list does not name action inputs, while actionBoundary is a sibling containing mutable acknowledgements. R2 nevertheless requires source capture to bind the action capability, and the plan requires S/B/C binding before CommonAuthority/input/prepared sealing. I did not confirm a bypass or an impossible DAG: those provisions can cover the immutable action inputs through S and the common proof. Before accepting D9 schemas, explicitly state which immutable boundary/roster/time inputs enter S/raw basis/common proof, which fields are mutable overlays, and how cold reconstruction validates each. This is a precision risk, not a confirmed material defect in the current dependency proposal.

Source: `WoundAcceptedTurnPlanner.MortalJointRepairCommonPlan.cs:81-102`, `AcceptedMechanicsPlan.cs:2008-2009,2037-2047`.

### F1-R2-O02 — Specify original absence evidence for reply-dependent generated paths

A new item/content/location-related path named only by the reply cannot gain absent-before authority from a missing dictionary entry or from its current submitted bytes. PendingTurnSnapshotReader distinguishes selected bytes, authenticated absence, and mere declared paths. R2 already requires exact owner catalogs/absence maps and makes their reconciliation a pre-grant T193-T195 dependency; it does not explicitly authorize that shortcut. Add this concrete boundary and a future new-path negative case to the dependency deliverable. No current runtime defect or unconditional coverage failure was established.

Source: `PendingTurnSnapshotReader.cs:648-669,961-968`.

## Architecture, scope and dependency assessment

- The design now distinguishes request identity from game action/turn n, preserves source-prefix effects, requires real Plan/ValidatedEmpty ordinary completion and rejects treating missing family support as successful HOLD.
- FD reservation, atomic FT, child-original-to-FD-to-parent proof, retained artifacts, candidate-only repair, shared M acknowledgement and same-turn separate story witness are addressed as proposed work; no current implementation proof is claimed.
- The 182-name table is accurate as a DTO/mapping coverage inventory. It correctly does not treat mapped names or paths as Mortal write authority; canonical-only and dynamic companions remain owner-catalog reconciliation work.
- The universal pure composite is substantially wider than the former three-family F1. T192-T198 and D9-D11 expose that scope instead of claiming existing StateDistributor/normalizer or lifecycle handlers are sufficient.
- T197 honestly requires a separately designed durable MortalDeath-to-LifeEvaluation target receipt and exact cohorts before activation; existing handler source is sequential and is not represented as an existing durable acknowledgement. The actual destination LifeEvaluation turn remains genuine.
- D4 allocator, guided US5, D8 narrow raw-payload/earned-XP limits, actual DOM/visual and Linux/Windows qualification remain open. No demonstrated F1 obstacle selects F2.

The explicit LifeEvaluation target design dependency is not a concealed defect or evidence that F1 is impossible. It does prevent activation until the exact protocol is accepted and qualified. The registry/compositor also remains future work: the inventory proves field coverage, not a completed universal owner implementation.

## Static verification and limits

- **Exact reviewed snapshot — PASS_STATIC:** HEAD equals reviewed SHA; working tree clean at opening and final read.
- **Scope and diff hygiene — PASS_STATIC:** git diff --check base reviewed -- exits 0; exactly six Markdown files differ. Production/tests/tools/catalog/governance diff empty.
- **Binary diff SHA256 — PASS_STATIC:** f2650aabe99df5e24dbc3768d2af5d1e0e2b8bf62c9217aaf3184388dd687070
- **Literal response inventory — PASS_STATIC:** Independent read-only Python extraction: 169 DTO top-level names, 176 FieldToFile names, 83 destination paths, union 182, 13 raw-only; exact 182 table rows; no missing/extra rows, DTO route mismatches or mapped destination mismatches.
- **Future matrix/tasks — PASS_STATIC:** 18 distinct R201-R218 table IDs; 21 unique T178-T198 tasks, all unchecked. No runtime result inferred.
- **Source inventory pins — PASS_STATIC:** All seven published inventory pins match; all 23 relevant unchanged source pins in this report agree at base, reviewed commit and checked-out bytes.
- **Catalog selection review — PASS_STATIC:** Read docs/testing.md and relevant current catalog responsibilities/exclusions/related requirements as static documents. Future narrow categories, Late2 prerequisites, actual native evidence and explicit no-full-suite restrictions are appropriate for this design-only scope. Add the D01 time case to the future matrix.

**UNRUN:** builds, tests, discovery, repository runtime tools, GM, console, browser, DOM/visual, Linux runtime and Windows runtime. No static result is a runtime PASS. Remote/ref and clean-recovery information supplied by the parent was not independently repeated.

Changed scope is exactly these six Markdown documents:

- `specs/1536-complete-wound-materialization/design/lawful-active-joint-tail-b-implementation-plan.md`
- `specs/1536-complete-wound-materialization/design/lawful-active-joint-tail-b-ordinary-outcomes.md`
- `specs/1536-complete-wound-materialization/design/lawful-active-joint-tail-b.md`
- `specs/1536-complete-wound-materialization/plan.md`
- `specs/1536-complete-wound-materialization/spec.md`
- `specs/1536-complete-wound-materialization/tasks.md`

## Relevant unchanged source pins

Each SHA256 below matches base, reviewed commit and the clean checked-out bytes.

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

## Remaining blockers

- F1-R2-D01 design correction and affected independent re-review.
- Written acceptance of the proposed D9-D11/F1 schemas, grants and plan; separate implementation authorization.
- Future exact source-catalog/DAG/owner/time contracts and T197 target handoff design before activation; all implementation/qualification tasks remain unchecked.

**FINALSTOP — no pending writes.** The immutable report pair is `/tmp/f1-r2-initial-review.md` and `/tmp/f1-r2-initial-review.json`.
