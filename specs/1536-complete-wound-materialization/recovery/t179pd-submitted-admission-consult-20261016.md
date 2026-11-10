# T179-Pd step 3: genuine submitted-current admission before S

Read-only architecture consultation at `c1eff0c66e44eb4223cdd5ed140e204a2ba53fee`. No repository edits, build, discovery, tests, application execution, fetch/push or delegation. Configured Astra / High as assigned; serving backend unknown. All admission checks proposed here are UNRUN. The consultant did not run or assess the parent's initial-ledger RED. This report does not qualify Pd/P0/F1. Previous consultation artifacts remain unchanged. While this source consultation ran, the parent advanced HEAD to `43d1a3d6f7b0cd2dc8fcbbae4257f9276ec90a5a` with RED evidence/checkpoint files and began ledger implementation in separate files. All source files pinned by this report still match the inspected c1eff0c commit; current dirty files are recorded in metadata and were not edited by the consultant.

## Decision

Add a separate, private engine-owned **submitted automatic/output admission** for the internal unactivated no-offer path. Its success must follow actual validation of original and submitted-current sources and their complete read dependencies under the existing live lease. It is neither a relaxed `CreateMortalJointRepairMechanicalAdmissionAsync` nor a renamed held frame. Preserve the existing v2 method and default routing.

There is no existing one-call API that supplies this proof. Missing API is routine implementation work under approved D9–D11, not a reason to request the same permission again. Do not call the encompassing health/repair/progression loop, pass `upstreamAccepted=true`, manufacture an empty response, or treat an arbitrary path exclusion as ownership. The new proof has the narrow scope actually checked; whole common acceptance still needs the same real C/B/candidate/take and atomic M3 publication.

## Existing source seams and hazards

Paths below are relative to `BookOfEternityClient/`.

| Existing seam | What it actually proves / why not call wholesale |
|---|---|
| `Core/GameEngine/GameEngine.MortalJointRepairMechanicalAdmission.cs:76–151` | Genuine frame/original/current binding, but broad original equality and `RequireInactiveMortalJointTail`; `ReadProvedEmptyResponseAsync:64` is justified only by that empty-work contour. Does not admit changed automatic inputs. |
| `GameEngine.ValidationAndRepair.cs:1695–1705`, `CollectAcceptedTurnUpstreamRawStateIssuesAsync()` | Critical raw roots, NPC/faction/location/item admission. Not a general status/level check. Its item wrapper in `GameEngine.SpiritualContinuation.cs:58` acquires a lease and touches retained authority; do not nest it or label it a pure checker. |
| `Services/CriticalStateHealthService.cs:54–82` | Raw checks only guardians, soul, achievements, codex. `ValidateCriticalJsonFileAsync` uses filesystem existence/length plus actual JSON (16 MiB bound); no automatic-root admission. Its body can be factored over a captured physical snapshot, retaining size/error behavior. |
| `Services/ValidationService.cs:17–25`; `Validation/ValidationService.ValidationPhases.cs:10,63,134` | Selected game-state orchestration, not a source authority. `RequiredFields` also calls canonical resource validation; `PlayerStateFiles` reaches skills/items/treatment capability; `JsonIntegrity` directly enumerates/read-files outside the proposed retained view and ignores the selected-path filter. An indiscriminate All invocation is inappropriate before common materialization. |
| `ValidationService.PlayerAndInventory.cs:3592–3669` | `ValidatePlayerFile` dispatches reusable player/status/experience/characteristic checks. Reads current via `ReadSameTurnOwnerCurrentTextAsync`, but player-status required-field check calls `ValidateFileFields`, which rereads `_fs` directly (`NpcWorldAndMeta.cs:4888`). Both reads must use the same source. |
| `PlayerAndInventory.cs:3669–3720,3808–3832,7092–7188` | Player contract rejects legacy resource fields, validates stat names/arrays and other command shapes; base-stat validation; permissive XP field shapes. These are validation bodies to reuse, not authorization to execute every syntactically accepted sibling command. |
| `AcceptedTurnAndInkFeathers.cs:760–807` | Mortal level-up materialization checker reads experience plus request/soul realm. It rejects a reached threshold left unmaterialized; it does not derive XP awards or a level formula and can return without errors for absent/non-numeric fields. |
| `AcceptedTurnAndInkFeathers.cs:78–225` | Narrative checks freshness, normalized shape, timestamp, technical leakage, achievement markers and prose/state correspondence. Existing `ValidateMortalJointAcceptedNarrativeReadOnlyAsync` avoids narrative writes; descendants still read live files and require source adaptation. |
| `AcceptedTurnAndInkFeathers.cs:581–715` | Interface validator **writes** projected dialogue options; fallback can read UtcNow. Needs an explicit read-only variant sharing its validation body. Do not call it under a seal as though diagnostic-only. |
| `AcceptedTurnAndInkFeathers.cs:7523+` | Full reasoning validates debug output plus actual structured actor/snapshot/guardian/journal dependencies. `Core` selection alone is not equivalent to existing Full acceptance. |
| `ValidationService.WorldReadPhase.cs:22–63,105–194`; `Core/FileSystemManager.WorldValidationRead.cs:11–38` | World/meta validation has actual reservations and own lease acquisition. With an ambient lease its physical-scope factory returns null and falls back to scalar reads; that is not a proof all scalar reads are bound to the caller's exact lease. |
| `Core/FileSystemManager.cs:1319–1458` | Public reads can perform publication recovery and follow-up lease acquisition; explicit `ReadFileBytesAsync(lease,path)`/snapshot overloads check the supplied lease and avoid that public recovery path. Use these in the source capture. |
| `AcceptedTurnAndInkFeathers.cs:3476–3543,5075–5123` | Original reads authenticate manifest/snapshot hashes; explicit-lease variants exist at lower levels. `UsePrevalidatedPendingTurnSnapshotScope(object)` alone is an override, not a new authenticated source issuer. Do not accept its argument as proof. |

