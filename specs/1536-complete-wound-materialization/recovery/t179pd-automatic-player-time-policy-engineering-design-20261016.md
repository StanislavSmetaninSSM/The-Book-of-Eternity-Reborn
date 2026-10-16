# T179-Pd: bounded automatic/player/level/time semantic policy engineering contract

DESIGN for independent review. Frozen production source: `3fdc16e8a954458f1e56bafa42ca24a66c06d020`; later parent test/docs/custody work is independent. Assigned Astra/High, serving backend unknown. Read-only source inspection; only this new /tmp pair written. No tests, builds, discovery, runtime, native operations, network or delegation. All proposed checks are FUTURE / UNRUN. Source aliases, blobs, SHA-256 and exact projections accompany this report.

## Decision and bounded completion criterion

Implement a **shared policy family**, consumed by the real ordinary validator/resolvers and by the actual engine's HOT Capturing phase. It returns named policy results and source/coverage dispositions. It does not issue S, full automatic admission, a time/event authority, B, common authority or M3.

There is enough source to close the extraction described here without durable P/A, a new pathname, changed GM schemas or replaying any mutation. Three things must remain distinct:
1. Existing ordinary validation and resolver semantics, including permissive skips, warnings and distinct alias priorities.
2. Approved strict joint automatic calculation preconditions (design §7.1/D2), which are narrower than ordinary permissive readers.
3. Genuine source/baseline/initializer ownership. A successful data calculation or an empty issue list proves neither.

The family is complete only when the same bodies serve ordinary callers, a genuine original-first corrected submission reaches the family under the actual capturing attempt, all family physical reads close/recheck, and the result explicitly retains uncovered sibling/domain/history obligations. No standalone unused kernel is the completion claim. Existing custody is not redesigned here. Its `UncoveredInSignedInventory` remains unknown original coverage, never signed absence or a pristine baseline.

## Source-backed behavior that must remain exact

### Player file validation and required-field reads

PI58–69 invokes player_status, experience, status_changes, effects, wounds, custom_states, stealth, weight, transformation, characteristics in that order. PI3592–3661 `ValidatePlayerFile` first respects `ShouldValidateStateFile`, then `ReadSameTurnOwnerCurrentTextAsync`; null/empty/whitespace returns without an issue. The same-turn reader can select genuine spiritual draft, meta/world phase or ordinary FS (SV18–23). Do not replace this with a new global mutable override or bypass existing browser/draft ownership.

For player_status, the exact sequence is: reject three legacy gauge fields → **second physical/read-family call** `ValidateFileFields(currentCondition,money)` → RequireString currentCondition → optional description → activeConditions strings → integer nonnegative money if present → return. N4888–4916 currently rereads scalar _fs rather than the same-turn view, catches every exception and logs debug. Extraction must preserve that ordinary read and catch order. The HOT adapter binds this second occurrence to its actual lease and latches source faults even if the legacy catch would return no issue. Do not silently reuse the first parsed document as the second observation.

Other files execute the complete existing `ValidatePlayerContract` first. Its automatic-relevant prefix (PI3663–3677) rejects legacy resource deltas, checks nested playerStatus, experienceGained, moneyChange, statsIncreased, statsDecreased, setCharacteristics. **All remaining skill/inventory/effect/wound/custom-state/effort/appearance siblings still execute in ordinary validation at their existing positions.** This family extracts the prefix, not a claim that the rest are absent or accepted. Base-characteristics/experience file checks run only after the complete generic contract. Results retain these operation slots; do not regroup all automatic errors ahead of sibling errors.

- Change-number fields accept JSON Number, without integer/range interpretation here. `experienceGained` string can therefore receive a prefix error even though the later experience-specific number-or-string helper accepts a nonempty string.
- stats arrays must be arrays of nonblank strings in Characteristics.All using OrdinalIgnoreCase. No trimming in this validator, no deduplication: order and duplicate occurrences remain.
- setCharacteristics requires object, exact ordinal canonical lowercase names and integer Int32 values. It does not enforce 1–100 here.
- Base characteristics: object; underscore metadata exempt; canonical names ordinal; integer errors may be emitted twice by the two loops; missing names and values outside 1–100 are warnings. Presence of exact setCharacteristics switches the command-surface branch and skips completeness/range warnings. Preserve this branch; a legal command is not already materialized base stats.
- Experience: object; optional experienceGained/totalExperience/experienceForNextLevel accept a Number or any nonblank string (not necessarily numeric). Missing both level/playerLevel number-or-string yields a warning. Optional effort tracker uses existing object, nullable-string and number-or-string checks. Preserve any duplicate effort checks caused by generic and specific passes.
- JsonException becomes the existing player-file error. Other exception behavior, including root-kind failures from TryGetProperty, must not be silently normalized to a new successful empty result.

