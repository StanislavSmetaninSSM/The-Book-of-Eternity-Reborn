# T041-SYSTEMD-MAIN — deferred source/design WIP

Source: [#1553](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/1553). Accepted base `13cdd9aedcacaba62e76859c16940583d285c365`. Owner changed priority to bounded real console Q1→Q2 before this design was completed; preserve this draft, do not treat it as an approved architecture or backend qualification.

## Verified existing boundaries

- `Services/GmWorkers/GmWorkerBackendSelector.cs` currently reports explicit SystemdUser NotImplemented. Ordinary Linux WorkerRelease remains NotQualified before launch; synthetic admission is separate. Public pool execution must not be enabled by a main adapter.
- `Services/GmRuntime/ProductionMainLaunch.cs` currently admits explicit NativeLineage only, resolves the exact configured PowerShell command/cwd and packaged helper before effects; Auto/SystemdUser are closed for main.
- `Services/GmRuntime/OwnedTerminalSessionFactory.cs` and `LinuxOwnedTerminalSession.cs` currently depend on the concrete original NativeLineageOwner for held preparation/release and actual retirement. A future systemd adapter needs a consumed original release/owner seam, not another launcher.
- `Services/GmRuntime/GmSessionRunCoordinator.cs` publishes schema1 Prepared before creation, Running before single release; stop requires original terminal retirement/ACK. Main/worker/generation mutation gates and typed pins stay conjunctive.
- [Main fence design](main-run-fence-design.md): LinuxSupervisor is the schema1 platform family, not a systemd guarantee. Do not unnecessarily replace it with a second journal/backend-specific run ledger. A systemd scope binding must be retained by the original live owner; decoded unit/PID/status fields cannot mint authority.
- [Approved Linux ownership](linux-ownership-design.md#approved-two-backend-linux-ownership--2026-10-05-1512-utc) already settles global Auto: prefer qualified accessible systemd-user, otherwise declared qualified native fallback only before launch; explicit SystemdUser never downgrades. Actual main Auto activation remains a later controlled integration/qualification slice, not a new policy decision inferred from manager presence.

## Unresolved architecture questions for resumption

1. Evaluate an original held PTY child attached to a transient `.scope` through the existing accessible user manager, preserving native PTY/reaping while systemd owns the declared unit/cgroup boundary. This is a hypothesis, not a chosen implementation. Verify exact PIDFD attachment support, manager/PID namespace coordinates, source-compatible minimum API, unit reference lifetime and release timeout before choosing it.
2. Compare official user D-Bus integration (narrow optional sd-bus binding in the prebuilt helper, with justified library/package capabilities) against a shipped managed D-Bus transport. Do not silently add a compulsory player install, shell-based stop oracle or systemd-run launcher. Existing .NET8/PowerShell7/native package contracts remain.
3. Design original manager connection/name-owner + unit/invocation/cgroup identity and pinned cgroup lifetime, Held→Running→Release, bounded stop and authoritative empty evidence. Job completion/inactive/root exit/EOF alone cannot settle the main fence. Authority loss, unknown start/stop/ACK or metadata debt retain Uncertain; original native emergency cleanup does not upgrade systemd scope proof.
4. Split source/controlled transport tests from mandatory positive qualification on an already-running accessible user manager; no manager/service/cgroup/privilege/security setup here. Preserve disabled worker inventory, existing lease order and no production worker enablement.

## Primary references read only

- [Pinned systemd v257 D-Bus interface](https://github.com/systemd/systemd/blob/v257/man/org.freedesktop.systemd1.xml): transient units, unit/invocation identities, job signals and references. Exact attachment/retention semantics still need focused source analysis.
- [Pinned scope implementation](https://github.com/systemd/systemd/blob/v257/src/core/dbus-scope.c), [scope docs](https://github.com/systemd/systemd/blob/v257/man/systemd.scope.xml), [kill docs](https://github.com/systemd/systemd/blob/v257/man/systemd.kill.xml), [user bus docs](https://github.com/systemd/systemd/blob/v257/man/sd_bus_default.xml), [kernel cgroup-v2 docs](https://github.com/torvalds/linux/blob/v6.12/Documentation/admin-guide/cgroup-v2.rst).
- The freedesktop latest-doc read returned HTTP403; official pinned upstream sources were readable. No network/auth/system settings were changed and no manager/device/process capability probe was performed.

Status: **DEFERRED WIP, independent design review pending**. Zero runtime/test/catalog changes, builds, CLI/systemd/service/cgroup/device probes. Resume source-backed architecture and independent Sol6.1/xhigh review before any implementation. Q1's previous readiness blocker and cleanup evidence remain accepted; new user authorization permits one TERM `y` and 1–2 isolated real console GM turns, separately from this postponed design.
