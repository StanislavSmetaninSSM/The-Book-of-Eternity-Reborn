# Feature Specification: Long-lived GM relay sessions

**Source issue**: [#1559](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/1559) (open; read-only snapshot, 2026-10-17).
**Branch**: `1559-relay-long-sessions-20261017`
**Base**: `a82b3d8439f76f83baade6df8516e78eea86e546`
**Status**: ARCH accepted at25713634 for tests-only G1 authoring; concrete SOURCE/FIXTURE/SELECTION/PREP and all native/product gates remain open.

## Source issues and scope

This multi-file transport, helper-publication and recovery change requires Spec Kit. The goal is sustained ordinary and repair turns between the daemon and an AI that already has computer access, including an AI without its own command shell. The relay transports information and applies a separately authored response; it does not choose a model, collect context, author gameplay or grant canonical authority.

Contract scope: relay/worker data exchange, existing bridge/daemon prompt delivery, fixed helper publication, operational documentation and affected guards. Game schemas, accepted-turn validation, console/browser gameplay and afterlife mechanics remain authoritative and unchanged. No general historical-save compatibility is added. The supported recovery population is a queue created by this feature under its original session generation and current protocol; pre-feature unresolved queues remain explicitly unsupported/read-only rather than silently replayed.

Out of scope: multiplayer #1251, mandatory file UI/browser/context collector, new agent workstation or access mechanism, network/Tailscale setup, provider adapters/credentials, systemd/backend redesign, new CI, artifact-transfer workarounds, issue closure, PR/merge/main changes. Windows and Linux acceptance are separate; an unavailable native platform remains open.

## User scenarios and testing

### US1 — Continue an ordinary game and repair cycles (P1)

The operator starts infrastructure once. The AI receives each exact daemon request through its existing computer-access capability, independently reads the indicated rules/state, and authors its response. Neither the fourth submission nor a later completed-prompt retention threshold terminates the game.

Independent test: a current production bridge/relay/helper chain completes more than three submissions, including several ordinary and repair requests, then continues past the old upstream 256-operation threshold. Exact request identities and complete payload hashes are checked, not only process liveness.

1. Given one live owned session, when ordinary and repair requests alternate, each keeps its own identity and completion kind.
2. Given settled historical requests, when their in-memory payloads/tasks are retired, old identities cannot cause another paste or application.
3. Given an AI without shell access, when it uses existing computer read/write access, it receives the current request and publishes a matching response without invoking relay Python/PowerShell commands. Operator startup is recorded separately.

### US2 — Exchange large complete packets within explicit resource bounds (P1)

Useful prompts and responses are accepted based on documented byte/resource budgets, not arbitrary line or write counts. Exceeding a configured budget produces a precise refusal that leaves the current request and canonical state recoverable.

Independent test: measured real repository packets and current canonical examples are transported byte-exact; controlled valid packets above the old byte/count thresholds exercise the new limits. Synthetic growth is labelled synthetic, and each retained budget has at/below/above boundary and allocation/disk observations.

1. A multiline Unicode prompt is not truncated or rejected merely for more than 120 lines.
2. A valid response with more than 32 distinct allowed writes is not rejected merely for its count.
3. An oversized/invalid packet is rejected before canonical mutation, with exact bytes retained and a safe next action; the terminal does not remain permanently unusable after a correctable error.

### US3 — Resume safely around publication and acknowledgement (P1)

The operator can reconnect/restart the relay without losing a request or duplicating a completed application. The recovery result distinguishes transport delivery, helper publication/terminal signal and actual client acceptance.

Independent test: disconnect/process interruption before application, during a multi-write set and after publication before relay acknowledgement; cold recovery observes either exact prior bytes or one complete published set, and never repeats a committed turn.

1. An incomplete response publication can be resumed only from its exact retained winner; another body cannot replace it.
2. A mid-set failure never leaves an undetected partially applied response or an unrestricted retry using stale original witnesses.
3. A committed terminal/receipt followed by lost acknowledgement is reconciled read-only; no write or terminal signal is repeated.
4. Changed session generation, pending request, hash or client authority blocks recovery; permanently closed queues stay closed. A new relay process obtains only its newly admitted terminal/run authority.

### Edge cases

Unknown helper outcome, committed publication with cleanup debt, malformed response/reply, full disk, queue quota, changed target between preparation and commit, duplicate aliases/case variants, stale repair attempt, manual terminal takeover, late old operation query, cancelled/closed queue, and process loss with an unresolved original child must retain explicit outcomes. Generic timeout, PID disappearance or queue metadata must not be treated as accepted-turn or replacement authority.

## Functional requirements

- **FR-001**: Remove lifetime submission and completed-operation caps while keeping finite concurrent work and bounded retained memory/disk.
- **FR-002**: Preserve complete UTF-8 bytes and request identity across prompt, request, response, helper application and recovery; no silent truncation or normalization of transported bytes.
- **FR-003**: Replace arbitrary character/line/write-count limits with validated configurable byte/resource budgets, documented defaults and exact over-budget diagnostics before mutation.
- **FR-004**: Keep RequestId/session/turn/kind/repair-revision, witness/hash, stale/duplicate, path, realm, client-owned surface and turn-validation checks.
- **FR-005**: Classify transport, format/contract, stale/duplicate, game-validation and file-publication failures separately; only proven safe recovery may restore readiness.
- **FR-006**: Retain the exact response winner and a durable application decision so reconnect/restart before, during and after acknowledgement neither loses the request/result binding nor applies the same response twice. The exact client-owned dispatch/apply records survive ordinary rejected-turn rollback/repair without deletion or rewind; genuine save/load/session-generation replacement invalidates their old authority. Missing/corrupt records never authorize replay.
- **FR-007**: Apply a multi-write response through the existing authoritative helper and durable publication mechanism, with detection/recovery of interruption and unchanged policy/normalization behavior.
- **FR-008**: Retire settled full prompt bodies/tasks/cancellation sources safely; expired identities remain non-replayable and unresolved operations remain owned. Bounded compaction must not reset a lifetime counter to permit replay.
- **FR-009**: Provide and prove the exchange using the AI's existing computer-access read/write capability without mandatory agent CLI invocation or new access/UI infrastructure; operator startup is a separate responsibility.
- **FR-010**: Update maintained operational docs and a worked exchange/recovery example, with explicit synthetic transport versus real client acceptance claims and no normal GM source-reading exception.
- **FR-011**: Verify applicable Linux and native Windows integrations separately. Missing Windows execution is an open acceptance item, not a portable/source-check PASS.
- **FR-012**: Preserve isolated owned fixtures, first failure evidence, exact source/package/payload provenance and independent architecture/source/evidence review, with a finite changed-contract selection.

## Success criteria

- **SC-001**: One current owned relay chain completes at least 12 submissions including at least four repair cycles, and a separate bounded prompt-retention control crosses 256 completed operations without replay or growing retained full-body/task counts.
- **SC-002**: Real captured/current-source-derived packets and controlled above-old-limit packets remain byte-exact; every retained configurable budget passes its boundary/invalid-configuration controls with no partial state mutation.
- **SC-003**: All three interruption phases and an injected mid-multiwrite failure recover to exact rollback or one whole committed result; duplicate/stale submissions cannot reapply it, and accepted client outcome is evidenced separately from helper/transport success.
- **SC-004**: A shell-less agent exchange is demonstrated using an existing computer-access capability, with exact request/response binding and independently read current instructions/state; operator infrastructure actions are separately recorded.
- **SC-005**: Applicable selected Linux verification is complete with owned cleanup and finite resource observations. Native Windows has its own execution evidence or is explicitly open; no cross-platform completion claim substitutes for it.
- **SC-006**: README/current handoff, finite selection, first failures and independent review decisions agree with the published exact remote/recovery checkpoint; previous #1557/#1558 evidence is retained with historical provenance.

## Assumptions and authority boundaries

The trusted local single-player model remains in force; deliberate concurrent external edits are unsupported, but confinement, generation fencing and process-crash recovery remain required. No user/private savedata is used for audit or fixtures. Request publication, prompt submission observation, helper committed publication, terminal protocol completion and client accepted turn are distinct facts. Existing current-schema recovery is reused; arbitrary legacy queues/saves are not migrated.