Raw item validation with a lease is also not pure: `ValidationService.MortalItemMaterialization.cs:73–95` invalidates validated item authority and enters planning. Reuse the already genuine item/resource owner evidence and exact guards where the bounded path admits no item work; do not rerun an authority-mutating pipeline simply to collect diagnostics. Any newly admitted item/faction/location operation still needs its actual domain owner, beyond this automatic-source extraction.

## Concrete API proposal (new names, not claims of existing methods)

Keep the new engine entry private and explicit:

```csharp
private Task<MortalJointSubmittedAdmissionResult>
    AdmitMortalJointSubmittedCurrentAsync(
        FileSystemManager.CanonicalWriteLease lease,
        ValidatedPendingTurnSnapshotContext frame,
        AcceptedMechanicsWoundStageBundle bundle,
        object continuation, object reservation);
```

The object parameters must be the actual existing private owner/reservation, verified through their existing validators. They are not caller-created descriptors. This method is called by the retained internal active attempt after genuine corrected-request intake and fresh owner reconstruction, before S and before any active common authority/input/prepared seal. No public boolean selects this authority and no test hook supplies acceptance.

Introduce a non-authoritative, private-constructed engine `MortalJointSubmittedReadSession` containing the live filesystem/lease identity, generation, actual frame and physically authenticated original. It exposes only read operations for source-audited logical roots and original entries, preserves exact bytes/absence, and records every consumed current/control/original dependency. It has no public map constructor, no ability to substitute arbitrary JSON, no write API and no fallback to ambient/public reads. Its existence means capture in progress, not accepted.

Proposed validator API:

```csharp
internal Task<IReadOnlyList<ValidationIssue>>
    ValidateMortalJointSubmittedAutomaticAsync(
        GameEngine.MortalJointSubmittedReadSession source);
internal Task<IReadOnlyList<ValidationIssue>>
    ValidateMortalJointSubmittedOutputsAsync(
        GameEngine.MortalJointSubmittedReadSession source);
internal Task<MortalJointSubmittedTimeValidation>
    ValidateMortalJointSubmittedTimeAsync(
        GameEngine.MortalJointSubmittedReadSession source);
```

These methods return diagnostics/data, never a publication capability. They select their own fixed checks; the caller cannot pass phases, path exemptions, realm/accepted booleans or an allowed-path map. Extract/share private leaf bodies, for example `ValidatePlayerFileRoot(path, JsonElement root, issues)` (including required fields without reread), `ValidateMortalLevelUpMaterialization(experienceRoot, owningRealmContext, issues)` and time-command validation. Existing ordinary entrypoints continue to obtain their existing sources and invoke the same bodies. For output checks thread the real session through all transitive current/original/existence reads. A narrowly constructed validation reader instance with immutable source dependency is preferable to a globally mutable service override; whichever implementation is chosen must preserve existing invocation/cache isolation.

Do not clone the entire validator into a new permissive checker. Add a read-only interface variant analogous to narrative: run `ProjectInterface` without writes, reject a required normalization before admission, and retain exact bytes. If projection requires timestamp fallback, report normalization needed rather than reading a new clock or silently projecting it into accepted data. Existing ordinary interface behavior remains unchanged. An actual prior normalization stage can supply already normalized submitted bytes, but no sequential normalizer may run inside this admission.

## Call order and source proof

