# Quickstart: Browser Command Result UX Audit

## Preconditions

- GitHub issue #1087 exists and has label `codex-agent in-progress`.
- Local backend is available at `http://127.0.0.1:8787/`.
- Browser frontend is available at `http://127.0.0.1:5173/`.
- A Browser Act session may already exist under `boe-browser-ux-audit`.

## Focused Verification

```powershell
dotnet test BookOfEternityClient.Tests\BookOfEternityClient.Tests.csproj --no-restore --filter "ExplorerWebCommandServiceTests"
```

## All-Command Default Hygiene Gates

```powershell
dotnet test BookOfEternityClient.Tests\BookOfEternityClient.Tests.csproj --no-restore --filter "FullyQualifiedName~ExplorerWebCommandServiceTests.ExecuteAsync_PlayerDefaultReadOnlyCommands_RenderPlayerFacingDefaultOutput|FullyQualifiedName~ExplorerWebCommandServiceTests.ExecuteAsync_PlayerDefaultMutatingCommands_RenderPlayerFacingDefaultPromptOrStatus"
```

## Browser Act Smoke

1. Open `http://127.0.0.1:5173/`.
2. Continue the chapter.
3. Run `/статус`, `/инв`, `/gacha`, and `/craft`.
4. Confirm default browser mode shows readable Russian player-facing blocks or forms.
5. Confirm the visible result does not show raw JSON, file paths, `DTO`, `API`, `pending`, `interactive/write`, `currentRealm`, `Realm`, `image_prompt`, color tokens, booleans, or repeated generic `detail` labels.

## Follow-Up Audit Commands

- `/нпс`
- `/инв`
- `/статус`
- `/книги`
- `/эффекты`
- afterlife-related detail commands
