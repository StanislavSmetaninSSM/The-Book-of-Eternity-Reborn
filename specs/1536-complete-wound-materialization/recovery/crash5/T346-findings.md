# T346 — completed-continuation fanout: fixture consumer first

Source-only investigation from saved T345 timeout; no code, builds/tests, materialization or new profile. Spec15r19/plan13r19, actual Spec Kit plan/tasks/prerequisites plus T346-consistency. Separate bounded architectural consultation used actual gpt-6-astra/high context /root/consult_continuation_fanout; parent inspected API/lease/history/source checkpoints and independently accounted all trace events. This consultation is not independent acceptance.

## Complete durable-prefix accounting

All525834events replayed with exact continuous sequence and paired stack:262912closed intervals,10unfinished scopes,79completed footprint-stage calls (not a claim79successful complete proof issuances). T346-trace-accounting.json retains every proof's full ancestry and sequence; T346-fanout-summary.json retains every consumer group. Counts: initial inspection11, accepted-result36, final story/cleanup32. Accepted-result36 includes GameState10, other postpublication10, progressioncommit4, projection3, direct continuation3 and six single reads/checks including its initial proof. Final32 includes append8, cleanup12, story validation4, projections3+3 and progression1+1. All counts sum79; no top-N replacement.

T345 measured accepted-result50.071s, completed-continuation48.954s, postpublication46.151s, GameState23.659s; these are nested inclusive wall intervals. Full observer overhead remains included. Owner CPU/overall completion and any historical hang cause are unknown. Service construction29.407ms is unrelated to this large consumer cost.

## Source and temporal contracts

Common project prefix below is BookOfEternityClient/.

| Source | Contract/fanout |
|---|---|
|Core/GameEngine/GameEngine.ValidationAndRepair.cs:537|Completed-continuation entry from actual accepted-result validation.|
|Core/GameEngine/GameEngine.MortalJointRepairCompletion.cs:39|Family/binding; phase/story/binding72-83; ordinary validators104-140; current proof144; fresh phase/commit/projection159-186.|
|Services/Validation/ValidationService.BootstrapAndProtocol.cs:669|First real story-proof history check; second at722, bracketing baseline history reads/enumeration.|
|Services/StoryService.MortalJointRepair.cs:39|History proof rejects foreign filesystem, acquires its own lease, reconstructs real phase47 and real story source51, compares exact current bytes.|
|Services/StoryService.MortalJointRepair.cs:168|Source entry phase1, binder202 containing2complete proof passes, source exit phase253. Together with history caller's phase1 =>5footprint stages per history check,10for two checks.|
|Services/CharacteristicsService.MortalJointProjection.cs:44|Fresh phase checks also bracket calculation/readback at53/78; repetition alone is not dispensable work.|
|Services/ValidationService.cs:54|Existing typed direct GameState/story consumer. Constructor Services/Validation/ValidationService.PrivateImplementation.cs:568 needs only filesystem/logger.|

Parent verified the real consumer releases its issuance lease before the history proof reacquires another. A fixture adapter must preserve this; keeping the initial lease causes deadlock. Existing expected-generation participation must never create or relabel observed generation.

T325 is a source-backed counterexample, not prior runtime evidence: introduce pending_wound_resolutions after outer phase entry, keep it through inner continuation, remove it before exit. Inner proof currently refuses; removing it admits a transient invalid state. Held lease does not prevent direct filesystem mutation. Therefore no phase/binder/projection fence removal, proof cache, global checked flag or cross-consumer reuse is selected.

## Selected next proposal

Test-only direct current-DATA consumer: immutable cleanup copy, actual current original/manifest/phase and StoryService.ReadMortalJointStoryAsync proof under an initial real lease, release lease, run ValidationService.UsePrevalidatedPendingTurnSnapshotScope(actual manifest) plus ValidateMortalJointStoryGameStateAsync(actual proof) within the existing expected-generation operation. All APIs already exist; no production authority seam or private proof constructor is needed. Exercise the actual ten-proof consumer, avoiding GameEngine/world/source42/Stage43/final progression/cleanup unrelated to this test's subject. Reuse strict missing-parent preparation and deferred full external diagnostics; don't copy older components' permissive GetOrCreateGeneration pattern.

Proposed next coherent block: separately categorized positive actual consumer and deterministic T325 transient pending-wave negative at inner binder via existing typed timing/read hooks, with full file/directory/generation preservation, exact refusal reason, proof ancestry/count observations, awaited operation disposal/independent admission+lease aftercondition and owned-root retention on uncertain closing. Do not add tests for a helper that merely mirror its implementation. Existing unchanged authority/phase/lease/refusal controls retain exact source-bound evidence; new negative does not relabel T325 source analysis as a historical test pass. PlanOnly/build/XML/catalog/source-input-DLL bindings and independent SOURCE/PREP precede one bounded component category attempt. No new full-owner/original/five/Linux retry.

Other integrity drift, foreign-root/lease, malformed-output and precedence contracts remain preserved; include relevant existing controls by actual changed contracts, without rerunning all unchanged groups. A new component test cannot qualify process-cold/native retirement or original graph.

## Rejected/deferred alternatives

A synthetic smaller joint-original builder is not yet established: existing integration helper creates engine/staged turn; CreateJointAsync needs a real taken receipt; generic LiveTurnPreparationService has empty rollback map and is not proved equivalent to joint requirements. Do not strip/reseal DATA or transport authority. A private immutable derivation could avoid duplicate pure frozen-progression recomposition within one phase-read call while retaining both physical validations, but runtime benefit is unmeasured and mutation/failure ordering needs a separate design. It is not selected here.

Independent coherent findings review and final remote recovery pending. Historical hangs remain unresolved; T345 ownedroot stays retained. This source spike authorizes no implementation/runtime by itself; update spec/plan/tasks for the selected component tests first.