Bootstrap required fields BP55–128 are additional policy: soul existence; soulName/currentRealm required reads; current soul realm, and for any resolved realm not classified ChaosSea, player_status existence and required currentCondition/money. The enclosing required-fields method also invokes actual resource materialization first. **Do not call that encompassing method under the new lease.** Extract its soul/player presence-and-fields portion at its existing call position; resource policy remains separately required for full S. The message's Mortal wording does not authorize narrowing the actual !IsChaosSea predicate.

### Three distinct level policies

| Named policy | Exact source / result |
|---|---|
| Training source | CS283–331 GetPlayerLevel: experience.level Number/GetInt32 first, then experience.playerLevel; otherwise player_status.level; otherwise soul.level; otherwise 1. Ordinary per-file catch falls through, joint derived projection rethrows. A numeric <=1 value is still returned; no positivity repair here. |
| Award source | TL2506–2537 CheckLevelUpAsync: experience.level then playerLevel Number/GetInt32; start 1; if resulting level <=1, read player_status.level; no soul fallback. A parse/GetInt32 fault reaches the outer ordinary warning catch, not training's fallback. |
| XP materialization | A760–807: only if actual realm helper says Mortal; read current experience; object and number-like total/threshold required; threshold>0; **playerLevel before level**; decimal JSON numeric or invariant NumberStyles.Number string; level>=1 and total>=threshold emits existing error. No threshold formula, alias equality rule, original-XP-delta rule, or stat-points read. Missing/unparseable data often skips this check; other validators still own shape. |

Realm helper A811–831 reads input/turn_request.json progressionControl.currentRealm first and returns true if string contains Mortal or Смерт case-insensitively. Otherwise it reads soul.currentRealm and tests the same; a non-Mortal request does not veto a Mortal soul. Exact helper parsing and JsonException/empty returns (A950–1001) remain. A private source owner binds real request/current soul bytes; it must not accept a caller-provided isMortal flag.

### Ordinary arithmetic, initialization and mutations are not admission

CS144–187 ApplyStatsIncreasedAsync reads base stats then training level; ordered lower-invariant+Trim names; unknown logs and skips; cap is playerLevel*2; cap branch precedes >=100; increments and saves base stats only when applied count>0. CS246–280 LoadBaseStats seeds twelve ones, accepts integer strings, and retains ordinary parse warnings/fallback; it is not strict original evidence.

TL2425–2499 reads status_changes, gathers only string array members, returns if none, calls that writer, then for blocked stats reads experienceForNextLevel with default100, computes max(25, rounded 5%) and a compObj/log. **There is no XP write.** It then emits notifications and rewrites status_changes without statsIncreased using its existing dictionary/serializer. No new compensation publisher is authorized by the comment.

TL2410–2418 sequences stats writer → ComputeAndWriteAsync → CheckLevelUp. Award code reads watermark; if <=0 reads computed_characteristics.playerLevel and unspent points, may initialize watermark by the existing heuristic, then max(_lastKnownLevel,1,watermark); grants five per gained level, AddStatPoints (which recomputes/writes), marks watermark, updates _lastKnownLevel and offers distribution. Errors warn except publication uncertainty. Preserve this ordinary sequence and catches. New family code must never invoke it as a read-only planner.

CS335–399 stat-points readers accept integer strings, fallback0, and malformed/blank/nonobject may become empty object in ordinary mode. Mark clamps level to >=1 and only advances; SaveUnspent clamps >=0; AddStatPoints and distribution write and recompute. InitializeForNewIncarnation writes twelve ones, eight unspent and watermark1. None is read-only admission or cold initializer proof.

Approved strict joint calculator CA13–139 remains its accepted D2 behavior: canonical complete base stats, integer watermark>=1, nonnegative integer balance, checked arithmetic, no legacy initialization heuristic, no _lastKnownLevel receipt, no XP write, exact three after-images and sibling retention. Do not replace its strict parser by ordinary permissive loaders, or change ordinary behavior to match it. Its data output is not a source grant. The existing accepted calculator need not be rewritten merely to make this family.

### Time validation, progression and effects remain separate owners

