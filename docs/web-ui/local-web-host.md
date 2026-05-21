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

## Current Skeleton

The initial host exposes:

```text
GET /
GET /api/health
GET /api/session
```

`/` serves a basic browser shell. `/api/health` and `/api/session` return local session metadata: status, local-only flag, base path, `game_session` path, and whether the directory exists.

Command execution, browser rendering, interactive protocols, and QTE support are tracked by the follow-up Web UI issues.

