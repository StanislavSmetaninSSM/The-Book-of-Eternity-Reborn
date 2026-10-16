# T179-Pd submitted-output bound-read closure consultation — 2026-10-16

Source-only consultation for #1536 T179-Pd step 3. Assigned Astra / High; serving backend unknown. Inspected baseline 59e05adb13602dca91a58238701620b00381d3f4; validator/FS source follows c1eff0c66e44eb4223cdd5ed140e204a2ba53fee. Exact historical blobs, SHA-256, completion HEAD and current-source comparisons are in the companion JSON. No repository edit, build, test, discovery, app/runtime, fetch/push, delegation or implementation occurred. All acceptance checks below are FUTURE/UNRUN. This new pair supplements the prior submitted-admission consultation without modifying it.

## Ruling

Thread one explicitly engine-owned submitted-output read session through the seven existing policy roots and their IO-bearing leaves under the already live lease. Preserve Full reasoning, branch predicates, diagnostics and authenticated-original distinctions. Capture/recheck actual current bytes and absence, original presence/absence, control tuples, rollback reads, completed guardian proof dependencies and applicable repair-freshness metadata. Only the genuine engine owner can issue admission after checking the source closure and its owner/reservation/generation; S/C/B then consume the same actual attempt.

This is a finite extraction, not a replacement filesystem, public map import, candidate overlay, broad game-state validator under a lease or upstreamAccepted boolean. A correlated repair-ready frame is not Full acceptance. The positive fixture must submit changed current files after the genuine original.

Additional material finding: some existing validation projections sample UTC and generate temporary IDs. Files alone do not make replay deterministic. Retain their genuine first-pass comparison results/observations; do not rerun those bodies during seal, same-attempt reentry or cold recovery. Such detached projection values are not canonical publication/identity authority. The earlier consultation's blanket “zero allocations” wording needs that correction.

## Source conventions

Paths below are beneath BookOfEternityClient unless stated otherwise.
A = Services/Validation/ValidationService.AcceptedTurnAndInkFeathers.cs.
N = Services/Validation/ValidationService.NpcWorldAndMeta.cs.
G = Services/Validation/ValidationService.GuardianPolicyKernel.cs.
GM = Services/Validation/ValidationService.GuardianMusingsPublication.cs.
E = Core/GameEngine/GameEngine.ValidationAndRepair.cs.
The appendix/JSON supplies full-path exact leaf read lines. Ranges are source locations, not executed coverage.

## Concrete minimal API and sequence

Suggested names/signatures are NEW APIs, not existing methods:

1. Private engine CreateMortalJointSubmittedAdmissionAsync(lease, actualValidatedFrame, actualJointAttempt). The actual attempt retains existing continuation/resource owner/reservation, not a public coordinates DTO. No caller path exemptions, selections, synthetic GameResponse or accepted flag.
2. ValidationService.ValidateMortalJointSubmittedOutputsAsync(MortalJointSubmittedReadSession source): invokes narrative read-only, interface read-only, reasoning Full, special actions, combat, pending-memory and actual no-offer eligibility. Returns diagnostics plus comparison evidence, not publication authority.
3. Current family: ReadCurrentTextAsync(OutputCurrentPath), ReadCurrentBytesAsync(OutputCurrentPath), CurrentExists(OutputCurrentPath). Finite internal IDs map to the families below; existing string leaf boundaries may adapt only by exact validation. No wildcard current reader.
4. Original family: ReadOriginalAsync(OutputOriginalPath), preserving authenticated-present, authenticated-absent and unavailable/invalid distinctions. Resolve physical coordinates only through the actual original owner. Original controls have fixed roles (guardian trade/offering etc.), not arbitrary map entries.
5. Authority family: an internal PendingTurnSnapshotReadSource and source-aware PendingTurnSnapshotReader core overload for manifest/authority/current contexts and authenticated signed/rollback bytes. Capture every callback read made by the existing authority validator. Bind raw bytes and exact decoding/hash mode; do not replace genuine authentication with parsed marker data.
6. Rollback family: ReadAuthenticatedRollbackAsync(OriginalLogicalPath); special-action extra semantic read is soul's backup, while manifest verification may read all registered rollback backups.
7. Synchronous guardian leaves: asynchronously pre-capture the finite source family, then ReadCapturedCurrentText/ReadCapturedOriginal with reject-on-unknown/unloaded semantics; or use a lease-bound synchronous physical snapshot primitive with identical tracking. No sync acquisition of another lease. Captured data is physical current/original, never a candidate overlay.
8. Completed musings: ReadCompletedMusingsForComparisonAsync(source,currentRoot) reuses the actual enabled completion and SAME lease, including its nested original/binding reads. Preserve real absent/disabled scope behavior; do not manufacture an empty comparison or mint completion.
9. Repair freshness: CaptureCanonicalRepairFreshnessAsync(source, actualRepairHistory) retains the actual repaired errors, start/fallback timestamps, target/output metadata and resolved boundary. Reuse a genuine existing completed owner proof when available; otherwise extract this same-lease read leaf. A naked DateTime boundary is not an owner proof.
10. source.RequireUnchangedAsync(actualFrame, actualAttempt): throw latched failures; recheck every consumed byte/presence/metadata observation plus genuine original, generation/lease/owner/reservation; close capture; issue private admission only if no error diagnostics. Preserve warnings.

Sequence: real corrected Ready/request/repair/original and reservation → six automatic root/current control capture → earlier consult's raw/player/level/time checks plus all output roots below → first-pass comparison observations → exact closure recheck → private submitted admission → S → retained C → B → same candidate sealing. No encompassing health/repair/progression loop or sequential normalizer.

Record source role, logical ID, owner-resolved physical coordinate, presence, exact bytes and original lookup status. Repeated reads must agree. Present-empty is not absence. Retain input bytes/BOM and the existing leaf's decoding semantics; never reserialize submitted/original images.

A source failure must latch outside existing catch-and-null/best-effort handlers. Those handlers may retain existing domain diagnostics, but cannot turn a storage/authority failure into accepted absence. Unsupported reads must fail before any scalar fallback.

## Existing source infrastructure: what can and cannot be reused

FileSystemManager explicit lease reads are at :1360 (text), :1417 (bytes), :1442 (snapshot), :1517 (sync text). Public methods :1319/:1401/:1467/:1542 can recover publications and acquire read-quiescence leases. Passing an ambient context is not observation tracking.

PendingTurnSnapshotReader.ReadCurrent :143 validates the lease but internally still uses scalar ReadFileSync for manifest/authority (:161/:193), scalar ReadFileBytesSync callback (:209) and selected originals (:344), scalar FileExists/ReadFileSync for lifecycle context (:712–722). A source-aware internal core is necessary; its current lease parameter alone does not bind transitive reads.

PendingTurnSnapshotAuthority.TryValidateManifestForReaderAuthority :482 calls genuine authority validation; BuildAuthorityPayload/CopyNormalizedFileHashes :769–889 reads rollback backup bytes through its callback. Bind these observations, including every actual backup read. This does not mean it reads every signed original file: selected original bytes are checked separately. Retain exact hash mode, manifest/case-confusable/original-presence rules.

SameTurnOwnerReadView only routes spiritual draft registered paths and selected helpers; scalar A/N/G reads bypass it. It is not a full submitted-output reader. WorldReadPhase :228–312 is a useful pattern (bounded path gate, fault latch, repeated-read equality, final recheck), but its World phase scope owns its own lease and may fall back to scalar IO (:107–190). Do not call that scope inside this lease or widen global fields into a task overlay.