LC146–151 selects world_time through ValidateFlexibleStateFile; LC7734–7825 supplies the generic file policy: selected read, blank skip, object/array only, underscore exemption, allowed-key membership case-insensitive, missing-any-allowed early return, ordered unknown-key errors. A root array checks item kinds and returns without calling the contract. Exact field dispatch within validators is case-sensitive. Preserve arrays/case behavior, not a new object-only or alias-equality rule.

N8143–8182 emits timeChange+setWorldTime mutual-exclusion error first when both are present and non-null. Other world checks retain their positions before the time leaf calls. Q1796–1995:
- timeChange absent/null allowed; otherwise Int32 nonnegative; zero valid.
- setWorldTime absent/null allowed; otherwise object; required year, nonblank monthName, dayOfMonth, nonblank timeOfDay. Missing required fields emits one issue and returns. Year/day use existing integer helper; day>0; existing HH:MM regex. **currentTimeInMinutes is optional and is not independently validated by this nested-object method.** Do not add a nested numeric/range constraint as “extraction”.
- Direct root absolute state is triggered by any exact absolute field; then all year/month/day/time/currentTime required. Direct minutes Int32>=0; day>0; HH:MM. No calendar-name arithmetic.
- Mixed direct fields+timeChange or direct fields+setWorldTime are not rejected solely for being mixed. Keep them lawful when their actual checks pass.

PS1577–1648 progression resolver: direct currentTimeInMinutes Int32 first; then nested setWorldTime numeric; then a calendar-looking nested absolute means fallback+unresolved; then allowed incremental timeChange gives max(0,fallback+delta); then calendar-looking direct means unresolved; otherwise fallback+unresolved. Ordinary wrapper catches/logs all exceptions. Afterlife callers can disable incremental. PBasis166–185 currently duplicates this with incremental allowed and a narrower catch. Preserve both adapters' decode/catch/log behavior while sharing the parsed pure resolver.

EF707–749 effect time: non-null nested setWorldTime has precedence; valid nonnegative Int64 nested minutes yields value, otherwise null **without falling back to direct**. Only absent/null nested permits direct nonnegative Int64. No incremental arithmetic. This existing pure method is reusable as-is; no need to recompute lifecycle scheduling.

ST661–713 is display formatting only: direct formatted calendar, nested formatted calendar, incremental display, and its own number/string reader. It is not D9 authority and need not be refactored into the semantic family.

CN406–442 invokes NormalizeSoulStateAsync; CNS316–399 builds current soul state from real authority/baseline and writes through its named owner. That normalization remains outside this family, even though soul.level/realm are read here. No normalization call is used to make a malformed root pass.

No world_time writer was found in the canonical normalizer entry/list; CN CanonicalAccumulatedFiles and source searches distinguish GM-authored time state from schedule bookkeeping. PS EnsureInitialized/BuildControl/Sanitize/Apply mutate schedule and sample LastUpdatedUtc; never call them to obtain a new comparison baseline. CS WriteComputedFile samples _lastComputed UTC. New family kernels do not sample UTC/GUID, tick, allocate gameplay IDs, draw dice or produce derived bytes. Existing actual first-pass samples/history remain with their genuine owners.

## Concrete shared API and integration contract

The minimal first implementation slice is A (shared ordinary policies) plus the actual Capturing consumer in B for those same named policies. Do not implement all legacy initialization/distribution orchestration anew. The source owner's result is partial by construction; this is substantive policy reuse, not another promise of a future reader.

### A. Pure policy bodies used by ordinary production immediately

Add `Services/Validation/ValidationService.AutomaticPlayerPolicy.cs` and `ValidationService.WorldTimePolicy.cs` as partial files. Keep existing common pure helpers in the partial class; no filesystem member access inside extracted bodies. Proposed private/internal data methods (all arguments are data, none grant authority):

```csharp
AppendPlayerAutomaticCommandPrefix(JsonElement root, string context, List<ValidationIssue> issues);
AppendPlayerStatusLegacyFields(JsonElement root, string context, List<ValidationIssue> issues);
AppendPlayerStatusBody(JsonElement root, string context, List<ValidationIssue> issues);
AppendRequiredFields(JsonElement root, string context, ReadOnlySpan<string> fields, List<ValidationIssue> issues);
AppendBaseCharacteristicsPolicy(JsonElement root, string context, List<ValidationIssue> issues);
AppendExperiencePolicy(JsonElement root, string context, List<ValidationIssue> issues);
AppendLevelMaterializationPolicy(JsonElement experience, List<ValidationIssue> issues);
AppendWorldTimeMutualExclusion(JsonElement root, string context, List<ValidationIssue> issues);
AppendWorldTimeFields(JsonElement root, string context, List<ValidationIssue> issues);
```