1. Validate the actual live lease/generation, corrected original frame and original physical/header/manifest/history bindings. Recheck the genuine continuation, reservation and current Ready/repair correlation. A correlated-ready record is only the starting condition. Capture current controls, raw submitted outputs and six automatic roots under this lease; original images come only from the authenticated original reader.
2. Verify protected client-owned roots and every unchanged/no-work domain guard. Use existing owner receipts/candidates for admitted nonautomatic work; original equality alone is not proof that an unchanged command has no pending work. Do not run `CollectAcceptedTurnUpstreamRawStateIssuesAsync` under this lease. Reuse its applicable pure checks on the bound view and retain the genuine item/resource admissions already established.
3. Validate strict source bytes/presence and relevant required fields, player-root contracts, soul realm/incarnation invariants, level-up materialization, the actual accepted time command, and all applicable output policies. No check may reread outside the session. Capture conditional dependencies when the existing policy actually reads them; reject an unregistered dependency rather than fall back to the filesystem. Errors prevent admission; preserve existing warning/error distinctions.
4. Re-read every consumed current/control image and revalidate original/current generation plus reservation on the same lease. Issue a private `MortalJointSubmittedAdmission` only after that succeeds. Its immutable seal binds the exact original tuple, current request/Ready/repair provenance, six root images, output/control read closure, validated time selection and applicable owner references. Expose `ValidateCurrentAsync(files,lease,frame,continuation,reservation)` for subsequent S/C/B/pre-take checks. A failure/abort invalidates this attempt; no reuse across another lease/reservation without a fresh owning revalidation.
5. S consumes that admission and freezes detached exact images with their actual roles. The accepted pure automatic calculator then computes its plan once from those six data roots; output proof is still independently required. C remains the actual retained calculation, B binds S/C, and the existing same-candidate final assembly/take discipline continues. Do not claim full canonical acceptance before C and whole common validation.

A source session may lazily capture a conditional read but must retain its exact coordinate and absence. Its permitted logical roots are a source-audited implementation closure, not FileMapping and not a caller-imported map. All reads consumed by proof must be final-rechecked; a hash of only the six automatic roots cannot cover narrative reasoning or original-source dependencies.

## Exact six automatic roots and permitted differences

`JointAutomaticInputs` names six inputs, while `JointAutomaticPlan` writes only three roots. Do not confuse them with the six **fields** of outputBinding.

| Root | Admission role and bounded difference policy |
|---|---|
| `game_state/player/status_changes.json` | Actual submitted-current `statsIncreased`, authored after original, may differ. Reuse `ValidatePlayerStatArray` and player contract. The new owner consumes only that field; preserve siblings exactly and require genuine owner/no-work evidence for every other pending operation. `statsDecreased`/moneyChange do not acquire a writer just because this root is accepted. |
| `game_state/player/experience.json` | Actual GM/current level and XP output may differ after original, after the existing experience and level-materialization validations. It is a **read-only automatic input**, not an automatic write target. Capture all bytes; do not synthesize this file from the calculator. |
| `game_state/core/player_status.json` | Actual submitted current canonical status; existing required currentCondition/money, shape and legacy-resource exclusions remain. A changed level can be an admitted resolver input; accepting that field does not admit unowned changes to other status mechanics. |
| `game_state/meta/soul_state.json` | Actual realm/incarnation and training fallback source. Keep the real source/request identity checks; a level fallback is not permission to mutate soul, lifecycle, ink or afterlife state. For the first bounded no-offer contour retain original equality unless the actual existing soul owner has admitted the exact change. |
| `game_state/misc/characteristics.json` | Existing canonical twelve-stat baseline for automatic calculation. Initial meaningful positive can keep it equal to the original while admitting post-original statsIncreased. A changed baseline requires its real admitted upstream owner/normalization result; do not silently allow direct setCharacteristics because the pure calculator parses numbers. Unnormalized command-shaped input cannot be repaired by this reader. |
| `game_state/player/stat_points.json` | Existing durable client-owned balance/watermark. Require authenticated unchanged before-state unless a separately genuine preceding owner proves a transform. No GM-authored reward/watermark, no initialization, no fallback from computed level or in-memory lastKnownLevel. Only later P2 writes the three calculated automatic after-images atomically with its receipt. |

This narrower first construction contour is not the future full F1 ordinary resolution policy and cannot reject lawful future outcomes by reusing it globally. Extending to additional real owners is implementation of their accepted contracts; inventing authority for unrelated mutations is not a mere path exception.

XP precision: `ValidateExperienceFile` accepts optional number/string values and only warns for missing level. `ValidateAcceptedTurnMortalLevelUpMaterializationInternalAsync` prefers playerLevel then level; training/award calculations prefer level then playerLevel and have different fallbacks. Preserve these real policies, the accepted calculator's strict numeric/overflow requirements and warning severities. There is no inspected existing proof of `newTotalXP = oldTotalXP + gain`, exact level-to-XP formula, or equality of all level aliases. Do not claim that guarantee or add it silently as D9. A new reward formula/alias-equality rule would be a separate policy decision; ordinary validation reuse does not require that rule.

