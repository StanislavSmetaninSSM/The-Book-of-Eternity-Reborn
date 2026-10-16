# T197 pending-fate original frame — independent DESIGN review

Candidate `e6796dc3b5454cceb1591ccc634b54dbb1021bd6`; direct parent `a85a726229e74465f986af69ca00a8e0225c0dea`. Source `ec50e4cd6c6e17ab651711eaf0c9cbc957dd8842`; comparison `f3bef7097660b12317842903f0849af43b29c06c`.

**Verdict: the hot proposal is reviewable, but the exact D01–D04 consumer contract requires PF-R1 correction and is NOT accepted. One material eligibility-contract ambiguity; no other confirmed material defect. Complete engineering/runtime/activation release remains blocked.**

Reviewer: independently retained GPT-6 Astra / XHigh, original separate fork none; actual configuration is supported by the parent-confirmed spawn invocation. Backend telemetry UNKNOWN. Author: separate Astra / High. No repository writes, code changes, runtime/native execution, tests/build/discovery, network/fetch/push, OS changes or delegation.

## Required finding

**PF-R1 — Define actual source-family eligibility at the shared ordinary Stage call; browser is not synonymous with recorder/recovery.**

Paragraph23 groups browser/recorder/legacy recovery as an alternative to the ordinary family, while paragraph28 replaces the shared call for both genuine ordinary modes. The scope exclusions separately name browser paid fate transactions but do not clearly exclude ordinary browser Stage. Thus the proposed factory/refusal contract does not unambiguously distinguish an ordinary browser invocation from actual recorder/recovery ownership. This is an eligibility-contract defect/ambiguity in the proposed design, not proof of an existing runtime regression.

Location: author MD23 and MD28; corresponding JSON construction/integration sections and Created-state family guard.

- `T:1507-1519: genuine ordinary browser branch calls StagePlayerTurnAsync inside participating session`
- `T:1564-1567: browser establishes its actual original operation scope/claim`
- `T:1594-1596: browser action ID is retained, then same fate attachment executes without a console-only condition`
- `T:4534-4539: the shared helper uses the public no-lease GetOrCreate`
- `BO:49-95,198-205: BrowserOriginalOperationScope retains/validates real MainAdmission and installs its own ambient scope; this constructor is not a MutationIntentRecorder or legacy-storage mode`
- `L:92-109: trusted-local family is rejected by actual MutationIntentRecorder/IsLegacyStorageRecovery and exact artifact rules, not by a browser UI flag`

**Scenario and consequence.** A genuine admitted ordinary browser action reaches shared Stage1596 with its actual original browser binding and an ordinary trusted-local pending path. A literal grouped browser-refusal implementation rejects it solely for being browser, even though it is not a recorder or legacy family. Stage1674-1681 then has an evidence-retaining browser failure path; a refusal would not be the requested successful same-situation continuation. A supposedly closed D01-D04 consumer boundary could silently narrow console/browser parity or leave the shared integration without a defined eligible route. Full browser-host qualification being open does not make browser and recorder equivalent.

**Required bounded correction.**

1. State the actual family predicate in terms of original live owner/parent/root/G and real lease/path family, retaining any actual BrowserOriginalOperationScope/action binding as an additional obligation; a UI/browser flag grants or denies no storage family by itself.
2. Distinguish ordinary browser Stage from the separate paid browser Reveal/Rewrite atomic owner, actual MutationIntentRecorder and legacy-recovery families. Refuse genuinely unsupported families before this adapter effects, without replay, mode-switch or invented recovery.
3. Alternatively expressly scope this consumer proposal to console only and qualify the shared1596 integration accordingly; leave ordinary browser integration OPEN and preserve its actual path. Such a documentation scope choice cannot claim global parity completion or authorize runtime behavior changes.
4. Add a future eligibility case: genuine admitted ordinary browser binding versus a bare browser flag/DTO and actual recorder/legacy families. This is an UNRUN contract case, not a request for runtime now.