GM:243–250 acquires/releases a lease synchronously when an enabled completion exists. New bound-path comparison must reuse the live lease. GM:85–97 and :140–149 check original guardians+soul, generation, request/manifest/authority binding hashes and published guardians bytes; those dependencies belong in the closure. No scope, disabled scope or missing completion is the actual existing absent branch, not evidence supplied by a boolean.

G:195–225/:1769–1793 caches apply only during IsGameStateValidationInProgress. Public output wrappers do not establish that game-state scope. ValidationPhases:9–58 resets/restores caches; G:235–324 restores mutable recursive _guardianPolicyContextInProgress; GM:190–213 scopes can invalidate/restore. Pass an isolated per-admission context; preserve recursion handling, reject foreign concurrent/reentrant use and invalidate on failure. Do not borrow another invocation's caches or open the broad game-state phase just to enable caching.

## Seven-root transitive source inventory

### Narrative and interface

A:79–213: current narrative_response, original/current freshness, schema/timestamp/technical leakage, achievement markers and prose deltas. Existing readOnly:true refuses projection changes. A:230–235 ordinary normalization writes; exclude it from admission.

MetaCodexAndAchievements:745–802 achievement marker helper reads current game_state/meta/achievements.json and then original achievements after a parseable current object. Domain best-effort catch behavior does not excuse a swallowed source failure.

A:264–468 prose audit reads current output/interface_updates.json, ready/turn_complete.json filesModified, output/debug_logs.json rationale, game_state/player/skills_active.json and skills_passive.json, game_state/quests/regular_quests.json. Claimed skill/quest progress conditionally reads original first then current:
- skill_mastery, skills_active, skills_passive;
- regular_quests, quest_history, plot_outline.
A:419–444 returns on first real delta; any original evidence with no delta returns false; Ready/filesModified fallback is used only with no pre-turn evidence. Preserve observed original absence and the short-circuit reads.

A:582–695 interface reads original/current freshness plus current physical existence/content. A:710–717 ordinary normalizer samples UTC only for timestamp fallback and writes changed output. New read-only entry must share validation/projection logic and reject required normalization; no invented timestamp or write.

A:3125–3143 freshness compares current and authenticated original TEXT and produces a WARNING for exact reuse. No mtime/UTC read here; do not convert this warning to an error.

### Full reasoning

A:7525–7987 reads current debug and original/current freshness. On the valid reasoning path it resolves guardian policy, structured actors, player/persistent aliases, current action, addressed canonical actors and realm. Early parse errors may terminate validation; successful acceptance cannot skip these policies.

Source families:
- Guardian policy subgraph below.
- N:749–1061 structured actor updates: current+original NPC core, residents, entity profiles, shining state, and guardian projections. Mortal realm does not skip these extractors.
- N:2638–2814 player aliases: current soul and game_state/control/next_life_scenario_core.json; persistent aliases: current NPC core, regular quests, inventory/items, factions/faction_core.
- N:2339–2435, actual nonempty action: current guardians, NPC core+journals, residents, entity profiles and shining state (including repeated read).
- InventoryNpcWorldCrossRefs:201–240: original and current NPC core.
- QuestsRivalsFactionsAndWorld:2660–2696: scalar current request gachaBaseResult, then genuine validated manifest fallback; reachable through guardian policy.

Full A:7959–7985 invokes all six scopes. Keep the selection Full; leaf predicates select:
- N:3150–3248 GuardianMusing: actors+usable original; current guardians and guardian_thought_journal, then matched actors' original both. NO realm guard.
- N:3364–3450 MortalNpcJournal: actors+usable original+original Mortal; current NPC core/journals, then matched actors' original both.
- N:3458–3535 AfterlifeResidentJournal: original Chaos Sea; current residents, then matched actors' original.
- N:3656–3741 AfterlifeEntityLedger: original Chaos Sea or Shining; current profiles, then matched actors' original.
- N:3749–3845 ShiningFactionMemory: original Shining; current shining state, then matched actors' original.
- N:3851–3893 AfterlifeMemoryOwner: original Chaos Sea/Shining; current guardians/residents/profiles, plus shining for Shining.

Authenticated Mortal original makes the last four leaf branches return at their own guard. It does not eliminate those sources already read by structured extraction. N:3588–3628 ReadActorMemorySnapshotFileAsync distinguishes authoritative nonexistence, baseline-tracked file omitted from manifest, and registered missing/unreadable/hash-mismatched original. Keep those statuses.

Constant paths are game_state/meta/guardian_abode_residents.json, afterlife_entity_profiles.json, shining_abode_state.json, guardian_thought_journal.json, guardian_projects.json, abode_power_journal.json.

### Guardian subgraph and original readers

G:195–225 current+original guardians; :383–390 original soul/realm; :652–689 current action from request then repair then Ready then manifest fallback, preserving order.
G:730–775 uses original game_state/control/pending_guardian_trade_request.json and already-read current guardian materialization/receipt contract.
G:1076–1204 conditionally reads original guardian tracker, power journal and soul according to actual manifest membership/strict authority needs.
G:1769–1793 current+original tracker; :2686–2728 request/repair turn, original/current soul, current game_state/control/life_transitions.json.
G:1569–1577 idle fallback uses manifest/request/Ready complete/error existence only for MissingManifest. The genuine live original required here rules out admission via that fallback.

Adapt every original read path, not only ReadPreTurnTrackedFileAsync:
A:3432–3469 sync lookup/authority/callback; :3506–3588 async+sync selected original bytes/hash/detached authority; :3621–3659 current+required original; :4010–4040 current context scalar reads; :5077–5202 manifest existence/content/authority/current repair-request-Ready; :5225–5242 shared physical leaves; G:1650–1739 independent guardian original readers.
_prevalidatedPendingTurnSnapshotOverride's immediate usable branch is not a new original proof. Only actual retained original authority may back it.

### Special action policies

No-offer does not disable A:8042–8443. Request resolver :8446–8485 reads current input/turn_request.json; malformed nonempty request errors; missing/empty uses validated manifest action/session/request/turn/gacha. Live-frame requirements may separately require request presence.

Four tag-gated helpers run before Ink parsing:
- :1035 CHAOS_SEA_DIRECT_GACHA: current+original soul, actual gacha request/prepaid evidence.
- :1207 CHAOS_SEA_TRAVEL: original soul and current+original guardians, actual target/policy.
- :8760 ABODE_RESIDENT_RELIC_GRANT: current+original soul/residents.
- :8909 ABODE_RESIDENT_QUEST_REQUEST: current+original residents/soul_quests; :9110 preserves optional original absence versus missing hash.

Only actual absence of those tags plus null ParseInkFeatherActionContext proves deeper action branches unreachable. Client-side tags return under their existing rule. GM tags use authenticated original realm whitelist and output/ink_feather_action_result.json receipt (:1621–1792; no clock freshness).

Conditional current+original families:
- SACRIFICE_TO_CHAOS :1795: world_events, world_flags, current_location, world_map, weather.
- ABSORB_FEATHERS :1819: experience.
- LEARN_SKILL :1866: active OR passive selected by validated category.
- FATE_SHIELD :1946: effects.
- SEAL_IN_INK :1998: game_state/control/pending_ink_actions.json plus actual pending shape.
- DONATE :2034 / GUARDIAN_FAVOR :2216: guardians and original/current policy.
- CULTIVATE :2153: soul enlightenment.
- ABODE_OFFERING :2289–2690: original pending_abode_offering control; original/current guardians; original tracker/power journal/soul; current power journal; conditional current soul and existing strict journal proof.
- SOUL_IMPRINT :2693: soul and pendingInk; :7260/:7290/:7334/:7373 current imprint/minimal traits/provenance.
- MEMORY_GATES :8191+: current soul and :8637 original pending legacy/grant comparison.

