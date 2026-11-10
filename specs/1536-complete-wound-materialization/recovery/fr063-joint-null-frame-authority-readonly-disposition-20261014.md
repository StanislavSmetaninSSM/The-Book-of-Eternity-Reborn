# FR063 joint null-frame authority — bounded read-only disposition

Status: **architectural recommendation supported by source; no SOURCE/FIXTURE/SELECTION acceptance, implementation or runtime release.** The specific evidence-loss route is confirmed by source, not by an executed test. The minimum fix is a private **hold-only applicability guard before ordinary late-terminal discard**, with all existing completion authority checks unchanged.

Read-time repository: `/workspace/1553-storage-migration-cloud-20261008-recovery-61d3a7f`. Coordinator-provided source/remote checkpoint: `fd2340d155d3275a81988ab13aeffeced0723bd9`; this consultation did not run Git or independently attest remote/clean status. The concurrent sole writer was authorized only for an independent world-time fixture row. The production dependencies below were read and hashed directly; no repository files were written.

Role/configuration provenance: retained consultant assigned by the parent as Astra High, not the independent Astra XHigh SOURCE gate. The tool interface supplies no independently inspectable backend model/reasoning telemetry; no configuration override or model/provider call was made. Systematic-debugging was used for the source trace only; project execution was prohibited.

## Confirmed route and its boundary

All paths in this section are relative to the repository above.

| Source | Observed behavior and implication |
| --- | --- |
| `Core/GameEngine/GameEngine.SessionAndSnapshots.cs:1105–1143` under `BookOfEternityClient/` | `LoadValidatedPendingTurnSnapshotContextAsync` returns null when `TryValidateManifestForDestructiveAuthority` fails. It discards that validator's failure output. Null therefore covers both missing work and present-but-invalid original authority. |
| `BookOfEternityClient/Core/GameEngine/GameEngine.TurnLifecycle.cs:917–967` | The late path loads that nullable frame, captures terminal signals, then passes the frame to the joint reconciler before ordinary concurrent-signal resolution. |
| `BookOfEternityClient/Core/GameEngine/GameEngine.MortalJointStoryTurn.cs:16–19` | `frame is null` returns null before any private-family applicability/protection. The valid-frame protection at lines 27–44 cannot run. |
| `BookOfEternityClient/Core/GameEngine/GameEngine.TurnLifecycle.cs:1010` and `GameEngine.SessionAndSnapshots.cs:1720–1751` | Completion reaches `DiscardMismatchedReadySignalAsync`; its null-frame branch clears Ready and transient outputs and, by default, pending snapshot evidence. Merely setting `preservePendingSnapshot` would still clear Ready/output. |
| `BookOfEternityClient/Core/FileSystemManager.ConsoleRollbackEvidence.cs:35–75` | First cohort admission requires the signed original. Later admission of the same owner rechecks its immutable cohort bytes and generation but does not re-read signed authority. This cache is explicitly storage admission only. It is neither a joint-family classifier nor a completion capability. |
| `BookOfEternityClient/Core/FileSystemManager.cs:3933` | That console admission occurs before returning the ordinary canonical lease. A fresh PID with an already-invalid original can refuse here; this differs from an already-admitted child whose original is corrupted afterward. |

Consequently the parent's specific sequence is reachable by source: admit intact console cohort in the child, corrupt original authority under the unchanged seal, retain valid active headers/terminal evidence, then process late completion using that same FileSystemManager. This consultation does not assert that the sequence has executed, or that every fresh process with preexisting corruption reaches the same branch.

## Minimum engineering change

Use one narrow guard in the existing console joint lifecycle owner, invoked when `snapshotContext == null` and there is no staged browser owner, immediately before the existing joint reconciliation/concurrent-signal resolution around `TurnLifecycle.cs:958`. It must finish before **either** completion or error mismatch handling, output clearing, snapshot cleanup or ordinary rollback. Do not change generic `DiscardMismatchedReadySignalAsync`, browser recovery, console cohort admission, or any public grammar.

