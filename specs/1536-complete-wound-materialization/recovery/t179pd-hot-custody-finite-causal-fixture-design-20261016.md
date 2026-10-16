# T179-Pd.3b finite causal custody fixture design

DESIGN ONLY; 2026-10-16. Configured Astra/High, backend unobservable. Sole repository writer: root. Production reference: 3fdc16e8a954458f1e56bafa42ca24a66c06d020. Test/reference checkpoint: 9c52f988e8daad0d6061e540b39db1e9bb9fb9b4. Current parent tests/docs may advance. No repository edit, build, discovery, test, application/native runtime, network or delegation. All new cases below are FUTURE_UNRUN. Existing custody and semantic design pairs remain immutable. This is a fixture/source consultation, not HS01 review or production success logic.

## Recommendation and exact cheap fixture

Use the existing genuine QTE-free ordinary Stage prefix at HC238–265: QtePhysicalInjuryConsumer_ComponentPriorTwentyOneOrdinaryEntryDoesNotGrantCompletionAuthority. Reuse ONLY its setup and actual Stage, stopping before the later manifest/presence-map/hash/test-authority mutations. This is a cheap genuine original issuer, not an imported helper original. No prior QTE is required for origin/Ready custody alone.

Proposed test-only Stage/Wait helper, within GameEngineTurnLifecycleTests:

1. Own a fresh fixture root and CleanupOwnedFixture per case. Copy TestRepoPaths.BaseSessionRoot. Author Characteristics.All at20 and experience {level:1,experience:0,experienceForNextLevel:100}. Call actual ResourceBootstrapStateBuilder.BuildMortalPlayer(2,42,20,20,20,20,20); require valid; publish its definitions/state/history. Use the actual CanonicalResourceOwnerAuthorityComposer.ComposeAsync(...,FinalAfterImage), require valid, publish its result. This is public pre-original seed setup, not a treatment/custody issuer.
2. Construct real CreateGameEngine(...,fileSystem:files) with InertRealmSettings and AutosaveIntervalTurns=0. GameLoop.SetSession(fixtureSession,42); StateManager.RefreshGameStateAsync. When hooks need a custom files instance, seed and run the SAME actual instance; do not mix _fs and files authority identities.
3. Under SessionOperationContext.RunParticipatingCurrentSessionAsync(files,...), invoke actual StagePlayerTurnAsync("Спокойно осмотреться.",null,null). Retain the returned actual PlayerTurnPreparation.Request/Backup/Generation/SnapshotContext through read-only reflection; PC153–175 already uses that returned-object inspection. No manually constructed preparation, original, backup or context. Require non-null, exact physical request tuple, readable genuine original and detached authority. Original optional status_changes may be UncoveredInSignedInventory, not signed absence.
4. Keep the console Stage and Wait in the same participating operation, matching ProcessPlayerTurn TL1492–1505. Launch the GM task before Wait; it waits for the real validation_repair_request, reads the actual tuple, authors public corrected current/output bytes AFTER original and writes matching validation_repair_ready LAST. Original manifest, authority and snapshot bytes remain untouched.
5. Invoke actual WaitForContractRepairAsync("обработки хода", errors, 2, actualPreparation.Backup, actualPreparation.Generation), with no fabricated retry obligations. GF1438–1550 provides the existing GM task/Ready/ledger orchestration, but replace its handmade-original setup with steps1–3. Its synthetic location packet is not required for a simple custody control. A directly authored ValidationIssue is only input to the real repair owner, not full validation evidence; preferably use a genuine ordinary validator issue from the corrected public file, with that selected branch source-reviewed.
6. Assert Wait true, Ready deleted, original/request unchanged, exact real tuple, and ledger acceptanceScope=correlated_repair_ready/fullCanonicalStateAccepted=false. This is source/custody control only; it never demonstrates changed-stat semantic admission or successful gameplay publication.

Existing GF10914–10997 constructs the real StateManager/validator/services. No fake empty response, upstreamAccepted bool, private issuer, registry field seeding, production callback or altered original is needed. Do not call Stage again to recover missing hot lineage.

## Exact post-Wait diagnostic DATA refinement

Accepted3b allows detached diagnostics, but its publication-attempt export cannot be the cheap oracle: Stage→Wait alone has no joint attempt. Use this private naming/observation refinement:

private HotOrdinarySubmissionDiagnostic? ExportHotOrdinarySubmissionDiagnostic()

It projects the actual retained original/Ready owner or its retained last detached observation. Fields:
- generation, sessionId, requestId, snapshotToken, turnNumber from the actual issuer;
- actualStageCompleted;
- custody status and reason, including Unavailable / SourceChanged;
- readyObservedBeforeDelete;
- deleteReturned and releaseReturned.

The last three flags are positive observations at their real event returns. False/unobserved never means proven no attempt, nonconsumption, successful abort or publication safety. SourceChanged remains sticky even if default deletion/release subsequently succeeds. Exporting absent retained origin returns no origin diagnostic; it cannot mint lineage from physical files.

