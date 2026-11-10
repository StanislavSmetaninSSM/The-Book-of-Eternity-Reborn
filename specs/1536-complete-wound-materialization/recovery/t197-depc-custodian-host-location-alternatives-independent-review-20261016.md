# T197 custodian host location alternatives — affected independent DESIGN review

**Accepted for conditional architectural discussion. No confirmed material engineering defect was found. NOT a final host-location selection, complete engineering release, implementation authorization or T197 qualification.** The Client-owned alternative can avoid remoting the game renderer for loss of a detachable GM terminal presentation process while the original Client keeper survives. Client process death is a different failure domain and remains unresolved keeper loss. The proposal makes that difference explicit and does not accept a reduced user outcome.

Actual configured independent reviewer: `gpt-6-astra`, `xhigh`, original separate `fork_turns:none`, confirmed by the coordinator and retained for this review. Backend telemetry: **UNKNOWN**. The author is the separate Astra/High agent. This reviewer did not author the proposal. Governance and prior reports are context, not a new source of user authorization. All new engineering contracts remain UNACCEPTED; all ten future checks remain UNRUN.

## Exact scope and evidence

Candidate `5439a8881281b3bc9fe3ccd8623e1461914482c7`; direct parent `7c9529bb36d1d16db8436c37f7da49a0f412cd80`; author source `80a707a54767f595fc44a3f323f82339382de287`; compared prior source `85464398184f388860921da873bb01440cca777c`. Publication/push/readback is coordinator-reported; no fetch was performed. Observed shared HEAD at report creation: `7942d0842ff8a2746acd499817cde52a0a25180c`, metadata only. Pinned Git blobs define this review, not moving worktree or runtime files.

Semantic scope: the alternative Markdown/JSON pair and T197-only status bullets in plan/tasks. The complete carrier has 12 documentation/evidence paths. Eight F1 Late2 raw/index/inspection artifacts and adjacent F1 status claims are excluded from semantic acceptance. Hashing that full carrier identifies bytes and does not execute or qualify those artifacts.

| Candidate path | Git blob | SHA-256 | Bytes |
|---|---|---|---:|
| `specs/1536-complete-wound-materialization/recovery/t197-depc-custodian-host-location-alternatives-20261016.md` | `e7a4a1ad59a8f8115598db70036ad5313e068dd0` | `c9fb91fe1079557de0e05a4ffa75d5e51533bfc444d7e667f0ddb19fa7a4e0a1` | 29616 |
| `specs/1536-complete-wound-materialization/recovery/t197-depc-custodian-host-location-alternatives-20261016.json` | `a1b7e627dbcc7a6df3f3d4b441df8b28390cff62` | `f60866e399305106b764924bf60830d78a4cf0427a758a2d5492b149acb733e3` | 42973 |
| `specs/1536-complete-wound-materialization/plan.md` | `63f8c1166feaec2eee3db94279e1937bb0f524d0` | `79f92fe1f29cd22e63e1c4d11f58fea8aff07620f80e823b302024c6c1b89273` | 2024234 |
| `specs/1536-complete-wound-materialization/tasks.md` | `c3b2722abbd43edc439d183a3021851c8f3e034f` | `9f6377b068f7c7f8d8b68973419dfad940226f9b08102ac77f11d1127a6adb33` | 1414292 |

Exact ordered diff commands:

```text
git diff --binary --full-index --no-ext-diff 7c9529bb36d1d16db8436c37f7da49a0f412cd80 5439a8881281b3bc9fe3ccd8623e1461914482c7 -- specs/1536-complete-wound-materialization/recovery/t197-depc-custodian-host-location-alternatives-20261016.md specs/1536-complete-wound-materialization/recovery/t197-depc-custodian-host-location-alternatives-20261016.json specs/1536-complete-wound-materialization/plan.md specs/1536-complete-wound-materialization/tasks.md
```

selected_four_path_carrier: SHA-256 `b284634e8baf418feee1582e13eb316cbd499cb2becbfd4b5bee15d0e3c8d47d`, 78102 bytes; diff-check exit 0.

```text
git diff --binary --full-index --no-ext-diff 7c9529bb36d1d16db8436c37f7da49a0f412cd80 5439a8881281b3bc9fe3ccd8623e1461914482c7 --
```

full_twelve_path_identity_only: SHA-256 `7af100de261e30b060afac36ebd87714f3f772acdeafa82e06d10321533b2782`, 6543761 bytes; diff-check exit 0.

