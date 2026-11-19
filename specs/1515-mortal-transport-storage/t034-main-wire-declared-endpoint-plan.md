# Main-wire declared endpoint fidelity (SOURCE proposal; no execution)

## Facts

The failed run's retained gm_bridge_status.json SHA
0eac97db74fd97412fa8249c4e9be4e9d7cf03e9be7f085f2b90b6cfdcb5b78c
publishes version1/kind linux-fifo-v1, transportRun
cd292d784308428fb46b7969e2337777. This is direct evidence from that
completed run, not an inference from mm19 or a newly started process.
The fixture nevertheless uses NamedPipeClientStream at
OwnedTerminalScenarioDriver.cs145–146; its first readiness Rpc at201 fails
in Socket construction. The underlying OS permission cause is unknown.
No connector/readiness/main-menu success is inferred from this failure.

Current production BridgeHost.EnsureBridgeTransport (Program.cs187–194) freezes
one listener before child startup and publishes its Endpoint. Existing
BridgeTransportListener.Create probes Unix socket construction, chooses its own
existing FIFO listener only on EPERM/EACCES at construction, and never changes
transport after later listener failures. The proposal does not change this code,
force a transport, repeat the failed connection or alter sandbox policy.

The ordinary live helper bridge-request.ps1 already calls the shipped
BridgeTransportConnector.RequestAsync with status JSON. DecodeStatus/Validate
require the explicit versioned endpoint; ConnectAsync dispatches exactly its kind
and has no catch-and-switch fallback. FIFO client enforces nonce/name binding and
uses maintained private-directory/FIFO owner/mode/inode checks. It does not create
an alternate client protocol in the fixture.

## Minimal proposed fixture change

Only production-main-console-continue-pty's local Rpc branch would use the same
public BridgeTransportConnector.RequestAsync. Other existing fixture modes stay
unchanged. Read the exact own root/game_session/game_state/control status path,
not a pipe-name reconstruction. After StartShell, obtain the existing original
owned terminal identity (move the already-present _pty access at line236 before
the first RPC; add no reflection surface) and use it only as a comparison witness, not adoption.

Before any request, pure admission checks require exact sessionPath, pipeName,
terminalRunId, shellPid and terminalUncertain=false against that original
witness. Do NOT require persisted terminalOwnerRetained=true: current status
publication serializes _status, while SnapshotStatus computes the live retained
flag only for RPC replies. Require that flag in the actual first/ordinary live-owner status reply instead, never in an expected shutdown/retirement receipt,
while the preconnection owner witness remains the exact existing session object. DecodeStatus enforces endpoint version/kind/address/run.
Capture the first admitted endpoint and require exact equality for every later
request and returned status. The first status need not be Ready: readiness remains
the real server response loop, not a bypass or forged true flag. A missing,
unpublished, malformed, changed or unjoined descriptor refuses before connection.
The endpoint is coordinates only, never a root/generation/owner capability.

The status has NO GenerationId. Before any connection, join the independently
fixed fixture root and held original IOwnedTerminalSession.RunId to the current
durable main record using its existing codec/reader. Capture its full identity
and generation, compare the already prepared generation if available, and refuse
any later drift rather than adopting the new record. Check held original
RootExited and AuthorityLost tasks before every ordinary request. Missing,
foreign-root, changed-generation or changed-owner records refuse before connect.
Shutdown keeps its existing command-specific retirement proof: retained=true is
not demanded after the expected original retirement. No new owner is acquired.

Keep the existing three-second per-RPC bound and existing whole fixture bound.
Use the same fresh stream per request through RequestAsync, with exact existing
request JSON and no transport fallback. Reuse existing common owner receipt and
scoped shutdown tail unchanged. No new reflection, transport registry, retry,
listener configuration or production implementation is required.

## Before any runtime

Independent source review must confirm the original identity witness is available
before the first readiness RPC, and the actual status publication point. Add pure
DATA cases for correct declared FIFO and named endpoint, missing/unknown version,
wrong session/pipe/run/PID, uncertainty, unretained actual reply, and endpoint drift;
assert refusal before invoking any connector. Reuse current connector codec tests
where suitable; do not execute sockets/FIFO/owner/bootstrap in these DATA controls.
Do not claim DATA validates runtime filesystem mode checks; those are maintained
connector obligations and remain subject to a later separately reviewed runtime
gate. No main-wire rerun, NamedPipe retry, sandbox escalation or alternate IPC has
been authorized or performed by this proposal.
