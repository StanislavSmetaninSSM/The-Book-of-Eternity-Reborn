# Local Web Host

Tracked task: #565  
Parent epic: #559

## Local-Only Model

The browser UI is a local shell over the same C# game client and the same `game_session` data. It is not a cloud service, does not require an account, and binds to loopback addresses only.

Default URL:

```text
http://127.0.0.1:8787
```

The host rejects non-loopback bind addresses such as `0.0.0.0`.

## Launch

From the repository root:

```powershell
dotnet run --project BookOfEternityClient -- --web
```

Use a custom session root with the existing base-path argument:

```powershell
dotnet run --project BookOfEternityClient -- "E:\Games\The Book of Eternity Reborn\BookOfEternityClient" --web
```

Use a custom local URL:

```powershell
dotnet run --project BookOfEternityClient -- --web --web-url http://127.0.0.1:8788
```

Console mode remains the default:

```powershell
dotnet run --project BookOfEternityClient
```

## Current Browser MVP

The local host exposes:

```text
GET /
GET /api/health
GET /api/session
POST /api/explorer/command
```

`/` serves the first browser command shell. It renders `ExplorerCommandResult` DTOs from the command API and does not duplicate game logic in JavaScript.

The renderer currently supports these DTO surfaces:

- `text`, `panel`, `table`, `list`, `keyValueGrid`, `message`, and `rawJson` blocks.
- `notifications` as message cards.
- `actions` as command buttons when an action has a direct command.
- `prompts` as read-only prompt cards showing prompt text, kind, requirement flag, and selection options.
- empty, loading, HTTP error, and command failure states.

`/api/health` and `/api/session` return local session metadata: status, local-only flag, base path, `game_session` path, and whether the directory exists.

`/api/explorer/command` accepts a JSON body:

```json
{
  "command": "/help"
}
```

It returns an `ExplorerCommandResult` DTO. At this stage only migrated DTO commands are executed, currently `/help` and `/помощь`. Planned, temporary-console-only, unknown, or blocked commands return structured `Blocked`/`Failed` DTOs instead of invoking console-bound handlers.

Interactive multi-step prompt submission, broad command migration, lifecycle/local-turn operations, and QTE support are tracked by the follow-up Web UI issues.

