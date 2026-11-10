# T197 original per-run custodian hosting DESIGN — 2026-10-16

## Ruling and guarantee boundary

Recommend an **original joinable per-run application host in a new `--custodian` mode of the existing BookOfEternityGMBridge executable**. That assembly already references Client and has Client's InternalsVisibleTo access; no new executable, installed service, credential or privilege is needed merely to construct the proposed graph. This is an application ownership proposal, not a claim that existing `--host` has the guarantee. The custodian creates the actual client service graph, GameEngine, original main coordinator and native terminal owners from birth. Console/agent/browser/GM presentation channels are clients of that live graph. A frontend process never receives its lease, pin, applied/story proof, native handles or executor task.

The strongest proposed positive case remains **frontend/bridge presentation loss while the original custodian, original native scope and registered publication owners survive**. A process called “bridge” cannot simultaneously be the failed process and the surviving keeper: new `--custodian` and presentation processes have separate roles. Today's `BridgeHost` owns native authority and today's Client owns GameEngine; neither arrangement has this combined survival property. Renaming either process supplies no proof. Already-running legacy operations cannot be upgraded into this route.

This bounded design makes hosting, original object construction, parent invocation seams and lifetime ordering concrete. It does not close exhaustive source publication/work enrollment, B03 codec/CAS/bounds, B04 endpoint/caller/retirement rights, or the post-seal native-settlement permission adapter. All proposed APIs and schema projections are **UNACCEPTED**. Whole B01/A1/G-C and T197 are not engineering releases. Safe HOLD on keeper loss, hung work, reboot/container or host migration/distinct-root restoration is preservation, not successful ordinary lawful-death resolution or an accepted reduced product contract.

Source accepted Mortal n/action/k_death and genuine evaluation n+1 remain unchanged. No source replay, extra tick/dice/reward, successor acceptance, new G from metadata, extra QTE or child receipt retiring its parent. DEP-C/I/L/full target/H2/H3/HQ/new joint QTE remain blocked; D4/guided/visual/Windows/D8 remain open. Continued design is already authorized; exact independent acceptance and bounded TDD/qualification remain engineering gates, not a request for repeated permission.

## Actual current invocation and ownership graph

All source statements below refer to historical `85464398184f388860921da873bb01440cca777c`. The companion records complete Git blob/SHA256/bytes/line pins. No working-tree source or current runtime was used as evidence.

| ID | Actual existing path / lines | Current owner and implication |
|---|---|---|
| E01 | Client/Program.cs:19–37,55–80 | Helper, participating-control and worker-host modes run before ordinary UI. prepare-live-turn creates its own FileSystemManager and participating operation, then EnsureDirectoryStructure/PrepareAsync. These are independent real writers, not passive UI. |
| E02 | Client/Program.cs:86–118,122–310,336–355 | Console/agent selects an input source, constructs a DI singleton graph, resolves GameEngine and awaits RunAsync. Agent HTTP host starts in the same process. Finally disposes audio/API/input; there is no original main/native owner in this graph. Web branches to a different host before console DI. |
| E03 | Core/GameEngine.cs:107–161,187–242 | Constructor retains actual FS, StateManager, GameLoop, StoryService and all listed services. RunAsync bootstraps storage, applies font/audio, health checks, then enters the interactive main menu; finally stops audio. The outer catch logs to canonical error_log.txt. Calling RunAsync is not a passive attach. |
| E04 | StateManager.cs:33–74; FileSystemManager.cs:589–616 | Public constructors use null hooks; FS resolves physical base path/interns root identity. Bootstrap acquires a real lease, ensures directories, admits config, atomically establishes config/G and releases the owned lease. No runtime G is copied from a host descriptor. Other service constructors are not exhaustively audited here. |
| E05 | TurnLifecycle.cs:1484–1504 | Ordinary console ProcessPlayerTurn retains one continuous participating operation across StagePlayerTurnAsync, terminal wait and FinalizePlayerTurnAsync. Its entry is a concrete parent enrollment seam before Stage/health/original request publication. |
| E06 | TurnLifecycle.cs:885–888,952; MortalJointStoryTurn.cs:75–115 | Late reconciliation enters participating scope; actual applied-phase/lease/projection checks precede story append at86; validation, runtime increment, terminal cleanup and response update follow. Prior story receipt remains only this child. |
| E07 | SessionOperationContext.cs:93–174,192–310; FileSystemManager.MainRunFence.cs | Participating call acquires original main admission before G/body; nested bindings borrow. Finalization uses a read-only lease and retains established outcomes separately from close failure. Actual coordinator ambient Access can be borrowed in-process; same root or a DI reference alone does not install that Access. |
| E08 | GMBridge/Program.cs:18–40,210–265,474–545 | Existing separate bridge creates BridgeHost, opens main coordinator, launches native terminal and runs server/control loops. Attach starts output/root/authority/keyboard/resize work and only afterward binds retirement at543. Host must change registration order, not merely keep these Tasks in a later list. |
| E09 | GmSessionRunCoordinator.cs:59–94,126–158,209–268 | Open acquires original main guard and worker conjunction; Launch takes lifecycle/canonical, establishes exact G, publishes Prepared, prepares held native child, publishes Running and consumes release once. RunOperationAsync creates a real local pin/ambient Access; it does not enroll arbitrary descendant tasks or publications. |
| E10 | OwnedTerminalSessionFactory.cs; NativeLineageOwner.cs:139–181,295–329; BridgeHost.WindowsLaunch.cs:10–59; ConPtySession.cs:34–118 | Linux creates original NativeLineageOwner/helper; Windows creates original suspended process/ConPTY/Job in BridgeHost. Managed native owners and descriptor/handle/stream tasks must be in the keeper before creation, not transferred after failure. |
| E11 | GmMainOperationClient.cs:23–86; BridgeHost.MainOperations.cs:5–28; GmSessionRunCoordinator.PipePins.cs | External clients authenticate an original connection/grant; active connection loss invokes Lost, retains unresolved pin and uncertainty. Moving client-owned operations into the keeper avoids a frontend being their pin transport; it does not recover arbitrary old external Lost pins. |
| E12 | LocalWebUiHost.cs:28–100,112–132 | Web owns its own FS/StateManager/service graph and eagerly bootstraps during Build. BrowserRequestAdmission queues web siblings; it is not the proposed host-wide parent lane. Build does not register GameEngine or StoryService. Some GET-like builders call RefreshGameStateAsync under participating scope and cannot be classified as passive presentation solely by HTTP verb. |
| E13 | TurnLifecycle.cs:1507–1554; BrowserPlayerActionService.cs; ExplorerWebCommandService.cs:63–97 | Browser staged preparation, idle wait and expected-G finalization deliberately have separate actual scopes. Do not replace those with a continuous pin merely by cohosting. Browser action/Explorer services are actual owners, not DTO-to-GameEngine aliases. |
| E14 | Launcher/bookofeternity.ps1:685–755,835–864; Launcher/gm_main_operation.ps1 | Linux current launcher requires foreground visible bridge; Windows launcher creates a separate PowerShell bridge; daemon starts separately. Participating script helper is a retained process/connection. PID status convenience checks do not establish custodian admission or retirement. No launcher/settings execution was performed. |

The JSON enumerates the **38 actual console DI registrations** and **30 actual web registrations**, with line references. Those are construction inventories, not all constructor side effects, all state leaves or all live work. Share the one original FS/StateManager/StoryService for the selected owned engine; do not inadvertently create another graph by calling existing Program or LocalWebUiHost.Build inside it. The web-only registrations and mode-specific AudioService require an explicit composition decision before browser hosting can be qualified.

## Chosen files, entry and object construction

Proposed code locations identify an implementable integration seam; none is created by this task.