This correction does not require implementing full browser hosting or closing PF-P01/PF-P07. It makes this proposed consumer boundary exact. Existing proposed files should remain immutable; use a new correction pair or explicit immutable amendment.

**PF-O1, optional:** MD28/JSON names `ProcessPlayerTurnAsync1497`; the actual declaration is `ProcessPlayerTurn` at T:1484. The delegate line is correct. Fix the label alongside the required clarification; it does not alter behavior.

## What the proposal establishes conditionally

**PF-C01 — Actual fate behavior and separate action owners.** Accurate. Reuse predicate remains permissive; null array throws outside parse catch. Valid existing state preserves lock/timestamp/gacha fields and references, with no application write/RNG. Fresh success is20 visible+4 hidden Fill calls with the original modulo/threshold/emptyDiceUsed behavior. Reveal/Rewrite/Rotate and fee owners are separate. Explorer can call GetOrCreate before fee deduction; the proposal does not authorize replaying those actions. Sources: `D:29-49`, `D:65-122`, `D:124-213`, `G:36-90`, `T:4534-4539`, `T:1866-1873`, `EXP:1162-1170`, `EXP:1217-1235`, `WEB:2020-2054`, `WEB:2096-2132`.

**PF-C02 — Same final read witness rather than reread reconstruction.** Accurate proposed seam. Existing public text read recovers, decodes a selected byte snapshot, calls after-read hook and may reacquire/reread. A second independent read or reserialization is not the original byte witness. The additive seam must retain the actual final selected snapshot and the exact decoded text while preserving the same follow-up behavior. PF-P02/P03 correctly remain unqualified; no completed-read claim is permitted at a cut where an original follow-up remains unissued. Sources: `FS:1319-1352`, `READ:45-139`, `FS:1442-1459`, `FS:1529-1545`.

**PF-C03 — Per-slot entropy, clock and seal safety.** Conditionally coherent. Gate then frame-state lock, no RNG/I/O/clock/await under locks; once-only four-byte slot registration and retained returned value before borrow release. Seal allows the already-armed slot to finish, forbids the next unissued slot, and cannot classify a changed failed buffer as a draw. Materialized creation/write clock results and desired bytes are retained once. Discarded constructor-clock defaults are acknowledged as technical work, not omitted domain timestamps. Actual registration/supervisor APIs and surviving thread/task custody are prerequisites, not demonstrated source capabilities. Sources: `G:36-51`, `D:124-137`, `D:169-213`, `proposal MD38-58`, `proposal R05-R10`.

**PF-C04 — Source before-image and publication outcomes.** Accurate distinction. Current ordinary publication rereads its before image; proposed exact earlier-read baseline is an explicit additive guard, not a claimed existing guarantee. Native publisher compares expected before bytes and retains Committed before cleanup. Directory preparation, mutation hooks, native attempts, retry timers and release are distinct work. Frame source admission cannot substitute for native-attempt/debt authority. Committed plus cleanup/release failure cannot be labeled wholly settled; a before mismatch must preserve the actual reported failure/disposition, not manufacture RolledBack. Sources: `FS:840-883`, `L:92-109`, `L:169-268`, `L:317-332`, `PUB:165-198`, `PUB:245-326`, `REL:103-122`.

**PF-C05 — Actual parent and held propagation.** Private Ready/HeldAtCut/Faulted is appropriately distinct from null, Cancelled, EOF and generic Exception. Shared Stage must branch before preview and must not inject held into existing cleanup catches. Local Ready/Attached does not close the main operation or release a Lost pin. Actual O/I/main/access and join lifetime remain required. No fake ClosedObserved, timeout or metadata fallback is supplied. Sources: `T:1561-1606`, `T:1668-1742`, `CTX:93-155`, `MAIN:19-90`, `COORD:96-154`, `PINS:44-50`, `proposal MD66-74`.