These are exact moves/forwarders of the pinned bodies, not newly written validators. Required fields stays between status legacy rejection and status body; ordinary ValidateFileFields retains its second read and try/catch/log. ValidatePlayerContract calls the prefix then its untouched remaining siblings. ValidatePlayerFile calls specific kernels at their original late slots. Existing level public entry retains realm/source IO and parse/error boundaries and calls the pure threshold kernel. World contract calls mutual exclusion at its original first slot and time fields at the original later slot. Extract the existing flexible wrapper body only as needed to share the exact world_time envelope; its ordinary allowedKeys/branch/catch/read order stays intact.

Move no extra checks from the generic player/world sibling graph into this bounded kernel. Pure helper closure includes existing RejectLegacyPlayerResourceFields, ValidatePlayerStatus, ValidatePlayerChangeNumber, ValidatePlayerStatArray, ValidateSetCharacteristics, ValidateEffortTrackerObject, HasNumberOrString, ValidateOptionalNumberOrString/NullableString/String, RequireObject/String/ArrayOfStrings, ValidateNonNegativeNumberField/IntegerField, HasNonEmptyString and exact TimeOfDayRegex. They perform no FS, clock, GUID or lease operations. Preserve severity, code, section, Expected/Actual/RepairHint and issue multiplicity.

Add `Services/CharacteristicsService.LevelPolicies.cs` for exact pure parsed-value seams:
- `ReadTrainingExperienceLevel(JsonElement)`, `ReadTrainingSingleLevel(JsonElement)`: nullable result for no matching numeric field; keep GetInt32 faults.
- `ReadAwardExperienceLevel(JsonElement)`, `ReadAwardStatusLevel(JsonElement)`: separate named methods, even where current leaf code matches; preserve <=1 decision in owner wrapper.
- `ShouldInitializeOrdinaryAwardWatermark(currentLevel, computedLevel, unspent)` and `DescribeOrdinaryAward(currentLevel,lastKnownLevel,awardedThroughLevel)`: exact ordinary arithmetic/data decisions only, no mutation, checked-policy substitution or authority.

Actual GetPlayerLevel and CheckLevelUp use these bodies while retaining IO order, fallback/catch behavior, initialize-write timing, award writes/UI and _lastKnownLevel update. Do not combine the legacy heuristic with the strict joint calculator. A later strict joint adapter records both named source resolutions and applies already-approved CA preconditions separately.

In `ProgressionScheduleService.WorldTimePolicy.cs`, factor `ResolveParsedWorldTime(JsonElement,int fallback,bool allowIncremental)` returning existing WorldTimeResolutionResult plus a **data-only selected-source tag**. Both actual PS resolver and existing frozen PBasis resolver call it, with their original parser/catch wrappers intact. Tags: DirectNumeric, NestedNumeric, NestedCalendarUnresolved, Incremental, DirectCalendarUnresolved, NoUsableField. Missing/blank/parse-failure tags belong to their read wrappers. Extract an EF parsed helper only if needed to return an effect source tag without parsing twice; actual ReadCanonicalWorldTime still uses that same helper. Tags: NestedNumeric, NestedUnusable, DirectNumeric, NoCanonicalNumericTime. Preserve Int32 progression versus Int64 effect ranges.

No new general resolver merges these named policies. No snapshot/accepted authority is returned by these APIs. Data-only result construction is permissible in unit tests; no such constructor feeds an owning source result.

### B. Genuine HOT source-family operation used by the actual attempt

After the accepted custody implementation, add a private fixed-family source operation in `GameEngine.MortalJointAutomaticPolicy.cs`, invoked by actual CP Capturing **before CreateJointRepairPreparation/prepared/cache/common seal**. It consumes the real attempt's live gate, files/lease/G/original-reader evidence and actual retained original/Ready lineage. It cannot be entered by arbitrary bytes, map, path list, delegate, bool or parsed frame. It does not consume the whole gate permanently itself: it registers one named family result with the same attempt; final S/capture closure still owns gate completion. Reentry returns the retained exact result only while its sources remain current, not another validation run.