The four-path diff includes adjacent excluded F1 metadata in shared plan/tasks files. All 43 source pins independently match blob IDs, SHA-256, byte counts and line counts. Each is identical at the author source, prior source and candidate. All six JSON section bodies match the Markdown; its declared hash/size agrees. L01–L10 are distinct and UNRUN. The four author-listed prior artifacts match both their expected hashes and source blobs. The previous independent hosting report pair matches the reviewer's delivered immutable hashes at this candidate; that report pair was published after the alternative's author source snapshot. Complete pins are in the JSON.

This affected review reuses prior source-identical analysis. It does not claim a repeated complete 70-pin semantic audit, current build/restoration identity, new API/platform research or runtime qualification. An initial report-generation precheck incorrectly expected the earlier independent report to exist already at the author source; it stopped before writing either artifact. The final verification uses the correct publication lineage and delivered hashes. No repository fix or runtime action followed.

## Affected source assessment

**Dependency direction supports the alternative.** GMBridge.csproj:13 references Client; Client.csproj:60–61 grants Bridge internal access. A reverse project reference would create a cycle. The coordinator, Linux terminal/lineage implementation and Windows Job implementation already reside in Client. `ConPtySession.cs:1–8,34–78,89–156,159–261` uses platform/framework facilities and Client runtime/worker types; its private FaultStream and P/Invoke helper belong with the adapter. Moving this coherent unit into the existing lower Client assembly, or a genuinely lower shared library, is a plausible extraction. Preserve suspended creation, original Job assignment, one release, partial owners, IO/close tasks and uncertainty. Relocation itself supplies no original authority or implementation release.

**Keeping the original graph local removes the added game-renderer transport, not the lifetime work.** Client `Program.cs:122–310,334–355` creates/runs the real engine/services; `GameEngine.cs:107–161,187–242` retains them and uses local input/render/audio. Keeping these in a surviving Client is coherent for a separate GM viewer's loss. Constructor effects, blocking input, cancellation/error logging, shutdown and provider/browser state still need the original-hosting contract. Proposal lines 9,24,33–41,48 and 54–58 explicitly distinguish Client survival from Client death. If the required outcome includes surviving death of that game-console process, a distinct keeper or another complete lifetime boundary remains necessary. The recommendation does not decide or narrow that requirement.

**Runtime observation and control must move with custody.** Bridge `Program.cs:529–545` owns the native session, input lifetime, terminal screen, output/authority/root/keyboard/resize tasks and actual retirement bindings; its stop path at596–695 joins genuine status/input/prompt/native work and retains uncertainty. `TerminalScreen.cs:5–28` and `SynchronizedTerminalScreen.cs:24–42` provide owner-bound reliability observations; `BridgeHost.PromptDispatch.cs:7–29` retains actual input and prompt tasks. The candidate correctly places these with the Client owner and leaves only display/input requests in the detachable viewer. Native drain cannot wait for viewer capacity, and readiness cannot come from a viewer screenshot/DTO.

There is also a concrete packaging dependency to retain in the extraction: `BridgeHost.DraftObservation.cs:14–28,48–63` binds a packaged GMBridge apphost, child EDITOR/VISUAL environment, draft directory, endpoint and one-use retained prompt. Moving the runtime requires an exact location/endpoint/execution contract for this helper. This is already within the proposal's open prompt/draft/helper/package work, not a material contradiction. A coordinator-field move or ConPTY-only copy would not satisfy the proposal.

**The parent still needs real local Access and a live pin body.** The proposed ordinary/late wrapper must enter the original coordinator's RunOperationAsync around the existing participating SessionOperation body. Current `GmSessionRunCoordinator.cs:126–130` disposes the pin/access when its awaited body returns. A stored Access reference, one DI root or the same process cannot extend that lifetime. `FileSystemManager.MainRunFence.cs:18–110` can borrow genuine ambient access; an absent local scope follows different admission/pipe behavior. Actual registration must precede asynchronous synchronous prefixes and other effects. The real task/lease/pin lifetime must include required joins and debt disposition. The comparison preserves this requirement and the story-child versus whole-parent boundary. Current-G/Lost/worker/root/metadata guards and the missing private post-seal issuer remain unchanged.

**Helper and load servers must remain with the original owner.** `BridgeHost.MainOperations.cs:5–26` binds an external connection to an actual RemotePin and calls Lost on unclosed transport failure. Keeping that server in the detachable viewer would retain the failure being addressed. Moving its endpoint termination into Client does not prove that already-lost external work completed. `GmLoadSessionOperation.cs:32–75,89–124` retains the original connection and requires IPC outside filesystem scopes; `BridgeHost.LoadSession.cs:8–15,33–125` retains the actual terminal/coordinator and one-use stop/restart decision. The alternative correctly requires either a typed local owner route or independently serviced IPC, avoiding a self-call behind a blocked source lane. Actual old-run stop, installed G, refresh and original fresh-launch decisions remain required. It does not assume one native main for the entire application or replace the engine per request.