**PF-C06 — Liveness, host choice and recoverability limit.** Correctly bounded. Client custodian is a conditional hot precondition, not a globally accepted host-mode choice. Original keeper/thread/native owners must survive the same GM-presentation-loss event. Before persisted after-image evidence, a dead keeper loses original entropy; hashes, PID, file absence and new processes cannot restore it. Journal bytes are not a stop/continuation issuer. PF-P05 explicitly lacks a lawful same-action continuation admission, so no successful hot recovery or permanent-HOLD resolution is claimed. Sources: `proposal MD11-17`, `proposal MD74-82`, `proposal PF-P01`, `proposal PF-P05`, `proposal PF-P06`.

The distinction matters: no claim here establishes a current hot or cold recovery route. PF-P01–PF-P07 are real missing owning contracts, explicitly disclosed by the author. They are not additional findings merely because implementation is absent. A fixed browser predicate would make the consumer boundary reviewable without granting any currently missing parent, native or continuation capability.

## Open owners and next concrete work

| Prerequisite | Owning contract still required | Limitation |
|---|---|---|
| PF-P01 — Original host and parent issuer | Actual Client-born graph/real coordinator pin/O+I/G/root; source gate and task registration before synchronous prefix; live retained record through bridge loss. | Missing proposed host/parent APIs, not supplied by a pending DTO. Qualified survival prerequisite; keeper death is outside hot contract. |
| PF-P02 — Same final FS read observation | ReadOriginalFateInputAsync retains the final existing read snapshot/text, recovery/hook/reread semantics, absence/error distinctions, before returning to parser. | Additive source specialization; no new gameplay authority. Exact implementation and read-debt behavior unqualified. |
| PF-P03 — Original storage/native/IO settlement | Pre-effect directory/callback/native registration, pending acquisition, no new retries after seal, actual outcome capture before facade, cleanup/release joins with original lease. | Lower native/full writer contracts remain OPEN. Read operations can recover; a registered composite call alone does not authorize later effects. |
| PF-P04 — Pending fate writer exclusion / baseline | Actual existing Reveal/Rewrite/Rotate/Clear, browser, restore/load/raw writers cannot mutate the same source while retained Stage uses it; exact-before rejects observed drift. | Actual service has no such frame/path reservation. Whole writer inventory remains OPEN; do not infer from main pin or existing canonical read alone. |
| PF-P05 — Lawful same-action continuation issuer | Live original frame can resume only remaining unissued cuts under proper sameG/root source continuation admission, exactly one executor, no sealed-old-pin reuse. | Consumer conditions specified; issuer/admission protocol still OPEN. This is the material liveness gap, not permission for permanent HOLD. |
| PF-P06 — Cold/keeper loss and durable lineage | Original work stopped + exact root/G/operation/debts and retained source lineage after keeper death/reboot/migration/restore. | Not solved by this hot-only adapter or stored dice bytes. Full G-C/T197 remains blocked. |
| PF-P07 — Transitive Stage/caller held integration | Actual Stage/participating wrapper catches/finally and upstream MainMenu/Explorer effects preserve typed held and real error results; local receipt cannot certify whole operation. | Full H-B02 remains OPEN; health/reminder/snapshot/cleanup and other helpers need their own exact adapters. |

Correct PF-R1 in the finite consumer scope/eligibility and add its future case; correct PF-O1 alongside it.

After that correction, the critical next owning design is **PF-P05**, coordinated with PF-P01/P03/P04/P07. Its positive continuation contract must:

1. Name the genuine surviving parent issuer and exactly which original operation/task/pin/admission stays retained through stop/storage/metadata handoff.
2. Define transition from sealed execution to exactly one lawful continuation executor without reusing a closed/sealed admission, creating a fresh coordinator/G, reconstructing from metadata, or reissuing the action.
3. Carry original state/cut, successful slot prefix, original creation/write clocks, publication/debt outcomes and exact baseline/path reservation into the next unissued phase.
4. Specify a positive finite ordinary same-situation continuation and actual fault disposition; no timeout, permanent HOLD or caller reoffer as completion.
5. If existing native/main guard semantics cannot supply the contract, keep this path blocked and refer the exact genuine new lifecycle authority decision to the coordinator. Routine path/read extraction alone does not require a new principled grant.

