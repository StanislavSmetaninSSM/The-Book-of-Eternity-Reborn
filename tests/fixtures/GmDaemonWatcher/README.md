# Serial daemon watcher regression

`reentry.ps1` extracts each **actual daemon callback** and registers it on a real
FileSystemWatcher. The Python child is an inert sequence/ok stdio responder, not
a GM, canonical-state helper or game simulator. Production
`Send-GmOperationCommand` / `Read-GmOperationReply` are loaded unchanged. Only the
four Process-* consumer bodies are replaced with an inert command so this test
isolates event scheduling and reply ownership, without acquiring game authority.

Run beneath an external bounded process runner (20 seconds per case), with an
explicit writable PowerShell XDG environment:

```
pwsh -NoProfile -File tests/fixtures/GmDaemonWatcher/reentry.ps1 \
  -RepoRoot /absolute/repo -OutputDirectory /fresh/unique/directory \
  -Scenario action
```

Run `control`, `action`, `repairAction`, `terminalProtocolFailureAction`, and
`qteEffectResolutionAction`, each with its own fresh directory. Python3 must be
available at `/usr/bin/python3`. A reused directory could hide a Created event;
use a fresh path, never overwrite an earlier result.

The responder creates a file during the outer command and delays its reply.
PowerShell delivers the event before the outer command returns. Old callbacks
enter a consumer and advance the shared sequence; the outer command then fails
with `Original operation response identity mismatch`. New callbacks only signal;
the fixture drains that signal and performs the consumer after outer return.
The result requires exactly one deferred consumer for each callback, sequence 1
accepted, no lost context/event error, child exit 0, stdout EOF, stderr joined and
no forced kill. This does not qualify a live turn or establish the exact cause of
any separately observed daemon stall.

`startup-failure.ps1 -RepoRoot ... -OutputDirectory ...` executes the exact
acquisition prefix and production cleanup helper, injecting failure on the fourth
registration. It requires the first three successful registrations to be retained
and cleaned, no remaining own jobs/subscriptions, a disposed signal, and an
unrelated subscriber to survive. Cleanup diagnostics must be empty. This is a
separate partial-startup regression, not live daemon/turn qualification.

`cleanup-failure.ps1` injects the first watcher disposal failure while executing
the actual shutdown block. It requires explicit failed status, remaining cleanup
attempts and signal disposal. Repeat with `-PriorFatal` to verify that an existing
fatal error remains the published cause rather than being masked by cleanup.
