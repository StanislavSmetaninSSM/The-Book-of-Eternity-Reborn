# T041-LOAD-SESSION-LIFECYCLE — authorized execution plan

Issue: [#1553](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/1553).
Branch: `codex/1553-load-filesystem`. Accepted base: `dc62a88a630ba20eacd65f7bdab138ba129123ff`.
Status: implementation WIP after independent actual Sol6.1/xhigh design PASS. Runtime source review PASS at8aa96165; final qualification/evidence review and GitHub restoration remain pending.

## Contract and source delta

The user accepted stop → confirmed Load → required refresh → fresh configured GM on 2026-10-06 21:11 UTC, then authorized this separate slice after bounded browser rollback. A failed or ambiguous stop/load/refresh/restart never resumes the old run or repeats an unknown command. Storage disposition and main-session disposition remain separate: Committed remains Committed when later refresh or fresh launch fails. This is client-owned lifecycle UX, with no new GM-authored game fields/mechanics; Mortal World/afterlife prompts and examples need no change.

Reuse accepted M1/F1–F3/T042 and T031 proof. Inspect changed interaction boundaries only:

| Boundary | Existing source and required delta |
| --- | --- |
| Original main stop/start | `BookOfEternityGMBridge/Program.cs`: actual `StopShellCoreAsync` revokes InputLifetime, closes/drains pins, scopes stop, joins I/O/disposal and acknowledges Stopped. `StartShellAsync` consumes the production profile/package/root admission. Add a connection-owned Load operation to the existing accept loop; do not create another launcher. |
| Durable run/generation | `Services/GmRuntime/GmSessionRunCoordinator.cs`: schema1 Prepared → held creation → Running → single release. Add expected-generation refusal under the actual launch lease before Prepared, retaining existing Running/release rechecks. No second journal. |
| Console | `Core/GameEngine/GameEngine.MainMenu.cs`: existing selected Load/rebind and required settings/normalize/response/validation refresh. Wrap the actual `LoadGameFlow` path in the new lifecycle, retain those storage methods and typed decisions. |
| Browser backend | `WebUi/LocalWebUiMainMenuService.cs` and `LocalWebUiHost.cs`: both UI Load inputs share `/api/saves/load`. Retain `BrowserLocalWriteCoordinator.ExecuteSessionReplacementAsync`, original UI token, held leases and full `BrowserLoadStateService` bundle. |
| Browser application | `GameLauncher.tsx`, `SettingsView.tsx`, `loadPersistenceNotice.ts`, `ShellContext.tsx`: synchronous shared owner already rejects repeated clicks and stale refresh/navigation. Add immutable operation ID, explicit complete/cancel acknowledgement and fresh-session result before navigation. |

## One connected operation

1. Reserve one live Load operation for the canonical root before effects. Freeze operation ID, selected source/save ID and refusal-only expected generation. A second request or reuse of an ID refuses; it cannot re-execute Load. A persistent bridge connection binds the operation to the original owner/run/input. Record/status/JSON identifies expectations and discovers the pipe; it cannot mint original authority.
2. With no client filesystem lease held, the bridge seals/revokes original input and performs the existing exact stop/disposal/Stopped ACK. Stop failure or lost receipt blocks Load. Hold only a non-filesystem operation reservation while IPC/stop waits occur. Manual restart cannot bypass an active Load operation. Manual cancel is an exact live-operation revocation, never a stale stop of a later run.
3. After confirmed stop, acquire original quiescent main admission before recovery/lifecycle/canonical/UI locks. Check the frozen generation at this admission, preserve independent worker/storage conjunction and the original UI replacement guard. Execute the actual typed Load, release its held mutation leases and refresh under the same original quiescent admission. No IPC/stop occurs while these filesystem leases are held.
4. Console completes its existing required refresh within that scope. Browser prepares the complete established-generation bundle and returns the retained typed storage outcome. Its operation-owning task keeps the original quiescent scope through application acknowledgement; the separate HTTP complete/cancel handler only signals that original task, never reconstructs its scope. The supplied bundle avoids another backend refresh call while the original guard is retained. Bound waiting; stale/unmounted/cancelled UI or missing reply never acknowledges refresh.
5. Release quiescent admission before fresh-launch IPC. Only Committed, required refresh confirmed, still-current original operation and matching installed generation authorize one finish frame on the retained connection. Fresh launch uses the existing configured production path/profile verbatim; command/model/args/cwd are not replaced with executor settings. Recheck the expected installed generation inside actual launch admission before Prepared/creation, then retain existing checks before single release.
6. Report confirmed Linux Running only from the original successful coordinator/release receipt. Legacy Windows receipt reports `StartedNotReady`: it proves only the existing shell/bootstrap result, not schema1 Running or CLI readiness (`CliProcessId` may be null). A lost stop/load/restart response is not replayable; retain logical uncertainty/owner evidence as applicable. NotLoaded/RolledBack/Uncertain never freshly launch. Cancellation before the restart decision prevents launch. Cancellation racing after that decision reports the actual independently committed launch outcome or uncertainty; it cannot reuse the old operation to stop a later epoch. UI still blocks continuation when its ownership is lost.

There is no automatic missing-transport bootstrap. With no active original GM and an absent/valid Stopped main record, perform the existing quiescent Load + mandatory refresh, report `NoActiveSession`, and leave the GM absent. Cold nonterminal/unknown records and unreachable or ambiguous live owner refuse. An advertised bridge endpoint is contacted before deciding absence; absence of schema1 alone cannot classify a reachable original Windows ConPTY `_pty`/Job as no active GM. The retained connection must bind and stop that actual original terminal/input as well. Legacy Windows creation additionally checks the installed generation under original quiescent admission before creation; preserve ConPTY/Job source behavior without claiming the still-unqualified durable Windows fence or native execution. Add a targeted protocol/source case for this distinction. This preserves usable no-GM Load without inventing an active session.

No filesystem locks span bridge stop/restart IPC. The original owner lifecycle already spans its own held creation/retirement, as accepted in F1; do not remove that ownership. Client Load quiescent guard spans its own acquisition/release/refresh, including original UI lease closing; release it before launch IPC. No automatic retry, salvage/reboot clear, live provider, model request, real save, systemd setup, Q1/Q2, public rollout or cold exactly-once promise.

## Small implementation sequence

1. Publish/review this plan. Add compile-ready causal tests against current actual console/browser refusal and both frontend handlers; preparation failures are separate from behavioral RED.
2. Implement bounded original Load connection/reservation and expected-generation launch guard. Integrate actual console storage/rebind/refresh path, then browser operation-owning task + supplied bundle/ack/cancel and both actual handlers. Publish small WIP checkpoints with exact unrun/failed checks.
3. Qualify actual configured persistent neutral CLI through installed M1 production route in isolated game roots under independent guardian. Prove old root/input revoked, confirmed stop/disposal/Stopped, exact archive bytes/generation, complete refresh, one fresh epoch/process and unchanged configured command/model/cwd. No neutralPackage bypass or model request.
4. Qualify failure/interruption matrix in small sets; repair only causal findings. Independent actual Sol6.1/xhigh source and evidence/selection review; exact remote/readback and fresh GitHub-only restore; handoff and stop before a new slice.

## Narrow verification matrix

Proposed new category `gm-load-session-lifecycle`: real bridge pipe accept loop + console/browser production-neutral entrypoints, stop/receipt loss, original pin drain, late T042 revocation, duplicate/cancel, failed fresh launch and installed-generation changes. Split by measured runtime if needed, not by broad platform audit. Proposed `browser-load-session-lifecycle`: actual frontend GameLauncher/Settings handlers plus protocol/result validation. Add portable backend focused cases to an explicitly owned category if they require no controlled process. Use only `scripts/test-csharp.ps1 -Category <selected IDs>` and exact affected frontend files through the runner; retain source-pinned evidence, expected/completed counts and guardian ECHILD.

- Positive: console and each browser entrypoint stop → actual Committed → required complete refresh → one fresh configured neutral CLI; two inputs prove a new persistent run, not a replayed prior command.
- Storage: NotLoaded, RolledBack, Uncertain, committed cleanup debt and failed refresh retain exact typed outcome; no fresh launch. No-active path refreshes without creating a transport.
- Ownership: repeat click/request, stale callback/UI generation, same-mount navigation/unmount, manual cancel before load and during refresh, stop/load/restart reply loss, generation replacement before fresh launch; actual competing `restartcli`/`restartshell` while reservation survives stop and filesystem-guard release must refuse; never false Running/replay/second mint.
- Order: no mutation before stop receipt; no held canonical/replacement/quiescent leases during stop/restart IPC; original guard present through Load/release/refresh; independent unresolved worker/storage refuses; lost logical authority remains Uncertain even after guardian cleanup.
- Affected regressions selected from changed shared coordinator/pin/input/load bundle/helper contracts only. Reuse accepted cohorts as evidence; do not rerun F1–F3/M1/T031 wholesale. Historical four F2 Windows-only IDs remain unpassed; native Windows/systemd not qualified.

## Spec Kit and checkpoint notes

This is an additive task within accepted #1553; do not run setup-plan over its existing plan or initialize another feature/branch. Constitution, existing spec/contracts/worker policy/trusted-local player contract remain governing. Optional `speckit.git.commit` before/after-plan hooks are satisfied by the explicit ordinary checkpoints; optional agent-context refresh is unnecessary because the existing #1553 pointer remains valid. No new configuration or install.

Direct-gacha and standalone Daren Linux tasks remain open and separate. Pending debt for systemd-user, native Windows, real CLI Q1/Q2 and live gameplay remains unchanged.

## Execution checkpoint — first causal RED

Independent actual Sol6.1/xhigh PLAN PASS on `c91adc00267e131d0f0dc7676e5d222ccd2cfa0f` after focused Windows/restart amendments. Runtime remains accepted base. Clean tested source `ed8aafb4e585affc0b9d06edd65f3ad2f0f3a5a5`: actual GameLauncher/Settings handlers2/2FAIL (navigate before pending fresh receipt), actual installed production bridge → browser Load1/1FAIL (Running refusal instead of stop/replacement). Guardian ECHILD true, emergency0/failures0/deadlinefalse, actual original scoped retirement confirmed. Combined runner stopped after frontend failure: native case was separately executed with NoBuild after that exact fresh successful unit/dependency build.

Three earlier zero-execution preparation failures (unsupported ResultsDirectory; selector files vs tests; frontend-relative vs repository-relative path) are not causal RED. Evidence: [frontend](recovery/evidence/load-ux-first-red-frontend/manifest.json), [native](recovery/evidence/load-ux-first-red-native/manifest.json). No provider/model requests. Next: minimal original connection/reservation + launch generation guard and actual consuming lifecycle.

### Connected browser/bridge WIP

Original retained Load connection/reservation added to the actual pipe accept loop, exact terminal stop, expected-generation production launch before Prepared and source-preserved legacy ConPTY mode. Browser operation-owning task keeps its original guard/full bundle until application acknowledgement, releases it before fresh IPC; both real handlers send one immutable operation and await the fresh receipt. Existing storage disposition survives follow-up failure. These source changes are **unbuilt/unrun/unreviewed WIP**; console integration and interruption matrix are still pending. The first causal frontend fixture now supplies the full bundle and captures its real request ID rather than a fabricated constant. No native Windows or overall completion claim.

### First bounded GREEN and console RED preparation

At runtime WIP `4a5397db91e490313b000d146502eff193d63980`, first actual browser1/1PASS proves original stop before Committed (without full bundle it correctly stays blocked/no fresh GM); both frontend pending-receipt2/2PASS. Own original retirement and guardian ECHILD/0 emergency confirmed; own fixture removed after capture. Typecheck found one nullable-menu compile failure; corrected and awaits verification. Added a separately selected real console entrypoint oracle against still-unmodified console Load code; it must produce causal Running-refusal RED before console integration. These are partial proofs, not complete lifecycle acceptance.

### Console evidence preparation correction

At `bf802ff8` the real console consumer ran, but the scenario writer attempted to JSON-serialize Exception.TargetSite and aborted134 before writing its result. TRX1FAIL is an evidence-preparation failure, not a verified causal RED; guardian ECHILD/emergency0/failures0/deadlinefalse. Prior commentary prematurely called this causal and is corrected here. New uncommitted console runtime wrapper was removed; console remains unchanged until the corrected oracle proves its actual assertion. Preserve anonymous safe exception text in the fixture. Factory namespace preparation failure at4c5431ad executed0 is separate; frontend typecheck2/2PASS at4c5431ad.

### Verified console causal RED → connected wrapper WIP

Clean tested `6f28d36134c665b787d985e404fe06c4aa7f16a9` corrected oracle1/1FAIL: actual console selected Load returned NotLoaded with original owner unavailable and never stopped its Running GM. Safe scenario captures the exact assertion; original retirement and guardian ECHILD/0 emergency/failures/deadline confirmed. [Evidence](recovery/evidence/load-ux-console-causal-red/manifest.json). The console flow now wraps unchanged actual storage/rebind/mandatory refresh under one original quiescent guard and sends a single finish after closing that scope. Added explicit current canonical/bound/main-admission refusal before Load IPC, preventing nested caller locks. WIP, unbuilt/unrun: next positive browser HTTP full-bundle/fresh epoch and console required-refresh qualification, then interruption sets.

### Connected positive set —2PASS/1FAIL at d69e4404

Real browser basic and HTTP full-bundle/current ACK/fresh epoch passed2/2, including duplicate Load/manual restart/stale cancel refusal and two fresh neutral inputs with Unicode in one process; profile command/model/args/cwd preserved. Console storage committed but required refresh reached validation error display and attempted a real redirected Console.ReadKey in the fixture, therefore failed/no new GM (1/1FAIL). Preserve failure pending diagnosis; do not claim console fresh qualification. All3 guardians ECHILD/0 emergency/failures/deadline; own fixtures removed after capture. [Evidence](recovery/evidence/load-ux-connected-positive-first/manifest.json). Fixture now uses inert real IConsoleInputSource and records actual validation errors plus safe exception text; runtime validator is unchanged. Next run only the failing console case.

### Console isolated archive diagnosis

At clean ebdf6b99 the console case1FAIL confirms Committed but correctly refuses refresh due nine missing required fixture artifacts (Mortal item index/player status/lore). Inert input and logger produced exact codes; no runtime validator changes. Guardian ECHILD/0emergency/failures/deadline. [Evidence](recovery/evidence/load-ux-console-fixture-validation/manifest.json). Complete only the positive console fixture archive and rerun that exact case.

### Independent source-review causal gaps

Pinned ebdf independent Sol identified premature null-generation completion before full bundle, malformed fresh receipt navigation and cleanup-debt launch mismatch. Added actual HTTP early-ACK oracle and real frontend malformed/lost receipt oracles before fixes. Console fixture schema refinements affect only isolated archive, not runtime. New causal tests pending; no completion claim.

### Review gaps causal RED and minimal fixes

At8d71b38a actual early ACK1FAIL authorized fresh launch before bundle application; both handlers4FAIL/2PASS proved malformed receipt navigation and lost reply message defects. Guardians clean. Added explicit AwaitingApplication + required nonempty matching generation and normalized fresh identity/notice validation. Added next cleanup-debt and explicit-receipt-field oracles before corresponding fixes. Console isolated archive now matches existing accepted achievement/codex seed contract rather than weakening validator; positive console remains pending.

### Explicit receipt and cleanup-debt fixture correction

Clean33c6b18e frontend6/6PASS after reviewed fixes; explicit stop/completion receipt fields3/3FAIL before JsonRequired correction. Console debt oracle at33c6b18e did **not inject debt** (hook applies failed preparation only), so its1FAIL is preparation failure, not causal cleanup RED. It incidentally proves valid console refresh/fresh execution but is not positive-case verdict. Correct fault injection adds an owned staging link only after Committed; real candidate disposal must report debt before runtime admission changes. Guardian clean.

### Cleanup debt causal RED → admission fix; bounded interruption groups

At9cb3481c debt injection reached1: actual candidate disposal returned Committed/NeedsFollowUp/nonblocked and console launched fresh;1FAIL causal. Correct only fresh authorization: NeedsFollowUp blocks a new GM while preserving storage decision. Added four-case controlled HTTP stop/refresh, restart/cancel/response-loss, and storage/admission groups; null receipt observer closes only the original fixture stream. Broader frontend original-owner/fault tests added; all new work pending build/run/review. Positive category remains three cases, not a growing full matrix.

### Fault-matrix preparation and stop message correction

Atb6f5849a native build executed0 due fixture bool/task name collision; fixed only fixture name. Actual frontend18 executed/16PASS/2FAIL: failed original stop lacked a specific GM message. Added typed main-state copy without unsafe diagnostic paths, plus focused ambient bound/main scope IPC refusal oracles. All changes remain WIP pending fresh selected build.

### Reviewed positive/handler GREEN; exact fault preparation and profile contract

Cleanff500224 native/portable10/10PASS and actual frontend handler18/18PASS. All5 guardians ECHILD/0emergency/failures/deadline; own fixtures removed after capture. Sol focused source review: runtime fixes PASS; corrected exact fault-name classification, actual Prepared worker debt witness outside swallowed hook, and duplicated catalog selector before fault execution. Exact affected regression selection is accepted and now recorded. Fresh uses **installed archive configuration**, as existing SaveLoad/ApplyLoadedValues already specify: selected config/model/command/args/cwd are consumed verbatim by M1, never replaced by executor settings or a neutral API. It does not preserve a contradictory pre-Load config. Added isolated neutral archived-vs-active profile sentinels to qualify this established contract; no runtime profile change.

### Stop/refresh faults and affected frontend GREEN

CLEANecd14793 stop/receipt/refresh/manualcancel4/4PASS; logical original Uncertain retained even with independent guardian cleanup. All4 guardians clean. Exact affected frontend62 plus two typecheck receipts64/64PASS. Console finish now reports its confirmed storage Committed independently of fresh permission (debt/refresh still send false RefreshConfirmed); server stop/launch behavior unchanged. Recheck only two actual console cases for this narrow truthfulness correction along with remaining new restart/storage/profile groups.

### Restart set and final source review

At clean8aa96165 six executed: console positive/debt2PASS; restart faults3PASS (lost restart receipt, generation race, post-decision cancel); lost Load reply1FAIL on fixture timeout awaiting original execution. All6 guardians reached ECHILD/0emergency/failures/deadline; captured and removed only own roots. [Evidence](recovery/evidence/load-ux-restart-faults-first/manifest.json). Local HttpClient cancellation was not yet proof that server RequestAborted was observed, so this timeout is not claimed as causal runtime RED. Replaced only that fixture transport with its own raw HTTP connection/reset and an explicit real RequestAborted observation; split the exact failed case to avoid repeating unchanged successful faults. Independent Sol6.1/xhigh final source review8aa96165 PASS, including installed-profile contract and test selection. Per reviewer, archived-vs-active profile fixture now additionally checks child-reported actual argv/cwd, pending execution. No runtime changes in this checkpoint.

### Installed child profile PASS; actual lost Load interruption diagnosis

At5671d19c profile1PASS proves both actual active/fresh child argv/cwd. Exact lost Load1FAIL despite observed server RequestAborted:true; original Execution still timed out after refresh release. Planned storage4 not executed (runner fail-fast). Both guardians clean/removed after capture. [Evidence](recovery/evidence/load-ux-profile-green-lost-load-timeout/manifest.json). Add only own operation-phase diagnostic fields before/at timeout, then rerun exact lost case to distinguish cancellation binding from guard/finish latency. No guessed timeout increase or runtime fix. Narrow fixture source review5671 PASS.

### Lost-response phase isolated

Clean63ac56ae exact lost1FAIL: real RequestAborted; original Cancelled:true, Applied:RanToCompletion, AwaitingApplication:false, Restarting:false, Retained:null unchanged after5s. This excludes missing cancellation/30s ACK/finish IPC; pending work is core required bundle or its admission close after the controlled hook. Guardian clean/removed. [Evidence](recovery/evidence/load-ux-lost-load-phase-red/manifest.json). Add only hook-return and existing canonical/open/contention/closing hook witnesses, with abort observer restricted to the exact Load route. Next exact diagnosis plus previously unrun storage4; no runtime fix or timeout change yet.

### Independent storage admission PASS and preparation correction

At1ffda2e8 four executed: no-active and real Prepared worker debt2PASS; rollback/uncertain2FAIL **before HTTP Load** because fixture attempted an ordinary canonical write while original GM was Running. Correct fence refusal, not causal Load RED. All4 guardians clean/removed after capture. [Evidence](recovery/evidence/load-ux-storage-admission-first/manifest.json). Move only that marker preparation to own quiescent root after archive creation, before terminal launch. Separate the two cut cases from unchanged passed admission cases. Lost-response diagnosis adds existing main-borrow/contention/read witnesses; hook-return witness uses Interlocked rather than concurrent dictionary mutation, as reviewer requested. Runtime remains unchanged.

### Bundle progress and real storage cut qualification

At03ee6d95 lost1FAIL after5s: hook returned, cancellation applied; whole Load counters541opens/554borrows, no main/canonical contention, pending core bundle. These counters include pre-hook work and do not establish post-release timing. Independent Sol approves only using the **existing HTTP timeout20s** for fixture Execution wait, with runtime/runner/guardian budgets unchanged; post-release counters and elapsed completion now retained. This remains unqualified until the final Committed/blocked/no-new-terminal oracle passes. [Evidence](recovery/evidence/load-ux-lost-load-bundle-progress/manifest.json). Separate same-source storage2 executed: actual Uncertain1PASS; rollback1FAIL only exact injection-count oracle, after actual typed RolledBack and exact restored generation/world/no fresh assertions passed. Observer was eligible again during rollback recovery; bound it to one intentional cut, add exact count witness, and rerun only rollback. All3 guardians clean/removed. [Storage evidence](recovery/evidence/load-ux-storage-cuts-first/manifest.json). No runtime changes.

### Lost Load reply GREEN; exact rollback fault classification

At555711c1 lost1PASS: actual RequestAborted, original Committed retained/ContinuationBlocked/no fresh terminal; refresh completed7727ms after release,847post-release canonical opens/860borrows/1closing/no lock contention. The former5s fixture oracle was too short, not a runtime hang; existing HTTP20s bound passed with original30s guardian unchanged. Single rollback cut1FAIL because it returned **Committed**: source FileSystemManager.LoadNamespace.cs:245–255 retries only a confirmed RolledBack transient IOException; one-shot transient fault then permits a safe revalidated commit. The earlier claim “fault again during rollback recovery” was an unverified interpretation and is corrected here: repeated callbacks were separate known-rollback transient attempts. Use a nontransient InvalidOperationException (as accepted PortableBrowserLoadTests already do) for the intended terminal RolledBack oracle; no change to accepted storage retry policy and no unknown GM command replay. Actual conflicting-marker Uncertain remains qualified. Both guardians clean/removed. [Evidence](recovery/evidence/load-ux-lost-load-green-rollback-transient/manifest.json). Corrected stale category responsibility per Sol; pending exact rollback1 and27affected backend cases. Runtime stays8aa96165.

### Actual ordinary no-GM consumer causal RED — schema expectation

Clean55ac46ac selected affected integration15 executed:8PASS/7FAIL; subsequent unit12/cuts2 not run (fail-fast). Actual console menu2 cannot load; actual HTTP bundle1/established-generation4 return409. Existing schema1 writer FileSystemManager.cs:5745 and LoadNamespace.cs:217 serialize **SchemaVersion/GenerationId**. New refusal-only GmLoadSessionOperation reader incorrectly assumed camelCase generationId; originalRunning owner identity had bypassed it in synthetic positives. This is a causal runtime RED on the real no-GM consumer, not fixture availability. [Evidence](recovery/evidence/load-ux-ordinary-schema-causal-red/manifest.json). Minimal fix plan pending independent Sol: expose/reuse the existing strict schema1 ParseSessionGenerationText interpretation for raw refusal-only expectation. Actual quiescent/canonical revalidation after stop stays mandatory; no new codec/journal/admission/authority or held locks during IPC. Rerun only the7 failed current-schema consumers; retain8unchangedpasses, then execute previously unrun12unit+2cuts.

### Reviewed existing-schema reader fix WIP

Independent actual Sol6.1/xhigh approved the minimal source plan. Share unchanged strict case-insensitive schema1 parser (including duplicate/schema/GUID rejection and BOM decoding); expectation remains refusal-only and canonical validation after original stop remains mandatory. Split exact7 failed integration methods from8passed to avoid replay; pending fresh build and7GREEN+unrun12unit+2nontransientcuts. No other runtime changes.