Proposed methods/types:
```csharp
private Task<HotAutomaticPolicyObservation> ObserveHotAutomaticPlayerTimeAsync(
    MortalJointPublicationAttempt actualCapturingAttempt);
// private issuer constructor; diagnostic copy is data only
private sealed class HotAutomaticPolicyObservation;
internal sealed record AutomaticPolicyDiagnostics(
    IReadOnlyList<PolicyIssueSegment> Segments,
    AutomaticSourceCoverage Coverage,
    NamedLevelPolicyData Levels,
    NamedWorldTimePolicyData Time,
    JointAutomaticPrerequisiteDisposition JointPrerequisite);
```

The result does NOT implement/convert to S/AcceptedMechanicsInput/CommonAuthority. A dedicated ValidationService instance may invoke pure partial kernels; it must not enter ValidateGameState, WorldReadPhase or an existing ambient spiritual draft under this lease. Do not reuse a broad same-turn override. Source operations are fixed methods for status, experience, core status, soul, characteristics, stat-points, request and world-time; an internal enum/switch may centralize their constant paths but exposes no caller path mapping. Reads use actual explicit-lease snapshot/bytes APIs and the same text decode as ordinary when calling ordinary kernels.

Required role set:
- Current six automatic roots, each exact presence/bytes/read result; independently observed required-field rereads for soul and player_status; existence occurrences where existing bootstrap policy checks existence.
- Current input/turn_request for the actual realm helper and its nested progressionControl; actual original/context agreement remains the enclosing source owner's job.
- Current world/world_time and genuine original world_time if D9 before/after meaning is requested.
- Genuine original soul, characteristics and stat-points when establishing original/client baseline. For that operation use those three paths as actual required selections in PendingTurnSnapshotPathSelection; the other family originals use the existing ordinary optional selection. Do not use CreateWithObservedOptionalPaths for paths outside the closed presence-v1 set. Original experience/status/status_changes observations are retained where covered, but their absence from the signed inventory does not authorize a synthesized absent original.
- Existing current generation bytes and observed original manifest/authority/all context/rollback/selected-source closure via accepted reader; no scalar fallback.
- Actual _lastKnownLevel may be observed directly by the owning GameEngine as HOT in-memory data, with its invocation identity; never as a cold receipt or defaulted from a parsed file. The strict joint path requires existing valid original watermark and therefore does not execute the ordinary <=0 initialization branch. Optional reporting of that branch requires actual computed-characteristics/read history and initializer-owner evidence; otherwise report MissingOwnerHistory, without adding computed reads or running initialization. Ordinary CheckLevelUp keeps its real existing reads. No blanket requirement to capture unneeded computed/relic/effect sources is introduced for the initialized strict joint contour.

Every occurrence has role, operation ordinal, actual owner/G, presence/status, bytes, decoder/parser outcome and issue-slot. Kernels add no UTC sample or GUID; the family has no historical clock authority. No FileInfo/mtime is needed by these named player/level/time leaves; separate freshness requires its real metadata/clock source and remains uncovered. Source failures latch even if ordinary permissive policy would swallow the exception; final physical recheck and genuine original Evidence.RequireCurrent precede family-result closure. An exact repeated read may be stored once but is physically performed at each existing operation; changed-then-restored observations remain a fault. A disposed/foreign lease, replaced G, withdrawn gate, unsupported source or delegated/sealed attempt returns unavailable with no new live family proof. No new native writes occur; ordinary v2 route keeps its existing validation/refusal behavior. This optional family result is diagnostic/retained construction until full semantic integration is reviewed; absence does not break old/helper/cold v2 paths.

### Explicit outcomes, not success-by-empty-issues

Each named policy has ExistingPolicyRan, ExistingPolicySkipped(reason), or PolicyFault; independently source has ClosedCurrent, SourceChanged/Fault, MissingRequiredOriginal, UncoveredInSignedInventory, or MissingOwnerHistory. Issue severity is preserved; warnings do not become errors. Joint prerequisite independently has ReadyForStrictCalculation, NotMaterializedBase, MissingInitializerBaseline, ClientBaselineChanged, StrictInputUnusable, or OtherOwnerRequired.

Example: experience has a nonnumeric nonempty level string. Ordinary experience shape may only warn/not error; threshold may skip; training may fall back; strict joint calculator refuses. Record those exact separate facts, do not add a fake ordinary error or claim full admission. A nonempty legal setCharacteristics command can pass its ordinary command checks but is NotMaterializedBase for the strict calculation until its real owner/materialized source is available. Lawful siblings are retained with coverage obligations; they are not deleted or globally forbidden.

