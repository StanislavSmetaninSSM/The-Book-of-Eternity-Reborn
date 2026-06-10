# Implementation Plan: NPC detail-section drill-down menus

**Source issue**: #946 https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/946

**Branch**: `work/946-npc-drilldown`

**Spec**: `specs/946-npc-drilldown/spec.md`

## Summary

Add focused read-only drill-down sections for selected NPCs so the existing long `/npc` overview remains available but is no longer the only way to inspect thoughts/journals, personal quests, activities, relationships, skills/effects/inventory, and memory/custom-state data. Keep console/browser parity explicit and keep C# as the command/result authority.

## Technical Context

- Main code: C#/.NET 8 in `BookOfEternityClient/`.
- Tests: xUnit in `BookOfEternityClient.Tests/`.
- Console NPC flow currently lives mainly in `BookOfEternityClient/UI/ExplorerMode/ExplorerMode.Npcs.ListAndDetails.cs` and render helpers in `ExplorerMode.Npcs.Rendering.cs`.
- Browser/read-only command pipeline uses `BookOfEternityClient/UI/ExplorerMortalWorldCommandResultBuilder.cs`, `ExplorerCommandResult`, and `UiBlock` DTOs.
- #928 added `BookOfEternityClient/UI/NpcJournalFallbackProjection.cs`; preserve that fallback and do not treat journal-only fallback as mutating NPC authority.
- Relevant fixture data may be created in tests rather than changing tracked `FileSystemExample/game_session` unless needed.

## Architecture Decision

Prefer a shared read-only NPC detail-section projection that can feed both console and browser command-result surfaces. The projection should gather availability/count/status hints for known NPC supplementary data files, produce focused section content blocks, and avoid duplicating parser logic in React. Console can present a second-level selection prompt after the existing overview; browser should receive typed section summaries/detail blocks or action metadata from C#, not React-side gameplay rules.

## Files likely to change

- `BookOfEternityClient/UI/ExplorerMode/ExplorerMode.Npcs.ListAndDetails.cs` — selected NPC second-level section flow and navigation.
- `BookOfEternityClient/UI/ExplorerMode/ExplorerMode.Npcs.Rendering.cs` — extraction/reuse of focused section renderers if needed.
- `BookOfEternityClient/UI/NpcDetailSectionProjection.cs` or similar new focused helper — shared read-only section projection for console/browser.
- `BookOfEternityClient/UI/ExplorerMortalWorldCommandResultBuilder.cs` — browser `/npc` command-result section affordances/detail blocks if existing DTOs are sufficient.
- `BookOfEternityClient/CommandProtocol/*` or browser action metadata only if a typed read-only detail action is required.
- `BookOfEternityClient.Tests/*Npc*`, `ExplorerWebCommandServiceTests`, `GameInterfaceTests`, or `ExplorerModeSourceGuardTests` — focused RED/GREEN coverage for console/browser drill-down semantics.
- `CLI_API_Specification.md`, `Rules/*`, or `Examples/*` only if the player-facing command contract/capability changes in a way documented there.
- `specs/946-npc-drilldown/*` — keep tasks/evidence synchronized.

## Implementation Slices

1. **RED tests/projection contract**: Add tests for a rich NPC with journal/thought data, one personal quest with objectives/rewards/failure consequences, and one activity. Tests should prove the current command/console surface lacks separate section affordances.
2. **Shared projection**: Implement a minimal read-only projection for section availability, counts/hints, and focused player-facing section blocks.
3. **Console flow**: Wire the selected NPC flow so the existing overview remains available and populated sections can be opened from a second-level menu, with back navigation.
4. **Browser parity**: Wire `/npc` command-result output to expose equivalent section-level affordances/detail content via C# DTOs/action metadata. If full interactive UI is too large, create a linked follow-up before closure and keep default output player-facing.
5. **Docs/spec reconciliation**: Update player-facing docs/examples only if the command capability is documented or GM/player guidance changes. Mark Spec Kit tasks only after evidence exists.
6. **Review/verification**: Run focused gates, build(s), static scan, independent review, PR, squash merge, and issue closure.

## Verification Commands

Baseline before implementation should be recorded in `tasks.md` after the first run. Planned gates:

```bash
# focused NPC/command/browser/console slice
 dotnet test BookOfEternityClient.Tests/BookOfEternityClient.Tests.csproj -p:IsTestProject=true --filter "FullyQualifiedName~Npc|FullyQualifiedName~ExplorerWebCommandServiceTests|FullyQualifiedName~GameInterfaceTests|FullyQualifiedName~ExplorerModeSourceGuardTests" --logger "console;verbosity=minimal"

# after implementation, run any narrower new #946 tests explicitly
 dotnet test BookOfEternityClient.Tests/BookOfEternityClient.Tests.csproj -p:IsTestProject=true --filter "<new #946 test names>" --logger "console;verbosity=minimal"

# builds
 dotnet build BookOfEternityClient/BookOfEternityClient.csproj --no-restore
 dotnet build BookOfEternityClient.Tests/BookOfEternityClient.Tests.csproj --no-restore -p:IsTestProject=true

# Spec Kit and hygiene
 powershell.exe -NoProfile -ExecutionPolicy Bypass -File .specify/scripts/powershell/check-prerequisites.ps1 -Json -RequireTasks -IncludeTasks
 git diff --check origin/main...HEAD
```

Run `npm run verify --prefix BookOfEternityClient.WebFrontend` if React/frontend files change.

## Risks and Non-goals

- Do not remove or shrink the existing full NPC overview without a separate tracked issue.
- Do not enable mutating NPC social/trade flows from read-only section data.
- Do not introduce browser React gameplay logic; use C# command-result/metadata authority.
- Do not broaden this into the #948 mortal-wide drill-down audit or #947 books flow.
- Do not change afterlife contracts or GM-authored pending/control surfaces.