The guard has only `NotApplicable` and `HeldBlocked` outcomes. It returns no validated frame, accepted outcome, phase capability, receipt, publication witness or replay permission. `HeldBlocked` uses the existing `HoldMortalJointStoryTurn` behavior (`GameEngine.MortalJointStoryTurn.cs:126–131`) and the existing late caller's held return semantics: stop gameplay and retain bytes. It does not claim that mechanics committed, or that the current proposal is accepted.

### Closed applicability boundary

Read the fixed files under a normal admitted canonical lease, using ordinary storage APIs. Re-read physical bytes rather than trusting the earlier parsed manifest variable. Use strict JSON parsing with exact property names/types; do not use deserializer defaults, regex recovery, caller paths or raw filesystem bypasses.

1. The fixed private marker `game_state/wounds/mortal_qte_decision_publication.json` must be identifiable as the existing joint family: `schemaVersion` integer 2, existing `jointTreatment` and `progression` objects, and unambiguous nonempty `sessionId`/`requestId` plus integer `turn` coordinates. This is a **candidate family**, not a validated marker. Nested phase/images/fingerprints, receipt and source validity must not be required to earn protection against deletion.
2. At least one still-present **active original header** must independently name the same exact session/request/turn: the fixed `game_state/control/pending_turn_snapshot.json`, or the fixed `input/turn_request.json`. Both actual schemas use `turnNumber`. Accept either header for hold applicability so corruption/deletion of one original surface does not disable protection while the other still identifies the active original. A conflicting second header is a reason to retain the candidate, not a reason to fall through to cleanup.
3. A marker alone, a Ready signal alone, or marker plus Ready with no matching active original header is insufficient. Ready is supplemental diagnostic evidence; a damaged/missing/foreign Ready must not remove protection once the active private candidate is identified. Do not require marker `snapshotToken` to equal a now-untrusted manifest hash for this protection. `TurnRequest` has no snapshotToken, and none may be invented. Those exact source/hash bindings remain mandatory only in the real completion proof.
4. If the validated frame is present, use the existing reconciler and its full original/G/union/receipt/phase checks unchanged. If there is no identifiable v2 joint marker, or neither active header matches its tuple, this new guard is not applicable. Existing v1, singleton, default and disjoint flows retain their owners and behavior.

This is deliberately a fail-closed preservation decision based on correlated **claims**, not a security assertion that either raw JSON is trustworthy. A fabricated matching private candidate can at most hold this active operation; it cannot complete, publish, reserve, take, roll back, delete or mint authority. The model remains trusted-local lifecycle integrity, with no new anti-owner security scheme.

Read-time instability after a candidate has been identified must hold, not silently reclassify as absent. Exceptions after identification must not enter a destructive fallback. Do not persist a new status flag or a new lifetime. Future completion must go through the unchanged validated-original path after actual authority is restored; the hold result is never a transferable capability.

### Original and generation failures

An invalid/missing signed authority, bad manifest payload hash, or changed original source bytes are exactly why the validated frame can be null. They must not exclude an otherwise correlated active candidate from preservation. Do not parse the marker into a replacement `ValidatedPendingTurnSnapshotContext` or manufacture a snapshot binding.

For G use the existing **read-only** `ReadLocalGenerationSnapshot`/`ReadExistingSessionGeneration` (`FileSystemManager.GenerationSnapshot.cs:12–42`, `FileSystemManager.cs:5881–5882`). If a candidate is readable but G is absent, invalid or mismatched, hold without minting/rotating G. Where storage admission itself already refuses, retain that refusal; do not bypass the lease fence to recover more identifying bytes.

The existing late path calls `CaptureCurrentSessionGenerationAsync` earlier (`TurnLifecycle.cs:912`). Its ordinary fallback calls `GetOrCreateSessionGeneration` (`ValidationAndRepair.cs:3178–3199`, `FileSystemManager.cs:5873–5878`). Do **not** describe that helper as read-only or use it in the new guard. In the stated real contour the retained console cohort requires an existing matching G before its lease is returned (`ConsoleRollbackEvidence.cs:43,50,68`), so a missing/wrong G can refuse before the new guard is reached. The finite G oracle must record that earlier refusal and prove retention through the actual caller; it must not manufacture a route by dropping the cohort or invoking the inspector below admission. This proposal does not change the default no-family bootstrap behavior.

