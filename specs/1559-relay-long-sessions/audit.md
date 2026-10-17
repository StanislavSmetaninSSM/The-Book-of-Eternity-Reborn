# Historical baseline relay audit — a82 architecture candidate

Source: [#1559](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/1559). Exact baseline `a82b3d8439f76f83baade6df8516e78eea86e546`; read-only source inspection, no relay/game/test execution. Machine-readable file bytes/SHA/blob/mode, packet measurements and category owners are in [source-inventory.json](source-inventory.json).

Current stage disposition is in [plan.md](plan.md): only the inactive two-path lifecycle reservation is accepted, with 47-case GREEN/final EVIDENCE. The full relay backlog remains deferred. This original a82 source audit, measurements and pin inventory are historical; they do not describe later source/test/runtime status or imply full relay implementation.

## Confirmed baseline boundaries

| Owner | Current behavior | Consequence / proposed responsibility |
|---|---|---|
| `relay_cli.py` capture_request | Incrementing ordinal rejects submission >3; startup rejects any nonempty queue | Not a durable long-session protocol; replace with current-generation reopen/reconciliation, not a counter reset |
| relay paste/input | 65,000 characters, 120 lines, 262,144 paste bytes; terminal redraw duplicates full draft | Byte budget needed across transport/render observation; remove arbitrary line/count caps without unbounded buffers |
| relay/worker contract | 1 MiB read/response bound; one-winner response then reply publication | Configured byte admission; exact incomplete-winner recovery, never regenerate an unknown response |
| relay consumer output/errors | 1 MiB bytearray; outer error parks until stop; inner failures may clear active while presenting RELAY ERROR | Spool bounded diagnostics; typed recoverable/unresolved outcomes, explicit safe readiness recovery |
| `relay-apply-response.ps1` | Refuses >32 writes; preflights originals, then sequential independent Write-BoeJson calls, terminal last | Mid-set failure can leave prior writes committed; re-entry fails old target witnesses. A complete authoritative batch is needed |
| daemon New-GmPromptOperation | Full PayloadJson dictionary refuses Count>=256; no retirement in function | Second lifetime ceiling; retire source-completed operations, retain unresolved identities and compact non-replay evidence |
| bridge PromptDispatch | Full-text operation dictionary refuses Count>=256; two admitted prompts; task and CTS retained | Keep two-concurrent resource admission; replace lifetime retention with bounded compact completion/query window and non-replay high-water discipline |
| bridge Program / observation | First general bridge frame read uses int.MaxValue; recent output is rolling 65,536 chars; Mini-only draft admission is 65,536 UTF-8 bytes | Bound general prompt frame before allocation; long neutral-relay observation cannot depend on a truncated diagnostic tail. Mini-specific qualified profile remains separate |
| MainOperationReader/helper transport | 65,536-byte control frames, 32,768-byte chunks; helper request transfer does not have a total length ceiling | Keep bounded control frames/chunks; add justified total transfer/resource admission, not a giant frame |
| GmHelperCanonicalScope / FileSystemManager | One original lease, policy in script, rechecked read/kind/tree witnesses; existing multi-member PublishLocalFilesAsync and TrustedLocalFilePublication Committed/RolledBack/Uncertain outcomes | Reuse these mechanisms and current generation; do not invent a second canonical journal/rollback engine |

`execution.json` currently proves consumer exit and I/O, not client acceptance. `Complete-BoeTurn` creates a correlated terminal signal; game validation/history remains a later client-owned phase. Old M1 PID, input binding, run records, queue hashes or recovered files cannot authorize a replacement process.

## Real data and synthetic distinction

Only already tracked public repository artifacts were read. The accepted historical r3 capture has request 59,172 bytes / 150 lines; prompt 40,139 bytes / one line; response 16,257 bytes / 121 lines / six writes. These are historical runtime artifacts, not proof of the final current schema or arbitrary large turns. A tracked original client repair artifact is 15,683 bytes / 156 lines; the newer wound repair artifact is 5,476 bytes / 73 lines. Their complete source paths and exact byte hashes are retained in the inventory.

Current JSON worked-example extraction (read-only regex + JSON parse, not the native ExampleSnippetExtractor/validator) finds an Actor Profiles object of 26,797 bytes / 605 lines, and a current Wound Materialization object of 10,972 bytes / 272 lines. A documented example is not a complete generated game response. There is no honest measured real >1 MiB response or >32-write accepted turn in this audit. Those are controlled growth tests and later current-chain qualification, not fabricated historical evidence. Defaults in the plan are a bounded proposal to be reviewed and measured, not a speed/production-readiness claim.

## Current no-agent-CLI path

The existing request directory already contains the daemon prompt and game request. An AI with ordinary computer/file read access can consume those bytes without running `relay_worker.py`; that CLI is an optional operator/adapter convenience. Current README primarily documents developer tests/worker commands and does not prove a usable shell-less-agent exchange. The minimal path is a known queue current-request descriptor plus immutable exact files, exposed through the AI's already available computer tools. The AI reads rules/state itself and uses its existing write/publication means; no browsing UI, discovery/collection service, model client or new network endpoint is introduced. A future fixture/actual exchange must demonstrate this path, not merely label another worker CLI run as shell-less.

## Changed-source dependency closure

Primary implementation: the five maintained relay Python/PowerShell files and README/input profile; bridge PromptDispatch/Program and neutral observation; daemon operation retention; existing GM helper script/transport plus GmTurnHelperControl/GmHelperCanonicalScope for authoritative batching. `relay_platform.py` changes only if actual terminal memory/pipe handling requires it. Native Mini/editor-observation behavior is not automatically widened to fit neutral relay prompts.

Test consumers: GmRelayReusable/Transport/Repair tests and their exact Python fixtures; OwnedTerminalScenarioDriver.Relay, ProductionMainLinuxFixture; GmBridgePromptOperationTests and daemon prompt-delivery tests; helper current/read-witness/policy contracts. New tests/categories will own long-session, budget, batch/recovery and shell-less exchange controls. Current #1558 selection is historical, not reused as #1559 selection.

Any daemon/helper change additionally binds real #1557 entrypoint inverse/source/AST guards and current packaged helper consumers. Preserve existing exact inverse semantics through an explicitly inventoried follow-up edit layer; do not shrink a golden hash or erase history. The daemon's current 52 document membership is unchanged. A genuine current packaged writer case is selected if the changed daemon/helper/package or a session-delivered guide requires it; no blanket A255/B78 replay. CLI quickstart is outside that fixed session52 list. No Mortal/afterlife rules or pending-authority semantics are changed; relay transport worked examples belong in the maintained README, with a no-gameplay-doc-change rationale checked at final review.

## Architecture decision to review

Prefer one relay application decision using the existing helper lease and multi-member trusted-local publication, including the terminal signal and a compact client-owned latest apply receipt in the same publication. Queue metadata remains correlation/transport only. Recovery uses the established journal decision and exact receipt/response identity; it does not replay writes because a Python process exited. Script policy/normalization is prepared before the single commit, with a staged read view for terminal checks. Client acceptance/validation remains separate. The precise helper interface and receipt lifetime are review targets in [plan.md](plan.md).

Rejected: simply raising/removing constants (leaves lifetime memory/error/restart/partial-apply defects); a second Python canonical transaction/undo system (duplicates authority and recovery); a new UI/network/context collection product (outside the issue). No implementation is released by this candidate.

## A01 amendment after independent ARCH3930

The independent review confirmed that the two proposed operational control records would currently enter ordinary engine/live-preparation snapshots, then be deleted or rewound during rejection/repair. This is a confirmed design integration defect, not a runtime/test failure. Root released a metadata-only amendment; the original CHANGES_REQUIRED reports remain exact. The amended lifetime is in plan §D and FR006, with [21 newly bound dependencies](source-inventory-a01.json); the original78 source packet and seven public measurements remain unchanged.

Keep the exact proposed paths; do not widen the helper from game_session into .boe_runtime. Exact exclusions/protections must cover engine/live snapshots, save/load ephemeral handling, repair/recoverable FilesModified, both client-owned validation predicates, SpiritualOriginalDraftInputs.IsDraftPathCandidate and helper GM-write/FilesModified denial. The engine selector also serves repair resubmission and browser restored-settlement inventory; ordinary rollback/cleanup algorithms stay unchanged. Existing snapshot cleanup owns only its manifest/authority/subtree. Save/load/new-session generation replacement invalidates operational authority; ordinary rejected-turn rollback preserves it. Raw missing/corrupt records are not proof that application did not happen.

Required new current-path controls retain latest records both when first created after snapshot and when changed from an earlier baseline, while restoring/removing ordinary neighbors. Rejected repair/lost ACK and same-live-binding restart must reconcile without replay; real save/load invalidates old generation. Existing exact snapshot/preparation/save and frozen-draft neighbors are listed by method and current owner, without broad storage/spiritual cohorts. C01–C06 from the exact ARCH report remain later SOURCE/EVIDENCE gates, including both authoritative readers, before-image/journal/scratch budget accounting, CRLF/final-newline/128-row/65536-column neutral framing and an optional descriptor over actual existing agent tools. No native validation or G1 release is implied.

Current gate: A01 architecture closed at25713634 for G1 tests-only, with exact reports/Root release persisted. Actual SaveLoadService.Loading.cs caller is separately bound;100 total distinct baseline sources. Historical3930 CHANGES_REQUIRED and all C01–C06 conditions are retained. No runtime proof is implied.