A:2803–2874 filters authored affectedFiles against an exact per-action allowed set before DescribeTrackedFileChangeAgainstManifestAsync :5392–5412. Guardian-sensitive variants :2881–2946 use genuine original/current. A caller's arbitrary path map is not authority.
A:1459–1497 prepaid check separately reads original soul's rollback backup from genuine detached authority and verifies its existing rollback hash. Signed original and rollback are distinct source roles.

### Pending memory

A:8488–8635 always reads current soul. Early return requires actual absent pendingMemoryLegacy or applicationState other than applied-awaiting-turn-accept. Mortal/no-offer alone does not prove that.
Applicable audit/materialization reads current characteristics (:8676–8697) or passive skills (:8705–8758), with existing canonical bonus comparison and any validated-original/guardian dependencies reached by those helpers. Do not initialize/apply/clear legacy here.

### Combat

A:720–758 uses actual request progressionControl realm with current soul fallback (:812–822), request action and narrative response. Explicit combat signal selects Ready/filesModified, current experience positive gain, current skill mastery and player_status existence.
HasMortalCombatState :883–890 is Ready membership OR combat_log/enemies/allies existence in short-circuit order. Retain existence-only observations. No new reward formula.

### No-offer and unreachable branches

PlayerAndInventory:238–243 reads output/qte_offer.json and returns for null/whitespace. The new owner separately requires PHYSICAL ABSENCE; an empty present file must refuse.
The present-offer response-fragment/full-validator/inventory subgraph is therefore unreachable. This excludes A:5553–5599 tracked file inventory, Directory.GetFiles and output existence, InventoryNpcWorldCrossRefs:373–387 direct File.Exists/File.ReadAllText and PlayerAndInventory's raw inventory readers :5220/:5322/:6507. Do not audit/activate present-offer behavior through a synthetic flag.

No-offer root's return does not validate runtime. Factor/reuse genuine checks in GameEngine.MortalJointInactiveTail:55–75 for game_state/control/qte_runtime.json (pendingOffer/activeScene absent) and the genuine prior finalized deferred-source/turn rule. Separate these from old inactive stats/level refusals only for the NEW internal owner. Preserve the full v2 inactive method and default routing.

A:5414–5422 story continuity directory enumeration and N's historical story fallback belong to broad game-state paths, not these seven roots.
LifecycleControlAndStateFiles:4171 generic ReadValidatedPendingResolutionContractAsync<T> has current/manifest/Ready and raw snapshot-bridge existence reads. Its callers :3491/:3885/:3976 are broad offering/trade/foundation pending-resolution validators, not these seven roots. Special-action offering uses its specific original helper. Do not confuse these similarly named branches.

## Metadata and clock freshness outside the seven roots

No FileInfo access was found in these seven roots or the absent-offer descendants. CriticalStateHealthService's FileInfo.Length/size policy is a separate raw-health owner, not a reason to call that service under this lease.

Actual repaired-canonical freshness remains required when applicable:
- E:2688–2729 selects actual canonical repaired errors excluding neutral actor-memory changes; narrative/interface physical File.Exists then File.GetLastWriteTimeUtc. Existing output passes only when mtime is STRICTLY greater than the retained boundary; equality fails. Missing output is skipped by this leaf, not by other required-output checks.
- E:2731–2784 normalizes/safe-filters distinct target paths; observes existence and mtime. Missing/error or mtime before actual repairStartedAtUtc is unobservable. Uses latest write and actual supplied fallback or DateTime.UtcNow; no-target branch also uses fallback/now. Capture actual failure/absence, timestamp selection and boundary, not just final bytes.
- Bind actual repaired-error selection, repairStartedAtUtc, fallback used, all target/output observations and resolved boundary to the real repair attempt. Reentry retains boundary; seal rechecks observable metadata. Cold verification uses retained proof/observations, not a new UtcNow.
- E:8711–8731 request File.Exists/mtime with manifest RequestTimestamp fallback; :8734–8772 recoverable output existence/content/JSON timestamp; :8774–8804 Directory.Exists/GetFiles recursive output/game_state/lore and mtimes are MISSING-TERMINAL RECOVERY. Corrected correlated Ready must not invoke this route to manufacture acceptance. If a genuine upstream frame actually used recovery, retain that owner's completed inventory/metadata proof; otherwise the actual corrected-Ready route excludes it.
- GameEngine.SessionAndSnapshots:2223–2249 full rollback resubmission comparison acquires a lease, uses EnumerateRollbackTrackedFiles :1900 (_fs.EnumerateFiles(lease,"*") and exact filtering), plus current and backup bytes in :2260+. Reuse genuine completed full-resubmission evidence; if rechecking inside admission, factor a same-lease overload and bind inventory+all compared bytes. Never call its lease-acquiring wrapper inside the live lease or substitute six roots for full resubmission.

Consequently the seven-root absent-offer reader needs no generic directory API. Its upstream resubmission/recovery owner may have real inventory dependencies and must supply their completed proof or exact same-lease capture. A bare success flag or historic unbound frame is insufficient.

## Transitive projection clocks and allocations

Zero canonical writes, scheduler runs and published identity allocations remain requirements. Existing detached comparison builders allocate objects and some temporary IDs/timestamps:

| Actual source owner | Reachable volatile behavior |
| --- | --- |
| GuardianAbodeResidentState.NormalizeShape :408–516 via N:954/:978/:1790 and tagged comparisons | Missing receipt resolvedAtUtc (:1202/:1213/:1234) or history revealedAtUtc (:1242) uses UTC. |
| CanonicalStateNormalizer.BuildGuardianAuthorityRootForValidation :109–162 → SharedAndSoulHelpers.ApplyGuardianCommands :216–415 | updateReputation UTC :276; completeQuest UTC :299; authorized event projection. |
| AbodePowerRules.EnsureCanonicalState :266–275 | Missing lastUpdatedAt uses UTC :272. |
| GuardianPowerEventState.BuildEvent :289–319 / ApplySingleEvent :321+ | Missing appliedAt uses UTC :315/:333; detached power history/journal materialization. |
| CanonicalStateNormalizer.BuildGuardianProjectAuthorityRootForValidation :10–107 → GuardianProjectHelpers | GUID power-event IDs :212/:242/:1344; journal IDs :979/:1216/:1276 on actual assist/sabotage/start/update/completion/token/gacha branches. |
| GuardianProjectHelpers.ApplyGuardianProjectRelationshipEffects :687–726 → GuardianRelationshipRules.ApplyMutualDelta/ApplyDirectedDelta | Applicable completed political project writes detached relationship history with UTC :273. |
| CanonicalStateNormalizer.TryResolveGuardianProjectAuthoritySoulContext :240–314 → BuildNormalizedSoulStateRoot → ApplyMetaStateUpdates :29–200 | Current memoryLegacyGrant with type: absent legacyId GUID :168 and grantedAtUtc :174–176. Existing AcceptedTurnProjectionClock supports this timestamp but current comparison caller does not pass it. |
| DTO deserialization in guardian policy/tagged offering | PendingGuardianTradeRequest defaults requestId GUID and CreatedAtUtc (:38/:65); PendingAbodeOfferingRequest CreatedAtUtc (:47). Initializers execute even if JSON later replaces values. |

These particular projection calls do not invoke filesystem writers. They mutate detached JsonObject copies and may invoke pure domain comparison helpers with temporary projections. This is not proof that their results can be recomputed later from bytes alone. No blanket Mortal/no-offer predicate excludes all of them: Full reasoning visits resident/guardian state.