| Proposed location / API | Exact responsibility and restriction |
|---|---|
| GMBridge/Program.cs: new `--custodian --root <existing-root>` dispatch before existing `--host` | Start one ordinary-user per-run keeper. An explicit root is data for physical-root admission, not authority. Reject mixed legacy/neutral/control flags. No automatic attach to a process found by PID, and no new service/install/security/network setup. A concrete launch/attach handshake is B04, not specified by inventing a key here. |
| GMBridge/OriginalCustodianHost.cs | Sole lifetime owner of root startup, one `OriginalClientRuntime`, one `OriginalMainRuntime`, host registration/seal state, original task registry and eventual teardown. Owns references strongly until actual retirement. Does not implement cold reconstruction or automatic restart. |
| Client/Core/ClientRuntimeServices.cs: `AddOriginalClientServices` | Extract the existing console registration closure from Program.cs. Accept the already-created exact FS, settings/input/presentation bindings; resolve GameEngine exactly once. Production hooks null. Construction is startup-ticket work; constructor effect audit remains a prerequisite. No `BuildServiceProvider` in a request or replacement session. |
| Client/Core/GameEngine/GameEngine.HostedLifetime.cs | Extract initialization currently at RunAsync:198–211 and hosted loop entry, preserving owner decisions. Add `RunOwnedClientActionAsync` call boundaries for ordinary ProcessPlayerTurn and late reconciliation. This is an actual-caller wrapper, not a public deserialized delegate/target API. UI-dependent health/error handling still needs its exact presentation seam. |
| GMBridge/OriginalMainRuntime.cs | Extract native launch, original status publisher, PTY input/output, prompt/root/authority observation and stop ownership from BridgeHost. Inject exact host FS; do not construct `_neutralFiles` again as current startup does. Current bridge console/keyboard/window operations move to a frontend adapter. Retain actual coordinator/terminal objects and release functions. |
| Client/Services/GmRuntime/GmSessionRunCoordinator.cs: `BindHeldCustodianWorkAsync` seam | After the real prepared terminal is assigned at241 and before its single release at257, require the host's pre-enrolled native/pump promises and actual retirement binding. The outer native creation ticket must exist before calling Prepare (which itself starts processes/IO). Installation cannot create fake completed tasks for unavailable work. Running publication/release ordering remains original; no replay of release on ACK loss. |
| Client/Core/CustodiedOperationRegistry.cs | Install a parent promise/borrow before invoking the actual delegate; retain actual Task and in-process Access, nested registrations and exact already-armed publication debts. Integrate SessionOperationContext entry/close; it cannot infer completion from Task status without observing its result and all registered subordinate work. |
| Client/Core/CustodiedClientInputSource.cs and presentation adapter | Keeper-owned input queue supplies existing IConsoleInputSource contract, with distinct seal outcome. Frontend loss never becomes a player Escape, null input, Cancelled turn, selected default or fabricated answer. A blocking SystemConsoleInputSource cannot be used in the keeper. |
| WebUi composition seam and frontend transport adapters | At most one authoritative engine/graph for the run; web action services remain their actual owners. Reuse shared original services without calling Build's eager bootstrap twice. Every writer-bearing request passes host admission, including browser state refresh and status writers. Browser integration remains OPEN until this graph/lane mapping is audited. |

Use the existing assembly relationship rather than a new process just to call StoryService. The keeper can have joinable asynchronous work on multiple threads; only gameplay owner invocation is serial through its admitted action lane. Native status/output draining must not run on the same blocked executor as synchronous input. Main/worker/native lifecycle services retain their own existing serialization.

**Presentation is a concrete blocking seam, not a claimed existing abstraction.** GameEngine directly calls Console/AnsiConsole, creates StandardTextComposerConsole, applies font/audio, and its outer catch logs canonical bytes. Merely injecting AgentConsoleLiveInputSource does not detach rendering. Proposed keeper mode needs a reviewed renderer/output boundary whose writes go to a host-owned presentation buffer and whose input is the keeper queue; frontend subscribers consume copies. GM terminal bytes and game presentation remain separate channels. No live canonical authority is placed in the renderer. Do not rebind process-wide Console streams on reconnect, or make a native drain await a dead frontend pipe. Bounds/overflow preserve drain safety and stop admission; UI frames may be classified replaceable only by their real presentation owner, never canonical receipts or original responses. Full console rendering, font/clipboard/desktop launch/media isolation and browser parity remain explicit integration obligations. They cannot be silently disabled to claim qualification.

## Birth and lifecycle: exact proposed ordering

Keep a host lifecycle distinct from the existing main-run record. Proposed host states are `Constructing, Initializing, StartingMain, NativeHeld, Serving, Sealed, Joining, Retired, Held, Disposed`; frontend states are `Attached, Detached, Revoked`. They are correlation/state-machine projections, not new on-disk authority or replacement meanings for Prepared/Running/Stopping/Stopped/Uncertain.

1. **Constructing.** Before invoking any factory, install `HostBirth` completion promise and startup work registry. Validate arguments; construct the single FS using its real physical-root resolution and null hooks, then DI graph. A constructor that opens files, starts tasks or invokes callbacks must register under this startup ticket before effects; no claim that all 38 constructors are inert. No source action admitted and no fresh G is guessed.
2. **Initializing.** Enter one host-wide startup lane before calling actual `StateManager.BootstrapLocalStorageAsync` or health initialization. Their existing participating/quiescent guards acquire and release normally. Do not acquire `GmMainOwnerGuard` separately and then recursively call bootstrap beneath it; that would attempt the same physical guard again. Bootstrap result and real lease disposal are retained. Existing storage may contain an active/unresolved main owner and must refuse, not be adopted. Initial health/runtime setup needs its own finite publication inventory (OPEN); it is not a story debt. After bootstrap, reserve exactly its G for subsequent start checks; no recovery creates a new G from the host record.
3. **StartingMain.** Register original main-open task before `OpenProductionAsync` (Windows uses its real startup observation first, registered under HostBirth before invoking the probe). Call original coordinator with the same FS. It acquires main guard/worker conjunct; `LaunchProductionAsync`/Windows equivalent receives the actual expected existing G and original admitted configuration callback. Existing lease acquisition/recovery and config reads are registered startup work. Coordinator rechecks G under its real fences. Startup may race external change only by failing its existing checks; startup lane is not a cross-process filesystem fence.
4. **NativeHeld.** Reserve native owner/descriptor/IO/partial-failure slots before native Prepare. Keep the original factory's partially-created owner on failure. Before single release, install host-native pumps/observations with registered promises and actual task binding. Pumps are host-owned, not frontend streams. Input/resize/prompt admission stays closed until actual release and successful required initialization. If seal or an ownership failure wins, never release/release again; retain and settle the held scope through its original owner. Prepared/no-child uncertainty remains HOLD.
5. **Serving.** Only after actual release, original metadata ACK and required startup joins may the keeper admit one client action lane. Resolve the existing game-loop state through its original ordinary entry; no new session/turn state from an attach DTO. Session lifetime objects survive frontend detach. One top-level protected action is admitted at a time; nested source work borrows that parent's registry/access. Native observer/drain/control tasks stay separately owned and can run while the action awaits GM output.
6. **Sealed/Joining.** One host mutex linearizes seal with action admission, native release, input/prompt/resize registration and native publication arming. Existing mutexes remain respected; never await while holding this mutex. Seal denies new work and new semantic phases, retains existing promises and original objects, revokes frontend input, and starts joining/settling only already-admitted work. A frozen plan with no original arm does not execute. Fresh retries are new admission, not cleanup. Actual node outcomes survive partial failure.
7. **Retired/Disposed.** Retirement requires the genuine whole-parent work/debt/native/main proof, not just no subscribers or one story result. Original main confirmation/metadata receipt and all relevant resource disposal must succeed before normal process exit. Retain failure/uncertainty and diagnostics while the keeper is alive. Disposed is terminal; a fresh process cannot resume the previous host state. The per-run keeper does not automatically launch another native run or replay a player action.

This ordering resolves who constructs and retains the original objects. It deliberately does not claim that existing bootstrap/health/lifecycle methods are fully inventoried or that the necessary permission after sealing already exists. For review, those are named pre-code dependencies below.

## Concrete parent entry and pre-effect registration

For the selected ordinary parent, insert an actual-owner wrapper at **ProcessPlayerTurn entry before line1495**, not at story append or after StagePlayerTurnAsync. For the late entry, insert it **before line888** of ProcessLateTerminalAndIdleTransitionsForCurrentSessionAsync. These two methods are concrete source admissions. Additional WaitForGmResponse/Raw and life/Explorer callers remain separately inventoried prerequisites; the two seams are not a complete source catalog.

