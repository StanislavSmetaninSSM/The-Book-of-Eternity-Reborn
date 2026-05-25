# Browser Player-Facing Copy and Empty States Design

Tracked issue: [#719](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/719)
Parent: [#718](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/718)

## Context

The current Reborn browser default screen still reads like implementation documentation. The hero headline says `Локальный игровой клиент`, the lead explains C# authority using phrases like `источник истины`, `маршруты`, `состояние интерфейса`, `посмертные контракты`, and `отдельный слой`, and normal no-session/API-missing cases render red `недоступно` cards. Issue #719 asks for the first screen to read like a game entrance while keeping the existing C# client as the gameplay/application authority.

Stanislav explicitly authorized unattended autonomous work for this worker, so the normal brainstorming approval gate is handled by this written design plus self-review instead of waiting for a human approval message.

## Reference and Boundaries

The old React project at `E:\Games\(test-version-0.9.14)-copy-of-the-book-of-eternity_-chronicle-of-the-unwritten-0.9` is used only as UI/UX reference. The applicable patterns are brand-first presentation, central game launcher feel, mode/section hierarchy, contextual save/config actions, and non-technical first-screen copy. Its old mechanics, prompts, API assumptions, and mortal-life-only terminology are not product truth for Reborn.

The Reborn React app remains presentation-only. It may change copy, route presentation, and error/empty-state rendering, but it must not add new gameplay rules, invent save/load behavior, or bypass existing C# endpoints.

## Design

### Hero copy

Replace the default H1 with an atmospheric Reborn title: `Книга Вечности: Перерождение`. Keep locality secondary in the eyebrow or supporting text, not as the headline. Replace the architecture lead with player-facing prose that frames the browser as the opened local book and mentions safe continuity without using the banned technical phrases from #719.

The hero status card remains a concise state summary, but fallback/no-session text should use soft book/game language such as `Книга ждёт открытия` or `Состояние книги уточняется`, not developer wording.

### Normal empty states vs. real errors

Introduce a reusable player-facing empty-state component for normal unavailable data. When a route cannot render because the game screen/menu/session is absent, the UI should present a calm next step: open the main page, start or continue a chapter, load a save, or wait for the local book to finish preparing. This component uses a neutral `.empty-state` style, not `.error-notice`, and it does not expose technical details.

Keep `ErrorNotice` for real shell/API failures and advanced diagnostics. Technical details continue to appear only after explicit advanced mode is enabled.

### Route-specific copy changes

- Home route: if the main menu result is unavailable, render a neutral launcher empty state instead of `Главное меню недоступно`.
- Game route: if the game screen result is unavailable, tell the player that the chapter has not been opened yet and point them to the main page or save/session actions.
- Soul, World, Media: use locked/awaiting-book copy that explains these sections fill in after a chapter/session is available.
- Settings: if menu/options are unavailable, present local comfort settings as preparing rather than broken.
- Sidebar session/game/audio failures are not the main target of #719, but any normal default copy touched by this issue should avoid multiplying red `недоступно` states.

### Guard tests

Add focused source guard coverage in `BrowserFrontendWorkspaceTests` that asserts:

1. the player-facing app source does not contain #719 banned phrases in default hero copy;
2. the default H1 is not `Локальный игровой клиент`;
3. normal no-session/no-route cases use a reusable neutral empty-state component and do not render multiple default `недоступно` titles;
4. technical details remain gated behind `advancedEnabled`.

These are source guards rather than browser automation because #719 is a copy and state-separation slice. Visual artifact work remains tracked by #723.

## Testing and Verification

Run the focused guard test first and watch it fail before editing production code. Then update React copy/CSS minimally, run the focused test again, and run:

```bash
npm run verify --prefix BookOfEternityClient.WebFrontend
dotnet test BookOfEternityClient.Tests/BookOfEternityClient.Tests.csproj --no-restore --filter "FullyQualifiedName~BrowserFrontendWorkspaceTests|FullyQualifiedName~LocalWebUi" --logger "console;verbosity=minimal"
git diff --check
```

A Vite preview visual smoke is useful after the build, but #719 does not claim reusable screenshot artifacts; those belong to #723.

## Self-Review

- Placeholder scan: no placeholder, TODO, or unresolved TBD text remains.
- Internal consistency: the design changes only React presentation/source guards and preserves C# authority.
- Scope check: this is one closure unit for #719; launcher CTA hierarchy (#720), icon system (#721), sidebar redesign (#722), and artifact workflow (#723) remain separate child tasks.
- Ambiguity check: normal absent data uses neutral empty-state copy; real failures keep `ErrorNotice` and advanced-gated details.