Export is side-effect-free DATA: no filesystem reread, lease/capture, clock/ID allocation, issuer call, reservation, S/B/M3 or capability. Deep-copy arrays/collections if included. It can share detached projection with the later attempt diagnostic. Fixture calls this private export read-only after the actual owner returns and lease closes; no production test callback. Read-only reflection over retained owner data is another possible seam, but this named projection avoids tests depending on private storage layout.

Root's proposed target ordering is sound: first require legacy Wait true + exact one causal cut + Ready absent + genuine original/request unchanged; then require this diagnostic and Unavailable/SourceChanged. Missing export is the explicit initial RED. Corresponding unchanged and replaced legacy controls can pass without the new export. An unchanged positive custody target must also require real returned capture/consumption, preventing an implementation that merely always reports unavailable.

## Concrete causal RED2 and existing cuts

### Ready replacement after Lr, before Lc

Production VR3460 reads Ready, parses it, then awaits ResolveActivePendingTurnSnapshotContextAsync. That resolver reads the manifest (SS888–905). The actual late replacement schedule is:

- Arm after the GM has published Ready.
- Existing AfterCanonicalReadInitialValidationAsync on that Ready records its physical open only.
- On the subsequent original-manifest physical read, replace the EARLIER Ready once with bytes containing a different requestId, leaving original/context/request unchanged.
- Record cut count, observed path ordering, first Ready bytes, replacement bytes and actual physical replacement before deletion.
- The old owner already retains the first Ready; it matches that value against unchanged original context, appends the actual old-tuple accepted ledger and deletes the replacement, returning true (VR3460–3567). Poll-time manifest reads before Ready do not trigger the cut.
- New custody must reject exact Lr/Lc disagreement as SourceChanged. Its optional channel must not change the independently valid old Wait return.

The hook is AFTER open but BEFORE bytes are copied (FR90–136). Mutating the same file inside its own initial-validation hook is not post-capture drift. Mutating earlier A only when later B opens is the required causal boundary. Assert the actual sequence rather than a global nth-read number.

This is a real source-drift intervention, but old Wait true is NOT an unsafe custody authority. It expressly means correlated_repair_ready. Therefore:
1. author and qualify the genuine unchanged and drift legacy controls;
2. author the targeted diagnostic oracle before production success logic, explicitly missing-owner RED while the export is absent;
3. implement complete safe capture/recheck/close together, never a temporary success issuer before barrier;
4. qualify unchanged custody success and drift refusal against the same actual new owner.

Do not introduce a fallback translating Wait true into successful custody. The earlier observed-reader fallback analogy does not apply: old ReadCurrent actually issued original authority; Wait never issued HOT custody. An independent failing unsafe-custody-authority assertion cannot honestly be obtained from this absent API. This limitation does not remove the drift obligation: the causal stimulus is established separately, and the new barrier target must pass only with genuine owner diagnostics. No broad implementation/activation release is implied.

### Further finite schedules

| Cut | Existing genuine seam and exact limit |
| --- | --- |
| Lc current bytes/presence/repeated reads | Alter already-captured role A when later role B opens, using existing BeforeCanonicalReadOpenAsync/AfterCanonicalReadInitialValidationAsync on the same files. Confirm implemented role order first. For changed-then-restored, leave A changed through an actual repeated observation, restore later, require sticky failure. Undetected change restored before every read is outside the stated model. |
| Lc original/rollback/context | Derive physical coordinates from actual original, use the genuine observed reader. OR13–99 demonstrates rollback A changed when later selected snapshot B opens. Add only the new consumer refusal assertion; do not repeat accepted131 or substitute Snapshot without Evidence. |
| Lc→Ld | Select actual accepted-trajectory ledger native publication between capture and deletion; alter Ready/current A once. No new production hook. Default trajectory remains correlated-only. In old source this proves the cut after match, not existence of Lc. |
| Ready delete publication uncertainty | Attach existing CleanupPublicationCut to same files; select actual journal member by physical Ready path AND deletion after-image, armed after accepted-trajectory publication to avoid initial cleanup/write. CUT64–94 produces genuine unresolved publication by faulting actual member publication; assert real retained debt and no Consumed custody. Never manufacture a CSP exception. |
| G replacement | Genuine lifecycle lease + replacement lease + RotateSessionGeneration (GF582–620; SG75–90), after old participation/leases ended. Then old-G Wait must fail without dispatch/rollback. Do not raw-write G or attempt nested replacement from a held read hook. Mid-repair replacement needs a real released-owner interval. |
| Fault/cancel/retry | Optional-capture ordinary IOException means unavailable, not successful absence. Legacy Lr exception retains its own old propagation. Missing Ready uses finite Escape cancellation. Malformed/mismatched Ready: observe actual rejection/deletion before publishing next valid Ready, assert no accepted custody from rejected input. Await all GM/input/cleanup tasks. |

## Isolated Ld release failure: specific OPEN frontier

