# Quickstart: Browser Console Command Parity Audit

## Verify

```powershell
dotnet test BookOfEternityClient.Tests\BookOfEternityClient.Tests.csproj --no-restore --filter BrowserCommandCoverage --logger "console;verbosity=minimal"
```

## Manual Spot Check

1. Inspect `docs/audits/browser-console-command-parity-audit.md`.
2. Confirm every command ID emitted by browser command coverage appears as a backticked literal.
3. Confirm non-adequate rows have P0/P1/P2/P3 priority and a follow-up issue or explicit no-fix reason.
4. Confirm the summary names #1121, #1122, #1123, #1124, #1125, and #1126 with recommended order/current state.