**The browser gap is source-backed and expressly open.** `LocalWebUiHost.cs:52–95` builds its web-only services and bootstraps without GameEngine/StoryService. `BrowserPlayerActionService.cs:25,65–66,92–114` writes the queued action through the actual local-write coordinator. `GameEngine.BrowserPlayerInput.cs:42–51,54–66,112–123` selects it through a genuine engine/participating lease; file presence is only a wake hint. The proposed one original consumer/graph and shared-service composition is necessary, not current behavior. Console-plus-web arbitration and standalone noninteractive source-loop entry remain unresolved interfaces. The comparison retains staged/idle/expected-G finalization and the retained-load complete/cancel exception; no ad-hoc engine or DTO becomes an original source owner.

**Platform and failure limits are preserved.** `bookofeternity.ps1:711–716` currently requires a visible foreground bridge and uses its packaged executable. `ProductionMainLaunch.cs:12–25,36–46` still demands configured NativeLineage/OwnedTerminal prerequisites and a one-use original launch. The alternative requires birth in Client, not post-death attachment; Client must be outside the native child scope it may stop. On Windows, original Job/HPCON/process/stream/close and startup-observation owners remain with Client. Ordinary-user launch topology, outer job/session/container policy and actual native exhaustion remain unqualified. No new service, credential, key, listener, OS policy or privilege is selected. No network research or platform probe occurred in this follow-up. Earlier API analysis is reused only within its original limits. Keeper death/cold/reboot/migration/distinct-root recovery stays open in both variants; HOLD is not lawful-death completion.

## Findings and remaining decisions

**Confirmed material engineering defects: none.** The alternative limits its recommendation to conditional review and leaves all complete-engineering prerequisites open. It does not make whole-source coverage or crash-safe retirement an existing capability.

**LA-O1 — nonblocking citation erratum.** Markdown line19 and the matching JSON section cite `Program.cs:336–337` for engine resolution/RunAsync. Those calls are at **334–335** in the pinned source. Line336 is inputSource.AssertCompleted and337 closes the try body. The substantive claim is correct. Use the corrected citation in subsequent explanatory/correction text while preserving immutable author bytes; the architectural verdict is unaffected.

These engineering decisions remain mandatory before release:

| Decision | Exact unresolved contract |
|---|---|
| Failure requirement and location | Record the accepted failure event and explicitly choose placement. This review does not select a mode. Client-process-loss survival cannot be obtained by relabeling viewer loss. |
| Original run lifecycle | Constructor/bootstrap/health/menu/source/load start-stop-replacement state machine, one actual graph, retained tasks through normal shutdown, no recursive main guard or self-IPC deadlock. |
| Coherent extraction | Native adapter/helpers, screen/readiness, pumps, input/prompt/draft/status and helper/load server ownership; actual packaging and endpoint/caller interfaces; pre-effect enrollment and final drains. |
| All-source parent ownership | Exact finite task/writer/debt inventory including external helper/daemon/CLI/worker/browser work and real pin-body/lease lifetime. A child receipt or executable identity does not close it. |
| Seal and recovery | Race ordering, nonsemantic unwind and precise already-armed original-attempt settlement followed by genuine joins/close/retirement. No timeout, guard weakening, pin zeroing or new G substitute. |
| Durability/browser/platform | B03 codec/CAS/ACK/retention and B04 caller/data-retirement rights; one real browser consumer and lanes; platform qualification and missing keeper-loss/cold/distinct-root issuer. |

The smallest useful next DESIGN is a concrete Client-owned composition/start/load/stop contract with an extraction map and exact invocation/lock/lifetime ordering. Keep native pumps and original server handlers serviced independently of the waiting source; include the draft-observer apphost/environment/endpoint and real parent pin-body lifetime. Review it before any implementation release. A lower shared library is an alternative packaging choice, not a reason to add a reverse executable dependency or privileged service.

## Final limits

L01–L10 are UNRUN. DEP-C/I/L, B01/A1/G-C, whole-source/target acceptance, H2/H3/HQ/newQTE remain blocked. D4, guided, visual, Windows and D8 remain open. No automatic F2, life-evaluation suppression, new G, source replay, extra turn/dice/reward, target authority, mode selection or transition activation follows. Separate F1/Pd Late2 raw evidence, later source work and shared restoration generations are outside this verdict.

Only pinned Git/text/hash examination and these two new /tmp reports were used. No repository writes, builds, tests, discovery, application/native/OS execution, network, fetch/push or delegation occurred. Prior artifacts were not edited. **FINALSTOP: no pending writes after delivery.** The JSON carries the Markdown digest and complete pins; the JSON digest is delivered separately to avoid circular hashing.
