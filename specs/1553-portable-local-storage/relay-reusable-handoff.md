# T043 maintained reusable relay — bounded handoff candidate

Source [#1553](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/1553).
Branch `codex/1553-load-filesystem`, sole writer; accepted S1 base
`41ecc2c54b538f400bdc180984c29416bec1508f`.
Frozen runtime/tests `1350a16c732bc9d8e90db25112d47a40d7d6c6bb`.
[Design](relay-reusable-design.md), [developer install/usage](../../tools/gm-relay/README.md).

## Delivered shared boundary

`tools/gm-relay/` contains one persistent CLI, fixed ordinary PowerShell consumer,
exact qualified input profile, frozen local request/response contract and
credential-free `inspect/answer/close` worker entrypoint. Copy the whole folder;
startup needs no source checkout/compiler/SDK. Python3/POSIX PTY is an optional
developer relay dependency, alongside PowerShell7 and the normal .NET8 game
package/bootstrap. Existing configured CLI/model/args and other game profiles
are unchanged. Legacy fixture paths forward to the one implementation.

Actual terminal paste then fresh observation then CR/T042 submit creates the
original bounded request/prompt/game-request bytes. Atomic no-overwrite response
then reply retains one winner; partial publication stays unresolved. Witnesses,
current turn/pending identity, bounded payload and closure are rechecked. The
fixed consumer uses actual session Read/Write-BoeJson and Complete-BoeTurn/
Complete-BoeValidationRepair. Worker publication and helper completion do not
prove game acceptance/history; those remain distinct game outcomes.

Shared API close and final open-check/executing snapshot serialize on one stable
bounded POSIX gate; its inode persists for the queue lifetime. Gate unavailability
or uncertainty refuses execution/ACK. After the authorized snapshot, the same
original child can settle after close. Historical direct-file close callers do
not acquire the new gate; stronger ordering is qualified for API participants.
Post-Popen started-metadata failure retains child/request until actual exit/I/O,
reports MetadataFailure, and stays RELAY ERROR. Subsequent unpersisted outcome
remains unresolved/noACK. Closed ACK requires execution disabled + actual child
exit + drain. Original main stop still requires its own native reap/I/O/disposal/
Stopped ACK; no queue/PID/EOF/guardian proof can mint authority or a fresh epoch.

One causal presentation correction displays LF as CRLF while exact stored
prompt bytes remain unchanged. Parser/VT subset, original M1/owner/fence/T042,
main/worker/generation admission, game mechanics and GM-authored schema remain
unchanged. This is client-owned transport, so Mortal World/afterlife GM prompts,
examples and contract matrix need no update; the accepted spec records that.

## Executed evidence

Only two new and two directly affected narrow categories ran through
`scripts/test-csharp.ps1`; no full suite, unrelated owner/S1 cohorts or frontend
execution. Synthetic packets and isolated owned fixtures, no provider/model calls.

| Stage / source | Executed result | Meaning |
| --- | --- | --- |
| Contract RED `6fbc133f` |12/12 FAIL|Missing shared contract|
| First main `6fbc133f` |2/2 FAIL|Fixture RPC3s preempted existing15s observation; preparation for intended worker RED|
| Main causal RED `2feb7272` |3/3 FAIL|2 actual submissions then missing worker;1 real multiline draft refusal before queue|
| First GREEN `aa5d7b16` |25/25 PASS|Extraction/presentation; source review still found close/start and metadata-error gaps|
| Focused causal RED `86f7df13` |7 PASS,2 FAIL|Close before snapshot still starts; post-start metadata falsely reports nonexecution|
| Final GREEN `1350a16c` |32/32 PASS|17 contract +3 production main +9 transport +3 repair, unique complete selection|

Final runner exit0, no timeout, cleanup complete, wall1:50.1179589. All **14 owned
scenario guardians** reached ECHILD with emergencySignals0/failures0/deadlinefalse
and driverExitCode0. Budget probes are separate primitive checks. Three relocated
actual production Bridge/pipe cases retain original owner/inventory, observed
T042 dispatch, shared worker and real fixed helper, API queue closure, original
scoped stop/I/O/Stopped ACK; one deliberately wrong completion kind proves real
consumer refusal with no output/completion. Positive cases prove Unicode and LF
multiline unchanged bytes, real helper output and original turn completion.
Metadata-error/close races use controlled barriers in the actual shared core;
they never substitute a fake execution/consumer result.

[Qualification](recovery/relay-reusable-qualification.json) links five packets:
[initial RED](recovery/evidence/relay-reusable/red/manifest.json),
[first GREEN](recovery/evidence/relay-reusable/first-green/manifest.json),
[review RED](recovery/evidence/relay-reusable/close-red/manifest.json),
[final GREEN](recovery/evidence/relay-reusable/final/manifest.json),
[discovery](recovery/evidence/relay-reusable/discovery/manifest.json).
All595 gzip hashes/decompressed bytes and27 runtime source pins were verified by
writer. PlanOnly32cases/4descriptors and catalog429categories/11178methods/files
both exit0 and execute0tests. Discovery at `70e50948` changed no runtime/tests.
Verified versions: Python3.12.14, SDK10.0.401, runtime8.0.31, PowerShell7.5.4,
cc Debian14.2.0-19. Preparation compiler provenance is separate from player startup.

Actual independent gpt-6.1-sol/xhigh design PASS `3a79b040`, narrow P1/P2 correction
design PASS, source/selection followup PASS `1350a16c`. Separate evidence/discovery
and metadata reviews remain pending for this candidate. Current qualification
records their exact final verdicts when received. Normal publication/readback and
fresh direct GitHub-only restoration are final writer delivery gates.

## Preserved live evidence and remaining work

Accepted r3 `dc1724afe66a9c0f07942e23e33c2af07aed8293` remains one genuinely
generated/applied action with clean original stop. All37 accepted artifact hashes,
historical profile/driver and historical pending/Uncertain are unchanged. No new
live GM run or model request occurred for extraction; controlled results do not
qualify Codex/OpenCode/provider compatibility or arbitrary APIs. Future adapters
need separate provider-specific design/authorization/qualification and ordinary
permitted access. Relay adds no credentials, grants, proxy or network bypass.

Only T043 reusable scope closes after final reviews/restoration. Remaining overall
obligations for parent-coordinated completion/merge:

1. Mandatory primary systemd **S2/S3 remain unavailable, unqualified and off**.
   S2 needs an already-running accessible user manager/cgroup-v2 environment,
   concrete pinned cgroup source/native ABI/FD/attachment/retirement/I/O/ACK and
   stop-deadline qualification; S3 connects qualified selection to the existing
   ordinary routes. No manager setup or implicit downgrade is authorized here.
2. Enabled production workers remain separately closed/unqualified; optional
   physical desktop/clipboard/audio and provider/CLI qualifications require their
   applicable environments. No universal API-support or overall game PASS claim.
3. **Native Windows runs after completion and merge**, in existing HOME-PC task
   «Лориан-Codex bridge», coordinated by parent. It is not a pre-merge PASS gate.
   This writer performs no Windows/HOME-PC access. Parent checks actual default
   branch and overall readiness before any future authorized merge.

No merge, force-push, branch deletion, issue closure or next implementation here.
