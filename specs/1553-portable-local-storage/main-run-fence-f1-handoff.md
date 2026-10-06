# T041-RUN-FENCE-F1 handoff

Accepted design0658e440; only connected neutral durable owner/fence is authorized.
Runtime candidate da3ffc6fa7a93bb89d8e81ee4d5e2c533b51c1f6. Independent final review pending.

The original held terminal consumes the existing schema1 main.json: Prepared before
creation, actual identity and Running durable ACK before one release. One coordinator/
persistence owns frozen retry; original guard stays held through Uncertain or metadata
debt, including visible Stopped without ACK. Real FileSystemManager admission precedes
recovery and validates held writes. Closing revokes new pins/input; existing linearized
publication can settle. Scoped stop, actual input/managed loops/I/O/disposal and ACK are
required for retirement and next epoch. Separate worker/storage gates and narrow cleanup
remain independent. No serialized record, PID or EOF reconstructs live authority.

Verification: 84distinct PASS through scripts/test-csharp.ps1, only new
`gm-main-run-fence` and `gm-main-run-owner-linux` owners plus exact affected methods.
Portable50 at39362ff8; finalLinux34 atda3ffc6f, after only the additional negative
observation seam/test (no change to ordinary path).27latest guardian ECHILD/emergency0/
failures0/deadlinefalse. Actual post-A1 Started ACK invalidation retains same owner,
closes input via actual pipe and prevents confirmed Stopped/replay.
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