Proposed in-process shape (illustrative signature, not runnable implementation):

```
OriginalClientRuntime.InvokeSourceAsync(
    OriginalClientActionSlot slot, Func<OwnedParentInvocation, Task<T>> actualOwnerBody)
OriginalMainRuntime.RunOriginalOperationAsync(
    OwnedParentInvocation parent, Func<Task<T>> body)
CustodiedOperationRegistry.RegisterBeforeInvoke(
    OwnedParentInvocation parent, OwnedWorkKind kind, Func<Task> body)
OwnedParentInvocation.RequireSemanticAdmission(OwnedSourcePhase phase)
OwnedParentInvocation.Seal(OwnedSealReason reason)
```

Only the live host's caller code creates action slots/delegates. Public protocol data never supplies delegates, method names, paths, source tuple authority or a serialized capability. Under the admission mutex: reserve the host action lane and install a promise/parent reference; then release mutex and enter the **actual** `coordinator.RunOperationAsync` to create its local pin and ambient Access; then call the existing SessionOperationContext wrapper. SessionOperationContext borrows that original Access via FileSystemManager.MainRunFence. It must not call back through GmMainOperationClient to its own server or lock the physical main guard again. The proposal extends this path with a registry association and retained exact outcomes; no fake remote CloseObserved is invented for a local pin.

The parent registry retains actual `Task<T>`, its invocation-before-Task-return promise, coordinator Access/pin, operation-owned leases, child calls, native attempts, stream disposals and callbacks. AsyncLocal is routing to a live object, not exhaustive task ownership. A body may execute synchronous code before returning its Task; the promise/borrow must already exist. Synchronous throw produces a retained failure on the same promise. Nested participating scopes and the story child reuse the parent; no duplicate whole-operation close. Registration failure before invocation must have zero body effects. Every branch completing after seal hands its typed result back to the original registry and holds before a next semantic phase.

The admission lane is held across the actual ordinary source operation, including waits, until its actual whole-parent join/finalization. **It is not the canonical lock** and cannot block the original GM from producing the awaited result. Cooperating native/daemon writers must be explicit enrolled subowners of that active source or separately scoped native obligations. A request that could perform an unrelated gameplay action is queued/refused before effects. Root serialization alone is insufficient for direct CLI writes that bypass the filesystem owner. Complete native/helper writer coverage remains OPEN; until proved, this topology cannot be labeled recoverable source admission.

Browser must preserve its actual staging/wait/finalization boundaries. A per-run host can retain a browser family owner without retaining an unauthorized continuous canonical/main lease through idle. Each genuine phase needs its own actual issuer and registered task, with expected-G/original checks and existing pending claims. BrowserRequestAdmission does not establish that contract on its own. Load completion/cancel currently sit outside its queue because they release a retained Load; a host must preserve that no-deadlock exception through a specific retained-load slot, not route all requests blindly under the source lane.

## Live work inventory and explicit uncovered writers

Rows below are the finite hosting integration catalog, **not a claim of all-leaf source closure**. A row marked OPEN cannot count as joined or be dismissed because its product UI is hidden.

| Work | Existing owner / effect | Proposed keeper ownership and unresolved closure |
|---|---|---|
| W01 startup/root/config/G/health | FS constructor, StateManager bootstrap, GameEngine initialization | Register before construction/bootstrap; actual same-G/native/lease outcomes. Full initialization health/namespace leaf inventory OPEN. |
| W02 ordinary/late source parent | TurnLifecycle ProcessPlayerTurn / ProcessLateTerminal... | Actual top Task and original in-process main pin, every nested admission/finalization. Phase gates before later turn/cleanup; full earlier/later source leaves OPEN. |
| W03 story child | Existing StoryService/applied capability/native append | Prior immutable A1 slice applies: one gameplay story leaf plus exact journal/scratch/read guards; no child retirement of W02. Exact-attempt recovery and post-seal permit OPEN. |
| W04 terminal wait/key/render callbacks | TurnLifecycle:486/553; actual awaits608/612 | Register both promises before Task.Run; join actual read tasks/disposal. Do not translate frontend seal into existing Cancelled branch. Renderer callback failure and canonical LogError are separate obligations. |
| W05 validation/repair waiting | ValidationAndRepair.cs waitTask around3367 | Register from its actual owner before invocation; callback/input/timeout writes and distinct outcomes still require full mapping, OPEN. |
| W06 native terminal and startup observation | NativeLineageOwner/helper/status/error/control/exit; ConPTY/Job/close/stream settlement; WindowsStartupObservationProbe | Create in keeper from birth, bind partial start before release, retain actual process/job/descriptor identity. Register the Windows PowerShell/WMI observation process, stdout/stderr/exit tasks and actual TrySettle before main guard acquisition; a failed probe retains its original foreground owner and blocks launch. Its restart timestamp is not unique boot identity or permission for an old run. A4 platform/provider qualifications remain OPEN. |
| W07 bridge input/prompt/resize/output | BridgeHost Program:528–543,654–662; PromptDispatch.cs | Extract into OriginalMainRuntime; pre-enroll input-write/prompt tasks, control loops and all retained drain tasks. Frontend subscribers are not the native output sink. Draft observation callback/process and prompt-delivery task must be registered separately; transitive IO audit OPEN. |
| W08 status canonical writer | BridgeHost.StatusPublication.cs:23–114 writes game_state/control/gm_bridge_status.json under owner.RunOperationAsync | Host owns publisher/queued snapshot/actual publication and settlement task. Queue clearing at seal only discards unstarted diagnostic snapshots; already-started native debt must settle once. No rename as pure display work. |
| W09 external main control/daemon | GmMainParticipatingControl, DaemonExchange, launcher helper/daemon | Current helper owns its own FS/publications; EOF marks unresolved. Existing grant is not an in-host task. Native containment plus exact helper publication/close adapters needed; full closure OPEN. No reconnect/tuple decoding repairs them. |
| W10 native GM CLI file writers | configured shell/CLI and descendants | Native stop covers only qualified original scope; it is not exact per-file publication/debt provenance. Direct writes, escaped jobs/namespaces and work sent to another service remain OPEN/blocked. No assertion canonical lease protects bypassing writers. |
| W11 workers | coordinator production conjunction, DispatchWorkerTaskAsync:1585–1609, worker pools/ledger | Preserve existing quiescence/absent-inventory rules. Worker dispatch is a real pinned task, not a UI command; D4 remains OPEN, no disabling/relabeling inventory to pass. |
| W12 browser request/state/pending/load | Web host/services/BrowserRequestAdmission | Shared root/graph composition and exact phase admission; GET-like state aggregation may write. Browser methods, retained-load release and media work not exhaustively enrolled, OPEN. |
| W13 auxiliary CLI/preparation | Program special helper/worker/prepare branches | Own lifetime and native scope or refuse recoverable admission before execution. Do not route their arbitrary command/path data into story authority. OPEN. |
| W14 audio/image/clipboard/desktop | AudioService starts operation Task; ImageService, ConsoleAppearanceService, IClipboardService/DesktopPathOpener | Separate presentation/device/external IO owners. Keep actual begun work joined or explicitly outside source with proved no writer authority; no simple no-op completion. Full side effects/provider closure OPEN. |
| W15 diagnostics/hooks/errors | null production hooks; debug collector, logger providers; TurnLifecycle:4386–4410 canonical error_log append | Hook-free production limits some callbacks but does not prove provider closure. Post-seal host-control outcome bypasses generic gameplay catch/log writer; already-started error log publication is a distinct exact debt. Arbitrary callbacks cannot mint follow-on work. OPEN. |
| W16 endpoint/read/write pumps and shutdown | current named-pipe peers, HTTP host, original input/response/close drains | Owned promises before accept/serve invocation; strict B04 authorization separate. Transport disposal is not original operation close; handler request cancellation cannot revoke native custody. Complete endpoint framing/bounds/rights OPEN. |