Minimal safe treatment is explicit per-admission AcceptedOutputProjectionObservations passed to the affected comparison helpers, owner-controlled clock observations and retained first-pass results keyed to actual source/call role. Keep legacy ordinary defaults. Existing AcceptedTurnProjectionClock supplies a useful clock-leaf pattern, but its five existing roles do not cover these guardian/resident responsibilities; the spiritual clock is another owner. Do not import its authority or accept arbitrary caller time delegates.

Preserve actual clock observation order where it matters; do not silently collapse multiple old fallback samples into a new policy. On seal/reentry/cold use retained results rather than invoking the bodies again. Generated temporary comparison IDs must never become C lifecycle receipts or canonical identities. If the implementation insists on zero temporary GUID calls too, factor comparison-only no-emission kernels and prove equivalence; do not use dummy IDs. DTO presence validation cannot be replaced by initializer defaults.

Frozen validation UTC is NOT action elapsed time, progression budget, P0 committed CurrentTime, P1 FrozenInputs, calendar command, reward rule or level rule. Preserve genuine D9 owner discriminator/evidence separately. Do not invent an XP formula or force level aliases equal.

## Future test-first qualification, all UNRUN

Use the genuine original→repair→corrected-submission fixture in MortalWoundTreatmentResourcePublicationLifecycleTests.JointCompletion.cs. Author actual changed current stats/level/output after original, Ready last, and enter the real new internal owner. Keep JointCompletionActiveTail v2 refusal fixtures unchanged. The new path may remain held before unfinished later publishers; a DTO helper result alone is not P0/M3.

Required controls:
1. Valid mortal Full reasoning with real NPC/guardian updates; mutate consumed current/original/control families after validation and before seal: achievements, aliases including residents/profiles/shining, scenario manifest, Ready/filesModified, original request/gacha and current outputs. Unchanged narrative does not excuse source drift.
2. Existence-only combat absent→present/present→absent; absent QTE offer→empty file; runtime pending/active and prior deferred turn drift.
3. Real conditional tagged action and pending-memory tests; no-tag positive proves actual parser branch, not a skipped root. Authored affectedFiles outside exact per-action sets cannot cause arbitrary reads.
4. Full selection still invokes all scopes; actual original Mortal guards leaf afterlife memory branches while earlier structured sources remain captured. Existing afterlife public policy remains unchanged.
5. Foreign original/lease/generation/resource owner/reservation; manifest/authority replacement, rollback-byte drift, selected original disappearance and invalid baseline absence. Exercise PendingTurnSnapshotReader callbacks/context leaves, not just its return DTO.
6. Catch-null/best-effort IO failure leaves sticky failure; unsupported read fails before fallback; repeated-read drift and foreign cache/reentry fail. No global override or caller map.
7. Genuine completed guardian proof uses SAME lease, retains invalidation and rejects changed source. No nested lease acquisition; genuine absent scope is handled by existing branch.
8. Repair metadata equality-to-boundary fails and greater passes; mtime-only drift invalidates retained observations; missing/error target uses actual fallback. Reentry/cold makes no new clock sample.
9. Reachable resident/guardian/project volatile projections are captured once; any no-emission refactor must prove decision-equivalence. Raw source arrays/files remain untouched; comparison IDs never become publication authority.
10. Source admission actually feeds S/C/B and the same retained candidate/fence; source drift/foreign owner after seal refuses, failure invalidates attempt and reentry does not repeat common reduction.

Relevant existing policy families include turn-lifecycle-core, turn-lifecycle-repair, accepted-mechanics-planning, guardian-projects-social, guardian-residents-rivals and guardian-lifecycle plus actual bounded joint/Pd ownership. Parent selects exact affected catalog members; this is not a request to run all those categories. Consultant ran none.

## Closure and remaining limits

The direct filesystem frontier for seven roots under genuine absent offer is enumerated here, including original callbacks and completed-proof dependencies. The attached lexical index is an audit aid, not compiler reachability or runtime proof; same-name overloads can overapproximate, and generic/static descendants were manually checked separately.

Concrete remaining implementation obligations: source-aware original callback/context core; source threading at listed scalar leaves with sticky failures and isolated caches; same-lease completed musings; genuine full-resubmission/freshness owner proof or exact extraction; first-pass volatile comparison capture. These are owner-preserving D9–D11 engineering obligations, not a missing principled grant.

This report does not establish whole canonical acceptance, active player behavior, completed S/C/B/M3, cold receipt support or successful tests. No equality exemptions, empty GameResponse, generic pending orchestration changes, new reward/level policy or afterlife widening. Default v2 stays INACTIVE. Full T179/F1/T197, D4/guided/visual/Windows/D8 and unfinished P1–P4/QTE activation gates remain open.


## Exact direct leaf index

This index records source IO/cache/lease sites, including the two ordinary output writers that the new read-only route must exclude. It is not an allowlist. Authority callback and external projection descendants are covered in the sections above.