VR8336–8354 deletes with a genuine lease and calls ReleaseOwnedLeaseAsync(...,completed:false,uncertainty). RW103–122 propagates a close failure when no primary operation failure exists; with genuine publication uncertainty it attaches the secondary close failure.

FS324–370 disposes external context, physical stream/directory, synchronous MainAdmission, worker root pin, then ambient registration. FS hooks expose no per-lease close fault. MF189–250 shows borrowed MainAdmission.Dispose is synchronous reference release; remote IPC disposal is in the outer owning DisposeAsync. Disconnecting IPC may fail validation/outer completion, but is not by itself an isolated Ld close exception. Pending native publication is a publication failure, not successful delete followed by failed release. BrowserLocalStorageAccess.Dispose only clears its actual registration (BL24–28). ExternalPublicationContext producers are browser local transaction/Explorer rollback, not this plain Ready deletion owner.

GC's existing OriginalEngineRepairCleanupOwnersRetainGenuineUncertaintyOnClose obtains the real suspended lease but then assigns ThrowingClose to ExternalPublicationContext (GC70–104). That field injection violates this consultation's stated no-field-seeding fixture constraint. It is not silently reusable. No supported deterministic native/IPC/FS-hook seam for isolated successful-Ready-delete/failed-Ld-release was established in this bounded trace. Keep that HS12 runtime assertion OPEN. Existing release regressions and source ordering are supporting evidence, not new custody integration qualification; do not relabel SessionOperationClosingAsync as Ld close. The production contract still sets consumption only after release returns.

## Cheap coverage versus genuinely joint coverage

- HS03: genuine Stage engine A, then engine B over the same physical source without copying private owner; HOT unavailable, existing v2 unaffected. BI468–493 directly creates a genuine browser original with a manually assembled request, without Stage: suitable missing-Stage negative, never HOT-positive.
- HS04: real QueueBrowserInputAsync (BI457–466) → actual Stage(binding.Action,null,binding); Stage owns BeginBrowserOriginalOperation/Claim. End its actual scope; later use real RunParticipatingExpectedSessionAsync and a NEW BeginBrowserOriginalOperation(actualStaged.Binding,actualStaged), matching TL1532–1553/BW12–22. Do not preclaim or retain disposed scope. Cheap direct Wait covers custody ownership only; actual ProcessPlayerTurn/terminal transitions and remote Main/bridge require their own existing full-owner fixture (BC). They remain separate until authored/qualified.
- HS05/07/08/12/14: cheap Stage/Wait handles grammar, cancellation, source drift, native deletion fault and current byte shapes. Current absent/empty/JSON-null/malformed/BOM stay distinct custody data. Do not rewrite signed originals to create positive absence; uncovered optional source remains uncovered. No semantic validation claim.
- HS13: mutate detached diagnostic copy then export again; actual retained data unchanged. No live authority mutation. Expired live evidence/current-lease checks remain genuine-reader/joint-consumer obligations.
- HS06: actual engine VR7867–7903 constructs real validator-backed ApplyGate, pool and delegator. GT427–488 is an actual Apply-accepted/Ready-failed component example; VR3355–3359 returns true on worker-direct. Reuse public profile/proposal protocol, not its canned tuple, default empty validation delegate, or hardcoded pwsh.exe as a Linux claim. Engine-compatible fault timing must select worker Ready WRITE after Apply, not initial cleanup/native uncertainty; GD's precreated Ready-directory trick cannot precede genuine Stage. Delegator BeforeReadyPublicationAsync exists but the actual engine does not inject it. Engine worker-direct/generated-Ready fixture adaptation remains a finite OPEN item; both legacy successes must stay supported and new HOT unavailable.
- HS02/09–11 and canonical HS12/16 require genuine RunJointCompletionControlAsync (JC24+) with actual QTE42→Stage43→repair/registry/native path. Cheap custody is not a replacement. NoNativeAttempt after prepared/cache sealing cannot reopen Capturing or attach S/B to v2.

Use separate finite categories for cheap Stage/Ready controls/drift, additional browser/worker ownership and measured full-joint cases. Do not put fifteen full-QTE cases in one five-minute owner. Parent reports ~216s/two Late2 cases; this consultation did not measure it. Protect unchanged firstLate2 bodies and accepted131. Final selector/count/budget/cleanup review belongs to authored source, catalog and SOURCE/FIXTURE/SELECTION; all new actuals remain UNRUN.

Immediate bounded next work is concrete: author cheap unchanged/drift legacy controls and the named detached diagnostic target; source-review their genuine Stage, actual one-cut ordering and missing-export RED; then proceed through the existing independent gates. Ld-release, worker and full browser/joint subcases are not claimed closed by this fixture.

No S, automatic semantic admission, C/B retrofit, cold/P0/M3, full F1, T197, D4, guided/visual/Windows/D8 or public QTE/activation release. No replay, extra tick/draw/reward/identity allocation follows from these diagnostics.

FINALSTOP.
