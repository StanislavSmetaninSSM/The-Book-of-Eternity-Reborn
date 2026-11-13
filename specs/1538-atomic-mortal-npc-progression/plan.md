# Atomic Mortal NPC Progression Implementation Plan

Source: [#1538](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/1538).
Spec: [spec.md](spec.md). Branch: `1538-atomic-mortal-npc-progression`.
Base and initial remote ref verified at `7c8bb04a43f40939a9c78b10245d78283bd5805d`.

## Architecture and research

Extend `Services/NpcCoreChangesContract.cs`, not a new writer. Its current Evaluate validates exact permanent NPC identity and mirrors, and Apply updates canonical copies and removes commands. `ValidationService.NpcCoreChanges.cs` reads authenticated pending-turn originals. `CanonicalStateNormalizer.Npcs.cs` and `MortalItemPublicationTailTransforms.NpcCore` reevaluate before applying. These gates and existing accepted-turn rollback must stay authoritative.

Current ValidateProgression merely checks three nonnegative integers, with a tracker when role changes; it does not prove XP arithmetic or consequences. A type-only transition currently bypasses the documented complete tuple. `Rules/Block_19.txt` uses per-level XP and a skill award per two event levels; `Rules/Block_5.txt` defines floor(100 * L^2.5). The recursive wording incorrectly says greater-than rather than greater-or-equal and will be corrected with the harness.

`NpcTradeService.ReadNpcLevel`, GetGenerationTier and ComputeSlotCount consume level. TrainingService hashes source actors for showcase freshness. Map current invalidation paths before adding any new invalidation. Existing skill and effect validators own capability mechanics. NPCJournals already validate actor reasoning but have no progression-specific link. UI projection must be audited for private new fields.

## Blocks

### P1 — Explicit profile and deterministic tuple
- Add one pure `MortalNpcProgressionContract` evaluator and typed tests for explicit standard/table profiles, no-progress mode, single/multiple/exact-boundary level changes, cap, loss, malformed and overflow.
- Extend only existing command/profile parsing and application. No standalone writes. Add narrow category ownership and current GM worked example in the same block.
- Confirm real RED against existing weak tuple behavior, GREEN, independent review, and checkpoint. This block alone does not qualify FR3-FR8.

### P2 — Source-bound transition and dependent package
- Bind exact actor/world/turn and original profile/tuple through existing authenticated snapshot owner. Store only required progression receipt state, with idempotent replay behavior.
- Reconcile explicitly required skill/mastery/characteristic consequences and declared linked surfaces against fully composed state. Reject omission and wrong actor/source without inferring game content.
- Prove existing trade/teacher freshness or minimally close a demonstrated gap. Preserve formula/profile changes as explicit transitions.
- Review source lifetime, repeated reads and refusal semantics; save verified checkpoint.

### P3 — Lifecycle, public projection and final qualification
- Qualify first creation, ordinary NPC, merchant/teacher/combatant, minimal static and setting-table profiles.
- Inject real write/post-validation failures and retry; prove restoration and no double award through existing publication infrastructure.
- Synchronize real GM/daemon/repair consumers, console/browser privacy and tests.
- Reconcile FR1-FR9, exact selected checks, independent final review, and remote recovery. Do not merge or close without owner direction.

## Optimization and boundaries

Reuse current and original parsed roots in the invocation/accepted-turn owner. Pure arithmetic requires no cache. Resolve each declared dependency root once within the existing source lifetime; do not add a static dictionary or extend authority/leases. Profiles are small immutable DATA; fresh source, generation, ownership, expected baseline and publication fences remain mandatory. Existing cold-path and real rollback tests cannot be replaced by warm cached fixtures. No speedup claim without measurement.

## Recovery checkpoint

2026-11-13: issue/dependencies and exact main inspected; remote branch created. Spec/plan/tasks only, implementation and tests UNRUN. Next: pin P1 concrete closed profile schema, obtain causal arithmetic/tuple tests, then implement. Source path currently `skill-1533-reconcile`; local branch, not folder name, identifies #1538. SDK/runtime/PowerShell from existing environment can be reused. Temporary filesystem is nearly full; use a new owned diagnostic directory outside the repository on workspace storage instead of deleting unrelated data.

### P1 causal start

The official numeric category with the minimal pure API stub completed 1/1 cases
and failed the impossible-absolute-tuple assertion as intended (0 PASS / 1 FAIL,
8.413s, owned cleanup complete). This proves the new numeric test, not the
existing command path. A separate command-level regression will demonstrate the
current existing validator accepting an impossible level jump before wiring the
evaluator. New diagnostics are owned under `/workspace/shared/1538-diagnostics`.

Additional consumer finding for P2: `TrainingService.BuildSourceSnapshotNode`
includes level/profile and excludes only showcases/receipts/inventory, so the
existing hash naturally invalidates changed actor authority. `NpcTradeService`
rechecks current slot count, but its inventory contract currently validates only
that generationTradeTier is a known enum, not that it equals the current computed
tier. A level change that crosses a tier without changing slot count needs a
focused proof before deciding whether a correction is required.

### P1 command RED / integration WIP

The unchanged existing NPCCoreChanges evaluator accepted level 1 to 99 with no
XP delta: the new command regression failed with an empty issue collection.
Official category `mortal-npc-progression-numeric` completed 62/62 cases:
61 pure numeric PASS, 1 causal command FAIL, no skips/duplicates, cleanup complete,
8.273 seconds. The wrapper now delegates arithmetic to the pure evaluator and
recognizes command-only experienceDelta; this integration is UNRUN and unreviewed.
Next: update the active progression fixture/example, build and run this numeric
category plus the existing npc-core-changes consumer category. No whole-feature
acceptance, source receipts, dependent package or creation coverage is claimed.

### P1 bounded GREEN and documentation WIP

Remote source checkpoint 4763541fab65196f91f7f8b55b931bf37b7dc2a5:
Linux official numeric category 62/62 PASS (8.222s), existing npc-core-changes
91/91 PASS (34.452s), no skipped/duplicate cases, complete owned cleanup. Serial
no-restore unit/integration builds: 0 errors, 81/118 warnings, 63.41/38.95s.
A fresh shallow sparse clone from GitHub at that exact SHA recovered all selected
new source/spec/catalog bytes; this proves source recovery, not a clean rebuild.

Subsequent WIP adds a positive Apply/mirror/delta-not-persisted control, an actual
worked-example numeric test and a native daemon template assertion. These new
checks are UNRUN. Existing rules/main/repair/daemon examples now describe explicit
profiles, delta/full result and exact >= boundaries. One existing renderer moved
from the broad template owner to mortal-npc-progression-guidance; Linux uses pwsh,
Windows retains powershell.exe. Source receipts/dependencies/creation remain P2/P3.
Next: rebuild affected test assemblies, run only numeric/guidance categories and
catalog discovery, then independent P1 review. The unchanged existing core suite
need not be repeated. No speedup claim: trivial numeric/profile work has no cache;
current parsed baseline DATA is reused and publication/source gates are untouched.

### P1 accepted bounded checkpoint

Final source 80e8a3630bc3f7c062fde7151c4224ba318db84f: official numeric + guidance
categories completed 65/65 PASS (64 numeric/command/example and 1 actual daemon
render), 20.960s; no skips/duplicates, complete owned cleanup. The first invocation
passed a comma-joined category as one shell argument and was rejected before any
workload; the correct PowerShell array invocation passed. Unit/integration builds
0 errors, 82/118 warnings, 62.24/38.59s. Catalog discovery passed 1,680 categories /
13,977 methods/files, 10.898s, no tests executed. PE/PDB joins passed; all current
non-generated source checksums match (runtime 1,225, unit 773, integration 1,054).
The two runtime build copies differ only in generated AssemblyInfo timestamp.
Linux SDK 10.0.100, net8 runtime and PowerShell 7 from the current prepared tools;
Windows execution and clean-source rebuild remain unqualified.

Independent fresh-context GPT-6.1 Sol XHigh reviewed the complete P1 diff and
actual logs: no Critical/Important findings, accepted P1 only. Optimization and
selection were accepted. T002/T003 are complete. P2/P3 remain open; no merge or
issue closure. Next: thread trusted current Mortal source context through the
existing validator and normalization owner, then bind dependent changes/replay.

P2 trade investigation rejects a premature derived-tier equality change: current
inventory contract intentionally accepts an explicitly authored Good tier even
when the computed suggestion is Standard (existing level10/trade14/relation80
fixture). Keep this established authority. Prove real slot-count freshness at
level15->16 (7->8 slots), plus teacher source-hash freshness, instead of inventing
an unconditional inventory regeneration or a new tier rule.

### P2a source binding: proposed bounded implementation

From accepted P1 checkpoint 090733ee. First obtain causal RED with
`MortalNpcProgressionSourceBindingTests.ExistingProgression_WithoutTrustedTurnContext_IsRejected`:
valid P1 arithmetic currently applies despite absent trusted source context.
Then add the closed progression.source schema documented in spec.md; attach
trusted session/request/turn/snapshot/realm DATA to existing NPC authority.
Reuse ValidationService's authenticated pending-turn lookup under the publication
owner's held canonical lease for the real NoPlan path. Pass context, do not
re-read it in the pure tail. Accepted-plan/treatment/joint paths reuse their
existing bindings, and existing authority clone/fingerprint paths retain it.

Bounded verification: typed source/realm/malformed/stale refusals; positive exact
source and consumed-command no-op; production NoPlan positive with authenticated
snapshot and lease, plus missing/stale source refusal. Parent owns category,
builds, independent review and publication. No production change before RED.
No new cache: detached small immutable context is operation DATA only; fresh
snapshot/request/source and publication ownership checks remain authoritative.
