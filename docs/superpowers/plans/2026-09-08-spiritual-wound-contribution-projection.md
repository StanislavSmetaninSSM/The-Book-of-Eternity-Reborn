# Spiritual wound contribution projection implementation plan

> **For agentic workers:** REQUIRED SUB-SKILL: use `subagent-driven-development` for this bounded task, with repository TDD, independent task review, and verification. The parent owns the combined Fast checkpoint; the implementer owns only the named Focused runs. The complete source/test body exists only in the companion `docs/superpowers/plans/2026-09-08-spiritual-wound-contribution-projection.patch`; do not duplicate or improvise it from this prose.

**Source issue:** [#1536](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/1536), T080/T081-D/T087 prerequisite.

**Goal:** Project each active, source-proven version-1 spiritual-wound component carried by an exact persistent current-conflict actor into one detached typed contribution for that actor's exact current side.

**Architecture:** `EffectMechanicsSnapshot.Build` keeps its existing accepted carrier/index boundary and additionally attaches the exact immutable `EffectSourceKey` already present on each canonical effect to every projected component. `SpiritualWoundConflictContributionProjector` consumes that snapshot together with the existing sealed `EffectSourceAuthority`, `EffectTargetAuthority`, and actual current conflict root; it rebuilds participant membership internally, verifies the component against the exact source definition/wound link, then emits a read-only typed contribution collection. It never writes a conflict, effect, wound, side carrier, or `combatConditions[]` row.

**Tech stack:** C#/.NET 8, `System.Text.Json`, existing #1535 effect source/target/snapshot contracts, xUnit, PowerShell 7 lane runner.

## Global constraints

- Work only in `E:/Games/worktrees/boe-1536-wound-materialization`, starting from source commit `e720b03f` plus the planning/GM documentation and text-guard commits through `2662aaea`. Preserve the existing untracked `.serena/` directory without reading or editing it.
- Use GitHub issue #1536 and `specs/1536-complete-wound-materialization/` as authority. This slice is the T080/T087 projector prerequisite; it does not complete T081-D, T087, US3, or #1536.
- Reuse `SpiritualWoundEffectProfileCatalog`, `SpiritualWoundProjectionKind`, `EffectSourceAuthority`, `EffectTargetAuthority`, and `EffectMechanicsSnapshot`. Do not introduce another spiritual profile registry, caller-authored participant list, boolean proof flag, or free-form side evidence.
- Only the eight registered spiritual profiles project. The source must be the exact current `kind=wound` definition, its one `links[]` source wound must equal `EffectSourceKey.SourceId`, and its exact component ID/profile/priority/payload must agree with the accepted effect component.
- Resolve persistent actor kinds only through the closed existing adapter vocabulary: conflict `player|player_soul|soul -> player` (whose canonical target ID remains `player_soul`), `guardian -> guardian`, `resident|shining_resident -> resident`, `radiant_actor -> radiant_actor`, and `afterlife_actor|shining_faction_head|saref_agent|system_actor|custom_afterlife_actor -> afterlife_actor`.
- Exact/confusable duplicate participant IDs, unresolved persistent targets, wrong-realm-only targets, malformed side membership, and source disagreement reject the whole projection with zero partial contributions.
- A closed conflict (`activeConflict = null`) produces an accepted empty derived collection. An unambiguously nonparticipant wounded actor is outside this duel and is ignored. Every legal nonterminal active-conflict state (`active`, `concession_pending`, `surrender_pending`, `retreat_pending`, `ready_to_resolve`) retains projection; an unknown state or illegal terminal `resolved|repair_cancelled` under `activeConflict` rejects. Active spiritual wounds/effects remain in their persistent actor carrier in every case.
- Preserve the complete owner-relative profile payload. Resolve only `actionCostAudit` to `actionCostAudit.player|opposition` and `sideStrain` to `playerSideStrain|oppositionSideStrain`; all other axes retain their registered exact name. Do not multiply magnitude by stacks or invent evaluation/reducer mechanics.
- Returned lists are copied/read-only; source/actor records contain immutable scalars; `Magnitude` and `ProfilePayload` are cloned `JsonElement` values. No mutable `JsonNode`, carrier, authority entry, conflict node, or definition escapes.
- Do not convert these contributions into `afterlife_combat_condition`, target `spiritual_conflict_side`, append to a side carrier, or touch `combatConditions[]`. A real non-empty combat-condition sibling must remain JSON-byte-identical across projection.
- Do not implement any of the eight downstream conflict consumers, suffix scheduler, source admission, pending/recovery transport, insertion, graph suffix, preview serialization, normalizer storage, or final publication here.

## Production-caller boundary

This prerequisite deliberately has **no live production caller before Unit D**. Calling it from `AfterlifeSpiritualConflictTurnPreviewService` or a validator by loading `EffectMechanicsSnapshot.LoadAsync` would read stale canonical disk after an in-candidate wound insertion and is forbidden.

The narrow Unit D integration is: from inside the accepted `EffectAcceptedDraft` owner, after `AdvanceForWoundInsertion` and `ApplyWoundInsertion` have produced the actual current generation and before dependent suffix admission, build `EffectMechanicsSnapshot` from `workspace.ToInput()`, `identityRoot.ReadSnapshot()`, and the draft's current skill authority; pass the draft's current versioned source authority, target authority, and current conflict root to `Project`. Unit B/C must supply those live draft capabilities. This patch does not manufacture an unused draft adapter or weaken Unit A's completion/lifetime gates.

## Exact files

| File | Change |
| --- | --- |
| `BookOfEternityClient/Services/EffectMechanicsSnapshot.cs` | Add nullable typed `Source` provenance to `EffectMechanicalComponent`; `Build` fills it from the exact accepted canonical effect source. Existing manually constructed roll-test rows remain source-less and unaffected. |
| `BookOfEternityClient/Services/SpiritualWoundConflictContributionProjector.cs` | Add the pure closed membership/source verifier and detached typed contribution/result types. |
| `BookOfEternityClient.Tests/SpiritualWoundConsequenceTests.ContractRed.cs` | Add one compilation-safe semantic RED proving the accepted snapshot currently loses typed component source provenance and the projector type is absent. |
| `BookOfEternityClient.Tests/SpiritualWoundConsequenceTests.cs` | Add deterministic Fast coverage for eight profiles, five target kinds, both side-relative axes/directions, exact source agreement, membership failures, inactive/nonparticipant behavior, detachment, and independent combat-condition preservation. |

## Exact API

The companion implements:

- `EffectMechanicalComponent.Source : EffectSourceKey?`; only `EffectMechanicsSnapshot.Build` supplies it in this slice. Null remains possible only for existing internal manually constructed component fixtures and is rejected by this projector.
- `SpiritualWoundConflictContribution`, carrying `EffectId`, `ComponentId`, `WoundSource`, `Actor`, `ResolvedSide`, `ProjectionKind`, `Profile`, `Operation`, `SourceAxis`, `ResolvedAxis`, exact `Magnitude`, `Priority`, `CurrentStacks`, and exact `ProfilePayload`.
- `SpiritualWoundConflictContributionProjection`, whose `IsAccepted` is true exactly when `Issues` is empty and whose collections are detached read-only copies.
- `SpiritualWoundConflictContributionProjector.Project(EffectMechanicsSnapshot, EffectSourceAuthority, EffectTargetAuthority, JsonObject?)`.

No overload accepts pre-resolved sides, participant rows, source booleans, proof hashes, or raw contribution DTOs.

---

### Task 1: Implement the detached source-proven projector

**Files:** the four exact files above.

**Consumes:** accepted current-generation `EffectMechanicsSnapshot`; sealed current-generation `EffectSourceAuthority`; sealed canonical/current `EffectTargetAuthority`; actual current spiritual-conflict root.

**Produces:** `SpiritualWoundConflictContributionProjection` for the later Unit D suffix consumers. It does not produce a canonical after-image.

- [X] **Step 1: Establish the semantic RED without a missing-type compile failure**

Apply only the `SpiritualWoundConsequenceTests.ContractRed.cs` section from the companion with `apply_patch`.

Run from the assigned worktree:

```powershell
.\scripts\test-csharp.ps1 -Lane Focused -Filter "FullyQualifiedName~SpiritualWoundConsequenceTests.ProjectorContract_AcceptedComponentCarriesTypedSourceAndProjectorExists"
```

Expected: build succeeds; exactly `0/1` passes. The assertion reports that `EffectMechanicalComponent.Source` is missing (and, once that first gap is fixed alone, that `SpiritualWoundConflictContributionProjector` is missing). This is a deterministic typed-authority behavior RED, not a compiler-only RED.

- [X] **Step 2: Apply the complete production and behavioral test body**

Apply the remaining three companion sections exactly with `apply_patch`. Do not use `git apply`, generate another DTO, or edit Unit A's draft files in this prerequisite.

- [X] **Step 3: Run the focused GREEN control**

```powershell
.\scripts\test-csharp.ps1 -Lane Focused -Filter "FullyQualifiedName~SpiritualWoundConsequenceTests"
```

Expected: `35/35` pass, zero skips, clean build and cleanup. The 35 cases are 1 contract row, 8 registered-profile rows, 7 accepted actor-spelling rows across 5 persistent target kinds, 4 side/axis rows, 4 membership rejection rows, 4 closure/nonparticipant/source/detachment facts, 4 additional legal nonterminal-state rows, and 3 illegal/unknown active-conflict-state rows.

- [X] **Step 4: Run focused existing snapshot/source/target controls**

```powershell
.\scripts\test-csharp.ps1 -Lane Focused -Filter "FullyQualifiedName~EffectMechanicsSnapshotTests|FullyQualifiedName~EffectSourceAuthorityTests|FullyQualifiedName~EffectTargetAuthorityTests|FullyQualifiedName~SpiritualWoundEffectProfileContractTests"
```

Expected: all selected existing tests pass; no existing roll resolver constructor or behavior changes because `Source` is an init property rather than a new positional parameter.

- [X] **Step 5: Parent runs one meaningful combined Fast checkpoint after the implementation handoff**

```powershell
.\scripts\test-csharp.ps1 -Lane Fast
```

The implementer must not run this checkpoint or hold the parent lane after the Focused handoff. Parent expected result: complete Fast project passes within the five-minute bound with zero duplicate IDs/timeouts and successful owned-tree cleanup. Do not move these fixture-free tests to Integration to avoid the Fast result.

- [X] **Step 6: Review and hand off without overclaiming live integration**

Inspect the actual diff and runner `summary.json`/TRX/log artifacts. Confirm no source/target authority bypass, no partial contributions on membership/source failure, no mutable alias, no conflict/effect/wound/condition mutation, and exact deterministic output ordering by priority/effect/component. The parent authorizes one local checkpoint commit of only the four scoped files after the required Focused evidence and diff inspection. No remote operation, issue closure, branch change, or cleanup is authorized.

Report that no GM prompt/example/matrix/manifest change is required for this internal, unpublished prerequisite. Keep all live Unit D consumer/suffix/insertion/publication work and its required GM synchronization open.

## Verification classification

Only deterministic fixture-free Fast tests are appropriate here. Do not add file/lease/generation/cold-restart Integration tests until Unit B/C supplies a real source-admitted insertion and Unit D binds this projector to the owned draft. At that point Integration must prove the two-exchange same-turn contour, simultaneous sides, original dice/OD/identity preservation, rollback, cold reconstruction, and all eight real consumers; none is claimed by this patch.

## Self-review result

- All T080 projector fields and the data-model axis mapping are represented exactly.
- Source provenance is acquired from the accepted canonical effect rather than reconstructed from display text or wound arrays.
- Membership is derived internally from current conflict participants and existing target authority; no caller-authored proof surface exists.
- The plan contains no placeholder code or undefined implementation type; the full body is in the single companion.
- Scope stays reviewable by one fresh implementer and does not duplicate the concurrent source-acquisition/checker work.

## Accepted prerequisite checkpoint — 2026-09-08

Implemented at `30f0897c699bc883e38decdf6c8deb53d8f18819`, task base
`b0f20ab3658aa77975ac4690129b7708cc2c6bec`. Parent audited all four final
postimages. Production matches the companion exactly. Three test-only corrections
are reflected in the companion: keep the existing fixture's valid target-derived
identity owner kind in both tests (not invalid `afterlife_profile`), and expose
`object` in the public xUnit theory while retaining the boxed internal enum assertion.

Actual semantic RED: 0/1 at missing `EffectMechanicalComponent.Source`; final
projector Focused 35/35 and existing snapshot/source/target/catalog Focused 743/743.
Earlier incorrect-owner fixture failures and CS0051 build failure are retained
in `sdd/spiritual-contribution-task-1-report.md`, not presented as semantic RED.
Parent inspected all six actual runner summaries and TRXs, including the same
contract test ID failing then passing. Independent Sol/high review: Spec compliant,
Quality Approved, code Critical0/Important0/Minor0. Two report-only timing typos
were corrected from the exact summaries.

Parent Fast: 7849/7849 in `00:04:24.3435932`, default five-minute bound, no timeout,
skips or duplicate executions, build0/0, successful owned-tree cleanup. All 28 TRXs
were inspected: 7849 unique executions, 7795 unique test IDs, all 35 new projector
rows and both preceding source-envelope documentation guards included. Artifact:
`TestResults/test-lanes/20260908-183545-287-2192-a0d450be90314e97a477fa58e50209c5-fast`.

Only this internal prerequisite is accepted. No live caller or downstream consumer
is claimed; T080/T081-D/T087/US3/#1536 remain open. Mortal/afterlife GM guides,
examples, manifests and source guards were explicitly considered: no new authorable
contract is exposed here, so the live Unit D synchronization remains mandatory.
No remote operation, merge, issue closure or cleanup was performed.
