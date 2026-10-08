# T055 original-owner HTTP recovery

See [current plan](../../http-original-owner-plan.md) and [manifest](manifest.json).
Runtime/tests: `3d6cfd24898fa30b37764de688432455ac9a4660`.
Tests-first RED: `0a2843af69c521d7507a9e036cb5494d8c9d73cb`; cleanup-only follow-up:
`09f965d911d504454535fcd81d6c5cceade51f8e`. Product runtime was unchanged before3d6cfd24.

Windows original archives contain8-case RED (4PASS/4FAIL) and33-case GREEN
(33PASS), complete original TRX, summary, plan, build/runner logs and launcher
receipts. Every raw entry is hashed; do not normalize CRLF or rewrite old results.
No archive is evidence of active Linux owner success.

Native Windows selection (already passed on3d6cfd24):

```powershell
pwsh -NoLogo -NoProfile -File scripts/test-csharp.ps1 -SelectionFile specs/1553-portable-local-storage/recovery/http-original-owner-20261008/selection-windows.json -Parallelism 1
```

Native Linux causal RED at0a2843af or09f965d9:

```powershell
pwsh -NoLogo -NoProfile -File scripts/test-csharp.ps1 -Category browser-original-owner-linux -Parallelism 1
```

Native Linux GREEN at3d6cfd24 includes the new original-owner fixture and actual
Load/fresh-epoch and premature-ACK boundaries (three cases):

```powershell
pwsh -NoLogo -NoProfile -File scripts/test-csharp.ps1 -SelectionFile specs/1553-portable-local-storage/recovery/http-original-owner-20261008/selection-linux.json -Parallelism 1
```

Use separate owned roots. No provider request or readiness override is needed.
Preparation failures, deadline, incomplete results or emergency guardian cleanup
are not causal RED/PASS. Also repeat the original ordinary-NewGame/web+idle-relay
133-request live series from Linux evidencee469976b. Require full meaningful DTOs,
original operation closes and original shutdown; HTTP200 alone is insufficient.

Linux evidence and final exact-carrier restoration remain pending at this checkpoint.
