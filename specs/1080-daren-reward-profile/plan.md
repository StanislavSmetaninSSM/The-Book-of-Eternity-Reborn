# Implementation Plan: Daren Reward Profile Presentation

**Branch**: `codex/1080-daren-reward-profile` | **Date**: 2026-06-17 | **Spec**: [spec.md](spec.md)

**Input**: Feature specification from `specs/1080-daren-reward-profile/spec.md`

## Summary

Finish GitHub issue #1080 by adding a shared player-facing Daren reward profile summary that explains the saved best tier, current result relationship, Ink Feather bonus, future New Game timing, and non-downgrade/non-stacking behavior. Follow-up live testing also requires removing the hidden unsafe-route hard failure so the final reward tier is determined by normalized score, then making the console final screen readable as a structured result.

## Technical Context

**Language/Version**: C#/.NET 8, TypeScript/React/Vite

**Primary Dependencies**: Spectre.Console, existing local web UI DTOs, Vitest, xUnit

**Storage**: Existing file-backed `client_profile/qte_showcase_rewards.json`; no schema migration required

**Testing**: xUnit via `dotnet test`, frontend player-facing suite via `npm run test:player-facing`, TypeScript/Vite build

**Target Platform**: Local Windows console client and loopback browser client

**Project Type**: Local game client with console and browser UI

**Performance Goals**: Runtime summary derivation must be negligible compared with existing Daren state rendering

**Constraints**: Preserve Daren thresholds, route/action ids, score deltas, profile schema, New Game grant behavior, and client-owned boundary. Do not preserve the hidden rule that any failed QTE step forces `no_reward_failure`.

**Scale/Scope**: One Daren reward/profile surface across shared C# authority and browser UI projection

**Source Issue(s)**: #1080 https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/1080

**Contract Scope**: player-facing, console, browser, frontend, runtime-state projection; no GM-authored contract changes

**Verification Commands**:

- `dotnet test BookOfEternityClient.Tests\BookOfEternityClient.Tests.csproj --no-restore --filter "Daren"`
- `dotnet test BookOfEternityClient.Tests\BookOfEternityClient.Tests.csproj --no-restore --filter "DarenShowcaseAttempt_PreHideoutFailureUsesScoreTierAndWritesPermanentReward|DarenConsoleCompletion_ResponseIsStructuredForPlayerReadability"`
- `npm run test:player-facing` from `BookOfEternityClient.WebFrontend/`
- `npm run build` from `BookOfEternityClient.WebFrontend/`

## Constitution Check

- **GitHub traceability**: Pass. Source issue #1080 is linked in spec, plan, and tasks.
- **Spec Kit fit**: Pass. The issue changes player-facing UX and console/browser parity.
- **Player-facing integrity**: Pass. Russian in-world copy and no debug/API terms are required.
- **Contract/state authority**: Pass. Summaries are derived from the canonical Daren reward profile and current ending; the score-derived ending resolver owns reward tier selection; no GM-authored behavior changes.
- **Test-first path**: Pass. Add failing C# and frontend tests before implementation.
- **Verification evidence**: Pass. Focused Daren, frontend player-facing tests, and frontend build are listed.
- **Agent orchestration**: Pass. Superpowers TDD and verification are the execution method.

## Project Structure

### Documentation (this feature)

```text
specs/1080-daren-reward-profile/
├── spec.md
├── plan.md
├── tasks.md
└── checklists/requirements.md
```

### Source Code (repository root)

```text
BookOfEternityClient/Services/DarenQteRewardProfileService.cs
BookOfEternityClient/Services/QteSceneService.Daren.cs
BookOfEternityClient/WebUi/QteWebInteractionService.cs
BookOfEternityClient.Tests/DarenQteShowcaseTests.cs
BookOfEternityClient.Tests/BrowserApiContractTests.cs
BookOfEternityClient.WebFrontend/src/api/contracts.ts
BookOfEternityClient.WebFrontend/src/components/DarenShowcaseView.tsx
BookOfEternityClient.WebFrontend/test/darenShowcase.test.tsx
```

**Structure Decision**: Keep reward/profile meaning in shared C# authority, project it through the existing browser DTO, and keep frontend rendering presentational.

## Follow-up Design Decision

The reopened #1080 report changes the mechanical contract in one narrow place: `HadUnsafeRouteFailure` must no longer force `no_reward_failure`. Failed steps remain meaningful because they already apply negative score and metric deltas. The resolver should therefore select `shadow_on_the_run`, `broken_trail`, `clean_heist`, or `perfect_shadow` solely from the normalized score threshold, with `no_reward_failure` reserved for scores below the first tier.

The console completion panel should stop acting as a prose dump. It should show a compact structured result:

- current outcome and score;
- permanent reward/profile consequence;
- one epilogue paragraph;
- boundary/reward notices already present in the mode.

## Complexity Tracking

No constitution violations.