### Original/client-owned baselines and siblings

SS458–512 includes extant rollback/validation files in the real original; absent optional files outside presence-v1 can remain UncoveredInSignedInventory. SS685–704 ClientOwnedValidationHashes covers chat/story, and BP676–699's GM-blame pass is history-only. It does **not** already prove stat-points unmodified. Never cite that pass as an initializer/award witness.

For strict joint baseline, require genuine covered present original stat_points with exact required integer balance/watermark and actual current byte equality before automatic planning in this initialized/no-intervening-client-award contour, including siblings. This is a strict joint client-baseline prerequisite, not a new ordinary validator error. The ordinary/new-incarnation initializer must have completed before original for newly initialized turns; do not move it into this stage. A genuine covered present original with valid D2 state supplies the existing initialized baseline contract. It does not prove a separately claimed historical initializer event: if an eventual policy needs such an event rather than the approved D2 baseline, its actual owner observation remains MissingOwnerHistory until integrated. If actual after-original client operations can legitimately change those values, demand their real typed owner disposition; do not compare against a guessed original or blame the GM for a client write. That branch remains OtherOwnerRequired until integrated.

Current characteristics/experience/player_status/soul may contain legitimate GM/current-owner changes. Do not impose blanket current==original on them merely to simplify admission. Preserve unchanged siblings and pending commands in source images; the complete domain/normalization owners must validate applicable changes before full S. CA's after-images preserve status_changes and stat_points siblings, and base-stat writer emits the canonical twelve fields when applied; this is already specified. There is no award or time inference from output/debug text.

If a required original observation is unavailable, family shape checks can still run but no baseline grant issues. Genuine additional Stage-era owner observation would be a separately specified hot source extension; this family does not rewrite issued originals, add presence-set members, or infer missing old observations from current files.

## D9 adapter boundary: preserve valid mixed time shapes

Return a closed data product containing:
1. Exact validated field-presence bits/root shape, null/non-null command facts and original/current bytes/coverage.
2. Ordered existing time-policy issues/skips.
3. Progression selected-source tag and resolution for the **actual owner-provided** frozen fallback; effect selected-source tag and nullable Int64 CurrentTime.
4. Optional exact incremental Int32 payload (only when the actual validator parsed it), numeric absolute source values, and calendar fields as raw validated data; never synthesize minutes from calendar names.

A fallback is read from the genuine captured request/progression owner evidence, not a caller int accepted as authority. If unavailable, expose source data but mark progression resolution unavailable. P0 lifecycle CurrentTime is the effect policy's value from the genuine first-pass owner; P1 due progression is the actual progression owner's FrozenInputs/control. These are not interchangeable and must not run scheduler/reducer again.

For an unmixed incremental command, named selection can later feed DescribeTime Incremental with real before/after/fallback proof. For an unmixed nested/direct absolute, retain Absolute plus each owner's independently selected value/unresolved outcome. For unchanged physical source, None requires actual byte equality and source provenance, not merely no recognized field. **Do not integrate those adapters in this extraction as an active command grant.**

For mixed direct+incremental or direct+nested sources, existing validators and resolvers already give precise local decisions, so this family can return them completely. Existing DescribeTime's three-value enum cannot by itself express independent command/source decomposition and all P0/P1 aggregate obligations. Keep that actual aggregate adaptation OPEN until the real D9 compositor binds the named decisions to the already executed lifecycle ledger and progression owner. Do not add a new ordinary rejection, suppress a valid field, choose arbitrary precedence, or rerun work to force a match. This is a finite engineering dependency, not a new principled permission, F2 or a whole-F1 narrowing.

## Finite source/IO/branch inventory for this family

