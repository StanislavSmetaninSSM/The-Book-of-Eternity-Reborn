# Owned main terminal — design WIP

Issue: https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/1553
Task: T041-OWNED-MAIN-TERMINAL-DESIGN. Base: accepted T042
`1bc9d67536dccbcc6672d8e7cd71ad885c2a946c`.

This checkpoint preserves the authorized design boundary before an isolated probe.
The result will specify one consumed terminal-session interface and the next
neutral-only implementation slice; runtime implementation is not authorized here.
Use existing Spec Kit #1553, Superpowers brainstorming/writing-plans and bridge;
sole Sol6.1/xhigh writer, separate actual Sol6.1/xhigh reviewer.

Current changed boundaries: BridgeHost directly starts ConPtySession; native
supervisor launch binds child stdin to `/dev/null` and calls setpgid, not setsid/
TIOCSCTTY; prompt dispatch still reads a Win32 visible console. Native exclusive
reap, accepted worker ownership/output/input/transaction proofs are reusable.
ConPTY main Dispose currently uses Process.Kill and does not prove Job emptiness.
The main record/admission helper remains a slot condition, not production launch
or write authority. The final design will pin these source boundaries.

Unanswered environment question: an unprivileged newly owned Linux PTY's
controlling-terminal attachment, bidirectional Unicode, resize/SIGWINCH,
canonical EOF distinct from process exit, and master hangup after scoped reap.
One throwaway no-descendant fixed fixture probe, bounded by an independent
existing guardian, is permitted. It changes no service, privileges or settings.
It cannot qualify the integrated terminal backend or systemd. All owned resources
and exact probe/source/toolchain/cleanup evidence will be recorded.

No tests or accepted audits will be rerun. No game-writing GM, real save, live
provider, cold exactly-once, public rollout, installation or environment changes.
Native ordinary-same-PID-namespace scope remains explicit; uncertain stop retains
the owner/session. Main run fence must precede any production write-authorized
launch. Primary existing systemd-user remains required by the overall roadmap;
this environment has no user manager and will not be configured to provide one.

Status: WIP; capability probe, final interface/plan and independent review pending.
