# T041-RUN-FENCE-F1 handoff

Accepted design0658e440; only connected neutral durable owner/fence is authorized.
Runtime candidate39362ff85cb1f8e9cfa68819787d2292ce2ae662. Independent final review pending.

The original held terminal consumes the existing schema1 main.json: Prepared before
creation, actual identity and Running durable ACK before one release. One coordinator/
persistence owns frozen retry; original guard stays held through Uncertain or metadata
debt, including visible Stopped without ACK. Real FileSystemManager admission precedes
recovery and validates held writes. Closing revokes new pins/input; existing linearized
publication can settle. Scoped stop, actual input/managed loops/I/O/disposal and ACK are
required for retirement and next epoch. Separate worker/storage gates and narrow cleanup
remain independent. No serialized record, PID or EOF reconstructs live authority.

Verification: 83distinct PASS through scripts/test-csharp.ps1, only new
`gm-main-run-fence` and `gm-main-run-owner-linux` owners plus exact affected methods.
Portable50 at39362ff8; Linux33 (connected full82PASS) ata874fb5d, runtime/native driver
unchanged between these points.26guardian ECHILD/emergency0/failures0/deadlinefalse.
Correct staged cut has causal RED→GREEN; BOM preconditions/compile preparation are
separately labelled. Synthetic save/load archives exist only in isolated fixture roots.
[Qualification](recovery/main-run-fence-f1-qualification.json) records commands, source
hashes, passing IDs, debt/authority/order/settlement evidence and excluded boundaries.

Publication checkpoints use ordinary non-force pushes, exact remote SHA and fetched byte
readback. Final carrier SHA and fresh independent GitHub HTTPS-only restore are returned
in the final handoff; no tests are rerun for restoration.

Stop before F2. Unqualified: real separate-client/daemon pin protocol and ordinary
launcher/QTE/repair mutation wiring (F2); application crash/cold/replacement (F3);
primary existing-systemd-user backend (no manager here); native Windows (source/build
only); live provider/arbitrary GM/TUI, production Release/game-writing launch, real
user saves, reboot/power-loss/cold exactly-once. Product stop/load/fresh-start UX remains
later. No system/network/security/service/environment settings changed. Prior foreign
or preexisting zombies were not touched or claimed cleaned. No automatic unknown-input
retry, merge/force push/branch deletion/issue closure. No next stage started.