| Method | Source | Direct operations / observations |
| --- | --- | --- |
| ValidateAcceptedTurnNarrativePayloadInternalAsync | [ValidationService.AcceptedTurnAndInkFeathers.cs:79](/workspace/The-Book-of-Eternity-Reborn/BookOfEternityClient/Services/Validation/ValidationService.AcceptedTurnAndInkFeathers.cs:79) | 88: var json = await _fs.ReadFileAsync("output/narrative_response.json"); |
| NormalizeAcceptedTurnNarrativeLineBreakArtifactsAsync | [ValidationService.AcceptedTurnAndInkFeathers.cs:230](/workspace/The-Book-of-Eternity-Reborn/BookOfEternityClient/Services/Validation/ValidationService.AcceptedTurnAndInkFeathers.cs:230) | 233: await _fs.WriteFileAtomicAsync(narrativePath, projected.Json); |
| BuildAcceptedTurnPlayerFacingTextForDeltaAuditAsync | [ValidationService.AcceptedTurnAndInkFeathers.cs:324](/workspace/The-Book-of-Eternity-Reborn/BookOfEternityClient/Services/Validation/ValidationService.AcceptedTurnAndInkFeathers.cs:324) | 326: var interfaceJson = await _fs.ReadFileAsync("output/interface_updates.json"); |
| ReadCurrentKnownPlayerSkillNamesAsync | [ValidationService.AcceptedTurnAndInkFeathers.cs:363](/workspace/The-Book-of-Eternity-Reborn/BookOfEternityClient/Services/Validation/ValidationService.AcceptedTurnAndInkFeathers.cs:363) | 365: names.UnionWith(ParsePlayerSkillNames(await _fs.ReadFileAsync("game_state/player/skills_active.json"), "activeSkillChanges"));<br>366: names.UnionWith(ParsePlayerSkillNames(await _fs.ReadFileAsync("game_state/player/skills_passive.json"), "passiveSkillChanges")); |
| ReadCurrentActiveRegularQuestIdentitiesAsync | [ValidationService.AcceptedTurnAndInkFeathers.cs:371](/workspace/The-Book-of-Eternity-Reborn/BookOfEternityClient/Services/Validation/ValidationService.AcceptedTurnAndInkFeathers.cs:371) | 373: var json = await _fs.ReadFileAsync("game_state/quests/regular_quests.json"); |
| HasAcceptedTurnStateDeltaAsync | [ValidationService.AcceptedTurnAndInkFeathers.cs:419](/workspace/The-Book-of-Eternity-Reborn/BookOfEternityClient/Services/Validation/ValidationService.AcceptedTurnAndInkFeathers.cs:419) | 429: var currentJson = await _fs.ReadFileAsync(path) ?? string.Empty; |
| ReadAcceptedTurnNoProgressRationaleTextAsync | [ValidationService.AcceptedTurnAndInkFeathers.cs:447](/workspace/The-Book-of-Eternity-Reborn/BookOfEternityClient/Services/Validation/ValidationService.AcceptedTurnAndInkFeathers.cs:447) | 448: var debugJson = await _fs.ReadFileAsync("output/debug_logs.json"); |
| ValidateAcceptedTurnInterfacePayloadInternalAsync | [ValidationService.AcceptedTurnAndInkFeathers.cs:582](/workspace/The-Book-of-Eternity-Reborn/BookOfEternityClient/Services/Validation/ValidationService.AcceptedTurnAndInkFeathers.cs:582) | 593: if (!_fs.FileExists(interfacePath))<br>596: var json = await _fs.ReadFileAsync(interfacePath); |
| NormalizeAcceptedTurnInterfaceDialogueOptionsAsync | [ValidationService.AcceptedTurnAndInkFeathers.cs:710](/workspace/The-Book-of-Eternity-Reborn/BookOfEternityClient/Services/Validation/ValidationService.AcceptedTurnAndInkFeathers.cs:710) | 712: ? DateTimeOffset.UtcNow : null;<br>715: await _fs.WriteFileAtomicAsync(interfacePath, projected.Json); |
| ValidateAcceptedTurnMortalCombatMaterializationInternalAsync | [ValidationService.AcceptedTurnAndInkFeathers.cs:720](/workspace/The-Book-of-Eternity-Reborn/BookOfEternityClient/Services/Validation/ValidationService.AcceptedTurnAndInkFeathers.cs:720) | 736: HasPositiveExperienceGain(await _fs.ReadFileAsync("game_state/player/experience.json")) &&<br>737: HasSkillMasteryChanges(await _fs.ReadFileAsync("game_state/player/skill_mastery.json")) &&<br>739: _fs.FileExists("game_state/core/player_status.json")); |
| ReadAcceptedTurnFilesModifiedAsync | [ValidationService.AcceptedTurnAndInkFeathers.cs:850](/workspace/The-Book-of-Eternity-Reborn/BookOfEternityClient/Services/Validation/ValidationService.AcceptedTurnAndInkFeathers.cs:850) | 852: var json = await _fs.ReadFileAsync("ready/turn_complete.json"); |
| HasMortalCombatState | [ValidationService.AcceptedTurnAndInkFeathers.cs:883](/workspace/The-Book-of-Eternity-Reborn/BookOfEternityClient/Services/Validation/ValidationService.AcceptedTurnAndInkFeathers.cs:883) | 887: _fs.FileExists("game_state/combat/combat_log.json") &#124;&#124;<br>888: _fs.FileExists("game_state/combat/enemies.json") &#124;&#124;<br>889: _fs.FileExists("game_state/combat/allies.json"); |
| TryReadStringPropertyFromJsonFileAsync | [ValidationService.AcceptedTurnAndInkFeathers.cs:951](/workspace/The-Book-of-Eternity-Reborn/BookOfEternityClient/Services/Validation/ValidationService.AcceptedTurnAndInkFeathers.cs:951) | 952: var json = await _fs.ReadFileAsync(relativePath); |
| TryReadNestedStringPropertyFromJsonFileAsync | [ValidationService.AcceptedTurnAndInkFeathers.cs:978](/workspace/The-Book-of-Eternity-Reborn/BookOfEternityClient/Services/Validation/ValidationService.AcceptedTurnAndInkFeathers.cs:978) | 979: var json = await _fs.ReadFileAsync(relativePath); |
| ValidateDirectChaosSeaGachaOutcomeAsync | [ValidationService.AcceptedTurnAndInkFeathers.cs:1035](/workspace/The-Book-of-Eternity-Reborn/BookOfEternityClient/Services/Validation/ValidationService.AcceptedTurnAndInkFeathers.cs:1035) | 1046: var currentSoulJson = await _fs.ReadFileAsync("game_state/meta/soul_state.json"); |
| ValidateChaosSeaTravelOutcomeAsync | [ValidationService.AcceptedTurnAndInkFeathers.cs:1207](/workspace/The-Book-of-Eternity-Reborn/BookOfEternityClient/Services/Validation/ValidationService.AcceptedTurnAndInkFeathers.cs:1207) | 1309: var currentGuardiansJson = await _fs.ReadFileAsync("game_state/meta/guardians.json"); |
| IsClientInkFeatherSpendAlreadyInSnapshotAsync | [ValidationService.AcceptedTurnAndInkFeathers.cs:1459](/workspace/The-Book-of-Eternity-Reborn/BookOfEternityClient/Services/Validation/ValidationService.AcceptedTurnAndInkFeathers.cs:1459) | 1472: var rollbackBytes = await _fs.ReadFileBytesAsync(rollbackPath); |
| ValidateAfterlifeClientPrepaidInkFeatherBalanceAsync | [ValidationService.AcceptedTurnAndInkFeathers.cs:1502](/workspace/The-Book-of-Eternity-Reborn/BookOfEternityClient/Services/Validation/ValidationService.AcceptedTurnAndInkFeathers.cs:1502) | 1507: var currentSoulJson = await _fs.ReadFileAsync("game_state/meta/soul_state.json"); |
| ValidateInkFeatherActionReceiptAsync | [ValidationService.AcceptedTurnAndInkFeathers.cs:1621](/workspace/The-Book-of-Eternity-Reborn/BookOfEternityClient/Services/Validation/ValidationService.AcceptedTurnAndInkFeathers.cs:1621) | 1623: var receiptJson = await _fs.ReadFileAsync(InkFeatherActionResultPath); |
| ValidateAbsorbFeathersOutcomeAsync | [ValidationService.AcceptedTurnAndInkFeathers.cs:1819](/workspace/The-Book-of-Eternity-Reborn/BookOfEternityClient/Services/Validation/ValidationService.AcceptedTurnAndInkFeathers.cs:1819) | 1838: var currentExperience = ReadPrimaryExperienceCounter(await _fs.ReadFileAsync("game_state/player/experience.json")); |
| ValidateLearnSkillOutcomeAsync | [ValidationService.AcceptedTurnAndInkFeathers.cs:1866](/workspace/The-Book-of-Eternity-Reborn/BookOfEternityClient/Services/Validation/ValidationService.AcceptedTurnAndInkFeathers.cs:1866) | 1914: var postSkillJson = await _fs.ReadFileAsync(skillFile); |
| ValidateFateShieldOutcomeAsync | [ValidationService.AcceptedTurnAndInkFeathers.cs:1946](/workspace/The-Book-of-Eternity-Reborn/BookOfEternityClient/Services/Validation/ValidationService.AcceptedTurnAndInkFeathers.cs:1946) | 1961: var currentEffectsJson = await _fs.ReadFileAsync("game_state/player/effects.json"); |
| ValidateCultivateEnlightenmentOutcomeAsync | [ValidationService.AcceptedTurnAndInkFeathers.cs:2153](/workspace/The-Book-of-Eternity-Reborn/BookOfEternityClient/Services/Validation/ValidationService.AcceptedTurnAndInkFeathers.cs:2153) | 2186: var currentExperience = await ReadEnlightenmentExperienceAsync(await _fs.ReadFileAsync("game_state/meta/soul_state.json")); |
| ValidateAbodeOfferingOutcomeAsync | [ValidationService.AcceptedTurnAndInkFeathers.cs:2289](/workspace/The-Book-of-Eternity-Reborn/BookOfEternityClient/Services/Validation/ValidationService.AcceptedTurnAndInkFeathers.cs:2289) | 2505: var postJournalJson = await _fs.ReadFileAsync(GuardianPowerEventState.JournalPath);<br>2682: var currentSoulJson = await _fs.ReadFileAsync("game_state/meta/soul_state.json"); |
| ValidateGuardianAffectedFilesChangedAsync | [ValidationService.AcceptedTurnAndInkFeathers.cs:2881](/workspace/The-Book-of-Eternity-Reborn/BookOfEternityClient/Services/Validation/ValidationService.AcceptedTurnAndInkFeathers.cs:2881) | 2928: var current = await _fs.ReadFileAsync(file); |
| ValidateAcceptedTurnTransientOutputFreshnessAsync | [ValidationService.AcceptedTurnAndInkFeathers.cs:3125](/workspace/The-Book-of-Eternity-Reborn/BookOfEternityClient/Services/Validation/ValidationService.AcceptedTurnAndInkFeathers.cs:3125) | 3130: var currentContent = await _fs.ReadFileAsync(relativePath) ?? string.Empty; |
| LoadValidatedPendingTurnSnapshotLookupSync | [ValidationService.AcceptedTurnAndInkFeathers.cs:3432](/workspace/The-Book-of-Eternity-Reborn/BookOfEternityClient/Services/Validation/ValidationService.AcceptedTurnAndInkFeathers.cs:3432) | 3436: var manifestJson = _fs.ReadFileSync( |
| ReadPendingTurnSnapshotAuthoritySync | [ValidationService.AcceptedTurnAndInkFeathers.cs:3457](/workspace/The-Book-of-Eternity-Reborn/BookOfEternityClient/Services/Validation/ValidationService.AcceptedTurnAndInkFeathers.cs:3457) | 3460: : _fs.ReadFileSync(PendingTurnSnapshotAuthority.AuthorityPath); |
| ReadRelativeFileBytesFromWorkspace | [ValidationService.AcceptedTurnAndInkFeathers.cs:3464](/workspace/The-Book-of-Eternity-Reborn/BookOfEternityClient/Services/Validation/ValidationService.AcceptedTurnAndInkFeathers.cs:3464) | 3468: return CurrentWorldReadPhase() is { } phase ? phase.ReadBytesSync(relativePath) : _fs.ReadFileBytesSync(relativePath); |
| ReadValidatedPendingTurnSnapshotFileAsync | [ValidationService.AcceptedTurnAndInkFeathers.cs:3506](/workspace/The-Book-of-Eternity-Reborn/BookOfEternityClient/Services/Validation/ValidationService.AcceptedTurnAndInkFeathers.cs:3506) | 3523: ? CurrentWorldReadPhase() is { } phase ? await phase.ReadBytesAsync(snapshotPath) : await _fs.ReadFileBytesAsync(snapshotPath)<br>3524: : await _fs.ReadFileBytesAsync(writeLease, snapshotPath); |
| ReadValidatedPendingTurnSnapshotFileSync | [ValidationService.AcceptedTurnAndInkFeathers.cs:3549](/workspace/The-Book-of-Eternity-Reborn/BookOfEternityClient/Services/Validation/ValidationService.AcceptedTurnAndInkFeathers.cs:3549) | 3565: var snapshotBytes = CurrentWorldReadPhase() is { } phase ? phase.ReadBytesSync(snapshotPath) : _fs.ReadFileBytesSync(snapshotPath); |
| ReadRequiredValidatedCurrentPreTurnTrackedFileAsync | [ValidationService.AcceptedTurnAndInkFeathers.cs:3621](/workspace/The-Book-of-Eternity-Reborn/BookOfEternityClient/Services/Validation/ValidationService.AcceptedTurnAndInkFeathers.cs:3621) | 3622: var currentJson = await _fs.ReadFileAsync(relativePath); |
| LoadPendingTurnRequestValidationContextSync | [ValidationService.AcceptedTurnAndInkFeathers.cs:4010](/workspace/The-Book-of-Eternity-Reborn/BookOfEternityClient/Services/Validation/ValidationService.AcceptedTurnAndInkFeathers.cs:4010) | 4012: ? CurrentWorldReadPhase() is { } phase ? phase.ReadTextSync(requestPath) : _fs.ReadFileSync(requestPath)<br>4013: : _fs.ReadFileAsync(writeLease, requestPath) |
| LoadValidatedPendingTurnSnapshotLookupAsync | [ValidationService.AcceptedTurnAndInkFeathers.cs:5077](/workspace/The-Book-of-Eternity-Reborn/BookOfEternityClient/Services/Validation/ValidationService.AcceptedTurnAndInkFeathers.cs:5077) | 5082: ? _fs.FileExists(PendingTurnSnapshotManifestPath)<br>5083: : _fs.FileExists(writeLease, PendingTurnSnapshotManifestPath); |
| ReadAcceptedTurnValidationFileAsync | [ValidationService.AcceptedTurnAndInkFeathers.cs:5225](/workspace/The-Book-of-Eternity-Reborn/BookOfEternityClient/Services/Validation/ValidationService.AcceptedTurnAndInkFeathers.cs:5225) | 5227: ? CurrentWorldReadPhase() is { } phase ? phase.ReadTextAsync(path) : _fs.ReadFileAsync(path)<br>5228: : _fs.ReadFileAsync(writeLease, path); |
| ReadRelativeFileBytesFromWorkspace | [ValidationService.AcceptedTurnAndInkFeathers.cs:5233](/workspace/The-Book-of-Eternity-Reborn/BookOfEternityClient/Services/Validation/ValidationService.AcceptedTurnAndInkFeathers.cs:5233) | 5238: ? CurrentWorldReadPhase() is { } phase ? phase.ReadBytesSync(relativePath) : _fs.ReadFileBytesSync(relativePath)<br>5239: : _fs.ReadFileBytesAsync(writeLease, relativePath) |
| ReadCanonicalFileSync | [ValidationService.AcceptedTurnAndInkFeathers.cs:5266](/workspace/The-Book-of-Eternity-Reborn/BookOfEternityClient/Services/Validation/ValidationService.AcceptedTurnAndInkFeathers.cs:5266) | 5270: return _fs.ReadFileSync(relativePath); |
| DescribeTrackedFileChangeAgainstManifestAsync | [ValidationService.AcceptedTurnAndInkFeathers.cs:5392](/workspace/The-Book-of-Eternity-Reborn/BookOfEternityClient/Services/Validation/ValidationService.AcceptedTurnAndInkFeathers.cs:5392) | 5393: var current = await _fs.ReadFileAsync(relativePath) ?? string.Empty; |
| SoulImprintExistsAsync | [ValidationService.AcceptedTurnAndInkFeathers.cs:7260](/workspace/The-Book-of-Eternity-Reborn/BookOfEternityClient/Services/Validation/ValidationService.AcceptedTurnAndInkFeathers.cs:7260) | 7261: var json = await _fs.ReadFileAsync("game_state/meta/soul_state.json"); |
| PendingInkActionExistsAsync | [ValidationService.AcceptedTurnAndInkFeathers.cs:7290](/workspace/The-Book-of-Eternity-Reborn/BookOfEternityClient/Services/Validation/ValidationService.AcceptedTurnAndInkFeathers.cs:7290) | 7291: var json = await _fs.ReadFileAsync(PendingInkActionsPath); |
| SoulImprintHasMinimalCoreTraitsAsync | [ValidationService.AcceptedTurnAndInkFeathers.cs:7334](/workspace/The-Book-of-Eternity-Reborn/BookOfEternityClient/Services/Validation/ValidationService.AcceptedTurnAndInkFeathers.cs:7334) | 7335: var json = await _fs.ReadFileAsync("game_state/meta/soul_state.json"); |
| SoulImprintHasSourceProvenanceAsync | [ValidationService.AcceptedTurnAndInkFeathers.cs:7373](/workspace/The-Book-of-Eternity-Reborn/BookOfEternityClient/Services/Validation/ValidationService.AcceptedTurnAndInkFeathers.cs:7373) | 7377: var json = await _fs.ReadFileAsync("game_state/meta/soul_state.json"); |
| ValidateAcceptedTurnReasoningInternalAsync | [ValidationService.AcceptedTurnAndInkFeathers.cs:7525](/workspace/The-Book-of-Eternity-Reborn/BookOfEternityClient/Services/Validation/ValidationService.AcceptedTurnAndInkFeathers.cs:7525) | 7535: var debugJson = await _fs.ReadFileAsync("output/debug_logs.json"); |
| ValidateAcceptedTurnSpecialActionOutcomesInternalAsync | [ValidationService.AcceptedTurnAndInkFeathers.cs:8042](/workspace/The-Book-of-Eternity-Reborn/BookOfEternityClient/Services/Validation/ValidationService.AcceptedTurnAndInkFeathers.cs:8042) | 8191: var soulJson = await _fs.ReadFileAsync("game_state/meta/soul_state.json"); |
| ResolveAcceptedTurnRequestDocumentForSpecialActionValidationAsync | [ValidationService.AcceptedTurnAndInkFeathers.cs:8446](/workspace/The-Book-of-Eternity-Reborn/BookOfEternityClient/Services/Validation/ValidationService.AcceptedTurnAndInkFeathers.cs:8446) | 8447: var requestJson = await _fs.ReadFileAsync("input/turn_request.json"); |
| ValidatePendingMemoryLegacyApplicationInternalAsync | [ValidationService.AcceptedTurnAndInkFeathers.cs:8488](/workspace/The-Book-of-Eternity-Reborn/BookOfEternityClient/Services/Validation/ValidationService.AcceptedTurnAndInkFeathers.cs:8488) | 8492: var soulJson = await _fs.ReadFileAsync("game_state/meta/soul_state.json"); |
| ReadCurrentCharacteristicValueAsync | [ValidationService.AcceptedTurnAndInkFeathers.cs:8676](/workspace/The-Book-of-Eternity-Reborn/BookOfEternityClient/Services/Validation/ValidationService.AcceptedTurnAndInkFeathers.cs:8676) | 8677: var json = await _fs.ReadFileAsync("game_state/misc/characteristics.json"); |
| PassiveSkillMatchesExpectedShapeAsync | [ValidationService.AcceptedTurnAndInkFeathers.cs:8705](/workspace/The-Book-of-Eternity-Reborn/BookOfEternityClient/Services/Validation/ValidationService.AcceptedTurnAndInkFeathers.cs:8705) | 8706: var json = await _fs.ReadFileAsync("game_state/player/skills_passive.json"); |
| ReadJsonObjectForSpecialActionAsync | [ValidationService.AcceptedTurnAndInkFeathers.cs:9034](/workspace/The-Book-of-Eternity-Reborn/BookOfEternityClient/Services/Validation/ValidationService.AcceptedTurnAndInkFeathers.cs:9034) | 9035: var json = await _fs.ReadFileAsync(path); |
| ResolveGuardianPolicyContextAsync | [ValidationService.GuardianPolicyKernel.cs:195](/workspace/The-Book-of-Eternity-Reborn/BookOfEternityClient/Services/Validation/ValidationService.GuardianPolicyKernel.cs:195) | 197: _gameStateValidationGuardianPolicyContextCache.Value is { } cachedContext)<br>202: var currentJson = await _fs.ReadFileAsync("game_state/meta/guardians.json");<br>206: _gameStateValidationGuardianPolicyContextCache.Value = context; |
| ResolveGuardianPolicyContextSync | [ValidationService.GuardianPolicyKernel.cs:211](/workspace/The-Book-of-Eternity-Reborn/BookOfEternityClient/Services/Validation/ValidationService.GuardianPolicyKernel.cs:211) | 214: _gameStateValidationGuardianPolicyContextCache.Value is { } cachedContext)<br>223: _gameStateValidationGuardianPolicyContextCache.Value = context; |
| IsIdleStateWithoutActiveTurn | [ValidationService.GuardianPolicyKernel.cs:1569](/workspace/The-Book-of-Eternity-Reborn/BookOfEternityClient/Services/Validation/ValidationService.GuardianPolicyKernel.cs:1569) | 1573: return !_fs.FileExists(PendingTurnSnapshotManifestPath) &&<br>1574: !_fs.FileExists("input/turn_request.json") &&<br>1575: !_fs.FileExists("ready/turn_complete.json") &&<br>1576: !_fs.FileExists("ready/turn_error.json"); |
| ResolveValidatedGuardianTrackedSnapshotFileAsync | [ValidationService.GuardianPolicyKernel.cs:1650](/workspace/The-Book-of-Eternity-Reborn/BookOfEternityClient/Services/Validation/ValidationService.GuardianPolicyKernel.cs:1650) | 1668: var snapshotBytes = await _fs.ReadFileBytesAsync(snapshotPath); |
| ResolveValidatedGuardianTrackedSnapshotFileSync | [ValidationService.GuardianPolicyKernel.cs:1690](/workspace/The-Book-of-Eternity-Reborn/BookOfEternityClient/Services/Validation/ValidationService.GuardianPolicyKernel.cs:1690) | 1711: snapshotBytes = _fs.ReadFileBytesAsync(snapshotPath).GetAwaiter().GetResult(); |
| ResolveGuardianProjectTrackerPolicyContextSync | [ValidationService.GuardianPolicyKernel.cs:1769](/workspace/The-Book-of-Eternity-Reborn/BookOfEternityClient/Services/Validation/ValidationService.GuardianPolicyKernel.cs:1769) | 1772: _gameStateValidationGuardianProjectTrackerPolicyContextCache.Value is { } cachedContext)<br>1791: _gameStateValidationGuardianProjectTrackerPolicyContextCache.Value = context; |
| ReadKnownNpcReferencesAsync | [ValidationService.InventoryNpcWorldCrossRefs.cs:201](/workspace/The-Book-of-Eternity-Reborn/BookOfEternityClient/Services/Validation/ValidationService.InventoryNpcWorldCrossRefs.cs:201) | 208: await _fs.ReadFileAsync("game_state/npcs/npc_core.json") |
| ValidateAchievementUnlockNarrativeMarkersAsync | [ValidationService.MetaCodexAndAchievements.cs:745](/workspace/The-Book-of-Eternity-Reborn/BookOfEternityClient/Services/Validation/ValidationService.MetaCodexAndAchievements.cs:745) | 746: var json = await _fs.ReadFileAsync("game_state/meta/achievements.json"); |
| TryResolveCurrentRealmAsync | [ValidationService.NpcWorldAndMeta.cs:578](/workspace/The-Book-of-Eternity-Reborn/BookOfEternityClient/Services/Validation/ValidationService.NpcWorldAndMeta.cs:578) | 579: var soulJson = await _fs.ReadFileAsync("game_state/meta/soul_state.json"); |
| CollectStructuredNpcUpdatesAsync | [ValidationService.NpcWorldAndMeta.cs:760](/workspace/The-Book-of-Eternity-Reborn/BookOfEternityClient/Services/Validation/ValidationService.NpcWorldAndMeta.cs:760) | 761: var npcJson = await _fs.ReadFileAsync("game_state/npcs/npc_core.json"); |
| CollectStructuredResidentUpdatesAsync | [ValidationService.NpcWorldAndMeta.cs:944](/workspace/The-Book-of-Eternity-Reborn/BookOfEternityClient/Services/Validation/ValidationService.NpcWorldAndMeta.cs:944) | 945: var residentJson = await _fs.ReadFileAsync(GuardianAbodeResidentState.StatePath); |
| CollectStructuredShiningActorUpdatesAsync | [ValidationService.NpcWorldAndMeta.cs:988](/workspace/The-Book-of-Eternity-Reborn/BookOfEternityClient/Services/Validation/ValidationService.NpcWorldAndMeta.cs:988) | 989: var currentJson = await _fs.ReadFileAsync(ShiningAbodeState.StatePath); |
| CollectStructuredAfterlifeEntityProfileUpdatesAsync | [ValidationService.NpcWorldAndMeta.cs:1016](/workspace/The-Book-of-Eternity-Reborn/BookOfEternityClient/Services/Validation/ValidationService.NpcWorldAndMeta.cs:1016) | 1017: var currentJson = await _fs.ReadFileAsync(AfterlifeEntityProfileState.StatePath); |
| ResolveCanonicalActorsMentionedInPlayerActionAsync | [ValidationService.NpcWorldAndMeta.cs:2339](/workspace/The-Book-of-Eternity-Reborn/BookOfEternityClient/Services/Validation/ValidationService.NpcWorldAndMeta.cs:2339) | 2347: var guardians = BuildGuardianMusingAuditMap(await _fs.ReadFileAsync("game_state/meta/guardians.json"));<br>2352: await _fs.ReadFileAsync("game_state/npcs/npc_core.json"),<br>2353: await _fs.ReadFileAsync("game_state/npcs/npc_journals.json"));<br>2358: await _fs.ReadFileAsync(GuardianAbodeResidentState.StatePath));<br>2363: await _fs.ReadFileAsync(AfterlifeEntityProfileState.StatePath));<br>2373: await _fs.ReadFileAsync(ShiningAbodeState.StatePath));<br>2383: var shiningJson = await _fs.ReadFileAsync(ShiningAbodeState.StatePath); |
| ReadMortalPlayerScopeActorAliasesAsync | [ValidationService.NpcWorldAndMeta.cs:2638](/workspace/The-Book-of-Eternity-Reborn/BookOfEternityClient/Services/Validation/ValidationService.NpcWorldAndMeta.cs:2638) | 2654: var soulJson = await _fs.ReadFileAsync("game_state/meta/soul_state.json");<br>2670: var json = await _fs.ReadFileAsync(ScenarioCoreService.ManifestPath); |
| AddMortalPersistentActorAliasesFromFileAsync | [ValidationService.NpcWorldAndMeta.cs:2777](/workspace/The-Book-of-Eternity-Reborn/BookOfEternityClient/Services/Validation/ValidationService.NpcWorldAndMeta.cs:2777) | 2780: var json = await _fs.ReadFileAsync(path); |
| ValidateRelevantGuardianMusingDeltasAsync | [ValidationService.NpcWorldAndMeta.cs:3150](/workspace/The-Book-of-Eternity-Reborn/BookOfEternityClient/Services/Validation/ValidationService.NpcWorldAndMeta.cs:3150) | 3159: var currentJson = await _fs.ReadFileAsync(guardiansPath);<br>3162: await _fs.ReadFileAsync(GuardianThoughtJournalState.StatePath));<br>3182: if (!preTurnGuardiansFile.IsUsable &#124;&#124; !preTurnStructuredThoughtsFile.IsUsable)<br>3185: var preTurnGuardians = BuildGuardianMusingAuditMap(preTurnGuardiansFile.Json);<br>3187: preTurnStructuredThoughtsFile.Json); |
| ValidateRelevantMortalNpcThoughtJournalDeltasAsync | [ValidationService.NpcWorldAndMeta.cs:3364](/workspace/The-Book-of-Eternity-Reborn/BookOfEternityClient/Services/Validation/ValidationService.NpcWorldAndMeta.cs:3364) | 3376: await _fs.ReadFileAsync(npcCorePath),<br>3377: await _fs.ReadFileAsync(npcJournalsPath)); |
| ValidateRelevantAfterlifeResidentThoughtJournalDeltasAsync | [ValidationService.NpcWorldAndMeta.cs:3458](/workspace/The-Book-of-Eternity-Reborn/BookOfEternityClient/Services/Validation/ValidationService.NpcWorldAndMeta.cs:3458) | 3468: await _fs.ReadFileAsync(GuardianAbodeResidentState.StatePath));<br>3481: if (!preTurnFile.IsUsable)<br>3485: preTurnFile.Json); |
| ValidateRelevantAfterlifeEntityMemoryLedgerDeltasAsync | [ValidationService.NpcWorldAndMeta.cs:3656](/workspace/The-Book-of-Eternity-Reborn/BookOfEternityClient/Services/Validation/ValidationService.NpcWorldAndMeta.cs:3656) | 3666: await _fs.ReadFileAsync(AfterlifeEntityProfileState.StatePath));<br>3683: if (!preTurnFile.IsUsable)<br>3687: preTurnFile.Json); |
| ValidateRelevantShiningFactionMemoryDeltasAsync | [ValidationService.NpcWorldAndMeta.cs:3749](/workspace/The-Book-of-Eternity-Reborn/BookOfEternityClient/Services/Validation/ValidationService.NpcWorldAndMeta.cs:3749) | 3759: await _fs.ReadFileAsync(ShiningAbodeState.StatePath));<br>3772: if (!preTurnFile.IsUsable)<br>3776: preTurnFile.Json); |
| ValidateRelevantAfterlifeActorsHaveCanonicalMemoryOwnersAsync | [ValidationService.NpcWorldAndMeta.cs:3851](/workspace/The-Book-of-Eternity-Reborn/BookOfEternityClient/Services/Validation/ValidationService.NpcWorldAndMeta.cs:3851) | 3860: await _fs.ReadFileAsync("game_state/meta/guardians.json"));<br>3862: await _fs.ReadFileAsync(GuardianAbodeResidentState.StatePath));<br>3864: await _fs.ReadFileAsync(AfterlifeEntityProfileState.StatePath));<br>3866: ? BuildShiningFactionMemoryAuditMap(await _fs.ReadFileAsync(ShiningAbodeState.StatePath)) |
| TryReadCurrentTurnGachaBaseRaritySync | [ValidationService.QuestsRivalsFactionsAndWorld.cs:2660](/workspace/The-Book-of-Eternity-Reborn/BookOfEternityClient/Services/Validation/ValidationService.QuestsRivalsFactionsAndWorld.cs:2660) | 2661: var requestJson = _fs.ReadFileSync("input/turn_request.json"); |
| ReadCompletedGuardianMusingsForComparison | [ValidationService.GuardianMusingsPublication.cs:243](/workspace/The-Book-of-Eternity-Reborn/BookOfEternityClient/Services/Validation/ValidationService.GuardianMusingsPublication.cs:243) | 245: var lease = _fs.AcquireCanonicalWriteLeaseAsync().GetAwaiter().GetResult();<br>247: try { return completion.ReadForComparisonAsync(_fs, lease, currentRoot).GetAwaiter().GetResult() ?? []; }<br>249: finally { CoordinatedStateWriteHelper.ReleaseOwnedLeaseAsync(_fs, lease, false, failure).GetAwaiter().GetResult(); } |

FINALSTOP.
