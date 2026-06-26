# Plan: GM Session-Local Context Pack

Source issue: https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/1277

## Technical Approach

- Add daemon-side context-pack generation near the existing turn helper
  bootstrap generation.
- Copy only GM-facing docs/examples into the context pack; do not copy source
  implementation files.
- Produce a small manifest/readme so the GM has one obvious starting document.
- Update prompt directives to reference context-pack paths and explicitly avoid
  implementation code during live play/repair.
- Adjust bridge working-directory defaults or generated config guidance so live
  GM sessions can start from the context pack/session directory.

## Verification

```powershell
dotnet test BookOfEternityClient.Tests\BookOfEternityClient.Tests.csproj --no-restore --filter "GmTurnHelperContractTests|GmBridgeDiagnosticsContractTests|AfterlifeDocumentationCoverageTests"
dotnet build BookOfEternityGMBridge\BookOfEternityGMBridge.csproj --no-restore
```

Manual/live:

- Start a Chaos Sea live bridge run.
- Confirm bootstrap and turn prompts point at the context pack.
- Confirm Codex GM does not read `BookOfEternityClient/**/*.cs` during ordinary
  turn or repair.

