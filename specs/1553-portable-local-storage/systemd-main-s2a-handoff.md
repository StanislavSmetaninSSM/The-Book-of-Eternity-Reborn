# T041-SYSTEMD-S2A — closed source/controlled handoff

Source [#1553](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/1553).
Runtime `b97b50a97123c5201f9022dba953e0396e1fa6d0`; final tests/source review `2ad77e2906d9863c86c74711743dca9bf4d89fb9`.
[Plan](systemd-main-s2-source-plan.md) / [qualification manifest](recovery/systemd-main-s2a-qualification.json).
Only the missing read-only source and controlled connection are implemented here. Full S2 actual-manager qualification, S3 ordinary selection and public backend remain open/off.

`SystemdCgroupSource` consumes existing `ISystemdCgroupSource`, unit ControlGroup and original held PID supplied by the retained owner. It pins the caller cgroup namespace FD, matches original child membership/namespace before and after binding, and accepts one unambiguous full-root cgroup2 mapping in that caller namespace. Root `/` is not a host-global claim. Read-only NOFOLLOW directory/events FDs retain mount/device/inode/type identity, with native cgroup2 filesystem check. Fresh bounded UTF8/events parsing and original path/FD/namespace/mount checks bracket every observation. Errors/disappearance/replacement/stale sequence/disposal latch refusal. Pruned remains unqualified; no missing pathname/EOF/nlink/deleted membership becomes Empty.

No existing coordinator/schema1/launcher was replaced. Connected fixtures inject only kernel/bus observations; the actual original native PTY/pidfd, Bridge pipe loop, T042 input and original native reap/I-O/ACK remain consumed. Scope-empty and native retirement/disposal conjunction is unchanged. No child PID reads after binding; no foreign process scans or environment/cmdline/secrets. No cgroup writes, service setup or new player dependencies.

| Evidence | Actual result |
| --- | --- |
| First preparation | CS0539 test fixture Dispose;0executed, preserved separately |
| Causal connected baseline |3executed FAIL at unavailable source Bind;3guardian ECHILD/emergency0; source selection not executed in fail-fast run |
| Causal source baseline |29executed,19FAIL/10baseline-negativePASS; no timeout/cleanup failure |
| First GREEN |38/38:29source +3original controlled Bridge +6exact observation/public refusal |
| Review coverage |33/33source with4new native readonly cases; connected/affected unchanged, not replayed |
| Final unique coverage |**42**:33source +3connected +6affected; total103executed including RED/repeated source coverage,81PASS/22FAIL historical |
| Guardian |6total/6ECHILD, emergency0/failure0/deadlinefalse; physical cleanup never promotes logical Uncertain |
| Discovery / selection |432categories/11192methods-files,0tests; PlanOnly42cases/3descriptors,0tests |
| Source evidence |18source pins/59artifact records; both dirty GREEN fingerprints reconstructed exactly from committed source/test blobs |
| Reviews |Actual Sol6.1/xhigh design PASS73d58e5e; source PASS2ad77e29 after two P2 verification requests closed; evidence PASSd99659fb; metadata PASS481c6ff6 |

The positive Bridge case uses two inputs, original Running identity and actual disposal/StoppedACK. Two fault cases first observe Empty, then replace identity or lose reading on the post-native-reap sample; input closes and the original owner/durable nonterminal remains retained. Native FD fixtures use owned regular directories and synthetic event bytes; separate tests check real self-namespace NSFS statx, exact owned FD closure/EBADF, and initial symlink/partial-open refusal. These do not qualify a systemd scope.

Read-only environment probe2026-10-07: UID1000, no `/run/user/1000/bus`; XDG_RUNTIME_DIR and DBUS_SESSION_BUS_ADDRESS absent; systemctl/busctl refuse, `sd_bus_open_user=-123` ENOMEDIUM. systemd257 binary/libsystemd exports are installed. Visible readonly cgroup2 root `/..` is outside this qualified mapping subset and is not an owned scope. No manager/unit/cgroup mutation. SDK10.0.401/.NET8.0.31/PowerShell7.5.4/ccDebian14.2.0-19 verified; native compiler only prepares owned fixtures, never player startup.

Next required environment: Linux with an **already-running accessible caller user manager**, existing user bus, optional libsystemd/Unix-FD APIs and matching observable cgroup-v2. Continue same retained adapter with fixed neutral CLI: native actual pidfd attachment/unit/invocation/child mapping, descendants/stop, reliable pruning witness or honest refusal, stop margin and original I/O/ACK. Controlled source cannot settle those gates. Then S3 connects ordinary declared backend selection/consumers under the already-accepted Auto policy; explicitSystemdUser never downgrades, post-start backend switching remains forbidden. SupportsPidfdScopes=false and production NativeLineage-only admission are unchanged here.

Other obligations retained: independent enabled production workers and aggregate ordinary gameplay/save-load/restart qualification; Codex state-home and OpenCode provider compatibility; physical desktop capability positives as applicable. The accepted maintained relay and its one clean real turn are preserved, no model requests repeated. Native Windows is **after completion/merge**, coordinated by parent in existing HOME-PC «Лориан-Codex bridge», not a pre-merge PASS condition. No HOME-PC access or Windows run here. Full #1553 completion/merge readiness is not declared by S2A; parent coordinates readiness/default branch/merge. No merge/force/issue closure performed.

Client-owned observation adapter only: game rules, profile/model and GM-authored contracts/prompt examples unchanged. Stop at this handoff; actual manager and public activation cannot progress safely in this current environment without a different already-ready environment. Final metadata carrier requires ordinary push/exact remote/byte readback and fresh direct GitHub restoration; exact delivery SHA/proof accompanies final response.
