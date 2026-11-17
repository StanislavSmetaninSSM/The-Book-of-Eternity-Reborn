# Linux inherited-channel stand (WIP)

Base: `fa5d5ec8a20851f6b7401e5e7b27b455c9deb9d7`, branch
`1553-linux-relay-fd-stand`. User authorized a separate isolated launch stand,
then a dedicated issue and full Linux relay implementation if feasible.

This block tests kernel primitives only, not game acceptance or production
NativeLineage admission. The existing supervisor legacy exit and double-fork
fixtures completed with guardian ECHILD and no emergency actions. Ordinary
production terminal launch still fails at AF_UNIX creation. Its separate bridge
NamedPipe listener also needs a legitimate connection design.

Design: an exclusively retained, unreaped direct-child helper owns a gated root,
its pidfd and PTY. Private inherited pipes carry bounded descriptor locators.
The original helper pidfd plus pidfd_getfd duplicates the actual root pidfd and
PTY open file descriptions; numeric locators alone never authorize release.
No ptrace, dumpability, seccomp, capabilities or other security setting changes.
If transfer fails, do not release the root; close its gate and reap it.

Before production adaptation, prove an equally race-free original helper handle
in the managed/native launch boundary. Python's exclusive fork/wait stand does
not prove .NET Process ownership. Preserve native cleanup, partial ownership,
hold deadlines, PTY lifecycle and bridge per-connection pins in later work.

Checks planned: real root pidfd and bidirectional PTY transfer, PTY dimensions,
held-child liveness, rejection of invalid descriptor and injected EPERM/ENOSYS,
no payload execution on refusal, root and helper reaping, ECHILD. Platform
coverage is this machine only; other kernels, Yama/LSM and seccomp policies may
refuse pidfd_getfd. Such refusal must remain a supported fail-closed outcome.

Performance: no cache added. Each test must own a fresh helper/root/PTY;
process authority cannot be reused. Small immutable protocol data is read once.
No game builds or broad test suites are needed for this primitive stand.

Status: source preparation, not yet run/reviewed. No production source modified.

Initial runtime: real transfer/PTY exchange passed on Linux 6.18.57 x86_64;
real EBADF and injected EPERM/ENOSYS refused release and reaped the root/helper.
Independent review then found an unsafe test-harness child exception cleanup
(`kill(0)`), missing acquisition cleanup, non-structured type refusal and a
partial-read assumption. These are corrected in source; new wrong-type,
helper-pidfd-denial and pre-setsid child-failure controls await rerun/re-review.
No production path was changed or qualified. Initial happy-path success does
not qualify the corrected source or its failure paths.