Do not implement the current proposal. A corrected application consumer review does not qualify its issuer, native debt, browser integration or full G-C/T197.

The18-state/17-race model is coherent as a conditional safety model: it retains original values, keeps registration distinct from permission for later effects, separates application progress from actual debt joins, and preserves actual faults. The22 future cases remain UNRUN and need the narrow eligibility case above. They do not prove eventual completion, real native storage exhaustion, unchanged platform behavior, or a lawful continuation issuer. A thread still executing Fill, an unfinished read hook/follow-up, or unresolved cleanup stays outstanding; timeout or an empty application task count cannot replace actual joins.

## Exact scope, artifacts and diffs

Semantic review covers the frozen author pair and T197 status in plan/tasks, plus source needed for its actual consumer claims. Other F1/Late2 evidence in this carrier and later metadata are excluded. Existing prior reports remain immutable and retain their earlier limited verdicts. Whole-file hashes establish identity, not a repeated whole-file or full-history semantic audit.

| Candidate document | Git blob | SHA-256 | Bytes |
|---|---|---|---:|
| `specs/1536-complete-wound-materialization/recovery/t197-stage-pending-fate-original-frame-design-20261016.md` | `50d008cc1a17412c548c6b3c9f94421ed5386268` | `57847988664ad455786d422d59e4e4e81168f82ff0b455c04da9fce112fb8797` | 43410 |
| `specs/1536-complete-wound-materialization/recovery/t197-stage-pending-fate-original-frame-design-20261016.json` | `34a815e0affa2064c7aed0428bc74ca0a319a34d` | `113bcc146618672647e5e64b44129b14c3da727abd818ee3d4f906192a173133` | 129565 |
| `specs/1536-complete-wound-materialization/plan.md` | `d9313b01fb4496873bab5c9b80372079bc57c3e5` | `be9d0ffb32ec1f04438c239870802ac2ca4199c20e21d0b68e6befb24644ea94` | 2071686 |
| `specs/1536-complete-wound-materialization/tasks.md` | `0d9b88ed60a9583dbaddc2c2e68c448e23a116ff` | `7460fe4984067234486dc8a3d677699b4e7e7d3876b2b1ae4deffe092ca45b86` | 1450149 |

Diff SHA-256 values cover exact unnormalized stdout of the following Git argv. All three static diff-check invocations returned0.

- **author_pair** — `6e8d14589cb593c7671ca2ef0931a64090b51cd0ee6feb2c8a18b581a7aec267`, 175089 bytes. Whole new author pair reviewed semantically.
```json
["git", "diff", "--binary", "--full-index", "--no-ext-diff", "--no-renames", "a85a726229e74465f986af69ca00a8e0225c0dea", "e6796dc3b5454cceb1591ccc634b54dbb1021bd6", "--", "specs/1536-complete-wound-materialization/recovery/t197-stage-pending-fate-original-frame-design-20261016.md", "specs/1536-complete-wound-materialization/recovery/t197-stage-pending-fate-original-frame-design-20261016.json"]
```

- **selected_four** — `65e46e7f91ca9a6f12d5a6f450e2c9b575b10b61c7f04f3716a1948ac22c98ba`, 180527 bytes. Full four-path identity diff; only T197 status statements in plan/tasks reviewed, adjacent F1 evidence statements excluded.
```json
["git", "diff", "--binary", "--full-index", "--no-ext-diff", "--no-renames", "a85a726229e74465f986af69ca00a8e0225c0dea", "e6796dc3b5454cceb1591ccc634b54dbb1021bd6", "--", "specs/1536-complete-wound-materialization/recovery/t197-stage-pending-fate-original-frame-design-20261016.md", "specs/1536-complete-wound-materialization/recovery/t197-stage-pending-fate-original-frame-design-20261016.json", "specs/1536-complete-wound-materialization/plan.md", "specs/1536-complete-wound-materialization/tasks.md"]
```

