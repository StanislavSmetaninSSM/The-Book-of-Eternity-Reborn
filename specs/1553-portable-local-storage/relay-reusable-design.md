# T043 reusable relay — bounded design and implementation plan

Source [#1553](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/1553).
Base `41ecc2c54b538f400bdc180984c29416bec1508f`; sole writer, existing branch.
Owner authorizes implementation after independent Sol6.1/xhigh design review,
unless a new product choice appears. Use Superpowers executing-plans/TDD and the
existing Spec Kit spec/plan/tasks; no second general journal.

## Intent and source map

Keep the accepted relay as maintained shared developer code and a small local
adapter boundary. A persistent CLI must receive the actual prompt through the
original terminal/Bridge/T042; an external worker reads that exact request and
publishes a response packet; the fixed existing consumer applies it normally.
Model generation is the worker's responsibility, not the terminal transport's.
No network provider, credential acquisition, universal provider framework or
new gameplay/GM-authored schema is introduced.

| Existing source | Reused behavior / change |
| --- | --- |
| `tests/fixtures/ProductionMain/codex-gm-relay.py` | Linux persistent POSIX PTY; supported neutral transcript; paste followed by actual CR; original request/pending witnesses; one fixed consumer child; bounded output; close disables execution before rollback; terminal-mode restoration |
| `relay-apply-response.ps1` in the same folder | Actual session bootstrap, Read/Write-BoeJson and Complete-BoeTurn/Complete-BoeValidationRepair; retain packet schema and witness semantics |
| `live-game-one-turn.py:77–103,328–386` | Qualified test command/model label and neutral profile; original input receipt, queue close before client cancellation, main owner/ACK/I/O; retain this driver/profile and historical evidence, do not rerun live generation |
| `ProductionMainLinuxFixture.cs`, `OwnedTerminalScenarioDriver.cs:110–194` | Existing shipped configuration, main+worker+generation admission, original production Bridge and real pipe accept loop; add only a controlled relay mode, no new launcher/owner |
| Existing GmRelayTransportTests/GmRelayRepairTests | Directly affected forwarding/resource consumers; retain cheap isolated transport and real helper checks |

Prefer extracting the existing Python transport and fixed PowerShell helper to
`tools/gm-relay/`, with small forwarding entrypoints at the old fixture paths.
A C# rewrite would requalify a second transport for no useful behavior change;
a provider framework would exceed this task. Python3 and PowerShell7 are optional
developer relay dependencies, not a new prerequisite for ordinary players who
select another CLI. Current terminal qualification is Linux POSIX PTY only;
native Windows is a separately scheduled **post-merge** desktop verification.

## Smallest shared contract

Shared files: `relay_cli.py`, `relay_contract.py`, `relay_worker.py`,
`relay-apply-response.ps1`, `input-profile.json`, `README.md` under `tools/gm-relay/`.
The copied folder works without a source checkout/compiler/SDK at relay startup.
The CLI keeps `--session`, `--queue`, `--model`, the existing three-submission
bound, terminal presentation and packet consumer. Old test paths forward to this
one implementation; the fixed helper remains the sole packet executor.

`relay_contract.py` exposes only:

- `read_request(queue: Path, request_dir: Path) -> RelayRequest`: bounded reads of
  the original request/prompt/game-request; validate QueueId, prompt/request/turn
  hashes, identity and unchanged original pending witnesses. An incomplete
  request is not ready. Paths are original queue/session data, not mintable main
  authority. Return a frozen packet with exact bytes, model and correlation.
- `publish_response(request: RelayRequest, packet: bytes, *, adapter_id: str)`:
  recheck the original immutable request/witnesses and queue-open state, bound
  packet size, publish response bytes then matching reply exactly once. Retain
  existing `AgentTask`/Model and hash fields; no response/state manufacture.
- `request_close(queue: Path)` and `read_close(queue: Path)`: the existing
  close-request/closed records. Closed is execution-disabled + actual fixed child
  exit/output drain, **not** original terminal/main stop or game acceptance.

Use typed local exceptions for incomplete, mismatched, closed and already
answered requests. A credential-free `relay_worker.py inspect/answer/close`
entrypoint uses these same functions; it does not invoke a model. A future
authorized API adapter may consume this boundary but needs its own design,
authorization and provider-specific qualification.

Atomic no-overwrite publication of new worker response/reply files avoids the
relay observing a partially written envelope. It uses temporary files and a
same-directory link, with cleanup of only the writer's own temporary file;
missing filesystem capability refuses. No extra receipt/journal or save-owner
security layer. One response winner; partial publication is unresolved, not
permission to repeat a model request. Core creates the immutable executing
snapshot before starting its one consumer. Closing forbids later starts; an
already linearized consumer must settle before closed ACK. A response racing
close may remain inert, so worker publication is never claimed as execution or
game acceptance. Stop/signal errors never mint a close ACK or logical success.

Preserve the exact input profile as a shared JSON artifact: IdleMarker
`NEUTRAL READY`, PromptPrefix `RELAY> `, WorkingMarker `NEUTRAL WORKING`,
BlockedMarkers `["RELAY ERROR"]` (plain marker, without literal brackets),
ObservationTimeoutMilliseconds15000. No Auto/
SystemdUser activation, input replay, process identity reconstruction, arbitrary
script execution or changes to client model/command settings.

## Implementation / causal verification

1. Publish design/spec/tasks and get independent Sol6.1/xhigh design verdict.
   Consistency: issue traceability, trusted local player, original admission and
   client-owned transport; no GM prompt/examples change is required.
2. Add narrow `gm-relay-reusable-contract` and `gm-relay-reusable-main` categories,
   explicit selection reasons and expected method counts. RED through the
   missing shared worker/entrypoint: exact Unicode/multiline bytes, incomplete
   request, wrong/stale identity, duplicate response, close/late response,
   bounded malformed/oversize response and atomic no-overwrite publication.
   Tests use isolated synthetic bytes and no model/network/device calls.
3. Extract the transport/helper, forward legacy fixtures, implement the small
   worker contract/CLI and document copying/configuration/worker loop and error
   outcomes. Preserve accepted r3 evidence byte-for-byte; its live verdict is
   historical, not a fresh qualification of the extraction.
4. Through existing production fixture/Bridge, configure the relocated shared
   relay and original neutral profile, use the real pipe loop to submit one
   synthetic current turn prompt, inspect/answer via the new worker, and verify
   the actual helper output/turn completion, original dispatch receipt, duplicate
   refusal, queue closure before same-original scoped stop/I/O/Stopped ACK.
   Second controlled case: real consumer error produces no completion and still
   closes its child/I/O before original retirement. Do not substitute canned
   transport results for model/gameplay PASS. Fixed synthetic payloads are valid
   for these transport/helper tests only.
5. Run only the two new categories and directly affected old relay transport/
   repair categories. No unchanged driver/history/view, S1 or whole owner
   cohorts; source-selection review checks sufficiency. PlanOnly/ValidateCatalog
   are discovery only. Preserve exact hashes, commands/counts/cleanup and RED.
6. Independent Sol source/evidence/metadata review, ordinary checkpoints/push/
   exact remote/byte readback, final fresh direct GitHub restoration and handoff.
   Stop before other integration or model requests.

## Completion limits / merge coordination

Successful controlled packet publication/helper completion is not live model
generation or full game acceptance; accepted r3 remains the existing proof of
one genuinely generated and applied action. Systemd S2/S3 require an available
already-running manager and remain unavailable/unqualified with backend off.
Production workers remain separate, no new provider integrations or grants.
The parent coordinates final readiness and conditional merge/default branch;
Windows execution is planned after merge in existing «Лориан-Codex bridge», not
a pre-merge condition. No HOME-PC access, merge or desktop launch occurs here.