D0 host/main metadata, D1 trusted-local publication, D2 original/reversible publication, D3 browser/mutation-recorder, D4 worker and other source-specific debts retain their original owners. The catalog does not merge them into one generic compensating transaction. An already-armed story D1 is settled once, not again as a semantic story debt. An after-image/hash or host-state field proves none of these owners completed.

## Seal, join, native stop and post-seal permission

Separate three events: **FrontendDetached** (transport/presentation), **SourceSealed** (no new semantic work), and **OriginalAuthorityLost** (native/main issuer invalid). Losing a frontend does not itself call current NotifyUncertain on a still-intact native coordinator, but does trigger host admission seal and truthful detached/held diagnostics. A genuine authority/metadata/IO failure keeps existing sticky uncertainty; never clear it merely because the keeper is still alive.

Under the host mutex, detach/seal wins or invocation/arming wins. After seal, no new action, new native launch/release, fresh publication retry, synthetic answer, turn phase, timeout error publication, output regeneration or new reward. Pre-enrolled native IO for an already-armed exact publication may finish/roll back/clean through its original owner. Merely registering a future gameplay task does not arm all of its later writes. A parent waiting for GM output must leave through a distinct host-control **HeldByCustodianSeal** branch; it must not take `TerminalSignalWaitOutcome.Cancelled`, whose callers delete requests/signals and may restore originals (TurnLifecycle:99–110,293 onward). Existing generic catch/LogError also cannot initiate canonical logging after seal. Place the new typed gate before those catch/cancel continuations; preserve established outcomes and no destructive fallback.

Current source/main validators deliberately reject ordinary work after AdmissionClosed/uncertainty/root exit. **This hosting design does not override those checks.** The already-started publication needs the previously proposed private debt-bound settlement admission: actual original parent/lease, frozen one-attempt inventory, exact current G/root fence, retained native decision, no new semantic validation/retry. A separate purpose or branch that merely bypasses `_closed` is forbidden. Its exact permission proof and lower recovery interception are SV-B02/SV-B03 and remain OPEN. If that seam is unavailable, settlement holds rather than using the regular API after revocation. Native commit with failed semantic proof remains known commit plus failed proof; no post-stop row regeneration.

The recovery controller runs outside the blocked source executor. It can seal future native input, request genuine original native stop, and join registered tasks without holding lifecycle/canonical locks needed by those tasks. Control cancellation is a request; actual task, stream/native operation and callback settlement is the proof. Existing BeginStopAsync waits pins before lifecycle acquisition, but currently times out after5seconds and marks uncertainty. Calling the current StopShell sequence is therefore **not** a completed new recovery implementation: an exact host recovery orchestration must preserve metadata semantics, stop needed external work without deadlocking on a source waiting for it, settle the admitted debts with its specific permit, then release actual borrows/pins and use the genuine same-G main retirement owner. No fake pin-zero operation or fabricated ClosedObserved frame is introduced.

Lock constraints: host registration mutex is short and never held over awaits, canonical/lifecycle acquisition, pipe IO or user callbacks. Existing parent main custody precedes filesystem work. A task holding its existing canonical lease may briefly register a nested borrow; recovery must not hold the registration mutex while waiting for that lease. Main coordinator transitions remain its own serialized work; do not call user callbacks while holding its `_sync`. At final cleanup, acquire existing lifecycle then canonical only after conflicting owned tasks/borrows have actually joined. A host action lane does not replace any current main/worker/browser-original/canonical check.

O1 retained: native supervisor EOF latches uncertainty and continues stop/reap. Exit is gated by `stopping && empty && !exec_pending`, not EOF alone. Supervisor EOF, root PID absence, elapsed deadline, closed frontend pipes, a new boot_id or a fresh custodian cannot issue retirement.

## Frontend protocol boundary and shutdown distinctions

No endpoint, credential/key, ACL, listening address, service installation or network policy is specified or changed here. B04 must independently establish the original host/caller relationship before this design is implementable. The logical command boundary is nevertheless fixed:

* `ObservePresentation` / `ObserveRecoveryStatus`: detached data from the actual host; never raw proof objects or an authority-bearing “resume” token.
* `SubmitExistingPlayerInput`: one existing input/prompt answer bound to the live issued prompt and source lane; action ID/sequence provides correlation, not capability. Duplicate delivery observes retained original outcome; it does not execute the input twice. No arbitrary method/delegate/file path/after-image command.
* `SubmitTerminalInput` / `ResizeTerminal`: existing original terminal input lifetime, real per-write registration and seal checks; no grant to launch an independent shell/writer outside the native owner.
* `DetachFrontend`: revoke only this subscriber; retain original source task/native owner. A detached current player input is not a default/cancel choice.
* `RequestApplicationShutdown`: explicit intent to stop this original host; passes existing close/stop ownership and exact registry joins. A request is not a retirement receipt.

These names are proposed logical variants, not a complete wire schema. The existing main-operation protocol remains a separate authenticated pin/close protocol for actual external writers; frontend commands are not aliases for begin/activate/closeMainOperation. Same-user status JSON, endpoint name, operation tuple and portable SHA256-PAYLOAD-JSON are integrity/correlation data, not a strong same-user credential or proof of original owner possession. No new cryptographic grant follows from the source term “signed”.

| Event | Host policy / retained facts |
|---|---|
| Browser tab closes / agent HTTP client disconnects / console presentation pipe breaks | Detach subscriber, seal protected source input/action admission as applicable. Keep source graph/native output drains. RequestAborted is not an original-operation close or permission to rollback. |
| Old frontend reconnects / replacement viewer appears | A valid B04 attach can display retained data only; it neither reopens sealed operation nor recreates a lease/proof. Original future gameplay resumption is outside this recovery design. |
| Explicit normal app exit with no unsettled source | Seal, join original presentation/input tasks and any owned metadata/native work, genuine stop/retirement, then dispose graph and exit. No idle-timeout forced success. |
| Explicit exit during an accepted/uncertain source operation | Hold gameplay; settle only already-started work under the exact owner permits. If retirement cannot be proved, report held state and retain process/objects while alive. Do not dispose DI/native handles in Program finally merely because UI RunAsync returned. |
| Ctrl+C/OS close/process kill | Best-effort shutdown requests are not guaranteed to run. If the actual keeper dies, its live issuers disappear and cold route remains blocked. No claim of survival through application/session/host teardown. |
| Frontend and keeper launched in same killed job/session/container | Not the positive bridge-loss case; topology/platform qualification fails. Do not relabel loss as a successfully recovered detach. |
| Original native scope/metadata authority lost while keeper remains | Existing uncertainty persists; no successful retirement from keeper liveness. |
| Kernel boot/PID namespace/root/host changes or copied distinct root | Metadata and identical bytes cannot attest old native work exhaustion; B05/DEP-L remain OPEN. |

Process lifetime is one explicit application run; no watchdog automatically recreates a dead custodian and no automatic native relaunch. The host can remain held until a supported owner resolves it or the user terminates it, but user termination does not turn an unresolved run into Stopped.

## Platform and alternative comparison

**Selected joinable host.** One per-run keeper owns client graph and native adapters; frontend loss cannot destroy those objects if the keeper survives. Same-owner proof identity and nested leases are preserved naturally. Cost: larger process contains UI/service integration, a hung managed task cannot be forcibly aborted safely, and keeper death loses all live capabilities. These are real liveness limits, not a timeout policy. Keeping a legacy external Client process as the authoritative engine would leave the hosting problem unsolved.

**Dedicated source runner alternative.** A fresh runner could be created before source effects with its own original graph and scoped process ownership; the parent could stop it without killing the native custodian. It cannot receive the current story applied/lease capability by serialization. It would need original action/authority issuance from birth, ordinary GameLoop/session identity ownership and exact cold publication/debt settlement after runner death. Killing the runner destroys its managed proof/readback/finalization owner; native process exit alone cannot recreate them. This is a larger source/authority contract and is not selected. A dedicated runner hosting the entire graph from birth is a possible future product design, not an adapter that can wrap the current story child after entry.

