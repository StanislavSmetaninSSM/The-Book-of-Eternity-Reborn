# Implementation Plan: GM Turn Helper Harness

Source issue: https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/1276

## Approach

Keep the GM as the authority for story and state decisions, but remove avoidable ceremony from file writes and terminal markers. Add a small repo-owned PowerShell helper and a daemon-generated session bootstrap that binds it to the current `game_session`.

## Implementation Steps

1. Add failing tests/source guards for the helper functions and daemon prompt wiring.
2. Add `BookOfEternityClient/Launcher/GM_Turn_Helper.ps1`.
3. Make `game_master_daemon.ps1` write `game_state/control/gm_turn_helper.bootstrap.ps1` for the active session.
4. Include a concise helper directive in normal turn, repair, and terminal-protocol prompts.
5. Update GM-facing daemon/help docs.
6. Run focused tests and repeat a live Chaos Sea turn.

## Verification

```powershell
dotnet test BookOfEternityClient.Tests\BookOfEternityClient.Tests.csproj --no-restore --filter "FullyQualifiedName~GmTurnHelperContractTests"
```

Then repeat the live Agent Console + ConPTY bridge + daemon Chaos Sea turn and inspect timeout/repair behavior.