## Outputs, time and dependent reads

Keep existing `MortalJointAcceptedOutputBasis` unchanged: exactly `version,localWoundRef,woundId,transitionId,narrativeBase64,narrativeHash` (`:13`). It is captured by `MortalJointRepairCommonAuthority` from the same prepared CREATE history, local-ref operation and sealed continuation narrative (`WoundAcceptedTurnPlanner.MortalJointRepairCommonPlan.cs:36–53`). The pre-S admission proves exact submitted narrative; the common owner later binds those same bytes to its actual identities. Neither six automatic images nor a new narrative digest replaces this seal. Other output roots receive their own captured source roles, not extra invented fields in v2 outputBinding.

Read-only output validation must retain the existing narrative/interface/debug policies, appropriate Full reasoning branches, special-action, combat and pending-memory checks when applicable. No-offer admission verifies actual offer absence and current runtime/prior-terminal eligibility; it neither offers nor binds new QTE. The narrative's transitive closure includes Ready/filesModified, interface, debug/no-progress rationale, current skill names/mastery, quests/history/outline and their original deltas; achievement markers add their actual achievement sources. Reasoning additionally resolves structured actors and guardian/current-original policies, aliases, journals and conditional owner sources. These reads cannot be replaced with only the narrative JSON. This report identifies these real dependency families but does not certify a complete leaf inventory for Full reasoning/special-action/memory: closing and testing that read adapter is an explicit remaining implementation obligation, not permission to omit a branch.

Time uses real root validation: `ValidateWorldQuestCombatFactionContract` rejects simultaneous non-null timeChange/setWorldTime (`NpcWorldAndMeta.cs:8145–8158`); `ValidateTimeChangeField`, `ValidateWorldTimeObject` and `ValidateDirectWorldTimeState` are in `QuestsRivalsFactionsAndWorld.cs:1796,1831,1907`. Extract just this time block, keeping root-shape policy, not a broad world-operation grant. Calendar-only setWorldTime is legal; direct normalized absolute state has its own stricter required fields. Derive the discriminator from the validated submitted command, never endpoint arithmetic, UTC or turn. Preserve null effect CurrentTime independently of progression fallback/control/report. Capture actual frozen progression through its genuine owner and keep P1 pending; no progression writer in admission.

## Meaningful first acceptance and remaining risks

Keep the existing `JointCompletionActiveTail` v2 refusal tests unchanged. Extend the actual fixture in `BookOfEternityClient.IntegrationTests/MortalWoundTreatmentResourcePublicationLifecycleTests.JointCompletion.cs`, using the existing genuine original43 and real repair resubmission (`ObserveActualRepair`, corrected outputs around :483–530). Author status/level changes in that current reply after original and before Ready-last; never alter original images, private checkpoint/issuer or accepted receipts. Include them in the authentic full resubmission and correlation, then drive the actual internal active owner. `AuthorJointActiveTailBeforeOriginalAsync` remains a refusal fixture and cannot be the new positive proof.

Positive evidence: source proof distinguishes original level1/no stats request from current level2/statsIncreased, preserves original stat-point balance/watermark, retains exact admitted outputs/time, feeds actual S/C/B and one candidate, and holds after real common/M3 P0 while P1/P2/P3/P4 remain unapplied. No synthetic active flag or fake frame. Admission-only helper tests are not P0 qualification.

Negatives: malformed/duplicate/aliased roots; invalid stat names; pending nonautomatic sibling command; changed client watermark/balance; wrong original/Ready/session/generation; post-capture drift including a conditional output source; foreign owner/reservation; output requiring normalization; unmaterialized level threshold; non-null dual time commands; calendar-only accepted time; no-offer violated; abort/reentry and unchanged old v2 fingerprints/behavior. Include differing legitimate level-resolver inputs without forcing equality. Validate that all admission paths perform zero writes/allocations and no nested lease acquisition. These checks are future UNRUN obligations, not results of this consultation.

Main unresolved engineering risk is the transitive read adapter for existing output validators, not a missing principled grant. A source audit plus meaningful dependency-drift tests must close it before this new proof can be accepted. Whole-common postconditions must account for planned C images rather than prematurely running a canonical-postseal validator against unmaterialized current roots. Default v2 stays INACTIVE; new QTE/player F1 activation, T197 implementation, full T179/F1/T197 and D4/guided/visual/Windows/D8 gates remain open. FINALSTOP.