- **full_carrier** — `d8b5b679395196d31efe8cbb72e64746d15cb0370dd8fef92757cb13a35fa924`, 380927 bytes. Five-path carrier identity only. Separate t179pd admission-fixture selected-recovery evidence excluded from semantic review.
```json
["git", "diff", "--binary", "--full-index", "--no-ext-diff", "--no-renames", "a85a726229e74465f986af69ca00a8e0225c0dea", "e6796dc3b5454cceb1591ccc634b54dbb1021bd6", "--"]
```

Observed HEAD `ad5859121253d7972dca51ced6f77927e9e953b4` is metadata continuity only. The frozen author pair, all16 source blobs and the additional browser-owner blob remained byte-identical at that observation. This is not acceptance of later F1 evidence or any moving code. Remote readback was supplied by the parent; this reviewer performed no network call.

## Source and evidence pins

Independently verified16 historical source pins against Git blob ID/SHA-256/byte length and equality with comparison/candidate,26 exact source text projections,4 immutable prior author artifacts, and the prior independent inventory review. One additional source file resolves the affected browser ownership claim. Governance/context pins and full projection metadata are in JSON.

| Alias | Historical source path | Git blob | SHA-256 | Bytes |
|---|---|---|---|---:|
| D | `BookOfEternityClient/Services/PendingTurnStateService.cs` | `74dfbbcecfbae0d4671dbfe28078c18d5f743b38` | `ae7afcfddf00b69e4a6dc77fedf456f20213ed9b4436e6391c8a74ee03b584d5` | 6769 |
| G | `BookOfEternityClient/Core/GameLoop.cs` | `c89bb3098f4b4c00b8f683047f9b0ecd1699dc3e` | `2b65cc471048f7f68252e1262a6fe5ac62b142f21c61bae9c594c5da5e3ea90d` | 2673 |
| T | `BookOfEternityClient/Core/GameEngine/GameEngine.TurnLifecycle.cs` | `2c017c657d4d3d36be146d052c8a158e42d163c0` | `e2ea305eee08627d224e91fbf10ea274fafb0197cc0c6e54ed03fc5e7db4190a` | 294934 |
| REQ | `BookOfEternityClient/Models/TurnRequest.cs` | `16eee09d71281d7f3fc2118c13d436178780eb57` | `29b21bc0a98bb65fe9139bd835452c614093f04134b67c5911da33806d73c733` | 10128 |
| JSON | `BookOfEternityClient/Configuration/SharedJsonOptions.cs` | `abe523ae63d23e238060f27b4a6fac2062b6c6ba` | `af3a89a08074879de8f9cca1e1f94bccadfb944d4d75b8faa861ed5a0d2562e2` | 607 |
| FS | `BookOfEternityClient/Core/FileSystemManager.cs` | `e6f7852156ca2a9c1f77fcb961f71068562d5257` | `c134530a5ef7d384146c4fac263dc96ba7bde4e83e79673027a2cad9387a514f` | 241886 |
| READ | `BookOfEternityClient/Core/FileSystemManager.OrdinaryReads.cs` | `022181b24c6e33e1be5d79cb6f705808a87c595a` | `094d4d2f2c2cf7abbf959165c9f2722f4057902f9ec0f78edc4b9ed9bc87a148` | 13822 |
| L | `BookOfEternityClient/Core/FileSystemManager.TrustedLocalStorage.cs` | `22d07c130ea56b5cab5ff3bbf04c2653c8ff0305` | `7154852b8ef29afa1262cef0e4bb3a0ff28f14afeebe98c2a0834676e4c5cf5b` | 19220 |
| PUB | `BookOfEternityClient/Core/TrustedLocalFilePublication.cs` | `99081f43140f8f59511d26666e19a6fa57544983` | `b0893b8b12f46f9c76e8b49b764fbac7343e69daea5c058dccd7c70ce570987f` | 33582 |
| REL | `BookOfEternityClient/Services/CoordinatedStateWriteHelper.cs` | `f8accbc3e70feb6e9de4a5bc3337b68e11ba2d14` | `5b79e2b7d42839ff6dadc0a67040e40ac4b2bf0a18fd589d2b66ffd22cff97b4` | 14216 |
| CTX | `BookOfEternityClient/Core/SessionOperationContext.cs` | `aa34f9fa8581384a007c1d73b33d927fdabecdd6` | `5b0b98825d0e4f0c6e91a3396b3e54acdda7e774f6afacfcf3fb49b18c155efa` | 23072 |
| MAIN | `BookOfEternityClient/Core/FileSystemManager.MainRunFence.cs` | `3f892cc44d59d81d49d1cba3b4f8199c5d92d33a` | `384f9f65d33e1c70c6f7e337d96aec6e71664d142612546139345bf274923a14` | 20796 |
| COORD | `BookOfEternityClient/Services/GmRuntime/GmSessionRunCoordinator.cs` | `7df60d8902617d71daaa28479c4c79a37744d67b` | `eeb23241dff44ce49a484319a76e493d607de4413e1355f9b853ed41c6c7b0b6` | 24413 |
| PINS | `BookOfEternityClient/Services/GmRuntime/GmSessionRunCoordinator.PipePins.cs` | `08ecdab49b89cf0c36cf6cf391c17a46a4db9b9e` | `2473c1f0b83bf142f34ec76ea613127ee8d3ef706c339192e28d9bb907ccad1d` | 5388 |
| EXP | `BookOfEternityClient/UI/ExplorerMode/ExplorerMode.Afterlife.InkFeathersAndOfferings.cs` | `e173e90939701f39d927f65d2d583555bb61615f` | `dae8ca299532f3b5db0277e7017be2dfc3ab38c4182c8665e4a25a5654648257` | 155530 |
| WEB | `BookOfEternityClient/WebUi/BrowserAfterlifeWriteService.cs` | `c3d4a605c35a68200290e6a1335066f17350de83` | `f3ed3ac34ec28d201c1da63b1316496734d23e3b6d9befd075b1dbe771d9fb1f` | 241907 |
| BO | `BookOfEternityClient/Core/FileSystemManager.BrowserOriginalAdmission.cs` | `50d49a20cdf7305d04412b35f25998f01e8448f4` | `1decb1beb44d2fd0466199e58ff06fb6b39e7795341493fe8da09a977ec8d202` | 29382 |

