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

Native Linux controlled RED/GREEN archives are published in carrier
7afb19349519bacb959b5ccad3069226c669efa1 under recovery/http-linux-20261008;
[t055-linux-controlled-readback.json](t055-linux-controlled-readback.json) records
direct SHA/entry/source-pin/receipt verification. GREEN is3/3PASS with10correct
HTTP DTOs and10matching original closes. The post-fix original live133 series is verified in final Linux carrier
[9e277a67](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/blob/9e277a67534f718e8456e92e46b3a807ecdade2a/specs/1553-portable-local-storage/recovery/http-linux-20261008/original-owner-final-verdict.json):133meaningful200, original RPC/queue/I-O/Stopped identity and clean guardian.
[t055-linux-live-readback.json](t055-linux-live-readback.json) records the62-entry
archive verification and unchanged final-carrier raw Git blobs. Model requests0;
Linux browser UI/audio persistence/devices not qualified.

Direct GitHub-only restoration off228 verified76changedfiles and all included
payloads, clean; independent review PASS. The final metadata carrier is checked
again before delivery, with no build or rerun from the restoration.

Catalog/PlanOnly original archives preserve clean36ca4961 inventory:441categories,
11207methods,14descriptors/83planned cases. Both executed0tests; no83-case PASS
is claimed. Windows33 and Linux3 retain their own exact runtime receipts.
