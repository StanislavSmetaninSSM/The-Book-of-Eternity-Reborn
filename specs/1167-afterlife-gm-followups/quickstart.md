# Quickstart: Afterlife and GM Bridge Follow-ups

## Scope

Validate issues #1167-#1171 without touching browser client code.

## Focused Verification

```powershell
dotnet test BookOfEternityClient.Tests\BookOfEternityClient.Tests.csproj --no-restore --filter "GmBridge|GmWorkerBridge|GmWorker|Daemon|Encoding|ChaosSeaCommandDisplaySaveTests|ShiningAbodeCommandDisplaySaveTests|ExplorerModeCommandTests.Afterlife"
dotnet test BookOfEternityClient.Tests\BookOfEternityClient.Tests.csproj --no-restore --filter "ExampleDocumentationValidationTests|AfterlifeDocumentationCoverageTests"
```

## Broad Non-Browser Verification

```powershell
dotnet test BookOfEternityClient.Tests\BookOfEternityClient.Tests.csproj --no-restore --filter "FullyQualifiedName!~LocalWebUiBuiltFrontendSmokeTests"
```

## Manual Console Checks

1. Load a Chaos Sea fixture save and run `/status`.
2. Load a Shining Abode fixture save and run `/status`.
3. Open `/shining_abode` details for gates, package/receipt, and pending core actions.
4. Open one guardian trade preview, one resident preview, and one archive/offering preview.
5. Confirm default views are readable in Russian and audit payloads are only shown after explicit audit selection.
6. Run a daemon logging smoke action containing Cyrillic text such as `Осторожно осматриваю письмо` and confirm stdout/log output remains readable.
7. Inspect generated Codex GM bridge launch guidance and confirm default cwd/context is GM-only, not the repository worktree.