If marker discrimination and both original headers are simultaneously unreadable/missing, these existing bytes no longer identify this private active family. This narrow design does not claim to solve that multiple-surface destruction, partially completed cleanup, or arbitrary corrupt namespace. Do not broaden it into a generic quarantine rule. The required one-at-a-time original/G/source negative controls retain an identifiable private marker and at least one genuine original header, or fail earlier at the existing storage gate.

## Why completed stale markers remain inert

`GameEngine.MortalJointTerminalCleanup.cs:23–54` demands applied phase, exact owned story, projection, correlated terminal and fresh destructive-original/backup validation. Lines 55–60 include both active manifest and `input/turn_request.json` in the deletion cohort. Lines 83–90 retain the mechanical marker and story; lines 101–123 publish the cleanup batch and prove exact after-images. Successful completion then clears in-memory joint state (`GameEngine.MortalJointStoryTurn.cs:107–118`).

Thus a correctly completed idle repeat has the retained marker **without either active original header** and must be `NotApplicable`. No story-row parsing, marker phase assertion or new cleanup receipt is needed to classify that negative. Conversely, a matching original header still present alongside an invalid original is unresolved work and must not be called a completed stale marker merely because the marker says `applied` or a story row exists. A disjoint new active turn does not correlate with the old marker and stays outside the guard.

## Finite controls for the source author and mandatory independent gate

These are proposed obligations, not discovered case counts, passed results or permission to execute.

- **Actual causal null-frame row:** use the real joint publisher and original, admit its intact console cohort in the actual child, then change authority bytes without changing its seal. Run the real late/idle caller. Require held/no accepted result; original/request/snapshot/authority/backups/Ready/public draft/output/private marker/full committed union/progression/story exact before-images remain unchanged; no new allocation, mutation, cleanup, claim rearm or ordinary rollback. The retained authority must remain the deliberately invalid submitted bytes, not be silently restored.
- **Single-surface source negatives:** cover invalid authority, original-source/hash mismatch and each missing/unreadable original-header surface while its genuine counterpart remains. Assert the actual invalid-frame classification and retained bytes. Restore exact original bytes only in fixture setup for a separate normal proof; never re-sign or copy a replacement original to turn a refusal into acceptance.
- **G refusal:** real missing/wrong/invalid G under retained console evidence must refuse without generation creation/rotation or deletion. Distinguish earlier lease admission from this guard's outcome; require actual outer caller preservation. A brand-new child with authority already bad is a separate admission-refusal control, not proof of the cached-child null-frame row.
- **Compatibility boundaries:** completed cleanup followed by idle with only stale v2 marker remains inert; stale v2 marker plus a genuinely disjoint active original does not claim the new turn; existing v1/singleton/default null-frame behavior remains owned by the old path. Ready-only/marker-only cannot manufacture a joint continuation. Relevant existing owners may satisfy these controls; do not invent a broad new suite.
- **No authority escalation:** an identifiable candidate with bad marker fingerprints/phase/receipt or conflicting original/Ready remains held and cannot issue any completion capability. A malformed/nonidentifiable marker plus a valid frame still follows the current original-bound inspection; the new null-frame guard must not replace that protection.
- **Valid path unchanged:** existing fresh/cold exact continuation still requires full original/G/whole-union/receipt/phase validation and owned once-story/cleanup. Reuse qualified controls where their source and scope remain valid; this consultation does not request rerunning unrelated archive, B2–B5 or earlier accepted cohorts.

The actual source diff must show both insertion order and no destructive fallback on the held result. The later first real causal result must be preserved, including any mismatch between source prediction and runtime. A direct classifier test cannot substitute for the admitted-child late-caller oracle.

## Scope disposition

**Compatible with the already approved progression/lifecycle release; no new gameplay or storage-contract decision is required for this exact hold-only correction.** The controlling amendment explicitly requires missing/foreign/ownership-uncertain cases to hold original/Ready/backups/output and committed mechanics without generic rollback (lines 29–37), and requires tampered-original/source negatives (line 41). The proposed correction closes a null-frame entry hole in that existing requirement. The amendment file still bears its original PROPOSED heading; the separate written approval/release record is the authority for its subsequently approved status.