The new observation/adapter methods are proposed private interfaces, not existing capabilities. Ordinary exact-path D11 specialization is not automatically a new principled grant; neither an absent helper nor this report grants source/lifecycle/target authority. The proposed Client custodian remains a conditional choice within this hot subcontract, not a globally selected T197 hosting mode.

## Release limits and final stop

All new APIs remain UNACCEPTED. Stage/H-B02, full parent, full G-C, DEP-C/I/L, full target, H2/H3/new joint QTE, actual native/platform qualification and keeper-loss/cold/reboot/migration/distinct-root remain open or blocked. D4, guidedUS5, actual DOM/visual, Windows, D8 and console/browser parity obligations remain. No F2, automatic life-evaluation turn suppression, new OS/security/service/endpoint/key/network grant or target write authority follows. Separate F1 evidence is not evidence for this T197 design.

Accepted action/turn/lifecycle consequences remain accepted. No extra dice, turn, fee, reward, source replay or same-action reoffer is an authorized recovery. Permanent HOLD remains a blocker, not an ordinary resolution. The candidate accurately labels its proposal UNREVIEWED and all seven prerequisites open; the next status must record PF-R1 correction required, not consumer or engineering acceptance.

FINALSTOP. Only this new /tmp Markdown/JSON pair was written. No pending writes after delivery. The JSON records the final Markdown hash; both artifact hashes are supplied in the final response without a circular self-hash.