| Owner methods | IO / mutations / lifetime | Shared bounded disposition |
|---|---|---|
| PI ValidatePlayerStateFiles, ValidatePlayerFile | Selection, current text; status required-field reread; JsonException boundary | Ordinary orchestration unchanged; fixed-family source counterpart |
| PI ValidatePlayerContract prefix + exact status/stat/set leaf methods | Pure JSON/issues | Shared moved bodies, all other siblings remain separate obligations |
| PI BaseCharacteristics/Experience/EffortTracker; Q/PI primitive helpers | Pure, exact case/order/warning/duplicate behavior | Shared bodies, no strict-parser substitution |
| N ValidateFileFields | Current scalar text, parse, catch+debug | Ordinary wrapper retains reads; HOT fixed reader repeats/latches; shared pure required-field loop |
| BP ValidateRequiredFiles/RequiredFields soul-player portion | Exists soul; soul fields read; soul realm read; optional status exists+fields read; enclosing resource call | Extract only named leaf portion, resource owner remains outside |
| A IsAcceptedTurnMortalRealm, string-property helpers, threshold validator | Request read then conditional soul read then conditional experience read; JsonException skip | Shared parsed kernels + exact ordinary and explicit HOT source wrappers |
| CS LoadBaseStats/GetPlayerLevel/stat-points loaders | Current sources; defaults/catches; strict projection mode differs | Share named parsed data decisions; never use loaders as source proof |
| TL ProcessStatsIncreased/CheckLevelUp | Writes, UI/log, in-memory last-known level; conditional computed/stat-points reads | Preserve ordinary owners; only pure leaf decisions extracted; never run for admission |
| CS Initialize/Add/Mark/Distribute/ComputeAndWrite | Canonical writes and _lastComputed UTC; bonus/effect domain reads | Not invoked; initializer/derived projection separate genuine owners |
| LC flexible world envelope + N world contract time slots + Q time checks | Selected current read, pure envelope/body | Exact wrapper semantics; no broader world graph under lease |
| PS ResolveWorldTimeFromFile / PBasis ResolveFrozenWorldTime | One world-time read vs passed frozen bytes; distinct catch/decode; pure Int32 resolution | Shared parsed resolver, existing actual callers use it |
| EF ReadCanonicalWorldTime | Pure text parse, nested-first nullable Int64 | Reuse unchanged or extract exact parsed helper; not schedule execution |
| ST FormatWorldTime | Presentation-only, distinct flexible number/string parsing | No admission use |
| SS original inventory + observed original reader | Actual original/G/contexts/rollback callbacks/selected original under same lease | Reuse issuer; missing/uncovered original not repaired |
| Existing active automatic calculator | Detached strict six-root parsing/calculation/after-image serialization | Existing approved D2 calculation only; genuine source prerequisites still required |

This inventories the named family methods and their physical leaves; the extracted prefix is intentionally a subset of the whole player contract. It does not claim that calling the whole ValidatePlayerContract, soul domain, Complete Full reasoning, generic world validation or raw resource/item owners is read-only or covered. Seven output roots/73 IO leaves, raw/domain, bootstrap content anchors, full sibling mechanical admission, repair freshness/history and output comparisons remain separate full-S prerequisites.

## Causal TDD and real-consumer acceptance

All cases FUTURE/UNRUN. Use per-test independent mutable roots. No all-suite/category sweep, no changed protected first Late2 bodies, no new production callback. Proposed narrowly owned categories: automatic-player-policy, level-policy-source-order, world-time-policy-selection, hot-automatic-policy-source; exact selectors/counts/budgets finalized in source review. Most are cheap existing ordinary entrypoint/kernel cases; only one or two genuine joint Capturing integrations per ≤5-minute category as measured.

| Case | Actual causal requirement |
|---|---|
| AP01 | Existing selected ValidateGameState player path records ordered complete issues; missing shared kernel is not sole positive. Exercise malformed root, blank, required-field second read and ordinary catch behavior. |
| AP02 | stats duplicate/order/case/whitespace/invalid member; setCharacteristics integer/case; base missing/out-of-range warnings and duplicate integer errors. Compare existing ordinary consumer before/after extraction. |
| AP03 | experience number/string/null/empty fields, generic experienceGained number-only versus specific permissive check, effort multiplicity; no new alias equality or XP formula. |
| AP04 | Actual public level-materialization entry: request realm priority/fallback; threshold equal/below/above; playerLevel-vs-level divergence; invariant decimal strings; source skip/error branches. |
| AP05 | Actual CharacteristicsService training owner, then actual CheckLevelUp path with genuine initialized input: divergent level sources, <=1 fallback, malformed numeric, string level, soul-only, default1. Pure helper controls are data-only. |
| AP06 | Award watermark<=0 computed/unspent heuristic vs valid watermark; _lastKnownLevel suppression; strict joint unavailable instead of init; real original stat-points mutation/unknown original refused as baseline, not fake repaired. No distribution/reward replay from family. |
| AP07 | World-time ordinary selected validation: none/null/zero/negative/fraction, nested calendar-only, malformed nested numeric existing behavior, direct required fields, root arrays/case/metadata/unknown keys, exact mutual-exclusion issue ordering. |
| AP08 | Real PS read consumer and frozen resolver share direct/nested/incremental/calendar branches; afterlife allowIncremental=false remains; int range/overflow/default/catch policy unchanged. |
| AP09 | EF nested-first vs PS direct-first on valid mixed objects; Int32 vs Int64; nested calendar suppresses direct for EF while PS may resolve direct. Mixed shapes stay valid and D9 aggregate status stays open. |
| AP10 | Genuine Stage-original then corrected public stats/level/time submission under actual HOT Capturing: owner source family consumes new shared kernels and records closed named results. Default v2 INACTIVE still refuses active automatic tail; no S/common activation. |
| AP11 | Existing FS hooks cause current body→required-fields reread drift, original/control/G drift, fault swallowed by legacy parser, same bytes restored after observed fault: sticky source failure, no family proof. |
| AP12 | Foreign actual files/lease/original, expired lease, after-prepared/cache seal, repeated invocation; no imported DTO/observer authority, no S/B attachment and no new work/IDs/UTC/tick/XP/write. |
| AP13 | Required original stat-points covered versus custody UncoveredInSignedInventory; legal sibling current change not blanket original equality; incomplete sibling/domain coverage cannot produce full S. |
| AP14 | Ordinary writer outputs/notifications/default exception boundaries remain; accepted CA calculator tests retain three after-images/checked arithmetic/no-init/no-XP policy; family operations produce zero canonical writes. |