This consultation does not itself release source edits or runtime. The sole writer still needs the coordinator's bounded work direction and the prescribed actual independent SOURCE/FIXTURE/SELECTION gate. A proposal to recover/rebind authority, auto-repair G, change ordinary cleanup for unrelated families, or quarantine unidentifiable namespaces would exceed this disposition and require a separate bounded design decision. No such extension is recommended.

## Read-time evidence and pins

Report created UTC: `2026-10-14T15:35:00.846013+00:00`. Parent source-only blocker SHA256: `552c085395889d7f6ddb551eb14ced59f4966cb2df15ea1744b6d04393bc700b`. Its four production-source pins and release pin were compared to these actual bytes.

- `BookOfEternityClient/Core/GameEngine/GameEngine.SessionAndSnapshots.cs` — `85ad957b7fa801930a7d4f13d6abd24e96b2e877d00544c5e74aad4440be6b1e`
- `BookOfEternityClient/Core/GameEngine/GameEngine.TurnLifecycle.cs` — `4534ec9dc87c79fd79e87b886cfd8f9870c45b5dcd7a854b57f8c2bc38946348`
- `BookOfEternityClient/Core/GameEngine/GameEngine.MortalJointStoryTurn.cs` — `bf4a5c905760308cd9bad2579c3eddd3a7320626242acc1d3a299ff5c6b3b5f9`
- `BookOfEternityClient/Core/GameEngine/GameEngine.MortalJointTerminalCleanup.cs` — `782dcc4904f5873060c1f267ef56b35bcc1ab2e52854c0639503b340f95ddb81`
- `BookOfEternityClient/Core/GameEngine/GameEngine.PrivateImplementation.cs` — `14496bf41b96ce8423f0471c01f5c4ef2c9b415aeb8f775c4d6d13174feff0cf`
- `BookOfEternityClient/Core/GameEngine/GameEngine.ValidationAndRepair.cs` — `d1c6bd3dab186a097129a792fdee4a8b795aed322cfc8311dc0fbaadbcbdbc21`
- `BookOfEternityClient/Core/FileSystemManager.ConsoleRollbackEvidence.cs` — `4898ea156f373ee1952a865a5f6fb34636fa739252311e575789944fe19a6aad`
- `BookOfEternityClient/Core/FileSystemManager.GenerationSnapshot.cs` — `8e78385fd23440ec87fa0d6843fe578b5e31d129af1ec51792e8701ec644c384`
- `BookOfEternityClient/Core/FileSystemManager.cs` — `14dcb900d73951bf5228d474255c197324ddb32df74a075ce22f4dec64600ec9`
- `BookOfEternityClient/Services/MortalQteDecisionPublication.cs` — `3ac7e761c514262ddff97158e02fb8646d911cebedc9300e2477e4c7558f9895`
- `BookOfEternityClient/Services/MortalQteDecisionPublication.MortalJointRepair.cs` — `bc2b5de5ff1691e2565be4558656d118b38a2c2dfbcd412d8f78801d2e86f923`
- `BookOfEternityClient/Models/TurnRequest.cs` — `29b21bc0a98bb65fe9139bd835452c614093f04134b67c5911da33806d73c733`
- `specs/1536-complete-wound-materialization/recovery/fr063-joint-progression-owner-design-amendment-20261014.md` — `cbd92b5d14b1bdf5b33e08065842c9689d0c9dcdaa03d4f1c79fe8e60be462ea`
- `specs/1536-complete-wound-materialization/recovery/fr063-joint-progression-implementation-release-20261014.json` — `71436d812eded1a1ea60b011fbd2a68a1452db0aa3b389ff6d0673fa7f9d8f51`

All five parent blocker pins MATCH. Reads included one harmless missing-path lookup for `Core/GameEngine/GameEngine.cs`; the actual main file is `Core/GameEngine.cs`. No tool/security refusal occurred.

FINAL STOPPED. No repository/Git mutation, build, discovery, PlanOnly, test, runtime, fault, provider or new-agent action; only source reads and this external report. No pending tool calls.
