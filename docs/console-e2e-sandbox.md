# Console E2E sandbox session

Issue: #675

Console E2E runs must not use the developer's live `BookOfEternityClient/game_session` directory. Use `FileSystemExample/game_session` as the deterministic fixture source and copy it into a disposable per-run base path.

## Fixture source

- Source fixture: `FileSystemExample/game_session`
- Per-run base path shape: `<artifact-root>/run-<guid>/`
- Client session path: `<artifact-root>/run-<guid>/game_session/`

The source fixture is never mutated directly. Each run receives a unique copy, so a failed run cannot pollute the next one.

## Programmatic setup

Use `BookOfEternityClient.Configuration.ConsoleE2ESandbox`:

```csharp
using var sandbox = ConsoleE2ESandbox.CreateFromFixture(
    fixtureGameSessionPath: Path.Combine(repoRoot, "FileSystemExample", "game_session"),
    artifactRoot: Path.Combine(repoRoot, "artifacts", "console-e2e"));

// Pass sandbox.BasePath as the client's legacy path argument.
```

Dispose deletes the sandbox by default. Pass `preserveArtifacts: true` when a failing run should keep its copied state for diagnosis.

## Local launch command

After creating a sandbox, launch the console client with the sandbox base path, not with the fixture path:

```bash
dotnet run --project BookOfEternityClient/BookOfEternityClient.csproj -- "<artifact-root>/run-<guid>"
```

Expected startup behavior for a valid sandbox is that the client can initialize its state from `<artifact-root>/run-<guid>/game_session` and reach the normal console flow. Invalid or missing fixture paths fail closed before launch with a clear `E2E fixture game_session was not found` diagnostic.