Initial RED sequence: (1) source-reviewed ordinary policy regression controls pass on old behavior; (2) causal genuine HOT family assertion fails because the real consuming family operation is absent, after original/Ready/custody prerequisites actually succeed; (3) source drift baseline demonstrates current legacy skip or unbound read does not grant the new closure; implement no successful issuer until final barrier exists. A purely absent-symbol test is secondary, not source authority evidence. Do not borrow custody HS01's pass to claim semantic AP10.

Reuse existing MortalLevelUpMaterializationValidationTests three public entry cases, targeted CharacteristicsServiceTests real source controls, MortalJointAutomaticCalculationTests and MortalJointActionBoundaryTests; select relevant ProgressionScheduleServiceTests including AfterlifeIgnoresStaleMortalTimeChange when resolver changes. These old unit fixtures may author data; they prove policy only, not genuine original authority. For AP10–13 reuse the accepted genuine ordinary Stage/repair and actual attempt seam, not the helper original or a handcrafted original DTO. Categories containing unrelated characteristics relic/scheduler work are not automatically selected wholesale; split exact affected controls as needed and use existing runner/source/evidence gates.

## Sequential implementation and remaining frontier

1. Independently review this bounded contract. Extract shared bodies and ordinary parsed resolvers with causal policy checks; ordinary callers must immediately use them. No policy changes hidden in refactor.
2. Integrate fixed source-family observation into the actual accepted custody attempt while Capturing remains open; genuine original/current/control closure and named diagnostics; full S remains unavailable. Missing engine private APIs are ordinary implementation work.
3. Complete raw/domain/sibling/player soul validation and seven output roots plus real repair freshness/history. Add genuine initializer/client-owner relations where required; do not convert optional absence or _lastKnownLevel into history.
4. Close D9 mixed-source decomposition against real first-pass lifecycle ledger and progression FrozenInputs; no scheduler replay or new tick.
5. Only complete whole semantic proof can issue S before C/B/same candidate seals. HOT sealed intermediate may remain publication-blocked as accepted step5 permits; cold R01–R08/M3 and whole-F1 lawful recovery remain prerequisites for activation.

Internal pure extraction with unchanged ordinary emitted/validated contract has no new GM field/example obligation. Any actual policy/schema/source eligibility change discovered during implementation requires same-change Mortal/afterlife docs/examples/manifests/guards; do not treat this design as approval to tighten permissive branches. No T197 implementation, new QTE, F2, host selection, D4/guided US5/visual/Windows/D8 or public activation claim.


## Static evidence identity

Frozen source `3fdc16e8a954458f1e56bafa42ca24a66c06d020`; local completion HEAD `1a5ec68f90fec0834c460e4519508abaa57017a2`. Inspected production-path committed diff bytes: 0, working diff bytes: 0. Parent test/documentation work is independent; repository status is recorded without attributing it to this author. Exact aliases/ranges/blob/hash evidence is in the paired JSON. No proposed check ran. Prior immutable reports were not changed.

FINALSTOP.