**Linux.** Keep the recommended A4 original NativeLineageOwner/C helper in the per-run keeper, not the presentation process. The current foreground Linux bridge launcher does not establish detached keeper lifetime. A new ordinary process launch must establish its own application lifetime/stdio and prevent frontend channel disposal from closing native control/status; platform qualification must demonstrate its actual session/container policy. `setsid` changes session/controlling-terminal relationships; it does not protect against host/container/session-manager termination. The subreaper API describes orphan reparenting to a living ancestor, not crash-proof restoration of a dead keeper. No systemd service, lingering/delegation, remount, privilege or policy change is selected. If ordinary launch constraints cannot supply the required topology, refuse qualification and leave the stronger-manager decision explicit. [Linux setsid(2), man-pages6.19](https://man7.org/linux/man-pages/man2/setsid.2.html); [PR_SET_CHILD_SUBREAPER, man-pages6.19](https://man7.org/linux/man-pages/man2/PR_SET_CHILD_SUBREAPER.2const.html).

**Windows.** Original ConPTY/Job/process handles and drains belong to the keeper before suspended child release. A frontend is not the last job-handle keeper or ConPTY host. Existing outer job policy may terminate both; no breakaway-rights/elevation workaround is granted. Job inheritance has documented exceptions, including WMI process creation and configured breakaway; that is why external writers cannot be assumed covered. ConPTY shutdown can produce final output and deadlock if channels are not drained, so presentation loss must not stop the keeper's native drain. No named-job lookup or PID re-open substitutes for original custody. [Microsoft Job Objects, updated2025-07-14](https://learn.microsoft.com/en-us/windows/win32/procthread/job-objects); [Creating a Pseudoconsole session, updated2022-09-20](https://learn.microsoft.com/en-us/windows/console/creating-a-pseudoconsole-session).

The official documents were read-only verified on2026-10-16. They support these limited API facts; the hosting recommendation and failure-policy consequences are architectural inferences. They do not prove this checkout/cloud supports any chosen launch topology. No platform, native, application or process qualification was run.

## Closed host projection and minimal pre-code blockers

The host projection is intentionally **data only**, proposed to be bound within the existing proposed A4/B03 metadata family rather than adding an accepted durable path. Exact field set: `schemaVersion,hostInstanceRef,rootRef,mainRunRef,hostPhase,frontendMode,frontendAttachmentState,admissionSequence,sealReason,startupWorkRefs,parentOperationRefs,nativeOwnerRefs,retirementRef`. Version1; hostPhase is the enum above; frontendMode is `Console|AgentConsole|Browser`; attachment is `Attached|Detached|Revoked`; sealReason is null or `FrontendLost|ExplicitShutdown|SourceFailure|OriginalAuthorityLost`. References are typed original-parent correlation references, never serialized objects/OS-handle integers. Unknown fields/variants, cross-root/G/run references and duplicate operation identities refuse. No automatic `Retired` from empty arrays; only original live retirement issuer supplies retirementRef. Empty work inventories before enrollment cannot certify work absence. Exact encoding, bounds, CAS/durable acknowledgement and terminal-record retention are B03 and remain OPEN; these field names do not close that contract.

Minimal dependencies before this becomes a code/qualification release:

1. **H-B01 exact composition/presentation and constructor effects:** extract the real 38-registration graph, audit every constructor/initialization callback before effects, retain original GameEngine/menu/turn state, provide the cancellable-but-nonsemantic input/output seam, and define browser's30-registration composition without duplicate bootstrap or concurrent service mutation. Full audio/desktop/provider parity remains separate, not skipped.
2. **H-B02 parent/source/writer closure:** finish W01/W02/W05/W07–W16 exact reachable tasks and finite publication inventories, including cooperating helpers/daemon/direct CLI and native scope. A native containment claim or the story's one leaf is insufficient. Do not admit a source that can reach lawful death with incomplete required recovery ownership, then reject it late.
3. **H-B03 permission and lower publication integration:** implementable private debt-bound post-seal permission preserving current-G/Lost/root guards; exact original publisher pre-effect arm ACK; reader recovery interception; actual outcome/cleanup/lease/close retention. Existing BeginStop/NotifyUncertain paths alone cannot do this. No guard weakening.
4. **H-B04 protocol/durability:** parent B03 strict codec/version/CAS/bounds and B04 actual endpoint/caller/data-retirement contract, request/response/task lifetime and no replay. No key/service/security assumption has been supplied by this report.
5. **H-B05 supported lifecycle/platform:** actual ordinary-user keeper topology and Linux/Windows bounded evidence; keeper-loss/cold/reboot/migration/distinct-root proof issuer remains absent. Safe HOLD does not qualify full G-C/T197.

Dependent DEP-I/L/full target/H2/H3/HQ/newQTE contracts remain unchanged. A missing helper in existing authority may be routine extraction; a new lifetime/registration/post-seal issuer is not routine D11. The source-backed host choice can receive independent conditional review now. No implementation release, F2, activation, target-write authority or new OS/service grant follows.

## Future bounded checks — all UNRUN

* **H01 Birth-before-constructor — UNRUN:** Synchronous constructor/FS/debug callback failure before first Task; startup promise already retained; no unowned native/file effect.
* **H02 Single actual graph — UNRUN:** All selected source services use original FS/StateManager/StoryService/GameEngine instances; web/bridge factories cannot create second graph or bootstrap twice.
* **H03 Bootstrap/main guard ordering — UNRUN:** Bootstrap recovery/G outcome retained; no recursive main guard acquisition; foreign active main/G change refuses original start.
* **H04 Held-native release race — UNRUN:** Seal before/after Prepare and binding; partial owner retained, all native/pump promises enrolled before effects, release consumed at most once.
* **H05 In-process actual access — UNRUN:** Host source delegate enters coordinator.RunOperation then existing participating scope; local nested borrows, no self-pipe handshake or reconstructed Access.
* **H06 Action invocation seal race — UNRUN:** Installed promise wins vs seal; synchronous prefix/throw registered; no next source stage after seal.
* **H07 Frontend loss is not cancellation — UNRUN:** Break viewer/agent/tab transport during terminal wait; no player Escape/default/null answer, timeout terminal-error write, rollback or ready deletion.
* **H08 Parent versus child — UNRUN:** Story success/commit with cleanup debt retained; whole turn, increment, terminal cleanup, error logger and finalization separately gated/joined.
* **H09 Native output during detach — UNRUN:** Detached/slow subscriber does not stall PTY drain or ConPTY final close output; overflow produces explicit held status, no lost canonical receipts.
* **H10 Status/prompt/helper work — UNRUN:** Status queued vs started publication, prompt tasks, draft observer and helper daemon writes remain owned; unregistered source route refuses before effects.
* **H11 Already-armed settlement — UNRUN:** Current validators not bypassed; exact post-seal permit settles one original attempt only; no new transaction retry or semantic replay.
* **H12 Stop deadlock/IO — UNRUN:** Recovery does not await pinned source under needed canonical/lifecycle locks; real read/write/dispose/task errors and hung work cannot be called complete.
* **H13 Browser phases/load release — UNRUN:** Preserve staging/idle/expected-G finalize and retained-load complete/cancel exception; read-like refresh remains registered if it can write.
* **H14 Explicit shutdown — UNRUN:** No-subscriber differs from shutdown; application finally cannot dispose unresolved graph/native scope; real normal shutdown joins before exit.
* **H15 Linux/Windows scope — UNRUN:** Separately prove actual ordinary-user lifetime topology, native EOF O1, descendants/job policy and native IO; unsupported environment refuses, no elevation/install.
* **H16 Keeper/cold/restore — UNRUN:** Kill original keeper, reboot/new namespace/new host/distinct-root copy: no old work proof from PID absence, metadata/hash or fresh process.
* **H17 Protocol replay and legacy — UNRUN:** Duplicate frontend delivery, wrong host/root/G, old pin, copied descriptor and legacy unregistered client never create original authority or repeat source action.
* **H18 Preserved global gates — UNRUN:** No source tick/reward/dice, target acceptance, HC/H2/H3/HQ/newQTE or B01/G-C completion follows from host or child status.

## Final stop

FINALSTOP. This read-only hosting DESIGN is ready for independent review as a conditional bounded proposal. No repository/code/test/runtime/native/OS/service/credential/network changes, build/test/discovery, fetch/push or delegation occurred. Only this new /tmp pair and static author helper were written. Prior immutable pairs remain unchanged. No pending writes after delivery; all engineering and lifecycle blockers above remain explicit.

## Source construction inventories

### BookOfEternityClient/Program.cs

| Line | Actual registration |
|---:|---|
| 132 | `GameSettings` |
| 135 | `FileSystemManager` |
| 137 | `StateManager` |
| 142 | `GameLoop` |
| 145 | `LocalizationManager` |
| 146 | `GameInterface` |
| 152 | `SaveLoadService` |
| 157 | `ImageService` |
| 163 | `StateDistributor` |
| 167 | `ValidationService` |
| 171 | `CriticalStateHealthService` |
| 175 | `CharacteristicsService` |
| 180 | `StoryService` |
| 184 | `ActorMemoryService` |
| 188 | `AudioService` |
| 193 | `ConsoleAppearanceService` |
| 197 | `SystemModService` |
| 202 | `WorldDirectiveService` |
| 206 | `ScenarioCoreService` |
| 210 | `AfterlifeArchiveCandidateService` |
| 214 | `AfterlifeArchiveConsultationService` |
| 218 | `AfterlifeArchiveProjectFuelService` |
| 222 | `SystemGuardianLibraryService` |
| 226 | `AfterlifeReturnGuardService` |
| 230 | `RivalSoulArcService` |
| 234 | `GuardianCorrectionService` |
| 239 | `SoulIdentityService` |
| 243 | `IClipboardService, SystemClipboardService` |
| 244 | `IConsoleInputSource` |
| 245 | `CanonicalStateNormalizer` |
| 249 | `ProgressionScheduleService` |
| 253 | `PendingTurnStateService` |
| 257 | `GuardianTradeService` |
| 261 | `NpcTradeService` |
| 265 | `TrainingService` |
| 269 | `QteSceneService` |
| 282 | `ExplorerMode` |
| 308 | `GameEngine` |

### BookOfEternityClient/WebUi/LocalWebUiHost.cs

| Line | Actual registration |
|---:|---|
| 52 | `FileSystemManager` |
| 55 | `GameSettings` |
| 56 | `StateManager` |
| 61 | `LocalizationManager` |
| 62 | `ValidationService` |
| 63 | `CharacteristicsService` |
| 64 | `ImageService` |
| 65 | `BrowserMediaGenerationService` |
| 66 | `LocalMediaService` |
| 67 | `AudioService.CreateBrowserManaged` |
| 70 | `BrowserAudioService` |
| 71 | `BrowserClientSettingsService` |
| 72 | `SaveLoadService` |
| 73 | `StateDistributor` |
| 74 | `CanonicalStateNormalizer` |
| 75 | `ScenarioCoreService` |
| 76 | `QteSceneService` |
| 77 | `QteWebInteractionService` |
| 78 | `LocalUiSessionLockService` |
| 79 | `BrowserLocalWriteCoordinator` |
| 80 | `BrowserMortalWorldWriteService` |
| 81 | `LocalWebUiSessionStatusService` |
| 82 | `BrowserGameScreenService` |
| 83 | `BrowserLifecycleDashboardService` |
| 84 | `LocalWebUiMainMenuService` |
| 85 | `BrowserLoadStateService` |
| 86 | `ExplorerWebPromptSessionService` |
| 87 | `ExplorerWebCommandService` |
| 88 | `BrowserPlayerActionService` |
| 89 | `BrowserRequestAdmission` |

## Historical source pins

All pins use `85464398184f388860921da873bb01440cca777c`; every listed production source blob also matches `3d262e0303169f2cefb17384e898967e85290e26`. The latter is a static identity comparison, not runtime qualification.

| Path | Git blob | SHA256 | Bytes | Lines |
|---|---|---|---:|---:|
| `AGENTS.md` | `ba14a76f2f3e3ae72f767440412b67b71779da5e` | `4410cc7fb11213629792b280c9a2ba3bdb1d74316dc7950072ce7c158a9c5e23` | 11662 | 199 |
| `docs/development-workflow.md` | `5b105f4f7c0e57212a71d5b110361accd71f7a6e` | `8cc1cb8f4673bf910b0cb3d9c0757b9fdc6facf06e6a2552a6b462c90b270e71` | 66432 | 493 |
| `.specify/memory/constitution.md` | `a307f01cf71528cc53a2ff4d45a0c6c16c34f97d` | `f3e8f4de6a6ac7c82f68ea6f6b37d9dcb95901f59f32b1a7f6353217bc490944` | 14081 | 264 |
| `specs/1536-complete-wound-materialization/plan.md` | `2fe13be20ce318f04e68e018c8b80903baec3335` | `316d9ad6e45df6db7f7a1f8300e15645ed4c6fc76cac1d3eac69718a566ee23f` | 2015249 | 22718 |
| `specs/1536-complete-wound-materialization/tasks.md` | `a2134b3c95e96b3c938032695ec90ae5d09a2894` | `3bc53ca4caa0385a06fdc58b7774bb7caedda6f2d6239dc06e3812470d11b7d0` | 1412351 | 14739 |
| `specs/1553-portable-local-storage/plan.md` | `16413b95c8889f0440d040ed1dd214c81b2e3f8f` | `865e6e30fc365225a0e26aa42f4bf48d645d7ac216fd8da037af9e6a9b40511c` | 2252104 | 20516 |
| `specs/1536-complete-wound-materialization/design/lawful-active-joint-tail-b-death-handoff.md` | `0d58236f7c1ea6ca8149dd1ae6fe26fa6928f99b` | `bfd35f9c095fe707154ebde2641f6627f267bd8dd998f128351f6b9cfcf5fe9e` | 72329 | 275 |
| `specs/1536-complete-wound-materialization/recovery/t197-depc-a1-source-vertical-design-20261016.md` | `f644739f1b52263b542868f988f1189f46681752` | `79a4573c629addccd7ca989b4387e2c5168405b222db475d459914f4a8f6fa64` | 62771 | 423 |
| `specs/1536-complete-wound-materialization/recovery/t197-depc-a1-source-vertical-design-20261016.json` | `2b243026bc850fb7d986623522cbe33e7183ed62` | `80d578549d7bb1d9f175244769d7475afce4ddc9ae38d1f06cb2d39ec540fcef` | 92481 | 1373 |
| `specs/1536-complete-wound-materialization/recovery/t197-depc-a1-source-vertical-independent-review-20261016.md` | `7b638dc523a454dd40bb82a009a76ade25434371` | `1d56a1548c083dd6929aa31446781d3025599cf3a7708305ba4ad1c61ae14814` | 16651 | 63 |
| `specs/1536-complete-wound-materialization/recovery/t197-depc-a1-source-vertical-independent-review-20261016.json` | `dc1e4b1a0080407b82694f14dc997ff726be7f9b` | `8021d3b95cc6f9818713604211c2c00b86fbf383a2ae8718fbef3bb3f958fead` | 79689 | 2115 |
| `specs/1536-complete-wound-materialization/recovery/t197-depc-a4-targeted-design-20261016.md` | `183c079de96d8939758371b900efaadd76470b04` | `ec43864fec0f3800980989366744e4ca5a275d4c10374c9644d1f85c6a97cdcd` | 69264 | 271 |
| `specs/1536-complete-wound-materialization/recovery/t197-depc-a4-targeted-design-20261016.json` | `e077ba66e9332e70fa29ac351d33357d468de3aa` | `25cef182df647d50e403714562b5c159018ac9a352598213cacd4ce3e7f5bfad` | 84819 | 653 |
| `specs/1536-complete-wound-materialization/recovery/t197-depc-a4-targeted-independent-review-20261016.md` | `2775edb6e4352df4b956f74f4570e899a8405324` | `693d1987affa688ac284b60320e1b2ea48544b0981ac3b7a21c58d2f58ac4a34` | 19051 | 58 |
| `BookOfEternityClient/Program.cs` | `4e10660f99b759b9454c4f59c5e643cbef7b0319` | `f6d7cf1a39f22717cf4da64becf7dd73f78deb2a50ed85203160e586152fd8b3` | 16911 | 378 |
| `BookOfEternityClient/BookOfEternityClient.csproj` | `e4f778864060557b6a9c783238c271fa88b63645` | `b1bafa2fffd26263bc821cd668f20d199a2d08a1a6ed1a4f32c69b95a893d59e` | 5795 | 95 |
| `BookOfEternityClient/Core/GameEngine.cs` | `bfb2fa7d29bdf42e794930fc5ae26e0cc384c3ca` | `28761cafd0394e222e18f194a44907e6b7102ee3e4e18198ec26cd3397814890` | 10951 | 244 |
| `BookOfEternityClient/Core/GameEngine/GameEngine.TurnLifecycle.cs` | `2c017c657d4d3d36be146d052c8a158e42d163c0` | `e2ea305eee08627d224e91fbf10ea274fafb0197cc0c6e54ed03fc5e7db4190a` | 294934 | 5166 |
| `BookOfEternityClient/Core/GameEngine/GameEngine.MortalJointStoryTurn.cs` | `7f054ea23df4b5156635d9155dd176cdc86a0341` | `bf4a5c905760308cd9bad2579c3eddd3a7320626242acc1d3a299ff5c6b3b5f9` | 10353 | 132 |
| `BookOfEternityClient/Core/GameEngine/GameEngine.ValidationAndRepair.cs` | `8a6f52a75bcb0e92d55cb896f9281aed3a82b6db` | `29b26ed98b3d732cb463c218a464487ba548afc7aaaed9b32031c96d4cf20bb7` | 503876 | 9016 |
| `BookOfEternityClient/Core/GameEngine/GameEngine.AgentConsole.cs` | `2548c6fb821a60a0230cc035a48f6c0307a31e20` | `4e248b04c73731e2cc0c5c4b35e69d1bdbd141a6bace970cabba7e64589b0f70` | 22014 | 557 |
| `BookOfEternityClient/Core/StateManager.cs` | `e61846fee2b796d41dd70048cd5de295bfc88acc` | `74742adb8c0adc70e13a61bc375d3fe74c6ce2c28666c927f875b4123f15437e` | 28110 | 714 |
| `BookOfEternityClient/Core/StateManager.LocalSettings.cs` | `111ff18c46a5d0613db57545f191896ca4f4e710` | `4c8b6defec4d8ba56be1e78769c72da9706ef6fdf437f5d44adfe469dd782403` | 2017 | 49 |
| `BookOfEternityClient/Core/FileSystemManager.cs` | `762505d7fbfbd14e4f8e55a7d512fcd35d1f3245` | `14dcb900d73951bf5228d474255c197324ddb32df74a075ce22f4dec64600ec9` | 241343 | 6231 |
| `BookOfEternityClient/Core/FileSystemManager.MainRunFence.cs` | `3f892cc44d59d81d49d1cba3b4f8199c5d92d33a` | `384f9f65d33e1c70c6f7e337d96aec6e71664d142612546139345bf274923a14` | 20796 | 316 |
| `BookOfEternityClient/Core/FileSystemManager.TrustedLocalStorage.cs` | `22d07c130ea56b5cab5ff3bbf04c2653c8ff0305` | `7154852b8ef29afa1262cef0e4bb3a0ff28f14afeebe98c2a0834676e4c5cf5b` | 19220 | 350 |
| `BookOfEternityClient/Core/TrustedLocalFilePublication.cs` | `99081f43140f8f59511d26666e19a6fa57544983` | `b0893b8b12f46f9c76e8b49b764fbac7343e69daea5c058dccd7c70ce570987f` | 33582 | 642 |
| `BookOfEternityClient/Core/SessionOperationContext.cs` | `aa34f9fa8581384a007c1d73b33d927fdabecdd6` | `5b0b98825d0e4f0c6e91a3396b3e54acdda7e774f6afacfcf3fb49b18c155efa` | 23072 | 524 |
| `BookOfEternityClient/Core/CanonicalRootIdentity.cs` | `7152787bd91c20630deda394d7a32f52f6a82867` | `d6e1bc480d50227ce43d1703e48fee9cf19bb45d859f5bd937a2a3432751d928` | 4859 | 150 |
| `BookOfEternityClient/Core/IConsoleInputSource.cs` | `9322a6911c9504c773559d9b668c9d47e3bae57d` | `eeb8876078e8612e4c26c5eb03bd4ff2ed9790617334bae29db6f82c916b2969` | 782 | 32 |
| `BookOfEternityClient/Services/StoryService.cs` | `ba6d79e87c0e00471cb71112980b980c404ebdef` | `08ffacbe1c85b27116aa1886b025c4a495c0cf6f198089f12505a66a566b3380` | 14974 | 346 |
| `BookOfEternityClient/Services/StoryService.MortalJointRepair.cs` | `1c3c29e4d43cfc47a82b495d72986e934b8e438c` | `ca4a34edb095b13d77ccfd5554b93b593cda2d428d27e3b92f40149c1f74340a` | 22909 | 319 |
| `BookOfEternityClient/Services/MortalQteDecisionPublication.MortalJointProgression.cs` | `7cbb6d25f243e66e32020b13eeb6a2fed2a20bce` | `3b96155adc121ec67ab42b2463cd7c6b4d71e321265b71348d6153273edcfd92` | 19147 | 258 |
| `BookOfEternityClient/Services/PendingTurnSnapshotAuthority.cs` | `19b481dd1880075598ddc41eb95bbc9c44968f79` | `f36c095de1746da11ba50dc0296cae9b238c3a11ad1b40e144c4dc0212267b13` | 41585 | 1054 |
| `BookOfEternityClient/Services/AudioService.cs` | `d4695f126952f31a2695c514ae48a5c9594b397a` | `2236f56e7f3c6b626d249a480eb6065abe8fe4dbb311386606dbe40949d3f67c` | 14340 | 362 |
| `BookOfEternityClient/Services/ImageService.cs` | `d6665a14395cdbcf4202d263e5245eec50b4bc40` | `152f7ae6afc901f74cbdfad26221fd9ad79e0275f783795650b390cefbf744a9` | 40280 | 981 |
| `BookOfEternityClient/Services/GmRuntime/GmSessionRunCoordinator.cs` | `7df60d8902617d71daaa28479c4c79a37744d67b` | `eeb23241dff44ce49a484319a76e493d607de4413e1355f9b853ed41c6c7b0b6` | 24413 | 360 |
| `BookOfEternityClient/Services/GmRuntime/GmSessionRunCoordinator.PipePins.cs` | `08ecdab49b89cf0c36cf6cf391c17a46a4db9b9e` | `2473c1f0b83bf142f34ec76ea613127ee8d3ef706c339192e28d9bb907ccad1d` | 5388 | 97 |
| `BookOfEternityClient/Services/GmRuntime/GmSessionRunAdmission.cs` | `aaa6433bb7ca0caa687f66d660be85ff82f83983` | `aa1338fbfa3ba4b8a89851acaa41b4a5bfa3b95c8cc3239116fc24057add621a` | 4093 | 56 |
| `BookOfEternityClient/Services/GmRuntime/GmMainOperationClient.cs` | `3f6d32096396ef7841074660f5774cabfb45ad59` | `b3e58253d7e7b285fa8dad42775ee230a17cd3875c34891da6f52bfd3aded2c3` | 6978 | 90 |
| `BookOfEternityClient/Services/GmRuntime/GmMainOperationProtocol.cs` | `a54deb686b6a85de26f37821fdf67456c2bffd30` | `199378d4f0477a2cfdd7e7afc6745ffdebe58935f49b5169342e0e58d0b9b2c9` | 2993 | 55 |
| `BookOfEternityClient/Services/GmRuntime/GmMainParticipatingControl.cs` | `527dd1f4f99f16faef164d0e5ee91832636632a1` | `223ff5442373639857570fa6a86b3c0b9a85e3ffd51697699c19afb171045202` | 10981 | 186 |
| `BookOfEternityClient/Services/GmRuntime/GmMainParticipatingControl.Daemon.cs` | `a09b793f325a6be828ef241ea74c6b358a62c49f` | `4488491ac2911d8b4c49347d10760106fc72e9c37f9a0169aa4d37f303cea349` | 9926 | 146 |
| `BookOfEternityClient/Services/GmRuntime/OwnedTerminalSessionFactory.cs` | `65b0a9d5fda59a86213a7805968eea1b53db7445` | `524f05673fd4b4f890896e43c06ed46fb11170e5112abcb214e1acecaeae07df` | 4982 | 75 |
| `BookOfEternityClient/Services/GmRuntime/LinuxOwnedTerminalSession.cs` | `5a46c4fe05f2392ee34f93ce392e36819c6aacd3` | `69b9943d4e3ce1e6b271508019936c4849a7585cb03391c06116d8bc4c1f5d60` | 4102 | 75 |
| `BookOfEternityClient/Services/GmRuntime/IOwnedTerminalSession.cs` | `4e0a4ed2348738b309be07482038fb923bf9ee64` | `d368c7be21cb2edb72935d8a86fb0f25da2f7f714792c66fab4d9b1ff389cbb7` | 901 | 20 |
| `BookOfEternityClient/Services/GmWorkers/NativeLineageOwner.cs` | `768513bf3d4b119f02b06d03fe4f20cfa51f67bd` | `614a0a4549976569ef779465967c2da0ce0c1502d4d44e960f3ab94842f75d7f` | 23738 | 410 |
| `BookOfEternityClient/WebUi/LocalWebUiHost.cs` | `d53830e80bd6352ec43c06e23c06d8509cc5f560` | `71c8972b07408d312e2a478447dd03defca4d617633245e02502b13c79da5df6` | 17677 | 309 |
| `BookOfEternityClient/WebUi/BrowserRequestAdmission.cs` | `e877134aabf7ad99e25ef079e121f4d82316e0ca` | `55c639dc2977647cdb54b922fb1081d9931c33a117fba4a0deab2d674bf76ff7` | 941 | 25 |
| `BookOfEternityClient/WebUi/BrowserGameScreenService.cs` | `a8ab842c2ee40491d1b83ddce327f04ce58bbf83` | `27d85725e3879ac801066e78bf64c6bb21cabccdd764eb58958875c231a677f6` | 32305 | 724 |
| `BookOfEternityClient/WebUi/BrowserPlayerActionService.cs` | `6b8f39d13803a78343efb0d58a8f1b481ee333d7` | `468216f874f91d79004d822ad772dfc3b3c9daaf13d3d6d1178e66e9a42a5783` | 4980 | 127 |
| `BookOfEternityClient/WebUi/BrowserLocalWriteCoordinator.cs` | `91c6e761c290d9feaa97cf1dabcd52eb8785d84c` | `95d98475770283a31e96f7d51aafba3bbfb348d9143f5f61598e1c0327db5450` | 30336 | 651 |
| `BookOfEternityClient/WebUi/ExplorerWebCommandService.cs` | `c886f03457726c47aaa8c5d026e5b81216df5b4d` | `62d4094c28c7ee3d7225da4eab88b1b17dd91084782f40ac920eba6cee50b0f3` | 21298 | 475 |
| `BookOfEternityClient/WebUi/ExplorerWebPromptSessionService.cs` | `7a7a8c5ebc2cde62cb93a0d1ec4f427a7dac5a63` | `1e553ca0367eb9f2556debafcf8944d9bc99aa92aebb546ed57138341a720fca` | 31187 | 760 |
| `BookOfEternityClient/AgentConsole/AgentConsoleApiHost.cs` | `d00eaf3bad6bc8b9f1213dd2dcd11ff516a7f466` | `c279232b0e136c140a5a562e321c8abc965627ea7dcb1e1635d885de9354dde9` | 12309 | 317 |
| `BookOfEternityClient/AgentConsole/AgentConsoleLiveInputSource.cs` | `be1c372f3407aa3f0737aae3fb23e0ace323440c` | `3a1f932bc800a229357bf307fb8e01f0f110f340c6f308f350c4bcf5eb3eca9f` | 42395 | 1096 |
| `BookOfEternityClient/Launcher/bookofeternity.ps1` | `cf04a81c0de629cd345ae0a5956076f12c3db648` | `0e05d358847cefdeaf04da6bafab61a67a1b95118de38fdbb161e122faf29ae4` | 42854 | 1013 |
| `BookOfEternityClient/Launcher/gm_main_operation.ps1` | `a175c87e3d3fd71144c100c7afcb6c9a473c0653` | `9ec146965c358bf28424154c1d9fa1fa5e626cb0b5568911a12b130dfb6ab97d` | 25591 | 409 |
| `BookOfEternityGMBridge/BookOfEternityGMBridge.csproj` | `d4594f6b45086b9b40a805cf7a7a718c3658be41` | `c49d352dbfb3bb464e0a5bd456366222e0abe792b1264d665d60c8d545e487d4` | 470 | 16 |
| `BookOfEternityGMBridge/Program.cs` | `c490bf47a8635cc6eb9d33ae54399ce4091553ed` | `a8b964bb7841fb83a61a48da4e1535b3fc7c3f939b12b82d880cf5fb86623272` | 91410 | 2042 |
| `BookOfEternityGMBridge/BridgeHost.WindowsLaunch.cs` | `6371e66ac4cf20b1d8d46aa1826d7f53b7ab80c0` | `388b54b4714db8f8a7ef5e73a96b73adb52970d05e12be21403060b8e0cfa942` | 4945 | 90 |
| `BookOfEternityGMBridge/BridgeHost.MainOperations.cs` | `34350bd4e07c6a8f69cddcad1730458964009415` | `5092cd0dd68436d7f5338ffdd15cf54bba223696aa94625f519e8dc8e033949c` | 2263 | 30 |
| `BookOfEternityGMBridge/BridgeHost.StatusPublication.cs` | `863a9a85f1831395bd8499bd05d66ce15e5d6732` | `2e6318b45aae98237326879f4d5a29c63714684ebbcff3a4075d5433c86f694c` | 4962 | 116 |
| `BookOfEternityGMBridge/BridgeHost.PromptDispatch.cs` | `a42082ae86d2f1773d157d734471ba1cec7209c7` | `92982796e3573122cbd2b8045835b6090b5564010ebd59043a1fb075a109f55e` | 20668 | 337 |
| `BookOfEternityGMBridge/BridgeHost.DraftObservation.cs` | `5f353743b0bf5a9937654fa961a64dd616b24bcc` | `e0ac9791df84c6ad58cf9a18c1133ba421415b821a2f25f16733a6def4bf8bf1` | 8337 | 119 |
| `BookOfEternityGMBridge/ConPtySession.cs` | `4487a6cf60b4892145410e5da1b5751d67f245f0` | `950937c493c51bbf09d514c22b6dc388ccd8960c2ef365871158ef7e77443b90` | 16505 | 261 |
| `native/linux/boe-lineage-supervisor.c` | `40b7a379056f3aa8cbd3df454f0c8848aa740b85` | `af579f12332914628f7fca4657775478d090b1aceccc2e7c3190c527a2074080` | 24566 | 446 |
| `BookOfEternityGMBridge/BridgeHost.StartupObservation.cs` | `1d9eb8fcb108e43865c0ffd1b8f5d333f03b9ddc` | `e55f5c28bb76504950174e859033a7182472e6900adb4fc51e95495b148a93a5` | 2273 | 54 |
| `BookOfEternityClient/Services/GmRuntime/WindowsStartupObservationProbe.cs` | `54c3f6e35de12b70ff2eec31911e294aba63e354` | `1af28e67d431bf4e13673c44916cb4f8b543b787267ab5d4b8d4180f141f54b1` | 8338 | 174 |
| `BookOfEternityClient/Services/GmRuntime/WindowsStartupObservation.cs` | `0af9a77bc6ec06c5dc024a86b5e7cf4b0294f377` | `f06d6f533b1e237f905e76ac21e3dec01803ff154e669f7250a7b24b5a5cff6a` | 1214 | 25 |
