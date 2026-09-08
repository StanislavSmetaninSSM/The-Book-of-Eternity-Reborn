# Effect draft write-owner Unit A implementation plan

> For agentic workers: use subagent-driven-development (recommended) or executing-plans and repository TDD/review/verification after parent inspection and task tracking. The complete source/test content is in the one companion named below; do not duplicate or replace its bodies from this prose.

**Source issue:** [#1536](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/1536), immediate T081-B2C-J2 Unit A after J1.

**Goal:** Make production ordinary effect completion consume one real carrier/source/phase/identity-write owner, retaining exact application edits—including non-create updates—and J1 history/allocation evidence.

**Architecture:** The existing completion entry delegates to nested `EffectAcceptedDraft`. That draft owns the actual finalizer workspace, J1 writer, effect/source/target collections, processed events, accepted import state, frozen preflight catalog, application results and ordinary phase runner. `CarrierWorkspace` owns actual Add/Replace/Remove collection writes and captures exact edits at those writes. The existing finalizer body is relocated, not independently replayed by a receipt collector.

**Tech stack:** Existing C#/.NET8, System.Text.Json.Nodes, xUnit and PowerShell7 bounded lane runner; no new package.

## Status and exact companion

**Parent reviewed and tracked for implementation; not yet implemented or verified by C#.** Parent read the complete plan and all new/changed companion bodies, and mechanically compared the relocated finalizer against its original: after the documented local-to-owned-state and phase-instrumentation changes, only its signature differs. The original diagnostic checks, phase algorithms and their order are retained. C# evidence and independent review remain required.

- Original source baseline: `80b0955870d17965449c2a60ec005fd3b07e71e5`.
- Parent's J1 acceptance commit `15e13a73` is documentation-only; reconcile source if implementation starts from another postimage.
- Worktree: `E:/Games/worktrees/boe-1536-wound-materialization`.
- Complete executable companion: `docs/superpowers/plans/2026-09-08-effect-draft-write-owner-unit-a.patch`, next to this tracked plan. Whitespace-only patch lines were normalized without changing code semantics.
- The earlier PAUSED body dump has been superseded by this plan and that single companion. No incomplete test/body placeholder remains in the companion.
- The companion is apply_patch format. Use apply_patch after parent approval; no git apply, alternate formatter rewrite or full planner-file replacement.

## Global constraints

- Execute in the same worktree/branch. The implementer owns only the six source/test paths below and all prescribed Focused execution until handoff; parent owns tracking and Fast. No second C# lane, child Fast/PreMerge, unbounded full-suite run, branch/worktree creation, remote mutation, issue closure or session cleanup. Leave unrelated `.serena` runtime metadata untouched.
- Apply all local edits with apply_patch and absolute worktree paths. Record the actual pre-dispatch BASE SHA; commit only the six scoped source/test files after required checks and self-review. Preserve all OLD/RED/GREEN artifacts; never hide failures by weakening source validation or assertions.

- Preserve the approved `2026-09-08-spiritual-effect-draft-journal-design.md`, `spiritual-wound-live-turn-boundary.md` and J2 vertical plan. This is the real ordinary write-owner unit, not spiritual source admission/insertion/next-exchange completion.
- J1 mutation/anchor/allocation/publication semantics remain unchanged. Its only source extension is three constant-time counts and three detached suffix reads.
- Ordinary order remains preflight → N → R → replacement agreement → U → one non-bound lifetime pass → one global terminal winner fold → final after-image validation/publication.
- Preserve original issue codes, issue stage, allocator call order, consume-before-replace placement, remove-over-suspend behavior, exact replacement batch rules and existing source/target/skill authority.
- No eligibility flags, source capability test doubles, signed-before substitution, new wound insertion body, extra turn/dice/OD, schema or balance change. This is current-behavior preservation, not compatibility with obsolete saves.
- Do not add a further observer-only prerequisite. Unit A's owner actually supplies production state; full J2 still requires real source acquisition, current-generation insertion and next-exchange consumer in the same feature slice.
- No GM prompt/example/matrix/manifest change is needed for this internal behavior-preserving refactor. Actual live J2 source/insertion/transport still requires the tracked synchronization.

## Exact owned files

| File | Complete companion change |
| --- | --- |
| `BookOfEternityClient/Services/EffectAcceptedTurnPlanner.cs` | Make partial; production completion adapter; remove relocated finalizer; actual application/workspace write routing and complete application materialization capture |
| `BookOfEternityClient/Services/EffectIdentityHistoryOwner.cs` | Add15 lines: counts and suffix-read projections only |
| `BookOfEternityClient/Services/EffectAcceptedTurnPlanner.Draft.cs` | New700-line nested owned draft and relocated admission/finalizer body |
| `BookOfEternityClient/Services/EffectAcceptedTurnPlanner.DraftState.cs` | New171-line immutable carrier/source/application/phase retained material |
| `BookOfEternityClient.Tests/EffectAcceptedTurnPlannerTests.Draft.cs` | New258-line old-API fixtures, seven behavior-golden rows and one ownership-contract RED/GREEN row |
| `BookOfEternityClient.Tests/EffectAcceptedTurnPlannerTests.DraftMaterialization.cs` | New244-line direct production-owner tests:15 rows |

No integration source edits are necessary: existing real cascade/pending/afterlife consumers are required controls below. SDK file inclusion and the existing partial test class supply the new files.

## New API and exact semantics

All bodies are in the companion. The following are actual proposed APIs implemented there, not current-source claims.

### Draft lifetime and production consumption

Nested `EffectAcceptedTurnPlanner.EffectAcceptedDraft` exposes:

- `Begin(EffectAcceptedTurnPlan acceptedBase, EffectIdentityFactory identityFactory)`.
- `Complete(AcceptedEffectBoundaryTranscript transcript)`.
- `Phases`, a detached read-only list of immutable applied-phase receipts.
- `ReadIdentityIndex()` and `ReadCarriers()`, detached state images only after successful completion.
- `Dispose()`.

Begin retains the already-immutable accepted base/factory without materializing or allocating. This preserves the old transcript-rejection-before-state-read ordering. Complete first performs the existing missing/completed/five-field-transcript checks and exact activation projection; only then initializes the owned mutable state in the original order. It cannot be re-entered, repeated, resumed after a failed result, or used after disposal.

The outer `CompleteAcceptedBoundaryTranscript` performs its existing null guard and calls Begin/Complete under using. There is no parallel old finalizer or test-only completion switch. `RunCore` contains the relocated production finalizer, including its original final `ValidateAfterImages` and J1 `Publish()`.

The draft retains the actual workspace/J1 writer/lists/sets and preflight/result state as fields, not copies of a separately executed finalizer. Source/target lists and immutable base skill authority are retained at this ordinary-only stage. Live generation-version registry and dependency-local operation scheduling are deliberately not advertised as implemented.

### Actual carrier and application writes

`CarrierWorkspace.AddEffect`, `TryReplaceEffect` and `TryRemoveEffect` are the only actual effect-collection write points. Add replaces the old direct slot.Collection.Add in ApplyApplication. Existing afterlife condition projection remains before capture; the log contains the actually stored shape, not an unprojected caller image.

Each carrier edit has its actual ordinal, effectId, exact carrier coordinate and immutable nullable BeforeJson/AfterJson. Null denotes genuine create/remove, not an empty object. Reads parse fresh JSON; no mutable workspace image is retained by callers.

ApplyApplication records its starting carrier/J1 cursors before mutation. Both create/replace and non-create paths return `ApplicationExecutionFacts` with Materialization and retain that same materialization in the workspace. A non-create stack/refresh/merge/no-change result records its real updated effect and chronology despite null CreatedEffect/CreatedIdentity/ReactionResult. No scheduler re-execution or final-carrier inference reconstructs a missing result.

An application receipt includes actual disposition/IDs/events, source binding/definition/predicates, target/carrier, parameters, provenance kind and exact producer, optional created effect/identity, existing reaction result/replaced identities, skill-scope fingerprint, actual carrier edits and J1 write/allocation suffixes. It is immutable retained material, not spiritual source authority. If the helper already has issues, no successful application receipt is created; existing failure diagnostics remain in control.

### Phase versus operation scope

Six phase receipts correspond exactly to N/R/agreement/U/final lifetime/final terminal. Each holds its exact carrier/application/J1 write/allocation/agreement ranges, newly processed refs, newly used source/target bindings and skill fingerprint.

These are **ordinary-phase receipts**, not the future per-operation dependency graph or wound frontier. Their order is checked once by the owner. The frozen preflight catalog, reaction application plans/results and pre-reaction consume images are real retained fields, preparing the actual owner for later local cuts without claiming those APIs exist.

A phase receipt is recorded only after that phase's old issue check succeeds. If final canonical validation later rejects malformed allocator output, earlier actual phase evidence remains inspectable but no completed state image/plan can be exported. This preserves the old late validation stage; receipt capture does not add an earlier identity uniqueness rule.

J1 point-in-time replacement agreements remain between R and U. Consuming writes still allocate later and insert before the exact replace anchor. Terminal/lifetime helpers are unchanged and run once. Final spiritual-conflict root projection/after-image assembly remain owned by the same workspace; phase edit lists are not claimed to be a generic JSON diff of unrelated root projection.

### J1 additions

- `WriteCount`, `AllocationCount`, `ReplacementAgreementCount`.
- `ReadWritesFrom(int start)`, `ReadAllocationsFrom(int start)`, `ReadReplacementAgreementsFrom(int start)`.

The counts do not clone. Suffix reads copy only the requested tail; invalid ranges fail through List.GetRange bounds. Like J1's existing immutable projections, these remain inspectable after Publish/Dispose. Mutable/state-image operations keep their original guards. No writer, allocator, anchor, collision or publish body changed.

## Task 1: Implement and verify the complete owner patch

### Step1 — exact source preflight and staging

- [ ] Confirm the six file scopes and actual source match the companion; preserve unrelated .serena metadata.
- [ ] Add **only the complete** `EffectAcceptedTurnPlannerTests.Draft.cs` file from the companion. It depends on existing private helpers/types in the accepted J1 partial and existing `CreateReactionInput`, not any new production type at compile time.
- [ ] Do not yet add `DraftMaterialization.cs` or production files.

### Step2 — meaningful OLD controls

Run from the worktree in PowerShell7:

```powershell
.\scripts\test-csharp.ps1 -Lane Focused -Filter "FullyQualifiedName~DraftOwner_Ordinary"
```

Expected **7/7** on OLD production:3 consuming/non-consuming replacement rows;4 legal distinct-incumbent stack/no-change/refresh/merge rows. These assert actual final identity/carrier semantics and exact allocator stream. Each writes a deterministic `DRAFT_OWNER_GOLDEN` line containing full accepted-plan payload fingerprint plus allocator kind/ID sequence into its TRX stdout. Retain the OLD TRX/summary/log paths.

The new non-create fixture uses the production planner/resource transcript with a separate canonical incumbent under the child definition's exact stack policy/max/reducer. Its second canonical identity has its own chronology/create transition ID and event, matching existing distinct-incumbent fixture practice. It does not relax source validation to make a synthetic non-create result legal.

Existing J1 malformed-transition/collision and Integration duplicate-child/cascade tests remain OLD characterization evidence; do not rename their failures or substitute new expected diagnostics.

### Step3 — owning-contract RED

```powershell
.\scripts\test-csharp.ps1 -Lane Focused -Filter "FullyQualifiedName~DraftOwner_ContractRedStartsWithRealReplacement"
```

Expected **0/1**, at the assertion that the actual production completion method calls EffectAcceptedDraft.Complete. Before that assertion, the test executes a real consuming replacement and verifies its four-call allocator sequence. The call assertion uses decoded IL operands, not byte-substring or source-text matching. After implementation it also completes through the owner and compares the actual identity image/six phases.

**Truthful RED classification:** this is a production ownership-contract RED for a behavior-preserving refactor, not a claim that OLD gameplay output is wrong. The seven OLD behavior controls must pass. Do not manufacture a gameplay failure or call missing-type compilation a semantic RED.

### Step4 — apply complete production and typed tests

- [ ] Apply the remaining companion file sections exactly, adding the15 typed owner rows in DraftMaterialization.cs.
- [ ] Preserve all existing J1 tests, initial-plan callers, fixed ordinary completion signature, diagnostics and old allocator ordering.
- [ ] Execute the following separate bounded selections:

```powershell
.\scripts\test-csharp.ps1 -Lane Focused -Filter "FullyQualifiedName~DraftOwner_"
.\scripts\test-csharp.ps1 -Lane Focused -Filter "FullyQualifiedName~EffectAcceptedTurnPlannerTests"
.\scripts\test-csharp.ps1 -Lane Focused -FocusedProject Integration -Filter "FullyQualifiedName~EffectResourceTriggerRoutingScaleTests"
.\scripts\test-csharp.ps1 -Lane Focused -FocusedProject Integration -Filter "FullyQualifiedName~EffectPendingWaveIntegrationTests"
.\scripts\test-csharp.ps1 -Lane Focused -FocusedProject Integration -Filter "FullyQualifiedName~EffectAfterlifeAdapterTests"
```

First selection expected **23/23**, zero skips:8 old/reflection rows plus15 new-owner rows. The broader focused selections must retain their existing rows/diagnostics; actual counts belong in the implementing report, not invented here. Default5m Focused bounds and separate project selection apply. Parent owns a meaningful Fast checkpoint and subsequent review; this child plan runs no tests.

The15 typed rows are exactly:3 replacement materialization/phase-range rows;4 real non-create edit rows;1 clone/completion/dispose row;4 invalid-allocator late-stage rows;1 allocator-exception/fault row;1 foreign-transcript rejection-before-materialization row;1 J1 range/detachment/lifetime row.

### Step5 — exact OLD/GREEN golden comparison

Use the retained OLD and GREEN `DraftOwner_Ordinary` stdout records. Both sets must contain the same7 scenario keys, full plan payload fingerprints and allocator sequences. Do not merely compare two GREEN calls to the same implementation.

The following complete helper extracts golden lines from explicit runner artifact directories chosen from those two recorded runs:

```powershell
function Read-DraftOwnerGolden([string] $RunDirectory) {
    $goldens = @{}
    foreach ($trx in Get-ChildItem -LiteralPath $RunDirectory -Filter *.trx -File -Recurse) {
        [xml] $document = Get-Content -LiteralPath $trx.FullName -Raw
        foreach ($stdout in $document.SelectNodes("//*[local-name()='StdOut']")) {
            foreach ($line in ($stdout.InnerText -split "\r?\n")) {
                if ($line -match '^DRAFT_OWNER_GOLDEN (\S+) (.+)$') {
                    if ($goldens.ContainsKey($Matches[1])) {
                        throw "Duplicate golden scenario: $($Matches[1])"
                    }
                    $goldens.Add($Matches[1], $Matches[2])
                }
            }
        }
    }
    if ($goldens.Count -ne 7) { throw "Expected exactly7 golden scenarios." }
    return $goldens
}
function Compare-DraftOwnerGolden([string] $OldRunDirectory, [string] $GreenRunDirectory) {
    $old = Read-DraftOwnerGolden $OldRunDirectory
    $green = Read-DraftOwnerGolden $GreenRunDirectory
    foreach ($scenario in $old.Keys) {
        if (-not $green.ContainsKey($scenario) -or $old[$scenario] -cne $green[$scenario]) {
            throw "OLD/GREEN plan or allocation mismatch: $scenario"
        }
    }
}
```

Invoke Compare-DraftOwnerGolden with the **actual two** recorded run directories. A later broad selection may also contain the same seven outputs; select one run, not a parent tree containing multiple runs. Missing/duplicate output is evidence to investigate, never implicit equality.

### Step6 — independent review/handoff

- [ ] Inspect actual source diff versus this companion and verify run summaries/TRX/logs, including failed RED evidence.
- [ ] Confirm the production adapter calls the owner, all actual effect collection writes go through the workspace and all returned plan images/IDs originate from the owner/J1.
- [ ] Confirm original diagnostics/ID stages for malformed transitions, duplicate child cascade, frozen replacement drift, after-component reactions, terminal winner groups and ordinary lifecycle.
- [ ] Measure actual scale/serialization overhead without raising lane bounds or moving deterministic tests out of Fast to hide it.
- [ ] Commit only the accepted six source/test files if parent authorizes implementation commit. Parent separately owns tracking/metadata acceptance and meaningful Fast/review.
- [ ] Report internal-only no-GM-update rationale and explicitly keep real source/insertion/next-exchange/full J2 open.

## Mechanical/source audit already performed (not C# verification)

- All13 planner update hunks and the1 J1 update hunk have unique exact original contexts.
- In-memory sequential hunk replay reconstructed both proposed source postimages exactly.
- All4 new-file sections equal their saved candidate postimages exactly.
- Planner delta:86 added/517 removed lines; the large removal is relocation of the old finalizer/admission body into the new draft, not lost mechanics. J1 adds15 lines only.
- Original application/lifecycle/reaction helper algorithms were retained; actual application create changes to workspace.AddEffect and ApplyApplication returns owned materialization through the existing facts.
- The existing source guard referencing this planner is the NPC TryLocate surface; its named context remains untouched. No FinalizeAfterResourceGraphCore source-guard reference was found.
- Named test/production symbols were checked against local source; e.g. accepted plan exposes DeferredReactions, not an invented ReactionExecutions property. The canonical identity fixture's second transition is explicitly made unique.
- No build/compiler/test execution was used. Mechanical replay cannot prove compilation, runtime behavior, fixture legality or performance; the implementing RED/GREEN and parent review gates remain necessary.

## Deviation/risk notes

1. The earlier broad J2 sketch named a complete future versioned routing image. Unit A deliberately exposes no source/routing/frontier/insertion API. It owns the actual ordinary source/target/skill state and mutation runner; the versioned generation registry and local-cut consumer belong to the mandatory following live units, not a new observer prerequisite.
2. Phase receipts are named and documented as phase receipts. Exact per-application edits and J1 history/allocation ranges are real, but this unit does not pretend it already implements per-operation dependency closure or current-generation wound authority.
3. Read-only J1 count/range additions are necessary to avoid repeated whole-journal copies at each application; they do not revise J1's just-accepted history owner.
4. Application/source/image serialization adds bounded per-write work. Focused scale controls and the unchanged parent Fast bound must measure it; no performance outcome is claimed.
5. A failure after an actual mutation faults the unpublished owner and exports no final image. Previously successful phase evidence remains inspectable, matching its non-authority role. This is not a new retry/rollback transport.
6. Parent reviewed the complete companion before tracking implementation; implementation, actual verification and independent review remain outstanding. Unit A acceptance alone cannot close T081-B2C/B2/C/D/E/T084/T085 or #1536.

### Parent fixture correction before production changes — 2026-09-08

The first OLD run `20260908-160848-811-48368-97bb7372d71c4fb68726b1808a27f30f-focused`
executed7 rows:6 passed and the merge row failed when the new assertion attempted
`GetValue<int>()` on the existing reducer's `JsonValue<double>`. Parent read the
actual summary/TRX and `EffectLifecycleScheduler.TryMergePayload`: all numeric
merge results are already written as finite doubles. Production is unchanged.
The companion now compares the exact numeric value via JSON deserialization to
double for all four non-create rows; expected amounts and every other assertion
remain unchanged. The old-API test file is259 lines after this one-line expansion.
Retain this failed artifact, repeat the seven OLD controls on unchanged production,
and use that successful OLD artifact for the required OLD/GREEN comparison.
This assertion correction is not the ownership-contract RED and is not proof
of owner implementation or acceptance.
